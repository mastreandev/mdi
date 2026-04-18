using MDI.Philips.M1350.Application.CTG;

namespace MDI.Philips.M1350.Simulator;

internal sealed class M1350CtgGenerator
{
    private readonly Lock gate = new();
    private readonly ushort baseFhr1RawValue;
    private readonly ushort baseFhr2RawValue;
    private readonly ushort baseMhrRawValue;
    private readonly byte baseTocoRawValue;
    private readonly byte baseFSpO2Value;
    private readonly bool isFmpEnabled;
    private readonly bool isHr1TwinOffsetActive;
    private readonly bool isDecgLogicOn;
    private readonly bool isHrCrossChannelVerified;
    private readonly bool isTelemetryOn;
    private readonly bool isFSpO2Available;
    private readonly bool isMonitorOn;
    private readonly M1350CtgScenario scenario;
    private QuarterSecondSample sample0;
    private QuarterSecondSample sample1;
    private QuarterSecondSample sample2;
    private QuarterSecondSample sample3;
    private CtgBlock currentBlock;
    private int nextTickIndex;

    public M1350CtgGenerator(CtgBlock seedBlock, M1350CtgScenario scenario = M1350CtgScenario.Baseline)
    {
        this.sample0 = QuarterSecondSample.From(seedBlock.Fhr1Sample0, seedBlock.Fhr2Sample0, seedBlock.MhrSample0, seedBlock.TocoSample0, seedBlock.FSpO2);
        this.sample1 = QuarterSecondSample.From(seedBlock.Fhr1Sample1, seedBlock.Fhr2Sample1, seedBlock.MhrSample1, seedBlock.TocoSample1, seedBlock.FSpO2);
        this.sample2 = QuarterSecondSample.From(seedBlock.Fhr1Sample2, seedBlock.Fhr2Sample2, seedBlock.MhrSample2, seedBlock.TocoSample2, seedBlock.FSpO2);
        this.sample3 = QuarterSecondSample.From(seedBlock.Fhr1Sample3, seedBlock.Fhr2Sample3, seedBlock.MhrSample3, seedBlock.TocoSample3, seedBlock.FSpO2);
        this.currentBlock = seedBlock;
        this.nextTickIndex = 4;

        this.baseFhr1RawValue = Average(seedBlock.Fhr1Sample0.RawValue, seedBlock.Fhr1Sample1.RawValue, seedBlock.Fhr1Sample2.RawValue, seedBlock.Fhr1Sample3.RawValue);
        this.baseFhr2RawValue = Average(seedBlock.Fhr2Sample0.RawValue, seedBlock.Fhr2Sample1.RawValue, seedBlock.Fhr2Sample2.RawValue, seedBlock.Fhr2Sample3.RawValue);
        this.baseMhrRawValue = Average(seedBlock.MhrSample0.RawValue, seedBlock.MhrSample1.RawValue, seedBlock.MhrSample2.RawValue, seedBlock.MhrSample3.RawValue);
        this.baseTocoRawValue = Average(seedBlock.TocoSample0, seedBlock.TocoSample1, seedBlock.TocoSample2, seedBlock.TocoSample3);
        this.baseFSpO2Value = seedBlock.FSpO2;
        this.isFmpEnabled = seedBlock.Status.IsFmpEnabled || HasMovement(seedBlock);
        this.isHr1TwinOffsetActive = seedBlock.Status.IsHr1TwinOffsetActive;
        this.isDecgLogicOn = seedBlock.Status.IsDecgLogicOn;
        this.isHrCrossChannelVerified = seedBlock.Status.IsHrCrossChannelVerified;
        this.isTelemetryOn = seedBlock.Status.IsTelemetryOn;
        this.isFSpO2Available = seedBlock.Status.IsFSpO2Available && seedBlock.FSpO2 > 0;
        this.isMonitorOn = true;
        this.scenario = scenario;
    }

    public CtgBlock CurrentBlock
    {
        get
        {
            lock (this.gate)
            {
                return this.currentBlock;
            }
        }
    }

