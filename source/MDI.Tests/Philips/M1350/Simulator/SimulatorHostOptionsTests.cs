using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Simulator;

namespace MDI.Tests.Philips.M1350.Simulator;

[TestClass]
public sealed class SimulatorHostOptionsTests
{
    [TestMethod]
    public void ParseShouldReturnDefaultsWhenNoArgumentsAreSupplied()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse([]);

        Assert.IsFalse(result.ShowUsage);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual("M1350A", result.Options.IdCode);
        Assert.AreEqual("A20", result.Options.ProtocolRevision);
        Assert.AreEqual(M1350CtgScenario.Baseline, result.Options.Scenario);
        Assert.AreEqual(TimeSpan.FromSeconds(1), result.Options.AutoSendInterval);
        Assert.IsTrue(result.Options.EmitPowerOnIdentity);
    }

    [TestMethod]
    public void ParseShouldSupportInlineAndSeparateOverrideValues()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(
        [
            "--id-code=M1351A",
            "--protocol-revision", "A10",
            "--software-revision=B.10.04",
            "--serial-number", "0000A12345",
            "--scenario", "fhr-rise",
            "--fhr1", "700",
            "--fhr2=680",
            "--mhr", "360",
            "--toco=30",
            "--fspo2", "99",
            "--auto-send-interval-ms", "250",
            "--no-power-on-identity",
        ]);

        Assert.IsFalse(result.ShowUsage);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(result.Options);
        Assert.AreEqual("M1351A", result.Options.IdCode);
        Assert.AreEqual("A10", result.Options.ProtocolRevision);
        Assert.AreEqual("B.10.04", result.Options.SoftwareRevision);
        Assert.AreEqual("0000A12345", result.Options.SerialNumber);
        Assert.AreEqual(M1350CtgScenario.FhrRise, result.Options.Scenario);
        Assert.AreEqual((ushort)700, result.Options.Fhr1RawValue);
        Assert.AreEqual((ushort)680, result.Options.Fhr2RawValue);
        Assert.AreEqual((ushort)360, result.Options.MhrRawValue);
        Assert.AreEqual((byte)30, result.Options.TocoRawValue);
        Assert.AreEqual((byte)99, result.Options.FSpO2);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), result.Options.AutoSendInterval);
        Assert.IsFalse(result.Options.EmitPowerOnIdentity);

        IdBlock identity = result.Options.CreateIdentityBlock();
        Assert.AreEqual("M1351A", identity.IdCode);
        Assert.AreEqual("A10", identity.ProtocolRevision);
    }

    [TestMethod]
    public void ParseShouldReturnHelpForHelpSwitch()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(["--help"]);

        Assert.IsTrue(result.ShowUsage);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNull(result.Options);
    }

    [TestMethod]
    public void ParseShouldRejectUnknownOption()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(["--bogus"]);

        Assert.IsTrue(result.ShowUsage);
        Assert.AreEqual("Unknown option '--bogus'.", result.ErrorMessage);
        Assert.IsNull(result.Options);
    }

    [TestMethod]
    public void ParseShouldRejectUnknownScenario()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(["--scenario", "bogus"]);

        Assert.IsTrue(result.ShowUsage);
        Assert.AreEqual(
            "Option '--scenario' must be one of: baseline, fhr-rise, fhr-drop, toco-rise.",
            result.ErrorMessage);
        Assert.IsNull(result.Options);
    }

    [TestMethod]
    public void ParseShouldRejectInvalidIdentityLayout()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(["--software-revision", "BAD"]);

        Assert.IsTrue(result.ShowUsage);
        Assert.AreEqual(
            "Identity fields must be fixed-width ASCII values: id-code=6, protocol-revision=3, software-revision=7, serial-number=10.",
            result.ErrorMessage);
        Assert.IsNull(result.Options);
    }

    [TestMethod]
    public void CreateCtgBlockShouldDisableFSpO2BeforeRevisionA20()
    {
        SimulatorHostParseResult result = SimulatorHostOptions.Parse(["--protocol-revision", "A10", "--fspo2", "99"]);

        Assert.IsNotNull(result.Options);

        CtgBlock ctg = result.Options.CreateCtgBlock();

        Assert.IsFalse(ctg.Status.IsFSpO2Available);
        Assert.AreEqual((byte)0, ctg.FSpO2);
    }
}
