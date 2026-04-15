namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Identifies the active transducer type for a fetal or maternal heart rate channel
/// as reported in the HR Mode word of a C-block.
/// </summary>
public enum HrMode
{
    /// <summary>No transducer connected.</summary>
    NoTransducer = 0,

    /// <summary>Ultrasound transducer (US/TOCO).</summary>
    Ultrasound = 1,

    /// <summary>Direct electrocardiography (DECG).</summary>
    Decg = 2,

    /// <summary>Maternal electrocardiography (MECG).</summary>
    Mecg = 3,

    /// <summary>External maternal heart rate source.</summary>
    ExternalMhr = 4,

    /// <summary>Unknown or unrecognised mode.</summary>
    Unknown = 7,
}
