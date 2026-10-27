#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4c1 phase A (docs/plan-e20-heros.md section 1.31.3, D-H4C1-1): the earth tome 1 (item <c>0x2B</c>): the driver <c>0x80035320</c> and the native handler E3 (<c>0x8007EDA0</c>, slot E) for the sprite type 16 only.
/// SKELETON of the tests-first commit: every function is inert, the tests are red against it.
/// </summary>
internal static class EarthTome1Ai
{
    /// <summary>The sprite type of the rocks: 16.</summary>
    internal static readonly int[] SpriteTypes = { 16 };

    /// <summary>The program code of E3 in slot E.</summary>
    internal const int ImpactCode = 3;

    /// <summary>The keys this class registers (the pins of the registry follow them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ImpactCode) };

    /// <summary>The 194 bytes of the table <c>0x8002343C</c> (32 entries of 6 bytes and the sentinel word).</summary>
    internal static readonly byte[] TableBytes = Array.Empty<byte>();

    /// <summary>The table decoded: (tick, speed of Z, delta of direction).</summary>
    internal static readonly IReadOnlyList<(int Tick, int Speed, int Delta)> Table = Array.Empty<(int, int, int)>();

    /// <summary>The driver <c>0x80035320</c>.</summary>
    internal static int Driver(EntityScriptProxy hero) => 1;

    /// <summary>E3 (<c>0x8007EDA0</c>).</summary>
    internal static void ImpactDeactivate(EntityScriptProxy entity)
    {
    }

    /// <summary>The cancellation <c>0x800351A0</c>: neutral.</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
    }

    /// <summary>Registers E3 for the type 16 only.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry) => new NoRegistration();

    private sealed class NoRegistration : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
