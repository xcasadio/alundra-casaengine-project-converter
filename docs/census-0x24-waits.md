# Census of the `0x24` (Wait force adjusted) sites

E19.d D5 (docs/plan-e19-opcodes.md §1.2g, D-E19-21, corrected by D-E19-24). Static census of every `0x24` of the exported corpus: what the actor waits on, and whether the wait can end on a wall in the port. `0x24` returns 1 when the logic entity's `ForceAdjusted` is nonzero (E19.d D4); in the port that flag is raised by a blocked step on the cell field (E19.a2/E19.a3), not yet by contacts between entities (E19.h), by the screen-border clamp (O-E19-17) or by native AI (E14).

**Verdict on the story path: 31 of the 59 reachable sites of the story-path maps end on a cell ("wall found"); 28 are not reached in the model (separate risks, listed below); 0 are "no wall" and 0 are "indeterminate", so the stop rule of D5 does not trigger.**

## Why this is the second version

The first run of this census (stopped, O-E19-16) found 26 of the 59 reachable story-path sites outside "wall found" (17 "no wall", 9 "indeterminate"). A read-only follow-up, checked by two independent passes, showed that those sites came from wrong start positions in the model, not from the port: the villagers of map 10 and Nestus of map 165 were started at their record position although the map-load pass never spawns them there (`SpriteDirection` bit 0x40 clear, binary gate `0x8003A268`; they appear through `0x8A`, or `0x2D` then `0x64`), and the hero was cast from every free cell of the map event's zone. D-E19-24 corrects the model as below and reruns it; the screen-border clamp (O-E19-17) is not needed to decide any story-path site.

## Method

1. **Population** (unchanged): every `0x24` at an instruction boundary of the linear decode of the code region (after the six entry tables) of every map: 429 sites in 82 maps.
2. **Reachability** (unchanged, mechanical rule of the plan). Roots: the B entry of every map event; for every record, the entry designated by each of its A, C, D, E and F slot indexes when bit 0x80 is set (`index & 0x7F`). *Reachable*: every instruction reached from a root by following every jump, branch and call (all branches open; `0x7D` returns to every call site seen); `0x40` (always `v2 = 0` in the corpus) and `0x41` open no script entry. *DLL only*: reached only from slot entries whose index has bit 0x80 clear. *Dormant*: neither. All 1714 map events have bit 0x80 set.
3. **Actor and state** (unchanged walk): a path-sensitive forward walk of every root program, all branches open, tracks the logic entity (`0x42`; `0x43 [n]` takes record `n`, taken as found; `0x43 [0x80]` unchanged; `0x43 [0x81]` the hero), its animation (`0x1A`, `0x59`, `0x5B`, and `0x0B`, whose first operand is the animation), its direction (`0x09`, `0x08`, `0x0A`, `0x3A`, `0x0C` as the four cardinal values, `0x5A`, `0x5B`), the hero lock (`0x10`/`0x11`) and the position when it can be pinned, moved along the direction by the known waits (`0x37 Wait n`: `n` ticks at the animation speed; `0x0B`/`0x1E`/`0x1F`: the operand radius or distance; `0x24`: up to the wall).
4. **Speed**: `Speed` of the animation in the actor's sprite header (`Data/sprite-records.json` `AnimSets`; the hero is the prefab `Alundra`, id `192c2eeb-...`).
5. **Ray** (unchanged): the footprint of the actor (`OffsetX/Y`, `SizeX/Y` of its header) is moved along its direction (the major axis one pixel at a time) on the cell grid of the map: the first cell whose `walkability | ground_property << 8` hits the actor's mask (`0x40`, `|= 0x01` with ClassB, `|= 0x1000` with ClassA, as `AlundraCellsCollisionField.WalkabilityMaskFor`), or whose ground height (slopes as `ComputeGroundHeight`) is higher than the current one by more than the controller's `step_height`, ends the ray (a wall). The ray ends with *no wall* when it leaves the map (the DLL does not port the screen clip of `ApplyEntityForces`, O-E19-17).
6. **Classes**: *wall found* (the wait ends on a cell), *zero speed* (the animation does not move the actor: it waits for an outside contact, E19.h or E14), *no wall* (the ray leaves the map), *indeterminate* (logic entity, animation, direction or position not deducible), and, new, *not reached* (no state of the walk arrives at the site, see 9). The class of a site is the worst of the states that reach it (indeterminate > no wall > zero speed > wall found).

**Corrections of D-E19-24 (the corrected model):**

7. **Where each actor starts.** A record starts at its record position only if the map-load pass spawns it (`IsEnabled` nonzero and `SpriteDirection` bit 0x40 set). Otherwise its start positions are the positions written by *any* program of the map: `0x8A` and `0x64` (literal record selector, or `0x80` resolved to the logic entity, or any selector resolved in the walk), collected to a fixpoint; a record that only a `0x2D` activates appears at its record position unless such a write exists; a record with no position source is unpinned (then *indeterminate* if it reaches a `0x24`). `0x2D` inside a walk pins the activated record at its record position. An actor that walks during a wait of unknown length and then turns is no longer unpinned: both ends of its possible position (where it was, the wall it walks to) become states (an actor that could leave the map stays unpinned).
8. **Where the hero starts.** From its real arrivals, never from every free cell of the zone: (a) the story-chain entry of five programs whose entry is fixed by a flag gate that a static walk cannot see (table below), read from the operands of the `0x53` or from the portal rectangle, never typed, and chained to the walk of the source map (an entry whose source program is not reached is itself *not reached*); (b) otherwise every `0x53` and every portal of the corpus that lands in the map, kept when its tile is inside the zone of the map event, with the portal direction; (c) at a `0x3B` the free sample positions of its box (stride 4, at most 60 per box), carried through a `T` flag to the programs that wait for it; (d) the contact positions around an interaction record (one program).
9. **Blocked walks and flags: *not reached*.** A `0x0B` whose wall comes before its radius in the cell model (more than 1 px short) is not clipped and continued: it has no `ForceAdjusted` exit (`0x8003D468`), so the rest of that program is *not reached (0x0B blocked)* for the states that blocked. A wait (`0x36`) or a "flag on" branch on a flag that has setters but none reached is cut: *not reached (flag never set)*. `T` flags: setters of the same map, to a fixpoint over all programs of the map (setters in dormant code do not count). `G` flags: setters of the whole corpus, evaluated on demand through the walk of the map that holds each setter; a flag whose only setters are dormant, or that has none, is taken as set outside the scripts. Between maps the walk is iterated until the table no longer changes (4 passes).
10. **Hero lock** (the "hero control" column): the program's own `0x10`/`0x11`, plus the lock of the program that raised the flag the site's program waited for, plus the interaction contact.
11. **Check of the model**: on map 163 (`0x24 @183`, Jess) the model gives the actor `rec0 Jess`, animation 1, speed 128, direction 0, pinned at (924, 216) after her two walks, wall at 81 px, i.e. `PosY` 297.0 px, the contact measured by arc A8. On map 178 the hero sites give 177 px, 57-133, 90-114 and 0 px (the follow-up found 177, 57-136, 89-116, 0), on map 165 Nestus 0-48 and 0-209 px (26 and 209).

