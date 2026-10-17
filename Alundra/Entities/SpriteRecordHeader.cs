#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Session;
using Alundra.Entities.Spawning;
using Alundra.Scripts;
using Alundra.Hero;
using Alundra.Cells;

namespace Alundra.Entities;

/// <summary>
/// One Data/sprite-records.json entry (see <c>AlundraCasaEngineProjectConverter.Writers.SpriteWriter</c>,
/// which documents the field list and packing rules this DLL still needs): the raw
/// <c>SpriteRecord.Header</c> fields of a bank prefab, keyed by that prefab's own asset id (the same
/// <c>PrefabAssetId</c> a tileMap record carries - see <c>EntityPrefabLinkWriter</c>). Field names/order
/// match the converter's own JSON contract exactly (no naming policy on either side).
/// </summary>
public readonly struct SpriteRecordHeader
{
    public int MoreFlags { get; init; }
    public int CanPickup { get; init; }
    public int FlagsPortraitShadowType { get; init; }
    public int ProgramLoad { get; init; }
    public int ProgramTick { get; init; }
    public int ProgramTouch { get; init; }
    public int ProgramDeactivate { get; init; }
    public int ProgramInteract { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int OffsetZ { get; init; }
    public int SizeX { get; init; }
    public int SizeY { get; init; }
    public int SizeZ { get; init; }
    public int Contents { get; init; }

    /// <summary>
    /// E14.a1b (D-E14-2, D-E14-9, ADR-0061): the byte <c>+0x1E</c> of the sprite header, the effect <c>DestroyEntity</c> creates at the entity when it is called with <c>fx = -1</c> (E0/E1, the VRAM-flag destruction); 0 for no effect.
    /// Exported as <c>BreakEffect</c> by E14.a1a; an export that predates it reads 0.
    /// </summary>
    public int BreakEffect { get; init; }

    /// <summary>
    /// The speaker's dialogue portrait (see <see cref="DialoguePortraitRef"/>), or null when the record has none: the
    /// 370 prefabs whose header bit 0x80 is clear, and any export made before E19.f4a (backward-tolerant like every other field).
    /// </summary>
    public DialoguePortraitRef? DialoguePortrait { get; init; }

    /// <summary>
    /// One entry per (AnimSet index, direction) the bank's converter run actually converted, each
    /// carrying its own per-frame Images.DepthSortValue list (see <c>SpriteWriter</c>'s class doc,
    /// "IdsvAnimDirs" bullet). <see cref="WallPlacementOverlay.ApplyEntitySortKey"/> only applies each
    /// pair's frame-0 value (a documented approximation) - the full per-frame list still travels here
    /// for completeness and future per-frame fidelity. Empty (real <see cref="SpriteRecordCatalog"/>
    /// entries, via <c>SpriteRecordJson.ToHeader</c>) or null (a <see cref="FakeSpriteRecordCatalog"/>
    /// entry built without it) when the source record had no entries, or the file predates this field
    /// (backward-tolerant: unknown/missing JSON fields default here, same as every other header
    /// field) - <see cref="EntitySpawnFactory.BuildIdsvByAnimDirection"/> treats both the same way.
    /// </summary>
    public IReadOnlyList<AnimDirIdsv>? IdsvAnimDirs { get; init; }

    /// <summary>
    /// Per-anim-index lookup of <see cref="AnimSetEntry"/> (walk speed/acceleration/z-force/sfx), built
    /// once here (not per-frame) from <c>Data/sprite-records.json</c>'s <c>"AnimSets"</c> array (converter
    /// E2-A - see <c>SpriteBankReader.ReadAnimSetHeader</c>), keyed by each entry's own <c>Anim</c> index
    /// (not array position - the source only carries one element per AnimSet the bank's converter run
    /// actually converted, so indexes can be sparse). Used by <see cref="AlundraPlayerManager"/>'s
    /// kinematic tick to resolve <c>AnimationSet.Speed</c>/<c>Acceleration</c> for the player's current
    /// <see cref="AlundraEntityScriptProxy.TargetAnimationId"/> (hero anim 1 "Moving" -&gt; Speed 208,
    /// Acceleration 1; anim 54 "LoadingMap" -&gt; Speed 0, Acceleration 64 - verified on the real export).
    /// Null (not empty) when the source record has no <c>"AnimSets"</c> array at all (older export,
    /// backward-tolerant like every other header field) or an empty one; callers treat a miss the same way
    /// as <see cref="IdsvAnimDirs"/>'s own null case ("no data for this entity/anim").
    /// </summary>
    public IReadOnlyDictionary<int, AnimSetEntry>? AnimSets { get; init; }
}
