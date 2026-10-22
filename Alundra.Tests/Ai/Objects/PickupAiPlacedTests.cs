#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Rendering.Effects;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c T4 (docs/plan-e20-heros.md section 1.26, phase D, tests (p1) to (p8) at the level of the handlers): the branch of the PLACED object (<c>+0x274 == 0</c>) of A255 (<c>0x80061BD4</c>) and C255 (<c>0x8007C174</c>), against the 258 vectors
/// of the placed objects that T0 made from the real code of the binary (<c>docs/plan-e20-annexe/e20-e0c/exec/vectors/c255.json</c>, the journal with the fanfare as a stub, 824 ticks, 31 pickups of which 16 key objects, 28 vectors that start in the state 2):
/// the words, the sound, the message and its control mode, the close masks, the <c>Block</c> and <c>Unblock</c>, the fanfare stub and the light destruction, tick by tick.
/// The world tests (the real map, the real dialogue box, the real <c>Block</c>) are in <see cref="PickupAiPlacedWorldTests"/>.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PickupAiPlacedTests : IDisposable
{
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    private const string Gained = "gagné!";

    public PickupAiPlacedTests()
    {
        EtcStringTable.ResetForTests();
        LoadEtc();
        PickupAi.FanfareStubCallsForTests = new List<string>();
    }

    public void Dispose()
    {
        PickupAi.FanfareStubCallsForTests = null;
        EtcStringTable.ResetForTests();
    }

    /// <summary>The name of the object <paramref name="item"/> in these tests, and the text the binary builds with it (the string of ETC 0x45 has its leading space put back, T0 (h)).</summary>
    private static string NameOf(int item) => "Obj" + item.ToString("X2");

    private static string MessageOf(int item) => NameOf(item) + " " + Gained;

    private static void LoadEtc()
    {
        var entries = Enumerable.Range(0, 0x58).Select(item => (0x200 + item, NameOf(item))).Append((0x45, Gained)).ToArray();
        EtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc(entries));
    }

    private static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    private static int I(JsonElement e) => unchecked((int)e.GetInt64());

    private static EntityScriptProxy Box(JsonElement position, JsonElement size)
    {
        var p = position.EnumerateArray().Select(I).ToArray();
        var s = size.EnumerateArray().Select(I).ToArray();
        return new EntityScriptProxy { ModdedPosX = p[0], ModdedPosY = p[1], ModdedPosZ = p[2], Width = s[0], Height = s[1], Depth = s[2] };
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p1) A255 (0x80061BD4), the placed object: +0x6C &= ~0x80 before any test, then +0x27C = 0, +0x280 = -1, +0x284 = 0, +0x278 = +0x40.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P1_A255_OfAPlacedObject_ClearsCollidable_AndWritesTheFourWordsOfTheBinary()
    {
        var entity = new EntityScriptProxy { SpriteType = 71, Flags = 0xFFFFFFFF, ItemState = 9, DelayOrAngleOrEntityId = 99, ContentsGameFlag = 0 };
        PickupAi.WriteWord280(entity, 5);
        PickupAi.WriteWord284(entity, 7);

        PickupAi.Load(entity);

        Assert.Equal(0xFFFFFFFF & ~EntityFlags.Collidable, entity.Flags);
        Assert.Equal((0, 0xFFFFFFFFu, 0u, 0), (entity.ItemState, PickupAi.ReadWord280(entity), PickupAi.ReadWord284(entity), entity.DelayOrAngleOrEntityId));
    }

    [Fact]
    public void P1_A255_OfADroppedObject_ReturnsAtOnce_AndTheChestIsANamedStub_TheirFlagsAndWordsAreKept()
    {
        foreach (var kind in new[] { PickupAi.KindDropped, 2u })
        {
            var entity = new EntityScriptProxy { SpriteType = 71, Flags = 0xFFFFFFFF, ItemState = 9, DelayOrAngleOrEntityId = 99 };
            PickupAi.WriteWord274(entity, kind);
            PickupAi.WriteWord280(entity, 5);
            PickupAi.WriteWord284(entity, 7);

            PickupAi.Load(entity);

            Assert.Equal(0xFFFFFFFFu, entity.Flags); // Collidable is NOT cleared: the drop cleared it already (a dropped object) or the chest is E14's.
            Assert.Equal((9, 5u, 7u, 99), (entity.ItemState, PickupAi.ReadWord280(entity), PickupAi.ReadWord284(entity), entity.DelayOrAngleOrEntityId));
        }

        var placed = new EntityScriptProxy { Flags = 0xFFFFFFFF };
        PickupAi.Load(placed);
        Assert.Equal(0xFFFFFFFF & ~EntityFlags.Collidable, placed.Flags); // the control: the same call on a placed object clears it.
    }

    [Fact]
    public void P1_A255_OfAPlacedObjectWithAFlag_IsANamedStub_ItClearsCollidableAndWritesNothingMore_D_E0C_3()
    {
        var entity = new EntityScriptProxy { Flags = 0xFFFFFFFF, ItemState = 9, DelayOrAngleOrEntityId = 99, ContentsGameFlag = 77 };
        PickupAi.WriteWord280(entity, 5);
        PickupAi.WriteWord284(entity, 7);

        PickupAi.Load(entity);

        Assert.Equal(0xFFFFFFFF & ~EntityFlags.Collidable, entity.Flags);
        Assert.Equal((9, 5u, 7u, 99), (entity.ItemState, PickupAi.ReadWord280(entity), PickupAi.ReadWord284(entity), entity.DelayOrAngleOrEntityId));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // C255, the placed object, against the 258 vectors of the oracle (the fanfare as a stub): (p2), (p3), (p4), (p7) at every tick.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class Mount
    {
        public PickupHost Host { get; }
        public FakeDialogueDirector Dialogue { get; } = new();
        public RecordingSoundPlayer Sounds { get; } = new();
        public GameState State { get; } = new();
        public EntityScriptProxy Hero { get; }
        public EntityScriptProxy Entity { get; }

        public Mount(JsonElement vector)
        {
            var e = vector.GetProperty("entity");
            var item = vector.GetProperty("item").GetInt32();
            Host = new PickupHost { Tables = Tables, Sound = Sounds, Dialogue = Dialogue };
            Hero = Box(vector.GetProperty("hero").GetProperty("pos"), vector.GetProperty("hero").GetProperty("size"));
            Hero.Hp = 5;
            Hero.HpMax = 10;
            Hero.LifePulledFromStats = true;
            Host.Hero = Hero;
            Entity = Box(e.GetProperty("pos"), e.GetProperty("size"));
            Entity.SpriteType = item + 0x1E;
            Entity.MapGravityRaw = vector.GetProperty("gravity").GetInt32();
            PickupAi.WriteWord274(Entity, U(e.GetProperty("word274")));
            Entity.DelayOrAngleOrEntityId = I(e.GetProperty("word278"));
            Entity.ItemState = e.GetProperty("state27C").GetInt32();
            PickupAi.WriteWord280(Entity, U(e.GetProperty("word280")));
            PickupAi.WriteWord284(Entity, U(e.GetProperty("word284")));
            Entity.ForceZ = I(e.GetProperty("forceZ"));
            Entity.IsOnGround = e.GetProperty("onGround").GetInt32();
            Entity.DamagedTickCounter = I(e.GetProperty("f1CC"));
        }
    }

    [Fact]
    public void TheOracle_Holds258PlacedVectors_824Ticks_31Pickups_16OfThemKeyObjects_28StartingInTheState2()
    {
        using var document = AnnexFiles.ReadVector("c255.json");
        var placed = document.RootElement.GetProperty("vectors").EnumerateArray().Where(v => v.GetProperty("placed").GetBoolean()).ToList();
        var steps = placed.SelectMany(v => v.GetProperty("projected").EnumerateArray()).ToList();

        Assert.Equal(258, placed.Count);
        Assert.Equal(824, steps.Count);
        Assert.Equal(31, steps.Count(s => s.GetProperty("journal").EnumerateArray().Any(j => j[0].GetString() == "give")));
        Assert.Equal(16, steps.Count(s => s.GetProperty("journal").EnumerateArray().Any(j => j[0].GetString() == "block")));
        Assert.Equal(28, placed.Count(v => v.GetProperty("entity").GetProperty("state27C").GetInt32() == 2));
    }

    [Fact]
    public void ThePlacedVectors_AreFollowedWordForWordAndEventForEvent_AtEveryTick_TheMessageTheSoundTheMasksTheBlockTheFanfareStubAndTheDestruction()
    {
        using var document = AnnexFiles.ReadVector("c255.json");
        var vectors = document.RootElement.GetProperty("vectors").EnumerateArray().Where(v => v.GetProperty("placed").GetBoolean()).ToList();
        var ticks = 0;
        var pickups = 0;
        var keys = 0;
        var messages = 0;

        foreach (var vector in vectors)
        {
            var id = vector.GetProperty("id").GetString();
            var item = vector.GetProperty("item").GetInt32();
            var mount = new Mount(vector);
            var twin = new Mount(vector);
            var host = mount.Host;
            var entity = mount.Entity;
            var flag = U(vector.GetProperty("entity").GetProperty("word280"));
            var flagWord = mount.State.GetFlag(flag);
            var ground = vector.GetProperty("groundAfterTick").EnumerateArray().ToArray();
            var steps = vector.GetProperty("projected").EnumerateArray().ToArray();
            for (var t = 0; t < steps.Length; t++)
            {
                mount.Dialogue.IsOpen = vector.GetProperty("dialogOpen")[t].GetBoolean();
                var soundsBefore = mount.Sounds.Requests.Count;
                var destroyedBefore = host.Destroyed.Count;
                var unblockedBefore = host.Unblocked.Count;
                var blockedBefore = host.Blocked.Count;
                var opensBefore = mount.Dialogue.Opens.Count;
                var masksBefore = mount.Dialogue.CloseMasks.Count;
                var fanfareBefore = PickupAi.FanfareStubCallsForTests!.Count;
                var stateBefore = entity.ItemState;

                PickupAi.Tick(entity, host, mount.State);
                ticks++;

                var after = steps[t].GetProperty("after");
                var journal = steps[t].GetProperty("journal").EnumerateArray().Select(j => j.EnumerateArray().ToArray()).Where(j => j[0].GetString() != "dialogOpen?" && j[0].GetString() != "cdBusy?").ToArray();
                var where = $"{id} (item {item:X2}) tick {t}";
                Assert.True(
                    (I(after.GetProperty("w278")), I(after.GetProperty("s27C")), U(after.GetProperty("w284")), I(after.GetProperty("forceZ")), I(after.GetProperty("f1CC")))
                    == (entity.DelayOrAngleOrEntityId, entity.ItemState, PickupAi.ReadWord284(entity), entity.ForceZ, entity.DamagedTickCounter),
                    $"{where}: the words");

                var gave = journal.Any(j => j[0].GetString() == "give");
                var expectedSounds = journal.Where(j => j[0].GetString() == "sound").Select(j => j[1].GetInt32()).ToArray();
                if (gave)
                {
                    // The sounds GiveItem itself makes (the life vessel) come first: the twin gives the same object in the same state.
                    pickups++;
                    ItemEffects.GiveItem(twin.Host, twin.State, twin.Hero, item);
                    var own = twin.Sounds.Requests.Count;
                    Assert.True(mount.Sounds.Requests.Skip(soundsBefore).Skip(own).SequenceEqual(expectedSounds) && mount.Sounds.Requests.Count - soundsBefore == own + expectedSounds.Length, $"{where}: the sounds [{string.Join(",", mount.Sounds.Requests.Skip(soundsBefore))}]");
                }
                else
                {
                    Assert.True(mount.Sounds.Requests.Skip(soundsBefore).SequenceEqual(expectedSounds), $"{where}: the sounds");
                }

                Assert.Equal(journal.Count(j => j[0].GetString() == "destroyLight"), host.Destroyed.Count - destroyedBefore);
                Assert.Equal(journal.Count(j => j[0].GetString() == "unblock"), host.Unblocked.Count - unblockedBefore);
                Assert.Equal(journal.Count(j => j[0].GetString() == "block"), host.Blocked.Count - blockedBefore);
                Assert.True(journal.Where(j => j[0].GetString() == "closeMask").Select(j => j[1].GetInt32()).SequenceEqual(mount.Dialogue.CloseMasks.Skip(masksBefore)), $"{where}: the close masks");
                Assert.True(
                    journal.Where(j => j[0].GetString() is "fanfareStart" or "fanfareEnd").Select(j => j[0].GetString() == "fanfareStart" ? "start" : "end").SequenceEqual(PickupAi.FanfareStubCallsForTests.Skip(fanfareBefore)),
                    $"{where}: the calls of the fanfare stub");

                var open = journal.SingleOrDefault(j => j[0].GetString() == "open");
                if (open != null)
                {
                    messages++;
                    Assert.Equal(new[] { (MessageOf(item), open[1].GetInt32()) }, mount.Dialogue.Opens.Skip(opensBefore));
                    Assert.Equal(open[1].GetInt32() == 0, journal.All(j => j[0].GetString() != "block")); // mode 0 for an ordinary object, mode 1 for a key object.
                }
                else
                {
                    Assert.Equal(opensBefore, mount.Dialogue.Opens.Count);
                }

                if (journal.Any(j => j[0].GetString() == "block"))
                {
                    keys++;
                    Assert.Same(entity, host.Blocked[^1]);
                }

                if (gave)
                {
                    Assert.True(stateBefore == 1, $"{where}: the pickup is made in the state 1");
                    Assert.Equal(journal.Any(j => j[0].GetString() == "block") ? 2 : 3, entity.ItemState);
                }

                Assert.Equal(flagWord, mount.State.GetFlag(flag)); // (p7) SetGameFlag(-1) of the placed object is not carried: no flag bank moves.

                if (host.Destroyed.Count > destroyedBefore)
                {
                    break; // the vector stops at the light destruction.
                }

                if (ground[t].ValueKind == JsonValueKind.Number)
                {
                    entity.IsOnGround = ground[t].GetInt32();
                }
            }
        }

        Assert.Equal((824, 31, 16, 31), (ticks, pickups, keys, messages));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p4) a dialogue that is already open: nothing until it closes, the first thing after is the pickup.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (EntityScriptProxy Entity, PickupHost Host, FakeDialogueDirector Dialogue, RecordingSoundPlayer Sounds, GameState State) Standing(int item)
    {
        var sounds = new RecordingSoundPlayer();
        var dialogue = new FakeDialogueDirector();
        var hero = new EntityScriptProxy { Width = 10, Height = 10, Depth = 10, Hp = 5, HpMax = 10, LifePulledFromStats = true };
        var host = new PickupHost { Tables = Tables, Sound = sounds, Dialogue = dialogue, Hero = hero };
        var entity = new EntityScriptProxy { SpriteType = item + 0x1E, Width = 10, Height = 10, Depth = 10 };
        PickupAi.Load(entity); // the placed object as the load leaves it (state 0, flag -1, no life rule), then the state 1 the handler reaches from 0.
        entity.ItemState = 1;
        return (entity, host, dialogue, sounds, new GameState());
    }

    [Fact]
    public void P4_AnObjectOnTheHero_DoesNothingWhileADialogueIsOpen_AndIsTakenWithItsMessageAtTheFirstTickAfter()
    {
        var (entity, host, dialogue, sounds, state) = Standing(0x45);
        dialogue.IsOpen = true;

        PickupAi.Tick(entity, host, state);
        PickupAi.Tick(entity, host, state);

        Assert.Equal((1, 0, 0, 0), (entity.ItemState, sounds.Requests.Count, dialogue.Opens.Count, (int)state.PlayerStats.Money));

        dialogue.IsOpen = false;
        PickupAi.Tick(entity, host, state);

        Assert.Equal((3, 1, 1, 1), (entity.ItemState, sounds.Requests.Count, dialogue.Opens.Count, (int)state.PlayerStats.Money));
        Assert.Equal(new[] { (MessageOf(0x45), 0) }, dialogue.Opens);

        // A choice that waits holds it too.
        var (entity2, host2, dialogue2, sounds2, state2) = Standing(0x45);
        dialogue2.IsAwaitingChoice = true;
        PickupAi.Tick(entity2, host2, state2);
        Assert.Equal((1, 0), (entity2.ItemState, sounds2.Requests.Count));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p2) the ordinary object: "name gained!" in mode 0 after the sound, never the state 2, no fanfare, no block; the object goes after the box has closed.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x45, 53)]
    [InlineData(0x55, 52)]
    [InlineData(0x24, 52)]
    [InlineData(0x52, 54)]
    public void P2_AnOrdinaryObject_OpensItsMessageInMode0_AfterItsSound_NeverPassesByTheState2_AndGoesWhenTheBoxIsClosed(int item, int sound)
    {
        var (entity, host, dialogue, sounds, state) = Standing(item);
        Assert.Equal(sound, Tables.DropSoundSfxIndex[item]);

        PickupAi.Tick(entity, host, state);

        Assert.Equal(3, entity.ItemState);
        Assert.Equal(new[] { (MessageOf(item), 0) }, dialogue.Opens);
        Assert.Equal(sound, sounds.Requests[^1]);
        Assert.Empty(dialogue.CloseMasks);          // the register [0x80149CBC] is not written by an ordinary object ...
        Assert.Empty(host.Blocked);                 // ... it blocks nobody ...
        Assert.Empty(PickupAi.FanfareStubCallsForTests!); // ... and plays no fanfare.
        Assert.Empty(host.Destroyed);

        dialogue.IsOpen = true;
        PickupAi.Tick(entity, host, state);
        Assert.Empty(host.Destroyed);               // the box is up: the object waits.

        dialogue.IsOpen = false;
        PickupAi.Tick(entity, host, state);
        Assert.Equal(new[] { entity }, host.Destroyed);
        Assert.Equal(new[] { entity }, host.Unblocked);
        Assert.Equal(3, entity.ItemState);
        Assert.Empty(PickupAi.FanfareStubCallsForTests!);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p3) the key object: Block, the fanfare stub, the message in mode 1, the mask 4, the state 2 (the mask 3), the state 3, Unblock.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x04)]
    [InlineData(0x1F)]
    [InlineData(0x53)]
    public void P3_AKeyObject_BlocksTheWorld_PlaysTheFanfareStub_OpensItsMessageInMode1_ClosableByScriptOnly_ThenTheState2GivesTheMask3Back_AndTheState3Unblocks(int item)
    {
        var (entity, host, dialogue, sounds, state) = Standing(item);
        Assert.Equal(0, Tables.DropSoundSfxIndex[item]);

        PickupAi.Tick(entity, host, state);

        Assert.Equal(2, entity.ItemState);
        Assert.Equal(new[] { entity }, host.Blocked);
        Assert.Equal(new[] { "start" }, PickupAi.FanfareStubCallsForTests);
        Assert.Equal(new[] { (MessageOf(item), 1) }, dialogue.Opens);
        Assert.Equal(new[] { 4 }, dialogue.CloseMasks);
        Assert.Empty(sounds.Requests.Where(s => s is 52 or 53 or 54)); // a key object has no pickup sound (byte 5 of the table is 0).
        Assert.Empty(host.Unblocked);

        // The state 2 does not look at the box (the binary waits for CdBusy(), false in the stub): the mask 3 and the end of the fanfare, whatever the box does.
        dialogue.IsOpen = true;
        PickupAi.Tick(entity, host, state);

        Assert.Equal(3, entity.ItemState);
        Assert.Equal(new[] { 4, 3 }, dialogue.CloseMasks);
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests);
        Assert.Empty(host.Unblocked);
        Assert.Empty(host.Destroyed);

        PickupAi.Tick(entity, host, state); // the state 3 waits for the box to close.
        Assert.Empty(host.Destroyed);

        dialogue.IsOpen = false;
        PickupAi.Tick(entity, host, state);

        Assert.Equal(new[] { entity }, host.Unblocked);
        Assert.Equal(new[] { entity }, host.Destroyed);
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests); // the stub is called once each way.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p5) the heal on the spot with a full inventory: the effect on the hero, the count unchanged, and the message all the same.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Fill(GameState state, int item)
    {
        for (var guard = 0; guard < 120; guard++)
        {
            var before = PlayerManager.GetNumberOfItem(state, item);
            PlayerManager.AddOneItemIfUnlocked(state, Tables, item);
            if (PlayerManager.GetNumberOfItem(state, item) == before)
            {
                return;
            }
        }

        throw new InvalidOperationException("the inventory never fills");
    }

    [Theory]
    [InlineData(0x24)]
    [InlineData(0x25)]
    [InlineData(0x26)]
    [InlineData(0x27)]
    [InlineData(0x29)]
    [InlineData(0x51)]
    [InlineData(0x52)]
    public void P5_TheObjectsOfHeal_WithAFullInventory_AreUsedOnTheSpot_AsGiveItemDoes_AndTheMessageIsStillOpened(int item)
    {
        var (entity, host, dialogue, sounds, state) = Standing(item);
        Fill(state, item);
        state.PlayerStats.MpMax = 4;
        state.PlayerStats.Mp = 1;
        host.Hero!.Hp = 3;
        host.Hero.HpMax = 10;
        var count = PlayerManager.GetNumberOfItem(state, item);

        // The same object given to a twin in the same state: the oracle of the effect.
        var twinState = new GameState();
        Fill(twinState, item);
        twinState.PlayerStats.MpMax = 4;
        twinState.PlayerStats.Mp = 1;
        var twinHero = new EntityScriptProxy { Hp = 3, HpMax = 10, LifePulledFromStats = true };
        var twinHost = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Hero = twinHero };
        ItemEffects.GiveItem(twinHost, twinState, twinHero, item);
        Assert.NotEqual((3, 1), (twinHero.Hp, (int)twinState.PlayerStats.Mp)); // the object heals the twin: the comparison is not vacuous.

        PickupAi.Tick(entity, host, state);

        Assert.Equal((twinHero.Hp, (int)twinState.PlayerStats.Mp), (host.Hero.Hp, (int)state.PlayerStats.Mp));
        Assert.Equal(count, PlayerManager.GetNumberOfItem(state, item)); // the inventory is full for the objects that enter it: the count did not move (the crystals never enter it).
        Assert.Equal(new[] { (MessageOf(item), 0) }, dialogue.Opens);
        Assert.Equal(3, entity.ItemState);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p6) the sparks of the heal go through the queue of the effect stage: nothing in the reservoir and no number drawn at the tick of the pickup.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P6_TheSparksOfThePickup_AreOnTheQueue_TheReservoirIsEmptyAndNoNumberIsDrawnBeforeTheStageRuns_ThenTheyAreCreated()
    {
        var previousSeed = GameRandom.RandomSeed;
        try
        {
            GameRandom.RandomSeed = 12345;
            var (entity, host, dialogue, sounds, state) = Standing(0x29);
            var stage = new EffectStage();
            stage.Load(EffectGolden.BankOf(Root, 476));
            host.Stage = stage;
            Fill(state, 0x29);
            host.Hero!.Hp = 3;
            host.Hero.HpMax = 10;
            var seed = GameRandom.RandomSeed;
            var active = stage.Reservoir.ActiveCount;

            PickupAi.Tick(entity, host, state);

            Assert.True(host.Hero.Hp > 3, "the small potion was drunk on the spot");
            Assert.Equal((active, seed), (stage.Reservoir.ActiveCount, GameRandom.RandomSeed)); // nothing created, nothing drawn at the tick.

            stage.RunFrame(0, Array.Empty<bool>());

            Assert.True(stage.Reservoir.ActiveCount > active, "the queue is applied by the stage");
            Assert.NotEqual(seed, GameRandom.RandomSeed);
        }
        finally
        {
            GameRandom.RandomSeed = previousSeed;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p7) SetGameFlag(-1) of the placed object is not carried (Q-E0C-5, D-E0C-12); the one of a dropped object is.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P7_ThePlacedObject_WritesNoGameFlag_WhileADroppedOneRaisesTheFlagOfItsContainer()
    {
        var (placed, host, dialogue, sounds, state) = Standing(0x45);
        var before = state.GetFlag(0xFFFFFFFF);

        PickupAi.Tick(placed, host, state);

        Assert.Equal(3, placed.ItemState);              // taken ...
        Assert.Equal(1, (int)state.PlayerStats.Money);
        Assert.Equal(before, state.GetFlag(0xFFFFFFFF)); // ... and the bit 31 of the word the binary writes by mistake is not raised.
        Assert.Equal(0xFFFFFFFFu, PickupAi.ReadWord280(placed));

        var (dropped, host2, _, _, state2) = Standing(0x45);
        PickupAi.WriteWord274(dropped, PickupAi.KindDropped);
        PickupAi.WriteWord280(dropped, 100);
        PickupAi.Tick(dropped, host2, state2);
        Assert.NotEqual(0u, state2.GetFlag(100) & (1u << (100 & 31))); // the control: the flag of the container is raised for a dropped object.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p8) the chest: no key (F, 255), A255 and C255 not registered for the type 30, the branch +0x274 == 2 is a named stub.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P8_TheChest_HasNoHandlerInTheRegistry_AndTheBranchOfTheChestInC255IsANamedStubAtEveryState()
    {
        var registry = new NativeSpriteRegistry();
        PickupAi.Register(registry);

        Assert.Equal(2, registry.Count);
        Assert.All(Enumerable.Range(0, 400), type => Assert.False(registry.TryGet(ScriptHelper.ProgramFInteract, PickupAi.Code, type, out _), $"no (F, 255) for the type {type}"));
        Assert.False(registry.TryGet(ScriptHelper.ProgramALoad, PickupAi.Code, 30, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, PickupAi.Code, 30, out _));
        Assert.True(registry.TryGet(ScriptHelper.ProgramALoad, PickupAi.Code, 31, out _)); // the control: a placed type has both.

        foreach (var itemState in new[] { 0, 1, 2, 3 })
        {
            var (entity, host, dialogue, sounds, state) = Standing(0x45);
            PickupAi.WriteWord274(entity, 2);
            entity.ItemState = itemState;
            entity.DelayOrAngleOrEntityId = 5;

            PickupAi.Tick(entity, host, state);

            Assert.Equal((itemState, 5, 0, 0, 0, 0), (entity.ItemState, entity.DelayOrAngleOrEntityId, sounds.Requests.Count, dialogue.Opens.Count, host.Destroyed.Count, (int)state.PlayerStats.Money));
        }
    }
}
