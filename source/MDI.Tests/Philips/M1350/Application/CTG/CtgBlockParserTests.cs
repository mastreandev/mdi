using System.Buffers;

using MDI.Philips.M1350.Application.CTG;

namespace MDI.Tests.Philips.M1350.Application.CTG;

[TestClass]
public sealed class CtgBlockParserTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal valid 35-byte C-block payload with all non-specified bytes
    /// set to zero.
    /// </summary>
    private static byte[] BuildPayload(Action<byte[]> configure)
    {
        byte[] payload = new byte[CtgBlockParser.PayloadLength];
        payload[0] = CtgBlockParser.TypeByte;
        configure(payload);
        return payload;
    }

    private static CtgBlock ParsePayload(byte[] payload)
    {
        bool result = CtgBlockParser.TryParse((ReadOnlySpan<byte>)payload, out CtgBlock block);
        Assert.IsTrue(result);
        return block;
    }

    private static CtgBlock ParsePayload(ReadOnlySequence<byte> payload)
    {
        bool result = CtgBlockParser.TryParse(payload, out CtgBlock block);
        Assert.IsTrue(result);
        return block;
    }

    // -----------------------------------------------------------------------
    // TryParse(ReadOnlySpan<byte>) — guard conditions
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldReturnFalseWhenPayloadIsTooShort()
    {
        byte[] payload = new byte[CtgBlockParser.PayloadLength - 1];
        payload[0] = CtgBlockParser.TypeByte;

        bool result = CtgBlockParser.TryParse(payload, out CtgBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void ShouldReturnFalseWhenTypeByteMismatch()
    {
        byte[] payload = BuildPayload(_ => { });
        payload[0] = (byte)'I'; // wrong block type

        bool result = CtgBlockParser.TryParse(payload, out CtgBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void ShouldReturnTrueForMinimalValidPayload()
    {
        byte[] payload = BuildPayload(_ => { });

        bool result = CtgBlockParser.TryParse(payload, out _);

        Assert.IsTrue(result);
    }

    // -----------------------------------------------------------------------
    // TryParse(ReadOnlySequence<byte>) — delegates to span overload
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldReturnFalseWhenSequenceIsTooShort()
    {
        byte[] payload = new byte[CtgBlockParser.PayloadLength - 1];
        payload[0] = CtgBlockParser.TypeByte;
        ReadOnlySequence<byte> sequence = new(payload);

        bool result = CtgBlockParser.TryParse(sequence, out CtgBlock block);

        Assert.IsFalse(result);
        Assert.AreEqual(default, block);
    }

    [TestMethod]
    public void ShouldParseIdenticallyFromSpanAndSequence()
    {
        byte[] payload = BuildPayload(p =>
        {
            p[1] = 0x00;
            p[2] = 0x41; // TelemetryOn + MonitorOn
            p[3] = 0x60; // HR1[0] high: Green quality, no FMP, upper bits 0
            p[4] = 0xF0; // HR1[0] low: 240 raw
        });

        CtgBlock spanBlock = ParsePayload(payload);
        CtgBlock seqBlock = ParsePayload(new ReadOnlySequence<byte>(payload));

        Assert.AreEqual(spanBlock, seqBlock);
    }

    // -----------------------------------------------------------------------
    // Status word
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseStatusWord()
    {
        // Status high byte = 0x80 (FmpEnabled), low byte = 0x41 (TelemetryOn + MonitorOn)
        byte[] payload = BuildPayload(p => { p[1] = 0x80; p[2] = 0x41; });

        CtgBlock block = ParsePayload(payload);

        Assert.IsTrue(block.Status.IsFmpEnabled);
        Assert.IsTrue(block.Status.IsTelemetryOn);
        Assert.IsTrue(block.Status.IsMonitorOn);
        Assert.IsFalse(block.Status.IsFSpO2Available);
    }

    // -----------------------------------------------------------------------
    // FHR1 coding (Table 3-6)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseBlankTraceHr1()
    {
        // High byte = 0x00, low byte = 0x00 → raw = 0
        byte[] payload = BuildPayload(p => { p[3] = 0x00; p[4] = 0x00; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)0, block.Fhr1Sample0.RawValue);
        Assert.AreEqual(SignalQuality.Unknown, block.Fhr1Sample0.Quality);
        Assert.AreEqual(FmpValue.None, block.Fhr1Sample0.Fmp);
    }

    [TestMethod]
    public void ShouldParse60BpmGreenHr1()
    {
        // 60 bpm = 240 raw (240 / 4 = 60).
        // High byte 0x60: bits[6:5]=11=Green, bits[4:3]=00=None, bits[2:0]=000 (upper 3 of 240).
        // Low byte 0xF0 = 240 (lower 8 of 240).
        byte[] payload = BuildPayload(p => { p[3] = 0x60; p[4] = 0xF0; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)240, block.Fhr1Sample0.RawValue);
        Assert.AreEqual(SignalQuality.Green, block.Fhr1Sample0.Quality);
        Assert.AreEqual(FmpValue.None, block.Fhr1Sample0.Fmp);
    }

    [TestMethod]
    public void ShouldParse300BpmGreenHr1()
    {
        // 300 bpm = 1200 raw = 0x4B0.
        // High byte 0x64: bits[6:5]=11=Green, bits[4:3]=00=None, bits[2:0]=100 (upper 3 of 0x4B0).
        // Low byte 0xB0 = 176 (lower 8 of 0x4B0).
        byte[] payload = BuildPayload(p => { p[3] = 0x64; p[4] = 0xB0; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)1200, block.Fhr1Sample0.RawValue);
        Assert.AreEqual(SignalQuality.Green, block.Fhr1Sample0.Quality);
    }

    [TestMethod]
    public void ShouldParseFmpMovementOnHr1()
    {
        // High byte 0x08: bits[4:3]=01=Movement, rest 0.
        byte[] payload = BuildPayload(p => { p[3] = 0x08; p[4] = 0x00; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual(FmpValue.Movement, block.Fhr1Sample0.Fmp);
    }

    // -----------------------------------------------------------------------
    // HR2 and MHR coding (Table 3-7 — no FMP field)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseHr2Sample()
    {
        // HR2[0] at offsets 11..12.
        // High byte 0x40: bits[6:5]=10=Yellow, bits[2:0]=000.
        // Low byte 0x80 = 128 raw.
        byte[] payload = BuildPayload(p => { p[11] = 0x40; p[12] = 0x80; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)128, block.Fhr2Sample0.RawValue);
        Assert.AreEqual(SignalQuality.Yellow, block.Fhr2Sample0.Quality);
    }

    [TestMethod]
    public void ShouldParseMhrSample()
    {
        // MHR[0] at offsets 19..20.
        byte[] payload = BuildPayload(p => { p[19] = 0x60; p[20] = 0x50; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)0x50, block.MhrSample0.RawValue);
        Assert.AreEqual(SignalQuality.Green, block.MhrSample0.Quality);
    }

    // -----------------------------------------------------------------------
    // Toco bytes
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseTocoBytes()
    {
        byte[] payload = BuildPayload(p =>
        {
            p[27] = 0x10;
            p[28] = 0x20;
            p[29] = 0x30;
            p[30] = 0x40;
        });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((byte)0x10, block.TocoSample0);
        Assert.AreEqual((byte)0x20, block.TocoSample1);
        Assert.AreEqual((byte)0x30, block.TocoSample2);
        Assert.AreEqual((byte)0x40, block.TocoSample3);
    }

    // -----------------------------------------------------------------------
    // HR Mode word (Tables 3-9, 3-10)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseHrModeWordAllUltrasound()
    {
        // MHR=001, HR2=001, HR1=001 (all Ultrasound).
        // High byte: [7:5]=001, [4]=0, [3:1]=001, [0]=0 = 0b_001_0_001_0 = 0x22
        // Low byte:  [7:5]=001, [4:0]=0                 = 0b_001_0_0000 = 0x20
        byte[] payload = BuildPayload(p => { p[31] = 0x22; p[32] = 0x20; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual(HrMode.Ultrasound, block.MhrMode);
        Assert.AreEqual(HrMode.Ultrasound, block.Hr2Mode);
        Assert.AreEqual(HrMode.Ultrasound, block.Hr1Mode);
    }

    [TestMethod]
    public void ShouldParseHrModeWordMixed()
    {
        // MHR=010 (Decg), HR2=011 (Mecg), HR1=100 (ExternalMhr).
        // High byte: [7:5]=010, [4]=0, [3:1]=011, [0]=0 = 0b_010_0_011_0 = 0x46
        // Low byte:  [7:5]=100, [4:0]=0                 = 0b_100_0_0000 = 0x80
        byte[] payload = BuildPayload(p => { p[31] = 0x46; p[32] = 0x80; });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual(HrMode.Decg, block.MhrMode);
        Assert.AreEqual(HrMode.Mecg, block.Hr2Mode);
        Assert.AreEqual(HrMode.ExternalMhr, block.Hr1Mode);
    }

    // -----------------------------------------------------------------------
    // Toco Mode byte (Table 3-11)
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow((byte)0x00, TocoMode.NoTransducer)]
    [DataRow((byte)0x02, TocoMode.External)]
    [DataRow((byte)0x04, TocoMode.Iup)]
    [DataRow((byte)0x0E, TocoMode.Unknown)]
    public void ShouldParseTocoMode(byte tocoModeByte, TocoMode expectedMode)
    {
        byte[] payload = BuildPayload(p => p[33] = tocoModeByte);

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual(expectedMode, block.TocoMode);
    }

    // -----------------------------------------------------------------------
    // FSpO2 byte (Table 3-12)
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldParseFSpO2Byte()
    {
        byte[] payload = BuildPayload(p =>
        {
            p[2] = 0x10; // IsFSpO2Available
            p[34] = 98;
        });

        CtgBlock block = ParsePayload(payload);

        Assert.IsTrue(block.Status.IsFSpO2Available);
        Assert.AreEqual((byte)98, block.FSpO2);
    }

    [TestMethod]
    public void FSpO2ByteShouldBeParsedRegardlessOfAvailabilityFlag()
    {
        // Parser stores the raw byte unconditionally; callers check the flag.
        byte[] payload = BuildPayload(p =>
        {
            p[2] = 0x00; // IsFSpO2Available = false
            p[34] = 0x42;
        });

        CtgBlock block = ParsePayload(payload);

        Assert.IsFalse(block.Status.IsFSpO2Available);
        Assert.AreEqual((byte)0x42, block.FSpO2);
    }

    // -----------------------------------------------------------------------
    // Four-sample ordering
    // -----------------------------------------------------------------------

    [TestMethod]
    public void ShouldPreserveSampleOrderOldestToNewest()
    {
        // Set each HR1 sample's low byte to a distinct value.
        byte[] payload = BuildPayload(p =>
        {
            p[4] = 0x01; // HR1[0] — oldest
            p[6] = 0x02; // HR1[1]
            p[8] = 0x03; // HR1[2]
            p[10] = 0x04; // HR1[3] — newest
        });

        CtgBlock block = ParsePayload(payload);

        Assert.AreEqual((ushort)1, block.Fhr1Sample0.RawValue);
        Assert.AreEqual((ushort)2, block.Fhr1Sample1.RawValue);
        Assert.AreEqual((ushort)3, block.Fhr1Sample2.RawValue);
        Assert.AreEqual((ushort)4, block.Fhr1Sample3.RawValue);
    }
}
