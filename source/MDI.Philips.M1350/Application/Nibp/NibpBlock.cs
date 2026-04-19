namespace MDI.Philips.M1350.Application.Nibp;

/// <summary>
/// Represents the parsed contents of a Philips M1350 P-block
/// (maternal non-invasive blood pressure block).
/// </summary>
/// <param name="SystolicPressure">The systolic pressure raw value in mm/Hg.</param>
/// <param name="DiastolicPressure">The diastolic pressure raw value in mm/Hg.</param>
/// <param name="MeanPressure">The mean pressure raw value in mm/Hg.</param>
/// <param name="MaternalHeartRate">The maternal heart-rate raw value with 0.25 bpm resolution.</param>
public readonly record struct NibpBlock(
    ushort SystolicPressure,
    ushort DiastolicPressure,
    ushort MeanPressure,
    ushort MaternalHeartRate);