Limits of the model (the census is static): contacts between entities (E19.h) are not modelled, they change distances and can stop a `0x0B` earlier than a wall (example: the walk of Septimus `rec39` on map 10 is stopped by the activated block `rec41`, not by the wall at 482 px, so the `0x0B @3673` that follows is *not reached* only in the cell model); the animation or direction inherited from another program is not tracked; every branch is taken as possible (infeasible paths can add states); the footprint is the sprite header's, not the exact controller capsule; the hero jump and `0x25` are not ported, so a walk that needs them blocks (O-E19-19); `0x45`/`0x46` (NoObstacleSlide) decide whether a wall contact raises the flag at once (O-E19-20) and are not modelled; a `G` flag set by a dialogue (Yarn) in addition to a script setter could be taken as never set; the walks of the unreached maps use every arrival of the corpus.

## Totals

| | sites | maps |
|---|---|---|
| population (linear decode) | 429 | 82 |
| reachable | 395 | 76 |
| DLL only | 0 | - |
| dormant | 34 | - |

Expected by the plan: 395 reachable sites in 76 maps and 34 dormant or DLL-only: **matched** (no site is DLL-only: no record has a slot index with bit 0x80 clear that reaches a `0x24`).

| class (reachable sites) | all maps | story-path maps | other maps |
|---|---|---|---|
| wall found | 293 | 31 | 262 |
| zero speed | 1 | 0 | 1 |
| no wall | 4 | 0 | 4 |
| indeterminate | 4 | 0 | 4 |
| not reached (0x0B blocked) | 30 | 4 | 26 |
| not reached (flag never set) | 63 | 24 | 39 |
| not reached (entry) | 0 | 0 | 0 |
| total | 395 | 59 | 336 |

Sites reached in the model: 302 of 395; of these, 201 can run under the hero's held control (the discovery's linear count of 160 had no model of reach).

First model for comparison (O-E19-16): wall found 269, zero speed 1, no wall 83, indeterminate 42 (all 395 reached by construction); story path 33 / 0 / 17 / 9.

## Story path

Criterion (plan D5): every map of the Inoa zone (maps 162 to 182, 172 included), map 44 (Wendels Nightmare, reached from map 179) and map 10 (Overworld 2,1, next to Inoa), from map 163 to the first dream. Maps of the criterion that have a `0x24`: 10, 44, 162, 163, 164, 165, 172, 176, 178, 179. Map 172: its six sites (`@372`, `@383`, `@839`, `@1004`, `@1342`, `@1666`) are **dormant**, as expected.

**Verdict**: 59 reachable sites; 31 "wall found"; 0 "zero speed"; **0 "no wall"; 0 "indeterminate"**; 28 not reached. The stop rule (a story-path site still "no wall" or "indeterminate") does not apply; the 28 not-reached sites are separate risks, they do not stop the commit of `0x24` (D-E19-24).

### Hero entries used for the programs whose entry a flag gate fixes

| program | entry used | why (flag gate, from the data) |
|---|---|---|
| 10 mapevent19 `B[20]` (`@2462`) | `0x53` of 176 `@600` -> (10, 58) | gate G1654 on, G1655 off: only this chain is inside the window |
| 176 mapevent6 `B[7]` (`@599`) | `0x53` of 179 `@568` -> (5, 33) | gate G1654 on, G1655 off; G1654 is set by 179 `@565` just before |
| 179 mapevent2 `B[3]` (`@564`) | `0x53` of 176 `@533` or 44 `@985` -> (17, 7) | gate G1653 on, G1654 off; G1653 is set by 176 `@530` right before that `0x53` |
| 178 mapevent0 `B[1]` (`@123`-`@151`) | portal 7 of 176 -> (38, 25), needs G1655 | the only entry inside the zone; 176 `B[8]` `@617` makes its source tile unwalkable while G1655 is off; G1655 is set only by 135 `@953` |
| 135 mapevent13 `B[14]` (`@927`, `@942`) | `0x53` of 10 `@2463` -> (19, 51) | the church door portal is made unwalkable by 10 `B[1]` while G1655 is off |
| 10 mapevent7 `B[8]` (`@1047`) | contact with rec34 Septimus when T300 is raised | T300 is set only by his `C[18]` `@3210`, after T3 raised by his interaction `F[5]` |

Everything else on the story path uses (b) or (c) of the method; their starting points are in the table of every site. On map 10, `B[9]` (`@1212`) starts at the tile of its `0x3B` (37, 46).

### Story-path sites that are not "wall found"

| map | pc | class | cause |
|---|---|---|---|
| 10 | 2462 | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) |
| 10 | 3731 | not reached (0x0B blocked) | 0x0B @3673 blocked after 2 px of 72 (cell (36, 16)) |
| 10 | 3757 | not reached (flag never set) | T511 |
| 10 | 3793 | not reached (flag never set) | T512 |
| 10 | 3862 | not reached (flag never set) | T514 |
| 10 | 4973 | not reached (flag never set) | T666 |
| 10 | 5008 | not reached (flag never set) | T666 |
| 10 | 5061 | not reached (flag never set) | T666 |
| 10 | 5077 | not reached (flag never set) | T666 |
| 10 | 5150 | not reached (flag never set) | T666 |
| 10 | 5227 | not reached (0x0B blocked) | 0x0B @5172 blocked after 0 px of 28 (cell (40, 21)) |
| 10 | 5275 | not reached (flag never set) | T667 |
| 10 | 5305 | not reached (flag never set) | T667 |
| 10 | 5344 | not reached (flag never set) | T669 |
| 10 | 5379 | not reached (flag never set) | T669 |
| 10 | 5430 | not reached (flag never set) | T669 |
| 10 | 5452 | not reached (flag never set) | T669 |
| 10 | 5588 | not reached (flag never set) | T671 |
| 10 | 5626 | not reached (flag never set) | T671 |
| 10 | 5706 | not reached (flag never set) | T671 |
| 10 | 5777 | not reached (flag never set) | T674 |
| 10 | 5819 | not reached (flag never set) | T690 |
| 10 | 6400 | not reached (0x0B blocked) | 0x0B @6394 blocked after 22 px of 210 (cell (21, 53)) |
| 178 | 123 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) |
| 178 | 141 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) |
| 178 | 146 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) |
| 178 | 151 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) |
| 178 | 694 | not reached (flag never set) | T0 |

Reading (O-E19-18, O-E19-19): in the cell model the villager scene of map 10 (G218) stops at the `0x0B @5172` of Nestus (up 28 px, blocked at the first step by the wall that ended `@5163`): T666 is never set, B[14] never ends, the hero stays locked (`0x10 @1826`), so the villagers' following sites and the scene of Ronan (`@5819`, T690, set by B[15] after G457) are not reached; `@3731`, `@3757`, `@3793` and `@3862` follow the `0x0B @3673` of Septimus (blocked in the cell model, probably stopped earlier by a block in the original). `10 @2462` and `10 @6400` (Giles) need the scripted jump with `0x25` (a 16 px ledge at x = 517, not ported): the chain 179 -> 176 -> 10 -> 135 stops there, so the four sites of map 178 (G1655, set only by 135 `@953`) and `178 @694` (T0, raised by B[1]) are not reached either. Map 135 and map 331 (the mirror of map 10 after the id remap) show the same blocks. In the original these scenes finish (contacts between entities, or the jump): to be settled by E19.d2 (D-E19-26) and E19.h; the screen clip (O-E19-17) decides none of them.

## Not reached in the model (risks, whole corpus)

