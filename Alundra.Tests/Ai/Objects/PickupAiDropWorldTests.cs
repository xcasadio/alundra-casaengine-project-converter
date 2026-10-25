#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c T2 (docs/plan-e20-heros.md section 1.26, phase B, tests (w1) to (w9)): the dropped object in the REAL world of an exported map (real prefabs, the catalogue, the production registry of the native handlers, see
/// <see cref="PickupDropRun"/>), dropped by the production code (<see cref="LootDrop.SpawnContents"/>) and followed tick by tick at 1, 2 and 3 ticks per frame: the rise and the bounces, the contact with the hero, the dialogue that
/// holds the pickup, the life of 600 ticks and its mark at 120, the object without a life rule, the placed object and the chest (named stubs), the whole life after the pickup, and the first tick of the object.
/// The handler is C255 (<c>0x8007C174</c>), its input is what the physics of the previous tick left; the rule it applies is written once more here (<see cref="Reference"/>) from the model of the binary
/// (<c>docs/plan-e20-annexe/e20-e0c/work/c255model.py</c>, 0 difference against the real code in T0) and compared with the words the DLL left at every tick.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PickupAiDropWorldTests : IDisposable
{
    private const int Vessel = 0x53;       // column 0 of the object table is 0: no life rule, GiveItem answers 1.
    private const int Gildas = 0x45;       // 1 gilda, life 600, sound 0x35.
    private const int SmallHeart = 0x54;   // life 600, sound 0x34.
    private const uint FirstRise = 0xA0000;
    private const int Hero0Z = 0;

    private readonly ulong _previousSeed = GameRandom.RandomSeed;
    private readonly ITestOutputHelper _output;

    public PickupAiDropWorldTests(ITestOutputHelper output)
    {
        _output = output;
        Reset();
    }

    public void Dispose() => Reset();

    private void Reset()
    {
        GameRandom.RandomSeed = _previousSeed;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The rule of C255 for a dropped object (+0x274 == 1), from the model of the binary; the input is the words at the head of the programs pass.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>What C255 leaves of the words of a dropped object that nothing touches (the hero is away): the bounce on the ground, the countdown of the life, the mark at 120, the wait for the end of the rise.</summary>
    private static (int Life, int State, uint Bounce, int ForceZ, int Marker, bool Destroyed) Reference(PickupDropRun.Words input, int gravity)
    {
        var bounce = input.Bounce;
        var force = input.ForceZ;
        if (input.OnGround != 0 && bounce != 0)
        {
            var next = unchecked((int)(bounce * 12u)) >> 4;
            bounce = unchecked((uint)next);
            if (!((gravity << 8) < next))
            {
                bounce = 0;
            }

            force = unchecked((int)bounce);
        }

        var life = input.Life;
        var marker = input.Marker;
        if (life != -1 && life != 0)
        {
            life--;
            if (life == 0)
            {
                return (life, input.State, bounce, force, marker, true);
            }

            if (life == 0x78)
            {
                marker = 0x78;
            }
        }

        var state = input.State;
        if (state == 0 && force <= 0)
        {
            state = 1;
        }

        return (life, state, bounce, force, marker, false);
    }

    private static void AssertEveryTickFollowsTheRule(IEnumerable<PickupDropRun.Sample> samples, int gravity)
    {
        var ran = 0;
        foreach (var sample in samples.Where(s => s.CRan))
        {
            ran++;
            var expected = Reference(sample.In, gravity);
            Assert.Equal(
                (expected.Life, expected.State, expected.Bounce, expected.ForceZ, expected.Marker),
                (sample.Out.Life, sample.Out.State, sample.Out.Bounce, sample.Out.ForceZ, sample.Out.Marker));
            if (expected.Destroyed)
            {
                Assert.True(sample.End.Status >= EntityStatus.FlagToDestroy, $"tick {sample.Tick}: the light destruction");
            }
        }

        Assert.True(ran > 0, "C255 ran");
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w1) the rise and the bounce, tick by tick, at the gravity 128 (map 14) and 3 (map 159).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The chain of the bounce words that the binary draws from 0xA0000 at the gravity 128 (report 1.6, replayed on the real code in T0).</summary>
    private static readonly uint[] ChainAtGravity128 = { 0x78000, 0x5A000, 0x43800, 0x32A00, 0x25F80, 0x1C7A0, 0x155B8, 0x1004A, 0xC037, 0x9029, 0 };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W1_TheRiseAndTheBounce_AtGravity128_FollowTheRuleOfTheBinaryAtEveryTick_AndTheChainOfTheBouncesIsTheBinarys(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w1-128-" + ticksPerFrame);
        var drop = run.DropAt(Vessel, 284, 96);
        Assert.Equal(128, drop.MapGravityRaw);

        var settled = run.RunUntil(() => run.Samples.Any(s => s.CRan && s.In.Bounce != 0 && s.Out.Bounce == 0), 700, ticksPerFrame);
        run.Run(6, ticksPerFrame);
        Assert.True(settled, "the last bounce came");

        var cTicks = run.Samples.Where(s => s.CRan).ToList();
        AssertEveryTickFollowsTheRule(run.Samples, 128);

        // The first tick of C255 sees the whole rise still to do; the object waits (state 0) while the force is above zero, and goes to 1 on the first tick where it is not.
        Assert.Equal((FirstRise, 0), (cTicks[0].In.Bounce, cTicks[0].In.State));
        var firstOne = cTicks.First(s => s.Out.State == 1);
        Assert.True(firstOne.In.ForceZ <= 0, "the wait ends when the force is no longer positive");
        Assert.All(cTicks.TakeWhile(s => s.Tick < firstOne.Tick), s => Assert.True(s.Out.ForceZ > 0 && s.Out.State == 0));

        // The chain of the words: the value C255 writes at each tick where the object stands on the ground with a bounce left.
        var landings = cTicks.Where(s => s.In.OnGround != 0 && s.In.Bounce != 0).Select(s => s.Out.Bounce).ToArray();
        Assert.Equal(ChainAtGravity128, landings);

        // The object rose and came back: the height was left and the ground found again.
        Assert.True(cTicks.Max(s => s.End.PosZ) > cTicks[0].In.PosZ + (50 << 16), "the object rose");
        Assert.Equal((1, 0u), (cTicks[^1].End.OnGround, cTicks[^1].End.Bounce));
    }

    /// <summary>
    /// The gravity 3 of the maps 159 and 160 (Fairy cave, underwater; viscosity 256): the world of the maps 159 and 160 does not run the entity block in a test (its entry holds the gate of the gameplay), so the object of the map 14 is given the
    /// two words of those maps (the gravity and the viscosity are read from the entity by the physics and by C255 alike); the rule is followed at each tick up to the first bounce.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W1_TheRiseAndTheFirstBounce_AtGravity3_FollowTheRuleOfTheBinaryAtEveryTick(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w1-3-" + ticksPerFrame);
        var drop = run.DropAt(Vessel, 284, 96);
        Assert.Equal(128, drop.MapGravityRaw);
        drop.MapGravityRaw = 3;
        drop.MapZViscosityRaw = 256;

        var bounced = run.RunUntil(() => run.Samples.Any(s => s.CRan && s.In.OnGround != 0 && s.In.Bounce != 0), 1500, ticksPerFrame);
        run.Run(4, ticksPerFrame);
        Assert.True(bounced, "the first landing came");

        AssertEveryTickFollowsTheRule(run.Samples, 3);
        var landing = run.Samples.First(s => s.CRan && s.In.OnGround != 0 && s.In.Bounce != 0);
        Assert.Equal(0x78000u, landing.Out.Bounce); // 0xA0000 * 12 >> 4, above the threshold 3 << 8.
        Assert.Equal(0x78000, landing.Out.ForceZ);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The parking of an object for the tests of the contact: state 1, no bounce left, no force, no gravity (it stays where the test puts it), no life rule.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Park(EntityScriptProxy item)
    {
        item.ItemState = 1;
        PickupAi.WriteWord284(item, 0);
        item.ForceZ = 0;
        item.DelayOrAngleOrEntityId = -1;
        item.Flags &= ~EntityFlags.Gravity;
    }

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

    /// <summary>Puts the parked object on the hero, shifted by (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>) between the ModdedPos of the two (the point the contact reads).</summary>
    private static void Overlap(PickupDropRun run, EntityScriptProxy item, int dx, int dy, int dz)
    {
        var hero = run.Hero;
        Put(item, hero.PosX + hero.ModX + dx - item.ModX, hero.PosY + hero.ModY + dy - item.ModY, hero.PosZ + hero.ModZ + dz - item.ModZ);
    }

    private static bool PickedUp(EntityScriptProxy item) => item.ItemState != 1;

    /// <summary>The object is out of the world: marked for destruction in this tick, or already recycled (status Destroyed, 0).</summary>
    private static bool IsGone(EntityScriptProxy item) => item.Status == EntityStatus.Destroyed || item.Status >= EntityStatus.FlagToDestroy;

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w2) the contact at the exact bounds of the overlap on the three axes: the size of the box plus one, from the side of the hero and from the side of the object.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    public static IEnumerable<object[]> Bounds()
    {
        foreach (var ticksPerFrame in new[] { 1, 3 })
        {
            foreach (var axis in new[] { 0, 1, 2 })
            {
                foreach (var side in new[] { +1, -1 })
                {
                    if (axis == 2 && side < 0)
                    {
                        continue; // the object would have to be under the ground the hero stands on (the physics lifts it): that bound is read in the vectors of the handler (PickupAiDropVectorTests).
                    }

                    foreach (var beyond in new[] { 0, 1 })
                    {
                        yield return new object[] { ticksPerFrame, axis, side, beyond };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Bounds))]
    public void W2_TheContact_IsMadeAtTheExactBoundOfTheOverlap_SizePlusOne_OnEachAxisAndFromEachSide(int ticksPerFrame, int axis, int side, int beyond)
    {
        using var run = PickupDropRun.OnMap14($"w2-{ticksPerFrame}-{axis}-{side}-{beyond}");
        var item = run.DropAt(Gildas, 284, 96);
        Park(item);

        // The box sizes: the hero's when the object is ahead of him (side +1: pos(object) - pos(hero) < size(hero) + 1), the object's when it is behind (hero - object < size(object) + 1). A prefab keeps its position on the
        // pixel grid (the root of the engine is in floats), so the bound is read at the pixel: the edge is size + 1, a whole number of pixels, which is OUT (the test is strict); one pixel inside it is IN.
        int[] heroSize = { run.Hero.Width, run.Hero.Height, run.Hero.Depth };
        int[] itemSize = { item.Width, item.Height, item.Depth };
        const int Pixel = 1 << 16;
        Assert.Equal((0, 0), ((heroSize[axis] + 1) % Pixel, (itemSize[axis] + 1) % Pixel)); // sizes of the export: whole pixels, minus one raw unit.
        Assert.True(heroSize[axis] > 0, "the hero has a size on this axis");
        var edge = side > 0 ? heroSize[axis] + 1 : -(itemSize[axis] + 1);
        var offset = beyond == 0 ? edge - side * Pixel : edge;
        var delta = new int[3];
        delta[axis] = offset;
        Overlap(run, item, delta[0], delta[1], delta[2]);

        run.Run(4, ticksPerFrame);

        Assert.Equal(beyond == 0, PickedUp(item));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w3) nothing while a dialogue is open; the pickup comes with its closing.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W3_NothingHappensWhileADialogueIsOpen_AndTheObjectIsTakenWhenItCloses(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w3-" + ticksPerFrame);
        var item = run.DropAt(Gildas, 284, 96);
        Park(item);
        Overlap(run, item, 0, 0, 0);
        var money = (int)GameState.Instance.PlayerStats.Money;
        var director = DialogueDirector.Instance;
        director.OpenComposed("test", 1); // mode 1: the hero is held, the world goes on.
        director.SetCloseMask(4); // closed by the script only, so that the box stays up as long as the test wants.
        Assert.True(director.IsOpen);
        var sounds = run.Sounds.Requests.Count; // the opening of the box has its own sound.

        run.Run(6, ticksPerFrame);

        Assert.True(director.IsOpen);
        Assert.Equal((1, money, 0), (item.ItemState, (int)GameState.Instance.PlayerStats.Money, run.Sounds.Requests.Count - sounds));
        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "w3 closes");
        run.Run(4, ticksPerFrame);

        Assert.Equal((3, money + 1), (item.ItemState, (int)GameState.Instance.PlayerStats.Money));
        Assert.Equal(new[] { 0x35 }, run.Sounds.Requests.Skip(sounds).ToArray());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w4) the life of 600 ticks: the object goes out at the 600th tick of C255, the mark of the last 120 ticks is written at 120.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W4_TheObjectGoesOutAtThe600thTick_AndTheMarkOfTheLast120IsWrittenAt120(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w4-" + ticksPerFrame);
        var drop = run.DropAt(Gildas, 284, 96);
        Assert.Equal(600, drop.DelayOrAngleOrEntityId);

        var gone = run.RunUntil(() => drop.Status >= EntityStatus.FlagToDestroy, 900, ticksPerFrame);
        Assert.True(gone, "the object went out");

        var cTicks = run.Samples.Where(s => s.CRan).ToList();
        Assert.Equal(600, cTicks.Count);
        AssertEveryTickFollowsTheRule(run.Samples, 128);
        Assert.Equal(EntityStatus.FlagToDestroy, cTicks[^1].End.Status);
        Assert.All(cTicks.Take(599), s => Assert.True(s.End.Status < EntityStatus.FlagToDestroy));

        // The mark: written (0x78) by the tick where the life reaches 120, then counted down by the counters pass (0x77 at the end of that tick); never before.
        var marked = cTicks.Where(s => s.Out.Marker == 0x78).ToList();
        Assert.Single(marked);
        Assert.Equal((120, 0x77), (marked[0].Out.Life, marked[0].End.Marker));
        Assert.All(cTicks.TakeWhile(s => s.Tick < marked[0].Tick), s => Assert.Equal(0, s.Out.Marker));
        run.Run(2, ticksPerFrame);
        Assert.DoesNotContain(drop, run.Context.SpawnedEntities);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w5) an object without a life rule stays; so does one whose life word is 0.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, -1)]
    [InlineData(3, -1)]
    [InlineData(1, 0)]
    [InlineData(3, 0)]
    public void W5_AnObjectWithoutALifeRule_StaysBeyond600Ticks(int ticksPerFrame, int lifeWord)
    {
        using var run = PickupDropRun.OnMap14($"w5-{ticksPerFrame}-{lifeWord}");
        var drop = run.DropAt(Vessel, 284, 96);
        Assert.Equal(-1, drop.DelayOrAngleOrEntityId); // the table gives no life to the vessel: the drop writes -1.
        drop.DelayOrAngleOrEntityId = lifeWord;

        run.Run(660, ticksPerFrame);

        Assert.Equal(lifeWord, drop.DelayOrAngleOrEntityId);
        Assert.True(drop.Status < EntityStatus.FlagToDestroy);
        Assert.Contains(drop, run.Context.SpawnedEntities);
        Assert.True(run.Samples.Count(s => s.CRan) >= 650);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w6) the effect of the pickup on the hero, and the sound of the byte 5 of the table.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W6_TheGildasAreAddedAndTheirSoundPlays_AtTheTickOfTheContact(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w6-gildas-" + ticksPerFrame);
        var item = run.DropAt(Gildas, 284, 96);
        Park(item);
        Overlap(run, item, 0, 0, 0);
        var money = (int)GameState.Instance.PlayerStats.Money;
        var sounds = run.Sounds.Requests.Count;

        run.Run(4, ticksPerFrame);

        Assert.Equal(money + 1, (int)GameState.Instance.PlayerStats.Money);
        Assert.Equal(new[] { 0x35 }, run.Sounds.Requests.Skip(sounds).ToArray());
        Assert.Equal(3, item.ItemState);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W6_TheSmallHeartHealsTwo_AndItsSoundPlays_AtTheTickOfTheContact(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w6-heart-" + ticksPerFrame);
        var item = run.DropAt(SmallHeart, 284, 96);
        Park(item);
        Overlap(run, item, 0, 0, 0);
        run.Hero.HpMax = 12;
        run.Hero.Hp = 5;
        var sounds = run.Sounds.Requests.Count;

        run.Run(4, ticksPerFrame);

        Assert.Equal(7, run.Hero.Hp);
        Assert.Equal(new[] { 0x34 }, run.Sounds.Requests.Skip(sounds).ToArray());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w7) the object of a chest (+0x274 == 2) is not a dropped one (E20.e0d: its own branch of C255, the countdown of the rise; A255 and C255 do nothing else for it); the same object made "dropped" is taken. (The placed object, +0x274 == 0, was a stub in phase B: since phase D it is taken, PickupAiPlacedTests and PickupAiPlacedWorldTests.)
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2u, 1)]
    [InlineData(2u, 3)]
    public void W7_TheObjectOfAChest_OnlyCountsDownItsRise_WhileTheSameObjectMadeDroppedIsTaken(uint kind, int ticksPerFrame)
    {
        using var run = new PickupDropRun($"w7-{kind}-{ticksPerFrame}", "Debug zone", "Debug zone-11", 54, 30, -1, -1, clearTheWorld: false);
        var item = run.Arc.EntityByRecord(22);
        Assert.NotNull(item);
        Assert.Equal((71, 0, 255), (item!.SpriteType, item.ProgramIndexes[ScriptHelper.ProgramCTick] & 0x7f, item.SpriteProgramIndexes[ScriptHelper.ProgramCTick])); // the Small Potion of the Debug zone: native C255.
        Assert.Equal(0u, PickupAi.ReadWord274(item));
        PickupAi.WriteWord274(item, kind);
        item.DelayOrAngleOrEntityId = 100; // E20.e0d: the countdown of the 64 ticks of the rise of a chest's object, long enough not to end in the 8 ticks below.
        item.Flags &= ~EntityFlags.Gravity;
        Put(run.Hero, item.PosX + item.ModX - run.Hero.ModX, item.PosY + item.ModY - run.Hero.ModY, item.PosZ + item.ModZ - run.Hero.ModZ);
        var money = GameState.Instance.PlayerStats.Money;
        var potions = PlayerManager.GetNumberOfItem(GameState.Instance, 0x29);
        var before = (item.ItemState, PickupAi.ReadWord284(item), item.DelayOrAngleOrEntityId, item.Status, run.Sounds.Requests.Count);

        run.Run(8, ticksPerFrame);

        Assert.True(run.Runs(ScriptHelper.ProgramCTick) > 0, "C255 reached the branch of the chest");
        Assert.Equal((before.Item1, before.Item2, 100 - 8, before.Item4, before.Item5), (item.ItemState, PickupAi.ReadWord284(item), item.DelayOrAngleOrEntityId, item.Status, run.Sounds.Requests.Count)); // one tick of countdown per tick, nothing else (it was: nothing at all).
        Assert.Equal((money, potions), (GameState.Instance.PlayerStats.Money, PlayerManager.GetNumberOfItem(GameState.Instance, 0x29)));

        // The same object, made a dropped one that waits for the contact: taken (the Small Potion enters the inventory, or is drunk when the inventory is full).
        PickupAi.WriteWord274(item, 1);
        item.ItemState = 1;
        item.DelayOrAngleOrEntityId = -1;
        run.Run(4, ticksPerFrame);
        Assert.Equal(3, item.ItemState);
        Assert.Equal(before.Item5 + 1, run.Sounds.Requests.Count);
        Assert.Equal(potions + 1, PlayerManager.GetNumberOfItem(GameState.Instance, 0x29));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w8) from the pickup of a dropped object to its destruction, tick by tick: the states 1 then 3, never 2, nothing written for the fanfare, no message, the object drawn until the destruction; a dialogue that is open holds the state 3.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W8_FromThePickupToTheDestruction_TheStatesAre1Then3Never2_NoMessageNoRegister_TheObjectIsDrawnUntilTheDestruction(int ticksPerFrame)
    {
        using var run = PickupDropRun.OnMap14("w8-" + ticksPerFrame);
        var item = run.DropAt(Gildas, 284, 96);
        Park(item);
        Overlap(run, item, 0, 0, 0);
        var director = DialogueDirector.Instance;
        var closeMask = director.CloseMaskForTests;
        var sounds0 = run.Sounds.Requests.Count;
        var states = new List<int>();
        var opened = false;
        run.OnPass = pass =>
        {
            if (pass == "inventory")
            {
                states.Add(item.ItemState);
                opened |= director.IsOpen;
                Assert.Equal(closeMask, director.CloseMaskForTests); // the register [0x80149CBC] of the fanfare is never written by a dropped object.
            }
        };

        run.Run(6, ticksPerFrame);

        var pickup = run.Samples.First(s => s.Out.State == 3);
        var destruction = run.Samples.First(s => s.End.Status >= EntityStatus.FlagToDestroy);
        Assert.All(run.Samples.Where(s => s.Tick < pickup.Tick && s.CRan), s => Assert.Equal(1, s.Out.State));
        Assert.Equal(pickup.Tick + 1, destruction.Tick);
        Assert.DoesNotContain(2, states);
        Assert.All(run.Samples.Where(s => s.Tick <= pickup.Tick), s => Assert.True(s.End.Status < EntityStatus.FlagToDestroy)); // drawn up to the pickup tick, and still at the end of it
        Assert.False(opened); // no message for a dropped object.
        Assert.Equal(new[] { 0x35 }, run.Sounds.Requests.Skip(sounds0).ToArray());
    }

    [Fact]
    public void W8_ADialogueThatIsOpenHoldsTheState3_AndTheObjectGoesWhenItCloses()
    {
        using var run = PickupDropRun.OnMap14("w8-held");
        var item = run.DropAt(Gildas, 284, 96);
        Park(item);
        Overlap(run, item, 0, 0, 0);
        var director = DialogueDirector.Instance;
        run.AfterTick = _ =>
        {
            if (item.ItemState == 3 && !director.IsOpen && item.Status < EntityStatus.FlagToDestroy)
            {
                director.OpenComposed("test", 1); // between two ticks: the box is up when the state 3 waits for it to close.
                director.SetCloseMask(4);
            }
        };

        run.Run(6, 1);
        Assert.True(director.IsOpen);
        Assert.Equal(3, item.ItemState);
        Assert.False(IsGone(item), "held by the dialogue");

        run.AfterTick = null;
        director.RequestScriptClose();
        run.Arc.RunUntil(() => !director.IsOpen, "w8 closes");
        run.Run(3, 1);
        Assert.True(IsGone(item));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (w9) the first tick of the object born in the program of another entity: its program A one tick after the birth, its first C255 one tick after that, on the ground flag 0; the object of a lower slot is
    // what the binary does, the object of a higher slot is one tick later than the binary (Q-E0C-8, the gap is named and pinned).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class Spy : IEventProgramRunner
    {
        private readonly IEventProgramRunner _inner;
        private readonly Action<EntityScriptProxy> _before;

        public Spy(IEventProgramRunner inner, Action<EntityScriptProxy> before)
        {
            _inner = inner;
            _before = before;
        }

        public void RunScript(EntityScriptProxy entity, int programSlot) => _inner.RunScript(entity, programSlot);

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
            _before(entity);
            _inner.RunSpriteEvent(entity);
        }
    }

    /// <summary>The object is made by the program C of an arrow (sprite 3), at its first tick of C, through <see cref="LootDrop.SpawnContents"/> (or, with <paramref name="throughDestroyEntity"/>, by the <c>DestroyEntity(e, -1)</c> of a container that holds it: (d3b), the real path); returns the ticks of the birth, of the first A and of the first C255, and the words C255 saw.</summary>
    private (int Birth, int FirstA, int FirstC, PickupDropRun.Words FirstIn, int ChildSlot, int CarrierSlot) BornByAnotherEntity(string name, bool childLower, int ticksPerFrame, bool throughDestroyEntity = false)
    {
        using var run = PickupDropRun.OnMap14(name);
        var context = run.Context;
        const int Arrow = 3;
        EntityScriptProxy? fillerA = null;
        if (childLower)
        {
            fillerA = context.SpawnEntityBySpriteIndex(run.Hero, false, Arrow, 80 << 16, 80 << 16, 0, 0x10)!;
            context.SpawnEntityBySpriteIndex(run.Hero, false, Arrow, 90 << 16, 80 << 16, 0, 0x10);
        }

        var carrier = context.SpawnEntityBySpriteIndex(run.Hero, false, Arrow, 100 << 16, 100 << 16, 0, 0x10)!;
        if (childLower)
        {
            run.Arc.OneFrame();
            context.DestroyEntity(fillerA!);
            run.Arc.Proxy.RecycleDestroyedEntities();
        }

        var birth = -1;
        run.Arc.Proxy.EventProgramRunner = new Spy(run.Arc.Proxy.EventProgramRunner, entity =>
        {
            if (ReferenceEquals(entity, carrier) && entity.EventTrigger == ScriptHelper.ProgramCTick && birth < 0)
            {
                birth = run.Tick;
                var container = new EntityScriptProxy { ContentsItemId = Gildas, PosX = 284 << 16, PosY = 96 << 16, PosZ = 0 };
                if (throughDestroyEntity)
                {
                    run.Arc.Proxy.DestroyEntity(container, -1); // E20.e0c phase C: the production path, the first act of the destruction.
                }
                else
                {
                    Assert.True(LootDrop.SpawnContents(context, GameState.Instance, container));
                }

                run.Drop = context.SpawnedEntities.Last(e => e.SpriteType == Gildas + 0x1E);
            }
        });

        run.Run(40, ticksPerFrame);
        Assert.NotNull(run.Drop);
        var firstA = run.Samples.FirstOrDefault(s => s.ARan);
        var firstC = run.Samples.FirstOrDefault(s => s.CRan);
        return (birth, firstA?.Tick ?? -1, firstC?.Tick ?? -1, firstC?.In ?? default, run.Drop!.Slot, carrier.Slot);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W9_TheObjectOfAHigherSlot_RunsItsProgramOneTickAfterTheBirth_ItsFirstC255OneTickLater_OnTheGroundFlag0_TheOneTickGapOfQ_E0C_8(int ticksPerFrame)
    {
        var born = BornByAnotherEntity("w9-higher-" + ticksPerFrame, childLower: false, ticksPerFrame);

        Assert.True(born.ChildSlot > born.CarrierSlot, "the object has a higher slot than the entity that made it");
        Assert.Equal((born.Birth + 1, born.Birth + 2), (born.FirstA, born.FirstC));
        Assert.Equal((0, FirstRise, 0, 600, true), (born.FirstIn.OnGround, born.FirstIn.Bounce, born.FirstIn.State, born.FirstIn.Life, born.FirstIn.ForceZ is > 0 and <= (int)FirstRise));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void W9_TheObjectOfALowerSlot_RunsItsProgramOneTickAfterTheBirth_AsTheBinary_ItsFirstC255OneTickLater_OnTheGroundFlag0(int ticksPerFrame)
    {
        var born = BornByAnotherEntity("w9-lower-" + ticksPerFrame, childLower: true, ticksPerFrame);

        Assert.True(born.ChildSlot < born.CarrierSlot, "the object has a lower slot than the entity that made it");
        Assert.Equal((born.Birth + 1, born.Birth + 2), (born.FirstA, born.FirstC));
        Assert.Equal((0, FirstRise, 0, 600, true), (born.FirstIn.OnGround, born.FirstIn.Bounce, born.FirstIn.State, born.FirstIn.Life, born.FirstIn.ForceZ is > 0 and <= (int)FirstRise));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void D3b_TheObjectDroppedByDestroyEntityOfAHigherSlot_HasTheFirstTicksOfW9_TheOneTickGapOfQ_E0C_8(int ticksPerFrame)
    {
        var born = BornByAnotherEntity("d3b-higher-" + ticksPerFrame, childLower: false, ticksPerFrame, throughDestroyEntity: true);

        Assert.True(born.ChildSlot > born.CarrierSlot, "the object has a higher slot than the entity that made it");
        Assert.Equal((born.Birth + 1, born.Birth + 2), (born.FirstA, born.FirstC));
        Assert.Equal((0, FirstRise, 0, 600, true), (born.FirstIn.OnGround, born.FirstIn.Bounce, born.FirstIn.State, born.FirstIn.Life, born.FirstIn.ForceZ is > 0 and <= (int)FirstRise));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void D3b_TheObjectDroppedByDestroyEntityOfALowerSlot_HasTheFirstTicksOfW9_AsTheBinary(int ticksPerFrame)
    {
        var born = BornByAnotherEntity("d3b-lower-" + ticksPerFrame, childLower: true, ticksPerFrame, throughDestroyEntity: true);

        Assert.True(born.ChildSlot < born.CarrierSlot, "the object has a lower slot than the entity that made it");
        Assert.Equal((born.Birth + 1, born.Birth + 2), (born.FirstA, born.FirstC));
        Assert.Equal((0, FirstRise, 0, 600, true), (born.FirstIn.OnGround, born.FirstIn.Bounce, born.FirstIn.State, born.FirstIn.Life, born.FirstIn.ForceZ is > 0 and <= (int)FirstRise));
    }
}
