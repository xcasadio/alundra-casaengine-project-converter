#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.f0 (docs/plan-e19-opcodes.md, F0-3; D-E19-48, ADR-0025): the DLL reads the positioned
/// <c>[flag id=N/]</c> markers of a Yarn line. Since E19.f2a (F2-R3, the end of D-E12-4) a flag is set when the box's own step
/// reaches it - at the glyph where the marker sits, on the pass the director's box takes that step - and no longer when the page is shown; the
/// capture presenter sets none; <c>[yield/]</c> (a step in which nothing is drawn) and any unknown marker set no flag; the old
/// <c>&lt;&lt;flag n&gt;&gt;</c> command is still served at the display of the page; the degraded (no-director) path reads the markers too and
/// stays instantaneous.
/// </summary>
public sealed class AlundraDialogueFlagMarkerTests : IDisposable
{
    private static readonly AlundraDialogueDirector Director = AlundraDialogueDirector.Instance;

    public AlundraDialogueFlagMarkerTests()
    {
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
    }

    public void Dispose()
    {
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
    }

    private static string Flag(int id) => $"[flag id={id} trimwhitespace=false/]";

    private const string Yield = "[yield trimwhitespace=false/]";

    private static bool IsSet(AlundraGameState gameState, int n)
        => (gameState.GetFlag((uint)n | 0x8000u) & (1u << (n & 0x1f))) != 0;

    private static void AttachRealDialogueContext(AlundraGameState gameState)
    {
        Director.AttachToWorld(new DialogueService(), gameState);
        Director.InstallForMapEntry();
    }

    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

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

    /// <summary>World presenter that snapshots, at the instant it is handed a line, what the game state
    /// already holds - proving the flags were set BEFORE the line was transmitted.</summary>
    private sealed class SnapshotPresenter : IDialoguePresenter
    {
        private readonly Func<string> _snapshot;

        public SnapshotPresenter(Func<string> snapshot) => _snapshot = snapshot;

        public List<string> SnapshotsAtShowLine { get; } = new();
        public List<string> Texts { get; } = new();

        public DialogueRuntimeState State => DialogueRuntimeState.Closed;
        public DialogueLine CurrentLine => DialogueLine.Empty;
        public bool IsOpen => false;
        public IReadOnlyList<string> Choices => Array.Empty<string>();
        public bool HasChoices => false;

#pragma warning disable CS0067
        public event EventHandler<DialoguePresentationChangedEventArgs>? PresentationChanged;
        public event EventHandler<DialogueChoiceSelectedEventArgs>? ChoiceSelected;
#pragma warning restore CS0067

        public bool ShowLine(DialogueLine line)
        {
            SnapshotsAtShowLine.Add(_snapshot());
            Texts.Add(line.Text);
            return true;
        }

        public bool ShowChoices(IReadOnlyList<string> labels) => true;
        public bool SelectChoice(int index) => false;
        public bool Close() => true;
    }

    // ---- the director path -------------------------------------------------------------------------

    [Fact]
    public void MidTextFlagMarker_IsSetWhenItsPageIsShown_AndNeverEarlier()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.BuildRaw(
            "FlagMarkersAcrossPages", "Start",
            "Salut" + Flag(10) + Yield + " ami", "page one", Flag(20) + "page two");

        Director.Open(asset, "Start", controlMode: 1);
        var passes = new DialogueBoxPassDriver(Director, gameState);

        // Page 0: the flag sits mid-text and is set by the step of its \Y, the pass 39 (E19.f2a, the end of D-E12-4); the later one is not set yet. The
        // director holds the whole page.
        Assert.False(IsSet(gameState, 10));
        Assert.False(IsSet(gameState, 20));
        Assert.Equal("Salut ami", Director.CurrentLineForTests?.Text);
        passes.RunTo(38);
        Assert.False(IsSet(gameState, 10));
        passes.RunTo(39);
        Assert.True(IsSet(gameState, 10));

        // The cursor of the page boundary shows at the pass 59, and a press the box sees at the pass 60 turns the page.
        passes.PressSeenAt(60);
        passes.RunTo(59);
        Assert.True(Director.Box.IsCursorShown);
        passes.RunTo(60);
        Assert.Equal(1, Director.PageIndexForTests);
        Assert.False(IsSet(gameState, 20));
        Assert.Equal("page one", Director.CurrentLineForTests?.Text);

