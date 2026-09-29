#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;
using static Alundra.Tests.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>What an arc loads and runs (E19.a T5, docs/plan-e19-opcodes.md §1.3): one real exported map, the
/// flags already set, the hero placed, and the frame budget after which the arc FAILS naming where each program
/// stopped.</summary>
/// <param name="Name">The arc's name in the plan (A0, A0b, A1...), quoted by its failure message.</param>
/// <param name="Zone">The folder of the map under <c>Maps/</c> (<c>The Klark</c>).</param>
/// <param name="WorldName">The world's own name (<c>Ship Klark (inner)-390</c>), also its folder name.</param>
/// <param name="Flags">The persistent game flags set before the first frame (<c>G866</c> is 866).</param>
/// <param name="HeroTileX">The hero's tile, set with its position before the first frame.</param>
/// <param name="HeroTileY">See <paramref name="HeroTileX"/>.</param>
/// <param name="HeroTileZ">See <paramref name="HeroTileX"/>; <c>TileZ = PosZ &gt;&gt; 20</c> exactly.</param>
/// <param name="FrameLimit">The frame budget of the arc.</param>
internal sealed record ArcSpec(
    string Name, string Zone, string WorldName, int[] Flags, int HeroTileX, int HeroTileY, int HeroTileZ, int FrameLimit);

/// <summary>One instruction of the arc's trace: the frame it ran in, the program it belongs to (slot and start
/// code index, the identity a trace record can give without its owner), and its outcome.</summary>
internal readonly record struct ArcInstruction(int Frame, int Slot, int ProgramStart, int Pc, int Opcode, EventTraceKind Kind);

/// <summary>
/// E19.a T5 (docs/plan-e19-opcodes.md §0.2.6, §1.2 T5): the shared support of the chain arcs, built on the montage
/// of <see cref="AlundraSaveBookEndToEndTests"/> - the real <see cref="AlundraWorldProxy.InitializeWithWorld"/>
/// over a real exported map, a hero possessed by a real <see cref="AlundraPlayerController"/> with a pad seam, a
/// real dialogue presenter over a recording UI view, one logic tick per <c>Update(0.02f)</c>. On top of it:
/// <list type="bullet">
/// <item><description>the world is built from the map's folder path (zone and name) with all its layers;</description></item>
/// <item><description>the map's dialogue asset, the shared one and the ETC one are injected (the headless game has no
/// asset manager, so no Yarn node would play otherwise);</description></item>
/// <item><description>flags set, then the hero's <c>Pos*</c> and <c>Tile*</c> set together before the first frame;</description></item>
/// <item><description>a trace collector on every instruction, for the end signal, the last pc of every program and
/// the skipped or exceeded lists;</description></item>
/// <item><description>a frame loop with a numbered budget: past it the arc fails, naming for every program that ran the last
/// (slot, pc, opcode) executed - a stuck arc never hangs the run.</description></item>
/// </list>
/// A missing export FAILS the test naming it, never skips it. The session singletons are reset at the start and at
/// the end, as the E16.e montage does; a class using this support carries
/// <c>[Collection(AlundraMusicPlayerSingletonCollection.Name)]</c>.
/// </summary>
internal sealed class ArcRun : IDisposable
{
    private readonly List<ArcInstruction> _trace = new();
    private readonly Dictionary<(int Slot, int Pc, int Opcode), int> _firstIndex = new();
    private readonly Dictionary<(int Slot, int ProgramStart), ArcInstruction> _lastByProgram = new();
    private readonly string _previousProjectPath;
    private uint _hold;

    public ArcSpec Spec { get; }

    public AlundraWorldProxy Proxy { get; }

    public AlundraEntityScriptProxy Hero { get; }

    public AlundraEventProgramRunner Runner { get; }

    /// <summary>Frames run so far.</summary>
    public int Frame { get; private set; }

    public IReadOnlyList<ArcInstruction> Trace => _trace;

    /// <summary>Called right after every instruction is recorded (so its effects are already applied): the seam
    /// to sample positions at an exact pc.</summary>
    public Action<ArcInstruction>? OnInstruction { get; set; }

    public static AlundraGameState State => AlundraGameState.Instance;

    public ArcRun(ArcSpec spec)
    {
        Spec = spec;
        ResetAll();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);

        var root = FindProjectRoot();
        _previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = root; // the sprite record catalog is resolved when the proxy is built.

        foreach (var flag in spec.Flags)
        {
            State.AddFlag((uint)flag, 1u << (flag & 0x1f));
        }

