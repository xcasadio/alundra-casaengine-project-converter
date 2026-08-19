#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using CasaEngine.Framework.Assets.TileMap;

namespace Alundra.Scripts;

/// <summary>
/// Pure mapping from one record of the "Entities" object layer emitted for a converted map
/// (see <c>AlundraDataExtractor.TiledMapExporter.CreateEntityProperties</c>, the authoritative source
/// of the key list below) onto the fields of <see cref="AlundraEntityScriptProxy"/> that can be derived
/// from the record alone, mirroring how the original <c>SiEntityRecord</c> was consumed by
/// <c>EntityManager.InitializeEntity</c> / <c>InitializeCodePrograms</c> / <c>GameEngine.InitializeContents</c>.
///
/// This fills the entity's own logical position fields (<c>PosX</c>/<c>PosY</c>/<c>PosZ</c>,
/// <c>TileX</c>/<c>TileY</c>/<c>TileZ</c>) from the record alone, with no dependency on world/save
/// state. Converting that logical position into a CasaEngine world-space transform (placing the
/// spawned entity's <c>RootComponent</c>) is a separate, engine-facing concern handled by
/// <see cref="AlundraWorldProxy"/>, not this pure mapper.
///
/// Mapped keys:
/// <list type="bullet">
/// <item><description><c>Index</c> -&gt; <see cref="AlundraEntityScriptProxy.EntityRefId"/>: this is the
/// record's index inside the map's entity array, which is exactly the <c>entityId</c>
/// <c>GameEngine.SpawnEntity</c> looks records up by and stores verbatim as
/// <c>entity.EntityRefId</c> (<c>EntityManager.InitializeEntity</c>) when the record is present.</description></item>
/// <item><description><c>SpriteTableIndex</c> -&gt; <see cref="AlundraEntityScriptProxy.SpriteTableIndex"/>:
/// stored as-is (<c>entity.SpriteTableIndex = spriteTableIndex</c>).</description></item>
/// <item><description><c>EventCodesA_LoadIndex</c> .. <c>EventCodesF_InteractIndex</c> -&gt;
/// <see cref="AlundraEntityScriptProxy.ProgramIndexes"/>[0..5]: <c>EntityManager.InitializeCodePrograms</c>
/// copies the six event-code slots verbatim into <c>ProgramIndexes</c> in A..F order, which is exactly
/// <c>ScriptHelper.ProgramALoad</c>..<c>ProgramFInteract</c> (0..5).</description></item>
/// <item><description><c>_10</c> -&gt; <see cref="AlundraEntityScriptProxy.ContentsGameFlag"/>: reproduces the
/// deterministic part of <c>GameEngine.InitializeContents</c> - the raw save-flag index is masked to zero
/// when <c>(_10 &amp; 0x7fff) &gt; 0x7ff</c>, otherwise stored as-is. The rest of that method (resolving
/// <c>ContentsItemId</c> by consulting live save-game flags and rolling randomised item tables) needs
/// runtime state this pure mapper does not have, so <see cref="AlundraEntityScriptProxy.ContentsItemId"/>
/// is deliberately left unmapped - see below.</description></item>
/// <item><description><c>XPos</c> -&gt; <see cref="AlundraEntityScriptProxy.PosX"/> /
/// <see cref="AlundraEntityScriptProxy.TileX"/>; <c>YPos</c> -&gt;
/// <see cref="AlundraEntityScriptProxy.PosY"/> / <see cref="AlundraEntityScriptProxy.TileY"/>;
/// <c>Height</c> -&gt; <see cref="AlundraEntityScriptProxy.PosZ"/> /
/// <see cref="AlundraEntityScriptProxy.TileZ"/>. Confirmed against the decompilation, not the Tiled
/// exporter's preview math (that one drops precision - see the caveat below):
/// <c>GameEngine.SpawnEntity</c> (AlundraEngine/GameEngine.cs:743-751) passes
/// <c>(entityRecord.XPos * 12 + 12) * 0x10000</c>, <c>(entityRecord.YPos * 8 + 8) * 0x10000</c> and
/// <c>entityRecord.Height &lt;&lt; 0x13</c> as the raw <c>x</c>/<c>y</c>/<c>z</c> spawn position -
/// i.e. <c>XPos</c>/<c>YPos</c>/<c>Height</c> are in half-tile units (12 px / 8 px / 8 px - half of
/// <c>MapTileWidth</c>=24 and <c>MapTileHeight</c>=16), and the "+half-tile" term centres the point in
/// the tile, all packed as 16.16 fixed-point pixels, exactly like <see cref="AlundraEntityScriptProxy.PosX"/>
/// et al. document at their declaration (offset 0x114). <c>PhysicsEngine.cs:1698-1700</c> (normally run
/// every physics tick, <c>entity.TileX = (entity.PosX &gt;&gt; 16) / MapTileWidth</c> etc.) gives the pure
/// formula this mapper reuses once, at spawn time, to seed <c>TileX</c>/<c>TileY</c>/<c>TileZ</c> from the
/// same <c>PosX</c>/<c>PosY</c>/<c>PosZ</c> it just computed.
/// <para>Caveat: <c>EntityManager.InitializeEntity</c> (EntityManager.cs:117-136) does not store this
/// spawn <c>z</c> as-is - it further offsets <c>PosZ</c> by <c>-entity.ModZ + 1</c> (from the sprite's
/// collision box, via <c>SetEntityDimensions</c>) and then clamps it against
/// <c>PhysicsEngine.ComputeEntityGroundHeight</c>'s live terrain probe. Both need runtime state (the
/// resolved sprite, the map's collision cells) this pure mapper does not have, so
/// <see cref="AlundraEntityScriptProxy.PosZ"/> here is the raw elevation input before that ground clamp,
/// not the entity's final resting height - reproducing the clamp is future physics/status-machine
/// work.</para>
/// <para>Also note the Tiled exporter's own <c>DisplayPixelX</c>/<c>DisplayPixelY</c> (see the
/// "deliberately NOT mapped" list below) use a coarser, editor-preview-only conversion - integer-divide
/// <c>XPos</c>/<c>YPos</c>/<c>Height</c> by 2 into whole tiles first (losing the half-tile bit) and skip
/// the "+half-tile" centring term - which is why they do not numerically match <see cref="AlundraEntityScriptProxy.PosX"/>/
/// <see cref="AlundraEntityScriptProxy.PosY"/> here; this mapper follows the decompiled runtime formula,
/// not the exporter's preview math.</para></description></item>
/// </list>
///
/// Deliberately NOT mapped:
/// <list type="bullet">
/// <item><description><c>XMin</c>, <c>YMin</c>, <c>XMax</c>, <c>YMax</c>: activation bounding box, compared
/// against the player's tile position at activation-check time (<c>GameEngine</c> ~0x8003C914 area); there is
/// no corresponding field on the proxy.</description></item>
/// <item><description><c>IsEnabled</c>: gates whether the record produces an entity at all
/// (<c>GameEngine.GetEntityRecord</c> returns <c>null</c> when 0) - a spawn-time gate, not per-instance
/// state, so it belongs to the future spawner, not this record-to-fields mapping.</description></item>
/// <item><description><c>SpriteDirection</c>: packs a spawn-zone gate bit (0x40), a "map sprite" flag
/// (0x80) and a cardinal-direction index (low 2 bits) that spawn logic resolves through
/// <c>g_cardinalDirectionTable</c> to seed <c>TargetDirection</c>/<c>CurrentDirection</c>. Reproducing that
/// requires spawn-time logic and table data this pure mapper does not have; left to the future
/// spawner/world proxy.</description></item>
/// <item><description><c>Contents</c>: raw item-content pool index, consumed only inside
/// <c>GameEngine.InitializeContents</c> together with live save-flag state and RNG to resolve
/// <see cref="AlundraEntityScriptProxy.ContentsItemId"/>; that resolution is stateful and non-deterministic,
/// so it is left to the runtime content-initialization step (out of scope here).</description></item>
/// <item><description><c>DisplayX</c>, <c>DisplayY</c>, <c>DisplayHeight</c>, <c>DisplayPixelX</c>,
/// <c>DisplayPixelY</c>: Tiled-editor display helpers derived from <c>XPos</c>/<c>YPos</c>/<c>Height</c> for
/// map-preview purposes only; the original entity-loading code never consumes them.</description></item>
/// <item><description><c>EntityName</c>: human-readable debug label emitted by the exporter
/// (<c>EntityNames.GetName</c>); not consumed by the original entity-loading logic and has no corresponding
/// proxy field (the record's own object name, e.g. <c>"Entity_0"</c>, is a separate value and is only used
/// here to build error messages).</description></item>
/// <item><description><c>PrefabAssetId</c>: the bank prefab's asset id (see
/// <c>EntityBankPrefabWriter</c>/<c>Entities/{Name}/{Name}.entity</c>), injected by the converter so the
/// spawner (<see cref="AlundraWorldProxy"/>) can clone the right prefab per record. It is consumed at
/// spawn time, before this mapper runs, and has no corresponding proxy field - deliberately not mapped
/// here.</description></item>
/// </list>
/// </summary>
public static class EntityRecordMapper
{
    // StaticVariables.MapTileWidth / MapTileHeight (docs/guidelines-runtime-alundra-casaengine.md
    // section 1) and their halves, as GameEngine.SpawnEntity (GameEngine.cs:743-744) computes them.
    private const int TileWidth = 24;
    private const int TileHeight = 16;
    private const int TileHalfWidth = TileWidth / 2;
    private const int TileHalfHeight = TileHeight / 2;

