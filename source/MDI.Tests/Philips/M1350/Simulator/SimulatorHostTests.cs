using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Replay;
using MDI.Philips.M1350.Simulator;

namespace MDI.Tests.Philips.M1350.Simulator;

[TestClass]
public sealed class SimulatorHostTests
{
    [TestMethod]
    public async Task RunReplayLoopAsyncShouldRepeatReplayUntilCanceled()
    {
        Pipe output = new();
        using CancellationTokenSource cancellationSource = new();
        M1350RecordedMessageReplay replay = new(
            new M1350ReplayMetadata("A20", null, "M1350A"),
            [
                new M1350ReplayEntry(TimeSpan.Zero, new IdMessage(CreateIdentityBlock())),
                new M1350ReplayEntry(TimeSpan.Zero, new NoteMessage(new NoteBlock("PC", "OK"))),
            ]);

        Task runTask = SimulatorHost.RunReplayLoopAsync(replay, output.Writer, cancellationSource.Token);

        try
        {
            M1350Message first = await ReadMessageAsync(output.Reader, cancellationSource.Token).ConfigureAwait(false);
            M1350Message second = await ReadMessageAsync(output.Reader, cancellationSource.Token).ConfigureAwait(false);
            M1350Message third = await ReadMessageAsync(output.Reader, cancellationSource.Token).ConfigureAwait(false);
            M1350Message fourth = await ReadMessageAsync(output.Reader, cancellationSource.Token).ConfigureAwait(false);

            Assert.IsInstanceOfType<IdMessage>(first);
            Assert.IsInstanceOfType<NoteMessage>(second);
            Assert.IsInstanceOfType<IdMessage>(third);
            Assert.IsInstanceOfType<NoteMessage>(fourth);
            Assert.AreEqual(((IdMessage)first).Block, ((IdMessage)third).Block);
            Assert.AreEqual(((NoteMessage)second).Block, ((NoteMessage)fourth).Block);
        }
        finally
        {
            await cancellationSource.CancelAsync().ConfigureAwait(false);
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => runTask).ConfigureAwait(false);
            await output.Reader.CompleteAsync().ConfigureAwait(false);
            await output.Writer.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static async Task<M1350Message> ReadMessageAsync(PipeReader reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;
            ReadOnlySequence<byte> remainder = buffer;

            if (M1350MessageReader.TryRead(ref remainder, out M1350Message message))
            {
                reader.AdvanceTo(remainder.Start, remainder.Start);
                return message;
            }

            if (result.IsCompleted)
            {
                reader.AdvanceTo(buffer.End, buffer.End);
                throw new AssertInconclusiveException("Replay loop completed before producing the expected message.");
            }

            reader.AdvanceTo(remainder.Start, buffer.End);
        }
    }

    private static IdBlock CreateIdentityBlock()
    {
        return new IdBlock(
            IdCode: "M1350A",
            ProtocolRevision: "A20",
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
    }
}