#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Physics;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.e0k2 (docs/plan-e20-heros.md section 1.18.2, T1, phase A): the logical Z of an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>. Nothing in the tick calls the step yet (phase B wires it): these tests
/// call the two functions BY HAND on a real entity of a real world (<see cref="ArcRun"/>, map 14, the real prefabs and controllers), with the head-of-tick clears the tick would do, against the oracle of
/// <c>docs/plan-e20-annexe/e20-e0k/oracle/out</c> read by key (never copied), in the DLL's frame (the oracle minus one raw unit, ADR-0026). Each case without effect is set next to a positive case in the same assertion.
/// (a) the marker <see cref="EntityScriptProxy.HasLogicalZ"/> is posed by the interface and by it alone; (b) the step: the ball (<c>arrows-ball-fire</c>), the arrow (Z constant, <c>IsOnGround</c> with and without the
/// unit), the impulse of 1 024 (<c>crate-flat-state1</c>), the exact graze, the ceiling, the magnet of 3 px, the stop marker 0x8000, the sentinel and the root that follows.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LogicalZStepTests
{
    private const int Arrow = 3;
    private const int FireBall = 11;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0k2-A-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static TileMapData Map(int gravity, int viscosity)
    {
        var map = new TileMapData();
        map.CustomProperties["Gravity"] = gravity.ToString();
        map.CustomProperties["ZViscosity"] = viscosity.ToString();
        return map;
    }

    private static void Install(ArcRun arc, CellsCollisionField field)
    {
        typeof(WorldProxy).GetProperty(nameof(WorldProxy.CollisionField))!.SetValue(arc.Proxy, field);
        arc.RealWorld!.CollisionField = field;
        typeof(WorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, Map(128, 4096));
    }

    /// <summary>A field whose every cell has the height byte <paramref name="heightByte"/> (16 px each).</summary>
    private static CellsCollisionField Floor(int heightByte) => FlatCells.Create(cell: (x, y) => (0, heightByte));

    private static EntityScriptProxy Born(ArcRun arc, int sprite, int zRaw)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, sprite, 100 << 16, 100 << 16, zRaw, 0x18)!;

    /// <summary>One tick of the logical step as the head of <c>RunOneMotionTick</c> and the two calls of phase B will make it: the clears, the first half, (no XY step), the second half.</summary>
    private static void Tick(EntityScriptProxy entity, bool post = true)
    {
        entity.CollidedWithEntityZ = 0;
        entity.ZHeldByTick = false;
        Assert.True(ScriptedMotion.RunLogicalZPreXyStep(entity));
        if (post)
        {
            ScriptedMotion.RunLogicalZPostXyStep(entity);
        }
    }

    /// <summary>A series as one string, so that a tuple of series compares by value.</summary>
    private static string Series(IEnumerable<int> values) => string.Join(",", values);

    private static JsonElement OracleRows(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-e0k", "oracle", "out", file);
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.Clone();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-e0k/oracle/out/{file}' found above '{AppContext.BaseDirectory}'");
    }

    private static IReadOnlyList<JsonElement> OracleSlot(JsonElement root)
    {
        var slot = root.GetProperty("meta").GetProperty("slot").GetInt32().ToString();
        return root.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("slots").GetProperty(slot)).ToList();
    }

    private static int OracleInit(JsonElement root, string field) => root.GetProperty("meta").GetProperty("init").GetProperty(field).GetInt32();

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a) the marker: posed by the interface and by it alone.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_TheMarker_IsPosedByTheInterfaceAndByItAlone_NotByTheHero_TheRecordCreations_TheRecycledEntities_NorTheClone()
    {
        using var arc = Arc("a");
        Install(arc, FlatCells.Create());

        var born = Born(arc, FireBall, 32 << 16);
        var clone = Assert.IsType<EntityScriptProxy>(born.Clone());
        var fromRecord = arc.Entities.First(e => !ReferenceEquals(e, arc.Hero) && !ReferenceEquals(e, born));
        var recycled = arc.Entities.First(e => !ReferenceEquals(e, arc.Hero) && !ReferenceEquals(e, born) && !ReferenceEquals(e, fromRecord));
        recycled.Status = EntityStatus.FlagToDestroy;
        arc.Proxy.RecycleDestroyedEntities();

        // The two criteria that would be wrong are pinned: the hero and the recycled entity have EntityRefId == -1 and no marker; the born entity has JoinsBirthTick and so does no entity of the map load (the entities that script opcodes made from a record have it since E20.e0a4).
        Assert.Equal((-1, -1), (arc.Hero.EntityRefId, recycled.EntityRefId));
        Assert.True(born.JoinsBirthTick);
        Assert.Equal(
            (Hero: false, Record: false, Recycled: false, Born: true, Clone: false),
            (Hero: arc.Hero.HasLogicalZ, Record: fromRecord.HasLogicalZ, Recycled: recycled.HasLogicalZ, Born: born.HasLogicalZ, Clone: clone.HasLogicalZ));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b) the step called by hand.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B1_TheBall_FollowsTheSeriesOfTheOracleMinusOneRawUnit_LandsAtTheTickOfTheOracle_AndTheSameBallWithoutGravityStaysAtItsHeight()
    {
        const int ticks = 14;
        var root = OracleRows("arrows-ball-fire.json");
        var oracle = OracleSlot(root);
        using var arc = Arc("b1");
        Install(arc, FlatCells.Create());

        var ball = Born(arc, FireBall, 32 << 16);
        var witness = Born(arc, FireBall, 32 << 16);
        witness.Flags &= ~EntityFlags.Gravity;
        Assert.Equal(OracleInit(root, "pz") - 1, ball.PosZ); // the birth, in the DLL's frame.

        var z = new List<int>();
        var collided = new List<int>();
        var ground = new List<int>();
        var tickForces = new List<int>();
        var witnessZ = new List<int>();
        for (var t = 0; t < ticks; t++)
        {
            Tick(ball);
            z.Add(ball.PosZ);
            collided.Add(ball.CollidedWithEntityZ);
            ground.Add(ball.IsOnGround);
            tickForces.Add(ball.TickForceZ);
            Tick(witness);
            witnessZ.Add(witness.PosZ);
        }

        var landingTick = oracle.Select((s, i) => (s, i)).First(p => p.s.GetProperty("ground_144").GetInt32() != 0).i + 1;
        var gravityStep = ball.MapGravityRaw << 8;
        Assert.Equal(
            (
                Series(oracle.Take(ticks).Select(s => s.GetProperty("pz").GetInt32() - 1)),
                Series(oracle.Take(ticks).Select(s => s.GetProperty("collz_140").GetInt32())),
                Series(oracle.Take(ticks).Select(s => s.GetProperty("ground_144").GetInt32())),
                Series(Enumerable.Range(1, ticks).Select(t => t <= landingTick ? -t * gravityStep : -gravityStep)),
                Series(Enumerable.Repeat(OracleInit(root, "pz") - 1, ticks))),
            (Series(z), Series(collided), Series(ground), Series(tickForces), Series(witnessZ)));
    }

    [Fact]
    public void B2_TheArrow_KeepsItsZ_IsOnGroundIsZeroWithTheRawUnitAndOneWithout_AndTheBallOnTheSameFloorLandsOnIt()
    {
        var root = OracleRows("arrows-arrow-h1.json");
        var oracleZ = OracleSlot(root).Select(s => s.GetProperty("pz").GetInt32()).Distinct().Single(); // constant for the whole flight (no gravity).
        using var arc = Arc("b2");
        var floor = Floor(1); // 16 px everywhere: the arrow born 16 px up is flush with the floor plus one raw unit.
        Install(arc, floor);

        var arrow = Born(arc, Arrow, 16 << 16);
        var flush = Born(arc, Arrow, 16 << 16);
        var ball = Born(arc, FireBall, 30 << 16);
        var terrain = arrow.ComputeTerrainHeight();
        flush.PosZ = terrain;

        var arrowZ = new List<int>();
        var arrowGround = new List<int>();
        var flushGround = new List<int>();
        var ballLandings = 0;
        for (var t = 0; t < 30; t++)
        {
            Tick(arrow);
            Tick(flush);
            Tick(ball);
            arrowZ.Add(arrow.PosZ);
            arrowGround.Add(arrow.IsOnGround);
            flushGround.Add(flush.IsOnGround);
            ballLandings += ball.CollidedWithEntityZ;
        }

        Assert.Equal(
            (oracleZ - 1, 0, 1, terrain, true),
            (arrowZ.Distinct().Single(), arrowGround.Max(), flushGround.Min(), ball.PosZ, ballLandings > 0));
    }

    [Fact]
    public void B3_TheImpulseOf1024_FollowsTheSeriesOfTheCrateMinusOneRawUnit_AndLandsAtTheTickOfTheOracle_WhereTheSameEntityWithoutImpulseOnlyFalls()
    {
        var root = OracleRows("crate-flat-state1.json");
        var oracle = OracleSlot(root);
        const int ticks = 26;
        using var arc = Arc("b3");
        Install(arc, FlatCells.Create());

        // The crate is born by a record at 32 px (+1 raw unit); the DLL's frame is the oracle's minus one, and the interface adds the unit of its own to the argument (the birth is z + 1).
        var crateBirth = OracleInit(root, "pz") - 1;
        var thrown = Born(arc, FireBall, crateBirth - 1);
        var dropped = Born(arc, FireBall, crateBirth - 1);
        Assert.Equal((crateBirth, crateBirth), (thrown.PosZ, dropped.PosZ));

        var z = new List<int>();
        var collided = new List<int>();
        var droppedZ = new List<int>();
        for (var t = 0; t < ticks; t++)
        {
            thrown.IsZForceApplied = t == 0 ? 1024 : 0; // the animation's impulse is taken at the first tick and lowered by the validation.
            Tick(thrown);
            Tick(dropped);
            z.Add(thrown.PosZ);
            collided.Add(thrown.CollidedWithEntityZ);
            droppedZ.Add(dropped.PosZ);
        }

        var landing = oracle.Select((s, i) => (s, i)).First(p => p.s.GetProperty("collz_140").GetInt32() != 0).i;
        Assert.Equal(
            (Series(oracle.Take(ticks).Select(s => s.GetProperty("pz").GetInt32() - 1)), landing + 1, true),
            (Series(z), collided.Select((c, i) => (c, i)).First(p => p.c != 0).i + 1, droppedZ.Max() < crateBirth));
    }

    [Fact]
    public void B4_TheExactGraze_DoesNotLandAtOnce_AndLandsAtTheNextTick_WhereAGrazeOneRawUnitDeeperLandsAtOnce()
    {
        using var arc = Arc("b4");
        Install(arc, FlatCells.Create());
        var exact = Born(arc, FireBall, 32 << 16);
        var deeper = Born(arc, FireBall, 32 << 16);
        var rest = exact.ComputeTerrainHeight() - exact.ModZ; // the PosZ at which the foot is on the ground.
        var step = exact.MapGravityRaw << 8;
        foreach (var e in new[] { exact, deeper })
        {
            e.ForceZ = 0;
            e.FinalForceZ = 0;
        }

        exact.PosZ = rest + step;       // PosZ + ModZ + F == the ground exactly: the STRICT test does not hold.
        deeper.PosZ = rest + step - 1;  // one raw unit under: it holds.

        Tick(exact);
        Tick(deeper);
        var first = (exact.PosZ, exact.CollidedWithEntityZ, exact.ForceZ != 0, deeper.PosZ, deeper.CollidedWithEntityZ, deeper.ForceZ);
        Tick(exact);
        var second = (exact.PosZ, exact.CollidedWithEntityZ, exact.ForceZ);

        Assert.Equal(
            ((rest, 0, true, rest, 1, 0), (rest, 1, 0)),
            (first, second));
    }

    [Fact]
    public void B5_TheCeiling_StopsTheRiseAtTheAbsoluteCeiling_WithTheContactAndTheForceKeptWithoutGravity_ClearedWithIt_AndALowerRiseGoesOn()
    {
        using var arc = Arc("b5");
        Install(arc, FlatCells.Create());
        var arrow = Born(arc, Arrow, 16 << 16);   // no gravity.
        var ball = Born(arc, FireBall, 16 << 16); // gravity.
        var low = Born(arc, FireBall, 16 << 16);
        var rise = 0x100000;                       // 16 px per tick.
        foreach (var e in new[] { arrow, ball, low })
        {
            e.ForceZ = rise;
            e.FinalForceZ = rise;
        }

        ball.IsZForceApplied = rise >> 8; // with gravity the law would decay the force: an impulse sets it (and it is the first tick of its life).
        low.IsZForceApplied = rise >> 8;
        arrow.PosZ = EntitySupport.AbsoluteCeiling - arrow.ModZ - arrow.Depth - 1 - rise / 2; // the target goes past the ceiling.
        ball.PosZ = EntitySupport.AbsoluteCeiling - ball.ModZ - ball.Depth - 1 - rise / 2;
        var lowStart = low.PosZ;

        Tick(arrow);
        Tick(ball);
        Tick(low);
        var ceilingPosZ = EntitySupport.AbsoluteCeiling - 2 - arrow.ModZ - arrow.Depth;

        Assert.Equal(
            (ceilingPosZ, 1, rise, ceilingPosZ, 1, 0, lowStart + rise, 0),
            (arrow.PosZ, arrow.CollidedWithEntityZ, arrow.ForceZ, ball.PosZ, ball.CollidedWithEntityZ, ball.ForceZ, low.PosZ, low.CollidedWithEntityZ));
    }

    [Fact]
    public void B6_TheMagnet_SnapsAtMostThreePixelsAboveAndThreePixelsPlusOneUnitBelow_OnlyWithGravityForceZeroAndAnAcceptedXyStep()
    {
        using var arc = Arc("b6");
        Install(arc, Floor(1)); // the terrain is 16 px everywhere.
        var ball = Born(arc, FireBall, 20 << 16);
        var terrain = ball.ComputeTerrainHeight();

        int Run(int posZ, Action<EntityScriptProxy>? setup = null)
        {
            ball.PosZ = posZ;
            ball.ForceZ = 0;
            ball.FinalForceX = 0x10000;
            ball.FinalForceY = 0;
            ball.LastTickDeltaX = 0x10000;
            ball.LastTickDeltaY = 0;
            setup?.Invoke(ball);
            ScriptedMotion.RunLogicalZPostXyStep(ball);
            return ball.PosZ - terrain;
        }

        int RunGround(int posZ)
        {
            // A box with no Z offset, so that the foot at the terrain is on the ground (PosZ + ModZ <= floor); the order of the snap and of the derivation is what the case pins.
            // (The case before it, "NoGravity", leaves the gravity flag down on the shared entity.)
            var modZ = ball.ModZ;
            ball.ModZ = 0;
            ball.Flags |= EntityFlags.Gravity;
            Run(posZ);
            ball.ModZ = modZ;
            return ball.IsOnGround;
        }

        // Terrain above the foot by 3 px + 1 unit: posed on it; 3 px + 2 units: not. Terrain below the foot by 3 px: posed; by 3 px + 1 unit: not (the DLL's frame of the thresholds 0x30000 and 0x30001).
        var results = (
            Up3: Run(terrain - 0x30001),
            Up3More: Run(terrain - 0x30002),
            Up4: Run(terrain - 0x40000),
            Down3: Run(terrain + 0x30000),
            Down3More: Run(terrain + 0x30001),
            ForceZ: Run(terrain - 0x20000, e => e.ForceZ = -1),
            NoXyForce: Run(terrain - 0x20000, e => { e.FinalForceX = 0; e.FinalForceY = 0; }),
            NoStep: Run(terrain - 0x20000, e => { e.LastTickDeltaX = 0; e.LastTickDeltaY = 0; }),
            NoGravity: Run(terrain - 0x20000, e => e.Flags &= ~EntityFlags.Gravity),
            // IsOnGround is derived AFTER the snap (E20.e0k2 W1, the binary's order): the foot posed by the magnet is on the ground, the foot left 3 px + 1 unit above it is not.
            Down3Ground: RunGround(terrain + 0x30000),
            Down3MoreGround: RunGround(terrain + 0x30001));

        Assert.Equal(
            (Up3: 0, Up3More: -0x30002, Up4: -0x40000, Down3: 0, Down3More: 0x30001, ForceZ: -0x20000, NoXyForce: -0x20000, NoStep: -0x20000, NoGravity: -0x20000, Down3Ground: 1, Down3MoreGround: 0),
            results);
    }

    [Fact]
    public void B7_TheStopMarker0x8000_ZeroesTheForceWithoutGravity_WhereWithGravityItIsTheImpulseShiftedByEight()
    {
        using var arc = Arc("b7");
        Install(arc, FlatCells.Create());
        var arrow = Born(arc, Arrow, 16 << 16);
        var ball = Born(arc, FireBall, 16 << 16);
        arrow.ForceZ = 0x40000;
        arrow.FinalForceZ = 0x40000;
        arrow.IsZForceApplied = 0x8000;
        ball.IsZForceApplied = 0x8000;

        Tick(arrow);
        Tick(ball);

        Assert.Equal((0, 0x8000 << 8), (arrow.ForceZ, ball.ForceZ));
    }

    [Fact]
    public void B8_TheStep_DeclaresTheSentinelAndThePositionTheRootFollows_ThroughAVerticalMoveOnly_AndTheHeroIsLeftAlone()
    {
        using var arc = Arc("b8");
        Install(arc, FlatCells.Create());
        var ball = Born(arc, FireBall, 32 << 16);
        var latch = typeof(CharacterControllerComponent).GetField("_externalVerticalDisplacement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var heroZ = arc.Hero.PosZ;

        var heroRan = ScriptedMotion.RunLogicalZPreXyStep(arc.Hero);
        var rootComponent = ArcRun.EntityOf(ball)!.RootComponent!;
        var rootBefore = rootComponent.LocalTransform.Position;
        Tick(ball);
        Tick(ball);
        var root = rootComponent.LocalTransform.Position;

        Assert.Equal(
            (false, heroZ, ScriptedMotion.AirborneExternalDisplacementSentinel, true, rootBefore.X, rootBefore.Y),
            (heroRan, arc.Hero.PosZ, (float)latch.GetValue(ball.Controller!)!, Math.Abs(root.Z - ball.PosZ / 65536f) < 0.001f, root.X, root.Y));
    }
}
