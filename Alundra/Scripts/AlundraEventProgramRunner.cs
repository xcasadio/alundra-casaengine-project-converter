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
public sealed class AlundraEventProgramRunner : IEventProgramRunner
{
    public int ScriptRunCount { get; private set; }
    public int SpriteEventRunCount { get; private set; }

    private readonly EventProgramDocument? _document;
    private readonly byte[]? _codes;
    private readonly AlundraGameState _gameState;

    private readonly HashSet<(int Slot, int ProgramIndex)> _loggedNoOpPrograms = new();
    private readonly HashSet<int> _loggedUnknownOpcodes = new();
    private readonly HashSet<int> _loggedDegradedOpcodes = new();
    private bool _loggedNoDocument;
    private bool _loggedGlobalTableFallback;
    private bool _loggedSpriteEventOnce;

    public AlundraEventProgramRunner(EventProgramDocument? document, AlundraGameState gameState)
    {
        _document = document;
        _codes = document?.CodesAsBytes();
        _gameState = gameState;
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

        var state = new EventProgramState();
        InitializeEventData(entity, programSlot, state);
        RunOneScriptCall(entity, state);
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
    internal void RunOneScriptCall(AlundraEntityScriptProxy entity, EventProgramState state)
    {
        while (true)
        {
            var variables = FillDataFromCommand(state);
            var command = variables[0];

            if (command == 0xFF)
            {
                return;
            }

            if (command == 0x00)
            {
                state.Parameters[1] = 0;
                state.CodeIndex++;
                return;
            }

            var result = Dispatch(command, entity, variables, state);

            if (result == 0)
            {
                return;
            }

            state.Parameters[1] = 0;
            state.CodeIndex += result;
        }
    }

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

            case 0x2D: // Activate entity - Script_45_02D (needs the entity search/spawn system)
                LogDegradedOpcodeOnce(0x2D, "ActivateEntity", "entity search/spawn system");
                return 2;

            case 0x2E: // Destroy entity - Script_46_02E (needs the entity search system)
                LogDegradedOpcodeOnce(0x2E, "DestroyEntity", "entity search system");
                return 2;

            case 0x62: // Set entities flags (low 16 bits) - Script_98_062 (needs the entity search system)
                LogDegradedOpcodeOnce(0x62, "SetEntitiesFlagsLow16", "entity search system");
                return 4;

            case 0x63: // Clear entities flags (low 16 bits) - Script_99_063 (needs the entity search system)
                LogDegradedOpcodeOnce(0x63, "ClearEntitiesFlagsLow16", "entity search system");
                return 4;

            case 0x64: // Set entities position - Script_100_064 (needs the entity search system)
                LogDegradedOpcodeOnce(0x64, "SetEntitiesPosition", "entity search system");
                return 8;

            case 0xAC: // Set entity shadow size - Script_172_0AC (shadow rendering not ported)
                LogDegradedOpcodeOnce(0xAC, "SetEntityShadowSize", "shadow rendering");
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
            if (_loggedUnknownOpcodes.Add(command))
            {
                Logs.WriteWarning(
                    $"AlundraEventProgramRunner: opcode 0x{command:x2} has no known size; terminating "
                    + "this script call (V1 deviation).");
            }

            return 0;
        }

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
        if (_loggedDegradedOpcodes.Add(opcode))
        {
            Logs.WriteDebug(
                $"AlundraEventProgramRunner: opcode 0x{opcode:x2} ({name}) not implemented "
                + $"({missingSystem} not ported yet) - degraded no-op, advancing by its size.");
        }
    }
}
