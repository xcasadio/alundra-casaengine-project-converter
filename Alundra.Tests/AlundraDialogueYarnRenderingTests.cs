#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c T5 (docs/plan-e15-yarn.md, E15.c contract items 1, 2, 4, 6 for dialogue files, 8): the DLL reads
/// the Yarn assets instead of raw text - glyph/line-break rendering into font3 text, empty pages/absent
/// nodes opening an empty box, each page's commands running exactly at its own display, the shared table
/// resolving by node instead of staying empty, and the degraded (no-director) path running every page's
/// commands headless.
/// </summary>
public sealed class AlundraDialogueYarnRenderingTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;

    public AlundraDialogueYarnRenderingTests()
    {
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
    }

    public void Dispose()
    {
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
    }

    private static EventProgramDocument NewDocument(params int[] codes)
    {
        return new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
    }

    private sealed class FakeEntityWorldContext : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public IAlundraDialogueDirector? DialogueDirector { get; set; }
    }

    private static void AttachRealDialogueContext(AlundraGameState gameState)
    {
        Director.AttachToWorld(new DialogueService(), gameState);
        Director.InstallForMapEntry();
    }

    // ---- item 4: [glyph/] -> font3 character, no stray operand digit -------------------------------

    [Fact]
    public void GlyphMarker_BecomesFont3Character_NoStrayOperandDigit()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        // The real corpus shape: "\W2" at the end of a clause ("Ouf\W2 Je l'ai échappé belle") - glyph 18
        // (the ellipsis), never a literal "2" left over (ADR-0006's whole visible-change point).
        var asset = DialogueTestAssets.BuildRaw(
            "Ellipsis", "Start", "Ouf[glyph id=18 trimwhitespace=false/] Je l'ai échappé belle");

        Director.Open(asset, "Start", controlMode: 1);

        var text = Director.CurrentLineForTests?.Text ?? string.Empty;
        Assert.Equal("Ouf" + (char)18 + " Je l'ai échappé belle", text);
        Assert.DoesNotContain("2", text);
    }

    // ---- item 4: [br/] -> line break -----------------------------------------------------------

    [Fact]
    public void BrMarker_BecomesLineBreak()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.BuildRaw("BrPage", "Start", "line one[br trimwhitespace=false/]line two");

        Director.Open(asset, "Start", controlMode: 1);

        Assert.Equal("line one\nline two", Director.CurrentLineForTests?.Text);
    }

    // ---- item 2/D-E15-10: [empty/] page and an absent node both open an empty box -----------------

    [Fact]
    public void EmptyMarkerPage_ShowsAnEmptyBox()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.BuildRaw("EmptyPage", "Start", "[empty trimwhitespace=false/]");

        Director.Open(asset, "Start", controlMode: 1);

        Assert.True(Director.IsOpen);
        Assert.Equal(string.Empty, Director.CurrentLineForTests?.Text);
    }

    [Fact]
    public void AbsentNode_OpensAnEmptyBox_LikeAnUnexportedSlotDidBeforeE15c()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.SinglePage("SomeNode", "text", node: "M1_S000");

        Director.Open(asset, "M1_S999", controlMode: 1); // no such node on this asset.

        Assert.True(Director.IsOpen);
        Assert.Equal(string.Empty, Director.CurrentLineForTests?.Text);
        Assert.Equal(1, Director.PageCountForTests);
    }

    [Fact]
    public void NullAsset_OpensAnEmptyBox()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        Director.Open(null, null, controlMode: 1);

        Assert.True(Director.IsOpen);
        Assert.Equal(string.Empty, Director.CurrentLineForTests?.Text);
    }

    // ---- item 2: each page's own commands run exactly at its own display, never earlier -----------

    [Fact]
    public void PageCommands_RunOnlyWhenTheirOwnPageIsShown_NeverBefore()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.Build(
            "FlagsAcrossPages", "Start",
            "<<flag 10>>\npage zero", "page one", "<<flag 20>>\npage two");

        Director.Open(asset, "Start", controlMode: 1);

        var flag10 = 10u | 0x8000u;
        var mask10 = 1u << (10 & 0x1f);
        var flag20 = 20u | 0x8000u;
        var mask20 = 1u << (20 & 0x1f);

        // Page 0 just displayed: its own flag is set, the later page's flag is not.
        Assert.NotEqual(0u, gameState.GetFlag(flag10) & mask10);
        Assert.Equal(0u, gameState.GetFlag(flag20) & mask20);

        // E19.f2a: the director asks the Yarn runner for a page at the release of the cursor of the page before. The cursor of page 0 (9 letters) shows at the
        // pass 55 and a press the box sees at 56 turns to page 1 (no command there): still not set.
        var passes = new DialogueBoxPassDriver(Director, gameState);
        passes.PressSeenAt(56);
        passes.PressSeenAt(93);
        passes.RunTo(55);
        Assert.Equal(0, Director.PageIndexForTests);
        passes.RunTo(56);
        Assert.Equal(1, Director.PageIndexForTests);
        Assert.Equal(0u, gameState.GetFlag(flag20) & mask20);

        // The cursor of page 1 (8 letters, typed from the pass 60) shows at 92 and a press seen at 93 turns to page 2: only NOW does its own flag get set.
        passes.RunTo(92);
        Assert.Equal(0u, gameState.GetFlag(flag20) & mask20);
        passes.RunTo(93);
        Assert.Equal(2, Director.PageIndexForTests);
        Assert.NotEqual(0u, gameState.GetFlag(flag20) & mask20);
        Assert.Contains("page two", Director.CurrentLineForTests?.Text ?? string.Empty);
    }

    // ---- item 1: the shared table (map_alundra, bit 0x80 clear) resolves and displays --------------

    [Fact]
    public void SharedTable_BitClear_ResolvesSharedNode_AndDisplaysIt()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);
        var worldContext = new FakeEntityWorldContext { DialogueDirector = Director };

        var sharedAsset = DialogueTestAssets.SinglePage("Shared", "a shared phrase", node: "Shared_S005");
        var document = NewDocument(0x0D, 5, 1, 0xFF); // textId=5, bit 0x80 CLEAR -> shared table.
        var runner = new AlundraEventProgramRunner(document, gameState, worldContext) { SharedDialogueAsset = sharedAsset };
        var entity = new AlundraEntityScriptProxy();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.Equal("a shared phrase", Director.CurrentLineForTests?.Text);
    }

    // ---- item 8: the degraded (no-director) path runs every page's commands, falcon_update included ---

    [Fact]
    public void DegradedPath_NoDirector_RunsEveryPagesCommands_FalconUpdateIncluded()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.FalconTemp = 4;
        var context = new FakeEntityWorldContext { DialogueDirector = null };

        var mapAsset = DialogueTestAssets.Build(
            "DegradedTwoPages", "M1_S001",
            "<<flag 10>>\npage zero", "<<flag 20>>\n<<falcon_update>>\npage one");
        var document = NewDocument(0x0D, 0x81, 1, 0xFF); // textId=0x81 -> bit 0x80 set, index 1 -> M1_S001.
        var runner = new AlundraEventProgramRunner(document, gameState, context) { MapDialogueAsset = mapAsset };
        var entity = new AlundraEntityScriptProxy();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        var kind = CaptureKindForOpcode(runner, 0x0D, () => runner.RunOneScriptCall(entity, state));

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, state.CodeIndex); // still advances - never suspends.

        var flag10 = 10u | 0x8000u;
        var mask10 = 1u << (10 & 0x1f);
        var flag20 = 20u | 0x8000u;
        var mask20 = 1u << (20 & 0x1f);
        Assert.NotEqual(0u, gameState.GetFlag(flag10) & mask10);
        Assert.NotEqual(0u, gameState.GetFlag(flag20) & mask20);
        Assert.Equal(4, gameState.PlayerStats.Falcon); // falcon_update folded FalconTemp (4) into Falcon.
        Assert.Equal(0, gameState.PlayerStats.FalconTemp);
    }

    private static EventTraceKind? CaptureKindForOpcode(AlundraEventProgramRunner runner, int opcode, Action run)
    {
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == opcode)
            {
                kind = record.Kind;
            }
        };

        run();
        runner.TraceSink = null;
        return kind;
    }
}
