#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Alundra.Audio;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.A362 (docs/plan-e20-heros.md section 1.30.1a, D-A362-1 to D-A362-3): the scene of Olen's dream (map 362) on the real controller. Olen throws his bomb (record 5, sprite 2), the bomb slides to the north wall, inside the footprint of the
/// lid (record 0, sprite 243), explodes against it and the lid is touched: its program D[1] deactivates it and the four Murutas are born. What the DLL does not do yet is pinned by two named tests that E20.h3a rewrites: the lid stays in place in status 3
/// (<c>(E, 19)</c> is not registered) and the bomb stays in status 3 (<c>(E, 12)</c> is not registered).
/// Every expected tick was written before the first execution of the scene, from the bytecode of the map and the keys of the oracle, in docs/plan-e20-annexe/e20-a362/t0a.md (table (d)); S is the tick of the <c>0x8B</c> of Olen (C[4] @569), A the tick of the
/// <c>Set anim 1</c> of the bomb (C[5] @641) and H = A + 16 the tick of the hit pass that touches the lid. A discrepancy stops the plan: it is never re-fitted. A "tick" is a logic tick, counted by the head of each tick of the world proxy (<c>pad</c>); a cadence k runs
/// k ticks per frame, and every state is read at the end of its tick (the <c>inventory</c> pass), after the hit pass.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class OlenBombLidArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;
    private const int F = ScriptHelper.ProgramFInteract;
    private const uint Square = 0x80;
    private const int Px = 65536;
    private const string Zone = "Coal Mine _ Olen's Dream";
    private const string World362 = "Coal Mine _ Olen's Dream-362";

    // -----------------------------------------------------------------------------------------
    // The recorder: everything is stamped with the logic tick it happened in
    // -----------------------------------------------------------------------------------------

    private sealed class Stamped : ISoundPlayer
    {
        private readonly Func<int> _tick;

        public Stamped(Func<int> tick) => _tick = tick;

        public List<(int Tick, int Id)> Requests { get; } = new();

        public void PlaySfx(int sfxId) => Requests.Add((_tick(), sfxId));

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

    /// <summary>The state of the world at the end of one tick (after its hit pass).</summary>
    private sealed class Snap
    {
        public int Tick;
        public bool BombPresent;
        public int BombX, BombY, BombZ, BombHit;
        public uint BombDirection, BombAnimation;
        public EntityStatus BombStatus;
        public bool BombHasFrameBox;
        public int OlenX, OlenY, OlenZ;
        public bool LidPresent, LidTouching, LidTouchingIsTheBomb;
        public int LidCounter, LidX, LidY, LidZ;
        public EntityStatus LidStatus;
        public readonly bool[] MurutaPresent = new bool[4];
        public readonly (int X, int Y)[] MurutaAt = new (int X, int Y)[4];
        public bool T2, T3, T4, T5, T6;
        public bool HeroTouching;
        public int HeroHp, StatsHp, HeroX, HeroY, HeroZ, HeroHit;
        public bool Rec0Present, Rec1Present, Rec2Present, Rec3Present, Rec4Present;
        public bool AnyOf5To10Present;
    }

    private readonly record struct Ins(int Tick, int Slot, int Pc, int Opcode);

    private sealed class Timeline
    {
        public int K;
        public readonly List<Snap> Snaps = new();
        public readonly List<Ins> Trace = new();
        public readonly List<(int Tick, int Id)> Sounds = new();
        public readonly List<(int Tick, int X, int Y, int Z)> Sparks = new();

        /// <summary>The end of each frame: the last tick of the frame and the number of active effect slots of the effects 9 (hit sparks) and 11 (the break effect of the lid).</summary>
        public readonly List<(int EndTick, int Sparks9, int Break11)> Frames = new();

        public string? Error;
        public List<(int Opcode, int Pc, string Kind)> Skipped = new();
        public int PressTick = -1;

        public Snap At(int tick) => Snaps[tick];

        public int FirstTick(int slot, int pc) => Trace.Where(t => t.Slot == slot && t.Pc == pc).Select(t => t.Tick).DefaultIfEmpty(-1).Min();

        public int[] Ticks(int slot, int pc) => Trace.Where(t => t.Slot == slot && t.Pc == pc).Select(t => t.Tick).ToArray();

        public int[] PcsAt(int slot, int tick) => Trace.Where(t => t.Slot == slot && t.Tick == tick).Select(t => t.Pc).ToArray();

        public int S => FirstTick(C, 569);

        public int A => FirstTick(C, 641);

        public int H => A + 16;

        public IEnumerable<Snap> Between(int from, int to) => Snaps.Where(s => s.Tick >= from && s.Tick <= to);
    }

    private sealed class Rig : IDisposable
    {
        public readonly ArcRun Arc;
        public readonly Timeline Line;
        public readonly float Elapsed;
        public int Tick = -1;

        public Rig(string name, int[] flags, int k, ArcArrival? arrival)
        {
            Line = new Timeline { K = k };
            Elapsed = k / 50f;
            Arc = new ArcRun(new ArcSpec(name, Zone, World362, flags, 0, 0, 0, 100_000, RealController: true, Prefabs: true, Arrival: arrival));
            try
            {
                var player = new Stamped(() => Tick);
                typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, player);
                _player = player;
                Arc.Proxy.HitSparksForTests = (x, y, z) => Line.Sparks.Add((Tick, x, y, z));
                Arc.Proxy.TickTraceForTests = pass =>
                {
                    if (pass == "pad")
                    {
                        Tick++;
                    }
                    else if (pass == "inventory")
                    {
                        Line.Snaps.Add(Take());
                    }
                };
                Arc.OnInstruction = t => Line.Trace.Add(new Ins(Tick, t.Slot, t.Pc, t.Opcode));
                Arc.OnFrame = () =>
                {
                    var slots = Arc.Proxy._effectStage.Reservoir.Slots.Where(s => s.Active).ToList();
                    Line.Frames.Add((Tick, slots.Count(s => s.TargetEffect == 9), slots.Count(s => s.TargetEffect == 11)));
                };
            }
            catch
            {
                Arc.Dispose();
                throw;
            }
        }

        private readonly Stamped _player;

        public void Frame() => Arc.OneFrame(Elapsed);

        public void FrameWithTheDialogueButton() => Arc.OneFrameWithTheDialogueButton(Elapsed);

        public void Frames(int n)
        {
            for (var i = 0; i < n; i++)
            {
                Frame();
            }
        }

        public void PressSquare() => Arc.Press(Square, Elapsed);

        public void Finish()
        {
            Arc.LetGoOfTheDialogueButton();
            Line.Sounds.AddRange(_player.Requests);
            Assert.Equal(Tick + 1, Line.Snaps.Count);
            try
            {
                ArcChecks.AssertNoUnexpectedError(Arc);
            }
            catch (Exception e)
            {
                Line.Error = e.Message;
            }

            Line.Skipped = Arc.SkippedOrExceeded.Select(t => (t.Opcode, t.Pc, t.Kind.ToString())).Distinct().ToList();
        }

        public void Dispose() => Arc.Dispose();

        private Snap Take()
        {
            var s = new Snap { Tick = Tick };
            var bomb = Arc.EntityByRecord(5);
            var olen = Arc.EntityByRecord(4);
            var lid = Arc.EntityByRecord(0);
            if (bomb is not null)
            {
                s.BombPresent = true;
                (s.BombX, s.BombY, s.BombZ) = (bomb.PosX, bomb.PosY, bomb.PosZ);
                s.BombHit = bomb.HitCounter;
                s.BombDirection = bomb.TargetDirection;
                s.BombAnimation = bomb.TargetAnimationId;
                s.BombStatus = bomb.Status;
                s.BombHasFrameBox = bomb.HasFrameBox;
            }

            if (olen is not null)
            {
                (s.OlenX, s.OlenY, s.OlenZ) = (olen.PosX, olen.PosY, olen.PosZ);
            }

            if (lid is not null)
            {
                s.LidPresent = true;
                s.LidTouching = lid.TouchingEntity is not null;
                s.LidTouchingIsTheBomb = bomb is not null && lid.TouchingEntity is not null && ReferenceEquals(lid.TouchingEntity, ArcRun.EntityOf(bomb));
                s.LidCounter = lid.FrameCollisionTickCounter;
                s.LidStatus = lid.Status;
                (s.LidX, s.LidY, s.LidZ) = (lid.PosX, lid.PosY, lid.PosZ);
            }

            for (var i = 0; i < 4; i++)
            {
                if (Arc.EntityByRecord(6 + i) is { } muruta)
                {
                    s.MurutaPresent[i] = true;
                    s.MurutaAt[i] = (muruta.PosX, muruta.PosY);
                }
            }

            s.T2 = ArcChecks.IsTemporarySet(2);
            s.T3 = ArcChecks.IsTemporarySet(3);
            s.T4 = ArcChecks.IsTemporarySet(4);
            s.T5 = ArcChecks.IsTemporarySet(5);
            s.T6 = ArcChecks.IsTemporarySet(6);
            s.HeroTouching = Arc.Hero.TouchingEntity is not null;
            s.HeroHp = Arc.Hero.Hp;
            s.StatsHp = ArcRun.State.PlayerStats.Hp;
            (s.HeroX, s.HeroY, s.HeroZ) = (Arc.Hero.PosX, Arc.Hero.PosY, Arc.Hero.PosZ);
            s.HeroHit = Arc.Hero.HitCounter;
            s.Rec0Present = lid is not null;
            s.Rec1Present = Arc.EntityByRecord(1) is not null;
            s.Rec2Present = Arc.EntityByRecord(2) is not null;
            s.Rec3Present = Arc.EntityByRecord(3) is not null;
            s.Rec4Present = olen is not null;
            s.AnyOf5To10Present = Enumerable.Range(5, 6).Any(r => Arc.EntityByRecord(r) is not null);
            return s;
        }
    }

    // -----------------------------------------------------------------------------------------
    // The two scenes, played once per cadence and shared by the tests (a timeline is plain data)
    // -----------------------------------------------------------------------------------------

    private static readonly object Gate = new();
    private static readonly Dictionary<(int K, bool Flag248), Timeline> Cache = new();

    /// <summary>D-A362-3: the arrival at the pixel (204, 144), level 2, facing north (direction 16), not the arrival tile of the map 185 (which would run B[6]).</summary>
    private static ArcArrival Arrival => new(204 << 16, 144 << 16, 2 << 20, GameState.ResetAnimationId, 16);

    private static Timeline Scene(int k, bool flag248)
    {
        lock (Gate)
        {
            if (!Cache.TryGetValue((k, flag248), out var line))
            {
                line = Play(k, flag248);
                Cache[(k, flag248)] = line;
            }

            return line;
        }
    }

    /// <summary>With G248: the hero talks to Olen (the production seam of the contact entity, then Square), the dialogue is closed by the button, and the scene runs through the tick H + 248 (before the dialogue 133 of H + 250). Without it: 330 ticks after the talk.</summary>
    private static Timeline Play(int k, bool flag248)
    {
        using var rig = new Rig($"E20A362-{(flag248 ? "g248" : "none")}-k{k}", flag248 ? new[] { 248 } : Array.Empty<int>(), k, Arrival);
        var arc = rig.Arc;
        rig.Frames(4);

        // The recipe of the preset: from the arrival the hero walks up to Olen (Up held until the contact stops him at y 134) and presses the button while he pushes. The talk must start from there: the walk of 48 px south that B[3]
        // orders then ends at y 184, against the block of the record 1 (x 192, y 200), and "beyond the radius 48" is passed; from the arrival pixel (204, 144) the block stops the hero after 40 px and B[3] never ends.
        arc.HoldDirections(PadState.Up);
        var still = 0;
        var lastY = arc.Hero.PosY;
        while (still < 3)
        {
            Assert.True(arc.Frame < 80, "the hero never stops against Olen");
            rig.Frame();
            still = arc.Hero.PosY == lastY ? still + 1 : 0;
            lastY = arc.Hero.PosY;
        }

        rig.Line.PressTick = rig.Tick + 1;
        rig.PressSquare();
        arc.ReleaseDirections();
        var h = -1;
        var scanned = 0;
        while (true)
        {
            if (arc.Frame >= 6000)
            {
                throw new XunitException($"arc {arc.Spec.Name}: no end after 6000 frames. {arc.StuckMessage("the end of the scene")}");
            }

            rig.FrameWithTheDialogueButton();
            for (; scanned < rig.Line.Snaps.Count; scanned++)
            {
                if (h < 0 && rig.Line.Snaps[scanned].LidTouching)
                {
                    h = scanned;
                }
            }

            // Without a contact the scene still ends at A + 16 + 248 (H is A + 16 by the table), so that the tests that do not need the contact stay independent of it.
            var end = h >= 0 ? h : rig.Line.A >= 0 ? rig.Line.A + 16 : -1;
            if (flag248 ? end >= 0 && rig.Tick >= end + 248 : rig.Tick >= rig.Line.PressTick + 330)
            {
                break;
            }
        }

        rig.Finish();
        return rig.Line;
    }

    public static TheoryData<int> Cadences => new() { 1, 2, 3 };

    // -----------------------------------------------------------------------------------------
    // (a1) the load
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A1_TheMap362Loads_TheRecords0To4Stand_AndTheRecords5To10AreAbsent(int k)
    {
        var line = Scene(k, true);

        Assert.Null(line.Error);
        Assert.True(line.Skipped.Count == 0, "skipped or exceeded: " + string.Join("; ", line.Skipped));
        var first = line.At(0);
        Assert.True(first.Rec0Present && first.Rec1Present && first.Rec2Present && first.Rec3Present && first.Rec4Present, "the records 0 to 4 stand at the first tick");
        Assert.False(first.AnyOf5To10Present, "the records 5 to 10 are born by 0x8B / 0x8A, none at the load");
        Assert.Equal(EntityStatus.Normal, first.LidStatus);
    }

    // -----------------------------------------------------------------------------------------
    // (a2) the talk: F[3] @748 sets T2, then Olen takes the branch of G248 (the dialogue 132, not the 131)
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A2_TheTalk_SetsT2ByF3At748_AndOlenSaysTheDialogue132NotThe131(int k)
    {
        var line = Scene(k, true);

        var talk = line.Ticks(F, 748);
        Assert.Single(talk);
        Assert.InRange(talk[0], line.PressTick, line.PressTick + k); // the press and F[3] run in the frame of the press.
        Assert.True(line.At(talk[0]).T2);
        Assert.Contains(line.Trace, t => t.Slot == F && t.Pc == 748 && t.Opcode == 0x05);

        var lose = line.FirstTick(C, 514);
        var dialog132 = line.FirstTick(C, 536);
        var flag3 = line.FirstTick(C, 540);
        Assert.True(lose > talk[0] && dialog132 >= lose && flag3 > dialog132, $"@514 {lose}, @536 {dialog132}, @540 {flag3} after the talk {talk[0]}");
        Assert.Equal(-1, line.FirstTick(C, 527)); // the dialogue 131 (without G248) is never reached.
        Assert.True(line.S > flag3);
    }

    // -----------------------------------------------------------------------------------------
    // (a3) the birth of the bomb
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A3_TheBomb_IsBornAtTheTickOfTheSpawnOfOlen_AtHisPositionPlus32PixelsOfZ_FacingNorth_InAnimation0(int k)
    {
        var line = Scene(k, true);
        var s = line.S;

        // Row 1 (BYTECODE): Wait 10 = 11 ticks, Wait 30 = 31 ticks.
        Assert.Equal(11, line.FirstTick(C, 562) - line.FirstTick(C, 558));
        Assert.Equal(31, s - line.FirstTick(C, 562));
        Assert.Equal(0x8B, line.Trace.First(t => t.Slot == C && t.Pc == 569).Opcode);

        // Row 3 (RULE): absent at the end of S - 1, present at the end of S, at the position of Olen, Z + 32 px, direction 0x10, animation 0.
        Assert.False(line.At(s - 1).BombPresent);
        var born = line.At(s);
        Assert.True(born.BombPresent);
        Assert.Equal((born.OlenX, born.OlenY), (born.BombX, born.BombY));
        Assert.Equal(born.OlenZ + 32 * Px, born.BombZ);
        Assert.Equal(0x10u, born.BombDirection);
        Assert.Equal(0u, born.BombAnimation);

        // Row 5 (RULE): the entity born by the program of another entity takes part from the next tick: its first program call is in S + 1 to S + 3.
        Assert.InRange(line.FirstTick(C, 628), s + 1, s + 3);
    }

    // -----------------------------------------------------------------------------------------
    // (a4) T4 at S + 31
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A4_T4_IsSetAtS31_AndTheBombSeesItInTheSameTick_SoItsAnimation2Starts(int k)
    {
        var line = Scene(k, true);
        var s = line.S;

        // Row 2 (BYTECODE): 0x1A and the wait of 30 = 31 ticks, then T4 at pc 582.
        Assert.Equal(s + 31, line.FirstTick(C, 582));
        Assert.False(line.At(s + 30).T4);
        Assert.True(line.At(s + 31).T4);

        // Row 6 (BYTECODE + RULE): the bomb is born after Olen, its slot is higher, the programs run in slot order: it sees T4 in the very tick.
        Assert.Equal(0, line.FirstTick(C, 635) - line.FirstTick(C, 582));
        Assert.Equal(0u, line.At(s + 30).BombAnimation);
        Assert.Equal(2u, line.At(s + 31).BombAnimation);
    }

    // -----------------------------------------------------------------------------------------
    // (a5) the slide
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A5_TheBomb_SlidesOnePixelPerTickToTheNorthWall_ComesToRestInTheFootprintOfTheLid_AndStaysUntilA(int k)
    {
        var line = Scene(k, true);
        var s = line.S;
        var a = line.A;

        // Row 7 (KEY + INFERENCE): from S + 31 (S + 32 at the latest) the Y decreases by one pixel per tick; at rest at X = 204 +/- 1, Y = 103 +/- 1, Z = 32 by S + 66; unchanged from S + 70 to A.
        var y0 = line.At(s + 30).BombY;
        var m = line.Snaps.First(x => x.Tick > s + 30 && x.BombY < y0).Tick; // recorded, not asserted: the start (S + 31 or S + 32) and the rest by S + 66 are INFERENCE rows of the table (note (iv)); see results.md.
        var r = line.Snaps.First(x => x.Tick > m && x.BombY == line.At(x.Tick - 1).BombY).Tick;
        Assert.True(r <= s + 70, $"the bomb is at rest at S + {r - s} (it starts at S + {m - s})");
        for (var t = m; t < r; t++)
        {
            Assert.Equal(-Px, line.At(t).BombY - line.At(t - 1).BombY);
        }

        var rest = line.At(r);
        Assert.InRange(rest.BombX >> 16, 203, 205);
        Assert.InRange(rest.BombY >> 16, 102, 104);
        Assert.Equal(32, rest.BombZ >> 16);
        foreach (var x in line.Between(r, a))
        {
            Assert.Equal((rest.BombX, rest.BombY, rest.BombZ), (x.BombX, x.BombY, x.BombZ));
        }

        Assert.True(a >= s + 70, "A is after S + 70");
    }

    // -----------------------------------------------------------------------------------------
    // (a6) A: the animation 1 and the sound 46
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A6_TheBomb_StartsTheAnimation1AtA_WithTheSound46OnceAtA(int k)
    {
        var line = Scene(k, true);
        var a = line.A;

        // Row 8 (BYTECODE + RULE): Olen sets T5 at @608, the bomb sees it in the same tick and sets the animation 1 at @641.
        Assert.Equal(0, line.FirstTick(C, 641) - line.FirstTick(C, 608));
        Assert.False(line.At(a - 1).T5);
        Assert.True(line.At(a).T5);

        // Row 9 (KEY): the animation 1 is in force at A, the sound 46 is requested once, at A, and not before.
        Assert.Equal(2u, line.At(a - 1).BombAnimation);
        Assert.Equal(1u, line.At(a).BombAnimation);
        Assert.Equal(new[] { (a, 46) }, line.Sounds.Where(x => x.Id == 46).ToArray());
    }

    // -----------------------------------------------------------------------------------------
    // (a7) H = A + 16: the first tick of box, the contact
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A7_TheBombTouchesTheLid_AtTheFirstTickOfItsBox_A16_WithThreeSparksAndTheCounter25(int k)
    {
        var line = Scene(k, true);
        var a = line.A;
        var h = line.H;

        // Row 10 (KEY): the frame box of the bomb is absent up to A + 15 and present from A + 16.
        foreach (var x in line.Between(a, a + 15))
        {
            Assert.False(x.BombHasFrameBox, $"the frame box of the bomb at A + {x.Tick - a}");
            Assert.False(x.LidTouching, $"the lid touched at A + {x.Tick - a}");
            Assert.Equal(0, x.LidCounter);
        }

        // Row 11 (KEY + RULE), at the end of H: the lid is touched by the bomb, the counter 25, the bomb's hit counter 1, the lid still Normal; the hero is not touched.
        var hit = line.At(h);
        Assert.True(hit.BombHasFrameBox);
        Assert.True(hit.LidTouching);
        Assert.True(hit.LidTouchingIsTheBomb);
        Assert.Equal(25, hit.LidCounter);
        Assert.Equal(1, hit.BombHit);
        Assert.Equal(EntityStatus.Normal, hit.LidStatus);
        Assert.Equal((212, 100, 32), (hit.LidX >> 16, hit.LidY >> 16, hit.LidZ >> 16));
        Assert.Equal(1, line.Snaps.Count(x => x.LidTouching && x.Tick <= h)); // the first and only contact up to H.

        // Three sparks (effect 9): the pass is told once at H, and the effect slots of the frame of H are three, none in the frame before.
        Assert.Single(line.Sparks, x => x.Tick == h);
        var frameOfH = line.Frames.FindIndex(f => f.EndTick >= h);
        Assert.Equal(3, line.Frames[frameOfH].Sparks9);
        Assert.Equal(0, line.Frames[frameOfH - 1].Sparks9);
    }

    // -----------------------------------------------------------------------------------------
    // (a8) H + 1: D[1], T6, the lid in status 3
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A8_TheProgramD1OfTheLid_RunsAtH1_SetsT6_AndPutsTheLidInStatus3(int k)
    {
        var line = Scene(k, true);
        var h = line.H;

        // Row 12 (BYTECODE + RULE): D[1] @728, 731, 734, 737 all at H + 1 and nowhere else.
        Assert.Equal(new[] { 728, 731, 734, 737, 738 }, line.PcsAt(D, h + 1)); // the four instructions of the table, then the End script @738 of the same run.
        Assert.Equal(5, line.Trace.Count(t => t.Slot == D && t.Pc >= 728 && t.Pc <= 738));
        Assert.Equal(new[] { 0x87, 0x04, 0x05, 0x19, 0xFF }, line.Trace.Where(t => t.Slot == D && t.Pc >= 728 && t.Pc <= 738).Select(t => t.Opcode).ToArray());

        Assert.False(line.At(h).T6);
        Assert.True(line.At(h + 1).T6);
        Assert.Equal(EntityStatus.Normal, line.At(h).LidStatus);
        Assert.Equal(EntityStatus.Deactivated, line.At(h + 1).LidStatus);
    }

    // -----------------------------------------------------------------------------------------
    // (a9) the two pins of D-A362-1, rewritten by E20.h3a T3 on what the registered E19 and E12 do
    // -----------------------------------------------------------------------------------------

    /// <summary>Row 19 (KEY), rewritten by E20.h3a T3: once <c>(E, 19)</c> is registered, from H + 1 (status 3) the lid is in status 4 at H + 2 with the sound 230 (<c>0xE6</c>) once and ONE break effect 11, and is out of the world from H + 3 to the end.</summary>
    [Theory]
    [MemberData(nameof(Cadences))]
    public void A9_TheLidIsDestroyedOneTickAfterItsStatus3_WithTheSound230AndTheBreakEffect11(int k)
    {
        var line = Scene(k, true);
        var h = line.H;

        Assert.Equal(EntityStatus.Deactivated, line.At(h + 1).LidStatus);
        Assert.Equal((212, 100, 32), (line.At(h + 1).LidX >> 16, line.At(h + 1).LidY >> 16, line.At(h + 1).LidZ >> 16));
        Assert.Equal(EntityStatus.FlagToDestroy, line.At(h + 2).LidStatus);
        Assert.All(line.Between(h + 3, line.Snaps.Count - 1), x => Assert.False(x.LidPresent));
        Assert.Equal(new[] { h + 2 }, line.Sounds.Where(x => x.Id == 230).Select(x => x.Tick).ToArray());
        var first = line.Frames.Find(f => f.Break11 > 0); // the stage holds the effect for several frames: the first frame that holds it is the one of H + 2, with ONE effect.
        Assert.InRange(first.EndTick, h + 2, h + 2 + k - 1);
        Assert.Equal(1, first.Break11);
    }

    /// <summary>
    /// E20.h3a T2 (w5), the key <c>k_w5_lid.txt</c> of the reading of 2026-10-25 (the lid put in status 3 at k+0, then at k+1 the status 4 with the sound 230 and the break effect 11, at k+2 the slot free): the lid of the map 362, put in status 3 by its program D[1] at H + 1 (k+0 = H + 1, A8), is destroyed one tick later, at H + 2, with the sound 230 (<c>0xE6</c>)
    /// and ONE break effect 11 created at that tick, and is out of the world at H + 3. RED while <c>(E, 19)</c> is not registered in the production registry (the A9 pin above says it stays in status 3 with neither the sound nor the effect); green once it is. The registry is the one of production: nothing is registered by the test.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cadences))]
    public void W5_TheLidDeactivatedByItsProgramD_IsDestroyedOneTickLater_WithTheSound230AndTheBreakEffect11_AndLeavesTheWorldAtOnceAfter_TheKey(int k)
    {
        var line = Scene(k, true);
        var h = line.H;

        Assert.Equal(EntityStatus.Deactivated, line.At(h + 1).LidStatus); // k+0 of the key.
        Assert.True(line.At(h + 2).LidPresent);
        Assert.Equal(EntityStatus.FlagToDestroy, line.At(h + 2).LidStatus); // k+1: status 4.
        Assert.False(line.At(h + 3).LidPresent); // k+2: the slot is free.
        Assert.All(line.Between(h + 3, line.Snaps.Count - 1), x => Assert.False(x.LidPresent));

        // The sound 230 once, at k+1 (H + 2), and the effect 11 once, in the frame that holds that tick (the frame of the arc ends at the tick of the record).
        Assert.Equal(new[] { h + 2 }, line.Sounds.Where(x => x.Id == 230).Select(x => x.Tick).ToArray());
        var firstFrameWithTheEffect = line.Frames.FindIndex(f => f.Break11 > 0);
        Assert.True(firstFrameWithTheEffect >= 0, "no break effect 11 on the stage");
        var frame = line.Frames[firstFrameWithTheEffect];
        Assert.InRange(frame.EndTick, h + 2, h + 2 + k - 1);
        Assert.Equal(1, frame.Break11);
    }

    /// <summary>Row 20 (KEY), rewritten by E20.h3a T3: the bomb is in status 2 until A + 51; <c>(E, 12)</c>, registered, finds the end of its animation 1 and gives the status 4 at A + 52 and a free slot from A + 53 to the end.</summary>
    [Theory]
    [MemberData(nameof(Cadences))]
    public void A9_TheBombIsDestroyedAtTheEndOfItsExplosion_E12(int k)
    {
        var line = Scene(k, true);
        var a = line.A;

        foreach (var x in line.Between(a, a + 50))
        {
            Assert.Equal(EntityStatus.Normal, x.BombStatus);
        }

        Assert.Equal(EntityStatus.FlagToDestroy, line.At(a + 52).BombStatus);
        Assert.All(line.Between(a + 53, line.Snaps.Count - 1), x => Assert.False(x.BombPresent));
    }

    // -----------------------------------------------------------------------------------------
    // (a10) B[4]: the sway of the camera and the four births
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A10_TheMapProgramB4_StartsTheSwayAtH3_StopsItAtH64_AndBearsTheFourMurutasAtH125_156_187_218(int k)
    {
        var line = Scene(k, true);
        var h = line.H;

        // Rows 13 to 16 (BYTECODE + RULE): the Break @171 at H + 2 (a map program sees the flag of an entity one tick later), 0x8E @172 at H + 3, 0x8F @179 at H + 64, then the births.
        Assert.Equal(h + 2, line.FirstTick(B, 171));
        Assert.Equal(h + 3, line.FirstTick(B, 172));
        Assert.Equal(h + 64, line.FirstTick(B, 179));
        var births = new[] { (Pc: 182, Tick: h + 125, X: 204, Y: 104), (Pc: 192, Tick: h + 156, X: 216, Y: 104), (Pc: 202, Tick: h + 187, X: 204, Y: 104), (Pc: 212, Tick: h + 218, X: 216, Y: 104) };
        for (var i = 0; i < 4; i++)
        {
            var birth = births[i];
            Assert.Equal(birth.Tick, line.FirstTick(B, birth.Pc));
            Assert.Equal(0x8A, line.Trace.First(t => t.Slot == B && t.Pc == birth.Pc).Opcode);
            Assert.False(line.At(birth.Tick - 1).MurutaPresent[i], $"the record {6 + i} is present before its birth");
            var born = line.At(birth.Tick);
            Assert.True(born.MurutaPresent[i], $"the record {6 + i} is absent at its birth");
            Assert.Equal((birth.X, birth.Y), (born.MurutaAt[i].X >> 16, born.MurutaAt[i].Y >> 16));
        }

        // Row 18: the arc ends before the Break @222 (H + 249) and the dialogue 133 (H + 250).
        Assert.True(line.Snaps.Count - 1 >= h + 248);
        var breakAt222 = line.FirstTick(B, 222);
        Assert.True(breakAt222 == -1 || breakAt222 == h + 249, $"the Break @222 ran at H + {breakAt222 - h}"); // the last frame of a cadence of 2 or 3 ticks may run past H + 248, never to another tick than H + 249.
    }

    // -----------------------------------------------------------------------------------------
    // (a11) the hero
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void A11_TheHero_IsNeverTouched_KeepsHisLife_AndIsMoreThan60PixelsFromTheExplosion(int k)
    {
        var line = Scene(k, true);
        var h = line.H;

        var life = line.At(0).HeroHp;
        var statsLife = line.At(0).StatsHp;
        Assert.All(line.Snaps, x =>
        {
            Assert.False(x.HeroTouching, $"the hero is touched at tick {x.Tick}");
            Assert.Equal(life, x.HeroHp);
            Assert.Equal(statsLife, x.StatsHp);
        });

        // Row 21: B[3] walks the hero 48 px south; at H he is more than 60 px from the bomb.
        var hit = line.At(h);
        Assert.True((hit.HeroY - hit.BombY) / Px > 60, $"the hero is {(hit.HeroY - hit.BombY) / Px} px from the bomb");
    }

    // -----------------------------------------------------------------------------------------
    // (n1) the witness without G248
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Cadences))]
    public void N1_WithoutG248_OlenRepeatsTheDialogue131_AndTheBombIsNeverBorn(int k)
    {
        var line = Scene(k, false);
        var talk = line.Ticks(F, 748);

        Assert.Null(line.Error);
        Assert.Single(talk);
        Assert.True(line.At(talk[0]).T2);

        // Row 22 (BYTECODE): the dialogue 131 (@527), then Olen goes back to the loop (@532, the Goto), for at least 300 ticks after the talk: no record 5, T3 never set, the branch of the dialogue 132 (@536) never taken.
        var dialog131 = line.FirstTick(C, 527);
        Assert.True(dialog131 > talk[0]);
        Assert.True(line.FirstTick(C, 532) > dialog131);
        Assert.Equal(-1, line.FirstTick(C, 536));
        Assert.Equal(-1, line.FirstTick(C, 569));
        Assert.True(line.Snaps.Count - 1 >= talk[0] + 300);
        Assert.All(line.Snaps, x =>
        {
            Assert.False(x.BombPresent);
            Assert.False(x.T3);
        });
    }

    // -----------------------------------------------------------------------------------------
    // (n2) the sword against the lid: the bench
    // -----------------------------------------------------------------------------------------

    /// <summary>The bench of the sword: the real map 362 without G248 (nothing else happens), the lid alone at (212, 100, 32), the hero placed at (x, y, z) facing east (direction 24), one Square pressed at the first tick of a frame.</summary>
    private static Timeline Bench(int k, int heroX, int heroY, int heroZ, uint direction)
    {
        using var rig = new Rig($"E20A362-n2-k{k}-{heroX}-{heroY}-{heroZ}-{direction}", Array.Empty<int>(), k, Arrival);
        var arc = rig.Arc;
        rig.Frames(4);
        arc.PlaceHero(heroX, heroY, heroZ);
        arc.Hero.TargetDirection = direction;
        rig.Frames(3);
        Assert.Equal((heroX, heroY, heroZ), (arc.Hero.PosX >> 16, arc.Hero.PosY >> 16, arc.Hero.PosZ >> 16)); // standing on the ground at the asked place (at y 100 the footprint reaches the wall row and the ground is 80).

        // Settled: nothing touched yet.
        var lid = arc.EntityByRecord(0)!;
        Assert.Equal(0, lid.FrameCollisionTickCounter);
        Assert.Equal(0, arc.Hero.HitCounter);
        Assert.Null(lid.TouchingEntity);
        Assert.Equal(0u, arc.Hero.TargetAnimationId);

        rig.Line.PressTick = rig.Tick + 1;
        rig.PressSquare();
        rig.Frames(40 / k + 2);
        rig.Finish();
        return rig.Line;
    }

    [Theory]
    [MemberData(nameof(Cadences))]
    public void N2_ASwordStroke_AtTheLid_LeavesItUntouched_ButTheHitPassWritesWhatItWritesForAnImmuneTarget(int k)
    {
        var line = Bench(k, 176, 105, 32, 24);
        var p = line.PressTick;

        // The stroke starts at the press tick and the box of the sword overlaps the body of the lid at once (T0 (d2)): the response 0x80 to the type 1 is immune, so TouchingEntity stays null
        // and, at the same tick, the pass writes the counter 25 on the lid, the hit counter 1 on the attacker and the three sparks, centred at the overlap.
        var hit = line.At(p);
        Assert.False(hit.LidTouching);
        Assert.Equal(25, hit.LidCounter);
        Assert.Equal(1, hit.HeroHit);
        Assert.Single(line.Sparks);
        Assert.Equal((p, 12910591, 6979583, 2588671), line.Sparks[0]); // bench_geometry.out.txt: (196.99998, 106.49998, 39.49998) px.
        var frameOfPress = line.Frames.FindIndex(f => f.EndTick >= p);
        Assert.Equal(3, line.Frames[frameOfPress].Sparks9);
        Assert.All(line.Snaps, x => Assert.False(x.LidTouching, $"the lid is touched at tick {x.Tick}"));
        Assert.Equal(EntityStatus.Normal, line.Snaps[^1].LidStatus);
    }

    [Theory]
    [InlineData(1, 176, 105, 32, 8)]
    [InlineData(2, 176, 105, 32, 8)]
    [InlineData(1, 176, 135, 32, 24)]
    [InlineData(3, 176, 135, 32, 24)]
    public void N2_AStrokeThatDoesNotReachTheLid_WritesNothingOnIt(int k, int heroX, int heroY, int heroZ, uint direction)
    {
        var line = Bench(k, heroX, heroY, heroZ, direction);
        var p = line.PressTick;

        // The same bench, the hero facing west in the same place, or 30 px south of it facing east (bench_geometry.out.txt, no frame of the stroke overlaps the body of the lid): the box of the sword does not overlap the body of the lid at the first tick of the stroke, and the pass writes nothing on the lid, nor on the attacker.
        Assert.True(line.Sparks.Count == 0, $"sparks: {string.Join("; ", line.Sparks)}; the lid counter by tick: {string.Join(" ", line.Snaps.Select(x => x.LidCounter))}; hero hit counter: {string.Join(" ", line.Snaps.Select(x => x.HeroHit))}; press tick {p}");
        Assert.Equal(0, line.At(p).LidCounter);
        Assert.Equal(0, line.At(p).HeroHit);
        Assert.False(line.At(p).LidTouching);
    }
}
