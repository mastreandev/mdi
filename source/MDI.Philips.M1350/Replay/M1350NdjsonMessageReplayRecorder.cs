using System.Text;
using System.Text.Json;

namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Writes Philips M1350 message replays as newline-delimited JSON.
/// </summary>
public sealed class M1350NdjsonMessageReplayRecorder : IM1350MessageReplayRecorder, IAsyncDisposable, IDisposable
{
    private readonly bool leaveOpen;
    private readonly StreamWriter writer;
    private bool headerWritten;

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350NdjsonMessageReplayRecorder" /> class.
    /// </summary>
    /// <param name="output">The destination stream.</param>
    /// <param name="leaveOpen"><see langword="true" /> to leave the stream open when disposed.</param>
    public M1350NdjsonMessageReplayRecorder(Stream output, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(output);

        this.leaveOpen = leaveOpen;
        this.writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: leaveOpen);
    }

    /// <inheritdoc />
    public async ValueTask WriteHeaderAsync(M1350ReplayMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();

        if (this.headerWritten)
        {
            throw new InvalidOperationException("Replay metadata has already been written.");
        }

        HeaderLine line = new(
            Kind: "header",
            Format: "mdi.philips.m1350.message-replay",
            Version: 1,
            NegotiatedRevision: metadata.NegotiatedRevision,
            CapturedAt: metadata.CapturedAt,
            DeviceId: metadata.DeviceId);

        await this.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
        this.headerWritten = true;
    }

    /// <inheritdoc />
    public async ValueTask WriteEntryAsync(M1350ReplayEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (!this.headerWritten)
        {
            throw new InvalidOperationException("Replay metadata must be written before replay entries.");
        }

        EventLine line = new(
            Kind: "event",
            DelayTicks: entry.Delay.Ticks,
            MessageType: M1350MessageReplay.GetMessageType(entry.Message),
            Message: CreateMessagePayload(entry.Message));

        await this.WriteLineAsync(line, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.writer.Dispose();

        if (this.leaveOpen)
        {
            GC.SuppressFinalize(this);
            return;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.writer.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private async ValueTask WriteLineAsync<TValue>(TValue value, CancellationToken cancellationToken)
    {
        string line = JsonSerializer.Serialize(value, JsonOptions);
        await this.writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
        await this.writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record HeaderLine(
        string Kind,
        string Format,
        int Version,
        string? NegotiatedRevision,
        DateTimeOffset? CapturedAt,
        string? DeviceId);

    private sealed record EventLine(
        string Kind,
        long DelayTicks,
        string MessageType,
        ReplayMessagePayload Message);

    private static ReplayMessagePayload CreateMessagePayload(M1350Message message)
    {
        return message switch
        {
            CtgMessage ctg => new ReplayMessagePayload(ctg.TypeByte, ctg.Direction, ctg.ReceivedOffset, ctg.Block),
            IdMessage id => new ReplayMessagePayload(id.TypeByte, id.Direction, id.ReceivedOffset, id.Block),
            NibpMessage nibp => new ReplayMessagePayload(nibp.TypeByte, nibp.Direction, nibp.ReceivedOffset, nibp.Block),
            TemperatureMessage temperature => new ReplayMessagePayload(temperature.TypeByte, temperature.Direction, temperature.ReceivedOffset, temperature.Block),
            SpO2Message spo2 => new ReplayMessagePayload(spo2.TypeByte, spo2.Direction, spo2.ReceivedOffset, spo2.Block),
            EventMarkerMessage eventMarker => new ReplayMessagePayload(eventMarker.TypeByte, eventMarker.Direction, eventMarker.ReceivedOffset, eventMarker.Block),
            NoteMessage note => new ReplayMessagePayload(note.TypeByte, note.Direction, note.ReceivedOffset, note.Block),
            FailureMessage failure => new ReplayMessagePayload(failure.TypeByte, failure.Direction, failure.ReceivedOffset, failure.Block),
            _ => throw new NotSupportedException($"Replay serialization does not support message type '{message.GetType().Name}'."),
        };
    }

    private sealed record ReplayMessagePayload(
        byte TypeByte,
        M1350MessageDirection Direction,
        TimeSpan? ReceivedOffset,
        object Block);
}
