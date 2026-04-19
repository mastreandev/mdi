# Philips M1350

This section documents Philips M1350 data block encoding and decoding support in MDI.

## Overview

The Philips M1350 area is organised by protocol layer and supporting workflows rather than by
workspace inventory. Use these docs for protocol semantics, transport behavior, replay shapes,
and simulator intent; the concrete type and file inventory is easier to inspect directly in the
workspace.

## Current scope

- **Implemented:** block framing, DLE escaping, CRC generation and validation, stream parsing primitives (`DataBlockReader`, `DataBlockWriter`), application-layer support for `C`, `F`, `I`, `MM`, `N`, `P`, `S`, `T`, `G`, `H`, `?`, and `V`, host-originated note encoding for `N`, the complete synchronous session facade for the currently implemented block set, and the first async session slice centered on `PipeReader`, `IDuplexPipe`, and stream entry points, `IAsyncEnumerable<M1350Message>` output, async request/workflow methods, and flush-aware async command writes
- **Implemented:** `M1350Monitor` as the higher-level monitor facade above the current pipe-based session API, including startup orchestration, a background receive loop, a retained snapshot, and an async update stream for inbound and monitor-owned state transitions
- **Planned:** additional higher-level workflow helpers only if repeated transport or session orchestration patterns still prove necessary

## Documents

- [Data Link Layer](data-link-layer.md)
- [Hashing](hashing/README.md)
- [Application Layer](application-layer.md)
- [Session Layer](session-layer.md)
- [Monitor Layer](monitor-layer.md)
- [Monitor Integration Shapes](monitor-integration-shapes.md)
- [Replay Capture Shapes](replay-capture-shapes.md)
- [Replay Instrumentation](replay-instrumentation.md)
- [Simulator and FM Origination](simulator-and-fm-origination.md)
