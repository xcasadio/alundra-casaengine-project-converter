#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Rng;

namespace Alundra.Probes;

/// <summary>
/// E20.e0b1 (D-E0B-7, docs/plan-e20-annexe/e20-e0b/README.md): the measuring sink of the per-tick advance of the shared random stream (<see cref="GameRandom"/>), a measuring channel like
/// <see cref="AlundraSlotShadow"/> (ADR-0064 precedent): production code that does NOTHING unless <see cref="EnvVar"/> names a file (read once; absent, no file is written, nothing is allocated and
/// neither the virtual word nor a counter moves). It never touches the real state of the stream: it only watches it.
/// <para>
/// <b>Predicate.</b> <c>R</c> is the real state (what the stream holds), <c>V</c> what it would hold with the advance (phase B). An assignment of <see cref="GameRandom.RandomSeed"/> or a
/// <see cref="GameRandom.Reset"/> opens an epoch (<c>V := (uint)value</c>); each <see cref="GameRandom.AdvanceFrame"/> makes <c>V := step(V)</c>; each real draw makes <c>R := step(R)</c> and
/// <c>V := step(V)</c> and is DIVERGENT when <c>V != (uint)R</c> afterwards (the step is a bijection modulo 2^32, the multiplier being odd: this is exactly "the draw would return another number in phase B");
/// a read of <see cref="GameRandom.RandomSeed"/> by a test is divergent under the same condition. Each event is attributed to the OUTERMOST frame of the <c>Alundra.Tests</c> namespace of the stack at the
/// event (the suites run without parallelism). Draws attributed to a test before any epoch of that test are AMBIENT (a state inherited from the order of the tests).
/// </para>
/// One line per attributed test, written when the attributed test changes and at the exit of the process:
/// <c>test=&lt;Type.Method&gt; virtual_advances=&lt;n&gt; draws=&lt;n&gt; divergent_draws=&lt;n&gt; first_divergent=&lt;category:Type.Method|-&gt; divergent_seed_reads=&lt;n&gt; epochs=&lt;n&gt; ambient_draws=&lt;n&gt;</c>.
/// </summary>
internal static class AlundraRandomShadow
{
    internal const string EnvVar = "ALUNDRA_RNG_SHADOW_LOG";

    private const ulong Multiplier = 0x7d2b89dd;
    private const ulong Increment = 0xe06a02e7;

