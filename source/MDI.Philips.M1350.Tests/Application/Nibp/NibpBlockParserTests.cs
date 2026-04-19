using System.Buffers;

using MDI.Philips.M1350.Application.Nibp;

namespace MDI.Philips.M1350.Tests.Application.Nibp;

[TestClass]
public sealed class NibpBlockParserTests
{
    private static byte[] BuildPayload(Action<byte[]> configure)
    {
        byte[] payload = new byte[NibpBlockParser.PayloadLength];
        payload[0] = NibpBlockParser.TypeByte;
        configure(payload);
        return payload;
    }

    [TestMethod]
    public void ShouldParseNibpFieldsFromSpan()
    {
        byte[] payload = BuildPayload(p =>
        {
            p[1] = 0x00; p[2] = 0x78;
            p[3] = 0x00; p[4] = 0x50;
            p[5] = 0x00; p[6] = 0x60;
            p[7] = 0x01; p[8] = 0x90;
        });

        bool result = NibpBlockParser.TryParse(payload, out NibpBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((ushort)120, block.SystolicPressure);
        Assert.AreEqual((ushort)80, block.DiastolicPressure);
        Assert.AreEqual((ushort)96, block.MeanPressure);
        Assert.AreEqual((ushort)400, block.MaternalHeartRate);
    }

    [TestMethod]
    public void ShouldParseNibpFieldsFromSequence()
    {
        byte[] payload = BuildPayload(p =>
        {
            p[1] = 0x00; p[2] = 0x64;
            p[3] = 0x00; p[4] = 0x3C;
            p[5] = 0x00; p[6] = 0x50;
            p[7] = 0x00; p[8] = 0x00;
        });

        bool result = NibpBlockParser.TryParse(new ReadOnlySequence<byte>(payload), out NibpBlock block);

        Assert.IsTrue(result);
        Assert.AreEqual((ushort)100, block.SystolicPressure);
        Assert.AreEqual((ushort)60, block.DiastolicPressure);
        Assert.AreEqual((ushort)80, block.MeanPressure);
        Assert.AreEqual((ushort)0, block.MaternalHeartRate);
    }
}
