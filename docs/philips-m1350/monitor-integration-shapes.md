# Philips M1350 Monitor Integration Shapes

This document focuses on a narrower question than the main monitor-layer sketch:

Once `M1350Monitor` exists, what useful integration shapes can consumers build on top of it without
forcing those choices into the core monitor API?

This is an important consumer-to-library seam, but it is not the only one. The point of this page
is to describe the first practical integration boundary that sits above `M1350Monitor`.

The design assumption here is the current recommendation from the monitor-layer sketch:

- `M1350Monitor` owns one background receive loop
- `M1350Monitor` exposes a retained `Snapshot`
- `M1350Monitor` exposes `WatchAsync()` as the primary update stream

```csharp
sealed class M1350Monitor : IAsyncDisposable
{
   M1350MonitorSnapshot Snapshot { get; }

   IAsyncEnumerable<M1350MonitorUpdate> WatchAsync(
      CancellationToken cancellationToken = default);
}
```

The question then becomes: what can consumers do with that shape?

## Core distinction: monitor-owned versus consumer-owned

The monitor layer should own:

1. protocol and session coordination
2. the single long-running receive loop
3. retained current state
4. publication of domain updates

Consumers may still want to own their own adaptation layer for:

1. buffering
2. fan-out
3. routing
4. throttling or coalescing
5. integration with worker pipelines or hosted services
6. UI binding or view-model shaping

That is the main reason to keep `Snapshot` plus `WatchAsync()` as the core API instead of making
channels, observers, or framework-specific callback shapes part of `M1350Monitor` itself.

## Snapshot versus update

An integration shape starts by recognizing that these are different things.

A snapshot answers:

- what is the monitor's latest known identity?
- what is the most recent CTG block?
- how old is the latest NIBP or SpO2 value?

An update answers:

- what changed just now?
- in what order did changes arrive?
- what should a consumer react to immediately?

So `Snapshot` is retained state, while `WatchAsync()` is a stream of state transitions.

## Integration shape A: direct snapshot reads

Some consumers do not need continuous push delivery. They just need the current picture.

Example:

```csharp
M1350MonitorSnapshot snapshot = monitor.Snapshot;

if (snapshot.Ctg is { } ctg)
{
   // render or inspect current CTG state
}
```

Good fit for:

- dashboards that repaint periodically
- status pages
- synchronous inspection code
- test assertions against current retained state

Tradeoff:

- this does not tell the consumer how many changes occurred in between reads

## Integration shape B: direct `WatchAsync()` consumption

Some consumers want to react to updates as they happen.

Example:

```csharp
await foreach (M1350MonitorUpdate update in monitor.WatchAsync(cancellationToken))
{
   switch (update)
   {
      case CtgUpdated ctg:
         // process new CTG update
         break;
      case FailureUpdated failure:
         // react to failure immediately
         break;
   }
}
```

Good fit for:

- hosted services
- logging or telemetry pipelines
- persistence pipelines
- alert or alarm-handling flows

Tradeoff:

- the consumer has to decide whether it also wants to inspect retained `Snapshot`

## Integration shape C: consumer-owned channels

This is the main extension point for callers who want something more pipeline-oriented.

A consumer can adapt `WatchAsync()` into its own `Channel<T>` without requiring `M1350Monitor`
itself to expose channels.

The current library includes one thin convenience helper for this shape:

```csharp
Task copyTask = monitor.CopyUpdatesToAsync(channel.Writer, cancellationToken);
```

That helper is intentionally narrow. The consumer still chooses:

- whether the channel is bounded or unbounded
- the capacity and backpressure policy
- who reads from the channel and how downstream work is coordinated

Example:

```csharp
Channel<M1350MonitorUpdate> channel = Channel.CreateBounded<M1350MonitorUpdate>(64);

Task producer = monitor.CopyUpdatesToAsync(channel.Writer, cancellationToken);
```

Good fit for:

- explicit buffering
- bridging to worker pipelines
- separating producer and consumer pace
- custom backpressure policies
- integration with apps that already standardize on channels

Interesting things a consumer can do with a channel:

1. send all updates through one bounded queue to a background processor
2. route only `FailureUpdated` and `NoteUpdated` into a higher-priority channel
3. coalesce CTG updates before forwarding them to a slower subsystem
4. feed multiple downstream workers from a consumer-controlled fan-out stage

Why this should stay consumer-owned in the first pass:

1. the monitor API should not need to pick bounded versus unbounded behavior
2. the monitor API should not need to own channel completion semantics for every caller
3. channel-based backpressure policy is an application concern more than a device-domain concern

## Integration shape D: consumer-owned projection types

Consumers may want to project the monitor stream into their own domain shape.

Example:

```csharp
sealed record LaborWardDisplayState(
   string? BedLabel,
   CtgBlock? Ctg,
   FailureBlock? Failure);
```

Then the consumer can map `Snapshot` and `WatchAsync()` into that local type.

Good fit for:

- UI layer adaptation
- downstream domain modeling
- integration boundaries between libraries and app code

This is preferable to pushing application-specific state classes into the core MDI library.

## Integration shape E: consumer-owned freshness policy

If a consumer cares about staleness, it can usually build that policy from timestamps already held
in `Snapshot`.

Example:

```csharp
M1350MonitorSnapshot snapshot = monitor.Snapshot;

bool isCtgFresh = snapshot.CtgReceivedAt is { } timestamp
    && DateTimeOffset.UtcNow - timestamp <= TimeSpan.FromSeconds(2);
```

This is one reason not to overdesign freshness helpers too early. The core library can expose the
time facts, while applications decide what "fresh enough" means.

## Why consumer-owned channels fit Model C well

Model C is strong precisely because it does not lock consumers into one infrastructure primitive.

`WatchAsync()` is a good domain-level contract because consumers can project it into:

- a direct `await foreach` loop
- one or more `Channel<T>` pipelines
- their own observer or event adapters
- framework-specific message buses

That is a better default than making `ChannelReader<T>` part of `M1350Monitor` itself.

The shipped helper is intentionally limited to copying updates into a caller-provided writer. The
monitor surface itself still stays centered on domain state and domain updates rather than owning
channel allocation or buffering policy.

## Recommended first-pass integration story

The recommended first-pass story is:

1. `M1350Monitor.Snapshot` for retained current state
2. `M1350Monitor.WatchAsync()` for ordered domain updates
3. consumer-owned adaptation layers for channels, view models, buffering, and routing

That keeps the core library clinically and semantically focused while still giving consumers room
to build more specialized orchestration around it.
