using System.Buffers.Binary;

using MDI.Philips.M1350.IO.Hashing;

namespace MDI.Philips.M1350.Tests.IO.Hashing;

public sealed partial class Crc16Tests
{
    [TestMethod]
    public void HashBytes()
    {
        byte[] source = StandardCheckVectorBytes;
        byte[] destination = Crc16.Hash(source);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(destination);

        Assert.AreEqual(StandardCheckVectorCrc, crc);
    }

    [TestMethod]
    public void HashSpan()
    {
        ReadOnlySpan<byte> source = StandardCheckVectorBytes;
        Span<byte> destination = stackalloc byte[sizeof(ushort)];

        int size = Crc16.Hash(source, destination);
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(destination);

        Assert.AreEqual(sizeof(ushort), size);
        Assert.AreEqual(StandardCheckVectorCrc, crc);
    }

    [TestMethod]
    public void TryHash()
    {
        ReadOnlySpan<byte> source = StandardCheckVectorBytes;
        Span<byte> destination = stackalloc byte[sizeof(ushort)];

        bool result = Crc16.TryHash(source, destination, out int size);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), size);
    }

    [TestMethod]
    public void HashSmallSpanThrows()
    {
        static void Throws()
        {
            ReadOnlySpan<byte> source = stackalloc byte[32];
            Span<byte> destination = stackalloc byte[1];
            Crc16.Hash(source, destination);
        }

        Assert.ThrowsExactly<ArgumentException>(Throws);
    }

    [TestMethod]
    public void TryHashSmallDestinationBuffer()
    {
        ReadOnlySpan<byte> source = stackalloc byte[32];
        Span<byte> destination = stackalloc byte[1];

        bool result = Crc16.TryHash(source, destination, out int size);

        Assert.IsFalse(result);
        Assert.AreEqual(0, size);
    }

    [TestMethod]
    public void TryHashLargeDestinationBuffer()
    {
        ReadOnlySpan<byte> source = stackalloc byte[16];
        Span<byte> destination = stackalloc byte[32];

        bool result = Crc16.TryHash(source, destination, out int size);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), size);
    }
}
