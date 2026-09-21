#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// Resolves opcode 0x44's own OUI/NON choice labels (docs/plan-e12-dialogues.md, D-E12-6): the GLOBAL
/// strings table (<c>Dialogues/global-strings.json</c>, keyed by decimal ETC_RES offset) addressed
/// through the ETC index table (<c>Dialogues/etc-index.json</c>, 1024 raw entries, E12.b's own export -
/// see that decision's own doc for why the converter needed a brand-new one-shot analyser dump for this,
/// the region never having been extracted before). <c>GetEtcString(id) = global-strings[etc-index[id]]</c>;
/// 0x44's own OUI/NON pair sits at ids 0x43/0x44 (confirmed on real data: etc-index[0x43]=3656 -&gt;
/// global-strings["3656"]="OUI", etc-index[0x44]=3660 -&gt; "NON").
///
/// Degraded mode (mirrors every other project-data loader in this DLL): a missing/malformed/short
/// etc-index.json, a missing/malformed global-strings.json, or a missing key in either logs exactly one
/// warning and returns false - <see cref="AlundraEventProgramRunner"/>'s own dispatch case 0x44 then
/// falls back to its OWN degraded behaviour for this one dialogue instance (optimistic Result=1), same
/// as if no presenter were attached at all.
/// </summary>
public static class AlundraEtcStringTable
{
    private const int YesIndex = 0x43;
    private const int NoIndex = 0x44;

    private static bool _loggedFailureOnce;

