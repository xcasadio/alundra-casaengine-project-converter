# E19.f2b1 discovery - S025 converter fix (D-E19-78) (read-only)

Repository `chantier/e19-suite` at `897504b` (+ the concurrent agent's test-only work, ignored); engine submodule
`integration/merge-2026-10-05` at `ebeb81c9` (only the author's `CasaEngine.Launcher/Program.cs` modified, untouched).
Nothing built, run, exported, edited, staged or committed inside the repository. Probes live only under
`f2b1-disc/yarn-s025/` (this folder): they read the repository and copy three of its built DLLs (`real/lib`, `proto/lib`,
`dllpath/lib`, built 2026-10-05 22:43-23:10 by the other agent) into scratch; nothing is written in the repository or in
`alundra-project/`. Tags: **[bin]** read in the binary `ALUN_CD.EXE` (France) or the disc data; **[code]** read in the repository;
**[probe]** observed by running the project's own Yarn compiler/parser/DLL code in scratch; **[calc]** computed by a scratch
script; **[hyp]** hypothesis, not proven.

## Summary

1. **Yarn behaviour, proved with the project's own pipeline [probe]** (YarnSpinner + YarnSpinner.Compiler **3.2.1**, through
   `YarnDialogueCompiler.CompileString` -> `LineTexts` -> `YarnLineTextParser.Parse` -> `AlundraDialogueDirector.Tokenize`):
   the compiler **removes the spaces that end a line** (8 trailing spaces + the separator, with or without a `#line` tag, with or
   without earlier `trimwhitespace=false` markers: the string table keeps none) and the spaces that start a line before any
   marker; it **keeps spaces that precede a self-closing marker** (`[empty/]`, `[yield/]`, `[flag/]`, any name, with or
   without `trimwhitespace=false`) and spaces that follow a leading marker written with `trimwhitespace=false`. So "keep the
   spaces in the emitter" alone changes nothing (case 4 below); the spaces need a zero-length marker right after them.
2. **Census [calc + bin + probe]**: over the 823 `\H` markers of the whole corpus (24 784 nodes, 31 757 pages), measured four
   ways (raw string with the binary rule and the glyph table of the exe; exported elements; exported `.yarn`; **every compiled
   `.dialogue` through the DLL's own `Tokenize`**) exactly **3** differ: `M472_S025`, `M473_S025`, `M474_S025` page 0, second
   centred line, 125 px (binary) against 93 px (export), x 97 against 113. No other centred line is affected; no centred line
   has a pen (width before the `\H`) difference; the font3.fnt advances equal the exe table on every glyph used (0
   disagreement). An **independent scan of the original disc data** (`DATAS.BIN`, `ETC_RES.R`) finds the same three and no other:
   the bytes are `Florin\W5Roulette` + 8 x 0x20 + NUL at file offsets 106732056, 106951148, 107192672.
3. **Fix**: keep the trailing spaces of a page whose last line is centred, and write a no-op marker after them so Yarn
   cannot drop them: exact emitted text for S025 in section 3.1 (`...Roulette` + 8 spaces + `[empty trimwhitespace=false/]`).
   Prototyped on a patched copy of the real emitter (`proto/`): on the whole corpus it changes **exactly 3 assets, 1 line
   each**, leaves 482 byte-identical; the unpatched copy reproduces **all 485** exported files byte for byte. Through the DLL
   path the second line becomes 125 px / x 97 and the page types 43 steps instead of 35 (the 8 spaces are typed, as in the
   binary). Export delta: 3 `.yarn` + 3 `.dialogue` (`line_texts` of one key each; `program_base64` identical), `report.json`
   and `AssetInfos.json` unchanged.
4. Touches: `docs/plan-e15-yarn.md` D-E15-8 (partly superseded), ADR-0006 (decision + consequence bullets; status line), ADR-0025
   (interaction with the transparent markers), a new ADR (next free number at HEAD: **0036**). Tests: 3 new emitter tests, an
   oracle rule + new oracle tests, one corpus test that is red on exactly 3 pages until both sides move; **nothing moves in
   `Alundra.Tests`**.

## 1. What Yarn does with the spaces (proof)

### 1.1 Versions and path [code]
- `CasaEngineMonogame/Directory.Packages.props:25-26`: `YarnSpinner` 3.2.1, `YarnSpinner.Compiler` 3.2.1 (central management).
  `CasaEngine/CasaEngine.csproj:27` references `YarnSpinner`; `CasaEngine.Compiler/CasaEngine.Compiler.csproj:9`
  `YarnSpinner.Compiler`. The converter takes the compiler through `ProjectReference` to `CasaEngine.Compiler`
  (`alundra-casaengine-project-converter.csproj:88`), `Alundra.Tests` likewise (`Alundra.Tests.csproj:34`); `Alundra.csproj` never
  references the compiler (the DLL loads compiled assets and uses the runtime through the engine). NuGet cache:
  `~/.nuget/packages/yarnspinner{,.compiler}/3.2.1` (the assemblies report 3.2.1.0).
- Compile: `YarnDialogueCompiler.CompileString` (`CasaEngine.Compiler/Dialogue/YarnDialogueCompiler.cs:39-70`) ->
  `CompilationJob.CreateFromString` + `Compiler.Compile`; the line text table = `result.StringTable[...].text`
  (`BuildLineTextTable` :72-80), stored as `line_texts` in the `.dialogue` asset.
- Runtime: `YarnDialogueRunner.OnLine` (`CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs:163-167`) ->
  `ResolveLineText` (:222-230: `LineTexts` lookup + `ExpandSubstitutions`) -> `YarnLineTextParser.Parse`
  (`YarnLineTextParser.cs:34-67`: `LineParser` with the select/plural/ordinal processors; text + attributes) ->
  presenter. The DLL: `AlundraDialogueCapturePresenter.ShowLine` -> `LineSink` -> `AlundraDialogueDirector.OnPageShown`
  (`AlundraDialogueDirector.cs:313`, sink set at :204) -> `Tokenize` (:323-342, `AppendMarker` :344-372: `br`, `glyph`, `voice`,
  `center` :357, `slow`, `flag`, `yield`; **any other marker, `empty` included, is ignored**; markers at position == text
  length are appended after the last character, loop to `i <= Text.Length`). The box skips the Center token
  (`AlundraDialogueBox.cs:606-607`).

### 1.2 Probe results [probe] (`probe/` = Yarn only; `real/` = CasaEngine.Compiler + CasaEngine + Alundra DLLs; outputs `probe_out.txt`,
`probe_out2.txt`, `real_out.txt`, `real_out2.txt`)

`.` = U+0020. "text" = `LineTexts[...]` after the compiler; "parsed" = what `YarnLineTextParser.Parse` returns.

| Source line (before `#line`) | compiled text | parsed text / markers |
|---|---|---|
| current export of S025 (`...Roulette` + 1 sep. space) | ends `Roulette` | len 34; voice@0 center@0 br@20 center@20 glyph@26 |
| `...Roulette` + 8 spaces + sep. space, no guard | **identical to the row above (spaces gone)** | len 34 |
| same with 0 or 1 or 2 extra spaces before `#line` | identical (spaces gone) | len 34 |
| `Florin` + 3 spaces, **no `#line` tag at all** | `Florin` | len 6 (trim is not tied to the tag) |
| `...Roulette` + 8 spaces + `[empty trimwhitespace=false/]` | spaces kept (len 227 vs 190) | **len 42**, empty@42 |
| same with `[yield .../]`, `[flag id=0 .../]`, `[pad n=8 .../]`, `[empty/]` (default) | spaces kept | len 42, marker@42 |
| `Florin` + 3 spaces + `[flag id=5 .../]` | kept | len 9, flag@9 |
| `ab` + 3 spaces + `[glyph .../]cd` / `[br .../]cd` | kept (mid line) | len 7, marker@5 |
| 3 leading spaces + plain text | **removed** | `Florin` |
| 3 leading spaces + `[center .../]Florin` | **removed** | `Florin`, center@0 |
| `[center .../]` + 3 spaces + `Florin` (also after `[voice][center]`) | **kept** | `...Florin` len 9, center@0 |
| NBSP x3 at the end | **removed too** (compiler trims all white space) | `Roulette` |
| backslash-space escape `\ ` | compile error ("Unexpected ' '") | - |
| inline expression `{"      "}` | becomes a substitution `{0}` (runtime value) | not usable |
| `[center width=125 .../]` | property kept as an int | `width=int:125` (possible, rejected below) |

`trimwhitespace`: a default `[x/]` swallows one space *after* it (`ab   [center/]   cd` -> 9 chars instead of 10);
`trimwhitespace=false` keeps it (ADR-0006 already states this). Nothing trims the space *before* a marker.

### 1.3 The DLL path on S025 [probe] (`real_out.txt`)
Real compile of the three variants, parsed with the engine parser, tokenized by the DLL's `Tokenize` (via reflection on the
copy of `Alundra.dll`), widths with the exported font3.fnt advances (space 4, glyph 21 = 14):

