#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.t (docs/plan-e19-opcodes.md section 1.2r, D-E19-57, D-E19-61, ADR-0028), tests T-1: every change of animation of an active entity asks its
/// sound of the world's sound player, at the tick of the change, appearances included. The identifier is <c>Sfx + (Acceleration &amp; 0x20 ? 0x100 : 0)</c>
/// (the export's <c>Acceleration</c> is the byte 0xD of the animation set; the decompilation reads the byte 0xB, wrong on 91 of the 2405 sets of the
/// export), nothing when it is not positive. A loop turn, a hold, the same animation asked again and the animation of an appearance (R2: the first switch of
/// <c>InitializeEntity</c>) give no second request. Every expected value was written by hand before the code; a value the measure contradicts is a stop.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraAnimationSoundTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    /// <summary>A recorder of the sounds asked, stamped with the frame of the arc (or of the rig) at which they were asked.</summary>
    private sealed class StampedSoundPlayer : IAlundraSoundPlayer
    {
        private readonly Func<int> _frame;

        public StampedSoundPlayer(Func<int>? frame = null) => _frame = frame ?? (() => 0);

        public List<(int Frame, int Id)> Requests { get; } = new();

        public int[] Ids => Requests.Select(r => r.Id).ToArray();

        public void PlaySfx(int sfxId) => Requests.Add((_frame(), sfxId));

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

    private static StampedSoundPlayer Inject(ArcRun arc)
    {
        var player = new StampedSoundPlayer(() => arc.Frame);
        typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, player);
        return player;
    }

    private static SpriteRecordHeader HeaderOf(Func<JToken, bool> pick)
    {
        var root = ProjectRootFinder.Find();
        var infos = JObject.Parse(File.ReadAllText(Path.Combine(root, "AssetInfos.json")));
        var node = ((JArray)infos["asset_infos"]!).First(pick);
        var id = Guid.Parse((string)node["id"]!);
        Assert.True(new SpriteRecordCatalog(root).TryGet(id, out var header), $"no sprite header for asset {id}");
        return header;
    }

    private static SpriteRecordHeader HeroHeader()
        => HeaderOf(n => (string?)n["name"] == "Alundra" && (string?)n["asset_type"] == "entity");

    private static SpriteRecordHeader PrefabHeader(string idPrefix)
        => HeaderOf(n => ((string?)n["id"])?.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase) == true);

    private static void SetSound(JumpNpcRig rig, int anim, int sfx, int acceleration = 0)
        => rig.SetAnimSet(new AnimSetEntry { Anim = anim, Speed = 0, Acceleration = acceleration, IsZForceApplied = 0, Sfx = sfx });

    // ------------------------------------------------------------------------------------------------------------------------------------
    // The identifier: byte 0xC, plus 0x100 with the bank bit of byte 0xD
    // ------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T1_TheHero_Animation93To480_TheBankBitOfTheByte0xDAdds256()
    {
        var header = HeroHeader();
        Assert.Equal(224, header.AnimSets![93].Sfx);
        Assert.Equal(0x60, header.AnimSets[93].Acceleration & 0xF0); // bit 0x20 set: the byte 0xD, not the byte 0xB (Flags), carries the bank.

        var sounds = new StampedSoundPlayer();
        var rig = JumpHeroRig.Build();
        rig.Hero.AnimSetsByAnim = header.AnimSets;
        rig.Host.SoundPlayer = sounds;
        rig.Hero.TargetAnimationId = 93;
        rig.Update();

        Assert.Equal(new[] { 480 }, sounds.Ids);
    }

    [Fact]
    public void T1_ThePrefab0edffd14_Animation5To365_NotTheDecompilationsByte()
    {
        var header = PrefabHeader("0edffd14");
        Assert.Equal(109, header.AnimSets![5].Sfx); // the decompilation (byte 0xB) would ask 109.
        Assert.NotEqual(0, header.AnimSets[5].Acceleration & 0x20);

        var sounds = new StampedSoundPlayer();
        var rig = JumpNpcRig.Build();
        rig.Npc.AnimSetsByAnim = header.AnimSets;
        rig.Host.SoundPlayer = sounds;
        rig.Npc.TargetAnimationId = 5;
        rig.Update();

        Assert.Equal(new[] { 365 }, sounds.Ids);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------
    // What gives nothing
    // ------------------------------------------------------------------------------------------------------------------------------------

    private static JumpNpcRig SpriteRig(StampedSoundPlayer sounds, params JumpNpcRig.SpriteAnim[] anims)
    {
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f) }.Concat(anims).ToArray());
        rig.Host.SoundPlayer = sounds;
        return rig;
    }

    [Fact]
    public void T1_AnAnimationOfSfxZero_AsksNothing()
    {
        var sounds = new StampedSoundPlayer();
        var rig = SpriteRig(sounds, JumpNpcRig.Loop(1, 1.6f));
        SetSound(rig, 1, sfx: 0, acceleration: 0x04);
        rig.Npc.TargetAnimationId = 1;
        for (var update = 1; update <= 5; update++)
        {
            rig.Update();
        }

        Assert.Equal((uint)1, rig.Npc.CurrentAnimationId);
        Assert.Empty(sounds.Ids);
    }

    [Fact]
    public void T1_ALoopTurnAsksNoSound_OnlyTheSwitchDid()
    {
        var sounds = new StampedSoundPlayer();
        var rig = SpriteRig(sounds, JumpNpcRig.Loop(2, 0.2f)); // 10 ticks per turn.
        SetSound(rig, 2, sfx: 9);
        rig.Npc.TargetAnimationId = 2;
        for (var update = 1; update <= 60; update++)
        {
            rig.Update(); // six turns.
        }

        Assert.Equal(new[] { 9 }, sounds.Ids);
    }

    [Fact]
    public void T1_AHoldAsksNoSound_OnlyTheSwitchDid()
    {
        var sounds = new StampedSoundPlayer();
        var rig = SpriteRig(sounds, JumpNpcRig.Hold(3, 0.1f));
        SetSound(rig, 3, sfx: 8);
        rig.Npc.TargetAnimationId = 3;
        for (var update = 1; update <= 40; update++)
        {
            rig.Update(); // the animation ended after 5 ticks and holds.
        }

        Assert.Equal(new[] { 8 }, sounds.Ids);
    }

    [Fact]
    public void T1_TheSameAnimationAskedAgain_AsksNoSecondSound()
    {
        var sounds = new StampedSoundPlayer();
        var rig = SpriteRig(sounds, JumpNpcRig.Loop(2, 1.6f));
        SetSound(rig, 2, sfx: 9);
        rig.Npc.TargetAnimationId = 2;
        rig.Update();
        Assert.Equal(new[] { 9 }, sounds.Ids);

        for (var update = 1; update <= 10; update++)
        {
            rig.Npc.TargetAnimationId = 2; // a script writes the animation it already plays, every tick.
            rig.Update();
        }

        Assert.Equal(new[] { 9 }, sounds.Ids);
    }

    [Fact]
    public void T1_ASelfChainingAnimation_AsksItsSoundOncePerCycle_TheHeroAnimation15To22()
    {
        var entry = HeroHeader().AnimSets![15];
        Assert.Equal(22, entry.Sfx);
        Assert.Equal(0, entry.Acceleration & 0x20);

        var sounds = new StampedSoundPlayer();
        var rig = SpriteRig(sounds, JumpNpcRig.Chain(15, 0.1f, to: 15));
        rig.SetAnimSet(entry);
        rig.Npc.TargetAnimationId = 15;
        var before = 0;
        var askedAt = new List<int>();
        for (var update = 1; update <= 60; update++)
        {
            rig.Update();
            Assert.True(sounds.Requests.Count - before <= 1, $"update {update}: more than one request in one tick");
            if (sounds.Requests.Count > before)
            {
                askedAt.Add(update);
            }

            before = sounds.Requests.Count;
        }

        Assert.All(sounds.Ids, id => Assert.Equal(22, id));
        Assert.Equal(1, askedAt[0]); // the switch.
        Assert.True(askedAt.Count >= 8, $"{askedAt.Count} requests in 60 updates, one per cycle expected");
        var gaps = askedAt.Zip(askedAt.Skip(1), (a, b) => b - a).Distinct().ToList();
        Assert.Single(gaps); // a constant cycle.
        Assert.InRange(gaps[0], 4, 8);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------
    // The scenes of the chain
    // ------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T1_Map476_0x8A_At553_AsksMelzasSound219AtTheTickOfTheOpcode_TheBankResolves864InTheGroup62()
    {
        var bank = new AlundraSoundBank(ProjectRootFinder.Find());
        Assert.True(bank.TryResolve(219, soundGroup: 62, out var resolution));
        Assert.Equal(864, resolution.ResolvedId);

        using var arc = new ArcRun(AlundraVisionArcTests.A4pSpec);
        var sounds = Inject(arc);
        var asked219Before = -1;
        var asked219At = -1;
        var countedAtTheInstruction = -1;
        arc.OnInstruction = t =>
        {
            if (t.Slot == B && t.Pc == 553 && t.Opcode == 0x8A)
            {
                asked219At = sounds.Ids.Count(id => id == 219);
                asked219Before = countedAtTheInstruction;
            }
            else
            {
                countedAtTheInstruction = sounds.Ids.Count(id => id == 219);
            }
        };

        arc.RunUntil(() => arc.Has(B, 553, 0x8A), "B5 executes 0x8A @553 (Melzas)");

        // The request is made inside the opcode: one more 219 than at the instruction before it.
        Assert.Equal(0, asked219Before);
        Assert.Equal(1, asked219At);
        arc.OneFrame();
        Assert.Equal(1, sounds.Ids.Count(id => id == 219)); // the first switch of the appearance (R2) asks no second one.
    }

    [Fact]
    public void T1_Map392_0x5B_At64_AsksTheHeroSprintSound13_Once()
    {
        var spec = new ArcSpec(
            "E19t-392", "The Klark", "Ship Klark (night, inner, break)-392", new[] { 1641 }, 29, 13, 4, 400, RealController: true, Prefabs: true);
        using var arc = new ArcRun(spec);
        var sounds = Inject(arc);
        arc.RunUntil(() => arc.Has(B, 104, 0x11), "B1 executes 0x11 @104 (the hero takes the hand)");

        // 0x5B @64 (frame 121) sets the hero's animation 3; the hero's tick of the frame after it (the DLL's frame order: the engine updates the entities before the world proxy runs the map events; D-E19-64 covers the text box only) asks
        // its sound 13, once: the animations 13 (@58) and 0 (@72, @78, @84) have none. (The scene's own sound opcodes ask other ids.)
        Assert.Equal(1, sounds.Ids.Count(id => id == 13));
        Assert.InRange(sounds.Requests.Single(r => r.Id == 13).Frame, 121, 123);
    }

    [Fact]
    public void T1_Map178_TheBookOfTheTickProgram728_AsksThePageSound204Once()
    {
        var spec = new ArcSpec(
            "A18", "Inoa", "Inoa (inner)-178", new[] { 203, 1654, 1655 }, 0, 0, 0, 4000,
            RealController: true, Prefabs: true,
            Arrival: new ArcArrival(60555264, 26738688, 0, AlundraGameState.ResetAnimationId, 16));
        using var arc = new ArcRun(spec);
        var sounds = Inject(arc);
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 543, 0x11), "C[6] executes 0x11 @543");
        arc.PlaceHero(948, 232, 16);
        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(C, 735, 0xFF), "the book's program C[7] ends (0xFF @735)");
        for (var frame = 0; frame < 10; frame++)
        {
            arc.OneFrame();
        }

        // The book (record 2) is born of 0x2D [2] @537 and its animation 0 has no sound; 0x1A [1] @728 asks 204, 0x1C [1] @730 counts the chain without
        // restarting it, 0x1A [2] @732 has no sound.
        Assert.Equal(1, sounds.Ids.Count(id => id == 204));
        var frameOf728 = arc.Trace.First(t => t.Slot == C && t.Pc == 728 && t.Opcode == 0x1A).Frame;
        Assert.InRange(sounds.Requests.Single(r => r.Id == 204).Frame, frameOf728, frameOf728 + 1);
    }

    [Fact]
    public void T1_Map179_TheBookSpawnedLikeBy0x8A_At206_AsksThePageSound204Once_When1A01Switches()
    {
        // B[1] of the map 179 (0x8A @206, then 0x1A [1] @224, 0x1C [1] @226, 0x1A [2] @228) cannot be run end to end by an arc: its 0x0B @114 waits for a walk of
        // Septimus that the headless montage never produces (it stays at @114 for 3000 frames). The book is therefore spawned by the production call of the opcode
        // (SpawnEntityByRecordId, record 2) and given the animation 1 as 0x1A does, then left to its chain.
        var spec = new ArcSpec(
            "E19t-179", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        var sounds = Inject(arc);
        arc.OneFrame();
        arc.OneFrame();
        Assert.Empty(sounds.Ids);

        var book = ((IEntityWorldContext)arc.Proxy).SpawnEntityByRecordId(arc.Hero, 2);
        Assert.NotNull(book);
        arc.OneFrame();
        Assert.Empty(sounds.Ids); // the animation 0 of the book has no sound.

        book!.TargetAnimationId = 1; // 0x1A [1] @224.
        for (var frame = 0; frame < 120; frame++)
        {
            arc.OneFrame(); // the chain 1 -> 2 ends inside this.
        }

        Assert.Equal((uint)2, book.CurrentAnimationId); // the chain went on to the animation 2 (no sound).
        Assert.Equal(new[] { 204 }, sounds.Ids);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------
    // The appearances
    // ------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void T1_TheHeroArrivingByAdoptPlayerPawnInTheAnimation43_AsksSound10_AndTheFirstSwitchAsksNoSecond()
    {
        // Seam: the sound player of the world proxy (a recorder written over its backing field after the montage, the way
        // AlundraArcGuardAndSoundHostTests does) and the arrival adoption invoked again, as UJ-7 does. The animation 43 carries an impulse and the sound 10
        // (system bank); the default arrival animation, 0x36, has no sound and would prove nothing.
        var spec = new ArcSpec("E19t-arrival", "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));
        using var arc = new ArcRun(spec);
        var hero = arc.Hero;
        Assert.Equal(10, hero.AnimSetsByAnim![43].Sfx);
        Assert.Equal(1280, hero.AnimSetsByAnim[43].IsZForceApplied);
        var sounds = Inject(arc);
        Assert.Empty(sounds.Ids);

        AlundraWarpDirector.Instance.ResetForTests();
        AlundraWarpDirector.Instance.SetPendingArrivalForTests(179, (17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, 43u, 0u);
        var tileMapData = (TileMapData)typeof(AlundraWorldProxy).GetField("_tileMapData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!;
        typeof(AlundraWorldProxy).GetMethod("AdoptPlayerPawn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(arc.Proxy, new object[] { arc.RealWorld!, tileMapData });
        AlundraWarpDirector.Instance.ResetForTests();

        Assert.Equal(43u, hero.TargetAnimationId);
        Assert.Equal(new[] { 10 }, sounds.Ids); // asked by the adoption itself.

        ArcRun.State.PlayerControlFlags |= AlundraGameState.PlayerControlBits.ControlLocked;
        arc.OneFrame();
        arc.OneFrame();
        Assert.NotEqual(~43u, hero.CurrentAnimationId); // the first switch happened (whatever animation the hero plays by now) ...
        Assert.Equal(new[] { 10 }, sounds.Ids); // ... and asked nothing more (R2).
    }

    [Fact]
    public void T1_AnAppearanceAtTheMapLoad_AsksItsSound_ResolvedInTheSoundGroupOfTheNewMap()
    {
        // Synthetic montage (the audit of the values found no real record that is both loaded at the map load and sounds on its animation 0): the map 476
        // (group 62) with the record 0 (Melzas, prefab b06c2c33, animation 0 asks 219) made loadable (bit 0x40 of its SpriteDirection), a game with a fake
        // audio backend. 219 is redirected by the bank to 864 in the group 62 (it plays its own tones in no group).
        var root = ProjectRootFinder.Find();
        var bank = new AlundraSoundBank(root);
        Assert.True(bank.TryResolve(219, soundGroup: 62, out var inGroup));
        Assert.Equal(864, inGroup.ResolvedId);
        Assert.True(bank.TryResolve(219, soundGroup: null, out var ownTones));
        Assert.Equal(219, ownTones.ResolvedId);

        var backend = new FakeAudioBackend();
        var provider = new FakeAudioClipProvider();
        foreach (var tone in inGroup.Tones)
        {
            provider.Register(tone.AssetId, new FakeAudioClip("group62"));
        }

        foreach (var tone in ownTones.Tones)
        {
            provider.Register(tone.AssetId, new FakeAudioClip("own219"));
        }

        using var arc = new ArcRun(AlundraVisionArcTests.A4pSpec, world =>
        {
            var audioComponent = (AudioSystemComponent)RuntimeHelpers.GetUninitializedObject(typeof(AudioSystemComponent));
            typeof(AudioSystemComponent).GetField("<Service>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(audioComponent, new AudioService(backend) { ClipProvider = provider });
            typeof(CasaEngineGame).GetField("<AudioSystemComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(world.Game, audioComponent);

            var tileMap = world.Entities.OfType<Entity>().Select(e => e.RootComponent).OfType<TileMapComponent>().Single();
            var records = tileMap.TileMapData!.ObjectLayers.Single(layer => layer.Name == "Entities").Objects;
            var melzas = records.Single(r => r.CustomProperties["Index"] == "0");
            Assert.Equal("128", melzas.CustomProperties["SpriteDirection"]);
            melzas.CustomProperties["SpriteDirection"] = "192";
        });

        Assert.NotNull(arc.EntityByRecord(0)); // loaded at the map load.
        Assert.Equal(new[] { "group62" }, backend.PlayCalls.Select(call => ((FakeAudioClip)call.Clip).Name).ToArray());
    }
}
