namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Identifies the active tocometry transducer type as reported in the Toco Mode byte
/// of a C-block.
/// </summary>
public enum TocoMode
{
    /// <summary>No transducer connected.</summary>
    NoTransducer = 0,

    /// <summary>External tocometry transducer.</summary>
    External = 1,

    /// <summary>Intrauterine pressure (IUP) transducer.</summary>
    Iup = 2,

    /// <summary>Unknown or unrecognised mode.</summary>
    Unknown = 7,
}
