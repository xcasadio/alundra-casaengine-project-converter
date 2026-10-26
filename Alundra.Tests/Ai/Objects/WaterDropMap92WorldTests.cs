#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T2, (w8) (docs/plan-e20-heros.md section 1.24.5, "E20.g2c3"): the arc of the map 92 in its REAL world (the production registry, every entity of the map, no synthetic field). Alundra arrives from the map 91 at the tile (38, 10) level 5 (the portal; the preset <c>gouttes-m92</c> is this cell): the four torches of the records 43 to 46 are born (their box holds that tile), the program B[1] of the map
/// (the map event 0, tiles x 26 to 42, y 8 to 28) runs at every tick he is in its zone and waits 240, 240 and 120 ticks, then makes four drops by the opcode <c>0x8A</c> at (756, 168), (804, 168), (852, 168), (900, 168), Z 240, above the four torches (Z 80): the drops are born at the tick 603 of the trace (T0 (d) section 4, S-D9), fall, and the torches
/// go out without a flame (status 3 and the mark at 669, the sound 142 at 670, the destruction at 673, the freeing at 674: the same at 1, 2 and 3 ticks per frame). Born, the four drops HANG at the HEAD (they have no handler): the extinction is what is red before the phase B. Alundra who leaves the zone before the tick 603 makes no drop (the program is reset at every tick outside).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WaterDropMap92WorldTests : IDisposable
{
    private const int BirthTick = 603;
    private static readonly int[] TorchX = { 756, 804, 852, 900 };

    public WaterDropMap92WorldTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public static TheoryData<int> Cadences => new() { 1, 2, 3 };

    private sealed class Sounds : ISoundPlayer
    {
        internal int Tick;
        internal readonly List<(int Tick, int Sfx)> Played = new();

        public void PlaySfx(int sfxId) => Played.Add((Tick, sfxId));

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

    private static ArcRun ArriveFromThe91(string name)
    {
        var spec = new ArcSpec(name, "Magyscar", "Magyscar-92", Array.Empty<int>(), 38, 10, 5, 4000, RealController: true, Prefabs: true, Arrival: new ArcArrival((38 * 24 + 12) << 16, (10 * 16 + 8) << 16, 0, 0x36, 0));
        var arc = new ArcRun(spec);
        try
        {
            arc.HoldDirections(0);
            arc.OneFrame();
            return arc;
        }
        catch
        {
            arc.Dispose();
            throw;
        }
    }

    [Theory]
    [MemberData(nameof(Cadences))]
    public void W8_AliceStayingAtTheBackOfTheRoom92_SeesFourDropsBornAtTheTick603AboveTheFourTorches_WhichGoOutWithoutAFlame_AndAliceWhoLeavesBeforeMakesNone(int ticksPerFrame)
    {
        AssertTheFourDropsPutTheFourTorchesOut(ticksPerFrame);

        // and the same arrival, Alundra leaving the zone at the tick 301: no drop, the torches stay lit.
        AssertThatAliceWhoLeavesTheZoneBeforeTheTick603MakesNoDropAndTheTorchesStayLit(ticksPerFrame);
    }

    private static void AssertTheFourDropsPutTheFourTorchesOut(int ticksPerFrame)
    {
        using var arc = ArriveFromThe91("E20g2c3-w8-" + ticksPerFrame);
        var heard = new Sounds();
        typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, heard);
        var torches = arc.Entities.Where(e => e.SpriteType == 393).OrderBy(e => e.PosX).ToList();
        Assert.Equal(TorchX, torches.Select(t => t.PosX >> 16).ToArray());
        Assert.All(torches, t => Assert.Equal((168, 80), (t.PosY >> 16, t.PosZ >> 16)));
        Assert.Equal(0, arc.Entities.Count(e => e.SpriteType == 391));
        var hp = arc.Hero.Hp;

        var tick = 0;
        var born = new Dictionary<EntityScriptProxy, int>();
        var firstState = new Dictionary<string, int>();
        var flames = 0;
        arc.Proxy.TickTraceForTests = pass =>
        {
            if (pass == "pad")
            {
                tick++;
                heard.Tick = tick;
            }
            else if (pass == "inventory")
            {
                foreach (var drop in arc.Entities.Where(e => e.SpriteType == 391))
                {
                    born.TryAdd(drop, tick);
                }

                flames = Math.Max(flames, arc.Entities.Count(e => e.SpriteType == 12));
                foreach (var (label, wanted) in new[] { ("out", EntityStatus.Deactivated), ("destroy", EntityStatus.FlagToDestroy) })
                {
                    if (torches.All(t => t.Status == wanted && t.Bytes[0] == 1))
                    {
                        firstState.TryAdd(label, tick);
                    }
                }

                if (torches.All(t => !arc.Entities.Contains(t)))
                {
                    firstState.TryAdd("gone", tick);
                }
            }
        };
        for (var t = 1; t <= 700; t += ticksPerFrame)
        {
            arc.HoldDirections(0);
            arc.OneFrame(ticksPerFrame * 0.02f);
        }

        arc.Proxy.TickTraceForTests = null;

        // the four drops, once each, born at the tick 603 over the torches (Z 240), natives (no program C).
        Assert.Equal(4, born.Count);
        Assert.All(born.Values, t => Assert.Equal(BirthTick, t));
        Assert.Equal(TorchX, born.Keys.Select(d => d.PosX >> 16).OrderBy(x => x).ToArray());
        Assert.All(born.Keys, d => Assert.Equal(168, d.PosY >> 16));
        Assert.All(born.Keys, d => Assert.Equal(0, d.Bytes[1]));

        // the four torches: status 3 and the mark at 669, the sound 142 (once each) at 670, the destruction at 673, the freeing at 674; not a flame.
        Assert.Equal(669, firstState["out"]);
        Assert.Equal(new[] { 670, 670, 670, 670 }, heard.Played.Where(p => p.Sfx == 142).Select(p => p.Tick).ToArray());
        Assert.Equal(673, firstState["destroy"]);
        Assert.Equal(674, firstState["gone"]);
        Assert.Equal(0, flames);
        Assert.Equal(hp, arc.Hero.Hp);
    }

    private static void AssertThatAliceWhoLeavesTheZoneBeforeTheTick603MakesNoDropAndTheTorchesStayLit(int ticksPerFrame)
    {
        using var arc = ArriveFromThe91("E20g2c3-w8-out-" + ticksPerFrame);
        var torches = arc.Entities.Where(e => e.SpriteType == 393).ToList();
        Assert.Equal(4, torches.Count);

        var tick = 0;
        var mostDrops = 0;
        arc.Proxy.TickTraceForTests = pass =>
        {
            if (pass == "pad")
            {
                tick++;
            }
            else if (pass == "inventory")
            {
                mostDrops = Math.Max(mostDrops, arc.Entities.Count(e => e.SpriteType == 391));
            }
        };
        for (var t = 1; t <= 700; t += ticksPerFrame)
        {
            if (t == 301)
            {
                // the tile (10, 10) is outside the zone of B[1] (x 26 to 42): the program is reset at every tick from there on.
                arc.PlaceHero(10 * 24 + 12, 10 * 16 + 8, 5 * 16);
            }

            arc.HoldDirections(0);
            arc.OneFrame(ticksPerFrame * 0.02f);
        }

        arc.Proxy.TickTraceForTests = null;
        Assert.True(tick >= 700);
        Assert.Equal(0, mostDrops);
        Assert.All(torches, t => Assert.Equal((EntityStatus.Normal, (byte)0), (t.Status, t.Bytes[0])));
    }
}
