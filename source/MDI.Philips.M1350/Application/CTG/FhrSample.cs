namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Represents a single HR1 (fetal heart rate) sample from a C-block.
/// </summary>
/// <param name="RawValue">
/// The raw 11-bit heart rate value (range 0 to 1200 inclusive). A value of zero
/// indicates a blank trace. To convert to beats per minute, divide by 4.
/// </param>
/// <param name="Fmp">The fetal movement provocation state reported for this sample.</param>
/// <param name="Quality">The signal quality reported for this sample.</param>
public readonly record struct FhrSample(ushort RawValue, FmpValue Fmp, SignalQuality Quality);
