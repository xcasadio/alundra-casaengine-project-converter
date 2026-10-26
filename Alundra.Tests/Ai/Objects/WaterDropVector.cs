#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T1, phase A: one vector of C71 that the oracle made from the real code of the binary (<c>docs/plan-e20-annexe/e20-g2c3-disc/g2c3/replay/c71_goldens.json</c>, 22 vectors over 11 classes, and
/// <c>docs/plan-e20-annexe/e20-g2c3/vectors/a1_state2_flags.json</c>, the four vectors of the state 2). The vector is laid out on entities made by hand, in the machine's own slots (<see cref="Slots"/>: 0 the hero, 3 the drop, 5 to 7 the candidates of the support),
/// played through <see cref="WaterDropAi.Tick"/>, and its expected words are read back (<see cref="Expected"/>). A word the real code did not read is posed with a sentinel, so that a stray write shows.
/// </summary>
internal sealed class WaterDropVector
{
    /// <summary>The address of the hero in the machine's run (slot 0): the value of <c>drop.+0x12C</c> that means "the support is the hero".</summary>
    internal const uint HeroAddress = 2148695344;

    /// <summary>The stride of an entity in the machine's run (<c>0x294</c>).</summary>
    internal const uint Stride = 0x294;

    internal const byte FreeByte = 0xA3;

    internal const short FreeCounterLow = 0x5A5A;

    internal const uint FreeFlags = 0x00AB5A31;

    private readonly JsonElement _vector;

    internal WaterDropVector(int index, JsonElement vector)
    {
        Index = index;
        _vector = vector;
        CheckSlot(vector.GetProperty("hero_carried").GetUInt32());
        CheckSlot(DropIn("0x12C"));
    }

    private static void CheckSlot(uint address)
    {
        if (address != 0 && ((address - HeroAddress) % Stride != 0 || (address - HeroAddress) / Stride > 7))
        {
            throw new InvalidOperationException($"the address is not a slot of the run: {address}");
        }
    }

    internal int Index { get; }

    internal string Class => _vector.GetProperty("cls").GetString()!;

    internal uint DropIn(string word) => _vector.GetProperty("drop_in").GetProperty(word).GetUInt32();

    internal bool DropOutHas(string word) => _vector.GetProperty("drop_out_changed").TryGetProperty(word, out _);

    internal uint DropOut(string word) => _vector.GetProperty("drop_out_changed").GetProperty(word).GetUInt32();

    /// <summary>The slot (0 = the hero, 3 = the drop, 5 to 7 the candidates) that an address of the run designates, or -1 for the null address.</summary>
    internal static int SlotOf(uint address) => address == 0 ? -1 : checked((int)((address - HeroAddress) / Stride));

    /// <summary>The slot of <c>drop.+0x12C</c> (the support); -1 when null.</summary>
    internal int SupportSlot => SlotOf(DropIn("0x12C"));

    /// <summary>The slot of <c>hero.+0x2C</c> (the object the hero carries); -1 when null.</summary>
    internal int CarriedSlot => SlotOf(_vector.GetProperty("hero_carried").GetUInt32());

    internal uint CandidateType(int slot) => _vector.GetProperty("cand_types").GetProperty(slot.ToString()).GetUInt32();

    internal bool CandidateChanged(int slot, string word) => _vector.GetProperty("others_changed").TryGetProperty(slot.ToString(), out var changed) && changed.TryGetProperty(word, out _);

    internal uint CandidateOut(int slot, string word) => _vector.GetProperty("others_changed").GetProperty(slot.ToString()).GetProperty(word).GetUInt32();

    /// <summary>The slot of the entity the drop puts out, or -1 (the hero's carried object when the support is the hero, otherwise the support), whatever its type.</summary>
    internal int TargetSlot => SupportSlot == 0 ? CarriedSlot : SupportSlot;

