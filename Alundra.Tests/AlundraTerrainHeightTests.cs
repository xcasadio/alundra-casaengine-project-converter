#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Covers E16.a T3 (docs/plan-e16-etat-partie.md, D-E16-29/D-E16-30):
/// <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>'s own <see cref="AlundraEntityScriptProxy.TerrainHeight"/>
/// write, now UNCONDITIONAL on <c>Controller</c>/<c>immediateAtSpawn</c> (T3.1's own finding: the original
/// writes it for EVERY entity, at spawn and every tick, regardless of controller). Uses a small synthetic
/// <see cref="ICollisionField"/> instead of the real map 389 fixture - this write is a pure
/// "unconditional now" plumbing change, not a terrain-sampling one (<see cref="AlundraEntityScriptProxy.ComputeTerrainHeight"/>
/// itself is untouched and already covered against real map data by <see cref="AlundraFloorHeightTests"/>/
/// <see cref="AlundraGroundSlopeTests"/>), so a fake flat/positional field is enough to prove WHEN it is
/// written, not what value real terrain would give.
///
/// The player's own equivalent write (<see cref="AlundraWorldProxy"/>'s private <c>AdoptPlayerPawn</c>, at
/// spawn, and <see cref="AlundraEntityScriptProxy.UpdateFloorHeight"/>, per tick) is covered instead by
/// <see cref="AlundraFloorHeightTests"/>'s own production-call-site test (per-tick) - <c>AdoptPlayerPawn</c>
/// itself is not headless-reachable in this test suite (needs a live <c>AlundraPlayerController</c> pawn -
/// see <see cref="AlundraWorldProxyUpdateCharacterizationTests"/>'s own documented limitation), so its
/// spawn-time write is verified by code inspection only, same as every other of its fields.
///
/// Platform inheritance (<c>PlatformEntity</c>, MoveEntity 0x80037E88-0x80037E90) has no test here:
/// <see cref="AlundraEntityScriptProxy.PlatformEntity"/> is never assigned anywhere in this runtime (only
/// ever compared against null - see <see cref="EntitySupport"/>'s own class doc, "out of scope, never
/// assigned by this runtime") - there is no live platform relation to inherit TerrainHeight from, so T3.2
/// leaves this sub-case unimplemented (reported, not invented).
/// </summary>
public class AlundraTerrainHeightTests
{
    /// <summary>Returns the SAME ground height for every sample - enough to prove TerrainHeight tracks
    /// <see cref="AlundraEntityScriptProxy.ComputeTerrainHeight"/>'s own return, regardless of WHERE the
    /// entity happens to be.</summary>
    private sealed class FlatCollisionField : ICollisionField
    {
        public float GroundHeight { get; set; }

        public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
        {
            sample = new GroundSample(GroundHeight, Vector3.UnitZ, true, "flat");
            return true;
        }
    }

    /// <summary>Returns the sampled X coordinate itself as the ground height - used by the "per-tick
    /// tracks the FINAL position" test to prove the write happens at THIS call's own position, not a
    /// stale one.</summary>
    private sealed class PositionalCollisionField : ICollisionField
    {
        public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
        {
            sample = new GroundSample(worldPosition.X, Vector3.UnitZ, true, "positional");
            return true;
        }
    }

