#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;

namespace Alundra.Combat;

/// <summary>
/// E20.h4b1 (docs/plan-e20-heros.md section 1.31.2): the target query of the spells <c>0x8003AF70</c> and its sort <c>0x8003AE04</c> as a pure function. Inert in production (no caller yet).
/// </summary>
public static class TargetQuery
{
    /// <summary>Production value of Q-H4B-2 (correct the defect of the original): the highest slot is scanned too (the binary stops one short).</summary>
    public const bool InclusiveBound = true;

    /// <summary>Production value of Q-H4B-3 (correct the defect of the original): the sort runs from 2 results (the binary sorts from 3).</summary>
    public const int SortThreshold = 2;

    public static IReadOnlyList<EntityScriptProxy> Find(
        IReadOnlyList<EntityScriptProxy> entities, int count, EntityScriptProxy caller, uint mask, int type, bool inclusiveBound = InclusiveBound, int sortThreshold = SortThreshold)
    {
        return Array.Empty<EntityScriptProxy>();
    }
}
