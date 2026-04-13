using MDI.Philips.M1350.Application.CTG;

namespace MDI.Tests.Philips.M1350.Application.CTG;

[TestClass]
public sealed class CtgBlockEncoderTests
{
    // -----------------------------------------------------------------------
    // EncodedLength
    // -----------------------------------------------------------------------

    [TestMethod]
    public void EncodedLengthShouldReturn35()
    {
        Assert.AreEqual(35, CtgBlockEncoder.EncodedLength);
    }

    // -----------------------------------------------------------------------
    // TryEncode — guard conditions
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldReturnFalseWhenDestinationIsTooSmall()
    {
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength - 1];

        bool result = CtgBlockEncoder.TryEncode(default, destination, out int bytesWritten);

        Assert.IsFalse(result);
        Assert.AreEqual(0, bytesWritten);
    }

    [TestMethod]
    public void ShouldReturnTrueAndWrite35BytesForDefaultBlock()
    {
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        bool result = CtgBlockEncoder.TryEncode(default, destination, out int bytesWritten);

        Assert.IsTrue(result);
        Assert.AreEqual(35, bytesWritten);
    }

    // -----------------------------------------------------------------------
    // Type byte
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldWriteTypeByte()
    {
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(default, destination, out _);

        Assert.AreEqual(CtgBlockParser.TypeByte, destination[0]);
    }

    // -----------------------------------------------------------------------
    // Round-trip: encode → parse → fields must match
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldRoundTripKnownBlock()
    {
        CtgBlock original = new()
        {
            Status = new CtgStatusWord(0x0041), // TelemetryOn + MonitorOn

            Fhr1Sample0 = new FhrSample(240, FmpValue.None, SignalQuality.Green),
            Fhr1Sample1 = new FhrSample(244, FmpValue.Movement, SignalQuality.Red),
            Fhr1Sample2 = new FhrSample(248, FmpValue.None, SignalQuality.Yellow),
            Fhr1Sample3 = new FhrSample(252, FmpValue.None, SignalQuality.Green),

            Fhr2Sample0 = new HeartRateSample(300, SignalQuality.Green),
            Fhr2Sample1 = new HeartRateSample(304, SignalQuality.Green),
            Fhr2Sample2 = new HeartRateSample(308, SignalQuality.Yellow),
            Fhr2Sample3 = new HeartRateSample(312, SignalQuality.Green),

            MhrSample0 = new HeartRateSample(280, SignalQuality.Red),
            MhrSample1 = new HeartRateSample(284, SignalQuality.Red),
            MhrSample2 = new HeartRateSample(288, SignalQuality.Green),
            MhrSample3 = new HeartRateSample(292, SignalQuality.Green),

            TocoSample0 = 0x10,
            TocoSample1 = 0x20,
            TocoSample2 = 0x30,
            TocoSample3 = 0x40,

            Hr1Mode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Decg,
            MhrMode = HrMode.Mecg,

            TocoMode = TocoMode.External,

            FSpO2 = 98,
        };

        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];
        CtgBlockEncoder.TryEncode(original, destination, out _);
        bool parsed = CtgBlockParser.TryParse((ReadOnlySpan<byte>)destination, out CtgBlock decoded);

        Assert.IsTrue(parsed);
        Assert.AreEqual(original, decoded);
    }

    // -----------------------------------------------------------------------
    // Status word encoding
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldEncodeStatusWordBigEndian()
    {
        CtgBlock block = new() { Status = new CtgStatusWord(0x8041) };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)0x80, destination[1]);
        Assert.AreEqual((byte)0x41, destination[2]);
    }

    // -----------------------------------------------------------------------
    // HR1 coding
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldEncodeBlankTraceHr1()
    {
        CtgBlock block = new() { Fhr1Sample0 = new FhrSample(0, FmpValue.None, SignalQuality.Unknown) };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)0x00, destination[3]);
        Assert.AreEqual((byte)0x00, destination[4]);
    }

    [TestMethod]
    public void ShouldEncode60BpmGreenHr1()
    {
        // 60 bpm = 240 raw; Green quality; no FMP.
        CtgBlock block = new() { Fhr1Sample0 = new FhrSample(240, FmpValue.None, SignalQuality.Green) };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)0x60, destination[3]); // [6:5]=11=Green, [4:3]=00=None, [2:0]=000
        Assert.AreEqual((byte)0xF0, destination[4]); // 240 & 0xFF
    }

    [TestMethod]
    public void ShouldEncode300BpmGreenHr1()
    {
        // 300 bpm = 1200 raw = 0x4B0; Green quality; no FMP.
        CtgBlock block = new() { Fhr1Sample0 = new FhrSample(1200, FmpValue.None, SignalQuality.Green) };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)0x64, destination[3]); // [6:5]=11=Green, [4:3]=00=None, [2:0]=100 (0x4B0>>8=4)
        Assert.AreEqual((byte)0xB0, destination[4]); // 0x4B0 & 0xFF
    }

    // -----------------------------------------------------------------------
    // HR Mode word encoding
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldEncodeHrModeWordAllUltrasound()
    {
        CtgBlock block = new()
        {
            MhrMode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Ultrasound,
            Hr1Mode = HrMode.Ultrasound,
        };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)0x22, destination[31]);
        Assert.AreEqual((byte)0x20, destination[32]);
    }

    // -----------------------------------------------------------------------
    // Toco Mode byte encoding
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow(TocoMode.NoTransducer, (byte)0x00)]
    [DataRow(TocoMode.External, (byte)0x02)]
    [DataRow(TocoMode.Iup, (byte)0x04)]
    [DataRow(TocoMode.Unknown, (byte)0x0E)]
    public void ShouldEncodeTocoMode(TocoMode mode, byte expectedByte)
    {
        CtgBlock block = new() { TocoMode = mode };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual(expectedByte, destination[33]);
    }

    // -----------------------------------------------------------------------
    // FSpO2 byte encoding
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldEncodeFSpO2Byte()
    {
        CtgBlock block = new()
        {
            Status = new CtgStatusWord(0x0010), // IsFSpO2Available
            FSpO2 = 97,
        };
        byte[] destination = new byte[CtgBlockEncoder.EncodedLength];

        CtgBlockEncoder.TryEncode(block, destination, out _);

        Assert.AreEqual((byte)97, destination[34]);
    }
}
