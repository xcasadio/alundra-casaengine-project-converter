#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S2") - <see cref="AlundraBackdropStage.Load"/>'s
/// own two mutation-driven pins (§3 "S2" mutation table): the null-service warning (D-E9b-2's "arrêt"
/// clause) and the per-world reset (D-E9b-9: <c>Clear()</c> before every <c>SetLayers</c>, so a second
/// world's layers never accumulate onto the first's).
/// </summary>
public sealed class BackdropStageLoadTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "AlundraBackdropStageLoadTests_" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    /// <summary>
    /// D-E9b-2's own "arrêt" clause: a world with a live <c>Game</c> but no
    /// <see cref="ScrollingLayerService"/> attached (<see cref="AlundraBackdropStage.AttachService"/>
    /// never called, or called with <see langword="null"/>) logs EXACTLY ONE warning - never zero (silent
    /// degraded mode would hide the bug) and never more than one per <see cref="AlundraBackdropStage.Load"/>
    /// call.
    /// </summary>
    [Fact]
    public void Load_WithLiveGameAndNullService_EmitsExactlyOneWarning()
    {
        using var capture = CapturingWarningLogger.Install();

        var stage = new AlundraBackdropStage(); // AttachService never called - _service stays null.
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var world = new World { Name = "TestWorld" };
        AlundraWorldProxyGlobalFreezeTests.SetProperty(world, nameof(World.Game), game);

        stage.Load(world, _projectPath);

        Assert.Single(capture.WarningMessages);
    }

    /// <summary>
    /// D-E9b-9: two successive <c>Load</c> calls on the SAME stage (one proxy per world, but the SAME
    /// attached service) leave <see cref="ScrollingLayerService.LayerCount"/> at the SECOND map's own
    /// value only, with <see cref="ScrollingLayerService.LayersVersion"/> strictly increasing across
    /// both loads. <c>SetLayers</c> itself always fully REPLACES the array (never appends - see
    /// <see cref="ScrollingLayerService.SetLayers"/>), so this pin does NOT by itself prove that
    /// <c>Load</c> calls <c>Clear()</c> before pushing the second world's layers - it only proves the
    /// second <c>SetLayers</c> call's own count and version wins. <c>Clear()</c> matters separately
    /// because it also resets <see cref="ScrollingLayerService.FramesPushed"/>,
    /// <c>PendingTicks</c>, <c>LastPushedScrollX</c>/<c>LastPushedScrollY</c> and <c>CameraTarget</c>,
    /// none of which this pin exercises.
    /// </summary>
    [Fact]
    public void Load_TwoSuccessiveWorlds_LeavesLayerCountAtTheSecondMapValue_LayersVersionStrictlyIncreasing()
    {
        WriteBackdropFixture(mapIndex: 5, worldName: "Map A-5", layerCount: 2);
        WriteBackdropFixture(mapIndex: 6, worldName: "Map B-6", layerCount: 1);

        var stage = new AlundraBackdropStage();
        var service = new ScrollingLayerService();
        stage.AttachService(service);

        var worldA = new World { Name = "Map A-5" };
        stage.Load(worldA, _projectPath);
        Assert.Equal(2, service.LayerCount);
        var versionAfterFirstLoad = service.LayersVersion;

        var worldB = new World { Name = "Map B-6" };
        stage.Load(worldB, _projectPath);

        // Not 3 (2 + 1, the accumulation bug this pin catches) - exactly the second map's own count.
        Assert.Equal(1, service.LayerCount);
        Assert.True(service.LayersVersion > versionAfterFirstLoad);
    }

    private void WriteBackdropFixture(int mapIndex, string worldName, int layerCount)
    {
        var mapFolder = Path.Combine(_projectPath, "Maps", "Zone", worldName);
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        Directory.CreateDirectory(Path.Combine(mapFolder, "backdrop"));

        var worldIndexPath = Path.Combine(_projectPath, "Maps", "world-index.json");
        var index = File.Exists(worldIndexPath)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(worldIndexPath))!
            : new Dictionary<string, string>();
        index[mapIndex.ToString()] = $"Maps\\Zone\\{worldName}\\{worldName}.world";
        File.WriteAllText(worldIndexPath, System.Text.Json.JsonSerializer.Serialize(index));

        var layers = new List<string>();
        for (var i = 0; i < layerCount; i++)
        {
            layers.Add(
                "{ \"LayerId\": " + i + ", \"Mode\": \"Tiles\", \"DepthOrder\": " + i + ", \"Ground\": true, "
                + "\"BlendMode\": 1, \"AnimTimer\": 0, "
                + "\"TextureAssetId\": \"" + Guid.NewGuid() + "\", \"Width\": 640, \"Height\": 480, "
                + "\"Scrollar\": { \"FactorXNum\": 1, \"FactorXDenom\": 1, \"FactorYNum\": 1, \"FactorYDenom\": 1, "
                + "\"ScrollXSpeed\": 0, \"ScrollXPeriod\": 0, \"ScrollYSpeed\": 0, \"ScrollYPeriod\": 0 } }");
        }

        File.WriteAllText(
            Path.Combine(mapFolder, "backdrop", $"{worldName}.backdrop.json"),
            "{ \"MapIndex\": " + mapIndex + ", \"Enabled\": true, \"Layers\": [" + string.Join(",", layers) + "] }");
    }

    /// <summary>Captures every <see cref="Logs.WriteWarning"/> call for one test, then unregisters
    /// itself (<see cref="Logs"/> has no public removal API - same private-field reflection precedent
    /// as <c>AlundraWarpArrivalTests.CapturingLogger</c>) so it does not leak into later tests.</summary>
    private sealed class CapturingWarningLogger : ILogger, IDisposable
    {
        public List<string> WarningMessages { get; } = new();

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }

        public void WriteWarning(string msg) => WarningMessages.Add(msg);

        public void WriteError(string msg)
        {
        }

        public static CapturingWarningLogger Install()
        {
            var logger = new CapturingWarningLogger();
            Logs.AddLogger(logger);
            return logger;
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }
}
