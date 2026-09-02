#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Pins the PRODUCTION wiring of D-T-3 (docs/plan-transitions-carte.md, slice T1):
/// <c>AlundraWorldProxy.GameState</c> resolves to <see cref="AlundraGameState.Instance"/> instead of a
/// per-world field initializer.
///
/// Why this class exists: the closing verifier of T1 found that this one statement - the most
/// load-bearing of the whole slice, since it is what actually makes flags survive a map change - was
/// green but unpinned. <see cref="AlundraGameStateSessionTests"/> drives the session carrier directly
/// and would stay green if the proxy went back to <c>= new()</c> per world; the classes that do build
/// two proxies (<see cref="AlundraMusicPlayerTests"/>, <see cref="AlundraScreenFadeDirectorTests"/>)
/// never touch <c>GameState</c>. Two bare proxies are enough to close that gap - no world, no tileMap,
/// none of the <c>InitializeWithWorld</c> montage T2 will build.
///
/// This class constructs <see cref="AlundraWorldProxy"/>, so it falls under D-T-14's own "every test
/// class that builds a proxy" criterion and resets the three session carriers in constructor (the
/// isolation-carrying half: xunit builds a fresh instance per test) and in Dispose (hygiene).
/// </summary>
public sealed class AlundraWorldProxySessionStateTests : IDisposable
{
    public AlundraWorldProxySessionStateTests()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    public void Dispose()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests(); // T4 (D-T-14): warp director joins the session carriers this class resets.
    }

    [Fact]
    public void TwoConsecutiveProxies_ShareTheOneSessionGameState()
    {
        var worldOneProxy = new AlundraWorldProxy();
        var worldTwoProxy = new AlundraWorldProxy();

        // D-T-3: the proxy is rebuilt per world by ElementFactory, but its game state is not.
        Assert.Same(worldOneProxy.GameState, worldTwoProxy.GameState);
        Assert.Same(AlundraGameState.Instance, worldOneProxy.GameState);
    }

    [Fact]
    public void AGameFlagSetThroughOneProxy_IsStillReadableThroughTheNextWorldsProxy()
    {
        var worldOneProxy = new AlundraWorldProxy();

        // Flag 860 is the intro guard of map 389: block 18 sets it, and the intro's own B-129 program
        // only runs while it is clear. Losing it across a map change is exactly what would replay the
        // whole ship intro on the way back (§1.4.c), which is what T6's in-game acceptance checks.
        worldOneProxy.GameState.AddFlag(flag: 860, mask: 0x1);

        var worldTwoProxy = new AlundraWorldProxy();

        Assert.Equal(0x1u, worldTwoProxy.GameState.GetFlag(860));
    }

    [Fact]
    public void SpriteRecordCatalog_GetOrCreate_ReadsTheRecordsOnlyOnceAcrossTwoWorlds_SameProjectPath()
    {
        var projectRoot = BuildTempProjectWithEmptySpriteRecords();
        try
        {
            var worldOneCatalog = SpriteRecordCatalog.GetOrCreate(projectRoot);
            var worldTwoCatalog = SpriteRecordCatalog.GetOrCreate(projectRoot);

            // Same instance both times: Data/sprite-records.json was parsed once, at the first request.
            Assert.Same(worldOneCatalog, worldTwoCatalog);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void SpriteRecordCatalog_GetOrCreate_DoesNotShareACacheEntryAcrossDifferentProjectPaths()
    {
        var projectRootA = BuildTempProjectWithEmptySpriteRecords();
        var projectRootB = BuildTempProjectWithEmptySpriteRecords();
        try
        {
            var catalogA = SpriteRecordCatalog.GetOrCreate(projectRootA);
            var catalogB = SpriteRecordCatalog.GetOrCreate(projectRootB);

            Assert.NotSame(catalogA, catalogB); // D-T-14: two projects never share a cached catalog.
        }
        finally
        {
            Directory.Delete(projectRootA, recursive: true);
            Directory.Delete(projectRootB, recursive: true);
        }
    }

    private static string BuildTempProjectWithEmptySpriteRecords()
    {
        var root = Path.Combine(Path.GetTempPath(), "AlundraWorldProxySessionStateTests_" + Guid.NewGuid().ToString("N"));
        var dataDir = Path.Combine(root, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "sprite-records.json"), "[]");
        return root;
    }
}
