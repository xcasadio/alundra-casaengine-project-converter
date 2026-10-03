using System.Globalization;
using System.Text;

namespace AlundraCasaEngineProjectConverter.Tests.Text;

/// <summary>
/// Thrown by <see cref="ReferenceTextDecoder"/> for any source that falls outside the corpus's own
/// shape (a code the corpus never uses, a missing operand, a lone trailing backslash, ...). The
/// message always names the offending code, the index of the page it was found in and that page's
/// own source (not the whole string), so a real occurrence is easy to locate.
/// </summary>
public sealed class ReferenceTextDecoderException : Exception
{
    public ReferenceTextDecoderException(string message) : base(message)
    {
    }

    public ReferenceTextDecoderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// One command executed before a page's line is delivered: <c>falcon_update</c> (the single command a
/// page holding any <c>\X</c> gets). Numeric codes and <c>\Y</c> are no longer commands (D-E19-48,
/// ADR-0025): they are the positioned <c>flag</c> and <c>yield</c> markers of <see cref="ReferenceMarker"/>.
/// </summary>
public sealed record ReferenceCommand(string Name, IReadOnlyList<string> Arguments)
{
    public static readonly ReferenceCommand FalconUpdate = new("falcon_update", Array.Empty<string>());

    public string ToCanonicalString()
        => Arguments.Count == 0 ? Name : $"{Name}({string.Join(",", Arguments)})";
}

/// <summary>
/// One function call in a page's text, in text order: <c>game_var</c> or one of the six falcon/
/// category functions of ADR-0007.
/// </summary>
public sealed record ReferenceCall(string Name, IReadOnlyList<string> Arguments)
{
    public static ReferenceCall GameVar(int n)
        => new("game_var", new[] { n.ToString(CultureInfo.InvariantCulture) });

    public static readonly ReferenceCall FalconTemp = new("falcon_temp", Array.Empty<string>());
    public static readonly ReferenceCall Falcon = new("falcon", Array.Empty<string>());
    public static readonly ReferenceCall CategoryThreshold = new("category_threshold", Array.Empty<string>());
    public static readonly ReferenceCall CategoryRemaining = new("category_remaining", Array.Empty<string>());
    public static readonly ReferenceCall CategoryItemName = new("category_item_name", Array.Empty<string>());
    public static readonly ReferenceCall CategoryItemNameBefore = new("category_item_name_before", Array.Empty<string>());

    public string ToCanonicalString()
        => Arguments.Count == 0 ? Name : $"{Name}({string.Join(",", Arguments)})";
}

/// <summary>
/// One marker in a page's text, at its position (an index into <see cref="ReferencePage.Text"/>,
/// UTF-16 code units). Several markers can share a position; the decoder keeps them in source order.
/// </summary>
public sealed record ReferenceMarker(string Name, int Position, IReadOnlyDictionary<string, string> Properties)
{
    public static ReferenceMarker Voice(int id, int position)
        => new("voice", position, Props(("id", id.ToString(CultureInfo.InvariantCulture))));

    public static ReferenceMarker Center(int position) => new("center", position, Props());

    public static ReferenceMarker Slow(int position) => new("slow", position, Props());

    public static ReferenceMarker Glyph(int id, int position)
        => new("glyph", position, Props(("id", id.ToString(CultureInfo.InvariantCulture))));

    public static ReferenceMarker Empty(int position) => new("empty", position, Props());

    /// <summary>A numeric code <c>\&lt;digits&gt;</c>: the flag it sets, at its text position (D-E19-48).</summary>
    public static ReferenceMarker Flag(int id, int position)
        => new("flag", position, Props(("id", id.ToString(CultureInfo.InvariantCulture))));

    /// <summary>A <c>\Y</c>: ends the render step, at its text position (D-E19-48).</summary>
    public static ReferenceMarker Yield(int position) => new("yield", position, Props());

    private static IReadOnlyDictionary<string, string> Props(params (string Key, string Value)[] entries)
    {
        var dictionary = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in entries)
        {
            dictionary[key] = value;
        }

