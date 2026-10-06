#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// The dialogue FRAME PASS at its real production site - written for the E12.a closing verifier's F1
/// (P1): six of map 389's seven sailors open their box from a mono-line F(Interact) program
/// (<c>0x27 ; 0x0D ; 0x05 ; 0xFF</c>) with NO 0x39 in it, and the box's advance/close used to live only
/// inside 0x39's dispatch - so those boxes could never close: a permanent softlock (MenuOpen posed
/// forever). The original runs the box's lifecycle EVERY main-loop frame, independent of scripts
/// (<c>UIManager.ProcessEtcTextAdvance</c>, UI/UIManager.cs:855-880); the fix moved it to a per-logic-tick
/// pass in <see cref="AlundraWorldProxy.Update"/>, next to the fade pass.
///
/// The first two tests below drive <see cref="AlundraWorldProxy.Update"/> ITSELF (the same headless
/// montage as <see cref="AlundraWorldProxyUpdateCharacterizationTests"/>: no <see cref="World.Game"/>, a
/// name with no trailing map id, a camera entity added directly) so the pass is pinned at its production
/// call site - the harness's own mirror tick in <c>HeadlessIntroSimulation.RunFramesForTest</c> cannot
/// stand in for it (the green-and-inert family: fade trigger, audio install, backdrop textures,
/// presenter install - this repo's most repeated trap). The last two are the closing verifier's F3/F4:
/// the <see cref="AlundraDialogueDirector.InstallForMapEntry"/> reset really runs on install, and
/// <see cref="AlundraDialogueDirector.AttachToWorld"/> really does NOT reset.
/// </summary>
public sealed class AlundraDialogueFramePassTests : IDisposable
{
    public AlundraDialogueFramePassTests()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        // Same seam note as AlundraWorldProxyUpdateCharacterizationTests: inert here (world.Game is
        // always null below), set explicitly anyway rather than relying on the env-var-backed static.
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    /// <summary>Headless proxy whose <c>Update</c> is drivable - the exact montage
    /// <see cref="AlundraWorldProxyUpdateCharacterizationTests"/> documents line by line (its class doc
    /// owns the reasoning; not restated here). The director is then attached to THIS proxy's own
    /// <see cref="AlundraWorldProxy.GameState"/>, production's wiring shape
    /// (<c>InstallDialogueSystems</c> passes <c>GameState</c>) - "TestWorld" has no tileMap entity, so
    /// <c>InitializeWithWorld</c> early-returns before reaching the real install.</summary>
    private static AlundraWorldProxy BuildProxyWithAttachedDirector()
    {
        var world = new World { Name = "TestWorld" };
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world);

        AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), proxy.GameState);
        return proxy;
    }

    [Fact]
    public void Update_RunsTheDialoguePass_ButtonClosesABoxNoScriptIsWatching()
    {
        var proxy = BuildProxyWithAttachedDirector();
        var director = AlundraDialogueDirector.Instance;

        // The sailor-13 shape: controlMode 0 -> MenuOpen posed, and NO script left running to pump 0x39.
        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 0);
        Assert.True(director.IsOpen);
        Assert.NotEqual(0u, proxy.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        // E19.f2a (F2-R1): one Update is one tick and the pass of the box of that tick, on the square button of the tick BEFORE. One tick with no button:
        // the pass runs but nothing closes the box yet.
        proxy.Update(1f / 50f);
        Assert.True(director.IsOpen);

        // A press written before the Update 2 is read by the pass 3, while the box still slides in (the slide does not read the pad): nothing changes.
        proxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Square };
        proxy.Update(1f / 50f);
        proxy.GameState.LastPadState = default;
        for (var update = 3; update <= 46; update++)
        {
            proxy.Update(1f / 50f);
        }

        Assert.True(director.IsOpen);

        // The text "bonjour" is typed from the pass 19 to the pass 43 and ends at 47. The square held before the Update 47 and released before the Update
        // 48 is a press the box sees at the pass 48: the box triggers its close at 48 (Update's own pass is the only driver here: no 0x39, no harness
        // mirror), slides out, and is released 18 passes later - open after the Update 65, released at the Update 66, lifting MenuOpen.
        proxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Square };
        proxy.Update(1f / 50f); // 47
        proxy.GameState.LastPadState = default;
        for (var update = 48; update <= 65; update++)
        {
            proxy.Update(1f / 50f);
            Assert.True(director.IsOpen, $"the box is released after the Update {update}, 66 expected");
        }

        proxy.Update(1f / 50f); // 66

        Assert.False(director.IsOpen);
        Assert.Equal(0u, proxy.GameState.PlayerControlFlags
            & (AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.MessageBox));
    }

    [Fact]
    public void Update_RunsTheDialoguePassOncePerLogicTick_NotOncePerFrame()
    {
        var proxy = BuildProxyWithAttachedDirector();
        var director = AlundraDialogueDirector.Instance;

        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 1);
        Assert.True(director.IsOpen);

        // 0.06 s frames = 3 logic ticks each (cap is 4). The default close mask's auto-timer is armed at the end of the typing (the pass 47) and fires 360
        // passes later: the box is released 18 passes after that, at the pass 425. ~363 ticks in (121 frames) the box must still be open, ~486 ticks in
        // (41 more) it must have closed. A pass that ticked once per FRAME instead of once per TICK would only have counted ~162 by then and still be open -
        // this is the mutation this test exists to kill (the checkpoints sit ~60 ticks away from the release on each side, far beyond the +/-1 tick of
        // float-accumulator drift).
        for (var frame = 0; frame < 121; frame++)
        {
            proxy.Update(0.06f);
        }

        Assert.True(director.IsOpen);

        for (var frame = 0; frame < 41; frame++)
        {
            proxy.Update(0.06f);
        }

        Assert.False(director.IsOpen);
        Assert.Equal(0u, proxy.GameState.PlayerControlFlags
            & (AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.MessageBox));
    }

    [Fact]
    public void InstallDialogueSystems_PerformsTheMapEntryReset_AnOpenBoxDoesNotSurviveIt()
    {
        // F3: InstallForMapEntry's call lives INSIDE InstallDialogueSystems (the M16 no-separate-site
        // rule) - deleting that one line used to leave every suite green. This drives the real install
        // method (world.Game null -> presenter null branch, the reset must run regardless).
        var proxy = new AlundraWorldProxy();
        var director = AlundraDialogueDirector.Instance;
        director.AttachToWorld(new DialogueService(), proxy.GameState);

        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 0);
        Assert.True(director.IsOpen);
        Assert.NotEqual(0u, proxy.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        proxy.InstallDialogueSystems(new World { Name = "TestWorld" });

        Assert.False(director.IsOpen);
        Assert.False(director.IsAwaitingChoice);
        Assert.Equal(0u, proxy.GameState.PlayerControlFlags
            & (AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.MessageBox));
    }

    [Fact]
    public void OpeningPress_DoesNotTouchTheSlide_APressSeenAt48ClosesTheBox_AndTheTimerStillClosesIt()
    {
        // E12.d T4 (docs/plan-e12d-interaction-joueur.md D-E12D-6), redone for E19.f2a: a box opened WHILE the interact button is held (the very press that
        // triggered the interaction) does not read the pad while it slides in (the slide, 18 passes, never does), so the opening press cannot advance or close
        // what it just opened and nothing needs to swallow it any more; the box closes on a press its pass sees once the typing is done (the pass 48: the
        // text "bonjour" ends at 47), is released 18 passes later (66), and the auto-timer, armed at the end of the typing, still closes it alone at 425.
        var gameState = new AlundraGameState();
        var director = AlundraDialogueDirector.Instance;
        director.AttachToWorld(new DialogueService(), gameState);

        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 0);
        Assert.True(director.IsOpen);

        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.HoldBetween(1, 2); // the opening press, still held for the first two ticks
        passes.PressSeenAt(48);
        passes.RunTo(2);
        Assert.True(director.IsOpen); // the held square did not close the box while it slides in
        passes.RunTo(65);
        Assert.True(director.IsOpen);
        passes.RunTo(66);
        Assert.False(director.IsOpen);

        // And the timer half: the same opening, then NO button ever again - the 360-pass auto-close (mask bit0), armed at the end of the typing (47), triggers at
        // 407 and releases at 425: open after 424 passes, closed at the 425th.
        gameState.LastPadState = new AlundraPadState { ButtonsHold = AlundraPadState.Square };
        director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: 0);
        gameState.LastPadState = default;
        var timerPasses = new DialogueBoxPassDriver(director, gameState);
        timerPasses.RunTo(424);
        Assert.True(director.IsOpen);
        timerPasses.RunTo(425);
        Assert.False(director.IsOpen);
    }

    [Fact]
    public void AttachToWorld_RePointsWithoutResetting_AnOpenDialogueSurvives()
    {
        // F4: collapsing AttachToWorld into a reset (the tempting "simplification") used to stay green
        // too. The AttachToWorld/InstallForMapEntry split is the session-singleton contract shared with
        // AlundraMusicPlayer/AlundraScreenFadeDirector: re-point WITHOUT resetting.
        var gameState = new AlundraGameState();
        var director = AlundraDialogueDirector.Instance;
        director.AttachToWorld(new DialogueService(), gameState);

        director.Open(DialogueTestAssets.Build("TwoPage", "Start", "page un", "page deux"), "Start", controlMode: 1);
        Assert.True(director.IsOpen);

        var rePointedPresenter = new DialogueService();
        director.AttachToWorld(rePointedPresenter, gameState);

        Assert.True(director.IsOpen);

        // The surviving page state must keep driving the NEW presenter: the cursor of the page boundary shows at the pass 47, a press the box sees at the
        // pass 48 turns the page - proof the pages/index really survived the re-point, not just a flag - and the first letter of the second page, typed at the
        // pass 52, reaches the new presenter.
        var passes = new DialogueBoxPassDriver(director, gameState);
        passes.PressSeenAt(48);
        passes.RunTo(47);
        Assert.True(director.Box.IsCursorShown);
        passes.RunTo(48);

        Assert.True(director.IsOpen);
        Assert.Equal(1, director.PageIndexForTests);
        Assert.Contains("page deux", director.CurrentLineForTests?.Text ?? "");
        passes.RunTo(52);
        Assert.Equal(("page un", "p"), (director.Box.Row(0), director.Box.Row(1))); // E19.f2b1c: the box draws the first letter of the second page, whatever the presenter
        director.OpenChoice(new[] { "OUI", "NON" });
        Assert.Equal(new[] { "OUI", "NON" }, director.ChoicesForTests); // E19.f3a: the choice box is the director's own: nothing reaches the engine's presenter any more
        Assert.Empty(rePointedPresenter.Choices);
    }
}
