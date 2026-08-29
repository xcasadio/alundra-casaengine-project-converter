#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Owns the record-to-entity spawn pipeline formerly on <see cref="AlundraWorldProxy"/>: the
/// spawn-time gates (<c>ShouldSpawnRecord</c>), the IDSV/animation-end lookup builders, the
/// animation-finished bridge, the prefab-clone and bare-fallback entity builders, and their shared
/// helpers (<c>ApplyRecord</c>/<c>ApplySpawnInitialization</c>/<c>SetEntityDimensions</c>/
/// <c>BuildEntityName</c>/<c>ResolveLogicalPosition</c>). Pure `static`, stateless, moved from
/// <see cref="AlundraWorldProxy"/> by slice R3 of docs/plan-decoupage-proxies.md - a
/// behaviour-preserving relocation only, see that plan's §3 for the exact delta rule (call
/// qualification and the two documented private-to-internal widenings this move used, both of which
/// stayed on <see cref="AlundraWorldProxy"/>: <c>TryGetRecordInt</c> and
/// <c>ResolveMapGravitySettings</c>). Broken `<see cref>` references left by this move are fixed in
/// slice R5, not here (plan §4 R5) - this class's XML documentation is otherwise the ORIGINAL text,
/// unmodified.
/// </summary>
internal static class AlundraEntitySpawnFactory
{
    /// <summary>Key of the custom property linking an "Entities" record to its bank prefab asset.</summary>
    private const string PrefabAssetIdPropertyKey = "PrefabAssetId";

    /// <summary>
    /// Spawn-time gate over one "Entities" record, ported from the two checks
    /// <c>GameEngine.SpawnEntity</c> (GameEngine.cs:681-758) applies before ever building an entity, for
    /// the specific call the map-load path makes: <c>GameEngine.InitializeEntitySlots</c>
    /// (GameEngine.cs:629-645) spawns every record of the map with <c>SpawnEntity(null, i, 0)</c> - i.e.
    /// <c>notCheckSpawnZone == 0</c>, so both of these checks apply, in this order:
    /// <list type="number">
    /// <item><description><c>IsEnabled == 0</c>: <c>GameEngine.GetEntityRecord</c> (GameEngine.cs:2126-2144)
    /// returns null for such a record, so <c>SpawnEntity</c> never proceeds past its very first line.
    /// Every map-389 record happens to have <c>IsEnabled == 1</c>, so this never fires there, but other
    /// maps do carry disabled records (9741 total, 9631 with <c>IsEnabled != 0</c> - see
    /// <c>WorldWriter</c>'s own count).</description></item>
    /// <item><description><c>(SpriteDirection &amp; 0x40) == 0</c> (GameEngine.cs:715-718): with
    /// <c>notCheckSpawnZone == 0</c> this alone is enough to skip the record. On map 389 this drops the
    /// spawn count from 19 to 14 (5 of its records carry <c>SpriteDirection</c> values 0 or 128, both with
    /// bit 0x40 clear).</description></item>
    /// </list>
    /// Deliberately NOT ported: the player-tile spawn-zone box (<c>XMin</c>/<c>XMax</c>/<c>YMin</c>/
    /// <c>YMax</c> vs <c>StaticVariables.PlayerEntity.TileX</c>/<c>TileY</c>, GameEngine.cs:690-711). The
    /// original resolves it against a player entity <c>GameEngine.ResetEntityState</c> (GameEngine.cs:648-672)
    /// already spawned before this loop runs; this world proxy has no player system yet (see the class
    /// doc), so the check would only ever compare against a zeroed sentinel, which is worse than not
    /// checking it at all - a follow-up task once a player entity exists.
    /// </summary>
    internal static bool ShouldSpawnRecord(TileMapObjectData record, out string skipReason)
        => ShouldSpawnRecord(record, notCheckSpawnZone: false, out skipReason);

    /// <summary>
    /// <paramref name="notCheckSpawnZone"/> overload: mirrors <c>GameEngine.SpawnEntity</c>'s own
    /// <c>notCheckSpawnZone</c> parameter (GameEngine.cs:684-708) - the map-load pass always calls this
    /// with it false (<see cref="ShouldSpawnRecord(TileMapObjectData,out string)"/> above); the
    /// dynamic-spawn opcode 0x2D always calls it true (see <see cref="SpawnEntityByRecordId"/>), which
    /// skips the <c>SpriteDirection</c> 0x40 gate below - <c>IsEnabled</c> always applies either way,
    /// exactly like the original (<c>GameEngine.GetEntityRecord</c> returns null before
    /// <c>notCheckSpawnZone</c> is ever consulted).
    /// </summary>
    internal static bool ShouldSpawnRecord(TileMapObjectData record, bool notCheckSpawnZone, out string skipReason)
    {
        if (TryGetRecordInt(record, "IsEnabled", out var isEnabled) && isEnabled == 0)
        {
            skipReason = "IsEnabled=0";
            return false;
        }

        if (!notCheckSpawnZone
            && TryGetRecordInt(record, "SpriteDirection", out var spriteDirection) && (spriteDirection & 0x40) == 0)
        {
            skipReason = $"SpriteDirection={spriteDirection} has bit 0x40 clear";
            return false;
        }

        skipReason = string.Empty;
        return true;
    }

