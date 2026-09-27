using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AlundraCasaEngineProjectConverter.Readers;
using AlundraCasaEngineProjectConverter.Text;
using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Configuration;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests.Text;

/// <summary>
/// Task T6 of docs/plan-e15-yarn.md's E15.b: proves the Yarn dialogues the real export chain produces
/// are equivalent, page for page, to <see cref="ReferenceTextDecoder"/>'s reading of
/// <c>TextDecoder.cs</c>, over the whole corpus. Two independent sides:
///  - "compiled": <see cref="YarnDialogueWriter.ConvertDialogues"/> emits and compiles every table into
///    a temporary project, each <c>.dialogue</c> is loaded back from disk, and every one of its nodes is
///    played on a real <see cref="YarnDialogueRunner"/> (the same engine class E15.c will use);
///  - "original": this file reads the corpus itself (never through <see cref="YarnDialogueWriter"/> or
///    <see cref="YarnTextEmitter"/>) and decodes each source string with <see cref="ReferenceTextDecoder"/>.
///
/// Skips silently when <c>data-extracted/</c> is absent (same convention as
/// <c>AnimationEndClassifierTests.FindRealDataFile</c>). The negative tests (discriminating power) build
/// their own tiny table in memory and never touch <c>data-extracted/</c>.
/// </summary>
public class YarnCorpusEquivalenceTests : IClassFixture<YarnCorpusEquivalenceTests.CorpusFixture>
{
    private readonly CorpusFixture _fixture;

    public YarnCorpusEquivalenceTests(CorpusFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- Whole corpus -----------------------------------------------------------------------------

    [Fact]
    public void WholeCorpus_EveryPageOfEveryNodeMatchesTheReferenceDecoder()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var comparator = new Comparator();
        var mismatches = new List<string>();
        var playedTitles = new HashSet<string>(StringComparer.Ordinal);
        var assetsByTitle = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var pageCount = 0;

        foreach (var asset in _fixture.Assets)
        {
            foreach (var title in asset.NodeTitles)
            {
                playedTitles.Add(title);

                // F3: an extra node (played but never expected anywhere) is reported below by
                // ReportNodeSetMismatches, never a KeyNotFoundException here; only a title with a known
                // expected asset (docs/plan-e15-yarn.md's file/node contract) is tracked for placement.
                if (!_fixture.ExpectedByTitle.TryGetValue(title, out var expectedPages))
                {
                    continue;
                }

                if (!assetsByTitle.TryGetValue(title, out var owningAssets))
                {
                    owningAssets = new List<string>();
                    assetsByTitle[title] = owningAssets;
                }

                owningAssets.Add(asset.CatalogName);

                var actualPages = comparator.Play(asset.Dialogue, title);
                pageCount += actualPages.Count;
                mismatches.AddRange(ComparePagesAndCommands(comparator, title, expectedPages, actualPages));
            }
        }

        if (comparator.UnhandledCommands.Count > 0)
        {
            mismatches.Add($"unhandled command(s): {string.Join(", ", comparator.UnhandledCommands.Take(20))}");
        }

        // No missing, no extra node: the played set equals the expected set exactly.
        var expectedTitles = _fixture.ExpectedByTitle.Keys.ToHashSet(StringComparer.Ordinal);
        mismatches.AddRange(ReportNodeSetMismatches(expectedTitles, playedTitles));

        // F3: no missing, no extra node PER ASSET - a title played under a different asset than the
        // one its own source table maps to, or under more than one asset, is a mismatch of its own.
        mismatches.AddRange(ReportNodePlacementMismatches(assetsByTitle, ExpectedAssetNameForTitle));

        // F5: every mismatch collected above is reported together, before any other assertion can fail.
        Assert.True(
            mismatches.Count == 0,
            $"{mismatches.Count} mismatch(es); first 50:\n" + string.Join("\n", mismatches.Take(50)));

        Assert.Equal(1024, _fixture.EtcIndexEntryCount);
        Assert.Equal(485, _fixture.Assets.Count);
        Assert.Equal(24784, playedTitles.Count);
        Assert.Equal(31757, pageCount);
    }

    // ---- Shared mismatch-reporting helper (F2/F3/F4/F5): the single place both the whole-corpus test
    // and the negative tests below turn a played node/asset/marker into mismatch strings, so every
    // negative test exercises the exact same reporting code the corpus test relies on. ---------------

    /// <summary>
    /// One node's mismatches against its oracle pages: pending commands/calls after the last line
    /// (contract step 3), a page-count mismatch (reported alone, since per-page comparison is then
    /// meaningless), one canonical-string diff per differing page (text, markers - including F4's
    /// non-"trimwhitespace" properties -, commands, calls), a non-empty <see cref="DialogueLine.Speaker"/>
    /// (contract: every page's Speaker must be empty), and any marker <see cref="Comparator.NonZeroLengthMarkers"/>
    /// recorded while playing the node.
    /// </summary>
    private static List<string> ComparePagesAndCommands(
        Comparator comparator, string title, IReadOnlyList<ReferencePage> expectedPages, IReadOnlyList<Comparator.ActualPage> actualPages)
    {
        var mismatches = new List<string>();

        if (comparator.PendingCommandsAfterLastLine != 0 || comparator.PendingCallsAfterLastLine != 0)
        {
            mismatches.Add(
                $"{title}: {comparator.PendingCommandsAfterLastLine} pending command(s) and "
                + $"{comparator.PendingCallsAfterLastLine} pending call(s) after its last line");
        }

        if (comparator.NonZeroLengthMarkers.Count > 0)
        {
            mismatches.Add($"{title}: non-zero length marker(s): {string.Join(", ", comparator.NonZeroLengthMarkers)}");
        }

        if (actualPages.Count != expectedPages.Count)
        {
            mismatches.Add(
                $"{title}: page count mismatch, expected {expectedPages.Count}, actual {actualPages.Count}");
            return mismatches;
        }

        for (var pageIndex = 0; pageIndex < actualPages.Count; pageIndex++)
        {
            var actual = actualPages[pageIndex];
            if (actual.Speaker.Length > 0)
            {
                mismatches.Add($"{title}_p{pageIndex}: non-empty Speaker '{actual.Speaker}'");
            }

            var expectedCanonical = expectedPages[pageIndex].ToCanonicalString();
            var actualCanonical = comparator.ToCanonicalString(actual);
            if (!string.Equals(expectedCanonical, actualCanonical, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"{title}_p{pageIndex}:\n  expected: {expectedCanonical}\n  actual:   {actualCanonical}");
            }
        }

        return mismatches;
    }

