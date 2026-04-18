using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350.Simulator.Application.Identity;

/// <summary>
/// Encodes <see cref="IdBlock" /> values into Philips M1350 I-block payload bytes.
/// </summary>
public static class IdBlockEncoder
{
    /// <summary>
    /// Gets the number of bytes required to encode an <see cref="IdBlock" />.
    /// This value is always <see cref="IdBlockParser.PayloadLength" />.
    /// </summary>
    public static int EncodedLength => IdBlockParser.PayloadLength;

    /// <summary>
    /// Attempts to encode an <see cref="IdBlock" /> into <paramref name="destination" />.
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
    /// <see langword="true" /> if <paramref name="destination" /> is large enough and the block
    /// contains fixed-width ASCII fields; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryEncode(in IdBlock block, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < EncodedLength)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = IdBlockParser.TypeByte;

        if (!TryWriteAsciiExact(block.IdCode, destination[1..7])
            || !TryWriteAsciiExact(block.ProtocolRevision, destination[7..10])
            || !TryWriteAsciiExact(block.SoftwareRevision, destination[10..17])
            || !TryWriteAsciiExact(block.SerialNumber, destination[17..27]))
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = EncodedLength;
        return true;
    }

    private static bool TryWriteAsciiExact(string value, Span<byte> destination)
    {
        if (value.Length != destination.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character > 0x7F)
            {
                return false;
            }

            destination[index] = (byte)character;
        }

        return true;
    }
}
