using System.Buffers.Binary;

using MDI.IO.Hashing;

namespace MDI.Tests.IO.Hashing;

[TestClass]
public sealed class Crc16Tests
{
    private readonly ReadOnlyMemory<byte> source = Constants.KnownMessageBytes;
    private readonly Memory<byte> destination = new byte[sizeof(ushort)];
    private readonly Crc16 subject = new();

    [TestMethod]
    public void HashBytes()
    {
        byte[] source = Constants.KnownMessageBytes;
        byte[] destination = Crc16.Hash(source);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(destination);

        Assert.AreEqual(Constants.KnownMessageCrc, crc);
    }

    [TestMethod]
    public void HashStandardCheckVector()
    {
        byte[] source = "123456789"u8.ToArray();
        byte[] destination = Crc16.Hash(source);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(destination);

        Assert.AreEqual(0x31c3, crc);
    }

    [TestMethod]
    public void HashSpan()
    {
        ReadOnlySpan<byte> source = Constants.KnownMessageBytes;
        Span<byte> destination = stackalloc byte[sizeof(ushort)];

        int size = Crc16.Hash(source, destination);
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(destination);

        Assert.AreEqual(sizeof(ushort), size);
        Assert.AreEqual(Constants.KnownMessageCrc, crc);
    }

    [TestMethod]
    public void TryHash()
    {
        ReadOnlySpan<byte> source = Constants.KnownMessageBytes;
        Span<byte> destination = stackalloc byte[sizeof(ushort)];

        bool result = Crc16.TryHash(source, destination, out int size);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), size);
    }

    [TestMethod]
    public void HashSmallSpanThrows()
    {
        _ = Assert.Throws<ArgumentException>(() =>
        {
            ReadOnlySpan<byte> source = stackalloc byte[32];
            Span<byte> destination = stackalloc byte[1];

            _ = Crc16.Hash(source, destination);
        });
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

    [TestMethod]
    public void DefaultState()
    {
        Assert.AreEqual(sizeof(ushort), this.subject.HashLengthInBytes);

        _ = this.subject.GetCurrentHash(this.destination.Span);
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc);
    }

    [TestMethod]
    public void GetCurrentHash()
    {
        this.subject.Append(this.source.Span);
        _ = this.subject.GetCurrentHash(this.destination.Span);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(Constants.KnownMessageCrc, crc);
    }

    [TestMethod]
    public void Reset()
    {
        this.subject.Append(this.source.Span);
        _ = this.subject.GetCurrentHash(this.destination.Span);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        this.subject.Reset();
        _ = this.subject.GetCurrentHash(this.destination.Span);
        ushort crc2 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc2);
        Assert.AreNotEqual(crc1, crc2);
    }

    [TestMethod]
    public void GetHashAndReset()
    {
        this.subject.Append(this.source.Span);

        _ = this.subject.GetHashAndReset(this.destination.Span);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        _ = this.subject.GetCurrentHash(this.destination.Span);
        ushort crc2 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc2);
        Assert.AreNotEqual(crc1, crc2);
    }

    [TestMethod]
    public void TryGetCurrentHash()
    {
        this.subject.Append(this.source.Span);
        bool result = this.subject.TryGetCurrentHash(this.destination.Span, out int bytesWritten);
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), bytesWritten);
        Assert.AreEqual(Constants.KnownMessageCrc, crc);
    }

    [TestMethod]
    public void TryGetHashAndReset()
    {
        this.subject.Append(this.source.Span);
        bool result = this.subject.TryGetHashAndReset(this.destination.Span, out int bytesWritten);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), bytesWritten);
        Assert.AreEqual(Constants.KnownMessageCrc, crc1);

        result = this.subject.TryGetCurrentHash(this.destination.Span, out _);
        ushort crc2 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.IsTrue(result);
        Assert.AreEqual(0, crc2);
    }

    [TestMethod]
    public void Residue()
    {
        this.subject.Append(this.source.Span);
        _ = this.subject.GetCurrentHash(this.destination.Span);
        this.subject.Append(this.destination.Span);
        _ = this.subject.GetCurrentHash(this.destination.Span);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc);
    }
}
