#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scripting;
using Newtonsoft.Json.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Maps;

/// <summary>
/// R0 (engine ADR-0075, docs/plan-restructuration-dll.md section 4): the names the converter writes in the data are the FULL names of the
/// gameplay classes (<c>Alundra.Scripts.AlundraEntityScriptProxy</c>, ...), because the engine creates a script object only by full name from the
/// gameplay assembly. These cases lock the data to the types, and the creation catches of the world proxy to the engine's dedicated exception.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ScriptNamesInTheDataTests
{
    private const string WorldRelativePath = "Maps/Overworld/Overworld 3,1-14/Overworld 3,1-14.world";

    private static ArcRun Arc(string name, Action<CasaEngine.Framework.Scene.World.World>? beforeInitialize = null)
        => new(new ArcSpec("R0-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true), beforeInitialize);

    private static JObject ReadJson(string relativePath)
        => JObject.Parse(File.ReadAllText(Path.Combine(SaveGameDirectorTestSupport.FindProjectRoot(), relativePath)));

    // The guard: the exported strings against the types, so a rename of a class or a namespace cannot leave the data behind.

    [Fact]
    public void TheGameMode_NamesThePlayerControllerByItsFullName()
    {
        var document = ReadJson("Entities/Alundra/Alundra.gameMode");
        Assert.Equal(typeof(HeroController).FullName, (string?)document["player_controller_class"]);
        Assert.True(ElementFactory.TryResolveScriptType(typeof(PlayerController), "player_controller_class", typeof(HeroController).FullName!, out var type, out var message), message);
        Assert.Equal(typeof(HeroController), type);
    }

    [Fact]
    public void TheHeroPrefab_NamesTheEntityProxyByItsFullName()
    {
        var document = ReadJson("Entities/Alundra/Alundra.entity");
        Assert.Equal(typeof(EntityScriptProxy).FullName, (string?)document["script_class_name"]);
        Assert.True(ElementFactory.TryResolveScriptType(typeof(GameplayProxy), "script_class_name", typeof(EntityScriptProxy).FullName!, out var type, out var message), message);
        Assert.Equal(typeof(EntityScriptProxy), type);
    }

    [Fact]
    public void AMapWorld_NamesTheWorldProxyByItsFullName()
    {
        var document = ReadJson(WorldRelativePath);
        Assert.Equal(typeof(WorldProxy).FullName, (string?)document["script_class_name"]);
        Assert.True(ElementFactory.TryResolveScriptType(typeof(GameplayProxy), "script_class_name", typeof(WorldProxy).FullName!, out var type, out var message), message);
        Assert.Equal(typeof(WorldProxy), type);
    }

    // The creation catches of AlundraWorldProxy (the map-load loop, SpawnEntityByRecordId, SpawnEntityBySpriteIndex): a script type name that
    // does not resolve is a fault of the data and stops the load; every other fault keeps the log, skip and finish.

    /// <summary>Gives every prefab loaded after the hero's the SIMPLE name of the entity proxy (the form that no longer resolves), as a stale export would.</summary>
    private static void CorruptEveryPrefabButTheHero(CasaEngine.Framework.Scene.World.World world)
    {
        var manager = world.Game!.AssetContentManager;
        var loaders = (System.Collections.IDictionary)typeof(AssetContentManager)
            .GetField("_assetLoaderByType", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        loaders[typeof(CasaEngine.Framework.Scene.Entities.Entity)] = new SimpleNameEntityLoader((IAssetLoader)loaders[typeof(CasaEngine.Framework.Scene.Entities.Entity)]!);
    }

    private sealed class SimpleNameEntityLoader : IAssetLoader
    {
        private readonly IAssetLoader _inner;

        public SimpleNameEntityLoader(IAssetLoader inner) => _inner = inner;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            var asset = _inner.LoadAsset(fileName, assetContentManager);
            if (asset is CasaEngine.Framework.Scene.Entities.Entity entity)
            {
                entity.GameplayProxyClassName = nameof(EntityScriptProxy);
            }

            return asset;
        }

        public bool IsFileSupported(string fileName) => _inner.IsFileSupported(fileName);
    }

    [Fact]
    public void TheMapLoad_OfARecordWhosePrefabNamesItsScriptWrongly_ThrowsNamingThePrefabFile_InsteadOfSkippingTheEntity()
    {
        var thrown = Assert.Throws<ScriptTypeResolutionException>(() => Arc("wrong-prefab-script", CorruptEveryPrefabButTheHero).Dispose());

        Assert.Equal("script_class_name", thrown.Key);
        Assert.Equal(nameof(EntityScriptProxy), thrown.Value);
        Assert.EndsWith(".entity", thrown.FileName, StringComparison.Ordinal);
        Assert.StartsWith("Entities", thrown.FileName, StringComparison.Ordinal);
        Assert.Contains(thrown.FileName, thrown.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(EntityScriptProxy).FullName!, thrown.Message, StringComparison.Ordinal); // the hint names the full name.
    }

    [Fact]
    public void AnotherFaultAtTheSameCreation_StillLogsSkipsAndFinishesTheLoad()
    {
        int normal;
        using (var baseline = Arc("other-fault-baseline"))
        {
            normal = baseline.Entities.Count;
        }

        var thrown = false;
        WorldProxy.CreationFaultHookForTests = (path, _) =>
        {
            if (path == "load" && !thrown)
            {
                thrown = true;
                throw new InvalidOperationException("an ordinary fault after the addition");
            }
        };
        try
        {
            using var arc = Arc("other-fault");
            Assert.True(thrown);
            Assert.Equal(normal - 1, arc.Entities.Count); // that record only is skipped, the load finishes.
            Assert.Equal(1, arc.Log!.Warnings.Count(w => w.Contains("failed to spawn entity for record", StringComparison.Ordinal) && w.Contains("an ordinary fault", StringComparison.Ordinal)));
            Assert.Equal(normal - 1, arc.Proxy.SlotTable.HeldCount); // the slot was given back and re-used: no hole.
        }
        finally
        {
            WorldProxy.CreationFaultHookForTests = null;
        }
    }
}