| variant | tokens | typed steps | width of `\H` #0 / #1 | x #0 / #1 (`16 + (288 - w)/2`) |
|---|---|---|---|---|
| 1. current export | 39 | 35 | 111 / **93** | 104 / **113** |
| 2. 8 spaces + `[empty trimwhitespace=false/]` | 47 | 43 | 111 / **125** | 104 / **97** |
| 3. 8 spaces + `[keep trimwhitespace=false/]` (a new name) | 47 | 43 | 111 / 125 | 104 / 97 (marker ignored like `empty`) |
| 4. 8 spaces, no guard | 39 | 35 | 111 / 93 | 104 / 113 (Yarn dropped them) |

The binary's numbers for this text are 111 and 125 [calc] (`../f2b-disc-verify/center_verify_out.txt`), so variant 2 matches.

## 2. Census

Scripts: `emitter_port.py` (Python port of `YarnTextEmitter.cs`; **validated: all 24 784 nodes re-rendered byte for byte against
the exported `.yarn` files, 0 differences, 0 emit errors**), `census.py`, `census2.py`, `dllpath/` + `dllpath_census.py`,
`disc_scan.py`, `disc_vs_json.py`.

### 2.1 Every `\H`, four measurements [calc]
- Binary rule = port of `CalcTextWidth 0x8004771C` (jump table `0x80024098`, read directly; `\V` is a normal-glyph fallback, `\X`
  skips two, `\W` is a glyph, `\H` is re-read as 'H') on the raw tokens, glyph table of the exe (`0x800993C4`, 20 bytes per entry).
