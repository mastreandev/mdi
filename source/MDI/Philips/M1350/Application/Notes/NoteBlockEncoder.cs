namespace MDI.Philips.M1350.Application.Notes;

/// <summary>
/// Encodes Philips M1350 N-block payloads for host-originated notes.
/// </summary>
public static class NoteBlockEncoder
{
    /// <summary>The largest supported host-originated N-block payload length.</summary>
    public const int MaximumPayloadLength = 30;

    /// <summary>
    /// Attempts to encode an N-block from a note value.
    /// </summary>
    public static bool TryEncode(in NoteBlock block, Span<byte> destination, out int bytesWritten)
    {
        string userId = block.UserId ?? "";
        string text = block.Text ?? "";

        int userIdLength = userId.Length;
        int textLength = text.Length;
        int printableCharacterCount = userIdLength + textLength;
        int payloadLength = 2 + printableCharacterCount;

        if (textLength == 0
            || printableCharacterCount > 28
            || userIdLength > byte.MaxValue
            || destination.Length < payloadLength
            || !TryWriteAscii(userId, destination[2..])
            || !TryWriteAscii(text, destination[(2 + userIdLength)..]))
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = NoteBlockParser.TypeByte;
        destination[1] = (byte)userIdLength;
        bytesWritten = payloadLength;
        return true;
    }

    private static bool TryWriteAscii(string value, Span<byte> destination)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsAscii(character))
            {
                return false;
            }

            destination[index] = (byte)character;
        }

        return true;
    }
}
