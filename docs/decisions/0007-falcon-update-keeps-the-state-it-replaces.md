# ADR-0007: The falcon update command keeps the state it replaces, and each `\X` reads on the original's side of the update

- **Status**: Accepted
- **Date**: 2026-09-27
- **Source**: this chantier: `docs/plan-e15-yarn.md` §1 and E15.b (closing review of E15.b on 2026-09-27; the author's answer "Commande + fonction avant"); supersedes the `\X` bullet of ADR-0006

## Context

- ADR-0006 maps every page holding `\X` to one `<<falcon_update>>` placed before the line, which records the temporary falcon count and then updates, plus side-effect-free functions in the text. Only `falcon_temp()` reads the recorded state.
- In the original decoder, `\X0`, `\X2` and `\X4` read first and update after, while `\X1`, `\X3` and `\X5` update first and read after (`alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs:505-565`). `UpdatePlayerProgressState` rewrites the category index `g_textCategoryIndex` from `GameFlags[0x2c]` (`TextDecoder.cs:1098-1183`); it is the only writer of that index in the decompilation. A `\X2` or `\X4` that opens a page therefore shows the item name of the index left by the previous update.
- Yarn Spinner 3.2.1 refuses a command inside a line (error YS0020). The functions of a line are evaluated once, left to right, just before the line is delivered (probe run on 2026-09-27).
- Measured on the exported corpus: 7 pages hold `\X`, all on map 134 (strings 13 to 20). `\X0` opens page 13, `\X2` opens pages 16 and 17, and page 19 is the only page with two codes (`\X3` then `\X4`). The ETC table holds none.
- The author kept the command form of ADR-0006 rather than functions with side effects.

## Decision

- A page holding any `\X` gets exactly one `<<falcon_update>>` before its line, whatever the number of codes. The command first keeps the state it is about to change (the temporary falcon count and the category index), then runs `UpdateNumberOfFalcon` and `UpdatePlayerProgressState`.
- The converter picks the function by code and by position in the page, so that each read sees what the original reads there:
  - `\X0` opening the page (first `\X` of the page) → `falcon_temp()`, the kept count;
  - `\X2`/`\X4` opening the page → `category_item_name_before()`, the name for the kept index;
  - `\X2`/`\X4` after another `\X` of the page → `category_item_name()`, the name for the current index;
  - `\X1` → `falcon()`, `\X3` → `category_threshold()`, `\X5` → `category_remaining()`, all on the updated state;
  - a `\X0` after another `\X` of the same page is a conversion error.
- The commands placed before a line follow the order of first appearance of their code in the page text.

## Consequences

- The function contract has seven functions instead of six. The DLL registers `category_item_name_before()`, and keeps the category index as the original does; its initial value and whether it persists are established from the executable when the DLL reads Yarn (E15.c).
- The equivalence oracle gives the before and after reads distinct test values, so a read placed on the wrong side of the update is caught.
- The `\X` bullet of ADR-0006 is superseded by this record; the rest of ADR-0006 stands.
- A future `\X0` after another `\X` would need a function reading the updated temporary count, which the original always finds at 0 there. None exists in the corpus.
