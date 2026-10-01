#nullable enable
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Alundra.Scripts;
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

    [Fact]
    public void PrefabMode_OnMap478_TheHeroIsTheRealPrefabOfTheHero()
    {
        // E19.d D1 (TH1, docs/plan-e19-opcodes.md §1.2g): the hero of the arcs is the prefab of production (World.SpawnEntity), with
        // its animated sprite on the logical clock, not a bare box and a controller.
        var spec = new ArcSpec("PrefabHeroSelfTest", Zone, Map478, new[] { 1641 }, 22, 57, 1, 200, RealController: true, Prefabs: true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var arc = new ArcRun(spec);
        arc.OneFrame();
        stopwatch.Stop();

        // (1) In the world, with its sprite on the logical clock, its box, its controller.
        var world = arc.RealWorld!;
        Assert.Contains(arc.HeroEntity, world.Entities);
        Assert.Same(world, arc.HeroEntity.World);
        var sprite = arc.HeroEntity.GetComponent<AnimatedSpriteComponent>();
        Assert.NotNull(sprite);
        Assert.Equal(376, sprite!.Animations.Count);
        Assert.Same(sprite, arc.Hero.LogicalClockSprite);
        Assert.Equal(50, sprite.LogicalTickRate);
        var collision = arc.HeroEntity.GetComponent<CollisionComponent>()!;
        var fixture = collision.Fixtures.Single();
        Assert.Equal(new Vector3(21f, 15f, 32f), ((Box)fixture.Shape!).Size);
        Assert.Equal(new Vector3(0.5f, 0.5f, 16f), fixture.LocalPosition);
        Assert.Equal(CasaEngine.Engine.Physics.PhysicsType.Kinetic, collision.PhysicsType);
        Assert.True(string.IsNullOrEmpty(collision.PhysicsDefinition.ProfileName), "the hero's collision profile is not empty (contact response)");
        Assert.Empty(collision.Collisions);
        Assert.NotNull(arc.Hero.Controller);
        Assert.Equal(0x41u, arc.Hero.Controller!.Settings.WalkabilityMask);

        // (2) At the end of frame 0: the forced Idle 0, logical tick 0.
        Assert.Equal("bankalundra_0_anim0_down", sprite.CurrentAnimation.Animation2dData.Name);
        Assert.Equal(0, sprite.LogicalTick);

        // (4) The log after the first frame: only the sprite-resolution errors (3628 of the NPCs + 2835 of the hero), no fallback.
        var log = arc.Log!;
        Assert.DoesNotContain(log.Warnings.Concat(log.Errors), m => m.Contains("falling back to a bare entity"));
        var unexpected = log.Errors.Where(e => !e.StartsWith("AnimatedSpriteComponent : can't resolve sprite", System.StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, "errors logged: " + string.Join(" | ", unexpected.Select(e => e.Length > 300 ? e[..300] : e).Take(5)));
        Assert.Equal(6463, log.Errors.Count);
        _output.WriteLine($"construction and first frame: {stopwatch.ElapsedMilliseconds} ms; {log.Errors.Count} sprite-resolution errors");

        // (3) The Loop of 54 ticks of the idle animation turns: the counter reads 0 at frame 54, 1 at 55, 2 at 109.
        var counters = new Dictionary<int, int>();
        while (arc.Frame < 109)
        {
            arc.OneFrame();
            if (arc.Frame is 54 or 55 or 109)
            {
                counters[arc.Frame] = arc.Hero.AnimCompleteCounter;
            }
        }

        Assert.Equal(new[] { 0, 1, 2 }, new[] { counters[54], counters[55], counters[109] });
    }

    [Fact]
    public void ArcRun_ResetsTheSharedRandomStream_AtConstructionAndAtDispose()
    {
        // E19.d D1 (TH2): the stream of the arcs starts from the seed whatever the tests that ran before.
        var spec = new ArcSpec("RandomReset", "The Klark", "Ship Klark (inner)-390", System.Array.Empty<int>(), 0, 0, 0, 10);
        AlundraRandom.Next();
        AlundraRandom.Next();
        AlundraRandom.Next();
        using (var arc = new ArcRun(spec))
        {
            Assert.Equal(0xB017C93Dul, AlundraRandom.RandomSeed);
            AlundraRandom.Next();
        }

        Assert.Equal(0xB017C93Dul, AlundraRandom.RandomSeed);
    }

    [Fact]
    public void ArcRun_HeldDirections_PersistOverFrames_AndOnlyTheFirstFrameIsJustPressed()
    {
        // E19.d D1 (TH3): HoldDirections/ReleaseDirections, beside Press (which keeps its meaning for A1 and A9).
        var spec = new ArcSpec("HeldDirections", "The Klark", "Ship Klark (inner)-390", System.Array.Empty<int>(), 0, 0, 0, 100);
        using var arc = new ArcRun(spec);

        arc.HoldDirections(AlundraPadState.Down);
        arc.OneFrame();
        Assert.Equal((0x4000u, 0x4000u), (ArcRun.State.LastPadState.ButtonsHold, ArcRun.State.LastPadState.ButtonsJustPressed));
        arc.OneFrame();
        Assert.Equal((0x4000u, 0u), (ArcRun.State.LastPadState.ButtonsHold, ArcRun.State.LastPadState.ButtonsJustPressed));

        arc.Press(AlundraPadState.Square);
        Assert.Equal((0x4080u, 0x0080u), (ArcRun.State.LastPadState.ButtonsHold, ArcRun.State.LastPadState.ButtonsJustPressed));

        arc.ReleaseDirections();
        arc.OneFrame();
        Assert.Equal((0u, 0u), (ArcRun.State.LastPadState.ButtonsHold, ArcRun.State.LastPadState.ButtonsJustPressed));

        arc.HoldDirections(AlundraPadState.Down);
        arc.OneFrame();
        Assert.Equal(0x4000u, ArcRun.State.LastPadState.ButtonsJustPressed);
    }
}
