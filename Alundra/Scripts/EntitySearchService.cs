#nullable enable
using System.Collections.Generic;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// Port of <c>GameEngine.GetMatchingEntityBySearchType</c> (alundra-datas-analyser
/// AlundraTools/AlundraEngine/GameEngine.cs:1934-2109, GHIDRA @ 0x8003C954) - the entity-search buffer
/// every entity-manipulation opcode (0x2D-adjacent 0x2E, 0x62, 0x63, 0x64, 0x65, 0xAC) fans out through.
/// The original fills a fixed global buffer (<c>g_matchingEntitiesBuffer</c>) and returns a count; this
/// port returns the matches directly as a list, in the same iteration order.
///
/// Operates over <see cref="AlundraEntityScriptProxy"/> instead of the original's <c>Entity</c>, and over
/// a plain list (<see cref="IEntityWorldContext.SpawnedEntities"/>, ultimately <c>AlundraWorldProxy</c>'s
/// own <c>_spawnedEntities</c>) instead of the original's flat, index-addressed <c>g_entitySlots</c>
/// array - both engine-facing seams already exist on the proxy (see the field-by-field doc below), which
/// is why every search type below could be ported without any new proxy field.
///
/// <b>Search-type reference</b> (searchType's bit 0x80 selects the two halves):
/// <list type="bullet">
/// <item><description>0x80 clear: raw entity-record id search (GameEngine.cs:1940-1953) - matches every
/// spawned entity whose <see cref="AlundraEntityScriptProxy.EntityRefId"/> equals <c>searchType</c>,
/// gated on <c>ownerEntity.IsLoadedNormalOrDeactivated</c>. The original also calls
/// <c>CheckEntityRecord(searchType)</c> first purely to validate the id and log "Illegal InitData
/// Number!!" on failure (GameEngine.cs:2113-2123) - its result is discarded and it has no effect on the
/// match count, so it is NOT ported here.</description></item>
/// <item><description>0x80 set: <c>functionId = searchType &amp; 0x7f</c> selects one of 12 canned
/// queries (GameEngine.cs:1956-2104):
/// <list type="number">
/// <item><description>0 - the owner itself.</description></item>
/// <item><description>1 - the player entity (GameEngine.cs:1962-1964:
/// <c>g_matchingEntitiesBuffer[0] = StaticVariables.PlayerEntity</c>). Since E1 the player IS a real
/// spawned entity (<see cref="AlundraWorldProxy.PlayerEntity"/>, also present in
/// <see cref="IEntityWorldContext.SpawnedEntities"/>) - ported by returning
/// <paramref name="playerEntity"/> when non-null (the caller's own <c>IEntityWorldContext.PlayerEntity</c>),
/// no matches when null (no player spawned this session, degraded mode, same shape as every other
/// "nothing to search" case in this class).</description></item>
/// <item><description>2 - every loaded/normal/deactivated entity, PLAYER INCLUDED (GameEngine.cs:1965-1975:
/// loop starts at slot index 0, the player's own slot).</description></item>
/// <item><description>3 - every loaded/normal/deactivated entity EXCEPT the player (GameEngine.cs:1978-1988:
/// loop starts at slot index 1, skipping the player's own slot 0). Ported here by excluding whichever
/// candidate has <see cref="AlundraEntityScriptProxy.IsPlayer"/> set - this runtime has no fixed
/// slot-0 layout to index around, but the player is always exactly one entity, so filtering on its own
/// flag reproduces the same set.</description></item>
/// <item><description>4 - every entity on the ground: <see cref="EntityFlags.Collidable"/> set, the
/// per-animation "no entity collision" bit (<c>EntityAnimFlags.NoEntityCollision</c> = 0x80, ported here
/// as a local constant - see that class's own doc for why it is not pulled in as a whole file) clear, and
/// no <see cref="AlundraEntityScriptProxy.PlatformEntity"/>.</description></item>
/// <item><description>5 - entities the owner is riding on (<c>ownerEntity.RidingEntity</c> matches the
/// candidate's own backing <see cref="AlundraEntityScriptProxy.LogicContextEntity"/>), EXCLUDING the
/// player (E4.f, GameEngine.cs:2010-2091 loops from slot 1, never slot 0).</description></item>
/// <item><description>6 - entities riding on the owner (candidate's <c>RidingEntity</c> matches the
/// owner's <c>LogicContextEntity</c>), EXCLUDING the player.</description></item>
/// <item><description>7 - entities the owner's <c>XCollisionEntity</c> points at, EXCLUDING the
/// player.</description></item>
/// <item><description>8 - entities whose <c>XCollisionEntity</c> points at the owner, EXCLUDING the
/// player.</description></item>
/// <item><description>9 - entities whose <c>ParentEntity</c> is the owner, EXCLUDING the
/// player.</description></item>
/// <item><description>10 - the entity the owner's <c>ParentEntity</c> points at, EXCLUDING the
/// player.</description></item>
/// <item><description>11 - every entity with a non-null <c>PlatformEntity</c> (riding something,
/// regardless of what), EXCLUDING the player.</description></item>
/// </list>
/// Cases 5-10 compare against a proxy's <c>Entity?</c>-typed relationship fields (which point at the
/// original engine <c>Entity</c>), so the comparison is done through each candidate's own
/// <see cref="AlundraEntityScriptProxy.LogicContextEntity"/> - the one field every spawned proxy carries
/// back to its own owning <c>Entity</c> (see <c>AlundraWorldProxy.ApplySpawnInitialization</c>).
/// Any other <c>functionId</c> (12-127) is the original's "Illegal Destination!" breakpoint trap
/// (GameEngine.cs:2101-2103) - logged once here instead, no matches.</description></item>
/// </list>
/// </summary>
public static class EntitySearchService
{
    /// <summary>
    /// <c>EntityAnimFlags.NoEntityCollision</c> (alundra-datas-analyser
    /// AlundraTools/AlundraEngine/Gameplay/EntityAnimFlags.cs:33) - the only bit of that per-animation
    /// flags byte this search needs (case 4). Kept as a local constant instead of porting the whole
    /// <c>EntityAnimFlags</c> file, which nothing else in this runtime reads yet.
    /// </summary>
    private const int NoEntityCollision = 0x80;

