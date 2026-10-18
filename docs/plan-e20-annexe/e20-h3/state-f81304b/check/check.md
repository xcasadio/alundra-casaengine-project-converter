# Contre-vérification de `dll/state.md` (état de la DLL et des tests pour E20.h0, h1, h3, h4)

Ma contre-vérification fait foi là où elle diffère. Légende : FAIT (lu dans le code, les tests, les données exportées ou le binaire, avec fichier:ligne ou adresse), INFÉRENCE (déduit, de quoi précisé), INCONNU. Verdicts sur les citations : JUSTE, FAUX, IMPRÉCIS, plus PÉRIMÉ (le rapport l'annonce lui-même, je confirme ou non) et HORS DLL.

## 0. Base, méthode, limites

- FAIT : HEAD actuel `f81304b4` (branche `chantier/restructuration-dll-r3`). Le rapport a été écrit à `66d86ebc` ; quatre commits de docs ont suivi. `git diff --stat a3ccb75 HEAD -- Alundra Alundra.Tests` et `git diff --stat 66d86ebc HEAD -- Alundra Alundra.Tests` sont vides : les numéros de ligne du rapport sont ceux du HEAD actuel.
- Rien n'a été écrit hors de mon dossier ; aucun build, test, export ; `git status` montre seulement deux sous-modules marqués modifiés (non touchés).
- Méthode DLL : `dump_cites.py` (extraction de toutes les occurrences `fichier:ligne` de `state.md`, 364 avec les anciens noms ; sortie `cites.txt`) puis relecture à la main de chaque ligne citée et du code autour. Le `verify_cites.py` de l'auteur du rapport ne contrôlait que ses propres regex et n'avait pas les lignes `WorldProxy.cs:3306` et `:3315` : je ne m'y suis pas fié.
- Méthode binaire (lecture seule de `ALUN_CD.EXE` France, scripts à moi, pas l'harnais de l'annexe) : `read_tables.py` (tables d'états `0x80022D3C` et `0x80022E7C`), `scan_jal.py` (sites d'appel `jal`), `mdis.py` (capstone). Sorties gardées : `evidence_tables.txt`, `evidence_jal_sites.txt`, `evidence_disasm.txt`.
- Limites : les corps de C62, D30, E12, C73, E21 et des gestionnaires de sorts ne sont pas relus ; la classification « casse / à revoir » des tests est une lecture d'atteignabilité, pas une exécution ; les valeurs de comportement du binaire non listées en §5 restent FAIT-RAPPORT (h4/report.md et check.md).

## 1. Verdict

1. Le fond du rapport est exact : rien de h0, h1, h3, h4 n'est dans la DLL (FAIT : `TryUseItem` bouchon `Hero/PlayerManager.cs:932`, pilote vide `:1507` appelé en `:353` seulement, `CancelItemMode` vide `:740` appelé en `:664` seulement, registre à 7 clés `Ai/NativeSpriteRegistry.cs:36-43`, aucun `DecreaseMp`, aucune table sin/cos, aucun consommateur de `PreviousAdjustedForceX/Y`, aucun support XA ; `rg` vides). Toutes les citations DLL de `e20-h4/report.md` §8 et de `check.md` sont encore vraies pour le fond.
2. Sur les 84 lignes des tables du rapport : 71 JUSTE, 2 JUSTE-INFÉRENCE (R35, C6), 5 PÉRIMÉ confirmées (A6, H7, H8, H9, G9 en partie), 2 HORS DLL (R37, G15), **4 IMPRÉCIS (R6, R13, R26, R33), 0 FAUX**.
3. Dans la prose (§3 à §5 du rapport) : 7 imprécisions de plus (§3 ci-dessous), dont trois qui changent une tâche : le « `else` final » de l'annulation (S1), la liste « non appelé » de `TryUseItem` (S2), et le fait que E3 serve aussi quatre sprites de la page globale (R33).
4. Un INCONNU du rapport est tranché par le binaire (adresse par défaut de la table `0x80022D3C`, §5).
5. Le rapport couvre tout le §8 de `h4/report.md`. Ce qu'il omet est en §4.

## 2. Les 84 citations

### 2.1 `e20-h4/report.md` (R1 à R37)

| # | Verdict | Ma preuve |
|---|---|---|
| R1 | JUSTE | `PlayerManager.cs:932` `=> TryUseItemResultForTests ?? 1`, `pad` non lu ; `[ThreadStatic] internal static int?` en `:936-937`. |
| R2 | JUSTE | `rg UpdateItemUseDriver` : appel `:353` seul, définition `:1507`. `:350` `Create(FreeMode)`, `:359` lecture du pad, `:372` nage, `:426` aiguillage. |
| R3 | JUSTE | `:1507-1509` corps vide. |
| R4 | JUSTE | `:2087` rend `uint` ; `NoItem` `:2036` ; `GetWeaponIdBySlotId` sans `default` rend `NoItem` pour le créneau -1 (`WeaponId - 1`, `:2089`). |
| R5 | JUSTE | `:2181`, `ItemsCount = 99` `:2033`. |
| R6 | IMPRÉCIS | Le corps est juste (`:2217-2233`). « Déjà appelé à chaque tick par `FillEquipmentRecords` » ne vaut que pour un hôte qui porte `EquipmentRecords`, `ItemTables` et `BalanceTables` (`:1308-1322`) ; `MovePlayer(..., host: null)` ou un `ContactHost` nu ne promeut rien. Le test qui l'épingle, non cité : `AlundraHeroLifeTests.cs:828-846`. |
| R7, R8, R9 | JUSTE | `:1970` borne `[0, MpMax]` (`short`), `:1951` plafond 4, `:2263` ; `EventProgramRunner.cs:1520-1522` (0x84). |
| R10 | JUSTE | Aucun id 0x32-0x34 dans `MovePlayer` (`:282-625`) ni dans `:1026-1031`. Binaire : table de nage `0x80022E7C` donne 0x32, 0x33, 0x34 à `0x80032674` = `Float` ; table principale `0x80022D3C` les donne à `0x800325A4` (0x32, 0x33) et `0x80032680` (0x34). Données : fiches 50-52 Speed 0, Acceleration 64, Sfx 0 ; `anim2d` Once/Loop/Once. |
| R11 | JUSTE | `WorldProxy.cs:3488`, `:3549` `z + 1`, `:3554`, `:3566`, `:3572-3573` ; `IEntityWorldContext.cs:81` `=> null`. (Le plafond est 63 cases libres, voir S4.) |
| R12 | JUSTE | `sprite-records.json` relu par `BankKey` : 1 (C73 E21), 2 (C62 D30 E12), 16 (E3), 17 (E13), 19 (A254 C75), 20 (E1), 21 (E28), 22 (E1), 23 (A254 C63 E3), 24 (A254 C65 E1), 154 (C65 E1, `MoreFlags` 68, tailles 0) ; 18 absent (aussi 72, 120-149, 155+). Préfabs dans `AssetInfos.json` : « Haricots de Jack », « Bombe », « Magie de terre Niv.1/2 », « Magie d'eau Niv.2 », « Magie de feu Niv.1/2 », « Magie de vent Niv.1 », « Projectile de magie de feu Niv.1 », « Magie de vent Niv.2 (bas) ». |
| R13 | IMPRÉCIS | `:3301` ✓, butin `:3303` ✓, porteur `:3315` ✓. « Effet actif `:3306` » : la ligne 3306 est vide, le bouchon (4) est en **`:3307`**. |
| R14 | JUSTE | `:3329`, `:3604`. |
| R15 | JUSTE | `EventProgramRunner.cs:283-292`, `:328` (codes 0, 1, 10). |
| R16 | JUSTE | `:36-43` : Beannoïde 3 + Muruta 3 + C61 = 7. |
| R17 | JUSTE | `EntityScriptProxy.cs:1717-1726`, `:1758-1760`. |
| R18 | JUSTE | `Damage.cs:24`, `:79`. FAIT supplémentaire : `ComputeNewHp` n'a qu'un appelant (`Ai/EnemyHelpers.cs:232`, Beannoïde et Muruta) et `Damage.cs:82` est le seul écrivain de `Hp` d'une autre entité : l'inférence « un sort ne blesse aujourd'hui que ces deux types » est exacte. |
| R19 | JUSTE | `CameraSway.cs:33-41` (9 propriétés `internal set`), `Start` `:45` (5 champs), `Step` `:65`. |
| R20 | JUSTE | `NativeEffects.cs:28-31` : 8 anneaux puis 4 étincelles de l'effet 14 ; seul appelant `EventProgramRunner.cs:3154`. |
| R21, R22 | JUSTE | `TestSaves.cs:72-75`, `:94-98` ; `InventoryDirector.cs:405`, `:419-422`. |
| R23 | JUSTE | Pas de `DecreaseMp` ni de `GetPlayerMp`. |
| R24, R25 | JUSTE | `EventProgramRunner.cs:3081` ; `EventOpcodeSizeTable.cs:233` seul ; aucun mot XA/CD dans `Alundra/`. |
| R26 | IMPRÉCIS | `EntitySearchService.cs:75` est la déclaration de classe ; `GetMatchingEntitiesBySearchType` est en `:99`. Le fond (autre algorithme, pas de tri) est juste. |
| R27 | JUSTE | `EntityScriptProxy.cs:341-342` ; seuls `Clone` `:2638-2639` et les tables d'oracle les touchent ; `ScriptedMotion.cs:609`, `:614`, `:668-674`, `:676-680`, `:683-691`, `:741` ✓. |
| R28 | JUSTE | `Bytes` `:1227`, `AIValues` `:1230` ; précédent `SaveBook.cs:164-171`. |
| R29 | JUSTE | `:670` ; `EntityFlags.cs:69,75,103,130` = 0x10, 0x20, 0x100, 0x2000. Précision : `NoObstacleSlide` est consommé par la DLL (`ScriptedMotion.cs:819`, « forbidden » → pas de glissade) ; le sens du bit 0x2000 dans le binaire reste INCONNU (le rapport h4 dit « glisse »). |
| R30 | JUSTE | `CameraMath.cs:216-228`, `:223` ; 0 occurrence de `DD874`. |
| R31 | JUSTE | `effects-global.json` : 29 effets ; 8 (1 animation), 14 (3), 18 (1), 19 (3), 26 (1). |
| R32 | JUSTE | `SaveGame.cs:182-186`, `:221-228` ; `HudDirector.cs:566-567`, `:612-613`. |
| R33 | IMPRÉCIS | `NativeSpriteRegistry.cs:54-67` ✓ ; types `0x100 + indice` (page de carte, `EntitySpawnFactory.cs:541`) ✓. Mais « les sprites qui partagent E3 sont des types de page de carte » est faux pour quatre sprites de la **page globale** : 150, 151, 152, 153 (« Projectile (ligne droite/chute/parabolique/boomerang », C24 ou C36, **E3**, `MoreFlags` 114). Types 150-153, sans offset. |
| R34 | JUSTE | Binaire : pilote `jal 0x8003634C` en `0x80031CC4`, avant le saut d'état `0x80031D80-0x80031DAC`. |
| R35 | JUSTE (INFÉRENCE) | `WorldProxy.cs:2075` (« reconstruit par monde » dit sa doc) ; `GameState.cs:120-124` ; `RegenerationCounters` et `LowHpAlarmCounter` absents des trois remises à zéro (`:368`, `:411`, `:438`). |
| R36 | JUSTE | `InventoryDirector.cs:405-427` ; appel `WorldProxy.cs:2591` après le bloc d'entités. |
| R37 | HORS DLL | |

### 2.2 `e20-h4/check.md` (C1 à C12)

| # | Verdict | Preuve |
|---|---|---|
| C1 | JUSTE | `HighestLiveSlot` privé `:182`, seul usage `:264` ; `RecycleDestroyedEntities` `:3265` ; `EntitySlotTable` : `HeldCount`, `IsHeld`, `Allocate`, `Release`, `HoldHeroSlot`. |
| C2 | JUSTE | Binaire : `0x800204F0`/`0x800206F0` = 256 halfwords, amplitude 256 (sin[1] = 6, cos[0] = 256). DLL : aucun `Math.Sin/Cos`, aucun « sinus/cosinus ». Tables 32 directions `0x80023654/94` = `AnimationTables.cs:71,82` (valeurs recontrôlées au binaire). |
| C3, C4 | JUSTE | `:1230` ; absence. (Voir §4 B8 pour `Step`.) |
| C5 | JUSTE | `PlayerManager.cs:321-328`, `WorldProxy.cs:2905-2917`, `GameState.cs:99`. Binaire : `0x80031BA4` teste `+0x20`, `0x80031CB8` teste `+0x188 & 0x80` avant le pilote. |
| C6 | JUSTE (INFÉRENCE) | |
| C7, C8, C9, C10, C11 | JUSTE | `CameraMath.cs:223` ; `:131`, `:372` ; ordre des passes `WorldProxy.cs:2905-2988` (`movePlayer, programs, pending, counters, lists, clock, physics, activeEffects, hit`) ; `TestSaves.cs` ; `EventProgramRunner.cs:283-292`. |
| C12 | JUSTE | Voir R12. |

### 2.3 `e20-g1a` (G1 à G15)

G1 à G8, G10 à G14 JUSTE (lignes relues : `:408`, `:321-345`, `:394-402`, `:456-459`, `:432/:451/:756/:800/:861`, `EntityScriptProxy.cs:101-105`, `:2609-2613`, `PlayerManager.cs:399`, `WorldProxy.cs:3301-3334`, `ScriptedMotion.cs:676-680`, `MovementObstacleProbe.cs:51`, `EntityScriptProxy.cs:1383`, `PlayerManager.cs:1282`, `:2181`, tests `:51-55`, `:88-91`). Complément G6 : `CarriedEntity` n'est jamais écrit en production, mais l'est par `AlundraLadderClimbTests.cs:242,669,688`. G14 : `PutInHands`, `TryLift`, `ThrowCarried` 0 occurrence (`rg`). **G9 PÉRIMÉ en partie : confirmé** (`ScriptedMotion.cs:173-175`). G15 HORS DLL.

### 2.4 `attacks.md` (A1 à A8) et notes anciennes (H1 à H12)

A1 à A5, A7, A8 JUSTE. **A6 PÉRIMÉ : confirmé** (`WaveSwordAi.cs:34,40`). `rg -i bomb Alundra.Tests` : seulement `AlundraLogicalZWorldTests.cs` et `AlundraTestSavesTests.cs:359,451` (A3 juste). H1 à H6, H10 à H12 JUSTE. **H7, H8, H9 PÉRIMÉ : confirmés** (`EventProgramRunner.cs:264-324` consulte le registre ; `PlayerManager.cs:321-345` atteint `EndOfMovePlayer` `:326`, `:343` ; miroir des PV `:634-645`, `IncreaseHpMaxAndFill` `:1875-1887`). H11 : nuance, un opcode de script peut lire n'importe quel bit du pad (`EventProgramRunner.cs:1833-1837`) ; aucun code **natif** ne lit le Rond hors `PlayerManager.cs:840`.

## 3. Imprécisions de la prose

| # | Où dans le rapport | Correction (mon FAIT) |
|---|---|---|
| S1 | §3.4 ligne « Défaut `0x80032758` », (b) « un `else` final à ajouter » | Binaire : la table principale `0x80022D3C` envoie à `0x80032758` exactement les **mêmes 15 états** que la table de nage (0x0D, 0x1A, 0x1B, 0x1E, 0x27, 0x37, 0x38, 0x3D, 0x43, 0x45, 0x47, 0x48, 0x4A, 0x4C, 0x4D), et `0x80031D8C-0x80031D90` (`sltiu 0x50`) y envoie tout état ≥ 0x50. Un `else` final de `MovePlayer` attraperait en plus **21 états non portés qui ont leur propre gestionnaire** : 0x05, 0x06, 0x07, 0x0B, 0x0C, 0x20-0x24, 0x21, 0x2E-0x30, 0x31-0x34, 0x39, 0x3C, 0x4E, 0x4F. L'annulation ne doit donc pas être un `else` mais un test `SwimHandlerOf(état) == Skip` (la table existe, `:169-190`) dans la chaîne. Le rapport ne voyait le piège que pour 0x32-0x34. |
| S2 | §3.3 « Non appelé : `SprintTick` (comme le binaire), états d'échelle, `LoadingMap` » | La liste mêle conforme et non conforme. Binaire : **15** sites `jal 0x8002ED64` (`evidence_jal_sites.txt`) ; la DLL en a 13 lignes qui couvrent 8 des 15. Sans équivalent DLL : `0x800321D4` (échelle 0x0E/0x35 : à ajouter par g1a phase B, plan (m8)), `0x800322B4` (0x0B), `0x800322F0` (0x05), `0x80032334` (0x06, 0x07, 0x0C, 0x2E-0x30), `0x800324C0` (0x22, 0x23), `0x8003251C` (0x20, 0x24), `0x800325A4` (0x32, 0x33 : h4a). Conformes au binaire : `SprintTick` (`0x80031FF4` appelle seulement `PlayerTryAction`) et `LoadingMap` (`0x800325E8` n'a pas de site). |
| S3 | §3.6 « Les sprites d'ennemis qui partagent E3 sont des types de page de carte » | Faux pour 150-153 (page globale), voir R33. Un enregistrement `(E,3)` limité à {16, 23} ne les touche pas ; l'enregistrement commun avec E14 (Q-H4-8) doit les citer. |
| S4 | §3.7 « plafond de 64 cases » | `SlotCount = 64` mais le héros tient la case 0 : **63** entités nées au plus (`EntitySlotTable.cs:16`, `:28-41` ; test `AlundraEntitySlotWorldTests.cs:168-186`). |
| S5 | §4.2 ligne `AlundraTestSavesTests.AWeaponPreset_IsTheNewGamePlusItsLoadout_InEveryField` | Ne pose pas que `ItemId == 0` : il épingle **tout le tableau des compteurs** (`:421-427`, `CommonCounters` + `Extra`). Ajouter les haricots (31) ou tout objet à `TestKitItems` le casse pour les six préréglages ; idem `:439-455` et `:451` (bombe). |
| S6 | §4.4 « Seules W3-O-*, W9-circle-*, R7-circle-windup sont chargées » | `g7/R7-circle-sprint` l'est aussi (`AlundraHeroRunTests.cs:569`, Theory `G7_ASquareACrossOrACircle_InTheRun_TacklesFor32Ticks`) ; la phrase suivante du rapport le suppose déjà. |
| S7 | §4.3 `Bounce_ThePadIsNotRead_NoTackleNoSwing` « impose que `TryUseItem` réel rende 1 » | Le test (`AlundraHeroRunStatesTests.cs:472-480`) n'asserte que `TargetAnimationId == Bounce` : il impose « pas d'exception, animation inchangée » ; une réponse 0 ne ferait que retourner la direction. Même remarque pour `Steer_TheCircleDoesNotBurst…` (`AlundraHeroSwimBranchTests.cs:208-215`). |

## 4. Ce que le rapport a manqué

### A. Revendications de `e20-h4/report.md` §8 et de `check.md` §3

Toutes les lignes de « Déjà là » et de « À ajouter » du §8, et les huit manques de `check.md` §3, sont traitées (R1-R36, C1-C12). Partiellement : (1) les cinq gestionnaires d'annulation propres aux tomes (`check.md` §3.7, aucun rattachement à une tranche) ne sont pas listés comme « absents » ; FAIT : aucun n'existe (`rg` des sites `0x80034BDC`, `0x80034C54`, `0x80034D2C`, `0x80034E08`, `0x80034EC4` : rien) ; (2) D13 (C65 lit les tables 256) n'est visible que par C2.

### B. Faits DLL ou binaire utiles et absents

1. FAIT (binaire) : le chemin de blessure et les sites sont tels que le rapport les dit (`0x80031C44` → remises à zéro `0x80031C5C`/`0x80031C68` (la seconde dans le délai de l'appel `0x80031C64`) → pilote `0x80031C6C` → 0x31/0x3A `0x80031C84`) ; branche verrouillée : remise à zéro de la minuterie (`0x80031BEC`), mode 1 `0x80031BF4` avec le pointeur dans son délai (`0x80031BF8`), annulation `0x80031BFC` ; sortie `+0x188` : annulation `0x80032770` **avant** le mode 1 `0x80032778` (ordre inverse de la branche verrouillée). Le rapport donne les adresses ; il ne signale pas l'inversion.
2. FAIT : précédents pour h1 absents du rapport : `RestoreLifeAndMagic` (`PlayerManager.cs:1909-1921`, opcode 0xC3 : `hero.Hp = hero.HpMax` + `SetPlayerMp` + `HudDirector.SnapDisplayedLifeAndMagic`), `RegenerateLife` (`:1454`, plafonnement `HpMax < life ? HpMax : life`). Le HUD relit `PlayerStats` (`HudDirector.cs:566-567`) : la montée des cœurs est un choix à faire (rouler ou sauter).
3. FAIT : `SpawnEntityBySpriteIndex` peut rendre `null` (table pleine, 63 cases) et sa doc (`IEntityWorldContext.cs:78`) impose que **l'appelant teste** ; chaque pilote de tome, la bombe et les haricots doivent le faire.
4. FAIT : `ScriptedMotion.cs:819` consomme `NoObstacleSlide` ; C75 en pose le bit.
5. FAIT : le pad d'un tick, l'entrée et le front du Rond existent (`PadState.FromTickPad`, `PadState.cs:68-75` ; `TickPad.cs:59`) ; le front de tick est `ButtonsJustPressed & Circle` (comme le rapport).
6. FAIT : tests qui touchent les sprites de h sans être cités : `AlundraBirthTickTests.cs:470-482` (sprite **20** à la naissance, E1 en ligne : neutre) ; `AlundraEntitySlotWorldTests`, `…V5RowsTests`, `…LootRollWorldTests` (sprites 3 et 254, neutres) ; `AlundraBirthBoxTests.cs:131` fait naître les 151 préfabs globaux sans tick (neutre, vérifié).
7. FAIT : `AlundraHeroSwimTablesTests.cs:100-102` épingle `SwimDiveButtons ∌ Circle` (neutre) ; `AlundraHeroRunStatesTests.cs:200` `Sprint_DoesNotCallTryUseItem_…` épingle l'absence d'appel dans `SprintTick` (reste vrai si h0 n'ajoute pas l'appel, ce qui est conforme au binaire).
8. INFÉRENCE : `CameraSway.Step` (`:67-70`) remet Limit, Speed et Offset à 0 quand `Flag == 0` ; un bloc de tremblement restauré par l'annulation du feu 2 avec `Flag == 0` est vidé au `Step` suivant (à mesurer en T0 de h4).
9. FAIT : les 20 traces `W9-item1F..32` (`docs/plan-e20-annexe/e20-d/oracle/out-swim/w9/`) sont des témoins binaires de l'**échec du départ en nage** (héros en 0x1D, `snd3` au tick 10, rien d'autre) ; elles servent h0 et h4a sans nouvel oracle (non chargées par un test).

### C. Tests qui épinglent le comportement et qui sont absents (`rg` vérifié)

Aucun test ne couvre : le front du Rond réel ; le verrou `PlayerControlFlags != 0` (tous bits) ; les échecs de départ (`≥ 0x62`, hors 0x1F-0x32, trous 0x21 0x22 0x28 0x2A) ; le pilote (une fois par tick libre, avant le pad, jamais en branche verrouillée, `BlockedByEntity` ou porte fermée ; `UpdateItemUseDriver` est privé, sans témoin) ; `CancelItemMode` et ses sites ; les états 0x32-0x34 et leurs durées (le rapport a raison : aucun id 0x32-0x34 comme état dans les tests) ; la garde du menu (`AlundraInventoryDirectorTests.cs:208-330` : aucun cas d'objet en cours) ; le coût en PM et le bâton ; la poussée `+0xCC/+0xD0` ; la requête de cibles ; les clés (E,3) (E,13) (E,28) (C,62) (C,63) (C,65) (C,73) (C,75) (D,30) (E,12) (E,21) hors épingles de comptage. Un test de monde avec le Rond n'existe pas ; `ArcRun` suffit (`Give`, `SetItem`, `SetMp`, `Equip`, `Press` : `AlundraArcSupport.cs:151-171,454-463`) mais `ContactHost` ne fait naître rien (`IScriptHost` seul).

## 5. INCONNU tranchés ou restants

- TRANCHÉ (FAIT, binaire) : l'adresse par défaut de la table `0x80022D3C` est `0x80032758`, comme la nage ; états ≥ 0x50 aussi (S1).
- TRANCHÉ (FAIT) : table de nage `0x80022E7C` = `SwimTable` de la DLL (15 états `Skip`, 0x32-0x34 `Float`).
- RESTE INCONNU : sens du bit 0x2000 dans le binaire ; corps de C62, D30, E12, C73, E21 (donc l'effet exact de h3 sur `AlundraLogicalZWorldTests.cs:292,398`) ; placement du sprite 154 dans les cartes ; sons des effets.

## 6. Fichiers (dans `.../scratchpad/disc-h/dll-check/`)

`check.md` (ce rapport), `cites.txt` (364 citations relues), `dump_cites.py`, `read_tables.py`, `mdis.py`, `scan_jal.py`, `evidence_tables.txt`, `evidence_jal_sites.txt`, `evidence_disasm.txt`.
