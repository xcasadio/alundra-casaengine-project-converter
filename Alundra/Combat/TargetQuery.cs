#nullable enable
using System.Collections.Generic;
using Alundra.Entities;

namespace Alundra.Combat;

/// <summary>
/// E20.h4b1 (docs/plan-e20-heros.md section 1.31.2, D-H4B1-1 to D-H4B1-3): the target query of the spells (<c>0x8003AF70</c>) and its sort (<c>0x8003AE04</c>) as a pure function. It lists the entities a spell can aim at, nearest
/// first. Inert in production: nothing calls it yet (the spells that aim, vent 1, C63, C65 and C75, will). <see cref="EntitySearchService"/> is the query of the script opcodes, another algorithm, and does not change.
/// </summary>
public static class TargetQuery
{
    /// <summary>
    /// Production value of Q-H4B-2 (the rule of the author on the defects of the original): the highest occupied slot (the count) is scanned too. The binary scans the slots 1 to count - 1 and misses it.
    /// </summary>
    public const bool InclusiveBound = true;

    /// <summary>
    /// Production value of Q-H4B-3 (the same rule): the sort runs from two results. The binary sorts only from three, so a pair stays in the order of the slots.
    /// </summary>
    public const int SortThreshold = 2;

    /// <summary>
    /// The candidates for a spell of attack type <paramref name="type"/> cast by <paramref name="caller"/>, in the order of the original: the entities of slots 1 to <paramref name="count"/> - 1 (to <paramref name="count"/> when
    /// <paramref name="inclusiveBound"/>), the caller excluded, those whose status is 2 or 3, which are blocked by no entity, whose flags hold a bit of <paramref name="mask"/>, with neither the hurt counter nor the frame-collision
    /// counter, without the bit <c>0x40</c> of the hit-pass animation byte, with a balance record whose byte <c>2 + type</c> is not a <c>0x80</c> response. The slot 0 (the hero) is never scanned. Sorted by the squared distance
    /// to the caller (Hoare, unstable, as the binary) when there are at least <paramref name="sortThreshold"/> results.
    /// </summary>
    /// <param name="entities">The born entities, a compact list in the order of the slots (a free cell is absent); each entity is judged by its <see cref="EntityScriptProxy.Slot"/>, never by its index in the list.</param>
    /// <param name="count">The highest occupied slot (<c>[0x80134360]</c>), given by the caller.</param>
    public static IReadOnlyList<EntityScriptProxy> Find(
        IReadOnlyList<EntityScriptProxy> entities, int count, EntityScriptProxy caller, uint mask, int type, bool inclusiveBound = InclusiveBound, int sortThreshold = SortThreshold)
    {
        var last = inclusiveBound ? count : count - 1;
        var found = new List<EntityScriptProxy>();
        var keys = new List<uint>();
        foreach (var entity in entities)
        {
            if (entity.Slot < 1 || entity.Slot > last || ReferenceEquals(entity, caller))
            {
                continue;
            }

            if (!entity.Status.IsActive()
                || entity.BlockedByEntity != null
                || (entity.Flags & mask) == 0
                || entity.FrameCollisionTickCounter != 0
                || entity.DamagedTickCounter != 0
                || (entity.HitAnimByte & 0x40) != 0)
            {
                continue;
            }

            var sheet = entity.BalanceRecord;
            if (sheet == null || (sheet.ByteAt(2 + type) & 0xC0) == 0x80)
            {
                continue;
            }

            found.Add(entity);
            keys.Add(Key(entity, caller));
        }

        if (found.Count >= sortThreshold && found.Count > 0)
        {
            var results = found.ToArray();
            var distances = keys.ToArray();
            Sort(results, distances, 0, results.Length - 1);
            return results;
        }

        return found;
    }

    /// <summary>
    /// E20.h4d2 (D-H4D2-5): the count the original stores in <c>[0x80134360]</c> and hands to the query: the highest slot of the born entities whose status is neither <see cref="EntityStatus.Destroyed"/> nor <see cref="EntityStatus.FlagToDestroy"/> (the status 0 and 4 are free or to be freed), -1 when there is none.
    /// Measured equal to the stored word on the 494 calls of the keys (probe <c>probe_count.py</c>). An entity taken off the table (a negative <see cref="EntityScriptProxy.Slot"/>) has no slot.
    /// </summary>
    /// <param name="entities">The born entities, a compact list; each is judged by its <see cref="EntityScriptProxy.Slot"/>.</param>
    public static int StoredCount(IReadOnlyList<EntityScriptProxy> entities)
    {
        var count = -1;
        foreach (var entity in entities)
        {
            if (entity.Slot > count && entity.Status != EntityStatus.Destroyed && entity.Status != EntityStatus.FlagToDestroy)
            {
                count = entity.Slot;
            }
        }

        return count;
    }

    /// <summary>The squared distance on the two axes modulo 2^32, each difference taken on 32 bits then shifted arithmetically by 16 (the positions are 16.16).</summary>
    private static uint Key(EntityScriptProxy entity, EntityScriptProxy caller)
    {
        unchecked
        {
            var dx = (entity.PosX - caller.PosX) >> 16;
            var dy = (entity.PosY - caller.PosY) >> 16;
            return (uint)(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// <c>0x8003AE04</c>: Hoare with the pivot fixed at the entry, <c>(K[lo] + K[hi]) mod 2^32 &gt;&gt; 1</c>, unsigned scans, a swap only when <c>i &lt; j</c>, then the recursion on <c>(lo, i - 1)</c> and <c>(j + 1, hi)</c>.
    /// Ported as is, the order of the equal keys included.
    /// </summary>
    private static void Sort(EntityScriptProxy[] results, uint[] keys, int lo, int hi)
    {
        var i = lo;
        var j = hi;
        var pivot = unchecked(keys[lo] + keys[hi]) >> 1;
        while (true)
        {
            while (keys[i] < pivot)
            {
                i++;
            }

            while (pivot < keys[j])
            {
                j--;
            }

            if (i < j)
            {
                (keys[i], keys[j]) = (keys[j], keys[i]);
                (results[i], results[j]) = (results[j], results[i]);
                i++;
                j--;
                continue;
            }

            break;
        }

        if (lo < i - 1)
        {
            Sort(results, keys, lo, i - 1);
        }

        if (j + 1 < hi)
        {
            Sort(results, keys, j + 1, hi);
        }
    }
}
