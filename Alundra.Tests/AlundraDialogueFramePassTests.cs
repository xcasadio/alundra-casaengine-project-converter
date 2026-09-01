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
    }

    public void Dispose()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
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
        director.Open("bonjour", controlMode: 0);
        Assert.True(director.IsOpen);
        Assert.NotEqual(0u, proxy.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        // One tick with no button: the pass runs but nothing closes the box yet.
        proxy.Update(1f / 50f);
        Assert.True(director.IsOpen);

        // Just-pressed interact on the NEXT tick - Update's own pass (the only Tick driver here: no
        // 0x39, no harness mirror) must close the box and lift MenuOpen. Before the F1 fix this frame
        // changed nothing and the box stayed open forever.
        proxy.GameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
        proxy.Update(1f / 50f);

        Assert.False(director.IsOpen);
        Assert.Equal(0u, proxy.GameState.PlayerControlFlags
            & (AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.MessageBox));
    }

    [Fact]
    public void Update_RunsTheDialoguePassOncePerLogicTick_NotOncePerFrame()
    {
        var proxy = BuildProxyWithAttachedDirector();
        var director = AlundraDialogueDirector.Instance;

        director.Open("bonjour", controlMode: 1);
        Assert.True(director.IsOpen);

        // 0.06 s frames = 3 logic ticks each (cap is 4). The default close mask's auto-timer fires at
        // 360 TICKS: ~300 ticks in (100 frames) the box must still be open, ~402 ticks in (34 more) it
        // must have auto-closed. A pass that ticked once per FRAME instead of once per TICK would only
        // have counted ~134 by then and still be open - this is the mutation this test exists to kill
        // (the checkpoints sit ~60 ticks away from the threshold on each side, far beyond the +/-1 tick
        // of float-accumulator drift).
        for (var frame = 0; frame < 100; frame++)
        {
            proxy.Update(0.06f);
        }

        Assert.True(director.IsOpen);

        for (var frame = 0; frame < 34; frame++)
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

        director.Open("bonjour", controlMode: 0);
        Assert.True(director.IsOpen);
        Assert.NotEqual(0u, proxy.GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.MenuOpen);

        proxy.InstallDialogueSystems(new World { Name = "TestWorld" });

        Assert.False(director.IsOpen);
        Assert.False(director.IsAwaitingChoice);
        Assert.Equal(0u, proxy.GameState.PlayerControlFlags
            & (AlundraGameState.PlayerControlBits.MenuOpen | AlundraGameState.PlayerControlBits.MessageBox));
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

        director.Open("page un\\Apage deux", controlMode: 1);
        Assert.True(director.IsOpen);

        var rePointedPresenter = new DialogueService();
        director.AttachToWorld(rePointedPresenter, gameState);

        Assert.True(director.IsOpen);

        // The surviving page state must keep driving the NEW presenter: a button tick advances to page
        // two and shows it there - proof the pages/index really survived the re-point, not just a flag.
        gameState.LastPadState = new AlundraPadState { ButtonsJustPressed = AlundraPadState.Square };
        director.Tick();

        Assert.True(director.IsOpen);
        Assert.Contains("page deux", director.CurrentLineForTests?.Text ?? "");
    }
}
