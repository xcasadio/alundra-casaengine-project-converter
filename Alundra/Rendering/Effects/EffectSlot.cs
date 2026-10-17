#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Rendering.Effects;

/// <summary>One slot of the effect reservoir (the binary's 0x80-byte slot, <c>0x80138608 + 0x80 * i</c>); see <see cref="AlundraEffectReservoir"/>.</summary>
public sealed class AlundraEffectSlot
{
    /// <summary>The place of the slot in the reservoir (0 to 127).</summary>
    public int Index { get; }

    /// <summary>The binary's status 2 (active); 0 is free.</summary>
    public bool Active { get; internal set; }

    /// <summary>The record that created it (the index the script opcodes name), -1 for a creation that does not come from a record.</summary>
    public int RecordIndex { get; internal set; } = -1;

    /// <summary>0 free with forces, 1 attached to an anchor (<see cref="AlundraEffectReservoir.CreateAttached"/>), 2 frozen, 3 detached from its anchor but following its depth key
    /// (<see cref="AlundraEffectReservoir.CreateDetached"/>); 1 and 3 are created only by native code (E19.g G3d, G3D-R4).</summary>
    public int Mode { get; internal set; }

    /// <summary>The entity a slot of mode 1 or 3 follows; null in the other modes.</summary>
    public IAlundraEffectAnchor? Anchor { get; internal set; }

    /// <summary>Added to the anchor's depth key (the binary's <c>+0x60</c>).</summary>
    public int DepthOffset { get; internal set; }

    /// <summary>Added to the anchor's position in mode 1 (the binary's <c>+0x48/4C/50</c>).</summary>
    public int OffsetX { get; internal set; }

    public int OffsetY { get; internal set; }

    public int OffsetZ { get; internal set; }

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

    /// <summary>
    /// E20.f1b (D-E20-30): the number the reservoir gave this slot's current occupant when it was created (never 0 once created, unique in the reservoir). A holder of a slot (the hero's aura) keeps it with
    /// the slot and acts on the slot only while it still carries it: a slot freed and taken by another effect has another number.
    /// </summary>
    public int Generation { get; internal set; }

    /// <summary>
    /// E20.f1b (D-E20-28): the logic tick of the frame at which an effect created at once by the hero (<see cref="AlundraEffectStage.CreateNow"/>) first exists; <see cref="AlundraEffectReservoir.Tick"/> does not
    /// update the slot before it. 0 for every other slot. Local to a frame: <see cref="AlundraEffectStage.RunFrame"/> clears it.
    /// </summary>
    internal int FirstUpdateTick;

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
