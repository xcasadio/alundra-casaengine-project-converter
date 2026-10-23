#nullable enable
using System;
using Xunit;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Entities;

/// <summary>
/// E19.d2c2 D5b T1 (docs/plan-e19-opcodes.md §1.2h.3.2b, D-E19-117, "tous les passagers"): the witnesses of the passengers other than the hero carried by a platform that moves
/// in Z (and in XY), written BEFORE the code, with the values of the binary replayed (<c>docs/plan-e19-d5b-annexe/exec-2026-10-22/vec_d5b_out.txt</c>, generator
/// <c>annex-delta/work/vec_d5b.py</c>): the crate, the NPC, the piles (hero on crate on platform), the passenger with an impulse. Every value is relative to the top of the carrier
/// (<c>rel</c>): the binary moves the passenger in the very tick of its carrier (rel 0 at every tick), where the DLL's gravity pulls it through a platform that rises. The montage is
/// the one of the discovery's probe P2 (<see cref="JumpHeroRig"/> with a far hero, the platform 48 x 32 x 8 at 16 px, the passenger on it at 24 px, both rooted at (100, 92)); the hero
/// plays its tick first, then the entities in the order they joined the world (the passenger after the platform, or before it). The schedule is the one of the generator: the force of the
/// carrier is put before the tick 3 and removed before the tick 9 (a push of 6 px), 14 ticks.
/// Path A is the entities with a controller (<see cref="EntityScriptProxy.EvaluateEntitySupport"/>), path B the entities marked <see cref="EntityScriptProxy.HasLogicalZ"/>
/// (<see cref="Alundra.Physics.ScriptedMotion.RunLogicalZPreXyStep"/>), path C the ones with no controller (<see cref="EntityScriptProxy.StepVerticalWithoutController"/> of a prefab).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PassengerPlatformFollowTests : IDisposable
{
    private const int StartTick = 3;
    private const int StopTick = 9;
    private const int Ticks = 14;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public PassengerPlatformFollowTests()
    {
        GameState.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
    }

    private static EntityScriptProxy AddObject(World world, ContactHost host, string name, int x, int y, int sizeX, int sizeY, int sizeZ, int offsetX, int offsetY, int z, bool withController = true)
    {
        var obj = ContactWorld.AddEntity(world, host, name, x, y, z, offsetX, offsetY, 0, sizeX, sizeY, sizeZ, withController: withController);
        obj.LogicContextEntity = obj.OwnerEntity!;
        return obj;
    }

    /// <summary>The top of the box of <paramref name="p"/> in the DLL's convention.</summary>
    private static int Top(EntityScriptProxy p) => p.PosZ + p.ModZ + p.Depth + 1;

    private static int Rel(EntityScriptProxy rider, EntityScriptProxy carrier) => rider.PosZ + rider.ModZ - Top(carrier);

    /// <summary>The gravity words of the map (the export's, as <c>CollidedWithEntityZTests</c> posts them) and the bit.</summary>
    private static void GiveGravity(EntityScriptProxy e)
    {
        e.Flags |= EntityFlags.Gravity;
        e.MapGravityRaw = 128;
        e.MapZViscosityRaw = 4096;
        e.ResyncControllerFromFlags();
    }

    private static void Settle(JumpHeroRig rig)
    {
        for (var i = 0; i < 4; i++)
        {
            rig.Step();
        }
    }

    /// <summary>The platform 48 x 32 x 8 (flags of the lift, no gravity) at 16 px, and a gravity passenger of the given box at 24 px, in the given order of the world; the hero far away.</summary>
    private static (JumpHeroRig Rig, EntityScriptProxy Platform, EntityScriptProxy Passenger) Montage(bool passengerAfterPlatform, int sizeX = 21, int sizeY = 15, int sizeZ = 16, bool gravityPassenger = true)
    {
        var rig = JumpHeroRig.Build(x: 40.25f, y: 40.5f, freePad: true);
        EntityScriptProxy platform, passenger;
        if (passengerAfterPlatform)
        {
            platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, -24, -16, 16);
            passenger = AddObject(rig.World, rig.Host, "Passenger", 100, 92, sizeX, sizeY, sizeZ, -10, -7, 24);
        }
        else
        {
            passenger = AddObject(rig.World, rig.Host, "Passenger", 100, 92, sizeX, sizeY, sizeZ, -10, -7, 24);
            platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, -24, -16, 16);
        }

        if (gravityPassenger)
        {
            GiveGravity(passenger);
        }

        Settle(rig);
        Assert.Equal(16 << 16, platform.PosZ);
        Assert.Equal(24 << 16, passenger.PosZ);
        Assert.True(ReferenceEquals(platform.LogicContextEntity, passenger.RidingEntity) || !gravityPassenger, "settled: riding");
        return (rig, platform, passenger);
    }

    /// <summary>The state of a passenger carried by the platform at the end of a tick, as the binary's vector reads it (rel 0, carried, no contact, on the ground). <paramref name="withContact"/> false leaves the contact out (the ticks at rest, RestA's).</summary>
    private static void AssertCarried(string label, EntityScriptProxy passenger, EntityScriptProxy platform, bool withContact = true)
    {
        Assert.True(Rel(passenger, platform) == 0, $"{label}: foot minus top {Rel(passenger, platform)}");
        Assert.True(ReferenceEquals(platform.LogicContextEntity, passenger.RidingEntity), $"{label}: riding");
        Assert.True(passenger.IsOnGround == 1, $"{label}: onGround {passenger.IsOnGround}");
        if (withContact)
        {
            Assert.True(passenger.CollidedWithEntityZ == 0, $"{label}: collided {passenger.CollidedWithEntityZ}");
        }
    }

    /// <summary>Runs the schedule of the generator (force before the tick 3, removed before the tick 9, 14 ticks) and checks the carrier's own push and the passenger at every tick.</summary>
    private static void RunSchedule(JumpHeroRig rig, EntityScriptProxy platform, EntityScriptProxy passenger, int force)
    {
        var start = platform.PosZ;
        var sign = Math.Sign(force);
        for (var tick = 0; tick < Ticks; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = force;
            }

            if (tick == StopTick)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var pushed = tick < StartTick ? 0 : Math.Min(tick - StartTick + 1, StopTick - StartTick);
            Assert.True(platform.PosZ - start == sign * pushed * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, passenger, platform, withContact: tick >= StartTick);
            if (tick >= StartTick)
            {
                // The force of the tick the passenger copies from its carrier (vec_d5b_out.txt: ffz). The ticks 0 to 2 (the passenger at rest on a carrier with no gravity) are RestA's.
                Assert.True(passenger.TickForceZ == (tick < StopTick ? force : 0), $"{label}: tick force {passenger.TickForceZ}");
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // CRATE-A / NPC-A - a passenger with a controller on a platform that rises or descends, before or after it in the order of the cases.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true, 65536)]
    [InlineData(false, 65536)]
    [InlineData(true, -65536)]
    [InlineData(false, -65536)]
    public void CrateA_AGravityCrateOnAPlatformThatRisesOrDescendsByOnePixelPerTick_FollowsItInTheSameTick_WhateverTheOrderOfTheCases(bool crateAfterPlatform, int force)
    {
        var (rig, platform, crate) = Montage(crateAfterPlatform);
        RunSchedule(rig, platform, crate, force);
    }

    [Fact]
    public void NpcA_AnNpcOf20By14By32WithGravityOnARisingPlatform_FollowsItInTheSameTick()
    {
        var (rig, platform, npc) = Montage(passengerAfterPlatform: true, sizeX: 20, sizeY: 14, sizeZ: 32);
        RunSchedule(rig, platform, npc, 65536);
    }

    [Fact]
    public void RestA_ACrateAtRestOnAPlatformWithNoGravity_HasNoContactAndIsOnTheGround_AsTheBinaryCopiesTheZeroForceOfTheCarrier()
    {
        // vec_d5b_out.txt, ticks 0 to 2: coll 0, gnd 1, ffz 0 (the crate takes the force of its carrier, which has no gravity: 0, so it does not land at every tick).
        var (rig, platform, crate) = Montage(passengerAfterPlatform: true);
        for (var tick = 0; tick < 6; tick++)
        {
            rig.Step();
            var label = $"tick {tick}";
            AssertCarried(label, crate, platform);
            Assert.True(crate.TickForceZ == 0, $"{label}: tick force {crate.TickForceZ}");
        }
    }

    [Fact]
    public void ImpulseA_APassengerThatTakesAnImpulse_DoesNotCopyTheForceOfItsCarrier_ItFliesByItsOwnImpulse()
    {
        // The negative witness (green before the code, since the passenger ignores its carrier today; it must stay so): the passenger switches to an animation that carries the impulse
        // IZF 1280 at the tick 3, when the carrier starts to rise by 1 px per tick. In the binary an entity with an impulse (+0xF8 != 0) does not copy the force of its carrier
        // (vec_d5b.py, impulse=True: fz = ffz = 1280 << 8 = 327680, never the carrier's 65536); the force of the next ticks is its own (ForceZ - (Gravity << 8): 294912, 262144...).
        // Only the force is pinned: the DLL's root pull of a supported entity that takes off comes one tick late (EvaluateEntitySupport, wasSupportedEnteringThisTick), outside D5b.
        var (rig, platform, crate) = Montage(passengerAfterPlatform: false);
        crate.AnimSetsByAnim = new System.Collections.Generic.Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [JumpNpcRig.ImpulseAnimation] = new AnimSetEntry { Anim = JumpNpcRig.ImpulseAnimation, Speed = 0, IsZForceApplied = 1280 },
        };
        crate.CurrentAnimationId = 0;
        crate.TargetAnimationId = 0;
        var platformStart = platform.PosZ;
        for (var tick = 0; tick < StartTick + 5; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = 65536;
                crate.TargetAnimationId = JumpNpcRig.ImpulseAnimation;
            }

            rig.Step();
            var label = $"tick {tick}";
            Assert.True(platform.PosZ - platformStart == (tick < StartTick ? 0 : tick - StartTick + 1) * 65536, $"{label}: platform {platform.PosZ - platformStart}");
            if (tick >= StartTick)
            {
                // 327680 at the tick of the switch, then the decay by the gravity (128 << 8 = 32768 per tick): never the carrier's 65536.
                Assert.True(crate.TickForceZ == 327680 - (tick - StartTick) * 32768, $"{label}: tick force {crate.TickForceZ}");
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // PILE_Z-A / PILE_XY-A - the hero on the crate on the platform.
    // -----------------------------------------------------------------------------------------

    private static (JumpHeroRig Rig, EntityScriptProxy Platform, EntityScriptProxy Crate) PileMontage()
    {
        // As in UH-14, the objects are rooted at (100, 92) and their logical boxes offset to where the hero is (their physical boxes must not meet the hero's body, which blocks their move):
        // the platform x 132..180, y 84..116; the crate x 140..161, y 93..108; the hero (root 156.25, 100.5) has its box at x 146.25..167.25, y 93.5..108.5.
        var rig = JumpHeroRig.Build(x: 156.25f, y: 100.5f, freePad: true);
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, 32, -8, 16);
        var crate = AddObject(rig.World, rig.Host, "Crate", 100, 92, 21, 15, 16, 40, 1, 24);
        GiveGravity(crate);
        rig.Hero.PosZ = 40 << 16;
        rig.Hero.PushLogicalPositionToRoot();
        Settle(rig);
        Assert.Same(crate.LogicContextEntity, rig.Hero.RidingEntity);
        Assert.Same(platform.LogicContextEntity, crate.RidingEntity);
        return (rig, platform, crate);
    }

    [Fact]
    public void PileZA_TheHeroOnTheCrateOnTheRisingPlatform_EveryoneFollowsInTheSameTick_TheMiddleCarrierPassesOnTheForceItReceives()
    {
        var (rig, platform, crate) = PileMontage();
        var hero = rig.Hero;
        var start = platform.PosZ;
        for (var tick = 0; tick < Ticks; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = 65536;
            }

            if (tick == StopTick)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var pushed = tick < StartTick ? 0 : Math.Min(tick - StartTick + 1, StopTick - StartTick);
            Assert.True(platform.PosZ - start == pushed * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried($"{label} crate", crate, platform, withContact: tick >= StartTick);
            Assert.True(Rel(hero, crate) == 0, $"{label}: hero foot minus crate top {Rel(hero, crate)}");
            Assert.True(ReferenceEquals(crate.LogicContextEntity, hero.RidingEntity), $"{label}: hero riding");
            Assert.True(hero.IsOnGround == 1, $"{label}: hero onGround {hero.IsOnGround}");
            if (tick >= StartTick)
            {
                Assert.True(hero.CollidedWithEntityZ == 0, $"{label}: hero collided {hero.CollidedWithEntityZ}");
            }
        }
    }

    [Fact]
    public void PileXYA_ThePlatformMovingEastAndUp_CarriesTheCrateAndTheHeroByItsPlannedForceOnce_NoDoubleForce_NoOneLeftBehind()
    {
        // D-D5B-7 (D-E19-117): the binary applies the force of the platform twice to the crate (vec_d5b_out.txt, PILE_XY: crate ffx 131072, dx growing by 65536 per tick) and leaves the hero behind
        // (ffx 0); the DLL takes the corrected form: each passenger adds ONCE the planned AdjustedForce of every carrier of its chain, so both keep their offset to the platform.
        var (rig, platform, crate) = PileMontage();
        var hero = rig.Hero;
        var crateOffset = crate.PosX - platform.PosX;
        var heroOffset = hero.PosX - platform.PosX;
        var start = platform.PosZ;
        var startX = platform.PosX;
        for (var tick = 0; tick < Ticks; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = 65536;
                platform.TargetDirection = platform.CurrentDirection;
                platform.TargetForceX = 65536;
                platform.ForceX = 65536;
                platform.ForceStepX = 0;
            }

            if (tick == StopTick)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var pushed = tick < StartTick ? 0 : Math.Min(tick - StartTick + 1, StopTick - StartTick);
            Assert.True(platform.PosZ - start == pushed * 65536, $"{label}: platform {platform.PosZ - start}");
            Assert.True(platform.PosX - startX == (tick < StartTick ? 0 : tick - StartTick + 1) * 65536, $"{label}: platform dx {platform.PosX - startX}");
            Assert.True(crate.PosX - platform.PosX == crateOffset, $"{label}: crate dx to the platform {crate.PosX - platform.PosX - crateOffset}");
            Assert.True(hero.PosX - platform.PosX == heroOffset, $"{label}: hero dx to the platform {hero.PosX - platform.PosX - heroOffset}");
            AssertCarried($"{label} crate", crate, platform, withContact: tick >= StartTick);
            Assert.True(Rel(hero, crate) == 0, $"{label}: hero foot minus crate top {Rel(hero, crate)}");
            Assert.True(ReferenceEquals(crate.LogicContextEntity, hero.RidingEntity), $"{label}: hero riding");
        }
    }

    // -----------------------------------------------------------------------------------------
    // MARKED-B / PILE_Z-B - the entities marked HasLogicalZ (the dropped and thrown objects), path B.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(65536)]
    [InlineData(-65536)]
    public void MarkedB_AMarkedBarrelWithGravityOnAPlatformThatRisesOrDescends_FollowsItInTheSameTick(int force)
    {
        var (rig, platform, barrel) = Montage(passengerAfterPlatform: true);
        barrel.HasLogicalZ = true;
        RunSchedule(rig, platform, barrel, force);
    }

    [Fact]
    public void PileZB_AMarkedObjectOnACrateOnTheRisingPlatform_BothFollowInTheSameTick()
    {
        var rig = JumpHeroRig.Build(x: 40.25f, y: 40.5f, freePad: true);
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, -24, -16, 16);
        var crate = AddObject(rig.World, rig.Host, "Crate", 100, 92, 21, 15, 16, -10, -7, 24);
        var marked = AddObject(rig.World, rig.Host, "Marked", 100, 92, 21, 15, 16, -10, -7, 40);
        GiveGravity(crate);
        GiveGravity(marked);
        marked.HasLogicalZ = true;
        Settle(rig);
        Assert.Same(crate.LogicContextEntity, marked.RidingEntity);
        var start = platform.PosZ;
        for (var tick = 0; tick < Ticks; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = 65536;
            }

            if (tick == StopTick)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var label = $"tick {tick}";
            var pushed = tick < StartTick ? 0 : Math.Min(tick - StartTick + 1, StopTick - StartTick);
            Assert.True(platform.PosZ - start == pushed * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried($"{label} crate", crate, platform, withContact: tick >= StartTick);
            AssertCarried($"{label} marked", marked, crate, withContact: tick >= StartTick);
        }
    }

    // -----------------------------------------------------------------------------------------
    // PREFAB-C - the prefabs with no controller (a point box): "I33 broken armour" (maps 11, 260, 398) and "Sara (dream)" (maps 41, 42, 47) are the two with the
    // gravity and the collidable bits (docs/plan-e19-h1b2-annexe/corpus-notes.md section 2); the montage is the one of ControllerlessVerticalStepTests (a world with a collision field).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PrefabC_ABrokenArmourI33WithNoController_OnARisingPlatform_FollowsItInTheSameTick()
    {
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost();
        var platform = AddObject(world, host, "Platform", 100, 92, 48, 32, 8, -24, -16, 16);
        var armour = AddObject(world, host, "Broken armour I33", 100, 92, 0, 0, 0, 0, 0, 24, withController: false);
        armour.MapGravityRaw = 128;
        armour.MapZViscosityRaw = 4096;
        armour.Flags |= EntityFlags.Gravity;
        for (var i = 0; i < 4; i++)
        {
            ContactWorld.Integrate(world, host);
        }

        Assert.Equal(24 << 16, armour.PosZ);
        Assert.Null(armour.Controller);
        Assert.Same(platform.LogicContextEntity, armour.RidingEntity);
        var start = platform.PosZ;
        for (var tick = 0; tick < Ticks; tick++)
        {
            if (tick == StartTick)
            {
                platform.ForceZ = 65536;
            }

            if (tick == StopTick)
            {
                platform.ForceZ = 0;
            }

            ContactWorld.Integrate(world, host);
            var label = $"tick {tick}";
            var pushed = tick < StartTick ? 0 : Math.Min(tick - StartTick + 1, StopTick - StartTick);
            Assert.True(platform.PosZ - start == pushed * 65536, $"{label}: platform {platform.PosZ - start}");
            AssertCarried(label, armour, platform, withContact: tick >= StartTick);
        }
    }
}
