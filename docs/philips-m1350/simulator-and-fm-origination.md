# Philips M1350 Simulator and FM Origination

This document captures the current design position on monitor-originated block construction,
simulator support, and where that functionality should live if it is added.

## Current library role

Today, the Philips M1350 support in MDI is intentionally host-role-first:

1. it parses blocks that originate from the fetal monitor (FM)
2. it encodes blocks and commands that originate from the host
3. it provides session helpers for host-driven request, negotiation, and command flows

That means the current public surface is deliberately asymmetric in one important sense:

1. Host -> FM blocks that the library must send in real use have encoders
2. FM -> Host blocks that the library must receive in real use have parsers

This is a product-boundary choice, not a statement that the protocol itself is asymmetric.

## Protocol symmetry versus library symmetry

At the wire level, the protocol is more symmetric than the current library surface:

1. the host sends requests, notes, revision changes, and control commands
2. the monitor sends identity, CTG, maternal measurements, failures, event markers, and notes

So the question "if Host -> FM blocks have encoders, should FM -> Host blocks have encoders too?"
is reasonable.

The answer depends on the job of the library.

If the library remains focused on the host role, then parser coverage for FM-originated blocks is
enough. If the library also needs to model the monitor role, then FM-originated encoding becomes a
real first-class capability.

## When FM-originated encoders become justified

FM-originated encoders should be extracted when they are needed by a real reusable capability,
not just because a unit test contains some byte offsets.

The clearest triggers are:

1. a monitor simulator
2. reusable integration-test harnesses
3. protocol scenario fixtures that model monitor behavior across multiple tests or tools
4. emulation support for development environments that do not have real hardware attached

Once one of those exists, the library is no longer just encoding host commands. It is also
constructing monitor-originated protocol frames intentionally, and the asymmetry stops being the
right abstraction boundary.

## What should not drive extraction

The following reasons are not enough on their own:

1. a private test helper looks low-level
2. a fixed-layout FM block is awkward to build by hand in one test file
3. the protocol has a pleasing conceptual symmetry

Those are signals to consider refactoring test scaffolding, but not necessarily signals to expand
the main public API.

## Recommended layering

If simulator or monitor-role support is added, keep it explicit in the namespace and package
structure rather than quietly folding it into the current host-facing surface.

Suggested layering:

1. `MDI.Philips.M1350`
   Host-facing session API, shared message types, data-link primitives
2. `MDI.Philips.M1350.Application`
   Shared parsers plus host-role command/request encoders
3. `MDI.Philips.M1350.Simulator`
   FM-originated encoders, scenario builders, and reusable monitor-behavior helpers

This keeps the intent readable:

1. the base library remains honest about its primary role
2. simulator/emulation code can grow without distorting the production host API
3. tests can share simulator builders without forcing all consumers to treat them as core APIs

## Likely simulator responsibilities

If a simulator layer is introduced, it would likely own some or all of the following:

1. encode `I` blocks from `IdBlock`
2. encode `C` blocks from `CtgBlock`
3. encode `P`, `S`, `T`, `F`, `MM`, and possibly inbound `N` blocks
4. build framed monitor output streams for scenario playback
5. produce canned startup sequences such as power-on identity followed by requested data blocks
6. emit auto-send sequences for CTG streaming behavior

That is a coherent capability set. It is also distinct from the host command API.

## Current executable host

The simulator now also has a runnable executable entrypoint in
`source/MDI.Philips.M1350.Simulator`.

Its current transport shape is intentionally minimal:

1. it reads framed host commands from standard input
2. it writes framed monitor output to standard output
3. it supports a small startup profile through command-line options for identity, CTG baseline,
   scenario selection, auto-send interval, and whether to emit the unsolicited power-on identity
   block

This keeps the first host implementation reusable and scriptable while leaving room for a later
TUI or richer scenario runner on top of the same simulator engine.

## Current scenario direction

The simulator now has a small set of deterministic CTG scenarios rather than a single hard-coded
waveform profile.

The current intent is:

1. `baseline` for a stable normal trace
2. `fhr-rise` for a synthetic fetal-heart-rate rise profile
3. `fhr-drop` for a synthetic fetal-heart-rate drop profile
4. `toco-rise` for a synthetic toco-rise profile with modest HR response

These profiles are still synthetic, but they avoid implying clinical interpretation while still
providing a better development surface for host-side testing than repeating one fixed CTG block
forever.

## Replay capture boundary

If the library grows a compact replay/export path for real fetal-monitor traffic, the best capture
boundary is the parsed message stream exposed by `M1350Session.ReadAllAsync(...)`, not the raw
data-link layer and not the retained-state `M1350Monitor` snapshot layer.

Why that layer:

1. raw framed blocks are faithful, but too transport-specific and noisy for compact reusable replay
2. `M1350Monitor` snapshots collapse time and overwrite repeated values, so they lose the ordered
   transition stream needed for replay
3. `M1350Session` already exposes the ordered typed inbound stream before state retention or policy
   decisions are applied

That suggests a compact replay record shaped roughly as:

1. protocol/session metadata such as negotiated revision and timing origin
2. an ordered sequence of `(delta-from-previous, typed message payload)` entries
3. optional export adapters that rehydrate those entries into simulator scenarios or exact message
   playback streams

So the monitor library should likely capture at the session/message layer, and the simulator should
later consume that capture format rather than inventing a second replay abstraction above monitor
snapshots.

## Short-term guidance

Until simulator support is a real goal, keep monitor-originated payload construction as test-only
scaffolding where needed.

Good short-term options:

1. private helpers such as `BuildIdentityPayload(IdBlock block)` in test files
2. small internal test builders shared across Philips M1350 tests
3. no new public encoder types for FM-originated blocks yet

## Long-term guidance

When simulator work begins, prefer extracting FM-originated encoding into a dedicated simulator or
testing layer rather than treating it as a side effect of parser work.

That gives the project both forms of symmetry:

1. protocol symmetry at the modeling level
2. architectural clarity at the API level
