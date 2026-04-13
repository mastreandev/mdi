# Philips M1350

This section documents Philips M1350 data block encoding and decoding support in MDI.

## What is supported

- Data Link Layer framing and escaping
- CRC validation using `CRC-16/XMODEM`
- Read and write support via `DataBlockReader` and `DataBlockWriter`
- Optional validation skipping for writer performance tuning

## Current scope

- **Implemented:** block framing, DLE escaping, CRC generation and validation, stream parsing primitives (`DataBlockReader`, `DataBlockWriter`)
- **In progress:** application-layer field-level parsing for block type `C` (CTG data block)
- **Planned:** application-layer parsers for `F`, `I`, `M`, `N`, `P`, `S`, `T` blocks; session-layer dispatcher and revision negotiation

## Namespaces

- `MDI.Philips.M1350` — link-layer types
- `MDI.Philips.M1350.Application.CTG` — CTG block model, parser, and encoder

## Documents

- [Data Link Layer](data-link-layer.md)
- [Application Layer](application-layer.md)
- [Session Layer](session-layer.md)
