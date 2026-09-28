#nullable enable
using System;
using System.Collections.Generic;
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
using Yarn;

namespace Alundra.Tests;

/// <summary>
/// E16.f T2 (docs/plan-e16-etat-partie.md, contrat 1 à 10 ; ADR-0010 ; ADR-0011) : acceptation
/// d'<see cref="AlundraYarnVariableStorage"/> sur un vrai <see cref="YarnDialogueRunner"/>/
/// <see cref="global::Yarn.Dialogue"/>, avec des scripts compilés par
/// <see cref="AlundraYarnBindings.CreateDeclarations"/> (même compilateur que la production).
/// </summary>
public sealed class AlundraYarnVariableStorageTests : IDisposable
{
    public AlundraYarnVariableStorageTests() => AlundraDialogueDirector.Instance.ResetForTests();

    public void Dispose() => AlundraDialogueDirector.Instance.ResetForTests();

    private static DialogueAsset CompileAsset(string name, string source)
    {
        var compiler = new YarnDialogueCompiler();
        YarnDialogueCompilationResult result = compiler.CompileString(source, name + ".yarn", AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram(name, "Start", result.ProgramBytes, result.LineTexts);
    }

    private static (YarnDialogueRunner Runner, FakeDialoguePresenter Presenter) NewRunner(AlundraGameState gameState)
    {
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = new AlundraYarnVariableStorage(gameState) };
        return (runner, presenter);
    }

    // -----------------------------------------------------------------------------------------
    // Lecture (contrat item 3).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Read_BitSetByOpcode0x05_IsTrueInIf()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(42, 1u << (42 & 0x1f)); // AlundraEventProgramRunner.cs:449 (0x05)
        var (runner, presenter) = NewRunner(gameState);
        DialogueAsset asset = CompileAsset("ReadFlag", """
            title: Start
            ---
            <<if $flag_42>>
            True branch.
            <<else>>
            False branch.
            <<endif>>
            ===
            """);

        runner.Start(asset);

        Assert.Equal("True branch.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Read_TempFlag_ReadsTemporaryBank()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(42 | 0x8000, 1u << (42 & 0x1f)); // temporary bank only
        var (runner, presenter) = NewRunner(gameState);
        DialogueAsset asset = CompileAsset("ReadTempFlag", """
            title: Start
            ---
            <<if $tmp_flag_42>>
            True branch.
            <<else>>
            False branch.
            <<endif>>
            ===
            """);

        runner.Start(asset);

        Assert.Equal("True branch.", presenter.CurrentLine.Text);
    }

    [Fact]
    public void Read_SameIdInBothBanks_AreDistinctFlags()
    {
        var gameState = new AlundraGameState();
        gameState.AddFlag(42, 1u << (42 & 0x1f)); // GameFlags only, not TemporaryFlags
        var (runner, presenter) = NewRunner(gameState);
        DialogueAsset asset = CompileAsset("DistinctBanks", """
            title: Start
            ---
            <<if $flag_42 and not $tmp_flag_42>>
            Distinct.
            <<else>>
            Same.
            <<endif>>
            ===
            """);

        runner.Start(asset);

        Assert.Equal("Distinct.", presenter.CurrentLine.Text);
    }

    // -----------------------------------------------------------------------------------------
    // Écriture (contrat item 4).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Write_SetTrue_IsSeenByOpcode0x30sOwnBitTest()
    {
        var gameState = new AlundraGameState();
        var (runner, _) = NewRunner(gameState);
        DialogueAsset asset = CompileAsset("WriteFlagTrue", """
            title: Start
            ---
            <<set $flag_100 to true>>
            Done.
            ===
            """);

        runner.Start(asset);

        // Same bit test as FlagBranch (AlundraEventProgramRunner.cs:1416, opcodes 0x30/0x31).
        var mask = 1u << (100 & 0x1f);
        Assert.Equal(mask, gameState.GetFlag(100) & mask);
    }

