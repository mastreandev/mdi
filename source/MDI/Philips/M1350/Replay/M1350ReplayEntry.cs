namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Represents one replayable Philips M1350 message plus the monotonic delay since the prior entry.
/// </summary>
/// <param name="Delay">The monotonic elapsed time since the prior replay entry.</param>
/// <param name="Message">The parsed Philips M1350 message.</param>
public sealed record M1350ReplayEntry(TimeSpan Delay, M1350Message Message);