    // 16.16 fixed-point scale, GameEngine.cs:749-751 ("* 0x10000").
    private const int FixedPointOne = 0x10000;

    /// <summary>
    /// Fills <paramref name="proxy"/> from one entity record's custom properties.
    /// Missing keys are tolerated and leave the corresponding field at its default value. A key that is
    /// present but cannot be parsed as the expected integer type throws, since this data is machine
    /// generated by the converter - a malformed value means a converter bug, not a legitimate absence.
    /// </summary>
    /// <param name="recordName">The record's object name (e.g. "Entity_0"), used only for error messages.</param>
    /// <param name="customProperties">The record's custom_properties, as emitted by the converter.</param>
    /// <param name="proxy">The proxy instance to fill.</param>
    public static void Map(string recordName, IReadOnlyDictionary<string, string> customProperties, AlundraEntityScriptProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(recordName);
        ArgumentNullException.ThrowIfNull(customProperties);
        ArgumentNullException.ThrowIfNull(proxy);

        if (TryGetInt(recordName, customProperties, "Index", out var index))
        {
            proxy.EntityRefId = index;
        }

        if (TryGetInt(recordName, customProperties, "SpriteTableIndex", out var spriteTableIndex))
        {
            proxy.SpriteTableIndex = unchecked((uint)spriteTableIndex);
        }

        if (TryGetInt(recordName, customProperties, "EventCodesA_LoadIndex", out var eventCodesA))
        {
            proxy.ProgramIndexes[0] = eventCodesA;
        }

        if (TryGetInt(recordName, customProperties, "EventCodesB_MapIndex", out var eventCodesB))
        {
            proxy.ProgramIndexes[1] = eventCodesB;
        }

        if (TryGetInt(recordName, customProperties, "EventCodesC_TickIndex", out var eventCodesC))
        {
            proxy.ProgramIndexes[2] = eventCodesC;
        }

        if (TryGetInt(recordName, customProperties, "EventCodesD_TouchIndex", out var eventCodesD))
        {
            proxy.ProgramIndexes[3] = eventCodesD;
        }

        if (TryGetInt(recordName, customProperties, "EventCodesE_DeactivateIndex", out var eventCodesE))
        {
            proxy.ProgramIndexes[4] = eventCodesE;
        }

        if (TryGetInt(recordName, customProperties, "EventCodesF_InteractIndex", out var eventCodesF))
        {
            proxy.ProgramIndexes[5] = eventCodesF;
        }

        if (TryGetInt(recordName, customProperties, "_10", out var rawContentsFlag))
        {
            // Mirrors GameEngine.InitializeContents: the save-flag index is discarded when it falls
            // outside the valid 0..0x7ff range once the sign/high bits are masked off.
            proxy.ContentsGameFlag = (rawContentsFlag & 0x7fff) > 0x7ff ? 0 : rawContentsFlag;
        }

        // GameEngine.SpawnEntity, GameEngine.cs:749: (entityRecord.XPos * tileHalfWidth + tileHalfWidth) * 0x10000.
        if (TryGetInt(recordName, customProperties, "XPos", out var xPos))
        {
            proxy.PosX = (xPos * TileHalfWidth + TileHalfWidth) * FixedPointOne;
            // PhysicsEngine.cs:1698: entity.TileX = (entity.PosX >> 16) / MapTileWidth.
            proxy.TileX = (proxy.PosX >> 16) / TileWidth;
        }

        // GameEngine.SpawnEntity, GameEngine.cs:750: (entityRecord.YPos * tileHalfHeight + tileHalfHeight) * 0x10000.
        if (TryGetInt(recordName, customProperties, "YPos", out var yPos))
        {
            proxy.PosY = (yPos * TileHalfHeight + TileHalfHeight) * FixedPointOne;
            // PhysicsEngine.cs:1699: entity.TileY = (entity.PosY >> 16) / MapTileHeight.
            proxy.TileY = (proxy.PosY >> 16) / TileHeight;
        }

        // GameEngine.SpawnEntity, GameEngine.cs:751: entityRecord.Height << 0x13. This is the raw
        // elevation input SpawnEntity passes down, before EntityManager.InitializeEntity's further
        // -ModZ+1 offset and ground-height clamp (EntityManager.cs:119,130-136) - see the XML doc above.
        if (TryGetInt(recordName, customProperties, "Height", out var height))
        {
            proxy.PosZ = height << 0x13;
            // PhysicsEngine.cs:1700: entity.TileZ = entity.PosZ >> 20.
            proxy.TileZ = proxy.PosZ >> 20;
        }
    }

    /// <summary>
    /// Adapter overload for the engine's runtime object-layer record type, so the future world proxy can
    /// call this mapper directly against what <see cref="TileMapObjectLayerData"/> hands it at runtime.
    /// </summary>
    public static void Map(TileMapObjectData record, AlundraEntityScriptProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(record);

        Map(record.Name ?? string.Empty, record.CustomProperties, proxy);
    }

    private static bool TryGetInt(string recordName, IReadOnlyDictionary<string, string> customProperties, string key, out int value)
    {
        if (!customProperties.TryGetValue(key, out var rawValue))
        {
            value = 0;
            return false;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            throw new FormatException(
                $"Entity record '{recordName}': malformed value '{rawValue}' for property '{key}'.");
        }

        return true;
    }
}
