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

    /// <summary>
    /// E12.d T1 (docs/plan-e12d-interaction-joueur.md §3 étage 2): the WHOLE interaction chain on the
    /// real map-389 world - contact detection → CheckEntityInteraction → ActiveCollisionEntity → the
    /// REAL slot-F pick (the sim's own per-entity Update, not a direct RunScript) → 0x27+0x0D → the
    /// dialogue opens, survives its own opening press (D-E12D-6), closes on later presses, and NEVER
    /// reopens uncommanded (consume-on-pick, D-E12D-4). The per-frame MovePlayer call and contact pass
    /// here are the harness MIRROR of the two production sites <see cref="AlundraInteractionPassTests"/>
    /// pins (the F1 contract: a mirror only stands when the production site carries its own test).
    /// Mirror order matches production phase: MovePlayer consumes the PREVIOUS frame's contact
    /// (events before physics in the original, EntityManager.cs:377-387), so the hook runs MovePlayer
    /// first, then the contact probe for the next frame.
    /// </summary>
    [Fact]
    public void SailorThirteen_FullInteractionChain_SquareOpensTheBox_AndNothingReopensIt()
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

            AlundraEntityScriptProxy? sailor = null;
            var openedAfterPress = false;
            var openSurvivedItsOwnPress = false;
            var closedOnLaterPresses = false;
            var reopenedUncommanded = false;

            sim.RunFramesForTest(120, s =>
            {
                var player = s.PlayerEntity;
                Assert.NotNull(player);

                // Stand-in for the post-intro state: the map program's own entry sequence poses
                // ControlLocked (0x10) even with flag 860 seeded, and the real 0x11 release only comes
                // from letting a full intro play out - this montage clears the input locks each frame
                // instead (MessageBox/MenuOpen are deliberately NOT touched: OUR dialogue poses those,
                // and the D-E12D-5 gate must see them).
                s.GameState.PlayerControlFlags &= ~(AlundraGameState.PlayerControlBits.ControlLocked
                    | AlundraGameState.PlayerControlBits.ForcedSequence);

                var pressThisFrame = false;

                if (s.Frame == 1)
                {
                    AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), s.GameState);
                    AlundraDialogueDirector.Instance.InstallForMapEntry();

                    // "The intro is over": flag 860 is the intro-completion flag the golden pins at
                    // frame 1704 - seeding it keeps the map program from running the intro (whose very
                    // first act poses ControlLocked, which gates MovePlayer's whole free branch).
                    SeedFlag(s.GameState, IntroCompletionFlag);

                    sailor = System.Linq.Enumerable.Single(
                        s.SpawnedEntities,
                        e => (e.ProgramIndexes[ScriptHelper.ProgramFInteract] & 0x7f) == 13);

                    // The sim builds its own bare player proxy (it never goes through AdoptPlayerPawn,
                    // which adopts the hero record header in production - AlundraWorldProxy.cs:1018-1037)
                    // so this montage adopts the SAME real record values here: Flags 0x3118c
                    // (MoreFlags 0x8c | CanPickup 0x11 << 8 | 0x3 << 16 - Collidable set, no
                    // InteractRequiresButton) and the 21/15/32 box at offsets -10/-7/0.
                    player!.Flags = 0x3118c;
                    AlundraEntitySpawnFactory.SetEntityDimensions(player, offsetX: -10, offsetY: -7, offsetZ: 0, sizeX: 21, sizeY: 15, sizeZ: 32);

                    player.TargetAnimationId = 0; // out of the intro's LoadingMap pose - Idle.
                }

                // Keep the hero ON sailor 13 every frame (detection-only D-E12D-1: overlap IS the
                // contact) - a one-shot teleport is not enough, because with flag 860 seeded the Load
                // programs' own 0x64 repositioning DOES run (the intro-skip branch) and moves the
                // sailors right after frame 1.
                player!.PosX = sailor!.PosX;
                player.PosY = sailor.PosY;
                player.PosZ = sailor.PosZ;

                // Press Square once at frame 10 (to open), then repeatedly at 40..48 while the box is
                // open (a page a press until close) - and NEVER after 48.
                if (s.Frame == 10 || (s.Frame is >= 40 and < 49 && director.IsOpen))
                {
                    pressThisFrame = true;
                }

                var pad = pressThisFrame
                    ? new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square }
                    : default;
                s.GameState.LastPadState = pad; // the production player branch publishes it likewise.

                // Mirror 1 - the player branch's MovePlayer call (production site pinned by
                // AlundraInteractionPassTests P-b), consuming LAST frame's contact.
                AlundraPlayerManager.MovePlayer(player!, in pad, s.GameState, s);

                // Mirror 2 - the world proxy's contact pass (production site pinned by P-a), gated like
                // it (D-E12D-5), feeding NEXT frame's MovePlayer.
                if ((s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) == 0)
                {
                    player!.XCollisionEntity = AlundraEntityCollision.FindEntityCollisionCandidate(player, s.Collidables);
                }

                // Frame 11: the press of frame 10 assigned the sailor; THIS frame's real pick chose F,
                // ran 0x27+0x0D, and the box opened - and D-E12D-6 must have swallowed the very press
                // that opened it (the pad snapshot of frame 10 was still live during this frame's tick).
                if (s.Frame == 11)
                {
                    // PageIndex 0: the opening press must not have silently turned page 1 either -
                    // sailor 13's text is multi-page, so a missing swallow (D-E12D-6) shows up here as
                    // a skipped first page rather than a closed box.
                    openedAfterPress = director.IsOpen
                        && (s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) != 0
                        && director.PageIndexForTests == 0;
                }

                // Frames 12..39: open, no press - it must simply stay open (auto-timer far away).
                if (s.Frame == 39)
                {
                    openSurvivedItsOwnPress = director.IsOpen;
                }

                if (s.Frame is >= 41 and < 55 && !closedOnLaterPresses && !director.IsOpen)
                {
                    closedOnLaterPresses =
                        (s.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen) == 0;
                }

                // Frames 55..120: still overlapping the sailor, contact live again, but NO press -
                // the consumed assignment must never resurrect the dialogue (D-E12D-4).
                if (s.Frame > 55 && director.IsOpen)
                {
                    reopenedUncommanded = true;
                }
            });

            Assert.True(openedAfterPress, "the Square press against sailor 13 did not open the dialogue through the real pick.");
            Assert.True(openSurvivedItsOwnPress, "the box did not survive its own opening press (D-E12D-6 swallow).");
            Assert.True(closedOnLaterPresses, "the later presses never closed the box (or MenuOpen stayed posed).");
            Assert.False(reopenedUncommanded, "the dialogue reopened with no press - the one-shot signal leaked (D-E12D-4).");
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
