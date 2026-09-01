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
