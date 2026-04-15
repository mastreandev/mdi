namespace MDI.Philips.M1350.Application.Temperature;

/// <summary>
/// Represents the parsed contents of a Philips M1350 T-block
/// (maternal temperature block).
/// </summary>
/// <param name="RawValue">The raw temperature value with 0.1 C resolution and a 25.0 C offset.</param>
public readonly record struct TemperatureBlock(byte RawValue);