        var uiView = new AlundraSaveBookTests.RecordingUIViewRuntime();
        var world = BuildWorld(root, spec, uiView);
        var heroEntity = AddHeroPawn(world);
        var controller = world.PlayerControllers.OfType<AlundraPlayerController>().Single();
        controller.PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = _hold, ButtonsJustPressed = _hold };

        Proxy = new AlundraWorldProxy();
        Proxy.InitializeWithWorld(world);
        typeof(AlundraBackdropStage).GetField("_clearColorApplied", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(Proxy._backdropStage, true); // the headless montage has no view for the clear colour.

        Runner = Assert.IsType<AlundraEventProgramRunner>(Proxy.EventProgramRunner);
        Runner.TraceSink = Record;

        // The headless game has no asset manager: the map's dialogues, the shared ones and the ETC texts come from
        // the export on disk.
        var mapFolder = Path.Combine(root, "Maps", spec.Zone, spec.WorldName);
        Runner.MapDialogueAsset = DialogueTestAssets.LoadFromDisk(Path.Combine(mapFolder, "dialogues", spec.WorldName + ".dialogue"));
        Runner.SharedDialogueAsset = DialogueTestAssets.LoadFromDisk(Path.Combine(root, "Dialogues", "Shared.dialogue"));
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.LoadFromDisk(Path.Combine(root, "Dialogues", "Etc.dialogue")));
        Assert.True(AlundraDialogueDirector.Instance.HasPresenter);

        Hero = Assert.IsType<AlundraEntityScriptProxy>(heroEntity.GameplayProxy);

        // The hero's sprite header (flags, programs, body box): AdoptPlayerPawn resolves it through the asset
        // catalog, empty in the test process - the same block, from the export's own catalog file.
        Assert.True(Proxy.SpriteRecordCatalog.TryGet(HeroPrefabId(root), out var heroHeader));
        AlundraWorldProxy.ApplyHeroSpriteHeader(Hero, heroHeader);

        // The map-entry animation (0x36) ends through the animation chain (anim 54 -> 0), which the headless
        // montage never plays: the hero is put at its end, Idle.
        Assert.Equal(AlundraGameState.ResetAnimationId, Hero.TargetAnimationId);
        Hero.TargetAnimationId = 0;

        // Position and tile together, before the first frame: AdoptPlayerPawn puts the hero on the New Game tile
        // and computes its Tile* once, and the zone tests (0x3B) read TileX/Y/Z.
        Hero.PosX = (spec.HeroTileX * 24 + 12) << 16;
        Hero.PosY = (spec.HeroTileY * 16 + 8) << 16;
        Hero.PosZ = spec.HeroTileZ << 20;
        Hero.TileX = spec.HeroTileX;
        Hero.TileY = spec.HeroTileY;
        Hero.TileZ = Hero.PosZ >> 20;
    }

    public void Dispose()
    {
        EngineEnvironment.ProjectPath = _previousProjectPath;
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        ResetAll();
    }

    private static void ResetAll()
    {
        ResetSingletons();
        AlundraEtcStringTable.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
    }

    public IReadOnlyList<AlundraEntityScriptProxy> Entities => ((IEntityWorldContext)Proxy).SpawnedEntities;

    /// <summary>The entity of an "Entities" record of the map (its <c>Index</c>), or null when it never spawned.</summary>
    public AlundraEntityScriptProxy? EntityByRecord(int record) => Entities.FirstOrDefault(e => e.EntityRefId == record);

    /// <summary>The index in the trace of the first execution of (slot, pc, opcode), or -1.</summary>
    public int FirstIndexOf(int slot, int pc, int opcode) => _firstIndex.TryGetValue((slot, pc, opcode), out var index) ? index : -1;

    public bool Has(int slot, int pc, int opcode) => FirstIndexOf(slot, pc, opcode) >= 0;

    /// <summary>Whether (slot, pc, opcode) ran after the trace index <paramref name="afterIndex"/>.</summary>
    public bool HasAfter(int afterIndex, int slot, int pc, int opcode)
    {
        for (var i = afterIndex + 1; i < _trace.Count; i++)
        {
            var t = _trace[i];
            if (t.Slot == slot && t.Pc == pc && t.Opcode == opcode)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every instruction the interpreter skipped by its size, terminated for want of a size, or cut off by
    /// the loop guard.</summary>
    public IEnumerable<ArcInstruction> SkippedOrExceeded => _trace.Where(t =>
        t.Kind is EventTraceKind.UnknownSkipped or EventTraceKind.UnknownNoSizeTerminated or EventTraceKind.LoopBudgetExceeded);

    private void Record(EventTraceRecord record)
    {
        var instruction = new ArcInstruction(
            Frame, record.ProgramSlot, record.State.Parameters[0], record.CodeIndex, record.Opcode, record.Kind);
        _firstIndex.TryAdd((instruction.Slot, instruction.Pc, instruction.Opcode), _trace.Count);
        _trace.Add(instruction);
        _lastByProgram[(instruction.Slot, instruction.ProgramStart)] = instruction;
        OnInstruction?.Invoke(instruction);
    }

    /// <summary>One frame the engine's way: every spawned entity's own <c>Update</c> - the hero's included, whose
    /// controller samples the pad - then the world's.</summary>
    public void OneFrame()
    {
        foreach (var entity in Entities.ToList())
        {
            entity.Update(0.02f);
        }

        Proxy.Update(0.02f);
        Frame++;
    }

    /// <summary>One frame with a pad button held (and just pressed) during it.</summary>
    public void Press(uint button)
    {
        _hold = button;
        OneFrame();
        _hold = 0;
    }

    /// <summary>Frames until <paramref name="done"/> holds, or fails at the arc's frame limit naming the last
    /// instruction of every program that ran and the signal that never came.</summary>
    public void RunUntil(Func<bool> done, string signal)
    {
        while (!done())
        {
            if (Frame >= Spec.FrameLimit)
            {
                throw new XunitException(StuckMessage(signal));
            }

            OneFrame();
        }
    }

    /// <summary>The text of a stuck arc: for every program that ran, the last (slot, pc, opcode) executed.</summary>
    public string StuckMessage(string signal)
    {
        var programs = _lastByProgram
            .OrderBy(p => p.Value.Frame)
            .Select(p => $"slot {p.Key.Slot} program @{p.Key.ProgramStart}: last 0x{p.Value.Opcode:X2} @{p.Value.Pc} ({p.Value.Kind}, frame {p.Value.Frame})");
        var skipped = SkippedOrExceeded.Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})").Distinct().Take(20);
        return $"arc {Spec.Name} did not reach its end signal ({signal}) within {Spec.FrameLimit} frames. "
            + $"Last instruction of each program: [{string.Join("; ", programs)}]. "
            + $"Skipped or exceeded: [{string.Join("; ", skipped)}].";
    }

    /// <summary>Presses Square, the dialogue button, while a dialogue is open, until it closes, like the player.
    /// Fails at the arc's frame limit.</summary>
    public void CloseDialogueWithTheButton(string signal)
    {
        RunUntil(() =>
        {
            if (!AlundraDialogueDirector.Instance.IsOpen)
            {
                return true;
            }

            Press(AlundraPadState.Square);
            return !AlundraDialogueDirector.Instance.IsOpen;
        }, signal);
    }

    private static Guid HeroPrefabId(string root)
    {
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(root, "AssetInfos.json")));
        var entry = ((JArray)assetInfos["asset_infos"]!).Single(e => (string?)e["name"] == "Alundra" && (string?)e["asset_type"] == "entity");
        return Guid.Parse((string)entry["id"]!);
    }

    /// <summary>The headless real-map montage of <see cref="AlundraSaveBookEndToEndTests"/>, generalised to a map's folder
    /// path: all the layers of its tile map, a game whose active render view carries <paramref name="uiView"/>.</summary>
    private static World BuildWorld(string root, ArcSpec spec, AlundraSaveBookTests.RecordingUIViewRuntime uiView)
    {
        var tileMapPath = Path.Combine(root, "Maps", spec.Zone, spec.WorldName, "tilemap", spec.WorldName + ".tileMap");
        Assert.True(File.Exists(tileMapPath), $"the real export of map '{spec.WorldName}' is missing: '{tileMapPath}'");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        var tileSetData = AlundraWorldProxyGlobalFreezeTests.LoadRealVisualTileSet(root, tileMapData);

        var world = SaveScreenTestWorlds.WorldWithUIView(spec.WorldName, uiView);

        var tileMapEntity = new Entity { Name = "tileMap" };
        AlundraWorldProxyGlobalFreezeTests.SetProperty(tileMapEntity, nameof(Entity.World), world);

        var component = new TileMapComponent();
        tileMapEntity.RootComponent = component;
        component.TileMapData = tileMapData;
        component.TileSetData = tileSetData;

        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<TileSetData>(component, "_tileSets").Add(tileSetData);
        AlundraWorldProxyGlobalFreezeTests.GetPrivateList<Texture2D>(component, "_tileSetTextures").Add(null!);

        var runtimeLayers = AlundraWorldProxyGlobalFreezeTests.GetLayers(component);
        for (var layerIndex = 0; layerIndex < tileMapData.Layers.Count; layerIndex++)
        {
            var layer = new TileMapLayer(tileMapData.Layers[layerIndex]);
            var cellCount = tileMapData.MapSize.Width * tileMapData.MapSize.Height;
            for (var i = 0; i < cellCount; i++)
            {
                layer.Tiles.Add(new AlundraWorldProxyGlobalFreezeTests.StubTile());
                layer.CollisionObjects.Add(null);
            }

            runtimeLayers.Add(layer);
            AlundraWorldProxyGlobalFreezeTests.InvokeBuildChunks(component, layer, layerIndex);
        }

        world.Entities.Add(tileMapEntity);
        return world;
    }
}
