# E20.f6b, T0 (2026-10-25) : résultats

Plan : `docs/plan-e20-heros.md` §1.22 (bloc « Révision au HEAD », points 1 à 10, puce « f6b ») et §1.22.2. HEAD de départ `133ebb99`, branche `chantier/theme2-attaques-objets`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Prérequis relus et présents : f6a (ADR-0087, `HasLogicalZ` nourrit `Slope_18c`), f4 (`NativeSpriteTypeFilterForTests`, D-F4-7), f5 (la clé de vitesse `TargetAnimationId` quand `HasLogicalZ`, D-F5-5, `ScriptedMotion.TickScriptedNpc`), f3 (dont f3b : les effets d'attache 4 et 5), e0b1, e0k1 à e0k3, e0k5 (ADR-0097) et c2b (ADR-0096, la blessure du héros : sans effet sur les boules).
T0 ne change aucun code de la DLL : la sonde de la DLL et le câblage temporaire de la prédiction tournaient dans des fichiers jetables (`probes/`), retirés avant le commit (`git diff` vide sur `Alundra/` et `Alundra.Tests/`).

## ARRÊT relevé par T0 (puce « R de (p1) » de la révision au HEAD)

Le plan écrit : « Sonde T0 : un tir chargé de chaque baguette dans l'arène fait naître le type 12 ou 15 au tick 102 ; sinon arrêt. » **La sonde contredit le texte.** Dans l'arène de la charge (carte 14 réelle, hero (212, 96), chemin de production, ensemble câblé {11, 12, 14, 15} posé temporairement), `probes/probes-p1-out.txt` :

| Dernier tick de Carré tenu | Relâchement R | Type chargé né | Tick de naissance |
|---|---|---|---|
| 90 (le calendrier de la vague, `WaveRun`) | 91 | **aucun** (jusqu'au tick 140, 1 tick par image, feu et glace) | - |
| 92 | 93 | 12 (feu, 1 tick par image) | 104 |
| 93 | 94 | 12 (feu, 1 tick par image) | 105 |
| 95 (le calendrier de l'oracle) | 96 | 12 (feu) et 15 (glace) | **107** (1 tick par image), 108 (3 ticks par image) |

Cause (lue pendant la mise au point de la sonde, version non versée qui imprimait le héros tous les 2 ticks) : le compteur de charge du héros (`WeaponChargeTimer`) valait 28 au tick 60 et 58 au tick 90 (`tick - 32` : il part après la fin du tir normal du début de la prise) et n'atteint les 60 du tir chargé qu'au tick 92 ; au tick 90 il vaut 58, le relâchement du tick 91 ne tire donc que le tir normal. L'épée (vague) n'a pas ce seuil, d'où le 91 de `WaveRun`. **Recommandation** : (p1) tient le Carré jusqu'au tick 95 et relâche au tick 96, comme l'oracle (S = R + 11 = 107, pas de décalage d'arène pour les baguettes) ; la baguette non chargée naît à P + 11 = 12 pour P = 1 (mesuré, 1, 2 et 3 ticks par image, feu et glace). La « plus basse case libre avant le tir » vaut 24 avant la naissance du tir normal (tick 12) et 25 avant celle du chargé. Le texte du plan (« 102 ») est à corriger ; T0 s'arrête sur cette sonde comme le plan le demande, sans contourner.

## 1. L'oracle rejoué (la machine MIPS, le vrai code de `ALUN_CD.EXE`)

- Les 30 clés des relevés (`fire11_*` 19, `fire12_*` 2, `ice14_*` 8, `ice15_flat`) et les deux clés des flammes (`flames309`, `flames309_without_A_program`), rejouées depuis une copie par `oracle/run_all.sh` : **les 64 fichiers (json et traces) sont identiques octet pour octet** à ceux de `bin/out`.
- **Manifeste identique sur deux exécutions complètes** depuis deux copies (`oracle/manifest.txt`, 183 lignes : les clés et traces `fire*`, `ice*`, `flames*` de `bin/out` réécrites par la copie et `f6b/oracle/out`).
- **Le plan vérifié contre les clés** (`oracle/check_plan_values.py`, sortie `oracle/out/check_plan_values.txt`) : **54 assertions sur 54** vraies : tout le tableau « Valeurs attendues » (statuts, X, Z DLL = Z oracle − 1 unité, forces, sol, boîtes, drapeaux, traînée, explosion, bouffées, fin), la glace, les chargées (S = 107, boîte d'explosion 36×24×31, sons 41 et 40, bouffées jusqu'à S+64, mort S+48 et S+68), le mur, les marches de 16 et 32, l'eau (pentes 1 et 4 : mort à S+11 par l'attache 6, libre à S+12, aucun tirage ; pentes 2 et 3 et pied moitié sur l'eau : explosion normale), la branche A après le mur, les touches (`attach(4)` à S+8 en feu, `attach(5)` à S+9 en glace, 9 tirages), les flammes.
  Deux précisions que le plan ne dit pas, lues dans les clés : (a) au mur, **les cinq effets de l'explosion sont au Z de la boule avant la physique du tick** (18 à S+7, la boule étant à 14 à la fin du tick) et **la première bouffée suit au tick S+8** (le compteur 8 est un multiple de 8, 2 tirages, Z 22) ; (b) la glace n'a aucun décalage de Z (`z=27` à S+4).
- **Clés de f6b** (`oracle/out/json` et `traces`, chacune en deux formes : brute, et `_pf` = avec le pas du générateur qui termine chaque tick du binaire, ajouté après l'enregistrement de `rng1` et avant la lecture de `rng0` du tick suivant, règle du point 3) :
  - **les 32 clés figées en `_pf`** (30 boules + 2 flammes) : `fire11_flat_pf`, ..., `flames309_pf`, `flames309_without_A_program_pf` ; les clés brutes restent dans `bin/out/json` ;
  - **la branche A sur l'eau** : `fire11_branchA_water_on_puff_tick` (le scénario du plan : `+0x18C` posé à 4 à la fin de S+15, injection documentée dans le titre de la clé), `ice14_branchA_water_on_puff_tick`, `fire11_branchA_slope1_on_puff_tick`, `fire11_branchA_slope2_on_puff_tick` (témoin négatif : pas de destruction, la bouffée continue), `fire11_branchA_water_off_puff_tick` (injection à la fin de S+16 : le tick S+17 n'est pas un multiple de 8, la destruction seule), `fire11_branchA_water_on_end_tick` (injection à la fin de S+47 où `+0xAC` vaut 1 : deux destructions dans le même tick) ;
  - **les boules tirées contre ou près d'un mur** (les clés de boule que l'e0k5 a laissées à f6b) : `fire11_flush`, `fire12_flush`, `ice14_flush`, `ice15_flush` (héros à 109, mur colonne 5), `fire11_near105` (105), `fire11_x107` (107) ; `fire11_point_blank` est une clé figée ;
  - **les flammes de la 309 avec le vrai interpréteur d'octets** : `flames309_real` et `flames309_real_pf`.
- **Résultat de la branche A sur l'eau** (`fire11_branchA_water_on_puff_tick`, tick 28 = S+16) : le tick contient **dans cet ordre** `RNG+2`, `attach(id=6,target=slot1,mode=1)`, puis la bouffée `fx(id=17,dir=0,x=166.000,y=100.000,z=8.000,v=(-0.4374,0.0926,2.0000))` ; la boule est au statut 4 (case libre au tick 29). C'est l'ordre littéral de `0x8007EF84-0x8007F214` (la destruction n'est pas suivie d'un retour). La glace : même ordre, effet 18 de Z + 0. Pente 2 : aucune destruction, la bouffée seule. Hors tick multiple de 8 : la destruction seule. À S+48 avec `+0xAC` = 1 : la destruction par la pente (l'effet d'attache 6, statut 4) puis, `+0xAC` valant 1, celle de la fin d'animation (`-1`, sans effet), dans le même tick ; la case est libre au tick suivant.
- **Les flammes de la 309 avec le vrai `0x8004205C`** : les `Codes` et les six `EventCodes*Table` de la carte 309 installés en RAM à l'endroit où le vrai `0x80041EE4` les lit (`[0x80126E70 + 0x30]` la base, `[+0x34 + 4 L]` la table de la lettre L), le bouchon retiré ; l'enregistrement porte `0xFFFFFF87` (bit `0x80` : le bloc de la carte, indice 7). Le programme `1A 02 / 63 80 F0 00 / FF` s'exécute pour de bon : animation 2, drapeaux `0x132154` -> `0x132104`. Comparé au bouchon (`flames309` figée) sur 80 ticks et les trois flammes : **statut, positions, forces, drapeaux, animation, effets (tick 5, 9, 13, ... ; trois par émission ; effet 17 ; Z 40 ; vitesse nulle), tirages : identiques**. **Un seul écart** : `coll_140` vaut 0 avec le vrai interpréteur et 1 avec le bouchon (même avec un programme vide) : le code de contact avec le sol (`0x800376E0`) ne s'exécute jamais pour une flamme portant le vrai interpréteur, alors que `ground_144` vaut 1 dans les deux. Les tests (m1) à (m3) ne lisent pas cette colonne ; la DLL met `IsOnGround` à 1 pour ces flammes (sonde de la section 3).

## 2. Les mutants (`oracle/mutants_f6b.py`, `oracle/out/mutants_f6b.txt`, la vraie machine, RAM patchée)

**27 mutants : 25 tués, 2 prouvés équivalents.** Chaque mutant est comparé à son témoin sur la signature de chaque tick de la boule (statut, position, forces, drapeaux, animation, pente, sol, boîte, effets avec leurs vitesses, sons, tirages).

| Mutants | Mot changé | Tué par |
|---|---|---|
| C60-1 | période de la traînée `andi 3` -> 7 | feu à plat |
| C60-2 | effet de glace `0x12` -> `0x11` | glace à plat |
| C60-3 | décalage de Z du feu 19 -> 18 | feu à plat |
| C60-4, C60-5, C60-6 | vitesse Z, X, Y non opposée (`negu` -> `move`) | feu à plat (Z, X) ; feu vers le nord (Y) |
| C60-7 | sélection feu/glace `sltiu 2` -> 1 (le type 12 prend la glace) | feu chargé |
| C60-8 | la glace prend aussi le décalage de Z | glace à plat |
| E9-1 | cinq effets -> quatre | feu à plat |
| E9-2 | période de la bouffée 7 -> 3 | feu à plat |
| E9-3, E9-17 | décalage de Z de la bouffée 19 -> 18 ; la glace le prend aussi | feu ; glace |
| E9-4 | branche B : test de la pente 1 retiré | feu sur pente 1 |
| E9-5a | branche B : pente 4 -> destruction toujours | feu à plat |
| **E9-5b** | branche B : pente 4 -> jamais de destruction | **équivalent** |
| E9-6 | branche B : effet de destruction 6 -> 5 | feu sur pente 1 |
| E9-7, E9-14 | échelle des tirages de l'explosion (vx `0x60001` -> 5, vy `0x40001` -> 3) | feu à plat |
| E9-8 | test de fin d'animation `+0xAC` jamais vrai | feu à plat |
| **E9-9** | branche B : `ori 0x40` -> 0 | **équivalent** |
| E9-10 | `+0x88 = 1` non écrit | feu à plat |
| E9-11, E9-12, E9-13, E9-18 | branche A : effet 6 -> 5 ; test de la pente 1 retiré ; test de la pente 4 jamais vrai ; appel de `DestroyEntity(e, 6)` retiré | injections de la pente (clés `branchA`) |
| E9-15, E9-16 | bouffée : échelle de vx 3 -> 2 ; vz 2 -> 1 | feu à plat |
| SPAWN-1 | le `+1` du Z de création retiré (`0x80039EA4`) | feu à plat |

- **E9-5b équivalent** : la branche B ne tourne qu'au statut 3 (le programme E), et le choix du programme prend la pente 4 au statut 2 avant elle. Preuve par la machine : `+0x18C` posé à 4 après chaque tick de S à S+13 sur quatre scénarios (feu, glace, feu contre le mur colonne 6, feu chargé), 56 exécutions ; dans les 56 la boule est détruite dans les deux ticks qui suivent l'injection (l'injection n'est donc pas inerte) et le mutant donne la clé du témoin à chaque fois.
- **E9-9 équivalent** : le bit `0x40` est déjà posé quand la branche B écrit les drapeaux (les en-têtes des six types portent `0x132154`). Preuve : 6 scénarios atteignables (feu, glace, feu chargé, nord, pente 1, pente 4) donnent la clé du témoin ; avec `0x40` effacé par le banc avant le premier appel d'E9 le mutant **diffère** (il n'est tuable que sur un état inatteignable).

