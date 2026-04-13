using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.Philips.M1350.DataLink;

namespace MDI.Benchmarks.Philips.M1350;

/// <summary>
/// Characterises the throughput of <see cref="DataBlockEncoder.Encode"/> across a
/// range of payload sizes and DLE-escape densities.
/// </summary>
/// <remarks>
/// <para>
/// <strong>What is being measured?</strong>
/// <see cref="DataBlockEncoder.Encode"/> is a pure byte-by-byte loop that DLE-escapes its
/// input into a caller-owned destination span. It performs no allocation of its own — the
/// <c>destination</c> span is pre-allocated in <see cref="Setup"/> and reused across all
/// iterations, so the <c>Allocated</c> column should be <c>-</c> for all cases.
/// </para>
/// <para>
/// <strong>Why <see cref="MemoryDiagnoserAttribute"/>?</strong>
/// To confirm the zero-allocation contract. Any non-zero <c>Allocated</c> value would
/// indicate an unexpected regression in the encoder itself.
/// </para>
/// <para>
/// <strong>Why these sizes?</strong>
/// <list type="bullet">
///   <item><description>
///     <c>0</c> — empty input; the foreach loop does not execute. Measures the fixed
///     per-call overhead of the method and establishes a floor for the other sizes.
///     All <see cref="PercentEscapes"/> values are equivalent at this size (degenerate
///     combinations, but kept so the results table is complete).
///   </description></item>
///   <item><description>
///     <c>32</c> — small realistic M1350 frame payload.
///   </description></item>
///   <item><description>
///     <c><see cref="DataBlockConstants.StackallocByteThreshold"/></c> (256) — the exact
///     boundary at which <see cref="DataBlockWriter.WriteData"/> switches from
///     <c>stackalloc</c> to <see cref="ArrayPool{T}"/>. Benchmarking at this size
///     measures the encoder's raw cost at the point that is most sensitive to
///     the caller's allocation budget.
///   </description></item>
///   <item><description>
///     <c>4096</c> — large input; exercises the encode loop at a size that stresses
///     throughput without making the full benchmark run impractically slow.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Why these escape densities?</strong>
/// <list type="bullet">
///   <item><description>
///     <c>0</c> — no DLE bytes; the <c>if (b == DLE)</c> branch never fires. Measures
///     the best-case throughput (pure copy).
///   </description></item>
///   <item><description>
///     <c>0.1</c> — one byte in ten is a DLE; representative of light escape traffic in
///     real M1350 waveform payloads.
///   </description></item>
///   <item><description>
///     <c>0.5</c> — every other byte is a DLE; worst-case input that maximises branch
///     mispredictions and output expansion. The destination is sized for the theoretical
///     maximum (<c>2 × input length</c>) so no bounds errors occur even here.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class DataBlockEncoderBenchmark
{
    private Memory<byte> value;
    private Memory<byte> destination;

    /// <summary>
    /// Number of raw (pre-escape) bytes passed to <see cref="DataBlockEncoder.Encode"/>.
    /// </summary>
    [Params(0, 32,
        DataBlockConstants.StackallocByteThreshold,
        4096
    )]
    public int Size { get; set; }

    /// <summary>
    /// Fraction of input bytes that are set to <see cref="DataBlockConstants.DLE"/> (0x10),
    /// requiring a two-byte escaped output sequence. <c>0</c> exercises the best-case copy
    /// path; <c>0.5</c> exercises the worst-case expansion path.
    /// </summary>
    [Params(0, 0.1, 0.5)]
    public double PercentEscapes { get; set; }

    /// <summary>
    /// Allocates and fills <see cref="value"/> with random DLE bytes at the requested
    /// density, and sizes <see cref="destination"/> to the theoretical worst-case encoded
    /// length. Both buffers are reused for all iterations of a given parameter combination.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.value = new byte[this.Size];

        int length = DataBlockEncoder.GetMaxEscapedLength(this.value.Length);
        this.destination = new byte[length];

        for (int i = 0; i < this.value.Length; i++)
        {
            if (Random.Shared.NextDouble() < this.PercentEscapes)
            {
                this.value.Span[i] = DataBlockConstants.DLE;
            }
        }
    }

    /// <summary>
    /// Encodes <see cref="value"/> into the pre-allocated <see cref="destination"/> span.
    /// The <see cref="OperationStatus"/> return value is consumed to prevent dead-code
    /// elimination. The <c>Allocated</c> column should be <c>-</c> at all sizes because
    /// the encoder writes into a caller-provided span and performs no heap allocation.
    /// </summary>
    [Benchmark(Baseline = true)]
    public OperationStatus Encode()
    {
        return DataBlockEncoder.Encode(this.value.Span, this.destination.Span, out _);
    }
}
