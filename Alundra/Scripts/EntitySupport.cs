#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// E4.f (docs/plan-e4-deplacement-scripte.md, decision E4-4): the entity-vs-entity Z SUPPORT clamp - NOT
/// <see cref="AlundraEntityScriptProxy.PlatformEntity"/> (the "carried/thrown" relation, sites
/// <c>PlayerManager.cs:1117/:2027/:2224</c>, <c>FunctionTypeC.cs</c> - out of scope, never assigned by
/// this runtime) - the generic "standing on" relation the original tracks as
/// <see cref="AlundraEntityScriptProxy.RidingEntity"/>, fed by a per-frame, NO-LATCH re-evaluation of
/// <c>CheckEntityCollisionDown</c>'s own entity-candidate branch (PhysicsEngine.cs:171-230 detection,
/// <c>:123-139</c> consumption). Shared between <see cref="AlundraEntityScriptProxy.EvaluateEntitySupport"/>
/// (the DLL's own controller-driven per-frame hook) and the intro trace harness's own simulated
/// kinematics (<c>Alundra.Tests/IntroTraceHarnessTests.cs</c>, no controller - it consumes
/// <see cref="TryFindSupport"/> directly against <c>Pos*</c>) - "the same shared detection" per the plan's
/// own wording, so this fidelity-critical arithmetic exists exactly once.
/// </summary>
internal static class EntitySupport
{
    /// <summary>
    /// <c>EntityAnimFlags.NoEntityCollision</c> (alundra-datas-analyser
    /// AlundraTools/AlundraEngine/Gameplay/EntityAnimFlags.cs:33) - same bit <see cref="EntitySearchService"/>
    /// already ports as its own local constant (case 4, "all entities on the ground"); kept as an
    /// independent copy here rather than a cross-file internal, matching this codebase's existing "one
    /// small documented local constant per consumer" style for this specific bit.
    /// </summary>
    private const int NoEntityCollisionAnimFlag = 0x80;

    /// <summary>
    /// Port of the collidable-entity list criteria (<c>EntityManager.cs:994</c>,
    /// <c>g_collideableEntities</c> population): <see cref="EntityFlags.Collidable"/> set, the
    /// per-animation <see cref="NoEntityCollisionAnimFlag"/> bit clear, and
    /// <see cref="AlundraEntityScriptProxy.PlatformEntity"/> null (always true in this runtime's current
    /// scope - see this class' own doc). Fills <paramref name="buffer"/> in place (<c>Clear</c> then
    /// re-add) so callers can reuse the SAME list instance every frame with no per-frame allocation,
    /// exactly like <c>IntroTraceHarnessTests</c>' own <c>entitiesThisFrame</c> snapshot pattern and
    /// <c>AlundraWorldProxy</c>'s own <c>_updateProxies</c>.
    /// </summary>
    internal static void BuildCollidables(IReadOnlyList<AlundraEntityScriptProxy> spawnedEntities, List<AlundraEntityScriptProxy> buffer)
    {
        buffer.Clear();

        for (var i = 0; i < spawnedEntities.Count; i++)
        {
            var candidate = spawnedEntities[i];
            if ((candidate.Flags & EntityFlags.Collidable) != 0
                && (candidate.AnimFlags & NoEntityCollisionAnimFlag) == 0
                && candidate.PlatformEntity == null)
            {
                buffer.Add(candidate);
            }
        }
    }

