# Préparation de la tranche E19.sm4 (lecture seule)

Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/sm-prep/sm4`. Appelé `SM4/` ci-dessous. Binaire `ALUN_CD.EXE` SHA-1 `a705f8da…58` vérifié. HEAD lu : `36ac0686`, branche `chantier/restructuration-dll-r3`. Étiquettes : FAIT, INFÉRENCE, INCONNU.

## 0. Verdict

Je n'ai rien exécuté côté C# : ni build, ni `dotnet test`, ni `ArcRun`. Tout ce qui touche la DLL est de la lecture de code ou une inférence.

- **FAIT : la matière est complète.**
  - Règles R1 à R7 aux noms du HEAD.
  - Vecteurs rejouables : 370 pour les 5 opcodes, 21 pour les 3 sans site. Chaque octet lu ou écrit est rattaché à un membre de la DLL.
  - 44 tests unitaires nommés, dont les valeurs sont auditées contre le vrai binaire.
  - 25 mutants au niveau du gestionnaire, tous tués.
  - Arcs spécifiés, tests sur octets réels pour ce qui ne tourne pas en arc, et conséquences sur la liste sm0 chiffrées.
- **Ce qui bloque encore n'est pas de la matière, ce sont des décisions et des prérequis.**
  - G1 : Q-E19-SM1 sans réponse, R5 non faite, Q-R-7 et la garde R7 non faites.
  - Prérequis : sm0 non exécutée, donc pas de liste à décrémenter.
  - Réponses de l'auteur : Q-sm-1, Q-sm-2, Q-sm-3, avec défauts écrits au §6.
  - Mesures de T0 que la lecture seule ne permet pas : le §4 en fait la liste.
- **Les 90 sites sont tous couverts.** Soit par un test exact, soit par le contrôle de forme (octets exacts au pc).
- **Pour les 3 opcodes sans site, je recommande de les laisser sautés** (§1.8). Vecteurs fournis si l'auteur veut les porter.

## 1. Règles d'exécution aux noms du HEAD

Chemins sous `Alundra/` : le runner est `Events/EventProgramRunner.cs`. `Dispatch` est en `:563`. `entity` y est l'entité logique, `owner` le propriétaire du programme. `default → UnknownOpcode` en `:2102-2103`, `UnknownOpcode` en `:3065-3091`.

Aucun des 5 gestionnaires n'écrit `Result` (FAIT : `SM4/vectors/read-write-sets.json`, aucune écriture de `state.result`).

**SM4-R1 : `0x72`** (`Script_114_072`, `0x8003FA24`, taille 1).
- FAIT : le gestionnaire ne lit ni les octets de l'instruction ni le propriétaire. Il lit seulement `+0x88/+0x8C` de l'entité logique et écrit `+0x26C/+0x270`.
- Code :
  ```csharp
  entity.LastTargetAnimationId = entity.TargetAnimationId;
  entity.LastTargetDirection = entity.TargetDirection;
  return 1;
  ```
- Les champs sont `uint` (`EntityScriptProxy.cs:1225-1226`). Aucune garde de statut, aucune recherche, `Result` intact.
- La sauvegarde au départ d'un programme A/D/E/F est en `EventProgramRunner.cs:225-230`. La restauration à la reprise d'un C est en `:200-204`. Les deux existent déjà.

**SM4-R2 : `0x88`** (`Script_136_088`, `0x8004011C`, taille 3).
- Recherche : `EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity)`. La référence est l'entité logique. Le type 1 (héros) passe par `PlayerEntity`. Sans `PlayerEntity`, le type 1 ne rend rien et ne lève pas d'exception.
- Écriture : `match.Status = StatusByIndex[v[2]]`, avec la table `{Normal, Deactivated, FlagToDestroy}` = les mots `{2,3,4}` de `0x80023D2C`.
- C'est une écriture simple du champ `Status` (`EntityScriptProxy.cs:88`), pas `DestroyEntity`. Ni `EventTrigger` effacé, ni effet de bris, ni butin. Même patron que `case 0x19` (`:710`) et `case 0x5D` (`:1163`).
- Indice `v[2] >= 3` : rien d'écrit, un avertissement une fois, taille 3. Précédent D-E19-22 pour `0x40/0x41` (`:836-858`, aide `:550`).
- FAIT : le mot d'indice 3 est « No.1 » (`0x312E6F4E`), l'indice 4 du Shift-JIS. Aucun des 3 sites n'a d'indice supérieur à 2 (`88 80 00` ×2, `88 80 02`).
- Statut 4 : recyclé au tick suivant. `RecycleDestroyedEntities` (`WorldProxy.cs:3265`) est appelé après chaque passe des événements de carte (`:2568-2572`). C'est tenu par le plan E19.r (R3, ADR-0024). Le héros n'est jamais recyclé.

**SM4-R3 : `0x96`** (`Script_150_096`, `0x800409A8`, taille 3).
- FAIT : l'octet 1 sert à la fois de type de recherche et de montant. L'octet 2 n'est jamais lu (il n'apparaît pas dans les lectures).
- Pour chaque entité trouvée : `var sum = unchecked(m.Hp + v[1]); m.Hp = m.HpMax < sum ? m.HpMax : sum;`
  - `Hp` et `HpMax` sont des `int` (`EntityScriptProxy.cs:89-90`). Le projet n'active pas `CheckForOverflowUnderflow` (INFÉRENCE : grep sur les `.csproj` et `.props`).
  - Pas de plancher.
  - Pas de test de vie.
  - Un `Hp` supérieur à `HpMax` est ramené à `HpMax`.
  - Un cadavre trouvé par identifiant brut est soigné.
- Règle M7 (cible qui ne porte pas sa vie).
  - Écriture sur `Hp` de l'entité, toujours, sans branche.
  - Un héros `LifePulledFromStats` (tous ceux d'un monde, `WorldProxy.cs:2018-2023`) voit ses statistiques suivre en fin de `MovePlayer` (`PlayerManager.cs:634-644`, existant).
  - Un héros nu de test, un PNJ ou une entité sans `LifePulledFromStats` : l'entité seule change.
  - L'opcode n'écrit jamais `PlayerStats`.
  - Il n'appelle pas `HudDirector.SnapDisplayedLifeAndMagic` (FAIT binaire, pas d'`InitializeHpAndMp`). La jauge remonte par roulement.
- FAIT (décodage linéaire) : tous les `96 80` du corpus ont le héros comme entité logique. Ceux des programmes B le tiennent de `WorldProxy.cs:3206`. Pour 394 C[3] et 475 B[2], un `0x42` précède (pc 592 et pc 139).
- Montants des 42 sites : 128 (`80`) ou 129 (`81`), toujours soin complet pour `HpMax` ≤ 127.

**SM4-R4 : `0x97`** (`Script_151_097`, `0x80040A2C`, taille 2).
- FAIT : le gestionnaire appelle `IncreaseMp` (`0x8004DF10`), pas `SpendMoney`. Il lit `stats.mp` et `stats.mpMax`, jamais `money`.
- Code : `PlayerManager.IncreaseMp(GameState state, int amount) => SetPlayerMp(state, ClampToShort(state.PlayerStats.Mp + amount))`, à placer près de `SetPlayerMp` (`PlayerManager.cs:1970`).
- La somme est en 32 bits, saturée en `short` avant `SetPlayerMp`. `ClampToShort` est privé (`:647`), d'où l'aide dans `PlayerManager` et non en ligne dans le runner.
- Piège pinné par U97-9 : avec `Mp = 32767` et `+255`, le binaire donne `MpMax`. Un `(short)(Mp + v1)` donnerait 0.
- Ni recherche, ni `Result`, ni instantané du HUD.
- E20.h4a prévoit `DecreaseMp` dans le même fichier (`docs/plan-e20-heros.md:8141, 8165`). Conflit de fusion textuel possible, voir §6.

**SM4-R5 : `0xB3`** (`Script_179_0B3`, `0x80041750`, taille 9).
- Code :
  ```csharp
  _gameState.TickPad.Overwrite(v[1]|v[2]<<8, v[3]|v[4]<<8, v[5]|v[6]<<8, v[7]|v[8]<<8);
  return 9;
  ```
- Couture : `internal void Overwrite(uint hold, uint pressed, uint released, uint interval)` dans `Hero/TickPad.cs`, entre `Update` (`:56`) et `Reset` (`:101`). Elle écrit les quatre propriétés, masquées à 16 bits, et ne touche ni `RepeatInterval`, ni `IsOverThanMaxNbFrameHeld`, ni `NumberOfFrameHold`.
- Ordre d'un tick, FAIT (`WorldProxy.cs:2527-2582`) :
  1. `TickPad.Update`
  2. enregistrements `_squareOfTick` et `_choicePadOfTick` (`:2528-2529`)
  3. `RunMapEventsPass` (`:2568`), où tourne `0xB3`
  4. `RecycleDestroyedEntities` (`:2572`)
  5. `RunEntityBlock` (`:2582`), dont `MovePlayerOnTickPad(PadState.FromTickPad(GameState.TickPad…))` (`:2927`)
- C'est l'ordre du binaire : `RunMapEvents` avant `UpdateEntities`.
- La direction est lue dans `pad.ButtonsHold >> 12` (`PlayerManager.cs:360`). Aucun consommateur du stick hors `StickProbe` (FAIT : grep de `StickX`, `StickBitsHold`, `DigitalDirectionBitsHold`).
- Le tick suivant calcule ses fronts depuis le mot forcé, car `Update` relit `ButtonsHold` (`TickPad.cs:56-98`).
- Limites, INFÉRENCE :
  - Dans un monde qui ne pilote pas les ticks, le héros lit `SelfDrivenTickPad` (`EntityScriptProxy.cs:1554`), que `Overwrite` n'atteint pas. Production et `ArcRun` ont `DrivesEntityTicks` vrai (`WorldProxy.cs:821`, car `world.Game != null`, posé par `SaveScreenTestWorlds.WorldWithUIView`).
  - Défaut Q-sm-3 : les enregistrements par tick (boîte, choix, sauvegarde) ne sont pas réécrits. Le seul site force `hold = 0x1000` et trois mots nuls. Une garde de corpus (RB-SHAPES) le fige.
- FAIT : `0x2F` ne gère que les modes 0 et 1 (`EventProgramRunner.cs:1829-1840`). Le binaire lit le mode 2 (Released) et les autres modes (Interval). Mais les 352 sites de `0x2F` du corpus n'utilisent que les modes 0 (341) et 1 (11) (`census-operands.tsv`). Sans conséquence ici.

**SM4-R6 : libellés** (`Events/EventOpcodeSizeTable.cs`).

| Ligne | Aujourd'hui | Après |
|---|---|---|
| `:184` (`0x88`) | `""` | « Set status of matching entities (0 Normal, 1 Deactivated, 2 FlagToDestroy) » |
| `:199` (`0x97`) | « Spend money » | « Add magic points » |
| `:198` (`0x96`) | « Restore HP » | optionnel : « Add HP to matching entities (amount = search byte) » |
| `:227` (`0xB3`) | « Set pad buttons » | optionnel : « Set pad words (hold, pressed, released, interval) » |
| `:160` (`0x72`), `:63`, `:148`, `:170` | — | inchangés |

- Le commentaire de classe gagne une phrase.
- Le pin P-LBL est détaillé au §3.

**SM4-R7 : tenue des comptes** (même commit que le code).
- `IntroTraceHarnessTests.ImplementedOpcodes` (`:332-387`) reçoit `0x72, 0x88, 0x96, 0x97, 0xB3`. Aucun test ne lit cette différence : la 389 n'a aucun de ces opcodes. C'est du suivi, pas une preuve.
- Liste sm0 : −90 lignes, `ClosedOwners += "sm4"`, comptes épinglés mis à jour. Détail au §5.

**§1.8 : `0x15`, `0x66`, `0x7A`** (0 site).
- FAIT : le binaire de `0x15` (`0x8003D710`) pose `PosZ = ((record[9]<<3) − [+0x1EC]) << 16`. Avec un pointeur d'enregistrement nul il appelle le message fatal `0x8003C840`.
- `0x7A` (`0x8003FB8C`) : si `Result ≠ 0` il rend 3. Sinon `_34 = pc+3` et il rend le saut signé 16 bits. C'est le miroir de `0x79`.
- `0x66` (`0x8003F794`) copie toujours l'état partagé `0x8013D9E8` (et non `a3`) vers l'état du propriétaire. Puis `LastTarget = Target` de l'entité logique, et curseur = `Parameters[0]` = pc + décalage.
- Recommandation : les laisser sautés, 0 site. Raisons :
  - `0x15` demande un ADR sur le repère Z (ADR-0026) et une politique pour l'enregistrement nul.
  - `0x66` lit un état global que le runner expose mal.
- Esquisse si l'auteur veut les porter :
  - `0x7A` : `if (state.Result == 0) { state._34 = state.CodeIndex + 3; return SignExtend16((v[2]<<8)|v[1]); } return 3;`
  - Pour `0x66`, INFÉRENCE : `owner.EventProgramState.CopyFrom(_slotAScratchState)`, puis curseur et `Parameters[0]` posés.
- Vecteurs dans `SM4/vectors/zero-site/` (8, 8 et 5 vecteurs).

## 2. L'oracle de valeurs gelé

**Fichiers.** `SM4/vectors/` contient :
- `op72.json op88.json op96.json op97.json opB3.json`
- `zero-site/op15.json op66.json op7A.json`
- `mapping.json`, `read-write-sets.json`, `counts.json`
- `README.md` (format, recette de rejeu C#, notes de domaine)
- `MANIFEST.sha256`

**Méthode (FAIT).**
- Les VRAIS gestionnaires de la table `0x80098FAC` et la vraie recherche `0x8003C954` tournent dans l'interpréteur MIPS de l'annexe (`emu.py`, copie identique). Je l'ai instrumenté (`SM4/tools/temu.py`) pour journaliser chaque octet de donnée lu ou écrit.
- Seuls sont remplacés par des no-op `0x8003C914` (validateur d'id, fatal en cas d'id invalide) et `0x8003C840` (message).
- Un modèle python écrit d'après le désassemblage tourne à côté. Tout désaccord arrête le générateur. Aucun désaccord.

**Comptes.**

| | 72 | 88 | 96 | 97 | B3 | Total |
|---|---|---|---|---|---|---|
| Vecteurs | 67 | 83 | 79 | 74 | 67 | 370 |
| dont cas limites écrits à la main | 7 | 23 | 19 | 14 | 7 | 70 |
| dont aléatoires (graine 20261018) | 60 | 60 | 60 | 60 | 60 | 300 |
| Rejouables par valeur | 67 | 61 | 79 | 74 | 67 | 348 |
| Rouges aujourd'hui | 66 | 30 | 45 | 64 | 67 | 272 |
| Gardes vertes | 1 | 31 | 34 | 10 | 0 | 76 |
| Hors table (`outOfTable`) | 0 | 22 | 0 | 0 | 0 | 22 |

- « Rouge aujourd'hui » veut dire que le vrai gestionnaire change au moins une valeur observable. La DLL saute l'opcode par sa taille et n'écrit rien, taille égale.
- Les 22 vecteurs hors table de `0x88` stockent le mot que le binaire lit après sa table. La règle R2 les ignore, ils ne sont donc pas rejoués par valeur.

**Cas limites couverts.**
- Bornes : somme égale à `HpMax`, `MpMax = 0`.
- Signes : `Hp` négatif sans plancher, `Mp` négatif plancher 0, `HpMax` négatif.
- Groupes vides : `0x8B`, joueur seul avec N = 0, référence inactive.
- Références nulles : `ridingSlot`, `xCollSlot`, `parentSlot` à −1.
- Dépassement 32 bits : `Hp = 0x7FFFFFFF` + 129 devient négatif et est écrit tel quel.
- Pas de dépassement 16 bits pour `0x97`.
- Octet 2 de `0x96` ignoré.
- Entité logique différente du propriétaire.
- Entités au-delà de N : pièges actifs qui ne doivent jamais être touchés.
- Cadavre trouvé par identifiant brut.

**Deux exécutions identiques (FAIT).** `diff -r` sans écart, sha256 dans `SM4/runs/two-runs-identical.txt` et `vectors/MANIFEST.sha256`.

**Rejeu depuis le JSON seul (FAIT).** `tools/replay_vectors.py` rejoue les 348 vecteurs rejouables sur les vrais gestionnaires, 0 écart. Le format est donc complet, sans entrée cachée.

**Accord des oracles de l'annexe (copies, FAIT).**
- `oracle.py` au `N = 3000`, graine 20261018 : 63 000 exécutions, 0 écart.
- Oracle du critique, 21 opcodes au `N = 2000` : 0 écart.
- Copie restreinte aux 5 opcodes au `N = 4000` : 0 écart.
- Plus les passes `N = 300` et `N = 1000` de départ.
- Résultats dans `SM4/runs/`.

**Rattachement octet par octet à un membre de la DLL.**
- Tableau complet dans `mapping.json`. Résumé :
  - **Entité logique et tableau d'entités.**
    - La liste d'entités (`+0x294×i`) est `SpawnedEntities[i]`. Le héros est l'élément 0, avec `IsPlayer` et `EntityRefId = −1`.
    - N (`0x80134360`) est `SpawnedEntities.Count − 1`.
  - **Champs d'entité.**
    - `+0x0C`, `+0x10`, `+0x14`, `+0x18`, `+0x28` : `ParentEntity`, `Status`, `Hp`, `HpMax`, `PlatformEntity`.
    - `+0x48`, `+0x6C`, `+0xB4` : `EntityRefId`, `Flags`, `AnimFlags`.
    - `+0x88`, `+0x8C`, `+0x26C`, `+0x270` : `TargetAnimationId`, `TargetDirection`, `LastTargetAnimationId`, `LastTargetDirection`.
    - `+0x12C`, `+0x130` : `RidingEntity`, `XCollisionEntity`.
  - **Autres zones.**
    - Statistiques via `[0x80176318]` : `PlayerStats.Hp/HpMax/Mp/MpMax/Money`, des `short`.
    - Pavé `0x80126F28/2A/2C/2E` : `TickPad.ButtonsHold/JustPressed/Released/JustPressedByInterval`.
    - Table `0x80023D2C` : nouvelle table statique de 3 statuts.
- Octets lus par le gestionnaire, union des vecteurs :
  - `0x72` : seulement `targetAnimId` et `targetDir` du slot logique.
  - `0x88` : octets 1 et 2, mot de la table, champs de recherche, N.
  - `0x96` : octet 1, `hp`, `hpMax`, champs de recherche, N.
  - `0x97` : octet 1, `stats.mp`, `stats.mpMax`.
  - `0xB3` : octets 1 à 8 seulement.
- Un octet sans rattachement est un ARRÊT : aucun rencontré. Autotest `tools/selftest_unmapped_stop.py` (FAIT) : en retirant `+0x6C` du rattachement, le générateur s'arrête bien.

**Réserves.**
- Les vecteurs à identifiant brut stubbent le validateur du binaire, fatal en cas d'id invalide. Ils fixent la DLL, ils ne sont pas des états atteignables avec un id invalide dans l'original.
- Les types de groupe ≥ 12 sont exclus des vecteurs : fatals dans le binaire (INFÉRENCE : BIOS exit, `check.md` R2).
- Des valeurs larges (`Hp = 0x7FFFFFFF`, `Mp = 32767`) sont inatteignables en jeu. Elles figent l'arithmétique du binaire. Si l'auteur préfère une saturation, on retire U96-8 et la ligne correspondante.

## 3. Tests nommés

### 3.1 Montage commun

- **Pas de partage possible avec `AlundraEventProgramRunnerTests`.** `NewDocument`/`NewRunner`/`NewEntity` y sont privés (`:28-41`). Je propose une classe support `ActionOpcodeMontage`, qui reprend le patron de `AlundraClassAndBranchOpcodesTests`.
- **Contenu du support.**
  - Un `Context` privé qui enregistre `DestroyEntity`.
  - Un `Run(int[] codes, owner, context, GameState, …)` à `ResultBefore = 7`.
  - Un constructeur de monde à partir de la description du vecteur.
- **Préconditions.**
  - Chaque proxy a un `LogicContextEntity` non nul (sinon le piège `null == null` des types 5 à 10 de la recherche).
  - Le héros a `IsPlayer` et `EntityRefId = −1` (FAIT : les proxies nus ont 0 par défaut, `EntitySpawnFactory.cs:497`).
  - Propriétaire ≠ logique : `owner.LogicEntity = logique`.
- **Noms de classes** (après R5, sans préfixe) : `EntityStateOpcodesTests` (0x72, 0x88), `LifeAndMagicOpcodesTests` (0x96, 0x97), `PadWriteOpcodeTests` (0xB3), `ActionOpcodeVectorTests`, `ActionOpcodeSiteTests`, ajouts à `TickPadTests`. À passer par la garde R7.
- Colonne « rouge » : ce que la DLL donne aujourd'hui (saut par taille, `Result` périmé). L'assertion sur `CodeIndex` (taille) est déjà verte. Seule la valeur est rouge.
- Les 44 tests unitaires de ce tableau sont dans `SM4/named-tests-audit.json`. 43 sont audités contre le vrai binaire (FAIT). U88-9 est une politique, non auditée. Leurs valeurs attendues sont donc celles du binaire.

### 3.2 Tests unitaires du gestionnaire

Les nombres sont en décimal ou en valeur lue.

| Id | Octets | Départ | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|
| U72-1 | `72` | logique = propriétaire, Target (5,9), Last (1,2) | Last (5,9) | Last (1,2) |
| U72-2 | `72` | propriétaire héros ; logique PNJ Target (3,21), Last (99,98) | PNJ Last (3,21), héros Last (0,0) | PNJ (99,98) |
| U72-3 | `72` | Target `0xFFFFFFFF/0xFFFFFFFF`, Last 0 | Last `0xFFFFFFFF` | 0 |
| U72-4 | `72` | Target 0, Last (0x41,0x1F) | Last (0,0) | (0x41,0x1F) |
| U72-5 | `72` | logique `FlagToDestroy`, Target (8,2) | Last (8,2) | (0,0) |
| U72-6 | scénario 3 appels | voir ci-dessous | Target (7,10) | (3,5) |

- U72-6, scénario : programme C `[00,FF]`, programme F `[09 0A, 1A 07, 72, FF]` avec tables `C=[0]`, `F=[2]`. Appels `RunScript(C)`, `RunScript(F)`, `RunScript(C)`, avec Target initial (3,5). Après F, Last = (7,10). Après la reprise de C, Target = (7,10). Aujourd'hui Target retombe à (3,5).

| Id | Octets | Départ | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|
| U88-1 | `88 80 00` | PNJ `Deactivated` | `Normal` | reste 3 |
| U88-2 | `88 80 01` | PNJ `Normal` | `Deactivated` | reste 2 |
| U88-3 | `88 80 02` | PNJ `Deactivated` | `FlagToDestroy` (4) | reste 3 |
| U88-3b | `88 80 02` | contexte enregistreur | `DestroyEntity` jamais appelé, `EventTrigger` intact | statut inchangé |
| U88-4 | `88 82 00` | statuts hero=3, n1=1, n2=3, n3=4, n4=0, n5=5, n6=2 ; logique n1 | hero, n1, n2, n6 à 2 ; n3, n4, n5 intacts | non écrits |
| U88-5 | `88 81 02` | héros 2 | héros 4, PNJ 2 | 2 |
| U88-7 | `88 07 00` | id 7 : n1 statut 4, n2 statut 0, n4 statut 2 ; n3 id 6 est la référence | n1, n2, n3, n4 à 2 | n1=4, n2=0 |
| U88-6, U88-8, U88-10 | groupe `8B` vide ; référence inactive ; sans `PlayerEntity` | — | rien d'écrit, taille 3, pas d'exception | gardes vertes |
| U88-9 | `88 80 03` (aussi 4 et 255) | PNJ `Deactivated` | statut inchangé, un avertissement | garde verte (politique) |

| Id | Octets | Départ | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|
| U96-1 | `96 81 FF` | héros Hp 10/50, stats 33/44 | Hp 50, stats 33/44 | Hp 10 |
| U96-2 | `96 80 32` | logique = héros 1/20 | Hp 20 | Hp 1 |
| U96-3 | `96 80 00` | idem avec octet 2 = 0 | Hp 20 | Hp 1 |
| U96-4 | `96 14 32` | id 20 : n1 3/50, n2 45/50 ; n3 id 5 1/50 | n1 23, n2 50, n3 1 | 3, 45 |
| U96-5 | `96 14 00` | id 20, Hp 30/50 | 50 | 30 |
| U96-6 | `96 81 00` | Hp 120/HpMax 10 | 10 | 120 |
| U96-7 | `96 14 00` | Hp −200/50 | −180 | −200 |
| U96-8 | `96 81 00` | Hp 0x7FFFFFFF/50 | −2147483520 | 2147483647 |
| U96-9 | `96 81 00` | Hp 5/HpMax −1 | −1 | 5 |
| U96-10 | `96 09 00` | cadavre statut 4, Hp 0/30, id 9 | 9 | 0 |
| U96-11 | `96 82 00` | hero 1/10, n1 5/200, n2 statut 4 1/10, n3 130/140 | 10, 135, 1, 140 | 1, 5, 1, 130 |
| U96-12 | `96 81 00` | Hp 2/30, stats 7/9 | Hp 30, stats 7/9 | Hp 2 |
| U96-13, U96-14, U96-15 | groupe vide ; sans `PlayerEntity` ; pas de `Snap` | — | rien d'écrit / affichage HUD inchangé | gardes vertes |
| U96-16 | héros `LifePulledFromStats` 10/50, stats 10/50 | puis `PlayerManager.EndOfMovePlayer` | Hp 50 tout de suite, stats 10 ; puis stats 50/50 | Hp 10 |

- U96-15 : l'affichage HUD vaut (10,20,0,2) pour des maxima de stats (50,4). Un `Snap` donnerait (50,50,4,4). Test vert aujourd'hui, il tue le mutant MS-2.

| Id | Octets | Départ | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|
| U97-1 | `97 32` | Mp 0/4, Money 777 | Mp 4, Money 777 | Mp 0 |
| U97-2 | `97 01` | Mp 1/4 | 2 | 1 |
| U97-5 | `97 00` | Mp 4/MpMax 2 | 2 | 4 |
| U97-6 | `97 00` | Mp −1/4 | 0 | −1 |
| U97-7 | `97 01` | Mp −1/4 | 0 | −1 |
| U97-8 | `97 01` | Mp −3/4 | 0 | −3 |
| U97-9 | `97 FF` | Mp 32767/4 | 4 | 32767 |
| U97-10 | `97 00` | Mp −32768/4 | 0 | −32768 |
| U97-11 | `97 00` | Mp 0/MpMax −1 | −1 | 0 |
| U97-12 | `97 0A` | Mp 3/4 | 4 | 3 |
| U97-3, U97-4, U97-13 | plein ; `MpMax = 0` ; pas de `Snap` | — | rien de changé | gardes vertes |

| Id | Octets | Départ | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|
| UB3-1 | `B3 34 12 78 56 BC 9A F0 DE` | mots 0 | hold 0x1234, pressed 0x5678, released 0x9ABC, interval 0xDEF0 | 0 partout |
| UB3-2 | `B3 00 10 00 00 00 00 00 00` | mots 0 | hold 0x1000, le reste 0 | hold 0 |
| UB3-3 | `B3` + 8×`00` | mots avant 0x1234/0x00F0/0x8001/0xFFFF | tous à 0 | mots d'avant |
| UB3-4 | `B3` + 8×`FF` | mots 0 | tous à 0xFFFF | 0 |
| UB3-5 | forme du site, logique PNJ | mots 0 | hold 0x1000 | hold 0 |
| UB3-6 | `B3 00 10 80 00 00 01 00 02` puis `2F 00 10 00` | — | `Result` 1 ; `2F 80 00 01` donne 1 ; `2F 00 20 00` donne 0 | `Result` 0 |
| UB3-7 | après 3×`Update(0x1000)` | `NumberOfFrameHold` 2, stick de `LastPadState` à 0,5 | hold 0x1000, compteurs 2/0/0, stick intact | hold 0 |

- UB3-6 : le binaire donne `2F 00 20 00` = 0, `2F 80 00 01` = 1 (modes 0 et 1 relus sur le vrai gestionnaire).
- UB3-3 et V-B3 posent leur état initial avec la couture `Overwrite`.

### 3.3 Couture `TickPad.Overwrite`

- **Tests du tick pad** (compilent avec la couture, verts d'emblée) :
  - S-1 : mots écrits et compteurs gardés (3×`Update`, puis `Overwrite(0x1234, 0x5678, 0x9ABC, 0xDEF0)`).
  - S-2 : `Overwrite(0x1000,0,0,0)` puis `Update(0)` donne pressed 0, released 0x1000, hold 0, compteurs remis à 0.
  - S-3 : après 3×`Update(0x1000)`, `Overwrite`, puis `Update(0x1000)` donne `NumberOfFrameHold` 3 et interval 0.
- **Rouge d'abord pour les tests de l'opcode.** Précédent L1-1 : un seul changement préalable sans effet, la couture sans appelant de production. Les tests de `0xB3` sont rouges tant que le `case` manque.

### 3.4 Rejeu des vecteurs

- Classe `ActionOpcodeVectorTests`. Les fichiers sont cherchés en remontant depuis `AppContext.BaseDirectory` vers `docs/plan-e19-sm-annexe/sm4/vectors`. Précédent : `AlundraSpawnIndexOracle.OutputFolder`.
- `Theory` par opcode : V-72, V-88, V-96, V-97, V-B3.
- V-OOT : les 22 lignes hors table n'écrivent rien (vert aujourd'hui).
- V-MANIFEST : le sha256 de chaque fichier égale `MANIFEST.sha256`.
- Rouges aujourd'hui : 272 lignes. Vertes : 76 lignes plus les 22 hors table.

### 3.5 Épingles

- **P-WIT : le témoin `UnknownOpcode_KnownSize_SkipsBySize`** (`AlundraEventProgramRunnerTests.cs:331-343`).
  - Repointé sur `0xB9`, taille 2 (`EventOpcodeSizeTable.cs:233`) : `NewDocument(0xB9, 0, 0x1A, 4, 0xFF)`, attendu `TargetAnimationId == 4`. Le commentaire `:333-334` gagne l'historique.
  - `0xB9` reste sauté tant que l'auteur n'a pas tranché O-E19-36 (sm7). `0x5F` a déjà son propre test.
  - La version `0xB3` resterait verte mais vidée de son objet (check E2).
  - Vert avant et après.
- **P-LBL** : `Entries[0x88].Name` non vide, et `Entries[0x97].Name` sans « money ». Rouge aujourd'hui.

## 4. Vérifications sur cartes réelles

### 4.1 Ce que couvre chaque famille de tests

**Octets réels.** Test sur le patron `IsForceAdjusted_0x6E_RealOccurrence_AncientShrine27` (`:4155-4180`) : document chargé par `MapEventProgramLoader.Load`, octets relus au pc réel, exécutés dans un `RunOneScriptCall`. Convention d'échec si l'export manque, comme `ArcRun`.

| Id | Cible | Attendu | Comportement actuel décodé (FAIT) |
|---|---|---|---|
| RB-452-B2 | `96 80 32` @204 de 452 B[2] ; héros 5/40 | 40 | `96` sauté, Hp reste 5 |
| RB-452-B3 | `97 32` @330 de 452 B[3] ; Mp 1/4, Money 500 | Mp 4, Money 500 | Mp reste 1 |
| RB-394-C3 | `96 80 32 97 0A` @749..753 de 394 C[3] (logique héros après le `0x42` @592) | Hp plein, Mp +10 plafonné à 4 | sauts, `CodeIndex` final 5 |
| RB-475-B2 | `96 81 32 97 32` @502..506 de 475 B[2] (logique héros après le `0x42` @139) | idem | idem |
| RB-22-C6 | `96 81 FF` @863 de 22 C[6] | Hp plein | Hp reste |
| RB-306-E14 | `88 80 00` @1110 | `Deactivated` → `Normal` | reste `Deactivated` |
| RB-442-E1 | `88 80 02` @1312 | `FlagToDestroy` | reste `Deactivated` |
| RB-444-D15 | `2B 09 10 72` @1299..1302 de 444 D[15] | ClassA effacée, direction 16, Last = Target | Last inchangé |
| RB-140-D2 | `27 0A BD DC 01 72` @2032..2037 de 140 D[2] | Last = Target et ≠ sentinelle 0x77 | Last reste 0x77 |
| RB-161-B1 | `B3 00 10 00 00 00 00 00 00` @54 | hold 0x1000 | `TickPad` inchangé |

- RB-SHAPES : les 90 sites ont aux (carte, pc) les octets de la liste, et leurs opérandes restent dans les formes couvertes. Garde de dérive de l'export, verte.
- RB-SAVEROOMS : les 19 cartes 452..470 ont `96 80 32` @204 (B[2]) et `97 32` @330 (B[3]). Garde verte.
- Pourquoi pas d'arc pour 394 C[3], 475 B[2] et 444 D[15] : longues scènes à boîtes de choix (394), flag `0x0666` plus `ForceAdjusted` plus Haut (475), attaque de type 6 (444).

**Couverture exacte des 90 sites** (`SM4/corpus/sm4-sites-coverage.tsv`) :
- Arc : 52 en octets réels ou arc ciblé. Ces 52 se répartissent ainsi.

  | Test exact | Sites |
  |---|---|
  | ARC-161 (a et b) | 1 |
  | ARC-306 | 2 |
  | ARC-40 | 1 |
  | ARC-442 | 1 |
  | ARC-452 | 2 |
  | RB-140-D2 | 1 |
  | RB-22-C6 | 1 |
  | RB-394-C3 | 2 |
  | RB-444-D15 | 1 |
  | RB-475-B2 | 2 |
  | RB-SAVEROOMS | 36 |

- Reste 40 sites couverts par la forme (RB-SHAPES et les vecteurs de même forme).

### 4.2 Arcs `ArcRun`

Aucun test existant ne charge ces cartes (grep des 50 cartes dans `Alundra.Tests` : seuls `Cave-140` et `Lizardman's Lair (Boss)-411`, par les tests de séquence réelle, insensibles ; `BackdropStageDefinitionTests` charge la 159 en compagnon). Tout ce qui suit est INFÉRENCE tant que T0 ne l'a pas mesuré.

