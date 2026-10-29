#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Combat;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4d2 (docs/plan-e20-heros.md section 1.31.6): the fire tome 1, the item <c>0x2F</c>: C63 (<c>0x8007AC60</c>) for the sprite type 23 and the cancellation <c>0x80034D2C</c>. Skeleton of the tests-first commit (compiles, does nothing: the real bodies come with the code).
/// </summary>
internal static class FireTome1Ai
{
    /// <summary>The sprite type of the fire balls: 23.</summary>
    internal static readonly int[] SpriteTypes = { 23 };

    /// <summary>The program code of C63 in slot C.</summary>
    internal const int BallCode = 63;

    /// <summary>The keys this class registers.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, BallCode) };

    /// <summary>C63 as the registry runs it.</summary>
    internal static void BallTick(EntityScriptProxy ball, IEntityWorldContext context)
    {
    }

    /// <summary>C63 with the count of the query given and the settings of the query.</summary>
    internal static void BallTick(EntityScriptProxy ball, IReadOnlyList<EntityScriptProxy> entities, int count, bool inclusiveBound = TargetQuery.InclusiveBound, int sortThreshold = TargetQuery.SortThreshold)
    {
    }

    /// <summary>The cancellation <c>0x80034D2C</c> of the item <c>0x2F</c>.</summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
    }

    /// <summary>Registers C63 for the type 23 alone.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, BallCode, SpriteTypes, (entity, context, state) => BallTick(entity, context));
}
