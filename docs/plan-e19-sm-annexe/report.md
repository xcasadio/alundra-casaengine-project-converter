# script2 — Couverture par opcode de l'interpréteur de programmes d'événements

Lecture seule sur `D:/development/repo/alundra-casaengine-project-converter` (branche `chantier/script-types-full-name`, 2026-10-17). Le binaire `ALUN_CD.EXE` (France) a été relu en lecture seule avec `capstone`. Étiquettes : FAIT (code, binaire ou script), INFÉRENCE, INCONNU.

**`report.md` n'a pas pu être écrit.** L'outil d'écriture a refusé les fichiers de rapport. Le rapport est donc ce message. `opcodes.tsv` a bien été écrit.

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/themes/script2/`. Les scripts se relancent avec `python -B`.
- `opcodes.tsv` : 256 lignes sur 28 colonnes.
  - taille de la table et concordance avec le binaire ;
  - adresse du gestionnaire binaire ;
  - état dans la DLL et `fichier:ligne` du gestionnaire ;
  - citation du binaire dans le commentaire de la DLL ;
  - tests, statut dans le plan E19 et points ouverts ;
  - comptes du corpus : sites, cartes, créneaux, chaîne 30, cœur 8, combat 7.
- `skipped-sites.tsv` : les 510 sites sautés atteignables (opérandes et instruction suivante).
- `census-sites.tsv` : les 110 140 instructions atteignables.
- `census-operands.tsv`, `census-op0x40.tsv`, `cycles.tsv`, `size-check.tsv`, `result-check.tsv`, `handler-addresses.tsv`, `decomp-unported.txt`, `tables.txt`.
- Scripts : `census.py` (port de `AlundraStoryChainOpcodeAudit.cs:128-338`), `aggregate.py`, `testscan.py`, `instrscan.py`, `planscan.py`, `handlerdoc.py`, `sizecheck.py`, `resultcheck.py`, `cycles.py`, `skipped_sites.py`, `variants.py`, `build_tsv.py`, `tables.py`.

## 1. Résumé

- **FAIT** : `Dispatch` (`Alundra/Scripts/AlundraEventProgramRunner.cs:613-2154`) a 158 `case`. `0x00` et `0xFF` sont traités dans la boucle d'appel (`:459-466`, `:452-457`).
- **FAIT** : `EventOpcodeSizeTable.cs:40-246` a 198 entrées (le commentaire `:10` dit 197). Les octets 0xC5-0xFE (58) n'y sont pas.
- **FAIT** : l'octet inconnu passe par `default` (`:2152`), puis `UnknownOpcode` (`:3115-3141`). Il est sauté par sa taille. Sans taille ou avec une taille ≤ 0, il rend 0 et l'appel s'arrête. `:478-479` n'est que la recherche de taille du `TraceSink`.
- **FAIT** : corpus de 483 cartes, 110 140 instructions atteignables, 171 valeurs d'opcode distinctes.

| État DLL | Opcodes (sur 256) | Instructions du corpus | Part |
|---|---|---|---|
| FAITHFUL | 150 | 92 967 | 84,41 % |
| PARTIAL | 5 | 973 | 0,88 % |
| NOOP_DELIBERATE | 3 | 138 | 0,13 % |
| LOOP_BUILTIN (0x00, 0xFF) | 2 | 15 552 | 14,12 % |
| SKIPPED_BY_SIZE | 29 | 510 | 0,46 % |
| TERMINATES_SIZE0 | 9 | 0 | 0 % |
| NOT_IN_TABLE | 58 | 0 | 0 % |

- FAITHFUL : `02-0D 10 11 12 16 17 19-1F 20-26 27-2E 30-3B 3E 3F 40-52 54 55 57-5E 62-65 67-69 6E 6F 70 71 73 74 75 78-81 83-87 89-95 98-9C 9F A0-A3 A5-A7 AB AD AF B0 B1 B8 BC-BF C0 C1 C3 C4`. Liste exacte dans `opcodes.tsv`.
- PARTIAL : `2F 53 82 A4 AC`.
- NOOP_DELIBERATE : `01 A8 BA`.
- SKIPPED_BY_SIZE : `15 3C 3D 56 5F 60 61 66 6A 6B 6C 6D 72 7A 88 96 97 9E A9 AA B2 B3 B4 B5 B6 B7 B9 BB C2`.
- TERMINATES_SIZE0 : `0E 0F 13 14 18 76 77 9D AE`.
- Portés mais jamais atteints dans le corpus : `01 12 48 4E 4F 71 75 AB B0 BC`.

Faits qui comptent pour le plan :
- **FAIT** : les 29 sautés n'ont aucun site sur les 30 cartes de la chaîne ni sur les 8 cartes centrales (389, 390, 476, 478, 392, 391, 416, 163). Un seul (`0xB9`) est sur une des 7 cartes de combat.
- **FAIT** : 21 de ces 29 sont atteints : 510 instructions dans 96 cartes. Les 8 autres (`15 3C 5F 66 7A 9E B4 B7`) ont 0 site.
- **FAIT** : la liste fermée de la chaîne est vide (`Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`, en-tête seul).
- **FAIT** : 157 des 158 opcodes portés ont un test qui les nomme. `0x30` (3 020 sites) n'en a aucun en propre. Il est couvert par `FlagBranch`, partagé avec `0x31` (`AlundraEventProgramRunnerTests.cs:404-433`), par la garde du marin 12 (`AlundraDialogueOpcodesProductionTests.cs:17-31`) et par `AlundraYarnVariableStorageTests.cs:120`.
- **Réutilisation** : mes comptes reproduisent 41 sur 41 de ceux du tableau G7 de `docs/plan-e19-m-annexe/backlog-2026-10-03.md:168-180` (`0xA2` 455, `0x60` 172, `0x20` 266, etc.).
- **INCONNU** : le chiffre « 111 136 instructions, 14 644 sautées, 88 opcodes, 392 cartes » (`plan-e19-opcodes.md` §0.2.1, `plan-conversion-totale.md:944+`) est périmé. Seize variantes de marche donnent 105 154 à 110 195 (`variants.py`), aucune ne donne 111 136.

## 2. Structure demandée

| Fichier | Contenu (FAIT) |
|---|---|
| `EventOpcodeSizeTable.cs` | `Entries` : opcode → (taille, libellé). Corrections issues du binaire : `0x78` = 3, `0x5F` = 8, `0xB8` « CurrentAnimationId », `0x35`/`0x36` remis dans le bon sens. |
| `AlundraEventProgramRunner.cs` (3512 l.) | `RunScript` `:213-296`, `InitializeEventData` `:385-419`, `RunOneScriptCall` `:428-518`, `FillDataFromCommand` `:564-588`, `Dispatch`, `UnknownOpcode`, `RunSpriteEvent` `:314-374`, garde de boucle `ProductionLoopBudget = 1024` `:137`. |
| `EventProgramRunner.cs` (45 l.) | Interface `IEventProgramRunner` et `NoOpEventProgramRunner` (compteur seul). C'est la valeur par défaut avant le monde (`AlundraWorldProxy.cs:299`), remplacée en `:874-876`. |
| `EventProgramState.cs` (57 l.) | `Sp`, `Parameters[10]` (marque d'attente en `[1]`, mémos en `[2..9]`), `Result`, `_30` (boucle `0x73`/`0x74`), `_34` (retour de la famille `0x78`-`0x81`), `Codes`, `CodeIndex`, `CopyFrom` (réservé à `0x66`, non porté). |
| `AlundraHitOpcodes.cs` (79 l.) | Fonctions pures de `0x87` (touché par un type d'attaque, table de 12 octets `0x80098FA4`) et de `0x95` (boîte d'attaque stockée dans un rectangle). Appelées en `Dispatch :849-875`. |
| `EventProgramDocument.cs` | Tables A-F et `Codes`. `MapEventProgramLoader` lit `Maps/world-index.json` puis `…/events/<nom>.events.json`. |

Binaire (FAIT, `RunScript` en `0x8004205C`) :
- `0x800422B4` : `0xFF` termine. `0x800422BC` : `0x00` repart par `0x800421F4`.
- Sinon appel du gestionnaire à `0x80098FAC + 4*op`. Retour 0 : fin d'appel. Sinon `state+8 := 0` (`0x8004232C`), soit le `Parameters[1] := 0` du DLL (`:515`).
- Le gestionnaire par défaut `0x8003D158` sert aux octets `00 01 0E 0F 13 18` et `C5-FE`. Il affiche « Data Logic Error! » et rend 0. `UnknownOpcode` est donc équivalent, sauf `0x01`, que le DLL traite en no-op rendant 1 (M-03, 0 site).
- `0x14`, `0x76`, `0x77` ont chacun un stub propre qui rend 0. `0x9D` et `0xAE` affichent puis appellent `0x800831E0(1)`, un exit probable (INFÉRENCE).
- Contrôles croisés (INFÉRENCE, heuristiques) :
  - la taille de la table est parmi les constantes `$v0` du gestionnaire pour 174 entrées sur 197 ;
  - les 14 autres sont des sauts ou des attentes, plus `0x00`/`0x01` (stub par défaut) ; `0x5F` = 8 et `0x78` = 3 sont déjà connus ;
  - les 28 sautés autres que `0x5F` concordent ;
  - l'écriture de `Result` concorde pour 153 gestionnaires sur 158. Les 5 écarts sont des faux positifs : `0x04`, `0x4B`, `0x7F` lisent `Result` ; `0xAF` et `0xC4` écrivent `+0x2C` d'une autre structure.

## 3. Opcodes sautés par la taille, classés par usage

« Sites » = instructions atteignables, avec les deux branches de chaque saut. « Conso. » = instructions suivies d'un lecteur de `Result` (`03 04 4A 4B 79 7E 7F`). Sauter un tel opcode laisse un `Result` périmé et change le flot. Chaîne 30, cœur 8 et combat 7 : 0 partout, sauf `0xB9` (1 site en combat).

| Opcode | Libellé de la table | Sites | Cartes | Créneaux | Conso. | Cartes principales |
|---|---|---|---|---|---|---|
| 0x60 | Is riding entity | 172 | 7 | C | 172/172 | 400 ×128 ; 27 ×24 ; 29 ×8 ; 30 ×4 |
| 0x6A | Save entity position | 59 | 6 | C | – | 344 ×20 ; 60 ×12 ; 59 ×10 ; 442 ×8 |
| 0x96 | Restore HP | 42 | 36 | B 31, C 11 | – | 84, 411, 29, 31… |
| 0xB9 | Start CD streaming | 42 | 26 | B 20, C 22 | – | 134 ×8 ; 398 ×3 |
| 0x6B | If entity x < threshold | 29 | 5 | C | 29/29 | 344 ×10 ; 442 ×8 ; 55 ×5 |
| 0x6D | If entity z < threshold | 29 | 5 | C | 29/29 | 59 ×10 ; 60 ×8 ; 55 ×5 |
| 0x97 | Spend money | 24 | 24 | B 23, C 1 | – | 147, 327, 379, 394, 452-455… |
| 0xAA | (vide) | 24 | 8 | C | 24/24 | 424 ×8 ; 426 ×5 ; 421 ×3 ; 440 ×3 |
| 0x72 | Set LastTargetAnim and Direction | 20 | 12 | D 10, F 10 | – | 40 ×4 ; 140, 160, 394 |
| 0x56 | Change area tiles | 17 | 2 | B | – | 21 ×16 ; 60 ×1 |
| 0x61 | Is riding entity 2 | 14 | 4 | B 8, C 6 | 7/14 | 398 ×7 ; 77 ×5 |
| 0x6C | If entity y < threshold | 12 | 2 | C | 12/12 | 344 ×10 ; 60 ×2 |
| 0xB2 | Compare entity groups | 10 | 2 | B | 10/10 | 258 ×5 ; 298 ×5 |
| 0xA9 | (vide) | 4 | 1 | B 1, C 3 | 4/4 | 440 |
| 0x88 | (vide) | 3 | 2 | E | – | 306 ×2 ; 442 |
| 0xB5 | Check ForceZ >= 0 | 3 | 2 | B 2, C 1 | 3/3 | 54 ×2 ; 440 |
| 0xBB | Check retry or title | 2 | 2 | B | – | 347, 477 |
| 0x3D | If entity in zone | 1 | 1 | B | – | 226 |
| 0xB3 | Set pad buttons | 1 | 1 | B | – | 161 |
| 0xB6 | Check ForceZ != 0 | 1 | 1 | C | 1/1 | 160 |
| 0xC2 | Check something save | 1 | 1 | B | 1/1 | 7 |
| 0x15 0x3C 0x5F 0x66 0x7A 0x9E 0xB4 0xB7 | | 0 | 0 | | | |

Sémantique tirée de la décompilation (corps dans `decomp-unported.txt`). Elle perd des gardes ; seuls `0xB4`-`0xB6` sont relus au binaire.
- Prédicats sur `Result` :
  - `0x60` : une entité trouvée par `v1` a `RidingEntity` égal à l'entité logique ; `0x61` est l'inverse.
  - `0x3C`/`0x3D` : une entité dans une boîte de cases.
  - `0x6B`/`0x6C`/`0x6D` : distance parcourue depuis `0x6A` supérieure ou égale au seuil, en X, Y ou Z.
  - `0xA9`/`0xAA` : `XCollisionEntity` ; `0xB2` : groupes de recherche communs.
  - `0xB4`-`0xB6` (FAIT, binaire `0x800417CC`, `0x80041830`, `0x80041894`) : `ForceZ > 0`, `ForceZ < 0`, `ForceZ == 0`. Les libellés de la table (`EventOpcodeSizeTable.cs:228-230`) sont faux.
  - `0xB7`, `0x9E`, `0xC2`.
- Actions :
  - `0x6A` mémorise la position dans `Parameters[2..4]`.
  - `0x72` : `LastTarget := Target`.
  - `0x88` : statut := table `0x80023D2C`.
  - `0x96` : PV augmentés puis plafonnés. La décompilation réutilise `v1` comme type et comme montant : suspect.
  - `0x97` : argent diminué. `0xB3` : manette virtuelle.
  - `0x56` : copie d'un rectangle par `MapCopies[v1]`, table non exportée (D-E7-2).
- À part : `0xB9` (piste XA), `0xBB` (réessayer, E18), `0x5F` (attente animation ou distance, taille 8, 0 site).

Plan E19 :
- 19 des 21 sautés atteints sont dans le groupe « 22 sans propriétaire » (M-05, `plan-e19-opcodes.md` §1.2s lignes 17-21). `0xC0`, `0xC1`, `0xC3` en sont sortis, portés par E20.
- `0xB9` relève de l'auteur (O-E19-36, Q-A1) ; `0xBB` d'E18.
- La question 5 du tri du 2026-10-08 (un propriétaire par famille / tout à E14 / « à placer ») n'a pas de réponse dans le plan.
- Le test statique du corpus entier (G7/M-05) n'existe pas ; seul celui de la chaîne existe.

## 4. Partiels et no-op délibérés

| Opcode | Sites (cœur 8 / chaîne 30) | Manque (FAIT) | Source |
|---|---|---|---|
| 0xA4 | 21 (0 / 0) | Cycle de palettes si `v2 > 0` non porté : 1 site, carte 471. | `Runner.cs:1611-1624` ; O-E19-43, « plus tard, moteur » |
| 0xAC | 32 (7 / 8 : cartes 135, 389, 478) | Écrit les bits de taille d'ombre. Aucun rendu d'ombre dans la DLL ni le moteur. Aucun propriétaire dans les plans. | `:1305`, `:2646-2654` |
| 0x82 | 235 (3 / 4) | Sous-cas `0x50-0x52` et `0x54-0x56` : `Result` 1, rien d'écrit. 0 site. | `:3150-3189` ; O-E19-39 |
| 0x53 | 333 (7 / 28) | Branche « effet 3, même carte » gelée. 0 site. | `:1928-1965` |
| 0x2F | 352 (9 / 31) | Modes ≥ 2 : `Result` 0 et journal. 0 site (341 en mode 0, 11 en mode 1). | `:1864-1896` |
| 0xA8 / 0xBA | 101 (2 / 3) ; 37 (0 / 0) | `Result = 0` constant (pas de CD, D-E11-5). O-E19-36 changerait `0xBA`. | `:1475-1486` |
| 0x01 | 0 | Rend 1 ; le binaire rend 0. M-03 attend l'auteur. | `:617` |

INCONNU : l'original affiche-t-il une ombre par entité ? Rien dans la DLL ni dans le moteur ne dessine d'ombre.

## 5. Gestionnaires portés dont le commentaire ne cite pas le binaire

- **FAIT** : 72 gestionnaires portés ne citent ni adresse ni « binaire » dans leur bloc de commentaire de la DLL. Ils pèsent 71 672 instructions, soit 65,1 % du corpus.
- Les plus lourds : `0x37` 9 071, `0x1A` 7 130, `0x02` 4 208, `0x05` 4 153, `0x04` 3 554, `0xBD` 3 303, `0x30` 3 020, `0x09`, `0x06`, `0x39`, `0x0D`, `0x03`, `0x2D`, `0x3B`…
- INFÉRENCE : « non cité » ne veut pas dire « non vérifié ». Le plan E19 a relu au binaire `0x1A`, `0x43`, `0xB8`, `0xAD`, `0x1C`, `0x78`. La taille et l'écriture de `Result` concordent déjà.
- INCONNU : si chacun égale le binaire ligne à ligne. La décompilation a déjà menti sur `0x42`, `0x43`, `0x5F`, `0x1C`, `0x78`, `0xB7`, `0xB8`, `0xAD`, `0x0C`.

## 6. Fonctionnalités de niveau interpréteur

| Fonction | État | Preuve et manque |
|---|---|---|
| Boucle fetch / dispatch / avance, Break, End | fidèle | `RunOneScriptCall` `:428-518` ; binaire `0x800422B4`-`0x8004232C`. |
| Attentes et reprise | fidèle | Seuls B et C reprennent (`:233-259`). A, D, E, F partagent `_slotAScratchState` (`:187`) et repartent de l'entrée. `InitializeEventData` ne réécrit que l'entrée et le pc (E19.m9). Opcodes qui suspendent : `00 0B 0D 1C 1D 1E 1F 20-26 35 36 37 39 44 47 48 5C C4`, et `5F` non porté (0 site). |
| Boucles, sauts, garde | fidèle, plus un écart | `02-04 30 31 73 74 49 4A 4B 57 58`, famille `78-81`. La garde de production 1024 (`:137`, `:436-446`) est un écart assumé (D-E19-3). `cycles.py` trouve 8 programmes de 7 cartes avec un cycle sans suspension, dont 135 F[4] et 476 B[2] sur la chaîne. Tous reposent sur des branches de drapeaux, donc probablement des faux positifs. INCONNU : les déclenchements réels. |
| Contexte logique (`0x42`, `0x43`, `0x66`) | fidèle sauf `0x66` | Entité logique relue avant chaque instruction (`:473`) ; `:2037-2074`. `0x66` non porté (0 site). `RunMapEventsPass` (`AlundraWorldProxy.cs:3128`) : une entité logique par événement. Réarmement hors zone fait (`:3149`). |
| Programmes d'entité contre programmes de carte | fidèle | `PickEventTrigger` (`AlundraEntityScriptProxy.cs:1663`) et `RunPickedEvent` (`:1761-1778`) : index `& 0x7F == 0` donne `RunSpriteEvent`, sinon `RunScript`. Rattrapage `RunPendingEventTriggers` (`AlundraWorldProxy.cs:3193`, appel `:2634`). Aucun des 9 741 enregistrements n'a de programme B propre. |
| **Table globale `map_alundra` (bit 0x80 clair)** | **non portée** | Repli sur la table de la carte, avec un journal (`:389-396`). Corpus : 6 enregistrements, carte 60 (« Pot magique (boïng) », `C` 1, 4, 4, 4, 4, 10). Tous les autres index ont le bit posé, dont 1 714 sur 1 714 événements de carte. Dans `data-extracted/data/map_alundra.json`, la table C est `[0]` et `Codes` fait 10 octets nuls : le programme d'origine serait vide (INFÉRENCE). Contenu fiable : INCONNU, la table F a 16 entrées aberrantes. |
| Passage au natif (`0x40`) | fidèle côté script ; dépend d'E14 | `:886-899` ; `g_clearProgramState` `:302`, `:495-507`. 426 sites dans 121 cartes, tous avec l'index 0 (C 389, D 16, E 20, F 1). Le consommateur `RunSpriteEvent` (`:314-374`) a le livre de sauvegarde, la destruction E 0/1 (`:378`), puis un registre natif de 3 familles (`AlundraNativeSpriteRegistry.cs:29-36` : Beannoïde 335, Muruta 347, vague d'épée C61). Le reste est un no-op compté. |
| Crochets de cinématique | sans objet | Aucun opcode dédié. La scène est un programme B ordinaire (verrou `0x10`/`0x11`, caméra `0x67-0x69 0x8E 0x8F`, fondus `0xAF-0xB1`, dialogue). `.cutscene` relève d'E17 (non planifiée). |
| Dialogue | fidèle, boîte en cours | `OpenDialog` `:2168-2204` rend 0 tant qu'une boîte est ouverte. E19.f : f2a, f2b0 et f2b1 en recette ; f3 et f4 faits. |
| Apparition d'entités | partiel | `0x2D 0x8A 0x8B`. Pas de plafond de 63 entités ni de réutilisation de créneaux (M-12 : boucles `0x8A` sans fin, cartes 28, 30, 134, 147, 159, 160). |
| Opcode inconnu / sans document | équivalent au binaire | `:3115-3141` ; `RunScript` sans document est un no-op compté (`:217-228`). |
| Trace et harnais | présent | `TraceSink` `:118`, `EventTraceKind` `:44-62`. `IntroTraceHarnessTests.cs:322` garde une copie manuelle de l'oracle (`ImplementedOpcodes`). |

## 7. Dettes relevées (FAIT)

1. `EventOpcodeSizeTable.cs:10` annonce 197 entrées pour 198.
2. Libellés faux de `0xB4`/`0xB5`/`0xB6` (`:228-230`). Libellés vides de `0x88`, `0xA9`, `0xAA` (`:184`, `:217-218`).
3. `0x29` : adresse citée `0x8003DC40` (`Runner.cs:736`), table binaire `0x8003DC3C`.
4. Plans périmés : « Dispatch porte 87 opcodes », « 88 opcodes sautés ».
5. La règle E19.m « aucun opcode atteignable sauté hors E14 et E18 » ne peut pas passer telle quelle.
6. `0xAC` n'a pas de propriétaire ; `0x66` : sens de la copie faux dans la décompilation (O-E19-2).

## 8. Ordre proposé des tranches

Principe : la DLL seule d'abord, avant tout ce qui demande le convertisseur, le moteur ou l'auteur. Chaque tranche suit le motif d'E19.l : gestionnaire relu au binaire via la table `0x80098FAC`, petit oracle de valeurs, tests rouges d'abord, vérificateur neuf, une ligne de moins dans la liste fermée.

| # | Tranche | Sites fermés | Taille | Dépend de |
|---|---|---|---|---|
| SM0 | Filet de sécurité : test statique du corpus entier (M-05/G7), liste fermée des 510 sites avec colonne propriétaire. Règles : aucune ligne manquante, aucune ligne périmée, aucune attente sautée, aucune taille nulle atteignable. Remplacer `ImplementedOpcodes` (`IntroTraceHarnessTests.cs:322`) par `AlundraStoryChainOpcodeAudit.IsPorted`. | 0 | S, tests seuls | Rien. Propriétaire par défaut « à placer ». |
| SM1 | Prédicats de chevauchement et de zone : `0x60 0x61 0x3D 0x3C` | 187 (11 cartes) | S | SM0 ; `RidingEntity` (existe) ; `AnimFlags` et `PlatformEntity` à vérifier |
| SM2 | Mémo de position et seuils : `0x6A 0x6B 0x6C 0x6D` | 129 (8 cartes) | S-M | SM0 ; `Parameters[2..4]` partagé avec les mémos d'attente (E19.m9 les conserve) |
| SM3 | Prédicats de contact, de groupe et de `ForceZ` : `0xA9 0xAA 0xB2 0xB4-0xB7 0x9E`, avec correction des libellés | 42 (11 cartes) | S | SM0 ; `XCollisionEntity` (E19.d2b) et `ForceZ` existent |
| SM4 | Actions : `0x72 0x88 0x96 0x97 0xB3` (et `0x15 0x66 0x7A`, 0 site) | 90 (50 cartes) | S-M | SM0 ; vie (G3d), `AddMoney` (E19.l2), `AlundraTickPad` (E20.a). Lire `0x96` et la table `0x80023D2C` au binaire d'abord. |
| SM5 | `0x56` (`MapCopies`) | 17 (cartes 21 et 60) | M | Export du convertisseur (256 entrées de 6 champs par carte, présentes dans `data-extracted/data/map_N.json`), export complet, `CellMutator.CopyCellRectangle` |
| SM6 | Manques des partiels : `0x82` (0 site, avec SM4), cycle de palettes de `0xA4` (1 site, moteur O-E19-43/56), ombres de `0xAC` (32 sites) | 33 | S / L / ? | SM4 ; propriétaire à désigner pour `0xAC` |
| SM7 | Pistes XA `0xB9` et `0xBA` | 42 (+37) | L | L'auteur (O-E19-36) ; décodage du `.bin` brut et lecteur de flux ponctuel du moteur |
| SM8 | `0xBB` et `0xC2` | 3 | – | E18 |
| SM9 | Niveau interpréteur : table globale `map_alundra` (6 pots, carte 60), `0x01` (M-03), plafond de 63 entités (M-12), recherches sans allocation (M-13), rognage de bord (O-E19-17) | – | S chacun | SM0. M-03 et le rognage attendent l'auteur. Pour la table globale : « programme vide » ou fidélité au binaire, à décider. |
| SM10 | Balayage au binaire des 72 gestionnaires sans citation, par poids (`0x37 0x1A 0x02 0x05 0x04 0xBD 0x30…`) | 71 672 instructions couvertes | M-L | SM0 ; `sizecheck.py` et `resultcheck.py` en sont un début |

- Après SM1-SM4, 448 des 510 sites sont fermés (88 %) et aucun prédicat à `Result` périmé ne reste.
- Les 62 restants : `0x56` (17), `0xB9` (42), `0xBB` et `0xC2` (3).
- Liés mais hors de ces tranches :
  - E14 : les 426 `0x40` aboutissent à l'IA native, dont 3 familles seulement sont portées.
  - E17 : cinématiques.
  - O-E19-17 : une marche vers le bord de la carte ne se termine pas dans le DLL, ce qui touche `0x1E` et `0x24`.
- À ajouter à SM0 quand SM1-SM4 seront faites : un test dynamique « zéro `LoopBudgetExceeded` » sur les arcs.

## 9. Limites

- Le recensement est un surensemble statique (les deux branches de chaque saut, retours de la famille `0x78-0x81` par repères). Il compte des instructions atteignables, pas exécutées.
- Il suit les racines de `AlundraStoryChainOpcodeAudit.ReadRoots` (index `& 0x7F` non nul dans la table). Il ignore la table globale (6 enregistrements) et les programmes que seule une exécution créerait.
- La détection de tests est heuristique : noms de méthodes, programmes littéraux décodés par la table de tailles, commentaires. Elle a été revérifiée à la main pour les 29 sautés et pour `0x23 0x30 0x41 0x4D 0x4E 0x6F`.
- Rien n'a été construit ni exécuté. Aucune mesure dynamique.