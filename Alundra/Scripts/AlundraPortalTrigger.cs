#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

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
    /// wins - NOT the smallest/closest rectangle, the FIRST match, exactly the original's own
    /// <c>foreach</c> + early return (<c>GameEngine.cs:2420-2434</c>). A winning slot whose
    /// <c>DestMapId == 0</c> returns null and STOPS the scan right there (the original's own
    /// <c>return null;</c> inside the loop body, not a <c>continue</c> to the next slot) - a later slot
    /// that might also contain the tile is never reached.
    /// </summary>
    internal static AlundraPortalRecord? FindPortalAtTile(IReadOnlyList<AlundraPortalRecord> portals, int tileX, int tileY)
    {
        foreach (var portal in portals)
        {
            if (tileX >= portal.X1 && tileX <= portal.X2 && tileY >= portal.Y1 && tileY <= portal.Y2)
            {
                return portal.DestMapId == 0 ? null : portal;
            }
        }

        return null;
    }
}

/// <summary>
/// T3 (docs/plan-transitions-carte.md §1.2.a, §3) - port of the TRIGGER half of
/// <c>PlayerManager.CheckAndExecuteWarp</c> (<c>PlayerManager.cs:3424-3459</c>), conjunction by
/// conjunction, EXCLUDING the dev-only debug-log branch (<c>StaticVariables.DebugPortalsEnabled</c>,
/// <c>PlayerManager.cs:3358-3417</c> - a logging path with no engine-visible effect, never active in the
/// retail build path this port targets, not ported).
///
/// DETECTION ONLY (T3's whole scope, per the ticket): <see cref="TryGetTrigger"/> is pure - it reads
/// <see cref="AlundraEntityScriptProxy"/>/<see cref="AlundraPadState"/>/<see cref="AlundraGameState"/>/the
/// portal list and writes nothing, starts no fade, requests no world change, and never calls
/// <c>HandleWarpTransition</c>. It is the seam T4's <c>AlundraWarpDirector</c> will consume (via
/// <see cref="AlundraPlayerManager.MovePlayer"/>'s own call site, wired through
/// <see cref="IAlundraScriptHost.OnPortalTriggerDetected"/> - see that member's own doc) - "exposé par
/// une couture que T4 remplira".
/// </summary>
internal static class AlundraPortalTrigger
{
    /// <summary>Bit tested against <see cref="AlundraEntityScriptProxy.CombinedVramFlagsAND"/> - the
    /// "hole" tile (§1.2.a, <c>PlayerManager.cs:3424</c>: <c>directionId &amp; 4U</c>).</summary>
    private const uint HoleBit = 0x4;

    /// <summary>Bit tested against <see cref="AlundraEntityScriptProxy.CombinedVramFlagsAND"/> - the
    /// "portal floor" tile (§1.2.a, <c>PlayerManager.cs:3428</c>: <c>directionId &amp; 0x8000U</c>).</summary>
    private const uint PortalFloorBit = 0x8000;

    /// <summary>
    /// Port of <c>StaticVariables.SHORT_ARRAY_80022776</c> (address 0x80022776, <c>StaticVariables.cs:530</c>):
    /// <c>[0x4000, 0x1000, 0x8000, 0x2000]</c>, indexed by <see cref="AlundraPortalRecord.RequiredFacingDirection"/>
    /// (domain 0..3), gives the <see cref="AlundraPadState.ButtonsHold"/> mask that must be held for the
    /// warp to trigger. The four values already equal this port's own <see cref="AlundraPadState"/>
    /// Down/Up/Left/Right masks (0x4000/0x1000/0x8000/0x2000, <c>AlundraPlayerController.cs:28-31</c>) -
    /// same literal PSX button-bit layout, not a coincidence, so no separate remapping table is needed.
    /// </summary>
    private static readonly uint[] RequiredInputByFacing = { 0x4000, 0x1000, 0x8000, 0x2000 };

    /// <summary>
    /// Port of the trigger half of <c>PlayerManager.CheckAndExecuteWarp</c> (<c>PlayerManager.cs:3424-3459</c>).
    /// Returns the winning portal plus its resolved arrival direction
    /// (<c>AnimationTables.CardinalDirectionTable[portal.ArrivalDirectionIndex]</c>, §1.2.c - e.g. index 1
    /// gives 0x10, never the raw index 1 itself) when a warp should begin THIS call; otherwise null.
    ///
    /// DEVIATION (documented, sibling of D-T-10/D-T-12): <see cref="AlundraGameState.IsWarpDisabled"/> is
    /// checked HERE, even though the original only tests <c>g_isWarpDisabled</c> inside
    /// <c>HandleWarpTransition</c> (downstream of this predicate, T4's own scope) - folded in per the
    /// plan's point 5 so this already-wired seam behaves correctly before T4's director exists, and T4
    /// need not re-check it once it does.
    /// </summary>
    internal static (AlundraPortalRecord Portal, uint ArrivalDirectionId)? TryGetTrigger(
        AlundraEntityScriptProxy player,
        in AlundraPadState pad,
        AlundraGameState state,
        IReadOnlyList<AlundraPortalRecord> portals)
    {
        if (state.IsWarpDisabled)
        {
            return null;
        }

        var combinedAnd = player.CombinedVramFlagsAND;

        if ((combinedAnd & HoleBit) != 0)
        {
            // PlayerManager.cs:3455-3468 (hole branch): unconditional once a portal is found here - no
            // orientation test, no held key, no PlayerControlFlags test.
            var holePortal = AlundraPortalScanner.FindPortalAtTile(portals, player.TileX, player.TileY);
            return holePortal == null
                ? null
                : (holePortal, AnimationTables.CardinalDirectionTable[holePortal.ArrivalDirectionIndex]);
        }

        // PlayerManager.cs:3425-3453 (portal-floor branch).
        if ((combinedAnd & PortalFloorBit) == 0)
        {
            return null;
        }

        // PlayerManager.cs:3429 - a test PROPER TO THIS BRANCH (g_playerControlFlags != 0), distinct
        // from the InputBlockedMask gate MovePlayer's own caller applies elsewhere - do not merge them.
        if (state.PlayerControlFlags != 0)
        {
            return null;
        }

        var portal = AlundraPortalScanner.FindPortalAtTile(portals, player.TileX, player.TileY);
        if (portal == null)
        {
            return null;
        }

        var requiredInput = RequiredInputByFacing[portal.RequiredFacingDirection];
        if ((pad.ButtonsHold & requiredInput) == 0)
        {
            return null;
        }

        // PlayerManager.cs:3446 - AnimationDirection (domain 0..3), NEVER TargetDirection (domain
        // {0x00, 0x08, 0x10, 0x18}).
        if ((uint)player.AnimationDirection != portal.RequiredFacingDirection)
        {
            return null;
        }

        return (portal, AnimationTables.CardinalDirectionTable[portal.ArrivalDirectionIndex]);
    }
}
