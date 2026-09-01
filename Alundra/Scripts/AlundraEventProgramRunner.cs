#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.AI.Navigation;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

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

    /// <summary>Diagnostic-only kind, never produced in production: <see cref="AlundraEventProgramRunner.MaxIterationsPerCall"/>
    /// forcibly ended this script call after too many dispatched opcodes without reaching 0xFF/0x00/a
    /// suspend - almost always an unimplemented suspending opcode (e.g. 0x35/0x36 wait-flag, skipped
    /// instead of suspending - see this runner's own class doc) sitting inside a Goto loop that never
    /// exits. Diagnostic only - not a fidelity concern for slot A (the only slot production code
    /// actually interprets), which never hits this in practice.</summary>
    LoopBudgetExceeded,
}

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

public sealed class AlundraEventProgramRunner : IEventProgramRunner
{
    public int ScriptRunCount { get; private set; }
    public int SpriteEventRunCount { get; private set; }

    /// <summary>
    /// Trace seam for the headless intro trace harness (Alundra.Tests/IntroTraceHarnessTests.cs) - null
    /// by default (zero cost: a single null-check per dispatched opcode, no allocation) and never set by
    /// production code. When non-null, <see cref="RunOneScriptCall"/> reports every dispatched opcode
    /// (after <see cref="Dispatch"/>, so its <see cref="EventTraceKind"/> is known) plus the 0xFF/0x00
    /// program-boundary terminations, so a caller can reconstruct the exact linear "skip path" the
    /// interpreter took without re-implementing any dispatch logic of its own.
    /// </summary>
    internal Action<EventTraceRecord>? TraceSink { get; set; }

    /// <summary>Diagnostic-only safety valve, null (unlimited) by default - unused, hence no behavior
    /// change, unless a caller (the intro trace harness) explicitly sets it. When set,
    /// <see cref="RunOneScriptCall"/> forcibly ends a script call after this many dispatched opcodes
    /// without reaching a natural end/suspend, reporting <see cref="EventTraceKind.LoopBudgetExceeded"/>
    /// through <see cref="TraceSink"/> instead of hanging forever - see that trace kind's own doc.</summary>
    internal int? MaxIterationsPerCall { get; set; }

    /// <summary>
    /// This world's own local dialogue-string table (E12.a, docs/plan-e12-dialogues.md §1.5) - loaded by
    /// <see cref="AlundraWorldProxy.InitializeWithWorld"/> via <see cref="AlundraDialogueStringsLoader"/>
    /// and handed here as a plain settable property (rather than a constructor parameter) so every
    /// EXISTING call site of this runner's constructor (17 of them across this DLL and its tests) keeps
    /// compiling unmodified - null means "not loaded/unavailable", the same degraded fallback opcode
    /// 0x0D's own <c>textId &amp; 0x80</c> branch already has for an out-of-range index.
    /// </summary>
    internal IReadOnlyList<string>? LocalDialogueStrings { get; set; }

    private bool _loggedSharedDialogTableOnce;

    private readonly EventProgramDocument? _document;
    private readonly byte[]? _codes;
    private readonly AlundraGameState _gameState;
    private readonly IEntityWorldContext _worldContext;

    private readonly HashSet<int> _loggedUnknownOpcodes = new();
    private readonly HashSet<int> _loggedDegradedOpcodes = new();
    private readonly HashSet<int> _loggedFailedActivations = new();
    private bool _loggedNoDocument;
    private bool _loggedGlobalTableFallback;
    private bool _loggedSpriteEventOnce;

    /// <summary>
    /// Shared scratch <see cref="EventProgramState"/> for every NON-RESUMING slot - A (Load), D (Touch),
    /// E (Deactivate) and F (Interact) - the original never gives any of these their own per-entity state,
    /// they all run off the SAME <c>_gameEngine.StaticVariables.g_eventProgramState</c>
    /// (EntityEventHandlers.cs:234), one instance shared across every call to any of the four (only B/Map
    /// and C/Tick resume the entity's own persisted <see cref="AlundraEntityScriptProxy.EventProgramState"/>
    /// instead - see <see cref="RunScript"/>). Reused (not reconstructed) by every such call so that
    /// <see cref="EventProgramState.Result"/> - never cleared by <see cref="InitializeEventData"/> - carries
    /// across sequential calls exactly like the original: a Load program that starts with a conditional
    /// opcode reading a <c>Result</c> a previous, unrelated A/D/E/F call happened to leave behind sees that
    /// same stale value. A V1 deviation only in that this runtime has one runner (hence one shared state)
    /// per world rather than one process-wide global, which does not matter in practice since only one
    /// world is ever interpreting these programs at a time.
    /// </summary>
    private readonly EventProgramState _slotAScratchState = new();

    public AlundraEventProgramRunner(EventProgramDocument? document, AlundraGameState gameState, IEntityWorldContext? worldContext = null)
    {
        _document = document;
        _codes = document?.CodesAsBytes();
        _gameState = gameState;
        _worldContext = worldContext ?? NoOpEntityWorldContext.Instance;
    }

    /// <summary>
    /// Full port of the slot resume/reset policy of <c>EntityEventHandlers.RunScript</c> @ 0x8004205c
    /// (EntityEventHandlers.cs:232-296) and its END_SCRIPT <c>g_clearProgramState</c> handling (:343-358,
    /// :382-391).
    ///
    /// Documented simplification on <c>g_clearProgramState</c>: the original re-checks the flag after
    /// EVERY dispatched opcode (not just once at the end) and, when set, distinguishes clearing the
    /// CURRENTLY RUNNING entity's own state (deferred to this call's own END_SCRIPT, via
    /// <c>wasEntityCleared</c>) from clearing a DIFFERENT entity's <c>LogicContextEntity.EventProgramState</c>
    /// immediately, inline, mid-loop. No opcode this runner implements ever sets the flag yet - only
    /// <c>Script_64_040</c> (0x40, EntityEventHandlers.cs:1332-1338) and one <c>FunctionTypeC</c> handler do,
    /// neither ported - so this port only re-checks <see cref="ClearProgramStateRequested"/> once, after
    /// <see cref="RunOneScriptCall"/> returns, and only ever clears the state THIS call actually ran
    /// (<paramref name="programSlot"/>'s own <c>state</c>). The self-vs-other distinction is deferred to
    /// whichever future task ports opcode 0x40.
    /// </summary>
    public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
    {
        ScriptRunCount++;

        if (_document == null || _codes == null)
        {
            if (!_loggedNoDocument)
            {
                _loggedNoDocument = true;
                Logs.WriteWarning(
                    "AlundraEventProgramRunner: no event-code document loaded for this world; RunScript "
                    + "is a counted no-op (degraded mode).");
            }

            return;
        }

        EventProgramState state;
        var resumed = false;

        switch (programSlot)
        {
            case ScriptHelper.ProgramBMap:
                // EntityEventHandlers.cs:242-249.
                state = entity.EventProgramState;
                if (state.Codes != null)
                {
                    resumed = true;
                }

                break;

            case ScriptHelper.ProgramCTick:
                // EntityEventHandlers.cs:251-264.
                state = entity.EventProgramState;
                if (state.Codes != null)
                {
                    if (entity.MapEventProgramId != ScriptHelper.ProgramCTick)
                    {
                        entity.TargetAnimationId = entity.LastTargetAnimationId;
                        entity.TargetDirection = entity.LastTargetDirection;
                    }

                    resumed = true;
                }

                break;

            case ScriptHelper.ProgramFInteract:
                // EntityEventHandlers.cs:266-273 - always zeroes the PLAYER's own forces, regardless of
                // which entity is actually running this slot F program.
                if (_worldContext.PlayerEntity is { } player)
                {
                    player.ForceStepY = 0;
                    player.ForceStepX = 0;
                    player.ForceY = 0;
                    player.ForceX = 0;
                }

                goto default;

            default: // A (Load), D (Touch), E (Deactivate), and F falling through from above.
                // EntityEventHandlers.cs:275-279.
                if (entity.MapEventProgramId == ScriptHelper.ProgramCTick)
                {
                    entity.LastTargetAnimationId = entity.TargetAnimationId;
                    entity.LastTargetDirection = entity.TargetDirection;
                }

                state = _slotAScratchState; // shared scratch for A/D/E/F - see its own doc.
                break;
        }

        if (!resumed)
        {
            InitializeEventData(entity, programSlot, state);
        }

        // EntityEventHandlers.cs:297 (SET_LOGIC_MODE) - both the resumed and re-initialized paths reach
        // this same assignment next, in the original.
        entity.MapEventProgramId = programSlot;

        ClearProgramStateRequested = false;
        RunOneScriptCall(entity, state, programSlot);

        if (ClearProgramStateRequested)
        {
            ClearProgramStateRequested = false;
            state.Sp = 0;
            state.Codes = null;
        }
    }

    /// <summary>
    /// Port of <c>StaticVariables.g_clearProgramState</c> - see <see cref="RunScript"/>'s own doc on this
    /// port's END_SCRIPT simplification. No opcode implemented by this runner sets this yet; it exists so
    /// a future opcode 0x40 port does not also need a <see cref="RunScript"/> change.
    /// </summary>
    internal bool ClearProgramStateRequested;

    public void RunSpriteEvent(AlundraEntityScriptProxy entity)
    {
        SpriteEventRunCount++;

        if (!_loggedSpriteEventOnce)
        {
            _loggedSpriteEventOnce = true;
            Logs.WriteDebug(
                "AlundraEventProgramRunner: RunSpriteEvent (native sprite AI, "
                + "g_entityEventFunctionsByType) not interpreted in V1 - counted no-op.");
        }
    }

    /// <summary>Port of EntityEventHandlers.InitializeEventData @ 0x80041ee4 for slot A - see class doc
    /// on the 0x80 bit fallback. Internal for direct unit testing of the A-table &amp; 0x7f resolution.</summary>
    internal void InitializeEventData(AlundraEntityScriptProxy entity, int programSlot, EventProgramState state)
    {
        var programIndex = entity.ProgramIndexes[programSlot];

        if ((programIndex & 0x80) == 0 && !_loggedGlobalTableFallback)
        {
            _loggedGlobalTableFallback = true;
            Logs.WriteDebug(
                $"AlundraEventProgramRunner: program index {programIndex} has bit 0x80 clear (original "
                + "would read the global AlundraMap/map_alundra table, not ported); falling back to the "
                + "current map's table.");
        }

        var table = _document!.TableFor(programSlot);
        var maskedIndex = programIndex & 0x7f;

        if (programIndex >= 0 && maskedIndex < table.Length)
        {
            state.CodeIndex = table[maskedIndex];
        }

        Array.Clear(state.Parameters);
        state.Codes = _codes;

        if (_codes == null || _codes.Length == 0 || state.CodeIndex >= _codes.Length)
        {
            state.Sp = 0xFF;
        }
        else
        {
            state.Sp = _codes[state.CodeIndex];
        }

        state.Parameters[0] = state.CodeIndex;
    }

