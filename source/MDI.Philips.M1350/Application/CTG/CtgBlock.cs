namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Represents the parsed contents of a Philips M1350 C-block (CTG data block).
/// </summary>
/// <remarks>
/// A C-block is 35 bytes long (1 type byte + 34 data bytes) and contains
/// four 250-millisecond samples each of HR1, HR2, MHR, and Toco, ordered oldest
/// to newest. All timestamps are relative; the caller is responsible for absolute
/// timing.
/// </remarks>
public readonly record struct CtgBlock
{
    /// <summary>Gets the status word for this block.</summary>
    public CtgStatusWord Status { get; init; }

    // -------------------------------------------------------------------
    // HR1 (fetal heart rate 1) — four 250 ms samples, oldest first
    // -------------------------------------------------------------------

    /// <summary>Gets the oldest HR1 sample in this reporting period.</summary>
    public FhrSample Fhr1Sample0 { get; init; }

    /// <summary>Gets the second HR1 sample in this reporting period.</summary>
    public FhrSample Fhr1Sample1 { get; init; }

    /// <summary>Gets the third HR1 sample in this reporting period.</summary>
    public FhrSample Fhr1Sample2 { get; init; }

    /// <summary>Gets the most recent HR1 sample in this reporting period.</summary>
    public FhrSample Fhr1Sample3 { get; init; }

    // -------------------------------------------------------------------
    // HR2 (fetal heart rate 2) — four 250 ms samples, oldest first
    // -------------------------------------------------------------------

    /// <summary>Gets the oldest HR2 sample in this reporting period.</summary>
    public HeartRateSample Fhr2Sample0 { get; init; }

    /// <summary>Gets the second HR2 sample in this reporting period.</summary>
    public HeartRateSample Fhr2Sample1 { get; init; }

    /// <summary>Gets the third HR2 sample in this reporting period.</summary>
    public HeartRateSample Fhr2Sample2 { get; init; }

    /// <summary>Gets the most recent HR2 sample in this reporting period.</summary>
    public HeartRateSample Fhr2Sample3 { get; init; }

    // -------------------------------------------------------------------
    // MHR (maternal heart rate) — four 250 ms samples, oldest first
    // -------------------------------------------------------------------

    /// <summary>Gets the oldest MHR sample in this reporting period.</summary>
    public HeartRateSample MhrSample0 { get; init; }

    /// <summary>Gets the second MHR sample in this reporting period.</summary>
    public HeartRateSample MhrSample1 { get; init; }

    /// <summary>Gets the third MHR sample in this reporting period.</summary>
    public HeartRateSample MhrSample2 { get; init; }

    /// <summary>Gets the most recent MHR sample in this reporting period.</summary>
    public HeartRateSample MhrSample3 { get; init; }

    // -------------------------------------------------------------------
    // Toco — four 250 ms samples, oldest first
    // -------------------------------------------------------------------

    /// <summary>
    /// Gets the oldest raw toco value in this reporting period (range 0–255).
    /// </summary>
    public byte TocoSample0 { get; init; }

    /// <summary>Gets the second raw toco value in this reporting period.</summary>
    public byte TocoSample1 { get; init; }

    /// <summary>Gets the third raw toco value in this reporting period.</summary>
    public byte TocoSample2 { get; init; }

    /// <summary>Gets the most recent raw toco value in this reporting period.</summary>
    public byte TocoSample3 { get; init; }

    // -------------------------------------------------------------------
    // Mode fields
    // -------------------------------------------------------------------

    /// <summary>Gets the active transducer mode for HR1.</summary>
    public HrMode Hr1Mode { get; init; }

    /// <summary>Gets the active transducer mode for HR2.</summary>
    public HrMode Hr2Mode { get; init; }

    /// <summary>Gets the active transducer mode for MHR.</summary>
    public HrMode MhrMode { get; init; }

    /// <summary>Gets the active tocometry transducer mode.</summary>
    public TocoMode TocoMode { get; init; }

    // -------------------------------------------------------------------
    // FSpO2
    // -------------------------------------------------------------------

    /// <summary>
    /// Gets the raw FSpO2 byte. Valid only when
    /// <see cref="CtgStatusWord.IsFSpO2Available" /> is <see langword="true" />.
    /// A value of zero indicates the reading should not be displayed.
    /// </summary>
    public byte FSpO2 { get; init; }
}
