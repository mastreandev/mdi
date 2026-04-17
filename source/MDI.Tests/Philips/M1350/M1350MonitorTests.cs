using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Threading.Channels;

using MDI.Philips.M1350;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350;

[TestClass]
public sealed class M1350MonitorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StartAsyncShouldPublishUpdatesAndRefreshSnapshot()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload(), [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<List<M1350MonitorUpdate>> collectTask = CollectUpdatesAsync(monitor, ready, cancellationToken);

        await ready.Task;
        await monitor.StartAsync(cancellationToken);

        List<M1350MonitorUpdate> updates = await collectTask;
        M1350MonitorSnapshot snapshot = monitor.Snapshot;

        Assert.HasCount(2, updates);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[0]);
        Assert.IsInstanceOfType<NoteUpdated>(updates[1]);
        Assert.IsNotNull(snapshot.Identity);
        Assert.IsNotNull(snapshot.Note);
        Assert.AreEqual("M1350A", snapshot.Identity.Value.IdCode);
        Assert.AreEqual("PC", snapshot.Note.Value.UserId);
        Assert.AreEqual("OK", snapshot.Note.Value.Text);
        Assert.IsNotNull(snapshot.MessageReceivedAt);
    }

    [TestMethod]
    public async Task ConnectAsyncShouldRequestIdentityAndStartAutoSendWhenEnabled()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload());

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<List<M1350MonitorUpdate>> collectTask = CollectUpdatesAsync(monitor, ready, cancellationToken);

        await ready.Task;

        M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(autoSend: AutoSendBehavior.Enabled, cancellationToken: cancellationToken);
        List<M1350MonitorUpdate> updates = await collectTask;
        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        AssertPayload(ref outputBuffer, "G"u8.ToArray());
        Assert.HasCount(2, updates);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[0]);
        Assert.IsInstanceOfType<AutoSendStateUpdated>(updates[1]);
        Assert.AreEqual("M1350A", ((IdentityUpdated)updates[0]).Block.IdCode);
        Assert.IsTrue(((AutoSendStateUpdated)updates[1]).IsActive);
        Assert.AreEqual("M1350A", snapshot.Identity?.IdCode);
        Assert.IsTrue(snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task ConnectAsyncShouldNegotiateRevisionWhenCurrentRevisionIsOlder()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload("A10"), BuildIdentityPayload("A20"));

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<List<M1350MonitorUpdate>> collectTask = CollectUpdatesAsync(monitor, ready, cancellationToken);

        await ready.Task;

        M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
            requestedRevision: "A20",
            autoSend: AutoSendBehavior.Disabled,
            cancellationToken: cancellationToken);

        List<M1350MonitorUpdate> updates = await collectTask;

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        AssertPayload(ref outputBuffer, "VA20"u8.ToArray());
        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        Assert.HasCount(3, updates);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[0]);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[1]);
        Assert.IsInstanceOfType<NegotiatedRevisionUpdated>(updates[2]);
        Assert.AreEqual("A10", ((IdentityUpdated)updates[0]).Block.ProtocolRevision);
        Assert.AreEqual("A20", ((IdentityUpdated)updates[1]).Block.ProtocolRevision);
        Assert.AreEqual("A20", ((NegotiatedRevisionUpdated)updates[2]).Revision);
        Assert.AreEqual("A20", snapshot.Identity?.ProtocolRevision);
        Assert.AreEqual("A20", snapshot.NegotiatedRevision);
        Assert.IsFalse(snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task ConnectAsyncShouldPublishOrderedStartupTransitionsWhenNegotiationAndAutoSendAreEnabled()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload("A10"), BuildIdentityPayload("A20"));

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<List<M1350MonitorUpdate>> collectTask = CollectUpdatesAsync(monitor, ready, cancellationToken);

        await ready.Task;

        M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
            requestedRevision: "A20",
            autoSend: AutoSendBehavior.Enabled,
            cancellationToken: cancellationToken);

        List<M1350MonitorUpdate> updates = await collectTask;

        Assert.HasCount(4, updates);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[0]);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[1]);
        Assert.IsInstanceOfType<NegotiatedRevisionUpdated>(updates[2]);
        Assert.IsInstanceOfType<AutoSendStateUpdated>(updates[3]);
        Assert.AreEqual("A10", ((IdentityUpdated)updates[0]).Block.ProtocolRevision);
        Assert.AreEqual("A20", ((IdentityUpdated)updates[1]).Block.ProtocolRevision);
        Assert.AreEqual("A20", ((NegotiatedRevisionUpdated)updates[2]).Revision);
        Assert.IsTrue(((AutoSendStateUpdated)updates[3]).IsActive);
        Assert.AreEqual("A20", snapshot.NegotiatedRevision);
        Assert.IsTrue(snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task CopyUpdatesToAsyncShouldBridgeOrderedMonitorUpdatesIntoConsumerChannel()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(
            input.Writer,
            cancellationToken,
            BuildIdentityPayload("A10"),
            BuildIdentityPayload("A20"),
            [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);
        Channel<M1350MonitorUpdate> channel = Channel.CreateBounded<M1350MonitorUpdate>(new BoundedChannelOptions(8)
        {
            SingleReader = true,
            SingleWriter = true,
        });

        Task copyTask = monitor.CopyUpdatesToAsync(channel.Writer, cancellationToken);

        M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
            requestedRevision: "A20",
            autoSend: AutoSendBehavior.Enabled,
            cancellationToken: cancellationToken);

        List<M1350MonitorUpdate> updates = await ReadAllUpdatesAsync(channel.Reader, cancellationToken);
        await copyTask;

        Assert.HasCount(5, updates);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[0]);
        Assert.IsInstanceOfType<IdentityUpdated>(updates[1]);
        Assert.IsInstanceOfType<NegotiatedRevisionUpdated>(updates[2]);
        Assert.IsInstanceOfType<AutoSendStateUpdated>(updates[3]);
        Assert.IsInstanceOfType<NoteUpdated>(updates[4]);
        Assert.AreEqual("A20", snapshot.NegotiatedRevision);
        Assert.IsTrue(snapshot.IsAutoSendActive);
        Assert.AreEqual("OK", ((NoteUpdated)updates[4]).Block.Text);
    }

    [TestMethod]
    public async Task StopAsyncShouldHaltAutoSendWhenActive()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload());

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        await monitor.ConnectAsync(autoSend: AutoSendBehavior.Enabled, cancellationToken: cancellationToken);
        await monitor.StopAsync(cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);
        M1350MonitorSnapshot snapshot = monitor.Snapshot;

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        AssertPayload(ref outputBuffer, "G"u8.ToArray());
        AssertPayload(ref outputBuffer, "H"u8.ToArray());
        Assert.IsFalse(snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task StartAsyncShouldPublishUpdatesToMultipleWatchers()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload(), [(byte)'N', 0x02, (byte)'P', (byte)'C', (byte)'O', (byte)'K']);

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        TaskCompletionSource ready1 = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource ready2 = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<List<M1350MonitorUpdate>> collectTask1 = CollectUpdatesAsync(monitor, ready1, cancellationToken);
        Task<List<M1350MonitorUpdate>> collectTask2 = CollectUpdatesAsync(monitor, ready2, cancellationToken);

        await Task.WhenAll(ready1.Task, ready2.Task);
        await monitor.StartAsync(cancellationToken);

        List<M1350MonitorUpdate> updates1 = await collectTask1;
        List<M1350MonitorUpdate> updates2 = await collectTask2;

        Assert.HasCount(2, updates1);
        Assert.HasCount(2, updates2);
        Assert.IsInstanceOfType<IdentityUpdated>(updates1[0]);
        Assert.IsInstanceOfType<NoteUpdated>(updates1[1]);
        Assert.IsInstanceOfType<IdentityUpdated>(updates2[0]);
        Assert.IsInstanceOfType<NoteUpdated>(updates2[1]);
    }

    [TestMethod]
    public async Task ConnectAsyncShouldNotNegotiateWhenCurrentRevisionAlreadySatisfiesRequest()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload("A21"));

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
            requestedRevision: "A20",
            autoSend: AutoSendBehavior.Disabled,
            cancellationToken: cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        AssertNoPayloadRemaining(outputBuffer);
        Assert.AreEqual("A21", snapshot.Identity?.ProtocolRevision);
        Assert.AreEqual("A21", snapshot.NegotiatedRevision);
        Assert.IsFalse(snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task StopAsyncShouldNotWriteHaltWhenAutoSendIsInactive()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken, BuildIdentityPayload());

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        await monitor.ConnectAsync(autoSend: AutoSendBehavior.Disabled, cancellationToken: cancellationToken);
        await monitor.StopAsync(cancellationToken);

        ReadOnlySequence<byte> outputBuffer = new(output.WrittenMemory);

        AssertPayload(ref outputBuffer, "?I"u8.ToArray());
        AssertNoPayloadRemaining(outputBuffer);
        Assert.IsFalse(monitor.Snapshot.IsAutoSendActive);
    }

    [TestMethod]
    public async Task StopAsyncShouldAllowWatchingAfterCompletion()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe input = new();
        ArrayBufferWriter<byte> output = new();
        await WriteInputAsync(input.Writer, cancellationToken);

        await using M1350Monitor monitor = CreateMonitor(input.Reader, output);

        await monitor.StartAsync(cancellationToken);
        await monitor.StopAsync(cancellationToken);

        await using IAsyncEnumerator<M1350MonitorUpdate> enumerator = monitor.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        bool moved = await enumerator.MoveNextAsync();

        Assert.IsFalse(moved);
    }

    [TestMethod]
    public async Task WatchAsyncShouldPropagateReceiveLoopErrorsToExistingWatcher()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        InvalidOperationException expected = new("boom");
        ThrowingPipeReader input = new(expected);
        ArrayBufferWriter<byte> output = new();

        InvalidOperationException? exception = null;

        try
        {
            await WithMonitorAsync(
                input,
                output,
                async monitor =>
                {
                    TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    Task<List<M1350MonitorUpdate>> collectTask = CollectUpdatesAsync(monitor, ready, cancellationToken);

                    await ready.Task;
                    await monitor.StartAsync(cancellationToken);

                    await collectTask;
                });
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(expected.Message, exception.Message);
    }

    [TestMethod]
    public async Task WatchAsyncShouldCompleteWithStoredErrorAfterReceiveLoopFaults()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        InvalidOperationException expected = new("boom");
        ThrowingPipeReader input = new(expected);
        ArrayBufferWriter<byte> output = new();

        await WithMonitorAsync(
            input,
            output,
            async monitor =>
            {
                await monitor.StartAsync(cancellationToken);

                await WaitForTerminalErrorAsync(monitor, cancellationToken);

                await using IAsyncEnumerator<M1350MonitorUpdate> enumerator = monitor.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

                InvalidOperationException? exception = null;

                try
                {
                    await enumerator.MoveNextAsync();
                }
                catch (InvalidOperationException caught)
                {
                    exception = caught;
                }

                Assert.IsNotNull(exception);
                Assert.AreEqual(expected.Message, exception.Message);
            });
    }

    private static async Task<List<M1350MonitorUpdate>> CollectUpdatesAsync(M1350Monitor monitor, TaskCompletionSource ready, CancellationToken cancellationToken)
    {
        List<M1350MonitorUpdate> updates = [];

        await using IAsyncEnumerator<M1350MonitorUpdate> enumerator = monitor.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        ready.SetResult();

        while (await enumerator.MoveNextAsync())
        {
            updates.Add(enumerator.Current);
        }

        return updates;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The analyzer cannot follow the explicit session-to-monitor ownership transfer in this helper.")]
    private static async Task WithMonitorAsync(
        PipeReader input,
        IBufferWriter<byte> output,
        Func<M1350Monitor, Task> action)
    {
        M1350Session? session = new(input, output);
        M1350Monitor? monitor = null;

        try
        {
            monitor = new(session);
            session = null;
            await action(monitor);
        }
        finally
        {
            if (monitor is not null)
            {
                try
                {
                    await monitor.DisposeAsync();
                }
                catch (InvalidOperationException)
                {
                }
            }

            if (session is not null)
            {
                await session.DisposeAsync();
            }
        }
    }

    private static M1350Monitor CreateMonitor(PipeReader input, IBufferWriter<byte> output)
    {
        M1350Session? session = new(input, output);

        try
        {
            M1350Monitor monitor = new(session);
            session = null;
            return monitor;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static void AssertPayload(ref ReadOnlySequence<byte> buffer, byte[] expectedPayload)
    {
        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> payload);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedPayload, payload.ToArray());
    }

    private static void AssertNoPayloadRemaining(ReadOnlySequence<byte> buffer)
    {
        bool result = DataBlockReader.TryRead(ref buffer, out _);

        Assert.IsFalse(result);
    }

    private static async Task WriteInputAsync(PipeWriter writer, CancellationToken cancellationToken = default, params byte[][] payloads)
    {
        ArrayBufferWriter<byte> output = new();

        foreach (byte[] payload in payloads)
        {
            using DataBlockWriter dataBlockWriter = new(output);
            dataBlockWriter.WriteMessage(payload);
        }

        await writer.WriteAsync(output.WrittenMemory, cancellationToken);
        await writer.CompleteAsync();
    }

    private static async Task<List<M1350MonitorUpdate>> ReadAllUpdatesAsync(ChannelReader<M1350MonitorUpdate> reader, CancellationToken cancellationToken)
    {
        List<M1350MonitorUpdate> updates = [];

        await foreach (M1350MonitorUpdate update in reader.ReadAllAsync(cancellationToken))
        {
            updates.Add(update);
        }

        return updates;
    }

    private static async Task WaitForTerminalErrorAsync(M1350Monitor monitor, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await using IAsyncEnumerator<M1350MonitorUpdate> enumerator = monitor.WatchAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
                await enumerator.MoveNextAsync();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            await Task.Yield();
        }

        Assert.Fail("Expected the monitor to observe a terminal receive-loop error.");
    }

    private static byte[] BuildIdentityPayload(string protocolRevision = "A20")
    {
        byte[] payload = new byte[27];
        payload[0] = (byte)'I';
        "M1350A"u8.CopyTo(payload.AsSpan(1, 6));
        payload[7] = (byte)protocolRevision[0];
        payload[8] = (byte)protocolRevision[1];
        payload[9] = (byte)protocolRevision[2];
        "A.03.00"u8.CopyTo(payload.AsSpan(10, 7));
        "3019G10010"u8.CopyTo(payload.AsSpan(17, 10));
        return payload;
    }

    private sealed class ThrowingPipeReader(Exception exception) : PipeReader
    {
        public override void AdvanceTo(SequencePosition consumed)
        {
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
        }

        public override void CancelPendingRead()
        {
        }

        public override void Complete(Exception? exception = null)
        {
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<ReadResult>(exception);
        }

        public override bool TryRead(out ReadResult result)
        {
            throw exception;
        }
    }
}
