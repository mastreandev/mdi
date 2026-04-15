namespace MDI.Philips.M1350.Application.Failure;

/// <summary>
/// Represents the parsed contents of a Philips M1350 F-block.
/// </summary>
/// <param name="ErrorCode">The 3-character ASCII error code.</param>
public readonly record struct FailureBlock(string ErrorCode);
