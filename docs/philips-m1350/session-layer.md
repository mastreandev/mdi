# Philips M1350 Session Layer

This document describes the planned session and dispatch layer that sits above the
application-layer block parsers. It is not yet implemented.

## Position in the stack

```
Physical layer      RS232 / RS422 serial port
Data link layer     DataBlockReader / DataBlockWriter  (framing, escaping, CRC)
Application layer   CtgBlockParser, etc.               (per-block typed parsing)
Session layer       M1350MessageReader                 (routing, state, IAsyncEnumerable)
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

| Type byte | Hex  | Direction   | Parser?                | Notes                                    |
| --------- | ---- | ----------- | ---------------------- | ---------------------------------------- |
| `C`       | 0x43 | FM→Host     | `CtgBlockParser`       | Fixed 35 bytes                           |
| `F`       | 0x46 | FM→Host     | `FailureBlockParser`   | Variable; 3-digit ASCII error code       |
| `G`       | 0x47 | Host→FM     | Encoder only           | No data bytes                            |
| `H`       | 0x48 | Host→FM     | Encoder only           | No data bytes                            |
| `I`       | 0x49 | Both        | `IdBlockParser`        | Fixed 26 bytes; also sent at FM power-on |
| `M`       | 0x4D | Both        | `EventMessageParser`   | Two-byte type `MM`; async both ways      |
| `N`       | 0x4E | Both        | `NoteBlockParser`      | Variable; ≤29 bytes host→FM, ≤510 FM→host |
| `P`       | 0x50 | FM→Host     | `NibpBlockParser`      | Fixed 9 bytes                            |
| `S`       | 0x53 | FM→Host     | `SpO2BlockParser`      | Fixed 4 bytes                            |
| `T`       | 0x54 | FM→Host     | `TemperatureBlockParser` | Fixed 2 bytes                          |
| `V`       | 0x56 | Host→FM     | Encoder only           | Fixed 4 bytes; revision string           |
| `?`       | 0x3F | Host→FM     | Encoder only           | 2 bytes; request wrapper                 |

## Proposed API shape

```csharp
sealed class M1350MessageReader : IAsyncDisposable
{
    M1350MessageReader(PipeReader reader);

    // Reads frames until cancellation, yielding parsed application messages.
    IAsyncEnumerable<M1350Message> ReadAllAsync(CancellationToken cancellationToken = default);
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

State is held in `M1350MessageReader` but reflected passively in
`CtgBlock.Status.IsFSpO2Available` — parsers remain stateless.

## Relationship to application layer

Application-layer parsers (`CtgBlockParser`, etc.) are pure stateless functions callable
independently of any session context. They require only a decoded payload span. The session
layer calls them but does not own them.

## What is out of scope for the session layer

- Serial port I/O (caller provides a `PipeReader`)
- Retry logic (the spec explicitly prohibits retransmission)
- Physical framing (handled by `DataBlockReader`)

## Namespace

`MDI.Philips.M1350` (session type)
`MDI.Philips.M1350.Application` (message union types)