    /// <summary>
    /// <paramref name="playerTileX"/>/<paramref name="playerTileY"/> overload: adds the player-tile
    /// spawn-zone box (<c>XMin</c>/<c>XMax</c>/<c>YMin</c>/<c>YMax</c> vs the PLAYER's own tile,
    /// GameEngine.cs:690-711) on top of <see cref="ShouldSpawnRecord(TileMapObjectData,bool,out string)"/>'s
    /// two existing checks - the third gate <c>SpawnEntity</c> applies with <c>notCheckSpawnZone == 0</c>,
    /// deliberately NOT ported before E1 for lack of a player entity (see that method's own doc, now
    /// resolved by <see cref="PlayerEntity"/>). Missing <c>XMin</c>/<c>XMax</c>/<c>YMin</c>/<c>YMax</c>
    /// keys leave that side of the box unchecked (same best-effort-tolerant shape as every other
    /// <see cref="TryGetRecordInt"/> read in this class) rather than failing the spawn outright - every
    /// converted record is expected to carry all four, this only guards a malformed/older export.
    /// <paramref name="notCheckSpawnZone"/> skips this box too, exactly like the original (0x2D/0x8B never
    /// check it, see <see cref="SpawnEntityByRecordId"/>).
    /// </summary>
    internal static bool ShouldSpawnRecord(
        TileMapObjectData record, bool notCheckSpawnZone, int playerTileX, int playerTileY, out string skipReason)
    {
        if (!ShouldSpawnRecord(record, notCheckSpawnZone, out skipReason))
        {
            return false;
        }

        if (notCheckSpawnZone)
        {
            return true;
        }

        if (TryGetRecordInt(record, "XMin", out var xMin) && playerTileX < xMin)
        {
            skipReason = $"player tileX={playerTileX} < XMin={xMin}";
            return false;
        }

        if (TryGetRecordInt(record, "XMax", out var xMax) && playerTileX > xMax)
        {
            skipReason = $"player tileX={playerTileX} > XMax={xMax}";
            return false;
        }

        if (TryGetRecordInt(record, "YMin", out var yMin) && playerTileY < yMin)
        {
            skipReason = $"player tileY={playerTileY} < YMin={yMin}";
            return false;
        }

        if (TryGetRecordInt(record, "YMax", out var yMax) && playerTileY > yMax)
        {
            skipReason = $"player tileY={playerTileY} > YMax={yMax}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Stride used to pack (anim, direction) into <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/>'s
    /// single-int key: directions are always 0-3 (<c>AnimationTables.DirectionNames.Length</c>), so 4 is
    /// enough to keep every (anim, direction) pair distinct without a tuple key/comparer.
    /// </summary>
    internal const int IdsvDirectionStride = 4;

    /// <summary>
    /// Builds the per-entity IDSV lookup <see cref="AlundraEntityScriptProxy.IdsvByAnimDirection"/> stashes
    /// at spawn (see <see cref="ApplySpawnInitialization"/>): one frame-0 value per (anim, direction) pair
    /// the catalog entry carries. Returns null when <paramref name="idsvAnimDirs"/> is empty (nothing to
    /// look up - callers treat a null table the same as "0 bias for every (anim, direction)").
    /// </summary>
    internal static Dictionary<int, int>? BuildIdsvByAnimDirection(IReadOnlyList<AnimDirIdsv>? idsvAnimDirs)
    {
        if (idsvAnimDirs == null || idsvAnimDirs.Count == 0)
        {
            return null;
        }

        var table = new Dictionary<int, int>(idsvAnimDirs.Count);
        foreach (var entry in idsvAnimDirs)
        {
            var frame0 = entry.Frames is { Count: > 0 } frames ? frames[0] : 0;
            table[entry.Anim * IdsvDirectionStride + entry.Direction] = frame0;
        }

        return table;
    }

    /// <summary>
    /// Builds the per-entity Hold/Chain lookup <see cref="AlundraEntityScriptProxy.AnimationEndByAnimDirection"/>
    /// stashes at spawn - same key packing as <see cref="BuildIdsvByAnimDirection"/>, so both tables share
    /// the (anim, direction) -&gt; int key without a tuple key/comparer. Only entries whose End is Hold or
    /// Chain are worth keeping (a Loop entry has nothing to bridge - <see cref="OnAnimationFinished"/>
    /// treats a lookup miss as "keep looping" already, so a Loop entry would be a table slot that is never
    /// read for anything different); this also keeps the table small - Loop entries were the majority
    /// (5207 of 9620 across the real export) and would triple its size for no observable effect.
    /// </summary>
    internal static Dictionary<int, AnimationEndInfo>? BuildAnimationEndByAnimDirection(
        IReadOnlyList<AnimDirIdsv>? idsvAnimDirs)
    {
        if (idsvAnimDirs == null || idsvAnimDirs.Count == 0)
        {
            return null;
        }

        Dictionary<int, AnimationEndInfo>? table = null;
        foreach (var entry in idsvAnimDirs)
        {
            if (entry.End == AnimationEndKind.Loop)
            {
                continue;
            }

            table ??= new Dictionary<int, AnimationEndInfo>(idsvAnimDirs.Count);
            table[entry.Anim * IdsvDirectionStride + entry.Direction] =
                new AnimationEndInfo { Kind = entry.End, ChainTargetAnimationId = entry.ChainTo };
        }

        return table;
    }

    /// <summary>
    /// Subscribes <paramref name="entity"/>'s <see cref="AnimatedSpriteComponent"/> (if it has one) to
    /// <see cref="OnAnimationFinished"/> exactly once, at spawn/adoption (see
    /// <see cref="ApplySpawnInitialization"/>/<see cref="AdoptPlayerPawn"/>) - bridging the engine's
    /// Once-finished event back to the original's Hold/Chain semantics (EntityManager.cs:257-281, see
    /// <see cref="OnAnimationFinished"/>'s own doc). The cached static delegate
    /// (<see cref="AnimationFinishedHandler"/>) means subscribing allocates nothing beyond the one-time
    /// delegate instance shared by every entity; the handler itself resolves the proxy from
    /// <c>sender</c>/<c>Owner.GameplayProxy</c> rather than capturing anything per-entity.
    /// </summary>
    /// <summary>
    /// No unsubscribe on destroy: <see cref="DestroyEntity(AlundraEntityScriptProxy)"/> only ever sets
    /// <see cref="EntityStatus.FlagToDestroy"/> (V1 scope is invisibility, not removal/slot recycling -
    /// see that method's own doc), never disposes the entity or its components, so the subscription
    /// this method makes lives exactly as long as the entity object itself and needs no explicit
    /// teardown. A FlagToDestroy entity's <see cref="OnAnimationFinished"/> calls (should its sampler
    /// still run while invisible) are themselves harmless: every per-frame pass that reads
    /// <see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/>/<c>TargetAnimationId</c>
    /// (<see cref="SyncAnimation"/>, <see cref="RunPendingEventTriggers"/>) already skips FlagToDestroy
    /// entities.
    /// </summary>
    internal static void SubscribeAnimationEndBridge(Entity entity)
    {
        var animatedSprite = entity.GetComponent<AnimatedSpriteComponent>();
        if (animatedSprite == null)
        {
            return;
        }

        animatedSprite.AnimationFinished += AnimationFinishedHandler;
    }

    private static readonly EventHandler<Animation2d> AnimationFinishedHandler = OnAnimationFinished;

    /// <summary>
    /// Bridge from <see cref="AnimatedSpriteComponent.AnimationFinished"/> (fired once, from inside
    /// <c>Entity.Update</c>'s component pass, strictly BEFORE that same entity's own
    /// <see cref="AlundraEntityScriptProxy.Update"/> runs - see <c>Animation2dCompositionSampler.Update</c>'s
    /// clamp-at-DurationSeconds/IsFinished and <c>AnimatedSpriteComponent.Update</c>) back to the
    /// original's Hold/Chain semantics (EntityManager.cs:257-281):
    /// <list type="bullet">
    /// <item><description>Hold: sets <see cref="AlundraEntityScriptProxy.ForceResetAnimationFlag"/> = 1
    /// (EntityManager.cs:273-275) - already read by <see cref="AlundraEntityScriptProxy.Update"/>'s pick
    /// phase for <c>DeactivateOnAnimationEnd</c>. The engine's own Once clamp already holds the last
    /// displayed frame's pose (the writer's terminal keyframe repeats it, see <c>SpriteWriter</c>'s
    /// class doc) - nothing else to do.</description></item>
    /// <item><description>Chain: sets <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> to the
    /// chain target (EntityManager.cs:277-279). <see cref="AlundraEntityScriptProxy.Update"/> calls
    /// <see cref="SyncAnimation"/> every frame regardless, so the very next call - later this SAME
    /// frame, since the component pass already ran - notices <c>TargetAnimationId</c> changed and
    /// switches animation: the same-tick effect the original gets from its own recursive
    /// <c>UpdateAnimation</c> call (EntityManager.cs:280), without this bridge needing to call
    /// <see cref="SyncAnimation"/> itself.</description></item>
    /// </list>
    /// A lookup miss (no entry for the just-finished (anim, direction), including every Loop entry -
    /// see <see cref="BuildAnimationEndByAnimDirection"/>) is a no-op: the engine already looped or
    /// nothing was ever wired up for this entity (degraded catalog). The original's own
    /// <c>AnimCompleteCounter++</c> per Loop cycle (EntityManager.cs:263) is NOT bridged - nothing in
    /// the ported V1 gameplay reads it yet, and <see cref="AnimatedSpriteComponent.AnimationFinished"/>
    /// does not even fire for a Loop animation (<c>Animation2dCompositionSampler</c> wraps instead of
    /// finishing) so there would be no signal to bridge it from.
    /// </summary>
    internal static void OnAnimationFinished(object? sender, Animation2d finishedAnimation)
    {
        if (sender is not AnimatedSpriteComponent component
            || component.Owner?.GameplayProxy is not AlundraEntityScriptProxy proxy
            || proxy.AnimationEndByAnimDirection == null)
        {
            return;
        }

        var key = (int)proxy.CurrentAnimationId * IdsvDirectionStride + proxy.AnimationDirection;
        if (!proxy.AnimationEndByAnimDirection.TryGetValue(key, out var end))
        {
            return;
        }

        if (end.Kind == AnimationEndKind.Hold)
        {
            proxy.ForceResetAnimationFlag = 1;
        }
        else if (end.Kind == AnimationEndKind.Chain)
        {
            proxy.TargetAnimationId = (uint)end.ChainTargetAnimationId;

            // A chain must restart its target even when that target is the animation that just ended -
            // the original's own way of spelling "loop" for the hero's walk (ChainTo = 1 on anim 1, all
            // four directions). SyncAnimation alone would not: TryResolveAnimationTarget only fires on a
            // CHANGE of animation id or direction, and a self-chain changes neither, so the sampler stayed
            // parked on its terminal pose - the frozen walk the user reported on 2026-08-26. See
            // AlundraEntityScriptProxy.PendingChainRestartFlag's own doc.
            proxy.PendingChainRestartFlag = 1;
        }
    }

    /// <summary>
    /// Builds one game entity from an "Entities" object-layer record.
    ///
    /// When <paramref name="record"/> carries a valid <c>PrefabAssetId</c> custom property and
    /// <paramref name="prefabLoader"/> successfully resolves it, the returned entity is a clone of that
    /// bank prefab (see <see cref="CreateEntityFromPrefab"/>) - so it carries the bank's
    /// sprite/collision components. Otherwise (missing/malformed link, no loader, loader throws or
    /// returns null) a single warning is logged and a bare entity is built instead (see
    /// <see cref="CreateBareEntityFromRecord"/>). Either way the result carries an
    /// <see cref="AlundraEntityScriptProxy"/> filled by <see cref="EntityRecordMapper"/>, with
    /// <see cref="EntityStatus.Loaded"/>. Does not add the entity to any world; the caller does that.
    ///
    /// <paramref name="prefabLoader"/> is a seam for unit tests: the live path
    /// (<see cref="InitializeWithWorld"/>) wires it over <c>World.Game.AssetContentManager.Load&lt;Entity&gt;</c>,
    /// which the headless unit test process cannot exercise (Alundra.csproj marks its own
    /// MonoGame.Framework.DesktopGL reference PrivateAssets="All" for game-folder deployment, so it never
    /// flows into Alundra.Tests's deps.json); tests inject a fake in-memory prefab instead.
    /// </summary>
    internal static Entity CreateEntityFromRecord(
        TileMapObjectData record, Func<Guid, Entity?>? prefabLoader, ISpriteRecordCatalog? spriteRecordCatalog = null,
        Entity? parentEntity = null, TileMapData? tileMapData = null)
    {
        if (TryGetPrefabAssetId(record, out var prefabAssetId))
        {
            Entity? prefab = null;
            string? failureReason = null;

            if (prefabLoader == null)
            {
                failureReason = "no prefab loader available";
            }
            else
            {
                try
                {
                    prefab = prefabLoader(prefabAssetId);
                    if (prefab == null)
                    {
                        failureReason = "prefab loader returned null";
                    }
                }
                catch (Exception ex)
                {
                    failureReason = ex.Message;
                }
            }

            if (prefab != null)
            {
                return CreateEntityFromPrefab(record, prefab, spriteRecordCatalog, parentEntity, tileMapData);
            }

            Logs.WriteWarning(
                $"AlundraWorldProxy: record '{record.Name}': could not clone prefab '{prefabAssetId}' "
                + $"({failureReason}); falling back to a bare entity.");
        }
        else
        {
            Logs.WriteWarning(
                $"AlundraWorldProxy: record '{record.Name}' has no valid '{PrefabAssetIdPropertyKey}' link; "
                + "falling back to a bare entity.");
        }

        return CreateBareEntityFromRecord(record, spriteRecordCatalog, parentEntity, tileMapData);
    }

    /// <summary>
    /// Parses the record's <c>PrefabAssetId</c> custom property (see <c>AlundraDataExtractor</c>'s
    /// tilemap exporter) into the bank prefab's asset id. Returns false when the key is missing or its
    /// value is not a valid <see cref="Guid"/>.
    /// </summary>
    internal static bool TryGetPrefabAssetId(TileMapObjectData record, out Guid prefabAssetId)
    {
        if (record.CustomProperties.TryGetValue(PrefabAssetIdPropertyKey, out var rawValue)
            && Guid.TryParse(rawValue, out prefabAssetId))
        {
            return true;
        }

        prefabAssetId = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Clones <paramref name="prefab"/> (a bank prefab loaded from <c>Entities/{Name}/{Name}.entity</c>,
    /// per <c>EntityBankPrefabWriter</c>) into a fresh, independent entity carrying that bank's
    /// sprite/collision components, renamed for this record and with its
    /// <see cref="AlundraEntityScriptProxy"/> filled from <paramref name="record"/>.
    /// </summary>
    internal static Entity CreateEntityFromPrefab(
        TileMapObjectData record, Entity prefab, ISpriteRecordCatalog? spriteRecordCatalog = null,
        Entity? parentEntity = null, TileMapData? tileMapData = null)
    {
        var entity = prefab.Clone();
        entity.Name = BuildEntityName(record);

        if (string.IsNullOrEmpty(entity.GameplayProxyClassName))
        {
            //The prefab is expected to carry AlundraEntityScriptProxy as its script class; fall back
            //explicitly rather than spawning an entity with no gameplay proxy at all.
            entity.GameplayProxyClassName = nameof(AlundraEntityScriptProxy);
        }

        //Creates/keeps the GameplayProxy (via ElementFactory, from GameplayProxyClassName) and calls its
        //Initialize(entity); InitializeWithWorld() runs later, when the engine integrates the entity.
        entity.Initialize();

        if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
        {
            ApplyRecord(record, proxy);
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog, parentEntity, tileMapData);

            // The prefab's root is the inert TransformComponent (SpriteWriter.WriteEntityPrefab, E3.a);
            // place it in the CasaEngine LOGICAL frame from the logical position
            // EntityRecordMapper/ApplySpawnInitialization just filled (PosZ already carries the -ModZ+1
            // header adjustment when a header was found).
            // Defensive null-check only: a bank prefab is expected to always carry a root component.
            if (entity.RootComponent != null)
            {
                entity.RootComponent.LocalTransform.Position = ResolveLogicalPosition(proxy.PosX, proxy.PosY, proxy.PosZ);

                // Resolve and cache the root's RenderProjectionComponent once, then re-project
                // immediately so the very first draw already shows the projected render pose rather
                // than whatever default position the prefab's projection carried before this spawn
                // wrote the root (see AlundraEntityScriptProxy.RenderProjection's own doc).
                proxy.RenderProjection = entity.GetComponent<RenderProjectionComponent>();
                proxy.RenderProjection?.UpdateProjection();
            }
        }

        return entity;
    }

    internal static Vector3 ResolveLogicalPosition(int posX, int posY, int posZ)
    {
        var pixelX = posX >> 16;
        var pixelY = posY >> 16;
        var elevationPixels = posZ >> 16;

        return new Vector3(pixelX, pixelY, elevationPixels);
    }

    /// <summary>
    /// Builds one bare game entity from an "Entities" object-layer record: a deterministically named
    /// entity carrying an <see cref="AlundraEntityScriptProxy"/> filled by <see cref="EntityRecordMapper"/>,
    /// with <see cref="EntityStatus.Loaded"/>. Used as the fallback when the record has no usable prefab
    /// link (see <see cref="CreateEntityFromRecord"/>). Unlike <see cref="CreateEntityFromPrefab"/> this
    /// never sets a world transform: a bare entity has no <c>RootComponent</c> to place (it carries no
    /// components at all), only the proxy's logical position fields - the existing "falling back to a
    /// bare entity" warning already covers this case, so it needs no separate warning of its own here.
    /// Does not add the entity to any world; the caller does that.
    /// </summary>
    internal static Entity CreateBareEntityFromRecord(
        TileMapObjectData record, ISpriteRecordCatalog? spriteRecordCatalog = null, Entity? parentEntity = null,
        TileMapData? tileMapData = null)
    {
        var entity = new Entity
        {
            Name = BuildEntityName(record),
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };

        //Creates the GameplayProxy (via ElementFactory, from GameplayProxyClassName) and calls its
        //Initialize(entity); InitializeWithWorld() runs later, when the engine integrates the entity.
        entity.Initialize();

        if (entity.GameplayProxy is AlundraEntityScriptProxy proxy)
        {
            ApplyRecord(record, proxy);
            ApplySpawnInitialization(record, entity, proxy, spriteRecordCatalog, parentEntity, tileMapData);
        }

        return entity;
    }

    /// <summary>Maps <paramref name="record"/> onto <paramref name="proxy"/> and marks it loaded.</summary>
    internal static void ApplyRecord(TileMapObjectData record, AlundraEntityScriptProxy proxy)
    {
        EntityRecordMapper.Map(record, proxy);
        proxy.Status = EntityStatus.Loaded;

        // Required plumbing for decision D2/D3, not part of the original struct's own zero-init (the
        // original never explicitly sets this either - see EntityManager.InitializeEntity): a freshly
        // spawned proxy's EventTrigger otherwise defaults to the C# int default 0, which IS
        // ScriptHelper.ProgramALoad, not ScriptHelper.ProgramUnknown(-1) - so a proxy created THIS FRAME
        // by a running script (0x2D/0x8B, via SpawnEntityByRecordId) would look, to
        // AlundraWorldProxy.RunPendingEventTriggers (called later this SAME frame, after any such
        // spawn), exactly like an entity whose pick phase already ran and chose slot A - triggering its
        // Load program immediately, without ever going through PickEventTrigger's own Loaded -> Normal
        // transition. Explicitly seeding ProgramUnknown here preserves the documented "next frame" spawn
        // visibility (docs/intro-roadmap.md §0 deviation B) under the new per-entity architecture.
        proxy.EventTrigger = ScriptHelper.ProgramUnknown;
    }

    /// <summary>
    /// Faithful port of the rest of <c>EntityManager.InitializeEntity</c> @ 0x80039D04 that
    /// <see cref="EntityRecordMapper"/> could not do on its own (no header, no owning entity) - run after
    /// <see cref="ApplyRecord"/> for both the prefab and the bare creation path (see
    /// <see cref="CreateEntityFromPrefab"/>/<see cref="CreateBareEntityFromRecord"/>).
    /// <list type="bullet">
    /// <item><description><c>entity.LogicContextEntity = entity</c> (EntityManager.cs:147,
    /// <c>InitializeCodePrograms</c> @ 0x8004201C) is unconditional in the original - it needs nothing but
    /// the entity that was just created, so it always runs here too, header or not.</description></item>
    /// <item><description>Everything else below it (<c>Flags</c>, <c>SpriteProgramIndexes</c>,
    /// <c>SetEntityDimensions</c>, the <c>PosZ</c> header adjustment, <c>ModdedPosX/Y/Z</c>, and the
    /// spawn-time animation/direction fields) needs the bank's <c>SpriteRecord.Header</c>
    /// (<see cref="SpriteRecordHeader"/>), which the original always has by construction - <c>SpawnEntity</c>
    /// (GameEngine.cs:721-726) returns null before ever calling <c>InitializeEntity</c> when the sprite
    /// record fails to resolve. <paramref name="spriteRecordCatalog"/> can fail to resolve one here (file
    /// missing, or this record's prefab link missing/invalid) in ways the original never could; when that
    /// happens this entire block is skipped and the entity keeps the plain <see cref="EntityRecordMapper"/>
    /// output - documented degraded mode, see <see cref="SpriteRecordCatalog"/>'s class doc.</description></item>
    /// </list>
    /// </summary>
    internal static void ApplySpawnInitialization(
        TileMapObjectData record, Entity entity, AlundraEntityScriptProxy proxy, ISpriteRecordCatalog? spriteRecordCatalog,
        Entity? parentEntity = null, TileMapData? tileMapData = null)
    {
        proxy.LogicContextEntity = entity;

        // EntityManager.cs:52: entity.ParentEntity = parentEntity is unconditional, independent of
        // whether the sprite record header resolves below - null for every map-load spawn (the original
        // always passes null there too, GameEngine.cs:629-645 InitializeEntitySlots), non-null only for
        // the dynamic-spawn opcode 0x2D (AlundraWorldProxy.SpawnEntityByRecordId).
        proxy.ParentEntity = parentEntity;

        if (spriteRecordCatalog == null
            || !TryGetPrefabAssetId(record, out var prefabAssetId)
            || !spriteRecordCatalog.TryGet(prefabAssetId, out var header))
        {
            return;
        }

        // EntityManager.cs:92-93 (Entity.Flags packing documented by EntityFlags).
        proxy.Flags = (uint)(header.MoreFlags | (header.CanPickup << 8) | (header.FlagsPortraitShadowType << 16));

        // E4.b ("Spawn" item, docs/plan-e4-deplacement-scripte.md): cache Controller/RenderProjection the
        // same way AdoptPlayerPawn does for the hero (E3.d) - every prefab with a positive body box now
        // carries a CharacterControllerComponent (E4.a), so a record-spawned NPC needs the same caching
        // this proxy's own per-frame passes (Update's root pull, MoveControllerAndPullPosition,
        // PushLogicalPositionToRoot) already assume. ??= rather than an unconditional overwrite: harmless
        // either way here (both start null on a freshly Initialize()'d proxy), but keeps this call site
        // consistent with "cache once" even if a future caller pre-seeds either field.
        proxy.Controller ??= entity.GetComponent<CharacterControllerComponent>();
        proxy.RenderProjection ??= entity.GetComponent<RenderProjectionComponent>();

        // E5.b (docs/plan-e5-camera.md): a controller-driven entity's logical pose keeps a float
        // remainder every frame (CharacterControllerComponent.Move has no rounding), so without this
        // the sprite's texel grid drifts off the screen's at non-unit zoom and blurs while moving. Set
        // unconditionally (not guarded by the ??= above): harmless to repeat on the same instance, and
        // keeps this true even if a future caller pre-seeds RenderProjection with the flag left off.
        if (proxy.RenderProjection != null)
        {
            proxy.RenderProjection.SnapToPixel = true;
        }

        // Overrides the converter-exported Gravity/MaxFallSpeed/WalkabilityMask - the only three
        // CharacterControllerSettings the converter cannot bake in (E4.a leaves them 0, see that
        // tranche's own "Contrôleurs PNJ" note), since they depend on THIS map's own properties and THIS
        // entity's own Flags, not on the prefab alone - same reasoning as AdoptPlayerPawn's own override
        // block (E3.d), reusing its exact formula via ResolveMapGravitySettings rather than duplicating
        // it. Deliberately AFTER the Flags assignment above: both WalkabilityMaskFor(proxy.Flags) and
        // ApplyGravitySettingsToController's own Gravity-bit gate need the real header flags. A no-op
        // without a controller (11 sprite-only prefabs, E4.a) or without this world's tilemap
        // (tileMapData null - a caller that never resolved one, e.g. an older test fixture).
        if (proxy.Controller != null && tileMapData != null)
        {
            (proxy.MapGravity, proxy.MapMaxFallSpeed, proxy.MapGravityRaw, proxy.MapZViscosityRaw) = ResolveMapGravitySettings(tileMapData);
            proxy.ApplyGravitySettingsToController();
            proxy.Controller.Settings.WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(proxy.Flags);

            // E4.g (docs/plan-e4-deplacement-scripte.md): a scripted NPC's vertical is entirely owned
            // by AlundraEntityScriptProxy.EvaluateEntitySupport's own per-tick ForceZ, declared through
            // Controller.SetExternalVerticalDisplacement - never the engine's own gravity/vertical
            // integration (already zeroed above, belt-and-suspenders). The hero is NEVER spawned through
            // this method (see AdoptPlayerPawn instead) so this flag never touches the hero's own
            // engine-driven vertical.
            proxy.Controller.IsVerticalOwnedExternally = true;
        }

        // EntityManager.cs:95-100.
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramALoad] = header.ProgramLoad;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramBMap] = 0;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramCTick] = header.ProgramTick;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramDTouch] = header.ProgramTouch;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = header.ProgramDeactivate;
        proxy.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = header.ProgramInteract;

