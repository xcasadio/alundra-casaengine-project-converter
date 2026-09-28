# Plan — E16, état de partie : drapeaux et sauvegarde

Étape E16 de [plan-conversion-totale.md](plan-conversion-totale.md). Elle termine la gestion des
deux banques de drapeaux, puis permet de sauvegarder et de recharger une partie comme l'original,
sur un service de sauvegarde générique ajouté au moteur.

**Statut** : proposé le 2026-09-27, **en attente d'approbation**. Position dans la file : **après
E15** (D-E16-7) ; E15 est close et mergée dans `main` le 2026-09-28 (`2b0283b`, moteur `793d1ee8`).

**Révision 2** : relecture de plan (REVISE, trois P2 corrigés), revue de sécurité (constats
tranchés au §6), puis relecture fraîche de clôture de l'enveloppe et d'E16.0 : **READY**.

**Révision 3 (2026-09-28)**, après le merge d'E15 ; la relecture de plan est à refaire :

- plan rebasé sur `main` ;
- **variables Yarn** : E15 a laissé à E16 le stockage des variables Yarn adossé aux drapeaux
  (D-E15-13). Réponses de l'auteur D-E16-14 à D-E16-18, nouvelle tranche **E16.f**, ADR-0010 ;
- **ADR** : l'ADR prévue en E16.0 sous le numéro 0006 est l'ADR-0010, écrite à cette révision (E15 a
  pris les numéros 0006 à 0009) ;
- **citations** : celles du code sont remises à jour, E15 et la suppression de F1 ayant déplacé ou
  retiré des lignes. Le monde n'écrit plus aucun drapeau ;
- **E16.c et E16.d** : l'indice de catégorie et les variables `\V`, ajoutés par E15, sont exclus de la
  sauvegarde et remis à zéro au chargement ; `DebugHudRecipeApplied`, supprimé avec F1, sort de la
  remise à zéro.

| Unité | Revue avant approbation | Après exécution |
|---|---|---|
| Enveloppe (ce plan) + E16.0 (mesure, première tranche exécutable) | plan-verifier ; revue de sécurité faite (§6) | commit de documentation |
| E16.a — opcodes | plan-verifier, approuvée après E16.0 | verifier frais |
| E16.b — plan moteur `CasaEngineMonogame/ai-agent/tasks/save-game-service-tasks.md` | revue de sécurité faite, plan-verifier séparé | verifier frais (**CONFIRMED** exigé) |
| E16.c — objet de sauvegarde | **revue de sécurité** puis plan-verifier, approuvée après E16.0 | verifier frais sur l'acceptation « entrée non fiable » |
| E16.d — chargement | **revue de sécurité** puis plan-verifier, approuvée après E16.c | verifier frais sur l'acceptation « entrée non fiable » |
| E16.e — écran de sauvegarde (plan propre) | **revue de sécurité** puis plan-verifier | verifier frais |
| E16.f — variables Yarn adossées aux drapeaux | plan-verifier, approuvée après E16.0 | verifier frais |

Cette approbation couvre l'enveloppe et E16.0 ; chaque tranche suivante est approuvée à part, avec
les revues de sa ligne.

---

## 0. Cadre

### 0.1 Décisions de l'auteur (2026-09-27)

- **D-E16-1 — Périmètre : tout, en tranches.** Les opcodes de drapeaux manquants, le service de
  sauvegarde, sauvegarder et recharger une partie, puis l'écran de sauvegarde du jeu d'origine pour
  que le joueur sauvegarde lui-même.
- **D-E16-2 — Deux formats, au choix** : JSON lisible et binaire compact.
- **D-E16-3 — Service générique dans le moteur**, inspiré de Godot, Unity et Unreal. Plan moteur :
  `CasaEngineMonogame/ai-agent/tasks/save-game-service-tasks.md`.
- **D-E16-4 — Contrat « objet de sauvegarde »** (modèle Unreal `USaveGame` + `SaveGameToSlot`) : la
  DLL remplit un objet que le service écrit dans un emplacement nommé.
- **D-E16-5 — Fichiers dans le dossier de l'utilisateur.**
- **D-E16-6 — Les drapeaux restent la propriété de la DLL** (`AlundraGameState`). L'interpréteur,
  qui est le moteur de script d'Alundra (D1), et les directeurs de la DLL les lisent et les écrivent.
  Le pont vers les systèmes de script du moteur (Yarn, cinématiques) se construit en E15, quand un
  programme converti en a besoin.
  *Mise à jour du 2026-09-28* : E15 n'a pas construit le pont Yarn. Aucun texte exporté n'utilise de
  variable, et le runner a gardé son stockage par défaut (D-E15-13 de `plan-e15-yarn.md`). Le pont
  Yarn revient à E16 (D-E16-14, tranche E16.f), celui des cinématiques à E17.
- **D-E16-7 — E16 passe après E15.**

Réponses de l'auteur aux points ouverts (2026-09-27, après la relecture READY) :

- **D-E16-8 — §9.9 du moteur précisé** (O1 du plan moteur, O-E16-4) : les assets restent à
  l'éditeur, la sauvegarde de partie du joueur est un service runtime. Le prérequis du plan moteur est
  levé.
- **D-E16-9 — La modification en cours de l'auteur** (`CasaEngine.Launcher/Program.cs`) suit la
  branche du chantier moteur dans l'arbre de travail, jamais indexée (O4 du plan moteur).
- **D-E16-10 — Charger au démarrage : plus tard** (O-E16-1). Ni écran « Continuer » ni écran titre
  dans E16 ; d'ici là, on ne charge que par la touche de recette.
- **D-E16-11 — Touches de recette** (O-E16-2) : F5 sauvegarde en binaire, F6 en JSON, F9 charge
  l'emplacement le plus récent, inactives par défaut et activées par un interrupteur de débogage. Le
  mécanisme de cet interrupteur reste à choisir (O-E16-6) : une variable d'environnement
  (`ALUNDRA_SAVE_DEBUG=1`, proposée d'abord) risque de ne jamais atteindre le processus du jeu.
- **D-E16-12 — E16.e porte le gestionnaire du livre de sauvegarde** (O-E16-5) ; E14 le laisse de côté.
- **D-E16-13 — La touche F1 de la recette de la jauge est supprimée**, et la recette
  `ALUNDRA_HUD_DEBUG` avec elle : elles ne servaient qu'à valider le HUD. Chantier séparé, hors de ce
  plan (branche `chantier/remove-f1-hud-key`, mergée dans `main` le 2026-09-27, `cb37168`).

Réponses de l'auteur sur les variables Yarn (2026-09-28, après le merge d'E15) :

