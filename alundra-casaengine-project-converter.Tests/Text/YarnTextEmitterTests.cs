using System.Linq;
using AlundraCasaEngineProjectConverter.Text;
using CasaEngine.Compiler.Dialogue;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Dialogue.Yarn;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests.Text;

/// <summary>
/// Covers <see cref="YarnTextEmitter"/> against docs/plan-e15-yarn.md §1 (the correspondence table)
/// and E15.b's task T3, one test per row/rule, plus the two named corpus examples the plan quotes for
/// map 134 (<c>M134_S016_p0</c>, <c>M134_S019_p0</c>). Written from the plan and
/// <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs</c> alone: it does not use
/// <c>ReferenceTextDecoder.cs</c> (the independent "original side" of the T6 equivalence oracle), which
/// a separate test compares this emitter's output against.
///
/// Every emitted example is also compiled with the real <see cref="YarnDialogueCompiler"/> and
/// <see cref="AlundraYarnFunctions.CreateDeclarations"/>, asserting zero diagnostics (not only zero
/// errors); a handful additionally run through <see cref="DialogueAsset"/> and
/// <see cref="YarnLineTextParser"/> to prove what a real dialogue would show on screen.
/// </summary>
public class YarnTextEmitterTests
{
    // ---- Compiling helpers ----------------------------------------------------------------

    private static YarnDialogueCompilationResult Compile(string source)
    {
        var compiler = new YarnDialogueCompiler();
        return compiler.CompileString(source, "test.yarn", AlundraYarnFunctions.CreateDeclarations());
    }

    private static void AssertCompilesWithNoDiagnostic(YarnDialogueCompilationResult result)
    {
        Assert.False(result.ContainsErrors, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>
    /// Emits a single-entry, single-page source, asserts it produced no error, compiles it with zero
    /// diagnostics, then looks its line up in the compiled <see cref="DialogueAsset"/>, expands
    /// substitutions (there are none in any line these tests feed it) and parses it with
    /// <see cref="YarnLineTextParser"/> - the same pipeline <c>YarnDialogueRunner</c> applies to a
    /// running dialogue's line.
    /// </summary>
    private static (DialogueLine Line, YarnTextEmitResult EmitResult) EmitCompileAndParse(string rawSource, string title = "T")
    {
        var emitResult = YarnTextEmitter.Emit(new[] { new YarnTextEntry(title, rawSource) });
        Assert.Empty(emitResult.Errors);

        var compileResult = Compile(emitResult.Source);
        AssertCompilesWithNoDiagnostic(compileResult);

        var asset = DialogueAsset.FromCompiledProgram(title, title, compileResult.ProgramBytes, compileResult.LineTexts);
        var lineId = $"line:{title}_p0";
        Assert.True(asset.TryGetLineText(lineId, out var rawText));

        var expanded = YarnLineTextParser.ExpandSubstitutions(rawText, Array.Empty<string>());
        return (YarnLineTextParser.Parse(expanded), emitResult);
    }

    private static string EmitSingle(string title, string rawSource)
        => YarnTextEmitter.Emit(new[] { new YarnTextEntry(title, rawSource) }).Source;

    // ---- Node shape and page splitting (\A) ------------------------------------------------

    [Fact]
    public void Emit_PlainTextNoCodes_OneNodeOnePage()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("T1", "Bonjour") });

