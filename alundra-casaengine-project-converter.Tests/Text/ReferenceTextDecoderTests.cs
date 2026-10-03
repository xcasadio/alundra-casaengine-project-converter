using System.Globalization;
using AlundraCasaEngineProjectConverter.Readers;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests.Text;

/// <summary>
/// Covers <see cref="ReferenceTextDecoder"/> against the E15.b/T2 contract (docs/plan-e15-yarn.md):
/// one test per control-code rule, one per named case, a guard that the named-case sources really
/// are what data-extracted holds, and a census over the whole corpus. The corpus-dependent tests
/// skip (rather than fail) when data-extracted/ is not present next to the built test binaries -
/// same convention as AnimationEndClassifierTests.
/// </summary>
public class ReferenceTextDecoderTests
{
    // ---------------------------------------------------------------------------------------
    // Page split
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_SplitsPagesOnBackslashA()
    {
        var pages = ReferenceTextDecoder.Decode("a\\Ab\\Ac");
        Assert.Equal(3, pages.Count);
        Assert.Equal("a", pages[0].Text);
        Assert.Equal("b", pages[1].Text);
        Assert.Equal("c", pages[2].Text);
    }

    [Fact]
    public void Decode_TrailingBackslashA_YieldsFinalEmptyPage()
    {
        var pages = ReferenceTextDecoder.Decode("a\\A");
        Assert.Equal(2, pages.Count);
        Assert.Equal("a", pages[0].Text);
        Assert.Equal("", pages[1].Text);
        Assert.Equal("empty", Assert.Single(pages[1].Markers).Name);
    }

    [Fact]
    public void Decode_LeadingBackslashA_YieldsEmptyFirstPage()
    {
        var pages = ReferenceTextDecoder.Decode("\\Aa");
        Assert.Equal(2, pages.Count);
        Assert.Equal("", pages[0].Text);
        Assert.Equal("empty", Assert.Single(pages[0].Markers).Name);
        Assert.Equal("a", pages[1].Text);
    }

