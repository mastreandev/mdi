# Philips M1350

This section documents Philips M1350 data block encoding and decoding support in MDI.

## What is supported

- Data Link Layer framing and escaping
- CRC validation using `CRC-16/XMODEM`
- Read and write support via `DataBlockReader` and `DataBlockWriter`
- Optional validation skipping for writer performance tuning
- Application-layer parsing for `C` (CTG), `F` (failure), `I` (identity), `MM` (event marker), and `N` (note) blocks
- Application-layer encoding for host-originated `N` notes
- Application-layer encoding for `G` and `H` auto-send control commands
- Application-layer parsing for `P` (NIBP), `S` (maternal SpO2), and `T` (temperature) blocks
- Application-layer encoding for `?` requests and `V` revision-change blocks
- Initial session-layer routing via `M1350MessageReader`
- Initial framed command writing via `M1350CommandWriter`, including `G` and `H` auto-send control
- Initial synchronous session facade via `M1350Session`
- Initial monitor orchestration via `M1350Monitor`
- Protocol revision comparison and negotiated identity validation via `ProtocolRevision`

## Current scope

- **Implemented:** block framing, DLE escaping, CRC generation and validation, stream parsing primitives (`DataBlockReader`, `DataBlockWriter`), application-layer support for `C`, `F`, `I`, `MM`, `N`, `P`, `S`, `T`, `G`, `H`, `?`, and `V`, host-originated note encoding for `N`, the complete synchronous session facade for the currently implemented block set, and the first async session slice centered on `PipeReader`, `IDuplexPipe`, and stream entry points, `IAsyncEnumerable<M1350Message>` output, async request/workflow methods, and flush-aware async command writes
- **Implemented:** `M1350Monitor` as the higher-level monitor facade above the current pipe-based session API, including startup orchestration, a background receive loop, a retained snapshot, and an async update stream for inbound and monitor-owned state transitions
- **Planned:** additional higher-level workflow helpers only if repeated transport or session orchestration patterns still prove necessary

## Namespaces

- `MDI.Philips.M1350` — data-link and current session-layer types
- `MDI.Philips.M1350.Application` — shared application-layer encoders
- `MDI.Philips.M1350.Application.CTG` — CTG block model, parser, and encoder
- `MDI.Philips.M1350.Application.EventMessage` — event marker block model and parser
- `MDI.Philips.M1350.Application.Failure` — failure block model and parser
- `MDI.Philips.M1350.Application.Identity` — identity block model and parser
- `MDI.Philips.M1350.Application.Nibp` — maternal blood-pressure block model and parser
- `MDI.Philips.M1350.Application.Notes` — note block model and parser
- `MDI.Philips.M1350.Application.SpO2` — maternal oxygen-saturation block model and parser
- `MDI.Philips.M1350.Application.Temperature` — maternal temperature block model and parser

## Documents

- [Data Link Layer](data-link-layer.md)
- [Application Layer](application-layer.md)
- [Session Layer](session-layer.md)
- [Monitor Layer](monitor-layer.md)
- [Monitor Integration Shapes](monitor-integration-shapes.md)
- [Replay Capture Shapes](replay-capture-shapes.md)
- [Replay Instrumentation](replay-instrumentation.md)
- [Simulator and FM Origination](simulator-and-fm-origination.md)
