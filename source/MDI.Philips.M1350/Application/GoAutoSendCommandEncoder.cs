namespace MDI.Philips.M1350.Application;

/// <summary>
/// Encodes Philips M1350 G-block payloads that enable automatic CTG transmission.
/// </summary>
public static class GoAutoSendCommandEncoder
{
    /// <summary>The encoded length of a G-block payload.</summary>
    public const int EncodedLength = 1;

    /// <summary>The G-block type byte (<c>'G'</c>, 0x47).</summary>
    public const byte TypeByte = (byte)'G';

    /// <summary>
    /// Attempts to encode a G-block payload.
    /// </summary>
    public static bool TryEncode(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < EncodedLength)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = TypeByte;
        bytesWritten = EncodedLength;
        return true;
    }
}
