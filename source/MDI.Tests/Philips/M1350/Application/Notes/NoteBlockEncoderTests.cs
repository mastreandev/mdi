using MDI.Philips.M1350.Application.Notes;

namespace MDI.Tests.Philips.M1350.Application.Notes;

[TestClass]
public sealed class NoteBlockEncoderTests
{
    [TestMethod]
    public void TryEncodeShouldEncodeTextWithoutUserId()
    {
        NoteBlock block = new("", "Hello");
        Span<byte> destination = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool result = NoteBlockEncoder.TryEncode(block, destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(7, bytesWritten);
        CollectionAssert.AreEqual(new byte[] { (byte)'N', 0x00, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' }, destination[..bytesWritten].ToArray());
    }

    [TestMethod]
    public void TryEncodeShouldEncodeTextWithUserId()
    {
        NoteBlock block = new("PC", "This is a note.");
        Span<byte> destination = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool result = NoteBlockEncoder.TryEncode(block, destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(19, bytesWritten);
        CollectionAssert.AreEqual("N\u0002PCThis is a note."u8.ToArray(), destination[..bytesWritten].ToArray());
    }

    [TestMethod]
    public void TryEncodeShouldRejectEmptyText()
    {
        NoteBlock block = new("PC", "");
        Span<byte> destination = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool result = NoteBlockEncoder.TryEncode(block, destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryEncodeShouldRejectNonAsciiContent()
    {
        NoteBlock block = new("", "Temp °C");
        Span<byte> destination = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool result = NoteBlockEncoder.TryEncode(block, destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void TryEncodeShouldRejectCombinedTextLongerThanTwentyEightCharacters()
    {
        NoteBlock block = new("AB", "123456789012345678901234567");
        Span<byte> destination = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool result = NoteBlockEncoder.TryEncode(block, destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }
}
