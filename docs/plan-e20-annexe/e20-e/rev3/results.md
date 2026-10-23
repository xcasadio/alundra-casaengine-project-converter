# E20.e revision 3, T0 : résultats mesurés (2026-10-23)

HEAD de départ `9c1f6fe3`, branche `chantier/theme1-porter-plateforme`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Aucun code de la DLL n'est écrit par T0.
Tout ce qui suit est mesuré, sauf mention. Les scripts et leurs sorties sont dans `oracle/` (`oracle/out/`), les vecteurs dans `vectors/`.

## Prérequis (arrêt sinon) : tous tenus

- E20.eR faite : la propriété exportée `TileCutEffectId = "2"` est dans les `.tileMap` des cartes 10, 14 et 16, et `BalanceLevel` n'y est plus (0 occurrence). Voir (j).
- E20.e0c faite : `Alundra/Items/LootDrop.cs` (`SpawnRandomLoot`) existe ; ADR-0082 ; HEAD `9c1f6fe3` « E20.e0c done ».
- `python -B tools/dll-structure/check_names.py --rules` sort 0 au départ (211 fichiers de DLL, 428 de tests, 0 trouvaille, 1 avertissement connu `TextBoxGpu.cs:266`).
- Les tranches amont (c1a, c1b, E7, e0a, e0b, e0k, f3c) sont dans l'historique de la branche.

## (a) Rejeu des outils et vecteurs JSON

