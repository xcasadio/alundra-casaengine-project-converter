#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E13 C3 acceptance (docs/plan-e13-hud.md, tranche "Les transitions et les deux cycles, au tick", D-E13-8).
/// <see cref="AlundraHudScreen"/> is not constructible headless (<see cref="AlundraDialoguePresenterWiringTests"/>'s
/// own class doc: a real <c>MGWindow</c>/<c>ScreenStack</c> needs a live graphics stack), so every test here
/// drives <see cref="AlundraHudPresenter"/> against a recording <see cref="IAlundraHudView"/> double instead -
/// the value the presenter itself PUSHED, exactly what mission item 4.a asks for when the element is not
/// constructible without a head.
/// </summary>
public sealed class AlundraHudPresenterTests : IDisposable
{
    // word 0x38 bit 21 -> (0x38 &lt;&lt; 5) + 21 = 1813, mask 0x200000 - "please appear" (same constants
    // AlundraHudDirectorTests re-derives independently from AlundraGameState's own IndexOf arithmetic).
    private const uint ScriptOpenRequestFlag = 1813;
    private const uint ScriptOpenRequestMask = 0x200000;

    // word 0x33 bit 30 -> (0x33 &lt;&lt; 5) + 30 = 1662, mask 0x40000000 - the persistent "already armed" latch.
    private const uint PersistentLatchFlag = 1662;
    private const uint PersistentLatchMask = 0x40000000;

    public AlundraHudPresenterTests()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private sealed class RecordingHudView : IAlundraHudView
    {
        public int PixelScale { get; }
        public readonly List<bool> VisibleCalls = new();
        public readonly List<Vector2> TranslationCalls = new();
        public readonly List<IReadOnlyList<AlundraHudTile>> TileCalls = new();

        public RecordingHudView(int pixelScale) => PixelScale = pixelScale;

        public void SetVisible(bool visible) => VisibleCalls.Add(visible);
        public void SetTranslation(Vector2 translation) => TranslationCalls.Add(translation);
        public void SetTiles(IReadOnlyList<AlundraHudTile> tiles) => TileCalls.Add(tiles);
    }

    private static AlundraHudDirector ArmedOpening(AlundraGameState state)
    {
        var director = AlundraHudDirector.Instance;
        director.AttachToWorld(state);
        state.AddFlag(ScriptOpenRequestFlag, ScriptOpenRequestMask);
        return director;
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 4.a - two tests of translation, one per direction, egalite EXACTE against §1.3's
    // OWN table (per direction) multiplied by the pixel-scale factor.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Tick_PushesTranslation_MatchingTheOpeningTable_ScaledByThePixelFactor()
    {
        var expectedY = new[] { -41, -38, -34, -30, -26, -22, -19, -15, -11, -7, -3, 0, 4, 8, 12, 16, 16, 16 };
        const int scale = 3; // arbitrary, non-1 factor - proves the multiplication actually happens.
        const int bakedBoxY = 0x10; // AlundraHudComposer's own baked UIBoxHud.Y - see AlundraHudPresenter's own doc.

        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        var view = new RecordingHudView(scale);
        var presenter = new AlundraHudPresenter(director, view);

        for (var i = 0; i < 18; i++)
        {
            director.Tick();
            presenter.Tick();
        }

        var expected = expectedY.Select(y => new Vector2(0f, (y - bakedBoxY) * scale)).ToArray();
        Assert.Equal(expected, view.TranslationCalls);
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);
    }

    [Fact]
    public void Tick_PushesTranslation_MatchingTheClosingTable_ScaledByThePixelFactor_DistinctFromOpening()
    {
        var expectedY = new[] { 16, 13, 9, 5, 1, -3, -6, -10, -14, -18, -22, -25, -29, -33, -37, -41, -41, -41 };
        const int scale = 3;
        const int bakedBoxY = 0x10;

        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        var view = new RecordingHudView(scale);
        var presenter = new AlundraHudPresenter(director, view);

        for (var i = 0; i < 18; i++) // reach Displayed(1) at Y = 16, same setup as AlundraHudDirectorTests's own.
        {
            director.Tick();
            presenter.Tick();
        }

        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);
        view.TranslationCalls.Clear(); // isolate the CLOSING sequence only.

