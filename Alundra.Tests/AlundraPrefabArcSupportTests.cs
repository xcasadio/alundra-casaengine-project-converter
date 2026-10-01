#nullable enable
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Alundra.Tests;

/// <summary>
/// E19.c1 T1 (docs/plan-e19-opcodes.md §1.2e, D-E19-14): the self-test of the "Prefabs" arc mode. It is the guard against a
/// SILENT fallback: without a working asset manager the production spawn path logs a warning and builds a bare entity,
/// and <c>World.InternalAddEntities</c> drops an entity whose initialisation throws without a trace but the log.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraPrefabArcSupportTests
{
    private readonly ITestOutputHelper _output;

    public AlundraPrefabArcSupportTests(ITestOutputHelper output) => _output = output;

    private const string Zone = "Inoa";
    private const string Map478 = "Inoa (Vision Event from Lars and Melzas cutscene)-478";

    [Fact]
    public void PrefabMode_OnMap478_EveryRecordIsARealPrefabEntityOfTheWorld()
    {
        var spec = new ArcSpec("PrefabSelfTest", Zone, Map478, new[] { 1641 }, 22, 57, 1, 100, RealController: true, Prefabs: true);
        using var arc = new ArcRun(spec);
        arc.OneFrame();

        var world = arc.RealWorld!;
        var failures = new List<string>();
        for (var record = 0; record < 22; record++)
        {
            var proxy = arc.EntityByRecord(record);
            if (proxy == null)
            {
                failures.Add($"record {record}: no entity");
                continue;
            }

            if (proxy.Controller == null)
            {
                failures.Add($"record {record}: no controller");
            }

            var entity = world.Entities.FirstOrDefault(e => ReferenceEquals(e.GameplayProxy, proxy));
            if (entity == null)
            {
                failures.Add($"record {record}: not in world.Entities (silently dropped?)");
            }
            else if (!ReferenceEquals(entity.World, world))
            {
                failures.Add($"record {record}: Entity.World is not the arc's world");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));

        // Record 0, the camera block: flags 0x6080, 24 animations, a 24 x 16 x 32 box at local (0, 0, 16).
        var block = arc.EntityByRecord(0)!;
        var blockEntity = world.Entities.Single(e => ReferenceEquals(e.GameplayProxy, block));
        Assert.Equal(0x6080u, block.Flags);
        var sprite = blockEntity.GetComponent<AnimatedSpriteComponent>();
        Assert.NotNull(sprite);
        Assert.Equal(24, sprite!.Animations.Count);
        var fixture = blockEntity.GetComponent<CollisionComponent>()!.Fixtures.Single();
        Assert.Equal(new Vector3(24f, 16f, 32f), ((Box)fixture.Shape!).Size);
        Assert.Equal(new Vector3(0f, 0f, 16f), fixture.LocalPosition);

        // No fallback to a bare entity, no exception logged while the entities were integrated.
        // The only errors left are the sprite resolutions of the animated sprites: the test has no SpriteData loader
        // (it would need a graphics device), so every animation reports it once. Anything else is an exception of the
        // integration (a dropped entity logs its exception here).
        var log = arc.Log!;
        Assert.DoesNotContain(log.Warnings.Concat(log.Errors), m => m.Contains("falling back to a bare entity"));
        var unexpected = log.Errors.Where(e => !e.StartsWith("AnimatedSpriteComponent : can't resolve sprite", System.StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, "errors logged: " + string.Join(" | ", unexpected.Select(e => e.Length > 300 ? e[..300] : e).Take(5)));
        _output.WriteLine($"log volume after the first frame: {log.Errors.Count} sprite-resolution errors ({log.Errors.Sum(e => e.Length)} chars), {log.Warnings.Count} warnings, {log.Infos.Count} infos");
    }

    [Fact]
    public void PrefabMode_WithoutRealController_IsRefused()
    {
        var spec = new ArcSpec("PrefabBare", Zone, Map478, new[] { 1641 }, 22, 57, 1, 100, RealController: false, Prefabs: true);
        Assert.Throws<System.ArgumentException>(() => new ArcRun(spec));
    }
}
