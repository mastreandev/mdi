using System.Buffers;
using System.Buffers.Binary;

using MDI.Philips.M1350.IO.Hashing;
using MDI.Philips.M1350.DataLink;

namespace MDI.Philips.M1350.Tests.DataLink;

[TestClass]
public sealed class DataBlockWriterTests : IDisposable
{
    private static readonly byte[] KnownPayloadBytes = "Check this message!"u8.ToArray();

    private readonly ArrayBufferWriter<byte> output = new();
    private DataBlockWriter subject;

    public DataBlockWriterTests()
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
        this.subject.WriteData(KnownPayloadBytes);
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

        byte[] framed =
        [
            .. DataBlockConstants.StartBlock,
            .. KnownPayloadBytes,
            .. DataBlockConstants.EndBlock,
        ];

        byte[] expectedCrcBytes = Crc16.Hash(framed);
        ushort expectedCrc = BinaryPrimitives.ReadUInt16BigEndian(expectedCrcBytes);

        Assert.AreEqual(expectedCrc, crcValue);
    }

    [TestMethod]
    public void ShouldWriteExampleMessage()
    {
        this.subject.WriteMessage(KnownPayloadBytes);
        this.subject.Flush();

        Assert.IsGreaterThan(0, this.output.WrittenCount);

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

        byte[] framed =
        [
            .. DataBlockConstants.StartBlock,
            .. KnownPayloadBytes,
            .. DataBlockConstants.EndBlock,
        ];

        byte[] expectedCrcBytes = Crc16.Hash(framed);
        ushort expectedCrc = BinaryPrimitives.ReadUInt16BigEndian(expectedCrcBytes);

        Assert.AreEqual(expectedCrc, crcValue);
    }

    [TestMethod]
    public void ShouldWriteCrcForFramedBytes()
    {
        this.subject.WriteMessage(KnownPayloadBytes);
        this.subject.Flush();

        byte[] framed =
        [
            .. DataBlockConstants.StartBlock,
            .. KnownPayloadBytes,
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

        Assert.IsGreaterThan(value.Length * 2, this.output.WrittenSpan.Length);
    }

    [TestMethod]
    public void ResetShouldClearInternalState()
    {
        this.subject.WriteStart();
        this.subject.WriteData(KnownPayloadBytes);
        this.subject.Flush();

        Assert.IsGreaterThan(0, this.subject.BytesCommitted);

        this.subject.Reset();

        Assert.AreEqual(0, this.subject.BytesPending);
        Assert.AreEqual(0, this.subject.BytesCommitted);

        Assert.ThrowsExactly<InvalidOperationException>(this.subject.WriteEnd);
    }

    // Performance/SkipValidation tests

    [TestMethod]
    public void SkipValidationShouldNotEscapeData()
    {
        this.subject.Dispose();
        this.output.Clear();
        this.subject = new DataBlockWriter(this.output, new(SkipValidation: true));

        Span<byte> value = new byte[16];
        value[0] = DataBlockConstants.DLE;

        this.subject.WriteData(value);
        this.subject.Flush();

        Assert.AreEqual(DataBlockConstants.DLE, this.output.WrittenSpan[0]);
        Assert.AreNotEqual(DataBlockConstants.DLE, this.output.WrittenSpan[1]);
    }

    [TestMethod]
    public void SkipValidationShouldAllowMissingStart()
    {
        this.subject.Dispose();
        this.output.Clear();
        this.subject = new DataBlockWriter(this.output, new(SkipValidation: true));

        this.subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] crc = Crc16.Hash(DataBlockConstants.EndBlock);

        byte[] expected =
        [
            .. DataBlockConstants.EndBlock,
            .. crc,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void SkipValidationShouldAllowMissingData()
    {
        this.subject.Dispose();
        this.output.Clear();
        this.subject = new DataBlockWriter(this.output, new(SkipValidation: true));

        this.subject.WriteStart();
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] frame =
        [
            .. DataBlockConstants.StartBlock,
            .. DataBlockConstants.EndBlock,
        ];

        byte[] crc = Crc16.Hash(frame);

        byte[] expected =
        [
            .. frame,
            .. crc,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void SkipValidationShouldAllowMissingEnd()
    {
        this.subject.Dispose();
        this.output.Clear();
        this.subject = new DataBlockWriter(this.output, new(SkipValidation: true));

        this.subject.WriteStart();
        this.subject.WriteData();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] crc = Crc16.Hash(DataBlockConstants.StartBlock);

        byte[] expected =
        [
            .. DataBlockConstants.StartBlock,
            .. crc,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }
}
