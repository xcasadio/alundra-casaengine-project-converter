# Critique indépendante de la préparation E19.sm3 (0xA9 0xAA 0xB2 0xB4 0xB5 0xB6 0xB7 0x9E)

Dépôt lu à `2b0c3f8b`, sans diff sous `Alundra/`, `Alundra.Tests/` ni `alundra-project/`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, identique à celui de la préparation. Aucun build, aucun test .NET, aucun git en écriture. Écriture uniquement sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3-critic/` (noté `CR/`).

## 0. Verdict

- **Le noyau tient.** Ce sont la sémantique binaire des 8 gestionnaires, les 837 vecteurs, le correspondant DLL, la liste de corpus (42 lignes, comptes avant et après), les valeurs « rouge aujourd'hui » et les faits statiques des arcs. Aucun P0, aucun P1.
- **Un P2** : les preuves de retrait et la liste « code de test existant touché » sont fausses ou incomplètes (C1).
- **Cinq P3 et plusieurs P4**, sections 3 et 4.
- La préparation reste utilisable après les corrections C1 à C6. Elle n'est pas exécutable avant ses prérequis externes : sm0 faite, R5 et R7 faits, Q-E19-SM1 répondue.

## 1. Méthode (tout est dans `CR/`)

- Mon désassemblage capstone des 8 gestionnaires et de la recherche `0x8003C954`, fait par la table `0x80098FAC` avant de lire celui de la préparation : `CR/listings/my_handlers.txt`, `my_search_raw.txt`.
  - Il est identique, instruction par instruction, aux `sm3/listings/op_*.txt` : 0 écart sur 8 gestionnaires.
- Mon interpréteur MIPS-I écrit de zéro, qui exécute les vrais gestionnaires : `CR/work/mymips.py`, `replay_mips.py`.
- Mes modèles : un modèle littéral du binaire et un modèle de la forme DLL (`EntitySearchService.cs`) : `CR/work/models.py`.
- Un parcours de flot indépendant des 483 cartes : `CR/work/reach8.py`.
- Une analyse de `Result` par graphe de flot : `CR/work/cfg_stale.py`.
- Les sorties sont dans `CR/results/*.txt`, les empreintes dans `CR/SHA1SUMS`.
- Fichier rejeté : `CR/work/mycorpus_linear_decode_REJECTED_false_positives.txt`. Un décodage linéaire lit les tables de pointeurs de l'en-tête comme du code (faux `0xB4`). Il est remplacé par `reach8.py`.

## 2. Verdicts par fait décisionnel

| # | Fait | Verdict | Fondement |
|---|---|---|---|
| F1 | Les 8 gestionnaires et leurs tailles (2, 2, 3, 2, 2, 2, 3, 6). Le seul mot écrit est `Result` (`+0x2C`). | **CONFIRMÉ** (FAIT) | Listings identiques. Les tailles égalent `EventOpcodeSizeTable.cs:206,217,218,226-231`. |
| F2 | Sémantique R1 à R6 (détail en tête de la liste ci-dessous). | **CONFIRMÉ** (FAIT) | Lecture du désassemblage, puis F4. |
| F3 | Recherche `0x8003C954` : 12 types ; id brut testant le statut de la référence ; héros exclu des boucles 1..N ; ordre croissant des slots ; types 0 et 1 sans test de statut. | **CONFIRMÉ** (FAIT) | `my_search_raw.txt`, table de sauts `0x80023C30` lue. |
| F4 | Les 837 vecteurs égalent le binaire. | **CONFIRMÉ** (FAIT) | Mon rejeu : 837/837, 0 écart. Test de sensibilité : un patch `bgez` vers `bltz` dans B5 donne 71 écarts. Répartition 0/1 identique à `stats.json`. Aucun vecteur n'a `expect == result_before`. |
| F5 | Modèles python (binaire et forme DLL) égaux aux gestionnaires. | **CONFIRMÉ** (FAIT) | Fuzz de mes modèles contre le MIPS réel : 7 opcodes × 4 000 mondes (graine 777) et 7 × 3 000 (graine 99), plus 3 000 rectangles aléatoires pour `0x9E`. 0 écart. Mes modèles égalent aussi les 837 attendus. |
| F6 | `EntitySearchService.cs` égale le binaire. | **CONFIRMÉ** avec nuances | Le héros (réf. −1) est dans la boucle d'id brut de la DLL mais pas dans la 1..N du binaire : équivalent. |
| F7 | Correspondance octet vers membre : `ForceZ` est un `int`, `TargetAnimationId` un `uint` (`EntityScriptProxy.cs:158`), `XCollisionEntity` est de type proxy (`:364`), `IsLoadedNormalOrDeactivated` = statuts 1 à 3. | **CONFIRMÉ** (FAIT) | Lecture du code. Nuances : voir C3 et C12. |
| F8 | « Rouge aujourd'hui » : `UnknownOpcode` rend `entry.Size` (`:3065-3091`), `Result` est intact, `Kind = UnknownSkipped`. | **CONFIRMÉ** (FAIT) | Les 8 tailles sont > 0. Les lecteurs suivants sont `03`/`04`/`4B` (`:574`, `:577`, `:1141-1145`). Un rouge exige un `Result` attendu différent du `Result` de départ, vrai pour 837/837. |
| F9 | Corpus : 42 lignes, 40 instructions, 11 cartes. `B4`, `B7`, `9E` : 0 site. Après retrait : 468 lignes, 468 instructions, 91 cartes, 16 opcodes. Variante `B2` en attente : 32 retirées, donc 478/478/91/17. | **CONFIRMÉ** (FAIT) | Mon parcours de flot (états libres, entrée 0 exclue) retrouve exactement ces 40 instructions. Recomptage sur `skipped-sites.tsv`. Les clés égalent `corpus-lines-sm3.tsv`. Les cartes 423 à 426 et 440 sortent. |
| F10 | Toutes les cartes de l'export font 52×60. | **CONFIRMÉ** (FAIT) | `CR/results/map_sizes.txt`. |
| F11 | Libellés : B4 est `ForceZ > 0`, B5 `< 0`, B6 `== 0`, sur le premier résultat. | **CONFIRMÉ** (FAIT) | `blez`/`bgez`/`bnez` sur l'entrée 1 du tampon `0x8013D8D8`. |
| F12 | Aucun test existant n'exécute ces opcodes. | **CONFIRMÉ**, imprécis | `rg` donne 0 littéral hexadécimal. Aucun test ne nomme les 11 cartes. `ProbeOpcode` (`AlundraStoryChainOpcodeAudit.cs:87-119`) les exécute sur hôte nu, sans effet sur les règles de la chaîne (0 site chaîne). |
| F13 | Retrait du code seul : « rouges exactement les 75 tests, plus (c1) ». Retrait des 42 lignes seules : « exactement (c2) et (k1) ». | **RÉFUTÉ** | Voir C1. |
| F14 | 56 mutants tués chacun par un vecteur écrit à la main. | **NON ÉTABLI** mutant par mutant (leur code), plausible | Mes mutants tout-sauf-leurs : 11 sur 13 sont tués. Deux survivent : C2 et C7. |
| F15 | Faits statiques des arcs : enregistrements, programmes, jumps, flags, arithmétique `741+0x1A0 = 1157`. | **CONFIRMÉ** (FAIT) | Voir la liste en fin de section. |
| F16 | Ordre du tick du binaire : cartes, puis `UpdateEntities`, puis programmes, puis physique. | **CONFIRMÉ** (FAIT) | `0x8002E100` (`jal 0x8003C67C`) et `0x8002E108` (`jal 0x8003B388`). Dans `0x8003B388` : `0x80038634`, `0x800386D0`, `0x80038998`, puis `0x80038364` en `0x8003B3E0`. |
| F17 | Ordre du tick de la DLL (programmes avant physique, contact du héros écrit en physique). | **CONFIRMÉ par lecture** (INFÉRENCE pour le dynamique) | `WorldProxy.cs:2568, 2582, 2927, 2939-2942, 2976-2980` exacts. Le contact du héros est écrit dans `TickPhysics` via `TickMotion` puis `PlayerManager.Tick` (`EntityScriptProxy.cs:1602-1621`). |
| F18 | `+0x130` n'est écrit que par `MoveEntity` (`0x80037E80`, `0x80037F08`) et l'init `0x800426A0`. | **CONFIRMÉ** | La liste d'adresses exclues est incomplète : C9. |
| F19 | `PlatformEntity` n'est jamais écrit dans la DLL. Dans le binaire, `+0x28` d'un objet porte le héros. | **CONFIRMÉ** (FAIT) | Seule copie du clone (`EntityScriptProxy.cs:2609`). `plan-e20-heros.md:6540` : trois sites, tous rangent le héros. |
| F20 | `ICellMutator` : `CellStore` est son seul implémenteur, aucun test n'en fournit un faux. Prochain ADR : 0076. | **CONFIRMÉ** | `rg ICellMutator`. ADR-0075 est le dernier, aucune réservation dans les plans. |
| F21 | Dépendances : sm0, R5, R7, Q-E19-SM1. `E20.g1a` est indépendant du code de sm3. | **CONFIRMÉ**, avec (k2) manquant | Voir C1. |
| F22 | Q-SM3-1 à Q-SM3-3. | **CONFIRMÉ** sauf C11 | La recommandation « porter B2 maintenant » est cohérente avec Q-sm-4. |

Détail de F2, la sémantique des 8 opcodes :
- A9 : la collision de l'entité logique est dans le groupe.
- AA : au moins un membre du groupe a pour collision l'entité logique.
- B2 : deux groupes ont une intersection d'identité, et le groupe 1 vide donne 0.
- B4/B5/B6 : prennent le `ForceZ` du **premier** résultat (le plus petit slot) ; un groupe vide donne 0, y compris pour B6.
- B7 : mot de 32 bits `+0x88` contre l'octet étendu par zéro de `v2`.
- 9E : compte des `lhu(cell) & 2`, cellule à `base+0x604+((y+r)*52+x+c)*8`, résultat `count >= v5`.

Détail de F15, ce que j'ai vérifié dans les données :
- 424 enregistrement 3 : C=131, sprite 177, tuile (37,27), zone x26..50, y26..31 ; programme `30 flag 0x04DB`, `73 28`, `AA 81`, `04 FB FF`, `74 FA FF`, `57`.
- 440 : enregistrement 24 « Jeune Meia », C=153 ; `B[9]` : `2F 80 00 00` est « Carré tenu » (`PadState.cs:31`).
- 54 `B[14]` : deux boîtes `3B` et flag `0x0427`.
- 160 `C[6]` : enregistrement 6, sprite 55, zone x30..51.
- 258 et 298 : ids 3, 4, 5, 6, 8 existent comme propriété `Index`, qui est `EntityRefId` (`EntityRecordMapper.cs:131-133`). Le point P7 est donc réglé statiquement.
- 54 : impulsion 204 800, première valeur négative −24 576 à la 7e décroissance (`UW-4`, `plan-e19-opcodes.md:3552`).

## 3. Défauts et corrections à appliquer

**C1 — P2. Preuves de retrait, liste du code de test touché.**
- *Retrait du code seul* (cas, libellés, `ImplementedOpcodes`) : les rouges ne sont pas « les 75 tests plus (c1) ». Il faut y ajouter :
  - les 40 lignes de la `[Theory]` M2 ;
  - les arcs M3 : tous, sauf S3-A3b qui est une garde verte par coïncidence ;
  - S3-A6a est rouge sur le seul `Kind`.
  Les 75 de `tests.tsv` sont les seuls `U-*` (66 en M1, 9 en M4 conditionnels à Q-SM3-2).
- *Retrait des 42 lignes seules* (`ClosedOwners` gardant `sm3`) : rouges (c2), **(c4)** (propriétaire clos avec lignes) et (k1). Le plan sm0, mutation m5, dit qu'ajouter un propriétaire clos rougit (c4). La préparation oublie (c4).
- *(k2) de sm0* épingle les trois lignes de 440 qui partagent `@1140`. Ce sont des lignes sm3. Après retrait il ne reste aucun pc partagé (468 lignes = 468 instructions) : (k2) rougit si elle n'est pas modifiée ou retirée dans le même commit. La liste « code de test existant touché » doit la nommer. Sinon l'arrêt « un rouge autre que l'ensemble attendu » se déclenche à tort.

**C2 — P3. Mutant manquant pour A9 et AA : identité contre `EntityRefId`.**
- Aucun vecteur écrit à la main ne distingue `ReferenceEquals` d'une égalité d'id. Résultat : 0 tueur sur 25 vecteurs A9 et 21 vecteurs AA (`CR/results/extra_mutants_hand_vectors.txt`). B2 a ce mutant et le test U-B2-14.
- Ajouter un vecteur et un test : deux entités distinctes de même `EntityRefId`, la collision est celle qui n'est pas dans le groupe, attendu 0. Le cas est réaliste : plusieurs entités à `EntityRefId −1`.

**C3 — P3. Règle de montage M1.**
- Tout proxy du monde de test, héros compris, doit porter un `LogicContextEntity` distinct et non nul (modèle `NewEntity()`). Sinon les types 5, 6, 9 et 10 de `EntitySearchService.cs:171-244` se réduisent à `null == null` et correspondent à tort.
- Le texte M1 de la préparation ne le dit pas pour le héros. À ajouter aux pré-conditions de B2-20.

**C4 — P3. Noms des fichiers et types de test.**
- Les quatre fichiers proposés (`AlundraContactGroupAndForceZOpcodesTests`, `AlundraCellCountOpcodeTests`, `AlundraSm3RealOccurrenceTests`, `AlundraSm3SiteArcTests`) sont à la racine et portent le préfixe `Alundra`.
- Ils seront refusés par R7 : règle 2 de l'ADR-0075, rangement de R5. La préparation liste ce refus comme un arrêt, puis le déclenche elle-même.
- T0 doit les nommer au rangement de R5 (sans préfixe, dans le sous-dossier du domaine). Les citations de tests existants se traduisent par `r5-files.tsv` et `r5-types.tsv`.

**C5 — P3. Arc S3-A6a et affirmation « le Result périmé vaut 0 à chaque retour ».**
- Réfuté pour `B2 @639` en 258 et 298. Chemin témoin : `3B @672` vrai donne `Result = 1`, puis `03 @679` pris, `30 @686` (flag `0x81F4` allumé) saute en `@637`, puis `B2 @639` avec `Result = 1`.
- Sans flag, le chemin existe aussi mais en `@686` il continue. L'analyse précédente listait `@682`, `@695` et `@699` et oubliait `@686`.
- Conséquences :
  - S3-A6a : « n'importe quelle tuile » doit exclure la tuile (5,46), la boîte de `3B` `05 05 2E 2E 01 01`, ou garder le flag `0x81F4` éteint ;
  - Option A de Q-SM3-1 (« rien ne change en jeu ») : vrai hors de ce coin, où il y a un changement de comportement (d'ailleurs une correction).

**C6 — P3→P4. Méthode « Result périmé ».**
- `stale_result.py` décode en ligne droite jusqu'au site : elle ne prouve rien sur les boucles.
- À remplacer par une analyse de flot (`CR/work/cfg_stale.py`, `CR/results/stale_result_cfg.txt`).
- Valeurs premier passage : 0 partout sauf 54 B[14] ×2 (1), 440 B[9] (1) et 258/298 @639 (0 ou 1). La phrase « seuls 4 sites sont atteints après un écrivain » ne vaut que pour le premier passage.

**C7 — P4. Inversion propriétaire contre entité logique.**
- Aucun test M1 ne retarget (`U-ALL-02` l'écarte expressément). Un cas qui lirait `owner` au lieu de `entity` (les deux paramètres de `Dispatch`) survit à tout.
- Aucun site réel n'est sensible : seul 54 B[14] contient `0x42` ou `0x43`, après ses sites (`@1267`), et ses sites utilisent le type absolu `0x81`.
- Un test par famille (A9, AA, B4 à B6 avec le type `0x80`) lèverait le risque. À décider.

**C8 — P4. Slot libre.**
- INCONNU levé : l'init de `0x8003B21C` écrit −1 dans le gabarit `+0x48`, et `UpdateDestroyedEntities` (`0x80038634`) le recopie. Elle recalcule aussi N = plus haut slot non libre.
- Un trou ne correspond donc jamais à un id brut 0 à 127. La précaution « éviter l'id brut 0 » est sans objet pour les trous.
- Les vecteurs à trou portent `ref_id 0` : à passer à −1, sans changement de résultat.

**C9 — P4. Liste des stores `+0x130`.**
- Quatre stores non cités : `0x80044144`, `0x80044250`, `0x80044274`, `0x80044374`. Ils visent une structure globale à `0x8013FA38`, pas les entités. Conclusion inchangée.

**C10 — P4. Consommateurs de libellés.**
- `IntroTraceHarnessTests.cs:1486,1828,1836` imprime `entry.Name` dans le texte d'annexe. Sans assertion, la carte 389 n'a aucun des 8 opcodes.
- Le message de `UnknownOpcode` n'est donc pas le seul consommateur.

**C11 — P4. Numérotation des questions.**
- Q-SM3-3 renvoie à Q-sm-7/8 pour les libellés. Q-sm-8 traite l'atteignabilité des cartes 55 à 60. Seul Q-sm-7 concerne les libellés.

**C12 — P4. Correspondance `+0xB4`.**
- `AnimFlags` est « le même octet, une image en retard » (`EntityScriptProxy.HitPass.cs:29`). Le type 4 de recherche est le seul concerné. Aucun site sm3 ne l'utilise.

**C13 — P4. Montage M1.**
- Le `Context` d'`AlundraClassAndBranchOpcodesTests.cs:25-35` est une classe privée imbriquée : le nouveau fichier doit la copier, ou un helper interne doit l'exposer.
- Le précédent M2 (`:4156`) se saute silencieusement sans racine. La préparation choisit d'échouer, ce qui est bon.

## 4. Verdicts par tâche proposée

| Tâche | Verdict |
|---|---|
| Tests rouges d'abord (M1 et M4) | **CONFIRMÉ** après C2, C3, C4. Valeurs justes (837/837 binaire). |
| Commit unique : cas, libellés, `ImplementedOpcodes`, 42 lignes, `ClosedOwners`, tests | **CONFIRMÉ** après C1 (preuves, (k2)). Emplacements `:2054` et `:1469` exacts. |
| Commit séparé : 9E, membre d'interface sur `ICellMutator`, ADR | **CONFIRMÉ** techniquement (lecteur `CellStore.GetWalkability:136` sans garde de bornes, d'où `TryReadWalkability`). La décision reste à l'auteur (Q-SM3-2 ; 0 site dans 483 cartes). |
| M2 octets réels (40 instructions) | **CONFIRMÉ**, à ajouter aux rouges de C1. |
| M3 arcs et mesures de T0 (P1 à P7) | **CONFIRMÉ** pour le statique. Le dynamique reste **NON ÉTABLI** (déclaré). C5 pour A6a. P7 est réglé statiquement (`EntityRefId` = propriété `Index`). |
| Acceptation (build Release neutralisé puis Debug, `--blame-hang-timeout`, `cmp`, `check_names --rules`, recette, verifier frais) | **CONFIRMÉ**. |
| Arrêts, budget, retour arrière | **CONFIRMÉ**. |

## 5. Cas manquants (tests ou vecteurs à ajouter)

- Identité contre `EntityRefId` pour A9 et AA (C2).
- Proxies sans `LogicContextEntity` pour les types 5, 6, 9, 10 (C3).
- Propriétaire différent de l'entité logique (C7, P4).
- `9E` hors grille (index ≥ 3120) : politique écrite (0 + un journal), valeur INCONNUE côté binaire.
- Raccord avec E20.g1a : le recensement de ses recherches 4 et 11 doit nommer B2 (`plan-e20-heros.md:6649-6650`). Déjà dans les arrêts.

## 6. NON ÉTABLI et INCONNU

- Tous les arcs dynamiques sont NON ÉTABLIS : la préparation les déclare comme tels, je ne les ai pas exécutés (lecture seule, pas de build).
  - Le héros est-il bloqué par les blocs 177, 25, 89 ?
  - `XCollisionEntity` des PNJ de la 440 marchant vers le héros (P5).
  - `ForceZ == 0` au repos de la boule de la 160 (P4) : la DLL ne remet à 0 que sous gravité (`EntityScriptProxy.cs:979-980`, `:892`).
  - Le nombre 7 ou 8 de `B5` en 54.
- Les 56 mutants un par un : NON ÉTABLI ici.
- Les noms finaux des types de test après R5.
- Le dessin de (k2) de sm0, sm0 n'étant pas exécutée.
- Contenu d'un slot jamais utilisé : hors périmètre ; N exclut les slots libres hauts.

## 7. Fichiers produits

Tout est sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3-critic/` :
- `listings/my_handlers.txt`, `listings/my_search_raw.txt` : désassemblages écrits avant la lecture des leurs.
- `work/` :
  - `mymips.py`, `replay_mips.py`, `models.py`, `fuzz.py`, `fuzz9e.py`, `sanity_replay.py`, `dll_vs_vectors.py` ;
  - `reach8.py`, `retarget_scan.py`, `cfg_stale.py`, `extra_mutants.py`, `sm0_recount.py`, `mapsizes.py`, `records.py`, `show.py`.
- `results/` : sorties de ces scripts (`replay_837.txt`, `replay_sanity_and_sensitivity.txt`, `fuzz_*.txt`, `reach_8_opcodes_flow_scan.txt`, `stale_result_cfg.txt`, `extra_mutants_hand_vectors.txt`, `sm0_recount.txt`, `undefined_memory_reads.txt`, …).
- `SHA1SUMS`.