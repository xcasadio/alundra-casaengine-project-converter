#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Transitions;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.f2 T3 (docs/plan-e20-heros.md section 1.10): the arcs of the flail bounce on the real worlds, with the real hit pass.
/// <list type="bullet">
/// <item><description>Map 14 (<see cref="HitArena"/>, the world of the oracle's pilot <c>weapons_vs_target.py</c>): the hero at pixel (212, 96) facing east, a Beannoid 40 pixels east of him, Square at tick 1. The iron flail
/// (item 9, swing 0x12) touches it at tick 11 and the steel flail (item 10, swing 0x40) at tick 12; the next tick the hero is in the rebound state (0x08, 0x25) for 16 ticks and Idle after them; no force. With the
/// Beannoid 160 pixels away (the pilot's case 30, whose label says "dx=100") nothing is touched and nothing bounces: the swing ends at tick 32 (iron) or 35 (steel). The numbers are the pilot's output
/// (<c>docs/plan-e20-annexe/e20-f2/oracle/test-means/out/weapons_vs_post.txt</c>), written here by hand.</description></item>
/// <item><description>Map 69 (<c>Coastal Cave-69</c>, the hammer post of record 0 at (132, 232), z 0), from a portal arrival (a record exists only when the hero's tile at the load lies in its zone): a smoke of the load,
/// then the flails from (110, 232) facing east and from (154, 232) facing west, 22 pixels from the post. The geometry is the INFERENCE of <c>test-means.md</c> section 6 (arena A2), never simulated by the oracle, so no
/// number of the oracle is asserted: the contact tick <c>c</c> is measured (the hero's <c>HitCounter</c> after the hit pass), and the relations the binary fixes are checked: the rebound state starts at the tick after
/// the contact, lasts 16 ticks and is followed by Idle, the hero's forces stay zero. A wall under a corner of the box before the post would start the rebound too; the test tells the two apart by <c>HitCounter</c>
/// (non-zero at the tick before the rebound: the post; zero: a wall), and the rebound's own relations hold in both cases.</description></item>
/// </list>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraFlailBounceArcTests : IDisposable
{
    private const int IronFlail = 9;
    private const int SteelFlail = 10;
    private const int FlailSlot = 3;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ITestOutputHelper _output;

    public AlundraFlailBounceArcTests(ITestOutputHelper output)
    {
        _output = output;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // -----------------------------------------------------------------------------------------
    // Map 14: the Beannoid of the pilot.
    // -----------------------------------------------------------------------------------------

    private sealed record SwingTick(int Tick, uint Target, int HitCounter, int TargetCooldown, int ForceX, int ForceY, int ForceZ);

    private static List<SwingTick> SwingOnMap14(int item, int targetX, int ticks)
    {
        using var arena = new HitArena("f2-arc-" + item + "-" + targetX, 212, 96, new List<int[]> { new[] { targetX, 96 } });
        arena.Arc.Give(item);
        arena.Arc.Equip(FlailSlot);
        var forces = new List<(int, int, int)>();
        arena.AfterTick = _ => forces.Add((arena.Hero.ForceX, arena.Hero.ForceY, arena.Hero.ForceZ));
        var samples = arena.RunSwing(ticks);
        Assert.Equal(ticks, samples.Count);
        return samples.Select((s, i) => new SwingTick(s.Tick, s.Hero.AnimTarget, s.Hero.HitCounter, s.Targets[0].CooldownTicks, forces[i].Item1, forces[i].Item2, forces[i].Item3)).ToList();
    }

    [Theory]
    [InlineData(IronFlail, 0x12u, 0x08u, 11, 28)]
    [InlineData(SteelFlail, 0x40u, 0x25u, 12, 29)]
    public void AFlailThatTouchesABeannoid40PixelsAway_BouncesAtTheNextTick_For16Ticks_ThenIdle_WithNoForce(int item, uint swing, uint rebound, int contact, int idle)
    {
        var ticks = SwingOnMap14(item, 252, 40);

        // The contact: the Beannoid's cooldown is set (25) at the tick the hero's swing touches it, and the hero's HitCounter is written by the same pass.
        var contactTick = ticks.First(t => t.TargetCooldown == 25).Tick;
        Assert.Equal(contact, contactTick);
        Assert.Equal(1, ticks[contactTick - 1].HitCounter);
        Assert.Equal(1, ticks.Count(t => t.TargetCooldown == 25)); // one hit: the rebound ends the swing.

        // The swing up to the contact tick, the rebound from the next tick for 16 ticks, Idle from the 17th.
        Assert.All(ticks.Take(contactTick), t => Assert.Equal(swing, t.Target));
        Assert.All(ticks.Skip(contactTick).Take(16), t => Assert.Equal(rebound, t.Target));
        Assert.Equal(contactTick + 1, ticks.First(t => t.Target == rebound).Tick);
        Assert.Equal(contactTick + 16, ticks.Last(t => t.Target == rebound).Tick);
        Assert.Equal(idle, ticks.First(t => t.Target == 0).Tick);
        Assert.Equal(contactTick + 17, idle);
        Assert.All(ticks.Skip(idle - 1), t => Assert.Equal(0u, t.Target));

        // The bounce adds no force (the swing's own brake aside, nothing is set while the hero stands still).
        Assert.All(ticks.Skip(contactTick), t => Assert.Equal((0, 0, 0), (t.ForceX, t.ForceY, t.ForceZ)));
        // The hit counter is read once: the counters pass clears it at the end of the tick.
        Assert.All(ticks.Skip(contactTick), t => Assert.Equal(0, t.HitCounter));
    }

    [Theory]
    [InlineData(IronFlail, 0x12u, 32)]
    [InlineData(SteelFlail, 0x40u, 35)]
    public void AFlailWithNothingInReach_160PixelsAway_NeverTouchesNorBounces_TheSwingEndsOnItsOwn(int item, uint swing, int idle)
    {
        var ticks = SwingOnMap14(item, 252 + 120, 40);

        Assert.DoesNotContain(ticks, t => t.TargetCooldown == 25);
        Assert.All(ticks, t => Assert.Equal(0, t.HitCounter));
        Assert.DoesNotContain(ticks, t => t.Target is 0x08 or 0x1F or 0x25 or 0x26);
        Assert.All(ticks.Take(idle - 1), t => Assert.Equal(swing, t.Target));
        Assert.Equal(idle, ticks.First(t => t.Target == 0).Tick);
        Assert.All(ticks.Skip(idle - 1), t => Assert.Equal(0u, t.Target));
    }

    // -----------------------------------------------------------------------------------------
    // Map 69: the hammer post.
    // -----------------------------------------------------------------------------------------

    private static ArcSpec Map69(string name, int pixelX, uint direction) => new(
        name, "Coastal Cave", "Coastal Cave-69", Array.Empty<int>(), 0, 0, 0, 600, RealController: true, Prefabs: true,
        Arrival: new ArcArrival(pixelX << 16, 232 << 16, 0, GameState.ResetAnimationId, direction));

    private static void WaitUntilTheHeroIsFree(ArcRun arc)
    {
        for (var frame = 0; frame < 300 && !(arc.Hero.TargetAnimationId == 0u && arc.Hero.CurrentAnimationId == 0u && ArcRun.State.PlayerControlFlags == 0); frame++)
        {
            arc.OneFrame();
        }

        Assert.Equal(0u, arc.Hero.TargetAnimationId);
        Assert.Equal(0u, ArcRun.State.PlayerControlFlags);
    }

    [Fact]
    public void Map69_Loads_TheHammerPostOfRecord0Stands_AndTheHeroSettlesFreeOnTheGround()
    {
        using var arc = new ArcRun(Map69("E20f2-smoke", 110, 24));
        WaitUntilTheHeroIsFree(arc);

        var post = arc.EntityByRecord(0);
        Assert.NotNull(post);
        Assert.Equal(0x100 + 126, post!.SpriteType); // the hammer post.
        Assert.Equal((132 << 16, 232 << 16, 0), (post.PosX, post.PosY, post.PosZ));
        Assert.Equal(1, arc.Hero.IsOnGround);
        Assert.Equal(110 << 16, arc.Hero.PosX);
        Assert.Equal(232 << 16, arc.Hero.PosY);
        Assert.Equal(0, arc.Hero.PosZ);
    }

    private sealed record PostTick(int Tick, uint Target, int HitCounter, int ForceX, int ForceY, int ForceZ);

    private List<PostTick> SwingAtThePost(string name, int item, int heroX, uint direction, int ticks)
    {
        using var arc = new ArcRun(Map69(name, heroX, direction));
        WaitUntilTheHeroIsFree(arc);
        arc.Give(item);
        arc.Equip(FlailSlot);

        var rows = new List<PostTick>();
        var tick = 0;
        var recording = false;
        arc.Proxy.TickTraceForTests = pass =>
        {
            if (!recording)
            {
                return;
            }

            if (pass == "pad")
            {
                tick++;
            }
            else if (pass == "inventory")
            {
                var hero = arc.Hero;
                rows.Add(new PostTick(tick, hero.TargetAnimationId, hero.HitCounter, hero.ForceX, hero.ForceY, hero.ForceZ));
            }
        };

        recording = true;
        arc.Press(PadState.Square); // one frame of one tick, Square pressed at tick 1.
        for (var i = 1; i < ticks; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(ticks, rows.Count);
        return rows;
    }

    [Theory]
    [InlineData(IronFlail, 0x12u, 0x08u, 110, 24u)] // east of the post's west: (110, 232) facing east.
    [InlineData(SteelFlail, 0x40u, 0x25u, 110, 24u)]
    [InlineData(IronFlail, 0x12u, 0x08u, 154, 8u)] // (154, 232) facing west.
    [InlineData(SteelFlail, 0x40u, 0x25u, 154, 8u)]
    public void AFlailAtTheHammerPost_BouncesFor16Ticks_AndTheRelationsOfTheBinaryHold(int item, uint swing, uint rebound, int heroX, uint direction)
    {
        var name = $"E20f2-post-{item}-{heroX}";
        var ticks = SwingAtThePost(name, item, heroX, direction, 60);

        var first = ticks.FirstOrDefault(t => t.Target == rebound);
        Assert.NotNull(first); // the flail does bounce: on the post or on a wall.
        var bounceTick = first!.Tick;
        var contactTick = ticks.Where(t => t.HitCounter != 0).Select(t => (int?)t.Tick).FirstOrDefault();
        var causeIsThePost = contactTick is { } contactAt && contactAt + 1 == bounceTick;
        var causeIsAWall = contactTick is null || contactTick > bounceTick;
        _output.WriteLine($"{name}: swing 0x{swing:X2} from tick 1, contact (HitCounter) at {(contactTick?.ToString() ?? "none")}, rebound 0x{rebound:X2} from tick {bounceTick}: {(causeIsThePost ? "the post" : "a wall under a corner of the box")}.");

        // Whatever starts it, the rebound is the binary's: the state lasts 16 ticks, then Idle; the swing runs up to the tick before it; no force.
        Assert.True(causeIsThePost || causeIsAWall, "the rebound neither follows a contact by one tick nor comes from a wall");
        Assert.All(ticks.Take(bounceTick - 1), t => Assert.Equal(swing, t.Target));
        Assert.All(ticks.Skip(bounceTick - 1).Take(16), t => Assert.Equal(rebound, t.Target));
        Assert.Equal(bounceTick + 16, ticks.First(t => t.Target == 0).Tick);
        Assert.All(ticks.Skip(bounceTick + 15), t => Assert.Equal(0u, t.Target));
        Assert.All(ticks.Skip(bounceTick - 1), t => Assert.Equal((0, 0, 0), (t.ForceX, t.ForceY, t.ForceZ)));

        // When the post is the cause (the case the arena A2 of the plan expects): the contact tick c is measured, the rebound is c + 1 to c + 16, Idle at c + 17.
        if (causeIsThePost)
        {
            var c = contactTick!.Value;
            Assert.Equal(c + 1, bounceTick);
            Assert.Equal(c + 16, ticks.Last(t => t.Target == rebound).Tick);
            Assert.Equal(c + 17, ticks.First(t => t.Target == 0).Tick);
            Assert.Equal(swing, ticks[c - 1].Target);
        }
    }
}
