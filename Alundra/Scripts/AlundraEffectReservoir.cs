#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>One slot of the effect reservoir (the binary's 0x80-byte slot).</summary>
public sealed class AlundraEffectSlot
{
    public int Index { get; }
    public bool Active { get; internal set; }
    public int RecordIndex { get; internal set; } = -1;
    public int Mode { get; internal set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int ForceX { get; set; }
    public int ForceY { get; set; }
    public int ForceZ { get; set; }
    public bool TargetMapTable { get; internal set; }
    public int TargetEffect { get; internal set; }
    public int TargetAnimation { get; set; }
    public int CurrentAnimation { get; internal set; }
    public int CurrentImageSetIndex { get; internal set; } = -1;
    public AlundraEffectImageSet? CurrentImageSet { get; internal set; }
    public int Idsv { get; internal set; }
    public uint DepthKey { get; internal set; }
    public bool Destroying { get; internal set; }

    internal AlundraEffectSlot(int index)
    {
        Index = index;
    }
}

/// <summary>The effect reservoir of the binary (128 slots), pure logic.</summary>
public sealed class AlundraEffectReservoir
{
    public const int SlotCount = 128;

    public AlundraEffectReservoir(AlundraEffectBank bank, Action<string>? warn = null)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyList<AlundraEffectSlot> Slots => throw new NotImplementedException();
    public IEnumerable<AlundraEffectSlot> ActiveSlots => throw new NotImplementedException();
    public int ActiveCount => throw new NotImplementedException();

    public AlundraEffectSlot? Spawn(int recordIndex) => throw new NotImplementedException();
    public void SpawnAtLoad(int tileX, int tileY) => throw new NotImplementedException();
    public AlundraEffectSlot? Create(bool mapTable, int effect, int animation, int x, int y, int z) => throw new NotImplementedException();
    public void Free(AlundraEffectSlot slot) => throw new NotImplementedException();
    public void Tick(bool frozen) => throw new NotImplementedException();
}
