# E19.sm3 : préparation en lecture seule (contacts, groupes, ForceZ), rapport

Dépôt lu à `36ac0686`, puis recontrôlé à `2b0c3f8b` : `git diff --stat` est vide sous `Alundra/`, `Alundra.Tests/`, `alundra-project/` et `docs/plan-e19-opcodes.md`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8da…5b5caa58`, vérifié. Rien n'a été écrit hors du dossier de sortie, ni modifié dans le dépôt. Aucun build, test .NET ni ArcRun n'a été lancé.

Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm3/` (noté `OUT/`). Contenu :
- `vectors/` : générateur, 837 vecteurs, carte des lectures, mutants, README du format.
- `tests.tsv` : 75 tests nommés.
- `arcs.tsv` : 10 arcs.
- `corpus-lines-sm3.tsv` : les 42 lignes à retirer.
- `tools/` : copies des outils de l'annexe. `work/` : scripts de lecture et sorties. `listings/` : désassemblages. `SHA1SUMS`.

## 0. Verdict et ce qui reste bloquant

Les lacunes de `check_sm34.md` §8 propres à sm3 sont levées, sauf trois points qui ne relèvent pas de la préparation.

| Lacune | État |
|---|---|
| S3-1 : « `B2 04 8B` vaut toujours 0 » | Corrigée. `0x04` est l'id brut 4 (FAIT, `EntitySearchService.cs:105-118`). Les 10 sites demandent « l'objet d'id N est-il porté ? ». |
| S3-2 (`9E`) | Un lecteur de case existe déjà (§1, R6). La politique hors grille est une question d'auteur. |
| S3-3 : T0 non exécutable | Remplacé par des faits du binaire et un ordre du tick de la DLL lu (§4.4). La valeur exacte en monde réel reste une mesure de T0. |
| S3-4 : ordre des slots | Retiré (`AlundraEntitySlotWorldTests.cs:77, :97, :117`). Le montage pose les entités dans l'ordre des slots. |
| S3-5 : `PredicateOpcodes` | Laissé inchangé par défaut (§6). |
| M3 : monde piloté | Résolu. `DrivesEntityTicks => _world?.Game != null` (`WorldProxy.cs:821`), vrai avec `RealController`. |
| G4, M5, M9, G6 | Résolus : tests, mutants, politique sans site, couplage avec g1a, libellés. |
| G2, G3 | Résolus quant aux conséquences et au format, mais sm0 n'est pas exécutée. |
| G5 (arcs) | Les faits statiques sont établis (FAIT). La géométrie et les limites dépendent de mesures de T0 (INCONNU). |
| G1 | Hors préparation. Il faut Q-E19-SM1 répondue, R5 faite et la garde R7 en place (`docs/plan-e19-opcodes.md:12124-12129`). |

Il reste donc :
- sm0 exécutée, R5 et R7 faites, et la réponse à Q-E19-SM1 ;
- les questions Q-SM3-1 à Q-SM3-3 du §6 ;
- les mesures de T0 du §4.5.

## 1. Règles d'exécution aux noms du HEAD

Toutes les étiquettes ci-dessous sont des FAIT (désassemblage `OUT/listings/`, 837 exécutions du vrai gestionnaire, code DLL lu).

**R0 : commun aux 8 opcodes.**
- L'entité qui agit est l'entité LOGIQUE, premier paramètre `entity` de `Dispatch` (`Alundra/Events/EventProgramRunner.cs:563`, calculée en `:423`). C'est le `a0` du binaire.
- La recherche est `EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity)`. C'est le même appel que `0x43` (`:2012`), `0xB8` (`:2062`) et `0x8D` (`:2087`).
- Le héros est dans `SpawnedEntities` et dans `PlayerEntity`. Sans `PlayerEntity`, le type 1 rend une liste vide, sans journal.
- Le cas écrit `state.Result` (0 ou 1) sur toutes les voies et rien d'autre : pas de `Parameters[2..9]`, de `LogicEntity` ni de drapeau.
- Il rend la taille de la table, jamais 0, et ne suspend pas. `Parameters[1] = 0` est déjà fait par la boucle (`:465`).
- Écriture du binaire : seul le mot `+0x2C` de l'état est écrit (0 écriture parasite sur 837 vecteurs).
- Emplacement des cas : après `case 0xB8` (`:2054-2076`) pour les 7 prédicats de recherche, après `case 0x85` (`:1469-1482`) pour `0x9E`.

**R1 : `0xA9`** (`0x80041174`, taille 2).
- Résultat à 1 si `entity.XCollisionEntity` n'est pas nul et si une entité trouvée lui est identique (`ReferenceEquals`). Sinon 0.
- Sens : « je suis bloqué par une entité du groupe ».
- Le champ est typé proxy (`EntityScriptProxy.cs:364`) : pas de risque `null == null`.