        SetEntityDimensions(proxy, header.OffsetX, header.OffsetY, header.OffsetZ, header.SizeX, header.SizeY, header.SizeZ);

        // Resolve this entity's IDSV table once, from the catalog entry already fetched above, and
        // stash it on the proxy - see AlundraEntityScriptProxy.IdsvByAnimDirection's doc comment and
        // WallPlacementOverlay.ApplyEntitySortKey's frame-0-only deviation note. Only frame 0 of each
        // (anim, direction) pair is kept; the per-frame lists Data/sprite-records.json carries are not
        // needed on this hot-path table.
        proxy.IdsvByAnimDirection = BuildIdsvByAnimDirection(header.IdsvAnimDirs);
        proxy.AnimationEndByAnimDirection = BuildAnimationEndByAnimDirection(header.IdsvAnimDirs);

        // E4.b fix (verifier F1, P2): same source as AdoptPlayerPawn's own hero-only assignment
        // (this file, AdoptPlayerPawn - "proxy.AnimSetsByAnim = header.AnimSets") - without this, every
        // record-spawned NPC's AnimSetsByAnim stays null, so AlundraScriptedMotion.RunOneKinematicTick's
        // own hasAnimSet lookup always misses (Speed/Acceleration both resolve to 0) and the entity never
        // moves at runtime, regardless of TargetAnimationId/TargetDirection. Null (not empty) when the
        // header carries no AnimSets at all (older export/degraded catalog) - same "0 speed for every
        // anim" fallback AlundraScriptedMotion already treats a null table as.
        proxy.AnimSetsByAnim = header.AnimSets;