93 sites (28 on the story path). A site here is not a defect of `0x24`: the program stops earlier, on a blocked `0x0B` or on a flag no reached program raises. They stay risks until the cell model, contacts or the jump explain the stop.

| map | pc | class | cause | story path |
|---|---|---|---|---|
| 1 | 205 | not reached (flag never set) | T1 |  |
| 1 | 210 | not reached (flag never set) | T1 |  |
| 1 | 722 | not reached (0x0B blocked) | 0x0B @702 blocked after 0 px of 36 (cell (49, 53)) |  |
| 1 | 810 | not reached (0x0B blocked) | 0x0B @753 blocked after 0 px of 9 (cell (44, 54)) |  |
| 1 | 834 | not reached (0x0B blocked) | 0x0B @753 blocked after 0 px of 9 (cell (44, 54)) |  |
| 1 | 976 | not reached (flag never set) | T0 |  |
| 1 | 1163 | not reached (flag never set) | T2 |  |
| 2 | 682 | not reached (0x0B blocked) | 0x0B @671 blocked after 2 px of 12 (cell (46, 21)) |  |
| 2 | 762 | not reached (0x0B blocked) | 0x0B @705 blocked after 0 px of 16 (cell (48, 25)) |  |
| 2 | 771 | not reached (0x0B blocked) | 0x0B @705 blocked after 0 px of 16 (cell (48, 25)) |  |
| 10 | 2462 | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) | yes |
| 10 | 3731 | not reached (0x0B blocked) | 0x0B @3673 blocked after 2 px of 72 (cell (36, 16)) | yes |
| 10 | 3757 | not reached (flag never set) | T511 | yes |
| 10 | 3793 | not reached (flag never set) | T512 | yes |
| 10 | 3862 | not reached (flag never set) | T514 | yes |
| 10 | 4973 | not reached (flag never set) | T666 | yes |
| 10 | 5008 | not reached (flag never set) | T666 | yes |
| 10 | 5061 | not reached (flag never set) | T666 | yes |
| 10 | 5077 | not reached (flag never set) | T666 | yes |
| 10 | 5150 | not reached (flag never set) | T666 | yes |
| 10 | 5227 | not reached (0x0B blocked) | 0x0B @5172 blocked after 0 px of 28 (cell (40, 21)) | yes |
| 10 | 5275 | not reached (flag never set) | T667 | yes |
| 10 | 5305 | not reached (flag never set) | T667 | yes |
| 10 | 5344 | not reached (flag never set) | T669 | yes |
| 10 | 5379 | not reached (flag never set) | T669 | yes |
| 10 | 5430 | not reached (flag never set) | T669 | yes |
| 10 | 5452 | not reached (flag never set) | T669 | yes |
| 10 | 5588 | not reached (flag never set) | T671 | yes |
| 10 | 5626 | not reached (flag never set) | T671 | yes |
| 10 | 5706 | not reached (flag never set) | T671 | yes |
| 10 | 5777 | not reached (flag never set) | T674 | yes |
| 10 | 5819 | not reached (flag never set) | T690 | yes |
| 10 | 6400 | not reached (0x0B blocked) | 0x0B @6394 blocked after 22 px of 210 (cell (21, 53)) | yes |
| 15 | 698 | not reached (0x0B blocked) | 0x0B @665 blocked after 7 px of 40 (cell (24, 18)) |  |
| 15 | 776 | not reached (0x0B blocked) | 0x0B @722 blocked after 0 px of 208 (cell (18, 18)) |  |
| 15 | 869 | not reached (0x0B blocked) | 0x0B @832 blocked after 19 px of 24 (cell (22, 22)) |  |
| 15 | 880 | not reached (0x0B blocked) | 0x0B @832 blocked after 19 px of 24 (cell (22, 22)) |  |
| 135 | 927 | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) |  |
| 135 | 942 | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) |  |
| 135 | 1821 | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) |  |
| 135 | 1834 | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) |  |
| 135 | 1847 | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) |  |
| 135 | 1852 | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) |  |
| 136 | 855 | not reached (flag never set) | G1005 |  |
| 145 | 420 | not reached (0x0B blocked) | 0x0B @500 blocked after 0 px of 48 (cell (18, 16)) |  |
| 145 | 447 | not reached (0x0B blocked) | 0x0B @500 blocked after 0 px of 48 (cell (18, 16)) |  |
| 145 | 604 | not reached (flag never set) | T402 |  |
| 145 | 792 | not reached (flag never set) | T404 |  |
| 145 | 886 | not reached (flag never set) | T401 |  |
| 145 | 995 | not reached (flag never set) | T403 |  |
| 178 | 123 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | yes |
| 178 | 141 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | yes |
| 178 | 146 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | yes |
| 178 | 151 | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | yes |
| 178 | 694 | not reached (flag never set) | T0 | yes |
| 185 | 120 | not reached (flag never set) | T70 |  |
| 185 | 125 | not reached (flag never set) | T70 |  |
| 185 | 216 | not reached (flag never set) | T40 |  |
| 197 | 162 | not reached (0x0B blocked) | 0x0B @116 blocked after 1 px of 24 (cell (41, 14)) |  |
| 197 | 167 | not reached (0x0B blocked) | 0x0B @116 blocked after 1 px of 24 (cell (41, 14)) |  |
| 197 | 515 | not reached (flag never set) | T102 |  |
| 197 | 802 | not reached (flag never set) | T105 |  |
| 274 | 386 | not reached (flag never set) | G78 |  |
| 274 | 394 | not reached (flag never set) | G78 |  |
| 274 | 922 | not reached (flag never set) | T14 |  |
| 274 | 930 | not reached (flag never set) | T14 |  |
| 274 | 1223 | not reached (flag never set) | G77 |  |
| 274 | 1431 | not reached (flag never set) | T406 |  |
| 331 | 2462 | not reached (0x0B blocked) | 0x0B @2447 blocked after 121 px of 240 (cell (43, 43)) |  |
| 331 | 3731 | not reached (0x0B blocked) | 0x0B @3673 blocked after 2 px of 72 (cell (36, 16)) |  |
| 331 | 3757 | not reached (flag never set) | T511 |  |
| 331 | 3793 | not reached (flag never set) | T512 |  |
| 331 | 3862 | not reached (flag never set) | T514 |  |
| 331 | 4973 | not reached (flag never set) | T666 |  |
| 331 | 5008 | not reached (flag never set) | T666 |  |
| 331 | 5061 | not reached (flag never set) | T666 |  |
| 331 | 5077 | not reached (flag never set) | T666 |  |
| 331 | 5150 | not reached (flag never set) | T666 |  |
| 331 | 5227 | not reached (0x0B blocked) | 0x0B @5172 blocked after 0 px of 28 (cell (40, 21)) |  |
| 331 | 5275 | not reached (flag never set) | T667 |  |
| 331 | 5305 | not reached (flag never set) | T667 |  |
| 331 | 5344 | not reached (flag never set) | T669 |  |
| 331 | 5379 | not reached (flag never set) | T669 |  |
| 331 | 5430 | not reached (flag never set) | T669 |  |
| 331 | 5452 | not reached (flag never set) | T669 |  |
| 331 | 5588 | not reached (flag never set) | T671 |  |
| 331 | 5626 | not reached (flag never set) | T671 |  |
| 331 | 5706 | not reached (flag never set) | T671 |  |
| 331 | 5777 | not reached (flag never set) | T674 |  |
| 331 | 5819 | not reached (flag never set) | T690 |  |
| 331 | 6400 | not reached (0x0B blocked) | 0x0B @6394 blocked after 22 px of 210 (cell (21, 53)) |  |
| 393 | 228 | not reached (0x0B blocked) | 0x0B @190 blocked after 0 px of 16 (cell (44, 43)) |  |
| 393 | 289 | not reached (0x0B blocked) | 0x0B @246 blocked after 0 px of 16 (cell (43, 43)) |  |

