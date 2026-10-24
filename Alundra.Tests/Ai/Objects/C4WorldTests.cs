#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c1 T2 (docs/plan-e20-heros.md section 1.24.5, phase B): the tests of the world of C4 (<c>0x80066984</c>), in the REAL world of the exported maps (<see cref="C4Run"/>: real prefabs, the real records of the objects born by
/// <c>SpawnEntityByRecordId</c>, the real hero controller, the effect stage, the sound player), the object put in the hands by <see cref="PlayerManager.PutInHands"/> (the lift itself is phase C). T is the tick of the front of the Square (the tick 1 of the run);
/// every expected value is the binary's (<c>docs/plan-e20-annexe/e20-g2c1-disc/g2c1/out/</c>: <c>q8_p1</c>, <c>q3_corners</c>, <c>s1_flat</c>, <c>s6_touch</c>, <c>q8_p4</c>, <c>s7_stack</c>, <c>q8_p5</c>), the ones the T0 probes of the DLL measured equal
/// (<c>docs/plan-e20-annexe/e20-g2c1/probes/out/</c>). NOTHING is registered by the tests: the production registry alone decides, so (m1) to (m7), (m9) and (m10) are red until the key <c>(C, 4)</c> is in it; (m8) and (m11) are witnesses, green before and after.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class C4WorldTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const uint Right = PadState.Right;
    private const double Px = 65536.0;

    public C4WorldTests()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    /// <summary>The seven types of C4: the type, a real record of the exported maps (zone, world, record) and whether its box is the wide one (21 x 15 x 16: the barrel, the stone, the trunk) or the narrow one of the objects (20 x 12 x 14).</summary>
    internal static readonly (string Name, int Type, string Zone, string World, int Record, bool Wide)[] Seven =
    {
        ("barrel22r21", 290, "Lars' Crypt", "Lars' Crypt-22", 21, true),
        ("stone24r24", 293, "Lars' Crypt", "Lars' Crypt-24", 24, true),
        ("star27r5", 363, "Ancient Shrine", "Ancient Shrine-27", 5, false),
        ("moon27r6", 367, "Ancient Shrine", "Ancient Shrine-27", 6, false),
        ("water27r7", 368, "Ancient Shrine", "Ancient Shrine-27", 7, false),
        ("sun27r8", 369, "Ancient Shrine", "Ancient Shrine-27", 8, false),
        ("trunk70r8", 373, "Coastal Cave", "Coastal Cave-70", 8, true),
    };

    public static TheoryData<int, bool> SevenMovingOrStill()
    {
        var data = new TheoryData<int, bool>();
        for (var i = 0; i < Seven.Length; i++)
        {
            data.Add(i, true);
            data.Add(i, false);
        }

        return data;
    }

    public static TheoryData<int> SevenIndexes()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Seven.Length; i++)
        {
            data.Add(i);
        }

        return data;
    }

    private static C4Run Build(int index, Func<int, int, CutBench.Cell>? cell = null, int? targetX = null, int targetIndex = 79, (int Index, int X, int Y)? plate = null, int? restRecord = null)
    {
        var o = Seven[index];
        return new C4Run(o.Name, o.Zone, o.World, 0, cell: cell, recordId: o.Record, targetX: targetX, targetIndex: targetIndex, plate: plate, restRecord: restRecord, contents: 0);
    }

    private static C4Run.CrateTick At(C4Run run, int k) => run.Ticks[k + 1];

    private static double X(C4Run.CrateTick t) => t.X / Px;

    private static double Z(C4Run.CrateTick t) => t.Z / Px;

    private static uint Pad(bool moving, int tick) => tick == 1 ? (moving ? Square | Right : Square) : 0u;

    private static List<int> Sfx24(C4Run run) => run.Heard.Played.Where(p => p.Sfx == 0x18).Select(p => p.Tick - 1).ToList();

    // -----------------------------------------------------------------------------------------
    // (m1) the flat throw of the seven types: the contact with the ground at T+22, the sound 24 and the animation 0 at T+23, the flags unchanged.
    // (s1_flat: land t83 = T+22, 't84 anim_t 1>0' with snd(24), final x 178 moving / 143.5 still, flags unchanged)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SevenMovingOrStill))]
    public void M1_TheFlatThrow_TouchesTheGroundAtT22_TheSound24AndTheAnimation0AtT23_TheFlagsUnchanged(int index, bool moving)
    {
        using var run = Build(index);
        var flags = run.Crate.Flags;
        run.Go(1, 40, tick => Pad(moving, tick));

        var flight = moving ? 1u : 3u;
        var thrown = At(run, 0);
        Assert.Equal((flight, 24u, false, 0), (thrown.Target, thrown.Dir, thrown.Carried, thrown.Flags2));
        for (var k = 0; k <= 22; k++)
        {
            var t = At(run, k);
            Assert.True(t.Flags == flags && t.Status == EntityStatus.Normal && t.Target == flight, $"{Seven[index].Name} T+{k}: {t}");
            Assert.True(k < 22 ? t.OnGround == 0 : t.OnGround == 1, $"{Seven[index].Name} T+{k}: ground {t.OnGround}");
        }

        var landed = At(run, 22);
        Assert.Equal((moving ? 178.0 : 143.5, 0.0), (X(landed), Z(landed)));

        // T+23: the sound 24 once, the animation 0 asked; nothing else moves, the flags stay, the object stays in the world.
        Assert.Equal(new[] { 23 }, Sfx24(run));
        for (var k = 23; k <= 39; k++)
        {
            var t = At(run, k);
            Assert.True(t.Target == 0 && t.Flags == flags && t.Present && t.Status == EntityStatus.Normal && t.Adj == 0, $"{Seven[index].Name} T+{k}: {t}");
            Assert.Equal(X(landed), X(t));
        }

        Assert.Empty(run.Effects9);
    }

    // -----------------------------------------------------------------------------------------
    // (m2) the rebound (P1): thrown against a wall in the column 5 while it goes up, the object turns half a turn, takes the lock and makes the effect 9 8 px above it (q8_p1.txt); the wall in the column 6: no rebound.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SevenMovingOrStill))]
    public void M2_TheWallInTheColumn5_TheObjectBouncesOnce_TheDirectionHalfATurn_TheLock_TheEffect9(int index, bool moving)
    {
        var o = Seven[index];
        using var run = Build(index, cell: (cx, _) => new CutBench.Cell(cx == 5 ? 0x40 : 0, 0));
        run.Go(1, 12, tick => Pad(moving, tick));

        // the wide ones touch the wall at T (ForceAdjusted at the end of T), bounce at T+1; the objects, narrower, one tick later (q8_p1: tonneau34, tronc117, etoile107).
        var bounce = o.Wide ? 1 : 2;
        Assert.Equal(o.Wide ? 1 : 0, At(run, 0).Adj);
        for (var k = 0; k < bounce; k++)
        {
            Assert.True(At(run, k).Dir == 24 && At(run, k).Lock == 0, $"{o.Name} T+{k} before the bounce: {At(run, k)}");
        }

        var after = At(run, bounce);
        Assert.Equal((8u, 1, 0), (after.Dir, after.Lock, after.Adj));
        Assert.Single(run.Effects9);
        var effect = run.Effects9[0];
        Assert.Equal(bounce + 1, effect.Tick);
        Assert.Equal(o.Wide ? (109.0, 100.0, 44.0) : (110.0, 100.0, 47.5), (effect.X, effect.Y, effect.Z));

        // the object leaves the wall, going away to the west, and keeps the lock and the new direction.
        if (o.Wide)
        {
            Assert.Equal(moving ? 106.0 : 107.5, X(after));
            Assert.Equal(40.0, Z(after));
        }
        else if (moving)
        {
            Assert.Equal(107.0, X(after));
        }

        for (var k = bounce; k <= 8; k++)
        {
            Assert.True(At(run, k).Dir == 8 && At(run, k).Lock == 1 && X(At(run, k)) < 109.0, $"{o.Name} T+{k}: {At(run, k)}");
        }
    }

    [Theory]
    [MemberData(nameof(SevenIndexes))]
    public void M2_TheWallInTheColumn6_NoRebound_TheObjectStopsAgainstTheWall_ThenFallsAndLands(int index)
    {
        var o = Seven[index];
        using var run = Build(index, cell: (cx, _) => new CutBench.Cell(cx == 6 ? 0x40 : 0, 0));
        var flags = run.Crate.Flags;
        run.Go(1, 30, tick => Pad(true, tick));

        Assert.Empty(run.Effects9);
        for (var k = 0; k <= 24; k++)
        {
            var t = At(run, k);
            Assert.True(t.Dir == 24 && t.Lock == 0 && t.Flags == flags, $"{o.Name} T+{k}: {t}");
        }

        // q8_p1_col6.txt: the wide ones stop at 133, the objects at 134; ForceAdjusted from T+8 (T+9 for the objects) to the landing at T+22.
        Assert.Equal(o.Wide ? 133.0 : 134.0, X(At(run, 24)));
        Assert.Equal(1, At(run, 20).Adj);
        Assert.Equal(1, At(run, 22).OnGround);
        Assert.Equal(0u, At(run, 24).Target);
    }

    // -----------------------------------------------------------------------------------------
    // (m3) the glide (P2): the cells 0x1001 in the columns 6 to 9: 3 px per tick from T+22 (x = 178) to T+46 (x = 250), the animation 0 at T+47, the sound 24 at each sliding tick (q3_corners.txt).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SevenIndexes))]
    public void M3_TheBandOf0x1001_TheObjectGlidesThreePixelsPerTick_UntilT46_TheAnimation0AtT47(int index)
    {
        var o = Seven[index];
        using var run = Build(index, cell: (cx, _) => cx >= 6 && cx <= 9 ? new CutBench.Cell(0x01, 0, null, 0x10) : new CutBench.Cell(0, 0));
        run.Go(1, 56, tick => Pad(true, tick));

        Assert.Equal(178.0, X(At(run, 22)));
        for (var k = 22; k <= 46; k++)
        {
            var t = At(run, k);
            Assert.True(t.Target == 1 && t.OnGround == 1 && t.Status == EntityStatus.Normal && X(t) == 178.0 + 3 * (k - 22), $"{o.Name} T+{k}: {t}");
        }

        Assert.Equal(250.0, X(At(run, 46)));
        Assert.Equal((0u, 250.0), (At(run, 47).Target, X(At(run, 47))));
        Assert.Equal((0u, 250.0), (At(run, 52).Target, X(At(run, 52))));

        // the sound 24 at each tick of the glide, T+23 to T+47 (a tick after the move that asked it), once per tick.
        var sounds = Sfx24(run);
        Assert.Equal(Enumerable.Range(23, 25).ToList(), sounds);
    }

    // -----------------------------------------------------------------------------------------
    // (m4) the swimming water (the word 0x0800): the barrel, the stone and the objects are destroyed at T+23 (free at T+24), the trunk floats (the animation 5 at T+23) (report-main.md, "Tonneau, tronc et objet ... T+23").
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SevenIndexes))]
    public void M4_TheDeepWater_TheBarrelTheStoneAndTheObjectsAreDestroyedAtT23_TheTrunkFloats(int index)
    {
        var o = Seven[index];
        using var run = Build(index, cell: (cx, _) => cx >= 6 && cx <= 9 ? new CutBench.Cell(0x00, 0, null, 0x08) : new CutBench.Cell(0, 0));
        run.Go(1, 40, tick => Pad(true, tick));

        Assert.Equal(1, At(run, 22).OnGround);
        if (o.Type == 373)
        {
            // the trunk is not destroyed: it floats.
            Assert.Equal(4, At(run, 22).Slope);
            for (var k = 23; k <= 39; k++)
            {
                Assert.True(At(run, k).Present && At(run, k).Status == EntityStatus.Normal && At(run, k).Target == 5, $"{o.Name} T+{k}: {At(run, k)}");
            }

            return;
        }

        Assert.True(At(run, 22).Present && At(run, 22).Status == EntityStatus.Normal, $"{o.Name} T+22: {At(run, 22)}");
        Assert.True(At(run, 23).Present && At(run, 23).Status == EntityStatus.FlagToDestroy, $"{o.Name} T+23: {At(run, 23)}");
        Assert.False(At(run, 24).Present, $"{o.Name} T+24: {At(run, 24)}");
    }

    // -----------------------------------------------------------------------------------------
    // (m5) the touch (P3): the barrel, the stone and the trunk thrown at a hittable sprite of their own page hit it (HitCounter 1, a spark), then the status 3 at the next tick with the flags 0x3323E4 -> 0x3323C4
    // (the trunk 0x2323E4 -> 0x2323C4), then the break (E2: the status 4 two ticks later, the slot free at the next); the star object touches nothing.
    // (s6_touch: 't82 hitc 0>1', 't83 status 2>3 flags E4>C4', 't85 status 3>4', 't86 status 4>0'; the DLL, T0 P3: the times depend on the size of the target.)
    // -----------------------------------------------------------------------------------------

    /// <summary>The target of the page of each object (the sprite index of its own map page) and the tick of the touch measured at T0 (the DLL equal to the binary where both exist).</summary>
    public static TheoryData<int, int, int> TouchCases() => new()
    {
        { 0, 49, 19 }, // the barrel of the map 22 against the sprite 49 of its page (type 305).
        { 1, 79, 21 }, // the stone of the map 24 against the Beannoid (index 79), the binary's own scene: the touch at T+21.
        { 6, 118, 16 }, // the trunk of the map 70 against the sprite 118 (type 374).
    };

    [Theory]
    [MemberData(nameof(TouchCases))]
    public void M5_TheBarrelTheStoneTheTrunk_TouchTheTarget_TheStatus3WithTheFlagsC4_ThenTheBreak(int index, int targetIndex, int touchK)
    {
        var o = Seven[index];
        using var run = Build(index, targetX: 165, targetIndex: targetIndex);
        var flags = run.Crate.Flags;
        run.Go(1, 40, tick => Pad(true, tick));

        // the touch: one spark, the HitCounter of the object at 1 at the end of the tick, nothing before.
        Assert.Equal(touchK + 1, run.Sparks.Where(p => p.Value != 0).Select(p => p.Key).Single());
        Assert.Equal(1, At(run, touchK).HitCounter);
        Assert.True(Enumerable.Range(0, touchK).All(k => At(run, k).HitCounter == 0 && At(run, k).Flags == flags), $"{o.Name}: a touch before T+{touchK}");

        // T+touch+1: the status 3 (Deactivated), the animation 2 asked by E2, the flags with the bits 0x20 and 0x10 cleared ... (flags 0x3323E4 -> 0x3323C4 is the touch's, E2 then writes (f | 0x40) & ~0x30).
        var deactivated = At(run, touchK + 1);
        Assert.Equal(EntityStatus.Deactivated, deactivated.Status);
        Assert.Equal(2u, deactivated.Target);
        Assert.Equal(flags & ~0x20u, deactivated.Flags);

        // E2 destroys it at its third call, two ticks after the first, and the slot is free at the next tick.
        Assert.Equal(EntityStatus.FlagToDestroy, At(run, touchK + 3).Status);
        Assert.True(At(run, touchK + 3).Present);
        Assert.False(At(run, touchK + 4).Present);

    }

    [Fact]
    public void M5_TheStarObject_TouchesNothing_FlatThrow_FlagsUnchanged()
    {
        // the object of the map 27 against the sprite 169 of its page (a mummy): no spark in the binary's scene (s6_touch: etoile107 'target touched at tNone').
        using var run = Build(2, targetX: 165, targetIndex: 169);
        var flags = run.Crate.Flags;
        run.Go(1, 40, tick => Pad(true, tick));

        Assert.Equal(0, run.Sparks.Values.Sum());
        Assert.True(Enumerable.Range(0, 40).All(k => At(run, k).HitCounter == 0 && At(run, k).Flags == flags && At(run, k).Status == EntityStatus.Normal), "the star object touched something");
        Assert.Equal(1, At(run, 22).OnGround);
        Assert.Equal(0u, At(run, 23).Target);
    }

    // -----------------------------------------------------------------------------------------
    // (m6) the plate and the pile (P4): the object of the map 27 thrown onto the plate (sprite 108 at (178, 100)) lands on it at T+20 (x = 172, z = 16) and the support appears one tick later (q8_p4.txt);
    // a second object thrown onto the first lands on it (x = 163, z = 30) and the first keeps the plate (s7_stack.txt).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void M6_TheObjectOnThePlate_LandsAtT20_TheSupportAtT21_StaysThere()
    {
        using var run = Build(2, plate: (108, 178, 100));
        run.Go(1, 45, tick => Pad(true, tick));

        var landed = At(run, 20);
        Assert.Equal((172.0, 16.0, 1, false), (X(landed), Math.Round(Z(landed), 3), landed.OnGround, landed.OnPlate)); // the DLL's top of a box is one raw unit (1/65536 px) above the binary's: rounded to the millipixel
        for (var k = 21; k <= 44; k++)
        {
            var t = At(run, k);
            Assert.True(t.OnPlate && t.OnGround == 1 && X(t) == 172.0 && Math.Round(Z(t), 3) == 16.0 && t.Target == 0 && t.Status == EntityStatus.Normal, $"T+{k}: {t}");
        }
    }

    [Fact]
    public void M6_TheSecondObjectThrownOntoTheFirst_LandsOnItAtXZ163And30_TheFirstKeepsThePlate()
    {
        using var run = Build(2, plate: (108, 178, 100), restRecord: 31);
        run.Go(
            1, 120,
            tick => tick == 1 ? Square | Right : (tick == 90 ? Square | Right : 0u),
            tick =>
            {
                if (tick == 80)
                {
                    var rest = run.Rest!;
                    rest.PosX = run.Arc.Hero.PosX;
                    rest.PosY = run.Arc.Hero.PosY;
                    rest.PosZ = 32 << 16;
                    rest.PushLogicalPositionToRoot();
                    rest.RidingEntity = run.Arc.Hero.LogicContextEntity;
                    PlayerManager.PutInHands(run.Arc.Hero, rest);
                }
            });

        // the first stays on the plate for ever; the second lands on the first (x 163, z 30), the support one tick after the landing.
        Assert.True(Enumerable.Range(22, 98).All(t => run.Ticks[t].OnPlate && run.Ticks[t].OnGround == 1), "the first object left the plate");
        var second = run.RestTicks;
        Assert.Equal((163.0, 30.0, 1, true), (second[120].X, Math.Round(second[120].Z, 3), second[120].Ground, second[120].OnCrate));
        var landing = Enumerable.Range(91, 29).First(t => second[t].Ground == 1);
        Assert.Equal(107, landing);
        Assert.False(second[landing].OnCrate);
        Assert.True(second[landing + 1].OnCrate);
    }

    // -----------------------------------------------------------------------------------------
    // (m7) the floating trunks at the map load (P5): the five trunks on the water of the map 70 and the ones of the map 73 float (the animation 5) with the slope 4 of the water from the first Normal tick, at the first run of C4 (the tick 2
    // of a map-loaded entity: the first tick runs its program A, as the binary's pick phase does); the trunk out of the water (70 rec 8) stays at the animation 0, the slope 0.
    // -----------------------------------------------------------------------------------------

    private static (EntityScriptProxy[] Trunks, ArcRun Arc) LoadTrunks(string world, int tx, int ty)
    {
        var spec = new ArcSpec("g2c1-m7-" + world + "-" + tx, "Coastal Cave", world, Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true, Arrival: new ArcArrival((tx * 24 + 12) << 16, (ty * 16 + 8) << 16, 3 << 20, GameState.ResetAnimationId, 0));
        var arc = new ArcRun(spec);
        var trunks = arc.Entities.Where(e => e.SpriteType == 373).ToArray();
        return (trunks, arc);
    }

    [Theory]
    [InlineData("Coastal Cave-70", 29, 24)]
    [InlineData("Coastal Cave-70", 31, 22)]
    public void M7_TheFloatingTrunksOfTheMap70_TheSlope4AtTheFirstNormalTick_TheAnimation5AtTheFirstRunOfC4_TheTrunkOutOfTheWaterStays(string world, int tx, int ty)
    {
        var (trunks, arc) = LoadTrunks(world, tx, ty);
        using var arcScope = arc;
        Assert.Equal(6, trunks.Length);
        Assert.All(trunks, t => Assert.Equal(EntityStatus.Loaded, t.Status));

        // frame 1: the load program ran, the trunks are Normal, the slope is fed (the hook of f6a), C4 has not run yet.
        Frame(arc);
        var counts = arc.Runner.NativeHandlerRunCounts;
        Assert.Equal(0, counts.GetValueOrDefault((Alundra.Events.ScriptHelper.ProgramCTick, LiftableAi.C4Code)));
        Assert.All(trunks, t => Assert.Equal(EntityStatus.Normal, t.Status));
        var outOfWater = trunks.Single(t => t.PosX >> 16 == 780);
        var floating = trunks.Where(t => !ReferenceEquals(t, outOfWater)).ToArray();
        Assert.Equal(5, floating.Length);
        Assert.All(floating, t => Assert.Equal(4, t.Slope_18c));
        Assert.Equal(0, outOfWater.Slope_18c);

        // frame 2: the first run of C4: the animation 5 asked for the five on the water, the animation 0 for the other.
        Frame(arc);
        Assert.Equal(6, counts.GetValueOrDefault((Alundra.Events.ScriptHelper.ProgramCTick, LiftableAi.C4Code)));
        Assert.All(floating, t => Assert.Equal(5u, t.TargetAnimationId));
        Assert.Equal((0u, 0), (outOfWater.TargetAnimationId, outOfWater.Slope_18c));

        // and they stay so.
        for (var i = 0; i < 4; i++)
        {
            Frame(arc);
        }

        Assert.All(floating, t => Assert.Equal((5u, 4), (t.TargetAnimationId, t.Slope_18c)));
        Assert.Equal(0u, outOfWater.TargetAnimationId);
    }

    [Theory]
    [InlineData(16, 45, 396, 728)]
    [InlineData(16, 45, 396, 792)]
    [InlineData(41, 44, 996, 712)]
    public void M7_TheFloatingTrunksOfTheMap73_TheSlope4_TheAnimation5AtTheFirstRunOfC4(int tx, int ty, int px, int py)
    {
        var (trunks, arc) = LoadTrunks("Coastal Cave-73", tx, ty);
        using var arcScope = arc;
        var trunk = trunks.Single(t => t.PosX >> 16 == px && t.PosY >> 16 == py);
        Assert.Equal(EntityStatus.Loaded, trunk.Status);
        Frame(arc);
        Assert.Equal((EntityStatus.Normal, 4), (trunk.Status, trunk.Slope_18c));
        Frame(arc);
        Assert.Equal(5u, trunk.TargetAnimationId);
        Frame(arc);
        Assert.Equal(5u, trunk.TargetAnimationId);
    }

    // -----------------------------------------------------------------------------------------
    // (m8) WITNESS, green before and after: the stone of the map 444 (record 25 at (972, 376, 64)) on the trap door (record 24) that goes down follows it in the very tick (D5b, ADR-0088): the passengers loop of ScriptedMotion, not C4.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void M8_Witness_TheStoneOfTheMap444OnTheTrapDoorThatGoesDown_FollowsItInTheSameTick()
    {
        // the hero arrives away from the stone (under it he would be its carrier).
        var spec = new ArcSpec("g2c1-m8", "Torla", "Torla (inner)-444", Array.Empty<int>(), 0, 0, 0, 2000, RealController: true, Prefabs: true, Arrival: new ArcArrival(1020 << 16, 392 << 16, 2 << 20, GameState.ResetAnimationId, 8));
        using var arc = new ArcRun(spec);
        for (var i = 0; i < 6; i++)
        {
            Frame(arc);
        }

        var door = arc.EntityByRecord(24)!;
        var stone = arc.EntityByRecord(25)!;
        Assert.Equal((328, 293), (door.SpriteType, stone.SpriteType));
        Assert.Same(door.LogicContextEntity, stone.RidingEntity);
        var startDoor = door.PosZ;
        var startStone = stone.PosZ;
        for (var f = 1; f <= 14; f++)
        {
            if (f == 3)
            {
                door.ForceZ = -65536;
            }

            if (f == 9)
            {
                door.ForceZ = 0;
            }

            Frame(arc);
            var down = f < 3 ? 0 : Math.Min(f - 2, 6);
            Assert.True(door.PosZ == startDoor - down * 65536, $"f{f}: door {door.PosZ - startDoor}");
            Assert.True(stone.PosZ == startStone - down * 65536, $"f{f}: stone {stone.PosZ - startStone}");
            Assert.True(ReferenceEquals(door.LogicContextEntity, stone.RidingEntity) && stone.IsOnGround == 1, $"f{f}: not carried");
        }
    }

    // -----------------------------------------------------------------------------------------
    // (m9) the ice block of the map 103 (record 7, C23 in its header, C4 after the `0x41 [C, 4]` of its load program): lifted (put in the hands) and thrown, it is released by C4: the flat chronology of (m1) and the flags UNCHANGED
    // (C23 would write |0x30), which also tells the C4 of the script from the C23 of the header.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void M9_TheIceBlockOfTheMap103_ThrownIsReleasedByC4_FlagsUnchanged_LandsAtT22_TheSound24AtT23()
    {
        using var run = new C4Run("ice", "Giles Nightmare", "Giles Nightmare-103", 0, recordId: 7, extraTypes: new[] { 304 }, contents: 0);
        Assert.Equal(304, run.Crate.SpriteType);
        var flags = run.Crate.Flags;
        run.Go(1, 40, tick => Pad(true, tick));

        Assert.Equal((1u, 24u, false, 0), (At(run, 0).Target, At(run, 0).Dir, At(run, 0).Carried, At(run, 0).Flags2));
        for (var k = 0; k <= 22; k++)
        {
            var t = At(run, k);
            Assert.True(t.Flags == flags && t.Status == EntityStatus.Normal && t.Target == 1 && t.Present, $"T+{k}: {t}");
            Assert.True(k < 22 ? t.OnGround == 0 : t.OnGround == 1, $"T+{k}: ground {t.OnGround}");
        }

        Assert.Equal((178.0, 0.0), (X(At(run, 22)), Z(At(run, 22))));
        Assert.Equal(new[] { 23 }, Sfx24(run));
        for (var k = 23; k <= 39; k++)
        {
            Assert.True(At(run, k).Target == 0 && At(run, k).Flags == flags && At(run, k).Present, $"T+{k}: {At(run, k)}");
        }

        Assert.Equal(0, At(run, 39).C23Calls);
        Assert.True(At(run, 39).C4Calls > 0);
    }

    // -----------------------------------------------------------------------------------------
    // (m10) the shop of Inoa (map 258): the herb (record 3, type 66, whose native C the script of the map changes to 4) put in the hands of the hero standing on the floor and thrown to the west lands on the counter (165, 680, z 32): the
    // program B[2] of the map sees it at rest in the box of the counter and opens the dialogue of the price (and takes the control from the hero). Thrown less far (still), it lands on the floor (178, 680, z 16), outside the counter: no dialogue.
    // The exit guard (0xB2) and Lurvy's loop (0x61) are not on this path.
    // -----------------------------------------------------------------------------------------

    private static (ArcRun Arc, EntityScriptProxy Herb) ShopWithTheHerbInTheHands(int heroX)
    {
        var spec = new ArcSpec("g2c1-m10", "Inoa", "Inoa (inner)-258", Array.Empty<int>(), 0, 0, 0, 2000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(heroX << 16, 680 << 16, 1 << 20, GameState.ResetAnimationId, 8));
        var arc = new ArcRun(spec);
        for (var i = 0; i < 6; i++)
        {
            Frame(arc);
        }

        var herb = arc.EntityByRecord(3)!;
        herb.PosX = arc.Hero.PosX;
        herb.PosY = arc.Hero.PosY;
        herb.PosZ = arc.Hero.PosZ + (32 << 16);
        herb.PushLogicalPositionToRoot();
        herb.RidingEntity = arc.Hero.LogicContextEntity;
        PlayerManager.PutInHands(arc.Hero, herb);
        for (var i = 0; i < 8; i++)
        {
            Frame(arc);
        }

        arc.Hero.TargetDirection = 8;
        return (arc, herb);
    }

    [Fact]
    public void M10_TheHerbThrownOnTheCounterOfInoa_OpensTheDialogueOfThePrice()
    {
        var (arc, herb) = ShopWithTheHerbInTheHands(228);
        using var arcScope = arc;
        Assert.Equal(66, herb.SpriteType);
        Assert.Equal((12u, true), (arc.Hero.TargetAnimationId, arc.Hero.CarriedEntity != null));
        Assert.False(DialogueDirector.Instance.IsOpen);

        var opened = 0;
        for (var f = 1; f <= 60 && opened == 0; f++)
        {
            arc.HoldDirections(f == 1 ? PadState.Square | PadState.Left : 0u);
            arc.OneFrame();
            if (DialogueDirector.Instance.IsOpen)
            {
                opened = f;
            }
        }

        // the herb lands on the counter (the cell of height 2: z 32) and the dialogue opens in the frame of its landing.
        Assert.InRange(opened, 20, 26);
        Assert.Equal((165.0, 680.0, 32.0, 1), (herb.PosX / Px, herb.PosY / Px, Math.Round(herb.PosZ / Px, 3), herb.IsOnGround));
        Assert.Null(arc.Hero.CarriedEntity);
        Assert.True((GameState.Instance.PlayerControlFlags & GameState.PlayerControlBits.ControlLocked) != 0, "the script took the control from the hero");
    }

    [Fact]
    public void M10_TheHerbThrownShortOfTheCounter_LandsOnTheFloor_NoDialogue()
    {
        var (arc, herb) = ShopWithTheHerbInTheHands(212);
        using var arcScope = arc;
        for (var f = 1; f <= 80; f++)
        {
            arc.HoldDirections(f == 1 ? PadState.Square : 0u);
            arc.OneFrame();
            Assert.False(DialogueDirector.Instance.IsOpen, $"f{f}: a dialogue is open");
        }

        // the still throw: half the distance, on the floor (z 16).
        Assert.Equal((178.0, 680.0, 16.0, 1), (herb.PosX / Px, herb.PosY / Px, Math.Round(herb.PosZ / Px, 3), herb.IsOnGround));
        Assert.Null(arc.Hero.CarriedEntity);
    }

    // -----------------------------------------------------------------------------------------
    // (m11) WITNESS, green before and after: at rest, the placements of the seven C4 types whose native C is C4 at the load (135 in the export; 117 are born at the load of their map, the other 18 have the bit 0x40 of their direction clear and are born by a script only: the table below is the list) stay inert for 6 ticks on their real cells:
    // every field of the proxy is the same (but the clocks every entity advances), and a placement that floats (the eight trunks of the maps 70 and 73, (m7)) is the only one that changes. The 43 scripted trunks and the 13 scripted objects never run C4.
    // -----------------------------------------------------------------------------------------

    private static readonly string[] Clocks = { "field AnimCompleteCounter", "field BoxTicks", "field FrameCounter", "field MotionTickCount", "field PhysicsDoneInPass", "field SupportTickCount" };

    /// <summary>The 117 placements: (map, zone, world, (record, type, x, y, z in pixels, the tile box of the zone of the record: the placement is loaded when the hero stands in it)).</summary>
    internal static readonly (int Map, string Zone, string World, (int Record, int Type, int X, int Y, int Z, int TileMinX, int TileMaxX, int TileMinY, int TileMaxY)[] Placements)[] NativePlacements =
    {
        (107, "Giles Nightmare", "Giles Nightmare-107", new[] { (1, 290, 588, 168, 16, 22, 37, 4, 21), (2, 290, 732, 184, 16, 22, 37, 4, 21) }),
        (113, "Nestus' Dream", "Nestus' Dream-113", new[] { (10, 290, 156, 328, 48, 0, 12, 0, 59), (11, 290, 204, 520, 48, 0, 12, 0, 59) }),
        (114, "Tarn's Manor", "Tarn's Manor-114", new[] { (22, 290, 1140, 760, 64, 38, 51, 30, 59) }),
        (116, "Tarn's Manor", "Tarn's Manor-116", new[] { (5, 290, 144, 696, 0, 0, 23, 32, 59), (15, 290, 144, 824, 0, 0, 23, 32, 59), (19, 290, 264, 696, 0, 0, 23, 32, 59), (20, 290, 1092, 808, 32, 0, 7, 0, 27), (21, 290, 1116, 808, 32, 0, 7, 0, 27), (23, 290, 264, 824, 0, 0, 23, 32, 59) }),
        (122, "Kline's Nightmare", "Kline's Nightmare (Boss)-122", new[] { (3, 290, 756, 136, 16, 0, 51, 0, 59) }),
        (148, "Murgg Tree", "Murgg Tree (inner)-148", new[] { (6, 290, 660, 392, 80, 0, 51, 0, 59), (7, 290, 900, 792, 48, 0, 51, 0, 59) }),
        (149, "Murgg Tree", "Murgg Tree (inner)-149", new[] { (24, 290, 660, 200, 80, 0, 51, 0, 59) }),
        (22, "Lars' Crypt", "Lars' Crypt-22", new[] { (21, 290, 888, 464, 32, 0, 51, 0, 59), (33, 290, 1104, 240, 32, 0, 51, 0, 59) }),
        (23, "Lars' Crypt", "Lars' Crypt-23", new[] { (10, 290, 732, 680, 32, 28, 51, 28, 55) }),
        (24, "Lars' Crypt", "Lars' Crypt-24", new[] { (24, 293, 852, 856, 32, 28, 50, 29, 55), (25, 293, 708, 696, 32, 28, 50, 29, 55), (26, 293, 948, 504, 32, 28, 50, 29, 55) }),
        (27, "Ancient Shrine", "Ancient Shrine-27", new[] { (5, 363, 816, 520, 32, 26, 41, 27, 41), (6, 367, 876, 544, 32, 26, 41, 27, 41), (7, 368, 816, 584, 32, 26, 41, 27, 41), (8, 369, 756, 544, 32, 26, 41, 27, 41), (29, 368, 336, 824, 16, 8, 28, 48, 59), (30, 369, 408, 824, 16, 8, 28, 48, 59), (31, 363, 504, 824, 16, 8, 28, 48, 59), (32, 367, 576, 824, 16, 8, 28, 48, 59) }),
        (29, "Ancient Shrine", "Ancient Shrine-29", new[] { (2, 290, 1068, 712, 16, 32, 47, 26, 55), (3, 363, 84, 696, 64, 0, 23, 36, 59), (4, 367, 84, 760, 64, 0, 23, 36, 59), (5, 368, 348, 696, 64, 0, 23, 36, 59), (6, 369, 348, 760, 64, 0, 23, 36, 59), (20, 363, 936, 216, 16, 32, 51, 0, 19), (21, 367, 1068, 88, 16, 32, 51, 0, 19), (22, 368, 804, 88, 16, 32, 51, 0, 19), (23, 369, 1068, 168, 16, 32, 51, 0, 19) }),
        (30, "Ancient Shrine", "Ancient Shrine-30", new[] { (22, 363, 156, 712, 32, 0, 15, 41, 59), (23, 367, 180, 712, 32, 0, 15, 41, 59), (24, 368, 204, 712, 32, 0, 15, 41, 59), (25, 369, 228, 712, 32, 0, 15, 41, 59) }),
        (307, "Meia Dream", "Meia Dream-307", new[] { (15, 293, 948, 520, 64, 30, 51, 0, 59), (16, 293, 972, 520, 64, 30, 51, 0, 59) }),
        (32, "Ancient Shrine", "Ancient Shrine-32", new[] { (25, 293, 204, 328, 16, 0, 16, 19, 33) }),
        (33, "Ancient Shrine", "Ancient Shrine-33", new[] { (10, 290, 972, 104, 16, 30, 43, 5, 18), (11, 290, 996, 104, 16, 30, 43, 5, 18) }),
        (339, "Lake Shrine", "Lake Shrine (inner)-339", new[] { (4, 290, 900, 664, 16, 25, 43, 24, 50), (11, 290, 684, 488, 240, 25, 43, 24, 50), (12, 290, 708, 488, 240, 25, 43, 24, 50), (27, 290, 804, 664, 48, 25, 43, 24, 50) }),
        (341, "Lake Shrine", "Lake Shrine (inner)-341", new[] { (18, 290, 204, 120, 64, 0, 25, 0, 30) }),
        (343, "Lake Shrine", "Lake Shrine (inner)-343", new[] { (18, 290, 660, 616, 80, 21, 37, 32, 50), (19, 290, 756, 616, 80, 21, 37, 32, 50) }),
        (344, "Lake Shrine", "Lake Shrine (inner)-344", new[] { (31, 290, 1188, 168, 64, 39, 51, 0, 25), (32, 290, 1212, 168, 64, 39, 51, 0, 25), (33, 290, 1212, 168, 80, 39, 51, 0, 25), (34, 290, 1188, 168, 80, 39, 51, 0, 25) }),
        (351, "Lake Shrine", "Lake Shrine (inner)-351", new[] { (0, 290, 876, 232, 32, 22, 40, 0, 24), (1, 290, 900, 232, 32, 22, 40, 0, 24), (2, 290, 876, 232, 48, 22, 40, 0, 24), (3, 290, 900, 232, 48, 22, 40, 0, 24), (4, 290, 876, 248, 32, 22, 40, 0, 24), (17, 290, 204, 776, 32, 0, 32, 36, 59), (28, 290, 1068, 744, 32, 37, 51, 37, 59), (29, 290, 1092, 744, 32, 37, 51, 37, 59) }),
        (38, "Nirude’s Lair", "Nirude’s Lair-38", new[] { (9, 290, 756, 808, 80, 24, 51, 40, 59), (10, 290, 804, 808, 128, 24, 51, 40, 59), (11, 290, 828, 808, 128, 24, 51, 40, 59) }),
        (39, "Nirude’s Lair", "Nirude’s Lair-39", new[] { (1, 290, 276, 376, 16, 0, 14, 0, 59) }),
        (400, "Desert", "Desert _ Ancient Shrine-400", new[] { (4, 363, 204, 200, 16, 0, 16, 0, 30), (5, 367, 228, 200, 16, 0, 16, 0, 30), (6, 368, 252, 200, 16, 0, 16, 0, 30), (7, 369, 276, 200, 16, 0, 16, 0, 30), (12, 363, 672, 208, 16, 22, 35, 0, 30), (13, 367, 696, 208, 16, 22, 35, 0, 30), (14, 368, 720, 208, 16, 22, 35, 0, 30), (15, 369, 744, 208, 16, 22, 35, 0, 30) }),
        (404, "Desert Mini Puzle to Shrine", "Desert Mini Puzle to Shrine-404", new[] { (0, 293, 372, 264, 16, 0, 51, 0, 59), (1, 293, 372, 280, 16, 0, 51, 0, 59) }),
        (423, "Nirude", "Nirude (inner)-423", new[] { (6, 290, 876, 440, 64, 34, 49, 8, 33), (7, 290, 1020, 440, 64, 34, 49, 8, 33) }),
        (424, "Nirude", "Nirude (inner)-424", new[] { (14, 290, 1032, 104, 16, 33, 50, 5, 19) }),
        (425, "Nirude", "Nirude (inner)-425", new[] { (0, 290, 276, 376, 16, 0, 24, 0, 35) }),
        (430, "Nava", "Nava''s Entrance Caves-430", new[] { (4, 290, 516, 184, 64, 18, 32, 0, 25), (5, 290, 516, 696, 32, 18, 33, 32, 59) }),
        (442, "Meia Dream", "Meia Dream-442", new[] { (15, 293, 948, 520, 64, 30, 51, 0, 59), (16, 293, 972, 520, 64, 30, 51, 0, 59), (27, 290, 348, 648, 32, 0, 20, 36, 59) }),
        (444, "Torla", "Torla (inner)-444", new[] { (25, 293, 972, 376, 64, 36, 51, 4, 30) }),
        (445, "Torla", "Torla (inner)-445", new[] { (15, 293, 852, 664, 80, 32, 51, 30, 59), (16, 293, 1044, 616, 80, 32, 51, 30, 59) }),
        (53, "Elenes Nightmare", "Elenes Nightmare-53", new[] { (3, 290, 492, 168, 112, 3, 21, 10, 29) }),
        (57, "Torla Buggy_ with bar music and random texts", "Torla Buggy_ with bar music and random texts (early version of Torla)-57", new[] { (6, 293, 852, 712, 80, 34, 51, 21, 59), (7, 293, 1020, 552, 80, 34, 51, 21, 59) }),
        (67, "Coal Mine", "Coal Mine-67", new[] { (11, 290, 1044, 888, 16, 35, 51, 43, 59), (12, 290, 1068, 888, 16, 35, 51, 43, 59) }),
        (69, "Coastal Cave", "Coastal Cave-69", new[] { (18, 293, 468, 760, 32, 0, 25, 30, 59), (40, 293, 204, 696, 32, 0, 25, 30, 59) }),
        (70, "Coastal Cave", "Coastal Cave-70", new[] { (8, 373, 780, 456, 48, 20, 51, 0, 37), (9, 373, 696, 392, 16, 20, 51, 0, 37), (10, 373, 744, 392, 16, 20, 51, 0, 37), (11, 373, 792, 360, 16, 20, 51, 0, 37), (12, 373, 840, 360, 16, 20, 51, 0, 37), (21, 373, 636, 360, 16, 20, 51, 0, 37) }),
        (71, "Coastal Cave", "Coastal Cave-71", new[] { (14, 290, 228, 120, 48, 0, 32, 0, 35) }),
        (72, "Coastal Cave", "Coastal Cave-72", new[] { (1, 290, 180, 200, 48, 0, 36, 0, 28) }),
        (73, "Coastal Cave", "Coastal Cave-73", new[] { (10, 373, 444, 696, 32, 0, 29, 39, 59), (11, 373, 396, 728, 16, 0, 29, 39, 59), (12, 373, 396, 792, 16, 0, 29, 39, 59), (36, 373, 996, 712, 16, 32, 51, 0, 59), (37, 373, 972, 904, 32, 32, 51, 0, 59) }),
        (79, "Underground Watercourse", "Underground Watercourse-79", new[] { (5, 293, 60, 808, 0, 0, 25, 40, 59) }),
        (86, "Magyscar", "Magyscar-86", new[] { (15, 290, 996, 440, 32, 36, 51, 16, 31), (16, 290, 1020, 440, 32, 36, 51, 16, 31) }),
        (87, "Magyscar", "Magyscar-87", new[] { (17, 290, 948, 440, 48, 0, 51, 0, 35) }),
        (93, "Magyscar", "Magyscar-93", new[] { (20, 290, 876, 232, 64, 34, 51, 0, 27) }),
    };

    /// <summary>The eight trunks of the maps 70 and 73 that stand on the water and float (m7): the only placements of the table that change.</summary>
    private static readonly HashSet<(int Map, int Record)> Floating = new() { (70, 9), (70, 10), (70, 11), (70, 12), (70, 21), (73, 11), (73, 12), (73, 36) };

    public static TheoryData<int> NativeMaps()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < NativePlacements.Length; i++)
        {
            data.Add(i);
        }

        return data;
    }

    [Fact]
    public void M11_ThePlacementsTable_Holds117NativePlacementsOfTheSevenTypes_AmongThemTheEightFloatingTrunks()
    {
        var all = NativePlacements.SelectMany(m => m.Placements.Select(p => (m.Map, p.Record, p.Type))).ToList();
        Assert.Equal(117, all.Count);
        Assert.All(all, p => Assert.Contains(p.Type, LiftableAi.C4SpriteTypes));
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.All(Floating, f => Assert.Contains((f.Map, f.Record, 373), all));
    }

    /// <summary>Every simple readable property and field of the proxy (numbers, booleans, enums), read the same way before and after (the model of <c>LiftableAiMap392Tests</c>).</summary>
    private static Dictionary<string, object?> Fields(EntityScriptProxy entity)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var property in typeof(EntityScriptProxy).GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            var type = property.PropertyType;
            if (property.GetIndexParameters().Length != 0 || property.GetMethod == null || !(type.IsPrimitive || type.IsEnum))
            {
                continue;
            }

            fields[property.Name] = property.GetValue(entity);
        }

        foreach (var field in typeof(EntityScriptProxy).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
            {
                fields["field " + field.Name] = field.GetValue(entity);
            }
        }

        fields["PlatformEntity"] = entity.PlatformEntity == null ? null : "set";
        fields["RidingEntity"] = entity.RidingEntity == null ? null : "set";
        return fields;
    }

    [Theory]
    [MemberData(nameof(NativeMaps))]
    public void M11_Witness_TheNativePlacementsOfAMapAtRest_StayInertFor6Ticks_OnTheirRealCells(int mapIndex)
    {
        var (map, zone, world, placements) = NativePlacements[mapIndex];
        var covered = new HashSet<int>();
        foreach (var start in placements)
        {
            if (covered.Contains(start.Record))
            {
                continue;
            }

            // the hero arrives at the placement's height, some tiles away from it and inside the zone that loads it (an object beside the arrival of the hero is not born): the first of these arrivals that loads the placement.
            ArcRun? loaded = null;
            foreach (var (dx, dy) in new[] { (3, 0), (-3, 0), (0, 4), (0, -4), (6, 0), (-6, 0), (0, 8), (0, -8), (1, 0), (-1, 0) })
            {
                var heroTileX = Math.Clamp(start.X / 24 + dx, start.TileMinX, start.TileMaxX);
                var heroTileY = Math.Clamp(start.Y / 16 + dy, start.TileMinY, start.TileMaxY);
                var spec = new ArcSpec("g2c1-m11-" + map + "-" + start.Record, zone, world, Array.Empty<int>(), 0, 0, 0, 2000, RealController: true, Prefabs: true,
                    Arrival: new ArcArrival((heroTileX * 24 + 12) << 16, (heroTileY * 16 + 8) << 16, start.Z << 16, GameState.ResetAnimationId, 8));
                var candidate = new ArcRun(spec);
                Frame(candidate);
                if (candidate.Entities.Any(e => e.SpriteType == start.Type && Math.Abs((e.PosX >> 16) - start.X) <= 2 && Math.Abs((e.PosY >> 16) - start.Y) <= 2))
                {
                    loaded = candidate;
                    break;
                }

                candidate.Dispose();
            }

            Assert.True(loaded != null, $"map {map} record {start.Record}: type {start.Type} at ({start.X}, {start.Y}, {start.Z}) was not born from any arrival tried");
            using var arc = loaded!;
            EntityScriptProxy? Find((int Record, int Type, int X, int Y, int Z, int TileMinX, int TileMaxX, int TileMinY, int TileMaxY) p)
                => arc.Entities.FirstOrDefault(e => e.SpriteType == p.Type && Math.Abs((e.PosX >> 16) - p.X) <= 2 && Math.Abs((e.PosY >> 16) - p.Y) <= 2);
            var present = placements.Where(p => Find(p) != null && !covered.Contains(p.Record)).ToList();
            var entities = present.Select(p => Find(p)!).ToList();

            // an object placed above its ground first falls to it, before and after C4 alike: the snapshot is taken once their heights have not moved for 2 ticks.
            var stable = 0;
            for (var settle = 0; settle < 120 && stable < 2; settle++)
            {
                var heights = entities.Select(e => e.PosZ).ToList();
                Frame(arc);
                stable = heights.SequenceEqual(entities.Select(e => e.PosZ)) ? stable + 1 : 0;
            }

            var before = entities.Select(Fields).ToList();
            for (var i = 0; i < 6; i++)
            {
                Frame(arc);
            }

            for (var i = 0; i < entities.Count; i++)
            {
                covered.Add(present[i].Record);
                if (Floating.Contains((map, present[i].Record)))
                {
                    continue;
                }

                var after = Fields(entities[i]);
                var changed = before[i].Where(f => !Clocks.Contains(f.Key) && !Equals(f.Value, after[f.Key])).Select(f => f.Key).ToList();
                Assert.True(changed.Count == 0, $"map {map} record {present[i].Record} (type {present[i].Type}): {string.Join(", ", changed)}");
            }
        }

        Assert.Equal(placements.Length, covered.Count);
    }

    private static void Frame(ArcRun arc)
    {
        arc.HoldDirections(0);
        arc.OneFrame();
    }
}
