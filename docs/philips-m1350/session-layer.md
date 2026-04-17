# Philips M1350 Session Layer

This document describes the session and dispatch layer that sits above the
application-layer block parsers.

A complete synchronous facade for the currently implemented block set is in place now, and an
initial async transport-facing slice is implemented as well:

- `M1350Message` as the current typed message union
- `M1350MessageReader.TryRead(...)` for framed routing of supported inbound blocks
- `M1350CommandWriter` for framed outbound `G`, `H`, `N`, `?I`, `?C`, and `Vxxx` commands
- `M1350Session` as the synchronous facade over those lower-level reader/writer APIs
- synchronous request/response helpers for identity, CTG, and revision negotiation
- static typed read helpers for every currently supported inbound block type
- async `PipeReader`-based message streaming via `M1350Session.ReadAllAsync(...)`
- async `IDuplexPipe` convenience entry point for paired input/output transports
- stream convenience factories that adapt `Stream` transports into the same pipe-based core
- async request/workflow helpers for identity, CTG, and revision negotiation
- async command-writing helpers that flush when the output is a `PipeWriter`
- `ProtocolRevision` comparison plus negotiated-identity validation for `I`-block revision checks
- inbound routing for `F`, `MM`, `N`, `P`, `S`, and `T` blocks

The broader async/session API described below is now implemented in its first slice, including
`IDuplexPipe` and `Stream` convenience entry points. The remaining planned work is broader
orchestration that sits above the current `PipeReader`-based core. See
[Monitor Layer](monitor-layer.md).

## Position in the stack

```
Physical layer      RS232 / RS422 serial port
Data link layer     DataBlockReader / DataBlockWriter  (framing, escaping, CRC)
Application layer   CtgBlockParser, etc.               (per-block typed parsing)
Session layer       M1350Session / async reader        (routing, state, IAsyncEnumerable)
```

## Responsibilities

The session layer owns:

1. **Block routing** — reads `payload[0]` (block type byte) and dispatches to the matching
   parser
2. **Unknown block tolerance** — payloads with unrecognised type bytes are silently
   dropped (required by the spec: "Unknown data blocks are ignored")
3. **Protocol revision negotiation** — sends `V`-block, waits for `I`-block response,
   confirms revision via ID code
4. **Auto-send lifecycle** — sends `G` to enter auto-send mode, `H` to halt
5. **Request/response pairing** — sends `?C` or `?I` and correlates the response

## Block routing table

| Type byte | Hex  | Direction | Parser?                   | Notes                                                   |
| --------- | ---- | --------- | ------------------------- | ------------------------------------------------------- |
| `C`       | 0x43 | FM→Host   | `CtgBlockParser`          | Fixed 35 bytes                                          |
| `F`       | 0x46 | FM→Host   | `FailureBlockParser`      | Variable; 3-digit ASCII error code                      |
| `G`       | 0x47 | Host→FM   | Encoder only              | No data bytes                                           |
| `H`       | 0x48 | Host→FM   | Encoder only              | No data bytes                                           |
| `I`       | 0x49 | Both      | `IdBlockParser`           | Fixed 27 bytes including type; also sent at FM power-on |
| `M`       | 0x4D | Both      | `EventMessageBlockParser` | Two-byte type `MM`; async both ways                     |
| `N`       | 0x4E | Both      | `NoteBlockParser`         | Variable; ≤29 bytes host→FM, ≤510 FM→host               |
| `P`       | 0x50 | FM→Host   | `NibpBlockParser`         | Fixed 9 bytes                                           |
| `S`       | 0x53 | FM→Host   | `SpO2BlockParser`         | Fixed 4 bytes                                           |
| `T`       | 0x54 | FM→Host   | `TemperatureBlockParser`  | Fixed 2 bytes                                           |
| `V`       | 0x56 | Host→FM   | Encoder only              | Fixed 4 bytes; revision string                          |
| `?`       | 0x3F | Host→FM   | Encoder only              | 2 bytes; request wrapper                                |

## Current implementation shape

