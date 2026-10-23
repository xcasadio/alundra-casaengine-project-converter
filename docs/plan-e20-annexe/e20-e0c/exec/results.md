# E20.e0c, T0 : annexe, oracle, mesures (2026-10-22)

HEAD de départ `916f0374` (branche `chantier/theme1-porter-plateforme`), binaire `ALUN_CD.EXE` (France) SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`. Aucun code de la DLL n'est
écrit par T0. Les sondes de la DLL (`probes/ZzT0Probes.cs.txt`) ont tourné dans un fichier de test jetable, retiré avant le commit. Tout ce qui suit est mesuré, sauf mention.

## (a) Rejeu des modèles et des harnais, depuis une copie

- `SHA1SUMS.txt` de l'annexe (110 fichiers) : `sha1sum -c` donne 110 OK (rien du dossier versé n'a bougé).
- `work/` rejoué dans une copie (le binaire est lu au chemin absolu que `exelib.py` nomme) : `replays/replays-work.txt`.
  `t_choose` 11 680 vecteurs 0 écart, 38/38 ; `t_spawn` 12 000 + 12 000, 0 écart, 62/62 et 59/59 ; `t_init` 20 000, 0 écart, 57/58 ; `t_a255` 15 671, 0 écart, 79/80 ;
  `t_give` 30 000, 0 écart ; `t_destroy` 8 000, 0 écart, 43/43 et 75/75 ; `t_c255` (graine 3) 6 000 vecteurs, 31 256 ticks, 0 écart, 381/381 ;
  mutants des modèles 23/25 tués (2 équivalents), mutants de C255 46/46, mutants de la récursion 7/11 (2 équivalents, 1 sans effet, 1 repère non trouvé), comme le rapport.
  `census.py` et `census2.py` : sorties identiques (fins de ligne normalisées) à `census_out.txt` et `census2_out.txt`.
- `posed-rev3/` rejoué dans une copie : `census_placed`, `census_exported`, `run_c255` (huit scénarios), `placed.json` et `fdis.py 0x8007C174` : sorties identiques aux fichiers versés ;
  `t_c255.py` : 0 écart, 381/381.
- Oracle d'h1 (`docs/plan-e20-annexe/e20-h1/disc/oracle/`), rejoué dans une copie : `replay_h1.py` et `check_model.py` : sorties identiques à `out/replay_out.txt` et `out/check_model_out.txt`
  (2 649 vecteurs, 0 écart).

## (b) P-L1 : le premier tick d'un objet lâché dans la passe des programmes d'une autre entité

Sonde `PL1_PL2_TheDroppedObjectsFirstTicks` sur la carte 14 (monde réel, prefabs réels). Une flèche (type 3) joue de programme C ; à son premier tick C elle fait naître l'objet du type 99
(`0x45 + 0x1E`, les gildas) par `SpawnEntityBySpriteIndex` à sa position, puis écrit ce que `SpawnContents` écrit (`ForceZ = 0xA0000`, `Collidable` retiré). Le tick de la mort est T.

| cas | case de la flèche | case de l'objet | programme A de l'objet | premier C de l'objet | `IsOnGround` au premier C | la montée (1re physique qui le déplace) |
|---|---|---|---|---|---|---|
| plus haute | 20 | 25 | T + 1 | T + 2 | 0 | physique de T + 1 (PosZ 2 -> 622594 vu à T + 2) |
| plus basse | 22 | 20 | T + 1 | T + 2 | 0 | physique de T + 1 |

Résultat : un tick après la mort dans les deux cas (le programme A de l'objet, qui tourne en T + 1, est son premier tick ; le C255 suit à T + 2). Cela confirme le défaut de Q-E0C-8 (case
plus haute : un tick de retard sur le binaire, qui fait naître l'objet au tick même) et BT-L2 (case plus basse : égal au binaire). Rien ne contredit V6 : pas d'arrêt.

## (c) P-L2 : le premier `IsOnGround`

Même sonde : `IsOnGround` vaut 0 à la création, 0 au programme A (T + 1), 0 au premier C (T + 2), et 0 ensuite tant que l'objet monte (6 ticks observés). Le binaire : 0 au premier tick de C255.
Égal. Pas d'arrêt. (Le rebond ne démarre donc pas au premier tick : pas de rebond parasite à `0x78000`.)

## (d) Où la DLL rafraîchira `ModdedPos`

Aujourd'hui `ModdedPosX/Y/Z` n'est écrit qu'à la création (`EntitySpawnFactory.cs:825-827`) et à la copie (`EntityScriptProxy.cs:2684-2686`) ; aucun lecteur (9 références, déclaration et
commentaires). Le binaire le réécrit en tête de `0x80038364` (`0x800383C0-0x800383E0`, `Pos + (+0x1E4/+0x1E8/+0x1EC)`) pour les entités de la liste active (états 2 et 3, non bloquées), AVANT le
déplacement, après l'horloge d'animation (`0x80038E18`). L'équivalent dans la DLL est `WorldProxy.RunEntityBlock`, entre la boucle `TickAnimationClock` (passe « clock ») et la boucle
`TickPhysics` (passe « physics », `TickTraceForTests?.Invoke("physics")`) : une boucle sur `_tickEntities` (le héros en tête), pour chaque entité active (`Status.IsActive()`) dont
`BlockedByEntity` est nul, `ModdedPosX = PosX + ModX`, `Y`, `Z`. Les lecteurs (C255 et le contact) voient ainsi la position de la fin du tick précédent, comme l'original. Phase B.

## (e) Liste prédite des tests qui bougent, par phase

Aucun test existant n'est lu comme déplacé par la phase A (membres additifs : colonnes de `ItemTables`, `DialogueDirector.OpenComposed`, créateurs de `NativeEffects`, membres par défaut de
`IEntityWorldContext`, fichiers neufs) : zéro. Le compte de départ de la suite est 5 098 tests en Release, tous verts (mesuré au HEAD de départ).

- **Phase B (T2)** : 2 tests.
  1. `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs`, `ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn` (`6 + 1` devient `6 + 1 + 2`).
  2. `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs`, `V3_TheProductionRegistryHoldsSevenKeys_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem` (`3 + 3 + 1` devient `3 + 3 + 1 + 2`).
  Le rafraîchissement de `ModdedPos` ne bouge aucun test (aucun lecteur). Les mondes réels qui portent des objets 31 à 118 n'exécutent, en phase B, que le C255 du lâché ; la branche posée et le coffre sont des bouchons.
- **Phase C (T3)** : 5 tests certains, 2 familles d'arcs à mesurer.
  3. `Alundra.Tests/Rendering/Effects/DestroyEntityBreakEffectTests.cs`, `TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` (épingle le bouchon).
  4. `Alundra.Tests/Ai/Enemies/BeannoideOracleTests.cs`, `E1_TheVectors_FollowTheProjectionOfThePlan` (`Assert.Empty(SpawnedEntities)`, 160 entrées exclues).
  5. `Alundra.Tests/Ai/Enemies/BeannoideOracleTests.cs`, `E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan` (même aide `RunE1Call`).
  6. `Alundra.Tests/Ai/Enemies/MurutaOracleTests.cs`, `E1_TheVectors_FollowTheProjectionOfThePlan`.
  7. `Alundra.Tests/Ai/Enemies/MurutaOracleTests.cs`, `E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan`.
  À mesurer, prévision non confirmée : les arcs `Alundra.Tests/Arcs/BeannoideGrateArcTests.cs` (`G116_…`) et `Alundra.Tests/Arcs/MurutaHallArcTests.cs` (tuent des ennemis dont le butin tiré à la
  création n'est pas nul : un objet naît, le décompte d'entités et le générateur bougent). Plafond du plan : 10 ; la prévision compte au plus 7 + ces arcs.
- **Phase D (T4)** : 0 test prévu. Les tests qui posent `BlockedByEntity` (`Hero/HeroChargeTests.cs:640`, `Hero/HeroLifeTests.cs:332` et `:574`, `Hero/PlayerManagerTests.cs:134`, `Maps/WorldProxyEventPassTests.cs:194`,
  `Events/ClassAndBranchOpcodesTests.cs:203`, `Combat/HitOpcodesTests.cs:332`, `UI/Inventory/InventoryDirectorTests.cs:244`) appellent des fonctions isolées ou le choix de programme, pas l'horloge ni la
  physique de `RunEntityBlock` sur une entité bloquée : à mesurer à la phase D. Aucun test ne charge la carte 11 (la Debug zone) ; aucun arc ne se tient sur un objet posé à A255 natif.

## (f) Vecteurs de C255 exportés (`vectors/c255.json`)

424 vecteurs, 1 477 ticks, `+0x274` 0 (posé) et 1 (lâché), le coffre exclu. Chaque vecteur : l'objet, l'entité (mots `+0x274`, `+0x278`, `+0x27C`, `+0x280`, `+0x284`, `+0xB8`, `+0x144`, `+0x1CC`,
positions `+0x1D8..` et tailles `+0x1F0..` en mots du binaire), le héros (positions et tailles), la gravité (`u16[[0x800E4334]+4]`), pour chaque tick la réponse « dialogue ouvert » (`0x80045004`), `CdBusy`
et le retour de `GiveItem`, l'écriture de `IsOnGround` entre deux ticks, et deux journaux par tick avec l'état de l'entité après le tick : `full` (le code réel, `CdBusy` tiré) et `projected`
(la fanfare en bouchon : `CdBusy` toujours faux, `bgmStop` devient `fanfareStart`, la piste CD est omise, `0x80049AF4` devient `fanfareEnd`). Couvre : états 0 à 3, clé (octet 5 nul) et non-clé, dialogue ouvert,
`CdBusy`, 59 ramassages (dont 16 d'objets-clés). Journal : `give`, `strcpy`/`strcat` (`name`, `etc`), `open`, `closeMask`, `sound`, `setGameFlag`, `block`, `unblock`, `destroyLight`.
Générateur : `scripts/gen_c255.py` (vrai code, graine fixe).

## (g) `GiveItem` de tous les objets posés

- Les corps de soin de GiveItem (vrai code, `vectors/give-item.json`, 228 vecteurs, 19 objets : `0x24` `0x25` `0x26` `0x27` `0x29` `0x45` `0x46` `0x47` `0x48` `0x4F` `0x51` `0x52` `0x53` `0x54` `0x55` `0x56` et trois
  ajouts simples `0x04` `0x21` `0x3D`) : par vecteur l'entrée (inventaire, PV, PM, argent, graine, position du héros, nombre de créations d'effets qui réussissent), le retour, les PV, PM, argent, compteur,
  graine et le drapeau 1450 après, les sons, et la file des effets dans l'ordre de création (animation, position d'entrée, et les champs que le code laisse : position, forces). Maxima d'inventaire lus au vrai code :
  herbe 9, potions et extrait 1, argent 0 (sans inventaire), clé 99, objet-clé de l'épée 1.
- `scripts/check_h1_bodies.py` : 40 vecteurs où un corps de soin s'exécute (`0x24` plein, `0x25` plein, `0x27` plein, `0x29` plein, `0x52`, `0x56`) rejoués sur MON modèle d'h1 (`model_h1.py`, membre par membre) :
  PV, PM, graine, créations (arguments) et champs de chaque effet : 0 écart (`replays/check_h1_bodies_out.txt`). Aucun corps de soin ne diffère du modèle d'h1 : pas d'arrêt.
- La table des sauts sur les 53 types (`vectors/give-jump-table.json`) : 53 types, 152 objets posés, 59 natifs. 42 types vont à « ajout simple » (`0x8004E530`) ; les 11 autres : herbe `0x24` (9), Potion L `0x25` (3), Extrait magique `0x26` (6),
  Breuvage `0x27` (2), Potion S `0x29` (6), argent 5 `0x46` (8), argent 10 `0x47` (1), Petit cristal `0x51` (1), Grand cristal `0x52` (1), vase de vie `0x53` (16), Cœur moyen `0x55` (10). Ni `0x4F` ni `0x50` n'est posé ni lâché ;
  `GiveItem` les porte donc en bouchons nommés épinglés par la garde de données (p10).
- Les clés des étincelles « dans l'ordre de la file » : la file crée les effets dans l'ordre de `GiveItem` ; chaque création qui réussit tire ses nombres à l'application (2 pas par étincelle : `hi(r1 * 0x20001) - 0x10000`,
  `hi(r2 * 0x30001) - 0x18000`) ; le vecteur porte l'état S d'entrée et la liste complète (`effects[].fx/fy/fz`, `seed` de sortie).

## (h) Le tampon du message, et la voie d'ouverture

- `scripts/gen_message.py` lit l'écriture brute (ETC_RES.R, `data-extracted/data/ETC_RES.R.json` par `EtcIndexTable.csv`) et le texte compilé de la DLL (`Dialogues/Etc.dialogue`) :
  l'ETC `0x45` brut est `" gagné!"` AVEC UN ESPACE EN TÊTE (offset 3664) ; le texte compilé est `"gagné!"` (le compilateur Yarn rogne l'espace de tête ; `Etc_0070` garde le sien par un marqueur).
  Le tampon du binaire est donc `nom + " gagné!"`, par exemple « Herbes gagné! ». La DLL doit remettre l'espace entre le nom et le texte compilé.
  Écart de détail : les noms bruts des objets `0x06` (« Arc en saule ») et `0x39` (« Passe secret ») portent un espace de fin ; compilé, il est perdu. Le tampon du binaire en a donc deux (nom + espace + espace de l'ETC) ;
  la DLL en aura un. Seul `0x06` est un objet posé natif (l'Arc de William, 1 des 59) ; écart noté, invisible.
- Voie retenue, sans toucher au moteur : un membre additif de `DialogueDirector` (classe de la DLL), `OpenComposed(string font3Text, int controlMode)`, qui fait ce qu'`Open` fait (`OpenSerial`, remise à zéro de la page,
  `_box.Open()` avec le son 6, `ApplyControlMode`, une page) mais donne la page directement : `OnPageShown(new DialogueLine(text))` (le texte est déjà du texte font3, il n'a pas de marqueurs), sans lecteur Yarn.
  `DialogueLine` est une classe du moteur déjà utilisée telle quelle ; rien n'est changé dans le moteur.

## (i) Où la DLL lit `BlockedByEntity`, et ce qu'elle fait d'une entité bloquée

- Lecteurs (lecture du code) : le choix du programme (`EntityScriptProxy.cs:1680`), la liste des coups (`WorldProxy.cs:3062`, `:3072`), le héros (`PlayerManager.cs:321`, `:1424`), la recherche
  (`EventProgramRunner.cs:2644`), les attaquants (`HitOpcodes.cs:69`), l'inventaire (`InventoryDirector.cs:414`). Non lecteurs : l'horloge d'animation (`TickAnimationClock`) et la physique (`TickPhysics`) de `RunEntityBlock`.
- Sonde `PI_WhatABlockedEntityStillDoes` (carte 14, trois flèches de cases 20, 21, 22 et le héros ; la flèche du milieu, au tick 2, bloque les autres et le héros) :
  - l'entité de rang INFÉRIEUR (20) a déjà joué son programme au tick du blocage ; celle de rang SUPÉRIEUR (22) ne le joue PAS au tick du blocage (son choix de programme vient après) ; ni l'une ni l'autre ne joue ensuite.
    Le binaire choisit les programmes de toutes les entités avant de les exécuter : une entité de rang supérieur y jouerait encore au tick du blocage. Écart connu (risque 2 du plan) ; à épingler en phase D.
  - l'horloge d'animation d'une entité bloquée continue de compter (`BoxTicks` 2, 3, 4, 5, 6, 7 sur les ticks) : à corriger en phase D (D-E0C-6).
  - le héros bloqué EN L'AIR continue de tomber (PosZ 13 074 432, 13 008 896, 12 910 592, …, décroissant tick après tick) : la physique ne lit pas `BlockedByEntity`. Le binaire sort une entité bloquée de la liste
    active que lit la physique : le héros resterait suspendu. À corriger en phase D (D-E0C-6).

## (j) Le héros sur un objet posé aujourd'hui, et ce qu'A255 y change

Sonde `PJ_TheHeroOnAPosedObject`, carte 11 (Debug zone), enregistrement 22 (Potion S, type 71, pixel (660, 248), `Flags = 0x00230180`, `Collidable` levé, taille 20 x 14 x 1 048 575) :
- Aujourd'hui le héros lâché de 100 unités au-dessus atterrit sur l'objet : PosZ 11 534 336 (= dessus + 1), `RidingEntity` non nul. Tous les objets posés de la Debug zone ont `Collidable`.
- Avec `Collidable` retiré (ce qu'A255 fait aux objets posés, `+0x6C &= ~0x80`) : PosZ 10 485 760 (le sol), `RidingEntity` nul.
Conséquence en phase D : le héros ne peut plus se tenir sur un objet posé natif ; il le traverse en le ramassant (comme l'original). Visible : le test (p11) l'épingle.

## (k) Préréglages de recette

Cases d'arrivée validées par les vraies règles de sauvegarde (`TestSaves.TryBuild`, sonde `PK_ThePresetsOfTheRecipe`) et chargées en monde réel (`PL_TheHeroTilesOfThePresets`).
- `pickup-m11` : carte 11 (Debug zone), case (27, 18, 10). Le héros y repose au sol (PosZ 10 485 760). Vers le nord, sur la même colonne : l'herbe (27, 16), la Potion S (27, 15), la Potion L (27, 14) ; l'Extrait magique est en (29, 15),
  le Breuvage en (29, 14), la Clé en (24, 20). Les 22 objets-clés de la Debug zone sont dans les rangées 22 à 30.
- `loot-m14` : carte 14, case (19, 34, 3). Un Beannoïde (enregistrement 1, case (22, 34, 3), butin tiré 71 = `0x47`, 10 gildas, code d'en-tête `0xFC`) est à 72 pixels à l'est, même hauteur ; l'enregistrement 6 (case (13, 30, 5)) tire aussi 71.
  Les autres tirent 0 à ce chargement (graine d'arc) : en jeu le tirage dépend du générateur, tuer plusieurs.
- La carte 1 (Cœur moyen, entité 24) : l'entité est à apparition forcée (`auto` faux) ; elle n'est pas dans le monde au chargement (sonde : `EntityByRecord(24)` vide). Pas de préréglage de recette ; le Cœur moyen se recette en
  Debug zone. Le site qui la fait naître sur la carte 1 est à trouver dans les programmes de la carte (hors T0).
Les deux préréglages sont des entrées de `Alundra/SaveGames/TestSaves.cs` à écrire en T4/T5 (arme 1, PV 4/10, PM 2/4 pour que soins et cristaux se voient).

## ADR

Prochain numéro d'ADR libre sur toutes les branches : **0082** (le plus haut présent est 0081 sur `main`, `origin/main`, `chantier/restructuration-dll-r3`, la branche courante ; aucune branche ne porte 0082).
À noter pour T5.

## Faits nouveaux, écarts au plan

1. L'ETC `0x45` brut commence par un espace que le compilé n'a plus (h) : le message est `nom + " " + "gagné!"`. Le plan disait « sans espace avant `!` », ce qui reste vrai ; l'espace est avant « gagné ».
2. Deux noms bruts portent un espace de fin perdu au compilé (h) : écart de détail.
3. Le premier tick C d'un objet lâché est T + 2 (A à T + 1) dans les deux cas de case (b) ; le plan disait « un tick après la mort » pour le premier tick qui tourne : c'est A (qui rend la main), C255 vient au suivant.
4. Le héros bloqué tombe encore, l'horloge d'animation d'une entité bloquée compte encore (i) : la phase D les corrige (D-E0C-6).
5. Un test de carte posée par A255 natif : le héros ne se tient plus sur l'objet (j).

## Synthèse par tâche (mesurée)

| tâche | commit | tests de la suite (Release, copie neutralisée) | échecs | fichier |
|---|---|---|---|---|
| départ (HEAD `916f0374`) | | 5 098 | 0 | |
| T1 phase A | `9e43e464` | 5 153 | 0 | `t1-results.md` |
| T2 phase B | `173ffdb9` | 5 224 | 0 | `t2-results.md` |
| T3 phase C | `d99df16e` | 5 235 | 0 | `t3-results.md` |
| T4 phase D | `f4eb9429` | 5 273 | 0 | `t4-results.md` |
| T5 (aucun code de la DLL) | voir les commits de T5 | 5 273 | 0 | cette section |

## T5 : ADR, mutants, suites, gardes (2026-10-23)

HEAD de départ `f4eb9429`, branche `chantier/theme1-porter-plateforme`. Aucun code de la DLL n'est écrit par T5 : `git diff` de `Alundra/` et d'`Alundra.Tests/` vide à la fin (les mutants sont annulés octet pour octet).
Les deux verifiers frais (dont celui sur les épingles) ne sont pas lancés ici : c'est la session principale.

### ADR

Numéro libre recompté sur toutes les branches locales et les `refs/` (`git ls-tree` de chaque ref) et sur les worktrees : le plus haut est 0081, aucun 0082 nulle part. L'ADR est
`docs/decisions/0082-the-loot-drop-and-the-pickup-of-objects-are-ported-with-named-stubs-for-the-fanfare-the-chest-and-flagged-objects.md` (Accepted, 2026-10-23), avec sa ligne dans l'index `docs/decisions/README.md`.
Commit `a41b4c0e`. La ligne « Decisions: see ADR-0082 » du plan n'est pas ajoutée : `docs/plan-e20-heros.md` ne se modifie pas dans cette tâche (à faire par la session principale).

### Mutants du code de la DLL

37 mutants (la liste du plan est couverte en entier, plus `drop_z_not_minus_one`, `drop_sound_omitted`, `life_mark_omitted`, `bounce_without_reset`, `block_without_the_hero` etc.), **tous tués** : `t5-mutants.txt` (nom, nombre de tests en échec, deux premiers noms),
`scripts/t5_mutants.py` (les mutations, appliquées et annulées par copie de sauvegarde, plusieurs remplacements possibles par mutant) et `scripts/t5_runmut.sh` (applique, construit en Release avec la copie neutralisée, lance les 12 familles de tests de la tranche, annule).
Filtre : `PickupAi`, `LootDrop`, `ItemEffects`, `HeroHeals`, `WorldProxyBlock`, `DestroyEntityBreakEffect`, `NativeSpriteRegistry`, `BeannoideLootArc`, `DialogueDirectorTests`, `ItemTablesTests`, `BeannoideOracleTests`, `MurutaOracleTests`.
Correspondance avec la liste du plan :

| liste du plan | mutant |
|---|---|
| veto oublié | `veto_forgotten_contents`, `veto_forgotten_random` |
| veto avant le tirage | `veto_before_the_roll` |
| `+0x278` toujours 600 | `life_always_600` |
| seuil du rebond `<` pour `<=` | `bounce_threshold_le` |
| rebond sans remise à 0 | `bounce_without_reset` |
| décompte qui détruit à 1 | `life_destroys_at_1` |
| marque à 120 oubliée | `life_mark_omitted` |
| recouvrement `<=` pour `<` (chaque sens) | `overlap_hero_le`, `overlap_object_le` |
| positions du tick courant | `overlap_positions_of_the_current_tick` |
| ramassage pendant un dialogue | `pickup_during_a_dialogue` |
| état 3 qui n'attend pas la fermeture | `state3_does_not_wait_for_the_box` |
| plafond d'argent | `money_cap_removed` |
| cœur moyen à `PVmax/2` | `medium_heart_at_half_of_hp_max` |
| étincelles créées tout de suite (retourné) | `sparks_created_at_once` |
| lâcher à la destruction légère | `drop_in_the_light_destruction` |
| lâcher après l'état 4 | `drop_after_the_status_4` |
| message omis pour un posé / présent pour un lâché | `message_omitted_for_the_placed`, `message_present_for_the_dropped` |
| clé et non-clé permutées | `key_and_ordinary_swapped` |
| modes 0 et 1 permutés | `modes_0_and_1_swapped` |
| masque 4 non écrit / masque 3 non écrit | `mask_4_not_written`, `mask_3_not_written` |
| état 2 atteint par un non-clé | `ordinary_reaches_the_state_2` |
| `Block` oublié / sans le héros | `block_forgotten`, `block_without_the_hero` |
| `Unblock` oublié | `unblock_forgotten` |
| entité bloquée qui tourne son horloge / sa physique | `blocked_entity_runs_its_clock`, `blocked_entity_runs_its_physics` |
| `SetGameFlag(-1)` porté | `set_game_flag_minus_1_ported` |
| A255 posé sans `~0x80` | `a255_without_not_0x80` |
| A255 inscrit sur le type 30 | `a255_registered_for_the_chest` (la liste `SpriteTypes` sert A255 et C255 ; tué par le registre et P8) |
| inventaire d'abord inversé | `inventory_first_inverted` |
| bouchon de la fanfare qui attend | `fanfare_waits_for_cd_busy` |

Mutants du plan sans mutant propre : aucun. Écart de lecture : « veto avant le tirage » est lu comme un veto qui annule avant que le générateur ne bouge (le veto dépend de l'objet tiré, il ne peut venir avant le tirage autrement) ; « plafond d'argent » est mesuré sur le chemin de `GiveItem` (l'objet `0x47` ajouté sans le plafond de `AddMoney`, qui n'est pas du code d'e0c).

### Suites

| suite | tests | échecs |
|---|---|---|
| Release, copie neutralisée (build puis test, `--blame-hang-timeout 60s`) | 5 273 | 0 |
| Debug, en dernier | 5 273 | 0 |

5 273 = 5 098 + 55 (T1) + 71 (T2) + 11 (T3) + 38 (T4). `docs/hero-trace-389-*.txt` (4 fichiers) réécrits en fins de ligne par les suites : `git diff --ignore-cr-at-eol --quiet -- docs/` rend 0 ; restaurés par `git checkout --` (après chaque suite), jamais indexés.

### Gardes

- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identiques (construction Debug de 02:38 le 2026-10-23, après les mutants).
- `python -B tools/dll-structure/check_names.py --rules` : 211 fichiers de DLL, 428 de tests, 0 trouvaille, 1 avertissement (celui du départ, `TextBoxGpu.cs:266`).
- `git diff 916f0374 HEAD` : `Alundra/Entities/EntityScriptProxy.cs`, le moteur (`CasaEngineMonogame`, pointeur de sous-module), `alundra-datas-analyser`, `data-extracted/`, `alundra-project/` et le convertisseur : aucun changement (les seuls chemins touchés sont sous `Alundra/`, `Alundra.Tests/` et `docs/`).

### Tests existants déplacés, contre la liste prédite de T0 (e)

Mesure : les tests présents à `916f0374` dont le texte a changé entre `916f0374` et `HEAD` (signature ou corps), `git diff 916f0374 HEAD` sur les sept fichiers de test modifiés.

| test (à `916f0374`) | prédit | mesuré |
|---|---|---|
| `NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheSevenHandlers…` (renommé `…HoldsTheHandlersOfTheBeannoid…AndThePickupObjects…`) | phase B | oui |
| `WaveSwordLifeTests.V3_TheProductionRegistryHoldsSevenKeys…` (renommé `…NineKeys…`) | phase B | oui |
| `DestroyEntityBreakEffectTests.TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` (réécrit, renommé) | phase C | oui |
| `BeannoideOracleTests.E1_TheVectors_FollowTheProjectionOfThePlan` | phase C | oui |
| `BeannoideOracleTests.E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan` | phase C | oui |
| `MurutaOracleTests.E1_TheVectors_FollowTheProjectionOfThePlan` | phase C | oui |
| `MurutaOracleTests.E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan` | phase C | oui |
| `TestSavesTests.TheNinePresets_AreListed_InTheOrderOfTheTable` (renommé `TheElevenPresets_…`) | **non prédit** | oui (T4 : les deux préréglages de recette de T0 (k) allongent la liste) |

Les arcs `BeannoideGrateArcTests` et `MurutaHallArcTests` (« à mesurer ») n'ont pas bougé : aucun changement dans le diff. 8 tests existants bougent, 7 prédits + 1 non prédit (plafond du plan : 10, non atteint). Aucun arc ni trace ne bouge.
Le compte des tests déplacés de `t4-results.md` (« trois ») comptait deux tests nés en T2 (`W7_…`, `ADroppedKeyObject_…`), absents de `916f0374` : ils ne sont pas des tests existants au sens de cette mesure ; le seul test existant de T4 est celui des préréglages.
