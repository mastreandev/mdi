using System.IO.Hashing;

using BenchmarkDotNet.Attributes;

using MDI.IO.Hashing;

namespace MDI.Benchmarks.IO.Hashing;

[MemoryDiagnoser]
[ShortRunJob]
public class CrcBenchmark
{
    private byte[]? buffer;

    [Params(32, 256, 1024)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.buffer = new byte[this.Size];
        Random.Shared.NextBytes(this.buffer);
    }

    [Benchmark]
    public byte[] Crc16Hash()
    {
        return Crc16.Hash(this.buffer!);
    }

    [Benchmark(Baseline = true)]
    public byte[] Crc32Hash()
    {
        return Crc32.Hash(this.buffer!);
    }
}