        SubscribeAnimationEndBridge(entity);

        // EntityManager.cs:119: the mapper seeded PosZ with the raw pre-clamp elevation
        // (EntityRecordMapper's documented caveat); this is the -ModZ+1 offset InitializeEntity applies
        // once the header (hence ModZ) is known. The ground-height clamp (EntityManager.cs:130-136) stays
        // out - it needs the map's collision cells, a later chantier.
        proxy.PosZ = proxy.PosZ - proxy.ModZ + 1;

        // EntityManager.cs:123-125.
        proxy.ModdedPosX = proxy.PosX + proxy.ModX;
        proxy.ModdedPosY = proxy.PosY + proxy.ModY;
        proxy.ModdedPosZ = proxy.PosZ + proxy.ModZ;

        // GameEngine.cs:752-753: SpawnEntity always passes animationId=0 and reads the facing off the
        // record's own SpriteDirection (not the header) - a missing/malformed key defaults to 0, same as
        // a record whose SpriteDirection happens to be 0.
        TryGetRecordInt(record, "SpriteDirection", out var spriteDirection);
        const uint animationId = 0;
        var direction = AnimationTables.CardinalDirectionTable[spriteDirection & 0x3];

        // EntityManager.cs:85-88.
        proxy.CurrentAnimationId = ~animationId;
        proxy.CurrentDirection = ~direction;
        proxy.TargetAnimationId = animationId;
        proxy.TargetDirection = direction;
    }

    /// <summary>
    /// Port of <c>EntityManager.SetEntityDimensions</c> @ 0x80039C40: derives the entity's collision/mod
    /// box from its bank header's raw offset/size fields (already 16.16-fixed-point-free integers; the
    /// original shifts them into 16.16 itself with <c>&lt;&lt; 16</c>). Constants
    /// <c>0x4e00000</c>/<c>0x3c00000</c>/<c>0x7800000</c> are ported verbatim, unexplained in the original
    /// beyond their use as screen-clip bounds.
    /// </summary>
    internal static void SetEntityDimensions(
        AlundraEntityScriptProxy proxy, int offsetX, int offsetY, int offsetZ, int sizeX, int sizeY, int sizeZ)
    {
        proxy.NegModX = -(offsetX << 16);
        proxy.NegModY = -(offsetY << 16);
        proxy.ModX = offsetX << 16;
        proxy.ModY = offsetY << 16;
        proxy.ModZ = offsetZ << 16;
        proxy.ScreenClipX = 0x4e00000 - ((offsetX + sizeX) << 16);
        proxy.ScreenClipY = 0x3c00000 - ((offsetY + sizeY) << 16);
        proxy.ScreenClipZ = 0x7800000 - ((offsetZ + sizeZ) << 16);

        proxy.Width = sizeX == 0 ? 0 : (sizeX << 16) - 1;
        proxy.Height = sizeY == 0 ? 0 : (sizeY << 16) - 1;
        proxy.Depth = sizeZ == 0 ? 0 : (sizeZ << 16) - 1;
    }

    internal static string BuildEntityName(TileMapObjectData record)
    {
        var baseName = string.IsNullOrEmpty(record.Name) ? "Entity" : record.Name;
        return record.CustomProperties.TryGetValue("EntityName", out var entityName) && !string.IsNullOrEmpty(entityName)
            ? $"{baseName} ({entityName})"
            : baseName;
    }

    /// <summary>Best-effort integer read of one custom property; missing or malformed leaves 0/false -
    /// mirroring how the converter always emits these two keys, so a missing key is not expected but
    /// should not itself block a spawn the way a malformed <see cref="EntityRecordMapper"/> key does.</summary>
    internal static bool TryGetRecordInt(TileMapObjectData record, string key, out int value)
    {
        if (record.CustomProperties.TryGetValue(key, out var raw) && int.TryParse(raw, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
    internal static (float Gravity, float MaxFallSpeed, int GravityRaw, int ZViscosityRaw) ResolveMapGravitySettings(TileMapData tileMapData)
    {
        tileMapData.CustomProperties.TryGetValue("Gravity", out var gravityRaw);
        int.TryParse(gravityRaw, out var mapGravity);
        tileMapData.CustomProperties.TryGetValue("ZViscosity", out var zViscosityRaw);
        int.TryParse(zViscosityRaw, out var mapZViscosity);

        return (mapGravity * 256f / 65536f * 2500f, mapZViscosity * 256f / 65536f * 50f, mapGravity, mapZViscosity);
    }
}
