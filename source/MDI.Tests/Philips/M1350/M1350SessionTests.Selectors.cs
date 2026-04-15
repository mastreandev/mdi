using System.Buffers;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350;

public sealed partial class M1350SessionTests
{
    [TestMethod]
    public void IsProtocolRevisionSatisfiedShouldReturnTrueWhenReturnedRevisionMatchesRequest()
    {
        IdBlock block = BuildIdentityBlock("A20");

        bool result = M1350Session.IsProtocolRevisionSatisfied(block, "A20");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsProtocolRevisionSatisfiedShouldReturnTrueWhenReturnedRevisionIsNewer()
    {
        IdBlock block = BuildIdentityBlock("A21");

        bool result = M1350Session.IsProtocolRevisionSatisfied(block, "A20");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsProtocolRevisionSatisfiedShouldReturnFalseWhenReturnedRevisionIsOlder()
    {
        IdBlock block = BuildIdentityBlock("A10");

        bool result = M1350Session.IsProtocolRevisionSatisfied(block, "A20");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryReadNegotiatedIdentityShouldReturnTrueWhenReturnedRevisionSatisfiesRequest()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload("A20"));
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadNegotiatedIdentity(ref buffer, "A20", out IdBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public void TryReadNegotiatedIdentityShouldReturnFalseWhenReturnedRevisionIsOlder()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload("A10"));
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadNegotiatedIdentity(ref buffer, "A20", out IdBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void TryReadIdentityShouldSkipCtgMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            byte[] ctgPayload = new byte[CtgBlockParser.PayloadLength];
            ctgPayload[0] = CtgBlockParser.TypeByte;
            ctgPayload[2] = 0x01;
            writer.WriteMessage(ctgPayload);
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadIdentity(ref buffer, out IdBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public void TryReadCtgShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            byte[] ctgPayload = new byte[CtgBlockParser.PayloadLength];
            ctgPayload[0] = CtgBlockParser.TypeByte;
            ctgPayload[2] = 0x41;
            ctgPayload[3] = 0x60;
            ctgPayload[4] = 0xF0;
            writer.WriteMessage(ctgPayload);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadCtg(ref buffer, out CtgBlock block);

        Assert.IsTrue(result);
        Assert.IsTrue(block.Status.IsTelemetryOn);
        Assert.AreEqual((ushort)240, block.Fhr1Sample0.RawValue);
    }

    [TestMethod]
    public void TryReadEventMessageShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'M', (byte)'M']);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadEventMessage(ref buffer, out EventMessageBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual(new EventMessageBlock(), block);
    }

    [TestMethod]
    public void TryReadNoteShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadNote(ref buffer, out NoteBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("PC", block.UserId);
        Assert.AreEqual("OK", block.Text);
    }

    [TestMethod]
    public void TryReadFailureShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'F', (byte)'5', (byte)'0', (byte)'3']);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadFailure(ref buffer, out FailureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual("503", block.ErrorCode);
    }

    [TestMethod]
    public void TryReadNibpShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'P', 0x00, 0x78, 0x00, 0x50, 0x00, 0x60, 0x01, 0x90]);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadNibp(ref buffer, out NibpBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((ushort)120, block.SystolicPressure);
        Assert.AreEqual((ushort)400, block.MaternalHeartRate);
    }

    [TestMethod]
    public void TryReadTemperatureShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'T', 0x69]);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadTemperature(ref buffer, out TemperatureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0x69, block.RawValue);
    }

    [TestMethod]
    public void TryReadSpO2ShouldSkipIdentityMessages()
    {
        ArrayBufferWriter<byte> output = new();

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage(BuildIdentityPayload());
        }

        using (DataBlockWriter writer = new(output))
        {
            writer.WriteMessage([(byte)'S', 0xC8, 0x01, 0x2C]);
        }

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);

        bool result = M1350Session.TryReadSpO2(ref buffer, out SpO2Block block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0xC8, block.OxygenSaturation);
        Assert.AreEqual((ushort)300, block.MaternalHeartRate);
    }
}