    [Fact]
    public void Decode_NullOrEmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => ReferenceTextDecoder.Decode(null!));
        Assert.Throws<ArgumentException>(() => ReferenceTextDecoder.Decode(""));
    }

    // ---------------------------------------------------------------------------------------
    // \N
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_BackslashN_BecomesRealNewlineInText()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\Nb"));
        Assert.Equal("a\nb", page.Text);
    }

    // ---------------------------------------------------------------------------------------
    // Numeric codes
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("a\\0999b", "999")]
    [InlineData("a\\999b", "999")]
    [InlineData("a\\0100b", "100")]
    public void Decode_NumericCode_NormalisesValue(string source, string expectedValue)
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(source));
        Assert.Empty(page.Commands);
        Assert.Equal("ab", page.Text);
        var marker = Assert.Single(page.Markers);
        Assert.Equal("flag", marker.Name);
        Assert.Equal(1, marker.Position);
        Assert.Equal(expectedValue, marker.Properties["id"]);
    }

    [Fact]
    public void Decode_NumericCode_IsGreedy()
    {
        // "\12345a" is one flag(12345) then the visible text "a", not "\1" + "2345a".
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\12345a"));
        Assert.Equal("a", page.Text);
        Assert.Equal("flag@0 id=12345", Assert.Single(page.Markers).ToCanonicalString());
    }

    [Fact]
    public void Decode_SeveralFlags_KeepSourceOrderAtTheirPositions()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\5b\\3c"));
        Assert.Equal("abc", page.Text);
        Assert.Equal(new[] { "flag@1 id=5", "flag@2 id=3" }, page.Markers.Select(m => m.ToCanonicalString()));
    }

    [Fact]
    public void Decode_FlagsAndYieldsAtTheSamePosition_KeepSourceOrder()
    {
        // M389_S106 p0 ends with "\1004\Y\999\Y".
        var page = Assert.Single(ReferenceTextDecoder.Decode("Fin ?\\1004\\Y\\999\\Y"));
        Assert.Equal("Fin ?", page.Text);
        Assert.Equal(
            new[] { "flag@5 id=1004", "yield@5", "flag@5 id=999", "yield@5" },
            page.Markers.Select(m => m.ToCanonicalString()));
    }

    [Fact]
    public void Decode_NumericCodeOverflow_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("\\99999999999999999999"));
    }

    [Fact]
    public void Decode_NumericCode_ShouldFitAnInt()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\2147483647"));
        Assert.Equal("flag@0 id=2147483647", page.Markers[0].ToCanonicalString());
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("\\2147483648"));
    }

    [Fact]
    public void Decode_FlagIsTransparentToTheEdgeSpaceTrim()
    {
        // F0-R2: the space between the last visible unit and a flag is still removed (M389_S022 p0),
        // and so is the one between a leading flag and the first visible unit.
        var trailing = Assert.Single(ReferenceTextDecoder.Decode("on \\999\\Y"));
        Assert.Equal("on", trailing.Text);
        Assert.Equal(new[] { "flag@2 id=999", "yield@2" }, trailing.Markers.Select(m => m.ToCanonicalString()));

        var leading = Assert.Single(ReferenceTextDecoder.Decode(" \\201 Tu"));
        Assert.Equal("Tu", leading.Text);
        Assert.Equal("flag@0 id=201", Assert.Single(leading.Markers).ToCanonicalString());

        var interior = Assert.Single(ReferenceTextDecoder.Decode("a \\5 b"));
        Assert.Equal("a  b", interior.Text);
        Assert.Equal("flag@2 id=5", Assert.Single(interior.Markers).ToCanonicalString());
    }

    // ---------------------------------------------------------------------------------------
    // Command order (\X vs numeric)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_XThenFlag_FalconUpdateCommandAndFlagAtItsPosition()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X1 a \\0100"));
        Assert.Equal(new[] { "falcon_update" }, page.Commands.Select(c => c.ToCanonicalString()));
        Assert.Equal("90002 a", page.Text);
        Assert.Equal("flag@7 id=100", Assert.Single(page.Markers).ToCanonicalString());
    }

    [Fact]
    public void Decode_FlagThenX_FalconUpdateCommandAndFlagAtItsPosition()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\0100 a \\X1"));
        Assert.Equal(new[] { "falcon_update" }, page.Commands.Select(c => c.ToCanonicalString()));
        Assert.Equal("a 90002", page.Text);
        Assert.Equal("flag@0 id=100", Assert.Single(page.Markers).ToCanonicalString());
    }

    // ---------------------------------------------------------------------------------------
    // \Y
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_BackslashY_BecomesAYieldMarkerAtItsPosition()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\Yb"));
        Assert.Equal("ab", page.Text);
        Assert.Empty(page.Commands);
        Assert.Equal("yield@1", Assert.Single(page.Markers).ToCanonicalString());
    }

    // ---------------------------------------------------------------------------------------
    // \W operands
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData('0', 16)]
    [InlineData('2', 18)]
    [InlineData('3', 19)]
    [InlineData('4', 20)]
    [InlineData('5', 21)]
    [InlineData('6', 22)]
    [InlineData('7', 23)]
    [InlineData('8', 24)]
    [InlineData('9', 25)]
    [InlineData('A', 26)]
    [InlineData('D', 29)]
    public void Decode_BackslashW_ComputesGlyphIdFromOperand(char operand, int expectedGlyphId)
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode($"a\\W{operand}b"));
        var marker = Assert.Single(page.Markers);
        Assert.Equal(ReferenceMarker.Glyph(expectedGlyphId, 1).ToCanonicalString(), marker.ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    [Fact]
    public void Decode_BackslashW_MissingOperand_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\W"));
    }

    [Fact]
    public void Decode_BackslashW_InvalidOperand_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\W#b"));
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\Waa"));
    }

    // ---------------------------------------------------------------------------------------
    // Raw control bytes
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_RawUnitSeparator_IsGlyph26()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\u001Ab"));
        var marker = Assert.Single(page.Markers);
        Assert.Equal(ReferenceMarker.Glyph(26, 1).ToCanonicalString(), marker.ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    [Fact]
    public void Decode_RawFileSeparator_IsGlyph28()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\u001Cb"));
        var marker = Assert.Single(page.Markers);
        Assert.Equal(ReferenceMarker.Glyph(28, 1).ToCanonicalString(), marker.ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    [Fact]
    public void Decode_OtherRawControlCharacter_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\nb"));
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\rb"));
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\tb"));
    }

    // ---------------------------------------------------------------------------------------
    // \X rules
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_X0_FirstOfPage_IsFalconTempWitness()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X0"));
        Assert.Equal("90001", page.Text);
        Assert.Equal("falcon_update", Assert.Single(page.Commands).Name);
        Assert.Equal("falcon_temp", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_X1_IsFalconWitness()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X1"));
        Assert.Equal("90002", page.Text);
        Assert.Equal("falcon", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_X2_FirstOfPage_IsCategoryItemNameBefore()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X2"));
        Assert.Equal(ReferenceTextDecoder.CategoryItemNameBeforeWitness, page.Text);
        Assert.Equal("category_item_name_before", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_X4_FirstOfPage_IsCategoryItemNameBefore()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X4"));
        Assert.Equal(ReferenceTextDecoder.CategoryItemNameBeforeWitness, page.Text);
        Assert.Equal("category_item_name_before", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_X2_AfterAnotherX_IsCategoryItemName()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X1\\X2"));
        Assert.Equal(new[] { "falcon", "category_item_name" }, page.Calls.Select(c => c.Name));
        Assert.EndsWith(ReferenceTextDecoder.CategoryItemNameWitness, page.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_X4_AfterX3_IsCategoryItemName()
    {
        // Named case 8's shape: \X3 then \X4.
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X3\\X4"));
        Assert.Equal(new[] { "category_threshold", "category_item_name" }, page.Calls.Select(c => c.Name));
    }

    [Fact]
    public void Decode_X3_IsCategoryThresholdWitness()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X3"));
        Assert.Equal("90003", page.Text);
        Assert.Equal("category_threshold", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_X5_IsCategoryRemainingWitness()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X5"));
        Assert.Equal("90004", page.Text);
        Assert.Equal("category_remaining", Assert.Single(page.Calls).Name);
    }

    [Fact]
    public void Decode_SeveralX_GetExactlyOneFalconUpdate()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\X0\\X1\\X3"));
        Assert.Single(page.Commands, c => c.Name == "falcon_update");
    }

    [Fact]
    public void Decode_FalconUpdatedFlag_ResetsPerPage()
    {
        // \X0 opens each page, so it must not throw on the second page even though the first
        // page's \X already updated the falcon state.
        var pages = ReferenceTextDecoder.Decode("\\X0\\A\\X0");
        Assert.Equal(2, pages.Count);
        Assert.Equal("falcon_temp", Assert.Single(pages[0].Calls).Name);
        Assert.Equal("falcon_temp", Assert.Single(pages[1].Calls).Name);

        // Each page runs its own update: one falcon_update per page, not one per string.
        Assert.Equal("falcon_update", Assert.Single(pages[0].Commands).ToCanonicalString());
        Assert.Equal("falcon_update", Assert.Single(pages[1].Commands).ToCanonicalString());
    }

    [Fact]
    public void Decode_X0AfterAnotherX_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("\\X1\\X0"));
    }

    [Fact]
    public void Decode_BadXOperand_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("\\X9"));
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("\\X"));
    }

    // ---------------------------------------------------------------------------------------
    // \V
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_BackslashV_CallsGameVarWithWitness()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\V3b"));
        Assert.Equal("a91003b", page.Text);
        var call = Assert.Single(page.Calls);
        Assert.Equal("game_var", call.Name);
        Assert.Equal("3", Assert.Single(call.Arguments));
    }

    [Fact]
    public void Decode_BackslashV_MissingOrInvalidOperand_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\V"));
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\VXb"));
    }

    // ---------------------------------------------------------------------------------------
    // Voice, center, slow
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData('B', -1)]
    [InlineData('C', 0)]
    [InlineData('D', 1)]
    [InlineData('E', 2)]
    [InlineData('F', 3)]
    [InlineData('G', 4)]
    public void Decode_VoiceCodes_MapToTheirId(char code, int expectedId)
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode($"a\\{code}b"));
        var marker = Assert.Single(page.Markers);
        Assert.Equal(ReferenceMarker.Voice(expectedId, 1).ToCanonicalString(), marker.ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    [Fact]
    public void Decode_BackslashH_IsCenterMarker()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\Hb"));
        Assert.Equal(ReferenceMarker.Center(1).ToCanonicalString(), Assert.Single(page.Markers).ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    [Fact]
    public void Decode_BackslashT_IsSlowMarker()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a\\Tb"));
        Assert.Equal(ReferenceMarker.Slow(1).ToCanonicalString(), Assert.Single(page.Markers).ToCanonicalString());
        Assert.Equal("ab", page.Text);
    }

    // ---------------------------------------------------------------------------------------
    // Plain visible text
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_ColonAndHash_AreKeptAsVisibleText()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("a : #Disuse"));
        Assert.Equal("a : #Disuse", page.Text);
    }

    [Fact]
    public void Decode_BracketsAngleBracketsAndSlashes_AreKeptAsVisibleText()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("[a] <<b>> //c"));
        Assert.Equal("[a] <<b>> //c", page.Text);
    }

    // ---------------------------------------------------------------------------------------
    // Edge spaces (D-E15-8)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_EdgeSpaces_BothEndsTrimmed()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("  abc  "));
        Assert.Equal("abc", page.Text);
    }

    [Fact]
    public void Decode_EdgeSpaces_TrailingNumericCodeDoesNotProtectTheSpaceBeforeIt()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("abc \\999"));
        Assert.Equal("abc", page.Text);
    }

    [Fact]
    public void Decode_EdgeSpaces_LeadingSpaceBeforeMarkerIsRemoved()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(" \\Cabc"));
        Assert.Equal("abc", page.Text);
        Assert.Equal(0, Assert.Single(page.Markers).Position);
    }

    [Fact]
    public void Decode_EdgeSpaces_SpaceAfterMarkerIsNotAtTheEdge()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\C abc"));
        Assert.Equal(" abc", page.Text);
        Assert.Equal(0, Assert.Single(page.Markers).Position);
    }

    [Fact]
    public void Decode_EdgeSpaces_TrailingLineBreakIsNeverRemoved()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("abc \\N"));
        Assert.Equal("abc \n", page.Text);
    }

    [Fact]
    public void Decode_EdgeSpaces_SpaceBeforeTrailingLineBreakIsRemoved()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("abc\\N "));
        Assert.Equal("abc\n", page.Text);
    }

    [Fact]
    public void Decode_EdgeSpaces_FinalLineBreakIsKept()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("Merci,\\NAlundra !\\N"));
        Assert.Equal("Merci,\nAlundra !\n", page.Text);
    }

    // ---------------------------------------------------------------------------------------
    // Empty pages
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_EmptyPage_OnlySpaces_IsEmptyMarker()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("   "));
        Assert.Equal("", page.Text);
        var marker = Assert.Single(page.Markers);
        Assert.Equal("empty", marker.Name);
        Assert.Equal(0, marker.Position);
    }

    [Fact]
    public void Decode_EmptyPage_FlagsOnly_KeepsTheirMarkersThenEmpty()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\999\\Y"));
        Assert.Equal("", page.Text);
        Assert.Equal(
            new[] { "flag@0 id=999", "yield@0", "empty@0" },
            page.Markers.Select(m => m.ToCanonicalString()));
        Assert.Empty(page.Commands);
    }

    [Fact]
    public void Decode_PageWithOnlyAMarker_IsNotEmpty()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode("\\H"));
        Assert.Equal("", page.Text);
        Assert.Equal("center", Assert.Single(page.Markers).Name);
    }

    // ---------------------------------------------------------------------------------------
    // Exception paths
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_BackslashM_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\Mb"));
    }

    [Fact]
    public void Decode_UnknownLetterCode_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a\\Zb"));
    }

    [Fact]
    public void Decode_LoneTrailingBackslash_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("abc\\"));
    }

    [Fact]
    public void Decode_OpenBrace_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a{b"));
    }

    [Fact]
    public void Decode_CloseBrace_Throws()
    {
        Assert.Throws<ReferenceTextDecoderException>(() => ReferenceTextDecoder.Decode("a}b"));
    }

    [Fact]
    public void Decode_ErrorMessage_NamesTheCodeAndQuotesOnlyItsPage()
    {
        var exception = Assert.Throws<ReferenceTextDecoderException>(
            () => ReferenceTextDecoder.Decode("first\\Asecond\\Mx\\Athird"));

        Assert.Contains("'\\M'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("page 1 'second\\Mx'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("first", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("third", exception.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Named cases (docs/plan-e15-yarn.md, E15.b/T2)
    // ---------------------------------------------------------------------------------------

    // Source strings below are copied verbatim from data-extracted (see Decode_NamedCaseSources_MatchDataExtracted).

    private const string Case1_M323_S095_p4_Source =
        "Tu en as rencontré un\\W2 Nirude.\\NLes Gazeck ont été taillés dans la pierre\\Npar d'anciens humains.";

    private const string Case2_M135_S090_p3_Source =
        "Pis encore : notre relation avec les dieux\\Na été\\401\\Ydétruite.\\NMais\\T\\W2\\T\\W2\\Til n'est pas trop tard pour";

    private const string Case3_M324_S041_p0_Source =
        "\\B\\HMain gauche : non opérationnelle\\N\\HMain droite : fonctionnelle";

    private const string Case4_M323_S097_FullSource =
        "\\DMelzas va bientôt détruire\\Ncet endroit.\\A\\999\\Y\\APars d'ici, Alundra ! Fais\\Nvite ! La destruction arrive !\\N";

    private const string Case5_M134_S016_p0_Source =
        "\\CJe pense que tu trouveras ce(t) \\X2\\Ntout à fait utile. Fais-en bon usage,\\NAlundra !";

    private const string Case6_M472_S011_p0_Source =
        "\\CTemps écoulé ! La partie est finie, mec !\\NComptons tes points\\W2";

    private const string Case6_M472_S011_p1_Source =
        "Tu as \\V0 cibles et\\V1\\Npénalités pour un total de \\V2.";

    private const string Case7_Etc0770_Source = "Charger en appuyant sur \u001A.";

    private const string Case7_Etc0140_Source = "appuie sur la Touche \u001C.";

    private const string Case8_M134_S019_p0_Source =
        "\\CRamène-moi \\X3 Statuettes de faucons\\Net je te récompenserai avec cela :\\N\\X4.\\0100\\Y";

    private const string Case9_M134_S012_p1_Source = "Je suis un homme heureux. Merci,\\NAlundra !\\N";

    [Fact]
    public void NamedCase1_M323_S095_p4()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case1_M323_S095_p4_Source));

        var expected = new ReferencePage(
            "Tu en as rencontré un Nirude.\nLes Gazeck ont été taillés dans la pierre\npar d'anciens humains.",
            new[] { ReferenceMarker.Glyph(18, 21) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());

        Assert.Equal(94, page.Text.Length);
        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    [Fact]
    public void NamedCase2_M135_S090_p3()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case2_M135_S090_p3_Source));

        var expected = new ReferencePage(
            "Pis encore : notre relation avec les dieux\na étédétruite.\nMaisil n'est pas trop tard pour",
            new[]
            {
                ReferenceMarker.Flag(401, 48),
                ReferenceMarker.Yield(48),
                ReferenceMarker.Slow(62),
                ReferenceMarker.Glyph(18, 62),
                ReferenceMarker.Slow(62),
                ReferenceMarker.Glyph(18, 62),
                ReferenceMarker.Slow(62),
            },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());

        Assert.Equal(89, page.Text.Length);
        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    [Fact]
    public void NamedCase3_M324_S041_p0()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case3_M324_S041_p0_Source));

        var expected = new ReferencePage(
            "Main gauche : non opérationnelle\nMain droite : fonctionnelle",
            new[]
            {
                ReferenceMarker.Voice(-1, 0),
                ReferenceMarker.Center(0),
                ReferenceMarker.Center(33),
            },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());

        Assert.Equal(60, page.Text.Length);
        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    [Fact]
    public void NamedCase4_M323_S097()
    {
        var pages = ReferenceTextDecoder.Decode(Case4_M323_S097_FullSource);
        Assert.Equal(3, pages.Count);

        var expectedP0 = new ReferencePage(
            "Melzas va bientôt détruire\ncet endroit.",
            new[] { ReferenceMarker.Voice(1, 0) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expectedP0.ToCanonicalString(), pages[0].ToCanonicalString());

        var expectedP1 = new ReferencePage(
            "",
            new[] { ReferenceMarker.Flag(999, 0), ReferenceMarker.Yield(0), ReferenceMarker.Empty(0) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expectedP1.ToCanonicalString(), pages[1].ToCanonicalString());

        var expectedP2 = new ReferencePage(
            "Pars d'ici, Alundra ! Fais\nvite ! La destruction arrive !\n",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(58, pages[2].Text.Length);
        Assert.Equal(expectedP2.ToCanonicalString(), pages[2].ToCanonicalString());
    }

    [Fact]
    public void NamedCase5_M134_S016_p0()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case5_M134_S016_p0_Source));

        var expected = new ReferencePage(
            $"Je pense que tu trouveras ce(t) {ReferenceTextDecoder.CategoryItemNameBeforeWitness}\ntout à fait utile. Fais-en bon usage,\nAlundra !",
            new[] { ReferenceMarker.Voice(0, 0) },
            new[] { ReferenceCommand.FalconUpdate },
            new[] { ReferenceCall.CategoryItemNameBefore });

        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    [Fact]
    public void NamedCase6_M472_S011()
    {
        var p0 = Assert.Single(ReferenceTextDecoder.Decode(Case6_M472_S011_p0_Source));
        var expectedP0 = new ReferencePage(
            "Temps écoulé ! La partie est finie, mec !\nComptons tes points",
            new[] { ReferenceMarker.Voice(0, 0), ReferenceMarker.Glyph(18, 61) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(61, p0.Text.Length);
        Assert.Equal(expectedP0.ToCanonicalString(), p0.ToCanonicalString());

        var p1 = Assert.Single(ReferenceTextDecoder.Decode(Case6_M472_S011_p1_Source));
        var expectedP1 = new ReferencePage(
            "Tu as 91000 cibles et91001\npénalités pour un total de 91002.",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            new[] { ReferenceCall.GameVar(0), ReferenceCall.GameVar(1), ReferenceCall.GameVar(2) });
        Assert.Equal(expectedP1.ToCanonicalString(), p1.ToCanonicalString());
    }

    [Fact]
    public void NamedCase7_EtcGlyphs()
    {
        var etc770 = Assert.Single(ReferenceTextDecoder.Decode(Case7_Etc0770_Source));
        var expected770 = new ReferencePage(
            "Charger en appuyant sur .",
            new[] { ReferenceMarker.Glyph(26, 24) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expected770.ToCanonicalString(), etc770.ToCanonicalString());

        var etc140 = Assert.Single(ReferenceTextDecoder.Decode(Case7_Etc0140_Source));
        var expected140 = new ReferencePage(
            "appuie sur la Touche .",
            new[] { ReferenceMarker.Glyph(28, 21) },
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());
        Assert.Equal(expected140.ToCanonicalString(), etc140.ToCanonicalString());
    }

    [Fact]
    public void NamedCase8_M134_S019_p0()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case8_M134_S019_p0_Source));

        var expected = new ReferencePage(
            $"Ramène-moi 90003 Statuettes de faucons\net je te récompenserai avec cela :\n{ReferenceTextDecoder.CategoryItemNameWitness}.",
            new[] { ReferenceMarker.Voice(0, 0), ReferenceMarker.Flag(100, 82), ReferenceMarker.Yield(82) },
            new[] { ReferenceCommand.FalconUpdate },
            new[] { ReferenceCall.CategoryThreshold, ReferenceCall.CategoryItemName });

        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    [Fact]
    public void NamedCase9_M134_S012_p1()
    {
        var page = Assert.Single(ReferenceTextDecoder.Decode(Case9_M134_S012_p1_Source));

        var expected = new ReferencePage(
            "Je suis un homme heureux. Merci,\nAlundra !\n",
            Array.Empty<ReferenceMarker>(),
            Array.Empty<ReferenceCommand>(),
            Array.Empty<ReferenceCall>());

        Assert.Equal(expected.ToCanonicalString(), page.ToCanonicalString());
    }

    // ---------------------------------------------------------------------------------------
    // Source guard: the hand-copied sources above really are what data-extracted holds.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_NamedCaseSources_MatchDataExtracted()
    {
        var dataDirectory = FindRealDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        var map323 = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_323.json"));
        var map135 = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_135.json"));
        var map324 = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_324.json"));
        var map134 = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_134.json"));
        var map472 = StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_472.json"));

        Assert.Equal(Case1_M323_S095_p4_Source, SplitPages(map323[95]!)[4]);
        Assert.Equal(Case2_M135_S090_p3_Source, SplitPages(map135[90]!)[3]);
        Assert.Equal(Case3_M324_S041_p0_Source, map324[41]);
        Assert.Equal(Case4_M323_S097_FullSource, map323[97]);
        Assert.Equal(Case5_M134_S016_p0_Source, map134[16]);

        var map472Pages = SplitPages(map472[11]!);
        Assert.Equal(Case6_M472_S011_p0_Source, map472Pages[0]);
        Assert.Equal(Case6_M472_S011_p1_Source, map472Pages[1]);

        var map134S012Pages = SplitPages(map134[12]!);
        Assert.Equal(Case9_M134_S012_p1_Source, map134S012Pages[1]);

        Assert.Equal(Case8_M134_S019_p0_Source, map134[19]);

        var etcIndex = EtcIndexCatalogReader.Read(RequireEtcIndexTableFile()).ValueByIndex;
        var etcOffset770 = etcIndex[770];
        var etcOffset140 = etcIndex[140];

        var globalStrings = StringTableReader.ReadGlobalTable(Path.Combine(dataDirectory, "ETC_RES.R.json"))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.Equal("9044", etcOffset770.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("4289", etcOffset140.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Case7_Etc0770_Source, globalStrings[etcOffset770.ToString(CultureInfo.InvariantCulture)]);
        Assert.Equal(Case7_Etc0140_Source, globalStrings[etcOffset140.ToString(CultureInfo.InvariantCulture)]);
    }

    // ---------------------------------------------------------------------------------------
    // Corpus census
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Decode_WholeCorpus_MatchesMeasuredCensus()
    {
        var dataDirectory = FindRealDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        var mapStrings = 0;
        var sharedStrings = 0;
        var etcStrings = 0;
        var pageCount = 0;
        var emptyPages = 0;
        var voiceMarkers = 0;
        var centerMarkers = 0;
        var slowMarkers = 0;
        var glyphMarkers = 0;
        var newlines = 0;
        var flagMarkers = 0;
        var yieldMarkers = 0;
        var falconUpdateCommands = 0;
        var callCount = 0;

        void Process(string? source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            var pages = ReferenceTextDecoder.Decode(source);
            foreach (var page in pages)
            {
                pageCount++;

                var visibleMarkers = page.Markers.Where(m => m.Name is not ("flag" or "yield")).ToList();
                if (page.Text.Length == 0 && visibleMarkers.Count == 1 && visibleMarkers[0].Name == "empty")
                {
                    emptyPages++;
                }

                foreach (var marker in page.Markers)
                {
                    switch (marker.Name)
                    {
                        case "voice": voiceMarkers++; break;
                        case "center": centerMarkers++; break;
                        case "slow": slowMarkers++; break;
                        case "glyph": glyphMarkers++; break;
                        case "flag": flagMarkers++; break;
                        case "yield": yieldMarkers++; break;
                    }
                }

                newlines += page.Text.Count(c => c == '\n');

                foreach (var command in page.Commands)
                {
                    if (command.Name == "falcon_update")
                    {
                        falconUpdateCommands++;
                    }
                }

                callCount += page.Calls.Count;
            }

            // Determinism: decoding the same string twice gives identical canonical strings.
            var again = ReferenceTextDecoder.Decode(source);
            Assert.Equal(
                string.Join("|", pages.Select(p => p.ToCanonicalString())),
                string.Join("|", again.Select(p => p.ToCanonicalString())));
        }

        for (var mapId = 0; mapId < 2000; mapId++)
        {
            var mapPath = Path.Combine(dataDirectory, $"map_{mapId}.json");
            if (!File.Exists(mapPath))
            {
                continue;
            }

            foreach (var s in StringTableReader.ReadMapStrings(mapPath))
            {
                if (!string.IsNullOrEmpty(s))
                {
                    mapStrings++;
                    Process(s);
                }
            }
        }

        foreach (var s in StringTableReader.ReadMapStrings(Path.Combine(dataDirectory, "map_alundra.json")))
        {
            if (!string.IsNullOrEmpty(s))
            {
                sharedStrings++;
                Process(s);
            }
        }

        var etcIndex = EtcIndexCatalogReader.Read(RequireEtcIndexTableFile()).ValueByIndex;
        var globalStrings = StringTableReader.ReadGlobalTable(Path.Combine(dataDirectory, "ETC_RES.R.json"))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        foreach (var offset in etcIndex)
        {
            var key = offset.ToString(CultureInfo.InvariantCulture);
            if (globalStrings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                etcStrings++;
                Process(value);
            }
        }

        Assert.Equal(24303, mapStrings);
        Assert.Equal(128, sharedStrings);
        Assert.Equal(353, etcStrings);
        Assert.Equal(31757, pageCount);
        Assert.Equal(95, emptyPages);
        Assert.Equal(24431, voiceMarkers);
        Assert.Equal(823, centerMarkers);
        Assert.Equal(5319, slowMarkers);
        Assert.Equal(11182, glyphMarkers);
        Assert.Equal(24707, newlines);
        Assert.Equal(932, flagMarkers);
        Assert.Equal(922, yieldMarkers);
        Assert.Equal(7, falconUpdateCommands);
        Assert.Equal(20, callCount);
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private static IReadOnlyList<string> SplitPages(string source)
        => source.Split("\\A");

    private static string? FindRealDataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data-extracted", "data");
            if (File.Exists(Path.Combine(candidate, "ETC_RES.R.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    // Only called once data-extracted has been found: from then on a missing index table is a broken
    // test setup, not a reason to skip, or the ETC part of the corpus would silently go unchecked.
    private static string RequireEtcIndexTableFile()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "EtcIndexTable.csv");
        Assert.True(
            File.Exists(candidate),
            $"data-extracted is present but EtcIndexTable.csv is missing at '{candidate}'; the ETC table cannot be checked.");
        return candidate;
    }
}