Tous les noms de dossier de zone se trouvent par suffixe `-NNN`, comme `AlundraHeightTargetSitesTests.cs:76-82`. Cela évite le piège de l'apostrophe typographique de « Nirude’s Lair » (la carte 40).

| Arc | Monde et montage | Départ | Signal de fin (trace) | Limite provisoire | Attendu | Rouge aujourd'hui |
|---|---|---|---|---|---|---|
| ARC-452 | `Save Room` / `Save Room (Coal Mine)-452`, `RealController` + `Prefabs` + `Arrival` à la case (13,12), Z 1<<20 | aucun drapeau ; `Hero.HpMax=40`, `Hero.Hp=5`, `SetMp(4,1)`, `Money=500` | B[2] : `(slot 1, pc 207, 0x11)` puis fermeture du dialogue par le bouton ; B[3] après `PlaceHero((16×24+12),(12×16+8),16)` : `(1, 332, 0x11)` | 700 | `Hero.Hp == 40` au signal ; stats 40 après 2 images ; `Mp == 4`, `Money == 500`, aucun opcode sm4 dans `SkippedOrExceeded` | Hp 5, Mp 1, la liste des sautés nomme `0x96 @204` et `0x97 @330` |
| ARC-161a | `Fairy cave` / `Fairy cave (Stenia)-161`, nu, aucun drapeau, 30 images | le programme place le héros en (10,56,1) | `(1, 54, 0xB3)` exécuté | 30 | à chaque image où `0xB3` a tourné, le pavé vu par `MovePlayer` (`PadOfTickSeenForTests`) vaut hold 0x1000, pressed 0, released 0 | hold 0, `0xB3 @54` sauté |
| ARC-161b | idem, `RealController` + `Prefabs` | idem | `(1, 67, 0x8B)` avec héros en (6..13, 29, 3) ; l'image après le dernier `0xB3` montre released 0x1000, hold 0 | 800 | idem | arrêt nommé « `0xB3 @54 (UnknownSkipped)` » |
| ARC-306 | `Meia Dream` / `Meia Dream-306`, nu, 40 images | enregistrements 13 et 14 mis à `Deactivated` avant la première image | E[14] : `(4, 1113, 0xFF)` ; E[15] : `(4, 1125, 0xFF)` | 40 | `88` exécuté une fois par programme, statut `Normal` | `88` exécuté à chaque image (sauté), statut reste 3 |
| ARC-442 | `Meia Dream` / `Meia Dream-442`, nu | enregistrement 0 mis à `Deactivated` | E[1] : `(4, 1315, 0xFF)` | 40 | statut 4 au `FF` ; absent de `Entities` au plus 2 images plus tard ; le proxy relu a `Status == Destroyed`, `EntityRefId == −1` | reste `Deactivated`, rejoue son E à chaque image |
| ARC-40 | `Nirude’s Lair-40`, nu | enregistrement 0 dans sa boucle C[2] (`(2, 605, 0x37)` vu) ; `((IScriptHost)arc.Proxy).ActiveCollisionEntity = entité` (précédent `AlundraInoaDayOneArcTests.cs:370`) | `(5, 1154, 0xFF)` | 120 | à `(5,1153,0x72)` : Last = Target = (1, dir) ; la première instruction C suivante garde la direction | Last = valeurs d'avant F |

