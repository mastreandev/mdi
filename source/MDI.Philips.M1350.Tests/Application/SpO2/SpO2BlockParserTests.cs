using System.Buffers;

using MDI.Philips.M1350.Application.SpO2;

namespace MDI.Philips.M1350.Tests.Application.SpO2;

[TestClass]
public sealed class SpO2BlockParserTests
{
    [TestMethod]
    public void ShouldParseSpO2FieldsFromSpan()
    {
        byte[] payload = [SpO2BlockParser.TypeByte, 0xC8, 0x01, 0x2C];

        bool result = SpO2BlockParser.TryParse(payload, out SpO2Block block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0xC8, block.OxygenSaturation);
        Assert.AreEqual((ushort)300, block.MaternalHeartRate);
    }

    [TestMethod]
    public void ShouldParseSpO2FieldsFromSequence()
    {
        byte[] payload = [SpO2BlockParser.TypeByte, 0x00, 0xFF, 0xFF];

        bool result = SpO2BlockParser.TryParse(new ReadOnlySequence<byte>(payload), out SpO2Block block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0x00, block.OxygenSaturation);
        Assert.AreEqual((ushort)0xFFFF, block.MaternalHeartRate);
    }
}