    /// <summary>Core fetch/dispatch/advance loop, port of the inner while loop of
    /// EntityEventHandlers.RunScript @ 0x8004205c (EntityEventHandlers.cs:304-377) - the command==0xFF
    /// and command==0x00 special cases, then <c>CodeIndex += result</c> for everything else, with 0
    /// meaning "suspend/end this call". Internal so synthetic interpreter tests can drive an explicit,
    /// reused <see cref="EventProgramState"/> across multiple calls (e.g. to exercise 0x37 Wait's
    /// multi-frame suspend/resume) independently of slot A's own always-fresh-state policy in
    /// <see cref="RunScript"/> above.</summary>
    internal void RunOneScriptCall(AlundraEntityScriptProxy entity, EventProgramState state, int programSlot = -1)
    {
        var iterations = 0;

        while (true)
        {
            if (MaxIterationsPerCall is { } budget && ++iterations > budget)
            {
                TraceSink?.Invoke(new EventTraceRecord(programSlot, state.CodeIndex, state.Sp, EventTraceKind.LoopBudgetExceeded, 0, null, state));
                return;
            }

            var codeIndexAtFetch = state.CodeIndex;
            var variables = FillDataFromCommand(state);
            var command = variables[0];

            if (command == 0xFF)
            {
                TraceSink?.Invoke(new EventTraceRecord(programSlot, codeIndexAtFetch, command, EventTraceKind.End, 0, null, state));
                return;
            }

            if (command == 0x00)
            {
                state.Parameters[1] = 0;
                state.CodeIndex++;
                TraceSink?.Invoke(new EventTraceRecord(programSlot, codeIndexAtFetch, command, EventTraceKind.Break, 1, null, state));
                return;
            }

            _lastDispatchKind = EventTraceKind.Implemented;
            var result = Dispatch(command, entity, variables, state);

            if (TraceSink != null)
            {
                EventOpcodeSizeTable.Entries.TryGetValue((byte)command, out var entry);
                var instructionSize = entry?.Size ?? 0;
                byte[]? parameters = null;
                if (instructionSize > 1)
                {
                    var count = Math.Max(0, Math.Min(instructionSize - 1, variables.Length - 1));
                    parameters = new byte[count];
                    for (var i = 0; i < count; i++)
                    {
                        parameters[i] = (byte)variables[i + 1];
                    }
                }

                TraceSink.Invoke(new EventTraceRecord(programSlot, codeIndexAtFetch, command, _lastDispatchKind, result, parameters, state));
            }

            if (result == 0)
            {
                return;
            }

            state.Parameters[1] = 0;
            state.CodeIndex += result;
        }
    }

    private EventTraceKind _lastDispatchKind = EventTraceKind.Implemented;

    /// <summary>
    /// Scratch buffer for <see cref="FillDataFromCommand"/> - deviation from the original AND from this
    /// port's own prior V1 shape: the decompiled <c>FillDataFromCommand</c> (EntityEventHandlers.cs:400-417)
    /// itself allocates its own local array every call too (the PSX C compiler just stack-allocates it, no
    /// GC pressure there), but the repo's own no-per-frame-allocation rule applies here regardless, since
    /// every Tick now calls this at least once per dispatched opcode (see <see cref="AlundraEntityScriptProxy.Update"/>).
    /// One instance per runner (not per <see cref="EventProgramState"/>): <see cref="RunOneScriptCall"/>
    /// always runs a single state to completion synchronously before this runner is free to fetch for any
    /// other state, so there is never more than one in-flight fetch to share a buffer across.
    /// </summary>
    private readonly int[] _fetchScratch = new int[10];

    /// <summary>Port of EntityEventHandlers.FillDataFromCommand (EntityEventHandlers.cs:400-417) - see
    /// <see cref="_fetchScratch"/>'s own doc for the one allocation deviation from the original.</summary>
    internal int[] FillDataFromCommand(EventProgramState state)
    {
        if (state.Codes == null || state.CodeIndex >= state.Codes.Length)
        {
            _fetchScratch[0] = 0xFF;
            return _fetchScratch;
        }

        state.Sp = state.Codes[state.CodeIndex];
        var length = Math.Min(10, state.Codes.Length - state.CodeIndex);
        for (var i = 0; i < length; i++)
        {
            _fetchScratch[i] = state.Codes[state.CodeIndex + i];
        }

        // Zero the tail past what was actually read this fetch - a shared, reused buffer must not leak a
        // PREVIOUS fetch's trailing bytes when this one has fewer than 10 remaining (matching the
        // zero-initialized "new int[10]" semantics the old per-call allocation gave for free).
        for (var i = length; i < _fetchScratch.Length; i++)
        {
            _fetchScratch[i] = 0;
        }

        return _fetchScratch;
    }

