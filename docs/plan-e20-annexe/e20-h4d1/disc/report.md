# Découverte E20.h4d1 : le feu 2 (objet 0x30), lecture seule

Je n'ai pas pu écrire `report.md` ni `check.md` : l'outil d'écriture a refusé. Ce message est donc le rapport complet, y compris le journal des vérifications (§7).

Les données et scripts sont écrits dans `H/` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4d1/`. Ce sont `keys/` (clés refaites, vecteurs d'E28, relevé du sprite 21, désassemblages), `work/` (scripts et copies de harnais), `SHA1SUMS_keys.txt` et `SHA1SUMS_scripts.txt`.

**Légende.** FAIT : vu dans le code, les données ou le binaire, avec fichier:ligne ou adresse. INFÉRENCE : déduit, avec de quoi. INCONNU : non établi.

**Sources.**
- `e20-h4` : `report.md` (R), `check.md` (C), la critique C faisant foi.
- `e20-h4b` : `critic/check.md` (CB), qui fait foi sur `bin/report.md`.
- États de la DLL : `e20-h3/state-f81304b` et `e20-h4b/state-c221a76`.
- Binaire : `ALUN_CD.EXE` France, SHA-1 `a705f8da...`, relu avec capstone.

**Base.** Dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`, HEAD `023b33ce`. Les commandes `git diff --stat f81304b HEAD -- Alundra Alundra.Tests alundra-project/Data` et `git log f81304b..HEAD -- Alundra Alundra.Tests` sont vides (FAIT). Le code lu est donc celui des états d'annexe. Rien n'a été écrit hors de `H/` : ni édition, ni git, ni build, ni test, ni export.

---

## 0. Résumé

1. Le feu 2 est un tome simple. Il n'a qu'un sprite (21) et qu'un gestionnaire (E28, code E ; aucun code A, C, D ou F). Il n'a ni requête de cibles, ni poussée, ni table de 256 entrées, ni accès 32 bits. Il ne tire que 8 nombres, tous dans E28 (FAIT). Son seul état propre est le bloc de tremblement (9 mots) et son tampon (9 mots).
2. Sept points que les annexes n'avaient pas, ou que le plan écrit autrement :
   - **(a)** La clé publiée de `trace_cast` ne garde que le nombre des 8 effets, pas leurs arguments. Le harnais d'e20-h4 ne modélise pas le pas de générateur de fin de trame. Les 8 positions de l'oracle ne sont donc pas comparables à la DLL. Je les ai refaites en `_pf` : `H/keys/fire2_cast_pf.json`.
   - **(b)** Le Z des effets est le Z de l'entité, sans `+ 0x100000`. Dans le repère de la DLL (ADR-0026), c'est `PosZ + 1`.
   - **(c)** La colonne meurt au tick S+121 (S = tick de naissance), un tick après l'indicateur de fin d'animation (S+120). Le pilote écrit encore le bloc à ce tick-là. La restauration a lieu à S+122.
   - **(d)** L'annulation avant la naissance restaure sans garde un tampon nul ou périmé. C'est confirmé sur le vrai code.
   - **(e)** Aucun tirage caché dans les effets : ni la création (`0x8003BDD8`) ni la mise à jour de la réserve (`0x8003C410`) n'appellent le générateur (balayage statique).
   - **(f)** La sauvegarde de test F9 `arsenal-b-m14` porte déjà le tome 48 (et 44, 46, 50, le bâton 7, MP 4/4) : `Alundra/SaveGames/TestSaves.cs:125`. La recette n'exige aucun ajout.
   - **(g)** Aucune des 30 cartes de la chaîne ne donne le tome 48. Le coffre est à la carte 448, entité 45, `Contents = 48`.
3. Atterrissage proposé :
   - un fichier neuf `Alundra/Ai/Items/FireTome2Ai.cs` (pilote, annulation, E28, `Register`) ;
   - une API de tampon sur `Alundra/Cameras/CameraSway.cs` ;
   - une ligne à `NativeSpriteRegistry.cs:41` ;
   - un champ « héros seulement » ;
   - les trois entrées de l'objet 0x30 dans les tables de h0a/h4a.
4. Deux tests existants bougent : les épingles du registre (`AlundraNativeSpriteRegistryTests.cs:55` et `AlundraWaveSwordLifeTests.cs:181-183`), de 7 à 8 clés si la tranche passe en premier.
5. Plan : T0 (annexe, rejeux, clés), T1 (tests puis code, phase A inerte puis phase B câblage), T2 (ADR, plan, verifiers, 14+ mutants).
6. Deux questions à l'auteur (§5) et une note de recette.

---

## 1. Faits du binaire qui portent une décision

Colonne « Re-vérifié » : N = non refait (la critique suffit), O = refait ici.

### 1.1 Le pilote `0x80035C64` (phase `[0x80127160]`, compteur `[0x8012715C]`)

| # | Fait | Étiq. | Source | Re-vérifié |
|---|---|---|---|---|
| P1 | Rend 0 « en cours » ou non nul « fini ». Phase 0 : appelle la garde `0x800351B8` (`0x80035CA8`). Si la garde rend ≠ 0, le pilote rend 1 sans rien faire d'autre. La restauration des 9 mots est faite par l'annulation que la garde a appelée. | FAIT | R §3 ; listing `e20-h4/work/lst/drv_80035C64.txt` ; CB §5 | O (listing relu ; `c6_fire2` 4 000 exécutions, 0 écart) |
| P2 | Phase 0, animation héros `+0x88` ≠ 0x33 : rend 0 (attend). Sinon `SpawnWarpEntity(héros, 0, 0x15, X, Y, Z, direction 0)` avec X, Y, Z = `+0x114/+0x118/+0x11C` du héros (`0x80035CD0-0x80035CEC`). | FAIT | R §3 ; listing | O |
| P3 | Le pointeur rendu est rangé en `[0x80127124]` avant le test de nullité (`0x80035CFC`). Nul : rend 1, rien d'autre (ni tampon, ni phase, ni identité). 4 vecteurs sur 60 (ids 13, 40, 51, 52). | FAIT | CB §5 ; `vec_fire2` | O |
| P4 | Non nul : copie des 9 mots `0x800E4338..0x800E4358` vers `0x80127138..` (`0x80035D00-0x80035D4C`), phase + 1, identité `[0x80127128] = [ptr+4]` (`0x80035D64-0x80035D6C`). Enchaîne dans la phase 1 du même tick (`0x80035D70`). | FAIT | R §3, CB §5 | O |
| P5 | Phase 1 : garde. Si elle rend ≠ 0, le pilote restaure encore les 9 mots (deuxième restauration, idempotente), rend 1, et la phase n'avance pas (`0x80035D80-0x80035DC4`). | FAIT | CB §5 | O |
| P6 | Phase 1, identité inchangée : `Flag = 1` (`0x800E4338`), `c = [0x8012715C]`, `LimitY = SpeedY = c >> 4` (`+8`, `+0x10`), `LimitX = SpeedX = c >> 6` (`+4`, `+0xC`), rend 0 (`0x80035DF0-0x80035E1C`). OffsetX/Y et ReachX/Y ne sont pas touchés. | FAIT | R §3, CB §5 | O |
| P7 | Phase 1, identité changée : restauration des 9 mots, `hero.+0x88 = 0x34` (`0x80127DB8`), phase + 1, rend 0 (`0x80035E20-0x80035E84`). | FAIT | CB §5 | O |
| P8 | Phase 2 : rend 1 si `hero.+0x88 != 0x34`, sinon 0 (`0x80035E88-0x80035E9C`). Toute autre phase (négative ou ≥ 3) rend 0 sans rien faire ; 10 vecteurs sur 60 sont en phase 3. | FAIT | listing `0x80035C7C-0x80035CA0` ; `vec_fire2` | O |
| P9 | Le compteur lu est celui d'avant l'incrément du cadre (`0x8003634C` incrémente après le pilote). Premier appel au tick T0+1 avec `c = 0` ; la naissance a lieu à `c = 32` (T0 = 1, naissance au tick 34). | FAIT | R §0 pt 9 ; C ; clé `fire2_cast_pf` | O |
| P10 | Le pilote n'est appelé ni quand `hero.+0x20 != 0` (`0x80031BA4`) ni sous `[0x800DC4B8] & 0x48`. La garde n'est pas appelée en phase 2. | FAIT | C §3 pt 5 ; R §1 | N |
| P11 | Aucun tirage, aucun son, aucune lecture du pad dans le pilote. | FAIT | clé (un seul tick à tirages) | O |

