#nullable enable
using System;
using System.Globalization;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// E20.d2a S3 (D-E20-32): the stick probe. When the environment variable <see cref="EnvVar"/> is <c>1</c> at process start, the DLL journals, through the engine's logs (<c>log.txt</c> of the game's working folder, rewritten
/// at each launch), the stick sample of every rendered frame and, at each read of the run's start table, the nibble it uses. Nothing is journalled otherwise, and nothing about the hero's behaviour depends on it: the run
/// keeps the OR of the keyboard, d-pad and stick bits (E20.d2b decides the rule from the author's measure).
/// <para>Lines: <c>StickProbe x y D S</c> once per frame (x, y signed, invariant culture, 4 decimals, x positive to the right and y positive upward; D = digital direction bits, S = stick direction bits, hex of the
/// <see cref="AlundraPadState"/> bits: Up 1000, Right 2000, Down 4000, Left 8000), and <c>StickProbe start S N</c> at each read of the table (S = the state, 29 for the start in 0x29, 2A for the resume in 0x2A from the
/// eleventh tick on; N = the direction nibble, <c>ButtonsHold &gt;&gt; 12</c>, the OR, in hex).</para>
/// </summary>
internal static class AlundraStickProbe
{
    /// <summary>The environment variable that turns the probe on (the value <c>1</c>).</summary>
    internal const string EnvVar = "ALUNDRA_DEBUG_STICK_PROBE";

    private static readonly bool ActiveFromEnvironment = ReadFromEnvironment();

    /// <summary>Test-only seam: null (the default) defers to the environment read at the first use; a non-null value overrides it. Per thread, so tests running in parallel do not see each other's.</summary>
    [ThreadStatic]
    private static bool? _overrideForTests;

    /// <summary>Test-only seam: when non-null, the lines go to this sink instead of the engine's logs. Per thread.</summary>
    [ThreadStatic]
    private static Action<string>? _sinkForTests;

    internal static bool IsActive => _overrideForTests ?? ActiveFromEnvironment;

    private static bool ReadFromEnvironment()
    {
        var active = Environment.GetEnvironmentVariable(EnvVar) == "1";
        if (active)
        {
            Logs.WriteWarning($"AlundraStickProbe: {EnvVar}=1 - the stick sample is journalled every frame (debug-only, never active by default).");
        }

        return active;
    }

    /// <summary>Test-only: forces the probe on or off (null restores the environment) and redirects the lines to <paramref name="sink"/> (null restores the logs).</summary>
    internal static void SetForTests(bool? active, Action<string>? sink)
    {
        _overrideForTests = active;
        _sinkForTests = sink;
    }

    /// <summary>The per-frame line, when the probe is active.</summary>
    internal static void LogFrame(in AlundraPadState sample)
    {
        if (!IsActive)
        {
            return;
        }

        Write(string.Create(CultureInfo.InvariantCulture, $"StickProbe {sample.StickX:F4} {sample.StickY:F4} {sample.DigitalDirectionBitsHold:X} {sample.StickBitsHold:X}"));
    }

    /// <summary>The line of one read of the run's start table, when the probe is active.</summary>
    internal static void LogStartTableRead(uint state, uint nibble)
    {
        if (!IsActive)
        {
            return;
        }

        Write(string.Create(CultureInfo.InvariantCulture, $"StickProbe start {state:X} {nibble:X}"));
    }

    private static void Write(string line)
    {
        if (_sinkForTests != null)
        {
            _sinkForTests(line);
            return;
        }

        Logs.WriteInfo(line);
    }
}
