#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Scripts;

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
    /// One entry per (AnimSet index, direction) the bank's converter run actually converted, each
    /// carrying its own per-frame Images.DepthSortValue list (see <c>SpriteWriter</c>'s class doc,
    /// "IdsvAnimDirs" bullet). <see cref="WallPlacementOverlay.ApplyEntitySortKey"/> only applies each
    /// pair's frame-0 value (a documented approximation) - the full per-frame list still travels here
    /// for completeness and future per-frame fidelity. Empty (real <see cref="SpriteRecordCatalog"/>
    /// entries, via <c>SpriteRecordJson.ToHeader</c>) or null (a <see cref="FakeSpriteRecordCatalog"/>
    /// entry built without it) when the source record had no entries, or the file predates this field
    /// (backward-tolerant: unknown/missing JSON fields default here, same as every other header
    /// field) - <see cref="AlundraWorldProxy.BuildIdsvByAnimDirection"/> treats both the same way.
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

/// <summary>
/// One <see cref="SpriteRecordHeader.AnimSets"/> entry - a bank's <c>SiAnimationSet</c> header fields
/// (converter E2-A, <c>SpriteBankReader.ReadAnimSetHeader</c>), read-only mirror of
/// <c>Data/sprite-records.json</c>'s per-AnimSet JSON object.
/// </summary>
public readonly struct AnimSetEntry
{
    /// <summary>The AnimSet index this entry describes (matches <see cref="AlundraEntityScriptProxy.TargetAnimationId"/>'s
    /// domain) - also the dictionary key in <see cref="SpriteRecordHeader.AnimSets"/>, duplicated here so
    /// a caller iterating the dictionary's values still has it.</summary>
    public int Anim { get; init; }

    /// <summary>Walk speed, pre-multiplied by <see cref="AnimationTables.OffsetXList"/>/<see cref="AnimationTables.OffsetYList"/>
    /// in <see cref="AlundraPlayerManager"/>'s kinematic tick (<c>PhysicsEngine.UpdateEntityPhysics</c>,
    /// PhysicsEngine.cs:1593-1594).</summary>
    public int Speed { get; init; }

    /// <summary>Right-shift amount applied to the force step (<c>&amp; 0xf</c> at the call site,
    /// PhysicsEngine.cs:1591/1596-1597) - larger values mean slower acceleration toward the target
    /// speed.</summary>
    public int Acceleration { get; init; }

    /// <summary>Not consumed by V1's kinematic tick (no Z-axis movement yet) - carried for a future
    /// jump/fall chantier.</summary>
    public int IsZForceApplied { get; init; }

    /// <summary>Sound-effect id associated with this AnimSet (e.g. footstep sfx) - not consumed yet (no
    /// audio system, E11's own scope).</summary>
    public int Sfx { get; init; }

    /// <summary>Raw AnimSet flags byte, meaning not yet reverse-engineered beyond <see cref="IsZForceApplied"/>.</summary>
    public int Flags { get; init; }

    /// <summary>Unidentified trailing field of the source AnimSet header - carried for completeness.</summary>
    public int Unknown { get; init; }
}

/// <summary>
/// How an (anim, direction)'s trailing control frame ends playback - mirrors the converter's own
/// <c>AnimationEndClassifier.AnimationEndKind</c> (alundra-casaengine-project-converter/Readers/
/// AnimationEndClassifier.cs), duplicated here rather than shared since this DLL does not reference
/// the converter project. See <see cref="AlundraWorldProxy.SubscribeAnimationEndBridge"/> for how
/// this drives the engine's Once-finished event back to the original's Hold/Chain semantics
/// (EntityManager.cs:257-281).
/// </summary>
public enum AnimationEndKind
{
    Loop,
    Hold,
    Chain,
}

/// <summary>
/// One <see cref="AlundraEntityScriptProxy.AnimationEndByAnimDirection"/> table value - see
/// <see cref="AlundraWorldProxy.BuildAnimationEndByAnimDirection"/> and
/// <see cref="AlundraWorldProxy.OnAnimationFinished"/>.
/// </summary>
public readonly struct AnimationEndInfo
{
    public AnimationEndKind Kind { get; init; }
    public int ChainTargetAnimationId { get; init; }
}

