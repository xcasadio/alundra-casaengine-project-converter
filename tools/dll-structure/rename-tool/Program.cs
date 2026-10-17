// R3 rename tool (docs/plan-restructuration-dll.md, section 7, T0 (a)).
//
//   RenameTool <repo root> <Domain> [--dry-run] [--table <table.json>]
//   RenameTool <repo root> --rename Old=New [--rename Old2=New2 ...] [--dry-run]      (proofs: explicit pairs instead of a domain)
//
// Opens Alundra.Tests/Alundra.Tests.csproj (which loads Alundra) and tools/AlundraTestSaves/AlundraTestSaves.csproj in one workspace.
// For a domain of table.json (old name = stage r2, new name = stage r3), it records every reference of every type to rename, renames the
// types with Roslyn (conflicts resolved by Roslyn, nothing renamed in comments or strings), then checks:
//   - every recorded reference now binds to the renamed type, under its new name;
//   - no other identifier changes its binding (the bound symbol of every identifier token of every touched file, before and after);
//   - the only tokens added are namespace qualifiers (or `global`), and no token is removed;
//   - no new compilation error.
// At the slightest deviation the domain is refused: exit 1, nothing written. Otherwise the changed files are written back with their
// line endings and BOM kept. Exit 2 on a usage or loading error.
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

static class Program
{
    static readonly SymbolDisplayFormat Fmt = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    static readonly List<string> Problems = new();
    static void Bad(string msg) { if (Problems.Count < 400) Problems.Add(msg); else if (Problems.Count == 400) Problems.Add("... (more)"); }

