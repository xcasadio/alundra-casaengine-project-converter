#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f4b F4B-1, "règles" (docs/plan-e19-opcodes.md, F4B-R1 to F4B-R4): who the speaker is for each of the three opcodes, the order of the refusals of the name, the
/// speaker the portrait keeps, the portrait field of the proxy, the scroll of the camera, the close trigger of the box, and what resets the name and the portrait. Every
/// number written here is derived by hand from the binary's rules (docs/plan-e19-f4-annexe/binary-notes.md), not read back from the code under test.
/// </summary>
public sealed class AlundraDialogueSpeakerRuleTests
{
    // ---- who the speaker is ------------------------------------------------------------------------------------------------

    [Fact]
    public void Dialog0x0D_TakesTheLogicEntityOfTheProgram_NotItsOwner()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(-1, portrait: false);
        var logic = SpeakerRig.Speaker(0x104, portrait: true);
        owner.LogicEntity = logic;

        Assert.True(rig.Run(owner, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        Assert.True(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(0x104, rig.Director.NameBox.NameId);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, rig.Director.Portrait.State);
        Assert.Same(logic, rig.Director.PortraitSpeaker);
    }

    [Fact]
    public void Dialog0x0D_AfterA0x43_TakesTheEntityTheSearchFound()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(-1, portrait: false);
        var found = SpeakerRig.Speaker(0x10C, portrait: true, refId: 7);
        rig.World.Spawned.Add(found);

        Assert.True(rig.Run(owner, new[] { 0x43, 7, 0x0D, SpeakerRig.TextAb, 1, 0xFF }));

        Assert.Same(found, owner.LogicEntity);
        Assert.Equal(0x10C, rig.Director.NameBox.NameId);
        Assert.Same(found, rig.Director.PortraitSpeaker);
    }

    [Fact]
    public void Dialog0x5C_TwoEntitiesWithTheSameRefId_TakesTheFirstOfTheSpawnedEntities()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(-1, portrait: false);
        var first = SpeakerRig.Speaker(0x104, portrait: true, refId: 5);
        var second = SpeakerRig.Speaker(0x10C, portrait: false, refId: 5);
        rig.World.Spawned.Add(first);
        rig.World.Spawned.Add(second);

        Assert.True(rig.Run(owner, SpeakerRig.Codes5C(5, SpeakerRig.TextAb)));

        Assert.Equal(0x104, rig.Director.NameBox.NameId);
        Assert.Same(first, rig.Director.PortraitSpeaker);

        // The other order of the list: the first is the one without a portrait.
        rig.Director.InstallForMapEntry();
        rig.World.Spawned.Reverse();
        Assert.True(rig.Run(owner, SpeakerRig.Codes5C(5, SpeakerRig.TextCd)));

