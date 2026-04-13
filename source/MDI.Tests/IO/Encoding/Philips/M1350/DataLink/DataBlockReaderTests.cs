using System.Buffers;

using MDI.IO.Encoding.Philips.M1350.DataLink;
using MDI.IO.Hashing;

namespace MDI.Tests.IO.Encoding.Philips.M1350.DataLink;

[TestClass]
public sealed class DataBlockReaderTests : IDisposable
{
    private static readonly byte[] Buffer = new byte[8];

    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriter writer;

    public DataBlockReaderTests()
    {
        this.writer = new DataBlockWriter(this.output);
    }

    [TestMethod]
    public void SingleEmptyBlockShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void MultipleEmptyBlocksShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.writer.WriteStart();
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.writer.WriteStart();
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 3, 0);
    }

    [TestMethod]
    public void SingleEscapeBlockShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData([DataBlockConstants.DLE]);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void SingleEscapeBlockShouldReturnDecodedPayload()
    {
        this.writer.WriteStart();
        this.writer.WriteData([DataBlockConstants.DLE]);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        ReadOnlySequence<byte> buffer = new(this.output.WrittenMemory);

        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> block);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new byte[] { DataBlockConstants.DLE }, block.ToArray());
    }

    [TestMethod]
    public void SingleBlockWithoutCrcShouldReturnBlockStart()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 0, 12);
    }

    [TestMethod]
    public void SingleBlockWithOneCrcByteShouldReturnBlockStart()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.Flush();

        this.output.Write(new byte[] { 0x99 });

        Check(this.output.WrittenMemory, 0, 13);
    }

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
    public void SingleMultiEscapeBlockShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData([DataBlockConstants.DLE, DataBlockConstants.DLE]);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void SingleBlockShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void MultipleBlocksShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 3, 0);
    }

    [TestMethod]
    public void SingleBlockWithStartEscapeShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData([DataBlockConstants.DLE]);
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void SingleBlockWithEndEscapeShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteData([DataBlockConstants.DLE]);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void SingleBlockWithMiddleEscapeShouldTruncateBuffer()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteData([DataBlockConstants.DLE]);
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void NoiseBeforeAndAfterSingleBlockShouldTruncateBuffer()
    {
        this.output.Write(Buffer);

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.output.Write(Buffer);

        Check(this.output.WrittenMemory, 1, 0);
    }

    [TestMethod]
    public void NoiseBetweenMultipleBlocksShouldTruncateBuffer()
    {
        this.output.Write(Buffer);

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.output.Write(Buffer);

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.output.Write(Buffer);

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.WriteEnd();
        this.writer.WriteCrc();
        this.writer.Flush();

        this.output.Write(Buffer);

        Check(this.output.WrittenMemory, 3, 0);
    }

    [TestMethod]
    public void IncompleteBlockShouldReturnEscapePosition()
    {
        this.output.Write(Buffer);

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.Flush();

        Check(this.output.WrittenMemory, 0, Buffer.Length + 2);
    }

    [TestMethod]
    public void IncompleteBlockInterruptedByIncompleteBlockShouldReturnSecondEscapePosition()
    {
        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.Flush();

        this.writer.WriteStart();
        this.writer.WriteData(Buffer);
        this.writer.Flush();

        Check(this.output.WrittenMemory, 0, Buffer.Length + 2);
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

    private static void Check(ReadOnlyMemory<byte> memory, int expectedBlockCount = 0, int expectedBufferLength = 0)
    {
        ReadOnlySequence<byte> buffer = new(memory);

        int actualBlockCount = 0;
        while (DataBlockReader.TryRead(ref buffer, out _))
        {
            actualBlockCount++;
        }

        Assert.AreEqual(expectedBlockCount, actualBlockCount);
        Assert.AreEqual(expectedBufferLength, buffer.Length);
    }

    public void Dispose()
    {
        this.writer.Dispose();
    }
}