/// <summary>
/// One <see cref="SpriteRecordHeader.IdsvAnimDirs"/> entry - see its doc comment. <see cref="Frames"/>
/// holds the per-frame Images.DepthSortValue list of every displayed frame (terminator frames
/// excluded), in animation-frame order. <see cref="End"/>/<see cref="ChainTo"/> are the same
/// classification <c>SpriteWriter</c> already used to pick this animation's own AnimationType
/// (Loop stays engine Loop; Hold/Chain became engine Once) - see
/// <see cref="AlundraWorldProxy.BuildAnimationEndByAnimDirection"/>.
/// </summary>
public readonly struct AnimDirIdsv
{
    public int Anim { get; init; }
    public int Direction { get; init; }
    public IReadOnlyList<int>? Frames { get; init; }
    public AnimationEndKind End { get; init; }
    public int ChainTo { get; init; }
}

/// <summary>
/// Seam over <see cref="SpriteRecordCatalog"/> so spawn-time code (<see cref="AlundraWorldProxy"/>) and
/// its tests can swap the real file-backed catalog for an in-memory fake, the same way
/// <c>AlundraWorldProxy.CreateEntityFromRecord</c>'s <c>prefabLoader</c> parameter is swapped in tests.
/// </summary>
public interface ISpriteRecordCatalog
{
    /// <summary>
    /// Looks up the raw sprite-record header of the bank prefab <paramref name="prefabAssetId"/> links
    /// to. Returns false when the catalog has no entry for it - including when the whole catalog
    /// failed to load (see <see cref="SpriteRecordCatalog"/>'s class doc): callers treat that the same
    /// way as "this particular prefab has no header", a degraded-but-safe spawn with no header-derived
    /// initialization.
    /// </summary>
    bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header);
}

/// <summary>
/// Loads Data/sprite-records.json once (from <see cref="EngineEnvironment.ProjectPath"/>, the same
/// project-root resolution the engine itself uses - this converter output is read-only from here, the
/// converter that writes it stays untouched) and answers <see cref="TryGet"/> from the in-memory
/// dictionary it parses it into.
///
/// Degraded mode: when the file is missing, unreadable, or fails to parse, this logs exactly one
/// warning at construction time and then behaves as an always-empty catalog - every
/// <see cref="TryGet"/> call returns false. Callers (<see cref="AlundraWorldProxy"/>) are documented to
/// treat that as "spawn the entity without header-derived initialization" rather than fail the spawn,
/// so a missing companion file degrades map entities to their pre-header-port behaviour instead of
/// breaking world load entirely.
/// </summary>
public sealed class SpriteRecordCatalog : ISpriteRecordCatalog
{
    private const string DataDirectoryName = "Data";
    private const string FileName = "sprite-records.json";

    private readonly Dictionary<Guid, SpriteRecordHeader> _headersByPrefabId = new();

    /// <summary>Loads from <c>Data/sprite-records.json</c> under <see cref="EngineEnvironment.ProjectPath"/>.</summary>
    public SpriteRecordCatalog() : this(EngineEnvironment.ProjectPath)
    {
    }

    /// <summary>Loads from <c>Data/sprite-records.json</c> under <paramref name="projectPath"/> - the
    /// overload tests use to point at a temporary fixture directory instead of the real project.</summary>
    public SpriteRecordCatalog(string projectPath)
    {
        var filePath = Path.Combine(projectPath, DataDirectoryName, FileName);

        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning(
                    $"SpriteRecordCatalog: '{filePath}' not found; spawns proceed without header "
                    + "initialization (degraded mode).");
                return;
            }

            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, SpriteRecordJson>>(json, SerializerOptions);

            if (parsed == null)
            {
                Logs.WriteWarning(
                    $"SpriteRecordCatalog: '{filePath}' parsed to nothing; spawns proceed without header "
                    + "initialization (degraded mode).");
                return;
            }

