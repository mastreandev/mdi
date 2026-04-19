using System.Buffers;

using MDI.Philips.M1350.Application.Failure;

namespace MDI.Philips.M1350.Tests.Application.Failure;

[TestClass]
public sealed class FailureBlockParserTests
{
    [TestMethod]
    public void TryParseShouldParseFailureCode()
    {
        byte[] payload = [(byte)'F', (byte)'5', (byte)'0', (byte)'3'];

        bool result = FailureBlockParser.TryParse(payload, out FailureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("503", block.ErrorCode);
    }

    [TestMethod]
    public void TryParseSequenceShouldParseFailureCode()
    {
        byte[] payload = [(byte)'F', (byte)'1', (byte)'2', (byte)'3'];
        ReadOnlySequence<byte> sequence = new(payload);

        bool result = FailureBlockParser.TryParse(sequence, out FailureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("123", block.ErrorCode);
    }

    [TestMethod]
    public void TryParseShouldRejectWrongType()
    {
        byte[] payload = [(byte)'X', (byte)'5', (byte)'0', (byte)'3'];

        bool result = FailureBlockParser.TryParse(payload, out FailureBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }
}
