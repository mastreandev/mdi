# Philips M1350 Application Layer

This document describes the planned application-layer block parsing and encoding for the
Philips M1350 protocol within MDI. It covers block-payload formats, typed C# model
design, test vector strategy, and API shape.

## Scope

This page covers the currently implemented application-layer payload formats plus the minimal
host-originated control payload encoders used for identity, CTG, and revision negotiation.
For block routing and directionality, see [Session Layer](session-layer.md).

Application-layer processing assumes the data link layer has already:

1. Located a valid CRC-verified frame in the stream
2. Decoded DLE escapes from the payload
3. Returned a `ReadOnlySequence<byte>` representing the raw block bytes (beginning with
   the type character)

## General block structure

After link-layer decoding, every application block has this shape:

```
[type byte] [data bytes...]
```

The type byte is defined in Table 2-3 of the programmer's guide. Parsers verify this byte
and return `false` if it does not match.

## C-Block (CTG Data Block)

### Summary

- Sent FM→Host automatically in auto-send mode, once per second
- Sent FM→Host once in response to a `?C` request
- Fixed payload length: 35 bytes (1 type byte + 34 data bytes)
- Contains 4 samples each of HR1, HR2, MHR, and Toco, sampled at 250 ms intervals
  (oldest first)

### Payload layout

| Offset | Field         | Size  |
| ------ | ------------- | ----- |
| 0      | Type byte `C` | 1     |
| 1–2    | Status word   | 2     |
| 3–10   | HR1[0..3]     | 4 × 2 |
| 11–18  | HR2[0..3]     | 4 × 2 |
| 19–26  | MHR[0..3]     | 4 × 2 |
| 27–30  | Toco[0..3]    | 4 × 1 |
| 31–32  | HR Mode word  | 2     |
| 33     | Toco Mode     | 1     |
| 34     | FSpO2         | 1     |

Cross-checked against PCDEMO: `Data[3..4]` = HR1[0], `Data[11..12]` = HR2[0],
`Data[27]` = Toco[0].

### Status word (2 bytes, big-endian, Table 3-4)

| Byte | Bit | Mask   | Meaning when 1                   |
| ---- | --- | ------ | -------------------------------- |
| High | 7   | 0x8000 | FMP enabled                      |
| High | 6   | 0x4000 | HR1 twin offset active (+20 bpm) |
| High | 5   | —      | Reserved (zero)                  |
| High | 4   | —      | Not used (zero)                  |
| High | 3   | —      | Reserved (zero — avoids DLE)     |
| High | 2   | 0x0400 | DECG logic on                    |
| High | 1   | —      | Reserved                         |
| High | 0   | —      | Reserved (zero)                  |
| Low  | 7   | 0x0080 | HR cross-channel verification    |
| Low  | 6   | 0x0040 | Telemetry on                     |
| Low  | 5   | —      | Reserved                         |
| Low  | 4   | 0x0010 | FSpO2 available (rev A.02.00+)   |
| Low  | 3   | —      | Remains zero (avoids DLE)        |
| Low  | 2   | 0x0004 | CTG data deleted (250 ms tick)   |
| Low  | 1   | 0x0002 | CTG data inserted (250 ms tick)  |
| Low  | 0   | 0x0001 | Monitor on                       |

The high byte keeps bit 4 fixed at zero so it can never equal `DLE` (0x10).

### HR1 sample coding (Table 3-6, two bytes per sample)

| Bits   | Field          | Notes                                   |
| ------ | -------------- | --------------------------------------- |
| H[7]   | Reserved       | Always zero                             |
| H[6:5] | Signal quality | 00=Unknown, 01=Red, 10=Yellow, 11=Green |
| H[4:3] | FMP            | 00=None, 01=Movement, 10–11=Reserved    |
| H[2:0] | HR bits[10:8]  | Upper 3 bits of 11-bit raw value        |
| L[7:0] | HR bits[7:0]   | Lower 8 bits of 11-bit raw value        |

Raw value (11-bit): `(highByte & 0x07) << 8 | lowByte`, range 0–1200.

`0` = blank trace. Resolution: 0.25 bpm (divide raw by 4 to get bpm).

PCDEMO formula: `(Data[4] + ((Data[3] & 0x07) << 8)) / 4.0` → bpm floating-point.

### HR2 / MHR sample coding (Table 3-7)

