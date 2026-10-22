#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Hero;

/// <summary>The probes of the flail bounce and the callers of their sounds.</summary>
internal static class FlailProbe
{
    public const string CreatePlayerAnimationEffects = "CreatePlayerAnimationEffects";
    public const string UpdateAnimation = "UpdateAnimation";

    private static readonly Regex RowLine = new(
        @"^t\s*(\d+) hold=([0-9A-F]+) anim_t=([0-9A-F]+) anim_c=([0-9A-F]+) fx=\s*(-?\d+) fy=\s*(-?\d+) fz=\s*(-?\d+) pz=\s*(-?\d+) gnd=(-?\d+) chg=\s*(-?\d+) ptr=([0-9A-F]+) cnt=\s*(-?\d+) hc=(\d+)\s*(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex SoundCall = new(@"sound\((\d+)\)", RegexOptions.Compiled);

    /// <summary>The folder of the probe outputs, found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f2", "oracle", "f-weapons", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-f2/oracle/f-weapons/out' directory found above '{AppContext.BaseDirectory}': this test needs the flail probes");
    }

    /// <summary>Loads <c>&lt;name&gt;.txt</c> (such as <c>F-iron-air</c>): one row per tick, in order.</summary>
    public static IReadOnlyList<FlailProbeRow> Load(string name)
    {
        var path = Path.Combine(OutputFolder(), name + ".txt");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the flail probe '{name}' is missing", path);
        }

        var rows = new List<FlailProbeRow>();
        foreach (var line in File.ReadAllLines(path).Where(l => l.StartsWith("t", StringComparison.Ordinal)))
        {
            var m = RowLine.Match(line);
            if (!m.Success)
            {
                throw new InvalidDataException($"{name}: a row of the probe does not parse: {line}");
            }

            var ids = SoundCall.Matches(m.Groups[14].Value).Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            rows.Add(new FlailProbeRow(
                Tick: int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                Hold: uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                AnimTarget: uint.Parse(m.Groups[3].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                AnimCurrent: uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                ForceX: int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                ForceY: int.Parse(m.Groups[6].Value, CultureInfo.InvariantCulture),
                ForceZ: int.Parse(m.Groups[7].Value, CultureInfo.InvariantCulture),
                PosZDll: int.Parse(m.Groups[8].Value, CultureInfo.InvariantCulture),
                OnGround: int.Parse(m.Groups[9].Value, CultureInfo.InvariantCulture),
                RecPtr: long.Parse(m.Groups[11].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                RecCnt: int.Parse(m.Groups[12].Value, CultureInfo.InvariantCulture),
                Sounds: ids.Where(id => id > 0).ToArray(),
                ZeroSounds: ids.Count(id => id == 0),
                HasLandingEffect: m.Groups[14].Value.Contains("effect_3BDD8", StringComparison.Ordinal)));
        }

        if (rows.Count == 0)
        {
            throw new InvalidDataException($"{name}: the probe has no row");
        }

        return rows;
    }

    /// <summary>The calls of <c>F-sound-callers.txt</c> for <paramref name="probe"/>.</summary>
    public static IReadOnlyList<FlailCall> Callers(string probe)
    {
        var path = Path.Combine(OutputFolder(), "F-sound-callers.txt");
        return File.ReadAllLines(path)
            .Where(l => l.StartsWith(probe + " ", StringComparison.Ordinal))
            .Select(l => l.Split(' '))
            .Select(p => new FlailCall(p[0], int.Parse(p[1], CultureInfo.InvariantCulture), p[2], int.Parse(p[3], CultureInfo.InvariantCulture), p[5]))
            .ToList();
    }
}
