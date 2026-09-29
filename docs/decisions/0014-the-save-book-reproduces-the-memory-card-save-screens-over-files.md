# ADR-0014: The save book reproduces the original's memory-card save screens over four file slots

- **Status**: Accepted
- **Date**: 2026-09-29
- **Source**: this chantier: `docs/plan-e16-etat-partie.md`, slice E16.e (author decisions D-E16-34 to D-E16-38 of 2026-09-29, choices L1 to L8 of the detailed plan, security review SE1 to SE13, O-E16-18 settled by the author's standing rule that the France binary settles disagreements with the decompilation)

## Context

- In the original, the player saves by interacting with a book entity ("SaveBook", sprite type 237, placed on 65 maps). Its native handler (`0x8007B998`) asks "Enregistrer tes progrès?", then OUI/NON, captures the game (`UpdateSavedData`) and runs the memory-card manager's save flow (`StartMemoryCardProcess`, about fifty states), which lists the card's saves in a four-box carousel and writes the chosen one.
- Many of those states only serve the PS1 memory card: port scan, formatting, free space and deletion when the card is full, block reservation, icon and checksum, missing or changed card. The port writes files through the engine's save service (engine ADR-0044) and E16.c's validated save object (ADR-0012).
- The decompilation differs from `ALUN_CD.EXE` on this flow: the opening heading is ETC `0x83`, not `0x84`; a slot's first line is the chapter name and the second the summary, not the reverse; an empty slot shows two empty lines, not `"n."` (the decompilation's desktop adaptation).
- The book's handler has two defects in the binary: a failed OUI/NON opening parks it in a state with no case, leaving the hero locked; the carousel's tint keeps interpolating past its target and rests brighter than intended.
- The dialogue window had a title-bar close button (MGUI default) that could end a pending choice without an answer.

## Decision

- **D-E16-34** — The memory-card save screens are reproduced over files. Kept, in the original's order: "Examen de la Carte Mémoire…", the four-box carousel with heading `0x83`/`0x84`, Up/Down/Cross and an OUI/NON confirm, "Enregistrement de l'histoire…", the write, "Histoire enregistrée." or the failure message, and the end chain (Square to dismiss). The write goes to the chosen slot in the binary format; every PS1-hardware state is dropped.
- **D-E16-35** — Four fixed slots, `slot1` to `slot4`.
- **D-E16-36** — A slot is labelled like the original, chapter name on line 1 and summary on line 2, both recomputed from the slot's save after `TryValidate`; the file's metadata is never read, and a slot that is empty, unreadable or refused shows the two empty lines of an original empty slot.
- **D-E16-37** — Only the texts of the kept states are used; the eight ETC texts missing from the export are empty in `ETC_RES.R` itself, and the hardcoded Japanese test strings are unreachable.
- **D-E16-38** — The dialogue window has no close button (`IsCloseButtonVisible="False"`), like the original.
- The native-AI hook only covers the SaveBook (sprite type 237, F-interact code 1 and Tick code 72); every other native handler stays a counted no-op.
- The picker's OUI/NON goes through the DLL's port of `InitializeAsyncOperation` (`AlundraDialogueDirector.OpenChoice`), and is closed by `CloseStandaloneChoice` without clearing the save screen's `MenuOpen`.
- The binary wins over the decompilation (O-E16-18), and the two original defects are fixed: a failed OUI/NON abandons the book and unlocks the hero; the tint stops at its target.

## Consequences

- Saving is reachable in play, from the 65 books, with the original's screens and texts; loading stays with the F9 recipe key until a title screen exists (D-E16-10).
- A hostile slot file can only make its label blank; it never reaches the live state, and nothing is written without the player's confirmation of that slot.
- The resting tint of the middle box is `0x80`, not the original's `0x99`/`0x8C` (fixed defect, to be confirmed in the recipe).
- The book's save does not refresh the in-memory copy used by "Retry" after death (E18).
- Residual, reported: a script that closes the book's box during its 61-tick wait would leave the following OUI/NON without a box; an old save screen could stay pushed if the world changed while it was shown (no known trigger, `MenuOpen` freezes the map); a project exported before E16.e lacks the `SaveScreen` catalogue entry and must be exported again.
