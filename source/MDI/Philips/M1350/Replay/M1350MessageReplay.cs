using System.Runtime.CompilerServices;
using System.Text.Json;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;

namespace MDI.Philips.M1350.Replay;

/// <summary>
/// Captures Philips M1350 message streams into replay metadata and entries.
/// </summary>
public static class M1350MessageReplay
{
    private const string ReplayFormat = "mdi.philips.m1350.message-replay";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Records a message stream to the supplied replay recorder.
    /// </summary>
    /// <param name="source">The message stream to capture.</param>
    /// <param name="recorder">The replay recorder that receives metadata and entries.</param>
    /// <param name="metadata">The metadata written before any entries.</param>
    /// <param name="timeProvider">
    /// The time provider used to measure monotonic inter-entry delays. When <see langword="null" />,
    /// <see cref="TimeProvider.System" /> is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for the capture operation.</param>
    public static async ValueTask RecordAsync(
        IAsyncEnumerable<M1350Message> source,
        IM1350MessageReplayRecorder recorder,
        M1350ReplayMetadata metadata,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(metadata);

        timeProvider ??= TimeProvider.System;

        await recorder.WriteHeaderAsync(metadata, cancellationToken).ConfigureAwait(false);

        bool isFirst = true;
        long previousTimestamp = timeProvider.GetTimestamp();

        await foreach (M1350Message message in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            long currentTimestamp = timeProvider.GetTimestamp();
            TimeSpan delay = isFirst
                ? TimeSpan.Zero
                : timeProvider.GetElapsedTime(previousTimestamp, currentTimestamp);

            await recorder.WriteEntryAsync(new M1350ReplayEntry(delay, message), cancellationToken).ConfigureAwait(false);

            previousTimestamp = currentTimestamp;
            isFirst = false;
        }
    }

    /// <summary>
    /// Creates a newline-delimited JSON replay recorder over the supplied stream.
    /// </summary>
    /// <param name="output">The replay output stream.</param>
    /// <param name="leaveOpen"><see langword="true" /> to leave the stream open when the recorder is disposed.</param>
    public static M1350NdjsonMessageReplayRecorder CreateNdjsonRecorder(Stream output, bool leaveOpen = false)
    {
        return new M1350NdjsonMessageReplayRecorder(output, leaveOpen);
    }

