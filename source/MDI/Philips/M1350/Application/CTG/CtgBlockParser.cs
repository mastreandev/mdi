using System.Buffers;
using System.Buffers.Binary;

namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Parses Philips M1350 C-block payloads into <see cref="CtgBlock" /> values.
/// </summary>
public static class CtgBlockParser
{
    /// <summary>The expected byte length of a C-block payload.</summary>
    public const int PayloadLength = 35;

    /// <summary>The C-block type byte (<c>'C'</c>, 0x43).</summary>
    public const byte TypeByte = (byte)'C';

    /// <summary>
    /// Attempts to parse a C-block from a span of decoded payload bytes.
    /// </summary>
    /// <param name="payload">
    /// The decoded payload bytes beginning with the block type byte.
    /// At least <see cref="PayloadLength" /> bytes must be present.
    /// </param>
    /// <param name="block">
    /// When this method returns <see langword="true" />, contains the parsed
    /// <see cref="CtgBlock" />; otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="payload" /> is at least
    /// <see cref="PayloadLength" /> bytes long and the first byte equals
    /// <see cref="TypeByte" />; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> payload, out CtgBlock block)
    {
        if (payload.Length < PayloadLength || payload[0] != TypeByte)
        {
            block = default;
            return false;
        }

        ushort statusWord = BinaryPrimitives.ReadUInt16BigEndian(payload[1..]);
        ushort hrModeWord = BinaryPrimitives.ReadUInt16BigEndian(payload[31..]);

        block = new CtgBlock
        {
            Status = new CtgStatusWord(statusWord),

            Fhr1Sample0 = ParseFhrSample(payload[3], payload[4]),
            Fhr1Sample1 = ParseFhrSample(payload[5], payload[6]),
            Fhr1Sample2 = ParseFhrSample(payload[7], payload[8]),
            Fhr1Sample3 = ParseFhrSample(payload[9], payload[10]),

            Fhr2Sample0 = ParseHeartRateSample(payload[11], payload[12]),
            Fhr2Sample1 = ParseHeartRateSample(payload[13], payload[14]),
            Fhr2Sample2 = ParseHeartRateSample(payload[15], payload[16]),
            Fhr2Sample3 = ParseHeartRateSample(payload[17], payload[18]),

            MhrSample0 = ParseHeartRateSample(payload[19], payload[20]),
            MhrSample1 = ParseHeartRateSample(payload[21], payload[22]),
            MhrSample2 = ParseHeartRateSample(payload[23], payload[24]),
            MhrSample3 = ParseHeartRateSample(payload[25], payload[26]),

            TocoSample0 = payload[27],
            TocoSample1 = payload[28],
            TocoSample2 = payload[29],
            TocoSample3 = payload[30],

            MhrMode = (HrMode)((hrModeWord >> 13) & 0x07),
            Hr2Mode = (HrMode)((hrModeWord >> 9) & 0x07),
            Hr1Mode = (HrMode)((hrModeWord >> 5) & 0x07),

            TocoMode = (TocoMode)((payload[33] >> 1) & 0x07),

            FSpO2 = payload[34],
        };

        return true;
    }

    /// <summary>
    /// Attempts to parse a C-block from a <see cref="ReadOnlySequence{T}" /> of
    /// decoded payload bytes.
    /// </summary>
    /// <param name="payload">
    /// The decoded payload bytes beginning with the block type byte.
    /// At least <see cref="PayloadLength" /> bytes must be present.
    /// </param>
    /// <param name="block">
    /// When this method returns <see langword="true" />, contains the parsed
    /// <see cref="CtgBlock" />; otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="payload" /> is at least
    /// <see cref="PayloadLength" /> bytes long and the first byte equals
    /// <see cref="TypeByte" />; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryParse(ReadOnlySequence<byte> payload, out CtgBlock block)
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

    private static FhrSample ParseFhrSample(byte highByte, byte lowByte)
    {
        ushort rawValue = (ushort)(((highByte & 0x07) << 8) | lowByte);
        SignalQuality quality = (SignalQuality)((highByte >> 5) & 0x03);
        FmpValue fmp = (FmpValue)((highByte >> 3) & 0x03);
        return new FhrSample(rawValue, fmp, quality);
    }

    private static HeartRateSample ParseHeartRateSample(byte highByte, byte lowByte)
    {
        ushort rawValue = (ushort)(((highByte & 0x07) << 8) | lowByte);
        SignalQuality quality = (SignalQuality)((highByte >> 5) & 0x03);
        return new HeartRateSample(rawValue, quality);
    }
}
