using System.Buffers;

using MDI.Philips.M1350.Application.Temperature;

namespace MDI.Tests.Philips.M1350.Application.Temperature;

[TestClass]
public sealed class TemperatureBlockParserTests
{
    [TestMethod]
    public void ShouldParseTemperatureFromSpan()
    {
        byte[] payload = [TemperatureBlockParser.TypeByte, 0x69];

        bool result = TemperatureBlockParser.TryParse(payload, out TemperatureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0x69, block.RawValue);
    }

    [TestMethod]
    public void ShouldParseTemperatureFromSequence()
    {
        byte[] payload = [TemperatureBlockParser.TypeByte, 0x00];

        bool result = TemperatureBlockParser.TryParse(new ReadOnlySequence<byte>(payload), out TemperatureBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((byte)0x00, block.RawValue);
    }
}
