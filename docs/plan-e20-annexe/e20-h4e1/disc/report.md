Le Write de `report.md` et `check.md` a été refusé par l'outil. Ce message en tient lieu. Seul `README.md` est écrit dans le dossier de sortie.

# Découverte E20.h4e1 : le vent 1 (objet 0x31, pilote 0x80035EB0), lecture seule

**Base.** Dépôt `D:/development/repo/alundra-casaengine-project-converter`, HEAD `ed60a0eb` (branche `chantier/restructuration-dll-r3`).
- `git diff --stat f81304b HEAD -- Alundra Alundra.Tests alundra-project/Data` est vide : le code DLL lu est celui de `docs/plan-e20-annexe/e20-h3/state-f81304b/`.
- Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, relu par `tools/facts_check.py`.
- Dossier de sortie `OUT` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el2/h4e1/`.
- Rien n'a été écrit hors de `OUT` : ni édition, ni git d'écriture, ni build, ni test, ni export. `data-extracted/`, `alundra-project/` et `alundra-datas-analyser/` sont lus seulement. Python en `-B`, depuis des copies de harnais.
- Dans le dépôt, `CasaEngineMonogame/CasaEngine.Launcher/Program.cs` (10 oct.), `alundra-datas-analyser/MGUI` et `AlundraTools/log.txt` (3 oct.) apparaissent modifiés. Ils sont antérieurs à la session et je n'y ai pas touché.

**Légende.** FAIT (vu dans le code, les données, le binaire ou l'oracle), INFÉRENCE (déduit, de quoi dit), INCONNU.
- Sources : `h4/report.md` et `h4/check.md` = `docs/plan-e20-annexe/e20-h4/` (la critique `check.md` fait foi). `plan:N` = `docs/plan-e20-heros.md:N`.
- Preuves : [B] binaire relu (capstone), [O] oracle (machine MIPS, clés refaites), [V] vecteurs sur le vrai code, [D] DLL au HEAD.

---

## 0. Verdict

1. **h4e1 n'a ni gestionnaire natif, ni clé de registre.**
   - FAIT [B][D] : le sprite 22 a les programmes A0 C0 D0 **E1** (`sprite-records.json` `alundra_22`, `ProgramDeactivate` 1 ; même en-tête dans `map_alundra.json`).
   - E1 est *en ligne* dans le runner, sans condition sur `NativeHandlersEnabled` (`Alundra/Events/EventProgramRunner.cs:283-292`, `IsInlineDeactivateCode` `:328`). Il fait `DestroyEntity(entité, -1)`, et `BreakEffect` 0 / `Contents` 0 : ni effet de bris, ni butin.
   - Donc **les deux épingles du registre ne bougent pas** (`AlundraNativeSpriteRegistryTests.cs:55`, `AlundraWaveSwordLifeTests.cs:181-183`) et **la liste prédite est vide**.
   - Aucun C63, C65, C75 ni E3. **Ni f4, ni f5, ni f6a, ni h4c1, ni E14.b0 ne sont des prérequis** : vitesse d'animation 0, gravité sans `DestroyOnSlidingSlope`.
   - Prérequis réels : h0a et h4a (états, garde, coutures), **h4b1** (la requête), la fin de la restructuration et Q-R-7.
2. **Le pilote est plus précis que le plan ne l'écrit** (FAIT [B][O][V], §1.1).
   - (a) La phase 0 ne regarde pas l'animation : elle met la dernière cible à 0, passe en phase 1 et *enchaîne dans le même appel*.
   - (b) La garde (pente 4 ou animation hors `0x32`/`0x33` : annulation, retour 1) précède la **porte de l'animation `0x33`**. C'est cette porte qui perd le compteur 30.
   - (c) La table se parcourt jusqu'à l'entrée dont le tick dépasse le compteur, avec une frappe à l'égalité seulement. **La fin (`0x34`, phase 2) est posée dans le même appel que la frappe du compteur 100** (tick 102), pas au 101.
   - (d) La distance se compare **signée** (`slti 0x6401`), le Z **strictement** (`slt`).
   - (e) La « dernière cible » est un **pointeur**, donc un numéro de case. Elle est posée même si la naissance échoue.
   - (f) En branche aléatoire, **les deux tirages précèdent la naissance**, quel qu'en soit le résultat.
3. **La branche « détruit sans effet si `IsOnGround` » est morte.**
   - INFÉRENCE appuyée par un FAIT [B] : le seul écrivain du mot `+0x144` d'une entité est `0x8003810C` (passe des attributs de tuile `0x80038064`, `out/scan_off_144.txt`).
   - Une entité née dans le pas du héros n'est pas encore passée par là, et sa case vient du gabarit (zéro).
   - D9 (la décompilation teste le sol dans les deux branches) est donc sans effet visible. Le test reste à porter littéralement (coût nul, couvert par 55 vecteurs).
4. **Manque dans le plan : h4b1 ne rend pas les clés.**
   - FAIT [B] : le pilote lit le tableau `0x800CC158` (`lw` en `0x80035FF8`, `slti 0x6401` en `0x80036000`).
   - `TargetQuery.Find` (plan:8235) ne rend que la liste.
   - Proposition : h4e1 ajoute `TargetQuery.DistanceKey(appelant, entité)`, même formule `(dx² + dy²) mod 2³²` avec différence 32 bits d'abord, sans toucher à `Find`.
5. **R-EL-1 se vérifie pour le vent 1** (FAIT [O]).
   - Harnais d'h4 tel quel (cases à zéro) : la frappe naît au statut 1, vie 42..80, première touche au tick 52.
   - Index écrit : statut 2 dès le tick de naissance, vie 42..79, **première touche au tick 51**.
   - Naissances (42, 52, … 102), libération de l'objet (129) et générateur sont inchangés.
   - Les clés d'h4 (`trace_summaries.txt`, `targets_inv0.txt`) ne servent donc pas. Les clés refaites sont versées (§2).
6. **Les clés avec cibles dépendent de trois réglages** : la borne de la requête (Q-H4B-2), le seuil du tri (Q-H4B-3), et la première frappe (nouvelle Q-H4E1-1).
   - Avec la borne exclusive du binaire, une cible seule à la plus haute case est **invisible**. Dans `wind1_one_near_bin_pf` au tick 42 : compte 1, 0 résultat, frappe aléatoire. La cible n'est visée qu'au tick 52, quand une frappe vivante occupe la case 2.
   - Un **leurre** (bloc non classe A né après les cibles) rend la borne sans effet. Prouvé par `out/boundcheck.txt` : 17 clés `_dec_` identiques ligne à ligne en borne inclusive.
   - Les clés `_fix_`/`_fixgate_` donnent le comportement de production si les défauts du plan sont gardés.
7. **Vecteurs.**
   - 800 vecteurs du vrai code (requête et tri réels), **0 écart avec mon modèle**, rejeu depuis une mémoire vide **800 sur 800**.
   - Couverture : pilote 218/218, garde 19/19, requête 94/94, tri 91/91. 45 mutants du modèle, tous tués.
   - Aucun gestionnaire natif, donc aucun vecteur de gestionnaire (E1 est déjà en ligne).
8. **Une seule vraie question nouvelle pour l'auteur** : la première frappe perdue (Q-H4E1-1, défaut proposé : corriger, 8 éclairs).

---

## 1. Faits du binaire

### 1.1 Le pilote tel que le code le fait

Preuves : [B] listing relu, [V] mon modèle `wind1_model.py` égale le vrai code sur 800 vecteurs.

Le wrapper `0x8003634C` appelle `0x80035EB0` pour l'objet `0x31` : table `0x800235F4` → `0x8003644C`, `a0 = [0x80127164]`, `jal 0x80035EB0`. Il fait ensuite `[0x8012715C]++` si ≤ `0x7FFFFFFE`, puis `[0x80127164] = 0` si le retour est non nul. Le compteur lu par le pilote est donc celui d'**avant** l'incrément : au tick t, avec le départ au tick T0 = 1, `c = t − T0 − 1`.

```
Drive(hero)                                  // c = compteur, last = [0x8012712C] (pointeur)
  phase 1 : -> P1
  phase 0 : last = 0 ; phase = 1 ; -> P1     // même appel, sans test d'animation
  phase 2 : return (anim != 0x34) ? 1 : 0    // sans garde
  autre   : return 0                         // phases négatives ou >= 3
