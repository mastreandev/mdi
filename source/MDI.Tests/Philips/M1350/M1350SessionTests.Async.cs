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
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload(), [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);

        await using M1350Session session = new(input.Reader, output);

        List<M1350Message> messages = [];

        await foreach (M1350Message message in session.ReadAllAsync(cancellationToken))
        {
            messages.Add(message);
        }

        Assert.HasCount(2, messages);
        Assert.IsInstanceOfType<IdMessage>(messages[0]);
        Assert.IsInstanceOfType<NoteMessage>(messages[1]);
        Assert.AreEqual(M1350MessageDirection.Inbound, messages[0].Direction);
        Assert.AreEqual(M1350MessageDirection.Inbound, messages[1].Direction);
        Assert.IsNotNull(messages[0].ReceivedOffset);
        Assert.IsNotNull(messages[1].ReceivedOffset);
        Assert.IsGreaterThanOrEqualTo(messages[0].ReceivedOffset!.Value, messages[1].ReceivedOffset!.Value);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldWriteIdentityRequestAndReadIdentity()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload());

        await using M1350Session session = new(input.Reader, output);

        IdBlock block = await session.RequestIdentityAsync(cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldSupportDuplexPipeTransport()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        Pipe output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload());

        await using M1350Session session = new(new TestDuplexPipe(input.Reader, output.Writer));

        IdBlock block = await session.RequestIdentityAsync(cancellationToken);

        ReadResult result = await output.Reader.ReadAsync(cancellationToken);
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
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        using MemoryStream input = BuildInputStream(BuildIdentityPayload());
        using MemoryStream output = new();

        await using M1350Session session = M1350Session.Create(input, output, leaveOpen: true);

        IdBlock block = await session.RequestIdentityAsync(cancellationToken);

        output.Position = 0;
        ReadOnlySequence<byte> outputBuffer = new(output.ToArray());

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldSupportDuplexStreamFactory()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        using MemoryStream input = BuildInputStream(BuildIdentityPayload());
        using MemoryStream output = new();
        using TestDuplexStream transport = new(input, output);

        await using M1350Session session = M1350Session.Create(transport, leaveOpen: true);

        IdBlock block = await session.RequestIdentityAsync(cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.ToArray());

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("M1350A", block.IdCode);
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RequestCtgAsyncShouldWriteCtgRequestAndReadCtg()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();

        byte[] ctgPayload = new byte[CtgBlockParser.PayloadLength];
        ctgPayload[0] = CtgBlockParser.TypeByte;
        ctgPayload[2] = 0x41;
        ctgPayload[3] = 0x60;
        ctgPayload[4] = 0xF0;

        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload(), ctgPayload);

        await using M1350Session session = new(input.Reader, output);

        CtgBlock block = await session.RequestCtgAsync(cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?C"u8.ToArray());
        Assert.IsTrue(block.Status.IsTelemetryOn);
        Assert.AreEqual((ushort)240, block.Fhr1Sample0.RawValue);
    }

    [TestMethod]
    public async Task NegotiateRevisionAsyncShouldWriteCommandsAndReadNegotiatedIdentity()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload("A20"));

        await using M1350Session session = new(input.Reader, output);

        IdBlock block = await session.NegotiateRevisionAsync("A20", cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "VA20"u8.ToArray());
        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.AreEqual("A20", block.ProtocolRevision);
    }

    [TestMethod]
    public async Task NegotiateRevisionAsyncShouldThrowWhenRevisionIsOlder()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload("A10"));

        await using M1350Session session = new(input.Reader, output);

        InvalidOperationException? exception = null;

        try
        {
            await session.NegotiateRevisionAsync("A20", cancellationToken);
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.Contains("does not satisfy requested revision", exception.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task StartAutoSendAsyncShouldFlushWhenOutputIsPipeWriter()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe output = new();
        using M1350Session session = new(output.Writer);

        await session.StartAutoSendAsync(cancellationToken);
        await output.Writer.CompleteAsync();

        ReadResult result = await output.Reader.ReadAsync(cancellationToken);
        ReadOnlySequence<byte> buffer = result.Buffer;

        AssertPayload(ref buffer, "G"u8.ToArray());

        output.Reader.AdvanceTo(buffer.End);
        await output.Reader.CompleteAsync();
    }

    [TestMethod]
    public async Task RequestIdentityAsyncShouldThrowWithoutAsyncInput()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        ArrayBufferWriter<byte> output = new();
        using M1350Session session = new(output);

        InvalidOperationException? exception = null;

        try
        {
            await session.RequestIdentityAsync(cancellationToken);
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.Contains("without an asynchronous PipeReader input", exception.Message, StringComparison.Ordinal);
    }
}