        // The second cursor shows at the pass 96 (the delay of the step is not reduced by the pass of the release); a press seen at 97 turns to page two,
        // whose flag is set by its first step, the pass 101.
        passes.PressSeenAt(97);
        passes.RunTo(96);
        Assert.True(Director.Box.IsCursorShown);
        passes.RunTo(97);
        Assert.Equal(2, Director.PageIndexForTests);
        Assert.Equal("page two", Director.CurrentLineForTests?.Text);
        passes.RunTo(100);
        Assert.False(IsSet(gameState, 20));
        passes.RunTo(101);
        Assert.True(IsSet(gameState, 20));
    }

    [Fact]
    public void SeveralFlagMarkersOfOnePage_AreAllSet_IncludingTheEndOfTextOne()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        // M389_S106 p0 shape: "...?[flag 1004][yield][flag 999][yield]" - the end-of-text classes.
        var asset = DialogueTestAssets.BuildRaw(
            "EndFlags", "Start", "Fin ?" + Flag(1004) + Yield + Flag(999) + Yield);

        Director.Open(asset, "Start", controlMode: 1);
        var passes = new DialogueBoxPassDriver(Director, gameState);

        // Each \Y is a step of its own: 1004 at the pass 39, 999 at 43, the end of the text at 47.
        Assert.False(IsSet(gameState, 1004));
        passes.RunTo(38);
        Assert.False(IsSet(gameState, 1004));
        passes.RunTo(39);
        Assert.True(IsSet(gameState, 1004));
        Assert.False(IsSet(gameState, 999));
        passes.RunTo(42);
        Assert.False(IsSet(gameState, 999));
        passes.RunTo(43);
        Assert.True(IsSet(gameState, 999));
        Assert.False(Director.Box.IsTypingDone);
        passes.RunTo(47);
        Assert.True(Director.Box.IsTypingDone);
        Assert.Equal("Fin ?", Director.CurrentLineForTests?.Text);
    }

    [Fact]
    public void FlagsOnlyPage_SetsItsFlagsAndShowsAnEmptyBox()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        // M45_S097 p1: [flag 999][yield][empty].
        var asset = DialogueTestAssets.BuildRaw(
            "FlagsOnly", "Start", Flag(999) + Yield + "[empty trimwhitespace=false/]");

        Director.Open(asset, "Start", controlMode: 1);
        var passes = new DialogueBoxPassDriver(Director, gameState);

        // The flag is set by the first step (the pass 19), the \Y takes it, the end of the text comes at 23.
        passes.RunTo(18);
        Assert.False(IsSet(gameState, 999));
        passes.RunTo(19);
        Assert.True(IsSet(gameState, 999));
        passes.RunTo(23);
        Assert.True(Director.Box.IsTypingDone);
        Assert.Equal(string.Empty, Director.CurrentLineForTests?.Text);
    }

    [Fact]
    public void YieldAndUnknownMarkers_AreIgnored_AndSetNoFlag()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.BuildRaw(
            "Ignored", "Start", "a" + Yield + "b[mystery id=5 trimwhitespace=false/]c");

        var exception = Record.Exception(() => Director.Open(asset, "Start", controlMode: 1));

        Assert.Null(exception);
        Assert.Equal("abc", Director.CurrentLineForTests?.Text);
        for (var n = 0; n < 8; n++)
        {
            Assert.False(IsSet(gameState, n));
        }
    }

    [Fact]
    public void TheOldFlagCommand_IsStillServed()
    {
        var gameState = new AlundraGameState();
        AttachRealDialogueContext(gameState);

        var asset = DialogueTestAssets.BuildRaw("OldCommand", "Start", "<<flag 30>>\npage zero");

        Director.Open(asset, "Start", controlMode: 1);

        Assert.True(IsSet(gameState, 30));
    }

    // ---- ordering relative to the line and to falcon_update ---------------------------------------

    [Fact]
    public void TheCapturePresenter_SetsNoFlag_FalconUpdateStillRunsBeforeTheLine()
    {
        // M134_S019 shape: falcon_update first, then the line ending "...{falcon()}.[flag 100][yield]".
        var gameState = new AlundraGameState();
        gameState.PlayerStats.FalconTemp = 4;
        var world = new SnapshotPresenter(() => $"flag100={IsSet(gameState, 100)};falcon={gameState.PlayerStats.Falcon}");
        var capture = new AlundraDialogueCapturePresenter(world);
        var runner = new YarnDialogueRunner(capture);
        new AlundraYarnBindings(gameState).Register(runner);

        var asset = DialogueTestAssets.BuildRaw(
            "Order", "Start", "<<falcon_update>>\nRamène-moi {falcon()} faucons." + Flag(100) + Yield);

        runner.Start(asset, "Start");

        // E19.f2a F2-R6: the flag of the page is the director's, set at its glyph - the capture presenter sets none; falcon_update, a command, still ran first.
        Assert.Equal(new[] { "flag100=False;falcon=4" }, world.SnapshotsAtShowLine);
        Assert.Single(world.Texts);
    }

    [Fact]
    public void ACapturePresenterBuiltWithoutAGameState_SetsNoFlagAndDoesNotThrow()
    {
        var world = new SnapshotPresenter(() => string.Empty);
        var capture = new AlundraDialogueCapturePresenter(world);
        var line = new DialogueLine(
            "x",
            string.Empty,
            new List<DialogueMarkupAttribute>
            {
                new("flag", 1, 0, new Dictionary<string, object> { ["id"] = 5 }),
            });

        var exception = Record.Exception(() => capture.ShowLine(line));

        Assert.Null(exception);
        Assert.Single(world.Texts);
    }

    // ---- the degraded (no-director) path ------------------------------------------------------------

    [Fact]
    public void DegradedPath_NoDirector_ReadsTheFlagMarkersOfEveryPage()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.FalconTemp = 4;
        var context = new FakeEntityWorldContext { DialogueDirector = null };

        var mapAsset = DialogueTestAssets.BuildRaw(
            "DegradedMarkers", "M1_S001",
            "page zero" + Flag(10) + Yield, "<<falcon_update>>\n" + Flag(20) + "page one" + Flag(21) + Yield);
        var document = NewDocument(0x0D, 0x81, 1, 0xFF); // textId=0x81 -> bit 0x80 set, index 1 -> M1_S001.
        var runner = new AlundraEventProgramRunner(document, gameState, context) { MapDialogueAsset = mapAsset };
        var entity = new AlundraEntityScriptProxy();
        var state = new EventProgramState { Codes = document.CodesAsBytes() };

        runner.RunOneScriptCall(entity, state);

        Assert.True(IsSet(gameState, 10));
        Assert.True(IsSet(gameState, 20));
        Assert.True(IsSet(gameState, 21));
        Assert.Equal(4, gameState.PlayerStats.Falcon); // falcon_update still ran.
    }
}
