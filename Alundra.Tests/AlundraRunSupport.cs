#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Xunit;

namespace Alundra.Tests;

/// <summary>One rectangle of the cell layout of a run-oracle scenario (inclusive, later rectangles win per attribute; <c>null</c> leaves the attribute as it was).</summary>
internal sealed record RunLayoutRect(int C0, int R0, int C1, int R1, int? HeightByte, int? Walkability, int? GroundProperty);

/// <summary>One item of the journal of a tick of the run oracle: a sound (<paramref name="Id"/>) or an effect created by the binary's <c>0x8003BDD8</c> (<paramref name="Id"/>, position in pixels).</summary>
internal readonly record struct RunJournalItem(string Kind, int Id, double X, double Y, double Z);

/// <summary>
/// E20.d1 T3: a trace of the run oracle (<c>docs/plan-e20-annexe/e20-d/oracle/out/g*/</c>, written by <c>rscen.py</c>: the real code of <c>ALUN_CD.EXE</c> (France) in the MIPS emulator, column-major JSON). Row 0 is the start
/// state, row <c>t</c> the state AFTER the passes of logic tick <c>t</c>; <see cref="Hold"/> at <c>t</c> is the pad word held on that tick.
/// </summary>
internal sealed class RunOracleTrace
{
    private readonly Dictionary<string, long[]> _hero = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<RunJournalItem>> _journal = new();
    private readonly List<Dictionary<string, long[]>> _targets = new();

    public string Name { get; private init; } = string.Empty;

    /// <summary>The number of logic ticks of the trace (rows 1 to <see cref="Ticks"/>).</summary>
    public int Ticks { get; private init; }

    public long[] Hold { get; private init; } = Array.Empty<long>();

    public int HeroPxX { get; private init; }

    public int HeroPxY { get; private init; }

    public string Inventory { get; private init; } = "kit";

    public IReadOnlyList<RunLayoutRect> Layout { get; private init; } = Array.Empty<RunLayoutRect>();

    public int TargetCount { get; private init; }

    public long Col(string column, int tick) => _hero[column][tick];

    /// <summary>A column of the target <paramref name="target"/> (0 for the first one) at <paramref name="tick"/>; a null cell (no entity touched) reads as -1.</summary>
    public long TargetCol(int target, string column, int tick) => _targets[target][column][tick];

    public IReadOnlyList<RunJournalItem> Journal(int tick) => _journal.TryGetValue(tick, out var items) ? items : Array.Empty<RunJournalItem>();

