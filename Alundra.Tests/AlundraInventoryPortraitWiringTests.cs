#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Cameras;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Dialogues;
using Alundra.UI.Hud;
using Alundra.UI.Inventory;
using Alundra.UI.Shared;
using Alundra.UI.SubInventory;

namespace Alundra.Tests;

/// <summary>
/// docs/plan-portrait-inventaire.md PI8: the opening portrait wired into both inventories, driven through the real
/// <see cref="AlundraWorldProxy.Update(float)"/> per-tick loop (one 0.02 s update = one logic tick). The expected tick
/// counts come from the executable (§1.1 of the plan, PI1): the trigger's opening steps first in the render half of
/// its own tick (the original's next render), and a start or a return made in a render half - the post-process
/// openings, the four exits - steps in that same tick.
/// </summary>
public sealed class AlundraInventoryPortraitWiringTests : IDisposable
{
    public AlundraInventoryPortraitWiringTests()
    {
        ResetAll();
    }

    public void Dispose()
    {
        ResetAll();
    }

    private static void ResetAll()
    {
        InventoryPortrait.Instance.ResetForTests();
        InventoryDirector.Instance.ResetForTests();
        SubInventoryDirector.Instance.ResetForTests();
        InventoryPostProcess.Instance.ResetForTests();
        HudDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static InventoryPortrait Portrait => InventoryPortrait.Instance;

    private static AlundraWorldProxy NewWorld()
    {
        var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
        var worldProxy = new AlundraWorldProxy();
        worldProxy.InitializeWithWorld(world);
        worldProxy.PlayerEntity = new AlundraEntityScriptProxy
        {
            PosX = (200 << 16) | 0x4000,
            PosY = 150 << 16,
            PosZ = 8 << 16,
        };

        // Same direct attach as AlundraInventoryDirectorTests' own world test: a bare headless world never reaches
        // InstallInventorySystems (no TileMap entity).
        var tables = ItemTablesFixture.LoadReal();
        InventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);
        SubInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);
        return worldProxy;
    }

    /// <summary>The head point the world hands the portrait: the player above, and the headless proxy's camera
    /// (none resolved, so <see cref="CameraMath.ToOriginalScrollSpace"/> of <see cref="Vector3.Zero"/>).</summary>
    private static (int X, int Y) ExpectedHead(AlundraWorldProxy worldProxy)
    {
        var scroll = CameraMath.ToOriginalScrollSpace(Vector3.Zero);
        var player = worldProxy.PlayerEntity!;
        return InventoryPortrait.ComputeHeadPoint(player.PosX, player.PosY, player.PosZ, scroll.X, scroll.Y);
    }

    private static void Press(AlundraWorldProxy worldProxy, uint buttons)
    {
        worldProxy.GameState.LastPadState = new PadState { ButtonsHold = buttons };
        worldProxy.Update(0.02f);
        worldProxy.GameState.LastPadState = default;
    }

    private static void Ticks(AlundraWorldProxy worldProxy, int count)
    {
        for (var i = 0; i < count; i++)
        {
            worldProxy.Update(0.02f);
        }
    }

    [Fact]
    public void TriggerOpening_StepsInItsOwnTick_VisibleNextTick_AtRestAfterFifteen()
    {
        var worldProxy = NewWorld();
        var head = ExpectedHead(worldProxy);

        Press(worldProxy, PadState.Start); // tick T: trigger (Update half), then the first step (render half).
        Assert.True(InventoryDirector.Instance.IsActive);
        Assert.Equal(InventoryPortrait.StateOpening, Portrait.State);
        Assert.Equal((head.X, head.Y, 0, 0), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 1); // T+1: call 2.
        Assert.True(Portrait.IsVisible);
        Assert.Equal((3, 3), (Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 13); // T+14: call 15.
        Assert.Equal((44, 52), (Portrait.DrawnWidth, Portrait.DrawnHeight));
        Assert.Equal(InventoryPortrait.StateOpening, Portrait.State);

        Ticks(worldProxy, 1); // T+15: call 16, at rest.
        Assert.Equal(InventoryPortrait.StateAtRest, Portrait.State);
        Assert.Equal((248, 104, 48, 56), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 30);
        Assert.Equal(InventoryPortrait.StateAtRest, Portrait.State);
    }

    [Fact]
    public void Close_ReturnStepsInItsOwnTick_AndEndsBeforeTheScreenIsRemoved()
    {
        var worldProxy = NewWorld();
        Press(worldProxy, PadState.Start);
        Ticks(worldProxy, 18); // the 18-call opening slide, then input is accepted
        Assert.Equal(InventoryPortrait.StateAtRest, Portrait.State);

        Press(worldProxy, PadState.Start); // tick M: close (render half), the return's first step in M.
        Assert.Equal(InventoryPortrait.StateReturning, Portrait.State);
        Assert.Equal((248, 104, 48, 56), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 15); // M+15: call 16, idle.
        Assert.Equal(InventoryPortrait.StateIdle, Portrait.State);
        Assert.False(Portrait.IsVisible);

        // The slide-out lasts longer than the return: the screen is still drawn when the portrait is gone.
        Assert.True(InventoryDirector.Instance.IsDrawn);
    }

    [Fact]
    public void ToggleToTheSubInventory_ReturnsThenStartsAgain_InThePostProcessTick()
    {
        var worldProxy = NewWorld();
        var head = ExpectedHead(worldProxy);
        Press(worldProxy, PadState.Start);
        Ticks(worldProxy, 18);

        Press(worldProxy, PadState.L1); // tick M: the main inventory's return.
        Assert.Equal(InventoryPortrait.StateReturning, Portrait.State);

        var ticksAfterReturn = 0;
        while (!SubInventoryDirector.Instance.IsActive && ticksAfterReturn < 60)
        {
            Ticks(worldProxy, 1);
            ticksAfterReturn++;
        }

        Assert.True(SubInventoryDirector.Instance.IsActive);

        // The post-process opened the sub-inventory in this tick: its start was not dropped by the idle guard (the
        // return ended at M+15), and it already made its first step here.
        Assert.True(ticksAfterReturn >= 16, $"the sub-inventory opened {ticksAfterReturn} ticks after the return: the start would be dropped");
        Assert.Equal(InventoryPortrait.StateOpening, Portrait.State);
        Assert.Equal((head.X, head.Y, 0, 0), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));
    }

    [Fact]
    public void ToggleBackToTheMainInventory_StartsInTheHeadTickOfThePostProcess()
    {
        var worldProxy = NewWorld();
        var head = ExpectedHead(worldProxy);
        Press(worldProxy, PadState.Start);
        Ticks(worldProxy, 18);
        Press(worldProxy, PadState.L1);
        for (var i = 0; i < 60 && !SubInventoryDirector.Instance.IsActive; i++)
        {
            Ticks(worldProxy, 1);
        }

        Ticks(worldProxy, 25); // the sub-inventory's own opening slide, then input is accepted
        Assert.Equal(InventoryPortrait.StateAtRest, Portrait.State);
        Assert.False(InventoryDirector.Instance.IsActive);

        Press(worldProxy, PadState.L1); // the sub-inventory's return, in its own tick.
        Assert.Equal(InventoryPortrait.StateReturning, Portrait.State);
        Assert.Equal(48, Portrait.DrawnWidth);

        var ticks = 0;
        while (Portrait.State != InventoryPortrait.StateOpening && ticks < 60)
        {
            Ticks(worldProxy, 1);
            ticks++;
        }

        // The main inventory's head ran from the post-process in this very tick (its setup follows next tick) and
        // the portrait already stepped once.
        Assert.Equal(InventoryPortrait.StateOpening, Portrait.State);
        Assert.True(InventoryDirector.Instance.IsCallbackArmed);
        Assert.Equal((head.X, head.Y, 0, 0), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));
    }

    // ---- presenter -----------------------------------------------------------------------------------

    private sealed class FakeScreen : IUIScreen
    {
        public UILayer Layer => UILayer.Menu;
        public bool IsModal => true;
        public bool BlocksViewsBelow => true;
        public void Initialize(UIRoot root) { }
        public void Show() { }
        public void Hide() { }
        public void Update(GameTime gameTime) { }
        public IEnumerable<MGWindow> GetWindows() => Array.Empty<MGWindow>();
    }

    private static ItemTables TablesWithPortrait(Guid? portraitId)
    {
        var projectPath = ItemTablesFixture.Write(ItemTablesFixture.RealProperties(), null, null);
        try
        {
            if (portraitId.HasValue)
            {
                File.WriteAllText(
                    Path.Combine(projectPath, "Data", "inventory-portrait.json"),
                    $"{{ \"SpriteAssetId\": \"{portraitId.Value}\" }}");
            }

            return new ItemTables(projectPath);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void Presenter_PushesTheFlightAsTranslationAndScale_WithTheIndexedSprite()
    {
        var portraitId = Guid.Parse("19436250-bfec-529a-bf3f-23d258f82db6");
        var worldProxy = NewWorld();
        var viewModel = new AlundraInventoryViewModel();
        var presenter = new InventoryPresenter(
            InventoryDirector.Instance, worldProxy.GameState, TablesWithPortrait(portraitId), viewModel, new FakeScreen(), null);

        Press(worldProxy, PadState.Start);
        presenter.Tick();
        Ticks(worldProxy, 1); // call 2: 3x3
        presenter.Tick();

        Assert.Equal(portraitId.ToString("D"), viewModel.Portrait.SourceName);
        Assert.Equal(Visibility.Visible, viewModel.Portrait.Visibility);
        Assert.Equal(new Vector2(Portrait.X - 248, Portrait.Y - 104), viewModel.Portrait.Translation);
        Assert.Equal(new Vector2(3f / 48f, 3f / 56f), viewModel.Portrait.Scale);

        Ticks(worldProxy, 14); // at rest
        presenter.Tick();
        Assert.Equal(Vector2.Zero, viewModel.Portrait.Translation);
        Assert.Equal(Vector2.One, viewModel.Portrait.Scale);
    }

    /// <summary>A quad with no area is the original's 0x0 quad, written as a zero scale: the element never collapses
    /// and shows again, which would run the window's layout twice per flight (plan D3: render-only).</summary>
    [Fact]
    public void ViewModel_AQuadWithNoArea_IsAZeroScale_NeverACollapse()
    {
        var portrait = new InventoryPortrait();
        var viewModel = new InventoryPortraitViewModel();
        var source = Guid.Parse("19436250-bfec-529a-bf3f-23d258f82db6");

        viewModel.Apply(portrait, source); // idle
        Assert.Equal(Visibility.Visible, viewModel.Visibility);
        Assert.Equal(Vector2.Zero, viewModel.Scale);

        portrait.Start(160, 120);
        portrait.Step(); // call 1: 0x0
        viewModel.Apply(portrait, source);
        Assert.Equal(Visibility.Visible, viewModel.Visibility);
        Assert.Equal(Vector2.Zero, viewModel.Scale);

        portrait.Step(); // call 2: 3x3, the element was already visible and laid out
        viewModel.Apply(portrait, source);
        Assert.Equal(new Vector2(3f / 48f, 3f / 56f), viewModel.Scale);
    }

    /// <summary>E19.f4c3 (D-E19-100, F4C3-R1): the inventory's portrait writer sets the tint, Rgb / 128, with the translation and the scale on every call that has a source: the whole
    /// cycle of the plan's table (the opening from 255 down to 135, rest 128, the return from 127 up to 246, the last pass and the idle at 0), size and Rgb together.</summary>
    [Fact]
    public void ViewModel_Apply_SetsTheBrightnessFromTheRgb_AcrossTheWholeCycle()
    {
        var portrait = new InventoryPortrait();
        var viewModel = new InventoryPortraitViewModel();
        var source = Guid.Parse("19436250-bfec-529a-bf3f-23d258f82db6");

        // (width, height, Rgb) per call, from the closed forms (plan E19.f4c3, forecast).
        var opening = new (int W, int H, int Rgb)[]
        {
            (0, 0, 255), (3, 3, 246), (6, 7, 237), (9, 11, 229), (12, 14, 220), (16, 18, 212), (19, 22, 203), (22, 26, 195), (25, 29, 186), (28, 33, 178),
            (32, 37, 169), (35, 41, 161), (38, 44, 152), (41, 48, 144), (44, 52, 135),
        };
        var returning = new (int W, int H, int Rgb)[]
        {
            (48, 56, 127), (44, 52, 135), (41, 48, 144), (38, 44, 152), (35, 41, 161), (32, 37, 169), (28, 33, 178), (25, 29, 186), (22, 26, 195), (19, 22, 203),
            (16, 18, 212), (12, 14, 220), (9, 11, 229), (6, 7, 237), (3, 3, 246),
        };

        void Check(string label, (int W, int H, int Rgb) expected)
        {
            Assert.True(
                (expected.W, expected.H, expected.Rgb) == (portrait.DrawnWidth, portrait.DrawnHeight, portrait.Rgb),
                $"{label}: size/Rgb {(portrait.DrawnWidth, portrait.DrawnHeight, portrait.Rgb)} expected {expected}");
            viewModel.Apply(portrait, source);
            Assert.True(expected.Rgb / 128f == viewModel.Brightness, $"{label}: Brightness {viewModel.Brightness} expected {expected.Rgb / 128f}");
            Assert.Equal(new Vector2(expected.W / 48f, expected.H / 56f), viewModel.Scale);
        }

        // Idle, before anything: Rgb 0, Brightness 0, scale 0.
        Check("idle", (0, 0, 0));

        portrait.Start(160, 120);
        for (var i = 0; i < opening.Length; i++)
        {
            portrait.Step();
            Check($"opening call {i + 1}", opening[i]);
        }

        portrait.Step();
        Check("rest", (48, 56, 128));
        Assert.Equal(1f, viewModel.Brightness);
        Assert.Equal(InventoryPortrait.StateAtRest, portrait.State);

        portrait.BeginReturn(160, 120);
        for (var i = 0; i < returning.Length; i++)
        {
            portrait.Step();
            Check($"return call {i + 1}", returning[i]);
        }

        portrait.Step();
        Check("last pass", (0, 0, 0));
        Assert.Equal(0f, viewModel.Brightness);

        portrait.Step();
        Check("idle after", (0, 0, 0));
    }

    /// <summary>E19.f4c3 (F4C3-R1): the early return without a source (a collapsed element) leaves the tint as it was.</summary>
    [Fact]
    public void ViewModel_Apply_WithoutASource_LeavesTheBrightnessUntouched()
    {
        var portrait = new InventoryPortrait();
        var viewModel = new InventoryPortraitViewModel();
        var source = Guid.Parse("19436250-bfec-529a-bf3f-23d258f82db6");

        portrait.Start(160, 120);
        portrait.Step();
        viewModel.Apply(portrait, source); // call 1: Rgb 255
        Assert.Equal(255f / 128f, viewModel.Brightness);

        portrait.Step(); // call 2: Rgb 246
        viewModel.Apply(portrait, null);
        Assert.Equal(Visibility.Collapsed, viewModel.Visibility);
        Assert.Equal(255f / 128f, viewModel.Brightness);

        viewModel.Brightness = 1f;
        viewModel.Apply(portrait, null);
        Assert.Equal(1f, viewModel.Brightness);
    }

    [Fact]
    public void Presenter_WithoutAPortraitIndex_KeepsThePortraitHidden()
    {
        var worldProxy = NewWorld();
        var viewModel = new AlundraInventoryViewModel();
        var presenter = new InventoryPresenter(
            InventoryDirector.Instance, worldProxy.GameState, TablesWithPortrait(null), viewModel, new FakeScreen(), null);

        Press(worldProxy, PadState.Start);
        Ticks(worldProxy, 15);
        presenter.Tick();

        Assert.True(Portrait.IsVisible);
        Assert.Equal(Visibility.Collapsed, viewModel.Portrait.Visibility);
        Assert.Null(viewModel.Portrait.SourceName);
    }
}
