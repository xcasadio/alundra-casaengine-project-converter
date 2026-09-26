#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.UI;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Xunit;

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
        AlundraInventoryPortrait.Instance.ResetForTests();
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static AlundraInventoryPortrait Portrait => AlundraInventoryPortrait.Instance;

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
        AlundraInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);
        AlundraSubInventoryDirector.Instance.AttachToWorld(worldProxy.GameState, tables, worldProxy.SoundPlayer);
        return worldProxy;
    }

    /// <summary>The head point the world hands the portrait: the player above, and the headless proxy's camera
    /// (none resolved, so <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> of <see cref="Vector3.Zero"/>).</summary>
    private static (int X, int Y) ExpectedHead(AlundraWorldProxy worldProxy)
    {
        var scroll = AlundraCameraMath.ToOriginalScrollSpace(Vector3.Zero);
        var player = worldProxy.PlayerEntity!;
        return AlundraInventoryPortrait.ComputeHeadPoint(player.PosX, player.PosY, player.PosZ, scroll.X, scroll.Y);
    }

    private static void Press(AlundraWorldProxy worldProxy, uint buttons)
    {
        worldProxy.GameState.LastPadState = new AlundraPadState { ButtonsHold = buttons };
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

        Press(worldProxy, AlundraPadState.Start); // tick T: trigger (Update half), then the first step (render half).
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, Portrait.State);
        Assert.Equal((head.X, head.Y, 0, 0), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 1); // T+1: call 2.
        Assert.True(Portrait.IsVisible);
        Assert.Equal((3, 3), (Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 13); // T+14: call 15.
        Assert.Equal((44, 52), (Portrait.DrawnWidth, Portrait.DrawnHeight));
        Assert.Equal(AlundraInventoryPortrait.StateOpening, Portrait.State);

        Ticks(worldProxy, 1); // T+15: call 16, at rest.
        Assert.Equal(AlundraInventoryPortrait.StateAtRest, Portrait.State);
        Assert.Equal((248, 104, 48, 56), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 30);
        Assert.Equal(AlundraInventoryPortrait.StateAtRest, Portrait.State);
    }

    [Fact]
    public void Close_ReturnStepsInItsOwnTick_AndEndsBeforeTheScreenIsRemoved()
    {
        var worldProxy = NewWorld();
        Press(worldProxy, AlundraPadState.Start);
        Ticks(worldProxy, 18); // the 18-call opening slide, then input is accepted
        Assert.Equal(AlundraInventoryPortrait.StateAtRest, Portrait.State);

        Press(worldProxy, AlundraPadState.Start); // tick M: close (render half), the return's first step in M.
        Assert.Equal(AlundraInventoryPortrait.StateReturning, Portrait.State);
        Assert.Equal((248, 104, 48, 56), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));

        Ticks(worldProxy, 15); // M+15: call 16, idle.
        Assert.Equal(AlundraInventoryPortrait.StateIdle, Portrait.State);
        Assert.False(Portrait.IsVisible);

        // The slide-out lasts longer than the return: the screen is still drawn when the portrait is gone.
        Assert.True(AlundraInventoryDirector.Instance.IsDrawn);
    }

    [Fact]
    public void ToggleToTheSubInventory_ReturnsThenStartsAgain_InThePostProcessTick()
    {
        var worldProxy = NewWorld();
        var head = ExpectedHead(worldProxy);
        Press(worldProxy, AlundraPadState.Start);
        Ticks(worldProxy, 18);

        Press(worldProxy, AlundraPadState.L1); // tick M: the main inventory's return.
        Assert.Equal(AlundraInventoryPortrait.StateReturning, Portrait.State);

        var ticksAfterReturn = 0;
        while (!AlundraSubInventoryDirector.Instance.IsActive && ticksAfterReturn < 60)
        {
            Ticks(worldProxy, 1);
            ticksAfterReturn++;
        }

        Assert.True(AlundraSubInventoryDirector.Instance.IsActive);

        // The post-process opened the sub-inventory in this tick: its start was not dropped by the idle guard (the
        // return ended at M+15), and it already made its first step here.
        Assert.True(ticksAfterReturn >= 16, $"the sub-inventory opened {ticksAfterReturn} ticks after the return: the start would be dropped");
        Assert.Equal(AlundraInventoryPortrait.StateOpening, Portrait.State);
        Assert.Equal((head.X, head.Y, 0, 0), (Portrait.X, Portrait.Y, Portrait.DrawnWidth, Portrait.DrawnHeight));
    }

    [Fact]
    public void ToggleBackToTheMainInventory_StartsInTheHeadTickOfThePostProcess()
    {
        var worldProxy = NewWorld();
        var head = ExpectedHead(worldProxy);
        Press(worldProxy, AlundraPadState.Start);
        Ticks(worldProxy, 18);
        Press(worldProxy, AlundraPadState.L1);
        for (var i = 0; i < 60 && !AlundraSubInventoryDirector.Instance.IsActive; i++)
        {
            Ticks(worldProxy, 1);
        }

        Ticks(worldProxy, 25); // the sub-inventory's own opening slide, then input is accepted
        Assert.Equal(AlundraInventoryPortrait.StateAtRest, Portrait.State);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);

        Press(worldProxy, AlundraPadState.L1); // the sub-inventory's return, in its own tick.
        Assert.Equal(AlundraInventoryPortrait.StateReturning, Portrait.State);
        Assert.Equal(48, Portrait.DrawnWidth);

        var ticks = 0;
        while (Portrait.State != AlundraInventoryPortrait.StateOpening && ticks < 60)
        {
            Ticks(worldProxy, 1);
            ticks++;
        }

        // The main inventory's head ran from the post-process in this very tick (its setup follows next tick) and
        // the portrait already stepped once.
        Assert.Equal(AlundraInventoryPortrait.StateOpening, Portrait.State);
        Assert.True(AlundraInventoryDirector.Instance.IsCallbackArmed);
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

    private static AlundraItemTables TablesWithPortrait(Guid? portraitId)
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

            return new AlundraItemTables(projectPath);
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
        var presenter = new AlundraInventoryPresenter(
            AlundraInventoryDirector.Instance, worldProxy.GameState, TablesWithPortrait(portraitId), viewModel, new FakeScreen(), null);

        Press(worldProxy, AlundraPadState.Start);
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

    [Fact]
    public void Presenter_WithoutAPortraitIndex_KeepsThePortraitHidden()
    {
        var worldProxy = NewWorld();
        var viewModel = new AlundraInventoryViewModel();
        var presenter = new AlundraInventoryPresenter(
            AlundraInventoryDirector.Instance, worldProxy.GameState, TablesWithPortrait(null), viewModel, new FakeScreen(), null);

        Press(worldProxy, AlundraPadState.Start);
        Ticks(worldProxy, 15);
        presenter.Tick();

        Assert.True(Portrait.IsVisible);
        Assert.Equal(Visibility.Collapsed, viewModel.Portrait.Visibility);
        Assert.Null(viewModel.Portrait.SourceName);
    }
}
