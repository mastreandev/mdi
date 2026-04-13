using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.IO.Encoding.Philips.M1350;

namespace MDI.Benchmarks.IO.Encoding.Philips.M1350;

[MemoryDiagnoser]
[ShortRunJob]
public sealed class DataBlockReaderBenchmark : IDisposable
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;

    public DataBlockReaderBenchmark()
    {
        this.writer = new DataBlockWriter(this.output);
    }

    // [GlobalSetup]
    // public void Setup()
    // {
    //     writer.WriteStartBlock();
    //     writer.WriteData(Size);
    //     writer.WriteEndBlock();
    //     writer.WriteCrc();
    // }

    [Params(32, 256, 1024)]
    public int Size { get; set; } = 1024;

    public void Dispose()
    {
        this.writer.Dispose();
    }

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
