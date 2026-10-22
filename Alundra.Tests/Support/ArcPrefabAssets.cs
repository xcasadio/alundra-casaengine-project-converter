#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;
using static Alundra.Tests.Support.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Objects;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Rendering.Backdrops;
using Alundra.Transitions;
using Alundra.Dialogues;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.c1 T1 (D-E19-14): the asset manager of the <see cref="ArcSpec.Prefabs"/> mode. The headless game is an
/// uninitialised <c>CasaEngineGame</c>, whose <c>AssetContentManager</c> initializer never ran: the production prefab
/// loader throws and every NPC falls back to a bare entity. A manager built here and set by reflection, as the montage
/// already sets the <c>GameManager</c>, gives the production spawn path its real prefab.
/// <list type="bullet">
/// <item><description>Loaders: <c>Entity</c>, <c>Animation2dData</c> and <c>UIScreenAsset</c> only (the HUD wiring of a
/// non-null manager builds a screen that acquires its asset). NEVER <c>SpriteData</c>, <c>Texture</c> or
/// <c>TileSetData</c>: the first two need a graphics device and a failure past them drops the entity, the third would
/// build a navigation grid (no detour: the arcs' walks meet no wall).</description></item>
/// <item><description>The resolver reads the export's <c>AssetInfos.json</c> and answers <c>null</c> for an unknown id
/// (a throwing resolver makes <c>World.InternalAddEntities</c> drop the entity without a trace).</description></item>
/// </list>
/// </summary>
internal static class ArcPrefabAssets
{
    public static AssetContentManager Install(World world, string projectRoot)
    {
        var infos = new Dictionary<Guid, AssetInfo>();
        var assetInfos = JObject.Parse(File.ReadAllText(Path.Combine(projectRoot, "AssetInfos.json")));
        foreach (var node in (JArray)assetInfos["asset_infos"]!)
        {
            var info = new AssetInfo();
            info.Load((JObject)node);
            infos[info.Id] = info;
        }

        var context = new EngineRuntimeContext(new ProjectSettings(), projectRoot, id => infos.TryGetValue(id, out var info) ? info : null!);
        var manager = new AssetContentManager { RuntimeContext = context };
        manager.RegisterAssetLoader(typeof(Entity), new AssetLoader<Entity>());
        manager.RegisterAssetLoader(typeof(Animation2dData), new AssetLoader<Animation2dData>());
        manager.RegisterAssetLoader(typeof(UIScreenAsset), new AssetLoader<UIScreenAsset>());

        typeof(CasaEngineGame)
            .GetField("<AssetContentManager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world.Game, manager);
        return manager;
    }
}