    private int Dispatch(int command, AlundraEntityScriptProxy entity, int[] v, EventProgramState state)
    {
        switch (command)
        {
            case 0x01: // Do nothing (debug command) - Script_DoNothing
                return 1;

            case 0x02: // Goto - Script_2_002
                return SignExtend16((v[2] << 8) | v[1]);

            case 0x03: // If true goto - Script_3_003
                return state.Result != 0 ? SignExtend16((v[2] << 8) | v[1]) : 3;

            case 0x04: // If false goto - Script_4_004
                return state.Result == 0 ? SignExtend16((v[2] << 8) | v[1]) : 3;

            case 0x05: // Flag on - Script_5_005
            {
                var flag = (uint)((v[2] << 8) | v[1]);
                var mask = (uint)(1 << (v[1] & 0x1f));
                _gameState.AddFlag(flag, mask);
                return 3;
            }

            case 0x06: // Flag off - Script_6_006
            {
                var flag = (uint)((v[2] << 8) | v[1]);
                var mask = (uint)(1 << (v[1] & 0x1f));
                _gameState.SetFlag(flag, ~mask);
                return 3;
            }

            case 0x07: // Check entity in area - Script_7_007 (EntityEventHandlers.cs:539-582): Result = 1
                       // if ANY entity matched by v1's search type has TileX/TileY/TileZ inside the
                       // inclusive box v2..v7, else 0. The original iterates the match buffer BACKWARD
                       // (last match down to the first) - immaterial here since only "any match" is
                       // observed, not which one; ported forward over EntitySearchService's own order.
                state.Result = EntityInArea(entity, v) ? 1 : 0;
                return 8;

            case 0x09: // Set direction - Script_9_009
                entity.TargetDirection = (uint)(v[1] & 0x1f);
                return 2;

            case 0x0A: // Reverse direction - Script_10_00A
                entity.TargetDirection = (entity.TargetDirection + 0x10) & 0x1f;
                return 1;

            case 0x10: // Player lose control - Script_16_010 (EntityEventHandlers.cs:680-684). The flag
                       // store IS the whole port: AlundraPlayerManager.MovePlayer's own InputBlockedMask
                       // gate reads it, at the same site the original tests it (PlayerManager.cs:38).
                       // E6 decision E6-1 deliberately declines an engine-level input cut - see
                       // AlundraGameState.PlayerControlFlags' own doc.
                _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
                return 1;

            case 0x11: // Player gain control - Script_17_011 (EntityEventHandlers.cs:686-690).
                _gameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.ControlLocked;
                return 1;

            case 0x16: // High gravity - Script_22_016
                entity.Flags |= EntityFlags.Gravity;
                entity.ResyncControllerFromFlags();
                return 1;

            case 0x17: // Low gravity - Script_23_017
                entity.Flags &= ~EntityFlags.Gravity;
                entity.ResyncControllerFromFlags();
                return 1;

            case 0x19: // Deactivate entity - Script_25_019 (EntityEventHandlers.cs:729-733).
                entity.Status = EntityStatus.Deactivated;
                return 1;

            case 0x1A: // Set anim - Script_26_01A
                entity.TargetAnimationId = (uint)v[1];
                return 2;

            case 0x1E: // Walk - Script_30_01E (EntityEventHandlers.cs:793-829): see this case's own doc
                       // on Walk below for the full port + E4.d navigation-detour extension (D5). Sets
                       // NEITHER anim NOR direction by itself (0x5A/0x5B do - free walk comes from the
                       // permanent physics tick, AlundraScriptedMotion).
                return Walk(entity, v, state, allowDetour: true);

            case 0x1F: // Walk with collision - Script_31_01F (EntityEventHandlers.cs:832-841): delegates
                       // to the SAME 0x1E core (Walk below, allowDetour: false - D5: no navigation
                       // detour for 0x1F, the original itself just ends on a curtailed movement), but ALSO
                       // ends once ForceAdjusted becomes nonzero this frame - faithful (the original tests
                       // the frame's own ForceAdjusted; this port tests the last completed sub-step's own
                       // curtailment instead, see AlundraEntityScriptProxy.ForceAdjusted's own doc -
                       // documented deviation, plan §3 E4.d).
                return Walk(entity, v, state, allowDetour: false) == 0 && entity.ForceAdjusted == 0 ? 0 : 3;

            case 0x1B: // Fly - Script_27_01B (EntityEventHandlers.cs:743-747): ForceZ = (((v2<<8)|v1) *
                       // 0x10000) >> 8, a signed 16.16 vertical impulse. Only the DLL-side struct field is
                       // set here now (root-cause vertical-fidelity fix, gull entity 6 map 389 - see
                       // AlundraEntityScriptProxy.EvaluateEntitySupport's own doc for the full
                       // investigation): a controller-driven NPC's vertical is driven ENTIRELY by that
                       // method's own per-tick decay + Controller.Move() displacement, read from ForceZ at
                       // the head of the very next EvaluateEntitySupport call this same tick (RunPickedEvent
                       // runs before AlundraScriptedMotion.TickScriptedNpc/EvaluateEntitySupport in the
                       // same per-tick loop - see AlundraEntityScriptProxy.Update's own doc). This opcode
                       // used to ALSO push the raw (pre-decay) impulse straight onto
                       // CharacterControllerComponent.SetVerticalVelocity, letting CharacterMotionSystem's
                       // own per-RENDER-FRAME integrator move the entity a SECOND, independent time this
                       // same tick before EvaluateEntitySupport's own Move() ran - real-time-integrated,
                       // exactly the render-rate-dependent bug this fix removes (and, once
                       // EvaluateEntitySupport also stopped consuming that pre-set velocity for real motion,
                       // simply a redundant write immediately about to be zeroed the same frame). Without a
                       // controller (bare-fallback spawn, or the intro trace harness - E4.e still owns that
                       // simulated kinematics), ForceZ alone is what ever mattered anyway.
                entity.ForceZ = SignExtend16(((v[2] << 8) | v[1])) * 0x10000 >> 8;
                return 3;

            case 0x27: // Face player - Script_39_027 (EntityEventHandlers.cs:973-978): TargetDirection =
                       // GetDirectionToTarget(player.Pos - entity.Pos). No player spawned this session
                       // (degraded mode) -> TargetDirection left unchanged, same "nothing to search"
                       // shape every other player-dependent path in this runner already falls back to.
                if (_worldContext.PlayerEntity is { } faceTarget)
                {
                    entity.TargetDirection = ScriptHelper.GetDirectionToTarget(faceTarget.PosX - entity.PosX, faceTarget.PosY - entity.PosY);
                }

                return 1;

            case 0x30: // If flag on - Script_48_030
                return FlagBranch(v, wantSet: true);

            case 0x31: // If flag off - Script_49_031
                return FlagBranch(v, wantSet: false);

            case 0x37: // Wait - Script_55_037
                return Wait(v, state);

            case 0x36: // Wait until flag on - Script_54_036 @ 0x8003E35C (EntityEventHandlers.cs:1166-1176);
                       // EventCodeDebugger/EventOpcodeSizeTable names this "Wait flag off", which is misleading -
                       // it returns 3 (advance) when the flag bit IS SET, and 0 (suspend) when it is clear.
                return WaitUntilFlagOn(v);

            case 0x2D: // Activate entity - Script_45_02D
                ActivateEntity(entity, v);
                return 2;

            case 0x2E: // Destroy entity - Script_46_02E
                DestroyMatchingEntities(entity, v, state);
                return 2;

            case 0x33: // Check flags on - Script_51_033
                return CheckFlagsOn(v, state);

            case 0x38: // Set save map-id -> internal map index - Script_SetSaveMapIdToInternalMapIndex_038
                       // (EntityEventHandlers.cs:1202-1207): MapIdToInternalMapIndexTable[v2<<8|v1] =
                       // v4<<8|v3. Real map-389 operand (docs/intro-programs-389.txt offset 305):
                       // params=[17,0,183,1] -> table[17] = 439.
                SetSaveMapIdToInternalMapIndex(v);
                return 5;

            case 0x0D: // Dialog - Script_OpenDialog_13_00D (E12.a, docs/plan-e12-dialogues.md): opens a
                       // dialogue box. v[1]=textId (bit 0x80 -> LOCAL Strings[textId&0x7F], clear -> the
                       // SHARED map_alundra table, not exported by the converter yet - degrades, once
                       // warned); v[2]=controlMode (1 -> MessageBox, 0 -> MenuOpen, see
                       // AlundraDialogueDirector.Open's own doc). Dispatch itself owns the reentrancy
                       // guard (T2): a dialogue already open makes this retry (return 0) rather than
                       // stomping a second one open.
                return OpenDialog(v[1], v[2], instructionSize: 3, opcode: 0x0D, opcodeName: "Dialog");

            case 0x39: // Wait for dialog - Script_59_039 (E12.a): blocking gate, NOT a predicate - writes
                       // no Result either way. Returns 0 while a dialogue is open, advances (1) once
                       // CLOSED.
            {
                var waitDirector = _worldContext.DialogueDirector;
                if (waitDirector == null || !waitDirector.HasPresenter)
                {
                    LogDegradedOpcodeOnce(0x39, "WaitForDialog", "dialogue presenter");
                    return 1;
                }

                // PURE POLL, like the original's Script_IsDialogInProgress_039 - the box's own
                // advance/close pass runs once per logic tick from AlundraWorldProxy.Update (see the
                // F1 comment there), NOT from this opcode: six of the seven sailors open their box
                // from a program with no 0x39 at all.
                return waitDirector.IsOpen ? 0 : 1;
            }

            case 0x44: // Wait dialog choice - Script_WaitDialogChoice_44 (E12.a): the CHOICE, WITH STATE.
                       // First entry (not yet awaiting a choice) opens OUI/NON (labels = GLOBAL strings
                       // via the ETC index table, D-E12-6 - never local/map strings) and blocks; once the
                       // player selects, Result = 1 iff the FIRST option, else 0, and this instruction
                       // finally advances (1). Degraded (no presenter, or no etc-index/global-strings
                       // data): Result = 1 unconditionally and advances immediately - the old
                       // optimistic-forcing behaviour the harness used to apply by hand (§1.6/item ⑦),
                       // now a real, documented degraded mode so this predicate can never deadlock a
                       // script with no dialogue system installed.
            {
                var choiceDirector = _worldContext.DialogueDirector;
                if (choiceDirector == null || !choiceDirector.HasPresenter)
                {
                    state.Result = 1;
                    LogDegradedOpcodeOnce(0x44, "WaitDialogChoice", "dialogue presenter");
                    return 1;
                }

                if (!choiceDirector.IsAwaitingChoice)
                {
                    if (!AlundraEtcStringTable.TryResolveYesNo(EngineEnvironment.ProjectPath, out var yesLabel, out var noLabel))
                    {
                        state.Result = 1;
                        LogDegradedOpcodeOnce(0x44, "WaitDialogChoice", "etc-index/global-strings data");
                        return 1;
                    }

                    choiceDirector.OpenChoice(new[] { yesLabel, noLabel });
                    return 0;
                }

                var choiceResult = choiceDirector.TakeChoiceResult();
                if (choiceResult == null)
                {
                    return 0;
                }

                state.Result = choiceResult.Value;
                return 1;
            }

            case 0x50: // Set dialog choice (misnomer, §1.3 - really sets the CLOSE-MODE mask) -
                       // Script_SetDialogChoice_50 (E12.a): bit0 auto-timer (360 ticks), bit1 button,
                       // bit2 script (0x51). No presenter needed to just remember the mask value.
                _worldContext.DialogueDirector?.SetCloseMask(v[1]);
                return 2;

            case 0x51: // Get dialog choice (misnomer, §1.3 - really a script-close REQUEST) -
                       // Script_GetDialogChoice_51 (E12.a): honoured only while the mask's bit2 is set
                       // (AlundraDialogueDirector.RequestScriptClose's own doc). No Result either way.
                _worldContext.DialogueDirector?.RequestScriptClose();
                return 1;

            case 0x5C: // Dialog with entity - Script_DialogWithEntity_5C (E12.a): same open semantics as
                       // 0x0D (textId is v[2] here, ctrl is v[3]); v[1] (entity search) is only for the
                       // deferred portrait/name box (E12.c) - ignored for display here, per plan.
                return OpenDialog(v[2], v[3], instructionSize: 4, opcode: 0x5C, opcodeName: "DialogWithEntity");

            case 0x49: // Restart - Script_73_049 (EntityEventHandlers.cs:1454-1459): unconditional jump
                       // back to Parameters[0] (this program's own start CodeIndex, set once by
                       // InitializeEventData - see that method's own doc). Same
                       // "target - CodeIndexAtFetch" shape as the 0x02/0x03/0x04 Goto family above;
                       // state.CodeIndex still equals codeIndexAtFetch here (Dispatch runs before
                       // RunOneScriptCall applies the returned delta).
                return state.Parameters[0] - state.CodeIndex;

            case 0x4B: // If false restart - Script_75_04B (EntityEventHandlers.cs:1476-1487): same jump
                       // as 0x49, but only when Result == 0; otherwise just advances past the 1-byte
                       // instruction (size 1, see EventOpcodeSizeTable).
                return state.Result == 0 ? state.Parameters[0] - state.CodeIndex : 1;

            case 0x5A: // Turn entity - Script_90_05A (EntityEventHandlers.cs:1694-1710): for every entity
                       // matched by v1's search type, TargetDirection = ResolveDirectionFromParam(v2).
                TurnMatchingEntities(entity, v[1], (uint)v[2], animationId: null);
                return 3;

            case 0x5B: // Turn entity with anim - Script_91_05B (EntityEventHandlers.cs:1713-1733): same
                       // as 0x5A, plus TargetAnimationId = v2 (v3 is the direction param here). Real
                       // map-389 operands (docs/intro-programs-389.txt, e.g. offset 1126): every
                       // occurrence's direction byte decodes to mode 2 (cardinal) - see pre-read census
                       // on ResolveDirectionFromParam's own doc.
                TurnMatchingEntities(entity, v[1], (uint)v[3], animationId: (uint)v[2]);
                return 4;

            case 0x8B: // Spawn entity next to entity - Script_139_08B @ 0x8004033C
                SpawnEntityNextToEntity(entity, v);
                return 9;

            case 0x62: // Set entities flags (low 16 bits) - Script_98_062
                SetEntitiesFlagsLow16(entity, v);
                return 4;

            case 0x63: // Clear entities flags (low 16 bits) - Script_99_063
                ClearEntitiesFlagsLow16(entity, v);
                return 4;

            case 0x64: // Set entities position - Script_100_064
                SetEntitiesPosition(entity, v);
                return 8;

            case 0x65: // Add entities position offset - Script_101_065
                AddEntitiesPositionOffset(entity, v);
                return 8;

            case 0xAC: // Set entity shadow size - Script_172_0AC
                SetEntityShadowSize(entity, v);
                return 4;

            case 0x67: // Camera follow entity - Script_103_067 (E5.a, EntityEventHandlers.cs:2068-2074)
                CameraFollowEntity(entity, v);
                return 2;

            case 0x68: // Camera stop follow entity - Script_104_068 (E5.a, EntityEventHandlers.cs:2077-2081)
                _worldContext.EntityFollowedByCamera = null;
                return 1;

            case 0x69: // Camera look at (forced) - Script_105_069 (E5.a, EntityEventHandlers.cs:2084-2091)
                CameraForceLookAt(v);
                return 7;

            case 0xBD: // Play sound 2 - Script_189_0BD (EntityEventHandlers.cs:3597-3610, E11.a,
                       // docs/plan-e11-audio.md): sfxId = (v[2] << 8) | v[1] - see AlundraSoundPlayer's
                       // own doc for the resolution/playback mechanics. SoundPlayer null (no
                       // AudioSystemComponent for this world, or the intro trace harness's own
                       // neutralization twin) -> degraded no-op, same shape as 0x54 below.
                if (_worldContext.SoundPlayer is { } playSound2Player)
                {
                    playSound2Player.PlaySfx((v[2] << 8) | v[1]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0xBD, "PlaySound2", "sound system");
                }

                return 3;

            case 0xBE: // Play sound 2 (bis) - Script_190_0BE (E11.a, docs/plan-e11-audio.md): same
                       // derivation and dispatch shape as 0xBD above - the decompilation never gives
                       // 0xBE an id derivation distinct from 0xBD's.
                if (_worldContext.SoundPlayer is { } playSound2BisPlayer)
                {
                    playSound2BisPlayer.PlaySfx((v[2] << 8) | v[1]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0xBE, "PlaySound2Bis", "sound system");
                }

                return 3;

            case 0x12: // Play sound 1 - Script_18_012 (EntityEventHandlers.cs:694-698, E11.a,
                       // docs/plan-e11-audio.md): sfxId = v[1] ALONE - a single-byte operand on a 2-byte
                       // instruction, NOT 0xBD's two-byte (v[2] << 8) | v[1] derivation (copying that
                       // would read one byte past this instruction, see T6's own doc).
                if (_worldContext.SoundPlayer is { } playSound1Player)
                {
                    playSound1Player.PlaySfx(v[1]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0x12, "PlaySound1", "sound system");
                }

                return 2;

            case 0x75: // Play sound effect - Script_117_075 (EntityEventHandlers.cs:2208-2212, E11.a,
                       // docs/plan-e11-audio.md): same single-byte v[1] derivation as 0x12 above, on its
                       // own 2-byte instruction.
                if (_worldContext.SoundPlayer is { } playSoundEffectPlayer)
                {
                    playSoundEffectPlayer.PlaySfx(v[1]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0x75, "PlaySoundEffect", "sound system");
                }

                return 2;

            case 0xA8: // Is sound loading - Script_168_0A8 (D-E11-5, docs/plan-e11-audio.md): this DLL
                       // never streams sound effects from a CD, so this predicate is always false. Writes
                       // Result explicitly (unlike the old UnknownOpcode fallback, which does NOT clear
                       // Result - the script would otherwise branch on whatever the PREVIOUS predicate
                       // left there).
                state.Result = 0;
                return 1;

            case 0xBA: // Check if loading from CD - Script_186_0BA (D-E11-5, docs/plan-e11-audio.md):
                       // same rationale as 0xA8 above - never streaming from a CD, always false.
                state.Result = 0;
                return 1;

            case 0x54: // Set walkable - Script_84_054 (EntityEventHandlers.cs:1589-1620): OR's
                       // v[3]/v[4] into (Walkability, GroundProperty) of the tile at (v[1],v[2]), clamped
                       // to the original's own hardcoded [0,0x33]x[0,0x3b] - see
                       // AlundraCellStore.SetCellBits's own doc for the exact clamp/index derivation.
                       // CellMutator null (no AlundraCellStore installed for this world) -> degraded
                       // no-op, same shape as 0xBD above.
                if (_worldContext.CellMutator is { } setBitsMutator)
                {
                    setBitsMutator.SetCellBits(v[1], v[2], v[3], v[4]);
                }
                else
                {
                    LogDegradedCellOpcodeOnce(0x54, "SetWalkable");
                }

                return 5;

            case 0x55: // Set unwalkable - Script_85_055 (EntityEventHandlers.cs:1623-1654): same clamp as
                       // 0x54 above, AND's the COMPLEMENT of v[3]/v[4] into (Walkability, GroundProperty)
                       // instead (bit clear).
                if (_worldContext.CellMutator is { } clearBitsMutator)
                {
                    clearBitsMutator.ClearCellBits(v[1], v[2], v[3], v[4]);
                }
                else
                {
                    LogDegradedCellOpcodeOnce(0x55, "SetUnwalkable");
                }

                return 5;

            case 0x85: // Set map tiles - Script_133_085 (EntityEventHandlers.cs:2440-2444): port of
                       // GameEngine.ChangeAreaTileProperties(srcX, srcY, width, height, dstX, dstY) - see
                       // AlundraCellStore.CopyCellRectangle's own doc for the exact field list and the
                       // documented no-clamp deviation (the original's own bounds check is a debug-only
                       // trap, not a guard).
                if (_worldContext.CellMutator is { } copyRectMutator)
                {
                    copyRectMutator.CopyCellRectangle(v[1], v[2], v[3], v[4], v[5], v[6]);
                }
                else
                {
                    LogDegradedCellOpcodeOnce(0x85, "ChangeAreaTileProperties");
                }

                return 7;

            case 0xAF: // Begin fade transition with color - Script_175_0AF (E10.b, docs/plan-e10-fondu.md):
                       // machine B (the drawn fade rectangle). v[1]/v[2]/v[3] are (R,G,B) already in
                       // DISPLAY order (§1.2 - see AlundraScreenFadeDirector's own class doc on the
                       // channel swap, which this port never re-applies). v[4] = tpage (blend selector),
                       // v[5] = duration (ticks), v[6] = persist (the persistence latch, §1.1 - never
                       // decremented anywhere in this DLL). ScreenFadeDirector null (no world context
                       // wired to exercise this opcode, e.g. most synthetic interpreter tests) ->
                       // degraded no-op, same shape as 0xBD/0x54 above.
                if (_worldContext.ScreenFadeDirector is { } beginFadeDirector)
                {
                    beginFadeDirector.BeginFadeEffect(v[1], v[2], v[3], v[4], v[5], v[6]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0xAF, "BeginFadeTransition", "screen fade director");
                }

                return 7;

            case 0xB0: // Set warp fade color and duration - Script_176_0B0 (E10.b): machine A, the "warp"
                       // timer - its colours are DEAD in this port (§1.1: their only output has zero
                       // readers in the decompilation) - only its flag/duration matter, consumed by
                       // 0xB1 below.
                if (_worldContext.ScreenFadeDirector is { } warpFadeDirector)
                {
                    warpFadeDirector.SetWarpFadeDuration(v[1], v[2], v[3], v[4]);
                }
                else
                {
                    LogDegradedOpcodeOnce(0xB0, "SetWarpFadeDuration", "screen fade director");
                }

                return 5;

            case 0xB1: // Check fade and warp flags - Script_177_0B1 (E10.b): predicate, Result =
                       // (fadeFlags == 0 && warpFlags == 0) ? 1 : 0. Writes Result in BOTH cases -
                       // unlike UnknownOpcode's own no-touch fallback, which would leave a stale Result
                       // from whatever the PREVIOUS predicate wrote (§1.7) - so a null ScreenFadeDirector
                       // still writes Result = 0 explicitly rather than skipping the write.
                state.Result = _worldContext.ScreenFadeDirector is { } fadeCheckDirector && fadeCheckDirector.IsSettled
                    ? 1
                    : 0;

                return 1;

            case 0x70: // Is above ground - Script_112_070 (EntityEventHandlers.cs:2161-2165): Result =
                       // logicEntity.IsOnGround, pulled from the controller every frame by
                       // AlundraEntityScriptProxy.Update's own root pull (E3.d) - 0 (falls) for an entity
                       // with no controller yet, same default the field already carries.
                state.Result = entity.IsOnGround;
                return 1;

            case 0x3B: // Check player in area - Script_59_03B (EntityEventHandlers.cs:1223-1240): tests
                       // the PLAYER entity's own TileX/TileY/TileZ - NOT the executing entity, unlike
                       // 0x07's EntityInArea (see that method's own doc) - against the inclusive box
                       // v[1]..v[6] (xmin,xmax,ymin,ymax,zmin,zmax), no clamp, same as the original.
                       // Writes Result only, advances by its own size (7) either way (docs/plan-e7-
                       // mutation-tuiles.md, slice E7.c, D-E7-6). D-E7-10: no PlayerEntity spawned for
                       // this world -> Result = 0, degraded no-op (once-logged warning) - the same
                       // "nothing to search" shape every other player-dependent path in this runner
                       // already falls back to (see case 0x27 above).
                if (_worldContext.PlayerEntity is { } areaPlayer)
                {
                    state.Result = areaPlayer.TileX >= v[1] && areaPlayer.TileX <= v[2]
                        && areaPlayer.TileY >= v[3] && areaPlayer.TileY <= v[4]
                        && areaPlayer.TileZ >= v[5] && areaPlayer.TileZ <= v[6]
                        ? 1 : 0;
                }
                else
                {
                    state.Result = 0;
                    LogDegradedNoPlayerOpcodeOnce(0x3B, "CheckPlayerInArea");
                }

                return 7;

            case 0x2F: // Check pad buttons - Script_47_02F (EntityEventHandlers.cs:1047-1069): NOT a
                       // direction test, despite the size table's old "Check moving in dir" label
                       // (D-E7-7 corrects it - see fact 1, docs/plan-e7-mutation-tuiles.md) - a raw pad-
                       // button test. Result = 1 iff (snapshot & ((v[2] << 8) | v[1])) != 0. v[3] selects
                       // the snapshot: 0 ButtonsHold, 1 ButtonsJustPressed - both read off
                       // AlundraGameState.LastPadState (D-E7-8's own seam, published by
                       // AlundraEntityScriptProxy.Update's player branch just before MovePlayer); 2
                       // (ButtonsReleased) and every other value (the original's own default arm,
                       // ButtonsJustPressedByInterval) have no field behind them on AlundraPadState
                       // (D-E7-9) - degraded no-op (Result = 0, once-logged warning), never an exception,
                       // since this runs on the production dispatch path.
            {
                var padFlag = (uint)((v[2] << 8) | v[1]);
                var padState = _gameState.LastPadState;

                if (v[3] == 0)
                {
                    state.Result = (padState.ButtonsHold & padFlag) != 0 ? 1 : 0;
                }
                else if (v[3] == 1)
                {
                    state.Result = (padState.ButtonsJustPressed & padFlag) != 0 ? 1 : 0;
                }
                else
                {
                    state.Result = 0;
                    LogDegradedPadSnapshotOnce(v[3]);
                }

                return 4;
            }

            default:
                return UnknownOpcode(command, state);
        }
    }

