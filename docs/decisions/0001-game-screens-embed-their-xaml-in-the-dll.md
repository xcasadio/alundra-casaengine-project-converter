# ADR-0001: The game's XAML screens embed their XAML in the Alundra DLL

- **Status**: Accepted
- **Date**: 2026-09-21
- **Source**: this chantier: `docs/plan-e13d-inventaire.md`, slice D5 (the main inventory screen), the author's answers of 2026-09-21

## Context

- The author's rule for UI: a screen's structure is declared in XAML, code finds its elements by name and pushes values (engine ADR-0035, `CasaEngineMonogame/docs/decisions/0035-xaml-authored-ui-screens.md`; runtime bridge `UIScreenLoader` and `XamlUIScreenBase`).
- The main inventory (E13.d D5) is the first XAML screen of the Alundra DLL; every screen the DLL had so far (the HUD) is built in C#.
- A XAML screen can take its markup from a catalogued `.uiscreen` asset of the project (the RPGDemo precedent, `CasaEngineMonogame/Projects/CasaEngine.RPGDemo/Scripts/Screens/RpgDemoScreenAssets.cs`) or from an uncatalogued `XamlDocumentSource`, for instance an embedded resource (the engine's own `DialogueScreen`, `CasaEngineMonogame/CasaEngine/Framework/Dialogue/UI/DialogueScreen.cs:16-22, :81-82`).
- `alundra-project/` is generated in full by the converter and is not versioned (`.gitignore`): anything edited there by hand is overwritten by the next export.
- `Alundra.Tests` cannot build a headless `MGDesktop`: the engine's `HeadlessUiTestHarness` is internal to `CasaEngine.Tests` (`CasaEngineMonogame/CasaEngine.Tests/UI/HeadlessUiTestHarness.cs:29`), which `Alundra.Tests` does not reference.

## Decision

- A XAML screen that belongs to the game (the Alundra DLL) keeps its markup in the DLL's source tree, `Alundra/Screens/*.xaml`, shipped as an embedded resource of `Alundra.dll` and loaded through `XamlDocumentSource` and `XamlUIScreenBase`, like the engine's `DialogueScreen`. It is neither exported by the converter into `alundra-project/` nor copied there by the DLL's build.
- `Alundra.Tests` gets its own minimal headless `MGDesktop` harness, a reduced copy of the engine's internal one, instead of exposing the engine's test harness to it.

## Consequences

- The converter is not touched, and the screen's single source is versioned with the code that drives it.
- Such a screen cannot be opened as a `.uiscreen` from the editor's content browser; it is edited in the DLL's source tree. Choosing otherwise later is a new ADR.
- A change to the markup needs a rebuild of the DLL.
- The test harness duplicates a small part of `CasaEngine.Tests` (a text engine that measures without drawing, a desktop runtime); a divergence between the two copies is possible if the engine's harness evolves.
