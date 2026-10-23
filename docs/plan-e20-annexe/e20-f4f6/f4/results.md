# E20.f4, T0 (2026-10-23) : résultats

Plan : `docs/plan-e20-heros.md` §1.20 (bloc « Révision au HEAD », points 1 à 10, puces « f4 ») et §1.20.1. HEAD de départ `c7c77eed`, branche `chantier/theme1-porter-plateforme`.
La note « ARRÊT » du 2026-10-22 est levée : E20.e0c et E20.e sont faites (`Alundra/Cells/TileEffect.cs` existe, `WaveSwordAi.CheckAndTriggerTileEffect` appelle `TileEffect.Trigger`).

## 1. Prérequis relus au HEAD

- **L'appel du port.** `Alundra/Ai/Projectiles/WaveSwordAi.cs:40-44` : `StubCallsForTests?.Add(entity); Alundra.Cells.TileEffect.Trigger(entity);` (le journal, puis le port). C94 fera **le même appel**
  par `ChargedArrowAi.CheckAndTriggerTileEffect(entity)` (journal `ChargedArrowAi.StubCallsForTests`, puis `TileEffect.Trigger`).
- **Le banc de coupe.** `Alundra.Tests/Support/CutBench.cs` (S-E5) construit **un seul** jeu d'enregistrements, d'où le champ (`CellsCollisionField.TryCreate`, surcharge à enregistrements) et le magasin
  (`CellStore.TryCreate`) ; ses tableaux sont partagés. f4 le réutilise : `CutBench.Create(cell)` donne `Field` et `Store` ; une aide de test neuve (dans `Support/`, à côté de `FlatCells`) les pose dans un
  monde par réflexion (`WorldProxy.CollisionField`, `WorldProxy.CellMutator`, `World.CollisionField`). Aucune aide de coupe en double.
- **Effet de bris.** `WorldProxy.DestroyEntity(entity, -1)` lit déjà `BreakEffect` de l'en-tête (8 pour le type 7) et émet `NativeEffects.CreateBreakEffect` (E14.a1b) ; E1 est en ligne dans le runner
  (`EventProgramRunner.IsInlineDeactivateCode`). Aucun changement de runner (D-F4-4).
- **Dernier ADR.** 0083 sur la branche courante (0081 sur `main`, rien au-delà sur les 40 branches) : l'ADR de la tranche est 0084 (à recompter à l'écriture, T4).

## 2. L'oracle rejoué (la machine MIPS, le vrai code de `ALUN_CD.EXE`)

- `bin/run_charged.py` et `bin/sweep_wall_charged.py` rejoués depuis deux copies : les neuf clés `charged7_*.json`, leurs traces et `sweep_wall_charged.txt` sont **identiques octet pour octet** à ceux
  versés (frozen). Les 140 sorties de `bin/out` et `f4/oracle/out` donnent un **manifeste identique sur deux exécutions complètes** (`oracle/manifest.txt`).
- Les clés `_pf` (point 3 de la révision) : `charged7_{east_wall,grass,map_edge,vs_target}_pf`. Vérifié : `rng0(t+1) = pas(rng1(t))` à chaque tick des quatre ; les créatures, les positions, les Z, les sons et
  les effets autres que ceux qui dépendent d'un tirage (étincelles de la charge, id 16 ; étincelles de touche, id 9) sont identiques à la clé sans pas ; seules les valeurs tirées changent.
  Les tirages par tick restent ceux de la clé (`draws`, sans le pas) : charge aux ticks 85, 89, 93 (2), touche à 134 (9) ; **la coupe** `charged7_grass_pf` coupe huit fois (ticks relatifs S+2, +6, +10, +14, +18, +22,
  +26, +30) avec 2 tirages à chaque tick de coupe sauf S+26 (3) ; la clé sans pas disait 3, 2, 3, 2, 2, 2, 2, 2 : la valeur du premier tirage décide, d'où la clé refaite.