**R2 : `0xAA`** (`0x80041200`, taille 2).
- Résultat à 1 si une entité trouvée `m` a `ReferenceEquals(m.XCollisionEntity, entity)`.
- Sens : « une entité du groupe est bloquée par moi ».

**R3 : `0xB2`** (`0x80041628`, taille 3).
- `g1 = Search(entity, v[1])`. Si `g1` est vide, Result vaut 0.
- Sinon `g2 = Search(entity, v[2])`, et Result vaut 1 s'il existe `a∈g1` et `b∈g2` identiques (identité de proxy, pas `EntityRefId`).
- Le binaire copie les 65 mots du tampon avant la 2e recherche. En C#, les deux listes sont indépendantes.

**R4 : `0xB4`, `0xB5`, `0xB6`** (`0x800417CC`, `0x80041830`, `0x80041894`, taille 2).
- `matches = Search(entity, v[1])`. S'il est vide, Result vaut 0, y compris pour `B6`.
- Sinon `f = matches[0].ForceZ`, un `int` signé du PREMIER résultat, donc le plus petit slot.
- `B4` : `f > 0`. `B5` : `f < 0`. `B6` : `f == 0`.
- Pas de `short`, pas de `uint`. Le piège est la liste vide : ne pas lire `matches[0]`.

**R5 : `0xB7`** (`0x800418F8`, taille 3).
- Résultat à 1 s'il existe `m` avec `m.TargetAnimationId == (uint)v[2]`.
- `v[1]` est le type de recherche et `v[2]` la valeur : mot de 32 bits contre octet étendu par zéro.
- La copie corrigée de la décompilation (`EntityEventHandlers.cs:3466`) a déjà cet ordre.

**R6 : `0x9E`** (`0x80040BB4`, taille 6, 0 site).
- `x=v1, y=v2, w=v3, h=v4, min=v5`.
- Compter les couples `(r<h, c<w)` dont l'octet de marchabilité de la case, à l'index linéaire `(y+r)*52+(x+c)`, a le bit `0x02` posé.
- Result vaut 1 si `count >= min`. L'octet haut (GroundProperty) est masqué. La décompilation ne fait pas avancer x.
- Lecteur de case, déjà existant : `CellsCollisionField.TryReadBinaryCell(column,row,out height,out walkability)` (`Alundra/Physics/CellsCollisionField.cs:272`, E20.f2).
  - Il n'est atteignable que par `EntityScriptProxy.ReadCollisionField()` (`:1404`), nul sur un proxy nu.
  - `CellStore.GetWalkability(x,y)` (`:136`) est public mais sans garde de bornes, et `ICellMutator` (`ICellMutator.cs:21-38`) n'a que des écritures.
  - Recommandation : un membre `bool TryReadWalkability(int column, int row, out int walkability)` sur `ICellMutator`, implémenté par `CellStore`, seul implémenteur.
- Le cas calcule `row = idx/52` et `col = idx%52`, ce qui reproduit le passage à la rangée suivante du binaire. Hors grille de 52×60, la case compte pour 0 avec un journal unique.
- Sans mutateur : `LogDegradedCellOpcodeOnce(0x9E, …)` (`:3204`), Result 0, taille 6.
- Un membre d'interface publique implique un ADR (prochain numéro 0076, à réserver à l'exécution).

**R7 : libellés de `Alundra/Events/EventOpcodeSizeTable.cs`, ligne par ligne.**

| Ligne | Aujourd'hui | Après |
|---|---|---|
| `:206` (`9E`) | `Count walkable tile` | `If count of tiles with walkability bit 1 >= value` (si `9E` est retenu) |
| `:217` (`A9`) | `""` | `Is blocked by entity of group` |
| `:218` (`AA`) | `""` | `Is any entity of group blocked by me` |
| `:226` (`B2`) | `Compare entity groups` | inchangé |
| `:228` (`B4`) | `Check ForceZ < 0` | `Check ForceZ > 0` |
| `:229` (`B5`) | `Check ForceZ >= 0` | `Check ForceZ < 0` |
| `:230` (`B6`) | `Check ForceZ != 0` | `Check ForceZ == 0` |
| `:231` (`B7`) | `Check TargetAnimation` | inchangé |

- Le libellé de `0x88` (`:184`) et celui de `0x97` (`:199`) appartiennent à sm4. Cela supprime le double propriétaire de G6.
- Aucun test ni code ne lit ces libellés (rg sur la DLL et les tests : FAIT). Le seul consommateur est le message de `UnknownOpcode` (`:3083-3087`).

**R8 : `IntroTraceHarnessTests.ImplementedOpcodes` (`:332-387`).**
- Y ajouter `0xA9, 0xAA, 0xB2, 0xB4, 0xB5, 0xB6, 0xB7` (et `0x9E`), sous un commentaire `// E19.sm3`.
- La carte 389 n'a aucun de ces opcodes : les traces ne bougent pas.

