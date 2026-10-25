#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.d1 T4 (docs/plan-e20-heros.md section 1.13): the run on the REAL world, through the production path (<see cref="ArcRun"/>, real prefabs, the real exported animations and the world's own <c>Update</c>), where
/// <see cref="HeroRunTests"/> uses the montage of E20.b. One arc, on map 14 (the ladder of the ship, map 389, cannot be played here: its intro locks the hero and moves him, see the plan):
/// <list type="bullet">
/// <item><description>map 14 (<see cref="HitArena"/>, the flat free cells of the oracle): the hero runs east at a Beannoid 152 pixels away, with and without the tackle, tick by tick against the run oracle
/// (G7: <c>R7-run-into-beannoid</c> and the sweep of the press tick <c>g7/tackle/R7-tackle-s30</c> to <c>s62</c>). The real exported animations give the lengths of the preparation (25 ticks) and of the tackle (32), the real
/// hit pass gives the blow of the tackle at tick 61 (three sparks) and the touch of the body. The traces are compared to their LAST tick (E20.c2b T2, b7): the hurt of the hero comes with the damage entry <c>0x80031A68</c> and the
/// states 0x31 and 0x1C, so the touch of the body (or the blow of the tackle that ends in it) is followed by the hurt, the recoil and the life, column by column;</description></item>
/// </list>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroRunArenaTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    private const int HeroPixelX = 100;
    private const int HeroPixelY = 96;
    private const int TargetPixelX = 252;

    public HeroRunArenaTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static IEnumerable<object[]> Tackles() => RunOracleTrace.NamesOf("g7/tackle").Select(n => new object[] { n });

    /// <summary>Plays the pad of the trace on the arena of map 14 and returns every difference with the oracle up to the last tick of the trace, the hurt of the hero included.</summary>
    private static (List<string> Diffs, HitArena Arena, int Last) PlayAgainstTheBeannoid(string name)
    {
        var trace = RunOracleTrace.Load(name);
        var arena = new HitArena("run-" + name, HeroPixelX, HeroPixelY, new List<int[]> { new[] { TargetPixelX, HeroPixelY } });
        var last = trace.Ticks;
        var heroAfter = new List<(uint Direction, int ForceX)>();
        arena.AfterTick = _ => heroAfter.Add((arena.Hero.TargetDirection, arena.Hero.ForceX));
        arena.Begin();
        for (var tick = 1; tick <= last; tick++)
        {
            arena.Frame((uint)trace.Hold[tick], 1);
        }

        var diffs = new List<string>();
        Assert.Equal(last, arena.Samples.Count);
        for (var tick = 1; tick <= last; tick++)
        {
            var sample = arena.Samples[tick - 1];

            void Check(string field, long dll, long oracle)
            {
                if (dll != oracle)
                {
                    diffs.Add($"{name} tick {tick} {field}: dll {dll}, oracle {oracle}");
                }
            }

            Check("hero anim_target", sample.Hero.AnimTarget, trace.Col("anim_target", tick));
            Check("hero dir_target", heroAfter[tick - 1].Direction, trace.Col("dir_target", tick));
            Check("hero force_x", heroAfter[tick - 1].ForceX, trace.Col("force_x", tick));
            Check("hero hp", sample.Hero.Hp, trace.Col("hp", tick));
            Check("hero c1cc (damaged ticks)", sample.Hero.DamagedTicks, trace.Col("inv_1cc", tick));
            Check("hero pos_x", sample.Hero.PosX - arena.HeroStartX, trace.Col("pos_x", tick) - trace.Col("pos_x", 0));
            Check("hero pos_y", sample.Hero.PosY - arena.HeroStartY, trace.Col("pos_y", tick) - trace.Col("pos_y", 0));
            Check("hero hit_counter", sample.Hero.HitCounter, trace.Col("hit_counter", tick));
            var target = sample.Targets[0];
            Check("target hp", target.Hp, trace.TargetCol(0, "hp", tick));
            Check("target c1cc (damaged ticks)", target.DamagedTicks, trace.TargetCol(0, "c1cc", tick));
            Check("target c1d0 (cooldown)", target.CooldownTicks, trace.TargetCol(0, "c1d0", tick));
            Check("target hit_counter", target.HitCounter, trace.TargetCol(0, "hit_counter", tick));
            Check("target touching", target.Touching, trace.TargetCol(0, "touching_slot", tick));

            var dllSparks = string.Join(";", sample.Sparks.SelectMany(s => Enumerable.Repeat($"{s.X},{s.Y},{s.Z + 1}", NativeEffects.HitSparkCount)));
            var oracleSparks = string.Join(";", trace.Journal(tick).Where(j => j.Kind == "effect" && j.Id == 9).Select(j => $"{(long)Math.Round(j.X * 65536.0)},{(long)Math.Round(j.Y * 65536.0)},{(long)Math.Round(j.Z * 65536.0)}"));
            if (dllSparks != oracleSparks)
            {
                diffs.Add($"{name} tick {tick} sparks: dll [{dllSparks}], oracle [{oracleSparks}]");
            }
        }

        return (diffs, arena, last);
    }

    [Fact]
    public void G7_RunningIntoABeannoid_OnTheRealWorld_IsTheOracle_TheBodyTouchesAtTick60()
    {
        var (diffs, arena, last) = PlayAgainstTheBeannoid("g7/R7-run-into-beannoid");
        using var _ = arena;

        RunMontage.AssertEqualToOracle(diffs);
        Assert.Equal(90, last);
        // Three sparks at the touch of the bodies (the oracle's tick 60); the hero is hurt at 61 (life 10 -> 9, 0x31 for 4 ticks, then the recoil 0x1C).
        Assert.Single(arena.Samples[59].Sparks);
        AssertStates(arena, 3, 26, 60);
        AssertStates(arena, 0x31, 61, 64);
        AssertStates(arena, 0x1C, 65, 75);
        Assert.Equal(10, arena.Samples[59].Hero.Hp);
        Assert.Equal(9, arena.Samples[60].Hero.Hp);
    }

    [Fact]
    public void G7_TheTackle_OnTheRealWorld_HitsTheBeannoidAtTick61_ThreeSparks_ThenTheBodyTouchesAt64AndTheHeroIsHurtAt65()
    {
        var (diffs, arena, last) = PlayAgainstTheBeannoid("g7/tackle/R7-tackle-s44");
        using var _ = arena;

        RunMontage.AssertEqualToOracle(diffs);
        Assert.Equal(96, last);
        AssertStates(arena, 0x29, 1, 25);
        AssertStates(arena, 3, 26, 43);
        AssertStates(arena, 4, 44, 64);
        AssertStates(arena, 0x31, 65, 68); // the body touches at 64 and the hero is hurt at 65: the tackle ends, the life falls, the recoil starts.
        Assert.Equal((10, 9), (arena.Samples[63].Hero.Hp, arena.Samples[64].Hero.Hp));
        Assert.Single(arena.Samples[60].Sparks); // tick 61: the blow of the tackle, three sparks (one hit, three effects).
        Assert.Equal(25, arena.Samples[60].Targets[0].CooldownTicks);
    }

    [Theory]
    [MemberData(nameof(Tackles))]
    public void G7_TheSweepOfThePressTick_OnTheRealWorld_IsTheOracle_TheHurtIncluded(string name)
    {
        var (diffs, arena, _) = PlayAgainstTheBeannoid(name);
        using var _ = arena;

        RunMontage.AssertEqualToOracle(diffs);
    }

    private static void AssertStates(HitArena arena, uint animation, int first, int last)
    {
        for (var t = first; t <= last; t++)
        {
            Assert.True(arena.Samples[t - 1].Hero.AnimTarget == animation, $"tick {t}: the hero's target animation is {arena.Samples[t - 1].Hero.AnimTarget:X}, expected {animation:X}");
        }
    }
}
