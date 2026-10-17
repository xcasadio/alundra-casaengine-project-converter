#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Audio;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a5 (docs/plan-e20-heros.md section 1.16.5, D-E0A5-1 to D-E0A5-4, D-E0A5-7): the oracle of the clock of an entity born by the interface, read BY KEY (never pasted): <c>docs/plan-e20-annexe/e20-e0a5/oracle/out/birth_clock.json</c>
/// (rows <c>WAVE</c>, <c>SW0</c> ... <c>SPR251</c>, <c>SPR3</c>; keys <c>cnt1c</c>, <c>end_AC</c>, <c>loops_B0</c>) and the <c>reset_AC</c> of <c>docs/plan-e20-annexe/e20-f3/oracle/out/wave_*.json</c>. S is the first tick of the entity.
/// </summary>
internal static class BirthClockOracle
{
    internal static string Find(params string[] relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(new[] { directory.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no '{string.Join('/', relative)}' found above '{AppContext.BaseDirectory}'");
    }

    private static JsonElement Row(string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Find("docs", "plan-e20-annexe", "e20-e0a5", "oracle", "out", "birth_clock.json")));
        return document.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("label").GetString() == label).Clone();
    }

    internal static IEnumerable<JsonElement> Ticks(string label) => Row(label).GetProperty("ticks").EnumerateArray();

    /// <summary>The first tick (relative to S) at which the end-of-animation flag of the row is raised.</summary>
    internal static int FirstEnd(string label) => Ticks(label).First(t => t.GetProperty("end_AC").GetInt32() == 1).GetProperty("k").GetInt32();

    /// <summary>The box cursor (<c>cnt1c</c>) of the row at S + <paramref name="k"/>.</summary>
    internal static int Cursor(string label, int k) => Ticks(label).First(t => t.GetProperty("k").GetInt32() == k).GetProperty("cnt1c").GetInt32();

    /// <summary>The first tick (relative to S) at which the loop counter (<c>loops_B0</c>) of the row reaches <paramref name="count"/>.</summary>
    internal static int FirstLoop(string label, int count) => Ticks(label).First(t => t.GetProperty("loops_B0").GetInt32() >= count).GetProperty("k").GetInt32();

    /// <summary>The first tick (relative to S) at which the <c>reset_AC</c> of the wave of the E20.f3 oracle file <paramref name="file"/> (<c>wave_steel_east.json</c>) is raised.</summary>
    internal static int FirstWaveFlag(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Find("docs", "plan-e20-annexe", "e20-f3", "oracle", "out", file)));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray()
            .Where(r => r.GetProperty("slots").TryGetProperty("1", out var one) && one.ValueKind == JsonValueKind.Object)
            .Select(r => (Tick: r.GetProperty("tick").GetInt32(), Flag: r.GetProperty("slots").GetProperty("1").GetProperty("reset_AC").GetInt32()))
            .ToList();
        return rows.First(r => r.Flag == 1).Tick - rows[0].Tick;
    }
}

