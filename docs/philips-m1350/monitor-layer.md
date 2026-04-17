# Philips M1350 Monitor Layer

This document describes the first implemented monitor layer above `M1350Session` and the
remaining stabilization work on that API.

For concrete integration shapes built on top of `M1350Monitor`, including channel-based
adaptation, see [Monitor Integration Shapes](monitor-integration-shapes.md).

The purpose of this layer is not to replace the current session API. It is to own the
longer-running monitor workflows that would otherwise force every caller to reimplement the same
startup, background-read, and latest-state coordination logic.

`M1350Session` stays transport-facing and relatively thin. `M1350Monitor` is the
clinical-domain-oriented facade that coordinates the session over time.

## Why a monitor layer exists

The current session layer is already a good fit for request/response and transport-facing flows:

- send `?I`, `?C`, `G`, `H`, `N`, and `Vxxx`
- wait for specific returned block types
- stream inbound messages via `ReadAllAsync(...)`
- enforce the current single-reader rule on a session instance

What it does not own yet is the longer-lived monitor lifecycle:

- startup sequencing
- continuous background consumption of incoming messages
- maintaining the latest known device state
- surfacing typed updates to application code
- encapsulating the "one async reader per session" constraint so callers do not manage it directly

That is the point where a separate monitor abstraction becomes justified.

## Position in the stack

```
Physical layer      RS232 / RS422 serial port
Data link layer     DataBlockReader / DataBlockWriter
Application layer   CtgBlockParser, IdBlockParser, etc.
Session layer       M1350Session
Monitor layer       M1350Monitor
```

## Responsibility split

`M1350Session` should continue to own:

1. Framed command writing
2. Typed block selection from framed input
3. Async request/workflow methods such as identity, CTG, and revision negotiation
4. Raw message streaming via `ReadAllAsync(...)`
5. Transport boundaries (`PipeReader`, `IDuplexPipe`, `Stream` factories)

`M1350Monitor` should own:

1. Connect/startup policy
2. Long-running background receive loop
3. Latest-known monitor state
4. Async-friendly update publication for incoming messages
5. Coordinated start/stop behavior for auto-send mode
6. Cancellation and shutdown behavior at the monitor-workflow level

## Current shape

The first public shape is now implemented.

```csharp
sealed class M1350Monitor : IAsyncDisposable
{
   M1350Monitor(M1350Session session);

   M1350MonitorSnapshot Snapshot { get; }

   ValueTask<M1350MonitorSnapshot> ConnectAsync(
      string? requestedRevision = null,
      AutoSendBehavior autoSend = AutoSendBehavior.Enabled,
      CancellationToken cancellationToken = default);

   ValueTask StartAsync(CancellationToken cancellationToken = default);
   ValueTask StopAsync(CancellationToken cancellationToken = default);

   IAsyncEnumerable<M1350MonitorUpdate> WatchAsync(
      CancellationToken cancellationToken = default);
}
```

There are three deliberate choices in this design:

1. The monitor composes `M1350Session` rather than inheriting from or replacing it.
2. The monitor surface is stateful and long-lived, while the session surface remains transport- and
   workflow-oriented.
3. The primary read model is a latest-state snapshot, with an async update stream layered on top.

## Why not plain .NET events

Plain `event EventHandler<T>` is still a normal .NET API shape, but it is not a great primary fit
for this monitor abstraction.

The problem is not whether events exist in .NET 10. They do. The problem is that they are a weak
coordination surface for async device workflows:

- `EventHandler<T>` is synchronous by shape
- async handlers tend to become `async void`, which makes error propagation and ordering awkward
- events do not naturally model backpressure
- events make it harder to reason about shutdown and cancellation of a long-running receive loop

For this API, an immutable snapshot plus an async update stream is the cleaner default. It matches
the existing session-layer choice to expose `ReadAllAsync(...)` as `IAsyncEnumerable<T>` rather
than trying to push everything through callbacks.

This does not rule out events forever. It just means they should be optional convenience, not the
foundation of the monitor abstraction.

## Startup workflow

The current startup flow is:

1. Create or receive an `M1350Session`
2. Request identity
3. Optionally negotiate protocol revision
4. Optionally request identity again if the negotiation flow requires confirmation
5. Optionally enter auto-send mode
6. Start the background receive loop
7. Publish initial snapshot/state

That keeps startup policy in one place instead of distributing it across each caller.

The current implementation treats `ConnectAsync(...)` as the normal monitor entry point.
It performs the startup workflow and then starts the background receive loop. `StartAsync()`
is the lower-level escape hatch for cases where a caller has already handled startup policy
outside `M1350Monitor` and only wants the retained-state/update loop.

`WatchAsync()` can be subscribed before `ConnectAsync(...)` begins. That lets a caller observe
the identity and revision updates emitted during startup rather than only the steady-state
receive-loop traffic.

Example:

```csharp
await using M1350Session session = M1350Session.Create(transport, leaveOpen: false);
await using M1350Monitor monitor = new(session);

M1350MonitorSnapshot snapshot = await monitor.ConnectAsync(
   requestedRevision: "A20",
   autoSend: AutoSendBehavior.Enabled,
   cancellationToken);
```

