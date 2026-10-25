#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Session;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e0c T2 (docs/plan-e20-heros.md section 1.26, phase B): the world of the pickup tests - the REAL world of an exported map with real prefabs and the catalogue (<see cref="ArcRun"/>, the production spawn path and the
/// production registry of the native handlers), cleared of every entity but the hero, in which an object is dropped by the production code (<see cref="LootDrop.SpawnContents"/>) and followed tick by tick, whatever the number of ticks of
/// a frame. A sample is taken at the head of the programs pass (what the handler of the object sees), after the events passes (what it left, <c>counters</c>) and at the end of the tick (<c>inventory</c>).
/// </summary>
internal sealed class PickupDropRun : IDisposable
{
    public const float OneTick = 0.02f;

    /// <summary>What the object holds at one moment, in the words of the binary.</summary>
    public readonly record struct Words(int Life, int State, uint Bounce, int ForceZ, int Marker, int OnGround, EntityStatus Status, int PosZ);

    /// <summary>One tick of the object: the words at the head of the programs pass, after the events passes and at the end of the tick.</summary>
    public sealed record Sample(int Tick, Words In, Words Out, Words End, int HeroMoney, bool ARan, bool CRan);

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private Words? _in;
    private Words? _out;
    private int _aRuns;
    private int _cRuns;
    private bool _aRan;
    private bool _cRan;

    public ArcRun Arc { get; }

    public RecordingSoundPlayer Sounds { get; } = new();

    public EntityScriptProxy Hero => Arc.Hero;

    public IEntityWorldContext Context => Arc.Proxy;

    public EntityScriptProxy? Drop { get; set; }

    public int Tick { get; private set; }

    public List<Sample> Samples { get; } = new();

    /// <summary>Told after each tick is sampled, with its number.</summary>
    public Action<int>? AfterTick { get; set; }

    /// <summary>Told at the head of each pass of the tick with the pass name, for the tests that act at an exact pass.</summary>
    public Action<string>? OnPass { get; set; }

    /// <summary>The world of <paramref name="world"/> in <paramref name="zone"/>, the hero carried to the pixel (<paramref name="heroX"/>, <paramref name="heroY"/>) on the ground (left where the map puts it when <paramref name="heroX"/> is negative), every other entity removed. <paramref name="flags"/> (E20.e0d): game flags raised before the map is entered.</summary>
    public PickupDropRun(string name, string zone, string world, int heroTileX, int heroTileY, int heroX, int heroY, bool clearTheWorld = true, int[]? flags = null)
    {
        Arc = new ArcRun(new ArcSpec("E20e0c-" + name, zone, world, flags ?? Array.Empty<int>(), heroTileX, heroTileY, 0, 20000, RealController: true, Prefabs: true));
        try
        {
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Sounds);
            for (var i = 0; i < 4; i++)
            {
                Arc.OneFrame();
            }

            if (clearTheWorld)
            {
                foreach (var entity in Arc.Entities.Where(e => !ReferenceEquals(e, Hero)).ToList())
                {
                    Context.DestroyEntity(entity);
                }

                Arc.Proxy.RecycleDestroyedEntities();
            }

            if (heroX >= 0)
            {
                Arc.PlaceHero(heroX, heroY, 0);
                for (var i = 0; i < 6; i++)
                {
                    Arc.OneFrame();
                }
            }

            Arc.Proxy.TickTraceForTests = OnTracedPass;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The map 14 (<c>Overworld 3,1-14</c>, gravity 128), the hero at (212, 96) on the flat cells of the hit tests.</summary>
    public static PickupDropRun OnMap14(string name, bool clearTheWorld = true) => new(name, "Overworld", "Overworld 3,1-14", 8, 6, 212, 96, clearTheWorld);

    public void Dispose()
    {
        Arc.Dispose();
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
    }

    /// <summary>Drops <paramref name="item"/> by the production code, at the pixel (<paramref name="pixelX"/>, <paramref name="pixelY"/>) of the hero's height, as <c>SpawnContents</c> does for a container that holds it.</summary>
    public EntityScriptProxy DropAt(int item, int pixelX, int pixelY, int flag = 0)
    {
        var container = new EntityScriptProxy { ContentsItemId = unchecked((uint)item), ContentsGameFlag = flag, PosX = pixelX << 16, PosY = pixelY << 16, PosZ = Hero.PosZ };
        Assert.True(LootDrop.SpawnContents(Context, GameState.Instance, container), $"the object {item:X2} appeared");
        Drop = Context.SpawnedEntities.Last(e => e.SpriteType == item + 0x1E);
        return Drop;
    }

    /// <summary>The words of <paramref name="entity"/> now.</summary>
    public static Words WordsOf(EntityScriptProxy entity)
        => new(entity.DelayOrAngleOrEntityId, entity.ItemState, Alundra.Ai.Objects.PickupAi.ReadWord284(entity), entity.ForceZ, entity.DamagedTickCounter, entity.IsOnGround, entity.Status, entity.PosZ);

    private void OnTracedPass(string pass)
    {
        OnPass?.Invoke(pass);
        switch (pass)
        {
            case "pad":
                Tick++;
                _in = null;
                _out = null;
                break;
            case "programs":
                _in = Drop is { } d1 && d1.Status != EntityStatus.Destroyed ? WordsOf(d1) : null;
                _aRuns = Runs(ScriptHelper.ProgramALoad);
                _cRuns = Runs(ScriptHelper.ProgramCTick);
                break;
            case "counters":
                _out = Drop is { } d2 && d2.Status != EntityStatus.Destroyed ? WordsOf(d2) : null;
                _aRan = Runs(ScriptHelper.ProgramALoad) > _aRuns;
                _cRan = Runs(ScriptHelper.ProgramCTick) > _cRuns;
                break;
            case "inventory":
                if (Drop is { } d3 && _in is { } input && _out is { } output)
                {
                    Samples.Add(new Sample(Tick, input, output, WordsOf(d3), GameState.Instance.PlayerStats.Money, _aRan, _cRan));
                }

                AfterTick?.Invoke(Tick);
                break;
        }
    }

    /// <summary>How many times the native handler of the key (<paramref name="slot"/>, 255) ran, whatever the entity.</summary>
    public int Runs(int slot) => Arc.Runner.NativeHandlerRunCounts.GetValueOrDefault((slot, 255));

    /// <summary>Runs <paramref name="ticks"/> ticks in frames of <paramref name="ticksPerFrame"/> (the last frame cut), nothing pressed.</summary>
    public void Run(int ticks, int ticksPerFrame)
    {
        for (var done = 0; done < ticks; done += ticksPerFrame)
        {
            Arc.OneFrame(Math.Min(ticksPerFrame, ticks - done) * OneTick);
        }
    }

    /// <summary>Runs in frames of <paramref name="ticksPerFrame"/> until <paramref name="done"/> holds after a tick (it is told at each tick), at most <paramref name="limit"/> ticks; returns whether it held.</summary>
    public bool RunUntil(Func<bool> done, int limit, int ticksPerFrame)
    {
        var finished = false;
        var previous = AfterTick;
        AfterTick = tick =>
        {
            previous?.Invoke(tick);
            finished |= done();
        };
        try
        {
            for (var ticks = 0; ticks < limit && !finished; ticks += ticksPerFrame)
            {
                Arc.OneFrame(ticksPerFrame * OneTick);
            }
        }
        finally
        {
            AfterTick = previous;
        }

        return finished;
    }
}