            foreach (var (key, value) in parsed)
            {
                if (Guid.TryParse(key, out var prefabAssetId))
                {
                    _headersByPrefabId[prefabAssetId] = value.ToHeader();
                }
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"SpriteRecordCatalog: failed to load '{filePath}' ({ex.Message}); spawns proceed without "
                + "header initialization (degraded mode).");
            _headersByPrefabId.Clear();
        }
    }

    public bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header)
        => _headersByPrefabId.TryGetValue(prefabAssetId, out header);

    // No naming policy: field names/order match SpriteWriter.SpriteRecordJson exactly.
    private static readonly JsonSerializerOptions SerializerOptions = new();

    private sealed class SpriteRecordJson
    {
        [JsonInclude] public int MoreFlags { get; set; }
        [JsonInclude] public int CanPickup { get; set; }
        [JsonInclude] public int FlagsPortraitShadowType { get; set; }
        [JsonInclude] public int ProgramLoad { get; set; }
        [JsonInclude] public int ProgramTick { get; set; }
        [JsonInclude] public int ProgramTouch { get; set; }
        [JsonInclude] public int ProgramDeactivate { get; set; }
        [JsonInclude] public int ProgramInteract { get; set; }
        [JsonInclude] public int OffsetX { get; set; }
        [JsonInclude] public int OffsetY { get; set; }
        [JsonInclude] public int OffsetZ { get; set; }
        [JsonInclude] public int SizeX { get; set; }
        [JsonInclude] public int SizeY { get; set; }
        [JsonInclude] public int SizeZ { get; set; }
        [JsonInclude] public int Contents { get; set; }
        [JsonInclude] public List<AnimDirIdsvJson>? IdsvAnimDirs { get; set; }
        [JsonInclude] public List<AnimSetJson>? AnimSets { get; set; }

        public SpriteRecordHeader ToHeader() => new()
        {
            MoreFlags = MoreFlags,
            CanPickup = CanPickup,
            FlagsPortraitShadowType = FlagsPortraitShadowType,
            ProgramLoad = ProgramLoad,
            ProgramTick = ProgramTick,
            ProgramTouch = ProgramTouch,
            ProgramDeactivate = ProgramDeactivate,
            ProgramInteract = ProgramInteract,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            OffsetZ = OffsetZ,
            SizeX = SizeX,
            SizeY = SizeY,
            SizeZ = SizeZ,
            Contents = Contents,
            IdsvAnimDirs = IdsvAnimDirs == null
                ? Array.Empty<AnimDirIdsv>()
                : IdsvAnimDirs
                    .Select(entry => new AnimDirIdsv
                    {
                        Anim = entry.Anim,
                        Direction = entry.Direction,
                        Frames = entry.Frames ?? new List<int>(),
                        // Absent/unrecognized End (older export, or a future value this DLL does not
                        // know about yet) defaults to Loop - the converter's own pre-End-field
                        // behaviour, and the safe choice: the engine keeps looping, nothing to bridge.
                        End = entry.End switch
                        {
                            "Hold" => AnimationEndKind.Hold,
                            "Chain" => AnimationEndKind.Chain,
                            _ => AnimationEndKind.Loop,
                        },
                        ChainTo = entry.ChainTo ?? 0,
                    })
                    .ToArray(),
            AnimSets = AnimSets is not { Count: > 0 }
                ? null
                : AnimSets.ToDictionary(
                    entry => entry.Anim,
                    entry => new AnimSetEntry
                    {
                        Anim = entry.Anim,
                        Speed = entry.Speed,
                        Acceleration = entry.Acceleration,
                        IsZForceApplied = entry.IsZForceApplied,
                        Sfx = entry.Sfx,
                        Flags = entry.Flags,
                        Unknown = entry.Unknown,
                    }),
        };
    }

    // No naming policy: field names/order match SpriteWriter.AnimDirIdsvJson exactly.
    private sealed class AnimDirIdsvJson
    {
        [JsonInclude] public int Anim { get; set; }
        [JsonInclude] public int Direction { get; set; }
        [JsonInclude] public List<int>? Frames { get; set; }
        [JsonInclude] public string? End { get; set; }
        [JsonInclude] public int? ChainTo { get; set; }
    }

    // No naming policy: field names/order match SpriteBankReader.ReadAnimSetHeader's own JSON contract
    // exactly (see SpriteRecordHeader.AnimSets' own doc).
    private sealed class AnimSetJson
    {
        [JsonInclude] public int Anim { get; set; }
        [JsonInclude] public int Speed { get; set; }
        [JsonInclude] public int Acceleration { get; set; }
        [JsonInclude] public int IsZForceApplied { get; set; }
        [JsonInclude] public int Sfx { get; set; }
        [JsonInclude] public int Flags { get; set; }
        [JsonInclude] public int Unknown { get; set; }
    }
}

/// <summary>
/// In-memory <see cref="ISpriteRecordCatalog"/> for tests: no file I/O, entries added directly.
/// </summary>
public sealed class FakeSpriteRecordCatalog : ISpriteRecordCatalog
{
    private readonly Dictionary<Guid, SpriteRecordHeader> _headersByPrefabId = new();

    public FakeSpriteRecordCatalog Add(Guid prefabAssetId, SpriteRecordHeader header)
    {
        _headersByPrefabId[prefabAssetId] = header;
        return this;
    }

    public bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header)
        => _headersByPrefabId.TryGetValue(prefabAssetId, out header);
}
