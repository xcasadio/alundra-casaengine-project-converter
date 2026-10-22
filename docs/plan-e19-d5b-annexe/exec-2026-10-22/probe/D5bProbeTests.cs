#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>THROWAWAY PROBE (scratch copy only, never in the repository): measures what the DLL does today in the D5b scenarios.</summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class D5bProbeTests : IDisposable
{
    private static readonly string Out = Path.Combine(AppContext.BaseDirectory, "d5b-probe-out.txt");
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public D5bProbeTests()
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

    private static void Log(string line) => File.AppendAllText(Out, line + Environment.NewLine);

    private static EntityScriptProxy AddObject(World world, ContactHost host, string name, int x, int y, int sizeX, int sizeY, int sizeZ, int offsetX = 0, int offsetY = 0, int z = 0)
    {
        var obj = ContactWorld.AddEntity(world, host, name, x, y, z, offsetX, offsetY, 0, sizeX, sizeY, sizeZ);
        obj.LogicContextEntity = obj.OwnerEntity!;
        return obj;
    }

    private static string Rel(int posZ, int topRef) => ((posZ - topRef) / 65536.0).ToString("0.###");

    [Fact]
    public void P1_HeroOnAPlatformRisingOnePixelPerTick()
    {
        Log("== P1 hero on a platform (24x16x16 box, no gravity) driven by ForceZ = +65536 from tick 1");
        var rig = JumpHeroRig.Build(x: 156.25f, freePad: true);
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 24, 16, 16, offsetX: 44);
        var hero = rig.Hero;
        hero.PosZ = 1048576;
        hero.PushLogicalPositionToRoot();
        for (var settle = 0; settle < 4; settle++)
        {
            rig.Step();
        }

        Log($"settled: hero.PosZ={hero.PosZ} platform.PosZ={platform.PosZ} riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne}");
        platform.ForceZ = 65536;
        var top0 = platform.PosZ + platform.ModZ + platform.Depth + 1;
        for (var tick = 1; tick <= 14; tick++)
        {
            if (tick == 10)
            {
                platform.ForceZ = 0;
            }

            rig.Step();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            Log($"tick {tick,2}: platform.PosZ={platform.PosZ} (+{Rel(platform.PosZ, 0)}px) hero.PosZ={hero.PosZ} heroFootMinusPlatformTop={Rel(hero.PosZ + hero.ModZ, top)}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} collZ={hero.CollidedWithEntityZ} airborne={hero.HeroAirborne} heroForceZ={hero.ForceZ} heroFinalForceZ={hero.FinalForceZ} platTickForceZ={platform.TickForceZ}");
        }
    }

    [Fact]
    public void P2_CrateWithGravityOnAPlatformRisingOnePixelPerTick()
    {
        foreach (var crateAfterPlatform in new[] { true, false })
        {
            Log($"== P2 gravity crate (21x15x16) on a platform (48x32x8) ForceZ=+65536 from tick 1, crate {(crateAfterPlatform ? "AFTER" : "BEFORE")} the platform in the world order, no hero action");
            var rig = JumpHeroRig.Build(x: 40.25f, y: 40.5f, freePad: true);
            EntityScriptProxy platform, crate;
            if (crateAfterPlatform)
            {
                platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, offsetX: -24, offsetY: -16, z: 16);
                crate = AddObject(rig.World, rig.Host, "Crate", 100, 92, 21, 15, 16, offsetX: -10, offsetY: -7, z: 24);
            }
            else
            {
                crate = AddObject(rig.World, rig.Host, "Crate", 100, 92, 21, 15, 16, offsetX: -10, offsetY: -7, z: 24);
                platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 48, 32, 8, offsetX: -24, offsetY: -16, z: 16);
            }

            crate.Flags |= EntityFlags.Gravity;
            crate.MapGravityRaw = 128;
            crate.MapZViscosityRaw = 4096;
            crate.ResyncControllerFromFlags();
            for (var settle = 0; settle < 4; settle++)
            {
                rig.Step();
            }

            Log($"settled: platform.PosZ={platform.PosZ} crate.PosZ={crate.PosZ} crate.RidingEntity==platform:{ReferenceEquals(crate.RidingEntity, platform.LogicContextEntity)} crate.IsOnGround={crate.IsOnGround}");
            platform.ForceZ = 65536;
            for (var tick = 1; tick <= 12; tick++)
            {
                if (tick == 9)
                {
                    platform.ForceZ = 0;
                }

                rig.Step();
                var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
                Log($"tick {tick,2}: platform.PosZ={platform.PosZ} crate.PosZ={crate.PosZ} crateFootMinusPlatformTop={Rel(crate.PosZ + crate.ModZ, top)}px riding={ReferenceEquals(crate.RidingEntity, platform.LogicContextEntity)} onGround={crate.IsOnGround} collZ={crate.CollidedWithEntityZ} crateForceZ={crate.ForceZ} crateTickForceZ={crate.TickForceZ}");
            }
        }
    }

    [Fact]
    public void P3_HeroOnAPlatformMovingWestOnePixelPerTick()
    {
        Log("== P3 UH-14 montage: platform ForceX=-65536 (phase of the hero)");
        var rig = JumpHeroRig.Build(x: 156.25f, freePad: true);
        var platform = AddObject(rig.World, rig.Host, "Platform", 100, 92, 24, 16, 16, offsetX: 44);
        var hero = rig.Hero;
        hero.PosZ = 1048576;
        hero.PushLogicalPositionToRoot();
        for (var settle = 0; settle < 4; settle++)
        {
            rig.Step();
        }

        platform.TargetDirection = platform.CurrentDirection;
        platform.TargetForceX = -65536;
        platform.ForceX = -65536;
        platform.ForceStepX = 0;
        var heroStart = hero.PosX;
        var platformStart = platform.PosX;
        for (var tick = 1; tick <= 6; tick++)
        {
            rig.Step();
            Log($"tick {tick}: platformDX={(platform.PosX - platformStart) / 65536.0}px heroDX={(hero.PosX - heroStart) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} posZ={hero.PosZ}");
        }
    }

    [Fact]
    public void P7_Map22ElevatorOnTheRealMap()
    {
        Log("== P7 map 22 (Lars' Crypt): arrival on the platform, T3 on (lever 2 hit), the elevator runs by itself");
        var spec = new ArcSpec("D5b-probe-22", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1008 << 16, 176 << 16, 2 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2);
        Log($"records present: platform={(platform != null)} lever0={(arc.EntityByRecord(0) != null)} lever1={(arc.EntityByRecord(1) != null)}");
        if (platform == null)
        {
            return;
        }

        var hero = arc.Hero;
        Log($"after frame 1: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) platform=({platform.PosX / 65536.0},{platform.PosY / 65536.0},{platform.PosZ / 65536.0}) top={(platform.PosZ + platform.ModZ + platform.Depth + 1) / 65536.0} riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne}");
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Log($"after 21 frames: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) platform.PosZ={platform.PosZ / 65536.0} riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne}");
        GameState.Instance.AddFlag(0x8003, 1u << 3); // T3 on: the lever 2 has been hit.
        for (var f = 1; f <= 200; f++)
        {
            arc.OneFrame();
            var t5 = (GameState.Instance.GetFlag(0x8005) & (1u << 5)) != 0;
            var g129 = (GameState.Instance.GetFlag(129) & (1u << (129 & 31))) != 0;
            var locked = (GameState.Instance.PlayerControlFlags & GameState.PlayerControlBits.ControlLocked) != 0;
            if (f <= 14 || f % 8 == 0 || (f > 60 && f < 90) || platform.ForceZ == 0 && f > 20 && f % 4 == 0)
            {
                var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
                Log($"f{f,3}: platform.PosZ={platform.PosZ / 65536.0} forceZ={platform.ForceZ} hero.Z={hero.PosZ / 65536.0} heroFoot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px heroXY=({hero.PosX / 65536.0},{hero.PosY / 65536.0}) riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne} T5={t5} G129={g129} locked={locked} warping={WarpDirector.Instance.IsTransitionInProgress}");
            }

            if (g129)
            {
                Log($"G129 raised at f{f} (the program reached its 0x53 just after)");
                break;
            }
        }
    }

    [Fact]
    public void P8_Map22PlatformGoingDownThenUpUnderTheHero()
    {
        Log("== P8 map 22: the lift rises 20 px by the real script, then the test writes ForceZ=-65536 for 24 frames (descent), then 0 for 5, then +65536 again for 10");
        var spec = new ArcSpec("D5b-probe-22b", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1008 << 16, 176 << 16, 2 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        var platform = arc.EntityByRecord(2)!;
        var hero = arc.Hero;
        GameState.Instance.AddFlag(0x8003, 1u << 3);
        var phase = 0;
        for (var f = 1; f <= 120; f++)
        {
            arc.OneFrame();
            if (f == 31)
            {
                phase = 1;
            }
            else if (f == 55)
            {
                phase = 2;
            }
            else if (f == 60)
            {
                phase = 3;
            }

            if (phase == 1)
            {
                platform.ForceZ = -65536;
            }
            else if (phase == 2)
            {
                platform.ForceZ = 0;
            }
            else if (phase == 3 && f < 71)
            {
                platform.ForceZ = 65536;
            }

            if (f >= 28 && f <= 75)
            {
                var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
                Log($"f{f,3}: phase {phase} platform.PosZ={platform.PosZ / 65536.0} forceZ={platform.ForceZ} hero.Z={hero.PosZ / 65536.0} heroFoot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} collZ={hero.CollidedWithEntityZ} airborne={hero.HeroAirborne}");
            }
        }
    }

    [Fact]
    public void P9_Map22TheWholeRecipeWithThePad()
    {
        Log("== P9 map 22: arrival on the floor south of the lever 2 (tile 46,8), hit the lever, walk round the recess, jump onto the lift, wait for the ride");
        var spec = new ArcSpec("D5b-probe-22c", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 1500, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1116 << 16, 136 << 16, 2 << 20, GameState.ResetAnimationId, 16));
        using var arc = new ArcRun(spec);
        var hero = arc.Hero;
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var lever = arc.EntityByRecord(1)!;
        Log($"start: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) lever1.pos=({lever.PosX / 65536.0},{lever.PosY / 65536.0},{lever.PosZ / 65536.0}) locked={(GameState.Instance.PlayerControlFlags & GameState.PlayerControlBits.ControlLocked) != 0}");
        for (var i = 0; i < 30; i++)
        {
            arc.OneFrame();
        }

        hero.TargetDirection = 16;
        Log($"after 31 frames: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) dir={hero.CurrentDirection} locked={(GameState.Instance.PlayerControlFlags & GameState.PlayerControlBits.ControlLocked) != 0}");
        arc.Press(PadState.Square);
        for (var i = 0; i < 30; i++)
        {
            arc.OneFrame();
        }

        bool T(uint n) => (GameState.Instance.GetFlag(0x8000 | n) & (1u << (int)(n & 31))) != 0;
        Log($"after the hit: T3={T(3)} T4={T(4)} T1={T(1)} lever.touching={(lever.TouchingEntity != null)} hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0})");
        arc.HoldDirections(PadState.Down);
        var guard = 0;
        while (hero.PosY < 186 * 65536 && guard++ < 120)
        {
            arc.OneFrame();
        }

        Log($"walked south: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) frames={guard}");
        arc.HoldDirections(PadState.Left);
        guard = 0;
        while (hero.PosX > 1094 * 65536 && guard++ < 120)
        {
            arc.OneFrame();
        }

        Log($"walked west to the edge: hero=({hero.PosX / 65536.0},{hero.PosY / 65536.0},{hero.PosZ / 65536.0}) frames={guard}");
        arc.HoldDirections(PadState.Left);
        arc.Press(PadState.Cross | PadState.Left);
        for (var i = 0; i < 40; i++)
        {
            arc.OneFrame();
            if (i % 2 == 0 || ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity))
            {
                Log($"jump +{i + 1}: hero=({hero.PosX / 65536.0:0.##},{hero.PosY / 65536.0:0.##},{hero.PosZ / 65536.0:0.##}) riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne} platZ={platform.PosZ / 65536.0} T5={T(5)}");
            }
        }

        Log("-- second jump, from the recess floor onto the lift (16 px up), holding West");
        arc.HoldDirections(PadState.Left);
        arc.Press(PadState.Cross | PadState.Left);
        for (var i = 0; i < 40; i++)
        {
            arc.OneFrame();
            if (i % 2 == 0 || ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity))
            {
                Log($"jump2 +{i + 1}: hero=({hero.PosX / 65536.0:0.##},{hero.PosY / 65536.0:0.##},{hero.PosZ / 65536.0:0.##}) riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne} platZ={platform.PosZ / 65536.0} T5={T(5)}");
            }
        }

        arc.ReleaseDirections();
        for (var f = 0; f < 200; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            if (f < 25 || f % 10 == 0 || T(5) && f % 5 == 0 || WarpDirector.Instance.IsTransitionInProgress)
            {
                Log($"wait {f,3}: hero=({hero.PosX / 65536.0:0.##},{hero.PosY / 65536.0:0.##},{hero.PosZ / 65536.0:0.##}) platZ={platform.PosZ / 65536.0} foot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} T5={T(5)} locked={(GameState.Instance.PlayerControlFlags & GameState.PlayerControlBits.ControlLocked) != 0} warping={WarpDirector.Instance.IsTransitionInProgress}");
            }

            if (WarpDirector.Instance.IsTransitionInProgress)
            {
                Log($"warp started at wait frame {f}");
                break;
            }
        }
    }

    [Fact]
    public void P10_Map22TheBarrelAndTheHeroOnTheRisingLift()
    {
        Log("== P10 map 22: the barrel (record 33, gravity crate 21x15x16) is put on the lift next to the hero, T3 on, the lift rises by its script");
        var spec = new ArcSpec("D5b-probe-22d", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(996 << 16, 176 << 16, 2 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var barrel = arc.EntityByRecord(33);
        Log($"barrel present={(barrel != null)}");
        if (barrel == null)
        {
            return;
        }

        var hero = arc.Hero;
        barrel.PosX = 1020 << 16;
        barrel.PosY = 176 << 16;
        barrel.PosZ = (32 << 16) - barrel.ModZ;
        barrel.TileX = barrel.PosX >> 16 / 24;
        barrel.PushLogicalPositionToRoot();
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Log($"settled: hero.Z={hero.PosZ / 65536.0} barrel=({barrel.PosX / 65536.0},{barrel.PosY / 65536.0},{barrel.PosZ / 65536.0}) barrel.riding={ReferenceEquals(barrel.RidingEntity, platform.LogicContextEntity)} barrel.onGround={barrel.IsOnGround} hero.riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)}");
        GameState.Instance.AddFlag(0x8003, 1u << 3);
        for (var f = 1; f <= 90; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            if (f <= 16 || f % 6 == 0 || WarpDirector.Instance.IsTransitionInProgress)
            {
                Log($"f{f,3}: platZ={platform.PosZ / 65536.0} hero.foot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px barrel.foot-top={(barrel.PosZ + barrel.ModZ - top) / 65536.0}px barrel.riding={ReferenceEquals(barrel.RidingEntity, platform.LogicContextEntity)} barrel.onGround={barrel.IsOnGround} barrel.collZ={barrel.CollidedWithEntityZ} barrel.ForceZ={barrel.ForceZ} barrelXY=({barrel.PosX / 65536.0},{barrel.PosY / 65536.0})");
            }

            if (WarpDirector.Instance.IsTransitionInProgress)
            {
                break;
            }
        }
    }

    [Fact]
    public void P11_Map22TheHeroFallsOntoTheRisingLift()
    {
        Log("== P11 map 22: the hero arrives 32 px above the lift (z level 4), the test makes the lift rise +1 px/tick from frame 1 (the script is idle, T3 off): the hero falls onto a RISING platform");
        var spec = new ArcSpec("D5b-probe-22e", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1008 << 16, 176 << 16, 4 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var hero = arc.Hero;
        Log($"after frame 1: hero.Z={hero.PosZ / 65536.0} platform.Z={platform.PosZ / 65536.0} airborne={hero.HeroAirborne} onGround={hero.IsOnGround}");
        platform.ForceZ = 65536;
        for (var f = 1; f <= 45; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            Log($"f{f,3}: platZ={platform.PosZ / 65536.0} plat.collZ={platform.CollidedWithEntityZ} hero.Z={hero.PosZ / 65536.0} heroFoot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} collZ={hero.CollidedWithEntityZ} airborne={hero.HeroAirborne} heroForceZ={hero.ForceZ}");
        }
    }

    [Fact]
    public void P12_Map22TheHeroFallsOntoAFastRisingLift()
    {
        Log("== P12 map 22: like P11 with the lift rising +3 px/tick (ForceZ 196608) and the hero arriving at z level 3 (48 px)");
        var spec = new ArcSpec("D5b-probe-22f", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1008 << 16, 176 << 16, 3 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var hero = arc.Hero;
        Log($"after frame 1: hero.Z={hero.PosZ / 65536.0} platform.Z={platform.PosZ / 65536.0}");
        platform.ForceZ = 196608;
        for (var f = 1; f <= 14; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            Log($"f{f,3}: platZ={platform.PosZ / 65536.0} plat.collZ={platform.CollidedWithEntityZ} hero.Z={hero.PosZ / 65536.0} heroFoot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, platform.LogicContextEntity)} onGround={hero.IsOnGround} collZ={hero.CollidedWithEntityZ} heroForceZ={hero.ForceZ}");
        }
    }

    [Fact]
    public void P13_Map22TheMarkedBarrelOnTheRisingLift()
    {
        Log("== P13 map 22: like P10 with the barrel marked HasLogicalZ (the path of the dropped items and thrown objects)");
        var spec = new ArcSpec("D5b-probe-22g", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(996 << 16, 176 << 16, 2 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var barrel = arc.EntityByRecord(33)!;
        barrel.PosX = 1020 << 16;
        barrel.PosY = 176 << 16;
        barrel.PosZ = (32 << 16) - barrel.ModZ;
        barrel.PushLogicalPositionToRoot();
        barrel.HasLogicalZ = true;
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        GameState.Instance.AddFlag(0x8003, 1u << 3);
        for (var f = 1; f <= 40; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            if (f <= 14 || f % 8 == 0)
            {
                Log($"f{f,3}: platZ={platform.PosZ / 65536.0} barrel.foot-top={(barrel.PosZ + barrel.ModZ - top) / 65536.0}px barrel.riding={ReferenceEquals(barrel.RidingEntity, platform.LogicContextEntity)} barrel.onGround={barrel.IsOnGround} barrel.collZ={barrel.CollidedWithEntityZ} barrel.ForceZ={barrel.ForceZ}");
            }
        }
    }

    [Fact]
    public void P14_Map22ThePileHeroOnBarrelOnTheLiftMovingEastAndUp()
    {
        Log("== P14 map 22: hero on the barrel on the lift; the test drives the lift +1 px/tick east and +1 px/tick up for 14 frames (the script is idle, T3 off)");
        var spec = new ArcSpec("D5b-probe-22h", "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, 900, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(1020 << 16, 176 << 16, 3 << 20, GameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var barrel = arc.EntityByRecord(33)!;
        var hero = arc.Hero;
        barrel.PosX = 1020 << 16;
        barrel.PosY = 176 << 16;
        barrel.PosZ = (32 << 16) - barrel.ModZ;
        barrel.PushLogicalPositionToRoot();
        hero.PosX = 1020 << 16;
        hero.PosY = 176 << 16;
        hero.PosZ = (48 << 16) - hero.ModZ;
        hero.PushLogicalPositionToRoot();
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Log($"settled: platform=({platform.PosX / 65536.0},{platform.PosY / 65536.0},{platform.PosZ / 65536.0}) barrel=({barrel.PosX / 65536.0},{barrel.PosZ / 65536.0}) hero=({hero.PosX / 65536.0},{hero.PosZ / 65536.0}) barrel.riding={ReferenceEquals(barrel.RidingEntity, platform.LogicContextEntity)} hero.riding={ReferenceEquals(hero.RidingEntity, barrel.LogicContextEntity)}");
        platform.TargetDirection = platform.CurrentDirection;
        platform.TargetForceX = 65536;
        platform.ForceX = 65536;
        platform.ForceStepX = 0;
        platform.ForceZ = 65536;
        var p0 = platform.PosX;
        var b0 = barrel.PosX;
        var h0 = hero.PosX;
        for (var f = 1; f <= 14; f++)
        {
            arc.OneFrame();
            var top = platform.PosZ + platform.ModZ + platform.Depth + 1;
            var btop = barrel.PosZ + barrel.ModZ + barrel.Depth + 1;
            Log($"f{f,3}: platform dX={(platform.PosX - p0) / 65536.0} dZ={(platform.PosZ - 24 * 65536) / 65536.0} | barrel dX={(barrel.PosX - b0) / 65536.0} foot-top={(barrel.PosZ + barrel.ModZ - top) / 65536.0}px riding={ReferenceEquals(barrel.RidingEntity, platform.LogicContextEntity)} | hero dX={(hero.PosX - h0) / 65536.0} foot-barreltop={(hero.PosZ + hero.ModZ - btop) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, barrel.LogicContextEntity)}");
        }
    }

    [Fact]
    public void P15_TheDockHeroStraddlingTheFloorAndARisingObjectFlushInAHollow()
    {
        Log("== P15 montage of UH-17: object 24x16x16 flush with the 16 px floor in a hollow (cell 8,6: x 192..216), the hero straddles the floor (cell 7) and the object; the object rises +1 px/tick from tick 1");
        var field = FlatCells.Create(cell: (cx, cy) => (0, cx == 8 && cy == 6 ? 0 : 1));
        var rig = JumpHeroRig.Build(
            field,
            configure: (world, host) => AddObject(world, host, "Switch", 148, 96, 24, 16, 16, offsetX: 44),
            x: 193f,
            y: 103.5f,
            z: 16f,
            freePad: true);
        var hero = rig.Hero;
        var obj = rig.Host.All.Single(e => !ReferenceEquals(e, hero));
        for (var i = 0; i < 4; i++)
        {
            rig.Step();
        }

        Log($"settled: hero=({hero.PosX / 65536.0},{hero.PosZ / 65536.0}) riding={ReferenceEquals(hero.RidingEntity, obj.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne} obj.Z={obj.PosZ / 65536.0}");
        obj.ForceZ = 65536;
        for (var tick = 1; tick <= 8; tick++)
        {
            if (tick == 7)
            {
                obj.ForceZ = 0;
            }

            rig.Step();
            var top = obj.PosZ + obj.ModZ + obj.Depth + 1;
            Log($"tick {tick}: obj.Z={obj.PosZ / 65536.0} hero.Z={hero.PosZ / 65536.0} heroFoot-top={(hero.PosZ + hero.ModZ - top) / 65536.0}px riding={ReferenceEquals(hero.RidingEntity, obj.LogicContextEntity)} onGround={hero.IsOnGround} airborne={hero.HeroAirborne}");
        }
    }
}