    public CtgBlock NextBlock()
    {
        lock (this.gate)
        {
            CtgBlock block = this.currentBlock;
            this.AdvanceWindow();
            this.currentBlock = this.CreateBlock();
            return block;
        }
    }

    private static ushort Average(ushort value0, ushort value1, ushort value2, ushort value3)
    {
        return (ushort)((value0 + value1 + value2 + value3) / 4);
    }

    private static byte Average(byte value0, byte value1, byte value2, byte value3)
    {
        return (byte)((value0 + value1 + value2 + value3) / 4);
    }

    private static bool HasMovement(CtgBlock block)
    {
        return block.Fhr1Sample0.Fmp == FmpValue.Movement
            || block.Fhr1Sample1.Fmp == FmpValue.Movement
            || block.Fhr1Sample2.Fmp == FmpValue.Movement
            || block.Fhr1Sample3.Fmp == FmpValue.Movement;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        int remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }

    private static int TriangleWave(int tickIndex, int amplitude, int period, int phase)
    {
        int cycle = PositiveModulo(tickIndex + phase, period);
        int halfPeriod = period / 2;
        int offset = cycle < halfPeriod
            ? cycle
            : period - cycle;

        return (offset * 2 * amplitude / halfPeriod) - amplitude;
    }

    private static ushort ClampHeartRate(int value)
    {
        return (ushort)Math.Clamp(value, 0, 1200);
    }

    private static byte ClampToco(int value)
    {
        return (byte)Math.Clamp(value, 0, byte.MaxValue);
    }

    private static byte ClampFSpO2(int value)
    {
        return (byte)Math.Clamp(value, 1, 100);
    }

    private static SignalQuality GetSignalQuality(int tickIndex, int phase)
    {
        return PositiveModulo(tickIndex + phase, 11) == 0
            ? SignalQuality.Yellow
            : SignalQuality.Green;
    }

    private void AdvanceWindow()
    {
        this.sample0 = this.CreateNextSample(this.sample3, this.nextTickIndex);
        this.sample1 = this.CreateNextSample(this.sample0, this.nextTickIndex + 1);
        this.sample2 = this.CreateNextSample(this.sample1, this.nextTickIndex + 2);
        this.sample3 = this.CreateNextSample(this.sample2, this.nextTickIndex + 3);
        this.nextTickIndex += 4;
    }

    private CtgBlock CreateBlock()
    {
        return new CtgBlock
        {
            Status = this.CreateStatusWord(),
            Fhr1Sample0 = this.sample0.ToFhrSample(),
            Fhr1Sample1 = this.sample1.ToFhrSample(),
            Fhr1Sample2 = this.sample2.ToFhrSample(),
            Fhr1Sample3 = this.sample3.ToFhrSample(),
            Fhr2Sample0 = this.sample0.ToFhr2Sample(),
            Fhr2Sample1 = this.sample1.ToFhr2Sample(),
            Fhr2Sample2 = this.sample2.ToFhr2Sample(),
            Fhr2Sample3 = this.sample3.ToFhr2Sample(),
            MhrSample0 = this.sample0.ToMhrSample(),
            MhrSample1 = this.sample1.ToMhrSample(),
            MhrSample2 = this.sample2.ToMhrSample(),
            MhrSample3 = this.sample3.ToMhrSample(),
            TocoSample0 = this.sample0.TocoRawValue,
            TocoSample1 = this.sample1.TocoRawValue,
            TocoSample2 = this.sample2.TocoRawValue,
            TocoSample3 = this.sample3.TocoRawValue,
            Hr1Mode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Decg,
            MhrMode = HrMode.ExternalMhr,
            TocoMode = TocoMode.External,
            FSpO2 = this.isFSpO2Available ? this.sample3.FSpO2 : (byte)0,
        };
    }