**R9 : liste du corpus** (sm0, voir §5) : retirer les 42 lignes dans le MÊME commit que le code.

## 2. Oracle de valeurs gelé

Fichiers : `OUT/vectors/{gen_vectors.py, sm3-vectors.jsonl, read-map.json, stats.json, mutants.py, mutants.json, mutants.txt, RUNS-IDENTICAL.txt, README.md}`. Les outils ont été copiés, jamais lancés en place : `OUT/tools/oracle/{oracle,emu,lib}.py` et `OUT/tools/check/`.

**Les 837 vecteurs.**
- Répartition : 213 écrits à la main (nomment les tests), 384 balayages (12 types de groupe et 4 ids bruts par opcode de recherche), 240 aléatoires à graine. Détail par opcode dans `stats.json`.
- Les vrais gestionnaires et la vraie recherche `0x8003C954` tournent dans l'interpréteur MIPS. Chaque ligne donne : monde lu, octets, `v0`, Result écrit, écritures hors Result (toujours vides).
- Résultat sur les 837 : `v0` égal à la taille de la table, modèle python égal au binaire, modèle de la forme DLL (`EntitySearchService`, sans slot libre) égal au binaire, et la DLL rouge aujourd'hui (le Result reste à 7). Aucune ligne n'est verte d'avance.
- Les deux exécutions du générateur sont identiques à l'octet (SHA-256 dans `RUNS-IDENTICAL.txt`).
- Les deux valeurs 0 et 1 sont couvertes pour chaque opcode.

**Contrôles complémentaires.**
- L'oracle copié de l'annexe : 21 opcodes × 2 500 mondes, graine 20261018, 52 500 exécutions, 0 écart (`work/oracle_run_2500_seed20261018.txt`).
- L'oracle indépendant du critique, avec comparaison de toute la fenêtre mémoire : 800 par opcode, 0 écart (`work/critic_oracle_run_800_seed20261018.txt`).

**Correspondance de chaque octet lu avec un membre de la DLL.** Le journal des chargements est classé. Une adresse sans membre fait sortir le script en code 2 (STOP). Aucune lecture n'est non mappée sur les 837 vecteurs.

| Lecture binaire | Membre DLL |
|---|---|
| entité `+0x0C`, `+0x10`, `+0x28`, `+0x48` | `ParentEntity`, `Status` (1 à 3 actif), `PlatformEntity` (test de nullité), `EntityRefId` (héros −1) |
| entité `+0x6C`, `+0x88`, `+0xB4`, `+0xB8` | `Flags` (`Collidable` 0x80), `TargetAnimationId` (uint), `AnimFlags` (0x80), `ForceZ` (int signé) |
| entité `+0x12C`, `+0x130` | `RidingEntity`, `XCollisionEntity` (proxy) |
| N `[0x80134360]` | aucun membre : la DLL parcourt `SpawnedEntities` (héros d'abord, puis l'ordre des slots) |
| octets d'instruction | `variables[k]` |
| table de sauts `0x80023C30` | le `switch (searchType & 0x7F)` |
| tampon `0x8013D8D8…` | la liste rendue par la recherche (écrite par le même appel). La copie de 65 mots de `0xB2` lit des mots non écrits au-delà de n1, sans jamais les comparer : classe `match_buffer_copy` |
| cases de `9E` | octet bas du mot de 16 bits de la case |

**Limites déclarées (INCONNU).**
- Type de recherche ≥ `0x8C` ou id brut invalide : le binaire quitte (appel BIOS inféré). La DLL journalise.
- `9E` au-delà de l'index linéaire 3120 : indéfini.
- Id brut 0 : peut apparier un slot libre effacé dans le binaire, jamais dans la DLL. À éviter dans les tests ; aucun site sm3 ne l'utilise.
- Contenu d'un slot libre : supposé nul.

## 3. Tests nommés (75, `OUT/tests.tsv`)

Les montages :
- **M1** : le `Context` de `AlundraClassAndBranchOpcodesTests.cs:25-35` et son `Run` (`:47-67`), avec `ResultBefore = 7`. Le héros porte `IsPlayer = true`, `EntityRefId = -1`, `Status = Normal`, et se trouve dans `SpawnedEntities` ET `PlayerEntity`. Un `new EntityScriptProxy()` nu vaut `EntityRefId = 0` : à poser explicitement.
- **M2** : octets réels au pc réel, sur le modèle de `IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27` (`AlundraEventProgramRunnerTests.cs:4155-4181`). Racine par `SaveGameDirectorTestSupport.FindProjectRoot()` : échec si absente, pas de saut, comme `ArcRun`.
- **M3** : `ArcRun`, `RealController` et `Prefabs`.
- **M4** : runner nu avec un faux `ICellMutator` (9E).

Mapping des pointeurs d'un vecteur :
- `xcoll` devient `XCollisionEntity` (proxy).
- `platform`, `riding` et `parent` deviennent le `LogicContextEntity` du slot visé.

