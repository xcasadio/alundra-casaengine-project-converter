#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Ai.Props;

/// <summary>
/// E20.h3a phase A (docs/plan-e20-heros.md section 1.30.2): the native handler E19 (<c>0x8007F690</c>, slot E) of the lids, the sprite types 241 to 243 of the global page. A lid whose program D put it in the status Deactivated (the opcode <c>0x19</c>) is
/// removed by this handler alone: the sound <c>0xE6</c> (230), then <c>DestroyEntity(e, -1)</c> (<c>0x8003A59C</c>, which gives the break effect of the sprite header, 11 for these three). It reads no data word of the entity. The map-page sprites 58 and 59 (the types 314 and 315)
/// run the same native function in the original; they are not registered here (docs/plan-e20-annexe/e20-h3a/README.md, census).
/// </summary>
internal static class BombLidAi
{
    /// <summary>The sprite types of the lids: 241, 242 and 243.</summary>
    internal static readonly int[] SpriteTypes = { 241, 242, 243 };

    /// <summary>The program code of E19 in slot E.</summary>
    internal const int DeactivateCode = 19;

    /// <summary>The sound of the lid that goes: <c>0xE6</c>.</summary>
    internal const int BreakSound = 0xE6;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Registers E19 for the types 241 to 243 only; the disposable takes the key off.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Break(entity));

    /// <summary>E19 (<c>0x8007F690</c>), one tick of a lid in the status Deactivated: the sound, then the destruction by the world the lid belongs to (<see cref="EntityScriptProxy.ScriptHost"/>); an entity without a host does nothing.</summary>
    internal static void Break(EntityScriptProxy entity)
    {
        if (entity.ScriptHost is not { } host)
        {
            return;
        }

        (host as IEntityWorldContext)?.SoundPlayer?.PlaySfx(BreakSound);
        host.DestroyEntity(entity, -1);
    }
}
