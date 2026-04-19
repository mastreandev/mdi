using System.Buffers;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.DataLink;

namespace MDI.Philips.M1350.Tests;

[TestClass]
public sealed class M1350MessageReaderTests
{
    [TestMethod]
    public void ShouldReadIdMessage()
    {
        byte[] payload = BuildIdentityPayload();
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is IdMessage);

        IdMessage idMessage = (IdMessage)message;
        Assert.AreEqual((byte)'I', idMessage.TypeByte);
        Assert.AreEqual(M1350MessageDirection.Inbound, idMessage.Direction);
        Assert.IsNull(idMessage.ReceivedOffset);
        Assert.AreEqual("M1350A", idMessage.Block.IdCode);
        Assert.AreEqual("A20", idMessage.Block.ProtocolRevision);
        Assert.AreEqual("A.03.00", idMessage.Block.SoftwareRevision);
        Assert.AreEqual("3019G10010", idMessage.Block.SerialNumber);
    }

    [TestMethod]
    public void ShouldReadCtgMessage()
    {
        byte[] payload = new byte[CtgBlockParser.PayloadLength];
        payload[0] = CtgBlockParser.TypeByte;
        payload[1] = 0x00;
        payload[2] = 0x41;
        payload[3] = 0x60;
        payload[4] = 0xF0;

        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is CtgMessage);

        CtgMessage ctgMessage = (CtgMessage)message;
        Assert.IsTrue(ctgMessage.Block.Status.IsTelemetryOn);
        Assert.AreEqual((ushort)240, ctgMessage.Block.Fhr1Sample0.RawValue);
    }

    [TestMethod]
    public void ShouldReadEventMessage()
    {
        byte[] payload = [(byte)'M', (byte)'M'];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is EventMarkerMessage);
    }

    [TestMethod]
    public void ShouldReadNoteMessage()
    {
        byte[] payload = [(byte)'N', 0x00, (byte)'H', (byte)'i'];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is NoteMessage);

        NoteMessage noteMessage = (NoteMessage)message;
        Assert.AreEqual("", noteMessage.Block.UserId);
        Assert.AreEqual("Hi", noteMessage.Block.Text);
    }

    [TestMethod]
    public void ShouldReadFailureMessage()
    {
        byte[] payload = [(byte)'F', (byte)'5', (byte)'0', (byte)'3'];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is FailureMessage);

        FailureMessage failureMessage = (FailureMessage)message;
        Assert.AreEqual((byte)'F', failureMessage.TypeByte);
        Assert.AreEqual("503", failureMessage.Block.ErrorCode);
    }

    [TestMethod]
    public void ShouldReadNibpMessage()
    {
        byte[] payload = [(byte)'P', 0x00, 0x78, 0x00, 0x50, 0x00, 0x60, 0x01, 0x90];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is NibpMessage);

        NibpMessage nibpMessage = (NibpMessage)message;
        Assert.AreEqual((ushort)120, nibpMessage.Block.SystolicPressure);
        Assert.AreEqual((ushort)400, nibpMessage.Block.MaternalHeartRate);
    }

    [TestMethod]
    public void ShouldReadTemperatureMessage()
    {
        byte[] payload = [(byte)'T', 0x69];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is TemperatureMessage);

        TemperatureMessage temperatureMessage = (TemperatureMessage)message;
        Assert.AreEqual((byte)0x69, temperatureMessage.Block.RawValue);
    }

    [TestMethod]
    public void ShouldReadSpO2Message()
    {
        byte[] payload = [(byte)'S', 0xC8, 0x01, 0x2C];
        ReadOnlySequence<byte> buffer = FramePayload(payload);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is SpO2Message);

        SpO2Message spO2Message = (SpO2Message)message;
        Assert.AreEqual((byte)0xC8, spO2Message.Block.OxygenSaturation);
        Assert.AreEqual((ushort)300, spO2Message.Block.MaternalHeartRate);
    }

    [TestMethod]
    public void ShouldSkipUnknownBlockAndReadNextSupportedMessage()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage("Xignored"u8);
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsTrue(message is IdMessage);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenFrameIsIncomplete()
    {
        byte[] incompleteFrame = DataBlockConstants.StartBlock.ToArray();
        ReadOnlySequence<byte> buffer = new(incompleteFrame);

        bool result = M1350MessageReader.TryRead(ref buffer, out _);

        Assert.IsFalse(result);
    }

    private static byte[] BuildIdentityPayload()
    {
        byte[] payload = new byte[27];
        payload[0] = (byte)'I';
        "M1350A"u8.CopyTo(payload.AsSpan(1, 6));
        "A20"u8.CopyTo(payload.AsSpan(7, 3));
        "A.03.00"u8.CopyTo(payload.AsSpan(10, 7));
        "3019G10010"u8.CopyTo(payload.AsSpan(17, 10));
        return payload;
    }

    private static ReadOnlySequence<byte> FramePayload(ReadOnlySpan<byte> payload)
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(payload);
        }

        return new ReadOnlySequence<byte>(output.WrittenMemory);
    }
}
