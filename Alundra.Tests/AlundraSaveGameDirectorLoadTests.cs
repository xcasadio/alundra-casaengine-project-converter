#nullable enable
using System;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E16.d T4 (docs/plan-e16-etat-partie.md, K7, SD1 to SD4, SD13): the load key F9 up to the departure - each
/// precondition refused before any load, each service status but "loaded" refused, a save the validation refuses
/// never departing, the departure checked before it is armed (warp disabled, transition, no GameManager, a map
/// the warp director cannot resolve), then the load made pending and the departure armed with the New Game
/// animation, while the game state stays untouched until the arrival. The rules are injected (K1: real export,
/// catalog answering yes); the service is a fake (D-E16-31).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveGameDirectorLoadTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly HeldKeys _keys = new();
    private readonly FakeSaveSlots _slots = new();
    private readonly GameManager _gameManager;
    private readonly string _root;

    public AlundraSaveGameDirectorLoadTests()
    {
        ResetSingletons();
        _root = FindProjectRoot();
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = true;
        Director.KeyHeldProviderForTests = _keys.IsHeld;
        Director.SaveSlots = _slots;
        Director.RulesFactoryForTests = RealRules;

        _slots.Slots.Add(new AlundraSlotEntry("debug-json", true, T0));
        _slots.LoadedSave = ValidSave(tileX: 20, tileY: 30, tileZ: 1);

        _gameManager = BuildGameManager();
        AlundraWarpDirector.Instance.AttachToWorld(_gameManager, null, _root);
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static AlundraSaveGameDirector Director => AlundraSaveGameDirector.Instance;

    private static AlundraGameState State => AlundraGameState.Instance;

    private void PressF9(AlundraEntityScriptProxy? hero)
    {
        _keys.Held.Clear();
        Director.UpdateRecipeKeys(null, State, Map389WorldName, hero);
        _keys.Held.Add(Keys.F9);
        Director.UpdateRecipeKeys(null, State, Map389WorldName, hero);
        _keys.Held.Clear();
    }

    private static void AssertNoDeparture()
    {
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);
        Assert.False(Director.HasPendingLoad);
    }

    // ---- Preconditions (K7 step 1) ------------------------------------------------------------------------------

    [Theory]
    [InlineData(SaveGameBlocker.NoHero)]
    [InlineData(SaveGameBlocker.DialogueOpen)]
    [InlineData(SaveGameBlocker.InventoryOpen)]
    [InlineData(SaveGameBlocker.SubInventoryOpen)]
    [InlineData(SaveGameBlocker.PostProcessPending)]
    [InlineData(SaveGameBlocker.PortraitOpening)]
    [InlineData(SaveGameBlocker.PortraitAtRest)]
    [InlineData(SaveGameBlocker.PortraitReturning)]
    [InlineData(SaveGameBlocker.TransitionInProgress)]
    [InlineData(SaveGameBlocker.MasterMusicFadeArmed)]
    [InlineData(SaveGameBlocker.ControlFlagsNonZero)]
    public void EachPreconditionNotHeld_IsRefused_BeforeAnyLoad_StateIdentical(SaveGameBlocker blocker)
    {
        var (hero, reason) = PoseBlocker(blocker);
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(hero);

        Assert.Empty(_slots.LoadCalls);
        Assert.Equal(0, _slots.ListCalls);
        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains(reason));
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    /// <summary>The load does not check the ground (K7 step 1 has no such precondition, unlike K6).</summary>
    [Fact]
    public void HeroInTheAir_DoesNotBlockTheLoad()
    {
        var hero = HeroAt();
        hero.IsOnGround = 0;

        PressF9(hero);

        Assert.True(Director.HasPendingLoad);
    }

    /// <summary>SD13: a second F9 while a load is pending is refused before loading anything - even once the
    /// transition gate itself is lifted.</summary>
    [Fact]
    public void F9_WhileALoadIsPending_IsRefused_BeforeAnyLoad()
    {
        PressF9(HeroAt());
        Assert.True(Director.HasPendingLoad);
        AlundraWarpDirector.Instance.InstallForMapEntry(); // lifts the gate, the load stays pending.
        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Single(_slots.LoadCalls);
        Assert.Equal(1, _slots.ListCalls);
        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("already pending"));
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    [Fact]
    public void NoReadableSlot_IsRefused_WithoutLoading()
    {
        _slots.Slots.Clear();
        _slots.Slots.Add(new AlundraSlotEntry("broken", false, T0));
        var before = StateSnapshot.Take(State);

        PressF9(HeroAt());

        Assert.Empty(_slots.LoadCalls);
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    // ---- Service results and validation (K7 steps 3 and 4) -----------------------------------------------------

    public static TheoryData<SaveGameLoadStatus> EveryStatusButLoaded()
    {
        var data = new TheoryData<SaveGameLoadStatus>();
        foreach (var status in Enum.GetValues<SaveGameLoadStatus>().Where(s => s != SaveGameLoadStatus.Loaded))
        {
            data.Add(status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusButLoaded))]
    public void EveryLoadStatusButLoaded_IsRefused_StateIdentical_NoDeparture(SaveGameLoadStatus status)
    {
        _slots.LoadStatus = status;
        _slots.ReturnObjectOnFailure = true; // even an object handed back with a failure is never used.
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Equal(new[] { "debug-json" }, _slots.LoadCalls);
        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains(status.ToString()));
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    [Fact]
    public void Loaded_WithoutAnObject_IsRefused()
    {
        _slots.LoadedSave = null;
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("without an object"));
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    [Fact]
    public void ASaveTheValidationRefuses_IsRefused_StateIdentical_NoDeparture()
    {
        _slots.LoadedSave!.Money = -1;
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("playerStats.money = -1"));
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    // ---- The departure check (K7 step 5) ---------------------------------------------------------------------

    [Fact]
    public void WarpDisabled_IsRefused_StateIdentical_NoDeparture()
    {
        State.IsWarpDisabled = true;
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("warp is disabled"));
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    [Fact]
    public void NoGameManagerAttached_IsRefused_StateIdentical_NoDeparture()
    {
        AlundraWarpDirector.Instance.AttachToWorld(null, null, _root);
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("no GameManager"));
        AssertNoDeparture();
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    /// <summary>The transition case of step 1 already covers "transition in progress"; this one arms it with the
    /// other departure entry, then checks nothing of the load is armed on top of it.</summary>
    [Fact]
    public void TransitionInProgress_IsRefused_NoLoadPending()
    {
        AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(390, 0, 0, 0, 0, 0, HeroAt(), new AlundraGameState());
        var before = StateSnapshot.Take(State);

        PressF9(HeroAt());

        Assert.False(Director.HasPendingLoad);
        Assert.Empty(_slots.LoadCalls);
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    /// <summary>SD1: a map the validation accepts but the warp director's own table cannot resolve (here, a
    /// project folder with no <c>world-index.json</c>) is refused BEFORE anything departs - no fade, no music
    /// request, no arrival, no pending load. Otherwise the abort guard would lift the gate with the screen left
    /// white and the music fading.</summary>
    [Fact]
    public void AMapTheWarpDirectorCannotResolve_IsRefused_WithoutFadeMusicOrArrival()
    {
        var emptyProject = Path.Combine(Path.GetTempPath(), "alundra-e16d-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyProject);
        try
        {
            AlundraWarpDirector.Instance.AttachToWorld(_gameManager, null, emptyProject);
            Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
            var before = StateSnapshot.Take(State);
            using var log = LogCapture.Install();

            PressF9(HeroAt());

            Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("map 389 has no world"));
            AssertNoDeparture();
            Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
            Assert.Null(GetMusicPendingWarpDeparture());
            StateSnapshot.Take(State).AssertSameAs(before);
        }
        finally
        {
            Directory.Delete(emptyProject, recursive: true);
        }
    }

    // ---- Success (K7 step 6) ----------------------------------------------------------------------------------

    [Fact]
    public void Success_PosesThePendingLoad_ArmsTheDepartureToTheSavedTile_AndLeavesTheGameStateUntouched()
    {
        State.AddFlag(1, 1);
        State.PlayerStats.Money = 77;
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        PressF9(HeroAt());

        Assert.True(Director.HasPendingLoad);
        var warp = AlundraWarpDirector.Instance;
        Assert.True(warp.IsTransitionInProgress);
        Assert.True(warp.HasPendingArrival);
        var arrival = warp.ArrivalRecordForTests;
        Assert.Equal(389u, arrival.MapIndex);
        Assert.Equal((20 * 24 + 12) << 16, arrival.PosX);
        Assert.Equal((30 * 16 + 8) << 16, arrival.PosY);
        Assert.Equal(1 << 20, arrival.PosZ);
        Assert.Equal(0, arrival.EffectId);
        Assert.True(warp.TryResolveWorldPath(389, out var path));
        Assert.Equal(path, warp.PendingWorldPathForTests);
        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled); // the departure's own fade.
        Assert.Null(GetMusicPendingWarpDeparture()); // empty sound action: the arrival sets the music.

        StateSnapshot.Take(State).AssertSameGameStateAs(before); // nothing applied before the arrival (K7).
        Assert.Contains(log.Infos, line => line.Contains("departing to map 389 tile (20, 30, 1)"));
    }

    /// <summary>SD2: F9 pressed during an attack or a jump - the arrival carries the New Game animation
    /// <c>0x36</c> and direction 0, never the hero's current ones.</summary>
    [Theory]
    [InlineData(0x0Cu, 3u)]
    [InlineData(0x21u, 0x10u)]
    public void Success_DuringAnAttackOrAJump_ArrivesWithAnimation0x36AndDirection0(uint currentAnimation, uint currentDirection)
    {
        var hero = HeroAt();
        hero.TargetAnimationId = currentAnimation;
        hero.TargetDirection = currentDirection;
        hero.IsOnGround = 0;

        PressF9(hero);

        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(AlundraGameState.ResetAnimationId, arrival.AnimationId);
        Assert.Equal(0x36u, arrival.AnimationId);
        Assert.Equal(AlundraGameState.ResetDirectionId, arrival.DirectionId);
    }

    /// <summary>A departure aborted by the warp director's abort guard (forced here by re-attaching it without a
    /// GameManager after the departure is armed, <c>AlundraWarpDirector.cs</c> Advance) abandons the pending load:
    /// no later arrival applies it.</summary>
    [Fact]
    public void ADepartureAbortedByTheAbortGuard_AbandonsThePendingLoad()
    {
        PressF9(HeroAt());
        Assert.True(Director.HasPendingLoad);
        AlundraWarpDirector.Instance.AttachToWorld(null, null, _root);
        using var log = LogCapture.Install();

        for (var tick = 0; tick < 100 && AlundraWarpDirector.Instance.IsTransitionInProgress; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraWarpDirector.Instance.Advance(1);
        }

        Assert.False(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(Director.HasPendingLoad);
        Assert.Contains(log.Warnings, line => line.Contains("pending load of map 389 abandoned"));
    }

    // ---- SD3: the frame of the key ------------------------------------------------------------------------------

    private sealed class CountingRunner : IEventProgramRunner
    {
        public int ScriptRuns;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => ScriptRuns++;

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    /// <summary>
    /// SD3: F9 on a frame where a map event would run - the key is read at the head of
    /// <see cref="AlundraWorldProxy.Update"/>, before the gameplay gate is computed, so the departure it arms
    /// freezes that same frame's map-event pass: the event does not run (it did on the frame before, proving it
    /// would have).
    /// </summary>
    [Fact]
    public void F9_OnAFrameWhereAMapEventWouldRun_TheEventDoesNotRunThatFrame()
    {
        var world = new World { Name = "TestWorld" };
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });
        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // no tileMap: returns before the installs, the warp director stays attached.

        var hero = HeroAt();
        hero.Status = EntityStatus.Normal;
        proxy.PlayerEntity = hero;
        var record = new TileMapObjectData();
        record.CustomProperties["EventCodesBIndex"] = "129";
        record.CustomProperties["Index"] = "1";
        record.CustomProperties["X1"] = "0";
        record.CustomProperties["Y1"] = "0";
        record.CustomProperties["X2"] = "100";
        record.CustomProperties["Y2"] = "100";
        var layer = new TileMapObjectLayerData();
        layer.Objects.Add(record);
        proxy.BuildMapEvents(layer);
        var runner = new CountingRunner();
        proxy.EventProgramRunner = runner;

        proxy.Update(0.02f);
        Assert.Equal(1, runner.ScriptRuns); // the event runs on a frame without the key.

        _keys.Held.Add(Keys.F9);
        proxy.Update(0.02f);

        Assert.True(Director.HasPendingLoad);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.Equal(1, runner.ScriptRuns); // frozen on the very frame of the key.
    }
}