Faits de décodage utilisés :
- **452 B[2] et B[3]** : `3B` hero en (13,12,1) pour B[2], (16,12,1) pour B[3]. Puis `70 Is above ground`, qui lit `entity.IsOnGround` et vaut 0 sans contrôleur (`EventProgramRunner.cs:1782-1788`). D'où le contrôleur réel.
- **452, effet du dialogue** : `96` suit `0D` sans attendre la fin du dialogue (@201-204). La guérison est donc au moment où la boîte s'ouvre. Les entités sont gelées tant que la boîte est ouverte, donc les stats ne suivent qu'après sa fermeture.
- **161 B[1]** : `64 81 F0 00 80 03 10 00` @36 place le héros au pixel (240, 896), soit la tuile (10,56). Puis boucle `3B 06 0D 1D 1D 03 03` @44, `03 10 00` @51, `B3` @54, `00` @63, `02 EC FF` @64. Sortie vers `8B` @67. La zone MapEvent_0 couvre toute la carte.
- **40 C[2]** : `2A`, `37 0A`, `1A 00`, `02 FC FF`. F[2] : `BD 12 00`, `27`, `0A`, `1A 01`, `3A 00`, `72`, `FF`.
- **306 E[14]** : `1B 00 00`, `40 02 00`, `88 80 00`, `FF`.
- **442 E[1]** : `BD 2D 00`, `A3 …`, `88 80 02`, `FF`.