    private static readonly HashSet<int> LoggedIllegalFunctionIds = new();

    /// <summary>
    /// Runs one search, in the original's own iteration order. <paramref name="ownerEntity"/> is the
    /// entity whose Load/Tick/... program issued the search (<c>logicEntity</c>/<c>ownerEntity</c> in the
    /// original are the same object by the time any opcode runs in this V1 interpreter - see
    /// <see cref="AlundraEventProgramRunner"/>'s class doc on always-self <c>LogicContextEntity</c>).
    /// <paramref name="playerEntity"/> backs function id 1 ("get player") - passed explicitly (the
    /// caller's own <see cref="IEntityWorldContext.PlayerEntity"/>) rather than found by scanning
    /// <paramref name="spawnedEntities"/> for <see cref="AlundraEntityScriptProxy.IsPlayer"/>, since every
    /// call site already has it at hand and a linear scan for a single, already-known reference would be
    /// wasted work on this hot-ish path. Optional (defaults to null - no match, same as no player spawned)
    /// so existing tests/call sites that never exercise function id 1 do not need to pass it.
    /// </summary>
    public static List<AlundraEntityScriptProxy> GetMatchingEntitiesBySearchType(
        AlundraEntityScriptProxy ownerEntity, int searchType, IReadOnlyList<AlundraEntityScriptProxy> spawnedEntities,
        AlundraEntityScriptProxy? playerEntity = null)
    {
        var matches = new List<AlundraEntityScriptProxy>();

        if ((searchType & 0x80) == 0)
        {
            if (ownerEntity.IsLoadedNormalOrDeactivated)
            {
                foreach (var candidate in spawnedEntities)
                {
                    if (candidate.EntityRefId == searchType)
                    {
                        matches.Add(candidate);
                    }
                }
            }

            return matches;
        }

        switch (searchType & 0x7f)
        {
            case 0: // get owner
                matches.Add(ownerEntity);
                break;

            case 1: // get player - GameEngine.cs:1962-1964
                if (playerEntity != null)
                {
                    matches.Add(playerEntity);
                }

                break;

            case 2: // get all entities (player included) - GameEngine.cs:1965-1975
                foreach (var candidate in spawnedEntities)
                {
                    if (candidate.IsLoadedNormalOrDeactivated)
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 3: // get all entities except player - GameEngine.cs:1978-1988
                foreach (var candidate in spawnedEntities)
                {
                    if (candidate.IsLoadedNormalOrDeactivated && !candidate.IsPlayer)
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 4: // all entities on the ground
                foreach (var candidate in spawnedEntities)
                {
                    if (ownerEntity.IsLoadedNormalOrDeactivated
                        && (candidate.Flags & EntityFlags.Collidable) != 0
                        && (candidate.AnimFlags & NoEntityCollision) == 0
                        && candidate.PlatformEntity == null)
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 5: // entities the owner is riding on (besides the player - GameEngine.cs:2010-2019, loop from slot 1)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(ownerEntity.RidingEntity, candidate.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 6: // entities riding on the owner (besides the player - GameEngine.cs:2023-2032)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(candidate.RidingEntity, ownerEntity.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 7: // entities the owner's XCollisionEntity points at (besides the player - GameEngine.cs:2036-2044)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(ownerEntity.XCollisionEntity, candidate.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 8: // entities whose XCollisionEntity points at the owner (besides the player - GameEngine.cs:2048-2058)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(candidate.XCollisionEntity, ownerEntity.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 9: // entities whose ParentEntity is the owner (besides the player - GameEngine.cs:2062-2072)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(candidate.ParentEntity, ownerEntity.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 10: // the entity the owner's ParentEntity points at (besides the player - GameEngine.cs:2076-2086)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated
                        && ReferenceEquals(ownerEntity.ParentEntity, candidate.LogicContextEntity))
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            case 11: // every entity riding something (besides the player - GameEngine.cs:2090-2100)
                foreach (var candidate in spawnedEntities)
                {
                    if (!candidate.IsPlayer && candidate.IsLoadedNormalOrDeactivated && candidate.PlatformEntity != null)
                    {
                        matches.Add(candidate);
                    }
                }

                break;

            default:
                if (LoggedIllegalFunctionIds.Add(searchType & 0x7f))
                {
                    Logs.WriteWarning(
                        $"EntitySearchService: search type 0x{searchType:x2} has no known function id "
                        + $"(0x{searchType & 0x7f:x2}) - \"Illegal Destination!\" in the original; no matches.");
                }

                break;
        }

        return matches;
    }
}
