using System.Globalization;
using System.Linq;
using System.Text;

namespace AlundraCasaEngineProjectConverter.Text;

/// <summary>
/// One string-table entry to emit as a Yarn node (docs/plan-e15-yarn.md, E15.b "Contrat des fichiers
/// et des nœuds"): <paramref name="Title"/> is the node name exactly as the contract names it
/// (<c>M{id}_S{nnn}</c>, <c>Shared_S{nnn}</c> or <c>Etc_{iiii}</c>), <paramref name="Source"/> is the
/// raw string exactly as read from the table (control codes and all, not yet decoded).
/// </summary>
public sealed record YarnTextEntry(string Title, string Source);

/// <summary>
/// A code found in <paramref name="Title"/>'s page <paramref name="PageIndex"/> that has no mapping to
/// Yarn (docs/plan-e15-yarn.md §1): an unsupported escape, a missing or invalid <c>\W</c>/<c>\V</c>/
/// <c>\X</c> operand, a numeric code too large for a 32-bit signed integer, a raw control character
/// other than <c>0x1A</c>/<c>0x1C</c>, an <c>\X0</c> reached after another <c>\X</c> of the same page,
/// or a page whose rendered line would start with Yarn syntax (<c>===</c>, <c>---</c>, <c>-&gt;</c>,
/// <c>=&gt;</c>). The whole node is left out of the emitted source when it has at least one such error.
/// </summary>
public sealed record YarnEmitError(string Title, int PageIndex, string Message)
{
    public override string ToString() => $"{Title}_p{PageIndex}: {Message}";
}

/// <summary>
/// Counters for <c>report.json</c> (T4): what <see cref="YarnTextEmitter.Emit"/> actually wrote to
/// <see cref="YarnTextEmitResult.Source"/> (nodes, lines, empty pages, markers, commands, function
/// calls), plus <see cref="CodeCounts"/>, an inventory of every control code scanned while emitting -
/// whether or not its node ended up written - keyed exactly as docs/plan-e15-yarn.md's E15.b task T3
/// names them (<c>"\A"</c>, <c>"\B"</c> … <c>"\N"</c>, <c>"\T"</c>, <c>"\Y"</c>, <c>"\W&lt;c&gt;"</c>,
/// <c>"\X&lt;c&gt;"</c>, <c>"\V&lt;c&gt;"</c>, <c>"\digits"</c> for a numeric code, <c>"U+001A"</c>,
/// <c>"U+001C"</c>). Over-reporting a scanned code beats hiding one behind a node that failed to emit.
/// </summary>
public sealed class YarnEmitStatistics
{
    private readonly Dictionary<string, int> _codeCounts = new(StringComparer.Ordinal);

    public int Nodes { get; internal set; }
    public int Lines { get; internal set; }
    public int EmptyPages { get; internal set; }
    public int GlyphMarkers { get; internal set; }
    public int FlagMarkers { get; internal set; }
    public int YieldMarkers { get; internal set; }
    public int FalconUpdateCommands { get; internal set; }
    public int FunctionCalls { get; internal set; }

    public IReadOnlyDictionary<string, int> CodeCounts => _codeCounts;

    internal void CountCode(string code)
        => _codeCounts[code] = _codeCounts.GetValueOrDefault(code) + 1;
}

/// <summary>The complete Yarn source of one file, and how it went.</summary>
public sealed record YarnTextEmitResult(string Source, IReadOnlyList<YarnEmitError> Errors, YarnEmitStatistics Statistics);