Rejeu depuis une copie, sorties égales aux versées : `emu_cate.py 4000` : 4 000 vecteurs, 0 écart avec le pseudo-code du rapport, 460 coupes ; version « décompilation » : 414 vecteurs diffèrent (264 sur le seul Z, 150 sur les rangées) ;
`check/emu2.py` (2 000 vecteurs, 0 écart, 1 409 coupes) et `check/emu3.py` (2 000, 0 écart, 1 381 coupes) ; contrôle d'outillage `e20-f3/oracle/tools/wave_grass.py`, `wave_grass2.py`, `wave_grass3.py` : `wave_grass.txt`,
`wave_grass2.txt`, `wave_grass_zwindow.txt` identiques (fins de ligne normalisées) aux fichiers versés (13 coupes vers l'est, 10 vers le nord).
Export JSON : `export_vectors.py` écrit `vectors/cate-vectors.json` : **4 000 vecteurs, 460 coupes, 0 écart avec le pseudo-code, 0 écart de la forme en deux temps** (voir (k)). Par vecteur : drapeau de boîte, boîte (mots 32 bits de
`+0x1FC..+0x21C`), octet 9, cases des coins (indicateurs, hauteur, mot de pile, avant et après), appels dans l'ordre du binaire (effet, butin, son), mot du générateur d'entrée, et par butin le modèle (objet, tirages 1 ou 2, mot après).

## (b) Q-E6 : le repère de Z, mesuré (`oracle/out/zedge.txt`)

Machine du dépôt, vraie fonction `0x8003A7B0` appelée sur l'entité du héros avec une boîte écrite à la main. Case de hauteur H, `zc = (H << 20) + 0x80000`. Pour H = 0, 1 et 2, mêmes résultats :

| bord | valeur | résultat |
|---|---|---|
| bas, `bz` (binaire) | `zc - 1`, `zc` | coupe |
| bas | `zc + 1` | saute |
| haut, `bz + bh` | `zc - 1` | saute |
| haut | `zc`, `zc + 1` | coupe |

Dans le repère de la DLL (`HitBoxZ = bz - 1`, ADR-0026) : la case est coupée ssi `HitBoxZ + 1 <= zc <= HitBoxZ + 1 + bh`. Le bord `HitBoxZ == zc` (atteignable : sol plus décalage de boîte) **saute** dans le binaire ; la règle naïve sans le `+1` couperait.
**La règle inférée du plan (`HitBoxZ + 1` dans les deux comparaisons, comme `IsWall`) est confirmée ; aucun arrêt.**
Vrai héros, épée de base vers l'est, sol plat de hauteur g, cases coupables de hauteur H : le héros posé a `bz = (g << 20) + 1` et `bh = 983039` (15 pixels moins une unité) ; il coupe (5 coupes en 30 ticks) seulement quand H = g, pour g de 0 à 3 (20 combinaisons, 4 coupent).

## (c) Latence de la boîte (`oracle/out/latency.txt`)

La boîte lue par `0x8003A7B0` est celle stockée à la fin du tick précédent. Épée de base (21 appels du héros) : boîte lue = boîte de fin du tick t-1 pour 21 appels sur 21 ; = boîte de fin du tick t pour 17 sur 21 ; aux 4 ticks où les deux
boîtes stockées diffèrent (t5, t7, t12, t22) la boîte lue est toujours celle de t-1 (4 sur 4), jamais celle de t. Scénario de la vague (92 appels, héros et vague) : 92 sur 92 égale à t-1 ; 15 ticks où les deux diffèrent, 15 sur 15 égale à t-1.
Le décalage est d'un tick logique (« une image » du plan).

## (d) `HeroAttackTests` de la carte 10 (`oracle/out/heroattack_m10.txt`)

Le test `HeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle` : arrivée (252, 936) = case (10, 58), direction 16, Carré au tick 1, Croix au tick 27, Carré au tick 31 (états du héros 0x10, puis 0x2B et 0x2D, puis 0x14).
Dans le binaire (vraies cellules de la carte 10, vrai héros) : **une coupe a lieu au tick 2**, case (10, 56) (hauteur 0, Z de l'effet 0), un seul mot de générateur tiré (`rng_before` 2472470177, `rng_after` 1020151012, aucun objet), les sons dans l'ordre
`29` (tick 1), `31` (tick 2), puis `0` (22), `10` (27), `0` (28), `29` (31), `0` (41). Le coup en l'air (tick 31) ne coupe rien (Z du héros jusqu'à 1 802 241). Le test existant asserte `[29]` après la frappe au sol : **il bougera** (`[29, 31]`) ;
il est sur la liste prédite (e). Valeur attendue dérivée du binaire : sons 29 puis 31 aux ticks 1 et 2, un mot de générateur de plus que sans coupe.

## (e) Liste prédite des tests existants qui bougent

Méthode : analyse des tests qui chargent les cartes à cases coupables, puis **mesure en ombre** : le port de T1 câblé temporairement dans les deux bouchons (`WaveSwordAi.CheckAndTriggerTileEffect` et `PlayerAnimationEffects.PlayerGrassCut` appellent
`TileEffect.Trigger` après leur journal), suite complète en Release (copie neutralisée) dans l'arbre de travail, câblage retiré ensuite (`git diff` vide sur `Alundra/Ai` et `Alundra/Hero`). Résultat : 5 311 tests, **1 échec**.

| test existant | phase | raison |
|---|---|---|
| `Alundra.Tests.Hero.HeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle` | B (T3) | le coup au sol coupe (10, 56) au tick 2 : sons `[29, 31]` (mesuré) |
| `Alundra.Tests.SaveGames.TestSavesTests` : le test qui épingle la liste exacte des préréglages (`AllPresetNames`, nom actuel `TheElevenPresets_AreListed_InTheOrderOfTheTable`) | B (T3) | le préréglage `herbe-m14` allonge la liste (lecture du code du test, non mesurable avant le préréglage) |

Aucun autre test ne bouge : les bancs `JumpHeroRig` et `ContactHost` ne sont pas des contextes de monde (S-E2, rien) ; les arènes de la carte 14 sont hors des cases coupables ; les épingles des bouchons (`FlailBounceTests`, `WaveSwordLifeTests`,
`WaveSwordWiringTests`) tiennent car les bouchons gardent leur journal. Plafond du plan : 10 ; prévision : 2.

## (f) Place des tests de monde et de la recette sur la carte 14

Candidat du plan confirmé : colonnes 26 à 29, rangées 40 à 44 (hauteur 1), **16 buissons sur 5 rangées** : rangée 40 : 27, 28 ; 41 : 27, 28, 29 ; 42 : 26, 27, 28, 29 ; 43 : 26, 27, 28, 29 ; 44 : 26, 27, 28. Voisins marchables : l'est, colonnes 30 à 33 des
rangées 43 et 44 (hauteur 1, marchabilité 0) ; la rangée 42 à l'est est un mur (5/01) ; l'ouest (colonne 25) est une falaise de hauteur 4. Le héros se place donc à l'est, face à l'ouest. Positions retenues (mesurées, `oracle/out/cut_m14.txt`) :
épée de base : (732, 696) face à l'ouest (4 coupes : (29,43) t2, (28,43) t6, (28,42) et (29,42) t8) ; vague : (744, 696) face à l'ouest, épée d'acier tenue 95 ticks (la vague naît au tick 117 ; elle traverse **deux colonnes** : (28,42) et (28,44) au tick 123,
(27,42) et (27,44) au tick 127). Case sans pile : (27, 45) (aussi (31, 30), hauteur 3) : le héros en (708, 728) face à l'ouest la coupe seule au tick 6.

