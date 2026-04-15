using System.Buffers;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350;

public sealed partial class M1350SessionTests
{
    [TestMethod]
    public void TryRequestIdentityShouldWriteIdentityRequestAndReadIdentity()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        ArrayBufferWriter<byte> input = new();
        using (DataBlockWriter writer = new(input))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        ReadOnlySequence<byte> inputBuffer = new(input.WrittenMemory);

        bool result = session.TryRequestIdentity(ref inputBuffer, out IdBlock block);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.IsTrue(result);
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public void TryRequestCtgShouldWriteCtgRequestAndReadCtg()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        ArrayBufferWriter<byte> input = new();
        using (DataBlockWriter writer = new(input))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(input))
        {
            byte[] ctgPayload = new byte[CtgBlockParser.PayloadLength];
            ctgPayload[0] = CtgBlockParser.TypeByte;
            ctgPayload[2] = 0x41;
            ctgPayload[3] = 0x60;
            ctgPayload[4] = 0xF0;
            writer.WriteMessage(ctgPayload);
        }

        ReadOnlySequence<byte> inputBuffer = new(input.WrittenMemory);

        bool result = session.TryRequestCtg(ref inputBuffer, out CtgBlock block);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?C"u8.ToArray());
        Assert.IsTrue(result);
        Assert.IsTrue(block.Status.IsTelemetryOn);
        Assert.AreEqual((ushort)240, block.Fhr1Sample0.RawValue);
    }

    [TestMethod]
    public void TryNegotiateProtocolRevisionShouldWriteCommandsAndReadNegotiatedIdentity()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        ArrayBufferWriter<byte> input = new();
        using (DataBlockWriter writer = new(input))
        {
            writer.WriteMessage(BuildIdentityPayload("A20"));
        }

        ReadOnlySequence<byte> inputBuffer = new(input.WrittenMemory);

        bool result = session.TryNegotiateProtocolRevision(ref inputBuffer, "A20", out IdBlock block);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "VA20"u8.ToArray());
        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.IsTrue(result);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public void TryNegotiateProtocolRevisionShouldReturnFalseWhenIdentityDoesNotSatisfyRequest()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        ArrayBufferWriter<byte> input = new();
        using (DataBlockWriter writer = new(input))
        {
            writer.WriteMessage(BuildIdentityPayload("A10"));
        }

        ReadOnlySequence<byte> inputBuffer = new(input.WrittenMemory);

        bool result = session.TryNegotiateProtocolRevision(ref inputBuffer, "A20", out IdBlock block);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "VA20"u8.ToArray());
        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void StartAutoSendShouldWriteGoCommand()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        session.StartAutoSend();

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        AssertPayload(ref buffer, "G"u8.ToArray());
    }

    [TestMethod]
    public void HaltAutoSendShouldWriteHaltCommand()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        session.HaltAutoSend();

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        AssertPayload(ref buffer, "H"u8.ToArray());
    }

    [TestMethod]
    public void SendNoteShouldWriteNoteWithoutUserId()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        session.SendNote("Hello");

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        AssertPayload(ref buffer, [(byte)'N', 0x00, (byte)'H', (byte)'e', (byte)'l', (byte)'l', (byte)'o']);
    }

    [TestMethod]
    public void SendNoteShouldWriteNoteWithUserId()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        session.SendNote("OK", "PC");

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        AssertPayload(ref buffer, "N\u0002PCOK"u8.ToArray());
    }

    [TestMethod]
    public void NegotiateProtocolRevisionShouldWriteRevisionRequestThenIdentityRequest()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        session.NegotiateProtocolRevision("A20");

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        AssertPayload(ref buffer, "VA20"u8.ToArray());
        AssertPayload(ref buffer, "?I"u8.ToArray());
    }
}