- 823 `\H` markers; widths 27..254; none > 255.
- **raw (binary rule) vs exported elements: 3 differences** (S025 of 472/473/474, marker #1, 125 vs 93).
- **raw vs the compiled assets through the DLL path** (`dllpath/Program.cs`: for each of the 485 `.dialogue`, each `line_texts`
  entry: `ExpandSubstitutions` -> `Parse` -> `Tokenize` -> font3.fnt advances): 823 centre tokens, same key set, **3 differences**,
  the same three.
- font3.fnt advances vs the exe table on every char/glyph of those lines: **0 disagreement**.
- Pen position before the `\H` (width of what precedes it on its line), raw vs post-trim: **0 differences** (no `   \H...` at a
  page start in the corpus).
- Independent of the extractor: `disc_scan.py` over `DATAS.BIN` finds 513 printable NUL-terminated strings holding `\H` (544
  runs when any byte is allowed inside, `disc_scan_any.py`) against 511 nodes in the extraction; the two counts were **not
  reconciled** (the disc spells accents `}Y`, `}X`... which the extractor decodes, and some hits sit in binary noise), which does not
  matter here because the question is answered on raw bytes: in both scans exactly **3** strings have a centred last line ending
  on spaces (8 spaces each, bytes `!\N\HFlorin\W5Roulette` + 8 x 0x20 + NUL); `ETC_RES.R` holds no `\H`.
- Verdict: **S025 of maps 472-474 is the only centred line whose width changes** (confirms D-E19-78 and the earlier two checks).

### 2.2 All pages whose edge spaces D-E15-8 removes [calc] (`census_out.txt`, `census2_out.txt`)
31 757 pages: **164 lose trailing spaces** (map 108, ETC 56), **4 lose a leading space**.
- Trailing, map/shared (108): 78 at the very end of the page (Yarn would drop them too), 30 before a trailing `flag`/`yield` (only the
  emitter's transparency rule F0-R2 / ADR-0025 drops them; Yarn would keep them: all 30 are `_S022` (15) and `_S108` (15) of
  15 maps (M389-392, M412-420, M476, M478), e.g. `M391_S022` noted in `plan-e19-f2a-valeurs.md:120`; `census3_out.txt`)); 105 of the 108 are **not centred**
  (typing time only: one extra step per space; histogram of the removed counts over all 164 pages: 1 space x113, 2 x2, 3 x1, 4 x2,
  6 x1, 7 x3, **8 x8**, 9 x3, 10 x4, 12 x5, 13 x3, 14 x4, 15 x4, 16 x1, 17 x1, 18 x3, 20 x1, 24 x5); **3 are centred: S025 x3**.
