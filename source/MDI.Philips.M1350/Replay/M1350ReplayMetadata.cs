namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Describes metadata recorded once for a message-layer replay capture.
/// </summary>
/// <param name="NegotiatedRevision">The negotiated protocol revision, when known.</param>
/// <param name="CapturedAt">An optional wall-clock timestamp for operator or diagnostic context.</param>
/// <param name="DeviceId">An optional device identifier, for example an M1350 ID code.</param>
public sealed record M1350ReplayMetadata(
    string? NegotiatedRevision,
    DateTimeOffset? CapturedAt,
    string? DeviceId);