    /// <summary>True when the real code puts the target out: the drop is in the state 3 and the target is a torch (type 393) among the candidates.</summary>
    internal bool IsTorchHit => DropIn("0x274") == 3 && TargetSlot is 5 or 6 or 7 && CandidateType(TargetSlot) == TorchAi.SpriteType;

    internal static int I(uint word) => unchecked((int)word);

    /// <summary>The entities laid out for this vector: the drop, the hero, the candidates by slot, and the world context (the hero).</summary>
    internal sealed record Layout(EntityScriptProxy Drop, EntityScriptProxy Hero, Dictionary<int, EntityScriptProxy> Slots, PickupHost Host);

    internal Layout Build()
    {
        var hero = CarryBench.Make("hero");
        hero.SpriteType = 0;
        var drop = CarryBench.Make("drop");
        var slots = new Dictionary<int, EntityScriptProxy> { [0] = hero, [3] = drop };

        drop.SpriteType = I(DropIn("0x68"));
        drop.Status = (EntityStatus)(int)DropIn("0x10");
        drop.Flags = DropIn("0x6C");
        drop.TargetAnimationId = DropIn("0x88");
        drop.ForceResetAnimationFlag = I(DropIn("0xAC"));
        drop.ProgramIndexes[2] = I(DropIn("0x54"));
        drop.ForceZ = I(DropIn("0xB8"));
        drop.PosZ = I(DropIn("0x11C"));
        drop.CollidedWithEntityZ = I(DropIn("0x140"));
        drop.IsOnGround = I(DropIn("0x144"));
        drop.Bytes[0] = (byte)DropIn("0x274");
        drop.Bytes[1] = (byte)DropIn("0x275");
        drop.Bytes[2] = FreeByte;
        drop.Bytes[3] = FreeByte;
        drop.AIValues[0] = FreeCounterLow;
        drop.AIValues[1] = unchecked((short)DropIn("0x282"));

        foreach (var slot in new[] { 5, 6, 7 })
        {
            var candidate = CarryBench.Make($"candidate{slot}");
            candidate.SpriteType = I(CandidateType(slot));
            candidate.Bytes[0] = FreeByte;
            candidate.Bytes[1] = FreeByte;
            slots[slot] = candidate;
        }

        // the target the real code writes to: its status and its mark are posed so that the write shows, unless the machine's own vector did not change them (they already held the value).
        if (IsTorchHit)
        {
            var torch = slots[TargetSlot];
            torch.Status = CandidateChanged(TargetSlot, "0x10") ? EntityStatus.Normal : EntityStatus.Deactivated;
            torch.Bytes[0] = CandidateChanged(TargetSlot, "0x274") ? FreeByte : (byte)1;
        }

        if (SupportSlot >= 0)
        {
            drop.RidingEntity = slots[SupportSlot].LogicContextEntity;
        }

        if (CarriedSlot >= 0)
        {
            hero.CarriedEntity = slots[CarriedSlot].LogicContextEntity;
        }

        return new Layout(drop, hero, slots, new PickupHost { Hero = hero });
    }

    /// <summary>The word after the tick: the changed word when the real code changed it, the word that was read otherwise.</summary>
    internal uint Expected(string word) => DropOutHas(word) ? DropOut(word) : DropIn(word);

    /// <summary>The expected value of the counter <c>+0x282</c> (the high half of the word <c>+0x280</c> when the real code wrote it).</summary>
    internal short ExpectedCounter => DropOutHas("0x280") ? unchecked((short)(DropOut("0x280") >> 16)) : unchecked((short)DropIn("0x282"));

    internal static IReadOnlyList<WaterDropVector> Load(string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.WaterDropAnnex(relative)));
        return document.RootElement.EnumerateArray().Select((e, i) => new WaterDropVector(i, e.Clone())).ToArray();
    }

    internal static IReadOnlyList<WaterDropVector> Goldens() => Load("e20-g2c3-disc/g2c3/replay/c71_goldens.json");

    internal static IReadOnlyList<WaterDropVector> State2() => Load("e20-g2c3/vectors/a1_state2_flags.json");
}
