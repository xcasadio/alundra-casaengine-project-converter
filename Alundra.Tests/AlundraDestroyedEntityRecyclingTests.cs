#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.r R3-1 (docs/plan-e19-opcodes.md section 1.2p, D-E19-45, ADR-0024): the recycling of the destroyed entities, as the binary does it
/// (<c>UpdateDestroyedEntities</c> <c>0x80038634</c>, first call of <c>UpdateEntities</c>, under the gate <c>g_playerControlFlags &amp; 0x48</c> read after the
/// map events). Driven through the world's own <c>Update</c> on the headless montage of the freeze tests: a hero, an NPC of record 2 and a runner that
/// stands for the map event program and asks the search of <c>0x2C [2]</c> (<c>Result</c> = no entity of record 2) at every pass.
/// </summary>
public sealed class AlundraDestroyedEntityRecyclingTests
{
    private const int Record = 2;

    /// <summary>Stands for a map program: at every pass it records what <c>0x2C [2]</c> would answer, then runs the scripted action of that pass.</summary>
    private sealed class ProbingRunner : IEventProgramRunner
    {
        private readonly AlundraEntityScriptProxy _player;
        private readonly IEntityWorldContext _world;
        public readonly List<int> NoEntityOfRecord2 = new();
        public Action<int>? OnPass;

        public ProbingRunner(AlundraEntityScriptProxy player, IEntityWorldContext world)
        {
            _player = player;
            _world = world;
        }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            var matches = EntitySearchService.GetMatchingEntitiesBySearchType(entity, Record, _world.SpawnedEntities, _player);
            NoEntityOfRecord2.Add(matches.Count == 0 ? 1 : 0);
            OnPass?.Invoke(NoEntityOfRecord2.Count - 1);
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class Montage
    {
        public World World { get; } = new() { Name = "TestWorld" };
        public AlundraWorldProxy Proxy { get; } = new();
        public AlundraEntityScriptProxy Hero { get; }
        public Entity HeroEntity { get; }
        public AlundraEntityScriptProxy Npc { get; }
        public Entity NpcEntity { get; }
        public ProbingRunner Runner { get; }

        public Montage()
        {
            Proxy.InitializeWithWorld(World);
            Hero = NewProxy(-1, EntityStatus.Normal, out var heroEntity);
            HeroEntity = heroEntity;
            Npc = NewProxy(Record, EntityStatus.Normal, out var npcEntity);
            NpcEntity = npcEntity;
            Proxy.PlayerEntity = Hero;

            var spawned = (List<Entity>)typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Proxy)!;
            spawned.Add(heroEntity);
            spawned.Add(npcEntity);

            // One map event whose zone is the hero's tile (0, 0): every tick runs its program once.
            var mapEvents = (List<AlundraMapEvent>)typeof(AlundraWorldProxy).GetField("_mapEvents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Proxy)!;
            mapEvents.Add(new AlundraMapEvent { Id = 0, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0, ProgramBMap = 1, OriginalProgramBMap = 1, Entity = Hero });

            Runner = new ProbingRunner(Hero, Proxy);
            Proxy.EventProgramRunner = Runner;
        }

        public IReadOnlyList<AlundraEntityScriptProxy> Listed => ((IEntityWorldContext)Proxy).SpawnedEntities;
    }

