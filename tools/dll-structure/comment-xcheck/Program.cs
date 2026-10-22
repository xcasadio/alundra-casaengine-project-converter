// R8 (docs/plan-restructuration-dll.md, section 13): the independent reader that backs prove_r8.py (the lexer of R2 is the first one).
// equiv <folderA> <folderB> <list>: for every file of the list, the stream of tokens and of non-comment trivia is the same in A and in B, both with and
// without the DEBUG symbol; only comments may differ.  Without the 'equiv' word: <repo> <list> <out.tsv> lists the comment trivia and the Alundra* tokens.
// Read-only.  Build: dotnet restore --source C:/Users/casad/.nuget/packages ; dotnet build -c Debug.
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

if (args[0] == "equiv")
{
    // equiv <repoA> <dirB> <list> : for every file, the syntax tree of A and of B (both preprocessor settings) must be equivalent ignoring trivia
    var ra = args[1]; var rb = args[2]; var lst = args[3];
    int n = 0, bad = 0, ctext = 0;
    foreach (var rel0 in File.ReadAllLines(lst))
    {
        if (string.IsNullOrWhiteSpace(rel0)) continue;
        var ta = File.ReadAllText(Path.Combine(ra, rel0));
        var tb = File.ReadAllText(Path.Combine(rb, rel0));
        n++;
        if (ta != tb) ctext++;
        foreach (var o in new[] { new CSharpParseOptions(LanguageVersion.Latest), new CSharpParseOptions(LanguageVersion.Latest).WithPreprocessorSymbols("DEBUG") })
        {
            var xa = CSharpSyntaxTree.ParseText(ta, o).GetRoot();
            var xb = CSharpSyntaxTree.ParseText(tb, o).GetRoot();
            // token stream (kind, text) and non-comment trivia stream (kind, text) must be identical; comment trivia is the only thing allowed to differ
            static List<string> Stream(SyntaxNode r)
            {
                var l = new List<string>();
                foreach (var tk in r.DescendantTokens(descendIntoTrivia: false))
                {
                    foreach (var tv in tk.LeadingTrivia) AddTrivia(l, tv);
                    l.Add("T:" + (int)tk.Kind() + ":" + tk.Text);
                    foreach (var tv in tk.TrailingTrivia) AddTrivia(l, tv);
                }
                return l;
            }
            static void AddTrivia(List<string> l, SyntaxTrivia tv)
            {
                var k = tv.Kind();
                if (k == SyntaxKind.SingleLineCommentTrivia || k == SyntaxKind.MultiLineCommentTrivia || k == SyntaxKind.SingleLineDocumentationCommentTrivia || k == SyntaxKind.MultiLineDocumentationCommentTrivia) { l.Add("C"); return; }
                l.Add("V:" + (int)k + ":" + tv.ToString());
            }
            var sa = Stream(xa); var sb2 = Stream(xb);
            if (!sa.SequenceEqual(sb2)) { bad++; Console.WriteLine("TOKEN/TRIVIA STREAM DIFFERS: " + rel0); break; }
        }
    }
    Console.WriteLine($"equiv: files {n}, textually different {ctext}, not equivalent {bad}");
    return;
}
var repo = args[0];
var listPath = args[1];
var outPath = args[2];
var rx = new Regex(@"(?<![A-Za-z0-9_])I?Alundra[A-Za-z0-9_]*", RegexOptions.Compiled);
var optsNo = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
var optsDbg = optsNo.WithPreprocessorSymbols("DEBUG");

static string ElemName(SyntaxNode attr)
{
    for (var n = attr.Parent; n != null; n = n.Parent)
    {
        if (n is XmlElementSyntax e) return e.StartTag.Name.ToString();
        if (n is XmlEmptyElementSyntax ee) return ee.Name.ToString();
    }
    return "?";
}

