namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Represents a materialized message-layer replay file.
/// </summary>
/// <param name="Metadata">The replay header metadata.</param>
/// <param name="Entries">The replay entries in playback order.</param>
public sealed record M1350RecordedMessageReplay(
    M1350ReplayMetadata Metadata,
    IReadOnlyList<M1350ReplayEntry> Entries);
