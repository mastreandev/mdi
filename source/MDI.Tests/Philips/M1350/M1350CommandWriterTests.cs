using System.Buffers;

using MDI.Philips.M1350;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350;

[TestClass]
public sealed class M1350CommandWriterTests
{
    [TestMethod]
    public void ShouldWriteIdentityRequest()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteRequestIdentity(output);

        CollectionAssert.AreEqual("?I"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldWriteCtgRequest()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteRequestCtg(output);

        CollectionAssert.AreEqual("?C"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldWriteStartAutoSend()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteStartAutoSend(output);

        CollectionAssert.AreEqual("G"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldWriteHaltAutoSend()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteHaltAutoSend(output);

        CollectionAssert.AreEqual("H"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldWriteNoteWithoutUserId()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteNote(output, "Hello");

        CollectionAssert.AreEqual(new byte[] { (byte)'N', 0x00, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o' }, ReadPayload(output));
    }

    [TestMethod]
    public void ShouldWriteNoteWithUserId()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteNote(output, "OK", "PC");

        CollectionAssert.AreEqual("N\u0002PCOK"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldThrowWhenNoteIsInvalid()
    {
        ArrayBufferWriter<byte> output = new();

        Assert.ThrowsExactly<ArgumentException>(() => M1350CommandWriter.WriteNote(output, "", ""));
    }

    [TestMethod]
    public void ShouldWriteProtocolRevisionChange()
    {
        ArrayBufferWriter<byte> output = new();

        M1350CommandWriter.WriteProtocolRevisionChange(output, "A20");

        CollectionAssert.AreEqual("VA20"u8.ToArray(), ReadPayload(output));
    }

    [TestMethod]
    public void ShouldThrowWhenProtocolRevisionIsInvalid()
    {
        ArrayBufferWriter<byte> output = new();

        Assert.ThrowsExactly<ArgumentException>(() => M1350CommandWriter.WriteProtocolRevisionChange(output, "A.02.00"));
    }

    private static byte[] ReadPayload(ArrayBufferWriter<byte> output)
    {
        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> payload);

        Assert.IsTrue(result);
        return payload.ToArray();
    }
}
