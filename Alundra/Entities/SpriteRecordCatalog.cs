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
using Alundra.Scripts;

namespace Alundra.Entities;

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

    /// <summary>E20.e0a1: the prefab of each <c>BankKey</c> (<c>alundra_N</c> or <c>N</c>), for <see cref="TryGetPrefab"/>.</summary>
    private readonly Dictionary<string, Guid> _prefabIdsByBankKey = new(StringComparer.Ordinal);

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
                    if (!string.IsNullOrEmpty(value.BankKey))
                    {
                        _prefabIdsByBankKey[value.BankKey] = prefabAssetId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logs.WriteWarning(
                $"SpriteRecordCatalog: failed to load '{filePath}' ({ex.Message}); spawns proceed without "
                + "header initialization (degraded mode).");
            _headersByPrefabId.Clear();
            _prefabIdsByBankKey.Clear();
        }
    }

    public bool TryGet(Guid prefabAssetId, out SpriteRecordHeader header)
        => _headersByPrefabId.TryGetValue(prefabAssetId, out header);

    /// <summary>The prefab of the sprite at <paramref name="index"/> of a page (see <see cref="ISpriteRecordCatalog.TryGetPrefab"/>): the entries without a <c>BankKey</c> (an older export) are simply absent.</summary>
    public bool TryGetPrefab(bool isMapPage, int index, out Guid prefabAssetId)
        => _prefabIdsByBankKey.TryGetValue(BankKeyOf(isMapPage, index), out prefabAssetId);

    /// <summary>The <c>BankKey</c> of the export for a sprite of a page: <c>alundra_N</c> on the global page, <c>N</c> on the map page.</summary>
    internal static string BankKeyOf(bool isMapPage, int index)
        => isMapPage
            ? index.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "alundra_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Session-scoped cache keyed by project path (D-T-14, docs/plan-transitions-carte.md, slice T1):
    /// <see cref="AlundraWorldProxy"/>'s own field initializer resolves through here instead of always
    /// constructing a fresh catalog, so two consecutive worlds over the SAME project read
    /// <c>Data/sprite-records.json</c> only once - the keying by <paramref name="projectPath"/> is what
    /// keeps two DIFFERENT projects (e.g. two test fixtures) from ever sharing a cached catalog.
    /// </summary>
    private static readonly Dictionary<string, SpriteRecordCatalog> SessionCacheByProjectPath = new();

    /// <summary>Returns the cached catalog for <paramref name="projectPath"/>, loading it once on the
    /// first request and reusing it for every later one - see <see cref="SessionCacheByProjectPath"/>'s
    /// own doc.</summary>
    public static SpriteRecordCatalog GetOrCreate(string projectPath)
    {
        if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var catalog))
        {
            catalog = new SpriteRecordCatalog(projectPath);
            SessionCacheByProjectPath[projectPath] = catalog;
        }

        return catalog;
    }

    /// <summary>Test-only: clears the session cache so tests do not leak a catalog loaded by one test
    /// into another through <see cref="GetOrCreate"/> - same seam as <see cref="GameState.ResetForTests"/>.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();

    // No naming policy: field names/order match SpriteWriter.SpriteRecordJson exactly.
    private static readonly JsonSerializerOptions SerializerOptions = new();

    private sealed class SpriteRecordJson
    {
        [JsonInclude] public string? BankKey { get; set; }
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
        [JsonInclude] public int BreakEffect { get; set; }
        [JsonInclude] public int Contents { get; set; }
        [JsonInclude] public List<AnimDirIdsvJson>? IdsvAnimDirs { get; set; }
        [JsonInclude] public List<AnimSetJson>? AnimSets { get; set; }
        [JsonInclude] public DialoguePortraitJson? DialoguePortrait { get; set; }

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
            BreakEffect = BreakEffect,
            Contents = Contents,
            DialoguePortrait = Guid.TryParse(DialoguePortrait?.SpriteAssetId, out var portraitSpriteAssetId)
                ? new DialoguePortraitRef(portraitSpriteAssetId, DialoguePortrait!.Width, DialoguePortrait.Height)
                : null,
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

    // No naming policy: field names/order match SpriteWriter.DialoguePortraitJson exactly. An unparseable or missing id reads as no portrait.
    private sealed class DialoguePortraitJson
    {
        [JsonInclude] public string? SpriteAssetId { get; set; }
        [JsonInclude] public int Width { get; set; }
        [JsonInclude] public int Height { get; set; }
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
