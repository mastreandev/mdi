using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.IO.Encoding.Philips.M1350;

namespace MDI.Benchmarks.IO.Encoding.Philips.M1350;

[MemoryDiagnoser]
[ShortRunJob]
public class DataBlockReaderBenchmark
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;
    private byte[] value = [];

    public DataBlockReaderBenchmark()
    {
        this.writer = new DataBlockWriter(this.output);
    }

    [Params(32, 256, 1024)]
    public int Size { get; set; } = 1024;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.value = new byte[this.Size];

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
