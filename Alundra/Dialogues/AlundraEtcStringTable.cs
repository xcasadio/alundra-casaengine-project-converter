#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Yarn;

namespace Alundra.Scripts;

/// <summary>
/// E15.c T6 (docs/plan-e15-yarn.md, contract items 5/6; ADR-0006, `docs/formats/dialogues-yarn.md`):
/// resolves opcode 0x44's own OUI/NON choice labels (D-E12-6) and the inventory's item names and
/// descriptions from the <c>dialogue_etc</c> Yarn asset (the compiled <c>Dialogues/Etc.dialogue</c>),
/// keeping this class' own public API from before the port
/// (<see cref="TryResolveYesNo"/>/<see cref="TryResolveItemName"/>/
/// <see cref="TryResolveItemDescriptionLine0"/>/<see cref="TryResolveItemDescriptionLine1"/> and their
/// signatures) so none of this DLL's eight call sites need to change. <c>projectPath</c> is no longer
/// read by this class - the asset comes from the catalog/asset manager (<see cref="EnsureLoaded"/>),
/// which is global to the one project a running game has loaded - but the parameter stays so every
/// caller (all of which still pass <c>EngineEnvironment.ProjectPath</c> or a fixture directory) keeps
/// compiling unchanged.
///
/// <b>Loading (contract item 5)</b>: <see cref="EnsureLoaded"/> is this table's ONE production entry
/// point - called once per game from <see cref="AlundraWorldProxy.InitializeWithWorld"/>, the same
/// "idempotent, called every world load" shape as
/// <see cref="AlundraPlayerController.EnsureInputMappingsRegistered"/> - and resolves the catalogued
/// <c>dialogue_etc</c> asset through <see cref="AssetCatalog.Get"/> and
/// <see cref="AssetContentManager.LoadCopy{T}"/> (ADR-0037's own "read once to derive an instance"
/// shape: this table has no per-world owner to give a counted handle back to - the ETC texts are read
/// across the whole session, not one world's lifetime, so a plain, uncounted copy is enough). A missing
/// asset or a failed load degrades exactly like a missing project file used to: one warning, then every
/// <c>TryResolve*</c> call returns <see langword="false"/>.
///
/// <b>Reading a line</b>: an ETC index becomes the node <c>Etc_{index:0000}</c> (decimal, four digits -
/// same convention as the converter's own <c>EtcIndexTable</c>-resolved nodes,
/// `docs/formats/dialogues-yarn.md`), read as <c>line:{node}_p0</c> through
/// <see cref="DialogueAsset.TryGetLineText"/>, expanded and parsed by <see cref="YarnLineTextParser"/>
/// (no substitutions expected on any ETC line) and turned into <c>font3</c> text by
/// <see cref="AlundraDialogueCapturePresenter.ToFont3Text"/> - the SAME glyph/<c>[br/]</c> conversion
/// T5's dialogue capture presenter already applies to dialogue-box lines, kept as the one
/// implementation. An index with no node (the converter only emits one for a non-null ETC entry) gives
/// what a null <c>global-strings.json</c> entry gave before this port: <see langword="true"/> and an
/// empty string, never a failure.
///
/// <b>Cache</b>: the inventory's text reveal asks for a name/description on every logic tick while it
/// types (50 per second, <see cref="AlundraInventoryTextReveal"/>) - each line's already-converted
/// <c>font3</c> text is kept in <see cref="_parsedTextCache"/> the first time it is read, so
/// <see cref="YarnLineTextParser"/> parses it only once per session, never once per tick.
/// </summary>
public static class AlundraEtcStringTable
{
    private const string EtcCatalogName = "dialogue_etc";

    private const int YesIndex = 0x43;
    private const int NoIndex = 0x44;

    private const int ItemNameOffset = 0x200;
    private const int ItemDescriptionLine0Offset = 0x280;
    private const int ItemDescriptionLine1Offset = 0x300;

    private const int MinEtcIndex = 0;
    private const int MaxEtcIndex = 1023;

    private static bool _loggedFailureOnce;
    private static bool _loadAttempted;
    private static DialogueAsset? _etcAsset;

