#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T1: the comparison of a run of the DLL (<see cref="EarthTome1Run"/>) with a key (<see cref="EarthTome1Key"/>), tick by tick, field by field; every difference is a line of text (an empty list is equality). The DLL's Z is the binary's minus 1 (the rest height
/// is 0 for the DLL, 1 in the keys: the comparison adds 1 to the DLL's Z, as the benches of f4 to f6 state). The animation row and the current animation of a rock are read at the END of the frame and from the tick after its birth (D-H4C1-3).
/// </summary>
internal static class EarthTome1Compare
{
    /// <summary>The rocks: the same set of slots at every tick, and every field the key holds.</summary>
    internal static List<string> Rocks(EarthTome1Key key, EarthTome1Run run, int from, int to)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var expected = key.At(tick).Rocks;
            var actual = run.Rocks[tick];
            if (!expected.Keys.OrderBy(k => k).SequenceEqual(actual.Keys.OrderBy(k => k)))
            {
                wrong.Add($"t{tick}: slots {string.Join(",", actual.Keys.OrderBy(k => k))} (key {string.Join(",", expected.Keys.OrderBy(k => k))})");
                continue;
            }

            foreach (var (slot, k) in expected.OrderBy(e => e.Key))
            {
                var r = actual[slot];
                void Check(string field, long got, long want)
                {
                    if (got != want)
                    {
                        wrong.Add($"t{tick} slot {slot} {field}: {got} (key {want})");
                    }
                }

                Check("status", r.Status, k.Status);
                Check("x", r.PosX, k.Px);
                Check("y", r.PosY, k.Py);
                Check("z+1", r.PosZ + 1, k.Pz);
                Check("fx", r.ForceX, k.Fx);
                Check("fy", r.ForceY, k.Fy);
                Check("fz", r.ForceZ, k.Fz);
                Check("direction", r.Direction, k.DirT);
                Check("animation asked", r.TargetAnimation, k.AnimT);
                Check("end of animation", r.AnimationEnd, k.ResetAc);
                Check("flags", r.Flags, k.Flags);
                Check("forceAdjusted", r.Adjusted, k.Adj);
                Check("ground", r.Ground, k.Ground);
                Check("slope", r.Slope, k.Slope);
                Check("hit counter", r.Counter, k.C1D0);
                Check("box width", r.Width, k.Bw);
                Check("box depth", r.Depth, k.Bd);
                Check("box height", r.Height, k.Bh);
                if (!IsBirth(key, tick, slot))
                {
                    // From the tick after the birth (D-H4C1-3): the DLL validates the switch of animation at the end of the frame, the binary's UpdateAnimation runs in the tick of the birth.
                    Check("animation row", r.RowAtEnd, k.Adir);
                    Check("current animation", r.CurrentAnimationAtEnd, k.AnimC);
                }
            }
        }

        return wrong;
    }

    /// <summary>The tick of birth of a rock in the key: the slot was free at the tick before (absent, or in the status 4 that frees it for the tick after).</summary>
    private static bool IsBirth(EarthTome1Key key, int tick, int slot)
        => tick == 1 || !key.At(tick - 1).Rocks.TryGetValue(slot, out var before) || before.Status == 4;

    /// <summary>
    /// The rocks against an obstacle in the column of the free flight (a wall, a step: R-EL-5, D-E20-80, D-F4-9): the flag of the wall comes up to <paramref name="maxLead"/> tick EARLIER in the DLL (the rock is at the status 3 that many ticks before the key's), and the step into the obstacle
    /// differs from the binary's search by halves (the DLL clips the blocked axis to the obstacle and keeps the free axis, the binary scales both) so the rock rests a little further along the free axis. What is compared, life by life:
    /// the status 3 of the DLL at the tick S, the key's at S + lead with lead in 0..<paramref name="maxLead"/>; the ticks before S: every field equal but the position from S - 2 on and the flag of the wall at S - 1 when the lead is 1 (the DLL raises it first); the ticks from S on: the DLL's tick against
    /// the key's tick + lead (the chronology is read from the observed status 3), every field equal but the position; the position: exact up to S - 3 and then within <see cref="BlockedAxisBound"/> raw units on the blocked axis (X: the obstacles of the keys are columns) and twice the free-flight force on the free axis (Y). The vertical motion (the height, its force, the ground contact and the slope)
    /// follows the tick, not the status 3: a rock stopped in the air by the obstacle goes on falling.
    /// </summary>
    /// <summary>The distance in raw 16.16 units the binary's search by halves leaves between the rock and the obstacle on the blocked axis (the DLL goes to the contact): 5 at most in the four keys of (w3), 8 is the bound.</summary>
    internal const int BlockedAxisBound = 8;

    internal static List<string> Obstacle(EarthTome1Key key, EarthTome1Run run, int from, int to, int maxLead)
    {
        var wrong = new List<string>();
        for (var slot = 1; slot < 64; slot++)
        {
            var births = Enumerable.Range(from, to - from + 1).Where(t => key.At(t).Rocks.ContainsKey(slot) && IsBirth(key, t, slot)).ToList();
            foreach (var birth in births)
            {
                var keyTicks = new List<int>();
                for (var t = birth; t <= to && key.At(t).Rocks.ContainsKey(slot); t++)
                {
                    keyTicks.Add(t);
                    if (key.At(t).Rocks[slot].Status == 4)
                    {
                        break;
                    }
                }

                var k3 = keyTicks.Where(t => key.At(t).Rocks[slot].Status == 3).Select(t => (int?)t).FirstOrDefault();
                var d3 = Enumerable.Range(birth, keyTicks.Count).Where(t => t <= to && run.Rocks[t].TryGetValue(slot, out var r) && r.Status == 3).Select(t => (int?)t).FirstOrDefault();
                var label = $"slot {slot} born at {birth}";
                if (k3 is null || d3 is null)
                {
                    wrong.Add($"{label}: status 3 at {d3} (key {k3})");
                    continue;
                }

                var lead = k3.Value - d3.Value;
                if (lead < 0 || lead > maxLead)
                {
                    wrong.Add($"{label}: status 3 at {d3} (key {k3}), lead {lead} out of 0..{maxLead}");
                    continue;
                }

                var free = key.At(d3.Value - 3).Rocks[slot];
                var dllTicks = Enumerable.Range(birth, to - birth + 1).TakeWhile(t => run.Rocks[t].ContainsKey(slot)).ToList();
                var end = dllTicks.FindIndex(t => run.Rocks[t][slot].Status == 4);
                if (end >= 0)
                {
                    dllTicks = dllTicks.Take(end + 1).ToList();
                }

                foreach (var t in dllTicks)
                {
                    var keyTick = t < d3 ? t : t + lead;
                    if (keyTick > to || !key.At(keyTick).Rocks.TryGetValue(slot, out var k))
                    {
                        wrong.Add($"{label}: t{t} has no key row at t{keyTick}");
                        continue;
                    }

                    var r = run.Rocks[t][slot];
                    void Same(string field, long got, long want)
                    {
                        if (got != want)
                        {
                            wrong.Add($"{label} t{t} (key t{keyTick}) {field}: {got} (key {want})");
                        }
                    }

                    // The vertical motion goes on after the status 3 (a rock stopped in the air by the obstacle falls on): its height, its force and its ground contact follow the tick, not the status 3.
                    // The tick of the death freezes them at the values of the tick before, in the DLL and in the key (the status 4 is written before the vertical step).
                    var same = run.Rocks[t][slot];
                    var absoluteTick = t == dllTicks[^1] ? t - 1 : t;
                    var absolute = key.At(absoluteTick).Rocks.TryGetValue(slot, out var atTick) ? atTick : null;
                    if (absolute is null)
                    {
                        wrong.Add($"{label} t{t}: the key has no rock in the slot at t{absoluteTick}");
                    }
                    else
                    {
                        Same("z+1", same.PosZ + 1, absolute.Pz);
                        Same("fz", same.ForceZ, absolute.Fz);
                        Same("ground", same.Ground, absolute.Ground);
                        Same("slope", same.Slope, absolute.Slope);
                    }

                    Same("status", r.Status, k.Status);
                    Same("fx", r.ForceX, k.Fx);
                    Same("fy", r.ForceY, k.Fy);
                    Same("direction", r.Direction, k.DirT);
                    Same("animation asked", r.TargetAnimation, k.AnimT);
                    Same("end of animation", r.AnimationEnd, k.ResetAc);
                    Same("flags", r.Flags, k.Flags);
                    if (!(lead == 1 && t == d3.Value - 1))
                    {
                        Same("forceAdjusted", r.Adjusted, k.Adj);
                    }
                    else if (r.Adjusted != 1 || k.Adj != 0)
                    {
                        wrong.Add($"{label} t{t}: the flag of the wall is {r.Adjusted} (key {k.Adj}), the DLL must raise it one tick before the key");
                    }

                    Same("hit counter", r.Counter, k.C1D0);
                    Same("box width", r.Width, k.Bw);
                    Same("box depth", r.Depth, k.Bd);
                    Same("box height", r.Height, k.Bh);
                    if (t != birth)
                    {
                        Same("animation row", r.RowAtEnd, k.Adir);
                        Same("current animation", r.CurrentAnimationAtEnd, k.AnimC);
                    }

                    var exact = t <= d3.Value - 3;
                    var boundX = exact ? 0 : BlockedAxisBound;
                    var boundY = exact ? 0 : 2 * Math.Abs(free.Fy);
                    if (Math.Abs((long)r.PosX - k.Px) > boundX || Math.Abs((long)r.PosY - k.Py) > boundY)
                    {
                        wrong.Add($"{label} t{t} (key t{keyTick}) position ({r.PosX}, {r.PosY}) (key ({k.Px}, {k.Py}), bounds {boundX}, {boundY})");
                    }
                }

                // The life ends 13 ticks after the status 3 in both (the death at S + 13).
                var death = dllTicks[^1];
                if (run.Rocks[death][slot].Status != 4 || death != d3.Value + 13)
                {
                    wrong.Add($"{label}: last tick {death} with the status {run.Rocks[death][slot].Status}, expected the status 4 at {d3 + 13}");
                }
            }
        }

        return wrong;
    }

    /// <summary>The item in use, its phase and its counter, at every tick; with <paramref name="hero"/> also the animation and the position of the hero (the Z plus 1).</summary>
    internal static List<string> Item(EarthTome1Key key, EarthTome1Run run, int from, int to, bool hero = true)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var k = key.At(tick);
            var h = run.Hero[tick];
            if ((h.ItemInUse, h.Phase, h.Counter) != (k.Item.ItemInUse, k.Item.Phase, k.Item.Counter))
            {
                wrong.Add($"t{tick}: item {h.ItemInUse:X} phase {h.Phase} counter {h.Counter} (key {k.Item.ItemInUse:X} {k.Item.Phase} {k.Item.Counter})");
            }

            if (hero && (h.Animation, h.PosX, h.PosY, h.PosZ + 1) != (k.Hero.AnimT, k.Hero.Px, k.Hero.Py, k.Hero.Pz))
            {
                wrong.Add($"t{tick}: hero animation {h.Animation:X} at ({h.PosX}, {h.PosY}, {h.PosZ + 1}) (key {k.Hero.AnimT:X} at ({k.Hero.Px}, {k.Hero.Py}, {k.Hero.Pz}))");
            }
        }

        return wrong;
    }

    /// <summary>The sounds of every tick (as a sorted list), the ones in <paramref name="ignored"/> left out on both sides.</summary>
    internal static List<string> Sounds(EarthTome1Key key, EarthTome1Run run, int from, int to, params int[] ignored)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var expected = key.At(tick).Sounds.Where(s => !ignored.Contains(s)).Order().ToList();
            var actual = run.Heard.Played.Where(p => p.Tick == tick && !ignored.Contains(p.Sfx)).Select(p => p.Sfx).Order().ToList();
            if (!expected.SequenceEqual(actual))
            {
                wrong.Add($"t{tick}: sounds [{string.Join(",", actual)}] (key [{string.Join(",", expected)}])");
            }
        }

        return wrong;
    }

    /// <summary>The draws of the generator of every frame (the key's draws plus the step of the end of the frame) and the effects of the frame (the sparks with the speeds the draws give, the effects attached), as the key writes them.</summary>
    internal static List<string> DrawsAndEffects(EarthTome1Key key, EarthTome1Run run, int from, int to)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var k = key.At(tick);
            if (run.FrameSteps[tick] != k.Draws + 1)
            {
                wrong.Add($"t{tick}: {run.FrameSteps[tick]} steps of the generator (key {k.Draws} draws + 1)");
            }

            var expected = k.Effects.Select(e => Normalize(key, tick, e)).Order().ToList();
            var actual = run.Effects[tick].Order().ToList();
            if (!expected.SequenceEqual(actual))
            {
                wrong.Add($"t{tick}: effects [{string.Join(" ", actual)}] (key [{string.Join(" ", expected)}])");
            }
        }

        return wrong;
    }

    /// <summary>
    /// The key's effect in the words of <see cref="EarthTome1Run.Effects"/>: a spark keeps its identifier and its centre (not its direction, nor the speeds, which the stage and the draws give); an effect attached to the slot N of the key is
    /// the position of the rock N in the row (the DLL's anchor is a snapshot of the entity, taken at its destruction).
    /// </summary>
    private static string Normalize(EarthTome1Key key, int tick, string effect)
    {
        var spark = System.Text.RegularExpressions.Regex.Match(effect, @"^fx\(id=(\d+),dir=\d+,(x=[^,]+,y=[^,]+,z=[^,]+),v=");
        if (spark.Success)
        {
            return $"fx(id={spark.Groups[1].Value},{spark.Groups[2].Value})";
        }

        var attach = System.Text.RegularExpressions.Regex.Match(effect, @"^attach\(id=(\d+),target=slot(\d+),mode=\d+\)$");
        if (attach.Success)
        {
            var rock = key.At(tick).Rocks.TryGetValue(int.Parse(attach.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), out var found) ? found : null;
            return rock is null ? effect : $"attach(id={attach.Groups[1].Value},x={rock.Px},y={rock.Py},z={rock.Pz})";
        }

        return effect;
    }

    /// <summary>The first target (an inert Beannoid): its status, its hit counter (+0x1D0), the slot of the entity that touches it, its place.</summary>
    internal static List<string> Target(EarthTome1Key key, EarthTome1Run run, int from, int to)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var k = key.At(tick).Others.Single().Value;
            var t = run.Target[tick];
            if ((t.Status, t.Counter, t.TouchedBy, t.PosX, t.PosY) != (k.Status, k.C1D0, k.TouchSlot, k.Px, k.Py))
            {
                wrong.Add($"t{tick}: target status {t.Status} counter {t.Counter} touched by {t.TouchedBy} at ({t.PosX}, {t.PosY}) (key {k.Status} {k.C1D0} {k.TouchSlot} ({k.Px}, {k.Py}))");
            }
        }

        return wrong;
    }

    internal static string Show(IReadOnlyList<string> wrong) => wrong.Count == 0 ? string.Empty : wrong.Count + " differences: " + Environment.NewLine + string.Join(Environment.NewLine, wrong.Take(12));
}