        Assert.Equal(0x10C, rig.Director.NameBox.NameId);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
    }

    [Fact]
    public void Dialog0x5C_WhenNobodyMatches_OpensTheBoxWithoutNameOrPortrait()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(0x104, portrait: true);

        Assert.True(rig.Run(owner, SpeakerRig.Codes5C(5, SpeakerRig.TextAb)));

        Assert.True(rig.Director.IsOpen);
        Assert.False(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
        Assert.Null(rig.Director.PortraitSpeaker);
    }

    [Fact]
    public void Dialog0xC4_TakesTheNameOfItsOperands_AndThePortraitOfTheEntityFound()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(0x104, portrait: true);

        Assert.True(rig.Run(owner, SpeakerRig.CodesC4(0x80, 0x10C, SpeakerRig.TextAb)));

        Assert.Equal(0x10C, rig.Director.NameBox.NameId); // not the 0x104 of the entity
        Assert.Same(owner, rig.Director.PortraitSpeaker);
        Assert.Equal(AlundraInventoryPortrait.StateOpening, rig.Director.Portrait.State);
    }

    [Fact]
    public void Dialog0xC4_WhenNobodyMatches_HasNoNameEvenWithOperands()
    {
        using var rig = new SpeakerRig();
        var owner = SpeakerRig.Speaker(0x104, portrait: true);

        Assert.True(rig.Run(owner, SpeakerRig.CodesC4(0x05, 0x10C, SpeakerRig.TextAb)));

        Assert.True(rig.Director.IsOpen);
        Assert.False(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
    }

    [Fact]
    public void WithoutAPresenter_NeitherNameNorPortrait()
    {
        using var rig = new SpeakerRig(attachPresenter: false);
        var owner = SpeakerRig.Speaker(0x104, portrait: true);

        Assert.False(rig.Run(owner, SpeakerRig.Codes0D(SpeakerRig.TextAb))); // the degraded advance: no box opened

        Assert.False(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
    }

    // ---- the refusals of the name, in their order, at the level of the opcode ---------------------------------------------------

    [Theory]
    [InlineData(-1)]   // an entity that has no sprite type (no header resolved)
    [InlineData(0)]    // the hero
    [InlineData(0xFF)] // a common sprite
    public void ABareEntity_NeverAsksTheEtc(int spriteType)
    {
        using var rig = new SpeakerRig(loadEtc: false);
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var owner = SpeakerRig.Speaker(spriteType, portrait: false);

        Assert.True(rig.Run(owner, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        Assert.DoesNotContain(log.Warnings, w => w.Contains("could not resolve an ETC text", StringComparison.Ordinal));
        Assert.False(rig.Director.NameBox.IsSlotOpen);
    }

    [Fact]
    public void ARetryTick_WithANameAlreadyOpen_NeverAsksTheEtc()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: false);
        Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb)));
        AlundraEtcStringTable.ResetForTests(); // from now on any lookup of the ETC logs a warning
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();

        for (var tick = 0; tick < 5; tick++)
        {
            Assert.False(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextCd))); // retry (0): a box is open
        }

        Assert.DoesNotContain(log.Warnings, w => w.Contains("could not resolve an ETC text", StringComparison.Ordinal));
    }

    [Fact]
    public void ANamedEntity_WithoutALoadedEtc_AsksItOnce_AndHasNoName()
    {
        using var rig = new SpeakerRig(loadEtc: false);
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var owner = SpeakerRig.Speaker(0x104, portrait: false);

        Assert.True(rig.Run(owner, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        Assert.Single(log.Warnings, w => w.Contains("could not resolve an ETC text", StringComparison.Ordinal));
        Assert.False(rig.Director.NameBox.IsSlotOpen);
    }

    // ---- the portrait: one speaker, kept; the field of the proxy -------------------------------------------------------------------

    [Fact]
    public void AStartIgnoredWhileTheMachineIsBusy_LeavesTheSpeakerOfTheAcceptedStart()
    {
        using var rig = new SpeakerRig();
        var a = SpeakerRig.Speaker(0x104, portrait: true, x: 200);
        var b = SpeakerRig.Speaker(0x10C, portrait: true, x: 300);
        Assert.True(rig.Run(a, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        Assert.False(rig.Run(b, SpeakerRig.Codes0D(SpeakerRig.TextCd))); // retry: a box is open, the start of b is ignored (the machine is busy), its name too (busy)

        Assert.Same(a, rig.Director.PortraitSpeaker);
        Assert.Equal(0x104, rig.Director.NameBox.NameId);

        Assert.True(rig.PassUntilCloseTrigger() > 0); // this pass draws the return at rest (c = 15)
        rig.Pass();                                   // c = 14: from the head of a (160, 98): x = 160 + trunc((8 - 160) * 14 / 15) = 19, y = 98 + trunc(18 * 14 / 15) = 114
        Assert.Equal((19, 114), (rig.Director.Portrait.X, rig.Director.Portrait.Y)); // the head of b would give x = 25
    }

    [Fact]
    public void ASpeakerMarkedForDestruction_KeepsItsLastPosition_ForTheReturn()
    {
        using var rig = new SpeakerRig();
        var a = SpeakerRig.Speaker(0x104, portrait: true, x: 200);
        Assert.True(rig.Run(a, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        a.Status = EntityStatus.FlagToDestroy; // destroyed (then recycled): its words keep their last values
        Assert.True(rig.PassUntilCloseTrigger() > 0);
        rig.Pass();

        Assert.Equal((19, 114), (rig.Director.Portrait.X, rig.Director.Portrait.Y));
    }

    [Fact]
    public void ASpeakerWithTheFlagButNoField_OpensTheName_AndWarnsOncePerSpriteType()
    {
        using var rig = new SpeakerRig();
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();
        var jess = SpeakerRig.Speaker(0x104, portrait: true);
        jess.DialoguePortrait = null;

        Assert.True(rig.Run(jess, SpeakerRig.Codes0D(SpeakerRig.TextAb)));
        Assert.False(rig.Run(jess, SpeakerRig.Codes0D(SpeakerRig.TextCd))); // the retry ticks must not warn again
        Assert.False(rig.Run(jess, SpeakerRig.Codes0D(SpeakerRig.TextCd)));

        Assert.True(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
        Assert.Single(log.Warnings, w => w.Contains("DialoguePortrait", StringComparison.Ordinal));
        Assert.Empty(log.Errors);

        rig.Director.InstallForMapEntry();
        var septimus = SpeakerRig.Speaker(0x10C, portrait: true);
        septimus.DialoguePortrait = null;
        Assert.True(rig.Run(septimus, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        Assert.Equal(2, log.Warnings.Count(w => w.Contains("DialoguePortrait", StringComparison.Ordinal)));
    }

    private static TileMapObjectData Record(Guid prefabAssetId)
    {
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "6";
        record.CustomProperties["YPos"] = "40";
        record.CustomProperties["Height"] = "0";
        record.CustomProperties["SpriteDirection"] = "128";
        record.CustomProperties["SpriteTableIndex"] = "4";
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();
        return record;
    }

    private static SpriteRecordHeader Header(DialoguePortraitRef? portrait) => new()
    {
        MoreFlags = 128,
        CanPickup = 161,
        FlagsPortraitShadowType = portrait is null ? 3 : 131,
        OffsetX = -10,
        OffsetY = -7,
        SizeX = 20,
        SizeY = 14,
        SizeZ = 32,
        DialoguePortrait = portrait,
    };

    [Fact]
    public void TheSpawnCopiesThePortraitOfItsHeader_AndTheCloneCopiesIt()
    {
        var prefab = Guid.NewGuid();
        var portrait = new DialoguePortraitRef(Guid.NewGuid(), 48, 72);
        var proxy = new AlundraEntityScriptProxy();
        var record = Record(prefab);
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);

        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog().Add(prefab, Header(portrait)));

        Assert.Equal(portrait, proxy.DialoguePortrait);
        Assert.Equal(EntityFlags.HasPortrait, proxy.Flags & EntityFlags.HasPortrait);
        var clone = (AlundraEntityScriptProxy)proxy.Clone();
        Assert.Equal(portrait, clone.DialoguePortrait);
    }

    [Fact]
    public void ASpawnWithoutAPortrait_AndTheHero_HaveNone()
    {
        var prefab = Guid.NewGuid();
        var proxy = new AlundraEntityScriptProxy();
        var record = Record(prefab);
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);
        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog().Add(prefab, Header(null)));
        Assert.Null(proxy.DialoguePortrait);

        var hero = new AlundraEntityScriptProxy { IsPlayer = true };
        AlundraWorldProxy.ApplyHeroSpriteHeader(hero, Header(new DialoguePortraitRef(Guid.NewGuid(), 48, 56)));

        Assert.Null(hero.DialoguePortrait);
        Assert.Null(((AlundraEntityScriptProxy)hero.Clone()).DialoguePortrait);
    }

    // ---- the scroll of the camera --------------------------------------------------------------------------------------------------

    [Fact]
    public void WithoutAScrollSource_TheScrollIsZeroZero()
    {
        using var rig = new SpeakerRig();
        rig.Director.ScrollSource = null;
        var speaker = SpeakerRig.Speaker(0x104, portrait: true, x: 200, y: 150, z: 0);

        Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb)));
        rig.Pass();

        Assert.Equal((200, 118), (rig.Director.Portrait.X, rig.Director.Portrait.Y)); // the first pass is at the head (200 - 0, 150 - 0 - 0 - 32)
    }

    [Fact]
    public void TheProxyInstallsTheScrollSource_OfItsCamera_AndItSurvivesAnAttachment()
    {
        var director = AlundraDialogueDirector.Instance;
        director.ResetForTests();
        try
        {
            var world = new CasaEngine.Framework.Scene.World.World { Name = "TestWorld" };
            world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });
            var proxy = new AlundraWorldProxy();
            proxy.InitializeWithWorld(world); // no tile map in this world: the installs are not reached, the one under test is called as the install block calls it
            proxy.InstallDialogueSystems(world); // the dialogue systems, the scroll source among them
            proxy._cameraDirector.ResolveDebugCameraOnce(world);
            var camera = proxy._cameraDirector.ResolvedCamera;
            Assert.NotNull(camera);
            Assert.NotNull(director.ScrollSource);

            camera!.Target = new Vector3(333f, -222f, 0f);
            Assert.Equal(AlundraCameraMath.ToOriginalScrollSpace(camera.Target), director.ScrollSource!());

            director.AttachToWorld(null, proxy.GameState); // a re-attachment does not drop it
            Assert.NotNull(director.ScrollSource);
            Assert.Equal(AlundraCameraMath.ToOriginalScrollSpace(camera.Target), director.ScrollSource!());

            director.ResetForTests();
            Assert.Null(director.ScrollSource);
        }
        finally
        {
            director.ResetForTests();
            AlundraGameState.Instance.ResetForTests();
        }
    }

    // ---- the close trigger of the box ---------------------------------------------------------------------------------------------

    private sealed class RecordingHost : IAlundraDialogueBoxHost
    {
        public List<string> Events { get; } = new();

        public void FlagReached(int flag) => Events.Add("flag:" + flag);

        public void PlaySound(int sfxId) => Events.Add("sound:" + sfxId);

        public void PageTurned() => Events.Add("page");

        public void Released() => Events.Add("released");

        public int Advance(char display) => 8;

        public void CloseTriggered() => Events.Add("close");
    }

    private static void RunABox(AlundraDialogueBox box)
    {
        box.Open();
        box.AppendPage(new[] { new DialogueToken(DialogueTokenKind.Character, 0, 'A'), new DialogueToken(DialogueTokenKind.Character, 0, 'B') }, hasFollowingPage: false);
        for (var pass = 0; pass < 200 && (box.IsActive || pass == 0); pass++)
        {
            box.Pass(true, true);
        }
    }

    [Fact]
    public void CloseTriggered_IsCalledRightAfterSound7_OncePerClose_NeverAtOpenOrReset()
    {
        var host = new RecordingHost();
        var box = new AlundraDialogueBox(host);

        box.Open();
        Assert.DoesNotContain("close", host.Events);
        box.Reset();
        Assert.DoesNotContain("close", host.Events);

        RunABox(box);
        var events = host.Events;
        Assert.Equal(1, events.Count(e => e == "close"));
        var sevens = events.Select((e, i) => (e, i)).Where(t => t.e == "sound:7").Select(t => t.i).ToArray();
        Assert.Single(sevens);
        Assert.Equal("close", events[sevens[0] + 1]);
        Assert.Contains("released", events.Skip(sevens[0] + 2));

        RunABox(box); // a second box: a second close, and no more
        Assert.Equal(2, host.Events.Count(e => e == "close"));
        Assert.Equal(2, host.Events.Count(e => e == "sound:7"));
    }

    [Fact]
    public void TheDirectorPass_RunsTheNameThenThePortrait_AfterTheBox_AndTheCloseTriggerStartsBoth()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb)));

        rig.Pass();
        Assert.Equal(320, rig.Director.NameBox.Drawn!.FrameX);
        Assert.Equal(AlundraPortraitPhase.In, rig.Director.Portrait.Phase);

        var passes = rig.PassUntilCloseTrigger();
        Assert.True(passes > 0);
        Assert.Equal(AlundraDialogueNameBox.FlagClosing | AlundraDialogueNameBox.FlagOpen, rig.Director.NameBox.Flags);
        Assert.Equal(AlundraPortraitPhase.Out, rig.Director.Portrait.Phase);
        Assert.Equal((64, 8, 116), (rig.Director.NameBox.Drawn!.FrameX, rig.Director.Portrait.X, rig.Director.Portrait.Y)); // the first closing passes are at the rest
    }

    // ---- what resets the name and the portrait -------------------------------------------------------------------------------------

    [Fact]
    public void TheMapEntry_ClearsTheNameAndThePortrait()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb)));
        rig.Pass();

        rig.Director.InstallForMapEntry();

        Assert.False(rig.Director.NameBox.IsSlotOpen);
        Assert.Equal(0, rig.Director.NameBox.Flags);
        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
        Assert.Null(rig.Director.PortraitSpeaker);
        rig.Pass();
        Assert.Null(rig.Director.NameBox.Drawn);
        Assert.False(rig.Director.Portrait.DrawnThisStep);
    }

    [Fact]
    public void AnOutOfBandClose_ClearsAPortraitLeftByAnAbandonedAttempt_EvenWithNoBoxOpen()
    {
        using var rig = new SpeakerRig();
        var speaker = SpeakerRig.Speaker(0x104, portrait: true);
        rig.Director.OpenSpeaker(speaker); // an attempt that opened the satellites and then no box
        Assert.False(rig.Director.IsOpen);
        Assert.NotEqual(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
        Assert.True(rig.Director.NameBox.IsSlotOpen);

        rig.Director.NotifyPresenterClosed();

        Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
        Assert.False(rig.Director.NameBox.IsSlotOpen);
        Assert.Null(rig.Director.PortraitSpeaker);
    }

    [Fact]
    public void ResetForTests_ClearsEverything_ButNotTheInventorysPortrait()
    {
        AlundraInventoryPortrait.Instance.ResetForTests();
        try
        {
            using var rig = new SpeakerRig();
            AlundraInventoryPortrait.Instance.Start(10, 10);
            using var log = SaveGameDirectorTestSupport.LogCapture.Install();
            var speaker = SpeakerRig.Speaker(0x104, portrait: true);
            speaker.DialoguePortrait = null;
            Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb))); // one warning (the flag without a field)
            Assert.Single(log.Warnings, w => w.Contains("DialoguePortrait", StringComparison.Ordinal));

            rig.Director.ResetForTests();

            Assert.False(rig.Director.NameBox.IsSlotOpen);
            Assert.Equal(AlundraInventoryPortrait.StateIdle, rig.Director.Portrait.State);
            Assert.Null(rig.Director.ScrollSource);
            Assert.Equal(AlundraInventoryPortrait.StateOpening, AlundraInventoryPortrait.Instance.State);

            // the warnings emitted are forgotten: the same sprite type warns again
            rig.Director.AttachToWorld(new CasaEngine.Framework.Dialogue.Runtime.DialogueService(), rig.GameState, rig.Sound);
            rig.Director.InstallForMapEntry();
            AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((0x104, "Jess")));
            rig.Director.AdvanceProviderForTests = SpeakerRig.Advance;
            Assert.True(rig.Run(speaker, SpeakerRig.Codes0D(SpeakerRig.TextAb)));
            Assert.Equal(2, log.Warnings.Count(w => w.Contains("DialoguePortrait", StringComparison.Ordinal)));
        }
        finally
        {
            AlundraInventoryPortrait.Instance.ResetForTests();
        }
    }

    // ---- the production advances of font3 give the 60 widths of the binary ---------------------------------------------------------

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (File.Exists(Path.Combine(candidate, "UI", "font3.fnt")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"AlundraDialogueSpeakerRuleTests: no 'alundra-project/UI/font3.fnt' found above '{AppContext.BaseDirectory}' (the real converter export is needed).");
    }

    [Fact]
    public void TheProductionAdvancesOfFont3_GiveTheSixtyWidthsOfTheBinary()
    {
        var advances = new AlundraFont3Advances(FindProjectRoot());
        var mismatches = new List<string>();
        foreach (var (id, (name, width)) in SpeakerAnnex.Names())
        {
            var box = new AlundraDialogueNameBox();
            Assert.True(box.TryOpen(id, _ => name, advances.Advance), $"name 0x{id:x} {name}");
            if (box.TextWidth != width)
            {
                mismatches.Add($"0x{id:x} {name}: {box.TextWidth}, binary {width}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }
}