- **D-E16-14 — Le stockage des variables Yarn adossé aux drapeaux se construit dans E16**, dans sa
  propre tranche (E16.f), bien qu'aucun texte exporté n'utilise encore de variable.
- **D-E16-15 — Il n'expose que les deux banques de drapeaux.** Les faucons et les variables `\V`
  restent lus par les fonctions d'E15.
- **D-E16-16 — Noms** : `$flag_n` est le drapeau `n` de `GameFlags`, `$tmp_flag_n` le drapeau `n` de
  `TemporaryFlags`. `n` s'écrit en décimal, de 0 à 32767, sans le bit de banque `0x8000` : c'est le
  même `n` que `<<flag n>>` pour la banque temporaire.
- **D-E16-17 — Lecture et écriture** : un drapeau est un booléen Yarn ; `true` pose le bit comme
  l'opcode `0x05`, `false` l'efface comme l'opcode `0x06`.
- **D-E16-18 — Tout autre nom est refusé et journalisé**, variables internes de Yarn comprises, tout
  comme un nom de drapeau hors bornes ou une valeur non booléenne rangée sous un nom de drapeau.
  Seul un drapeau est une variable ; rien ne se perd en silence au chargement.

Decisions: see ADR-0010 (`docs/decisions/0010-game-flags-stay-in-the-dll-and-yarn-reads-them-as-variables.md`),
qui consigne aussi D-E16-6.

### 0.2 Faits établis (2026-09-27, citations du code remises à jour le 2026-09-28)

Chaque fait a été relu dans le code ou le binaire cité ; les recherches larges ont été contre-vérifiées
par un agent neuf.

**Drapeaux, côté DLL**

- Stockage et API : banques `GameFlags` et `TemporaryFlags` de 1024 mots (`AlundraGameState.cs:192`,
  `:220`), port de `GetFlag`/`AddFlag`/`SetFlag`/`XorFlag` (`:244-259`, `GameEngine.cs:2828-2926`) ;
  banque choisie par le bit `0x8000`, mot `(id >> 5) & 0x3ff`.
- Cycle de vie : `InstallForMapEntry` vide `TemporaryFlags` et garde `GameFlags`
  (`AlundraGameState.cs:275-294`, D-T-13), pinné par `AlundraGameStateSessionTests` et
  `AlundraWorldProxySessionStateTests`.
- Opcodes portés : `0x05`, `0x06`, `0x30`, `0x31`, `0x33`, `0x36`
  (`AlundraEventProgramRunner.cs:445-459`, `:554-577`) ; `0x05` pose le bit `1 << (id & 0x1f)` par
  `AddFlag` (`:449`), `0x06` l'efface par `SetFlag(id, ~masque)` (`:457`), la lecture teste
  `GetFlag(id) & masque` (`:1227`, `:1240`, `:1255`).
- **Opcodes non portés**, sautés par taille : `0x32` bascule par `XorFlag`
  (`EntityEventHandlers.cs:1102`), `0x34` vrai si aucun des quatre drapeaux n'est posé (`:1132`),
  `0x35` avance quand le drapeau est à 0 (`:1152`), `0x7B`/`0x7C`/`0x80`/`0x81` sauts conditionnels à
  paramètre mémorisé (`:2264-2373`). `XorFlag` n'a aucun appelant dans la DLL.
- **Famille du paramètre mémorisé** : `0x7B`/`0x7C` partagent le registre `EventProgramState._34`
  avec d'autres opcodes. `0x78`, `0x79`, `0x7A`, `0x7B` et `0x7C` l'écrivent (`:2231`, `:2242`,
  `:2256`, `:2286`, `:2316`) ; `0x7D`, `0x7E`, `0x7F`, `0x80` et `0x81` sautent en le relisant
  (`:2324-2382`). La DLL n'en porte aucun, et `EventProgramState._34` existe sans écrivain
  (`Alundra/Scripts/EventProgramState.cs:33`). Porter une partie de la famille enverrait l'exécution
  vers un retour encore sauté par taille, ou vers un `_34` jamais posé.
- Autres lecteurs et écrivains :
  - la commande `<<flag n>>` du texte Yarn (E15), qui pose `n | 0x8000` dans la banque temporaire
    (`AlundraYarnBindings.cs:142-153`) ;
  - la jauge : drapeaux 1662, 1813 et 1814 (`AlundraHudDirector.cs:250`, `:357-378`).

  Le monde n'en écrit plus aucun. Ses trois écritures (`AlundraWorldProxy.cs:1449`, `:1466`, `:1712`
  à `cbda4f8`) appartenaient à la recette `ALUNDRA_HUD_DEBUG` et à la touche F1, supprimées par
  D-E16-13.
- Tailles : la DLL dimensionne les deux banques à 1024 mots ; l'original déclare `GameFlags` sur
  64 mots (`SaveData.cs:17`) et `ClearTemporaryFlags` n'en vide que 64 (`GameEngine.cs:429-438`).

**Données sauvegardées par l'original** (`SaveData.cs:6-23`) et leur équivalent dans la DLL

| Champ d'origine | Dans la DLL |
|---|---|
| `GameFlags[64]` | `AlundraGameState.GameFlags` (1024 mots) |
| `MapIdToInternalMapIndexTable[500]` | `AlundraGameState.MapIdToInternalMapIndexTable` |
| `PlayerStats` (`Hp`, `HpMax`, `Mp`, `MpMax`, `MoneyAmount`, `WeaponId`, `ItemId`, `FalconTemp`, `Falcon`) | `AlundraPlayerStats` (`AlundraPlayerStats.cs:31-94`) |
| `NumberOfItems[256]` | `AlundraGameState.NumberOfItems` |
| `InitialMapId`, `CameraTileX/Y/Z` | constantes de nouvelle partie (`AlundraGameState.cs:42-51`) |
| `SlotData`, `LastMapId`, `CurrentFlagName`, `GameStateDescription`, `GameTime`, `SaveSlotIndex`, `Field_757`, `Offset` | absents |

