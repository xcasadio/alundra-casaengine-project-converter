#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests;

/// <summary>One entity of a row of an E20.c1 hit-oracle trace (<c>docs/plan-e20-annexe/e20-c1/oracle/out/&lt;name&gt;.json</c>): the integer columns of the binary's entity, by name (<c>anim_target</c>,
/// <c>anim_flags_B4</c>, <c>frame_box_1D4</c>, <c>boff_x</c>, <c>bsize_x</c>, <c>atk_attr</c>, <c>c1cc</c>, <c>c1d0</c>, <c>hit_counter</c>, <c>touching_slot</c>...). A null cell reads as -1.</summary>
internal sealed class HitOracleEntity
{
    private readonly Dictionary<string, long> _values = new();

    public HitOracleEntity(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            _values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.GetInt64(),
                JsonValueKind.True => 1,
                _ => -1,
            };
        }
    }

    public long this[string name] => _values.TryGetValue(name, out var value) ? value : throw new KeyNotFoundException($"the oracle column '{name}' does not exist");

    /// <summary>True when the entity carries a frame box (<c>+0x1D4</c> is not null).</summary>
    public bool HasBox => this["frame_box_1D4"] != 0;

    /// <summary>The slot of the entity this one is touching (<c>+0x224</c>), -1 for none.</summary>
    public int TouchingSlot => (int)this["touching_slot"];
}

/// <summary>One row of a hit-oracle trace: the state AFTER the passes of one tick of the real code of <c>ALUN_CD.EXE</c> (France): the hero, the targets in slot order, the latch of the hero's animation
/// (<c>0x80146DF8</c>), the sparks the stub of <c>0x8003BDD8</c> was asked for (effect 9) and the program letters (slot, letter: 2 = C, 3 = D) the events pass chose.</summary>
internal sealed record HitOracleRow(
    int Tick, uint Hold, uint Latch, HitOracleEntity Hero, IReadOnlyList<HitOracleEntity> Targets, IReadOnlyList<(int Id, int X, int Y, int Z)> Sparks, IReadOnlyList<(int Slot, int Letter)> Handlers)
{
    public HitOracleEntity Entity(int index) => index == 0 ? Hero : Targets[index - 1];
}

/// <summary>A hit-oracle trace (<c>H1</c>, <c>H3</c>...): the start state and one row per tick, as <c>run_c1.py</c> writes them.</summary>
internal sealed record HitOracleTrace(string Name, HitOracleRow Start, IReadOnlyList<HitOracleRow> Rows, IReadOnlyDictionary<int, uint> Holds, int HeroPixelX, int HeroPixelY, IReadOnlyList<int[]> TargetPixels)
{
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c1", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c1/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the hit oracle traces");
    }

    public static HitOracleTrace Load(string name)
    {
        var path = Path.Combine(OutputFolder(), name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the hit oracle trace '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var spec = root.GetProperty("spec");
        var holds = new Dictionary<int, uint>();
        foreach (var hold in spec.GetProperty("holds").EnumerateObject())
        {
            holds[int.Parse(hold.Name, System.Globalization.CultureInfo.InvariantCulture)] = hold.Value.GetUInt32();
        }

        var meta = root.GetProperty("meta");
        var heroPixels = meta.GetProperty("hero_start_px");
        var targets = meta.GetProperty("targets").EnumerateArray().Select(t => t.GetProperty("px").EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToList();
        return new HitOracleTrace(
            name, ReadRow(root.GetProperty("pre"), 0), root.GetProperty("rows").EnumerateArray().Select(r => ReadRow(r, r.GetProperty("tick").GetInt32())).ToList(), holds,
            heroPixels[0].GetInt32(), heroPixels[1].GetInt32(), targets);
    }

    private static HitOracleRow ReadRow(JsonElement e, int tick)
    {
        var sparks = new List<(int Id, int X, int Y, int Z)>();
        var handlers = new List<(int Slot, int Letter)>();
        if (e.TryGetProperty("journal_raw", out var journal))
        {
            foreach (var entry in journal.EnumerateArray())
            {
                var kind = entry[0].GetString();
                var args = entry[1];
                if (kind == "effect_3BDD8" && args[1].GetInt32() == 9)
                {
                    sparks.Add((9, args[3].GetInt32(), args[4].GetInt32(), args[5].GetInt32()));
                }
                else if (kind == "handler")
                {
                    handlers.Add((args[0].GetInt32(), args[1].GetInt32()));
                }
            }
        }

        return new HitOracleRow(
            tick, e.GetProperty("hold").GetUInt32(), e.GetProperty("latch_80146DF8").GetUInt32(), new HitOracleEntity(e.GetProperty("hero")),
            e.GetProperty("targets").EnumerateArray().Select(t => new HitOracleEntity(t)).ToList(), sparks, handlers);
    }
}
