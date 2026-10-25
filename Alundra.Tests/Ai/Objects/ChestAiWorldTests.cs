#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.SaveGames;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0d T2 (docs/plan-e20-heros.md section 1.26.2, phase B, tests (b3) to (b6), (b9) and (b10) in the REAL world): the hero comes in by a production arrival next to a chest of the exported maps, pushes against it, presses Square on the real controller, and the whole
/// opening is followed tick by tick - the animation 2 and the sound 51, the object that is held Loaded (and not drawn) for the 61 ticks of the animation, the rise and the countdown of 64 ticks, the message in mode 1, the sound of the table, the box closed by the player,
/// <c>ApplyItem</c>, the flag, <c>Unblock</c>. The ticks are counted from the tick of F255 (the binary's tick 0): the binary has the chest open at 62 and the message at 127 (<c>chest_sequence.out</c>); the DLL's order of the passes moves a count by one
/// (docs/plan-e20-heros.md: "à ±1 selon l'ordre des passes de la DLL"), so each count is bounded by the binary's value and the DLL's own count is pinned (see <see cref="Pinned"/>).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChestAiWorldTests : IDisposable
{
    private const int DebugZoneHerbsFlag = 2000;

    public ChestAiWorldTests()
    {
        Reset();
        PickupAi.FanfareStubCallsForTests = new List<string>();
    }

    public void Dispose()
    {
        Reset();
        PickupAi.FanfareStubCallsForTests = null;
    }

    private static void Reset()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    /// <summary>The ticks the DLL takes, counted from the tick of F255 (0): the animation 2 of the chest ends on the tick <see cref="ChestOpen"/>, the message opens <see cref="MessageAfterOpen"/> ticks later (the binary: 62 and 65).</summary>
    private static class Pinned
    {
        public const int ChestOpen = 61;
        public const int MessageAfterOpen = 65;
    }

    private static PickupDropRun Arrive(string name, string zone, string world, int tileX, int tileY, int tileZ, int[]? flags = null)
        => new(name, zone, world, 0, 0, -1, -1, clearTheWorld: false, flags, new ArcArrival((tileX * 24 + 12) << 16, (tileY * 16 + 8) << 16, tileZ << 20, GameState.ResetAnimationId, 0));

    /// <summary>The hero pushes south against the chest that stands on the tile under him (a save cannot carry the direction: it is always south), then lets go of the pad: the contact is made.</summary>
    private static void PushAgainstTheChest(PickupDropRun run)
    {
        run.Run(6, 1);
        run.Arc.HoldDirections(PadState.Down);
        run.Run(8, 1);
        run.Arc.ReleaseDirections();
        run.Run(2, 1);
    }

    private sealed record Moment(int Tick, uint ChestTarget, uint ChestCurrent, bool ObjectExists, EntityStatus ObjectStatus, bool ObjectVisible, bool BoxOpen, string? Text, int CloseMask, int SoundCount, int HeroBlocked, int ObjectBlocked, IReadOnlyList<string> Journal);

    private sealed class Opening
    {
        public PickupDropRun Run { get; }

        public EntityScriptProxy Chest { get; }

        public int ObjectType { get; }

        public int FirstTick { get; private set; }

        public List<Moment> Moments { get; } = new();

        public Opening(PickupDropRun run, EntityScriptProxy chest, int objectType)
        {
            Run = run;
            Chest = chest;
            ObjectType = objectType;
        }

        public EntityScriptProxy? Object => Run.Arc.Entities.FirstOrDefault(e => e.SpriteType == ObjectType && e.Status != EntityStatus.FlagToDestroy && e.Status != EntityStatus.Destroyed && ReferenceEquals(e.ParentEntity, Chest.LogicContextEntity));

        private Moment Snap(int tick)
        {
            var obj = Object;
            var director = DialogueDirector.Instance;
            return new Moment(tick - FirstTick, Chest.TargetAnimationId, Chest.CurrentAnimationId, obj != null, obj?.Status ?? EntityStatus.Destroyed, obj != null && (ArcRun.EntityOf(obj)?.IsVisible ?? false),
                director.IsOpen, director.IsOpen ? director.CurrentLineForTests?.Text : null, director.CloseMaskForTests, Run.Sounds.Requests.Count,
                Run.Hero.BlockedByEntity == null ? 0 : 1, obj?.BlockedByEntity == null ? 0 : 1, PickupAi.FanfareStubCallsForTests!.ToList());
        }

        /// <summary>Presses Square on the frame that follows (the tick 0 is the tick of F255) and follows <paramref name="ticks"/> ticks, one moment per tick.</summary>
        public void PressAndFollow(int ticks)
        {
            FirstTick = Run.Tick + 1;
            Run.AfterTick = tick => Moments.Add(Snap(tick));
            Run.Arc.Press(PadState.Square);
            Run.Run(ticks - 1, 1);
            Run.AfterTick = null;
        }

        public Moment At(int tick) => Moments.Single(m => m.Tick == tick);

        public int FirstTickWhere(Func<Moment, bool> predicate)
        {
            var found = Moments.FirstOrDefault(predicate);
            Assert.True(found != null, "the event never came");
            return found!.Tick;
        }
    }

    private static Opening Mount(PickupDropRun run, int record, int objectType)
    {
        var chest = run.Arc.EntityByRecord(record)!;
        Assert.Equal(30, chest.SpriteType);
        return new Opening(run, chest, objectType);
    }

    private static bool Raised(uint flag) => (GameState.Instance.GetFlag(flag) & (1u << (int)(flag & 31))) != 0;

    private static void CloseTheBoxAsThePlayerDoes(PickupDropRun run)
        => run.Arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => !DialogueDirector.Instance.IsOpen, "the player closes the box");

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b3) the herbs chest of the Debug zone, from the Square to the herb in the bag.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B3_TheHerbsChest_OpensFromTheSquare_TheMessageComesAtTheTickOfTheBinary_ThePlayerClosesTheBox_TheHerbIsGiven_TheFlagRaised_TheWorldReleased()
    {
        using var run = Arrive("b3", "Debug zone", "Debug zone-11", 17, 34, 10);
        GameState.Instance.NumberOfItems[0x24 * 2 + 1] = 3;
        var opening = Mount(run, 37, 0x24 + 0x1E);
        Assert.Equal((0x24u, 0u), (opening.Chest.ContentsItemId, opening.Chest.TargetAnimationId));
        PushAgainstTheChest(run);
        var sounds = run.Sounds.Requests.Count;

        opening.PressAndFollow(140);

        // The tick 0: F255 ran. The chest plays the animation 2 (and its sound 51), the object is born with the chest as its parent, the world is blocked but for the chest and the object.
        var first = opening.At(0);
        Assert.Equal(2u, first.ChestTarget);
        Assert.True(first.ObjectExists, "the object was born at the tick of F255");
        Assert.Equal(1, first.HeroBlocked);
        Assert.Equal(0, first.ObjectBlocked);
        Assert.Contains(51, run.Sounds.Requests.Skip(sounds).Take(opening.At(1).SoundCount - sounds).ToArray()); // the sound 51 comes with the animation, at the tick of the birth or the next.
        Assert.Equal((2u, 0x24 + 0x1E), (PickupAi.ReadWord274(opening.Object!), opening.Object!.SpriteType));

        // The chest open: the animation 2 lasts 61 ticks (chest_anim_oracle.out), then the chain to 1.
        var open = opening.FirstTickWhere(m => m.ChestTarget == 1);
        Assert.InRange(open, 61, 62);
        Assert.Equal(Pinned.ChestOpen, open);

        // The object waits Loaded, and is not drawn, as long as the chest is not open (b9); then it is promoted and drawn (b9).
        foreach (var moment in opening.Moments.Where(m => m.Tick < open))
        {
            Assert.True(moment.ObjectExists && moment.ObjectStatus == EntityStatus.Loaded && !moment.ObjectVisible, $"tick {moment.Tick}: held Loaded and hidden ({moment.ObjectStatus}, visible {moment.ObjectVisible})");
        }

        foreach (var moment in opening.Moments.Where(m => m.Tick > open && m.ObjectExists))
        {
            Assert.True(moment.ObjectStatus == EntityStatus.Normal && moment.ObjectVisible, $"tick {moment.Tick}: promoted and drawn ({moment.ObjectStatus}, visible {moment.ObjectVisible})");
        }

        // The message: 64 ticks of countdown and the tick of the state 0 after the chest is open (the binary: 62 -> 127).
        var message = opening.FirstTickWhere(m => m.BoxOpen);
        Assert.InRange(message, 126, 128);
        Assert.InRange(message - open, 64, 66);
        Assert.Equal(open + Pinned.MessageAfterOpen, message);
        var atMessage = opening.At(message);
        Assert.Equal("Herbes gagné!", atMessage.Text);
        Assert.Equal(3, atMessage.CloseMask);                                    // an ordinary object: the player closes the box.
        Assert.Equal(GameState.PlayerControlBits.MessageBox, GameState.Instance.PlayerControlFlags & (GameState.PlayerControlBits.MessageBox | GameState.PlayerControlBits.MenuOpen)); // mode 1.
        Assert.Contains(52, run.Sounds.Requests.Skip(opening.At(message - 1).SoundCount).ToArray());   // the sound of the byte 5, at the tick of the message,
        Assert.DoesNotContain(52, run.Sounds.Requests.Take(opening.At(message - 1).SoundCount).ToArray()); // not before.
        Assert.Equal(3, GameState.Instance.NumberOfItems[0x24 * 2 + 1]);         // nothing is given yet.
        Assert.False(Raised(DebugZoneHerbsFlag));

        CloseTheBoxAsThePlayerDoes(run);
        run.Run(4, 1);

        Assert.Equal(4, GameState.Instance.NumberOfItems[0x24 * 2 + 1]);         // ApplyItem.
        Assert.True(Raised(DebugZoneHerbsFlag));                                 // SetGameFlag(chest flag).
        Assert.Null(run.Hero.BlockedByEntity);                                   // Unblock(chest).
        Assert.Null(opening.Object);                                             // the object is gone.
        Assert.Equal(1u, opening.Chest.TargetAnimationId);                       // the chest stays open.
        Assert.False(DialogueDirector.Instance.IsOpen);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b4) the bag is full.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B4_TheBagIsFull_TheMessageSaysSo_NothingIsGiven_NoFlag_TheChestClosesAgain_AndOpensAgainWhenThereIsRoom()
    {
        using var run = Arrive("b4", "Debug zone", "Debug zone-11", 17, 34, 10);
        GameState.Instance.NumberOfItems[0x24 * 2 + 1] = 9;
        var opening = Mount(run, 37, 0x24 + 0x1E);
        PushAgainstTheChest(run);
        var sounds = run.Sounds.Requests.Count;

        opening.PressAndFollow(135);

        var message = opening.FirstTickWhere(m => m.BoxOpen);
        var text = opening.At(message).Text!;
        Assert.StartsWith("Tu transportes trop de choses pour", text);
        Assert.EndsWith("garder Herbes.", text);                                  // the space after "garder" is restored.
        Assert.DoesNotContain("gagné", text);
        Assert.DoesNotContain(52, run.Sounds.Requests.Skip(sounds).ToArray());    // no pickup sound.

        CloseTheBoxAsThePlayerDoes(run);
        run.Run(70, 1);

        Assert.Equal(9, GameState.Instance.NumberOfItems[0x24 * 2 + 1]);
        Assert.False(Raised(DebugZoneHerbsFlag));
        Assert.Equal((0u, 0u), (opening.Chest.TargetAnimationId, opening.Chest.CurrentAnimationId)); // closed again.
        Assert.Null(run.Hero.BlockedByEntity);
        Assert.Null(opening.Object);

        // It opens again when there is room: the same Square, the herb is given.
        GameState.Instance.NumberOfItems[0x24 * 2 + 1] = 8;
        var again = new Opening(run, opening.Chest, 0x24 + 0x1E);
        again.PressAndFollow(140);
        Assert.Equal("Herbes gagné!", again.At(again.FirstTickWhere(m => m.BoxOpen)).Text);
        CloseTheBoxAsThePlayerDoes(run);
        run.Run(4, 1);

        Assert.Equal(9, GameState.Instance.NumberOfItems[0x24 * 2 + 1]);
        Assert.True(Raised(DebugZoneHerbsFlag));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b5) an opened chest, saved and loaded: drawn open, F255 answers "empty".
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B5_AnOpenedChest_SavedAndLoaded_IsDrawnOpen_AndF255AnswersThatItIsEmpty()
    {
        SaveGame save;
        using (var first = Arrive("b5a", "Debug zone", "Debug zone-11", 17, 34, 10))
        {
            var opening = Mount(first, 37, 0x24 + 0x1E);
            PushAgainstTheChest(first);
            opening.PressAndFollow(135);
            CloseTheBoxAsThePlayerDoes(first);
            first.Run(4, 1);
            Assert.True(Raised(DebugZoneHerbsFlag));
            save = SaveGame.Capture(GameState.Instance, 11, first.Hero.TileX, first.Hero.TileY, first.Hero.TileZ);
        }

        var raised = new List<int>();
        for (var word = 0; word < 64; word++)
        {
            for (var bit = 0; bit < 32; bit++)
            {
                if ((save.GameFlags[word] & (1u << bit)) != 0)
                {
                    raised.Add(word * 32 + bit);
                }
            }
        }

        Assert.Contains(DebugZoneHerbsFlag, raised);
        Reset();
        using var loaded = Arrive("b5b", "Debug zone", "Debug zone-11", 17, 34, 10, raised.ToArray());
        var chest = loaded.Arc.EntityByRecord(37)!;
        PushAgainstTheChest(loaded);
        var herbs = GameState.Instance.NumberOfItems[0x24 * 2 + 1];

        Assert.Equal((0u, 1u, 1u), (chest.ContentsItemId, chest.TargetAnimationId, chest.CurrentAnimationId)); // drawn open.

        loaded.Arc.Press(PadState.Square);
        loaded.Run(3, 1);

        Assert.True(DialogueDirector.Instance.IsOpen);
        Assert.Contains("reste vide", DialogueDirector.Instance.CurrentLineForTests!.Text); // ETC 0x48.
        Assert.Equal(GameState.PlayerControlBits.MenuOpen, GameState.Instance.PlayerControlFlags & (GameState.PlayerControlBits.MessageBox | GameState.PlayerControlBits.MenuOpen)); // mode 0.
        Assert.Equal(herbs, GameState.Instance.NumberOfItems[0x24 * 2 + 1]);
        Assert.DoesNotContain(loaded.Arc.Entities, e => e.SpriteType == 0x24 + 0x1E);  // no object.
        Assert.Null(loaded.Hero.BlockedByEntity);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b10) a key object: the weapon chest of the test map. The music, the track 11, the mask 4 then 3.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B10_TheSwordChest_CutsTheMusicAtF255_PlaysTheTrack11WhenTheObjectIsLetGo_OpensTheMessageWithTheMask4_TheMask3AndTheMusicComeBackOneTickLater_TheBoxStillOpen()
    {
        using var run = Arrive("b10", "Test Map", "Test Map-0", 13, 2, 0);
        var opening = Mount(run, 2, 2 + 0x1E);
        Assert.Equal(2u, opening.Chest.ContentsItemId);
        PushAgainstTheChest(run);
        var sounds = run.Sounds.Requests.Count;

        opening.PressAndFollow(250);

        var open = opening.FirstTickWhere(m => m.ChestTarget == 1);
        var message = opening.FirstTickWhere(m => m.BoxOpen);

        // The journal of the stubs: the music cut at the tick of F255, the track 11 when A255 lets the object go, StopAllSound at the tick that follows the opening of the message.
        Assert.Equal(new[] { "stop" }, opening.At(0).Journal.ToArray());
        Assert.DoesNotContain("track11", opening.At(open - 1).Journal);
        var trackTick = opening.FirstTickWhere(m => m.Journal.Contains("track11"));
        Assert.InRange(trackTick, open, open + 1);
        Assert.Equal(new[] { "stop", "track11" }, opening.At(message).Journal.ToArray());
        Assert.Equal(open + Pinned.MessageAfterOpen, message);

        Assert.Contains("gagné!", opening.At(message).Text);
        Assert.Equal(4, opening.At(message).CloseMask);                        // the box is closed by the script only ...
        var back = opening.FirstTickWhere(m => m.Journal.Contains("end"));
        Assert.Equal(message + 1, back);                                       // ... and the music comes back at the next tick (the CD is never busy),
        Assert.Equal(3, opening.At(back).CloseMask);                           // the mask 3,
        Assert.True(opening.At(back).BoxOpen);                                 // the box still open.
        Assert.Empty(run.Sounds.Requests.Skip(sounds).Where(s => s is 52 or 53 or 54)); // no pickup sound: the fanfare is the music.
        Assert.Equal(0, PlayerManager.GetNumberOfItem(GameState.Instance, 2));

        CloseTheBoxAsThePlayerDoes(run);
        run.Run(4, 1);

        Assert.Equal(1, PlayerManager.GetNumberOfItem(GameState.Instance, 2));  // ApplyItem(hero, Epee).
        Assert.Null(run.Hero.BlockedByEntity);
        Assert.Null(opening.Object);
        Assert.Equal(new[] { "stop", "track11", "end" }, PickupAi.FanfareStubCallsForTests!.ToArray());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b6) the chain of the story: the key of the map 117, the vase of the map 115.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B6_TheChestOfTheMap117_GivesTheKey_RaisesTheFlag260_AndTheProgramThatWaitsForItGoesOn()
    {
        using var run = Arrive("b6a", "Tarn's Manor", "Tarn's Manor (unused broken)-117", 42, 40, 2);
        var chest = run.Arc.EntityByRecord(19)!;
        Assert.Equal((0x3Du, 260), (chest.ContentsItemId, chest.ContentsGameFlag));
        var opening = new Opening(run, chest, 0x3D + 0x1E);
        var before = run.Arc.Entities.Where(e => e.EntityRefId > 0 && !ReferenceEquals(e, run.Hero)).Select(e => e.EntityRefId).ToHashSet();
        PushAgainstTheChest(run);

        opening.PressAndFollow(135);
        Assert.Equal("Clef gagné!", opening.At(opening.FirstTickWhere(m => m.BoxOpen)).Text);
        Assert.False(Raised(260));
        CloseTheBoxAsThePlayerDoes(run);
        run.Run(120, 1);

        Assert.Equal(1, GameState.Instance.NumberOfItems[0x3D * 2 + 1]);
        Assert.True(Raised(260));
        var after = run.Arc.Entities.Where(e => e.EntityRefId > 0 && !ReferenceEquals(e, run.Hero)).Select(e => e.EntityRefId).ToHashSet();
        Assert.NotEmpty(before.Except(after)); // the program of the map that waits for the flag 260 destroys an entity.
    }

    [Fact]
    public void B6_TheVaseChestOfTheMap115_OpensThePassage_TheScriptThatGuardsTheChestPlaysTheSound223()
    {
        using var run = Arrive("b6b", "Tarn's Manor", "Tarn's Manor-115", 39, 8, 0);
        var chest = run.Arc.EntityByRecord(10)!;
        Assert.Equal((0x53u, 659), (chest.ContentsItemId, chest.ContentsGameFlag));
        var opening = new Opening(run, chest, 0x53 + 0x1E);
        var hpMax = GameState.Instance.PlayerStats.HpMax;
        PushAgainstTheChest(run);
        run.Run(10, 1);
        Assert.DoesNotContain(223, run.Sounds.Requests);

        opening.PressAndFollow(250);
        CloseTheBoxAsThePlayerDoes(run);
        run.Run(10, 1);

        Assert.True(Raised(659));
        Assert.Contains(223, run.Sounds.Requests); // 0x9F [10] sees the chest open and the script plays the sound of the passage.
        Assert.Equal(hpMax + 1, GameState.Instance.PlayerStats.HpMax);  // the life vessel is a key object: ApplyItem(0x53) raised the maximum of life.
    }
}