```csharp
abstract record M1350Message;
sealed record CtgMessage(CtgBlock Block) : M1350Message;
sealed record IdMessage(IdBlock Block)   : M1350Message;
sealed record EventMarkerMessage(EventMessageBlock Block) : M1350Message;
sealed record NoteMessage(NoteBlock Block) : M1350Message;
sealed record FailureMessage(FailureBlock Block) : M1350Message;
sealed record NibpMessage(NibpBlock Block) : M1350Message;
sealed record TemperatureMessage(TemperatureBlock Block) : M1350Message;
sealed record SpO2Message(SpO2Block Block) : M1350Message;

static class M1350MessageReader
{
   static bool TryRead(ref ReadOnlySequence<byte> buffer, out M1350Message message);
}

static class M1350CommandWriter
{
   static void WriteStartAutoSend(IBufferWriter<byte> output);
   static void WriteHaltAutoSend(IBufferWriter<byte> output);
   static void WriteNote(IBufferWriter<byte> output, string text);
   static void WriteNote(IBufferWriter<byte> output, string text, string userId = "");
   static void WriteRequestIdentity(IBufferWriter<byte> output);
   static void WriteRequestCtg(IBufferWriter<byte> output);
   static void WriteRequest(IBufferWriter<byte> output, byte requestedType);
   static void WriteProtocolRevisionChange(IBufferWriter<byte> output, string requestedRevision);
}

sealed class M1350Session
{
   M1350Session(IBufferWriter<byte> output);
   M1350Session(IDuplexPipe transport);
   M1350Session(PipeReader input, IBufferWriter<byte> output);

   static M1350Session Create(Stream transport, bool leaveOpen = false);
   static M1350Session Create(Stream input, Stream output, bool leaveOpen = false);

   void StartAutoSend();
   ValueTask StartAutoSendAsync(CancellationToken cancellationToken = default);
   void HaltAutoSend();
   ValueTask HaltAutoSendAsync(CancellationToken cancellationToken = default);
   void SendNote(string text);
   void SendNote(string text, string userId);
   ValueTask SendNoteAsync(string text,
                           string userId = "",
                           CancellationToken cancellationToken = default);
   void RequestIdentity();
   bool TryRequestIdentity(ref ReadOnlySequence<byte> buffer, out IdBlock block);
   ValueTask<IdBlock> RequestIdentityAsync(CancellationToken cancellationToken = default);
   void RequestCtg();
   bool TryRequestCtg(ref ReadOnlySequence<byte> buffer, out CtgBlock block);
   ValueTask<CtgBlock> RequestCtgAsync(CancellationToken cancellationToken = default);
   void RequestProtocolRevisionChange(string requestedRevision);
   void NegotiateProtocolRevision(string requestedRevision);
   bool TryNegotiateProtocolRevision(ref ReadOnlySequence<byte> buffer,
                                     string requestedRevision,
                                     out IdBlock block);
   ValueTask<IdBlock> NegotiateRevisionAsync(string requestedRevision,
                                             CancellationToken cancellationToken = default);

   IAsyncEnumerable<M1350Message> ReadAllAsync(CancellationToken cancellationToken = default);

   static bool TryRead(ref ReadOnlySequence<byte> buffer, out M1350Message message);
   static bool TryReadIdentity(ref ReadOnlySequence<byte> buffer, out IdBlock block);
   static bool TryReadNegotiatedIdentity(ref ReadOnlySequence<byte> buffer,
                                         string requestedRevision,
                                         out IdBlock block);
   static bool TryReadCtg(ref ReadOnlySequence<byte> buffer, out CtgBlock block);
   static bool TryReadEventMessage(ref ReadOnlySequence<byte> buffer, out EventMessageBlock block);
   static bool TryReadNote(ref ReadOnlySequence<byte> buffer, out NoteBlock block);
   static bool TryReadFailure(ref ReadOnlySequence<byte> buffer, out FailureBlock block);
   static bool TryReadNibp(ref ReadOnlySequence<byte> buffer, out NibpBlock block);
   static bool TryReadTemperature(ref ReadOnlySequence<byte> buffer, out TemperatureBlock block);
   static bool TryReadSpO2(ref ReadOnlySequence<byte> buffer, out SpO2Block block);
   static bool IsProtocolRevisionSatisfied(in IdBlock block, string requestedRevision);
}
```

This synchronous facade is intentionally bounded to the current application-layer coverage (`C`,
`F`, `I`, `MM`, `N`, `P`, `S`, and `T`). It exposes command writing and request/response
helpers on the instance surface, with static typed read helpers for the supported inbound set,
while still ignoring unknown or not-yet-implemented inbound block types after framing succeeds.

## Current async API shape

The async API should model the device as a framed byte stream, not as a message queue.
That means:

1. `PipeReader` is the core input boundary
2. `IDuplexPipe` is the preferred convenience surface when the caller already has a duplex transport
3. `IAsyncEnumerable<M1350Message>` is the primary message-consumption surface
4. `Stream` is a convenience entry point, not the core implementation shape

```csharp
sealed class M1350Session : IAsyncDisposable
{
   M1350Session(IDuplexPipe transport);
   M1350Session(PipeReader input, IBufferWriter<byte> output);

   static M1350Session Create(Stream transport, bool leaveOpen = false);
   static M1350Session Create(Stream input, Stream output, bool leaveOpen = false);

   ValueTask StartAutoSendAsync(CancellationToken cancellationToken = default);
   ValueTask HaltAutoSendAsync(CancellationToken cancellationToken = default);
   ValueTask SendNoteAsync(string text,
                     string userId = "",
                     CancellationToken cancellationToken = default);

   ValueTask<IdBlock> RequestIdentityAsync(CancellationToken cancellationToken = default);
   ValueTask<CtgBlock> RequestCtgAsync(CancellationToken cancellationToken = default);
   ValueTask<IdBlock> NegotiateRevisionAsync(string requestedRevision,
                                   CancellationToken cancellationToken = default);

    IAsyncEnumerable<M1350Message> ReadAllAsync(CancellationToken cancellationToken = default);
}
```

