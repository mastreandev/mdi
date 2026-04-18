using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350.Application;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350.Simulator;

/// <summary>
/// Simulates a Philips M1350 fetal monitor over duplex pipes.
/// </summary>
public sealed class M1350FmSimulator : IDisposable
{
    private readonly Lock gate = new();
    private readonly M1350CtgGenerator ctgGenerator;
    private readonly PipeReader input;
    private readonly PipeWriter output;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly TimeSpan autoSendInterval;

    private bool IsAutoSendEnabledState { get; set; }

    private IdBlock IdentityState { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350FmSimulator" /> class.
    /// </summary>
    public M1350FmSimulator(
        PipeReader input,
        PipeWriter output,
        IdBlock identity,
        CtgBlock ctg,
        M1350CtgScenario scenario = M1350CtgScenario.Baseline,
        TimeSpan? autoSendInterval = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        this.input = input;
        this.output = output;
        this.IdentityState = identity;
        this.ctgGenerator = new M1350CtgGenerator(ctg, scenario);
        this.autoSendInterval = autoSendInterval ?? TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Gets the current simulated identity block.
    /// </summary>
    public IdBlock Identity
    {
        get
        {
            lock (this.gate)
            {
                return this.IdentityState;
            }
        }
    }

    /// <summary>
    /// Gets the current simulated CTG block.
    /// </summary>
    public CtgBlock Ctg => this.ctgGenerator.CurrentBlock;

    /// <summary>
    /// Gets a value indicating whether auto-send mode is currently enabled.
    /// </summary>
    public bool IsAutoSendEnabled
    {
        get
        {
            lock (this.gate)
            {
                return this.IsAutoSendEnabledState;
            }
        }
    }

    /// <summary>
    /// Emits the unsolicited power-on identity block.
    /// </summary>
    public ValueTask PowerOnAsync(CancellationToken cancellationToken = default)
    {
        return this.WriteIdentityAsync(this.Identity, cancellationToken);
    }

    /// <summary>
    /// Runs the simulator command loop until cancellation or input completion.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource autoSendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task autoSendTask = this.RunAutoSendLoopAsync(autoSendCancellation.Token);

        try
        {
            while (await this.TryReadPayloadAsync(cancellationToken).ConfigureAwait(false) is { } payload)
            {
                await this.HandlePayloadAsync(payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await autoSendCancellation.CancelAsync().ConfigureAwait(false);

            try
            {
                await autoSendTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (autoSendCancellation.IsCancellationRequested)
            {
            }
        }
    }

    /// <summary>
    /// Disposes resources owned by the simulator instance.
    /// </summary>
    public void Dispose()
    {
        this.writeLock.Dispose();
    }

    private static bool TryReadRequestedRevision(ReadOnlySpan<byte> payload, out string revision)
    {
        if (payload.Length == ProtocolRevisionChangeRequestEncoder.EncodedLength
            && payload[0] == ProtocolRevisionChangeRequestEncoder.TypeByte)
        {
            revision = string.Create(
                3,
                payload[1..4],
                static (span, bytes) =>
                {
                    span[0] = (char)bytes[0];
                    span[1] = (char)bytes[1];
                    span[2] = (char)bytes[2];
                });

            return ProtocolRevision.TryParse(revision.AsSpan(), out _);
        }

        revision = "";
        return false;
    }

    private async Task HandlePayloadAsync(ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        int length = checked((int)payload.Length);
        byte[] payloadBytes = new byte[length];
        payload.CopyTo(payloadBytes);

        if (length == 0)
        {
            return;
        }

        switch (payloadBytes[0])
        {
            case RequestBlockEncoder.TypeByte when length == RequestBlockEncoder.EncodedLength:
                await this.HandleRequestAsync(payloadBytes[1], cancellationToken).ConfigureAwait(false);
                break;

            case GoAutoSendCommandEncoder.TypeByte when length == GoAutoSendCommandEncoder.EncodedLength:
                this.SetAutoSendEnabled(true);
                break;

            case HaltAutoSendCommandEncoder.TypeByte when length == HaltAutoSendCommandEncoder.EncodedLength:
                this.SetAutoSendEnabled(false);
                break;

            case ProtocolRevisionChangeRequestEncoder.TypeByte when TryReadRequestedRevision(payloadBytes, out string revision):
                this.ApplyRequestedRevision(revision);
                break;

            default:
                break;
        }
    }

    private async Task HandleRequestAsync(byte requestedType, CancellationToken cancellationToken)
    {
        switch (requestedType)
        {
            case IdBlockParser.TypeByte:
                await this.WriteIdentityAsync(this.Identity, cancellationToken).ConfigureAwait(false);
                break;

            case CtgBlockParser.TypeByte:
                this.SetAutoSendEnabled(false);
                await this.WriteCtgAsync(this.ctgGenerator.NextBlock(), cancellationToken).ConfigureAwait(false);
                break;

            default:
                break;
        }
    }

    private void ApplyRequestedRevision(string revision)
    {
        lock (this.gate)
        {
            this.IdentityState = this.IdentityState with { ProtocolRevision = revision };
        }
    }

    private void SetAutoSendEnabled(bool isEnabled)
    {
        lock (this.gate)
        {
            this.IsAutoSendEnabledState = isEnabled;
        }
    }

    private async Task RunAutoSendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(this.autoSendInterval, cancellationToken).ConfigureAwait(false);

            if (!this.IsAutoSendEnabled)
            {
                continue;
            }

            await this.WriteCtgAsync(this.ctgGenerator.NextBlock(), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<ReadOnlySequence<byte>?> TryReadPayloadAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await this.input.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;
            ReadOnlySequence<byte> remainder = buffer;

            if (DataLink.DataBlockReader.TryRead(ref remainder, out ReadOnlySequence<byte> payload))
            {
                this.input.AdvanceTo(remainder.Start, remainder.Start);
                return payload;
            }

            if (result.IsCompleted)
            {
                this.input.AdvanceTo(buffer.End, buffer.End);
                return null;
            }

            this.input.AdvanceTo(remainder.Start, buffer.End);
        }
    }

    private async ValueTask WriteIdentityAsync(IdBlock block, CancellationToken cancellationToken)
    {
        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            M1350FmWriter.WriteIdentity(this.output, block);
            await this.output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    private async ValueTask WriteCtgAsync(CtgBlock block, CancellationToken cancellationToken)
    {
        await this.writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            M1350FmWriter.WriteCtg(this.output, block);
            await this.output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.writeLock.Release();
        }
    }
}
