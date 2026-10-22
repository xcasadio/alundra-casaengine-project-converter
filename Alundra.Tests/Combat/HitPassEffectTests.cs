#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Combat;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.f3b T1 (docs/plan-e20-heros.md section 1.19.2, D-F3-9, D-F3-10): the effects 4 and 5 of the hit pass (<c>0x800395D4-0x80039668</c>) on hand-made actors and a recording callback. The expectations are the keys of the
/// oracle of E20.f3a T0 (<c>docs/plan-e20-annexe/e20-f3/oracle/out/hitfx_table.json</c>: the real code of <c>ALUN_CD.EXE</c> (France), 160 pairs of an attack nibble and a response byte, read by key, never recomputed here):
/// <c>fx</c> (the effects attached), <c>touch</c> (the store of <c>TouchingEntity</c>), <c>sparks</c>, <c>c1d0</c> (the 25 ticks the target is protected), <c>hitc</c> (the attacker's <c>HitCounter</c>).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class HitPassEffectTests
{
    private sealed class Actor
    {
        public readonly Entity Entity;
        public readonly EntityScriptProxy Proxy;

        public Actor(uint flags, BalanceRecord? record, bool withBox)
        {
            var sprite = new AnimatedSpriteComponent();
            var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
            var data = new Animation2dData { Name = "bank_anim0_down", AnimationType = AnimationType.Loop };
            data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
            var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(1f, spriteId));
            data.Tracks.Add(track);
            var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = 0f };
            if (withBox)
            {
                keyframe.Fixtures.Add(new ColliderFixture { Shape = new Box { Size = new Vector3(8, 8, 8) }, LocalPosition = new Vector3(4, 4, 4) });
            }

            data.CollisionKeyframes.Add(keyframe);
            sprite.AddAnimation(new Animation2d(data));

            Entity = new Entity { Name = "actor", GameplayProxyClassName = typeof(EntityScriptProxy).FullName!, RootComponent = sprite };
            Entity.Initialize();
            Proxy = (EntityScriptProxy)Entity.GameplayProxy;
            Proxy.LogicContextEntity = Entity;
            Proxy.Status = EntityStatus.Normal;
            Proxy.Flags = flags;
            Proxy.PosX = 100 << 16;
            Proxy.PosY = 100 << 16;
            Proxy.PosZ = 0;
            EntitySpawnFactory.SetEntityDimensions(Proxy, 0, 0, 0, 8, 8, 8);
            Proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [0] = new AnimSetEntry { Anim = 0, Acceleration = 0 } };
            Proxy.BalanceRecord = record;
            Proxy.LoadAnimationSwitch(0, 0, fromClock: false);
        }
    }

    /// <summary>
    /// A record whose byte at offset <c>nibble + 2</c> (the one the pass reads) is <paramref name="response"/>: the maximum life for the nibble 0, a response for 1 to 11, the number of attacks for 12, then the bytes of the attacks
    /// (<see cref="BalanceRecord.ByteAt"/>), so that the 160 pairs of the oracle (nibbles 0 to 15) can all be written.
    /// </summary>
    private static BalanceRecord SheetWithByte(int nibble, int response)
    {
        var maxHp = 9;
        var responses = Enumerable.Repeat((byte)16, 11).ToArray();
        var attacks = new List<BalanceAttack>();
        switch (nibble)
        {
            case 0:
                maxHp = response;
                break;
            case <= 11:
                responses[nibble - 1] = (byte)response;
                break;
            case 12:
                attacks.AddRange(Enumerable.Range(0, response).Select(_ => new BalanceAttack(0, 0)));
                break;
            case 13:
                attacks.Add(new BalanceAttack(response, 0));
                break;
            case 14:
                attacks.Add(new BalanceAttack(0, response));
                break;
            default:
                attacks.Add(new BalanceAttack(0, 0));
                attacks.Add(new BalanceAttack(response, 0));
                break;
        }

        return new BalanceRecord(255, 255, maxHp, responses, attacks.ToArray(), null);
    }

    private sealed record Row(int Attribute, int Nibble, int Response, int[] Effects, int Sparks, int CooldownTicks, bool Touched, int HitCounter);

    private static IReadOnlyList<Row> LoadRows(string arrayName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f3", "oracle", "out", "hitfx_table.json");
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.GetProperty(arrayName).EnumerateArray().Select(row => new Row(
                    row.GetProperty("attr").GetInt32(),
                    row.GetProperty("nib").GetInt32(),
                    row.GetProperty("resp").GetInt32(),
                    row.GetProperty("fx").EnumerateArray().Select(e => e.GetInt32()).ToArray(),
                    row.GetProperty("sparks").GetInt32(),
                    row.GetProperty("c1d0").GetInt32(),
                    row.GetProperty("touch").GetInt64() != 0,
                    row.GetProperty("hitc").GetInt32())).ToList();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-f3/oracle/out/hitfx_table.json' found above '{AppContext.BaseDirectory}': this test needs the oracle of E20.f3a");
    }

    /// <summary>One hit of an attacker with the given attribute on a target with the given sheet; returns the journal of what the pass asked for, in order ("fx:4", "spark").</summary>
    private static (List<string> Journal, Actor Attacker, Actor Target, HitPass Pass, int TouchedWhenFirstCalled) Hit(int attribute, BalanceRecord targetSheet, bool withCallback = true)
    {
        var attacker = new Actor(EntityFlags.HitsClassA, new BalanceRecord(255, 17, 9, Enumerable.Repeat((byte)16, 11).ToArray(), new[] { new BalanceAttack(attribute, 1) }, null), withBox: true);
        var target = new Actor(EntityFlags.ClassA, targetSheet, withBox: false);
        var journal = new List<string>();
        var touchedWhenFirstCalled = -1;
        var pass = new HitPass();
        var list = new List<EntityScriptProxy> { attacker.Proxy, target.Proxy };
        Action<EntityScriptProxy, int>? attach = withCallback
            ? (who, id) =>
            {
                if (touchedWhenFirstCalled < 0)
                {
                    touchedWhenFirstCalled = target.Proxy.TouchingEntity == null ? 0 : 1;
                }

                Assert.Same(target.Proxy, who);
                journal.Add("fx:" + id);
            }
            : null;
        pass.Run(list, attacker.Proxy, (_, _, _) => journal.Add("spark"), attach);
        return (journal, attacker, target, pass, touchedWhenFirstCalled);
    }

    private static void AssertRow(Row row)
    {
        var (journal, attacker, target, _, _) = Hit(row.Attribute, SheetWithByte(row.Nibble, row.Response));
        var label = $"attr {row.Attribute:X2} nibble {row.Nibble} response {row.Response:X2}";
        var effects = journal.Where(j => j.StartsWith("fx:", StringComparison.Ordinal)).Select(j => int.Parse(j[3..])).ToArray();
        Assert.True(effects.SequenceEqual(row.Effects), $"{label}: effects [{string.Join(",", effects)}], oracle [{string.Join(",", row.Effects)}]");
        Assert.True(journal.Count(j => j == "spark") == 1, $"{label}: the sparks are asked once per hit (three of them each)");
        Assert.True(row.Sparks == 3, $"{label}: the oracle says three sparks");
        Assert.True((target.Proxy.TouchingEntity != null) == row.Touched, $"{label}: TouchingEntity stored {target.Proxy.TouchingEntity != null}, oracle {row.Touched}");
        Assert.True(target.Proxy.TouchingEntity == null || ReferenceEquals(target.Proxy.TouchingEntity, attacker.Entity), $"{label}: TouchingEntity is the attacker");
        Assert.True(target.Proxy.FrameCollisionTickCounter == row.CooldownTicks, $"{label}: +0x1D0 {target.Proxy.FrameCollisionTickCounter}, oracle {row.CooldownTicks}");
        Assert.True(attacker.Proxy.HitCounter == row.HitCounter, $"{label}: HitCounter {attacker.Proxy.HitCounter}, oracle {row.HitCounter}");
    }

    // (e1) The 160 pairs of the oracle, replayed by their keys; the immune rows (response 0x80, 0xBF: no effect, no store, but +0x1D0 = 25, HitCounter 1 and three sparks) are in the same loop as the others.
    [Fact]
    public void E1_The160PairsOfTheOracle_AttachTheEffects_AsTheBinary()
    {
        var rows = LoadRows("rows");
        Assert.Equal(160, rows.Count);
        Assert.Equal(16, rows.Select(r => r.Nibble).Distinct().Count());
        Assert.Equal(10, rows.Select(r => r.Response).Distinct().Count());
        Assert.Contains(rows, r => r.Effects.Length == 0 && !r.Touched && r.CooldownTicks == 25 && r.HitCounter == 1); // the immune rows are present
        Assert.Contains(rows, r => r.Effects.SequenceEqual(new[] { 4 }) && r.Touched);
        Assert.Contains(rows, r => r.Effects.SequenceEqual(new[] { 5 }) && r.Touched);
        foreach (var row in rows)
        {
            AssertRow(row);
        }
    }

    // (e1, equipment) The eight variants of the oracle (attribute with and without the high bits, response 16): the high bits of the attribute do not change the effect.
    [Fact]
    public void E1_TheEquipmentVariantsOfTheOracle_AttachTheSameEffects()
    {
        var rows = LoadRows("equipment_variants");
        Assert.Equal(8, rows.Count);
        foreach (var row in rows)
        {
            AssertRow(row);
        }
    }

    // (e2) The order of the binary: the effect is asked first, then the sparks, and TouchingEntity is still null when the effect is asked (the store comes after).
    [Fact]
    public void E2_TheEffectIsAskedBeforeTheStoreOfTouchingEntity_AndBeforeTheSparks()
    {
        foreach (var (attribute, effect) in new[] { (6, 4), (10, 4), (7, 5), (9, 5) })
        {
            var (journal, attacker, target, _, touchedWhenFirstCalled) = Hit(attribute, SheetWithByte(attribute, 16));
            Assert.Equal(new[] { "fx:" + effect, "spark" }, journal);
            Assert.Equal(0, touchedWhenFirstCalled);
            Assert.Same(attacker.Entity, target.Proxy.TouchingEntity); // stored after, for the same hit
        }

        // The same hit with a nibble that has no effect: only the sparks; and with no callback at all the pass behaves as before.
        Assert.Equal(new[] { "spark" }, Hit(1, SheetWithByte(1, 16)).Journal);
        var (silent, _, silentTarget, silentPass, _) = Hit(6, SheetWithByte(6, 16), withCallback: false);
        Assert.Equal(new[] { "spark" }, silent);
        Assert.NotNull(silentTarget.Proxy.TouchingEntity);
        Assert.Equal(1, silentPass.HitsWritten);
    }

    // (e3) The hero target (sheet 0 of the binary: the entry 0, null responses): the effect is asked for the nibbles 6, 7, 9 and 10, never filtered (D-F3-10).
    [Fact]
    public void E3_AHeroTargetWithNullResponses_GetsTheEffectsOfTheNibbles6_7_9_10()
    {
        var heroSheet = new BalanceRecord(255, 17, 9, new byte[11], Array.Empty<BalanceAttack>(), null);
        foreach (var (attribute, effect) in new[] { (6, 4), (7, 5), (9, 5), (10, 4) })
        {
            Assert.Equal(new[] { "fx:" + effect, "spark" }, Hit(attribute, heroSheet).Journal);
        }

        foreach (var attribute in new[] { 1, 2, 3, 4, 5, 8, 11 })
        {
            Assert.Equal(new[] { "spark" }, Hit(attribute, heroSheet).Journal);
        }
    }

    // (e4) One effect per hit, not per pass: one attacker of the nibble 6 hits two targets in the same pass and the effect 4 is asked for each of them, in the order of the list, each before its own spark; a second attacker of the nibble 7,
    // whose targets are already in the cooldown of the first hit, asks for nothing.
    [Fact]
    public void E4_OneEffectPerHit_NotPerPass()
    {
        var sheet = new BalanceRecord(255, 17, 9, Enumerable.Repeat((byte)16, 11).ToArray(), new[] { new BalanceAttack(6, 1) }, null);
        var sheet7 = new BalanceRecord(255, 17, 9, Enumerable.Repeat((byte)16, 11).ToArray(), new[] { new BalanceAttack(7, 1) }, null);
        var first = new Actor(EntityFlags.HitsClassA, sheet, withBox: true);
        var second = new Actor(EntityFlags.HitsClassA, sheet7, withBox: true);
        var targetA = new Actor(EntityFlags.ClassA, SheetWithByte(6, 16), withBox: false);
        var targetB = new Actor(EntityFlags.ClassA, SheetWithByte(6, 16), withBox: false);
        var journal = new List<string>();
        var pass = new HitPass();
        var list = new List<EntityScriptProxy> { first.Proxy, second.Proxy, targetA.Proxy, targetB.Proxy };
        pass.Run(
            list,
            first.Proxy,
            (_, _, _) => journal.Add("spark"),
            (who, id) => journal.Add((ReferenceEquals(who, targetA.Proxy) ? "A" : ReferenceEquals(who, targetB.Proxy) ? "B" : "?") + ":fx" + id));

        Assert.Equal(new[] { "A:fx4", "spark", "B:fx4", "spark" }, journal);
        Assert.Equal(2, pass.HitsWritten);
    }
}