The current implementation keeps the session-facing API centered on `M1350Session`, not on a
separate public message-reader type. `IDuplexPipe` is now the preferred convenience entry point
for fully asynchronous transports because it makes the read and write halves explicit without
giving up the existing `IBufferWriter<byte>`-based sync-friendly surface.

## PipeReader versus Stream

For modern .NET, `PipeReader` is the better core fit for this protocol because it matches the
existing parser style built around `ReadOnlySequence<byte>` and makes partial-frame consumption
natural.

`Stream` should still be supported as a convenience entry point for callers that do not already
have a pipeline-based transport. The current implementation exposes factories that adapt either
a duplex `Stream` or separate input/output streams into the same pipe-based core rather than
building the core session loop directly on `Stream`.

Example convenience entry point:

```csharp
sealed class M1350Session : IAsyncDisposable
{
   static M1350Session Create(Stream transport, bool leaveOpen = false);
   static M1350Session Create(Stream input, Stream output, bool leaveOpen = false);
}
```

`M1350Message` is a closed hierarchy via abstract record + sealed subtypes:

```csharp
abstract record M1350Message;
sealed record CtgMessage(CtgBlock Block)                      : M1350Message;
sealed record FailureMessage(string ErrorCode)                : M1350Message;
sealed record IdMessage(string IdCode,
                        string ProtocolRevision,
                        string SoftwareRevision,
                        string SerialNumber)                  : M1350Message;
// ... one per receivable block type
```

This allows callers to `switch` exhaustively without casting.

## Protocol revision state

Revision negotiation is the only stateful operation in the session layer:

1. Caller requests revision upgrade by sending a `V`-block with target revision (e.g. `A20`)
2. Session layer sends `?I` request
3. Session layer receives `I`-block and reads the protocol revision field
4. If revision matches or exceeds target, FSpO2 data is live in subsequent `C`-blocks

State is held in the session-level async read loop but reflected passively in
`CtgBlock.Status.IsFSpO2Available` — parsers remain stateless.

In the current synchronous slice, revision comparison is handled by `ProtocolRevision`, and
`M1350Session.TryReadNegotiatedIdentity(...)` validates that the returned `I` block satisfies
the requested revision token before reporting success.

## Async workflow sketch

The startup and request workflows are small enough that they should be exposed directly rather
than forcing callers to manually compose `V`, `?I`, `?C`, and `IdBlockParser` every time.

```csharp
sealed class M1350Session : IAsyncDisposable
{
   ValueTask<IdBlock> RequestIdentityAsync(CancellationToken cancellationToken = default);
   ValueTask<CtgBlock> RequestCtgAsync(CancellationToken cancellationToken = default);

   ValueTask<IdBlock> NegotiateRevisionAsync(
      string requestedRevision,
      CancellationToken cancellationToken = default);

   IAsyncEnumerable<M1350Message> ReadAllAsync(CancellationToken cancellationToken = default);
}
```

Expected `NegotiateRevisionAsync` flow:

1. Encode and send `V` plus the requested 3-character revision token
2. Encode and send `?I`
3. Read frames until an `I` block is received
4. Parse the block with `IdBlockParser`
5. Return the parsed `IdBlock` if the revision was accepted; otherwise fail with a protocol-level error

Expected `RequestCtgAsync` flow:

1. Encode and send `?C`
2. Read frames until a `C` block is received
3. Parse the block with `CtgBlockParser`
4. Return the parsed `CtgBlock`

The important separation is:

- application layer owns `IdBlockParser`, `RequestBlockEncoder`, and `ProtocolRevisionChangeRequestEncoder`
- session layer owns ordering, waiting, timeout/cancellation, and correlating the returned `I` block to the request flow

Current behavior notes:

1. the async API uses `PipeReader` for inbound framed transport bytes
2. async command methods flush when the output destination is a `PipeWriter`
3. only one asynchronous read operation is supported at a time on a single `M1350Session`

Today, the current synchronous API surface is complete for the implemented block set, and the
first async transport-facing slice is implemented on top of `PipeReader`, `IAsyncEnumerable`, and
explicit async workflows.

## Relationship to application layer

Application-layer parsers (`CtgBlockParser`, etc.) are pure stateless functions callable
independently of any session context. They require only a decoded payload span. The session
layer calls them but does not own them.

## What is out of scope for the session layer

- Serial port ownership and configuration (caller provides a `PipeReader` or, in the future, uses a stream-to-pipe convenience entry point)
- Retry logic (the spec explicitly prohibits retransmission)
- Physical framing (handled by `DataBlockReader`)

## Namespace

`MDI.Philips.M1350` (session type)
`MDI.Philips.M1350.Application` (message union types)
