using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.DataLink;
using MDI.Philips.M1350.Simulator;

namespace MDI.Tests.Philips.M1350.Simulator;

[TestClass]
public sealed class M1350FmWriterTests
{
    [TestMethod]
    public void WriteIdentityShouldProduceFramedIdentityReadableByMessageReader()
    {
        ArrayBufferWriter<byte> output = new();
        IdBlock expected = CreateIdentityBlock();

        M1350FmWriter.WriteIdentity(output, expected);

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);
        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsInstanceOfType<IdMessage>(message);
        Assert.AreEqual(expected, ((IdMessage)message).Block);
    }

    [TestMethod]
    public void WriteCtgShouldProduceFramedCtgReadableByMessageReader()
    {
        ArrayBufferWriter<byte> output = new();
        CtgBlock expected = CreateCtgBlock();

        M1350FmWriter.WriteCtg(output, expected);

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);
        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsInstanceOfType<CtgMessage>(message);
        Assert.AreEqual(expected, ((CtgMessage)message).Block);
    }

    [TestMethod]
    public async Task WriteIdentityShouldProduceFrameConsumableBySessionRequestIdentityAsync()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        IdBlock expected = CreateIdentityBlock();

        M1350FmWriter.WriteIdentity(input.Writer, expected);
        await input.Writer.CompleteAsync();

        await using M1350Session session = new(input.Reader, output);

        IdBlock block = await session.RequestIdentityAsync(cancellationToken);

        Assert.AreEqual(expected, block);
        AssertRequestPayload(output.WrittenMemory, "?I"u8.ToArray());
    }

    [TestMethod]
    public async Task WriteCtgShouldProduceFrameConsumableBySessionRequestCtgAsync()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        CtgBlock expected = CreateCtgBlock();

        M1350FmWriter.WriteCtg(input.Writer, expected);
        await input.Writer.CompleteAsync();

        await using M1350Session session = new(input.Reader, output);

        CtgBlock block = await session.RequestCtgAsync(cancellationToken);

        Assert.AreEqual(expected, block);
        AssertRequestPayload(output.WrittenMemory, "?C"u8.ToArray());
    }

    [TestMethod]
    public void WriteMessageShouldProduceFramedNoteReadableByMessageReader()
    {
        ArrayBufferWriter<byte> output = new();
        NoteMessage expected = new(new NoteBlock("PC", "OK"));

        M1350FmWriter.WriteMessage(output, expected);

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);
        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsInstanceOfType<NoteMessage>(message);
        Assert.AreEqual(expected.Block, ((NoteMessage)message).Block);
    }

    [TestMethod]
    public void WriteMessageShouldProduceFramedFailureReadableByMessageReader()
    {
        ArrayBufferWriter<byte> output = new();
        FailureMessage expected = new(new FailureBlock("004"));

        M1350FmWriter.WriteMessage(output, expected);

        ReadOnlySequence<byte> buffer = new(output.WrittenMemory);
        bool result = M1350MessageReader.TryRead(ref buffer, out M1350Message message);

        Assert.IsTrue(result);
        Assert.IsInstanceOfType<FailureMessage>(message);
        Assert.AreEqual(expected.Block, ((FailureMessage)message).Block);
    }

    [TestMethod]
    public void WriteMessageShouldRejectOutboundReplayMessages()
    {
        ArrayBufferWriter<byte> output = new();
        IdMessage outbound = new IdMessage(CreateIdentityBlock()) with { Direction = M1350MessageDirection.Outbound };

        Assert.ThrowsExactly<ArgumentException>(() => M1350FmWriter.WriteMessage(output, outbound));
    }

    public TestContext TestContext { get; set; } = null!;

    private static void AssertRequestPayload(ReadOnlyMemory<byte> writtenMemory, byte[] expectedPayload)
    {
        ReadOnlySequence<byte> buffer = new(writtenMemory);

        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> payload);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedPayload, payload.ToArray());
    }

    private static IdBlock CreateIdentityBlock()
    {
        return new IdBlock(
            IdCode: "M1350A",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
    }

    private static CtgBlock CreateCtgBlock()
    {
        return new CtgBlock
        {
            Status = new CtgStatusWord(0x0051),
            Fhr1Sample0 = new FhrSample(600, FmpValue.Movement, SignalQuality.Green),
            Fhr1Sample1 = new FhrSample(604, FmpValue.None, SignalQuality.Green),
            Fhr1Sample2 = new FhrSample(608, FmpValue.None, SignalQuality.Yellow),
            Fhr1Sample3 = new FhrSample(612, FmpValue.None, SignalQuality.Green),
            Fhr2Sample0 = new HeartRateSample(560, SignalQuality.Green),
            Fhr2Sample1 = new HeartRateSample(564, SignalQuality.Green),
            Fhr2Sample2 = new HeartRateSample(568, SignalQuality.Yellow),
            Fhr2Sample3 = new HeartRateSample(572, SignalQuality.Green),
            MhrSample0 = new HeartRateSample(320, SignalQuality.Green),
            MhrSample1 = new HeartRateSample(324, SignalQuality.Green),
            MhrSample2 = new HeartRateSample(328, SignalQuality.Green),
            MhrSample3 = new HeartRateSample(332, SignalQuality.Green),
            TocoSample0 = 20,
            TocoSample1 = 22,
            TocoSample2 = 24,
            TocoSample3 = 26,
            Hr1Mode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Decg,
            MhrMode = HrMode.ExternalMhr,
            TocoMode = TocoMode.External,
            FSpO2 = 98,
        };
    }
}