`TemporaryFlags` n'appartient pas à `SaveData` : l'original ne le sauvegarde pas.

**État ajouté par E15** (§5.7 de `plan-e15-yarn.md`, lu dans `ALUN_CD.EXE`)

- L'indice de catégorie `TextCategoryIndex` (`AlundraGameState.cs:206`, `g_textCategoryIndex` à
  `0x80149CD8`) et les quatre variables `\V` `GameVariables` (`:217`, `INT_ARRAY_80191908`) sont dans
  la BSS de l'original, **hors de `g_saveData`** (`0x801EB2E8`–`0x801EBA40`). Le point d'entrée
  `0x8008b538` met cette BSS à zéro, et rien ne la remet à zéro ensuite. Le port ne les remet à zéro
  que dans `ResetForTests` (`AlundraGameState.cs:299-341`).
- `Falcon` et `FalconTemp` sont dans `g_playerStats` (`g_saveData + 0x544`, champs `+0x10` et `+0x0E`),
  donc déjà dans les stats sauvegardées.

**Yarn, depuis E15**

- La DLL crée le runner sans stockage de variables (`AlundraDialogueDirector.cs:171-172`). Le moteur
  donne alors à chaque dialogue un `Yarn.MemoryVariableStore` neuf ; un jeu peut injecter le sien
  par `YarnDialogueRunner.VariableStorage` (`CasaEngineMonogame/CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs:25-32`,
  `:142`, ADR-0042 du moteur).
- Les 485 `.yarn` exportés ne déclarent, ne lisent ni n'écrivent aucune variable. Leurs seules
  commandes sont `<<flag>>` (932) et `<<falcon_update>>` (7).
- Contrat de stockage de Yarn Spinner 3.2.1 (documentation XML du paquet) :
  - `Yarn.IVariableAccess` : `TryGetValue<T>`, `GetVariableKind`, `Program`, `SmartVariableEvaluator` ;
  - `Yarn.IVariableStorage` : `SetValue` pour un texte, un nombre ou un booléen, et `Clear`.

  Selon cette documentation, `TryGetValue` lit la valeur dans le stockage, dans les valeurs
  initiales du `Program` ou dans une variable calculée. Ce que la machine virtuelle de Yarn fait
  quand `TryGetValue` échoue n'est pas établi (E16.f, T1).

**Chargement et texte de l'emplacement dans l'original**

- Branche « charger » d'`InitializeGameState` (`GameInitializer.cs:350-356`) : copie de la
  sauvegarde (`UpdateSaveData`, `GameEngine.cs:2688-2692`), puis départ sur `InitialMapId` à la
  tuile `CameraTileX/Y/Z`, temps de jeu repris (`GameInitializer.cs:417-424`).
- Texte d'un emplacement (`UpdateMenuStatusText`, `GameEngine.cs:2695-2747`) : le chapitre, tiré
  des 41 drapeaux de fin de chapitre (`ChapterFlags.cs`), et `HP xx TIME hh:mm:ss`, calculé depuis
  `HpMax` et le temps de jeu.
- **Temps de jeu, contradiction à trancher** : le compteur prend +1 à chaque appel de `EndGame`
  (`GameEngine.cs:1471-1474`, fin d'image) et plafonne à `0x14996C4` = 21 599 940 = 99:59:59 × 60 ;
  l'analyseur l'affiche pourtant comme un nombre de secondes (`GameEngine.cs:2724-2733`).
- **Chaîne de la sauvegarde en jeu** (relevée à la relecture du 2026-09-27) :
  1. le livre de sauvegarde est une entité d'**IA native** : `AI_ProcessWarpTransitionState`
     (`@ 0x8007B998`, `FunctionTypeC.cs:6824-6931`, entité nommée « SaveBook » dans l'analyseur) ;
     message (chaîne ETC `0x40`), puis question oui/non (chaînes `0x41`/`0x42`, `:6872-6875`) ;
  2. sur « oui », `UpdateSavedData` (`@ 0x8003153C`, `GameEngine.cs:2648-2662`, appelé en `:6915`)
     pose `InitialMapId = g_currentMap`, `CameraTileX/Y/Z` = tuile du joueur, le texte de
     l'emplacement (`UpdateMenuStatusText`) et `GameTime = g_gameplayTime` ;
  3. `InitializeSaveDataCopy(g_saveData, 0x758, 1)` (`GameEngine.cs:2664-2685`) pose
     `g_globalTransitionState = 10000` et `g_postProcessingState = 1`, ce qui démarre la machine
     d'états `MemoryCardManager.UpdateMemoryCardProcess` (`@ 0x8005EC98`, appelée par
     `GraphicManager.cs:62`, départ en `MemoryCardManager.cs:107-126`) ;
  4. l'entité attend la fin de cette machine (`g_globalTransitionState == 0`, `:6920-6928`).
- L'original a 4 emplacements par carte mémoire (`LoaderSaveSlots.cs:83`, enregistrements de `0x76C`
  octets en `:80`). Le choix d'une sauvegarde au démarrage appartient à `LOADER.EXE`, un exécutable
  séparé : le portage n'a pas d'écran titre.

**Accroches dans la DLL**

- Nouvelle partie : `AlundraWorldProxy.AdoptPlayerPawn` (`:1557-1561`), quand aucune arrivée de warp
  n'est en attente.
- Arrivée sur une carte à une tuile donnée : `AlundraWarpDirector.ConsumeArrivalRecord`
  (`AlundraWarpDirector.cs:177-186`), lue en `AlundraWorldProxy.cs:1550` et appliquée au héros en
  `:1563-1580` ; départ par opcode `AlundraWarpDirector.BeginDepartureFromChangeMapOpcode`
  (`AlundraWarpDirector.cs:355-396`).