/// <summary>
/// Renders Alundra's text tables as Yarn source (docs/plan-e15-yarn.md, E15.b task T3), following the
/// correspondence of §1 and the original decoder's semantics
/// (<c>alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs</c>) and the executable's
/// <c>\W</c> glyph formula. Deterministic: the same entries always produce the same
/// <see cref="YarnTextEmitResult.Source"/>, byte for byte.
///
/// Written from the plan and <c>TextDecoder.cs</c> alone, independently of
/// <c>alundra-casaengine-project-converter.Tests/Text/ReferenceTextDecoder.cs</c> (the other, "original
/// side" of the T6 equivalence oracle): the two are never compared against each other from within this
/// file or its tests, only by the separate T6 corpus test.
///
/// One node per <see cref="YarnTextEntry"/>, one Yarn line per page (a page is a run of the source
/// between two <c>\A</c>, or before the first/after the last):
/// <code>
/// title: &lt;title&gt;
/// ---
/// &lt;commands of page 0, one per line: only &lt;&lt;falcon_update&gt;&gt;&gt;
/// &lt;line of page 0&gt; #line:&lt;title&gt;_p0
/// ...
/// ===
/// </code>
/// A numeric code <c>\&lt;digits&gt;</c> and a <c>\Y</c> are not commands: they are the zero-length
/// markers <c>[flag id=N/]</c> and <c>[yield/]</c> in the line, at their position in the text
/// (D-E19-48, ADR-0025).
/// The spaces at the edges of a page are removed (D-E15-8), except the ones that end a page whose last
/// line holds a <c>\H</c> (D-E19-78, D-E19-84): they are kept, and a page that then ends on a space gets a
/// final <c>[empty trimwhitespace=false/]</c> so the Yarn compiler, which drops the spaces that end a line,
/// keeps them (ADR-0038).
/// A node whose source has at least one code with no mapping (an unsupported escape, a missing or
/// invalid operand, a raw control character, an <c>\X0</c> read after another <c>\X</c> of the same
/// page, a numeric code too large for a signed 32-bit integer, or a page whose line would start
/// with Yarn syntax: <c>===</c>, <c>---</c>, <c>-&gt;</c>, <c>=&gt;</c>) is left out of
/// <see cref="YarnTextEmitResult.Source"/> entirely, and each such code is
/// reported once in <see cref="YarnTextEmitResult.Errors"/>. This never throws: bad data becomes an
/// error, not an exception.
/// </summary>
public static class YarnTextEmitter
{
    public static YarnTextEmitResult Emit(IReadOnlyList<YarnTextEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var statistics = new YarnEmitStatistics();
        var errors = new List<YarnEmitError>();
        var blocks = new List<string>();

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            var (block, entryErrors) = ProcessEntry(entry, statistics);
            if (entryErrors.Count > 0)
            {
                errors.AddRange(entryErrors);
                continue;
            }

            blocks.Add(block!);
        }