Valeur attendue : `Result` du vecteur et `CodeIndex == taille`. Rouge d'aujourd'hui : le Result reste à sa valeur de départ (7, ou 0 ou 1 pour les vecteurs « écrase un Result périmé »), le `Kind` de trace est `UnknownSkipped`, `CodeIndex` avance de la taille de la table.

**Fichier `Alundra.Tests/AlundraContactGroupAndForceZOpcodesTests.cs` (M1)** : A9, AA, B2, B4 à B7, tous rouges aujourd'hui.

| Groupe | Tests (identifiants, vecteurs épinglés) |
|---|---|
| A9 (14) | `U-A9-01`..`14`. Contact du héros, contact nul, contact hors groupe, réel 440 B[9] (id 24, référence = héros), statut de la référence en id brut (1, 3 vrai, 4 faux), statut du candidat ignoré en id brut, 2e de deux, types 0, 2, 3, 11, héros seul, héros logique. |
| AA (11) | `U-AA-01`..`11`. Même structure, plus « seul le dernier membre bloqué », statuts de membre, forme réelle 440 à trois blocs. |
| B2 (14) | `U-B2-01`..`14`. Réel 258 id 3 porté, non porté, porté mais statut 4, deux entités du même id dont une portée, groupe 1 vide, référence inactive, types qui se recouvrent, groupe 2 vide, 64 slots (dernier et premier), groupes 5/6, recouvrement partiel, identité et non `EntityRefId`. |
| B4/B5/B6 (18) | Pour chacun : table `ForceZ` sur 12 valeurs pour la cible héros (`81`) et la cible soi-même (`80`), premier résultat seul (5 ordres), saut d'un inactif, groupe vide, Result périmé. Valeurs : 0, ±1, `0x7FFFFFFF`, `int.MinValue`, ±0x10000, ±0xFFFF, 0x8000, ±0x100. |
| B7 (6) | `U-B7-01`..`06`. Égal, 32 bits contre octet (`0x141` contre `0x41`, `0xFFFFFFFF` contre `0xFF`), rôle des opérandes, tout membre, vide et inactif, Result périmé. |
| Transverses (3) | `U-ALL-01` (taille et rien d'autre écrit), `U-ALL-02` (la recherche part de l'entité logique, pas du héros), `U-LBL-01` (les 7 libellés ; rouge : `A9` et `AA` vides, `B4`..`B6` faux). |

Chacun des 213 vecteurs écrits à la main est épinglé par au moins un de ces tests (vérifié par script).

**Fichier `Alundra.Tests/AlundraCellCountOpcodeTests.cs` (M4, sous réserve de Q-SM3-2)** : `U-9E-01`..`09` (9 tests). Masque de bit 1 (pas bit 2, octet haut masqué), égalité au seuil, avance en x, largeur ou hauteur nulle, colonne ≥ 52, ordre des opérandes, Result périmé, pas de lecteur.

**Fichier `Alundra.Tests/AlundraSm3RealOccurrenceTests.cs` (M2)** : une `[Theory]` sur les 40 instructions distinctes. Elle vérifie les octets au pc dans la carte chargée, puis exécute `[octets, 0xFF]` avec un monde déduit de la forme. Elle exige `Kind == Implemented` et `CodeIndex == taille`. Rouge : `UnknownSkipped`.

**Fichier `Alundra.Tests/AlundraSm3SiteArcTests.cs` (M3)** : `[Collection(AlundraMusicPlayerSingletonCollection.Name)]`, tables de sites sur le modèle de `AlundraHeightTargetSitesTests.cs:21-88`. Voir §4.

**Ordre des commits.** Les tests rouges sont montrés avant le code. Le commit qui porte le code porte aussi les tests, les libellés, `ImplementedOpcodes` et le retrait des lignes (bissectabilité, précédent L1-1/L1-2).

## 4. Contrôles sur cartes réelles (`OUT/arcs.tsv`)

### 4.1 Faits statiques (FAIT)

- Les 11 cartes (54, 159, 160, 258, 298, 421, 423, 424, 425, 426, 440) ont monde, événements, tilemap, dialogues, et tous leurs ids de préfabriqué résolus dans `AssetInfos.json` (`work/mapcheck.py`).
- Aucun test existant ne charge ces cartes (rg).
- Aujourd'hui, le Result périmé vaut le Result initial 0 à presque tous les sites. Seuls 4 sites sont atteints après un écrivain de Result : `54 B[14] @1226` et `@1244` (Result 1), `440 B[9] @628` (Result 1) et `425 C[12] @402` (Result 0) (`work/stale_result.py`).

### 4.2 Conséquences de lecture pour les 10 arcs (tous INFÉRENCE de décodage, rien n'est exécuté)

