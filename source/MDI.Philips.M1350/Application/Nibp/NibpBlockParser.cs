using System.Buffers;
using System.Buffers.Binary;

namespace MDI.Philips.M1350.Application.Nibp;

/// <summary>
/// Parses Philips M1350 P-block payloads into <see cref="NibpBlock" /> values.
/// </summary>
public static class NibpBlockParser
{
    /// <summary>The expected byte length of a P-block payload.</summary>
    public const int PayloadLength = 9;

    /// <summary>The P-block type byte (<c>'P'</c>, 0x50).</summary>
    public const byte TypeByte = (byte)'P';

    /// <summary>
    /// Attempts to parse a P-block from a span of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> payload, out NibpBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        block = new NibpBlock(
            SystolicPressure: BinaryPrimitives.ReadUInt16BigEndian(payload[1..3]),
            DiastolicPressure: BinaryPrimitives.ReadUInt16BigEndian(payload[3..5]),
            MeanPressure: BinaryPrimitives.ReadUInt16BigEndian(payload[5..7]),
            MaternalHeartRate: BinaryPrimitives.ReadUInt16BigEndian(payload[7..9]));

        return true;
    }

    /// <summary>
    /// Attempts to parse a P-block from a <see cref="ReadOnlySequence{T}" /> of decoded payload bytes.
    /// </summary>
    public static bool TryParse(ReadOnlySequence<byte> payload, out NibpBlock block)
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