    /// <summary>
    /// Shared "open" half of opcodes 0x0D and 0x5C (E12.a, docs/plan-e12-dialogues.md): resolves
    /// <paramref name="textIdParam"/> (see <see cref="ResolveDialogText"/>), then either opens the real
    /// dialogue through <see cref="AlundraDialogueDirector"/> (Dispatch's own reentrancy guard - T2 -
    /// already ran BEFORE this is called, via <see cref="IAlundraDialogueDirector.IsOpen"/>) or degrades:
    /// still parses the text and applies every numeric control-code flag it contains (D-E12-4's own P0
    /// correction - "le mode dégradé pose AUSSI les drapeaux numériques", or a later <c>0x36</c> waiting
    /// on one of them would suspend forever) before advancing by <paramref name="instructionSize"/>
    /// regardless.
    /// </summary>
    private int OpenDialog(int textIdParam, int controlMode, int instructionSize, int opcode, string opcodeName)
    {
        var text = ResolveDialogText(textIdParam) ?? string.Empty;
        var director = _worldContext.DialogueDirector;

        if (director == null || !director.HasPresenter)
        {
            foreach (var page in AlundraDialogueTextParser.SplitIntoPages(text))
            {
                foreach (var n in page.NumericCodes)
                {
                    _gameState.AddFlag((uint)(n | 0x8000), 1u << (n & 0x1f));
                }
            }

            LogDegradedOpcodeOnce(opcode, opcodeName, "dialogue presenter");
            return instructionSize;
        }

        if (director.IsOpen)
        {
            return 0; // T2: a dialogue is already open - retry rather than opening a second one.
        }

        director.Open(text, controlMode);
        return instructionSize;
    }