- **Valeurs lues par les tests** (S = 105, R = 96, ticks relatifs à S) : naissance S, statut 2 de S à S+42, 4 à S+43, case libre à S+44 ; X = 106 + 6 par tick (346 à S+40, 349 à S+41 et S+42) ; Z = 8,0 (la clé
  dit 8,00003 : l'unité brute en plus) ; `fx` 393216 ; drapeau de mur `adj_13C` 1 à S+42 ; boîte 24 × 16 × 15 à `hbz` 0 (0 à 15 en Z absolu, soit −8 relatif) ; `flags6C` `0x32054` ; attaque (131, 5) ;
  traînée id 25 à S+4, +8, …, +40 (dix), x = 124 + 24(k−1), y = 100, z = 16, v = (−3, 0, 0) ; bris `attach(id 8, cible la flèche, mode 1)` à S+43 ; son 33 à S ; aucun tirage de la traînée ni du bris.
  **Les quatre directions** (suivi de la direction, `+0x98` de la clé contre l'indice de la DLL) : est 3 (v = (−3, 0, 0)), ouest 2 (v = (3, 0, 0)), nord 1 (v = (0, 2, 0)), sud 0 (v = (0, −2, 0)) ; position de
  la traînée : celle de la flèche à la fin du tick précédent (x = 124 pour une flèche à 106 + 18).
  **Les 12 départs du mur** (`sweep_wall_charged.txt`, x0 de 96 à 107) : drapeau à S+41, S+42 ou S+43, statut 4 à S+42 à S+44 ; le drapeau est à S+42 pour x0 = 97, 99, 100, 102, 104, 107, à S+43 pour 96,
  98, 101, à S+41 pour 103, 105, 106 (D-F4-9, règle « arrivée + 1 »).
- **Les 13 mutants de l'oracle** (`oracle/out/mutants_f4.txt`), **tous tués** : C94-1 (période 3 → 7), C94-2 (id 0x19 → 0x18), C94-3 et C94-3b (décalage de la vitesse X et Y), C94-4 (décalage de Z),
  C94-5 et C94-5b (direction, héros à l'est puis au nord), C94-6 (l'appel de `CheckAndTriggerTileEffect` retiré : plus de coupe, clé herbe), E1-1 (sans effet de bris), HDR-1 (drapeau `0x20` posé dans l'en-tête :
  la flèche ne traverse plus), HDR-2 (`BreakEffect` 8 → 0), FICHE-1 (octet z de la fiche 8 → 16), SPAWN-1 (le `+1` du Z de création retiré). Aucun mutant équivalent.

## 3. Les sondes de la DLL (au HEAD de départ, sans code de f4 ; `probes/`)

Le banc : `HitArena` (carte 14 réelle, le héros à (100, 100), tourné à l'est), le champ synthétique et son magasin du scénario posés par réflexion (`CutBench`), les autres entités retirées
(`Status = FlagToDestroy` puis `RecycleDestroyedEntities`), les gestionnaires natifs allumés, le chemin de production (crochet nul) avec l'ensemble `{7}` (`WiredWeaponTypesForTests`), l'arc de William
par `Give(6)` puis `Equip(2)`, Carré tenu aux ticks 1 à 95, relâché au 96, le générateur posé au germe `0x12345678` et `FrameCounter` du héros posé à 12 en tête du tick 1 (comme E20.e), un tick par image.

| Sonde | Attendu (écrit avant) | Mesuré |
|---|---|---|
| Banc : relâchement et naissance | 96 et S = 105 (décalage d = 0) | le type 7 naît au tick 105, fille du héros, à la case 1 (la plus basse case libre avant le tir) ; l'appel du tir de type 4 (début de la prise) est journalisé et ne crée rien (4 hors ensemble) ; d = 0 |
| Banc : vie sans C94 | la flèche vit sa vie native (E1 en ligne, mur, bord) | statut 2 de S à S+42, drapeau de mur à S+42, statut 4 à S+43, absente à S+44 ; X = 106 + 6/tick, 349 à S+41 ; bord de carte (x = 1150) : drapeau à S+14, X = 1237, statut 4 à S+15, absente à S+16 : **identiques aux clés** |
| Banc : aucune touche ni étincelle | sur `east_wall`, `map_edge`, `grass`, banc vidé et sans cible : 0 touche, 0 étincelle de touche | 0 et 0 aux trois |
| Banc : champ et magasin partagés | une coupe par le `CellMutator` du monde efface le bit que le champ lit | bit `0x02` de (7, 5) : lu 0x2 avant, 0x0 après la coupe ; le mutateur du monde est bien le magasin du banc |
| Pas par image (point 3, révision 4) | après chaque image : état de la DLL = `rng0(t+1)` de la clé `_pf` et pas de la DLL = `draws` + 1 | **0 écart** sur `east_wall_pf` (150 images) et `map_edge_pf` (135) : états identiques, pas = tirages + 1 à chaque image, charge comprise. Sur `grass_pf`, des écarts à partir du tick 107 : attendu, la coupe n'est pas encore branchée (aucune coupe, aucun tirage ; ils tombent avec f4) |
| Effet 25 (banque globale) | l'étage d'effets du monde crée l'effet global 25 | créé (animation 3) : l'inconnu « banque d'effets » du relevé est levé |
| P-F4-1 | indice de direction = 0, 1, 2, 3 pour sud, nord, ouest, est (table inverse de `{0, 0x10, 8, 0x18}`) = `BoxDirection` | `TargetDirection` 0, 16, 8, 24 ; `BoxDirection` et `AnimationDirection` 0, 1, 2, 3 : **égaux à l'attendu** aux quatre directions |
| P-F4-2 | boîte de la flèche au tick S : 24 × 16 × 15 à `hbz` 0 | largeur 1 572 863, profondeur 1 048 575, hauteur 983 039 (24, 16, 15 pixels moins un brut), décalage (−12, −8, −8) pixels, `HitBoxZ` 0, drapeaux `0x32054`, `BreakEffect` 8 : **conforme** |

Autres relevés : `FrameCounter` 1 à S (le compteur d'une entité née vaut 1 à la fin de son tick de naissance, comme `cnt1c` de la clé) ; les trames de la traînée tombent donc quand `FrameCounter & 3 == 0` à
l'entrée du tick (S+4 : compteur 4).

## 4. Liste prédite des tests qui bougent, recomptée au HEAD de départ

Mesurée en posant temporairement, sur le HEAD, une clé (C, 94) pour le type 7 (gestionnaire qui journalise le test appelant) **et** 7 dans `ProductionWiredWeaponTypes`, puis la suite complète Release
(l'édition est annulée, `git diff` vide) : trois échecs, un test qui exécute le gestionnaire.

| Test | Phase | Raison |
|---|---|---|
| `Alundra.Tests.Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn` | A | l'épingle du registre (`:57`) : 6 + 1 + clés des objets, plus la clé (C, 94) |
| `Alundra.Tests.Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsNineKeys_…` | A | la seconde épingle (`:181-183`, neuf clés), même dérivation |
| `Alundra.Tests.Hero.WeaponSpawnSeamTests.S5_TheProductionSetPassesTheInvariant_TheFakeSetWithTheArrowFailsOnE17` | A (clause `Contains((C, 94))`, `:257`) puis B (égalité de l'ensemble de production, `:243`) | le type 7 passe à « géré » |
| `Alundra.Tests.Entities.BirthTickTests.TheSoundOfTheCreation_IsRequestedOnce_WhenTheEntityTakesPartInTheTickOfItsBirth` | A (reste vert à surveiller) | il fait naître un type 7 par l'interface et fait tourner trois fois C94 : verte avec un gestionnaire neutre, à relire avec le vrai |

Aucun autre test ne fait tourner C94 ni ne dépend de l'ensemble de production. **L'épingle de S1** (`:83`, `:85-95`) et le (p5) de f3c (`WaveSwordWiringTests`) ne sont **pas** touchés par f4
(le plan les range en f5). Le total est 4, sous le seuil d'arrêt (plus de 10).

## 5. Comptes de départ

`Alundra.Tests` en Release (construction avec `skipcopy.targets`) : **5 330 réussites, 0 échec**, 6 min 55 s. (Debug : relevé à la fin de T1.) Tests neufs prévus : le plan donne la liste (c1)-(c9) ; le compte final se lit à la fin de T1.

## 6. Écarts au plan

1. Les noms des fichiers de tests des épingles ont changé avec la restructuration : `Ai/NativeSpriteRegistryTests.cs`, `Ai/Projectiles/WaveSwordLifeTests.cs`, `Hero/WeaponSpawnSeamTests.cs` (miroir de R5).
2. Le plan demande des clés « `.effects[]`, `.snd[]` » dans l'ancien T0 : abandonné par la révision (les tests lisent les chaînes `fx(...)` et `snd(N)` des clés versées).
3. `HDR-1` et `FICHE-1` : la fiche de William et l'en-tête sont corrompus dans la RAM de la machine (jamais dans un test) ; `FICHE-1` écrit l'octet z de l'enregistrement de tir pointé au tick 96.
4. Les sondes P-F4-1 et P-F4-2 ne demandent pas de gestionnaire : une entité de type 7 née par l'interface vit sa vie native (E1 en ligne, mur, bord) ; ce que f4 ajoute est la traînée, l'appel de la
   coupe, la clé du registre et l'ensemble.
