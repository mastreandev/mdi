using MDI.Philips.M1350.Application;

namespace MDI.Philips.M1350.Tests.Application;

[TestClass]
public sealed class ProtocolRevisionChangeRequestEncoderTests
{
    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooSmall()
    {
        Span<byte> destination = stackalloc byte[ProtocolRevisionChangeRequestEncoder.EncodedLength - 1];

        bool result = ProtocolRevisionChangeRequestEncoder.TryEncode("A20".AsSpan(), destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenRevisionLengthIsNotThree()
    {
        Span<byte> destination = stackalloc byte[ProtocolRevisionChangeRequestEncoder.EncodedLength];

        bool result = ProtocolRevisionChangeRequestEncoder.TryEncode("A.02.00".AsSpan(), destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenRevisionContainsNonAsciiCharacters()
    {
        Span<byte> destination = stackalloc byte[ProtocolRevisionChangeRequestEncoder.EncodedLength];

        bool result = ProtocolRevisionChangeRequestEncoder.TryEncode("Å20".AsSpan(), destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldEncodeRequestedRevision()
    {
        Span<byte> destination = stackalloc byte[ProtocolRevisionChangeRequestEncoder.EncodedLength];

        bool result = ProtocolRevisionChangeRequestEncoder.TryEncode("A20".AsSpan(), destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(ProtocolRevisionChangeRequestEncoder.EncodedLength, bytesWritten);
        CollectionAssert.AreEqual("VA20"u8.ToArray(), destination.ToArray());
    }
}
