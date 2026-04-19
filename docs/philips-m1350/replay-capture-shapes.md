# Philips M1350 Replay Capture Shapes

This document sketches three plausible record/replay boundaries for Philips M1350 traffic:

1. message-layer capture
2. framed-block capture
3. raw-byte capture

They are all useful, but they serve different audiences and different failure modes.

## Layer map

The current implementation stack already separates these concerns:

```text
Raw bytes           serial stream / stream reads / chunking / timing
Framed blocks       DataBlockReader / DataBlockWriter
Application blocks  CtgBlockParser, IdBlockParser, etc.
Session messages    M1350Session.ReadAllAsync() -> M1350Message
Monitor state       M1350Monitor snapshot and update stream
```

The replay question is not only "what should we record?" but also "what problem are we trying to solve?"

## Short answer

If the goal is compact reusable playback for simulator and host-side behavior testing, message-layer
capture is the best default.

If the goal is protocol debugging, parser validation, or exact wire fidelity, lower-level capture is
better. That lower-level space still splits into two distinct forms:

1. framed-block capture for protocol-faithful but parser-independent replay
2. raw-byte capture for exact transport-faithful replay

## Compare at a glance

| Capture boundary | What gets recorded | Best audience | Best problems | Main loss |
| --- | --- | --- | --- | --- |
| Message layer | typed `M1350Message` values plus timing deltas | simulator, host-integration, app developers | behavior replay, compact fixtures, regression coverage | wire fidelity, unknown blocks, malformed traffic |
| Framed blocks | validated Philips blocks after framing/escaping/CRC | protocol developers, parser work | parser reprocessing, unknown-block retention, application-layer validation | raw transport chunking and exact byte stream |
| Raw bytes | exact byte stream as observed on the transport | transport/debugging work, forensics | framing bugs, CRC issues, serial-port behavior, exact fidelity | compactness, readability, ease of reuse |

## API shape guidance

These three replay boundaries should not be forced into one symmetric public API.

They represent different products:

1. message replay is application-facing
2. framed-block replay is protocol-facing
3. raw-byte capture is transport-facing

That means each layer should expose the .NET surface that best matches its consumers.

## Option 1: Message-layer capture

This records the ordered typed stream already exposed by `M1350Session.ReadAllAsync(...)`.

Example shape:

```text
session metadata
- negotiated revision
- optional wall-clock capture timestamp for diagnostics or operator context
- monotonic timing origin used to compute inter-entry delays
- optional monitor identity metadata

entries
- delta: 1000 ms, message: IdMessage(...)
- delta: 250 ms,  message: CtgMessage(...)
- delta: 1000 ms, message: FailureMessage(...)
```

### Strengths

1. Smallest and easiest format for normal replay work.
2. Best match for the simulator and session APIs that already exist.
3. Straightforward to diff, inspect, and version in tests.
4. Naturally aligned with host-facing behavior: callers usually care about typed messages, not DLE escaping.
5. Avoids tying replay artifacts to one specific transport or buffering pattern.

### Weaknesses

1. Unknown inbound block types are intentionally dropped at the session layer, so they cannot be preserved.
2. Malformed frames, CRC failures, and framing edge cases are lost.
3. Replay reflects the parser's interpretation of traffic, not the original bytes.
4. It is weaker for protocol forensics and parser-regression triage.

### Best fit

Use this when the audience is primarily:

1. simulator development
2. host-side application developers
3. monitor integration testing
4. regression suites that need compact representative traces

### Planning implication

This should be the primary replay format if the project wants one durable, broadly useful capture format.

### Suggested .NET API shape

Message-layer replay should look like a session- or simulator-facing API built around typed entries.

```csharp
public sealed record M1350ReplayMetadata(
	string? NegotiatedRevision,
	DateTimeOffset CapturedAt,
	string? DeviceId);

public sealed record M1350ReplayEntry(
	TimeSpan Delay,
	M1350Message Message);

public interface IM1350MessageReplayRecorder
{
	ValueTask WriteHeaderAsync(
		M1350ReplayMetadata metadata,
		CancellationToken cancellationToken = default);

	ValueTask WriteEntryAsync(
		M1350ReplayEntry entry,
		CancellationToken cancellationToken = default);
}

public static class M1350MessageReplay
{
	public static M1350NdjsonMessageReplayRecorder CreateNdjsonRecorder(
		Stream output,
		bool leaveOpen = false);

	public static ValueTask RecordAsync(
		IAsyncEnumerable<M1350Message> source,
		IM1350MessageReplayRecorder recorder,
		M1350ReplayMetadata metadata,
		TimeProvider? timeProvider = null,
		CancellationToken cancellationToken = default);

	public static ValueTask<M1350RecordedMessageReplay> ReadNdjsonAsync(
		Stream input,
		CancellationToken cancellationToken = default);

	public static IAsyncEnumerable<M1350ReplayEntry> ReadAllAsync(
		M1350ReplayMetadata metadata,
		IEnumerable<M1350ReplayEntry> entries,
		CancellationToken cancellationToken = default);
}
```