- Trailing, ETC (56): fixed-width record padding ("Un Nouveau Depart" + spaces, `StringTableReader.cs:22-23`), not authored
  text; none is centred, none is typed by the box. Keeping them would be wrong for the UI readers.
- **Leading-space cases** (4, none centred): `M311_S029` p1 and `M398_S029` p1 (`\101 Pas mal, hein...`: a space after the flag
  code; **lost by the emitter's F0-R2 rule, not by Yarn** (case "spaces after a leading marker" above: Yarn keeps it); in the
  binary the space goes through the normal character path [bin: `0x80046D24`-`0x80046DF8`, no `0x20` test anywhere in the loop
  head `0x80046100`-`0x8004616C`], so the page's first line starts 4 px further right and takes one more step [bin, deduced from
  the code path; not seen running]); `Etc_0069`, `Etc_0077` (" gagne!", ETC item texts, not typed by the box).
  No centred page has a leading-space issue.

## 3. The minimal faithful fix

### 3.1 Rule and exact output
Rule (converter, `YarnTextEmitter.BuildPageRender`): **if the last line of a page (the elements after its last `\N`, or the
whole page) contains a `\H`, the page's trailing spaces are not trimmed; and when the page's last element is then a text run
that ends on a space, `[empty trimwhitespace=false/]` is appended at the end of the line** (a trailing `flag`/`yield` marker
already protects the spaces, so no guard is needed in that case, probe 1.2). Leading trim, trim of every other page, the
transparency of `flag`/`yield` elsewhere: unchanged. Why `empty`: the DLL, the Alundra.Tests oracle and the converter's
equivalence harness already know it as a no-op (`AlundraDialogueDirector.cs:320-322` doc, `OraclePages.PageBytes` case "empty" `AlundraTextBoxOracle.cs:727`,
`YarnCorpusEquivalenceTests.BuildActualPage` case "empty" :1158, `ReferenceMarker.Empty` `ReferenceTextDecoder.cs:72`); a new name (`[keep/]`, proved to behave the same, variant 3) would
also need a case in `BuildActualPage` (it throws on unknown names), in `OraclePages.PageBytes` (throws
`NotSupportedException`), a new `ReferenceMarker` factory and a documented vocabulary entry - **technical choice, not a product
one; default = reuse `empty`**.

Emitted line for `M472_S025`/`M473_S025`/`M474_S025` page 0 (line 137 of each `.yarn`; `.` = U+0020, written here only to
count them; the file holds real spaces):
```
[voice id=-1 trimwhitespace=false/][center trimwhitespace=false/]Roue de la fortune ![br trimwhitespace=false/][center trimwhitespace=false/]Florin[glyph id=21 trimwhitespace=false/]Roulette........[empty trimwhitespace=false/] #line:M472_S025_p0
```
(8 spaces between `Roulette` and `[empty`; 1 space before `#line`.) Compiled `line_texts` value: the same without the
` #line:...`. Parsed: text `Roue de la fortune !FlorinRoulette` + 8 spaces (len 42), attributes voice@0 center@0 br@20 center@20
glyph@26 empty@42. Compile: 0 diagnostics. Program bytes identical to today's (7 063 bytes); the only differing `LineTexts`
key per asset is `line:M47x_S025_p0` (`proto_out2.txt`).

Edge cases through the same prototype (real emitter code, then the real compile+parse; `proto_out3.txt`):

