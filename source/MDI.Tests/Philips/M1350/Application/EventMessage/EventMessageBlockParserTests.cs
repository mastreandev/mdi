using System.Buffers;

using MDI.Philips.M1350.Application.EventMessage;

namespace MDI.Tests.Philips.M1350.Application.EventMessage;

[TestClass]
public sealed class EventMessageBlockParserTests
{
    [TestMethod]
    public void TryParseShouldParseEventMessage()
    {
        byte[] payload = [(byte)'M', (byte)'M'];

        bool result = EventMessageBlockParser.TryParse(payload, out EventMessageBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual(new EventMessageBlock(), block);
    }

    [TestMethod]
    public void TryParseSequenceShouldParseEventMessage()
    {
        byte[] payload = [(byte)'M', (byte)'M'];
        ReadOnlySequence<byte> sequence = new(payload);

        bool result = EventMessageBlockParser.TryParse(sequence, out EventMessageBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual(new EventMessageBlock(), block);
    }

    [TestMethod]
    public void TryParseShouldRejectUnexpectedSecondByte()
    {
        byte[] payload = [(byte)'M', (byte)'X'];

        bool result = EventMessageBlockParser.TryParse(payload, out EventMessageBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }
}
