#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0d T2 (docs/plan-e20-heros.md section 1.26.2, phase B, tests (b3), (b4) and (b10) at the level of the handlers): the object that a chest gives birth to (<c>+0x274 == 2</c>), tick by tick, on bare proxies - A255 (<c>0x80061BD4</c>, the branch
/// of the kind 2) and C255 (<c>0x8007C174</c>, the branch of the kind 2: the countdown of 64 ticks, the states 0, 1 and 2) against the model of the binary (<c>docs/plan-e20-annexe/e20-e0d-disc/chest_sequence.out</c>, <c>c255_whole.dis</c>):
/// <list type="bullet">
/// <item><description>A255: the object is held Loaded (<c>+0x10 = 1</c>: neither simulated nor drawn) while the chest (<c>+0x280</c>, its <see cref="EntityScriptProxy.ParentEntity"/>) is not at the animation 1; at the call that finds it open, a key object plays the track 11 of the CD (a journaled stub);</description></item>
/// <item><description>C255: 64 ticks of countdown, one per tick; then the state 0: the rise stops (<c>+0xB8 = 0</c>), nothing more while a dialogue is open, otherwise the message in MODE 1 - "name gained!" (ETC <c>0x4C</c> empty, the name, ETC <c>0x4D</c>) with the sound of the byte 5 of the table
/// for an ordinary object (the state 2), or the same message and the mask 4 for a key object (the state 1), or "too many things to keep name." (ETC <c>0x4E</c>, the name, ETC <c>0x4F</c>) when the bag cannot take it (<c>+0x284 == 0</c>, the state 2); the state 1 (a key object): the CD is never busy, so the
/// mask 3 and <c>StopAllSound</c> come at the next tick, the box still open; the state 2: once the box is closed, <c>ApplyItem</c> and the flag of the chest (or, for a bag that is full, the chest closed again, no flag), <c>Unblock(chest)</c> and the light destruction of the object.</description></item>
/// </list>
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChestAiObjectTests : IDisposable
{
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());

    private const int Herbs = 0x24;
    private const int Sword = 0x02;
    private const int Gildas30 = 0x48;
    private const int ChestFlag = 1234;

    public ChestAiObjectTests()
    {
        EtcStringTable.ResetForTests();
        var entries = Enumerable.Range(0, 0x58).Select(item => (0x200 + item, "Obj" + item.ToString("X2"))).Concat(new[] { (0x4D, "gagné!"), (0x4E, "TROP"), (0x4F, ".") }).ToArray(); // ETC 0x4C, the empty prefix, has no node in the export: it resolves to an empty line.
        EtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc(entries));
        PickupAi.FanfareStubCallsForTests = new List<string>();
    }

    public void Dispose()
    {
        PickupAi.FanfareStubCallsForTests = null;
        EtcStringTable.ResetForTests();
    }

    private sealed class Rig
    {
        public EntityScriptProxy Chest { get; } = new() { SpriteType = ChestAi.SpriteType, TargetAnimationId = 2, ContentsGameFlag = ChestFlag };

        public EntityScriptProxy Object { get; }

        public EntityScriptProxy Hero { get; } = new() { Width = 10, Height = 10, Depth = 10, Hp = 5, HpMax = 10, LifePulledFromStats = true };

        public PickupHost Host { get; }

        public FakeDialogueDirector Dialogue { get; } = new();

        public RecordingSoundPlayer Sounds { get; } = new();

        public RecordingBgmFadeDirector Bgm { get; }

        public GameState State { get; } = new();

        public Rig(int item, bool take, bool key)
        {
            Chest.ContentsItemId = (uint)item;
            var chestEntity = new Entity();
            chestEntity.SetGameplayProxy(Chest);
            Object = new EntityScriptProxy { SpriteType = item + 0x1E, ParentEntity = chestEntity, ForceZ = 0x8000, DelayOrAngleOrEntityId = 64, ItemState = 0, Flags = 0x8000 };
            PickupAi.WriteWord274(Object, 2);
            PickupAi.WriteWord284(Object, take ? 1u : 0u);
            Object.AIValues[4] = (short)(key ? 1 : 0);
            Host = new PickupHost { Tables = Tables, Sound = Sounds, Dialogue = Dialogue, Hero = Hero };
            Bgm = new RecordingBgmFadeDirector(Host, Dialogue);
            Host.BgmFadeDirector = Bgm;
        }

        public void Tick() => PickupAi.Tick(Object, Host, State);

        public void Ticks(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Tick();
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // A255 of the object (b3, b10).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_A255_HoldsTheObjectLoaded_WhileTheChestIsNotAtTheAnimation1_AndLetsItGoWhenItIs()
    {
        var rig = new Rig(Herbs, take: true, key: false);
        rig.Object.Status = EntityStatus.Normal;

        foreach (var animation in new uint[] { 0, 2, 3 })
        {
            rig.Chest.TargetAnimationId = animation;
            rig.Object.Status = EntityStatus.Normal;
            PickupAi.Load(rig.Object);
            Assert.Equal(EntityStatus.Loaded, rig.Object.Status); // +0x10 = 1: neither simulated nor drawn.
        }

        rig.Chest.TargetAnimationId = 1;
        rig.Object.Status = EntityStatus.Normal;
        PickupAi.Load(rig.Object);

        Assert.Equal(EntityStatus.Normal, rig.Object.Status);
        Assert.Empty(PickupAi.FanfareStubCallsForTests!); // an ordinary object: no track.
        Assert.Equal((64, 0x8000, 2u), (rig.Object.DelayOrAngleOrEntityId, rig.Object.ForceZ, PickupAi.ReadWord274(rig.Object))); // A255 writes nothing else.
    }

    [Fact]
    public void B10_A255_OfAKeyObject_PlaysTheTrack11Once_AtTheCallThatFindsTheChestOpen_AndNotBefore()
    {
        var rig = new Rig(Sword, take: true, key: true);

        rig.Chest.TargetAnimationId = 2;
        PickupAi.Load(rig.Object);
        Assert.Empty(PickupAi.FanfareStubCallsForTests!);

        rig.Chest.TargetAnimationId = 1;
        rig.Object.Status = EntityStatus.Normal;
        PickupAi.Load(rig.Object);

        Assert.Equal(new[] { "track11" }, PickupAi.FanfareStubCallsForTests!.ToArray());
        Assert.Empty(rig.Bgm.Calls); // the music was cut by F255; A255 only starts the track of the CD (a stub until E19.sm7).
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // C255 of the object: the ordinary object (b3).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_C255_OfAnOrdinaryObject_CountsDown64Ticks_ThenStopsTheRise_AndOpensTheMessageInMode1WithTheSoundOfTheTable()
    {
        var rig = new Rig(Herbs, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;

        rig.Ticks(63);

        Assert.Equal((1, 0x8000, 0, 0), (rig.Object.DelayOrAngleOrEntityId, rig.Object.ForceZ, rig.Dialogue.Opens.Count, rig.Sounds.Requests.Count)); // 64 ticks: 63 done, one left; the object still rises.

        rig.Tick();

        Assert.Equal((0, 0x8000, 0, 0), (rig.Object.DelayOrAngleOrEntityId, rig.Object.ForceZ, rig.Dialogue.Opens.Count, rig.Sounds.Requests.Count)); // the 64th tick takes the last one; the rise goes on that tick too.

        rig.Tick(); // the state 0.

        Assert.Equal((0, 2), (rig.Object.ForceZ, rig.Object.ItemState)); // +0xB8 = 0, +0x27C += 2.
        Assert.Equal(new[] { ("Obj24 gagné!", 1) }, rig.Dialogue.Opens.ToArray());
        Assert.Equal(new[] { 52 }, rig.Sounds.Requests.ToArray());     // the byte 5 of the table for the herb.
        Assert.Empty(rig.Dialogue.CloseMasks);                         // the default mask: the player closes the box.
        Assert.Empty(rig.Bgm.Calls);
        Assert.Empty(rig.Host.Destroyed);
    }

    [Theory]
    [InlineData(0x24, 52)]
    [InlineData(Gildas30, 53)]
    [InlineData(0x3D, 52)] // the Key: its byte 5 is 52, it is NOT a key object despite its name.
    public void B3_TheSoundIsTheByte5OfTheTable_AtTheTickOfTheMessage_AndTheMessageNamesTheObject(int item, int sound)
    {
        Assert.Equal(sound, Tables.DropSoundSfxIndex[item]);
        var rig = new Rig(item, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;

        rig.Ticks(66);

        Assert.Equal(new[] { ($"Obj{item:X2} gagné!", 1) }, rig.Dialogue.Opens.ToArray());
        Assert.Equal(new[] { sound }, rig.Sounds.Requests.ToArray());
    }

    [Fact]
    public void B3_C255_WaitsForTheBoxToClose_ThenAppliesTheItem_RaisesTheFlagOfTheChest_UnblocksTheChest_AndDestroysTheObject()
    {
        var rig = new Rig(Herbs, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;
        rig.State.NumberOfItems[Herbs * 2 + 1] = 3;
        rig.Ticks(66);
        Assert.True(rig.Dialogue.IsOpen);

        rig.Ticks(5); // the box is open: the state 2 waits.

        Assert.Equal((2, 3, 0, 0), (rig.Object.ItemState, PlayerManager.GetNumberOfItem(rig.State, Herbs), rig.Host.Destroyed.Count, rig.Host.Unblocked.Count));
        Assert.Equal(0u, rig.State.GetFlag(ChestFlag) & (1u << (ChestFlag & 31)));

        rig.Dialogue.IsOpen = false;
        rig.Tick();

        Assert.Equal(4, PlayerManager.GetNumberOfItem(rig.State, Herbs));                                  // ApplyItem(hero, herb).
        Assert.NotEqual(0u, rig.State.GetFlag(ChestFlag) & (1u << (ChestFlag & 31)));                      // SetGameFlag(chest + 0x40).
        Assert.Equal(new[] { rig.Chest }, rig.Host.Unblocked.ToArray());                                   // Unblock(chest): the world goes on.
        Assert.Equal(new[] { rig.Object }, rig.Host.Destroyed.ToArray());                                  // the light destruction of the object.
        Assert.Equal(1u, rig.Chest.TargetAnimationId);                                                     // the chest stays open.
    }

    [Fact]
    public void B3_ApplyItem_HasNoInventoryFirstRule_AHerbWhoseStockIsAtItsCapIsNeitherAddedNorDrunk()
    {
        var rig = new Rig(Herbs, take: true, key: false); // T4 mutant m09: C255 calling GiveItem (the rule "the inventory first", a refused herb is drunk) instead of ApplyItem.
        rig.Chest.TargetAnimationId = 1;
        rig.State.NumberOfItems[Herbs * 2 + 1] = 9;
        rig.Ticks(66);
        rig.Dialogue.IsOpen = false;

        rig.Tick();

        Assert.Equal(9, PlayerManager.GetNumberOfItem(rig.State, Herbs)); // AddOneItemIfUnlocked refuses a tenth herb,
        Assert.Equal(5, rig.Hero.Hp);                                     // and ApplyItem drinks nothing: the hero is not healed.
    }

    [Fact]
    public void B3_ApplyItem_GivesThe30Florins_ToTheMoneyOfTheHero()
    {
        var rig = new Rig(Gildas30, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;
        rig.Ticks(66);
        rig.Dialogue.IsOpen = false;

        rig.Tick();

        Assert.Equal(30, (int)rig.State.PlayerStats.Money);
    }

    [Fact]
    public void B3_C255_WithADialogueOpenAtTheState0_StopsTheRise_AndOpensNothing_UntilTheBoxIsClosed()
    {
        var rig = new Rig(Herbs, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;
        rig.Ticks(64);
        rig.Dialogue.IsOpen = true; // another box is up when the countdown ends.

        rig.Ticks(3);

        Assert.Equal((0, 0), (rig.Object.ForceZ, rig.Object.ItemState)); // +0xB8 = 0 is written before the test of the dialogue.
        Assert.Empty(rig.Dialogue.Opens);
        Assert.Empty(rig.Sounds.Requests);

        rig.Dialogue.IsOpen = false;
        rig.Tick();

        Assert.Equal(("Obj24 gagné!", 1), rig.Dialogue.Opens.Single());
        Assert.Equal(2, rig.Object.ItemState);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The bag cannot take the object (b4).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheBagIsFull_TheMessageSaysSo_NoSound_NothingIsGiven_NoFlag_TheChestIsClosedAgain_AndTheWorldIsReleased()
    {
        var rig = new Rig(Herbs, take: false, key: false);
        rig.Chest.TargetAnimationId = 1;
        rig.State.NumberOfItems[Herbs * 2 + 1] = 9;
        rig.Ticks(66);

        Assert.Equal(new[] { ("TROP Obj24.", 1) }, rig.Dialogue.Opens.ToArray()); // ETC 0x4E (its trailing space restored), the name, ETC 0x4F.
        Assert.Empty(rig.Sounds.Requests);
        Assert.Equal(2, rig.Object.ItemState);

        rig.Dialogue.IsOpen = false;
        rig.Tick();

        Assert.Equal(9, PlayerManager.GetNumberOfItem(rig.State, Herbs));
        Assert.Equal(0u, rig.State.GetFlag(ChestFlag) & (1u << (ChestFlag & 31)));
        Assert.Equal(0u, rig.Chest.TargetAnimationId);                                // +0x88 = 0: closed, it will open again.
        Assert.Equal(new[] { rig.Chest }, rig.Host.Unblocked.ToArray());
        Assert.Equal(new[] { rig.Object }, rig.Host.Destroyed.ToArray());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The key object (b10): the mask 4, the state 1, the mask 3 and StopAllSound, the box still open.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B10_AKeyObject_OpensTheMessageWithTheMask4_AtTheNextTickTheMask3AndStopAllSoundBringTheMusicBack_TheBoxStillOpen_ThenTheItemIsGivenWhenTheBoxCloses()
    {
        var rig = new Rig(Sword, take: true, key: true);
        rig.Chest.TargetAnimationId = 1;
        rig.Ticks(65); // the countdown and the tick of the state 0.

        Assert.Equal(("Obj02 gagné!", 1), rig.Dialogue.Opens.Single());
        Assert.Equal(1, rig.Object.ItemState);                      // +0x27C += 1.
        Assert.Equal(new[] { 4 }, rig.Dialogue.CloseMasks.ToArray()); // the box is closed by the script only.
        Assert.Empty(rig.Sounds.Requests);                           // no sound: the table's byte 5 is 0, the fanfare is the music.
        Assert.Empty(rig.Bgm.Calls);                                 // the music was cut by F255, long ago.

        rig.Tick(); // the state 1: the CD is never busy.

        Assert.Equal(2, rig.Object.ItemState);
        Assert.Equal(new[] { 4, 3 }, rig.Dialogue.CloseMasks.ToArray());
        Assert.Equal(new[] { "StopAllSound" }, rig.Bgm.Calls.ToArray()); // the music restarts from the top ...
        Assert.True(rig.Dialogue.IsOpen);                                // ... with the box still open.
        Assert.Equal(new[] { "end" }, PickupAi.FanfareStubCallsForTests!.ToArray());

        rig.Ticks(10);
        Assert.Empty(rig.Host.Destroyed);                                // the state 2 waits for the box.
        Assert.Equal(0, PlayerManager.GetNumberOfItem(rig.State, Sword));

        rig.Dialogue.IsOpen = false;
        rig.Tick();

        Assert.Equal(1, PlayerManager.GetNumberOfItem(rig.State, Sword));
        Assert.NotEqual(0u, rig.State.GetFlag(ChestFlag) & (1u << (ChestFlag & 31)));
        Assert.Equal(new[] { rig.Chest }, rig.Host.Unblocked.ToArray());
        Assert.Equal(new[] { rig.Object }, rig.Host.Destroyed.ToArray());
    }

    [Fact]
    public void B10_AKeyObject_StopAllSoundIsNotMovedToTheCloseOfTheBox()
    {
        var rig = new Rig(Sword, take: true, key: true);
        rig.Chest.TargetAnimationId = 1;
        rig.Ticks(66);

        Assert.Equal(new[] { "StopAllSound" }, rig.Bgm.Calls.ToArray());

        rig.Dialogue.IsOpen = false;
        rig.Tick();

        Assert.Equal(new[] { "StopAllSound" }, rig.Bgm.Calls.ToArray()); // once: not again at the close.
    }

    [Fact]
    public void B10_AKeyObjectTheBagCannotTake_IsAnOrdinaryRefusal_NoFanfare_NoMask4()
    {
        var rig = new Rig(Sword, take: false, key: false); // CanTake false: +0x288 stays 0 (F255 writes it only for an object that can be taken).
        rig.Chest.TargetAnimationId = 1;

        rig.Ticks(66);

        Assert.Equal(("TROP Obj02.", 1), rig.Dialogue.Opens.Single());
        Assert.Empty(rig.Dialogue.CloseMasks);
        Assert.Empty(rig.Bgm.Calls);
        Assert.Empty(PickupAi.FanfareStubCallsForTests!);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The branch of the kind 2 answers only for the kind 2.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B7_TheDroppedObject_IsStillTakenByContact_WhileTheSameWordsMadeKind2AreTheChests()
    {
        var rig = new Rig(Herbs, take: true, key: false);
        rig.Chest.TargetAnimationId = 1;
        PickupAi.WriteWord274(rig.Object, PickupAi.KindDropped);
        rig.Object.DelayOrAngleOrEntityId = -1;
        rig.Object.ItemState = 1;
        rig.Hero.ModdedPosX = rig.Object.ModdedPosX;

        rig.Tick();

        Assert.Equal(3, rig.Object.ItemState); // the dropped object: taken at once, the sound, the state 3 (not the chest's countdown).
        Assert.Equal(new[] { 52 }, rig.Sounds.Requests.ToArray());
    }
}
