#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Entities;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d2 T1 bis: the comparison of a run of the DLL (<see cref="FireTome1Run"/>) with a key (<see cref="FireTome1Key"/>), tick by tick, field by field; every difference is a line of text (an empty list is equality). The DLL's Z is the binary's minus 1 (the rest height is 0 for the
/// DLL, 1 in the keys: the comparison adds 1 to the DLL's Z, as the benches of f4 to h4c2 state). The columns compared (critic section 5, point 12): the hero (animation asked, current animation, X, Y, Z), the item (in use, phase, counter, magic), the pointer kept, the generator and, per entity, the status,
/// the type, the flags, X, Y, Z, the forces, the animations, the directions, the animation row, the end of animation, the wait counter, the target slot, the blocker, the two counters, the touch, the slope, the ground, the box of the hit pass, the attack, the hits and the events. NEVER <c>id</c>
/// (the relation "same entity" replaces it), <c>b27c</c>, <c>count</c> or <c>ident</c>. The hit points of a block are not compared: the block of the sprite 25 of the DLL's bench has no damage pass (the key's <c>resolved</c> is the oracle's own stub of it); the touches that cause the damage (the counters, the touch, the hits, the sparks) are. The current animation, the direction and the row are read at the END of the frame and from the tick after the birth (D-H4C1-3), but not for an entity at the status 4 of the tick (the DLL does not validate the animation of an entity destroyed in the tick: nothing sees it).
/// </summary>
internal static class FireTome1Compare
{
    /// <summary>The tick of birth of an entity in the key: the slot was free at the tick before (absent, or in the status 4 that frees it for the tick after), or held another entity.</summary>
    private static bool IsBirth(FireTome1Key key, int tick, int slot)
        => tick == 1 || !key.At(tick - 1).Ents.TryGetValue(slot, out var before) || before.Status == 4 || before.Id != key.At(tick).Ents[slot].Id;