| source (raw) | emitted body (`.` = space) | parsed text / markers |
|---|---|---|
| `\HFlorin\W5Roulette` + 8 sp. | `[center/]Florin[glyph 21/]Roulette........[empty/]` | `FlorinRoulette` + 8 sp., center@0 glyph@6 empty@22 |
| `\HHi` (no trailing space) | `[center/]Hi` (unchanged, no guard) | `Hi`, center@0 |
| `\HHi` + 3 sp. | `[center/]Hi...[empty/]` | `Hi...`, center@0 empty@5 |
| `Haut   \N\HBas  ` (last line centred) | `Haut...[br/][center/]Bas..[empty/]` | `Haut...Bas..`, br@7 center@7 empty@12 |
| `\HHaut\NBas   ` (last line not centred) | `[center/]Haut[br/]Bas` (unchanged) | `HautBas` |
| `\HAbc  \999` | `[center/]Abc..[flag id=999/]` (kept, no guard) | `Abc..`, center@0 flag@5 |
| `\HAbc  \999  ` | `[center/]Abc..[flag id=999/]..[empty/]` | `Abc....`, center@0 flag@5 empty@7 |
| `\H   ` | `[center/]...[empty/]` | `...`, center@0 empty@3 |
| `\HA  \A\HB  ` | each page gets its own guard | `A..` empty@3 / `B..` empty@3 |
| `  \HFoo  ` | leading trimmed, trailing kept: `[center/]Foo..[empty/]` | `Foo..`, center@0 empty@5 |

### 3.2 Code change (prototype diff in `emitter_fix.diff`; the repository is untouched)
`alundra-casaengine-project-converter/Text/YarnTextEmitter.cs`, three places: (1) `BuildPageRender` :536-537 - compute
`keepTrailingSpaces = LastLineIsCentred(elements)` before `TrimEdgeSpaces(elements, keepTrailingSpaces)`; (2) :558-560 - append
`[empty trimwhitespace=false/]` also when `keepTrailingSpaces` and the last element is a `TextElement` ending on `' '`
(`isEmpty` and the `Yarn.EmptyPages` counter are unaffected: the page is not empty); (3) `TrimEdgeSpaces` :616 - a flag to skip the
trailing loop; a new private `LastLineIsCentred` (walk back to the last `LineBreakElement`, true on a `CenterElement`).
Prototype caveat: the helper was inserted between the XML doc of `TrimEdgeSpaces` and the method; in the real change put it
above that doc and extend the doc with the exception. Doc comments to touch: class summary :60-78, `BuildPageRender` :440-446,
`TrimEdgeSpaces` :608-615.

### 3.3 Export delta (exact) [probe]
Converter run on the whole corpus (`proto/`, groups = the 485 assets): 3 changed, 482 identical. In `alundra-project/`:
1. `Maps/Pub/Pub (Ring)-472/dialogues/Pub (Ring)-472.yarn` (line 137)
2. `Maps/Pub/Pub (Ring)-472/dialogues/Pub (Ring)-472.dialogue` (`line_texts["line:M472_S025_p0"]` only)
3. `Maps/Pub/Pub (Shooting)-473/dialogues/Pub (Shooting)-473.yarn` (line 137)
4. `Maps/Pub/Pub (Shooting)-473/dialogues/Pub (Shooting)-473.dialogue` (`line_texts["line:M473_S025_p0"]` only)
5. `Maps/Pub/Pub (Roulette)-474/dialogues/Pub (Roulette)-474.yarn` (line 137)
6. `Maps/Pub/Pub (Roulette)-474/dialogues/Pub (Roulette)-474.dialogue` (`line_texts["line:M474_S025_p0"]` only)

Unchanged: `report.json` counters (no counter counts a marker by name; `Yarn.EmptyPages` stays 95, `Yarn.Lines` 31 757, ...), `AssetInfos.json`
(ids and names come from the map names), `program_base64` of the three assets, every other asset, the DLL. (A new
`report.json` counter such as kept-space pages would change `report.json`; not proposed.) Proof regime of the memory note
applies when it is done for real: full in-place export, manifest before/after, expected diff = exactly these six files.
Order: no constraint (the current DLL already types the 8 characters and ignores `empty`; an old DLL on the new export is harmless).

