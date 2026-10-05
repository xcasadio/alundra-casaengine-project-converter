# Counter-check of the "yarn-s025" discovery (adversarial, read-only)

Repository `chantier/e19-suite`, HEAD **`262f92a`** (not `897504b`: three more commits landed while the discovery was running,
`c53ceb5`, `d435992`, `262f92a` = E19.f2b0 F2B0-0..2, see correction 1). Nothing was built, run, exported, edited, staged or
committed inside the repository. Everything below was re-derived from the repository files, the binary `ALUN_CD.EXE` (France),
`DATAS.BIN`/`ETC_RES.R` (France) and my own scratch programs in this folder; the discovery's outputs were not used as evidence.
Tags: **[code]** read in the repository, **[bin]** read in the binary or the disc data (my own disassembly, capstone),
**[probe]** observed by running YarnSpinner(.Compiler) 3.2.1 or a copy of the real emitter in scratch, **[calc]** computed.

## Verdict table

| # | Claim | Verdict | Evidence |
|---|---|---|---|
| 1 | The project pins YarnSpinner and YarnSpinner.Compiler 3.2.1 (`CasaEngineMonogame/Directory.Packages.props:25-26`) | CONFIRMED | [code] lines 25-26 read as cited |
| 2 | Path = `CompileString` -> `LineTexts` -> `YarnLineTextParser.Parse` (`YarnDialogueRunner.cs:163-167, 222-230`) -> `Tokenize` (`AlundraDialogueDirector.cs:323`) | CONFIRMED | [code] all lines read; `Tokenize` :323, `AppendMarker` :344, `center` :357, doc :320-322 exact |
| 3 | Yarn 3.2.1 removes the spaces that end a line, with or without a `#line` tag; 8 trailing spaces compile identical to none; "keep the spaces" alone gives the same 93 px | CONFIRMED | [probe] `probe_out.txt` V1, V2, V3 (compiled text 190 chars in all three, parsed len 34, widths 111/93, x 104/113) |
| 4 | Spaces before a self-closing marker are kept (`[empty]`, `[yield]`, `[flag]`, any name; with or without `trimwhitespace=false`) | CONFIRMED | [probe] V4 (empty, false), V5 (empty, default), V6 (yield), V7 (flag), V19 (`keep`), V21 (`pad n=8`): len 42, marker at 42; V12 `ab   [center/]cd` keeps the 3 spaces |
| 5 | Spaces right after a leading marker written with `trimwhitespace=false` are kept; leading spaces before any marker are removed | CONFIRMED | [probe] V11/V11b kept (`...Florin`, len 9); V9, V10 removed (len 6) |
| 6 | Trailing NBSP is trimmed too; backslash-space is a compile error; `{"   "}` becomes a runtime substitution | CONFIRMED | [probe] V8 (compiled `Roulette`), V17 (`Syntax error: Unexpected " "`), V18 (`{0}`) |
| 7 | Default `[x/]` swallows one space after it; nothing trims the space before a marker | CONFIRMED | [probe] V13 (`ab[center/]   cd` -> `ab..cd`, 2 spaces), V12 (3 kept before), V14 (`trimwhitespace=false` keeps 3 after) |
| 8 | The DLL ignores a marker it does not know, `empty` included; the box skips the Center token (`AlundraDialogueBox.cs:606-607`) | CONFIRMED | [code] `AppendMarker` switch has no `empty` case; box `case Center: continue;` at :606-607; no `"empty"` string in `Alundra/` or `CasaEngine/` production code; V19 shows the same widths with an unknown name |
| 9 | The Python port of the emitter matches all 24,784 exported nodes byte for byte | CONFIRMED (stronger form) | [probe] `emit/`: a namespace-renamed copy of the REAL `YarnTextEmitter.cs` + `AlundraYarnFunctions.cs` run over all 24,431 map+shared strings reproduces every exported node, 0 differences, 0 errors (`emit_out.txt`). ETC (353 nodes) not rerun: it holds no `\H` |
| 10 | 823 `\H`; exactly 3 differ: `M472_S025`, `M473_S025`, `M474_S025` page 0 second line, 125 (binary) vs 93 (export), x 97 vs 113 | CONFIRMED | [bin] my own `CalcTextWidth` port (`binwidth.py`): line 1 = 111, line 2 = 93 without spaces, **125 with the 8 spaces**. [probe+calc] `census/` + `census_raw.py`: every compiled `.dialogue` (485 files, 31,757 pages) parsed with the engine's LineParser configuration; 823 centred lines compared with the raw strings, **3 differ** (the same three), 110 pages differ by spaces in all (107 plain, 3 centred) |
| 11 | font3.fnt advances equal the exe table on every glyph used | CONFIRMED with a nuance | [calc] `adv_check.py`: 144 of 145 `char` lines equal the exe table; id **339** (`œ`, advance 9) has no exe entry (the table has 256 rows; reading row 339 returns garbage). No centred line contains a char above 255; the 86 codes used by centred lines are all present and equal |
| 12 | Disc data: the same three, `Florin\W5Roulette` + 8 x 0x20 + NUL at 106732056, 106951148, 107192672; `ETC_RES.R` has no `\H` | CONFIRMED | [bin] `disc_scan.py` (`disc_scan_out.txt`): the same three offsets, followed by `b'        \x00\\DV'`; `ETC_RES.R`: 0 `\H`; no other centred last line ends on spaces |
| 13 | The binary types a space like any char: no 0x20 test in the loop head `0x80046100-0x8004616C`, normal path `0x80046D24-0x80046DF8` | CONFIRMED | [bin] head tests NUL, 0x0A, `{`, `}`, `\`; normal path stores the byte, renders it (`0x800478C4`), adds its advance, one step. Whole interpreter scanned for 0x20 immediates: only stack offsets and the digit-run zero-to-space fill at `0x80046C38` (flag number parsing), unrelated |
| 14 | `CalcTextWidth` (`0x8004771C`) takes the rest of the line after `\H`, stops at `\N`/`\A`, counts spaces | CONFIRMED | [bin] caller `0x800469F0-0x80046A08`: the `\` case has already advanced the cursor to `H` (`0x80046278`), the `\H` case advances once more, so the scan starts after the `H`; jump table `0x80024098`: `\A` and `\N` return; the sum includes every space |
| 15 | x of a centred line = `16 + (288 - w)/2` | CONFIRMED | [bin] `RenderText 0x800455B4`: `cfg.x + (cfg.w*8 - width)/2`; `msgBoxCfg 0x8009CFBC` = {16, 168, 36, 7}; 177/2 -> 104, 163/2 -> 97, 195/2 -> 113 |
| 16 | Leading-space pages: `M311_S029` p1, `M398_S029` p1 (flag then space; lost by the emitter's F0-R2, not by Yarn); `Etc_0069`, `Etc_0077` | CONFIRMED (the census); the "4 px further right in the binary" part UNCONFIRMED | [calc] 2 map pages + 2 ETC pages lose a leading space, none centred; [probe] Yarn keeps a space after a leading flag marker (V11-like), so the emitter alone drops it; raw is `\0101 Pas mal...` (leading zero, the report writes `\101`). The 4 px is a deduction (the binary's digit-run code then reads ' ' as a normal char); not seen running |
| 17 | Edge counts: 164 trailing (108 map = 78 at the very end + 30 before a flag/yield, ETC 56), 4 leading; the 30 are `_S022` (15) + `_S108` (15) of 15 maps (M389-392, M412-420, M476, M478); 3 centred = S025 | CONFIRMED | [calc] `census_cls_out.txt`: 108 trailing, 78 + 30, S022 15 / S108 15, the same 15 maps; ETC (`census_etc.py`): 56 trailing, 2 leading, 0 centred |
| 18 | The fix rule and the emitted S025 line | CONFIRMED | [probe] my own independent patch (`emit/mkpatch.py`, written from the rule, not from the diff): output for the three nodes is exactly `...Roulette` + 8 spaces + `[empty trimwhitespace=false/]` + 1 space + `#line:M47x_S025_p0` (`emit_out.txt`); the nine edge-case rows of the report give identical emitted bodies (`emit_out.txt`, section edge cases) |
| 19 | Through the DLL path: second line 125 px at x 97, 43 typed steps instead of 35 | CONFIRMED | [probe] `probe_out.txt` V4: tokens 47, steps 43, widths [111, 125], x [104, 97]; V1: 39 tokens, 35 steps, [111, 93], [104, 113]. The box draws one step per `Character` token without special-casing spaces ([code] `AlundraDialogueBox.cs:585-620`) |
| 20 | Export delta is exactly 6 files; the compiled program bytes of the three assets are identical; `line_texts` changes one key per asset | CONFIRMED | [probe] full-file compile with the real compiler and the Alundra declarations, old file vs file with the patched line: 0 errors, programs equal (7,063 bytes each), same key set, **changed keys exactly `line:M47x_S025_p0`**; the old compile equals the exported `.dialogue` (`line_texts` and `program_base64`) byte for byte. 3 changed nodes -> 3 `.yarn` + 3 `.dialogue` |
| 21 | `report.json` counters and `AssetInfos.json` unchanged | CONFIRMED by reading (not run) | [code] counters are `Yarn.Nodes/Lines/EmptyPages/GlyphMarkers/FlagMarkers/YieldMarkers/...` (`YarnDialogueWriter.cs:369-377`), none keyed on the `empty` marker; the S025 pages are not empty (`isEmpty` false); `AssetInfos.json` carries ids/names/paths, no hash or size |
| 22 | Tests: no existing test moves; `WholeCorpus_EveryPageOfEveryNodeMatchesTheReferenceDecoder` goes red on exactly the 3 pages until the oracle moves; census counts unchanged | CONFIRMED by reading (tests not run) | [code] no existing converter test has a `\H` with trailing spaces (`YarnTextEmitterTests:249`, `ReferenceTextDecoderTests:396, 510, 574` only); `Decode_WholeCorpus_MatchesMeasuredCensus` counts empty pages as "text empty and a single `empty` marker" (:856), so the S025 pages do not count; counts 31,757 / 95 / 823 / 485 / 24,784 stay. `Alundra.Tests`: no test names 472-474 / S025; `SweptMaps` (:308) has none; `AlundraYarnVariableCorpusTests` reads programs only; `OraclePages.PageBytes` already maps `empty` to nothing (`AlundraTextBoxOracle.cs:727`) |
| 23 | Oracle canonical string after the fix: `markers=[voice@0 id=-1, center@0, center@21, glyph@27 id=21, empty@43]`, 8 trailing spaces | CONFIRMED (by position arithmetic) | `br` at 20 becomes `\n` (shift +1): center@21, glyph@26+1, empty@42+1 |
| 24 | Rejected candidates: NBSP / backslash-space / inline expression fail; `[center width=N/]` is technically possible | CONFIRMED / UNCONFIRMED | first three: [probe] V8, V17, V18. `width=int:125` property: not rerun (no impact on the decision) |
| 25 | Stray `out*.txt` files in the NuGet cache | CONFIRMED | `yarnspinner.compiler/3.2.1/lib/netstandard2.1/out.txt` and `yarnspinner/3.2.1/lib/netstandard2.1/out2.txt, out3.txt, out4.txt` (all 2026-09-28 22:16; string dumps of the DLLs). The report says "out*.txt in yarnspinner*": it is `out.txt` in the compiler folder and `out2-4.txt` in the runtime folder |
| 26 | "Reusing `empty` is a technical choice, a new name behaves the same" | CONFIRMED | V19; no production consumer of `empty`; a new name would need `BuildActualPage` (:1158), `OraclePages.PageBytes` (:727), a `ReferenceMarker` factory (:72), as stated |

