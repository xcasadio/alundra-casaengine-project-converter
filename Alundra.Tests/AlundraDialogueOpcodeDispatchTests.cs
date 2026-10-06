using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Assets;
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
        AlundraEtcStringTable.ResetForTests(); // E15.c T6: the 0x44 choice-flow tests inject an ETC asset.
    }

    public void Dispose()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraEtcStringTable.ResetForTests();
    }

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
        DialogueAsset? mapAsset = null)
        => new(document, gameState, worldContext) { MapDialogueAsset = mapAsset };

    private static AlundraEntityScriptProxy NewEntity() => new();

    /// <summary>E15.c T5: builds a map-134 (see <see cref="NewDocument"/>'s own <c>MapIndex = 1</c>) Yarn
    /// asset with one single-page node <c>M1_S{index:000}</c> per entry - replaces the pre-E15.c raw
    /// <c>LocalDialogueStrings</c> array these dispatch tests used to inject directly.</summary>
    private static DialogueAsset MapAsset(params (int Index, string Text)[] entries)
    {
        var compiler = new YarnDialogueCompiler();
        var source = string.Concat(entries.Select(e =>
            $"title: M1_S{e.Index:000}\n---\n{e.Text} #line:M1_S{e.Index:000}_p0\n===\n"));
        var result = compiler.CompileString(source, "MapAsset.yarn", AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram("MapAsset", $"M1_S{entries[0].Index:000}", result.ProgramBytes, result.LineTexts);
    }

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
        var runner = NewRunner(document, gameState, context, MapAsset((1, "FIRST")));
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

        director.Open(DialogueTestAssets.SinglePage("Hello", "hello"), "Start", controlMode: 1);
        Assert.Equal(3, director.CloseMaskForTests);

        // E19.f2a: "hello" is typed from the pass 19 and ends at 39; a press the box sees at the pass 40 triggers the close, and the box is released
        // 18 passes later - open after the pass 57, closed at the pass 58.
        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.PressSeenAt(40);
        passes.RunTo(57);
        Assert.True(director.IsOpen);
        passes.RunTo(58);

        Assert.False(director.IsOpen);
    }

    [Fact]
    public void CloseMask_Mask4_ButtonDoesNotClose_OnlyScriptCloseDoes()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open(DialogueTestAssets.SinglePage("Hello", "hello"), "Start", controlMode: 1);
        director.SetCloseMask(4);

        // The press of the old test was written before the first tick: the box sees it at the pass 2, while it still slides in. It changes nothing, and the
        // button alone never closes a mask-4 box.
        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.PressSeenAt(2);
        passes.RunTo(2);
        Assert.True(director.IsOpen); // button alone must NOT close mask-4 boxes.

        // E19.f2a: 0x51 is a latch the box takes once its typing is done: "hello" ends at 39, the latch triggers the close at 40, the release is at 58.
        var closed = director.RequestScriptClose();
        Assert.True(closed);
        Assert.True(director.IsOpen);
        passes.RunTo(57);
        Assert.True(director.IsOpen);
        passes.RunTo(58);
        Assert.False(director.IsOpen);
    }

    [Fact]
    public void EveryOpen_ResetsCloseMaskToDefault3()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open(DialogueTestAssets.SinglePage("First", "first"), "Start", controlMode: 1);
        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());
        Assert.True(director.IsOpen);

        // E19.f2a: the latch is set before the first pass, "first" ends at 39, the close triggers at 40 and the box is released at 58.
        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.RunTo(57);
        Assert.True(director.IsOpen);
        passes.RunTo(58);
        Assert.False(director.IsOpen);

        // Mutation target (T3): "ne pas remettre -> T3 tombe" - a SECOND open must reset the mask back
        // to 3, not inherit the 4 the FIRST dialogue left behind.
        director.Open(DialogueTestAssets.SinglePage("Second", "second"), "Start", controlMode: 1);
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

    /// <summary>E19.f3a: a director without a presenter (<c>HasPresenter</c> false) is the degraded mode of 0x44 too - pinned before the choice box became the
    /// director's own (the choice box needs no presenter, but the opcode still gates on it): Result 1 at once, no choice opened.</summary>
    [Fact]
    public void Degraded_ADirectorWithoutAPresenter_0x44_WritesResultOne_AndOpensNoChoice()
    {
        var gameState = new AlundraGameState();
        AlundraDialogueDirector.Instance.AttachToWorld(null, gameState);
        AlundraDialogueDirector.Instance.InstallForMapEntry();
        Assert.False(AlundraDialogueDirector.Instance.HasPresenter);
        var context = new FakeEntityWorldContext { DialogueDirector = AlundraDialogueDirector.Instance };
        var document = NewDocument(0x44, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 0 };

        var kind = CaptureKindForOpcode(runner, 0x44, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(1, state.Result);
        Assert.Equal(1, state.CodeIndex);
        Assert.False(AlundraDialogueDirector.Instance.IsAwaitingChoice);
        Assert.Empty(AlundraDialogueDirector.Instance.ChoicesForTests);
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
        // The original's "\999" mid-string numeric code becomes a <<flag 999>> command before the line
        // (docs/plan-e15-yarn.md §1) - item 8's headless play must still run it before advancing.
        var compiler = new YarnDialogueCompiler();
        var result = compiler.CompileString(
            "title: M1_S001\n---\n<<flag 999>>\nline end #line:M1_S001_p0\n===\n",
            "Map1S001.yarn",
            AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        var mapAsset = DialogueAsset.FromCompiledProgram("Map1S001", "M1_S001", result.ProgramBytes, result.LineTexts);
        var runner = NewRunner(document, gameState, context, mapAsset);
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

    // ---- E19.f2a F2-R4: 0x4C to 0x4F ------------------------------------------------------------------

    [Fact]
    public void Opcodes0x4CTo0x4F_AreExecutedBySize_NoneGivesTheHandBack_AndTheyReachTheBox()
    {
        var gameState = new AlundraGameState();
        var context = NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;
        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 1);

        // 0x4C [4] (size 2: manual typing), 0x4D (size 1: one step latched), 0x4E [6] (size 2), 0x4F (size 1), 0x50 [4] (size 2), 0x51 (size 1): nine bytes
        // and one more instruction, in one call - none of them suspends the script.
        var document = NewDocument(0x4C, 4, 0x4D, 0x4E, 6, 0x4F, 0x50, 4, 0x51, 0x1A, 9, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(9u, entity.TargetAnimationId); // the instruction after the six was reached in the same call
        Assert.Equal(4, director.Box.TextFlags);
        Assert.Equal(6, director.Box.ScrollMode);
        Assert.Equal(4, director.CloseMaskForTests);

        // The 0x4D latched while the text flags say a 0x4D gates (bit 4): the first call of the interpreter, the pass 19, types a letter and nothing else ever does.
        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.RunTo(18);
        Assert.Equal(0, director.Box.GlyphCount);
        passes.RunTo(19);
        Assert.Equal(1, director.Box.GlyphCount);
        passes.RunTo(80);
        Assert.Equal(1, director.Box.GlyphCount);
    }

    [Fact]
    public void Degraded_0x4CTo0x4F_NoDirector_AdvanceBySizeWithoutThrowing()
    {
        var gameState = new AlundraGameState();
        var context = new FakeEntityWorldContext { DialogueDirector = null };
        var document = NewDocument(0x4C, 4, 0x4D, 0x4E, 6, 0x4F, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal(6, state.CodeIndex); // 2 + 1 + 2 + 1, the 0xFF at 6.
    }

    // ---- T6: control-flag gates -------------------------------------------------------------------

    [Fact]
    public void ControlMode1_SetsMessageBox_ControlMode0_SetsMenuOpen_CloseClearsBoth()
    {
        var gameState = new AlundraGameState();
        NewRealDialogueContext(gameState);
        var director = AlundraDialogueDirector.Instance;

        director.Open(DialogueTestAssets.SinglePage("Box", "box"), "Start", controlMode: 1);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MessageBox,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MessageBox);

        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());

        // E19.f2a: the close is the box's, not the opcode's: "box" ends at 31, the latch triggers the close at 32, and MessageBox falls with the release at the
        // pass 50 (counted from this opening).
        var boxPasses = new DialogueBoxPassDriver(director, gameState);
        boxPasses.RunTo(49);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MessageBox,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MessageBox);
        boxPasses.RunTo(50);
        Assert.Equal(
            0u,
            gameState.PlayerControlFlags & (AlundraGameState.PlayerControlBits.MessageBox | AlundraGameState.PlayerControlBits.MenuOpen));

        director.Open(DialogueTestAssets.SinglePage("MenuBox", "menu box"), "Start", controlMode: 0);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MenuOpen,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        director.SetCloseMask(4);
        Assert.True(director.RequestScriptClose());

        // "menu box" ends at 51, the close triggers at 52 and MenuOpen falls with the release at the pass 70.
        var menuPasses = new DialogueBoxPassDriver(director, gameState);
        menuPasses.RunTo(69);
        Assert.Equal(
            AlundraGameState.PlayerControlBits.MenuOpen,
            gameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);
        menuPasses.RunTo(70);
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
        var mapAsset = MapAsset((0, "index0"), (1, "index1"), (2, "index2"));
        // textId = 0x82 -> masked 0x82 & 0x7f = 2 -> "index2".
        var document = NewDocument(0x0D, 0x82, 1, 0xFF);
        var runner = NewRunner(document, gameState, context, mapAsset);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal("index2", AlundraDialogueDirector.Instance.CurrentLineForTests?.Text);
    }

    // ---- 0x44 choice flow, using the real exported Dialogues/Etc.dialogue --------------------------

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
        // E15.c T6: OUI/NON now come from the real exported dialogue_etc asset, injected directly -
        // AlundraEtcStringTable no longer reads Dialogues/etc-index.json/global-strings.json off
        // EngineEnvironment.ProjectPath.
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        var gameState = new AlundraGameState();
        var context = NewRealDialogueContext(gameState);
        AlundraDialogueDirector.Instance.Open(DialogueTestAssets.SinglePage("Question", "question"), "Start", controlMode: 1);

        var document = NewDocument(0x44, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        // First dispatch: opens the choice, must suspend (CodeIndex stays 0).
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(0, state.CodeIndex);
        Assert.True(AlundraDialogueDirector.Instance.IsAwaitingChoice);
        Assert.Equal(new[] { "OUI", "NON" }, AlundraDialogueDirector.Instance.ChoicesForTests);

        // Second dispatch, still no selection: must keep suspending. (E19.f3a: each dispatch is one logic tick: the pass of the choice box, then the script.)
        AlundraDialogueDirector.Instance.Tick();
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(0, state.CodeIndex);

        // Simulate the player picking the SECOND option (NON): Right at the first interactive pass, Cross at the next.
        Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(1));

        var ticks = 1; // the opener is the tick 0
        while (state.CodeIndex == 0 && ticks < 150)
        {
            AlundraDialogueDirector.Instance.Tick();
            runner.RunOneScriptCall(entity, state);
            ticks++;
        }

        Assert.Equal(38, ticks); // NON is resolved at the 38th tick after the opener's
        Assert.Equal(1, state.CodeIndex); // advances (instruction size 1).
        Assert.Equal(0, state.Result); // NOT the first option.
    }

    [Fact]
    public void Choice_0x44_FirstOptionSelected_ResultOne()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(FindProjectRoot(), "Dialogues", "Etc.dialogue")));
        var gameState = new AlundraGameState();
        var context = NewRealDialogueContext(gameState);
        AlundraDialogueDirector.Instance.Open(DialogueTestAssets.SinglePage("Question", "question"), "Start", controlMode: 1);

        var document = NewDocument(0x44, 0xFF);
        var runner = NewRunner(document, gameState, context);
        var entity = NewEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state); // opens
        Assert.True(AlundraDialogueDirector.Instance.SelectChoiceForTests(0)); // OUI

        var ticks = 0; // the opener is the tick 0
        while (state.CodeIndex == 0 && ticks < 150)
        {
            AlundraDialogueDirector.Instance.Tick(); // E19.f3a: one logic tick: the pass of the choice box, then the script
            runner.RunOneScriptCall(entity, state);
            ticks++;
        }

        Assert.Equal(37, ticks); // OUI is resolved at the 37th tick after the opener's
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(1, state.Result);
    }
}