P1:
  g = Garde(hero) ; if g != 0 return g       // 0x800351B8 : pente 4, ou anim hors {0x32, 0x33} : annulation 0x800350C0, rend 1
  if anim != 0x33 return 0                   // 0x80035F40 : PORTE (perd le compteur 30 : le héros est en 0x32 jusqu'au tick 32)
  if c < Table[0] return 0
  i = 0
  do { if Table[i] == c : Strike() ; i++ } while (c >= Table[i])      // i = premier indice dont le tick est > c
  if Table[i] == 0xFFFF : anim = 0x34 ; phase = 2                     // même appel que la frappe du compteur 100
  return 0
Strike():
  (P, K) = Requête(hero, masque 1 = classe A, type 11)               // a3 = 0x800CC058 (pointeurs), 5e argument 0x800CC158 (clés)
  cible = 0
  pour k = 0 .. n-1 : e = P[k]
     si e.PosZ > hero.PosZ + 0x500000 (signé)  : continuer           // Z : 80 px, strict
     si e == last                              : continuer           // pointeur
     si (int)K[k] < 0x6401                     : cible = e ; sortir  // 160 px : K <= 0x6400 passe
     sinon                                     : sortir              // D8 : un candidat trop loin arrête la recherche
  si cible :  r = Spawn(hero, page globale, 22, cible.X, cible.Y, cible.Z + 0x800000, direction 0)
              si r : r.ForceZ = 0xFFF80000 ;  last = cible          // last posé même si r est nul
  sinon    :  x = Below(0xC00001) ; y = Below(0x800001)              // deux tirages, X d'abord, AVANT la naissance
              r = Spawn(hero, 22, hero.X + x - 0x600000, hero.Y + y - 0x400000, hero.Z + 0x800000, 0)
              si r : si r.IsOnGround != 0 : DestroyNoFx(r)  sinon r.ForceZ = 0xFFF80000
              last = 0                                               // dans tous les cas