Identical to HR1 except high byte bits [4:3] are reserved (zero). No FMP information.

### Toco coding (Table 3-8)

Single byte per sample, raw value 0–255. Values 0–200 represent 0–127 toco units at
0.5-unit resolution. PCDEMO formula: `Data[27] / 2.0`.

### HR Mode word (Tables 3-9, 3-10, two bytes big-endian)

| Byte | Bits  | Field    |
| ---- | ----- | -------- |
| High | [7:5] | MHR mode |
| High | [4]   | 0        |
| High | [3:1] | HR2 mode |
| High | [0]   | 0        |
| Low  | [7:5] | HR1 mode |
| Low  | [4:0] | 0        |

Keeping bits [4] of the high byte and bits [4:0] of the low byte fixed at zero ensures
neither byte can equal `DLE` (0x10).

3-bit mode codes (Table 3-10):

| Code | Name         |
| ---- | ------------ |
| 000  | NoTransducer |
| 001  | Ultrasound   |
| 010  | Decg         |
| 011  | Mecg         |
| 100  | ExternalMhr  |
| 101  | Reserved     |
| 110  | Reserved     |
| 111  | Unknown      |

### Toco Mode byte (Table 3-11)

Bits [3:1] hold a 3-bit toco mode code. All other bits are zero. Bit [0] reserved zero
prevents the byte from matching `DLE`.

| Code | Name         |
| ---- | ------------ |
| 000  | NoTransducer |
| 001  | External     |
| 010  | Iup          |
| 111  | Unknown      |

### FSpO2 byte (Table 3-12)

Valid only when `Status.IsFSpO2Available == true`. Value 0 = invalid/do not print.
Otherwise: 1% resolution, bits [6:0] for revision A.02.

The FSpO2 byte is always present in the 35-byte payload on both A.01 and A.02 devices.
Whether it carries meaningful data is signalled entirely by the `IsFSpO2Available` status
bit — no external revision state is required at the parser level.

## C# model design

### Enums

```csharp
enum SignalQuality { Unknown = 0, Red = 1, Yellow = 2, Green = 3 }
enum FmpValue      { None = 0, Movement = 1 }
enum HrMode        { NoTransducer = 0, Ultrasound = 1, Decg = 2, Mecg = 3,
                     ExternalMhr = 4, Unknown = 7 }
enum TocoMode      { NoTransducer = 0, External = 1, Iup = 2, Unknown = 7 }
```

### Sample types

```csharp
readonly record struct HeartRateSample(ushort RawValue, SignalQuality Quality)
readonly record struct FhrSample(ushort RawValue, FmpValue Fmp, SignalQuality Quality)
```

`RawValue` is the 11-bit integer (0–1200). Callers compute bpm as `RawValue / 4.0` if
needed; this library does not perform unit conversion.

### Status word

```csharp
readonly record struct CtgStatusWord(ushort RawValue)
```

Exposes computed bool properties: `IsFmpEnabled`, `IsHr1TwinOffsetActive`,
`IsDecgLogicOn`, `IsHrCrossChannelVerified`, `IsTelemetryOn`, `IsFSpO2Available`,
`IsCtgDataDeleted`, `IsCtgDataInserted`, `IsMonitorOn`.

### Top-level block

```csharp
readonly record struct CtgBlock
```

Properties:

- `CtgStatusWord Status`
- Four `FhrSample` values for HR1 (oldest to newest: `Fhr1Sample0` through `Fhr1Sample3`)
- Four `HeartRateSample` values for HR2 (`Fhr2Sample0` through `Fhr2Sample3`)
- Four `HeartRateSample` values for MHR (`MhrSample0` through `MhrSample3`)
- Four `byte` toco values (`TocoSample0` through `TocoSample3`)
- `HrMode Hr1Mode, Hr2Mode, MhrMode`
- `TocoMode TocoMode`
- `byte FSpO2`

### Parser and encoder

```csharp
static class CtgBlockParser
{
    static bool TryParse(ReadOnlySpan<byte> payload, out CtgBlock block);
    static bool TryParse(ReadOnlySequence<byte> payload, out CtgBlock block);
}

static class CtgBlockEncoder
{
    static int EncodedLength { get; }  // constant 35
    static bool TryEncode(in CtgBlock block, Span<byte> destination, out int bytesWritten);
}
```

