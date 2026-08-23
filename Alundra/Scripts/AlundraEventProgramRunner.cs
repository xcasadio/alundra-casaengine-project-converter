#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// V1 of the event-program bytecode interpreter (see docs on <see cref="IEventProgramRunner"/>):
/// interprets slot A (Load) programs to completion, faithfully porting
/// AlundraEngine.Gameplay.Scripts.EntityEventHandlers.RunScript @ 0x8004205c and its
/// InitializeEventData helper @ 0x80041ee4. Every other slot (B-F) and <see cref="RunSpriteEvent"/>
/// stay a counted no-op with a one-shot debug log per (slot, program index) - porting the ~120 native
/// sprite AI handlers (SpriteEventHandlers.cs) and slots B-F's own bytecode semantics is a later
/// chantier (see the class docs on <see cref="IEventProgramRunner"/> and this brief's scope).
///
/// Slot A always re-initializes on every entry (see <see cref="RunScript"/>'s own doc comment): the
/// original's RunScript only resumes the entity's own persisted <c>EventProgramState</c> for slots
/// B (Map) and C (Tick) - every other slot, including A, always falls into InitializeEventData, using
/// what amounts to a throwaway per-call state. Combined with the pick phase only ever offering slot A
/// once per entity (the Loaded -&gt; Normal transition happens in the very same pick pass that chose
/// slot A - see <c>AlundraWorldProxy.RunEntityEventsPass</c>), a Load program that suspends (returns 0,
/// e.g. via 0x37 Wait) simply never resumes: it stops there for good. This is faithful to the original,
/// not a V1 shortcut - map 389's own Load programs never hit a suspending opcode, so this never fires
/// there in practice.
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

    private readonly HashSet<(int Slot, int ProgramIndex)> _loggedNoOpPrograms = new();
    private readonly HashSet<int> _loggedUnknownOpcodes = new();
    private readonly HashSet<int> _loggedDegradedOpcodes = new();
    private readonly HashSet<int> _loggedFailedActivations = new();
    private bool _loggedNoDocument;
    private bool _loggedGlobalTableFallback;
    private bool _loggedSpriteEventOnce;

    /// <summary>
    /// Slot A's shared scratch <see cref="EventProgramState"/> - the original never gives Load its own
    /// per-entity state, it always runs off <c>_gameEngine.StaticVariables.g_eventProgramState</c>
    /// (EntityEventHandlers.cs:234), one instance shared by every non-resuming slot call. Reused (not
    /// reconstructed) by every <see cref="RunScript"/> call for slot A so that <see cref="EventProgramState.Result"/>
    /// - never cleared by <see cref="InitializeEventData"/> - carries across sequential slot-A calls
    /// exactly like the original: a Load program that starts with a conditional opcode reading a
    /// <c>Result</c> a previous, unrelated Load call happened to leave behind sees that same stale value.
    /// A V1 deviation only in that this runtime has one runner (hence one shared state) per world rather
    /// than one process-wide global, which does not matter in practice since only one world is ever
    /// interpreting slot A programs at a time.
    /// </summary>
    private readonly EventProgramState _slotAScratchState = new();

    public AlundraEventProgramRunner(EventProgramDocument? document, AlundraGameState gameState, IEntityWorldContext? worldContext = null)
    {
        _document = document;
        _codes = document?.CodesAsBytes();
        _gameState = gameState;
        _worldContext = worldContext ?? NoOpEntityWorldContext.Instance;
    }

    public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
    {
        ScriptRunCount++;

        if (programSlot != ScriptHelper.ProgramALoad)
        {
            var programIndex = entity.ProgramIndexes[programSlot];
            if (_loggedNoOpPrograms.Add((programSlot, programIndex)))
            {
                Logs.WriteDebug(
                    $"AlundraEventProgramRunner: slot {programSlot} program {programIndex} not "
                    + "interpreted (V1 interprets slot A only) - counted no-op.");
            }

            return;
        }

        if (_document == null || _codes == null)
        {
            if (!_loggedNoDocument)
            {
                _loggedNoDocument = true;
                Logs.WriteWarning(
                    "AlundraEventProgramRunner: no event-code document loaded for this world; Load "
                    + "programs are a counted no-op (degraded mode).");
            }

            return;
        }

        InitializeEventData(entity, programSlot, _slotAScratchState);
        RunOneScriptCall(entity, _slotAScratchState, programSlot);
    }

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

    /// <summary>Port of EntityEventHandlers.FillDataFromCommand (EntityEventHandlers.cs:400-417).</summary>
    internal static int[] FillDataFromCommand(EventProgramState state)
    {
        if (state.Codes == null || state.CodeIndex >= state.Codes.Length)
        {
            return new[] { 0xFF };
        }

        state.Sp = state.Codes[state.CodeIndex];
        var variables = new int[10];
        var length = Math.Min(10, state.Codes.Length - state.CodeIndex);
        for (var i = 0; i < length; i++)
        {
            variables[i] = state.Codes[state.CodeIndex + i];
        }

        return variables;
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

            case 0x09: // Set direction - Script_9_009
                entity.TargetDirection = (uint)(v[1] & 0x1f);
                return 2;

            case 0x17: // Low gravity - Script_23_017
                entity.Flags &= ~EntityFlags.Gravity;
                return 1;

            case 0x1A: // Set anim - Script_26_01A
                entity.TargetAnimationId = (uint)v[1];
                return 2;

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

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);

        if (spawned != null && matches.Count != 0)
        {
            spawned.PosX = matches[0].PosX + ((v[3] + v[4] * 0x100) << 16);
            spawned.PosY = matches[0].PosY + ((v[5] + v[6] * 0x100) << 16);
            spawned.PosZ = matches[0].PosZ + ((v[7] + v[8] * 0x100) << 16);
        }
    }

    /// <summary>Script_46_02E (0x2E DestroyEntity) - destroys every entity matched by
    /// <c>variables[1]</c>'s search type, and sets <see cref="EventProgramState.Result"/> to 1 if at
    /// least one entity matched (0 otherwise) - read by a following conditional-goto opcode.</summary>
    private void DestroyMatchingEntities(AlundraEntityScriptProxy entity, int[] v, EventProgramState state)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);
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
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);

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
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);
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
    /// fields, exactly like the original.</summary>
    private void SetEntitiesPosition(AlundraEntityScriptProxy entity, int[] v)
    {
        var x = ((v[3] << 8) | v[2]) << 16;
        var y = ((v[5] << 8) | v[4]) << 16;
        var z = (((v[7] << 8) | v[6]) << 16) + 1;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);

        foreach (var match in matches)
        {
            match.PosX = x;
            match.PosY = y;
            match.PosZ = z;
        }
    }

    /// <summary>Script_101_065 (0x65) - adds a raw 16.16 fixed-point offset to PosX/PosY/PosZ of every
    /// matched entity (note the original's little-endian byte order here is the mirror image of 0x64's -
    /// <c>v[2]|(v[3]&lt;&lt;8)</c> vs 0x64's <c>(v[3]&lt;&lt;8)|v[2]</c>, same value either way but ported
    /// verbatim for fidelity).</summary>
    private void AddEntitiesPositionOffset(AlundraEntityScriptProxy entity, int[] v)
    {
        var x = (v[2] | (v[3] << 8)) << 16;
        var y = (v[4] | (v[5] << 8)) << 16;
        var z = (v[6] | (v[7] << 8)) << 16;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);

        foreach (var match in matches)
        {
            match.PosX += x;
            match.PosY += y;
            match.PosZ += z;
        }
    }

    /// <summary>Script_172_0AC (0xAC) - rewrites the shadow-size bits of the FIRST matched entity's
    /// <see cref="AlundraEntityScriptProxy.Flags"/> only (the original reads
    /// <c>g_matchingEntitiesBuffer[0]</c> unconditionally when <c>count != 0</c>, every other match is
    /// ignored). Actual shadow drawing stays unported (no shadow renderer yet) - this only reproduces the
    /// flag write, which is all following searches/reads of the shadow-size bits can observe.</summary>
    private void SetEntityShadowSize(AlundraEntityScriptProxy entity, int[] v)
    {
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities);

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
}
