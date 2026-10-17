#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using Xunit.Abstractions;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Enemies;
using Alundra.Combat;
using Alundra.Cells;

namespace Alundra.Tests;

/// <summary>
/// E14.a1c T2 (docs/plan-e14-ia-native.md section 1.3c, "Arc H115"): the hall of map 115, with the natives live. B[2] (map event 1, entered at (252, 872)) activates the four Muruta of the records 24 to 27 (<c>0x2D</c> at
/// <c>@401</c>, <c>@403</c>, <c>@405</c>, <c>@407</c>); each lands from 128 px, runs its C program up to <c>@1469</c> (<c>0x40 [2, 0]</c>: C10 takes over) and is killed with the sword by a deterministic policy (a port of the oracle's
/// <c>Swordsman</c>, as the arc G116 of <see cref="AlundraBeannoideGrateArcTests"/>) on the real hit pass. B[2] waits on the four deaths (<c>0x2C [24..27]</c>), posts G569 (<c>@434</c>) and opens three gates (bit <c>0x80</c> of the
/// ground property). What is asserted is what the generator's flow cannot change: the eight calls of D9 (counted by the journal of the native handlers of the runner, never inferred from the life points), the life sequence 3, 1, 0 of each
/// Muruta (2 per hit), the timeline of each death (D9 at H+1, status 4 at H+23, recycled at H+24), the door of B[2] at the latest E1 + 2 and its fixed gaps, the flags, the cells and the four break effects. Not asserted: the positions of the
/// Muruta (the generator decides their walk), the loot, the sound 84 (E20.e0). The arc runs outside the generator's collection: it reads no number of it.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraMurutaHallArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;
    private const int D = ScriptHelper.ProgramDTouch;
    private static readonly int[] Records = { 24, 25, 26, 27 };
    private readonly ITestOutputHelper _output;

    public AlundraMurutaHallArcTests(ITestOutputHelper output) => _output = output;

    private sealed class Sample
    {
        public int Tick;
        public int Hp;
        public EntityStatus Status;
        public bool Touching;
        public uint Animation;
        public int PosX;
        public int PosY;
        public int PosZ;
        public bool InWorld;
        public bool CNative;
        public int D9Calls;
        public List<(int Index, int Mode, int X, int Y)> BreakSlots = new();
    }

    private static bool Hittable(AlundraEntityScriptProxy target)
        => target.Status == EntityStatus.Normal && target.IsOnGround != 0 && target.FrameCollisionTickCounter == 0 && target.DamagedTickCounter == 0 && (target.HitAnimByte & AlundraHitPass.InvulnerableAnimationBit) == 0;

    /// <summary>The first place (40, 34, 28, 22 or 16 px west of the target) whose four footprint corners (offset (-10, -7), size (21, 15)) are walkable cells at the target's height, or -1.</summary>
    private static int FindDx(AlundraCellStore store, AlundraEntityScriptProxy target)
    {
        var x = target.PosX >> 16;
        var y = target.PosY >> 16;
        var units = unchecked(target.PosZ + 1) >> 20;
        foreach (var dx in new[] { 40, 34, 28, 22, 16 })
        {
            var ok = true;
            foreach (var ex in new[] { -11, 12 })
            {
                foreach (var ey in new[] { -8, 9 })
                {
                    var column = (int)Math.Floor((x - dx + ex) / 24.0);
                    var row = (int)Math.Floor((y + ey) / 16.0);
                    if (column < 0 || column >= 52 || row < 0 || row >= 60)
                    {
                        ok = false;
                        continue;
                    }

                    var cell = LeverArcs.CellAt(store, column, row);
                    ok &= cell.Walkability == 0 && cell.Height == units;
                }
            }

            if (ok)
            {
                return dx;
            }
        }

        return -1;
    }

    [Fact]
    public void H115_TheFourMuruta_DieByTheSword_TheDoorOfTheHallOpens_WithAFrameBreakEffectEach()
    {
        using var arc = new ArcRun(LeverArcs.Spec("E14a1c-H115", LeverArcs.Zone, LeverArcs.World115, new[] { 262 }, 252, 872, 0, 16, 3200));
        arc.Give(1);
        arc.Equip(1);
        var store = LeverArcs.Store(arc);
        var samples = Records.ToDictionary(r => r, _ => new Dictionary<int, Sample>());
        var bodies = new Dictionary<int, AlundraEntityScriptProxy>();
        var cNativeSince = new Dictionary<int, int>();
        var d9AtTick = new Dictionary<int, int>();

        int D9Count() => arc.Runner.NativeHandlerRunCounts.GetValueOrDefault((D, MurutaAi.TouchCode));

        arc.OnFrame = () =>
        {
            var tick = arc.Frame - 1; // the number of the tick the frame just ran, as the trace stamps it
            d9AtTick[tick] = D9Count();
            foreach (var r in Records)
            {
                if (!bodies.ContainsKey(r) && arc.EntityByRecord(r) is { } found)
                {
                    bodies[r] = found;
                }

                if (!bodies.TryGetValue(r, out var body))
                {
                    continue;
                }

                var native = (body.ProgramIndexes[C] & 0x7F) == 0;
                if (native && !cNativeSince.ContainsKey(r))
                {
                    cNativeSince[r] = tick;
                }

                samples[r][tick] = new Sample
                {
                    Tick = tick, Hp = body.Hp, Status = body.Status, Touching = body.TouchingEntity != null, Animation = body.TargetAnimationId, PosX = body.PosX, PosY = body.PosY, PosZ = body.PosZ,
                    InWorld = arc.EntityByRecord(r) != null, CNative = native, D9Calls = D9Count(),
                    BreakSlots = arc.Proxy.EffectStage!.Reservoir.ActiveSlots
                        .Where(s => s.TargetEffect == 8 && !s.TargetMapTable && s.RecordIndex == -1 && s.Mode is 1 or 2 && s.Anchor != null)
                        .Select(s => (s.Index, s.Mode, s.X, s.Y)).ToList(),
                };
            }
        };

        // The hall opens by itself (the arrival is in the map event); wait for the four activations, then for the four landings, then the policy.
        var cooldown = 0;
        var presses = new List<int>();
        var activated = new Dictionary<int, int>();
        while (true)
        {
            if (arc.Frame >= arc.Spec.FrameLimit)
            {
                Assert.Fail($"the hall is not done at the frame limit; hp {string.Join("/", bodies.Select(kv => $"{kv.Key}:{kv.Value.Hp}:{kv.Value.Status}"))}");
            }

            foreach (var r in Records)
            {
                if (!activated.ContainsKey(r) && bodies.ContainsKey(r))
                {
                    activated[r] = arc.Frame;
                }
            }

            var alive = Records.Where(r => bodies.ContainsKey(r) && bodies[r].Status != EntityStatus.Destroyed && arc.EntityByRecord(r) != null).ToList();
            if (bodies.Count == 4 && alive.Count == 0)
            {
                break;
            }

            if (cooldown > 0)
            {
                cooldown--;
                arc.OneFrame();
                continue;
            }

            var target = alive.Select(r => bodies[r]).FirstOrDefault(b => cNativeSince.ContainsKey(b.EntityRefId) && Hittable(b) && FindDx(store, b) >= 0);
            if (target == null)
            {
                arc.OneFrame();
                continue;
            }

            var dx = FindDx(store, target);
            var hx = (target.PosX >> 16) - dx;
            var hy = target.PosY >> 16;
            var hz = unchecked(target.PosZ + 1) >> 16;
            for (var settle = 0; settle < 2; settle++)
            {
                arc.PlaceHero(hx, hy, hz);
                arc.Hero.TargetDirection = 24;
                arc.OneFrame();
            }

            if (!Hittable(target))
            {
                continue;
            }

            arc.PlaceHero(hx, hy, hz);
            arc.Hero.TargetDirection = 24;
            presses.Add(arc.Frame);
            arc.Press(LeverArcs.Square);
            cooldown = 25;
        }

        for (var i = 0; i < 150; i++)
        {
            arc.OneFrame();
        }

        _output.WriteLine("presses at frames " + string.Join(" ", presses));

        // The four activations (0x2D at @401, @403, @405, @407) and the four landings (@1469, 0x40 [2, 0]: C10 takes over), each within 600 frames of its activation.
        var activations = new[] { 401, 403, 405, 407 }.Select(pc => ArcChecks.FramesOf(arc, B, pc, 0x2D)).ToList();
        Assert.All(activations, frames => Assert.Single(frames));
        var activationFrames = activations.Select(f => f[0]).ToArray();
        var landings = ArcChecks.FramesOf(arc, C, 1469, 0x40);
        _output.WriteLine($"0x2D frames: {string.Join(" ", activationFrames)}; @1469 frames: {string.Join(" ", landings)}; C native since (tick): {string.Join(" ", cNativeSince.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}:{kv.Value}"))}");
        Assert.Equal(4, landings.Count);
        for (var k = 0; k < 4; k++)
        {
            Assert.True(landings[k] > activationFrames[k] && landings[k] - activationFrames[k] <= 600, $"the Muruta of the record {Records[k]} landed {landings[k] - activationFrames[k]} frames after its 0x2D");
            Assert.Equal(landings[k], cNativeSince[Records[k]]); // the tick at which the record's own program index went to 0.
        }

        // The calls of D9: eight, counted by the journal of the native handlers, and each one matched by the life drop of the Muruta it hit (never inferred from the life alone).
        Assert.Equal(8, D9Count());
        Assert.Equal(8, d9AtTick.Values.Max());
        var dropsByTick = new Dictionary<int, int>();
        var e1Ticks = new Dictionary<int, int>();
        foreach (var r in Records)
        {
            var series = samples[r].Values.OrderBy(sample => sample.Tick).ToList();
            Assert.Equal(3, series.First(sample => sample.Tick == landings[Array.IndexOf(Records, r)]).Hp); // level 1, 3 life points.
            var drops = new List<(int Tick, int From, int To)>();
            for (var i = 1; i < series.Count; i++)
            {
                if (series[i].Hp < series[i - 1].Hp)
                {
                    drops.Add((series[i].Tick, series[i - 1].Hp, series[i].Hp));
                    dropsByTick[series[i].Tick] = dropsByTick.GetValueOrDefault(series[i].Tick) + 1;
                }
            }

            // Two hits of 2 points each: 3, 1, 0.
            Assert.Equal(new[] { (3, 1), (1, 0) }, drops.Select(d => (d.From, d.To)).ToArray());

            // The timeline of the death: D9 at H + 1 (the tick after the hit), status 4 at H + 23, recycled at H + 24.
            var firstFlag = series.First(sample => sample.Status == EntityStatus.FlagToDestroy).Tick;
            var firstRecycled = series.First(sample => !sample.InWorld).Tick;
            foreach (var drop in drops)
            {
                Assert.True(series.First(sample => sample.Tick == drop.Tick - 1).Touching, $"record {r}: no hit written at H = {drop.Tick - 1}");
            }

            var h = drops[^1].Tick - 1;
            Assert.Equal(h + 23, firstFlag);
            Assert.Equal(h + 24, firstRecycled);
            e1Ticks[r] = firstFlag;
            _output.WriteLine($"record {r}: D9 at {string.Join(", ", drops.Select(d => $"{d.Tick}:{d.From}->{d.To}"))}; H = {h}; status 4 at {firstFlag}; recycled at {firstRecycled}");
        }

        // Each D9 call of the journal is a drop of the same tick (two bodies hit by one press count twice).
        foreach (var (tick, count) in dropsByTick)
        {
            Assert.Equal(count, d9AtTick[tick] - d9AtTick[tick - 1]);
        }

        Assert.Equal(8, dropsByTick.Values.Sum());

        // The break effects: at E1 + 1, exactly one NEW slot (absent at E1 - 1) of the effect 8 (RecordIndex -1, mode 1 or 2, anchored) at the Muruta killed, whose position is that of its E1 tick. An effect of 8 lasts about 33 ticks and
        // two Muruta can stand on the same spot (they are not collidable): the slot of an earlier death at the same place is told apart by being already there before this E1.
        var slotsSeen = new List<string>();
        foreach (var r in Records)
        {
            var at = samples[r][e1Ticks[r]];
            var before = samples[r][e1Ticks[r] - 1].BreakSlots.Select(slot => slot.Index).ToHashSet();
            var next = samples[r][e1Ticks[r] + 1];
            var atEntity = next.BreakSlots.Where(slot => slot.X == at.PosX && slot.Y == at.PosY && !before.Contains(slot.Index)).ToList();
            Assert.True(atEntity.Count == 1, $"record {r}: {atEntity.Count} new break effects at the Muruta at E1 + 1 ({e1Ticks[r] + 1})");
            Assert.Contains(atEntity[0].Mode, new[] { 1, 2 });
            slotsSeen.Add($"{r}: slot {atEntity[0].Index} mode {atEntity[0].Mode} at {e1Ticks[r] + 1}");
        }

        _output.WriteLine("break effects: " + string.Join("; ", slotsSeen));

        // The door of B[2]: @434 (0x05 [57, 2], G569) passes at the latest E1 + 2; the sounds and the waits then fix the gates: N + 1 frames per Wait N (pinned by A115 of AlundraLeverArcTests).
        var x = ArcChecks.FrameOf(arc, B, 434);
        _output.WriteLine($"@434 = {x} (latest E1 {e1Ticks.Values.Max()}); @442 = {ArcChecks.FrameOf(arc, B, 442)}; @461 = {ArcChecks.FrameOf(arc, B, 461)}; @477 = {ArcChecks.FrameOf(arc, B, 477)}; @493 = {ArcChecks.FrameOf(arc, B, 493)}");
        Assert.Equal(e1Ticks.Values.Max() + 2, x);
        Assert.Equal(x + 41, ArcChecks.FrameOf(arc, B, 442));
        Assert.Equal(x + 50, ArcChecks.FrameOf(arc, B, 461));
        Assert.Equal(x + 59, ArcChecks.FrameOf(arc, B, 477));
        Assert.Equal(x + 68, ArcChecks.FrameOf(arc, B, 493));

        // The flags, the three gates (bit 0x80 of the ground property) and the gate that stays shut.
        Assert.True(ArcChecks.IsSet(569));
        Assert.True(ArcChecks.IsSet(262));
        Assert.Equal(0x80, store.GetGroundProperty(10, 44) & 0x80);
        Assert.Equal(0x80, store.GetGroundProperty(11, 44) & 0x80);
        Assert.Equal(0x80, store.GetGroundProperty(5, 42) & 0x80);
        Assert.Equal(0, store.GetGroundProperty(16, 42) & 0x80);
        _output.WriteLine($"ground property (10,44) {store.GetGroundProperty(10, 44)}, (11,44) {store.GetGroundProperty(11, 44)}, (5,42) {store.GetGroundProperty(5, 42)}, (16,42) {store.GetGroundProperty(16, 42)}");

        Assert.All(Records, r => Assert.Null(arc.EntityByRecord(r)));
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
    }
}
