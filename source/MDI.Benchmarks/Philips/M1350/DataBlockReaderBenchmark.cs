using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.Philips.M1350.DataLink;

namespace MDI.Benchmarks.Philips.M1350;

/// <summary>
/// Characterises the throughput of <see cref="DataBlockReader.TryRead"/> across a range of
/// payload sizes and DLE-escape densities, including both the fast path (no escaped bytes)
/// and the slow path (DLE-escaped payload that allocates a decoded <c>byte[]</c>).
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is being measured?</strong>
/// Each benchmark invocation calls <see cref="DataBlockReader.TryRead"/> on a
/// <see cref="ReadOnlySequence{T}"/> that contains exactly one complete, CRC-valid M1350
/// frame written by <see cref="DataBlockWriter"/>. The sequence is reconstructed from
/// <see cref="output"/> on every call, so <c>TryRead</c> always starts from the beginning
/// of the frame. The while-loop calls <c>TryRead</c> a second time after the frame is
/// consumed; that second call immediately returns <see langword="false"/> (nothing remains),
/// so the benchmark cost is dominated by the one successful parse + CRC verification.
/// The <see langword="bool"/> return value is forwarded to prevent dead-code elimination.
/// </para>
/// <para>
/// <strong>Two distinct code paths exercised by <see cref="PercentEscapes"/>:</strong>
/// <list type="bullet">
///   <item><description>
///     <c>0</c> — no DLE bytes in the payload. <see cref="DataBlockReader.TryRead"/> sets
///     <c>escapedDleSeen = false</c> and returns the raw payload slice directly as a
///     <see cref="ReadOnlySequence{T}"/> without allocating. The <c>Allocated</c> column
///     should reflect only the <c>Crc16</c> struct and small locals — no heap allocation
///     for the payload itself.
///   </description></item>
///   <item><description>
///     <c>&gt; 0</c> — some DLE bytes are present, which the writer DLE-escapes during
///     framing. The reader detects <c>escapedDleSeen = true</c> and calls the private
///     <c>DecodeEscapedPayload</c> path, which allocates a new <c>byte[]</c> for the
///     decoded output and performs two passes over the payload span. The <c>Allocated</c>
///     column will show a non-zero value proportional to payload size.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Why these sizes?</strong>
/// <c>[9, 32, 256, 1024, 4096]</c> are the same sizes used in <c>CrcBenchmark</c>
/// for consistency. Size 9 is the smallest plausible M1350 payload after framing overhead;
/// 4096 confirms that <c>SequenceReader</c> scanning scales linearly at large inputs.
/// </para>
/// <para>
/// <strong>Setup strategy:</strong>
/// <see cref="GlobalSetup"/> writes a single complete frame (start marker, DLE-escaped
/// data, end marker, CRC-16) into <see cref="output"/> using <see cref="DataBlockWriter"/>.
/// The frame therefore contains valid CRC bytes so the reader's <c>BinaryPrimitives.ReadUInt16BigEndian(crc) == 0</c>
/// check passes and <c>TryRead</c> returns <see langword="true"/>. The output and writer
/// are explicitly reset at the start of each <see cref="GlobalSetup"/> call so that the
/// benchmark produces consistent results even if the same instance is reused across
/// parameter combinations by BenchmarkDotNet.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class DataBlockReaderBenchmark
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;
    private byte[] value = [];

    public DataBlockReaderBenchmark()
    {
        this.writer = new DataBlockWriter(this.output);
    }

    /// <summary>
    /// Number of raw (pre-escape) data bytes in the M1350 frame payload.
    /// </summary>
    [Params(9, 32, 256, 1024, 4096)]
    public int Size { get; set; } = 1024;

    /// <summary>
    /// Fraction of payload bytes set to <see cref="DataBlockConstants.DLE"/> (0x10) before
    /// framing. <c>0</c> exercises the zero-allocation fast path; any non-zero value forces
    /// the reader into <c>DecodeEscapedPayload</c>, which allocates a decoded <c>byte[]</c>.
    /// </summary>
    [Params(0, 0.1, 0.5)]
    public double PercentEscapes { get; set; }

    /// <summary>
    /// Resets the output buffer and writer, then writes a single complete M1350 frame
    /// containing <see cref="Size"/> payload bytes at the specified <see cref="PercentEscapes"/>
    /// DLE density. The written frame is CRC-valid so <c>TryRead</c> will return
    /// <see langword="true"/> exactly once per benchmark invocation.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.output.Clear();
        this.writer.Reset();

        this.value = new byte[this.Size];

        for (int i = 0; i < this.value.Length; i++)
        {
            if (Random.Shared.NextDouble() < this.PercentEscapes)
            {
                this.value[i] = DataBlockConstants.DLE;
            }
        }

        this.writer.WriteStart();
        this.writer.WriteData(this.value);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.writer.Dispose();
    }

    /// <summary>
    /// Parses one complete M1350 frame from a <see cref="ReadOnlySequence{T}"/> view of
    /// the pre-encoded output buffer. The sequence is reconstructed each invocation so the
    /// reader always starts at the beginning of the frame. The <see langword="bool"/> return
    /// (always <see langword="false"/> — the sequence is empty after the frame is consumed)
    /// is returned to prevent dead-code elimination; the meaningful work happens on the first
    /// loop iteration where <c>TryRead</c> returns <see langword="true"/>.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool TryRead()
    {
        ReadOnlyMemory<byte> buffer = this.output.WrittenMemory;
        ReadOnlySequence<byte> sequence = new(buffer);

        bool result;
        while (result = DataBlockReader.TryRead(ref sequence, out ReadOnlySequence<byte> block))
        {
            _ = block.Length;
        }

        return result;
    }
}
