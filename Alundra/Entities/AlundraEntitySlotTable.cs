#nullable enable
using System;
using System.Diagnostics;
using System.IO;

namespace Alundra.Scripts;

/// <summary>
/// E20.e0a2 (docs/plan-e20-heros.md section 1.16.2, ADR-0064): the table of the entity slots of one world, as the 64 entries of the binary's flat entity array (<c>0x80127FC4</c>, stride <c>0x294</c>). Slot 0 is the hero
/// and is held from the construction (even in a world without a hero: the search of <c>FindFreeEntitySlot</c> <c>0x80039AD0</c> starts at 1); the other entities take the LOWEST free slot of 1 to 63, and a full table
/// refuses (the binary prints <c>0x800239B0</c> and returns 0). A slot is held until it is released, which the world does when it recycles a destroyed entity (<c>0x80038634</c>): an entity in the state 4 still holds its slot.
/// Pure state, no engine: the world owns one per instance, so a new map starts empty.
/// </summary>
internal sealed class AlundraEntitySlotTable
{
    /// <summary>The number of slots of the binary's table (the hero's slot 0 plus 63 others).</summary>
    internal const int SlotCount = 64;

    private ulong _held = 1UL; // slot 0: the hero, held from the start.

    /// <summary>The number of held slots, the hero's included.</summary>
    internal int HeldCount => System.Numerics.BitOperations.PopCount(_held);

    /// <summary>True when <paramref name="slot"/> (0 to 63) is held.</summary>
    internal bool IsHeld(int slot) => slot >= 0 && slot < SlotCount && (_held & (1UL << slot)) != 0;

    /// <summary>Takes the lowest free slot of 1 to 63 and returns it, or -1 when the table is full (the binary's refusal).</summary>
    internal int Allocate()
    {
        for (var slot = 1; slot < SlotCount; slot++)
        {
            var bit = 1UL << slot;
            if ((_held & bit) == 0)
            {
                _held |= bit;
                return slot;
            }
        }

        return -1;
    }

    /// <summary>Holds slot 0 for the hero (already held from the construction; kept explicit for the adoption).</summary>
    internal void HoldHeroSlot() => _held |= 1UL;

    /// <summary>Frees <paramref name="slot"/>. -1 (an entity without a slot) and the hero's slot 0 are no-ops, as is an out-of-range value.</summary>
    internal void Release(int slot)
    {
        if (slot <= 0 || slot >= SlotCount)
        {
            return;
        }

        _held &= ~(1UL << slot);
    }
}

/// <summary>
/// E20.e0a2 phase A (docs/plan-e20-heros.md section 1.16.2, T0): the shadow channel that predicts what the sorted insertion and the cap of phase B would do, without doing it. Active only when the environment variable
/// <see cref="EnvVar"/> names a file; absent, nothing is written and nothing is allocated. One line per event, <c>&lt;event&gt; test=&lt;Type.Method&gt; world=&lt;name&gt; slot=&lt;s&gt; maxLive=&lt;m&gt;</c>, the test being the
/// outermost frame of the stack in the <c>Alundra.Tests</c> namespace (the suites run without parallelism, and an apparition is always made in the call stack of its test).
/// </summary>
internal static class AlundraSlotShadow
{
    internal const string EnvVar = "ALUNDRA_SLOT_SHADOW_LOG";

    private static readonly string? Path = Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } p ? p : null;

    private static readonly object Gate = new();

    /// <summary>An apparition whose slot is lower than the highest slot of the live entries: phase B would insert it somewhere else than at the end of the list.</summary>
    internal const string OutOfOrder = "out-of-order";

    /// <summary>An apparition with no free slot from 1 to 63: phase B would refuse it.</summary>
    internal const string Overflow = "overflow";

    /// <summary>The highest slot seen, reported for every assignment (so the register of a test holds its peak).</summary>
    internal const string Seen = "seen";

    internal static bool Active => Path != null;

    internal static void Report(string kind, string? worldName, int slot, int maxLive)
    {
        if (Path == null)
        {
            return;
        }

        var test = "?";
        foreach (var frame in new StackTrace().GetFrames())
        {
            var type = frame.GetMethod()?.DeclaringType;
            if (type?.Namespace != null && type.Namespace.StartsWith("Alundra.Tests", StringComparison.Ordinal))
            {
                var outer = type;
                while (outer.DeclaringType != null)
                {
                    outer = outer.DeclaringType;
                }

                test = outer.Name + "." + frame.GetMethod()!.Name; // the last such frame wins: the outermost.
            }
        }

        lock (Gate)
        {
            File.AppendAllText(Path, $"{kind} test={test} world={worldName ?? "?"} slot={slot} maxLive={maxLive}{Environment.NewLine}");
        }
    }
}
