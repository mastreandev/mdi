using System.Buffers;

using MDI.Philips.M1350.Application.Notes;

namespace MDI.Philips.M1350.Tests.Application.Notes;

[TestClass]
public sealed class NoteBlockParserTests
{
    [TestMethod]
    public void TryParseShouldParseMonitorNoteWithoutUserId()
    {
        byte[] payload = [(byte)'N', 0x00, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o'];

        bool result = NoteBlockParser.TryParse(payload, out NoteBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("", block.UserId);
        Assert.AreEqual("Hello", block.Text);
    }

    [TestMethod]
    public void TryParseShouldParseHostNoteWithUserId()
    {
        byte[] payload = [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K'];

        bool result = NoteBlockParser.TryParse(payload, out NoteBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("PC", block.UserId);
        Assert.AreEqual("OK", block.Text);
    }

    [TestMethod]
    public void TryParseSequenceShouldParseNote()
    {
        byte[] payload = [(byte)'N', 0x00, (byte)'A'];
        ReadOnlySequence<byte> sequence = new(payload);

        bool result = NoteBlockParser.TryParse(sequence, out NoteBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("A", block.Text);
    }

    [TestMethod]
    public void TryParseShouldRejectInvalidUserIdLength()
    {
        byte[] payload = [(byte)'N', 0x04, (byte)'A', (byte)'B'];

        bool result = NoteBlockParser.TryParse(payload, out NoteBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }
}
