#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using Xunit;
using Alundra.Session;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b-2 (docs/plan-e20-heros.md section 1.7): a recorder of the sounds asked, stamped with the frame of the arc.
/// </summary>
internal sealed class LeverStampedSounds : IAlundraSoundPlayer
{
    private readonly Func<int> _frame;

    public LeverStampedSounds(Func<int> frame) => _frame = frame;

    public List<(int Frame, int Id)> Requests { get; } = new();

    public void PlaySfx(int sfxId) => Requests.Add((_frame(), sfxId));

    public void RemixVoice(int sfxId, int left, int right)
    {
    }

    public void FlushFrameSounds()
    {
    }

    public void StopAllSfx()
    {
    }

    /// <summary>Whether the sound <paramref name="id"/> was asked at the frame <paramref name="frame"/> (the frame during which the request ran).</summary>
    public bool Contains(int frame, int id) => Requests.Contains((frame, id));

    /// <summary>Installs a recorder on the world proxy of the arc (the proxy of the headless montage has no sound player).</summary>
    public static LeverStampedSounds Inject(ArcRun arc)
    {
        var player = new LeverStampedSounds(() => arc.Frame);
        typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, player);
        return player;
    }
}

/// <summary>One cell as the lever programs rewrite it: walkability, height, ground property, floor tile and the wall stack.</summary>
internal readonly record struct LeverCell(int Walkability, int Height, int GroundProperty, int FloorTile, string WallStack)
{
    public override string ToString() => $"[W{Walkability} H{Height} GP{GroundProperty} T{FloorTile} S{WallStack}]";
}

/// <summary>
/// E20.c1b-2: the worlds, the specs and the readers of the lever arcs. Every value of the arcs comes from docs/plan-e20-heros.md section 1.7
/// and docs/plan-e20-annexe/e20-c1b2/ (check.md first).
/// </summary>
internal static class LeverArcs
{
    public const uint Square = 0x80;

    public const string Zone = "Tarn's Manor";
    public const string World115 = "Tarn's Manor-115";
    public const string World116 = "Tarn's Manor-116";
    public const string World117 = "Tarn's Manor (unused broken)-117";
    public const string Zone44 = "Wendels Nightmare";
    public const string World44 = "Wendels Nightmare-44";

    /// <summary>A spec on the real prefabs and controller, arriving at a pixel (the arrival is what puts the hero in the spawn zone of the lever records).</summary>
    public static ArcSpec Spec(string name, string zone, string world, int[] flags, int pixelX, int pixelY, int zLevel, uint direction, int frameLimit) => new(
        name, zone, world, flags, 0, 0, 0, frameLimit, RealController: true, Prefabs: true,
        Arrival: new ArcArrival(pixelX << 16, pixelY << 16, zLevel << 20, AlundraGameState.ResetAnimationId, direction));

    public static AlundraCellStore Store(ArcRun arc) => (AlundraCellStore)arc.Proxy.CellMutator!;

    public static LeverCell CellAt(AlundraCellStore store, int x, int y)
    {
        var stack = store.GetWallTileStack(x, y);
        var text = stack is null ? "none" : stack.Value.Offset + ":" + string.Join(",", stack.Value.Tiles);
        return new LeverCell(store.GetWalkability(x, y), store.GetHeight(x, y), store.GetGroundProperty(x, y), store.GetFloorTileId(x, y), text);
    }

    /// <summary>The cells of a rectangle, row by row.</summary>
    public static List<LeverCell> Rect(AlundraCellStore store, int x, int y, int width, int height)
    {
        var cells = new List<LeverCell>();
        for (var j = 0; j < height; j++)
        {
            for (var i = 0; i < width; i++)
            {
                cells.Add(CellAt(store, x + i, y + j));
            }
        }

        return cells;
    }

    /// <summary>The wall stack of a cell as a list (empty when the cell carries none).</summary>
    public static int[] Walls(AlundraCellStore store, int x, int y) => store.GetWallTileStack(x, y)?.Tiles.ToArray() ?? Array.Empty<int>();

    /// <summary>The frames at which the pc of the slot ran (any opcode).</summary>
    public static List<int> FramesAt(ArcRun arc, int slot, int pc) => arc.Trace.Where(t => t.Slot == slot && t.Pc == pc).Select(t => t.Frame).ToList();

    /// <summary>The pcs that ran in the slot during the tick <paramref name="tick"/> (the frame of the trace), in order.</summary>
    public static int[] Pcs(ArcRun arc, int slot, int tick) => arc.Trace.Where(t => t.Slot == slot && t.Frame == tick).Select(t => t.Pc).ToArray();

    /// <summary>Runs the arc through the tick <paramref name="tick"/> (the trace frame <paramref name="tick"/> has run when this returns).</summary>
    public static void RunThrough(ArcRun arc, int tick)
    {
        while (arc.Frame <= tick)
        {
            arc.OneFrame();
        }
    }

    /// <summary>The first frame at or after <paramref name="fromFrame"/> at which (slot, pc) ran, or -1.</summary>
    public static int FirstFrameAtOrAfter(ArcRun arc, int slot, int pc, int fromFrame)
    {
        foreach (var t in arc.Trace)
        {
            if (t.Slot == slot && t.Pc == pc && t.Frame >= fromFrame)
            {
                return t.Frame;
            }
        }

        return -1;
    }

    /// <summary>Runs frames until the arc's own frame counter reaches <paramref name="frame"/>.</summary>
    public static void RunTo(ArcRun arc, int frame)
    {
        while (arc.Frame < frame)
        {
            arc.OneFrame();
        }
    }

    public static bool Temp(uint n) => ArcChecks.IsTemporarySet(n);

    public static bool ClassA(AlundraEntityScriptProxy e) => (e.Flags & EntityFlags.ClassA) != 0;

    public static bool Locked() => (AlundraGameState.Instance.PlayerControlFlags & AlundraGameState.PlayerControlBits.ControlLocked) != 0;

    /// <summary>The hero stands free and where the arc put it: on the ground, nothing locks the pad, Idle.</summary>
    /// <summary>The tuples (walkability, height, ground property, floor tile) of the cells.</summary>
    public static void AssertTuples(IReadOnlyList<LeverCell> cells, params (int Walkability, int Height, int GroundProperty, int Tile)[] expected)
    {
        Assert.Equal(expected, cells.Select(c => (c.Walkability, c.Height, c.GroundProperty, c.FloorTile)).ToArray());
    }

    public static void AssertHeroReady(ArcRun arc, int x, int y, int z)
    {
        Assert.Equal(1, arc.Hero.IsOnGround);
        Assert.Equal(0u, AlundraGameState.Instance.PlayerControlFlags);
        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        Assert.Equal((x, y, z), (arc.Hero.PosX >> 16, arc.Hero.PosY >> 16, arc.Hero.PosZ >> 16));
    }
}
