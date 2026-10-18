# E20.h0, h1, h3, h4 : état de la DLL et des tests au HEAD (lecture seule)

## 0. Base, méthode, légende

- Dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`, HEAD `66d86ebc` (2026-10-18). FAIT : `git diff --stat a3ccb75 HEAD -- Alundra Alundra.Tests` est vide ; depuis `a3ccb75` (le HEAD de l'annexe `docs/plan-e20-annexe/e20-g1a/state-a3ccb75/`) il n'y a que des commits de docs. Le code lu ici est donc celui que cette annexe a déjà relu pour g1a.
- Rien n'a été modifié, buildé, lancé ni exporté. Seul dossier écrit : celui-ci. Les scripts de contrôle (`verify_cites.py`) lisent le dépôt en lecture seule.
- Légende. **FAIT** : lu par moi dans le code, les tests ou les données exportées du dépôt (fichier:ligne). **FAIT-RAPPORT** : affirmé par un rapport ou une annexe sur le binaire ALUN_CD.EXE (France) ; le binaire n'a pas été relu pour cette tâche. **INFÉRENCE** : déduit (de quoi précisé). **INCONNU** : non établi.
- Statuts des citations : **VRAI** (le fait tient ; la ligne ou le fichier a changé ; « absent » = le manque annoncé est bien là) · **PÉRIMÉ** (le fait n'est plus vrai) · **FAUX** · **INFÉRENCE** · **HORS DLL** (binaire ou décompilation : non jugé ici).
- Noms de fichiers. La restructuration a retiré le préfixe `Alundra` des types de la DLL (`tools/dll-structure/table.json`) ; les fichiers de test gardent leurs noms `Alundra*`. Table de passage des fichiers cités par les cinq sources : `AlundraPlayerManager` → `Alundra/Hero/PlayerManager.cs` ; `AlundraWorldProxy` → `Alundra/Maps/WorldProxy.cs` ; `AlundraEntityScriptProxy` → `Alundra/Entities/EntityScriptProxy.cs` (+ `.HitPass.cs`) ; `AlundraEventProgramRunner` → `Alundra/Events/EventProgramRunner.cs` ; `AlundraNativeSpriteRegistry` → `Alundra/Ai/NativeSpriteRegistry.cs` ; `AlundraInventoryDirector` → `Alundra/UI/Inventory/InventoryDirector.cs` ; `AlundraCameraSway`/`AlundraCameraMath` → `Alundra/Cameras/CameraSway.cs`/`CameraMath.cs` ; `AlundraNativeEffects` → `Alundra/Rendering/Effects/NativeEffects.cs` ; `AlundraDamage`/`AlundraHitPass` → `Alundra/Combat/Damage.cs`/`HitPass.cs` ; `AlundraTestSaves` → `Alundra/SaveGames/TestSaves.cs` ; `AlundraWaveSwordAi` → `Alundra/Ai/Projectiles/WaveSwordAi.cs` ; `AlundraPlayerAnimationEffects` → `Alundra/Hero/PlayerAnimationEffects.cs` ; `AlundraWeaponTables` → `Alundra/Items/WeaponTables.cs` ; `AlundraScriptedMotion` → `Alundra/Physics/ScriptedMotion.cs` ; `AlundraMovementObstacleProbe` → `Alundra/Physics/MovementObstacleProbe.cs` ; `AlundraPlayerController` → `Alundra/Hero/HeroController.cs` ; `AlundraHudDirector` → `Alundra/UI/Hud/HudDirector.cs` ; `AlundraItemTables` → `Alundra/Items/ItemTables.cs` ; `AlundraGameState` → `Alundra/Session/GameState.cs`.
- Décalages : `PlayerManager.cs` +13 partout (13 lignes `using` ajoutées en tête) ; `EntityScriptProxy.cs` +13 ; `WorldProxy.cs` et `EventProgramRunner.cs` variables. Tous les numéros de ce document ont été relus par `verify_cites.py` (voir `check.md`).

## 1. Verdict court

1. **Rien de h0, h1, h3, h4 n'est dans la DLL.** FAIT : `TryUseItem` est un bouchon (`PlayerManager.cs:932`), le pilote est vide (`:1507`, un seul appel `:353`), l'annulation d'objet est vide (`:740`, appelée par la seule nage `:664`), il n'existe aucun état d'usage, aucun état héros 0x32/0x33/0x34, aucun `DecreaseMp`, aucune requête de cibles, aucun des gestionnaires E3, E13, E28, C63, C65, C75 (sorts) ni C62, D30, E12, C73, E21 (bombe, haricots), aucune consommation de `+0xCC/+0xD0`, aucun coup de caméra Y d'E13, aucun départ XA, aucune table sin/cos à 256 entrées. Le registre de production a 7 clés.
2. **84 citations classées** (tables §2) : 75 vraies (dont 6 « absent comme annoncé »), 5 périmées (A6 d'`attacks.md`, H7 à H9 des notes anciennes, G9 du rapport g1a, partielle), 2 inférences, 2 hors DLL, **0 fausse**. **Toutes les citations DLL de `e20-h4/report.md` §8 et de `check.md` sont encore vraies pour le fond** ; seuls les numéros de ligne et les noms de fichier ont changé (tables §2). Trois précisions utiles : (a) `alundra_18` n'existe pas dans l'export (ni en-tête ni préfab) ; (b) l'emplacement E0/E1 inline est `EventProgramRunner.cs:283-292`, pas « +13 » ; (c) `TickScriptedNpc` lit la fiche de `TargetAnimationId` (et non de `CurrentAnimationId`) pour une entité née par l'interface tant que sa première animation n'est pas validée (`ScriptedMotion.cs:173-175`) : cela touche la première image des sprites des sorts.
3. **Citations PÉRIMÉES trouvées** (aucune dans les rapports h4 ; une partielle dans g1a, G9) : `h-g-deps.md:43` (les sorties anticipées de `MovePlayer` n'existent plus : toutes atteignent `EndOfMovePlayer`), `h-g-deps.md:47` (le miroir des PV et le réceptacle de vie (`IncreaseHpMaxAndFill`) sont faits), `attacks.md:32` (la coupe d'herbe n'est pas « absente » : bouchon nommé `WaveSwordAi.cs:40`).
4. **Tests.** Deux épingles cassent dès qu'une tranche enregistre une clé de production : `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`) et `AlundraWaveSwordLifeTests.cs:182-183` (`3 + 3 + 1`). Un banc de test utilise la **bombe (sprite 2) comme entité à gravité** (`AlundraLogicalZWorldTests.cs:292` et `:398`) : h3 y enregistre C62/D30/E12 et change ce que ces tests voient (INFÉRENCE). Les épingles de la liste des préréglages (`AlundraTestSavesTests.cs:88-91`) et du « pas d'objet du Rond » (`:429-433`) bougent si une sauvegarde de test reçoit un préréglage ou un objet du Rond. Les tests S1 et S5 de la couture de tir ne bougent pas (§4). Aucun test ne presse le Rond au niveau d'un monde.
5. **Bancs.** `ArcRun` (carte 14) donne un objet (`Give`), fixe l'objet du Rond (`SetItem`), la magie (`SetMp`), l'arme (`Equip`) et presse un bouton (`Press`) : il suffit pour un test de tome, de bombe ou de soin (§5). `HitArena` coupe les gestionnaires natifs ; `ContactHost` ne peut pas faire naître d'entité.

## 2. Tables de traduction des citations

### 2.1 `docs/plan-e20-annexe/e20-h4/report.md` (§0, §2, §4, §5, §8)

| # | Citation ancienne | HEAD | Statut | Note |
|---|---|---|---|---|
| R1 | §8 `AlundraPlayerManager.cs:919` `TryUseItem`, bouchon, rend 1 | `Hero/PlayerManager.cs:932` | VRAI | `=> TryUseItemResultForTests ?? 1` ; le paramètre `pad` n'est pas lu ; seam `[ThreadStatic] internal static int? TryUseItemResultForTests` en `:936-937`. |
| R2 | §8 `:340` appel de `UpdateItemUseDriver` | `PlayerManager.cs:353` | VRAI | seul site d'appel ; juste après `PlayerAnimationEffects.Create(player, FreeMode)` (`:350`) et avant la lecture du pad (`:359-368`), la nage (`:372`) et l'aiguillage d'état (`:426`). |
| R3 | §8 `:1494` définition (vide) | `PlayerManager.cs:1507` | VRAI | corps vide. |
| R4 | §8 `:2074` `GetItemIdFromCurrentWeapon` | `PlayerManager.cs:2087` | VRAI | rend `uint` ; `NoItem` = `0xFFFFFFFF` (`:2036`) sans arme : le test « arme ≠ 7 (bâton) » du départ est donc vrai sans arme. |
| R5 | §8 `:2168` `GetNumberOfItem` | `PlayerManager.cs:2181` | VRAI | 0 si l'id est hors de `[0, 99)` (`ItemsCount` = 99, `:2033`). |
| R6 | §8 `:2204` `SetItemIdFromCurrentItemId` « rend l'ancien id et promeut comme le binaire » | `PlayerManager.cs:2217` | VRAI | relu : rend l'ancien `ItemId` si son compte ≠ 0, sinon `NoItem` ; écrit `ItemId` par `SetCurrentItemId(GetItemIdFromSlotId(...))` (`:2227-2229`). Déjà appelé à chaque tick par `FillEquipmentRecords` (`:1498`, fin de `MovePlayer`) et par le HUD (`:2318`). |
| R7 | §8 `:1957` `SetPlayerMp` | `PlayerManager.cs:1970` | VRAI | borne `[0, MpMax]`, argument `short`. |
| R8 | §8 `:1938` `SetPlayerMpMax` (plafond 4) | `PlayerManager.cs:1951` | VRAI | toute valeur ≥ 5 devient 4. |
| R9 | §8 `:2250` `UseItem` | `PlayerManager.cs:2263` | VRAI | déjà consommé par l'opcode 0x84 (`EventProgramRunner.cs:1520-1522`). |
| R10 | §8 « États 0x32/0x33/0x34 : aucun cas dans `MovePlayer`, absence silencieuse » | `PlayerManager.cs:282-625` | VRAI | aucun de ces ids dans la chaîne d'états ; ni dans `IsGroundAttackGroup` (`:1026-1027`) ni dans `IsAirAttackGroup` (`:1030-1031`). En nage (pente 4) ils tombent dans `SwimHandler.Float` par défaut (table `:176-189`) : l'état devient 0x1D puis `TryUseItem` (`:667-670`). Les fiches d'animation 50, 51, 52 du héros existent (`alundra-project/Data/sprite-records.json`, `alundra_0`, `AnimSets` : Speed 0, Acceleration 64) ; les animations 50/51/52 sont de type Once/Loop/Once (`alundra-project/Entities/Alundra/bankalundra_0_anim50_down.anim2d`, `…anim51…`, `…anim52…`). |
| R11 | §8 `AlundraWorldProxy.cs:3448` `SpawnEntityBySpriteIndex(parent, false, idx, x, y, z, direction)` « direction, Z + 1, boucle de naissance, fiche d'équilibrage, butin » | `Maps/WorldProxy.cs:3488` | VRAI | Z + 1 en `:3549` ; `InitializeBalance` `:3554` ; `RollLootAtCreation` `:3566` ; `JoinsBirthTick` et `HasLogicalZ` en `:3572-3573`. Exige un hôte `IEntityWorldContext` : le défaut de l'interface rend `null` (`Maps/IEntityWorldContext.cs:81`). |
| R12 | §8 « préfabs `alundra_16..24` exportés avec les en-têtes du binaire » | `alundra-project/Data/sprite-records.json` | VRAI sauf 18 | en-têtes relus : 16 (E3), 17 (E13), 19 (A254 C75 E0), 20 (E1), 21 (E28), 22 (E1), 23 (A254 C63 E3), 24 (A254 C65 E1). **`alundra_18` est absent** de l'export (ni en-tête ni préfab) : cohérent avec « le sprite 18 n'est jamais créé » (§0.2). Préfabs présents pour 1, 2, 16, 17, 19 à 24 (`AssetInfos.json`). |
| R13 | §8 `:3267` `DestroyEntity(e, fx)` | `WorldProxy.cs:3301` | VRAI | trois bouchons nommés dedans : butin (`:3303`), effet actif (`:3306`), porteur (`:3315`). |
| R14 | §8 `:3295` variante sans effet | `WorldProxy.cs:3329` | VRAI | un argument, ni bris ni butin ; exposée par `IEntityWorldContext.DestroyEntity` (`:3604`). |
| R15 | §8 `AlundraEventProgramRunner.cs:378` E0/E1 inline | `Events/EventProgramRunner.cs:283-292`, prédicat `:328` | VRAI | `IsInlineDeactivateCode` = codes 0, 1 et 10 (E10 par E20.f3c) ; appelle `DestroyEntity(entity, -1)`. |
| R16 | §8 `AlundraNativeSpriteRegistry.cs:29` registre de production (Beannoïde, Muruta, C61 seulement) | `Ai/NativeSpriteRegistry.cs:36-43` | VRAI | 7 clés (§3.6). |
| R17 | §8 statut 3 « seulement E » `AlundraEntityScriptProxy.cs:1709-1745` | `Entities/EntityScriptProxy.cs:1717-1726` (Normal → Deactivated) et `:1758-1760` (`case Deactivated` → programme E) | VRAI | |
| R18 | §8 `AlundraDamage.cs:27` règle de dégâts ; passe de coups portée | `Combat/Damage.cs:24` (`ResolveBalanceTarget`), `:79` (`ComputeNewHp`) ; `Combat/HitPass.cs` | VRAI | la passe pose `TouchingEntity` ; **les dégâts sont appliqués par le programme D de la cible** (doc de `HitPass.cs`). Seules deux cibles ont un D natif : (D,1) type 335 et (D,9) type 347. Un sort ne blesse donc aujourd'hui que ces deux types (INFÉRENCE ; le rapport g1a §1.9 dit la même chose). |
| R19 | §8 `AlundraCameraSway.cs` (9 champs, `Step`) | `Cameras/CameraSway.cs:33-41` (9 propriétés `internal set`), `Step` `:65` | VRAI | `Start` `:45`, `Stop`, `OnMapLoad` ; aucune capture ni restauration des 9 champs. |
| R20 | §8 `AlundraNativeEffects.cs:28` motif d'effets (anneaux, étincelles) | `Rendering/Effects/NativeEffects.cs:28-31` | VRAI | `CreateLifeVesselEffects` fait **8 anneaux puis 4 étincelles** de l'effet 14 dans une seule fonction ; les deux soins de h4 (`0x80032F84` : 4 étincelles ; `0x80033274` : 8 anneaux) en veulent un morceau chacun (INFÉRENCE d'après §5 du rapport). Seul appelant : `EventProgramRunner.cs:3154`. |
| R21 | §2 « sauvegardes d'essai : `Mp 4 / MpMax 4`, tomes niveau 1 (43, 45, 47, 49), HP 4/10 » `AlundraTestSaves.cs:67-72, 96` | `SaveGames/TestSaves.cs:72-75` (`TestKitItems`), `:94-98` (`WeaponPreset`) | VRAI | §5. |
| R22 | §8 « À ajouter » : état d'usage, front du Rond, garde de verrou ≠ 0, pilote, annulation et ses sites, garde du menu `AlundraInventoryDirector.cs:406` | §3 | VRAI (tout absent) | garde du menu : `UI/Inventory/InventoryDirector.cs:419-422` (commentaire « NO PORT EQUIVALENT ») dans `TryTrigger` (`:405`). |
| R23 | §8 `DecreaseMp` : aucun équivalent | — | VRAI | `SetPlayerMp(state, (short)(Mp - 1))` (`:1970`) donne la borne ; il n'y a pas non plus de `GetPlayerMp` (lecture directe de `state.PlayerStats.Mp`). |
| R24 | §8 états héros 0x32/0x33/0x34 et « départ XA en bouchon journalisé » | — | VRAI (absent) | un opcode sans `case` donne `UnknownSkipped` (`EventProgramRunner.cs:3081`) ; `0xB9` n'est que dimensionné (`Events/EventOpcodeSizeTable.cs:233`) ; aucun support CD/XA dans `Alundra/Audio/`. |
| R25 | §8 « Les 8 pilotes et leurs tables ; les 6 gestionnaires natifs et leur enregistrement » | — | VRAI (absent) | |
| R26 | §8 « La requête avec son tri exact » | — | VRAI (absent) | `Entities/EntitySearchService.cs:75` (`GetMatchingEntitiesBySearchType`) est un autre algorithme ; `Ai/EnemyHelpers.cs` n'a pas de requête de cibles. |
| R27 | §0.4 et §8 `PreviousAdjustedForceX/Y` (`AlundraEntityScriptProxy.cs:328-329`) sans consommateur ; poussée « consommée par le moteur de déplacement, bornée à la carte, avec `ForceAdjusted` » | `Entities/EntityScriptProxy.cs:341-342` | VRAI | consommateurs : aucun (seuls `Clone` `:2638-2639` et les tables d'état des tests d'oracle `AlundraBeannoideOracleTests.cs:93-94`, `AlundraMurutaOracleTests.cs:92-93` les posent). La passe de forces `RunOneKinematicTick` (`Physics/ScriptedMotion.cs:614`, forces `:668-674`) ne les lit pas. Point d'insertion naturel : entre `:674` et le flux du porteur `:676` / la borne de carte `:683-691`. `ClampStepToMapEdge` (`:741`) existe, pour les entités `HasLogicalZ` seulement ; or `SpawnEntityBySpriteIndex` marque `HasLogicalZ` (`WorldProxy.cs:3573`) : les sprites des sorts reçoivent déjà la borne (INFÉRENCE : à tester). Le commentaire `ScriptedMotion.cs:609` range ces champs sous « riding-platform force feed » : approximatif, le flux de plateforme de la DLL passe par `LastTickDeltaX/Y` (`:676-680`). |
| R28 | §8 accès 32 bits à `+0x274` : `Bytes` est un `byte[4]` (`:1214`) | `Entities/EntityScriptProxy.cs:1227` | VRAI | voisins : `DelayOrAngleOrEntityId` int `+0x278` (`:1228`), `ItemState` int `+0x27C` (`:1229`), `AIValues` `short[10]` `+0x280..+0x293` (`:1230`) = exactement les 5 mots (+0x280 à +0x290) de l'eau, en deux shorts chacun. Précédent de composition de 4 octets en un mot : `Ai/Objects/SaveBook.cs:164-171`. Utilisateurs de `Bytes`/`AIValues` : Beannoïde, Muruta, `EnemyHelpers.cs:221,234`, livre de sauvegarde. |
| R29 | §8 `ResyncControllerFromFlags` (`:657`) quand C75 modifie les drapeaux | `Entities/EntityScriptProxy.cs:670` | VRAI | `internal`, appelée par le runner (`EventProgramRunner.cs:671-698`, `:2478`, `:2500`). Les drapeaux de C75, `\|= 0x2130`, sont dans `EntityFlags` : 0x10 `DeactivateOnImpact`, 0x20 `DeactivateOnHit`, 0x100 `Gravity`, 0x2000 `NoObstacleSlide` (`Entities/EntityFlags.cs:69,75,103,130`). Attention : le rapport glose 0x2000 par « glisse » ; la DLL le documente comme « désactive la glissade le long de l'obstacle » (INFÉRENCE : même bit, sens à vérifier par h4). |
| R30 | §8 coup de caméra Y d'un tick (`[0x800DD874]`) dans `AlundraCameraMath.cs` | `Cameras/CameraMath.cs:216-228` | VRAI (absent) | `ComputeSmoothedCameraTarget(..., swayOffsetX, swayOffsetY)` ajoute `new Vector3(swayOffsetX, -swayOffsetY, 0f)` (`:223`) ; aucune occurrence de `DD874` ni de coup d'un tick dans la DLL. |
| R31 | §8 « effets 8, 14, 18, 19, 26 via le réservoir (existent côté banque) » | `alundra-project/Data/effects/effects-global.json` | VRAI | 29 effets ; les indices 8, 14, 18, 26 existent ; l'effet 19 a 3 animations (la variante 2 existe). Création : `EffectReservoir.Create(false, effet, animation, x, y, z)` (motif `NativeEffects.cs:39`). Sons des effets : INCONNU (comme Q5). |
| R32 | §8 « Les sauvegardes et le HUD n'ont rien à ajouter » | `SaveGames/SaveGame.cs:182-186,224-228` ; `UI/Hud/HudDirector.cs:612-613` | VRAI | `Mp`, `MpMax`, `ItemId` sont sauvés ; le HUD lit `PlayerStats.Mp`. L'état d'usage n'est pas sauvé (le rapport dit qu'il est remis à 0 à l'initialisation du héros, 0x8003295C). |
| R33 | §0.7, Q8 : E3/E13 partagés avec des ennemis, clé commune avec E14 | `Ai/NativeSpriteRegistry.cs:54-67` | VRAI | une clé = un enregistrement (le doublon lève `InvalidOperationException`, `:62`), mais l'enregistrement est restreint à une liste de types ; aucune clé (E,3), (E,13), (E,28), (C,63), (C,65), (C,75) aujourd'hui. Types de page : `0x100 + indice` pour la page de carte, l'indice seul pour la page globale (`Alundra.Tests/AlundraHitArena.cs:82` : `0x100 + 79`). |
| R34 | §0.9 calendrier : pilote appelé avant l'aiguillage d'état | `PlayerManager.cs:353` | VRAI | le premier appel suit donc le départ d'un tick (le départ est dans l'aiguillage, `:432`). |
| R35 | §1 « Reset : l'initialisation du héros remet `[0x80127164]` à 0 » | `Maps/WorldProxy.cs:2075` (`AdoptPlayerPawn`), `:1993-2034` (ResetEntityState) | INFÉRENCE | où vivra l'état : deux précédents de globales du binaire. (1) Sur le proxy du héros (`EntityScriptProxy.cs:434-441` : pointeur d'arme, compteur, minuterie de charge, « Hero only »), reconstruit à chaque carte par `AdoptPlayerPawn` : la remise à zéro à l'initialisation du héros vient alors d'elle-même. (2) Dans `GameState` (`RegenerationCounters`, `GameState.cs:124`, doc `:120-123` : « nothing resets at a map load ») : ces compteurs ne sont dans aucune des trois méthodes de remise à zéro (`InstallForMapEntry` `:368`, `ResetSessionForLoad` `:411`, `ResetForTests` `:438`) ; un état d'usage placé là demanderait des remises à zéro explicites à l'adoption du héros et dans `ResetForTests`. |
| R36 | §0.6 menu refusé pendant l'objet | `InventoryDirector.cs:405-427` | VRAI | `TryTrigger(GameState, EntityScriptProxy?)` reçoit déjà l'état et le héros : aucune signature à changer, quel que soit l'endroit où vit l'état d'usage (INFÉRENCE). Le déclencheur est appelé à la fin de chaque tick (`WorldProxy.cs:2591`). |
| R37 | §7 D1-D12, §3-§5 comportements, vecteurs, tables | — | HORS DLL | non jugés ici. |

### 2.2 `docs/plan-e20-annexe/e20-h4/check.md`

| # | Citation | HEAD | Statut | Note |
|---|---|---|---|---|
| C1 | « `HighestLiveSlot()` est privé (`AlundraWorldProxy.cs:153`) ; `RecycleDestroyedEntities` (`:3220`) ne le calcule pas ; la DLL n'a pas l'équivalent de `[0x80134360]` » | `WorldProxy.cs:182` (privé) ; `:3265` | VRAI | `HighestLiveSlot` ne sert qu'à la sonde `ReportSlot` (`:257-264`). `EntitySlotTable` (`Entities/EntitySlotTable.cs`, 64 cases) n'a que `HeldCount`, `IsHeld`, `Allocate`, `Release`, `HoldHeroSlot`. La boucle de la requête (`1..count-1`) et celles des autres passes (`1..count` incluses) n'ont donc pas de support. |
| C2 | « Tables sin/cos à 256 entrées (`0x800204F0`, `0x800206F0`) n'existent pas dans la DLL (grep vide) » | — | VRAI | `rg` sur `800204F0|800206F0|sin256|SinTable|CosTable` : 0 résultat. Les tables à 32 directions `0x80023654/0x80023694` existent : `Entities/AnimationTables.cs:71` (`OffsetXList`), `:82` (`OffsetYList`) ; l'anneau de `NativeEffects.cs` en utilise les entrées 0, 4, 8. |
| C3 | « Accès 32 bits sur `AIValues[0..9]`, `short[10]` (`:1217`) » | `EntityScriptProxy.cs:1230` | VRAI | voir R28. |
| C4 | « Feu 2 : tampon `0x80127138` jamais sauvegardé, restauré sans garde » | `Cameras/CameraSway.cs` | VRAI (absent) | INFÉRENCE : il faudra un tampon de session de 9 mots initialisé à 0. |
| C5 | « Pilote non appelé si `hero+0x20 != 0` (`AlundraPlayerManager.cs:305`) ni si `[0x800DC4B8] & 0x48` » | `PlayerManager.cs:321-328` ; `WorldProxy.cs:2905-2917` | VRAI | le retour de `BlockedByEntity` précède `:353`. Porte fermée : `RunEntityBlock(tick, gateClosed)` ne fait que les listes, `MovePlayer` n'est pas appelé. Masque : `GameplayBlockedMask = MenuOpen \| Unused40` = 0x48 (`Session/GameState.cs:99`). |
| C6 | « Après la pose, le héros est libre (~200 à 250 ticks) avec l'objet toujours en cours » | — | INFÉRENCE | rien dans la DLL ne le restreindrait ; ce sont les gardes à écrire (menu, Rond, nouveau tome). |
| C7 | « Axe Y inversé en espace de rendu : `ComputeSmoothedCameraTarget` ajoute `-swayOffsetY` » | `CameraMath.cs:223` | VRAI | la conclusion « secousse −2 » reste une inférence. |
| C8 | « Pente 4 = eau profonde (`DeepWaterSlope`) » | `PlayerManager.cs:131`, branche `:372` | VRAI | le pilote est appelé (`:353`) avant la nage (`:372`) : comme dans le binaire, il voit la pente 4 avant que la nage ne remplace l'état. |
| C9 | « `MovePlayer` appelé une fois par tick logique ; ordre des passes de `0x8003B388` » | `WorldProxy.cs:2905-2988` | VRAI | `movePlayer`, `programs`, `pending`, `counters`, `lists`, `clock`, `physics`, `activeEffects`, `hit` ; épinglé par `AlundraTickStructureTests.cs:169,239`. |
| C10 | « sauvegardes d'essai (`AlundraTestSaves.cs:67-72, 96`) » | `TestSaves.cs:72-75, 94-98` | VRAI | |
| C11 | « E0/E1 inline (`AlundraEventProgramRunner.cs:378`) » | `EventProgramRunner.cs:283-292` | VRAI | voir R15. |
| C12 | « Sprite 154 : C65, E1, `MoreFlags 0x44`, sans fiche d'attaque » | `sprite-records.json` (`alundra_154`) | VRAI | en-tête relu : A0 C65 D0 E1, `MoreFlags` 68, boîte (0,0,0,0,0,0), préfab `Entities/bank_alundra_154` présent. Qu'aucune carte ne le place : INCONNU (non relu ici). |

### 2.3 `docs/plan-e20-annexe/e20-g1a/report.md` (parties h0, h1, h3) et son annexe `state-a3ccb75`

| # | Citation | HEAD | Statut | Note |
|---|---|---|---|---|
| G1 | §3.7 « appel mode 0 `AlundraPlayerManager.cs:395` ; verrou `:317-335` ; garde d'échelle `:386` ; états d'échelle `:444` » | `PlayerManager.cs:408` ; `:321-345` (BlockedByEntity `:321-328`, verrouillée `:335-345`) ; `:394-402` ; `:456-459` | VRAI | |
| G2 | §4 bouchons : `:919`, `:927`, `:1271-1274` | `PlayerManager.cs:932`, `:940`, `:1285-1287` | VRAI | appel de `UpdatePlayerCarriedEntity` mode 0 `:408`, mode 2 (nage) `:659`. |
| G3 | §4 « branche verrouillée : n'appelle ni le mode 1 ni l'annulation d'objet » | `PlayerManager.cs:335-345` | VRAI | contenu : `Create(Locked)` `:337`, deux remises à zéro `:341-342`, `EndOfMovePlayer` `:343`. |
| G4 | §4 sites de `PlayerTryAction` `:419, :438, :743, :787, :848`, absent des états d'échelle `:444` | `:432, :451, :756, :800, :861` ; `:457-458` | VRAI | `:800` (course 0x03) n'appelle pas `TryUseItem`. |
| G5 | §4 « aucun cas pour 0x05, 0x06, 0x07, 0x0B, 0x0C, 0x2E, 0x2F, 0x30 ; 0x09 et 0x0A dans les groupes d'attaque » | `PlayerManager.cs:1026-1031` | VRAI | |
| G6 | §4 champs `PlatformEntity`, `CarriedEntity`, `RelativeWarpOffset*` (`:88-92`), clonés (`:2596-2600`), `CarriedEntity` lu seulement en `AlundraPlayerManager.cs:386` | `EntityScriptProxy.cs:101-105`, `:2609-2613` ; `PlayerManager.cs:399` | VRAI | `CarriedEntity` n'est jamais écrit : « tome refusé si on porte » (`hero+0x2C == 0`) est toujours vrai aujourd'hui. |
| G7 | §4 « aucune des deux surcharges de `DestroyEntity` ne touche au porteur ; `RecycleDestroyedEntities` `:3231` » | `WorldProxy.cs:3301-3318`, `:3329-3334` ; `:3265` | VRAI | |
| G8 | §4 registre : aucune clé C23, C4, C70, C73, C62, E2, E12, E20, E21 ; « compte épinglé à 7 (`AlundraNativeSpriteRegistryTests.cs:51`) » | `NativeSpriteRegistry.cs:36-43` ; `AlundraNativeSpriteRegistryTests.cs:51,55` | VRAI | |
| G9 | §4 « `TickScriptedNpc` (`:165-170`) lit la fiche de `CurrentAnimationId`, pas de `TargetAnimationId` » | `Physics/ScriptedMotion.cs:171-176` | PÉRIMÉ en partie | depuis E20.e0a3b : une entité née par l'interface (`JoinsBirthTick`) dont `CurrentAnimationId` vaut encore `~0` lit la fiche de `TargetAnimationId` (`:173-175`) ; sinon `CurrentAnimationId`. |
| G10 | §4 plateforme horizontale `ScriptedMotion.cs:671-675` | `ScriptedMotion.cs:676-680` | VRAI | |
| G11 | §4 « obstacle : `MovementObstacleProbe.cs:48-49` passe `PickupKindMask` » | `Physics/MovementObstacleProbe.cs:51` | VRAI | |
| G12 | §4 autres : `HasLogicalZ :1370`, `SpawnDirections :1269`, `GetNumberOfItem :2168` | `EntityScriptProxy.cs:1383` ; `PlayerManager.cs:1282` ; `:2181` | VRAI | |
| G13 | §6 tests : registre `:51` (7 → 8 → 9) ; `TheNinePresets_AreListed… :85-87` | `AlundraNativeSpriteRegistryTests.cs:51-55` ; `AlundraTestSavesTests.cs:88-91` | VRAI | |
| G14 | §0.4 « aucun chemin de production ne met un objet dans les mains avant g3 et g1b ; de même avant h3 pour la bombe et les haricots » | — | VRAI | `PutInHands`, `TryLift`, `ThrowCarried` : 0 occurrence dans `Alundra/`. |
| G15 | §1.2 à §1.9 comportements binaires (TryLift, UpdatePlayerCarriedEntity, tables de libération, sites 0x80034B10/0x80034B98, C62/C73) | — | HORS DLL | |

### 2.4 `docs/plan-e20-annexe/e20-themes/attacks.md`

| # | Citation | HEAD | Statut | Note |
|---|---|---|---|---|
| A1 | l.7 « seuls 27, 28, 29 câblés au tir (`AlundraPlayerManager.cs:1213`) » | `PlayerManager.cs:1226` (ensemble), `:1216` (`Contains`) | VRAI | |
| A2 | l.8 `TryUseItem`, `PlayerTryAction` bouchons (`:919`, `:927`) | `:932`, `:940` | VRAI | |
| A3 | l.26 bombe : « DLL absent ; `AlundraTestSaves.cs:66` la donne, sans test de tir » | `TestSaves.cs:72-75` (`(35, 1)`) | VRAI | aucun test ne tire la bombe (hors `AlundraTestSavesTests.cs`, qui compte la bombe dans le kit, `rg -i bomb` ne donne que `AlundraLogicalZWorldTests.cs`). |
| A4 | l.27 tomes : « DLL absent : `TryUseItem` stub ; tests absents » | — | VRAI | |
| A5 | l.31 appel `SpawnEntityBySpriteIndex` `AlundraPlayerManager.cs:1205` ; registre `:28-34` | `PlayerManager.cs:1218` ; `NativeSpriteRegistry.cs:36-43` | VRAI | |
| A6 | l.32 « aucune occurrence de `CheckAndTriggerTileEffect` dans `Alundra/Scripts` » | `Ai/Projectiles/WaveSwordAi.cs:34,40` | PÉRIMÉ | bouchon nommé, `internal static void CheckAndTriggerTileEffect(EntityScriptProxy)` (`:40`), appelé par `Tick` (`:34`) ; la section 1.27 du plan crée `Alundra/Cells/TileEffect.cs` et fait appeler le port par ce bouchon et celui de `PlayerAnimationEffects` ; « la bombe (E12) n'aura que l'appel à ajouter ». |
| A7 | l.30 passe de coups, dégâts (`AlundraHitPass.cs:8,:166`, `AlundraDamage.cs:21,:76`, `AlundraWorldProxy.cs:2952,:3062`) | `Combat/HitPass.cs`, `Combat/Damage.cs:24,79`, `WorldProxy.cs:3083,3093` (étincelles, effet attaché) | VRAI | |
| A8 | l.13-20 (`PlayerTryAttack :948`, `UpdateWeaponStepProgression :1160`, `UpdatePlayerWeaponEffect :1041`, aura `:1024-1136`) | `PlayerManager.cs:961`, `:1173`, `:1054`, `:1125-1161` | VRAI | f-slices : hors h. |

### 2.5 Notes anciennes (`e20-armes/h-g-deps.md` §2.3, `e20-0-rapports/D4-D5-equipement-0x8003634C.md`, `decouverte-2026-10-09/`)

| # | Citation | HEAD | Statut | Note |
|---|---|---|---|---|
| H1 | `h-g-deps.md:116` « `TryUseItem` rend 1 ; le Rond est lié à `UseItem` (`AlundraPlayerController.cs:109`) mais rien ne le lit ; le son 3 d'échec ne joue jamais » | `PlayerManager.cs:932` ; `Hero/HeroController.cs:65` | VRAI | action `UseItem` → `PadState.Circle` (`Hero/PadState.cs:29`), exportée dans `alundra-project/Data/Alundra.buttonsMapping:217`. Les seules lectures du Rond : le maintien dans `SprintTick` (`PlayerManager.cs:840`, plaquage). Aucune lecture du front. |
| H2 | `h-g-deps.md:117` `UpdateItemUseDriver` vide, appelé en `:230` = 2e site `0x80031CC4` ; pas de site de blessure | `PlayerManager.cs:353`, `:1507` | VRAI | |
| H3 | `h-g-deps.md:118` setters et lecteurs d'objets ; « pas de `DecreaseMp` » | voir R4-R9, R23 | VRAI | |
| H4 | `h-g-deps.md:119` `CreateLifeVesselEffects` (`:28`) | `NativeEffects.cs:31` | VRAI | R20. |
| H5 | `h-g-deps.md:120` « `AlundraItemTables` : colonne 3 et catégorie ; le HUD lit `PlayerStats` (`AlundraHudDirector.cs:544-586`) » | `Items/ItemTables.cs` ; `UI/Hud/HudDirector.cs:566-567,612-613` | VRAI | |
| H6 | `h-g-deps.md:121` « États 0x05, 0x0B, 0x20-0x24, 0x32-0x34 sans cas ; le héros n'a pas de sprites de sorts » | — | VRAI | `AlundraPlayerManagerTests.cs:100-114` utilise 0x20 comme exemple d'état non porté. |
| H7 | `h-g-deps.md:27` « `RunSpriteEvent` ne tourne que le livre et E0/E1 (`AlundraEventProgramRunner.cs:293-330`) » | `EventProgramRunner.cs:264-324` | PÉRIMÉ | il consulte aussi le registre natif (`:294-312`, E14.a0). |
| H8 | `h-g-deps.md:43` « la DLL retourne tôt ici (`AlundraPlayerManager.cs:220-223`) » | `PlayerManager.cs:321-345` | PÉRIMÉ | les deux sorties anticipées atteignent `EndOfMovePlayer` (E20.c2a T1, `:326`, `:343`) et effacent la charge et le pointeur d'arme (E20.f1a). |
| H9 | `h-g-deps.md:47` « PV 10/10 sur l'entité, pas de miroir ; le réceptacle de vie écrit `PlayerStats` (`:1004`, « provisional seam ») » | `PlayerManager.cs:634-645` ; `:1875-1887` | PÉRIMÉ | miroir des PV en fin de `MovePlayer` ; `IncreaseHpMaxAndFill` écrit l'entité du héros qui porte sa vie. Les soins de h1 écriront donc `hero.Hp`, comme le dit le plan. |
| H10 | `D4-D5` « appeler le pilote au site de blessure (`0x80031C6C`) et au 2e site (`0x80031CC4`) ; un bouchon est fidèle jusqu'à E20.h » | `PlayerManager.cs:353` | VRAI | site de blessure absent (§3.4). |
| H11 | `decouverte-2026-10-09/4-dll-state.md:100` « Rond : aucun consommateur » | — | VRAI | |
| H12 | `decouverte-2026-10-09/5-chain-needs.md:45` : la bombe de la 362 naît par le script d'Olen (C62 `0x8007A978`, D30, E12) | `sprite-records.json` alundra_2 : C62 D30 E12 | VRAI | elle naît aujourd'hui sans gestionnaire (no-op compté) : enregistrer C62/D30/E12 change le comportement de la carte 362 dans le jeu (INFÉRENCE ; aucun test ne joue la 362 : `AlundraEntityContactArcTests.cs:54-55` joue la 185 jusqu'au départ vers la 362, doc `:50-51`). |

## 3. Inventaire de ce qui existe

### 3.1 Le front du Rond

- FAIT : `PadState.Circle = 0x0020` (`Hero/PadState.cs:29`) ; `ButtonsJustPressed` (`:45`), `ButtonsHold` (`:40`), `ButtonsReleased` (`:49`) ; le pad d'un tick est `PadState.FromTickPad(TickPad…)` (front du tick, `Hero/TickPad.cs:43`). Le binaire lit le mot « pressé » `[0x80126F2A] & 0x20` (FAIT-RAPPORT) : c'est `pad.ButtonsJustPressed & PadState.Circle`. Pour C75 le maintien `[0x80126F28] & 0x20` est `ButtonsHold` ; un gestionnaire natif reçoit `GameState` (`NativeSpriteHandler(entity, context, state)`, `Ai/NativeSpriteRegistry.cs:20`) et donc `GameState.TickPad` (avancé en tête de tick, `Maps/WorldProxy.cs:2527`).
- FAIT : l'entrée vient de l'action `UseItem` (`Hero/HeroController.cs:65`, `alundra-project/Data/Alundra.buttonsMapping:217`).
- FAIT : aucun code ne lit le front du Rond. `TryUseItem(EntityScriptProxy player, in PadState pad)` ignore `pad` (`:932`). La seule lecture du Rond est le maintien de `SprintTick` (`:840`).
- FAIT : le son d'échec 3 a des précédents côté hôte : `player.ScriptHost?.SoundPlayer?.PlaySfx(NoAttackSoundId)` avec `NoAttackSoundId = 3` (`PlayerManager.cs:946,982`) et `_soundPlayer?.PlaySfx(3)` dans le menu (`InventoryDirector.cs:895,959,975`).
- FAIT : forme de l'appel. `TryUseItem` n'a ni `GameState` ni hôte en paramètre ; `MovePlayer(player, in pad, state, host)` les a, mais `PrepareSprintTick`, `StopSprintTick`, `BounceTick` et les gestionnaires de nage n'ont que `player` et `pad` (`SwimTick` reçoit `state` et `host`). Le héros porte `player.ScriptHost` (hôte) ; en production `state == host.GameState` (`EntityScriptProxy.cs:1666`). Dans les tests à hôte nul (`MovePlayer(hero, in pad, new GameState(), host: null)`) `player.ScriptHost` est nul : `TryUseItem` devra rendre 1 sans exception sans hôte ni tables (INFÉRENCE ; voir §4.3).

### 3.2 L'état d'usage (objet en cours, compteur, phase)

- FAIT : aucun des trois n'existe (`rg` sur `80127164|8012715C|80127160|ItemInUse|UseItemState` : rien). Ce qui existe est autre chose : l'**objet du Rond** (`PlayerStats.ItemId`, `Session/PlayerStats.cs`, `+0xC`), sauvé (`SaveGame.cs:186`, `:228`).
- FAIT : deux précédents de globales du binaire : sur le proxy du héros (`EntityScriptProxy.cs:434-441` : `WeaponRecordPointer`, `WeaponStepCounter`, `WeaponChargeTimer`, « Hero only ») ; dans `GameState` (`RegenerationCounters`, `:124`, qui n'est remis à zéro par aucune des trois méthodes de remise à zéro de la classe, `:368`, `:411`, `:438`). Le proxy du héros est reconstruit à chaque carte (`AdoptPlayerPawn`, `WorldProxy.cs:2075`).
- INFÉRENCE : `hero+0x2C == 0` du départ = `player.CarriedEntity == null` (toujours vrai, G6) ; `+0x18C == 4` = `player.Slope_18c == DeepWaterSlope` (`:131`) ; `hero+0x88 < 2` = `TargetAnimationId` Idle ou Moving.

### 3.3 Les 13 sites d'appel de `TryUseItem` dans la DLL

FAIT. `PlayerManager.cs` : `:432` (Idle/Moving, `!= 0 &&` `PlayerTryAction`), `:451` (sauts, idem), `:575` (sorties de nage, résultat ignoré), `:607` (groupe d'attaque au sol, ignoré), `:617` (groupe d'attaque en l'air, ignoré), `:669`, `:675`, `:679` (gestionnaires de nage Float, FlipThenFloat, ItemOnly, ignorés), `:700` (`SwimSteerTick` : 0 termine le tick), `:726` (`SwimHurtEndTick` : 0 retourne la direction), `:756` (`PrepareSprintTick`), `:861` (`StopSprintTick`), `:915` (`BounceTick`). Non appelé : `SprintTick` (`:798`, comme le binaire), états d'échelle (`:457-458`, commentaire « NOT PORTED »), branche `LoadingMap` (`:414-420`). Le groupe `Skip` de la nage annule au lieu d'appeler (`:664`).

### 3.4 Les quatre sites d'annulation, le site de blessure, et où ils atterrissent

Adresses du binaire : FAIT-RAPPORT (`e20-h4/report.md` §1, `check.md`, `e20-g1a/report.md` §1.3, `D4-D5`) ; atterrissages : FAIT pour les lignes citées, INFÉRENCE pour la forme du code à ajouter.

| Site binaire | Où dans `PlayerManager` | État à HEAD |
|---|---|---|
| Verrou `0x80031BFC` (précédé de `UpdatePlayerCarriedEntity(1)` en `0x80031BF4`) | `MovePlayer`, bloc verrouillé `:335-345`, entre `:342` (`WeaponRecordPointer = 0`) et `:343` (`EndOfMovePlayer`). Ordre du binaire : `Create(1)` `:337`, deux remises à zéro `:341-342`, mode 1, annulation, fin. Le plan (D-G-4) place déjà le mode 1 ici, « l'annulation d'objet reste le bouchon de h0 ». | Absent. Le commentaire `:330` dit encore que `UpdatePlayerCarriedEntity(1)` est sans effet. |
| Défaut `0x80032758` (états sans gestionnaire) | (a) nage : `SwimTick`, `case SwimHandler.Skip` `:663-665` appelle `CancelItemMode(player)` ; table `:176-189` (15 états + tout état ≥ 0x50, `:169`). (b) hors nage : un `else` final à ajouter après la chaîne qui se termine en `:618` (commentaire « every other TargetAnimationId case - NOT PORTED » `:620-621`), avant `EndOfMovePlayer` `:624`. | (a) en place, **bouchon vide** `CancelItemMode` (`:740-742`). (b) absent. INFÉRENCE : que l'adresse par défaut de la table principale `0x80022D3C` soit la même que celle de la table de nage n'est pas relu ici (INCONNU). Piège : les états 0x32/0x33/0x34 ont un gestionnaire dans le binaire (0x800325A4, 0x80032680) : h4a doit les traiter avant tout `else` final. |
| Case de sortie `+0x188 & 0x80`, `0x80032770` (`UpdatePlayerCarriedEntity(1)` en `0x80032778`) | nouveau bloc de `MovePlayer` entre `:350` et `:353` (l'appel `0x80031CC4` n'est atteint que « sans dégât, PV ≠ 0 et `+0x188 & 0x80 == 0` », `D4-D5`). | Non porté : `TileAttributes` (`EntityScriptProxy.cs:473`) n'est jamais écrit ; doc `PlayerManager.cs:66-67`, commentaire `:355-357`. |
| Mort `0x800327B4` (`UpdatePlayerCarriedEntity(2)` en `0x800327AC`) | nouveau bloc entre `:350` et `:353` (branche PV = 0, `0x800327A4` au plan, D-E20-5) ; hors de l'ordre physique du binaire mais avant `:353` dans le flux. | Non porté (E18), doc `PlayerManager.cs:62-65`. |
| Blessure `0x80031C6C` (pilote appelé après `UpdatePlayerCarriedEntity(2)` `0x80031C64` et les deux remises à zéro `0x80031C5C`, `0x80031C68`, avant l'écriture 0x31/0x3A en `0x80031C84`) | bloc E20.c2b entre `:350` et `:353` : il mettra `WeaponChargeTimer` et `WeaponRecordPointer` à 0 (`EntityScriptProxy.cs:434-441`), appellera `UpdatePlayerCarriedEntity(player, 2)` (stub `:1285`), puis `UpdateItemUseDriver(player)` (stub `:1507`), puis posera 0x31 (0x3A en eau profonde ; `SwimHurtAnimationId`, `:143`, existe). Le commentaire `:347-348` nomme l'entrée des dégâts `0x80031C44`. | Non porté : le héros ne perd aucun PV au contact (plan l. 2343) ; `PlayerManager.cs:722` : « Nothing enters 0x3A or 0x3B yet ». |
| Tick libre `0x80031CC4` (2e site du pilote, résultat ignoré) | `UpdateItemUseDriver(player)` `:353`. | Stub vide en place. |
| Garde de pente `0x800351B8` (interne aux 8 pilotes de tomes, 13 appels) | dans les pilotes (nouveaux), jamais dans `MovePlayer`. | Absent. En eau profonde le pilote (`:353`) passe avant la nage (`:372`), comme dans le binaire. |

Statut de E20.c2b et E18 (FAIT : grep du plan) : aucune section détaillée de E20.c2b (citée « après E14.a, qui remesure ses arcs », l. 727, et dépendante d'E20.i, l. 134-135, l. 570) ; E18 (mort, « Réessayer ») ne porte que les rappels D-E20-5, D-E20-20, O-E20-6 (« avant E18, le héros perd des PV mais ne meurt pas »). `GameState.DeathRetryCount` existe (`GameState.cs:286`) sans branche de mort. Pas de réinitialisation douce Start+Select (`hold == 0x900`) dans la DLL (`rg 0x900` : rien).

### 3.5 Tomes : MP, bâton, états

- FAIT : MP. `PlayerStats.Mp`/`MpMax` (`short`), plafond 4 (`PlayerManager.cs:1951-1965`), `SetPlayerMp` (`:1970`), HUD (`HudDirector.cs:612-613`), sauvegarde (`SaveGame.cs:182-183`). Pas de `DecreaseMp`/`GetPlayerMp`.
- FAIT : bâton magique. Objet 7, créneau 6 (`alundra-project/Data/items-properties.json` : `[6, 1, 1, 1, 37]`) ; `GetItemIdFromCurrentWeapon(...) == 7` suffit pour « aucun PM exigé ni consommé » (FAIT-RAPPORT pour la règle, FAIT pour la lecture de l'arme). Les sauvegardes d'essai « arsenal-a », « arsenal-b », « fire-m303 », « arrow-m16 » donnent le bâton (`TestSaves.cs:124,125,128,129`) ; le rapport dit que dans le jeu seul un script de la carte 134 le donne.
- FAIT : créneaux des tomes (données). Objets 43/44 → créneau 16, 45/46 → 17, 47/48 → 18, 49/50 → 19, drapeau 1 (remplaçable), rang 0 (niveau 1) et 1 (niveau 2) : posséder les deux fait passer l'objet du Rond au niveau 2 (`GetItemIdFromSlotId`, `PlayerManager.cs:2128-2177`). Bombe (35) : créneau 11 ; herbes (36) : créneau 12, 9 max ; potions 37, 38, 39, 41 : créneaux 13, 15, 30, 14 ; haricots (31) : créneau 29.
- FAIT : fiches d'animation du héros 0x32/0x33/0x34 (50/51/52) : vitesse 0, Sfx 0 ; types Once, Loop, Once. Les durées (32 ticks, boucle, 26 ticks) et les chaînes (0x32 → 0x33, 0x34 → 0) : FAIT-RAPPORT, non relues ici.

### 3.6 Registre, gestionnaires et sprites

- FAIT : clés de production (`NativeSpriteRegistry.CreateForProduction`, `:36-43`) : (A,1), (C,1), (D,1) pour le type 335 (`BeannoideAi.cs:33-35`) ; (A,0), (C,10), (D,9) pour le type 347 (`MurutaAi.cs:47-49`) ; (C,61) pour 27, 28, 29 (`WaveSwordAi.cs:31`) = 7 clés. Slots : A=0, B=1, C=2, D=3, E=4, F=5 (`Events/ScriptHelper.cs:19-24`).
- FAIT : en-têtes des sprites de h (export, `sprite-records.json`, clé `alundra_N`) :

| Sprite | A | C | D | E | Rôle | Clé à enregistrer (rapport h4) |
|---|---|---|---|---|---|---|
| 1 | 0 | 73 | 0 | 21 | haricots | (C,73) (E,21) — h3 |
| 2 | 0 | 62 | 30 | 12 | bombe | (C,62) (D,30) (E,12) — h3 |
| 16 | 0 | 0 | 0 | 3 | terre 1 | (E,3) |
| 17 | 0 | 0 | 0 | 13 | terre 2 | (E,13) |
| 18 | — | — | — | — | absent de l'export | — |
| 19 | 254 | 75 | 0 | 0 | orbe d'eau | (C,75) ; E0 inline |
| 20 | 0 | 0 | 0 | 1 | colonne de feu | E1 inline |
| 21 | 0 | 0 | 0 | 28 | feu 2 | (E,28) |
| 22 | 0 | 0 | 0 | 1 | vent 1 | E1 inline |
| 23 | 254 | 63 | 0 | 3 | boule de feu | (C,63) (E,3) |
| 24 | 254 | 65 | 0 | 1 | vent 2 | (C,65) ; E1 inline |
| 154 | 0 | 65 | 0 | 1 | boîte vide | (C,65) si le type est ajouté (check.md) |

  A254 sur 19, 23, 24 : aucune clé (A,254) : no-op compté avec une trace de debug unique par clé (`EventProgramRunner.cs:309-314`) ; conforme au rapport (fonction vide).
- FAIT : une clé de la DLL = un enregistrement restreint à des types (`Register` `:54`, doublon → exception `:62`). (E,3) devra donc couvrir {16, 23} (et plus tard les types d'ennemis d'E14) dans **un seul** enregistrement ou par un dispatcheur : INFÉRENCE.
- FAIT : `E0/E1` et `E10` ne passent pas par le registre (`EventProgramRunner.cs:283-292`, `:328`).
- FAIT-RAPPORT (`check.md` §2.3 de h4) : 4 entités de la carte 388 placent le sprite 16 hors du sort ; un enregistrement (E,3) pour le type 16 les concernera aussi en jeu. Aucun test ne charge la 388.
- FAIT : types. Page globale : type = indice ; page de carte : `0x100 + indice` (`AlundraHitArena.cs` assert `0x100 + 79`). Les sprites d'ennemis qui partagent E3 sont des types de page de carte : un enregistrement restreint à {16, 23} ne les touche pas.
- FAIT : `UnhandledWeaponSpriteCodes` (`PlayerManager.cs:1240`) ne vérifie que les codes C et E des types câblés au tir ; les sorts ne passent pas par le tir (le pilote appelle `SpawnEntityBySpriteIndex` lui-même) : `ProductionWiredWeaponTypes` (`:1226`) n'a pas à changer pour h3 et h4 (INFÉRENCE).

### 3.7 Briques moteur dont h3 et h4 dépendent (présence)

| Brique | Présence | Où |
|---|---|---|
| Naissance par indice de sprite (page globale ou de carte), parent, direction, plafond de 64 cases | Oui | `WorldProxy.cs:3488-3598`, `Entities/EntitySlotTable.cs:17,28` |
| Participation au tick de naissance | Oui | `JoinsBirthTick` `WorldProxy.cs:3572`, `RunEntityBlock` `:2932-2936` |
| Z logique des entités nées (gravité, impulsion, borne de carte) | Oui | `HasLogicalZ` `WorldProxy.cs:3573` ; `ScriptedMotion.cs:212` (pas vertical avant XY, `RunLogicalZPreXyStep` `:465`), `:741` (borne de carte) |
| Destruction avec effet de bris (2 args) / sans effet (1 arg) | Oui | `WorldProxy.cs:3301`, `:3329` |
| Statut 3 « seulement E » | Oui | `EntityScriptProxy.cs:1717-1726,1758` |
| Passe de coups et dégâts | Oui | `Combat/HitPass.cs`, `Combat/Damage.cs` ; dégâts par le programme D de la cible |
| Réservoir d'effets, création attachée, file de la scène | Oui | `EffectStage.CreateAttachedNow` (`PlayerManager.cs:1139`), `Emit` (`:1101`), `NativeEffects` |
| Tremblement de caméra (9 champs) | Oui, sans capture ni restauration | `CameraSway.cs`, appelé `CameraDirector.cs:216-231,270` |
| Coup de caméra Y d'un tick d'E13 | Non | |
| Poussée `+0xCC/+0xD0` consommée par le déplacement | Non | R27 |
| Accès 32 bits à `+0x274..+0x290` | Non (champs byte/short) | R28 |
| Requête de cibles + tri de Hoare | Non | R26 |
| Tables sin/cos 256 | Non | C2 |
| Tables 32 directions `0x80023654/94` | Oui | `AnimationTables.cs:71,82` |
| Indice du plus haut emplacement occupé (`[0x80134360]`) | Non | C1 |
| Départ XA / piste CD | Non | R24 |
| Soins `0x80032F84`, `0x80033274` | Non (effets du réceptacle de vie fusionnés) | R20 |
| `CheckAndTriggerTileEffect` | Bouchon nommé | `WaveSwordAi.cs:40` ; E20.e fait `Cells/TileEffect.cs` |
| `ResyncControllerFromFlags` | Oui | `EntityScriptProxy.cs:670` |
| Tirage de nombres aléatoires | Oui | `Alundra/Rng/` (`GameRandom` ; `EnemyHelpers.Below`, `Ai/EnemyHelpers.cs:35`) |

## 4. Tests

Seules les lignes relues sont données. Classement : **casse** (échoue dès que la tranche touche ce qu'il épingle), **à revoir** (peut changer ; INFÉRENCE), **conscient du bouchon** (passe avant et après, mais encode le bouchon ou masque le comportement), **neutre**.

### 4.1 Casse

| Test | Ligne | Ce qu'il épingle | Tranche |
|---|---|---|---|
| `AlundraNativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheSevenHandlersOfTheBeannoidTheMurutaAndTheSwordWave_AndTheSwitchIsOn` | `:50-66`, assertion `:55` : `Assert.Equal(6 + 1, runner.NativeSprites.Count)` ; doc de classe `:15-17` | 7 clés de production | toute tranche qui enregistre une clé (h3 : au moins C62, D30, E12, C73, E21 ; h4 : E3, E13, E28, C63, C65, C75 = 6) |
| `AlundraWaveSwordLifeTests.V3_TheProductionRegistryHoldsSevenKeys_…` | `:166-188`, tuple `(3 + 3 + 1, …)` `:182`, `registry.Count` `:183` | idem | idem |

Note : ces deux épingles sont déjà annoncées « à réécrire par dérivation » par le plan (§1.24, tête). `AlundraNativeSpriteRegistryTests.cs:84-96` (`ProductionRunner_NoRegistrationListsTheTypeMinus1`) reste vrai si les enregistrements listent des types explicites (jamais −1). `:238-251` (doublon) utilise une clé libre (C,25), sans conflit.

### 4.2 À revoir

| Test | Ligne | Pourquoi | Tranche |
|---|---|---|---|
| `AlundraLogicalZWorldTests.Z4_AnImpulseOf1024_…` via `Throw` | `:270-300` (`Born(arc, false, Bomb, …)` `:292`, puis `bomb.TargetAnimationId = 2` `:293`) | emploie le **sprite 2 (bombe)** né par l'interface comme « entité à gravité » ; `ArcRun` garde le registre natif actif ; avec C62/D30/E12 enregistrés, la bombe née (fusée `+0x274` à 0 : le `0xF0` est écrit par la fonction de départ) exécutera son C62 : effet inconnu ici (C62 non lu), donc la série de Z de l'oracle peut changer | h3 |
| `AlundraLogicalZWorldTests.Z7_TheStepHeight_…` | `:394-413` (`Born(…, Bomb, …)` `:398`) | idem | h3 |
| `AlundraTestSavesTests.TheNinePresets_AreListed_InTheOrderOfTheTable` | `:88-91`, liste `:72` | ajouter un préréglage (h3 : bombe/haricots ; h4 : sorts ; plan g1b : `porter-m<N>`) change la liste | h0/h3/h4 si un préréglage est ajouté |
| `AlundraTestSavesTests.AWeaponPreset_IsTheNewGamePlusItsLoadout_InEveryField` | `:407-433`, `:429` « no Circle item », tuple `:432` avec `save.ItemId` | pose `ItemId == 0` pour les 6 préréglages d'armes ; donner un objet du Rond à l'un d'eux casse le test pour ce préréglage | idem |
| `AlundraTestSavesTests.AWeaponPreset_ReloadedByTheLoadPath_…` | `:439-455` | épingle les compteurs du kit (`CommonCounters` `:361`) : ajouter les haricots (31) ou la cape aux `TestKitItems` change les compteurs attendus | h3 |

### 4.3 Conscients du bouchon (passent avant et après)

| Test | Ligne | Détail |
|---|---|---|
| `AlundraHeroRunTests.G7_InThePreparation_…` | `:600-615`, `ignore: (tick, field) => tick == 10 && field == "sounds"` `:612` ; docs `:20-21`, commentaire `:611` | le Rond pressé dans la préparation 0x29 : le binaire joue le son 3 au tick 10 (trace `R7-circle-windup`) ; le bouchon n'en joue pas. Après h0 l'ignore devient inutile et masque l'égalité. |
| `AlundraHeroSwimTests.StubbedSounds` | `:60` (`id == 3 \|\| …`), emploi `:106` ; doc `:20`, `:59` | toute la suite de nage ignore le son 3 des deux côtés. |
| `AlundraRunSupport.CompareRows` | `:402-405` (`ignoreSound`, commentaire « the sound 3 of the Circle is E20.h »), `IsStubbedSound` `:334` (11 et 12, E20.e) | |
| `AlundraHeroSwimTests.W3_TheCircle_DoesNotBurst_ItIsTheItemButton` | `:356-363` (traces `w3/W3-O-swim`, `W3-O-idle`) | oracle : son 3 aux ticks 20 et 10 ; le test n'assert que l'absence d'élan. |
| `AlundraHeroSwimTests.W9W10_TheOtherCases_AreTheOracle` | `:665-673` (`w9/W9-circle-idle`, `W9-circle-swim`) | oracle : son 3 aux ticks 10 et 20. Avec l'objet du Rond à 0 (kit), `TryUseItem` échouera avec le son 3 : le test deviendra exact si l'ignore est retiré. |
| `AlundraHeroRunStatesTests` (seam `TryUseItemResultForTests = 0`) | `:109, :202, :336, :438` ; `Dispose` `:38` | les branches « le pilote d'objet a répondu 0 » sont inatteignables par le bouchon : les tests posent 0. Valables tant que le seam est gardé (`PlayerManager.cs:937`). |
| `AlundraHeroSwimBranchTests` (même seam) | `:39, :178, :274` ; `AlundraHeroSwimTests.cs:49` | idem. |
| `AlundraHeroRunStatesTests.Bounce_ThePadIsNotRead_NoTackleNoSwing` | `:472-480` (`Move(hero, Triangle \| Square \| Cross \| Circle, pressed: Square \| Cross \| Circle)`, hôte nul) | impose que `TryUseItem` réel rende 1 sans exception avec un Rond pressé, sans hôte, sans tables. |
| `AlundraHeroSwimBranchTests.Steer_TheCircleDoesNotBurst_…` | `:208-215` | idem pour l'état Float (hôte nul). |

### 4.4 Neutres ou non affectés

- `AlundraWeaponSpawnSeamTests` S1 (`:83-107`) et S5 (`:238-258`) : S1 pose `Wire(Arrow)` (type 3) et assert qu'un ensemble vide ne crée rien ; S5 assert `ProductionWiredWeaponTypes == {27, 28, 29}` (`:243`), `UnhandledWeaponSpriteCodes` vide pour eux (`:244`), puis `(E,17)` non géré pour le type 3 et `(C,94)` pour le type 7 (`:246-257`). Ni h0, ni h3, ni h4 ne touchent ces valeurs **à condition de ne pas ajouter 1, 2, 16 à 24 à `ProductionWiredWeaponTypes`** (les sorts n'utilisent pas le tir). S5 enregistre `(E,17)` sur le registre de production : un futur enregistrement de production (E,17) (E14.f5) le ferait lever ; sans rapport avec h.
- Aucun test ne presse le Rond au niveau d'un monde : `Press(PadState.Circle)` et les maintiens à `0x20` n'apparaissent nulle part dans `Alundra.Tests` (`rg` : le Rond n'est cité que dans les tests de course, de nage et de pad listés ci-dessus et dans `AlundraPlayerControllerTests.cs:164`, test du pad seul). Aucune assertion « aucune entité ne naît au Rond ».
- Les oracles avec un front de Rond : 26 traces (`docs/plan-e20-annexe/e20-d/oracle/out*/`) = `w3/W3-O-idle`, `W3-O-swim`, `w9/W9-circle-idle`, `W9-circle-swim`, `W9-item1F` à `W9-item32` (20), `g7/R7-circle-sprint`, `g7/R7-circle-windup` (script Python sur les JSON). Seules `W3-O-*`, `W9-circle-*` et `R7-circle-windup` sont chargées par les tests ; les 20 `W9-item*` ne le sont pas (`rg W9-item` : rien) ; elles journalisent le seul son 3 (objet équipé en nage). `R7-circle-sprint` : pas de son 3 (la course n'appelle pas `TryUseItem`), c'est ce que fait la DLL.
- `AlundraInventoryDirectorTests.cs:208-330` (gardes du déclencheur : drapeaux, `BlockedByEntity`, délai, départ de warp, Select, boîte de dialogue) : aucun test du verrou d'objet (`rg warpLock|ItemInUse` : rien). Un test nouveau, pas une épingle qui bouge.
- Tests qui font naître tous les préfabs globaux (`AlundraBirthBoxTests.cs:131`, `AlundraSpawnBySpriteIndex*Tests`) : sans tick, donc sans gestionnaire.
- `AlundraPlayerManagerTests.MovePlayer_AirStillOnTheGround_…` (`:100-114`) : l'exemple d'état non porté est 0x20 (cape de sable) ; neutre pour h0, h3, h4 (0x32 à 0x34 ne sont dans aucun test d'état : `rg` vide), cassera si la cape (h2) est portée.
- `AlundraTickStructureTests.cs:169,239` : l'ordre des passes ; h0/h3/h4 n'ajoutent pas de passe (le pilote est dans `MovePlayer`).
- Oracles d'état `AlundraBeannoideOracleTests.cs:93-94`, `AlundraMurutaOracleTests.cs:92-93` : posent `PreviousAdjustedForceX/Y` depuis les vecteurs et les relisent ; sans passe de mouvement dans ces appels de gestionnaire unique ; neutres tant que la consommation reste dans la passe de forces (INFÉRENCE).

## 5. Sauvegardes d'essai et bancs

### 5.1 Ce que les sauvegardes d'essai donnent (`SaveGames/TestSaves.cs`)

- FAIT : kit de tout préréglage d'armes (`TestKitItems` `:72-75`) : bombe 35 (×1), herbes 36 (×9), 37 (×1), 38 (×1), 41 (×1), et les parchemins de niveau 1 **43, 45, 47, 49** ; plus le kit de la nouvelle partie (objets 1, 17, 25, compteurs `3, 35, 51` de `AlundraTestSavesTests.cs:361`). Vie 4/10, magie 4/4 (`WeaponPreset` `:94-98`).
- FAIT : par préréglage (`:124-129`, nage `:134`) : `arsenal-a-m14` + objets 5, 9, 14, 16, 7 (bâton) ; `arsenal-b-m14` + 4, 6, 10, 14, 16, 7 et les **livres 44, 46, 48, 50** ; `steel-m14` + 2 ; `demon-m14` + 3 ; `fire-m303` et `arrow-m16` + 5, 9, 14, 16, 7 ; `swim-m14` : bottes 0x1B seulement (magie 0/0, vie 10/10). Cartes : 14 (8,6,0), 303 (38,11,1), 16 (34,31,3).
- FAIT : les haricots (31), la cape de sable (32), l'Ale (39) ne sont donnés par aucun préréglage. Les soins de h1 : herbes 36, 37, 38, 41 oui, 39 non.
- FAIT : **aucun préréglage ne fixe l'objet du Rond** : `Preset.Item` existe (`:57`, appliqué par `SetCurrentItemId` `:236`) mais les `WeaponPreset` ne le posent pas ; l'`ItemId` sauvé vaut 0 (pin `AlundraTestSavesTests.cs:429-433`). Sans objet du Rond, `TryUseItem` échoue (son 3) : un test de tome devra poser `Item` (43 à 50) ou appeler `SetItem`. Poser un tome de niveau 1 alors que le niveau 2 est possédé le promeut au premier tick (R6) : c'est le comportement du binaire.
- FAIT : le chargement en jeu passe par `tools/AlundraTestSaves/Program.cs` (écrit le préréglage dans le dossier de sauvegarde, F9 de la DLL Debug le charge).

### 5.2 Bancs

| Banc | Ce qu'il permet pour h | Limites |
|---|---|---|
| `ArcRun` (`AlundraArcSupport.cs:94`) avec `ArcSpec(…, RealController: true, Prefabs: true)` (modèle : `AlundraWeaponSpawnSeamTests.cs:38`, carte 14) | `Give(params int[])` (`:151`, par `AddOneItemIfUnlocked`), `Equip(slot)` (`:161`), `SetMp(max, mp)` (`:164`), `SetItem(item)` (`:171`, objet du Rond), `Press(PadState.Circle)` (`:454-459`, une image avec le bouton tenu et pressé), `HoldDirections` (`:463`), `OneFrame(elapsed)`. Monde réel : préfabs des sprites 1, 2, 16, 17, 19 à 24 (le 18 n'existe pas), registre natif actif (`NativeHandlersEnabled` vrai par défaut, `EventProgramRunner.cs:56`), hôte `IEntityWorldContext` (naissances), sons et effets réels. Helpers testés par `AlundraArcInventoryHelpersTests.cs` (carte 10 `Overworld 2,1-10`, arrivée de portail). | Aucun test de tome ni de bombe ne l'emploie encore. |
| `HitArena` (`AlundraHitArena.cs:27`) | enveloppe `ArcRun` (`Arc`) : `Frame(hold, ticks)` (`:166-170`), `BeforeTick`/`AfterTick`, échantillons par tick, cibles = 7 Beannoïdes de la carte 14 (cibles utiles à un test de sort). | **Coupe les gestionnaires natifs** (`:70`) : les C63/C65/C75 enregistrés ne tourneraient pas ; à rallumer comme `AlundraWaveSwordLifeTests.cs:128`. Les Beannoïdes y sont des no-ops comptés (D natif coupé : pas de dégâts réels). |
| `ContactHost` + `JumpHeroRig`/`RunMontage` (`AlundraContactTestSupport.cs:52`, `AlundraJumpTestSupport.cs:235`, `AlundraRunSupport.cs:202`) | hôte minimal : `SoundPlayer` (`RecordingSoundPlayer`), `ItemTables`, `BalanceTables`, `EffectStage`, `GameState` propre, héros réel ; pad à la main (`HeroPad`) ; le montage `RunMontage.Start` joue une trace d'oracle et lit les sons (`:242-246`). Convient au cadre h0 (son 3, garde de verrou, gardes de pente) et aux comparaisons de trace. | **N'implémente pas `IEntityWorldContext`** : `SpawnEntityBySpriteIndex` rend `null` (défaut d'interface), donc aucun sort, aucune bombe ne naît (vu aussi par `AlundraWeaponSpawnSeamTests.S3`, `:159`). |
| Oracle Python de h4 | scénarios `trace_cast.py`, `trace_targets.py` (graine, héros en (100,100) px) | dans l'annexe `docs/plan-e20-annexe/e20-h4/work/`, hors du projet de tests : `Alundra.Tests/Data` n'a que `story-chain-skipped-opcodes.tsv` (en-tête seul, aucune ligne). |

## 6. INCONNU et points laissés ouverts

- INCONNU : le corps de C62, D30, E12 (bombe) et de C73, E21 (haricots) : non lus dans cette tâche ; ils décident de l'effet de h3 sur `AlundraLogicalZWorldTests.cs:292,398`.
- INCONNU : l'adresse par défaut de la table d'états principale `0x80022D3C` (même `0x80032758` que la nage ?) ; le binaire n'a pas été relu.
- INCONNU : si une carte de l'export place le sprite 154 (C65) ; si le Rond est utilisable par des cartes de la chaîne actuelle avec un tome (Q-H4-7).
- INCONNU : sons des effets 8, 14, 18, 19 (variante 2), 26 (Q-H4-5) ; le contenu des pistes XA.
- INFÉRENCE à confirmer par la tranche : C75 écrit des drapeaux (`ResyncControllerFromFlags`) ; les sprites des sorts reçoivent la borne de carte parce que `HasLogicalZ` est posé ; l'ordre `hurt`/`mort`/`case de sortie` avant `:353`.
