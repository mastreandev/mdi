using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Simulator;

namespace MDI.Tests.Philips.M1350.Simulator;

[TestClass]
public sealed class M1350FmSimulatorTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PowerOnAsyncShouldEmitUnsolicitedIdentityReadableBySession()
    {
        CancellationToken cancellationToken = this.TestContext.CancellationToken;
        Pipe hostToFm = new();
        Pipe fmToHost = new();
        using M1350FmSimulator simulator = new(
            hostToFm.Reader,
            fmToHost.Writer,
            CreateIdentityBlock("A20"),
            CreateCtgBlock(),
            autoSendInterval: TimeSpan.FromMilliseconds(20));

        await simulator.PowerOnAsync(cancellationToken);

        await using M1350Session session = new(fmToHost.Reader, new ArrayBufferWriter<byte>());
        await using IAsyncEnumerator<M1350Message> enumerator = session.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

        bool moved = await enumerator.MoveNextAsync();

        Assert.IsTrue(moved);
        Assert.IsInstanceOfType<IdMessage>(enumerator.Current);
        Assert.AreEqual("A20", ((IdMessage)enumerator.Current).Block.ProtocolRevision);
    }

    [TestMethod]
    public async Task RunAsyncShouldRespondToIdentityAndCtgRequestsThroughSession()
    {
        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.TestContext.CancellationToken);
        CancellationToken cancellationToken = cancellationSource.Token;
        Pipe hostToFm = new();
        Pipe fmToHost = new();
        IdBlock expectedIdentity = CreateIdentityBlock("A20");
        CtgBlock expectedCtg = CreateCtgBlock();
        using M1350FmSimulator simulator = new(
            hostToFm.Reader,
            fmToHost.Writer,
            expectedIdentity,
            expectedCtg,
            autoSendInterval: TimeSpan.FromMilliseconds(20));

        Task runTask = simulator.RunAsync(cancellationToken);

        try
        {
            await using M1350Session session = new(fmToHost.Reader, hostToFm.Writer);

            IdBlock identity = await session.RequestIdentityAsync(cancellationToken);
            CtgBlock ctg = await session.RequestCtgAsync(cancellationToken);

            Assert.AreEqual(expectedIdentity, identity);
            Assert.AreEqual(expectedCtg, ctg);
            Assert.IsFalse(simulator.IsAutoSendEnabled);
        }
        finally
        {
            await cancellationSource.CancelAsync();
            await runTask;
        }
    }

    [TestMethod]
    public async Task RunAsyncShouldSupportMonitorConnectAndAutoSend()
    {
        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.TestContext.CancellationToken);
        CancellationToken cancellationToken = cancellationSource.Token;
        Pipe hostToFm = new();
        Pipe fmToHost = new();
        CtgBlock expectedCtg = CreateCtgBlock();
        using M1350FmSimulator simulator = new(
            hostToFm.Reader,
            fmToHost.Writer,
            CreateIdentityBlock("A10"),
            expectedCtg,
            autoSendInterval: TimeSpan.FromMilliseconds(20));

        Task runTask = simulator.RunAsync(cancellationToken);

        try
        {
            await using M1350Session session = new(fmToHost.Reader, hostToFm.Writer);
            await using M1350Monitor monitor = new(session);

            Task<CtgUpdated> ctgUpdateTask = WaitForCtgUpdateAsync(monitor, cancellationToken);

            M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
                requestedRevision: "A20",
                autoSend: AutoSendBehavior.Enabled,
                cancellationToken: cancellationToken);

            CtgUpdated ctgUpdate = await ctgUpdateTask;

            Assert.AreEqual("A20", snapshot.Identity?.ProtocolRevision);
            Assert.AreEqual("A20", snapshot.NegotiatedRevision);
            Assert.IsTrue(snapshot.IsAutoSendActive);
            Assert.AreEqual(expectedCtg, ctgUpdate.Block);
            Assert.IsTrue(simulator.IsAutoSendEnabled);
        }
        finally
        {
            await cancellationSource.CancelAsync();
            await runTask;
        }
    }

    [TestMethod]
    public async Task RunAsyncShouldAdvanceCtgDataAcrossConsecutiveRequests()
    {
        using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(this.TestContext.CancellationToken);
        CancellationToken cancellationToken = cancellationSource.Token;
        Pipe hostToFm = new();
        Pipe fmToHost = new();
        CtgBlock initialCtg = CreateCtgBlock();
        using M1350FmSimulator simulator = new(
            hostToFm.Reader,
            fmToHost.Writer,
            CreateIdentityBlock("A20"),
            initialCtg,
            autoSendInterval: TimeSpan.FromMilliseconds(20));

        Task runTask = simulator.RunAsync(cancellationToken);

        try
        {
            await using M1350Session session = new(fmToHost.Reader, hostToFm.Writer);

            CtgBlock first = await session.RequestCtgAsync(cancellationToken);
            CtgBlock second = await session.RequestCtgAsync(cancellationToken);

            Assert.AreEqual(initialCtg, first);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(second.Status.IsMonitorOn);
            Assert.IsTrue(second.Status.IsTelemetryOn);
            Assert.AreEqual(first.Status.IsFSpO2Available, second.Status.IsFSpO2Available);
            Assert.AreNotEqual(first.Fhr1Sample3.RawValue, second.Fhr1Sample3.RawValue);
            Assert.AreNotEqual(first.TocoSample3, second.TocoSample3);
        }
        finally
        {
            await cancellationSource.CancelAsync();
            await runTask;
        }
    }

    private static CtgBlock CreateCtgBlock()
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

    private static IdBlock CreateIdentityBlock(string protocolRevision)
    {
        return new IdBlock(
            IdCode: "M1350A",
            ProtocolRevision: protocolRevision,
            SoftwareRevision: "A.03.00",
            SerialNumber: "3019G10010");
    }

    private static async Task<CtgUpdated> WaitForCtgUpdateAsync(M1350Monitor monitor, CancellationToken cancellationToken)
    {
        await foreach (M1350MonitorUpdate update in monitor.WatchAsync(cancellationToken))
        {
            if (update is CtgUpdated ctgUpdate)
            {
                return ctgUpdate;
            }
        }

        throw new AssertInconclusiveException("The simulator did not publish a CTG update before the monitor completed.");
    }
}