## 3. Les sondes de la DLL (HEAD de départ, câblage temporaire de la section 5 ; `probes/`)

Le banc : `BallRun` et `BallOracle` de f6a, avec trois options jetables (la position du héros, le tir chargé, la recherche des clés de `f6b/oracle/out`) ; le filtre d'arène de D-F4-7 posé sur les types de la boule. Les sources sont versées (`probes/ZzF6bT0Probes.cs.txt`, `ZzF6bT0ProbesP1.cs.txt`, `TempBallAi.cs.txt`, `temp-wiring.diff.txt`), jamais les fichiers du dépôt.

| Sonde | Attendu (écrit au plan) | Mesuré |
|---|---|---|
| **P-F6-1** force verticale d'une flamme posée, au moment de C60 | « lue telle que la DLL la tient ; un écart avec 0 est documenté » | **0** : à chaque appel de C60, `ForceZ` = 0, `ForceX` = `ForceY` = 0, `PosZ` = 2 097 152 (32 px), statut Normal, au sol. L'effet 17 est créé à Z 2 621 441 (40 px + 1 unité brute, `PosZ + 1 + 0x80000`), vitesse nulle : **aucun écart avec l'oracle** (O-F6-4 est sans objet pour cette valeur) |
| **P-F6-2** la 309 se construit-elle dans `ArcRun` | à mesurer | **oui** : `ArcSpec("Meia Dream", "Meia Dream (flashback)-309")` avec arrivée à la tuile (38, 47) (`RealController`, `Prefabs`), 8 entités, trois du type 12 en (948, 672), (960, 664), (936, 664), Z 32, drapeaux `0x132154` au chargement ; **au 1ᵉʳ cadre** le programme A de la DLL a tourné (animation 2, drapeaux `0x132104`, statut Normal). Avec C60 câblé, trois effets 17 apparaissent au cadre 5 puis tous les 4 cadres, comme l'oracle. L'arrivée est posée directement sur la tuile ; le trajet par les cartes 306 et 441 (`Change map`) n'est pas rejoué |
| **P-F6-3** position de l'explosion à 1, 2, 3 ticks par image | x = 166 (feu), 163 (glace) | **égale à l'oracle aux trois cadences** : sur 28 scénarios × cadences (76 exécutions), de la naissance à la fin de la clé, la ligne (statut, X, Z, pente, sol) égale la clé à **73 exécutions sur 76** ; les trois écarts sont `fire11_x107` (voir plus bas) |

