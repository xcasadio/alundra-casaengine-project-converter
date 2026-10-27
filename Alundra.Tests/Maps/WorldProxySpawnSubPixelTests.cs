#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Ai;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;
using Xunit;

namespace Alundra.Tests.Maps;

/// <summary>
/// E20.e0a6 T1 (docs/plan-e20-heros.md section 1.16.6, D-E20-92, D-E0A6-1), (s2): the birth of the rocks of the earth tome 2 (item 0x2C, the sprite 17) through <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>, replayed against the key
/// <c>cast_2c_pf_mid_x600y480</c> of the binary (<c>docs/plan-e20-annexe/e20-h4c2/critic/keys/</c>), at the tick of the birth and at the next one: the birth position is 16.16, with its fraction of pixel, and the force of the tick is added to it.
/// The driver 0x800354D0 below is the bench of the probe of h4c2 (a throwaway port, the births of the phase 0 only, as the probe, without the code of h4c2: no E13, no cancellation): the rock is born by the seam of the birth alone.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WorldProxySpawnSubPixelTests : IDisposable
{
    private const int Rock = 17;
    private const int Item = 0x2C;
    private const int Last = 36; // the births at the ticks 18 and 34 of the key, and the tick that follows each.

    // the table 0x80023504 of the binary: 15 entries (u16 tick, s16 p) and the sentinel (checked against the executable in the annex of h4c2, probes/check_table.py)
    private static readonly int[] TableTick = { 16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 224, 240 };

    private static int TableP(int i) => 512 * (i + 1);

    public WorldProxySpawnSubPixelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private sealed class Sounds : ISoundPlayer
    {
        public void PlaySfx(int sfxId) { }
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() { }
    }

    private static int Driver(EntityScriptProxy hero)
    {
        var world = (IEntityWorldContext)hero.ScriptHost!;
        if (hero.ItemUsePhase != 0)
        {
            return 0;
        }

        var guard = PlayerManager.TomeSlopeGuard(hero);
        if (guard != 0)
        {
            return guard;
        }

        var counter = hero.ItemUseCounter;
        for (var i = 0; i < 15 && counter >= TableTick[i]; i++)
        {
            if (TableTick[i] != counter)
            {
                continue;
            }

            var r1 = EnemyHelpers.Below(32);
            var r2 = EnemyHelpers.Below(32);
            var p = TableP(i) + 0xA00;
            var x = hero.PosX + AnimationTables.OffsetXList[r1] * p;
            var y = hero.PosY + AnimationTables.OffsetYList[r1] * p;
            var z = hero.PosZ + 0x1000000;
            var born = world.SpawnEntityBySpriteIndex(hero, false, Rock, x, y, z, r2);
            if (born is not null)
            {
                var terrain = born.ComputeTerrainHeight();
                if (unchecked(hero.PosZ + 1 + 0x1000000) < terrain)
                {
                    world.DestroyEntity(born);
                }
                else
                {
                    born.ForceZ = unchecked((int)0xFFF80000);
                }
            }
        }

        return 0;
    }

    [Fact]
    public void S2_TheRocksOfTheEarthTome2_BornByTheInterface_AreWhereTheKeyOfTheBinaryPutsThem_AtTheTickOfTheirBirthAndTheNext()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.WaterDropAnnex("e20-h4c2/critic/keys/cast_2c_pf_mid_x600y480.json")));
        var keyed = document.RootElement.GetProperty("rows").EnumerateArray().Take(2).Select(r => (
            Slot: r.GetProperty("slot").GetInt32(),
            Birth: r.GetProperty("birth").GetInt32(),
            Track: r.GetProperty("track").EnumerateArray().Take(2).Select(t => (Tick: t[0].GetInt32(), X: t[3].GetInt32(), Y: t[4].GetInt32())).ToList())).ToList();
        Assert.Equal(new[] { 18, 34 }, keyed.Select(k => k.Birth).ToArray());

        using var arena = new HitArena("e0a6-s2", 100, 100, new List<int[]>(), 24, 0);
        SyntheticField.Install(arena.Arc, null);
        var keep = new HashSet<EntityScriptProxy> { arena.Hero };
        foreach (var other in arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
        {
            other.Status = EntityStatus.FlagToDestroy;
        }

        arena.Arc.Proxy.RecycleDestroyedEntities();
        arena.Arc.PlaceHero(600, 480, 0);
        typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(arena.Arc.Proxy, new Sounds());
        arena.Arc.Give(Item);
        arena.Arc.SetMp(4, 4);
        PlayerManager.SetCurrentItemId(GameState.Instance, Item);
        arena.Hero.FrameCollisionTickCounter = 0;
        arena.Hero.DamagedTickCounter = 0;
        arena.Hero.HitCounter = 0;
        arena.Hero.TouchingEntity = null;

        EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { Rock };
        PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
        PlayerManager.ItemDriverForTests = (hero, _) => Driver(hero);
        arena.Arc.Runner.NativeHandlersEnabled = false;

        var seen = new Dictionary<(int Slot, int Tick), (int X, int Y)>();
        arena.BeforeTick = tick =>
        {
            if (tick == 1)
            {
                Alundra.Rng.GameRandom.RandomSeed = 0x12345678;
            }
        };
        arena.AfterTick = tick =>
        {
            foreach (var rock in arena.Arc.Entities.Where(x => x.SpriteType == Rock && x.Status != EntityStatus.Destroyed))
            {
                seen[(rock.Slot, tick)] = (rock.PosX, rock.PosY);
            }
        };

        arena.Begin();
        for (var tick = 1; tick <= Last; tick++)
        {
            arena.Frame(tick == 1 ? PadState.Circle : 0, 1);
        }

        foreach (var k in keyed)
        {
            foreach (var t in k.Track)
            {
                Assert.True(seen.TryGetValue((k.Slot, t.Tick), out var at), $"the rock of the slot {k.Slot} is absent at the tick {t.Tick}");
                Assert.True((t.X, t.Y) == at, $"slot {k.Slot}, tick {t.Tick}: the key ({t.X}, {t.Y}), the DLL ({at.X}, {at.Y})");
            }
        }
    }
}
