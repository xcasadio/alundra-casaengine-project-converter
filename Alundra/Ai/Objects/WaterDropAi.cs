#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Objects;

/// <summary>E20.g2c3 skeleton (tests first): compiles, does nothing yet.</summary>
internal static class WaterDropAi
{
    internal const int SpriteType = TorchAi.WaterDropSpriteType;

    internal const int LoadCode = 19;

    internal const int TickCode = 71;

    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramALoad, LoadCode), (ScriptHelper.ProgramCTick, TickCode) };

    internal readonly record struct DropStep(byte State, ushort Counter, uint? Animation, uint Flags, bool Restore, bool Extinguish);

    internal static DropStep Step(byte state, ushort counter, bool landed, bool animationDone, uint flags, bool targetIsTorch)
        => throw new NotImplementedException();

    internal static void Load(EntityScriptProxy drop) => throw new NotImplementedException();

    internal static void Tick(EntityScriptProxy drop, IEntityWorldContext context) => throw new NotImplementedException();

    internal static void Register(NativeSpriteRegistry registry) => throw new NotImplementedException();
}