        return new YarnTextEmitResult(string.Join("\n", blocks), errors, statistics);
    }

    // ---- Per-entry pipeline -------------------------------------------------------------------

    private static (string? Block, List<YarnEmitError> Errors) ProcessEntry(YarnTextEntry entry, YarnEmitStatistics statistics)
    {
        var (pages, errors) = TokenizePages(entry, statistics);
        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var renders = new List<PageRender>(pages.Count);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            renders.Add(BuildPageRender(entry.Title, pageIndex, pages[pageIndex], errors));
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var builder = new StringBuilder();
        builder.Append("title: ").Append(entry.Title).Append('\n').Append("---\n");

        var lineCount = 0;
        var emptyPageCount = 0;
        var glyphCount = 0;
        var flagMarkerCount = 0;
        var yieldMarkerCount = 0;
        var falconCommandCount = 0;
        var functionCallCount = 0;

        for (var pageIndex = 0; pageIndex < renders.Count; pageIndex++)
        {
            var render = renders[pageIndex];
            foreach (var command in render.Commands)
            {
                builder.Append(command).Append('\n');
                falconCommandCount++;
            }

            builder.Append(render.Body).Append(" #line:").Append(entry.Title).Append("_p").Append(pageIndex).Append('\n');

            lineCount++;
            if (render.IsEmpty)
            {
                emptyPageCount++;
            }

            glyphCount += render.GlyphCount;
            flagMarkerCount += render.FlagCount;
            yieldMarkerCount += render.YieldCount;
            functionCallCount += render.FunctionCallCount;
        }

        builder.Append("===\n");

        statistics.Nodes++;
        statistics.Lines += lineCount;
        statistics.EmptyPages += emptyPageCount;
        statistics.GlyphMarkers += glyphCount;
        statistics.FlagMarkers += flagMarkerCount;
        statistics.YieldMarkers += yieldMarkerCount;
        statistics.FalconUpdateCommands += falconCommandCount;
        statistics.FunctionCalls += functionCallCount;

        return (builder.ToString(), errors);
    }

    // ---- Tokenizing (TextDecoder.cs's own dispatch, §1) ----------------------------------------

    private abstract record PageToken;
    private sealed record TextToken(char Char) : PageToken;
    private sealed record LineBreakToken : PageToken; // \N
    private sealed record VoiceToken(int Id) : PageToken; // \B .. \G
    private sealed record CenterToken : PageToken; // \H
    private sealed record SlowToken : PageToken; // \T
    private sealed record GlyphToken(int Id) : PageToken; // \W<c>, or the raw ETC bytes 0x1A/0x1C
    private sealed record GameVarToken(int Index) : PageToken; // \V<digit>
    private sealed record FalconToken(char Code) : PageToken; // \X<0-5>
    private sealed record FlagToken(int Value) : PageToken; // \<digits>, normalised
    private sealed record YieldToken : PageToken; // \Y

    /// <summary>
    /// Walks one entry's raw source once, dispatching on the character after every '\' exactly as
    /// <c>TextDecoder.cs:264-599</c> does, and splits the result into pages on <c>\A</c>. A valid code
    /// is counted into <paramref name="statistics"/>'s code inventory; an invalid or unsupported one is
    /// never counted there - it is reported as an error naming the entry's title and the page it was
    /// found on instead, and the node is left out of the emitted source (§1).
    /// </summary>
    private static (List<List<PageToken>> Pages, List<YarnEmitError> Errors) TokenizePages(
        YarnTextEntry entry, YarnEmitStatistics statistics)
    {
        var pages = new List<List<PageToken>> { new() };
        var errors = new List<YarnEmitError>();
        var source = entry.Source;
        var index = 0;
        var pageIndex = 0;

        void AddError(string message) => errors.Add(new YarnEmitError(entry.Title, pageIndex, message));

        while (index < source.Length)
        {
            var current = source[index];

            if (current == '\\')
            {
                if (index + 1 >= source.Length)
                {
                    AddError("a lone trailing '\\' with no code after it");
                    index++;
                    continue;
                }

                var next = source[index + 1];

                if (next is >= '0' and <= '9')
                {
                    var start = index + 1;
                    var end = start;
                    while (end < source.Length && source[end] is >= '0' and <= '9')
                    {
                        end++;
                    }

                    var digits = source[start..end];
                    if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    {
                        AddError($"numeric code '\\{digits}' does not fit a signed 32-bit integer");
                        index = end;
                        continue;
                    }

                    pages[pageIndex].Add(new FlagToken(value));
                    statistics.CountCode("\\digits");
                    index = end;
                    continue;
                }

                switch (next)
                {
                    case 'A':
                        pageIndex++;
                        pages.Add(new List<PageToken>());
                        statistics.CountCode("\\A");
                        index += 2;
                        continue;
                    case 'B':
                        pages[pageIndex].Add(new VoiceToken(-1));
                        statistics.CountCode("\\B");
                        index += 2;
                        continue;
                    case 'C':
                        pages[pageIndex].Add(new VoiceToken(0));
                        statistics.CountCode("\\C");
                        index += 2;
                        continue;
                    case 'D':
                        pages[pageIndex].Add(new VoiceToken(1));
                        statistics.CountCode("\\D");
                        index += 2;
                        continue;
                    case 'E':
                        pages[pageIndex].Add(new VoiceToken(2));
                        statistics.CountCode("\\E");
                        index += 2;
                        continue;
                    case 'F':
                        pages[pageIndex].Add(new VoiceToken(3));
                        statistics.CountCode("\\F");
                        index += 2;
                        continue;
                    case 'G':
                        pages[pageIndex].Add(new VoiceToken(4));
                        statistics.CountCode("\\G");
                        index += 2;
                        continue;
                    case 'H':
                        pages[pageIndex].Add(new CenterToken());
                        statistics.CountCode("\\H");
                        index += 2;
                        continue;
                    case 'N':
                        pages[pageIndex].Add(new LineBreakToken());
                        statistics.CountCode("\\N");
                        index += 2;
                        continue;
                    case 'T':
                        pages[pageIndex].Add(new SlowToken());
                        statistics.CountCode("\\T");
                        index += 2;
                        continue;
                    case 'Y':
                        // Ends the current render step, the page continues: a zero-length marker at its position.
                        pages[pageIndex].Add(new YieldToken());
                        statistics.CountCode("\\Y");
                        index += 2;
                        continue;
                    case 'V':
                    {
                        if (index + 2 >= source.Length || source[index + 2] is < '0' or > '9')
                        {
                            AddError("\\V has no single decimal digit operand");
                            index = Math.Min(index + 2, source.Length);
                            continue;
                        }

                        var operand = source[index + 2];
                        pages[pageIndex].Add(new GameVarToken(operand - '0'));
                        statistics.CountCode($"\\V{operand}");
                        index += 3;
                        continue;
                    }
                    case 'W':
                    {
                        if (index + 2 >= source.Length)
                        {
                            AddError("\\W has no operand");
                            index += 2;
                            continue;
                        }

                        var operand = source[index + 2];
                        var glyphId = ComputeGlyphId(operand);
                        if (glyphId is null)
                        {
                            AddError($"\\W{operand} has no glyph (only a digit or an uppercase letter maps to one)");
                            index += 3;
                            continue;
                        }

                        pages[pageIndex].Add(new GlyphToken(glyphId.Value));
                        statistics.CountCode($"\\W{operand}");
                        index += 3;
                        continue;
                    }
                    case 'X':
                    {
                        if (index + 2 >= source.Length || source[index + 2] is < '0' or > '5')
                        {
                            AddError("\\X has no 0-5 operand");
                            index = Math.Min(index + 2, source.Length);
                            continue;
                        }

                        var operand = source[index + 2];
                        pages[pageIndex].Add(new FalconToken(operand));
                        statistics.CountCode($"\\X{operand}");
                        index += 3;
                        continue;
                    }
                    default:
                        AddError($"unsupported code '\\{next}'");
                        index += 2;
                        continue;
                }
            }

            if (current < 0x20)
            {
                if (current == '\u001A')
                {
                    pages[pageIndex].Add(new GlyphToken(26));
                    statistics.CountCode("U+001A");
                    index++;
                    continue;
                }

                if (current == '\u001C')
                {
                    pages[pageIndex].Add(new GlyphToken(28));
                    statistics.CountCode("U+001C");
                    index++;
                    continue;
                }

                AddError($"raw control character U+{(int)current:X4}");
                index++;
                continue;
            }

            pages[pageIndex].Add(new TextToken(current));
            index++;
        }

        return (pages, errors);
    }

    /// <summary>
    /// The executable's <c>\W</c> glyph formula (<c>ALUN_CD.EXE</c> <c>0x800462e0</c>-<c>0x800462f0</c>,
    /// not the decompilation, which drops the letter branch): <c>c - 0x20</c> for an ASCII digit,
    /// <c>c - 0x27</c> for an uppercase ASCII letter. Anything else has no known glyph.
    /// </summary>
    private static int? ComputeGlyphId(char operand) => operand switch
    {
        >= '0' and <= '9' => operand - 0x20,
        >= 'A' and <= 'Z' => operand - 0x27,
        _ => null,
    };

    // ---- Per-page rendering --------------------------------------------------------------------

    private abstract record Element;
    private sealed record TextElement(string Text) : Element;
    private sealed record LineBreakElement : Element;
    private sealed record VoiceElement(int Id) : Element;
    private sealed record CenterElement : Element;
    private sealed record SlowElement : Element;
    private sealed record GlyphElement(int Id) : Element;
    private sealed record FunctionCallElement(string Name, string? Argument) : Element;
    private sealed record FlagElement(int Id) : Element;
    private sealed record YieldElement : Element;

    private sealed record PageRender(
        IReadOnlyList<string> Commands, string Body, bool IsEmpty, int GlyphCount, int FlagCount, int YieldCount, int FunctionCallCount);

    /// <summary>
    /// Builds one page's commands and rendered line body. Every numeric code produces its own
    /// <c>[flag id=n/]</c> marker and every <c>\Y</c> a <c>[yield/]</c> marker, at their position in the
    /// text, in source order (repeats included): no <c>&lt;&lt;flag n&gt;&gt;</c> command any more
    /// (D-E19-48). The only command is the single <c>&lt;&lt;falcon_update&gt;&gt;</c> of a page holding
    /// any <c>\X</c> (§1).
    /// <c>\X</c> is resolved to its function by code and by whether it is the first <c>\X</c> of the
    /// page (ADR-0007); an <c>\X0</c> that is not the first is reported as an error into
    /// <paramref name="errors"/> instead of guessing a function for it. A page whose rendered line
    /// would start with a Yarn syntax marker (<c>===</c>, <c>---</c>, <c>-&gt;</c>, <c>=&gt;</c>) is
    /// also reported as an error instead of being emitted as-is. The trailing spaces of a page whose last
    /// line is centred are kept and guarded by <c>[empty trimwhitespace=false/]</c> (D-E19-78, D-E19-84).
    /// </summary>
    private static PageRender BuildPageRender(string title, int pageIndex, List<PageToken> tokens, List<YarnEmitError> errors)
    {
        var commands = new List<string>();
        var falconCommandAdded = false;

        foreach (var token in tokens)
        {
            switch (token)
            {
                case FalconToken when !falconCommandAdded:
                    falconCommandAdded = true;
                    commands.Add("<<falcon_update>>");
                    break;
            }
        }

        var elements = new List<Element>();
        var textBuffer = new StringBuilder();
        var isFirstFalcon = true;

        void FlushText()
        {
            if (textBuffer.Length > 0)
            {
                elements.Add(new TextElement(textBuffer.ToString()));
                textBuffer.Clear();
            }
        }

        foreach (var token in tokens)
        {
            switch (token)
            {
                case FlagToken flag:
                    FlushText();
                    elements.Add(new FlagElement(flag.Value));
                    break;
                case YieldToken:
                    FlushText();
                    elements.Add(new YieldElement());
                    break;
                case TextToken text:
                    textBuffer.Append(text.Char);
                    break;
                case LineBreakToken:
                    FlushText();
                    elements.Add(new LineBreakElement());
                    break;
                case VoiceToken voice:
                    FlushText();
                    elements.Add(new VoiceElement(voice.Id));
                    break;
                case CenterToken:
                    FlushText();
                    elements.Add(new CenterElement());
                    break;
                case SlowToken:
                    FlushText();
                    elements.Add(new SlowElement());
                    break;
                case GlyphToken glyph:
                    FlushText();
                    elements.Add(new GlyphElement(glyph.Id));
                    break;
                case GameVarToken gameVar:
                    FlushText();
                    elements.Add(new FunctionCallElement(
                        AlundraYarnFunctions.GameVar,
                        gameVar.Index.ToString(CultureInfo.InvariantCulture)));
                    break;
                case FalconToken falcon:
                    FlushText();
                    if (TryResolveFalconFunction(falcon.Code, isFirstFalcon, out var functionName))
                    {
                        elements.Add(new FunctionCallElement(functionName!, Argument: null));
                    }
                    else
                    {
                        errors.Add(new YarnEmitError(
                            title,
                            pageIndex,
                            $"\\X{falcon.Code} read after another \\X of the same page has no defined function"));
                    }

                    isFirstFalcon = false;
                    break;
            }
        }

        FlushText();

        // D-E19-78, D-E19-84: the last line of a page holding a \H keeps the spaces that end it. The original's
        // CalcTextWidth counts every one (4 px each in font3) and the text box types them; Yarn drops the
        // spaces that end a compiled line, so they are protected by a zero-length marker (see below).
        var keepTrailingSpaces = LastLineIsCentred(elements);
        TrimEdgeSpaces(elements, keepTrailingSpaces);

        // Checked on the page's leading raw text, before EscapeText: only a TextElement can ever start
        // with one of these (every marker and function call renders as '[' or '{'), and checking the
        // raw text catches "->"/"=>" too, which EscapeText's own escaping of '>' would otherwise always
        // break up before this check ever saw them.
        if (elements.Count > 0 && elements[0] is TextElement firstText)
        {
            var forbiddenPrefix = ForbiddenLinePrefixes.FirstOrDefault(
                prefix => firstText.Text.StartsWith(prefix, StringComparison.Ordinal));
            if (forbiddenPrefix is not null)
            {
                errors.Add(new YarnEmitError(
                    title,
                    pageIndex,
                    $"the emitted line would start with '{forbiddenPrefix}', which Yarn would read as syntax"));
            }
        }

        // F0-R2: flag and yield markers are transparent to the empty-page rule too; a page holding only
        // markers of those two kinds keeps them, in source order, and then gets [empty/].
        var isEmpty = elements.All(IsPositionMarker);
        var renderedBody = RenderElements(elements, out var glyphCount, out var flagCount, out var yieldCount, out var functionCallCount);

        // Yarn 3.2.1 keeps the spaces that precede a self-closing marker but removes the ones that end a line
        // (probed with the project's compiler and parser): a page that ends on kept spaces gets the no-op
        // [empty/] right after them. A trailing flag or yield marker already protects them.
        var endsOnKeptSpaces = keepTrailingSpaces
            && elements.Count > 0
            && elements[^1] is TextElement { Text: var lastText }
            && lastText.EndsWith(' ');
        var body = isEmpty || endsOnKeptSpaces ? renderedBody + "[empty trimwhitespace=false/]" : renderedBody;

        return new PageRender(commands, body, isEmpty, glyphCount, flagCount, yieldCount, functionCallCount);
    }

    /// <summary>
    /// The Yarn line syntax a page's rendered line must never start with (a header/body separator, a
    /// node terminator or a shortcut/jump arrow): none of the corpus's pages produce one, but a future
    /// or hand-written entry could.
    /// </summary>
    private static readonly string[] ForbiddenLinePrefixes = { "===", "---", "->", "=>" };

    /// <summary>
    /// <c>\X0</c>/<c>\X2</c>/<c>\X4</c> read the state a page's single <c>&lt;&lt;falcon_update&gt;&gt;</c>
    /// is about to change when they open the page, and the state it just changed otherwise;
    /// <c>\X1</c>/<c>\X3</c>/<c>\X5</c> always read the changed state (ADR-0007). Returns
    /// <see langword="false"/> for the one case with no defined function: an <c>\X0</c> that is not the
    /// page's first <c>\X</c>.
    /// </summary>
    private static bool TryResolveFalconFunction(char code, bool isFirstFalconOfPage, out string? functionName)
    {
        switch (code)
        {
            case '0':
                functionName = isFirstFalconOfPage ? AlundraYarnFunctions.FalconTemp : null;
                return isFirstFalconOfPage;
            case '1':
                functionName = AlundraYarnFunctions.Falcon;
                return true;
            case '2':
            case '4':
                functionName = isFirstFalconOfPage
                    ? AlundraYarnFunctions.CategoryItemNameBefore
                    : AlundraYarnFunctions.CategoryItemName;
                return true;
            case '3':
                functionName = AlundraYarnFunctions.CategoryThreshold;
                return true;
            case '5':
                functionName = AlundraYarnFunctions.CategoryRemaining;
                return true;
            default:
                // Unreachable: TokenizePages only ever produces a FalconToken for '0'-'5'.
                functionName = null;
                return false;
        }
    }

    /// <summary>
    /// D-E15-8: Yarn drops the spaces at the edges of a compiled line; this reproduces the same
    /// visible result deterministically instead of relying on Yarn's own trimming, by removing U+0020
    /// spaces from the start while the leading elements are text, and from the end while the trailing
    /// elements are text. A line break is never removed, so a leading or trailing <c>\N</c> stays a
    /// leading or trailing marker. The <c>flag</c> and <c>yield</c> markers are transparent (F0-R2):
    /// the walk goes through them, so a space between the last visible unit and a flag is still removed.
    /// Exception (D-E19-78, D-E19-84): with <paramref name="keepTrailingSpaces"/> the trailing walk is
    /// skipped (the page's last line is centred), so every trailing space stays, flag or yield markers
    /// included.
    /// </summary>
    private static void TrimEdgeSpaces(List<Element> elements, bool keepTrailingSpaces)
    {
        var head = 0;
        while (head < elements.Count)
        {
            if (IsPositionMarker(elements[head]))
            {
                head++;
                continue;
            }

            if (elements[head] is not TextElement text)
            {
                break;
            }

            var trimmed = text.Text.TrimStart(' ');
            if (trimmed.Length == 0)
            {
                elements.RemoveAt(head);
                continue;
            }

            elements[head] = new TextElement(trimmed);
            break;
        }

        var tail = keepTrailingSpaces ? -1 : elements.Count - 1;
        while (tail >= 0)
        {
            if (IsPositionMarker(elements[tail]))
            {
                tail--;
                continue;
            }

            if (elements[tail] is not TextElement text)
            {
                break;
            }

            var trimmed = text.Text.TrimEnd(' ');
            if (trimmed.Length == 0)
            {
                elements.RemoveAt(tail);
                tail--;
                continue;
            }

            elements[tail] = new TextElement(trimmed);
            break;
        }
    }

    /// <summary>
    /// Whether the last line of the page (the elements after its last <c>\N</c>, or the whole page) holds a
    /// <c>\H</c> centring marker (D-E19-78): the original's centring width counts the spaces that end it.
    /// </summary>
    private static bool LastLineIsCentred(List<Element> elements)
    {
        for (var index = elements.Count - 1; index >= 0; index--)
        {
            switch (elements[index])
            {
                case CenterElement:
                    return true;
                case LineBreakElement:
                    return false;
            }
        }

        return false;
    }

    private static bool IsPositionMarker(Element element) => element is FlagElement or YieldElement;

    private static string RenderElements(
        List<Element> elements, out int glyphCount, out int flagCount, out int yieldCount, out int functionCallCount)
    {
        glyphCount = 0;
        flagCount = 0;
        yieldCount = 0;
        functionCallCount = 0;
        var builder = new StringBuilder();

        foreach (var element in elements)
        {
            switch (element)
            {
                case TextElement text:
                    builder.Append(EscapeText(text.Text));
                    break;
                case LineBreakElement:
                    builder.Append("[br trimwhitespace=false/]");
                    break;
                case VoiceElement voice:
                    builder.Append("[voice id=").Append(voice.Id).Append(" trimwhitespace=false/]");
                    break;
                case CenterElement:
                    builder.Append("[center trimwhitespace=false/]");
                    break;
                case SlowElement:
                    builder.Append("[slow trimwhitespace=false/]");
                    break;
                case GlyphElement glyph:
                    builder.Append("[glyph id=").Append(glyph.Id).Append(" trimwhitespace=false/]");
                    glyphCount++;
                    break;
                case FlagElement flag:
                    builder.Append("[flag id=").Append(flag.Id.ToString(CultureInfo.InvariantCulture)).Append(" trimwhitespace=false/]");
                    flagCount++;
                    break;
                case YieldElement:
                    builder.Append("[yield trimwhitespace=false/]");
                    yieldCount++;
                    break;
                case FunctionCallElement call:
                    builder.Append('{').Append(call.Name).Append('(');
                    if (call.Argument is not null)
                    {
                        builder.Append(call.Argument);
                    }

                    builder.Append(")}");
                    functionCallCount++;
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// §1: ':' and '#' are escaped so Yarn's <c>LineParser</c> does not read the start of the line as
    /// a character name, or the compiler a <c>#Disuse</c> placeholder as a hashtag to strip; '[', ']',
    /// '{', '}', '/' and '&lt;'/'&gt;' are escaped so a literal one is never mistaken for markup, a
    /// substitution, a comment ("//") or a command ("&lt;&lt;"/"&gt;&gt;") - proved by
    /// <c>YarnTextEmitterTests</c> compiling and parsing each one back to its own character. None of
    /// these occur in the corpus except '#' inside <c>#Disuse</c> (D-E15-7) and a lone '/' in two
    /// places, but escaping every occurrence, alone or paired, is simpler than detecting which need it
    /// and no less correct.
    /// </summary>
    private static string EscapeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case ':':
                    builder.Append("\\:");
                    break;
                case '#':
                    builder.Append("\\#");
                    break;
                case '[':
                    builder.Append("\\[");
                    break;
                case ']':
                    builder.Append("\\]");
                    break;
                case '{':
                    builder.Append("\\{");
                    break;
                case '}':
                    builder.Append("\\}");
                    break;
                case '/':
                    builder.Append("\\/");
                    break;
                case '<':
                    builder.Append("\\<");
                    break;
                case '>':
                    builder.Append("\\>");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
