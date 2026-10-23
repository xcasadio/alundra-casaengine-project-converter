# E20.f5, T0 (2026-10-23) : résultats

Plan : `docs/plan-e20-heros.md` §1.21 (bloc « Révision au HEAD », points 1 à 10, puces « f5 ») et §1.21.1. HEAD de départ `b0116485`, branche `chantier/theme1-porter-plateforme`.
Prérequis relus : E20.f4 faite (`ChargedArrowAi`, `ProductionWiredWeaponTypes` = `{7, 27, 28, 29}`, l'interrupteur d'arène `EventProgramRunner.NativeSpriteTypeFilterForTests`, `SyntheticField` et `CutBench`, ADR-0084 à étendre).
Dernier ADR : 0084 sur la branche courante (à recompter à l'écriture, T4).

## 1. L'oracle rejoué (la machine MIPS, le vrai code de `ALUN_CD.EXE`)

- Les 17 clés gelées `arrow*` (13 de `run_arrows.py`, 3 de `run_arrows_extra.py`, `arrow3_point_blank`) et `sweep_wall.txt` : rejouées depuis une copie, **identiques octet pour octet** à celles versées.
- Les sorties de `bin/out` (clés `arrow*`, balayage, `fire11_point_blank`) et de `f5/oracle/out` : **manifeste identique sur deux exécutions complètes** (`oracle/manifest.txt`, 113 lignes).
- **Clés neuves** (S = 10 sauf mention ; tick relatif à S) :

| Clé | Résultat de la machine |
|---|---|
| `arrow3_target_filter_b4` | cible touchée à S+30, `+0xB4 \|= 0x40` posé juste après : E17 ne trouve personne, la flèche reste au statut 2 et vole jusqu'au bout (x = 493) |
| `arrow3_target_filter_class` | `+0x6C &= ~1` posé après la touche : même issue |
| `arrow3_target_dead_same_dispatch` | la cible (case 1, statut 4 dans son D) est trouvée par la flèche (case 2) : touche S+30, statut 4 à S+31, case libre S+32 |
| `arrow3_target_higher_slot` | case 1 libérée avant le tir, flèche en case 1, cible en case 2 (la dernière du balayage) : trouvée, même issue (S+30, S+31, S+32) |
| `arrow3_zreach_16` et `arrow3_zreach_17` | corps de 16 : jamais touchée ; corps de 17 : touche S+30, morte S+31, libre S+32 |
| `arrow3_target_sprite136` | le vrai sprite 136 de la carte 14 (corps d'en-tête 14×16×17), animation posée à 3 (la première avec une boîte vulnérable 9×9×5) : touche S+30, morte S+31 |
| `arrow3_air_east` | tir du chasseur en l'air (Croix au tick 1, Carré au 8) : **S = 9 = P+1** ; mur : drapeau S+42, rebond S+43, fin S+60, libre S+61 |
| `arrow4_air_east` | tir normal de William en l'air : **S = 17 = P+9** (l'INFÉRENCE du relevé est **confirmée**) ; même vie contre le mur |
| `arrow3_map_edge_south` | héros en (100, 900) vers le sud : drapeau à S+13 (y = 952), rebond, statut 4 à S+31 ; le bord sud existe dans le monde de l'oracle, l'obligation reportée par e0k3 est jouable |

- **Les départs du mur** (`../bin/out/sweep_wall.txt`, type 3, colonne 15, x0 de 96 à 107) : drapeau à S+43, 43, 44, 43, 42, 43, 42, 42, 43, 42, 41, 42 (fenêtre S+41 à S+44).
- **Les 15 mutants** (`oracle/out/mutants_f5.txt`) : **14 tués, 1 équivalent** :
  E17-1 (garde du compteur de touche), E17-2 (direction +8), E17-3 (bit de gravité), E17-5 (masque `andi`, écriture morte, vu par `flags6C`), E17-6 (statut 3 au repli), E17-7 (règle `+0x1CC`), E17-8 (règle `+0xB4 & 0x40`, tué par `arrow3_target_filter_b4`),
  E17-9 (règle de classe, tué par `arrow3_target_filter_class`), E17-10 (type `0x1AC`), E17-11 (phase `== 2`), E17-12 (fin sans test), E17-13 (balayage arrêté une case trop tôt, tué par `arrow3_target_higher_slot`),
  DELAY-1 (délai de l'arc 8 → 9, fiche w8, `0x80028CE3`), ZBYTE-1 (octet z 0x10 → 0x11, `0x80028CE4`).
  **E17-4 est équivalent** (`ori 0x140 → 0x100` : le bit `0x40` est déjà posé dans `0x12074` ; les `flags6C` après le rebond sont identiques, `0x12144`).
- **Générateur** : les clés `_pf` de cibles prennent `rng0` du tick 1 = germe + 2 pas (le créateur réel de la cible tire deux fois) : les tests de cibles posent ce mot, pas le germe (`ArrowRun`, `generatorAtTick1`).

## 2. Les sondes de la DLL (HEAD de départ + E17 de l'exécutant, sans la règle de la clé de vitesse ; `probes/`)

Le banc : `HitArena` (carte 14 réelle), le champ synthétique et son magasin partagés (`SyntheticField`), les autres entités retirées, l'arène natifs éteints et l'interrupteur de f4 pour le type de la flèche, l'arc par `Give(5)` ou `Give(6)` puis `Equip(2)`,
Carré au tick 1 (P = 1), le générateur au germe `0x12345678` au tick 1, un tick par image (le banc d'`ArrowRun`, qui entre dans T1).

| Sonde | Attendu (écrit avant) | Mesuré |
|---|---|---|
| Banc : naissance (chasseur, William) | S = 10 = P + 9 (décalage 0), fille du héros, case la plus basse libre (1), première ligne égale à la clé | **S = 10** aux deux, case 1, fille du héros, ligne S+0 identique à la clé ; boîte 24×16×11 à hbz 16, attaque (131, 1) ou (131, 2) |
| Banc : tir en l'air | chasseur S = P + 1 ; William S = P + 9 | S = 9 (P = 8) et S = 17 (P = 8) : **égaux aux clés**, lignes S..S+2 identiques |
| Banc vidé, sans cible : touche ni étincelle | aucune sur les clés mur, bord, nord, ouest | **0 touche, 0 étincelle** aux quatre ; les lignes ne diffèrent de la clé qu'après le rebond (18, 18, 6, 14 ticks), c'est la règle de vitesse (ci-dessous) |
| Pas par image (point 3, révision 4) | après chaque image : état = `rng0(t+1)` de la clé `_pf`, pas = tirages + 1 | **0 écart** sur `arrow3_east_wall_pf` (75 ticks) et sur `arrow3_target_pf` (70, avec les 9 tirages de la touche à S+30) |
| Tick du drapeau de mur, 12 départs | règle « arrivée + 1 » (D-F4-9) ; la clé égale la DLL pour 8 départs, un tick de plus pour 4 | DLL : 43, 43, 43, 43, 42, 42, 42, 42, 42, 42, 41, 41 ; clé : 43, 43, 44, 43, 42, 43, 42, 42, 43, 42, 41, 42 ; **un tick de plus pour 98, 101, 104, 107** (les mêmes 4 départs qu'en f4) |
| Rebond et position **avant** la règle | tick du rebond S+43 ; écart X (DLL − clé) de **+1,875 px** à 1 tick par image ; −2,25 px quand le rebond tombe au 1er tick d'une image de 2 ticks | rebond vu à S+43 aux trois cadences. Écart persistant : **+1,875** (1 tick par image) ; **−2,25** (2 ticks par image : +1,875 au tick du rebond, 1er de l'image, puis −4,125 au 2ᵉ, où la DLL fait 6 px dans la nouvelle direction) ; **−2,25** (3 ticks par image : le rebond tombe au 2ᵉ tick de l'image 52-54). Le cas « 1er tick d'une image de 3 ticks », −6,375 px (1,875 − 2 × 4,125), n'est pas atteint par ce calendrier : la valeur reste celle de l'arithmétique de la contre-vérification |
| Cibles, avant la règle | touche S+30, morte S+31, libre S+32 pour un corps de 32 et de 17 ; ratée pour 16 et 8 | **égal** aux clés ; corps de 16 et de 8 : aucune touche ; le vrai sprite 136 (animation 3) : touche S+30, morte S+31 |
| Le vrai sprite 136 sur le banc | corps d'en-tête 14×16×17 | largeur 917503, hauteur 1048575, profondeur 1114111 (14, 16, 17 pixels moins un raw) ; sans boîte vulnérable en animation 0, **avec** une boîte 9×9×5 en animation 3 (comme la machine) |

## 3. Liste prédite des tests qui bougent, recomptée au HEAD de départ

Mesurée en posant temporairement, sur le HEAD : E17 de l'exécutant (`ArrowAi`, registre de production), la règle de la clé de vitesse (D-F5-5) **et** `{3, 4}` dans `ProductionWiredWeaponTypes`, puis la suite complète Release (copie neutralisée),
5 369 tests : **9 échecs** (l'édition de la phase B est annulée ensuite).

| Test | Phase | Raison |
|---|---|---|
| `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` | A | l'épingle du registre : plus la clé (E, 17) |
| `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_…` | A | la seconde épingle : même dérivation |
| `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses` | A | la troisième épingle (née d'f4, comptée par somme des classes) : **hors de la liste du plan**, même nature (compte du registre) |
| `Alundra.Tests.Hero.WeaponSpawnSeamTests.S5_TheProductionSetPassesTheInvariant_TheFakeSetWithTheArrowFailsOnE17` | A (clauses de E17) puis B (l'égalité de l'ensemble de production) | E17 est enregistré : le type 3 passe l'invariant, la clause négative prend un registre vide |
| `Alundra.Tests.Maps.MapStateTests+MarkedEntityPins.P3_AMarkedEntityWhoseFirstAnimationIsValidated_KeysItsKinematicsOnItsCurrentAnimation_NotOnAnAskedOne` | A | l'épingle P3 d'e0k1 : la règle de la clé de vitesse |
| `Alundra.Tests.Entities.BirthTickTests.AnEntityWithoutTheMarker_KeysItsKinematicsOnItsCurrentAnimation_NeverOnTheTarget_EvenWithCurrentStillNotValidated` | A | né par l'interface, fait tourner des images : la règle (attendu 150, 150, 150 ; actuel 153, 159, 165) |
| `Alundra.Tests.Entities.LogicalZWorldTests.Z5_AFreezeAndAThawInTheAir_LetTheZGoOnOnTheSeriesOfTheOracle_AndNeverAnchorTheEntity_WhereTheRecordEntityIsUnchanged` | A | né par l'interface, fait tourner des images : la règle (la série des forces change après le dégel) |
| `Alundra.Tests.Hero.WeaponSpawnSeamTests.S1_TheEmptySet_CountsAndJournalsTheShotAndCreatesNothing_TheSetWithTheArrow_CreatesOneEntityInTheLowestFreeSlot` | B | premier bloc : l'ensemble de production vide n'existe plus pour le type 3 |
| `Alundra.Tests.Ai.Projectiles.ChargedArrowProductionTests.P1_TheChargeOfWilliamBirthsTheArrowAtS_…_IfAndOnlyIfTheProductionSetHolds4` | B | le type 4 naît en production : le compte passe de 1 à 2 (test d'f4, **hors de la liste du plan**) |

Surveillés et **restés verts** : `MapEdgeWorldTests` B1 et `MapStateTests` K1 (les risques nommés par la contre-vérification), le (p5) de f3c, `BirthTickTests.TheSoundOfTheCreation_…`.
Total 9 (au plus 10 : sous le seuil d'arrêt) ; la phase A en déplace 7 (S5 compris), la phase B 3 (S5, S1 et P1 d'f4).
Les quatre tests que la liste nominative du plan ne nomme pas (C2, BirthTickTests, Z5, P1 d'f4) sont ceux que le plan range sous « les épingles du registre » et « les tests nés par l'interface qui font tourner des images » ; le (p5) de f3c n'est pas déplacé.

## 4. Comptes de départ

`Alundra.Tests` en Release (copie neutralisée) : **5 369 réussites, 0 échec** au HEAD de départ (fin d'f4) ; 5 360 réussites et 9 échecs avec l'édition temporaire ci-dessus.

## 5. Écarts au plan

1. Les noms de fichiers de tests ont changé avec la restructuration (miroir de R5) : `Ai/NativeSpriteRegistryTests.cs`, `Ai/Projectiles/WaveSwordLifeTests.cs`, `Hero/WeaponSpawnSeamTests.cs`, `Maps/MapStateTests.cs`, `Entities/BirthTickTests.cs`, `Entities/LogicalZWorldTests.cs`.
2. Le plan annonce quatorze scénarios déjà relevés ; la boîte compte **17 clés gelées `arrow*`** (dont `arrow4_east_wall`, `arrow4_target`, `arrow3_point_blank`) ; les clés neuves sont les **dix** ci-dessus ; les mutants sont quinze (douze de la critique, la borne de la boucle, le délai, l'octet z).
3. `arrow3_target_sprite136` : le sprite 136 démarre en animation 0, sans boîte vulnérable (son programme A, bouchonné, la changerait) ; la clé pose l'animation 3 au départ (injection documentée dans le titre de la clé).
4. Le calendrier à 3 ticks par image ne met pas le rebond au 1er tick de l'image (voir la sonde) ; le −6,375 px de la contre-vérification reste dérivé, pas mesuré.
5. Le bord sud de l'oracle existe (y = 952) : la scène demandée par e0k3 est jouée.
6. Aucun enregistrement de carte n'a un type 3 à 6 de la page globale (`../bin/out/census_global_page_records.txt` : seuls les types 2, 12 et 16) : le registre (E, 17) de la phase A est inerte en production.
