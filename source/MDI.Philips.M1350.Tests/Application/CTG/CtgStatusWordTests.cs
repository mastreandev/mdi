using MDI.Philips.M1350.Application.CTG;

namespace MDI.Philips.M1350.Tests.Application.CTG;

[TestClass]
public sealed class CtgStatusWordTests
{
    [TestMethod]
    [DataRow((ushort)0x8000, true, false, false, false, false, false, false, false, false)]
    [DataRow((ushort)0x4000, false, true, false, false, false, false, false, false, false)]
    [DataRow((ushort)0x0400, false, false, true, false, false, false, false, false, false)]
    [DataRow((ushort)0x0080, false, false, false, true, false, false, false, false, false)]
    [DataRow((ushort)0x0040, false, false, false, false, true, false, false, false, false)]
    [DataRow((ushort)0x0010, false, false, false, false, false, true, false, false, false)]
    [DataRow((ushort)0x0004, false, false, false, false, false, false, true, false, false)]
    [DataRow((ushort)0x0002, false, false, false, false, false, false, false, true, false)]
    [DataRow((ushort)0x0001, false, false, false, false, false, false, false, false, true)]
    [DataRow((ushort)0x0000, false, false, false, false, false, false, false, false, false)]
    public void StatusBitsShouldBeIsolated(
        ushort rawValue,
        bool isFmpEnabled,
        bool isHr1TwinOffsetActive,
        bool isDecgLogicOn,
        bool isHrCrossChannelVerified,
        bool isTelemetryOn,
        bool isFSpO2Available,
        bool isCtgDataDeleted,
        bool isCtgDataInserted,
        bool isMonitorOn)
    {
        CtgStatusWord subject = new(rawValue);

        Assert.AreEqual(isFmpEnabled, subject.IsFmpEnabled);
        Assert.AreEqual(isHr1TwinOffsetActive, subject.IsHr1TwinOffsetActive);
        Assert.AreEqual(isDecgLogicOn, subject.IsDecgLogicOn);
        Assert.AreEqual(isHrCrossChannelVerified, subject.IsHrCrossChannelVerified);
        Assert.AreEqual(isTelemetryOn, subject.IsTelemetryOn);
        Assert.AreEqual(isFSpO2Available, subject.IsFSpO2Available);
        Assert.AreEqual(isCtgDataDeleted, subject.IsCtgDataDeleted);
        Assert.AreEqual(isCtgDataInserted, subject.IsCtgDataInserted);
        Assert.AreEqual(isMonitorOn, subject.IsMonitorOn);
    }

    [TestMethod]
    public void AllFlagsShouldBeSetWhenAllBitsAreLive()
    {
        // Set all 9 live bits simultaneously.
        const ushort allLiveBits = 0x8000 | 0x4000 | 0x0400 | 0x0080 | 0x0040 |
                                   0x0010 | 0x0004 | 0x0002 | 0x0001;
        CtgStatusWord subject = new(allLiveBits);

        Assert.IsTrue(subject.IsFmpEnabled);
        Assert.IsTrue(subject.IsHr1TwinOffsetActive);
        Assert.IsTrue(subject.IsDecgLogicOn);
        Assert.IsTrue(subject.IsHrCrossChannelVerified);
        Assert.IsTrue(subject.IsTelemetryOn);
        Assert.IsTrue(subject.IsFSpO2Available);
        Assert.IsTrue(subject.IsCtgDataDeleted);
        Assert.IsTrue(subject.IsCtgDataInserted);
        Assert.IsTrue(subject.IsMonitorOn);
    }

    [TestMethod]
    public void RawValueShouldRoundTrip()
    {
        CtgStatusWord original = new(0xC4D7);

        Assert.AreEqual((ushort)0xC4D7, original.RawValue);
    }
}
