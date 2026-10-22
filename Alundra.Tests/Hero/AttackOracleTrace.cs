#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Hero;

/// <summary>An E20.b oracle trace (<c>docs/plan-e20-annexe/e20-b/oracle/out/*.trace.json</c>): the state before tick 1 and one row per tick.</summary>
internal sealed record AttackOracleTrace(string Name, AttackOracleRow Start, IReadOnlyList<AttackOracleRow> Rows, int? WeaponItemNow)
{
    private static readonly Regex SoundCall = new(@"^sound\((\d+)\)$", RegexOptions.Compiled);

    /// <summary>The folder of the oracle outputs, found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-b", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-b/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the attack oracle traces");
    }

    /// <summary>Loads <c>&lt;name&gt;.trace.json</c> (a name such as <c>AT1</c> or <c>at9/AT9g-item05</c>).</summary>
    public static AttackOracleTrace Load(string name)
    {
        var path = Path.Combine(OutputFolder(), name + ".trace.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the oracle trace '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        int? weaponItem = root.GetProperty("meta").TryGetProperty("weapon_item_now", out var item) ? item.GetInt32() : null;
        return new AttackOracleTrace(name, Row(root.GetProperty("start")), root.GetProperty("rows").EnumerateArray().Select(Row).ToList(), weaponItem);
    }

    private static AttackOracleRow Row(JsonElement e)
    {
        var events = e.GetProperty("events").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
        var sounds = events
            .Select(x => SoundCall.Match(x))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Where(id => id > 0)
            .ToArray();
        return new AttackOracleRow(
            e.GetProperty("tick").GetInt32(), e.GetProperty("hold").GetUInt32(), e.GetProperty("anim_target").GetUInt32(), e.GetProperty("anim_cur").GetUInt32(),
            e.GetProperty("dir_target").GetInt32(), e.GetProperty("force_x").GetInt32(), e.GetProperty("force_y").GetInt32(), e.GetProperty("force_z").GetInt32(),
            e.GetProperty("pos_x").GetInt32(), e.GetProperty("pos_y").GetInt32(), e.GetProperty("pos_z_dll").GetInt32(), e.GetProperty("on_ground").GetInt32(),
            e.GetProperty("rec_ptr").GetInt64(), e.GetProperty("rec_cnt").GetInt32(), sounds, events.Count(x => x.StartsWith("spawn(", StringComparison.Ordinal)));
    }
}