    // The inventory's text reveal asks for a string on every logic tick while it types (50 per second):
    // each line is parsed into font3 text once and kept, keyed by its compiled line id.
    private static readonly Dictionary<string, string> _parsedTextCache = new(StringComparer.Ordinal);

    /// <summary>
    /// This table's one production entry point (contract item 5): loads <c>dialogue_etc</c> from the
    /// catalog through <paramref name="assetContentManager"/> the first time it is called with a
    /// non-null one, and does nothing on every later call (idempotent, same shape as
    /// <see cref="AlundraPlayerController.EnsureInputMappingsRegistered"/>) - so a per-world caller such
    /// as <see cref="AlundraWorldProxy.InitializeWithWorld"/> can call it unconditionally on every world
    /// load. A null <paramref name="assetContentManager"/> (headless/editor-preview, no
    /// <see cref="CasaEngine.Framework.Application.CasaEngineGame"/>) is a no-op, exactly like
    /// <see cref="AlundraPlayerController.EnsureInputMappingsRegistered"/>'s own null-game gate: every
    /// <c>TryResolve*</c> call then degrades (one warning, <see langword="false"/>) until a later call
    /// with a real game succeeds.
    /// </summary>
    public static void EnsureLoaded(AssetContentManager? assetContentManager)
    {
        if (_loadAttempted || assetContentManager == null)
        {
            return;
        }

        _loadAttempted = true;

        var assetInfo = AssetCatalog.Get(EtcCatalogName);
        if (assetInfo == null)
        {
            LogFailureOnce($"no '{EtcCatalogName}' asset in the catalog");
            return;
        }

        try
        {
            _etcAsset = assetContentManager.LoadCopy<DialogueAsset>(assetInfo.Id);
        }
        catch (Exception ex)
        {
            LogFailureOnce($"'{EtcCatalogName}' ({assetInfo.Id}) failed to load: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// E16.e L6 (docs/plan-e16-etat-partie.md): the compiled <c>dialogue_etc</c> asset this table loaded
    /// (<see cref="EnsureLoaded"/>) or was given (<see cref="SetEtcDialogueAssetForTests"/>), or null when none
    /// is loaded - what the save book hands <see cref="AlundraDialogueDirector.Open"/> to show ETC <c>0x40</c>
    /// through the ordinary box (node <c>Etc_0064</c>), as the original's <c>InitializeDialogMessage</c> does
    /// (<c>AI_ProcessWarpTransitionState</c>, 0x8007B998, state 1).
    /// </summary>
    public static DialogueAsset? EtcDialogueAsset => _etcAsset;

    /// <summary>
    /// E16.e L6 (docs/plan-e16-etat-partie.md): public, generic port of <c>EtcRes.GetEtcString</c>
    /// (0x800816D4) - ETC text <paramref name="etcIndex"/>, read, cached and converted to <c>font3</c> text
    /// exactly like the item names (<see cref="TryGetEtcText"/>). An index whose entry is empty in the
    /// original (the 8 save-flow texts of J5, <c>0x88</c> among them, empty in <c>ETC_RES.R</c> itself) gives
    /// <see langword="true"/> and an empty line, as the original draws nothing for them; <see langword="false"/>
    /// only without a loaded asset or outside <c>0..1023</c>.
    /// </summary>
    public static bool TryResolveText(int etcIndex, out string text) => TryGetEtcText(etcIndex, out text);

    public static bool TryResolveYesNo(string projectPath, out string yesLabel, out string noLabel)
    {
        var yesOk = TryGetEtcText(YesIndex, out yesLabel);
        var noOk = TryGetEtcText(NoIndex, out noLabel);
        return yesOk && noOk;
    }

    /// <summary>Port of <c>EtcRes.GetItemName</c> (EtcResUsa.cs:83-91) - the weapon/item name shown in the
    /// inventory's two name boxes (MainInventoryManager.cs:1750-1793).</summary>
    public static bool TryResolveItemName(string projectPath, int itemId, out string name) =>
        TryGetEtcText(itemId + ItemNameOffset, out name);

    /// <summary>Port of <c>EtcRes.GetItemDescription</c> (EtcResUsa.cs:98-101) - the description box's
    /// first line (MainInventoryManager.cs:1008-1024, state <c>0x4e..0x8d</c>).</summary>
    public static bool TryResolveItemDescriptionLine0(string projectPath, int itemId, out string line) =>
        TryGetEtcText(itemId + ItemDescriptionLine0Offset, out line);

    /// <summary>Port of <c>EtcRes.GetItemDescriptionSecondLine</c> (EtcResUsa.cs:103-106) - the description
    /// box's second line (MainInventoryManager.cs:1039-1056, state <c>0x8f..0xce</c>).</summary>
    public static bool TryResolveItemDescriptionLine1(string projectPath, int itemId, out string line) =>
        TryGetEtcText(itemId + ItemDescriptionLine1Offset, out line);

    /// <summary>
    /// Reads ETC index <paramref name="etcIndex"/> as node <c>Etc_{etcIndex:0000}</c>'s single page
    /// (contract item 5): the compiled asset's raw line text, expanded (no substitutions on any ETC
    /// line), parsed and turned into <c>font3</c> text, cached by line id. Returns
    /// <see langword="false"/> only when no <c>dialogue_etc</c> asset could be loaded at all, or for an
    /// index outside the table's own <c>0..1023</c> range (a caller error, never produced by the
    /// converter) - a valid index with no node (an ETC entry the converter left empty) returns
    /// <see langword="true"/> and an empty string, the same result a null <c>global-strings.json</c>
    /// entry gave before this port.
    /// </summary>
    private static bool TryGetEtcText(int etcIndex, out string text)
    {
        text = string.Empty;

        if (_etcAsset == null)
        {
            LogFailureOnce($"no '{EtcCatalogName}' asset loaded ({nameof(EnsureLoaded)} was not called, or it failed)");
            return false;
        }

        if (etcIndex < MinEtcIndex || etcIndex > MaxEtcIndex)
        {
            LogFailureOnce($"ETC index {etcIndex} is out of range ({MinEtcIndex}..{MaxEtcIndex})");
            return false;
        }

        var lineId = $"line:Etc_{etcIndex:D4}_p0";
        if (_parsedTextCache.TryGetValue(lineId, out var cached))
        {
            text = cached;
            return true;
        }

        // D-E15-10/contract item 5: an index whose entry was empty in the original has no node in the
        // exported asset - that is not a failure, it is what an empty entry already rendered as.
        if (!_etcAsset.TryGetLineText(lineId, out var rawText))
        {
            _parsedTextCache[lineId] = string.Empty;
            return true;
        }

        var expanded = YarnLineTextParser.ExpandSubstitutions(rawText, Array.Empty<string>());
        var line = YarnLineTextParser.Parse(expanded);
        var font3Text = AlundraDialogueCapturePresenter.ToFont3Text(line);

        _parsedTextCache[lineId] = font3Text;
        text = font3Text;
        return true;
    }

    private static void LogFailureOnce(string reason)
    {
        if (_loggedFailureOnce)
        {
            return;
        }

        _loggedFailureOnce = true;
        Logs.WriteWarning(
            $"AlundraEtcStringTable: could not resolve an ETC text ({reason}) - OUI/NON and every item "
            + "name/description fall back to their own caller's degraded mode.");
    }

    /// <summary>Test-only entry point (contract item 5's "seul point d'entrée des tests"): injects a
    /// compiled ETC asset directly, bypassing <see cref="AssetCatalog"/>/<see cref="AssetContentManager"/>
    /// entirely - build one with <c>DialogueTestAssets</c>, nodes named <c>Etc_{index:0000}</c> (same
    /// shape as <see cref="EnsureLoaded"/> would have loaded from the catalog). Marks the table as
    /// already loaded, so a later production <see cref="EnsureLoaded"/> call in the same process (there
    /// is none in tests) would be a no-op.</summary>
    internal static void SetEtcDialogueAssetForTests(DialogueAsset? asset)
    {
        _etcAsset = asset;
        _loadAttempted = true;
        _parsedTextCache.Clear();
    }

    /// <summary>Test-only: clears the one-shot warning latch, the loaded/injected asset and the parsed-text
    /// cache so successive tests start from the same "nothing loaded yet" state.</summary>
    internal static void ResetForTests()
    {
        _loggedFailureOnce = false;
        _loadAttempted = false;
        _etcAsset = null;
        _parsedTextCache.Clear();
    }
}