`TryParse` returns `false` if `payload.Length < 35` or `payload[0] != 'C'`. The
`ReadOnlySequence<byte>` overload copies to a stackalloc span and delegates to the span
version.

## Identity and negotiation control blocks

## Variable-length and control-originated inbound blocks

### MM-Block (Event Marker Message)

- Sent FM→Host asynchronously whenever the marker button is pressed
- Fixed payload length: 2 bytes
- Encoded as the literal ASCII sequence `MM`

| Offset | Field              | Size |
| ------ | ------------------ | ---- |
| 0      | Type byte `M`      | 1    |
| 1      | Secondary byte `M` | 1    |

This is the only currently supported inbound block whose identity spans two bytes instead of
the usual single-byte type plus data payload.

```csharp
readonly record struct EventMessageBlock;

static class EventMessageBlockParser
{
    static bool TryParse(ReadOnlySpan<byte> payload, out EventMessageBlock block);
    static bool TryParse(ReadOnlySequence<byte> payload, out EventMessageBlock block);
}
```

### N-Block (Note)

- Sent FM→Host for monitor-originated nursing notes
- Also defined Host→FM for annotations printed on the CTG trace
- Variable payload length

| Offset | Field          | Size |
| ------ | -------------- | ---- |
| 0      | Type byte `N`  | 1    |
| 1      | User ID length | 1    |
| 2..    | User ID + text | Var  |

The byte at offset 1 is the number of characters used for the optional user ID. The text begins
immediately after the user ID bytes; there is no separator. Monitor-originated notes normally use
`0` here and place the full note text in the remaining bytes.

The spec states these practical limits:

- Host→FM: up to 29 transmitted bytes total for the N-block payload, which means up to 28 printable
  characters across user ID plus text
- FM→Host: up to 510 transmitted bytes total; the monitor sets the user ID length byte to `0`

```csharp
readonly record struct NoteBlock(string UserId, string Text);

static class NoteBlockParser
{
    static bool TryParse(ReadOnlySpan<byte> payload, out NoteBlock block);
    static bool TryParse(ReadOnlySequence<byte> payload, out NoteBlock block);
}

static class NoteBlockEncoder
{
    static int MaximumPayloadLength { get; }  // constant 30
    static bool TryEncode(in NoteBlock block, Span<byte> destination, out int bytesWritten);
}
```

For host-originated notes, the encoder requires ASCII-only text, a non-empty note body, and no
more than 28 printable characters across `UserId` plus `Text`.

### F-Block (Failure)

- Sent FM→Host when the monitor reports a defect or fatal error
- Fixed payload length: 4 bytes
- Contains a 3-character ASCII error code

| Offset | Field            | Size |
| ------ | ---------------- | ---- |
| 0      | Type byte `F`    | 1    |
| 1–3    | ASCII error code | 3    |

Example payload: `F503`.

```csharp
readonly record struct FailureBlock(string ErrorCode);

static class FailureBlockParser
{
    static bool TryParse(ReadOnlySpan<byte> payload, out FailureBlock block);
    static bool TryParse(ReadOnlySequence<byte> payload, out FailureBlock block);
}
```

### I-Block (ID-Code)

- Sent FM→Host at power-on and in response to a `?I` request
- Fixed payload length: 27 bytes (1 type byte + 26 data bytes)
- Encoded entirely as fixed-width ASCII fields

| Offset | Field             | Size |
| ------ | ----------------- | ---- |
| 0      | Type byte `I`     | 1    |
| 1–6    | ID code           | 6    |
| 7–9    | Protocol revision | 3    |
| 10–16  | Software revision | 7    |
| 17–26  | Serial number     | 10   |

The protocol revision uses the compact wire form from the spec, for example `A20` for
revision `A.02.00`.

```csharp
readonly record struct IdBlock(
    string IdCode,
    string ProtocolRevision,
    string SoftwareRevision,
    string SerialNumber);

static class IdBlockParser
{
    static bool TryParse(ReadOnlySpan<byte> payload, out IdBlock block);
    static bool TryParse(ReadOnlySequence<byte> payload, out IdBlock block);
}
```

### Request block `?`

The request wrapper is a tiny host-originated payload used to ask for a specific block type,
for example `?I` or `?C`.

