#nullable enable
using System;
using System.Collections.Generic;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T5 (docs/plan-e20-heros.md section 1.24.1, the mutants of the DLL code): two things the world tests of T2 did not pin and the real mutations of T5 showed. (1) The follow of an object put in the hands (W3, the carried branch of
/// <c>MoveEntity</c>, <c>0x80037E34</c>): the forces of the object go to 0, its contact entity is cleared and its floor and terrain heights are the carrier's, even when the object was moving, touching something and standing on other heights when it was lifted.
/// (2) The wiring of the two lift gestures (W4, <c>0x800322F0</c> and <c>0x800322B4</c>): the gesture ends where the rules say (the ground after a lift in the air, the loss of the object during a lift on the ground), each next to its neighbour that keeps the gesture.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CarryFollowAndGestureTests : IDisposable
{
    private const int Px = CarryRig.Px;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public CarryFollowAndGestureTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> TicksPerFrame => new() { 1, 2, 3 };

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void F1_TheFollow_ZeroesTheForces_ClearsTheContactEntity_AndTakesTheHeightsOfTheCarrier_WhateverTheObjectWasDoingWhenLifted(int ticksPerFrame)
    {
        var rig = CarryRig.Build(ticksPerFrame);
        var crate = rig.AddCrate(130, 100);
        var wall = rig.AddObject("wall", 200, 100, 16, 16, 16);
        rig.Run(3);
        var hero = rig.Hero;
        var lift = rig.Tick + 1;

        // The object is lifted while it moves, rests against something and stands on heights of its own.
        var crateFloor = 77 * Px;
        var crateTerrain = 55 * Px;
        rig.AfterMovePlayer = tick =>
        {
            if (tick != lift)
            {
                return;
            }

            PlayerManager.PutInHands(hero, crate);
            crate.ForceX = 3 * Px;
            crate.ForceY = -2 * Px;
            crate.ForceZ = Px;
            crate.TargetForceX = 4 * Px;
            crate.TargetForceY = 5 * Px;
            crate.FinalForceX = 6 * Px;
            crate.FinalForceY = 7 * Px;
            crate.FinalForceZ = 8 * Px;
            crate.ForceAdjusted = 0;
            crate.CollidedWithEntityZ = 0;
            crate.IsOnGround = 1;
            crate.XCollisionEntity = (EntityScriptProxy?)wall;
            crate.FloorHeight = crateFloor;
            crate.TerrainHeight = crateTerrain;
        };

        Assert.NotEqual(crateFloor, hero.FloorHeight);
        Assert.NotEqual(crateTerrain, hero.TerrainHeight);
        var failures = new List<string>();
        rig.Run(6, tick =>
        {
            if ((crate.ForceX, crate.ForceY, crate.ForceZ, crate.TargetForceX, crate.TargetForceY, crate.FinalForceX, crate.FinalForceY, crate.FinalForceZ) != (0, 0, 0, 0, 0, 0, 0, 0))
            {
                failures.Add($"tick {tick}: forces {crate.ForceX} {crate.ForceY} {crate.ForceZ} / {crate.TargetForceX} {crate.TargetForceY} / {crate.FinalForceX} {crate.FinalForceY} {crate.FinalForceZ}");
            }

            if (crate.XCollisionEntity != null)
            {
                failures.Add($"tick {tick}: the contact entity is still {crate.XCollisionEntity.Name}");
            }

            if ((crate.ForceAdjusted, crate.CollidedWithEntityZ, crate.IsOnGround) != (1, 1, 0))
            {
                failures.Add($"tick {tick}: adjusted {crate.ForceAdjusted}, collidedZ {crate.CollidedWithEntityZ}, ground {crate.IsOnGround}");
            }

            if ((crate.FloorHeight, crate.TerrainHeight) != (hero.FloorHeight, hero.TerrainHeight))
            {
                failures.Add($"tick {tick}: heights {crate.FloorHeight}/{crate.TerrainHeight}, the hero's {hero.FloorHeight}/{hero.TerrainHeight}");
            }
        });

        Assert.True(failures.Count == 0, string.Join("\n", failures.GetRange(0, Math.Min(6, failures.Count))));
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void F2_TheLiftGestureOnTheGround_PassesByTheFallStateWhenTheObjectIsLost_AndStaysWhileItIsHeld(int ticksPerFrame)
    {
        var held = LiftOnTheGround(ticksPerFrame, loseTheObject: false, out var heldRig);
        Assert.NotNull(heldRig.Hero.CarriedEntity);
        Assert.All(held, state => Assert.Equal(CarryRules.LiftGroundState, state));

        var lost = LiftOnTheGround(ticksPerFrame, loseTheObject: true, out var lostRig);
        Assert.Null(lostRig.Hero.CarriedEntity);
        Assert.Contains(CarryRules.FallState, lost);
    }

    /// <summary>The states of the hero tick by tick during the 3 ticks after 10 ticks of the lift gesture on the ground, with the object lost at the start of them or kept.</summary>
    private static List<uint> LiftOnTheGround(int ticksPerFrame, bool loseTheObject, out CarryRig rig)
    {
        rig = CarryRig.Build(ticksPerFrame);
        rig.AddCrate(130, 100);
        rig.Run(3);
        rig.LiftNow();
        rig.Run(10);
        if (loseTheObject)
        {
            rig.Hero.CarriedEntity = null; // what the destruction of the object does (W5); the lift gesture then sees nothing in the hands.
        }

        var states = new List<uint>();
        var hero = rig.Hero;
        rig.Run(3, _ => states.Add(hero.TargetAnimationId));
        return states;
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void F3_TheLiftGestureInTheAir_StaysWhileTheHeroFalls_AndBecomesTheStandOnTheGround(int ticksPerFrame)
    {
        var rig = CarryRig.Build(ticksPerFrame, heroZ: 20f); // the hero starts 20 px up and falls.
        rig.AddCrate(130, 100, 20);
        rig.Run(1);
        rig.LiftNow();
        var hero = rig.Hero;
        Assert.Equal(CarryRules.LiftAirState, hero.TargetAnimationId);
        var inTheAir = new List<uint>();
        rig.Run(40, _ =>
        {
            if (hero.IsOnGround == 0)
            {
                inTheAir.Add(hero.TargetAnimationId);
            }
        });

        Assert.True(inTheAir.Count >= 4, $"the hero fell for {inTheAir.Count} ticks");
        Assert.All(inTheAir, state => Assert.Equal(CarryRules.LiftAirState, state));
        Assert.Equal(CarryRules.CarryStillState, hero.TargetAnimationId);
    }
}
