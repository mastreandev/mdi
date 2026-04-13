using System.Buffers;

using MDI.IO.Encoding.Philips.M1350;

namespace MDI.Tests.IO.Encoding.Philips.M1350.Writer;

[TestClass]
public sealed class SkipValidationTrueWriterTests : IDisposable
{
    private readonly ArrayBufferWriter<byte> output = new();
    private readonly DataBlockWriterOptions options = new(SkipValidation: true);
    private readonly DataBlockWriter subject;

    public SkipValidationTrueWriterTests()
    {
        this.subject = new DataBlockWriter(this.output, this.options);
    }

    public void Dispose()
    {
        this.subject.Dispose();
    }

    [TestMethod]
    public void ShouldNotEscapeData()
    {
        Span<byte> value = new byte[16];
        value[0] = DataBlockConstants.DLE;

        this.subject.WriteData(value);
        this.subject.Flush();

        Assert.AreEqual(DataBlockConstants.DLE, this.output.WrittenSpan[0]);
        Assert.AreNotEqual(DataBlockConstants.DLE, this.output.WrittenSpan[1]);
    }

    [TestMethod]
    public void ShouldAllowMissingStart()
    {
        // subject.WriteStartBlock();
        this.subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] expected =
        [
            .. DataBlockConstants.EndBlock,
            0x00,
            0x00,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void ShouldAllowMissingData()
    {
        this.subject.WriteStart();
        // subject.WriteData();
        this.subject.WriteEnd();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] expected =
        [
            .. DataBlockConstants.StartBlock,
            .. DataBlockConstants.EndBlock,
            0x00,
            0x00,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }

    [TestMethod]
    public void ShouldAllowMissingEnd()
    {
        this.subject.WriteStart();
        this.subject.WriteData();
        // subject.WriteEndBlock();
        this.subject.WriteCrc();
        this.subject.Flush();

        byte[] expected =
        [
            .. DataBlockConstants.StartBlock,
            0x00,
            0x00,
        ];

        CollectionAssert.AreEqual(expected, this.output.WrittenSpan.ToArray());
    }
}
