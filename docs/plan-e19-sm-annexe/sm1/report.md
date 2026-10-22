# Rapport E19.sm1 — préparation en lecture seule (2026-10-18)

Opcodes `0x60 0x61 0x3C 0x3D`, 187 sites dans 11 cartes. DLL lue au HEAD `36ac0686`, branche `chantier/restructuration-dll-r3`.

Je n'ai rien modifié dans le dépôt, la décompilation, `data-extracted/`, `alundra-project/` ni le binaire. Je n'ai lancé aucun build, aucun test ni aucun export. Tout est écrit sous :

`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm1/`

Ce dossier sera appelé `OUT/` dans la suite.

Étiquettes : **FAIT** (ligne citée, adresse ou sortie de commande), **INFÉRENCE** (déduit, avec la source), **INCONNU**.

## 0. Verdict

**La matière technique de sm1 est complète : règles, valeurs, tests, mutants, retrait et conséquences de la liste.**

Restent bloquants trois points qui ne relèvent pas de la préparation :

1. **B0** : Q-E19-SM1 est ouverte, et `docs/plan-restructuration-dll.md:160` interdit toute tranche E19 pendant le chantier.
2. **Prérequis hérités de sm0** : R5, la garde R7 et sm0 exécutée avant sm1. sm0 est READY (`docs/plan-e19-opcodes.md:12122`) mais pas exécutée.
3. **T0** : la mesure dynamique d'`ArcRun` sur les cartes 27, 400 et 398. Aucun arc n'a jamais chargé ces cartes (A20, `check_sm12.md`), et je ne peux pas exécuter de test.

Deux arcs sont remplacés :

- 226 B[3] par un test d'octets réels. Le nombre d'images de la scène est INCONNU.
- 27 C[2] par 27 C[26]. La « bonne pièce » de C[2] change toutes les 24 images.

Les résolutions point par point du §8 de `check_sm12.md` sont au §10.

## 1. Ce que j'ai rejoué ou vérifié

**Désassemblage.**
- **FAIT** : j'ai redésassemblé moi-même les 4 gestionnaires depuis la table `0x80098FAC + 4*op` : `0x8003E558` (0x3C), `0x8003E64C` (0x3D), `0x8003F3F8` (0x60), `0x8003F488` (0x61).
- **FAIT** : mon listing (`OUT/binary/handlers-sm1.txt`) est identique, instruction par instruction, à `sm1-sm4/check/listings/op_*.txt` (61 + 47 + 36 + 35 instructions, 0 écart).
- **FAIT** : SHA-1 du binaire `a705f8dab665645af5dfaf8fb348c7926b5caa58`, égal à celui de l'annexe.

**Oracles.**
- **FAIT** : copie de `work/oracle/oracle.py`, graine 7 : 31 500 exécutions sur 21 opcodes, 0 écart. Histogrammes à deux valeurs : 0x3C {0:1434, 1:66}, 0x3D {975, 525}, 0x60 {1390, 110}, 0x61 {1375, 125} (`OUT/binary/annex-oracle-run-seed7.txt`).
- **FAIT** : copie de `check/myoracle.py`, graine 11, 2 000 exécutions par opcode : 0 écart (`OUT/binary/critic-oracle-run.txt`).
- **FAIT** : mon propre générateur, §4 : 1 384 vecteurs, 0 écart entre le vrai gestionnaire, mon modèle et le modèle de la DLL.

**Export.**
- **FAIT** : les 187 sites relus dans l'export du jour sont conformes à `skipped-sites.tsv` (opcode et opérandes, 187 sur 187).
- **FAIT** : les 178 sites à identifiant brut nomment un enregistrement activé de leur carte. C'est l'omission O1 du critique, dont le validateur `0x8003C914` est fatal.
- **FAIT** : les types de groupe des sites sont des identifiants bruts (178 sites) et `0x81`, le héros (8 sites : 136 C[5] et C[10], 77 C[14 15 16 17 24], 78 C[19]). Aucun autre type n'est utilisé dans le corpus.

## 2. Écarts relevés dans l'existant

1. **La « comparaison signée » de `0x3C`/`0x3D` ne distingue rien.**
   - **FAIT** : les bornes sont des octets 0..255 et les `slt` du binaire sont signés. Comparer la tuile en non signé donne le même résultat que le signé.
   - Preuve exhaustive : `OUT/vectors/equivalent-mutants.txt`, 134 676 480 cas, 0 différence.
   - Le plan et `check_sm12.md` M10 disaient qu'une tuile négative tuait ce mutant. C'est faux : c'est un mutant équivalent.
   - Les vrais bords sont : l'octet d'opérande lu en signé (≥ 0x80), la troncature de la tuile à un octet (256, 261, 65 536), les extrêmes 32 bits et −1 (les mutants non équivalents).
2. **O14/I9 est levé.**
   - **FAIT** (commentaire de la DLL, `WorldProxy.cs:3254-3258`, E19.r R3 / ADR-0024) : la passe de recyclage remet chaque case `FlagToDestroy` à son modèle, état 0 et `+0x48 = -1`.
   - Une case libre ne peut donc pas être retenue par un identifiant brut.
   - La valeur du modèle est écrite à l'exécution (`0x8003B21C`, `0x80134368` hors image) : je ne l'ai pas relue dans le binaire.
3. **La valeur « rouge » d'aujourd'hui n'est pas « `Result` périmé » sur 18 sites.**
   - **FAIT** (dataflow sur le graphe de contrôle, `OUT/corpus/sm1-sites.tsv`) : c'est le `Result` écrit par un `0x43`, un `0x70` ou un `0x3B` voisin (§6).
   - Conséquence : le comportement actuel est un faux positif visible dans plusieurs programmes, pas seulement une inertie.
4. **Q-sm-5 est périmé** : les objets des cartes 27, 29 et 400 sont « transportables », donc de la famille d'E20.g, et g1a, g2a, g2b et g1b sont READY.
5. **La grille statique de `RidingEntity` est complète pour les objets des cartes 27 et 400.**
   - **FAIT** : le plateau (sprite 108) a les drapeaux `0x60C0` ; les objets (107, 111, 112, 113) ont `0x332384`.
   - **FAIT** : les `AnimSets` de ces sprites n'ont que les drapeaux 0 ou 4 (jamais `0x80`).
   - **FAIT** : aucun programme sm1 n'efface Gravity, Collidable ou NoRiders d'un objet ou d'un plateau. Ils font `63 N 00 02` (efface `0x0200`), `62 80 00 01` (136 C[5], pose Gravity sur l'interrupteur) et `62 80 80 80` / `63 80 80 00` (159 C[13], le bulle bascule son propre Collidable).
   - **INCONNU** : seule la 159 C[13] est dynamique.

## 3. Règles d'exécution SM1-R1 à R12 (noms du HEAD)

`Dispatch` est dans `Alundra/Events/EventProgramRunner.cs:563-2105`. Les 4 `case` s'insèrent entre `:1985` et `case 0x42` (`:1987`). Ne pas recopier le commentaire `:1961-1963` (« la décompilation s'arrête avant N », réfuté par C R1).

