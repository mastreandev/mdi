using System.Buffers;
using System.Text;

namespace MDI.Philips.M1350.Application.Notes;

/// <summary>
/// Parses Philips M1350 N-block payloads into <see cref="NoteBlock" /> values.
/// </summary>
public static class NoteBlockParser
{
    /// <summary>The minimum byte length of an N-block payload.</summary>
    public const int MinimumPayloadLength = 2;

    /// <summary>The N-block type byte (<c>'N'</c>, 0x4E).</summary>
    public const byte TypeByte = (byte)'N';

    /// <summary>
    /// Attempts to parse an N-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out NoteBlock block)
    {
        if (payload.Length < MinimumPayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        int userIdLength = payload[1];
        int totalTextLength = payload.Length - MinimumPayloadLength;
        if (userIdLength > totalTextLength)
        {
            block = default;
            return false;
        }

        int userIdStart = 2;
        int textStart = userIdStart + userIdLength;
        string userId = Encoding.ASCII.GetString(payload.Slice(userIdStart, userIdLength));
        string text = Encoding.ASCII.GetString(payload[textStart..]);

        block = new NoteBlock(userId, text);
        return true;
    }

    /// <summary>
    /// Attempts to parse an N-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out NoteBlock block)
    {
        if (payload.Length < MinimumPayloadLength)
        {
            block = default;
            return false;
        }

        byte[] buffer = payload.ToArray();
        return TryParse(buffer, out block);
    }
}