## Corrections (things to change in the report or the plan)

1. **ADR number: 0036 is taken.** ADR-0036 "The font3 glyph rectangles come from the glyph table of the binary" was committed at HEAD
   (`262f92a`, 2026-10-05 23:30:40, README row 52, status Proposed). The discovery's "next free number at HEAD: 0036" was true at
   `897504b` and is stale. The S025 ADR is the next free **parent** number, **0037** at this HEAD (the `ADR-00xx du moteur` mentions in
   the plans are the engine submodule's own numbering). Re-check at write time: f2b0 may add another. The ADR-0006 status line then
   reads "partly superseded by ADR-0037". Related: f2b0 F2B0-0..2 is now done (it was "planned" in the task text). It changes
   `UI/font3.fnt` rectangles and `yoffset`, not the advances I re-read (space 4, glyph 21 = 14); the S025 export delta (6 Pub files)
   is disjoint from f2b0's (font files), but the "manifest before/after" proof must be taken against the post-f2b0 export.
2. **A document is missing from the "touched" list: `docs/formats/dialogues-yarn.md`.** It is the format contract and says, in the
   marker table, `[empty trimwhitespace=false/]` = "page sans texte" (:64), edge spaces "retirés (D-E15-8)" (:66), the
   flag/yield transparency (:86) and under "Limites" "Les espaces en bord de page sont perdus (Yarn les retire) ; aucun lecteur actuel
   n'en dépend" (:195). After the fix that last sentence is false (the f2b1 view depends on S025's spaces) and `empty` has a second
   meaning (a guard after kept trailing spaces). Same family, cosmetic: the comment "the edge spaces already cut by D-E15-8" in
   `Alundra.Tests/AlundraTextBoxOracle.cs:658`, and `plan-e19-opcodes.md` F0-R2 (:4302).
