#nullable enable
using System;
using Alundra.Maps.Loop;
using Alundra.Rendering.Effects;

namespace Alundra.Entities;

/// <summary>
/// E20.f1b (docs/plan-e20-heros.md section 1.9, ADR-0057, annex <c>docs/plan-e20-annexe/e20-f1/depth-key.md</c>): the depth key <c>+0x1BC</c> of an entity of the binary, as the sort driver <c>0x800399B8</c> leaves it
/// for an entity that carries no other entity and overlaps no collidable lower than itself:
/// <c>((PosY + (IDSV &lt;&lt; 16)) &amp; 0xFFFF0000) + (short)(PosZ &gt;&gt; 16)</c>. For ANY entity proxy (the hero's aura and, since E14.a1b, the break effect of a destroyed entity (its frozen anchor) read it through
/// <see cref="IEffectAnchor"/>), with no hero-only shortcut. Written deviation: no raise by a neighbour, a support or a carrier (the binary's recursive maximum over the collidables); the oracle's 150 ticks of the
/// charge (<c>docs/plan-e20-annexe/e20-f1/oracle/keyprobe.py</c>) do not need it.
/// </summary>
internal static class EntityDepthKey
{
    /// <summary>The key of an entity at <paramref name="posY"/> (16.16) with the sprite bias <paramref name="idsv"/> and the integer height of <paramref name="posZ"/> (16.16, in the binary's frame of Z: the DLL's <c>PosZ + 1</c>, ADR-0026).</summary>
    internal static uint Compute(int posY, int idsv, int posZ)
    {
        unchecked
        {
            return (uint)(((posY + (idsv << 16)) & (int)0xFFFF0000) + (short)(posZ >> 16));
        }
    }

    /// <summary>The key of <paramref name="proxy"/>: its <c>PosY</c>, the IDSV of the sprite it shows (<see cref="FrameSyncPasses.ResolveIdsv"/>) and its <c>PosZ + 1</c>.</summary>
    internal static uint Compute(EntityScriptProxy proxy)
        => Compute(proxy.PosY, FrameSyncPasses.ResolveIdsv(proxy), unchecked(proxy.PosZ + 1));

    /// <summary>
    /// E20.g1a T3 (W6, binary <c>0x8003981C-0x80039858</c>, the sort driver <c>0x800397AC</c>), widened by E20.g1d (D-G1D-2, D-E20-89 (2), a written deviation): an entity in the hands of another is sorted at least as its carrier: when its depth value
    /// <paramref name="depthSortValue"/> (<c>PosY + (IDSV &lt;&lt; 16)</c>) is below the carrier's key (<see cref="Compute(EntityScriptProxy)"/>, signed comparison), the carrier's key replaces it. The binary raises the collidable ones only (the flag
    /// <see cref="EntityFlags.Collidable"/> set, the animation flag <c>0x80</c> clear); the DLL raises every held object, the bomb (not a collidable) included, so that it is never behind its carrier, not even at the take-off of the jump facing up
    /// (the tick 162 of <c>docs/plan-e20-annexe/e20-g1d/keys/out_bomb_keys2.txt</c>). An entity nobody carries keeps its value.
    /// </summary>
    internal static int RaiseToCarrier(EntityScriptProxy proxy, int depthSortValue)
    {
        if (proxy.PlatformEntity?.GameplayProxy is not EntityScriptProxy carrier)
        {
            return depthSortValue;
        }

        var carrierKey = unchecked((int)Compute(carrier));
        return depthSortValue < carrierKey ? carrierKey : depthSortValue;
    }

    /// <summary>
    /// E20.g1d (docs/plan-e20-heros.md section 1.24.8, D-G1D-4): K, the offset of the local sort that puts the pieces of a held object after every piece of its carrier. It must exceed the largest <c>DrawOrder</c> of the pieces of the hero (13) and of the
    /// liftable objects (6, the bomb): <c>max(14, largest + 1)</c>, pinned against the export by <c>EntityDrawTieTests</c>.
    /// </summary>
    internal const int HeldDrawOffset = 14;

    /// <summary>What the sort pass writes an entity for: nothing special, in the hands of another (the hero), or thrown by the hero and still flying.</summary>
    internal enum DrawRole
    {
        Plain,
        Carried,
        Thrown,
    }

    /// <summary>The two things the pass writes beyond <see cref="CasaEngine.Framework.Scene.Entities.Components.DepthSortable2DComponent.Elevation"/>: the sort coordinate the sprite must have (null: none, the anchor stays (0, 0)) and its local sort offset.</summary>
    internal readonly record struct DrawTie(int? Coordinate, int LocalOffset);

    /// <summary>
    /// E20.g1d (D-G1D-1 to D-G1D-3, D-G1D-5): the pure rule of the fine draw order of a held or thrown object, against its partner (the carrier, or the thrower). <paramref name="partnerCoordinate"/> is the sort coordinate of the partner's sprite (null when the
    /// rendered poses are not known: only the offset is then written). <see cref="DrawRole.Carried"/>: the partner's coordinate and +K, so the object is drawn right after the pieces of its carrier. <see cref="DrawRole.Thrown"/>: the partner's coordinate plus 100
    /// per pixel of difference of the high halves of the two binary keys (<see cref="Compute(EntityScriptProxy)"/>, <paramref name="objectKey"/> and <paramref name="partnerKey"/>), and, at the same pixel, +K when the key of the object is above the partner's, -K when
    /// it is below, 0 when equal (the binary does not order two equal keys): the order against the partner is then the binary's. <see cref="DrawRole.Plain"/>: nothing.
    /// </summary>
    internal static DrawTie ResolveDrawTie(DrawRole role, int? partnerCoordinate, uint objectKey, uint partnerKey)
    {
        switch (role)
        {
            case DrawRole.Carried:
                return new DrawTie(partnerCoordinate, HeldDrawOffset);
            case DrawRole.Thrown:
                var delta = ((int)objectKey >> 16) - ((int)partnerKey >> 16);
                var coordinate = partnerCoordinate is { } c ? c + 100 * delta : (int?)null;
                return new DrawTie(coordinate, delta != 0 ? 0 : Math.Sign(((int)objectKey).CompareTo((int)partnerKey)) * HeldDrawOffset);
            default:
                return default;
        }
    }

    /// <summary>
    /// The <c>SortAnchorLocal.Y</c> that makes the engine's own computation (<c>round(-(renderY + anchor) * 100)</c>, top-down Y up) give EXACTLY <paramref name="coordinate"/> for a sprite whose rendered translation is at <paramref name="renderY"/>
    /// (the rounding of the engine absorbs the float error of the sum: the wanted value is an integer hundredth).
    /// </summary>
    internal static float AnchorY(float renderY, int coordinate) => -(coordinate / 100f) - renderY;
}
