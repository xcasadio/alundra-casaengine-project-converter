#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d: the checks the chain arcs of E19.d share (A5, A6, A8). Same conventions as the arcs of E19.c1 (docs/plan-e19-opcodes.md
/// §1.3): absolute frames within +/- 3, written gaps exact, the end signal first, then the skipped or exceeded set, then the rest.
/// </summary>
internal static class ArcChecks
{
    /// <summary>The frame of the first execution of (slot, pc), whatever its opcode (a suspended instruction counts from its first call).</summary>
    public static int FrameOf(ArcRun arc, int slot, int pc)
    {
        foreach (var t in arc.Trace)
        {
            if (t.Slot == slot && t.Pc == pc)
            {
                return t.Frame;
            }
        }

        Assert.Fail($"(slot {slot}) @{pc} never ran");
        return -1;
    }

    /// <summary>Every frame in which (slot, pc, opcode) ran, in order (one entry per execution).</summary>
    public static List<int> FramesOf(ArcRun arc, int slot, int pc, int opcode) =>
        arc.Trace.Where(t => t.Slot == slot && t.Pc == pc && t.Opcode == opcode).Select(t => t.Frame).ToList();

    /// <summary>Absolute frame of (slot, pc) within +/- 3.</summary>
    public static void AssertFrame(ArcRun arc, int slot, int pc, int expected)
    {
        var frame = FrameOf(arc, slot, pc);
        Assert.True(Math.Abs(frame - expected) <= 3, $"(slot {slot}) @{pc} first ran at frame {frame}, {expected} (+/- 3) expected");
    }

    /// <summary>Exact gap, in frames, between the first executions of two pcs of one slot.</summary>
    public static void AssertGap(ArcRun arc, int slot, int fromPc, int toPc, int expected)
    {
        var gap = FrameOf(arc, slot, toPc) - FrameOf(arc, slot, fromPc);
        Assert.True(gap == expected, $"@{toPc} - @{fromPc} = {gap} frames, {expected} expected");
    }

    /// <summary>The skipped or exceeded instructions must all be in <paramref name="allowed"/> ((opcode, pc) pairs); a
    /// <c>LoopBudgetExceeded</c> is never allowed.</summary>
    public static void AssertSkippedWithin(ArcRun arc, ISet<(int Opcode, int Pc)> allowed)
    {
        var offenders = arc.SkippedOrExceeded
            .Where(t => t.Kind == EventTraceKind.LoopBudgetExceeded || !allowed.Contains((t.Opcode, t.Pc)))
            .Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})").Distinct().ToList();
        Assert.True(offenders.Count == 0, "skipped or exceeded instructions out of the expected set: " + string.Join("; ", offenders));
    }

    public static void AssertNothingSkippedOrExceeded(ArcRun arc) => AssertSkippedWithin(arc, new HashSet<(int, int)>());

    public static void AssertNoUnexpectedError(ArcRun arc)
    {
        var unexpected = arc.Log!.Errors
            .Where(e => !e.StartsWith("AnimatedSpriteComponent : can't resolve sprite", StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, "errors logged: " + string.Join(" | ", unexpected.Take(3)));
        Assert.DoesNotContain(arc.Log.Warnings, m => m.Contains("falling back to a bare entity"));
    }

    /// <summary>Whether the persistent flag <paramref name="flag"/> (<c>G866</c> is 866) is set.</summary>
    public static bool IsSet(uint flag) => (ArcRun.State.GetFlag(flag) & (1u << (int)(flag & 0x1f))) != 0;

    /// <summary>Whether the temporary flag <c>T</c><paramref name="number"/> is set (the session flags start at 0x8000).</summary>
    public static bool IsTemporarySet(uint number) => IsSet(0x8000u + number);
}
