# Philips M1350 Replay Instrumentation

This document captures the observability side of Philips M1350 replay work.

It is intentionally separate from the replay file-format discussion in
[Replay Capture Shapes](replay-capture-shapes.md): instrumentation helps operators and developers
understand capture/replay behavior, but it is not itself the replay artifact.

## Short answer

OpenTelemetry can be useful around replay capture and playback, but it should not replace the
recorded replay data.

The durable artifact should remain one of:

1. message-layer replay entries
2. framed-block replay entries
3. raw-byte replay entries

OTEL, logging, and lower-level runtime diagnostics should sit beside those artifacts.

## What observability is good for

Replay instrumentation is useful for:

1. capture started/completed/failed events
2. replay started/completed/failed events
3. bytes, blocks, and messages recorded
4. replay lag or drift versus expected timing
5. parser failures or malformed-frame counts
6. long-running collector health and throughput

Replay instrumentation is not a substitute for exact captured data when the project needs:

1. faithful device playback
2. parser reprocessing of historical captures
3. byte-level forensic analysis

## Recommended tools by concern

### OpenTelemetry

Use OTEL for distributed or service-level observability around replay workflows.

Good uses:

1. tracing a capture session or replay session
2. surfacing counters for bytes/messages/blocks processed
3. recording latency or timing histograms
4. correlating replay activity with higher-level host workflows

Poor uses:

1. persisting replay content itself
2. reconstructing exact Philips traffic from traces alone
3. preserving malformed or unknown protocol content for future parser work

### ILogger

Use structured logging for operator-readable events and local diagnostics.

Good uses:

1. capture file opened/closed
2. replay file loaded
3. parser error summaries
4. warnings about dropped or unsupported content

### EventSource

Use `EventSource` only if low-overhead always-on runtime diagnostics become important.

That is more relevant if this library ends up inside services or tools where EventPipe or ETW-based
inspection matters. It is probably not needed for the first replay implementation.

## Recommended instrumentation shape

The clean split is:

1. replay files are the source-of-truth artifacts
2. OTEL and logs describe the act of creating or consuming those files

That means a recorder or player may emit instrumentation like:

1. replay format selected
2. capture duration
3. message or block count
4. byte count
5. parser failure count
6. playback drift or skipped-delay count

but the actual replay content still lives in the replay file.

## Example OTEL-friendly surfaces

If replay support is implemented, a small internal telemetry surface would likely be enough:

```csharp
internal static class M1350ReplayTelemetry
{
    public static readonly ActivitySource ActivitySource =
        new("MDI.Philips.M1350.Replay");

    public static readonly Meter Meter =
        new("MDI.Philips.M1350.Replay");

    public static readonly Counter<long> MessagesRecorded =
        Meter.CreateCounter<long>("mdi.m1350.replay.messages.recorded");

    public static readonly Counter<long> BlocksRecorded =
        Meter.CreateCounter<long>("mdi.m1350.replay.blocks.recorded");

    public static readonly Counter<long> BytesRecorded =
        Meter.CreateCounter<long>("mdi.m1350.replay.bytes.recorded");

    public static readonly Histogram<double> PlaybackDelayMs =
        Meter.CreateHistogram<double>("mdi.m1350.replay.playback.delay.ms");
}
```

The exact metric list should stay small until there is a demonstrated need for more.

## Instrumentation by replay layer

### Message-layer replay

Useful metrics:

1. message count by message type
2. average and max inter-message delay
3. replay drift from scheduled timing
4. dropped or unsupported message serialization failures

### Framed-block replay

Useful metrics:

1. framed block count
2. unknown type-byte count
3. decode success/failure rate when converting blocks into messages
4. average payload size

### Raw-byte capture

Useful metrics:

1. total bytes captured
2. chunk count
3. malformed-frame counts after offline decoding
4. CRC failure counts in analysis tools

## Recommendation

If replay work proceeds, add observability in this order:

1. structured logs around capture/replay lifecycle
2. a minimal `ActivitySource` and `Meter`
3. additional counters or histograms only when an actual diagnostic need appears
4. `EventSource` only if low-overhead production diagnostics become a real requirement

That keeps the replay implementation simple while still leaving a clean path to richer operational
visibility later.
