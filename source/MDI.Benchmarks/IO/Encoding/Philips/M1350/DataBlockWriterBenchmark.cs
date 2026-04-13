using System.Buffers;

using BenchmarkDotNet.Attributes;

using MDI.IO.Encoding.Philips.M1350;

namespace MDI.Benchmarks.IO.Encoding.Philips.M1350;

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

    [Params(0,
        DataBlockConstants.StackallocByteThreshold,
        DataBlockConstants.StackallocByteThreshold + 1,
        DataBlockConstants.ArrayPoolByteThreshold,
        DataBlockConstants.ArrayPoolByteThreshold + 1
    )]
    public int Size { get; set; }

    [Params(0.1)]
    public double PercentEscapes { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.value = new byte[this.Size];

        for (int i = 0; i < this.value.Length; i++)
        {
            if (Random.Shared.NextDouble() <= this.PercentEscapes)
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

    [IterationCleanup]
    public void IterationCleanup()
    {
        this.writer.Reset();
        this.rawWriter.Reset();

        this.output.Clear();
        this.rawOutput.Clear();
    }

    [Benchmark(Baseline = true)]
    public int Write()
    {
        this.writer.WriteMessage(this.value.Span);
        this.writer.Flush();

        return this.output.WrittenCount;
    }

    [Benchmark]
    public int WriteRaw()
    {
        this.rawWriter.WriteMessage(this.value.Span);
        this.rawWriter.Flush();

        return this.rawOutput.WrittenCount;
    }
}