        return dictionary;
    }

    public string ToCanonicalString()
    {
        var builder = new StringBuilder();
        builder.Append(Name).Append('@').Append(Position.ToString(CultureInfo.InvariantCulture));
        foreach (var property in Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(' ').Append(property.Key).Append('=').Append(property.Value);
        }

        return builder.ToString();
    }
}

/// <summary>
/// The oracle quadruple for one page of a source string, as the original decoder
/// (<c>TextDecoder.cs</c>'s <c>TextInterpreter</c>) would render it: visible text, markers in source
/// order, commands run before the line, and function calls in text order. Records/lists here do not
/// carry value equality on their own - compare pages through <see cref="ToCanonicalString"/>.
/// </summary>
public sealed class ReferencePage
{
    public string Text { get; }
    public IReadOnlyList<ReferenceMarker> Markers { get; }
    public IReadOnlyList<ReferenceCommand> Commands { get; }
    public IReadOnlyList<ReferenceCall> Calls { get; }

    public ReferencePage(
        string text,
        IReadOnlyList<ReferenceMarker> markers,
        IReadOnlyList<ReferenceCommand> commands,
        IReadOnlyList<ReferenceCall> calls)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Markers = markers ?? throw new ArgumentNullException(nameof(markers));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Calls = calls ?? throw new ArgumentNullException(nameof(calls));
    }

    /// <summary>
    /// Deterministic string form of all four parts, for T6 to diff pages as strings: newlines in
    /// <see cref="Text"/> are shown as the two characters "\n" rather than a real line break, markers
    /// carry their properties sorted by key, commands and calls are shown in list order.
    /// </summary>
    public string ToCanonicalString()
    {
        var builder = new StringBuilder();
        builder.Append("text=").Append(Text.Replace("\n", "\\n", StringComparison.Ordinal));
        builder.Append("; markers=[").Append(string.Join(", ", Markers.Select(m => m.ToCanonicalString()))).Append(']');
        builder.Append("; commands=[").Append(string.Join(", ", Commands.Select(c => c.ToCanonicalString()))).Append(']');
        builder.Append("; calls=[").Append(string.Join(", ", Calls.Select(c => c.ToCanonicalString()))).Append(']');
        return builder.ToString();
    }
}

/// <summary>
/// Reference decoder of the original Alundra text control codes, following
/// <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs</c>'s
/// <c>TextInterpreter</c> (page/line codes) and <c>UpdatePlayerProgressState</c> (the <c>\X</c>
/// codes, per ADR-0007), plus the page-oriented contract of docs/plan-e15-yarn.md ("E15.b", T2): one
/// <see cref="ReferencePage"/> oracle quadruple per <c>\A</c>-separated page of a source string.
///
/// This is the "original side" of the equivalence oracle T6 will build: it must never reference the
/// Yarn emitter (alundra-casaengine-project-converter/Text/) or share any code with it, so a bug
/// shared by both sides cannot cancel out.
/// </summary>
public static class ReferenceTextDecoder
{
    public const int FalconTempValue = 90001;
    public const int FalconValue = 90002;
    public const int CategoryThresholdValue = 90003;
    public const int CategoryRemainingValue = 90004;
    public const int GameVarBase = 91000;
    public const string CategoryItemNameWitness = "⟦objet⟧";
    public const string CategoryItemNameBeforeWitness = "⟦objet d'avant⟧";

    public static IReadOnlyList<ReferencePage> Decode(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            throw new ArgumentException("Source text is null or empty; an empty slot has no node.", nameof(source));
        }

        var pageBuilders = new List<PageBuilder>();
        var current = new PageBuilder();
        pageBuilders.Add(current);
        var location = new PageLocation(source, 0, 0);