### Simple durable flat-file shape

The first durable format should stay deliberately simple: newline-delimited JSON with one header line,
then one event line per replay entry.

Example:

```json
{"kind":"header","format":"mdi.philips.m1350.message-replay","version":1,"negotiatedRevision":"A20","capturedAt":"2026-04-19T12:00:00+00:00","deviceId":"M1350A"}
{"kind":"event","delayTicks":0,"messageType":"id","message":{"block":{"idCode":"M1350A","protocolRevision":"A20","softwareRevision":"A.03.00","serialNumber":"SN12345678"}}}
{"kind":"event","delayTicks":2500000,"messageType":"note","message":{"block":{"userId":"NURSE","text":"hello"}}}
```

That keeps the file:

1. append-friendly and diffable
2. easy to inspect without custom tools
3. explicit about replay version and message type
4. one line per replay event after the header

### Helpful .NET idioms

1. `IAsyncEnumerable<T>` matches `M1350Session.ReadAllAsync(...)` naturally.
2. `TimeProvider` makes capture and playback timing testable.
3. small immutable records fit replay metadata and entries well.
4. `Stream` is a good persistence boundary here because this layer is not transport-implementation specific.

### Timing note

Replay delays should be measured from a monotonic source, not from wall-clock time.

That means:

1. `Delay` values should come from elapsed monotonic time between entries
2. wall-clock timestamps such as `CapturedAt` are optional metadata for diagnostics or operator context
3. wall-clock timestamps should not be used to derive replay timing
4. `TimeProvider` is still a good implementation abstraction because it provides monotonic timestamp access, elapsed-time helpers, and testable delay behavior in one place

## Option 2: Framed-block capture

This records complete Philips data-link blocks after framing has already succeeded, but before those
blocks are parsed into `M1350Message` values.

That means the capture keeps block boundaries and payloads, but does not try to preserve the original
stream chunking or read-buffer behavior.

Example shape:

```text
session metadata
- transport direction if needed
- monotonic timing origin

entries
- delta: 1000 ms, block payload: 49 2D 41 30 31 ...
- delta: 250 ms,  block payload: 43 00 51 ...
```

### Strengths

1. More faithful than message-layer capture.
2. Retains unknown block types as long as data-link framing succeeded.
3. Lets future parsers reinterpret old captures without needing the original serial stream.
4. Good fit for validating application-layer parsers independent of transport chunking noise.
5. Keeps the replay model tied to Philips protocol objects rather than raw serial behavior.

### Weaknesses

1. Larger and less readable than message-layer capture.
2. Still loses exact read boundaries and byte-level transport behavior.
3. Requires a separate decode path to turn stored payloads back into typed messages.
4. Less convenient than message-layer capture for normal simulator playback.

### Best fit

Use this when the audience is primarily:

1. parser and protocol implementers
2. developers diagnosing unsupported or newly discovered block types
3. tooling that wants long-lived protocol-faithful fixtures without serial-port noise

### Planning implication

This is the strongest lower-level compromise if the project wants more authority than typed-message
capture but does not want to commit to exact transport replay.

### Suggested .NET API shape

Framed-block replay should stay protocol-facing rather than exposing `M1350Message` directly.

```csharp
public sealed record M1350FramedBlock(
	TimeSpan Delay,
	ReadOnlyMemory<byte> Payload);

public static class M1350BlockReplay
{
	public static ValueTask RecordAsync(
		PipeReader input,
		Stream output,
		CancellationToken cancellationToken = default);

	public static IAsyncEnumerable<M1350FramedBlock> ReadAllAsync(
		Stream input,
		CancellationToken cancellationToken = default);
}

public static class M1350BlockReplayDecoder
{
	public static bool TryDecode(
		in M1350FramedBlock block,
		out M1350Message message);
}
```

### Helpful .NET idioms

1. `PipeReader` is the right recording boundary because parsing already operates on `ReadOnlySequence<byte>`.
2. `ReadOnlyMemory<byte>` is a better public payload surface than mutable `byte[]`.
3. adapters are better than inheritance here: block replay can be decoded upward into messages later.
4. `ArrayPool<byte>` is useful in the implementation for copied payload storage.

## Option 3: Raw-byte capture

