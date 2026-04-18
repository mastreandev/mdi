using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Simulator;

namespace MDI.Tests.Philips.M1350.Simulator;

[TestClass]
public sealed class M1350CtgGeneratorTests
{
    [TestMethod]
    public void FhrRiseScenarioShouldRaiseFhr1AboveBaseline()
    {
        M1350CtgGenerator baseline = new(CreateSeedBlock(), M1350CtgScenario.Baseline);
        M1350CtgGenerator fhrRise = new(CreateSeedBlock(), M1350CtgScenario.FhrRise);

        double baselineAverage = GetAverageFhr1(baseline, skipBlocks: 2, blockCount: 6);
        double fhrRiseAverage = GetAverageFhr1(fhrRise, skipBlocks: 2, blockCount: 6);

        Assert.IsGreaterThan(baselineAverage, fhrRiseAverage);
    }

    [TestMethod]
    public void FhrDropScenarioShouldDropFhr1BelowBaseline()
    {
        M1350CtgGenerator baseline = new(CreateSeedBlock(), M1350CtgScenario.Baseline);
        M1350CtgGenerator fhrDrop = new(CreateSeedBlock(), M1350CtgScenario.FhrDrop);

        double baselineAverage = GetAverageFhr1(baseline, skipBlocks: 2, blockCount: 6);
        double fhrDropAverage = GetAverageFhr1(fhrDrop, skipBlocks: 2, blockCount: 6);

        Assert.IsLessThan(baselineAverage, fhrDropAverage);
    }

    [TestMethod]
    public void TocoRiseScenarioShouldRaiseTocoAboveBaseline()
    {
        M1350CtgGenerator baseline = new(CreateSeedBlock(), M1350CtgScenario.Baseline);
        M1350CtgGenerator tocoRise = new(CreateSeedBlock(), M1350CtgScenario.TocoRise);

        double baselineAverage = GetAverageToco(baseline, skipBlocks: 2, blockCount: 6);
        double tocoRiseAverage = GetAverageToco(tocoRise, skipBlocks: 2, blockCount: 6);

        Assert.IsGreaterThan(baselineAverage, tocoRiseAverage);
    }

    private static double GetAverageFhr1(M1350CtgGenerator generator, int skipBlocks, int blockCount)
    {
        long total = 0;
        int samples = 0;

        for (int index = 0; index < skipBlocks; index++)
        {
            generator.NextBlock();
        }

        for (int index = 0; index < blockCount; index++)
        {
            CtgBlock block = generator.NextBlock();
            total += block.Fhr1Sample0.RawValue;
            total += block.Fhr1Sample1.RawValue;
            total += block.Fhr1Sample2.RawValue;
            total += block.Fhr1Sample3.RawValue;
            samples += 4;
        }

        return (double)total / samples;
    }

    private static double GetAverageToco(M1350CtgGenerator generator, int skipBlocks, int blockCount)
    {
        long total = 0;
        int samples = 0;

        for (int index = 0; index < skipBlocks; index++)
        {
            generator.NextBlock();
        }

        for (int index = 0; index < blockCount; index++)
        {
            CtgBlock block = generator.NextBlock();
            total += block.TocoSample0;
            total += block.TocoSample1;
            total += block.TocoSample2;
            total += block.TocoSample3;
            samples += 4;
        }

        return (double)total / samples;
    }

    private static CtgBlock CreateSeedBlock()
    {
        return new CtgBlock
        {
            Status = new CtgStatusWord(0x0051),
            Fhr1Sample0 = new FhrSample(600, FmpValue.Movement, SignalQuality.Green),
            Fhr1Sample1 = new FhrSample(604, FmpValue.None, SignalQuality.Green),
            Fhr1Sample2 = new FhrSample(608, FmpValue.None, SignalQuality.Yellow),
            Fhr1Sample3 = new FhrSample(612, FmpValue.None, SignalQuality.Green),
            Fhr2Sample0 = new HeartRateSample(560, SignalQuality.Green),
            Fhr2Sample1 = new HeartRateSample(564, SignalQuality.Green),
            Fhr2Sample2 = new HeartRateSample(568, SignalQuality.Yellow),
            Fhr2Sample3 = new HeartRateSample(572, SignalQuality.Green),
            MhrSample0 = new HeartRateSample(320, SignalQuality.Green),
            MhrSample1 = new HeartRateSample(324, SignalQuality.Green),
            MhrSample2 = new HeartRateSample(328, SignalQuality.Green),
            MhrSample3 = new HeartRateSample(332, SignalQuality.Green),
            TocoSample0 = 20,
            TocoSample1 = 22,
            TocoSample2 = 24,
            TocoSample3 = 26,
            Hr1Mode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Decg,
            MhrMode = HrMode.ExternalMhr,
            TocoMode = TocoMode.External,
            FSpO2 = 98,
        };
    }
}
