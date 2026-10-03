#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E16.d T5 (docs/plan-e16-etat-partie.md, K8, D-E16-19, SC7, SD5, SD8, SD9): the pending load applied on its
/// arrival map - field by field, once, only on the save's own map, with the session reset around it - and, end to
/// end on the real map 389 installation, applied BEFORE the first tick so the entities' load programs read the
/// loaded flag 860. The rules are injected (K1: the real export, a catalog predicate that answers yes; the
/// end-to-end test never touches the asset catalog); the service is a fake (D-E16-31).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveGameApplyTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Flag 860 (<c>0x35C</c>), the intro guard of map 389: word 26, bit 28, as opcode <c>0x31</c>
    /// <c>[92, 3, …]</c> tests it (<c>docs/intro-programs-389.txt</c>, offsets 143, 180, 234).</summary>
    private const uint Flag860 = 860;
    private const uint Flag860Mask = 1u << 28;

    private readonly HeldKeys _keys = new();
    private readonly FakeSaveSlots _slots = new();
    private readonly GameManager _gameManager;
    private readonly string _root;

    public AlundraSaveGameApplyTests()
    {
        ResetSingletons();
        _root = FindProjectRoot();
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = true;
        Director.KeyHeldProviderForTests = _keys.IsHeld;
        Director.SaveSlots = _slots;
        Director.RulesFactoryForTests = RealRules;
        _slots.Slots.Add(new AlundraSlotEntry("debug-json", true, T0));

        _gameManager = BuildGameManager();
        AlundraWarpDirector.Instance.AttachToWorld(_gameManager, null, _root);
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static AlundraSaveGameDirector Director => AlundraSaveGameDirector.Instance;

    private static AlundraGameState State => AlundraGameState.Instance;

    /// <summary>A save carrying something in every field, valid against the real export.</summary>
    private static AlundraSaveGame RichSave()
    {
        var save = ValidSave(tileX: 33, tileY: 59, tileZ: 0);
        save.GameFlags[0] = 0x8000_0001;
        save.GameFlags[26] = Flag860Mask;
        save.GameFlags[63] = 0x0F0F_0F0F;
        save.MapIdToInternalMapIndexTable[5] = 390;
        save.HpMax = 20;
        save.Hp = 15;
        save.MpMax = 2;
        save.Mp = 1;
        save.Money = 500;
        save.WeaponId = 1;
        save.ItemId = 17;
        save.FalconTemp = 3;
        save.Falcon = 4;
        save.NumberOfItems[1 * 2 + 1] = 1;
        save.GameTime = 123_456;
        save.DeathRetryCount = 2;
        Assert.True(save.TryValidate(RealRules(), out var error), error);
        return save;
    }

    /// <summary>F9, from a hero at the New Game tile: the load becomes pending and its departure is armed.</summary>
    private void ArmLoad(AlundraSaveGame save)
    {
        _slots.LoadedSave = save;
        _keys.Held.Clear();
        Director.UpdateRecipeKeys(null, State, Map389WorldName, HeroAt());
        _keys.Held.Add(Keys.F9);
        Director.UpdateRecipeKeys(null, State, Map389WorldName, HeroAt());
        _keys.Held.Clear();
        Assert.True(Director.HasPendingLoad);
    }

    private static void AssertStateIsTheSave(AlundraSaveGame save)
    {
        Assert.Equal(save.GameFlags, State.GameFlags.Take(64));
        Assert.All(State.GameFlags.Skip(64), word => Assert.Equal(0u, word));
        Assert.Equal(save.MapIdToInternalMapIndexTable, State.MapIdToInternalMapIndexTable);
        Assert.Equal(save.NumberOfItems, State.NumberOfItems);
        var s = State.PlayerStats;
        Assert.Equal(
            (save.Hp, save.HpMax, save.Mp, save.MpMax, save.Money, save.WeaponId, save.ItemId, save.FalconTemp, save.Falcon),
            (s.Hp, s.HpMax, s.Mp, s.MpMax, s.Money, s.WeaponId, s.ItemId, s.FalconTemp, s.Falcon));
        Assert.Equal(save.GameTime, State.GameTime);
        Assert.Equal(save.DeathRetryCount, State.DeathRetryCount);
    }

    // ---- ApplyPendingLoad (K8) --------------------------------------------------------------------------------

    [Fact]
    public void ApplyPendingLoad_OnItsArrivalMap_LeavesTheStateOfTheSave_FieldByField()
    {
        var save = RichSave();
        State.TextCategoryIndex = 3;
        State.GameVariables[1] = 42;
        State.GameVariables[3] = -7;
        State.GameFlags[100] = 0xFFFF;
        State.PlayerStats.Money = 9;
        ArmLoad(save);

        // What a session holds by the arrival: temporary flags, an interact latch.
        State.TemporaryFlags[4] = 0xABCD;
        State.InteractLatchEntity = new AlundraEntityScriptProxy();
        State.InteractLatchFacing = 2;

        // The arrival map's own order (AlundraWorldProxy.InitializeWithWorld): map entry, then the load.
        State.InstallForMapEntry();
        Director.ApplyPendingLoad(State, 389);

        AssertStateIsTheSave(save);
        Assert.All(State.TemporaryFlags, word => Assert.Equal(0u, word)); // the map entry's.
        Assert.Equal(0, State.TextCategoryIndex); // D-E16-19.
        Assert.All(State.GameVariables, value => Assert.Equal(0, value)); // D-E16-19.
        Assert.Null(State.InteractLatchEntity);
        Assert.Equal(0, State.InteractLatchFacing);
        Assert.Equal(0u, State.PlayerControlFlags);
        Assert.True(State.NewGameInventoryInitialized); // SC7.
        Assert.False(Director.HasPendingLoad);

        // The jauge shows the loaded values at once (SC1).
        var hud = AlundraHudDirector.Instance;
        Assert.Equal((15, 20, 1, 2, 500), (hud.Hp, hud.HpMax, hud.Mp, hud.MpMax, hud.Money));
        Assert.Equal((0, 0, 0, 0), hud.SubStepsForTests);

        // The game time resumes from the loaded value.
        State.AdvanceGameTime(1f);
        Assert.Equal(save.GameTime + 60u, State.GameTime);
    }

    /// <summary>SC7: after a load, a later map entry WITHOUT a warp arrival (the real map 389 installation) does
    /// not run the New Game inventory over the loaded items and weapon.</summary>
    [Fact]
    public void AfterALoad_AMapEntryWithoutAWarpArrival_KeepsTheLoadedItems()
    {
        var save = RichSave();
        save.WeaponId = 3;
        save.NumberOfItems[1 * 2 + 1] = 0;
        ArmLoad(save);
        State.InstallForMapEntry();
        Director.ApplyPendingLoad(State, 389);
        Assert.True(State.NewGameInventoryInitialized);

        AlundraWarpDirector.Instance.ConsumeArrivalRecord(); // the load's own arrival is over.
        Assert.False(AlundraWarpDirector.Instance.HasPendingArrival);

        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        AddHeroPawn(world);
        InitializeWithRealProject(new AlundraWorldProxy(), world);

        Assert.Equal(3, State.PlayerStats.WeaponId); // the New Game inventory would put the sword's slot (1) back.
        Assert.All(State.NumberOfItems, count => Assert.Equal(0, count)); // ... and its items in.
    }

    /// <summary>E19.r R1: a save carrying the latch G1662 (the jauge was brought on before the save) loaded onto its
    /// arrival map: the load runs inside the map installation, then the HUD install runs the map-entry call
    /// (0x8002c3d0), which sees the loaded latch and arms the appearance; without the latch in the save, nothing.</summary>
    [Theory]
    [InlineData(true, AlundraHudDirector.HudPhase.Opening)]
    [InlineData(false, AlundraHudDirector.HudPhase.Idle)]
    public void AMapEntryAfterALoad_ArmsTheHud_OnlyWhenTheSaveCarriesTheLatch(bool latchInSave, AlundraHudDirector.HudPhase expected)
    {
        var save = RichSave();
        save.GameFlags[AlundraHudDirector.PersistentLatchFlag >> 5] = latchInSave ? AlundraHudDirector.PersistentLatchMask : 0u;
        Assert.True(save.TryValidate(RealRules(), out var error), error);
        ArmLoad(save);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, AlundraHudDirector.Instance.Phase);

        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        AddHeroPawn(world);
        InitializeWithRealProject(new AlundraWorldProxy(), world);

        Assert.False(Director.HasPendingLoad);
        Assert.Equal(expected, AlundraHudDirector.Instance.Phase);
    }

    [Theory]
    [InlineData(390)]
    [InlineData(null)]
    public void ApplyPendingLoad_OnAnotherMap_AppliesNothing_AndAbandonsTheLoad(int? arrivalMapId)
    {
        ArmLoad(RichSave());
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        Director.ApplyPendingLoad(State, arrivalMapId);

        Assert.False(Director.HasPendingLoad);
        StateSnapshot.Take(State).AssertSameGameStateAs(before);
        Assert.Contains(log.Warnings, line => line.Contains("pending load of map 389 abandoned"));

        // Abandoned for good: the save's own map arriving later applies nothing either.
        Director.ApplyPendingLoad(State, 389);
        StateSnapshot.Take(State).AssertSameGameStateAs(before);
    }

    /// <summary>SD9: without a pending load nothing changes, and a second call after an application re-applies
    /// nothing.</summary>
    [Fact]
    public void ApplyPendingLoad_WithoutAPendingLoad_ChangesNothing_AndNeverAppliesTwice()
    {
        State.TextCategoryIndex = 5;
        State.PlayerStats.Money = 9;
        var untouched = StateSnapshot.Take(State);
        Director.ApplyPendingLoad(State, 389);
        StateSnapshot.Take(State).AssertSameGameStateAs(untouched);

        ArmLoad(RichSave());
        Director.ApplyPendingLoad(State, 389);
        Assert.Equal(500, State.PlayerStats.Money);

        State.PlayerStats.Money = 1;
        State.TextCategoryIndex = 2;
        State.AddFlag(1, 1u << 4);
        var afterPlay = StateSnapshot.Take(State);

        Director.ApplyPendingLoad(State, 389);

        StateSnapshot.Take(State).AssertSameGameStateAs(afterPlay);
    }

    /// <summary>SD9: an early return of <see cref="AlundraWorldProxy.InitializeWithWorld"/> before the application
    /// (a world without a tile map) abandons the pending load, with a message.</summary>
    [Fact]
    public void AnArrivalWorldWithoutATileMap_AbandonsThePendingLoad()
    {
        ArmLoad(RichSave());
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        new AlundraWorldProxy().InitializeWithWorld(new World { Name = "Ship Klark (beginning)-389" });

        Assert.False(Director.HasPendingLoad);
        Assert.Contains(log.Warnings, line => line.Contains("pending load of map 389 abandoned"));
        StateSnapshot.Take(State).AssertSameGameStateAs(before);
    }

    /// <summary>SD5: Start pressed during the load's fade does not open the inventory - since the SD5 fix
    /// (D-E13D-38, merged into main before E16) its trigger refuses while a warp transition runs, and the load's
    /// departure is one; after <see cref="AlundraSaveGameDirector.ApplyPendingLoad"/> both inventories are still
    /// at rest, <c>PlayerControlFlags</c> is 0, and N ticks run without an exception.</summary>
    [Fact]
    public void StartPressedDuringTheLoadsFade_DoesNotOpenTheInventory_AndTheApplicationLeavesItAtRest()
    {
        var tables = ItemTablesFixture.LoadReal();
        AlundraInventoryDirector.Instance.AttachToWorld(State, tables, null);
        AlundraSubInventoryDirector.Instance.AttachToWorld(State, tables, null);
        ArmLoad(RichSave());
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);

        InventoryTick(AlundraPadState.Start);
        InventoryTicks(10);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        Assert.Equal(0u, State.PlayerControlFlags);

        State.InstallForMapEntry();
        Director.ApplyPendingLoad(State, 389);

        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        Assert.False(AlundraInventoryDirector.Instance.IsCallbackArmed);
        Assert.False(AlundraSubInventoryDirector.Instance.IsActive);
        Assert.Equal(0, AlundraInventoryPostProcess.Instance.State);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, AlundraInventoryPortrait.Instance.State);
        Assert.Equal(0u, State.PlayerControlFlags);

        var exception = Record.Exception(() =>
        {
            for (var tick = 0; tick < 120; tick++)
            {
                InventoryTick(tick % 30 == 0 ? AlundraPadState.Start : 0u);
                AlundraHudDirector.Instance.Tick();
            }
        });
        Assert.Null(exception);
    }

    // ---- End to end on the real map 389 (SD8) -------------------------------------------------------------------

    private static void SetPrivate(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static IReadOnlyList<AlundraEntityScriptProxy> Spawned(AlundraWorldProxy proxy)
        => ((IEntityWorldContext)proxy).SpawnedEntities;

    private static AlundraEntityScriptProxy LoadEntity(AlundraWorldProxy proxy, int loadIndex)
        => Assert.Single(Spawned(proxy), e => !e.IsPlayer && e.ProgramIndexes[ScriptHelper.ProgramALoad] == loadIndex);

    /// <summary>One frame the engine's way: every spawned entity's own <c>Update</c> (their load program, slot A,
    /// on their first), then the world's. The hero is not stepped: this headless montage has no input for it,
    /// and the loaded values' readers under test are the other entities' load programs.</summary>
    private static void OneFrame(AlundraWorldProxy proxy)
    {
        foreach (var entity in Spawned(proxy).Where(e => !e.IsPlayer).ToList())
        {
            entity.Update(0.02f);
        }

        proxy.Update(0.02f);
    }

    /// <summary>
    /// SD8, mandatory: F9 in a session where flag 860 is OFF, from a save where it is ON; the departure hands map
    /// 389 to the engine; the real map 389 installation (<see cref="AlundraWorldProxy.InitializeWithWorld"/>), then
    /// the first frame. The load programs that test flag 860 with <c>0x31</c> (<c>docs/intro-programs-389.txt</c>:
    /// load 134 at offset 143, load 139 at offset 234; load 135 at offset 180 is spawned by load 134's own
    /// <c>0x2D</c>) take the LOADED flag's branch - <c>0x31</c> skips when the flag is off
    /// (<c>AlundraEventProgramRunner.cs:1416</c>), so with it on their <c>0x64</c> placement runs and each entity
    /// ends at the position that placement gives. The hero is at the saved tile, the New Game one (33, 59, 0),
    /// where those entities spawn (checked first), and the New Game inventory does not run.
    /// <para>Two mutations of the production code, made and undone by a script, each fail this test: removing
    /// the <c>ApplyPendingLoad</c> call (nothing is loaded), and moving it to the end of the arrival world's first
    /// <c>Update</c>, after the entity pass (the load programs have read the old flag).</para>
    /// </summary>
    [Fact]
    public void EndToEnd_F9_ThenTheRealMap389Installation_TheLoadProgramsReadTheLoadedFlag860()
    {
        var save = ValidSave(tileX: 33, tileY: 59, tileZ: 0);
        save.GameFlags[26] = Flag860Mask;
        State.TextCategoryIndex = 1;
        Assert.Equal(0u, State.GetFlag(Flag860) & Flag860Mask); // the session's flag 860 is off.

        // F9 on the departure map, then the departure until the engine is asked for map 389.
        ArmLoad(save);
        Assert.True(AlundraWarpDirector.Instance.TryResolveWorldPath(389, out var map389Path));
        for (var tick = 0; tick < 100 && GetPendingWorldToLoad(_gameManager) == null; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraWarpDirector.Instance.Advance(1);
        }

        Assert.Equal(map389Path, GetPendingWorldToLoad(_gameManager));
        Assert.Equal(0u, State.GetFlag(Flag860) & Flag860Mask); // nothing applied before the arrival.

        // The arrival: the real map 389 installation.
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var heroEntity = AddHeroPawn(world);
        var proxy = new AlundraWorldProxy();
        InitializeWithRealProject(proxy, world);
        SetPrivate(proxy._backdropStage, "_clearColorApplied", true); // the headless montage has no view (BackdropPushProductionTests).

        // The entities of those load programs have spawned, still at their record positions.
        var load134 = LoadEntity(proxy, 134);
        var load139 = LoadEntity(proxy, 139);
        Assert.Equal(EntityStatus.Loaded, load134.Status);
        Assert.Equal(EntityStatus.Loaded, load139.Status);
        Assert.NotEqual(0x24F << 16, load134.PosX);
        Assert.NotEqual(0x234 << 16, load139.PosX);
        Assert.DoesNotContain(Spawned(proxy), e => e.ProgramIndexes[ScriptHelper.ProgramALoad] == 135);

        // The hero at the saved tile; the New Game inventory did not run (the save carries no item).
        var hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);
        Assert.Equal((33, 59, 0), (hero.TileX, hero.TileY, hero.TileZ));
        Assert.Equal((33 * 24 + 12) << 16, hero.PosX);
        Assert.Equal((59 * 16 + 8) << 16, hero.PosY);
        Assert.All(State.NumberOfItems, count => Assert.Equal(0, count));

        // The first frame: the load programs run, on the LOADED flag. The state is checked AFTER it, so a load
        // applied too late - at the end of this frame - fails on the entities, not on the state itself.
        OneFrame(proxy);

        Assert.Equal(0, State.TextCategoryIndex); // D-E16-19: the load reset it.
        Assert.Equal(Flag860Mask, State.GetFlag(Flag860) & Flag860Mask);
        Assert.Equal((0x24F << 16, 0x248 << 16), (load134.PosX, load134.PosY)); // offset 148's 0x64.
        Assert.Equal(12u, load134.TargetAnimationId);
        Assert.Equal((0x234 << 16, 0x178 << 16), (load139.PosX, load139.PosY)); // offset 239's 0x64.
        Assert.Equal(1u, load139.TargetAnimationId);

        // Load 135, activated by load 134's 0x2D, runs its own load program (offset 180) on the next frame.
        OneFrame(proxy);
        var load135 = LoadEntity(proxy, 135);
        Assert.Equal((0x158 << 16, 0x248 << 16), (load135.PosX, load135.PosY)); // offset 185's 0x64.
    }
}
