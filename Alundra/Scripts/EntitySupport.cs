#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;

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
    /// <c>AlundraWorldProxy</c>'s own <c>_updateProxies</c>. Same predicate as <see cref="IsEligibleSubject"/>
    /// below - one entity can be both a CANDIDATE (via this list) and a SUBJECT (via that gate) of the
    /// exact same test, since <c>CheckEntityCollisionDown</c> uses the identical criteria for both roles
    /// (PhysicsEngine.cs:189, :994).
    /// </summary>
    internal static void BuildCollidables(IReadOnlyList<AlundraEntityScriptProxy> spawnedEntities, List<AlundraEntityScriptProxy> buffer)
    {
        buffer.Clear();

        for (var i = 0; i < spawnedEntities.Count; i++)
        {
            var candidate = spawnedEntities[i];

            // E19.d2b (D-E19-29): the binary's list takes no entity flagged for destruction (its status is not 2 or 3 once
            // flagged); the soulevables STAY in the list, their exclusion as obstacles belongs to the movement obstacle probe.
            if (candidate.Status != EntityStatus.FlagToDestroy && IsEligibleSubject(candidate))
            {
                buffer.Add(candidate);
            }
        }
    }

    /// <summary>
    /// Verifier A2 (PhysicsEngine.cs:189): the SAME gate <see cref="BuildCollidables"/> uses to decide
    /// candidacy, reused here as the SUBJECT-eligibility guard <see cref="TryFindSupport"/>'s own callers
    /// must apply BEFORE ever searching for a support - <c>CheckEntityCollisionDown</c> is only ever
    /// called (from <c>MoveEntity</c>) for an entity that itself passes
    /// <c>(entity.Flags &amp; Collidable) != 0 &amp;&amp; (entity.AnimFlags &amp; NoEntityCollision) == 0
    /// &amp;&amp; entity.PlatformEntity == null</c>. <see cref="AlundraEntityScriptProxy.PlatformEntity"/>
    /// is always null in this runtime's current scope (the "carried/thrown" relation, out of scope - see
    /// this class' own doc), so this gate's third conjunct is checked anyway (faithfully, not assumed)
    /// rather than special-cased away.
    /// </summary>
    internal static bool IsEligibleSubject(AlundraEntityScriptProxy entity)
        => (entity.Flags & EntityFlags.Collidable) != 0
            && (entity.AnimFlags & NoEntityCollisionAnimFlag) == 0
            && entity.PlatformEntity == null;

    /// <summary>
    /// Port of <c>CheckEntityCollisionDown</c>'s ENTITY-candidate branch (PhysicsEngine.cs:189-258),
    /// INCLUDING the full original conjunct at <c>:205</c> - verifier A1. The TERRAIN-based seed
    /// (<c>platformTopZ = ModdedPosZ + FinalForceZ</c>, clamped UP to <c>TerrainHeight + 1</c> when the
    /// natural step would go at-or-below terrain, PhysicsEngine.cs:180-187 -
    /// <c>Math.Max(ModdedPosZ + FinalForceZ, TerrainHeight + 1)</c>, algebraically identical to the
    /// original's own if/reassign) is each caller's own concern to compute and pass in as
    /// <paramref name="platformTopZSeed"/> - the harness's own terrain probe for a bare proxy; for a
    /// controller-driven entity in production there is no DLL-tracked <c>TerrainHeight</c> at all (the
    /// engine's own <c>CharacterControllerComponent</c> ground-snap against the real
    /// <c>AlundraCellsCollisionField</c> owns terrain separately), so that caller passes the UNCLAMPED
    /// <c>ModdedPosZ + FinalForceZ</c> - see each call site's own doc.
    ///
    /// The candidate conjunct is now BOTH halves of PhysicsEngine.cs:205: <c>candidateTop &lt;
    /// entity.ModdedPosZ</c> (STRICT - see below) AND <c>platformTopZ &lt;= candidateTop</c> (this tick's
    /// own downward reach must actually get TO OR PAST the candidate's top - without this half, a falling
    /// entity would snap onto any overlapping candidate however far below, the very first tick it starts
    /// overlapping in X/Y, instead of only once its own vertical step legitimately reaches that height).
    /// <paramref name="platformTopZSeed"/> is itself UPDATED to <c>candidateTop + 1</c> once a qualifying
    /// candidate is found (PhysicsEngine.cs:219/226/240/247's own <c>platformTopZ = deltaY + 1</c>), so a
    /// LATER candidate in the same call only replaces it when at least as high - the original's own
    /// "highest qualifying support wins" rule, now correctly seeded from the real per-tick reach instead
    /// of unconditionally accepting anything below.
    ///
    /// The STRICT comparator itself must stay strict: a real body box's own <c>Depth</c> is
    /// <c>(SizeZ &lt;&lt; 16) − 1</c> (one 16.16 unit short of a full tile edge, <c>EntityManager.cs:192-199</c>),
    /// which is exactly what lets an entity resting flush on top of another satisfy this test - a
    /// non-strict (<c>&lt;=</c>) comparator would ALSO support an entity merely touching the platform's
    /// top edge, which the original never does (see docs/plan-e4-deplacement-scripte.md's own E4.f
    /// "Pourquoi" note on this exact edge).
    /// </summary>
    internal static bool TryFindSupport(
        AlundraEntityScriptProxy entity, IReadOnlyList<AlundraEntityScriptProxy> collidables,
        int platformTopZSeed, out AlundraEntityScriptProxy? support, out int supportTopZ)
    {
        support = null;
        supportTopZ = 0;
        var platformTopZ = platformTopZSeed;

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

            // PhysicsEngine.cs:205 - BOTH conjuncts (verifier A1): strictly below the entity's own feet,
            // AND at/above this tick's own reach (see this method's own doc).
            if (candidateTop >= moddedPosZ || platformTopZ > candidateTop)
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
            platformTopZ = candidateTop + 1; // PhysicsEngine.cs:219/226/240/247 - "deltaY + 1".
            supportTopZ = platformTopZ;
        }

        return support != null;
    }

    /// <summary>
    /// The absolute ceiling of the binary (<c>0x80036D94</c>, the constant <c>0x7800000</c>, 1920 px), tested before every entity: the top of a box may not go above it.
    /// </summary>
    internal const int AbsoluteCeiling = 0x7800000;

    /// <summary>
    /// E19.h3 (H3-R2), port of the ceiling half of the binary's vertical step (<c>0x80036D94</c>) for a rising entity, in the DLL's convention (<c>ModdedPosZ = PosZ + ModZ</c>,
    /// <c>Depth = SizeZ * 65536 - 1</c>, a floor is its height without the binary's +1). <paramref name="force"/> is the rise of the tick (<c>&gt; 0</c>); the top of the box
    /// is <c>PosZ + ModZ + Depth</c> and its target that plus <paramref name="force"/>. The absolute ceiling is tested first: a target whose top plus one (the conversion
    /// of the binary's convention) goes past <see cref="AbsoluteCeiling"/> puts <c>PosZ</c> at <c>0x77FFFFF - Depth - ModZ - 1</c>. Otherwise, for an eligible subject, any
    /// collidable (not the entity) whose <c>ModdedPosZ</c> is above the current top and at or below the target, and whose XY box overlaps (the semi-open rule of
    /// <see cref="TryFindSupport"/>), lowers the target to its <c>ModdedPosZ - 1</c> (the lowest wins) and <paramref name="newPosZ"/> is the <c>PosZ</c> that puts the top there.
    /// Returns true on a contact (the caller sets <c>CollidedWithEntityZ</c> and, with gravity, clears <c>ForceZ</c>).
    /// </summary>
    internal static bool TryFindCeiling(
        AlundraEntityScriptProxy entity, IReadOnlyList<AlundraEntityScriptProxy> collidables, int force, out int newPosZ)
    {
        var top = entity.PosZ + entity.ModZ + entity.Depth;
        var targetTop = top + force;

        if (targetTop + 1 > AbsoluteCeiling)
        {
            newPosZ = 0x77FFFFF - entity.Depth - entity.ModZ - 1;
            return true;
        }

        newPosZ = 0;
        if (!IsEligibleSubject(entity))
        {
            return false;
        }

        var moddedPosX = entity.PosX + entity.ModX;
        var moddedPosY = entity.PosY + entity.ModY;
        var contact = false;

        for (var i = 0; i < collidables.Count; i++)
        {
            var candidate = collidables[i];
            if (ReferenceEquals(candidate, entity))
            {
                continue;
            }

            var candidateModZ = candidate.PosZ + candidate.ModZ;
            if (!(top < candidateModZ && candidateModZ <= targetTop))
            {
                continue;
            }

            var deltaX = candidate.PosX + candidate.ModX - moddedPosX;
            if (deltaX < 0 ? -deltaX >= candidate.Width + 1 : deltaX >= entity.Width + 1)
            {
                continue;
            }

            var deltaY = candidate.PosY + candidate.ModY - moddedPosY;
            if (deltaY < 0 ? -deltaY >= candidate.Height + 1 : deltaY >= entity.Height + 1)
            {
                continue;
            }

            targetTop = candidateModZ - 1;
            contact = true;
        }

        if (contact)
        {
            newPosZ = targetTop - entity.Depth - entity.ModZ;
        }

        return contact;
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
    /// <see cref="TryFindSupport"/>. E19.d2c2 F1: the Y window of the rider is <c>entity.Height + 1</c> (<c>other.Height + 1</c> on the other side),
    /// as the binary compares it (<c>CheckRidingEntities</c>, <c>0x80036514</c>, <c>0x80036528</c>, <c>0x800365B0</c>); the decompilation read <c>Depth + 1</c> there.
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

            entity.RidingEntity = FindRidingEntity(entity, collidables);
        }
    }

    /// <summary>
    /// E19.d2c2 S4 (binary <c>0x80038998</c> clears the field, <c>0x800364C8</c> sets it again at the head of the entity's tick): the hero's own recalculation, run at
    /// the head of EVERY tick of the hero, never kept from the previous one. <see cref="AlundraEntityScriptProxy.RidingEntity"/> is the logic entity of what carries
    /// <paramref name="entity"/> by the exact rule of <see cref="UpdateRidingEntities"/> (<c>(Flags &amp; 0x4100) == 0x100</c>, the top plus one equal to
    /// <c>ModdedPosZ</c> as it is before this tick's movement, XY overlap), null when nothing does or the flags exclude it. Without a script host: unchanged.
    /// </summary>
    internal static void UpdateRidingEntityOfHero(AlundraEntityScriptProxy entity)
    {
        var host = entity.ScriptHost;
        if (host == null)
        {
            return;
        }

        entity.RidingEntity = (entity.Flags & (EntityFlags.Gravity | EntityFlags.NoRiders)) == EntityFlags.Gravity
            ? FindRidingEntity(entity, host.Collidables)
            : null;
    }

    /// <summary>The search of <see cref="UpdateRidingEntities"/> for one entity: the logic entity of the first collidable (other than itself) it rests exactly on, or null.</summary>
    private static Entity? FindRidingEntity(AlundraEntityScriptProxy entity, IReadOnlyList<AlundraEntityScriptProxy> collidables)
    {
        var moddedPosX = entity.PosX + entity.ModX;
        var moddedPosY = entity.PosY + entity.ModY;
        var moddedPosZ = entity.PosZ + entity.ModZ;
        var entityWidth = entity.Width + 1;
        var entityHeight = entity.Height + 1; // E19.d2c2 F1: the binary's window, Height + 1 (0x80036514, 0x80036528, 0x800365B0).

        for (var j = 0; j < collidables.Count; j++)
        {
            var other = collidables[j];
            if (ReferenceEquals(other, entity))
            {
                continue;
            }

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
            else if (!(deltaY < entityHeight))
            {
                continue;
            }

            return other.LogicContextEntity;
        }

        return null;
    }
}
