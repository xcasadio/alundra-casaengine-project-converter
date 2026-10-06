#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>One slot of the effect reservoir (the binary's 0x80-byte slot, <c>0x80138608 + 0x80 * i</c>); see <see cref="AlundraEffectReservoir"/>.</summary>
public sealed class AlundraEffectSlot
{
    /// <summary>The place of the slot in the reservoir (0 to 127).</summary>
    public int Index { get; }

    /// <summary>The binary's status 2 (active); 0 is free.</summary>
    public bool Active { get; internal set; }

    /// <summary>The record that created it (the index the script opcodes name), -1 for a creation that does not come from a record.</summary>
    public int RecordIndex { get; internal set; } = -1;

    /// <summary>0 free with forces, 2 frozen; 1 and 3 (attached to an entity) are created only by native code (E19.g G3d).</summary>
    public int Mode { get; internal set; }

    /// <summary>Position, 16.16 (the binary's <c>+0x3C/40/44</c>).</summary>
    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    /// <summary>Per-tick forces added to the position in mode 0 (<c>+0x54/58/5C</c>).</summary>
    public int ForceX { get; set; }

    public int ForceY { get; set; }

    public int ForceZ { get; set; }

    /// <summary>True when the effect is in the map table (the record's <c>0x80</c>), false for the global one.</summary>
    public bool TargetMapTable { get; internal set; }

    public int TargetEffect { get; internal set; }

    /// <summary>The animation the slot is asked to play; the next update switches to it (the binary's <c>+0x70</c>, written by the record and by <c>0x92</c>).</summary>
    public int TargetAnimation { get; set; }

    /// <summary>The animation being played (<c>+0x71</c>; the complement of the target until the first update).</summary>
    public int CurrentAnimation { get; internal set; }

    /// <summary>The image set shown, and its index in the table; -1 and null before the first update.</summary>
    public int CurrentImageSetIndex { get; internal set; } = -1;

    public AlundraEffectImageSet? CurrentImageSet { get; internal set; }

    /// <summary>The depth bias of the image set shown (<c>IDSV</c>, shifted left by 16 in the key).</summary>
    public int Idsv { get; internal set; }

    /// <summary>The sort key <c>(y &amp; 0xFFFF0000) + (z &gt;&gt; 16) + (IDSV &lt;&lt; 16)</c> of the last update in mode 0.</summary>
    public uint DepthKey { get; internal set; }

    /// <summary>The destruction flag: set on the tick the animation ends with <c>Destroy</c> (the slot is still drawn that tick), the slot is freed at the next one.</summary>
    public bool Destroying { get; internal set; }

    internal bool CurrentMapTable;
    internal int CurrentEffect;
    internal AlundraEffectTable? Table;
    internal AlundraEffectAnimation? Animation;
    internal int FrameIndex;
    internal int Counter;

    internal AlundraEffectSlot(int index)
    {
        Index = index;
    }
}

/// <summary>
/// The effect reservoir of ALUN_CD.EXE (France), E19.g G3a (docs/plan-e19-opcodes.md section 1.2o.6, ADR-0045): 128 slots, the smallest free one wins, a full reservoir
/// creates nothing (no log). Pure logic, no engine type, headless-testable. Ported from <c>GetFreeEffect 0x8003B9C4</c>, <c>InitEffect 0x8003BB14</c>, <c>SpawnMapEffect
/// 0x8003C094</c>, the map-entry spawn <c>0x8003C1A4</c>, <c>UpdateEffects 0x8003C410</c>, <c>Animate 0x8003BBDC</c> and <c>Move 0x8003C284</c>; the model checked against the real
/// code is <c>docs/plan-e19-g1g3-annexe/fxpool.py</c>.
/// <para>
/// A slot's first update loads its table entry and image 0 (the target effect and animation start as the complement of the current ones). A change of the target animation shows
/// image 0 of the new animation the same tick and clears the destruction flag; otherwise the counter decreases and the next image comes when it reaches 0 (an image lasts its
/// <c>ticks</c>; a counter of 0 lasts 256). A <c>Destroy</c> end sets the flag (the slot is still drawn that tick), the next update frees the slot; a <c>Loop</c> end goes back to
/// image 0. The tick 0 of a slot is its first update; a <c>Destroy</c> animation of duration S (the sum of its delays) is drawn on the ticks 0 to S and freed on the tick S + 1.
/// </para>
/// <para>
/// Rules the binary does not have (it prints and halts, never reached on real data): an effect, an animation, an image set or an end the data does not hold is one warning
/// (once per reservoir) and a destruction.
/// </para>
/// </summary>
public sealed class AlundraEffectReservoir
{
    public const int SlotCount = 128;

