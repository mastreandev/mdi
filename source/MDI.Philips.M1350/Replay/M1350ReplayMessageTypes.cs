namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Defines the stable message-type discriminator values used in persisted Philips M1350 replay files.
/// </summary>
internal static class M1350ReplayMessageTypes
{
    public const string Ctg = "ctg";
    public const string Id = "id";
    public const string Nibp = "nibp";
    public const string Temperature = "temperature";
    public const string SpO2 = "spo2";
    public const string EventMarker = "event-marker";
    public const string Note = "note";
    public const string Failure = "failure";
}
