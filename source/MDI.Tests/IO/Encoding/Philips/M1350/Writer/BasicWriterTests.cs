using System.Buffers;
using System.Buffers.Binary;

using MDI.IO.Encoding.Philips.M1350;
using MDI.IO.Hashing;

namespace MDI.Tests.IO.Encoding.Philips.M1350.Writer;

[TestClass]
public sealed class BasicWriterTests : IDisposable
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter subject;

    public BasicWriterTests()
    {
        this.subject = new DataBlockWriter(this.output);
    }

    public void Dispose()
    {
        this.subject.Dispose();
    }

    [TestMethod]
    public void ShouldWriteIndividualBlocks()
    {
        this.subject.WriteStart();
        this.subject.WriteData(Constants.KnownMessageBytes);
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();

        ReadOnlySpan<byte> startBlockBytes = this.output.WrittenSpan[0..2];
        ushort startBlockValue = BinaryPrimitives.ReadUInt16BigEndian(startBlockBytes);
        ushort expectedStartBlockValue = BinaryPrimitives.ReadUInt16BigEndian(DataBlockConstants.StartBlock);
        Assert.AreEqual(expectedStartBlockValue, startBlockValue);

        ReadOnlySpan<byte> endBlockBytes = this.output.WrittenSpan[^4..^2];
        ushort endBlockValue = BinaryPrimitives.ReadUInt16BigEndian(endBlockBytes);
        ushort expectedEndBlockValue = BinaryPrimitives.ReadUInt16BigEndian(DataBlockConstants.EndBlock);
        Assert.AreEqual(expectedEndBlockValue, endBlockValue);

        ReadOnlySpan<byte> crcBytes = this.output.WrittenSpan[^2..];
        ushort crcValue = BinaryPrimitives.ReadUInt16BigEndian(crcBytes);
        Assert.AreEqual(Constants.KnownMessageCrc, crcValue);
    }

    [TestMethod]
    public void ShouldWriteExampleMessage()
    {
        this.subject.WriteMessage(Constants.KnownMessageBytes);
        this.subject.Flush();

        Assert.IsTrue(this.output.WrittenCount > 0);

        ReadOnlySpan<byte> startBlockBytes = this.output.WrittenSpan[0..2];
        ushort startBlockValue = BinaryPrimitives.ReadUInt16BigEndian(startBlockBytes);
        ushort expectedStartBlockValue = BinaryPrimitives.ReadUInt16BigEndian(DataBlockConstants.StartBlock);
        Assert.AreEqual(expectedStartBlockValue, startBlockValue);

        ReadOnlySpan<byte> endBlockBytes = this.output.WrittenSpan[^4..^2];
        ushort endBlockValue = BinaryPrimitives.ReadUInt16BigEndian(endBlockBytes);
        ushort expectedEndBlockValue = BinaryPrimitives.ReadUInt16BigEndian(DataBlockConstants.EndBlock);
        Assert.AreEqual(expectedEndBlockValue, endBlockValue);

        ReadOnlySpan<byte> crcBytes = this.output.WrittenSpan[^2..];
        ushort crcValue = BinaryPrimitives.ReadUInt16BigEndian(crcBytes);
        Assert.AreEqual(Constants.KnownMessageCrc, crcValue);
    }

    [TestMethod]
    public void ShouldWriteCrcForFramedBytes()
    {
        this.subject.WriteMessage(Constants.KnownMessageBytes);
        this.subject.Flush();

        byte[] framed =
        [
            .. DataBlockConstants.StartBlock,
            .. Constants.KnownMessageBytes,
            .. DataBlockConstants.EndBlock,
        ];

        byte[] expectedCrcBytes = Crc16.Hash(framed);
        ushort expectedCrc = BinaryPrimitives.ReadUInt16BigEndian(expectedCrcBytes);

        ReadOnlySpan<byte> actualCrcBytes = this.output.WrittenSpan[^2..];
        ushort actualCrc = BinaryPrimitives.ReadUInt16BigEndian(actualCrcBytes);

        Assert.AreEqual(expectedCrc, actualCrc);
    }

    [TestMethod]
    public void ShouldWriteLargeMessage()
    {
        Span<byte> value = new byte[1024];
        Random.Shared.NextBytes(value);

        this.subject.WriteMessage(value);
        this.subject.WriteMessage(value);
        this.subject.Flush();

        Assert.IsTrue(this.output.WrittenSpan.Length > value.Length * 2);
    }
}