    /// <summary>
    /// Resolves opcode 0x0D/0x5C's own textId operand (§1.3): bit 0x80 set -&gt; this world's LOCAL
    /// <see cref="LocalDialogueStrings"/>[textId &amp; 0x7F] (null if the table was never loaded, or the
    /// masked index is out of range); bit clear -&gt; the SHARED <c>map_alundra</c> table, which the
    /// converter does not export yet (E12.c) - always null here, logged once.
    /// </summary>
    private string? ResolveDialogText(int textIdParam)
    {
        if ((textIdParam & 0x80) != 0)
        {
            var localIndex = textIdParam & 0x7f;
            var localStrings = LocalDialogueStrings;
            if (localStrings != null && localIndex >= 0 && localIndex < localStrings.Count)
            {
                return localStrings[localIndex];
            }

            return null;
        }

        if (!_loggedSharedDialogTableOnce)
        {
            _loggedSharedDialogTableOnce = true;
            Logs.WriteWarning(
                "AlundraEventProgramRunner: dialog opcode referenced the SHARED table (map_alundra, "
                + "textId bit 0x80 clear), which the converter does not export yet (E12.c) - degraded, "
                + "empty text.");
        }

        return null;
    }

    /// <summary>Shared shape of Script_48_030 (If flag on) / Script_49_031 (If flag off).</summary>
    private int FlagBranch(int[] v, bool wantSet)
    {
        var flag = (uint)(v[1] + v[2] * 0x100);
        var mask = 1 << (v[1] & 0x1f);
        var isSet = (_gameState.GetFlag(flag) & mask) != 0;
        return isSet == wantSet ? SignExtend16((v[4] << 8) | v[3]) : 5;
    }

    /// <summary>Script_54_036 (0x36) - a pure-flag suspend/advance test, same shape as
    /// <see cref="FlagBranch"/> but WITHOUT a goto (it is only ever used as a suspend gate, not a
    /// branch): returns 3 (advance past the instruction) once the flag bit is SET, 0 (suspend, retry
    /// next frame) while it is clear. The size table calls this "Wait flag off", which is backwards -
    /// see the case 0x36 comment on <see cref="Dispatch"/>.</summary>
    private int WaitUntilFlagOn(int[] v)
    {
        var flag = (uint)((v[2] << 8) | v[1]);
        var mask = 1u << (v[1] & 0x1f);
        return (_gameState.GetFlag(flag) & mask) != 0 ? 3 : 0;
    }

    /// <summary>Script_51_033 (0x33 CheckFlagsOn) - tests FOUR (flag,bit) pairs from v[1..8]:
    /// Result=1 only if ALL FOUR bits are set, Result=0 (short-circuit on the first clear pair)
    /// otherwise. Always returns 9 (instruction size) regardless of Result - unlike FlagBranch,
    /// this opcode never branches itself; a following conditional-goto (0x03/0x04) reads Result.
    /// </summary>
    private int CheckFlagsOn(int[] v, EventProgramState state)
    {
        for (var i = 0; i < 4; i++)
        {
            var flag = (uint)(v[i * 2 + 1] + (v[i * 2 + 2] << 8));
            var mask = 1u << (int)(flag & 0x1f);

            if ((_gameState.GetFlag(flag) & mask) == 0)
            {
                state.Result = 0;
                return 9;
            }
        }

        state.Result = 1;
        return 9;
    }

    /// <summary>
    /// Script_45_02D (0x2D ActivateEntity) - dynamic spawn by entity-record id, always with
    /// <c>notCheckSpawnZone = 1</c> (the original hardcodes that argument). Delegates to
    /// <see cref="IEntityWorldContext.SpawnEntityByRecordId"/>; the original breakpoints (debug-only
    /// trap) when the spawn fails, this V1 port just logs once per failing record id instead.
    /// </summary>
    private void ActivateEntity(AlundraEntityScriptProxy entity, int[] v)
    {
        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[1]);

