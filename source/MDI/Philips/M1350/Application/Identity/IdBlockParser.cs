using System.Buffers;
using System.Text;

namespace MDI.Philips.M1350.Application.Identity;

/// <summary>
/// Parses Philips M1350 I-block payloads into <see cref="IdBlock" /> values.
/// </summary>
public static class IdBlockParser
{
    /// <summary>The expected byte length of an I-block payload.</summary>
    public const int PayloadLength = 27;

    /// <summary>The I-block type byte (<c>'I'</c>, 0x49).</summary>
    public const byte TypeByte = (byte)'I';

    /// <summary>
    /// Attempts to parse an I-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out IdBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        block = new IdBlock(
            IdCode: Encoding.ASCII.GetString(payload.Slice(1, 6)),
            ProtocolRevision: Encoding.ASCII.GetString(payload.Slice(7, 3)),
            SoftwareRevision: Encoding.ASCII.GetString(payload.Slice(10, 7)),
            SerialNumber: Encoding.ASCII.GetString(payload.Slice(17, 10)));

        return true;
    }

    /// <summary>
    /// Attempts to parse an I-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out IdBlock block)
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