- **R1 — `0x60`, taille 2.**
  - Le groupe est `EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity)`, comme `0x43` (`:2012`) et `EntityInArea` (`:3271`). `entity` est l'entité logique : `LogicEntity ?? owner`, déjà résolue par `RunOneScriptCall`.
  - `Result` = 1 si un membre a `RidingEntity` non nul et identique à `entity.LogicContextEntity`. Sinon 0, groupe vide compris.
  - Aucun test de statut, aucun test de `BlockedByEntity`.
  - Ne pas réutiliser `IsRidingTheLogicEntity` (`:2641-2645`) : il ajoute `Status.IsActive()` et `BlockedByEntity == null` que le binaire n'a pas (`0x8003F444-0x8003F44C`). Le mutant `reuse_IsRidingTheLogicEntity` est tué par RP-05.
- **R2 — `0x61`, taille 2.**
  - Même groupe. Appeler la recherche d'abord, comme le binaire.
  - `Result` = 1 si `entity.RidingEntity` est non nul et identique au `LogicContextEntity` d'un membre. Sinon 0.
  - `RidingEntity` nul donne toujours 0, car le binaire compare `+0x12C` à une adresse non nulle (`0x8003F4D0-0x8003F4D8`).
- **R3 — `0x3D`, taille 7.**
  - Parcourir `PlayerEntity` puis `SpawnedEntities` (boucle indexée, pas de `foreach` sur l'interface).
  - Un candidat compte s'il est `IsLoadedNormalOrDeactivated` (statuts 1, 2 ou 3, donc pas `IsActive()`) et si `v[1] ≤ TileX ≤ v[2]`, `v[3] ≤ TileY ≤ v[4]`, `v[5] ≤ TileZ ≤ v[6]`.
  - Les bornes sont inclusives, les `v[i]` sont des octets non signés 0..255 (`FillDataFromCommand`, `:514-538`), et `TileX/Y/Z` des `int`.
  - L'entité logique n'intervient pas. `Result` est toujours écrit (binaire `0x8003E68C-0x8003E6FC`).
- **R4 — `0x3C`, taille 7.** R3, plus `EntitySupport.IsEligibleSubject(candidate)` (`Alundra/Physics/EntitySupport.cs:73-76`), c'est-à-dire :
  - `(Flags & Collidable) != 0` ;
  - `(AnimFlags & 0x80) == 0` ;
  - `PlatformEntity == null`.

  C'est le contraire de la décompilation (`Script_60_03C`, `EntityEventHandlers.cs:1243-1271`) ; le binaire l'emporte (D-E19-2).
- **R5 — Inclusion du héros.**
  - Pour `0x60` et `0x61`, le héros entre par l'argument `playerEntity` du service (type 1) et par `SpawnedEntities` (types 2 et 4).
  - **FAIT** : en production `SpawnedEntities` contient le héros au slot 0 (`WorldProxy.cs:2304`, `IEntityWorldContext.cs:29`). La fausse `Context` de `AlundraClassAndBranchOpcodesTests.cs:25-35` ne le met pas : le montage doit l'y ajouter.
  - Pour `0x3C` et `0x3D`, visiter `PlayerEntity` d'abord. Un héros visité deux fois est sans effet, car le prédicat est « il existe ».
  - Pas de mutant équivalent ici : `spawned_only` est tué par ZP-3D-08, ZP-3C-08 et ZP-3C-23 (héros seulement `PlayerEntity`).
- **R6 — Entité logique.**
  - La référence de la recherche et le porteur comparé sont `entity`, jamais `owner`.
  - Programmes B (`WorldProxy.cs:3195-3214`) : owner = héros, logique = `mapEvent.Entity ?? player`, donc le héros ou la cible d'un `0x43`/`0x42`.
  - Sites concernés : 159 B[5] (logique = héros), 398 B[1..7] (cible du `0x43`), 226 B[3] (aucun effet, `0x3D` l'ignore).
- **R7 — Écritures.** `state.Result` seulement. Aucun champ d'entité, aucun `Parameters`, aucun état. Trace `Implemented`. `Parameters[1] := 0` est la remise du runner, commune à tous les opcodes.
- **R8 — Libellés de `EventOpcodeSizeTable.cs`** (aucun test ni trace ne les épingle, **FAIT** par recherche dans `Alundra Alundra.Tests tools docs`) :

| Ligne | Actuel | Proposé |
|---|---|---|
| `:106` | `If entity in zone with flag 0x80` | `If entity in zone (Collidable, no NoEntityCollision, no PlatformEntity)` |
| `:107` | `If entity in zone` | inchangé (juste) |
| `:142` | `Is riding entity` | `If entity of search group riding me` |
| `:143` | `Is riding entity 2` | `If riding an entity of search group` |

  Ne pas recopier la colonne de sens de `opcodes.tsv` pour `0x3C` : elle est inversée (O5).
- **R9 — Miroirs.**
  - `IntroTraceHarnessTests.ImplementedOpcodes` (`:332`) reçoit `0x3C, 0x3D, 0x60, 0x61` (précédent L1-R8). D-SM0-5 le laisse à cette tranche.
  - `PredicateOpcodes` (`AlundraStoryChainOpcodeAudit.cs:57`) est inchangé : aucun site sur la chaîne.
  - `IsPorted` (`:75`) bascule seul pour les 4 opcodes. La sonde est sur hôte nu, donc le code doit être sans exception avec `NoOpEntityWorldContext` et un `EntityScriptProxy` nu.
- **R10 — Liste fermée (sm0).** Dans le même commit que le code :
  - retirer les 187 lignes `sm1` de `corpus-skipped-opcodes.tsv`, en suppression pure : les 323 autres lignes restent identiques à l'octet ;
  - ajouter `sm1` à `ClosedOwners` ;
  - changer les comptes épinglés de la règle 5 (§7).
- **R11 — Pas de code d'infrastructure.** Aucune modification de `RidingEntity` ni de `EntitySupport`.
  - **FAIT** : le recalcul de `RidingEntity` est par tick depuis E20.a (`WorldProxy.cs:2962-2966`, étape « lists »). Le texte `:2994-2995` « une fois par image » est périmé (A9).
- **R12 — Aides.** Deux méthodes statiques privées de `EventProgramRunner` (`AnyEntityInZone`, `IsInZone`). Aucun type ni fichier neuf : la garde Q-R-7 n'est pas déclenchée.

Squelette (INFÉRENCE : non compilé) :

```csharp
case 0x60: // E19.sm1, Script_96_060 @ 0x8003F3F8 PER THE BINARY
{
    var carrier = entity.LogicContextEntity;
    var any = false;
    foreach (var member in EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity))
        if (member.RidingEntity is { } restsOn && ReferenceEquals(restsOn, carrier)) { any = true; break; }
    state.Result = any ? 1 : 0; return 2;
}
case 0x61: // Script_97_061 @ 0x8003F488
{
    var any = false;
    var group = EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity);
    if (entity.RidingEntity is { } restsOn)
        foreach (var member in group) if (ReferenceEquals(restsOn, member.LogicContextEntity)) { any = true; break; }
    state.Result = any ? 1 : 0; return 2;
}
case 0x3C: case 0x3D: // @ 0x8003E558 / 0x8003E64C
    state.Result = AnyEntityInZone(v, onTheGroundOnly: command == 0x3C) ? 1 : 0; return 7;
```

## 4. Oracle de valeurs gelé en vecteurs

Les fichiers sont dans `OUT/vectors/`.

- `sm1-vectors.json` : 1 384 vecteurs, une ligne par vecteur.
  - 184 curés : 0x60 38, 0x61 27, 0x3C 66, 0x3D 53.
  - 1 200 aléatoires : 300 par opcode, graines 6001, 6101, 3601 et 3701.
- `sm1-vectors.sha256` : `a8c599c9d4deebd9400181b3ab4cc1508ef31afef11b90c8fe75deb1e0f82069`.
- `README.txt` : format, montage de rejeu, règles d'arrêt.
- `read-map.tsv` : table de correspondance des octets lus, détaillée plus bas.
- Outils : `OUT/work/vectors/*.py`.
  - Régénérer : `python -B work/vectors/gen_vectors.py <dossier>`.
  - Durée : environ 16 s.
  - **FAIT** : deux exécutions donnent un fichier identique à l'octet (`cmp`).

**Contrôles faits sur chaque vecteur** (un échec arrête la génération) :

1. Le vrai gestionnaire rend la taille attendue (2 ou 7) et écrit `Result` dans {0, 1}.
2. Tout l'espace `0x80100000..0x80190000` est comparé avant et après. Rien d'autre que `Result` et le tampon de résultats de la recherche n'est écrit.
3. Chaque lecture de donnée est nommée. Une lecture non classée est un **arrêt**. Il n'y en a aucune sur les 1 384 exécutions.
4. Mon modèle indépendant, écrit pour la tranche, donne le même `Result`.
5. Le modèle de la DLL en montage « prod » (héros en tête de `SpawnedEntities`) donne le même `Result`.
6. En montage « ctx » (héros seulement `PlayerEntity`), 14 vecteurs 0x60/0x61 diffèrent : groupes `0x82` et `0x84` seulement. Aucun vecteur 0x3C/0x3D ne diffère (`ctx-differences.txt`).

**Histogrammes de `Result`** (tous / curés) :

| Opcode | Tous | Curés |
|---|---|---|
| 0x3C | {0:300, 1:66} | {36, 30} |
| 0x3D | {209, 144} | {27, 26} |
| 0x60 | {233, 105} | {18, 20} |
| 0x61 | {215, 112} | {15, 12} |

**Octets lus ↔ membre de la DLL** (`read-map.tsv`, extrait). Tout octet lu hors de cette table est un arrêt.

| Binaire | Membre de la DLL | Remarque |
|---|---|---|
| `+0x10` statut | `EntityScriptProxy.Status` | les statuts 0 à 4 seulement |
| `+0x0C` parent | `ParentEntity` | types 9 et 10 |
| `+0x28` plate-forme | `PlatformEntity` | type 4, type 11 et `0x3C` |
| `+0x48` identifiant | `EntityRefId` | mot entier ; le héros vaut −1 (un héros à 0 est un arrêt du montage) |
| `+0x6C` / `+0xB4` | `Flags` / `AnimFlags` | bit `0x80` seulement |
| `+0x120/124/128` | `TileX/Y/Z` | signés |
| `+0x12C` | `RidingEntity` | jamais recalculé par le test |
| `+0x130` | `XCollisionEntity` | types 7 et 8 |
| `+0x20` | `BlockedByEntity` | **jamais lu** : preuve par `v60-c07` |
| `N` `0x80134360` | étendue de `SpawnedEntities` | les cases au-dessus de N portent un piège « attractif » dans la mémoire binaire |
| octets 1 à 6 | `EventProgramState.Codes` via `v[k]` | |
| `a0` | `entity` (logique) | |
| `a1` | `owner` | jamais lu |
| `a3 + 0x2C` | `EventProgramState.Result` | seule écriture |

**Couverture des cas limites.**

- Bornes : les deux de chaque axe, un pas hors borne, boîte inversée, boîte `[0,0,0,0,0,0]`, boîte réelle de 226 B[3].
- Signes : tuiles −1, INT_MIN, INT_MAX ; opérandes 0x80..0xFF lus non signés.
- Tailles : tuiles 256, 261, 65 536 ; identifiant `0x105` contre 5.
- Groupes vides ; références nulles, y compris deux montages anonymes sans `LogicContextEntity` (null contre null).
- Pointeurs étrangers.
- Statuts 0 à 4, pour la référence et pour le candidat.
- Héros : logique, groupes `0x81 0x82 0x83`, seul occupant d'une boîte, seulement `PlayerEntity`.
- Premier et dernier slot (N = 4 et N = 9).
- Entité logique différente de l'owner.
- Éligibilité de `0x3C`, dont le cas de la décompilation.
- Arithmétique 32 bits : aucune des quatre opcodes n'en fait ; la seule valeur sensible est la tuile.

**Mutants** (modèle DLL, `OUT/vectors/analysis.txt`, `mutant-kills.json`).

- 37 mutants : 13 sur 0x60/0x61 et 24 sur 0x3C/0x3D.
- **36 sont tués** par des tests nommés (`OUT/tests/mutants.tsv`).
- Le 37e (`unsigned_tile`) est le mutant équivalent prouvé au §2.

Au moins un mutant par règle :

| Règle | Mutants tués (tests nommés) |
|---|---|
| R1/R2 | `swap_60_61` (14 tests), `reuse_IsRidingTheLogicEntity` (RP-05), `no_null_guard` (RP-10, RP-27), `empty_group_keeps_result` (RP-03, RP-04, RP-08, RP-22, RP-24, RP-26), `first_only` (RP-01, RP-06, RP-11, RP-21), `last_only` (RP-01, RP-06, RP-21, RP-25) |
| R3 | `upper_exclusive`, `lower_exclusive` (ZP-3D-02, -05, -06, -12, -13), `signed_operands` (ZP-3D-05, ZP-3C-05), `byte_truncated_tile` (ZP-3D-05, -06, ZP-3C-05, -06), `no_status_filter` et `active_only` (ZP-3D-07, ZP-3C-07), `z_ignored` (ZP-3D-03, -06, -10, -13, ZP-3C-03, ...), `axes_xy_swapped`, `axes_yz_swapped` |
| R4 | `inverted_3c`, `3c_no_collidable`, `3c_no_anim` (ZP-3C-20 seul), `3c_no_platform` (ZP-3C-20 seul), `3c_flags_equal` (ZP-3C-08, -22, -23), `3c_anim_nonzero` (ZP-3C-22), `zone_3c_equals_3d`, `zone_3d_equals_3c` (ZP-3C-25 seul) |
| R5 | `no_player_in_search` (RP-06, RP-21), `spawned_only` (ZP-3D-08, ZP-3C-08, ZP-3C-23), `skip_last`, `logic_only` |
| R6 | `owner_instead_of_logic` (RP-01, -02, -04, ...) |
| R7 | `result_always_one`, `result_only_on_true`, `size_one`, `size_three`, `size_six`, `size_eight`, `platform_instead_of_riding` |

Les tests d'octets réels tuent un sous-ensemble en intégration (`OUT/tests/real-bytes-kills.tsv`). Par exemple `swap_60_61` est tué par 13 groupes, `no_player_in_search` par RB-08, RB-13 et RB-14, `skip_last` et `logic_only` par RB-11.

**Limite.**
- **FAIT** : le modèle de la DLL (`dll_search`, `dll_case`) est une transcription Python de `EntitySearchService.cs:99-269` et des 4 `case` proposés.
- **INFÉRENCE** : l'équivalence avec le C# est à établir en T1, par le rejeu de ces mêmes vecteurs.
- Toute valeur contredite en T2 est un arrêt (précédent E19.l, acceptation 1), pas une ré-épingle.

## 5. Tests nommés

### 5.1 Montages et état de départ commun

**Montages.**

- **MB-prod** : runner nu, avec `Context`, `NewEntity`, `Run`, selon `AlundraClassAndBranchOpcodesTests.cs:21-67`.
  - Le héros est en `Spawned[0]` et `PlayerEntity` (`IsPlayer = true`, `EntityRefId = -1`).
  - Chaque proxy a son `LogicContextEntity` non nul et distinct, sauf les cases « anon ».
  - `RidingEntity` est posé à la main. Les tests n'appellent jamais `UpdateRidingEntities`.
- **MB-ctx** : héros seulement `PlayerEntity` (le montage actuel de `Context`).
- **MB-owner-hero** : owner = héros avec `LogicEntity` = la plaque.
- **RB** : octets réels tronqués (§6).
- **CH** : enregistrements réels et chaîne de production.
- **AR** : `ArcRun`.

**État de départ de tous les tests par vecteurs** :
- programme `[code..., 0xFF]`, `CodeIndex 0`, `Result = 7`, `Parameters` à 0 ;
- owner = l'entité logique, sauf MB-owner-hero.

**Assertions communes** : `Result == binary.result` du vecteur ; `CodeIndex == taille` (2 ou 7) ; trace `Kind == Implemented` ; aucun champ d'aucun proxy ni `Parameters[0, 2..9]` modifié.

**Valeur rouge d'aujourd'hui** (FAIT, `UnknownOpcode` `:3065-3091` : saut par la taille, `Result` inchangé, `Kind = UnknownSkipped`) :
- `Result` reste 7 ;
- `CodeIndex` vaut la taille, donc cette assertion est verte d'avance ;
- la trace donne `UnknownSkipped`.

Tous les tests par vecteurs sont donc rouges sur `Result` (attendu 0 ou 1, valeur actuelle 7) et sur le `Kind`.

### 5.2 Tests par vecteurs

Classes avant R5 : `AlundraRidingPredicateOpcodesTests`, `AlundraZonePredicateOpcodesTests`. Après R5 : `Events/RidingPredicateOpcodesTests.cs`, `Events/ZonePredicateOpcodesTests.cs`. La liste complète avec tous les vecteurs est dans `OUT/tests/tests.tsv`.

**Les 21 tests de 0x60 / 0x61** (`RP-*`). Valeurs attendues issues des vecteurs, dans l'ordre des vecteurs :

| ID | Test | Montage | Attendu |
|---|---|---|---|
| RP-01 | `RidingOnMe_0x60_AMemberOfTheGroupRidesTheLogicEntity_GivesOne` | prod | 1,1,1,1,1 |
| RP-02 | `..._NobodyOfTheGroupRidesTheLogicEntity_GivesZero` | prod | 0 ×7 |
| RP-03 | `..._AnEmptyGroupGivesZero_AndOverwritesAStaleResult` | prod | 0,0 (avec Result avant 7 puis 1) |
| RP-04 | `..._TheReferenceStatusGatesTheRawIdGroup` | prod | 0,0,1,1 (statuts 0, 4, 1, 3) |
| RP-05 | `..._TheRiderStatusAndTheBlockAreNotTested` | prod | 1,1,1,1 |
| RP-06 | `..._TheHeroIsAMemberOfTheGroups81_82_83` | prod | 1,0,0,0,1,0,1 |
| RP-07 | `..._TheCarrierIsTheLogicEntity_NotTheOwner` | owner-hero | 0,1 |
| RP-08 | `..._TheRawIdIsTheWholeWord_AndTheHeroIsNoRawIdCandidate` | prod | 0,1,0 |
| RP-09 | `..._TheLogicEntityIsTheHero_ProgramB` | prod | 1,0 |
| RP-10 | `..._ABareLogicEntityWithoutIdentityIsRiddenByNobody` | anon | 0 |
| RP-11 | `..._TheOtherSearchGroupsPassThrough` | prod | 1,1,0 |
| RP-12 | `..._TheLastOfTenSlotsCounts` | prod | 1 |
| RP-21 | `RidingOn_0x61_TheLogicEntityRidesAMemberOfTheGroup_GivesOne` | prod | 1 ×7 |
| RP-22 | `..._TheLogicEntityRidesNoMemberOfTheGroup_GivesZero` | prod | 0 ×8 |
| RP-23 | `..._TheReverseRelationIsNotEnough` | prod | 0,1,0 (0x61 contre 0x60 sur le même monde) |
| RP-24 | `..._TheCarrierStatusIsNotTested_TheReferenceStatusIs` | prod | 1,1,1,0,0,1 |
| RP-25 | `..._TheGroups82And83` | prod | 1,0 |
| RP-26 | `..._TheRawIdIsTheWholeWord` | prod | 0 |
| RP-27 | `..._ACandidateWithoutIdentityIsNeverACarrier` | anon | 0 |
| RP-30 | `RidingPredicates_AreImplementedNotSkipped_SizeTwo_AndWriteOnlyResult` | prod | 1,0,1,0 |
| RP-31 | `RidingPredicates_OnABareHost_GiveZero_AndDoNotThrow` | hôte nu | 0, sans exception |

**Les 34 tests de 0x3C / 0x3D** (`ZP-*`) :

- Chaque famille existe pour `0x3D` et pour `0x3C` : `ZP-3D-01..13` et `ZP-3C-01..13`.
  - 01 : dedans, attendu 1,1.
  - 02 : deux coins et six faces, attendu 1 ×8.
  - 03 : un pas dehors sur un axe, attendu 0 ×6.
  - 04 : boîte inversée, attendu 0.
  - 05 : octets non signés, attendu 1,0,1,0.
  - 06 : tuiles hors plage, attendu 0 ×8 puis 1.
  - 07 : statuts 0..4 et un trou, attendu 0,1,1,1,0,0.
  - 08 : héros dans la liste ou seulement `PlayerEntity` (prod et ctx), attendu 1.
  - 09 : premier et dernier slot, N = 4 et N = 9 (prod et ctx), attendu 1 ×3.
  - 10 : personne, attendu 0.
  - 11 : l'entité logique dehors ou dedans, attendu 1,1.
  - 12 : axes non permutés, attendu 1,0,0.
  - 13 : boîte réelle de 226 B[3] (`2B 2B 30 33 0C 0C`), attendu 1,1,0,0,0,0.
- Éligibilité de `0x3C` :
  - ZP-3C-20, les trois conditions : 1,0,0,0,0,0.
  - ZP-3C-21, le sens de la décompilation : 0.
  - ZP-3C-22, seul le bit `0x80` de chaque mot compte : 1,0,1.
  - ZP-3C-23, le héros par ses drapeaux (prod et ctx) : 1,0.
  - ZP-3C-24, chaque candidat est testé : 1,0.
  - ZP-3C-25, `0x3C` n'est pas `0x3D` : 1,0.
- ZP-30 : implémenté, taille 7, écrit `Result` seulement (1,0,1,0).
- ZP-31 : hôte nu, sans héros, sans exception.

### 5.3 Rejeu complet des vecteurs

Classe `Oracle...Tests` du lecteur de vecteurs :

| ID | Contenu |
|---|---|
| OV-01..04 | Rejeu des 338 (0x60), 327 (0x61), 366 (0x3C), 353 (0x3D) vecteurs en montage prod. **Rouge** : 1 384 cas. |
| OV-05 | Rejeu en montage ctx des 719 vecteurs 0x3C/0x3D. |
| OV-06 | SHA-256 du fichier, comptes 1 384 / 184 / 1 200, SHA-1 du binaire. **Vert d'avance** (garde). |
| OV-07 | Toute lecture nommée est dans la table ; aucune lecture de `blocked` ni au-dessus de N. **Vert d'avance** (garde). |

### 5.4 Tests d'octets réels (RB) et de chaîne réelle (CH)

**RB** : octets réels à leurs vrais pc, document tronqué après les instructions testées.
- Convention du test `IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27` (`AlundraEventProgramRunnerTests.cs:4155-4180`) : `MapEventProgramLoader.Load(root, "<monde>")`, assertion de l'octet attendu.
- Différence : pc absolus conservés ; un saut au-delà de la coupure termine l'appel à ce pc.
- Racine par `SaveGameDirectorTestSupport.FindProjectRoot()` : le test **échoue** si l'export manque, il ne se saute pas.
- Valeurs : `OUT/tests/real-bytes.tsv`, 53 lignes, simulées sur le runner avec la sémantique de la DLL. 32 lignes sont rouges aujourd'hui et 21 sont des gardes vertes d'avance.

**CH** : montage entre les vecteurs et `ArcRun`, sur le modèle de `AlundraNpcCharacterControllerMoverTests.cs:2182-2211`.
- Bâtir les enregistrements réels (`EntitySpawnFactory.ApplyRecord` et `ApplySpawnInitialization`, tileMap et `SpriteRecordCatalog` de l'export).
- Poser l'objet au sommet exact du porteur : `PosX = porteur.PosX + porteur.ModX − objet.ModX`, idem Y, `PosZ = porteur.PosZ + porteur.ModZ + porteur.Depth + 1 − objet.ModZ` (`EntitySupport.cs:313-345`).
- Appeler `EntitySupport.UpdateRidingEntities(collidables)`, vérifier la prémisse, puis lancer les octets RB.
- CH-01 : plaque 25 et objet 31 de la carte 27 (octets de RB-01). Attendu : fin à 990 ; aujourd'hui : bloqué à 985.
- CH-02 : objet laissé à sa place. Attendu : 985 (garde).
- CH-03 : plaque 0 et objet 4 de la carte 400 (octets de RB-02, trois lignes).

| RB | Programme (carte, pc) | Cas : attendu → rouge d'aujourd'hui |
|---|---|---|
| RB-01 | 27 C[26] `00 60 1F 04 FD FF` @984 | porté : 985 puis 990 → bloqué 985 (**rouge**) ; non porté : 985, 985 (garde) |
| RB-02 | 27 C[2] @604 (cut 635) ; 400 C[1] @440 (cut 471) | bon objet : 731 (27) / 567 (400) ; mauvais : 844 / 673 ; personne : 607 / 443. **Rouge** pour les deux premiers (la boucle tourne sans sortir) ; personne : garde |
| RB-03 | 27 C[36] pile de quatre @1106 | empilés : 1136 avec logique = slot 4 (garde) ; sommet non empilé : **1109** → aujourd'hui **1136** (la pile se « résout » seule) |
| RB-04 | 29 C[17] `60 14` | comme RB-01 |
| RB-05 | 30 C[19] `60 16`, flag 0x83FC clair | comme RB-01 |
| RB-06 | 113 C[9] `60 0A` | comme RB-01 |
| RB-07 | 113 C[10] bascule (trois appels ; puis départ du cavalier) | 1069, 1083, 1083 avec flag `0x8064` posé → bloqué 1069 ; cavalier parti : flag effacé, 1069 → 1083 |
| RB-08 | 136 C[5] `37 01 60 81 04 FC FF` (3 appels) | héros dessus : 1577, 1577, **1584** → 1577 ; ailleurs : 1577 (garde) |
| RB-09 | 159 B[5] `00 61 0D 4B` (logique = héros) | héros porté, Result avant 0 : 732 → 729 ; non porté, Result avant 7 : 729 → 732 ; les deux autres variantes sont des gardes |
| RB-10 | 159 C[13] `60 03 4A 60 04 4A 60 0A 4A` | enregistrement 3 ou 10 porteur : redémarre à 1652 → tombe à 1663 ; personne : garde |
| RB-11 | 226 B[3] `3D 2B 2B 30 33 0C 0C 00 04 F8 FF` | dedans, Result avant 0 : 258 → bloqué à 255 ; dehors ou personne, Result avant 1 : reste à 255 → passe à 258 (rouge, 3 variantes) ; trois variantes en garde |
| RB-12 | 398 B[1..7] `43 0N 61 00 42 03 64 00` | non porté : B[1] 94 → 191 ; B[2] 274 → 371 ; B[3] 454 → 551 ; B[4] 634 → 731 ; B[5] 829 → 926 ; B[6] 1049 → 1146 ; B[7] 1257 → 1354 (**rouge**) ; porté : 191, 371, 551, 731, 926, 1146, 1354 (garde, le `0x43` a écrit 1) |
| RB-13, RB-14 | 77 C[14] `1A 00 61 81 03 0C 00` ; 78 C[19] | porté, Result avant 0 : 1885 / 1693 → 1876 / 1684 ; non porté, Result avant 1 : 1876 / 1684 → 1885 / 1693 (rouges) ; porté avec Result avant 1 : garde |

### 5.5 Arcs `ArcRun` (cartes réelles)

Classe : `[Collection(AlundraMusicPlayerSingletonCollection.Name)]`. Un arc `RealController` doit finir à 0 glissement (`AlundraArcSupport.cs:342`, `ArcsThatSlide`).

| ID | Carte et arc | Signal de fin (trace) | Limite initiale | Aujourd'hui |
|---|---|---|---|---|
| AR-01 | 27 C[26], plaque 25, objet 31 posé dessus | `(2, 999, 0x05)`, drapeau `0x8104`, `Flags & 0x600` de l'objet à 0 | 60 | échoue à la limite, `StuckMessage` nomme `0x60 @985 (UnknownSkipped)` |
| AR-02 | même carte, objet non posé | jamais `(2, 999, 0x05)` ; chaque `0x60 @985` est `Implemented` | 60 | **rouge** par le `Kind`, vert sur le signal |
| AR-03 | 400 C[1], plaque 0, objet 4 | `(2, 578, 0x05)` avec `ProgramStart` 440, drapeau `0x8014` | 60 | échoue à la limite |
| AR-04 | 398 B[1], rien sur Lurvy | `(1, 94, 0x8D)` exécuté et jamais `(1, 191, 0x05)` | 400 | **rouge** : `@191` est atteint |
| AR-05 | 398 B[1], objet 1 posé sur Lurvy | `(1, 191, 0x05)` | 400 | garde (déjà vrai aujourd'hui) |

Réglages :

- **Carte 27** : `ArcSpec("SM1-27", "Ancient Shrine", "Ancient Shrine-27", [], 13, 57, 0, 60)`, arrivée du portail 1 de la 26 en (13,57) z 0. Pose à la main de l'objet avant la première image, par la formule du §5.4. Prémisse dans `OnInstruction` à `(2, 985, 0x60)` : `Assert.Same(plaque.LogicContextEntity, objet.RidingEntity)`.
- **Carte 400** : arrivée en (9,17) z 5.
- **Carte 398** : `RealController: true, Prefabs: true` et arrivée `Arrival` en (7,31) z 0 du portail 1 de la 20 ; le programme attend `70` (le héros au sol).
- **Mesure de la limite d'images** : les limites sont des premières valeurs larges, à resserrer à la première mesure (§1.3 du plan). Estimation en appels : C[26] se résout au 2e appel après que la plaque est Normal.

## 6. Vérifications sur cartes réelles

### 6.1 Les 187 sites par forme

Table complète : `OUT/corpus/sm1-sites.tsv`. Écrivains de `Result` qui atteignent chaque site aujourd'hui : `OUT/corpus/red_values.tsv` (dataflow, INFÉRENCE sur l'exécution réelle).

| Forme | Sites | Entité logique | Écrivain de `Result` aujourd'hui | Comportement actuel (INFÉRENCE) |
|---|---|---|---|---|
| Boucle de plaque à 4 phases (27 C[2], 400 C[1..4, 9..12]) | 144 | la plaque | entrée de programme (0 sur état neuf) | inerte : aucune plaque ne réagit |
| Attente de plaque (27 C[26..29], 29 C[17..20, 25..28], 30 C[19..22], 113 C[9]) | 17 | la plaque | entrée | `04` boucle sans fin |
| Pile de quatre objets (27 C[36]) | 4 | les `43` successifs | `43 NN` (écrit 1) | le « sommet de pile » passe seul : la suite (`85 ...` retouche de cases, drapeau `0x0627`) part dès que les momies sont mortes |
| Bois flottant (77 C[14..17, 24], 78 C[19]) | 6 | le bois | `70` (IsOnGround) | si le bois est au sol, le programme saute vers `1A 04 ; FF` |
| Interrupteur à bascule (113 C[10]) | 2 | la plaque | entrée | inerte |
| Interrupteur au sol (136 C[5], C[10]) | 2 | l'interrupteur | entrée | inerte |
| Programme B du héros (159 B[5]) | 1 | le héros | entrée ou `3B` | `4B` redémarre en boucle ou passe selon le dernier `3B` |
| Bulle (159 C[13]) | 3 | la bulle | entrée | `4A` ne redémarre jamais |
| Zone attendue (226 B[3]) | 1 | aucune | `43 0B` @234 (écrit 1) | la scène continue immédiatement (`04 F8 FF` ne boucle plus) |
| Cible de `0x43` (398 B[1..7]) | 7 | l'objet N | `43 0N` (écrit 1) | **le saut `03 64 00` est pris à chaque passage** |

**398 aujourd'hui (INFÉRENCE sur le décodage, à confirmer par AR-04).**
- Dès que le héros est au sol, B[1..7] prennent le saut vers `@191` : drapeau `0x8384` posé, perte de contrôle, dialogue `0x1A6`, puis `2E 0N` détruit l'objet, `2D 0N` le recrée, `64` le repositionne, `11` rend le contrôle, et le programme reprend au début.
- Le joueur voit donc à l'arrivée un dialogue en boucle dans la maison de Lurvy.
- Cela correspond au sens binaire « l'objet repose sur Lurvy » : ces dialogues devraient partir seulement quand un objet est posé sur Lurvy.
- **Les zones B** sont `0..51 × 0..59` (carte entière) pour B[1..9]. Le héros n'a donc pas besoin d'être dans une zone précise pour que la boucle tourne.

**27 C[36] aujourd'hui.** Après `2C 17 04 ...` (les quatre momies tuées), le programme exécute `43 23 ; 00 ; 60 2D ; 04 ; 43 2D ; 60 2E ...`. Chaque `04` lit le `1` du `0x43` précédent, donc la récompense (`85 ...` retouche de 3 zones de cases, `05 27 06`) part sans pile. Après sm1 le passage restera fermé tant que le transport n'existe pas (voir Q-1).

### 6.2 Les arcs proposés

**27 C[2].** À remplacer par RB-02 et par AR-01.
- Les 4 plaques exécutent le même programme.
- La bonne pièce change par phase de 24 images (appels) : objet 5 en phase 1 (`03 75 00` → 731), 6 en phase 2 (→ 749), 7 en phase 3 (→ 767), 8 en phase 4 (→ 785). Les autres objets mènent à 844 / 864 / 884 / 904.
- La phase en cours dépend du nombre de ticks écoulés : un arc sur C[2] serait fragile.
- C[26] (plaque 25, `60 1F` fixe) est stable.
- Succès à l'appel 2 : `63 05 00 02` efface `0x0200` de l'objet, `05 14 80` @742 pose `0x8014`, puis `31 14 80 ...` aboutit à `FF` @925.

**400 C[1].**
- Même forme. Objet 4 en phase 1 : `05 14 80` @578 puis `31 15 80 6E 00` → 754 (`FF`).
- La fin du puzzle écrit `05 87 02` (G647) quand les quatre drapeaux `0x8014..0x8017` sont posés.

**226 B[3].**
- **INCONNU** : le nombre d'images avant la zone.
- La scène attend le drapeau G257 (`36 01 01` @177), posé par la carte 229 (B[2] @317, B[3] @376), et le temporaire `0x87D0` (T2000, `36 D0 87` @201), posé par 226 C[3] @1266 (Meade).
- Elle fait ensuite marcher des entités par `0B` et attend que « n'importe quelle entité » entre en (43, 48..51, 12). Le loup-garou (enregistrement 12, C[13]) attend `0x87D1` puis `40 02 00`.
- Un arc obligerait à rejouer ces scripts. **Remplacé par RB-11.**
- **INFÉRENCE** : après sm1 la scène attend réellement. Si rien n'entre dans la zone (par exemple un opcode non porté dans la scène du loup-garou), elle ne finit pas. C'est le risque de Q-3.

**398 B[1].** Voir AR-04 (rouge aujourd'hui) et AR-05 (garde). Le test d'octets réels est RB-12.

**Autres cartes d'arc possibles** (les conditions nécessaires statiques sont vérifiées pour 27, 400, 226, 398, 159, 136, 29, 113) :
- **FAIT** : le dossier de la carte, `events.json`, `tilemap`, le dialogue de la carte, `Shared.dialogue`, `Etc.dialogue` existent.
- **FAIT** : tous les `PrefabAssetId` sont dans `AssetInfos.json` et dans `sprite-records.json`.
- **INFÉRENCE** : le chargement dynamique est probable (précédent `AlundraHeightTargetSitesTests.cs:76-90`, cartes 127, 89, 363, 36, 115 avec `Prefabs + RealController + Arrival`). **INCONNU** pour 27, 400 et 398 tant que le sondage T0 n'est pas fait.

### 6.3 Prémisse `RidingEntity` et valeurs périmées

- Les tests par vecteurs, RB et CH ne dépendent d'aucune valeur périmée : `RidingEntity` est posé à la main (vecteurs, RB) ou calculé par `UpdateRidingEntities` sur des collidables à jour (CH).
- Les arcs ne posent que des positions. Ils affirment la prémisse à l'instant de l'instruction ; un échec de la prémisse est un arrêt (la chaîne de production est en cause, pas sm1).
- Cas périmé connu (**FAIT**, A10/A31) : une entité qui sort des collidables (statut 4, `NoEntityCollision`, `PlatformEntity` posé, Collidable effacé) garde son ancien `RidingEntity`. Aucun site sm1 ne sort un cavalier des collidables, sauf le héros porteur (E20.g1a) qui n'existe pas encore.
- Le héros est recalculé en tête de chaque tick (`EntitySupport.cs:297-308`) : jamais périmé.
- Sur la 159 C[13], la bulle bascule son bit Collidable : le cas est **INCONNU** (dynamique).

## 7. Retrait, mutants, liste fermée, arrêts, budget, retour arrière

### 7.1 Preuves de retrait

- Retirer les 4 `case` (et rien d'autre) rend rouges exactement :
  - les tests par vecteurs RP-*, ZP-* et OV-01..05 ;
  - les 32 lignes rouges de RB, CH-01, CH-03, AR-01, AR-03, AR-04 et AR-02 (par le `Kind`).
- Restent verts : les assertions de taille, OV-06 et OV-07, les 21 lignes RB gardes, CH-02, AR-05.
- Retirer le code sans retirer les lignes de la liste rend rouge la règle 2 de sm0 (« ligne périmée ») ; retirer les lignes sans le code rend rouge la règle 1. Les deux vont dans **le même commit**.
- Retirer les 187 lignes seules est déjà rouge pour la règle 1.

### 7.2 Comptes épinglés de la règle 5 de sm0 après sm1 (calculés sur `skipped-sites.tsv`)

| | Avant | Après |
|---|---|---|
| Lignes | 510 | **323** |
| Instructions distinctes | 508 | **321** |
| Cartes | 96 | **89** (disparaissent 27, 30, 77, 78, 113, 226, 400) |
| Opcodes atteints | 21 | **18** (`0x60 0x61 0x3D` sortent ; `0x3C` n'avait aucun site) |

Par propriétaire :

| Propriétaire | Lignes |
|---|---|
| sm1 | 187 → 0 (clos) |
| sm2 | 129 |
| sm3 | 42 |
| sm4 | 90 |
| sm5 | 17 |
| sm7 | 42 |
| sm8 | 3 |

Total après : 323. Les mutations (m1), (m2), (m4) et (m5) de sm0 utilisent des lignes `sm1` : ce sont des exécutions uniques de T1 de sm0, rien à maintenir.

### 7.3 Arrêts

- Q-E19-SM1 autre que l'accord ; R5 non faite ; garde R7 non faite ; sm0 non exécutée (ou garde de remplacement non décidée, Q-8).
- Le générateur lève un arrêt de lecture non classée ; le SHA-1 du binaire diffère ; le SHA-256 du fichier de vecteurs diffère.
- Un vecteur contredit le modèle de la DLL, ou, en T2, une valeur écrite est contredite par la DLL : arrêt, pas de ré-épingle.
- Un test existant bouge hors de la liste fermée (`ImplementedOpcodes`, la liste de sm0 et ses comptes, les 3 libellés).
- La prémisse `RidingEntity` d'un arc est fausse : arrêt, à soumettre aux propriétaires de la chaîne.
- Un fichier de `Alundra/` autre que `EventProgramRunner.cs` et `EventOpcodeSizeTable.cs` est touché.
- La liste de sm0 ne laisse pas exactement 323 lignes, ou la suppression n'est pas pure.
- Un abandon par `--blame-hang-timeout 300s`.
- Le sondage T0 d'`ArcRun` échoue : l'arc concerné est retiré (l'équivalent RB/CH le remplace) et la décision est consignée ; ce n'est pas un arrêt de la tranche.

### 7.4 Budget et retour arrière

- **Budget** : un `executor`, deux tentatives ; un `plan-verifier` avant ; un `verifier` frais après (mutations réelles par script sur le C#, les 36 mutants du §4 : chacun doit rendre rouges les tests nommés, fichiers rendus à l'octet après chacun). Taille estimée : environ 65 lignes de C# et 3 lignes de libellés ; environ 80 méthodes de test, dont ~1 400 cas de rejeu.
- **Retour arrière** : annuler le commit T2 (code, libellés, miroir, liste de sm0) ; les tests de T1 redeviennent rouges par construction, annuler T1 aussi. Aucune donnée, aucun export, aucun format de sauvegarde ne change.

### 7.5 Acceptation (motif d'E19.l)

1. Tests de T1 rouges aux valeurs du plan (sauf les gardes nommées), verts après ; une valeur contredite est un arrêt ; vecteurs régénérés identiques à l'octet.
2. Code de test existant touché, liste fermée : `IntroTraceHarnessTests.ImplementedOpcodes`, la liste de sm0 et ses comptes, les 3 libellés ; rien d'autre ; les six traces à l'octet identiques.
3. `Alundra.Tests` sans échec en Release puis en Debug (la Debug en dernier), `--blame-hang-timeout 300s`.
4. Recette par l'auteur (§8).

## 8. Questions à l'auteur et dépendances

Chaque question est posée en termes de ce qu'on voit en jeu.

- **Q-1 — Ordre avec le transport (E20.g).**
  - Option A (défaut) : sm1 juste après sm0, sans attendre E20.g. Les énigmes à plaques restent insolubles (rien ne soulève encore les objets) mais ne se déclenchent plus à tort ; la maison de Lurvy (398) cesse sa boucle de dialogues ; la scène d'Inoa (226) attend sa zone.
  - Option B : après E20.g. La maison de Lurvy reste cassée plus longtemps.
  - Coût de A : le passage de la carte 27 qui s'ouvre aujourd'hui par accident après la mort des momies (27 C[36]) reste fermé jusqu'au transport.
- **Q-2 — `0x3C` (0 site).**
  - Défaut : le porter avec ses vecteurs. Environ 8 lignes de code et 19 tests.
  - Rien ne change en jeu, mais E19 se clôt alors sur « aucun opcode sauté » et le sens binaire (contraire de la décompilation) est figé par des tests.
  - Autre option : le laisser sauté, ce qui conserve un trou d'inertie sans site.
- **Q-3 — Risque d'attente infinie (226).**
  - Défaut : accepter, avec recette sur la scène d'Inoa.
  - Après sm1, si le loup-garou (ou l'entité attendue) n'atteint jamais la zone, la scène ne se termine pas, au lieu de se terminer trop tôt comme aujourd'hui.
- **Q-4 — Arcs.** Défaut : CH et RB toujours ; AR seulement si le sondage T0 charge la carte. L'arc de 398 (rouge aujourd'hui) est le plus utile.
- **Q-5 — Libellés.** Défaut : accepter les 3 corrections de R8 (journaux et vidages de traces seulement).
- **Q-6 — ADR.** Défaut : aucun (D-E19-2 tranche déjà « le binaire l'emporte » ; E19.l n'en a pas écrit). Le prochain numéro libre serait 0076 si l'auteur en veut un.
- **Q-7 — `RidingEntity` périmé.** Défaut : ne pas l'épingler dans sm1 ; D5b (M4 : effacement à la passe des compteurs) et E20.g1a (`PutInHands` vide `RidingEntity`, D-G-2) en sont propriétaires.
- **Q-8 — sm1 avant sm0.** Défaut : attendre sm0. Garde de remplacement si l'auteur veut sm1 d'abord : un test statique qui parcourt les 11 cartes avec `AlundraStoryChainOpcodeAudit.WalkMap` et épingle les 187 sites comme portés, avec un compte non vide.

**Recette de l'auteur après sm1** : entrer chez Lurvy (plus de dialogue en boucle) ; marcher sur l'interrupteur de l'église 136 ; monter sur une bulle de la 159 ; la scène d'Inoa 226 jusqu'à sa fin ; le bois flottant des 77 et 78 ; la carte 27 après les momies.

**Dépendances.**

| Tranche | Lien avec sm1 |
|---|---|
| sm0 | **dure** : liste fermée, `ClosedOwners`, comptes de la règle 5 |
| R5 / R7 | **dures** via sm0 |
| E20.g1a | douce : l'ordre n'importe pas. `PutInHands` vide `RidingEntity` de l'objet porté. Pas de prérequis pour les tests, mais la recette complète des plaques en dépend |
| D5b | douce : M4 efface `RidingEntity` à la passe des compteurs, sans changement du code de sm1 |
| E20.c2a, E18 | aucune (c2a concerne la vie du héros, sm4 ; E18 concerne `0xBB` et `0xC2`, sm8) |

## 9. Tâches proposées

- **T0 — annexe et sondes (aucun code de la DLL).**
  - Copier `OUT/` vers `docs/plan-e19-sm-annexe/sm1/` (gelé).
  - Régénérer les vecteurs et vérifier que le SHA-256 est égal à celui du §4.
  - Refaire le recompte des 187 sites sur l'export du jour.
  - Sonder `ArcRun` sur 27, 400, 398 (deux modes, 3 images) ; consigner.
  - Baselines des suites Release et Debug.
- **T1 — tests, rouges d'abord.**
  - Fichiers (avant R5 puis après) : `AlundraRidingPredicateOpcodesTests` / `Events/RidingPredicateOpcodesTests.cs` ; `AlundraZonePredicateOpcodesTests` / `Events/ZonePredicateOpcodesTests.cs` ; le lecteur des vecteurs et son test ; les tests RB, CH, AR.
  - Message : `test(alundra): the riding and zone predicates, written red on the skipped opcodes`.
- **T2 — code, libellés, miroir et liste dans un seul commit** (règle B11 : le code d'un opcode et le retrait de ses lignes vont ensemble).
  - Message : `feat(alundra): port the riding and zone predicate opcodes like the binary`.
- **T3 — arcs** (si le sondage les autorise) : `test(alundra): arcs on the real plates and the Lurvy house`.
- **T4 — plan, `verifier` frais, recette.**

## 10. Résolution du §8 de `check_sm12.md` pour sm1

| Manque | État |
|---|---|
| B0 (règle du chantier, Q-E19-SM1) | **non résoluble ici** : décision de l'auteur et ordre des chantiers |
| B1 (sm0 absente) | sm0 est READY ; reste à l'exécuter avant sm1. Conséquences de sm1 sur la liste : §7.2 |
| B2 (règles R1..Rn) | résolu : §3 |
| B3 (tests, valeurs, arrêts, acceptation) | résolu : §5, §7 |
| B4 (arcs) | partiel : AR-01..05 spécifiés ; **INCONNU** : chargement dynamique (T0) ; 226 remplacé par RB-11 |
| B5 (portée de `0x3C`) | question Q-2 avec défaut |
| B6 (27, 29, 400 sans transport) | résolu par CH et AR avec pose à la main ; 226 par RB-11 |
| M1 (vecteurs) | résolu : §4 |
| M2 (libellés) | résolu : R8 |
| M3 (`ImplementedOpcodes` ou `IsPorted`) | résolu : R9 |
| M4 (ADR) | aucun par défaut : Q-6 |
| M5, M6 (noms HEAD, R5) | résolu : noms avant et après R5 au §5 |
| M7 (corrections de la critique) | R1 pas recopié ; O1 vérifié (178 ids valides) ; O3 : la porte de gel `[0x800DC4B8] & 0x48` fait que l'ordre « événements puis entités » vaut porte ouverte, la DLL a le chemin porte fermée (`WorldProxy.cs:2907-2915`) ; O4 : `0x800384F4` construit les listes ; O6 : programmes B, logique = héros (R6) |
| M8 (écarts de `RidingEntity`) | résolu : §6.3 |
| M9 (drapeaux dynamiques) | résolu sauf 159 C[13] : §2 point 5 |
| M10 (héros) | résolu : R5 |
| M11 (programmes B) | résolu : vecteurs `v61-c07*`, `v60-c17`, RB-09, RB-12 |
| M12 (recette) | résolu : §8 |
| M13 | résolu : R8 |

## 11. Fichiers produits

Tout est sous `OUT/`. `OUT/SHA1SUMS.txt` liste les 66 autres fichiers. Les scripts s'exécutent depuis leur dossier, jamais en place sur l'annexe.

- `vectors/` : `sm1-vectors.json`, `sm1-vectors.sha256`, `README.txt` (en anglais, comme l'annexe), `read-map.tsv`, `equivalent-mutants.txt`, `ctx-differences.txt`, `analysis.txt`, `mutant-kills.json`.
- `tests/` : `tests.tsv` (55 tests par vecteurs), `mutants.tsv`, `real-bytes.tsv` (53 lignes), `real-bytes-kills.tsv`, `real-bytes-scenarios.json`.
- `corpus/` : `sm1-sites.tsv` (187 sites, forme, logique, écrivains de `Result`), `red_values.tsv`, `sites_programs.txt` (décodage de 41 programmes).
- `binary/` : `handlers-sm1.txt`, `annex-oracle-run-seed7.txt`, `critic-oracle-run.txt`.
- `work/oracle/` (copie de l'oracle de l'annexe), `work/check/` (outils du critique), `work/corpus/` (scripts du corpus), `work/vectors/` (`sm1lib.py`, `gen_vectors.py`, `analyse.py`, `tests_def.py`, `realbytes.py`, `rb_kills.py`, `realbytes_table.py`, `equivalent_mutants.py`, `disasm4.py`, `emu.py`, `lib.py`).

Les scripts lisent `ALUN_CD.EXE` à `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted\ALUN_CD.EXE`, hors dépôt.