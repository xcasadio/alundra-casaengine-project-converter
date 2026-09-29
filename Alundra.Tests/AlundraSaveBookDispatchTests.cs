#nullable enable
using System;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E16.e T1 (docs/plan-e16-etat-partie.md, L1, L6, SE6): the generic ETC text access
/// (<see cref="AlundraEtcStringTable.TryResolveText"/>, on the real export's <c>Dialogues/Etc.dialogue</c>), and
/// the dispatch of <see cref="AlundraEventProgramRunner.RunSpriteEvent"/> to the save book - only for sprite type
/// 237, a native slot, and (F, 1) or (C, 72) - with the sprite type retained by both spawn paths.
/// </summary>
public sealed class AlundraSaveBookDispatchTests : IDisposable
{
    public AlundraSaveBookDispatchTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        AlundraEtcStringTable.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
    }

    private static void LoadRealEtc()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(
            DialogueTestAssets.LoadFromDisk(Path.Combine(SaveGameDirectorTestSupport.FindProjectRoot(), "Dialogues", "Etc.dialogue")));
    }

    // ---- L6: TryResolveText ----------------------------------------------------------------------------

    [Theory]
    [InlineData(0x40, "Enregistrer tes progrès?")]
    [InlineData(0x4A, "OUI")]
    [InlineData(0x4B, "NON")]
    [InlineData(0, "Un Nouveau Départ")]
    [InlineData(0x87, "Examen de la Carte Mémoire . . .")]
    public void TryResolveText_RealExport_ReadsTheEtcText(int etcIndex, string expected)
    {
        LoadRealEtc();

        Assert.True(AlundraEtcStringTable.TryResolveText(etcIndex, out var text));
        Assert.Equal(expected, text);
    }

    /// <summary>J5: <c>0x88</c> is one of the 8 texts empty in <c>ETC_RES.R</c> itself - an empty line, as the original
    /// draws nothing.</summary>
    [Fact]
    public void TryResolveText_EmptyOriginalEntry_IsAnEmptyLineNotAFailure()
    {
        LoadRealEtc();

        Assert.True(AlundraEtcStringTable.TryResolveText(0x88, out var text));
        Assert.Equal(string.Empty, text);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1024)]
    public void TryResolveText_OutsideTheTable_ReturnsFalse(int etcIndex)
    {
        LoadRealEtc();

        Assert.False(AlundraEtcStringTable.TryResolveText(etcIndex, out var text));
        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void EtcDialogueAsset_IsTheLoadedAsset()
    {
        Assert.Null(AlundraEtcStringTable.EtcDialogueAsset);

        LoadRealEtc();

        Assert.NotNull(AlundraEtcStringTable.EtcDialogueAsset);
        Assert.True(AlundraEtcStringTable.EtcDialogueAsset!.LineTexts.ContainsKey("line:Etc_0064_p0"));
    }

    // ---- L1: the dispatch ------------------------------------------------------------------------------

    /// <summary>An entity as the spawn factory leaves a save book of the real export (header of prefab
    /// <c>1ab20de9-...</c>: ProgramLoad 254, ProgramTick 72, ProgramInteract 1), with <paramref name="slot"/>
    /// picked.</summary>
    private static AlundraEntityScriptProxy Entity(int spriteType, int slot, int code)
    {
        var entity = new AlundraEntityScriptProxy { SpriteType = spriteType, Status = EntityStatus.Normal, EventTrigger = slot };
        entity.SpriteProgramIndexes[slot] = code;
        return entity;
    }

    [Theory]
    [InlineData(ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode)]
    [InlineData(ScriptHelper.ProgramCTick, AlundraSaveBook.TickCode)]
    public void RunPickedEvent_SaveBookSlot_ReachesTheBook(int slot, int code)
    {
        var runner = new AlundraEventProgramRunner(null, AlundraGameState.Instance);
        var book = Entity(AlundraSaveBook.SpriteType, slot, code);

        book.RunPickedEvent(runner);

        Assert.Equal(1, runner.SpriteEventRunCount);
        Assert.Equal(1, runner.SaveBookEventRunCount);
        Assert.Equal(ScriptHelper.ProgramUnknown, book.EventTrigger);
    }

    /// <summary>J6/L1: slot F code 1 is shared by other entities, whose native AI is not ported - they stay a counted
    /// no-op and never lock the hero.</summary>
    [Theory]
    [InlineData(30, ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode)]
    [InlineData(30, ScriptHelper.ProgramCTick, AlundraSaveBook.TickCode)]
    [InlineData(AlundraSaveBook.SpriteType | 0x100, ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode)]
    [InlineData(-1, ScriptHelper.ProgramCTick, AlundraSaveBook.TickCode)]
    public void RunPickedEvent_OtherSpriteType_WithABookCode_IsACountedNoOp(int spriteType, int slot, int code)
    {
        var runner = new AlundraEventProgramRunner(null, AlundraGameState.Instance);
        var entity = Entity(spriteType, slot, code);

        entity.RunPickedEvent(runner);

        Assert.Equal(1, runner.SpriteEventRunCount);
        Assert.Equal(0, runner.SaveBookEventRunCount);
        Assert.Equal(0u, AlundraGameState.Instance.PlayerControlFlags);
        Assert.Equal(new byte[4], entity.Bytes);
    }

    [Theory]
    [InlineData(ScriptHelper.ProgramALoad, 254)]
    [InlineData(ScriptHelper.ProgramFInteract, 255)]
    [InlineData(ScriptHelper.ProgramFInteract, AlundraSaveBook.TickCode)]
    [InlineData(ScriptHelper.ProgramCTick, AlundraSaveBook.InteractCode)]
    [InlineData(ScriptHelper.ProgramCTick, 73)]
    [InlineData(ScriptHelper.ProgramDTouch, AlundraSaveBook.InteractCode)]
    [InlineData(ScriptHelper.ProgramEDeactivate, AlundraSaveBook.TickCode)]
    public void RunPickedEvent_SaveBookType_WithAnotherSlotOrCode_IsACountedNoOp(int slot, int code)
    {
        var runner = new AlundraEventProgramRunner(null, AlundraGameState.Instance);
        var book = Entity(AlundraSaveBook.SpriteType, slot, code);

        book.RunPickedEvent(runner);

        Assert.Equal(1, runner.SpriteEventRunCount);
        Assert.Equal(0, runner.SaveBookEventRunCount);
        Assert.Equal(0u, AlundraGameState.Instance.PlayerControlFlags);
    }

    /// <summary>L1: a script program on the slot (<c>ProgramIndexes[slot] &amp; 0x7f != 0</c>) is never the native
    /// handler, even when <see cref="AlundraEventProgramRunner.RunSpriteEvent"/> is reached directly.</summary>
    [Fact]
    public void RunSpriteEvent_SaveBookSlotCarryingAScriptProgram_IsNotTheBook()
    {
        var runner = new AlundraEventProgramRunner(null, AlundraGameState.Instance);
        var book = Entity(AlundraSaveBook.SpriteType, ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode);
        book.ProgramIndexes[ScriptHelper.ProgramFInteract] = 0x85;

        runner.RunSpriteEvent(book);

        Assert.Equal(1, runner.SpriteEventRunCount);
        Assert.Equal(0, runner.SaveBookEventRunCount);
    }

    // ---- SE6: both spawn paths retain the sprite type ---------------------------------------------------

    private static TileMapObjectData Record(Guid prefabAssetId, string? spriteTableIndex, string spriteDirection)
    {
        var record = new TileMapObjectData { Id = 0, Name = "Entity_0" };
        record.CustomProperties["Index"] = "0";
        record.CustomProperties["XPos"] = "6";
        record.CustomProperties["YPos"] = "40";
        record.CustomProperties["Height"] = "0";
        record.CustomProperties["SpriteDirection"] = spriteDirection;
        record.CustomProperties["PrefabAssetId"] = prefabAssetId.ToString();
        if (spriteTableIndex != null)
        {
            record.CustomProperties["SpriteTableIndex"] = spriteTableIndex;
        }

        return record;
    }

    private static SpriteRecordHeader BookHeader() => new()
    {
        MoreFlags = 128,
        CanPickup = 225,
        FlagsPortraitShadowType = 3,
        ProgramLoad = 254,
        ProgramTick = AlundraSaveBook.TickCode,
        ProgramInteract = AlundraSaveBook.InteractCode,
        OffsetX = -12,
        OffsetY = -8,
        SizeX = 24,
        SizeY = 16,
        SizeZ = 16,
    };

    /// <summary>Map 17's save book (SpriteTableIndex 237, SpriteDirection 0) is type 237; a map sprite
    /// (SpriteDirection bit 0x80, e.g. map 17's « Roi Mimming », index 122) is 0x100 further.</summary>
    [Theory]
    [InlineData("237", "0", 237)]
    [InlineData("237", "64", 237)]
    [InlineData("122", "128", 0x100 | 122)]
    [InlineData("237", "192", 0x100 | 237)]
    [InlineData(null, "0", -1)]
    public void SpawnFactory_KnownHeader_RetainsTheOriginalSpriteType(string? spriteTableIndex, string spriteDirection, int expected)
    {
        var prefabAssetId = Guid.NewGuid();
        var record = Record(prefabAssetId, spriteTableIndex, spriteDirection);
        var proxy = new AlundraEntityScriptProxy();
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);

        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog().Add(prefabAssetId, BookHeader()));

        Assert.Equal(expected, proxy.SpriteType);
    }

    [Fact]
    public void SpawnFactory_NoHeader_LeavesTheSpriteTypeUnknown()
    {
        var record = Record(Guid.NewGuid(), "237", "0");
        var proxy = new AlundraEntityScriptProxy();
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);

        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog());

        Assert.Equal(-1, proxy.SpriteType);
    }

    [Fact]
    public void HeroHeader_SetsSpriteTypeZero()
    {
        var proxy = new AlundraEntityScriptProxy { IsPlayer = true };

        AlundraWorldProxy.ApplyHeroSpriteHeader(proxy, BookHeader());

        Assert.Equal(0, proxy.SpriteType);
        Assert.Equal(AlundraSaveBook.TickCode, proxy.SpriteProgramIndexes[ScriptHelper.ProgramCTick]);
    }

    /// <summary>A spawned save book, end to end through the factory: the dispatch recognises it.</summary>
    [Fact]
    public void SpawnedSaveBook_TickSlot_ReachesTheBook()
    {
        var prefabAssetId = Guid.NewGuid();
        var record = Record(prefabAssetId, "237", "0");
        var proxy = new AlundraEntityScriptProxy();
        AlundraEntitySpawnFactory.ApplyRecord(record, proxy);
        AlundraEntitySpawnFactory.ApplySpawnInitialization(record, new Entity(), proxy, new FakeSpriteRecordCatalog().Add(prefabAssetId, BookHeader()));
        proxy.EventTrigger = ScriptHelper.ProgramCTick;
        var runner = new AlundraEventProgramRunner(null, AlundraGameState.Instance);

        proxy.RunPickedEvent(runner);

        Assert.Equal(1, runner.SaveBookEventRunCount);
    }

    [Fact]
    public void Clone_CopiesTheSpriteType()
    {
        var original = new AlundraEntityScriptProxy { SpriteType = AlundraSaveBook.SpriteType };

        var clone = (AlundraEntityScriptProxy)original.Clone();

        Assert.Equal(AlundraSaveBook.SpriteType, clone.SpriteType);
    }
}
