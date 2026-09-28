#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Engine.Environment;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E15.c T6 (docs/plan-e15-yarn.md, contract items 5/6): unit coverage of
/// <see cref="AlundraEtcStringTable"/> itself, against a <c>dialogue_etc</c>-shaped asset injected
/// through <see cref="AlundraEtcStringTable.SetEtcDialogueAssetForTests"/> - never a raw-file reader.
/// Integration-level coverage (through the real inventory/sub-inventory directors and the real OUI/NON
/// dispatch) lives in <see cref="AlundraInventoryDirectorTests"/>, <see cref="AlundraSubInventoryDirectorTests"/>,
/// <see cref="AlundraDialogueOpcodeDispatchTests"/> and <see cref="AlundraDialogueOpcodesProductionTests"/>.
/// <c>projectPath</c> is passed as <see cref="EngineEnvironment.ProjectPath"/> throughout, matching every
/// production call site, even though the table itself no longer reads it (see that class' own doc).
/// </summary>
public sealed class AlundraEtcStringTableTests : IDisposable
{
    public AlundraEtcStringTableTests() => AlundraEtcStringTable.ResetForTests();

    public void Dispose() => AlundraEtcStringTable.ResetForTests();

    [Fact]
    public void TryResolveYesNo_ReadsEtc0067AndEtc0068()
    {
        // D-E12-6/§5: 0x44's own OUI/NON pair sits at ETC ids 0x43/0x44 - node naming is the ETC index in
        // decimal, four digits (docs/formats/dialogues-yarn.md).
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((0x43, "OUI"), (0x44, "NON")));

        Assert.True(AlundraEtcStringTable.TryResolveYesNo(EngineEnvironment.ProjectPath, out var yesLabel, out var noLabel));
        Assert.Equal("OUI", yesLabel);
        Assert.Equal("NON", noLabel);
    }

    [Fact]
    public void TryResolveItemName_ReadsTheNamedNode()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((5 + 0x200, "Dague")));

        Assert.True(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 5, out var name));
        Assert.Equal("Dague", name);
    }

    [Fact]
    public void TryResolveItemDescriptionLine0_ReadsTheNamedNode()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((5 + 0x280, "Une lame courte.")));

        Assert.True(AlundraEtcStringTable.TryResolveItemDescriptionLine0(EngineEnvironment.ProjectPath, 5, out var line));
        Assert.Equal("Une lame courte.", line);
    }

    /// <summary>Contract item 5/D-E15-6: the raw byte <c>0x1A</c> of an object description becomes
    /// <c>[glyph id=26/]</c> at export, which this table renders as <c>font3</c> character 26 (□) - the
    /// same conversion T5's <see cref="AlundraDialogueCapturePresenter.ToFont3Text"/> applies to a
    /// dialogue-box line, shared rather than reimplemented.</summary>
    [Fact]
    public void TryResolveItemDescriptionLine1_GlyphMarker_RendersAsFont3Character26()
    {
        // itemId 7's description-line-1 offset (7 + 0x300 = 775) - built with BuildMultiNode (not
        // BuildEtc) since the marker's brackets must reach the compiler unescaped.
        var asset = DialogueTestAssets.BuildMultiNode("Etc", ("Etc_0775", new[] { "Cd[glyph id=26/]Ef" }));
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(asset);

        Assert.True(AlundraEtcStringTable.TryResolveItemDescriptionLine1(EngineEnvironment.ProjectPath, 7, out var line));
        Assert.Equal("Cd" + (char)26 + "Ef", line);
    }

    /// <summary>Contract item 5/D-E15-10: the converter only emits a node for a non-null ETC entry - an
    /// index with none behaves like an empty entry did before the port (<see langword="true"/>, empty),
    /// never a failure.</summary>
    [Fact]
    public void TryResolveItemName_IndexWithoutNode_ResolvesToAnEmptyStringNotAFailure()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc());

        Assert.True(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 9, out var name));
        Assert.Equal(string.Empty, name);
    }

    /// <summary>An ETC index outside the table's own <c>0..1023</c> range (negative, or past the last
    /// entry) is a caller error, never produced by the converter, and returns <see langword="false"/> -
    /// NOT <see langword="true"/> with an empty string, which is reserved for an in-range index whose
    /// entry the converter left empty (<see cref="TryResolveItemName_IndexWithoutNode_ResolvesToAnEmptyStringNotAFailure"/>).</summary>
    [Fact]
    public void TryResolveItemName_IndexOutOfRange_ReturnsFalse()
    {
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc());

        // itemId -600 -> ETC index -600 + 0x200 (512) = -88 (negative).
        Assert.False(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, -600, out var negative));
        Assert.Equal(string.Empty, negative);

        // itemId 600 -> ETC index 600 + 0x200 (512) = 1112, past the last valid index (1023).
        Assert.False(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 600, out var pastEnd));
        Assert.Equal(string.Empty, pastEnd);
    }

    /// <summary>No asset loaded at all (neither <see cref="AlundraEtcStringTable.EnsureLoaded"/> nor test
    /// injection ran) is the one real failure - every <c>TryResolve*</c> call returns
    /// <see langword="false"/>, same degraded shape as a missing project file gave before the port.</summary>
    [Fact]
    public void TryResolveItemName_NoAssetLoaded_ReturnsFalse()
    {
        Assert.False(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 5, out var name));
        Assert.Equal(string.Empty, name);
    }

    /// <summary>Contract item 5: the inventory's text reveal asks for a name/description on every logic
    /// tick while it types - the SECOND read of the same line must come from the cache, not a fresh
    /// <c>YarnLineTextParser</c> pass. Proven by mutating the asset's own raw line text between the two
    /// reads: a cache hit keeps returning the FIRST parse.</summary>
    [Fact]
    public void TryResolveItemName_SecondRead_ComesFromTheCache_NotAFreshParse()
    {
        var asset = DialogueTestAssets.BuildEtc((7 + 0x200, "Ab")); // 7 + 0x200 = 519 -> node Etc_0519.
        AlundraEtcStringTable.SetEtcDialogueAssetForTests(asset);

        Assert.True(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 7, out var first));
        Assert.Equal("Ab", first);

        asset.LineTexts["line:Etc_0519_p0"] = "Zz";

        Assert.True(AlundraEtcStringTable.TryResolveItemName(EngineEnvironment.ProjectPath, 7, out var second));
        Assert.Equal("Ab", second);
    }
}