Risques à mesurer en T0, tous INCONNU en lecture seule :
- **a)** Le héros de 452 atterrit-il et `70` devient-il vrai ?
- **b)** Les cartes se chargent-elles ? Aucun test ne les charge. Les prefabs absents se replient sur des entités nues (doc `ArcPrefabAssets`).
- **c)** Pour 161 : la tuile (10,56) est dans la zone du portail 0 (9..10, 56, vers 158). Le portail se déclenche-t-il au chargement ? Le chemin vers (6..13,29,3) est-il praticable dans la DLL ? Estimation de durée : 432 px à 2,4375 px par tick (`docs/plan-e20-heros.md:6515`), soit environ 178 ticks, limite provisoire 800.
- **d)** `PadOfTickSeenForTests` est un `[ThreadStatic]` statique : à poser et remettre dans un `finally`.
- **e)** Le singleton `HudDirector` est partagé en parallèle avec d'autres classes sans collection (`AlundraHudDirectorTests` n'en a pas) : les tests de non-instantané du HUD remettent le singleton à zéro avant et après. Risque P4.
- **f)** Les durées des tests.

Repli : si l'arc 161b ne peut pas atteindre `@67` pour une raison de terrain sans rapport avec `0xB3`, il est remplacé par ARC-161a (la sonde d'ordre tient en quelques images) plus RB-161-B1. Ce n'est pas un arrêt de la tranche.

