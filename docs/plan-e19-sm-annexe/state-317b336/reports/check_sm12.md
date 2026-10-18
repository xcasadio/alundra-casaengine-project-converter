# sm12 — contre-vérification du relevé « E19.sm1 et E19.sm2 traduits au HEAD »

Lecture seule. Dépôt `D:/development/repo/alundra-casaengine-project-converter`, HEAD `317b3360`, branche `chantier/restructuration-dll-r3`.
Étiquettes : **FAIT** (ligne lue au HEAD ou sortie de commande), **INFÉRENCE**, **INCONNU**.

Rien n'a été écrit dans le dépôt, les sous-modules ni `alundra-project/`. Aucun build, aucun test, aucun binaire exécuté.
Les faits du binaire sont repris de `docs/plan-e19-sm-annexe/sm1-sm4/check.md` (qui prévaut sur le rapport) ; ils ne sont pas rejoués ici.

Rapport sauvegardé : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-state/sm12/check.md`.

Mes scripts portent le préfixe `v_` pour les distinguer de ceux du relevé. Ils sont dans le même dossier :

| Script | Rôle |
|---|---|
| `v_map.py` | Correspondance ligne à ligne `9502f75` → HEAD, par difflib, après les renommages `final` de `tools/dll-structure/table.json`. |
| `v_sites.py`, `v_sites2.py` | Recomptes, et contrôle des 316 sites dans l'export. |
| `v_decode.py` | Décodage linéaire d'un programme. |
| `v_memo.py` | Lectures du mémo de sm2. |
| `v_riders.py` | Drapeaux statiques des porteurs et des cavaliers. |

## 0. Verdict

- **Le relevé est fiable sur l'essentiel.**
  - Toutes les citations de DLL relues désignent le même code. Seul le numéro de ligne change.
  - Les 316 sites sont toujours dans l'export.
  - A1-A8, A10-A12, A14, A15, A17, A19, A21-A23, A25 et A26 tiennent tels quels.
- **À corriger :**
  - 9 erreurs (E1-E9) et 8 imprécisions de citation (P1-P8).
  - 5 verdicts à nuancer : A9, A13, A16, A18, A20. A27 est à compléter.
  - 3 gravités à reclasser (G1-G3).
- **18 omissions (O1-O18).** Trois d'entre elles changent la tranche :
  - La règle qui interdit toute tranche E19 pendant la restructuration est `docs/plan-restructuration-dll.md:160`, pas D-R-8.
  - La valeur « rouge » d'aujourd'hui sur plusieurs sites réels n'est pas « `Result` inchangé ». C'est le `Result` qu'un `0x43` voisin vient d'écrire.
  - E20.g1a, READY depuis le 2026-10-18, écrit le champ `RidingEntity` que lisent `0x60` et `0x61`.
- **E19.sm1 : REVISE. E19.sm2 : REVISE.** Aucune des deux n'est exécutable (§8). Elles sont bloquées :
  - d'abord par la règle du chantier ;
  - puis par l'absence de sm0, ou d'un substitut décidé ;
  - enfin par des tâches sans règles d'exécution ni valeurs attendues.

## 1. Erreurs, imprécisions, verdicts à nuancer, gravités

### 1.1 Erreurs (E)

**E1. « D-R-8 interdit une autre tranche en parallèle » (§6.2) : faux.**
- D-R-8 (`docs/plan-restructuration-dll.md:27-35`) fixe l'ordre de fusion : E20 dans `main` d'abord, puis le chantier.
- L'interdiction est une règle du programme, `:160` : « Aucune autre tranche (E20, E14, E19) pendant le chantier ».

**E2. « R5 ne doit ajouter ni retirer aucun test (`:869`) » : mauvaise ligne.**
- La règle est en `:872` (« aucune valeur attendue changée, aucun test ajouté ni retiré ») et redite en `:994`.
- `:869` termine une autre phrase (« autonomes, Q-R-6). »).

**E3. « D-E19-2 (`:49`) » : mauvaise ligne.** D-E19-2 est `docs/plan-e19-opcodes.md:51` ; `:49` est D-E19-1.

**E4. « Propriété de `EntitySupport.cs`, `ScriptedMotion.cs`, `EntityScriptProxy.cs` réservée à D5b (plan `:7403`) » : faux.**
`:7403` est la quatrième des « Questions à l'auteur, avant le plan d'h2 » (`:7399-7403`). C'est une question ouverte, pas une réservation.

**E5. A16 compte les lignes du détour parmi les écritures de P2 et P3.**
- `:2991-2992` sont des **lectures** : `var destPxX = (state.Parameters[2] >> 16) + …`.
- `Wait` écrit aussi en `:2660` (`Parameters[2] = counter + 1`), pas seulement en `:2655`.
- Écrivains réels au HEAD : `:2655`, `:2660`, `:2715-2716`, `:2766`, `:2814`, `:2861`, `:2880`, `:2885`, `:2908-2909`.
- Le verdict VRAI ne change pas.

**E6. §3 : « `AlundraClassAndBranchOpcodesTests.cs:143-219` (0x3E/0x3F) ».**
- Cette section ne teste que `0x3F`. Elle va de `:143` à `:222`, la fin du dernier test étant `:222`.
- Les tests de `0x3E` sont dans `AlundraEventProgramRunnerTests.cs`, en `:4045` et `:4062`.

**E7. §6.7 : « Pour sm2, seules 344, 307 et 442 sont des cartes du jeu ».**
- Ce n'est pas prouvé, et c'est contredit par I3 du même relevé.
- Seul FAIT : 64 des 129 sites sont sur 307, 344 et 442 (8 + 40 + 16), et 65 sur 55 à 60.

**E8. §6.14, pour `60 81` : « le cas doit parcourir `PlayerEntity` en plus de `SpawnedEntities` comme 0x3F ».**
- Pour `0x60` et `0x61`, le type 1 de la recherche rend le héros par l'argument `playerEntity` (`EntitySearchService.cs:127-133`).
- Il suffit donc de passer `_worldContext.PlayerEntity`, comme le font `EntityInArea` (`EventProgramRunner.cs:3271`) et `0x43` (`:2012`).
- Le parcours supplémentaire du héros ne concerne que `0x3C` et `0x3D`, qui balaient les slots 0..N. Le modèle est `0x3F` (`:1967-1980`).

**E9. §0 et §1.1 : quatre worktrees contiennent l'ancienne DLL, pas deux.**
- Ce sont `awesome-bun-0d3ca1`, `e13d-sub-inventory`, `musing-gagarin-acef8b` et `upbeat-robinson-197cfc`.
- FAIT : présence de `Alundra/Scripts/AlundraEventProgramRunner.cs` testée dans chaque `.claude/worktrees/*/`.

### 1.2 Imprécisions de citation (P)

Les traductions mécaniques du relevé sont justes. Certaines citations d'origine débordaient déjà, ou n'atteignaient pas, ce qu'elles désignent.

| # | Citation | Traduction mécanique | Citation juste au HEAD |
|---|---|---|---|
| P1 | `:377-419` (`InitializeEventData`, S §2) | `:327-369` | `:330-369`. `:327-328` est la doc d'`IsInlineDeactivateCode` : la citation débordait de 8 lignes dès `9502f75`. Doc `:330-334`, signature `:335`. Le « corps `:337-369` » du relevé s'entend sans l'accolade `:336`. |
| P2 | `EntitySearchService.cs:98-262` | `:99-263` | La méthode va jusqu'à `:269`. L'ancienne citation s'arrêtait 6 lignes avant la fin (`:268` à `9502f75`). |
| P3 | `AlundraWorldProxy.cs:2955-2960` (note D-E20-13) | `:2989-2994` | `:2989` est vide. La phrase « `+0x12C` … once per frame … D-E20-13 » est en `:2994-2995`. |
| P4 | `EntitySupport.cs:71-75` | `:72-76` | `:72` est `</summary>`. `IsEligibleSubject` : signature `:73`, corps `:74-76`. |
| P5 | `AlundraEntityScriptProxy.cs:81` (`LifePulledFromStats`, sm4) | `:94` | `:94` est une ligne de doc ; le champ est en `:97`. |
| P6 | `:1990-2040` | `:1940-1990` | La plage mord sur `case 0x42` de 4 lignes (`:1987-1990`), plus la ligne vide `:1986`. Le relevé dit 5. |
| P7 | `:2011-2013` (C R1) | `:1961-1963` | Ce commentaire porte « the decompilation accepts it and stops before N », le raisonnement que C R1 réfute. Ne pas le recopier dans les nouveaux `case`. |
| P8 | §0, « seules différences » | — | R0 (`146e1d81`) change aussi trois lignes d'`AlundraEntitySpawnFactory.cs` (`:438`, `:509`, `:588` à l'époque) : `nameof(…)` devient `typeof(…).FullName!`, sans décalage. Aucune n'est citée. FAIT : `git show 146e1d81 -- Alundra/Scripts/AlundraEntitySpawnFactory.cs`. |

### 1.3 Verdicts à nuancer

**A9 — PÉRIMÉ, confirmé.**
- La cause n'est pas la restructuration : le chemin par tick existait déjà à `9502f75` (appel en `:2931` de l'ancien fichier, `b68fefca` du 2026-10-09).
- Le texte d'O-E20-9 (`docs/plan-e20-heros.md:9125`, « la DLL ne recalcule le porteur qu'une fois par image ») est périmé lui aussi. Le relevé ne le dit pas.

**A13 — VRAI, mais la liste des écrivains est incomplète.**
- `TileZ` seul est aussi écrit en `ScriptedMotion.cs:377`, `:444`, `:520` et `:586`.
- `TileX`, `TileY` et `TileZ` ensemble sont écrits en :
  - `WorldProxy.cs:2151-2153` ;
  - `EntityRecordMapper.cs:183`, `:191`, `:204` ;
  - `EntitySpawnFactory.cs:532-534` ;
  - le clone, `EntityScriptProxy.cs:2659-2661`.

**A16 — VRAI.** Voir E5.

**A18 — VRAI seulement pour les 18 lectures Z précédées d'un `0x6A`.**
- Pour les 11 lectures sans `0x6A`, `|0 − PosZ|` diffère d'une unité brute de la valeur du binaire (ADR-0026).
- Cet écart ne change `Result` qu'à l'égalité exacte avec le seuil (C O7).

**A20 — VRAI, mais la liste des cartes d'arc est incomplète.**
- Les arcs chargent aussi, entre autres, 83, 89, 163, 311, 321 et 392 :
  - `AlundraAbsoluteZWritesTests.cs:68-108` ;
  - `AlundraInoaAwakeningArcTests.cs:36` (163) et `AlundraShipCorridorArcTests.cs` (392) ;
  - `AlundraHeightTargetSitesTests.cs:34-67` ;
  - `AlundraLoadingMapAirArrivalTests.cs:50` ;
  - `AlundraRecordBirthTickTests.cs:368`.
- Aucune n'est une carte de sm1 ou sm2. FAIT : `rg` des noms de monde des 19 cartes dans `Alundra.Tests` ne ramène que deux tests :
  - `AlundraEventProgramRunnerTests.cs:4164` (carte 27, lecture d'un seul octet) ;
  - `BackdropStageDefinitionTests.cs:76` (carte 159, le fond).

**A27 — à compléter : le contrôle statique couvre maintenant les 186 sites `0x60`/`0x61`** (A30 au §4).

### 1.4 Gravités à reclasser (G)

**G1. Manque 4 (« preuve de retrait »), classé [B] par le relevé.**
- Le motif d'E19.l exige :
  - des rouges aux valeurs du plan (« rouge = la valeur d'aujourd'hui, l'opcode sauté ») ;
  - un arrêt quand une valeur est contredite ;
  - une acceptation en 4 points (`docs/plan-e19-opcodes.md:6692`, `:6760-6767`).
- Ces trois points sont [B].
- Les « ensembles de retrait exacts » viennent des révisions récentes d'E20 (h4), pas du motif d'E19.l : [M].

**G2. Manque 5 (« T0 vecteurs »), classé [B].**
- Ce qui bloque, ce sont les valeurs attendues des tests.
- E19.l1 a écrit ses valeurs à la main, puis les a auditées contre le binaire et la DLL (« Audit des valeurs », `:6663-6667`).
- Des vecteurs figés ne sont qu'une des deux voies : leur format est [M].
- Une deuxième source existe : l'oracle propre de la critique, `docs/plan-e19-sm-annexe/sm1-sm4/check/myoracle.py`. Il n'écrit pas non plus de vecteurs.

**G3. Manque 7 (« pas de recette en jeu définissable »), trop fort.**
- Plusieurs sites mettent en jeu le héros ou un PNJ scripté, sans transport :
  - 136 C[5] et C[10], `60 81` : interrupteurs au sol sous le héros ;
  - 159 B[5], `61 0D` : le héros sur la bulle, enregistrement 13 ;
  - 77 et 78, `61 81` : bois flottant ;
  - 226 B[3] : scène d'Inoa.
- Une recette y est définissable, à condition de connaître l'atteignabilité et d'avoir un état de départ (sauvegarde de test). INFÉRENCE.

## 2. Omissions (O)

**O1. Valeur « rouge » d'aujourd'hui sur les sites réels.**
FAIT pour le décodage (`v_decode.py`) et pour les écrivains de `Result` dans le corps des `case` de `Dispatch`. INFÉRENCE pour l'exécution réelle.
- **226 B[3].** Le dernier écrivain de `Result` avant `04 F8 FF @255` est `43 0B @234` (`0x43` écrit `Result`, `EventProgramRunner.cs:2016` et `:2020`). Entre les deux, `09`, `0B`, `1A`, `42` et `00` n'écrivent pas `Result`.
  - Aujourd'hui, si l'enregistrement 11 existe, `Result` vaut 1 et la scène continue tout de suite.
  - Le binaire, lui, attend qu'une entité entre dans (43, 48..51, 12).
  - La scène retire la main au joueur (`10 @187`) et la rend en `11 @298`.
- **398 B[1..7].** La suite est `43 0N @86 / 61 00 @88 / 42 @90 / 03 64 00 @91`. Le `03` lit le `Result` du `0x43`.
  - Aujourd'hui, le saut vers `@191` est pris dès que l'enregistrement N existe.
  - Le binaire teste si l'enregistrement N repose sur l'enregistrement 0.
  - `next_is_Result_consumer = N` dans `skipped-sites.tsv` masque ce lecteur.
- **27 C[2] et 400 C[1]** (même forme).
  - La boucle `73 18 / 00 / 30 … / 60 0N / 03 … ×4 / 74 E6 FF` ne contient aucun opcode porté qui écrive `Result` :
    - `0x73` : `:1199-1202` ;
    - `0x30` : `FlagBranch`, `:2240-2246` ;
    - `0x74` : `:1204-1208`.
  - Chaque `03` lit donc le `Result` d'entrée.
  - Un état neuf a `Result` à 0 : champ sans initialiseur, `EventProgramState.cs:36`.
  - INFÉRENCE : aujourd'hui, aucune plaque ne réagit.

**O2. Couplage avec E20.g.**
- E20.g1a « Porter » est **READY** (révision du 2026-10-18, `docs/plan-e20-heros.md:6713`). Sa couture D-G-2 (`:6584`) :
  - écrit `PlatformEntity` ;
  - **vide `RidingEntity` de l'objet** porté.
- sm1 lit ce champ. L'ordre entre sm1 et g1a, et les tests de « soulever un objet posé sur une plaque », sont à fixer.
- Le constat de Q-sm-5 (« pas de tranche », O-E20-21) est en partie périmé. Les objets des cartes 27, 29 et 400 sont des « Objet étoile / lune / eau / soleil (transportable) » (`EntityName` des `*.tileMap`), donc de la famille du transport :
  - g1a, g2a, g2b et g1b sont READY (`:6713`, `:6790`, `:6848`, `:6896`) et, comme toute tranche E20, attendent la fin du chantier (`docs/plan-restructuration-dll.md:160` ; E20.g3 `:7434` le dit pour lui-même) ;
  - g2c (C4, tonneaux, carte 113) n'est qu'une esquisse (`:7015`).

**O3. Grille statique vérifiée sur les 186 sites `0x60`/`0x61`, mais insuffisante.**
- FAIT (`v_riders.py`) : voir A30 au §4.
- Des écritures de drapeaux au cours de l'exécution existent dans ces mêmes programmes :
  - 27 C[2] `63 05 00 02 @738` (efface `0x0200` sur l'objet 5) ;
  - 136 C[5] `62 80 00 01 @1568` (pose `0x0100` sur l'interrupteur lui-même).
- La « mesure des cavaliers réels » de T0 doit donc être dynamique, ou couvrir ces écritures.

**O4. Deux points de la critique concernent sm1 et ne sont pas repris.**
- C O6 : dans un programme B, l'entité logique est le héros. Cela vaut pour 8 des 14 sites `0x61` :
  - 159 B[5] `61 0D` : logique = héros ;
  - 398 B[1..7] : logique = la cible du `0x43`.
- C O4 : `0x800384F4` construit les listes, ce n'est pas « coups ». C'est le maillon de l'ordre des passes qu'utilise A9.

**O5. Le sens de `0x3C` dans `opcodes.tsv` est celui de la décompilation, donc inversé.**
- `opcodes.tsv`, colonne `semantics_or_binary_behaviour` : « avec AnimFlags NoEntityCollision, sans Collidable ».
- Le binaire teste l'inverse (S §4.1, C §2.3 et §2.7).
- À ne pas recopier.

**O6. Q-sm-8 ne vise que les cartes 55 à 57.**
- 59 et 60 sont aussi des « (early version of Torla) ». Elles portent 44 sites, dont 2 des 5 arcs proposés pour sm2 (59 C[6], 60 C[1]).
- La question doit couvrir 55 à 60.

**O7. Les citations de la décompilation n'ont pas à être traduites.**
- `git diff 9502f75 HEAD -- alundra-datas-analyser` est vide.
- Le pointeur `fc168729` égale le HEAD du sous-module.

**O8. Les citations de tests du HEAD ne valent que jusqu'à R5.**
- R5 déplace 333 `.cs` et en renomme 306 (`docs/plan-restructuration-dll.md:881-883`).
- Les citations de la DLL, elles, restent valides pendant R5 : un fichier de `Alundra/` qui change est un arrêt de R5 (`:999`).
- Un test ajouté avant R5 rend périmés les comptes et les tables de R5 (T0 à refaire).

**O9. L'ADR-0075 a 7 règles, pas 4** (`docs/decisions/0075-…md:18`).
- La règle 7 (« plans, ADRs and historical annexes are not rewritten ») dit que le §1.4 du plan et l'annexe gelée gardent les anciens noms.
- Seul le texte d'une nouvelle tranche s'écrit avec les noms du HEAD.

**O10. Mise à jour d'`ImplementedOpcodes` : dépend de l'ordre avec sm0.**
- La ligne SM0 du rapport général (`docs/plan-e19-sm-annexe/report.md:179`) propose de remplacer `ImplementedOpcodes` par `AlundraStoryChainOpcodeAudit.IsPorted`.
- La mise à jour d'`ImplementedOpcodes` par sm1 et sm2 (précédent L1-R8) dépend donc de qui passe en premier.

**O11. Libellés de la table des tailles.**
- La Q-sm-8 du rapport inclut `:152`, le libellé de `0x6A` (« Save entity position »), qui est juste. Pour sm2, seuls `:153-155` sont à corriger.
- Pour sm1, la liste des libellés n'est écrite nulle part. À décider :
  - `:142-143` : `0x60`/`0x61`, ambigus ;
  - `:106` : `0x3C`, partiel, car le binaire exige aussi `NoEntityCollision` clair et `PlatformEntity` nul.

**O12. Risque hérité d'E19.l1 pour 136 `60 81`.**
- E19.l1 écrit dans ses risques : « les interrupteurs au sol dépendent de l'exactitude de `RidingEntity` pour le héros (E19.d2c2 S4), non vérifiée ».
- L'en-tête `:6661` note « l'interrupteur non atteint » (recette du 2026-10-06).
- 136 `60 81` (interrupteurs `0x32080`, le héros dessus) hérite de cette prémisse.

**O13. A9 et le texte d'O-E20-9.** Voir §1.3 ; à corriger dans la tranche, pas dans le plan gelé.

**O14. Slots libres et recherche par identifiant brut (P3).**
- Dans le binaire, cette recherche ne teste pas le statut du candidat sur 1..N (C §2.2).
- Un slot libre (statut 0, ou trou ≤ N) qui garderait un `+0x48` égal serait retenu. Les `SpawnedEntities` de la DLL n'ont pas ces slots.
- INCONNU : le recyclage efface-t-il `+0x48` ?

**O15. Numéro d'ADR.**
- `docs/decisions/` a 77 entrées : 0001 à 0075, `README.md` et `template.md`. 0076 est libre.
- Mais `docs/plan-e19-opcodes.md` (17 fois) et `docs/plan-e20-heros.md` (7 fois) renvoient au « prochain numéro libre ».
- Le numéro se fixe donc au commit, pas dans le plan.

**O16. Relecture linéaire du mémo (`v_memo.py`).** FAIT :
- 70 lectures : 59 précédées d'un `0x6A` dans l'ordre linéaire, 11 sans.
- 0 écriture de l'axe entre le dernier `0x6A` et la lecture.
- 4 appels `0x78` intercalés, tous sur la 307 : `@430`, `@448`, `@495`, `@513`.
- Cela confirme C §2.4 et C O5 en lecture linéaire. La partie sensible au flot n'est pas rejouée.

**O17. Citations du rapport général non traduites par le relevé, qui touchent le cadre de sm1 et sm2.**

| Ancienne | HEAD | Contenu |
|---|---|---|
| `:515` | `:465` | `Parameters[1] = 0` |
| `:2152` | `:2102` | `default:` |
| `:385-419` | `:335-369` | `InitializeEventData` |
| `:564-588` | `:514-538` | `FillDataFromCommand` : les octets sont lus non signés, de 0 à 255 |
| `:452-457` | `:402-407` | branche `0xFF` |
| `:459-466` | `:409-416` | branche `0x00` |
| `:3149` | `WorldProxy.cs:3183` | réarmement hors zone |

Pour sm4 : `AlundraPlayerManager.cs:1873` → `PlayerManager.cs:1886`, et `:1893-1910` → `:1906-1923`.

**O18. Le motif d'E19.l a d'autres pièces que le relevé ne liste pas.**
- les règles d'exécution numérotées (L1-R1 à L1-R8, `:6669-6688`) ;
- un « Montage » par test (`:6717`) ;
- les messages de commit ;
- la fusion de commits documentée ;
- `MapsWithoutSkippedSite`, sans effet ici : aucune carte de la chaîne.

## 3. Table corrigée des citations

### 3.1 Fichiers

Stade `final` de `table.json`, arbre réel vérifié. Les entrées sont confirmées, sauf la ligne des tests (précisée).

| Ancien (`9502f75`) | HEAD | Type (avant → après) |
|---|---|---|
| `Alundra/Scripts/AlundraEventProgramRunner.cs` | `Alundra/Events/EventProgramRunner.cs` | `AlundraEventProgramRunner` → `EventProgramRunner`. Décalage de −50 : `EventTraceKind` et `EventTraceRecord` sont coupés dehors. La doc de classe du runner est restée au-dessus de l'enum, dans `EventTraceKind.cs:18-46`, comme déjà à `9502f75`. |
| `Alundra/Scripts/EventProgramRunner.cs` (interface) | `Alundra/Events/IEventProgramRunner.cs` | `IEventProgramRunner`, avec `NoOpEventProgramRunner` |
| `Alundra/Scripts/EventOpcodeSizeTable.cs` | `Alundra/Events/EventOpcodeSizeTable.cs` | inchangé |
| `Alundra/Scripts/EventProgramState.cs` | `Alundra/Events/EventProgramState.cs` | inchangé |
| `Alundra/Scripts/EntitySearchService.cs` | `Alundra/Entities/EntitySearchService.cs` | inchangé |
| `Alundra/Scripts/AlundraEntityScriptProxy.cs` (avec `.HitPass.cs`) | `Alundra/Entities/EntityScriptProxy.cs` (avec `.HitPass.cs`) | `EntityScriptProxy` |
| `Alundra/Scripts/AlundraWorldProxy.cs` | `Alundra/Maps/WorldProxy.cs` | `WorldProxy` ; `AlundraMapEvent` → `MapEvent` |
| `Alundra/Scripts/EntitySupport.cs` | `Alundra/Physics/EntitySupport.cs` | inchangé |
| `Alundra/Scripts/EntityFlags.cs` | `Alundra/Entities/EntityFlags.cs` | `EntityPickupKind` coupé vers son fichier |
| `Alundra/Scripts/AlundraScriptedMotion.cs` | `Alundra/Physics/ScriptedMotion.cs` | `ScriptedMotion` |
| `Alundra/Scripts/IEntityWorldContext.cs` | `Alundra/Maps/IEntityWorldContext.cs` | `NoOpEntityWorldContext` coupé vers son fichier |
| `Alundra/Scripts/AlundraEntitySpawnFactory.cs` | `Alundra/Entities/Spawning/EntitySpawnFactory.cs` | `EntitySpawnFactory` |
| `Alundra/Scripts/AlundraPlayerManager.cs` | `Alundra/Hero/PlayerManager.cs` | `PlayerManager` |
| `Alundra/Scripts/AlundraMapSizeReader.cs` | `Alundra/Maps/MapSizeReader.cs` | `MapSizeReader` |
| `Alundra/Scripts/AlundraCellStore.cs` | `Alundra/Cells/CellStore.cs` ; interface `Alundra/Cells/ICellMutator.cs:21` (fichier de 38 lignes) | `CellStore` ; `IAlundraCellMutator` → `ICellMutator` |
| `Alundra/Scripts/AlundraGameState.cs` | `Alundra/Session/GameState.cs` | `GameState` |
| `Alundra/Scripts/AlundraTickPad.cs` | `Alundra/Hero/TickPad.cs` | `TickPad` |
| `Alundra.Tests/*` | mêmes chemins, mêmes noms | FAIT : la liste des types de premier niveau de `Alundra.Tests` ne diffère que par `AlundraScriptAssemblyRegistration`, `AlundraScriptNamesInTheDataTests` et `SimpleNameEntityLoader` (ajouts de R0). Aucun type de test renommé. |

### 3.2 DLL

« = » signifie : même ligne de code, numéro décalé. Les corrections sont en gras.

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:613-2154`, `Dispatch` | `EventProgramRunner.cs:563-2104` | = ; 158 étiquettes `case 0xNN:`, toutes au niveau 12 espaces, aucun doublon |
| `:2152-2153` | `:2102-2103` | = |
| `:3115-3141`, `UnknownOpcode` | `:3065-3091` (doc `:3058-3064`) | = |
| `:377-419`, `InitializeEventData` | **`:330-369`** (mécanique `:327-369`) | P1 |
| `:1990-2040` | `:1940-1990` (0x3E `:1940-1959`, 0x3F `:1961-1985`, 0x42 dès `:1987`) | = (P6) |
| `:2011`, et T2 « après 0x3F » | `:1961` ; insertion entre `:1985` et `case 0x42` `:1987` | = |
| `:2011-2013` | `:1961-1963` | = (P7 : porte le raisonnement réfuté) |
| `:2691`, `IsRidingTheLogicEntity` | `:2641-2645` | = |
| `:3312-3334`, `EntityInArea` | `:3262-3284` (recherche `:3271`) | = |
| `:2700-3050`, attentes | `:2650-3000` : `Wait :2650`, `Walk :2708`, `WaitZDistance :2761`, `WaitHeightTarget :2791`, `RepeatAnimation :2856`, `WalkUntilBeyondRadius :2901`, `TryEngageDetour :2980` | = |
| Écritures P2/P3 (A16) | **`:2655`, `:2660`, `:2715-2716`, `:2766`, `:2814`, `:2861`, `:2880`, `:2885`, `:2908-2909`** ; `:2991-2992` sont des lectures | E5 |
| `:617-618` (GC) ; `:452-466` ; `:428-518` ; `:213-296` ; `:137` ; `:436-446` ; `:478-479` (G) | `:567-568` ; `:402-416` ; `:378-468` ; `:163-246` ; `:87` ; `:386-396` ; `:428-429` | = |
| G, non traduites par le relevé | voir O17 | ajout |
| sm3 et sm4 : `:250-254`, `:275-280`, `:760-762`, `:1213-1222`, `:1879`, `:1611-1624`, `:3150-3189` | `:200-204`, `:225-230`, `:710-712`, `:1163-1172`, `:1829`, `:1561-1574`, `:3100-3139` | = |
| Dispatch, nouveau : `0x43` | `:2004-2024` (écrit `Result`, `:2016` et `:2020`) | ajout (O1) |
| `EventOpcodeSizeTable.cs:10-11` (« 197 ») | mêmes lignes | = ; 198 entrées, aucune en `0xC5-0xFE` |
| `:40-246` ; `:106`, `:107`, `:142`, `:143`, `:152-155` | mêmes lignes | = ; tailles 7, 7, 2, 2, 1, 3, 3, 3 |
| `:184`, `:199`, `:217-218`, `:228-231` | mêmes lignes | = |
| `EntitySearchService.cs:98-262` | **`:99-269`** (mécanique `:99-263`) | P2 |
| `AlundraEntityScriptProxy.cs:19` | `EntityScriptProxy.cs:32` | = |
| `:76-77` ; `:81` ; `:325` ; `:341-343` ; `:344` ; `:351` | `:89-90` ; **`:97`** (le champ ; `:94` est de la doc) ; `:338` ; `:354-356` ; `:357` ; `:364` | = (P5) |
| `:1212-1213` ; `:1663` ; `:1761-1778` ; `:2411-2418` ; `:2596` (C §2.8) | `:1225-1226` ; `:1676` ; `:1774-1791` ; `:2424-2431` ; `:2609` | = |
| — | `EntityRefId` `:124` ; `LogicEntity` `:64` ; `LogicContextEntity` `:502` ; `PlatformEntity` `:101` ; clone de `RidingEntity` `:2662` | repères |
| `AlundraWorldProxy.cs:299` ; `:874-876` ; `:2077` ; `:2453-2454` ; `:2493` ; `:2893` | `WorldProxy.cs:327` ; `:902-904` ; `:2111` ; `:2487-2488` ; `:2527` ; `:2927` | = |
| `:2955-2960`, D-E20-13 | **`:2994-2995`** (mécanique `:2989-2994`) | P3 |
| `:3128` ; `:3193` ; `:2634` | `:3162` ; `:3227` ; `:2668` | = |
| `:3652-3653` | `:3698-3699`, dans `RefreshUpdateProxiesAndCollidables` `:3687`. Appelée en `:1139`, `:2660` (monde sans pilote), `:2912` (porte fermée), `:2965` (étape « lists » de chaque tick) | = |
| — | `WorldProxy.cs:2304`, le héros inséré au slot 0 ; `:3207-3211`, échange d'état des événements de carte | ajout |
| `EntitySupport.cs:71-75` | **`:73-76`** | P4 |
| `:276-290` ; `:296-307` | `:277-289` (méthode `UpdateRidingEntities`) ; `:297-308` ; `FindRidingEntity` `:311`, sommet exact `:330-335` | = |
| — | `BuildCollidables` `:45-60` : statut ≠ 4 et `IsEligibleSubject` | ajout |
| `EntityFlags.cs:103,136` | mêmes lignes (Collidable `:92`) | = |
| `AlundraScriptedMotion.cs:191` ; `:719-720` | `ScriptedMotion.cs:197` ; `:725-727` | = |
| `IEntityWorldContext.cs:24` | `:35` (doc « hero's slot 0 first » `:29`) | = |
| `AlundraEntitySpawnFactory.cs:493` | `EntitySpawnFactory.cs:497` | = |
| `AlundraPlayerManager.cs:610-631` ; `:1750` ; `:1873` ; `:1885` ; `:1893-1910` ; `:1957` | `PlayerManager.cs:623-644` ; `:1763` ; **`:1886`** ; `:1898` ; **`:1906-1923`** ; `:1970` | = (2 ajouts) |
| `AlundraGameState.cs:143` ; `AlundraTickPad.cs:31-49` | `GameState.cs:152` ; `TickPad.cs:33-51` | = |
| `AlundraCellStore.cs:13-40` (interface à l'ancienne `:18-35`) | `ICellMutator.cs:21-38` | type coupé |
| `opcodes.tsv`, `dll_handler` `…:3115` | `EventProgramRunner.cs:3065` | = (O5 : la colonne de sens de `0x3C` est fausse) |

### 3.3 Tests (valables jusqu'à R5)

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraClassAndBranchOpcodesTests.cs:140-216` (0x3F) | **`:143-222`** ; classe `:21`, `ResultBefore` `:23`, `Context` `:25-35`, `Document` `:37`, `NewEntity` `:44`, `Run` `:47-67`, `Rider` `:146` | = |
| — | tests de 0x3E : `AlundraEventProgramRunnerTests.cs:4045`, `:4062` | ajout (E6) |
| `IntroTraceHarnessTests.cs:322` | `:332` ; étiquette `:1788-1791` | = |
| `AlundraStoryChainOpcodeAudit.cs:128-338` | `:130-340`. `ChainMaps :39`, `MapsWithoutSkippedSite :51`, `WaitOpcodes :54`, `PredicateOpcodes :57`, `IsPorted :75` (sonde sur hôte nu, `:88-…`), `WalkMap :130`, `WalkProgram :163`, `ReadRoots :294`, `LoadMap :343`, `FindListPath :367` | = |
| `AlundraEventProgramRunnerTests.cs:404-433` | `:412-441` | = |
| `AlundraDialogueOpcodesProductionTests.cs:17-31` ; `AlundraYarnVariableStorageTests.cs:120` | `:23-37` ; `:123` | = |
| `Data/story-chain-skipped-opcodes.tsv` | même chemin ; en-tête seul, avec une colonne `slice` | = |

### 3.4 Plans et docs

| Ancienne | HEAD | Statut |
|---|---|---|
| `docs/plan-e20-heros.md:6287` ; `:6321` | `:6525` ; `:6559` (D-G-2 `:6584`) | +238 ; E20.g1a READY, `:6713` (O2) |
| `docs/plan-e19-opcodes.md` §1.4 | `:12034-12097` ; Q-E19-SM1 `:12474` ; Q-sm-1 à Q-sm-8 `:12475-12482` | à jour |
| D-E19-2 | **`:51`** | E3 |
| Règle du chantier | **`docs/plan-restructuration-dll.md:160`** | E1 |
| R5, aucun test ajouté ni retiré | **`:872`** (et `:994`) ; comptes `:881-883` ; arrêt sur la DLL `:999` | E2, O8 |
| Question de propriété D5b | `docs/plan-e19-opcodes.md:7399-7403` (question 4 avant le plan d'h2) | E4 |

### 3.5 Décompilation

`EntityEventHandlers.cs`, `GameEngine.cs`, `EntityManager.cs` et `PlayerManager.cs` de l'analyseur : citations inchangées. FAIT : pointeur de sous-module inchangé (O7).

## 4. Affirmations du relevé : tableau corrigé

| # | Verdict corrigé | Preuve ou correction |
|---|---|---|
| A1 | VRAI | `rg "case 0x(3C\|3D\|60\|61\|6A\|6B\|6C\|6D)\b" Alundra` : 0 résultat ; 158 `case` sur `:563-2104` |
| A2 | VRAI | 198 motifs `{ 0xNN, new(`, uniques ; `:11` dit « 197 » ; maximum hors `0xFF` = `0xC4` |
| A3 | VRAI | tailles et libellés relus ; sens du binaire repris de C §2.4 |
| A4 | VRAI | `:2102-2103`, `:3065-3091` |
| A5 | VRAI | relu type par type, `:105-266`. Réserve P3 : O14 |
| A6 | VRAI | `:105-117` ; `WorldProxy.cs:2111` ; `EntityScriptProxy.cs:124` |
| A7 | VRAI en production, non garanti en test | `WorldProxy.cs:2302-2305` ; `IEntityWorldContext.cs:29` ; `Context` du test `:25-35` |
| A8 | VRAI | seules écritures : `EntitySupport.cs:287`, `:305`, et le clone `EntityScriptProxy.cs:2662` |
| A9 | PÉRIMÉ (déjà à `9502f75`) | §1.3 ; ordre de `RunEntityBlock` `:2905-2988` : `movePlayer`, `programs`, `pending`, `counters`, `lists` (`:2962-2966`), `clock`, `physics` |
| A10 | VRAI | `EntitySupport.cs:282-285` (le `continue` garde l'ancienne valeur) ; seuls les collidables y passent (`BuildCollidables :45-60`) |
| A11 | VRAI | seule écriture : `EntityScriptProxy.cs:2609` (clone) |
| A12 | VRAI | `internal static class EntitySupport` `:21`, méthode `:73-76`, `using Alundra.Physics;` dans le runner `:22` |
| A13 | VRAI, liste à compléter | §1.3 |
| A14 | PARTIEL | `EntityInArea` cherche puis teste la boîte ; `0x3D` n'a pas de recherche |
| A15 | VRAI | `EventProgramState.cs:32`, `:36` ; `InitializeEventData :335-369` ; `CopyFrom :47-56` (utilisé par `WorldProxy.cs:3207-3211`) ; tests M9a-M9d `:28`, `:47`, `:87`, `:111` |
| A16 | VRAI | lignes corrigées (E5) ; écrivains par opcode : 0x0B `:618`, 0x1C/0x1D `:719`/`:724`, 0x1E/0x1F `:732`/`:741`, 0x20/0x21 `:759`/`:765`, 0x22/0x23 `:768`/`:772`, 0x37 `:919` ; aucune `Parameters[4]` dans `Alundra/` |
| A17 | VRAI | `:423-424`, signature `:563` |
| A18 | VRAI, avec réserve | §1.3 |
| A19 | VRAI | liste vide ; `ChainMaps :39-43` |
| A20 | VRAI, liste à compléter | §1.3 |
| A21 | VRAI | sm1 : 187 sites sur 11 cartes (60 : 172 sur 7, 61 : 14 sur 4, 3D : 1, 3C : 0 ; créneaux C 178, B 9). sm2 : 129 sites sur 8 cartes, tous en C |
| A22 | VRAI | O16 |
| A23 | VRAI | `307 C[1]` : `6A @426`, `1B 00 FF @427`, `78 2C 00 @430` (appelle `@474` : `6E / 7F / 1B 00 00 / 7D`), `6B 3C 00 @433` ; idem `@444`, `@448`, `@451` ; C[2] `@491-516` |
| A24 | NON REJOUÉ (flot), FAIT en lecture linéaire | O16 |
| A25 | RÉFUTÉ par C R1, phrase présente | la même idée est dans le commentaire DLL `:1961-1963` (P7) et dans le texte d'E19.l (§1.2m : « La décompilation s'arrête avant N ») |
| A26 | INEXACT pour 27, et aussi pour 29 et 400 | les objets des cartes 27, 29 et 400 sont « transportables » (`EntityName`) ; 113 a « Tonneau générique » et des interrupteurs ; 30 a des boules de fer et des plateformes ; 77 et 78 du bois flottant ; 136 des interrupteurs au sol |
| A27 | voir A30 | |
| **A28** (nouveau) | FAIT (décodage) | 27 C[2] et 400 C[1] : quatre phases de 24 ticks (`73 18 … 74 E6 FF`), chacune teste les quatre objets. La branche de réussite de 27 C[2] (cible de `03 75 00 @614`) est `BD FE 00 @731`, `5B 80 01 00 @734`, `63 05 00 02 @738`, `05 14 80 @742`. Candidat de signal de fin pour un arc : `05 14 80 @742` (INFÉRENCE sur le sens) |
| **A29** (nouveau) | FAIT (code) / INFÉRENCE (exécution) | valeurs rouges d'aujourd'hui : O1 |
| **A30** (nouveau) | FAIT (statique) | Les 186 sites `0x60`/`0x61` passent la grille statique `(Flags & 0x4100) == 0x100` pour le cavalier, avec `Collidable` des deux côtés (détail ci-dessous). Restent inconnus `AnimFlags & 0x80` et les écritures de drapeaux en cours d'exécution (O3) |
| **A31** (nouveau) | FAIT | `RidingEntity` n'est jamais écrit pour une entité hors des collidables (statut 4, `NoEntityCollision` posé, `PlatformEntity` non nul, ou `Collidable` clair) : le porteur comme le cavalier doivent être dans `BuildCollidables` (`EntitySupport.cs:55`, `:319-335`) |

Détail d'A30, drapeaux statiques (`v_riders.py`) :
- Les 170 sites `0x60` à identifiant brut : propriétaires collidables, candidats conformes.
- 8 sites `0x61` à identifiant brut :
  - 398 : objets 1 à 7 `0x230180`, enregistrement 0 (« Lurvy ») `0x3A180` ;
  - 159 : bulle 13 `0x20C0`, le cavalier étant le héros.
- `61 81` sur 77 et 78 : propriétaires « Bois flottant » `0x301E2`.
- `60 81` sur 136 : interrupteurs `0x32080`.
- Le héros (asset `Alundra`, `192c2eeb-…`, `MoreFlags` 140, `CanPickup` 17, `FlagsPortraitShadowType` 3) donne `0x3118C`, calculé selon `WorldProxy.cs:1992`.

## 5. Tests qui épinglent : tableau corrigé

`rg -i "0x(3C|3D|60|61|6A|6B|6C|6D)\b" Alundra.Tests` ne ramène aucun usage comme opcode (FAIT). `opcodes.tsv` donne `tests_named_methods = 0` pour les huit.

| Garde ou test (HEAD) | Effet de sm1 | Effet de sm2 |
|---|---|---|
| `IntroTraceHarnessTests.cs:332` `ImplementedOpcodes` (étiquette du vidage de la 389, `:1788-1791`) | + 0x3C 0x3D 0x60 0x61, sauf si sm0 le remplace d'abord par `IsPorted` (O10) | + 0x6A-0x6D (même réserve) |
| `AlundraStoryChainOpcodeAudit.cs:75` `IsPorted` (vrai runner, hôte nu, `:88-…`) | bascule seul ; une exception sans hôte compte comme « porté » | idem |
| `PredicateOpcodes :57` (règle 4) | facultatif : aucun site sur la chaîne. Précédent : E19.l2 y a ajouté 0x83 et 0x9F, et a retiré 0x9F de `WaitOpcodes` | idem |
| Liste fermée et `AlundraStoryChainSkippedOpcodesTests.cs:74-86` | aucun effet : liste vide, chaîne seulement. La clause « une ligne de moins » n'a pas d'objet sans sm0 | idem |
| `AlundraStoryChainSkippedOpcodesTests.cs:103-111` | reste vrai | idem |
| `AlundraWeaponLockOpcodesTests.cs:149` (`IsPorted` sur un programme B) | aucun effet (il exige l'absence de non-porté) | idem |
| `AlundraEventProgramRunnerTests.cs:331` (0xB3), `:346` (0x5F), `:361` (0xC5) | rien | rien (sm4 changera `:331`) |
| `AlundraInitializeEventDataTests.cs:28`, `:47`, `:87`, `:111` | rien | appui du « mémo qui survit » |
| Tests de 0x0B, 0x1C-0x1F, 0x20-0x23, 0x37 (`AlundraZWaitOpcodesTests.cs`, `AlundraHeightWaitOpcodesTests.cs`, `AlundraRepeatAnimationOpcodeTests.cs`, `AlundraEventProgramRunnerTests.cs`) | rien | appui de « l'écrasement fidèle » |
| `EntitySearchServiceTests.cs` (18 `Fact`/`Theory`), `AlundraClassAndBranchOpcodesTests.cs:143-222` (0x3F), `AlundraEventProgramRunnerTests.cs:4045`, `:4062` (0x3E) | doivent rester verts | rien |
| `AlundraArcSupport.cs:342` `ArcsThatSlide` (contrôlé dans `Dispose :349-370`, arcs `RealController`) | tout nouvel arc à vrai contrôleur doit finir avec 0 glissement | idem |
| `AlundraArcSupport.cs:332` `ArcsWithoutEntityContact` | sans effet (nom absent) | idem |
| `AlundraMissingAnimationWaitCorpusTests.cs:164`, `AlundraDialogueSpeakerOpcodeTests.cs:232`, `AlundraDialogueSpeakerCorpusTests` (balayages par la table des tailles) | rien | rien |
| `AlundraDay3SceneArcTests.cs:39-48` (règle 5) et traces à l'octet de la 389 | rien (aucun site sm1 ni sm2 sur la chaîne) | rien |

## 6. Banc et arcs : confirmés, avec ajouts

Confirmés tels quels :
- `ArcRun` `AlundraArcSupport.cs:94` ; `ArcSpec` `:62-64` ; constructeur `:175-324`.
- `OneFrame :430`, `Press :454`, `PlaceHero :470`, `RunUntil :483`, `Trace :132`, `OnInstruction :136`, `OnFrame :140`.
- `Entities :387`, `EntityByRecord :390`, `SkippedOrExceeded :414`.
- Arcs du plan : `AlundraShipArcTests.cs:90`, `:133`, `:201`.
- Arcs sur sites réels : `AlundraHeightTargetSitesTests.cs:76-90`, `AlundraRecordBirthClockTests.cs:290-303`.
- Montage synthétique : `AlundraClassAndBranchOpcodesTests.cs:21-67`.
- `FindProjectRoot` `SaveGameDirectorTestSupport.cs:70`.
- Lecteur de vecteurs `AlundraSpawnIndexOracle.cs:37-51`.
- Oracle de l'annexe `sm1-sm4/work/oracle/oracle.py` (649 lignes ; `m60 :228`, `m61 :235`, `m3c :253`, `m3d :258`, `m6a :263`, `m6b-m6d :277-287`).
- `lib.py:3` : le binaire existe, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, celui du README de l'annexe.

Ajouts :
- **Deuxième oracle.** `sm1-sm4/check/myoracle.py` (333 lignes) : oracle indépendant de la critique. Il n'écrit pas non plus de vecteurs.
- **Le runner sans contexte.** `EventProgramRunner.cs:147` : `_worldContext = worldContext ?? NoOpEntityWorldContext.Instance`. Un runner sans contexte ne lève pas d'exception dans les nouveaux `case`.
- **Héros absent du faux contexte.** La fausse `Context` des tests ne met pas le héros dans `Spawned` (`:25-35`). Les tests de `0x3D`, `0x3C` et des types de recherche 2 et 4 doivent l'y ajouter, ou le `case` doit parcourir `PlayerEntity` comme `0x3F`.
- **Piège `null == null`.**
  - `NewEntity` donne un `LogicContextEntity` non nul (`:44-45`), mais `Rider` crée des proxies où il est nul (`:146-147`).
  - `IsRidingTheLogicEntity` (`:2641-2645`) n'a pas non plus de garde de nullité.
  - En production, `LogicContextEntity` n'est jamais nul.
- **Pose d'un objet sur une plaque.** Il faut le sommet exact : `otherTopZ = PosZ + ModZ + Depth + 1 == moddedPosZ`, avec chevauchement en X et Y (`EntitySupport.cs:313-345`).
- **Programmes des plaques de 27.** Enregistrements 1 à 4 : `EventCodesC_TickIndex` 130 = `0x82`, donc C[2] (vu sur l'enregistrement 1 ; les propriétaires de tous les sites sont collidables, A30).

## 7. Noms : tableau corrigé

**Règles de l'ADR-0075 (`:18`).** Elles sont **7**, pas 4 :
1. un dossier = un namespace ;
2. aucun préfixe `Alundra` dans les noms de types ;
3. un type principal par fichier, les types publics secondaires dans leur propre fichier ;
4. un nom unique parmi la DLL, le moteur, MGUI, MonoGame, les paquets et la BCL, et aucun type au nom d'un segment de namespace ;
5. les renommages imposés ;
6. ce qui ne change pas ;
7. **plans, ADR et annexes historiques ne sont pas réécrits.**

**Garde Q-R-7** (`docs/plan-restructuration-dll.md:1234-1239`, proposition) :
- `check_names.py --tree` ne lit que la DLL (`scan_types.dll_decls`, chemin `Alundra`) et ignore les types imbriqués.
- Le mode `--table` émet aussi des avertissements sur les tests.

| Élément proposé | Nom final | Garde |
|---|---|---|
| 8 blocs `case` | `Alundra/Events/EventProgramRunner.cs`, après `:1985` | non déclenchée |
| aides privées éventuelles | membres statiques privés de `EventProgramRunner`, sur le modèle de `IsRidingTheLogicEntity :2641` | non déclenchée |
| libellés | `EventOpcodeSizeTable.cs` : sm2 `:153-155` ; sm1 à décider, `:106`, `:142-143` (O11) | non déclenchée |
| tests, test statique, arcs | aucun nom dans le plan. Avant R5 : `Alundra.Tests/Alundra<Sujet>Tests.cs`, namespace `Alundra.Tests` (308 fichiers sur 333). Après R5 (attend Q-R-3) : `Alundra.Tests/Events/<Sujet>Tests.cs`, classe sans préfixe. Indication non versionnée, `scratchpad/r5-disc2/r5-files.tsv` : `AlundraClassAndBranchOpcodesTests` → `Events/ClassAndBranchOpcodesTests.cs`, l'audit → `Support/StoryChainOpcodeAudit.cs`, `IntroTraceHarnessTests` → `Arcs/` | hors garde ; R5 n'admet aucun test ajouté pendant son exécution (**`:872`**) ; un test ajouté avant R5 rend ses comptes périmés (O8) |
| vecteurs | `docs/plan-e19-sm-annexe/sm1/` et `sm2/` (rapport §7) ; l'annexe `sm1-sm4/` est gelée (`SHA1SUMS.txt`, README) | hors ADR-0075 |
| lecteur C# des vecteurs | non nommé ; modèle `Alundra.Tests/Alundra<Sujet>Oracle.cs` (8 fichiers `Alundra*Oracle.cs`) | — |
| liste fermée du corpus (sm0) | non nommée ; la liste de la chaîne a déjà une colonne `slice` | — |
| ADR « du sens de 0x60/0x61 » | à décider (D-E19-2, `:51`, tranche déjà « le binaire l'emporte » ; E19.l n'a pas écrit d'ADR). Numéro au commit, 0076 libre aujourd'hui (O15) | — |

## 8. Manques : liste corrigée, et verdict par tranche

[B] : bloque la mise en tranche exécutable. [M] : à combler avant de lancer.

### Communs à sm1 et sm2

**B0. Interdiction pendant le chantier** (`docs/plan-restructuration-dll.md:160`).
- R5 attend Q-R-3 (`:4`, `:861-868`). Q-E19-SM1 (`docs/plan-e19-opcodes.md:12474`), qui demande « quand », est ouverte.
- Sans décision de l'auteur, rien ne s'exécute.
- Relevé : manque 2 confirmé, avec la citation corrigée (E1).

**B1. sm0 absente.**
- Le plan fait dépendre sm1 et sm2 de sm0 (`:12068-12069`). La clause « une ligne de moins » n'a pas d'objet.
- Il faut soit écrire sm0, soit remplacer la clause par un garde propre à la tranche, par exemple un compte statique des sites atteints et non portés par opcode. Ce choix revient à l'auteur ou au plan.
- Relevé : manque 1 confirmé, avec l'alternative en plus.

**B2. Aucune règle d'exécution (R1..Rn) aux noms du HEAD.**
Le motif d'E19.l en exige (`:6669-6688`) : sens, entité logique, recherche avec `PlayerEntity`, garde de nullité, tailles, `Result` seul.

**B3. Aucun test nommé avec ses valeurs.**
- Il manque, pour chaque test : l'état de départ, l'attendu, la valeur rouge d'aujourd'hui, le montage.
- Il manque aussi les arrêts (« une valeur contredite est un arrêt ») et l'acceptation : suites `Alundra.Tests` en Release puis Debug, `cmp`, six traces à l'octet (`:6760-6767`).
- Relevé : manques 3 et 4 confirmés et reclassés (G1).

**B4. Arcs T3 non spécifiés.**
- §1.3 impose pour chaque arc : drapeaux posés, héros, signal de fin prouvé par la trace, limite d'images, valeurs écrites avant le code (`:12003-12011`).
- Relevé : manque 6 confirmé. Matière ajoutée par cette vérification :
  - candidat de signal de fin `05 14 80 @742` pour 27 C[2] (A28) ;
  - valeurs rouges d'aujourd'hui (O1) ;
  - pose au sommet exact (§6).

**Manques [M] :**
- **M1.** Valeurs de l'oracle : vecteurs figés ou valeurs auditées à la main. Le format est libre (G2).
- **M2.** Libellés à corriger, ligne par ligne (O11).
- **M3.** `ImplementedOpcodes` ou `IsPorted`, selon l'ordre avec sm0 (O10).
- **M4.** ADR à écrire ou non (§7).
- **M5.** Citations aux noms du HEAD dans le texte de la tranche, l'annexe restant gelée (ADR-0075, règle 7 ; O9).
- **M6.** Interaction avec R5 (O8).
- **M7.** Corrections de la critique à reporter : R1, O1, O3, O4, O5, O6, O7 (O4 de ce rapport ; manque 12 du relevé complété).

### E19.sm1 — **REVISE**

**Bloquants propres :**
- **B5.** La portée de `0x3C` (0 site) n'est pas tranchée. Q-sm-6 pose la même question pour `0x9E` et `0x88`, pas pour `0x3C`.
  - Le sens est acquis : C §7.4 et D-E19-2, « suivre le binaire ».
  - Son test pose `PlatformEntity` à la main (A11).
- **B6.** Les arcs de 27, 29 et 400 demandent un objet transportable posé à la main au sommet exact d'une plaque. En jeu, il faut le transport : E20.g1a, g1b et g2a, READY, après le chantier (O2).
  - 226 B[3] demande la scène déclenchée : drapeaux attendus par `36 01 01 @177` et `36 D0 87 @201`, et l'événement de carte.
  - Aujourd'hui, la scène n'attend pas l'entité dans la boîte (O1).

**[M] :**
- **M8.** Écarts de `RidingEntity` :
  - non effacé à la passe des compteurs (A10, O-E20-9, D5b M4, Q-D5B-1 à 6) ;
  - recalcul à l'étape « lists » au lieu de la physique (A9) ;
  - couplage avec E20.g1a, qui vide `RidingEntity` de l'objet porté (O2).
  - Il faut dire si sm1 attend D5b ou g1a, et comment les tests évitent une valeur périmée.
- **M9.** Grille statique acquise (A30), mais les drapeaux changent en cours d'exécution dans ces programmes (O3). La mesure de T0 doit être dynamique.
- **M10.** Inclusion du héros :
  - pour `0x3C` et `0x3D`, parcourir `PlayerEntity` comme `0x3F`, ou ajouter le héros au contexte de test ;
  - pour `0x60` et `0x61`, passer `PlayerEntity` à la recherche (E8).
  - Statuts 1 à 3 par `IsLoadedNormalOrDeactivated`.
  - Comparaison signée : les octets sont lus de 0 à 255 (`:514-538`). Seule une tuile négative distingue un mutant qui comparerait en non signé.
- **M11.** Programmes B : l'entité logique est le héros, ou la cible du `0x43` (O4). Un test de `0x61` à logique héros est nécessaire (159 B[5]).
- **M12.** Recette : surévaluée comme impossible par le relevé (G3). Sites candidats : 136, 159, 77, 78, 226. L'état de départ est à définir. La prémisse des interrupteurs au sol reste non vérifiée (O12).
- **M13.** Ne pas recopier le sens de `0x3C` donné par `opcodes.tsv` (O5), ni « la décompilation s'arrête avant N » (P7).

### E19.sm2 — **REVISE**

**Bloquants propres :**
- **B7.** Le test statique « porter `memo_flow.py` » n'est pas spécifié.
  - Il faut suivre les appels `0x78`/`0x7B`/`0x7C` et leurs retours `0x7D`-`0x81` (C O5).
  - `WalkProgram` (`AlundraStoryChainOpcodeAudit.cs:163`) n'est pas sensible au flot.
  - Les ensembles d'écrivains sont copiés du binaire, sans lien avec la DLL.
  - Les comptes attendus ne sont pas écrits. Ceux qui tiennent, en lecture linéaire : 70 lectures, 59 avec `0x6A`, 11 sans, 0 écrasement, 4 appels intercalés sur la 307 (O16).
  - Une version linéaire, avec contrôle des 4 appelés, suffirait peut-être : choix de plan.
- **B8.** Atteignabilité des cartes 55 à 60 inconnue. Q-sm-8 ne couvre que 55 à 57 (O6), alors que 4 des 5 arcs proposés visent 55, 56, 59 et 60.

**[M] :**
- **M14.** Arithmétique sur 32 bits.
  - Ne pas utiliser `Math.Abs`, qui lève une exception sur `int.MinValue` (le patron de `:2915` le fait). Le binaire fait `negu`.
  - Le seuil `(v1 | v2 << 8) << 16` est négatif dès que `v2 >= 0x80`.
  - Aucun `CheckForOverflowUnderflow` dans les `.csproj` (FAIT).
- **M15.** `0x6D` sans `0x6A` (11 lectures).
  - Avec un état neuf, P4 vaut 0 et `Result = |PosZ| >= seuil` (C O7).
  - Sinon, P4 garde la valeur d'un autre programme C de la même entité (INCONNU).
  - La convention Z ne joue qu'à l'égalité exacte (A18).
- **M16.** Choix des arcs : 307, 344 et 442 portent 64 sites dont l'atteignabilité n'est pas non plus prouvée (E7).

## 9. Inconnus : liste corrigée

- **I1 (réduit).** Le héros passe la grille statique (`0x3118C`) et les 186 propriétaires et candidats aussi (A30). Restent `AnimFlags & 0x80` par animation et les écritures de drapeaux en cours d'exécution (O3).
- **I2.** Égalité entre le `RidingEntity` de la DLL et le `+0x12C` du binaire au moment du script. L'ordre diffère : étape « lists » contre `CheckRidingEntities` dans la physique. S'y ajoutent l'absence d'effacement et les porteurs en mouvement. Aucune mesure.
- **I3.** Atteignabilité des cartes 55 à 60. Les 65 sites se répartissent par opcode : `0x6A` 27, `0x6B` 7, `0x6C` 2, `0x6D` 29. Celle de 307, 344 et 442 n'est pas prouvée non plus.
- **I4.** Les cartes d'arc se chargent-elles dans `ArcRun` sans autre montage ? Un parcours C# sur 483 cartes redonne-t-il les 510 sites ?
- **I5.** Solvabilité en jeu des énigmes 27, 29, 30, 113 et 400. Elle dépend d'E20.g (READY, après le chantier) et de g2c ou C4 (esquisse).
- **I6 (réduit).** Le comportement d'aujourd'hui est décodé statiquement pour 226, 398, 27 et 400 (O1). Il n'est pas mesuré à l'exécution, et reste inconnu pour 136, 159, 77, 78 et les sites de sm2.
- **I7.** Le `Result` d'entrée des programmes C vaut 0 pour un état neuf (champ par défaut). Un état copié ou réutilisé n'est pas mesuré.
- **I8.** Les noms d'après R5 ne sont donnés que par `scratchpad/r5-disc2/r5-files.tsv`. Q-R-3 peut les changer.
- **I9 (nouveau, P3).** Les slots libres de 1..N dans la recherche par identifiant brut (O14).