Observabilité commune : `ArcInstruction` n'a pas de Result. Chaque arc lit donc la branche prise (le pc de l'instruction suivante) dans la trace. Clé de trace : (slot, `ProgramStart`, pc). Aucune modification d'`AlundraArcSupport.cs` n'est nécessaire.

| Arc | Site | Aujourd'hui (décodé) | Signal et valeur attendue |
|---|---|---|---|
| S3-A1 | 424 C[3] `AA 81` @700 (bloc enregistrement 3, tuile (37,27)), drapeau `0x04DB` clair, héros à l'OUEST du bloc, direction droite tenue | Result périmé 0, donc `04 FB FF` repris : `73 28` @697 à chaque tick, `(C,717,0x05)` n'a jamais lieu | `(C,692,717,0x05)` après exactement 40 `74` @705 consécutifs (compteur `0x28`) |
| S3-A1b | idem, côté EST, direction gauche | rien | `57` @708 prend la case d2 (−11) vers `(C,692,697,0x73)`, jamais 717 |
| S3-A2 | 421 C[8] `AA 81` @1467 puis `4B` (enregistrements 8 à 12) | `4B` relance le programme à chaque tick | `(C,1464,1484\|1492,0x5B)` |
| S3-A3a | 440 B[9] `A9 18` @628 (id 24 = Jeune Meia, tuile (25,9)), drapeau temporaire `0x83E8` posé APRÈS le chargement, Carré pressé sans contact | `2F` laisse un Result périmé à 1 : `05 FA 80` @633 s'exécute à tout appui de Carré | la branche fausse `(B,612,630)` puis `(B,612,640)` ; `(B,633,0x05)` ne doit PAS s'exécuter |
| S3-A3b | idem avec contact contre Meia | vert par coïncidence : c'est une garde | `(B,612,633,0x05)` puis `(B,612,636,0x02)` |
| S3-A4 | 54 B[14] `B5 81` @1226 et @1244 | Result périmé 1 (3B @1164), donc les deux attentes passent sans attendre | voir §4.4 |
| S3-A5 | 160 C[6] `B6 80` @1013 (boule de fer enregistrement 6) | Result périmé 0 : la séquence d'écrasement (`8E` @1031) part dès les 4 premiers pas | `(C,960,1018,0x0A)` si `ForceZ==0` ; `(C,960,1031,0x8E)` ne doit pas apparaître |
| S3-A6a | 258 B[6] `B2 03/04/05/06/08 8B` | même flot (le Result périmé vaut 0 à chaque retour), mais `Kind = UnknownSkipped` ×5 | 5 `B2` « Implemented », 5 `03` sans saut |
| S3-A6b | idem avec `PlatformEntity` de l'enregistrement 3 posé PAR LE TEST (indépendant d'E20.g1a) | `(B,632,1056,0x55)` n'a jamais lieu | saut `642+0x19E = 1056` ; id 8 : `1103` ; 298 id 5 : `1157` (le `05` est doublé @738/@744) |
| S3-A7 | 440 C[2], C[3], C[4] : le MÊME `AA 81` @1140 (blocs 1, 2, 3, sprite 25) | `04 FD FF` repris vers @1139 | trois `ProgramStart` (1068, 1088, 1108) pour un seul pc ; vrai pour le seul bloc poussé |

- Les clés de ces arcs ont des appels reconnus par le cadre existant (`Has`, `FirstIndexOf`, `HoldDirections`, `Press`).
- Les limites d'images sont à fixer par T0 d'après une première mesure (règle de §1.3), avec une valeur initiale donnée dans `arcs.tsv`.
- Les boutons de Square-pour-A9 s'appuient sur `Press(PadState.Square)`.

### 4.3 Peut-on les faire tourner dans ArcRun maintenant ?

- Statique : oui (§4.1).
- Dynamique : INCONNU pour les 11 cartes. T0 doit lancer un arc de construction de 6 images (`RealController`, `Prefabs`) sur chacune.
- Prémisse à mesurer : le héros est bloqué par les blocs de sprite 177, 25 et 89. Ces en-têtes ne sont pas parmi les 17 soulevables (`docs/plan-e20-heros.md:6558`), donc ce sont des obstacles ordinaires si le préfabriqué est collidable (INFÉRENCE).
- Si un arc ne peut pas tourner : le test de remplacement est le test d'octets réels `R-*` avec un monde bâti à la main (M1/M2), comme indiqué dans la dernière colonne d'`arcs.tsv`. Le défaut serait alors consigné comme limite d'E19.d2b.

### 4.4 `XCollisionEntity` et `ForceZ` du héros au moment du script

