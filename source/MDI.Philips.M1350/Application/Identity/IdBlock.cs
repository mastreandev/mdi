namespace MDI.Philips.M1350.Application.Identity;

/// <summary>
/// Represents the parsed contents of a Philips M1350 I-block (monitor identification block).
/// </summary>
/// <param name="IdCode">The 6-character monitor ID code, for example <c>M1350A</c>.</param>
/// <param name="ProtocolRevision">
/// The 3-character protocol revision code, for example <c>A20</c> for revision A.02.00.
/// </param>
/// <param name="SoftwareRevision">
/// The 7-character fetal monitor software revision, for example <c>A.03.00</c>.
/// </param>
/// <param name="SerialNumber">The 10-character monitor serial number.</param>
public readonly record struct IdBlock(
    string IdCode,
    string ProtocolRevision,
    string SoftwareRevision,
    string SerialNumber);
