#nullable enable
using System;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Framework.Assets.Animations;
using Alundra.Entities.Spawning;

namespace Alundra.Scripts;

/// <summary>One frame box of an animation, in the binary's integers (E20.c1a C2): the three signed offsets and the three sizes, in pixels (the binary stores them <c>&lt;&lt; 16</c>, sizes minus 1).</summary>
internal readonly record struct AlundraFrameBox(int OffsetX, int OffsetY, int OffsetZ, int Width, int Depth, int Height);

/// <summary>
/// The box timeline of an animation, read from the exported <c>collision_keyframes</c> (E20.c1a C2, D-E20-6, docs/plan-e20-annexe/d6-boites/notes.md): the box of the frame the binary shows at a logic tick
/// is the last keyframe whose <c>round(TimeSeconds * 50)</c> is at or before that tick; a keyframe with no fixture, or no keyframe yet, means no box (the binary's index <c>0xFFFF</c>,
/// <c>0x80038CB8</c>). Offset = <c>LocalPosition - Size / 2</c>, sizes = <c>Size</c> (X, Y, Z = the record's width, depth, height). Proven equal to the decoded records on 30585 frames and 173408 ticks (D6).
/// <para>
/// The raw tick a caller holds is the number of ticks since the switch; this class applies what the sprite's logical clock does to it: a Loop wraps at its duration, a Once saturates at it (the duration being
/// <c>max(1, round(duration * 50))</c> ticks, 0 when the animation has none). Built once per animation (<see cref="For"/>), then read with no allocation.
/// </para>
/// </summary>
internal sealed class AlundraFrameBoxTimeline
{
    private static readonly ConditionalWeakTable<Animation2d, AlundraFrameBoxTimeline> Cache = new();
    private static readonly ConditionalWeakTable<Animation2dData, AlundraFrameBoxTimeline> DataCache = new();

    private readonly int[] _startTicks;
    private readonly bool[] _hasBox;
    private readonly AlundraFrameBox[] _boxes;

    private AlundraFrameBoxTimeline(int[] startTicks, bool[] hasBox, AlundraFrameBox[] boxes, int durationTicks, bool isLoop)
    {
        _startTicks = startTicks;
        _hasBox = hasBox;
        _boxes = boxes;
        DurationTicks = durationTicks;
        IsLoop = isLoop;
    }

    /// <summary>The duration of the animation in logic ticks (0 when it has none).</summary>
    public int DurationTicks { get; }

    /// <summary>True for a Loop (the tick wraps), false for the others (it saturates at the duration).</summary>
    public bool IsLoop { get; }

    /// <summary>The timeline of <paramref name="animation"/>, built on the first request and kept with the animation.</summary>
    public static AlundraFrameBoxTimeline For(Animation2d animation)
    {
        if (!Cache.TryGetValue(animation, out var timeline))
        {
            timeline = Build(animation.Animation2dData);
            Cache.Add(animation, timeline);
        }

        return timeline;
    }

    /// <summary>
    /// E20.e0a5 (D-E0A5-3): the timeline of the shared <paramref name="data"/> of an animation, built on the first request and kept with the data. It is the timeline of a sprite that holds this very instance (the
    /// asset manager shares one instance per id), so an entity that is not integrated yet and the same entity after the integration read the same boxes.
    /// </summary>
    public static AlundraFrameBoxTimeline ForData(Animation2dData data)
    {
        if (!DataCache.TryGetValue(data, out var timeline))
        {
            timeline = Build(data);
            DataCache.Add(data, timeline);
        }

        return timeline;
    }

    private static AlundraFrameBoxTimeline Build(Animation2dData data)
    {
        var keyframes = data.CollisionKeyframes;
        var startTicks = new int[keyframes.Count];
        var hasBox = new bool[keyframes.Count];
        var boxes = new AlundraFrameBox[keyframes.Count];
        for (var i = 0; i < keyframes.Count; i++)
        {
            startTicks[i] = (int)Math.Round((double)keyframes[i].TimeSeconds * AlundraEntitySpawnFactory.LogicTicksPerSecond, MidpointRounding.AwayFromZero);
            foreach (var fixture in keyframes[i].Fixtures)
            {
                if (fixture.Shape is Box box)
                {
                    var size = box.Size;
                    var local = fixture.LocalPosition;
                    boxes[i] = new AlundraFrameBox(
                        RoundToInt(local.X - size.X / 2f), RoundToInt(local.Y - size.Y / 2f), RoundToInt(local.Z - size.Z / 2f),
                        RoundToInt(size.X), RoundToInt(size.Y), RoundToInt(size.Z));
                    hasBox[i] = true;
                    break;
                }
            }
        }

        var durationSeconds = data.GetDurationSeconds();
        var durationTicks = durationSeconds <= 0f
            ? 0
            : Math.Max(1, (int)Math.Round((double)durationSeconds * AlundraEntitySpawnFactory.LogicTicksPerSecond, MidpointRounding.AwayFromZero));
        return new AlundraFrameBoxTimeline(startTicks, hasBox, boxes, durationTicks, data.AnimationType == AnimationType.Loop);
    }

    private static int RoundToInt(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>The tick of the sprite's logical clock after <paramref name="ticksSinceSwitch"/> ticks (a Loop wraps, a Once saturates at its duration).</summary>
    public int LogicalTick(int ticksSinceSwitch)
    {
        if (IsLoop)
        {
            return DurationTicks > 0 ? ticksSinceSwitch % DurationTicks : 0;
        }

        return Math.Min(ticksSinceSwitch, DurationTicks);
    }

    /// <summary>The box of the frame shown <paramref name="ticksSinceSwitch"/> ticks after the switch; false when there is none.</summary>
    public bool TryGetBox(int ticksSinceSwitch, out AlundraFrameBox box)
    {
        var tick = LogicalTick(ticksSinceSwitch);
        var found = -1;
        for (var i = 0; i < _startTicks.Length; i++)
        {
            if (_startTicks[i] > tick)
            {
                break;
            }

            found = i;
        }

        if (found >= 0 && _hasBox[found])
        {
            box = _boxes[found];
            return true;
        }

        box = default;
        return false;
    }
}
