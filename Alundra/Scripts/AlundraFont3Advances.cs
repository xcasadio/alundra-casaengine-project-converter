#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CasaEngine.Core.Logging;

namespace Alundra.Scripts;

/// <summary>
/// E19.f2b1b F2B1B-R1: the advances of the glyphs of the dialogue box, read from <c>UI/font3.fnt</c> of the project (the lines <c>char id=N ... xadvance=M</c>, the ids being Unicode
/// code points: <c>id=339</c> is the oe ligature, <c>id=21</c> the glyph 21). They make the width of a centred line (<c>\H</c>), which the binary measures with the advances of its own glyph table;
/// on every glyph of the corpus the two agree (docs/plan-e19-f2b1-annexe/s025-notes.md, summary 2). A missing or unreadable file gives an empty table - every width 0, every x 32 - and one warning
/// per project path. Same shape as <see cref="AlundraSoundBank"/>: a session cache keyed by the path of the project.
/// </summary>
internal sealed class AlundraFont3Advances
{
    private const string RelativeDirectory = "UI";
    private const string FileName = "font3.fnt";

    private readonly Dictionary<int, int> _advanceByCodePoint = new();

    /// <summary>Reads <c>UI/font3.fnt</c> under <paramref name="projectPath"/>.</summary>
    public AlundraFont3Advances(string projectPath)
    {
        var filePath = Path.Combine(projectPath, RelativeDirectory, FileName);
        try
        {
            if (!File.Exists(filePath))
            {
                Logs.WriteWarning($"AlundraFont3Advances: '{filePath}' not found; every centred line of the dialogue box gets width 0 and x 32 (degraded mode).");
                return;
            }

            foreach (var line in File.ReadLines(filePath))
            {
                if (!line.StartsWith("char ", StringComparison.Ordinal))
                {
                    continue;
                }

                int? id = null;
                int? advance = null;
                foreach (var part in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.StartsWith("id=", StringComparison.Ordinal) && int.TryParse(part.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
                    {
                        id = parsedId;
                    }
                    else if (part.StartsWith("xadvance=", StringComparison.Ordinal) && int.TryParse(part.AsSpan(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAdvance))
                    {
                        advance = parsedAdvance;
                    }
                }

                if (id is { } codePoint && advance is { } value)
                {
                    _advanceByCodePoint[codePoint] = value;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logs.WriteWarning($"AlundraFont3Advances: failed to read '{filePath}' ({ex.Message}); every centred line of the dialogue box gets width 0 and x 32 (degraded mode).");
            _advanceByCodePoint.Clear();
        }
    }

    /// <summary>The advance of the glyph shown as <paramref name="display"/>; 0 for a code point the file does not describe.</summary>
    public int Advance(char display) => _advanceByCodePoint.TryGetValue(display, out var advance) ? advance : 0;

    private static readonly Dictionary<string, AlundraFont3Advances> SessionCacheByProjectPath = new();

    /// <summary>The cached table of <paramref name="projectPath"/>, read on the first request.</summary>
    public static AlundraFont3Advances GetOrCreate(string projectPath)
    {
        if (!SessionCacheByProjectPath.TryGetValue(projectPath, out var table))
        {
            table = new AlundraFont3Advances(projectPath);
            SessionCacheByProjectPath[projectPath] = table;
        }

        return table;
    }

    /// <summary>Test-only: clears the session cache so tests do not leak tables into each other.</summary>
    internal static void ResetForTests() => SessionCacheByProjectPath.Clear();
}
