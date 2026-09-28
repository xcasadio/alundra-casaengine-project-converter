#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c T4 (docs/plan-e15-yarn.md): covers <see cref="AlundraYarnBindings"/> - the two commands and
/// seven functions of the E15.b function contract registered on a real <see cref="YarnDialogueRunner"/>,
/// against real Yarn scripts compiled by <see cref="YarnDialogueCompiler"/> (same pattern as
/// <c>CasaEngine.Tests.Dialogue.YarnDialogueRunnerTests</c>). Item names come through the DLL's own
/// <see cref="AlundraEtcStringTable"/> fixture convention (same JSON shape as
/// <see cref="AlundraInventoryDirectorTests"/>'s own <c>WriteEtcFixture</c>) - never a raw-file reader
/// of this test's own.
/// </summary>
public sealed class AlundraYarnBindingsTests : IDisposable
{
    public AlundraYarnBindingsTests() => AlundraEtcStringTable.ResetForTests();

    public void Dispose() => AlundraEtcStringTable.ResetForTests();

    // -----------------------------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------------------------

    /// <summary>Writes a synthetic <c>Dialogues/etc-index.json</c>/<c>global-strings.json</c> pair
    /// resolving the reward item names this test needs (same shape as
    /// <see cref="AlundraInventoryDirectorTests"/>'s <c>WriteEtcFixture</c>) - never a raw-file reader
    /// of its own, only the JSON <see cref="AlundraEtcStringTable"/> already reads.</summary>
    private static string WriteItemNameFixture(params (int ItemId, string Name)[] items)
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "AlundraYarnBindingsFixture_" + Guid.NewGuid());
        var dialoguesPath = Path.Combine(projectPath, "Dialogues");
        Directory.CreateDirectory(dialoguesPath);

        var etcIndex = new int[1024];
        Array.Fill(etcIndex, -1);
        var globalStrings = new Dictionary<string, string>();
        var nextKey = 100;
        foreach (var (itemId, name) in items)
        {
            etcIndex[itemId + 0x200] = nextKey;
            globalStrings[nextKey.ToString()] = name;
            nextKey++;
        }

        File.WriteAllText(Path.Combine(dialoguesPath, "etc-index.json"), "[" + string.Join(",", etcIndex) + "]");
        File.WriteAllText(
            Path.Combine(dialoguesPath, "global-strings.json"),
            "{" + string.Join(",", globalStrings.Select(pair => $"\"{pair.Key}\":\"{pair.Value}\"")) + "}");

        return projectPath;
    }

    private static DialogueAsset CompileAsset(string name, string source, global::Yarn.Library? functionDeclarations = null)
    {
        var compiler = new YarnDialogueCompiler();
        YarnDialogueCompilationResult result = compiler.CompileString(source, name + ".yarn", functionDeclarations ?? AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram(name, "Start", result.ProgramBytes, result.LineTexts);
    }

    private static (YarnDialogueRunner Runner, FakeDialoguePresenter Presenter) NewRunner(AlundraYarnBindings bindings)
    {
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter);
        bindings.Register(runner);
        return (runner, presenter);
    }

    // -----------------------------------------------------------------------------------------
    // "flag" command - AlundraDialogueDirector.ShowCurrentPage's own write (~:290-300).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(999)]
    [InlineData(100)]
    [InlineData(401)]
    public void FlagCommand_SetsTheRightBitInTemporaryFlags(int n)
    {
        var gameState = new AlundraGameState();
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, _) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("FlagCommand", $$"""
            title: Start
            ---
            <<flag {{n}}>>
            Done.
            ===
            """);

        runner.Start(asset);

        var flagId = (uint)(n | 0x8000);
        var mask = 1u << (n & 0x1f);
        Assert.Equal(mask, gameState.GetFlag(flagId) & mask);
    }

    // -----------------------------------------------------------------------------------------
    // "falcon_update" command - ADR-0007: keeps state, then the two ported updates.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void FalconUpdateCommand_SumsFalconsAndMovesCategory()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 5;
        gameState.PlayerStats.FalconTemp = 3;
        gameState.GameFlags[0x2c] = 0x2000000; // selects category 1 (§5.7)
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, _) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("FalconUpdate", """
            title: Start
            ---
            <<falcon_update>>
            Done.
            ===
            """);

        runner.Start(asset);

        Assert.Equal(8, gameState.PlayerStats.Falcon);
        Assert.Equal(0, gameState.PlayerStats.FalconTemp);
        Assert.Equal(1, gameState.TextCategoryIndex);
    }

    // -----------------------------------------------------------------------------------------
    // The seven functions - each read on its own, isolated small script.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void FalconTemp_ReadsTheKeptValue_NotTheResetOne()
    {
        // Falcon starts non-zero (2) so falcon_temp() (kept FalconTemp, 7) and falcon() (updated Falcon,
        // 2 + 7 = 9) differ - a mixup between the two would go unnoticed if they happened to match.
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 2;
        gameState.PlayerStats.FalconTemp = 7;
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("FalconTemp", """
            title: Start
            ---
            <<falcon_update>>
            {falcon_temp()}
            ===
            """);

        runner.Start(asset);

        // falcon_update resets FalconTemp to 0 - falcon_temp() must still report the pre-update 7.
        Assert.Equal("7", presenter.CurrentLine.Text);
    }

    [Fact]
    public void FalconTemp_StateChangedAfterBindingsBuilt_ReadsTheChangedValue()
    {
        // ADR-0007/T4 fix: the kept values start at 0 in the constructor, never copied from gameState at
        // construction time - so a change made to the game state AFTER the bindings are built, but
        // before falcon_update ever runs, must still be the value falcon_update's own snapshot picks up.
        var gameState = new AlundraGameState();
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        gameState.PlayerStats.Falcon = 2;
        gameState.PlayerStats.FalconTemp = 7;
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("FalconTempAfterBuild", """
            title: Start
            ---
            <<falcon_update>>
            {falcon_temp()}
            ===
            """);

        runner.Start(asset);

        Assert.Equal("7", presenter.CurrentLine.Text);
    }

    [Fact]
    public void CategoryItemNameBefore_TwoFalconUpdatesOnSameBindings_EachKeepsThePriorCategory()
    {
        // ADR-0007/T4 fix: category_item_name_before() after a SECOND falcon_update on the same
        // bindings/runner must report the category the FIRST falcon_update left behind (1, item 0x33),
        // not category 0's item (0x53) - which is what a constructor-time-only snapshot (or one taken
        // once and never refreshed by the falcon_update handler) would still report.
        var projectPath = WriteItemNameFixture((0x53, "Vaisseau de vie"), (0x33, "Anneau d'Olga"), (0x35, "Bouclier"));
        try
        {
            var gameState = new AlundraGameState(); // TextCategoryIndex starts at 0 -> reward item 0x53
            gameState.GameFlags[0x2c] = 0x2000000; // first falcon_update moves to category 1 (item 0x33)
            var bindings = new AlundraYarnBindings(gameState, projectPath);
            var (runner, presenter) = NewRunner(bindings);
            DialogueAsset asset = CompileAsset("CategoryItemNameBeforeTwice", """
                title: Start
                ---
                <<falcon_update>>
                {category_item_name_before()}
                <<falcon_update>>
                {category_item_name_before()}
                ===
                """);

            runner.Start(asset);
            Assert.Equal("Vaisseau de vie", presenter.CurrentLine.Text);

            gameState.GameFlags[0x2c] = 0x8000000; // second falcon_update moves to category 3 (item 0x35)
            runner.Continue();

            Assert.Equal("Anneau d'Olga", presenter.CurrentLine.Text);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void Falcon_ReadsTheUpdatedValue()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 2;
        gameState.PlayerStats.FalconTemp = 3;
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("Falcon", """
            title: Start
            ---
            <<falcon_update>>
            {falcon()}
            ===
            """);

        runner.Start(asset);

        Assert.Equal("5", presenter.CurrentLine.Text);
    }

    [Fact]
    public void CategoryItemNameBefore_ReadsTheKeptCategorysItem()
    {
        var projectPath = WriteItemNameFixture((0x53, "Vaisseau de vie"), (0x33, "Anneau d'Olga"));
        try
        {
            var gameState = new AlundraGameState(); // TextCategoryIndex starts at 0 -> reward item 0x53
            gameState.GameFlags[0x2c] = 0x2000000; // moves to category 1 (reward item 0x33) on update
            var bindings = new AlundraYarnBindings(gameState, projectPath);
            var (runner, presenter) = NewRunner(bindings);
            DialogueAsset asset = CompileAsset("CategoryItemNameBefore", """
                title: Start
                ---
                <<falcon_update>>
                {category_item_name_before()}
                ===
                """);

            runner.Start(asset);

            Assert.Equal("Vaisseau de vie", presenter.CurrentLine.Text);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void CategoryItemName_ReadsTheCurrentCategorysItem()
    {
        var projectPath = WriteItemNameFixture((0x53, "Vaisseau de vie"), (0x33, "Anneau d'Olga"));
        try
        {
            var gameState = new AlundraGameState();
            gameState.GameFlags[0x2c] = 0x2000000; // moves to category 1 (reward item 0x33) on update
            var bindings = new AlundraYarnBindings(gameState, projectPath);
            var (runner, presenter) = NewRunner(bindings);
            DialogueAsset asset = CompileAsset("CategoryItemName", """
                title: Start
                ---
                <<falcon_update>>
                {category_item_name()}
                ===
                """);

            runner.Start(asset);

            Assert.Equal("Anneau d'Olga", presenter.CurrentLine.Text);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void CategoryThreshold_ReadsTheCurrentCategorysThreshold()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[0x2c] = 0x2000000; // category 1 -> threshold 20 (§5.7)
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("CategoryThreshold", """
            title: Start
            ---
            <<falcon_update>>
            {category_threshold()}
            ===
            """);

        runner.Start(asset);

        Assert.Equal("20", presenter.CurrentLine.Text);
    }

    [Fact]
    public void CategoryRemaining_ReadsThresholdMinusFalcon()
    {
        var gameState = new AlundraGameState();
        gameState.PlayerStats.Falcon = 5;
        gameState.GameFlags[0x2c] = 0x2000000; // category 1 -> threshold 20
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("CategoryRemaining", """
            title: Start
            ---
            <<falcon_update>>
            {category_remaining()}
            ===
            """);

        runner.Start(asset);

        Assert.Equal("15", presenter.CurrentLine.Text);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(1, 22)]
    [InlineData(2, 33)]
    public void GameVar_ReadsGameVariables(int index, int value)
    {
        var gameState = new AlundraGameState();
        gameState.GameVariables[0] = 11;
        gameState.GameVariables[1] = 22;
        gameState.GameVariables[2] = 33;
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("GameVar", $$"""
            title: Start
            ---
            {game_var({{index}})}
            ===
            """);

        runner.Start(asset);

        Assert.Equal(value.ToString(), presenter.CurrentLine.Text);
    }

    [Fact]
    public void GameVar_OutOfRange_ReturnsZeroAndLogsOnce()
    {
        var gameState = new AlundraGameState();
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("GameVarOutOfRange", """
            title: Start
            ---
            {game_var(4)}
            Again {game_var(5)}.
            ===
            """);
        using var logger = CapturingWarningLogger.Install();

        runner.Start(asset);
        Assert.Equal("0", presenter.CurrentLine.Text);
        runner.Continue();
        Assert.Equal("Again 0.", presenter.CurrentLine.Text);

        Assert.Single(logger.WarningMessages);
    }

    // -----------------------------------------------------------------------------------------
    // Church (lobby)-134, nodes M134_S016 and M134_S019 - exact lines emitted by the converter
    // (alundra-project/Maps/Church/Church (lobby)-134/dialogues/Church (lobby)-134.yarn).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ChurchM134S016_ShowsTheCategoryItemNameBeforeTheUpdate()
    {
        var projectPath = WriteItemNameFixture((0x53, "Vaisseau de vie"), (0x33, "Anneau d'Olga"));
        try
        {
            var gameState = new AlundraGameState(); // TextCategoryIndex starts at 0 -> reward item 0x53
            gameState.GameFlags[0x2c] = 0x2000000; // the update this page triggers moves to category 1
            var bindings = new AlundraYarnBindings(gameState, projectPath);
            var (runner, presenter) = NewRunner(bindings);
            DialogueAsset asset = CompileAsset("M134_S016", """
                title: Start
                ---
                <<falcon_update>>
                [voice id=0 trimwhitespace=false/]Je pense que tu trouveras ce(t) {category_item_name_before()}[br trimwhitespace=false/]tout à fait utile. Fais-en bon usage,[br trimwhitespace=false/]Alundra ! #line:M134_S016_p0
                ===
                """);

            runner.Start(asset);

            // [br/]/[voice/] markup is stripped here (the DLL's font3 presenter re-inserts line breaks
            // at those positions, out of this test's T4 scope) - only the substituted item name matters.
            Assert.Contains("Vaisseau de vie", presenter.CurrentLine.Text);
            Assert.DoesNotContain("category_item_name_before", presenter.CurrentLine.Text);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void ChurchM134S019_ShowsTheThresholdAndCategoryItemNameAfterTheUpdate()
    {
        var projectPath = WriteItemNameFixture((0x53, "Vaisseau de vie"), (0x33, "Anneau d'Olga"));
        try
        {
            var gameState = new AlundraGameState();
            gameState.GameFlags[0x2c] = 0x2000000; // moves to category 1: threshold 20, item 0x33
            var bindings = new AlundraYarnBindings(gameState, projectPath);
            var (runner, presenter) = NewRunner(bindings);
            DialogueAsset asset = CompileAsset("M134_S019", """
                title: Start
                ---
                <<falcon_update>>
                <<flag 100>>
                [voice id=0 trimwhitespace=false/]Ramène-moi {category_threshold()} Statuettes de faucons[br trimwhitespace=false/]et je te récompenserai avec cela \:[br trimwhitespace=false/]{category_item_name()}. #line:M134_S019_p0
                ===
                """);

            runner.Start(asset);

            // Same markup-stripping caveat as ChurchM134S016 above.
            Assert.Contains("20", presenter.CurrentLine.Text);
            Assert.Contains("Anneau d'Olga", presenter.CurrentLine.Text);
            var mask = 1u << (100 & 0x1f);
            Assert.Equal(mask, gameState.GetFlag(100u | 0x8000) & mask);
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Unknown command - logged once, never an exception.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void UnknownCommand_IsLoggedOnceAndDoesNotThrow()
    {
        var gameState = new AlundraGameState();
        var bindings = new AlundraYarnBindings(gameState, Path.GetTempPath());
        var (runner, presenter) = NewRunner(bindings);
        DialogueAsset asset = CompileAsset("UnknownCommand", """
            title: Start
            ---
            <<mystery>>
            First.
            <<mystery>>
            Second.
            ===
            """, functionDeclarations: null);
        using var logger = CapturingWarningLogger.Install();

        var exception = Record.Exception(() => runner.Start(asset));
        Assert.Null(exception);
        Assert.Equal("First.", presenter.CurrentLine.Text);

        exception = Record.Exception(() => runner.Continue());
        Assert.Null(exception);
        Assert.Equal("Second.", presenter.CurrentLine.Text);

        Assert.Single(logger.WarningMessages);
    }

    // -----------------------------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------------------------

    private sealed class FakeDialoguePresenter : IDialoguePresenter
    {
        public DialogueRuntimeState State { get; private set; } = DialogueRuntimeState.Closed;
        public DialogueLine CurrentLine { get; private set; } = DialogueLine.Empty;
        public bool IsOpen => State == DialogueRuntimeState.Open;

        public IReadOnlyList<string> Choices => Array.Empty<string>();
        public bool HasChoices => false;

        public event EventHandler<DialoguePresentationChangedEventArgs>? PresentationChanged;
#pragma warning disable CS0067 // required by IDialoguePresenter; this fake never presents choices.
        public event EventHandler<DialogueChoiceSelectedEventArgs>? ChoiceSelected;
#pragma warning restore CS0067

        public bool ShowLine(DialogueLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            var previousState = State;
            State = DialogueRuntimeState.Open;
            CurrentLine = line;
            PresentationChanged?.Invoke(this, new DialoguePresentationChangedEventArgs(previousState, State, CurrentLine));
            return true;
        }

        public bool ShowChoices(IReadOnlyList<string> labels) => throw new NotSupportedException("Not used by AlundraYarnBindingsTests.");

        public bool SelectChoice(int index) => throw new NotSupportedException("Not used by AlundraYarnBindingsTests.");

        public bool Close()
        {
            if (!IsOpen)
            {
                return false;
            }

            var previousState = State;
            State = DialogueRuntimeState.Closed;
            CurrentLine = DialogueLine.Empty;
            PresentationChanged?.Invoke(this, new DialoguePresentationChangedEventArgs(previousState, State, CurrentLine));
            return true;
        }
    }

    /// <summary>Captures every <see cref="Logs.WriteWarning"/> call for one test, then unregisters
    /// itself (same private-field reflection precedent as <c>BackdropStageLoadTests.CapturingWarningLogger</c>)
    /// so it does not leak into later tests.</summary>
    private sealed class CapturingWarningLogger : ILogger, IDisposable
    {
        public List<string> WarningMessages { get; } = new();

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }

        public void WriteWarning(string msg) => WarningMessages.Add(msg);

        public void WriteError(string msg)
        {
        }

        public static CapturingWarningLogger Install()
        {
            var logger = new CapturingWarningLogger();
            Logs.AddLogger(logger);
            return logger;
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }
}
