#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.h1b2 (docs/plan-e19-opcodes.md §1.2n.1c, H1B2-R3 and R4): the vertical step of an entity without a controller (a point box or a one-unit box: 12 prefabs,
/// 67 records), which the binary steps exactly like any other entity (<c>0x80038364</c>, no notion of a controller). Every value is a row of
/// <c>docs/plan-e19-h1b2-annexe/traces.out</c> (the PosZ column, already in the DLL's convention: binary - 1) or of <c>scen_real.out</c>, written before the code;
/// a value the measure contradicts is a stop, never a re-pin. The montage is <see cref="ContactWorld.AddEntity"/> with <c>withController: false</c> in a real world
/// on a flat field, one update = one logic tick, one settling update first (the entity is then put in the state of the scenario).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraControllerlessVerticalStepTests
{
    private sealed class Rig
    {
        public required World World { get; init; }

        public required ContactHost Host { get; init; }

        public required AlundraEntityScriptProxy Entity { get; init; }

        public void Tick()
        {
            Host.Rebuild();
            World.Update(0.02f);
            Host.Rebuild();
        }
    }

    /// <summary>A controller-less entity (a box of 24 x 16 x <paramref name="sizeZ"/>) in a world on a flat field of <paramref name="terrainPixels"/>, in the state of
    /// the scenario: <paramref name="startPosZ"/> (raw), <paramref name="forceZ"/>, the gravity flag and the raw gravity/viscosity words of the map.</summary>
    private static Rig Build(
        int terrainPixels, int startPosZ, int forceZ, bool gravity, int gravityRaw = 128, int viscosityRaw = 4096, int sizeZ = 16)
    {
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = terrainPixels }, null);
        var host = new ContactHost();
        var entity = ContactWorld.AddEntity(world, host, "Point", 200, 100, 0, 0, 0, 0, 24, 16, sizeZ, withController: false);
        entity.MapGravityRaw = gravityRaw;
        entity.MapZViscosityRaw = viscosityRaw;
        var rig = new Rig { World = world, Host = host, Entity = entity };
        rig.Tick(); // the settling update (the entity is integrated into the world), then the scenario's own state.
        entity.PosZ = startPosZ;
        entity.TileZ = startPosZ >> 20;
        entity.ForceZ = forceZ;
        entity.FinalForceZ = 0;
        entity.CollidedWithEntityZ = 0;
        entity.IsOnGround = 0;
        entity.Flags &= ~EntityFlags.Gravity;
        if (gravity)
        {
            entity.Flags |= EntityFlags.Gravity;
        }

        return rig;
    }

    private static (int PosZ, int ForceZ, int FinalForceZ, int Contact, int OnGround) State(AlundraEntityScriptProxy e) =>
        (e.PosZ, e.ForceZ, e.FinalForceZ, e.CollidedWithEntityZ, e.IsOnGround);

    /// <summary>Runs the ticks and checks each row of <paramref name="expected"/> (tick number -> state) at its tick.</summary>
    private static void AssertRows(Rig rig, IDictionary<int, (int PosZ, int ForceZ, int FinalForceZ, int Contact, int OnGround)> expected)
    {
        var last = 0;
        foreach (var tick in expected.Keys)
        {
            last = Math.Max(last, tick);
        }

        for (var tick = 1; tick <= last; tick++)
        {
            rig.Tick();
            if (expected.TryGetValue(tick, out var row))
            {
                Assert.True(row == State(rig.Entity), $"tick {tick}: expected (PosZ, ForceZ, FinalForceZ, contact, IsOnGround) {row}, read {State(rig.Entity)}");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // Z-1 .. Z-9 of traces.out (the PosZ column, the DLL's convention).
    // ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Z1_ARiseWithoutGravity_MovesByTheForceEveryTick_AndIsNeverOnTheGround()
    {
        var rig = Build(0, 0, 32768, gravity: false);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (32768, 32768, 32768, 0, 0),
            [2] = (65536, 32768, 32768, 0, 0),
            [3] = (98304, 32768, 32768, 0, 0),
            [4] = (131072, 32768, 32768, 0, 0),
            [5] = (163840, 32768, 32768, 0, 0),
        });
    }

    [Fact]
    public void Z2_AFallWithoutGravity_OverALowTerrain_Takes192TicksFor96Pixels_WithNoContact()
    {
        // Sara of map 47: 160 px, ForceZ -32768, the terrain 16 px: 192 ticks to 64 px.
        var rig = Build(16, 10485760, -32768, gravity: false);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (10452992, -32768, -32768, 0, 0),
            [2] = (10420224, -32768, -32768, 0, 0),
            [3] = (10387456, -32768, -32768, 0, 0),
            [190] = (4259840, -32768, -32768, 0, 0),
            [191] = (4227072, -32768, -32768, 0, 0),
            [192] = (4194304, -32768, -32768, 0, 0),
        });
    }

    [Fact]
    public void Z3_AGravityEntityDroppedFrom5Px_LandsAtTick4_WithTheContactOneTickLater()
    {
        var rig = Build(0, 327680, 0, gravity: true);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (294912, -32768, -32768, 0, 0),
            [2] = (229376, -65536, -65536, 0, 0),
            [3] = (131072, -98304, -98304, 0, 0),
            [4] = (0, -131072, -131072, 0, 1), // reaching T exactly is not a landing (strict test): on the ground, no contact yet.
            [5] = (0, 0, -163840, 1, 1),
            [6] = (0, 0, -32768, 1, 1),
            [7] = (0, 0, -32768, 1, 1),
            [8] = (0, 0, -32768, 1, 1),
        });
    }

    [Fact]
    public void Z4_AtRestWithGravity_TheContactIsRaisedAtEveryTick()
    {
        var rig = Build(0, 0, 0, gravity: true);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (0, 0, -32768, 1, 1),
            [2] = (0, 0, -32768, 1, 1),
            [3] = (0, 0, -32768, 1, 1),
            [4] = (0, 0, -32768, 1, 1),
        });
    }

    [Fact]
    public void Z5_AtRestWithoutGravity_IsOnTheGround_WithoutContact()
    {
        var rig = Build(0, 0, 0, gravity: false);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (0, 0, 0, 0, 1),
            [2] = (0, 0, 0, 0, 1),
            [3] = (0, 0, 0, 0, 1),
        });
    }

    [Fact]
    public void Z6_OneUnitBelowTheTerrainWithoutForce_IsLiftedToItWithAContact()
    {
        var rig = Build(16, 1048575, 0, gravity: false);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (1048576, 0, 0, 1, 1),
            [2] = (1048576, 0, 0, 0, 1),
            [3] = (1048576, 0, 0, 0, 1),
        });
    }

    [Fact]
    public void Z6b_ExactlyAtTheTerrainWithoutForce_NothingHappens_ItIsOnTheGround()
    {
        var rig = Build(16, 1048576, 0, gravity: false);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (1048576, 0, 0, 0, 1),
            [2] = (1048576, 0, 0, 0, 1),
            [3] = (1048576, 0, 0, 0, 1),
        });
    }

    [Fact]
    public void Z7_TheTerminalVelocityOfAMap_Is32StepsOfTheGravity_ThenConstant_AndTheLandingIsAtTick78()
    {
        var rig = Build(0, 39321600, 0, gravity: true);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (39288832, -32768, -32768, 0, 0),
            [2] = (39223296, -65536, -65536, 0, 0),
            [3] = (39124992, -98304, -98304, 0, 0),
            [30] = (24084480, -983040, -983040, 0, 0),
            [31] = (23068672, -1015808, -1015808, 0, 0),
            [32] = (22020096, -1048576, -1048576, 0, 0),
            [33] = (20971520, -1048576, -1048576, 0, 0),
            [34] = (19922944, -1048576, -1048576, 0, 0),
            [35] = (18874368, -1048576, -1048576, 0, 0),
            [78] = (0, 0, -32768, 1, 1),
            [79] = (0, 0, -32768, 1, 1),
            [80] = (0, 0, -32768, 1, 1),
        });
    }

    [Fact]
    public void Z7b_TheMapsWithALowGravity_ReachTheirTerminalVelocityAtTick86()
    {
        // Maps 159 and 160: G 3, V 256 (terminal 256 << 8 = 65536).
        var rig = Build(0, 19660800, 0, gravity: true, gravityRaw: 3, viscosityRaw: 256);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (19660032, -768, -768, 0, 0),
            [2] = (19658496, -1536, -1536, 0, 0),
            [3] = (19656192, -2304, -2304, 0, 0),
            [84] = (16919040, -64512, -64512, 0, 0),
            [85] = (16853760, -65280, -65280, 0, 0),
            [86] = (16788224, -65536, -65536, 0, 0),
            [87] = (16722688, -65536, -65536, 0, 0),
            [98] = (16001792, -65536, -65536, 0, 0),
            [99] = (15936256, -65536, -65536, 0, 0),
            [100] = (15870720, -65536, -65536, 0, 0),
        });
    }

    [Fact]
    public void Z8_AScriptForceOnAGravityEntity_IsEatenByTheDecayOfTheSameTick()
    {
        // 0x1B [128, 0] (+32768) written by the script of the tick, the gravity not cleared first.
        var rig = Build(0, 655360, 32768, gravity: true);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (655360, 0, 0, 0, 0),
            [2] = (622592, -32768, -32768, 0, 0),
            [3] = (557056, -65536, -65536, 0, 0),
            [4] = (458752, -98304, -98304, 0, 0),
        });
    }

    [Fact]
    public void Z9_TheAbsoluteCeilingOf1920Pixels_StopsTheRise_WithAContactAtEveryTick()
    {
        // SizeZ 32 (Depth 2097151): the top of the box may not go past 0x7800000; the force is unchanged.
        var rig = Build(0, 117440512, 4194304, gravity: false, sizeZ: 32);
        AssertRows(rig, new Dictionary<int, (int, int, int, int, int)>
        {
            [1] = (121634816, 4194304, 4194304, 0, 0),
            [2] = (123731967, 4194304, 4194304, 1, 0),
            [3] = (123731967, 4194304, 4194304, 1, 0),
            [4] = (123731967, 4194304, 4194304, 1, 0),
        });
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // The guard of the harness: an entity outside any world is not stepped by the new code.
    // ---------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Guard_ABareProxyWithGravityAndTheRawWords_OutsideAnyWorld_KeepsItsForcePositionAndGround()
    {
        // The intro harness: bare entities, never in a world, which run their own vertical pass. The criterion "the world has a collision field" keeps them out.
        var proxy = new AlundraEntityScriptProxy { EntityRefId = 1, Status = EntityStatus.Normal, PosZ = 327680 };
        proxy.Flags |= EntityFlags.Gravity | EntityFlags.Collidable;
        proxy.MapGravityRaw = 128;
        proxy.MapZViscosityRaw = 4096;

        for (var call = 0; call < 5; call++)
        {
            proxy.EvaluateEntitySupport(Array.Empty<AlundraEntityScriptProxy>());
            Assert.Equal((327680, 0, 0, 0), (proxy.PosZ, proxy.ForceZ, proxy.IsOnGround, proxy.CollidedWithEntityZ));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    // The gate of the factory and the two real cases, on the real maps with the production spawn path.
    // ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>The program of Sara (C[4] of map 47, @352) holds five <c>0x36</c> waits (flags 33778, 33779, 33780, 33783 and 32819) and three dialogues before the fall
    /// at @426: the montage sets the five flags (the dream's other programs set them in the game) and presses the dialogue button like the player.</summary>
    private static readonly int[] SaraFlags = { 33778, 33779, 33780, 33783, 32819 };

    private const int ProbeTileX = 10;
    private const int ProbeTileY = 10;

    /// <summary>The arc of a real map with the real prefabs, and the record <paramref name="record"/> spawned by the production call of the opcodes 0x2D and 0x8B
    /// (<c>SpawnEntityByRecordId</c>): the two records of these cases have a <c>SpriteDirection</c> without the bit 0x40 (the spawn zone of the map load never takes them), a
    /// program of the map spawns them in the game.</summary>
    private static ArcRun Arc(string name, string zone, string world, int frames, int record, params int[] flags)
    {
        var arc = new ArcRun(new ArcSpec(name, zone, world, flags, ProbeTileX, ProbeTileY, 1, frames, RealController: true, Prefabs: true));
        var spawned = ((IEntityWorldContext)arc.Proxy).SpawnEntityByRecordId(arc.Hero, record);
        Assert.NotNull(spawned);
        return arc;
    }

    [Fact]
    public void Gate_ARealPrefabWithoutController_HasTheRawMapWordsOfItsMap()
    {
        // Map 260 record 17 (the broken armour of I33) and map 47 record 3 (Sara): gravity, no controller, maps at 128 / 4096.
        using (var arc = Arc("H1b2-gate-260", "Inoa", "Inoa (inner)-260", 10, 17))
        {
            var armour = arc.EntityByRecord(17);
            Assert.NotNull(armour);
            Assert.Null(armour!.Controller);
            Assert.Equal((128, 4096), (armour.MapGravityRaw, armour.MapZViscosityRaw));
        }

        using (var arc = Arc("H1b2-gate-47", "Unused Dream", "Unused Dream (Boss) (Beta Surferboys Dream)_-47", 10, 3, SaraFlags))
        {
            var sara = arc.EntityByRecord(3);
            Assert.NotNull(sara);
            Assert.Null(sara!.Controller);
            Assert.Equal((128, 4096), (sara.MapGravityRaw, sara.MapZViscosityRaw));
        }
    }

    /// <summary>Runs the arc until <paramref name="record"/>'s <c>ForceZ</c> first equals <paramref name="startForce"/> (the frame c0 of its program: its <c>0x1B</c> and its
    /// first <c>0x20</c> ran), then until it returns to 0 (the frame in which the wait ended and <c>1B [0,0]</c> ran). Returns (rest PosZ before c0, c0, end frame, PosZ
    /// at the end frame, whether a contact or IsOnGround was ever set from c0 to the end).</summary>
    private static (int RestPosZ, int C0, int End, int EndPosZ, bool AnyContactOrGround) RunTheWait(ArcRun arc, int record, int startForce, int[]? heldFlags = null)
    {
        var entity = arc.EntityByRecord(record);
        Assert.NotNull(entity);
        Assert.Null(entity!.Controller);

        var rest = entity.PosZ;
        var c0 = -1;
        var end = -1;
        var any = false;
        arc.OnFrame = () =>
        {
            foreach (var flag in heldFlags ?? Array.Empty<int>())
            {
                ArcRun.State.AddFlag((uint)flag, 1u << (flag & 0x1f));
            }

            if (c0 < 0)
            {
                if (entity.ForceZ == startForce)
                {
                    c0 = arc.Frame;
                }
                else
                {
                    rest = entity.PosZ;
                }
            }
            else if (end < 0)
            {
                any |= entity.CollidedWithEntityZ != 0 || entity.IsOnGround != 0;
                if (entity.ForceZ == 0)
                {
                    end = arc.Frame;
                }
            }
        };

        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => end >= 0, $"the 0x20 of record {record} ends");
        arc.OnFrame = null;
        return (rest, c0, end, entity.PosZ, any);
    }

    [Fact]
    public void Real_Map260_TheArmourOfRecord17_Rises32Pixels_InExactly64Calls()
    {
        using var arc = Arc("H1b2-260", "Inoa", "Inoa (inner)-260", 400, 17);

        var (rest, c0, end, endPosZ, any) = RunTheWait(arc, 17, 32768);

        // 0x63 clears the gravity, 0x1B [128,0] = +32768 per tick, 0x20 [32,0] @747: the wait ends at the 64th call after the first; the climb is 2097152 exactly.
        Assert.Equal(64, end - c0);
        Assert.Equal(rest + 2097152, endPosZ);
        Assert.False(any, "no contact and never on the ground during the climb");

        // Afterwards the armour stays where it is.
        for (var frame = 0; frame < 3; frame++)
        {
            arc.OneFrame();
            Assert.Equal(rest + 2097152, arc.EntityByRecord(17)!.PosZ);
        }
    }

    [Fact]
    public void Real_Map47_SaraOfRecord3_Falls96Pixels_InExactly192Calls_WithoutContact()
    {
        using var arc = Arc("H1b2-47", "Unused Dream", "Unused Dream (Boss) (Beta Surferboys Dream)_-47", 4000, 3, SaraFlags);

        var (_, c0, end, endPosZ, any) = RunTheWait(arc, 3, -32768, SaraFlags);

        // 0x17 clears the gravity, 0x64 puts her at 160 px, 0x1B [128,255] = -32768 per tick, 0x20 [96,0] @442: 192 calls, 64 px, the terrain (16 px) never reached.
        Assert.Equal(192, end - c0);
        Assert.Equal(4194304, endPosZ);
        Assert.False(any, "no contact and never on the ground during the fall");

        for (var frame = 0; frame < 3; frame++)
        {
            arc.OneFrame();
            Assert.Equal(4194304, arc.EntityByRecord(3)!.PosZ);
        }
    }
}
