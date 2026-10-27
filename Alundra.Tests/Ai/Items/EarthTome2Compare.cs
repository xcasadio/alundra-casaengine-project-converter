#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1: the comparison of a run of the DLL (<see cref="EarthTome2Run"/>) with a key (<see cref="EarthTome2Key"/>), tick by tick, field by field; every difference is a line of text (an empty list is equality). The DLL's Z is the binary's minus 1 (the rest height is 0
/// for the DLL, 1 in the keys: the comparison adds 1 to the DLL's Z, as the benches of f4 to f6 and h4c1 state). R-EL-5 (extended to the position by (s10a)): <paramref name="positionBound"/> is the number of raw units a rock's X and Y may differ from the key's, 0 for every rock
/// but the rock held against a wall by the corrected game (the rock 8 of the plateaus, 715 680 on each axis).
/// </summary>
internal static class EarthTome2Compare
{
    internal static List<string> Rocks(EarthTome2Key key, EarthTome2Run run, int last, Func<EarthTome2Key.KeyRock, int>? positionBound = null)
    {
        var wrong = new List<string>();
        var keyRocks = key.Rocks.Where(r => r.Birth <= last).ToList();
        var lives = run.Lives();
        if (keyRocks.Count != lives.Count)
        {
            wrong.Add($"{lives.Count} lives of rocks (key {keyRocks.Count} up to the tick {last})");
        }

        foreach (var (k, life) in keyRocks.Zip(lives))
        {
            var label = $"rock {k.Id} (born {k.Birth})";
            if (k.Birth != life.Birth || k.Slot != life.Slot)
            {
                wrong.Add($"{label}: born at {life.Birth} in the slot {life.Slot} (key slot {k.Slot})");
                continue;
            }

            var bound = positionBound?.Invoke(k) ?? 0;
            var ticks = k.Track.Where(r => r.Tick <= last).Select(r => r.Tick).ToList();
            var seen = life.Rows.Select(r => r.Tick).ToList();
            if (!ticks.SequenceEqual(seen))
            {
                wrong.Add($"{label}: rows from {seen.FirstOrDefault()} to {seen.LastOrDefault()} (key {ticks.FirstOrDefault()} to {ticks.LastOrDefault()})");
            }

            foreach (var row in k.Track.Where(r => r.Tick <= last))
            {
                var r = life.At(row.Tick);
                if (r is null)
                {
                    continue;
                }

                void Check(string field, long got, long want)
                {
                    if (got != want)
                    {
                        wrong.Add($"{label} t{row.Tick} {field}: {got} (key {want})");
                    }
                }

                Check("status", r.Status, row.Status);
                Check("animation asked", r.TargetAnimation, row.TargetAnimation);
                Check("z+1", r.PosZ + 1, row.Z);
                Check("end of animation", r.AnimationEnd, row.AnimationEnd);
                Check("forceAdjusted", r.Adjusted, row.Adjusted);
                // A rock destroyed on the spot at its birth (a plateau above it) is destroyed before its first physics pass: the DLL's ground contact is still 0, the key's row reads the word of the slot template (1). Nothing reads it: the slot is freed.
                if (!(row.Tick == k.Birth && row.Status == 4))
                {
                    Check("ground", r.Ground, row.Ground);
                }
                Check("flags", r.Flags, row.Flags);
                if (Math.Abs((long)r.PosX - row.X) > bound || Math.Abs((long)r.PosY - row.Y) > bound)
                {
                    wrong.Add($"{label} t{row.Tick} position ({r.PosX}, {r.PosY}) (key ({row.X}, {row.Y}), bound {bound})");
                }
            }

            // The birth: the direction of the rock is the second draw.
            if (life.Rows[0].Direction != (uint)k.Direction && life.Rows[0].Status != 4)
            {
                wrong.Add($"{label}: direction {life.Rows[0].Direction} at its birth (key {k.Direction})");
            }
        }

        return wrong;
    }

