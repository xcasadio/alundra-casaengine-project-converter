#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Xunit;
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
/// E20.g2c3 T2 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c3", the world tests (w1) to (w7), (w9) and (w10)): the drop of water (the type 391, A19 and C71) in the REAL world of the exported map 92 of Magyscar (and the map 158 of the Fairy cave for (w7)): <see cref="WaterDropRun"/>, real prefabs and controllers (the drop's <c>CharacterControllerComponent</c>, profile <c>Pawn</c>), the sound player, the real hero
/// who carries a torch. NOTHING is registered by the tests: the production registry alone decides, so (w1) to (w7) and (w10) are RED until <see cref="Alundra.Ai.Objects.WaterDropAi"/> is in it (the drops hang, as they do at the HEAD); (w9) is a witness, green before and after (the torch thrown against a drop is the branch of E20 of g2c2).
/// Every tick expected is the binary's, from <c>docs/plan-e20-annexe/e20-g2c3/expected-ticks.md</c> (T0 (d)) and was measured equal in the DLL by the probes of T0 (<c>probes/out/</c>, <c>results.md</c>), at 1, 2 and 3 ticks per frame: R is the tick of the restoration (the tick 1 for a drop born by the <c>0x8A</c>, whose A19 ran at the load), d the tick of the first state 2.
/// A tick is one "pad" pass of the frame, sampled at its "inventory" pass.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WaterDropWorldTests : IDisposable
{
    private const uint FlagsAtRest = 0x32080;
    private const uint FlagsInTheFall = 0x32180;
    private const uint FlagsAfterTheSplash = 0x32100;
    private const int NativeRecord = 15;
    private const int ScriptedRecord = 28;
    private const int DropOfTheMap158 = 42;
    private const int Cycle = 88;

    public WaterDropWorldTests()
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

    // -----------------------------------------------------------------------------------------
    // the cycle of a drop that falls on nothing (T0 (d) section 1), shared by (w1) and (w2)
    // -----------------------------------------------------------------------------------------

    private static void AssertTheRestoration(WaterDropRun.Track drop, int tick, int program, string where)
    {
        var s = drop.At(tick);
        Assert.True(s.Present, where);
        Assert.Equal((EntityStatus.Normal, (byte)0, 0u), (s.Status, s.State, s.Animation));
        Assert.Equal(FlagsAtRest, s.Flags); // Gravity out, Collidable back
        Assert.Equal(0, s.ForceZ);
        Assert.Equal(160 << 16, s.PosZRaw); // the rise of 160 px
        Assert.Equal(program, s.Program); // the program C given back (A19's byte)
        Assert.True(s.RootFollows, $"{where}: the root is at ({s.RootX}, {s.RootZ}), the logical position says ({s.WantedX}, {s.WantedZ})");
    }

    /// <summary>The cycle that starts at the tick <paramref name="r"/> (the state 0 armed or restored) of a drop with no script: the table of T0 (d) section 1, relative to R.</summary>
    private static void AssertTheNativeCycle(WaterDropRun run, WaterDropRun.Track drop, int r)
    {
        var where = "R = " + r;
        Assert.Equal((byte)0, drop.At(r).State);

        // R + 1 to R + 40: the state 1, the counter 40 down to 1.
        for (var k = 1; k <= 40; k++)
        {
            var s = drop.At(r + k);
            Assert.True((byte)1 == s.State && (short)(41 - k) == s.Counter, $"{where}, R+{k}: state {s.State}, counter {s.Counter}");
            Assert.Equal(FlagsAtRest, s.Flags);
            Assert.Equal(160 << 16, s.PosZRaw);
        }

        // R + 41: the fall starts (animation 1, Gravity); the drop is in the air until R + 64.
        var fall = drop.At(r + 41);
        Assert.Equal((byte)2, fall.State);
        Assert.Equal(1u, fall.Animation);
        Assert.Equal(FlagsInTheFall, fall.Flags);
        for (var k = 41; k <= 64; k++)
        {
            var s = drop.At(r + k);
            Assert.True((byte)2 == s.State && 0 == s.OnGround && 0 == s.CollidedZ && s.Z > 0, $"{where}, R+{k}: state {s.State}, ground {s.OnGround}, collz {s.CollidedZ}, z {s.Z}");
        }

        // R + 65: the landing, the two flags at the same tick, Z 0; the state 2 sees it at R + 66.
        var landing = drop.At(r + 65);
        Assert.Equal((byte)2, landing.State);
        Assert.Equal((1, 1, 0), (landing.OnGround, landing.CollidedZ, landing.PosZRaw));

        // R + 66: the state 3, the animation 2, the sound 145, once.
        var splash = drop.At(r + 66);
        Assert.Equal((byte)3, splash.State);
        Assert.Equal(2u, splash.Animation);
        Assert.Equal(FlagsInTheFall, splash.Flags);
        Assert.Equal(1, run.SoundsAt(r + 66, 145));

        // R + 67 to R + 87: the state 4, Collidable out; the animation 2 is a Hold of 7 frames of 3 ticks, its end (+0xAC) at R + 87.
        for (var k = 67; k <= 87; k++)
        {
            var s = drop.At(r + k);
            Assert.True((byte)4 == s.State && FlagsAfterTheSplash == s.Flags, $"{where}, R+{k}: state {s.State}, flags {s.Flags:X}");
            Assert.True((k < 87) == (0 == s.Reset), $"{where}, R+{k}: the end of the animation is {s.Reset}");
        }
    }

    // -----------------------------------------------------------------------------------------
    // (w1) a native drop (the template of the map 92, born by the 0x8A): three cycles
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W1_ANativeDropOfTheMap92_CyclesEvery88Ticks_FallsAt41_LandsAt65_SplashesAt66WithTheSound145_AndRestoresItselfAt88(int ticksPerFrame)
    {
        using var run = new WaterDropRun("w1-" + ticksPerFrame);
        var drop = run.Watch(run.BornAt(NativeRecord, 300, 100, 160));
        run.Go(ticksPerFrame, 270);

        foreach (var r in new[] { 1, 1 + Cycle, 1 + 2 * Cycle })
        {
            AssertTheNativeCycle(run, drop, r);
            // the restoration (R + 88): state 0, animation 0, ForceZ 0, Gravity out, Collidable back, 160 px up, the program given back, the root where the logic is.
            AssertTheRestoration(drop, r + Cycle, 0, "restoration at " + (r + Cycle));
        }

        // the sound 145 once per cycle, at R + 66, and nothing else is heard.
        Assert.Equal(new[] { 67, 155, 243 }, run.SoundTicks(145));
        Assert.Equal(3, run.Heard.Played.Count);
        Assert.All(run.Hero.Values, h => Assert.Equal(10, h.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (w2) a scripted drop with a simple wait (the record 28, 37 [30] ; 40 [2,0]): the period N + 90, the program given back, the script resumed from its first opcode
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W2_AScriptedDropWithASimpleWait_KeepsItsProgramInA19_CyclesEveryNPlus90_AndItsScriptRestartsAfterTheRestoration(int ticksPerFrame)
    {
        const int N = 30;
        using var run = new WaterDropRun("w2-" + ticksPerFrame);
        var drop = run.Watch(run.BornAt(ScriptedRecord, 300, 100, 160));
        run.Go(ticksPerFrame, 170);

        // A19 at the first pass, before any pass of C71: the low byte of the program C, kept; the script waits N ticks and its 40 starts the drop.
        Assert.Equal((157, 157), ((int)drop.At(1).Saved, drop.At(1).Program));
        for (var tick = 1; tick < 34; tick++)
        {
            Assert.Equal((byte)0, drop.At(tick).State);
            Assert.Equal(157, (int)drop.At(tick).Saved);
        }

        // S = 34: the first state 1 (the counter 40). The cycle is the native one from S - 1 on.
        var s = drop.First(x => x.State == 1);
        Assert.Equal(34, s);
        Assert.Equal(40, (int)drop.At(s).Counter);
        Assert.Equal((byte)2, drop.At(s + 40).State);
        Assert.Equal(1u, drop.At(s + 40).Animation);
        Assert.Equal((1, 1), (drop.At(s + 64).OnGround, drop.At(s + 64).CollidedZ));
        Assert.Equal((byte)3, drop.At(s + 65).State);
        Assert.Equal(1, run.SoundsAt(s + 65, 145));
        Assert.Equal((byte)4, drop.At(s + 66).State);
        Assert.Equal(1, drop.At(s + 86).Reset);

        // S + 87 = 121: the restoration; the program C is back (the script is armed again, the 40 had cleared it), the drop waits at rest.
        AssertTheRestoration(drop, s + 87, 157, "restoration at S + 87");
        Assert.Equal(157, drop.At(s + 88).Program);
        for (var tick = s + 88; tick < s + N + 90; tick++)
        {
            Assert.Equal((byte)0, drop.At(tick).State);
        }

        // the script restarted from its first opcode: the wait of N ticks runs again, the 40 clears the program at S + 87 + N + 2, the next state 1 comes at S + N + 90 (T0 (d) section 2).
        Assert.Equal(0, drop.At(s + 87 + N + 2).Program);
        Assert.Equal((byte)1, drop.At(s + N + 90).State);
        Assert.Equal(40, (int)drop.At(s + N + 90).Counter);
        Assert.Equal(s + N + 90, drop.All(x => x.State == 1).Where(t => t > s + 87).Min());
    }

    // -----------------------------------------------------------------------------------------
    // (w3) (w4) (w5) the three contacts (T0 (d) section 3): the hero who carries a torch, the hero who carries nothing, a torch posed on the ground
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W3_ADropOnAlundraWhoCarriesATorch_PutsItOutInHisHands_TheSound142_TheEffect10_NoFlame_AndAlundraStaysStandingUnhurt(int ticksPerFrame)
    {
        using var run = new WaterDropRun("w3-" + ticksPerFrame, 109, 100);
        var torch = run.Watch(run.CarryATorch());
        var drop = run.Watch(run.BornAt(NativeRecord, 109, 100, 160));
        var hp = run.Arc.Hero.Hp;
        run.Go(ticksPerFrame, 100);

        // d = 42: the fall starts. The landing on the head of the hero (Z 32) at d + 22, the drop's support is the hero at d + 23 (state 3, the sound 145).
        Assert.Equal(42, drop.First(x => x.State == 2));
        Assert.Equal((1, 32.0), (drop.At(64).OnGround, drop.At(64).Z));
        Assert.Equal(65, drop.First(x => x.State == 3));
        Assert.Equal('H', drop.At(65).Rides);
        Assert.Equal(new[] { 65 }, run.SoundTicks(145));

        // d + 24 = 66: the torch is put out (status 3, the mark of E20), the drop is in the state 4 with Collidable out and no longer rides the hero.
        Assert.Equal(66, drop.First(x => x.State == 4));
        Assert.Equal(FlagsAfterTheSplash, drop.At(66).Flags);
        Assert.Equal('-', drop.At(66).Rides);
        Assert.Equal(66, torch.First(x => x.Status == EntityStatus.Deactivated));
        Assert.Equal((byte)1, torch.At(66).State);
        for (var tick = 1; tick < 66; tick++)
        {
            Assert.Equal(EntityStatus.Normal, torch.At(tick).Status);
            Assert.Equal((byte)0, torch.At(tick).State);
        }

        // d + 25 = 67: the animation 2 and the sound 142 (once); the end of the animation, the destruction (the carried pointer cleared) and the freeing at d + 28 = 70 and d + 29 = 71.
        Assert.Equal(2u, torch.At(67).Animation);
        Assert.Equal(new[] { 67 }, run.SoundTicks(142));
        Assert.Equal(70, torch.First(x => x.Status == EntityStatus.FlagToDestroy));
        Assert.Equal(71, torch.First(x => !x.Present));
        // the break effect 10 is queued by the destruction at d + 28 = 70 (the binary attaches it at that tick, `fxattach` of the journal) and the stage makes it at the end of its frame: seen once, from the tick after to the tick after the frame.
        Assert.InRange(Assert.Single(run.Effects, e => e.Id == 10).Tick, 71, 70 + ticksPerFrame + 1);

        // no flame, ever: the mark made the torch go without one.
        Assert.All(run.Flames.Values, count => Assert.Equal(0, count));
        Assert.Equal(0, run.Arc.Entities.Count(e => e.SpriteType == WaterDropRun.FlameType));

        // Alundra: the hands full until d + 27, empty from d + 28; standing (the animation 0) at d + 29; his life never moved.
        for (var tick = 1; tick <= 100; tick++)
        {
            Assert.Equal(hp, run.Hero[tick].Hp);
            Assert.Equal(tick < 70, run.Hero[tick].Carrying);
        }

        Assert.Equal(0u, run.Hero[71].TargetAnimation);
        Assert.All(Enumerable.Range(71, 30), tick => Assert.Equal(0u, run.Hero[tick].TargetAnimation));
        Assert.Null(run.Arc.Hero.CarriedEntity);
    }

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W4_ADropOnAlundraWithoutATorch_MakesTheSplashAlone_TheDropPassesTheStates3And4_AndRestoresItselfAt87(int ticksPerFrame)
    {
        using var run = new WaterDropRun("w4-" + ticksPerFrame, 109, 100);
        var drop = run.Watch(run.BornAt(NativeRecord, 109, 100, 160));
        var hp = run.Arc.Hero.Hp;
        run.Go(ticksPerFrame, 100);

        Assert.Equal(42, drop.First(x => x.State == 2));
        Assert.Equal((1, 32.0), (drop.At(64).OnGround, drop.At(64).Z));
        Assert.Equal(65, drop.First(x => x.State == 3));
        Assert.Equal('H', drop.At(65).Rides);
        Assert.Equal(66, drop.First(x => x.State == 4));
        Assert.Equal(FlagsAfterTheSplash, drop.At(66).Flags); // Collidable out, with or without a torch
        Assert.Equal(Enumerable.Range(66, 21).ToList(), drop.All(x => x.State == 4).Where(t => t < 100).ToList());

        // the end of the animation at d + 44 = 86, the restoration at d + 45 = 87.
        Assert.Equal(86, drop.First(x => x.State == 4 && x.Reset != 0));
        AssertTheRestoration(drop, 87, 0, "restoration at 87");
        Assert.Equal((byte)1, drop.At(88).State);

        // the sound of the splash alone; nothing else is heard, no effect is made, the hero is untouched.
        Assert.Equal(new[] { 65 }, run.SoundTicks(145));
        Assert.Single(run.Heard.Played);
        Assert.Empty(run.Effects.Where(e => e.Id == 10));
        Assert.All(run.Flames.Values, count => Assert.Equal(0, count));
        Assert.All(run.Hero.Values, h => Assert.Equal((hp, 0u, false), (h.Hp, h.TargetAnimation, h.Carrying)));
    }

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W5_ADropOnATorchPosedOnTheGround_PutsItOutWithoutAFlame_OneTickAfterTheContactOnTheHero(int ticksPerFrame)
    {
        using var run = new WaterDropRun("w5-" + ticksPerFrame, 100, 100);
        var posed = run.PlaceATorch(130, 100, 0);
        run.Torch = posed;
        var torch = run.Watch(posed);
        var drop = run.Watch(run.BornAt(NativeRecord, 130, 100, 160));
        run.Go(ticksPerFrame, 110);

        // d = 42; the landing on the top of the torch (Z 15) at d + 23 = 65, the support is the torch at d + 24 = 66.
        Assert.Equal(42, drop.First(x => x.State == 2));
        Assert.Equal((1, 15.0), (drop.At(65).OnGround, drop.At(65).Z));
        Assert.Equal(66, drop.First(x => x.State == 3));
        Assert.Equal('T', drop.At(66).Rides);
        Assert.Equal(new[] { 66 }, run.SoundTicks(145));

        // d + 25 = 67: status 3 and the mark; d + 26 = 68: the animation 2 and the sound 142; d + 29 = 71 the destruction; d + 30 = 72 the freeing; no flame.
        Assert.Equal(67, drop.First(x => x.State == 4));
        Assert.Equal(67, torch.First(x => x.Status == EntityStatus.Deactivated));
        Assert.Equal((byte)1, torch.At(67).State);
        Assert.Equal(2u, torch.At(68).Animation);
        Assert.Equal(new[] { 68 }, run.SoundTicks(142));
        Assert.Equal(71, torch.First(x => x.Status == EntityStatus.FlagToDestroy));
        Assert.Equal(72, torch.First(x => !x.Present));
        // the break effect 10: queued by the destruction at d + 29 = 71, made by the stage at the end of its frame (see (w3)).
        Assert.InRange(Assert.Single(run.Effects, e => e.Id == 10).Tick, 72, 71 + ticksPerFrame + 1);
        Assert.All(run.Flames.Values, count => Assert.Equal(0, count));

        // the hero, a few pixels away, is not touched; the drop falls from the top of the torch to the floor (Z 0 at d + 32 = 74) and is restored at d + 46 = 88.
        Assert.All(run.Hero.Values, h => Assert.Equal((10, 0u, false), (h.Hp, h.TargetAnimation, h.Carrying)));
        Assert.Equal(74, drop.First(x => x.State == 4 && x.PosZRaw == 0));
        AssertTheRestoration(drop, 88, 0, "restoration at 88");
    }

    // -----------------------------------------------------------------------------------------
    // (w6) the window: the drop puts out the carried torch when its centre falls at dx -11 to +12 and dy -8 to +8 pixels of the centre of the hero (replay/out_s3_hit_window.txt)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(12, 0, true)]
    [InlineData(13, 0, false)]
    [InlineData(-11, 0, true)]
    [InlineData(-12, 0, false)]
    [InlineData(0, 8, true)]
    [InlineData(0, 9, false)]
    [InlineData(0, -8, true)]
    [InlineData(0, -9, false)]
    public void W6_TheDropPutsOutTheCarriedTorchInAWindowOfDxMinus11To12AndDyMinus8To8_AndNotAPixelOutside(int dx, int dy, bool putOut)
    {
        using var run = new WaterDropRun($"w6-{dx}-{dy}", 109, 100);
        var torch = run.Watch(run.CarryATorch());
        var drop = run.Watch(run.BornAt(NativeRecord, 109 + dx, 100 + dy, 160));
        run.Go(1, 100);

        Assert.Equal(42, drop.First(x => x.State == 2));
        if (putOut)
        {
            Assert.Equal(66, torch.First(x => x.Status == EntityStatus.Deactivated));
            Assert.Equal((byte)1, torch.At(66).State);
            Assert.Equal(new[] { 67 }, run.SoundTicks(142));
        }
        else
        {
            Assert.Equal(-1, torch.First(x => x.Status != EntityStatus.Normal));
            Assert.All(Enumerable.Range(1, 100), tick => Assert.Equal((byte)0, torch.At(tick).State));
            Assert.Empty(run.SoundTicks(142));
            Assert.All(run.Hero.Values, h => Assert.True(h.Carrying));
        }

        Assert.All(run.Hero.Values, h => Assert.Equal(10, h.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (w7) the map 158: a drop that waits for Alundra to be in its box (00 ; 3B [box] ; 04 -8 ; 37 [60] ; 40 [2,0]), period N + 91 while he stays
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void W7_ADropOfTheMap158_HangsMotionlessWhileAlundraIsOutsideItsBox_FallsWhenHeEnters_AndCyclesEveryNPlus91WhileHeStays(int ticksPerFrame)
    {
        const int N = 60;
        const int EntryTick = 121;
        // the box of the program C 170 is the tiles x 2 to 10, y 39 to 59 (dump_prog.py 158 C170): the hero outside is at the tile (20, 10), inside at the tile (5, 45).
        using var run = new WaterDropRun("w7-" + ticksPerFrame, 20 * 24 + 12, 10 * 16 + 8, zone: "Fairy cave", world: "Fairy cave-158");
        var drop = run.Watch(run.BornAt(DropOfTheMap158, 300, 700, 160));
        run.Go(ticksPerFrame, 420, beforeFrame: tick =>
        {
            if (tick == EntryTick)
            {
                run.Arc.PlaceHero(5 * 24 + 12, 45 * 16 + 8, 0);
            }
        });

        // outside the box: the program C (170) is given by A19 and the drop does not leave the state 0 (the script loops on the box test).
        Assert.Equal(170, drop.At(1).Saved);
        for (var tick = 1; tick < EntryTick; tick++)
        {
            var s = drop.At(tick);
            Assert.True((byte)0 == s.State && 160 << 16 == s.PosZRaw, $"tick {tick}: state {s.State}, z {s.Z}");
        }

        Assert.Empty(run.Heard.Played.Where(p => p.Tick < EntryTick));

        // inside the box: the wait of N ticks, then the native cycle (state 2 at S + 40, landing at S + 64, the sound 145 at S + 65, the restoration at S + 87).
        var s1 = drop.First(x => x.State == 1);
        Assert.InRange(s1, EntryTick + N, EntryTick + N + 4);
        Assert.Equal(40, (int)drop.At(s1).Counter);
        Assert.Equal((byte)2, drop.At(s1 + 40).State);
        Assert.Equal((1, 1), (drop.At(s1 + 64).OnGround, drop.At(s1 + 64).CollidedZ));
        Assert.Equal(new[] { s1 + 65 }, run.SoundTicks(145).Where(t => t < s1 + 87).ToArray());
        AssertTheRestoration(drop, s1 + 87, 170, "restoration at S + 87");

        // he stays in the box: the next state 1 at S + N + 91 (one tick more than the simple wait: the box test), not before.
        for (var tick = s1 + 88; tick < s1 + N + 91; tick++)
        {
            Assert.Equal((byte)0, drop.At(tick).State);
        }

        Assert.Equal((byte)1, drop.At(s1 + N + 91).State);
    }

    // -----------------------------------------------------------------------------------------
    // (w9) the witness: a torch thrown against a drop (the second site of the type 391 of the binary, the branch of E20 of g2c2) goes without a flame, before and after the drops move
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void W9_ATorchThrownAgainstADrop_IsPutOutWithoutAFlame()
    {
        // the drop is the map-page sprite 135 (the type 391) at rest on the path of the throw; its handlers are OFF (the witness does not depend on the phase B).
        using var run = new WaterDropRun("w9", 109, 100, new[] { WaterDropRun.TorchType, WaterDropRun.FlameType });
        var torch = run.Watch(run.CarryATorch());
        var drop = run.World.SpawnEntityBySpriteIndex(null, true, 135, 165 << 16, 100 << 16, 0, 0x10) ?? throw new InvalidOperationException("the drop was not born");
        Assert.Equal(391, drop.SpriteType);
        // the montage is synthetic: the DLL's thrown torch does not report the obstacle it meets in flight (<c>XCollisionEntity</c> is written by the scripted steps of the controller only), so the contact the binary reports (+0x130) is posed at every tick, as the vectors of E20 pose it.
        run.Go(1, 140, tick => tick == 1 ? PadState.Square | PadState.Right : tick < 4 ? PadState.Right : 0u, beforeFrame: _ => torch.Entity.XCollisionEntity = drop);

        var burning = torch.First(x => x.Status == EntityStatus.Deactivated);
        Assert.InRange(burning, 2, 60);
        Assert.Equal((byte)1, torch.At(burning).State); // the mark
        Assert.Equal(burning + 3, torch.First(x => x.Status == EntityStatus.FlagToDestroy));
        Assert.Equal(burning + 4, torch.First(x => !x.Present));
        Assert.All(run.Flames.Values, count => Assert.Equal(0, count));
        Assert.Single(run.SoundTicks(142));
        Assert.All(run.Hero.Values, h => Assert.Equal(10, h.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (w10) after the state 3 the drop goes through the hero, ends its fall on the ground and rises 160 px from THAT height
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W10_AfterTheState3TheDropGoesThroughAlundra_EndsItsFallOnTheGround_AndRisesFromThereAtTheRestoration(int ticksPerFrame)
    {
        using var run = new WaterDropRun("w10-" + ticksPerFrame, 109, 100);
        var drop = run.Watch(run.BornAt(NativeRecord, 109, 100, 160));
        run.Go(ticksPerFrame, 100);

        // on the head at Z 32 until the state 3; then no support, no obstacle: the drop falls through him and is on the ground at d + 34 = 76.
        Assert.Equal(32.0, drop.At(65).Z);
        var previous = drop.At(65).Z;
        for (var tick = 66; tick <= 76; tick++)
        {
            var s = drop.At(tick);
            Assert.Equal('-', s.Rides);
            Assert.True(s.Z < previous, $"tick {tick}: z {s.Z} after {previous}");
            previous = s.Z;
        }

        Assert.Equal(0.0, drop.At(76).Z);
        Assert.Equal((1, 1), (drop.At(76).OnGround, drop.At(76).CollidedZ));
        for (var tick = 77; tick <= 86; tick++)
        {
            Assert.Equal(0, drop.At(tick).PosZRaw);
            Assert.Equal((byte)4, drop.At(tick).State);
        }

        // the restoration at d + 45 = 87: 160 px above the ground it reached (Z 160 exactly), the root with it.
        AssertTheRestoration(drop, 87, 0, "restoration at 87");
        Assert.Equal(160.0, drop.At(87).Z);
        Assert.Equal(160f, drop.At(87).RootZ);
        Assert.Equal((byte)1, drop.At(88).State);
        Assert.Equal(160 << 16, drop.At(88).PosZRaw);
    }
}
