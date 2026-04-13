using System.Buffers;

using MDI.IO.Hashing;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350.DataLink;

public sealed partial class DataBlockReaderTests
{
    [TestMethod]
    public void SingleBlockWithBadCrcShouldDiscardBlock()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.Flush();

        this.output.Write(new byte[] { 0x99, 0x99 });

        Check(this.output.WrittenMemory, 0, 0);
    }

    [TestMethod]
    public void MultipleBlocksWithBadCrcShouldDiscardToNextBlock()
    {
        ArrayBufferWriter<byte> output = new();
        using DataBlockWriter writer = new(output, new() { SkipValidation = true });

        writer.WriteStart();
        writer.WriteData(Buffer);
        writer.WriteEnd();
        writer.Flush();

        output.Write(new byte[] { 0x99, 0x99 });

        writer.WriteStart();
        writer.WriteData(Buffer);
        writer.WriteEnd();
        writer.Flush();

        Check(output.WrittenMemory, 0, 12);
    }

    [TestMethod]
    public void DleEtxWithoutStartShouldNotBeAcceptedAsBlock()
    {
        byte[] prefix = DataBlockConstants.EndBlock.ToArray();
        byte[] crc = Crc16.Hash(prefix);
        byte[] data =
        [
            .. prefix,
            .. crc,
        ];

        ReadOnlySequence<byte> buffer = new(data);

        bool result = DataBlockReader.TryRead(ref buffer, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void InvalidDataEscapeShouldDiscardPartialAndContinue()
    {
        ArrayBufferWriter<byte> validOutput = new();
        using DataBlockWriter validWriter = new(validOutput);

        validWriter.WriteStart();
        validWriter.WriteData(Buffer);
        validWriter.WriteEnd();
        validWriter.WriteCrc();
        validWriter.Flush();

        List<byte> data =
        [
            DataBlockConstants.DLE,
            DataBlockConstants.STX,
            0x01,
            DataBlockConstants.DLE,
            0x20,
        ];

        data.AddRange(validOutput.WrittenSpan.ToArray());

        byte[] payload = [.. data];
        ReadOnlySequence<byte> buffer = new(payload);

        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> block);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(Buffer, block.ToArray());
        Assert.AreEqual(0, buffer.Length);
    }
}
