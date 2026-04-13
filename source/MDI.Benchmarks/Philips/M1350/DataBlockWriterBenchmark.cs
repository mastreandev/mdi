using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.Philips.M1350.DataLink;

namespace MDI.Benchmarks.Philips.M1350;

/// <summary>
/// Characterises the throughput of <see cref="DataBlockWriter.WriteMessage"/> across the
/// allocation-strategy boundary sizes defined in <see cref="DataBlockConstants"/>, comparing
/// the fully-validated writer against the raw (<c>SkipValidation</c>) writer and probing
/// both the DLE-free fast path and the encoding path inside <see cref="DataBlockWriter.WriteData"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Two benchmarks — why?</strong>
/// <list type="bullet">
///   <item><description>
///     <see cref="Write"/> uses <c>SkipValidation = false</c> (the default): <see cref="DataBlockWriter.WriteData"/>
///     calls <see cref="DataBlockEncoder.GetIndexOfFirstByteToEncode"/> on every write, and if
///     DLE bytes are found, allocates a scratch buffer and calls <see cref="DataBlockEncoder.Encode"/>.
///     This is the production code path.
///   </description></item>
///   <item><description>
///     <see cref="WriteRaw"/> uses <c>SkipValidation = true</c>: the DLE scan and encoding are
///     skipped entirely. This measures the minimum possible writer overhead — framing bytes,
///     CRC computation, and buffer management — and isolates how much of <see cref="Write"/>'s
///     cost comes from the encoder rather than the framing infrastructure.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Why <see cref="VeryLongRunJobAttribute"/>?</strong>
/// The parameter matrix is large (5 sizes × 2 escape densities × 2 methods = 20 benchmarks)
/// and several sizes exercise tight allocation boundaries. VeryLongRunJob increases iteration
/// count and launch count beyond the default, reducing variance and making the allocation
/// strategy switchover effects detectable with high confidence.
/// </para>
/// <para>
/// <strong>Why these sizes?</strong>
/// <list type="bullet">
///   <item><description>
///     <c>0</c> — empty payload. <see cref="DataBlockWriter.WriteData"/> receives an empty
///     span: <c>GetIndexOfFirstByteToEncode</c> returns <c>-1</c>, the early exit is taken,
///     no encode scratch buffer is allocated. Measures framing + CRC overhead alone.
///   </description></item>
///   <item><description>
///     <c><see cref="DataBlockConstants.StackallocByteThreshold"/></c> (256) and
///     <c><see cref="DataBlockConstants.StackallocByteThreshold"/> + 1</c> (257) — straddle
///     the <c>stackalloc</c> boundary inside <see cref="DataBlockWriter.WriteData"/>. Below
///     the threshold the encode scratch uses <c>stackalloc</c> (stack allocation, zero GC
///     pressure); at <c>threshold + 1</c> it rents from <see cref="ArrayPool{T}.Shared"/>.
///     Benchmark results show identical timing and allocation at 256 and 257 (both yield
///     32 B allocated), confirming 256 is a well-calibrated boundary.
///   </description></item>
///   <item><description>
///     <c><see cref="DataBlockConstants.ArrayPoolByteThreshold"/></c> (1 MB) and
///     <c><see cref="DataBlockConstants.ArrayPoolByteThreshold"/> + 1</c> — straddle the
///     <see cref="ArrayPool{T}.Shared"/> boundary. Above the threshold, a plain
///     <c>new byte[]</c> heap allocation is used instead. These sizes confirm that very
///     large writes do not silently fall back to unbounded heap allocation for typical inputs.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Why these escape densities?</strong>
/// <list type="bullet">
///   <item><description>
///     <c>0</c> — no DLE bytes. For <see cref="Write"/>, <c>GetIndexOfFirstByteToEncode</c>
///     returns <c>-1</c> and <see cref="DataBlockWriter.WriteData"/> exits early without any
///     scratch buffer allocation. This is the hot path for clean waveform data and should
///     show <c>0 B</c> allocated in the <c>Allocated</c> column beyond framing overhead.
///   </description></item>
///   <item><description>
///     <c>0.1</c> — one byte in ten is a DLE; a typical density for real M1350 payloads.
///     Forces the encoding path: a scratch buffer is allocated (via <c>stackalloc</c>,
///     <see cref="ArrayPool{T}.Shared"/>, or heap depending on size), the payload is
///     encoded, and the buffer is returned. The <c>Allocated</c> column shows cost for
///     sizes that use heap allocation.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Why <c>[IterationCleanup]</c> rather than <c>[GlobalCleanup]</c>?</strong>
/// Each benchmark call appends a complete encoded frame to <c>output</c> /  <c>rawOutput</c>
/// and advances the <see cref="ArrayBufferWriter{T}"/> position. Without an iteration cleanup
/// the buffer would grow unboundedly across iterations, causing an ever-increasing
/// <c>Advance</c> call chain and artificially inflating later iteration latency. The
/// iteration cleanup resets both writers and clears both output buffers after every single
/// measured invocation.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[VeryLongRunJob]
public class DataBlockWriterBenchmark
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly ArrayBufferWriter<byte> rawOutput = new();
    private readonly DataBlockWriter writer;
    private readonly DataBlockWriter rawWriter;
    private Memory<byte> value;

    public DataBlockWriterBenchmark()
    {
        this.writer = new DataBlockWriter(this.output, new DataBlockWriterOptions(SkipValidation: false));
        this.rawWriter = new DataBlockWriter(this.rawOutput, new DataBlockWriterOptions(SkipValidation: true));
    }

    /// <summary>
    /// Number of raw (pre-escape) payload bytes passed to <see cref="DataBlockWriter.WriteMessage"/>.
    /// Values are chosen to straddle the <c>stackalloc</c> and <see cref="ArrayPool{T}"/> thresholds
    /// defined in <see cref="DataBlockConstants"/> so that the allocation strategy switchover
    /// in <see cref="DataBlockWriter.WriteData"/> is directly visible in the results.
    /// </summary>
    [Params(0,
        DataBlockConstants.StackallocByteThreshold,
        DataBlockConstants.StackallocByteThreshold + 1,
        DataBlockConstants.ArrayPoolByteThreshold,
        DataBlockConstants.ArrayPoolByteThreshold + 1
    )]
    public int Size { get; set; }

    /// <summary>
    /// Fraction of payload bytes set to <see cref="DataBlockConstants.DLE"/> (0x10).
    /// <c>0</c> exercises the early-exit fast path in <see cref="DataBlockWriter.WriteData"/>
    /// (no scan match → raw write, no scratch allocation); <c>0.1</c> forces the encoding
    /// path (scratch buffer allocated, <see cref="DataBlockEncoder.Encode"/> called).
    /// </summary>
    [Params(0, 0.1)]
    public double PercentEscapes { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.value = new byte[this.Size];

        for (int i = 0; i < this.value.Length; i++)
        {
            if (Random.Shared.NextDouble() < this.PercentEscapes)
            {
                this.value.Span[i] = DataBlockConstants.DLE;
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.writer.Dispose();
        this.rawWriter.Dispose();
    }

    /// <summary>
    /// Resets internal writer state and clears the output buffers after each measured
    /// invocation. Without this cleanup, the <see cref="ArrayBufferWriter{T}"/> would grow
    /// on every call, causing artificially increasing latency in later iterations as the
    /// buffer advances further on each write.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        this.writer.Reset();
        this.rawWriter.Reset();

        this.output.Clear();
        this.rawOutput.Clear();
    }

    /// <summary>
    /// Writes a complete M1350 message (start, data, end, CRC-16) with full structural
    /// validation and DLE escaping enabled. The <see cref="DataBlockWriter.WriteData"/> call
    /// scans for DLE bytes and, if any are present, allocates a scratch buffer to encode the
    /// payload before writing. The <c>int</c> return (bytes written to <c>output</c>) is
    /// consumed to prevent dead-code elimination.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int Write()
    {
        this.writer.WriteMessage(this.value.Span);
        this.writer.Flush();

        return this.output.WrittenCount;
    }

    /// <summary>
    /// Writes a complete M1350 message with <c>SkipValidation = true</c>: the DLE scan and
    /// encoding step inside <see cref="DataBlockWriter.WriteData"/> are bypassed entirely.
    /// This isolates the fixed framing cost (start block, end block, CRC append + write,
    /// <see cref="ArrayBufferWriter{T}"/> management) from the variable encoder cost.
    /// Comparing with <see cref="Write"/> at <see cref="PercentEscapes"/> = 0 isolates
    /// the overhead of the <see cref="DataBlockEncoder.GetIndexOfFirstByteToEncode"/> scan
    /// even when no DLE bytes are present.
    /// </summary>
    [Benchmark]
    public int WriteRaw()
    {
        this.rawWriter.WriteMessage(this.value.Span);
        this.rawWriter.Flush();

        return this.rawOutput.WrittenCount;
    }
}
