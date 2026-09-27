#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Alundra.Scripts;
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

namespace Alundra.Tests;

/// <summary>
/// The New Game entry in <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> (no pending warp arrival): the
/// original's New Game stats and inventory, and a HUD that stays hidden until a map script asks for it
/// (docs/plan-e13-hud.md §1.6). This class used to test the <c>ALUNDRA_HUD_DEBUG</c> recipe too (E13 C5.a,
/// D-E13-10), removed at the author's request on 2026-09-27; the tests of the game's own behaviour stay.
///
/// Drives the real <see cref="AlundraWorldProxy.InitializeWithWorld"/> through its full installation
/// block against map 389 (the New Game map) - same real-map/hand-built-pawn montage as
/// <see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/> (reused directly, internal in
/// the same assembly) and <see cref="AlundraWarpArrivalTests"/>'s own hero-pawn/player-controller
/// fixtures (re-built here, since those are private to that class).
///
/// <para>F5 (docs/plan-bgm-demarrage-binaire.md): the real map 389 install below writes the
/// SESSION-scoped <see cref="AlundraMusicPlayer.Instance"/> (map entry arms its reset flag, B7) - so
/// this class shares <see cref="AlundraMusicPlayerSingletonCollection"/> with every other class touching
/// that same shared instance, and resets <see cref="AlundraBgmFadeDirector.Instance"/> too.</para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraNewGameEntryTests : IDisposable
{
    public AlundraNewGameEntryTests()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests(); // F5: joins the music-player singleton collection.
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------------
    // Fixture plumbing - hero pawn + real AlundraPlayerController, same shape as
    // AlundraWarpArrivalTests.BuildHeroPawnEntity/RegisterPlayerController (private there, rebuilt here).
    // -----------------------------------------------------------------------------------------------

    private static void SetProperty<TTarget, TValue>(TTarget target, string propertyName, TValue value)
    {
        var property = typeof(TTarget).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(target, value);
    }

    /// <summary>Hand-built hero pawn - root <see cref="TransformComponent"/> plus sibling
    /// <see cref="CollisionComponent"/>, same fixture shape as
    /// <see cref="AlundraWarpArrivalTests.BuildHeroPawnEntity"/> - AdoptPlayerPawn only needs a box
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
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();
        SetProperty(entity, nameof(Entity.World), world); // ClampToGround reads Owner.World.CollisionField.

        Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        return entity;
    }

    /// <summary>Possesses <paramref name="pawn"/> with a real <see cref="AlundraPlayerController"/> and
    /// registers it into <paramref name="world"/>'s own private <c>World.PlayerControllers</c> backing
    /// list - same reflection precedent as <see cref="AlundraWarpArrivalTests.RegisterPlayerController"/>
    /// (private there).</summary>
    private static void RegisterPlayerController(World world, Entity pawn)
    {
        var controller = new AlundraPlayerController();
        controller.Possess(pawn);

        var field = typeof(World).GetField("_playerControllers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var list = (List<PlayerController>)field!.GetValue(world)!;
        list.Add(controller);
    }

    /// <summary>Builds map 389 (the New Game map) with a real player
    /// controller adopted, then runs <see cref="AlundraWorldProxy.InitializeWithWorld"/> end to end -
    /// <c>EngineEnvironment.ProjectPath</c> is set/restored around the call exactly like
    /// <see cref="AlundraWarpArrivalTests"/>'s own acceptance tests, since map events/dialogue strings load
    /// from it.</summary>
    private static AlundraWorldProxy InitializeNewGameEntryOnMap389()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var hero = BuildHeroPawnEntity(world);
        RegisterPlayerController(world, hero);

        var proxy = new AlundraWorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
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
        => (AlundraGameState.Instance.GetFlag(AlundraHudDirector.ScriptOpenRequestFlag) & AlundraHudDirector.ScriptOpenRequestMask) != 0;

    // -----------------------------------------------------------------------------------------------
    // Comportement d'origine : stats de nouvelle partie, et la jauge n'apparaît que sur demande de script.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_AtNewGameEntry_KeepsNewGameDefaultsAndNoHudRequest()
    {
        InitializeNewGameEntryOnMap389();

        var stats = AlundraGameState.Instance.PlayerStats;
        Assert.Equal(10, stats.Hp);
        Assert.Equal(10, stats.HpMax);
        Assert.Equal(0, stats.Mp);
        Assert.Equal(0, stats.MpMax);
        Assert.Equal(0, stats.Money);

        Assert.False(HudScriptOpenRequestIsRaised());
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

        Assert.True(AlundraGameState.Instance.NewGameInventoryInitialized);
        Assert.Equal(1, AlundraGameState.Instance.PlayerStats.WeaponId);
    }

    [Fact]
    public void AdoptPlayerPawn_DoesNotRerunTheNewGameInventory_AtASecondNoArrivalMapEntry()
    {
        InitializeNewGameEntryOnMap389();

        // Were the inventory to run again, it would zero every counter and put the slot back to 1.
        AlundraGameState.Instance.PlayerStats.WeaponId = 3;
        AlundraGameState.Instance.NumberOfItems[4 * 2 + 1] = 1;

        InitializeNewGameEntryOnMap389();

        Assert.Equal(3, AlundraGameState.Instance.PlayerStats.WeaponId);
        Assert.Equal(1, AlundraGameState.Instance.NumberOfItems[4 * 2 + 1]);
    }
}
