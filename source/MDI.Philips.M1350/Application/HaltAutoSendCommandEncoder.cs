namespace MDI.Philips.M1350.Application;

/// <summary>
/// Encodes Philips M1350 H-block payloads that halt automatic CTG transmission.
/// </summary>
public static class HaltAutoSendCommandEncoder
{
    /// <summary>The encoded length of an H-block payload.</summary>
    public const int EncodedLength = 1;

    /// <summary>The H-block type byte (<c>'H'</c>, 0x48).</summary>
    public const byte TypeByte = (byte)'H';

    /// <summary>
    /// Attempts to encode an H-block payload.
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
