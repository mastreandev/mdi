using System.Buffers;

namespace MDI.Tests.Philips.M1350.DataLink;

public sealed partial class DataBlockReaderTests
{
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
}
