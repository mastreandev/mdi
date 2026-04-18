using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Simulator.Application.Identity;

namespace MDI.Tests.Philips.M1350.Simulator.Application.Identity;

[TestClass]
public sealed class IdBlockEncoderTests
{
    [TestMethod]
    public void ShouldEncodeIdentityPayload()
    {
        IdBlock block = new(
            IdCode: "M1350A",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];

        bool result = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(IdBlockEncoder.EncodedLength, bytesWritten);
        CollectionAssert.AreEqual(
            "IM1350AA20A.03.003019G10010"u8.ToArray(),
            payload.ToArray());
    }

    [TestMethod]
    public void ShouldRoundTripWithIdentityParser()
    {
        IdBlock block = new(
            IdCode: "M1351A",
            ProtocolRevision: "A01",
            SoftwareRevision: "B.10.04",
            SerialNumber: "0000A12345");
        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];

        bool encodeResult = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);
        bool parseResult = IdBlockParser.TryParse(payload, out IdBlock parsed);

        Assert.IsTrue(encodeResult);
        Assert.AreEqual(IdBlockEncoder.EncodedLength, bytesWritten);
        Assert.IsTrue(parseResult);
        Assert.AreEqual(block, parsed);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooShort()
    {
        IdBlock block = new(
            IdCode: "M1350A",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength - 1];

        bool result = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenAnyFieldLengthIsInvalid()
    {
        IdBlock block = new(
            IdCode: "M1350",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];

        bool result = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenAnyFieldContainsNonAsciiCharacters()
    {
        IdBlock block = new(
            IdCode: "M1350A",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.0é",
            SerialNumber: "3019G10010");
        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];

        bool result = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }
}
