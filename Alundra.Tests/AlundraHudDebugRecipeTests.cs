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
/// E13 C5.a (docs/plan-e13-hud.md, D-E13-10): the <c>ALUNDRA_HUD_DEBUG</c> recipe in
/// <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> - a New Game entry (no pending warp arrival) loads
/// <see cref="AlundraPlayerManager.LoadDebugStats"/> and raises the same "please appear" request a map
/// script's own opcode 0x05 would (<see cref="AlundraHudDirector.ScriptOpenRequestFlag"/>/
/// <see cref="AlundraHudDirector.ScriptOpenRequestMask"/>), gated by
/// <see cref="AlundraWorldProxy.DebugHudRecipeEnabled"/> and never replayed twice in the same session
/// (<see cref="AlundraGameState.DebugHudRecipeApplied"/>).
///
/// Drives the real <see cref="AlundraWorldProxy.InitializeWithWorld"/> through its full installation
/// block against map 389 (the recipe's own target map) - same real-map/hand-built-pawn montage as
/// <see cref="AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World"/> (reused directly, internal in
/// the same assembly) and <see cref="AlundraWarpArrivalTests"/>'s own hero-pawn/player-controller
/// fixtures (re-built here, since those are private to that class).
///
/// The environment variable itself is NEVER written by these tests - only
/// <see cref="AlundraWorldProxy.SetDebugHudRecipeEnabledOverrideForTests"/>, the seam that exists exactly
/// so a headless run never touches this process' real environment (see that seam's own doc).
/// </summary>
public sealed class AlundraHudDebugRecipeTests : IDisposable
{
    public AlundraHudDebugRecipeTests()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraWorldProxy.SetDebugHudRecipeEnabledOverrideForTests(null);
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
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

    /// <summary>Builds map 389 (this recipe's own target map, D-E13-6/D-E13-10) with a real player
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
    // Acceptance: variable active -> debug stats + HUD request, exactly D-E13-10's own two deliverables.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_WithRecipeEnabled_AtNewGameEntry_LoadsDebugStatsAndRaisesHudOpenRequest()
    {
        AlundraWorldProxy.SetDebugHudRecipeEnabledOverrideForTests(true);

        InitializeNewGameEntryOnMap389();

        var stats = AlundraGameState.Instance.PlayerStats;
        Assert.Equal(38, stats.Hp);
        Assert.Equal(45, stats.HpMax);
        Assert.Equal(2, stats.Mp);
        Assert.Equal(3, stats.MpMax);
        Assert.Equal(2163, stats.Money);

        Assert.True(HudScriptOpenRequestIsRaised());
        Assert.True(AlundraGameState.Instance.DebugHudRecipeApplied);
    }

    // -----------------------------------------------------------------------------------------------
    // Sans la variable: comportement d'origine, inchangé - la jauge n'apparaît que sur demande de script.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_WithRecipeNotSet_AtNewGameEntry_KeepsNewGameDefaultsAndNoHudRequest()
    {
        // Default is inactive - no override forced, same as an unset environment variable.
        InitializeNewGameEntryOnMap389();

        var stats = AlundraGameState.Instance.PlayerStats;
        Assert.Equal(10, stats.Hp);
        Assert.Equal(10, stats.HpMax);
        Assert.Equal(0, stats.Mp);
        Assert.Equal(0, stats.MpMax);
        Assert.Equal(0, stats.Money);

        Assert.False(HudScriptOpenRequestIsRaised());
        Assert.False(AlundraGameState.Instance.DebugHudRecipeApplied);
    }

    [Fact]
    public void AdoptPlayerPawn_WithRecipeExplicitlyDisabled_AtNewGameEntry_KeepsNewGameDefaultsAndNoHudRequest()
    {
        AlundraWorldProxy.SetDebugHudRecipeEnabledOverrideForTests(false);

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
    // "L'activation ne doit jouer QU'UNE FOIS, à la nouvelle partie, jamais à chaque carte" (D-E13-10) -
    // proven by forcing a SECOND no-pending-arrival entry in the same session (GameState.Instance is the
    // session carrier, D-T-3) and showing the recipe does not fire again, even though its own gate
    // ("no pending arrival") would otherwise be satisfied a second time too.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void AdoptPlayerPawn_WithRecipeEnabled_DoesNotReplayAtASecondNoArrivalMapEntry()
    {
        AlundraWorldProxy.SetDebugHudRecipeEnabledOverrideForTests(true);

        InitializeNewGameEntryOnMap389();
        Assert.True(AlundraGameState.Instance.DebugHudRecipeApplied);

        // Simulate the player having since changed HP away from the debug value, and the HUD having since
        // been closed by its own machine (clear the request bit the same way AlundraHudDirector consumes
        // it, GameState.SetFlag with the complemented mask, C1's own shape) - if the recipe replayed on
        // the entry below, both would be stomped back to the debug set/raised again.
        AlundraPlayerManager.SetPlayerHp(AlundraGameState.Instance, 5);
        AlundraGameState.Instance.SetFlag(AlundraHudDirector.ScriptOpenRequestFlag, ~AlundraHudDirector.ScriptOpenRequestMask);

        InitializeNewGameEntryOnMap389();

        Assert.Equal(5, AlundraGameState.Instance.PlayerStats.Hp);
        Assert.False(HudScriptOpenRequestIsRaised());
        Assert.True(AlundraGameState.Instance.DebugHudRecipeApplied);
    }
}