### 1.2 E28 `0x8007FB58` (slot E = 4, sprite 21)

| # | Fait | Étiq. | Source | Re-vérifié |
|---|---|---|---|---|
| E1 | 8 itérations `k = 0..7`. Un pas du générateur (`s = s × 0x7D2B89DD + 0xE06A02E7`, 32 bits, rangé avant l'effet), tirage `r = (s × 0x1801) >> 32` (non signé, `r` dans [0, 6144]), `r' = r + 0x320` (800 à 6944). | FAIT | R §4 ; listing `0x8007FB90-0x8007FBF0` | O (capstone ; 3 000 vecteurs e20-h4 graine 777, 0 écart, 67/67 ; 202 vecteurs `vec_e28`) |
| E2 | Effet : `CreateEffectEntity(0, 19, 2, X + TabX[4k]·r', Y + TabY[4k]·r', Z)`. `TabX = 0x80023654` et `TabY = 0x80023694` sont des demi-mots, avec un pas de 8 octets par itération. TabX[4k] vaut 0, −543, −768, −543, 0, 543, 768, 543 ; TabY[4k] vaut 512, 362, 0, −362, −512, −362, 0, 362. Produits sur 32 bits. Z = `+0x11C` de l'entité, sans décalage. Effet global, animation 2 (20 images de 2 ticks, fin « Destroy » : 40 ticks), aucune force. | FAIT | R §4 ; listing `0x8007FBD0-0x8007FC20` ; `effects-global.json` effet 19 anim 2 | O |
| E3 | Ces tables sont déjà dans la DLL : `Alundra/Entities/AnimationTables.cs:71` (`OffsetXList`) et `:82` (`OffsetYList`), aux indices 0, 4, ..., 28 (valeurs relues à la main). `NativeEffects.RingOffsetX/Y` (`NativeEffects.cs:21-23`) en est une copie privée. | FAIT | DLL | O |
| E4 | Puis `DestroyEntity(self, -1)` (`0x8007FC34-0x8007FC3C`). Le sprite 21 a `BreakEffect = 0` : aucun effet de bris. | FAIT | listing ; `sprite-records.json` `alundra_21` | O |
| E5 | Le tirage a lieu même si la création d'effet échoue (réserve pleine) : aucun test de `v0` après le `jal`. | FAIT | listing | O |
| E6 | Ni `0x8003BDD8` (et ses callees `0x8003B9C4`, `0x8003BB14`) ni la mise à jour de la réserve `0x8003C410` (13 fonctions) n'appellent le générateur. C'est un balayage des `jal` (`H/work/rng_scan.py`). Les sauts indirects (tables de saut des modes de position) ne sont pas suivis : INFÉRENCE, appuyée par les tests dorés G3 de la DLL. | FAIT + INFÉRENCE | `rng_scan.py` ; `docs/plan-e19-opcodes.md:7602` | O (nouveau) |
| E7 | Le harnais d'e20-h4 ne modélise pas le pas de fin de trame. Il est en `0x8002BD0C-0x8002BD34` (`lw 0x80098708`, `× 0x7D2B89DD`, `+ 0xE06A02E7`, `sw`), dans `0x8002BAEC`. Les passes du harnais sont celles de `0x8003B388` seules (`world.py:PASSES`), et la passe `0x80038E84` et la création d'effet sont des bouchons. | FAIT | `world.py` ; capstone | O |

### 1.3 L'annulation `0x80034E08` (table `0x800233EC`, entrée de 0x30 en `0x80023430`)

| # | Fait | Étiq. | Source | Re-vérifié |
|---|---|---|---|---|
| A1 | Restaure les 9 mots `0x80127138 → 0x800E4338` sans garde (`0x80034E08-0x80034E50`). Avant toute sauvegarde le tampon est nul (BSS) ou périmé. Sur la clé : un bloc `[1,5,6,7,8,3,-2,1,1]` annulé au tick 20 devient 9 zéros ; avec un tampon `9×9` il devient `9×9`. | FAIT | C §3 pt 4 ; CB §5 ; `H/keys/fire2_hurt20_*` | O |
| A2 | Pour les emplacements `1..[0x80134360]` inclus, toute entité de type `+0x68 == 0x15` au statut 2 passe au statut 3 avec `+0x20 = 0` (`0x80034E54-0x80034EB8`). Les autres statuts et types sont laissés. | FAIT | CB §3.2 ; listing | O |
| A3 | Le type `+0x68` d'une entité de la page de carte vaut `index + 0x100` (`0x80039FC0-0x80039FC4`). L'annulation ne touche donc pas une entité de la page de carte d'indice 21 (19 cartes en ont un, `map-sprite-indices.json`). La DLL doit comparer `SpriteType` et non `SpriteTableIndex` (`EntityScriptProxy.cs:141` et `:127`). | FAIT | listing ; `EntitySpawnFactory.cs:541` | O |
| A4 | Sites d'appel : branche verrouillée `0x80031BFC`, défaut des états sans gestionnaire `0x80032758`, case de sortie `0x80032770`, mort `0x800327B4`, garde du pilote (phases 0 et 1). Le statut 3 posé déclenche E28 au même tick (la passe des entités suit `MovePlayer`) : 8 effets et statut 4 au tick de l'annulation. | FAIT | R §1 ; `interrupt_check.txt` ; clé `fire2_hurt60` | O |

### 1.4 Le sprite 21 (page globale)

| # | Fait | Étiq. | Source | Re-vérifié |
|---|---|---|---|---|
| S1 | En-tête : `MoreFlags 68` (0x44 = HitsClassA + DeactivateOnAnimationEnd), `CanPickup 32` (donne 0x2000 NoObstacleSlide), `FlagsPortraitShadowType 0`. Drapeaux de l'entité `0x2044` (`EntitySpawnFactory.cs:740`). `ProgramLoad/Tick/Touch/Interact = 0`, `ProgramDeactivate = 28`. Décalage (−10, −7, 0), taille 21×15×16, `BreakEffect 0`, `Contents 0`. E28 n'est porté que par ce sprite, dans tout l'export. | FAIT | `alundra-project/Data/sprite-records.json` `BankKey alundra_21` (id `3c31ec67-...`) ; extraction | O |
| S2 | Un seul jeu d'animations (`Speed 0`, `Acceleration 192` = octet d'animation `0xC0`). Le bit `0x80` le met hors des listes de collision et de support, le bit `0x40` le rend intouchable. Animation 0 identique dans les 4 directions, 40 images : 37 de 3 ticks, une de 4, une de 5, une terminale de délai 0 = 120 ticks. Le fichier d'animation du prefab dit `Once`, avec 40 clés de sprite à 0, 3, ..., 108, 111, 115, 120 ticks. | FAIT | `H/keys/sprite21_dump.txt` ; `.anim2d` | O |
| S3 | La boîte d'attaque grossit. 19 clés de collision tous les 3 ticks (0,06 s), de 30×18×127 à 178×158×127 (image 18, tick 54 de la vie), puis constante. Décalage de (−16, −9, 0) à (−89, −79, 0). | FAIT | `sprite21_dump.txt` ; `collision_keyframes` | O |
| S4 | Fiche d'équilibrage 21 : niveau 255, `MaxHp 0`, réponses `0x80` partout. Une attaque, type 10, attribut 10, puissance 128, sans puissance d'équipement. L'export et l'extraction sont égaux. | FAIT | `balance.json` / `BALANCE.BIN.json` | O |
| S5 | `ClassMaskOf(0x2044) = 1` (ClassA seul). Le héros (MoreFlags 140, CanPickup 17) est ClassB et pas ClassA : il n'est jamais touché. Le compteur de coups `hitc` de la colonne reste 0 sur tout le cast, alors que sa boîte pleine recouvre le héros. Le réglage D-H-7 est sans objet. | FAIT | `HitPass.cs:43-50` ; clé | O |
| S6 | Vie (clé `fire2_cast_pf`, T0 = tick 1) : naissance au tick 34 (emplacement 1, statut 1, identité 2, position (100, 100), Z brut 2 = Z héros 1 + 1), statut 2 au 35, fin d'animation `+0xAC = 1` au 154 (S+120), choix du programme E puis E28 et statut 4 au 155 (S+121), case recyclée et identité changée au 156. | FAIT | clé | O |
| S7 | Pendant la vie : aucune force, aucune gravité, position et Z constants. La fiche d'attaque `+0x1C8` est posée dès la naissance. Chaque cible touchée reçoit l'effet attaché 4 (nibble 10 donne l'effet 4, `HitPass.EffectOfNibble`). | FAIT | clé ; `HitPass.cs:64-70` | O |

### 1.5 Chronologie du cast (graine `0x12345678`, héros (100, 100) vers l'est, champ plat, aucune autre entité ; clé `fire2_cast_pf`)

| Tick | Événement |
|---|---|
| 1 | Front du Rond : départ des tomes, PM 4 → 3, animation `0x32`, objet 0x30 en cours, phase 0, compteur 0 (après le tick). |
| 2 | Premier appel du pilote (`c = 0`). |
| 33 | Animation héros `0x33` (chaîne de 32 ticks). |
| 34 | Pilote `c = 32` : naissance du sprite 21, tampon ← bloc (zéros), phase 1 dans le même tick. Bloc = `[1,0,2,0,2,0,0,0,0]`. |
| 35 à 155 | Bloc écrit à chaque tick : `LimitY = SpeedY = (t−2) >> 4` (2 au tick 34, 3 dès 50, 4 dès 66, 5 dès 82, 6 dès 98, 7 dès 114, 8 dès 130, 9 dès 146) et `LimitX = SpeedX = (t−2) >> 6` (0 jusqu'à 65, 1 dès 66, 2 dès 130). |
| 154 | Fin d'animation du sprite 21. |
| 155 | E28 : 8 tirages (rng0 = `0xC4F6D6C2` en `_pf`), 8 effets (19, 2), statut 4. Le pilote, qui précède les entités dans le tick, voit encore l'identité 2 et écrit le bloc. |
| 156 | La passe des emplacements change l'identité de la case. Pilote : bloc ← tampon (zéros), animation héros `0x34`, phase 2. |
| 182 | Animation héros 0 (chaîne de 26 ticks). |
| 183 | Pilote phase 2 rend 1 : objet libéré. |

Le héros est immobile de 1 à 182 (3,64 s à 50 Hz).

Les 8 effets de la clé, en (X, Y, Z) bruts, dans l'ordre k = 0..7 :
- (6553600, 9507328, 2), (4249651, 8089566, 2)
- (1998592, 6553600, 2), (5212390, 5659460, 2)
- (6553600, 5334528, 2), (9104614, 4852924, 2)
- (8568832, 6553600, 2), (10066810, 8895740, 2)

### 1.6 Dégâts et tremblement

| # | Fait | Étiq. | Source | Re-vérifié |
|---|---|---|---|---|
| D1 | Contre une cible ClassA à réponse 16, le coup vaut `max(1, (128·16) >> 4) = 128`. Une même cible est reprise tous les 25 ticks (`+0x1D0 = 0x19`) tant que la boîte la recouvre. | FAIT | R §5 ; `damage.txt` (20 480 cas, 0 écart) ; clé | O |
| D2 | Écritures de coup, cible en (140, 100) : ticks 43, 68, 93, 118, 143. Cible en (100, 140) : 55, 80, 105, 130. Cible en (180, 100) : 73, 98, 123, 148. Le premier coup de (140, 100) est à S+9, image 3 : la boîte couvre x = 71..129 (décalage −29, largeur 58) contre le corps du bloc 25 en 128..152 (décalage −12, taille 24). À l'image 2 la boîte s'arrête en 122 : pas de recouvrement. Cela contrôle la croissance de la boîte. | FAIT | `H/keys/fire2_targets_pf.txt` ; `sprite-records.json` `alundra_25` | O (nouveau) |
| D3 | Chaque coup tire dans le générateur (3 étincelles à 2 tirages, 6 par tick de coup). Avec des cibles, les 8 positions d'E28 changent. | FAIT | clé (ticks à tirages : 43, 55, 68, 73, 80, ..., 155) | O |
| T1 | Disposition du bloc : Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY. Ce sont les neuf propriétés de `CameraSway.cs:33-41`. Le pas `0x8002C894` est `CameraSway.Step` (`:65`). | FAIT | CB §5 ; `vec_sway` | N |
| T2 | Le tampon `0x80127138` n'a que quatre sites, tous du feu 2 : `0x80034E14`, `0x80035D04`, `0x80035D88`, `0x80035E28`. | FAIT | CB §5 | N |
| T3 | Dans le binaire la caméra (donc le pas du tremblement) précède le tick dans `0x8002C3F4-0x8002C45C`. Le pas du tick N+1 lit ce qui a été écrit au tick N. | FAIT | CB §5 | N |

---

## 2. Les clés d'oracle

### 2.1 Ce qui existe

| Clé | Contenu | S'applique au banc DLL ? | Vu |
|---|---|---|---|
| `trace_cast.py 0x30` et l'entrée `item 0x30` de `results/trace_summaries.txt` | Chronologie du héros (animation, objet, phase, compteur, PM) et entités nées : 1 sprite 21, naissance 34, vie 122, 8 effets (19, 2) comptés seulement. `--out` écrit aussi, par entité, statut, identité, type, X, Y, Z, animations, forces, PV et drapeaux. Pas d'arguments d'effet, de générateur, de bloc ni de tampon. | Oui : héros (100, 100), direction 0x18, champ plat synthétique, graine `0x12345678`, aucune autre entité. | O : rejeu identique (`H/work/fire2_trace_orig.json`) |
| `trace_targets.py`, `targets_inv0.txt`, `targets_inv30.txt` | Six cibles du sprite 25 (ClassA, 5 000 PV, réponses 16). Coups, dégâts, tick de résolution (écriture + 1). | Seulement avec des cibles. La résolution est faite par `ComputeNewHp` (le programme D de l'ennemi), absente de la DLL pour un bloc. | O pour (140, 100) : 5 coups de 128, le premier résolu au tick 44 |
| `interrupt_check.py`, `interrupt_check.txt` (animation 0x31 posée avant le tick 60) | État des entités aux ticks 60 à 62 ; journal des effets (8 × (19, 2) au tick 60). | Oui : la DLL pose `TargetAnimationId = 0x31` de la même façon (le chemin de blessure E20.c2b n'est pas nécessaire). | O |
| `e20-h4b/bin/vec/vec_fire2_80035C64.json` (60) et `critic/vec_extra/vec_extra_cancel_fire2.json` (1) | Le pilote seul, en images mémoire. 7 vecteurs appellent le bouchon de naissance (3 réussissent, 4 échouent). Phases 0 (19), 1 (25), 2 (6), 3 (10). | Rejouables par un adaptateur image → proxies (méthode de D-H4B1-3). | O : les 770 vecteurs d'e20-h4b, 0 échec |
| `vec_sway_8002C894.json` (80) | Le pas du tremblement. | Déjà couvert côté DLL par les tests K1 de `CameraSway`. | N |
| Vecteurs d'E28 : aucun versé (`h4_vec_handlers.py` les tire au vol) | | | Générés ici : `H/keys/vec_e28_8007FB58.json`, 202 vecteurs |

### 2.2 Ce qui doit être refait, et pourquoi

1. **Le cast complet en `_pf`.** C'est la règle du bloc du §1.20, point 3.
   - La valeur des 8 tirages décide la position des 8 effets, que le test compare.
   - Le harnais n'avance pas le générateur par tick (E7). La DLL l'avance d'un pas par tick après la mise à jour des effets (`WorldProxy.cs:2692`, `:3035`).
   - Refaite : `H/keys/fire2_cast_pf.json`, avec son `.summary.txt`.
   - Contrôle fait : `rng0(t+1) == pas(rng1(t))` à tous les ticks, et un seul tick à tirages (le 155, huit pas).
   - La sonde de T0 est donc : après chaque image, l'état de la DLL égale `rng0(t+1)`, et le tick 155 compte 8 + 1 pas.
   - Pour mémoire, la clé sans `_pf` donne d'autres positions (premier effet en Y 9224704 au lieu de 9507328) : `fire2_cast_nopf.json`.
2. **L'annulation en vol** : `fire2_hurt60` (en `_pf`).
   - Les 8 effets naissent au tick de l'annulation ; le premier est en (6553600, 9487360, 2).
   - Le bloc est remis à zéro, le statut 4 est atteint au tick 60, l'objet est libéré.
   - Elle remplace la ligne de `interrupt_check.txt`, qui ne garde que des comptes.
3. **L'annulation avant la naissance** (cas absent des annexes).
   - `fire2_hurt20` : bloc nul et tampon nul, rien ne bouge.
   - `fire2_hurt20_zerobuf` : le bloc préexistant est remplacé par des zéros.
   - `fire2_hurt20_stale` : le tampon `9×9` est restauré tel quel.
4. **L'aller-retour d'un bloc préexistant** : `fire2_cast_preset_block`.
   - Au tick 34 le tampon reçoit l'ancien bloc.
   - Au tick 156 le bloc est restauré à l'identique, Offsets et Reach compris (le pilote ne les touche pas).
5. **L'annulation en phase 2** : `fire2_hurt170`.
   - Aucune annulation : la garde n'est pas appelée.
   - L'objet est libéré au tick de la blessure, parce que le pilote rend 1 (l'animation n'est plus 0x34).
   - Le bloc reste restauré.
6. **Les cibles en `_pf`** : `fire2_targets_pf` (trois cibles).
   - Les écritures de coup ne dépendent pas des tirages.
   - Les positions d'E28 en dépendent.
   - Un test qui les compare avec des cibles lit cette clé.
7. **Vrai départ.**
   - Toutes ces clés font tourner le vrai départ des tomes (`0x800348E0`, Rond au tick 1) puis le vrai pilote dès le tick 2, jamais un pilote posé au milieu.
   - Côté DLL, avec h4a faite, les tests de monde utilisent le départ réel.
   - Les coutures `ItemStartForTests` et `ItemDriverForTests` ne servent qu'aux tests du pilote seul.
8. **Pas de héros insensible** : S5, le héros n'est pas ClassA.
9. **Naissance refusée en monde** : à l'échelle du pilote, les 4 vecteurs à bouchon nul suffisent. Une clé de monde est optionnelle.

### 2.3 Ce qui ne s'applique pas tel quel

- **Cibles des clés de coups.** `HitArena` a des Beannoïdes (sprite 79, fiche 335), pas des blocs 25.
  - Première voie : faire naître des blocs `alundra_25` par l'interface. Il existe : MoreFlags 128, CanPickup 96, 24×16×32, décalage (−12, −8, 0). Il faut poser `ClassA` à la main, comme le harnais le fait. C'est la plus simple.
  - Seconde voie : refaire la clé avec le Beannoïde de l'arène (harnais d'`e20-f4f6`).
- **Résolution des dégâts** : `ComputeNewHp` en tête de tick appartient au programme D de la cible, hors tranche.

---

## 3. Atterrissage dans la DLL au HEAD `023b33ce`

### 3.1 Ce qui existe

| Brique | Où | Remarque |
|---|---|---|
| Naître par indice | `Alundra/Maps/WorldProxy.cs:3488` (`SpawnEntityBySpriteIndex`) ; interface `IEntityWorldContext.cs:81` ; précédent d'appel `PlayerManager.cs:1216-1218` | Z en repère DLL, l'unité brute est ajoutée par l'interface (`:3549`, D-E0A-1). Pose `JoinsBirthTick` et `HasLogicalZ` en dernier (`:3572-3573`). Peut rendre `null`. |
| Détruire et recycler | `WorldProxy.cs:3301` (`DestroyEntity(entity, effectId)` : `FlagToDestroy`, `EventTrigger = -1`, bris si `fx != 0`) ; `:3329` (un argument) ; `:3265` (`RecycleDestroyedEntities`, appelé en `:2572` après les événements de carte et avant `RunEntityBlock` `:2582`, ordre de la passe `0x80038634`) | Après recyclage `Slot = -1`. L'identité `+4` du binaire n'a pas de membre. « Identité changée » équivaut à « entité recyclée », soit `Slot < 0` (INFÉRENCE). |
| Chaîne vers E | `EntityScriptProxy.cs:1717-1722` (Normal vers Deactivated sur `DeactivateOnAnimationEnd` et `ForceResetAnimationFlag != 0`) ; `:1758` (Deactivated : E à chaque tick) ; `:1774` (`RunPickedEvent`) | Un statut 3 posé de l'extérieur fait lancer E au tick de l'entité, dans le même tick si le héros la précède. |
| Registre | `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (`CreateForProduction`), `:54-67` (`Register`), `:70-72` (`TryGet`) ; consulté en `Alundra/Events/EventProgramRunner.cs:301`, après le livre de sauvegarde et E0/E1 en ligne (`:288`, `:328`) | 7 clés. Aucune clé (E, 28). `ScriptHelper.ProgramEDeactivate = 4` (`Alundra/Events/ScriptHelper.cs:23`). |
| Modèle de fichier | `Alundra/Ai/Projectiles/WaveSwordAi.cs` (`Register`, journal par thread) ; D-H-6 prévoit `Alundra/Ai/Items/BombAi.cs` | Le dossier `Alundra/Ai/Items/` est à créer. |
| Effets | `EffectStage.Emit` (`EffectStage.cs:192`) ; `CreateNow` (`:199`, réservé au héros, D-E20-28) ; `EffectReservoir.Create(bool mapTable, int effect, int animation, x, y, z)` (`EffectReservoir.cs:101`, 128 emplacements, `null` si plein) ; précédents `NativeEffects.CreateSprintBounce` (`NativeEffects.cs:161`) et `PlayerManager.cs:813-818` (`z = PosZ + 1`) ; accès depuis une entité par `(ScriptHost as IEntityWorldContext)?.EffectStage` (`EntityScriptProxy.cs:1521`) | L'effet 19, animation 2, existe dans `alundra-project/Data/effects/effects-global.json`. |
| Générateur | `Alundra/Rng/GameRandom.cs` : `Next()` (pas puis 32 bits bas) ; `AdvanceFrame()` (un pas par tick) | Précédent du produit haut : `(int)(((ulong)r1 * 0x20001) >> 32)` (`NativeEffects.cs:52-57`). |
| Tremblement | `Alundra/Cameras/CameraSway.cs` : singleton de session (`:26`), neuf propriétés (`:33-41`), `Start` (`:45`), `Stop` (`:55`), `OnMapLoad` (`:58`, Flag seul), `Step` (`:65`), `ResetForTests` (`:116`). Accessible par `IEntityWorldContext.CameraSway` (`:193`, `WorldProxy.cs:699-701`). Le pas est fait par `CameraDirector.UpdateCameraFollow` (`:214-236`) et `CameraMath.AdvanceCameraSmoothing` (`:167`), une fois par image après tous les ticks (`WorldProxy.cs:2716`). | Aucun tampon, aucune capture ni restauration. Les opcodes `0x8E/0x8F` l'écrivent (`EventProgramRunner.cs:1580-1598`). |
| Passe de coups | `Alundra/Combat/HitPass.cs:43` (`ClassMaskOf`), `:100-135` (attaquants, cibles, filtres), `FrameBoxTimeline.cs` | Le sprite 21 est attaquant. |
| Cadre d'usage de l'objet | `PlayerManager.cs:282` (`MovePlayer`), `:353` (appel du pilote), `:932` (`TryUseItem`, bouchon), `:937` (`TryUseItemResultForTests`), `:1507-1509` (`UpdateItemUseDriver`, vide), `:740-742` (`CancelItemMode`, vide, appelé en `:664`), `:620-621` (commentaire « NOT PORTED » à la place du test du défaut) | h0a et h4a ne sont pas faits : ce sont les sites qu'elles transformeront en tables d'entrées et coutures D-H-4. |
| Champs du héros | `EntityScriptProxy.cs:158` (`TargetAnimationId`), `:474` (`Slope_18c`), `:432-442` (zone « Hero only » de D-H-1) | Il manque un champ « entité née par le sort ». |
| Banc de monde | `Alundra.Tests/AlundraArcSupport.cs` (`ArcRun` : `Give`, `Equip`, `SetMp`, `SetItem`, `Press`, `OneFrame`, `PlaceHero`) ; `PadState.Circle` (`Alundra/Hero/PadState.cs:29`) ; champ plat `AlundraMapEdgeWorldTests.cs:43-50` ; retrait des entités `AlundraLootRollWorldTests.cs:74-78` ; vie d'un projectile `AlundraWaveSwordLifeTests.cs` | `GameRandom.RandomSeed = 0x12345678` posé juste avant la première image. |

### 3.2 Où va chaque pièce

| Pièce | Fichier proposé | Contenu |
|---|---|---|
| Pilote, annulation, E28, inscription | `Alundra/Ai/Items/FireTome2Ai.cs` (neuf ; nom à passer à `tools/dll-structure/check_names.py`, Q-R-7) | `Drive(hero, item)` (phases 0 à 2, rend 0 ou 1), `Cancel(hero)`, `Explode(entity, context, state)` (E28), `Register(registry)` avec `registry.Register(ScriptHelper.ProgramEDeactivate, 28, new[] { 21 }, ...)` |
| Inscription de production | `Alundra/Ai/NativeSpriteRegistry.cs:41` | `FireTome2Ai.Register(registry);` (7 vers 8 clés) |
| Tampon | `Alundra/Cameras/CameraSway.cs` | neuf entiers de session ; `CaptureToBuffer()` ; `RestoreFromBuffer()` sans garde ; remis à zéro par `ResetForTests` ; non touché par `OnMapLoad` |
| Pointeur de l'entité née | `EntityScriptProxy.cs` (zone `:432-442`) | un champ « héros seulement » `ItemSpawnedEntity`, remis à nul à la reconstruction du héros. Un champ commun suffit aux huit pilotes : chacun écrit avant de lire, et un seul tome est en cours. |
| Entrées de l'objet 0x30 | `PlayerManager.cs` (tables de h0a/h4a) | départ = le départ commun des tomes (h4a), pilote = `Drive`, annulation = `Cancel`. Ce sont des bouchons nommés (D-H-3) avant la tranche. |

### 3.3 Tests existants qui bougent (prédits, recomptés au HEAD)

- `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:55` : `Assert.Equal(6 + 1, runner.NativeSprites.Count)` (test aux lignes 51-55). Devient « compte d'avant + 1 ».
- `Alundra.Tests/AlundraWaveSwordLifeTests.cs:181-183` : `(3 + 3 + 1, ..., registry.Count, ...)`. Idem.

Ne bougent pas (FAIT par lecture et `grep`) :
- S5, `AlundraWeaponSpawnSeamTests.cs:238-258` : l'ensemble câblé est {27, 28, 29} et il n'inscrit que (E, 17).
- `AlundraNativeSpriteRegistryTests.cs:84-96` : aucune inscription ne liste −1 ; la nôtre liste {21}.
- `AlundraNativeSpriteRegistryTests.cs:205-236` et `:238-251`.
- `AlundraBirthBoxTests.cs:131` : fait naître tous les préfabs globaux sans tick.
- Aucun test ne fait vivre le sprite 21.
- `AlundraCameraSway*Tests` : `ResetForTests` « neuf champs » reste vrai.
- `AlundraTestSavesTests.cs` : `arsenal-b-m14` n'est pas modifié.

Il y a six `ResetSingletons` dans `Alundra.Tests`. Un seul appelle `CameraSway.Instance.ResetForTests()` : `SaveGameDirectorTestSupport.cs:134`. Les cinq autres sont `AlundraGameTimeTests.cs:33`, `AlundraNativeEffectTests.cs:49`, `AlundraSaveGameProductionTests.cs:48`, `AlundraSaveGameLoadResetTests.cs:34` et `BackdropWarpDepartureFreezeTests.cs:224`. La suite est sérialisée (`xunit.runner.json`, `parallelizeTestCollections: false`). Le tampon ne fuit que dans une classe qui lance un cast. Il suffit que les classes de la tranche remettent à zéro dans leur constructeur et leur `Dispose` (motif de `AlundraCameraSwayTests.cs:22-39`). L'ajout aux cinq autres est une précaution, pas une nécessité (INFÉRENCE).

---

## 4. Plan proposé

**But.** Le héros lance le tome du feu 2 comme l'original :
- 1 PM, 32 ticks de pose ;
- la colonne naît au tick 34 et grossit pendant 120 ticks, l'écran tremble par paliers ;
- elle brûle ce qu'elle recouvre (attaque 10, puissance 128, tous les 25 ticks par cible) ;
- huit explosions à sa fin, puis 26 ticks de relâchement.

**Prérequis.** h0a, h4a (READY), la fin du chantier de restructuration et Q-R-7. Déjà faits : e0a1 à e0a5, e0b1/e0b2, e0k1 à e0k3, c1a/c1b, f3b, E19.k1. Aucune dépendance à f5, f6a, h4b1, h4c, E3 ni E13.

**Périmètre.**
- Fichiers DLL : `Alundra/Ai/Items/FireTome2Ai.cs`, `NativeSpriteRegistry.cs` (une ligne), `CameraSway.cs`, `EntityScriptProxy.cs` (un champ), `PlayerManager.cs` (trois entrées).
- Tests : `Alundra.Tests/` (nouvelles classes et les deux épingles).
- Documents : l'annexe `docs/plan-e20-annexe/e20-h4d1/`, ce plan, l'index des ADR et un ADR.

**Hors périmètre.**
- Le coup de caméra d'E13 (h4c2), E3, la requête de cibles, la poussée.
- Les pistes XA (Q-H4-1) et les sons des effets (Q-H4-5).
- Les sites de blessure (E20.c2b), de mort (E18) et de case de sortie, et le menu Start (h0a).

**Phases.** A est inerte en production (rien ne fait naître le sprite 21 ; l'entrée 0x30 reste le bouchon). B est le câblage visible. Le compte du registre change en A.

### T0 — Annexe, oracle, liste prédite (aucun code)

- **(a)** Rejouer depuis des copies :
  - `trace_cast.py 0x30` ;
  - `interrupt_check.py` ;
  - `h4_vec_handlers.py 3000 777 E28` (0 écart, 67/67) ;
  - `h4_vec_drivers.py 3000 4242` (0 écart) ;
  - `e20-h4b/critic/c9_replay.py` avec `VEC` repointé (ligne 11) sur `e20-h4b/bin/vec/` ;
  - `c6_fire2.py` (4 000/0, 144/146) ;
  - `c5_camera.py`.
  Les sorties doivent être identiques à celles du §7.
- **(b)** Verser les clés refaites de `H/keys/` et les 202 vecteurs `vec_e28_8007FB58.json`, avec `fire2_key.py`, `fire2_targets.py` et `gen_e28_vectors.py`. Rejeu identique sur deux exécutions (comparer `rows`, pas `options`).
- **(c)** Table de correspondance des mots lus par `vec_fire2`, par classes (modèle de D-H4B1-3) :
  - entrées de l'appel : phase, compteur, objet en cours, retour du bouchon de naissance ;
  - le héros : `+0x88` (`TargetAnimationId`), `+0x18C` (`Slope_18c`), `+0x114/118/11C` (`PosX/Y/Z`) ;
  - le bloc et le tampon : les neuf propriétés de `CameraSway` et le tampon ;
  - l'identité : `[ptr+4]` contre `[0x80127128]`, soit `Slot >= 0` ;
  - la boucle d'annulation : `+0x10` (`Status`), `+0x68` (`SpriteType`), `+0x20` (`BlockedByEntity`), et le compte `[0x80134360]`. La DLL parcourt toutes les entités vivantes ; c'est équivalent sous l'invariant « tout emplacement non 0/4 est ≤ compte » (CB §3.4) ;
  - la table d'annulation `0x800233EC` (entrée `0x80023430`) est le travail de h0a, hors DLL ici.
  Un mot lu sans membre arrête la tranche.
- **(d)** Sonde de la DLL avant le code. Faire naître le sprite 21 par l'interface au point du pilote et lire 150 ticks :
  - statut 1 puis 2 ;
  - fin d'animation à S+120, E28 à S+121 (statut 4), case libre à S+122 ;
  - position et Z constants ;
  - Z de naissance = Z héros + 1 unité brute ;
  - écritures de coup aux ticks des clés.
  Un écart arrête (animation de 120 ticks, boîte, `HasLogicalZ`).
- **(e)** Sonde du générateur : après chaque image, l'état de la DLL égale `rng0(t+1)` de `fire2_cast_pf`, et le tick 155 compte 8 + 1 pas. Si E20.e0c est faite avant, son tirage éventuel au `DestroyEntity` du sprite 21 (`Contents 0`) doit rester nul.
- **(f)** Les noms par `check_names.py`. Liste prédite : les deux épingles, recomptées au HEAD de départ.

### T1 — Tests puis code

**Phase A (inerte).**
- **(e1)** E28 contre les 202 vecteurs, en un seul test. Pour chaque vecteur : les 8 appels d'effet (19, 2, X, Y, Z), l'état du générateur, `DestroyEntity(−1)`. Z DLL = brut − 1, et le Z de l'effet est `PosZ + 1`.
- **(e2)** Réserve pleine (128 emplacements) : 8 tirages, 0 effet, destruction faite.
- **(e3)** La destruction laisse `FlagToDestroy` et `EventTrigger = −1`, sans effet de bris.
- **(e4)** Exactement 8 tirages, mesurés par la sonde du générateur.
- **(e5)** Inscription : `(4, 28)` pour le type 21 seul (`TryGet(4, 28, 0x115)` faux), clé non en ligne, compte = avant + 1, E28 lancé par `RunSpriteEvent` sur un statut 3.
- **(e6)** Les huit valeurs des tables aux indices 0, 4, ..., 28, écrites à la main.
- **(b1)** `CaptureToBuffer` copie les neuf mots dans l'ordre, sans changer le bloc.
- **(b2)** `RestoreFromBuffer` sans garde : un tampon nul remet à zéro un bloc non nul ; un tampon périmé est restauré tel quel.
- **(b3)** Le tampon est nul au départ, survit à `OnMapLoad`, est vidé par `ResetForTests`.
- **(b4)** Un bloc restauré avec Flag 0 est vidé au pas suivant ; avec Flag 1 il continue.
- **(d1)** Le pilote contre les 61 vecteurs, en un seul test, par l'adaptateur image → proxies.
- **(d2)** Phase ≥ 3 ou négative : rend 0, rien d'écrit.
- **(d3)** Naissance refusée (`null`) : pointeur nul, rend 1, aucune capture, phase 0.
- **(d4)** La capture précède l'écriture de Flag ; le bloc du tick de naissance est `[1,0,2,0,2,0,0,0,0]` à `c = 32`.
- **(d5)** Une entité en `FlagToDestroy` non recyclée (`Slot >= 0`) compte comme vivante (le bloc est écrit au tick 155). Recyclée, elle déclenche la restauration et l'animation 0x34 (tick 156).
- **(d6)** Paliers : `c` = 15, 16, 63, 64, 127, 128 donnent (c>>4, c>>6) = (0,0), (1,0), (3,0), (4,1), (7,1), (8,2).
- **(c1)** Annulation :
  - une entité de type 21 au statut 2 passe au statut 3 et son `BlockedByEntity` est vidé ;
  - accolés : les statuts 1, 3 et 4 du type 21 ne bougent pas ; les types 20, 22, 23, 24, 17 au statut 2 ne bougent pas ; une entité de la page de carte d'indice 21 (`SpriteType = 0x115`) ne bouge pas.
- **(c2)** L'appel par la garde (pente 4, ou animation hors 0x32/0x33) restaure et rend 1 sans avancer la phase.

**Phase B (câblage).**

Banc :
- `ArcRun` de la carte 14, champ plat par `FlatCells.Create` et réflexion ;
- entités de la carte retirées (`Status = FlagToDestroy` puis `RecycleDestroyedEntities`) ;
- héros en (100, 100) vers l'est, `Give(48)`, `SetItem(48)`, `SetMp(4, 4)` ;
- `GameRandom.RandomSeed = 0x12345678`, un tick par image, `Press(PadState.Circle)` à la première image.

Le bloc et les entités sont lus au point `TickTraceForTests("inventory")` du tick (après le bloc d'entités, avant le suivi de caméra). Le générateur et les effets sont lus après l'image.

- **(w1)** Le cast complet contre `fire2_cast_pf`, aux ticks 1 à 183 :
  - animation du héros, objet en cours, phase, compteur, PM ;
  - les mots du bloc, le tampon ;
  - le sprite 21 (statut, position, Z) ;
  - les 8 effets de S+121 ;
  - le générateur ;
  - la restauration au tick 156 et l'objet libéré au 183.
- **(w2)** Annulation en vol (blessure posée avant le tick 60) contre `fire2_hurt60`, accolée au cast complet.
- **(w3)** Annulation avant la naissance (tick 20) : trois cas, `fire2_hurt20`, `_zerobuf` et `_stale` ; aucun effet, aucun tirage.
- **(w4)** Aller-retour d'un bloc préexistant (`fire2_cast_preset_block`). La lecture doit se faire au point « inventory », puisque le pas de caméra modifie les Offsets.
- **(w5)** Annulation en phase 2 (`fire2_hurt170`).
- **(w6)** Cibles : un bloc 25 ClassA en (140, 100) donne des coups aux ticks 43, 68, 93, 118, 143 ; en (180, 100) aux ticks 73, 98, 123, 148 ; en (100, 140) aux ticks 55, 80, 105, 130 ; le héros n'est jamais touché ; une cible hors de la boîte maximale ne reçoit aucun coup.
- **(w7)** 2 et 3 ticks par image : seulement ce qui ne dépend pas des valeurs tirées (ticks de naissance, de fin et d'annulation, libération, nombre d'effets, nombre de tirages). Le pas du tremblement d'un lot d'images utilise les réglages du dernier tick (écart documenté, Q1).
- **(w8)** Câblage : le Rond avec l'objet 0x30 fait naître le sprite 21 ; les tomes dont la tranche n'est pas faite restent des bouchons silencieux (D-H-3).
- **(w9)** Optionnel : naissance refusée en monde (table des emplacements pleine), le cast perd son PM, l'objet est libéré au tick 34, retour à Idle sans 0x34.

### T2 — ADR, plan, verifiers frais

**Mutants (au moins 14, tous tués).**
- Table : indice `k` au lieu de `4k` ; `+0x320` retiré ; borne `0x1800` ; produit signé ; X et Y permutés.
- Effet : Z avec `+0x100000` ; Z sans `+1` ; animation 1 ou effet 18 ; tirage sauté quand la réserve est pleine.
- Destruction : avec effet de bris ; `−1` retiré.
- Tampon et bloc : capture après l'écriture de Flag ; capture en phase 1 ; `>>4` et `>>6` permutés ; Speed ≠ Limit.
- Pilote : identité testée par le statut 4 ; restauration gardée par « tampon non nul » ; restauration manquante à la mort ; 0x34 non posé ; phase 2 qui n'attend pas 0x34 ; garde appelée en phase 2.
- Annulation : `SpriteTableIndex` au lieu de `SpriteType` (tué par l'indice 21 de la page de carte) ; statuts 1, 3 et 4 rendus à 3 ; `+0x20` laissé.
- Câblage : entrée 0x30 laissée au bouchon (tué par w1).

**Verifiers frais.** Deux, avec les verdicts `CONFIRMED` ou `REFUTED` : l'un sur les clés et la correspondance des vecteurs, l'autre sur les tests de monde et les mutants.

### Acceptation

- **Oracle.** T0 (a) et (b) identiques sur deux exécutions ; (d) et (e) mesurés sans écart.
- **DLL.**
  - (e1) à (e6), (b1) à (b4), (d1) à (d6), (c1), (c2) et (w1) à (w8) sont verts, et rouges avant le code (sauf les témoins négatifs).
  - La suite est verte en Release (copie neutralisée) et en Debug.
  - Les tests déplacés sont inclus dans la liste prédite (les deux épingles).
  - `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.
- **Preuve de retrait.**
  - Phase A : retirer l'inscription rend rouges exactement (e5) et les deux épingles réécrites par dérivation.
  - Phase A : neutraliser E28 (ni effet, ni tirage, ni destruction) rend rouges (e1) à (e4).
  - Phase A : neutraliser le tampon rend rouges (b1), (b2), (d1), (w3) et (w4).
  - Phase B : remettre l'entrée 0x30 au bouchon rend rouges exactement (w1) à (w7) et (w9) ; restent verts (e*), (b*), (d*), (c*).
  - Un pilote constant à 1 rend rouges (d1) à (d6) et (w1) à (w5).
- **Recette de l'auteur** (non bloquante).
  - Charger la sauvegarde F9 `arsenal-b-m14` (carte 14, case (8, 6, 0), tomes 43 à 50, bâton 7, MP 4/4) et choisir l'objet du Rond.
  - Si le Rond tient 47 (feu 1, pas encore porté), le premier appui est silencieux et promeut vers 48 (h4a (t5)) : appuyer une seconde fois.
  - À voir : 32 ticks de pose, la colonne qui grossit pendant 2,4 s, l'écran qui tremble par paliers (jusqu'à 9 px en Y et 2 px en X), huit éclatements, le héros bloqué 3,6 s, 1 PM de moins.
  - Avec un ennemi dans la zone de 178×158 px : un coup toutes les 25 ticks.
  - Si le sort est cassé (héros touché pendant la pose) : les huit éclatements d'un coup, plus de tremblement.

### Arrêts, budget, retour arrière

- **Arrêts.**
  - Un test existant autre que les deux épingles bouge, ou une trace change.
  - Un rejeu de T0 diffère.
  - La sonde du générateur diffère ailleurs qu'au tick 155 (ou y compte autre chose que 8 + 1).
  - La vie du sprite 21 dans la DLL diffère de S+121, ou son Z ou sa position dérivent, ou la boîte ne recouvre pas la cible aux ticks de la clé.
  - Un mot lu par les vecteurs du pilote n'a pas de membre.
  - Un besoin de toucher au moteur, au convertisseur ou aux données.
  - Un nom refusé par `check_names.py`.
  - h0a ou h4a absentes ou différentes du plan.
- **Budget.** Un `executor`, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière.** Remettre l'entrée 0x30 au bouchon (phase B) ; retirer le fichier, l'inscription et l'API du tampon (phase A). Rien d'autre n'en dépend.

---

## 5. Questions pour l'auteur

1. **Q-H4D1-1 — Le tremblement de l'écran : exact au tick près, ou celui de la DLL ?**
   - Dans l'original la caméra, donc le pas du tremblement, passe avant les entités du tick.
   - Dans la DLL elle passe après toutes les entités de l'image. Le tremblement commence donc un tick plus tôt par rapport à la colonne (20 ms, invisible).
   - Quand une image compte 2 ou 3 ticks, tous ses pas utilisent les réglages du dernier tick (au plus un palier de 16 ticks de différence, invisible).
   - (A, défaut) garder l'ordre de la DLL, écart documenté.
   - (B) ranger le bloc écrit par tick comme le coup de caméra de h4c2 (Q-H4B-4) : un champ de plus, aucun gain visible.
2. **Q-H4D1-2 — Le tampon du tremblement vit-il toute la session ?**
   - L'original ne le remet à zéro qu'au démarrage de la console : il passe d'une carte à l'autre et d'une partie chargée à l'autre.
   - Cas où cela se voit : annuler le sort avant la naissance de la colonne (blessure dans les 32 premiers ticks) restaure le tampon du dernier lancer. Si un tremblement de scène était actif à ce lancer, il revient (rare).
   - (A, défaut) fidèle, remis à zéro seulement au démarrage du jeu.
   - (B) le remettre à zéro à chaque nouvelle partie et chaque chargement : un écart documenté.
3. **Note de recette, pas une question.** `arsenal-b-m14` porte déjà le tome 48 (`TestSaves.cs:125` ; lignes de `WeaponRows` dans `AlundraTestSavesTests.cs`), et aucune carte de la chaîne ne le donne. Aucun changement de `TestSaves.cs` n'est nécessaire.

Les ouvertes déjà au plan qui touchent la tranche sans changer sa forme :
- Q-H4-1 : piste XA du départ, dans h4a.
- Q-H4-4 : menu Start refusé 181 ticks, dans h0a.
- Q-H4-5 : le son éventuel de l'effet 19, animation 2, n'est pas décodé.

---

## 6. INCONNU et INFÉRENCE restants

- **INCONNU.**
  - Le son de l'effet 19, animation 2.
  - Le rendu exact du tremblement en jeu.
  - Les écritures du bloc ou du tampon par déplacement calculé dans la machine virtuelle de scripts.
  - Le verdict de `check_names.py` pour `FireTome2Ai`.
  - Si le menu du Rond donne 47 ou 48 à la sélection.
- **INFÉRENCE.**
  - « Identité changée » côté DLL = `Slot < 0` (autre voie : un numéro de série ajouté par `SpawnEntityBySpriteIndex`).
  - Création des effets par `Emit` plutôt que `CreateNow` : aucune différence observable tant qu'aucune autre source ne crée dans le même tick, puisque la création ne tire pas.
  - Un champ commun `ItemSpawnedEntity` pour les huit pilotes.
  - L'ordre caméra / entités du binaire n'a pas d'effet visible.
  - La réserve ne tire pas pendant sa mise à jour (appuyé par les tests dorés G3).
  - Les tests des cinq autres `ResetSingletons` n'ont pas besoin de l'ajout.
- **Limite du FAIT.** Le balayage du graphe d'appels est statique : seuls les `jal` sont suivis.

---

## 7. Journal des vérifications (tout est dans `H/`, lancé avec `python -B` depuis des copies)

| Vérification | Résultat |
|---|---|
| Rejeu de `trace_cast.py 0x30` (copie de `e20-h4/work/oracle`) | Identique à la ligne versée : MP 4 → 3, ticks 1, 33, 34, 156, 182, 183, sprite 21 né au 34 et vivant 122 images, effets (19, 2) × 8 |
| `h4_vec_handlers.py 3000 777 E28` | 3 000 vecteurs, 0 écart, 67/67 instructions |
| `h4_vec_drivers.py 3000 4242` | 3 000 vecteurs, 0 écart ; feu 2 145/147, annulation du feu 2 47/47, garde 19/19 |
| `e20-h4b/critic/c9_replay.py` (copie, `VEC` repointé sur la copie de `bin/vec`) | 770 vecteurs, 0 échec (dont `vec_fire2` 60/0, `vec_sway` 80/0, `vec_camera` 100/0) |
| `c6_fire2.py`, `c5_camera.py` | 4 000 exécutions du pilote, 0 écart, 144/146 ; 3 000 essais caméra et tremblement, 0 écart, 184/185 |
| Capstone (copies de `exelib.py` et `dis2.py`), `0x8007FB58-0x8007FC64`, `0x80034E08-0x80034EC4`, `0x8002BCF0-0x8002BD50` | E28 : `0x1801`, `0x320`, `0x13`, tables `0x80023654/94`, `+0x114/118/11C`, 8 tours, `DestroyEntity(−1)`. Annulation : boucle `1..[0x80134360]` inclus, type `0x15`, statut 2 vers 3, `+0x20 = 0`. Pas de générateur en `0x8002BD0C-0x8002BD34` |
| `gen_e28_vectors.py` : 202 vecteurs enregistrés sur le vrai code avec `cmips.py` | 0 écart avec le modèle (tirage, tables, Z, destruction). Le vecteur « graine de la clé » égale les 8 effets du tick 155 de `fire2_cast_pf` |
| `rng_scan.py` sur `0x8003BDD8`, `0x8003C410`, `0x80038E84` | Aucune fonction atteinte ne touche `0x80098708` ; sauts indirects présents dans la mise à jour |
| `fire2_key.py` (cast, `--pf`, `--hurt`, `--block`, `--buf`) | Clés refaites, déterministes (deux exécutions, mêmes `rows`) ; un seul tick à tirages (155) ; `rng0(t+1) = pas(rng1(t))` partout |
| `fire2_targets.py --pf` (cibles (140,100), (100,140), (180,100)) | Écritures de coup aux ticks 43, 68, 93, 118, 143 / 55, 80, 105, 130 / 73, 98, 123, 148 ; 3 étincelles + effet attaché par tick de coup |
| `sprite21_dump.py` | En-tête, 40 images de 120 ticks, 19 boîtes de 30×18×127 à 178×158×127, fiche d'équilibrage 21 |
| Lectures de la DLL | Lignes citées au §3 (registre, runner, proxy, `WorldProxy`, `CameraSway`, `CameraDirector`, `CameraMath`, `HitPass`, `NativeEffects`, `PlayerManager`, `TestSaves`, tests) |

Le dossier ne contient que des fichiers sous `H/`. Les scripts de `work/h4oracle`, `work/h4b` et `work/h4res` sont des copies des annexes `e20-h4` et `e20-h4b`. Rien n'est écrit dans `data-extracted/`, `alundra-project/` ni `alundra-datas-analyser/`.