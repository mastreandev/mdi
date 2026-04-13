using System.Buffers;

using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350.DataLink;

public sealed partial class DataBlockReaderTests
{
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
}
