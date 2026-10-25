#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f6a T1 (docs/plan-e20-heros.md section 1.22.1): the keys of the fire and ice balls (sprite types 10 to 15) of the oracle <c>docs/plan-e20-annexe/e20-f4f6/</c> read BY KEY, never copied: the frozen keys <c>bin/out/json/fire11_*.json</c>, <c>ice14_*.json</c>, and the keys of E20.f6a
/// <c>f6a/oracle/out/json/*.json</c>. The oracle presses Square at the tick P = 1 and the ball is born at S = P + 11 = 12; the bench of the tests (<see cref="BallRun"/>) does the same, so a tick of the oracle is a tick of the test. The Z of the DLL is the oracle's minus one raw unit (ADR-0026).
/// </summary>
internal sealed class BallOracle
{
    /// <summary>The tick of the birth of the ball (the delay 10 of the fiches w4 and w5, plus the tick of the press, plus one).</summary>
    internal const int Birth = 12;

    /// <summary>The unit of raw Z that the binary's Z has over the DLL's (ADR-0026).</summary>
    internal const int RawZUnit = 1;

    private readonly List<JsonElement> _rows;

    /// <summary>The slot (as the key names it) that holds the ball, and the tick of its first row.</summary>
    internal string BallSlot { get; }

    internal int FirstTick { get; }

    private BallOracle(List<JsonElement> rows, string ballSlot, int firstTick)
    {
        _rows = rows;
        BallSlot = ballSlot;
        FirstTick = firstTick;
    }

    private static string? TryAnnex(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f4f6", relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static readonly Regex FxPattern = new(@"^fx\(id=(?<id>\d+),dir=(?<dir>-?\d+),x=(?<x>-?[\d.]+),y=(?<y>-?[\d.]+),z=(?<z>-?[\d.]+),v=\((?<vx>-?[\d.]+),(?<vy>-?[\d.]+),(?<vz>-?[\d.]+)\)\)$", RegexOptions.CultureInvariant);

    /// <summary>One effect of the oracle with the numbers as the oracle prints them (pixels with 3 decimals, speeds with 4).</summary>
    internal sealed record Effect(int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz)
    {
        /// <summary>The line the tests compare, with the tick given (the tick of the oracle, or the tick of the end of the frame).</summary>
        internal string Line(int tick) => $"t{tick} id={Id} dir={Direction} x={X} y={Y} z={Z} v=({Vx},{Vy},{Vz})";

        /// <summary>The line without the speed (the position only).</summary>
        internal string PositionLine(int tick) => $"t{tick} id={Id} dir={Direction} x={X} y={Y} z={Z}";
    }

    /// <summary>The key <paramref name="name"/> (<c>fire11_gp8_deepwater</c>, ...): the key of E20.f6b (<c>f6b/oracle/out/json</c>) when the name is one of its new scenarios, the key of E20.f6a (<c>f6a/oracle/out/json</c>) when it is one of its scenarios, otherwise the frozen one (<c>bin/out/json</c>).</summary>
    internal static BallOracle Load(string name) => Load(name, 10, 15);

    /// <summary>
    /// The key <paramref name="name"/> with the step of the generator that ends every tick of the binary (<c>f6b/oracle/out/json/{name}_pf.json</c>, "Revision au HEAD" point 3): the keys whose generator the tests follow. The plan reads them at ONE tick per frame.
    /// </summary>
    internal static BallOracle LoadStepByImage(string name) => Load(name + "_pf", 10, 15);

    /// <summary>The key of a charged ball (<paramref name="name"/> is <c>fire12_flat</c>, ...): the ball of the key is the charged one (the type 12 or 15), not the normal ball of the start of the hold (type 11 or 14) that the same key holds from the tick 12.</summary>
    internal static BallOracle LoadCharged(string name, bool stepByImage = true) => Load(stepByImage ? name + "_pf" : name, 12, 15, onlyChargedTypes: true);

    private static BallOracle Load(string name, int firstType, int lastType, bool onlyChargedTypes = false)
    {
        var path = TryAnnex($"f6b/oracle/out/json/{name}.json") ?? TryAnnex($"f6a/oracle/out/json/{name}.json") ?? TryAnnex($"bin/out/json/{name}.json")
            ?? throw new InvalidOperationException($"no key '{name}' found above '{AppContext.BaseDirectory}'");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var rows = document.RootElement.EnumerateArray().Select(r => r.Clone()).ToList();
        foreach (var row in rows)
        {
            foreach (var slot in row.GetProperty("slots").EnumerateObject())
            {
                var type = slot.Value.GetProperty("type68").GetInt32();
                if (type >= firstType && type <= lastType && (!onlyChargedTypes || type is 12 or 15))
                {
                    return new BallOracle(rows, slot.Name, row.GetProperty("tick").GetInt32());
                }
            }
        }

        throw new InvalidOperationException($"the key {name} holds no ball");
    }

    internal int Ticks => _rows.Count;

    internal JsonElement Row(int tick) => _rows[tick - 1];

    /// <summary>The slot of the ball at <paramref name="tick"/>, or null when the ball is not in the table.</summary>
    internal JsonElement? Ball(int tick) => Row(tick).GetProperty("slots").TryGetProperty(BallSlot, out var element) && element.ValueKind == JsonValueKind.Object ? element : null;

    /// <summary>The value of the column <paramref name="key"/> of the ball at <paramref name="tick"/>.</summary>
    internal int Value(int tick, string key) => Ball(tick)!.Value.GetProperty(key).GetInt32();

    /// <summary>The line of the ball as the tests compare it, in the frame of the DLL (the Z minus one raw unit): the status, the X, the Z, the slope (<c>+0x18C</c>) and the ground flag (<c>coll_140</c>); or "absent".</summary>
    internal string Line(int tick)
        => Ball(tick) is null
            ? "absent"
            : string.Create(CultureInfo.InvariantCulture, $"st={Value(tick, "status")} x={Value(tick, "px")} z={Value(tick, "pz") - RawZUnit} slope={Value(tick, "slope_18C")} grounded={Value(tick, "coll_140")}");

    /// <summary>The draws of the tick (the key does not count the step of the end of the frame).</summary>
    internal int Draws(int tick) => Row(tick).GetProperty("draws").GetInt32();

    /// <summary>The effects the key creates with an attached form (<c>attach(id=N,...)</c>), as (tick, id).</summary>
    internal IReadOnlyList<(int Tick, int Id)> Attached()
        => _rows.SelectMany(row => row.GetProperty("effects").EnumerateArray().Select(e => (Tick: row.GetProperty("tick").GetInt32(), Text: e.GetString()!)))
            .Where(x => x.Text.StartsWith("attach(id=", StringComparison.Ordinal))
            .Select(x => (x.Tick, Id: int.Parse(x.Text["attach(id=".Length..x.Text.IndexOf(',')], CultureInfo.InvariantCulture))).ToList();

    /// <summary>The first tick at which the ball stands on the ground (<c>coll_140</c> is 1): S+10 on a flat field, S+7 on the step of 16 px.</summary>
    internal int LandingTick()
        => Enumerable.Range(FirstTick, Ticks - FirstTick + 1).First(t => Ball(t) is not null && Value(t, "coll_140") == 1);

    /// <summary>The last tick at which the ball is in the table; it is absent from the next one.</summary>
    internal int LastTickPresent()
        => Enumerable.Range(FirstTick, Ticks - FirstTick + 1).Where(t => Ball(t) is not null).DefaultIfEmpty(-1).Max();

    /// <summary>The generator word at the head of the tick (<c>rng0</c>).</summary>
    internal uint Rng0(int tick) => Row(tick).GetProperty("rng0").GetUInt32();

    /// <summary>The raw texts of the effects of the tick, in the order the machine created them (<c>fx(...)</c> and <c>attach(...)</c>).</summary>
    internal IReadOnlyList<string> EffectTexts(int tick) => Row(tick).GetProperty("effects").EnumerateArray().Select(e => e.GetString()!).ToList();

    /// <summary>The effects <c>fx(id=...)</c> of the key with the identifier <paramref name="id"/>, in order, with the tick of the oracle.</summary>
    internal IReadOnlyList<Effect> Effects(int id)
    {
        var list = new List<Effect>();
        foreach (var row in _rows)
        {
            var tick = row.GetProperty("tick").GetInt32();
            foreach (var text in row.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!))
            {
                var m = FxPattern.Match(text);
                if (m.Success && int.Parse(m.Groups["id"].Value, CultureInfo.InvariantCulture) == id)
                {
                    list.Add(new Effect(tick, id, int.Parse(m.Groups["dir"].Value, CultureInfo.InvariantCulture), m.Groups["x"].Value, m.Groups["y"].Value, m.Groups["z"].Value, m.Groups["vx"].Value, m.Groups["vy"].Value, m.Groups["vz"].Value));
                }
            }
        }

        return list;
    }

