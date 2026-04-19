namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Stores replay metadata and entries in memory.
/// </summary>
public sealed class M1350InMemoryMessageReplayRecorder : IM1350MessageReplayRecorder
{
    private readonly List<M1350ReplayEntry> entries = [];

    /// <summary>
    /// Gets the header metadata after it has been written.
    /// </summary>
    public M1350ReplayMetadata? Metadata { get; private set; }

    /// <summary>
    /// Gets the recorded entries in capture order.
    /// </summary>
    public IReadOnlyList<M1350ReplayEntry> Entries => this.entries;

    /// <inheritdoc />
    public ValueTask WriteHeaderAsync(M1350ReplayMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.Metadata is not null)
        {
            throw new InvalidOperationException("Replay metadata has already been written.");
        }

        this.Metadata = metadata;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask WriteEntryAsync(M1350ReplayEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.Metadata is null)
        {
            throw new InvalidOperationException("Replay metadata must be written before replay entries.");
        }

        this.entries.Add(entry);
        return ValueTask.CompletedTask;
    }
}