3. Line cites that drifted (cosmetic): converter csproj ProjectReference is `:95` (not :88), `Alundra.Tests.csproj:35` (not :34),
   `YarnTextEmitterTests` `\HHi` test is at `:249` (not :247), plan f2b1 sketch "Ligne S025 corrigée au convertisseur" is at
   `plan-e19-opcodes.md:4901` now (not :4880) because f2b0 added text above it.
4. "font3.fnt advances equal the exe table on the 145 characters" (the f2b discovery summary, `plan-e19-opcodes.md:4784`, and the
   report's claim 11) is 144 of 145: id 339 (`œ`) has no row in the 256-row exe table. No effect on S025 (no centred line holds it).
5. ETC nuance: "none [of the 56 ETC pages] is typed by the box" is right for the dialogue box, but `plan-e15-yarn.md:746-748` records that
   three ETC texts in the inventory ranges (`0x206`, `0x239`, `0x2A6`) lose a trailing space, "un caractère de moins à révéler dans
   l'inventaire": a known gap, unchanged by this fix, worth one line in the new ADR so the "keep all edge spaces" rejection is complete.

## Nothing found that changes the plan's direction

- No second affected centred line anywhere (maps, shared, ETC, raw disc data), no centred page with a leading-space problem, no
  consumer of `empty` in production code, program bytes identical, the three Pub assets compile with 0 errors.
- The narrow rule (keep the trailing spaces only when the page's last line holds `\H`) is the right size: the only pages it touches
  in the whole corpus are the three S025 ones (my patched copy changes exactly 3 of 24,431 nodes). A page whose last line is centred
  and whose spaces precede a trailing flag/yield would also be changed by the rule, but no such page exists (the 30 flag/yield pages
  are all non-centred).
- Not done by me (read-only constraint): the real converter and test runs (the new tests' expected values are computed, not run);
  the `[center width=N/]` property probe.

## Files in this folder (all scratch)

`probe/` (Yarn-only probe, `probe_out.txt`), `census/` (+ `export_canon.jsonl`, the exported side), `census_raw.py` (+ `census_raw_out.txt`),
`census_cls.py` (+ `census_cls_out.txt`), `census_etc.py`, `disc_scan.py` (+ `disc_scan_out.txt`), `vdis.py` (my disassembler),
`binwidth.py` (CalcTextWidth port), `adv_check.py`, `emit/` (`orig/` copies of the real emitter, `mkpatch.py`, `o/` unpatched copy, `p/` my
patched copy, `Program.cs` harness, `emit_out.txt` via `tee` into `../emit_out.txt`).