    /// <summary>Contract item 3: no missing, no extra node, over the whole played/expected title sets.</summary>
    private static List<string> ReportNodeSetMismatches(IReadOnlySet<string> expectedTitles, IReadOnlySet<string> actualTitles)
    {
        var mismatches = new List<string>();
        var missing = expectedTitles.Except(actualTitles).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var extra = actualTitles.Except(expectedTitles).OrderBy(t => t, StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            mismatches.Add($"missing {missing.Count} node(s): {string.Join(", ", missing.Take(20))}");
        }

        if (extra.Count > 0)
        {
            mismatches.Add($"extra {extra.Count} node(s): {string.Join(", ", extra.Take(20))}");
        }

        return mismatches;
    }

    /// <summary>
    /// F3: "no missing, no extra node, per asset" - <paramref name="assetsByTitle"/> is every title
    /// actually played, mapped to the catalog name(s) of the asset(s) it was found under (more than one
    /// entry means the title is duplicated across assets); <paramref name="expectedAssetForTitle"/>
    /// resolves the asset its own source table maps to (docs/plan-e15-yarn.md's file/node contract).
    /// </summary>
    private static List<string> ReportNodePlacementMismatches(
        IReadOnlyDictionary<string, List<string>> assetsByTitle, Func<string, string> expectedAssetForTitle)
    {
        var mismatches = new List<string>();
        foreach (var title in assetsByTitle.Keys.OrderBy(t => t, StringComparer.Ordinal))
        {
            var assets = assetsByTitle[title];
            if (assets.Count > 1)
            {
                mismatches.Add($"{title}: duplicated in {assets.Count} asset(s): {string.Join(", ", assets)}");
                continue;
            }

            var expectedAsset = expectedAssetForTitle(title);
            if (!string.Equals(expectedAsset, assets[0], StringComparison.Ordinal))
            {
                mismatches.Add($"{title}: played in asset '{assets[0]}', expected asset '{expectedAsset}'");
            }
        }

        return mismatches;
    }

    /// <summary>
    /// The catalog name <see cref="YarnDialogueWriter.ConvertDialogues"/> gives the asset each title's
    /// own source table is written to: <c>M{n}_S...</c> -&gt; <c>dialogue_{n}</c>, <c>Shared_S...</c> -&gt;
    /// <c>dialogue_shared</c>, <c>Etc_...</c> -&gt; <c>dialogue_etc</c> (docs/plan-e15-yarn.md's file/node
    /// contract).
    /// </summary>
    private static string ExpectedAssetNameForTitle(string title)
    {
        if (title.StartsWith("Shared_S", StringComparison.Ordinal))
        {
            return "dialogue_shared";
        }

        if (title.StartsWith("Etc_", StringComparison.Ordinal))
        {
            return "dialogue_etc";
        }

        var match = Regex.Match(title, @"^M(\d+)_S\d{3}$");
        if (match.Success)
        {
            return "dialogue_" + match.Groups[1].Value;
        }

        throw new InvalidOperationException($"Cannot determine the expected asset for title '{title}'.");
    }

    // ---- Named cases (docs/plan-e15-yarn.md, E15.b "Cas nommés") ----------------------------------

    [Theory]
    [InlineData("M323_S095", 4, "text=Tu en as rencontré un Nirude.\\nLes Gazeck ont été taillés dans la pierre\\npar d'anciens humains."
        + "; markers=[glyph@21 id=18]; commands=[]; calls=[]")]
    [InlineData("M135_S090", 3, null)] // checked structurally below (§ NamedCase_M135_S090_p3_...).
    [InlineData("M324_S041", 0, null)] // contains " : ", no Speaker: checked structurally below.
    public void NamedCase_MatchesTheHandWrittenExpectation(string titlePrefix, int pageIndex, string? expectedCanonical)
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith(titlePrefix);
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var actual = pages[pageIndex];
        var actualCanonical = comparator.ToCanonicalString(actual);

        if (expectedCanonical is not null)
        {
            Assert.Equal(expectedCanonical, actualCanonical);
        }

        // Every named case is also checked against the independent reference decoder.
        var expected = _fixture.ExpectedByTitle[title][pageIndex].ToCanonicalString();
        Assert.Equal(expected, actualCanonical);
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M135_S090_p3_HasASingleFlag401Command()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M135_S090");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var page = pages[3];

        // Copied from ReferenceTextDecoderTests.NamedCase2_M135_S090_p3: the numeric code \401 never
        // enters the visible text, so "a été" and "détruite." are adjacent with no space between them.
        Assert.Equal("Pis encore : notre relation avec les dieux\na étédétruite.\nMaisil n'est pas trop tard pour", page.Text);
        var command = Assert.Single(page.Commands);
        Assert.Equal("flag", command.Name);
        Assert.Equal("401", command.Arguments[0]);

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase2_M135_S090_p3's own literal.
        var expectedPage = new ReferencePage(
            "Pis encore : notre relation avec les dieux\na étédétruite.\nMaisil n'est pas trop tard pour",
            new[]
            {
                ReferenceMarker.Slow(62),
                ReferenceMarker.Glyph(18, 62),
                ReferenceMarker.Slow(62),
                ReferenceMarker.Glyph(18, 62),
                ReferenceMarker.Slow(62),
            },
            new[] { ReferenceCommand.Flag(401) },
            Array.Empty<ReferenceCall>());
        Assert.Equal(expectedPage.ToCanonicalString(), comparator.ToCanonicalString(page));

        Assert.Equal(_fixture.ExpectedByTitle[title][3].ToCanonicalString(), comparator.ToCanonicalString(page));
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M324_S041_p0_ContainsAColonAndHasNoSpeaker()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M324_S041");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var page = pages[0];

        Assert.Contains(" : ", page.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, page.Speaker);

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase3_M324_S041_p0's own literal.
        var expectedPage = new ReferencePage(
            "Main gauche : non opérationnelle\nMain droite : fonctionnelle",
            new[]
            {
                ReferenceMarker.Voice(-1, 0),
                ReferenceMarker.Center(0),
                ReferenceMarker.Center(33),
            },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expectedPage.ToCanonicalString(), comparator.ToCanonicalString(page));

