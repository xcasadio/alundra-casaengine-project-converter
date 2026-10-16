# E19.sm1 à sm4 : relecture au binaire des 21 opcodes sautés par la taille (label e19sm, 2026-10-17)

Lecture seule sur le dépôt et sur `ALUN_CD.EXE` (France). Étiquettes : FAIT (lu dans le binaire ou le code, ou mesuré par un script), INFÉRENCE, INCONNU.

Le rapport n'a pas pu être écrit en `report.md` : l'outil d'écriture refuse les fichiers de rapport, comme pour le rapport sm précédent. Le rapport est donc ce message. Scripts et désassemblages sont dans :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/e19sm/`
- `listings/op_3C.txt` … `op_B7.txt` : un désassemblage annoté par gestionnaire.
- `listings/ALL_handlers.txt`, `listings/search_8003C954.txt`, `listings/helpers.txt`.
- `oracle/oracle.py` (avec `emu.py`, `lib.py`), `oracle_binary.txt`, `oracle_decomp.txt`.
- `memo_flow.py`, `memo_conflicts.py`, `stateuse.py`, `gref.py`, `memref.py`, `findcalls.py`, `annot.py`, `listing.py`, `corpus.py`, `ctx.py`, `ctxw.py`.

## 0. Constats qui changent le plan

1. **FAIT : `0x97` n'est pas « Spend money », c'est « ajoute des PM ».**
   - Le gestionnaire `0x80040A2C` appelle `0x8004DF10`. C'est `IncreaseMp` (`PlayerManager.cs:1767-1771` de la décompilation). Il fait `SetPlayerMp(Mp + a0)` via `0x8004DEBC`. Ce n'est pas `SpendMoney` (`0x8004E004`).
   - Les 24 sites sont des salles de sauvegarde et fontaines : `96 80 32` (PV) puis `97 32` (+50 PM, plafonné à `MpMax`, 4 au plus).
   - La décompilation (`EntityEventHandlers.cs:2879-2885`) et la table (`EventOpcodeSizeTable.cs:199`) se trompent. Porter la décompilation ferait payer 50 pièces dans chaque salle de sauvegarde.
   - Preuves croisées : l'appelant `0x800335AC` est `IncreaseMpAndCreateEffect` (`a0 = 1`, `PlayerManager.cs:4749`). L'appelant `0x80034948` de `0x8004DF3C` (`DecreaseMp(1)`) est précédé de `GetPlayerMp`. `PlayerStats` : Mp à `+4`, MpMax à `+6`, argent à `+8`.
2. **FAIT : `0x3C` est inversé dans la décompilation** (`EntityEventHandlers.cs:1243-1271`).
   - Le binaire exige `Flags & 0x80` posé, `AnimFlags & 0x80` clair et `PlatformEntity == 0`. C'est le prédicat « collidable » de `EntitySupport.IsEligibleSubject` (`EntitySupport.cs:71-75`).
   - La décompilation teste l'inverse des deux bits. 0 site dans le corpus.
3. **FAIT : `0x9E`, la décompilation n'avance pas le long de x.** Elle compte w fois la première case de chaque rangée et indexe par la largeur d'exécution. Le binaire avance de 8 octets par case sur une grille de 52 colonnes (pas de rangée 416) et ne borne rien. 0 site.
4. **FAIT : les boucles de la décompilation sont exclusives.**
   - Elles s'écrivent `i < g_numberOfEntities` pour les types de groupe 2 à 11 et pour `0x3C`/`0x3D`. Le binaire est inclusif (`0..N`, N = indice du dernier slot utilisé, `0x80134360`). La décompilation perd donc le dernier slot.
   - La branche « identifiant brut » de la décompilation est inclusive, comme le binaire.
   - INFÉRENCE : la décompilation lit `g_numberOfEntities` comme ce même global.
5. **`EntitySearchService.cs` égale le binaire** pour les 12 types de groupe et pour l'identifiant brut (§3). FAIT sur ma traduction python, INFÉRENCE pour le C# lui-même. La DLL n'est donc pas touchée par le point 4.
6. **FAIT : plusieurs libellés de `EventOpcodeSizeTable.cs` sont faux ou absents.**
   - `0xB4`, `0xB5`, `0xB6` : le binaire donne `ForceZ > 0`, `< 0`, `== 0` du PREMIER résultat. La table dit « < 0 », « >= 0 », « != 0 ».
   - `0x97` : voir point 1.
   - `0x6B`/`0x6C`/`0x6D` : trompeurs, `Result = 1` quand la distance parcourue est supérieure ou égale au seuil.
   - `0x88`, `0xA9`, `0xAA` : pas de libellé.
7. **FAIT : `0x96` réutilise bien l'octet 1 comme type de recherche et comme montant.**
   - La décompilation fait pareil ; la suspicion du rapport sm est levée. L'octet 2 n'est jamais lu (taille 3).
   - Dans le corpus, `96 80 32` donne +128 PV (le `0x32` est ignoré) et `96 81 FF` donne +129 : tous deux soignent en plein.
8. **FAIT (mesure) : aucune obstruction de mémo sur le corpus.**
   - Sur les 45 programmes qui contiennent `0x6A`-`0x6D`, aucune attente qui écrit `Parameters[2..4]` ne s'intercale entre un `0x6A` et sa lecture, sur aucun chemin du graphe de contrôle.
   - 11 lectures de `0x6D` (cartes 55, 56, 57) n'ont aucun `0x6A` dans leur programme (§5).
