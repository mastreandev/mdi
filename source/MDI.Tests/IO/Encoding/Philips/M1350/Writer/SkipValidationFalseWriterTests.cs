using System.Buffers;

using MDI.IO.Encoding.Philips.M1350;

namespace MDI.Tests.IO.Encoding.Philips.M1350.Writer;

[TestClass]
public sealed class SkipValidationFalseWriterTests : IDisposable
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriterOptions options = new(SkipValidation: false);
    private readonly DataBlockWriter subject;

    public SkipValidationFalseWriterTests()
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
        _ = Assert.Throws<InvalidOperationException>(() =>
        {
            // subject.WriteStartBlock();
            this.subject.WriteData();
            this.subject.WriteEnd();
            this.subject.WriteCrc();
            this.subject.Flush();
        });
    }

    [TestMethod]
    public void ShouldThrowIfMissingStartBeforeEnd()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
        {
            // subject.WriteStartBlock();
            this.subject.WriteEnd();
            this.subject.WriteCrc();
            this.subject.Flush();
        });
    }

    [TestMethod]
    public void ShouldThrowIfMissingEnd()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
        {
            this.subject.WriteStart();
            this.subject.WriteData();
            // subject.WriteEndBlock();
            this.subject.WriteCrc();
            this.subject.Flush();
        });
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
        _ = Assert.Throws<InvalidOperationException>(() =>
        {
            this.subject.WriteStart();
            this.subject.WriteData();
            this.subject.WriteEnd();
            // subject.WriteCrc(interrupt: true);
            this.subject.WriteStart();
            this.subject.Flush();
        });
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
}
