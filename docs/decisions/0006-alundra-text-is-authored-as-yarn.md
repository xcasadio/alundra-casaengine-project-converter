# ADR-0006: All Alundra text is authored as Yarn and the raw text tables are no longer exported

- **Status**: Accepted; partly superseded by ADR-0025 (numeric codes and `\Y`); the `\X` bullet is superseded by ADR-0007
- **Date**: 2026-09-27
- **Source**: this chantier: `docs/plan-e15-yarn.md` §0.1 (D-E15-1 to D-E15-5, approved by the author on 2026-09-27) and §1/§5 (D-E15-6 to D-E15-9, the author's answers to the E15.0 measurement on 2026-09-27)

## Context

- The converter exports the game's text as raw JSON: one 128-string table per map (`{map}.strings.json`), the ETC table (`Dialogues/global-strings.json` plus `Dialogues/etc-index.json`) and an inventory of control codes (`Dialogues/control-codes.json`), written by `alundra-casaengine-project-converter/Writers/TextWriter.cs`. The shared table `map_alundra` (128 entries, 99 real texts, reached when bit `0x80` of a text id is clear) is not exported.
- The gameplay DLL parses those strings itself (`Alundra/Scripts/AlundraDialogueTextParser.cs`): `\A` pages, `\N` lines, numeric codes set temporary flags; every other code is stripped but its operand digit stays on screen, so `\W2` shows "2" instead of the ellipsis the original draws.
- The original decoder (`alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs`) and the France executable give the codes' meaning. `\W<c>` draws font glyph `c − 0x20` for a digit and `c − 0x27` for a letter (confirmed in `ALUN_CD.EXE` at `0x800462e0`–`0x800462f0`; the C# at `TextDecoder.cs:442-443` drops the letter branch). The glyphs are • … “ ” ☆ → ← ↑ ↓ and the □ △ ○ ✕ pad buttons, none of which has a Unicode entry in the exported `font3.fnt`. `\X0`–`\X5` insert falcon counts, an item name, a threshold or a remainder, and update the falcon count and the player's progress. `\V<n>` inserts a game variable. `\Y` ends a render step only.
- Measured on the exported corpus (E15.0): 9 868 real map strings, 99 in `map_alundra`, 348 in ETC, plus 14 469 `#Disuse` placeholders; 11 170 `\W` codes (10 515 `\W2`); `\X` only on map 134, `\V` only on the pub mini-games; 442 colons, all of which Yarn's `LineParser` would read as a character name; 37 strings ending with `\A` and 58 pages that only set a flag.
- Yarn Spinner 3.2.1 (tested against the library on the real shapes): a line holding only `[empty/]` compiles to an empty text; `\:` and `\#` keep their character; a self-closing marker swallows the next space unless it carries `trimwhitespace=false`; leading and trailing spaces of a line are removed at compile time.
- The engine's `YarnDialogueRunner` plays compiled `DialogueAsset`s but has no extension point for commands, functions or variable storage (engine plan `CasaEngineMonogame/ai-agent/tasks/yarn-extension-points-tasks.md`).

## Decision

- **Yarn is the only text format of the exported project.** The converter writes one `.yarn` source and one compiled, catalogued `.dialogue` per map, plus `Dialogues/Shared.yarn` (the 128 entries of `map_alundra`) and `Dialogues/Etc.yarn` (the ETC table). One node per string (`M{map}_S{nnn}`, `Shared_S{nnn}`, `Etc_{iiii}`), one Yarn line per page, with deterministic `#line:` ids (`{node}_p{k}`).
- **The raw text tables are no longer exported**: `{map}.strings.json`, `global-strings.json`, `etc-index.json` and `control-codes.json` disappear, and the converter removes those left by an earlier export. The code inventory moves into `report.json`. `EtcIndexTable.csv` stays a converter input.
- **Mapping of the control codes**:
  - `\A` separates Yarn lines; `\N` becomes `[br trimwhitespace=false/]`; `\Y` is dropped.
  - A numeric code becomes the command `<<flag n>>` right before the line of its page, with the value normalised (`\0999` and `\999` are the same flag).
  - `\W<c>` becomes `[glyph id=N trimwhitespace=false/]` with `N` given by the executable's formula, drawn by the DLL; the raw bytes `0x1A`/`0x1C` of the ETC item descriptions become the same marker (glyphs 26 and 28). The font is not changed.
  - A line with any `\X` code is preceded by the command `<<falcon_update>>`, which records the temporary falcon count and then updates the falcon count and the player's progress, in the original's order; the text uses side-effect-free functions: `falcon_temp()` (the count recorded before the update), `falcon()`, `category_item_name()`, `category_threshold()`, `category_remaining()`. `\V<n>` becomes `game_var(n)`.
  - `\B`–`\G`, `\H` and `\T` become markers kept for the later dialogue-fidelity step (`[voice id=…/]`, `[center/]`, `[slow/]`), all with `trimwhitespace=false`.
  - A page without visible text becomes a line holding only `[empty/]`, so the dialogue pacing does not change.
  - `:` is written `\:` and `#` is written `\#`. The `#Disuse` placeholders are kept.
- **Leading and trailing spaces of a page are lost**, as Yarn removes them; no current reader depends on them.
- **The engine only gains generic extension points** (injectable variable storage, command registry, function registration, markup parsing, single-line lookup); every command, function and marker above is provided and interpreted by the Alundra DLL.
- **The intro cinematic's conversion to a `.cutscene` is a separate step, E17**, not part of this one.

## Consequences

- The DLL resolves dialogue text, the yes/no labels and the item names and descriptions from `.dialogue` assets by node and line id. `AlundraDialogueStringsLoader`, `AlundraEtcStringTable`'s file reading and `MapCatalogReader.StringsRelativePath` go away, and the tests that write raw JSON fixtures build assets instead.
- Visible change: the stray operand digits disappear and the `\W` glyphs (ellipses, arrows, stars, quotes, pad buttons) are drawn as in the original.
- The text is translatable through the stable line ids; the translation tooling itself is not built. Yarn Spinner 3.2.1's built-in French plural is wrong and must not be relied on.
- The markers keep every original code in the data, so the later fidelity step (typewriter, voices, centring, delays) needs no new conversion.
- The `#Disuse` placeholders add about 14 500 nodes; at least 7 reachable scripts open one, so they must exist.
- The edge spaces of about 110 map pages and of the fixed-width ETC chapter titles are lost; a future reader that needs one handles it itself.
- Opcode `0xC4` (dialogue with a name) also opens text; the DLL does not implement it yet, and it will read the same assets when it does.
- The decompilation's `\W` letter branch (`TextDecoder.cs:442-443`) disagrees with the executable; the converter follows the executable.
