using MDI.Philips.M1350.Application;

namespace MDI.Philips.M1350.Tests.Application;

[TestClass]
public sealed class GoAutoSendCommandEncoderTests
{
    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooSmall()
    {
        Span<byte> destination = [];

        bool result = GoAutoSendCommandEncoder.TryEncode(destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldEncodeGoCommand()
    {
        Span<byte> destination = stackalloc byte[GoAutoSendCommandEncoder.EncodedLength];

        bool result = GoAutoSendCommandEncoder.TryEncode(destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(GoAutoSendCommandEncoder.EncodedLength, bytesWritten);
        CollectionAssert.AreEqual("G"u8.ToArray(), destination.ToArray());
    }
}
