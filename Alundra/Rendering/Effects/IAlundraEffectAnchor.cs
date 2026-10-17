#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;

namespace Alundra.Rendering.Effects;

/// <summary>
/// What an attached or detached native effect (modes 1 and 3) follows: the position, the depth key and the status of an entity of the binary (the key is <c>+0x1BC</c> of the entity). <see cref="AlundraEntityScriptProxy"/>
/// implements it (E20.f1b: the hero's charge aura follows its entity), and the break effect of a destroyed entity takes a frozen snapshot of it (<see cref="AlundraFrozenEffectAnchor"/>, E14.a1b); the other attached creators
/// and the detached one are native sites of E13 and E14.
/// </summary>
public interface IAlundraEffectAnchor
{
    /// <summary>The position, 16.16 (the binary's <c>+0x114/118/11C</c>).</summary>
    int PosX { get; }

    int PosY { get; }

    int PosZ { get; }

    /// <summary>The depth key of the entity (<c>+0x1BC</c>) the effect's own key follows.</summary>
    uint DepthKey { get; }

    /// <summary>The status of the entity: 0 free (gone), 4 flagged to be destroyed; any other value is alive.</summary>
    int Status { get; }

    /// <summary>
    /// E20.f1b (D-E20-29, amends ADR-0049): the sort coordinate of the anchor's SPRITE as the engine computes it (<c>100 * (y - z)</c> of the sprite's render position, in whole pixels), which an anchored slot
    /// (modes 1 and 3) sorts on, plus its <see cref="AlundraEffectSlot.DepthOffset"/>, instead of the coordinate of its own, higher, position. The default is the formula of the Alundra sprites (sort anchor 0).
    /// </summary>
    int SortCoordinate => 100 * ((PosY >> 16) - (PosZ >> 16));
}
