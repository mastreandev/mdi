using MDI.Philips.M1350.Application;
using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350.Tests.Application;

[TestClass]
public sealed class RequestBlockEncoderTests
{
    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooSmall()
    {
        Span<byte> destination = stackalloc byte[RequestBlockEncoder.EncodedLength - 1];

        bool result = RequestBlockEncoder.TryEncode(IdBlockParser.TypeByte, destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldEncodeRequestedType()
    {
        Span<byte> destination = stackalloc byte[RequestBlockEncoder.EncodedLength];

        bool result = RequestBlockEncoder.TryEncode(IdBlockParser.TypeByte, destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(RequestBlockEncoder.EncodedLength, bytesWritten);
        CollectionAssert.AreEqual("?I"u8.ToArray(), destination.ToArray());
    }
}
