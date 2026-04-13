using System.Buffers;

namespace MDI.IO.Encoding.Philips.M1350;

/// <summary>
/// Encodes raw Philips M1350 data blocks.
/// </summary>
public static class DataBlockEncoder
{
    /// <summary>
    /// Gets the expansion factor for this encoder.
    /// </summary>
    public static int MaxOutputBytesPerInputBytes { get; } = 2;

    /// <summary>
    /// Gets the index of the first byte to escape in the source span.
    /// </summary>
    /// <param name="value">The span to index into.</param>
    /// <returns>The position of the first byte to escape or -1 if none is found.</returns>
    public static int GetIndexOfFirstByteToEncode(ReadOnlySpan<byte> value)
    {
        return value.IndexOf(DataBlockConstants.DLE);
    }

    /// <summary>
    /// Gets the maximum possible length of an encoded destination span.
    /// </summary>
    /// <param name="length">The length of the source span.</param>
    /// <param name="indexOfFirstByteToEscape">The position of the first byte to escape.</param>
    /// <returns>The maximum possible length of an encoded destination span.</returns>
    public static int GetMaxEscapedLength(int length, int indexOfFirstByteToEscape = 0)
    {
        return indexOfFirstByteToEscape + (MaxOutputBytesPerInputBytes * (length - indexOfFirstByteToEscape));
    }

    /// <summary>
    /// Encodes the source span into the destination span.
    /// </summary>
    /// <param name="value">The source span.</param>
    /// <param name="destination">The destination span.</param>
    /// <param name="written">The number of bytes written into the destination.</param>
    /// <returns>The operation status.</returns>
    public static OperationStatus Encode(ReadOnlySpan<byte> value, Span<byte> destination, out int written)
    {
        written = 0;
        foreach (byte b in value)
        {
            if (b == DataBlockConstants.DLE)
            {
                destination[written++] = DataBlockConstants.DLE;
            }

            destination[written++] = b;
        }

        return OperationStatus.Done;
    }
}
