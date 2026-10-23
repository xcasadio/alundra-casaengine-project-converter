#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Cells;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests.Cells;

/// <summary>
/// E20.e T1 (docs/plan-e20-heros.md section 1.27, (c1) and (c6) on the REAL map 14 <c>Overworld 3,1-14</c>): the cut of a bush takes its tiles out of the live overlay, one reconstruction per frame however many cuts, none for a cell with no stack;
/// the collision and the navigation are not changed; the world reads the property <c>TileCutEffectId</c> of the map once at its load. The world tests of the cut by the hero and by the wave are T2.
/// </summary>
public sealed class TileEffectWorldTests
{
    private const int Grass = CellStore.CuttableWalkabilityBit;

    private static ArcRun Arc(Action<World>? beforeInitialize = null)
        => new(new ArcSpec("E20e-t1", "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true), beforeInitialize);

    private static int WallModelCount(CellVisualSync sync)
        => ((ICollection)typeof(CellVisualSync).GetField("_wallModel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sync)!).Count;

    private static CellStore StoreOf(ArcRun arc) => Assert.IsType<CellStore>(((IEntityWorldContext)arc.Proxy).CellMutator);

    [Fact]
    public void C1_CuttingTheBushOfTheCell_13_11_TakesExactlyItsTwoTilesOutOfTheOverlay_AfterOneReconstruction()
    {
        using var arc = Arc();
        arc.OneFrame();
        var sync = arc.Proxy.CellVisualSync!;
        var store = StoreOf(arc);
        Assert.NotEqual(0, store.GetWalkability(13, 11) & Grass);
        Assert.Equal(2, store.GetWallTileStack(13, 11)!.Value.Tiles.Count);
        var before = WallModelCount(sync);
        var rebuilds = sync.ReconstructionCount;

        store.CutCell(13, 11);
        sync.FlushPendingOverlayReconstruction();

        Assert.Equal(before - 2, WallModelCount(sync));
        Assert.Equal(rebuilds + 1, sync.ReconstructionCount);
        Assert.Null(store.GetWallTileStack(13, 11));
    }

    [Fact]
    public void C1_SeveralCutsOfTheSameFrame_AreOneReconstruction_AndTakeAllTheirTilesOut()
    {
        using var arc = Arc();
        arc.OneFrame();
        var sync = arc.Proxy.CellVisualSync!;
        var store = StoreOf(arc);
        var before = WallModelCount(sync);
        var rebuilds = sync.ReconstructionCount;

        store.CutCell(13, 11);
        store.CutCell(14, 11);
        store.CutCell(24, 11);
        sync.FlushPendingOverlayReconstruction();

        Assert.Equal(before - 6, WallModelCount(sync));
        Assert.Equal(rebuilds + 1, sync.ReconstructionCount);
    }

    [Fact]
    public void C1_ACellWithoutAStack_31_30_ReconstructsNothing_AndStillLosesItsBit_TheContraryOfABush()
    {
        using var arc = Arc();
        arc.OneFrame();
        var sync = arc.Proxy.CellVisualSync!;
        var store = StoreOf(arc);
        Assert.NotEqual(0, store.GetWalkability(31, 30) & Grass);
        Assert.Null(store.GetWallTileStack(31, 30));
        var before = WallModelCount(sync);
        var rebuilds = sync.ReconstructionCount;

        store.CutCell(31, 30);
        sync.FlushPendingOverlayReconstruction();

        Assert.Equal(0, store.GetWalkability(31, 30) & Grass);
        Assert.Equal(before, WallModelCount(sync));
        Assert.Equal(rebuilds, sync.ReconstructionCount);
    }

    [Fact]
    public void C1_TheCutChangesNeitherTheCollisionNorTheNavigation_TheHeightAndTheWalkMaskStay()
    {
        using var arc = Arc();
        arc.OneFrame();
        var store = StoreOf(arc);
        var field = arc.Proxy.CollisionField!;
        Assert.True(field.TryReadBinaryCell(13, 11, out var heightBefore, out var walkBefore));
        var groundBefore = store.GetGroundProperty(13, 11);
        const int impassableMask = 0x40;   // the bit the navigation and the walk read: not the grass bit.
        var navigationBefore = ((walkBefore | (groundBefore << 8)) & impassableMask) == 0;

        store.CutCell(13, 11);

        Assert.True(field.TryReadBinaryCell(13, 11, out var heightAfter, out var walkAfter));
        Assert.Equal(heightBefore, heightAfter);
        Assert.Equal(walkBefore & ~Grass, walkAfter);
        Assert.Equal(groundBefore, store.GetGroundProperty(13, 11));
        Assert.Equal(navigationBefore, ((walkAfter | (store.GetGroundProperty(13, 11) << 8)) & impassableMask) == 0);
    }

    [Fact]
    public void C6_TheWorldReadsTheRealExportedProperty_TileCutEffectId_OfTheMap14_ItIsTwo()
    {
        using var arc = Arc();
        var tileMap = arc.RealWorld!.Entities.First(e => e.Name == "tileMap").GetComponent<TileMapComponent>()!.TileMapData;
        Assert.Equal("2", tileMap.CustomProperties[TileEffect.CutEffectPropertyName]);   // the real property, not the fallback.
        Assert.False(tileMap.CustomProperties.ContainsKey("BalanceLevel"));
        Assert.Equal(2, ((IScriptHost)arc.Proxy).TileCutEffectIndex);
    }

    [Fact]
    public void C6_TheWorldReadsThePropertyOnceAtItsLoad_ThePropertyOfTheMapChangesTheIndex_ALaterChangeDoesNot()
    {
        using var arc = Arc(world => world.Entities.First(e => e.Name == "tileMap").GetComponent<TileMapComponent>()!.TileMapData.CustomProperties[TileEffect.CutEffectPropertyName] = "7");
        Assert.Equal(7, ((IScriptHost)arc.Proxy).TileCutEffectIndex);

        var tileMap = arc.RealWorld!.Entities.First(e => e.Name == "tileMap").GetComponent<TileMapComponent>()!.TileMapData;
        tileMap.CustomProperties[TileEffect.CutEffectPropertyName] = "9";
        arc.OneFrame();
        Assert.Equal(7, ((IScriptHost)arc.Proxy).TileCutEffectIndex);
    }
}
