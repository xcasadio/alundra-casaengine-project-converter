#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;
using Yarn;

namespace Alundra.Tests;

/// <summary>
/// E16.f T1 (docs/plan-e16-etat-partie.md): establishes, on a REAL <see cref="YarnDialogueRunner"/> /
/// <see cref="global::Yarn.Dialogue"/> against the pinned Yarn Spinner 3.2.1 packages, the three points
/// the E16.f contract needs before <see cref="Alundra.Scripts.AlundraYarnVariableStorage"/> (T2) can be
/// written - never inferred from the SDK's docs or source, since neither ships with this repo:
/// <list type="number">
/// <item><description>what the virtual machine does with a refused read (exception, the <c>Program</c>'s
/// own default value, or something else) and a refused write;</description></item>
/// <item><description>which <c>T</c> it asks <see cref="global::Yarn.IVariableAccess.TryGetValue{T}"/>
/// for, reading a boolean;</description></item>
/// <item><description>whether a plain dialogue (no <c>visited()</c>) ever calls <c>Clear()</c> or reads/
/// writes a <c>$Yarn.Internal.*</c> variable.</description></item>
/// </list>
/// Every test here uses <see cref="RecordingRefusingStorage"/>, which behaves exactly like the refusal
/// branch T2's real storage will take (D-E16-18: <see cref="global::Yarn.IVariableAccess.TryGetValue{T}"/>
/// always returns <see langword="false"/>, <c>SetValue</c> is recorded but changes no state, no exception
/// is ever thrown) - so these results describe the CONTRACT T2 must satisfy, not one particular
/// implementation of it.
/// </summary>
public sealed class AlundraYarnVariableStorageProbeTests
{
    private static DialogueAsset CompileAsset(string name, string source)
    {
        var compiler = new YarnDialogueCompiler();
        YarnDialogueCompilationResult result = compiler.CompileString(source, name + ".yarn", functionDeclarations: null);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram(name, "Start", result.ProgramBytes, result.LineTexts);
    }

    // -----------------------------------------------------------------------------------------
    // Point 1 - a refused read/write, no exception.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RefusedRead_IsFalse_NoExceptionAndDialogueRunsToTheEnd()
    {
        var storage = new RecordingRefusingStorage();
        var (runner, presenter) = NewRunner(storage);
        DialogueAsset asset = CompileAsset("RefusedRead", """
            title: Start
            ---
            <<if $mystery>>
            True branch.
            <<else>>
            False branch.
            <<endif>>
            ===
            """);

        var exception = Record.Exception(() => runner.Start(asset));

        Assert.Null(exception);
        Assert.Equal("False branch.", presenter.CurrentLine.Text);
        Assert.Contains("$mystery", storage.ReadNames);
    }

    [Fact]
    public void RefusedWrite_NoExceptionAndDialogueRunsToTheEnd()
    {
        var storage = new RecordingRefusingStorage();
        var (runner, presenter) = NewRunner(storage);
        DialogueAsset asset = CompileAsset("RefusedWrite", """
            title: Start
            ---
            <<set $mystery to true>>
            Done.
            ===
            """);

        var exception = Record.Exception(() => runner.Start(asset));

        Assert.Null(exception);
        Assert.Equal("Done.", presenter.CurrentLine.Text);
        Assert.Contains(storage.Writes, write => write.Name == "$mystery" && Equals(write.Value, true));
    }

    // -----------------------------------------------------------------------------------------
    // Point 2 - the T requested to read a boolean.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void BooleanRead_RequestsIConvertible_NotBool()
    {
        var storage = new RecordingRefusingStorage();
        var (runner, _) = NewRunner(storage);
        DialogueAsset asset = CompileAsset("BooleanReadType", """
            title: Start
            ---
            <<if $mystery>>
            True branch.
            <<else>>
            False branch.
            <<endif>>
            ===
            """);

        runner.Start(asset);

        Assert.Contains(typeof(IConvertible), storage.RequestedTypes);
        Assert.DoesNotContain(typeof(bool), storage.RequestedTypes);
    }

    // -----------------------------------------------------------------------------------------
    // Point 3 - no Clear() and no $Yarn.Internal.* traffic on a plain dialogue.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PlainDialogueWithoutVisited_NeverCallsClearOrTouchesInternalVariables()
    {
        var storage = new RecordingRefusingStorage();
        var (runner, presenter) = NewRunner(storage);
        DialogueAsset asset = CompileAsset("NoInternalVariables", """
            title: Start
            ---
            <<set $mystery to true>>
            <<if $mystery>>
            True branch.
            <<else>>
            False branch.
            <<endif>>
            ===
            """);

        var exception = Record.Exception(() => runner.Start(asset));

        Assert.Null(exception);
        // The storage refuses everything, so the read right after the write still reports false
        // (point 1) - only the branch taken, never Clear()/$Yarn.Internal.* traffic, is this test's point.
        Assert.Equal("False branch.", presenter.CurrentLine.Text);
        Assert.Equal(0, storage.ClearCount);
        Assert.DoesNotContain(storage.ReadNames, n => n.StartsWith("$Yarn.Internal.", StringComparison.Ordinal));
        Assert.DoesNotContain(storage.Writes, w => w.Name.StartsWith("$Yarn.Internal.", StringComparison.Ordinal));
    }

    // -----------------------------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------------------------

    private static (YarnDialogueRunner Runner, FakeDialoguePresenter Presenter) NewRunner(IVariableStorage storage)
    {
        var presenter = new FakeDialoguePresenter();
        var runner = new YarnDialogueRunner(presenter) { VariableStorage = storage };
        return (runner, presenter);
    }

    /// <summary>Refuses every name (D-E16-18's refusal branch): a read always misses, a write is recorded
    /// but changes no state, <c>Clear()</c> is counted, and no member ever throws - the shape T2's real
    /// storage must have for anything outside the flag-name grammar.</summary>
    private sealed class RecordingRefusingStorage : IVariableStorage
    {
        public List<string> ReadNames { get; } = new();
        public List<Type> RequestedTypes { get; } = new();
        public List<(string Name, object? Value)> Writes { get; } = new();
        public int ClearCount { get; private set; }

        public ISmartVariableEvaluator? SmartVariableEvaluator { get; set; }

        public Program? Program { get; set; }

        public bool TryGetValue<T>(string name, out T? result)
        {
            ReadNames.Add(name);
            RequestedTypes.Add(typeof(T));
            result = default!;
            return false;
        }

        public VariableKind GetVariableKind(string name) => VariableKind.Unknown;

        public void SetValue(string name, string value) => Writes.Add((name, value));

        public void SetValue(string name, float value) => Writes.Add((name, value));

        public void SetValue(string name, bool value) => Writes.Add((name, value));

        public void Clear() => ClearCount++;
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

        public bool ShowChoices(IReadOnlyList<string> labels) => throw new NotSupportedException("Not used by AlundraYarnVariableStorageProbeTests.");

        public bool SelectChoice(int index) => throw new NotSupportedException("Not used by AlundraYarnVariableStorageProbeTests.");

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
}
