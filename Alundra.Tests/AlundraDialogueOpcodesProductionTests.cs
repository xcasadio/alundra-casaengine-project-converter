using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T1 (docs/plan-e12-dialogues.md, slice E12.a): drives the REAL production call site - sailor 12's own
/// Tick program (map 389, masked index 12, offset 1356) via
/// <see cref="AlundraEntityScriptProxy.Update"/> -&gt; <c>PickEventTrigger</c> -&gt; <c>RunPickedEvent</c>,
/// in a FRAME LOOP crossing the pick phase every iteration (<see cref="HeadlessIntroSimulation.RunFramesForTest"/>'s
/// own per-entity <c>entity.Update</c> call - NOT <c>AlundraWorldProxy.RunPendingEventTriggers</c>'s D3
/// catch-up), exactly like <see cref="AlundraSoundOpcodesProductionTests"/> does for E11.a.
///
/// SAILOR-12 ENTRY GUARD (§1.4): the Tick program's own first instruction (offset 1356, <c>0x30 If flag
/// on 0x35C(=860)</c>) only reaches the dialogue body when flag 860 - the intro-completion flag the golden
/// oracle itself pins at frame 1704 - is ON; without it the program jumps to 1433 and nothing opens
/// (negative case below). <c>0x800C</c> (posed by the sailor's own F(Interact) program,
/// <c>0x05 FlagOn 0x800C</c>) is what the following <c>0x36</c> waits on - both are SEEDED directly on
/// this harness's own <see cref="AlundraGameState"/>, standing in for "the intro just finished AND the
/// player just interacted" without needing to actually simulate 1704 frames of intro first.
/// </summary>
public class AlundraDialogueOpcodesProductionTests : IDisposable
{
    private const string WorldName = "Ship Klark (beginning)-389";

    // Flags 860 (0x35C) and 0x800C - see this class's own doc. Masks/indices match
    // AlundraGameState.GetFlag's own (flag>>5)&0x3ff / 1<<(flag&0x1f) formula exactly (the SAME formula
    // opcodes 0x30/0x36 themselves use against these exact raw operand bytes - docs/intro-programs-389.txt
    // offset 1356/1365).
    private const uint IntroCompletionFlag = 860;
    private const uint InteractArmedFlag = 0x800C;

