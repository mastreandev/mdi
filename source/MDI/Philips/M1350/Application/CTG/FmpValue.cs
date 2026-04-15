namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Indicates the fetal movement provocation (FMP) state embedded in an
/// <see cref="FhrSample" /> in a C-block.
/// </summary>
public enum FmpValue
{
    /// <summary>No FMP event is active.</summary>
    None = 0,

    /// <summary>Fetal movement detected.</summary>
    Movement = 1,
}
