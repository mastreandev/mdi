using System.Buffers;

using MDI.IO.Encoding.Philips.M1350.DataLink;

namespace MDI.Tests.IO.Encoding.Philips.M1350.DataLink;

[TestClass]
public sealed class DataBlockEncoderTests
{
    [TestMethod]
    [DataRow(null, -1)]
    [DataRow(new byte[0], -1)]
    [DataRow(new byte[] { 0x00, 0x00, 0x00 }, -1)]
    [DataRow(new byte[] { 0x10, 0x00, 0x00 }, 0)]
    [DataRow(new byte[] { 0x00, 0x10, 0x00 }, 1)]
    [DataRow(new byte[] { 0x00, 0x00, 0x10 }, 2)]
    public void GetIndexOfFirstByteToEncode(byte[] value, int expectedIndex)
    {
        int index = DataBlockEncoder.GetIndexOfFirstByteToEncode(value);

        Assert.AreEqual(expectedIndex, index);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x99, 0x99, 0x99 })]
    [DataRow(new byte[] { 0x10, 0x99, 0x99 })]
    [DataRow(new byte[] { 0x99, 0x10, 0x99 })]
    [DataRow(new byte[] { 0x99, 0x99, 0x10 })]
    [DataRow(new byte[] { 0x10, 0x10, 0x10 })]
    public void Encode(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);

        int index = DataBlockEncoder.GetIndexOfFirstByteToEncode(value);
        int length = DataBlockEncoder.GetMaxEscapedLength(value.Length, index);

        Span<byte> destination = new byte[length];

        OperationStatus status = DataBlockEncoder.Encode(value, destination, out int written);

        Assert.AreEqual(OperationStatus.Done, status);

        if (index != -1)
        {
            Assert.IsTrue(value.Length <= written && destination.Length >= written);

            Span<byte> encodedBytes = destination.Slice(index, 2);

            Assert.AreEqual(DataBlockConstants.DLE, encodedBytes[0]);
            Assert.AreEqual(DataBlockConstants.DLE, encodedBytes[1]);
        }
    }
}
