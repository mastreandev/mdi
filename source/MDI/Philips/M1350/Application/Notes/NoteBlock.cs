namespace MDI.Philips.M1350.Application.Notes;

/// <summary>
/// Represents the parsed contents of a Philips M1350 N-block.
/// </summary>
/// <param name="UserId">The optional user identifier string.</param>
/// <param name="Text">The note text.</param>
public readonly record struct NoteBlock(string UserId, string Text);
