# Philips M1350 Data Link Layer

This document is a cleaned extraction of the Data Link Layer behavior for the Philips Series 50 M1350 protocol.

## Scope

This page covers link-layer transport only:

- block framing
- escaping rules
- special control characters
- receiver behavior for malformed or interrupted blocks
- CRC-16 details needed for block validation

Application-layer block payload formats are intentionally out of scope.

## Control characters

The protocol uses three control bytes:

| Symbol | Hex  | Meaning          |
| ------ | ---- | ---------------- |
| DLE    | 0x10 | Data Link Escape |
| STX    | 0x02 | Start of Text    |
| ETX    | 0x03 | End of Text      |

## Frame format

A transport frame is encoded as:

1. Start delimiter: DLE STX
2. Payload bytes (with DLE escaping applied)
3. End delimiter: DLE ETX
4. CRC trailer: 2 bytes, CRC-16 (CCITT / XMODEM polynomial)

Conceptually:

DLE STX [payload...] DLE ETX CRC_H CRC_L

## Escaping rule

To preserve 8-bit transparency:

- every payload DLE byte is duplicated as DLE DLE
- the receiver decodes DLE DLE back to a single payload DLE

This prevents payload bytes from being misread as block delimiters.

## Receiver rules

The protocol behavior in the guide implies these receiver-side rules:

1. CRC failure: discard the whole block.
2. New start before prior end: if DLE STX appears before a valid DLE ETX + CRC completion, discard the incomplete block and begin a new one.
3. Out-of-block bytes: bytes between completed blocks may appear and are ignored until the next DLE STX.
4. Interrupted-after-ETX edge case: if transmission stops after DLE ETX but before both CRC bytes are safely consumed, senders can emit two non-special filler bytes (for example 0x00 0x00) before starting the next block.

## CRC details

The referenced CRC is CCITT CRC-16 with generator polynomial:

x^16 + x^12 + x^5 + 1

Operational validation pattern:

- compute CRC over the framed block bytes and compare to transmitted CRC, or
- compute CRC over framed block bytes including transmitted CRC and expect zero residue

In this repository, the implementation aligns with CRC-16/XMODEM conventions used by the Philips M1350 codec.

## Notes for MDI implementation

- Link-layer processing should remain independent from application block semantics.
- Data blocks are not retried at the link layer; invalid blocks are dropped.
- Keep block parser logic robust to noise and partial frames on serial streams.