    public AlundraDialogueOpcodesProductionTests()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
    }

    public void Dispose() => AlundraDialogueDirector.Instance.ResetForTests();

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

        throw new InvalidOperationException(
            $"AlundraDialogueOpcodesProductionTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export of map 389.");
    }

    private static void SeedFlag(AlundraGameState gameState, uint flag)
        => gameState.AddFlag(flag, 1u << (int)(flag & 0x1f));

    [Fact]
    public void SailorTwelve_FirstVisit_FullOuiNonFlow_AcrossRealFrames()
    {
        var projectRoot = FindProjectRoot();
        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installDialogueDirector: true);

            var director = AlundraDialogueDirector.Instance;
            var reachedOpen = false;
            var reachedAwaitingChoice = false;
            var choiceMade = false;
            var reachedFollowUpBox = false;
            var sawNumericFlagSet = false;
            var tempFlag = 999u | 0x8000u;
            var tempFlagMask = 1u << (999 & 0x1f);

            sim.RunFramesForTest(400, s =>
            {
                // Seed the entry guard + interact flags on frame 1, BEFORE the sailor's own Tick program
                // dispatches for the first time this run (T1's own negative-case twin below proves this
                // seeding is load-bearing).
                if (s.Frame == 1)
                {
                    AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), s.GameState);
                    AlundraDialogueDirector.Instance.InstallForMapEntry();
                    SeedFlag(s.GameState, IntroCompletionFlag);
                    SeedFlag(s.GameState, InteractArmedFlag);
                }

                // Clear the one-shot "just pressed" pad snapshot every frame (a real pad only reports a
                // fresh press once) - re-armed below only on the exact frame it is needed.
                s.GameState.LastPadState = default;

                if (director.IsOpen && !reachedOpen)
                {
                    reachedOpen = true;
                    // §1.4: the question is the chaîne d'index 1 ("Qu'est-ce que tu veux...").
                    Assert.Contains("Qu'est-ce que tu veux", director.CurrentLineForTests?.Text ?? "");
                }

                // \999 (index-1 string) must set its temporary flag the moment the page displays - the
                // EXACT flag the 0x36 between 0x50 and 0x44 waits on (0x83E7). Captured DURING the run,
                // not after: the script's own cleanup (0x06 Flag off [231,131], pc=1418) clears this same
                // flag again once the follow-up box closes, so checking only at the very end would see a
                // false negative.
                if (!sawNumericFlagSet && (s.GameState.GetFlag(tempFlag) & tempFlagMask) != 0)
                {
                    sawNumericFlagSet = true;
                }

                if (director.IsAwaitingChoice)
                {
                    reachedAwaitingChoice = true;

                    if (!choiceMade)
                    {
                        // Assert the exact OUI/NON labels (etc-index[0x43]=3656->"OUI", [0x44]=3660->"NON")
                        // BEFORE consuming the selection - state must have survived the suspension.
                        Assert.Equal(new[] { "OUI", "NON" }, director.ChoicesForTests);
                        Assert.True(director.SelectChoiceForTests(0)); // choice 1 (OUI) -> Result=1 -> idx2.
                        choiceMade = true;
                    }
                }

                if (choiceMade && director.IsOpen && !director.IsAwaitingChoice && !reachedFollowUpBox)
                {
                    reachedFollowUpBox = true;
                    // idx2's own text ("...CINQUIEME fois...") - confirms the OUI branch was taken.
                    Assert.Contains("CINQUIEME", director.CurrentLineForTests?.Text ?? "");

                    // Press the interact button on THIS frame to close the follow-up box via 0x39's own
                    // button-close path (default mask 3).
                    s.GameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
                }
            });

            Assert.True(reachedOpen, "the dialogue never opened - the 860/0x800C entry guard was not crossed.");
            Assert.True(reachedAwaitingChoice, "0x44 never opened the OUI/NON choice.");
            Assert.True(choiceMade, "the choice was never resolved.");
            Assert.True(reachedFollowUpBox, "the post-choice follow-up box (idx2) never opened.");

            Assert.True(sawNumericFlagSet, "the \\999 numeric control code never set its temporary flag (0x83E7).");

            // Player-control flags: 0x10 (Player lose control) ran right after the 860 gate, and the
            // dialogue itself posed MessageBox (controlMode=1) - both cleared again by the time the
            // follow-up box's own 0x39/0x06 sequence finishes (idx2/idx3 close, then 0x06 clears 0x800C,
            // 0x11 regains control).
            Assert.False(director.IsOpen, "the follow-up box should have closed on the simulated button press.");
        }
        finally
        {
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    /// <summary>
    /// The closing verifier's F1 (P1), on the REAL bytes: sailors 11/13-17 have NO Tick dialogue program
    /// at all - their whole interaction is the mono-line F(Interact) program
    /// <c>0x27 ; 0x0D [textId, ctrl=0] ; 0x05 ; 0xFF</c> (sailor 13's sits at code offset 1660), which
    /// opens the box with MenuOpen and RUNS TO ITS END in the same call (0x0D advances immediately after
    /// opening). No script is left running, so nothing ever pumped the box's advance/close while that
    /// lived only in 0x39's dispatch: the box never closed, MenuOpen never lifted - six of the seven
    /// sailors were a permanent softlock. The fix runs the pass once per logic tick from the frame loop
    /// (AlundraWorldProxy.Update in production, mirrored by RunFramesForTest here - the production call
    /// site itself is pinned by <see cref="AlundraDialogueFramePassTests"/>).
    /// </summary>
    [Fact]
    public void SailorThirteen_MonoLineInteractProgram_BoxClosesOnButton_NoSoftlock()
    {
        var projectRoot = FindProjectRoot();
        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        // The verifier's own ground truth: masked F index 13 resolves to code offset 1660 in the real
        // map-389 export - the exact program dumped as "0x27 ; 0x0D ; 0x05 ; 0xFF".
        Assert.Equal(1660, document!.EventCodesFTable[13]);

        var previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document, installDialogueDirector: true);
            var director = AlundraDialogueDirector.Instance;

            AlundraEntityScriptProxy? sailorThirteen = null;
            var openedWithMenuOpen = false;
            var stillOpenLongAfterProgramEnded = false;
            var closedOnButton = false;
            var reopenedAfterClose = false;

            sim.RunFramesForTest(60, s =>
            {
                s.GameState.LastPadState = default;

                if (s.Frame == 1)
                {
                    AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), s.GameState);
                    AlundraDialogueDirector.Instance.InstallForMapEntry();
                }

                if (s.Frame == 2)
                {
                    // The interact dispatch itself (EntityEventHandlers' slot F), on the spawned sailor
                    // whose own F program index is 13 - the real runner, the real bytes at 1660.
                    sailorThirteen = System.Linq.Enumerable.Single(
                        s.SpawnedEntities,
                        e => (e.ProgramIndexes[ScriptHelper.ProgramFInteract] & 0x7f) == 13);
                    s.Runner.RunScript(sailorThirteen, ScriptHelper.ProgramFInteract);

                    openedWithMenuOpen = director.IsOpen
                        && (s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0;
                }

                // Frames 3..38: the program is long over (0xFF), no 0x39 anywhere, no button - only the
                // frame pass is looking at the box, and without a button it must simply stay open (the
                // 360-tick auto-timer is far away).
                if (s.Frame == 38)
                {
                    stillOpenLongAfterProgramEnded = director.IsOpen;
                }

                // Frames 39..47: press interact each frame while the box is open - a page a press until
                // the last page's press closes it (each press is seen by the NEXT frame's per-frame
                // tick, which runs before this hook).
                if (s.Frame is >= 39 and < 48 && director.IsOpen)
                {
                    s.GameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
                }

                if (s.Frame is >= 40 and < 50 && !closedOnButton && !director.IsOpen)
                {
                    closedOnButton =
                        (s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) == 0;
                }

                // And the world is NOT wedged: the same interaction works again (pre-fix, the stale
                // open box made every later 0x0D retry forever).
                if (s.Frame == 50)
                {
                    s.Runner.RunScript(sailorThirteen!, ScriptHelper.ProgramFInteract);
                    reopenedAfterClose = director.IsOpen;
                }
            });

            Assert.True(openedWithMenuOpen, "sailor 13's F(Interact) program did not open the box with MenuOpen posed.");
            Assert.True(stillOpenLongAfterProgramEnded, "the box closed on its own with no button - not this scenario.");
            Assert.True(closedOnButton, "F1 regression: the interact button did not close a box no script was watching.");
            Assert.True(reopenedAfterClose, "the dialogue system stayed wedged after the first box closed.");
        }
        finally
        {
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }

    [Fact]
    public void SailorTwelve_NegativeCase_WithoutIntroCompletionFlag_NothingOpens()
    {
        var projectRoot = FindProjectRoot();
        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = projectRoot;
        try
        {
            var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installDialogueDirector: true);
            var director = AlundraDialogueDirector.Instance;

            sim.RunFramesForTest(200, s =>
            {
                // Seed ONLY 0x800C (the interact flag) - deliberately WITHOUT flag 860 (§1.4's own entry
                // guard). The Tick program must take the 1433 branch instead, and the dialogue must never
                // open - this is the exact negative case the plan calls out as load-bearing.
                if (s.Frame == 1)
                {
                    AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), s.GameState);
                    AlundraDialogueDirector.Instance.InstallForMapEntry();
                    SeedFlag(s.GameState, InteractArmedFlag);
                }
            });

            Assert.False(director.IsOpen, "the dialogue opened even though the intro-completion flag (860) was never seeded.");
            Assert.False(director.IsAwaitingChoice);
        }
        finally
        {
            CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = previousProjectPath;
        }
    }
}