### 3.4 Decisions and documents touched
- `docs/plan-e15-yarn.md`: D-E15-8 (:58-59, "Les espaces de debut et de fin de page sont perdus ... aucun lecteur actuel n'en
  depend": add the exception and D-E19-78), the control-code table row :203, the oracle bullet :335 ("espaces de bord retires
  (D-E15-8)").
- `docs/decisions/0006-alundra-text-is-authored-as-yarn.md`: Decision bullet "Leading and trailing spaces of a page are lost ..."
  and Consequences bullet "The edge spaces of about 110 map pages ... are lost" (3 of the 108 map pages come back); status line
  gets "partly superseded by ADR-0036" (ADR-0025 did the same; a decision is never rewritten). Its Context line on Yarn's
  trimming is confirmed and sharpened by section 1.2.
- `docs/decisions/0025-text-flag-codes-and-backslash-y-are-positioned-yarn-markers.md`: bullet "The edge-space trim ... ignore
  these two markers": the new rule wins for a centred last line (spaces before a trailing flag/yield are then kept); no edit
  needed if the new ADR cites it.
- ADR-0007 (`\X` functions), ADR-0008 (`[glyph id=N]` = font3 char N; the width of glyph 21 = 14 is read from it), ADR-0009 (font3
  charset, 17 proven non-ASCII chars; S025 is ASCII + one glyph): **not touched**. ADR-0029 (the box follows the binary to the
  tick): consequence only, the page types 8 more steps as the binary does.
- New ADR **0036** (check the number at write time) + `docs/decisions/README.md` row; `docs/plan-e19-opcodes.md`: D-E19-78
  (:263-265, mark implemented), f2b discovery summary (:4782), f2b1 sketch (:4880), risk line (:4750: the edge-space shortfall no
  longer applies to S025).

### 3.5 Tests [code + probe]
**Converter** (`alundra-casaengine-project-converter.Tests/Text`):
- `YarnTextEmitterTests.cs`, edge-space section (:483-515): the existing five (`Emit_LeadingAndTrailingSpaces_AreRemoved`,
  `Emit_LeadingSpaceBeforeMarker_...`, `Emit_TrailingLineBreak_...`, `Emit_H_BecomesCenterMarker` :247 `\HHi`, ...) **stay green
  unchanged** (none has a centred last line with trailing spaces). New, red first, with the exact strings of the table in 3.1:
  e.g. `EmitSingle("TCENTER8", "\\HFlorin\\W5Roulette        ")` ==
  `"title: TCENTER8\n---\n[center trimwhitespace=false/]Florin[glyph id=21 trimwhitespace=false/]Roulette        [empty trimwhitespace=false/] #line:TCENTER8_p0\n===\n"`
  (+ `EmitCompileAndParse`: text `"FlorinRoulette        "`, attributes center@0 glyph@6 empty@22; `Statistics.EmptyPages == 0`);
  a not-last centred line (`"\\HHaut\\NBas   "` unchanged); a flag after the spaces (no guard); two pages; `\H` + spaces only.
- `ReferenceTextDecoder.cs` `PageBuilder.Build` (:496-580, the independent oracle): add the same rule from the decision text (skip
  the trailing trim when the last line holds a centre marker; add `ReferenceMarker.Empty(text.Length)` last when the page text ends
  on a space) - never from the emitter (ADR-0025's rule). `ReferenceTextDecoderTests.cs` edge-space section (:425-500): add
  `Decode("\\HFlorin\\W5Roulette        ")` -> text `FlorinRoulette        `, markers `[center@0, glyph@6 id=21, empty@22]`; the existing
  `Decode_EdgeSpaces_*` stay green. `Decode_WholeCorpus_MatchesMeasuredCensus` (:820-958): every count unchanged (95 empty pages
  is "text empty and a single `empty` marker", which the S025 pages are not).
- `YarnCorpusEquivalenceTests.WholeCorpus_EveryPageOfEveryNodeMatchesTheReferenceDecoder` (:46-108): **red on exactly 3 pages**
  (`M472_S025_p0`, `M473_S025_p0`, `M474_S025_p0`) if only the emitter moves; green when the oracle moves too; totals unchanged (485
  assets, 24 784 titles, 31 757 pages). Canonical string, **before**: `text=Roue de la fortune !\nFlorinRoulette; markers=[voice@0 id=-1, center@0, center@21, glyph@27 id=21]; commands=[]; calls=[]`;
  **after**: `text=Roue de la fortune !\nFlorinRoulette        ; markers=[voice@0 id=-1, center@0, center@21, glyph@27 id=21, empty@43]; commands=[]; calls=[]`
  (8 spaces; `br` becomes `\n` at 20 so later positions shift by 1). A named case next to :431 is the natural place.
- `YarnDialogueWriterTests.cs:687` etc. (485 / 24 784 / 31 757 / 95 / 11 182 / 932 / 922 / 7 / 20 / 38 192): unchanged.
**Alundra.Tests**: **no existing test moves** - `SweptMaps` of `AlundraDialogueBoxOracleComparisonTests` (:308) has no 472-474;
`AlundraTextBoxOracleTests` reads M389/M391/M164 only; `AlundraYarnVariableCorpusTests` (all 485 `.dialogue`) checks variables
only; `OraclePages.PageBytes` already reads `empty` as nothing and the spaces as bytes 0x20. Additions belong to f2b1's T1: a
named real-text case S025 of 472 (first line `\H` width 111 -> x 104; second 125 -> x 97; 43 typed steps; today 93 -> 113 and 35).

## 4. Rejected candidates (with the reason) [probe]
- `[center width=N/]` computed by the converter: technically possible (property kept as int) but it bakes a font-dependent number
  in the data (stale under translation or font edits), needs a DLL rule "property over computed width", does not restore the 8 typed
  steps, and the binary computes the width from the characters. Rejected.
- NBSP or any silent glyph: the compiler trims NBSP too; font3 has no usable glyph. Backslash-space: compile error. Inline
  expression: becomes a runtime substitution. All rejected by probe.
- Keeping **all** edge spaces (164 + 4 pages): ETC trailing spaces are record padding (56 pages, UI readers would break), and the
  other map pages differ only in typing time; not part of D-E19-78.

## 5. Hypotheses, limits, gaps
- **[hyp]** `[empty/]` reuse is judged cleaner than a new name for lack of consumers; a future DLL feature that treats `empty` as
  "skip the page" would collide (none exists: only `Tokenize` and `ToFont3Text` read attributes in the DLL, grep over `Alundra/`).
- **[calc]** The Python port of the emitter and the C# copy are validated by byte equality against the current export, not
  against a fresh converter run (the export in `alundra-project/` is dated 2026-10-05 17:46 and equal to HEAD's emitter output).
- Not done (read-only): the real converter/test run; `Alundra.Tests` expected values for f2b1 T1 are computed, not run.
- Same family, **not** covered by D-E19-78 (default: stay named gaps, no action): the 2 leading-space pages `M311_S029` p1 and
  `M398_S029` p1 (first line 4 px right and one step more in the original; visible; fixing needs the head walk to stop at a
  leading `flag`/`yield`, 2 more nodes in 2 more files, plus the F0-R2 text/tests), and the 30 map pages whose spaces before a
  trailing flag/yield are dropped (one step each; touching them moves f2a's pinned arc values, e.g. `M391_S022`).
- Housekeeping seen: stray `out.txt`, `out2.txt`, `out3.txt`, `out4.txt` (Sep 28 22:16) inside
  `~/.nuget/packages/yarnspinner/3.2.1/lib/netstandard2.1/` and `yarnspinner.compiler/3.2.1/lib/netstandard2.1/` (an earlier probe
  wrote them into the global NuGet cache); harmless to builds, not removed.

## Files in this folder
`emitter_port.py` (+ validate), `census.py` (+ `census_out.txt`), `census2.py` (+ `_out.txt`), `census_pages.json`, `dis_interp.py`
(binary dump helper), `disc_scan.py`/`disc_vs_json.py` (+ `_out.txt`), `dllpath/` (C# census of every compiled asset through the DLL's
`Tokenize`; `dll_widths.tsv`), `dllpath_census.py` (+ `_out.txt`), `census3.py`, `disc_scan_any.py`, `disc_vs_json2.py` (+ `_out.txt`), `probe/` + `probe_out*.txt` (Yarn-only probe), `real/` +
`real_out*.txt` (real project pipeline), `proto/` (copies `EmitterOrig.cs`, `EmitterProto.cs`, corpus run; `out/` = patched sources of
the 3 assets) + `proto_out*.txt`, `emitter_fix.diff`.
