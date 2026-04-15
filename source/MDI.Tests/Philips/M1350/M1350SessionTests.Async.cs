using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;

namespace MDI.Tests.Philips.M1350;

public sealed partial class M1350SessionTests
{
    [TestMethod]
    public async Task ReadAllAsyncShouldYieldSupportedMessagesUntilInputCompletes()
    {
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, BuildIdentityPayload(), [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);

        await using M1350Session session = new(input.Reader, output);

        List<M1350Message> messages = [];

        await foreach (M1350Message message in session.ReadAllAsync())
        {
            messages.Add(message);
        }

        Assert.AreEqual(2, messages.Count);
        Assert.IsInstanceOfType<IdMessage>(messages[0]);
        Assert.IsInstanceOfType<NoteMessage>(messages[1]);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldWriteIdentityRequestAndReadIdentity()
    {
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, BuildIdentityPayload());

        await using M1350Session session = new(input.Reader, output);

        IdBlock block = await session.RequestIdentityAsync();

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldSupportDuplexPipeTransport()
    {
        Pipe input = new();
        Pipe output = new();
        await WriteInputAsync(input.Writer, BuildIdentityPayload());

        await using M1350Session session = new(new TestDuplexPipe(input.Reader, output.Writer));

        IdBlock block = await session.RequestIdentityAsync();

        ReadResult result = await output.Reader.ReadAsync();
        ReadOnlySequence<byte> outputBuffer = result.Buffer;

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);

        output.Reader.AdvanceTo(outputBuffer.End);
        await output.Reader.CompleteAsync();
        await output.Writer.CompleteAsync();
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldSupportSeparateStreamFactory()
    {
        using MemoryStream input = BuildInputStream(BuildIdentityPayload());
        using MemoryStream output = new();

        await using M1350Session session = M1350Session.Create(input, output, leaveOpen: true);

        IdBlock block = await session.RequestIdentityAsync();

        output.Position = 0;
        ReadOnlySequence<byte> outputBuffer = new(output.ToArray());

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldSupportDuplexStreamFactory()
    {
        using MemoryStream input = BuildInputStream(BuildIdentityPayload());
        using MemoryStream output = new();
        using TestDuplexStream transport = new(input, output);

        await using M1350Session session = M1350Session.Create(transport, leaveOpen: true);

        IdBlock block = await session.RequestIdentityAsync();

        ReadOnlySequence<byte> outputBuffer = new(output.ToArray());

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestCtgAsyncShouldWriteCtgRequestAndReadCtg()
    {
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();

        byte[] ctgPayload = new byte[CtgBlockParser.PayloadLength];
        ctgPayload[0] = CtgBlockParser.TypeByte;
        ctgPayload[2] = 0x41;
        ctgPayload[3] = 0x60;
        ctgPayload[4] = 0xF0;

        await WriteInputAsync(input.Writer, BuildIdentityPayload(), ctgPayload);

        await using M1350Session session = new(input.Reader, output);

        CtgBlock block = await session.RequestCtgAsync();

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?C"u8.ToArray());
        Assert.IsTrue(block.Status.IsTelemetryOn);
        Assert.AreEqual((ushort)240, block.Fhr1Sample0.RawValue);
    }

    [TestMethod]
    public async Task NegotiateRevisionAsyncShouldWriteCommandsAndReadNegotiatedIdentity()
    {
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, BuildIdentityPayload("A20"));

        await using M1350Session session = new(input.Reader, output);

        IdBlock block = await session.NegotiateRevisionAsync("A20");

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "VA20"u8.ToArray());
        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task NegotiateRevisionAsyncShouldThrowWhenRevisionIsOlder()
    {
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, BuildIdentityPayload("A10"));

        await using M1350Session session = new(input.Reader, output);

        try
        {
            await session.NegotiateRevisionAsync("A20");
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException exception)
        {
            StringAssert.Contains(exception.Message, "does not satisfy requested revision", StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task StartAutoSendAsyncShouldFlushWhenOutputIsPipeWriter()
    {
        Pipe output = new();
        using M1350Session session = new(output.Writer);

        await session.StartAutoSendAsync();
        await output.Writer.CompleteAsync();

        ReadResult result = await output.Reader.ReadAsync();
        ReadOnlySequence<byte> buffer = result.Buffer;

        AssertPayload(ref buffer, "G"u8.ToArray());

        output.Reader.AdvanceTo(buffer.End);
        await output.Reader.CompleteAsync();
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldThrowWithoutAsyncInput()
    {
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        try
        {
            await session.RequestIdentityAsync();
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException exception)
        {
            StringAssert.Contains(exception.Message, "without an asynchronous PipeReader input", StringComparison.Ordinal);
        }
    }
}