        // Same simulated external latch clear AlundraHudDirectorTests's own closing test uses.
        state.SetFlag(PersistentLatchFlag, ~PersistentLatchMask);

        for (var i = 0; i < 18; i++)
        {
            director.Tick();
            presenter.Tick();
        }

        var expected = expectedY.Select(y => new Vector2(0f, (y - bakedBoxY) * scale)).ToArray();
        Assert.Equal(expected, view.TranslationCalls);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);

        // The two sequences genuinely differ (§1.3: truncation-toward-zero makes closing NOT the opening
        // reversed) - guards against a presenter/test bug that would make both tables coincidentally equal.
        var openingExpected = new[] { -41, -38, -34, -30, -26, -22, -19, -15, -11, -7, -3, 0, 4, 8, 12, 16, 16, 16 }
            .Select(y => new Vector2(0f, (y - bakedBoxY) * scale)).ToArray();
        Assert.NotEqual(openingExpected, expected);
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 4.b - image indices over 40 ticks, as the composer actually receives them: the
    // presenter's every pushed tile list must equal what AlundraHudComposer.Compose itself returns for
    // the director's CURRENT (already-ticked) state, on EVERY one of the 40 ticks - a stale or
    // out-of-order push would desync this pass-through and fail immediately.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Tick_PushesRecomposedTiles_MatchingWhatTheComposerItselfProduces_Over40Ticks()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        AlundraPlayerManager.SetPlayerMpMax(state, 4); // instant at ArmAppearance - HpMax/MpMax jump immediately.
        AlundraPlayerManager.SetPlayerMp(state, 4); // catches up 1 point / 8 active ticks (C1, already proven).
        AlundraPlayerManager.SetMoney(state, 1000); // keeps the coin roll going for the whole 40 ticks.

        var view = new RecordingHudView(pixelScale: 1);
        var presenter = new AlundraHudPresenter(director, view);

        for (var i = 0; i < 40; i++)
        {
            director.Tick();
            presenter.Tick();

            var expectedTiles = AlundraHudComposer.Compose(
                director.IsDrawn,
                director.Hp, director.HpMax, director.TrueHpMax, director.HpDisplayPreviewIncrement,
                director.Mp, director.MpMax, director.MpDisplayPreviewIncrement,
                director.Money, director.CoinIconFrame,
                director.MagicPipFrame);

            Assert.Equal(expectedTiles, view.TileCalls[i]);
        }

        // Targeted check tying the pass-through above to §1.4's own magic-pip cadence (1 image / 10,
        // cycle of 4): tick index 18 -> FrameCounter 19, phase (19/10)%4 == 1 (AlundraHudDirectorTests's
        // own already-proven MagicPipFrame_AdvancesOnce_EveryTenActiveTicks_CycleOfFour_Unison test), read
        // here through the tiles the PRESENTER pushed rather than the bare director field.
        Assert.Contains(view.TileCalls[18], t => t.Glyph == HudGlyph.MagicPipFull1);
        Assert.Equal(4, director.MpMax); // set instantly at ArmAppearance, C1.
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 4.c - "la jauge continue de se rafraichir alors que AlundraDialogueDirector.Instance
    // est ouvert", driven through the REAL AlundraWorldProxy.Update per-tick loop (the patron of C1's own
    // Update_AdvancesTheHudDirector_EvenWhileAModalDialogueIsOpen), never through a manually-ticked
    // presenter - so a regression that re-gates the presenter behind a screen-stack Update would be caught.
    // -----------------------------------------------------------------------------------------

    private static AlundraWorldProxy BuildProxyWithHudPresenterAttached(IAlundraHudView view)
    {
        var world = new World { Name = "TestWorld" };
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // "TestWorld" has no tileMap entity - early-returns before the
                                           // real install, same seam AlundraHudDirectorTests documents.

        AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), proxy.GameState);
        AlundraHudDirector.Instance.AttachToWorld(proxy.GameState);
        proxy.AttachHudPresenterForTests(view);
        return proxy;
    }

    [Fact]
    public void Update_KeepsRefreshingTheView_EvenWhileAModalDialogueIsOpen()
    {
        var view = new RecordingHudView(pixelScale: 3);
        var proxy = BuildProxyWithHudPresenterAttached(view);
        var dialogue = AlundraDialogueDirector.Instance;

        proxy.GameState.AddFlag(ScriptOpenRequestFlag, ScriptOpenRequestMask); // arm the HUD open.

        // controlMode 0 = MenuOpen (AlundraGameState.PlayerControlBits.MenuOpen) - the strictest freeze,
        // same choice as AlundraHudDirectorTests's own Update_AdvancesTheHudDirector_... test.
        dialogue.Open("bonjour", controlMode: 0);
        Assert.True(dialogue.IsOpen);
        Assert.Empty(view.VisibleCalls);

        proxy.Update(1f / 50f); // one logic tick.

        Assert.True(dialogue.IsOpen); // unaffected either way - not this test's own point.
        Assert.Single(view.VisibleCalls);
        Assert.True(view.VisibleCalls[0]); // AlundraHudDirector.Instance.IsDrawn became true this same tick (Opening).
        Assert.Single(view.TranslationCalls);
        Assert.Single(view.TileCalls);

        for (var i = 0; i < 4; i++)
        {
            proxy.Update(1f / 50f);
        }

        Assert.True(dialogue.IsOpen); // still open - the presenter's own advance never depended on it.
        Assert.Equal(5, view.VisibleCalls.Count);
        Assert.Equal(5, view.TranslationCalls.Count);
        Assert.Equal(5, view.TileCalls.Count);
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 4.d - IsDrawn false -> rien de visible.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Tick_WithNothingDrawn_PushesFalseVisibilityAndNoTiles()
    {
        var director = AlundraHudDirector.Instance; // freshly reset (constructor) - Idle, IsDrawn == false.
        var view = new RecordingHudView(pixelScale: 4);
        var presenter = new AlundraHudPresenter(director, view);

        Assert.False(director.IsDrawn);
        presenter.Tick();

        Assert.Equal(new[] { false }, view.VisibleCalls);
        Assert.Single(view.TileCalls);
        Assert.Empty(view.TileCalls[0]);
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 3 - AUCUNE ANIMATION MGUI: fixed by grep over the two source files this slice touches
    // (mission's own "au choix" between reflection and grep). Doc-comment lines (which legitimately name
    // UIAnimation/EnterExit in prose) are stripped before matching, so only real code lines count.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PresenterAndScreen_NeverCallAnyMguiAnimationApi()
    {
        var repoRoot = FindRepoRoot();
        AssertNoAnimationCalls(Path.Combine(repoRoot, "Alundra", "Scripts", "AlundraHudPresenter.cs"));
        AssertNoAnimationCalls(Path.Combine(repoRoot, "Alundra", "Scripts", "AlundraHudScreen.cs"));
    }

    private static void AssertNoAnimationCalls(string path)
    {
        var codeOnly = string.Join(
            Environment.NewLine,
            File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        Assert.DoesNotMatch(@"\.Animate\(|EnterExit\(|UIAnimationClock|new\s+UIAnimation", codeOnly);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Alundra", "Scripts", "AlundraHudDirector.cs");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraHudPresenterTests: no 'Alundra/Scripts/AlundraHudDirector.cs' found above "
            + $"'{AppContext.BaseDirectory}' - this test needs the repo's own source tree, not just the built DLL.");
    }
}