        if (spawned == null && _loggedFailedActivations.Add(v[1]))
        {
            Logs.WriteDebug(
                $"AlundraEventProgramRunner: opcode 0x2D ActivateEntity({v[1]}) - spawn failed (record "
                + "disabled/missing, or the spawn path threw) - the original breakpoints here instead.");
        }
    }

    /// <summary>Script_139_08B (0x8B SpawnEntityNextToEntity) - dynamic spawn by entity-record
    /// id (v[2]), same notCheckSpawnZone=1 spawn path as 0x2D ActivateEntity, then positions the
    /// NEW entity relative to the first entity matched by v[1]s search type: raw 16.16 offset
    /// added to that match own PosX/PosY/PosZ. The original (EntityEventHandlers.cs:2557-2575)
    /// dereferences the spawned entity unconditionally even when SpawnEntity returned null (a
    /// latent null-pointer bug it never hits in practice on real data) - this port null-checks
    /// instead and simply skips the position write when the spawn failed, logging once like 0x2D
    /// own ActivateEntity above (shares the same failed-activation log set).</summary>
    private void SpawnEntityNextToEntity(AlundraEntityScriptProxy entity, int[] v)
    {
        var spawned = _worldContext.SpawnEntityByRecordId(entity, v[2]);

        if (spawned == null && _loggedFailedActivations.Add(v[2]))
        {
            Logs.WriteDebug(
                $"AlundraEventProgramRunner: opcode 0x8B SpawnEntityNextToEntity({v[2]}) - spawn "
                + "failed (record disabled/missing, or the spawn path threw) - the original "
                + "breakpoints here instead.");
        }

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        if (spawned != null && matches.Count != 0)
        {
            spawned.PosX = matches[0].PosX + ((v[3] + v[4] * 0x100) << 16);
            spawned.PosY = matches[0].PosY + ((v[5] + v[6] * 0x100) << 16);
            spawned.PosZ = matches[0].PosZ + ((v[7] + v[8] * 0x100) << 16);
            // E3.d: grep-routed Pos* write site (docs/plan-e3-collisions.md "DLL - propriete de la
            // racine par frame" item 4) - a no-op today (no spawned prefab carries a controller, E3.d
            // scopes CharacterControllerComponent to the hero alone), kept for parity with every other
            // scripted Pos* write site so a future controller-driven spawn is routed correctly too.
            spawned.PushLogicalPositionToRoot();
        }
    }

    /// <summary>Script_46_02E (0x2E DestroyEntity) - destroys every entity matched by
    /// <c>variables[1]</c>'s search type, and sets <see cref="EventProgramState.Result"/> to 1 if at
    /// least one entity matched (0 otherwise) - read by a following conditional-goto opcode.</summary>
    private void DestroyMatchingEntities(AlundraEntityScriptProxy entity, int[] v, EventProgramState state)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);
        state.Result = matches.Count > 0 ? 1 : 0;

        foreach (var match in matches)
        {
            _worldContext.DestroyEntity(match);
        }
    }

    /// <summary>Script_98_062 (0x62) - ORs the low-16-bit flag word <c>(v[3]&lt;&lt;8|v[2])</c> into every
    /// matched entity's <see cref="AlundraEntityScriptProxy.Flags"/>. Bug fix (user-reported runtime timing
    /// bug, gull entity 6): the original has no such bridge (physics reads <c>Flags</c> directly every
    /// tick, PhysicsEngine.cs:1460) - this engine's own controller caches Gravity/WalkabilityMask from
    /// Flags in its own Settings, so every match with a controller must be resynced here too (see
    /// <see cref="AlundraEntityScriptProxy.ResyncControllerFromFlags"/>'s own doc), same as
    /// <see cref="ClearEntitiesFlagsLow16"/> below and the 0x16/0x17 opcode handlers.</summary>
    private void SetEntitiesFlagsLow16(AlundraEntityScriptProxy entity, int[] v)
    {
        var flag = (uint)((v[3] << 8) | v[2]);
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            match.Flags |= flag;
            match.ResyncControllerFromFlags();
        }
    }

    /// <summary>Script_99_063 (0x63) - clears the low-16-bit flag word <c>(v[3]&lt;&lt;8|v[2])</c> out of
    /// every matched entity's <see cref="AlundraEntityScriptProxy.Flags"/> (bits 16-31 always survive). Bug
    /// fix (user-reported runtime timing bug): same controller resync requirement as
    /// <see cref="SetEntitiesFlagsLow16"/> above - see that method's own doc and
    /// <see cref="AlundraEntityScriptProxy.ResyncControllerFromFlags"/>'s own doc. This is the exact site
    /// the gull-6 bug traced to: Tick program 134's own 0x63 [128,0,1] (clear mask (1&lt;&lt;8)|0 = 0x100 =
    /// <see cref="EntityFlags.Gravity"/>) at its climb apex used to clear the Flags bit without ever telling
    /// the controller, so <c>Settings.Gravity</c> stayed at its spawn-time value and kept pulling the gull
    /// down through its own scripted hover.</summary>
    private void ClearEntitiesFlagsLow16(AlundraEntityScriptProxy entity, int[] v)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);
        var clearMask = (uint)((v[3] << 8) | v[2]);
        var andMask = 0xFFFF0000u | (~clearMask & 0xFFFFu);

        foreach (var match in matches)
        {
            match.Flags &= andMask;
            match.ResyncControllerFromFlags();
        }
    }

    /// <summary>Script_100_064 (0x64) - sets PosX/PosY/PosZ of every matched entity from the raw operand
    /// bytes, packed as 16.16 fixed-point (PosZ gets the original's own <c>+1</c> bias). Transform
    /// re-derivation onto the CasaEngine world position happens later, once per frame, in
    /// <see cref="AlundraWorldProxy"/>'s own per-frame pass - this handler only ever touches the logical
    /// fields, exactly like the original; E3.d's <see cref="AlundraEntityScriptProxy.PushLogicalPositionToRoot"/>
    /// (docs/plan-e3-collisions.md "DLL - propriete de la racine par frame" item 4) pushes it onto the
    /// root immediately instead, but only for a controller-driven entity - a no-op for every other
    /// match, keeping this handler's own behaviour exactly as before E3.d for them.</summary>
    private void SetEntitiesPosition(AlundraEntityScriptProxy entity, int[] v)
    {
        var x = ((v[3] << 8) | v[2]) << 16;
        var y = ((v[5] << 8) | v[4]) << 16;
        var z = (((v[7] << 8) | v[6]) << 16) + 1;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            match.PosX = x;
            match.PosY = y;
            match.PosZ = z;
            match.PushLogicalPositionToRoot();
        }
    }

    /// <summary>Script_101_065 (0x65) - adds a raw 16.16 fixed-point offset to PosX/PosY/PosZ of every
    /// matched entity (note the original's little-endian byte order here is the mirror image of 0x64's -
    /// <c>v[2]|(v[3]&lt;&lt;8)</c> vs 0x64's <c>(v[3]&lt;&lt;8)|v[2]</c>, same value either way but ported
    /// verbatim for fidelity). Same E3.d root-push as 0x64 above - see that handler's own doc.</summary>
    private void AddEntitiesPositionOffset(AlundraEntityScriptProxy entity, int[] v)
    {
        var x = (v[2] | (v[3] << 8)) << 16;
        var y = (v[4] | (v[5] << 8)) << 16;
        var z = (v[6] | (v[7] << 8)) << 16;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            match.PosX += x;
            match.PosY += y;
            match.PosZ += z;
            match.PushLogicalPositionToRoot();
        }
    }

    /// <summary>Script_172_0AC (0xAC) - rewrites the shadow-size bits of the FIRST matched entity's
    /// <see cref="AlundraEntityScriptProxy.Flags"/> only (the original reads
    /// <c>g_matchingEntitiesBuffer[0]</c> unconditionally when <c>count != 0</c>, every other match is
    /// ignored). Actual shadow drawing stays unported (no shadow renderer yet) - this only reproduces the
    /// flag write, which is all following searches/reads of the shadow-size bits can observe.</summary>
    private void SetEntityShadowSize(AlundraEntityScriptProxy entity, int[] v)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        if (matches.Count != 0)
        {
            matches[0].Flags = EntityFlags.WithShadowSize(matches[0].Flags, (uint)v[2]);
        }
    }

    /// <summary>Script_103_067 (0x67, E5.a) - retargets <see cref="IEntityWorldContext.EntityFollowedByCamera"/>
    /// to the FIRST entity matched by <c>v[1]</c>'s search type, or <c>null</c> when nothing matched
    /// (faithful - the original reads <c>g_matchingEntitiesBuffer[0]</c> unconditionally, which is
    /// <c>null</c>/unset on an empty search; no fallback to the player or to the previous target).</summary>
    private void CameraFollowEntity(AlundraEntityScriptProxy entity, int[] v)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);
        _worldContext.EntityFollowedByCamera = matches.Count != 0 ? matches[0] : null;
    }

    /// <summary>Script_105_069 (0x69, E5.a) - nulls <see cref="IEntityWorldContext.EntityFollowedByCamera"/>
    /// and imposes the camera's look-at position directly from <c>v[1..6]</c>, each axis packed as
    /// low+high bytes (<c>v[2n-1] | (v[2n] &lt;&lt; 8)</c>) exactly like <c>g_cameraLookAtX/Y/Z</c>'s own
    /// plain-pixel-int units (no fixed-point shift - see <see cref="IEntityWorldContext.SetForcedCameraLookAt"/>'s
    /// own doc).</summary>
    private void CameraForceLookAt(int[] v)
    {
        var x = v[1] | (v[2] << 8);
        var y = v[3] | (v[4] << 8);
        var z = v[5] | (v[6] << 8);
        _worldContext.SetForcedCameraLookAt(x, y, z);
    }

    /// <summary>Script_55_037 (0x37 Wait) - suspends (returns 0) until <c>v[1]</c> frames have elapsed
    /// since this same instruction was first reached, tracked in Parameters[1]/[2] the same way the
    /// original keys re-entrancy off CodeIndex.</summary>
    private static int Wait(int[] v, EventProgramState state)
    {
        if (state.Parameters[1] != state.CodeIndex)
        {
            state.Parameters[1] = state.CodeIndex;
            state.Parameters[2] = 0;
            return 0;
        }

        var counter = state.Parameters[2];
        state.Parameters[2] = counter + 1;

        return counter >= v[1] ? 2 : 0;
    }

    // Tile size the whole walk/detour port converts px<->grid-cell with (StaticVariables.MapTileWidth/
    // Height - same constants AlundraCellsCollisionField/AlundraScriptedMotion already duplicate for the
    // same reason, see either class' own doc).
    private const int TileWidthPx = 24;
    private const int TileHeightPx = 16;

    /// <summary>Margin (E4.d decision D5) added past the original's own distance threshold when the
    /// navigation detour projects its destination cell - one full tile, so the projected point clears
    /// the CURRENT cell in either axis rather than landing right on its own boundary (which could
    /// resolve, after px-&gt;cell clamping, back to the very cell the entity is already standing in).
    /// <see cref="TileWidthPx"/> (the larger of the two tile axes) is used uniformly for both X and Y -
    /// the original's own threshold test itself is a single scalar applied identically to both axes
    /// (Script_30_01E's <c>threshold &lt;= dx || threshold &lt;= dy</c>), so the margin follows the same
    /// shape.</summary>
    private const int WalkDetourMarginPx = TileWidthPx;

    /// <summary>Arrival radius (E4.d decision D5) the navigation detour advances
    /// <see cref="NavigationPath.CurrentPointIndex"/> at - half the SHORTER tile axis, so a waypoint
    /// counts as "reached" well before the entity could overshoot past its own cell into the next
    /// one.</summary>
    private const float WalkDetourArrivalRadiusPx = TileHeightPx / 2f;

    /// <summary>Reused across every detour this runner engages (never per-tick - see
    /// <see cref="TryEngageDetour"/>'s own call site) - default query (no diagonal-corner-cutting,
    /// <see cref="NavigationLayerMask.All"/>): E4.a's own navigation layer only encodes universal walls
    /// (M = 0x40, no per-entity class/layer split - see that tranche's own "Réalisé" note), so no
    /// per-entity query customization is needed here.</summary>
    private readonly NavigationQuery _walkDetourQuery = new();

    /// <summary>
    /// Shared core of Script_30_01E (0x1E "Walk") and Script_31_01F (0x1F "Walk with collision") -
    /// EntityEventHandlers.cs:793-829, port kept bit-for-bit: packs a re-entry <paramref name="v"/>-based
    /// signature into <c>state.Parameters[1]</c>, memorizes the walk's own start position into
    /// <c>Parameters[2..3]</c> on the FIRST call for this signature (suspending, return 0), then on every
    /// later call compares the CURRENT position against that memorized start: <c>threshold &lt;= |ΔX|&gt;&gt;16
    /// || threshold &lt;= |ΔY|&gt;&gt;16</c> (both an inclusive test AND a truncating, not rounding, pixel
    /// shift - ported exactly) ends the walk (return 3), otherwise it keeps suspending (return 0).
    /// <paramref name="allowDetour"/> (true for 0x1E, false for 0x1F - D5) additionally drives
    /// <see cref="UpdateWalkDetour"/> while still suspended - see that method's own doc for the E4.d
    /// navigation extension. <see cref="AlundraEntityScriptProxy.WalkDetourPath"/>/
    /// <see cref="AlundraEntityScriptProxy.WalkDetourAttempted"/> are reset on every fresh occurrence
    /// (signature change) AND on completion, so a LATER 0x1E/0x1F on the same entity always starts clean.
    /// </summary>
    private int Walk(AlundraEntityScriptProxy entity, int[] v, EventProgramState state, bool allowDetour)
    {
        var signature = (v[2] << 16) | (v[1] << 8) | v[0];

        if (state.Parameters[1] != signature)
        {
            state.Parameters[1] = signature;
            state.Parameters[2] = entity.PosX;
            state.Parameters[3] = entity.PosY;
            entity.WalkDetourPath = null;
            entity.WalkDetourAttempted = false;
            return 0;
        }

        var dx = state.Parameters[2] - entity.PosX;
        var dy = state.Parameters[3] - entity.PosY;

        if (dx < 0)
        {
            dx = -dx;
        }

        if (dy < 0)
        {
            dy = -dy;
        }

        dx >>= 16;
        dy >>= 16;

        var threshold = (v[2] << 8) | v[1];

        if (threshold <= dx || threshold <= dy)
        {
            entity.WalkDetourPath = null;
            entity.WalkDetourAttempted = false;
            return 3;
        }

        if (allowDetour)
        {
            UpdateWalkDetour(entity, state, threshold);
        }

        return 0;
    }

    /// <summary>
    /// E4.d navigation detour (decision D5, docs/plan-e4-deplacement-scripte.md): default is to do
    /// NOTHING (free walk continues, identical to the original) - only engages when this frame's
    /// <see cref="AlundraEntityScriptProxy.ForceAdjusted"/> is nonzero (the last completed sub-step's own
    /// movement was curtailed - see that field's own doc) AND a navigation grid is available
    /// (<see cref="IEntityWorldContext.NavigationGrid"/>, null in degraded mode - see
    /// <see cref="TryEngageDetour"/>'s own doc for what happens when <c>TryFindPath</c> itself fails).
    /// Once a detour path exists and has not finished, <see cref="AdvanceDetourWaypoint"/> re-derives
    /// <see cref="AlundraEntityScriptProxy.TargetDirection"/> toward the CURRENT waypoint every tick, so
    /// the entity visibly walks around the obstacle instead of pushing into it - the 0x1E/0x1F walk
    /// itself still only ends via <see cref="Walk"/>'s own ORIGINAL distance test (this method never
    /// returns a "completed" signal of its own). Once <c>WalkDetourPath.IsFinished</c>,
    /// <see cref="TargetDirection"/> is simply left at its last derived value (documented écart, D5) -
    /// nothing here re-engages a SECOND detour for the same occurrence (see
    /// <see cref="AlundraEntityScriptProxy.WalkDetourAttempted"/>'s own doc on why one attempt per
    /// occurrence is deliberate, not merely an optimization).
    /// </summary>
    private void UpdateWalkDetour(AlundraEntityScriptProxy entity, EventProgramState state, int thresholdPx)
    {
        var grid = _worldContext.NavigationGrid;

        if (entity.WalkDetourPath == null && !entity.WalkDetourAttempted && grid != null && entity.ForceAdjusted != 0)
        {
            TryEngageDetour(entity, state, grid, thresholdPx);
        }

        if (entity.WalkDetourPath != null && !entity.WalkDetourPath.IsFinished)
        {
            AdvanceDetourWaypoint(entity);
        }
    }

    /// <summary>
    /// One-shot detour engagement (see <see cref="AlundraEntityScriptProxy.WalkDetourAttempted"/>'s own
    /// doc on why this never runs twice for the same 0x1E occurrence): destination = the walk's own
    /// MEMORIZED start position (<c>state.Parameters[2..3]</c>, NOT the entity's current position) offset
    /// by <c>(sign(OffsetXList[TargetDirection]), sign(OffsetYList[TargetDirection])) *
    /// (thresholdPx + WalkDetourMarginPx)</c> - i.e. projected past where the ORIGINAL walk was always
    /// heading anyway, exactly <see cref="WalkDetourMarginPx"/> beyond the distance that would have ended
    /// it cleanly. Both the entity's current position and that destination are converted px-&gt;cell the
    /// same clamped way <see cref="AlundraCellsCollisionField"/> does (24x16, clamped to the grid bounds -
    /// see that class' own doc), then <c>NavigationGrid2D.TryFindPath</c> runs ONCE, from cell-center to
    /// cell-center. On failure (no path exists, e.g. a fully enclosed wall) <see cref="AlundraEntityScriptProxy.WalkDetourPath"/>
    /// stays null - D5's own "no detour (keep pushing, original behavior)" - and
    /// <see cref="AlundraEntityScriptProxy.WalkDetourAttempted"/> still latches true, so this occurrence
    /// never retries (matching "keep pushing" for its own remaining duration, not just this one tick).
    /// </summary>
    private void TryEngageDetour(AlundraEntityScriptProxy entity, EventProgramState state, NavigationGrid2D grid, int thresholdPx)
    {
        entity.WalkDetourAttempted = true;

        var dirIndex = (int)entity.TargetDirection;
        var offsetX = dirIndex >= 0 && dirIndex < AnimationTables.OffsetXList.Length ? AnimationTables.OffsetXList[dirIndex] : (short)0;
        var offsetY = dirIndex >= 0 && dirIndex < AnimationTables.OffsetYList.Length ? AnimationTables.OffsetYList[dirIndex] : (short)0;
        var signX = Math.Sign((int)offsetX);
        var signY = Math.Sign((int)offsetY);

        var reachPx = thresholdPx + WalkDetourMarginPx;
        var destPxX = (state.Parameters[2] >> 16) + signX * reachPx;
        var destPxY = (state.Parameters[3] >> 16) + signY * reachPx;

        var (startCellX, startCellY) = ToCell(grid, entity.PosX >> 16, entity.PosY >> 16);
        var (destCellX, destCellY) = ToCell(grid, destPxX, destPxY);

        var start = grid.GetWorldPosition(startCellX, startCellY);
        var goal = grid.GetWorldPosition(destCellX, destCellY);

        if (grid.TryFindPath(start, goal, _walkDetourQuery, out var path))
        {
            entity.WalkDetourPath = path;
        }
    }

    /// <summary>px-&gt;cell conversion (E4.d decision E4-2), same clamped shape
    /// <see cref="AlundraCellsCollisionField"/>'s own ground sampling uses (that class' own doc,
    /// "cell = (x / 24, y / 16), clamped to [0, width-1] x [0, height-1]").</summary>
    private static (int X, int Y) ToCell(NavigationGrid2D grid, int px, int py)
        => (Math.Clamp(px / TileWidthPx, 0, grid.Width - 1), Math.Clamp(py / TileHeightPx, 0, grid.Height - 1));

    /// <summary>
    /// Re-derives <see cref="AlundraEntityScriptProxy.TargetDirection"/> toward the CURRENT waypoint of an
    /// active detour (E4.d decision D5) every tick this is called, via the SAME
    /// <see cref="ScriptHelper.GetDirectionToTarget"/> 0x27 already uses (raw 16.16 deltas). The waypoint
    /// (a <see cref="NavigationGrid2D"/> "grid world" point, cell size 1 - <c>X = cellX + 0.5</c>,
    /// <c>Z = cellY + 0.5</c>, Y-logical mapped onto grid Z per E4-2) converts back to its own cell-center
    /// pixel position (<c>cx*24+12</c>, <c>cy*16+8</c>) before the delta is taken. Advances
    /// <see cref="NavigationPath.CurrentPointIndex"/> once the entity is within
    /// <see cref="WalkDetourArrivalRadiusPx"/> of the current waypoint; if that advance finishes the path,
    /// this leaves <see cref="AlundraEntityScriptProxy.TargetDirection"/> at whatever it last resolved to
    /// (documented écart, D5) rather than re-deriving toward a waypoint that no longer exists.
    /// </summary>
    private static void AdvanceDetourWaypoint(AlundraEntityScriptProxy entity)
    {
        var path = entity.WalkDetourPath!;
        var waypoint = path.Points[path.CurrentPointIndex];
        var waypointPxX = (int)MathF.Floor(waypoint.X) * TileWidthPx + TileWidthPx / 2;
        var waypointPxY = (int)MathF.Floor(waypoint.Z) * TileHeightPx + TileHeightPx / 2;

        var dx = (waypointPxX << 16) - entity.PosX;
        var dy = (waypointPxY << 16) - entity.PosY;

        var deltaXPx = dx / 65536f;
        var deltaYPx = dy / 65536f;

        if (deltaXPx * deltaXPx + deltaYPx * deltaYPx <= WalkDetourArrivalRadiusPx * WalkDetourArrivalRadiusPx)
        {
            path.CurrentPointIndex++;

            if (path.IsFinished)
            {
                return;
            }

            waypoint = path.Points[path.CurrentPointIndex];
            waypointPxX = (int)MathF.Floor(waypoint.X) * TileWidthPx + TileWidthPx / 2;
            waypointPxY = (int)MathF.Floor(waypoint.Z) * TileHeightPx + TileHeightPx / 2;
            dx = (waypointPxX << 16) - entity.PosX;
            dy = (waypointPxY << 16) - entity.PosY;
        }

        entity.TargetDirection = ScriptHelper.GetDirectionToTarget(dx, dy);
    }

    private static int SignExtend16(int value) => (short)value;

    /// <summary>
    /// Unknown/unimplemented-opcode policy (V1 deviation from the original, which has a real handler
    /// for every byte 0x00-0xC4 plus 0xFF): advance by the ported size table
    /// (<see cref="EventOpcodeSizeTable.Entries"/>) with one log per opcode value. An opcode with no
    /// known size (or a listed size &lt;= 0 - see that table's own doc on its debug/not-implemented
    /// entries) cannot be safely skipped, so it terminates this script call instead (treated as 0xFF).
    /// </summary>
    private int UnknownOpcode(int command, EventProgramState state)
    {
        if (!EventOpcodeSizeTable.Entries.TryGetValue((byte)command, out var entry) || entry.Size <= 0)
        {
            _lastDispatchKind = EventTraceKind.UnknownNoSizeTerminated;

            if (_loggedUnknownOpcodes.Add(command))
            {
                Logs.WriteWarning(
                    $"AlundraEventProgramRunner: opcode 0x{command:x2} has no known size; terminating "
                    + "this script call (V1 deviation).");
            }

            return 0;
        }

        _lastDispatchKind = EventTraceKind.UnknownSkipped;

        if (_loggedUnknownOpcodes.Add(command))
        {
            Logs.WriteWarning(
                $"AlundraEventProgramRunner: opcode 0x{command:x2} ('{entry.Name}') not implemented; "
                + $"skipping by its known size ({entry.Size}) (V1 deviation).");
        }

        return entry.Size;
    }

    private void LogDegradedOpcodeOnce(int opcode, string name, string missingSystem)
    {
        _lastDispatchKind = EventTraceKind.Degraded;

        if (_loggedDegradedOpcodes.Add(opcode))
        {
            Logs.WriteDebug(
                $"AlundraEventProgramRunner: opcode 0x{opcode:x2} ({name}) not implemented "
                + $"({missingSystem} not ported yet) - degraded no-op, advancing by its size.");
        }
    }

    /// <summary>
    /// Degraded fallback for the three cell-mutation opcodes (0x54/0x55/0x85, E7.a,
    /// docs/plan-e7-mutation-tuiles.md) when this world's <see cref="IEntityWorldContext.CellMutator"/> is
    /// null - same shape as <see cref="LogDegradedOpcodeOnce"/> (once per opcode value, trace kind
    /// <see cref="EventTraceKind.Degraded"/>, caller still advances by the instruction's own size), but a
    /// dedicated WARNING (not <see cref="Logs.WriteDebug"/>) since a missing cell store is a world-setup
    /// gap, not an unported subsystem like 0xBD's sound system.
    /// </summary>
    private void LogDegradedCellOpcodeOnce(int opcode, string name)
    {
        _lastDispatchKind = EventTraceKind.Degraded;

        if (_loggedDegradedOpcodes.Add(opcode))
        {
            Logs.WriteWarning(
                $"AlundraEventProgramRunner: opcode 0x{opcode:x2} ({name}) has no CellMutator installed "
                + "for this world - degraded no-op, advancing by its size.");
        }
    }

    /// <summary>
    /// Degraded fallback for opcode 0x3B (Check player in area, D-E7-10, docs/plan-e7-mutation-tuiles.md,
    /// slice E7.c) when this world spawned no <see cref="IEntityWorldContext.PlayerEntity"/> - same
    /// "nothing to search" shape every other player-dependent path in this runner already falls back to
    /// (e.g. case 0x27's FacePlayer), but reported as <see cref="EventTraceKind.Degraded"/> with a
    /// dedicated WARNING (not <see cref="Logs.WriteDebug"/>), same convention as
    /// <see cref="LogDegradedCellOpcodeOnce"/>: a world with no player is a setup gap, not an unported
    /// subsystem.
    /// </summary>
    private void LogDegradedNoPlayerOpcodeOnce(int opcode, string name)
    {
        _lastDispatchKind = EventTraceKind.Degraded;

        if (_loggedDegradedOpcodes.Add(opcode))
        {
            Logs.WriteWarning(
                $"AlundraEventProgramRunner: opcode 0x{opcode:x2} ({name}) has no PlayerEntity spawned "
                + "for this world - degraded no-op (Result = 0), advancing by its size.");
        }
    }

    /// <summary>
    /// Degraded fallback for opcode 0x2F's two unported pad snapshots (D-E7-9, docs/plan-e7-mutation-
    /// tuiles.md, slice E7.c): mode 2 (ButtonsReleased) and every other value (the original's own default
    /// arm, ButtonsJustPressedByInterval) have no field behind them on <see cref="AlundraPadState"/> -
    /// this degrades (Result = 0, once-logged warning) rather than throwing, since this runs on the
    /// production dispatch path and every real map-389 0x2F site is mode 0 (unreached in practice).
    /// </summary>
    private void LogDegradedPadSnapshotOnce(int mode)
    {
        _lastDispatchKind = EventTraceKind.Degraded;

        if (_loggedDegradedOpcodes.Add(0x2F))
        {
            Logs.WriteWarning(
                $"AlundraEventProgramRunner: opcode 0x2F (CheckPadButtons) snapshot mode {mode} "
                + "(ButtonsReleased/ButtonsJustPressedByInterval) is not backed by AlundraPadState - "
                + "degraded no-op (Result = 0).");
        }
    }

    /// <summary>Script_7_007 (0x07 CheckEntityInArea) - true if AT LEAST ONE entity matched by
    /// <c>v[1]</c>'s search type has <see cref="AlundraEntityScriptProxy.TileX"/>/
    /// <see cref="AlundraEntityScriptProxy.TileY"/>/<see cref="AlundraEntityScriptProxy.TileZ"/> inside
    /// the inclusive box <c>v[2]..v[7]</c> (xmin,xmax,ymin,ymax,zmin,zmax). See the 0x07 case's own doc
    /// on <see cref="Dispatch"/> for the one order-of-iteration deviation from the original.</summary>
    private bool EntityInArea(AlundraEntityScriptProxy entity, int[] v)
    {
        var xmin = v[2];
        var xmax = v[3];
        var ymin = v[4];
        var ymax = v[5];
        var zmin = v[6];
        var zmax = v[7];

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            if (match.TileX >= xmin && match.TileX <= xmax
                && match.TileY >= ymin && match.TileY <= ymax
                && match.TileZ >= zmin && match.TileZ <= zmax)
            {
                return true;
            }
        }

        return false;
    }

    private readonly HashSet<int> _loggedOutOfRangeMapIndexes = new();

    /// <summary>Script_SetSaveMapIdToInternalMapIndex_038 (0x38) - see the 0x38 case's own doc on
    /// <see cref="Dispatch"/>. <see cref="AlundraGameState.MapIdToInternalMapIndexTable"/> is sized 500
    /// (the original's own <c>ushort[500]</c>, SaveData.cs:18) but the packed index <c>v2&lt;&lt;8|v1</c>
    /// spans a full byte pair (0..65535) - out of range on real map-389 data (indices observed stay under
    /// 60), guarded here (logged once per offending index) rather than trusting every possible operand
    /// byte pair the way the original's raw array write implicitly would.</summary>
    private void SetSaveMapIdToInternalMapIndex(int[] v)
    {
        var index = (v[2] << 8) | v[1];
        var table = _gameState.MapIdToInternalMapIndexTable;

        if (index < 0 || index >= table.Length)
        {
            if (_loggedOutOfRangeMapIndexes.Add(index))
            {
                Logs.WriteWarning(
                    $"AlundraEventProgramRunner: opcode 0x38 SetSaveMapIdToInternalMapIndex({index}) - "
                    + $"index out of range (table size {table.Length}); write skipped (V1 deviation).");
            }

            return;
        }

        table[index] = (ushort)((v[4] << 8) | v[3]);
    }

    /// <summary>Shared shape of Script_90_05A (Turn entity) / Script_91_05B (Turn entity with anim) - for
    /// every entity matched by <paramref name="searchType"/>, resolves and writes
    /// <see cref="AlundraEntityScriptProxy.TargetDirection"/> via <see cref="ResolveDirectionFromParam"/>,
    /// and, when <paramref name="animationId"/> is non-null (0x5B only), also writes
    /// <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> first (matching the original's own
    /// assignment order, EntityEventHandlers.cs:1726-1729 - the anim id itself never influences direction
    /// resolution, so the order has no observable effect, but is kept faithful anyway).</summary>
    private void TurnMatchingEntities(AlundraEntityScriptProxy entity, int searchType, uint directionParam, uint? animationId)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, searchType, _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            if (animationId.HasValue)
            {
                match.TargetAnimationId = animationId.Value;
            }

            match.TargetDirection = ResolveDirectionFromParam(match, directionParam);
        }
    }

    /// <summary>
    /// Full port of <c>GameEngine.ResolveDirectionFromParam</c> (GameEngine.cs:2325-2382, address
    /// 0x8003cfc8) - the 3 high bits of <paramref name="encodedDir"/> select one of 8 modes, the low 5
    /// bits (<c>result</c>) feed most of them. Modes 4/5 (random, <c>Random.Next()</c>) are NOT ported -
    /// no faithful PSX RNG exists in this runtime - and throw instead of silently guessing: the E4.c
    /// pre-read census (docs/plan-e4-deplacement-scripte.md "MANDATORY PRE-READ") decoded every real
    /// 0x5A/0x5B direction-parameter occurrence in map 389's own programs and found mode 2 (cardinal)
    /// exclusively, so this path is provably unreached by the intro; a future map that DOES reach it must
    /// stop here loudly, not silently fall back to a wrong direction.
    /// </summary>
    internal uint ResolveDirectionFromParam(AlundraEntityScriptProxy entity, uint encodedDir)
    {
        var result = encodedDir & 0x1f;

        switch (encodedDir >> 5)
        {
            case 0: // Direct: the low 5 bits are the direction verbatim.
                return result;

            case 1: // Relative to the entity's own current TargetDirection.
                return (entity.TargetDirection + result) & 0x1f;

            case 2: // Cardinal, via g_cardinalDirectionTable (AnimationTables.CardinalDirectionTable).
                return AnimationTables.CardinalDirectionTable[encodedDir & 3];

            case 3: // Toward the player, plus a signed offset (the FULL encodedDir byte, not just result).
            {
                var toPlayer = _worldContext.PlayerEntity is { } player3
                    ? ScriptHelper.GetDirectionToTarget(player3.PosX - entity.PosX, player3.PosY - entity.PosY)
                    : 0u;
                return (toPlayer + encodedDir) & 0x1f;
            }

            case 4: // Random cardinal - RNG not ported, see this method's own doc.
            case 5: // Random direction (0..31) - RNG not ported, see this method's own doc.
                throw new NotSupportedException(
                    $"ResolveDirectionFromParam: mode {encodedDir >> 5} (random) requires a PSX RNG port "
                    + "that does not exist in this runtime - the E4.c pre-read census found no map-389 "
                    + "occurrence reaching this mode, so hitting it live is unexpected; refusing to guess "
                    + "a direction instead of silently deviating from the original.");

            case 6: // The PLAYER's current TargetDirection, plus result (not toward-target, unlike mode 3).
            {
                var playerDirection = _worldContext.PlayerEntity?.TargetDirection ?? 0u;
                return (playerDirection + result) & 0x1f;
            }

            case 7: // Warp-departure facing - see GetWarpFacingDirection's own doc.
            {
                var facingDirection = GetWarpFacingDirection(entity);
                return facingDirection == -1 ? result : (uint)((facingDirection + result) & 0x1f);
            }

            default: // Unreachable: encodedDir is always a byte (0..255), so encodedDir >> 5 is 0..7.
                return 0;
        }
    }

    /// <summary>Port of <c>GameEngine.GetWarpFacingDirection</c> (GameEngine.cs:2385-2415, address
    /// 0x8003cf20) - only ever non-trivial for the entity <c>g_activeCollisionEntity</c> currently points
    /// at (the warp trigger an entity just walked into), a piece of state E7's own warp system has not
    /// ported yet. With no concept of "the active collision entity" in this runtime, the original's own
    /// <c>g_activeCollisionEntity != entity</c> guard is always true here, so this always returns -1 -
    /// documented accepted deviation (mode 7 falls back to <c>ResolveDirectionFromParam</c>'s own "no
    /// facing direction" branch, exactly like the original does for every entity that ISN'T mid-warp).
    /// </summary>
    private static int GetWarpFacingDirection(AlundraEntityScriptProxy entity) => -1;
}
