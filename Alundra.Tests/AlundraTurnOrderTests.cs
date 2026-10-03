#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.m1 (docs/plan-e19-opcodes.md, section 1.2s.2, M-47): opcodes <c>0x5A</c> (<c>0x8003EEF4</c>) and <c>0x5B</c> (<c>0x8003EF80</c>)
/// walk the entities found by the search from the LAST to the FIRST (the buffer <c>0x8013D8D8</c> from index n-1 down to 0). It matters for
/// the modes that draw from the shared generator (4 and 5: the last found takes the first draw) and for mode 6 (reads the hero direction
/// the loop writes). The class is in the collection that serialises the generator's static state.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraTurnOrderTests : IDisposable
{
    private const ulong Seed = 0xB017C93D;

    public AlundraTurnOrderTests() => AlundraRandom.Reset();

    public void Dispose() => AlundraRandom.Reset();

    private sealed class WorldContext : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Entities { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Entities;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity)
        {
        }

        public NavigationGrid2D? NavigationGrid => null;
    }

    private static AlundraEntityScriptProxy NewNormalEntity(bool isPlayer = false)
        => new() { Status = EntityStatus.Normal, IsPlayer = isPlayer };

    private static void Run(WorldContext context, params int[] codes)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);
        var owner = NewNormalEntity();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        runner.RunOneScriptCall(owner, state);
    }

    [Fact]
    public void Turn_0x5A_Mode4_TwoEntitiesFound_TheLastFoundTakesTheFirstDraw()
    {
        AlundraRandom.RandomSeed = Seed;
        var a = NewNormalEntity();
        var b = NewNormalEntity();
        var context = new WorldContext();
        context.Entities.Add(a);
        context.Entities.Add(b);

        Run(context, 0x5A, 0x83, 0x80, 0xFF);

        Assert.Equal(24u, a.TargetDirection);
        Assert.Equal(0u, b.TargetDirection);
        Assert.Equal(0xC81B4C37u, (uint)AlundraRandom.RandomSeed);
    }

    [Fact]
    public void Turn_0x5A_Mode5_TwoEntitiesFound_TheLastFoundTakesTheFirstDraw()
    {
        AlundraRandom.RandomSeed = Seed;
        var a = NewNormalEntity();
        var b = NewNormalEntity();
        var context = new WorldContext();
        context.Entities.Add(a);
        context.Entities.Add(b);

        Run(context, 0x5A, 0x83, 0xA0, 0xFF);

        Assert.Equal(25u, a.TargetDirection);
        Assert.Equal(6u, b.TargetDirection);
    }

    [Fact]
    public void Turn_0x5B_Mode5_TwoEntitiesFound_BothGetTheAnimation_TheLastFoundTakesTheFirstDraw()
    {
        AlundraRandom.RandomSeed = Seed;
        var a = NewNormalEntity();
        var b = NewNormalEntity();
        var context = new WorldContext();
        context.Entities.Add(a);
        context.Entities.Add(b);

        Run(context, 0x5B, 0x83, 0x05, 0xA0, 0xFF);

        Assert.Equal(5u, a.TargetAnimationId);
        Assert.Equal(5u, b.TargetAnimationId);
        Assert.Equal(25u, a.TargetDirection);
        Assert.Equal(6u, b.TargetDirection);
    }

    [Fact]
    public void Turn_0x5A_Mode6_HeroFirstThenX_TheHeroIsProcessedLast_XReadsTheHeroOldDirection()
    {
        var hero = NewNormalEntity(isPlayer: true);
        hero.TargetDirection = 4;
        var x = NewNormalEntity();
        var context = new WorldContext { PlayerEntity = hero };
        context.Entities.Add(hero);
        context.Entities.Add(x);

        Run(context, 0x5A, 0x82, 0xC3, 0xFF);

        Assert.Equal(7u, hero.TargetDirection);
        Assert.Equal(7u, x.TargetDirection);
    }
}