    private CtgStatusWord CreateStatusWord()
    {
        byte highByte = 0;
        byte lowByte = 0;

        if (this.isFmpEnabled)
        {
            highByte |= 0x80;
        }

        if (this.isHr1TwinOffsetActive)
        {
            highByte |= 0x40;
        }

        if (this.isDecgLogicOn)
        {
            highByte |= 0x04;
        }

        if (this.isHrCrossChannelVerified)
        {
            lowByte |= 0x80;
        }

        if (this.isTelemetryOn)
        {
            lowByte |= 0x40;
        }

        if (this.isFSpO2Available)
        {
            lowByte |= 0x10;
        }

        if (this.isMonitorOn)
        {
            lowByte |= 0x01;
        }

        return new CtgStatusWord((ushort)((highByte << 8) | lowByte));
    }

    private QuarterSecondSample CreateNextSample(QuarterSecondSample previous, int tickIndex)
    {
        ushort fhr1RawValue = ClampHeartRate(
            this.baseFhr1RawValue
            + TriangleWave(tickIndex, amplitude: 18, period: 28, phase: 4)
            + this.GetFhr1ScenarioOffset(tickIndex));
        ushort fhr2RawValue = ClampHeartRate(
            this.baseFhr2RawValue
            + TriangleWave(tickIndex, amplitude: 14, period: 24, phase: 10)
            + this.GetFhr2ScenarioOffset(tickIndex));
        ushort mhrRawValue = ClampHeartRate(
            this.baseMhrRawValue
            + TriangleWave(tickIndex, amplitude: 10, period: 20, phase: 7)
            + this.GetMhrScenarioOffset(tickIndex));
        byte tocoRawValue = ClampToco(
            this.baseTocoRawValue
            + TriangleWave(tickIndex, amplitude: 16, period: 32, phase: 0)
            + this.GetTocoScenarioOffset(tickIndex));
        byte fspo2 = this.isFSpO2Available
            ? ClampFSpO2(
                this.baseFSpO2Value
                + TriangleWave(tickIndex, amplitude: 2, period: 40, phase: 15)
                + this.GetFSpO2ScenarioOffset(tickIndex))
            : (byte)0;

        if (Math.Abs(fhr1RawValue - previous.Fhr1RawValue) > 8)
        {
            fhr1RawValue = ClampHeartRate(previous.Fhr1RawValue + (fhr1RawValue > previous.Fhr1RawValue ? 8 : -8));
        }

        if (Math.Abs(fhr2RawValue - previous.Fhr2RawValue) > 8)
        {
            fhr2RawValue = ClampHeartRate(previous.Fhr2RawValue + (fhr2RawValue > previous.Fhr2RawValue ? 8 : -8));
        }

        if (Math.Abs(mhrRawValue - previous.MhrRawValue) > 6)
        {
            mhrRawValue = ClampHeartRate(previous.MhrRawValue + (mhrRawValue > previous.MhrRawValue ? 6 : -6));
        }

        if (Math.Abs(tocoRawValue - previous.TocoRawValue) > 6)
        {
            tocoRawValue = ClampToco(previous.TocoRawValue + (tocoRawValue > previous.TocoRawValue ? 6 : -6));
        }

        FmpValue fmp = this.isFmpEnabled && PositiveModulo(tickIndex, this.GetFmpPeriod()) == 0
            ? FmpValue.Movement
            : FmpValue.None;

        return new QuarterSecondSample(
            fhr1RawValue,
            fmp,
            GetSignalQuality(tickIndex, phase: 0),
            fhr2RawValue,
            GetSignalQuality(tickIndex, phase: 3),
            mhrRawValue,
            GetSignalQuality(tickIndex, phase: 6),
            tocoRawValue,
            fspo2);
    }

    private static int GetTocoRiseWave(int tickIndex)
    {
        return Math.Max(0, TriangleWave(tickIndex, amplitude: 60, period: 24, phase: 2));
    }

