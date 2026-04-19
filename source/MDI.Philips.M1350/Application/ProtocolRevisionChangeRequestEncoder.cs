namespace MDI.Philips.M1350.Application;

/// <summary>
/// Encodes Philips M1350 protocol revision change request V-blocks.
/// </summary>
public static class ProtocolRevisionChangeRequestEncoder
{
    /// <summary>The V-block type byte (<c>'V'</c>, 0x56).</summary>
    public const byte TypeByte = (byte)'V';

    /// <summary>Gets the number of bytes required to encode a revision change request block.</summary>
    public static int EncodedLength => 4;

    /// <summary>
    /// Attempts to encode a 3-character protocol revision request, for example <c>A20</c>.
    /// </summary>
    public static bool TryEncode(ReadOnlySpan<char> requestedRevision, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < EncodedLength || requestedRevision.Length != 3)
        {
            bytesWritten = 0;
            return false;
        }

        if (requestedRevision[0] > 0x7F || requestedRevision[1] > 0x7F || requestedRevision[2] > 0x7F)
        {
            bytesWritten = 0;
            return false;
        }

        destination[0] = TypeByte;
        destination[1] = (byte)requestedRevision[0];
        destination[2] = (byte)requestedRevision[1];
        destination[3] = (byte)requestedRevision[2];
        bytesWritten = EncodedLength;
        return true;
    }
}
