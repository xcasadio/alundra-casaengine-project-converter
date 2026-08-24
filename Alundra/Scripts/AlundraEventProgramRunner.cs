#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;

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

            case 0x10: // Player lose control - Script_16_010 (EntityEventHandlers.cs:680-684): the full
                       // engine bridge (PlayerInput.IsInputEnable/CharacterControlMode) is E6 - E4 only
                       // ports the flag store, which AlundraPlayerManager.MovePlayer's own
                       // InputBlockedMask gate already reads.
                _gameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
                return 1;

            case 0x11: // Player gain control - Script_17_011 (EntityEventHandlers.cs:686-690).
                _gameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.ControlLocked;
                return 1;

            case 0x16: // High gravity - Script_22_016
                entity.Flags |= EntityFlags.Gravity;
                entity.ApplyGravitySettingsToController();
                return 1;

            case 0x17: // Low gravity - Script_23_017
                entity.Flags &= ~EntityFlags.Gravity;
                entity.ApplyGravitySettingsToController();
                return 1;

            case 0x19: // Deactivate entity - Script_25_019 (EntityEventHandlers.cs:729-733).
                entity.Status = EntityStatus.Deactivated;
                return 1;

            case 0x1A: // Set anim - Script_26_01A
                entity.TargetAnimationId = (uint)v[1];
                return 2;

            case 0x1B: // Fly - Script_27_01B (EntityEventHandlers.cs:743-747): ForceZ = (((v2<<8)|v1) *
                       // 0x10000) >> 8, a signed 16.16 vertical impulse. E4.b (docs/plan-e4-deplacement-
                       // scripte.md): stores it on the proxy AND, when this entity has a controller, pushes
                       // it onto CharacterControllerComponent.SetVerticalVelocity (E4.0) - the controller's
                       // own vertical axis is in px/s, ForceZ is 16.16 px/tick at the original's 50 Hz
                       // tick rate, hence the *50f/65536f conversion. Without a controller (bare-fallback
                       // spawn, or the intro trace harness - E4.e still owns that simulated kinematics),
                       // ForceZ alone is kept, same as before this opcode was ported.
                entity.ForceZ = SignExtend16(((v[2] << 8) | v[1])) * 0x10000 >> 8;
                entity.Controller?.SetVerticalVelocity(entity.ForceZ * 50f / 65536f);
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

            case 0xBD: // Play sound 2 - Script_189_0BD (sound system not wired to the interpreter)
                LogDegradedOpcodeOnce(0xBD, "PlaySound2", "sound system");
                return 3;

            case 0x70: // Is above ground - Script_112_070 (EntityEventHandlers.cs:2161-2165): Result =
                       // logicEntity.IsOnGround, pulled from the controller every frame by
                       // AlundraEntityScriptProxy.Update's own root pull (E3.d) - 0 (falls) for an entity
                       // with no controller yet, same default the field already carries.
                state.Result = entity.IsOnGround;
                return 1;

            default:
                return UnknownOpcode(command, state);
        }
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
    /// matched entity's <see cref="AlundraEntityScriptProxy.Flags"/>.</summary>
    private void SetEntitiesFlagsLow16(AlundraEntityScriptProxy entity, int[] v)
    {
        var flag = (uint)((v[3] << 8) | v[2]);
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);

        foreach (var match in matches)
        {
            match.Flags |= flag;
        }
    }

    /// <summary>Script_99_063 (0x63) - clears the low-16-bit flag word <c>(v[3]&lt;&lt;8|v[2])</c> out of
    /// every matched entity's <see cref="AlundraEntityScriptProxy.Flags"/> (bits 16-31 always
    /// survive).</summary>
    private void ClearEntitiesFlagsLow16(AlundraEntityScriptProxy entity, int[] v)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);
        var clearMask = (uint)((v[3] << 8) | v[2]);
        var andMask = 0xFFFF0000u | (~clearMask & 0xFFFFu);

        foreach (var match in matches)
        {
            match.Flags &= andMask;
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
