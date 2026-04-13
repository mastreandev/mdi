using System.Buffers.Binary;

using MDI.IO.Hashing;

namespace MDI.Tests.IO.Hashing;

[TestClass]
public sealed partial class Crc16Tests
{
    public const ushort StandardCheckVectorCrc = 0x31c3;

    public static readonly byte[] StandardCheckVectorBytes = "123456789"u8.ToArray();

    private readonly ReadOnlyMemory<byte> source = StandardCheckVectorBytes;
    private readonly Memory<byte> destination = new byte[sizeof(ushort)];
    private readonly Crc16 subject = new();

    [TestMethod]
    public void DefaultState()
    {
        Assert.AreEqual(sizeof(ushort), this.subject.HashLengthInBytes);

        this.subject.GetCurrentHash(this.destination.Span);
        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc);
    }

    [TestMethod]
    public void GetCurrentHash()
    {
        this.subject.Append(this.source.Span);
        this.subject.GetCurrentHash(this.destination.Span);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(StandardCheckVectorCrc, crc);
    }

    [TestMethod]
    public void Reset()
    {
        this.subject.Append(this.source.Span);
        this.subject.GetCurrentHash(this.destination.Span);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        this.subject.Reset();
        this.subject.GetCurrentHash(this.destination.Span);
        ushort crc2 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc2);
        Assert.AreNotEqual(crc1, crc2);
    }

    [TestMethod]
    public void GetHashAndReset()
    {
        this.subject.Append(this.source.Span);

        this.subject.GetHashAndReset(this.destination.Span);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        this.subject.GetCurrentHash(this.destination.Span);
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
        Assert.AreEqual(StandardCheckVectorCrc, crc);
    }

    [TestMethod]
    public void TryGetHashAndReset()
    {
        this.subject.Append(this.source.Span);
        bool result = this.subject.TryGetHashAndReset(this.destination.Span, out int bytesWritten);
        ushort crc1 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.IsTrue(result);
        Assert.AreEqual(sizeof(ushort), bytesWritten);
        Assert.AreEqual(StandardCheckVectorCrc, crc1);

        result = this.subject.TryGetCurrentHash(this.destination.Span, out _);
        ushort crc2 = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.IsTrue(result);
        Assert.AreEqual(0, crc2);
    }

    [TestMethod]
    public void Residue()
    {
        this.subject.Append(this.source.Span);
        this.subject.GetCurrentHash(this.destination.Span);
        this.subject.Append(this.destination.Span);
        this.subject.GetCurrentHash(this.destination.Span);

        ushort crc = BinaryPrimitives.ReadUInt16BigEndian(this.destination.Span);

        Assert.AreEqual(0, crc);
    }
}
