using System.Runtime.CompilerServices;
using System.Text;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Replay;

namespace MDI.Tests.Philips.M1350.Replay;

[TestClass]
public sealed class M1350MessageReplayTests
{
    [TestMethod]
    public async Task RecordAsyncShouldWriteMetadataAndEntriesToRecorder()
    {
        M1350ReplayMetadata metadata = new("A20", DateTimeOffset.Parse("2026-04-19T12:00:00+00:00", null, System.Globalization.DateTimeStyles.RoundtripKind), "M1350A");
        M1350InMemoryMessageReplayRecorder recorder = new();
        TestTimeProvider timeProvider = new();

        await M1350MessageReplay.RecordAsync(
            BuildReplaySource(timeProvider, this.TestContext.CancellationToken),
            recorder,
            metadata,
            timeProvider,
            this.TestContext.CancellationToken);

        Assert.AreEqual(metadata, recorder.Metadata);
        Assert.HasCount(3, recorder.Entries);
        Assert.AreEqual(TimeSpan.Zero, recorder.Entries[0].Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(1), recorder.Entries[1].Delay);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), recorder.Entries[2].Delay);
        Assert.IsInstanceOfType<IdMessage>(recorder.Entries[0].Message);
        Assert.IsInstanceOfType<NoteMessage>(recorder.Entries[1].Message);
        Assert.IsInstanceOfType<FailureMessage>(recorder.Entries[2].Message);
    }

    [TestMethod]
    public async Task WriteEntryAsyncShouldThrowWhenHeaderWasNotWritten()
    {
        M1350InMemoryMessageReplayRecorder recorder = new();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => recorder.WriteEntryAsync(
                new M1350ReplayEntry(TimeSpan.Zero, CreateIdMessage()),
                this.TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task ReadAllAsyncShouldYieldEntriesInOrder()
    {
        M1350ReplayMetadata metadata = new("A20", null, "M1350A");
        M1350ReplayEntry[] entries =
        [
            new(TimeSpan.Zero, CreateIdMessage()),
            new(TimeSpan.FromMilliseconds(250), CreateNoteMessage()),
        ];

        List<M1350ReplayEntry> materialized = [];

        await foreach (M1350ReplayEntry entry in M1350MessageReplay.ReadAllAsync(metadata, entries, this.TestContext.CancellationToken))
        {
            materialized.Add(entry);
        }

        CollectionAssert.AreEqual(entries, materialized);
    }

    [TestMethod]
    public async Task PlaybackAsyncShouldYieldMessagesInOrder()
    {
        M1350ReplayEntry[] entries =
        [
            new(TimeSpan.Zero, CreateIdMessage()),
            new(TimeSpan.Zero, CreateNoteMessage()),
        ];

        List<M1350Message> materialized = [];

        await foreach (M1350Message message in M1350MessageReplay.PlaybackAsync(entries, cancellationToken: this.TestContext.CancellationToken))
        {
            materialized.Add(message);
        }

        Assert.HasCount(2, materialized);
        Assert.AreEqual(CreateIdMessage(), materialized[0]);
        Assert.AreEqual(CreateNoteMessage(), materialized[1]);
    }

    [TestMethod]
    public async Task PlaybackAsyncShouldHonorCancellationBeforeDelayedEntry()
    {
        M1350RecordedMessageReplay replay = new(
            new M1350ReplayMetadata("A20", null, "M1350A"),
            [
                new(TimeSpan.Zero, CreateIdMessage()),
                new(TimeSpan.FromMinutes(1), CreateNoteMessage()),
            ]);

        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.TestContext.CancellationToken);
        await using IAsyncEnumerator<M1350Message> enumerator = M1350MessageReplay
            .PlaybackAsync(replay, cancellationToken: cancellationSource.Token)
            .GetAsyncEnumerator(cancellationSource.Token);

        bool firstMove = await enumerator.MoveNextAsync();
        Assert.IsTrue(firstMove);
        Assert.AreEqual(CreateIdMessage(), enumerator.Current);

        await cancellationSource.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => enumerator.MoveNextAsync().AsTask());
    }

    [TestMethod]
    public async Task NdjsonRoundTripShouldPreserveMetadataAndEntries()
    {
        M1350ReplayMetadata metadata = new("A20", DateTimeOffset.Parse("2026-04-19T12:00:00+00:00", null, System.Globalization.DateTimeStyles.RoundtripKind), "M1350A");
        MemoryStream stream = new();

        await using (M1350NdjsonMessageReplayRecorder recorder = M1350MessageReplay.CreateNdjsonRecorder(stream, leaveOpen: true))
        {
            await recorder.WriteHeaderAsync(metadata, this.TestContext.CancellationToken);
            await recorder.WriteEntryAsync(new M1350ReplayEntry(TimeSpan.Zero, CreateIdMessage()), this.TestContext.CancellationToken);
            await recorder.WriteEntryAsync(new M1350ReplayEntry(TimeSpan.FromMilliseconds(250), CreateNoteMessage()), this.TestContext.CancellationToken);
            await recorder.WriteEntryAsync(new M1350ReplayEntry(TimeSpan.FromSeconds(1), new FailureMessage(new("004"))), this.TestContext.CancellationToken);
        }

        stream.Position = 0;
        M1350RecordedMessageReplay replay = await M1350MessageReplay.ReadNdjsonAsync(stream, this.TestContext.CancellationToken);

        Assert.AreEqual(metadata, replay.Metadata);
        Assert.HasCount(3, replay.Entries);
        Assert.AreEqual(new M1350ReplayEntry(TimeSpan.Zero, CreateIdMessage()), replay.Entries[0]);
        Assert.AreEqual(new M1350ReplayEntry(TimeSpan.FromMilliseconds(250), CreateNoteMessage()), replay.Entries[1]);
        Assert.AreEqual(new M1350ReplayEntry(TimeSpan.FromSeconds(1), new FailureMessage(new("004"))), replay.Entries[2]);
    }

    [TestMethod]
    public async Task NdjsonRecorderShouldWriteOneHeaderLineAndOneEventLinePerEntry()
    {
        M1350ReplayMetadata metadata = new("A20", null, "M1350A");
        MemoryStream stream = new();

        await using (M1350NdjsonMessageReplayRecorder recorder = M1350MessageReplay.CreateNdjsonRecorder(stream, leaveOpen: true))
        {
            await recorder.WriteHeaderAsync(metadata, this.TestContext.CancellationToken);
            await recorder.WriteEntryAsync(new M1350ReplayEntry(TimeSpan.Zero, CreateIdMessage()), this.TestContext.CancellationToken);
            await recorder.WriteEntryAsync(new M1350ReplayEntry(TimeSpan.FromMilliseconds(250), CreateNoteMessage()), this.TestContext.CancellationToken);
        }

        string[] lines = Encoding.UTF8.GetString(stream.ToArray())
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.HasCount(3, lines);
        Assert.Contains("\"kind\":\"header\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"event\"", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"kind\":\"event\"", lines[2], StringComparison.Ordinal);
    }

    public required TestContext TestContext { get; init; }

    private static async IAsyncEnumerable<M1350Message> BuildReplaySource(
        TestTimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return CreateIdMessage();

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return CreateNoteMessage();

        timeProvider.Advance(TimeSpan.FromMilliseconds(250));
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new FailureMessage(new("004"));
    }

    private static IdMessage CreateIdMessage()
    {
        return new IdMessage(new IdBlock("M1350A", "A20", "A.03.00", "SN12345678"));
    }

    private static NoteMessage CreateNoteMessage()
    {
        return new NoteMessage(new NoteBlock("NURSE", "hello"));
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return this.timestamp;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(this.timestamp);
        }

        public void Advance(TimeSpan elapsed)
        {
            this.timestamp += elapsed.Ticks;
        }
    }
}