    private static readonly string? Path = Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } p ? p : null;

    private static readonly object Gate = new();

    /// <summary>True when <see cref="EnvVar"/> names a file: the sink is measuring.</summary>
    internal static bool Active => Path != null;

    /// <summary>The virtual word <c>V</c>: what the real state would be with the advance. Untouched while the sink is inactive.</summary>
    internal static uint VirtualWord { get; private set; } = (uint)GameRandom.InitialSeed;

    /// <summary>Number of events recorded since the start of the process (virtual advances, draws, assignments, reads). Zero while the sink is inactive.</summary>
    internal static long EventCount { get; private set; }

    /// <summary>Number of lines written to the file. Zero while the sink is inactive.</summary>
    internal static long LinesWritten { get; private set; }

    private sealed class Counters
    {
        internal long VirtualAdvances;
        internal long Draws;
        internal long DivergentDraws;
        internal string FirstDivergent = "-";
        internal long DivergentSeedReads;
        internal long Epochs;
        internal long AmbientDraws;
        internal long VirtualRolls;
        internal long VirtualRollSteps;
    }

    private static string? _currentTest;
    private static Counters _counters = new();

    static AlundraRandomShadow()
    {
        if (Path != null)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
        }
    }

    private static uint Step(uint v) => (uint)(v * Multiplier + Increment);

    /// <summary>An assignment of <c>RandomSeed</c> or a <c>Reset()</c>: opens an epoch.</summary>
    internal static void OnEpoch(ulong value)
    {
        if (Path == null)
        {
            return;
        }

        lock (Gate)
        {
            var c = Attribute(out _);
            EventCount++;
            VirtualWord = (uint)value;
            c.Epochs++;
        }
    }

    /// <summary><c>AdvanceFrame()</c>: the virtual step of phase A.</summary>
    internal static void OnVirtualAdvance()
    {
        if (Path == null)
        {
            return;
        }

        lock (Gate)
        {
            var c = Attribute(out _);
            EventCount++;
            VirtualWord = Step(VirtualWord);
            c.VirtualAdvances++;
        }
    }

    /// <summary>
    /// E20.e0b2 phase A: the virtual loot roll of one creation. <paramref name="roll"/> replays <see cref="LootRoll"/> with the draw it is given, which steps the virtual word <c>V</c> (never a copy of the real state)
    /// and returns the steps it made (0, 1 or 2); the real state is not touched. Counted per attributed test as <c>virtual_rolls</c> (creations) and <c>virtual_roll_steps</c>.
    /// </summary>
    internal static void OnVirtualRoll(Func<Func<uint>, int> roll)
    {
        if (Path == null)
        {
            return;
        }

        lock (Gate)
        {
            var c = Attribute(out _);
            EventCount++;
            c.VirtualRolls++;
            c.VirtualRollSteps += roll(() =>
            {
                VirtualWord = Step(VirtualWord);
                return VirtualWord;
            });
        }
    }

    /// <summary>A real draw; <paramref name="realState"/> is the state of the stream AFTER the step.</summary>
    internal static void OnDraw(ulong realState)
    {
        if (Path == null)
        {
            return;
        }

        lock (Gate)
        {
            var c = Attribute(out var stack);
            EventCount++;
            VirtualWord = Step(VirtualWord);
            c.Draws++;
            if (c.Epochs == 0)
            {
                c.AmbientDraws++;
            }

            if (VirtualWord != (uint)realState)
            {
                c.DivergentDraws++;
                if (c.FirstDivergent == "-")
                {
                    c.FirstDivergent = Classify(stack);
                }
            }
        }
    }

    /// <summary>A read of <c>RandomSeed</c> (outside <c>Next()</c>): divergent when the virtual word differs from the low half of the real state.</summary>
    internal static void OnSeedRead(ulong realState)
    {
        if (Path == null)
        {
            return;
        }

        lock (Gate)
        {
            var c = Attribute(out _);
            EventCount++;
            if (VirtualWord != (uint)realState)
            {
                c.DivergentSeedReads++;
            }
        }
    }

    /// <summary>The counters of the test the current event belongs to (the outermost frame of <c>Alundra.Tests</c> of the stack; the previous test is written when it changes).</summary>
    private static Counters Attribute(out StackTrace stack)
    {
        stack = new StackTrace();
        var test = "?";
        foreach (var frame in stack.GetFrames())
        {
            var type = frame.GetMethod()?.DeclaringType;
            if (type?.Namespace != null && type.Namespace.StartsWith("Alundra.Tests", StringComparison.Ordinal))
            {
                var outer = type;
                while (outer.DeclaringType != null)
                {
                    outer = outer.DeclaringType;
                }

                test = outer.Name + "." + frame.GetMethod()!.Name; // the last such frame wins: the outermost.
            }
        }

        if (test != _currentTest)
        {
            WriteCurrent();
            _currentTest = test;
            _counters = new Counters();
        }

        return _counters;
    }

    /// <summary>The first frame under the stream and the sink: <c>category:Type.Method</c>, the category being ai, opcode, effect or other.</summary>
    private static string Classify(StackTrace stack)
    {
        foreach (var frame in stack.GetFrames())
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (type == null || type == typeof(GameRandom) || type == typeof(AlundraRandomShadow))
            {
                continue;
            }

            var name = type.Name;
            var category = name.Contains("EventProgramRunner", StringComparison.Ordinal) ? "opcode"
                : name.Contains("NativeEffect", StringComparison.Ordinal) || name.Contains("EffectStage", StringComparison.Ordinal) ? "effect"
                : name.Contains("Enemy", StringComparison.Ordinal) || name.Contains("Beannoide", StringComparison.Ordinal) || name.Contains("Muruta", StringComparison.Ordinal)
                  || name.Contains("Ai", StringComparison.Ordinal) ? "ai"
                : "other";
            return category + ":" + name + "." + method!.Name;
        }

        return "other:?";
    }

    private static void WriteCurrent()
    {
        if (_currentTest == null)
        {
            return;
        }

        var c = _counters;
        var line = new StringBuilder()
            .Append("test=").Append(_currentTest)
            .Append(" virtual_advances=").Append(c.VirtualAdvances)
            .Append(" draws=").Append(c.Draws)
            .Append(" divergent_draws=").Append(c.DivergentDraws)
            .Append(" first_divergent=").Append(c.FirstDivergent)
            .Append(" divergent_seed_reads=").Append(c.DivergentSeedReads)
            .Append(" epochs=").Append(c.Epochs)
            .Append(" ambient_draws=").Append(c.AmbientDraws)
            .Append(" virtual_rolls=").Append(c.VirtualRolls)
            .Append(" virtual_roll_steps=").Append(c.VirtualRollSteps)
            .Append(Environment.NewLine)
            .ToString();
        File.AppendAllText(Path!, line);
        LinesWritten++;
    }

    private static void Flush()
    {
        lock (Gate)
        {
            WriteCurrent();
            _currentTest = null;
        }
    }
}
