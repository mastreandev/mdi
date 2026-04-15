namespace MDI.Philips.M1350.Application;

/// <summary>
/// Encodes Philips M1350 request blocks whose payload is a single requested block type byte.
/// </summary>
public static class RequestBlockEncoder
{
    /// <summary>The request block type byte (<c>'?'</c>, 0x3F).</summary>
    public const byte TypeByte = (byte)'?';

    /// <summary>Gets the number of bytes required to encode a request block.</summary>
    public static int EncodedLength => 2;

    /// <summary>
    /// Attempts to encode a request block into <paramref name="destination" />.
    /// </summary>
    public static bool TryEncode(byte requestedType, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < EncodedLength)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = TypeByte;
        destination[1] = requestedType;
        bytesWritten = EncodedLength;
        return true;
    }
}
