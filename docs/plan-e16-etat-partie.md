# Plan — E16, état de partie : drapeaux et sauvegarde

Étape E16 de [plan-conversion-totale.md](plan-conversion-totale.md). Elle termine la gestion des
deux banques de drapeaux, puis permet de sauvegarder et de recharger une partie comme l'original,
sur un service de sauvegarde générique ajouté au moteur.

**Statut** : proposé le 2026-09-27 ; **enveloppe et E16.0 approuvées le 2026-09-28**, E16.0 faite
(§2) ; chaque tranche suivante se planifie et s'approuve à part. Position dans la file : **après
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

La première relecture de la révision 3 a rendu **REVISE**, sur un P2. Le plan disait que l'original
ne charge que dans un processus neuf ; or il recharge aussi la sauvegarde dans le processus en cours,
après la mort (« Réessayer »), sans remettre la BSS à zéro. Corrections :
- §0.2 décrit ce chemin ;
- E16.0 le confirme dans le binaire (question 4) ;
- D-E16-19 fixe la remise à zéro au chargement ;
- D-E16-20 renvoie « Réessayer » à l'étape E18 du plan maître ;
- E16.d, étapes 1 et 4, ne s'appuie plus sur le processus neuf.

Deuxième relecture neuve de l'enveloppe et d'E16.0 (révision 3, `b9a0d85`) : **READY**. Enveloppe et
E16.0 approuvées par l'auteur le 2026-09-28. E16.0 est faite (`59fac90`, §2) ; une vérification neuve
du §2 a rendu **CONFIRMED** ; ses quatre remarques P4 (bornes de `_10`, taille de la copie après
sauvegarde, impasses du recomptage, copie lue par `0xC2`) sont corrigées dans le texte.

Relecture du plan corrigé après E16.0, exigée par la règle d'arrêt d'E16.0 :
- première relecture de `2d1f333` : **REVISE**, un P2 : un `$flag_n` au-delà de 2047 aurait été
  perdu sans message au chargement ;
- correction : D-E16-25 et ADR-0011 (`a7c020b`, `7bceb48`) ;
- relecture de clôture : **READY**.

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
  même `n` que `<<flag n>>` pour la banque temporaire. *Plage ramenée à 0..2047 par D-E16-25.*
- **D-E16-17 — Lecture et écriture** : un drapeau est un booléen Yarn ; `true` pose le bit comme
  l'opcode `0x05`, `false` l'efface comme l'opcode `0x06`.
- **D-E16-18 — Tout autre nom est refusé et journalisé**, variables internes de Yarn comprises, tout
  comme un nom de drapeau hors bornes ou une valeur non booléenne rangée sous un nom de drapeau.
  Seul un drapeau est une variable ; rien ne se perd en silence au chargement.

Decisions: see ADR-0010 (`docs/decisions/0010-game-flags-stay-in-the-dll-and-yarn-reads-them-as-variables.md`),
qui consigne aussi D-E16-6.

Réponses de l'auteur sur le chargement (2026-09-28, après la première relecture de la révision 3) :

- **D-E16-19 — Au chargement, `TextCategoryIndex` et `GameVariables` sont remis à zéro**, comme au
  démarrage d'un processus neuf et comme le veut S4 : un chargement ne garde rien de la session en
  cours. Le rechargement après la mort de l'original les garde, lui (§0.2) ; il relève d'E18.
- **D-E16-20 — Le rechargement après la mort (« Réessayer ») n'est pas dans E16.** Il devient
  l'étape **E18** du plan maître, qui corrige aussi la décompilation C# de ce chemin :
  - le champ `SaveSlotIndex`, qui semble compter les essais, est à confirmer dans `ALUN_CD.EXE`, puis
    à renommer avec ses lecteurs ;
  - tout le chemin est à vérifier contre le binaire.

Réponses de l'auteur aux questions d'E16.0 (2026-09-28, §2 et §3) :

- **D-E16-21 — `0x78` fait 3 octets, dans les deux tables** (O-E16-8) : E16.a corrige
  `EventOpcodeSizeTable.cs` dans la DLL et `EventCodeDebugger.cs` dans l'analyseur, sur une branche
  dédiée du sous-module analyseur.
- **D-E16-22 — `SaveSlotIndex` est sauvegardé et restitué** (O-E16-9), dans le domaine 0..255,
  comme l'original. La DLL le nomme d'après ce qu'il compte, les reprises après la mort. Cette
  décision remplace, pour ce champ, la disposition S8 du §6 (« non repris du fichier »), qui le
  prenait pour un numéro d'emplacement.
- **D-E16-23 — Le temps de jeu compte 60 unités par seconde réelle** (O-E16-10). L'affichage divise
  par 60, le plafond reste `0x14996C4` = 99:59:59, et le format est celui de l'original.
- **D-E16-24 — Les autres désaccords de décompilation relevés par E16.0 sont corrigés en E18**
  (O-E16-11) : la division par 60 d'`UpdateMenuStatusText`, `displayMenu` d'`UpdateSavedData`,
  `Offset` de `SaveData.cs`, `0xC2` sur un octet et la valeur rendue par `UpdateMemoryCardProcess`.
  Ils s'ajoutent à `SaveSlotIndex` et au chemin « Réessayer » (D-E16-20). Le port de la DLL suit
  le binaire dès E16.c, sans attendre ces corrections.

Réponse de l'auteur après la relecture qui a suivi E16.0 (2026-09-28) :

- **D-E16-25 — `$flag_n` et `$tmp_flag_n` n'acceptent que `n` de 0 à 2047**, les ids des deux
  banques de 64 mots de l'original (§2, Q1 et Q3). Un `n` plus grand est refusé comme tout autre nom
  (D-E16-18). Avec l'ancienne plage de D-E16-16, un `$flag_n` au-delà du mot 63 aurait été posé en
  mémoire mais jamais sauvegardé, puis perdu sans message au chargement. Les banques de la DLL gardent
  leurs 1024 mots en mémoire.

Decisions: see ADR-0011 (`docs/decisions/0011-yarn-flag-variables-cover-the-original-64-word-banks.md`),
which supersedes the range of D-E16-16 in ADR-0010.

