#nullable enable
using System;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T1, phase A (docs/plan-e20-heros.md section 1.24.5, test (a3), D-G2C2-3): the native handler D34 (<c>0x8007E8AC</c>, slot D) of the brambles, the global-page sprite types 238 and 239, on an entity made by hand, and its key on a registry of its own. The expected values are the 30 vectors that T0 made
/// from the real code of the binary (<c>docs/plan-e20-annexe/e20-g2c2/vectors/goldens_g2c2.json</c>): the entity that touches the bramble holds an attack (<c>+0x224</c> then <c>+0x1C8</c>, a pointer to the attribute byte); when the low nibble of the byte is 6 (fire) the bramble asks the animation 1 and its flags become
/// <c>(flags | 0x46) &amp; ~0x80</c>; otherwise nothing is written. The binary does not look at the high nibble, a frame box or a zero byte.
/// </summary>
public sealed class BrambleAiTests
{
    private const uint FreeFlags = 0x00AB5A31;

    private const uint FreeAnimation = 9;

    private static Entity Toucher(int? attribute)
    {
        var entity = new Entity { Name = "toucher", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.CurrentAttack = attribute is null ? null : new BalanceAttack(attribute.Value, 1);
        return entity;
    }

    private static EntityScriptProxy Bramble(Entity? toucher, int type = 239)
        => new() { SpriteType = type, Status = EntityStatus.Normal, TouchingEntity = toucher, Flags = FreeFlags, TargetAnimationId = FreeAnimation };

    [Fact]
    public void A3_D34_TheThirtyVectorsOfTheOracle_AreReplayedWordForWord_TheAnimation1AndTheFlagsWithTheBits0x46AtTheNibble6Only()
    {
        var burnt = 0;
        var untouched = 0;

        foreach (var v in TorchVector.Load("D34"))
        {
            var touching = v.Read(0x224);
            var attack = v.WordAt(touching + 0x1C8);
            var attribute = v.ByteAt(attack);
            var bramble = Bramble(Toucher(attribute));
            var flags = v.HasRead(0x6C) ? v.Read(0x6C) : FreeFlags;
            bramble.Flags = flags;

            BrambleAi.Touch(bramble);

            var burns = (attribute & 0xF) == 6;
            Assert.True(!v.Wrote(0x88) || burns, $"vector {v.Index}"); // the oracle writes nothing but at the nibble 6 (a word already at its value is not listed).
            Assert.True(!burns || v.HasRead(0x6C), $"vector {v.Index}");
            Assert.Equal(burns ? 1u : FreeAnimation, bramble.TargetAnimationId);
            Assert.Equal(burns ? (flags | 0x46u) & ~0x80u : flags, bramble.Flags);
            Assert.Equal(v.After(0x6C, flags), bramble.Flags); // and the flags of the oracle: written when they changed.
            Assert.Equal(EntityStatus.Normal, bramble.Status);
            if (burns)
            {
                burnt++;
            }
            else
            {
                untouched++;
            }
        }

        Assert.True(burnt >= 8 && untouched >= 15, $"the vectors cover both classes: burnt {burnt}, untouched {untouched}");
    }

    [Fact]
    public void A3_D34_TheAttributeNibble6AloneBurns_TheHighNibbleDoesNotMatter()
    {
        for (var attribute = 0; attribute < 256; attribute++)
        {
            var bramble = Bramble(Toucher(attribute));

            BrambleAi.Touch(bramble);

            var burns = (attribute & 0xF) == 6;
            Assert.True(burns == (bramble.TargetAnimationId == 1), $"attribute 0x{attribute:X2}");
            Assert.Equal(burns ? (FreeFlags | 0x46u) & ~0x80u : FreeFlags, bramble.Flags);
            if (!burns)
            {
                Assert.Equal(FreeAnimation, bramble.TargetAnimationId);
            }
        }
    }

    [Fact]
    public void A3_D34_TheFlagsBecome_OrWith0x46_ThenWithoutTheBit0x80_ForEveryBit()
    {
        foreach (var flags in Enumerable.Range(0, 32).Select(i => 1u << i).Concat(new[] { 0u, 0xFFFFFFFFu, 0x80u, 0xC3u, 0x47u, 0x3323E4u }))
        {
            var bramble = Bramble(Toucher(6));
            bramble.Flags = flags;

            BrambleAi.Touch(bramble);

            Assert.Equal((flags | 0x46u) & ~0x80u, bramble.Flags);
            Assert.Equal(1u, bramble.TargetAnimationId);
        }
    }

    [Fact]
    public void A3_D34_NoTouchingEntity_OrNoAttack_ChangesNothing_AndTheTypeOfTheBrambleDoesNotMatter()
    {
        var alone = Bramble(null);
        BrambleAi.Touch(alone);
        Assert.Equal((FreeFlags, FreeAnimation), (alone.Flags, alone.TargetAnimationId));

        var unarmed = Bramble(Toucher(null));
        BrambleAi.Touch(unarmed);
        Assert.Equal((FreeFlags, FreeAnimation), (unarmed.Flags, unarmed.TargetAnimationId));

        foreach (var type in new[] { 238, 239 })
        {
            var bramble = Bramble(Toucher(6), type);
            BrambleAi.Touch(bramble);
            Assert.Equal(1u, bramble.TargetAnimationId);
        }
    }

    [Fact]
    public void TheBrambleRegistersD34_ForTheTypes238And239Alone_AndTheDisposableTakesTheKeyOff()
    {
        var registry = new NativeSpriteRegistry();

        var registration = BrambleAi.Register(registry);

        Assert.Equal(1, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramDTouch, 34) }, BrambleAi.Keys);
        Assert.Equal(new[] { 238, 239 }, BrambleAi.SpriteTypes);
        for (var type = 0; type < 600; type++)
        {
            Assert.True((type is 238 or 239) == registry.TryGet(ScriptHelper.ProgramDTouch, 34, type, out _), $"type {type}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 34, 238, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 34, 238, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 30, 238, out _));
        Assert.Throws<InvalidOperationException>(() => BrambleAi.Register(registry));

        Assert.True(registry.TryGet(ScriptHelper.ProgramDTouch, 34, 239, out var handler));
        var burning = Bramble(Toucher(6));
        handler!(burning, CutBenchHost(), new Alundra.Session.GameState());
        Assert.Equal(1u, burning.TargetAnimationId);

        registration.Dispose();

        Assert.Equal(0, registry.Count);
    }

    private static Alundra.Maps.IEntityWorldContext CutBenchHost() => CutBench.Create(frame: false).Host;
}
