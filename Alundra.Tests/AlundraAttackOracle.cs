#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests;

/// <summary>One row of an E20.b attack-oracle trace: the state AFTER the passes of one logic tick of the real <c>MovePlayer</c> of <c>ALUN_CD.EXE</c> (France) in the D1 oracle.</summary>
/// <param name="Tick">The 1-based logic tick (0 for the start state).</param>
/// <param name="Hold">The pad word held on that tick (the tick pad derives its edges from it).</param>
/// <param name="PosZDll">PosZ in the DLL convention (the binary's value minus the +1 of its rest).</param>
/// <param name="RecPtr">The pending-spawn pointer <c>0x80127110</c> (an address of the executable), 0 when none.</param>
/// <param name="RecCnt">The counter <c>0x80127114</c>.</param>
/// <param name="Sounds">The sounds asked on the tick (<c>sound(N)</c> with N &gt; 0: a call with 0 is a call of the function, not a sound, and the DLL asks none).</param>
/// <param name="Spawns">The number of spawn calls (<c>0x80039F58</c>) journalled on the tick.</param>
internal sealed record AttackOracleRow(
    int Tick, uint Hold, uint AnimTarget, uint AnimCur, int DirTarget, int ForceX, int ForceY, int ForceZ, int PosX, int PosY, int PosZDll, int OnGround,
    long RecPtr, int RecCnt, int[] Sounds, int Spawns);

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