This records the exact byte stream as seen on the transport, in arrival order.

Depending on design, it may also record:

1. monotonic arrival timing or inter-byte timing
2. direction markers if duplex traffic is captured
3. read-chunk boundaries if transport behavior matters for debugging

### Strengths

1. Highest-fidelity source of truth.
2. Preserves malformed frames, CRC failures, escaping behavior, and unexpected transport noise.
3. Preserves bytes for block types not yet understood by the application parser.
4. Ideal for transport debugging and forensic investigation.
5. Allows parser improvements to be tested against old captures with no information loss.

### Weaknesses

1. Noisiest and hardest format to work with day to day.
2. Harder to inspect and much less pleasant as a reusable test fixture format.
3. Replay semantics are less obvious: exact byte timing, original chunking, or just ordered bytes?
4. Less aligned with the existing simulator and session APIs.
5. Easy to overfit to one transport implementation's behavior.

### Best fit

Use this when the audience is primarily:

1. transport and framing implementers
2. developers investigating parser bugs or wire anomalies
3. forensic/debugging tools

### Planning implication

This should not be the default replay format for normal simulator work, but it is the most authoritative
diagnostic capture if exact wire truth matters.

### Suggested .NET API shape

Raw-byte capture should look like transport tooling rather than business-level replay.

```csharp
public enum M1350CaptureDirection
{
	Inbound,
	Outbound,
}

public sealed record M1350RawByteChunk(
	TimeSpan Delay,
	M1350CaptureDirection Direction,
	ReadOnlyMemory<byte> Bytes);

public static class M1350RawCapture
{
	public static ValueTask CopyToCaptureAsync(
		Stream input,
		Stream captureOutput,
		CancellationToken cancellationToken = default);

	public static IAsyncEnumerable<M1350RawByteChunk> ReadAllAsync(
		Stream input,
		CancellationToken cancellationToken = default);
}
```

### Helpful .NET idioms

1. keep `Stream` as the convenience capture boundary for raw bytes
2. use pooled buffers internally to avoid repeated allocation pressure
3. keep direction explicit if duplex capture is required
4. avoid promising exact read-chunk replay unless the format intentionally records chunking semantics

## Audience and problem mapping

### Simulator and host behavior work

Primary need:

1. realistic ordered device behavior
2. compact fixtures
3. stable replay that is easy to author and review

Best choice:

1. message-layer capture

### Protocol and parser work

Primary need:

1. preserve unsupported blocks
2. replay known frames through improved parsers later
3. separate application parsing from transport artifacts

Best choice:

1. framed-block capture

### Transport and wire debugging

Primary need:

1. preserve exact serial bytes
2. investigate framing and CRC failures
3. understand transport-level corruption or chunking behavior

Best choice:

1. raw-byte capture

## Recommended direction

For this repository, the most pragmatic plan is:

1. make message-layer capture the primary replay/export format
2. keep framed-block capture as the best protocol-faithful fallback if lower-level authority is needed
3. reserve raw-byte capture for dedicated diagnostics and wire investigation

That gives the project one default format optimized for the simulator and monitor library, while still
leaving room for more authoritative lower-level tooling if future debugging needs justify it.

For observability around these capture and replay flows, see [Replay Instrumentation](replay-instrumentation.md).

## Why not monitor snapshots

`M1350Monitor` snapshots are intentionally above all three of these boundaries.

They are valuable for current-state observation, but they are a poor record/replay format because they:

1. collapse repeated values
2. overwrite transition history
3. lose the ordered inbound event stream needed for faithful replay

That makes monitor snapshots useful as derived views, not as the primary capture artifact.

## Suggested implementation order

If replay work starts soon, the cleanest order is:

1. define a compact message-layer replay format and recorder
2. build simulator playback against that format
3. add framed-block capture only if parser or unsupported-block needs appear
4. add raw-byte diagnostics only if exact wire investigations become a recurring problem

## Cross-cutting implementation notes

Across all three layers, the most useful modern .NET implementation patterns are:

1. `PipeReader` and `PipeWriter` at transport and protocol boundaries
2. `IAsyncEnumerable<T>` for ordered replay consumption
3. `TimeProvider` for deterministic timing and testability
4. immutable record entry types for persisted replay facts
5. `ArrayPool<byte>` for lower-level implementations that copy payloads or chunks

Across all three layers, replay timing should be based on monotonic elapsed time. If wall-clock
timestamps are recorded, they should be treated as metadata rather than as the source of playback
delay calculations.

OpenTelemetry can be useful around capture and playback as tracing and metrics, but it should not be
treated as the replay persistence mechanism. The replay file remains the durable artifact.