        Assert.Empty(result.Errors);
        Assert.Equal("title: T1\n---\nBonjour #line:T1_p0\n===\n", result.Source);
        Assert.DoesNotContain('\r', result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_A_SplitsIntoTwoPages()
    {
        var source = EmitSingle("T2", "Un\\ADeux");

        Assert.Equal("title: T2\n---\nUn #line:T2_p0\nDeux #line:T2_p1\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_StringEndingWithA_HasEmptyLastPage()
    {
        var source = EmitSingle("T3", "Un\\A");

        Assert.Equal(
            "title: T3\n---\nUn #line:T3_p0\n[empty trimwhitespace=false/] #line:T3_p1\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_StringStartingWithA_HasEmptyFirstPage()
    {
        var source = EmitSingle("T4", "\\AUn");

        Assert.Equal(
            "title: T4\n---\n[empty trimwhitespace=false/] #line:T4_p0\nUn #line:T4_p1\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_TwoEntries_AreSeparatedByOneBlankLine()
    {
        var result = YarnTextEmitter.Emit(new[]
        {
            new YarnTextEntry("T1", "Bonjour"),
            new YarnTextEntry("T2", "Salut"),
        });

        Assert.Empty(result.Errors);
        Assert.Equal(
            "title: T1\n---\nBonjour #line:T1_p0\n===\n\ntitle: T2\n---\nSalut #line:T2_p0\n===\n",
            result.Source);
        Assert.Equal(2, result.Statistics.Nodes);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_SameEntries_IsDeterministic()
    {
        var entries = new[]
        {
            new YarnTextEntry("T1", "Un\\ADeux"),
            new YarnTextEntry("Etc_0067", "\\CRamène-moi \\X3 des statuettes\\Net voilà :\\N\\X4.\\0100\\Y"),
        };

        var first = YarnTextEmitter.Emit(entries).Source;
        var second = YarnTextEmitter.Emit(entries).Source;

        Assert.Equal(first, second);
        AssertCompilesWithNoDiagnostic(Compile(first));
    }

    // ---- \N -----------------------------------------------------------------------------------

    [Fact]
    public void Emit_N_BecomesBrMarker()
    {
        var source = EmitSingle("T5", "Un\\NDeux");

        Assert.Equal(
            "title: T5\n---\nUn[br trimwhitespace=false/]Deux #line:T5_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_TrailingN_KeepsTrailingBrMarker()
    {
        // Mirrors the named example M134_S012_p1: "Je suis un homme heureux. Merci,\NAlundra !\N".
        var (line, result) = EmitCompileAndParse("Je suis un homme heureux. Merci,\\NAlundra !\\N", "M134_S012");

        Assert.Contains(
            "Je suis un homme heureux. Merci,[br trimwhitespace=false/]Alundra ![br trimwhitespace=false/]",
            result.Source);
        Assert.Equal("Je suis un homme heureux. Merci,Alundra !", line.Text);
        Assert.Equal(2, line.Attributes.Count(a => a.Name == "br"));
    }

    // ---- Numeric flags (\<digits>) ----------------------------------------------------------

    [Fact]
    public void Emit_NumericCode_BecomesFlagCommandBeforeLine()
    {
        var source = EmitSingle("T6", "\\100Texte");

        Assert.Equal("title: T6\n---\n<<flag 100>>\nTexte #line:T6_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_NumericCode_NormalisesLeadingZeros()
    {
        var withLeadingZero = EmitSingle("T7", "\\0100Texte");
        var without = EmitSingle("T7", "\\100Texte");

        Assert.Equal(without, withLeadingZero);
        AssertCompilesWithNoDiagnostic(Compile(without));
    }

    [Fact]
    public void Emit_999And0999_AreTheSameFlag()
    {
        var plain = EmitSingle("T8", "\\999Texte");
        var padded = EmitSingle("T8", "\\0999Texte");

        Assert.Equal(plain, padded);
        Assert.Contains("<<flag 999>>", plain);
        AssertCompilesWithNoDiagnostic(Compile(plain));
    }

    // ---- \Y ("ends the render step, the page continues", drops nothing else) ------------------

    [Fact]
    public void Emit_Y_ProducesNoTextButKeepsFlagBefore()
    {
        // Named example: "\401\Ydétruite." -> one line, <<flag 401>> before it.
        var source = EmitSingle("T9", "\\401\\Ydétruite.");

        Assert.Equal("title: T9\n---\n<<flag 401>>\ndétruite. #line:T9_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- \B .. \G (voice) ---------------------------------------------------------------------

    [Theory]
    [InlineData('B', -1)]
    [InlineData('C', 0)]
    [InlineData('D', 1)]
    [InlineData('E', 2)]
    [InlineData('F', 3)]
    [InlineData('G', 4)]
    public void Emit_VoiceCodes_MapToVoiceMarkerWithExpectedId(char code, int expectedId)
    {
        var source = EmitSingle("TV", $"\\{code}Hi");

        Assert.Equal(
            $"title: TV\n---\n[voice id={expectedId} trimwhitespace=false/]Hi #line:TV_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_VoiceMinusOne_PropertyIsAnIntegerAtRuntime()
    {
        var (line, _) = EmitCompileAndParse("\\BHi");

        var voice = Assert.Single(line.Attributes, a => a.Name == "voice");
        var id = voice.Properties["id"];
        Assert.IsType<int>(id);
        Assert.Equal(-1, (int)id);
    }

    // ---- \H (center), \T (slow) -----------------------------------------------------------------

    [Fact]
    public void Emit_H_BecomesCenterMarker()
    {
        var source = EmitSingle("TH", "\\HHi");

        Assert.Equal("title: TH\n---\n[center trimwhitespace=false/]Hi #line:TH_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_T_BecomesSlowMarker()
    {
        var source = EmitSingle("TT", "\\THi");

        Assert.Equal("title: TT\n---\n[slow trimwhitespace=false/]Hi #line:TT_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- \W<c> (glyph, executable's formula) -----------------------------------------------------

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
    public void Emit_W_MapsOperandToGlyphIdByExecutableFormula(char operand, int expectedGlyphId)
    {
        var source = EmitSingle("TW", $"\\W{operand}");

        Assert.Equal(
            $"title: TW\n---\n[glyph id={expectedGlyphId} trimwhitespace=false/] #line:TW_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_WOnlyPage_IsNotAnEmptyPage()
    {
        // Corpus fact (5.1): 72 pages hold only \W/\T once codes are removed; they draw a glyph and
        // are not empty pages.
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TWT", "\\W2\\T") });

        Assert.Empty(result.Errors);
        Assert.Equal(0, result.Statistics.EmptyPages);
        Assert.Equal(
            "title: TWT\n---\n[glyph id=18 trimwhitespace=false/][slow trimwhitespace=false/] #line:TWT_p0\n===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    // ---- Raw ETC control bytes 0x1A / 0x1C ------------------------------------------------------

    [Fact]
    public void Emit_RawByte0x1A_BecomesGlyph26()
    {
        var source = EmitSingle("TETC1", "a\u001Ab");

        Assert.Equal("title: TETC1\n---\na[glyph id=26 trimwhitespace=false/]b #line:TETC1_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_RawByte0x1C_BecomesGlyph28()
    {
        var source = EmitSingle("TETC2", "a\u001Cb");

        Assert.Equal("title: TETC2\n---\na[glyph id=28 trimwhitespace=false/]b #line:TETC2_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- \V<n> (game_var) -------------------------------------------------------------------------

    [Fact]
    public void Emit_V_BecomesGameVarFunctionCall()
    {
        var source = EmitSingle("TVv", "a\\V3b");

        Assert.Equal("title: TVv\n---\na{game_var(3)}b #line:TVv_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- \X (falcon_update command + position-dependent function) ---------------------------------

    [Fact]
    public void Emit_X0_OpeningPage_IsFalconTempWithFalconUpdate()
    {
        var source = EmitSingle("TX0", "\\X0");

        Assert.Equal(
            "title: TX0\n---\n<<falcon_update>>\n{falcon_temp()} #line:TX0_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X1_IsFalconWithFalconUpdate()
    {
        var source = EmitSingle("TX1", "\\X1");

        Assert.Equal("title: TX1\n---\n<<falcon_update>>\n{falcon()} #line:TX1_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X2OpeningPage_IsCategoryItemNameBefore()
    {
        var source = EmitSingle("TX2", "\\X2");

        Assert.Equal(
            "title: TX2\n---\n<<falcon_update>>\n{category_item_name_before()} #line:TX2_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X4OpeningPage_IsCategoryItemNameBefore()
    {
        var source = EmitSingle("TX4", "\\X4");

        Assert.Equal(
            "title: TX4\n---\n<<falcon_update>>\n{category_item_name_before()} #line:TX4_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X2AfterAnotherX_IsCategoryItemNameCurrent()
    {
        var source = EmitSingle("TX24", "\\X1\\X2");

        Assert.Equal(
            "title: TX24\n---\n<<falcon_update>>\n{falcon()}{category_item_name()} #line:TX24_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X3_IsCategoryThreshold()
    {
        var source = EmitSingle("TX3", "\\X3");

        Assert.Equal(
            "title: TX3\n---\n<<falcon_update>>\n{category_threshold()} #line:TX3_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_X5_IsCategoryRemaining()
    {
        var source = EmitSingle("TX5", "\\X5");

        Assert.Equal(
            "title: TX5\n---\n<<falcon_update>>\n{category_remaining()} #line:TX5_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_SeveralX_ProducesExactlyOneFalconUpdate()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TXN", "\\X0\\X1\\X3\\X5") });

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Statistics.FalconUpdateCommands);
        Assert.Equal(
            "title: TXN\n---\n<<falcon_update>>\n{falcon_temp()}{falcon()}{category_threshold()}{category_remaining()} #line:TXN_p0\n===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_X0AfterAnotherX_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TXBAD", "\\X1\\X0") });

        Assert.DoesNotContain("TXBAD", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TXBAD" && e.PageIndex == 0);
        Assert.Contains("\\X0", error.Message);
    }

    [Fact]
    public void Emit_CommandOrder_XBeforeFlag_PutsFalconUpdateFirst()
    {
        var source = EmitSingle("TORD1", "\\X1a\\0100b");

        Assert.Equal(
            "title: TORD1\n---\n<<falcon_update>>\n<<flag 100>>\n{falcon()}ab #line:TORD1_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_CommandOrder_FlagBeforeX_PutsFlagFirst()
    {
        var source = EmitSingle("TORD2", "\\0100a\\X1b");

        Assert.Equal(
            "title: TORD2\n---\n<<flag 100>>\n<<falcon_update>>\na{falcon()}b #line:TORD2_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- Empty pages ------------------------------------------------------------------------------

    [Fact]
    public void Emit_PageWithOnlyFlagAndY_IsEmpty()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TEMPTY", "\\999\\Y") });

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Statistics.EmptyPages);
        Assert.Equal(
            "title: TEMPTY\n---\n<<flag 999>>\n[empty trimwhitespace=false/] #line:TEMPTY_p0\n===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_ABoundedFlagAndYPage_IsEmpty()
    {
        // Named example: a page "\A\999\Y\A" (<<flag 999>>, then [empty/]).
        var source = EmitSingle("TEMPTY2", "\\A\\999\\Y\\A");

        Assert.Equal(
            "title: TEMPTY2\n---\n[empty trimwhitespace=false/] #line:TEMPTY2_p0\n<<flag 999>>\n[empty trimwhitespace=false/] #line:TEMPTY2_p1\n[empty trimwhitespace=false/] #line:TEMPTY2_p2\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- Edge spaces (D-E15-8) ----------------------------------------------------------------------

    [Fact]
    public void Emit_LeadingAndTrailingSpaces_AreRemoved()
    {
        var source = EmitSingle("TSPACE", " Bonjour ");

        Assert.Equal("title: TSPACE\n---\nBonjour #line:TSPACE_p0\n===\n", source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_LeadingSpaceBeforeMarker_IsRemovedButMarkerStays()
    {
        var source = EmitSingle("TSPACEM", " \\W2Texte");

        Assert.Equal(
            "title: TSPACEM\n---\n[glyph id=18 trimwhitespace=false/]Texte #line:TSPACEM_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    [Fact]
    public void Emit_TrailingLineBreak_IsNeverRemovedByEdgeTrim()
    {
        var source = EmitSingle("TSPACEBR", "Bonjour \\N ");

        // The trailing space that follows \N is its own trailing text run and is removed; \N itself is
        // never removed by the edge trim, and the space right before it is not at either true edge of
        // the page's element sequence (it sits inside the leading text run, not at its very start), so
        // it survives - matching what Yarn's own compile-time trimming would keep too.
        Assert.Equal(
            "title: TSPACEBR\n---\nBonjour [br trimwhitespace=false/] #line:TSPACEBR_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- Engine round-trip: empty page, trailing br, leading marker ----------------------------

    [Fact]
    public void EmitCompileAndParse_EmptyPage_HasEmptyTextAndOneEmptyAttributeAtPositionZero()
    {
        // "\A" with nothing before it: page 0 is empty.
        var (line, _) = EmitCompileAndParse("\\A", "TEMPTYRUN");

        Assert.Equal(string.Empty, line.Text);
        var attribute = Assert.Single(line.Attributes);
        Assert.Equal("empty", attribute.Name);
        Assert.Equal(0, attribute.Position);
    }

    [Fact]
    public void EmitCompileAndParse_TrailingBr_AttributePositionIsAtTheTextEnd()
    {
        var (line, _) = EmitCompileAndParse("Je suis un homme heureux. Merci,\\NAlundra !\\N", "M134_S012RT");

        Assert.Equal("Je suis un homme heureux. Merci,Alundra !", line.Text);
        var brAttributes = line.Attributes.Where(a => a.Name == "br").OrderBy(a => a.Position).ToList();
        Assert.Equal(2, brAttributes.Count);
        Assert.Equal("Je suis un homme heureux. Merci,".Length, brAttributes[0].Position);
        Assert.Equal(line.Text.Length, brAttributes[1].Position);
    }

    [Fact]
    public void EmitCompileAndParse_LeadingGlyphMarker_IsAtPositionZeroAndTextStaysUnmarked()
    {
        var (line, _) = EmitCompileAndParse("\\W2Texte", "TLEADMARK");

        Assert.Equal("Texte", line.Text);
        var attribute = Assert.Single(line.Attributes, a => a.Name == "glyph");
        Assert.Equal(0, attribute.Position);
    }

    // ---- Escapes: proved by compiling and parsing back to the literal character -------------------

    [Fact]
    public void Emit_Colon_EscapedSoNoCharacterNameIsParsed()
    {
        var (line, result) = EmitCompileAndParse("Nom : Bonjour", "TCOLON");

        Assert.Contains("Nom \\: Bonjour", result.Source);
        Assert.Equal("Nom : Bonjour", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_Hash_EscapedSoDisuseIsNotStrippedAsAHashtag()
    {
        var (line, result) = EmitCompileAndParse("#Disuse", "TDISUSE");

        Assert.Contains("\\#Disuse", result.Source);
        Assert.Equal("#Disuse", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_Brackets_EscapedSoTheyAreNotReadAsMarkup()
    {
        var (line, result) = EmitCompileAndParse("[b]", "TBRACKET");

        Assert.Contains("\\[b\\]", result.Source);
        Assert.Equal("[b]", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_Braces_EscapedSoTheyAreNotReadAsASubstitution()
    {
        var (line, result) = EmitCompileAndParse("{b}", "TBRACE");

        Assert.Contains("\\{b\\}", result.Source);
        Assert.Equal("{b}", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_SingleSlash_EscapedRoundTripsToLiteralSlash()
    {
        var (line, result) = EmitCompileAndParse("a/b", "TSLASH1");

        Assert.Contains("a\\/b", result.Source);
        Assert.Equal("a/b", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_DoubleSlash_EscapedSoItIsNotReadAsAComment()
    {
        var (line, result) = EmitCompileAndParse("a//b", "TSLASH2");

        Assert.Contains("a\\/\\/b", result.Source);
        Assert.Equal("a//b", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    [Fact]
    public void Emit_AngleBrackets_EscapedSoTheyAreNotReadAsACommand()
    {
        var (line, result) = EmitCompileAndParse("a<<b>>c", "TANGLE");

        Assert.Contains("a\\<\\<b\\>\\>c", result.Source);
        Assert.Equal("a<<b>>c", line.Text);
        Assert.Equal(string.Empty, line.Speaker);
        Assert.Empty(line.Attributes);
    }

    // ---- Unsupported codes: node errors, never an exception -----------------------------------------

    [Fact]
    public void Emit_M_IsUnsupportedAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TM", "a\\Mb") });

        Assert.DoesNotContain("TM", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TM" && e.PageIndex == 0);
        Assert.Contains("\\M", error.Message);
    }

    [Fact]
    public void Emit_UnknownLetterAfterBackslash_IsUnsupportedAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TZ", "a\\Zb") });

        Assert.DoesNotContain("TZ", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TZ" && e.PageIndex == 0);
        Assert.Contains("\\Z", error.Message);
    }

    [Fact]
    public void Emit_LoneTrailingBackslash_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TTRAIL", "abc\\") });

        Assert.DoesNotContain("TTRAIL", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TTRAIL" && e.PageIndex == 0);
        Assert.Contains("\\", error.Message);
    }

    [Fact]
    public void Emit_RawControlCharacterOtherThanEtcBytes_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TCTRL", "a\u0001b") });

        Assert.DoesNotContain("TCTRL", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TCTRL" && e.PageIndex == 0);
        Assert.Contains("U+0001", error.Message);
    }

    [Fact]
    public void Emit_WWithMissingOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TWMISS", "a\\W") });

        Assert.DoesNotContain("TWMISS", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TWMISS" && e.PageIndex == 0);
        Assert.Contains("\\W", error.Message);
    }

    [Fact]
    public void Emit_WWithUnmappedOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TWBAD", "a\\Wzb") });

        Assert.DoesNotContain("TWBAD", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TWBAD" && e.PageIndex == 0);
        Assert.Contains("\\Wz", error.Message);
    }

    [Fact]
    public void Emit_VWithMissingOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TVMISS", "a\\V") });

        Assert.DoesNotContain("TVMISS", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TVMISS" && e.PageIndex == 0);
        Assert.Contains("\\V", error.Message);
    }

    [Fact]
    public void Emit_VWithNonDigitOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TVBAD", "a\\VAb") });

        Assert.DoesNotContain("TVBAD", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TVBAD" && e.PageIndex == 0);
        Assert.Contains("\\V", error.Message);
    }

    [Fact]
    public void Emit_XWithMissingOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TXMISS", "a\\X") });

        Assert.DoesNotContain("TXMISS", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TXMISS" && e.PageIndex == 0);
        Assert.Contains("\\X", error.Message);
    }

    [Fact]
    public void Emit_XWithOutOfRangeOperand_IsAnErrorAndDropsTheNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TXOOR", "a\\X9b") });

        Assert.DoesNotContain("TXOOR", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TXOOR" && e.PageIndex == 0);
        Assert.Contains("\\X", error.Message);
    }

    [Fact]
    public void Emit_NodeWithError_IsDroppedButOtherNodesStillEmit()
    {
        var result = YarnTextEmitter.Emit(new[]
        {
            new YarnTextEntry("TGOOD", "Bonjour"),
            new YarnTextEntry("TBAD", "a\\Mb"),
        });

        Assert.Contains("title: TGOOD", result.Source);
        Assert.DoesNotContain("TBAD", result.Source);
        Assert.Single(result.Errors);
        Assert.Equal("TBAD", result.Errors[0].Title);
        Assert.Equal(1, result.Statistics.Nodes);
    }

    // ---- The seven functions, all in one line -------------------------------------------------------

    [Fact]
    public void AlundraYarnFunctions_AllSevenFunctionsInOneLine_Compiles()
    {
        const string source = "title: T\n---\n"
            + "{falcon_temp()} {falcon()} {category_item_name_before()} {category_item_name()} "
            + "{category_threshold()} {category_remaining()} {game_var(3)} #line:x\n===\n";

        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- Named corpus examples (docs/plan-e15-yarn.md, E15.b "Corpus") ------------------------------

    [Fact]
    public void Emit_M134S016p0_MatchesTheNamedCorpusExample()
    {
        // "\CJe pense que tu trouveras ce(t) \X2\Ntout à fait utile. Fais-en bon usage,\NAlundra !"
        var result = YarnTextEmitter.Emit(new[]
        {
            new YarnTextEntry(
                "M134_S016",
                "\\CJe pense que tu trouveras ce(t) \\X2\\Ntout à fait utile. Fais-en bon usage,\\NAlundra !"),
        });

        Assert.Empty(result.Errors);
        Assert.Equal(
            "title: M134_S016\n---\n"
            + "<<falcon_update>>\n"
            + "[voice id=0 trimwhitespace=false/]Je pense que tu trouveras ce(t) {category_item_name_before()}"
            + "[br trimwhitespace=false/]tout à fait utile. Fais-en bon usage,"
            + "[br trimwhitespace=false/]Alundra ! #line:M134_S016_p0\n"
            + "===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_M134S019p0_MatchesTheNamedCorpusExample()
    {
        // "\CRamène-moi \X3 Statuettes de faucons\Net je te récompenserai avec cela :\N\X4.\0100\Y"
        var result = YarnTextEmitter.Emit(new[]
        {
            new YarnTextEntry(
                "M134_S019",
                "\\CRamène-moi \\X3 Statuettes de faucons\\Net je te récompenserai avec cela :\\N\\X4.\\0100\\Y"),
        });

        Assert.Empty(result.Errors);
        Assert.Equal(
            "title: M134_S019\n---\n"
            + "<<falcon_update>>\n"
            + "<<flag 100>>\n"
            + "[voice id=0 trimwhitespace=false/]Ramène-moi {category_threshold()} Statuettes de faucons"
            + "[br trimwhitespace=false/]et je te récompenserai avec cela \\:"
            + "[br trimwhitespace=false/]{category_item_name()}. #line:M134_S019_p0\n"
            + "===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    // ---- Statistics (report.json inventory) ----------------------------------------------------------

    [Fact]
    public void Emit_Statistics_CountCodesEvenForADroppedNode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TSTAT", "a\\Mb\\N") });

        Assert.Equal(1, result.Statistics.CodeCounts["\\N"]);
        Assert.Equal(0, result.Statistics.Nodes);
    }

    [Fact]
    public void Emit_Statistics_CountsGlyphsFlagsFalconUpdatesAndFunctionCalls()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TSTAT2", "\\W2\\100\\X1\\V3") });

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Statistics.GlyphMarkers);
        Assert.Equal(1, result.Statistics.FlagCommands);
        Assert.Equal(1, result.Statistics.FalconUpdateCommands);
        Assert.Equal(2, result.Statistics.FunctionCalls); // falcon() and game_var(3)
        Assert.Equal(1, result.Statistics.Lines);
        Assert.Equal(1, result.Statistics.CodeCounts["\\W2"]);
        Assert.Equal(1, result.Statistics.CodeCounts["\\digits"]);
        Assert.Equal(1, result.Statistics.CodeCounts["\\X1"]);
        Assert.Equal(1, result.Statistics.CodeCounts["\\V3"]);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    // ---- Repeated numeric codes (P2 fix: no per-value dedup) -----------------------------------

    [Fact]
    public void Emit_RepeatedNumericCode_ProducesOneFlagCommandPerOccurrence()
    {
        // Real corpus case: map 389 string 108, page 0 holds \1001 twice.
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TREPEAT", "\\1001\\Ya\\1001\\Y") });

        Assert.Empty(result.Errors);
        Assert.Equal(
            "title: TREPEAT\n---\n<<flag 1001>>\n<<flag 1001>>\na #line:TREPEAT_p0\n===\n",
            result.Source);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_RepeatedNumericCode_StatisticsCountsEveryOccurrence()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TREPEATSTAT", "\\1005a\\1005b\\1005c\\1005") });

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Statistics.FlagCommands);
        Assert.Equal(4, result.Statistics.CodeCounts["\\digits"]);
        AssertCompilesWithNoDiagnostic(Compile(result.Source));
    }

    [Fact]
    public void Emit_RepeatedNumericCode_OrderInterleavesWithFalconUpdateAtFirstXRank()
    {
        var source = EmitSingle("TREPEATORD", "\\1001\\X1\\1001");

        Assert.Equal(
            "title: TREPEATORD\n---\n<<flag 1001>>\n<<falcon_update>>\n<<flag 1001>>\n{falcon()} #line:TREPEATORD_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }

    // ---- Numeric code overflow (P3 fix: never throws) ------------------------------------------

    [Fact]
    public void Emit_NumericCodeOverflowingUInt_IsANodeErrorNamingTheCode()
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TOVERFLOW", "a\\99999999999b") });

        Assert.DoesNotContain("TOVERFLOW", result.Source);
        Assert.Contains(
            result.Errors,
            e => e.Title == "TOVERFLOW" && e.PageIndex == 0 && e.Message.Contains("99999999999"));
    }

    // ---- Leading Yarn-syntax markers (P4 fix: node error, never emitted as-is) ------------------

    [Theory]
    [InlineData("===")]
    [InlineData("---")]
    [InlineData("->")]
    [InlineData("=>")]
    public void Emit_LineStartingWithYarnSyntaxMarker_IsANodeErrorNamingThePrefix(string prefix)
    {
        var result = YarnTextEmitter.Emit(new[] { new YarnTextEntry("TMARKER", $"{prefix} x") });

        Assert.DoesNotContain("TMARKER", result.Source);
        var error = Assert.Single(result.Errors, e => e.Title == "TMARKER" && e.PageIndex == 0);
        Assert.Contains(prefix, error.Message);
    }

    [Fact]
    public void Emit_LineStartingWithMarkerAfterAnEscapedLeadingBracket_IsFine()
    {
        // "\C=== x" renders as "[voice id=0 .../]=== x": the rendered line starts with '[', not '=', so
        // this is not the case the P4 fix guards against.
        var source = EmitSingle("TMARKEROK", "\\C=== x");

        Assert.Equal(
            "title: TMARKEROK\n---\n[voice id=0 trimwhitespace=false/]=== x #line:TMARKEROK_p0\n===\n",
            source);
        AssertCompilesWithNoDiagnostic(Compile(source));
    }
}
