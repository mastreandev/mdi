using System.Buffers.Binary;
using System.IO.Hashing;

namespace MDI.IO.Hashing;

/// <summary>
/// Provides an implementation of CRC-16/XMODEM, as used in ITU-T V.41.
/// </summary>
/// <remarks>
/// This implementation emits the answer in Big Endian byte order so that the
/// CRC residue relationship (CRC(message concat CRC(message))) is a fixed value)
/// holds. For CRC-16/XMODEM this stable output is the byte sequence { 0x00,
/// 0x00 }, the Big Endian representation of 0x0000.
/// <br />
/// <seealso href="https://reveng.sourceforge.io/crc-catalogue/16.htm#crc.cat.crc-16-xmodem" />
/// </remarks>
public sealed partial class Crc16 : NonCryptographicHashAlgorithm
{
    private const ushort InitialState = 0x0000;
    private const int Size = sizeof(ushort);

    public static byte[] Hash(byte[] source)
    {
        return Hash(new ReadOnlySpan<byte>(source));
    }

    public static byte[] Hash(ReadOnlySpan<byte> source)
    {
        byte[] ret = new byte[Size];
        _ = StaticHash(source, ret);
        return ret;
    }

    public static bool TryHash(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < Size)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = StaticHash(source, destination);
        return true;
    }

    public static int Hash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("Argument is too short.", nameof(destination));
        }

        return StaticHash(source, destination);
    }

    private static int StaticHash(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ushort crc = InitialState;
        crc = Update(crc, source);
        BinaryPrimitives.WriteUInt16BigEndian(destination, crc);
        return Size;
    }

    private static ushort Update(ushort crc, ReadOnlySpan<byte> source)
    {
        for (int i = 0; i < source.Length; ++i)
        {
            crc = (ushort)((crc << 8) ^ Lookup[(crc >> 8) ^ source[i]]);
        }

        return crc;
    }

    private ushort crc = InitialState;

    /// <summary>
    /// Initializes a new instance of the <see cref="Crc16" /> class.
    /// </summary>
    public Crc16() : base(Size) { }

    public override void Append(ReadOnlySpan<byte> source)
    {
        this.crc = Update(this.crc, source);
    }

    public override void Reset()
    {
        this.crc = InitialState;
    }

    protected override void GetCurrentHashCore(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt16BigEndian(destination, this.crc);
    }

    protected override void GetHashAndResetCore(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt16BigEndian(destination, this.crc);
        this.crc = InitialState;
    }
}