    /// <summary>
    /// Reads a newline-delimited JSON replay file.
    /// </summary>
    /// <param name="input">The replay input stream.</param>
    /// <param name="cancellationToken">The cancellation token for the read operation.</param>
    public static async ValueTask<M1350RecordedMessageReplay> ReadNdjsonAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using StreamReader reader = new(input, leaveOpen: true);
        string? headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidDataException("Replay input did not contain a header line.");
        }

        M1350ReplayMetadata metadata = ReadHeader(headerLine);
        List<M1350ReplayEntry> entries = [];

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            entries.Add(ReadEntry(line));
        }

        return new M1350RecordedMessageReplay(metadata, entries);
    }

    /// <summary>
    /// Materializes a replay source in memory.
    /// </summary>
    /// <param name="metadata">The replay header metadata.</param>
    /// <param name="entries">The replay entries in playback order.</param>
    /// <param name="cancellationToken">The cancellation token for enumeration.</param>
    public static async IAsyncEnumerable<M1350ReplayEntry> ReadAllAsync(
        M1350ReplayMetadata metadata,
        IEnumerable<M1350ReplayEntry> entries,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(entries);

        foreach (M1350ReplayEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Plays back a materialized replay as a timed stream of Philips M1350 messages.
    /// </summary>
    /// <param name="replay">The recorded replay to play back.</param>
    /// <param name="timeProvider">
    /// The time provider used to schedule inter-message delays. When <see langword="null" />,
    /// <see cref="TimeProvider.System" /> is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for playback.</param>
    public static IAsyncEnumerable<M1350Message> PlaybackAsync(
        M1350RecordedMessageReplay replay,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replay);

        return PlaybackAsync(replay.Entries, timeProvider, cancellationToken);
    }

    /// <summary>
    /// Plays back replay entries as a timed stream of Philips M1350 messages.
    /// </summary>
    /// <param name="entries">The replay entries in playback order.</param>
    /// <param name="timeProvider">
    /// The time provider used to schedule inter-message delays. When <see langword="null" />,
    /// <see cref="TimeProvider.System" /> is used.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for playback.</param>
    public static async IAsyncEnumerable<M1350Message> PlaybackAsync(
        IEnumerable<M1350ReplayEntry> entries,
        TimeProvider? timeProvider = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        timeProvider ??= TimeProvider.System;

        foreach (M1350ReplayEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.Delay > TimeSpan.Zero)
            {
                await Task.Delay(entry.Delay, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            yield return entry.Message;
        }
    }

    internal static string GetMessageType(M1350Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            CtgMessage => M1350ReplayMessageTypes.Ctg,
            IdMessage => M1350ReplayMessageTypes.Id,
            NibpMessage => M1350ReplayMessageTypes.Nibp,
            TemperatureMessage => M1350ReplayMessageTypes.Temperature,
            SpO2Message => M1350ReplayMessageTypes.SpO2,
            EventMarkerMessage => M1350ReplayMessageTypes.EventMarker,
            NoteMessage => M1350ReplayMessageTypes.Note,
            FailureMessage => M1350ReplayMessageTypes.Failure,
            _ => throw new NotSupportedException($"Replay serialization does not support message type '{message.GetType().Name}'."),
        };
    }

    private static M1350ReplayMetadata ReadHeader(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("kind", out JsonElement kindElement) || kindElement.GetString() != "header")
        {
            throw new InvalidDataException("Replay header line must have kind 'header'.");
        }

        string? format = root.GetProperty("format").GetString();
        int version = root.GetProperty("version").GetInt32();

        if (!string.Equals(format, ReplayFormat, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Replay format '{format}' is not supported.");
        }

        if (version != 1)
        {
            throw new InvalidDataException($"Replay version '{version}' is not supported.");
        }

        return new M1350ReplayMetadata(
            GetOptionalString(root, "negotiatedRevision"),
            GetOptionalDateTimeOffset(root, "capturedAt"),
            GetOptionalString(root, "deviceId"));
    }

    private static M1350ReplayEntry ReadEntry(string line)
    {
        using JsonDocument document = JsonDocument.Parse(line);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("kind", out JsonElement kindElement) || kindElement.GetString() != "event")
        {
            throw new InvalidDataException("Replay event line must have kind 'event'.");
        }

        long delayTicks = root.GetProperty("delayTicks").GetInt64();
        string messageType = root.GetProperty("messageType").GetString()
            ?? throw new InvalidDataException("Replay event line did not contain a message type.");

        JsonElement messageElement = root.GetProperty("message");
        M1350Message message = DeserializeMessage(messageType, messageElement);
        return new M1350ReplayEntry(TimeSpan.FromTicks(delayTicks), message);
    }

    private static M1350Message DeserializeMessage(string messageType, JsonElement messageElement)
    {
        M1350Message message = messageType switch
        {
            M1350ReplayMessageTypes.Ctg => new CtgMessage(DeserializeBlock<CtgBlock>(messageElement)),
            M1350ReplayMessageTypes.Id => new IdMessage(DeserializeBlock<IdBlock>(messageElement)),
            M1350ReplayMessageTypes.Nibp => new NibpMessage(DeserializeBlock<NibpBlock>(messageElement)),
            M1350ReplayMessageTypes.Temperature => new TemperatureMessage(DeserializeBlock<TemperatureBlock>(messageElement)),
            M1350ReplayMessageTypes.SpO2 => new SpO2Message(DeserializeBlock<SpO2Block>(messageElement)),
            M1350ReplayMessageTypes.EventMarker => new EventMarkerMessage(DeserializeBlock<EventMessageBlock>(messageElement)),
            M1350ReplayMessageTypes.Note => new NoteMessage(DeserializeBlock<NoteBlock>(messageElement)),
            M1350ReplayMessageTypes.Failure => new FailureMessage(DeserializeBlock<FailureBlock>(messageElement)),
            _ => throw new InvalidDataException($"Replay message type '{messageType}' is not supported."),
        };

        ValidateTypeByte(messageElement, message.TypeByte);

        return message with
        {
            Direction = GetOptionalDirection(messageElement) ?? message.Direction,
            ReceivedOffset = GetOptionalTimeSpan(messageElement, "receivedOffset"),
        };
    }

    private static TBlock DeserializeBlock<TBlock>(JsonElement messageElement)
    {
        if (!messageElement.TryGetProperty("block", out JsonElement blockElement))
        {
            throw new InvalidDataException("Replay message payload did not contain a 'block' property.");
        }

        return JsonSerializer.Deserialize<TBlock>(blockElement.GetRawText(), JsonOptions)
            ?? throw new InvalidDataException($"Replay message block could not be deserialized as '{typeof(TBlock).Name}'.");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.GetString();
    }

    private static DateTimeOffset? GetOptionalDateTimeOffset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return element.GetDateTimeOffset();
    }

    private static TimeSpan? GetOptionalTimeSpan(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TimeSpan>(element.GetRawText(), JsonOptions);
    }

    private static M1350MessageDirection? GetOptionalDirection(JsonElement root)
    {
        if (!root.TryGetProperty("direction", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return (M1350MessageDirection)element.GetInt32();
    }

    private static void ValidateTypeByte(JsonElement root, byte expectedTypeByte)
    {
        if (!root.TryGetProperty("typeByte", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        byte actualTypeByte = element.GetByte();

        if (actualTypeByte != expectedTypeByte)
        {
            throw new InvalidDataException(
                $"Replay message type byte '{actualTypeByte}' did not match expected type byte '{expectedTypeByte}'.");
        }
    }
}
