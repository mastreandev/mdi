using System.Buffers;

using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350.DataLink;

[TestClass]
public sealed partial class DataBlockReaderTests : IDisposable
{
    private static readonly byte[] Buffer = new byte[8];

    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;

    public DataBlockReaderTests()
    {
        this.writer = new DataBlockWriter(this.output);
    }

    private static void Check(ReadOnlyMemory<byte> memory, int expectedBlockCount = 0, int expectedBufferLength = 0)
    {
        ReadOnlySequence<byte> buffer = new(memory);

        int actualBlockCount = 0;
        while (DataBlockReader.TryRead(ref buffer, out _))
        {
            actualBlockCount++;
        }

        Assert.AreEqual(expectedBlockCount, actualBlockCount);
        Assert.AreEqual(expectedBufferLength, buffer.Length);
    }

    public void Dispose()
    {
        this.writer.Dispose();
    }
}
