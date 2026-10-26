#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T2 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c2", the tests of the world (w1) to (w9), phases B and C): the torch (the type 393, C70 and E20), the flame it leaves (the type 12 without its six native codes) and the brambles (D34), in the REAL world of the exported map 92 of Magyscar
/// (<see cref="TorchRun"/>: real prefabs and controllers, the effect stage, the sound player, the real hero throwing with the Square). NOTHING is registered by the tests: the production registry alone decides, so (w1) to (w7) and (w9) are RED until the keys <c>(C, 70)</c>, <c>(E, 20)</c> and <c>(D, 34)</c> are in it;
/// (w8) is a witness, green before and after. T is the tick of the front of the Square (the tick 1 of the run). The expected values of the throw are the binary's (<c>docs/plan-e20-annexe/e20-g2c2/replay/s8_torch.txt</c>, read here: the tick of the throw is the tick 1, the oracle's tick o is the test's tick o - throw + 1), the contact and the rain
/// are <c>p_hitswitch.txt</c> and <c>p_rain.txt</c> of the same folder (the ticks relative to the blow), the DLL probes of T0 measured them equal (<c>probes/out/</c>) except the one tick of the rain disposed by D-G2C2-9, which is why (w5) pins no tick of the status 3.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class TorchWorldTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const uint Right = PadState.Right;
    private const uint FlameFlags = 0x132146;
    private const uint BurningTorchFlags = 0x332346;
    private const int FlameLife = 39;

    public TorchWorldTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> Cadences => new() { 1, 2, 3 };

    public static TheoryData<int, bool> ThrownCases => new() { { 1, false }, { 2, false }, { 3, false }, { 1, true } };

    // -----------------------------------------------------------------------------------------
    // the key of the throw: replay/s8_torch.txt (the binary's replay of T0), read here
    // -----------------------------------------------------------------------------------------

    /// <summary>The ticks of the binary's replay of the throw (the line "torch moving" or "torch still" of <c>s8_torch.txt</c>) and the x of the flame at the end.</summary>
    private sealed record Key(int Throw, int Status3, int Status4, int Freed, int SoundOfStatus3, int SoundOfTheFreeing, double FlameX)
    {
        /// <summary>The test tick of an oracle tick: the throw is the tick 1.</summary>
        internal int Tick(int oracleTick) => oracleTick - Throw + 1;

        internal static Key Load(bool moving)
        {
            var line = File.ReadAllLines(AnnexFiles.TorchAnnex("replay/s8_torch.txt")).Single(l => l.StartsWith(moving ? "torch moving:" : "torch still:", StringComparison.Ordinal));
            int First(string pattern) => int.Parse(Regex.Match(line, pattern).Groups[1].Value, CultureInfo.InvariantCulture);
            return new Key(
                First(@"'t(\d+) anim_t:0>"),
                First(@"'t(\d+) status:2>3"),
                First(@"'t(\d+) status:3>4"),
                First(@"'t(\d+) status:4>0"),
                First(@"\((\d+), \['snd\(142\)'\]\)"),
                First(@"\((\d+), \['snd\(41\)'\]\)"),
                double.Parse(Regex.Match(line, @"\(2, '0xc', 2, 3, '0x132146', ([\d.]+), 0\.0\)").Groups[1].Value, CultureInfo.InvariantCulture));
        }
    }

    private static Func<int, uint> ThrowPad(bool walking) => t => t == 1 ? Square | (walking ? Right : 0u) : walking && t < 4 ? Right : 0u;

    private static void AssertTheFlame(TorchRun.Sample flame, string where)
    {
        Assert.True(flame.Present, where);
        Assert.Equal(3u, flame.Animation);
        Assert.Equal(FlameFlags, flame.Flags);
        Assert.Equal("0/0/0/0/0/0", flame.Codes);
    }

    // -----------------------------------------------------------------------------------------
    // (w1) the chronology of a thrown torch, still and walking, at the three rates
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ThrownCases))]
    public void W1_ATorchThrownWithTheSquare_LandsAtT22_BurnsAtT23WithTheSound142_LeavesItsFlameAtT26_IsFreedAtT27WithTheSound41_AndTheFlameLivesThirtyNineTicks(int ticksPerFrame, bool walking)
    {
        var key = Key.Load(walking);
        using var run = new TorchRun("w1-" + ticksPerFrame + (walking ? "w" : "s"));
        var torch = run.Watch(run.CarryATorch());
        run.Go(ticksPerFrame, 75, ThrowPad(walking));

        var burning = key.Tick(key.Status3);
        var born = key.Tick(key.Status4);
        var freed = key.Tick(key.Freed);
        Assert.Equal((24, 27, 28), (burning, born, freed));

        // the landing at T+22, the tick before the status 3; nothing before it touches the ground.
        Assert.Equal(burning - 1, torch.Ticks.Where(t => t.Value.OnGround == 1).Select(t => t.Key).DefaultIfEmpty(-1).Min());
        for (var tick = 1; tick < burning; tick++)
        {
            Assert.Equal(EntityStatus.Normal, torch.At(tick).Status);
            Assert.Empty(run.Flames[tick]);
        }

        // the status 3 at T+23: the animation 2, the flags of the burning torch, the sound 142 once.
        Assert.Equal(EntityStatus.Deactivated, torch.At(burning).Status);
        Assert.Equal((2u, BurningTorchFlags), (torch.At(burning).Animation, torch.At(burning).Flags));
        Assert.Equal(1, run.SoundsAt(key.Tick(key.SoundOfStatus3), 142));
        Assert.Equal(burning, key.Tick(key.SoundOfStatus3));
        for (var tick = burning; tick < born; tick++)
        {
            Assert.Equal(EntityStatus.Deactivated, torch.At(tick).Status);
            Assert.Empty(run.Flames[tick]);
        }

        // the flame is born at T+26, in the tick the torch is flagged for destruction: where the torch is, at the height of the ground, with no native code.
        Assert.Equal(EntityStatus.FlagToDestroy, torch.At(born).Status);
        var flame = Assert.Single(run.Flames[born]);
        AssertTheFlame(flame, "born");
        Assert.Equal(key.FlameX, flame.X);
        Assert.Equal(100.0, flame.Y);
        Assert.InRange(flame.Z, 0.0, 0.001); // the Z of the DLL's frame (D-E0A-1) differs from the binary's by one raw unit at most

        // the torch is freed at T+27 with the sound 41, once; the flame lives 39 ticks (the nappe of fire: the animation 3).
        Assert.False(torch.At(freed).Present);
        Assert.Equal(1, run.SoundsAt(key.Tick(key.SoundOfTheFreeing), 41));
        Assert.Equal(freed, key.Tick(key.SoundOfTheFreeing));
        Assert.Equal(Enumerable.Range(born, FlameLife).ToList(), run.FlameTicks());
        foreach (var tick in run.FlameTicks())
        {
            var f = Assert.Single(run.Flames[tick]);
            AssertTheFlame(f, "tick " + tick);
            Assert.InRange(f.X, key.FlameX - 1.0, key.FlameX);
        }

        // the torch and its flame never touch the hero; the sounds are one each.
        Assert.All(run.Hero.Values, h => Assert.Equal(10, h.Hp));
        Assert.All(run.Sparks.Values, s => Assert.Equal(0, s));
        Assert.Equal(1, run.Heard.Played.Count(p => p.Sfx == 142));
        Assert.Equal(1, run.Heard.Played.Count(p => p.Sfx == 41));
    }

    // -----------------------------------------------------------------------------------------
    // (w2) the contact: a torch that meets a target burns at once, the flame is born four ticks after the blow and touches the target again 25 ticks after it
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W2_ATorchThrownAgainstATarget_BurnsAtTheBlowPlus1_LeavesItsFlameAtThePlus4_AndTheFlameTouchesTheTargetAgainAtThePlus25(int ticksPerFrame)
    {
        using var run = new TorchRun("w2-" + ticksPerFrame);
        var torch = run.Watch(run.CarryATorch());
        var target = run.PlaceATarget(165, 100);
        run.Go(ticksPerFrame, 140, ThrowPad(true));

        // the blow: the first tick the torch's own attack registers (the hit pass counts it on the attacker, even on a target that takes no damage).
        var blow = torch.Ticks.Where(t => t.Value.Present && t.Value.HitCounter > 0).Select(t => t.Key).DefaultIfEmpty(-1).Min();
        Assert.True(blow > 0, "the torch never touched the target");
        Assert.Equal(EntityStatus.Normal, torch.At(blow).Status);
        Assert.Equal(EntityStatus.Deactivated, torch.At(blow + 1).Status);
        Assert.Equal(2u, torch.At(blow + 1).Animation);

        // the flame: born at the blow + 4, in the tick the torch is flagged for destruction, at the place of the contact; none before.
        Assert.All(Enumerable.Range(1, blow + 3), tick => Assert.Empty(run.Flames[tick]));
        Assert.Equal(EntityStatus.FlagToDestroy, torch.At(blow + 4).Status);
        var flame = Assert.Single(run.Flames[blow + 4]);
        AssertTheFlame(flame, "born at the blow + 4");
        Assert.Equal(100.0, flame.Y);
        Assert.False(torch.At(blow + 5).Present);
        Assert.Equal(Enumerable.Range(blow + 4, FlameLife).ToList(), run.FlameTicks());

        // the flame touches the same target again when the cooldown of 25 ticks the blow set on it is over: the first tick its own hit counter moves.
        var again = run.Flames.Where(f => f.Value.Any(x => x.HitCounter > 0)).Select(f => f.Key).DefaultIfEmpty(-1).Min();
        Assert.Equal(blow + 25, again);
        Assert.NotNull(target);
        Assert.All(run.Hero.Values, h => Assert.Equal(10, h.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (w3) the switches of the map 92: the torch lights them itself, before any flame, through the real hit pass (0x87 [128, 6] of their program D)
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The four switches ("Interrupteur (allumage)", sprite 51) of the group of the records 39 to 42 of the map 92. Their program D (D40 to D43) tests <c>0x87 [128, 6]</c> and raises the flag <c>lo | 129 &lt;&lt; 8</c> (144, 154, 164, 174); their program C (C40 to C43,
    /// <c>dump_prog.py 92 D40 C40</c>) waits for it, clears it, plays the sound 39, runs the animation 1 and raises the flag of the lit switch (145, 155, 165, 175). The chain proves the touch went through the real hit pass: the torch itself (attack attribute 6) lights the switch, before any flame.
    /// </summary>
    [Theory]
    [InlineData(39, 144, 145)]
    [InlineData(40, 154, 155)]
    [InlineData(41, 164, 165)]
    [InlineData(42, 174, 175)]
    public void W3_ASwitchOfTheMap92_IsLitByTheTorchThrownAtIt_AndByNothingElse(int record, int touched, int lit)
    {
        uint Word(int low) => GameState.Instance.GetFlag((uint)(low | (129 << 8))) & (1u << (low & 31));

        using (var thrown = new TorchRun("w3-lit-" + record))
        {
            var torch = thrown.Watch(thrown.CarryATorch());
            var sw = thrown.World.SpawnEntityByRecordId(thrown.Arc.Hero, record) ?? throw new InvalidOperationException("the switch was not born");
            Place(sw, 165, 100);
            Assert.Equal((0u, 0u), (Word(touched), Word(lit)));
            thrown.Go(1, 120, ThrowPad(true));
            Assert.True(torch.Ticks.Values.Any(t => t.HitCounter > 0), "the torch never touched the switch");
            Assert.NotEqual(0u, Word(lit));
            Assert.Equal(1, thrown.Heard.Played.Count(p => p.Sfx == 39));
        }

        GameState.Instance.ResetForTests();
        using (var untouched = new TorchRun("w3-dark-" + record))
        {
            var sw = untouched.World.SpawnEntityByRecordId(untouched.Arc.Hero, record) ?? throw new InvalidOperationException("the switch was not born");
            Place(sw, 165, 100);
            untouched.Go(1, 120);
            Assert.Equal((0u, 0u), (Word(touched), Word(lit)));
            Assert.Equal(0, untouched.Heard.Played.Count(p => p.Sfx == 39));
        }
    }

    private static void Place(EntityScriptProxy entity, int x, int y)
    {
        entity.PosX = x << 16;
        entity.PosY = y << 16;
        entity.PosZ = 0;
        entity.PushLogicalPositionToRoot();
    }

    // -----------------------------------------------------------------------------------------
    // (w4) the brambles: fire burns them, they stop blocking and go; the sword does nothing
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(238)]
    [InlineData(239)]
    public void W4_ABrambleTouchedByTheTorch_BurnsAtOnce_StopsBlocking_AndGoes(int type)
    {
        using var run = new TorchRun("w4-" + type, types: new[] { 393, TorchRun.FlameType, 238, 239 });
        var torch = run.Watch(run.CarryATorch());
        var bramble = run.World.SpawnEntityBySpriteIndex(null, false, type, 165 << 16, 100 << 16, 0, 0) ?? throw new InvalidOperationException("the bramble was not born");
        var track = run.Watch(bramble);
        Assert.NotEqual(0u, bramble.Flags & EntityFlags.Collidable);
        Assert.True(EntitySupport.IsEligibleSubject(bramble));
        run.Go(1, 220, ThrowPad(true));

        var blow = torch.Ticks.Where(t => t.Value.Present && t.Value.HitCounter > 0).Select(t => t.Key).DefaultIfEmpty(-1).Min();
        Assert.True(blow > 0, "the torch never touched the bramble");
        // before the blow the bramble is an obstacle that does nothing.
        Assert.All(Enumerable.Range(1, blow - 1), tick => Assert.Equal((0u, true), (track.At(tick).Animation, (track.At(tick).Flags & EntityFlags.Collidable) != 0)));

        // from the tick after the blow at the latest: the animation 1, the flags (flags | 0x46) & ~0x80 (it stops blocking), no more an obstacle for the entities.
        var burning = track.Ticks.Where(t => t.Value.Animation == 1).Select(t => t.Key).DefaultIfEmpty(-1).Min();
        Assert.InRange(burning, blow, blow + 2);
        var burnt = track.At(burning);
        Assert.Equal(0x46u, burnt.Flags & 0x46u);
        Assert.Equal(0u, burnt.Flags & EntityFlags.Collidable);

        // it goes by the end of its animation (the generic E0): the slot is free before the end of the run, and it did not stay in the world.
        var gone = track.FirstTickGone();
        Assert.True(gone > burning, "the bramble never went");
        Assert.True(gone < 220);
    }

    [Fact]
    public void W4_ABrambleTouchedByTheSword_DoesNothing()
    {
        using var run = new TorchRun("w4-sword", types: new[] { 393, TorchRun.FlameType, 238, 239 });
        run.Arc.Give(1);
        run.Arc.Equip(1);
        var bramble = run.World.SpawnEntityBySpriteIndex(null, false, 238, 130 << 16, 100 << 16, 0, 0) ?? throw new InvalidOperationException("the bramble was not born");
        var track = run.Watch(bramble);
        var before = bramble.Flags;
        run.Go(1, 90, t => t is 1 or 30 or 60 ? Square : 0u);

        // the sword strikes the bramble (the hero is in the sword animation 16, the hit pass makes its sparks); the sheet of a bramble answers only to the attribute 6 (the fire), so the blow changes nothing in it: it keeps its animation, its flags, its place in the world.
        Assert.Contains(run.Hero.Values, h => h.Animation == 16);
        Assert.Contains(run.Sparks.Values, sparks => sparks > 0);
        Assert.All(track.Ticks.Values, s => Assert.Equal((true, 0u, before), (s.Present, s.Animation, s.Flags)));
        Assert.True(EntitySupport.IsEligibleSubject(bramble));
    }

    // -----------------------------------------------------------------------------------------
    // (w5) the rain of the lever of the map 92: four torches born by the program C[15] with the bit 0x10, each burns where it lands
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W5_TheLeverOfTheMap92_LetsFourTorchesFall_AndEachLeavesItsFlameOnTheGround()
    {
        using var run = new TorchRun("w5", heroX: 40, heroY: 40);
        // the lever (record 14) waits for the flag 150 of its program C[15] (0x36 [54, 150, 128]); the flag stands for the pull of the lever.
        var lever = run.World.SpawnEntityByRecordId(run.Arc.Hero, 14) ?? throw new InvalidOperationException("the lever was not born");
        Assert.NotNull(lever);
        GameState.Instance.AddFlag(150u | (128u << 8), 1u << (150 & 31));
        run.Go(1, 600);

        // exactly four torches, born by the program at its four points, each flagged DeactivateOnImpact (0x62 [98, 68, 16, 0]) and each burning.
        Assert.Equal(4, run.SeenTorches.Count);
        Assert.Equal(4, run.SeenFlames.Count);
        var points = new HashSet<(double, double)> { (216, 424), (288, 424), (216, 456), (288, 456) };
        var flameTicks = run.FlameTicks();
        var seenPoints = new HashSet<(double, double)>();
        foreach (var tick in flameTicks)
        {
            Assert.InRange(run.Flames[tick].Count, 1, 4);
            foreach (var f in run.Flames[tick])
            {
                AssertTheFlame(f, "tick " + tick);
                Assert.InRange(f.Z, 0.0, 0.001);
                Assert.Contains((Math.Round(f.X), Math.Round(f.Y)), points);
                seenPoints.Add((Math.Round(f.X), Math.Round(f.Y)));
            }
        }

        Assert.Equal(points, seenPoints);
        Assert.All(run.SeenTorches, t => Assert.Equal(EntityStatus.Destroyed, t.Status));
        Assert.Equal(4, run.Heard.Played.Count(p => p.Sfx == 142));
        Assert.Equal(4, run.Heard.Played.Count(p => p.Sfx == 41));
        Assert.Equal(1, run.Heard.Played.Count(p => p.Sfx == 55));

        // and the same torches without the bit stay torches: the lever's trap is the bit, not the fall.
    }

    // -----------------------------------------------------------------------------------------
    // (w6) the flame burns the hero who stands in it: the injury of c2b
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W6_TheFlameBornInFrontOfTheHero_HurtsHimWhenHeWalksIntoIt_AndTheTorchNever()
    {
        using var run = new TorchRun("w6");
        run.Watch(run.CarryATorch());
        // the torch is thrown still (its flame is born at x = 143.5 at the tick 27); the hero stays where he is for the flight and the burning of the torch (it never touches him), then walks into the flame.
        run.Go(1, 100, t => t == 1 ? Square : t is >= 30 and <= 70 ? Right : 0u);

        var born = run.FlameTicks().DefaultIfEmpty(-1).Min();
        Assert.Equal(27, born);
        var hurt = run.Hero.Where(h => h.Value.Hp < 10).Select(h => h.Key).DefaultIfEmpty(-1).Min();
        Assert.True(hurt > born, $"the hero was not hurt by the flame (born {born}, hurt {hurt})");
        Assert.InRange(hurt, born, born + FlameLife);
        Assert.All(Enumerable.Range(1, hurt - 1), tick => Assert.Equal(10, run.Hero[tick].Hp));
        // the injury of c2b: the hurt animation 0x31 right after the loss of life.
        Assert.Contains(run.Hero.Where(h => h.Key >= hurt && h.Key <= hurt + 2).Select(h => h.Value.Animation), a => a == 0x31);
    }

    // -----------------------------------------------------------------------------------------
    // (w7) the flame on the deep water is destroyed; the torch thrown above a hole disappears without a flame
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W7_TheFlameBornAboveTheDeepWater_IsDestroyedWhereItLands_WithTheBreakEffect6()
    {
        // the torch is thrown over the water (the cells of the columns 5 and more are deep water), meets a target in mid-air and burns there; a torch that LANDS on the water is destroyed by it (its own DestroyOnSlidingSlope) before it burns, the flame is what is left to see.
        using var run = new TorchRun("w7-water", (cx, _) => cx >= 5 ? new CutBench.Cell(0, 0, null, 0x08) : new CutBench.Cell(0, 0));
        var torch = run.Watch(run.CarryATorch());
        run.PlaceATarget(165, 100);
        run.Go(1, 120, ThrowPad(true));

        var blow = torch.Ticks.Where(t => t.Value.Present && t.Value.HitCounter > 0).Select(t => t.Key).DefaultIfEmpty(-1).Min();
        Assert.True(blow > 0, "the torch never touched the target");
        var ticks = run.FlameTicks();
        Assert.NotEmpty(ticks);
        // the flame is born in the air (the torch met the target at z = 22.5 and went on falling in its status 3), falls to the water, and is destroyed there with the break effect 6 of the entities that carry DestroyOnSlidingSlope: it lives a few ticks, nothing like the 39 of a flame on dry ground.
        var last = ticks[^1];
        Assert.Equal(Enumerable.Range(ticks[0], ticks.Count).ToList(), ticks);
        Assert.True(ticks.Count <= 8, $"the flame lived {ticks.Count} ticks over the deep water");
        Assert.Equal(1, run.Flames[last][0].OnGround);
        Assert.Contains(run.Effects, e => e.Id == 6 && e.Tick > last && e.Tick <= last + 2);
    }

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W7_ATorchThrownAboveAHole_DisappearsAtT23WithTheEffect10_WithoutAFlame(int ticksPerFrame)
    {
        // the columns 6 to 9 are a hole (walkability 0x04): the torch lands on it at x = 178 (T+22), the pick phase destroys it one tick later (the status 4, never through 3), the slot is free at T+24 (g2b2).
        using var run = new TorchRun("w7-hole-" + ticksPerFrame, (cx, _) => cx is >= 6 and <= 9 ? new CutBench.Cell(0x04, 0) : new CutBench.Cell(0, 0));
        var torch = run.Watch(run.CarryATorch());
        run.Go(ticksPerFrame, 60, ThrowPad(false));

        Assert.Equal(EntityStatus.Normal, torch.At(23).Status);
        Assert.Equal(1, torch.At(23).OnGround);
        Assert.Equal(EntityStatus.FlagToDestroy, torch.At(24).Status);
        Assert.False(torch.At(25).Present);
        Assert.Empty(run.FlameTicks());
        Assert.Equal(0, run.Heard.Played.Count(p => p.Sfx == 142));
        Assert.Contains(run.Effects, e => e.Id == 10);
    }

    // -----------------------------------------------------------------------------------------
    // (w10) phase C: the torch at rest is lifted by the hero (393 is in the wired set of the lift); without it the Square gives the sword
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W10_ATorchAtRest_IsLiftedByTheHeroWhoWalksIntoItAndPressesTheSquare(int ticksPerFrame)
    {
        using var run = new TorchRun("w10-" + ticksPerFrame);
        run.Arc.Give(1);
        run.Arc.Equip(1);
        var torch = run.PlaceATorch(135, 100, 0, deactivateOnImpact: false);
        run.Frames(4);
        // walks east into the torch, then presses the Square once, at the first frame from the tick 8 (a front edge of the Square in contact lifts; a second press would throw it).
        var pressed = false;
        run.Go(ticksPerFrame, 80, t =>
        {
            if (t >= 8 && !pressed)
            {
                pressed = true;
                return Right | Square;
            }

            return Right;
        });

        Assert.Same(torch.LogicContextEntity, run.Arc.Hero.CarriedEntity);
        Assert.Contains(run.Hero.Values, h => h.Animation == 5); // the lift on the ground
        Assert.Same(run.Arc.Hero.LogicContextEntity, torch.PlatformEntity);
    }

    // -----------------------------------------------------------------------------------------
    // (w8) the torches at rest: the 44 torches born at the load of the maps 86 to 94 keep their flags, animation, position and status for six ticks (a witness, green before and after the inscriptions)
    // -----------------------------------------------------------------------------------------

    /// <summary>The torches born at the load: the census of T0 (<c>native-torches-86-94.txt</c>): the map and the record, read here.</summary>
    public static TheoryData<int> MapsWithTorches()
    {
        var data = new TheoryData<int>();
        foreach (var map in NativeTorches().Select(t => t.Map).Distinct().OrderBy(m => m))
        {
            data.Add(map);
        }

        return data;
    }

    private static List<(int Map, int Record)> NativeTorches()
        => File.ReadAllLines(AnnexFiles.TorchAnnex("native-torches-86-94.txt"))
            .Where(l => l.Length > 0 && char.IsDigit(l[0]))
            .Select(l => l.Split(' '))
            .Select(p => (int.Parse(p[0], CultureInfo.InvariantCulture), int.Parse(p[1], CultureInfo.InvariantCulture)))
            .ToList();

    [Fact]
    public void W8_TheCensusIsTheFortyFourTorchesOfTheMaps86To94()
    {
        var all = NativeTorches();
        Assert.Equal(44, all.Count);
        Assert.Equal(new[] { 86, 87, 88, 89, 90, 92, 93, 94 }, all.Select(t => t.Map).Distinct().OrderBy(m => m).ToArray());
        Assert.Equal(15, all.Count(t => t.Map == 92));
    }

    [Theory]
    [MemberData(nameof(MapsWithTorches))]
    public void W8_TheNativeTorchesOfAMap_AtRest_KeepTheirFlagsAnimationPositionAndStatusForSixTicks(int map)
    {
        using var run = new TorchRun("w8-" + map, heroX: 12, heroY: 8, zone: "Magyscar", world: map == 94 ? "Magyscar (Boss _ Uma)-94" : "Magyscar-" + map, synthetic: false);
        var torches = NativeTorches().Where(t => t.Map == map).Select(t => (t.Record, Entity: run.World.SpawnEntityByRecordId(run.Arc.Hero, t.Record) ?? throw new InvalidOperationException($"the record {t.Record} was not born"))).ToList();
        Assert.All(torches, t => Assert.Equal(393, t.Entity.SpriteType));
        run.Frames(2);
        var tracks = torches.Select(t => (t.Record, Track: run.Watch(t.Entity))).ToList();
        var before = torches.Select(t => (t.Entity.Flags, t.Entity.TargetAnimationId, t.Entity.PosX, t.Entity.PosY, t.Entity.PosZ, t.Entity.Status)).ToList();
        run.Go(1, 6);
        for (var i = 0; i < torches.Count; i++)
        {
            var e = torches[i].Entity;
            Assert.Equal(before[i], (e.Flags, e.TargetAnimationId, e.PosX, e.PosY, e.PosZ, e.Status));
            Assert.All(tracks[i].Track.Ticks.Values, s => Assert.True(s.Present && s.Status == EntityStatus.Normal, $"map {map} record {tracks[i].Record}"));
        }
    }

    // -----------------------------------------------------------------------------------------
    // (w9) the flame stays mute even when the handlers of the fire rod (C60 and E9 of f6b) are in the registry
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W9_TheFlameOfTheTorch_StaysMute_WhileTheKeyC60OfTheFireRodIsInTheRegistry(int ticksPerFrame)
    {
        // the precondition that makes the test mean something: f6b put (C, 60) in the production registry for the sprite type 12 the torch copies.
        Assert.True(NativeSpriteRegistry.CreateForProduction().TryGet(ScriptHelper.ProgramCTick, 60, TorchRun.FlameType, out _));

        using var run = new TorchRun("w9-" + ticksPerFrame);
        run.Watch(run.CarryATorch());
        run.Go(ticksPerFrame, 100, ThrowPad(false));

        // the flame of the torch has its six codes erased: it emits no effect 17 (the fire of the rod, every four ticks) and does not explode at the end; it lives its 39 ticks.
        Assert.Equal(Enumerable.Range(27, FlameLife).ToList(), run.FlameTicks());
        Assert.Empty(run.Effects.Where(e => e.Id == 17));
        Assert.All(run.SeenFlames, f => Assert.Equal(0, f.SpriteProgramIndexes.Count(c => c != 0)));
    }
}
