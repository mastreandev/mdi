using System.Buffers;

namespace MDI.Philips.M1350.Application.Temperature;

/// <summary>
/// Parses Philips M1350 T-block payloads into <see cref="TemperatureBlock" /> values.
/// </summary>
public static class TemperatureBlockParser
{
    /// <summary>The expected byte length of a T-block payload.</summary>
    public const int PayloadLength = 2;

    /// <summary>The T-block type byte (<c>'T'</c>, 0x54).</summary>
    public const byte TypeByte = (byte)'T';

    /// <summary>
    /// Attempts to parse a T-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out TemperatureBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        block = new TemperatureBlock(payload[1]);
        return true;
    }

    /// <summary>
    /// Attempts to parse a T-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out TemperatureBlock block)
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