## Off the story path: risky sites

Reachable sites off the story path: 336 (wall found 262, zero speed 1, no wall 4, indeterminate 4, not reached 65). The sites in a risky class (zero speed, no wall, indeterminate) are listed below with the hero control of their program; they are reported to the author, with no stop.

| map | pc | actor | anim | speed | dir | class | detail | hero control |
|---|---|---|---|---|---|---|---|---|
| 40 | 804 | rec14 ◆Mimming (rotation) Niv.1 | 1 | 128 | 8 | indeterminate | position not deducible | free |
| 40 | 836 | rec16 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  px | free |
| 40 | 868 | rec15 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  px | free |
| 40 | 900 | rec17 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  px | free |
| 62 | 1018 | Alundra (hero) | 55 | 256 | 0 | no wall | 0 px | free |
| 62 | 1036 | Alundra (hero) | 55 | 256 | 8 | indeterminate | position not deducible | free |
| 62 | 1054 | Alundra (hero) | 55 | 256 | 16 | indeterminate | position not deducible | free |
| 152 | 795 | rec8 Flamme | 0 | 0 | 0 | zero speed | - | free |
| 249 | 296 | rec2 Bloc transparent (1×1×2) | 3 | 208 | 8 | indeterminate | position not deducible | held |

Held control among them: 1 of 9. The "no wall" sites end in the original on the screen clip (O-E19-17), the "indeterminate" ones on an actor whose position the program does not fix, the "zero speed" one on a contact.

## Table of every site