## (g) Oracle `cut_m14` (`oracle/out/cut_m14.json`, `.txt`)

Le héros seul sur les vraies cases de la carte 14, pas du générateur par tick modélisé (O-E-1, ADR-0066 : un pas après les passes de chaque tick). Six scénarios, semences cherchées par le modèle puis **confirmées sur les appels réels** (0 écart modèle/réel sur les butins) :

| scénario | position | semence | coupes | objets |
|---|---|---|---|---|
| `sword_none` | (732, 696) O, épée de base | `s0` = 0x1002 | 4 (t2, t6, t8 x2) | 0 |
| `wave_none` | (744, 696) O, acier tenu | 0x1002 | 6 (héros t2, t8 ; vague t123 x2, t127 x2) | 0 |
| `nostack_none` | (708, 728) O | 0x1002 | 4 dont (27,45) sans pile au t6 | 0 |
| `sword_obj` | (732, 696) | 0x1006 | 1 jusqu'à la naissance (t2) | 1 (type 100, objet 0x46) |
| `wave_obj` | (744, 696) | 0x1005 | 4 ; la 1re coupe de la vague (t123) donne l'objet | 1 (type 114, objet 0x54) |
| `nograss` | (212, 96) E | 0x1002 | 0 | 0 |

Pour chaque coupe : tick, appelant (héros ou vague), case, hauteur, position et Z de l'effet (`x = col*24 + 12`, `y = row*16 + 8`, `z = 16 px` pour la hauteur 1), mots du générateur avant et après le butin, objet et tirages du modèle, ordre des effets et des sons
par tick (effet, puis `snd(84)` si objet, puis `snd(31)`, coupe par coupe), cases avant et après (indicateurs, mot de pile 0xFFFF). Fait notable : dans la vague, les 6 mots tirés par les étincelles de la charge (3 étincelles, 2 mots chacune, ticks 85, 89, 93) s'ajoutent au flux ;
le modèle des semences en tient compte (`extras_of`). Le `snd(0)` qui précède `snd(84)` est l'appel du son d'animation d'indice 0 de l'objet né (aucun son joué) ; les tests de la DLL l'ignorent. Scénarios « objet » arrêtés trois ticks après la naissance.