    /// <summary>Bare entity + <see cref="AlundraEntityScriptProxy"/>, attached to <paramref name="world"/>
    /// via <c>InitializeWithWorld</c> directly (not the queued <c>World.AddEntity</c>/<c>World.Update</c>
    /// flush - unneeded here since nothing under test goes through <c>CharacterMotionSystem</c>) so
    /// <c>Owner.World.CollisionField</c> resolves for <see cref="AlundraEntityScriptProxy.ComputeTerrainHeight"/>
    /// with no <c>PhysicsWorld</c>/content-pipeline setup at all.</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) BuildEntity(World world, bool withController)
    {
        var entity = new Entity { Name = "TerrainHeightTestEntity", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        if (withController)
        {
            entity.AddComponent(new CharacterControllerComponent());
        }

        entity.Initialize();
        entity.InitializeWithWorld(world);

        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        if (withController)
        {
            proxy.Controller = entity.GetComponent<CharacterControllerComponent>();
            Assert.NotNull(proxy.Controller);
        }

        return (entity, proxy);
    }

    // -----------------------------------------------------------------------------------------
    // Spawn (immediateAtSpawn: true) - AlundraWorldProxy's own map-load/dynamic-spawn call sites.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EvaluateEntitySupport_AtSpawn_ControllerLessEntity_SetsTerrainHeight()
    {
        var world = new World { CollisionField = new FlatCollisionField { GroundHeight = 10f } };
        var (_, proxy) = BuildEntity(world, withController: false);
        Assert.Null(proxy.Controller);

        proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>(), immediateAtSpawn: true);

        Assert.Equal((int)Math.Round(10.0 * 65536.0), proxy.TerrainHeight);
        Assert.Equal(proxy.ComputeTerrainHeight(), proxy.TerrainHeight);
    }

    [Fact]
    public void EvaluateEntitySupport_AtSpawn_ControllerDrivenEntity_SetsTerrainHeight()
    {
        // D-E16-30: the FORMER `Controller != null && !immediateAtSpawn` gate excluded exactly this case
        // (a controller-driven entity's own spawn-time evaluation) from ever writing TerrainHeight - the
        // whole point of E16.a's fix.
        var world = new World { CollisionField = new FlatCollisionField { GroundHeight = 15f } };
        var (_, proxy) = BuildEntity(world, withController: true);

        proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>(), immediateAtSpawn: true);

        Assert.Equal((int)Math.Round(15.0 * 65536.0), proxy.TerrainHeight);
    }

    // -----------------------------------------------------------------------------------------
    // Per tick (immediateAtSpawn: false) - the NPC per-tick loop's own call site
    // (AlundraEntityScriptProxy.Update, ~:1001), which runs unconditionally on Controller for every
    // non-player entity.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void EvaluateEntitySupport_PerTick_ControllerLessEntity_TracksFinalPositionEachTick()
    {
        var world = new World { CollisionField = new PositionalCollisionField() };
        var (_, proxy) = BuildEntity(world, withController: false);
        proxy.Width = 0;
        proxy.Height = 0;

        proxy.PosX = 50 << 16;
        proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>());
        Assert.Equal((int)Math.Round(50.0 * 65536.0), proxy.TerrainHeight);

        // A later tick's own final position (moved between ticks, e.g. by AlundraScriptedMotion) is what
        // the NEXT EvaluateEntitySupport call samples - proving this is a live per-tick write, not a
        // spawn-only one.
        proxy.PosX = 80 << 16;
        proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>());
        Assert.Equal((int)Math.Round(80.0 * 65536.0), proxy.TerrainHeight);
    }

    [Fact]
    public void EvaluateEntitySupport_PerTick_ControllerDrivenEntity_SetsTerrainHeight()
    {
        var world = new World { CollisionField = new FlatCollisionField { GroundHeight = 20f } };
        var (_, proxy) = BuildEntity(world, withController: true);

        // Landed-and-resting pose (wasAlreadyLanded == true: PosZ already == terrainHeight - ModZ, ForceZ
        // == 0) - the "not found" tail's terrain-landing branch takes its NO-OP sub-path
        // (PushLogicalPositionToRoot skipped), so this exercises the real per-tick landing test without
        // needing a wired root transform.
        var terrainHeight16 = 20 << 16;
        proxy.PosZ = terrainHeight16; // ModZ defaults to 0 -> moddedPosZ == terrainHeight.
        proxy.ForceZ = 0;
        proxy.FinalForceZ = 0;

        proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>());

        Assert.Equal(terrainHeight16, proxy.TerrainHeight);
        Assert.Equal(1, proxy.IsOnGround);
    }
}
