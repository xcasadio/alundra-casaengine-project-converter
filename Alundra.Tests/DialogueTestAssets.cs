#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Alundra.Scripts;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Dialogue.Assets;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c T5 (docs/plan-e15-yarn.md): shared helper that compiles a tiny Yarn asset for tests exercising
/// <see cref="AlundraDialogueDirector"/>'s open/close/paging mechanics, which no longer take raw text
/// directly (contract item 2) - same compiler and function declarations the DLL registers in production
/// (<see cref="AlundraYarnBindings.CreateDeclarations"/>, same pattern as
/// <c>AlundraYarnBindingsTests.CompileAsset</c>). Every page is tagged with an explicit
/// <c>#line:{node}_p{k}</c> id, because <see cref="AlundraDialogueDirector"/> counts pages by that exact
/// convention (docs/plan-e15-yarn.md, E15.c contract item 2) - Yarn's own auto-generated line ids would
/// not match it.
/// </summary>
internal static class DialogueTestAssets
{
    /// <summary>A one-page node named <paramref name="node"/> (default <c>"Start"</c>, the asset's own
    /// <see cref="DialogueAsset.StartNode"/>) showing <paramref name="text"/> verbatim.</summary>
    public static DialogueAsset SinglePage(string name, string text, string node = "Start")
        => Build(name, node, text);

    /// <summary>A node named <paramref name="node"/> (default <c>"Start"</c>) with one page per entry of
    /// <paramref name="pageTexts"/>, in order. Each entry's PLAIN TEXT is escaped for Yarn's own special
    /// characters (<c>:</c>, <c>#</c>, brackets...) - use <see cref="BuildRaw"/> when an entry must carry
    /// real Yarn commands/markup syntax of its own (<c>&lt;&lt;command&gt;&gt;</c>, <c>[marker/]</c>).</summary>
    public static DialogueAsset Build(string name, string node = "Start", params string[] pageTexts)
        => BuildRaw(name, node, pageTexts.Select(Escape).ToArray());

    /// <summary>Same as <see cref="Build"/>, but every entry of <paramref name="pageBodies"/> is used
    /// VERBATIM as the page's own Yarn line body (before the <c>#line:</c> tag this method appends) - for
    /// tests that need a real <c>&lt;&lt;command&gt;&gt;</c> before the line or real <c>[marker/]</c>
    /// markup inside it, neither of which <see cref="Build"/>'s escaping would leave intact.</summary>
    public static DialogueAsset BuildRaw(string name, string node, params string[] pageBodies)
    {
        var compiler = new YarnDialogueCompiler();
        var lines = new StringBuilder();
        for (var i = 0; i < pageBodies.Length; i++)
        {
            lines.Append(pageBodies[i]).Append(" #line:").Append(node).Append("_p").Append(i).Append('\n');
        }

        var source = $"title: {node}\n---\n{lines}===\n";
        var result = compiler.CompileString(source, name + ".yarn", AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram(name, node, result.ProgramBytes, result.LineTexts);
    }

    /// <summary>E15.c T6 (docs/plan-e15-yarn.md, contract items 5/6): a <c>dialogue_etc</c>-shaped asset
    /// with one node <c>Etc_{index:0000}</c> per entry of <paramref name="entries"/> - the converter's own
    /// node-naming convention (`docs/formats/dialogues-yarn.md`) - each holding one page of its (escaped)
    /// text. An ETC offset with no entry here has no node at all, matching the converter's own contract
    /// (only non-null ETC entries get a node) - <see cref="AlundraEtcStringTable"/> then resolves it like
    /// an empty entry. No entries at all returns a valid, node-less asset (nothing to compile).</summary>
    public static DialogueAsset BuildEtc(params (int Index, string Text)[] entries)
    {
        if (entries.Length == 0)
        {
            return DialogueAsset.FromCompiledProgram("Etc", "Etc_0000", Array.Empty<byte>(), new Dictionary<string, string>());
        }

        var nodes = entries
            .Select(entry => ($"Etc_{entry.Index:D4}", new[] { Escape(entry.Text) }))
            .ToArray();
        return BuildMultiNode("Etc", nodes);
    }

    /// <summary>Same compilation as <see cref="BuildRaw"/>, but for several titled nodes in one source -
    /// each of <paramref name="nodes"/> is a node name and its own page bodies (used verbatim, same
    /// escaping caveat as <see cref="BuildRaw"/>). The asset's <see cref="DialogueAsset.StartNode"/> is the
    /// first node.</summary>
    public static DialogueAsset BuildMultiNode(string name, params (string Node, string[] PageBodies)[] nodes)
    {
        Assert.NotEmpty(nodes);

        var compiler = new YarnDialogueCompiler();
        var source = new StringBuilder();
        foreach (var (node, pageBodies) in nodes)
        {
            source.Append("title: ").Append(node).Append("\n---\n");
            for (var i = 0; i < pageBodies.Length; i++)
            {
                source.Append(pageBodies[i]).Append(" #line:").Append(node).Append("_p").Append(i).Append('\n');
            }

            source.Append("===\n");
        }

        var result = compiler.CompileString(source.ToString(), name + ".yarn", AlundraYarnBindings.CreateDeclarations());
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        return DialogueAsset.FromCompiledProgram(name, nodes[0].Node, result.ProgramBytes, result.LineTexts);
    }

    /// <summary>Loads an already-compiled <c>.dialogue</c> file straight off disk, no
    /// <c>AssetContentManager</c> needed for one known file path - the engine's own
    /// <see cref="CasaEngine.Framework.Assets.Loaders.DialogueAssetLoader"/>, same shape
    /// <c>IntroTraceHarnessTests.LoadDialogueAssetBestEffort</c> uses for the intro harness's own map/
    /// shared dialogue assets. Used by tests that need the real exported strings (e.g. the real
    /// <c>Dialogues/Etc.dialogue</c>'s OUI/NON labels or item names) rather than a synthetic fixture.</summary>
    public static DialogueAsset LoadFromDisk(string path)
    {
        var asset = new CasaEngine.Framework.Assets.Loaders.DialogueAssetLoader().LoadAsset(path, null!) as DialogueAsset;
        Assert.NotNull(asset);
        return asset!;
    }

    private static string Escape(string text)
        => text
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace("#", "\\#")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("{", "\\{")
            .Replace("}", "\\}");
}