    /// <summary>
    /// Port of <c>CheckEntityCollisionDown</c>'s ENTITY-candidate branch only (PhysicsEngine.cs:189-258) -
    /// the TERRAIN-based half of that function (<c>platformTopZ = ModdedPosZ + FinalForceZ; collisionDetected
    /// = platformTopZ &lt;= TerrainHeight</c>) is a separate concern each caller already owns (the
    /// harness's own terrain probe; the engine's own <c>CharacterControllerComponent</c> ground-snap
    /// against the real <c>AlundraCellsCollisionField</c> for a controller-driven entity) - see each call
    /// site's own doc for how the two are merged. Takes the HIGHEST qualifying candidate (ties broken by
    /// iteration order, same as the original's own "replace only if not lower" rule) whose top sits
    /// STRICTLY below <paramref name="entity"/>'s own feet (<c>ModdedPosZ</c>) -
    /// <b>this comparator must stay strict</b>: a real body box's own <c>Depth</c> is
    /// <c>(SizeZ &lt;&lt; 16) − 1</c> (one 16.16 unit short of a full tile edge, <c>EntityManager.cs:192-199</c>),
    /// which is exactly what lets an entity resting flush on top of another satisfy this test - a
    /// non-strict (<c>&lt;=</c>) comparator would ALSO support an entity merely touching the platform's
    /// top edge, which the original never does (see docs/plan-e4-deplacement-scripte.md's own E4.f
    /// "Pourquoi" note on this exact edge).
    /// </summary>
    internal static bool TryFindSupport(
        AlundraEntityScriptProxy entity, IReadOnlyList<AlundraEntityScriptProxy> collidables,
        out AlundraEntityScriptProxy? support, out int supportTopZ)
    {
        support = null;
        supportTopZ = 0;
        var bestCandidateTop = int.MinValue;

        var moddedPosX = entity.PosX + entity.ModX;
        var moddedPosY = entity.PosY + entity.ModY;
        var moddedPosZ = entity.PosZ + entity.ModZ;

        for (var i = 0; i < collidables.Count; i++)
        {
            var candidate = collidables[i];
            if (ReferenceEquals(candidate, entity))
            {
                continue;
            }

            var candidateModZ = candidate.PosZ + candidate.ModZ;
            var candidateTop = candidateModZ + candidate.Depth;

            // PhysicsEngine.cs:205 - the strict comparator (see this method's own doc).
            if (candidateTop >= moddedPosZ)
            {
                continue;
            }

            // PhysicsEngine.cs:219/226/240/247 - "platformTopZ <= deltaY": only a candidate at least as
            // high as the current best replaces it (keep the HIGHEST qualifying support).
            if (support != null && candidateTop < bestCandidateTop)
            {
                continue;
            }

            var candidateModX = candidate.PosX + candidate.ModX;
            var candidateModY = candidate.PosY + candidate.ModY;

            // PhysicsEngine.cs:207-230 - asymmetric X/Y overlap (candidate's own Width/Height feeds the
            // threshold when it sits to the left/above; entity's own Width/Height when to the
            // right/below or exactly aligned) - ported nested, same shape as the original.
            var deltaX = candidateModX - moddedPosX;
            bool overlapsX;
            if (deltaX < 0)
            {
                overlapsX = moddedPosX - candidateModX < candidate.Width + 1;
            }
            else
            {
                overlapsX = deltaX < entity.Width + 1;
            }

            if (!overlapsX)
            {
                continue;
            }

            var deltaY = candidateModY - moddedPosY;
            bool overlapsY;
            if (deltaY < 0)
            {
                overlapsY = moddedPosY - candidateModY < candidate.Height + 1;
            }
            else
            {
                overlapsY = deltaY < entity.Height + 1;
            }

            if (!overlapsY)
            {
                continue;
            }

            support = candidate;
            bestCandidateTop = candidateTop;
            supportTopZ = candidateTop + 1; // PhysicsEngine.cs:219/226/240/247 - "deltaY + 1".
        }

        return support != null;
    }

    /// <summary>
    /// Port of <c>CheckRidingEntities</c> (PhysicsEngine.cs:1288-1358) - a SEPARATE, EXACT-match test (not
    /// <see cref="TryFindSupport"/>'s own strict-below/highest-wins one) that only feeds
    /// <see cref="AlundraEntityScriptProxy.RidingEntity"/> for <see cref="EntitySearchService"/>'s own
    /// search types 5/6 (who is riding whom) - completely independent of the actual Z clamp above. Runs
    /// over every <paramref name="collidables"/> entry that carries <see cref="EntityFlags.Gravity"/> and
    /// NOT <see cref="EntityFlags.NoRiders"/> (<c>(Flags &amp; (Gravity|NoRiders)) == Gravity</c>,
    /// PhysicsEngine.cs:1290); for each such entity, finds another collidable whose top sits EXACTLY at
    /// (not below, not overlapping into) the entity's own feet, with the same asymmetric overlap test as
    /// <see cref="TryFindSupport"/> - ported field for field, INCLUDING the original's own apparent quirk
    /// of testing the Y axis with <c>entity.Depth + 1</c> / <c>other.Height + 1</c> instead of
    /// <c>Height</c>/<c>Height</c> (PhysicsEngine.cs:1338-1356, both variables literally named that way in
    /// the decompilation) - not "fixed" here, since this is a faithfulness port, not a redesign.
    /// </summary>
    internal static void UpdateRidingEntities(IReadOnlyList<AlundraEntityScriptProxy> collidables)
    {
        for (var i = 0; i < collidables.Count; i++)
        {
            var entity = collidables[i];
            if ((entity.Flags & (EntityFlags.Gravity | EntityFlags.NoRiders)) != EntityFlags.Gravity)
            {
                continue;
            }

            var moddedPosX = entity.PosX + entity.ModX;
            var moddedPosY = entity.PosY + entity.ModY;
            var moddedPosZ = entity.PosZ + entity.ModZ;
            var entityWidth = entity.Width + 1;
            var entityDepth = entity.Depth + 1; // sic - see this method's own doc.

            entity.RidingEntity = null;

            for (var j = 0; j < collidables.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var other = collidables[j];
                var otherModX = other.PosX + other.ModX;
                var otherModY = other.PosY + other.ModY;
                var otherModZ = other.PosZ + other.ModZ;
                var otherTopZ = otherModZ + other.Depth + 1;

                if (otherTopZ != moddedPosZ)
                {
                    continue;
                }

                var deltaX = otherModX - moddedPosX;
                if (deltaX < 0)
                {
                    if (!(moddedPosX - otherModX < other.Width + 1))
                    {
                        continue;
                    }
                }
                else if (!(deltaX < entityWidth))
                {
                    continue;
                }

                var deltaY = otherModY - moddedPosY;
                if (deltaY < 0)
                {
                    if (!(moddedPosY - otherModY < other.Height + 1))
                    {
                        continue;
                    }
                }
                else if (!(deltaY < entityDepth)) // sic - see this method's own doc.
                {
                    continue;
                }

                entity.RidingEntity = other.LogicContextEntity;
                break;
            }
        }
    }
}