    /// <summary>The words of each tick: the generator at the head (one tick a frame) and its steps in the frame, the camera kick, the hero, the item, the magic.</summary>
    internal static List<string> Ticks(EarthTome2Key key, EarthTome2Run run, int last, int ticksPerFrame = 1)
    {
        var wrong = new List<string>();
        for (var tick = 1; tick <= last; tick++)
        {
            var k = key.Ticks[tick];
            if (ticksPerFrame == 1 && run.StateAtHead[tick] != k.Rng0)
            {
                wrong.Add($"t{tick}: generator {run.StateAtHead[tick]} at the head (key {k.Rng0})");
            }

            if (ticksPerFrame == 1 && run.FrameSteps[tick] != EarthTome2Run.Steps(k.Rng0, k.Rng1) + 1)
            {
                wrong.Add($"t{tick}: {run.FrameSteps[tick]} steps of the generator (key {EarthTome2Run.Steps(k.Rng0, k.Rng1)} draws + 1)");
            }

            if (run.Kicks[tick] != k.Kick)
            {
                wrong.Add($"t{tick}: camera kick {run.Kicks[tick]} (key {k.Kick})");
            }

            var h = run.Hero[tick];
            if ((h.Animation, h.PosZ + 1, (int)h.ItemInUse, h.Phase, h.Counter, h.Mp) != (k.HeroAnimation, k.HeroZ, k.Item, k.Phase, k.Counter, k.Mp))
            {
                wrong.Add($"t{tick}: hero animation {h.Animation:X} z {h.PosZ + 1}, item {h.ItemInUse:X} phase {h.Phase} counter {h.Counter}, magic {h.Mp} (key {k.HeroAnimation:X} z {k.HeroZ}, item {k.Item:X} phase {k.Phase} counter {k.Counter}, magic {k.Mp})");
            }
        }

        return wrong;
    }

    /// <summary>The sounds of every tick of <paramref name="from"/> to <paramref name="to"/> (as a sorted list), the ones in <paramref name="ignored"/> left out on both sides.</summary>
    internal static List<string> Sounds(IReadOnlyList<(int Tick, int Sfx)> key, EarthTome2Run run, int from, int to, params int[] ignored)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var expected = key.Where(s => s.Tick == tick && !ignored.Contains(s.Sfx)).Select(s => s.Sfx).Order().ToList();
            var actual = run.Heard.Played.Where(p => p.Tick == tick && !ignored.Contains(p.Sfx)).Select(p => p.Sfx).Order().ToList();
            if (!expected.SequenceEqual(actual))
            {
                wrong.Add($"t{tick}: sounds [{string.Join(",", actual)}] (key [{string.Join(",", expected)}])");
            }
        }

        return wrong;
    }

    /// <summary>An interruption: from the tick before it on, the hero, the item and each rock by slot against the key of the oracle.</summary>
    internal static List<string> Cancelled(EarthTome2Key.CancelKey key, EarthTome2Run run)
    {
        var wrong = new List<string>();
        foreach (var (tick, k) in key.Ticks.OrderBy(t => t.Key))
        {
            var h = run.Hero[tick];
            if ((h.Animation, (int)h.ItemInUse, h.Phase) != (k.HeroAnimation, k.Item, k.Phase))
            {
                wrong.Add($"t{tick}: hero animation {h.Animation:X}, item {h.ItemInUse:X} phase {h.Phase} (key {k.HeroAnimation:X}, item {k.Item:X} phase {k.Phase})");
            }

            if (run.Kicks[tick] != k.Kick)
            {
                wrong.Add($"t{tick}: camera kick {run.Kicks[tick]} (key {k.Kick})");
            }

            var rocks = run.Rocks[tick];
            if (!rocks.Keys.Order().SequenceEqual(k.Rocks.Keys.Order()))
            {
                wrong.Add($"t{tick}: rocks in the slots [{string.Join(",", rocks.Keys.Order())}] (key [{string.Join(",", k.Rocks.Keys.Order())}])");
                continue;
            }

            foreach (var (slot, e) in k.Rocks.OrderBy(r => r.Key))
            {
                var r = rocks[slot];
                void Check(string field, long got, long want)
                {
                    if (got != want)
                    {
                        wrong.Add($"t{tick} slot {slot} (rock {e.Id}) {field}: {got} (key {want})");
                    }
                }

                Check("status", r.Status, e.Status);
                Check("animation asked", r.TargetAnimation, e.TargetAnimation);
                Check("z+1", r.PosZ + 1, e.Z);
                Check("end of animation", r.AnimationEnd, e.AnimationEnd);
                Check("flags", r.Flags, e.Flags);
                Check("blocked", r.Blocked ? 1 : 0, e.Blocked ? 1 : 0);
            }
        }

        return wrong;
    }

    internal static string Show(IReadOnlyList<string> wrong) => wrong.Count == 0 ? string.Empty : wrong.Count + " differences: " + Environment.NewLine + string.Join(Environment.NewLine, wrong.Take(12));
}
