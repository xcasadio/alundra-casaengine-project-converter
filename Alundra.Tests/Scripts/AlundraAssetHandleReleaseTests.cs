using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;

namespace Alundra.Tests.Scripts;

/// <summary>
/// Plan docs/plan-migration-handles.md, M1 (engine ADR-0037): the DLL holds what it uses through the engine's
/// counted handles and gives it back when its owner goes away, so a map's assets can be freed at the next
/// world change.
/// </summary>
public sealed class AlundraAssetHandleReleaseTests
{
    private static readonly Guid FirstTileSetId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000001");
    private static readonly Guid SecondTileSetId = Guid.Parse("a1a1a1a1-0000-0000-0000-000000000002");

    private sealed class TileSetLoader : IAssetLoader
    {
        public int Loads;

        public object LoadAsset(string fileName, AssetContentManager assetContentManager)
        {
            Loads++;
            return new TileSetData();
        }

        public bool IsFileSupported(string fileName) => true;
    }

    private static AssetContentManager NewAssets(out TileSetLoader loader)
    {
        var infos = new Dictionary<Guid, AssetInfo>
        {
            [FirstTileSetId] = new AssetInfo(FirstTileSetId) { Name = "first", FileName = "first.tileset" },
            [SecondTileSetId] = new AssetInfo(SecondTileSetId) { Name = "second", FileName = "second.tileset" },
        };

        var assets = new AssetContentManager
        {
            RuntimeContext = new EngineRuntimeContext(null, Path.GetTempPath(), id => infos.GetValueOrDefault(id)),
        };
        loader = new TileSetLoader();
        assets.RegisterAssetLoader(typeof(TileSetData), loader);
        return assets;
    }

    [Fact]
    public void BuildingTheNavigationGrid_GivesTheTileSetsBack_OnceTheGridIsBuilt()
    {
        var assets = NewAssets(out var loader);
        var tileMapData = new TileMapData();
        tileMapData.TileSetDataAssetIds.Add(FirstTileSetId);
        tileMapData.TileSetDataAssetIds.Add(SecondTileSetId);

        // No navigation layer in this map: the grid is not built, and the tilesets must be given back anyway.
        var grid = AlundraWorldProxy.TryBuildNavigationGrid("TestWorld", assets, tileMapData);

        Assert.Null(grid);
        Assert.Equal(2, loader.Loads);
        Assert.Equal(2, assets.CollectUnreferenced());
    }

    [Fact]
    public void BuildingTheNavigationGrid_LeavesATileSetStillHeldByTheMap()
    {
        var assets = NewAssets(out _);
        using var heldByTheMap = assets.Acquire<TileSetData>(FirstTileSetId);
        var tileMapData = new TileMapData();
        tileMapData.TileSetDataAssetIds.Add(FirstTileSetId);

        AlundraWorldProxy.TryBuildNavigationGrid("TestWorld", assets, tileMapData);

        Assert.Equal(0, assets.CollectUnreferenced());
    }

    [Fact]
    public void OnEndPlay_DisposesTheHudScreen()
    {
        var assets = NewAssets(out _);
        var screen = new AlundraHudScreen(AlundraHudDirector.Instance, assets);
        var proxy = new AlundraWorldProxy();
        proxy.AttachHudScreenForTests(screen);

        proxy.OnEndPlay(null!);

        Assert.True(screen.IsDisposed);
    }
}
