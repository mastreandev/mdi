using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.IO.Encoding.Philips.M1350.DataLink;

namespace MDI.Benchmarks.IO.Encoding.Philips.M1350;

[MemoryDiagnoser]
[ShortRunJob]
public class DataBlockEncoderBenchmark
{
    private Memory<byte> value;
    private Memory<byte> destination;

    [Params(0,
        DataBlockConstants.StackallocByteThreshold,
        DataBlockConstants.ArrayPoolByteThreshold
    )]
    public int Size { get; set; }

    [Params(0, 0.1, 0.25, 0.5)]
    public double PercentEscapes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.value = new byte[this.Size];

        int length = DataBlockEncoder.GetMaxEscapedLength(this.value.Length);
        this.destination = new byte[length];

        for (int i = 0; i < this.value.Length; i++)
        {
            if (Random.Shared.NextDouble() <= this.PercentEscapes)
            {
                this.value.Span[i] = DataBlockConstants.DLE;
            }
        }
    }

    [Benchmark(Baseline = true)]
    public OperationStatus Encode()
    {
        return DataBlockEncoder.Encode(this.value.Span, this.destination.Span, out _);
    }
}