| map | pc | slot | reachability | actor | anim | speed | dir | class | distance to the wall (px) or reason | hero control |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 205 | B | reachable | - |  |  |  | not reached (flag never set) | T1 | - |
| 1 | 210 | B | reachable | - |  |  |  | not reached (flag never set) | T1 | - |
| 1 | 722 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @702 blocked after 0 px of 36 (cell (49, 53)) | - |
| 1 | 810 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @753 blocked after 0 px of 9 (cell (44, 54)) | - |
| 1 | 834 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @753 blocked after 0 px of 9 (cell (44, 54)) | - |
| 1 | 976 | C | reachable | - |  |  |  | not reached (flag never set) | T0 | - |
| 1 | 1163 | C | reachable | - |  |  |  | not reached (flag never set) | T2 | - |
| 1 | 1286 | C | reachable | rec13 ◆Muruta (griffes) Niv.1 | 1 | 256 | 8 | wall found | 17-100 | free |
| 2 | 355 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 39-51 | held |
| 2 | 359 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 15-59 | held |
| 2 | 632 | C | reachable | rec4 ◆Muruta (griffes) Niv.1 | 1 | 256 | 16 | wall found | 81 | free |
| 2 | 641 | C | reachable | rec4 ◆Muruta (griffes) Niv.1 | 1 | 256 | 16 | wall found | 0 | free |
| 2 | 668 | C | reachable | rec5 ◆Muruta (griffes) Niv.1 | 1 | 256 | 16 | wall found | 27 | free |
| 2 | 682 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @671 blocked after 2 px of 12 (cell (46, 21)) | - |
| 2 | 702 | C | reachable | rec6 ◆Muruta (griffes) Niv.1 | 1 | 256 | 8 | wall found | 0 | held |
| 2 | 762 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @705 blocked after 0 px of 16 (cell (48, 25)) | - |
| 2 | 771 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @705 blocked after 0 px of 16 (cell (48, 25)) | - |
| 8 | 673 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 0-32 | held |
| 8 | 1333 | C | reachable | rec19 Meia | 1 | 128 | 16 | wall found | 65 | free |
| 10 | 1047 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 99-126 | held |
| 10 | 1212 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 505-517 | held |
| 10 | 2462 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) | - |
| 10 | 2629 | B | reachable | rec107 Meia | 9 | 208 | 16 | wall found | 79 | held |
| 10 | 3222 | C | reachable | rec34 Septimus (chercheur) | 1 | 128 | 0 | wall found | 82 | held |
| 10 | 3475 | C | reachable | rec5 Yuri (marchand d’objets) | 8 | 64 | 0 | wall found | 217 | held |
| 10 | 3578 | C | reachable | rec6 Lutas (gros bonhomme) | 8 | 64 | 0 | wall found | 153 | held |
| 10 | 3670 | C | reachable | rec39 Septimus (chercheur) | 9 | 208 | 16 | wall found | 482 | free |
| 10 | 3731 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @3673 blocked after 2 px of 72 (cell (36, 16)) | - |
| 10 | 3757 | C | reachable | - |  |  |  | not reached (flag never set) | T511 | - |
| 10 | 3793 | C | reachable | - |  |  |  | not reached (flag never set) | T512 | - |
| 10 | 3862 | C | reachable | - |  |  |  | not reached (flag never set) | T514 | - |
| 10 | 3967 | C | reachable | rec43 Meia | 1 | 128 | 0 | wall found | 129 | held |
| 10 | 4839 | C | reachable | rec68 Naomi (épouse courageuse) | 8 | 64 | 0 | wall found | 97 | held |
| 10 | 4920 | C | reachable | rec70 Yuri (marchand d’objets) | 8 | 64 | 0 | wall found | 89 | held |
| 10 | 4973 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 10 | 5008 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 10 | 5061 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 10 | 5077 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 10 | 5150 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 10 | 5163 | C | reachable | rec74 Nestus (frère cadet) | 1 | 128 | 16 | wall found | 193 | held |
| 10 | 5227 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @5172 blocked after 0 px of 28 (cell (40, 21)) | - |
| 10 | 5275 | C | reachable | - |  |  |  | not reached (flag never set) | T667 | - |
| 10 | 5305 | C | reachable | - |  |  |  | not reached (flag never set) | T667 | - |
| 10 | 5344 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 10 | 5379 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 10 | 5430 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 10 | 5452 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 10 | 5588 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 10 | 5626 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 10 | 5706 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 10 | 5777 | C | reachable | - |  |  |  | not reached (flag never set) | T674 | - |
| 10 | 5819 | C | reachable | - |  |  |  | not reached (flag never set) | T690 | - |
| 10 | 5905 | C | reachable | rec88 Meia | 9 | 208 | 16 | wall found | 17 | free |
| 10 | 6400 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @6394 blocked after 22 px of 210 (cell (21, 53)) | - |
| 15 | 596 | - | dormant | - | - | - | - | - | - | - |
| 15 | 648 | - | dormant | - | - | - | - | - | - | - |
| 15 | 698 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @665 blocked after 7 px of 40 (cell (24, 18)) | - |
| 15 | 776 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @722 blocked after 0 px of 208 (cell (18, 18)) | - |
| 15 | 869 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @832 blocked after 19 px of 24 (cell (22, 22)) | - |
| 15 | 880 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @832 blocked after 19 px of 24 (cell (22, 22)) | - |
| 35 | 336 | B | reachable | rec19 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | wall found | 242 | held |
| 35 | 1232 | C | reachable | rec14 Flamme | 2 | 128 | 0 | wall found | 64-80 | free |
| 38 | 216 | C | reachable | rec0 ◆Mimming (faux) Niv.1 | 9 | 256 | 24 | wall found | 2 | free |
| 38 | 229 | C | reachable | rec0 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | wall found | 4 | free |
| 38 | 334 | C | reachable | rec8 Plateforme flottante (grande) | 1 | 64 | 24 | wall found | 96 | free |
| 39 | 730 | C | reachable | rec10 Flamme | 3 | 176 | 0 | wall found | 80 | free |
| 40 | 804 | C | reachable | rec14 ◆Mimming (rotation) Niv.1 | 1 | 128 | 8 | indeterminate | position not deducible | free |
| 40 | 836 | C | reachable | rec16 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  | free |
| 40 | 868 | C | reachable | rec15 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  | free |
| 40 | 900 | C | reachable | rec17 ◆Mimming (faux) Niv.1 | 9 | 256 | 8 | no wall |  | free |
| 44 | 295 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 55-67 | held |
| 57 | 192 | C | reachable | rec2 Plateforme flottante fragile (grande) | 2 | 128 | 0 | wall found | 0-32 | free |
| 57 | 200 | C | reachable | rec2 Plateforme flottante fragile (grande) | 2 | 128 | 16 | wall found | 0-32 | free |
| 61 | 867 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 65 | free |
| 61 | 929 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 79-103 | held |
| 61 | 934 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 155-179 | held |
| 61 | 959 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 249 | held |
| 61 | 964 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 135 | held |
| 61 | 977 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 57 | held |
| 61 | 993 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 61 | 1011 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0-17 | held |
| 61 | 1016 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-180 | held |
| 61 | 1029 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 61 | 1076 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0-199 | held |
| 61 | 1081 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-204 | held |
| 61 | 1086 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 23-103 | held |
| 61 | 1113 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 23-103 | held |
| 61 | 1118 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-46 | held |
| 61 | 1123 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 61 | 1128 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 62 | 454 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 39 | free |
| 62 | 459 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 611 | free |
| 62 | 464 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 6 | free |
| 62 | 538 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0-150 | held |
| 62 | 543 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-178 | held |
| 62 | 606 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-370 | held |
| 62 | 611 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0-151 | held |
| 62 | 675 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 2-30 | held |
| 62 | 693 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 62 | 711 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 12-163 | held |
| 62 | 716 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 59-215 | held |
| 62 | 944 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 134 | free |
| 62 | 972 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | free |
| 62 | 977 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 11 | free |
| 62 | 993 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 36-298 | free |
| 62 | 998 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0-54 | free |
| 62 | 1003 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 36-204 | free |
| 62 | 1013 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | free |
| 62 | 1018 | B | reachable | Alundra (hero) | 55 | 256 | 0 | no wall | 0 | free |
| 62 | 1036 | B | reachable | Alundra (hero) | 55 | 256 | 8 | indeterminate | 0 | free |
| 62 | 1054 | B | reachable | Alundra (hero) | 55 | 256 | 16 | indeterminate | 0 | free |
| 63 | 566 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0-11 | held |
| 63 | 571 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0-7 | held |
| 63 | 576 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 63 | 641 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 98-106 | held |
| 63 | 646 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 546-550 | held |
| 63 | 651 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 9 | held |
| 63 | 669 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 407 | held |
| 63 | 674 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 63 | 782 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 102 | free |
| 63 | 787 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | free |
| 63 | 792 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | free |
| 63 | 797 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | free |
| 63 | 867 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 6 | free |
| 63 | 872 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | free |
| 63 | 877 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | free |
| 63 | 882 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | free |
| 63 | 887 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 7 | free |
| 63 | 892 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | free |
| 64 | 458 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 64 | 463 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 64 | 468 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 64 | 473 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 64 | 478 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 64 | 483 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 64 | 488 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 64 | 493 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 64 | 498 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 64 | 540 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 64 | 545 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 64 | 550 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 64 | 555 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 64 | 560 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 64 | 565 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 64 | 570 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 64 | 575 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 64 | 580 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 64 | 616 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 39 | free |
| 64 | 621 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 35 | free |
| 64 | 647 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 66 | 304 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 114-146 | held |
| 66 | 309 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0-394 | held |
| 66 | 314 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0-151 | held |
| 66 | 324 | B | reachable | Alundra (hero) | 27 | 128 | 24 | wall found | 0-353 | held |
| 66 | 425 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 66 | 476 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 135 | free |
| 66 | 494 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 59 | free |
| 66 | 499 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | free |
| 66 | 515 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0-59 | free |
| 66 | 571 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | free |
| 66 | 763 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | free |
| 68 | 757 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 87 | free |
| 68 | 762 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 58 | free |
| 68 | 806 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 263-275 | held |
| 72 | 279 | C | reachable | rec11 Tronc (transportable), petite boule de fer | 7 | 128 | 0 | wall found | 111-224 | free |
| 76 | 123 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 377-419 | held |
| 105 | 1002 | C | reachable | rec24 Flamme | 2 | 128 | 0 | wall found | 0-95 | held |
| 115 | 1314 | - | dormant | - | - | - | - | - | - | - |
| 115 | 1330 | - | dormant | - | - | - | - | - | - | - |
| 115 | 1346 | - | dormant | - | - | - | - | - | - | - |
| 115 | 1386 | - | dormant | - | - | - | - | - | - | - |
| 116 | 618 | - | dormant | - | - | - | - | - | - | - |
| 117 | 789 | C | reachable | rec4 Boule de fer (grande) | 3 | 176 | 0 | wall found | 190-216 | free |
| 119 | 90 | C | reachable | rec6 Plateforme flottante (petite) | 2 | 128 | 24 | wall found | 72 | free |
| 119 | 106 | C | reachable | rec7 Plateforme flottante (petite) | 2 | 128 | 24 | wall found | 72 | free |
| 121 | 170 | C | reachable | rec4 Boule de fer (grande) | 2 | 128 | 24 | wall found | 204-240 | free |
| 121 | 186 | C | reachable | rec5 Boule de fer (grande) | 2 | 128 | 8 | wall found | 204-240 | free |
| 122 | 774 | C | reachable | rec22 Flamme | 3 | 176 | 8 | wall found | 192 | free |
| 122 | 790 | C | reachable | rec23 Flamme | 3 | 176 | 8 | wall found | 192 | free |
| 122 | 806 | C | reachable | rec24 Flamme | 3 | 176 | 8 | wall found | 192 | free |
| 122 | 822 | C | reachable | rec25 Flamme | 3 | 176 | 8 | wall found | 192 | free |
| 128 | 563 | C | reachable | rec20 Boule de fer (petite) | 2 | 128 | 24 | wall found | 22-71 | free |
| 128 | 571 | C | reachable | rec22 Boule de fer (petite) | 3 | 176 | 8 | wall found | 21-70 | free |
| 135 | 229 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 1-417 | held |
| 135 | 234 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 0-217 | held |
| 135 | 452 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 92-120 | held |
| 135 | 465 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 433-457 | held |
| 135 | 475 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 0 | held |
| 135 | 880 | B | reachable | rec27 Ronan (prêtre) | 13 | 160 | 0 | wall found | 140 | held |
| 135 | 908 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 384 | held |
| 135 | 927 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) | - |
| 135 | 942 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @2456 blocked after 18 px of 194 (cell (21, 53)) | - |
| 135 | 1081 | C | reachable | rec0 Ronan (prêtre) | 1 | 128 | 16 | wall found | 0-113 | held |
| 135 | 1787 | C | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 1-205 | held |
| 135 | 1821 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) | - |
| 135 | 1834 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) | - |
| 135 | 1847 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) | - |
| 135 | 1852 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @1806 blocked after 26 px of 160 (cell (26, 26)) | - |
| 135 | 2365 | C | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 15-23 | held |
| 135 | 2372 | C | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 172-220 | held |
| 136 | 855 | B | reachable | - |  |  |  | not reached (flag never set) | G1005 | - |
| 139 | 343 | C | reachable | rec10 Flamme | 3 | 176 | 8 | wall found | 115-144 | free |
| 142 | 390 | C | reachable | rec13 Boule de fer (petite) | 4 | 256 | 0 | wall found | 76 | free |
| 142 | 401 | C | reachable | rec13 Boule de fer (petite) | 2 | 128 | 16 | wall found | 78 | free |
| 142 | 426 | C | reachable | rec14 Boule de fer (petite) | 4 | 256 | 0 | wall found | 76 | free |
| 142 | 437 | C | reachable | rec14 Boule de fer (petite) | 2 | 128 | 16 | wall found | 78 | free |
| 142 | 462 | C | reachable | rec15 Boule de fer (petite) | 4 | 256 | 0 | wall found | 76 | free |
| 142 | 473 | C | reachable | rec15 Boule de fer (petite) | 2 | 128 | 16 | wall found | 78 | free |
| 145 | 420 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @500 blocked after 0 px of 48 (cell (18, 16)) | - |
| 145 | 447 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @500 blocked after 0 px of 48 (cell (18, 16)) | - |
| 145 | 487 | C | reachable | rec1 Jess (armurier) | 1 | 128 | 24 | wall found | 278 | held |
| 145 | 493 | C | reachable | rec1 Jess (armurier) | 1 | 128 | 0 | wall found | 15 | held |
| 145 | 604 | C | reachable | - |  |  |  | not reached (flag never set) | T402 | - |
| 145 | 792 | C | reachable | - |  |  |  | not reached (flag never set) | T404 | - |
| 145 | 886 | C | reachable | - |  |  |  | not reached (flag never set) | T401 | - |
| 145 | 995 | C | reachable | - |  |  |  | not reached (flag never set) | T403 | - |
| 147 | 716 | C | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 1-117 | held |
| 147 | 1174 | C | reachable | rec4 Bergus (frère aîné) | 1 | 128 | 16 | wall found | 17 | free |
| 147 | 1196 | C | reachable | rec4 Bergus (frère aîné) | 1 | 128 | 0 | wall found | 145 | free |
| 148 | 746 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 0-109 | held |
| 148 | 753 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-744 | held |
| 148 | 1252 | C | reachable | rec26 Maître des clés | 9 | 208 | 16 | wall found | 641 | held |
| 149 | 758 | C | reachable | rec3 Plateforme flottante (grande) | 1 | 64 | 8 | wall found | 192 | free |
| 152 | 795 | C | reachable | rec8 Flamme | 0 | 0 | 0 | zero speed |  | free |
| 153 | 83 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 176-188 | held |
| 154 | 520 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 25-73 | held |
| 154 | 579 | B | reachable | rec6 Maître des clés | 1 | 128 | 24 | wall found | 62 | held |
| 154 | 723 | B | reachable | rec6 Maître des clés | 1 | 128 | 0 | wall found | 241 | held |
| 154 | 747 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 1-253 | held |
| 154 | 1082 | C | reachable | rec7 ◆Muruta (griffes) Niv.1 | 11 | 208 | 24 | wall found | 0 | held |
| 154 | 1412 | C | reachable | rec11 ◆Muruta (griffes) Niv.1 | 11 | 208 | 24 | wall found | 0 | held |
| 162 | 205 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 313 | held |
| 162 | 210 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 157 | held |
| 162 | 215 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0 | held |
| 162 | 707 | - | dormant | - | - | - | - | - | - | - |
| 162 | 715 | - | dormant | - | - | - | - | - | - | - |
| 162 | 723 | - | dormant | - | - | - | - | - | - | - |
| 163 | 183 | B | reachable | rec0 Jess (armurier) | 1 | 128 | 0 | wall found | 81 | held |
| 164 | 341 | C | reachable | rec1 Septimus (chercheur) | 9 | 208 | 0 | wall found | 0-66 | held |
| 164 | 346 | C | reachable | rec1 Septimus (chercheur) | 9 | 208 | 8 | wall found | 11-13 | held |
| 165 | 932 | C | reachable | rec3 Nestus (frère cadet) | 1 | 128 | 24 | wall found | 0-48 | held |
| 165 | 943 | C | reachable | rec3 Nestus (frère cadet) | 9 | 208 | 0 | wall found | 0-209 | held |
| 165 | 1395 | - | dormant | - | - | - | - | - | - | - |
| 165 | 1560 | - | dormant | - | - | - | - | - | - | - |
| 165 | 1898 | - | dormant | - | - | - | - | - | - | - |
| 165 | 2222 | - | dormant | - | - | - | - | - | - | - |
| 172 | 372 | - | dormant | - | - | - | - | - | - | - |
| 172 | 383 | - | dormant | - | - | - | - | - | - | - |
| 172 | 839 | - | dormant | - | - | - | - | - | - | - |
| 172 | 1004 | - | dormant | - | - | - | - | - | - | - |
| 172 | 1342 | - | dormant | - | - | - | - | - | - | - |
| 172 | 1666 | - | dormant | - | - | - | - | - | - | - |
| 176 | 526 | B | reachable | rec4 Giles (homme religieux) | 9 | 208 | 24 | wall found | 314 | free |
| 176 | 599 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 177 | held |
| 176 | 783 | C | reachable | rec5 Septimus (chercheur) | 9 | 208 | 16 | wall found | 194 | held |
| 176 | 877 | C | reachable | rec6 Giles (homme religieux) | 9 | 208 | 16 | wall found | 193 | held |
| 178 | 123 | B | reachable | - |  |  |  | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | - |
| 178 | 141 | B | reachable | - |  |  |  | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | - |
| 178 | 146 | B | reachable | - |  |  |  | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | - |
| 178 | 151 | B | reachable | - |  |  |  | not reached (flag never set) | G1655 (no reached setter: 135 @953: 0x0B @2456 blocked after 18 px of 194 (cell (21, 53))) | - |
| 178 | 251 | - | dormant | - | - | - | - | - | - | - |
| 178 | 259 | - | dormant | - | - | - | - | - | - | - |
| 178 | 264 | - | dormant | - | - | - | - | - | - | - |
| 178 | 269 | - | dormant | - | - | - | - | - | - | - |
| 178 | 428 | - | dormant | - | - | - | - | - | - | - |
| 178 | 523 | C | reachable | rec15 Septimus (chercheur) | 9 | 208 | 16 | wall found | 226 | free |
| 178 | 694 | C | reachable | - |  |  |  | not reached (flag never set) | T0 | - |
| 179 | 174 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 72 | held |
| 179 | 200 | B | reachable | rec0 Septimus (chercheur) | 1 | 128 | 0 | wall found | 29 | held |
| 179 | 247 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 32 | held |
| 179 | 255 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 66 | held |
| 179 | 564 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 192 | held |
| 179 | 876 | - | dormant | - | - | - | - | - | - | - |
| 179 | 887 | - | dormant | - | - | - | - | - | - | - |
| 179 | 1371 | - | dormant | - | - | - | - | - | - | - |
| 179 | 1536 | - | dormant | - | - | - | - | - | - | - |
| 179 | 1874 | - | dormant | - | - | - | - | - | - | - |
| 179 | 2198 | - | dormant | - | - | - | - | - | - | - |
| 185 | 95 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 2-26 | held |
| 185 | 120 | B | reachable | - |  |  |  | not reached (flag never set) | T70 | - |
| 185 | 125 | B | reachable | - |  |  |  | not reached (flag never set) | T70 | - |
| 185 | 216 | C | reachable | - |  |  |  | not reached (flag never set) | T40 | - |
| 185 | 458 | C | reachable | rec6 Septimus (chercheur) | 9 | 208 | 0 | wall found | 210 | free |
| 185 | 463 | C | reachable | rec6 Septimus (chercheur) | 9 | 208 | 24 | wall found | 86 | free |
| 186 | 832 | C | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 102-168 | held |
| 197 | 162 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @116 blocked after 1 px of 24 (cell (41, 14)) | - |
| 197 | 167 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @116 blocked after 1 px of 24 (cell (41, 14)) | - |
| 197 | 515 | C | reachable | - |  |  |  | not reached (flag never set) | T102 | - |
| 197 | 802 | C | reachable | - |  |  |  | not reached (flag never set) | T105 | - |
| 217 | 411 | C | reachable | rec1 Meia | 9 | 208 | 24 | wall found | 74 | held |
| 217 | 526 | C | reachable | rec5 Septimus (chercheur) | 9 | 208 | 8 | wall found | 73 | free |
| 249 | 296 | B | reachable | rec2 Bloc transparent (1×1×2) | 3 | 208 | 8 | indeterminate | position not deducible | held |
| 253 | 51 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 0-97 | held |
| 253 | 56 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-192 | held |
| 253 | 213 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 0 | wall found | 289 | held |
| 253 | 229 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 16 | wall found | 290 | held |
| 258 | 1456 | - | dormant | - | - | - | - | - | - | - |
| 260 | 323 | B | reachable | rec18 Jess (armurier) | 1 | 128 | 0 | wall found | 72 | held |
| 260 | 762 | C | reachable | rec6 Septimus (chercheur) | 1 | 128 | 16 | wall found | 98 | free |
| 260 | 804 | C | reachable | rec6 Septimus (chercheur) | 1 | 128 | 0 | wall found | 100 | free |
| 266 | 607 | C | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 111-151 | held |
| 266 | 729 | C | reachable | rec3 Gustav (ivrogne) | 9 | 208 | 16 | wall found | 0 | free |
| 272 | 93 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 134 | held |
| 272 | 142 | C | reachable | rec0 Kisha (petite sœur) | 9 | 208 | 16 | wall found | 143 | held |
| 272 | 180 | C | reachable | rec0 Kisha (petite sœur) | 9 | 208 | 0 | wall found | 144 | held |
| 272 | 193 | C | reachable | rec1 Septimus (chercheur) | 9 | 208 | 16 | wall found | 162 | held |
| 272 | 214 | C | reachable | rec1 Septimus (chercheur) | 9 | 208 | 0 | wall found | 163 | held |
| 274 | 92 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-64 | held |
| 274 | 98 | B | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 0-73 | held |
| 274 | 104 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-16 | held |
| 274 | 386 | B | reachable | - |  |  |  | not reached (flag never set) | G78 | - |
| 274 | 394 | B | reachable | - |  |  |  | not reached (flag never set) | G78 | - |
| 274 | 922 | C | reachable | - |  |  |  | not reached (flag never set) | T14 | - |
| 274 | 930 | C | reachable | - |  |  |  | not reached (flag never set) | T14 | - |
| 274 | 1223 | C | reachable | - |  |  |  | not reached (flag never set) | G77 | - |
| 274 | 1273 | C | reachable | rec9 Septimus (chercheur) | 9 | 208 | 24 | wall found | 72 | free |
| 274 | 1373 | C | reachable | rec10 Kisha (petite sœur) | 9 | 208 | 24 | wall found | 72 | free |
| 274 | 1431 | C | reachable | - |  |  |  | not reached (flag never set) | T406 | - |
| 282 | 648 | B | reachable | rec0 Yuri (marchand d’objets) | 1 | 128 | 24 | wall found | 50 | held |
| 292 | 360 | C | reachable | rec0 Meade (père des jumeaux) | 1 | 128 | 0 | wall found | 161 | held |
| 295 | 107 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-192 | held |
| 298 | 1356 | - | dormant | - | - | - | - | - | - | - |
| 302 | 252 | - | dormant | - | - | - | - | - | - | - |
| 309 | 686 | C | reachable | rec11 L’Homme en robe | 1 | 128 | 0 | wall found | 129 | free |
| 309 | 728 | C | reachable | rec12 L’Homme en robe | 1 | 128 | 16 | wall found | 129 | free |
| 309 | 754 | C | reachable | rec12 L’Homme en robe | 1 | 128 | 0 | wall found | 50 | free |
| 325 | 477 | C | reachable | rec6 Bloc transparent (1×1×2) | 3 | 208 | 8 | wall found | 15 | held |
| 328 | 204 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 328 | 214 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 328 | 219 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 328 | 224 | B | reachable | Alundra (hero) | 55 | 256 | 24 | wall found | 0 | held |
| 328 | 244 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 328 | 249 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 0 | held |
| 328 | 254 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 328 | 259 | B | reachable | Alundra (hero) | 55 | 256 | 16 | wall found | 0 | held |
| 328 | 264 | B | reachable | Alundra (hero) | 55 | 256 | 8 | wall found | 0 | held |
| 329 | 980 | B | reachable | Alundra (hero) | 55 | 256 | 0 | wall found | 171-191 | free |
| 331 | 1047 | B | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 0-56 | held |
| 331 | 1212 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 505-517 | held |
| 331 | 2462 | B | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @2447 blocked after 121 px of 240 (cell (43, 43)) | - |
| 331 | 2629 | B | reachable | rec107 Meia | 9 | 208 | 16 | wall found | 79 | held |
| 331 | 3222 | C | reachable | rec34 Septimus (chercheur) | 1 | 128 | 0 | wall found | 82 | held |
| 331 | 3475 | C | reachable | rec5 Yuri (marchand d’objets) | 8 | 64 | 0 | wall found | 217 | held |
| 331 | 3578 | C | reachable | rec6 Lutas (gros bonhomme) | 8 | 64 | 0 | wall found | 153 | held |
| 331 | 3670 | C | reachable | rec39 Septimus (chercheur) | 9 | 208 | 16 | wall found | 482 | free |
| 331 | 3731 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @3673 blocked after 2 px of 72 (cell (36, 16)) | - |
| 331 | 3757 | C | reachable | - |  |  |  | not reached (flag never set) | T511 | - |
| 331 | 3793 | C | reachable | - |  |  |  | not reached (flag never set) | T512 | - |
| 331 | 3862 | C | reachable | - |  |  |  | not reached (flag never set) | T514 | - |
| 331 | 3967 | C | reachable | rec43 Meia | 1 | 128 | 0 | wall found | 129 | held |
| 331 | 4839 | C | reachable | rec68 Naomi (épouse courageuse) | 8 | 64 | 0 | wall found | 97 | held |
| 331 | 4920 | C | reachable | rec70 Yuri (marchand d’objets) | 8 | 64 | 0 | wall found | 89 | held |
| 331 | 4973 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 331 | 5008 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 331 | 5061 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 331 | 5077 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 331 | 5150 | C | reachable | - |  |  |  | not reached (flag never set) | T666 | - |
| 331 | 5163 | C | reachable | rec74 Nestus (frère cadet) | 1 | 128 | 16 | wall found | 193 | held |
| 331 | 5227 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @5172 blocked after 0 px of 28 (cell (40, 21)) | - |
| 331 | 5275 | C | reachable | - |  |  |  | not reached (flag never set) | T667 | - |
| 331 | 5305 | C | reachable | - |  |  |  | not reached (flag never set) | T667 | - |
| 331 | 5344 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 331 | 5379 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 331 | 5430 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 331 | 5452 | C | reachable | - |  |  |  | not reached (flag never set) | T669 | - |
| 331 | 5588 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 331 | 5626 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 331 | 5706 | C | reachable | - |  |  |  | not reached (flag never set) | T671 | - |
| 331 | 5777 | C | reachable | - |  |  |  | not reached (flag never set) | T674 | - |
| 331 | 5819 | C | reachable | - |  |  |  | not reached (flag never set) | T690 | - |
| 331 | 5905 | C | reachable | rec88 Meia | 9 | 208 | 16 | wall found | 17 | free |
| 331 | 6400 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @6394 blocked after 22 px of 210 (cell (21, 53)) | - |
| 333 | 184 | B | reachable | rec0 Jess (armurier) | 1 | 128 | 0 | wall found | 81 | held |
| 334 | 74 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 65 | held |
| 334 | 115 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 217 | held |
| 334 | 268 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 16 | wall found | 33 | held |
| 334 | 292 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 24 | wall found | 266 | held |
| 334 | 347 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 8 | wall found | 256 | held |
| 334 | 352 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 0 | wall found | 45 | held |
| 334 | 394 | C | reachable | rec2 I35_Bombe explosive | 1 | 128 | 24 | wall found | 0-72 | held |
| 362 | 410 | C | reachable | rec2 Jaylen (mineur) | 1 | 128 | 0 | wall found | 0-271 | free |
| 362 | 461 | C | reachable | rec3 Lyman (mineur) | 1 | 128 | 8 | wall found | 0-271 | free |
| 362 | 508 | C | reachable | rec4 Olen (mineur) | 1 | 128 | 24 | wall found | 0-271 | free |
| 362 | 620 | C | reachable | rec4 Olen (mineur) | 1 | 128 | 24 | wall found | 0-271 | held |
| 363 | 106 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 1-37 | held |
| 363 | 138 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 1-25 | held |
| 363 | 155 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-48 | held |
| 363 | 160 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 61-313 | held |
| 363 | 215 | C | reachable | rec1 Zane (mineur) | 1 | 128 | 24 | wall found | 98-146 | held |
| 363 | 235 | C | reachable | rec1 Zane (mineur) | 1 | 128 | 24 | wall found | 0 | held |
| 364 | 973 | C | reachable | rec2 Kisha (petite sœur) | 9 | 208 | 0 | wall found | 0-386 | held |
| 364 | 1028 | C | reachable | rec2 Kisha (petite sœur) | 9 | 208 | 16 | wall found | 0-282 | held |
| 365 | 104 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 57-65 | held |
| 365 | 145 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 217 | held |
| 365 | 276 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 16 | wall found | 33 | held |
| 365 | 300 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 24 | wall found | 266 | held |
| 365 | 355 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 8 | wall found | 256 | held |
| 365 | 360 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 0 | wall found | 45 | held |
| 365 | 402 | C | reachable | rec2 I35_Bombe explosive | 1 | 128 | 24 | wall found | 0-168 | held |
| 366 | 259 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 49-145 | held |
| 366 | 264 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 8-24 | held |
| 366 | 485 | C | reachable | rec3 Beaumont (chef du village) | 1 | 128 | 16 | wall found | 0-480 | held |
| 366 | 788 | C | reachable | rec4 Yustel (vieille voyante) | 1 | 128 | 16 | wall found | 32-193 | held |
| 366 | 793 | C | reachable | rec4 Yustel (vieille voyante) | 1 | 128 | 24 | wall found | 0-91 | held |
| 366 | 802 | C | reachable | rec4 Yustel (vieille voyante) | 1 | 128 | 24 | wall found | 0 | held |
| 367 | 190 | C | reachable | rec1 Kline (somnambule) | 9 | 208 | 0 | wall found | 65 | free |
| 367 | 200 | C | reachable | rec2 Kline (somnambule) | 9 | 208 | 0 | wall found | 113 | free |
| 367 | 247 | C | reachable | rec2 Kline (somnambule) | 9 | 208 | 24 | wall found | 218 | free |
| 367 | 276 | C | reachable | rec3 Kline (somnambule) | 9 | 208 | 0 | wall found | 49 | free |
| 367 | 323 | C | reachable | rec3 Kline (somnambule) | 9 | 208 | 16 | wall found | 95 | free |
| 367 | 350 | C | reachable | rec4 Kline (somnambule) | 9 | 208 | 0 | wall found | 241 | free |
| 367 | 355 | C | reachable | rec4 Kline (somnambule) | 9 | 208 | 24 | wall found | 2 | free |
| 372 | 75 | B | reachable | Alundra (hero) | 1 | 208 | 24 | wall found | 1-265 | held |
| 372 | 82 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 8-72 | held |
| 372 | 222 | C | reachable | rec0 Jess (armurier) | 1 | 128 | 16 | wall found | 16-97 | held |
| 383 | 1231 | C | reachable | rec16 ◆Muruta (griffes) Niv.1 | 1 | 256 | 24 | wall found | 183 | held |
| 388 | 1143 | B | reachable | Alundra (hero) | 3 | 336 | 16 | wall found | 177 | held |
| 393 | 228 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @190 blocked after 0 px of 16 (cell (44, 43)) | - |
| 393 | 289 | C | reachable | - |  |  |  | not reached (0x0B blocked) | 0x0B @246 blocked after 0 px of 16 (cell (43, 43)) | - |
| 398 | 1913 | C | reachable | rec0 Lurvy | 1 | 160 | 24 | wall found | 6-9 | held |
| 398 | 1930 | C | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 0-18 | held |
| 398 | 1935 | C | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 24-36 | held |
| 398 | 2080 | C | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 92-96 | held |
| 440 | 1296 | C | reachable | Alundra (hero) | 1 | 208 | 0 | wall found | 12-400 | held |
| 440 | 1309 | C | reachable | Alundra (hero) | 1 | 208 | 8 | wall found | 38-90 | held |
| 440 | 1821 | C | reachable | rec15 Jeune Meia | 2 | 256 | 24 | wall found | 0 | held |
| 440 | 2138 | C | reachable | rec20 Jeune Meia | 4 | 128 | 16 | wall found | 86 | held |
| 440 | 2286 | C | reachable | rec23 Jeune Meia | 2 | 256 | 16 | wall found | 94 | held |
| 473 | 212 | B | reachable | Alundra (hero) | 1 | 208 | 16 | wall found | 0-76 | held |