- **Les scénarios « mur » au HEAD avec l'e0k5** (« l'effet d'e0k5 sur une boule tirée à bout portant ») : `fire11_flush` (109), `ice14_flush`, `fire12_flush`, `ice15_flush`, `fire11_point_blank` (108), `fire11_near105` : **0 ligne différente de la clé** (1, 2 et 3 ticks par image ; le chargé à 1). Les murs libres (`wall_col6`, `wall_col8`, `step16_col6`, `step16_col7`, `step32_col6`, et les glaces) : 0 ligne différente. **Un écart** : `fire11_x107` (héros à 107, pas de 6 px) : la boule est au statut 3 au tick S+2 au lieu de S+3, l'explosion (10 tirages) un tick trop tôt, aux trois cadences. C'est exactement l'écart « le drapeau d'un tick » de l'e0k5 (D-E20-38, D-E0K5-2 : après l'intégration l'entité repasse par le moteur) : la position de S (`7143423`) est la bonne, le drapeau arrive à S+1 au lieu de S+2. Pour f6b : `fire11_x107` ne se compare qu'à S (ou documente l'écart), comme l'e0k5 l'a fait pour `arrow3_x107`.
- **Le pas du générateur** (clés brutes, un tick par image) : 1 pas par tick plus les tirages du tick, égal à la clé sur tous les scénarios sauf (a) `fire11_x107` (l'écart ci-dessus) et (b) les quatre clés chargées, aux ticks 93 et 95 : la clé tire les deux nombres de l'étincelle de pleine charge du héros au tick 93, la DLL au tick 95 (hors périmètre de f6b : l'état du générateur est égal à partir du tick 96, donc à S = 107).
- **Le point de création des effets** (à relire) : inchangé depuis f4 : la fonction d'IA capture les valeurs puis `world.EffectStage?.Emit(reservoir => ...)` ; les tirages se font à l'application (D-E0B-3(a)) ; les créateurs sont dans `Alundra/Rendering/Effects/NativeEffects.cs`, qui appelle `CreatedForTests` (`CreateChargedArrowTrail` est le modèle) : `NativeEffects.cs` est au périmètre (point 8). Le câblage temporaire suit ce chemin et reproduit la clé (explosion, bouffées, traînée).
- **L'interrupteur d'arène** (D-F4-7) : `EventProgramRunner.NativeSpriteTypeFilterForTests` existe, `[ThreadStatic]`, nul en production ; avec l'ensemble {type de la boule} les gestionnaires C60 et E9 tournent dans `HitArena` (natifs éteints), pas les Beannoïdes. **La règle de vitesse** (D-F5-5) : `ScriptedMotion.TickScriptedNpc` lit `TargetAnimationId` pour `HasLogicalZ` ; sans elle l'explosion serait à 6 à 18 px de trop : mesurée juste aux trois cadences.

## 4. Chemin du fichier neuf et noms (`tools/dll-structure/`, ADR-0075 et ADR-0080)

- Le plan date d'avant R5 : `Alundra/Scripts/AlundraBallAi.cs` devient **`Alundra/Ai/Projectiles/BallAi.cs`**, `namespace Alundra.Ai.Projectiles`, `internal static class BallAi` (modèle `WaveSwordAi.cs`, `ChargedArrowAi.cs`, `ArrowAi.cs`), avec `SpriteTypes` = 10..15, `TickCode` = 60, `DeactivateCode` = 9, `Keys`, `Register(NativeSpriteRegistry)`.
- Tests neufs, dossier miroir `Alundra.Tests/Ai/Projectiles/` : `BallAiTests.cs` (b1 à b4, b7), `BallLifeTests.cs` (b5, b6), `BallFlamesTests.cs` (m1 à m3), `BallProductionTests.cs` (p1 à p4) ; `BallRun.cs` et `BallOracle.cs` existent (f6a).
- Contrôle : ces cinq fichiers (vides) ajoutés au dépôt, `python -B tools/dll-structure/check_names.py --rules` : **0 finding**, 1 avertissement préexistant (`probes/check-names-proposed.txt`) ; retirés.

## 5. Liste prédite des tests qui bougent (câblage temporaire, mesurée)

Le câblage (jetable, `probes/TempBallAi.cs.txt` et `probes/temp-wiring.diff.txt`, annulé : `git diff` vide) : un `BallAi` complet (C60 et E9 d'après le désassemblage, les effets par `Emit`), `BallAi.Register(registry)` dans `NativeSpriteRegistry.CreateForProduction` (phase B), puis l'ensemble `{3, 4, 7, 11, 12, 14, 15, 27, 28, 29}` dans `PlayerManager.ProductionWiredWeaponTypes` (phase C). Suite complète Release (copie neutralisée), `--blame-hang-timeout 60s`, trois partitions disjointes (`suite-wired/runpart.sh`, celles de l'e0k5 ; une passe complète dépasse dix minutes : p1 7,5 min, p2 5 min, p3 1,2 min). 6 719 tests au HEAD (1 668 + 3 085 + 1 966), plus 2 sondes dans p1.

| Phase | Tests qui bougent | Détail |
|---|---|---|
| A (la classe seule, rien d'enregistré) | **0** | aucune clé inscrite hors des tests |
| B (`Register` dans `CreateForProduction`) | **4** (p2 et p3 verts) | les épingles du compte du registre : `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOf...` (le compte `6 + 1 + ... Keys.Count`), `Alundra.Tests.Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses`, `Alundra.Tests.Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_AndItsCountIsTheSumOfTheCountsOfTheClasses`, `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_...` : chacun se réécrit par dérivation (`+ BallAi.Keys.Count`, les deux clés (C, 60) et (E, 9)) |
| C (l'ensemble) | **5** = les 4 de B + `Alundra.Tests.Hero.WeaponSpawnSeamTests.S5_TheProductionSetPassesTheInvariant_TheArrowFailsOnE17InARegistryWithoutIt_AndPassesInTheProductionRegistry` (l'égalité de l'ensemble de production, à dériver des octets de type non nuls des fiches d'arme) | p3 vert |

- **Ce que la contre-vérification du plan prévoyait en plus ne bouge pas** : les 13 cas à boule des tests de monde (`LogicalZWorldTests` Z3 à Z11, `MapStateWorldTests` K2 et K3) restent verts avec C60 et E9 câblés (mesuré ; leurs lectures sont la chute, les forces et le sol de la boule, que C60 et E9 ne changent pas : cette raison est lue dans les noms des tests, non vérifiée test par test). **La phase B ne déplace donc que les 4 épingles du registre** ; il n'y a rien à réécrire « par dérivation du binaire » en dehors d'elles. Le seuil de 10 tests du plan n'est pas atteint (5 au plus). La liste mesurée tient lieu de liste prédite : un test hors d'elle qui bouge à T1, T3 ou T4 est un arrêt.
- Épingles de la clé de vitesse et autres tests cités par le plan (`MapStateTests` P3, `RecordBirthTickTests` B6_R, `WaveSwordWiringTests`, `TestSavesTests`, `FireSwitchRegressionTests`) : verts avec les deux câblages.

## 6. Écarts au plan

1. **R de (p1)** : voir l'arrêt en tête. 102 -> 107 (R = 96), à confirmer par le plan.
2. **`BallAi` et non `AlundraBallAi`** : le chemin et le nom suivent R5 (section 4).
3. **Les mutants** : 27 au lieu de « au moins 14 » ; E9-5b et E9-9 prouvés équivalents par la machine (section 2), et non seulement par lecture.
4. **Des clés en plus** de la liste du plan : les six clés de la branche A (seule `fire11_branchA_water_on_puff_tick` était demandée), les six clés de boule contre un mur (e0k5), les flammes avec le vrai interpréteur, toutes en forme `_pf`.
5. **La contre-vérification annonçait 13 tests déplacés en phase B** : mesuré 0 ; les 4 épingles du registre bougent, dont deux (`LiftableAiRegistrationTests.R1`, `ChargedArrowAiTests.C2`) que le plan ne nommait pas.
6. **L'écart `fire11_x107`** d'un tick (e0k5) : hérité, à documenter par f6b, pas à fermer.
7. **Le trajet par les cartes 306 et 441** vers la 309 n'est pas rejoué (P-F6-2 pose l'arrivée sur la tuile (38, 47)).
