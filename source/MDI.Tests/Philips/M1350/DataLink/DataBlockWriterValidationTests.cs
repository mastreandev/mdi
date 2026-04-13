using System.Buffers;

using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350.DataLink;

[TestClass]
public sealed class DataBlockWriterValidationTests : IDisposable
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriterOptions options = new(SkipValidation: false);
    private readonly DataBlockWriter subject;

    public DataBlockWriterValidationTests()
    {
        this.subject = new DataBlockWriter(this.output, this.options);
    }

    public void Dispose()
    {
        this.subject.Dispose();
    }

    [TestMethod]
    public void ShouldThrowIfMissingStartBeforeData()
    {
        Assert.ThrowsExactly<InvalidOperationException>(this.WriteWithoutStartBeforeData);
    }

    [TestMethod]
    public void ShouldThrowIfMissingStartBeforeEnd()
    {
        Assert.ThrowsExactly<InvalidOperationException>(this.WriteWithoutStartBeforeEnd);
    }

    [TestMethod]
    public void ShouldThrowIfMissingEnd()
    {
        Assert.ThrowsExactly<InvalidOperationException>(this.WriteWithoutEnd);
    }

    [TestMethod]
    public void ShouldAllowStartBlockToInterruptStartBlock()
    {
        this.subject.WriteStart();
        this.subject.WriteStart();
        this.subject.Flush();

        byte[] expected =
        [
            .. DataBlockConstants.StartBlock,
            .. DataBlockConstants.StartBlock,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void ShouldAllowStartBlockToInterruptDataBlock()
    {
        this.subject.WriteStart();
        this.subject.WriteData();
        this.subject.WriteStart();
        this.subject.Flush();

        byte[] expected =
        [
            .. DataBlockConstants.StartBlock,
            .. DataBlockConstants.StartBlock,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void ShouldThrowIfStartBlockInterruptsEndBlockWithoutZeroCrc()
    {
        Assert.ThrowsExactly<InvalidOperationException>(this.InterruptEndWithoutZeroCrc);
    }

    [TestMethod]
    public void ShouldAllowStartBlockToInterruptZeroCrc()
    {
        this.subject.WriteStart();
        this.subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteCrc(interrupt: true);
        this.subject.WriteStart();
        this.subject.Flush();
    }

    private void InterruptEndWithoutZeroCrc()
    {
        this.subject.WriteStart();
        this.subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteStart();
        this.subject.Flush();
    }

    private void WriteWithoutEnd()
    {
        this.subject.WriteStart();
        this.subject.WriteData();
        this.subject.WriteCrc();
        this.subject.Flush();
    }

    private void WriteWithoutStartBeforeData()
    {
        this.subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();
    }

    private void WriteWithoutStartBeforeEnd()
    {
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();
    }
}
