namespace MDI.Philips.M1350.DataLink;

/// <summary>
/// Allows defining custom behavior when writing Philips M1350 encoded data
/// using a <see cref="DataBlockWriter" />.
/// </summary>
/// <param name="SkipValidation">Whether the writer should skip structural validation.</param>
public sealed record DataBlockWriterOptions(bool SkipValidation = false);
