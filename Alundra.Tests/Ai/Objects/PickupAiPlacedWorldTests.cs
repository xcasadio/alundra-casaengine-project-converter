#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c T4 (docs/plan-e20-heros.md section 1.26, phase D, tests (p1) to (p3), (p9), (p11), (p12) in the REAL world): the placed objects of the Debug zone (map 11: 31 objects with the native A255 and C255, 22 key objects, T0 (k)) and of the map 5 (a
/// native object and objects with a script of the map at the slot C), with the real dialogue box, the real <c>Block</c> and <c>Unblock</c> of the world, the real production registry. The handlers against the vectors of the oracle are in <see cref="PickupAiPlacedTests"/>.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PickupAiPlacedWorldTests : IDisposable
{
    private const int SmallPotionRecord = 22;   // type 71, item 0x29, sound 52, native A255 and C255.
    private const int LargePotionRecord = 21;   // type 67, item 0x25.
    private const int KeyRecord = 7;            // type 34, item 0x04, byte 5 of the table zero: a key object.
    private const int Arrow = 3;

    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public PickupAiPlacedWorldTests() => Reset();

    public void Dispose()
    {
        Reset();
        PickupAi.FanfareStubCallsForTests = null;
    }

    private void Reset()
    {
        GameRandom.RandomSeed = _previousSeed;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    private static PickupDropRun DebugZone(string name) => new(name, "Debug zone", "Debug zone-11", 54, 30, -1, -1, clearTheWorld: false);

    private static void Put(EntityScriptProxy entity, int x, int y, int z)
    {
        entity.PosX = x;
        entity.PosY = y;
        entity.PosZ = z;
        entity.TileX = (x >> 16) / 24;
        entity.TileY = (y >> 16) / 16;
        entity.TileZ = z >> 20;
        entity.PushLogicalPositionToRoot();
    }

    /// <summary>Puts the object on the hero (the same point between their ModdedPos), held where it is (no gravity, no force), in the state 1 that waits for the contact.</summary>
    private static void OnTheHero(PickupDropRun run, EntityScriptProxy item)
    {
        var hero = run.Hero;
        item.Flags &= ~EntityFlags.Gravity;
        item.ForceZ = 0;
        item.ItemState = 1;
        Put(item, hero.PosX + hero.ModX - item.ModX, hero.PosY + hero.ModY - item.ModY, hero.PosZ + hero.ModZ - item.ModZ);
    }

    private static bool IsGone(EntityScriptProxy item) => item.Status == EntityStatus.Destroyed || item.Status >= EntityStatus.FlagToDestroy;

    private static EntityScriptProxy Item(PickupDropRun run, int record)
    {
        var item = run.Arc.EntityByRecord(record);
        Assert.NotNull(item);
        Assert.Equal(255, item!.SpriteProgramIndexes[ScriptHelper.ProgramCTick]);
        Assert.Equal(0, item.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f); // the record has no script at the slot C: the native handler runs.
        return item;
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p1) the load of the 31 placed objects of the Debug zone: A255 ran, Collidable is cleared and the words are the binary's.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P1_TheThirtyOnePlacedObjectsOfTheDebugZone_HaveRunA255_CollidableClearedAndTheWordsOfTheBinary()
    {
        using var run = DebugZone("p1");
        var objects = run.Arc.Entities.Where(e => e.SpriteType is >= 31 and <= 118 && e.SpriteType != 72).ToList();

        Assert.Equal(31, objects.Count);
        foreach (var item in objects)
        {
            var where = $"record {item.EntityRefId} type {item.SpriteType}";
            Assert.Equal(0u, item.Flags & EntityFlags.Collidable);
            Assert.True(PickupAi.ReadWord274(item) == 0 && PickupAi.ReadWord280(item) == 0xFFFFFFFFu && PickupAi.ReadWord284(item) == 0 && item.DelayOrAngleOrEntityId == 0, $"{where}: the words");
            Assert.True(item.ItemState is 0 or 1, $"{where}: state {item.ItemState}");
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p2) an ordinary object on the map: the hero walks on it, "name gained!" in mode 0 (the entities stop), the sound, the effect, the object goes after the box.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P2_AnOrdinaryPlacedObject_IsTakenOnContact_MessageInMode0_TheBlockOfTheEntitiesStopsUnderIt_NeverTheState2_AndTheObjectGoesWhenTheBoxCloses()
    {
        using var run = DebugZone("p2");
        var item = Item(run, SmallPotionRecord);
        var potions = PlayerManager.GetNumberOfItem(GameState.Instance, 0x29);
        var director = DialogueDirector.Instance;
        var states = new List<int>();
        var physicsPasses = 0;
        run.OnPass = pass =>
        {
            if (pass == "inventory")
            {
                states.Add(item.ItemState);
            }

            if (pass == "physics")
            {
                physicsPasses++;
            }
        };
        Assert.False(director.IsOpen);
        OnTheHero(run, item);

        Assert.True(run.RunUntil(() => item.ItemState == 3, 12, 1), "the object is taken");

        Assert.True(director.IsOpen);
        Assert.EndsWith(" gagné!", director.CurrentLineForTests!.Text);
        Assert.Equal(GameState.PlayerControlBits.MenuOpen, GameState.Instance.PlayerControlFlags & (GameState.PlayerControlBits.MenuOpen | GameState.PlayerControlBits.MessageBox));
        Assert.Contains(52, run.Sounds.Requests);
        Assert.Equal(potions + 1, PlayerManager.GetNumberOfItem(GameState.Instance, 0x29));
        Assert.Equal(3, director.CloseMaskForTests);    // the mask of an ordinary object is the default one.

        // The block of the entities stops under the message: no entity runs a tick.
        var passes = physicsPasses;
        run.Run(8, 1);
        Assert.Equal(passes, physicsPasses);
        Assert.False(IsGone(item), "the object waits for the box to close");

        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "p2 closes");
        run.Run(4, 1);

        Assert.True(IsGone(item));
        Assert.DoesNotContain(2, states);
        Assert.True(physicsPasses > passes, "the entities run again");
        Assert.Null(item.BlockedByEntity);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p3) a key object: Block of every active entity (the hero included), the message in mode 1 closable by script only (mask 4), the stub of the fanfare, the mask 3 one tick after, Unblock when the box has closed;
    // a blocked entity chooses no program, runs no clock and no physics, then resumes.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P3_AKeyObject_BlocksEveryActiveEntity_OpensItsMessageInMode1WithTheMask4_ThenTheMask3_AndUnblocksWhenTheBoxCloses_ABlockedEntityStandsStill()
    {
        PickupAi.FanfareStubCallsForTests = new List<string>();
        using var run = DebugZone("p3");
        var item = Item(run, KeyRecord);
        var director = DialogueDirector.Instance;
        var context = run.Context;
        var arrow = context.SpawnEntityBySpriteIndex(run.Hero, false, Arrow, 100 << 16, 100 << 16, 0, 0x10);
        Assert.NotNull(arrow);
        run.Run(3, 1);
        Assert.True(arrow!.Status.IsActive());
        OnTheHero(run, item);
        var states = new List<int>();
        run.OnPass = pass =>
        {
            if (pass == "inventory")
            {
                states.Add(item.ItemState);
            }
        };

        Assert.True(run.RunUntil(() => item.ItemState == 2, 12, 1), "the key object is taken");

        // At the end of the tick of the pickup.
        var blocker = item.OwnerEntity ?? item.LogicContextEntity;
        Assert.NotNull(blocker);
        var others = context.SpawnedEntities.Append(run.Hero).Distinct().Where(e => !ReferenceEquals(e, item) && e.Status.IsActive()).ToList();
        Assert.Contains(run.Hero, others);
        Assert.Contains(arrow, others);
        Assert.True(others.Count > 10, "the Debug zone is full of objects");
        Assert.All(others, e => Assert.Same(blocker, e.BlockedByEntity));
        Assert.Null(item.BlockedByEntity);
        Assert.True(director.IsOpen);
        Assert.Equal(4, director.CloseMaskForTests);                                       // closable by script only.
        Assert.Equal(GameState.PlayerControlBits.MessageBox, GameState.Instance.PlayerControlFlags & (GameState.PlayerControlBits.MenuOpen | GameState.PlayerControlBits.MessageBox));
        Assert.Equal(new[] { "start" }, PickupAi.FanfareStubCallsForTests);
        Assert.Empty(run.Sounds.Requests.Where(s => s is 52 or 53 or 54));

        // A blocked entity stands still: the arrow flies no more and the clock of its animation stops; the hero, lifted in the air, does not fall.
        var arrowAt = (arrow.PosX, arrow.PosY, arrow.PosZ, arrow.BoxTicks);
        var hero = run.Hero;
        Put(hero, hero.PosX, hero.PosY, hero.PosZ + (3 << 20));
        var heroAt = hero.PosZ;
        run.Run(6, 1);
        Assert.Equal(3, item.ItemState);
        Assert.Equal(3, director.CloseMaskForTests);                                       // the state 2 gave the mask 3 back ...
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests);         // ... and ended the fanfare, once.
        Assert.Equal(arrowAt, (arrow.PosX, arrow.PosY, arrow.PosZ, arrow.BoxTicks));
        Assert.Equal(heroAt, hero.PosZ);
        Assert.All(others, e => Assert.Same(blocker, e.BlockedByEntity));
        Assert.True(director.IsOpen);

        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "p3 closes");
        run.Run(6, 1);

        Assert.True(IsGone(item));
        Assert.All(others, e => Assert.Null(e.BlockedByEntity));
        Assert.Equal(states.Where(s => s >= 2).OrderBy(s => s), states.Where(s => s >= 2)); // the state never went back.
        Assert.True(hero.PosZ < heroAt, "the hero falls again");
        Assert.NotEqual(arrowAt, (arrow.PosX, arrow.PosY, arrow.PosZ, arrow.BoxTicks));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p9) an object placed on a map with a script of the map at the slot C does not run C255 (the entity code of the record wins, as in the binary); the native object of the same map does. Map 5, records 0 (script 129 at the slot C) and 16
    // (no script), both forced into the world as the map's own program does (opcode 0x2D).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P9_APlacedObjectWithAScriptOfTheMapAtTheSlotC_DoesNotRunC255_WhileTheNativeObjectOfTheSameMapDoes()
    {
        using var run = new PickupDropRun("p9", "Overworld", "Overworld 1,0-5", 8, 6, -1, -1, clearTheWorld: false);
        var scripted = run.Context.SpawnEntityByRecordId(run.Hero, 0);
        var native = run.Context.SpawnEntityByRecordId(run.Hero, 16);
        Assert.NotNull(scripted);
        Assert.NotNull(native);
        Assert.Equal((129, 0), (scripted!.ProgramIndexes[ScriptHelper.ProgramCTick], scripted.ProgramIndexes[ScriptHelper.ProgramALoad])); // the script of the map at the slot C, none at the slot A.
        Assert.Equal(0, native!.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f);
        Assert.Equal(255, scripted.SpriteProgramIndexes[ScriptHelper.ProgramCTick]); // both sprites carry C255.
        Assert.Equal(255, native.SpriteProgramIndexes[ScriptHelper.ProgramCTick]);

        run.Run(10, 1);

        Assert.Equal(1, native.ItemState);   // the control: C255 ran for the native object (the state 0 ends at its first tick) ...
        Assert.Equal(0, scripted.ItemState); // ... and not for the other one.
        Assert.Equal(0u, native.Flags & EntityFlags.Collidable);   // A255 is native for both (the records have no script at the slot A).
        Assert.Equal(0u, scripted.Flags & EntityFlags.Collidable);
        Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu), (PickupAi.ReadWord280(native), PickupAi.ReadWord280(scripted)));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p11) the hero can no longer stand on a placed object (A255 clears Collidable); the control: with Collidable put back he stands on it.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (int HeroZ, bool Rode) DropTheHeroOver(string name, bool collidable)
    {
        using var run = DebugZone(name);
        var item = Item(run, SmallPotionRecord);
        var director = DialogueDirector.Instance;
        director.OpenComposed("held", 1);   // a box held open (closed by script only) keeps the pickup from taking the object while the hero crosses it.
        director.SetCloseMask(4);
        item.Flags &= ~EntityFlags.Gravity;
        if (collidable)
        {
            item.Flags |= EntityFlags.Collidable;
        }

        var rode = false;
        run.Arc.PlaceHero((item.PosX >> 16) + 4, (item.PosY >> 16) + 4, (item.PosZ >> 16) + 100);
        for (var i = 0; i < 60; i++)
        {
            run.Arc.OneFrame();
            rode |= run.Hero.RidingEntity != null;
            if (collidable)
            {
                item.Flags |= EntityFlags.Collidable;
            }
        }

        return (run.Hero.PosZ, rode);
    }

    [Fact]
    public void P11_TheHeroCannotStandOnAPlacedObject_TheGroundIsUnderIt_WhileWithCollidableBackHeStandsOnTop()
    {
        var control = DropTheHeroOver("p11-control", collidable: true);
        Reset();
        var production = DropTheHeroOver("p11", collidable: false);

        Assert.True(control.Rode, "the control: the hero rides the object when it is collidable");
        Assert.Equal(11534336, control.HeroZ);            // the top of the object + 1 (T0 (j)).
        Assert.False(production.Rode);
        Assert.Equal(10485760, production.HeroZ);         // the floor of the Debug zone.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (p12) two placed objects on the hero in the same tick: the second waits for the box of the first to close.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void P12_TwoObjectsOnTheHeroInTheSameTick_TheSecondWaitsForTheBoxOfTheFirstToClose()
    {
        using var run = DebugZone("p12");
        var first = Item(run, SmallPotionRecord);
        var second = Item(run, LargePotionRecord);
        var director = DialogueDirector.Instance;
        OnTheHero(run, first);
        OnTheHero(run, second);
        var ordered = new[] { first, second }.OrderBy(e => e.Slot).ToArray();
        var sounds = run.Sounds.Requests.Count;

        Assert.True(run.RunUntil(() => ordered[0].ItemState == 3, 12, 1), "one of the two is taken");

        Assert.Equal((3, 1), (ordered[0].ItemState, ordered[1].ItemState)); // the lower slot took it, the other one saw the box open.
        Assert.Equal(1, run.Sounds.Requests.Skip(sounds).Count(s => s == 52));
        var opens = director.OpenSerial;

        run.Run(6, 1);
        Assert.Equal((3, 1, opens), (ordered[0].ItemState, ordered[1].ItemState, director.OpenSerial)); // the box is still up: nothing more happens.
        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "p12 closes");
        run.Arc.RunUntil(() => ordered[1].ItemState == 3, "p12 second taken");

        Assert.Equal(opens + 1, director.OpenSerial);   // the second box.
        Assert.Equal(2, run.Sounds.Requests.Skip(sounds).Count(s => s == 52));
        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "p12 closes again");
        run.Run(4, 1);
        Assert.True(IsGone(ordered[0]) && IsGone(ordered[1]));
    }
}
