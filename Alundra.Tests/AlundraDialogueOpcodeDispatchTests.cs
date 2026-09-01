using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Dispatch-level coverage of E12.a's own dialogue opcodes (docs/plan-e12-dialogues.md) on SYNTHETIC
/// bytecode - T2 (0x0D reentrancy guard), T3 (close-mode masks), T5 (degraded mode never deadlocks), T6
/// (control-flag gates), plus local-string masking (0x7F) and the numeric-control-code flag application
/// that T1 (<see cref="AlundraDialogueOpcodesProductionTests"/>) exercises again on the real map 389
/// production path. Uses the REAL, session-scoped <see cref="AlundraDialogueDirector.Instance"/> attached
/// to a real, headless <see cref="DialogueService"/> - no UI/graphics device needed (D-E12-5's own "the
/// engine gets the generic mechanism") - so these tests exercise the SAME production singleton
/// <see cref="AlundraWorldProxy.InstallDialogueSystems"/> wires, reset before/after each test
/// (<see cref="AlundraDialogueDirector.ResetForTests"/>) so no state leaks between tests.
/// </summary>
public class AlundraDialogueOpcodeDispatchTests : IDisposable
{
    public AlundraDialogueOpcodeDispatchTests()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraDialogueTextParser.ResetCountersForTests();
    }

    public void Dispose() => AlundraDialogueDirector.Instance.ResetForTests();

    private static EventProgramDocument NewDocument(params int[] codes)
    {
        return new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
    }

    private static AlundraEventProgramRunner NewRunner(
        EventProgramDocument document, AlundraGameState gameState, IEntityWorldContext? worldContext,
        IReadOnlyList<string>? localStrings = null)
        => new(document, gameState, worldContext) { LocalDialogueStrings = localStrings };

    private static AlundraEntityScriptProxy NewEntity() => new();

    private sealed class FakeEntityWorldContext : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;

        // E12.a: overrides IEntityWorldContext.DialogueDirector's default-interface-member "=> null".
        public IAlundraDialogueDirector? DialogueDirector { get; set; }
    }

    private static EventTraceKind? CaptureKindForOpcode(AlundraEventProgramRunner runner, int opcode, Action run)
    {
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == opcode)
            {
                kind = record.Kind;
            }
        };

        run();
        runner.TraceSink = null;
        return kind;
    }

    private static FakeEntityWorldContext NewRealDialogueContext(AlundraGameState gameState)
    {
        AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), gameState);
        AlundraDialogueDirector.Instance.InstallForMapEntry();
        return new FakeEntityWorldContext { DialogueDirector = AlundraDialogueDirector.Instance };
    }

    // ---- T2: 0x0D reentrancy guard --------------------------------------------------------------

    [Fact]
    public void OpenDialog_0x0D_DispatchedAgainWhileStillOpen_RetriesInsteadOfOpeningASecondBox()
    {
        var gameState = new AlundraGameState();
        var context = NewRealDialogueContext(gameState);

        // 0x0D(textId=0x81,ctrl=1) opens "FIRST"; 0x02 Goto(-3) jumps straight back to the SAME 0x0D -
        // its second dispatch, in the SAME RunOneScriptCall call, must find the box already open and
        // retry (suspend) rather than reopening with textId 0x81 again (still index 1, so a mutation
        // that opened a second time would be invisible on TEXT alone - the codeIndex-not-advancing
        // assertion below is what a "just open a second box" mutation actually breaks).
        var document = NewDocument(0x0D, 0x81, 1, 0x02, 0xFD, 0xFF);
        var runner = NewRunner(document, gameState, context, localStrings: new[] { "zero", "FIRST" });
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.True(AlundraDialogueDirector.Instance.IsOpen);
        Assert.Equal(0, state.CodeIndex); // suspended AT the 0x0D itself - never advanced past it again.
        Assert.Equal("FIRST", AlundraDialogueDirector.Instance.CurrentLineForTests?.Text);
    }

    // ---- T3: close-mode masks --------------------------------------------------------------------

    [Fact]
    public void CloseMask_DefaultMask3_ButtonClosesTheBox()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open("hello", controlMode: 1);
        Assert.Equal(3, director.CloseMaskForTests);

        gameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
        director.Tick();

        Assert.False(director.IsOpen);
    }

    [Fact]
    public void CloseMask_Mask4_ButtonDoesNotClose_OnlyScriptCloseDoes()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open("hello", controlMode: 1);
        director.SetCloseMask(4);

        gameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
        director.Tick();
        Assert.True(director.IsOpen); // button alone must NOT close mask-4 boxes.

        var closed = director.RequestScriptClose();
        Assert.True(closed);
        Assert.False(director.IsOpen);
    }

    [Fact]
    public void EveryOpen_ResetsCloseMaskToDefault3()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open("first", controlMode: 1);
        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());
        Assert.False(director.IsOpen);

        // Mutation target (T3): "ne pas remettre -> T3 tombe" - a SECOND open must reset the mask back
        // to 3, not inherit the 4 the FIRST dialogue left behind.
        director.Open("second", controlMode: 1);
        Assert.Equal(3, director.CloseMaskForTests);
    }

    // ---- T5: degraded mode never deadlocks ---------------------------------------------------------

    [Fact]
    public void Degraded_NoDirectorAtAll_0x44_WritesResultOne_KindDegraded()
    {
        var gameState = new AlundraGameState();
        var context = new FakeEntityWorldContext { DialogueDirector = null };
        var document = NewDocument(0x44, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        var kind = CaptureKindForOpcode(runner, 0x44, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(1, state.Result);
        Assert.Equal(1, state.CodeIndex); // advanced, never suspended - no infinite block.
    }

    [Fact]
    public void Degraded_NoDirectorAtAll_0x39_AlwaysAdvances()
    {
        var gameState = new AlundraGameState();
        var context = new FakeEntityWorldContext { DialogueDirector = null };
        var document = NewDocument(0x39, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x39, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(1, state.CodeIndex);
    }

    [Fact]
    public void Degraded_0x0D_StillParsesAndAppliesNumericFlags_SoALaterWaitDoesNotDeadlock()
    {
        var gameState = new AlundraGameState();
        var context = new FakeEntityWorldContext { DialogueDirector = null };
        var document = NewDocument(0x0D, 0x81, 1, 0xFF);
        var runner = NewRunner(document, gameState, context, localStrings: new[] { "zero", "line\\999end" });
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x0D, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, state.CodeIndex); // still advances (never blocks) even in degraded mode.

        var flag = 999u | 0x8000u;
        var mask = 1u << (999 & 0x1f);
        Assert.NotEqual(0u, gameState.GetFlag(flag) & mask);
    }

    [Fact]
    public void Degraded_0x50And0x51_NoOpButNeverThrow()
    {
        var gameState = new AlundraGameState();
        var context = new FakeEntityWorldContext { DialogueDirector = null };
        var document = NewDocument(0x50, 4, 0x51, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(3, state.CodeIndex); // 0x50 (2) + 0x51 (1) = 3, both advanced without a director.
    }

    // ---- T6: control-flag gates -------------------------------------------------------------------

    [Fact]
    public void ControlMode1_SetsMessageBox_ControlMode0_SetsMenuOpen_CloseClearsBoth()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open("box", controlMode: 1);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MessageBox,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MessageBox);

        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());
        Assert.Equal(
            0u,
            gameState.PlayerControlFlags & (AlundraGameState.PlayerControlBits.MessageBox | AlundraGameState.PlayerControlBits.MenuOpen));

        director.Open("menu box", controlMode: 0);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MenuOpen,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());
        Assert.Equal(
            0u,
            gameState.PlayerControlFlags & (AlundraGameState.PlayerControlBits.MessageBox | AlundraGameState.PlayerControlBits.MenuOpen));
    }

    // ---- local text masking (0x7F) -------------------------------------------------------------

    [Fact]
    public void OpenDialog_0x0D_MasksTextIdWith0x7F_ToIndexLocalStrings()
    {
        var gameState = new AlundraGameState();
        var context = NewRealDialogueContext(gameState);
        var localStrings = new[] { "index0", "index1", "index2" };
        // textId = 0x82 -> masked 0x82 & 0x7f = 2 -> "index2".
        var document = NewDocument(0x0D, 0x82, 1, 0xFF);
        var runner = NewRunner(document, gameState, context, localStrings);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal("index2", AlundraDialogueDirector.Instance.CurrentLineForTests?.Text);
    }

    // ---- 0x44 choice flow, using the real etc-index/global-strings data --------------------------

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("no alundra-project/Maps directory found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Choice_0x44_FirstEntry_OpensRealOuiNonLabels_ThenBlocksUntilSelected_ThenWritesResult()
    {
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            var gameState = new AlundraGameState();
            var context = NewRealDialogueContext(gameState);
            AlundraDialogueDirector.Instance.Open("question", controlMode: 1);

            var document = NewDocument(0x44, 0xFF);
            var runner = NewRunner(document, gameState, context);
            var entity = NewEntity();
            var state = new EventProgramState { Codes = document.CodesAsBytes() };

            // First dispatch: opens the choice, must suspend (CodeIndex stays 0).
            runner.RunOneScriptCall(entity, state);
            Assert.Equal(0, state.CodeIndex);
            Assert.True(AlundraDialogueDirector.Instance.IsAwaitingChoice);
            Assert.Equal(new[] { "OUI", "NON" }, AlundraDialogueDirector.Instance.ChoicesForTests);

            // Second dispatch, still no selection: must keep suspending.
            runner.RunOneScriptCall(entity, state);
            Assert.Equal(0, state.CodeIndex);

            // Simulate the player picking the SECOND option (NON).
            Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(1));

            runner.RunOneScriptCall(entity, state);
            Assert.Equal(1, state.CodeIndex); // advances (instruction size 1).
            Assert.Equal(0, state.Result); // NOT the first option.
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void Choice_0x44_FirstOptionSelected_ResultOne()
    {
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = FindProjectRoot();
        try
        {
            var gameState = new AlundraGameState();
            var context = NewRealDialogueContext(gameState);
            AlundraDialogueDirector.Instance.Open("question", controlMode: 1);

            var document = NewDocument(0x44, 0xFF);
            var runner = NewRunner(document, gameState, context);
            var entity = NewEntity();
            var state = new EventProgramState { Codes = document.CodesAsBytes() };

            runner.RunOneScriptCall(entity, state); // opens
            Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(0)); // OUI

            runner.RunOneScriptCall(entity, state);
            Assert.Equal(1, state.CodeIndex);
            Assert.Equal(1, state.Result);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }
}