L'épreuve de l'ordre repose sur ARC-161a. Une sonde indépendante de la marche existe aussi : `TickTraceForTests` (`WorldProxy`) émet "pad", "box", "mapEvents", "movePlayer" dans cet ordre. Elle prouve l'ordre des phases, mais pas que les mots forcés sont vus par `MovePlayer`.

## 5. Preuves de retrait, mutants, arrêts, budget, retour arrière

### 5.1 Retrait d'un élément, tests gardés

Catalogue : `SM4/tests-catalog.json` (83 entrées : 53 unitaires, 3 couture, 7 rejeu/garde de vecteurs, 12 octets réels, 6 arcs, 2 épingles). Matrice de retrait : `SM4/removal-matrix.json`. Elle découle des déclarations de dépendance du catalogue, pas d'une exécution.

Aujourd'hui 65 tests sont rouges (43 unitaires, 5 rejeux, 10 octets réels, 6 arcs, 1 libellé). 18 sont verts : 10 unitaires, 3 couture, 2 de vecteurs, 2 gardes d'octets réels, 1 témoin.

| On retire | Tests rouges | Lignes de vecteurs rouges |
|---|---|---|
| `case 0x72` | U72-1 à U72-6, V-72, RB-444-D15, RB-140-D2, ARC-40 (10) | 66 |
| `case 0x88` | U88-1 à U88-5, U88-7, U88-3b, V-88, RB-306-E14, RB-442-E1, ARC-306, ARC-442 (12) | 30 |
| `case 0x96` | U96-1 à U96-12, U96-16, V-96, RB-452-B2, RB-394-C3, RB-475-B2, RB-22-C6, ARC-452 (19) | 45 |
| `case 0x97` | U97-1, U97-2, U97-5 à U97-12, V-97, RB-452-B3, RB-394-C3, RB-475-B2, ARC-452 (15) | 64 |
| `case 0xB3` | UB3-1 à UB3-7, V-B3, RB-161-B1, ARC-161a, ARC-161b (11) | 67 |
| libellés | P-LBL (1) | — |

