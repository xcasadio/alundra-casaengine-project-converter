#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Ai;
using Alundra.Ai.Projectiles;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f6b T4 (docs/plan-e20-heros.md section 1.22.2, "Phase C et tests de production"): the fire and ice rods through the PRODUCTION path (no birth hook, no test set of wired types: <see cref="PlayerManager.ProductionWiredWeaponTypes"/> alone decides) in the real world of map 14 on the synthetic field
/// of the scenario (<see cref="BallRun"/> with <c>production: true</c>) and, for the switch, in the real world of map 303 (<see cref="ArcRun"/>). They are RED before the set {11, 12, 14, 15} (phase C) and green after it.
/// (p1) the four balls (normal and charged, fire and ice) are born at the tick of the oracle's calendar (R = 96: S = 107 at one tick per frame, 108 at three; the normal ball at P + 11 = 12), daughters of the hero, at the lowest free slot measured before the shot, and their life equals the key;
/// (p2) the fire switch of the map 303 (<c>fire-m303</c>): the fire ball touches it, its program D runs, the flag is set and the class A is cleared (the opcode 0x87 [128, 6] answers yes); the ice ball touches it, the program D runs, and nothing is set (it answers no);
/// (p3) a full table of slots: the shot is lost and the pointer cleared; exactly one free slot: one ball; (p4) the closure: the production set equals the non-zero type bytes of the weapon sheets, and every code of those types is handled by the production registry.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BallProductionTests : IDisposable
{
    private const int Fire = 16;
    private const int Ice = 14;
    private const uint Square = 0x80;

    private readonly ITestOutputHelper _output;
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public BallProductionTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose()
    {
        Reset();
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
    }

    private static void Reset()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        PlayerManager.WeaponSpawnCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static BallRun Production(string key, int item, bool charged, int rate, int? freeSlotsAtBirth = null)
    {
        var oracle = charged ? BallOracle.LoadCharged(key) : BallOracle.LoadStepByImage(key);
        var pad = charged ? (Func<int, uint>)(t => t <= 95 ? PadState.Square : 0u) : null;
        return new BallRun("p-" + key + "-" + rate, item, null, null, pad, 100, charged, handlers: true, seed: oracle.Rng0(1), production: true, freeSlotsAtBirth: freeSlotsAtBirth);
    }

    private static string Fmt(IEnumerable<string> lines) => string.Join("\n", lines);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p1) the four balls are born at the tick of the calendar, daughters of the hero, at the lowest free slot; their life equals the key (flat ground, status, X, Z, slope, ground), their trail and their sound too.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Fire, 11, false, "fire11_flat", 1, 12)]
    [InlineData(Fire, 11, false, "fire11_flat", 3, 12)]
    [InlineData(Ice, 14, false, "ice14_flat", 1, 12)]
    [InlineData(Ice, 14, false, "ice14_flat", 3, 12)]
    [InlineData(Fire, 12, true, "fire12_flat", 1, 107)]
    [InlineData(Fire, 12, true, "fire12_flat", 3, 108)]
    [InlineData(Ice, 15, true, "ice15_flat", 1, 107)]
    [InlineData(Ice, 15, true, "ice15_flat", 3, 108)]
    public void P1_TheRodBirthsTheBallAtTheTickOfTheCalendar_DaughterOfTheHeroAtTheLowestFreeSlot_ItsLifeEqualsTheKey_IfAndOnlyIfTheProductionSetHoldsItsType(int item, int type, bool charged, string name, int rate, int birth)
    {
        var key = charged ? BallOracle.LoadCharged(name, stepByImage: false) : BallOracle.Load(name);
        using var run = Production(name, item, charged, rate);

        run.Go(rate, key.Ticks / rate * rate);

        // The shot is asked with the type of the ball, and the ball is born if and only if the production set holds that type (derived from the set, not copied), in the same assertion.
        Assert.Contains(run.SpawnCalls, c => c.Type == type);
        Assert.Equal(PlayerManager.ProductionWiredWeaponTypes.Contains(type), run.Born is not null);

        // Born at the tick of the calendar of the oracle (S = P + 11 = 12; S = R + 11 = 107 for R = 96, read at the frame boundary: 108 at three ticks per frame), of the type of the rod, daughter of the hero, at the lowest free slot measured before the shot.
        Assert.NotNull(run.Born);
        Assert.Equal(birth, run.BirthTick);
        Assert.Equal(type, run.Born!.SpriteType);
        Assert.Same(run.Arena.Arc.HeroEntity, run.Born.ParentEntity);
        Assert.Equal(run.LowestFreeSlotBeforeBirth, run.BirthSlot);
        Assert.InRange(run.BirthSlot, 0, 63);

        // The life: the line of the ball at every tick of the key (status, X, Z, slope, ground), up to its death; the trail (the effect of the type) and the sound of the explosion.
        var shift = run.BirthTick - key.FirstTick;
        Assert.InRange(shift, 0, rate - 1);
        var last = Math.Min(key.Ticks - key.FirstTick, run.LastTick - run.BirthTick);
        Assert.True(last >= 40, $"the life compared is {last} ticks long");
        var expected = Enumerable.Range(0, last + 1).Select(k => $"S+{k,2}: {key.Line(key.FirstTick + k)}");
        var actual = Enumerable.Range(0, last + 1).Select(k => $"S+{k,2}: {run.Line(run.BirthTick + k)}");
        Assert.Equal(Fmt(expected), Fmt(actual));

        var trail = type is 11 or 12 ? 17 : 18;
        var wantedTrail = key.Effects(trail).Where(e => e.Tick >= key.FirstTick && e.Tick <= key.FirstTick + last).Select(e => e.PositionLine(BallRun.FrameEnd(e.Tick + shift, rate))).ToList();
        var gotTrail = run.Effects.Where(e => e.Id == trail && e.Tick >= run.BirthTick).Select(e => $"t{e.Tick} id={e.Id} dir={e.Direction} x={e.X} y={e.Y} z={e.Z}").ToList();
        Assert.NotEmpty(wantedTrail);
        Assert.Equal(Fmt(wantedTrail), Fmt(gotTrail));

        var wantedSounds = key.Sounds(key.FirstTick, key.FirstTick + last).Select(x => $"+{x.Tick - key.FirstTick} snd{x.Sfx}").ToList();
        var heard = run.Heard.Played.Where(x => x.Tick >= run.BirthTick && x.Tick <= run.BirthTick + last && x.Sfx != 84).Select(x => $"+{x.Tick - run.BirthTick} snd{x.Sfx}").ToList();
        Assert.Equal(wantedSounds, heard);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p2) the fire switch of the map 303 (record 4, sprite 51, class A, program D at the code 700 = 0x87 [128, 6]): the fire ball lights it, the ice ball does not.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Fire, true)]
    [InlineData(Ice, false)]
    public void P2_TheFireBallLightsTheFireSwitchOfTheMap303_TheProgramDRunsTheFlagIsSetAndTheClassAIsCleared_TheIceBallTouchesItAndNothingIsSet(int item, bool lights)
    {
        var spec = new ArcSpec("E20f6b-fire303", "Olen, Cephas house", "Olen, Cephas house-303", Array.Empty<int>(), 0, 0, 0, 2000, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((38 * 24 + 12) << 16, (11 * 16 + 8) << 16, 0, GameState.ResetAnimationId, 24));
        using var arc = new ArcRun(spec);
        arc.Hero.TargetAnimationId = 0;
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var fire = arc.EntityByRecord(4);
        Assert.NotNull(fire);
        Assert.Equal(0x100 + 51, fire!.SpriteType);
        Assert.NotEqual(0u, fire.Flags & EntityFlags.ClassA);

        arc.Give(item);
        for (var slot = 1; slot <= 6; slot++)
        {
            arc.Equip(slot);
            if (PlayerManager.GetItemIdFromCurrentWeapon(ArcRun.State, ((IScriptHost)arc.Proxy).ItemTables!) == (uint)item)
            {
                break;
            }
        }

        arc.PlaceHero(950, 184, 32);
        arc.Hero.TargetDirection = 24;
        for (var i = 0; i < 4; i++)
        {
            arc.OneFrame();
        }

        arc.Press(Square);
        for (var i = 0; i < 60; i++)
        {
            arc.OneFrame();
        }

        var ran = arc.Trace.Where(r => r.ProgramStart == 700).ToList();
        _output.WriteLine("program D @700: " + string.Join(", ", ran.Select(r => $"f{r.Frame} pc{r.Pc} op0x{r.Opcode:X2} {r.Kind}")));
        _output.WriteLine($"class A kept: {(fire.Flags & EntityFlags.ClassA) != 0}");

        // The ball touched the switch: its program D ran, and the opcode 0x87 itself ran (it is not skipped) - in the same assertion as the outcome, so a ball that was never born cannot pass the negative case.
        var test = Assert.Single(ran, r => r.Opcode == 0x87);
        Assert.Equal(EventTraceKind.Implemented, test.Kind);
        Assert.Equal((lights, lights), (ran.Any(r => r.Opcode == 0x05), ran.Any(r => r.Opcode == 0x2B)));
        Assert.Equal(!lights, (fire.Flags & EntityFlags.ClassA) != 0);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p3) the table of slots: full, the shot is lost (the pointer is cleared all the same); one free slot, one ball.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Fire, 11, false, "fire11_flat")]
    [InlineData(Ice, 14, false, "ice14_flat")]
    [InlineData(Fire, 12, true, "fire12_flat")]
    [InlineData(Ice, 15, true, "ice15_flat")]
    public void P3_WithNoFreeSlotTheShotOfTheBallCreatesNothingAndClearsThePointer_WithExactlyOneFreeSlotItCreatesOneBall(int item, int type, bool charged, string name)
    {
        var fillTick = charged ? 107 : 12;
        (int Held, bool Asked, bool Born, uint Pointer) full;
        using (var run = Production(name, item, charged, 1, freeSlotsAtBirth: 0))
        {
            run.Go(1, fillTick + 3);
            full = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == type), run.Born is not null, run.PointerAfter[fillTick]);
        }

        (int Held, bool Asked, bool Born, uint Pointer, int Tick, int Type) one;
        using (var run = Production(name, item, charged, 1, freeSlotsAtBirth: 1))
        {
            run.Go(1, fillTick + 3);
            one = (run.HeldBeforeBirthTick, run.SpawnCalls.Any(c => c.Type == type), run.Born is not null, run.PointerAfter[fillTick], run.BirthTick, run.Born?.SpriteType ?? -1);
        }

        Assert.Equal(
            ((EntitySlotTable.SlotCount, true, false, 0u), (EntitySlotTable.SlotCount - 1, true, true, 0u, fillTick, type)),
            ((full.Held, full.Asked, full.Born, full.Pointer), (one.Held, one.Asked, one.Born, one.Pointer, one.Tick, one.Type)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p4) the closure: the production set is exactly the non-zero type bytes of the weapon sheets (ground, air and charged animation of each of the 12 records), and the production registry handles every code of those types.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P4_TheProductionSetEqualsTheNonZeroTypeBytesOfTheWeaponSheets_AndTheProductionRegistryHandlesEveryCodeOfThem()
    {
        var sheetTypes = new SortedSet<int>();
        for (var record = 0; record < WeaponTables.RecordCount; record++)
        {
            foreach (var address in new[] { WeaponTables.AnimationAddress(record, air: false), WeaponTables.AnimationAddress(record, air: true), WeaponTables.ChargeAnimationAddress(record) })
            {
                var type = WeaponTables.RecordByte(address + 1);
                if (type != 0)
                {
                    sheetTypes.Add(type);
                }
            }
        }

        Assert.Contains(11, sheetTypes); // the sheets of the rods hold the balls.
        Assert.Equal(sheetTypes.ToList(), PlayerManager.ProductionWiredWeaponTypes.OrderBy(t => t).ToList());

        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var registry = new EventProgramRunner(null, GameState.Instance).NativeSprites;
        Assert.Empty(PlayerManager.UnhandledWeaponSpriteCodes(sheetTypes, catalog, registry));
    }
}