    [Fact]
    public void Write_SetFalse_ClearsOnlyThatBit()
    {
        var gameState = new AlundraGameState();
        gameState.GameFlags[IndexOf(100)] = 0xFFFFFFFF; // every bit of that word set beforehand
        var (runner, _) = NewRunner(gameState);
        DialogueAsset asset = CompileAsset("WriteFlagFalse", """
            title: Start
            ---
            <<set $flag_100 to false>>
            Done.
            ===
            """);

        runner.Start(asset);

        var mask = 1u << (100 & 0x1f);
        Assert.Equal(0u, gameState.GetFlag(100) & mask);
        Assert.Equal(0xFFFFFFFFu & ~mask, gameState.GetFlag(100)); // every OTHER bit of the word untouched
    }

    // -----------------------------------------------------------------------------------------
    // Bornes n = 0, 31, 32, 2047 - identique aux opcodes 0x05/0x06 (contrat items 3/4, D-E16-25).
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(2047)]
    public void BoundaryIds_SetTrue_MatchOpcode0x05_InBothBanks(int n)
    {
        foreach (var isTemporary in new[] { false, true })
        {
            var name = isTemporary ? $"$tmp_flag_{n}" : $"$flag_{n}";
            var flagId = (uint)n | (isTemporary ? 0x8000u : 0u);

            var viaYarn = new AlundraGameState();
            var (runner, _) = NewRunner(viaYarn);
            runner.Start(CompileAsset($"Boundary{n}{isTemporary}", $$"""
                title: Start
                ---
                <<set {{name}} to true>>
                Done.
                ===
                """));

            var viaOpcode = new AlundraGameState();
            viaOpcode.AddFlag(flagId, 1u << (n & 0x1f)); // opcode 0x05 (AlundraEventProgramRunner.cs:449)

            Assert.Equal(viaOpcode.GameFlags, viaYarn.GameFlags);
            Assert.Equal(viaOpcode.TemporaryFlags, viaYarn.TemporaryFlags);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(2047)]
    public void BoundaryIds_SetFalse_MatchOpcode0x06_InBothBanks(int n)
    {
        foreach (var isTemporary in new[] { false, true })
        {
            var name = isTemporary ? $"$tmp_flag_{n}" : $"$flag_{n}";
            var flagId = (uint)n | (isTemporary ? 0x8000u : 0u);

            var viaYarn = new AlundraGameState();
            (isTemporary ? viaYarn.TemporaryFlags : viaYarn.GameFlags)[IndexOf(n)] = 0xFFFFFFFF;
            var (runner, _) = NewRunner(viaYarn);
            runner.Start(CompileAsset($"BoundaryFalse{n}{isTemporary}", $$"""
                title: Start
                ---
                <<set {{name}} to false>>
                Done.
                ===
                """));

            var viaOpcode = new AlundraGameState();
            (isTemporary ? viaOpcode.TemporaryFlags : viaOpcode.GameFlags)[IndexOf(n)] = 0xFFFFFFFF;
            viaOpcode.SetFlag(flagId, ~(1u << (n & 0x1f))); // opcode 0x06 (AlundraEventProgramRunner.cs:457)

            Assert.Equal(viaOpcode.GameFlags, viaYarn.GameFlags);
            Assert.Equal(viaOpcode.TemporaryFlags, viaYarn.TemporaryFlags);
        }
    }

    private static int IndexOf(int n) => (n >> 5) & 0x3ff; // AlundraGameState.IndexOf, mirrored (private there).

    // -----------------------------------------------------------------------------------------
    // Refus (contrat item 5, D-E16-18/D-E16-25) : une ligne de journal par nom, aucun changement d'état.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("$flag_2048")]
    [InlineData("$tmp_flag_2048")]
    [InlineData("$flag_32768")]
    [InlineData("$flag_07")]
    [InlineData("$foo")]
    [InlineData("$tmp_flag_x")]
    public void RefusedName_SetTrue_LogsOnceAndLeavesBanksUnchanged(string name)
    {
        var gameState = new AlundraGameState();
        SeedBanks(gameState);
        var gameFlagsSnapshot = (uint[])gameState.GameFlags.Clone();
        var temporaryFlagsSnapshot = (uint[])gameState.TemporaryFlags.Clone();
        var (runner, presenter) = NewRunner(gameState);
        using var logger = CapturingWarningLogger.Install();

        var exception = Record.Exception(() => runner.Start(CompileAsset("RefusedSet", $$"""
            title: Start
            ---
            <<set {{name}} to true>>
            Done.
            ===
            """)));

        Assert.Null(exception);
        Assert.Equal("Done.", presenter.CurrentLine.Text);
        Assert.Equal(gameFlagsSnapshot, gameState.GameFlags);
        Assert.Equal(temporaryFlagsSnapshot, gameState.TemporaryFlags);
        Assert.Single(logger.WarningMessages);
    }

    [Fact]
    public void NumberUnderFlagName_IsRefused_LogsOnceAndLeavesBanksUnchanged()
    {
        var gameState = new AlundraGameState();
        SeedBanks(gameState);
        var gameFlagsSnapshot = (uint[])gameState.GameFlags.Clone();
        var temporaryFlagsSnapshot = (uint[])gameState.TemporaryFlags.Clone();
        var (runner, presenter) = NewRunner(gameState);
        using var logger = CapturingWarningLogger.Install();

        var exception = Record.Exception(() => runner.Start(CompileAsset("NumberUnderFlag", """
            title: Start
            ---
            <<set $flag_5 to 7>>
            Done.
            ===
            """)));

        Assert.Null(exception);
        Assert.Equal("Done.", presenter.CurrentLine.Text);
        Assert.Equal(gameFlagsSnapshot, gameState.GameFlags);
        Assert.Equal(temporaryFlagsSnapshot, gameState.TemporaryFlags);
        Assert.Single(logger.WarningMessages);
    }

    [Theory]
    [InlineData("$flag_2048")]
    [InlineData("$foo")]
    public void RefusedName_Read_IsFalseAndLogsOnce(string name)
    {
        var gameState = new AlundraGameState();
        var storage = new AlundraYarnVariableStorage(gameState);
        using var logger = CapturingWarningLogger.Install();

        var ok = storage.TryGetValue<IConvertible>(name, out var value);
        storage.TryGetValue<IConvertible>(name, out _); // second read, same name - still ONE log line

        Assert.False(ok);
        Assert.Single(logger.WarningMessages);
    }

    [Theory]
    [InlineData("$flag_2048")]
    [InlineData("$tmp_flag_2048")]
    [InlineData("$flag_32768")]
    [InlineData("$flag_07")]
    [InlineData("$foo")]
    public void GetVariableKind_RefusedName_IsUnknown(string name)
    {
        var storage = new AlundraYarnVariableStorage(new AlundraGameState());

        Assert.Equal(VariableKind.Unknown, storage.GetVariableKind(name));
    }

    [Theory]
    [InlineData("$flag_0")]
    [InlineData("$tmp_flag_2047")]
    public void GetVariableKind_ValidFlagName_IsStored(string name)
    {
        var storage = new AlundraYarnVariableStorage(new AlundraGameState());

        Assert.Equal(VariableKind.Stored, storage.GetVariableKind(name));
    }

    // -----------------------------------------------------------------------------------------
    // Clear() (contrat item 7).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Clear_LeavesBothBanksUnchanged()
    {
        var gameState = new AlundraGameState();
        SeedBanks(gameState);
        var gameFlagsSnapshot = (uint[])gameState.GameFlags.Clone();
        var temporaryFlagsSnapshot = (uint[])gameState.TemporaryFlags.Clone();
        var storage = new AlundraYarnVariableStorage(gameState);

        storage.Clear();

        Assert.Equal(gameFlagsSnapshot, gameState.GameFlags);
        Assert.Equal(temporaryFlagsSnapshot, gameState.TemporaryFlags);
    }

    // -----------------------------------------------------------------------------------------
    // Directeur (contrat item 9) : le runner qu'il crée utilise ce stockage.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Director_CreatedRunner_UsesThisStorage()
    {
        var gameState = new AlundraGameState();
        AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), gameState);
        AlundraDialogueDirector.Instance.InstallForMapEntry();

        AlundraDialogueDirector.Instance.Open(CompileAsset("DirectorFlag", """
            title: Start
            ---
            <<set $flag_9 to true>>
            Message. #line:Start_p0
            ===
            """), "Start", controlMode: 1);

        var mask = 1u << (9 & 0x1f);
        Assert.Equal(mask, gameState.GetFlag(9) & mask);
    }

    // -----------------------------------------------------------------------------------------
    // Cycle de vie (contrat item 7, D-E16-6) : InstallForMapEntry vide TemporaryFlags, garde GameFlags.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void MapEntry_ClearsTempFlagButKeepsGameFlag()
    {
        var gameState = new AlundraGameState();
        var (runner, _) = NewRunner(gameState);
        runner.Start(CompileAsset("MapEntryFlags", """
            title: Start
            ---
            <<set $flag_5 to true>>
            <<set $tmp_flag_5 to true>>
            Done.
            ===
            """));
        var mask = 1u << (5 & 0x1f);
        Assert.Equal(mask, gameState.GetFlag(5) & mask);
        Assert.Equal(mask, gameState.GetFlag(5 | 0x8000) & mask);

        gameState.InstallForMapEntry();

        Assert.Equal(mask, gameState.GetFlag(5) & mask); // GameFlags kept
        Assert.Equal(0u, gameState.GetFlag(5 | 0x8000) & mask); // TemporaryFlags cleared
    }

    // -----------------------------------------------------------------------------------------
    // Sauvegarde (E16.c livrée, D-E16-31) : $flag_0/$flag_2047 font l'aller-retour, pas les temporaires.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Save_RoundTrip_KeepsGameFlagsDropsTemporaryFlags()
    {
        var gameState = new AlundraGameState();
        var (runner, _) = NewRunner(gameState);
        runner.Start(CompileAsset("SaveRoundTrip", """
            title: Start
            ---
            <<set $flag_0 to true>>
            <<set $flag_2047 to true>>
            <<set $tmp_flag_0 to true>>
            <<set $tmp_flag_2047 to true>>
            Done.
            ===
            """));

        var save = AlundraSaveGame.Capture(gameState, currentMapId: 1, tileX: 0, tileY: 0, tileZ: 0);
        var restored = new AlundraGameState();
        save.ApplyTo(restored);

        var mask0 = 1u << (0 & 0x1f);
        var mask2047 = 1u << (2047 & 0x1f);
        Assert.Equal(mask0, restored.GetFlag(0) & mask0);
        Assert.Equal(mask2047, restored.GetFlag(2047) & mask2047);
        Assert.Equal(0u, restored.GetFlag(0 | 0x8000) & mask0);
        Assert.Equal(0u, restored.GetFlag(2047 | 0x8000) & mask2047);
    }

    // -----------------------------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------------------------

    /// <summary>Seeds a few bits in both banks so a "no state change" assertion is meaningful (an
    /// all-zero snapshot could hide an accidental write that happens to land on an already-zero bit).</summary>
    private static void SeedBanks(AlundraGameState gameState)
    {
        gameState.GameFlags[0] = 0x12345678;
        gameState.GameFlags[5] = 0xABCDEF01;
        gameState.TemporaryFlags[0] = 0x87654321;
        gameState.TemporaryFlags[10] = 0xFEDCBA98;
    }

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

        public bool ShowChoices(IReadOnlyList<string> labels) => throw new NotSupportedException("Not used by AlundraYarnVariableStorageTests.");

        public bool SelectChoice(int index) => throw new NotSupportedException("Not used by AlundraYarnVariableStorageTests.");

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

    /// <summary>Same reflection precedent as <c>AlundraYarnBindingsTests.CapturingWarningLogger</c>.</summary>
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
