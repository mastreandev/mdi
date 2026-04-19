using System.Buffers;
using System.Buffers.Binary;

namespace MDI.Philips.M1350.Application.SpO2;

/// <summary>
/// Parses Philips M1350 S-block payloads into <see cref="SpO2Block" /> values.
/// </summary>
public static class SpO2BlockParser
{
    /// <summary>The expected byte length of an S-block payload.</summary>
    public const int PayloadLength = 4;

    /// <summary>The S-block type byte (<c>'S'</c>, 0x53).</summary>
    public const byte TypeByte = (byte)'S';

    /// <summary>
    /// Attempts to parse an S-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out SpO2Block block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        block = new SpO2Block(
            OxygenSaturation: payload[1],
            MaternalHeartRate: BinaryPrimitives.ReadUInt16BigEndian(payload[2..4]));

        return true;
    }

    /// <summary>
    /// Attempts to parse an S-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out SpO2Block block)
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
