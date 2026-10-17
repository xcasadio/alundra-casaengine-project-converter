#nullable enable
using System;
using System.Diagnostics;
using System.IO;

namespace Alundra.Probes;

/// <summary>
/// E20.e0a2 phase A (docs/plan-e20-heros.md section 1.16.2, T0): the shadow channel that predicts what the sorted insertion and the cap of phase B would do, without doing it. Active only when the environment variable
/// <see cref="EnvVar"/> names a file; absent, nothing is written and nothing is allocated. One line per event, <c>&lt;event&gt; test=&lt;Type.Method&gt; world=&lt;name&gt; slot=&lt;s&gt; maxLive=&lt;m&gt;</c>, the test being the
/// outermost frame of the stack in the <c>Alundra.Tests</c> namespace (the suites run without parallelism, and an apparition is always made in the call stack of its test).
/// </summary>
internal static class SlotShadow
{
    internal const string EnvVar = "ALUNDRA_SLOT_SHADOW_LOG";

    private static readonly string? Path = Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } p ? p : null;

    private static readonly object Gate = new();

    /// <summary>An apparition whose slot is lower than the highest slot of the live entries: phase B would insert it somewhere else than at the end of the list.</summary>
    internal const string OutOfOrder = "out-of-order";

    /// <summary>An apparition with no free slot from 1 to 63: phase B would refuse it.</summary>
    internal const string Overflow = "overflow";

    /// <summary>The highest slot seen, reported for every assignment (so the register of a test holds its peak).</summary>
    internal const string Seen = "seen";

    internal static bool Active => Path != null;

    internal static void Report(string kind, string? worldName, int slot, int maxLive)
    {
        if (Path == null)
        {
            return;
        }

        var test = "?";
        foreach (var frame in new StackTrace().GetFrames())
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

        lock (Gate)
        {
            File.AppendAllText(Path, $"{kind} test={test} world={worldName ?? "?"} slot={slot} maxLive={maxLive}{Environment.NewLine}");
        }
    }
}