9. **FAIT : `PlatformEntity` (`+0x28`) n'est jamais écrit par la DLL.** Dans le binaire il n'est non nul que pour un objet porté (`docs/plan-e20-heros.md:6287`). Les 10 sites de `0xB2` (type `0x8B`, cartes 258 et 298) valent donc 0 jusqu'à E20.g1a.
10. **FAIT : `0xB3` écrit les 4 mots de `g_padState1` (`0x80126F28..2E`) pendant les événements de carte, avant `MovePlayer` du même tick.**
    - Ordre du binaire : `RunMapEvents` puis `UpdateEntities` (`0x8003B388`).
    - Le seul site (carte 161, B[1] @54, `Haut` tenu) est une marche automatique.
    - La DLL a le bon point d'accroche (`AlundraGameState.TickPad`) mais `AlundraTickPad` n'a pas de setter.

## 1. Méthode et preuves

- **Tables :** `0x80098FAC + 4*op`. Les 21 adresses sont dans le §4.
- **Désassemblage :** capstone 5.0.7, MIPS32 little endian. Helpers `exelib.py` et `mipsdis.py` copiés. L'annotation d'adresses (`annot.py`) oublie un registre dès qu'il est réécrit.
- **Oracle différentiel (FAIT).**
  - Les VRAIS gestionnaires et la VRAIE recherche `0x8003C954` tournent dans l'interpréteur MIPS-I minimal d'E14 (`emu.py`/`lib.py`, copiés de `docs/plan-e14-annexe/e14a/oracle-a1/tools/`).
  - Les mondes sont aléatoires : 2 à 8 slots, avec statuts, identifiants, flags, pointeurs `+0x0C/+0x28/+0x12C/+0x130`, `ForceZ` signé, positions, cases, `PlayerStats` et grille de cases.
  - Seuls `0x8003C914` (validateur d'id dont tous les appelants jettent le résultat) et `0x8003C840` (message de débogage du type illégal) sont remplacés par des no-op.
  - Chaque modèle python est écrit d'après le désassemblage. Sont comparés : `v0`, `Result` (écrit ou non), et tout mot modifié de la zone entités, de l'état, du pavé `0x80126F18` et de `PlayerStats`.
  - **Mode binaire :** 21 opcodes x 3 000 mondes = 63 000 exécutions, 0 écart. Les 15 prédicats ont des histogrammes de `Result` à deux valeurs.
  - **Mode décompilation :** le modèle suit le texte de `decomp-unported.txt`. Il donne 4 645 écarts sur 63 000, par opcode :

| Opcode | Écarts |
|---|---|
| `0x97` | 2 762 |
| `0x96` | 414 |
| `0x88` | 325 |
| `0x3C` | 269 |
| `0x3D` | 211 |
| `0x9E` | 179 |
| `0xB7` | 72 |
| `0xB2` | 66 |
| `0xAA` | 65 |
| `0xB4` | 57 |
| `0x61` | 55 |
| `0x60` | 49 |
| `0xA9` | 49 |
| `0xB5` | 42 |
| `0xB6` | 30 |
| `0x6A`-`0x6D`, `0x72`, `0xB3` | 0 |

  - De `0x60` à `0xB7`, les écarts viennent uniquement de la borne exclusive de la recherche.
- **Limite :** `dll_search` est MA traduction python d'`EntitySearchService.cs`, pas le C# exécuté.
- **Corpus :** `docs/plan-e19-sm-annexe/skipped-sites.tsv` (510 lignes) et `alundra-project/Maps/**/events/*.events.json`. `corpus.py` décode un programme depuis son entrée avec la table des tailles.

## 2. Conventions du binaire (FAIT)

- **Appel** (`RunScript`, `0x8004205C`).
  - `h(a0 = entité logique, a1 = propriétaire, a2 = &pointeur d'instruction, a3 = état)`. L'opérande n est `lbu n(*(a2))`.
  - Le retour `v0` est la taille ajoutée au pc ; 0 termine l'appel. Après un retour non nul : `sw zero, 8(s3)` (`Parameters[1] := 0`, `0x8004232C`), puis `pc += v0` (`0x80042338-0x80042340`).
  - Les 21 gestionnaires rendent un entier strictement positif : aucun ne suspend ni ne termine l'appel.
- **État.**
  - `+4` `Parameters[0]`, `+8` `Parameters[1]` (marque d'attente), `+0xC/+0x10/+0x14` `Parameters[2..4]`, `+0x2C` `Result`, `+0x30` `_30`, `+0x34` `_34`.
  - L'état partagé par A/D/E/F est à `0x8013D9E8`.
  - `InitializeEventData` (`0x80041EE4`) n'écrit que `+0` et `+4` (`0x80041F5C-0x80041F60`). `Parameters[1..9]` survivent donc à une réinitialisation, et la DLL fait pareil (`AlundraEventProgramRunner.cs:377-419`).
- **Entités.**
  - Taille `0x294`. Le héros est à `0x80127D30` (slot 0), le slot i à `0x80127D30 + 0x294*i`.
  - N = `[0x80134360]` = indice du dernier slot utilisé. Il est écrit en fin de la passe de recyclage `0x80038634` (`0x800386CC`), à la création (`0x80039D3C`, `N = max(N, slot)`), et remis à 0 par `0x8003B2D8`.
  - Offsets utilisés :

| Offset | Champ |
|---|---|
| `+0x0C` | parent |
| `+0x10` | statut (1 Loaded, 2 Normal, 3 Deactivated, 4 FlagToDestroy) |
| `+0x14` / `+0x18` | Hp / HpMax |
| `+0x28` | PlatformEntity |
| `+0x48` | EntityRefId |
| `+0x6C` | Flags |
| `+0x88` / `+0x8C` | TargetAnimationId / TargetDirection |
| `+0xB4` | AnimFlags |
| `+0xB8` | ForceZ |
| `+0x114/118/11C` | PosX/Y/Z (16.16) |
| `+0x120/124/128` | TileX/Y/Z |
| `+0x12C` | RidingEntity |
| `+0x130` | XCollisionEntity |
| `+0x26C` / `+0x270` | LastTargetAnimationId / LastTargetDirection |

- **Buffer de résultats :** premier résultat à `0x8013D8D8`. Les gestionnaires lisent `0x8013D8D4 + 4*i`, de `i = compte` à 1.
- **Ordre d'une image.**
  - `UpdatePads` ouvre `Update`, puis `UpdateWorld` = `RunMapEvents` (`0x8003C67C`), puis `UpdateEntities` (`0x8003B388`).
  - `UpdateEntities` enchaîne : recyclage `0x80038634`, programmes `0x800386D0` (`MovePlayer` en premier), passe des compteurs `0x80038998` (efface `+0x12C`, `+0x224`, `+0x220`), coups `0x800384F4`, horloge d'animation `0x80038E18`, physique `0x80038364`, effets.
  - La physique met `+0x13C` et `+0x140` à 0, puis appelle `CheckRidingEntities` (`0x800364C8`), qui pose `+0x12C` pour les entités à `(Flags & 0x4100) == 0x100`.
  - À l'instant d'un script, `+0x12C` et `+0x130` sont donc ceux calculés par la physique du tick précédent.

## 3. La recherche `0x8003C954` (dépendance de 0x60 0x61 0x88 0x96 0xA9 0xAA 0xB2 0xB4-0xB7)

`n = Search(a0 = logique, a1 = octet de type)`. Désassemblage dans `listings/search_8003C954.txt`, table de sauts `0x80023C30` (12 entrées).

- **Bit `0x80` clair (identifiant brut).**
  - Appelle `0x8003C914` (validateur, résultat ignoré).
  - Puis, SI le statut de l'entité de référence (`lw 0x10(s3)`, `0x8003C9AC`) est 1 à 3, retient les slots 1..N dont `+0x48 == type`.
  - Le statut du candidat n'est pas testé. Le héros (slot 0) n'est pas parcouru.
- **`type & 0x7F`.**
  - 0 : la référence, sans condition.
  - 1 : le héros `0x80127D30`, sans condition.
  - 2 : slots 0..N de statut 1 à 3.
  - 3 : slots 1..N de statut 1 à 3.
  - 4 : slots 0..N avec référence de statut 1 à 3, `Flags & 0x80`, `AnimFlags & 0x80 == 0`, `+0x28 == 0`.
  - 5 : slots 1..N actifs avec `réf.+0x12C == cand`.
  - 6 : `cand+0x12C == réf`.
  - 7 : `réf.+0x130 == cand`.
  - 8 : `cand+0x130 == réf`.
  - 9 : `cand+0x0C == réf`.
  - 10 : `réf.+0x0C == cand`.
  - 11 : actifs avec `cand+0x28 != 0`.
  - 12 ou plus : message (`0x8003C840`) et 0 résultat.
- **DLL :** `EntitySearchService.cs:98-262`.
  - Sur environ 500 mondes aléatoires par type (identifiants bruts 0-15 et 0x7F, groupes 0-15), l'ensemble de résultats est identique au binaire. Le héros a `EntityRefId = -1`, comme `AlundraWorldProxy.cs:2077`.
  - Écart théorique sans effet : la branche brute de la DLL parcourt aussi le héros, le binaire non. C'est invisible tant que son `EntityRefId` reste -1. Les proxies nus des tests ont 0 par défaut (`AlundraEntitySpawnFactory.cs:493`).
- **Ordre :** `0xB4` à `0xB6` lisent le PREMIER résultat de l'ordre de parcours des slots. Les autres prédicats sont des « il existe », insensibles à l'ordre.

## 4. Les 21 opcodes

Les tailles rendues par les gestionnaires (`2 2 7 7 1 3 3 3 1 3 3 2 6 2 2 3 9 2 2 2 3`) sont toutes égales à `EventOpcodeSizeTable.cs`. Aujourd'hui `Dispatch` les envoie dans `default` (`AlundraEventProgramRunner.cs:2153`), puis `UnknownOpcode` (`:3115-3141`). L'opcode est sauté par sa taille, `Result` reste périmé, et un avertissement est loggé par opcode.

Adresses des gestionnaires :

| Op | Adresse | Op | Adresse | Op | Adresse |
|---|---|---|---|---|---|
| `0x3C` | `0x8003E558` | `0x6D` | `0x8003F990` | `0xAA` | `0x80041200` |
| `0x3D` | `0x8003E64C` | `0x72` | `0x8003FA24` | `0xB2` | `0x80041628` |
| `0x60` | `0x8003F3F8` | `0x88` | `0x8004011C` | `0xB3` | `0x80041750` |
| `0x61` | `0x8003F488` | `0x96` | `0x800409A8` | `0xB4` | `0x800417CC` |
| `0x6A` | `0x8003F8DC` | `0x97` | `0x80040A2C` | `0xB5` | `0x80041830` |
| `0x6B` | `0x8003F908` | `0x9E` | `0x80040BB4` | `0xB6` | `0x80041894` |
| `0x6C` | `0x8003F94C` | `0xA9` | `0x80041174` | `0xB7` | `0x800418F8` |

### 4.1 Tranche sm1 : `0x60 0x61 0x3C 0x3D` (187 sites, 11 cartes)

**0x60** (`0x8003F3F8`, taille 2, table `:142` « Is riding entity »)
- Pseudo-code :
  - `n = Search(logique, op1)`.
  - Pour `i = n .. 1` : si `res[i].+0x12C == logique`, alors `Result := 1` et retour 2.
  - Sinon (aussi si `n <= 0`) : `Result := 0`, retour 2.
  - Clés : appel `0x8003F418`, comparaison `0x8003F444-0x8003F44C`, écritures `0x8003F458` et `0x8003F468`.
- Sens : « une entité du groupe repose sur moi ».
- Décompilation (`Script_96_060`, `EntityEventHandlers.cs:1933-1953`) : même logique, borne de recherche exclusive (point 4).
- DLL fournie :
  - `RidingEntity` (`AlundraEntityScriptProxy.cs:344`, typé `Entity?`) porte le `LogicContextEntity` du porteur.
  - Il est recalculé une fois par image pour tout collidable `Gravity` sans `NoRiders` (`AlundraWorldProxy.cs:3652-3653`, via `EntitySupport.UpdateRidingEntities`, `EntitySupport.cs:276-290` ; flags `EntityFlags.cs:103,136`). Il est aussi recalculé en tête de chaque tick du héros (`AlundraScriptedMotion.cs:191`, `EntitySupport.cs:296-307`).
  - Recherche : `EntitySearchService`.
  - Précédents `0x3E`/`0x3F` (`AlundraEventProgramRunner.cs:1990-2040`) et leurs tests (`AlundraClassAndBranchOpcodesTests.cs:140-216`).
  - D-E20-13 documente que le recalcul n'a lieu qu'une fois par image (`AlundraWorldProxy.cs:2955-2960`).
- Manque : le `case`.
  - Piège de nullité : `ReferenceEquals(candidat.RidingEntity, logique.LogicContextEntity)` est vrai si les deux sont null (proxies nus de test). Il faut garder `logique.LogicContextEntity != null`.
  - Ne PAS réutiliser `IsRidingTheLogicEntity` (`:2691`) : il ajoute des gardes de statut et de blocage que `0x60` n'a pas.
  - INCONNU : les porteurs réels (cartes 27, 29, 30, 113, 400, 136, 159) passent-ils la grille `(Flags & 0x4100) == 0x100` ? À mesurer en T0.
- Sites réels :
  - Carte 27 C[2] @612 (`60 05 / 03`) : 24 sites dans la carte. INFÉRENCE : les entités 1 à 4 (sprite 108, programme C 130) sont des plaques et les enregistrements 5 à 8 (sprites 107/111/112/113, hauteur 4) des blocs posés dessus.
  - Carte 400 C[1] @448 : 128 sites, 8 programmes.
  - Carte 29 C[17] @813, carte 113 C[9] @993.
  - Carte 136 C[5] @1579 (`60 81` : le héros sur moi).

**0x61** (`0x8003F488`, taille 2, `:143`)
- Pseudo-code :
  - `n = Search(logique, op1)`.
  - Pour `i = n .. 1` : si `logique.+0x12C == res[i]`, alors `Result := 1` et retour 2.
  - Sinon `Result := 0`.
  - Clé : `0x8003F4D0-0x8003F4D8`.
- Sens : « je repose sur une entité du groupe ». Un `+0x12C` nul n'égale jamais un résultat.
- Décompilation identique (hors borne).
- DLL : mêmes briques. Piège : la comparaison `ReferenceEquals(logique.RidingEntity, candidat.LogicContextEntity)` doit refuser `RidingEntity == null`.
- Sites réels : 77 C[14] @1871 (`61 81`, cinq programmes : C[14], C[15], C[16], C[17], C[24]), 78 C[19] @1679, 159 B[5] @729 (`61 0D / 4B`), 398 B[1] @88 (`43 01 / 61 00 / 42 / 03`, 7 programmes B[1..7], id brut 0).

**0x3D** (`0x8003E64C`, taille 7, `:107` « If entity in zone »)
- Pseudo-code :
  - Lire `b1..b6` (octets non signés).
  - Pour `i = 0 .. N` (héros compris) : si statut 1 à 3 et `b1 <= TileX <= b2`, `b3 <= TileY <= b4`, `b5 <= TileZ <= b6` (comparaisons signées `slt`), alors `Result := 1` et retour 7.
  - Sinon `Result := 0`, retour 7.
  - Boucle : `0x8003E68C-0x8003E6F4`.
- Décompilation : même test, borne exclusive.
- DLL fournie :
  - `TileX/Y/Z` (`AlundraEntityScriptProxy.cs:341-343` ; `AlundraScriptedMotion.cs:719-720`, `TileZ = PosZ >> 20`).
  - `IsLoadedNormalOrDeactivated` (`:19`).
  - `SpawnedEntities`, héros compris (`IEntityWorldContext.cs:24`).
  - Patron `EntityInArea` (`AlundraEventProgramRunner.cs:3312-3334`).
- Manque : le `case`.
- Site réel : 226 B[3] @247 (`3D 2B 2B 30 33 0C 0C`). Le résultat est lu après un `Break` par le `04` de @255.

**0x3C** (`0x8003E558`, taille 7, `:106`)
- Pseudo-code : comme `0x3D`, avec en plus par candidat `Flags(+0x6C) & 0x80 != 0`, `AnimFlags(+0xB4) & 0x80 == 0`, `+0x28 == 0`.
- Décompilation (`Script_60_03C`) : inversée, avec une borne exclusive.
- DLL : le prédicat existe (`EntitySupport.IsEligibleSubject`).
- Sites : aucun. Test synthétique obligatoire.

### 4.2 Tranche sm2 : `0x6A 0x6B 0x6C 0x6D` (129 sites, 8 cartes, tous en créneau C)

- **0x6A** (taille 1) : `Parameters[2] := logique.PosX`, `[3] := PosY`, `[4] := PosZ`. `Result` intact, retour 1. Décompilation identique.
- **0x6B** (taille 3) :
  - `d = |Parameters[2] - PosX|` en 32 bits (`bgez`/`negu`).
  - `seuil = (b1 | b2 << 8) << 16`, entier signé. Un seuil de 0x8000 px ou plus devient négatif et donne toujours 1.
  - `Result := (d < seuil) ? 0 : 1`, retour 3.
- **0x6C** : idem avec `Parameters[3]` et `PosY`.
- **0x6D** : idem avec `Parameters[4]` et `PosZ`.
- Décompilation (`Script_106_06A` à `109_06D`) identique. Les libellés `:152-155` sont trompeurs (`Result = 1` quand la distance est supérieure ou égale au seuil).
- DLL fournie :
  - `EventProgramState.Parameters` (10 entiers), persistants entre les appels pour B/C et à travers `InitializeEventData`.
  - `PosX/Y/Z` en 16.16.
  - La convention Z de la DLL est celle du binaire moins 1 (ADR-0026). Cela est sans effet sur une différence `PosZ - mémo`.
  - Une lecture `0x6D` SANS `0x6A` (11 sites) compare `|0 - PosZ|` et ne bascule qu'à l'égalité exacte (INFÉRENCE).
- Manque : 4 `case`. Aucune source de mémo ni lecture nouvelle à créer.
- Sites réels :
  - 55 C[4] : `6A @272`, `6B 78 00 @276`.
  - 344 C[60] : `6A @2358`, `6C 40 00 @2374`, `6A @2382`, `6B 60 00 @2398` (20 `6A` dans la carte).
  - 59 C[6] : `6A @457`, `6D 20 00 @462`.
  - 60 C[1] : `6A @164`, `6B 60 00 @168`, `6A @185`, `6C 80 00 @189`.
  - 56 C[1] @97 : `6D 00 01` sans `6A`.

### 4.3 Tranche sm3 : `0xA9 0xAA 0xB2 0xB4 0xB5 0xB6 0xB7 0x9E` (42 sites, 11 cartes)

**0xA9** (`0x80041174`, taille 2)
- `Result := 1` si un `res[i]` vaut `logique.+0x130`, sinon 0. Retour 2.
- Sens : « je suis bloqué par une entité du groupe ».

**0xAA** (`0x80041200`, taille 2)
- `Result := 1` si un `res[i].+0x130 == logique`, sinon 0. Retour 2.
- Sens : « une entité du groupe est bloquée par moi ».
- Les deux sont identiques à la décompilation.
- DLL fournie :
  - `XCollisionEntity` (`AlundraEntityScriptProxy.cs:351`), typé proxy : pas de risque `null == null`.
  - Il est écrit à chaque pas par `MoveControllerAndPullPosition` (`:2411-2418`, E19.d2b B4) : c'est l'obstacle du pas, nul sans déplacement demandé. Sans contrôleur il n'est jamais écrit.
  - Il est lu pour le contact du héros (`AlundraPlayerManager.cs:1750`).
  - Les types de recherche 7 et 8 sont déjà portés.
- Sites réels :
  - `A9` : 440 B[9] @628 (`A9 18`, id brut 24, après un `2F` Carré) ; 440 C[16] @1755, C[18] @1880, C[21] @2000 (`A9 81`).
  - `AA 81` : 24 sites dans les cartes 159, 160, 421, 423 à 426 et 440, par exemple 424 C[3] @700 (`04`), 421 C[8] @1467 (`4B`), 159 C[13] @1698, 426 C[13] @1484. Le sens plausible est « pousser un bloc » (INFÉRENCE).

**0xB2** (`0x80041628`, taille 3)
- `n1 = Search(logique, op1)`. Si `n1 == 0` : `Result := 0` et retour 3.
- Sinon, copie des 65 mots du buffer (`0x8013D8D8..0x8013D9D8`) sur la pile.
- `n2 = Search(logique, op2)`. Si un `res2[j]` égale un des `n1` premiers de la copie : `Result := 1`, sinon 0. Retour 3.
- Identique à la décompilation.
- DLL : deux appels de `EntitySearchService` et une intersection par identité de proxy. Pour les vrais sites (`op2 = 0x8B`, type 11) cela dépend de `PlatformEntity`, jamais écrit (E20.g1a, `docs/plan-e20-heros.md:6321`, D-G-2).
- Sites réels : 258 B[6] @639/645/651/657/663 (`B2 03 8B` …), 298 B[6] @726 ….

**0xB4 / 0xB5 / 0xB6** (`0x800417CC` / `0x80041830` / `0x80041894`, taille 2)
- `n = Search(logique, op1)`. Si `n == 0` : `Result := 0`.
- Sinon `f = res[1].+0xB8` (premier résultat, `0x8013D8D8`).
  - `0xB4` : `Result := f > 0` (`blez`).
  - `0xB5` : `Result := f < 0` (`bgez`).
  - `0xB6` : `Result := f == 0` (`bnez`).
- Retour 2.
- Décompilation : corps identiques. Libellés de la table `:228-230` faux.
- DLL : `ForceZ` (`AlundraEntityScriptProxy.cs:325`), entier signé. Manque : 3 `case` et 3 libellés.
- Sites réels : B4 aucun ; `B5 81` 54 B[14] @1226 et @1244 (le héros tombe) ; `B5 80` 440 C[21] @2012 ; `B6 80` 160 C[6] @1013.

**0xB7** (`0x800418F8`, taille 3)
- `n = Search(logique, op1)`. `Result := 1` si un `res[i].+0x88 == op2` (32 bits contre l'octet), sinon 0. Retour 3.
- La copie de la décompilation du sous-module porte déjà la correction. Aucun site. Libellé `:231` correct.

**0x9E** (`0x80040BB4`, taille 6)
- `x1 = b1, y1 = b2, w = b3, h = b4, min = b5`.
- `compte` = nombre de cases `(y1+r, x1+c)`, avec `r < h` et `c < w`, dont le mot de 16 bits à l'adresse de la case a le bit 2.
- Adresse de case : `[0x800DD86C] + 0x604 + ((y1+r)*52 + x1+c)*8`.
- `Result := (compte >= min) ? 1 : 0`, retour 6.
- Aucune borne sur x1, y1, w, h (le `0x54` voisin borne x à 51 et y à 59).
- Les 483 cartes exportées font 52 x 60 (`AlundraMapSizeReader.cs`).
- Décompilation : n'avance pas en x, utilise la largeur d'exécution.
- DLL : `IAlundraCellMutator` n'a que des écritures (`AlundraCellStore.cs:13-40`) ; il manque un accesseur en lecture. Aucun site.

### 4.4 Tranche sm4 : `0x72 0x88 0x96 0x97 0xB3` (90 sites, 50 cartes)

**0x72** (`0x8003FA24`, taille 1)
- `logique.+0x26C := logique.+0x88` ; `logique.+0x270 := logique.+0x8C` ; retour 1. Identique à la décompilation.
- DLL : `LastTargetAnimationId` et `LastTargetDirection` (`AlundraEntityScriptProxy.cs:1212-1213`) existent. Leur restauration à la reprise d'un C (`AlundraEventProgramRunner.cs:250-254`) et leur sauvegarde au départ d'un A/D/E/F (`:275-280`) sont déjà faites (binaire `0x8004213C-0x8004218C`). Le `case` tient en deux affectations.
- Sens : « valider » l'animation et la direction posées par un programme D/F qui interrompt un C.
- Sites réels (D/F uniquement) : 40 F[2] @1153, 140 D[2] @2037, 444 D[15] @1302 et @1317, 394 F[9] @1257, 160 D[5] @1612.

**0x88** (`0x8004011C`, taille 3)
- `val = *(u32*)(0x80023D2C + 4*op2)`.
  - La table n'a que TROIS mots : `{2, 3, 4}` = Normal, Deactivated, FlagToDestroy.
  - Au-delà, ce sont des octets sans rapport, sans borne.
- `n = Search(logique, op1)`. Pour `i = n .. 1` : `res[i].+0x10 := val`. Retour 3, `Result` intact.
- Décompilation identique.
- DLL : `.Status` est un champ simple (patron des `case 0x19`/`0x5D`, `AlundraEventProgramRunner.cs:760-762, 1213-1222`). `FlagToDestroy` est recyclé par `AlundraWorldProxy.RecycleDestroyedEntities`. Manque : le `case` et la table `{2,3,4}`.
- Sites réels (créneau E) : 306 E[14] @1110 (`88 80 00`, après un `40` : retour à Normal) ; 442 E[1] @1312 (`88 80 02` : se détruire, après un effet).

**0x96** (`0x800409A8`, taille 3)
- `b = op1`. `n = Search(logique, b)`.
- Pour chaque `res[i]` : `hp = Hp(+0x14) + b`, puis `hp = (HpMax(+0x18) < hp) ? HpMax : hp` (signé), puis `Hp := hp`. Retour 3.
- L'octet 2 n'est pas lu. Il n'y a ni plancher ni test de vie (ressuscite à `Hp = 0 + b`).
- Décompilation identique.
- DLL fournie :
  - `Hp` et `HpMax` de l'entité (`AlundraEntityScriptProxy.cs:76-77`).
  - `LifePulledFromStats` (`:81`) : le héros porte sa vie, les statistiques en sont le miroir écrit en fin de `MovePlayer` (`AlundraPlayerManager.cs:610-631`).
  - Précédents `IncreaseHpMax` (`:1873`) et `RestoreLifeAndMagic` (`:1893-1910`).
- Le gestionnaire binaire n'appelle PAS `InitializeHpAndMp` : l'HUD remonte par roulement, donc ne pas appeler `SnapDisplayedLifeAndMagic`.
- Sites réels : salles 452 à 470 B[2] @204 (`96 80 32`), 475 B[2] @502 (`96 81 32`), 22 C[6] @863 (`96 81 FF`), 394 C[3] @749, 379 B[1] @156, 84 et 411 B[4] @1056.

**0x97** (`0x80040A2C`, taille 2)
- `IncreaseMp(op1)` : `SetPlayerMp(Mp + op1)` avec `Mp := (MpMax < v) ? MpMax : (v < 0 ? 0 : v)`. Retour 2.
- L'argent (`PlayerStats +8`) n'est pas touché.
- DLL : `AlundraPlayerManager.SetPlayerMp(state, short)` (`:1957`) et `PlayerStats.Mp/MpMax`. Manque : le `case` (`SetPlayerMp(state, (short)(Mp + v1))`) et le libellé `:199`.
- Sites réels : 452 à 470 B[3] @330 (`97 32`), 147 B[4] @650, 379 B[1] @159, 394 C[3] @752 (`97 0A`), 475 B[2] @505.

**0xB3** (`0x80041750`, taille 9)
- Écrit quatre mots de 16 bits :
  - `0x80126F28 := b1 | b2<<8` (ButtonsHold)
  - `0x80126F2A := b3 | b4<<8` (JustPressed)
  - `0x80126F2C := b5 | b6<<8` (Released)
  - `0x80126F2E := b7 | b8<<8` (JustPressedByInterval)
- Les compteurs `+0..+0xC` et `Result` restent intacts. Retour 9. Identique à la décompilation.
- DLL fournie :
  - `AlundraGameState.TickPad` (`AlundraGameState.cs:143`), avancé en tête de chaque tick (`AlundraWorldProxy.cs:2493`).
  - Il est lu par `0x2F` (`AlundraEventProgramRunner.cs:1879`) et par `MovePlayerOnTickPad` (`AlundraWorldProxy.cs:2893`, `AlundraPadState.FromTickPad(TickPad, LastPadState)`).
  - L'ordre « événements de carte puis bloc entités » est celui du binaire.
- Manque : `AlundraTickPad` a des setters privés (`AlundraTickPad.cs:31-49`). Il faut une couture `Overwrite(hold, pressed, released, interval)`.
  - La DLL enregistre aussi les mots du pavé par tick pour la boîte de dialogue, le choix et la sauvegarde (`_squareOfTick`, `_choicePadOfTick`, `AlundraWorldProxy.cs:2453-2454`).
  - Le binaire les lit avant le prochain `UpdatePads`, donc voit les mots de `0xB3`. INCONNU : l'effet sur le site, qui n'a pas de boîte.
  - Le stick (`LastPadState`) n'est pas touché par `0xB3`.
- Site réel : 161 B[1] @54 (`B3 00 10 00 00 00 00 00 00`). La boucle `3B` @44 / `03` / `B3` / `Break` / `Goto -20` tourne jusqu'à ce que le héros entre dans (6..13, 29, 3). Un `64` le place avant.

## 5. Mémos `Parameters[2..4]` et conflit avec les attentes

- **FAIT** (`stateuse.py` sur le binaire) : les gestionnaires qui touchent `+0xC/+0x10/+0x14` sont :

| Gestionnaire | Cases touchées |
|---|---|
| `0x0B` | P2, P3 |
| `0x1C` (et `0x1D` par appel) | P2 |
| `0x1E` (et `0x1F` par appel) | P2, P3 |
| `0x20` (et `0x21` par appel) | P2 |
| `0x22` (et `0x23` par appel) | P2 |
| `0x37` | P2 |
| `0x5F` (non porté) | P2 à P6 |
| `0x66` (non porté) | lecture |
| `0xBB` (non porté) | P2 |
| `0x6A` | écriture |
| `0x6B`, `0x6C`, `0x6D` | lecture |

  Tous les ports DLL de ces attentes utilisent les mêmes cases (`AlundraEventProgramRunner.cs:2700-3050`).
- **Par axe :**
  - X (P2) est écrasable par `0x0B 0x1C 0x1D 0x1E 0x1F 0x20-0x23 0x37`.
  - Y (P3) est écrasable par `0x0B 0x1E 0x1F`.
  - Z (P4) n'est écrasable par aucun opcode porté : `0x6D` est immunisé.
- **FAIT** (`memo_flow.py`, graphe de contrôle du corpus, deux branches de chaque saut, `0x7D-0x81` traités en impasse) :
  - 25 lectures X, 12 lectures Y et 18 lectures Z ne sont atteintes que par l'état « 0x6A passé ».
  - 11 lectures Z (cartes 55 C[20..24], 56 C[1..4], 57 C[4] et C[7]) n'ont aucun `0x6A` dans leur programme. Elles lisent un `Parameters[4]` initial (0 pour un état neuf) ou résiduel d'un programme précédent du même état, car `InitializeEventData` ne l'efface pas, ni `0x40`.
  - 0 chemin d'écrasement.
- **INCONNU :**
  - Les cartes 55, 56 et 57 (« Torla Buggy, early version ») sont-elles atteignables dans le jeu livré ?
  - Le `Parameters[4]` des 11 lectures vient-il d'un `0x6A` d'un autre programme C de la même entité ? Les `0x6A` de la carte 55 sont dans C[4..9], les lectures dans C[20..24].
- Une DLL fidèle n'a rien de plus à faire : mêmes cases, mêmes moments d'écriture. À figer par un test statique (voir sm2).

## 6. Questions ouvertes

1. **Q-sm-1** `0x97` = `IncreaseMp`. À acter par ADR, puisque la décompilation et la table disent « argent ».
2. **Q-sm-2** `0x96` : suivre le binaire à la lettre (montant = octet 1, octet 2 ignoré). Aucune différence visible tant que le `HpMax` du héros reste inférieur ou égal à 51.
3. **Q-sm-3** `0xB3` :
   - La couture dans `AlundraTickPad` suffit-elle ? Le tick suivant calculerait ses fronts depuis le `ButtonsHold` forcé, comme `UpdatePad`.
   - Faut-il aussi réécrire les enregistrements par tick lus par la boîte de dialogue ?
   - INCONNU : le comportement du stick pendant un pavé virtuel.
4. **Q-sm-4** `0xB2` : porter maintenant (Result 0 tant que rien n'est porté) ou attendre E20.g1a ? Attendre laisse 10 sites sautés.
5. **Q-sm-5** `0x60`/`0x61` :
   - Mesurer que les cavaliers réels passent `(Flags & 0x4100) == 0x100` et que les plaques sont collidables.
   - Les blocs poussables des cartes 27, 29, 30, 113 et 400 n'ont pas de tranche (O-E20-21). Les énigmes à plaques ne se résoudront donc pas en jeu, même avec sm1.
6. **Q-sm-6** `0x9E` (0 site) : lire hors grille comme le binaire (comportement indéfini) ou borner avec un journal ? Un accesseur de lecture est à créer dans `IAlundraCellMutator`. À reporter après sm4 si l'auteur ne veut pas de code sans site.
7. **Q-sm-7** `0x88` : un indice de 3 ou plus (table de 3 mots) doit-il être journalisé sans rien écrire ? Pas de site.
8. **Q-sm-8** Accord sur les corrections de libellés de `EventOpcodeSizeTable.cs` (`:142-143, 152-155, 184, 199, 217-218, 228-230`), dans la même tranche que le code.
9. **Q-sm-9** Cartes 55 à 57 atteignables dans le jeu livré ? (11 lectures `0x6D` sans `0x6A`.)

## 7. Liste de tâches proposée

Chaque tranche suit le motif d'E19.l : relecture au binaire, oracle de valeurs, tests rouges d'abord, verifier frais, une ligne de moins dans la liste fermée.

Prérequis commun : E19.sm0 (filet), puis, pour chaque tranche, un T0 qui fige en vecteurs (monde, octets, `v0`, `Result`, mots écrits) les rejeux de `oracle/oracle.py`, dans `docs/plan-e19-sm-annexe/smN/`.

**E19.sm1 (0x60 0x61 0x3D 0x3C ; 187 sites)**
- T0 :
  - Vecteurs d'oracle.
  - Mesure des cavaliers réels (cartes 27, 29, 30, 113, 400, 136, 159, 77, 78, 398, 226) : flags de sprite, plaques collidables.
- T1 (rouges) :
  - `0x60` : vrai, faux, groupe vide, référence non active (types bruts).
  - `0x61` : idem, plus `RidingEntity` nul et garde `null == null`.
  - `0x3D` : bornes incluses, héros inclus, statut 1 à 3, comparaison signée.
  - `0x3C` : Collidable posé, NoEntityCollision clair, `PlatformEntity` nul, et le contraire de la décompilation en rouge.
  - Mutants : permuter 60/61, borne exclusive, bits inversés, oubli du héros.
- T2 :
  - 4 `case` après `0x3F` (`AlundraEventProgramRunner.cs:~2011`), tailles 2/2/7/7, `Result` seul.
  - Libellés.
- T3 : arcs sur 27 C[2] (blocs 5 à 8 posés sur les plaques), 400 C[1], 226 B[3] @247, 398 B[1].
- T4 : liste fermée −187, plan, ADR du sens de `0x60`/`0x61`.
- T5 : verifier frais.

**E19.sm2 (0x6A-0x6D ; 129 sites)**
- T0 : vecteurs (seuils de 0x8000 et plus, différences négatives, enroulement 32 bits).
- T1 :
  - `0x6A` copie P2 à P4 sans toucher `Result`.
  - `0x6B`/`0x6C`/`0x6D` à l'égalité du seuil (supérieur ou égal donne 1), seuil signé.
  - Mémo qui survit à un `Break` et à une réinitialisation.
  - Écrasement par `0x37`/`0x1E`/`0x0B` (fidèle, voulu).
  - Z immunisé.
  - `0x6D` sans `0x6A` (état neuf).
  - Test statique du corpus : porter `memo_flow.py` en test (aucun chemin d'écrasement, 11 lectures sans `0x6A` listées).
- T2 : 4 `case` (tailles 1/3/3/3), libellés « Result = distance supérieure ou égale au seuil ».
- T3 : arcs 55 C[4], 344 C[60] (`6A`, `Break`, `6C`), 59 C[6], 60 C[1], 56 C[1] (cas sans `0x6A`).
- T4/T5 comme sm1 (−129).

**E19.sm3 (0xA9 0xAA 0xB2 0xB4-0xB7 0x9E ; 42 sites)**
- T0 : vecteurs ; vérifier en monde réel que `XCollisionEntity` et `ForceZ` du héros sont ceux du binaire au moment du script (440 B[9], 54 B[14], 424 C[3]).
- T1 :
  - `A9`/`AA` : proxy nul, groupe vide, héros.
  - `B2` : groupe 1 vide, intersection, type `0x8B` avec `PlatformEntity` posé à la main.
  - `B4`/`B5`/`B6` : premier résultat seulement, groupe vide, signe.
  - `B7` : octet contre 32 bits.
  - `9E` si retenue : largeur, avance en x, pas de rangée 52, seuil.
- T2 : 8 `case`, libellés `B4 > 0`, `B5 < 0`, `B6 == 0`, `A9`, `AA`, `88` ; accesseur de lecture de case pour `9E` (au plus tard).
- T3 : arcs 424 C[3] @700, 421 C[8] @1467, 440 B[9] @628, 54 B[14], 160 C[6] @1013, 258/298 B[6] (Result 0 tant que E20.g1a manque, à écrire dans le test).
- T4/T5 : −42, dont 10 qui ne se voient en jeu qu'avec E20.g1a.

**E19.sm4 (0x72 0x88 0x96 0x97 0xB3 ; 90 sites)**
- T0 : vecteurs ; sonde de l'ordre pavé/tick avec `TickPad`.
- T1 :
  - `0x72` copie sur l'entité logique.
  - `0x88` : indices 0 à 2, groupe vide, `FlagToDestroy` recyclé au tick suivant.
  - `0x96` : plafond `HpMax`, octet 2 ignoré, héros porteur de sa vie (miroir des statistiques), pas d'appel `Snap`.
  - `0x97` : plafond `MpMax`, argent inchangé.
  - `0xB3` : mots écrits ; le `0x2F` et le `MovePlayer` du même tick les voient ; le front du tick suivant est calculé depuis le mot forcé.
- T2 : 5 `case`, couture `AlundraTickPad.Overwrite`, libellé `0x97`.
- T3 : arcs :
  - Salle 452 : B[2] @204 puis B[3] @330 (PV au maximum, PM au plafond).
  - 394 C[3] @749-752 et 475 B[2].
  - 40 F[2] @1153, 140 D[2] @2037, 444 D[15] @1302 (reprise d'un C avec l'animation validée).
  - 306 E[14] @1110 et 442 E[1] @1312 (statut).
  - 161 B[1] @54 (le héros marche vers (6..13, 29, 3)).
- T4/T5 : −90.

## 8. Fichiers produits

Tous dans le dossier de travail indiqué en tête : `listings/` (21 gestionnaires, `ALL_handlers.txt`, `search_8003C954.txt`, `helpers.txt`), `oracle/` (`oracle.py`, `emu.py`, `lib.py`), `oracle_binary.txt`, `oracle_decomp.txt`, et les scripts listés en tête. `report.md` n'a pas été écrit (refus de l'outil, voir plus haut).