    public static bool TryResolveYesNo(string projectPath, out string yesLabel, out string noLabel)
    {
        yesLabel = string.Empty;
        noLabel = string.Empty;

        try
        {
            var etcIndexPath = Path.Combine(projectPath, "Dialogues", "etc-index.json");
            var globalStringsPath = Path.Combine(projectPath, "Dialogues", "global-strings.json");

            if (!File.Exists(etcIndexPath) || !File.Exists(globalStringsPath))
            {
                LogFailureOnce($"'{etcIndexPath}' or '{globalStringsPath}' not found");
                return false;
            }

            var etcIndex = JsonSerializer.Deserialize<int[]>(File.ReadAllText(etcIndexPath));
            var globalStrings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(globalStringsPath));

            if (etcIndex == null || globalStrings == null || etcIndex.Length <= NoIndex)
            {
                LogFailureOnce("etc-index.json/global-strings.json parsed to nothing, or etc-index has fewer than 0x45 entries");
                return false;
            }

            if (!globalStrings.TryGetValue(etcIndex[YesIndex].ToString(), out yesLabel!)
                || !globalStrings.TryGetValue(etcIndex[NoIndex].ToString(), out noLabel!))
            {
                LogFailureOnce("etc-index[0x43]/[0x44] do not resolve to keys present in global-strings.json");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ex.Message);
            return false;
        }
    }

    // E13.d D4 (docs/plan-e13d-inventaire.md, §1.4/§6 point 3): the inventory's own three string lookups -
    // GetItemName/GetItemDescription/GetItemDescriptionSecondLine (alundra-datas-analyser
    // AlundraTools/AlundraEngine/Etc/EtcRes.cs:25-31). Confirmed in EtcResUsa.cs:83-107 (the only concrete
    // EtcRes today): GetItemName(id) reads StringByIndex[IndexTable[id + 0x200]] (via the IconNames[id*2]
    // cache built by the same loop, EtcResUsa.cs:57-63 - the loop variable equals id, so the cache is just
    // that one indirection); GetItemDescription(itemId) reads IndexTable[itemId + 0x280]
    // (EtcResUsa.cs:65-71/98-101); GetItemDescriptionSecondLine(itemId) reads IndexTable[itemId + 0x300]
    // (EtcResUsa.cs:73-79/103-106) - matching the plan's own "id + 0x200 / 0x280 / 0x300". All three share
    // the same etc-index.json/global-strings.json pair TryResolveYesNo already loads, so they share its
    // degraded-mode discipline (one warning, false) through the private helper below.
    private const int ItemNameOffset = 0x200;
    private const int ItemDescriptionLine0Offset = 0x280;
    private const int ItemDescriptionLine1Offset = 0x300;

    /// <summary>Port of <c>EtcRes.GetItemName</c> (EtcResUsa.cs:83-91) - the weapon/item name shown in the
    /// inventory's two name boxes (MainInventoryManager.cs:1750-1793).</summary>
    public static bool TryResolveItemName(string projectPath, int itemId, out string name) =>
        TryResolveIndexedString(projectPath, itemId + ItemNameOffset, out name);

    /// <summary>Port of <c>EtcRes.GetItemDescription</c> (EtcResUsa.cs:98-101) - the description box's
    /// first line (MainInventoryManager.cs:1008-1024, state <c>0x4e..0x8d</c>).</summary>
    public static bool TryResolveItemDescriptionLine0(string projectPath, int itemId, out string line) =>
        TryResolveIndexedString(projectPath, itemId + ItemDescriptionLine0Offset, out line);

    /// <summary>Port of <c>EtcRes.GetItemDescriptionSecondLine</c> (EtcResUsa.cs:103-106) - the description
    /// box's second line (MainInventoryManager.cs:1039-1056, state <c>0x8f..0xce</c>).</summary>
    public static bool TryResolveItemDescriptionLine1(string projectPath, int itemId, out string line) =>
        TryResolveIndexedString(projectPath, itemId + ItemDescriptionLine1Offset, out line);

    // The inventory's text reveal asks for a string on every logic tick while it types (50 per second):
    // the two tables are parsed once and kept, re-read only when either file changes on disk.
    private static string? _cachedEtcIndexPath;
    private static DateTime _cachedEtcIndexWriteTime;
    private static DateTime _cachedGlobalStringsWriteTime;
    private static int[]? _cachedEtcIndex;
    private static Dictionary<string, string?>? _cachedGlobalStrings;

    private static (int[]? EtcIndex, Dictionary<string, string?>? GlobalStrings) LoadCached(
        string etcIndexPath, string globalStringsPath)
    {
        var etcIndexWriteTime = File.GetLastWriteTimeUtc(etcIndexPath);
        var globalStringsWriteTime = File.GetLastWriteTimeUtc(globalStringsPath);

        if (_cachedEtcIndexPath != etcIndexPath
            || _cachedEtcIndexWriteTime != etcIndexWriteTime
            || _cachedGlobalStringsWriteTime != globalStringsWriteTime)
        {
            _cachedEtcIndex = JsonSerializer.Deserialize<int[]>(File.ReadAllText(etcIndexPath));
            _cachedGlobalStrings = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(globalStringsPath));
            _cachedEtcIndexPath = etcIndexPath;
            _cachedEtcIndexWriteTime = etcIndexWriteTime;
            _cachedGlobalStringsWriteTime = globalStringsWriteTime;
        }

        return (_cachedEtcIndex, _cachedGlobalStrings);
    }

    private static bool TryResolveIndexedString(string projectPath, int index, out string value)
    {
        value = string.Empty;

        try
        {
            var etcIndexPath = Path.Combine(projectPath, "Dialogues", "etc-index.json");
            var globalStringsPath = Path.Combine(projectPath, "Dialogues", "global-strings.json");

            if (!File.Exists(etcIndexPath) || !File.Exists(globalStringsPath))
            {
                LogFailureOnce($"'{etcIndexPath}' or '{globalStringsPath}' not found");
                return false;
            }

            var (etcIndex, globalStrings) = LoadCached(etcIndexPath, globalStringsPath);

            if (etcIndex == null || globalStrings == null || index < 0 || etcIndex.Length <= index)
            {
                LogFailureOnce("etc-index.json/global-strings.json parsed to nothing, or index out of range");
                return false;
            }

            if (etcIndex[index] < 0 || !globalStrings.TryGetValue(etcIndex[index].ToString(), out var found))
            {
                LogFailureOnce($"etc-index[{index}] does not resolve to a key present in global-strings.json");
                return false;
            }

            // global-strings.json holds a JSON null for every offset with no text (562 of them - for
            // instance the base dagger's second description line): the original reads those as
            // "no text" and replaces them with an empty string at every inventory call site
            // (MainInventoryManager.cs:972/:1010/:1041, "?? string.Empty"). Same here, once, for every
            // caller: a present key never yields null.
            value = found ?? string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ex.Message);
            return false;
        }
    }

    private static void LogFailureOnce(string reason)
    {
        if (_loggedFailureOnce)
        {
            return;
        }

        _loggedFailureOnce = true;
        Logs.WriteWarning(
            $"AlundraEtcStringTable: could not resolve the OUI/NON choice labels ({reason}) - opcode 0x44 "
            + "will fall back to its own degraded mode for this dialogue.");
    }

    /// <summary>Test-only: clears the one-shot warning latch so successive tests can each observe their
    /// own failure log.</summary>
    internal static void ResetForTests() => _loggedFailureOnce = false;
}
