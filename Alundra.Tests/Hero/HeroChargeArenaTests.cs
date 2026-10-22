#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.f1a T5 (docs/plan-e20-heros.md section 1.9): the charge on the REAL world of map 14 (<see cref="HitArena"/>: the production path, the real hit pass, a real Beannoid), with the steel sword (item 2, slot 1).
/// Two things the montage of <see cref="HeroChargeTests"/> cannot show: the effects reach the WORLD's reservoir through the stage the world drives (the world marks the hero's operations itself, rank 1), and the
/// charged swing 0x18 touches a Beannoid.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroChargeArenaTests : IDisposable
{
    private const uint Square = PadState.Square;
    private const int SteelEdge = 2;
    private const int SwordSlot = 1;

    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;
    private readonly ulong _previousSeed = GameRandom.RandomSeed;
    private readonly ITestOutputHelper _output;

    public HeroChargeArenaTests(ITestOutputHelper output)
    {
        _output = output;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameRandom.RandomSeed = _previousSeed;
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static HitArena Arena(string name, int targetX)
    {
        var arena = new HitArena("f1a-" + name, 212, 96, new List<int[]> { new[] { targetX, 96 } });
        arena.Arc.Give(SteelEdge);
        arena.Arc.Equip(SwordSlot);
        return arena;
    }

    /// <summary>The steel sword held on a Beannoid 40 pixels east (the geometry of the flail's pilot): the first swing 0x3F touches it at tick 5; the charged swing 0x18 (the Square released at tick 91) touches it too.</summary>
    [Fact]
    public void TheChargedSwing0x18_OfTheSteelSword_TouchesABeannoidOfMap14()
    {
        using var arena = Arena("hit", 212 + 40);
        arena.Begin();
        for (var tick = 1; tick <= 140; tick++)
        {
            arena.Frame(tick <= 90 ? Square : 0, 1);
        }

        Assert.Equal(1, arena.Hero.CurrentWeaponRecord); // the steel sword's record.
        var contacts = arena.Samples.Where(s => s.Targets[0].CooldownTicks == 25).Select(s => (s.Tick, Swing: s.Hero.AnimTarget)).ToList();
        _output.WriteLine("contacts (tick, hero animation): " + string.Join(" ", contacts.Select(c => $"({c.Tick}, 0x{c.Swing:X2})")));

        // The press swing touches at tick 5; the charged swing is the animation 0x18 from tick 91 on, for 46 ticks (to tick 136), and touches the target while it plays.
        Assert.Equal(2, contacts.Count);
        Assert.Equal((5, 0x3Fu), contacts[0]);
        Assert.Equal(0x18u, contacts[1].Swing);
        Assert.InRange(contacts[1].Tick, 92, 136);
        Assert.Equal(0x18u, arena.Samples[90].Hero.AnimTarget);   // tick 91.
        Assert.Equal(0x18u, arena.Samples[135].Hero.AnimTarget);  // tick 136, the last of the swing.
        Assert.Equal(0u, arena.Samples[136].Hero.AnimTarget);     // tick 137: Idle.
    }

    /// <summary>
    /// The wiring through the world: the hold sparks and the burst are created in the WORLD's reservoir by the stage the world drives, at the ticks they are emitted. Nothing is created before the counter reaches 60 (tick 82
    /// with the press at tick 1 and the swing 0x3F of 21 ticks); a hold spark is created at the ticks of the cadence of 4 of the counter <c>+0x1C</c> from then on; at the release (tick 91) the 16 effects of the burst appear at
    /// the hero, in the table's order, with the binary's Z.
    /// </summary>
    [Fact]
    public void TheEffectsOfTheCharge_AreCreatedInTheWorldsReservoir_TheBurstAtTheRelease()
    {
        using var arena = Arena("wiring", 212 + 100);
        var reservoir = arena.Arc.Proxy.EffectStage!.Reservoir;
        var createdAtTick = new Dictionary<int, List<(int X, int Y, int Z, int ForceX, int ForceY)>>();
        var tick = 0;
        arena.BeforeTick = t => tick = t;
        NativeEffects.CreatedForTests = slot =>
        {
            if (!createdAtTick.TryGetValue(tick, out var list))
            {
                createdAtTick[tick] = list = new();
            }

            list.Add((slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY));
        };
        try
        {
            arena.Begin();
            var activeAfter91 = 0;
            for (var t = 1; t <= 95; t++)
            {
                arena.Frame(t <= 90 ? Square : 0, 1);
                if (t == 91)
                {
                    activeAfter91 = reservoir.ActiveCount;
                }
            }

            var firstSparkTick = createdAtTick.Keys.Min();
            Assert.InRange(firstSparkTick, 82, 85); // the counter is 60 from tick 82, and +0x1C & 3 == 0 within four ticks.
            var sparkTicks = createdAtTick.Keys.Where(t => t < 91).OrderBy(t => t).ToList();
            Assert.True(sparkTicks.Count >= 2, "two hold sparks between ticks 82 and 90");
            Assert.All(sparkTicks, t => Assert.Single(createdAtTick[t]));
            Assert.All(sparkTicks.Zip(sparkTicks.Skip(1)), p => Assert.Equal(4, p.Second - p.First)); // the cadence of +0x1C & 3.

            var release = createdAtTick[91];
            Assert.True(release.Count is 16 or 17, "the release tick creates the burst, preceded by a hold spark on a tick of the cadence");
            var hero = arena.Hero;
            var burst = release.TakeLast(16).ToList();
            for (var i = 0; i < 16; i++)
            {
                // The offsets of the burst from the hero: Burst[i] relative to (6553600, 6553600), Z = the hero's Z + 1 + 0x100000.
                Assert.Equal(hero.PosX + (Burst[i].X - 6553600), burst[i].X);
                Assert.Equal(hero.PosY + (Burst[i].Y - 6553600), burst[i].Y);
                Assert.Equal(hero.PosZ + 1 + 0x100000, burst[i].Z);
                Assert.Equal((Burst[i].ForceX, Burst[i].ForceY), (burst[i].ForceX, burst[i].ForceY));
            }

            Assert.True(activeAfter91 >= 16, $"the world's reservoir holds the burst after tick 91 ({activeAfter91} active)");
            Assert.DoesNotContain(createdAtTick.Keys, t => t > 91);
        }
        finally
        {
            NativeEffects.CreatedForTests = null;
        }
    }

    /// <summary>
    /// The queued operations carry the tick that emitted them (rank 1, ADR-0046), so the reservoir does not depend on how the ticks are grouped into frames: the same scenario run in frames of 1, 2 and 3 logic ticks (the
    /// charge to the release at tick 91 and four ticks after it) leaves the same effects in the same slots, at the same positions with the same forces and the same image, drawn from the same generator words.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame(int ticksPerFrame)
    {
        var reference = RunCharge(1);
        var grouped = RunCharge(ticksPerFrame);

        Assert.NotEmpty(reference.Slots);
        Assert.Equal(reference.Slots, grouped.Slots);
        Assert.Equal(reference.Word, grouped.Word);
        Assert.Equal(reference.Creations, grouped.Creations);
    }

    private (List<(int Index, int X, int Y, int Z, int ForceX, int ForceY, int Image, bool Destroying)> Slots, uint Word, List<(int X, int Y, int Z, int ForceX, int ForceY)> Creations) RunCharge(int ticksPerFrame)
    {
        using var arena = Arena("frames" + ticksPerFrame, 212 + 100);
        GameRandom.RandomSeed = 0x12345678;
        var creations = new List<(int X, int Y, int Z, int ForceX, int ForceY)>();
        NativeEffects.CreatedForTests = slot => creations.Add((slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY));
        try
        {
            arena.Begin();
            for (var tick = 1; tick <= 96; tick += ticksPerFrame)
            {
                arena.Frame(tick <= 90 ? Square : 0, ticksPerFrame);
            }
        }
        finally
        {
            NativeEffects.CreatedForTests = null;
        }

        var slots = arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots
            .Select(s => (s.Index, s.X, s.Y, s.Z, s.ForceX, s.ForceY, s.CurrentImageSetIndex, s.Destroying)).ToList();
        return (slots, (uint)GameRandom.RandomSeed, creations);
    }

    // The 16 effects of the burst at the hero (6553600, 6553600): (X, Y, ForceX, ForceY), the table of HeroChargeTests (hand-written from the two tables of the binary).
    private static readonly (int X, int Y, int ForceX, int ForceY)[] Burst =
    {
        (6553600, 8126464, 344064, 0), (5650432, 8006656, 318080, 87808), (4885504, 7665664, 243264, 162176), (4372480, 7155712, 131712, 211904),
        (4194304, 6553600, 0, 229376), (4372480, 5951488, -131712, 211904), (4885504, 5441536, -243264, 162176), (5650432, 5100544, -318080, 87808),
        (6553600, 4980736, -344064, 0), (7456768, 5100544, -318080, -87808), (8221696, 5441536, -243264, -162176), (8734720, 5951488, -131712, -211904),
        (8912896, 6553600, 0, -229376), (8734720, 7155712, 131712, -211904), (8221696, 7665664, 243264, -162176), (7456768, 8006656, 318080, -87808),
    };
}