    /// <summary>The entities: the same set of slots at every tick, the relation "same entity" from a tick to the next, and every field the key holds.</summary>
    internal static List<string> Entities(FireTome1Key key, FireTome1Run run, int from, int to, bool wall = false)
    {
        var wrong = new List<string>();
        var contact = new Dictionary<int, int>();
        var slide = new Dictionary<int, long>();
        for (var tick = from; tick <= to; tick++)
        {
            var expected = key.At(tick).Ents;
            var actual = run.Entities[tick];
            if (!expected.Keys.Order().SequenceEqual(actual.Keys.Order()))
            {
                wrong.Add($"t{tick}: slots {string.Join(",", actual.Keys.Order())} (key {string.Join(",", expected.Keys.Order())})");
                continue;
            }

            foreach (var (slot, k) in expected.OrderBy(e => e.Key))
            {
                var r = actual[slot];
                void Check(string field, long got, long? want)
                {
                    if (want.HasValue && got != want.Value)
                    {
                        wrong.Add($"t{tick} slot {slot} (type {k.Type}) {field}: {got} (key {want})");
                    }
                }

                // The relation "same entity": the key's identity word is kept from a tick to the next exactly when the DLL's object is.
                if (tick > from && key.At(tick - 1).Ents.TryGetValue(slot, out var before) && run.Entities[tick - 1].TryGetValue(slot, out var dllBefore) && before.Status != 4)
                {
                    var keySame = before.Id == k.Id;
                    var dllSame = ReferenceEquals(dllBefore.Entity, r.Entity);
                    if (keySame != dllSame)
                    {
                        wrong.Add($"t{tick} slot {slot}: same entity as the tick before: {dllSame} (key {keySame})");
                    }
                }

                var atWall = wall && k.Type == FireTome1Run.Ball && WallContact(key, tick, slot, contact, slide);
                Check("type", r.SpriteType, k.Type);
                Check("status", (int)r.Status, k.Status);
                if (atWall)
                {
                    // R-EL-5 (the family D-E20-80, D-H4C1-4): from the contact with the wall the DLL goes to the wall on the blocked axis (X, within 8 raw units) and keeps the force of the free axis, while the binary's search by halves scales both and freezes the ball.
                    // The free axis (Y) may differ by what the DLL slides: at most the sum of the key's own forces since the contact. The box origin follows the position (its offset from the position is compared).
                    if (Math.Abs((long)r.PosX - k.X) > WallBlockedAxisBound)
                    {
                        wrong.Add($"t{tick} slot {slot} (type {k.Type}) x: {r.PosX} (key {k.X}, bound {WallBlockedAxisBound})");
                    }

                    if (Math.Abs((long)r.PosY - k.Y) > WallBlockedAxisBound + slide[slot])
                    {
                        wrong.Add($"t{tick} slot {slot} (type {k.Type}) y: {r.PosY} (key {k.Y}, bound {WallBlockedAxisBound + slide[slot]})");
                    }
                }
                else
                {
                    Check("x", r.PosX, k.X);
                    Check("y", r.PosY, k.Y);
                }

                Check("z+1", r.PosZ + 1, k.Z);
                Check("counter 1CC", r.DamagedCounter, k.C1cc);
                Check("counter 1D0", r.CollisionCounter, k.C1d0);
                if (k.Touch != r.TouchedBy)
                {
                    wrong.Add($"t{tick} slot {slot} (type {k.Type}) touched by {r.TouchedBy?.ToString() ?? "none"} (key {k.Touch?.ToString() ?? "none"})");
                }

                if (!k.Full)
                {
                    continue;
                }

                Check("flags", r.Flags, k.Flags);
                Check("fx", r.ForceX, k.Fx);
                Check("fy", r.ForceY, k.Fy);
                Check("fz", r.ForceZ, k.Fz);
                Check("animation asked", r.TargetAnimation, k.AnimT);
                Check("direction asked", r.TargetDirection, k.DirT);
                Check("end of animation", r.AnimationEnd, k.AnimEnd);
                Check("wait +0x274", r.Wait, k.B274);
                Check("target slot +0x278", r.TargetSlot, k.B278Slot ?? 0);
                Check("blocker +0x20", r.Blocked ? 1 : 0, k.C20 is null ? (long?)null : k.C20 != 0 ? 1L : 0L);
                Check("slope", r.Slope, k.Slope);
                Check("ground", r.Ground, k.Ground);
                Check("forceAdjusted", r.Adjusted, k.Adj13c);
                Check("attack attribute", r.Attribute ?? -1, k.AtkAttr);
                Check("attack power", r.Power ?? -1, k.AtkPow);
                if (k.Hb is { } hb)
                {
                    var box = new[] { r.Box[0], r.Box[1], r.Box[2] + 1, r.Box[3], r.Box[4], r.Box[5] };
                    if (atWall)
                    {
                        // The origin of the box is the position plus the offsets of the box: the offsets are compared, the position was bounded above.
                        box[0] -= r.PosX;
                        box[1] -= r.PosY;
                        hb = new[] { hb[0] - k.X, hb[1] - k.Y, hb[2], hb[3], hb[4], hb[5] };
                    }

                    if (!box.SequenceEqual(hb))
                    {
                        wrong.Add($"t{tick} slot {slot} (type {k.Type}) box [{string.Join(",", box)}] (key [{string.Join(",", hb)}])");
                    }
                }

                if (!IsBirth(key, tick, slot) && r.Status != EntityStatus.FlagToDestroy)
                {
                    // From the tick after the birth (D-H4C1-3): the DLL validates the switch of animation at the end of the frame, the binary's UpdateAnimation runs in the tick of the birth.
                    Check("animation row", r.RowAtEnd, k.Row);
                    Check("current animation", r.AnimationAtEnd, k.AnimC);
                    Check("current direction", r.DirectionAtEnd, k.DirC);
                }
            }
        }

        return wrong;
    }

    /// <summary>R-EL-5: the distance in raw units the DLL's ball may stand from the wall on the blocked axis (the key's search by halves leaves it a few units short: 1 in the key of the column 6).</summary>
    internal const int WallBlockedAxisBound = 8;

    /// <summary>
    /// True from the tick the key's ball first meets the wall (its step is shorter than its force) on: <paramref name="slide"/> then holds, for the slot, the sum of the absolute values of the key's free-axis force (Y) since the contact, this tick included.
    /// </summary>
    private static bool WallContact(FireTome1Key key, int tick, int slot, Dictionary<int, int> contact, Dictionary<int, long> slide)
    {
        var k = key.At(tick).Ents[slot];
        var born = tick == 1 || !key.At(tick - 1).Ents.TryGetValue(slot, out var before) || before.Id != k.Id;
        if (born)
        {
            contact.Remove(slot);
            slide.Remove(slot);
        }
        else if (!contact.ContainsKey(slot))
        {
            var previous = key.At(tick - 1).Ents[slot];
            if (k.X - previous.X != previous.Fx || k.Y - previous.Y != previous.Fy)
            {
                contact[slot] = tick;
                slide[slot] = 0;
            }
        }

        if (!contact.ContainsKey(slot))
        {
            return false;
        }

        slide[slot] += Math.Abs((long)(k.Fy ?? 0));
        return true;
    }

