#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c T2 (docs/plan-e20-heros.md section 1.26, phase B): the handler C255 (<c>0x8007C174</c>) for a DROPPED object against the vectors that T0 made from the real code of the binary
/// (<c>docs/plan-e20-annexe/e20-e0c/exec/vectors/c255.json</c>, the journal with the fanfare as a stub): 166 vectors with <c>+0x274 == 1</c>, run tick by tick on the words, the contact and the journal (give, flag, sound, unblock, light destruction).
/// Two kinds of vector are left to the phase that owns them or to the data: the vectors that start in the state 2 (the fanfare, phase D: a dropped object never reaches it) and those of the objects <c>0x21</c> and <c>0x3D</c> (the vector forces the answer of
/// <c>GiveItem</c> to 0, the real one answers 1 for them - they carry no life rule - and a dropped object that answers 1 has a message, which no drop produces). The chain of the bounces is read here too, at the gravity 128 and 3.
/// </summary>
public sealed class PickupAiDropVectorTests
{
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());

    private static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    private static int I(JsonElement e) => unchecked((int)e.GetInt64());

    private static IEnumerable<JsonElement> DroppedVectors(JsonDocument document)
        => document.RootElement.GetProperty("vectors").EnumerateArray().Where(v => !v.GetProperty("placed").GetBoolean());

    private static bool IsOurs(JsonElement vector)
        => vector.GetProperty("entity").GetProperty("state27C").GetInt32() != 2 && vector.GetProperty("item").GetInt32() is not (0x21 or 0x3D);

    private static EntityScriptProxy Box(JsonElement position, JsonElement size)
    {
        var p = position.EnumerateArray().Select(I).ToArray();
        var s = size.EnumerateArray().Select(I).ToArray();
        return new EntityScriptProxy { ModdedPosX = p[0], ModdedPosY = p[1], ModdedPosZ = p[2], Width = s[0], Height = s[1], Depth = s[2] };
    }

    [Fact]
    public void TheVectorsOfTheDroppedObjects_AreFollowedWordForWordAndEventForEvent_AtEveryTick()
    {
        using var document = AnnexFiles.ReadVector("c255.json");
        var all = DroppedVectors(document).ToList();
        var vectors = all.Where(IsOurs).ToList();
        Assert.Equal((166, 140), (all.Count, vectors.Count)); // 166 dropped vectors; 16 start in the state 2 and 10 more are of the objects 0x21 and 0x3D.
        var ticks = 0;
        var pickups = 0;

        foreach (var vector in vectors)
        {
            var id = vector.GetProperty("id").GetString();
            var e = vector.GetProperty("entity");
            var item = vector.GetProperty("item").GetInt32();
            var dialogue = new FakeDialogueDirector();
            var sounds = new RecordingSoundPlayer();
            var host = new PickupHost { Tables = Tables, Sound = sounds, Dialogue = dialogue };
            var state = new GameState();
            var hero = Box(vector.GetProperty("hero").GetProperty("pos"), vector.GetProperty("hero").GetProperty("size"));
            hero.Hp = 5;
            hero.HpMax = 10;
            hero.LifePulledFromStats = true;
            host.Hero = hero;
            var entity = Box(e.GetProperty("pos"), e.GetProperty("size"));
            entity.SpriteType = item + 0x1E;
            entity.MapGravityRaw = vector.GetProperty("gravity").GetInt32();
            PickupAi.WriteWord274(entity, U(e.GetProperty("word274")));
            entity.DelayOrAngleOrEntityId = I(e.GetProperty("word278"));
            entity.ItemState = e.GetProperty("state27C").GetInt32();
            PickupAi.WriteWord280(entity, U(e.GetProperty("word280")));
            PickupAi.WriteWord284(entity, U(e.GetProperty("word284")));
            entity.ForceZ = I(e.GetProperty("forceZ"));
            entity.IsOnGround = e.GetProperty("onGround").GetInt32();
            entity.DamagedTickCounter = I(e.GetProperty("f1CC"));
            var flag = U(e.GetProperty("word280"));

            var ground = vector.GetProperty("groundAfterTick").EnumerateArray().ToArray();
            var steps = vector.GetProperty("projected").EnumerateArray().ToArray();
            for (var t = 0; t < steps.Length; t++)
            {
                dialogue.IsOpen = vector.GetProperty("dialogOpen")[t].GetBoolean();
                var soundsBefore = sounds.Requests.Count;
                var destroyedBefore = host.Destroyed.Count;
                var unblockedBefore = host.Unblocked.Count;
                var stateBefore = entity.ItemState;

                PickupAi.Tick(entity, host, state);
                ticks++;

                var after = steps[t].GetProperty("after");
                var journal = steps[t].GetProperty("journal").EnumerateArray().Select(j => j.EnumerateArray().ToArray()).Where(j => j[0].GetString() != "dialogOpen?").ToArray();
                var where = $"{id} (item {item:X2}, gravity {entity.MapGravityRaw}) tick {t}";
                Assert.True(
                    (I(after.GetProperty("w278")), I(after.GetProperty("s27C")), U(after.GetProperty("w284")), I(after.GetProperty("forceZ")), I(after.GetProperty("f1CC")))
                    == (entity.DelayOrAngleOrEntityId, entity.ItemState, PickupAi.ReadWord284(entity), entity.ForceZ, entity.DamagedTickCounter),
                    $"{where}: the words");

                var expectedSounds = journal.Where(j => j[0].GetString() == "sound").Select(j => j[1].GetInt32()).ToArray();
                Assert.True(expectedSounds.SequenceEqual(sounds.Requests.Skip(soundsBefore)), $"{where}: the sound");
                Assert.Equal(journal.Count(j => j[0].GetString() == "destroyLight"), host.Destroyed.Count - destroyedBefore);
                Assert.Equal(journal.Count(j => j[0].GetString() == "unblock"), host.Unblocked.Count - unblockedBefore);
                var gave = journal.Any(j => j[0].GetString() == "give");
                Assert.True(gave == (stateBefore == 1 && entity.ItemState == 3), $"{where}: the pickup");
                if (gave)
                {
                    pickups++;
                    Assert.True(host.Hero == hero);
                    var raised = journal.Single(j => j[0].GetString() == "setGameFlag")[1].GetInt64();
                    Assert.Equal(flag, unchecked((uint)raised));
                    if (flag != 0)
                    {
                        Assert.True((state.GetFlag(flag) & (1u << (int)(flag & 31))) != 0, $"{where}: the flag {flag}");
                    }
                }

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

        Assert.Equal((568, 26), (ticks, pickups)); // the ticks and the pickups of the 140 vectors (the count of the oracle).
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The chain of the bounces (report 1.6, replayed on the real code in T0): from 0xA0000, the speed times 12 / 16 until it does not exceed the gravity << 8.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static List<uint> ChainOf(int gravity)
    {
        var entity = new EntityScriptProxy { MapGravityRaw = gravity, ItemState = 1, DelayOrAngleOrEntityId = -1, ModdedPosX = int.MaxValue / 2, Width = 1, Height = 1, Depth = 1 };
        PickupAi.WriteWord274(entity, 1);
        PickupAi.WriteWord284(entity, 0xA0000);
        var host = new PickupHost { Tables = Tables, Hero = new EntityScriptProxy() };
        var chain = new List<uint>();
        for (var landing = 0; landing < 100; landing++)
        {
            entity.IsOnGround = 1;
            PickupAi.Tick(entity, host, new GameState());
            chain.Add(PickupAi.ReadWord284(entity));
            Assert.Equal(unchecked((int)PickupAi.ReadWord284(entity)), entity.ForceZ);
            if (PickupAi.ReadWord284(entity) == 0)
            {
                break;
            }
        }

        return chain;
    }

    [Fact]
    public void TheChainOfTheBounces_AtGravity128_IsTheOneOfTheBinary_ElevenValuesTheLastZero()
        => Assert.Equal(new uint[] { 0x78000, 0x5A000, 0x43800, 0x32A00, 0x25F80, 0x1C7A0, 0x155B8, 0x1004A, 0xC037, 0x9029, 0 }, ChainOf(128));

    [Fact]
    public void TheChainOfTheBounces_AtGravity3_HasTheTwentyFourBouncesOfTheBinary_AndTheirFirstAndLastValues()
    {
        var chain = ChainOf(3);

        Assert.Equal(24, chain.Count); // "24 rebonds" of the map 159 and 160 (report 1.6).
        Assert.Equal((0x78000u, 0x5A000u, 0x43800u, 0u), (chain[0], chain[1], chain[2], chain[^1]));
        Assert.Equal(0x36Bu, chain[^2]); // the last bounce, above 3 << 8 = 0x300; the next one (0x28B) is not.
    }

    /// <summary>The speeds around the threshold <c>gravity &lt;&lt; 8</c>, run on the REAL code of the binary (<c>exec/scripts/boundary_c255.py</c>, <c>exec/replays/boundary_c255_out.txt</c>): a speed whose product <c>(speed * 12) &gt;&gt; 4</c> EQUALS the threshold is lost (0), one above it is kept.</summary>
    [Theory]
    [InlineData(128, 0xAAAAu, 0u)]
    [InlineData(128, 0xAAABu, 0u)]
    [InlineData(128, 0xAAACu, 0x8001u)]
    [InlineData(128, 0xAAAEu, 0x8002u)]
    [InlineData(3, 0x3FFu, 0u)]
    [InlineData(3, 0x400u, 0u)]
    [InlineData(3, 0x401u, 0u)]
    [InlineData(3, 0x402u, 0x301u)]
    [InlineData(3, 0x403u, 0x302u)]
    public void ABounceThatDoesNotExceedTheThreshold_IsLost_OneThatExceedsItIsKept_AsTheRealCode(int gravity, uint speed, uint expected)
    {
        var entity = new EntityScriptProxy { MapGravityRaw = gravity, ItemState = 1, DelayOrAngleOrEntityId = -1, ModdedPosX = int.MaxValue / 2, Width = 1, Height = 1, Depth = 1, IsOnGround = 1 };
        PickupAi.WriteWord274(entity, 1);
        PickupAi.WriteWord284(entity, speed);
        var host = new PickupHost { Tables = Tables, Hero = new EntityScriptProxy() };

        PickupAi.Tick(entity, host, new GameState());

        Assert.Equal((expected, unchecked((int)expected)), (PickupAi.ReadWord284(entity), entity.ForceZ));
    }

    /// <summary>A key object (byte 5 of the table zero: the block of the world, the fanfare, the mask 4) that is dropped - nothing in the loot does it - takes the key branch of C255 all the same, as in the binary (phase D): the state 2, no sound, the world blocked,
    /// and no box (a dropped object has no message) - while an ordinary object on the same spot is taken to the state 3.</summary>
    [Theory]
    [InlineData(0x04, true)]
    [InlineData(0x45, false)]
    public void ADroppedKeyObject_TakesTheKeyBranchOfThePickup_WithNoMessage_AnOrdinaryObjectOnTheSameSpotIsTakenToTheState3(int item, bool key)
    {
        var sounds = new RecordingSoundPlayer();
        var dialogue = new FakeDialogueDirector();
        var host = new PickupHost { Tables = Tables, Sound = sounds, Dialogue = dialogue, Hero = new EntityScriptProxy { Width = 1, Height = 1, Depth = 1 } };
        var entity = new EntityScriptProxy { SpriteType = item + 0x1E, ItemState = 1, DelayOrAngleOrEntityId = -1, Width = 1, Height = 1, Depth = 1 };
        PickupAi.WriteWord274(entity, 1);
        var state = new GameState();
        Assert.Equal(key, Tables.DropSoundSfxIndex[item] == 0);

        PickupAi.Tick(entity, host, state);

        Assert.Equal((key ? 2 : 3, key ? 0 : 1), (entity.ItemState, sounds.Requests.Count));
        Assert.Equal(key ? 1 : 0, host.Blocked.Count);
        Assert.Empty(dialogue.Opens); // a dropped object has no message.
        Assert.Equal(key ? new[] { 4 } : Array.Empty<int>(), dialogue.CloseMasks); // the mask 4 is written all the same by a key object.
        Assert.Equal(1, key ? Alundra.Hero.PlayerManager.GetNumberOfItem(state, item) : state.PlayerStats.Money);
    }

    [Fact]
    public void ABounceNeedsTheGround_AndASpeedLeft()
    {
        var entity = new EntityScriptProxy { MapGravityRaw = 128, ItemState = 1, DelayOrAngleOrEntityId = -1, ModdedPosX = int.MaxValue / 2, Width = 1, Height = 1, Depth = 1, ForceZ = 7 };
        PickupAi.WriteWord274(entity, 1);
        var host = new PickupHost { Tables = Tables, Hero = new EntityScriptProxy() };
        PickupAi.WriteWord284(entity, 0xA0000);
        entity.IsOnGround = 0;
        PickupAi.Tick(entity, host, new GameState());
        Assert.Equal((0xA0000u, 7), (PickupAi.ReadWord284(entity), entity.ForceZ)); // in the air: nothing.

        PickupAi.WriteWord284(entity, 0);
        entity.IsOnGround = 1;
        PickupAi.Tick(entity, host, new GameState());
        Assert.Equal((0u, 7), (PickupAi.ReadWord284(entity), entity.ForceZ)); // on the ground with no speed: nothing.
    }
}