        Assert.Equal(_fixture.ExpectedByTitle[title][0].ToCanonicalString(), comparator.ToCanonicalString(page));
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M323_S097_Pages0To2_MatchTheReferenceDecoder()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M323_S097");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var expected = _fixture.ExpectedByTitle[title];

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase4_M323_S097's own literals.
        var expectedP0 = new ReferencePage(
            "Melzas va bientôt détruire\ncet endroit.",
            new[] { ReferenceMarker.Voice(1, 0) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        var expectedP1 = new ReferencePage(
            "",
            new[] { ReferenceMarker.Empty(0) },
            new[] { ReferenceCommand.Flag(999) },
            Array.Empty<ReferenceCall>());
        var expectedP2 = new ReferencePage(
            "Pars d'ici, Alundra ! Fais\nvite ! La destruction arrive !\n",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        var expectedPages = new[] { expectedP0, expectedP1, expectedP2 };

        for (var i = 0; i <= 2; i++)
        {
            Assert.Equal(expectedPages[i].ToCanonicalString(), comparator.ToCanonicalString(pages[i]));
            Assert.Equal(expected[i].ToCanonicalString(), comparator.ToCanonicalString(pages[i]));
        }

        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M134_S016_p0_ChurchMarinReadsCategoryItemNameBefore()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M134_S016");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var page = pages[0];

        Assert.Equal(
            "Je pense que tu trouveras ce(t) ⟦objet d'avant⟧\ntout à fait utile. Fais-en bon usage,\nAlundra !",
            page.Text);
        Assert.Contains(page.Markers, m => m.Name == "voice" && m.Position == 0 && m.Properties["id"] == "0");
        Assert.Contains(page.Commands, c => c.Name == "falcon_update");
        Assert.Contains(page.Calls, c => c.Name == "category_item_name_before");

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase5_M134_S016_p0's own literal.
        var expectedPage = new ReferencePage(
            $"Je pense que tu trouveras ce(t) {ReferenceTextDecoder.CategoryItemNameBeforeWitness}\ntout à fait utile. Fais-en bon usage,\nAlundra !",
            new[] { ReferenceMarker.Voice(0, 0) },
            new[] { ReferenceCommand.FalconUpdate },
            new[] { ReferenceCall.CategoryItemNameBefore });
        Assert.Equal(expectedPage.ToCanonicalString(), comparator.ToCanonicalString(page));

        Assert.Equal(_fixture.ExpectedByTitle[title][0].ToCanonicalString(), comparator.ToCanonicalString(page));
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M472_S011_Pages0To1_MatchTheReferenceDecoder()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M472_S011");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var expected = _fixture.ExpectedByTitle[title];

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase6_M472_S011's own literals.
        var expectedP0 = new ReferencePage(
            "Temps écoulé ! La partie est finie, mec !\nComptons tes points",
            new[] { ReferenceMarker.Voice(0, 0), ReferenceMarker.Glyph(18, 61) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        var expectedP1 = new ReferencePage(
            "Tu as 91000 cibles et91001\npénalités pour un total de 91002.",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            new[] { ReferenceCall.GameVar(0), ReferenceCall.GameVar(1), ReferenceCall.GameVar(2) });
        Assert.Equal(expectedP0.ToCanonicalString(), comparator.ToCanonicalString(pages[0]));
        Assert.Equal(expectedP1.ToCanonicalString(), comparator.ToCanonicalString(pages[1]));

        Assert.Equal(expected[0].ToCanonicalString(), comparator.ToCanonicalString(pages[0]));
        Assert.Equal(expected[1].ToCanonicalString(), comparator.ToCanonicalString(pages[1]));
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_Etc_0770_And_Etc_0140_MatchTheReferenceDecoder()
    {
        if (!_fixture.Available)
        {
            return;
        }

        // F1: hand-written expectations, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase7_EtcGlyphs's own literals.
        var expectedByTitle = new Dictionary<string, ReferencePage>(StringComparer.Ordinal)
        {
            ["Etc_0770"] = new ReferencePage(
                "Charger en appuyant sur .",
                new[] { ReferenceMarker.Glyph(26, 24) },
                Array.Empty<ReferenceCommand>(),
                Array.Empty<ReferenceCall>()),
            ["Etc_0140"] = new ReferencePage(
                "appuie sur la Touche .",
                new[] { ReferenceMarker.Glyph(28, 21) },
                Array.Empty<ReferenceCommand>(),
                Array.Empty<ReferenceCall>()),
        };

        foreach (var title in new[] { "Etc_0770", "Etc_0140" })
        {
            var comparator = new Comparator();
            var asset = _fixture.FindAssetForTitle(title);
            var pages = comparator.Play(asset, title);
            var expected = _fixture.ExpectedByTitle[title];
            Assert.Equal(expected.Count, pages.Count);
            var page = Assert.Single(pages);
            Assert.Equal(expectedByTitle[title].ToCanonicalString(), comparator.ToCanonicalString(page));
            Assert.Equal(expected[0].ToCanonicalString(), comparator.ToCanonicalString(page));
            Assert.Empty(comparator.UnhandledCommands);
        }
    }

    [Fact]
    public void NamedCase_M134_S019_p0_ChurchMarinReadsThresholdThenItemName()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M134_S019");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var page = pages[0];

        // Copied from ReferenceTextDecoderTests.NamedCase8_M134_S019_p0.
        Assert.Equal(
            "Ramène-moi 90003 Statuettes de faucons\net je te récompenserai avec cela :\n⟦objet⟧.",
            page.Text);

        Assert.Equal(2, page.Commands.Count(c => c.Name is "falcon_update" or "flag"));
        Assert.Contains(page.Commands, c => c.Name == "falcon_update");
        Assert.Contains(page.Commands, c => c.Name == "flag" && c.Arguments[0] == "100");
        Assert.Contains(page.Calls, c => c.Name == "category_threshold");
        Assert.Contains(page.Calls, c => c.Name == "category_item_name");
        Assert.Equal(string.Empty, page.Speaker);

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase8_M134_S019_p0's own literal.
        var expectedPage = new ReferencePage(
            $"Ramène-moi 90003 Statuettes de faucons\net je te récompenserai avec cela :\n{ReferenceTextDecoder.CategoryItemNameWitness}.",
            new[] { ReferenceMarker.Voice(0, 0) },
            new[] { ReferenceCommand.FalconUpdate, ReferenceCommand.Flag(100) },
            new[] { ReferenceCall.CategoryThreshold, ReferenceCall.CategoryItemName });
        Assert.Equal(expectedPage.ToCanonicalString(), comparator.ToCanonicalString(page));

        Assert.Equal(_fixture.ExpectedByTitle[title][0].ToCanonicalString(), comparator.ToCanonicalString(page));
        Assert.Empty(comparator.UnhandledCommands);
    }

    [Fact]
    public void NamedCase_M134_S012_p1_KeepsTheFinalLineBreak()
    {
        if (!_fixture.Available)
        {
            return;
        }

        var title = _fixture.FindTitleStartingWith("M134_S012");
        var comparator = new Comparator();
        var asset = _fixture.FindAssetForTitle(title);
        var pages = comparator.Play(asset, title);
        var page = pages[1];

        Assert.Equal("Je suis un homme heureux. Merci,\nAlundra !\n", page.Text);

        // F1: hand-written expectation, independent of the reference decoder - copied from
        // ReferenceTextDecoderTests.NamedCase9_M134_S012_p1's own literal.
        var expectedPage = new ReferencePage(
            "Je suis un homme heureux. Merci,\nAlundra !\n",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expectedPage.ToCanonicalString(), comparator.ToCanonicalString(page));

        Assert.Equal(_fixture.ExpectedByTitle[title][1].ToCanonicalString(), comparator.ToCanonicalString(page));
        Assert.Empty(comparator.UnhandledCommands);
    }

    // ---- Discriminating power (negative tests) - never touch data-extracted/ ----------------------
    //
    // Each test takes a small synthetic table, emits+compiles+plays it once as a control (proving the
    // comparator agrees with the reference decoder on unmodified data), then applies one controlled
    // corruption to either the Yarn source or the compiled asset and re-plays, asserting the comparator
    // now reports the expected mismatch. This proves the comparator is not vacuously green.

    [Fact]
    public void Discriminates_AGlyphIdChangedInTheCompiledSource()
    {
        const string title = "Neg_Glyph";
        const string source = "Bonjour \\W2 ami"; // \W2 -> glyph 18

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        // Corrupt: change the glyph id in the emitted Yarn source before compiling (18 -> 19).
        var corruptedYarn = BuildSingleNodeYarn(title, "Bonjour [glyph id=19 trimwhitespace=false/] ami");
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        var expectedMismatch =
            $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}";
        Assert.Equal(new[] { expectedMismatch }, mismatches);
        Assert.Equal("text=Bonjour  ami; markers=[glyph@8 id=19]; commands=[]; calls=[]", badComparator.ToCanonicalString(badPages[0]));
    }

    [Fact]
    public void Discriminates_AnExtraNode()
    {
        var expectedTitles = new HashSet<string>(StringComparer.Ordinal) { "Neg_Table_S000" };
        var actualTitles = new HashSet<string>(StringComparer.Ordinal) { "Neg_Table_S000", "Neg_Table_S009" };

        var mismatches = ReportNodeSetMismatches(expectedTitles, actualTitles);

        Assert.Equal(new[] { "extra 1 node(s): Neg_Table_S009" }, mismatches);
    }

    [Fact]
    public void Discriminates_AFlagCommandRemoved()
    {
        const string title = "Neg_Flag";
        const string source = "\\100Bonjour"; // <<flag 100>> before the line.

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        var corruptedYarn = BuildSingleNodeYarn(title, "Bonjour"); // flag command dropped
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        Assert.Empty(badPages[0].Commands);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        var expectedMismatch =
            $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}";
        Assert.Equal(new[] { expectedMismatch }, mismatches);
    }

    [Fact]
    public void Discriminates_ABrMarkerRemoved()
    {
        const string title = "Neg_Br";
        const string source = "Ligne un\\NLigne deux";

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        var corruptedYarn = BuildSingleNodeYarn(title, "Ligne unLigne deux"); // [br/] dropped
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        var expectedMismatch =
            $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}";
        Assert.Equal(new[] { expectedMismatch }, mismatches);
    }

    [Fact]
    public void Discriminates_CategoryItemNameBeforeSwappedForCategoryItemName()
    {
        const string title = "Neg_Category";
        const string source = "\\X2objet"; // \X2 opening the page -> category_item_name_before.

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));
        Assert.Contains(goodPages[0].Calls, c => c.Name == "category_item_name_before");

