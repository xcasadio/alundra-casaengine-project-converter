#nullable enable
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
    /// E20.g1a T3 (W6, binary <c>0x8003981C-0x80039858</c>, the sort driver <c>0x800397AC</c>): an entity in the hands of another that is collidable (<see cref="EntityFlags.Collidable"/> set, the animation flag <c>0x80</c> clear) is sorted at least as its
    /// carrier: when its depth value <paramref name="depthSortValue"/> (<c>PosY + (IDSV &lt;&lt; 16)</c>) is below the carrier's key (<see cref="Compute(EntityScriptProxy)"/>, signed comparison), the carrier's key replaces it. Any other entity keeps its value.
    /// </summary>
    internal static int RaiseToCarrier(EntityScriptProxy proxy, int depthSortValue)
    {
        if ((proxy.Flags & EntityFlags.Collidable) == 0 || (proxy.AnimFlags & 0x80) != 0 || proxy.PlatformEntity?.GameplayProxy is not EntityScriptProxy carrier)
        {
            return depthSortValue;
        }

        var carrierKey = unchecked((int)Compute(carrier));
        return depthSortValue < carrierKey ? carrierKey : depthSortValue;
    }

    /// <summary>STUB of E20.g1d T1 (tests first): the draw order of a held or thrown object is not written yet.</summary>
    internal const int HeldDrawOffset = 0;

    /// <summary>STUB of E20.g1d T1.</summary>
    internal enum DrawRole
    {
        Plain,
        Carried,
        Thrown,
    }

    /// <summary>STUB of E20.g1d T1.</summary>
    internal readonly record struct DrawTie(int? Coordinate, int LocalOffset);

    /// <summary>STUB of E20.g1d T1.</summary>
    internal static DrawTie ResolveDrawTie(DrawRole role, int? partnerCoordinate, uint objectKey, uint partnerKey) => default;

    /// <summary>STUB of E20.g1d T1.</summary>
    internal static float AnchorY(float renderY, int coordinate) => 0f;
}