static string Ctx(SyntaxToken tok)
{
    for (var n = tok.Parent; n != null; n = n.Parent)
    {
        switch (n)
        {
            case XmlCrefAttributeSyntax a: return "attr/" + ElemName(a) + "/cref";
            case XmlNameAttributeSyntax a: return "attr/" + ElemName(a) + "/name";
            case XmlTextAttributeSyntax a: return "attr/" + ElemName(a) + "/" + a.Name.ToString();
            case XmlElementSyntax e: return "in/" + e.StartTag.Name.ToString();
            case XmlEmptyElementSyntax e: return "in/" + e.Name.ToString();
            case DocumentationCommentTriviaSyntax: return "text";
        }
    }
    return "text";
}

var rxAny = new Regex(@"[A-Za-z0-9_]*Alundra[A-Za-z0-9_]*", RegexOptions.Compiled);
var sbCode = new StringBuilder();
var sb = new StringBuilder();
int nfiles = 0, ncomments = 0, nmatches = 0, nparseerr = 0;
foreach (var rel in File.ReadAllLines(listPath))
{
    if (string.IsNullOrWhiteSpace(rel)) continue;
    var path = Path.Combine(repo, rel);
    var bytes = File.ReadAllBytes(path);
    string text;
    using (var ms = new MemoryStream(bytes))
    using (var sr = new StreamReader(ms, new UTF8Encoding(false), true))
        text = sr.ReadToEnd();
    var st = SourceText.From(text);
    var seen = new HashSet<int>();
    var seenTok = new HashSet<int>();
    foreach (var opts in new[] { optsNo, optsDbg })
    {
        var tree = CSharpSyntaxTree.ParseText(st, opts);
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)) nparseerr++;
        var root = tree.GetRoot();
        foreach (var tk in root.DescendantTokens(descendIntoTrivia: false))
        {
            var kk = tk.Kind();
            string cls;
            if (kk == SyntaxKind.IdentifierToken) cls = "code";
            else if (kk == SyntaxKind.StringLiteralToken || kk == SyntaxKind.InterpolatedStringTextToken || kk == SyntaxKind.SingleLineRawStringLiteralToken || kk == SyntaxKind.MultiLineRawStringLiteralToken || kk == SyntaxKind.Utf8StringLiteralToken || kk == SyntaxKind.Utf8SingleLineRawStringLiteralToken || kk == SyntaxKind.Utf8MultiLineRawStringLiteralToken || kk == SyntaxKind.InterpolatedStringStartToken) cls = "str";
            else continue;
            if (!seenTok.Add(tk.SpanStart)) continue;
            var ts = tk.ToString();
            foreach (Match m in rxAny.Matches(ts))
            {
                int abs = tk.SpanStart + m.Index;
                int line = st.Lines.GetLinePosition(abs).Line + 1;
                sbCode.Append(rel).Append('\t').Append(line).Append('\t').Append(cls).Append('\t').Append(m.Value).Append('\n');
            }
        }
        foreach (var tr in root.DescendantTrivia(descendIntoTrivia: false))
        {
            var k = tr.Kind();
            bool doc = k == SyntaxKind.SingleLineDocumentationCommentTrivia || k == SyntaxKind.MultiLineDocumentationCommentTrivia;
            bool plain = k == SyntaxKind.SingleLineCommentTrivia || k == SyntaxKind.MultiLineCommentTrivia;
            if (!doc && !plain) continue;
            if (!seen.Add(tr.SpanStart)) continue;
            ncomments++;
            var s = tr.ToString();
            foreach (Match m in rx.Matches(s))
            {
                int abs = tr.SpanStart + m.Index;
                int line = st.Lines.GetLinePosition(abs).Line + 1;
                string ctx = "plain";
                if (doc)
                {
                    var structure = tr.GetStructure()!;
                    var tok = structure.FindToken(abs, findInsideTrivia: false);
                    ctx = Ctx(tok);
                }
                sb.Append(rel).Append('\t').Append(line).Append('\t').Append(doc ? "doc" : "plain").Append('\t').Append(ctx).Append('\t').Append(m.Value).Append('\n');
                nmatches++;
            }
        }
    }
    nfiles++;
}
File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
File.WriteAllText(outPath + ".code", sbCode.ToString(), new UTF8Encoding(false));
Console.WriteLine($"files {nfiles}, comments {ncomments}, matches {nmatches}, files with parse errors {nparseerr}");
