#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1: the comparison of a run of the DLL (<see cref="FireTome2Run"/>) with a key (<see cref="FireTome2Key"/>), tick by tick, field by field; every difference is a line of text (an empty list is equality). The DLL's Z is the binary's minus 1 (the rest height is 0
/// for the DLL, 1 in the keys: the comparison adds 1 to the DLL's Z). R-EL-7: the key's bench does not run the step of the shake, the DLL does at every tick, so the block is compared by its five words <c>Flag</c>, <c>LimitX</c>, <c>LimitY</c>, <c>SpeedX</c>, <c>SpeedY</c> at every tick,
/// and by its nine words only at the ticks <paramref name="nineWords"/> names (where the step leaves the other four unchanged: zero limits, or a block at zero).
/// </summary>
internal static class FireTome2Compare
{
    /// <summary>The hero: the item in use, its phase and its counter, the magic at every tick; the animation asked up to <paramref name="animationUntil"/> (R-EL-6 revised: the injury after T is not compared by this slice).</summary>
    internal static List<string> Hero(FireTome2Key key, FireTome2Run run, int last, int animationUntil)
    {
        var wrong = new List<string>();
        for (var tick = 1; tick <= last; tick++)
        {
            var k = key.Ticks[tick];
            var h = run.Hero[tick];
            if ((h.ItemInUse, h.Phase, h.Counter, h.Mp) != ((uint)k.Item, k.Phase, k.Counter, k.Mp))
            {
                wrong.Add($"t{tick}: item {h.ItemInUse:X} phase {h.Phase} counter {h.Counter} magic {h.Mp} (key item {k.Item:X} phase {k.Phase} counter {k.Counter} magic {k.Mp})");
            }

            if (tick <= animationUntil && h.Animation != k.HeroAnimation)
            {
                wrong.Add($"t{tick}: hero animation {h.Animation:X} (key {k.HeroAnimation:X})");
            }
        }

        return wrong;
    }

    /// <summary>The shake block (five words at every tick, nine where <paramref name="nineWords"/> says so) and the buffer of the pilot (nine words at every tick).</summary>
    internal static List<string> Shake(FireTome2Key key, FireTome2Run run, int last, Func<int, bool>? nineWords = null)
    {
        var wrong = new List<string>();
        for (var tick = 1; tick <= last; tick++)
        {
            var k = key.Ticks[tick];
            var count = nineWords?.Invoke(tick) == true ? 9 : 5;
            if (!run.Blocks[tick].Take(count).SequenceEqual(k.Block.Take(count)))
            {
                wrong.Add($"t{tick}: block [{string.Join(",", run.Blocks[tick].Take(count))}] (key [{string.Join(",", k.Block.Take(count))}])");
            }

            if (!run.Buffers[tick].SequenceEqual(k.Buffer))
            {
                wrong.Add($"t{tick}: buffer [{string.Join(",", run.Buffers[tick])}] (key [{string.Join(",", k.Buffer)}])");
            }
        }

        return wrong;
    }

    /// <summary>The column of fire (the sprite 21): present when the key has it, with its status, flags, position, end of animation, animation asked and hit counter.</summary>
    internal static List<string> Column(FireTome2Key key, FireTome2Run run, int last)
    {
        var wrong = new List<string>();
        for (var tick = 1; tick <= last; tick++)
        {
            var k = key.Ticks[tick].Column;
            var has = run.Columns.TryGetValue(tick, out var c);
            if ((k is null) == has)
            {
                wrong.Add($"t{tick}: column {(has ? "present" : "absent")} (key {(k is null ? "absent" : "present")})");
                continue;
            }

            if (k is null || c is null)
            {
                continue;
            }

            void Check(string field, long got, long want)
            {
                if (got != want)
                {
                    wrong.Add($"t{tick} column {field}: {got} (key {want})");
                }
            }

            Check("status", c.Status, k.Status);
            Check("flags", c.Flags, k.Flags);
            Check("x", c.PosX, k.X);
            Check("y", c.PosY, k.Y);
            Check("z+1", c.PosZ + 1, k.Z);
            Check("end of animation", c.AnimationEnd, k.AnimationEnd);
            Check("animation asked", c.TargetAnimation, k.AnimationTarget);
            Check("hit counter", c.HitCounter, k.HitCounter);
        }

        return wrong;
    }

    /// <summary>The effects 19 of the animation 2 created at each tick, in order, against the effects of the key (one tick a frame).</summary>
    internal static List<string> Effects(FireTome2Key key, FireTome2Run run, int last)
    {
        var wrong = new List<string>();
        var made = run.ColumnBlasts();
        for (var tick = 1; tick <= last; tick++)
        {
            var expected = key.Ticks[tick].Effects.Select(e => (e.Effect, e.Animation, e.X, e.Y, e.Z)).ToList();
            var actual = made.Where(e => e.Tick == tick).Select(e => (e.Effect, e.Animation, e.X, e.Y, e.Z)).ToList();
            if (!expected.SequenceEqual(actual))
            {
                wrong.Add($"t{tick}: effects [{string.Join(" ", actual)}] (key [{string.Join(" ", expected)}])");
            }
        }

        return wrong;
    }

    /// <summary>The generator at the head of each tick and its steps in the frame (the draws plus the step at the end of the frame), one tick a frame.</summary>
    internal static List<string> Generator(IEnumerable<(int Tick, uint Rng0, uint Rng1)> key, FireTome2Run run, int last)
    {
        var wrong = new List<string>();
        foreach (var (tick, rng0, rng1) in key.Where(k => k.Tick <= last))
        {
            if (run.StateAtHead[tick] != rng0)
            {
                wrong.Add($"t{tick}: generator {run.StateAtHead[tick]} at the head (key {rng0})");
            }

            if (run.FrameSteps[tick] != FireTome2Run.Steps(rng0, rng1) + 1)
            {
                wrong.Add($"t{tick}: {run.FrameSteps[tick]} steps of the generator (key {FireTome2Run.Steps(rng0, rng1)} draws + 1)");
            }
        }

        return wrong;
    }

    internal static List<string> Generator(FireTome2Key key, FireTome2Run run, int last) => Generator(key.Ticks.Values.Select(k => (k.Tick, k.Rng0, k.Rng1)), run, last);

    /// <summary>Everything a cast key holds, tick by tick.</summary>
    internal static List<string> Everything(FireTome2Key key, FireTome2Run run, int last, int animationUntil, Func<int, bool>? nineWords = null)
    {
        var all = new List<string>();
        all.AddRange(Hero(key, run, last, animationUntil));
        all.AddRange(Shake(key, run, last, nineWords));
        all.AddRange(Column(key, run, last));
        all.AddRange(Effects(key, run, last));
        all.AddRange(Generator(key, run, last));
        return all;
    }

    internal static string Show(IReadOnlyList<string> wrong) => wrong.Count == 0 ? string.Empty : wrong.Count + " differences: " + Environment.NewLine + string.Join(Environment.NewLine, wrong.Take(12));
}
