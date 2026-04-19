namespace MDI.Philips.M1350;

/// <summary>
/// Indicates the direction in which a Philips M1350 message was observed.
/// </summary>
public enum M1350MessageDirection
{
    /// <summary>
    /// The message was received from the fetal monitor.
    /// </summary>
    Inbound,

    /// <summary>
    /// The message was sent toward the fetal monitor.
    /// </summary>
    Outbound,
}
