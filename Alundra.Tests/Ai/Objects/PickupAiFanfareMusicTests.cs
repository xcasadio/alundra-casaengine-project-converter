#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Streaming;
using Alundra.Ai.Objects;
using Alundra.Audio;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0c2 T1 (docs/plan-e20-heros.md section 1.26.1, D-E0C2-1 to -3, D-E20-65): the music of the fanfare of a key object. At the pickup the binary calls <c>LoadBgm(0)</c> (<c>0x80049B7C</c>, the music stops) after <c>Block(self)</c> and before the message;
/// at the state 2 it calls <c>StopAllSound</c> (<c>0x80049AF4</c>, the music restarts from the top). The track 11 of the CD stays a journaled stub. (m1) the calls and their order on a recording director, (m2) the live voice of the real
/// <see cref="BgmPlayer"/> on a fake audio backend, (m3) a host without a director.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class PickupAiFanfareMusicTests : IDisposable
{
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());
    private readonly List<string> _tempDirs = new();
    private FakeAudioClipProvider? _provider;

    private const int KeyItem = 0x04;
    private const int OrdinaryItem = 0x45;

    public PickupAiFanfareMusicTests()
    {
        BgmPlayer.Instance.ResetForTests();
        BgmFadeDirector.Instance.ResetForTests();
        EtcStringTable.ResetForTests();
        var entries = Enumerable.Range(0, 0x58).Select(item => (0x200 + item, "Obj" + item.ToString("X2"))).Append((0x45, "gagné!")).ToArray();
        EtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc(entries));
        PickupAi.FanfareStubCallsForTests = new List<string>();
    }

    public void Dispose()
    {
        PickupAi.FanfareStubCallsForTests = null;
        EtcStringTable.ResetForTests();
        BgmPlayer.Instance.ResetForTests();
        BgmFadeDirector.Instance.ResetForTests();
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed record Mount(EntityScriptProxy Entity, PickupHost Host, FakeDialogueDirector Dialogue, GameState State);

    /// <summary>An object on the hero, ready to be taken at the next tick (the state 1), placed or dropped.</summary>
    private static Mount Standing(int item, IBgmFadeDirector? director, uint kind = 0)
    {
        var dialogue = new FakeDialogueDirector();
        var hero = new EntityScriptProxy { Width = 10, Height = 10, Depth = 10, Hp = 5, HpMax = 10, LifePulledFromStats = true };
        var host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Dialogue = dialogue, Hero = hero, BgmFadeDirector = director };
        var entity = new EntityScriptProxy { SpriteType = item + 0x1E, Width = 10, Height = 10, Depth = 10 };
        if (kind == PickupAi.KindDropped)
        {
            PickupAi.WriteWord274(entity, kind);
        }
        else
        {
            PickupAi.Load(entity);
        }

        entity.ItemState = 1;
        return new Mount(entity, host, dialogue, new GameState());
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m1) the calls of the director, and their order.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void M1_AKeyObject_CallsLoadBgm0_OnceAtThePickupAfterBlockAndBeforeTheMessage_ThenStopAllSoundOnceAtTheState2_NothingBetween_AndTheJournalIsUnchanged()
    {
        var dialogue = new FakeDialogueDirector();
        var hero = new EntityScriptProxy { Width = 10, Height = 10, Depth = 10, Hp = 5, HpMax = 10, LifePulledFromStats = true };
        var host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Dialogue = dialogue, Hero = hero };
        var director = new RecordingBgmFadeDirector(host, dialogue);
        host.BgmFadeDirector = director;
        var entity = new EntityScriptProxy { SpriteType = KeyItem + 0x1E, Width = 10, Height = 10, Depth = 10 };
        PickupAi.Load(entity);
        entity.ItemState = 1;
        var state = new GameState();

        PickupAi.Tick(entity, host, state);

        Assert.Equal(2, entity.ItemState);
        Assert.Equal(new[] { "LoadBgm(0)" }, director.Calls);
        Assert.Equal((1, 0), director.HostAtCall[0]); // after Block(self), before the message is opened.
        Assert.Single(dialogue.Opens);
        Assert.Equal(new[] { "start" }, PickupAi.FanfareStubCallsForTests);

        PickupAi.Tick(entity, host, state); // the state 2

        Assert.Equal(3, entity.ItemState);
        Assert.Equal(new[] { "LoadBgm(0)", "StopAllSound" }, director.Calls);
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests);

        dialogue.IsOpen = true;
        PickupAi.Tick(entity, host, state);
        dialogue.IsOpen = false;
        PickupAi.Tick(entity, host, state); // the state 3, the destruction.

        Assert.Equal(new[] { entity }, host.Destroyed);
        Assert.Equal(new[] { "LoadBgm(0)", "StopAllSound" }, director.Calls);
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void M1_AnOrdinaryObject_AndADroppedObject_CallNeitherLoadBgmNorStopAllSound(bool dropped)
    {
        var dialogue = new FakeDialogueDirector();
        var hero = new EntityScriptProxy { Width = 10, Height = 10, Depth = 10, Hp = 5, HpMax = 10, LifePulledFromStats = true };
        var host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Dialogue = dialogue, Hero = hero };
        var director = new RecordingBgmFadeDirector(host, dialogue);
        host.BgmFadeDirector = director;
        var entity = new EntityScriptProxy { SpriteType = OrdinaryItem + 0x1E, Width = 10, Height = 10, Depth = 10 };
        if (dropped)
        {
            PickupAi.WriteWord274(entity, PickupAi.KindDropped);
        }
        else
        {
            PickupAi.Load(entity);
        }

        entity.ItemState = 1;
        var state = new GameState();

        for (var i = 0; i < 4; i++)
        {
            dialogue.IsOpen = false;
            PickupAi.Tick(entity, host, state);
        }

        Assert.NotEmpty(host.Destroyed); // the object was taken and went.
        Assert.Empty(director.Calls);
        Assert.Empty(PickupAi.FanfareStubCallsForTests!);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m2) the real director and the real player: the voice stops at the pickup, a fresh one starts from the top at the next tick.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Guid AssetId(int soundIndex) => Guid.Parse($"00000000-0000-0000-0000-{soundIndex:D12}");

    /// <summary>The montage of <c>MusicPlayerTests.T1bis_...</c>: a fixture project (the map 389 plays the track 25), a fake backend, the real singletons attached; the voice of the track 25 is playing when it returns.</summary>
    private (FakeAudioBackend Backend, MusicTrackHandle FirstVoice) PlayingTrack25()
    {
        var root = Path.Combine(Path.GetTempPath(), "PickupAiFanfareMusicTests_" + Guid.NewGuid());
        _tempDirs.Add(root);
        Directory.CreateDirectory(Path.Combine(root, "Maps"));
        File.WriteAllText(Path.Combine(root, "Maps", "music-index.json"), JsonSerializer.Serialize(new Dictionary<string, int> { ["389"] = 25 }));
        Directory.CreateDirectory(Path.Combine(root, "Musics"));
        File.WriteAllText(Path.Combine(root, "Musics", "bgm-manifest.json"), JsonSerializer.Serialize(new[] { new { sound_index = 25, asset_id = AssetId(25).ToString() } }));

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        provider.RegisterStream(AssetId(25), "bgm_025_fixture");
        _provider = provider;
        var service = new AudioService(backend) { ClipProvider = provider };
        BgmPlayer.Instance.AttachToWorld(service, root);
        BgmFadeDirector.Instance.AttachToWorld(service, null);
        BgmPlayer.Instance.PlayMapMusic(389);
        WorldProxy.ConsumeMusicResetSoundFlagOnFrameClose(BgmPlayer.Instance, BgmFadeDirector.Instance);

        Assert.Single(backend.StreamVoices);
        Assert.True(BgmPlayer.Instance.IsCurrentVoiceAlive);
        return (backend, BgmPlayer.Instance.CurrentVoiceForTests);
    }

    [Fact]
    public void M2_AKeyObject_StopsTheLiveVoiceAtThePickup_AndAFreshVoiceStartsFromTheTopAtTheNextTick()
    {
        var (backend, first) = PlayingTrack25();
        var m = Standing(KeyItem, BgmFadeDirector.Instance);

        PickupAi.Tick(m.Entity, m.Host, m.State); // the pickup

        Assert.Equal(2, m.Entity.ItemState);
        Assert.False(BgmPlayer.Instance.IsCurrentVoiceAlive); // the music is cut ...
        Assert.Single(backend.StreamVoices);                  // ... and nothing restarted yet.

        PickupAi.Tick(m.Entity, m.Host, m.State); // the state 2

        Assert.True(BgmPlayer.Instance.IsCurrentVoiceAlive);
        Assert.Equal(2, backend.StreamVoices.Count);           // a fresh stream from the top ...
        Assert.NotEqual(first, BgmPlayer.Instance.CurrentVoiceForTests); // ... not the old voice.
        Assert.Equal(new[] { "bgm_025_fixture", "bgm_025_fixture" }, _provider!.OpenedStreams); // the same track, opened again: from the top.

        PickupAi.Tick(m.Entity, m.Host, m.State); // the state 3: the music is left alone.
        Assert.Equal(2, backend.StreamVoices.Count);
    }

    [Fact]
    public void M2_AnOrdinaryObject_AndADroppedObject_LeaveTheVoicePlayingWithoutARestart()
    {
        foreach (var dropped in new[] { false, true })
        {
            BgmPlayer.Instance.ResetForTests();
            BgmFadeDirector.Instance.ResetForTests();
            var (backend, first) = PlayingTrack25();
            var m = Standing(OrdinaryItem, BgmFadeDirector.Instance, dropped ? PickupAi.KindDropped : 0);

            for (var i = 0; i < 4; i++)
            {
                m.Dialogue.IsOpen = false;
                PickupAi.Tick(m.Entity, m.Host, m.State);
            }

            Assert.NotEmpty(m.Host.Destroyed);
            Assert.Single(backend.StreamVoices);
            Assert.True(BgmPlayer.Instance.IsCurrentVoiceAlive);
            Assert.Equal(first, BgmPlayer.Instance.CurrentVoiceForTests);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (m3) a host without a director.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void M3_AHostWithoutADirector_TakesAKeyObjectAsBefore_WithoutException()
    {
        var m = Standing(KeyItem, null);
        Assert.Null(m.Host.BgmFadeDirector);

        PickupAi.Tick(m.Entity, m.Host, m.State);
        Assert.Equal(2, m.Entity.ItemState);
        Assert.Equal(new[] { m.Entity }, m.Host.Blocked);

        PickupAi.Tick(m.Entity, m.Host, m.State);
        Assert.Equal(3, m.Entity.ItemState);
        Assert.Equal(new[] { "start", "end" }, PickupAi.FanfareStubCallsForTests);

        m.Dialogue.IsOpen = false;
        PickupAi.Tick(m.Entity, m.Host, m.State);
        Assert.Equal(new[] { m.Entity }, m.Host.Destroyed);
    }
}
