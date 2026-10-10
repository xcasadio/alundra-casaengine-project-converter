> Report returned by one agent of the read-only discovery for E20.d, the run (night of 2026-10-10), kept verbatim as evidence. The plan holds the reconciled facts.

# Things that change the plan

1. **The run oracle exists only in the session temp folder. It should be copied into the annex now.**
   - `run-oracle` holds the harness and 255 traces under `$S/run-oracle/` (`$S` is defined in §1).
   - I copied its harness and re-ran groups G1, G4, G5 and G6. All 192 output files are byte-identical to theirs.
   - The other files of `base/` are byte copies of `docs/plan-e20-annexe/e20-c2/oracle/`. The only edited file is `tools/compare.py` (its `ANNEX` path was made absolute).
   - Copy `base/` and `out/` to `docs/plan-e20-annexe/e20-d/oracle/` in a docs commit. Restore `compare.py` from the repository copy. Do not copy `work/` or `d1/`.
2. **Split E20.d in two.**
   - **E20.d1** is the keyboard and D-pad run. It follows the binary exactly and is pinned tick by tick on the oracle.
   - **E20.d2** is the D-E20-15 stick rule. It is a port addition and touches the pad code (`AlundraPadState`, `ComputePadState`, the two places that build the pad of a tick, and `MovePlayer`).
   - D2 needs a stick measurement that only the author can make. The oracle can only prove what the rule does once a direction nibble is chosen.
3. **The bounce comes 2 or 3 ticks after contact, depending on where the binary stops.**
   - FACT (traces `g5`, raw 16.16 values): on the W, N and S walls the binary stops exactly at the wall, and the bounce comes 2 ticks after contact.
   - On the E wall it stops 1 unit short (`21299199`), moves that last unit on the next tick, and the bounce comes 3 ticks after contact.
   - This is O-E19-42. The DLL stops exactly at the contact (D-E19-58), so on this alignment it can bounce one tick early.
   - So the acceptance pins W, N and S exactly, and gives E a documented ±1 tick.
4. **The cone check must read the facing of the tick, not the facing of the frame.**
   - FACT: the binary rereads the facing (`+0x98`) at every tick (`0x80038AD8`-`0x80038B40`). The DLL only writes `AnimationDirection` at the end of a frame (`SyncAnimation`, `AlundraFrameSyncPasses.cs:143`).
   - INFERENCE: with 2 ticks in a frame, a run that starts with a new facing misses the cone on its next tick and stops at once.
   - Fix: compute the facing with `TryResolveAnimationTarget` at the start of `MovePlayer`. It is exact with 1 or 2 ticks per frame, and exact for any tick count in state 0x03. This is the binary's own value, not a deviation.
5. **Deep water needs a stand-in guard, and run-oracle's "new" finding was already known.**
   - FACT (`0x80037504`-`0x80037558`): without boots of level 2 or more, a deep-water cell is a wall for the hero. With the starting gear (level 0), a run into deep water bounces (`g9 deep-noboots`, bounce at tick 87).
   - This is O-E19-22 in the E19 plan, already raised to the author as Q-N3. The DLL has neither this wall nor the swim branch.
   - A guard keeps the approved recipe line "sur l'eau profonde, le héros marche encore": on deep water (slope 4), Triangle does not start the run, and any run state becomes Idle.
6. **O-E20-12 stays out of E20.d.**
   - At 1 tick per frame the decisions match the binary: the existing tests AT8 and AT8n show it, and the oracle confirms the binary's own one-tick lag (G6).
   - Reading ground contact at every tick would need the binary's 3 px snap-to-ground rule (`0x80037848`, decoded) and the flush-on-entity case. It would also move the UH7, AT8, AT8n and AT8b pins and the high-ground traces.
   - So the ledge is pinned the way AT8 is: the ground column is compared one tick late on the tick the ground is lost. The gap with 2 ticks per frame stays documented, and O-E20-12 becomes a slice of its own.
7. **Turning during the wind-up restarts it.** A turn at tick 10 delays the start to tick 35; a turn at tick 20 delays it to 45. This is the first visible gameplay use of the DLL's restart on a facing change, so it must be pinned.
8. **The test rig cannot play the run yet, but the exported animations are right.**
   - `HeroAttackClock` (the attack test rig) has no 0x03, 0x04, 0x29, 0x2A or 0x3E, and cannot express an animation that ends by holding (0x3E does).
   - FACT: the exported `.anim2d` lengths are 24, 30, 32, 16 and 10 ticks for 0x29, 0x03, 0x04, 0x2A and 0x3E, in all four directions. They equal the binary's records. This resolves run-dll's UNKNOWN.