    /// <summary>The hero, the item and the pointer kept, tick by tick. <paramref name="heroTo"/>: the hero (animation, place) is compared up to that tick only (an interruption leaves the hero to the injury, R-EL-6).</summary>
    internal static List<string> HeroAndItem(FireTome1Key key, FireTome1Run run, int from, int to, int? heroTo = null, bool heroAfterToo = false)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var k = key.At(tick);
            var h = run.Hero[tick];
            if ((h.ItemInUse, h.Phase, h.Counter, h.Mp) != ((uint)k.Item.InUse, k.Item.Phase, k.Item.Counter, k.Item.Mp))
            {
                wrong.Add($"t{tick}: item {h.ItemInUse:X} phase {h.Phase} counter {h.Counter} magic {h.Mp} (key {k.Item.InUse:X} phase {k.Item.Phase} counter {k.Item.Counter} magic {k.Item.Mp})");
            }

            if (k.HasPointer && h.PointerSlot != k.PtrSlot)
            {
                wrong.Add($"t{tick}: pointer slot {h.PointerSlot?.ToString() ?? "none"} (key {k.PtrSlot?.ToString() ?? "none"})");
            }

            if (heroTo is null || tick <= heroTo || heroAfterToo)
            {
                if ((h.TargetAnimation, h.AnimationAtEnd, h.PosX, h.PosY, h.PosZ + 1) != (k.Hero.AnimT, k.Hero.AnimC, k.Hero.X, k.Hero.Y, k.Hero.Z))
                {
                    wrong.Add($"t{tick}: hero animation {h.TargetAnimation}/{h.AnimationAtEnd} at ({h.PosX}, {h.PosY}, {h.PosZ + 1}) (key {k.Hero.AnimT}/{k.Hero.AnimC} at ({k.Hero.X}, {k.Hero.Y}, {k.Hero.Z}))");
                }
            }
        }

        return wrong;
    }

    /// <summary>The generator at the head of each tick and its steps in the frame (the key's draws plus the step of the end of the frame): one tick a frame.</summary>
    internal static List<string> Generator(FireTome1Key key, FireTome1Run run, int from, int to)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var k = key.At(tick);
            if (run.StateAtHead[tick] != k.Rng0)
            {
                wrong.Add($"t{tick}: generator {run.StateAtHead[tick]} at the head (key {k.Rng0})");
            }

            if (run.FrameSteps[tick] != FireTome1Run.Steps(k.Rng0, k.Rng1) + 1)
            {
                wrong.Add($"t{tick}: {run.FrameSteps[tick]} steps of the generator (key {FireTome1Run.Steps(k.Rng0, k.Rng1)} draws + 1)");
            }

            if (FireTome1Run.Steps(k.Rng0, k.Rng1) != k.Draws)
            {
                wrong.Add($"t{tick}: the key's own draws {k.Draws} are not the steps from rng0 to rng1");
            }
        }

        return wrong;
    }

    /// <summary>The events of each tick (effects created, effects attached, sounds) and the blocks hit.</summary>
    internal static List<string> Events(FireTome1Key key, FireTome1Run run, int from, int to)
    {
        var wrong = new List<string>();
        for (var tick = from; tick <= to; tick++)
        {
            var expected = FireTome1Key.NormalEvents(key.At(tick));
            if (!expected.SequenceEqual(run.Events[tick]))
            {
                wrong.Add($"t{tick}: events [{string.Join("; ", run.Events[tick])}] (key [{string.Join("; ", expected)}])");
            }

            var hits = key.At(tick).Hits.Order().ToList();
            if (!hits.SequenceEqual(run.Hits[tick].Order()))
            {
                wrong.Add($"t{tick}: hits [{string.Join(",", run.Hits[tick])}] (key [{string.Join(",", hits)}])");
            }
        }

        return wrong;
    }

    /// <summary>Everything, tick <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal static List<string> Everything(FireTome1Key key, FireTome1Run run, int from, int to, int? heroTo = null, bool heroAfterToo = false, bool wall = false)
    {
        var all = new List<string>();
        all.AddRange(HeroAndItem(key, run, from, to, heroTo, heroAfterToo));
        all.AddRange(Generator(key, run, from, to));
        all.AddRange(Entities(key, run, from, to, wall));
        all.AddRange(Events(key, run, from, to));
        return all;
    }

    internal static string Show(IReadOnlyList<string> wrong) => wrong.Count == 0 ? string.Empty : wrong.Count + " differences: " + Environment.NewLine + string.Join(Environment.NewLine, wrong.Take(14));
}