The explicit auto-send setting belongs here. The spec makes that a startup policy choice, not just
an incidental command:

- after power-up, the monitor does not automatically send CTG data
- under normal conditions, `G` mode is preferred
- `H` stops automatic CTG transmission but does not stop event-marker or note traffic
- a `?C` request cancels auto-send mode

That means the monitor abstraction should make the policy visible instead of hiding it.

An enum is a little clearer than a bare boolean:

```csharp
enum AutoSendBehavior
{
   Disabled,
   Enabled,
}
```

## Receive loop

The monitor layer is the natural owner of the single long-running read loop:

```csharp
await foreach (M1350Message message in session.ReadAllAsync(cancellationToken))
{
   switch (message)
   {
      case IdMessage id:
         // update state and publish update
         break;
      case CtgMessage ctg:
         // update state and publish update
         break;
      case NibpMessage nibp:
         // update state and publish update
         break;
      case SpO2Message spo2:
         // update state and publish update
         break;
      case TemperatureMessage temperature:
         // update state and publish update
         break;
      case NoteMessage note:
         // update state and publish update
         break;
      case FailureMessage failure:
         // update state and publish update
         break;
   }
}
```

This is exactly the kind of repeated orchestration that should live above the session layer, not in
every consumer.

## State shape

The monitor layer uses both a mutable internal state object and an externally consumable snapshot
shape.

This aligns well with the protocol described in the Philips guide. The monitor mostly emits
current-device state and current observations:

- CTG data is either requested as the next current block or streamed once per second in auto-send mode
- the `I` block is used at startup and for protocol confirmation
- NIBP, maternal SpO2, maternal temperature, failures, notes, and event markers are all pushed as
  current updates rather than queried as historical records

So a snapshot model is a better semantic match than a request-centric object graph. It gives the
caller the latest known clinical picture of the device while the receive loop continues to update it.

What the snapshot likely needs, however, is stronger freshness metadata than the first sketch showed.
Clinical consumers usually care not just about the retained value but how old it is.

Current snapshot:

```csharp
sealed record M1350MonitorSnapshot(
   IdBlock? Identity,
   CtgBlock? Ctg,
   NibpBlock? Nibp,
   SpO2Block? SpO2,
   TemperatureBlock? Temperature,
   EventMessageBlock? EventMarker,
   NoteBlock? Note,
   FailureBlock? Failure,
   string? NegotiatedRevision,
   bool IsAutoSendActive,
   DateTimeOffset? MessageReceivedAt,
   DateTimeOffset? CtgReceivedAt,
   DateTimeOffset? NibpReceivedAt,
   DateTimeOffset? SpO2ReceivedAt,
   DateTimeOffset? TemperatureReceivedAt,
   DateTimeOffset? FailureReceivedAt);
```

This keeps the transport/session surface separate from the monitor's domain view of the device.

The current API also exposes a small update-union type without abandoning the snapshot-first model:

```csharp
abstract record M1350MonitorUpdate(DateTimeOffset Timestamp);
sealed record IdentityUpdated(IdBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);
sealed record CtgUpdated(CtgBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);
sealed record NibpUpdated(NibpBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);
// ... one per currently supported inbound block category
```

## Stable update contract

`WatchAsync()` is now the ordered stream of observable monitor state transitions.

That includes both categories of change owned by `M1350Monitor`:

1. inbound device messages that update retained monitor state
2. monitor-owned lifecycle transitions, such as negotiated revision selection and auto-send
   activation changes

This means a caller that subscribes before `ConnectAsync(...)` can observe the complete startup
story in order rather than reconstructing part of it from `Snapshot` alone.

The retained snapshot is still the primary current-state model, but `WatchAsync()` is the stable
transition log for startup and steady-state behavior.

## What stays out of the monitor layer

The monitor layer should still avoid taking on unrelated concerns:

- serial port discovery and configuration
- UI-specific binding models
- persistence/storage
- retry or retransmission policy that contradicts the protocol requirements
- per-block parsing logic already owned by the application layer

## Design constraints carried upward from the session layer

Any monitor abstraction needs to preserve the constraints already present in `M1350Session`:

1. only one async reader should consume a given session at a time
2. request/response operations and continuous receive mode must be coordinated, not run as unrelated reads
3. `M1350Session` remains the unit that understands framing-aware request/response helpers
4. `?C` requests cancel auto-send mode, so request-style sampling and background streaming are not independent modes

This means the monitor implementation centralizes all async reading into one owned loop and still
needs an explicit long-term policy for when command methods are allowed while auto-send is active.

## First-pass implementation status

The first worthwhile monitor-layer increment is now in place:

1. compose an existing `M1350Session`
2. provide `ConnectAsync(...)`
3. own one background `ReadAllAsync(...)` loop
4. maintain a latest-state snapshot
5. expose a small async update stream for the currently implemented inbound block types

That is enough to validate `M1350Monitor` as a useful abstraction. The remaining work is API
stabilization, not another large implementation slice.