    static int Main(string[] args)
    {
        string? root = null, domain = null, tablePath = null;
        bool dry = false;
        var explicitPairs = new List<(string Old, string New)>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dry-run": dry = true; break;
                case "--table": tablePath = args[++i]; break;
                case "--rename":
                    var kv = args[++i].Split('=');
                    if (kv.Length != 2) return Usage();
                    explicitPairs.Add((kv[0], kv[1]));
                    break;
                default:
                    if (root == null) root = args[i]; else if (domain == null) domain = args[i]; else return Usage();
                    break;
            }
        }
        if (root == null || (domain == null) == (explicitPairs.Count == 0)) return Usage();
        root = Path.GetFullPath(root);
        tablePath ??= Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "table.json");

        List<(string Old, string New)> pairs;
        if (domain != null)
        {
            try { pairs = DomainPairs(tablePath, domain); }
            catch (Exception e) { Console.Error.WriteLine("table: " + e.Message); return 2; }
            if (pairs.Count == 0) { Console.WriteLine($"{domain}: no type to rename."); return 0; }
        }
        else pairs = explicitPairs;

        var dupNew = pairs.GroupBy(p => p.New).Where(g => g.Count() > 1).ToList();
        // (a fabricated insoluble conflict is detected by the compiler check below, not here)

        var inst = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(i => i.Version).ToList();
        var pick = inst.FirstOrDefault(i => i.Version.Major == 9) ?? inst.FirstOrDefault();
        if (pick == null) { Console.Error.WriteLine("no MSBuild instance found"); return 2; }
        MSBuildLocator.RegisterInstance(pick);
        Console.WriteLine($"msbuild {pick.Version}; {pairs.Count} type(s) to rename");
        return Run(root, domain ?? "(explicit)", pairs, dry).GetAwaiter().GetResult();
    }

    static int Usage()
    {
        Console.Error.WriteLine("usage: RenameTool <repo root> <Domain> [--dry-run] [--table <table.json>]\n       RenameTool <repo root> --rename Old=New [--rename ...] [--dry-run]");
        return 2;
    }

    static string DomainOfPath(string p)
    {
        var parts = p.Split('/');
        return parts.Length > 2 ? parts[1] : "";
    }

    static List<(string, string)> DomainPairs(string tablePath, string domain)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(tablePath, Encoding.UTF8));
        var res = new List<(string, string)>();
        foreach (var r in doc.RootElement.GetProperty("types").EnumerateArray())
        {
            string o = r.GetProperty("r2").GetProperty("name").GetString()!, n = r.GetProperty("r3").GetProperty("name").GetString()!;
            if (o == n) continue;
            string f = r.GetProperty("r2").GetProperty("file").GetString()!;
            string d = f.StartsWith("Alundra/Scripts/") ? DomainOfPath(r.GetProperty("final").GetProperty("file").GetString()!) : DomainOfPath(f);
            if (d == domain && !res.Contains((o, n))) res.Add((o, n));
        }
        return res;
    }

    static string Desc(ISymbol? s)
    {
        if (s == null) return "none";
        string d = s.Kind + ":" + s.ToDisplayString(Fmt);
        if (s.Kind is SymbolKind.Local or SymbolKind.Parameter or SymbolKind.RangeVariable or SymbolKind.Label or SymbolKind.TypeParameter or SymbolKind.Discard)
            d += "@" + (s.ContainingSymbol?.ToDisplayString(Fmt) ?? "");
        return d;
    }

    static string TokenDesc(SemanticModel m, SyntaxToken t)
    {
        var n = t.Parent;
        if (n == null || n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlNameSyntax || n is Microsoft.CodeAnalysis.CSharp.Syntax.XmlPrefixSyntax) return "-";
        try
        {
            var decl = m.GetDeclaredSymbol(n);
            if (decl != null) return Desc(decl);
            var info = m.GetSymbolInfo(n);
            if (info.Symbol != null) return Desc(info.Symbol);
            if (info.CandidateSymbols.Length > 0)
                return "cand(" + string.Join("|", info.CandidateSymbols.Select(Desc).OrderBy(x => x, StringComparer.Ordinal)) + ")";
            return "none";
        }
        catch (Exception) { return "?"; }
    }

    static bool IsOurs(string root, Project p)
    {
        var f = p.FilePath;
        if (f == null) return false;
        var dir = Path.GetDirectoryName(Path.GetFullPath(f))!;
        return string.Equals(dir, Path.Combine(root, "Alundra"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(dir, Path.Combine(root, "Alundra.Tests"), StringComparison.OrdinalIgnoreCase)
            || string.Equals(dir, Path.Combine(root, "tools", "AlundraTestSaves"), StringComparison.OrdinalIgnoreCase);
    }

    static async Task<INamedTypeSymbol?> FindType(Solution sol, Project alundra, string name)
    {
        var comp = await sol.GetProject(alundra.Id)!.GetCompilationAsync();
        var found = comp!.GetSymbolsWithName(n => n == name, SymbolFilter.Type).OfType<INamedTypeSymbol>().Where(t => t.ContainingType == null).ToList();
        if (found.Count == 1) return found[0];
        Bad($"type {name}: {found.Count} top-level declaration(s) found (expected exactly one)");
        return null;
    }

    static async Task<Dictionary<string, int>> Errors(Solution sol, IEnumerable<ProjectId> ids, Regex? mapper, Dictionary<string, string>? map)
    {
        var d = new Dictionary<string, int>();
        foreach (var id in ids)
        {
            var comp = await sol.GetProject(id)!.GetCompilationAsync();
            foreach (var x in comp!.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error))
            {
                string msg = x.GetMessage();
                if (mapper != null) msg = mapper.Replace(msg, m => map![m.Value]);
                string k = $"{sol.GetProject(id)!.Name}|{x.Id}|{x.Location.GetLineSpan().Path}|{msg}";
                d[k] = d.GetValueOrDefault(k) + 1;
            }
        }
        return d;
    }

    static async Task<int> Run(string root, string label, List<(string Old, string New)> pairs, bool dry)
    {
        using var ws = MSBuildWorkspace.Create();
        ws.WorkspaceFailed += (s, e) => { if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure) Console.Error.WriteLine("workspace: " + e.Diagnostic.Message); };
        try
        {
            await ws.OpenProjectAsync(Path.Combine(root, "Alundra.Tests", "Alundra.Tests.csproj"));
            await ws.OpenProjectAsync(Path.Combine(root, "tools", "AlundraTestSaves", "AlundraTestSaves.csproj"));
        }
        catch (Exception e) { Console.Error.WriteLine("loading failed: " + e.Message); return 2; }

        var sol0 = ws.CurrentSolution;
        var oursIds = sol0.Projects.Where(p => IsOurs(root, p)).Select(p => p.Id).ToList();
        var alundra = sol0.Projects.FirstOrDefault(p => p.FilePath != null && string.Equals(Path.GetFullPath(p.FilePath), Path.Combine(root, "Alundra", "Alundra.csproj"), StringComparison.OrdinalIgnoreCase));
        if (alundra == null || oursIds.Count != 3) { Console.Error.WriteLine($"expected the 3 projects Alundra, Alundra.Tests, AlundraTestSaves; found {oursIds.Count}"); return 2; }
        // documentation comments parsed, so that `cref` is structured and renamed
        foreach (var id in oursIds)
        {
            var po = (CSharpParseOptions)sol0.GetProject(id)!.ParseOptions!;
            sol0 = sol0.WithProjectParseOptions(id, po.WithDocumentationMode(DocumentationMode.Diagnose));
        }
        var before = sol0;
        var oursDocs = oursIds.SelectMany(id => before.GetProject(id)!.Documents).ToList();
        var oursDocSet = oursDocs.ToImmutableHashSet();
        Console.WriteLine($"loaded: {oursDocs.Count} documents in {oursIds.Count} projects");

        var map = pairs.ToDictionary(p => p.Old, p => p.New);
        var mapper = new Regex(@"(?<![A-Za-z0-9_])(" + string.Join("|", pairs.Select(p => Regex.Escape(p.Old)).OrderByDescending(x => x.Length)) + @")(?![A-Za-z0-9_])");
        string MapStr(string s) => mapper.Replace(s, m => map[m.Value]);

        var errBefore = await Errors(before, oursIds, null, null);
        if (errBefore.Count > 0)
        {
            Console.Error.WriteLine($"the tree already has {errBefore.Values.Sum()} compilation error(s); nothing done");
            foreach (var k in errBefore.Keys.Take(10)) Console.Error.WriteLine("  " + k);
            return 2;
        }

        // before: the references of each type, and the descriptors of the renamed types
        var refsBefore = new Dictionary<string, List<(DocumentId Doc, Microsoft.CodeAnalysis.Text.TextSpan Span)>>();
        var renamedDescs = new HashSet<string>();
        foreach (var (o, n) in pairs)
        {
            var sym = await FindType(before, before.GetProject(alundra.Id)!, o);
            if (sym == null) continue;
            renamedDescs.Add(Desc(sym.OriginalDefinition));
            var refs = await SymbolFinder.FindReferencesAsync(sym, before, oursDocSet);
            var list = new List<(DocumentId, Microsoft.CodeAnalysis.Text.TextSpan)>();
            foreach (var r in refs)
                foreach (var l in r.Locations)
                    if (!l.IsImplicit && l.Location.IsInSource) list.Add((l.Document.Id, l.Location.SourceSpan));
            refsBefore[o] = list;
        }
        if (Problems.Count > 0) return Refuse(label);

        // SymbolFinder (hence Renamer) sometimes does not report a cref although the semantic model binds it (seen in a doc comment of a
        // nested test class): rewrite those tokens first, textually, so that every cref bound to a renamed type is renamed; the
        // verification below then checks the binding of each of them as for any other reference
        var found = new HashSet<(DocumentId, int)>(refsBefore.Values.SelectMany(l => l.Select(x => (x.Doc, x.Span.Start))));
        var cur = before;
        int crefFixups = 0;
        foreach (var d in oursDocs)
        {
            if (!d.SupportsSyntaxTree) continue;
            var droot = (await d.GetSyntaxRootAsync())!;
            var dm = (await d.GetSemanticModelAsync())!;
            var edits = new List<Microsoft.CodeAnalysis.Text.TextChange>();
            foreach (var tk in droot.DescendantTokens(descendIntoTrivia: true))
            {
                if (!tk.IsKind(SyntaxKind.IdentifierToken) || !map.ContainsKey(tk.ValueText)) continue;
                if (tk.Parent?.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.XmlCrefAttributeSyntax>() == null) continue;
                if (found.Contains((d.Id, tk.SpanStart)) || !IsRenamedSymbolDesc(dm, tk, renamedDescs)) continue;
                edits.Add(new Microsoft.CodeAnalysis.Text.TextChange(tk.Span, map[tk.ValueText]));
            }
            if (edits.Count == 0) continue;
            crefFixups += edits.Count;
            cur = cur.WithDocumentText(d.Id, (await d.GetTextAsync()).WithChanges(edits));
        }
        if (crefFixups > 0) Console.WriteLine($"{crefFixups} cref(s) not reported by SymbolFinder rewritten textually");

        // rename
        foreach (var (o, n) in pairs)
        {
            var sym = await FindType(cur, cur.GetProject(alundra.Id)!, o);
            if (sym == null) return Refuse(label);
            var opts = new SymbolRenameOptions(RenameOverloads: false, RenameInStrings: false, RenameInComments: false, RenameFile: false);
            cur = await Renamer.RenameSymbolAsync(cur, sym, opts, n);
        }
        var after = cur;

        var changes = after.GetChanges(before);
        var changedIds = new List<DocumentId>();
        foreach (var pc in changes.GetProjectChanges())
            changedIds.AddRange(pc.GetChangedDocuments());
        foreach (var pc in changes.GetProjectChanges())
        {
            if (pc.GetAddedDocuments().Any() || pc.GetRemovedDocuments().Any()) Bad($"project {before.GetProject(pc.ProjectId)!.Name}: documents added or removed by the rename");
        }
        changedIds = changedIds.Distinct().ToList();
        Console.WriteLine($"rename done: {changedIds.Count} document(s) changed");

        var nsSegments = new HashSet<string> { "global", "Alundra" };
        {
            var comp = await after.GetProject(alundra.Id)!.GetCompilationAsync();
            void Walk(INamespaceSymbol ns) { foreach (var m in ns.GetNamespaceMembers()) { nsSegments.Add(m.Name); Walk(m); } }
            Walk(comp!.GlobalNamespace.GetNamespaceMembers().First(x => x.Name == "Alundra"));
        }

        int qualifiers = 0, tokensChecked = 0;
        var pairIndexByDoc = new Dictionary<DocumentId, Dictionary<int, SyntaxToken>>(); // before token start -> after token
        foreach (var id in changedIds)
        {
            var bd = before.GetDocument(id)!; var ad = after.GetDocument(id)!;
            string path = bd.FilePath ?? bd.Name;
            if (!IsUnderRepoSource(root, path)) { Bad($"{path}: a changed document outside the sources of the tranche"); continue; }
            var bt = (await bd.GetSyntaxRootAsync())!; var at = (await ad.GetSyntaxRootAsync())!;
            var bm = (await bd.GetSemanticModelAsync())!; var am = (await ad.GetSemanticModelAsync())!;
            var btok = bt.DescendantTokens(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.IdentifierToken)).ToList();
            var atok = at.DescendantTokens(descendIntoTrivia: true).Where(t => t.IsKind(SyntaxKind.IdentifierToken)).ToList();
            var byStart = new Dictionary<int, SyntaxToken>();
            pairIndexByDoc[id] = byStart;
            int j = 0;
            bool ok = true;
            for (int i = 0; i < btok.Count && ok; i++)
            {
                var b = btok[i];
                string bdesc = TokenDesc(bm, b);
                bool renamedTok = map.ContainsKey(b.ValueText) && IsRenamedSymbolDesc(bm, b, renamedDescs);
                string exp = renamedTok ? map[b.ValueText] : b.ValueText;
                int k = j, skipped = 0;
                while (k < atok.Count && atok[k].ValueText != exp && skipped < 8 && nsSegments.Contains(atok[k].ValueText)) { k++; skipped++; }
                if (k >= atok.Count || atok[k].ValueText != exp)
                {
                    Bad($"{path}:{b.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: identifier `{b.ValueText}` has no counterpart `{exp}` after the rename (the after token is `{(j < atok.Count ? atok[j].ValueText : "<end>")}` at line {(j < atok.Count ? atok[j].GetLocation().GetLineSpan().StartLinePosition.Line + 1 : 0)})");
                    ok = false; break;
                }
                qualifiers += skipped;
                var a = atok[k];
                string adesc = TokenDesc(am, a);
                tokensChecked++;
                if (MapStr(bdesc) != adesc)
                    Bad($"{path}:{b.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: `{b.ValueText}` bound to {bdesc} before, {adesc} after");
                byStart[b.SpanStart] = a;
                j = k + 1;
            }
            if (ok && j < atok.Count) Bad($"{path}: {atok.Count - j} identifier token(s) added at the end after the rename");
        }

        // every recorded reference is a renamed token bound to the renamed type
        int refsChecked = 0;
        foreach (var (o, n) in pairs)
        {
            if (!refsBefore.TryGetValue(o, out var list)) continue;
            foreach (var (docId, span) in list)
            {
                var bd = before.GetDocument(docId)!;
                var text = (await bd.GetTextAsync()).ToString(span);
                if (text != o) continue; // implicit location (the name is not written there)
                refsChecked++;
                if (!pairIndexByDoc.TryGetValue(docId, out var m) || !m.TryGetValue(span.Start, out var at))
                { Bad($"{bd.FilePath}: reference to {o} at {span.Start} was not seen as changed"); continue; }
                if (at.ValueText != n) Bad($"{bd.FilePath}: reference to {o} is now `{at.ValueText}`, expected `{n}`");
            }
        }

        // no new compilation error
        var errAfter = await Errors(after, oursIds, mapper, map);
        var errBeforeMapped = errBefore.ToDictionary(kv => MapStr(kv.Key), kv => kv.Value);
        foreach (var kv in errAfter)
            if (kv.Value > errBeforeMapped.GetValueOrDefault(kv.Key)) Bad("new compilation error: " + kv.Key);

        if (Problems.Count > 0) return Refuse(label);

        // text identity of the files to write: they decode strictly as UTF-8 to the very text Roslyn holds
        var writes = new List<(string Path, byte[] Bytes)>();
        foreach (var id in changedIds)
        {
            var bd = before.GetDocument(id)!; var ad = after.GetDocument(id)!;
            string path = bd.FilePath!;
            var orig = File.ReadAllBytes(path);
            bool bom = orig.Length >= 3 && orig[0] == 0xEF && orig[1] == 0xBB && orig[2] == 0xBF;
            string origText;
            try { origText = new UTF8Encoding(false, true).GetString(orig, bom ? 3 : 0, orig.Length - (bom ? 3 : 0)); }
            catch (Exception) { Bad($"{path}: not valid UTF-8"); continue; }
            if (origText != (await bd.GetTextAsync()).ToString()) { Bad($"{path}: the text held by Roslyn differs from the file"); continue; }
            var enc = new UTF8Encoding(bom);
            var body = enc.GetBytes((await ad.GetTextAsync()).ToString());
            writes.Add((path, enc.GetPreamble().Concat(body).ToArray()));
        }
        if (Problems.Count > 0) return Refuse(label);

        Console.WriteLine($"{label}: {pairs.Count} type(s), {changedIds.Count} file(s) changed, {refsChecked} reference(s) and {tokensChecked} identifier(s) verified, {qualifiers} namespace qualifier(s) added by Roslyn");
        if (dry) { Console.WriteLine("dry run: nothing written"); return 0; }
        foreach (var (p, b) in writes) File.WriteAllBytes(p, b);
        Console.WriteLine($"{writes.Count} file(s) written");
        return 0;
    }

    static bool IsUnderRepoSource(string root, string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        var rel = full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
        if (rel.Contains("/obj/") || rel.Contains("/bin/")) return false;
        return rel.StartsWith("Alundra/") || rel.StartsWith("Alundra.Tests/") || rel.StartsWith("tools/AlundraTestSaves/");
    }

    // The identifier is the renamed type itself, or the constructor / destructor of one.
    static bool IsRenamedSymbolDesc(SemanticModel m, SyntaxToken t, HashSet<string> renamedDescs)
    {
        var n = t.Parent;
        if (n == null) return false;
        try
        {
            ISymbol? s = m.GetDeclaredSymbol(n);
            if (s == null) { var info = m.GetSymbolInfo(n); s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault(); }
            if (s == null) return false;
            if (s is IMethodSymbol ms && (ms.MethodKind is MethodKind.Constructor or MethodKind.Destructor or MethodKind.StaticConstructor)) s = ms.ContainingType;
            if (s is INamedTypeSymbol nt) return renamedDescs.Contains(Desc(nt.OriginalDefinition));
            return false;
        }
        catch (Exception) { return false; }
    }

    static int Refuse(string label)
    {
        Console.Error.WriteLine($"{label}: REFUSED, nothing written ({Problems.Count} problem(s)):");
        foreach (var p in Problems.Take(60)) Console.Error.WriteLine("  " + p);
        if (Problems.Count > 60) Console.Error.WriteLine($"  ... {Problems.Count - 60} more");
        return 1;
    }
}
