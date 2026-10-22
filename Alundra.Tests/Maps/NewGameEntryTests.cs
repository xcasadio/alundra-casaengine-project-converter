#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.UI.Hud;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Audio;
using Alundra.Tests.Transitions;

namespace Alundra.Tests.Maps;

/// <summary>
/// The New Game entry in <see cref="WorldProxy.AdoptPlayerPawn"/> (no pending warp arrival): the
/// original's New Game stats and inventory, and a HUD that stays hidden until a map script asks for it
/// (docs/plan-e13-hud.md §1.6). This class used to test the <c>ALUNDRA_HUD_DEBUG</c> recipe too (E13 C5.a,
/// D-E13-10), removed at the author's request on 2026-09-27; the tests of the game's own behaviour stay.
///
/// Drives the real <see cref="WorldProxy.InitializeWithWorld"/> through its full installation
/// block against map 389 (the New Game map) - same real-map/hand-built-pawn montage as
/// <see cref="WorldProxyGlobalFreezeTests.BuildRealMap389World"/> (reused directly, internal in
/// the same assembly) and <see cref="WarpArrivalTests"/>'s own hero-pawn/player-controller
/// fixtures (re-built here, since those are private to that class).
///
/// <para>F5 (docs/plan-bgm-demarrage-binaire.md): the real map 389 install below writes the
/// SESSION-scoped <see cref="BgmPlayer.Instance"/> (map entry arms its reset flag, B7) - so
/// this class shares <see cref="MusicPlayerSingletonCollection"/> with every other class touching
/// that same shared instance, and resets <see cref="BgmFadeDirector.Instance"/> too.</para>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class NewGameEntryTests : IDisposable
{
    public NewGameEntryTests()
    {
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        ScreenFadeDirector.Instance.ResetForTests();
        BgmPlayer.Instance.ResetForTests();
        BgmFadeDirector.Instance.ResetForTests(); // F5: joins the music-player singleton collection.
        HudDirector.Instance.ResetForTests(); // E19.r R1: the map entry now arms the jauge.
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        ScreenFadeDirector.Instance.ResetForTests();
        BgmPlayer.Instance.ResetForTests();
        BgmFadeDirector.Instance.ResetForTests();
        HudDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------------
    // Fixture plumbing - hero pawn + real HeroController, same shape as
    // WarpArrivalTests.BuildHeroPawnEntity/RegisterPlayerController (private there, rebuilt here).
    // -----------------------------------------------------------------------------------------------

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>Hand-built hero pawn - root <see cref="TransformComponent"/> plus sibling
    /// <see cref="CollisionComponent"/>, same fixture shape as
    /// <see cref="WarpArrivalTests.BuildHeroPawnEntity"/> - AdoptPlayerPawn only needs a box
    /// fixture for ClampToGround to sample, no CharacterControllerComponent.</summary>
    private static Entity BuildHeroPawnEntity(World world)
    {
        var root = new TransformComponent();
        var collisionComponent = new CollisionComponent();
        collisionComponent.Fixtures.Add(new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(21f, 15f, 32f) },
            LocalPosition = new Vector3(0.5f, 0.5f, 16f),
            LocalRotation = Quaternion.Identity,
        });
        root.AddChildComponent(collisionComponent);

        var entity = new Entity
        {
            Name = "AlundraHeroTestPawn",
            RootComponent = root,
            GameplayProxyClassName = typeof(EntityScriptProxy).FullName!,
        };
        entity.Initialize();
        SetProperty(entity, nameof(Entity.World), world); // ClampToGround reads Owner.World.CollisionField.

        Assert.IsType<EntityScriptProxy>(entity.GameplayProxy);
        return entity;
    }

    /// <summary>Possesses <paramref name="pawn"/> with a real <see cref="HeroController"/> and
    /// registers it into <paramref name="world"/>'s own private <c>World.PlayerControllers</c> backing
    /// list - same reflection precedent as <see cref="WarpArrivalTests.RegisterPlayerController"/>
    /// (private there).</summary>
    private static void RegisterPlayerController(World world, Entity pawn)
    {
        var controller = new HeroController();
        controller.Possess(pawn);

        var field = typeof(World).GetField("_playerControllers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (List<PlayerController>)field!.GetValue(world)!;
        list.Add(controller);
    }

    /// <summary>Builds map 389 (the New Game map) with a real player
    /// controller adopted, then runs <see cref="WorldProxy.InitializeWithWorld"/> end to end -
    /// <c>EngineEnvironment.ProjectPath</c> is set/restored around the call exactly like
    /// <see cref="WarpArrivalTests"/>'s own acceptance tests, since map events/dialogue strings load
    /// from it.</summary>
    private static WorldProxy InitializeNewGameEntryOnMap389()
    {
        var (world, _) = WorldProxyGlobalFreezeTests.BuildRealMap389World();
        var hero = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, hero);

        var proxy = new WorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = WorldProxyGlobalFreezeTests.FindProjectRoot();
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        return proxy;
    }

    private static bool HudScriptOpenRequestIsRaised()
        => (GameState.Instance.GetFlag(HudDirector.ScriptOpenRequestFlag) & HudDirector.ScriptOpenRequestMask) != 0;

    // -----------------------------------------------------------------------------------------------
    // Comportement d'origine : stats de nouvelle partie, et la jauge n'apparaît que sur demande de script.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_AtNewGameEntry_KeepsNewGameDefaultsAndNoHudRequest()
    {
        InitializeNewGameEntryOnMap389();

        var stats = GameState.Instance.PlayerStats;
        Assert.Equal(10, stats.Hp);
        Assert.Equal(10, stats.HpMax);
        Assert.Equal(0, stats.Mp);
        Assert.Equal(0, stats.MpMax);
        Assert.Equal(0, stats.Money);

        Assert.False(HudScriptOpenRequestIsRaised());
    }

    /// <summary>E19.r R1: a New Game on map 389 has no latch (G1662), so the map entry arms nothing - the jauge stays
    /// hidden until the wake-up scene on map 163 sets it (0x05 at @208) and the next warp re-enters the block.</summary>
    [Fact]
    public void MapEntry_NewGameOn389_LeavesTheHudIdle()
    {
        InitializeNewGameEntryOnMap389();

        Assert.Equal(HudDirector.HudPhase.Idle, HudDirector.Instance.Phase);
    }

    /// <summary>E19.r R1 (0x8002c3d0): the map-entry block calls <c>InitializeHudPositionBeforeHide</c>; with the latch
    /// set in the session by an earlier map, the jauge starts to slide in on this map's entry.</summary>
    [Fact]
    public void MapEntry_WithTheLatchSetInTheSession_ArmsTheHudAppearance()
    {
        GameState.Instance.AddFlag(HudDirector.PersistentLatchFlag, HudDirector.PersistentLatchMask);

        InitializeNewGameEntryOnMap389();

        Assert.Equal(HudDirector.HudPhase.Opening, HudDirector.Instance.Phase);
    }

    // -----------------------------------------------------------------------------------------------
    // E13.c S3 (docs/plan-e13c-icones-hud.md): the same New Game entry runs the game's own New Game
    // inventory, latched once per session. Whatever item tables the proxy resolved, the unconditional
    // SetPlayerWeaponId(1) puts the sword's slot in place.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_AtNewGameEntry_RunsTheNewGameInventory()
    {
        InitializeNewGameEntryOnMap389();

        Assert.True(GameState.Instance.NewGameInventoryInitialized);
        Assert.Equal(1, GameState.Instance.PlayerStats.WeaponId);
    }

    [Fact]
    public void AdoptPlayerPawn_DoesNotRerunTheNewGameInventory_AtASecondNoArrivalMapEntry()
    {
        InitializeNewGameEntryOnMap389();

        // Were the inventory to run again, it would zero every counter and put the slot back to 1.
        GameState.Instance.PlayerStats.WeaponId = 3;
        GameState.Instance.NumberOfItems[4 * 2 + 1] = 1;

        InitializeNewGameEntryOnMap389();

        Assert.Equal(3, GameState.Instance.PlayerStats.WeaponId);
        Assert.Equal(1, GameState.Instance.NumberOfItems[4 * 2 + 1]);
    }
}
