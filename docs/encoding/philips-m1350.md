# Philips M1350

This section documents the Philips M1350 data block encoding and decoding support in the MDI project.

The implementation is currently the only device-specific encoding supported by MDI, and it is a natural starting point for expanding support to additional devices later.

## What is supported

- Philips M1350 data block framing and escaping
- CRC validation using `CRC-16/XMODEM`
- Read and write support via `DataBlockReader` and `DataBlockWriter`
- Optional validation skipping for writer performance tuning

## Namespace

The implementation lives under:

- `MDI.IO.Encoding.Philips.M1350`

## Framing format

Philips M1350 data blocks are framed using the following control bytes:

- `DLE` = `0x10`
- `STX` = `0x02`
- `ETX` = `0x03`

A valid block is written as:

- `DLE STX` to start the block
- escaped data bytes
- `DLE ETX` to end the block
- 2-byte CRC-16/XMODEM value in big-endian order

## Escaping

- The encoder duplicates any `DLE` byte found in the raw data.
- This prevents the reader from misinterpreting an in-band `DLE` byte as a frame delimiter.

## CRC algorithm

The Philips M1350 writer and reader use a shared CRC algorithm:

- `CRC-16/XMODEM`
- big-endian output

The reader verifies a block by expecting the final two CRC bytes to produce a zero residue when appended to the decoded payload.

## API contract

The Philips M1350 docs are intended to describe protocol behavior, not code-level implementation details.

Key contract points:

- a block begins with `DLE STX`
- raw payload bytes are escaped by duplicating `DLE`
- a block ends with `DLE ETX`
- two CRC-16/XMODEM bytes follow the end delimiter
- interrupted blocks may be terminated with a zeroed CRC sequence

The reader exposes a `TryRead`-style parser that advances the source buffer on success and returns the decoded payload.

The writer exposes a block-writing API that emits framing, escaped data, and CRC bytes in the correct order.

## State machine

The Philips M1350 reader parses the stream using a block-oriented state machine.

::: mermaid
stateDiagram-v2
direction LR

    [*] --> None
    None --> Escape: DLE
    None --> None: other byte

    Escape --> Data: STX
    Escape --> None: ETX + CRC valid
    Escape --> None: ETX + CRC invalid

    Data --> Data: normal byte
    Data --> Data: DLE DLE (escaped DLE)
    Data --> Escape: DLE + non-DLE

    note right of Escape
      After a DLE, the reader expects either STX or ETX.
      ETX triggers CRC validation on the block.
    end note

:::