    private readonly AlundraEffectBank _bank;
    private readonly Action<string>? _warn;
    private readonly AlundraEffectSlot[] _slots = new AlundraEffectSlot[SlotCount];
    private bool _warned;

    public AlundraEffectReservoir(AlundraEffectBank bank, Action<string>? warn = null)
    {
        _bank = bank;
        _warn = warn;
        for (var i = 0; i < SlotCount; i++)
        {
            _slots[i] = new AlundraEffectSlot(i);
        }
    }

    public IReadOnlyList<AlundraEffectSlot> Slots => _slots;

    /// <summary>The active slots in the order of the slots (the order of the update, of the push and of the draw).</summary>
    public IEnumerable<AlundraEffectSlot> ActiveSlots
    {
        get
        {
            foreach (var slot in _slots)
            {
                if (slot.Active)
                {
                    yield return slot;
                }
            }
        }
    }

    public int ActiveCount
    {
        get
        {
            var count = 0;
            foreach (var slot in _slots)
            {
                if (slot.Active)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary><c>SpawnMapEffect(record, force)</c> with <c>force</c> set (the opcodes <c>0x90</c> and <c>0xA2</c>, no zone test, no <c>0x40</c> test): the effect of the record at its
    /// position and animation. A record outside the table: nothing (as the binary); a full reservoir: nothing.</summary>
    public AlundraEffectSlot? Spawn(int recordIndex)
    {
        var records = _bank.Records;
        return recordIndex >= 0 && recordIndex < records.Count ? SpawnCore(recordIndex, records[recordIndex]) : null;
    }

    /// <summary>The map-entry spawn (<c>0x8003C1A4</c>): every record with <c>Flags &amp; 0x40</c> whose inclusive zone holds the tile of the hero appears, in the order of the records.</summary>
    public void SpawnAtLoad(int tileX, int tileY)
    {
        var records = _bank.Records;
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if ((record.Flags & 0x40) != 0 && tileX >= record.X1 && tileX <= record.X2 && tileY >= record.Y1 && tileY <= record.Y2)
            {
                SpawnCore(i, record);
            }
        }
    }

    /// <summary>The generic creator of mode 0 (<c>CreateEffectEntity 0x8003BDD8</c>, E19.g G3d): not from a record, at the given 16.16 position.</summary>
    public AlundraEffectSlot? Create(bool mapTable, int effect, int animation, int x, int y, int z) => CreateCore(-1, mapTable, effect, animation, x, y, z);

    /// <summary>Frees a slot at once (the binary's status 0).</summary>
    public void Free(AlundraEffectSlot slot)
    {
        slot.Active = false;
    }

    /// <summary>
    /// One logic tick (<c>UpdateEffects 0x8003C410</c>). With <paramref name="frozen"/> (<c>PlayerControlFlags &amp; 0x48</c>) nothing is freed, animated or moved - the slots stay
    /// and are still drawn; otherwise, in the order of the slots, a slot whose destruction flag is set is freed, any other is animated then moved.
    /// </summary>
    public void Tick(bool frozen)
    {
        if (frozen)
        {
            return;
        }

        foreach (var slot in _slots)
        {
            if (!slot.Active)
            {
                continue;
            }

            if (slot.Destroying)
            {
                slot.Active = false;
                continue;
            }

            Animate(slot);
            Move(slot);
        }
    }

    private AlundraEffectSlot? SpawnCore(int recordIndex, AlundraEffectRecord record) =>
        CreateCore(recordIndex, (record.Flags & 0x80) != 0, record.Effect, record.Anim,
            unchecked((record.X * 12 + 12) << 16), unchecked((record.Y * 8 + 8) << 16), unchecked(record.Z << 19));

    private AlundraEffectSlot? CreateCore(int recordIndex, bool mapTable, int effect, int animation, int x, int y, int z)
    {
        AlundraEffectSlot? slot = null;
        foreach (var candidate in _slots)
        {
            if (!candidate.Active)
            {
                slot = candidate;
                break;
            }
        }

        if (slot == null)
        {
            return null;
        }

        // InitEffect: the template (zero) over the slot, then the record, mode, status, target effect and animation; the current ones are the complements, so that the first update
        // always loads the table entry and image 0.
        slot.Active = true;
        slot.RecordIndex = recordIndex;
        slot.Mode = 0;
        slot.X = x;
        slot.Y = y;
        slot.Z = z;
        slot.ForceX = 0;
        slot.ForceY = 0;
        slot.ForceZ = 0;
        slot.TargetMapTable = mapTable;
        slot.TargetEffect = effect & 0xFF;
        slot.TargetAnimation = animation & 0xFF;
        slot.CurrentMapTable = !mapTable;
        slot.CurrentEffect = ~slot.TargetEffect & 0xFF;
        slot.CurrentAnimation = ~slot.TargetAnimation & 0xFF;
        slot.CurrentImageSetIndex = -1;
        slot.CurrentImageSet = null;
        slot.Idsv = 0;
        slot.DepthKey = 0;
        slot.Destroying = false;
        slot.Table = null;
        slot.Animation = null;
        slot.FrameIndex = 0;
        slot.Counter = 0;
        return slot;
    }

    private void Animate(AlundraEffectSlot slot)
    {
        if (slot.TargetEffect != slot.CurrentEffect || slot.TargetMapTable != slot.CurrentMapTable)
        {
            if (!_bank.TryGetTable(slot.TargetMapTable, slot.TargetEffect, out var table))
            {
                Destroy(slot, $"effect {slot.TargetEffect} of the {(slot.TargetMapTable ? "map" : "global")} table not found");
                return;
            }

            slot.Table = table;
            slot.CurrentMapTable = slot.TargetMapTable;
            slot.CurrentEffect = slot.TargetEffect;
            slot.CurrentAnimation = ~slot.TargetAnimation & 0xFF;
        }

        if (slot.TargetAnimation != slot.CurrentAnimation)
        {
            if (slot.TargetAnimation >= slot.Table!.Animations.Count)
            {
                Destroy(slot, $"animation {slot.TargetAnimation} of effect {slot.TargetEffect} not found");
                return;
            }

            slot.Animation = slot.Table.Animations[slot.TargetAnimation];
            slot.CurrentAnimation = slot.TargetAnimation;
            slot.FrameIndex = 0;
            slot.Counter = 0;
            slot.Destroying = false;
        }
        else
        {
            slot.Counter = (slot.Counter - 1) & 0xFF;
            if (slot.Counter != 0)
            {
                return;
            }
        }

        var animation = slot.Animation!;
        var wraps = 0;
        while (true)
        {
            if (slot.FrameIndex < animation.Frames.Length)
            {
                var frame = animation.Frames[slot.FrameIndex++];
                var setIndex = frame.Length > 1 ? frame[1] : -1;
                if (setIndex < 0 || setIndex >= slot.Table!.ImageSets.Count)
                {
                    Destroy(slot, $"image set {setIndex} of effect {slot.TargetEffect} not found");
                    return;
                }

                slot.Counter = frame[0] & 0xFF;
                slot.CurrentImageSetIndex = setIndex;
                slot.CurrentImageSet = slot.Table.ImageSets[setIndex];
                slot.Idsv = slot.CurrentImageSet.Idsv & 0xFFFF;
                return;
            }

            if (animation.End == "Destroy")
            {
                slot.Counter = 0xFF;
                slot.Destroying = true;
                return;
            }

            if (animation.End != "Loop" || ++wraps > 1)
            {
                Destroy(slot, animation.End == "Loop"
                    ? $"animation {slot.TargetAnimation} of effect {slot.TargetEffect} loops without a frame"
                    : $"animation {slot.TargetAnimation} of effect {slot.TargetEffect} ends with '{animation.End}'");
                return;
            }

            slot.FrameIndex = 0;
        }
    }

    private static void Move(AlundraEffectSlot slot)
    {
        // Mode 0: the forces, then the key. Mode 2 stays put; modes 1 and 3 are created only by native code (G3d).
        if (slot.Mode != 0)
        {
            return;
        }

        unchecked
        {
            slot.X += slot.ForceX;
            slot.Z += slot.ForceZ;
            slot.Y += slot.ForceY;
            slot.DepthKey = (uint)((slot.Y & (int)0xFFFF0000) + (slot.Z >> 16) + (slot.Idsv << 16));
        }
    }

    /// <summary>The defensive rule: one warning per reservoir, no image, the destruction flag (the slot is freed at the next update).</summary>
    private void Destroy(AlundraEffectSlot slot, string reason)
    {
        if (!_warned)
        {
            _warned = true;
            _warn?.Invoke($"AlundraEffectReservoir: {reason}; the effect is destroyed (the binary would halt here).");
        }

        slot.CurrentImageSetIndex = -1;
        slot.CurrentImageSet = null;
        slot.Idsv = 0;
        slot.Destroying = true;
    }
}