**Binaire (FAIT).**
- `+0x130` n'est écrit sur une entité que par `MoveEntity` `0x80037E34` : effacé en `0x80037E80` si l'entité est portée, posé en `0x80037F08` sinon. Il y a aussi l'initialisation en `0x800426A0`.
- Les autres stores sur `+0x130` du texte entier visent des structures globales d'interface (`0x8002BB1C`, `0x800588B8`, `0x8005941C`) ou des tableaux sur la pile (scan `work/scan_fields.py`).
- La passe physique `0x80038364` (appelée de `UpdateEntities` en `0x8003B3E0`) parcourt la liste d'entités et appelle `MoveEntity` pour chacune en `0x8003847C`.
- Un script voit donc le contact du tick PRÉCÉDENT, et `ForceZ` tel que laissé par la physique du tick précédent.
- Les programmes B tournent avant `UpdateEntities` (`0x8002E100` puis `0x8002E108`). Les programmes C tournent avant la physique du même tick.

**DLL (FAIT).**
- Ordre de `WorldProxy.cs` : `RunMapEventsPass` en `:2568`, puis `RunEntityBlock` en `:2582`.
- Dans `RunEntityBlock` (`:2905-2988`) : `MovePlayerOnTickPad` `:2927`, `TickPrograms` `:2939-2942`, compteurs `:2960`, listes `:2962-2966`, horloge `:2970-2974`, physique `:2976-2980`.
- `XCollisionEntity` est remis à nul puis réécrit à chaque `MoveControllerAndPullPosition` (`EntityScriptProxy.cs:2424-2431`), et conservé pendant un gel (`WorldProxy.cs:2790-2793`).
- Conclusion (INFÉRENCE, non exécutée) : l'ordre d'écriture et de lecture est celui du binaire pour 440 B[9], 424 C[3] et 54 B[14].
- Le contact du tick précédent est aussi établi côté binaire par l'oracle d'E20 (`docs/plan-e20-heros.md:6544`).
- Hors monde piloté, sans `Game`, l'ordre diffère : seul un arc `RealController` est valable.

**Prédiction pour 54 B[14] (INFÉRENCE).**
- L'impulsion de l'animation 2 du héros est 1280 × 160 = 204 800, avec décroissance de 32 768 par tick (épingles UW-4, `docs/plan-e19-opcodes.md:3552`).
- Le premier `ForceZ` négatif vaut 204 800 − 7 × 32 768 = −24 576.
- Le nombre d'exécutions de `B5` @1226 est donc 7 ou 8, selon que la décroissance du tick d'impulsion est déjà appliquée au passage de script suivant. T0 fixe laquelle des deux dans la DLL.
- Aujourd'hui ce nombre est 1.

### 4.5 Mesures de T0 (liste fermée)

- **P1** : arc de construction de 6 images sur les 11 cartes.
- **P2** : héros contre un bloc, direction tenue 60 images, identité de `XCollisionEntity` par image.
- **P3** : trajectoire de `ForceZ` du héros après `1A 02` en 54, tick par tick.
- **P4** : `ForceZ == 0` de l'enregistrement 6 de la 160 au repos.
- **P5** : `XCollisionEntity` des PNJ de la 440 (Jeune Meia) quand ils marchent vers le héros.
- **P6** : test d'ordre sur `TickTraceForTests` (`mapEvents` avant `programs` avant `physics`).
- **P7** : `EntityRefId` des enregistrements 3 à 8 des cartes 258 et 298 égal à leur index.

## 5. Preuves de retrait, mutants, arrêts, budget, retour arrière

### Mutants

Au total 56 mutants d'une ligne, évalués sur les 837 mondes. Chacun est tué par au moins un vecteur écrit à la main, donc par un test nommé (`vectors/mutants.json`).

| Opcode | Mutants (nombre, exemples) |
|---|---|
| A9 | 3 : inversé en AA, premier résultat seul, référence = héros |
| AA | 3 : mêmes |
| B2 | 7 : intersection de g2 avec lui-même, tous au lieu d'un, comparaison par `EntityRefId`, 2e recherche avec le type de la 1re, g1 seul, premier de chaque, dernier de g2 seul |
| B4 | 5 : `>=`, non signé, 16 bits, dernier, n'importe lequel |
| B5 | 6 : `<=`, libellé ">= 0" porté en code, non signé, 16 bits, dernier, n'importe lequel |
| B6 | 5 : `!=` (libellé), 16 bits, dernier, n'importe lequel, vide vrai |
| B7 | 4 : opérandes permutés, octet bas, opérande étendu de signe, premier seul |
| 9E | 7 : pas d'avance en x, masque 4, octet haut, `>`, x/y permutés, w/h permutés, pas de passage à la rangée suivante |
| Tous | 2 par opcode : « Result périmé gardé sur faux », « gardé sur vrai » |

Mutants équivalents (non tuables, prouvés par lecture) :
- Retirer la garde de nullité de A9 : la liste ne contient jamais de nul.
- Retirer le court-circuit `n1 == 0` de B2 : seul le journal du type illégal du 2e groupe change.
- Changer l'ordre d'itération d'un prédicat « il existe » (A9, AA, B7).
- Utiliser `_width` au lieu de la constante 52 pour 9E : les 483 cartes font 52×60.

