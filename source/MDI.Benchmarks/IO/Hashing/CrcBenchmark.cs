using System.IO.Hashing;

using BenchmarkDotNet.Attributes;

using MDI.IO.Hashing;

namespace MDI.Benchmarks.IO.Hashing;

/// <summary>
/// Characterises every distinct public API surface of <see cref="Crc16"/> across a range of
/// payload sizes that reflect realistic Philips M1350 Data-Link-Layer traffic.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why <see cref="MemoryDiagnoserAttribute"/>?</strong>
/// The most important behavioural distinction between the <c>Crc16</c> overloads is allocation:
/// the <c>byte[]</c>-returning static method allocates on every call, while the span and instance
/// overloads are zero-alloc. <see cref="MemoryDiagnoserAttribute"/> makes that visible in the
/// Allocated column so the cost is reported in addition to throughput.
/// </para>
/// <para>
/// <strong>Why these sizes?</strong>
/// <list type="bullet">
///   <item><description>
///     <c>9</c> — smallest plausible frame body after DLE STX / DLE ETX framing overhead.
///     Exercises the non-vectorised small-input path and makes allocation overhead most visible
///     as a ratio of compute cost.
///   </description></item>
///   <item><description>
///     <c>32</c> — typical short status / acknowledgement payload.
///   </description></item>
///   <item><description>
///     <c>256</c> — mid-range waveform or parameter block.
///   </description></item>
///   <item><description>
///     <c>1024</c> — larger aggregate transfer; confirms linear scaling beyond L1 cache.
///   </description></item>
///   <item><description>
///     <c>4096</c> — stress size, well above any realistic single frame, used to verify that
///     throughput scales predictably and to make the <c>Crc32</c> hardware-acceleration gap
///     visible at large input.
///   </description></item>
/// </list>
/// The Philips M1350 protocol uses variable-length DLE-escaped frames with a 2-byte CRC-16/XMODEM
/// trailer; there is no fixed maximum frame body size defined by the protocol specification, so no
/// single canonical size can represent real workloads.
/// </para>
/// <para>
/// <strong>Why <c>crc16</c> is a field, not created per benchmark call?</strong>
/// <c>NonCryptographicHashAlgorithm</c>'s constructor stores a single <c>int</c> after a
/// range check — allocation cost is negligible and not what we are measuring. Allocating inside the
/// benchmark body would conflate construction with hashing. The instance is therefore created once
/// in the field initialiser; state is reset inside each benchmark method that requires it.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class CrcBenchmark
{
    private byte[]? buffer;
    private readonly Crc16 crc16 = new();

    /// <summary>
    /// The number of random bytes to hash per operation. Drives all <c>[Params]</c> matrix rows.
    /// </summary>
    [Params(9, 32, 256, 1024, 4096)]
    public int Size { get; set; }

    /// <summary>
    /// Allocates and fills <see cref="buffer"/> once before all benchmark iterations for this
    /// <see cref="Size"/> value. Using <c>[GlobalSetup]</c> rather than <c>[IterationSetup]</c>
    /// ensures the allocation does not appear in the measured cost.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.buffer = new byte[this.Size];
        Random.Shared.NextBytes(this.buffer);
    }

    /// <summary>
    /// Measures the <em>allocating</em> static overload: <c>Crc16.Hash(byte[])</c>, which creates
    /// and returns a new <c>byte[2]</c> on every call. This is the baseline because it represents
    /// the simplest, most discoverable API entry point, and all other benchmarks are measured as
    /// ratios against it. The <c>Allocated</c> column should show <c>32 B</c> (object header +
    /// 2 data bytes, aligned to 16 bytes on .NET 64-bit) for every size.
    /// </summary>
    [Benchmark(Baseline = true)]
    public byte[] Crc16Hash()
    {
        return Crc16.Hash(this.buffer!);
    }

    /// <summary>
    /// Measures the zero-alloc static overload: <c>Crc16.Hash(ReadOnlySpan&lt;byte&gt;,
    /// Span&lt;byte&gt;)</c>. The two-byte destination is stack-allocated via <c>stackalloc</c>,
    /// which is the idiomatic caller pattern. The return value (number of bytes written) is
    /// consumed to prevent dead-code elimination. The <c>Allocated</c> column should show <c>-</c>
    /// at all sizes.
    /// </summary>
    [Benchmark]
    public int Crc16HashSpan()
    {
        Span<byte> destination = stackalloc byte[2];
        return Crc16.Hash(this.buffer, destination);
    }

    /// <summary>
    /// Measures the zero-alloc try-pattern static overload:
    /// <c>Crc16.TryHash(ReadOnlySpan&lt;byte&gt;, Span&lt;byte&gt;, out int)</c>. This overload
    /// mirrors the BCL <c>TryXxx</c> convention for callers who cannot guarantee destination
    /// length at compile time. The <c>out</c> parameter is discarded with <c>_</c> because the
    /// destination is always exactly 2 bytes; the <c>bool</c> return value is consumed to prevent
    /// dead-code elimination. The <c>Allocated</c> column should show <c>-</c> at all sizes.
    /// </summary>
    [Benchmark]
    public bool Crc16TryHash()
    {
        Span<byte> destination = stackalloc byte[2];
        return Crc16.TryHash(this.buffer, destination, out _);
    }

    /// <summary>
    /// Measures the instance streaming API using the explicit three-step idiom:
    /// <c>Reset()</c> → <c>Append()</c> → <c>GetCurrentHash()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Why <c>Reset()</c> is inside the benchmark body:</strong> BDN calls this method
    /// thousands of times per iteration. <c>[IterationSetup]</c> fires only once per iteration,
    /// not once per invocation, so placing <c>Reset()</c> there would cause every call after the
    /// first to accumulate state across the previous appends — measuring an ever-growing hash
    /// rather than a fixed-size one. <c>Reset()</c> itself is a single field write and contributes
    /// negligible overhead relative to the table-driven byte loop.
    /// </para>
    /// <para>
    /// Compare with <see cref="Crc16InstanceGetHashAndReset"/> to quantify the cost of splitting
    /// the combined operation into separate steps.
    /// </para>
    /// </remarks>
    [Benchmark]
    public void Crc16InstanceAppendGet()
    {
        Span<byte> destination = stackalloc byte[2];
        this.crc16.Reset();
        this.crc16.Append(this.buffer!);
        this.crc16.GetCurrentHash(destination);
    }

    /// <summary>
    /// Measures the instance streaming API using the combined two-step idiom:
    /// <c>Append()</c> → <c>GetHashAndReset()</c>. <c>GetHashAndReset()</c> writes the hash and
    /// resets internal state atomically, saving one explicit method call compared with
    /// <see cref="Crc16InstanceAppendGet"/>. This is the preferred hot-path pattern in
    /// <c>DataBlockWriter</c> and similar callers that hash a single contiguous buffer per frame.
    /// The <c>Allocated</c> column should show <c>-</c> at all sizes.
    /// </summary>
    [Benchmark]
    public void Crc16InstanceGetHashAndReset()
    {
        Span<byte> destination = stackalloc byte[2];
        this.crc16.Append(this.buffer!);
        this.crc16.GetHashAndReset(destination);
    }

    /// <summary>
    /// Reference throughput indicator: <c>Crc32.Hash(byte[])</c> from <c>System.IO.Hashing</c>.
    /// On x86-64 with SSE4.2, the runtime uses the <c>CRC32</c> hardware instruction, making this
    /// orders of magnitude faster than the software table-driven <c>Crc16</c> at large sizes
    /// (e.g., ~72× at 4096 bytes). This benchmark is <em>not</em> a fair algorithmic comparison —
    /// CRC-16/XMODEM cannot use the same instruction because it uses a different polynomial. It is
    /// included solely to make the hardware-acceleration gap visible and to confirm that the
    /// <c>Crc16</c> measurements are plausible relative to a known-good BCL baseline.
    /// </summary>
    [Benchmark]
    public byte[] Crc32Hash()
    {
        return Crc32.Hash(this.buffer!);
    }
}