- RB-394-C3, RB-475-B2 et ARC-452 dépendent de deux `case` à la fois.
- Retirer la couture `Overwrite` casse la compilation de UB3-3, S-1, S-2, S-3 et V-B3. Ce n'est pas un rouge.
- Retirer tous les nouveaux tests en gardant le code ne rend rien rouge. Les seuls tests existants qui touchent ces opcodes ou ces cartes sont `UnknownOpcode_KnownSize_SkipsBySize` (déjà repointé) et les deux séquences réelles 140 et 411, insensibles (B[4]@744 de 140 et B[9]@1712 de 411 ne contiennent aucun site sm4).
- Conséquences sur la liste de sm0 :
  - Retirer le code seul, listes retirées : la règle 1 rougit sur les 90 sites non listés.
  - Garder les 90 lignes avec le code porté : la règle 2 rougit (lignes périmées) et (c4) aussi (propriétaire clos).
  - Oublier `sm4` dans `ClosedOwners` : rien ne rougit. C'est un trou de suivi, à couvrir par un contrôle de diff dans l'acceptation.

### 5.2 Mutants

Calcul sur un modèle python des règles contre les 44 tests unitaires et les 348 lignes de vecteurs (`SM4/mutants-matrix.json`, outil `tools/mutants.py`). Les 25 mutants ci-dessous sont tués par au moins un test unitaire nommé. Les lignes de vecteurs tuent aussi chacun d'eux, sauf M88e (0 ligne sur 61, hors table).

