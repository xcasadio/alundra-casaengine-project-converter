#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Props;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;
using Alundra.Tests.Ai.Items;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Props;

/// <summary>
/// E20.h3a T1, phase A (docs/plan-e20-heros.md section 1.30.2, tests (b3) and (b4), the lid half): the native handler E19 (<c>0x8007F690</c>, slot E) of the lids, the sprite types 241 to 243, on an entity made by hand, and its keys on a registry of its own. The expected values are the 25 vectors
/// of E19 that T0 made from the real code of the binary (<c>docs/plan-e20-annexe/e20-h3a/goldens_e19.json</c>, the same method as the critic's vectors of D30 and E12; the real code reads no data word of the entity and writes none): the sound <c>0xE6</c>, then <c>DestroyEntity(E, -1)</c>, in that order.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombLidAiTests : IDisposable
{
    private const string Goldens = "e20-h3a/goldens_e19.json";

    public BombLidAiTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset() => GameState.Instance.ResetForTests();

    /// <summary>A sound player that notes, at each request, how many destructions the host had already recorded (the order of the journal of the real code).</summary>
    private sealed class OrderedSounds : ISoundPlayer
    {
        private readonly CutBench.CutHost _host;

        public OrderedSounds(CutBench.CutHost host) => _host = host;

        public List<(int Sound, int DestroyedBefore)> Requests { get; } = new();

        public void PlaySfx(int sfxId) => Requests.Add((sfxId, _host.Destroyed.Count));

        public void StopSfx(int sfxId)
        {
        }

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

    private static (CutBench Bench, OrderedSounds Sounds) Lid(int type, EntityStatus status = EntityStatus.Deactivated)
    {
        var bench = CutBench.Create(frame: false);
        var sounds = new OrderedSounds(bench.Host);
        bench.Host.SoundPlayer = sounds;
        bench.Entity.SpriteType = type;
        bench.Entity.Status = status;
        return (bench, sounds);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b3) E19.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_E19_TheTwentyFiveVectorsOfTheOracle_AreReplayed_TheSound0xE6_ThenTheDestructionWithTheMinus1_AndNothingElse()
    {
        var vectors = BombVector.Load(Goldens, "E19");

        Assert.Equal(25, vectors.Count);
        foreach (var vector in vectors)
        {
            Assert.Equal(new[] { "sound", "destroy2" }, vector.Journal.Select(j => j.Kind)); // the journal of the real code.
            Assert.Equal(0xE6u, vector.Journal[0].Arguments[0]);
            Assert.Equal(uint.MaxValue, vector.Journal[1].Arguments[1]);
            Assert.Empty(vector.Reads);
            Assert.Empty(vector.Writes);

            var (bench, sounds) = Lid(243);
            var flags = bench.Entity.Flags;
            var animation = bench.Entity.TargetAnimationId;

            BombLidAi.Break(bench.Entity);

            var gone = Assert.Single(bench.Host.Destroyed);
            Assert.Equal((bench.Entity, -1), (gone.Entity, gone.EffectId));
            Assert.Equal(new[] { (0xE6, 0) }, sounds.Requests); // the sound is asked BEFORE the destruction.
            Assert.Equal((flags, animation), (bench.Entity.Flags, bench.Entity.TargetAnimationId)); // no data word written.
        }
    }

    [Theory]
    [InlineData(241)]
    [InlineData(242)]
    [InlineData(243)]
    public void B3_E19_ForEachLidType_TheSoundIs230AndTheDestructionHasTheArgumentMinus1_TheBreakEffectIsTheHeadersOwn(int type)
    {
        var (bench, sounds) = Lid(type);

        BombLidAi.Break(bench.Entity);

        Assert.Equal(230, BombLidAi.BreakSound);
        Assert.Equal(new[] { (230, 0) }, sounds.Requests);
        var gone = Assert.Single(bench.Host.Destroyed);
        Assert.Equal(-1, gone.EffectId); // -1: the break effect of the sprite header (11 for the three lids), never an effect forced by the handler.
        Assert.Same(bench.Entity, gone.Entity);
    }

    [Fact]
    public void B3_E19_WithoutAHost_NothingHappens_WithAHostButNoSoundPlayer_TheLidIsDestroyedAllTheSame()
    {
        var lonely = new EntityScriptProxy { SpriteType = 243, Status = EntityStatus.Deactivated };
        BombLidAi.Break(lonely);
        Assert.Equal(EntityStatus.Deactivated, lonely.Status);

        var (bench, _) = Lid(243);
        bench.Host.SoundPlayer = null;
        BombLidAi.Break(bench.Entity);
        Assert.Single(bench.Host.Destroyed);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b4) the keys.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheLidRegistersE19_ForTheTypes241To243Alone_ThePagesSprites314And315AndTheNeighboursStayOut()
    {
        var registry = new NativeSpriteRegistry();

        using var registration = BombLidAi.Register(registry);

        Assert.Equal(1, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 19) }, BombLidAi.Keys);
        foreach (var type in new[] { 241, 242, 243 })
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 19, type, out var handler), $"(E, 19) must be registered for the type {type}");
            Assert.NotNull(handler);
        }

        foreach (var type in new[] { 0, 2, 240, 244, 314, 315, 258 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 19, type, out _), $"(E, 19) must not be registered for the type {type}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 19, 243, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 19, 243, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 12, 243, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 30, 243, out _));
    }

    [Fact]
    public void B4_TheRegisteredHandlerIsTheLids_TheSoundThenTheDestruction_AndTheDisposableTakesTheKeyOff()
    {
        var registry = new NativeSpriteRegistry();
        var registration = BombLidAi.Register(registry);
        Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 19, 242, out var handler));

        var (bench, sounds) = Lid(242);
        handler!(bench.Entity, bench.Host, bench.Host.GameState);

        Assert.Equal(new[] { (0xE6, 0) }, sounds.Requests);
        Assert.Single(bench.Host.Destroyed);

        registration.Dispose();

        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 19, 242, out _));
    }
}