    /// <summary>
    /// The sounds of the key (<c>snd(N)</c> of the journal) from the tick <paramref name="from"/> to <paramref name="to"/>, in order, WITHOUT the identifier 0 (no sound: the machine journals a <c>snd(0)</c> at some absolute ticks of every key whatever the ball does).
    /// </summary>
    internal IReadOnlyList<(int Tick, int Sfx)> Sounds(int from, int to)
        => _rows.Where(r => r.GetProperty("tick").GetInt32() >= from && r.GetProperty("tick").GetInt32() <= to)
            .SelectMany(r => r.GetProperty("journal").EnumerateArray().Select(j => (Tick: r.GetProperty("tick").GetInt32(), Text: j.GetString()!)))
            .Where(j => j.Text.StartsWith("snd(", StringComparison.Ordinal) && j.Text != "snd(0)")
            .Select(j => (j.Tick, int.Parse(j.Text[4..^1], CultureInfo.InvariantCulture))).ToList();

    /// <summary>The generator word after the draws of the tick and before the step that ends it (<c>rng1</c>).</summary>
    internal uint Rng1(int tick) => Row(tick).GetProperty("rng1").GetUInt32();

    /// <summary>The raw value of the column <paramref name="key"/> of the ball at <paramref name="tick"/> (the key's units: the binary's raw Z).</summary>
    internal int Raw(int tick, string key) => Value(tick, key);

    /// <summary>The frame box of the ball at <paramref name="tick"/> as <see cref="BallRun"/> prints it: "WxDxH hbz=Z" in pixels (the raw sizes of the key are the size minus one raw unit).</summary>
    internal string BoxLine(int tick)
        => string.Create(CultureInfo.InvariantCulture, $"{(Value(tick, "bw") + 1) >> 16}x{(Value(tick, "bd") + 1) >> 16}x{(Value(tick, "bh") + 1) >> 16} hbz={Value(tick, "hbz") >> 16}");
}
