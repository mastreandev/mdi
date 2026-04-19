using System.Buffers;

namespace MDI.Philips.M1350.Application.EventMessage;

/// <summary>
/// Parses Philips M1350 MM-block payloads into <see cref="EventMessageBlock" /> values.
/// </summary>
public static class EventMessageBlockParser
{
    /// <summary>The expected byte length of an MM-block payload.</summary>
    public const int PayloadLength = 2;

    /// <summary>The MM-block first type byte (<c>'M'</c>, 0x4D).</summary>
    public const byte TypeByte = (byte)'M';

    /// <summary>The MM-block second type byte (<c>'M'</c>, 0x4D).</summary>
    public const byte SubtypeByte = (byte)'M';

    /// <summary>
    /// Attempts to parse an MM-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out EventMessageBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte || payload[1] != SubtypeByte)
        {
            block = default;
            return false;
        }

        block = new EventMessageBlock();
        return true;
    }

    /// <summary>
    /// Attempts to parse an MM-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out EventMessageBlock block)
    {
        if (payload.Length < PayloadLength)
        {
            block = default;
            return false;
        }

        Span<byte> buffer = stackalloc byte[PayloadLength];
        payload.Slice(0, PayloadLength).CopyTo(buffer);
        return TryParse(buffer, out block);
    }
}
