#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Tests.Rendering.Effects;
using Alundra.Tests.Dialogues;

namespace Alundra.Tests.Support;

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

    public required EffectStage Stage { get; init; }

    public required RunOracleTrace Trace { get; init; }

    /// <summary>The effects the stage created on the last tick, as (id, X, Y, Z) in 16.16.</summary>
    public List<(int Id, int X, int Y, int Z)> Created { get; } = new();

    public EntityScriptProxy Hero => Rig.Hero;

    public int StartX { get; private set; }

    public int StartY { get; private set; }

    private static ItemTables Tables() => ItemTables.GetOrCreate(ProjectRootFinder.Find());

    /// <summary>The field of the trace: 52 x 60 cells of 24 x 16 px, the layout painted (walkability, ground property, height).</summary>
    public static CellsCollisionField FieldOf(RunOracleTrace trace)
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
        rig.Controller.Settings.WalkabilityMask = CellsCollisionField.BaseWalkabilityMask; // the LIVE settings of the controller: the montage leaves the mask at 0, the wall cells (0x40) would not block.
        // The Idle of the hero record carries the acceleration byte 1 (`sprite-records.json`, animation 0: 129024 -> 64512 on the tick the slide ends); the sprite clock of the swings keeps the 0 its oracles were built with.
        rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 1 });
        var sounds = new RecordingSoundPlayer();
        rig.Host.SoundPlayer = sounds;

        var tables = Tables();
        rig.Host.ItemTables = tables;
        PlayerManager.InitializeNewGameInventory(rig.Host.GameState, tables);
        switch (trace.Inventory)
        {
            case "kit":
                break;
            case "holy":
                rig.Host.GameState.NumberOfItems[4 * 2 + 1] = 1;
                PlayerManager.SetPlayerWeaponId(rig.Host.GameState, tables, tables.ItemsProperties[4 * 5]);
                break;
            case "b1a":
                rig.Host.GameState.NumberOfItems[0x1A * 2 + 1] = 1;
                break;
            case "b1b":
                rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 1;
                break;
            case "b1c": // E20.i: the boots of level 3
                rig.Host.GameState.NumberOfItems[0x1C * 2 + 1] = 1;
                break;
            case "holy_b1b": // E20.i: the sacred sword and the boots 0x1B together
                rig.Host.GameState.NumberOfItems[4 * 2 + 1] = 1;
                PlayerManager.SetPlayerWeaponId(rig.Host.GameState, tables, tables.ItemsProperties[4 * 5]);
                rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 1;
                break;
            case var named when named.StartsWith("item", StringComparison.Ordinal) && int.TryParse(named.AsSpan(4), System.Globalization.NumberStyles.HexNumber, null, out var itemId):
                // E20.h0a: the boots 0x1B of the kit of the swim traces and the object `itemId` owned and equipped (the family W9-item1F to W9-item32).
                rig.Host.GameState.NumberOfItems[0x1B * 2 + 1] = 1;
                rig.Host.GameState.NumberOfItems[itemId * 2 + 1] = 1;
                rig.Host.GameState.PlayerStats.ItemId = (short)itemId;
                break;
            default:
                throw new InvalidOperationException($"{trace.Name}: the inventory '{trace.Inventory}' is not built by the montage");
        }

        var stage = new EffectStage();
        stage.Load(EffectGolden.BankOf(Root, 163));
        rig.Host.EffectStage = stage;

        var montage = new RunMontage { Rig = rig, Sounds = sounds, Stage = stage, Trace = trace };
        NativeEffects.CreatedForTests = slot => montage.Created.Add((slot.TargetEffect, slot.X, slot.Y, slot.Z));
        configure?.Invoke(montage);

        var hero = rig.Hero;
        hero.TargetDirection = (uint)trace.Col("dir_target", 0);
        hero.TargetAnimationId = (uint)trace.Col("anim_target", 0); // E20.i: the swim groups start in any state (`hero_anim`); the run groups all start in Idle.
        hero.AnimationDirection = (int)trace.Col("facing", 0);
        hero.CurrentAnimationId = ~hero.TargetAnimationId;
        rig.Step(); // the first switch (the sprite takes the animation 0 in the facing) is resolved: nothing pending at tick 1.
        // E20.i: a hero that starts in deep water (slope 4 already probed by the settling updates) has been sent to the float by that step; the oracle does not run MovePlayer before tick 1, so the start state of the trace is posed again
        // (and its animation reloaded at tick 1, as the binary's first tick does). On land the step changes nothing and this is skipped.
        var startAnimation = (uint)trace.Col("anim_target", 0);
        if (hero.TargetAnimationId != startAnimation)
        {
            hero.TargetAnimationId = startAnimation;
            hero.TargetDirection = (uint)trace.Col("dir_target", 0); // the exits of the land turn the direction of 0x3A and 0x3B: the settling step has done it once already
            hero.CurrentAnimationId = ~startAnimation;
        }

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
    public List<string> CompareRows(int lastTick = int.MaxValue, int firstTick = 1, int[]? onGroundLag = null, Func<int, string, bool>? ignore = null, int positionTolerance = 0, int shiftFrom = 0, int shift = 0,
        Func<int, int, bool>? ignoreSound = null, bool compareEndCounter = false)
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
            if (compareEndCounter)
            {
                Check("b0", row.AnimCompleteCounter, Trace.Col("b0_complete", o)); // E20.i: the counter of animation ends (+0xB0), turned by the chains of the swim.
            }

            // `ignoreSound(tick, id)` drops one sound of this tick on both sides (E20.i: the sounds 21 and 6 of a landing in the water are E20.e).
            bool Kept(int id) => !IsStubbedSound(id) && ignoreSound?.Invoke(tick, id) != true;
            var dllSounds = string.Join(",", row.Sounds.Where(Kept).OrderBy(s => s));
            var oracleSounds = string.Join(",", Trace.Journal(o).Where(j => j.Kind == "sound" && j.Id > 0 && Kept(j.Id)).Select(j => j.Id).OrderBy(s => s));
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
    public List<string> Compare(int lastTick = int.MaxValue, int firstTick = 1, int[]? onGroundLag = null, Func<int, string, bool>? ignore = null, int positionTolerance = 0, int shiftFrom = 0, int shift = 0,
        Func<int, int, bool>? ignoreSound = null, bool compareEndCounter = false)
    {
        Play(lastTick, shiftFrom, shift);
        return CompareRows(lastTick, firstTick, onGroundLag, ignore, positionTolerance, shiftFrom, shift, ignoreSound, compareEndCounter);
    }

    private static string Fx(long x, long y, long z) => string.Create(CultureInfo.InvariantCulture, $"({x},{y},{z})");

    public static void AssertEqualToOracle(List<string> diffs)
        => Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the oracle; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(12))}");
}
