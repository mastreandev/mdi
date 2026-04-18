using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Simulator.Application.Identity;

using M1350ProtocolRevision = MDI.Philips.M1350.ProtocolRevision;

namespace MDI.Philips.M1350.Simulator;

internal sealed record class SimulatorHostOptions(
    string IdCode,
    string ProtocolRevision,
    string SoftwareRevision,
    string SerialNumber,
    M1350CtgScenario Scenario,
    ushort Fhr1RawValue,
    ushort Fhr2RawValue,
    ushort MhrRawValue,
    byte TocoRawValue,
    byte FSpO2,
    TimeSpan AutoSendInterval,
    bool EmitPowerOnIdentity)
{
    private static readonly M1350ProtocolRevision FSpO2MinimumRevision = new('A', 2, 0);

    public static SimulatorHostParseResult Parse(IReadOnlyList<string> args)
    {
        string idCode = "M1350A";
        string protocolRevision = "A20";
        string softwareRevision = "A.03.00";
        string serialNumber = "3019G10010";
        M1350CtgScenario scenario = M1350CtgScenario.Baseline;
        ushort fhr1RawValue = 600;
        ushort fhr2RawValue = 560;
        ushort mhrRawValue = 320;
        byte tocoRawValue = 20;
        byte fspo2 = 98;
        TimeSpan autoSendInterval = TimeSpan.FromSeconds(1);
        bool emitPowerOnIdentity = true;

        for (int index = 0; index < args.Count; index++)
        {
            string token = args[index];
            if (token is "-h" or "--help" or "/?")
            {
                return SimulatorHostParseResult.Help();
            }

            SplitOptionToken(token, out string optionName, out string? inlineValue);

            switch (optionName)
            {
                case "--id-code":
                    if (!TryReadStringValue(args, ref index, optionName, inlineValue, out idCode, out string? idCodeError))
                    {
                        return SimulatorHostParseResult.Error(idCodeError!);
                    }

                    break;

                case "--protocol-revision":
                    if (!TryReadStringValue(args, ref index, optionName, inlineValue, out protocolRevision, out string? protocolRevisionError))
                    {
                        return SimulatorHostParseResult.Error(protocolRevisionError!);
                    }

                    break;

                case "--software-revision":
                    if (!TryReadStringValue(args, ref index, optionName, inlineValue, out softwareRevision, out string? softwareRevisionError))
                    {
                        return SimulatorHostParseResult.Error(softwareRevisionError!);
                    }

                    break;

                case "--serial-number":
                    if (!TryReadStringValue(args, ref index, optionName, inlineValue, out serialNumber, out string? serialNumberError))
                    {
                        return SimulatorHostParseResult.Error(serialNumberError!);
                    }

                    break;

                case "--scenario":
                    if (!TryReadStringValue(args, ref index, optionName, inlineValue, out string rawScenario, out string? scenarioError))
                    {
                        return SimulatorHostParseResult.Error(scenarioError!);
                    }

                    if (!TryParseScenario(rawScenario, out scenario))
                    {
                        return SimulatorHostParseResult.Error(
                            "Option '--scenario' must be one of: baseline, fhr-rise, fhr-drop, toco-rise.");
                    }

                    break;

                case "--fhr1":
                    if (!TryReadUShortValue(args, ref index, optionName, inlineValue, out fhr1RawValue, out string? fhr1Error))
                    {
                        return SimulatorHostParseResult.Error(fhr1Error!);
                    }

                    break;

                case "--fhr2":
                    if (!TryReadUShortValue(args, ref index, optionName, inlineValue, out fhr2RawValue, out string? fhr2Error))
                    {
                        return SimulatorHostParseResult.Error(fhr2Error!);
                    }

                    break;

                case "--mhr":
                    if (!TryReadUShortValue(args, ref index, optionName, inlineValue, out mhrRawValue, out string? mhrError))
                    {
                        return SimulatorHostParseResult.Error(mhrError!);
                    }

                    break;

                case "--toco":
                    if (!TryReadByteValue(args, ref index, optionName, inlineValue, out tocoRawValue, out string? tocoError))
                    {
                        return SimulatorHostParseResult.Error(tocoError!);
                    }

                    break;

                case "--fspo2":
                    if (!TryReadByteValue(args, ref index, optionName, inlineValue, out fspo2, out string? fspo2Error))
                    {
                        return SimulatorHostParseResult.Error(fspo2Error!);
                    }

                    break;

                case "--auto-send-interval-ms":
                    if (!TryReadIntValue(args, ref index, optionName, inlineValue, out int intervalMilliseconds, out string? intervalError))
                    {
                        return SimulatorHostParseResult.Error(intervalError!);
                    }

                    if (intervalMilliseconds <= 0)
                    {
                        return SimulatorHostParseResult.Error("Option '--auto-send-interval-ms' must be greater than zero.");
                    }

                    autoSendInterval = TimeSpan.FromMilliseconds(intervalMilliseconds);
                    break;

                case "--no-power-on-identity":
                    if (inlineValue is not null)
                    {
                        return SimulatorHostParseResult.Error("Option '--no-power-on-identity' does not accept a value.");
                    }

                    emitPowerOnIdentity = false;
                    break;

                default:
                    return SimulatorHostParseResult.Error($"Unknown option '{token}'.");
            }
        }

        SimulatorHostOptions options = new(
            idCode,
            protocolRevision,
            softwareRevision,
            serialNumber,
            scenario,
            fhr1RawValue,
            fhr2RawValue,
            mhrRawValue,
            tocoRawValue,
            fspo2,
            autoSendInterval,
            emitPowerOnIdentity);

        return Validate(options, out string? validationError)
            ? SimulatorHostParseResult.Success(options)
            : SimulatorHostParseResult.Error(validationError!);
    }

    public CtgBlock CreateCtgBlock()
    {
        bool fspo2Available = M1350ProtocolRevision.Parse(this.ProtocolRevision, provider: null) >= FSpO2MinimumRevision;

        return new CtgBlock
        {
            Status = new CtgStatusWord((ushort)(fspo2Available ? 0x0051 : 0x0041)),
            Fhr1Sample0 = new FhrSample(this.Fhr1RawValue, FmpValue.Movement, SignalQuality.Green),
            Fhr1Sample1 = new FhrSample(AddRawValue(this.Fhr1RawValue, 4), FmpValue.None, SignalQuality.Green),
            Fhr1Sample2 = new FhrSample(AddRawValue(this.Fhr1RawValue, 8), FmpValue.None, SignalQuality.Yellow),
            Fhr1Sample3 = new FhrSample(AddRawValue(this.Fhr1RawValue, 12), FmpValue.None, SignalQuality.Green),
            Fhr2Sample0 = new HeartRateSample(this.Fhr2RawValue, SignalQuality.Green),
            Fhr2Sample1 = new HeartRateSample(AddRawValue(this.Fhr2RawValue, 4), SignalQuality.Green),
            Fhr2Sample2 = new HeartRateSample(AddRawValue(this.Fhr2RawValue, 8), SignalQuality.Yellow),
            Fhr2Sample3 = new HeartRateSample(AddRawValue(this.Fhr2RawValue, 12), SignalQuality.Green),
            MhrSample0 = new HeartRateSample(this.MhrRawValue, SignalQuality.Green),
            MhrSample1 = new HeartRateSample(AddRawValue(this.MhrRawValue, 4), SignalQuality.Green),
            MhrSample2 = new HeartRateSample(AddRawValue(this.MhrRawValue, 8), SignalQuality.Green),
            MhrSample3 = new HeartRateSample(AddRawValue(this.MhrRawValue, 12), SignalQuality.Green),
            TocoSample0 = this.TocoRawValue,
            TocoSample1 = AddByteValue(this.TocoRawValue, 2),
            TocoSample2 = AddByteValue(this.TocoRawValue, 4),
            TocoSample3 = AddByteValue(this.TocoRawValue, 6),
            Hr1Mode = HrMode.Ultrasound,
            Hr2Mode = HrMode.Decg,
            MhrMode = HrMode.ExternalMhr,
            TocoMode = TocoMode.External,
            FSpO2 = fspo2Available ? this.FSpO2 : (byte)0,
        };
    }

    public IdBlock CreateIdentityBlock()
    {
        return new(
            this.IdCode,
            this.ProtocolRevision,
            this.SoftwareRevision,
            this.SerialNumber);
    }

    private static bool TryParseScenario(string value, out M1350CtgScenario scenario)
    {
        scenario = value.ToUpperInvariant() switch
        {
            "BASELINE" => M1350CtgScenario.Baseline,
            "FHR-RISE" => M1350CtgScenario.FhrRise,
            "FHR-DROP" => M1350CtgScenario.FhrDrop,
            "TOCO-RISE" => M1350CtgScenario.TocoRise,
            _ => default,
        };

        return value.Equals("baseline", StringComparison.OrdinalIgnoreCase)
            || value.Equals("fhr-rise", StringComparison.OrdinalIgnoreCase)
            || value.Equals("fhr-drop", StringComparison.OrdinalIgnoreCase)
            || value.Equals("toco-rise", StringComparison.OrdinalIgnoreCase);
    }

    private static ushort AddRawValue(ushort value, int delta)
    {
        return (ushort)Math.Min(1200, value + delta);
    }

    private static byte AddByteValue(byte value, int delta)
    {
        return (byte)Math.Min(byte.MaxValue, value + delta);
    }

    private static void SplitOptionToken(string token, out string optionName, out string? inlineValue)
    {
        int separatorIndex = token.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            optionName = token;
            inlineValue = null;
            return;
        }

        optionName = token[..separatorIndex];
        inlineValue = token[(separatorIndex + 1)..];
    }

    private static bool TryReadByteValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string? inlineValue,
        out byte value,
        out string? error)
    {
        if (!TryReadStringValue(args, ref index, optionName, inlineValue, out string rawValue, out error))
        {
            value = 0;
            return false;
        }

        if (!byte.TryParse(rawValue, out value))
        {
            error = $"Option '{optionName}' requires an integer value in the range 0 to 255.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadIntValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string? inlineValue,
        out int value,
        out string? error)
    {
        if (!TryReadStringValue(args, ref index, optionName, inlineValue, out string rawValue, out error))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(rawValue, out value))
        {
            error = $"Option '{optionName}' requires a whole-number value.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadStringValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string? inlineValue,
        out string value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            value = inlineValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Count)
        {
            value = "";
            error = $"Option '{optionName}' requires a value.";
            return false;
        }

        index++;
        value = args[index];
        error = null;
        return true;
    }

    private static bool TryReadUShortValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string? inlineValue,
        out ushort value,
        out string? error)
    {
        if (!TryReadStringValue(args, ref index, optionName, inlineValue, out string rawValue, out error))
        {
            value = 0;
            return false;
        }

        if (!ushort.TryParse(rawValue, out value) || value > 1200)
        {
            error = $"Option '{optionName}' requires an integer value in the range 0 to 1200.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool Validate(SimulatorHostOptions options, out string? error)
    {
        if (!M1350ProtocolRevision.TryParse(options.ProtocolRevision.AsSpan(), out _))
        {
            error = "Protocol revision must be a 3-character ASCII token, for example A20.";
            return false;
        }

        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];
        if (!IdBlockEncoder.TryEncode(options.CreateIdentityBlock(), payload, out _))
        {
            error = "Identity fields must be fixed-width ASCII values: id-code=6, protocol-revision=3, software-revision=7, serial-number=10.";
            return false;
        }

        error = null;
        return true;
    }
}
