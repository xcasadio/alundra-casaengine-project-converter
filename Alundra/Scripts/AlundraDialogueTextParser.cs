#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// One page of a dialogue string (docs/plan-e12-dialogues.md, D-E12-4): the text to DISPLAY (control
/// codes already stripped, <c>\N</c> already resolved to a real newline) plus every NUMERIC control code
/// (<c>\&lt;digits&gt;</c>) found on this page, in the order they were encountered, RAW (not yet OR'd
/// with the temporary-flag bit - see <see cref="AlundraEventProgramRunner"/>'s own dispatch case 0x0D for
/// the exact <c>AddFlag(n | 0x8000, 1 &lt;&lt; (n &amp; 0x1f))</c> application, proven equivalent to the
/// decompiled <c>TextDecoder.cs:259-307</c> formula in that method's own doc).
/// </summary>
public readonly record struct AlundraDialoguePage(string DisplayText, IReadOnlyList<int> NumericCodes);

/// <summary>
/// Splits one dialogue string (already resolved from <c>strings.json</c>/the shared table) into pages on
/// <c>\A</c> (D-E12-4) - a page break is where the ORIGINAL's own <c>TextDecoder</c> sets
/// <c>g_textHoldState</c> and returns, waiting for the interact button (TextDecoder.cs, case 'A').
/// Within a page: <c>\N</c> becomes a real newline; a run of digits after a backslash is a NUMERIC
/// control code (collected on <see cref="AlundraDialoguePage.NumericCodes"/>, not rendered - the caller
/// applies each one as a temporary flag, D-E12-4's own P0 correction); every OTHER backslash + single
/// character is an unhandled decorative/voice code (per docs/intro-roadmap's own control-codes.json
/// inventory - <c>\B/\C/\D/\E/\F/\H/\T/\W/\Y/...</c>), stripped from the displayed text and counted/
/// logged once per code (E12.c owns any real behaviour for these - the typewriter pause of <c>\W&lt;n&gt;</c>,
/// the centring of <c>\C</c>, etc. - out of scope here).
///
/// Documented deviation: an unhandled LETTER code that is itself followed by a bare numeric OPERAND
/// (e.g. <c>\W2</c> - a "wait 2 ticks" cadence code, cf. control-codes.json's own examples) has that
/// operand digit fall through to the NEXT loop iteration with no backslash in front of it any more, so it
/// renders as a literal stray digit in the displayed text instead of being consumed as part of the code.
/// Immaterial to E12.a (instantaneous, non-typewriter display; no test constructs this combination) - left
/// for E12.c, which owns the real meaning of these codes, to resolve properly.
/// </summary>
public static class AlundraDialogueTextParser
{
    private static readonly HashSet<char> LoggedCodes = new();
    private static readonly Dictionary<char, int> Counts = new();

    /// <summary>Every unhandled control-code letter encountered so far this process, with its total
    /// occurrence count (T4) - logged once per letter (<see cref="Logs.WriteDebug"/>), counted every time.</summary>
    public static IReadOnlyDictionary<char, int> UnknownCodeCounts => Counts;

    /// <summary>Test-only: clears the shared logging/counting state so successive tests do not leak into
    /// each other (same shape as every other session-wide "logged once" HashSet in this DLL).</summary>
    internal static void ResetCountersForTests()
    {
        LoggedCodes.Clear();
        Counts.Clear();
    }

    public static IReadOnlyList<AlundraDialoguePage> SplitIntoPages(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var pages = new List<AlundraDialoguePage>();
        var text = new StringBuilder();
        var numericCodes = new List<int>();

        var i = 0;
        while (i < raw.Length)
        {
            var c = raw[i];

            if (c == '\\' && i + 1 < raw.Length)
            {
                var next = raw[i + 1];

                if (next == 'A')
                {
                    pages.Add(new AlundraDialoguePage(text.ToString(), numericCodes));
                    text.Clear();
                    numericCodes = new List<int>();
                    i += 2;
                    continue;
                }

                if (next == 'N')
                {
                    text.Append('\n');
                    i += 2;
                    continue;
                }

                if (next is >= '0' and <= '9')
                {
                    var j = i + 1;
                    while (j < raw.Length && raw[j] is >= '0' and <= '9')
                    {
                        j++;
                    }

                    if (int.TryParse(raw.AsSpan(i + 1, j - (i + 1)), out var n))
                    {
                        numericCodes.Add(n);
                    }

                    i = j;
                    continue;
                }

                RecordUnknownCode(next);
                i += 2;
                continue;
            }

            text.Append(c);
            i++;
        }

        pages.Add(new AlundraDialoguePage(text.ToString(), numericCodes));
        return pages;
    }

    private static void RecordUnknownCode(char code)
    {
        Counts[code] = Counts.GetValueOrDefault(code) + 1;

        if (LoggedCodes.Add(code))
        {
            Logs.WriteDebug(
                $"AlundraDialogueTextParser: control code '\\{code}' not handled (D-E12-4 scope: only "
                + "\\A/\\N/numeric codes are active in E12.a) - stripped from displayed text.");
        }
    }
}