        var i = 0;
        var length = source.Length;
        while (i < length)
        {
            var c = source[i];

            if (c == '\\')
            {
                if (i + 1 >= length)
                {
                    throw new ReferenceTextDecoderException(
                        $"Lone trailing backslash at the end of {location.Describe()}.");
                }

                var code = source[i + 1];

                if (code is >= '0' and <= '9')
                {
                    var digitsStart = i + 1;
                    var j = digitsStart;
                    while (j < length && source[j] is >= '0' and <= '9')
                    {
                        j++;
                    }

                    var digits = source.Substring(digitsStart, j - digitsStart);
                    int value;
                    try
                    {
                        value = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
                    }
                    catch (OverflowException ex)
                    {
                        throw new ReferenceTextDecoderException(
                            $"Numeric code overflow '\\{digits}' in {location.Describe()}.", ex);
                    }

                    current.AddElement(Element.MarkerAt(ReferenceMarker.Flag(value, 0)));
                    i = j;
                    continue;
                }

                switch (code)
                {
                    case 'A':
                        current.Flush();
                        current = new PageBuilder();
                        pageBuilders.Add(current);
                        location = new PageLocation(source, i + 2, location.Index + 1);
                        i += 2;
                        continue;

                    case 'N':
                        current.AddElement(Element.LineBreak());
                        i += 2;
                        continue;

                    case 'Y':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Yield(0)));
                        i += 2;
                        continue;

                    case 'B':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(-1, 0)));
                        i += 2;
                        continue;

                    case 'C':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(0, 0)));
                        i += 2;
                        continue;

                    case 'D':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(1, 0)));
                        i += 2;
                        continue;

                    case 'E':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(2, 0)));
                        i += 2;
                        continue;

                    case 'F':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(3, 0)));
                        i += 2;
                        continue;

                    case 'G':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Voice(4, 0)));
                        i += 2;
                        continue;

                    case 'H':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Center(0)));
                        i += 2;
                        continue;

                    case 'T':
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Slow(0)));
                        i += 2;
                        continue;

                    case 'W':
                    {
                        if (i + 2 >= length)
                        {
                            throw new ReferenceTextDecoderException(
                                $"Missing \\W operand in {location.Describe()}.");
                        }

                        var operand = source[i + 2];
                        var glyphId = ComputeGlyphId(operand, location);
                        current.AddElement(Element.MarkerAt(ReferenceMarker.Glyph(glyphId, 0)));
                        i += 3;
                        continue;
                    }

                    case 'V':
                    {
                        if (i + 2 >= length)
                        {
                            throw new ReferenceTextDecoderException(
                                $"Missing \\V operand in {location.Describe()}.");
                        }

                        var operand = source[i + 2];
                        if (operand is < '0' or > '9')
                        {
                            throw new ReferenceTextDecoderException(
                                $"Invalid \\V operand '{operand}' in {location.Describe()}.");
                        }

                        var n = operand - '0';
                        current.AddElement(Element.CallAt(
                            ReferenceCall.GameVar(n),
                            (GameVarBase + n).ToString(CultureInfo.InvariantCulture)));
                        i += 3;
                        continue;
                    }

                    case 'X':
                    {
                        var operand = i + 2 < length ? source[i + 2] : (char?)null;
                        HandleFalconCode(current, operand, location);
                        i += 3;
                        continue;
                    }

                    default:
                        throw new ReferenceTextDecoderException(
                            $"Unsupported code '\\{code}' in {location.Describe()}.");
                }
            }

            if (c is '{' or '}')
            {
                throw new ReferenceTextDecoderException(
                    $"Unsupported code '{c}' in {location.Describe()}.");
            }

            if (c < 0x20)
            {
                if (c == '\u001A')
                {
                    current.AddElement(Element.MarkerAt(ReferenceMarker.Glyph(26, 0)));
                    i += 1;
                    continue;
                }

                if (c == '\u001C')
                {
                    current.AddElement(Element.MarkerAt(ReferenceMarker.Glyph(28, 0)));
                    i += 1;
                    continue;
                }

                throw new ReferenceTextDecoderException(
                    $"Raw control character U+{(int)c:X4} in {location.Describe()}.");
            }

            current.AddText(c);
            i += 1;
        }

        current.Flush();

        var pages = new List<ReferencePage>(pageBuilders.Count);
        foreach (var builder in pageBuilders)
        {
            pages.Add(builder.Build());
        }

        return pages;
    }

    private static void HandleFalconCode(PageBuilder page, char? operand, PageLocation location)
    {
        if (operand is not ('0' or '1' or '2' or '3' or '4' or '5'))
        {
            throw new ReferenceTextDecoderException(
                $"Invalid \\X operand '{(operand?.ToString() ?? "<missing>")}' in {location.Describe()}.");
        }

        if (!page.FalconUpdateEmitted)
        {
            page.Commands.Add(ReferenceCommand.FalconUpdate);
            page.FalconUpdateEmitted = true;
        }

        switch (operand.Value)
        {
            case '0':
                if (page.FalconStateUpdated)
                {
                    throw new ReferenceTextDecoderException(
                        $"\\X0 after another \\X already updated the falcon state in {location.Describe()}.");
                }

                page.AddElement(Element.CallAt(
                    ReferenceCall.FalconTemp, FalconTempValue.ToString(CultureInfo.InvariantCulture)));
                page.FalconStateUpdated = true;
                break;

            case '2':
            case '4':
                page.AddElement(page.FalconStateUpdated
                    ? Element.CallAt(ReferenceCall.CategoryItemName, CategoryItemNameWitness)
                    : Element.CallAt(ReferenceCall.CategoryItemNameBefore, CategoryItemNameBeforeWitness));
                page.FalconStateUpdated = true;
                break;

            case '1':
                page.FalconStateUpdated = true;
                page.AddElement(Element.CallAt(ReferenceCall.Falcon, FalconValue.ToString(CultureInfo.InvariantCulture)));
                break;

            case '3':
                page.FalconStateUpdated = true;
                page.AddElement(Element.CallAt(
                    ReferenceCall.CategoryThreshold, CategoryThresholdValue.ToString(CultureInfo.InvariantCulture)));
                break;

            case '5':
                page.FalconStateUpdated = true;
                page.AddElement(Element.CallAt(
                    ReferenceCall.CategoryRemaining, CategoryRemainingValue.ToString(CultureInfo.InvariantCulture)));
                break;
        }
    }

    private static int ComputeGlyphId(char operand, PageLocation location)
    {
        // ALUN_CD.EXE @ 0x800462e0-0x800462f0: id = c - 0x20 for c <= 0x40 (digits), c - 0x27 for
        // c > 0x40 (uppercase letters). TextDecoder.cs:442-443 drops the letter branch; this follows
        // the executable, per ADR-0006's closing bullet.
        if (operand is >= '0' and <= '9')
        {
            return operand - 0x20;
        }

        if (operand is >= 'A' and <= 'Z')
        {
            return operand - 0x27;
        }

        throw new ReferenceTextDecoderException(
            $"Invalid \\W operand '{operand}' in {location.Describe()}.");
    }

    /// <summary>
    /// Where the decoder currently is, for error messages only: the page's index and the start of
    /// its source. <see cref="Describe"/> quotes that page alone, not the whole string.
    /// </summary>
    private readonly record struct PageLocation(string Source, int Start, int Index)
    {
        public string Describe()
        {
            var end = Source.IndexOf("\\A", Start, StringComparison.Ordinal);
            var page = Source[Start..(end < 0 ? Source.Length : end)];
            return $"page {Index} '{page.Replace("\n", "\\n", StringComparison.Ordinal)}'";
        }
    }

    /// <summary>
    /// The page's own element stream while it is being built: text runs (merged char by char),
    /// line breaks, markers and function calls, in source order - exactly the sequence D-E15-8's
    /// edge-space rule is defined over. Numeric codes and <c>\Y</c> enter it as <c>flag</c> and
    /// <c>yield</c> markers (D-E19-48), but are transparent to the edge-space trim and to the empty-page
    /// check (F0-R2): a space between the last visible unit and a flag is still removed, and a page
    /// holding only flags and yields is still empty.
    /// </summary>
    private sealed class PageBuilder
    {
        public List<Element> Elements { get; } = new();
        public List<ReferenceCommand> Commands { get; } = new();
        public bool FalconStateUpdated { get; set; }
        public bool FalconUpdateEmitted { get; set; }

        private readonly StringBuilder _textBuffer = new();

        public void AddText(char c) => _textBuffer.Append(c);

        public void AddElement(Element element)
        {
            Flush();
            Elements.Add(element);
        }

        public void Flush()
        {
            if (_textBuffer.Length > 0)
            {
                Elements.Add(Element.TextRun(_textBuffer.ToString()));
                _textBuffer.Clear();
            }
        }

        public ReferencePage Build()
        {
            var elements = new List<Element>(Elements);

            // D-E15-8: only U+0020 spaces are removed, only from the sequence's two edges, only
            // while the leading/trailing element is plain visible text - never past a line break,
            // marker, glyph or function call, and a line break itself is never removed.
            // flag and yield markers are transparent (F0-R2): the walk goes through them. A text run
            // that strips down to nothing stays as an empty run, which adds nothing to the text.
            for (var k = 0; k < elements.Count; k++)
            {
                if (IsTransparent(elements[k]))
                {
                    continue;
                }

                if (elements[k].Kind != ElementKind.Text)
                {
                    break;
                }

                var stripped = elements[k].Text!.TrimStart(' ');
                elements[k] = Element.TextRun(stripped);
                if (stripped.Length > 0)
                {
                    break;
                }
            }

            for (var k = elements.Count - 1; k >= 0; k--)
            {
                if (IsTransparent(elements[k]))
                {
                    continue;
                }

                if (elements[k].Kind != ElementKind.Text)
                {
                    break;
                }

                var stripped = elements[k].Text!.TrimEnd(' ');
                elements[k] = Element.TextRun(stripped);
                if (stripped.Length > 0)
                {
                    break;
                }
            }

            var textBuilder = new StringBuilder();
            var markers = new List<ReferenceMarker>();
            var calls = new List<ReferenceCall>();

            foreach (var element in elements)
            {
                switch (element.Kind)
                {
                    case ElementKind.Text:
                        textBuilder.Append(element.Text);
                        break;

                    case ElementKind.LineBreak:
                        textBuilder.Append('\n');
                        break;

                    case ElementKind.Marker:
                        markers.Add(element.Marker! with { Position = textBuilder.Length });
                        break;

                    case ElementKind.Call:
                        calls.Add(element.Call!);
                        textBuilder.Append(element.Witness);
                        break;
                }
            }

            var text = textBuilder.ToString();
            if (text.Length == 0 && calls.Count == 0 && markers.All(m => m.Name is "flag" or "yield"))
            {
                markers.Add(ReferenceMarker.Empty(0));
            }

            return new ReferencePage(text, markers, new List<ReferenceCommand>(Commands), calls);
        }
    }

    private static bool IsTransparent(Element element)
        => element.Kind == ElementKind.Marker && element.Marker!.Name is "flag" or "yield";

    private enum ElementKind
    {
        Text,
        LineBreak,
        Marker,
        Call,
    }

    /// <summary>
    /// One item of a page's pre-trim element stream. A tiny closed union (kept private - T6 and
    /// callers only ever see the finished <see cref="ReferencePage"/>) rather than a class hierarchy,
    /// since every case is a handful of fields and the switch above is exhaustive.
    /// </summary>
    private sealed class Element
    {
        public ElementKind Kind { get; private init; }
        public string? Text { get; private init; }
        public ReferenceMarker? Marker { get; private init; }
        public ReferenceCall? Call { get; private init; }
        public string? Witness { get; private init; }

        public static Element TextRun(string text) => new() { Kind = ElementKind.Text, Text = text };
        public static Element LineBreak() => new() { Kind = ElementKind.LineBreak };
        public static Element MarkerAt(ReferenceMarker marker) => new() { Kind = ElementKind.Marker, Marker = marker };

        public static Element CallAt(ReferenceCall call, string witness)
            => new() { Kind = ElementKind.Call, Call = call, Witness = witness };
    }
}
