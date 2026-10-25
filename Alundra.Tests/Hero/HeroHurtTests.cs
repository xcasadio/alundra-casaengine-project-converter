#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.c2b T2 (docs/plan-e20-heros.md section 1.12.1, tests b1 to b6 and b8): the hurt of the hero on the REAL world of map 14 (<see cref="HitArena"/>: the production path, the real hit pass, a real Beannoid), at one, two and
/// three ticks per frame, against the hurt-hero oracle of E20.c2 (<c>docs/plan-e20-annexe/e20-c2/oracle/out/</c>: the real code of <c>ALUN_CD.EXE</c> (France), <see cref="HurtOracleTrace"/>). After every tick the DLL is compared
/// to the row of the trace: the target animation (<c>+0x88</c>), the direction (<c>+0x8C</c>), the life of the entity and of the stats block, <c>+0x1CC</c> and <c>+0x1D0</c>, what the hero touches, the force and the position.
/// The expected values are the oracle's, never computed by the code under test.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroHurtTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const uint Hurt = 0x31;
    private const uint Recoil = 0x1C;

    public HeroHurtTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        PlayerManager.TryUseItemCallCountForTests = 0;
    }

    public void Dispose()
    {
        PlayerManager.ItemUseDriverHookForTests = null;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static IEnumerable<object[]> TicksPerFrame() => new[] { 1, 2, 3 }.Select(n => new object[] { n });

    // -----------------------------------------------------------------------------------------
    // The run of an oracle trace on the arena.
    // -----------------------------------------------------------------------------------------

    /// <summary>What the arena's sample does not hold: the facing, the force and the life of the stats block after the tick.</summary>
    private sealed record HeroExtra(int Tick, uint Direction, int ForceX, int StatsHp, uint Animation, int Charge);

    private sealed class StampedSounds : ISoundPlayer
    {
        private readonly Func<int> _frame;

        public StampedSounds(Func<int> frame) => _frame = frame;

        public List<(int Frame, int Id)> Requests { get; } = new();

        public void PlaySfx(int sfxId) => Requests.Add((_frame(), sfxId));

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

    private sealed class Played : IDisposable
    {
        public required HitArena Arena { get; init; }

        public required List<HeroExtra> Extras { get; init; }

        public required StampedSounds Sounds { get; init; }

        /// <summary>The frame of the arc at which the scenario began (the frame of tick 1 is this one).</summary>
        public int FirstFrame { get; init; }

        public required HurtOracleTrace Trace { get; init; }

        public required int TicksPerFrame { get; init; }

        public int FrameOfTick(int tick) => FirstFrame + (tick - 1) / TicksPerFrame;

        public void Dispose() => Arena.Dispose();
    }

    /// <summary>
    /// Builds the arena of <paramref name="trace"/> (the hero and the Beannoid on the pixels of the oracle) and plays its first <paramref name="lastTick"/> ticks in frames of <paramref name="ticksPerFrame"/> ticks, the pad
    /// of a frame being the oracle's pad of its first tick. <paramref name="flagsOf"/> gives the control word <c>[0x800DC4B8]</c> the harness poses before each tick (the oracle's <c>flags</c> event).
    /// </summary>
    private static Played Play(
        HurtOracleTrace trace, int ticksPerFrame, int lastTick = int.MaxValue, Func<int, uint?>? flagsOf = null, Action<HitArena>? configure = null, Action<HitArena, int>? before = null, Action<HitArena, int>? after = null)
    {
        var arena = new HitArena("hurt-" + trace.Name + "-" + ticksPerFrame, trace.HeroPixelX, trace.HeroPixelY, trace.TargetPixels);
        try
        {
            var extras = new List<HeroExtra>();
            var sounds = new StampedSounds(() => arena.Arc.Frame);
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arena.Arc.Proxy, sounds);
            configure?.Invoke(arena);

            arena.BeforeTick = tick =>
            {
                if (flagsOf?.Invoke(tick) is { } flags)
                {
                    GameState.Instance.PlayerControlFlags = flags;
                }

                before?.Invoke(arena, tick);
            };
            arena.AfterTick = tick =>
            {
                extras.Add(new HeroExtra(tick, arena.Hero.TargetDirection, arena.Hero.ForceX, GameState.Instance.PlayerStats.Hp, arena.Hero.TargetAnimationId, arena.Hero.WeaponChargeTimer));
                after?.Invoke(arena, tick);
            };

            var last = Math.Min(trace.Ticks, lastTick);
            arena.Begin();
            var firstFrame = arena.Arc.Frame;
            for (var tick = 1; tick <= last;)
            {
                var size = Math.Min(ticksPerFrame, last - tick + 1);
                arena.Frame((uint)trace.Hold[tick], size);
                tick += size;
            }

            Assert.Equal(last, arena.Samples.Count);
            Assert.Equal(last, extras.Count);
            return new Played { Arena = arena, Extras = extras, Sounds = sounds, FirstFrame = firstFrame, Trace = trace, TicksPerFrame = ticksPerFrame };
        }
        catch
        {
            arena.Dispose();
            throw;
        }
    }

    /// <summary>Every difference between the arena and the trace up to <paramref name="lastTick"/>, as "name tick N field: dll X, oracle Y".</summary>
    private static List<string> Compare(Played run, int lastTick = int.MaxValue, bool positions = true)
    {
        var trace = run.Trace;
        var diffs = new List<string>();
        var last = Math.Min(Math.Min(trace.Ticks, lastTick), run.Arena.Samples.Count);
        for (var tick = 1; tick <= last; tick++)
        {
            var sample = run.Arena.Samples[tick - 1];
            var extra = run.Extras[tick - 1];

            void Check(string field, long dll, long oracle)
            {
                if (dll != oracle)
                {
                    diffs.Add($"{trace.Name} x{run.TicksPerFrame} tick {tick} {field}: dll {dll}, oracle {oracle}");
                }
            }

            Check("hero anim_target", sample.Hero.AnimTarget, trace.Col("anim_target", tick));
            Check("hero dir_target", extra.Direction, trace.Col("dir_target", tick));
            Check("hero hp", sample.Hero.Hp, trace.Col("hp", tick));
            Check("stats hp", extra.StatsHp, trace.Global("stats_hp", tick));
            Check("hero c1cc", sample.Hero.DamagedTicks, trace.Col("c1cc", tick));
            Check("hero c1d0", sample.Hero.CooldownTicks, trace.Col("c1d0", tick));
            Check("hero touching", sample.Hero.Touching, trace.Col("touching_slot", tick));
            Check("hero force_x", extra.ForceX, trace.Col("force_x", tick));
            Check("hero charge counter", extra.Charge, trace.Global("charge", tick));
            if (positions)
            {
                Check("hero pos_x", sample.Hero.PosX, trace.Col("pos_x", tick));
                Check("hero pos_y", sample.Hero.PosY, trace.Col("pos_y", tick));
            }

            var target = sample.Targets[0];
            Check("target hp", target.Hp, trace.TargetCol(0, "hp", tick));
            Check("target c1cc", target.DamagedTicks, trace.TargetCol(0, "c1cc", tick));
            Check("target c1d0", target.CooldownTicks, trace.TargetCol(0, "c1d0", tick));
            Check("target hit_counter", target.HitCounter, trace.TargetCol(0, "hit_counter", tick));
            Check("target touching", target.Touching, trace.TargetCol(0, "touching_slot", tick));
        }

        return diffs;
    }

    private static void AssertEqualToOracle(List<string> diffs)
        => Assert.True(diffs.Count == 0, $"{diffs.Count} differences with the oracle; the first ones:{Environment.NewLine}{string.Join(Environment.NewLine, diffs.Take(15))}");

    // -----------------------------------------------------------------------------------------
    // (b1) A1, end to end: the walk into a Beannoid, the touch at 17, the hurt at 18.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B1_TheHeroWalkingIntoABeannoid_IsHurtAtTick18_ThrownBackBy33Pixels_AndComesBackToIdle_TickByTickTheOracle(int ticksPerFrame)
    {
        var trace = HurtOracleTrace.Load("A1.json", "A1");
        using var run = Play(trace, ticksPerFrame);
        var samples = run.Arena.Samples;

        AssertEqualToOracle(Compare(run));

        // The oracle's facts, read from its columns (not typed from the DLL): the touch at 17, the hurt at 18 (life 10 -> 9, +0x1CC 119), 0x31 for 4 ticks, 0x1C for 11, Idle at T + 15 facing the other way again.
        const int t = 18;
        Assert.Equal(17, trace.FirstTick("touching_slot", 1));
        Assert.Equal(t, trace.FirstTick("anim_target", Hurt));
        Assert.Equal(1, samples[t - 2].Hero.Touching);
        Assert.Equal(25, samples[t - 2].Hero.CooldownTicks);
        Assert.Equal((10, 9), (samples[t - 2].Hero.Hp, samples[t - 1].Hero.Hp));
        Assert.Equal((10, 9), ((int)run.Extras[t - 2].StatsHp, (int)run.Extras[t - 1].StatsHp));
        Assert.Equal(119, samples[t - 1].Hero.DamagedTicks);
        for (var tick = t; tick <= t + 3; tick++)
        {
            Assert.Equal(Hurt, samples[tick - 1].Hero.AnimTarget);
            Assert.Equal(8u, run.Extras[tick - 1].Direction); // away from the Beannoid, which stands east.
        }

        for (var tick = t + 4; tick <= t + 14; tick++)
        {
            Assert.Equal(Recoil, samples[tick - 1].Hero.AnimTarget);
        }

        Assert.Equal(0u, samples[t + 14].Hero.AnimTarget); // tick 33 = T + 15.
        Assert.Equal(24u, run.Extras[t + 14].Direction);

        // The recoil: the forces of ticks T to T + 15 sum to -2 168 832 raw (-33.09375 px), the position moves by exactly that from the touch to the end of the recoil.
        var forces = Enumerable.Range(t, 16).Sum(tick => (long)trace.Col("force_x", tick));
        Assert.Equal(-2168832L, forces);
        Assert.Equal(-2168832L, (long)samples[t + 14].Hero.PosX - samples[t - 2].Hero.PosX);

        // The sound 23 comes with the animation 0x31, once, in the frame of the hurt tick.
        var hurtSounds = run.Sounds.Requests.Where(r => r.Id == 23).ToList();
        Assert.Single(hurtSounds);
        Assert.Equal(run.FrameOfTick(t), hurtSounds[0].Frame);
    }

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B1_TheHeroStandingInTheBodyOfABeannoid_IsHurtAtTick2_ThrownOutOfReach_AndNeverTouchedAgain_A1Pad0(int ticksPerFrame)
    {
        var trace = HurtOracleTrace.Load("A1.json", "A1-pad0");
        using var run = Play(trace, ticksPerFrame);

        AssertEqualToOracle(Compare(run));

        Assert.Equal(2, trace.FirstTick("anim_target", Hurt));
        Assert.Equal(1, run.Arena.Samples.Count(s => s.Targets[0].HitCounter == 1 && s.Hero.Touching == 1)); // one touch in 260 ticks: the recoil leaves the reach of the body
        Assert.All(run.Arena.Samples.Skip(1), s => Assert.Equal(9, s.Hero.Hp));
    }

    // -----------------------------------------------------------------------------------------
    // (b3) the charge: the holy sword held for 70 ticks, the hurt at 71 sets the counter to 0 and it stays 0 while the button is still held.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B3_AChargedHolySword_HurtAtTick71_HasItsChargeCounterAt0_FromThatTickOn_WhereTheControlKeepsCounting(int ticksPerFrame)
    {
        var hurtRun = HurtOracleTrace.Load("A5.json", "A5-charge", "e20-c2b");
        var control = HurtOracleTrace.Load("A5.json", "A5-charge-nocontact", "e20-c2b");
        void Configure(HitArena arena)
        {
            arena.Arc.Give(4); // the holy sword (item 4), the best of the sword slot,
            arena.Arc.Equip(1);
        }

        // The oracle moves the hero to x 232 before tick 70 (the Beannoid's body is then 20 px east and touches him at 70).
        void Teleport(HitArena arena, int tick)
        {
            if (tick == 70)
            {
                arena.Hero.PosX = 232 << 16;
                arena.Hero.PushLogicalPositionToRoot();
            }
        }

        using var hurt = Play(hurtRun, ticksPerFrame, configure: Configure, before: Teleport);
        AssertEqualToOracle(Compare(hurt));
        Assert.Equal(48, hurt.Extras[69].Charge); // tick 70: the counter of the oracle, 48,
        Assert.Equal(0, hurt.Extras[70].Charge); // tick 71, the hurt: 0,
        Assert.All(hurt.Extras.Skip(70), e => Assert.Equal(0, e.Charge)); // and 0 after, with the button still held.
        Assert.Equal(Hurt, hurt.Arena.Samples[70].Hero.AnimTarget);

        // The control: nobody touches him (the Beannoid is far away); the counter keeps counting (49, 50 ...).
        // (Compared to tick 79, the last tick the button is held: the pad of a multi-tick frame is the one of its first tick, so the release of tick 80 would be seen a tick late, which has nothing to do with the hurt.)
        using var kept = Play(control, ticksPerFrame, lastTick: 79, configure: Configure);
        AssertEqualToOracle(Compare(kept));
        Assert.Equal(49, kept.Extras[70].Charge);
        Assert.True(kept.Extras[71].Charge >= 50);
    }

    // -----------------------------------------------------------------------------------------
    // (b4) the control word: only 0x20 exactly lets the hit through, and then it writes the invulnerability alone.
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> ControlWords() => new[] { "D1-00", "D1-80", "D1-20", "D1-24", "D1-30", "D1-10", "D1-04", "D1-A0", "D1-20-release" }.Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(ControlWords))]
    public void B4_TheControlWord_OnlyTheWholeWord0x20ReachesTheEntry_WithoutAStateNorARecoil_AnyOtherLockLosesTheHit(string name)
    {
        var trace = HurtOracleTrace.Load("D1.json", name);
        var changes = trace.Ticks;
        uint? Flags(int tick)
        {
            // The oracle's 'flags' events: the word is posed before tick 1, and (D1-20-release) released before tick 40.
            return tick == 1 ? (uint)trace.Global("ctl_flags", 1) : (tick == 40 && name == "D1-20-release" ? 0u : null);
        }

        using var run = Play(trace, 1, flagsOf: Flags, configure: arena => PlayerManager.SetPlayerHp(GameState.Instance, 6));

        AssertEqualToOracle(Compare(run));
        Assert.True(changes >= 60);
        var word = (uint)trace.Global("ctl_flags", 1);
        var samples = run.Arena.Samples;
        if (word == 0x20)
        {
            Assert.Equal(9, samples[1].Hero.Hp); // tick 2: the entry took the life,
            Assert.Equal(119, samples[1].Hero.DamagedTicks); // and the invulnerability is the only thing written:
            Assert.All(samples.Take(60), s => Assert.Equal(0u, s.Hero.AnimTarget)); // no state,
            Assert.All(samples.Take(60), s => Assert.Equal(232 << 16, s.Hero.PosX)); // no recoil.
        }
        else if ((word & 0x34) != 0)
        {
            Assert.All(samples.Take(60), s => Assert.Equal(10, s.Hero.Hp)); // the hit is lost: the touch is written and cleared, the life stays,
            Assert.All(samples.Take(60), s => Assert.Equal(0, s.Hero.DamagedTicks)); // no invulnerability.
            Assert.True(samples.Count(s => s.Hero.CooldownTicks == 25) > 1, "the contact goes on and touches again every 26 ticks");
        }
    }

    // -----------------------------------------------------------------------------------------
    // (b6) a hero at 1 life: he goes to 0 and keeps the hand.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B6_AHeroWith1Life_Touched_GoesTo0_ButKeepsTheHand_NoDeathYet(int ticksPerFrame)
    {
        var trace = HurtOracleTrace.Load("E1.json", "E1-hp1-bean");
        using var run = Play(trace, ticksPerFrame, lastTick: 2, configure: arena =>
        {
            arena.Hero.Hp = 1;
            PlayerManager.SetPlayerHp(GameState.Instance, 1);
        });

        // Ticks 1 and 2 are the oracle's (the touch, the hurt with life 1 -> 0); from tick 3 the oracle enters the death branch (E18, not ported), so the comparison stops there.
        AssertEqualToOracle(Compare(run, lastTick: 2));
        Assert.Equal((1, 0), (run.Arena.Samples[0].Hero.Hp, run.Arena.Samples[1].Hero.Hp));
        Assert.Equal(0, (int)run.Extras[1].StatsHp);
        Assert.Equal(Hurt, run.Arena.Samples[1].Hero.AnimTarget);

        // The hero keeps the hand: the recoil ends in Idle at T + 15 and a held direction walks him again; nothing in the DLL kills or reloads him at 0 life.
        var arena = run.Arena;
        for (var tick = 3; tick <= 17; tick++)
        {
            arena.Frame(0, 1);
        }

        Assert.Equal(0u, arena.Hero.TargetAnimationId);
        Assert.Equal(0, arena.Hero.Hp);
        var x = arena.Hero.PosX;
        for (var tick = 18; tick <= 30; tick++)
        {
            arena.Frame(PadState.Right, 1);
        }

        Assert.True(arena.Hero.PosX > x + (4 << 16), "the hero at 0 life walks east");
        Assert.Equal(1u, arena.Hero.TargetAnimationId);
        Assert.Equal(0, arena.Hero.Hp);
    }

    // -----------------------------------------------------------------------------------------
    // (a4) the facing away from the toucher, at twelve bearings.
    // -----------------------------------------------------------------------------------------

    public static IEnumerable<object[]> Bearings() => new[] { "E", "W", "S", "N", "SE", "SW", "NE", "NW", "ENE", "WSW", "SSE", "NNW" }.Select(n => new object[] { "A4-" + n });

    [Theory]
    [MemberData(nameof(Bearings))]
    public void A4_TheHeroTouchedFromAnyBearing_IsHurtFacingAwayFromTheBeannoid(string name)
    {
        var trace = HurtOracleTrace.Load("A4.json", name);
        using var run = Play(trace, 1);

        AssertEqualToOracle(Compare(run, positions: false));
        Assert.Equal(2, trace.FirstTick("anim_target", Hurt));
    }

    // -----------------------------------------------------------------------------------------
    // (b5) the item driver at its place, the state 0x31 on the land calls TryUseItem.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(TicksPerFrame))]
    public void B5_TheItemDriver_RunsOncePerTick_OnTheHurtTickBeforeTheStateIsWritten_AndTheState0x31CallsTryUseItem(int ticksPerFrame)
    {
        var trace = HurtOracleTrace.Load("A1.json", "A1");
        var tick = 0;
        var driver = new Dictionary<int, List<(uint Animation, uint Direction)>>();
        var startState = new Dictionary<int, uint>();
        var tryUse = new Dictionary<int, int>();
        var countAtStart = 0;
        PlayerManager.ItemUseDriverHookForTests = hero =>
        {
            if (!driver.TryGetValue(tick, out var calls))
            {
                driver[tick] = calls = new List<(uint, uint)>();
            }

            calls.Add((hero.TargetAnimationId, hero.TargetDirection));
        };

        using var run = Play(
            trace, ticksPerFrame, lastTick: 40,
            before: (arena, t) =>
            {
                tick = t;
                startState[t] = arena.Hero.TargetAnimationId;
                countAtStart = PlayerManager.TryUseItemCallCountForTests;
            },
            after: (_, t) => tryUse[t] = PlayerManager.TryUseItemCallCountForTests - countAtStart);

        // The binary's driver runs once a tick: at its site before the pad, or in the hurt block (which ends the tick before that site).
        for (var t = 1; t <= 40; t++)
        {
            Assert.Equal(1, driver[t].Count);
        }

        // Tick 18 (the hurt): the driver sees the walk (1) and the old facing (24): it runs BEFORE the state and the facing are written.
        Assert.Equal((1u, 24u), driver[18][0]);
        Assert.Equal(Hurt, run.Arena.Samples[17].Hero.AnimTarget);
        Assert.Equal(8u, run.Extras[17].Direction);

        // The state 0x31 calls TryUseItem once a tick (0x80032680): the ticks that START in 0x31 are 19 to 22 (the chain to 0x1C comes after the 22nd's pass of the hero). Without the case the state would call it 0 times.
        var inHurt = Enumerable.Range(1, 40).Where(t => startState[t] == Hurt).ToList();
        Assert.Equal(new[] { 19, 20, 21, 22 }, inHurt);
        Assert.All(inHurt, t => Assert.Equal(1, tryUse[t]));
    }
}
