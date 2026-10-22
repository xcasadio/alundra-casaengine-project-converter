#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Events;
using Alundra.Dialogues;
using Alundra.Dialogues.Yarn;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Dialogues;

/// <summary>
/// E19.b T1 (docs/plan-e19-opcodes.md §1.2d, D-E19-5): <c>0xC4</c> (dialogue with a speaker search, 6 bytes) and the
/// first direct test of <c>0x5C</c> (the same box with the name, 4 bytes), on synthetic bytecode and the real
/// session-scoped <see cref="DialogueDirector"/> (the pattern of <see cref="DialogueOpcodeDispatchTests"/>),
/// then the corpus of the real export: the 31 sites of 11 maps, each opening a node that exists.
/// </summary>
public sealed class DialogueSpeakerOpcodeTests : IDisposable
{
    public DialogueSpeakerOpcodeTests()
    {
        DialogueDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        DialogueDirector.Instance.ResetForTests();
    }

    /// <summary>The text id of the box under test (bit 0x80: the map's node <c>M1_S002</c>, "TARGET").</summary>
    private const int TargetTextId = 0x82;

    /// <summary>One theory row per opcode: the instruction size and a builder of its bytes, with decoy operands
    /// that name OTHER existing nodes, so an operand read one byte off opens the wrong text.</summary>
    public static IEnumerable<object[]> SpeakerOpcodes()
    {
        yield return new object[] { 0xC4, 6 };
        yield return new object[] { 0x5C, 4 };
    }

    /// <summary>0xC4: v1 search (decoy), v2 and v3 name (decoys), v4 text, v5 mode. 0x5C: v1 search (decoy), v2 text,
    /// v3 mode. Both followed by 0xFF.</summary>
    private static int[] Codes(int opcode, int mode) => opcode == 0xC4
        ? new[] { 0xC4, 0x84, 0x83, 0x81, TargetTextId, mode, 0xFF }
        : new[] { 0x5C, 0x84, TargetTextId, mode, 0xFF };

    private static EventProgramDocument NewDocument(int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    /// <summary>A map-1 asset with the nodes <c>M1_S001</c> (decoy), <c>M1_S002</c> (target), <c>M1_S003</c> and
    /// <c>M1_S004</c> (decoys); the target also sets the temporary flag 999 like the nodes of map 476 do.</summary>
    private static DialogueAsset MapAsset()
    {
        var compiler = new YarnDialogueCompiler();
        var source =
            "title: M1_S001\n---\nDECOY-V3 #line:M1_S001_p0\n===\n"
            + "title: M1_S002\n---\n<<flag 999>>\nTARGET #line:M1_S002_p0\n===\n"
            + "title: M1_S003\n---\nDECOY-V2 #line:M1_S003_p0\n===\n"
            + "title: M1_S004\n---\nDECOY-V1 #line:M1_S004_p0\n===\n";
        var result = compiler.CompileString(source, "MapAsset.yarn", YarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram("MapAsset", "M1_S001", result.ProgramBytes, result.LineTexts);
    }

    private sealed class FakeContext : IEntityWorldContext
    {
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities { get; } = Array.Empty<EntityScriptProxy>();
        public EntityScriptProxy? PlayerEntity => null;
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public IDialogueDirector? DialogueDirector { get; set; }
    }

    private static (EventProgramRunner Runner, GameState GameState, FakeContext Context) NewRealMontage(int[] codes)
    {
        var gameState = new GameState();
        DialogueDirector.Instance.AttachToWorld(new DialogueService(), gameState);
        DialogueDirector.Instance.InstallForMapEntry();
        var context = new FakeContext { DialogueDirector = DialogueDirector.Instance };
        var runner = new EventProgramRunner(NewDocument(codes), gameState, context) { MapDialogueAsset = MapAsset() };
        return (runner, gameState, context);
    }

    private static EventProgramState NewState(int[] codes, int result = 7)
        => new() { Codes = NewDocument(codes).CodesAsBytes(), Result = result };

    [Theory]
    [MemberData(nameof(SpeakerOpcodes))]
    public void OpensTheTextOfItsOwnOperand_ReturnsItsSize_AndLeavesResultAlone(int opcode, int size)
    {
        var codes = Codes(opcode, mode: 1);
        var (runner, _, _) = NewRealMontage(codes);
        var state = NewState(codes);
        EventTraceKind? kind = null;
        runner.TraceSink = record => kind ??= record.Opcode == opcode ? record.Kind : null;

        runner.RunOneScriptCall(new EntityScriptProxy(), state);

        Assert.True(DialogueDirector.Instance.IsOpen);
        Assert.Equal("TARGET", DialogueDirector.Instance.CurrentLineForTests?.Text);
        Assert.Equal(size, state.CodeIndex); // advanced by the instruction's size, then 0xFF ended the call.
        Assert.Equal(7, state.Result);
        Assert.Equal(EventTraceKind.Implemented, kind);
    }

    [Theory]
    [MemberData(nameof(SpeakerOpcodes))]
    public void WhileABoxIsOpen_RetriesWithoutAdvancing_AndWithoutOpeningASecondBox(int opcode, int size)
    {
        var codes = Codes(opcode, mode: 1);
        var (runner, _, _) = NewRealMontage(codes);
        DialogueDirector.Instance.Open(DialogueTestAssets.SinglePage("First", "FIRST"), "Start", controlMode: 1);
        var serial = DialogueDirector.Instance.OpenSerial;
        var state = NewState(codes);

        runner.RunOneScriptCall(new EntityScriptProxy(), state);

        Assert.Equal(0, state.CodeIndex); // returned 0: suspended AT the instruction, size never applied.
        Assert.NotEqual(size, state.CodeIndex);
        Assert.Equal(serial, DialogueDirector.Instance.OpenSerial);
        Assert.Equal("FIRST", DialogueDirector.Instance.CurrentLineForTests?.Text);
        Assert.Equal(7, state.Result);
    }

    [Theory]
    [InlineData(0xC4, 1, true)]
    [InlineData(0xC4, 0, false)]
    [InlineData(0x5C, 1, true)]
    [InlineData(0x5C, 0, false)]
    public void ControlMode1_IsAMessageBox_ControlMode0_IsAMenu(int opcode, int mode, bool messageBox)
    {
        var codes = Codes(opcode, mode);
        var (runner, gameState, _) = NewRealMontage(codes);

        runner.RunOneScriptCall(new EntityScriptProxy(), NewState(codes));

        var messageBoxBit = gameState.PlayerControlFlags & GameState.PlayerControlBits.MessageBox;
        var menuBit = gameState.PlayerControlFlags & GameState.PlayerControlBits.MenuOpen;
        Assert.Equal(messageBox ? GameState.PlayerControlBits.MessageBox : 0u, messageBoxBit);
        Assert.Equal(messageBox ? 0u : GameState.PlayerControlBits.MenuOpen, menuBit);
    }

    [Theory]
    [MemberData(nameof(SpeakerOpcodes))]
    public void WithoutAPresenter_DegradesToAnAdvance_StillSettingTheNodeFlags_AndLeavesResultAlone(int opcode, int size)
    {
        var codes = Codes(opcode, mode: 1);
        var gameState = new GameState();
        var runner = new EventProgramRunner(NewDocument(codes), gameState, new FakeContext { DialogueDirector = null })
        {
            MapDialogueAsset = MapAsset(),
        };
        var state = NewState(codes);
        EventTraceKind? kind = null;
        runner.TraceSink = record => kind ??= record.Opcode == opcode ? record.Kind : null;

        runner.RunOneScriptCall(new EntityScriptProxy(), state);

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(size, state.CodeIndex);
        Assert.Equal(7, state.Result);
        // The node ran headless to its end: its <<flag 999>> is set, or a later 0x36 on it would wait forever.
        Assert.NotEqual(0u, gameState.GetFlag(999u | 0x8000u) & (1u << (999 & 0x1f)));
    }

    // ---- the corpus ------------------------------------------------------------------------------

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
            $"DialogueSpeakerOpcodeTests: no 'alundra-project/Maps' directory found above '{AppContext.BaseDirectory}' - "
            + "this test needs the real converter export of the whole corpus and cannot self-skip without it "
            + "(docs/plan-e19-opcodes.md, slice E19.b).");
    }

