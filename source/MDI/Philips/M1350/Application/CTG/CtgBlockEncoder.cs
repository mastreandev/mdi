using System.Buffers.Binary;

namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Encodes <see cref="CtgBlock" /> values into Philips M1350 C-block payload bytes.
/// </summary>
public static class CtgBlockEncoder
{
    /// <summary>
    /// Gets the number of bytes required to encode a <see cref="CtgBlock" />.
    /// This value is always <see cref="CtgBlockParser.PayloadLength" />.
    /// </summary>
    public static int EncodedLength => CtgBlockParser.PayloadLength;

    /// <summary>
    /// Attempts to encode a <see cref="CtgBlock" /> into <paramref name="destination" />.
    /// </summary>
    /// <param name="block">The block to encode.</param>
    /// <param name="destination">
    /// The span to write into. Must be at least <see cref="EncodedLength" /> bytes long.
    /// </param>
    /// <param name="bytesWritten">
    /// When this method returns <see langword="true" />, the number of bytes written to
    /// <paramref name="destination" />; otherwise zero.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="destination" /> is large enough;
    /// otherwise <see langword="false" />.
    /// </returns>
    public static bool TryEncode(in CtgBlock block, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < CtgBlockParser.PayloadLength)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = CtgBlockParser.TypeByte;

        BinaryPrimitives.WriteUInt16BigEndian(destination[1..], block.Status.RawValue);

        EncodeFhrSample(block.Fhr1Sample0, destination[3..]);
        EncodeFhrSample(block.Fhr1Sample1, destination[5..]);
        EncodeFhrSample(block.Fhr1Sample2, destination[7..]);
        EncodeFhrSample(block.Fhr1Sample3, destination[9..]);

        EncodeHeartRateSample(block.Fhr2Sample0, destination[11..]);
        EncodeHeartRateSample(block.Fhr2Sample1, destination[13..]);
        EncodeHeartRateSample(block.Fhr2Sample2, destination[15..]);
        EncodeHeartRateSample(block.Fhr2Sample3, destination[17..]);

        EncodeHeartRateSample(block.MhrSample0, destination[19..]);
        EncodeHeartRateSample(block.MhrSample1, destination[21..]);
        EncodeHeartRateSample(block.MhrSample2, destination[23..]);
        EncodeHeartRateSample(block.MhrSample3, destination[25..]);

        destination[27] = block.TocoSample0;
        destination[28] = block.TocoSample1;
        destination[29] = block.TocoSample2;
        destination[30] = block.TocoSample3;

        ushort hrModeWord = (ushort)(
            ((int)block.MhrMode << 13) |
            ((int)block.Hr2Mode << 9) |
            ((int)block.Hr1Mode << 5));
        BinaryPrimitives.WriteUInt16BigEndian(destination[31..], hrModeWord);

        destination[33] = (byte)((int)block.TocoMode << 1);
        destination[34] = block.FSpO2;

        bytesWritten = CtgBlockParser.PayloadLength;
        return true;
    }

    private static void EncodeFhrSample(FhrSample sample, Span<byte> destination)
    {
        byte highByte = (byte)(
            ((int)sample.Quality << 5) |
            ((int)sample.Fmp << 3) |
            ((sample.RawValue >> 8) & 0x07));
        destination[0] = highByte;
        destination[1] = (byte)(sample.RawValue & 0xFF);
    }

    private static void EncodeHeartRateSample(HeartRateSample sample, Span<byte> destination)
    {
        byte highByte = (byte)(
            ((int)sample.Quality << 5) |
            ((sample.RawValue >> 8) & 0x07));
        destination[0] = highByte;
        destination[1] = (byte)(sample.RawValue & 0xFF);
    }
}
