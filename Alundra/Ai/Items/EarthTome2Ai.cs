#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>E20.h4c2 T1 (phase A), SKELETON of the tests commit: every function is inert so that the tests compile and fail; the code commit replaces it.</summary>
internal static class EarthTome2Ai
{
    internal static readonly int[] SpriteTypes = { 17 };

    internal const int ImpactCode = 13;

    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ImpactCode) };

    internal static readonly byte[] TableBytes = Array.Empty<byte>();

    internal static readonly IReadOnlyList<(int Tick, int P)> Table = Array.Empty<(int, int)>();

    internal static int Driver(EntityScriptProxy hero) => 0;

    internal static bool ShouldDestroyAtBirth(int heroPosZ, int terrain) => false;

    internal static void ImpactDeactivate(EntityScriptProxy entity)
    {
    }

    internal static void Cancel(EntityScriptProxy hero)
    {
    }

    internal static IDisposable Register(NativeSpriteRegistry registry) => new Nothing();

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
