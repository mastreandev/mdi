using System.Buffers;

using MDI.IO.Encoding.Philips.M1350;
using MDI.IO.Hashing;

namespace MDI.Tests.IO.Encoding.Philips.M1350.Reader;

[TestClass]
public sealed class AdvancedReaderTests : IDisposable
{
    private static readonly byte[] Buffer = new byte[8];

    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;

    public AdvancedReaderTests()
    {
        this.writer = new DataBlockWriter(this.output, new() { SkipValidation = true });
    }

    [TestMethod]
    public void MultipleBlocksWithBadCrcShouldDiscardToNextBlock()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.Flush();

        this.output.Write(new byte[] { 0x99, 0x99 });

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 0, 12);
    }

    [TestMethod]
    public void DleEtxWithoutStartShouldNotBeAcceptedAsBlock()
    {
        byte[] prefix = DataBlockConstants.EndBlock.ToArray();
        byte[] crc = Crc16.Hash(prefix);
        byte[] data =
        [
            .. prefix,
            .. crc,
        ];

        ReadOnlySequence<byte> buffer = new(data);

        bool result = DataBlockReader.TryRead(ref buffer, out _);

        Assert.IsFalse(result);
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