9. **Plan text to correct in §0.2.3.**
   - The wind-up is 25 ticks, and turning restarts it.
   - Dust in 0x29 does not come every 4 frames: there is none in the first loop, then one every 8 ticks, then every 4.
   - The exit of 0x3E in the air (value 1) is the normal path, not a corner case: the bounce lasts 11 ticks and the hop 16.
   - Deep water is a wall without boots (point 5).
10. **The 0x1C state is reached in play.** Running into a Beannoïde gives the hurt state 0x31 at ticks 61-64, then 0x1C at 65-75, then Idle at 76. 0x1C shares the 0x3E handler, so whichever of E20.c2b or E20.d lands first should wire both ids.
11. **Errors found in the three reports:**
    - run-binary §7: the R1 tick numbers are off by 20 (61 should read 41).
    - run-binary item 2: a one-line fix for O-E20-12 is not safe, because of the snap rule in point 6.
    - run-oracle item 4: "tick 90 would still run Sprint" is wrong at 1 tick per frame.
    - run-oracle item 5: at tick 85 the hero still moves 1 unit, so it is not the first tick without progress.

## 1. Method

- `$S` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20d`
- **Binary.** I wrote my own decoder, `$S/run-check/mdec.py` (capstone, file offset = 0x800 + addr − 0x80020000). With it I re-read:
  - the slope table 0x80022D1C and the state table 0x80022D3C;
  - the tail and the handlers of 0x29, 0x03, 0x2A, 0x3E and 0x1C;
  - the swim branch 0x8003260C and the swim table 0x80022E7C;
  - `UpdateAnimation` 0x80038AB4, the snap rule 0x80037848, the `IsOnGround` write 0x800380F8, and the deep-water wall 0x80037488;
  - the dash, cone, 8-way and facing tables.
- **Immediate scan.** `$S/run-check/immscan.py` lists every instruction whose 16-bit immediate is a given value (0x2CAC, 0x2CEC, 0x6FF4).
- **Oracle rerun.** I copied `run-oracle/base` to `$S/run-check/rerun/base` and re-ran G1, G4, G5 and G6. The outputs went to `$S/run-check/rerun/out/` and their md5 match theirs (192 of 192).
- **Traces.** I read the raw positions with `wallraw.py` and `rows.py`, and the exported animation lengths with `animlen.py`.
- **DLL.** I read HEAD `c8c99e0` through `git show`, snapshots in `$S/run-check/*.HEAD.cs`. HEAD moved since dd775ca (E20.c2a T1 and T2), so run-dll's line numbers are stale; I cite symbols instead. No uncommitted line was read, and nothing in the repository was written.

## 2. Adversarial check (C = CONFIRMED, R = REFUTED, U = UNVERIFIABLE)

| # | Claim | From | Verdict and evidence |
|---|---|---|---|
| 1 | State dispatch: 0x29→0x80031F54, 0x03→0x80031FF4, 0x2A→0x80032118, 0x04→0x80031E18, 0x3E and 0x1C→0x80032558 | all | **C**, table dump 0x80022D3C |
| 2 | Slope table: 4→0x8003260C, 6→0x80031D20, others→0x80031D68; slope ≥8 goes straight to END | binary | **C**, 0x80031CF4-0x80031D18 |
| 3 | Tail order: attack, air test, Cross edge (`VramOR&0x4000` ends the tick), Triangle held→0x29 with `+0xB0=0`, then Moving/Idle | all | **C**, 0x80031EA8-0x80031F40 |
| 4 | 0x29 order: TryUseItem, PlayerTryAction, PlayerTryAttack; TargetDirection written in a delay slot; air test; Triangle; `+0xB0`; dash table | all | **C**, 0x80031F54-0x80031FF0 |
| 5 | 0x03 order: PlayerTryAction; air; ForceAdjusted→effect 9, then 0x3E with direction +0x10; no nibble→0x2A, counter 1; Triangle or cone→0x2A, counter 0; `hold&0xE0`→0x04 | all | **C**, 0x80031FF4-0x80032114; arguments of 0x8003BDD8 are (0, 9, 0, +0x114, +0x118, +0x11C+0x100000) |
| 6 | 0x2A: TryUseItem, PlayerTryAction, air; counter 0 ends the tick; ForceAdjusted or no Triangle clears it; +1; below 11 a direction bit clears it; from 11 on, dash table (−1 keeps the counter) | all | **C**, 0x80032118-0x800321D0 |
| 7 | The counter 0x80126FF4 is touched only by MovePlayer, at 5 sites | binary, dll | **C**, immediate scan (5 sites, all in MovePlayer) |
| 8 | The dash table is read only at 0x80031FCC and 0x8003219C, the cone only at 0x800320A4 | binary, oracle | **C**, immediate scan |
| 9 | Table contents: dash, cone rows, 8-way, facing | all | **C**, dump; DLL `AnimationDirectionTable` and `DirectionByButtons` are identical |
| 10 | 0x3E and 0x1C: TryUseItem 0 gives direction +0x10; `+0xAC==1`; direction flipped; value 1 in the air, 0 on the ground | all | **C**, 0x80032558-0x800325A0 and 0x8003270C |
| 11 | Deep water (slope 4): clears 0x80126FFC and 0x80127110, calls 0x8002F884(2); every run state goes to 0x1D; 0x3E and 0x1C flip first; dive is `pressed&0xD0` | binary | **C**, 0x8003260C-0x80032758 |
| 12 | Deep water is a wall when boots level is below 2 | oracle | **C** as a fact (0x80037504, 0x8003753C, 0x80037554); **R** as "new" (it is O-E19-22, see plan-e19 line 12326, and Q-N3) |
| 13 | A facing change restarts the animation and leaves `+0xB0` untouched | oracle, binary | **C**, 0x80038B08-0x80038B6C; rerun G1: a turn at 10 starts at 35, a turn at 20 starts at 45 |
| 14 | Wind-up: 0x29 on ticks 1-25, start on 26; forces 64512…258048 | all | **C**, rerun G1 |
| 15 | Resume window is E+10 to E+16; E+17 means a fresh wind-up; momentum is kept | binary, oracle | **C**, rerun G4 (k=12: 161280→185472…258048) |
| 16 | run-binary §7: "Right released at 61, 0x2A on 61-76" | binary | **R**: its own `R1.txt` shows 41-56, then Idle at 57 (offset of 20); the slide of 47.25 px and the forces are **C** |
| 17 | Bounce latency "2 ticks" | binary | **Partly R**: 2 ticks on W/N/S and on run-binary's WALL; 3 ticks on run-oracle's E wall (raw x 21299199, 21299200, 21299200); O-E19-42 |
| 18 | run-oracle E wall: "85: no progress" | oracle | **R**: tick 85 moves +1 unit; its own summary gives 86 as the first tick without progress |
| 19 | 0x3E lasts 11 ticks, hop apex 18 px, landing at bump+16, then 0x01, then 0x2C or 0x2D | all | **C**, rerun G5 (W: 78-88, 89, 90-94, 0x29 at 95) |
| 20 | The ledge loses the ground at the end of tick N; 0x2D at N+1 | all | **C**, rerun G6 (on_ground 0 at 89, 0x2D at 90) |
| 21 | "A DLL reading contact late would still sprint at tick 90" | oracle | **R** at 1 tick per frame: AT8 and AT8n compare only the on_ground column one tick late, and the decisions are exact (`AlundraHeroAttackTests.cs` `Compare`); true only on the 2nd tick of a frame |
| 22 | O-E20-12 fixed by `IsOnGround = PosZ <= floor` in `RunHeroProbes` | binary | **R** as sufficient: the binary's snap rule at 0x80037848 (\|floor−`+0x1E0`−1\| < 0x30003 sets PosZ = floor+1) and `HeadPullFlushOnEntity` would be skipped, and the pins in point 6 would move |
| 23 | `IsOnGround = !(floor < PosZ)` is written in 0x80038064 | binary | **C**, 0x800380F8-0x8003810C |
| 24 | DLL: `IsOnGround` is read once per frame from the engine; slope, VRAM flags and floor are refreshed every tick | dll, binary | **C**, `PullRootPose`, `TickMotion`→`RunHeroProbes` |
| 25 | DLL `AnimationDirection` is written only in `SyncAnimation` | dll | **C**, `AlundraFrameSyncPasses.cs:143`; `TryResolveAnimationTarget` returns `table[row,col]` |
| 26 | `ComputePadState` merges stick and D-pad and drops `Value` | dll | **C**, `AlundraPlayerController.cs` (`ActionBits`, `ComputePadState`) |
| 27 | A pre-resolved direction nibble cannot express the facing-axis tie; resolve it in MovePlayer | dll | **C** (reasoning: only MovePlayer knows the facing; D8 had proposed changing the tick pad) |
| 28 | Stick geometry: at radius 0.75 every stick diagonal is a tie; ε measured as \|x\|−\|y\| | oracle | **C** by arithmetic (42-48° lies inside the band 41.6-48.4°); ε follows D8 (difference of axes, not an angle) |
| 29 | The montage table lacks the run states and has no Hold end | dll | **C**, `AlundraJumpTestSupport.cs` `HeroAttackClock` |
| 30 | Run animation lengths unknown | dll | **Resolved**: 24, 30, 32, 16, 10 ticks (`animlen.py`); 0x10 is 21, matching E20.b |
| 31 | "Oracle lengths 25/16/32/11 against the plan's 24/16/32/10" | oracle | **No conflict**: the oracle counts the tick on which a loop or hold end is processed, after MovePlayer |
| 32 | Only the SJ3 pin moves | dll | **C** by read (`AlundraHeroJumpStatesTests.cs:216-219`, Theory over 0x2C and 0x2D; other Triangle uses are in the inventory and controller tests); the full suite is the proof |
| 33 | 0x04 is already handled by `IsGroundAttackGroup` | dll | **C** |
| 34 | The bump needs a creator for effect 9, with no forces and no random draws on the caller side | dll | **C**, 0x80032028-0x80032068 |
| 35 | Map edge: the clamp is not ported, but the binary bumps there | binary, dll, oracle | **C**; already known as O-E19-17 (D-E19-25, slice later) |
| 36 | The climb gate wins over the bounce | all | **C**, code; G8 traces (not rerun): slope 6 at 51, ForceAdjusted at 53, 0x0E at 54 |
| 37 | Tackle: 32 ticks, −4032 per tick, sound 83; Square, Cross and Circle all start it; nothing in 0x2A | oracle | **C** by code; G7 not rerun; distances 93.52 px (0x04 only) and 94.5 px (with the Idle tick) are both right |
| 38 | Charge keeps counting in every run state; a release fires only from 0x29 or the tail | binary | **C** by code (the second PlayerTryAttack call); G7 (not rerun): counter 1, 16, 31, 60 |
| 39 | Map 392's scripted sprint is unaffected | dll | **C** for the DLL locked branch (returns early); export bytes not re-read |
| 40 | Second interpreter, mutation and D1 gate checks pass | oracle | **C** from read files: gate PASS (md5 of S-A and S-B equal), mutations PASS |
| 41 | G2, G3, G7, G8, G9, G10 | oracle | **U** by rerun (not re-run); every value I use from them agrees with the decoded handlers |

---

## 3. Draft plan section (French, ready to paste; renumber as needed)

````markdown
### 1.13 E20.d — La course ⏳ (proposée le 2026-10-10 ; découpée en E20.d1 et E20.d2)

Découverte en lecture seule du 2026-10-10 (`run-binary`, `run-dll`, `run-oracle`, contre-vérifiées par `run-check` : handlers
redécodés, G1, G4, G5, G6 de l'oracle rejoués à l'identique). Les traces sont dans `docs/plan-e20-annexe/e20-d/oracle/` (T0).

**But.** Le héros court comme dans le binaire :
- Triangle tenu, 25 ticks de préparation, puis la course à 258048 par tick ;
- l'arrêt glissé, la reprise, la charge `0x04`, le rebond contre un mur ;
- la chute d'un rebord et l'escalade, prioritaire sur le rebond ;
- au stick, l'arrondi de D-E20-15.

Le test « Triangle seul donne Idle » s'inverse.

**Découpage.**
- **E20.d1 — la course au clavier et à la croix.** Tout ce que fait le binaire, prouvé tick par tick par l'oracle.
- **E20.d2 — l'arrondi du stick (D-E20-15).** Un ajout du port, limité aux deux lectures de la table de départ. Il demande que le pad
  transporte l'échantillon du stick, et une mesure du stick par l'auteur. Après E20.d1.

**Prérequis.**
- Exigés : E20.a, E20.b, E20.c1a, E20.f1a (le relâchement chargé passe aussi par `0x29`), E20.f2, et E20.c2a terminée (mêmes
  branches de `MovePlayer`, épilogue).
- Non exigés :
  - E20.e : sans elle, le son 12, la poussière et l'atterrissage (son 11, effet 7) restent des bouchons et ne sont pas comparés ;
  - E20.c2b : sans elle, les colonnes de la blessure ne sont pas comparées.

**Faits porteurs.**
- [binaire] Table des états `0x80022D3C` :
  - `0x29` va à `0x80031F54`, `0x03` à `0x80031FF4`, `0x2A` à `0x80032118` ;
  - `0x04` va au groupe au sol `0x80031E18`, déjà porté ;
  - `0x3E` et `0x1C` vont à `0x80032558`.
- [binaire] La pente 4 (table `0x80022D1C`) mène à `0x8003260C`. Cette branche n'atteint ni la queue ni ces états.
- [binaire] Queue `0x80031EA8`, dans cet ordre :
  - `PlayerTryAttack` ;
  - en l'air, `0x2C` ou `0x2D` ;
  - front de Croix (sauf `VramOR & 0x4000`) ;
  - Triangle **tenu** (`hold & 0x10`) : `0x29` et `+0xB0 = 0` (`0x80031F14`-`0x80031F30`) ;
  - sinon Moving ou Idle.
- [binaire] `0x29`, dans cet ordre :
  - `TryUseItem` (0 finit le tick), `PlayerTryAction` (≠ 0 finit le tick), `PlayerTryAttack` (≠ 0 finit le tick) ;
  - `TargetDirection` = la direction à 8 voies, écrite même au tick en l'air ;
  - en l'air, `0x2D` ; Triangle relâché, Idle ; `+0xB0 == 0`, fin ;
  - `0x80022CAC[nibble]` (haut `0x10`, droite `0x18`, bas `0`, gauche `8`, −1 ailleurs) : −1 finit le tick, sinon `TargetDirection`
    prend la valeur et l'état passe à `0x03`.
- [binaire] `0x03`, dans cet ordre :
  - `PlayerTryAction`, puis en l'air `0x2D` ;
  - `ForceAdjusted` : effet 9, `0x8003BDD8(0, 9, 0, X, Y, Z + 0x100000)`, sans force ; puis `0x3E` et `TargetDirection += 0x10` ;
  - nibble nul : `0x2A`, compteur `0x80126FF4 = 1` ;
  - Triangle relâché, ou `dir` hors de la ligne `0x80022CEC + 12 × regard` (bas {1C,0,4}, haut {C,10,14}, gauche {4,8,C},
    droite {14,18,1C}) : `0x2A`, compteur 0 ;
  - `hold & 0xE0` : `0x04`.

  Pas de `TryUseItem`. `TargetDirection` n'est jamais écrite hors du rebond.
- [binaire] `0x2A`, dans cet ordre :
  - `TryUseItem`, `PlayerTryAction`, puis en l'air `0x2D` ;
  - compteur nul : fin ;
  - `ForceAdjusted` ou Triangle relâché : compteur 0 ;
  - sinon compteur + 1. Sous 11, un bit de direction le remet à 0. À 11 et plus, la table de départ : −1 finit le tick et garde le
    compteur, sinon reprise.

  Seul `MovePlayer` touche le compteur (cinq sites).
- [binaire] `0x3E` et `0x1C` :
  - `TryUseItem` à 0 donne `TargetDirection += 0x10` (`0x8003270C`) ;
  - sinon, si `+0xAC == 1` : `TargetDirection += 0x10`, puis Idle au sol et **1 en l'air**.
- [binaire] Regard : `UpdateAnimation` le recalcule à chaque tick (`0x80038AD8`). Un changement de regard relance l'animation à
  l'image 0 sans toucher `+0xB0` : tourner pendant la préparation la retarde.
- [binaire] La porte d'escalade de pente 6 (`0x80031D20`) précède l'aiguillage ; elle est déjà portée.
- [binaire] Eau profonde :
  - sans bottes de niveau ≥ 2, c'est un mur (`0x80037504`, O-E19-22) ;
  - avec, la pente 4 donne `0x1D` puis `0x0F`, la nage (E20.i).
- [données] Durées exportées, dans les quatre directions, égales aux fiches du binaire : `0x29` 24 ticks en boucle, `0x03` 30 en
  boucle, `0x04` 32 enchaîné sur 0, `0x2A` 16 enchaîné sur 0, `0x3E` 10, tenu.
- [DLL] Rien de la course n'est porté ; `0x04` est déjà dans `IsGroundAttackGroup`. Déjà génériques :
  - vitesses ; sons de changement d'animation (13, 14, 83, 28) ; impulsion Z de `0x3E` (1024) ;
  - invulnérabilité `0x40` ; charge comptée ; attaque Tackle rechargée au changement d'animation.

  `AnimationDirection` n'est posé qu'en fin d'image.

**Défauts proposés (écarts écrits dans le code et ici).**
- **Eau profonde** (recette de l'enveloppe : « le héros marche encore ») :
  - sur la pente 4, la queue ne teste pas Triangle ;
  - un état de course (`0x29`, `0x03`, `0x2A`, `0x04`, `0x3E`, `0x1C`) passe à Idle, à la place de `0x1D`, avec l'écriture de
    direction du binaire : retournée pour `0x3E` et `0x1C` (`0x80032658`), inchangée sinon ;
  - E20.i remplacera ce garde par la branche `0x8003260C` ;
  - le mur d'eau profonde sans bottes (O-E19-22) reste hors d'E20.d (Q-N3).
- **Regard du tick.** Le cône lit le regard que le binaire aurait posé au tick précédent (et en E20.d2, l'égalité du stick aussi) :
  - il se calcule une fois en tête de `MovePlayer`, avant toute écriture, par `TryResolveAnimationTarget` ;
  - il est exact à 1 et 2 ticks par image, et quel que soit le nombre de ticks dans `0x03` (direction cardinale, table idempotente) ;
  - c'est la valeur du binaire, pas un écart.
- **Compteur de reprise** : un champ du héros, à côté de `WeaponChargeTimer`. Il équivaut au global du binaire, puisque `0x03` le
  pose toujours avant d'entrer dans `0x2A`.
- **`0x1C`** : le gestionnaire est partagé avec `0x3E`. La première tranche qui arrive, d'E20.c2b ou d'E20.d, branche les deux
  numéros.
- **O-E20-12 hors d'E20.d.**
  - À 1 tick par image, les décisions sont exactes (AT8, AT8n). Le rebord s'épingle comme AT8 : la colonne `on_ground` est comparée
    au tick d'avant, au tick de la perte.
  - Une lecture par tick demande la règle de recollement au sol de 3 px (`0x80037848`). Elle déplacerait UH7, AT8, AT8n, AT8b et les
    traces highground : tranche à part.

**Hors périmètre.**
- Pas, poussière, son 12 et atterrissage (E20.e) ; blessure et entrée des dégâts (E20.c2b) ; nage et mur d'eau profonde (E20.i,
  O-E19-22) ; porter (E20.g, `PlayerTryAction` reste un bouchon) ; objets du Rond (E20.h).
- Rognage au bord de la carte (O-E19-17) ; glissement exact (O-E19-42).
- Rebond contre une entité solide : inconnu dans l'oracle, à observer en recette.

#### E20.d1 — Tâches

- ⏳ **T0 — Oracle dans l'annexe (commit docs).**
  - Copier `base/` et `out/` du dossier temporaire de `run-oracle` dans `docs/plan-e20-annexe/e20-d/oracle/`, sans `work/` ni `d1/`.
  - Rendre `tools/compare.py` identique à `e20-c2/oracle/tools/compare.py`.
  - Écrire un README court : commande de rejeu, groupes G1 à G10, colonnes, limites.
  - Preuve : `manifest.py` redonne `out/manifest.txt`.
- ⏳ **T1 — Tables et champs.**
  - Tables `0x80022CAC` et `0x80022CEC`, avec un test octet par octet contre l'exe.
  - Champ du compteur `0x80126FF4`.
  - Constantes `0x03`, `0x04`, `0x29`, `0x2A`, `0x3E`, `0x1C`.
  - Aide « regard du tick ».
- ⏳ **T2 — Entrée et états.**
  - Triangle dans `ApplyGroundOrAirTail`, après le bloc de la Croix, avec `AnimCompleteCounter = 0`.
  - Branches `0x29`, `0x03`, `0x2A`, `0x3E`/`0x1C`, dans l'ordre du binaire, y compris le second appel de `PlayerTryAttack` et la
    branche `TryUseItem == 0`.
  - Effet 9 du rebond par l'étage d'effets, à Z = `PosZ + 1 + 0x100000`.
  - Garde de la pente 4.
  - SJ3 inversé : « Triangle seul » donne `0x29`, `AnimCompleteCounter` 0, direction gardée.
- ⏳ **T3 — Moyens de test.**
  - `HeroAttackClock` gagne les cinq états (durées ci-dessus) et la fin Hold ; aucun test d'E20.b ne doit bouger.
  - Un lecteur du format de l'oracle d'E20.c2-0 : celui d'E20.c2a T5 s'il existe, sinon un neuf.
- ⏳ **T4 — Tests** (acceptation ci-dessous) **et arcs** au vrai préfab (carte 10 ou 14 ; l'échelle de la 389 en courant vers le
  haut).
- ⏳ **T5 — Plan.** Résultats, §0.2.3 corrigé, ligne d'O-E20-12 mise à jour.

#### E20.d1 — Acceptation

À 1 tick par image, champ par champ contre l'oracle : `anim_target`, `anim_cur`, `dir_target`, forces, positions relatives,
`pos_z_dll`, `on_ground` (règle d'AT8 au tick de la perte), `sprint_cnt`, `charge_cnt`, sons de changement d'animation, effet 9.

- **G1, départs.**
  - Quatre cardinales : `0x29` aux ticks 1-25, course au 26 ; ForceX 64512, 129024, 193536, 258048 aux ticks 26-29 (Y ±43008 à
    ±172032) ; son 13 au 26.
  - T seul, Gauche+Droite et les quatre diagonales : `0x29` sans fin, forces nulles ; direction 0x14, 0x1C, 0x04, 0x0C ; regard
    3, 3, 2, 2 depuis l'est.
  - Direction ajoutée au tick 10, 24, 25, 30 ou 40 : départ au 26, 26, 26, 30, 40.
  - Demi-tour au 10 : départ au 35 ; au 20 : départ au 45.
  - Diagonale 1-29 puis Droite : départ au 30 ; diagonale 1-9 puis Droite : départ au 26.
  - Marche puis Triangle : `0x29` au 20, force 0 dès ce tick, départ au 45.
  - Saut et Triangle : `0x29` au 22, départ au 47.
  - Triangle relâché au 10 : Idle au 10, Moving dès le 11.
- **G3, cône (52 cas).** La course continue pour sa direction, les deux diagonales voisines, Gauche+Droite et Haut+Bas. Sinon `0x2A`
  au 41, compteur 0 ; compteur 1 si toutes les directions sont relâchées.
- **G4, arrêt et reprise.**
  - Arrêt : `0x2A` aux ticks 41-56 ; ForceX 249984, puis −8064 par tick jusqu'à 129024 ; Idle au 57 (64512) ; 0 au 58.
  - Reprise au tick même de l'appui pour k = 10 à 16 (ticks 51 à 57), avec la force reprise : k = 12 donne 161280, puis 185472,
    209664, 233856, 258048, et le son 13.
  - k ≤ 9 ou k ≥ 17 : Idle au 57, `0x29` 58-82, course au 83.
  - Haut ou Gauche à k = 10-12 : direction 0x10 ou 0x08. Haut+Droite : pas de reprise, compteur gardé.
- **G5, rebond.**
  - Mur ouest : contact au 76, `ForceAdjusted` au 77.
    - `0x3E` aux ticks 78-88 : direction 08→18, ForceX +196608, ForceZ 262144, effet 9 à Z + 16 px, son 28, recul de 33 px,
      sommet à 18 px.
    - `0x01` au 89 (ForceX 18432), `0x2C` 90-94, `0x29` au 95.
  - Mur nord : `0x3E` 55-65. Mur sud : `0x3E` au 95.
  - Mur est : `0x3E` au 87 dans le binaire. Si la DLL rebondit au 86, c'est l'écart O-E19-42 / D-E19-58, écrit.
  - Course partie collée au mur : `0x3E` au 27. Pad relâché : `0x2D` 99-103, puis Idle.
  - Une marche montante de 16 ou 32 px fait rebondir comme un mur ; en marchant, Moving sans rebond. Carré pendant le rebond : rien.
- **G6, rebord.** `0x2D` au 90 (ForceX 129024), `0x2C` 91-100 (139776, puis 150528), atterrissage au 100, `0x29` au 101.
  - Chute de 16 px : `0x29` au 98. Chute de 64 px : au 106.
  - Triangle relâché en l'air : `0x01` au 101. Tout relâché : Idle.
  - Carré en l'air : `0x14` 93-100, Idle au 101, `0x29` au 102.
- **G7, charge `0x04`.**
  - Carré, Croix ou Rond pendant un tick au 40 : `0x04` 40-71, son 83, ForceX 254016 puis −4032 par tick (93,52 px) ; Idle au 72,
    `0x29` 73-97, course au 98.
  - Dans `0x2A` : rien. Dans la préparation : Carré donne `0x10` aux ticks 10-30, Idle au 31, course au 57 ; Croix et Rond : rien.
  - Épée sacrée, Carré tenu dès le 30 : compteur 1, 16, 31, 60 aux ticks 30, 45, 60, 90.
  - Beannoïde de la 14 (s = 44) : coup au 61 avec 3 étincelles, comparé jusqu'au tick 64 (blessure au 65, E20.c2b) ; balayage
    s = 30 à 62 aux ticks de l'oracle.
- **G8, échelle.** Pente 6 au 51, `ForceAdjusted` au 53, `0x0E` au 54, sans `0x3E`. Sans échelle : `0x3E` au 54.
- **G9.**
  - Eau peu profonde : ForceX 129024 sans bottes, 258048 avec.
  - Glace : 20160, 60480, 141120 aux ticks 30, 40, 60.
  - Eau profonde (garde, écart) : au tick où les quatre coins sont sur la pente 4, Idle au tick suivant, puis la marche.
- **2 ticks par image.** Un départ qui change le regard (diagonale puis Haut) ne sort pas du cône. Les épingles de rattrapage
  d'E20.c1a ne changent pas.
- **Épingles et preuves générales.**
  - Seule SJ3 change. Traces du héros et de l'intro inchangées ; sonde du vrai jeu inchangée.
  - Build Release puis Debug ; `Alundra.Tests` sans échec ; verifier frais.
- **Recette de l'auteur, au clavier et à la croix.**
  - Courir, s'arrêter, reprendre entre 10 et 16 images après l'arrêt, foncer, buter contre un mur (petit saut en arrière), sauter
    d'un rebord, grimper en courant vers le haut.
  - Sur l'eau profonde, le héros marche.
  - Fermer l'inventaire par Triangle en le gardant tenu lance la préparation, comme l'original.

#### E20.d2 — Tâches

- ⏳ **S1 — L'échantillon du stick.**
  - `AlundraPadState` gagne : les bits de direction du clavier et de la croix (D), les bits du stick (S), et x, y signés
    (`ButtonState.Value` des actions `Move*Stick`). Valeurs par défaut nulles : les initialiseurs existants ne changent pas de sens.
  - `ComputePadState` garde le OU dans `ButtonsHold`.
  - `AlundraTickPad` ne change pas. L'échantillon de l'image est recopié dans chaque pad de tick : au `FromTickPad` du bloc des
    entités, et dans le mode autonome.
- ⏳ **S2 — La règle**, aux deux lectures seulement : le départ dans `0x29`, la reprise dans `0x2A` à 11 et plus.
  - Elle ne joue que si D est vide et S est une diagonale.
  - \|x\| − \|y\| > ε : bit horizontal ; \|y\| − \|x\| > ε : bit vertical ; sinon l'axe du regard du tick. ε = 1/16 (D8).
  - Tout le reste garde le OU : la marche, le cône, la fenêtre courte, les sorties.
- ⏳ **S3 — La sonde.** Une variable d'environnement, comme `DebugIgnoreControlLock`, journalise x, y, D, S et le nibble choisi à
  chaque lecture. Pas de `Program.cs`.
- ⏳ **S4 — Tests.**
  - `ComputePadState` avec des valeurs analogiques.
  - Balayage de 0 à 359° aux rayons 1, 0,75 et 0,6, contre la règle écrite.
  - Les 48 départs et les 15 reprises de G10 : le nibble choisi par la règle égale celui de l'oracle.
  - G1 rejoué au clavier, inchangé.
- ⏳ **S5 — Mesure de l'auteur.** ε réglé ou gardé ; D-E20-15 complétée.

**Acceptation d'E20.d2.** S4 vert ; E20.d1 inchangée ; verifier frais. Recette au stick : une diagonale franche part sur son axe
dominant, une diagonale à 45° sur l'axe du regard, et la marche ne change pas.

#### Arrêts, budget, retour arrière (E20.d1 et E20.d2)

- **Arrêts.**
  - Un écart à l'oracle que ni O-E19-42 (mur est ou sud), ni la colonne `on_ground` (O-E20-12), ni E20.e ou E20.c2b non portées
    n'expliquent.
  - Un test qui change autre que SJ3 sans raison du binaire.
  - Un besoin de toucher au moteur ; un repère de l'intro ou un arc de la chaîne qui bouge.
  - En E20.d2, une modification de la marche.
  - Tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Budget**, par sous-tranche : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits de la sous-tranche.

**Questions pour l'auteur.**
- **Q-D1** (bloque seulement la clôture d'E20.d2) : la mesure du stick. Avec quelle manette ? Vous lancez le jeu avec la sonde de S3
  et renvoyez x, y aux huit directions. On garde ε = 1/16 si la mesure ne montre rien ?
- **Q-D2** (non bloquante, le défaut s'applique) : sur l'eau profonde, le garde fait marcher le héros à la place de la course.
  Préférez-vous porter d'abord le mur d'eau profonde sans bottes (O-E19-22, Q-N3), qui rendrait la course fidèle avec l'équipement
  de départ ?

**Corrections du §0.2.3** (à reporter) :
- la préparation dure 25 ticks, et un changement de regard la relance ;
- poussière de `0x29` : aucune pendant la première boucle, puis toutes les 8 images (`+0xB0` à 1 ou 2), puis toutes les 4 (E20.e) ;
- `0x3E` dure 11 ticks puis passe à `0x01` en l'air : c'est le chemin normal ;
- sans bottes de niveau ≥ 2, l'eau profonde est un mur (O-E19-22).

**Ligne O-E20-12** : « à 1 tick par image, les décisions sont exactes ; l'écart ne se voit qu'à 2 ticks et plus par image ; la
lecture par tick demande la règle de recollement au sol de 3 px `0x80037848` et la re-dérivation de UH7, AT8, AT8n, AT8b et des
traces highground ; tranche à part après E20.d ».
````

**Files I wrote** (all in `$S/run-check/`):
- `mdec.py`, `immscan.py`, `animlen.py`, `wallraw.py`, `rows.py`
- `*.HEAD.cs` (snapshots of the DLL at HEAD)
- `rerun/` (copy of the run-oracle harness, and its reproduced `out/`)

**Source for the annex copy:** `$S/run-oracle/base/` and `$S/run-oracle/out/`.
