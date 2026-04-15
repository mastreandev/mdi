using System.Buffers;

using MDI.Philips.M1350.Application.Identity;

namespace MDI.Tests.Philips.M1350.Application.Identity;

[TestClass]
public sealed class IdBlockParserTests
{
    private static byte[] BuildPayload(Action<byte[]> configure)
    {
        byte[] payload = new byte[IdBlockParser.PayloadLength];
        payload[0] = IdBlockParser.TypeByte;
        configure(payload);
        return payload;
    }

    private static IdBlock ParsePayload(byte[] payload)
    {
        bool result = IdBlockParser.TryParse((ReadOnlySpan<byte>)payload, out IdBlock block);
        Assert.IsTrue(result);
        return block;
    }

    private static IdBlock ParsePayload(ReadOnlySequence<byte> payload)
    {
        bool result = IdBlockParser.TryParse(payload, out IdBlock block);
        Assert.IsTrue(result);
        return block;
    }

    [TestMethod]
    public void ShouldReturnFalseWhenPayloadIsTooShort()
    {
        byte[] payload = new byte[IdBlockParser.PayloadLength - 1];
        payload[0] = IdBlockParser.TypeByte;

        bool result = IdBlockParser.TryParse(payload, out IdBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenTypeByteMismatch()
    {
        byte[] payload = BuildPayload(_ => { });
        payload[0] = (byte)'C';

        bool result = IdBlockParser.TryParse(payload, out IdBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void ShouldParseIdentityFieldsFromSpan()
    {
        byte[] payload = BuildPayload(p =>
        {
            "M1350A"u8.CopyTo(p.AsSpan(1, 6));
            "A20"u8.CopyTo(p.AsSpan(7, 3));
            "A.03.00"u8.CopyTo(p.AsSpan(10, 7));
            "3019G10010"u8.CopyTo(p.AsSpan(17, 10));
        });

        IdBlock block = ParsePayload(payload);

        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
        Assert.AreEqual("A.03.00", block.SoftwareRevision);
        Assert.AreEqual("3019G10010", block.SerialNumber);
    }

    [TestMethod]
    public void ShouldParseIdenticallyFromSpanAndSequence()
    {
        byte[] payload = BuildPayload(p =>
        {
            "M1351A"u8.CopyTo(p.AsSpan(1, 6));
            "A01"u8.CopyTo(p.AsSpan(7, 3));
            "B.10.04"u8.CopyTo(p.AsSpan(10, 7));
            "0000A12345"u8.CopyTo(p.AsSpan(17, 10));
        });

        IdBlock spanBlock = ParsePayload(payload);
        IdBlock sequenceBlock = ParsePayload(new ReadOnlySequence<byte>(payload));

        Assert.AreEqual(spanBlock, sequenceBlock);
    }
}
