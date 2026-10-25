#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;

namespace Alundra.Ai.Projectiles;

/// <summary>
/// E20.f6b phase A, SKELETON of T1 (docs/plan-e20-heros.md section 1.22.2): the native handlers C60 (<c>0x8007A8A0</c>, the trail) and E9 (<c>0x8007EF50</c>, the explosion and the puffs) of the fire and ice balls, the sprite types 10 to 15 of the global page.
/// The bodies come with the code of the phase; the skeleton only fixes the names the tests compile against.
/// </summary>
internal static class BallAi
{
    /// <summary>The sprite types of the balls: 10 to 15 (11 fire, 12 charged fire, 14 ice, 15 charged ice; 10 and 13 have no weapon sheet).</summary>
    internal static readonly int[] SpriteTypes = { 10, 11, 12, 13, 14, 15 };

    /// <summary>The program code of C60 in slot C.</summary>
    internal const int TickCode = 60;

    /// <summary>The program code of E9 in slot E.</summary>
    internal const int DeactivateCode = 9;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, TickCode), (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Skeleton: registers nothing.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry) => new Nothing();

    /// <summary>Skeleton: C60 does nothing.</summary>
    internal static void Trail(EntityScriptProxy entity)
    {
    }

    /// <summary>Skeleton: E9 does nothing.</summary>
    internal static void Explode(EntityScriptProxy entity)
    {
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
