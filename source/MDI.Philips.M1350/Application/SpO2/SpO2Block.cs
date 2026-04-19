namespace MDI.Philips.M1350.Application.SpO2;

/// <summary>
/// Represents the parsed contents of a Philips M1350 S-block
/// (maternal oxygen saturation block).
/// </summary>
/// <param name="OxygenSaturation">The oxygen saturation raw value with 0.5 percent resolution.</param>
/// <param name="MaternalHeartRate">The maternal heart-rate raw value with 0.25 bpm resolution.</param>
public readonly record struct SpO2Block(byte OxygenSaturation, ushort MaternalHeartRate);
