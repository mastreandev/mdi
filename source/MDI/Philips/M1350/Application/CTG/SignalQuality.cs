namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Indicates the signal quality of a fetal or maternal heart rate sample as reported
/// by the Philips M1350 in a C-block.
/// </summary>
public enum SignalQuality
{
    /// <summary>Reserved; not assigned a quality level.</summary>
    Unknown = 0,

    /// <summary>Signal quality is poor (red).</summary>
    Red = 1,

    /// <summary>Signal quality is adequate (yellow).</summary>
    Yellow = 2,

    /// <summary>Signal quality is good (green).</summary>
    Green = 3,
}