- Recette gardée par une variable d'environnement : `ALUNDRA_HUD_DEBUG` (`AlundraWorldProxy.cs:156-189`
  à `cbda4f8`), lue une fois, journalisée, avec un point d'injection pour les tests. **Elle
  n'atteignait pas le processus du lanceur** (D-E13-12 de `plan-e13-hud.md`) ; elle a été supprimée
  avec la touche F1 par D-E16-13 (`cb37168`). Seul son modèle de code (lecture unique, journal, point
  d'injection), lisible à `cbda4f8`, sert encore de référence pour l'interrupteur d'E16.d.

**Moteur** : aucun service de sauvegarde, runtime en lecture seule par contrat (§9.9 de son
`AGENTS.md`) ; détail et proposition dans le plan moteur.

---

## 1. Tranches

### E16.0 — Mesure ⏳ (lecture seule, analyseur, corpus, `ALUN_CD.EXE`)

- **But** : les chiffres qui fixent le reste du plan.
- **Contenu** :
  0. Correction de `intro-roadmap.md:321`, qui dit encore `ClearTemporaryFlags` « non porté ».
     (D-E16-6 est consignée dans l'ADR-0010, écrite à la révision 3.)
  1. Ids de drapeaux réellement utilisés : arguments des opcodes de drapeaux dans tous les programmes
     exportés, commandes `<<flag n>>` du Yarn exporté, champ `ContentsGameFlag` des records. Plus
     grand id de chaque banque ; **existe-t-il un id persistant ≥ 2048 ?**
  2. Nombre d'occurrences dans le corpus de `0x32`, `0x34`, `0x35` et des **dix** opcodes de la
     famille du paramètre mémorisé (`0x78` à `0x81`), et comment ils s'y enchaînent : quel opcode
     écrit `_34`, lequel le relit ensuite, dans quels programmes.
  3. Dans `ALUN_CD.EXE` (France, qui tranche) : disposition et taille de `g_saveData`, taille de
     `g_temporaryFlags`, site et unité du compteur de temps de jeu, conversion faite par
     `UpdateMenuStatusText`.
  4. **Confirmer dans `ALUN_CD.EXE`** la chaîne de sauvegarde du §0.2 (`@ 0x8007B998` →
     `@ 0x8003153C` → `@ 0x8005EC44`), et lister les types de sprite et les cartes qui portent le
     gestionnaire du livre de sauvegarde.
  5. Suites de référence avant chantier : `Alundra.Tests`, tests du convertisseur, `CasaEngine.Tests`.
  6. Domaines encore inconnus des champs restitués (E16.c) : plafond du compteur de chaque objet
     (source lue par la DLL), bornes de `Falcon` et `FalconTemp`, **source des dimensions de chaque
     carte lisible avant de charger son monde** (le `world-index.json` n'en porte pas,
     `AlundraWorldIndexTable.cs:34`), valeur maximale de `CameraTileZ`.
- **Livrable** : une section « §2 Mesures » dans ce plan, chiffres et sources.
- **Acceptation** : chaque question 1 à 6 a une réponse sourcée, ou une ligne « non trouvé » avec
  les recherches faites ; toute réponse qui change une tranche suivante est remontée à l'auteur
  avant elle.
- **Budget** : par question, une passe dans l'analyseur et le corpus, puis une vérification ciblée
  dans `ALUN_CD.EXE` ; au-delà, la réponse s'écrit « non trouvé » avec les recherches faites.
- **Arrêt** : une mesure qui contredit une décision D-E16 ou qui change une tranche suivante → la
  tranche s'arrête, la question va au §3, le plan est corrigé et relu avant E16.a.
- **Retour** : les seules écritures sont ce plan et la ligne d'`intro-roadmap.md`, dans un seul
  commit de documentation, annulable par `git revert`.
- **Commit** : `docs(e16): record the flag and save-data measurements`.

### E16.a — Opcodes de drapeaux ⏳ (DLL)

- **But** : porter les opcodes de drapeaux qui apparaissent dans le corpus d'après E16.0.
- **Contenu** :
  - `0x32`, `0x34` et `0x35`, un à la fois, recoupés avec le binaire ; `0x32` donne enfin un
    appelant à `XorFlag` ;
  - la **famille du paramètre mémorisé, `0x78` à `0x81`, comme une seule unité** : tous ses opcodes
    présents dans le corpus sont portés ensemble, avec l'écriture de `EventProgramState._34`. Si
    E16.0 montre que `0x7B`, `0x7C`, `0x80` et `0x81` n'apparaissent pas, la famille entière est
    exclue d'E16, avec cette raison écrite ici.
- **Acceptation** : un test par opcode contre la décompilation ; un **test de séquence** sur un
  programme réel du corpus qui écrit `_34` puis y revient (opcode d'écriture jusqu'à l'opcode de
  retour) ; `Alundra.Tests` sans échec ; oracle de l'intro inchangé (`0x11` à la frame 1704) ; plus
  aucun saut par taille pour les opcodes portés.
- **Arrêt** : un opcode dont le binaire contredit la décompilation → consigné, décision de l'auteur
  avant de le porter ; l'oracle de l'intro qui bouge → la tranche s'arrête.
- **Dépendances** : E16.0.

### E16.b — Service de sauvegarde du moteur ⏳ (moteur)

- **But** : exécuter le plan moteur `save-game-service-tasks.md` (T0.1 à T4.2), puis déplacer le
  pointeur du sous-module.
- **Acceptation** : plan moteur clos, verifier **CONFIRMED** ; ce dépôt compile avec le nouveau
  pointeur.
- **Arrêt** : O1 du plan moteur refusé → le plan moteur s'arrête et se replanifie, E16.c à E16.e
  attendent.
- **Dépendances** : aucune dans ce plan ; la question O1 du plan moteur (§9.9) doit être tranchée
  avant.

### E16.c — Objet de sauvegarde d'Alundra ⏳ (DLL)

- **But** : `AlundraSaveGame`, l'objet que la DLL confie au service (D-E16-4).
- **Contenu** :
  - les champs de `SaveData` : `GameTime`, `InitialMapId`, `CameraTileX/Y/Z`, `GameFlags`,
    `MapIdToInternalMapIndexTable`, les neuf stats, `NumberOfItems`. `LastMapId`, `Field_757` et
    `Offset` seulement si E16.0 leur trouve un lecteur (la DLL n'en a aucun aujourd'hui).
    `SaveSlotIndex` n'est pas repris du fichier : l'identité d'un emplacement est son nom dans le
    service, un fichier copié ne peut pas se réclamer d'un autre emplacement ;
  - `GameFlags` écrit sur **64 mots** comme l'original si E16.0 ne trouve aucun id persistant ≥ 2048
    (sinon O-E16-3) ; la banque en mémoire garde ses 1024 mots, et la restitution **met les 1024 mots
    à zéro avant de copier les 64** (sinon des drapeaux de la session en cours survivraient) ;
  - `TemporaryFlags` n'est pas sauvegardé, comme dans l'original ;
  - `TextCategoryIndex` et `GameVariables` ne sont pas sauvegardés non plus : ils sont hors de
    `g_saveData` dans l'original (§0.2, état ajouté par E15). `Falcon` et `FalconTemp` le sont, avec
    les neuf stats ;
  - l'objet ne porte aucun champ propre à Yarn : les variables Yarn sont les drapeaux (D-E16-15,
    E16.f) ;
  - le chapitre et le résumé `HP xx TIME hh:mm:ss` vont dans les **métadonnées** de l'emplacement,
    que la liste lit sans décoder la sauvegarde ;
  - le port du compteur de temps de jeu, dans l'unité mesurée en E16.0 ;
  - capture depuis `AlundraGameState` ; la capture suit les sources d'`UpdateSavedData`
    (`GameEngine.cs:2648-2662`) : carte courante, tuile du joueur, texte de l'emplacement, temps de
    jeu ;
  - **l'objet possède ses propres tableaux** : au chargement, le service remplit ceux de
    `AlundraSaveGame`, jamais ceux d'`AlundraGameState.Instance` (dont les tableaux sont `readonly`,
    `AlundraGameState.cs:170-231`) ; l'état vivant n'est touché qu'à l'application (E16.d) ;
  - **une sauvegarde est une donnée non fiable, dans les deux formats** (le CRC-32 du binaire se
    recalcule, le JSON s'édite à la main) : `Validate()` contrôle chaque champ contre le domaine
    ci-dessous ; une seule valeur hors domaine refuse tout le chargement, avec un message qui la
    nomme.

  | Champ | Domaine | Source de la borne |
  |---|---|---|
  | `HpMax` | 0..50 | `SetPlayerHpMax` (`AlundraPlayerManager.cs:660-671`) |
  | `Hp` | 0..`HpMax` | règle croisée |
  | `MpMax` | 0..4 | `SetPlayerMpMax` (`:704-714`) ; la jauge n'a que 4 cases de magie (`AlundraHudDirector.cs:164`) |
  | `Mp` | 0..`MpMax` | règle croisée |
  | `Money` | 0..9999 | `SetMoney` (`:746-757`) ; un montant négatif fait lever la jauge (`AlundraHudComposer.cs:320-331`) |
  | `WeaponId` | -1 ou 1..6 | `SetPlayerWeaponId` (`:822-834`) |
  | `ItemId` | 0..98 | `ItemsCount = 99` (`:784`) |
  | `Falcon`, `FalconTemp` | ≥ 0, borne haute d'E16.0 | E16.0 question 6 |
  | `NumberOfItems` | indices impairs dans [0, plafond de l'objet] ; indices pairs et indices ≥ 198 à 0 | `AlundraGameState.cs:173-180` ; plafond d'E16.0 question 6 |
  | `MapIdToInternalMapIndexTable` | chaque valeur est une clé de `world-index.json` | `AlundraWorldIndexTable.Resolve` (`:88`) ; une valeur inconnue ferait avorter à jamais le portail qui la lit (`AlundraWarpDirector.cs:304`) |
  | `InitialMapId` | clé de `world-index.json`, monde présent au catalogue | idem |
  | `CameraTileX/Y/Z` | dans les dimensions de la carte, et sans débordement de `(tuile × largeur + largeur / 2) << 16` ni de `Z << 20` | `AlundraWorldProxy.cs:1563-1565` ; dimensions et Z maximal d'E16.0 question 6 |
  | `GameTime` | 0..`0x14996C4` | plafond du compteur (`GameEngine.cs:1471-1474`) |

- **Acceptation** : tests —
  - aller-retour identique en JSON et en binaire ; capture puis application donnent un état identique
    champ par champ ; `TemporaryFlags` inchangé ; ni `TemporaryFlags`, ni `TextCategoryIndex`, ni
    `GameVariables` dans aucun des deux formats ; texte du résumé comparé au calcul
    d'`UpdateMenuStatusText` ; capture sur un héros placé à une tuile connue d'une carte connue →
    `InitialMapId` et `CameraTileX/Y/Z` égaux à ces valeurs ;
  - **pour chaque ligne du tableau, dans les deux formats**, une sauvegarde portant la valeur
    minimale, maximale et maximale + 1 (ou minimale − 1) ; hors domaine → refus nommant le champ ;
  - la valeur invalide placée **dans le dernier champ sérialisé**, et une faute détectée par le moteur
    (tableau de mauvaise longueur) : l'état d'`AlundraGameState` (tous les tableaux, les neuf stats,
    `PlayerControlFlags`) est identique octet pour octet à un instantané pris avant ;
  - aux bornes du domaine, une restitution suivie d'un tick du directeur de la jauge, du compositeur
    de la jauge et des compositeurs de l'inventaire → aucune exception ;
  - si E16.f est livrée : un `$flag_n` posé par Yarn fait l'aller-retour, un `$tmp_flag_n` non.
    Sinon, E16.f ajoute ce test.
- **Arrêt** : un champ dont E16.0 n'a pas établi la source, l'unité ou le domaine n'est pas écrit dans
  le format ; question au §3. **Si E16.0 ne trouve aucune source des dimensions de carte lisible
  avant de charger le monde**, la promesse « état inchangé » ne couvre pas la tuile : arrêt, question
  à l'auteur.
- **Dépendances** : E16.0, E16.b.

### E16.d — Chargement et recette ⏳ (DLL)

- **But** : reprendre une partie sauvegardée.
- **Contenu** : port de la branche `SlotData == 1` d'`InitializeGameState`, dans cet ordre, dont
  seule la dernière étape modifie l'état vivant :
  1. **préconditions** d'un chargement en cours de partie (l'original ne charge qu'au démarrage d'un
     processus neuf, par `LOADER.EXE`) : aucun dialogue, aucun inventaire ouvert, aucune transition
     en cours, `PlayerControlFlags == 0` ; sinon refus avec un message ;
  2. chargement par le service, puis `Validate()` d'E16.c ;
  3. **contrôle du départ** : monde de `InitialMapId` résolu et présent au catalogue, aucune transition
     en cours, warp non désactivé (`AlundraWarpDirector.cs:365-368`) et garde d'abandon non
     déclenchable (`:540-548`) ; sinon refus ;
  4. **application en une étape qui ne lève pas** (copies de tableaux de longueurs déjà contrôlées),
     de préférence à l'entrée de la carte d'arrivée. Elle enchaîne quatre choses :
     - la remise des singletons de session à un état équivalent à une nouvelle partie, contrepartie
       de production de `ResetForTests` (`AlundraGameState.cs:299-341`). Elle couvre
       `PlayerControlFlags`, le verrou d'interaction et ses huit nombres, `NewGameInventoryInitialized`,
       les états des directeurs de dialogue et d'inventaire et les valeurs affichées de la jauge. Elle
       couvre aussi **`TextCategoryIndex` à 0 et `GameVariables` à zéro** : l'original ne charge que
       dans un processus neuf, lancé par `LOADER.EXE` (§0.2), dont le point d'entrée met la BSS à
       zéro (état ajouté par E15, §0.2). La liste exacte est arrêtée et testée ici ;
     - la copie de l'objet ;
     - le départ sur `InitialMapId` à la tuile `CameraTileX/Y/Z`, par le chemin d'arrivée des warps ;
     - la reprise du temps de jeu.

  **Touches de recette** (D-E16-11) gardées par un interrupteur de débogage dont le mécanisme est
  tranché avant cette tranche (O-E16-6), lu une fois et journalisé quand il est actif, avec un point
  d'injection pour les tests. **Acceptation propre à l'interrupteur** : activé par le moyen retenu, il
  est vu par le jeu lancé depuis le lanceur, et le journal du jeu en porte la trace. La capture est refusée hors d'un état que
  l'original sauvegarde : aucune transition, aucun dialogue, aucun menu, `PlayerControlFlags == 0`,
  héros au sol. Chaque résultat du service autre que « chargé » et chaque refus laissent la partie en
  cours intacte, avec un message ; aucun ne fait planter le jeu.
- **Tests** (dans les deux formats) : chaque résultat du service autre que « chargé » ; chaque
  précondition non tenue (dialogue, inventaire, transition, verrou de script) → refus, état
  identique à l'instantané ; warp désactivé, transition en cours, monde introuvable → refus, état
  identique ; un fichier au nom invalide posé dans le dossier de sauvegarde → aucune exception ;
  interrupteur inactif → F5, F6 et F9 sans effet ; chargement réussi depuis une session où
  `TextCategoryIndex` et `GameVariables` ne sont pas nuls → les deux valent 0 après.
- **Acceptation en jeu** (lancée hors de l'app Claude, O3 du plan moteur) : nouvelle partie sur la
  389, intro jusqu'au bout, passage sur la 390, sauvegarde ; quitter ; relancer, charger → sur la 390
  à la même tuile, stats et objets identiques ; retour sur la 389 **sans** que l'intro rejoue. La même
  recette réussit avec un emplacement JSON et avec un emplacement binaire. Une sauvegarde JSON éditée à
  la main (`Money: -1`, `MpMax: 9`, carte 9999) → message de refus, le jeu continue.
- **Arrêt** : l'intro qui rejoue au retour sur la 389, ou un écart de stats ou d'objets après
  chargement → la tranche s'arrête, cause établie avant toute correction.
- **Dépendances** : E16.c.

### E16.e — Écran de sauvegarde en jeu ⏳ (DLL, MGUI en XAML)

- **But** : le joueur sauvegarde lui-même, comme dans l'original.
- **Contenu** : le gestionnaire du livre de sauvegarde (`AI_ProcessWarpTransitionState`, question
  oui/non puis `UpdateSavedData`), porté ici (D-E16-12), et le flux de `MemoryCardManager`
  (choix de l'emplacement, confirmation d'écrasement, messages de réussite et d'échec) ; écran
  déclaré en XAML (règle de l'auteur). Le gestionnaire d'origine fait environ 2 800 lignes : **cette
  tranche aura son propre plan**, écrit après E16.0, avec ce prérequis de sécurité : les métadonnées
  d'un emplacement sont du texte non fiable, affiché borné en longueur et avec le formatage en ligne
  de MGUI désactivé (`MGTextBlock.cs:837`), ou recalculé depuis une sauvegarde validée.
- **Arrêt** : un manque de MGUI ou du moteur → consigné dans le rapport dédié, la tranche s'arrête
  (règle de l'auteur : signaler, jamais contourner).
- **Dépendances** : E16.0, E16.d.

### E16.f — Variables Yarn adossées aux drapeaux ⏳ (DLL, docs)

- **But** : D-E16-14 à D-E16-18 (ADR-0010). Un texte Yarn lit et écrit les drapeaux d'Alundra comme
  des variables, sans que le moteur ni Yarn ne gardent d'état à eux.
- **Contrat** :
  1. Un stockage de la DLL, `AlundraYarnVariableStorage`, implémente `Yarn.IVariableStorage` sur
     `AlundraGameState` et ne garde aucune donnée propre.
  2. **Noms** (D-E16-16) :
     - `$flag_n` est le drapeau d'id `n` de `GameFlags` ; `$tmp_flag_n` est le drapeau d'id
       `n | 0x8000`, dans `TemporaryFlags` ;
     - `n` est un décimal de 0 à 32767, écrit sans zéro de tête, pour qu'un drapeau n'ait qu'un seul
       nom.
  3. **Lecture** : la valeur est le booléen `(GetFlag(id) & (1 << (n & 0x1f))) != 0`, le test des
     opcodes `0x30` et `0x31` (`AlundraEventProgramRunner.cs:1227`).
  4. **Écriture** (D-E16-17) : `true` fait `AddFlag(id, masque)`, comme `0x05` (`:449`) ; `false`
     fait `SetFlag(id, ~masque)`, comme `0x06` (`:457`). Les autres bits du mot ne changent pas.
  5. **Refus** (D-E16-18) : les cas suivants sont journalisés une fois par nom, sans exception levée
     par le stockage et sans changement d'état :
     - tout autre nom ;
     - un `n` hors bornes ou écrit autrement ;
     - une valeur texte ou nombre rangée sous un nom de drapeau ;
     - la lecture d'un nom refusé.
  6. `GetVariableKind` rend `Stored` pour un nom de drapeau et `Unknown` sinon.
  7. **`Clear()` ne touche pas les banques** : leur cycle de vie reste à `AlundraGameState` (entrée de
     carte, nouvelle partie, chargement), conformément à D-E16-6 ; l'appel est journalisé.
  8. `Program` et `SmartVariableEvaluator` sont gardés tels que Yarn les pose ; ils ne servent à
     aucun drapeau.
  9. `AlundraDialogueDirector` pose ce stockage sur chaque runner qu'il crée
     (`AlundraDialogueDirector.cs:171-172`), donc pour tous les dialogues.
  10. Rien ne change dans le moteur, le convertisseur ni le Yarn exporté (aucune variable, §0.2).
- **Tâches** (un commit par tâche avec la mise à jour de ce plan) :
  - **T1 — Mesure du comportement de Yarn** (tests exploratoires, aucun code de production). Sur un
    runner réel avec un stockage de test qui refuse tout, établir trois points :
    - ce que la machine virtuelle de Yarn 3.2.1 fait d'une lecture refusée (exception, valeur
      initiale du `Program`, autre chose) et d'une écriture refusée ;
    - quel type `T` elle demande à `TryGetValue` ;
    - si un dialogue sans `visited()` écrit des variables internes (`$Yarn.Internal.*`) ou appelle
      `Clear()`.

    Les résultats vont dans cette tranche, sourcés par les tests.
  - **T2 — Stockage et branchement** : contrat 1 à 9, avec les tests d'acceptation.
  - **T3 — Documentation** : section « Variables » de `docs/formats/dialogues-yarn.md` (noms, types,
    refus, cycle de vie, rien dans la sauvegarde).
- **Acceptation** : tests d'`Alundra.Tests` sur des Yarn de test compilés avec les déclarations de
  la DLL (`AlundraYarnBindings.CreateDeclarations`) :
  - **lecture** :
    - un bit posé par l'opcode `0x05` est lu vrai par `<<if $flag_n>>` ;
    - `$tmp_flag_n` lit la banque temporaire ;
    - le même `n` dans les deux banques donne deux drapeaux distincts ;
  - **écriture** :
    - `<<set $flag_n to true>>` est vu par l'opcode `0x30` ; `false` n'efface que ce bit ;
    - aux bornes `n` = 0, 31, 32 et 32767, dans les deux banques, l'état obtenu est identique à celui
      des opcodes `0x05` et `0x06` sur le même id ;
  - **refus** : `$flag_32768`, `$flag_07`, `$foo`, `$tmp_flag_x` et un nombre rangé sous
    `$flag_5` donnent une seule ligne de journal par nom, un état des deux banques identique à un
    instantané, et aucune exception levée par le stockage ;
  - **`Clear()`** : banques identiques ;
  - **directeur** : le runner qu'il crée utilise ce stockage ;
  - **cycle de vie** : à l'entrée de carte, un `$tmp_flag_n` posé par Yarn disparaît et un
    `$flag_n` reste (`InstallForMapEntry`) ;
  - **sauvegarde** : si E16.c est livrée, un `$flag_n` posé par Yarn fait l'aller-retour et un
    `$tmp_flag_n` non. Sinon, E16.c ajoute ce test ;
  - **non-régression** : `Alundra.Tests` sans échec, oracle de l'intro inchangé (`0x11` à la
    frame 1704), les 485 dialogues exportés toujours joués.
- **Arrêts** :
  - T1 montre que la machine virtuelle lève une exception sur une lecture refusée, ou qu'un dialogue
    exporté a besoin de variables internes : la tranche s'arrête et la question va à l'auteur
    (O-E16-7) ;
  - Yarn exige du stockage un comportement que l'ADR-0010 ne couvre pas : question à l'auteur.
- **Dépendances** : E16.0 (suites de référence). Indépendante d'E16.a à E16.e.

---

## 2. Mesures

_(Remplie par E16.0.)_

---

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E16-1 | ~~Charger au démarrage~~ — **tranché : plus tard** (D-E16-10). Conséquence connue : un jeu livré n'a aucun moyen de charger avant un écran titre. | E16.d |
| O-E16-2 | ~~Touches de recette~~ — **tranché** (D-E16-11). | E16.d |
| O-E16-3 | Seulement si E16.0 trouve un id persistant ≥ 2048 : l'original écrirait au-delà de `GameFlags`, dans `MapIdToInternalMapIndexTable`. Reproduire, ou corriger (règle « corriger les défauts de l'original ») ? | E16.c |
| O-E16-4 | ~~§9.9 du moteur~~ — **tranché** (D-E16-8). | E16.b |
| O-E16-5 | ~~Qui porte le gestionnaire du livre de sauvegarde ?~~ — **tranché : E16.e** (D-E16-12). | E16.e |
| O-E16-6 | **Comment activer les touches de recette ?** Une variable d'environnement (`ALUNDRA_SAVE_DEBUG=1`) risque le même sort que `ALUNDRA_HUD_DEBUG`, qui n'a jamais atteint le processus du lanceur (D-E13-12). Pistes à comparer en E16.d, sur ce que le lanceur transmet vraiment au jeu : un argument de ligne de commande du lanceur, un réglage du projet, un fichier de configuration à côté du jeu. À trancher par l'auteur **avant E16.d**. | E16.d |
| O-E16-7 | Seulement si E16.f T1 montre que la machine virtuelle de Yarn lève une exception sur une lecture refusée, ou qu'un dialogue exporté a besoin de variables internes de Yarn : erreur visible (dialogue interrompu), valeur initiale du `Program` avec journal, ou autre ? | E16.f |

## 4. Hors périmètre

- Écran titre et choix d'une sauvegarde au démarrage (D-E16-10 : plus tard).
- Relecture des vraies sauvegardes de carte mémoire PS1 (non retenue le 2026-09-27).
- Pont des drapeaux vers les cinématiques : E17 (mise à jour de D-E16-6 du 2026-09-28). Le pont
  vers Yarn est dans E16 (E16.f).
- Variables Yarn autres que les drapeaux (`visited()`, variables déclarées ou calculées) : refusées
  (D-E16-18).
- Lecteurs de `ContentsGameFlag` de l'IA native (coffres, `FunctionTypeA.cs:236-264`) : E14.
- Noms lisibles pour les drapeaux, au-delà des 41 drapeaux de chapitre.
- **Risques résiduels acceptés** (jeu solo) : le sens des drapeaux ne peut pas être validé, donc une
  sauvegarde éditée peut casser la suite de l'histoire ou bloquer le joueur ; une tuile dans les
  bornes mais dans un mur est acceptée.

## 5. Arrêts, budgets et retours arrière

### 5.1 Budgets

- Chaque tranche d'exécution (E16.a, E16.c, E16.d, E16.e, E16.f) a au plus **deux tentatives** au même
  niveau d'exécutant ; ensuite la session principale reprend ou l'exécution monte d'un niveau, jamais
  de troisième tentative identique.
- Chaque frontière à risque (E16.b côté moteur, E16.c pour le format, E16.d pour la recette) a au
  plus **cinq passes** correction puis re-vérification, chacune sur un état réellement changé.
- E16.0 : budget de recherche par question donné dans la tranche.
- Budget épuisé : la tranche passe en ⚠️, la question va au §3, le travail s'arrête ; les tranches qui
  n'en dépendent pas peuvent continuer.

### 5.2 Retours arrière

| Mutation | Tranche | Retour |
|---|---|---|
| Documentation (ce plan, `intro-roadmap.md`) | E16.0 | `git revert` du commit de documentation. |
| ADR-0010 | révision 3 | `git revert` de son commit ; D-E16-6 et D-E16-14 à D-E16-18 restent dans ce plan. |
| Code de la DLL et tests | E16.a, E16.c, E16.d, E16.e, E16.f | Branche du chantier abandonnée (`git switch main`) ; rien n'est fusionné sans l'auteur. |
| Pointeur du sous-module moteur | E16.b | Revenir au pointeur de `main` du parent ; la branche moteur est gardée. |
| Fichiers de sauvegarde écrits par la recette | E16.d | Hors du dépôt, dans le dossier de l'utilisateur ; supprimés par l'auteur s'il le souhaite. |

### 5.3 Arrêts communs

- Une mesure ou un fait qui contredit une décision D-E16 : arrêt, question à l'auteur, plan corrigé et
  relu.
- Un test existant qui devrait changer pour une autre raison que la tranche en cours.
- Une modification de l'auteur indexée par erreur ; un commit sur `main` ; un push.
- Un contrôle qui ne peut s'exécuter qu'après avoir modifié l'état vivant.
- Une exception qui s'échappe d'une capture ou d'une restitution.
- La DLL qui lit ou écrit un fichier de sauvegarde sans passer par le service.
- Un cas de refus couvert dans un seul des deux formats.

## 6. Revue de sécurité (2026-09-27)

Revue en lecture seule par un `security-reviewer` frais sur la révision 1 de ce plan : aucun P0 ni
P1. Le constat F3 de la revue du plan moteur (« le jeu doit valider les valeurs chargées ») est
appliqué ici, en E16.c.

| Réf | Prio | Constat | Décision | Où |
|---|---|---|---|---|
| S1 | P2 | Liste de contrôles incomplète ; argent négatif, `HpMax` négatif ou `Mp` > 4 font lever la jauge | FIX | E16.c : tableau des domaines, tests aux bornes suivis d'un tick de la jauge et de l'inventaire |
| S2 | P2 | La tuile ne peut pas être contrôlée avant de charger le monde ; débordements arithmétiques | FIX | E16.0 question 6 ; E16.c tableau (débordements) et arrêt si aucune source des dimensions |
| S3 | P2 | L'application n'est pas atomique telle qu'écrite | FIX | E16.c « l'objet possède ses propres tableaux », test du dernier champ ; E16.d étapes 1-4 et tests de départ |
| S4 | P2 | Un chargement en cours de partie garde l'état de la session courante | FIX | E16.d étapes 1 et 4 (préconditions, remise à zéro des singletons), tests |
| S5 | P2 | Touches de debug non gardées, capture dans des états que l'original ne sauvegarde pas | FIX ; choix produit → O-E16-1 | E16.d « Touches de recette », tests ; O-E16-1, O-E16-2 |
| S6 | P3 | Le binaire a besoin des mêmes contrôles que le JSON | FIX | E16.c « dans les deux formats », tests ; §5.3 |
| S7 | P3 | Métadonnées non fiables à l'affichage | DEFER vers le plan d'E16.e, prérequis écrit | E16.e |
| S8 | P3 | `SaveSlotIndex`, `LastMapId` et `GameTime` repris du fichier | FIX | E16.c champs et tableau |
| S9 | P3 | Nom de fichier invalide dans le dossier et « emplacement le plus récent » | FIX (le plan moteur filtre la liste par la même règle) | E16.d tests |
| S10 | P3 | Pas de revue de sécurité ni de verifier par tranche | FIX | Tableau des unités en tête ; §5.3 |
| S11 | P4 | Risques résiduels à écrire | FIX (texte) | §4 |

Relevé en passant, hors de ce plan : la touche F1 de la recette de la jauge n'avait aucune garde
(`AlundraWorldProxy.cs:2197`, `:1369-1394` à `cbda4f8`), contrairement à la recette par variable
d'environnement. L'auteur a demandé sa suppression le 2026-09-27, avec celle de la recette par variable (D-E16-13, chantier séparé).