/// <summary>
/// E20.e0a5 phase A (T1): the animation clock of an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> counts its first tick (the binary makes the first switch at the creation and counts the first call of the animation
/// pass), at 1, 2 and 3 ticks per frame and wherever in the frame the entity is born. The real world of map 14 (<see cref="ArcRun"/>, real prefabs), the birth by the test seam of <c>MovePlayer</c>
/// (<see cref="AlundraPlayerManager.WeaponSpawnBirthHookForTests"/>) or, for "born in the native program of another entity", by a call of the interface from the programs pass. Labels: [R] red on the HEAD (predicted), [V] green by construction.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraCreationSwitchClockTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public AlundraCreationSwitchClockTests(ITestOutputHelper output)
    {
        _output = output;
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
    }

    public void Dispose() => AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

    internal static ArcRun Arc(string name) => new(new ArcSpec("E20e0a5-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    internal static void Frame(ArcRun arc, int ticks) => arc.OneFrame(ticks * 0.02f + 0.0002f);

    /// <summary>What the entity shows at the end of a tick (taken before the hit pass, after the clock and the physics).</summary>
    internal sealed record Sample(int Cursor, int Flag, int Loops, EntityStatus Status, bool Owed, int Reserved, uint Current, uint Target, int Impulse);

    /// <summary>One birth in the real world: the entity of the sprite <see cref="Sprite"/> born at the tick <see cref="S"/> (the <c>position</c>-th tick of its frame), sampled at the end of every tick.</summary>
    internal sealed class Birth : IDisposable
    {
        public readonly ArcRun Arc;
        public readonly int Sprite;
        public readonly int Direction;
        public readonly bool Native;
        public readonly bool LowerFlag;
        public AlundraEntityScriptProxy? Born;
        public int Tick;
        public int S = int.MaxValue;
        public readonly SortedDictionary<int, Sample> After = new();

        /// <summary>Told at the head of the lists pass of every tick from the birth on (before the clock), with the entity and the tick relative to S: the scripts that ask for another animation or direction.</summary>
        public Action<AlundraEntityScriptProxy, int>? AtLists;

        /// <summary>E20.e0a5 phase B: told at the hit point of every tick from the birth on (after the clock and the physics), with the entity and the tick relative to S.</summary>
        public Action<AlundraEntityScriptProxy, int>? AtHit;

        public Birth(string name, int sprite, int direction, bool native = false, bool lowerFlag = false)
        {
            Arc = AlundraCreationSwitchClockTests.Arc(name);
            Sprite = sprite;
            Direction = direction;
            Native = native;
            LowerFlag = lowerFlag;
            AlundraPlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
            {
                Born = Spawn(hero, call.X, call.Y, call.Z);
            };
            Arc.Proxy.TickTraceForTests = pass =>
            {
                switch (pass)
                {
                    case "pad":
                        Tick++;
                        if (!Native && Tick == S)
                        {
                            Arm();
                        }

                        break;
                    case "programs":
                        if (Native && Tick == S)
                        {
                            var hero = Arc.Hero;
                            Born = Spawn(hero, hero.PosX, hero.PosY, hero.PosZ);
                        }

                        break;
                    case "lists":
                        if (Born != null)
                        {
                            AtLists?.Invoke(Born, Tick - S);
                        }

                        break;
                    case "hit":
                        if (Born != null)
                        {
                            After[Tick - S] = new Sample(Born.BoxTicks, Born.ForceResetAnimationFlag, Born.AnimCompleteCounter, Born.Status, Born.AnimationSwitchTickOwed, Born.AnimationSwitchTicksReserved,
                                Born.CurrentAnimationId, Born.TargetAnimationId, Born.IsZForceApplied);
                            AtHit?.Invoke(Born, Tick - S);
                        }

                        break;
                }
            };
        }

        private AlundraEntityScriptProxy? Spawn(AlundraEntityScriptProxy hero, int x, int y, int z)
        {
            var born = ((IEntityWorldContext)Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, Sprite, x, y, z, Direction);
            if (born != null && LowerFlag)
            {
                born.SwitchedAtCreation = false; // the record path: the same entity, without the flag.
            }

            return born;
        }

        /// <summary>Arms the hero's bow so that the tick of the call reaches the spawn of the shot (the same montage as <see cref="AlundraBirthTickTests"/>).</summary>
        private void Arm()
        {
            var hero = Arc.Hero;
            hero.WeaponRecordPointer = AlundraWeaponTables.AnimationAddress(8, air: false);
            hero.WeaponStepCounter = AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 2);
            hero.TargetAnimationId = 0x13;
        }

        /// <summary>One tick settles the world (the first frame has a tick of its own); S is then the <paramref name="position"/>-th tick (1-based) of the next frame of <paramref name="ticksPerFrame"/> ticks.</summary>
        public void Settle(int position)
        {
            Frame(Arc, 1);
            S = Tick + position;
        }

        /// <summary>Runs frames of <paramref name="ticksPerFrame"/> ticks until the tick S + <paramref name="last"/> has run.</summary>
        public void Run(int ticksPerFrame, int last)
        {
            while (Tick < S + last)
            {
                Frame(Arc, ticksPerFrame);
            }
        }

        public void RunAll(int ticksPerFrame, int position, int last)
        {
            Settle(position);
            Run(ticksPerFrame, last);
        }

        /// <summary>The first tick (relative to S) at which the end-of-animation flag is raised, or -1.</summary>
        public int FirstFlag() => After.Where(a => a.Value.Flag != 0).Select(a => a.Key).DefaultIfEmpty(-1).First();

        public void Dispose()
        {
            AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
            Arc.Dispose();
        }
    }

    private static int FlagOf(int sprite, int direction, int ticksPerFrame, int position, bool lowerFlag, string name)
    {
        using var run = new Birth(name, sprite, direction, lowerFlag: lowerFlag);
        run.RunAll(ticksPerFrame, position, 34);
        Assert.NotNull(run.Born);
        return run.FirstFlag();
    }

    private static string WaveFile(int sprite, int direction)
        => sprite switch
        {
            28 => "wave_demon_east.json",
            29 => "wave_holy_east.json",
            _ => direction switch { 0x00 => "wave_steel_south.json", 0x08 => "wave_steel_west.json", 0x10 => "wave_steel_north.json", _ => "wave_steel_east.json" },
        };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a1) The wave: the end-of-animation flag is 0 up to S+24 and 1 at S+25 (key reset_AC of E20.f3), at 1, 2 and 3 ticks per frame, born at every position of the frame; the same entity with the flag lowered
    // before its first tick (the path of the records) raises it one tick later.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(27, 0x18, 1, 1)]
    [InlineData(27, 0x18, 2, 1)]
    [InlineData(27, 0x18, 2, 2)]
    [InlineData(27, 0x18, 3, 1)]
    [InlineData(27, 0x18, 3, 2)]
    [InlineData(27, 0x18, 3, 3)]
    [InlineData(27, 0x10, 1, 1)]
    [InlineData(27, 0x10, 2, 2)]
    [InlineData(27, 0x10, 3, 2)]
    [InlineData(27, 0x08, 1, 1)]
    [InlineData(27, 0x08, 2, 1)]
    [InlineData(27, 0x08, 3, 3)]
    [InlineData(27, 0x00, 1, 1)]
    [InlineData(27, 0x00, 2, 2)]
    [InlineData(27, 0x00, 3, 1)]
    [InlineData(28, 0x18, 1, 1)]
    [InlineData(28, 0x18, 2, 2)]
    [InlineData(28, 0x18, 3, 2)]
    [InlineData(29, 0x18, 1, 1)]
    [InlineData(29, 0x18, 2, 1)]
    [InlineData(29, 0x18, 3, 3)]
    public void A1_R_TheWave_RaisesItsEndFlagAtSPlus25_WhateverTheGroupingAndThePositionInTheFrame_AndOneTickLaterWithoutTheFlag(int sprite, int direction, int ticksPerFrame, int position)
    {
        var expected = BirthClockOracle.FirstWaveFlag(WaveFile(sprite, direction));
        var name = $"a1-{sprite}-{direction}-{ticksPerFrame}-{position}";
        var withFlag = FlagOf(sprite, direction, ticksPerFrame, position, lowerFlag: false, name);
        var withoutFlag = sprite == 27 && direction == 0x18 ? FlagOf(sprite, direction, ticksPerFrame, position, lowerFlag: true, name + "-low") : expected + 1;
        Assert.Equal((expected, expected + 1), (withFlag, withoutFlag));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a2) The cursor of the box: BoxTicks is the cnt1c of the binary at S, S+1, S+2, S+3 (1, 2, 3, 4); born in the programs pass of another entity, the first clock tick is S+1 and it shows 1. The four
    // directions (the direction 0 is the trap of e0a3b), the sprites 3 and 27.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(3, 0x00, 1, 1)]
    [InlineData(3, 0x08, 2, 2)]
    [InlineData(3, 0x10, 3, 1)]
    [InlineData(3, 0x18, 1, 1)]
    [InlineData(27, 0x00, 2, 1)]
    [InlineData(27, 0x08, 1, 1)]
    [InlineData(27, 0x10, 3, 3)]
    [InlineData(27, 0x18, 2, 2)]
    public void A2_R_TheCursorOfTheBox_IsTheCnt1cOfTheBinary_FromTheTickOfTheBirth_InTheFourDirections(int sprite, int direction, int ticksPerFrame, int position)
    {
        using var run = new Birth($"a2-{sprite}-{direction}-{ticksPerFrame}-{position}", sprite, direction);
        run.RunAll(ticksPerFrame, position, 5);
        var expected = Enumerable.Range(0, 4).Select(k => BirthClockOracle.Cursor("WAVE", k)).ToArray();
        Assert.Equal(expected, Enumerable.Range(0, 4).Select(k => run.After[k].Cursor).ToArray());
    }

    [Theory]
    [InlineData(3, 0x18, 1)]
    [InlineData(3, 0x00, 2)]
    [InlineData(27, 0x10, 1)]
    [InlineData(27, 0x08, 2)]
    public void A2_R_BornInTheProgramsOfAnotherEntity_TheFirstClockTickIsSPlus1_AndItsCursorIs1(int sprite, int direction, int ticksPerFrame)
    {
        using var run = new Birth($"a2n-{sprite}-{direction}-{ticksPerFrame}", sprite, direction, native: true);
        run.RunAll(ticksPerFrame, ticksPerFrame, 5);
        var oracle = Enumerable.Range(0, 3).Select(k => BirthClockOracle.Cursor("WAVE", k)).ToArray(); // call number 1, 2, 3
        Assert.Equal((0, oracle[0], oracle[1], oracle[2]), (run.After[0].Cursor, run.After[1].Cursor, run.After[2].Cursor, run.After[3].Cursor));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a3) The loop counter: the sprite 251 (first image of 1 tick) turns once at S+4 and twice at S+9 (key loops_B0 of SPR251), the arrow (3) once at S+11 (SPR3), at 1 and 2 ticks per frame (at 3 ticks per frame the
    // window is longer than the first turn: the residue of D-E0A5-6).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(251, 1, 1)]
    [InlineData(251, 2, 1)]
    [InlineData(251, 2, 2)]
    [InlineData(3, 1, 1)]
    [InlineData(3, 2, 1)]
    [InlineData(3, 2, 2)]
    public void A3_R_TheLoopCounter_TurnsAtTheTicksOfTheBinary(int sprite, int ticksPerFrame, int position)
    {
        using var run = new Birth($"a3-{sprite}-{ticksPerFrame}-{position}", sprite, 0x18);
        run.RunAll(ticksPerFrame, position, 14);
        int First(int count) => run.After.Where(a => a.Value.Loops >= count).Select(a => a.Key).DefaultIfEmpty(-1).First();
        var label = sprite == 251 ? "SPR251" : "SPR3";
        var expected = sprite == 251 ? (BirthClockOracle.FirstLoop(label, 1), BirthClockOracle.FirstLoop(label, 2)) : (BirthClockOracle.FirstLoop(label, 1), -1);
        var seen = sprite == 251 ? (First(1), First(2)) : (First(1), -1);
        Assert.Equal(expected, seen);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [R] (a4) A frame without a tick between the birth and the validation (OneFrame(0)): the flag still comes at S+25. And the validation with zero ticks reserved: an entity born in the programs of another entity at
    // the LAST tick of a frame, then a frame without a tick, owes no tick: nothing is owed after the validation and the first clock tick (S+1) shows the cursor 1.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A4_R_AFrameWithoutATickBetweenTheBirthAndTheValidation_DoesNotDelayTheFlag_AndAValidationWithZeroTicksReservedOwesNoTick()
    {
        var expected = BirthClockOracle.FirstWaveFlag("wave_steel_east.json");

        // Born in MovePlayer at S, then a frame without a tick, then ticks of one per frame.
        using var withGap = new Birth("a4-gap", 27, 0x18);
        withGap.Settle(1);
        Frame(withGap.Arc, 1);   // the tick S
        Frame(withGap.Arc, 0);   // integrated at the head of this frame, validated at its end: no tick between.
        withGap.Run(1, 34);
        var gapFlag = withGap.FirstFlag();

        // Born in the programs pass at the last tick of a frame of two ticks, then a frame without a tick.
        using var native = new Birth("a4-zero", 27, 0x18, native: true);
        native.Settle(2);
        Frame(native.Arc, 2);    // S is the second (last) tick of this frame.
        Frame(native.Arc, 0);    // validation with zero reserved ticks.
        var owedAfterTheEmptyFrame = native.Born!.AnimationSwitchTickOwed;
        var reservedAfterTheEmptyFrame = native.Born.AnimationSwitchTicksReserved;
        Frame(native.Arc, 1);    // S+1: the first clock tick.
        var cursorAtTheFirstClockTick = native.After[1].Cursor;

        Assert.Equal((expected, false, 0, BirthClockOracle.Cursor("WAVE", 0)), (gapFlag, owedAfterTheEmptyFrame, reservedAfterTheEmptyFrame, cursorAtTheFirstClockTick));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a5) A switch asked inside the unvalidated window (by a script, before the clock of the tick): the ticks counted for the creation are dropped; the flag of the end of the animation asked comes at the tick of
    // the rows SW<k> / SWD<k> of the oracle (key end_AC). One tick per frame: SW0 [V], SW1 [R], SW1-2 [V], SWD1 [R], SWD2 [V] (the first validation has already closed the window: regression cases). Two ticks per frame,
    // born at the first tick of the frame: SW2 [R], SWD2 [R] (it keeps the creation switch current in the tick that is not the first, and the lock of D-E0A5-2 for the direction). Three ticks per frame: SW5 [R].
    // SW1-2 at two ticks per frame is excluded (O-E0A5-6).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (int K, uint Animation, int? Direction)[] SwitchesOf(string label)
        => label switch
        {
            "SW0" => new[] { (0, 1u, (int?)null) },
            "SW1" => new[] { (1, 1u, (int?)null) },
            "SW2" => new[] { (2, 1u, (int?)null) },
            "SW5" => new[] { (5, 1u, (int?)null) },
            "SW1-2" => new[] { (1, 1u, (int?)null), (2, 0u, (int?)null) },
            "SWD1" => new[] { (1, 0u, (int?)16) },
            "SWD2" => new[] { (2, 0u, (int?)16) },
            _ => throw new ArgumentOutOfRangeException(nameof(label), label, null),
        };

    private static int FlagWithSwitches(string label, int ticksPerFrame)
    {
        var switches = SwitchesOf(label);
        using var run = new Birth($"a5-{label}-{ticksPerFrame}", 27, 0x18);
        run.AtLists = (born, k) =>
        {
            foreach (var (at, animation, direction) in switches)
            {
                if (k == at)
                {
                    born.TargetAnimationId = animation;
                    if (direction is { } d)
                    {
                        born.TargetDirection = (uint)d;
                    }
                }
            }
        };
        run.RunAll(ticksPerFrame, 1, 38);
        return run.FirstFlag();
    }

    [Theory]
    [InlineData("SW0")]    // [V]
    [InlineData("SW1")]    // [R]
    [InlineData("SW1-2")]  // [V]
    [InlineData("SWD1")]   // [R]
    [InlineData("SWD2")]   // [V]
    public void A5_OneTickPerFrame_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt(string label)
        => Assert.Equal(BirthClockOracle.FirstEnd(label), FlagWithSwitches(label, 1));

    [Theory]
    [InlineData("SW2")]    // [R]
    [InlineData("SWD2")]   // [R]
    public void A5_R_TwoTicksPerFrame_BornAtTheFirstTick_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt(string label)
        => Assert.Equal(BirthClockOracle.FirstEnd(label), FlagWithSwitches(label, 2));

    [Theory]
    [InlineData("SW5")]    // [R]
    public void A5_R_ThreeTicksPerFrame_BornAtTheFirstTick_ASwitchInTheWindow_EndsWhereTheBinaryEndsIt(string label)
        => Assert.Equal(BirthClockOracle.FirstEnd(label), FlagWithSwitches(label, 3));

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // [V] (a6) Zero change: an entity of the MAP LOAD keeps its first clock tick uncounted (the known deviation Q-E0A5-1 and D-E0A5-5: the cursor shows 0, 1, 2 at its first three clock ticks), without the flag. Since E20.e0a4 (D-E0A4-6)
    // the pin is on an entity of the load: the entities born from a record by a script carry the rule (AlundraRecordBirthClockTests); the creation sound of an entity born by the interface is requested once and no impulse is given
    // in its window.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A6_V_AnEntityOfTheMapLoad_KeepsItsFirstClockTickUncounted_KnownDeviationQE0A5_1()
    {
        using var arc = Arc("a6-load");
        var loaded = arc.Entities.First(e => e.EntityRefId >= 0);
        var cursors = new List<int>();
        var tick = 0;
        arc.Proxy.TickTraceForTests = pass =>
        {
            if (pass == "pad")
            {
                tick++;
            }

            if (pass == "hit" && tick <= 3)
            {
                cursors.Add(loaded.BoxTicks);
            }
        };
        while (tick < 3)
        {
            Frame(arc, 1);
        }

        Assert.False(loaded.SwitchedAtCreation);
        Assert.Equal(new[] { 0, 1, 2 }, cursors.ToArray());
    }

    [Fact]
    public void A6_V_TheCreationSoundIsRequestedOnce_AndNoImpulseIsGivenInTheWindowOfTheWave()
    {
        using var run = new Birth("a6-sound", 27, 0x18);
        var player = new RecordingSoundPlayer();
        typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(run.Arc.Proxy, player);
        run.RunAll(2, 1, 6);
        Assert.NotNull(run.Born);
        Assert.Equal((1, 0), (player.Ids.Count(id => id == 0x1E), run.After.Values.Take(6).Sum(s => Math.Abs(s.Impulse)))); // the other requests are the hero's own sounds.
    }

    private sealed class RecordingSoundPlayer : IAlundraSoundPlayer
    {
        public List<int> Ids { get; } = new();

        public void PlaySfx(int sfxId) => Ids.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }
}
