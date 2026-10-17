#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Microsoft.Xna.Framework;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;

namespace Alundra.Events;

/// <summary>One dispatched opcode (or program-boundary event), reported to <see cref="AlundraEventProgramRunner.TraceSink"/>
/// - see that property own doc. Read-only trace record, never allocated on the null-sink path.
/// <see cref="State"/> is the live <see cref="EventProgramState"/> being executed (a reference,
/// not a copy - no extra allocation) so a trace-mode sink can inspect or mutate it (e.g. the intro
/// trace harness own optimistic-predicate deviation, which sets <c>State.Result = 1</c> for a
/// handful of skipped predicate opcodes - see IntroTraceHarnessTests own class doc, section 0, for
/// the rationale). Never mutated by this runner itself outside of normal opcode handling.</summary>
public readonly record struct EventTraceRecord(
    int ProgramSlot,
    int CodeIndex,
    int Opcode,
    EventTraceKind Kind,
    int Size,
    byte[]? Parameters,
    EventProgramState State);
