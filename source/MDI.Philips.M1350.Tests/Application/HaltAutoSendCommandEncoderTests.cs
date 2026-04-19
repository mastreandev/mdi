using MDI.Philips.M1350.Application;

namespace MDI.Philips.M1350.Tests.Application;

[TestClass]
public sealed class HaltAutoSendCommandEncoderTests
{
    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooSmall()
    {
        Span<byte> destination = [];

        bool result = HaltAutoSendCommandEncoder.TryEncode(destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldEncodeHaltCommand()
    {
        Span<byte> destination = stackalloc byte[HaltAutoSendCommandEncoder.EncodedLength];

        bool result = HaltAutoSendCommandEncoder.TryEncode(destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(HaltAutoSendCommandEncoder.EncodedLength, bytesWritten);
        CollectionAssert.AreEqual("H"u8.ToArray(), destination.ToArray());
    }
}