    private int GetFhr1ScenarioOffset(int tickIndex)
    {
        return this.scenario switch
        {
            M1350CtgScenario.Baseline => 0,
            M1350CtgScenario.FhrRise => Math.Max(0, TriangleWave(tickIndex, amplitude: 48, period: 36, phase: 8)) + 24,
            M1350CtgScenario.FhrDrop => Math.Min(0, TriangleWave(tickIndex, amplitude: 84, period: 40, phase: 6)) - 48,
            M1350CtgScenario.TocoRise => -(GetTocoRiseWave(tickIndex) / 3),
            _ => 0,
        };
    }

    private int GetFhr2ScenarioOffset(int tickIndex)
    {
        return this.scenario switch
        {
            M1350CtgScenario.Baseline => 0,
            M1350CtgScenario.FhrRise => Math.Max(0, TriangleWave(tickIndex, amplitude: 20, period: 36, phase: 12)) + 8,
            M1350CtgScenario.FhrDrop => Math.Min(0, TriangleWave(tickIndex, amplitude: 28, period: 40, phase: 10)) - 12,
            M1350CtgScenario.TocoRise => 0,
            _ => 0,
        };
    }

    private int GetMhrScenarioOffset(int tickIndex)
    {
        return this.scenario switch
        {
            M1350CtgScenario.Baseline => 0,
            M1350CtgScenario.FhrRise => 0,
            M1350CtgScenario.FhrDrop => 0,
            M1350CtgScenario.TocoRise => Math.Max(0, TriangleWave(tickIndex, amplitude: 6, period: 24, phase: 6)),
            _ => 0,
        };
    }

    private int GetTocoScenarioOffset(int tickIndex)
    {
        return this.scenario switch
        {
            M1350CtgScenario.Baseline => 0,
            M1350CtgScenario.FhrRise => Math.Max(0, TriangleWave(tickIndex, amplitude: 6, period: 36, phase: 4)),
            M1350CtgScenario.FhrDrop => Math.Max(0, TriangleWave(tickIndex, amplitude: 10, period: 40, phase: 6)),
            M1350CtgScenario.TocoRise => GetTocoRiseWave(tickIndex) + 24,
            _ => 0,
        };
    }

    private int GetFSpO2ScenarioOffset(int tickIndex)
    {
        return this.scenario switch
        {
            M1350CtgScenario.FhrDrop => Math.Min(0, TriangleWave(tickIndex, amplitude: 2, period: 40, phase: 10)),
            M1350CtgScenario.Baseline => 0,
            M1350CtgScenario.FhrRise => 0,
            M1350CtgScenario.TocoRise => 0,
            _ => 0,
        };
    }

    private int GetFmpPeriod()
    {
        return this.scenario switch
        {
            M1350CtgScenario.FhrRise => 12,
            M1350CtgScenario.TocoRise => 18,
            M1350CtgScenario.FhrDrop => 32,
            M1350CtgScenario.Baseline => 24,
            _ => 24,
        };
    }

    private readonly record struct QuarterSecondSample(
        ushort Fhr1RawValue,
        FmpValue Fmp,
        SignalQuality Fhr1Quality,
        ushort Fhr2RawValue,
        SignalQuality Fhr2Quality,
        ushort MhrRawValue,
        SignalQuality MhrQuality,
        byte TocoRawValue,
        byte FSpO2)
    {
        public static QuarterSecondSample From(FhrSample fhr1, HeartRateSample fhr2, HeartRateSample mhr, byte tocoRawValue, byte fspo2)
        {
            return new QuarterSecondSample(
                fhr1.RawValue,
                fhr1.Fmp,
                fhr1.Quality,
                fhr2.RawValue,
                fhr2.Quality,
                mhr.RawValue,
                mhr.Quality,
                tocoRawValue,
                fspo2);
        }

        public FhrSample ToFhrSample()
        {
            return new FhrSample(this.Fhr1RawValue, this.Fmp, this.Fhr1Quality);
        }

        public HeartRateSample ToFhr2Sample()
        {
            return new HeartRateSample(this.Fhr2RawValue, this.Fhr2Quality);
        }

        public HeartRateSample ToMhrSample()
        {
            return new HeartRateSample(this.MhrRawValue, this.MhrQuality);
        }
    }
}
