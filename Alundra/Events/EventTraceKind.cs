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
using Alundra.Scripts;

namespace Alundra.Events;

/// <summary>
/// Event-program bytecode interpreter (see docs on <see cref="IEventProgramRunner"/>). <see cref="RunScript"/>
/// is a faithful port of AlundraEngine.Gameplay.Scripts.EntityEventHandlers.RunScript @ 0x8004205c,
/// including its per-slot resume policy (EntityEventHandlers.cs:239-296) and <c>g_clearProgramState</c>
/// END_SCRIPT handling (:343-391) - see that method's own doc for the port's one documented
/// simplification. <see cref="RunSpriteEvent"/> stays a counted no-op (porting the ~120 native sprite AI
/// handlers, SpriteEventHandlers.cs, is a later chantier - E14).
///
/// Slots B (Map) and C (Tick) are the only two the original ever RESUMES across calls, off the entity's
/// own persisted <see cref="AlundraEntityScriptProxy.EventProgramState"/> - every other slot (A/D/E/F)
/// always falls into <see cref="InitializeEventData"/>, using a scratch state shared by all four
/// (<see cref="_slotAScratchState"/>, mirroring the original's single <c>g_eventProgramState</c>). A slot
/// A/D/E/F program that suspends (returns 0, e.g. via 0x37 Wait) therefore never resumes where it left
/// off on a LATER call for a DIFFERENT entity/slot (they all share the one scratch state) - faithful to
/// the original, not a V1 shortcut. Combined with the pick phase only ever offering slot A once per
/// entity (the Loaded -&gt; Normal transition happens in the same pick pass that chose slot A - see
/// <see cref="AlundraEntityScriptProxy.PickEventTrigger"/>), a Load program that suspends simply never
/// resumes at all: it stops there for good. Map 389's own Load programs never hit a suspending opcode, so
/// this never fires there in practice.
///
/// The 0x80 bit of a program index (checked in <see cref="InitializeEventData"/>) selects, in the
/// original, between the current map's own event-code table and the global <c>AlundraMap</c>
/// (<c>map_alundra</c>) table shared by every map (portrait/HUD-adjacent sprite data, not gameplay
/// entity Load/Tick/etc. programs - see EntityEventHandlers.cs:423-428). This runtime has no such
/// global table yet (only the current map's own events document is loaded - see
/// <see cref="MapEventProgramLoader"/>), so an index with the bit clear degrades: logged once, then
/// falls back to using the current map's table anyway. Every real map-389 Load program index observed
/// (133-145) carries the bit set, so this fallback path is not expected to fire during normal play.
/// </summary>
public enum EventTraceKind
{
    Implemented,
    Degraded,
    UnknownSkipped,
    UnknownNoSizeTerminated,
    End,
    Break,

    /// <summary>The loop guard ended this script call after too many dispatched opcodes without reaching
    /// 0xFF/0x00/a suspend (E19.a T4, D-E19-3, docs/plan-e19-opcodes.md): the budget is
    /// <see cref="EventProgramRunner.MaxIterationsPerCall"/> when set, else
    /// <see cref="EventProgramRunner.ProductionLoopBudget"/> - so this kind IS produced in production, and
    /// logged once per program. A documented deviation from the original, which has no guard (its loops always
    /// contain an opcode that suspends): almost always an unported suspending opcode (skipped instead of
    /// suspending) sitting inside a loop that never exits. The record carries <c>Codes[CodeIndex]</c>, the opcode
    /// the call was about to read.</summary>
    LoopBudgetExceeded,
}
