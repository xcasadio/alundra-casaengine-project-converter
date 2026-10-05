# E19.m5, M-38: exact texts

The one-frame delay of a switch written by a map-event program (measured by the verifier of E19.t on map 392: `0x5B @64` at frame
121, the sound and the visible switch at frame 122) comes from the DLL's existing frame order: the engine updates every entity
(script and animation step included) before the world proxy, whose `Update` runs the map events (`AlundraWorldProxy.cs:1955-1960`,
`RunMapEventsPass` at `:2106`). D-E19-64 accepts a one-tick gap for the text box only (plan, decisions list). Source: discovery
`e19m5-disc/notes.md` (M-38) and its verification `e19m5-verify/verify.md`, 2026-10-05.

## 1. `Alundra.Tests/AlundraAnimationSoundTests.cs:281` (comment only)

Old (one line, 8-space indent):

```
        // 0x5B @64 (frame 121) sets the hero's animation 3; the hero's tick of the frame after it (the world proxy runs after the entities, D-E19-64) asks
```

New (one line, same indent):

```
        // 0x5B @64 (frame 121) sets the hero's animation 3; the hero's tick of the frame after it (the DLL's frame order: the engine updates the entities before the world proxy runs the map events; D-E19-64 covers the text box only) asks
```

## 2. `docs/decisions/0028-every-animation-switch-plays-its-sound-appearances-included.md`

Line 29 is not rewritten. Append at the end of the file (after the last line, one blank line before the heading), as ADR-0020 did:

```
## Amendment (E19.m5, M-38)

The consequence "a switch written by a map-event program is heard one frame after the opcode (D-E19-64)" names the wrong cause.
D-E19-64 accepts that gap for the text box only. The lag comes from the DLL's existing frame order: the engine updates every entity,
script and animation step included, before the world proxy, whose `Update` runs the map events (`AlundraWorldProxy`). The verifier of
E19.t measured it on map 392: `0x5B @64` at frame 121, the sound and the visible switch at frame 122. No decision of E19.t changes it.
```

The index row of ADR-0028 in `docs/decisions/README.md` does not change.

## 3. `docs/plan-e19-opcodes.md`, E19.t, "Écarts" (2)

The old text spans a line break and the 4-space indent of the next line; match it across both lines:

Old (end of one line, start of the next):

```
la demande suit l'opcode d'une image
    (D-E19-64) ;
```

New (the same two-line layout; the rest of both lines unchanged):

```
la demande suit l'opcode d'une image
    (ordre existant de la DLL : les événements de carte passent après les entités ; D-E19-64 ne vaut que pour la boîte, M-38) ;
```

## Check after the edit (revised after review n°2: the ADR is amended, never rewritten)

- `rg -n "D-E19-64" Alundra.Tests/AlundraAnimationSoundTests.cs` finds only the new comment at `:281`, which names the DLL's frame order
  and says D-E19-64 covers the text box only.
- `rg -n "D-E19-64" docs/decisions/0028-every-animation-switch-plays-its-sound-appearances-included.md` finds line 29, unchanged (the
  expected leftover of an amend-only ADR), and the lines of the `## Amendment (E19.m5, M-38)` section, which comes after it and names
  the DLL's frame order as the cause.
- The plan sentence of E19.t "Écarts" (2) reads "(ordre existant de la DLL : les événements de carte passent après les entités ;
  D-E19-64 ne vaut que pour la boîte, M-38)".