```

- Table `0x800235E0` (u16) = 30, 40, 50, 60, 70, 80, 90, 100, `0xFFFF` ([B] `out/facts_check.txt`).
- `Below(n)` de la DLL (`Alundra/Ai/EnemyHelpers.cs:35`, `(uint)Next() × n >> 32`) est exactement le motif `multu/mfhi` du binaire. `0xC00001` vaut 192 px + 1 unité, `0x800001` vaut 128 px + 1.
- La re-lecture capstone du pilote donne 218 instructions identiques au listing d'h4 (`out/dis_80035EB0.txt`).

### 1.2 Faits porteurs (le `check.md` prime)

| # | Fait | Source |
|---|---|---|
| F1 | Pilote `0x80035EB0` ; table des compteurs ; requête `(héros, 1, 11)` | `h4/report.md:167` ; `check.md:60` (CONFIRMÉ) ; [B] |
| F2 | Première frappe (compteur 30, tick 32) perdue : le héros est en `0x32` aux ticks 1 à 32, `0x33` dès le 33. Sept éclairs aux ticks 42, 52, … 102 | `h4/report.md:33`, `:358` ; `check.md:62` ; [O] `wind1_cast_pf` |
| F3 | D8 : le premier candidat non sauté dont la clé est ≥ `0x6401` arrête la recherche (branche aléatoire) ; la décompilation continue. Ne diffère qu'avec moins de 3 résultats (non triés, ordre des cases) | `h4/report.md:302` ; `check.md:81-82` ; [O] `wind1_d8_far_first_dec_pf` |
| F4 | D9 : le sol n'est testé qu'en branche aléatoire, **et ce test est mort** (§0.3) | `h4/report.md:303` ; `check.md:81-82` ; [B] seul écrivain `0x8003810C` |
| F5 | Portée de Q2 pour le vent 1 : l'appelant est le héros (case 0). Seule l'entité de la plus haute case est ignorée, pas « un ennemi seul » | `check.md:124-126` ; [O] `wind1_one_near_bin_pf` |
| F6 | Requête : cases `1..compte−1` (exclusive), statuts 2 et 3, `+0x20 == 0`, `(+0x6C & 1) != 0`, `+0x1D0 == 0`, `+0x1CC == 0`, `+0xB4 & 0x40 == 0`, fiche `+0x1C4` présente, réponse `octet[fiche+2+11] & 0xC0 != 0x80`. Clé `dx²+dy²` (`>>16` signé après différence 32 bits). Tri de Hoare dès 3 résultats | `h4/report.md:212-223` ; `e20-h4b/critic/check.md` §4 ; [B] `out/dis_8003AF70_query.txt` ; [V] |
| F7 | Le masque `1` est le bit `ClassA` du corps (`Alundra/Entities/EntityFlags.cs:43`). Le type 11 lit l'octet 13 de la fiche = `DamageResponses[10]` (`Alundra/Combat/BalanceRecord.cs:76`). La fiche 25 (blocs de test) répond 16 : elle passe | [D] ; `BALANCE.BIN.json` |
| F8 | Le sprite 22 n'est **pas** candidat : drapeaux `0x2144` = `HitsClassA`, `DeactivateOnAnimationEnd`, `Gravity`, `NoObstacleSlide`, sans bit classe A ni `Collidable`. Attaque type 11, attribut 11, puissance 32. Il ne touche jamais le héros (PV du héros constants dans les 42 clés) | `sprite-records.json` ; fiche 22 ; [O] |
| F9 | Sprite 22 : animation de 37 ticks (4 images de 2, une de 2, 9 de 3, une terminale à 0), fin `Hold`. Boîte de coup 36×24×31 (décalage −18, −12, 0) aux images 4 à 13 (ticks d'animation 8 à 36). Vitesse 0, `Acceleration` 80 (donc `HitAnimByte` 80, `HitPass.cs:108`). Corps 7×5×8 (décalage −3, −2, 0). Ni son ni effet attaché | `map_alundra.json` (`out/sprite22_dump.txt`) ; [O] aucun son |
| F10 | Vie d'une frappe sur le plat, née au tick S : statut 2 dès la fin de S. Chute de 128 px à −8 px/tick + gravité 0,5 px/tick (Z binaire 7 831 554, 7 241 730, …). Atterrissage à S+11 (Z 1, force 0). Boîte non nulle dès S+7. Fin d'animation (`+0xAC`) à S+36, statut 4 à S+37, case libre à S+38 | [O] `wind1_cast_pf` (S = 42 : vue 42..79, libre à 80) |
| F11 | Contre une cible fixe : première touche à S+9 (le Z de la frappe passe sous 32 px). 3 étincelles (effet 9), **9 tirages**, `+0x1D0 = 25`. Retouche 25 ticks plus tard tant que la boîte vit (S+34). La cible touchée sort de la requête 25 ticks | [O] `wind1_one_near_dec_pf` (touches 51, 76, 101) |
| F12 | Fin de l'objet : `0x34` au tick 102, animation 0 au tick 128, objet libéré au **tick 129**. PM 4 → 3. Aucun son. Deux tirages par frappe aléatoire, neuf par touche | [O] ; `out/replay_trace_cast_31.txt` (sortie d'h4 rejouée à l'identique) |
| F13 | L'annulation de `0x31` est neutre : `0x800351A0` (`[0x80127164] = 0`). Les éclairs nés continuent. La phase 2 n'a pas de garde : une blessure en phase 2 libère l'objet au tick même | [B] ; [O] `wind1_hurt60_pf`, `wind1_hurt103_pf` |
| F14 | Le compteur s'incrémente aussi au tick de l'annulation (101 après le tick 102 de `wind1_hurt102_pf`) | [O] ; wrapper `0x80036480-0x800364A8` |
| F15 | Une naissance hors grille est ramenée au bord au premier pas : X → 3,0 px, Y → 2,0 px, `ForceAdjusted` 1 | [O] `wind1_edge_west_x20_pf`, `wind1_edge_north_y10_pf` |
| F16 | Table des 64 cases pleine : `SpawnWarpEntity` rend 0, les tirages sont faits quand même (2 par frappe), l'objet se libère au 129 | [O] `wind1_full_table_pf` |
| F17 | La dernière cible est un pointeur : une entité née dans la même case est sautée comme elle | [O] `wind1_slot_reuse_dec_pf` (tick 52 : A' en case 1 sauté, B visé) contre `wind1_slot_control_dec_pf` |
| F18 | Le pilote n'est pas appelé quand `hero+0x20 != 0` ni porte fermée (`& 0x48`) : le compteur ne bouge pas ces ticks-là | `h4/check.md:163` ; [D] `PlayerManager.cs:321-328` |
| F19 | La direction du héros ne compte pas (positions et générateur identiques est/sud) | [O] `out/compare.txt` (a) |

**Précision sur h4** : `report.md:167` (« ne s'exécute que si l'anim est `0x33` ») est exact pour la table. La phase 0 n'a pas ce test, la garde passe d'abord, et la fin de la table est posée dans l'appel de la dernière frappe.

**Écarts avec le plan** :
- plan:8127 (« pilote, requête ») ne dit ni la clé, ni le pointeur de la dernière cible, ni la mort de D9.
- Les statistiques de mutants d'h4 ne sont pas citées (`check.md:128`) ; les 45 mutants ci-dessous sont les miens.

---

## 2. Les clés d'oracle refaites

### 2.1 Machine

`wind1_key.py` est une copie généralisée de `e20-h4d1/critic/c/h4o/fire2_key_c.py`, sur `h4o/` (le harnais d'h4 tel que la critique d'h4d1 l'a corrigé). Aucun code de l'émulateur n'est changé. Ajouts :
1. **Index écrit** avant le tick 1 : `+0 = i` et `+0x48 = −1` sur les cases 1 à 63 (la 0 est le héros), et `+0x48 = −1` sur le gabarit `0x80134368` (`0x8003B24C-0x8003B2D8`, `0x8003B2BC`, relu par `out/facts_check.txt`).
2. **Pas par image `_pf`** : `s ← s × 0x7D2B89DD + 0xE06A02E7` après l'enregistrement de `rng1` du tick et avant la lecture de `rng0` du suivant. Vérifié sur toute clé : `rng0(t+1) = pas(rng1(t))`.
3. **Observateurs** (aucune instruction remplacée) : retour de la requête (`0x80035FAC`), choix (`0x80036028`), entrée et retour de `SpawnWarpEntity` du pilote, destruction sans effet (`0x80036158`).
4. **Poses de scénario** : héros, direction, blocs `alundra_25` posés classe A comme `trace_targets.py`, leurre non classe A, Z d'une cible, mort puis renaissance dans la même case, coup du tick T, champ rehaussé.

Banc R-EL-4 : champ plat 52×60, héros en (100, 100) tourné à l'est (direction 24), graine `0x12345678` au tick 1, aucune entité de carte, Rond au tick 1, un tick par image, objet 0x31, PM 4/4.

Le harnais copié rejoue d'abord la sortie archivée d'h4 : `trace_cast.py 0x31` est identique (`out/replay_trace_cast_31.txt`).

**Z.** Les clés sont en repère binaire. La DLL a un Z d'une unité brute de moins (ADR-0026, D-E0A-1) : `Z_DLL = Z_clé − 1`, et la DLL ajoute 1 à l'argument de naissance.

### 2.2 Les 42 clés (`keys/`, 84 fichiers, 14 Mo, `MANIFEST.sha1` identique sur deux exécutions complètes)

| Famille | Clés | Ce qu'elles fixent |
|---|---|---|
| Lancer sans cible (4) | `wind1_cast_pf`, `_south`, `_gateoff` (what-if Q-H4E1-1, 8 éclairs), `_full_table_pf` | naissances, Z, générateur, vie, libération 129 ; direction sans effet ; table pleine |
| Interruptions R-EL-6 (8) | `wind1_hurt{20,31,33,41,42,60,102,103}_pf` | annulation au tick T par la garde, plus de naissance ensuite, éclairs déjà nés qui continuent, fin de phase 2 sans garde (103), compteur +1 au tick d'annulation |
| Bords et relief (3) | `wind1_edge_west_x20_pf`, `_north_y10_pf`, `wind1_plateau_h64_pf` | F15 ; atterrissage sur 64 px (5 éclairs sur 7 à S+6, Z 4 194 305) |
| Une cible (140,100) (5) | `wind1_one_near_{bin,dec,fix,fixgate}_pf`, `_dec_gateoff_pf` | F11, F5, effet de chaque réglage |
| Six cibles de h4 `targets_inv0` (5) | `wind1_six_{dec,bin,fix,fixgate}_pf`, `_dec_gateoff_pf` | ordre du tri de Hoare sur quatre égalités à 1600 ; une cible différente à chaque frappe |
| D8 et tri (5) | `wind1_d8_far_first_dec_pf`, `_near_first_dec_pf`, `_three_sorted_dec_pf`, `_far_first_fix_pf`, `_far_first_fixgate_pf` | lointain en tête sans tri : branche aléatoire (D8) ; avec tri dès 2, la proche d'abord |
| Seuil 160 px (8) | `wind1_edge_{east,south}{160,161}_dec_pf`, `_east160_frac`, `_east161_frac`, `_west160_h200`, `_west161_h200` | clé 25600 passe, 25601 arrête ; fraction de pixel ; signe |
| Porte du Z (2) | `wind1_zgate_{pass,skip}_dec_pf` | `héros.Z + 0x500000` passe, `+ 1` saute |
| Dernière cible (2) | `wind1_slot_reuse_dec_pf`, `wind1_slot_control_dec_pf` | F17 |

Suffixes :
- `_pf` : pas par image. `_dec` : leurre. `_bin` : requête du binaire sans leurre.
- `_fix` : borne inclusive + tri dès 2. Le compte est relevé d'une unité pendant l'appel et l'immédiat de `0x8003B0B4` est mis à 2.
- `_gateoff`/`_fixgate` : porte `0x33` retirée (`0x80035F40` mis à `nop`).
- Les clés `_fix` et `_gateoff` sont **dérivées, non fidèles à l'octet**.

### 2.3 Valeurs porteuses (`out/expected_tables.txt`)

X, Y bruts 16.16 ; argument Z = Z héros brut 1 + `0x800000` = 8 388 609.

`wind1_cast_pf` (héros au repos, graine au tick 1) :

| tick | c | case née | X brut (px) | Y brut (px) | tirages | `rng0 → rng1 → rng2` |
|---|---|---|---|---|---|---|
| 42 | 40 | 1 | 2 673 096 (40,7882) | 9 490 330 (144,8109) | 2 | `0x2f9d6da7 → 0xd99f3371 → 0xc25be474` |
| 52 | 50 | 2 | 4 350 866 (66,3889) | 3 202 181 (48,8614) | 2 | `0xfcbcb593 → 0x19b90bbd → 0x5e304a10` |
| 62 | 60 | 3 | 3 633 126 (55,4371) | 3 123 578 (47,6620) | 2 | `0xc55964bf → 0x1752f549 → 0x408ed3ec` |
| 72 | 70 | 4 | 9 554 625 (145,7920) | 3 500 734 (53,4170) | 2 | `0x9729a72b → 0x22d57c15 → 0x4aa45e08` |
| 82 | 80 | 1 | 2 257 614 (34,4485) | 7 367 478 (112,4188) | 2 | `0x1286e8d7 → 0x98d66c21 → 0xd7ee0464` |
| 92 | 90 | 2 | 3 438 189 (52,4626) | 4 682 344 (71,4469) | 2 | `0x9ad9d5c3 → 0x46e4d16d → 0xee552300` |
| 102 | 100 | 3 | 11 243 690 (171,5651) | 5 294 716 (80,7910) | 2 | `0xa64659ef → 0x5994f7f9 → 0x2a8b55dc` |

- `wind1_cast_pf_gateoff` : 8 éclairs aux ticks 32, 42, … 102. Le premier est en (92,7608 ; 112,5488) au tick 32, le héros étant encore en `0x32`. Objet libéré au 129 comme dans le fidèle.
- `wind1_one_near_fixgate_pf` (production par défaut des trois questions) : tick 32 cible case 1 (clé 1600), touches aux ticks 41, 66, 101, 126.
- `wind1_six_fix_pf` : cibles successives 5, 4, 2, 1, 6, 3, 5. Au premier tick, l'ordre trié de la requête est [5, 4, 1, 2, 6, 3], et non l'ordre des cases.

### 2.4 Quelles clés pour quels réglages (à poser dans le test)

| Clés | `StrikeDuringPose` | `InclusiveBound` | `SortThreshold` | Valable |
|---|---|---|---|---|
| sans cible (`cast`, `hurt*`, `edge_west_x20`, `edge_north_y10`, `plateau`, `full_table`) | non | indifférent | indifférent | toujours |
| `*_dec_pf` (hors `gateoff`) | non | oui | 3 | toujours (leurre ; `out/boundcheck.txt`) |
| `*_dec_gateoff_pf`, `cast_pf_gateoff` | oui | oui | 3 | toujours |
| `*_fix_pf` | non | oui | 2 | défauts Q-H4B-2/3 |
| `*_fixgate_pf` | oui | oui | 2 | **défauts des trois questions = production** |
| `*_bin_pf` | non | non | 3 | seulement si Q-H4B-2 = reproduire (champ stocké h4b1b) |

`InclusiveBound` et `SortThreshold` sont les réglages de D-H4B1-2. `StrikeDuringPose` est celui que propose h4e1 (nom libre, à passer à `check_names.py`).

### 2.5 Déterminisme

- Deux exécutions complètes des 42 scénarios donnent le même `MANIFEST.sha1` (84 fichiers : `out/MANIFEST_run1.sha1` = `out/MANIFEST_run2.sha1`).
- `FAITHFUL=0` redonne les clés d'h4 (`stale/`) : vie de la frappe 42..80 au lieu de 42..79, première touche 52 au lieu de 51 (`out/compare.txt` c et d).

---

## 3. Les vecteurs

### 3.1 Génération et résultats

`wind1_vec.py 800 20261018` produit `vec/vec_wind1_80035EB0.json`, au format de `e20-h4b/bin/vec/README.txt`. Son SHA-1 est identique sur deux générations.
- Vrai code : pilote, garde `0x800351B8`, dispatcheur d'annulation `0x800350C0` (objets 0x31, 0x2B et 0 seulement), requête `0x8003AF70`, tri `0x8003AE04`.
- Stubbés et journalisés : `SpawnWarpEntity` (rend `stub_spawn_ret`, qui porte le mot `+0x144`) et `DestroyEntityNoFx`.
- Entrées aléatoires biaisées (70 % des vecteurs atteignent une frappe) : phase −1 à 100 ; compteur sur les entrées de la table ±1 ; animation ; pente ; 0 à 8 entités (statuts, `+0x20`, drapeaux, `+0x1D0`, `+0x1CC`, `+0xB4`, fiche, octet de réponse) ; distances de 160/161 px, fractions, grandes distances, coin −32 768 px ; Z autour de `héros + 0x500000` ; compte de n−1 à n+2 ; dernière cible ; retour de naissance nul ou non ; `IsOnGround` ; graine.

Résultats (`vec/vec_wind1_gen.txt`, `vec_wind1_replay.txt`) :
- **0 écart** avec le modèle sur le retour, la phase, l'animation, l'objet, la dernière cible, le générateur, les arguments de naissance, le `ForceZ` écrit, les destructions, et les tableaux de pointeurs et de clés.
- **Rejeu depuis une mémoire vide : 800 sur 800** (23 524 mots lus, 3 512 écrits).
- Couverture : pilote 218/218, garde 19/19, requête 94/94, tri 91/91, dispatcheur d'annulation 22/62 (les autres objets relèvent de h0a).
- Répartition : 171 frappes sur cible, 352 aléatoires, 55 destructions de la branche morte, 149 naissances refusées, 88 passages à la phase 2, 173 requêtes à ≥ 3 résultats, 95 à exactement 2.

Limites :
- Un compteur ≥ `0xFFFF` fait lire la table au-delà de la sentinelle. Comme pour h4c1, c'est inatteignable et non épinglé.
- Les vecteurs sont en **mode binaire** (borne exclusive, tri dès 3, compte = mot `0x80134360` du vecteur). Il n'y a pas de vecteurs « corrigés » du pilote ; les clés `_fix` couvrent le mode corrigé de bout en bout.

### 3.2 Table de correspondance (T0 (b) : octets lus → membres de la DLL)

| Classe lue | Membre de la DLL (HEAD) |
|---|---|
| entité k ≥ 1 `+0x10` / `+0x20` / `+0x6C` | `Status` (`EntityScriptProxy.cs:88`, `IsActive()` `EntityStatusExtensions.cs:11`) / `BlockedByEntity` (`:99`, tout mot non nul = une entité quelconque) / `Flags` (`:156`) |
| `+0xB4` / `+0x1CC` / `+0x1D0` | `HitAnimByte` (`EntityScriptProxy.HitPass.cs:30`) / `DamagedTickCounter` (`:483`) / `FrameCollisionTickCounter` (`:484`) |
| `+0x114` `+0x118` `+0x11C` | `PosX` `PosY` `PosZ` (`:351-353`) |
| `+0x1C4` et l'octet `fiche+13` | un `BalanceRecord` non nul dont `ByteAt(13)` rend l'octet du vecteur (`BalanceRecord.cs:76`) ; mot nul = pas de fiche |
| héros `+0x88` / `+0x18C` / `+0x114..11C` | `TargetAnimationId` (`:158`) / `Slope_18c` (`:474`) / `PosX..PosZ` |
| naissance : lecture `+0x144`, écriture `+0xB8` | `IsOnGround` (`:417`) / `ForceZ` (`:338`) du proxy rendu par l'hôte de test |
| `0x8012712C` | nouveau champ « Hero only » : case de la dernière cible (D-H4E1-2) |
| `0x8012715C` `0x80127160` `0x80127164` | `ItemUseCounter`, `ItemUsePhase`, `ItemInUse` (D-H-1 de h0a) |
| `0x80134360` | le paramètre `compte` |
| `0x80098708` | `GameRandom.RandomSeed` |
| `0x800235E0` | `WindTome1Ai.Table` |
| `0x800CC058` `0x800CC158` | sorties de la requête, non modélisées (le pilote lit la clé par `DistanceKey`) |

Aucune lecture n'est sans membre : la règle d'arrêt de D-H4B1-3 ne se déclenche pas. L'adaptateur est celui de h4b1 (image → proxies), étendu au héros et aux mots du pilote.

### 3.3 Mutants du modèle (`out/mutants.txt`)

Ils sont rejoués sur les mêmes états que les vecteurs. Le modèle non muté égale le vrai code, donc un mutant qui en diffère est tué par le vecteur concerné. **45 mutants, 45 tués.**
- **Pilote** :
  - porte `0x33` retirée ; garde qui accepte `0x34`, sans pente ou sans annulation ;
  - Z à 60 px, porte du Z retirée, `>=` au lieu de `>` ;
  - dernière cible ignorée, jamais posée, jamais effacée en aléatoire, non effacée en phase 0, posée seulement si la naissance réussit ;
  - seuil `<=` ; seuil non signé (tué par 22 vecteurs du coin −32 768 px) ; D8 absent ;
  - Z de la cible +64 px ; X/Y échangés ; direction 1 ; `ForceZ` −4 px ; test du sol aussi en branche cible ;
  - un seul tirage ; ordre des tirages ; échelles ou décalages permutés ; Z aléatoire +64 px ; test du sol retiré ;
  - frappe à chaque tick ; fin au 101 ; fin sans phase 2 ; phase 2 gardée ou inversée.
- **Requête** : borne inclusive ; tri dès 2 ; tri stable ; masque 2 ; type 10 ; statut 2 seul ; statuts 1 à 3 ; chacun des filtres `+0x1D0`, `+0x1CC`, `+0xB4`, fiche absente, réponse `0xC0` ; pivot signé ; échange sur `i <= j` (tué par un seul vecteur).
- **Équivalent déclaré** : clé en 64 bits. La clé ne dépasse jamais 2³¹ avec une différence 32 bits.

---

## 4. Atterrissage dans la DLL (HEAD `ed60a0eb`)

### 4.1 Où va quoi

| Pièce | Où | État |
|---|---|---|
| Pilote `Drive(hero, item)`, table, annulation neutre `Cancel(hero, item)`, réglage `StrikeDuringPose` | `Alundra/Ai/Items/WindTome1Ai.cs` (neuf ; le dossier `Items/` naît en h4c1) | absent |
| Requête et tri | `Alundra/Combat/TargetQuery.cs` (neuf, h4b1) ; h4e1 y ajoute `DistanceKey` | absent (prérequis) |
| Dernière cible (case, 0 = aucune) | `Alundra/Entities/EntityScriptProxy.cs`, zone « Hero only » (`:429-452`, près de `WeaponRecordPointer` `:434`), remise à 0 à la reconstruction du héros | absent |
| Entrées de l'objet 0x31 : départ (celui de h4a), pilote `Drive`, annulation `Cancel` | tables de h0a dans `Alundra/Hero/PlayerManager.cs`. Aujourd'hui : `TryUseItem` bouchon `:932`, pilote vide `:1507-1509` (appelé en `:353`), `CancelItemMode` vide `:740-742`. Lignes à relire à l'exécution : h0a les déplace | bouchons |
| Registre | **rien** : `NativeSpriteRegistry.CreateForProduction` `:36-43` (7 clés) inchangé ; E1 en ligne `EventProgramRunner.cs:283-292` | sans objet |
| Garde de pente | celle de h4a, appelée une fois par appel | prérequis |
| Naissance | `IEntityWorldContext.SpawnEntityBySpriteIndex` (`Alundra/Maps/IEntityWorldContext.cs:81`), `WorldProxy.cs:3488-3598` (Z+1 `:3549`, case libre la plus basse `EntitySlotTable.Allocate` `:28`, `JoinsBirthTick`/`HasLogicalZ` `:3572-3574`) ; modèle d'appel `PlayerManager.cs:1218` | existe |
| Destruction sans effet (branche morte) | `IEntityWorldContext.DestroyEntity(entity)` `:90` (`WorldProxy.cs:3329`) | existe |
| Tirages | `EnemyHelpers.Below` `EnemyHelpers.cs:35` ; pas par image `GameRandom.AdvanceFrame` après les effets (`WorldProxy.cs:2692`, `:3035`) | existe |
| Passe de coups et étincelles | `Alundra/Combat/HitPass.cs:122-131` (filtres), `NativeEffects.CreateHitSparks` | existe |
| Banc | `ArcRun` (`AlundraArcSupport.cs:94`, `Give` `:151`, `SetMp` `:164`, `SetItem` `:171`, `OneFrame` `:430`, `Press` `:454`) ; champ plat par réflexion (`AlundraMapEdgeWorldTests.cs:43-50`) ; `FlatCells.Create(width, height, cell, groundProperty, slope)` (`AlundraJumpTestSupport.cs:457`) ; retrait des entités de carte `Recycle` (`AlundraLootRollWorldTests.cs:74`) | existe |

Le tick (INFÉRENCE appuyée par ADR-0065) :
- Le pilote tourne dans `MovePlayer`, premier du bloc (`WorldProxy.cs:2905-2935`), et l'éclair né y prend part au tick de sa naissance.
- `SpawnedEntities` est dans l'ordre des cases. Le balayage de la requête se fait par `Slot`, jamais par l'indice de la liste (plan:8238).
- `EntitySupport.IsEligibleSubject` exige `Collidable` (`EntitySupport.cs:73-76`). Le sprite 22 n'en a pas : il se pose sur le terrain, pas sur le bloc cible. FAIT [D], et cohérent avec l'oracle.

### 4.2 Décisions proposées

- **D-H4E1-1** : un fichier neuf `WindTome1Ai.cs` avec `Table` (u16[9]), `Drive`, `Cancel` (corps vide nommé) et `StrikeDuringPose`. **Pas de `Register`.**
- **D-H4E1-2** : la dernière cible est le **numéro de case**.
  - 0 = aucune. La case 0 est le héros, jamais candidat.
  - Elle est posée même si la naissance rend `null`, et effacée en phase 0 et à chaque branche aléatoire.
  - Elle est comparée par `Slot`, pas par référence (F17).
- **D-H4E1-3** : `TargetQuery.DistanceKey(appelant, entité)` additif (§0.4). Le pilote l'utilise pour le test signé `(int)clé < 0x6401`.
- **D-H4E1-4** : les tirages par `EnemyHelpers.Below(0xC00001)` puis `Below(0x800001)`, dans deux instructions séparées, X d'abord, avant la naissance.
- **D-H4E1-5** : la branche `IsOnGround` est portée littéralement (`if (r.IsOnGround != 0) DestroyEntity(r)`). Elle est testée par l'hôte de test des vecteurs et documentée « inatteignable en jeu ».
- **D-H4E1-6** : le `compte` passé à `Find` vaut `TargetQuery.AllSlots` (63) en production, la borne inclusive n'excluant rien. Le test (d1) passe celui du vecteur. Aucun accès à `HighestLiveSlot` (`WorldProxy.cs:182`, privé).

### 4.3 Enregistrements de l'export

- **`alundra_22`** :
  - `MoreFlags` 68, `CanPickup` 33, `FlagsPortraitShadowType` 0, soit des drapeaux `0x2144`.
  - Programmes A0 C0 D0 E1 F0 ; décalage (−3, −2, 0), taille 7×5×8 ; `BreakEffect` 0, `Contents` 0.
  - Une `AnimSet` (`Speed` 0, `Acceleration` 80, `IsZForceApplied` 0, `Sfx` 0) ; `IdsvAnimDirs` : animation 0, quatre directions, 14 images, fin `Hold`.
  - Boîte et délais : `map_alundra.json` (F9).
- **`alundra_25`** (cibles de banc) :
  - 128 / 96 / 0, soit `0x6080` (`Collidable`, `NoObstacleSlide`, `NoRiders`), 24×16×32.
  - Fiche 25 : toutes réponses 16, aucune attaque.
- Prefab : `Entities/Magie de vent Niv.1/` (anim0 down/left/right/up).

### 4.4 Tests existants qui bougent

**Aucun** (liste prédite vide, à recompter au HEAD de départ).
- Les deux épingles du registre ne bougent pas (aucune clé).
- `AlundraBirthBoxTests.cs:131` fait naître tous les préfabs globaux sans tick.
- Aucun test ne presse le Rond au niveau d'un monde (`state.md` §4.4) : l'entrée 0x31 de la phase B ne déplace rien.
- `UnhandledWeaponSpriteCodes` et `ProductionWiredWeaponTypes` (`PlayerManager.cs:1226`) ne sont pas concernés (les sorts ne passent pas par le tir).
- Conditionnel : les épingles de la liste des préréglages (`AlundraTestSavesTests.cs:88-91`, `:429-433`) bougent seulement si la recette ajoute un préréglage ou un objet du Rond (même choix que Q-H4C1-4).
- La recette utilise `arsenal-a-m14` : son kit contient le parchemin 49 sans le 50 (`TestSaves.cs:72-75`, `:124`).

**Si E3 est étendu à {16, 23} (h4d2, pas h4e1)** :
- Les deux épingles ne bougent pas pour cela : le nombre de clés ne change pas. h4d2 ajoute (C, 63), soit +1.
- Dans h4c1 changent (e2) (« `(E, 3)` pour le type 16 seul, pas 17 ni 23 », plan:8416) et le mutant « E3 inscrit pour {16, 23} » (plan:8433), qui devient l'état de production.
- (p3) et (p4) de h4c1 ne bougent pas.

### 4.5 Sondes de T0

Les prédictions sont écrites avant ; une sonde contredite est un arrêt.
- **P1** : Z d'une frappe sur le plat = `Z_clé − 1` à chaque tick (7 831 553, 7 241 729, …). Atterrissage à S+11, Z 0, force 0.
- **P2** : première touche à S+9 sur `one_near_dec`, 9 tirages, `+0x1D0` 25, retouche à S+34.
- **P3** : `ForceResetAnimationFlag` à S+36, statut 4 à S+37, case rendue au recyclage de S+38 (la case 1 est reprise au tick 82).
- **P4** : statut 2 et position constante à la fin du tick de naissance.
- **P5** : naissance hors grille : X = 3,0 px, Y = 2,0 px, `ForceAdjusted` 1. `ClampStepToMapEdge` (`ScriptedMotion.cs:741`) borne aussi un pas nul.
- **P6** : atterrissage sur le relief (`plateau_h64`) : Z 64 px − 1 unité, à S+6 pour les cinq frappes concernées.
- **P7** : `IsOnGround` d'un nouveau-né vaut 0 au retour de `SpawnEntityBySpriteIndex`.
- **P8** : l'éclair se pose au sol (Z 0), pas sur le bloc cible.
- **Générateur** : après chaque image, l'état de la DLL égale `rng0(t+1)` de la clé. Compter 2 pas par frappe aléatoire, 9 par touche, plus 1.

---

## 5. Tâches proposées (E20.h4e1, vent 1)

**Prérequis.**
- h0a et h4a (les trois coutures, la fonction de départ des tomes, les états `0x32`-`0x34`, la garde).
- h4b1 (requête).
- La fin du chantier de restructuration et Q-R-7.
- Les défauts retenus de Q-H4B-2, Q-H4B-3 et Q-H4E1-1. Si l'auteur répond autrement, §2.4 dit quelles clés servent.
- **Ni f4, f5, f6a, h4c1, h4c2, E14.b0.**

**But.** Comme l'original :
- 1 PM et 32 ticks de pose.
- Un éclair tous les 10 ticks depuis le tick 42 : sept dans l'original, huit avec la correction (le premier au tick 32).
- Chaque éclair tombe de 128 px sur l'ennemi de classe A le plus proche s'il est à moins de 160 px (jamais deux fois de suite sur le même, jamais plus haut que 80 px au-dessus d'Alundra). Sinon il tombe au hasard dans un rectangle de 192×128 px centré sur le héros.
- Chaque éclair blesse (type 11, puissance 32) pendant 30 ticks, une fois tous les 25 ticks par cible.
- Le héros est figé jusqu'au tick 129.

**Périmètre.**
- Dans le périmètre : `Alundra/Ai/Items/WindTome1Ai.cs` (neuf) ; `Alundra/Combat/TargetQuery.cs` (`DistanceKey`) ; `Alundra/Entities/EntityScriptProxy.cs` (un champ) ; `Alundra/Hero/PlayerManager.cs` (entrées `0x31`) ; `Alundra.Tests/` ; l'annexe `docs/plan-e20-annexe/e20-h4e1/` ; ce plan, l'index des ADR et un ADR.
- Hors périmètre :
  - le départ, les PM, la piste XA, les états et la garde (h4a) ;
  - la requête (h4b1) et le champ stocké (h4b1b) ;
  - les autres tomes ;
  - la blessure du héros et le butin ;
  - `NativeSpriteRegistry`.

**Phases.** A est inerte en production : les entrées `0x31` restent des bouchons et les tests de monde passent par les trois coutures de R-EL-4. B câble les entrées de production.

- ⏳ **T0 — Annexe, oracle, liste prédite** (aucun code).
  - (a) Rejouer depuis des copies, sorties identiques : `wind1_key.py` (deux exécutions, `MANIFEST.sha1`), `trace_cast.py 0x31` (h4), `wind1_vec.py 800 20261018` (0 écart ; 218/218, 19/19, 94/94, 91/91), `wind1_replay.py` (800/800), `wind1_mut.py` (45/45), `wind1_boundcheck.py`.
  - (b) Verser clés, vecteurs, scripts et la table de correspondance de §3.2 (un octet sans membre arrête).
  - (c) Les sondes P1 à P8.
  - (d) La liste prédite (vide), recomptée.
  - (e) Vérifier que `TargetQuery`, `WindTome1Ai` et le champ passent `check_names.py`.
- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette).
  - R-EL-4 : `ItemStartForTests` = la fonction de départ des tomes de h4a, `ItemDriverForTests` = `WindTome1Ai.Drive`, `ItemCancelForTests` = `WindTome1Ai.Cancel`.
  - Chaque « sans effet » est accolé à un positif.
  - **(t1)** la table `u16` contre l'exécutable (9 mots, sentinelle).
  - **(d1)** le pilote contre les 800 vecteurs en **un seul test**.
    - Adaptateur de h4b1 étendu ; réglages `InclusiveBound` faux et `SortThreshold` 3 pendant le test.
    - L'hôte de test rend `stub_spawn_ret` ou `null` avec l'`IsOnGround` du vecteur, et journalise naissances et destructions.
    - Il compare retour, phase, animation, objet, dernière cible, générateur, arguments de naissance, `ForceZ` et destructions.
    - Y figurent les 55 vecteurs de la branche morte et ceux qui distinguent le seuil signé du seuil non signé (coin −32 768 px).
  - **(d2)** naissance nulle. Cible : dernière cible posée, `ForceZ` non écrit. Hasard : 2 tirages, dernière cible effacée. Aucune exception.
  - **(d3)** la garde : pente 4 ou animation hors `0x32`/`0x33` donne annulation neutre, objet libéré, retour 1, rien d'autre. Animation `0x32` en mode fidèle : 0 sans requête, accolé à `0x33` qui interroge.
  - **(d4)** phase 2 : 0 tant que `0x34`, 1 sinon, **sans garde**. Accolé : une pente 4 en phase 2 ne libère pas.
  - **(d5)** phase 0 : efface la dernière cible, passe en phase 1 et frappe dans le même appel (compteur 40, animation `0x33`).
  - **(d6)** la fin : au compteur 100, `0x34` et phase 2 dans le même appel que la frappe. Un compteur 101 sans passage par 100 pose `0x34` sans frappe. Accolés aux compteurs 99 et 31.
  - **(d7)** bornes :
    - (d7a) clé 25600 passe, 25601 arrête, dans les quatre sens et avec fractions ;
    - (d7b) Z `héros + 0x500000` passe, `+1` saute et la recherche continue ;
    - (d7c) D8 : un lointain en tête arrête la recherche (mode binaire, moins de 3 résultats), accolé au proche en tête ;
    - (d7d) dernière cible sautée par numéro de case, et une entité née dans la même case aussi.
  - **(q1)** `DistanceKey` égale la clé qu'écrit la requête de h4b1 (vecteurs de h4b).
  - **(e1)** aucune clé : le compte de `CreateForProduction()` est inchangé par la tranche (dérivé du HEAD de départ, 7 aujourd'hui) et une frappe née meurt à S+37 par E1 en ligne.
  - **(c1)** l'annulation neutre laisse les frappes et libère l'objet.
  - Tests de monde (banc R-EL-4, sans tolérance ; R-EL-5 sans objet, aucun mur) :
    - **(w1)** `wind1_cast_pf` : 7 naissances (ticks, cases, X, Y, Z), générateur à chaque tick, vie de chaque éclair S..S+37, objet libéré au 129, `0x34` au tick 102, PM 3, réglage fidèle.
    - **(w1b)** `wind1_cast_pf_gateoff` : 8 éclairs, le premier au tick 32.
    - **(w2)** `_south` identique.
    - **(w3)** les huit interruptions R-EL-6 : objet, phase, compteur, éclairs, générateur. Le héros après T et le son 23 ne sont jamais comparés.
    - **(w4)** `edge_west_x20`, `edge_north_y10`, `plateau_h64` (P5, P6).
    - **(w5)** `one_near_dec` (touches 51, 76, 101 ; 9 tirages ; `+0x1D0` 25), `one_near_fix` et `one_near_fixgate` (réglages de production).
    - **(w6)** `six_dec`, `six_fix`, `six_fixgate` (ordre exact du tri sur les égalités).
    - **(w7)** les cinq clés `d8_*`, les huit `edge_*160/161`, les deux `zgate_*`.
    - **(w8)** `slot_reuse` accolé à `slot_control`.
    - **(w9)** à 2 et 3 ticks par image : seulement les ticks de naissance, de fin (S+36), la libération, le nombre de tirages (2 par frappe aléatoire, 9 par touche) et le nombre d'éclairs, jamais leurs positions (écart d'ADR-0066).
  - Les clés `_bin_` ne sont pas comparées sans h4b1b.
- ⏳ **T2 — Phase B et tests de production, rouges avant.**
  - **(p1)** Rond avec le seul tome 49, PM 4/4 : `wind1_cast_pf` (ou `_gateoff` selon Q-H4E1-1) par le chemin de production, PM 3.
  - **(p2)** table des cases pleine : aucune entité, aucune exception, les deux tirages faits (`wind1_full_table_pf`), objet libéré au 129.
  - **(p3)** une cible par la production, avec les réglages de production : `one_near_fixgate` si le défaut de Q-H4E1-1 est gardé, sinon `one_near_fix`.
  - **(p4)** interruption `hurt60` par la production (R-EL-6).
  - **(p5)** invariants : compte du registre inchangé, `ProductionWiredWeaponTypes` inchangé.
  - La phase B câble les trois entrées `0x31`.
- ⏳ **T3 — ADR, plan, verifiers frais** (deux, l'un sur les clés et leurs réglages).
  - Mutants : au moins 30, tous tués sauf l'équivalent déclaré. Ce sont les 45 de §3.3 portés sur le code C#, chaque famille ayant un test tueur nommé ci-dessus.
  - Exemples de correspondance :
    - porte `0x33` retirée : (d3), (w1) ;
    - garde absente : (d3), (w3), (p4) ;
    - Z à 60 px, sans porte ou `>=` : (d7b), `zgate_*` ;
    - dernière cible par référence : (d7d), `slot_reuse` ;
    - D8 absent : (d7c), `d8_far_first_dec` ;
    - tirages permutés ou un seul : (w1), (d1) ;
    - test du sol en branche cible : (d1) ;
    - fin au 101 : (d6) ;
    - phase 2 gardée : (d4).

**Acceptation.**
- *Oracle* : rejeux de T0 (a) identiques, manifeste identique sur deux exécutions.
- *DLL* :
  - T1 vert en phase A ; (p1) à (p4) rouges avant la phase B et verts après ; (p5) vert avant et après.
  - Suite en Release (copie neutralisée) et en Debug, zéro échec.
  - Tests déplacés ⊂ liste prédite (vide) ; `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.

