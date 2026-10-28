#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>E20.h4d1 (docs/plan-e20-heros.md section 1.31.5), phase A SKELETON (tests first, R-EL-4): the fire tome 2, the item <c>0x30</c>. The bodies come with the code commit of the task.</summary>
internal static class FireTome2Ai
{
    /// <summary>The sprite type of the column of fire: 21.</summary>
    internal static readonly int[] SpriteTypes = { 21 };

    /// <summary>The program code of E28 in slot E.</summary>
    internal const int ColumnEndCode = 28;

    /// <summary>The keys this class registers (the pins of the registry follow them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, ColumnEndCode) };

    internal static int Driver(EntityScriptProxy hero) => throw new NotImplementedException();

    internal static void Cancel(EntityScriptProxy hero) => throw new NotImplementedException();

    internal static void ColumnEnds(EntityScriptProxy entity) => throw new NotImplementedException();

    /// <summary>Registers E28 for the type 21 alone; the disposable takes the key off.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, ColumnEndCode, SpriteTypes, (entity, context, state) => ColumnEnds(entity));
}