Note sur D-E16-23 : c'est un **écart voulu** avec l'original, qui ajoute une unité par image affichée,
transitions comprises (§2, Q3). Le port compte le temps réel, dans l'unité que l'affichage de
l'original suppose.

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
  - la jauge : drapeaux 1662, 1813 et 1814 (`AlundraHudDirector.cs:250`, `:357-378`) ;
  - la progression des faucons (E15), qui accède directement aux mots de `GameFlags`, sans passer par
    l'API : elle efface un bit de `GameFlags[0x2d]` (`AlundraTextProgress.cs:60`), lit
    `GameFlags[0x2c]` et réécrit `GameFlags[0x2d]` (`:80-86` et la suite de
    `UpdatePlayerProgressState`).

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
- **Rechargement dans le processus en cours, après la mort** (relevé à la relecture du 2026-09-28,
  lu dans la décompilation ; à confirmer par E16.0 question 4) :
  - sur la carte `0x1DD`, `Script_187_0BB` (opcode `0xBB`, « Menu after died », `@ 0x80041A74`,
    `EntityEventHandlers.cs:3513-3553`) pose `g_mapTransitionEffectId = 10` ;
  - l'effet 10 (`GameEngine.cs:326-333`) appelle `InitializeMapWarpPosition` (`@ 0x800315b0`,
    `GameEngine.cs:1480-1498`), qui fait `InitializePlayerStatsAndItems`, puis `UpdateSaveData`
    (copie de `g_saveDataInRam` dans `g_saveData`), puis repart sur `InitialMapId` à la tuile
    `CameraTileX/Y/Z` ;
  - aucun point d'entrée ne tourne, donc la BSS n'est pas remise à zéro : `g_textCategoryIndex` et
    `INT_ARRAY_80191908` gardent leur valeur de la session (§5.7 de `plan-e15-yarn.md` : « jamais
    remis à zéro ») ;
  - la décompilation journalise `SaveSlotIndex + 1` sous le nom « Retry = »
    (`EntityEventHandlers.cs:3527-3537`) et incrémente ce champ à chaque essai
    (`GameEngine.cs:1482-1485`). Son sens (compteur d'essais ?) est à établir ;
  - la DLL ne porte pas ce chemin : `0xBB` est sauté par taille (`EventOpcodeSizeTable.cs:218`).
    Il relève d'E18 (D-E16-20).
- Texte d'un emplacement (`UpdateMenuStatusText`, `GameEngine.cs:2695-2747`) : le chapitre, tiré
  des 41 drapeaux de fin de chapitre (`ChapterFlags.cs`), et `HP xx TIME hh:mm:ss`, calculé depuis
  `HpMax` et le temps de jeu.
- **Temps de jeu, contradiction à trancher** : le compteur prend +1 à chaque appel de `EndGame`
  (`GameEngine.cs:1471-1474`, fin d'image) et plafonne à `0x14996C4` = 21 599 940 = 99:59:59 × 60 ;
  l'analyseur l'affiche pourtant comme un nombre de secondes (`GameEngine.cs:2724-2733`).
  *Tranché par E16.0* : le binaire l'affiche en soixantièmes de seconde, et la décompilation a perdu
  la division par 60 (§2, Q3) ; le port compte 60 unités par seconde réelle (D-E16-23).
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

### E16.0 — Mesure ✅ (lecture seule, analyseur, corpus, `ALUN_CD.EXE` ; faite le 2026-09-28, résultats au §2, questions O-E16-8 à O-E16-11 à l'auteur avant E16.a et E16.c)

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
  4. **Confirmer dans `ALUN_CD.EXE`** :
     - la chaîne de sauvegarde du §0.2 (`@ 0x8007B998` → `@ 0x8003153C` → `@ 0x8005EC44`), et
       lister les types de sprite et les cartes qui portent le gestionnaire du livre de sauvegarde ;
     - le rechargement après la mort du §0.2 (`@ 0x80041A74` → effet 10 → `@ 0x800315b0` →
       `UpdateSaveData`) : tout ce qu'il remet ou non à zéro, dont `g_textCategoryIndex` et
       `INT_ARRAY_80191908` ;
     - l'écrivain qui remplit `g_saveDataInRam` pendant la partie, et le sens du champ
       `SaveSlotIndex`, qu'E16.c écarte du fichier.

     Ces constats servent E16.c et E18 ; la correction de la décompilation reste à E18 (D-E16-20).
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

### E16.a — Opcodes de drapeaux 🧪 (DLL, analyseur ; approuvée le 2026-09-28, mode ASK ; T1 à T6 faites et vérifiées CONFIRMED, reste la recette en jeu T7 proposée)

- **But** : porter les opcodes de drapeaux qui apparaissent dans le corpus d'après E16.0.
- **Contenu** :
  - `0x32`, `0x34` et `0x35`, un à la fois, recoupés avec le binaire ; `0x32` donne enfin un
    appelant à `XorFlag` ;
  - la **famille du paramètre mémorisé, `0x78` à `0x81`, comme une seule unité** : tous ses opcodes
    présents dans le corpus sont portés ensemble, avec l'écriture de `EventProgramState._34`. Si
    E16.0 montre que `0x7B`, `0x7C`, `0x80` et `0x81` n'apparaissent pas, la famille entière est
    exclue d'E16, avec cette raison écrite ici.
    *E16.0 (§2, Q2)* : ils apparaissent tous, donc la famille entre. `0x7A` n'apparaît pas ;
  - **`0x78` sur 3 octets** (D-E16-21), dans `EventOpcodeSizeTable.cs:151` et dans
    `EventCodeDebugger.cs` de l'analyseur (branche dédiée du sous-module, pointeur déplacé dans ce
    dépôt).
- **Acceptation** : un test par opcode contre la décompilation ; un **test de séquence** sur un
  programme réel du corpus qui écrit `_34` puis y revient (opcode d'écriture jusqu'à l'opcode de
  retour) ; `Alundra.Tests` sans échec ; oracle de l'intro inchangé (`0x11` à la frame 1704) ; plus
  aucun saut par taille pour les opcodes portés.
- **Arrêt** : un opcode dont le binaire contredit la décompilation → consigné, décision de l'auteur
  avant de le porter ; l'oracle de l'intro qui bouge → la tranche s'arrête.
- **Dépendances** : E16.0.

#### Plan détaillé d'E16.a (2026-09-28)

Relectures :
- première (`464e5ab`) : **REVISE**, un P2 : `TerrainHeight` manque aussi aux entités sans
  contrôleur et avant leur première mise à jour ;
- correction : D-E16-30 (`6398fa0`) ;
- seconde : **READY**.

**Approuvé par l'auteur le 2026-09-28** (mode ASK : arrêt sur les vraies décisions, P3/P4 corrigés dans la tâche et rapportés).

**Faits établis par la reconnaissance** (lecture seule, deux passes contre-vérifiées ; scripts dans
`scratchpad/e16a/` et `scratchpad/e16a-prod/`) :

- **Sémantique des opcodes de la tranche**, confirmée dans le binaire :

  | Opcode | Gestionnaire | Lit | Écrit | Rend |
  |---|---|---|---|---|
  | `0x32` | `0x8003DEFC`, `EntityEventHandlers.cs:1102-1109` | drapeau `(b2 << 8) \| b1` | `XorFlag(id, 1 << (b1 & 0x1f))` | 3 |
  | `0x34` | `0x8003E128`, `:1132-1149` | 4 drapeaux | `Result = 0` au premier bit posé, sinon 1 | 9 |
  | `0x35` | `0x8003E2DC`, `:1152-1163` | 1 drapeau | rien | 3 si le bit est effacé ; 0 (attendre) s'il est posé |
  | `0x78` | `0x8003FB10` | b1, b2 | `_34 = CodeIndex + 3` | saut signé 16 bits (toujours) |
  | `0x79` | `0x8003FB44` | b1, b2 (si saut) | `_34 = CodeIndex + 3` si `Result != 0` | saut si `Result != 0`, sinon 3 |
  | `0x7B` / `0x7C` | `0x8003FBD4` / `0x8003FC74` | drapeau (b1, b2), saut (b3, b4) | `_34 = CodeIndex + 5` si le bit est posé / effacé | saut, sinon 5 |
  | `0x7D` | `0x8003FD14` | rien | rien | `_34 − CodeIndex` |
  | `0x7E` / `0x7F` | `0x8003FD24` / `0x8003FD4C` | `Result` | rien | `_34 − CodeIndex` si `Result != 0` / `== 0`, sinon 1 |
  | `0x80` / `0x81` | `0x8003FD74` / `0x8003FDF8` | drapeau | rien | `_34 − CodeIndex` si le bit est posé / effacé, sinon 3 |
  | `0x2C` | `0x8003DC84`, `:1008-1014` | type de recherche (b1) | `Result = 1` si aucune entité, sinon 0 | 2 |
  | `0x3E` | `0x8003E708`, `:1298-1310` | rien | `Result = 1` si le joueur chevauche l'entité | 1 |
  | `0x6E` | `0x8003F9D4`, `:2146-2151` | rien | `Result = ForceAdjusted` de l'entité | 1 |
  | `0x8D` | `0x800404A8`, `:2597-2615` | type de recherche (b1) | `Result = 1` si une entité trouvée a `PosZ <= TerrainHeight + 1`, sinon 0 | 2 |
  | `0xAD` | `0x80041344`, `:3221-3278` | 2 types de recherche, 3 octets **signés**, 3 octets non signés | `Result = 1` si une entité de la 2e recherche est dans la boîte, sinon 0 | 9 |
  | `0xB8` | `0x80041988`, `:3471-3494` | type de recherche (b1), valeur (b2) | `Result = 1` si une entité trouvée a `CurrentAnimationId == b2` (`+0x90`), sinon 0 | 3 |

  Les tailles d'`EventOpcodeSizeTable.cs` sont justes pour tous, sauf `0x78` (D-E16-21). `0x7A`
  n'apparaît pas dans le corpus : il reste non porté.
- **Désaccords avec la décompilation**, tranchés selon le binaire (D-E16-27) :
  - `0xAD` : sa boucle `while (i > 0)` ne teste jamais l'entité d'indice 0. Le binaire les teste toutes
    (boucle testée en bas). Dans 54 usages sur 67, la 2e recherche ne trouve que le joueur : portée
    telle quelle, `0xAD` rendrait presque toujours faux. La décompilation oublie aussi `Result = 0`
    quand la 2e recherche est vide et quand la boucle s'épuise ;
  - `0xB8` : le binaire compare `CurrentAnimationId` (`+0x90`), alors que la décompilation dit
    `TargetAnimationId` et que les deux tables l'appellent « Check TargetDirection » ;
  - vus en passant, sans être portés ici : `0xB7` passe `variables[1]`, et non `variables[2]`, comme
    type de recherche, et a la même boucle que `0xAD` ; `0x8D` parcourt ses entités à rebours (sans
    effet) ; `0x7D` calcule une variable jamais lue.
- **Producteurs de `Result`** : ce sont ces six opcodes qui précèdent 61 des 77 occurrences de `0x79`,
  `0x7E` et `0x7F` : `0xAD` ×48, `0x2C` ×5, `0x3E` ×4, `0x8D` ×2, `0x6E` et `0xB8` ×1. Leurs autres
  producteurs (`0x07`, `0x2F`, `0x3B`) sont déjà portés. Le producteur des 4 derniers `0x7F` n'est
  atteint que par un saut dynamique (`_34`) : non établi. Aujourd'hui, `UnknownOpcode` saute ces
  opcodes sans toucher `Result` (`AlundraEventProgramRunner.cs:1698-1724`).
- **Côté DLL** :
  - `EntitySearchService.GetMatchingEntitiesBySearchType` (`EntitySearchService.cs:98`) sert `0xAD`,
    `0x2C`, `0x8D` et `0xB8` ;
  - `PosX/PosY/PosZ` (`AlundraEntityScriptProxy.cs:134-136`), `RidingEntity` (`:140`),
    `CurrentAnimationId` (`:87`) et `ForceAdjusted` (`:171`) sont déjà tenus à jour ;
  - `TerrainHeight` (`:148`) n'est écrit qu'en `:536`, sous la garde
    `Controller != null && !immediateAtSpawn` (`:532-537`). Il n'est donc tenu que pour les PNJ qui
    ont un contrôleur, et seulement après leur première mise à jour de support. Il reste à 0 pour le
    joueur, pour les entités sans contrôleur (sprites seuls, qui existent en production :
    `:986-990`) et pour toute entité avant sa première mise à jour, par exemple quand un programme
    de chargement la teste dès son apparition. Le joueur calcule bien la hauteur de son terrain dans
    `UpdateFloorHeight` (`:1539-1552`), mais ne la garde que dans `FloorHeight`. `0x8D` teste le
    joueur dans 41 de ses 183 usages, et d'autres entités dans les 142 autres ;
  - `FillDataFromCommand` ne lit que des octets non signés (`AlundraEventProgramRunner.cs:403-427`) :
    `0xAD` doit convertir ses trois premiers décalages en octets signés ;
  - modèles déjà portés : `0x05`/`0x06` (`:445-459`), `FlagBranch`, `WaitUntilFlagOn`, `CheckFlagsOn`
    (`:1222-1264`), `DestroyMatchingEntities` (`0x2E`, `:1322`), `EntityInArea` (`0x07`, `:1804`),
    `0x70` (`:992-996`), `LogDegradedNoPlayerOpcodeOnce` (`:1767`) ;
  - les programmes B et C gardent leur état d'un appel à l'autre, donc `_34` survit à une attente
    (`0x1E`, `0x37`) ; A, D, E et F partagent un état de travail ;
  - `EventProgramState.cs:16-18` dit encore que `_34` n'est lu par aucun opcode porté ;
  - `HeadlessIntroSimulation.ImplementedOpcodes` (`Alundra.Tests/IntroTraceHarnessTests.cs:319-323`)
    recopie à la main l'ensemble des opcodes portés ; il a déjà pris du retard deux fois.
- **Intro** : aucun opcode de la tranche, producteurs et lecteurs compris, n'est atteignable sur la
  carte 389, seule carte du harnais de l'intro (`IntroTraceHarnessTests.cs:50`) ; l'oracle ne peut
  pas bouger par eux.
- **Occurrences dans le corpus** : `0x2C` 772 (117 cartes), `0x3E` 458 (82), `0x6E` 210 (98), `0x8D`
  183 (105), `0xB8` 95 (22), `0xAD` 67 (34), plus celles du §2 pour la famille et `0x32`/`0x34`/
  `0x35`. Porter ces opcodes change donc le comportement de nombreuses cartes, dans le sens de
  l'original.
- **Analyseur** : `EventCodeDebugger.cs:248` (`0x78`, taille 4) et `:312` (`0xB8`, nom). Ni le
  convertisseur ni la DLL ne le référencent, donc l'export ne change pas. L'analyseur n'a pas de tests
  pour ces fichiers ; son projet est `AlundraTools/AlundraEngine/AlundraEngine.csproj`. Le sous-module
  est sur `master`.

**Décisions de l'auteur pour E16.a** (2026-09-28) :

- **D-E16-26** — E16.a porte aussi les six producteurs de `Result` : `0xAD`, `0x2C`, `0x3E`, `0x8D`,
  `0x6E`, `0xB8`.
- **D-E16-27** — Là où le binaire contredit la décompilation, la DLL suit le binaire. `0xAD` teste
  toutes les entités et pose `Result = 0` sur chaque sortie sans résultat ; `0xB8` compare
  `CurrentAnimationId` et s'appelle « Check CurrentAnimationId » dans la table de la DLL.
- **D-E16-28** — E16.a corrige aussi la décompilation de ces opcodes dans l'analyseur, sur la même
  branche que la taille de `0x78` :
  - la boucle et les `Result = 0` de `0xAD` ;
  - le champ et le nom de `0xB8` ;
  - le paramètre et la boucle de `0xB7` ;
  - l'ordre de parcours de `0x8D` ;
  - la variable morte de `0x7D`.

  Les désaccords d'E16.0 restent à E18 (D-E16-24).
- **D-E16-29** — E16.a tient à jour `TerrainHeight` du joueur, à partir de sa sonde de sol existante,
  après avoir vérifié dans le binaire que c'est la même valeur que l'original. Sinon, arrêt et
  question à l'auteur.
- **D-E16-30** (réponse de l'auteur après la relecture d'E16.a) — D-E16-29 s'étend à **toutes les
  entités** que `0x8D` peut tester : joueur, PNJ avec ou sans contrôleur, dès leur apparition. T3.1
  vérifie dans le binaire où et quand l'original pose `TerrainHeight` pour toute entité ; E16.a le
  pose de même, depuis la même sonde. Si le binaire diffère, arrêt (O-E16-12).

**Contrat** :

1. Chaque opcode de la tranche a un `case` dans `Dispatch` qui suit le tableau ci-dessus ; plus aucun
   n'est sauté par taille. Chaque `case` cite son gestionnaire (adresse) et sa ligne de décompilation,
   et signale l'écart quand elle diverge.
2. `0x78` fait 3 octets dans `EventOpcodeSizeTable.cs` ; `0xB8` s'y appelle « Check CurrentAnimationId ».
3. `TerrainHeight` de toute entité est tenu à jour selon D-E16-29 et D-E16-30, là et quand
   l'original le pose ; rien d'autre ne change dans la physique.
4. `EventProgramState.cs` décrit `_34` tel qu'il est désormais utilisé ;
   `HeadlessIntroSimulation.ImplementedOpcodes` contient tous les opcodes portés.
5. L'analyseur, sur la branche `chantier/e16a-opcodes` de son sous-module :
   - `EventCodeDebugger.cs` : `0x78` fait 3 octets, `0xB8` s'appelle « Check CurrentAnimationId » ;
   - `EntityEventHandlers.cs` suit le binaire pour `0xAD`, `0xB8`, `0xB7`, `0x8D` et `0x7D`
     (D-E16-28) ;
   - le projet `AlundraEngine` compile ; le pointeur du sous-module est déplacé dans ce dépôt.
6. Rien ne change dans le moteur, le convertisseur ni l'export.

**Tâches** (un commit par tâche avec la mise à jour de ce plan ; build à 0 erreur et `Alundra.Tests`
sans échec avant chaque ✅) :

- ✅ **T1 — Drapeaux `0x32`, `0x34`, `0x35`** (DLL ; fait le 2026-09-28 : trois `case` et deux aides, `WaitUntilFlagOff` et `CheckFlagsOff` ; 11 tests, dont un par occurrence réelle, qui vérifient les octets du corpus puis exécutent l'instruction ; `Alundra.Tests` 1372/1372). Tests sur programmes synthétiques
  (`NewDocument`, `AlundraEventProgramRunnerTests.cs:18`) : bascule par `XorFlag` (`0x32` donne son
  premier appelant à `XorFlag`), `Result` de `0x34` pour 0 à 4 bits posés, `0x35` qui attend puis
  avance. Un test par opcode sur son occurrence réelle :
  - `0x32` : Ancient Shrine-26, D[1], octets `32 07 80` en 1529 ;
  - `0x34` : Church (basement, Holy sword)-137, B[5], en 451 ;
  - `0x35` : Ancient Shrine-28, C[14], `35 5A 80` en 738.
- ✅ **T2 — Producteurs `0x2C`, `0x3E`, `0x6E`, `0xAD`, `0xB8`** (DLL, D-E16-26 et D-E16-27 ; fait le 2026-09-28 : cinq `case`, `0xAD` et `0xB8` selon le binaire, `0xB8` renommé dans la table ; 26 tests, dont les cinq occurrences réelles ; relecture : `0x6E` recopie `ForceAdjusted` tel quel, comme le binaire ; `Alundra.Tests` 1398/1398). Tests :
  - `0x2C` : aucune entité, puis une ;
  - `0x3E` : joueur sur l'entité, sur une autre, et sans joueur (chemin dégradé journalisé) ;
  - `0x6E` : `ForceAdjusted` à 0 puis à 1 ;
  - `0xAD` : une seule entité trouvée (l'indice 0 est testé), plusieurs, aucune ; décalages négatifs ;
    bornes incluses ; `Result = 0` sur les trois sorties sans résultat, même si `Result` valait 1
    avant ;
  - `0xB8` : `CurrentAnimationId` égal et différent, et `TargetAnimationId` égal sans effet.

  Plus une occurrence réelle chacun :
  - `0x2C` : Ancient Shrine - Golem-34, `2C 00` en 83 ;
  - `0x3E` : Ancient Shrine-26, C[28], en 1189 ;
  - `0x6E` : Ancient Shrine-27, en 546 ;
  - `0xAD` : Inoa (inner)-164, en 100 ;
  - `0xB8` : Arena Black Dragon (Boss)-323, B[2], en 108.
- ✅ **T3 — Hauteur de terrain de toute entité, puis `0x8D`** (DLL, D-E16-29 et D-E16-30). Deux
  temps :
  1. **Mesure** :
     - dans le binaire : où l'original écrit `TerrainHeight` (`+0x138`) pour toute entité, joueur
       compris, avec quelle valeur, à quel moment de l'image par rapport aux scripts, et dès
       l'apparition ou non ;
     - comparaison avec `ComputeTerrainHeight`, que calcule déjà `UpdateFloorHeight` ;
     - dans le corpus : les entités que visent les usages de `0x8D` autres que le joueur (types de
       recherche `0x80`, `0x89` et ids d'enregistrement), avec les programmes qui les testent dès leur
       apparition.

     Résultats consignés ici, sourcés. Si la valeur ou le moment diffèrent de ce que la DLL peut
     reproduire, la tâche s'arrête (O-E16-12).
  2. Si la mesure concorde :
     - `TerrainHeight` de toute entité est posé au même endroit du cycle que l'original, dès
       l'apparition si l'original le fait ;
     - `0x8D` est porté.

     Tests de `0x8D`, chacun avec le `Result` attendu justifié par l'original :
     - le joueur au sol et en l'air ;
     - un PNJ avec contrôleur ;
     - une entité sans contrôleur ;
     - une entité testée par un programme de chargement avant sa première mise à jour ;
     - `TerrainHeight` de chacune après un tick ;
     - occurrence réelle : Arena Zorgia (Boss)-321, B[1], `8D 81` en 69.

  **Résultats de T3.1** (2026-09-28 ; deux lectures indépendantes du binaire, concordantes, et un
  relevé du corpus ; scripts dans `scratchpad/e16a-t3/`) : **reproductible, pas d'arrêt.**
  - **Où l'original pose `TerrainHeight`** (`+0x138`) :
    - à l'apparition de **toute** entité : `InitializeEntity` (`0x80039D04`, `EntityManager.cs:127-128`)
      appelle `ComputeEntityGroundHeight` (`0x800370C4`) et range le résultat (`0x80039EF8`). Aucun
      script ne tourne dans cette fonction ; le programme de chargement vient plus tard ;
    - à chaque image, pour toute entité active (statut `Normal` ou `Deactivated`, sans
      `BlockedByEntity`, joueur et sprites seuls compris ; `UpdateEntityLists`, `0x800384F4`) :
      `UpdateEntitiesPhysics` (`0x80038364`) → `MoveEntity` (`0x80037E34`) → `ComputeZPosition`
      (`0x80037604`, `0x8003768C`) et `ComputeXYPosition` (`0x80037844`, `0x80037DF8`), qui
      recalculent `ComputeEntityGroundHeight` ; la dernière écriture est faite à la position finale ;
    - une entité portée par une plateforme (`PlatformEntity`) reprend la valeur de la plateforme
      sans la recalculer (`MoveEntity`, `0x80037E88`–`0x80037E90`) ;
    - un gestionnaire d'IA native (fonction de type C, cas 10, `0x8006AD0C`) décale la valeur quand
      le mouvement a été contrarié. L'IA native n'est pas portée (E14) : hors de cette tranche.
  - **Quand** : les scripts tournent avant la physique dans l'image (`UpdateEntities`, `0x8003B388` :
    événements puis physique). Un script lit donc la valeur de l'image précédente, ou celle de
    l'apparition. Aucun script ne peut voir une valeur jamais calculée.
  - **Même valeur** : `ComputeTerrainHeight` (`AlundraEntityScriptProxy.cs:1286-1304`) porte la même
    formule : maximum sur les quatre coins de l'emprise, pentes comprises, repli à 0.
  - **Cibles de `0x8D`** (corpus) :
    - tables B (110) et C (73), jamais A : aucun test pendant le chargement ;
    - types de recherche : identifiant d'enregistrement 61, propriétaire 58, joueur 41, enfants du
      propriétaire 23 ;
    - aucune des 160 cibles résolues n'est sans contrôleur. Les 23 recherches « enfants du
      propriétaire » ne se résolvent pas statiquement.

    Tenir `TerrainHeight` pour toute entité, comme l'original, couvre tous ces cas.
  - **Écart de moment connu** : la DLL enchaîne scripts et physique entité par entité (`Update`,
    `:927-1001`), là où l'original fait tous les scripts puis toute la physique. Un script peut donc
    lire la valeur de cette image pour une entité déjà mise à jour. C'est la même classe d'écart que
    celle déjà documentée pour `FloorHeight`.
  **Fait le 2026-09-28 (T3.2)** :
  - aucun code de la DLL ne lisait `TerrainHeight` avant `0x8D` (relevé avant modification) : l'élargir
    ne change rien d'autre ;
  - `EvaluateEntitySupport` écrit désormais `TerrainHeight` pour toute entité, à l'apparition et à
    chaque tick. La valeur locale qui alimente l'atterrissage garde sa garde d'origine. Le joueur
    l'écrit dans `UpdateFloorHeight` (chaque tick) et dans `AdoptPlayerPawn` (apparition) ;
  - héritage par plateforme non implémenté : la DLL n'assigne jamais `PlatformEntity`, il n'y a donc
    rien à hériter ;
  - `0x8D` est porté ;
  - 12 tests, dont l'occurrence réelle d'Arena Zorgia ; l'écriture du joueur à l'apparition n'est
    vérifiée qu'à la lecture du code, `AdoptPlayerPawn` n'étant pas atteignable sans moteur ;
  - `Alundra.Tests` 1410/1410, oracle de l'intro inchangé.
- ✅ **T4 — Famille `0x78` à `0x81`** (fait le 2026-09-28 : neuf `case`, `0x7A` non porté, `0x78` à 3 octets dans la table, doc de `_34` et `ImplementedOpcodes` à jour pour T1 à T4 ; 19 tests, dont les trois séquences réelles A, B et C de bout en bout ; `Alundra.Tests` 1429/1429, oracle de l'intro inchangé) (DLL, D-E16-21). Taille de `0x78` à 3 ; un test par opcode (saut
  pris et non pris, écriture et relecture de `_34`) ; `EventProgramState.cs` et `ImplementedOpcodes`
  mis à jour (tous les opcodes de T1 à T4). **Trois tests de séquence** sur des programmes réels, du
  point d'entrée jusqu'au retour :
  - A : Lake Shrine (inner)-337, B[2] (entrée 512) : `0x78` en 807 saute en 822 sur `0x81` ;
  - B : Cave-140, B[4] (entrée 744) : `0x78` en 773, puis `0x5B`, `0x1E` et `0x7D` en 1361, qui
    revient en 776. `0x1E` (marche) ne se termine qu'une fois l'entité déplacée : le test fait
    avancer le mouvement (ticks, ou position pilotée) pour atteindre le `0x7D` ;
  - C : Lizardman's Lair (Boss)-411, B[9] (entrée 1712) : `0x7B` en 1713 (drapeau `0x807A`), puis
    `0x37`, `0x07` et `0x7E` en 1747, dont le `Result` vient du `0x07`.
- ✅ **T5 — Analyseur** (fait le 2026-09-28 : `82f1718` sur `chantier/e16a-opcodes` du sous-module ; `EventCodeDebugger.cs` et cinq fonctions d'`EntityEventHandlers.cs` suivent le binaire ; `0xB7` contrôlé dans le binaire avant correction, `variables[1]` ; `AlundraEngine` compile à 0 erreur ; le `Breakpoint.TriggerBreak()` en tête de `0xB7` est laissé tel quel, hors de la demande) (contrat 5, D-E16-28). Branche `chantier/e16a-opcodes` créée depuis le commit
  que le parent enregistre pour le sous-module (`118c6c5`, égal à `master` au 2026-09-28 ; s'ils ont
  divergé, arrêt et question) ; build d'`AlundraEngine.csproj` à 0 erreur ; commit dans le sous-module, puis commit du
  pointeur dans ce dépôt. Rien n'est poussé.
- ✅ **T6 — Vérification finale** (faite le 2026-09-28 : vérificateur neuf **CONFIRMED**, sans constat P0 à P2 ; `Alundra.Tests` 1429/1429, convertisseur 400/400, oracle de l'intro inchangé, analyseur compilé. Remarque P3 corrigée ici : `ImplementedOpcodes` omettait 23 opcodes portés par des tranches antérieures ; la liste égale désormais les `case` de `Dispatch`, et l'annexe régénérée `docs/intro-programs-389.txt` ne change que 17 étiquettes « [NOT IMPLEMENTED] » en « [implemented] ». Remarque P4 reportée : `0xAD` prend comme base la première entité trouvée, comme le binaire ; un écart n'apparaîtrait que si l'ordre de recherche de la DLL différait de celui de l'original et que la première recherche trouvait plusieurs entités). Contrat : `Alundra.Tests` et tests du convertisseur sans échec ; oracle de
  l'intro inchangé ; vérificateur neuf sur toute la tranche.
- ⏳ **T7 — Recette en jeu** (auteur, proposée) : trois lieux qui exercent la tranche, si les systèmes
  qu'ils demandent par ailleurs le permettent (sinon, on consigne ce qui est atteignable) :
  - l'arène de Zorgia (321), attente de l'atterrissage du joueur (`0x8D`) ;
  - le repaire des Lizardmen (411), boucle `0x7B` → `0x7E` ;
  - un buggy de Torla (55 à 60), test de chevauchement (`0x3E`).

**Acceptation** : tous les tests ci-dessus ; `Alundra.Tests` sans échec ; oracle de l'intro inchangé
(`0x11` à la frame 1704) ; aucun opcode de la tranche sauté par taille ; analyseur compilé ;
vérification finale neuve **CONFIRMED**.

**Limite connue** : 4 occurrences de `0x7F` (Coast house-143) ne sont atteintes que par un saut
dynamique (`_34`), et leur producteur de `Result` n'est pas établi. Si ce producteur n'est pas porté,
ces `0x7F` lisent un `Result` périmé. La tranche ne peut pas le corriger sans le connaître : la limite
est consignée. Le runner n'a pas de borne d'itérations en production (`MaxIterationsPerCall` n'est
posé que par les tests, `AlundraEventProgramRunner.cs:98`, `:332`) : si l'un de ces chemins formait une
boucle sans attente, il tournerait sans fin.

**Arrêts** :
- T3.1 ne retrouve pas dans le binaire la valeur ou le moment de `TerrainHeight` pour une entité que
  `0x8D` teste (O-E16-12) ;
- un opcode dont le binaire contredit à nouveau la décompilation sur un point non tranché ;
- l'oracle de l'intro qui bouge ;
- un test existant qui devrait changer pour une autre raison que la tranche ;
- un producteur qui demande un sous-système non porté ;
- le build de l'analyseur qui échoue pour une autre raison que la tranche.

**Retour arrière** : branche du chantier (§5.2) ; branche de l'analyseur gardée et pointeur ramené à
celui de `main` (§5.2).

### E16.b — Service de sauvegarde du moteur ✅ (moteur ; approuvée le 2026-09-28, « fait tout E16 », mode ASK ; faite et vérifiée CONFIRMED le même jour)

- **But** : exécuter le plan moteur `save-game-service-tasks.md` (T0.1 à T4.2), puis déplacer le
  pointeur du sous-module.
- **Révision 3 du plan moteur (2026-09-28)**, après le merge Yarn du moteur (`793d1ee8`) : ses
  citations restent justes ; son ADR devient l'ADR-0044 du moteur (0042 et 0043 sont prises par Yarn) ;
  la ligne citée du document Yarn devient `:164` ; une note renvoie les variables Yarn d'Alundra à E16.f.
  Le fichier reste non suivi dans le sous-module jusqu'à T0.1.
- **Fait le 2026-09-28** : plan moteur exécuté sur `chantier/save-game-service` (`f6220b8e` à
  `c6e8e2aa`) :
  - T0.1 : ADR-0044 du moteur et précision de sa règle §9.9 ;
  - T1.1 : stockage ;
  - T2.1 : archive symétrique ;
  - T2.2 : formats JSON et binaire ;
  - T3.1 : `SaveGameService` public, par `GameSettings.SaveGames` ;
  - T4.1 : documentation ;
  - T4.2 : vérification de clôture **CONFIRMED**, sans P0 à P2, trois P4 reportés (O6 à O8 du plan
    moteur).
  `CasaEngine.Tests` 2355/2355. Le pointeur du sous-module est déplacé ; ce dépôt compile à 0 erreur,
  avec `Alundra.Tests` 1429/1429 et le convertisseur 400/400. Rien n'est poussé, et la modification
  de l'auteur (`Program.cs`) reste non indexée.
  **Pour E16.c** : les constructeurs du service sont internes au moteur, donc les tests de la DLL ne
  peuvent pas construire un service sur un dossier temporaire. À trancher à la planification d'E16.c.
- **Acceptation** : plan moteur clos, verifier **CONFIRMED** ; ce dépôt compile avec le nouveau
  pointeur.
- **Arrêt** : O1 du plan moteur refusé → le plan moteur s'arrête et se replanifie, E16.c à E16.e
  attendent.
- **Dépendances** : aucune dans ce plan ; la question O1 du plan moteur (§9.9) doit être tranchée
  avant.

### E16.c — Objet de sauvegarde d'Alundra ✅ (DLL ; plan détaillé plus bas, READY, approuvé par l'auteur le 2026-09-28 ; faite et vérifiée CONFIRMED le même jour, `b5ae0f2` à `24bde7a`)

- **But** : `AlundraSaveGame`, l'objet que la DLL confie au service (D-E16-4).
- **Contenu** :
  - les champs de `SaveData` : `GameTime`, `InitialMapId`, `CameraTileX/Y/Z`, `GameFlags`,
    `MapIdToInternalMapIndexTable`, les neuf stats, `NumberOfItems`, et le compteur de reprises
    après la mort, `SaveSlotIndex` dans l'original (D-E16-22). La DLL n'a pas encore ce compteur :
    E16.c l'ajoute à `AlundraGameState`, sous un nom qui dit ce qu'il compte. Rien ne l'incrémente
    avant E18.
    `LastMapId`, `Field_757` et `Offset` ne sont pas écrits : E16.0 ne leur trouve aucun lecteur, et
    `Offset` n'existe pas dans l'original (§2, Q3). L'identité d'un emplacement reste son nom dans le
    service ;
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
  - le port du compteur de temps de jeu (D-E16-23) : 60 unités par seconde réelle, plafond
    `0x14996C4`. Le résumé divise par 60 comme le binaire (`0x800311D4`, §2, Q3), et non comme
    `GameEngine.cs:2724` ;
  - capture depuis `AlundraGameState` ; la capture suit les sources d'`UpdateSavedData`
    (`GameEngine.cs:2648-2662`) : carte courante, tuile du joueur, texte de l'emplacement, temps de
    jeu ;
  - **l'objet possède ses propres tableaux** : au chargement, le service remplit ceux de
    `AlundraSaveGame`, jamais ceux d'`AlundraGameState.Instance` (dont les tableaux sont `readonly`,
    `AlundraGameState.cs:170-231`) ; l'état vivant n'est touché qu'à l'application (E16.d) ;
  - **une sauvegarde est une donnée non fiable, dans les deux formats** (le CRC-32 du binaire se
    recalcule, le JSON s'édite à la main) : `TryValidate` (C4) contrôle chaque champ contre le domaine
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
  | `Falcon`, `FalconTemp` | 0..50 | `IncreaseFalcon2` (`0x8004E6EC`) et `UpdateNumberOfFalcon` (`0x8004E738`) plafonnent à `0x32` (§2, Q6) |
  | `NumberOfItems` | indices impairs `id × 2 + 1` dans [0, `ItemsProperties[id × 5 + 3]`] ; indices pairs et indices ≥ 198 à 0 | `AlundraGameState.cs:173-180` ; plafond par objet, `Data/items-properties.json`, déjà chargé (`AlundraItemTables.cs:132-134`) (§2, Q6) |
  | compteur de reprises (`SaveSlotIndex`) | 0..255 | un octet, plafonné à `0xFF` par `InitializeMapWarpPosition` (§2, Q4 ; D-E16-22) |
  | `MapIdToInternalMapIndexTable` | chaque valeur est une clé de `world-index.json` | `AlundraWorldIndexTable.Resolve` (`:88`) ; une valeur inconnue ferait avorter à jamais le portail qui la lit (`AlundraWarpDirector.cs:304`) |
  | `InitialMapId` | clé de `world-index.json`, monde présent au catalogue | idem |
  | `CameraTileX/Y/Z` | dans les dimensions de la carte, et sans débordement de `(tuile × largeur + largeur / 2) << 16` ni de `Z << 20` | `AlundraWorldProxy.cs:1563-1565` ; dimensions lues dans `tilemap/<nom>.tileMap` de la carte (§2, Q6). Pour `CameraTileZ`, la hauteur de tuile est un octet (+1 en pente), le maximum observé sur un sol est 55, et le débordement commence à 2048 : borne haute arrêtée dans le plan détaillé d'E16.c |
  | `GameTime` | 0..`0x14996C4` | plafond du compteur (`GameEngine.cs:1471-1474`, `0x80042834`) ; soixantièmes de seconde (D-E16-23) |

- **Acceptation** (réduite par D-E16-31 : voir l'acceptation du plan détaillé ci-dessous) : tests —
  - aller-retour identique en JSON et en binaire ; capture puis application donnent un état identique
    champ par champ ; `TemporaryFlags` inchangé ; ni `TemporaryFlags`, ni `TextCategoryIndex`, ni
    `GameVariables` dans aucun des deux formats ; texte du résumé comparé au calcul
    d'`UpdateMenuStatusText` du binaire (division par 60) ; capture sur un héros placé à une tuile connue d'une carte connue →
    `InitialMapId` et `CameraTileX/Y/Z` égaux à ces valeurs ;
  - **pour chaque ligne du tableau, dans les deux formats**, une sauvegarde portant la valeur
    minimale, maximale et maximale + 1 (ou minimale − 1) ; hors domaine → refus nommant le champ ;
  - la valeur invalide placée **dans le dernier champ sérialisé**, et une faute détectée par le moteur
    (tableau de mauvaise longueur) : l'état d'`AlundraGameState` (tous les tableaux, les neuf stats,
    `PlayerControlFlags`) est identique octet pour octet à un instantané pris avant ;
  - aux bornes du domaine, une restitution suivie d'un tick du directeur de la jauge, du compositeur
    de la jauge et des compositeurs de l'inventaire → aucune exception ;
  - si E16.f est livrée : `$flag_0` et `$flag_2047` posés par Yarn font l'aller-retour,
    `$tmp_flag_0` et `$tmp_flag_2047` non (D-E16-25). Sinon, E16.f ajoute ce test ;
  - temps de jeu (D-E16-23) : sur une horloge de test, une seconde de jeu ajoute 60 unités, et le
    compteur s'arrête à `0x14996C4`.
- **Arrêt** : un champ dont E16.0 n'a pas établi la source, l'unité ou le domaine n'est pas écrit dans
  le format ; question au §3. **Si E16.0 ne trouve aucune source des dimensions de carte lisible
  avant de charger le monde**, la promesse « état inchangé » ne couvre pas la tuile : arrêt, question
  à l'auteur.
- **Dépendances** : E16.0, E16.b.

#### Plan détaillé d'E16.c (2026-09-28)

**Statut** : proposé. Revue de sécurité faite (tableau en fin de section, constats intégrés).
Première relecture de plan (`727d6d4`) : **REVISE**, un bloquant. La capture prenait la carte et la
tuile en entiers : aucun test ne prouvait qu'elles viennent de la carte courante et du héros. Corrigé
par `TryCaptureFromWorld` (C5, T3), avec le contrat d'E16.d. Remarques mineures corrigées aussi :
type d'`InitialMapId`, `TryValidate` partout, stats à plat. Relecture de clôture (`1851162`) :
**REVISE**, un bloquant (FIX). Sans valeur de départ, `LoadedDataVersion` aurait valu 0 sur tout objet
capturé, donc toute capture aurait été refusée ; valeur de départ fixée à 1 (C2), avec un test
« capture valide acceptée » (T4). C'est le deuxième REVISE : cette correction ouvre une seule
relecture de clôture ; un nouveau REVISE met la tranche en pause. Relecture de clôture (`393dec3`) :
**READY**. Reste l'approbation de l'auteur. Exécution par un `security-executor` (entrée non fiable), vérification
par un `verifier` frais.

**Réponses de l'auteur à la planification (2026-09-28)**

- **D-E16-31 — Pas d'aller-retour par le service dans les tests de la DLL.**
  - Obstacle : les constructeurs de `SaveGameService` sont internes au moteur
    (`SaveGameService.cs:40-55`), et celui de `SaveGameArchive` est `private protected`
    (`SaveGameArchive.cs:77`). La DLL ne peut donc ni créer un service sur un dossier temporaire, ni
    sérialiser sans service. Le seul service public, `GameSettings.SaveGames`, écrit sous le vrai
    `LocalApplicationData`, ce qui est interdit aux tests (O3 du plan moteur).
  - Les tests d'E16.c couvrent la capture, la validation et l'application. Les deux formats restent
    couverts par les tests du moteur (`CasaEngine.Tests/SaveGames/`). Aucun changement du moteur.
  - Conséquence acceptée : `AlundraSaveGame.Serialize` (noms, ordre et longueurs des champs) ne tourne
    pour la première fois qu'à la recette en jeu d'E16.d, en JSON et en binaire.
- **D-E16-32 — Les dimensions de la carte sont lues dans `tilemap/<nom>.tileMap`** (`map_size`), au
  moment de la validation, comme le prévoyait le tableau des domaines.

**Faits établis à la planification** (lus dans le code, le corpus ou `ALUN_CD.EXE` ; scripts dans
`scratchpad/e16c/`)

| Réf | Fait | Source |
|---|---|---|
| F1 | La table des cartes de la DLL part de l'identité sur 500 entrées, mais `world-index.json` n'a que les clés 0 à 482. Les entrées 483 à 499 d'une nouvelle partie ne sont donc pas des clés : le domaine « chaque valeur est une clé » refuserait toute sauvegarde. Corrigé en C3. | `AlundraGameState.cs:231-242` ; `Maps/world-index.json` de l'export |
| F2 | Les 66 `0x38` atteignables du corpus écrivent 10 indices (de 5 à 439) et 51 valeurs, toutes clés de `world-index.json`. | parcours d'E16.0, `op38.py` |
| F3 | Chacune des 483 cartes a son `tilemap/<nom>.tileMap`, au chemin déduit de `world-index.json` : dossier du `.world`, sous-dossier `tilemap`, même nom. `map_size` y vaut 52 × 60 partout. | `sizes.py` ; même dérivation qu'`EventProgramDocument.cs:107-117` |
| F4 | La DLL lit la carte courante dans le suffixe « -{id} » du nom du monde. C'est aussi l'id qu'un warp charge, par `AlundraWorldIndexTable.Resolve`. | `AlundraWorldProxy.cs:1072`, `:1361` ; `AlundraWarpDirector.cs:304` |
| F5 | La tuile du héros est dans `TileX`, `TileY` et `TileZ` de son proxy, avec `TileZ = PosZ >> 20`. | `AlundraWorldProxy.cs:1575-1577` |
| F6 | `UpdateMenuStatusText` (`0x80030FC8`) garde en dernier le modèle de `0x80022C38`, `"  HP 00       TIME 00:00:00   "` (30 caractères). Il écrit les PV en 5-6, sans borne ; les heures, `t / 216000 % 100`, en 19-20 ; les minutes, `t / 3600 − 60 × (t / 216000)`, en 22-23 ; les secondes, `t / 60 − 60 × (t / 3600)`, en 25-26. | désassemblage `0x80030FC8`–`0x8003132C` |
| F7 | Les PV du résumé sont `g_entitySlots[0].HpMax` (`0x80127D48`), pas `g_playerStats.HpMax`. Le héros les reçoit des stats à chaque réinitialisation (`ResetEntityState`, `0x800319F8`–`0x80031A04`), et les stats les reprennent du héros à la fin de chaque `MovePlayer` (`0x80032920`–`0x80032930`, `PlayerManager.cs:947-950`). La DLL ne porte pas cette recopie : ses PV n'existent que dans `PlayerStats`. Le résumé du port lit donc `PlayerStats.HpMax`. | binaire ; `AlundraPlayerManager.cs:393` |
| F8 | Le chapitre vient de `GetFirstEnabledFlagIndex` (`0x800813B0`). La fonction parcourt des enregistrements de 0x22 octets (texte, puis id), dont le premier id est en `0x8002962E`. Un id nul compte comme posé, un id ≥ `0x8000` arrête le parcours. Elle rend de 0 à 41 et recopie le texte de l'enregistrement, qui commence par l'indice sur quatre chiffres ASCII (`"0000"`, `"0001"`…). Les 41 ids lus dans le binaire sont ceux de `ChapterFlags.cs`. | désassemblage `0x800813B0`–`0x8008149C` ; `ChapterFlags.cs:24-32` de l'analyseur |
| F9 | La DLL n'a ni temps de jeu ni chapitre. Son horloge logique tourne à 50 Hz : les 60 unités par seconde réelle de D-E16-23 se comptent sur le temps écoulé, pas sur les ticks. | `rg` ; `AlundraLogicClock.cs:9` ; `AlundraWorldProxy.cs:526` |
| F10 | `WeaponId` vaut 0 à la construction, puis 1 dès l'entrée en nouvelle partie, avant tout tick. Le domaine « −1 ou 1..6 » ne refuse donc aucune capture faite en jeu. | `AlundraPlayerStats.cs:58` ; `AlundraWorldProxy.cs:1557-1561` ; `AlundraPlayerManager.cs:816` |
| F11 | Sans `items-properties.json`, `ItemsProperties` vaut 0 partout (mode dégradé) : tout compteur d'objet non nul serait alors refusé. | `AlundraItemTables.cs:132-134` |

**Choix du plan (à approuver)**

- **C1 — Noms** (DLL, `Alundra/Scripts/`) :
  - `AlundraSaveGame` : l'objet, qui implémente `ISaveGameData` ;
  - `AlundraSaveGameRules` : ce que la validation consulte (table des mondes, dossier du projet,
    présence au catalogue, table des objets) ; le catalogue est un prédicat injecté, que la
    production branchera sur `AssetCatalog.GetByFileName` en E16.d ;
  - `AlundraMapSizeReader` : lecture de `map_size` ;
  - `AlundraChapterFlags` : port de `GetFirstEnabledFlagIndex` ;
  - dans `AlundraGameState` : `GameTime` (`uint`), `DeathRetryCount` (`byte`, le `SaveSlotIndex` de
    l'original, D-E16-22) et `AdvanceGameTime(float elapsedSeconds)`.
- **C2 — Contenu et ordre**. Version de données 1 ; l'ordre compte pour le binaire, qui est
  positionnel :
  1. `gameTime` ;
  2. `initialMapId` ;
  3. `cameraTileX`, `cameraTileY`, `cameraTileZ` ;
  4. `gameFlags` (64 mots) ;
  5. `mapIdToInternalMapIndexTable` (500) ;
  6. un objet `playerStats` : `hp`, `hpMax`, `mp`, `mpMax`, `money`, `weaponId`, `itemId`,
     `falconTemp`, `falcon` ;
  7. `numberOfItems` (256) ;
  8. `deathRetryCount`.

  C'est l'ordre de `g_saveData` (§2, Q3), sans `SlotData`, `LastMapId`, `CurrentFlagName`,
  `GameStateDescription`, `Field_757` ni `Offset`. Les types sont ceux de la DLL.

  - `Serialize` suit la disposition de la version 1 quelle que soit `archive.DataVersion`. Il ne se
    branche sur aucune valeur lue et ne lève jamais de lui-même, car une exception du jeu sort de
    `TryLoad` (`SaveGameService.cs:165-176`). Au chargement, il retient `archive.DataVersion` dans une
    propriété non sérialisée, `LoadedDataVersion`, que `TryValidate` contrôle (C4). Raison : le moteur
    charge toute version ≤ `LatestDataVersion`, 0 compris (SC2).
  - `LoadedDataVersion` vaut `LatestDataVersion` (1) sur un objet construit ou capturé. Seul
    `Serialize`, quand `archive.IsLoading` est vrai, le remplace par `archive.DataVersion`. Sans cette
    valeur de départ, tout objet capturé porterait 0 et serait refusé, ce qui bloquerait toute
    sauvegarde (relecture de clôture).
  - Les trois tableaux sont des champs `readonly`, initialisés à leur longueur. Ni `TryValidate` ni
    `ApplyTo` ne peuvent donc trouver un tableau nul ou de mauvaise longueur (SC4).
- **C3 — Domaines arrêtés ou corrigés** (le reste du tableau d'E16.c ne change pas) :
  - `MapIdToInternalMapIndexTable[i]` : une clé de `world-index.json`, **ou `i` lui-même** (F1). Une
    entrée identité au-delà de 482 ne mène nulle part, comme en nouvelle partie : le portail qui la
    lirait tombe dans la garde d'abandon (`AlundraWarpDirector.cs:540-553`) ;
  - `InitialMapId` : clé de `world-index.json`, monde présent au catalogue, `.tileMap` lisible, avec
    une largeur `w` et une hauteur `h` d'au moins 1 ;
  - `CameraTileX` : 0 ≤ X < `w`, et X ≤ 1364 pour que `(X × 24 + 12) << 16` tienne dans un `int` ;
  - `CameraTileY` : 0 ≤ Y < `h`, et Y ≤ 2047 pour `(Y × 16 + 8) << 16` ;
  - `CameraTileZ` : de 0 à 256, soit la hauteur d'une tuile (un octet), plus 1 en pente (§2, Q6), bien
    en dessous de 2048, où `Z << 20` déborde. Au-dessus du sol, le héros apparaît en l'air : risque
    accepté, comme une tuile dans un mur (§4).
- **C4 — Validation** : `bool TryValidate(AlundraSaveGameRules rules, out string error)`, sans état et
  sans exception, quel que soit le contenu de l'objet :
  - elle contrôle chaque champ ; au premier champ hors domaine, elle rend `false` avec un message qui
    nomme le champ, sa valeur et le domaine (par exemple `playerStats.money = -1, outside 0..9999`) ;
  - elle refuse d'abord un `LoadedDataVersion` autre que 1 (SC2) ;
  - elle ne lit que le `.tileMap` de `InitialMapId`, et seulement après avoir trouvé la clé dans
    `world-index.json` : le chemin vient toujours de ce fichier, jamais de la sauvegarde ;
  - toute exception du lecteur ou du prédicat du catalogue devient un refus (entrées-sorties, accès,
    JSON), et les entiers de `map_size` se lisent par des lectures « Try », sans conversion qui lève
    (SC6).
- **C5 — Capture**, en deux méthodes statiques d'`AlundraSaveGame` :
  - `Capture(AlundraGameState state, int currentMapId, int tileX, int tileY, int tileZ)` recopie les
    mots 0 à 63 de `GameFlags`, la table, les compteurs, les stats, `GameTime` et `DeathRetryCount`,
    dans des tableaux propres à l'objet, puis pose `InitialMapId` et `CameraTileX/Y/Z` avec les
    valeurs données ;
  - `bool TryCaptureFromWorld(AlundraGameState state, string worldName, AlundraEntityScriptProxy?
    player, out AlundraSaveGame? save)` lit les sources d'`UpdateSavedData`, puis appelle `Capture` :
    - la carte courante vient du suffixe « -{id} » du nom du monde, par
      `BackdropLoader.TryParseMapIndex` (F4, `BackdropLoader.cs:110`) ;
    - la tuile vient de `TileX`, `TileY` et `TileZ` du proxy du héros (F5).

    Elle rend `false` sans lever quand le nom n'a pas d'id ou quand le héros manque.
  - Aucune des deux ne valide. Contrat pour E16.d :
    - la touche de sauvegarde capture par `TryCaptureFromWorld`, avec le nom du monde courant et
      `AlundraWorldProxy.PlayerEntity` (`AlundraWorldProxy.cs:208`) ;
    - la sauvegarde n'est écrite qu'après une validation réussie de l'objet capturé, pour ne jamais
      écrire ce qu'un chargement refuserait.
  - Types : `InitialMapId` est un `int`, comme l'id que rend `TryParseMapIndex` et comme les clés de
    `world-index.json`. Les neuf stats sont des champs à plat de l'objet ; l'archive les range sous
    l'objet `playerStats` (C2).
- **C6 — Application** : `void ApplyTo(AlundraGameState state)`, sur un objet déjà validé :
  - elle efface les 1024 mots de `GameFlags`, puis copie les 64 ;
  - elle copie la table (500), les compteurs (256), les neuf stats, `DeathRetryCount` et `GameTime`,
    et remet à zéro le reste fractionnaire du temps ;
  - elle ne touche ni `TemporaryFlags`, ni `TextCategoryIndex`, ni `GameVariables`, ni
    `PlayerControlFlags`, ni le reste de la session : c'est l'étape 4 d'E16.d ;
  - les longueurs des tableaux sont fixées à la construction, donc elle ne lève pas.
- **C7 — Métadonnées** : `IReadOnlyDictionary<string, string> BuildMetadata()` rend deux entrées,
  calculées depuis l'objet lui-même :
  - `chapter` : l'indice de F8 sur quatre chiffres (`"0000"` à `"0041"`), comme la charge utile de
    `CurrentFlagName` ;
  - `summary` : le texte de F6, calculé sur `hpMax` et `gameTime`.

  Le nom du chapitre et l'affichage relèvent d'E16.e.
- **C8 — Temps de jeu** : `AdvanceGameTime(elapsedSeconds)` est appelé une fois par image, en tête
  d'`AlundraWorldProxy.Update` (`:1824`). Il ignore une durée non finie, nulle ou négative. Sinon, il
  ajoute `elapsedSeconds × 60` à un reste fractionnaire (`double`), passe les unités entières au
  compteur et plafonne à `0x14996C4`.
  - Le compteur avance pendant les dialogues, les menus et les transitions, comme l'original, qui
    compte chaque image affichée (§2, Q3). Il n'avance pas pendant le chargement d'un monde, faute de
    proxy.
  - Le reste fractionnaire n'est pas sauvegardé.
  - `InstallForMapEntry` garde `GameTime` et `DeathRetryCount`, qui sont dans `g_saveData` ;
    `ResetForTests` les remet à zéro, avec le reste fractionnaire.

**Tâches** (branche `chantier/e16-proposition` ; un commit par tâche, avec la mise à jour de ce
plan ; `Alundra.Tests` sans échec à chaque commit)

- ✅ **T1 — Temps de jeu et compteur de reprises** (fait le 2026-09-28 : `GameTime`, `DeathRetryCount`, `AdvanceGameTime` et son reste fractionnaire dans `AlundraGameState`, appel en tête d'`AlundraWorldProxy.Update` ; 23 tests dans `AlundraGameTimeTests`, dont une durée qui passe juste 2^32 unités, ajoutée après une mutation « conversion avant le plafond » que les durées `1e12` et `float.MaxValue` ne voyaient pas (conversion saturante de .NET 9) ; `Alundra.Tests` 1452/1452, oracle de l'intro inchangé) (C1, C8). Tests :
  - une seconde ajoute 60, en 60 pas de 1/60 s comme en un pas de 1 s ;
  - 120 pas de 1/120 s ajoutent 60 ;
  - le compteur s'arrête à `0x14996C4` ;
  - une durée NaN, infinie, nulle ou négative n'ajoute rien ;
  - une durée finie énorme (`1e12`, `float.MaxValue`) mène au plafond sans repli du compteur : la
    borne s'applique en `double` avant la conversion (SC11) ;
  - `InstallForMapEntry` garde les deux champs ; `ResetForTests` les remet à zéro ;
  - un `Update` du proxy fait avancer le compteur ;
  - oracle de l'intro inchangé.
- ✅ **T2 — Chapitre et résumé** (fait le 2026-09-28 : `AlundraChapterFlags` (41 ids, parcours, `FormatChapter` sur quatre chiffres) et `AlundraSaveGame.BuildSummary`, statique, posé dans le fichier de l'objet que T3 complète ; des PV max hors de 0..99, refusés par la validation, donnent des caractères non numériques comme l'original, sans exception ni surrogate ; 39 tests dans `AlundraChapterFlagsTests`, dont les résumés attendus écrits en clair ; `Alundra.Tests` 1491/1491) (C1, C7, F6 à F8). `AlundraChapterFlags` porte les 41 ids et le
  parcours. Tests :
  - aucun drapeau posé → 0 ; le premier seul → 1 ; tous → 41 ; un trou au milieu → l'indice du trou ;
  - un id de chapitre posé dans `TemporaryFlags` n'est pas vu ;
  - le texte pour les temps 0, 59, 60, 3599, 3600, 215 999, 216 000 et `0x14996C4` (→ `99:59:59`), et
    pour les PV 0, 10 et 50, comparé à un calcul écrit dans le test selon les formules de F6.
- ✅ **T3 — Objet, capture, application, métadonnées** (fait le 2026-09-28 : `AlundraSaveGame` implémente `ISaveGameData` dans l'ordre de C2, avec `Capture`, `TryCaptureFromWorld`, `ApplyTo` et `BuildMetadata` ; les longueurs et la version 1 sont des constantes internes, pour que les champs publics d'instance soient exactement ceux de la liste ; `LoadedDataVersion` a un setter interne, pour les tests de T4 ; un nom de monde nul rend aussi `false` ; 26 tests dans `AlundraSaveGameTests` ; `Alundra.Tests` 1517/1517) (C2, C5, C6, C7). Tests :
  - capture puis `ApplyTo` sur un état neuf → état identique champ par champ (mots 0 à 63 de
    `GameFlags`, table, compteurs, neuf stats, `GameTime`, `DeathRetryCount`) ;
  - un mot de `GameFlags` d'indice ≥ 64 posé dans l'état cible est effacé par `ApplyTo` ;
  - `TemporaryFlags`, `TextCategoryIndex`, `GameVariables` et `PlayerControlFlags` de l'état cible ne
    changent pas ;
  - l'objet n'a aucun membre pour eux. Ses champs publics sont exactement `GameTime`, `InitialMapId`,
    `CameraTileX`, `CameraTileY`, `CameraTileZ`, `GameFlags`, `MapIdToInternalMapIndexTable`, `Hp`,
    `HpMax`, `Mp`, `MpMax`, `Money`, `WeaponId`, `ItemId`, `FalconTemp`, `Falcon`, `NumberOfItems`
    et `DeathRetryCount` (test par réflexion). `LoadedDataVersion` est une propriété ;
  - un objet neuf a ses trois tableaux non nuls, de longueurs 64, 500 et 256, déclarés `readonly`
    (réflexion : champs en lecture seule), et `LatestDataVersion` vaut 1 (SC3, SC4) ;
  - la capture ne partage aucun tableau avec l'état : modifier l'état après la capture ne change pas
    l'objet ;
  - `TryCaptureFromWorld` sur un vrai `AlundraEntityScriptProxy` placé à une tuile connue
    (`TileX`, `TileY`, `TileZ` posés), avec le nom de monde réel de la carte 389,
    `Ship Klark (beginning)-389` → `InitialMapId` = 389 et `CameraTileX/Y/Z` égaux à la tuile ; un
    nom sans id, ou un héros nul → `false`, sans exception ;
  - métadonnées : `chapter` et `summary` égaux au calcul de T2.
- ✅ **T4 — Validation** (fait le 2026-09-28 : `AlundraSaveGame.TryValidate`, `AlundraSaveGameRules` (dossier du projet, `world-index.json` lu depuis ce dossier, prédicat du catalogue et table des objets injectés) et `AlundraMapSizeReader` ; ordre des contrôles : version, `gameTime`, `initialMapId` (clé, catalogue, `.tileMap`, taille ≥ 1), tuile, table des cartes, stats (max avant la valeur qu'il borne), `numberOfItems` ; 129 tests dans `AlundraSaveGameValidationTests` (dossier temporaire) et `AlundraSaveGameProductionTests` (vrai export : clés 0 à 482, carte 389 en 52 × 60, capture de nouvelle partie acceptée, bornes puis jauge et inventaires dessinés) ; mutations de production vérifiées : version `>` au lieu de `!=`, plafond 1364 retiré, règle « son propre indice » retirée, chacune refusée par un test ; un doublon `-0f` retiré des tests de T1 (22 tests) ; `Alundra.Tests` 1645/1645, oracle de l'intro inchangé) (C3, C4, D-E16-32). `AlundraMapSizeReader` et `AlundraSaveGameRules`
  travaillent sur un dossier de projet donné ; les tests utilisent un dossier temporaire avec un
  `world-index.json` et des `.tileMap` de test, et un catalogue injecté. Tests :
  - pour chaque ligne du tableau d'E16.c, corrigé par C3 : minimum, maximum, puis maximum + 1 (ou
    minimum − 1) → refus nommant le champ ; pour `Hp`, `Mp` et les compteurs d'objets, la borne vient
    de la règle croisée ou de la table ;
  - `MapIdToInternalMapIndexTable` : la table d'une nouvelle partie est acceptée (F1) ; une valeur qui
    n'est ni une clé ni son propre indice est refusée ;
  - `InitialMapId` absente de l'index, monde absent du catalogue, `.tileMap` absent, JSON invalide,
    `map_size` absent ou non entier, `w` ou `h` ≤ 0 → refus, sans exception ;
  - un dossier à la place du `.tileMap`, un `map_size` flottant, texte ou trop grand pour un `int`, et
    un prédicat de catalogue qui lève → refus, sans exception (SC6) ;
  - `LoadedDataVersion` à 0 ou à 2, posé directement sur l'objet → refus nommant la version (SC2) ;
  - **capture valide acceptée** : `TryCaptureFromWorld` sur un état de nouvelle partie (après
    `InitializeNewGameInventory`), héros à la tuile (33, 59, 0), nom de monde
    `Ship Klark (beginning)-389` → `TryValidate` rend vrai, et l'objet capturé porte
    `LoadedDataVersion` = 1. Les règles s'appuient sur le vrai export, dont les 483 clés de
    `world-index.json` couvrent la table identité de la nouvelle partie (F1). Elles utilisent les mêmes
    tables d'objets que l'inventaire de nouvelle partie, et un prédicat de catalogue qui répond vrai.
    Sans export, le test échoue en le nommant ;
  - la vraie carte 389 de l'export se lit en 52 × 60 ; sans export, le test échoue en le nommant
    (convention d'`AlundraCellStoreProductionTests.cs:21-45`) ;
  - `ItemsProperties` en mode dégradé → tout compteur non nul est refusé ;
  - une valeur invalide dans le dernier champ contrôlé (`numberOfItems`) → refus, et l'état
    d'`AlundraGameState` (tous les tableaux, les neuf stats, `PlayerControlFlags`, `GameTime`,
    `DeathRetryCount`) est identique à un instantané pris avant ;
  - aux bornes (tous les champs au minimum, puis au maximum) : `ApplyTo`, puis la jauge et les
    inventaires, **avec des valeurs réellement dessinées** (SC5). Un appel des compositeurs avec des
    arguments choisis à la main ne teste rien, puisque ce sont les présentateurs qui calculent ces
    arguments depuis l'état. Le test doit donc :
    - ouvrir la jauge par sa demande d'affichage et faire tourner `AlundraHudDirector.Tick` jusqu'à la
      fin des roulements (l'argent roule de 10 par tick, soit environ 1000 ticks pour 9999) ;
    - passer par les calculs des présentateurs (`AlundraHudPresenter`, `AlundraInventoryPresenter`,
      `AlundraSubInventoryPresenter`), état dessiné à vrai, résolution des icônes d'équipement
      comprise ;
    - n'obtenir aucune exception.

    Le départ d'une jauge en plein rattrapage des PM max relève d'E16.d, qui remet la jauge à zéro
    (SC1).
- ✅ **T5 — Documentation et ADR** (fait le 2026-09-28 en session principale : ADR-0012
  `docs/decisions/0012-alundra-save-game-content-and-validation.md`, page
  `docs/formats/save-game.md`, index des deux dossiers, tableau de suivi du plan maître ; le §3 de ce
  plan n'avait rien à changer ; la doc d'`AlundraPlayerStats`, qui disait les faucons « non portés »,
  est remise à jour) :
  - ADR-0012 de ce dépôt : contenu de la sauvegarde d'Alundra (champs, 64 mots de drapeaux, champs
    exclus, compteur de reprises, unité du temps de jeu, domaines, métadonnées ; D-E16-22, D-E16-23,
    D-E16-31, D-E16-32) ;
  - page `docs/formats/save-game.md` : champs, noms, domaines, métadonnées, limites ;
  - mise à jour du §3 de ce plan et du tableau de suivi du plan maître.
- ✅ **T6 — Vérification** (faite le 2026-09-28 sur `24bde7a`, deux contrôles neufs et indépendants) :
  - un `verifier` a rejoué le build (0 erreur) et `Alundra.Tests` (1645/1645, dont les 218 tests
    d'E16.c et l'oracle de l'intro), puis il a relu chaque ligne du tableau des domaines,
    `ApplyTo`, `TryCaptureFromWorld` et `Serialize` contre la liste ci-dessous : **CONFIRMED** ;
  - un contradicteur a cherché, en lecture seule, une sauvegarde hostile qui fasse lever la
    validation, l'application ou un lecteur de l'état appliqué, ou qui passe hors du domaine :
    **CONFIRMED**, aucun P0 à P2.

  Deux P4 sont reportés :
  - les quatre méthodes lèvent `ArgumentNullException` sur un état ou des règles nuls. Aucun contenu
    de sauvegarde n'y mène : c'est un contrat de l'appelant, à respecter par E16.d ;
  - `BuildSummary` écrit des caractères non numériques pour des PV max hors de 0..99, comme
    l'original. Seul un objet validé est écrit (contrat de C5), et E16.e traite les métadonnées
    comme du texte non fiable.

  Énoncé d'origine de la tâche : `verifier` frais sur l'acceptation ci-dessous, en particulier l'entrée
  non fiable. Faute d'aller-retour (D-E16-31), il relit `Serialize` contre cette liste (SC3) :
  - appels dans l'ordre de C2 ;
  - noms uniques dans chaque objet ;
  - `BeginObject` et `EndObject` équilibrés ;
  - aucun branchement sur `DataVersion` ni sur une valeur lue ;
  - `LoadedDataVersion` vaut 1 à la construction, et `Serialize` ne la remplace qu'au chargement,
    par `archive.DataVersion`.

**Acceptation d'E16.c**, réduite par D-E16-31 :
- les tests de T1 à T4 passent ; `Alundra.Tests` est sans échec, l'oracle de l'intro inchangé ; le
  build est à 0 erreur ;
- la DLL ne lit ni n'écrit aucun fichier de sauvegarde : E16.c n'appelle pas le service ;
- le verifier rend **CONFIRMED**.

Retirés par D-E16-31, et couverts par les tests du moteur : l'aller-retour JSON et binaire, et la faute
détectée par le moteur (tableau de mauvaise longueur). La capture depuis le héros et la carte
courante reste dans l'acceptation : `TryCaptureFromWorld` la prouve sur un vrai proxy (T3). Reporté : E16.f n'étant pas livrée, le test
« `$flag_n` fait l'aller-retour » revient à E16.f, sur la capture et l'application.

**Arrêts** :
- un champ dont la source, l'unité ou le domaine n'est pas établi ;
- une exception qui sort de `TryValidate`, de `Capture` ou d'`ApplyTo` ;
- un test qui aurait besoin du service (D-E16-31) ;
- un test existant qui change pour une autre raison, ou l'oracle de l'intro qui bouge.

**Conséquence pour E16.d** : ses tests « chaque résultat du service autre que « chargé » » butent sur
le même obstacle que D-E16-31. À trancher à sa planification, par exemple avec une interface de la
DLL devant le service, simulée dans les tests.

**Revue de sécurité d'E16.c (2026-09-28)** : `security-reviewer` frais, en lecture seule, sur
`503950b`. Aucun P0 ni P1. Pour chaque lecteur de la DLL, les domaines corrigés par C3 tiennent :
`ItemId`, `WeaponId`, compteurs d'objets, chiffres de la jauge, faucons, parcours des chapitres (id
max `0x6A2`, mot 53) et arithmétique de C3.

| Réf | Prio | Constat | Décision | Où |
|---|---|---|---|---|
| SC1 | P2 | Un `_mpMaxSubStep` resté non nul (la jauge refermée pendant le rattrapage des PM max, puis `ArmAppearance`, qui ne remet que les max) garde l'aperçu actif ; avec 4 PM, `ComposeMagic` lit `magicPipFrame[4]` et lève. Un chargement qui ne remet pas les sous-étapes y mène (`AlundraHudDirector.cs:158`, `:426-445`, `:584-598` ; `AlundraHudComposer.cs:304-310`). Contrôlé à la lecture par la session principale. | FIX | E16.d étape 4 (remise exacte de la jauge) et ses tests. Le même chemin sans chargement (fermeture scriptée pendant le rattrapage) est un défaut préexistant de la jauge : signalé à part, hors E16 |
| SC2 | P3 | Le moteur charge toute version ≤ `LatestDataVersion`, 0 compris ; une exception de `Serialize` sort de `TryLoad` | FIX | C2, C4, T4 |
| SC3 | P3 | Sans aller-retour, des invariants de `Serialize` restent testables ; le §5.3 exige une couverture par format que D-E16-31 retire | FIX | T3, T6, §5.3 ; E16.d : un emplacement binaire hostile dans la recette |
| SC4 | P3 | « Ne lève pas » suppose des tableaux non réassignables | FIX | C2 (`readonly`), T3 |
| SC5 | P3 | Le test « bornes, puis jauge et inventaires » peut passer sans rien dessiner | FIX | T4 |
| SC6 | P3 | Exceptions d'entrée-sortie, de JSON et du catalogue à absorber ; aucune traversée de chemin possible (la sauvegarde ne donne qu'une clé entière) | FIX | C4, T4 |
| SC7 | P3 | Après un chargement, `NewGameInventoryInitialized` doit valoir vrai, sinon une entrée de carte sans arrivée de warp relancerait l'inventaire de nouvelle partie et effacerait les objets chargés (`AlundraWorldProxy.cs:1550-1561`) | FIX | E16.d étape 4 et ses tests |
| SC8 | P3 | `Hp = 0` est dans le domaine ; sans effet tant que la mort n'est pas portée, mais « Réessayer » rechargerait une sauvegarde à 0 PV en boucle | DEFER | E18 : trancher `Hp ≥ 1` d'après le binaire, ou casser la boucle |
| SC9 | P3 | E16.e doit borner `chapter` avant de s'en servir comme indice | FIX | prérequis d'E16.e |
| SC10 | P4 | Une combinaison de drapeaux pourrait figer l'interpréteur (hypothèse, aucun script trouvé) | FIX | §4 : risque accepté, écrit |
| SC11 | P4 | Une durée finie énorme pourrait faire reboucler le compteur de temps avant le plafond | FIX | T1 |
| SC12 | P4 | Les valeurs de la table des cartes ne sont pas contrôlées contre le catalogue | REJECT | même exposition que les opcodes `0x38` et `0x53` ; les 483 mondes existent (F3) ; risque résiduel au §4 |

### E16.d — Chargement et recette 🧪 (DLL ; plan détaillé plus bas, READY le 2026-09-28, exécuté en mode AUTO ; T1 à T7 faites et vérifiées CONFIRMED le 2026-09-29, `f2cc8be` à `00746c7` ; reste la recette en jeu T8 de l'auteur)

- **But** : reprendre une partie sauvegardée.
- **Contenu** : port de la branche `SlotData == 1` d'`InitializeGameState`, dans cet ordre, dont
  seule la dernière étape modifie l'état vivant :
  1. **préconditions** d'un chargement en cours de partie. L'original ne charge qu'à deux moments :
     au démarrage d'un processus neuf, par `LOADER.EXE`, et après la mort, par « Réessayer » (§0.2) ;
     jamais librement en pleine partie. Préconditions : aucun dialogue, aucun inventaire ouvert,
     aucune transition en cours, `PlayerControlFlags == 0`. Sinon, refus avec un message ;
  2. chargement par le service, puis `TryValidate` d'E16.c ;
  3. **contrôle du départ** : monde de `InitialMapId` résolu et présent au catalogue, aucune transition
     en cours, warp non désactivé (`AlundraWarpDirector.cs:365-368`) et garde d'abandon non
     déclenchable (`:540-548`) ; sinon refus ;
  4. **application en une étape qui ne lève pas** (copies de tableaux de longueurs déjà contrôlées),
     de préférence à l'entrée de la carte d'arrivée. Elle enchaîne quatre choses :
     - la remise des singletons de session à un état équivalent à une nouvelle partie, contrepartie
       de production de `ResetForTests` (`AlundraGameState.cs:366`, à `24bde7a`). Elle couvre
       `PlayerControlFlags`, le verrou d'interaction et ses huit nombres, et les états des directeurs
       de dialogue et d'inventaire. Elle couvre aussi **`TextCategoryIndex` à 0 et `GameVariables` à
       zéro** (D-E16-19). La liste exacte est arrêtée et testée ici, avec deux points fixés par la
       revue de sécurité d'E16.c :
       - **la jauge est remise entièrement** : valeurs affichées de PV, PV max, PM, PM max et argent,
         les quatre compteurs de sous-étapes, `CoinIconFrame`, `IsMoneyRolling` et les images des
         cases de magie, soit le contenu d'`AlundraHudDirector.ResetForTests` hors `_gameState` et la
         phase (SC1) ;
       - **`NewGameInventoryInitialized` vaut vrai après un chargement**, et non faux comme en
         nouvelle partie : sinon, une entrée de carte sans arrivée de warp relancerait l'inventaire
         de nouvelle partie et effacerait les objets chargés (SC7) ;
     - la copie de l'objet ;
     - le départ sur `InitialMapId` à la tuile `CameraTileX/Y/Z`, par le chemin d'arrivée des warps ;
     - la reprise du temps de jeu.

  **Touches de recette** (D-E16-11) gardées par un interrupteur de débogage dont le mécanisme est
  tranché avant cette tranche (O-E16-6), lu une fois et journalisé quand il est actif, avec un point
  d'injection pour les tests. **D-E16-33** (réponse de l'auteur du 2026-09-28 à O-E16-6) : les touches
  sont actives quand `Alundra.dll` est compilée en Debug, inactives en Release. Faits qui ont écarté
  les autres pistes : le lanceur est démarré depuis l'IDE, sa modification locale par l'auteur ignore
  ses arguments, et une variable d'environnement n'a jamais atteint le jeu (D-E13-12). **Acceptation propre à l'interrupteur** : activé par le moyen retenu, il
  est vu par le jeu lancé depuis le lanceur, et le journal du jeu en porte la trace. La capture est refusée hors d'un état que
  l'original sauvegarde : aucune transition, aucun dialogue, aucun menu, `PlayerControlFlags == 0`,
  héros au sol. Chaque résultat du service autre que « chargé » et chaque refus laissent la partie en
  cours intacte, avec un message ; aucun ne fait planter le jeu.
- **Tests** (dans les deux formats) : chaque résultat du service autre que « chargé » ; chaque
  précondition non tenue (dialogue, inventaire, transition, verrou de script) → refus, état
  identique à l'instantané ; warp désactivé, transition en cours, monde introuvable → refus, état
  identique ; un fichier au nom invalide posé dans le dossier de sauvegarde → aucune exception ;
  interrupteur inactif → F5, F6 et F9 sans effet ; chargement réussi depuis une session où
  `TextCategoryIndex` et `GameVariables` ne sont pas nuls → les deux valent 0 après (D-E16-19) ;
  chargement depuis une jauge en plein rattrapage des PM max (affichés 3, vrais 4, `_mpMaxSubStep`
  non nul) d'une sauvegarde à 4 PM sur 4, puis jauge rouverte et roulements finis → aucune exception
  (SC1) ; après un chargement, une entrée de carte sans arrivée de warp garde les objets chargés
  (SC7) ; la touche de sauvegarde, héros placé à une tuile connue d'une carte connue, capture cette
  carte et cette tuile par `TryCaptureFromWorld` (contrat de C5 d'E16.c).
- **Acceptation en jeu** (lancée hors de l'app Claude, O3 du plan moteur) : nouvelle partie sur la
  389, intro jusqu'au bout, passage sur la 390, sauvegarde ; quitter ; relancer, charger → sur la 390
  à la même tuile, stats et objets identiques ; retour sur la 389 **sans** que l'intro rejoue. La même
  recette réussit avec un emplacement JSON et avec un emplacement binaire. Une sauvegarde JSON éditée à
  la main (`Money: -1`, `MpMax: 9`, carte 9999) → message de refus, le jeu continue. De même pour un
  emplacement binaire hostile, édité hors du dépôt avec son CRC-32 recalculé (SC3).
- **Arrêt** : l'intro qui rejoue au retour sur la 389, ou un écart de stats ou d'objets après
  chargement → la tranche s'arrête, cause établie avant toute correction.
- **Dépendances** : E16.c.

#### Plan détaillé d'E16.d (2026-09-28)

**Statut** : proposé en mode AUTO (l'auteur, le 2026-09-28 au soir : « fait tout E16 de façon
autonome »). Revue de sécurité faite (tableau en fin de section, constats intégrés). Première
relecture de plan (`ad07ae4`) : **REVISE**, deux bloquants corrigés. Les mutations de T5 n'étaient
pas détectables : aucune lecture des valeurs chargées pendant l'installation, les premiers lecteurs
sont au premier tick (G5, K8, T5). « Portrait au repos » était ambigu, désormais `StateIdle` (K6,
K7, T3). Remarques mineures corrigées aussi : source de `mapId`, action sonore vide,
`ResetDisplayForLoad(stats)`. Relecture de clôture (`73504cf`) : **REVISE**, un bloquant. Les règles
de production vérifient le catalogue d'assets, vide dans les tests, donc tout chemin de succès de T3
à T5 aurait été refusé. Correction : `RulesFactoryForTests` (K1), avec les règles de chaque test
nommées (T3 à T5, SD15). Remarques corrigées aussi : `FrameCounter` gardé, sens de la dépendance à
l'abandon, tuile du test de bout en bout. C'est le deuxième REVISE : cette correction ouvre une
seule relecture de clôture ; un nouveau REVISE met la tranche en pause. Relecture de clôture
(`9a70844`) : **READY**. Ses remarques P3 sont passées comme précisions à l'exécuteur, sans changer
le plan :
- les tables d'objets des règles de production sont `AlundraItemTables.GetOrCreate(EngineEnvironment.ProjectPath)`,
  l'instance de session de `AlundraWorldProxy.ItemTables` ;
- `AlundraSlotEntry.IsReadable` vient de `SaveGameSlotInfo.IsReadable` (`Status == Loaded`) ;
- le test « départ avorté » force la garde d'abandon en rattachant le directeur des warps sans
  `GameManager` après l'armement.

Exécution par un `security-executor`, puis vérification par un `verifier` frais et un
contradicteur. La recette en jeu reste à l'auteur.

**Faits établis à la planification** (exploration en lecture seule, chaque rapport recontrôlé par
un second agent ; scripts et rapports dans `scratchpad/e16def/`)

| Réf | Fait | Source |
|---|---|---|
| G1 | `AlundraWarpDirector` a deux entrées publiques de départ, sans lien avec l'interpréteur : `BeginDeparture` (portail) et `BeginDepartureFromChangeMapOpcode(desiredMapIndex, posX, posY, posZ, effectId, sfxId, player, state)`. La seconde prend l'id de carte tel quel, sans la table des cartes, et le résout dans `world-index.json` : le même fichier que celui qui a validé `InitialMapId` (E16.c). | `AlundraWarpDirector.cs:280-326`, `:355-396`, `:374-382` ; `AlundraWorldIndexTable.cs:88` |
| G2 | Aucune des deux ne se protège d'un départ déjà armé : `BeginDepartureCore` écrase l'arrivée en attente. Toutes deux ne font rien si `IsWarpDisabled`. | `AlundraWarpDirector.cs:409-455`, `:290-293`, `:365-368` |
| G3 | Le chemin du monde est résolu dès l'armement ; le changement de monde a lieu plus tard, dans `Advance`, une fois le fondu stabilisé. La garde d'abandon ne se déclenche que sans chemin ou sans `GameManager` attaché. | `AlundraWarpDirector.cs:450`, `:514-554` |
| G4 | Un départ vers la carte courante recharge tout le monde : le moteur n'a pas de raccourci pour le même chemin. | `GameManager.cs:98-144`, `:154-157` |
| G5 | À l'arrivée, `InitializeWithWorld` exécute `GameState.InstallForMapEntry()` au tout début de son installation, puis installe les systèmes (cellules, audio, warp, fondu, dialogue, jauge, inventaires), adopte le héros (`AdoptPlayerPawn`, qui consomme l'arrivée en attente), bâtit les évènements de carte et les portails, et fait enfin apparaître les entités. Les installeurs ne font que rattacher l'état, et `AdoptPlayerPawn` ne lit `NewGameInventoryInitialized` que sans arrivée en attente : les premiers lecteurs des valeurs chargées sont les programmes des entités (dont leur programme de chargement, créneau A) et les évènements de carte, au premier tick du monde d'arrivée, après toute l'installation. Exemple sur la carte 389 : plusieurs programmes de chargement testent le drapeau 860 par `0x31` (`docs/intro-programs-389.txt`, décalages 143, 180, 234). | `AlundraWorldProxy.cs:642-739` |
| G6 | `AdoptPlayerPawn` ne distingue une nouvelle partie d'un warp que par l'absence d'arrivée en attente. Un chargement parti par le chemin des warps est vu comme un warp : l'inventaire de nouvelle partie ne se relance pas, et le héros est placé à la position de l'arrivée. | `AlundraWorldProxy.cs:1546-1561` |
| G7 | Préconditions disponibles : `AlundraDialogueDirector.Instance.IsOpen`, `AlundraInventoryDirector.Instance.IsActive`, `AlundraSubInventoryDirector.Instance.IsActive`, `AlundraWarpDirector.Instance.IsTransitionInProgress`, `AlundraGameState.PlayerControlFlags`, `IsWarpDisabled`, `AlundraEntityScriptProxy.IsOnGround` (tenu depuis le contrôleur à chaque image). La transition n'est pas impliquée par `PlayerControlFlags` ; un dialogue ouvert avec un mode de contrôle autre que 0 ou 1 laisse `PlayerControlFlags` intact (`ApplyControlMode` n'a pas de cas par défaut). Chaque précondition se teste donc à part. | `AlundraDialogueDirector.cs:134`, `:258-274` ; `AlundraInventoryDirector.cs:91` ; `AlundraSubInventoryDirector.cs:55` ; `AlundraWarpDirector.cs:85` ; `AlundraEntityScriptProxy.cs:191`, `:937` |
| G8 | Déjà remis par l'arrivée : `AlundraGameState.InstallForMapEntry` (`TemporaryFlags`, l'entité du verrou d'interaction, `IsWarpDisabled`) et `AlundraDialogueDirector.InstallForMapEntry` (tout l'état du dialogue). Pas remis : les directeurs d'inventaire, le post-traitement et le portrait (aucune remise d'entrée de carte), ni la jauge (`InstallForMapEntry` vide, voulu). `ResetForTests` de la jauge pose les valeurs de nouvelle partie (10/10/0/0/0) et détache l'état : il ne convient pas à un chargement. | `AlundraGameState.cs` (`InstallForMapEntry`) ; `AlundraDialogueDirector.cs:183-201` ; `AlundraInventoryDirector.cs:132-152` ; `AlundraSubInventoryDirector.cs:81-104` ; `AlundraInventoryPostProcess.cs:46-49` ; `AlundraInventoryPortrait.cs:189-203` ; `AlundraHudDirector.cs:200-217`, `:262-284` |
| G9 | La DLL lisait la touche F1 par `world.Game.InputComponent.KeyboardManager.IsKeyPressed`, une fois par image rendue (hors de la boucle des ticks), avec son propre front montant et un fournisseur pour les tests (`DebugHudToggleKeyHeldProviderForTests`). `KeyboardManager` avance une fois par image rendue : lue dans la boucle des ticks, une touche compterait une fois par tick. | `git show cbda4f8:Alundra/Scripts/AlundraWorldProxy.cs` ; `KeyboardManager.cs:12-30`, `:109-113` |
| G10 | Aucun `#if DEBUG` dans la DLL ; `Alundra.csproj` garde les configurations Debug et Release du SDK. `dotnet test` sans `-c` compile la DLL en Debug : la branche « inactif » a besoin d'un point d'injection pour être testée. Modèle « lu une fois, journalisé » : `AlundraPlayerManager.DebugIgnoreControlLockFromEnvironment` et son `...OverrideForTests`. | `Alundra/Alundra.csproj` ; `AlundraPlayerManager.cs:109-147` |
| G11 | Les types de résultat du service (`SaveGameSaveResult`, `SaveGameLoadResult`, `SaveGameSlotInfo`) n'ont que des constructeurs internes au moteur, mais leurs énumérations d'état (`SaveGameSaveStatus`, `SaveGameLoadStatus`) sont publiques. `ListSlots` rend les emplacements par nom, avec `LastWriteTimeUtc` éventuellement nul ; aucun « plus récent » n'est fourni. Les noms d'emplacement suivent `^[a-z0-9_-]{1,32}$`. | `SaveGameSaveResult.cs:44`, `SaveGameLoadResult.cs:48`, `SaveGameSlotInfo.cs:65-85` ; `SaveGameService.cs:216-241` ; `SaveGameNames.cs` |
| G12 | La DLL n'a aucun affichage de message à l'écran ; ses refus passent par `Logs.WriteInfo` et `Logs.WriteWarning`. Le lanceur écrit le journal dans `log.txt`, relatif au dossier de travail du processus. | `Logs.cs` ; `CasaEngine.Launcher/Program.cs:13-14` ; `FileLogger.cs:13-17` |

**Choix du plan** :

- **K1 — Un directeur de session**, `AlundraSaveGameDirector` (`Alundra/Scripts/`, `Instance` et
  `ResetForTests` comme les autres). Il porte l'interrupteur, les touches, le service, le
  chargement en attente et son application.
  - **Règles de validation injectables** (relecture de clôture du 2026-09-28) : le directeur
    obtient ses règles par `internal Func<AlundraSaveGameRules>? RulesFactoryForTests`, nul par
    défaut, sur le modèle `...OverrideForTests` de G10. Nul, il construit les règles de
    production `new AlundraSaveGameRules(EngineEnvironment.ProjectPath, path =>
    AssetCatalog.GetByFileName(path) != null, ItemTables)`. Raison : dans le processus de
    `Alundra.Tests`, le catalogue d'assets est vide et ne se remplit que par des membres internes
    au moteur ou par un `AssetCatalog.Load` global qui remplace tout
    (`AssetCatalog.cs:17`, `:44-58`, `:169`). Sans ce point, toute validation refuserait
    `InitialMapId` en test. Les tests existants d'E16.c passent déjà un prédicat `_ => true`
    (`AlundraSaveGameProductionTests.cs:117`, `:209`). Le point n'est atteignable que par du code
    déjà dans le processus, comme SD14 ; aucune donnée ne l'atteint.
- **K2 — Interrupteur (D-E16-33)** : `internal static bool RecipeKeysEnabled`, vrai par `#if DEBUG`
  et faux sinon, avec un `...OverrideForTests` nullable sur le modèle de G10. Il est journalisé une
  fois (`Logs.WriteInfo`), au premier passage du directeur, quand il est actif. Inactif, le
  directeur ne lit même pas le clavier.
- **K3 — Touches** : F5 sauvegarde en binaire dans l'emplacement `debug-binary`, F6 en JSON dans
  `debug-json`, et F9 charge l'emplacement lisible le plus récent. Elles sont lues une fois par image
  rendue, dans `AlundraWorldProxy.Update` et hors de la boucle des ticks (G9), par
  `KeyboardManager.IsKeyPressed`, avec un front montant propre au directeur et un fournisseur
  `Func<Keys, bool>? KeyHeldProviderForTests`. Une seule touche est traitée par image, dans l'ordre
  F5, F6, F9.
  - **Emplacement de l'appel** : en tête d'`AlundraWorldProxy.Update`, juste après
    `AdvanceGameTime` et avant le calcul de `gameplayBlocked`. Un départ armé par F9 fige donc les
    passes « dedans » dès cette image : aucun évènement de carte ni `0x53` scripté ne peut, dans la
    même image, écraser le départ du chargement ou ouvrir un dialogue (SD3).
  - Le front montant vit dans le directeur de session : une touche maintenue pendant le changement
    de monde n'agit pas une seconde fois (SD13).
- **K4 — Service derrière une interface de la DLL** : `IAlundraSaveSlots` (interne) expose `Save`,
  `TryLoad` et `ListSlots`. Ses résultats sont des types de la DLL, sur les énumérations publiques du
  moteur (G11) :
  - `AlundraSaveOutcome(SaveGameSaveStatus Status, string Message)` ;
  - `AlundraLoadOutcome(SaveGameLoadStatus Status, string Message)` ;
  - `AlundraSlotEntry(string Name, bool IsReadable, DateTime? LastWriteTimeUtc)`.

  L'adaptateur de production, `AlundraEngineSaveSlots`, délègue à `GameSettings.SaveGames` et
  recopie ses résultats. Les tests en simulent tous les états (D-E16-31).
  - Il attrape **toute** exception du service : `InvalidOperationException` (nom de projet invalide,
    dossier local introuvable), `ArgumentException` et les autres. Il la journalise par
    `Logs.WriteError` et rend un état `IoError`, ou une liste vide pour `ListSlots` : un problème
    d'environnement ne fait jamais planter le jeu (SD6).
  - Un `Loaded` accompagné d'un objet nul est un refus.
- **K5 — Plus récent** : parmi les emplacements lisibles dont l'heure est connue, celui dont
  `LastWriteTimeUtc` est la plus grande ; à égalité, le nom le plus grand en ordre ordinal. Aucun
  emplacement de ce genre → refus « aucune sauvegarde lisible ». Limites acceptées pour une touche
  de recette et consignées dans l'ADR-0013 (SD11) :
  - une heure dans le futur gagne toujours ;
  - « lisible » ne veut dire que l'en-tête ouvert : un emplacement le plus récent mais abîmé bloque
    F9, sans repli sur le précédent ;
  - `ListSlots` lit chaque emplacement en entier (1 Mio au plus).

  Le choix d'un emplacement par le joueur relève d'E16.e.
- **K6 — Sauvegarde (F5/F6)**, dans cet ordre, sans rien écrire au moindre refus :
  1. **préconditions**, chacune testée à part (G7) : héros présent, au sol ; aucun dialogue ; aucun
     inventaire ni sous-inventaire ; post-traitement de l'inventaire à l'état 0
     (`AlundraInventoryPostProcess.Instance.State == 0`) et portrait inactif
     (`AlundraInventoryPortrait.Instance.State == AlundraInventoryPortrait.StateIdle`, et non
     `StateAtRest`, qui est le portrait affiché pendant un menu) (SD12) ; aucune transition ; aucun fondu maître de la musique armé
     (`AlundraBgmFadeDirector.Instance.IsArmed`, SD4) ; `PlayerControlFlags == 0` ;
  2. `TryCaptureFromWorld(GameState, world.Name, PlayerEntity)` (contrat de C5 d'E16.c) ;
  3. `TryValidate` avec les règles du directeur (K1 : celles de production hors tests) : un objet
     qui ne se chargerait pas n'est jamais écrit ;
  4. `Save(emplacement, objet, format, objet.BuildMetadata())`, puis le résultat au journal.
- **K7 — Chargement (F9)**, dans cet ordre ; seule l'étape 7 change l'état vivant, et seulement à
  l'arrivée :
  1. préconditions : héros présent ; aucun chargement déjà en attente (SD13) ; aucun dialogue ;
     aucun inventaire ni sous-inventaire ; post-traitement à l'état 0 et portrait à `StateIdle`,
     comme en K6 (SD12) ;
     aucune transition ; aucun fondu maître de la musique armé (SD4) ; `PlayerControlFlags == 0` ;
  2. emplacement le plus récent (K5) ;
  3. `TryLoad` : tout état autre que `Loaded` est un refus ;
  4. `TryValidate` avec les règles du directeur (K1) ;
  5. **contrôle du départ**, pour que la garde d'abandon ne puisse jamais se déclencher sur un
     chargement. Deux nouveautés internes et en lecture seule du directeur des warps y servent : un
     indicateur « `GameManager` attaché », et `TryResolveWorldPath(mapId, out path)` sur sa propre
     table. Conditions : warp non désactivé ; aucune transition ; `GameManager` attaché ;
     `InitialMapId` résolu par la table du directeur en un chemin non nul. Raison : un départ fait
     partir le fondu (verrou de persistance 1) et le son avant de résoudre son chemin, et
     `AbortDeparture` ne les défait pas (`AlundraWarpDirector.cs:409-455`, `:234-247`, SD1) ;
  6. le chargement devient **en attente** (`_pendingLoad`), puis
     `BeginDepartureForLoad(InitialMapId, (X × 24 + 12) << 16, (Y × 16 + 8) << 16, Z << 20,
     PlayerEntity)`. Ce nouveau départ interne passe par `BeginDepartureCore`, avec l'animation
     `ResetAnimationId` (`0x36`), la direction `ResetDirectionId` (0), l'effet 0 et une action sonore
     vide (ni effet sonore, ni `AlundraMusicPlayer.HandleWarpDeparture` : c'est le `PlayMapMusic` de
     l'arrivée qui règle la musique), comme
     le départ d'une nouvelle partie ou d'un chargement de l'original (`AlundraGameState.cs:53-59`),
     et non avec l'animation en cours du héros, que `BeginDepartureFromChangeMapOpcode` recopierait
     (`AlundraWarpDirector.cs:379-380`, SD2). La position suit la formule de la nouvelle partie
     (`AlundraWorldProxy.cs:1563-1565`). Si le départ n'est pas armé ensuite
     (`IsTransitionInProgress` faux), l'attente est abandonnée, avec un message ;
  7. application à l'arrivée (K8).
- **K8 — Application à l'entrée de la carte d'arrivée**. `AlundraSaveGameDirector.ApplyPendingLoad(
  GameState, mapId)` est appelé dans `InitializeWithWorld`, juste après `GameState.InstallForMapEntry()`.
  La contrainte réelle (G5) : l'application a lieu pendant l'installation du monde d'arrivée, donc
  avant son premier tick, où les programmes des entités et les évènements de carte lisent les
  valeurs chargées. `mapId` vient de `BackdropLoader.TryParseMapIndex(world.Name, …)`, comme dans
  `TryCaptureFromWorld` ; un nom sans id compte comme « carte d'arrivée ≠ `InitialMapId` ».
  - Sans chargement en attente, il ne fait rien.
  - Il **vide l'attente avant toute autre chose** : un chargement ne s'applique jamais deux fois
    (SD9).
  - Si la carte d'arrivée n'est pas `InitialMapId`, il abandonne l'attente avec un avertissement,
    sans rien appliquer.
  - Les retours anticipés d'`InitializeWithWorld` qui précèdent l'appel (`AlundraWorldProxy.cs:621-634`)
    abandonnent aussi l'attente, avec un message (SD9).
  - Sinon, dans cet ordre, sans lever :
    1. `AlundraGameState.ResetSessionForLoad()` : `PlayerControlFlags` à 0, `LastPadState` par
       défaut, `TickPad.Reset()`, le verrou d'interaction (entité et huit nombres), `IsWarpDisabled`
       à faux, `TextCategoryIndex` à 0, `GameVariables` à zéro (D-E16-19),
       `NewGameInventoryInitialized` à **vrai** (SC7). Rien de ce que porte la sauvegarde ;
    2. `ApplyTo` de l'objet (E16.c, C6) : drapeaux, table, compteurs, stats, compteur de reprises,
       temps de jeu ;
    3. `AlundraHudDirector.ResetDisplayForLoad(AlundraPlayerStats stats)`, avec les stats de l'état
       qui vient d'être chargé (la jauge peut ne pas avoir d'état attaché) : les valeurs affichées (PV, PV max, PM, PM max,
       argent) prennent les valeurs chargées, les quatre sous-étapes à 0, `CoinIconFrame` à 0,
       `IsMoneyRolling` à faux, images des cases de magie à 0. La phase, `Y`, le glissement,
       `FrameCounter` et `_gameState` ne changent pas : une jauge ouverte reste ouverte, sans
       saut (SC1) ;
    4. `ResetSessionForLoad()` des directeurs d'inventaire et de sous-inventaire (tout ce que remet
       leur `ResetForTests`, sauf les trois attaches `_gameState`, `_itemTables` et `_soundPlayer`),
       du post-traitement et du portrait (tout ce que remet leur `ResetForTests`) : curseurs et
       états à leur valeur de début de session. Chaque `ResetForTests` appelle désormais cette
       méthode, pour que la liste n'existe qu'une fois.

    Le dialogue est remis par sa propre entrée de carte (G8), et le héros est placé par l'arrivée
    du warp (G6).
  - `AlundraWarpDirector.AbortDeparture` (privée) abandonne aussi le chargement en attente, avec un
    message, en appelant `AlundraSaveGameDirector.Instance.AbandonPendingLoad(raison)` : le
    directeur des warps dépend du directeur de sauvegarde, jamais l'inverse. Un départ avorté ne
    laisse aucune attente appliquée plus tard à une autre arrivée.
- **K9 — Messages** : chaque refus, chaque résultat du service et chaque succès donnent une ligne de
  journal (`Logs.WriteWarning` pour un refus, `Logs.WriteInfo` pour un succès). La DLL n'a pas
  d'affichage à l'écran (G12), et ces touches ne servent qu'à la recette.

**Tâches** (branche `chantier/e16-proposition` ; un commit par tâche, avec la mise à jour de ce
plan ; `Alundra.Tests` sans échec à chaque commit, oracle de l'intro inchangé)

- ✅ **T1 — Remises de session pour un chargement** (fait le 2026-09-28 : `AlundraGameState.ResetSessionForLoad`, `AlundraHudDirector.ResetDisplayForLoad(stats)` avec un accès de test aux quatre sous-étapes, et `ResetSessionForLoad` des deux directeurs d'inventaire, du post-traitement et du portrait, que leur `ResetForTests` appelle désormais ; la jauge SC1 est fermée par la demande « cacher tout de suite » du script (drapeau 1814), qui fige la sous-étape ; 5 tests dans `AlundraSaveGameLoadResetTests` ; `Alundra.Tests` 1690/1690) (K8, étapes 1, 3 et 4). Tests :
  - `ResetSessionForLoad` de l'état de jeu : chaque champ listé revient à sa valeur, et aucun champ
    porté par la sauvegarde ne change ;
  - la jauge en plein rattrapage des PM max (affichés 3, vrais 4, `_mpMaxSubStep` non nul), puis des
    stats à 4 PM sur 4 et `ResetDisplayForLoad`, puis l'ouverture de la jauge et la fin des
    roulements → aucune exception (SC1), sur le chemin de la présentation réelle comme au T4
    d'E16.c ;
  - `ResetDisplayForLoad` garde la phase, `Y` et l'état attaché ;
  - les remises d'inventaire gardent les attaches ; les tests existants de `ResetForTests` passent
    sans changement.
- ✅ **T2 — Interrupteur, service et choix de l'emplacement** (fait le 2026-09-28 : `AlundraSaveGameDirector` (interrupteur `#if DEBUG` et son forçage, touches lues par `UpdateRecipeKeys` en tête d'`AlundraWorldProxy.Update` juste après le temps de jeu, fronts montants, `RulesFactoryForTests`, `SelectMostRecent`), `IAlundraSaveSlots` et ses trois types de résultat, `AlundraEngineSaveSlots` (qui prend une fonction rendant le service, pour que le test SD6 y branche un service qui lève) ; F5/F6 capturent, valident et écrivent, F9 choisit, charge et valide : préconditions de K6 et K7, départ et application viennent aux T3 à T5 ; 14 tests dans `AlundraSaveGameDirectorKeysTests`, plomberie commune dans `SaveGameDirectorTestSupport` ; `Alundra.Tests` 1704/1704 ; le test de la valeur par défaut en Release est lancé à l'acceptation) (K1 à K5). Tests :
  - interrupteur forcé actif puis inactif ; journal une seule fois, quand il est actif ;
  - inactif : aucune lecture du clavier, F5, F6 et F9 sans effet ;
  - front montant : une touche maintenue dix images n'agit qu'une fois, et la même touche, relâchée
    puis rappuyée, agit de nouveau ; F9 maintenue pendant le changement de monde n'agit pas une
    seconde fois (SD13) ;
  - K5 : heures différentes, heures égales (ordre des noms), heures nulles écartées, emplacements
    illisibles écartés, aucun emplacement ;
  - valeur par défaut de l'interrupteur, sans forçage : vrai dans une DLL compilée en Debug, faux en
    Release. Le test porte un `#if DEBUG` du projet de test, et se lance aussi avec
    `dotnet test -c Release --filter` (SD7) ;
  - un service simulé qui lève à chaque méthode, derrière l'adaptateur : F5, F6 et F9 donnent un
    refus, sans exception, état identique (SD6).
- ✅ **T3 — Sauvegarde F5/F6** (fait le 2026-09-28 : préconditions de K6 dans `AlundraSaveGameDirector` (`FindSaveBlocker`, puis `FindSessionBlocker`, commun à K7), chacune testée seule et nommée dans la ligne de refus ; les inventaires posent `MenuOpen` en s'ouvrant, remis à 0 dans leurs cas pour que seul l'inventaire refuse ; 34 tests dans `AlundraSaveGameDirectorSaveTests` (12 préconditions × F5/F6, succès F5 et F6 à la tuile (20, 30, 1) de la 389 avec métadonnées, capture refusée par `playerStats.money = -1`, monde sans id, cinq états de service autres que `Saved`) ; `Alundra.Tests` 1738/1738) (K6). Tests, sur un service simulé, avec des règles injectées par
  `RulesFactoryForTests` : le vrai dossier du projet exporté, un prédicat de catalogue `_ => true`
  et les tables d'objets réelles (le test échoue en nommant l'export s'il manque). Un test de refus
  par `TryValidate` garde ce prédicat qui accepte, pour que le refus vienne bien du champ hors
  domaine :
  - chaque précondition non tenue (héros absent, en l'air, dialogue, inventaire, sous-inventaire,
    post-traitement actif, portrait autre que `StateIdle` (ouverture, affiché, retour), transition,
    fondu maître de la musique armé,
    `PlayerControlFlags` non nul) → aucun appel à `Save`, état identique à un instantané ;
  - succès : `Save` reçoit `debug-binary` en binaire (F5) ou `debug-json` en JSON (F6), l'objet et
    ses métadonnées ; héros placé à une tuile connue de la carte 389 → objet capturé à cette carte et
    à cette tuile ;
  - un objet que `TryValidate` refuse (par exemple une stat hors domaine posée dans l'état) → aucun
    appel à `Save` ;
  - chaque état de sauvegarde autre que `Saved` → une ligne de journal, aucune exception.
- ✅ **T4 — Chargement F9 jusqu'au départ** (fait le 2026-09-28 : préconditions de K7 (héros, attente, puis `FindSessionBlocker` commun à K6), contrôle du départ et `_pendingLoad` dans `AlundraSaveGameDirector`, avec `AbandonPendingLoad` ; côté directeur des warps, `IsGameManagerAttached`, `TryResolveWorldPath` et `BeginDepartureForLoad` (`0x36`, direction 0, effet 0, action sonore vide), et `AbortDeparture` qui abandonne l'attente ; la précondition « héros en l'air » ne bloque pas F9 (K7 n'en a pas), un test le fixe ; le cas « `InitialMapId` non résolu » attache le directeur des warps à un dossier sans `world-index.json` ; les aides de blocage de T3 sont passées dans `SaveGameDirectorTestSupport` ; 32 tests dans `AlundraSaveGameDirectorLoadTests` ; `Alundra.Tests` 1770/1770) (K7). Tests, sur un service simulé, avec les mêmes
  règles injectées qu'au T3 :
  - chaque précondition non tenue (celles de K7, étape 1, dont un chargement déjà en attente et un
    fondu maître armé) → refus, aucun appel à `TryLoad`, état identique ;
  - aucun emplacement lisible → refus ;
  - `InitialMapId` que la table du directeur des warps ne résout pas → refus, et ni fondu, ni
    changement de musique, ni arrivée en attente (SD1) ;
  - F9 pendant une attaque ou un saut : l'arrivée porte l'animation `0x36` et la direction 0 (SD2) ;
  - F9 sur une image où un évènement de carte partirait : l'évènement ne tourne pas dans cette
    image (SD3) ;
  - chaque état de chargement autre que `Loaded` (`NotFound`, `TooLarge`, `Corrupted`,
    `UnsupportedContainer`, `NewerDataVersion`, `InvalidData`, `IoError`) → refus, état identique ;
  - un objet que `TryValidate` refuse → refus, état identique, aucun départ ;
  - warp désactivé, transition en cours, `GameManager` absent → refus, état identique, aucun
    départ, aucune attente ;
  - succès : attente posée, départ armé vers `InitialMapId` à la position de la tuile, état de jeu
    encore inchangé ;
  - départ avorté par la garde d'abandon → attente abandonnée.
- ✅ **T5 — Application à l'arrivée** (fait le 2026-09-28 : `AlundraSaveGameDirector.ApplyPendingLoad(state, arrivalMapId)`, appelé dans `InitializeWithWorld` juste après `GameState.InstallForMapEntry()` avec l'id tiré de `BackdropLoader.TryParseMapIndex`, et les deux retours anticipés qui abandonnent l'attente ; la tuile (33, 59, 0) convient : les entités des chargements 134 (décalage 143) et 139 (décalage 234) y apparaissent, et celle du chargement 135 (décalage 180) est activée par le `0x2D` du 134 et se place à l'image suivante ; le test de bout en bout contrôle l'état après la première image, pour qu'un chargement appliqué trop tard échoue sur les entités ; mutations faites et défaites par un script hors dépôt (remplacement d'un extrait exact, build, test filtré, fichier restauré octet pour octet) : appel supprimé → échec sur `TextCategoryIndex` resté à 1 (état non chargé), appel déplacé en fin de premier `Update` → échec sur la position du chargement 134 (ancien drapeau lu) ; 8 tests dans `AlundraSaveGameApplyTests` ; `Alundra.Tests` 1778/1778, oracle de l'intro inchangé) (K8). Tests, avec les mêmes règles injectées qu'au T3 ; le
  test de bout en bout atteint `ApplyPendingLoad` sans toucher au catalogue d'assets :
  - après `ApplyPendingLoad`, l'état est celui de la sauvegarde, champ par champ ;
    `TemporaryFlags` est vide (entrée de carte) ; `TextCategoryIndex` et `GameVariables` valent 0
    alors qu'ils ne l'étaient pas avant (D-E16-19) ;
  - `NewGameInventoryInitialized` vaut vrai, et une entrée de carte suivante sans arrivée de warp
    garde les objets chargés (SC7) ;
  - carte d'arrivée différente de `InitialMapId` → rien n'est appliqué, attente abandonnée ;
  - `ApplyPendingLoad` sans attente ne change rien, et un second appel après une application ne
    réapplique rien (SD9) ;
  - Start appuyé pendant le fondu du chargement : après `ApplyPendingLoad`, les deux inventaires au
    repos, `PlayerControlFlags` à 0, puis N ticks sans exception (SD5) ;
  - **de bout en bout, obligatoire** (SD8), sur le chemin de l'installation réelle de la carte 389
    (`InitializeWithWorld` puis le premier `Update`, comme les tests de production de la carte 389
    et `AlundraWorldProxySessionStateTests`) : F9 sur une session où le drapeau 860 est éteint,
    d'une sauvegarde où il est posé, arrivée sur la 389, puis premier tick. Assertions :
    - les programmes de chargement qui testent le drapeau 860 par `0x31`
      (`docs/intro-programs-389.txt`, décalages 143, 180, 234) prennent la branche du drapeau chargé,
      et non celle de la session. `0x31` saute quand le drapeau est éteint (`FlagBranch`,
      `AlundraEventProgramRunner.cs:1416`) : drapeau posé, leur placement `0x64` qui suit s'exécute,
      et l'entité est à la position qu'il donne ;
    - le héros est à la tuile sauvegardée, choisie pour que les entités de ces programmes
      apparaissent : la tuile de la nouvelle partie, (33, 59, 0), si elles y apparaissent comme
      dans le harnais de l'intro ; sinon l'exécuteur établit une tuile qui convient et l'inscrit ;
      le test vérifie d'abord que ces entités sont apparues ;
    - l'inventaire de nouvelle partie ne tourne pas.

    Deux mutations du code de production, faites puis défaites par un script, doivent chacune faire
    échouer ce test :
    - supprimer l'appel à `ApplyPendingLoad` → l'état n'est pas chargé ;
    - déplacer l'appel à la fin du premier `Update` du monde d'arrivée, après la passe des entités →
      les programmes de chargement ont lu l'ancien drapeau.

    La mutation « avant `GameState.InstallForMapEntry()` » est un mutant équivalent : cette méthode
    ne touche que `TemporaryFlags`, que `ApplyTo` n'écrit pas, et l'entité du verrou et
    `IsWarpDisabled`, que `ResetSessionForLoad` remet de toute façon. Elle n'est donc pas demandée.
- ✅ **T6 — Documentation et ADR** (fait le 2026-09-29 en session principale : ADR-0013
  `docs/decisions/0013-save-loads-go-through-the-warp-path.md`, sections « Chargement » et « Touches
  de recette » de `docs/formats/save-game.md`, doc d'`AlundraGameState.TextCategoryIndex` et
  `GameVariables`, que `ResetSessionForLoad` remet désormais, tableau de suivi du plan maître) : ADR-0013 de ce dépôt (le chargement part par le chemin des
  warps et s'applique à l'entrée de la carte d'arrivée ; touches de recette en Debug seulement,
  D-E16-33, la DLL déployée étant celle du dernier build ; le service derrière une interface de la
  DLL ; limites du choix du plus récent, SD11) ; section « Chargement et touches de
  recette » de `docs/formats/save-game.md` ; tableau de suivi du plan maître.
- ✅ **T7 — Vérification** : un `verifier` frais sur l'acceptation ci-dessous, et un contradicteur
  sur les chemins hostiles (fichier refusé, départ impossible, arrivée inattendue).

  Fait le 2026-09-29 sur `00746c7` :
  - `verifier` frais : build Release et Debug à 0 erreur, test de la valeur par défaut en Release,
    `Alundra.Tests` 1778/1778, oracle de l'intro inchangé, les deux mutations de T5 établies à la
    lecture : **CONFIRMED** ;
  - contradicteur, en lecture seule : **CONFIRMED**, aucun P0 à P2.

  Reportés :
  - (P3) si l'inventaire s'ouvre pendant le fondu d'un chargement, la jauge peut rester cachée à
    l'arrivée. `ResetSessionForLoad` referme l'inventaire sans son chemin de fermeture, et
    `ResetDisplayForLoad` garde la phase. Même trou préexistant sur les warps ordinaires (SD5).
    Cosmétique, rattrapable, Debug seulement ; écrit dans les limites de `save-game.md` et de
    l'ADR-0013 ;
  - (P4) seul l'adaptateur de production absorbe les exceptions du service ; le directeur se fie à
    l'interface ;
  - (P4) le chargement n'exige pas le héros au sol, contrairement à la sauvegarde (conforme à K7) ;
    le départ pose l'animation `0x36`.
- 🧪 **T8 — Recette en jeu** (l'auteur, hors de l'app Claude ; la DLL Debug est déployée dans
  `alundra-project/` par le dernier build de T5) : l'acceptation en jeu ci-dessus, avec
  la DLL compilée en Debug ; le journal montre la ligne de l'interrupteur.

**Acceptation d'E16.d** :
- les tests de T1 à T5 passent ; `Alundra.Tests` est sans échec, l'oracle de l'intro inchangé ;
- build à 0 erreur, **dans cet ordre** (SD7). Chaque build d'`Alundra.csproj` recopie sa DLL dans
  `alundra-project/`, et le jeu charge la dernière recopiée :
  1. `-c Release`, avec les touches compilées mais inactives par défaut, et le test de la valeur par
     défaut lancé en Release ;
  2. Debug, puis les tests, en dernier : la DLL laissée dans le projet pour la recette est celle de
     Debug ;
- le verifier et le contradicteur rendent **CONFIRMED** ;
- T8 reste 🧪 tant que l'auteur n'a pas joué la recette.

Adaptations dues à D-E16-31, et couvertes ailleurs :
- « dans les deux formats » : les refus de la DLL portent sur l'objet décodé et ne dépendent pas du
  format ; la recette en jeu essaie les deux formats, un emplacement hostile compris ;
- « un fichier au nom invalide dans le dossier » : `ListSlots` du moteur ne le liste pas du tout
  (`SaveGameFileStorage.cs:280-284`, SD10), donc la DLL ne le voit jamais ; elle écarte aussi les
  emplacements illisibles (T2).

**Revue de sécurité d'E16.d (2026-09-28)** : `security-reviewer` frais, en lecture seule, sur
`775d7d9`. Aucun P0 à P2. L'ordre et l'atomicité de K7 et K8 tiennent :
- rien de ce qui précède `InstallForMapEntry` ne lit l'état de session ;
- rien de ce qui suit n'écrase les valeurs chargées ;
- un second F9 est refusé par la porte de la transition ;
- une arrivée sur une autre carte abandonne l'attente.

La remise de session couvre tous les singletons, sauf le fondu maître de la musique (SD4).

| Réf | Prio | Constat | Décision | Où |
|---|---|---|---|---|
| SD1 | P3 | Un départ avorté par la garde d'abandon laisse l'écran blanc et la musique en fondu : fondu et son partent avant la résolution du chemin, que la table du directeur des warps fait seule | FIX (contrôlé en session principale) | K7 étape 5 (`TryResolveWorldPath`), T4 |
| SD2 | P3 | Le départ de `0x53` recopie l'animation en cours du héros ; l'original charge avec `0x36` et la direction 0 | FIX | K7 étape 6 (`BeginDepartureForLoad`), T4 |
| SD3 | P3 | Emplacement des touches dans `Update` non fixé : un F9 après le calcul de `gameplayBlocked` laisserait tourner les évènements de la même image | FIX | K3, T4 |
| SD4 | P3 | Un fondu maître de la musique (`0xA6`) armé survit au chargement et peut couper la musique de la carte d'arrivée | FIX | K6 et K7 étape 1, T3, T4 |
| SD5 | P3 | L'inventaire peut s'ouvrir pendant le fondu d'un départ (probable) ; pour un chargement, K8 nettoie | FIX (test) | T5. Le même trou sur les warps ordinaires est préexistant : signalé à part, hors E16 |
| SD6 | P3 | Une exception du service (environnement) sortirait dans `Update` | FIX | K4, T2 |
| SD7 | P3 | La configuration active dépend du dernier build : un build Release éteint les touches de la recette, un `dotnet test` redéploie une DLL Debug | FIX | acceptation (ordre), T2 (valeur par défaut), ADR-0013 |
| SD8 | P3 | Le test de bout en bout de l'ordre d'application n'était que conditionnel | FIX | T5 (obligatoire, deux mutations) |
| SD9 | P4 | L'attente n'est pas vidée d'abord, ni abandonnée sur un retour anticipé | FIX | K8, T5 |
| SD10 | P4 | Un nom de fichier invalide n'est pas listé du tout, et non « illisible » | FIX | texte des adaptations |
| SD11 | P4 | K5 : heure future, emplacement récent abîmé sans repli, lecture complète | DEFER | ADR-0013 ; E16.e porte le choix d'emplacement |
| SD12 | P4 | Post-traitement et portrait absents des préconditions ; `Hp ≥ 1` absent de K6 ; scripts de carte non verrouillés admis | FIX les deux premiers ; DEFER le reste | K6, K7 ; `Hp` → E18 (SC8), le reste → E16.e |
| SD13 | P4 | Refuser F9 si une attente existe ; une touche maintenue pendant le changement de monde ne doit pas repartir | FIX | K3, K7 étape 1, T2 |
| SD14 | P4 | Le forçage de test est atteignable en Release par `InternalsVisibleTo` | REJECT | seul du code déjà dans le processus peut s'en servir |
| SD15 | — | Ajout de la relecture de clôture : `RulesFactoryForTests` (K1), autre point d'injection de test ; il ne remplace que les règles, jamais le parcours de validation | même raison que SD14 | K1 |

**Arrêts** : ceux de la section E16.d ci-dessus ; une exception qui sort d'une touche ou de
`ApplyPendingLoad` ; un test existant qui change pour une autre raison ; l'oracle de l'intro qui
bouge.

### E16.e — Écran de sauvegarde en jeu ⏳ (DLL, MGUI en XAML ; préparée le 2026-09-28 en mode AUTO ; choix de l'auteur reçus le 2026-09-29, D-E16-34 à D-E16-37 ; plan détaillé à écrire)

- **But** : le joueur sauvegarde lui-même, comme dans l'original.
- **Contenu** : le gestionnaire du livre de sauvegarde (`AI_ProcessWarpTransitionState`, question
  oui/non puis `UpdateSavedData`), porté ici (D-E16-12), et le flux de `MemoryCardManager`
  (choix de l'emplacement, confirmation d'écrasement, messages de réussite et d'échec) ; écran
  déclaré en XAML (règle de l'auteur). Le gestionnaire d'origine fait environ 2 800 lignes : **cette
  tranche aura son propre plan**, écrit après E16.0, avec ce prérequis de sécurité : les métadonnées
  d'un emplacement sont du texte non fiable, affiché borné en longueur et avec le formatage en ligne
  de MGUI désactivé (`MGTextBlock.cs:837`), ou recalculé depuis une sauvegarde validée. Avant de
  servir d'indice, `chapter` est lu en culture invariante et borné à 0..41, ou recalculé depuis une
  sauvegarde validée (SC9).
- **Arrêt** : un manque de MGUI ou du moteur → consigné dans le rapport dédié, la tranche s'arrête
  (règle de l'auteur : signaler, jamais contourner).
- **Dépendances** : E16.0, E16.d.

#### Préparation d'E16.e (2026-09-28, mode AUTO) — ⚠️ en pause : choix de l'auteur

L'auteur a demandé de faire tout E16 en autonomie. E16.e demande pourtant des choix de produit
qu'aucun fait du code ne tranche (règle : « Ask user only for product/authority choice »). La
tranche s'arrête donc ici, préparée, avec les questions ci-dessous (points O-E16-13 à O-E16-16 du
§3).

**Faits établis** (exploration du 2026-09-28, recontrôlée ; désassemblage de la session principale)

| Réf | Fait | Source |
|---|---|---|
| H1 | Le gestionnaire du livre, `AI_ProcessWarpTransitionState` (`0x8007B998`), est un petit automate : environ 130 lignes décompilées, états 1 à 6, dont le 3 est un simple retour. État 1 : si `0x80045004` rend non nul, abandon ; sinon message ETC `0x40` (`InitializeDialogMessage(texte, 1)`, `SetEtcAnimationMode(4)`), attente de 60 images. État 2 : question oui/non (ETC `0x41`/`0x42`, `InitializeAsyncOperation`). État 4 : réponse ; autre que « oui » → abandon. État 5 : 60 images, puis `UpdateSavedData`. État 6 : attente de `g_globalTransitionState == 0`, puis remise. L'abandon et la remise effacent le bit `ControlLocked`, posé par l'entrée d'interaction (`SpriteEventHandlers.cs:270-277`). | désassemblage `0x8007B998`–`0x8007BB2C`, table `0x80028204` ; `FunctionTypeC.cs:6823-6951` |
| H2 | **Défaut de l'original** : si `InitializeAsyncOperation` échoue (retour 0), l'état passe à 3, qui ne fait rien : le livre est gelé et le héros reste verrouillé (`ControlLocked`). La double écriture de la décompilation est bien dans le binaire (`0x8007BA5C`–`0x8007BA7C`). Selon la règle de l'auteur, le port corrige : échec → abandon. | idem |
| H3 | Le flux de carte mémoire, `MemoryCardManager` (`0x8005EC98`), occupe un fichier décompilé de 2 848 lignes et environ 90 états ; il lit 38 textes ETC distincts, dont 8 n'ont aucun nœud dans `Dialogues/Etc.yarn` exporté (`0x88`, `0x9F`, `0xA0`, `0xA4`, `0xA6`, `0xAA`, `0xAE`, `0xB2`), et deux chaînes japonaises en dur, sur un chemin gardé par l'état `0x2710` qui ressemble à un test. Une partie des états ne sert que le matériel PS1 (présence, format et corruption de la carte, icône, somme de contrôle). | `MemoryCardManager.cs` de l'analyseur ; `alundra-project/Dialogues/Etc.yarn` |
| H4 | Dans la DLL, le livre (type de sprite 237, 65 cartes) apparaît comme toute entité, mais son IA native ne tourne pas : `RunSpriteEvent` est un « no-op » compté pour les ~120 gestionnaires natifs (E14). Le point d'accroche le plus petit est `AlundraEntityScriptProxy.RunPickedEvent` (`:1192-1198`), sur `SpriteProgramIndexes[ProgramCTick]` = 72 ; les champs d'état (`Bytes`, `DelayOrAngleOrEntityId`) existent déjà. | `AlundraEventProgramRunner.cs:16-21`, `:268-279` ; `AlundraEntityScriptProxy.cs:789-790`, `:1185-1205` |
| H5 | La DLL sait poser une question oui/non Yarn (`AlundraDialogueDirector.OpenChoice`/`TakeChoiceResult`) mais n'a pas d'équivalent du couple « message ETC + attente » de l'original. `AlundraEtcStringTable` résout tout texte ETC 0..1023 en privé, sans accès générique public. Quatre écrans XAML existent déjà (dialogue, jauge, inventaire, sous-inventaire), sur le modèle Window + Canvas + MGBinding + font3, avec `AllowsInlineFormatting="False"` pour du texte non fiable. | `AlundraDialogueDirector.cs:393-428` ; `AlundraEtcStringTable.cs:111-181` ; `alundra-project/UI/Screens/*.xaml` |
| H6 | Le service du moteur a des emplacements nommés libres ; l'original avait 4 blocs par carte, et le choix d'une sauvegarde au démarrage appartenait à `LOADER.EXE`. E16.c écrit déjà les métadonnées `chapter` et `summary`. | `SaveGameNames.cs` ; `LoaderSaveSlots.cs:83` ; C7 d'E16.c |

**Questions à l'auteur** (réponses nécessaires avant le plan détaillé) :
- **O-E16-13 — Aspect** : reproduire les écrans de carte mémoire de l'original (liste des blocs,
  icônes, messages de carte absente, pleine ou abîmée), ou un flux simplifié qui garde les temps
  visibles (message du livre, question oui/non, choix de l'emplacement, confirmation d'écrasement,
  réussite ou échec) et abandonne les états propres au matériel PS1 ?
- **O-E16-14 — Emplacements** : 4 emplacements fixes comme une carte mémoire, un seul emplacement
  implicite, ou une liste libre ?
- **O-E16-15 — Libellé d'un emplacement** : chapitre et résumé (`HP xx TIME hh:mm:ss`) recalculés
  depuis la sauvegarde validée, date d'écriture, numéro, ou une combinaison ?
- **O-E16-16 — Textes** : quels messages de l'original garder (textes ETC), et que faire des 8
  textes absents de l'export et des chaînes japonaises en dur ?

Défaut H2 : corrigé par défaut (règle « corriger les défauts de l'original »), sauf avis contraire.

**Réponses de l'auteur (2026-09-29)** :
- **D-E16-34 (O-E16-13) — Les écrans de carte mémoire de l'original sont reproduits**, adaptés aux
  fichiers : liste des blocs, icônes, messages. Le plan détaillé classe chaque état de
  `MemoryCardManager` du flux de sauvegarde : reproduit tel quel, adapté aux fichiers, ou sans
  objet ;
- **D-E16-35 (O-E16-14) — 4 emplacements fixes**, comme une carte mémoire de l'original ;
- **D-E16-36 (O-E16-15) — Un emplacement est libellé par son chapitre et son résumé**
  (`HP xx TIME hh:mm:ss`), comme l'original, **recalculés depuis la sauvegarde validée** et non lus
  dans les métadonnées non fiables ;
- **D-E16-37 (O-E16-16) — Seuls les textes utiles sont gardés** : les textes ETC des états
  reproduits ; les 8 textes absents de l'export sont vérifiés dans `ETC_RES.R` avant d'être
  écartés ; les chaînes japonaises en dur du chemin de test sont ignorées.

#### Plan détaillé d'E16.e (2026-09-29)

**Statut** : proposé en mode AUTO (« fait tout E16 de façon autonome ») ; revue de sécurité, puis
relecture de plan jusqu'à READY, avant l'exécution ; vérification par un `verifier` frais et un
contradicteur. Exploration du 2026-09-29 en lecture seule, chaque surface recontrôlée par un second
agent (`scratchpad/e16def/e16e-*.md`).

**Faits établis** (compléments de H1 à H6)

| Réf | Fait | Source |
|---|---|---|
| J1 | Seul `UpdateSavedData` (état 5 du livre) arme le flux de carte mémoire : `InitializeSaveDataCopy(g_saveData, 0x758, 1)` pose `g_globalTransitionState = 0x2710` et `g_postProcessingState = 1` ; `UpdateMemoryCardProcess` part alors dans `StartMemoryCardProcess`. La voie `g_postProcessingState == 3` (`UpdateSaveGameTransition`, avec les chaînes japonaises de test) n'est jamais armée. | désassemblage `0x8005EC44`, `0x8005EC98` ; `MemoryCardManager.cs:99-132`, `:580-586` ; `GameEngine.cs:2649-2680` |
| J2 | Le flux de sauvegarde de `StartMemoryCardProcess` (`0x8005F458`, une cinquantaine d'états) se classe en trois familles, tableau complet dans `scratchpad/e16def/e16e-flow.md` : (a) les états visibles du joueur, conservés par L2 ; (b) les états qui ne servent que la carte PS1 : examen des ports, formatage, place libre et suppression d'une partie quand la carte est pleine, réservation d'un bloc, icône et somme de contrôle, carte absente ou changée ; (c) la chaîne de fin commune. L'examen des ports est asymétrique dans le binaire lui-même : une carte trouvée au seul port 2 mène à « Insère une Carte Mémoire. » sans jamais sauvegarder. | `MemoryCardManager.cs:592-1737` ; désassemblage `0x8005F7F4`–`0x8005F8AC` |
| J3 | L'écran de choix (état `0x0C`) remplit quatre entrées : pour une partie existante, ligne 1 = résumé (`GameStateDescription`), ligne 2 = chapitre (`CurrentFlagName`) ; pour une entrée vide, `"{n}."` et une ligne vide. Titre : ETC `0x83`/`0x84`. Le sélecteur (`DisplayMemoryCardMenu`, rendu de la transition 10) lit Haut/Bas (au rythme de répétition) et Croix : Croix ouvre OUI/NON (ETC `0x4A`/`0x4B`) ; OUI retient l'entrée, NON annule tout le choix (pas de retour au choix). | `MemoryCardManager.cs:1229-1269`, `:2128-2480` |
| J4 | Le sélecteur est un **carrousel** de quatre boîtes (288 × 56, même dessin que la boîte de description de l'inventaire, déjà exportée) : les quatre enregistrements tournent sur les hauteurs `-1, 0, 64, 128, 240` (`SHORT_ARRAY_800C436C`), glissent par `UpdateUiBoxesPosition` (déjà porté trois fois dans la DLL) et changent de teinte RVB sur 15 images (`InitializeUIMemoryFileBox`, cible `0x80` pour l'entrée du milieu). Le texte : deux lignes par boîte, à `(X + 16, Y + 8)` et `(X + 16, Y + 32)`, en font3. La boîte des messages est `g_uiBoxesInventoryDescriptionBackground` (16, 168). L'icône de carte mémoire n'est jamais dessinée à l'écran : elle ne va que dans l'en-tête du fichier PS1. | `MemoryCardManager.cs:2128-2480`, `:2599-2699`, `:1758-1813` ; `StaticVariables.cs:11195-11203`, `:11329-11386`, `:12406` ; `MGImage.cs:220-230` (`TextureColor`) |
| J5 | Les 8 textes absents de l'export sont vides dans `ETC_RES.R` lui-même (vérifié octet par octet dans le fichier d'origine) : 6 secondes lignes vides et une paire vide (`0x9F`/`0xA0`). Les noms des 42 chapitres sont les textes ETC 0 à 41 (`Etc_0000` « Un Nouveau Départ » … `Etc_0041`). | `EtcIndexTable.csv` ; `data-extracted/data/ETC_RES.R.json` ; `alundra-project/Dialogues/Etc.yarn` |
| J6 | La DLL : le livre atteint son créneau F (interaction) par `PickEventTrigger`, puis `RunPickedEvent` appelle `RunSpriteEvent`, un « no-op » compté pour toute IA native. Le code F 1 de l'original (`Script_FInteract_FUN_8007fc64`) verrouille le héros (`ControlLocked`) : le porter pour toute entité de code F 1 verrouillerait le héros devant des entités dont l'IA de tick n'est pas portée. | `AlundraEntityScriptProxy.cs:1140-1205` ; `AlundraEventProgramRunner.cs:268-279` ; `SpriteEventHandlers.cs:233-234`, `:270-277` |
| J7 | Message ETC et oui/non dans la DLL : `AlundraDialogueDirector.Open(asset, nœud, 1)` sur l'asset `dialogue_etc` (nœuds `Etc_{index:D4}` en décimal) ; `SetCloseMask` est l'opcode `0x50`, que l'original appelle `SetEtcAnimationMode` ; `OpenChoice`/`TakeChoiceResult` pour OUI/NON. `AlundraEtcStringTable` charge `dialogue_etc` mais n'expose ni l'asset ni un texte ETC quelconque. | `AlundraDialogueDirector.cs:98-103`, `:213-343`, `:393-428` ; `AlundraEtcStringTable.cs:51-181` |
| J8 | Les écrans XAML modaux à suivre sont ceux de l'inventaire et du sous-inventaire : classe `XamlUIScreenBase`, `.uiscreen` catalogué par l'export (`UiWriter.RegisterVersionedScreens`), toile 320 × 240, directeur logique (singleton de session, sans MGUI), présentateur câblé par `AlundraWorldProxy`, `TickPad` pour les touches, `MenuOpen` posé à l'ouverture. Les fichiers d'écran sont suivis par git sous `alundra-project/UI/Screens/`. | `AlundraInventoryScreen.cs:34-164` ; `AlundraWorldProxy.cs:1244-1274` ; `UiWriter.cs:161` |
| J9 | La garde « `g_globalTransitionState == 0` » du déclencheur de l'inventaire est déclarée absente et traitée comme toujours vraie. | `AlundraInventoryDirector.cs:14-20`, `:417-421` |
| J10 | E16.d ne se réutilise pas tel quel : sa sauvegarde refuse tout `PlayerControlFlags` non nul, or le livre pose `ControlLocked` avant d'écrire. E16.e compose donc les briques : `TryCaptureFromWorld`, `TryValidate`, `IAlundraSaveSlots.Save`, `TryLoad`. | `AlundraSaveGameDirector.cs:465-533` ; `IAlundraSaveSlots.cs:15-42` |

**Choix du plan (à approuver)**

- **L1 — Le livre seulement.** Le port de l'IA native ne vise que le livre : l'entité de type de
  sprite 237, nommée « SaveBook (Ne pas toucher !) » (H4, J6). `RunSpriteEvent` reçoit une
  répartition minimale, sur (créneau, code), qui ne connaît que le créneau F code 1 et le créneau C
  code 72, **et seulement pour cette entité**. Toute autre IA native reste un « no-op » compté,
  comme aujourd'hui.
- **L2 — États conservés, adaptés, sans objet** (D-E16-34) :
  - conservés, dans l'ordre de l'original :
    - `0x2710` : message ETC `0x87` « Examen de la Carte Mémoire . . . » ;
    - attentes `0x2711` et `0x3f4` ;
    - `0x0C` : choix, titre `0x83`/`0x84`, quatre entrées ;
    - le carrousel de J4, avec Haut/Bas, Croix, OUI/NON `0x4A`/`0x4B` ;
    - `0x3f8` : message `0xA5` « Enregistrement de l'histoire . . . » ;
    - `0x10` : écriture ;
    - `0x3fe` : message `0xB1` « Histoire enregistrée. » ;
    - `0x3fd`/`0x15` : message `0xAF`/`0xB0` d'échec ;
    - la chaîne de fin `0x3f5` (Carré ou automatique), `0x0D`, `0x3f6`, `0x0E` (remise de la jauge
      avant de la cacher), `0x44B`, `0x63` ;
  - adapté : `0x10` écrit l'objet capturé à l'état 5 du livre dans l'emplacement `slot{n}` (n = 1 à
    4), en binaire, par `IAlundraSaveSlots.Save` avec `BuildMetadata`. Tout état autre que `Saved`
    mène à l'échec `0x15` ;
  - sans objet, car le port écrit des fichiers : examen des ports, formatage, place libre,
    suppression quand la carte est pleine, réservation d'un bloc, icône et somme de contrôle, et les
    messages de carte absente, changée, non formatable. Avec quatre emplacements fixes, la carte
    n'est jamais pleine.

  Les attentes gardent les durées de l'original (`AdvanceFadeOldCheck` : `0x13`, `0x1c`, `0x0B`
  images). Ce sont des temporisations, pas des fondus (J4).
- **L3 — Libellés** (D-E16-36). À l'ouverture du choix, pour chaque emplacement `slot1` à `slot4` :
  `TryLoad` puis `TryValidate` avec les règles de production (injectables comme au K1 d'E16.d).
  - Réussite : ligne 1 = `AlundraSaveGame.BuildSummary(hpMax, gameTime)` ; ligne 2 = le nom du
    chapitre, texte ETC d'indice `AlundraChapterFlags.GetFirstEnabledFlagIndex(GameFlags)` (0 à 41,
    J5).
  - Tout autre cas (vide, illisible, refusé) : `"{n}."` et une ligne vide, comme un emplacement vide
    de l'original. L'emplacement reste écrasable.
  - Les métadonnées du fichier ne sont jamais lues.
  - Les textes affichés viennent du jeu (textes ETC, résumé calculé), sont bornés en longueur, et
    passent par des `TextBlock` font3 avec `AllowsInlineFormatting="False"`.
- **L4 — Livre (port de `0x8007B998`, avec le défaut H2 corrigé)** :
  - **créneau F** : `Bytes` à 1, 0, 0, 0 ; `TargetAnimationId` à 0 ; `ControlLocked` posé ;
  - **état 1** : si un dialogue est déjà ouvert, abandon (l'équivalent de `0x80045004`) ; sinon
    `Open(dialogue_etc, "Etc_0064", 1)` (ETC `0x40`), `SetCloseMask` avec le bit de script, 60
    ticks, puis fermeture par le script ;
  - **état 2** : `OpenChoice` avec les textes ETC `0x41`/`0x42`. Si l'ouverture échoue, **abandon**,
    au lieu de rester à l'état 3 comme l'original (H2) ;
  - **état 4** : `TakeChoiceResult`. Autre que OUI : abandon. OUI : 60 ticks ;
  - **état 5** : capture par `TryCaptureFromWorld` (la carte courante et la tuile du héros, comme
    `UpdateSavedData`), puis `TryValidate`. Refus : message d'échec `0x15`, sans écriture. Sinon :
    démarrage de l'écran de sauvegarde avec l'objet capturé ;
  - **état 6** : attente de la fin de l'écran, puis remise (état 0, `ControlLocked` effacé) ;
  - l'abandon efface `ControlLocked` comme `ResetWarpState`.
- **L5 — Directeur et écran**. `AlundraSaveScreenDirector`, directeur logique singleton de session,
  sans MGUI, sur le modèle de l'inventaire (J8). Il remplace l'état global `g_globalTransitionState`
  de l'original :
  - `IsActive` vaut vrai de l'état `0x2710` jusqu'à la fin de `0x63` ;
  - la garde du déclencheur de l'inventaire (J9) teste désormais `!AlundraSaveScreenDirector.Instance.IsActive` ;
  - `MenuOpen` est posé et effacé là où l'original le fait (effacé en `0x63`).

  Autour du directeur :
  - `AlundraSaveScreenPresenter` et `AlundraSaveScreen : XamlUIScreenBase` ;
  - `SaveScreen.xaml`, `.uiscreen` et `.design.json` sous `alundra-project/UI/Screens/`, suivis par
    git comme ceux de l'inventaire ;
  - toile 320 × 240, reprise du sprite `g_uiBoxesInventoryDescriptionBackground` pour la boîte des
    messages et pour les quatre boîtes du carrousel (même dessin, J4) ;
  - teinte par `TextureColor` ;
  - Haut/Bas/Croix/Carré lus sur `TickPad`.
- **L6 — Textes ETC**. `AlundraEtcStringTable` gagne un accès public générique,
  `TryResolveText(int etcIndex, out string text)`, et l'accès à son asset chargé pour
  `AlundraDialogueDirector.Open`. Les textes absents (J5) rendent une ligne vide, comme l'original.
- **L7 — Le livre ne charge pas.** Le chargement par le joueur relève d'un écran titre (D-E16-10,
  plus tard) ; F9 reste la recette.
- **L8 — Écart consigné** : la sauvegarde du livre ne met pas à jour la copie de reprise après la
  mort (`g_saveDataInRam`), qui appartient à « Réessayer » (E18).

**Tâches** (branche `chantier/e16-proposition` ; un commit par tâche, avec la mise à jour de ce
plan ; `Alundra.Tests` sans échec à chaque commit, oracle de l'intro inchangé)

- ⏳ **T1 — Textes ETC et répartition du livre** (L1, L6). Tests :
  - `TryResolveText` rend « Enregistrer tes progrès? » pour `0x40`, « OUI »/« NON » pour
    `0x4A`/`0x4B`, « Un Nouveau Départ » pour 0 ; une ligne vide pour `0x88` ; faux hors de 0..1023 ;
  - une entité de type 237 atteint le livre porté par ses créneaux F et C ; une autre entité de code
    F 1 reste un « no-op » compté (aucun `ControlLocked`).
- ⏳ **T2 — Livre** (L4). Tests, sur le directeur de dialogue réel :
  - chaque transition des états 1, 2, 4, 5 et 6, avec les durées de 60 ticks ;
  - NON → abandon, `ControlLocked` effacé ;
  - un dialogue déjà ouvert à l'état 1 → abandon ;
  - échec de l'ouverture du choix → abandon, et non blocage à l'état 3 (H2) ;
  - OUI → capture à la carte et à la tuile du héros, puis démarrage de l'écran (directeur simulé) ;
  - capture refusée par `TryValidate` → message d'échec, aucune écriture.
- ⏳ **T3 — Directeur de l'écran** (L2, L3, L5, sans MGUI). Tests, sur un service simulé :
  - la suite des états conservés, les temporisations, les textes affichés ;
  - libellés : un emplacement valide montre son résumé et son chapitre recalculés, et des
    métadonnées fausses dans le fichier n'y changent rien ; un emplacement vide, illisible ou refusé
    montre `"{n}."` ;
  - carrousel : Haut/Bas au rythme de répétition, positions et teintes de J4, bornes (pas de
    défilement au-delà de la première et de la quatrième entrée) ;
  - Croix → OUI : écriture dans `slot{n}` en binaire, avec l'objet capturé et ses métadonnées,
    puis « Histoire enregistrée. » ; NON : annulation, rien d'écrit, fin directe par la chaîne de
    fin ;
  - chaque état de sauvegarde autre que `Saved`, et une exception du service simulé derrière
    l'adaptateur → message d'échec `0x15`, aucune exception ;
  - `IsActive` et `MenuOpen` sur tout le parcours ; l'inventaire ne s'ouvre pas pendant l'écran.
- ⏳ **T4 — Écran XAML et présentation** (L5). `SaveScreen.xaml`, `.uiscreen`, `.design.json`,
  présentateur et vue-modèle, câblés comme l'inventaire.
  - Tests : le présentateur pousse l'écran à l'activation et le retire à la fin ; la vue-modèle suit
    le directeur (positions, teintes, textes) ; un `MGDesktop` de test charge l'écran sans erreur
    (règle de l'auteur : jamais un `UIRoot`).
  - Ensuite, **export complet en place**, pour cataloguer le nouvel écran. Il ne doit changer que
    le catalogue et le rapport (preuve par diff des manifestes) ; jamais pendant que `Alundra.Tests`
    tourne.
  - Un manque de MGUI ou du moteur est consigné dans le rapport dédié, et la tâche s'arrête.
- ⏳ **T5 — Bout en bout** : sur la vraie carte 17 (un des 65 livres), interaction avec le livre (Carré si son drapeau
  `InteractRequiresButton` l'exige, sinon au contact), message,
  OUI, écran, choix de `slot2`, OUI → `Save` reçoit `slot2` en binaire. Puis le libellé de `slot2`
  recalculé, sur un service simulé qui rend ce qu'il a reçu, montre le chapitre et le résumé de la
  partie.
- ⏳ **T6 — Documentation et ADR** (session principale) : ADR-0014 (écran de sauvegarde du livre :
  états conservés, adaptés et sans objet, emplacements, libellés recalculés, correction de H2) ;
  `docs/formats/save-game.md` ; plan maître.
- ⏳ **T7 — Vérification** : un `verifier` frais et un contradicteur.
- ⏳ **T8 — Recette en jeu** (l'auteur) : un livre, sauvegarde dans chacun des 4 emplacements,
  libellés relus ; F9 recharge la plus récente.

**Acceptation d'E16.e** : les tests de T1 à T5 passent ; `Alundra.Tests` sans échec, oracle de
l'intro inchangé ; build Release puis Debug à 0 erreur (Debug en dernier) ; export en place dont le
diff ne touche que le catalogue et le rapport ; verifier et contradicteur **CONFIRMED** ; T8 🧪.

**Arrêts** :
- un état de l'original dont la classe (conservé, adapté, sans objet) n'est pas établie par le code
  ou le binaire ;
- un manque de MGUI ou du moteur (rapport dédié, règle de l'auteur) ;
- une exception qui sort du livre ou de l'écran ;
- un test existant qui change pour une autre raison, ou l'oracle de l'intro qui bouge.

### E16.f — Variables Yarn adossées aux drapeaux ✅ (DLL, docs ; relecture REVISE puis READY le 2026-09-28 ; exécutée en mode AUTO, « fait tout E16 de façon autonome » ; faite et vérifiée CONFIRMED le même jour, `82ab618` à `c4ff1d5`)

Relectures du 2026-09-28 :
- première relecture fraîche (`775d7d9`) : **REVISE**, un P2. La ligne « les 485 dialogues exportés
  toujours joués » ne nommait aucun test ; elle est remplacée par le test de corpus
  `AlundraYarnVariableCorpusTests` (`ad07ae4`) ;
- relecture de clôture : **READY**.

Vérification du 2026-09-28, sur `3e58ad9` :
- `verifier` frais : **CONFIRMED**, `Alundra.Tests` 1683/1683 ;
- contradicteur : **REFUTED**, un P2. Le premier constat de T1 attribuait au stockage le « faux » d'une
  lecture refusée. En réalité, la machine de Yarn prend alors la valeur initiale du programme compilé
  (`VirtualMachine.cs`, cas `PushVariable`, décompilé depuis le paquet 3.2.1), et ne lève que pour un
  nom absent de ces valeurs.
  - Correction `c4ff1d5` : deux tests de T1 le prouvent (variable déclarée à vrai, lue vraie malgré le
    refus ; variable non déclarée, qui reçoit une valeur initiale implicite du compilateur), et le
    test de corpus compte exactement 485 fichiers ;
  - revérification ciblée : **CONFIRMED**, 1685/1685.

Reportés, sans effet tant que le corpus n'a pas de variable :
- (P3) le chemin dégradé `AlundraEventProgramRunner.PlayNodeHeadlessToEnd` crée son runner sans ce
  stockage ; limite écrite dans `docs/formats/dialogues-yarn.md` ;
- (P4) `TryGetValue<T>` refuse d'autres types auxquels un booléen s'affecte (`bool?`,
  `IComparable`…), que Yarn 3.2.1 ne demande jamais ;
- (P4) le test de `Clear()` ne vérifie pas la ligne de journal, que le code écrit.

Écarté : `Clear()` se journalise à chaque appel, ce que demande le point 7 du contrat.

- **But** : D-E16-14 à D-E16-18 (ADR-0010). Un texte Yarn lit et écrit les drapeaux d'Alundra comme
  des variables, sans que le moteur ni Yarn ne gardent d'état à eux.
- **Contrat** :
  1. Un stockage de la DLL, `AlundraYarnVariableStorage`, implémente `Yarn.IVariableStorage` sur
     `AlundraGameState` et ne garde aucune donnée propre.
  2. **Noms** (D-E16-16, plage de D-E16-25) :
     - `$flag_n` est le drapeau d'id `n` de `GameFlags` ; `$tmp_flag_n` est le drapeau d'id
       `n | 0x8000`, dans `TemporaryFlags` ;
     - `n` est un décimal de 0 à 2047, écrit sans zéro de tête, pour qu'un drapeau n'ait qu'un seul
       nom.
  3. **Lecture** : la valeur est le booléen `(GetFlag(id) & (1 << (n & 0x1f))) != 0`, le test des
     opcodes `0x30` et `0x31` (`FlagBranch`, `AlundraEventProgramRunner.cs:1416`).
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
  - **T1 — Mesure du comportement de Yarn** ✅ (`AlundraYarnVariableStorageProbeTests`, 6 tests,
    aucun code de production ; confirme l'indice préalable). Sur un
    runner réel avec un stockage de test qui refuse tout, établir trois points :
    - ce que la machine virtuelle de Yarn 3.2.1 fait d'une lecture refusée (exception, valeur
      initiale du `Program`, autre chose) et d'une écriture refusée ;
    - quel type `T` elle demande à `TryGetValue` ;
    - si un dialogue sans `visited()` écrit des variables internes (`$Yarn.Internal.*`) ou appelle
      `Clear()`.

    Les résultats vont dans cette tranche, sourcés par les tests.

    **Résultats (2026-09-28, confirmés par T1 - `AlundraYarnVariableStorageProbeTests`, 6 tests
    dans `Alundra.Tests`)**, sur un vrai `YarnDialogueRunner`/`Yarn.Dialogue` (paquets 3.2.1 épinglés
    par le moteur) et un `IVariableStorage` de test qui refuse tout (`RecordingRefusingStorage`,
    `TryGetValue` rend toujours faux, `SetValue`/`Clear` sont enregistrés sans lever) :
    - `RefusedRead_FallsBackToProgramInitialValue_NoExceptionAndDialogueRunsToTheEnd` : une lecture
      refusée n'est pas directement fausse - la machine virtuelle retombe sur la valeur initiale
      déclarée du `Program` (ici `false`, la valeur implicite que le compilateur donne à `$mystery`,
      jamais `<<declare>>`é), sans exception, et le dialogue va au bout ;
    - `RefusedRead_OfADeclaredVariable_ReadsProgramInitialValue` : avec `<<declare $foo = true>>`,
      la même lecture refusée prend la branche VRAIE - la valeur vient bien de la valeur initiale
      déclarée du `Program`, jamais du stockage, qui continue de rendre faux sans lever ;
    - `UndeclaredVariable_GetsAnImplicitInitialValueInTheCompiledProgram` : compiler un script qui lit
      `$x` sans le déclarer donne un `Program` dont `InitialValues` contient `$x` - toute variable lue
      reçoit une déclaration implicite du compilateur (`AlundraYarnBindings.CreateDeclarations()`,
      comme la DLL) ;
    - `RefusedWrite_NoExceptionAndDialogueRunsToTheEnd` : une écriture refusée (`<<set $x to true>>`)
      n'écrit rien d'accepté par le stockage et ne lève pas non plus ;
    - `BooleanRead_RequestsIConvertible_NotBool` : `TryGetValue` est demandé avec `T = IConvertible`,
      jamais `bool`, pour lire une variable dans `<<if>>` ;
    - `PlainDialogueWithoutVisited_NeverCallsClearOrTouchesInternalVariables` : sur un dialogue avec
      `<<set>>` et `<<if>>`/`<<else>>` mais sans `visited()`, aucun `Clear()` et aucun nom lu ou écrit
      ne commence par `$Yarn.Internal.`.

    **Correction du 2026-09-28 (relecture de vérification, P2) :** le résultat 1 disait « une lecture
    refusée vaut faux, sans exception », ce qui confondait le refus du stockage avec la valeur
    initiale du `Program`. La décompilation de `VirtualMachine.cs` (cas `PushVariable`) montre que la
    machine virtuelle, quand `IVariableStorage.TryGetValue<IConvertible>` échoue, retombe sur
    `Program.InitialValues[nom]` et ne lève `InvalidOperationException("Variable storage returned a
    null value for variable ...")` que si ce nom en est aussi absent. Ce chemin d'exception est
    inatteignable pour un programme produit par le compilateur : toute variable lue reçoit une
    déclaration implicite (`UndeclaredVariable_GetsAnImplicitInitialValueInTheCompiledProgram`) -
    O-E16-7 n'est donc pas déclenché par une simple lecture refusée. Une lecture refusée d'une
    variable déclarée lit sa valeur initiale déclarée, journalisée par le stockage comme un nom
    refusé (`RefusedRead_OfADeclaredVariable_ReadsProgramInitialValue`).

    Confirmé aussi en écrivant `RecordingRefusingStorage` : `Yarn.IVariableAccess` déclare une
    propriété `Program` (type `Yarn.Program`) en plus de `SmartVariableEvaluator` - un stockage doit
    l'implémenter pour compiler contre l'interface, même s'il ne s'en sert pas (contrat item 8, T2).
    Ce point n'a pas été mesuré à l'exécution (quel appelant la pose, à quel moment) : sans effet sur
    E16.f, qui la garde telle que Yarn la pose.

    Le corpus exporté ne déclare, ne lit ni n'écrit aucune variable, et ses 24 784 nœuds n'ont que
    l'en-tête `title:` (aucun `tracking:`).
  - **T2 — Stockage et branchement** ✅ (`AlundraYarnVariableStorage` + branchement dans
    `AlundraDialogueDirector.AttachToWorld` ; `AlundraYarnVariableStorageTests` 33 cas de test (10 faits, 6 théories à 23 cas ; corrigé en T3, la note disait 34) couvrant
    lecture/écriture/bornes/refus/`Clear()`/directeur/cycle de vie/sauvegarde, et
    `AlundraYarnVariableCorpusTests` sur les 485 `.dialogue` exportés) : contrat 1 à 9, avec les tests
    d'acceptation. `TryGetValue<T>` rend la valeur d'un drapeau pour tout `T` auquel un booléen
    s'affecte (`bool`, `IConvertible`, `object`) ; pour un autre `T`, le refus est journalisé comme une
    valeur non booléenne.
  - **T3 — Documentation** ✅ (faite le 2026-09-28 en session principale : section « Variables » de
    `docs/formats/dialogues-yarn.md`, dont le repli de Yarn sur la valeur initiale et la limite du
    chemin dégradé) : section « Variables » de `docs/formats/dialogues-yarn.md` (noms, types,
    refus, cycle de vie, rien dans la sauvegarde).
- **Acceptation** : tests d'`Alundra.Tests` sur des Yarn de test compilés avec les déclarations de
  la DLL (`AlundraYarnBindings.CreateDeclarations`) :
  - **lecture** :
    - un bit posé par l'opcode `0x05` est lu vrai par `<<if $flag_n>>` ;
    - `$tmp_flag_n` lit la banque temporaire ;
    - le même `n` dans les deux banques donne deux drapeaux distincts ;
  - **écriture** :
    - `<<set $flag_n to true>>` est vu par l'opcode `0x30` ; `false` n'efface que ce bit ;
    - aux bornes `n` = 0, 31, 32 et 2047, dans les deux banques, l'état obtenu est identique à celui
      des opcodes `0x05` et `0x06` sur le même id ;
  - **refus** : `$flag_2048`, `$tmp_flag_2048`, `$flag_32768`, `$flag_07`, `$foo`, `$tmp_flag_x` et
    un nombre rangé sous `$flag_5` donnent une seule ligne de journal par nom, un état des deux banques
    identique à un instantané, et aucune exception levée par le stockage ;
  - **`Clear()`** : banques identiques ;
  - **directeur** : le runner qu'il crée utilise ce stockage ;
  - **cycle de vie** : à l'entrée de carte, un `$tmp_flag_n` posé par Yarn disparaît et un
    `$flag_n` reste (`InstallForMapEntry`) ;
  - **sauvegarde** : si E16.c est livrée, `$flag_0` et `$flag_2047` posés par Yarn font
    l'aller-retour, `$tmp_flag_0` et `$tmp_flag_2047` non. Sinon, E16.c ajoute ce test. E16.c est
    livrée : l'aller-retour passe par `AlundraSaveGame.Capture` puis `ApplyTo` sur un état neuf,
    sans le service (D-E16-31) ;
  - **non-régression** : `Alundra.Tests` sans échec, oracle de l'intro inchangé (`0x11` à la
    frame 1704) ;
  - **corpus** (relecture de plan du 2026-09-28, P2) : un test obligatoire, `AlundraYarnVariableCorpusTests`,
    prouve qu'aucun dialogue exporté ne consulte le stockage. Aucun test existant ne joue le corpus, et
    le runner du moteur n'avance pas seul les choix (`YarnDialogueRunner.cs:169-171`) : le test
    examine donc les programmes compilés, sans les jouer :
    - **fichiers** : chaque `.dialogue` exporté sous `alundra-project/`, les 485 ; le test échoue en
      nommant l'export s'il manque (convention d'`AlundraCellStoreProductionTests.cs:21-45`) ;
    - **chargement** : comme la DLL, par `DialogueTestAssets.LoadFromDisk`. Si le programme Yarn
      compilé n'est pas accessible depuis l'asset, le `.yarn` voisin est recompilé avec
      `AlundraYarnBindings.CreateDeclarations()`, comme `DialogueTestAssets.BuildRaw` ;
    - **prédicat**, pour chaque programme : aucune valeur initiale de variable, et aucune instruction
      qui lit ou écrit une variable, sur tous les nœuds. Les genres d'instruction de Yarn Spinner
      3.2.1 qui lisent ou écrivent une variable sont relevés par T1 et cités dans le test ;
    - **conséquence** : aucun dialogue exporté ne lit ni n'écrit de variable, Yarn ou interne. Le
      stockage n'est donc jamais consulté par le corpus, et l'arrêt « un dialogue exporté a besoin de
      variables internes » devient vérifiable. Que `Clear()` ne soit jamais appelé repose sur T1 et
      sur le runner, qui ne l'appelle pas (`YarnDialogueRunner.cs`, lu en entier).
- **Arrêts** :
  - T1 montre que la machine virtuelle lève une exception sur une lecture refusée, ou qu'un dialogue
    exporté a besoin de variables internes : la tranche s'arrête et la question va à l'auteur
    (O-E16-7) ;
  - Yarn exige du stockage un comportement que l'ADR-0010 ne couvre pas : question à l'auteur.
- **Dépendances** : E16.0 (suites de référence). Indépendante d'E16.a à E16.e.

---

## 2. Mesures

Mesuré le 2026-09-28 (E16.0), en lecture seule. Méthode :
- binaire (questions 3 et 4) : deux lectures indépendantes d'`ALUN_CD.EXE` (France, capstone) ; chaque
  désaccord entre elles a été tranché par un désassemblage de la session principale ;
- corpus et domaines (questions 1, 2 et 6) : un relevé, puis un contradicteur qui refait tout de zéro,
  puis un recomptage par la session principale pour la question 2 ;
- scripts et sorties dans le dossier temporaire de la session (`scratchpad/e16-0/`).

Étiquettes : [binaire], [décompilation], [corpus], [code DLL].

**Q1 — Ids de drapeaux utilisés** [corpus]

- **Lecture des programmes.** Les six tables A à F sont les premiers octets de `Codes`
  (`SpriteInfoEventCodes.cs:16-108` : le même décalage sert à lire les tables puis `Codes`). Une
  entrée plus petite que la taille cumulée des six tables ne désigne donc aucun programme (0 est la
  sentinelle courante). Les programmes sont parcourus en largeur depuis chaque entrée valide, avec les
  tailles d'`EventOpcodeSizeTable.cs`, sauf `0x78`, qui fait 3 octets (Q2). Toutes les issues d'un
  branchement sont suivies, et l'adresse qui suit un `0x78` aussi (le `0x7D` y revient).
- **Opcodes** : `GameFlags` a 1013 ids distincts, id max 2047 (mot 63) ; `TemporaryFlags` 469 ids,
  id max 2047 (mot 63).
- **`<<flag n>>` du Yarn** : 932 occurrences, 33 ids distincts, id max 1005 (mot 31), tous dans la
  banque temporaire, et tous déjà vus par les opcodes.
- **`ContentsGameFlag`** : propriété `_10` de la couche `Entities` des `.tmj`. Elle est présente sur
  9 741 enregistrements, avec 367 valeurs non nulles distinctes de 185 à 2000 (mot 62), toutes dans
  `GameFlags`, aucune invalidée par `EntityRecordMapper.cs:170-175`.
- **Union** : `GameFlags` 1357 ids, max 2047 ; `TemporaryFlags` 469 ids, max 2047. **Aucun id
  persistant ≥ 2048** : O-E16-3 est sans objet, et `GameFlags` s'écrit sur 64 mots (E16.c).
- **Limite, « non trouvé »** : un index de programme sans le bit `0x80` lit la table globale de
  `map_alundra` (`EntityEventHandlers.cs:420-445`) ; la DLL retombe alors sur la table de la carte
  (`AlundraEventProgramRunner.cs:287-294`). La zone de programmes de `map_alundra` est dégénérée à
  l'extraction (`Codes` de 10 octets, `EventCodesFSize = −10`), donc ses ids ne sont pas mesurés.
- **Contre-vérification** : mêmes ensembles d'ids et mêmes maxima. Deux écarts restent sans effet sur
  les ensembles : 7874 entrées valides contre 7419, et 1420 ids distincts pour `0x05` contre 1425.

**Q2 — `0x32`, `0x34`, `0x35` et la famille `0x78` à `0x81`** [corpus, binaire]

- **Occurrences** : adresses atteignables distinctes, sommées sur les cartes ; recomptage par la
  session principale avec le parcours de Q1.

  | Opcode | Occurrences | Cartes |
  |---|---|---|
  | `0x32` | 113 | 21 |
  | `0x34` | 8 | 3 |
  | `0x35` | 725 | 111 |
  | `0x78` | 1075 | 132 |
  | `0x79` | 9 | 2 |
  | `0x7A` | 0 | 0 |
  | `0x7B` | 43 | 3 |
  | `0x7C` | 4 | 2 |
  | `0x7D` | 317 | 130 |
  | `0x7E` | 58 | 30 |
  | `0x7F` | 10 | 4 |
  | `0x80` | 4 | 4 |
  | `0x81` | 6 | 1 |

- **Premier relevé rejeté.** Il démarrait aussi des programmes sur des entrées qui tombent dans les
  tables, et décodait donc des octets de table comme du code. Son exemple « Cave-145, décalage 2 » est
  dans les tables ; ses chiffres et ceux de son contradicteur, construit sur la même hypothèse, sont
  écartés.
- **Cohérence du recomptage** : avec `0x78` sur 3 octets, les rares impasses restantes (opcodes de
  taille 0, chemins qui sortent du flux) sont toutes sur des cartes à extraction dégénérée
  (`EventCodesFSize` négatif) ou à `Codes` minuscule. Avec 4 octets, on obtient 126 cibles invalides
  et de faux ids persistants jusqu'à 30726.
- **Enchaînement** : `0x78` (toujours), `0x79`/`0x7A` (selon `Result`) et `0x7B`/`0x7C` (selon un
  drapeau) écrivent `_34`, à `CodeIndex + 3` ou `+ 5`. `0x7D` (toujours), `0x7E`/`0x7F` et
  `0x80`/`0x81` y sautent. Le motif dominant est `0x78` … `0x7D` : un bloc de dialogue à choix, puis le
  retour après le `0x78`. Exemples vérifiés, octets et entrées de table :
  - `Ancient Shrine - Golem-34`, B[2] (entrée 68) : `78 5E 00` en 89, saut en 183, `0x7D` en 237 ;
  - `Lizardman's Lair (Boss)-411`, B[9] (entrée 1712) : `7B 7A 80 18 00` en 1713 (drapeau `0x807A`) ;
  - `Fairy cave (underwater)-160`, C[25] (entrée 1480) : `0x7C` en 1489, `0x7D` en 1597.
- **Présence** : `0x7B`, `0x7C`, `0x80` et `0x81` apparaissent tous, donc la famille entière entre
  dans E16.a ; `0x7A` n'apparaît pas.
- **Binaire** : les dix gestionnaires (`0x8003FB10`–`0x8003FE7C`) suivent la décompilation
  instruction par instruction ; `0x7B`/`0x7C` choisissent la banque entre `0x801EBA40` et `0x801EB344`.
- **Désaccord** [binaire] : `0x78` fait **3 octets**. Il ne lit que les octets +1 et +2, puis écrit
  `_34 = CodeIndex + 3` (`0x8003FB10`). `EventOpcodeSizeTable.cs:151`, recopié de la table
  `EventCodeDebugger.cs` de l'analyseur, dit 4. Aujourd'hui, la DLL saute les opcodes non portés par
  leur taille : elle saute donc un `0x78` de 4 octets.

**Q3 — `g_saveData`, `g_temporaryFlags`, temps de jeu** [binaire]

- **`g_saveData`** (`0x801EB2E8`, **0x758 octets**) :

  | Décalage | Champ | Taille |
  |---|---|---|
  | `+0x000` | `SlotData` | 4 |
  | `+0x004` | `LastMapId` | 4 |
  | `+0x008` | `CurrentFlagName` | 32 |
  | `+0x028` | `GameStateDescription` | 32 |
  | `+0x048` | `GameTime` | 4 |
  | `+0x04C` | `InitialMapId` | 4 |
  | `+0x050`, `+0x054`, `+0x058` | `CameraTileX`, `CameraTileY`, `CameraTileZ` | 4 chacun |
  | `+0x05C` | `GameFlags` | 64 × 4 |
  | `+0x15C` | `MapIdToInternalMapIndexTable` | 500 × 2 |
  | `+0x544` | `PlayerStats` : `Hp`, `HpMax`, `Mp`, `MpMax`, `MoneyAmount`, `WeaponId`, `ItemId`, `FalconTemp`, `Falcon` | 9 × 2 |
  | `+0x556` | `NumberOfItems` | 256 × 2 |
  | `+0x756` | `SaveSlotIndex` | 1 |
  | `+0x757` | `Field_757` | 1 |

  Sources dans le binaire :
  - la taille littérale `0x758` passée par `UpdateSavedData` (`0x80031588`) ;
  - les boucles `0x800814A4`–`0x800814DC` (64 mots de drapeaux, 500 entrées de table) ;
  - les deux copies de 0x758 octets (`0x800814E8`, `0x8008153C`) ;
  - `g_temporaryFlags`, qui commence juste après, à `0x801EBA40`.
- **Désaccords** :
  - le `short Offset` final de `SaveData.cs` n'existe pas dans la structure : c'est un champ du seul
    port (`MemoryCardManager.cs:226`) ;
  - `UpdateSavedData` n'a pas le paramètre `displayMenu` : son appel à `InitializeSaveDataCopy` est
    inconditionnel (`0x80031594`).
- **Champs sans lecteur** :
  - `LastMapId` est seulement écrit (−1 en nouvelle partie, `0x8003185C`) et recopié avec le reste de
    la structure ;
  - `Field_757` n'a aucune référence.

  E16.c ne les écrit donc pas, ni `Offset`.
- **`g_temporaryFlags`** : 64 mots à `0x801EBA40`. `ClearTemporaryFlags` (`0x8008159C`) en vide
  64, comme `GameEngine.cs:429-438` ; la DLL en vide 1024, sans effet puisque le mot max est 63 (Q1).
- **Temps de jeu.** `g_gameplayTime` est à `0x8013FB4C`. La fonction de fin d'image (`0x80042798`)
  attend un VSync (plus `n − 1` si `n > 1`), puis ajoute 1 au compteur, plafonné à `0x14996C4`. Elle
  est appelée une fois par tour de la boucle de jeu (`0x8002C3F4`–`0x8002C45C`) et dans les boucles
  de transition (`0x8002C4B0`, puis `0x8004288C` en `0x8002C4C8`). Le compteur avance donc **d'une
  unité par image affichée**. La cadence réelle en PAL (50 Hz si le jeu tient un VSync par image)
  n'est pas mesurée.
- **Affichage** : `UpdateMenuStatusText` (`0x800311D4`–`0x80031328`) calcule les heures par
  `t / 216000` (`0x9B583739`, décalage 17), les minutes par `t / 3600` (`0x91A2B3C5`) et les secondes
  par `t / 60` (`0x88888889`). **Le compteur s'affiche en soixantièmes de seconde**, et
  `0x14996C4` = 99:59:59. **Désaccord** : `GameEngine.cs:2724` le lit comme des secondes, la division
  par 60 est perdue.

**Q4 — Sauvegarde, rechargement après la mort, `g_saveDataInRam`, `SaveSlotIndex`** [binaire]

- **Chaîne de sauvegarde, confirmée** :
  - le gestionnaire du livre est l'entrée 72 de la table `ProgramCTick` (`0x800C4F34`, mot
    `0x800C5054` = `0x8007B998`, `SpriteEventHandlers.cs:122`) ;
  - à l'état 5 (`0x8007BAD8`), il appelle `UpdateSavedData`, qui pose `InitialMapId`,
    `CameraTileX/Y/Z`, le texte (`UpdateMenuStatusText`) et `GameTime`, puis appelle
    `InitializeSaveDataCopy(g_saveData, 0x758, 1)` ;
  - `InitializeSaveDataCopy` pose `g_globalTransitionState = 10000` et `g_postProcessingState = 1` ;
  - l'état 6 attend `g_globalTransitionState == 0`, puis efface le bit 4 de `g_playerControlFlags`.
- **Livre de sauvegarde** : une seule entité, « SaveBook (Ne pas toucher !) », type de sprite 237,
  posée sur 65 cartes : 17, 48, 52, 53, 54, 140, 147, 158, 159, 160, 163, 170, 177, 184, 187, 194,
  199, 206, 213, 220, 227, 231, 238, 242, 249, 253, 260, 267, 272, 276, 283, 290, 294, 298, 302,
  333, 358, 365, 372, 381, 394, 424, 439, 443, 447, 448, 452 à 470.
- **Second appelant d'`UpdateSavedData`** (`0x8002ADA4`) : il est dans une fonction non décompilée,
  qui pose aussi des bits de `g_debugFlags` (`0x800DC05C`) selon la manette. C'est probablement une
  fonction de débogage (non établi).
- **Rechargement après la mort, confirmé par les deux lectures** :
  - sur la carte `0x1DD` (« Continue Screen-477 »), `Script_187_0BB` renvoie au menu principal si
    le bit 2 du mot 0 de `g_temporaryFlags` est posé (effet `0xB`) ; sinon, après 60 images, il pose
    l'effet 10 ;
  - l'effet 10 (`0x8002C590`) appelle `LoadBgm(0)`, met `g_playerControlFlags` à 0, puis appelle
    `InitializeMapWarpPosition` (`0x800315B0`) ;
  - celle-ci incrémente `SaveSlotIndex` dans `g_saveDataInRam` (plafonné à `0xFF`), appelle
    `InitializePlayerStatsAndItems`, puis copie les 0x758 octets de `g_saveDataInRam` dans
    `g_saveData` (`UpdateSaveData`, `0x800814E8`), et repart de `InitialMapId`/`CameraTile*` ;
  - `g_textCategoryIndex` et `INT_ARRAY_80191908` ne sont pas touchés : leurs 20 références sont
    dans le code du texte (`0x80046400`–`0x800476C0`) et dans les programmes de mini-jeu qui écrivent
    `INT_ARRAY_80191908` (`0x800643E0`–`0x80064E60`). `TemporaryFlags` est hors de la copie.
- **`g_saveDataInRam`** (`0x80010000`, pointeur en `0x80029BC4`) n'est pas figé :
  - `InitializeGameState` y copie `g_saveData` au démarrage (`0x8008153C`, appelé en `0x8003189C`) ;
  - chaque sauvegarde réussie y recopie le bloc sauvegardé (`0x8006163C`–`0x800616A8`, 0x76C
    octets, soit 0x14 de plus que la structure) ;
  - une fonction non décompilée y copie un enregistrement d'emplacement de carte mémoire (4 × 0x76C
    octets, `0x8005F2B8`–`0x8005F348`), probablement la lecture d'un emplacement (conditions non
    établies) ;
  - « Réessayer » repart donc de la dernière sauvegarde réussie de la session, ou de l'état du
    démarrage.
- **`SaveSlotIndex`** (`+0x756`, 1 octet) **compte les reprises après la mort** :
  - il est incrémenté seulement par `InitializeMapWarpPosition`, plafonné à `0xFF`, et remis à 0 en
    nouvelle partie (`0x80031860`) ; il est sauvegardé avec le reste ;
  - il est lu par `Script_187_0BB`, pour le journal de débogage « Retry = » ;
  - il est lu par l'**opcode `0xC2`** (`0x80041D34`), qui pose `Result = 0` si `SaveSlotIndex` est
    inférieur à un octet de paramètre, 1 sinon. `0xC2` lit la copie de `g_saveData` (`0x801EBA3E`),
    pas celle de `g_saveDataInRam` ; le rechargement recopie l'une dans l'autre juste après
    l'incrément. Le corpus a un seul `0xC2`, sur Overworld 1,2-7
    (décalage 1508, seuil 20).
- **Désaccords** :
  - `0xC2` ne lit qu'un octet de paramètre, alors que `EntityEventHandlers.cs:3630-3652` en combine
    deux ;
  - `Script_187_0BB` calcule la valeur « Retry » sans condition (sans effet) ;
  - selon une seule lecture, non recontrôlée : `UpdateMemoryCardProcess` renvoie la valeur de
    `StartMemoryCardProcess` dans les états 1 et 2, là où la décompilation force 1.

**Q5 — Suites de référence** : parent `26183ed`, moteur `793d1ee8` : `CasaEngine.Tests` 1991/1991,
tests du convertisseur 400/400, `Alundra.Tests` 1361/1361.

**Q6 — Domaines des champs restitués**

- **`NumberOfItems[id × 2 + 1]`** [binaire, code DLL] : de 0 à `ItemsProperties[id × 5 + 3]`, colonne
  « nombre maximal » de `Data/items-properties.json`. La DLL la charge déjà (`AlundraItemTables.cs:132-134`)
  et l'applique dans `AddOneItemIfUnlocked`. Ses 500 valeurs sont identiques à `g_itemsProperties`
  (`0x800B9FE8`).
- **`Falcon` et `FalconTemp`** [binaire] : `short`, de 0 à 50. `IncreaseFalcon2` (`0x8004E6EC`)
  plafonne `FalconTemp` à `0x32`, `UpdateNumberOfFalcon` (`0x8004E738`) plafonne `Falcon` au même
  seuil. Ce sont leurs seuls écrivains ; aucune instruction ne pose de plancher.
- **Dimensions de chaque carte** [corpus] : `tilemap/<nom>.tileMap` (`map_size`) et `.tmj`
  (`width`, `height`). Leur chemin se déduit de `world-index.json` comme le fait
  `MapEventProgramLoader` (`EventProgramDocument.cs:107-117`). Les 483 cartes en ont, avec des valeurs
  identiques à `Map.Width/Height` de `data-extracted`.
- **`CameraTileZ`** [binaire, corpus] : `CameraTileZ` vaut `PosZ >> 20`. Au sol, `PosZ` vaut la
  hauteur de la tuile décalée de 20 bits (`ComputeEntityGroundHeight`, `0x800370C4`, octet `+3` de
  la tuile). La hauteur est un octet de 0 à 255 ; une pente peut ajouter 1 (non vérifié dans le
  binaire). Valeurs observées :
  - 55 sur une tuile de sol réelle (carte 329) ;
  - 60 sur une tuile quelconque (carte 160) ;
  - `ZLevel` des portails : 34 au plus.

  `Z << 20` déborde à partir de 2048. Qu'un héros puisse se tenir sur la tuile de hauteur 55 n'est pas
  établi.

**Conséquences pour les tranches suivantes** (questions au §3, avant E16.a et E16.c) :
- E16.a : la famille `0x78` à `0x81` entre entière, avec `0x78` sur 3 octets (O-E16-8) ;
- E16.c : `SaveSlotIndex` est un état de jeu lu par un script (O-E16-9) ; l'unité du temps de jeu
  est à trancher (O-E16-10) ;
- E16.c : les domaines de `NumberOfItems`, `Falcon`, `FalconTemp` et `CameraTileZ` sont connus ;
- E16.c ne sérialise ni `LastMapId`, ni `Field_757`, ni `Offset`, et garde `GameFlags` sur 64 mots ;
- E18 : des désaccords de décompilation sont à ranger (O-E16-11).

---

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E16-1 | ~~Charger au démarrage~~ — **tranché : plus tard** (D-E16-10). Conséquence connue : un jeu livré n'a aucun moyen de charger avant un écran titre. | E16.d |
| O-E16-2 | ~~Touches de recette~~ — **tranché** (D-E16-11). | E16.d |
| O-E16-3 | **Sans objet** : E16.0 ne trouve aucun id persistant ≥ 2048 (§2, Q1). Question d'origine : seulement si E16.0 trouve un id persistant ≥ 2048 : l'original écrirait au-delà de `GameFlags`, dans `MapIdToInternalMapIndexTable`. Reproduire, ou corriger (règle « corriger les défauts de l'original ») ? | E16.c |
| O-E16-4 | ~~§9.9 du moteur~~ — **tranché** (D-E16-8). | E16.b |
| O-E16-5 | ~~Qui porte le gestionnaire du livre de sauvegarde ?~~ — **tranché : E16.e** (D-E16-12). | E16.e |
| O-E16-6 | ~~Touches de recette~~ — **tranché** (D-E16-33) : actives quand `Alundra.dll` est compilée en Debug. Question d'origine : **Comment activer les touches de recette ?** Une variable d'environnement (`ALUNDRA_SAVE_DEBUG=1`) risque le même sort que `ALUNDRA_HUD_DEBUG`, qui n'a jamais atteint le processus du lanceur (D-E13-12). Pistes à comparer en E16.d, sur ce que le lanceur transmet vraiment au jeu : un argument de ligne de commande du lanceur, un réglage du projet, un fichier de configuration à côté du jeu. À trancher par l'auteur **avant E16.d**. | E16.d |
| O-E16-7 | Seulement si E16.f T1 montre que la machine virtuelle de Yarn lève une exception sur une lecture refusée, ou qu'un dialogue exporté a besoin de variables internes de Yarn : erreur visible (dialogue interrompu), valeur initiale du `Program` avec journal, ou autre ? | E16.f |
| O-E16-8 | ~~Taille de `0x78`~~ — **tranché** (D-E16-21) : 3 octets, dans la DLL et dans l'analyseur. | E16.a |
| O-E16-9 | ~~`SaveSlotIndex`~~ — **tranché** (D-E16-22) : sauvegardé et restitué, 0..255. | E16.c |
| O-E16-10 | ~~Unité du temps de jeu~~ — **tranché** (D-E16-23) : 60 unités par seconde réelle, affichage divisé par 60. | E16.c |
| O-E16-11 | ~~Désaccords de décompilation~~ — **tranché** (D-E16-24) : tous corrigés en E18. | E18 |
| O-E16-12 | Seulement si E16.a T3.1 ne retrouve pas, dans le binaire, que la hauteur de terrain (`TerrainHeight`, `+0x138`) d'une entité que `0x8D` teste (joueur, PNJ avec ou sans contrôleur, dès l'apparition) vaut celle que la sonde de la DLL (`ComputeTerrainHeight`) peut calculer, au même moment de l'image : quelle source prendre pour `0x8D` ? | E16.a |
| O-E16-13 | ~~tranché~~ (D-E16-34, écrans de carte mémoire reproduits, adaptés aux fichiers). Question d'origine : **Aspect de l'écran de sauvegarde** : écrans de carte mémoire de l'original (blocs, icônes, messages de carte absente, pleine ou abîmée), ou flux simplifié qui garde les temps visibles (message du livre, oui/non, choix de l'emplacement, écrasement, réussite ou échec) sans les états propres au matériel PS1 ? | E16.e |
| O-E16-14 | ~~tranché~~ (D-E16-35, 4 emplacements fixes). Question d'origine : **Emplacements** : 4 fixes comme une carte mémoire, un seul implicite, ou une liste libre ? | E16.e |
| O-E16-15 | ~~tranché~~ (D-E16-36, chapitre et résumé recalculés depuis la sauvegarde validée). Question d'origine : **Libellé d'un emplacement** : chapitre et résumé recalculés depuis la sauvegarde validée, date d'écriture, numéro, ou une combinaison ? | E16.e |
| O-E16-16 | ~~tranché~~ (D-E16-37, textes utiles seulement ; les 8 absents vérifiés dans `ETC_RES.R`). Question d'origine : **Textes** : quels messages ETC de l'original garder, et que faire des 8 textes absents de l'export et des chaînes japonaises en dur ? | E16.e |

## 4. Hors périmètre

- Écran titre et choix d'une sauvegarde au démarrage (D-E16-10 : plus tard).
- Relecture des vraies sauvegardes de carte mémoire PS1 (non retenue le 2026-09-27).
- Pont des drapeaux vers les cinématiques : E17 (mise à jour de D-E16-6 du 2026-09-28). Le pont
  vers Yarn est dans E16 (E16.f).
- Variables Yarn autres que les drapeaux (`visited()`, variables déclarées ou calculées) : refusées
  (D-E16-18).
- Le rechargement après la mort (« Réessayer », opcode `0xBB`, effet 10) et la correction de la
  décompilation C# de ce chemin : étape E18 du plan maître (D-E16-20). E16.0 en confirme seulement
  les faits.
- Lecteurs de `ContentsGameFlag` de l'IA native (coffres, `FunctionTypeA.cs:236-264`) : E14.
- Noms lisibles pour les drapeaux, au-delà des 41 drapeaux de chapitre.
- **Risques résiduels acceptés** (jeu solo) : le sens des drapeaux ne peut pas être validé, donc une
  sauvegarde éditée peut casser la suite de l'histoire, bloquer le joueur, ou même figer
  l'interpréteur de scripts, qui n'a pas de budget de boucle (SC10, hypothèse sans script connu) ; une
  tuile dans les bornes mais dans un mur est acceptée ; une valeur de la table des cartes qui est une
  clé de `world-index.json` n'est pas contrôlée contre le catalogue, comme pour les opcodes `0x38` et
  `0x53` (SC12).

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
| Table `EventCodeDebugger.cs` de l'analyseur et pointeur du sous-module analyseur | E16.a | Revenir au pointeur de `main` du parent ; la branche de l'analyseur est gardée. Le convertisseur n'utilise pas cette table, donc l'export n'est pas touché. |
| Fichiers de sauvegarde écrits par la recette | E16.d | Hors du dépôt, dans le dossier de l'utilisateur ; supprimés par l'auteur s'il le souhaite. |

### 5.3 Arrêts communs

- Une mesure ou un fait qui contredit une décision D-E16 : arrêt, question à l'auteur, plan corrigé et
  relu.
- Un test existant qui devrait changer pour une autre raison que la tranche en cours.
- Une modification de l'auteur indexée par erreur ; un commit sur `main` ; un push.
- Un contrôle qui ne peut s'exécuter qu'après avoir modifié l'état vivant.
- Une exception qui s'échappe d'une capture ou d'une restitution.
- La DLL qui lit ou écrit un fichier de sauvegarde sans passer par le service.
- Un cas de refus couvert dans un seul des deux formats. Depuis D-E16-31, les refus d'E16.c portent
  sur l'objet décodé et ne dépendent pas du format ; la couverture par format repose sur les tests du
  moteur et sur la recette d'E16.d, qui essaie un emplacement hostile dans chaque format (SC3).

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
| S8 | P3 | `SaveSlotIndex`, `LastMapId` et `GameTime` repris du fichier | FIX | E16.c champs et tableau ; pour `SaveSlotIndex`, remplacé par D-E16-22 après E16.0 (c'est un compteur de reprises, pas un numéro d'emplacement) |
| S9 | P3 | Nom de fichier invalide dans le dossier et « emplacement le plus récent » | FIX (le plan moteur filtre la liste par la même règle) | E16.d tests |
| S10 | P3 | Pas de revue de sécurité ni de verifier par tranche | FIX | Tableau des unités en tête ; §5.3 |
| S11 | P4 | Risques résiduels à écrire | FIX (texte) | §4 |

Relevé en passant, hors de ce plan : la touche F1 de la recette de la jauge n'avait aucune garde
(`AlundraWorldProxy.cs:2197`, `:1369-1394` à `cbda4f8`), contrairement à la recette par variable
d'environnement. L'auteur a demandé sa suppression le 2026-09-27, avec celle de la recette par variable (D-E16-13, chantier séparé).
