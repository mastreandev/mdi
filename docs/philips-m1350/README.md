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
- Protocol revision comparison and negotiated identity validation via `ProtocolRevision`

## Current scope

- **Implemented:** block framing, DLE escaping, CRC generation and validation, stream parsing primitives (`DataBlockReader`, `DataBlockWriter`), application-layer support for `C`, `F`, `I`, `MM`, `N`, `P`, `S`, `T`, `G`, `H`, `?`, and `V`, host-originated note encoding for `N`, and an initial synchronous session-layer slice for routing, request writing, and revision-negotiation validation
- **In progress:** expanding session orchestration from the current synchronous facade into richer request/response helpers and the planned async API
- **Planned:** broader async session API and richer session orchestration above the current synchronous facade

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
