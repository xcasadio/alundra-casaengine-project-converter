#nullable enable
using System;
using System.IO;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Assets.Loaders;
using CasaEngine.Framework.Dialogue.Assets;
using Xunit;
using Yarn;

namespace Alundra.Tests;

/// <summary>
/// E16.f (docs/plan-e16-etat-partie.md, acceptance "corpus", 2026-09-28 plan review, P2): proves that
/// none of the 485 exported <c>.dialogue</c> files under <c>alundra-project/</c> ever consults
/// <see cref="AlundraYarnVariableStorage"/> - it declares no variable's initial value and no node's
/// bytecode ever reads or writes a variable (Yarn's own or a flag one). No existing test plays the whole
/// corpus, and the engine's own runner does not advance choices by itself
/// (<c>YarnDialogueRunner.cs:169-171</c>), so this test inspects each COMPILED <c>Yarn.Program</c>
/// directly rather than running it - <see cref="Instruction.InstructionTypeOneofCase.PushVariable"/> and
/// <see cref="Instruction.InstructionTypeOneofCase.StoreVariable"/> are the only two bytecode instruction
/// kinds Yarn Spinner 3.2.1 has for reading or writing a variable (confirmed by inspecting every member
/// of <c>Instruction.InstructionTypeOneofCase</c>, E16.f T1) - together with
/// <see cref="Program.InitialValues"/> (a declared variable's default), that covers every way a compiled
/// program can touch a variable.
///
/// These used to self-skip silently when <c>alundra-project/</c> was absent - following
/// <see cref="AlundraCellStoreProductionTests"/>'s own fix (docs/plan-e7-mutation-tuiles.md, E7.b), they
/// throw instead, naming the missing export.
/// </summary>
public sealed class AlundraYarnVariableCorpusTests
{
    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraYarnVariableCorpusTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - this test needs the real converter export of the whole "
            + "corpus and cannot self-skip without it (docs/plan-e16-etat-partie.md, slice E16.f).");
    }

    /// <summary>Every exported <c>.dialogue</c> file, from <c>alundra-project/Dialogues</c> and every
    /// map's own <c>dialogues/</c> folder (485 files as of the E16.f plan review).</summary>
    private static string[] FindExportedDialogueFiles(string projectRoot)
    {
        var files = Directory.GetFiles(projectRoot, "*.dialogue", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>Loads <paramref name="dialoguePath"/>'s compiled <see cref="Program"/> the same way the
    /// DLL does at runtime (<see cref="DialogueTestAssets.LoadFromDisk"/>'s own loader); if the asset
    /// carries no compiled program (D-E16-... none observed in the corpus, but the plan asks for the
    /// fallback anyway), the neighbour <c>.yarn</c> source is recompiled with the same declarations the
    /// DLL registers in production (<see cref="AlundraYarnBindings.CreateDeclarations"/>), exactly like
    /// <see cref="DialogueTestAssets.BuildRaw"/> does for a synthetic fixture.</summary>
    private static Program LoadCompiledProgram(string dialoguePath)
    {
        var asset = DialogueTestAssets.LoadFromDisk(dialoguePath);
        if (asset.HasCompiledProgram)
        {
            return Program.Parser.ParseFrom(asset.ProgramBytes);
        }

        var yarnPath = Path.ChangeExtension(dialoguePath, ".yarn");
        Assert.True(File.Exists(yarnPath), $"AlundraYarnVariableCorpusTests: '{dialoguePath}' has no compiled program and no neighbour '{yarnPath}' to recompile.");

        var compiler = new YarnDialogueCompiler();
        var result = compiler.CompileFile(yarnPath, AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, $"AlundraYarnVariableCorpusTests: recompiling '{yarnPath}' failed: " + string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return Program.Parser.ParseFrom(result.ProgramBytes);
    }

    [Fact]
    public void ExportedCorpus_NeverConsultsAVariable()
    {
        var projectRoot = FindProjectRoot();
        var dialogueFiles = FindExportedDialogueFiles(projectRoot);

        foreach (var dialoguePath in dialogueFiles)
        {
            var program = LoadCompiledProgram(dialoguePath);

            Assert.True(program.InitialValues.Count == 0,
                $"'{dialoguePath}' declares {program.InitialValues.Count} variable initial value(s) - the exported corpus was expected to declare none.");

            foreach (var node in program.Nodes.Values)
            {
                foreach (var instruction in node.Instructions)
                {
                    Assert.True(
                        instruction.InstructionTypeCase != Instruction.InstructionTypeOneofCase.PushVariable
                        && instruction.InstructionTypeCase != Instruction.InstructionTypeOneofCase.StoreVariable,
                        $"'{dialoguePath}' node '{node.Name}' has a {instruction.InstructionTypeCase} instruction - the exported corpus was expected to never read or write a variable.");
                }
            }
        }
    }
}