    /// <summary>The folder of the oracle outputs, found above the test binaries; the test fails naming it when it is absent.</summary>
    public static string OutputFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-d", "oracle", "out");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-d/oracle/out' directory found above '{AppContext.BaseDirectory}': this test needs the run oracle traces");
    }

    /// <summary>Every trace of a group folder (<c>g1</c>, ...), as names such as <c>g1/R1-T+D</c>, in order.</summary>
    public static IEnumerable<string> NamesOf(string group)
    {
        var folder = Path.Combine(OutputFolder(), group);
        return Directory.GetFiles(folder, "*.json").Select(f => group + "/" + Path.GetFileNameWithoutExtension(f)).OrderBy(n => n, StringComparer.Ordinal);
    }

    /// <summary>Loads <c>&lt;name&gt;.json</c> (a name such as <c>g1/R1-T+D</c>).</summary>
    public static RunOracleTrace Load(string name)
    {
        var path = Path.Combine(OutputFolder(), name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the run oracle trace '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var spec = root.GetProperty("spec");
        var ticks = root.GetProperty("tick").GetArrayLength() - 1;
        var layout = new List<RunLayoutRect>();
        if (spec.TryGetProperty("layout", out var rects))
        {
            foreach (var r in rects.EnumerateArray())
            {
                layout.Add(new RunLayoutRect(
                    r.GetProperty("c0").GetInt32(), r.GetProperty("r0").GetInt32(), r.GetProperty("c1").GetInt32(), r.GetProperty("r1").GetInt32(),
                    Nullable(r, "height_byte"), Nullable(r, "walk"), Nullable(r, "gprop")));
            }
        }

        var trace = new RunOracleTrace
        {
            Name = name,
            Ticks = ticks,
            Hold = root.GetProperty("hold").EnumerateArray().Select(e => e.GetInt64()).ToArray(),
            HeroPxX = spec.GetProperty("hero_px")[0].GetInt32(),
            HeroPxY = spec.GetProperty("hero_px")[1].GetInt32(),
            Inventory = spec.TryGetProperty("inv", out var inv) ? inv.GetString() ?? "kit" : "kit",
            Layout = layout,
            TargetCount = root.GetProperty("targets").GetArrayLength(),
        };

        foreach (var column in root.GetProperty("hero").EnumerateObject())
        {
            trace._hero[column.Name] = column.Value.EnumerateArray().Select(e => e.GetInt64()).ToArray();
        }

        foreach (var target in root.GetProperty("targets").EnumerateArray())
        {
            var columns = new Dictionary<string, long[]>(StringComparer.Ordinal);
            foreach (var column in target.EnumerateObject())
            {
                columns[column.Name] = column.Value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.Number ? e.GetInt64() : -1).ToArray();
            }

            trace._targets.Add(columns);
        }

        foreach (var entry in root.GetProperty("journal").EnumerateArray())
        {
            var tick = entry[0].GetInt32();
            var items = new List<RunJournalItem>();
            foreach (var item in entry[1].EnumerateArray())
            {
                var kind = item[0].GetString() ?? string.Empty;
                if (kind == "sound")
                {
                    items.Add(new RunJournalItem(kind, item[1][0].GetInt32(), 0, 0, 0));
                }
                else if (kind == "effect")
                {
                    var e = item[1];
                    items.Add(new RunJournalItem(kind, e.GetProperty("id").GetInt32(), e.GetProperty("cx").GetDouble(), e.GetProperty("cy").GetDouble(), e.GetProperty("cz").GetDouble()));
                }
            }

            trace._journal[tick] = items;
        }

        return trace;
    }

    private static int? Nullable(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    /// <summary>The first tick (from <paramref name="from"/>) at which <paramref name="column"/> equals <paramref name="value"/>, or -1.</summary>
    public int FirstTick(string column, long value, int from = 1)
    {
        for (var t = Math.Max(from, 0); t <= Ticks; t++)
        {
            if (Col(column, t) == value)
            {
                return t;
            }
        }

        return -1;
    }

    /// <summary>The cell (walkability, ground property, height byte) the layout gives at (<paramref name="column"/>, <paramref name="row"/>), the rectangles applied in order.</summary>
    public (int Walkability, int GroundProperty, int Height) CellAt(int column, int row)
    {
        int wk = 0, gp = 0, hb = 0;
        foreach (var r in Layout)
        {
            if (column >= r.C0 && column <= r.C1 && row >= r.R0 && row <= r.R1)
            {
                hb = r.HeightByte ?? hb;
                wk = r.Walkability ?? wk;
                gp = r.GroundProperty ?? gp;
            }
        }

        return (wk, gp, hb);
    }
}

