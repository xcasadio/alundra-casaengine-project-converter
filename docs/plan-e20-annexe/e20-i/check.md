> Report returned by one agent of the read-only discovery for E20.i, swimming (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

# E20.i swim check: verdicts on the three discovery reports and a draft of section 1.14

## Things that change the plan

1. **D-E20-23's stand-in will never exist.** c2b waits for E14.a and is not done, so E20.i now comes before c2b.
   - FACT: nothing in the DLL at HEAD writes 0x3A or 0x3B as a hero state.
   - FACT: the hurt block is c2b's. It writes `+0x88 = (s2==4) ? 0x3A : 0x31` at 0x80031C74-0x80031C84.
   - INFERENCE: E20.i can port the two handlers for 0x3A and 0x3B (the swim table and the land exit) and test them by setting the state directly. c2b then only adds the entry, so the "blessure brève" of D-E20-23 never happens.
   - The envelope edge "E20.i : … E20.c2 pour 0x3A" is reversed: the water branch of c2b depends on E20.i.
2. **The row is too narrow.** Two parts are missing:
   - FACT: the swim table 0x80022E7C has 80 entries in six handler groups (re-dumped). Every state that is not a swim state goes to 0x1D, except 15 states that stay unchanged.
   - FACT: the land table sends 0x0F, 0x1D and 0x28 to 0x80031DD0, which writes Idle, or 0x2D in the air, then calls `TryUseItem`. It sends 0x3A and 0x3B to 0x80031DB4, which flips the direction first. The DLL leaves any state it does not know unchanged.
   - FACT: the warp keeps `TargetAnimationId` (`AlundraWarpDirector.cs:411`, `AlundraWorldProxy.cs:1941`).
   - Both parts belong in E20.i. Without the land exits, a hero who leaves the water or warps out stays in a swim pose on land.
3. **The water effects belong to E20.e, and no oracle measures them.**
   - FACT (decoded): pass 0x80038E84 creates effect 6 when `+0x18C != +0x190` and one of them is 4. It hides the shadow on slope 4 and creates effect 0x15 when `+0x1C & 7 == 0` and ForceX or ForceY is non-zero. It runs on every entity.
   - FACT: the DLL slot `AlundraWorldProxy.RunActiveEffectsPass` is empty and marked as E20.e's. The DLL never writes `Slope_190`, and it has no shadow renderer.
   - FACT: every oracle stubs 0x80038E84 (`tools/world.py` PASSES, `machine.py`).
   - FACT: the 0x28 ripple (0x8002FFE4) and the water landing (sound 21 plus effect 6) are already E20.e stubs (`IsWalkStepState` includes 0x28).
   - The author's recipe will show no splash and no wake.
4. **Currents (`+0x188`) have no owner.**
   - FACT: `TileAttributes` (`+0x188`) is declared but never computed in the DLL.
   - FACT: the branch at 0x80032768 (lock bit set: 0x1D on slope 4, pad ignored) and the drift are not ported.
   - FACT: on map 416, the 16 current cells are on the river cascade (x 14-15, y 35-42). The sea and map 14's pond have none.
   - This is a new open point. The recipe should avoid the river.
5. **The deep-water wall (O-E19-22): what E20.i needs from it.**
   - FACT: E20.i needs nothing from the wall to work. The four readers of the boots level `[0x80127000]` are all outside the swim branch (cross-referenced: 0x80031AD0, 0x800368BC, 0x800369E0, 0x800374FC).
   - Without the wall, a hero with no boots swims, starting with the 416 sea. Today he walks on it.
   - The recipe needs a save with boots 0x1B. No test save has one.
   - FACT: the wall's traces are already measured: `R9-deepwater-noboots`, `W1-*-L0/L1`, `W2-L0/L1-inside-R`, `W5-L0/L1-*`, `W10-shallow-to-deep-L0`.
6. **"Plongée" is a burst on the surface.** FACT (oracle):
   - It lasts 42 ticks with ForceX = 135168 the whole time. Z does not change.
   - The hero can still be hurt: `W7-hurt-dive-s45` goes from 0x28 to 0x3A at tick 81.
   - The word can stay, but no acceptance should expect an underwater state.
7. **The test montage needs two inputs the run oracle never used.** FACT: the swim specs carry `hero_anim` (the start state; 29 = 0x1D, or any s in W8) and the inventories `b1b` and `holy_b1b`. The montage must also keep Idle's accel byte of 1, as the run montage already corrects.
8. **The D1 gate cannot be replayed in place from the E20.d annex.** FACT, an inherited P4 from E20.d1 T0:
   - `e20-d/oracle/base/tools/compare.py` resolves `../../../../plan-e19-d2c-valeurs.md` to `docs/plan-e20-annexe/`, one level too high.
   - `rgate.py` reads `oracle/d1/out`, which is absent.
   - E20.i T0 must prove non-regression from a scratch copy, or by the manifest of G1-G10.
9. **Map 14's pond can trap the hero.** FACT (cells): the water is at height 0 and the shores at height 1. The hero can only fall in. The only same-level exit is the map's south edge (x 48-49, y 59). A 16 px bank is a wall for a swimmer (oracle `W4-bank16`). The 416 sea has 43 shore cells at the same level as the water, so the hero can walk out there.

## Method

- Re-decoded with capstone from the France binary: 0x8003260C-0x80032840, 0x80031B50-0x80031E20, the land tail 0x80031EA8, `TryUseItem` 0x8002ED64, the wall 0x80037488-0x80037590, the shadow pass 0x80038E84-0x80039180 with its slope table 0x800238DC, the T1/T2 tables of `CreatePlayerAnimationEffects`, the ripple 0x8002FFE4, and the death table 0x80022FBC.
- Dumped the tables 0x80022D1C, 0x80022D3C and 0x80022E7C.
- Cross-referenced every access to 0x80127000.
- Read the DLL and the tests at HEAD with `git show`.
- Copied swim-oracle's harness to my scratch folder and re-ran W1 to W10 (without `--cross`). All ten groups are identical byte for byte to swim-oracle's `out-swim/` (764 trace files).
- The harness `base/` equals HEAD's `e20-d/oracle/base` apart from CRLF line endings (42 files).

## Verdicts: swim-binary

| # | Claim | Verdict |
|---|---|---|
| 0.1 | 0x28 is a burst: handler `TryUseItem` only, 42 ticks then 0x1D, 176 speed, shift 0, direction frozen, no invulnerability | CONFIRMED. The handler is decoded. The oracle gives 0x28 at 20-61 and 0x1D at 62 (`W3-T-swim`), and a hurt during 0x28 (`W7-hurt-dive-s45`). The rename is a suggestion, not a decision. |
| 0.2 | Dive on `pressed & 0xD0` (edge); a held button does nothing; Circle is item use; Square on a talkable entity talks | CONFIRMED (0x800326D0 `lhu 0x80126F2A`; W3 T/X/S identical, O gives sound 3). The citation `R9-allwater-start-T` is imprecise: its tick-1 edge is used up by the Idle→0x1D entry. The held-button rule is shown by its ticks 2-40 and by `W3-T-held`. The talk case is decoded only. |
| 0.3 | Land exits through 0x80031DD0 and 0x80031DB4 | CONFIRMED (decoded; `W9-land-s*`) |
| 0.4 | Shadow-pass water effects; the oracle does not run the pass | CONFIRMED. Addition: the pass loops over all entities, and the slope-3 case creates the shallow-water effect. |
| 0.5 | 0x28 ripple: effect 6, speed −F/4 truncated toward zero, `+0x1C & 7 == 0` | CONFIRMED (decoded 0x8002FFE4-0x80030070; gate 0x8002FDC8 on non-zero force) |
| 0.6 | Order before the slope switch; `+0x188 & 0x80` forces 0x1D on slope 4 | CONFIRMED (decoded 0x80031BA4-0x80031CBC and 0x80032768-0x800327A0) |
| 0.7 | The hero moves during 0x3A and 0x3B | CONFIRMED (`W7-hurt-idle-L2`: x 232 → 200.29 by tick 15) |
| 0.8 / §8 | The branch never reads the boots; the wall is `bootsLevel < 2 && (cell & 0xE00) == 0x800` per corner | CONFIRMED (cross-reference and decode). The mask is 0x40, 0x41 with flag 8, and 0x1000 with flag 1. |
| 0.9, §6 | Hurt timeline; slope read at 0x80031B98 | CONFIRMED (W7, C1) |
| §3 | Swim table partition | CONFIRMED (re-dumped; W8 identical) |
| §4 | Rates and durations, step computed only on a change | CONFIRMED arithmetically against all re-run traces |
| §6 | Death: 0x3B unchanged until `+0xAC`, then 0x3C | CONFIRMED (0x800328CC). This is E18's. |
| §10 | Whether the DLL ports the shadow pass: UNKNOWN | Resolved: not ported (empty slot owned by E20.e) |
| §10 | 0x28 numbers not measured on the oracle | Resolved: measured (W3) |

## Verdicts: swim-dll

| # | Claim | Verdict |
|---|---|---|
| 1 | Exits needed; the warp keeps the animation | CONFIRMED (code at the cited lines) |
| 2 | The branch goes before the trio; remove the guard (483-494), `IsRunState` (568), the tail clause (1247) | CONFIRMED. `DeepWaterSlope` can stay as the branch's constant. |
| 3 | Wall not needed; ladder trap (0xC00 & 0x800 ≠ 0); touch points `WalkabilityMaskFor`, `BlockedCorners` | CONFIRMED (both symbols exist) |
| 4 | The repository oracle has no dive, exit or fall-in trace | CONFIRMED for the annex. The scratch W traces now cover all three. |
| 5 | No DLL path enters 0x3A; the stand-in does not exist | CONFIRMED |
| §1 | `Slope_18c` computed for the hero only, one tick late; `+1` dropped | CONFIRMED from the code. E20.d1's G9 test pins slope 4 at tick 89. |
| §1 | A hero who spawns in water has slope 0 on his first tick | PLAUSIBLE (no probe at adoption; C1 agrees). UNKNOWN whether the binary keeps `+0x18C` across a warp. |
| §3 | Exports exist; durations; sound replays on a self-chain | CONFIRMED. Files exist; a chain end goes through `TakeZImpulse` → `GiveImpulse`, so sound 22 replays at a same-id chain (`AlundraFrameSyncPasses.cs`). |
| §5 | Map 14 pond at x 40-47 | CONFIRMED, except the box ends at x 46 |
| §7 | Tests that move (lines 490, 503, 685, 703) | CONFIRMED |

## Verdicts: swim-oracle

| # | Claim | Verdict |
|---|---|---|
| 1 | The table covers all 80 states (W8) | CONFIRMED (re-run identical; matches my decode) |
| 2 | One 0x1D tick at entry; a press on that tick is lost | CONFIRMED (`W3-enter-T+0/+1/+2`) |
| 3 | The guard gives Idle at about 129024 | PLAUSIBLE (258048 >> 1 with Idle shift 1). Not measured. |
| 4-5 | Swim constants; dive rules; surfacing; taps | CONFIRMED (W2, W3 re-run) |
| 6 | Sound 22 on every self-chain; landing in water is E20.e | CONFIRMED (`W2-long-R`: 5, 47, 89, 131, 173; `W1-jump-s03` tick 24) |
| 7 | Charge zeroed in water; the trio is skipped | CONFIRMED (`W6-walk-square-held`: 60 at 83, 0 from 84) |
| 8-9 | Exits are geometric; walls; slide along a wall | CONFIRMED (W4, W5, W10) |
| 10 | Hurt timeline | CONFIRMED (W7) |
| Harness | Base untouched; D1 gate not replayable in place | CONFIRMED |
| Harness | `--cross` 0 mismatches; `sverify` ran against an older `sscen` | UNVERIFIABLE by me (not re-run with `--cross`). T0 must re-run `sverify`. |

## Draft section 1.14 (French, ready to paste)

```markdown
### 1.14 E20.i — La nage ⏳ (à relire)

Découverte en lecture seule du 2026-10-10 (`docs/plan-e20-annexe/e20-i/` : `swim-binary.md`, `swim-dll.md`, `swim-oracle.md`,
contre-vérifiées par `check.md` : tables et gestionnaires redécodés, W1 à W10 de l'oracle rejoués à l'identique).

**But.** Le héros nage comme dans le binaire sur la pente 4 : il flotte (`0x1D`), nage (`0x0F`), plonge au front de Triangle, Croix
ou Carré (`0x28`, un élan de 42 ticks en surface, touchable), et sort de l'eau par la terre ; les gestionnaires de `0x3A` et `0x3B`
sont prêts pour la blessure (E20.c2b). Le garde d'E20.d1 disparaît.

**Découpage.** Une tranche. Le mur d'eau profonde sans bottes (O-E19-22) et les courants (O-E20-15) restent dehors (Q-N3, Q-I1) ; s'ils
reviennent à E20, ils forment E20.i2, dont l'oracle est déjà mesuré (traces `*-L0`, `*-L1`, `R9-deepwater-noboots`).

**Prérequis.** E20.a, E20.c2a (épilogue, bottes), E20.d1 (garde à remplacer, montage de la course), E20.f1a (charge). Pas E20.c2b :
l'ordre s'inverse (E20.c2b, branche de l'eau, dépend d'E20.i) ; l'écart de D-E20-23 (« blessure brève ») n'existera jamais.

**Faits porteurs.**
- [binaire] `MovePlayer` lit la pente au tout début (`0x80031B98`) ; pente 4 → `0x8003260C` après le pilote `0x8003634C` et la
  lecture du pad, à la place de la porte d'échelle et du trio `0x80031D68`-`0x80031D78`. La branche met la charge (`0x80126FFC`) et le
  pointeur d'arme (`0x80127110`) à 0, appelle `UpdatePlayerCarriedEntity(2)`, puis aiguille par `0x80022E7C` (80 entrées) et finit par
  `0x80032914`.
- [binaire] Six groupes : `0x80032674` (tous les états non cités : `0x1D`, puis `TryUseItem`, sans écrire la direction) ;
  `0x80032658` (`0x1C`, `0x31`, `0x3E` : direction + 0x10, puis `0x1D`, puis `TryUseItem`) ; `0x80032680` (`0x28`, `0x3A` :
  `TryUseItem` seul) ; `0x80032690` (`0x0F`, `0x1D`) ; `0x800326FC` (`0x3B`) ; `0x80032758` (`0x0D 1A 1B 1E 27 37 38 3D 43 45 47 48
  4A 4C 4D` et ≥ `0x50` : annulation `0x800350C0`, état inchangé).
- [binaire] `0x80032690` : direction = celle du pad (écrite avant `TryUseItem`, même sans direction) ; `TryUseItem` à 0 finit le tick ;
  `CheckEntityInteraction` 2 → `0x1D`, 1 → fin ; front `& 0xD0` (Triangle 0x10, Croix 0x40, Carré 0x80) → `0x28` ; sinon quartet de
  direction non nul (même contradictoire) → `0x0F`, sinon `0x1D`.
- [binaire] `0x800326FC` : `TryUseItem` à 0 → direction + 0x10 ; sinon, `+0xAC == 1` → `0x1D` et direction + 0x10.
- [binaire] Côté terre (`0x80022D3C`) : `0x0F`, `0x1D`, `0x28` → `0x80031DD0` (Idle au sol, `0x2D` en l'air, puis `TryUseItem`) ;
  `0x3A`, `0x3B` → `0x80031DB4` (direction + 0x10 d'abord).
- [données, oracle] Fiches : `0x1D` 0/3, boucle 36 ; `0x0F` 144/4, son 22, 42 enchaîné sur lui-même ; `0x28` 176/0, son 21, 42 →
  `0x1D` ; `0x3A` 256/0x40, son 23, 4 → `0x3B` ; `0x3B` 0/0x44, 8, tenu. La physique générique de la DLL suffit (déduit, à prouver).
- [DLL] Le son d'une animation rejoue sur un enchaînement sur elle-même (`TakeZImpulse` → `GiveImpulse`) ; `ForceResetAnimationFlag`
  est `+0xAC` ; les bits du pad égalent ceux du binaire (`AlundraPlayerController.cs:27-30`).

**Défauts proposés (écarts écrits dans le code et ici).**
- Sans bottes de niveau ≥ 2, le héros nage (l'original : mur, O-E19-22, Q-N3).
- Pas d'éclaboussure (effet 6) à l'entrée et à la sortie, pas de sillage (effet 0x15), pas de ride de la plongée, pas d'atterrissage
  dans l'eau (son 21, effet 6) : passe `0x80038E84` et `CreatePlayerAnimationEffects`, E20.e (aucun oracle ne les mesure ; `Slope_190`
  non tenu par la DLL).
- Courants (`+0x188`, branche `0x80032768`, dérive) non portés (O-E20-15).
- `0x800350C0` et `UpdatePlayerCarriedEntity(2)` : bouchons nommés à leur place (E20.h0, E20.g).
- `0x3A` et `0x3B` ne sont atteints que par les tests jusqu'à E20.c2b.

**Hors périmètre.** L'entrée de la blessure et le clignotement (E20.c2b), la mort dans l'eau (`0x3C`, E18), le mur et les courants,
les effets d'eau (E20.e), les objets du Rond dans l'eau et le son 3 (E20.h), l'aura (E20.f1b), le rognage au bord de carte (O-E19-17).

#### Tâches

- ⏳ **T0 — Oracle dans l'annexe (commit docs).**
  - Copier, depuis `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-oracle/tree/docs/plan-e20-annexe/e20-d/oracle/`,
    `base/sscen.py`, `base/sverify.py`, `base/sindex.py` (fichiers neufs, à côté de `rworld.py`) et `out-swim/` (786 fichiers) dans
    `docs/plan-e20-annexe/e20-d/oracle/` ; aucun fichier existant modifié, sauf une section « Nage » du README.
  - Relancer `sverify.py` (il a tourné sur une version antérieure de `sscen.py`).
  - Preuves : les dix groupes relancés depuis l'annexe redonnent `out-swim/manifest-swim.txt` (785 lignes) ; G1 à G10 relancés
    après l'import de `sscen` redonnent `out/manifest.txt` (557). La porte D1 ne se rejoue pas en place (P4 hérité d'E20.d1 T0 :
    `compare.py` vise un niveau trop haut, `d1/out` absent) : la rejouer depuis une copie, sans modifier d'outil copié.
- ⏳ **T1 — Table et noms.** Constantes des cinq états ; la table `0x80022E7C` en six genres, avec un test entrée par entrée contre
  l'exe ; masque de plongée = `0xD0` ; bouchon nommé `0x800350C0`.
- ⏳ **T2 — La branche et les sorties.**
  - Dans `MovePlayer`, après le pilote et la direction, avant la porte d'échelle : pente 4 → charge 0, pointeur 0,
    `UpdatePlayerCarriedEntity(2)`, les six gestionnaires dans l'ordre du binaire, `EndOfMovePlayer`, retour.
  - Côté terre : `0x0F`, `0x1D`, `0x28` → Idle ou `0x2D` puis `TryUseItem` ; `0x3A`, `0x3B` → direction + 0x10 d'abord.
  - Retirer le garde d'E20.d1, `IsRunState` et la clause de la pente 4 de la queue ; documentation de la classe.
- ⏳ **T3 — Moyens de test.** Le montage gagne l'état de départ (`hero_anim`), les inventaires `b1b` et `holy_b1b`, les cinq états
  (durées, fins, fiches) avec l'Idle à l'accélération 1 ; le lecteur charge `out-swim/` ; un point d'accès qui pose `0x3A` et la
  direction avant le `MovePlayer` d'un tick donné (la sortie du bloc de blessure, sans l'entrée).
- ⏳ **T4 — Tests et arcs.** Acceptation ci-dessous ; un arc au vrai préfab sur l'étang de la 14 (bottes 0x1B) : tomber de la berge,
  `0x1D` puis `0x0F`, plonger, buter contre la berge ; le même sans bottes, épinglé comme écart (il nage).
- ⏳ **T5 — Sauvegarde de test** `swim-m14` : carte 14, bottes 0x1B, tuile proposée (40, 48, 1) sur la berge (à vérifier : marchable,
  hors zone d'événement).
- ⏳ **T6 — Plan.** Résultats, §0.2, lignes O-E19-22 et O-E20-15, D-E20-23 caduque, arête de dépendance d'E20.c2b, recette d'E20.d
  (« sur l'eau profonde, le héros nage »).

#### Acceptation

Champ par champ contre l'oracle, à 1 tick par image (`anim_target`, `anim_cur`, `dir_target`, forces, positions relatives,
`on_ground`, `b0`, `charge_cnt`, sons de changement d'animation). Les effets 6 et 0x15, les sons 21 et 6 d'atterrissage et le son 3 du
Rond ne sont pas comparés (E20.e, E20.h).
- `R9-deepwater-boots2` en entier : pente 4 au 89 (`0x03`, 258048) ; `0x1D` au 90 (225792, son 0) ; `0x0F` au 91 (218592, son 22),
  −7200 par tick jusqu'à 110592 au 106.
- `R9-allwater-start-R` et `-T-R` : `0x1D` au 1 ; `0x0F` au 2 (6912, son 22), + 6912 par tick, 110592 au 17 ; son 22 et `b0 = 1` au
  44 ; aucune préparation de course. `R9-allwater-start-T` : `0x1D` de 1 à 40, forces nulles, `b0 = 1` au 37.
- W1 (bottes 2 et 3) : marche, pente 4 au 20, `0x1D` au 21 (139776), `0x0F` au 22 (137952, son 22) ; saut `s03`, `0x2C` jusqu'au 23,
  `0x1D` au 24 (131712), `0x0F` au 25 (130392) ; chute de 32 px, `0x2C` 21-31, `0x1D` au 32, `0x0F` au 33.
- W2 : les huit directions (cardinales 110592 ou 73728, diagonales 78192 et 52128 ; rampe 6912, 4608, 4887, 3258) ; demi-tour au 41
  (96768) ; son 22 aux 5, 47, 89, 131 et 173 (`W2-long-R`).
- W3 : `W3-T-swim`, `0x0F` 5-19, `0x28` 20-61 (135168, son 21 au 20), `0x1D` au 62 (118272, `b0 = 1`), `0x0F` au 63 (117792) ;
  Croix et Carré identiques au Triangle ; Rond sans plongée ; maintien sans seconde plongée (`W3-T-held`, `0x1D` au 52) ; appuis
  toutes les 5 ticks, plongées 10-51 et 55-96 ; diagonale (95568, −63712) ; sans direction ou Gauche+Droite, direction gardée ;
  entrée (`W3-enter-T+0`, `0x29` au 12, Idle 13, Moving 14, `0x1D` 15, `0x0F` 16 ; `+1`, appui perdu ; `+2`, `0x28` 14-55).
- W4 : sortie à gauche, pente 0 au 103, Idle au 104 (−55296), Moving au 105 (−107520) et 106 (−159744) ; Triangle tenu, Idle 122,
  `0x29` 123-147, `0x03` au 148 ; plongée coupée, `0x28` 5-31, Idle au 32 (−67584) ; berge de 16 px, x 346,00, `ForceAdjusted` dès
  le 104, `0x0F` gardé.
- W5 : mur, `ForceAdjusted` dès le 78, x 709,00, `0x0F` à 110592 (mur est : tolérance d'O-E19-42, ± 1 unité, ± 1 tick) ; glissement
  en diagonale ; plongée contre le mur jusqu'à sa fin ; bords de carte : écart O-E19-17 écrit si la DLL diffère.
- W6 : charge 60 au 83, 0 dès le 84, Carré tenu ; jamais comptée dans l'eau ; recomptée à terre (3 au 90, 60 au 150).
- W7, la blessure posée par le point d'accès au tick de l'oracle : `W7-hurt-idle-L2`, `0x3A` 2-5 (−196608, son 23), `0x3B` 6-14
  (−184320 puis −12288 par tick), `0x1D` au 15 (−75264, direction 08 → 18), x 229,00, 217,19, 200,29 aux 2, 6, 15 ;
  `W7-hurt-leaving-x352`, Idle au 5 (−98304, direction 18) ; `W7-hurt-dive-s45`, `0x3A` au 81.
- W8 : les 240 traces (80 états × repos, Droite, front de Triangle), ticks 1 à 3. W9 côté terre : Idle au tick 1, direction
  retournée pour `0x3A` et `0x3B`. W10 : quartets contradictoires, canal, eau peu profonde vers profonde.
- 2 ticks par image : un front de plongée ne plonge qu'une fois.
- Tests qui changent, et eux seuls : `DeepWater_EveryStateOfTheRun_…` (six cas → `0x1D` ; direction retournée pour `0x3E` et `0x1C`
  seulement) ; `DeepWater_TheTailDoesNotStartTheRun_…` (Idle + Triangle + Droite sur la pente 4 → `0x1D` ; sec et peu profond
  inchangés) ; `G9_OnDeepWater_…` et `G9_ATriangleOnDeepWater_…` (traces entières) ; `AlundraTestSavesTests` (le préréglage).
  Traces du héros et de l'intro inchangées ; sonde du vrai jeu inchangée ; Release puis Debug sans échec ; verifier frais.
- Recette de l'auteur, avec `swim-m14` : tomber dans l'étang de la 14, nager dans les huit directions, flotter, plonger au
  Triangle, à la Croix et au Carré (pas au Rond), buter contre la berge ; sortir par une rive de même niveau sur la 416 si on y
  accède (pas par la cascade). Écarts attendus : ni éclaboussure ni sillage ; sans bottes, le héros nage.

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que ni O-E19-42, ni O-E19-17, ni E20.e, E20.h ou E20.f1b n'expliquent ; un test qui change hors de
  la liste ; une trace du héros ou de l'intro, ou la sonde, qui bouge ; un besoin de toucher au moteur ou de porter le mur, les
  courants, l'entrée de la blessure ou `0x80038E84` ; tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus. Mutations à attraper :
  masque `0xD0` → `0x50`, direction écrite après `TryUseItem`, sortie de terre `0x2D` au sol, retournement oublié pour `0x3B`, charge
  non remise à 0, trio non sauté, groupe `0x80032758` réécrit en `0x1D`.
- **Retour arrière** : `git revert` des commits d'E20.i.

**Questions pour l'auteur.**
- **Q-N3, complétée** (non bloquante pour E20.i) : la nage n'a besoin d'aucun mur ; sans lui, le héros sans bottes nage, dès la mer
  de la 416. Recommandé : le mur en E20.i2, juste après (une égalité sur `0xE00`, pas un bit de masque : les échelles valent `0xC00`).
- **Q-I1** (non bloquante) : les courants (`+0x188`, 16 cartes, dont la cascade de la 416) : E20.i2 avec le mur, ou plus tard ?
```

## Paths

- My scratch folder, with the re-run outputs identical to swim-oracle's: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-check/` (the re-run outputs are in `rerun/oracle/out-swim/`)
- Scripts there: `xdis.py` (disassembler), `xref.py` (cross-reference), `waterview.py` and `belts.py` (map cells), `summ.py` (trace summary)
- What the executor copies: `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-oracle/tree/docs/plan-e20-annexe/e20-d/oracle/` (`base/sscen.py`, `base/sverify.py`, `base/sindex.py`, `out-swim/`)
