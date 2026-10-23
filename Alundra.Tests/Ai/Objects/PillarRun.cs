#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Physics;
using Alundra.Ai.Objects;
using Alundra.Tests.Support;
using Alundra.Session;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T2 (docs/plan-e20-heros.md section 1.24.6, phase B): the world of the pushable pillar tests - the REAL world of an exported map with real prefabs and the production registry (<see cref="ArcRun"/>), the hero placed
/// at a pixel and holding a direction, the sounds recorded and the stop of the sound journalled by the named stub of <see cref="PillarAi"/>. One <see cref="Frame"/> is one logic tick (0.02 s); the frame counter starts at 1 on the first frame
/// the direction is held, the way the discovery's probe numbered them (<c>docs/plan-e20-annexe/e20-g5/disc/probe_G5ProbeTests.cs.txt</c>).
/// </summary>
internal sealed class PillarRun : IDisposable
{
    /// <summary>The sound of the push (<see cref="PillarAi.PushSfx"/>).</summary>
    public const int PushSfx = PillarAi.PushSfx;

    /// <summary>One frame of the run: the hero's pixel position, the record it is blocked by and the one that would carry it, the push sounds asked this frame, the pillar followed (position, animation and direction asked, counter) and the stops of the sound asked so far.</summary>
    public readonly record struct Sample(int Frame, double HeroX, double HeroY, double HeroZ, int? BlockedBy, int PushSounds, double PillarX, double PillarY, double PillarZ, uint PillarAnimation, uint PillarDirection, int? SupportedBy, int PillarCounter, int StopCalls);

    private readonly List<Sample> _samples = new();
    private int _soundsSeen;

    public ArcRun Arc { get; }

    public RecordingSoundPlayer Sounds { get; } = new();

    public EntityScriptProxy Hero => Arc.Hero;

    public IReadOnlyList<Sample> Samples => _samples;

    /// <summary>The pillar followed by the samples (its position, its animation and direction asked); the hero's own fields are always sampled.</summary>
    public EntityScriptProxy? Watched { get; set; }

    /// <summary>The frames run since the hero was placed.</summary>
    public int Frame { get; private set; }

    /// <summary>The sounds the stub of the stop was asked to stop, in order.</summary>
    public List<int> Stops { get; } = new();

    /// <param name="zone">The folder of the map under <c>Maps/</c>.</param>
    /// <param name="world">The name of the world.</param>
    /// <param name="arrival">Starts from a portal arrival at the hero's pixel (the montage of the discovery for the maps it entered by a warp).</param>
    /// <param name="destroy">The records removed by a write of the harness before the run (the blockers of a puzzle).</param>
    public PillarRun(string name, string zone, string world, int heroX, int heroY, int heroZ, bool arrival = false, params int[] destroy)
    {
        PillarAi.StopSoundStubCallsForTests = Stops;
        var spec = new ArcSpec("E20g5a-" + name, zone, world, Array.Empty<int>(), heroX / 24, heroY / 16, heroZ / 16, 100000, RealController: true, Prefabs: true,
            Arrival: arrival ? new ArcArrival(heroX << 16, heroY << 16, (heroZ / 16) << 20, GameState.ResetAnimationId, 16) : null);
        Arc = new ArcRun(spec);
        try
        {
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Sounds);
            for (var i = 0; i < 6; i++)
            {
                Arc.OneFrame();
            }

            Arc.PlaceHero(heroX, heroY, heroZ);
            for (var i = 0; i < 3; i++)
            {
                Arc.OneFrame();
            }

            foreach (var record in destroy)
            {
                ((IScriptHost)Arc.Proxy).DestroyEntity(Arc.EntityByRecord(record)!, -1);
            }

            if (destroy.Length > 0)
            {
                for (var i = 0; i < 3; i++)
                {
                    Arc.OneFrame();
                }
            }

            _soundsSeen = Sounds.Requests.Count;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Arc.Dispose();
        PillarAi.StopSoundStubCallsForTests = null;
    }

    /// <summary>The entity of the record <paramref name="record"/>, present in the map.</summary>
    public EntityScriptProxy Record(int record) => Arc.EntityByRecord(record) ?? throw new InvalidOperationException($"no record {record}");

    /// <summary>Runs <paramref name="frames"/> frames holding <paramref name="directions"/> (pad bits, 0 for none), sampling each.</summary>
    public void Hold(uint directions, int frames)
    {
        Arc.HoldDirections(directions);
        for (var i = 0; i < frames; i++)
        {
            Arc.OneFrame();
            Frame++;
            var requests = Sounds.Requests;
            var pushes = 0;
            for (var s = _soundsSeen; s < requests.Count; s++)
            {
                pushes += requests[s] == PushSfx ? 1 : 0;
            }

            _soundsSeen = requests.Count;
            var h = Hero;
            var p = Watched;
            _samples.Add(new Sample(Frame, h.PosX / 65536.0, h.PosY / 65536.0, h.PosZ / 65536.0, h.XCollisionEntity?.EntityRefId, pushes,
                (p?.PosX ?? 0) / 65536.0, (p?.PosY ?? 0) / 65536.0, (p?.PosZ ?? 0) / 65536.0, p?.TargetAnimationId ?? 0, p?.TargetDirection ?? 0, SupportOf(h), p is null ? 0 : PillarAi.ReadCounter(p), Stops.Count));
        }
    }

    /// <summary>The record of the entity that would carry <paramref name="entity"/> now (the highest collidable strictly under its feet, whatever its reach: <see cref="EntitySupport.TryFindSupport"/> with the seed of the spawn), or null.</summary>
    public int? SupportOf(EntityScriptProxy entity)
        => EntitySupport.TryFindSupport(entity, ((IScriptHost)Arc.Proxy).Collidables, int.MinValue, out var support, out _) ? support?.EntityRefId : null;

    /// <summary>The sample of the frame <paramref name="frame"/> (numbered from 1).</summary>
    public Sample At(int frame) => _samples[frame - 1];

    /// <summary>Runs <paramref name="frames"/> frames with no direction held.</summary>
    public void Release(int frames) => Hold(0, frames);

    /// <summary>The pixel position of <paramref name="entity"/> (X, Y, Z).</summary>
    public static (double X, double Y, double Z) PixelOf(EntityScriptProxy entity) => (entity.PosX / 65536.0, entity.PosY / 65536.0, entity.PosZ / 65536.0);
}
