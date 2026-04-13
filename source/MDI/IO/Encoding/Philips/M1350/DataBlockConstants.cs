namespace MDI.IO.Encoding.Philips.M1350;

public static class DataBlockConstants
{
    /// <summary>
    /// The size threshold for using stackalloc.
    /// </summary>
    public const int StackallocByteThreshold = 256; // TODO: tune this!

    /// <summary>
    /// The size threshold for renting from the ArrayPool.
    /// </summary>
    public const int ArrayPoolByteThreshold = 1024 * 1024; // TODO: tune this!

    /// <summary>
    /// Data linkage escape (0x10).
    /// </summary>
    public const byte DLE = 0x10;

    /// <summary>
    /// Start of text (0x02).
    /// </summary>
    public const byte STX = 0x02;

    /// <summary>
    /// End of text (0x03).
    /// </summary>
    public const byte ETX = 0x03;

    /// <summary>
    /// The start block is the byte sequence { DLE, STX }; in big Endian byte
    /// order: 0x1002.
    /// </summary>
    public static ReadOnlySpan<byte> StartBlock => [DLE, STX];

    /// <summary>
    /// The end block is the byte sequence { DLE, ETX }; in big Endian byte
    /// order: 0x1003.
    /// </summary>
    public static ReadOnlySpan<byte> EndBlock => [DLE, ETX];

    /// <summary>
    /// If a block is interrupted inside the CRC bytes, those bytes should be
    /// set to zeroes to ensure a correct read.
    /// </summary>
    public static ReadOnlySpan<byte> InterruptCrc => new byte[2];
}
