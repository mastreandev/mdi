using System.IO.Pipelines;

using MDI.Philips.M1350.Replay;

namespace MDI.Philips.M1350.Simulator;

internal static class SimulatorHost
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        SimulatorHostParseResult parseResult = SimulatorHostOptions.Parse(args);

        if (parseResult.ShowUsage)
        {
            TextWriter writer = parseResult.ErrorMessage is null ? Console.Out : Console.Error;
            await WriteUsageAsync(writer, parseResult.ErrorMessage).ConfigureAwait(false);
            return parseResult.ErrorMessage is null ? 0 : 1;
        }

        using CancellationTokenSource stoppingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler cancelHandler = static (_, eventArgs) => eventArgs.Cancel = true;

        cancelHandler += (_, _) => stoppingSource.Cancel();
        Console.CancelKeyPress += cancelHandler;

        PipeReader? input = null;
        PipeWriter? output = null;

        try
        {
            output = PipeWriter.Create(Console.OpenStandardOutput());

            SimulatorHostOptions options = parseResult.Options!;

            if (options.ReplayPath is not null)
            {
                await RunReplayAsync(options.ReplayPath, output, stoppingSource.Token).ConfigureAwait(false);
                return 0;
            }

            input = PipeReader.Create(Console.OpenStandardInput());
            using M1350FmSimulator simulator = new(
                input,
                output,
                options.CreateIdentityBlock(),
                options.CreateCtgBlock(),
                options.Scenario,
                options.AutoSendInterval);

            if (options.EmitPowerOnIdentity)
            {
                await simulator.PowerOnAsync(stoppingSource.Token).ConfigureAwait(false);
            }

            await simulator.RunAsync(stoppingSource.Token).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (stoppingSource.IsCancellationRequested)
        {
            return 0;
        }
        catch (IOException ex)
        {
            await Console.Error.WriteLineAsync($"Simulator host failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;

            if (output is not null)
            {
                await output.CompleteAsync().ConfigureAwait(false);
            }

            if (input is not null)
            {
                await input.CompleteAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task RunReplayAsync(string replayPath, PipeWriter output, CancellationToken cancellationToken)
    {
        await using FileStream input = File.OpenRead(replayPath);
        M1350RecordedMessageReplay replay = await M1350MessageReplay.ReadNdjsonAsync(input, cancellationToken).ConfigureAwait(false);

        await RunReplayLoopAsync(replay, output, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task RunReplayLoopAsync(
        M1350RecordedMessageReplay replay,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ArgumentNullException.ThrowIfNull(output);

        while (true)
        {
            await foreach (M1350Message message in M1350MessageReplay.PlaybackAsync(replay, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                M1350FmWriter.WriteMessage(output, message);

                FlushResult flushResult = await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCanceled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }
    }

    private static async Task WriteUsageAsync(TextWriter writer, string? errorMessage)
    {
        if (errorMessage is not null)
        {
            await writer.WriteLineAsync(errorMessage).ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);
        }

        string[] lines =
        [
            "Philips M1350 simulator host",
            "",
            "Reads host commands from standard input and writes framed monitor output to standard output.",
            "",
            "Options:",
            "  --id-code <value>                 Default: M1350A",
            "  --protocol-revision <value>       Default: A20",
            "  --software-revision <value>       Default: A.03.00",
            "  --serial-number <value>           Default: 3019G10010",
            "  --scenario <value>                Default: baseline (baseline|fhr-rise|fhr-drop|toco-rise)",
            "  --fhr1 <0-1200>                   Default: 600",
            "  --fhr2 <0-1200>                   Default: 560",
            "  --mhr <0-1200>                    Default: 320",
            "  --toco <0-255>                    Default: 20",
            "  --fspo2 <0-255>                   Default: 98",
            "  --auto-send-interval-ms <value>   Default: 1000",
            "  --replay <path>                   Loop an NDJSON message stream to standard output.",
            "  --no-power-on-identity            Suppress the initial unsolicited identity block.",
            "  -h|--help|/?                      Show this usage text.",
        ];

        foreach (string line in lines)
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