        // Corrupt: swap the function actually called, keeping the falcon_update command line untagged
        // and only the body line carrying the #line: id (matching the real emitter's own shape).
        var corruptedYarn = "title: " + title + "\n---\n<<falcon_update>>\n{category_item_name()}objet #line:"
            + title + "_p0\n===\n";
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        Assert.Contains(badPages[0].Calls, c => c.Name == "category_item_name");
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        var expectedMismatch =
            $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}";
        Assert.Equal(new[] { expectedMismatch }, mismatches);
    }

    [Fact]
    public void Discriminates_ANodeDropped()
    {
        var entries = new List<YarnTextEntry>
        {
            new("Neg_Table_S000", "Un"),
            new("Neg_Table_S001", "Deux"),
        };

        var emit = YarnTextEmitter.Emit(entries);
        Assert.Empty(emit.Errors);

        var compiler = new YarnDialogueCompiler();
        var compiled = compiler.CompileString(emit.Source, "neg_table.yarn", AlundraYarnFunctions.CreateDeclarations());
        Assert.False(compiled.ContainsErrors);

        var expectedTitles = new HashSet<string>(entries.Select(e => e.Title), StringComparer.Ordinal);
        var actualTitles = ParseNodeTitles(compiled.ProgramBytes);
        Assert.Empty(ReportNodeSetMismatches(expectedTitles, actualTitles));

        // Corrupt: drop the second node entirely from the source before compiling.
        var droppedSource = emit.Source.Replace(
            "title: Neg_Table_S001\n---\nDeux #line:Neg_Table_S001_p0\n===\n", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(emit.Source, droppedSource); // guard: the replacement actually matched something.

        var droppedCompiled = compiler.CompileString(droppedSource, "neg_table.yarn", AlundraYarnFunctions.CreateDeclarations());
        Assert.False(droppedCompiled.ContainsErrors);
        var droppedTitles = ParseNodeTitles(droppedCompiled.ProgramBytes);
        var mismatches = ReportNodeSetMismatches(expectedTitles, droppedTitles);

        Assert.Equal(new[] { "missing 1 node(s): Neg_Table_S001" }, mismatches);
    }

    [Fact]
    public void Discriminates_AnExtraPageAdded()
    {
        const string title = "Neg_Page";
        const string source = "Une seule page";

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Single(reference);
        Assert.Single(goodPages);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        // Corrupt: add a second page the reference decoder never produced.
        var corruptedYarn = BuildSingleNodeYarn(title, "Une seule page", "Page en trop #line:" + title + "_p1");
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        Assert.Equal(
            new[] { $"{title}: page count mismatch, expected {reference.Count}, actual {badPages.Count}" },
            mismatches);
    }

    [Fact]
    public void Discriminates_ANodePlacedInTheWrongAsset()
    {
        // Corrupt: M2_S000 is only ever the real M2's own table (dialogue_2), but is here reported as
        // played from dialogue_1 instead - simulating the export placing a node's compiled program in
        // the wrong asset file, which no amount of corrupting one node's own Yarn source can reach.
        var assetsByTitle = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["M1_S000"] = new List<string> { "dialogue_1" },
            ["M2_S000"] = new List<string> { "dialogue_1" },
        };

        var mismatches = ReportNodePlacementMismatches(assetsByTitle, ExpectedAssetNameForTitle);

        Assert.Equal(new[] { "M2_S000: played in asset 'dialogue_1', expected asset 'dialogue_2'" }, mismatches);
    }

    [Fact]
    public void Discriminates_ANodeDuplicatedAcrossAssets()
    {
        var assetsByTitle = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["M1_S000"] = new List<string> { "dialogue_1", "dialogue_2" },
        };

        var mismatches = ReportNodePlacementMismatches(assetsByTitle, ExpectedAssetNameForTitle);

        Assert.Equal(new[] { "M1_S000: duplicated in 2 asset(s): dialogue_1, dialogue_2" }, mismatches);
    }

    [Fact]
    public void Discriminates_AMarkerWithAnExtraProperty()
    {
        const string title = "Neg_MarkerProperty";
        const string source = "\\Cami"; // \C -> voice id=0, self-closing.

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        // Corrupt: the compiled marker carries an extra property the oracle never sets.
        var corruptedYarn = BuildSingleNodeYarn(title, "[voice id=0 extra=1 trimwhitespace=false/]ami");
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        var expectedMismatch =
            $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}";
        Assert.Equal(new[] { expectedMismatch }, mismatches);
    }

    [Fact]
    public void Discriminates_AMarkerWithNonZeroLength()
    {
        const string title = "Neg_MarkerLength";
        const string source = "\\Cami"; // \C -> voice id=0, self-closing (zero-length) in the oracle.

        var (goodPages, comparator) = CompileAndPlayOne(title, source);
        var reference = ReferenceTextDecoder.Decode(source);
        Assert.Empty(ComparePagesAndCommands(comparator, title, reference, goodPages));

        // Corrupt: turn the self-closing voice marker into a ranged one covering "ami" (length 3).
        var corruptedYarn = BuildSingleNodeYarn(title, "[voice id=0 trimwhitespace=false]ami[/voice]");
        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        Assert.Equal(new[] { $"{title}: non-zero length marker(s): voice@0 length=3" }, mismatches);
    }

    [Fact]
    public void Discriminates_ACommandLeftAfterTheLastLine()
    {
        const string title = "Neg_Pending";
        const string source = "\\100Bonjour"; // <<flag 100>> before the line.

        var emit = YarnTextEmitter.Emit(new List<YarnTextEntry> { new(title, source) });
        Assert.Empty(emit.Errors);
        var reference = ReferenceTextDecoder.Decode(source);
        var goodPages = CompileAndPlayYarn(title, emit.Source, out var goodComparator);
        Assert.Empty(ComparePagesAndCommands(goodComparator, title, reference, goodPages));

        // Corrupt the emitter's output: the flag command moves after the node's last line.
        var corruptedYarn = emit.Source.Replace(
            "<<flag 100>>\nBonjour #line:" + title + "_p0\n",
            "Bonjour #line:" + title + "_p0\n<<flag 100>>\n",
            StringComparison.Ordinal);
        Assert.NotEqual(emit.Source, corruptedYarn); // guard: the replacement actually matched something.

        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        Assert.Empty(badPages[0].Commands);
        Assert.Equal(
            new[]
            {
                $"{title}: 1 pending command(s) and 0 pending call(s) after its last line",
                $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}",
            },
            mismatches);
    }

    [Fact]
    public void Discriminates_ANonEmptySpeakerFromAnUnescapedColon()
    {
        const string title = "Neg_Speaker";
        const string source = "Nom : Bonjour";

        var emit = YarnTextEmitter.Emit(new List<YarnTextEntry> { new(title, source) });
        Assert.Empty(emit.Errors);
        var reference = ReferenceTextDecoder.Decode(source);
        var goodPages = CompileAndPlayYarn(title, emit.Source, out var goodComparator);
        Assert.Empty(ComparePagesAndCommands(goodComparator, title, reference, goodPages));

        // Corrupt the emitter's output: drop the colon's escape, so Yarn reads "Nom" as a character name.
        var corruptedYarn = emit.Source.Replace("\\:", ":", StringComparison.Ordinal);
        Assert.NotEqual(emit.Source, corruptedYarn); // guard: the replacement actually matched something.

        var badPages = CompileAndPlayYarn(title, corruptedYarn, out var badComparator);
        var mismatches = ComparePagesAndCommands(badComparator, title, reference, badPages);

        Assert.Equal("Nom", badPages[0].Speaker.Trim());
        Assert.Equal(
            new[]
            {
                $"{title}_p0: non-empty Speaker '{badPages[0].Speaker}'",
                $"{title}_p0:\n  expected: {reference[0].ToCanonicalString()}\n  actual:   {badComparator.ToCanonicalString(badPages[0])}",
            },
            mismatches);
    }

    // ---- Negative-test helpers ----------------------------------------------------------------

    private static (IReadOnlyList<Comparator.ActualPage> Pages, Comparator Comparator) CompileAndPlayOne(string title, string source)
    {
        var entries = new List<YarnTextEntry> { new(title, source) };
        var emit = YarnTextEmitter.Emit(entries);
        Assert.Empty(emit.Errors);

        var comparator = new Comparator();
        var pages = CompileAndPlayYarn(title, emit.Source, out comparator);
        return (pages, comparator);
    }

    private static IReadOnlyList<Comparator.ActualPage> CompileAndPlayYarn(string title, string yarnSource, out Comparator comparator)
    {
        var compiler = new YarnDialogueCompiler();
        var compiled = compiler.CompileString(yarnSource, title + ".yarn", AlundraYarnFunctions.CreateDeclarations());
        Assert.False(compiled.ContainsErrors, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));

        var asset = DialogueAsset.FromCompiledProgram(title, title, compiled.ProgramBytes, compiled.LineTexts);
        comparator = new Comparator();
        return comparator.Play(asset, title);
    }

    private static string BuildSingleNodeYarn(string title, params string[] lines)
    {
        var builder = new StringBuilder();
        builder.Append("title: ").Append(title).Append('\n').Append("---\n");
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            builder.Append(line);
            if (!line.Contains("#line:", StringComparison.Ordinal))
            {
                builder.Append(" #line:").Append(title).Append("_p").Append(i);
            }

            builder.Append('\n');
        }

        builder.Append("===\n");
        return builder.ToString();
    }

    private static HashSet<string> ParseNodeTitles(byte[] programBytes)
    {
        var program = global::Yarn.Program.Parser.ParseFrom(programBytes);
        return program.Nodes is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(program.Nodes.Keys, StringComparer.Ordinal);
    }

    // =================================================================================================
    // Comparator: observes a DialogueAsset played through the real YarnDialogueRunner (E15.a) and turns
    // each shown page into the same four-part shape ReferenceTextDecoder.ReferencePage compares against.
    // =================================================================================================

    /// <summary>
    /// Plays one node of a <see cref="DialogueAsset"/> on a real <see cref="YarnDialogueRunner"/>,
    /// registering the seven functions of <see cref="AlundraYarnFunctions"/> with the exact witness
    /// values of <see cref="ReferenceTextDecoder"/> and the two commands ("flag", "falcon_update") the
    /// contract defines, and turns every delivered <see cref="DialogueLine"/> into a
    /// <see cref="ActualPage"/>: commands and calls logged since the previous line (or since the start
    /// of the node) are attributed to the line that follows them, exactly like the original decoder
    /// attributes them to the page that reads them - the same rule the T3 whole-corpus preview verified.
    /// One instance is single-use per call to <see cref="Play"/> (its logs are per-node), but is reused
    /// across nodes when the caller resets it by constructing a new one, or - for the whole-corpus test,
    /// where constructing thousands of runners would be wasteful - reused across nodes by clearing its
    /// logs before each <see cref="Play"/> call.
    /// </summary>
    private sealed class Comparator
    {
        public sealed record ActualPage(
            string Text,
            string Speaker,
            IReadOnlyList<ReferenceMarker> Markers,
            IReadOnlyList<ReferenceCommand> Commands,
            IReadOnlyList<ReferenceCall> Calls);

        private readonly List<(string Name, IReadOnlyList<string> Args)> _commandLog = new();
        private readonly List<(string Name, IReadOnlyList<string> Args)> _callLog = new();
        private readonly TestPresenter _presenter;
        private readonly YarnDialogueRunner _runner;

        public List<string> UnhandledCommands { get; } = new();

        /// <summary>Set by <see cref="Play"/>: commands/calls still logged after the node's last line.</summary>
        public int PendingCommandsAfterLastLine { get; private set; }
        public int PendingCallsAfterLastLine { get; private set; }

        /// <summary>
        /// F4: every oracle marker is self-closing (zero-length). Populated by <see cref="Play"/>
        /// (across every page of the node) whenever a delivered attribute's <c>Length</c> is not zero,
        /// as <c>"{name}@{position} length={length}"</c>.
        /// </summary>
        public List<string> NonZeroLengthMarkers { get; } = new();

        public Comparator()
        {
            _presenter = new TestPresenter(_commandLog, _callLog);
            _runner = new YarnDialogueRunner(_presenter);
            _runner.UnhandledCommand += (_, e) => UnhandledCommands.Add($"{e.Name}({string.Join(",", e.Arguments)})");

            _runner.AddCommandHandler("flag", args => _commandLog.Add(("flag", args)));
            _runner.AddCommandHandler("falcon_update", args => _commandLog.Add(("falcon_update", args)));

            _runner.RegisterFunction(AlundraYarnFunctions.FalconTemp, (Func<float>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.FalconTemp, Array.Empty<string>()));
                return ReferenceTextDecoder.FalconTempValue;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.Falcon, (Func<float>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.Falcon, Array.Empty<string>()));
                return ReferenceTextDecoder.FalconValue;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.CategoryThreshold, (Func<float>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.CategoryThreshold, Array.Empty<string>()));
                return ReferenceTextDecoder.CategoryThresholdValue;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.CategoryRemaining, (Func<float>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.CategoryRemaining, Array.Empty<string>()));
                return ReferenceTextDecoder.CategoryRemainingValue;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.CategoryItemName, (Func<string>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.CategoryItemName, Array.Empty<string>()));
                return ReferenceTextDecoder.CategoryItemNameWitness;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.CategoryItemNameBefore, (Func<string>)(() =>
            {
                _callLog.Add((AlundraYarnFunctions.CategoryItemNameBefore, Array.Empty<string>()));
                return ReferenceTextDecoder.CategoryItemNameBeforeWitness;
            }));
            _runner.RegisterFunction(AlundraYarnFunctions.GameVar, (Func<float, float>)(n =>
            {
                var i = (int)Math.Round(n, MidpointRounding.AwayFromZero);
                _callLog.Add((AlundraYarnFunctions.GameVar, new[] { i.ToString(CultureInfo.InvariantCulture) }));
                return ReferenceTextDecoder.GameVarBase + i;
            }));
        }

        /// <summary>
        /// Starts <paramref name="title"/> on <paramref name="asset"/> and drives the dialogue to
        /// completion, returning one <see cref="ActualPage"/> per delivered line, in order. Asserts
        /// nothing itself (the caller compares); it does clear its own logs first, so this can be called
        /// repeatedly on the same instance for different nodes/assets.
        /// </summary>
        public IReadOnlyList<ActualPage> Play(DialogueAsset asset, string title)
        {
            _commandLog.Clear();
            _callLog.Clear();
            _presenter.Pages.Clear();
            NonZeroLengthMarkers.Clear();

            var started = _runner.Start(asset, title);
            Assert.True(started, $"YarnDialogueRunner failed to start node '{title}'.");

            while (_runner.IsRunning)
            {
                _runner.Continue();
            }

            // No pending command/call after the node's last line is part of the contract (T6, step 3):
            // recorded rather than asserted here, so a caller iterating the whole corpus can report every
            // offending node instead of stopping at the first one.
            PendingCommandsAfterLastLine = _commandLog.Count;
            PendingCallsAfterLastLine = _callLog.Count;

            var result = new List<ActualPage>(_presenter.Pages.Count);
            foreach (var captured in _presenter.Pages)
            {
                result.Add(BuildActualPage(captured));
            }

            return result;
        }

        public string ToCanonicalString(ActualPage page)
            => new ReferencePage(page.Text, page.Markers, page.Commands, page.Calls).ToCanonicalString();

        /// <summary>
        /// Builds the oracle quadruple from one captured <see cref="DialogueLine"/>: "br" attributes are
        /// rendered as a real newline at their position (later positions shift accordingly, since a
        /// "\n" widens the text by one code unit), the other attributes become <see cref="ReferenceMarker"/>s
        /// in list order (Yarn keeps source order for several zero-length attributes at the same
        /// position, per T3's whole-corpus preview), and the logged commands/calls become
        /// <see cref="ReferenceCommand"/>/<see cref="ReferenceCall"/> in log order.
        ///
        /// F4: a marker's properties are built from ALL of the attribute's properties except
        /// "trimwhitespace" (never normalised away to just "id"), so an extra or renamed property shows
        /// up as a canonical-string mismatch; a non-zero <c>Length</c> is recorded into
        /// <see cref="NonZeroLengthMarkers"/> instead, since every oracle marker is self-closing.
        /// </summary>
        private ActualPage BuildActualPage(TestPresenter.CapturedPage captured)
        {
            var text = new StringBuilder(captured.Line.Text);
            var markers = new List<ReferenceMarker>();
            var shift = 0;

            foreach (var attribute in captured.Line.Attributes)
            {
                var position = attribute.Position + shift;

                if (attribute.Length != 0)
                {
                    NonZeroLengthMarkers.Add($"{attribute.Name}@{position} length={attribute.Length}");
                }

                switch (attribute.Name)
                {
                    case "br":
                        text.Insert(position, '\n');
                        shift += 1;
                        break;
                    case "voice":
                    case "center":
                    case "slow":
                    case "glyph":
                    case "empty":
                        markers.Add(new ReferenceMarker(attribute.Name, position, BuildMarkerProperties(attribute.Properties)));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"unexpected marker '{attribute.Name}' at position {attribute.Position}");
                }
            }

            var commands = new List<ReferenceCommand>();
            foreach (var (name, args) in captured.Commands)
            {
                commands.Add(name switch
                {
                    "flag" => ReferenceCommand.Flag(uint.Parse(args[0], CultureInfo.InvariantCulture)),
                    "falcon_update" => ReferenceCommand.FalconUpdate,
                    _ => throw new InvalidOperationException($"unexpected command '{name}'"),
                });
            }

            var calls = new List<ReferenceCall>();
            foreach (var (name, args) in captured.Calls)
            {
                calls.Add(name switch
                {
                    "falcon_temp" => ReferenceCall.FalconTemp,
                    "falcon" => ReferenceCall.Falcon,
                    "category_threshold" => ReferenceCall.CategoryThreshold,
                    "category_remaining" => ReferenceCall.CategoryRemaining,
                    "category_item_name" => ReferenceCall.CategoryItemName,
                    "category_item_name_before" => ReferenceCall.CategoryItemNameBefore,
                    "game_var" => ReferenceCall.GameVar(int.Parse(args[0], CultureInfo.InvariantCulture)),
                    _ => throw new InvalidOperationException($"unexpected call '{name}'"),
                });
            }

            return new ActualPage(text.ToString(), captured.Line.Speaker, markers, commands, calls);
        }

        /// <summary>
        /// F4: every property of a delivered attribute except "trimwhitespace" (a rendering-only flag
        /// the emitter always sets, never part of the oracle contract), formatted the same way
        /// <see cref="ReferenceMarker"/>'s own factories format "id": int/float with
        /// <see cref="CultureInfo.InvariantCulture"/>, bool as "true"/"false", string as is.
        /// </summary>
        private static IReadOnlyDictionary<string, string> BuildMarkerProperties(IReadOnlyDictionary<string, object> properties)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in properties)
            {
                if (string.Equals(key, "trimwhitespace", StringComparison.Ordinal))
                {
                    continue;
                }

                result[key] = FormatPropertyValue(value);
            }

            return result;
        }

        private static string FormatPropertyValue(object value) => value switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };
    }

    /// <summary>
    /// Presenter that snapshots the command/call logs into each shown line then clears them, so the
    /// commands and calls attributed to page k are exactly those that ran between page k-1's line
    /// (or the start of the node) and page k's line.
    /// </summary>
    private sealed class TestPresenter : IDialoguePresenter
    {
        public sealed record CapturedPage(
            DialogueLine Line,
            List<(string Name, IReadOnlyList<string> Args)> Commands,
            List<(string Name, IReadOnlyList<string> Args)> Calls);

        private readonly List<(string Name, IReadOnlyList<string> Args)> _commandLog;
        private readonly List<(string Name, IReadOnlyList<string> Args)> _callLog;

        public List<CapturedPage> Pages { get; } = new();

        public TestPresenter(
            List<(string Name, IReadOnlyList<string> Args)> commandLog,
            List<(string Name, IReadOnlyList<string> Args)> callLog)
        {
            _commandLog = commandLog;
            _callLog = callLog;
        }

        public DialogueRuntimeState State { get; private set; }
        public DialogueLine CurrentLine { get; private set; } = DialogueLine.Empty;
        public bool IsOpen { get; private set; }
        public IReadOnlyList<string> Choices => Array.Empty<string>();
        public bool HasChoices => false;

        public event EventHandler<DialoguePresentationChangedEventArgs>? PresentationChanged { add { } remove { } }
        public event EventHandler<DialogueChoiceSelectedEventArgs>? ChoiceSelected { add { } remove { } }

        public bool ShowLine(DialogueLine line)
        {
            Pages.Add(new CapturedPage(
                line,
                new List<(string, IReadOnlyList<string>)>(_commandLog),
                new List<(string, IReadOnlyList<string>)>(_callLog)));
            _commandLog.Clear();
            _callLog.Clear();
            CurrentLine = line;
            IsOpen = true;
            State = DialogueRuntimeState.Open;
            return true;
        }

        public bool ShowChoices(IReadOnlyList<string> labels) => false;

        public bool SelectChoice(int index) => false;

        public bool Close()
        {
            var wasOpen = IsOpen;
            IsOpen = false;
            CurrentLine = DialogueLine.Empty;
            State = DialogueRuntimeState.Closed;
            return wasOpen;
        }
    }

    // =================================================================================================
    // Fixture: exports the whole corpus once (real export chain, temp project), loads every catalogued
    // .dialogue back from disk, and independently reads the corpus itself to build the expected
    // title -> ReferencePage list map. Shared by the whole-corpus test and every named case.
    // =================================================================================================

    public sealed class CorpusFixture : IDisposable
    {
        private readonly string? _outputDirectory;

        public bool Available { get; }
        public IReadOnlyList<LoadedAsset> Assets { get; } = Array.Empty<LoadedAsset>();
        public IReadOnlyDictionary<string, IReadOnlyList<ReferencePage>> ExpectedByTitle { get; }
            = new Dictionary<string, IReadOnlyList<ReferencePage>>();

        /// <summary>F5: the ETC index table (<c>EtcIndexTable.csv</c>) is expected to have 1024 entries.</summary>
        public int EtcIndexEntryCount { get; }

        public sealed record LoadedAsset(DialogueAsset Dialogue, IReadOnlyList<string> NodeTitles, string CatalogName);

        public CorpusFixture()
        {
            var dataExtractedDirectory = FindRealDataExtractedDirectory();
            var mapsJsonPath = Path.Combine(AppContext.BaseDirectory, "maps.json");
            if (dataExtractedDirectory is null || !File.Exists(mapsJsonPath))
            {
                Available = false;
                return;
            }

            // ---- Original side: read the corpus ourselves, independently of the emitter/writer. ----
            var expected = new Dictionary<string, IReadOnlyList<ReferencePage>>(StringComparer.Ordinal);
            var dataDirectory = Path.Combine(dataExtractedDirectory, "data");

            var mapFileRegex = new Regex(@"^map_(\d+)\.json$", RegexOptions.Compiled);
            foreach (var path in Directory.EnumerateFiles(dataDirectory, "map_*.json"))
            {
                var match = mapFileRegex.Match(Path.GetFileName(path));
                if (!match.Success)
                {
                    continue; // excludes map_alundra.json
                }

                var mapId = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var strings = StringTableReader.ReadMapStrings(path);
                for (var i = 0; i < strings.Count; i++)
                {
                    var value = strings[i];
                    if (!string.IsNullOrEmpty(value))
                    {
                        expected[$"M{mapId}_S{i:000}"] = ReferenceTextDecoder.Decode(value);
                    }
                }
            }

            var sharedStrings = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_alundra.json"));
            for (var i = 0; i < sharedStrings.Count; i++)
            {
                var value = sharedStrings[i];
                if (!string.IsNullOrEmpty(value))
                {
                    expected[$"Shared_S{i:000}"] = ReferenceTextDecoder.Decode(value);
                }
            }

            var etcCsvPath = Path.Combine(AppContext.BaseDirectory, "EtcIndexTable.csv");
            var etcIndex = EtcIndexCatalogReader.Read(etcCsvPath);
            EtcIndexEntryCount = etcIndex.ValueByIndex.Count;
            var globalEntries = StringTableReader.ReadGlobalTable(Path.Combine(dataDirectory, "ETC_RES.R.json"));
            var valueByOffset = new Dictionary<int, string?>();
            foreach (var entry in globalEntries)
            {
                if (entry.Offset is int offset)
                {
                    valueByOffset[offset] = entry.Value;
                }
            }

            for (var index = 0; index < etcIndex.ValueByIndex.Count; index++)
            {
                var offset = etcIndex.ValueByIndex[index];
                if (valueByOffset.TryGetValue(offset, out var value) && !string.IsNullOrEmpty(value))
                {
                    expected[$"Etc_{index:0000}"] = ReferenceTextDecoder.Decode(value);
                }
            }

            ExpectedByTitle = expected;

            // ---- Compiled side: the real export chain, into a temp project. ----
            var outputDirectory = Path.Combine(
                Path.GetTempPath(), "AlundraCasaEngineConverterTests", "YarnCorpusEquivalence", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);
            _outputDirectory = outputDirectory;

            var previousProjectPath = EngineEnvironment.ProjectPath;
            try
            {
                var mapCatalog = MapCatalogReader.Read(mapsJsonPath);

                EngineEnvironment.ProjectPath = outputDirectory;
                EditorAssetCatalogService.Clear();

                var report = new ConversionReport();
                YarnDialogueWriter.ConvertDialogues(dataExtractedDirectory, outputDirectory, null, mapCatalog.Locations, report);

                if (report.Errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "YarnDialogueWriter.ConvertDialogues reported error(s): " + string.Join(" | ", report.Errors));
                }

                var assets = new List<LoadedAsset>();
                foreach (var info in EditorAssetCatalogService.AssetInfos)
                {
                    if (!info.FileName.EndsWith(Constants.FileNameExtensions.Dialogue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(outputDirectory, info.FileName);
                    var document = JObject.Parse(File.ReadAllText(fullPath));
                    var asset = new DialogueAsset();
                    asset.Load(document);

                    var program = global::Yarn.Program.Parser.ParseFrom(asset.ProgramBytes);
                    var titles = program.Nodes is null
                        ? Array.Empty<string>()
                        : program.Nodes.Keys.ToArray();

                    assets.Add(new LoadedAsset(asset, titles, info.Name));
                }

                Assets = assets;
            }
            finally
            {
                EditorAssetCatalogService.Clear();
                EngineEnvironment.ProjectPath = previousProjectPath;
            }

            Available = true;
        }

        public string FindTitleStartingWith(string prefix)
        {
            var title = ExpectedByTitle.Keys.FirstOrDefault(t => t.StartsWith(prefix, StringComparison.Ordinal));
            if (title is null)
            {
                throw new InvalidOperationException($"No node title starts with '{prefix}' in the exported corpus.");
            }

            return title;
        }

        public DialogueAsset FindAssetForTitle(string title)
        {
            foreach (var asset in Assets)
            {
                if (asset.NodeTitles.Contains(title, StringComparer.Ordinal))
                {
                    return asset.Dialogue;
                }
            }

            throw new InvalidOperationException($"No exported asset contains node '{title}'.");
        }

        private static string? FindRealDataExtractedDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "data-extracted");
                if (File.Exists(Path.Combine(candidate, "data", "ETC_RES.R.json")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        public void Dispose()
        {
            if (_outputDirectory is not null && Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, recursive: true);
            }
        }
    }
}