/// <summary>
/// E20.d1 T3: the hero montage of a run-oracle trace and its tick-by-tick comparison. The montage is the hero rig of E20.b (<see cref="JumpHeroRig"/>, free pad, the sprite clock and end bridge of
/// <see cref="HeroAttackClock"/> with the run states) on a field of 52 x 60 cells painted from the layout of the trace, with an effect stage over the bank of map 163 (the global effects 9 and 16); the hero starts from the state
/// of row 0, one tick per frame; after each step the harness runs the stage's frame, then does what the counters pass of the world does (<c>FrameCounter++</c>).
/// </summary>
internal sealed class RunMontage
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    public required JumpHeroRig Rig { get; init; }

    public required RecordingSoundPlayer Sounds { get; init; }

    public required AlundraEffectStage Stage { get; init; }

    public required RunOracleTrace Trace { get; init; }

    /// <summary>The effects the stage created on the last tick, as (id, X, Y, Z) in 16.16.</summary>
    public List<(int Id, int X, int Y, int Z)> Created { get; } = new();

    public AlundraEntityScriptProxy Hero => Rig.Hero;

    public int StartX { get; private set; }

    public int StartY { get; private set; }

    private static AlundraItemTables Tables() => AlundraItemTables.GetOrCreate(ProjectRootFinder.Find());

    /// <summary>The field of the trace: 52 x 60 cells of 24 x 16 px, the layout painted (walkability, ground property, height).</summary>
    public static AlundraCellsCollisionField FieldOf(RunOracleTrace trace)
        => trace.Layout.Count == 0
            ? FlatCells.Create(52, 60)
            : FlatCells.Create(52, 60, cell: (c, r) => { var cell = trace.CellAt(c, r); return (cell.Walkability, cell.Height); }, groundProperty: (c, r) => trace.CellAt(c, r).GroundProperty);

    /// <summary>
    /// Builds the montage in the start state of <paramref name="trace"/>: the hero at row 0's position, on its ground, facing as row 0 says, the inventory of the trace (<c>kit</c>, <c>holy</c>: the sacred sword,
    /// <c>b1a</c> and <c>b1b</c>: the boots 0x1A and 0x1B on top of the kit). <paramref name="configure"/> runs on the montage before the settling step.
    /// </summary>
    public static RunMontage Start(RunOracleTrace trace, ICollisionField? field = null, Action<RunMontage>? configure = null)
    {
        var rig = JumpHeroRig.Build(
            field ?? FieldOf(trace), x: trace.Col("pos_x", 0) / 65536f, y: trace.Col("pos_y", 0) / 65536f, z: trace.Col("pos_z_dll", 0) / 65536f, freePad: true, attackClock: true);
        rig.Controller.Settings.WalkabilityMask = AlundraCellsCollisionField.BaseWalkabilityMask; // the LIVE settings of the controller: the montage leaves the mask at 0, the wall cells (0x40) would not block.
        // The Idle of the hero record carries the acceleration byte 1 (`sprite-records.json`, animation 0: 129024 -> 64512 on the tick the slide ends); the sprite clock of the swings keeps the 0 its oracles were built with.
        rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 1 });
        var sounds = new RecordingSoundPlayer();
        rig.Host.SoundPlayer = sounds;

        var tables = Tables();
        rig.Host.ItemTables = tables;
        AlundraPlayerManager.InitializeNewGameInventory(rig.Host.GameState, tables);
        switch (trace.Inventory)
        {
            case "kit":
                break;
            case "holy":
                rig.Host.GameState.NumberOfItems[4 * 2 + 1] = 1;
                AlundraPlayerManager.SetPlayerWeaponId(rig.Host.GameState, tables, tables.ItemsProperties[4 * 5]);
                break;
            case "b1a":
                rig.Host.GameState.NumberOfItems[0x1A * 2 + 1] = 1;
                break;
            case "b1b":
                rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 1;
                break;
            default:
                throw new InvalidOperationException($"{trace.Name}: the inventory '{trace.Inventory}' is not built by the montage");
        }

        var stage = new AlundraEffectStage();
        stage.Load(EffectGolden.BankOf(Root, 163));
        rig.Host.EffectStage = stage;

        var montage = new RunMontage { Rig = rig, Sounds = sounds, Stage = stage, Trace = trace };
        AlundraNativeEffects.CreatedForTests = slot => montage.Created.Add((slot.TargetEffect, slot.X, slot.Y, slot.Z));
        configure?.Invoke(montage);

        var hero = rig.Hero;
        hero.TargetDirection = (uint)trace.Col("dir_target", 0);
        hero.AnimationDirection = (int)trace.Col("facing", 0);
        hero.CurrentAnimationId = ~hero.TargetAnimationId;
        rig.Step(); // the first switch (the sprite takes the animation 0 in the facing) is resolved: nothing pending at tick 1.
        sounds.Requests.Clear();
        montage.Created.Clear();
        hero.FrameCounter = 0;
        hero.HitCounter = 0;
        hero.WeaponChargeTimer = 0;
        hero.StopSprintCounter = 0;
        montage.StartX = hero.PosX;
        montage.StartY = hero.PosY;
        return montage;
    }

    /// <summary>One tick (one frame of <paramref name="ticks"/> logic ticks, 1 by default): the sounds and the effects read are those of this frame alone.</summary>
    public void Tick(uint hold, int ticks = 1)
    {
        Sounds.Requests.Clear();
        Created.Clear();
        Rig.Step(hold, elapsed: 0.02f * ticks);
        Stage.RunFrame(ticks, new bool[ticks]);
        Hero.HitCounter = 0;
        Hero.FrameCounter += ticks;
    }

    /// <summary>What the DLL looks like after one tick: the columns of the oracle, positions relative to the start.</summary>
    public sealed record DllRow(
        int Tick, uint AnimTarget, uint AnimCur, int Facing, uint DirTarget, int ForceX, int ForceY, int ForceZ, int PosX, int PosY, int PosZ, int OnGround, bool ForceAdjusted, int Slope,
        int StopSprintCounter, int ChargeCounter, int AnimCompleteCounter, IReadOnlyList<int> Sounds, IReadOnlyList<(int X, int Y, int Z)> Bounces);

    /// <summary>The rows <see cref="Play"/> recorded, <c>Rows[t]</c> is the state after tick <c>t</c> (row 0 is the start).</summary>
    public List<DllRow> Rows { get; } = new();

    private DllRow Snapshot(int tick) => new(
        tick, Hero.TargetAnimationId, Hero.CurrentAnimationId, Hero.AnimationDirection, Hero.TargetDirection, Hero.ForceX, Hero.ForceY, Hero.ForceZ, Hero.PosX - StartX, Hero.PosY - StartY, Hero.PosZ,
        Hero.IsOnGround, Hero.ForceAdjusted != 0, Hero.Slope_18c, Hero.StopSprintCounter, Hero.WeaponChargeTimer, Hero.AnimCompleteCounter, Sounds.Requests.ToList(),
        Created.Where(c => c.Id == 9).Select(c => (c.X - StartX, c.Y - StartY, c.Z)).ToList());

    /// <summary>The sounds of the run the DLL does not ask yet: the step (12, E20.e) and the landing (11, E20.e).</summary>
    public static bool IsStubbedSound(int id) => id is 11 or 12;

    /// <summary>
    /// Plays the montage over the pad of the trace, one tick per frame, and records <see cref="Rows"/> (row 0 is the start). From the DLL tick <paramref name="shiftFrom"/> on (0: never) the pad of the oracle row
    /// <c>tick + shift</c> is played, so that a DLL which is <paramref name="shift"/> tick(s) EARLY on one event (the bounce against an east wall, O-E19-42) keeps the oracle's alignment afterwards. <paramref name="before"/>
    /// runs before the step of a DLL tick; <paramref name="drive"/> replaces the pad of the trace (the negative control).
    /// </summary>
    public IReadOnlyList<DllRow> Play(int lastTick = int.MaxValue, int shiftFrom = 0, int shift = 0, Action<int>? before = null, Func<int, uint>? drive = null)
    {
        Rows.Clear();
        Rows.Add(Snapshot(0));
        var last = Math.Min(lastTick, Trace.Ticks - (shiftFrom > 0 ? shift : 0));
        for (var tick = 1; tick <= last; tick++)
        {
            before?.Invoke(tick);
            Tick(drive?.Invoke(tick) ?? (uint)Trace.Hold[OracleRow(tick, shiftFrom, shift)]);
            Rows.Add(Snapshot(tick));
        }

        return Rows;
    }

    private static int OracleRow(int tick, int shiftFrom, int shift) => shiftFrom > 0 && tick >= shiftFrom ? tick + shift : tick;

    /// <summary>
    /// Compares the recorded rows with the oracle and returns every difference, as "name tick N field: dll X, oracle Y". Compared: the target and the current animation, the facing, the direction, the three forces, the
    /// three positions (X and Y relative to the start, within <paramref name="positionTolerance"/> units, Z absolute in the DLL convention), <c>IsOnGround</c> (<paramref name="onGroundLag"/> lists the ticks that compare it
    /// with the oracle's value of the tick before, the one-tick representation of AT8 and UH7), the mover's curtailment flag, the slope, the resume counter, the charge counter, the sounds (the step and the landing, E20.e,
    /// are not compared) and the effects 9 of the tick (the bounce: position). <paramref name="ignore"/> drops a (tick, field) pair; <paramref name="shiftFrom"/> and <paramref name="shift"/> are those of <see cref="Play"/>.
    /// </summary>
    public List<string> CompareRows(int lastTick = int.MaxValue, int firstTick = 1, int[]? onGroundLag = null, Func<int, string, bool>? ignore = null, int positionTolerance = 0, int shiftFrom = 0, int shift = 0)
    {
        var diffs = new List<string>();
        var lag = onGroundLag ?? Array.Empty<int>();
        for (var tick = Math.Max(firstTick, 1); tick < Rows.Count && tick <= lastTick; tick++)
        {
            var row = Rows[tick];
            var o = OracleRow(tick, shiftFrom, shift);

            void Check(string field, long dll, long oracle, long tolerance = 0)
            {
                if (Math.Abs(dll - oracle) > tolerance && ignore?.Invoke(tick, field) != true)
                {
                    diffs.Add($"{Trace.Name} tick {tick} {field}: dll {dll}, oracle {oracle}");
                }
            }

            Check("anim_target", row.AnimTarget, Trace.Col("anim_target", o));
            Check("anim_cur", row.AnimCur, Trace.Col("anim_cur", o));
            Check("facing", row.Facing, Trace.Col("facing", o));
            Check("dir_target", row.DirTarget, Trace.Col("dir_target", o));
            Check("force_x", row.ForceX, Trace.Col("force_x", o));
            Check("force_y", row.ForceY, Trace.Col("force_y", o));
            Check("force_z", row.ForceZ, Trace.Col("force_z", o));
            Check("pos_x", row.PosX, Trace.Col("pos_x", o) - Trace.Col("pos_x", 0), positionTolerance);
            Check("pos_y", row.PosY, Trace.Col("pos_y", o) - Trace.Col("pos_y", 0), positionTolerance);
            Check("pos_z", row.PosZ, Trace.Col("pos_z_dll", o));
            Check("on_ground", row.OnGround, Trace.Col("on_ground", lag.Contains(tick) ? o - 1 : o));
            Check("force_adj", row.ForceAdjusted ? 1 : 0, Trace.Col("force_adj", o) != 0 ? 1 : 0);
            Check("slope", row.Slope, Trace.Col("slope", o));
            Check("sprint_cnt", row.StopSprintCounter, Trace.Col("sprint_cnt", o));
            Check("charge_cnt", row.ChargeCounter, Trace.Col("charge_cnt", o));

            var dllSounds = string.Join(",", row.Sounds.Where(s => !IsStubbedSound(s)).OrderBy(s => s));
            var oracleSounds = string.Join(",", Trace.Journal(o).Where(j => j.Kind == "sound" && j.Id > 0 && !IsStubbedSound(j.Id)).Select(j => j.Id).OrderBy(s => s));
            if (dllSounds != oracleSounds && ignore?.Invoke(tick, "sounds") != true)
            {
                diffs.Add($"{Trace.Name} tick {tick} sounds: dll [{dllSounds}], oracle [{oracleSounds}]");
            }

            var dllEffects = string.Join(";", row.Bounces.Select(b => Fx(b.X, b.Y, b.Z)));
            var oracleEffects = string.Join(";", Trace.Journal(o).Where(j => j.Kind == "effect" && j.Id == 9).Select(j => Fx(
                (long)Math.Round(j.X * 65536.0) - Trace.Col("pos_x", 0), (long)Math.Round(j.Y * 65536.0) - Trace.Col("pos_y", 0), (long)Math.Round(j.Z * 65536.0))));
            if (dllEffects != oracleEffects && ignore?.Invoke(tick, "effects") != true)
            {
                diffs.Add($"{Trace.Name} tick {tick} effects 9: dll [{dllEffects}], oracle [{oracleEffects}]");
            }
        }

        return diffs;
    }

    /// <summary>Plays then compares, in one call.</summary>
    public List<string> Compare(int lastTick = int.MaxValue, int firstTick = 1, int[]? onGroundLag = null, Func<int, string, bool>? ignore = null, int positionTolerance = 0, int shiftFrom = 0, int shift = 0)
    {
        Play(lastTick, shiftFrom, shift);
        return CompareRows(lastTick, firstTick, onGroundLag, ignore, positionTolerance, shiftFrom, shift);
    }

    private static string Fx(long x, long y, long z) => string.Create(CultureInfo.InvariantCulture, $"({x},{y},{z})");

    public static void AssertEqualToOracle(List<string> diffs)
        => Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the oracle; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(12))}");
}
