#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// Port of <c>PhysicsEngine.FindEntityCollisionCandidate</c> @ 0x80036F34 (decompilation
/// PhysicsEngine.cs:1169-1283) - the entity-pair overlap probe the original's movement resolution
/// uses, ported for E12.d (docs/plan-e12d-interaction-joueur.md) as DETECTION ONLY (D-E12D-1, user
/// decision): the result feeds the player's <see cref="AlundraEntityScriptProxy.XCollisionEntity"/>
/// and from there <c>CheckEntityInteraction</c>; nothing here blocks movement (entity↔entity blocking
/// stays with the E14 chantier).
///
/// Ported as its own function rather than reusing an <see cref="EntitySupport"/> helper (the ÉCHELLES
/// rule n°3: the original has a distinct function, we port it distinct) - with ONE deliberate shared
/// piece: the subject gate is <see cref="EntitySupport.IsEligibleSubject"/>, whose three conjuncts
/// (<c>Flags &amp; Collidable</c>, <c>AnimFlags &amp; NoEntityCollision == 0</c>,
/// <c>PlatformEntity == null</c>) are compared line-by-line identical to this function's own gate at
/// PhysicsEngine.cs:1183-1186 (both decompile the same predicate; only the debug bypass at :1176-1181
/// is not ported - no debug flags exist here). Candidates come pre-filtered from the world's own
/// collidables list, the port of <c>g_collideableEntities</c> (same population rule - see
/// <c>AlundraWorldProxy</c>'s <c>_collidables</c> doc), and the FIRST match in list order wins,
/// exactly like the original's early <c>return</c>.
/// </summary>
public static class AlundraEntityCollision
{
    /// <summary>
    /// The asymmetric AABB overlap of the original, all three axes (X↔Width, Y↔Height, Z↔Depth -
    /// the same axis naming FindEntityCollisionCandidate itself uses, NOT the attack path's): for each
    /// axis, a negative delta (candidate to the left/above/below) tests against the CANDIDATE's own
    /// dimension + 1, a non-negative delta against the SUBJECT's (+1 derived: <c>dif &lt; dim + 1</c>
    /// ⇔ <c>dif &lt;= dim</c>, flush contact counts).
    ///
    /// D-E12D-1's position-source correction (plan relecture P2): the original reads
    /// <c>ModdedPos*</c>, refreshed on every movement attempt (PhysicsEngine.cs:428-430/:849-851);
    /// in this DLL those cached fields are only written at spawn, so this port recomputes
    /// <c>Pos* + Mod*</c> on the fly for subject AND candidate - the established convention of
    /// <see cref="EntitySupport"/> (EntitySupport.cs:112-114), for exactly this staleness reason.
    /// </summary>
    public static AlundraEntityScriptProxy? FindEntityCollisionCandidate(
        AlundraEntityScriptProxy entity, IReadOnlyList<AlundraEntityScriptProxy> collidables)
    {
        // PhysicsEngine.cs:1183-1186 - the subject gate (see this class' own doc on the shared helper).
        if (!EntitySupport.IsEligibleSubject(entity) || collidables.Count == 0)
        {
            return null;
        }

        var moddedPosX = entity.PosX + entity.ModX;
        var moddedPosY = entity.PosY + entity.ModY;
        var moddedPosZ = entity.PosZ + entity.ModZ;

        for (var i = 0; i < collidables.Count; i++)
        {
            var candidate = collidables[i];
            if (ReferenceEquals(candidate, entity))
            {
                continue; // PhysicsEngine.cs:1195-1198.
            }

            var candidateModX = candidate.PosX + candidate.ModX;
            var candidateModY = candidate.PosY + candidate.ModY;
            var candidateModZ = candidate.PosZ + candidate.ModZ;

            // X (PhysicsEngine.cs:1199-1210 / :1263-1270).
            var delta = candidateModX - moddedPosX;
            if (delta < 0)
            {
                if (moddedPosX - candidateModX >= candidate.Width + 1)
                {
                    continue;
                }
            }
            else if (delta >= entity.Width + 1)
            {
                continue;
            }

            // Y (same asymmetric shape, Height).
            delta = candidateModY - moddedPosY;
            if (delta < 0)
            {
                if (moddedPosY - candidateModY >= candidate.Height + 1)
                {
                    continue;
                }
            }
            else if (delta >= entity.Height + 1)
            {
                continue;
            }

            // Z (same asymmetric shape, Depth).
            delta = candidateModZ - moddedPosZ;
            if (delta < 0)
            {
                if (moddedPosZ - candidateModZ >= candidate.Depth + 1)
                {
                    continue;
                }
            }
            else if (delta >= entity.Depth + 1)
            {
                continue;
            }

            return candidate; // first match in list order, like the original's early return.
        }

        return null;
    }
}