    /// <summary>Every 0xC4 of the corpus by a linear decode of each map's code area (after its six tables,
    /// <c>2 * sum(table lengths)</c> bytes), with the sizes of <see cref="EventOpcodeSizeTable"/>; the decode
    /// stops at an unknown size. Linear decoding and entry reachability agree on this opcode (the E19.b audit).</summary>
    [Fact]
    public void Corpus_The31Sites_In11Maps_EachOpensAnExistingNode()
    {
        var root = FindProjectRoot();
        var index = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "Maps", "world-index.json")))!;
        var perMap = new SortedDictionary<int, int>();
        var failures = new List<string>();

        foreach (var (_, worldRelativePath) in index)
        {
            var worldName = Path.GetFileNameWithoutExtension(worldRelativePath);
            var document = MapEventProgramLoader.Load(root, worldName)
                ?? throw new Xunit.Sdk.XunitException($"the events document of '{worldName}' did not load");
            var codes = document.CodesAsBytes();
            var tables = new[]
            {
                document.EventCodesATable, document.EventCodesBTable, document.EventCodesCTable,
                document.EventCodesDTable, document.EventCodesETable, document.EventCodesFTable,
            };
            var position = 2 * tables.Sum(table => table.Length);

            DialogueAsset? mapAsset = null;
            while (position < codes.Length)
            {
                var opcode = codes[position];
                if (!EventOpcodeSizeTable.Entries.TryGetValue(opcode, out var entry) || entry.Size <= 0)
                {
                    break;
                }

                if (opcode == 0xC4 && position + 5 < codes.Length)
                {
                    var textId = codes[position + 4];
                    var mode = codes[position + 5];
                    var mapFolder = Path.GetDirectoryName(Path.Combine(root, worldRelativePath))!;
                    mapAsset ??= DialogueTestAssets.LoadFromDisk(Path.Combine(mapFolder, "dialogues", worldName + ".dialogue"));
                    var mapIndex = document.MapIndex;
                    var node = $"M{mapIndex}_S{textId & 0x7f:000}";
                    perMap[mapIndex] = perMap.GetValueOrDefault(mapIndex) + 1;

                    if ((textId & 0x80) == 0 || mode != 1 || !mapAsset.TryGetLineText($"line:{node}_p0", out _))
                    {
                        failures.Add($"map {mapIndex} @{position}: textId 0x{textId:X2}, mode {mode}, node {node}");
                    }
                }

                position += entry.Size;
            }
        }

        Assert.True(failures.Count == 0, "0xC4 sites that do not open an existing map node in mode 1: " + string.Join("; ", failures));
        Assert.Equal(
            new Dictionary<int, int> { [25] = 6, [76] = 3, [84] = 1, [95] = 2, [161] = 3, [226] = 1, [274] = 3, [347] = 3, [411] = 1, [432] = 1, [476] = 7 },
            perMap.ToDictionary(pair => pair.Key, pair => pair.Value));
        Assert.Equal(31, perMap.Values.Sum());
    }
}