**Preuve de retrait** (exacte, déduite des définitions) :
- Remettre les trois entrées `0x31` au bouchon, ou la seule entrée du pilote : rouges exactement (p1) à (p4). (p5) et toute la phase A restent verts, les coutures remplaçant les entrées.
- Remettre la seule entrée d'annulation au bouchon : **rien de rouge**. Équivalent déclaré : l'annulation générale de h0a libère l'objet et le corps de `Cancel` est vide.
- Neutraliser `Drive` (retour 0, rien d'autre) : rouges (d1) à (d7), (w1) à (w9), (p1) à (p4) ; verts (t1), (q1), (e1), (c1).
- Retirer l'appel de la requête (liste vide) : rouges (d1), (d2) cible, (d7), (w5) à (w8), (p3) ; verts (w1) à (w4), (w9) sans cible, (p1), (p2), (p4).
- Neutraliser la garde : rouges (d3), (d1), (w3), (p4) ; verts (w1), (w2), (p1) à (p3).
- Rétablir la porte `0x33` avec `StrikeDuringPose` vrai : rouges (w1b), (w5)/(w6) `_fixgate`/`_gateoff`, (p1) et (p3) si le défaut est gardé ; verts en réglage fidèle.
- Retirer `DistanceKey`, ou la fausser : rouges (q1), (d1), (d7a).
- Ajouter une clé `(E, 1)` au registre : rouges (e1) et les deux épingles (le plan l'interdit).

**Arrêts.**
- h0a, h4a ou h4b1 non faites.
- `TargetQuery` change de contrat et ne permet plus `DistanceKey`.
- Un test existant bouge, ou une trace change.
- Un rejeu de T0 diffère, ou deux exécutions donnent des manifestes différents.
- Une sonde P1 à P8 contredit son attendu.
- Un octet de table diffère de l'exécutable.
- Un octet lu par les vecteurs n'a pas de membre.
- Un besoin de toucher au moteur.
- Un nom refusé par `check_names.py`.
- Une réponse de l'auteur qui met `_bin_` en jeu sans h4b1b.

**Budget.** Un `executor` par phase, deux tentatives ; `plan-verifier` avant, deux verifiers frais après.

**Retour arrière.** La phase B retire les trois entrées. La phase A retire le fichier, le champ et `DistanceKey`.

**Recette de l'auteur** (non bloquante).
- `arsenal-a-m14`, avec le tome 49 mis en objet du Rond par l'inventaire.
- Rond : 0,64 s de pose, puis un éclair toutes les 0,2 s, du premier à 0,82 s (tick 42) au dernier à 2,02 s (tick 102).
- Sept éclairs dans l'original, huit avec la correction.
- Chaque éclair tombe d'en haut sur un Beannoïde à moins de 160 px, sinon au hasard autour d'Alundra. Le Beannoïde touché meurt.
- Alundra reste immobile jusqu'à environ 2,56 s (tick 129). 1 PM de moins.

**À reporter dans le plan.**
1. Remplacer plan:8127 par une section 1.31.6 « E20.h4e1 » reprenant §5.
2. Noter dans h4b1 que le pilote du vent 1 lit la clé de la requête (`DistanceKey`).
3. Ajouter Q-H4E1-1 au §2.
4. Retirer f4, f5, f6a et E3 des prérequis du vent 1.

---

## 6. Questions pour l'auteur

**Q-H4E1-1 — la première frappe perdue du vent 1 (item de Q-H4-2).**
- *L'original* : la table prévoit huit éclairs (comptes 30, 40, … 100). Le premier, à 32 ticks (0,64 s), tombe pendant que le héros finit sa pose et il est perdu. Il y a donc sept éclairs, aux ticks 42 à 102.
- *Visible en jeu* :
  - Fidèle (A) : sept éclairs.
  - Corrigé (B) : huit éclairs, le premier 20 ms avant la fin de la pose (tick 32).
  - Avec (B), **tous les emplacements tirés au hasard changent**, car le premier tirage décale les suivants. L'objet reste libéré au tick 129.
- *Coût* :
  - (B) ajoute un réglage nommé `StrikeDuringPose`, une seconde série de clés (`_gateoff`, `_fixgate`) dérivée du binaire modifié en mémoire (donc non fidèle à l'octet), et un test de plus.
  - (A) n'ajoute rien.
- *Défaut proposé* : **B (corriger)**. C'est la règle de l'auteur sur les défauts avérés, cohérente avec Q-H4B-2 et Q-H4B-3. Le réglage garde (A) testable.

**Rappels qui conditionnent les clés** (déjà au plan) :
- **Q-H4B-2**, la borne exclusive : un ennemi est ignoré tant qu'aucune entité n'occupe une case plus haute. Reproduire coûte h4b1b.
- **Q-H4B-3** : avec exactement deux ennemis possibles, le tri est absent et le plus lointain est visé environ une fois sur deux. Corriger ne coûte rien.
- Leurs défauts sont « corriger ». §2.4 dit quelles clés servent selon la réponse.

**Aucune autre question.** La mort de D9, le pointeur de la dernière cible, le signe de la comparaison de distance et l'ordre des tirages se portent à l'identique, sans choix visible.

---

## 7. Vérifications, limites, INCONNU (contenu de `check.md`)

**Refait par moi.**
- Relecture capstone du binaire : table `0x800235E0`, tables de saut `0x800235F4` et `0x800233EC`, corps `0x800351A0`, wrapper `0x8003644C`/`0x80036480`, init des cases et du gabarit `0x8003B1F0-0x8003B2D8`.
- Balayage de tout accès à `+0x144`.
- Listing du pilote identique à celui d'h4.
- Rejeu de `trace_cast.py 0x31` d'h4, à l'identique.
- Mon propre modèle du pilote, de la requête et du tri, fuzzé contre le vrai code (800 vecteurs, 0 écart).
- 45 mutants.
- 42 clés refaites deux fois, manifestes identiques.
- Preuve que les clés `_dec_` valent en borne inclusive (`out/boundcheck.txt`).
- Croisements (`out/compare.txt`).

**Verdicts sur les affirmations d'h4 pour le vent 1.**
- CONFIRMÉ : la table, la requête, D8, D9 (la branche existe mais est morte), la première frappe perdue, l'annulation neutre, le calendrier.
- PRÉCISÉ : « ne s'exécute que si l'anim est `0x33` » ; fin au même appel que la dernière frappe ; dernière cible = pointeur.
- CORRIGÉ : les clés de monde d'h4 pour le vent 1, décalées d'un tick (R-EL-1).
- NON ÉTABLI : le comportement de la DLL.

**INFÉRENCE.**
- La DLL donnera les mêmes ticks et `Z − 1` (ADR-0026, ADR-0065), à confirmer par P1 à P8.
- `IsOnGround` d'un nouveau-né vaut 0 dans la DLL comme dans le binaire.
- Le bord (X = 3,0, Y = 2,0) vient de `ClampStepToMapEdge`.
- Le défaut B de Q-H4E1-1 est celui de la règle de l'auteur, pas un fait.

**INCONNU.**
- Le comportement de la DLL (rien n'a été exécuté).
- Le contenu de la piste XA 4 de l'objet `0x31` (Q-H4-1).
- Le verdict de `check_names.py` pour `WindTome1Ai`.
- La disponibilité du tome 49 sur la chaîne de l'histoire (Q-H4-7).
- Le pilote pour un compteur ≥ `0xFFFF` (inatteignable).
- Les objets autres que `0x31`, `0x2B` et 0 dans le dispatcheur d'annulation des vecteurs (h0a).

---

## 8. Fichiers (tous sous `OUT`)

- `README.md`.
- `keys/` : 42 clés `.json` + `.txt` et `MANIFEST.sha1`.
- `vec/` : `vec_wind1_80035EB0.json`, `vec_wind1_gen.txt`, `vec_wind1_replay.txt`, `SHA1SUMS`.
- Scripts : `wind1_key.py`, `wind1_vec.py`, `wind1_replay.py`, `wind1_model.py`, `wind1_mut.py`, `wind1_compare.py`, `wind1_boundcheck.py`, `wind1_tables.py`.
- `tools/` : `lib.py`, `bdis.py`, `scan_off.py`, `facts_check.py`.
- `h4o/` et `h4b_h/` : copies des harnais.
- `stale/` : clés d'h4 refaites avec `FAITHFUL=0`.
- `out/` :
  - `facts_check.txt`, `scan_off_144.txt`, `sprite22_dump.txt`, `compare.txt`, `boundcheck.txt`, `mutants.txt`, `expected_tables.txt`, `replay_trace_cast_31.txt` ;
  - `dis_80035EB0.txt`, `dis_800351B8_guard.txt`, `dis_8003AF70_query.txt`, `dis_8003644C.txt` ;
  - `MANIFEST_run{1,2}.sha1` et les journaux.