```csharp
static class RequestBlockEncoder
{
    static int EncodedLength { get; }  // constant 2
    static bool TryEncode(byte requestedType, Span<byte> destination, out int bytesWritten);
}
```

This encoder writes application bytes only: `?` followed by the requested block type.

### Protocol revision change request `V`

The revision change request is a host-originated 4-byte payload used before re-requesting the
ID block.

```csharp
static class ProtocolRevisionChangeRequestEncoder
{
    static int EncodedLength { get; }  // constant 4
    static bool TryEncode(ReadOnlySpan<char> requestedRevision,
                          Span<byte> destination,
                          out int bytesWritten);
}
```

The encoder expects the 3-character wire token from the spec, for example `A20`, and rejects
non-ASCII input or dotted revision strings such as `A.02.00`.

These control-block encoders do not add link-layer framing or CRC; they produce only the
application-layer payload to be passed to `DataBlockWriter`.

### Auto-send control commands `G` and `H`

The auto-send commands are host-originated single-byte payloads.

```csharp
static class GoAutoSendCommandEncoder
{
    static int EncodedLength { get; }  // constant 1
    static bool TryEncode(Span<byte> destination, out int bytesWritten);
}

static class HaltAutoSendCommandEncoder
{
    static int EncodedLength { get; }  // constant 1
    static bool TryEncode(Span<byte> destination, out int bytesWritten);
}
```

These write only the application payload bytes `G` and `H` respectively. Framing and CRC remain
the responsibility of `DataBlockWriter`.

## Maternal fixed-length blocks

### P-Block (Maternal Non-Invasive Blood Pressure)

- Sent FM→Host when a new NIBP measurement is available
- Fixed payload length: 9 bytes (1 type byte + 4 big-endian words)

| Offset | Field              | Size |
| ------ | ------------------ | ---- |
| 0      | Type byte `P`      | 1    |
| 1–2    | Systolic pressure  | 2    |
| 3–4    | Diastolic pressure | 2    |
| 5–6    | Mean pressure      | 2    |
| 7–8    | NIBP maternal HR   | 2    |

The three pressure values are raw mm/Hg values. The heart-rate word uses the same 0.25 bpm
resolution as the CTG and SpO2 maternal HR fields and may also carry the special `0000H` and
`FFFFH` values described by the spec.

### T-Block (Maternal Temperature)

- Sent FM→Host when maternal temperature is available
- Fixed payload length: 2 bytes (1 type byte + 1 data byte)

| Offset | Field           | Size |
| ------ | --------------- | ---- |
| 0      | Type byte `T`   | 1    |
| 1      | Raw temperature | 1    |

The temperature field has 0.1 C resolution with a 25.0 C offset. The parser keeps this as a raw
byte value.

### S-Block (Maternal Oxygen Saturation)

- Sent FM→Host when maternal SpO2 data is available
- Fixed payload length: 4 bytes (1 type byte + `u8 + u16`)

| Offset | Field             | Size |
| ------ | ----------------- | ---- |
| 0      | Type byte `S`     | 1    |
| 1      | Oxygen saturation | 1    |
| 2–3    | SpO2 maternal HR  | 2    |

The oxygen-saturation byte uses the same 0 to 200 raw range described in the spec, representing
0% to 100% at 0.5% resolution. The maternal heart-rate word uses the same 0.25 bpm raw coding as
the NIBP and CTG blocks.

## Test vector strategy

No concrete hex captures exist in the programmer's guide. Tests are synthesised from the
bit-layout tables and cross-verified against the PCDEMO formula.

Reference anchors:

| Scenario                          | High byte | Low byte | Expected parsed value              |
| --------------------------------- | --------- | -------- | ---------------------------------- |
| Blank trace HR1                   | `0x00`    | `0x00`   | `RawValue = 0`                     |
| 60 bpm HR1, no FMP, green quality | `0x60`    | `0xF0`   | `RawValue = 240, Quality = Green`  |
| 300 bpm HR1, green quality (max)  | `0x64`    | `0xB0`   | `RawValue = 1200, Quality = Green` |
| Toco raw 128 (= 64 toco units)    | —         | `0x80`   | `Toco = 0x80`                      |
| FSpO2 = 98%                       | —         | `0x62`   | `FSpO2 = 98`                       |
| All-zero status word              | `0x00`    | `0x00`   | All flags false                    |