    private static AlundraEntityScriptProxy NewProxy(int recordId, EntityStatus status, out Entity entity)
    {
        entity = new Entity
        {
            Name = "RecycleTestEntity" + recordId,
            RootComponent = new TransformComponent(),
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.EntityRefId = recordId;
        proxy.Status = status;
        return proxy;
    }

    private static void AssertRecycled(Montage m)
    {
        Assert.Equal(EntityStatus.Destroyed, m.Npc.Status);
        Assert.Equal(-1, m.Npc.EntityRefId);
        Assert.DoesNotContain(m.Npc, m.Listed);
        Assert.True(m.NpcEntity.ToBeRemoved);
    }

    private static void AssertNotRecycled(Montage m)
    {
        Assert.Equal(EntityStatus.FlagToDestroy, m.Npc.Status);
        Assert.Equal(Record, m.Npc.EntityRefId);
        Assert.Contains(m.Npc, m.Listed);
        Assert.False(m.NpcEntity.ToBeRemoved);
    }

    [Fact]
    public void TheCorpseIsSeenByTheSearchBeforeTheRecyclingAndNotAfter_AndIsPutBackToTheTemplate()
    {
        var m = new Montage();
        m.Runner.OnPass = pass =>
        {
            // The destruction of a script of an entity, done during the entity update that precedes the map events of the same image.
            if (pass == 0)
            {
                m.Npc.Status = EntityStatus.FlagToDestroy;
            }
        };

        // Image 1: the program of the map sees the live NPC (Result 0), then destroys it; nothing recycled yet (the map events come first).
        m.Proxy.Update(0.02f);
        Assert.Equal(new[] { 0 }, m.Runner.NoEntityOfRecord2);

        // Image 1 ends with the recycling (the pass that follows the map events): the NPC is back to the template of the binary.
        AssertRecycled(m);
        Assert.Contains(m.Hero, m.Listed);

        // Image 2: the same search now finds nothing: Result 1.
        m.Proxy.Update(0.02f);
        Assert.Equal(new[] { 0, 1 }, m.Runner.NoEntityOfRecord2);
    }

    [Fact]
    public void ACorpseThatTheEntitiesLeftBeforeTheMapEvents_IsSeenByExactlyOnePassOfTheMapEvents()
    {
        var m = new Montage();
        m.Npc.Status = EntityStatus.FlagToDestroy; // posed during the entity update of the image, before the map events.

        m.Proxy.Update(0.02f);

        Assert.Equal(new[] { 0 }, m.Runner.NoEntityOfRecord2);
        AssertRecycled(m);
        m.Proxy.Update(0.02f);
        Assert.Equal(new[] { 0, 1 }, m.Runner.NoEntityOfRecord2);
    }

    [Fact]
    public void OnAnImageOfTwoTicks_ACorpsePosedByTheFirstPassOfTheMapEvents_IsRecycledBeforeTheSecond()
    {
        var m = new Montage();
        m.Runner.OnPass = pass =>
        {
            if (pass == 0)
            {
                m.Npc.Status = EntityStatus.FlagToDestroy;
            }
        };

        m.Proxy.Update(0.04f);

        Assert.Equal(new[] { 0, 1 }, m.Runner.NoEntityOfRecord2);
        AssertRecycled(m);
    }

    [Fact]
    public void WithAGateBitPosedBeforeTheImage_NothingIsRecycled_ThenTheCorpseGoesWhenTheGateIsLifted()
    {
        var m = new Montage();
        m.Npc.Status = EntityStatus.FlagToDestroy;
        m.Proxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;

        m.Proxy.Update(0.02f);

        AssertNotRecycled(m);

        m.Proxy.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        m.Proxy.Update(0.02f);

        AssertRecycled(m);
    }

    [Fact]
    public void AMapProgramThatDestroysAndOpensABoxInTheSameTick_DelaysTheRecyclingUntilTheBoxCloses()
    {
        var m = new Montage();
        m.Runner.OnPass = pass =>
        {
            if (pass == 0)
            {
                m.Npc.Status = EntityStatus.FlagToDestroy;
                m.Proxy.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen; // the box opens in the same program.
            }
        };

        // The gate is read again after the map events (0x8003B38C): the box that the program just opened holds the recycling back.
        m.Proxy.Update(0.02f);
        AssertNotRecycled(m);

        m.Proxy.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
        m.Proxy.Update(0.02f);
        AssertRecycled(m);
    }

    [Fact]
    public void TheHeroIsNeverRecycled()
    {
        var m = new Montage();
        m.Hero.Status = EntityStatus.FlagToDestroy;

        m.Proxy.Update(0.02f);

        Assert.Equal(EntityStatus.FlagToDestroy, m.Hero.Status);
        Assert.Equal(-1, m.Hero.EntityRefId);
        Assert.Contains(m.Hero, m.Listed);
        Assert.False(m.HeroEntity.ToBeRemoved);
    }

    [Fact]
    public void ALiveEntityStaysListedAndUntouched()
    {
        var m = new Montage();

        m.Proxy.Update(0.02f);

        Assert.Equal(EntityStatus.Normal, m.Npc.Status);
        Assert.Equal(Record, m.Npc.EntityRefId);
        Assert.Contains(m.Npc, m.Listed);
        Assert.False(m.NpcEntity.ToBeRemoved);
    }
}
