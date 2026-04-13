# Philips M1350

This section documents Philips M1350 data block encoding and decoding support in MDI.

## What is supported

- Data Link Layer framing and escaping
- CRC validation using `CRC-16/XMODEM`
- Read and write support via `DataBlockReader` and `DataBlockWriter`
- Optional validation skipping for writer performance tuning

## Current scope

MDI currently implements Data Link Layer behavior only.

- Implemented: block framing, escaping, CRC generation and validation, and stream parsing primitives.
- Not implemented here: application-layer message semantics and field-level parsing for block types such as `C`, `I`, `N`, and `F`.

## Namespace

- `MDI.IO.Encoding.Philips.M1350`

## Documents

- [Data Link Layer](data-link-layer.md)
