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
/// E14.a1b T5 (docs/plan-e14-ia-native.md section 1.3b, "Arc G116"): the grate of map 116, with the natives live. The two Beannoids of the records 0 and 1 (A1, C1, D1 and E1 of the registry and of the host) are killed with the sword by a
/// deterministic policy (a port of the oracle's <c>Swordsman</c>) on the real hit pass; the program C[1] of the grate (record 6, <c>@312</c>: <c>0x2C [0]</c>, <c>0x2C [1]</c>, then <c>@323 0x1A [3]</c>) then sets its animation 3. What is
/// asserted is what the generator's flow cannot change: the life sequence 9, 7, 5, 3, 1, 0 (five hits of 2 each), the timeline of each death (D1 at H+1, status 4 at H+23, recycled at H+24, the gate at the latest E1 + 1), the grate, the
/// other Beannoids left alone and the two break effects (one per death, at the entity). Not asserted: the loot and the sound 84 (E20.e0) and the trajectories after the apparition (the DLL draws no number at the apparition, so its flow
/// differs from the oracle's). The arc runs outside the generator's collection: it reads no number of it.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraBeannoideGrateArcTests
{
    private const int C = ScriptHelper.ProgramCTick;
    private readonly ITestOutputHelper _output;

    public AlundraBeannoideGrateArcTests(ITestOutputHelper output) => _output = output;

    private sealed class Sample
    {
        public int Tick;
        public int Hp;
        public EntityStatus Status;
        public bool Touching;
        public uint Animation;
        public int PosX;
        public int PosY;
        public bool InWorld;
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
    public void G116_TheTwoBeannoids_DieByTheSword_AndTheGrateOpens_WithAFrameBreakEffectEach()
    {
        using var arc = new ArcRun(LeverArcs.Spec("E14a1b-G116", LeverArcs.Zone, LeverArcs.World116, Array.Empty<int>(), 84, 328, 0, 8, 3200));
        arc.Give(1);
        arc.Equip(1);
        arc.OneFrame();
        var bodies = new[] { arc.EntityByRecord(0)!, arc.EntityByRecord(1)! };
        Assert.NotNull(bodies[0]);
        Assert.NotNull(bodies[1]);
        Assert.Equal(BeannoideAi.SpriteType, bodies[0].SpriteType);
        Assert.Equal(BeannoideAi.SpriteType, bodies[1].SpriteType);
        var others = new[] { 17, 41, 42, 43, 44 };
        Assert.All(others, record => Assert.NotNull(arc.EntityByRecord(record)));
        var store = LeverArcs.Store(arc);

        var samples = new[] { new Dictionary<int, Sample>(), new Dictionary<int, Sample>() };
        var grate = arc.EntityByRecord(6)!;
        Assert.NotNull(grate);
        var grateAnimation = new Dictionary<int, uint>();
        arc.OnFrame = () =>
        {
            var tick = arc.Frame - 1; // the number of the tick the frame just ran, as the trace stamps it
            grateAnimation[tick] = grate.TargetAnimationId;
            for (var r = 0; r < 2; r++)
            {
                var body = bodies[r];
                samples[r][tick] = new Sample
                {
                    Tick = tick, Hp = body.Hp, Status = body.Status, Touching = body.TouchingEntity != null, Animation = body.TargetAnimationId, PosX = body.PosX, PosY = body.PosY,
                    InWorld = arc.EntityByRecord(r) != null,
                    BreakSlots = arc.Proxy.EffectStage!.Reservoir.ActiveSlots
                        .Where(s => s.TargetEffect == 8 && !s.TargetMapTable && s.RecordIndex == -1 && s.Mode is 1 or 2 && s.Anchor != null)
                        .Select(s => (s.Index, s.Mode, s.X, s.Y)).ToList(),
                };
            }
        };

        // The hero on the plateau (the cells x 15..21, y 16..22 at the height of 10 cell units), then the policy until both are gone.
        arc.PlaceHero(444, 328, 160);
        arc.Hero.TargetDirection = 24;
        var cooldown = 0;
        var presses = new List<int>();
        while (bodies.Any(b => b.Status != EntityStatus.Destroyed))
        {
            if (arc.Frame >= arc.Spec.FrameLimit)
            {
                Assert.Fail($"the Beannoids are not both dead at the frame limit; hp {bodies[0].Hp}/{bodies[1].Hp}, status {bodies[0].Status}/{bodies[1].Status}");
            }

            if (cooldown > 0)
            {
                cooldown--;
                arc.OneFrame();
                continue;
            }

            var target = bodies.FirstOrDefault(b => b.Status is EntityStatus.Loaded or EntityStatus.Normal or EntityStatus.Deactivated);
            if (target == null || !Hittable(target) || FindDx(store, target) < 0)
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

        // Let the gate of the grate and the end of the effects run.
        for (var i = 0; i < 60; i++)
        {
            arc.OneFrame();
        }

        _output.WriteLine("presses at frames " + string.Join(" ", presses));
        var e1Ticks = new int[2];
        for (var r = 0; r < 2; r++)
        {
            var series = samples[r].Values.OrderBy(s => s.Tick).ToList();
            var drops = new List<(int Tick, int From, int To)>();
            for (var i = 1; i < series.Count; i++)
            {
                if (series[i].Hp < series[i - 1].Hp)
                {
                    drops.Add((series[i].Tick, series[i - 1].Hp, series[i].Hp));
                }
            }

            var firstFlag = series.First(s => s.Status == EntityStatus.FlagToDestroy).Tick;
            var firstRecycled = series.First(s => !s.InWorld).Tick;
            var hitTicks = series.Where((s, i) => i > 0 && s.Touching && !series[i - 1].Touching).Select(s => s.Tick).ToList();
            _output.WriteLine($"record {r}: drops {string.Join(", ", drops.Select(d => $"{d.Tick}:{d.From}->{d.To}"))}; hits (touching rises) {string.Join(" ", hitTicks)}; status 4 at {firstFlag}; recycled at {firstRecycled}");

            // Five hits, of 2 points each: 9, 7, 5, 3, 1, 0.
            Assert.Equal(new[] { (9, 7), (7, 5), (5, 3), (3, 1), (1, 0) }, drops.Select(d => (d.From, d.To)).ToArray());

            // The timeline of the last death: D1 at H + 1 (the tick after the hit), status 4 at H + 23, recycled at H + 24.
            var d1 = drops[^1].Tick;
            var h = d1 - 1;
            Assert.True(series.First(s => s.Tick == h).Touching, $"record {r}: no hit written at H = {h}");
            Assert.Equal(h + 23, firstFlag);
            Assert.Equal(h + 24, firstRecycled);
            e1Ticks[r] = firstFlag;

            // The break effect: exactly one slot of the effect 8 at the entity, at the tick after E1 (mode 2 after its first update).
            var at = samples[r][firstFlag];
            var next = samples[r][firstFlag + 1];
            var atEntity = next.BreakSlots.Where(s => s.X == at.PosX && s.Y == at.PosY).ToList();
            Assert.True(atEntity.Count == 1, $"record {r}: {atEntity.Count} break effects at the entity at E1 + 1 ({firstFlag + 1})");
            Assert.All(atEntity, s => Assert.Contains(s.Mode, new[] { 1, 2 }));
            _output.WriteLine($"record {r}: break effect slot {atEntity[0].Index} mode {atEntity[0].Mode} at E1 + 1 = {firstFlag + 1}");
        }

        // The grate (record 6): its program C[1] passes the gate of the two 0x2C one tick after the latest E1, and sets the animation 3.
        var gate = ArcChecks.FrameOf(arc, C, 323);
        _output.WriteLine($"E1 ticks {e1Ticks[0]} {e1Ticks[1]}; FrameOf(C, 323) = {gate}");
        Assert.Equal(e1Ticks.Max() + 1, gate);
        Assert.True(grateAnimation.Where(kv => kv.Key < gate).All(kv => kv.Value == 0), "the grate moved before its gate");
        Assert.Equal(3u, grateAnimation[gate]);
        _output.WriteLine("grate animation after the gate: " + string.Join(" ", grateAnimation.Where(kv => kv.Key >= gate).OrderBy(kv => kv.Key).Take(40).Select(kv => $"{kv.Key}:{kv.Value}")));

        // The others stay where they were.
        Assert.All(others, record => Assert.True(arc.EntityByRecord(record) is { } other && other.Status.IsActive(), $"record {record} is gone or destroyed"));
        Assert.Null(arc.EntityByRecord(0));
        Assert.Null(arc.EntityByRecord(1));
        ArcChecks.AssertNoUnexpectedError(arc);
    }
}
