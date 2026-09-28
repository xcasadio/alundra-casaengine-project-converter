# ADR-0008: The dialogue box uses font3 and draws each glyph marker as the matching font3 character

- **Status**: Accepted
- **Date**: 2026-09-28
- **Source**: this chantier: `docs/plan-e15-yarn.md` §0.1 (D-E15-12, the author's answer while preparing E15.c on 2026-09-28) and the E15.c section; refines how ADR-0006's glyph markers are drawn

## Context

- ADR-0006 exports every `\W<c>` code and the raw ETC bytes `0x1A`/`0x1C` as `[glyph id=N/]` markers, drawn by the DLL, without changing the font. The glyphs are font3 glyphs 16 to 29 (• … “ ” ☆ → ← ↑ ↓ □ △ ○ ✕).
- `UI/font3.fnt` carries glyphs 16 to 29 at their own code point (`UI/font3-charset.json`, e.g. 18 at x=32 y=16 with advance 11).
- The dialogue box is the engine's `DialogueScreen`: `RefreshLine` gives `DialogueLine.Text` to an MGUI `MGTextBlock` and ignores the line's attributes (`CasaEngineMonogame/CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs:358-367`). The DLL has built it without a font family since E12.a (`1c89427`), and `lblLine` in `UI/Screens/DialogueScreen.xaml` names none, so the box uses the default TTF font, although D-E12-2 chose font3 for it.
- The inventory and sub-inventory screens already hold font3 through the engine's `UIFontRegistry` and show their texts in font3 (`Alundra/Scripts/AlundraInventoryScreen.cs:57-74`).
- MGUI can also draw inline images in a text block (`[img=Name WxH]`, `MGTextRunImage`), resolved by the name of a catalogued sprite; that option needed glyph sprites and would mix two fonts in one line.

## Decision

- The dialogue box shows its text in font3: the DLL's presenter holds font3 through `UIFontRegistry`, as the inventory does, and gives it to `DialogueScreen` through its existing `fontFamily` parameter (line text and yes/no labels).
- The DLL turns each line it receives into font3 text: `[br/]` becomes a line break and `[glyph id=N/]` becomes character `N` at the marker's position. The inventory texts read from the ETC asset get the same treatment, which reproduces what the raw bytes gave them.
- A test must first prove that MGUI keeps and draws a character 16 to 29 of font3 as the font's glyph; if it does not, the gap is reported (MGUI) and the choice goes back to the author, with no workaround.

## Consequences

- The glyphs appear as in the original, from the same font, and the stray operand digits disappear.
- The dialogue text changes look, from the default TTF font to the game's own bitmap font, as D-E12-2 intended.
- No engine change is needed: the font path (`UIFontRegistry`) and the `fontFamily` parameter already exist.
- The E12.c markers (`voice`, `center`, `slow`) stay in the data and are ignored at display until E12.c.