### Preuves de retrait (suivent des définitions de tests)

- **Le code seul est retiré** (les 7 cas, les libellés et `ImplementedOpcodes`, tests et liste gardés comme dans le commit) : rouges exactement les 75 tests de §3, plus `(c1)` de sm0 (« tout site atteint et sauté est une ligne » : 42 sites sautés non listés). Rien d'autre, car aucun test existant n'exécute ces opcodes (rg).
- **Les 42 lignes seules sont remises** : rouges exactement `(c2)` de sm0 (lignes périmées) et `(k1)` (comptes).

### Conséquences sur la liste du corpus (D-SM0-3)

42 lignes (`OUT/corpus-lines-sm3.tsv`, avec le libellé d'avant et d'après) :

| Opcode | Lignes | Cartes |
|---|---|---|
| `AA` | 24 | 159, 160, 421, 423, 424, 425, 426, 440 |
| `B2` | 10 | 258, 298 |
| `A9` | 4 | 440 |
| `B5` | 3 | 54, 440 |
| `B6` | 1 | 160 |

- 40 instructions distinctes : 440 `C[2]`, `C[3]`, `C[4]` partagent `@1140` (3 lignes pour 1 instruction).
- Règle 5 de sm0, avant : 510 lignes, 508 instructions, 96 cartes, 21 opcodes. Après : **468 lignes, 468 instructions, 91 cartes, 16 opcodes**. Comptes par propriétaire : sm1 187, sm2 129, **sm3 0**, sm4 90, sm5 17, sm7 42, sm8 3. Somme 468.
- Les cartes 423, 424, 425, 426 et 440 sortent de la liste. 54, 159, 160, 258, 298 et 421 gardent les lignes d'un autre propriétaire.
- `ClosedOwners` reçoit `sm3` dans le même commit (règle 4, test `c4`).
- Variante Q-sm-4 « `B2` attend E20.g1a » : sm3 vaut 32 lignes, 10 lignes passent à un propriétaire neuf `g1a` à entrer dans le vocabulaire (D-SM0-4), et l'après devient 478 lignes, 478 instructions, 91 cartes, 17 opcodes.

### Acceptation proposée