**(g2)** Sonde du générateur dans le banc vidé, natifs allumés (arène de la carte 14, entités retirées, `NativeHandlersEnabled = true`, un coup en l'air en (212, 96) face à l'est, semence 0x1002 posée en tête du tick 1) : les 40 mots (échantillonnés à la passe d'inventaire,
donc avant le pas du tick) égalent l'oracle `[semence] + rng_by_tick` sur les 32 bits bas, 40 sur 40 (`probes/probe-g2-output.txt`). Le mot de la DLL est sur 64 bits (le bas est celui du binaire).

## (h) P-E1 : le premier tick d'un objet né d'une coupe

Binaire (`object_first_ticks` de `cut_m14.json`) : héros dans `MovePlayer` : objet né au tick 2 (slot 1), programme A au tick 2 (le **même** tick, D-E0A-5 confirmé), C255 au tick 3, poussée verticale vue dès la physique du tick 2.
Vague dans la passe des entités : objet né au tick 123 (slot 2, après la vague au slot 1), A aux ticks 123 **et** 124, C255 au tick 125, première poussée vue au tick 124. Côté DLL, la règle de Q-E0C-8 (e0c T0 (b)) : un objet né dans la passe des programmes d'une autre entité tourne un tick plus tard que dans
le binaire quand sa case est plus haute (A à T+1, C à T+2) ; l'écart d'un tick est gardé, documenté, à épingler en T2/T4. Non mesuré dans la DLL ici (aucun appelant de production avant T3).

## (i) Complétude du visuel (sonde de la DLL, `probes/probe-i-output.txt`)

| carte | cases coupables | avec pile | tuiles visibles de ces piles | absentes du modèle vivant | « did not match » | une coupe puis un flush |
|---|---|---|---|---|---|---|
| 14 | 179 | 177 | 354 | 0 | 0 | (13,11) : 2 137 -> 2 135 entrées de mur, 1,2 ms |
| 10 | 133 | 133 | 266 | 0 | 0 | (9,15) : 1 884 -> 1 882, 0,6 ms |
| 16 | 266 | 261 | 519 | 0 | 0 | (7,7) : 1 816 -> 1 814, 1,9 ms |

Sur la 14, couper (13, 11) retire exactement 2 entrées avec une reconstruction. Coût mesuré (carte 16, la plus riche) : moins de 2 ms. Pas d'arrêt.

## (j) Propriété exportée

`TileCutEffectId = "2"` dans les `.tileMap` des cartes 10, 14 et 16, `BalanceLevel` absent (0 occurrence dans chacun). E20.eR est faite.

## (k) Forme en deux temps

`export_vectors.py` : la forme en deux temps de S-E3 (les quatre coins lus et coupés un à un sur l'état courant, puis effet, butin, son par site) donne exactement les appels et les cases de l'entrelacement du binaire sur les 4 000 vecteurs : 0 écart.
**(k2)** Un coin coûte 1 ou 2 tirages et rien d'autre ne tire : `loot_check.py`, vrai `0x80032968` sur 12 016 états (12 000 aléatoires et un par quartet) : 0 écart avec le modèle (objet, nombre de pas), coûts `{1: 10464, 2: 1552}`. Aucun autre coût : pas d'arrêt.

## ADR

Prochain numéro d'ADR libre sur toutes les branches locales, les refs et les worktrees : **0083** (le plus haut présent est 0082, sur la branche courante ; aucune branche, aucun worktree ne porte 0083). À employer à T4.

## Faits nouveaux, écarts au plan

1. Candidat de la carte 14 confirmé (16 buissons) ; les voisins marchables sont à l'est (la rangée 42 y est un mur).
2. Les étincelles de la charge tirent 6 mots avant la libération (ticks 85, 89, 93) : un scénario de vague qui assertera l'état du générateur doit en tenir compte (le modèle de semence le fait).
3. Le mot du générateur de la DLL est sur 64 bits ; la comparaison avec le binaire porte sur les 32 bits bas.
4. La liste prédite de (e) tient en 2 tests (mesure en ombre plus lecture) ; le plan en autorisait 10.
5. Le numéro d'ADR de T4 est 0083.
