using System.Buffers;
using System.Text;

namespace MDI.Philips.M1350.Application.Failure;

/// <summary>
/// Parses Philips M1350 F-block payloads into <see cref="FailureBlock" /> values.
/// </summary>
public static class FailureBlockParser
{
    /// <summary>The expected byte length of an F-block payload.</summary>
    public const int PayloadLength = 4;

    /// <summary>The F-block type byte (<c>'F'</c>, 0x46).</summary>
    public const byte TypeByte = (byte)'F';

    /// <summary>
    /// Attempts to parse an F-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out FailureBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        block = new FailureBlock(Encoding.ASCII.GetString(payload.Slice(1, 3)));
        return true;
    }

    /// <summary>
    /// Attempts to parse an F-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out FailureBlock block)
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