1. Tests de §3 rouges d'abord (aucune garde verte d'avance), puis verts, valeurs inchangées. Une valeur contredite est un arrêt.
2. Code de test existant touché, liste fermée : `IntroTraceHarnessTests.cs` (`ImplementedOpcodes`), la classe et la liste de sm0 (`ClosedOwners`, comptes, 42 lignes). `AlundraStoryChainOpcodeAudit.cs` n'est pas touché. Rien d'autre.
3. `Alundra.Tests` : build Release à copie neutralisée puis Debug en dernier, `dotnet test … --blame-hang-timeout 300s`, zéro échec, `cmp` sans écart. Les traces ne bougent pas (la 389 n'a aucun des 8 opcodes).
4. `check_names.py --rules` (R7) à 0 sur l'arbre final.
5. Recette de l'auteur, résultat attendu (INFÉRENCE de décodage) :
   - Nirude, cartes 421 et 423 à 426 : un bloc poussé 0,8 s glisse d'une case, pas dans la mauvaise direction.
   - Fairy cave 160 : la boule de fer roule et rebondit sans s'écraser tout de suite.
   - Elenes Nightmare 54 : la montée du saut du héros est visiblement attendue avant l'animation suivante.
   - Meia Dream 440 : Carré appuyé loin de Meia ne déclenche plus rien.
   - Inoa 258 et 298 : aucun changement visible.
6. Verifier frais : CONFIRMED sur « chaque gestionnaire égale le binaire », avec rejeu de l'oracle et des mutants.

### Arrêts, budget, retour arrière

- **Arrêts** : une valeur du plan contredite par le binaire ou les vecteurs ; une lecture non mappée ; un test vert avant le code ; un rouge autre que l'ensemble attendu ; une clé du corpus qui change ; un arc qui n'atteint pas son signal sans explication ; R5, R7 ou sm0 non faites ; un nom refusé par la garde R7 ; E20.g1a mergée avant (le recensement de ses recherches de types 4 et 11 doit nommer `B2`, `plan-e20-heros.md:6649-6650`) ; un besoin de toucher au moteur, au convertisseur ou aux données ; un abandon par `--blame-hang-timeout`.
- **Budget** : un `executor`, deux tentatives ; `plan-verifier` avant ; verifier frais après.
- **Retour arrière** : retirer le commit unique (cas, libellés, `ImplementedOpcodes`, lignes, `ClosedOwners`, tests). 9E et le membre d'interface sont un commit séparé, qui se retire seul.

## 6. Questions à l'auteur et dépendances

**Q-SM3-1 (= Q-sm-4) : `0xB2` maintenant ou avec le portage.**
- Vu en jeu : dans les boutiques d'Inoa (cartes 258 et 298), les articles sur le comptoir (Herbe médicinale, Potion S, Extrait magique, Récipient de vie, Veste en argent) deviennent soulevables par leur programme C[4] (`62 80 80 03` @1640). Le script demande « le héros porte-t-il l'article N ? » pour lancer l'achat. Le héros ne peut rien porter avant E20.g1b ; `PlatformEntity` n'est jamais écrit aujourd'hui (seule copie du clone, `EntityScriptProxy.cs:2609`).
- Option A, porter maintenant (recommandée) : rien ne change en jeu aujourd'hui, car le Result périmé vaut déjà 0 sur tous les chemins. Coût : un cas de 8 lignes et 14 tests. Les arcs ne figent jamais « Result 0 » (S3-A6b pose `PlatformEntity` à la main).
- Option B, attendre : 10 sites restent sautés et sm0 doit créer un propriétaire `g1a`.

**Q-SM3-2 (= Q-sm-6 réduit) : les opcodes sans site, `B4`, `B7`, `9E`.**
- Vu en jeu : rien dans les 483 cartes.
- Défaut proposé : porter `B4` et `B7` maintenant, car ils partagent le corps de `B5`/`B6`/recherche et leurs libellés sont à corriger de toute façon. Porter `9E` avec le lecteur de case et un ADR, car E19 ne se clôt sur « aucun opcode sauté » que si tous le sont ; le coût est un membre d'interface et un journal hors grille.
- Option : laisser `9E` sauté, sans code mort, mais E19 ne se clôt alors pas dessus.

**Q-SM3-3 (= Q-sm-7/8 : libellés).** Accord sur les textes du tableau R7, dans le même commit que le code. Défaut : oui.

Décision technique sans question (P3) : `PredicateOpcodes` (`AlundraStoryChainOpcodeAudit.cs:57`) reste inchangé. La règle 4 ne lit que la liste de la chaîne, qui n'a aucun site sm3. Les précédents se contredisent (L1-2 inchangé, L2-2 ajouté).

**Dépendances.**
- **sm0** : prérequis dur (liste, `ClosedOwners`, garde R7, R5).
- **E20.g1a** : indépendant pour le code de sm3, car les tests posent `PlatformEntity` à la main. Si g1a passe d'abord, seuls les épingles « Result 0 tant que g1a manque » deviendraient fausses, et sm3 n'en écrit aucune. Dans tous les cas le recensement de g1a doit citer les recherches de `B2`.
- **E20.g1b / h3** : seuls à rendre `B2` vrai en jeu (`PutInHands`, `docs/plan-e20-heros.md:6584`).
- **D5b** : aucune. Aucun site sm3 n'utilise les types 5 et 6 (`RidingEntity`).
- **E20.c2a** : aucune (pas de vie du héros lue).
- **E18** : aucune trouvée dans les 42 sites (INCONNU pour le reste des programmes).

## 7. Points complémentaires

- **Sens de `A9`/`AA`** (FAIT, `lw 0x130(s0)` contre le résultat pour A9, `lw 0x130(res[i])` contre `s0` pour AA) : le bloc de Nirude dit « le héros est bloqué par moi » (`AA 81`). La Jeune Meia de la 440 dit « je suis bloquée par le héros » (`A9 81`), et la salle de Meia teste « le héros est bloqué par l'entité d'id 24 » (`A9 18`).
- **Libellés `B4` à `B6`** : confirmés `>`, `<`, `==` sur le premier résultat par le binaire et les vecteurs. Les corps de la décompilation étaient justes (`EntityEventHandlers.cs:3411-3463`).
- **Nuance sur la décompilation** : `A9` et `AA` sont identiques à la décompilation, `B7` est corrigé dans la copie du sous-module (`:3466`), `9E` n'avance pas en x (`:2951`). Elle se lit, ne se corrige pas.
- **Masque de `9E`** : `andi 2`, le bit d'indice 1 de l'octet de marchabilité (écart O2 de `check.md`). Le mot de 16 bits contient aussi GroundProperty, masqué.
- **Id brut** : 3, 4, 5, 6, 8 en 258/298, 24 en 440. Un id brut invalide est fatal dans le binaire (O1) ; ces ids sont des enregistrements de leurs cartes.

## 8. Limites

- Rien de C# n'a été exécuté. La conformité de `EntitySearchService.cs` au binaire repose sur la traduction python (INFÉRENCE sur le C# lui-même, relu à la main par le critique de l'annexe).
- Les arcs réels n'ont pas été lancés : géométrie des poussées, tuiles de départ, limites d'images, repos à `ForceZ == 0` du préfabriqué de la 160, tout cela est INCONNU jusqu'à T0.
- Les textes de recette décrivent ce que le décodage du programme prévoit, pas une observation.
- Le contenu d'un slot libre du binaire et l'appariement de l'id brut 0 sont INCONNUS.