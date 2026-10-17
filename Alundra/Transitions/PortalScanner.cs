#nullable enable
using System.Collections.Generic;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;

namespace Alundra.Transitions;

/// <summary>
/// T3 (docs/plan-transitions-carte.md §3): port of <c>GameEngine.GetActivatedPortal</c>
/// (<c>GameEngine.cs:2418-2438</c>, §1.2.b) - the portal slot scan every warp check runs before doing
/// anything else. DETECTION ONLY, same scope note as <see cref="AlundraPortalTrigger"/>'s own class doc.
/// </summary>
internal static class AlundraPortalScanner
{
    /// <summary>
    /// First slot (in <paramref name="portals"/> order - i.e. record/slot order, §1.2.b) whose
    /// <c>[X1,X2] x [Y1,Y2]</c> rectangle contains (<paramref name="tileX"/>, <paramref name="tileY"/>)
    /// wins - NOT the smallest/closest rectangle, the FIRST match. Binary <c>0x8003166C</c> (E19.m1, M-10):
    /// it walks the 64 slots of 12 bytes and returns the first one that holds the hero tile; it never reads
    /// the slot's destination map id (+4), so a slot with <c>DestMapId == 0</c> is returned like any other
    /// (the decompilation's <c>return null</c> for it is not in the binary) and a later slot that might also
    /// contain the tile is never reached.
    /// </summary>
    internal static AlundraPortalRecord? FindPortalAtTile(IReadOnlyList<AlundraPortalRecord> portals, int tileX, int tileY)
    {
        foreach (var portal in portals)
        {
            if (tileX >= portal.X1 && tileX <= portal.X2 && tileY >= portal.Y1 && tileY <= portal.Y2)
            {
                return portal;
            }
        }

        return null;
    }
}
