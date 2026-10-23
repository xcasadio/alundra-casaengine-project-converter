#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T1 (docs/plan-e20-heros.md section 1.24.6): the world of the arcs of the scripted buttons and blocks - the REAL world of an exported map with real prefabs (<see cref="ArcRun"/>), the hero placed at a pixel from a portal
/// arrival, holding a direction, and one sample per frame of the hero and of the watched records. The set-up and the numbering are the discovery's probe's (<c>docs/plan-e20-annexe/e20-g5/disc/probe_G5ProbeTests.cs.txt</c>):
/// 6 frames to settle, the placement, 3 frames, then the frame counter starts at 1 on the first frame a direction is held. <see cref="ArcRun.Frame"/> of the trace is the pad frame plus <see cref="Offset"/>.
/// </summary>
internal sealed class ScriptedPushRun : IDisposable
{
    /// <summary>One frame: the hero's pixel position, the record that blocks it (null if none), and the watched records' position and animation.</summary>
    public readonly record struct Sample(int Frame, double HeroX, double HeroY, double HeroZ, int? BlockedBy, IReadOnlyDictionary<int, (double X, double Y, uint Animation)> Watched);

    private readonly List<Sample> _samples = new();

    public ArcRun Arc { get; }

    public IReadOnlyList<Sample> Samples => _samples;

    /// <summary>The trace frame of the last frame before the pad frame 1 (a pad frame <c>n</c> is the trace frame <c>Offset + n - 1</c>, see <see cref="FramesOf"/>).</summary>
    public int Offset { get; }

    /// <summary>The records whose position is sampled.</summary>
    public int[] Watch { get; }

    public int Frame { get; private set; }

    public EntityScriptProxy Hero => Arc.Hero;

    /// <param name="zone">The folder of the map under <c>Maps/</c>.</param>
    /// <param name="world">The name of the world.</param>
    /// <param name="flags">The flags set before the map entry.</param>
    public ScriptedPushRun(string name, string zone, string world, int[] flags, int heroX, int heroY, int heroZ, params int[] watch)
    {
        Watch = watch;
        Arc = new ArcRun(new ArcSpec("E20g5b-" + name, zone, world, flags, heroX / 24, heroY / 16, heroZ / 16, 100000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(heroX << 16, heroY << 16, (heroZ / 16) << 20, GameState.ResetAnimationId, 16)));
        try
        {
            for (var i = 0; i < 6; i++)
            {
                Arc.OneFrame();
            }

            Arc.PlaceHero(heroX, heroY, heroZ);
            for (var i = 0; i < 3; i++)
            {
                Arc.OneFrame();
            }

            Offset = Arc.Frame;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose() => Arc.Dispose();

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
            var hero = Arc.Hero;
            var watched = new Dictionary<int, (double X, double Y, uint Animation)>();
            foreach (var record in Watch)
            {
                var entity = Arc.EntityByRecord(record)!;
                watched[record] = (entity.PosX / 65536.0, entity.PosY / 65536.0, entity.TargetAnimationId);
            }

            _samples.Add(new Sample(Frame, hero.PosX / 65536.0, hero.PosY / 65536.0, hero.PosZ / 65536.0, hero.XCollisionEntity?.EntityRefId, watched));
        }
    }

    public void Release(int frames) => Hold(0, frames);

    /// <summary>The sample of the pad frame <paramref name="frame"/> (numbered from 1).</summary>
    public Sample At(int frame) => _samples[frame - 1];

    /// <summary>The first pad frame where <paramref name="predicate"/> holds, or -1.</summary>
    public int FirstFrame(Func<Sample, bool> predicate) => _samples.FirstOrDefault(predicate).Frame is var f && f > 0 ? f : -1;

    /// <summary>
    /// The pad frames of the instruction (<paramref name="slot"/>, <paramref name="pc"/>, <paramref name="opcode"/>) in the trace, from the first held frame on. The trace records an instruction with the
    /// <see cref="ArcRun.Frame"/> of the frame it runs in, read before the frame ends: the pad frame <c>n</c> is the trace frame <c>Offset + n - 1</c>.
    /// </summary>
    public List<int> FramesOf(int slot, int pc, int opcode)
        => Arc.Trace.Where(t => t.Slot == slot && t.Pc == pc && t.Opcode == opcode && t.Frame >= Offset).Select(t => t.Frame - Offset + 1).ToList();

    /// <summary>True when the flag (the number the scripts use, bank and bit as <c>0x05</c> reads them) is set.</summary>
    public static bool HasFlag(uint flag) => (GameState.Instance.GetFlag(flag) & (1u << (int)(flag & 0x1f))) != 0;

    public static uint Pad(char c) => c switch
    {
        'U' => PadState.Up,
        'D' => PadState.Down,
        'L' => PadState.Left,
        'R' => PadState.Right,
        _ => 0u,
    };
}