| Règle | Mutants | Tests unitaires tueurs |
|---|---|---|
| R1 | M72a pas de direction copiée ; M72b agit sur le propriétaire ; M72c copie inverse ; M72d garde de statut 1..3 | U72-1 à U72-5 ; U72-2 ; U72-1 à U72-5 ; U72-5 |
| R2 | M88a table permutée ; M88c seulement la première cible ; M88e indice > 2 écrit ; M88f référence = propriétaire | U88-1, 2, 4, 7 ; U88-4, 7 ; U88-9 ; U88-1, 2, 3 |
| R3 | M96a montant = octet 2 ; M96b pas de plafond ; M96c plancher 0 ; M96d n'abaisse jamais ; M96e somme saturée 64 bits ; M96f seulement statut 1..3 ; M96g saute Hp 0 | U96-3, 4, 5, 7, 8, 10, 11, 12 ; U96-1, 2, 3, 4, 6, 9, 11, 12 ; U96-7, 8, 9 ; U96-6, 8, 9 ; U96-8 ; U96-10 ; U96-10 |
| R4 | M97a dépense l'argent ; M97b somme coupée à 16 bits ; M97c pas de plancher ; M97d pas de plafond ; M97e plafond constant 4 | 11 tests dont U97-1, 2, 4 à 12 ; U97-9 ; U97-6, 8, 10 ; 9 tests ; U97-4, 5, 11 |
| R5 | MB3a seulement hold ; MB3b OU au lieu d'écraser ; MB3c gros-boutiste ; MB3d pressed/released permutés ; MB3e mots à 8 bits | UB3-1, 3, 4 ; UB3-3 ; UB3-1, 2, 5 ; UB3-1 ; UB3-1, 2, 4, 5 |

Mutants structurels, non calculables ici, avec le test qui les tue par construction :
- **MS-1** : `0x96` écrit aussi `PlayerStats` → U96-1 (stats 33/44) et U96-12.
- **MS-2** : `0x96`/`0x97` appelle `SnapDisplayedLifeAndMagic` → U96-15 et U97-13 (affichage (10,20,0,2) devient (50,50,4,4)).
- **MS-3** : `0x88` indice 2 passe par `DestroyEntity` → U88-3b.
- **MS-4** : `0xB3` écrit `LastPadState` au lieu de `TickPad` → UB3-6, UB3-1, ARC-161a.
- **MS-5** : `0xB3` appliqué après `MovePlayer` du même tick → ARC-161a (pad vu au tick du `0xB3`).
- **MS-6** : `Overwrite` remet les compteurs à zéro → S-1 et UB3-7.
- **MS-7** : `Overwrite` recalcule les fronts → S-2 et S-3.
- **MS-8** : `IncreaseMp` coupe en `short` → U97-9.

### 5.3 Conséquences sur la liste sm0 (règle 5)

FAIT, recompté sur `skipped-sites.tsv` (`SM4/corpus/sm0-rule5-counts.json`).

| | Avant | Après sm4 |
|---|---|---|
| Lignes | 510 | 420 |
| Instructions distinctes | 508 | 418 |
| Cartes | 96 | 53 |
| Opcodes | 21 | 16 |
| sm1 | 187 | 187 |
| sm2 | 129 | 129 |
| sm3 | 42 | 42 |
| sm4 | 90 | 0 (ligne supprimée) |
| sm5 | 17 | 17 |
| sm7 | 42 | 42 |
| sm8 | 3 | 3 |

- 43 des 50 cartes de sm4 perdent tous leurs sites. 7 gardent d'autres sites : 29, 54, 159, 160, 199, 421, 442.
- Les 90 lignes retirées sont : `96` ×42, `97` ×24, `72` ×20, `88` ×3, `B3` ×1.
- Les 5 `case`, l'aide `IncreaseMp`, les libellés et les 90 lignes partent dans le même commit (B11).
- Aucun site sur les 30 cartes de la chaîne : `story-chain-skipped-opcodes.tsv`, `MapsWithoutSkippedSite` et `PredicateOpcodes` ne changent pas.

### 5.4 Arrêts

1. Q-E19-SM1 autre que l'accord ; R5, R7 ou Q-R-7 non faites ; sm0 non exécutée.
2. Les générateurs ne redonnent pas les sha256 du manifeste ; `replay_vectors.py` rend un écart ; sha-1 du binaire différent.
3. Une valeur contredite par la DLL après le code : arrêt, jamais de ré-épingle (règle E19.l).
4. Un arc bloqué avant d'atteindre l'opcode pour une raison sans rapport : arrêt, repli sur octets réels, à soumettre.
5. RB-SHAPES ou RB-SAVEROOMS rouge à T0 : l'export a changé, recompter et soumettre.
6. Un test existant autre que ceux de la liste fermée bouge ; une des six traces change.
7. Un besoin de toucher à `WorldProxy` pour les enregistrements du pavé (Q-sm-3 autre que le défaut).
8. Une autre tranche a modifié `PlayerManager.cs`, `EventProgramRunner.cs` ou `TickPad.cs` en cours de route (E20.h4a) : conflit signalé.
9. Abandon par `--blame-hang-timeout`.

Liste fermée du code de test existant touché : `AlundraEventProgramRunnerTests.cs` (témoin), `AlundraTickPadTests.cs` (3 tests ajoutés), `IntroTraceHarnessTests.cs` (`ImplementedOpcodes`), et les deux fichiers de sm0 (liste et classe : suppression des 90 lignes et ajout de `sm4` à `ClosedOwners`). Production : `EventProgramRunner.cs`, `EventOpcodeSizeTable.cs`, `PlayerManager.cs`, `TickPad.cs`.

### 5.5 Budget et retour arrière

- **Budget** : un `executor`, deux tentatives. `plan-verifier` avant, `verifier` frais après.
- **T0** : copie de `SM4/` dans `docs/plan-e19-sm-annexe/sm4/`, deux rejeux depuis une copie, empreintes égales, mesures de départ. Aucun code de la DLL.
- **T1** : couture et ses tests dans un commit à part ; puis les tests, rouges consignés avant le code.
- **T2** : code, libellés, miroir et liste dans le même commit.
- **T3** : Release (copie neutralisée) puis Debug en dernier, `cmp`, six traces, mutations réelles par script.
- **T4** : plan, ADR.
- **T5** : `verifier` frais.
- **Retour arrière** : `git revert` du commit unique de code et de liste, puis du commit de couture. Retirer `docs/plan-e19-sm-annexe/sm4/`. Aucune donnée, aucun convertisseur, aucun moteur touchés.

## 6. Questions pour l'auteur, défauts, dépendances

Numérotation proposée : Q-sm-1 à Q-sm-3 existent déjà au plan ; Q-sm-11 à Q-sm-13 sont nouvelles.

**Q-sm-1 : `0x97`, ce que tu vois dans les salles de sauvegarde.**
- 24 sites : les 19 salles 452 à 470, plus 147, 327, 379, 394 (auberge) et 475.
- Option A (défaut, le binaire) : en y dormant, la jauge de magie se remplit jusqu'à 4 pastilles ; l'argent ne bouge pas. Coût : une aide `IncreaseMp` et un ADR.
- Option B (la décompilation) : l'argent baisse de 50 à chaque passage, la magie ne revient jamais.
- Option C (laisser sauté, situation actuelle) : la magie ne se remplit jamais dans ces salles.
- Recommandation : A.

