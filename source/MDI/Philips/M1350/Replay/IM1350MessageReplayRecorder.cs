namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Receives message-layer replay metadata and entries in capture order.
/// </summary>
public interface IM1350MessageReplayRecorder
{
    /// <summary>
    /// Writes the replay header metadata.
    /// </summary>
    /// <param name="metadata">The metadata for the capture.</param>
    /// <param name="cancellationToken">The cancellation token for the write operation.</param>
    ValueTask WriteHeaderAsync(M1350ReplayMetadata metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one replay entry.
    /// </summary>
    /// <param name="entry">The entry to record.</param>
    /// <param name="cancellationToken">The cancellation token for the write operation.</param>
    ValueTask WriteEntryAsync(M1350ReplayEntry entry, CancellationToken cancellationToken = default);
}