**Q-sm-2 : `0x96`, le montant du soin.**
- Aujourd'hui, aucun des 42 sites de soin ne soigne.
- Binaire (défaut) : montant 128 ou 129, soin complet partout.
- Variante « octet 2 » (50 ou 255) : soin complet aussi, sauf pour un héros à 51 PV max et 0 PV (il remonterait à 50, un de moins).
- Aucune différence visible dans le jeu livré. Recommandation : suivre le binaire.

**Q-sm-3 : `0xB3`, pavé forcé en 161 (Stenia).**
- Aujourd'hui, le héros n'est pas poussé vers le nord à l'arrivée ; le joueur doit marcher lui-même jusqu'à la zone.
- Avec `Overwrite` seul (défaut) : il marche tout seul vers le nord jusqu'à la zone (6..13, 29, 3), puis la cinématique commence.
- Réécrire aussi les enregistrements lus par la boîte de dialogue, le choix et la sauvegarde ne se voit nulle part. Le seul site force « Haut » sans touche de bouton ni de choix. Coût : du câblage dans `WorldProxy` pour zéro différence visible.
- Défaut : `Overwrite` seul, avec la garde de corpus RB-SHAPES.

**Q-sm-11 : `0x15`, `0x66`, `0x7A` (0 site).**
- Défaut : les laisser sautés, vecteurs versés.
- Coût si on les porte : trois `case`, un ADR sur le repère Z pour `0x15`, un accès à l'état partagé pour `0x66`. Aucun effet visible.
- Effet de bord : le test de l'oracle qui exige qu'il reste un opcode sauté reste vert pour toujours (Q-sm-10 le dit).

**Q-sm-12 : `0x88`, indice 3 ou plus.**
- Défaut : rien d'écrit et un avertissement, comme D-E19-22 pour `0x40/0x41`.
- Aucun site : les trois sites utilisent les indices 0, 0 et 2. Aucun effet visible.

**Q-sm-13 : numéro d'ADR.** Le prochain libre est 0076. Plusieurs tranches E20 annoncent « un ADR » sans numéro, donc risque de collision : prendre le numéro libre au moment de l'exécution. Un ADR « sm4 : les opcodes d'action suivent le binaire » couvre `0x97` = `IncreaseMp`, la couture `0xB3` et la politique `0x88`.

**Recette de l'auteur.**
- Dans une salle de sauvegarde (par exemple 452) : les cœurs puis la magie remontent, l'argent ne bouge pas.
- En 161 (Stenia) : le héros marche seul vers le nord, puis la cinématique commence.
- En Meia Dream : en 442, une créature désactivée disparaît au lieu de rejouer son et son effet ; en 306, les deux créatures reviennent à l'état normal.
- En Nirude’s Lair (40) : après la scène avec le PNJ, il garde son changement d'orientation.

**Dépendances.**
- **E20.g1a** : aucune. Les 90 sites n'utilisent que les types de recherche 0x80 et 0x81. Après g1a, les tests posent `PlatformEntity` et `RidingEntity` à la main, donc rien ne bouge.
- **D5b** : aucune.
- **E20.c2a** : acquise. R3 s'appuie sur `LifePulledFromStats` et `EndOfMovePlayer` (FAIT).
- **E18** : aucune pour sm4 (elle concerne sm8, `0xBB` et `0xC2`).
- **E20.h4a** : conflit de fusion textuel possible dans `PlayerManager.cs` (`DecreaseMp` à côté de `IncreaseMp`). À sérialiser ; le second rebase l'insertion adjacente.
- **sm0, R5, R7** : prérequis dur.
- **sm1 à sm3** : aucun ordre imposé, hors témoin. Si `0xB9` est un jour porté (sm7), le témoin se repointe encore.

## 7. Manques de `check_sm34.md` §8, état pour sm4

| Manque | État |
|---|---|
| S4-1 `0xB3` (conception) | Résolu : R5 et le défaut Q-sm-3, avec l'ordre prouvé par le code et ARC-161a |
| S4-2 témoin | Résolu : `0xB9` (§3.5) |
| M7 `0x96` sans vie tirée des stats | Résolu : R3 |
| S4-4/E19 `0x97` | Reste la réponse à Q-sm-1 ; aide ou ligne, tranché (aide) ; ADR |
| S4-5 `0x88` | Résolu : table, politique Q-sm-12, 3 sites |
| M6 `0x15`, `0x66`, `0x7A` | Résolu par lecture et vecteurs ; recommandation de les sortir |
| S4-6 `0x72` | Résolu : ARC-40 (F par `ActiveCollisionEntity`), scénario U72-6, octets réels pour 140 et 444 |
| G1 calendrier | Reste : décision d'autorité |
| G2 liste sm0 | Reste : prérequis, mais comptes d'effet chiffrés |
| G3 vecteurs | Résolu |
| G4 valeurs | Résolu : 44 tests audités contre le binaire |
| G5 arcs | Résolu sur le papier, mesures de T0 en suspens |
| M3 banc | Résolu : monde piloté, `RealController` nécessaire pour 452 |
| G6 libellés | Résolu : §1, R6 |
| G7 numérotation | Résolu : Q-sm-11 à Q-sm-13 |
| M5/M14 | Résolu : politique des sans-site, pas de défaut avéré à corriger |
| M8 acceptation et recette | Résolu : §5.4, §5.5, §6 |
| M1/M2 | Restent : les citations valent sur cette branche ; citer les tests par leur nom |

Corrections de `check.md` appliquées : R1 (ne pas écrire « la décompilation perd le dernier slot »), O1 (validateur fatal, hors sites sm4), O3 (porte de gel : les `case` n'écrivent que porte ouverte, comme le binaire).

## 8. Fichiers produits

Tous sous `SM4/`.
- `vectors/` : `README.md` (format, rattachement, recette de rejeu), `MANIFEST.sha256`, `op72/88/96/97/B3.json`, `zero-site/`, `mapping.json`, `read-write-sets.json`, `counts.json`.
- `named-tests-audit.json` (44 tests audités), `tests-catalog.json` (83 entrées), `removal-matrix.json`, `mutants-matrix.json`.
- `corpus/` : `sites-context.txt` (contexte décodé des 90 sites), `sm0-rule5-counts.json`, `sm4-sites-coverage.tsv`.
- `runs/` : accords des oracles, `two-runs-identical.txt`, `selftest_unmapped_stop.txt`.
- `tools/` : copies des scripts de l'annexe (`PROVENANCE.txt` dit lesquelles sont identiques, lesquelles modifiées) et mes scripts (`temu.py`, `sm4model.py`, `gen_vectors.py`, `gen_zero_site.py`, `gen_mapping.py`, `replay_vectors.py`, `named_tests.py`, `mutants.py`, `build_catalog.py`, `finalize_manifest.py`, `corpus_counts.py`, `sites_context.py`, `sites_coverage.py`, `dump_site.py`, `mapinfo.py`, `disas.py`, `selftest_unmapped_stop.py`). Tous lancés avec `python -B` depuis des copies.

Rien n'a été écrit dans le dépôt ni dans `data-extracted/`, `alundra-project/` ou `alundra-datas-analyser/`. Aucun build, aucun test, aucun export convertisseur.