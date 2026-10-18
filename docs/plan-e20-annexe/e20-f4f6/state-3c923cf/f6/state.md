# E20.f6 (docs/plan-e20-heros.md section 1.22, f6a et f6b) au HEAD e1a68676 : état, lecture seule

Provenance. Branche `chantier/restructuration-dll-r3`, HEAD `e1a68676`. Aucune écriture dans le dépôt, les sous-modules ni `alundra-project/` (seul `python -B` en lecture : `tools/dll-structure/check_names.py --tree --stage final`, `scan_types`). Le premier `git status --short` (avant tout travail de ma part) montrait déjà ` m CasaEngineMonogame` et ` m alundra-datas-analyser`.

- Texte du plan : les sections 1.20, 1.21 et 1.22 sont **identiques octet pour octet** à celles du commit `9ecc7d5` (15 oct. 21:07), vérifié par `diff` (seules les marges changent). Aucune revue depuis. Plan : §1.22 = `docs/plan-e20-heros.md:6054-6262` (f6a :6139, f6b :6168, D-F6-1..10 :6114-6125, « Ordre ferme » :6131, « Corrections induites » :6256).
- Annexes citées par §1.22 : « Découverte : celle de §1.20 » et « Voir §1.20 ». §1.20 renvoie à `docs/plan-e20-annexe/e20-f4f6/` (créée par f4) : **ce dossier n'existe pas au HEAD** (`ls docs/plan-e20-annexe` : aucun `e20-f4f6`). Les preuves (30 scénarios JSON, scripts `run_balls*.py`/`run_flames*.py`, modèles du critique, recensements) sont dans le scratchpad `.../b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20f4f6/{bin,critic,dll,tests}` : présent aujourd'hui, **hors dépôt**, non versionné. Annexes versionnées voisines : `docs/plan-e20-annexe/e20-armes/f-projectiles.md` (découverte du 9 oct., faits sur les boules), `docs/plan-e20-annexe/e20-f3/oracle/tools` (machine, source de copie de f4), `docs/plan-e20-annexe/e20-themes/attacks.md`.
- Restructuration : table `tools/dll-structure/table.json` (base `5434cf6`). `Alundra/Scripts/` n'existe plus. `check_names.py --tree --stage final` au HEAD : « tree: 207 files, 220 top-level types, 0 finding(s), 1 warning(s) ».
- Commits non restructurants sur les fichiers cités depuis `9ecc7d5` (`git log --follow`) : f3b (`5cfac5b8`, `2f16e610`, `48cf43ba` : HitPass, NativeEffects, WorldProxy), e0k1 (`b4f3938e`, `81b68b5f`), e0k2 (`181954f0`, `82a979da`, `2fb42f9c`), e0k3 (`65f20873`, `0d6bd623`, `bef323b4`), e0a5 (`71b209d0`, `0630f054`), f3c (`073de215`, `36e32055`, `f616dc83`, `d56f0b03`), e0b2 (`4753357c`, `b5b2c888`, `a8d1b8d5`), e0a4 (`683b9dcf`, `0ff26e4a`, `bda82292`), R0 (`146e1d81`, catch de WorldProxy).

## 1. Citations traduites

Statuts : OK = même code ; DEPLACE = même code, autre ligne ; FAUX = n'est plus vrai ; ABSENT = n'existe pas au HEAD.

### 1a. Citations explicites de §1.22

| Ancienne | HEAD file:line | Statut |
|---|---|---|
| `AlundraEntityScriptProxy.cs` (f6a périmètre, plan:6139+) | `Alundra/Entities/EntityScriptProxy.cs` (+ partielle `EntityScriptProxy.HitPass.cs`) ; table.json : `AlundraEntityScriptProxy` -> `EntityScriptProxy`, ns `Alundra.Entities` | OK (renommé) |
| `Alundra/Scripts/AlundraBallAi.cs` (nouveau, plan:6174) | n'existe pas ; dossier `Alundra/Scripts/` supprimé ; nom final conforme à ADR-0075 : `Alundra/Ai/Projectiles/BallAi.cs`, `Alundra.Ai.Projectiles.BallAi` (modèle `Alundra/Ai/Projectiles/WaveSwordAi.cs:14`) | ABSENT (à traduire) |
| `AlundraNativeSpriteRegistry.cs` (f6b périmètre) | `Alundra/Ai/NativeSpriteRegistry.cs` ; `CreateForProduction` :36-43 (anc. :29) | DEPLACE |
| `AlundraPlayerManager.cs` (l'ensemble, phase C) | `Alundra/Hero/PlayerManager.cs` ; `ProductionWiredWeaponTypes` :1226 (anc. :1213) | DEPLACE |
| `Slope_18c` (+0x18C) | `EntityScriptProxy.cs:474` (anc. :455) | DEPLACE |
| `UpdateGroundSlope()` | `EntityScriptProxy.cs:1971` (anc. :1907) ; doc :1912-1970 ; unique appelant DLL : :1644 | DEPLACE |
| `RunHeroProbes` | `EntityScriptProxy.cs:1642-1647` (anc. :1578-1583) | DEPLACE |
| `TickMotion` (point d'appel de f6a) | `EntityScriptProxy.cs:1614-1635` (anc. :1550) ; appelé par `TickPhysics` :1602-1612 | DEPLACE |
| « nom du marqueur » | `HasLogicalZ` `EntityScriptProxy.cs:1383` (doc :1376-1382) ; posé `Alundra/Maps/WorldProxy.cs:3573` ; absent à `9ecc7d5` (créé par e0k2) | NOUVEAU depuis le plan (livré) |
| `UpdateVramFlags` | `EntityScriptProxy.cs:2044` (anc. :1980) | DEPLACE |
| choix du programme, `DestroyEntity(e, 6)` | `EntityScriptProxy.cs:1708-1711` (`DestroyOnSlidingSlope` et `Slope_18c == 4` -> `host.DestroyEntity(this, 6)`), anc. :1644 ; `Alundra/Maps/IScriptHost.cs:48` ; `Alundra/Maps/WorldProxy.cs:3301` | DEPLACE |
| `Emit` | `Alundra/Rendering/Effects/EffectStage.cs:192` (anc. `AlundraEffectStage.cs:190`) ; application dans `RunFrame` :230-250 avant `_reservoir.Tick` :243 | DEPLACE |
| `CreateHitSparks` (modèle des créateurs) | `Alundra/Rendering/Effects/NativeEffects.cs:200` (anc. :190) ; `reservoir.Create` `EffectReservoir.cs:101` ; 128 emplacements `EffectReservoir.cs:25` | DEPLACE |
| `FrameCounter` (+0x1C) | `EntityScriptProxy.cs:98` ; incrément `WorldProxy.cs:3009` | OK |
| `Flags` (+0x6C), `TargetAnimationId` (+0x88), `ForceResetAnimationFlag` | `EntityScriptProxy.cs:156`, `:158`, `:167` (le décalage +0xAC de ce dernier n'est pas annoté dans le code : INFERENCE) | OK |
| « crochet de test » | `PlayerManager.WeaponSpawnBirthHookForTests` `PlayerManager.cs:1271` (anc. :1258) ; remplace l'appel de production, `SpawnWeaponProjectile` :1199, appel :1210-1219 | DEPLACE |
| « registre du runner de test » | `EventProgramRunner.NativeSprites` `Alundra/Events/EventProgramRunner.cs:51` ; `NativeSpriteRegistry.Register` :54 ; exception sur clé prise :60-63 | OK |
| `HitArena` / `ArcRun` (P-F6-2) | `Alundra.Tests/AlundraHitArena.cs:70` (`NativeHandlersEnabled = false`, inchangé) ; `ArcRun` `Alundra.Tests/AlundraArcSupport.cs:94` (anc. :84) | OK / DEPLACE |
| effets d'attache 4 et 5 (D-F6-7, f3b) | `Alundra/Combat/HitPass.cs:54` (`EffectOfNibble`), :88 (`attachEffect`), :152-160 ; `NativeEffects.cs:182` ; `WorldProxy.cs:3093` | OK (livré) |
| fiches d'arme w4, w5 | `Alundra/Items/WeaponTables.cs` : `Records`, enregistrements w4 et w5 (5e et 6e lignes du tableau) | OK |
| `fire-m303`, `arsenal-a-m14` (recette) | `Alundra/SaveGames/TestSaves.cs:124` (arsenal-a-m14), :128 (fire-m303) ; `Alundra.Tests/AlundraTestSavesTests.cs:67`, :372, :376 ; `docs/test-saves.md:77`, :81 | OK |
| opcode `0x87 [128, 6]` (p2) | `EventProgramRunner.cs:799` ; test existant `Alundra.Tests/AlundraFireSwitchRegressionTests.cs` (cartes 303, programme D code 700) | OK |

### 1b. Citations héritées par renvoi (« Voir §1.20 », D-F5-5)

| Ancienne | HEAD | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:336-342` (E0, E1 en ligne) | `EventProgramRunner.cs:283-292` ; `IsInlineDeactivateCode` :328 = `code <= 1 || code == WaveSwordAi.DeactivateCode` (E10 ajouté par f3c) | DEPLACE + complété |
| « le registre a 6 clés » | 7 clés : `BeannoideAi.Register` (A1, C1, D1), `MurutaAi.Register` (A0, C10, D9) `Alundra/Ai/Enemies/*.cs:30-35`, `:44-49` ; `WaveSwordAi.Register` (C61) `WaveSwordAi.cs:30` | FAUX (7) |
| `AlundraNativeSpriteRegistryTests.cs:51` | `:55` (`Assert.Equal(6 + 1, runner.NativeSprites.Count)`) **et** `AlundraWaveSwordLifeTests.cs:181-183` (`3 + 3 + 1`) | DEPLACE + 2e épingle |
| « `ProductionWiredWeaponTypes` est vide » | `{ 27, 28, 29 }` `PlayerManager.cs:1226` (f3c) | FAUX |
| S5 `S5_...` (4 clauses qui basculent) | `Alundra.Tests/AlundraWeaponSpawnSeamTests.cs:238-259` (anc. :239-251), réécrit par f3c : :243 égalité du jeu à {27,28,29} ; :247 `Contains((E,17))` ; :251 `Register(E,17,...)` ; :256 `Contains((C,94))` pour 7 | DEPLACE + modifié |
| `UnhandledWeaponSpriteCodes` | `PlayerManager.cs:1240` (anc. :1227) | DEPLACE |
| `ResolveSpriteType` | `Alundra/Entities/Spawning/EntityRecordMapper.cs:227-238` (`SpriteTableIndex`, `\| 0x100` si `SpriteDirection & 0x80`) | DEPLACE |
| `AlundraHitPass.AxisOverlaps` | `Alundra/Combat/HitPass.cs:63` (anc. :54) | DEPLACE |
| `AlundraHitPass.cs:109-131` (corps de la cible) | `HitPass.cs:134-144` (corps `Pos + Mod`, trois `AxisOverlaps` :142-144) ; le bloc a glissé de 25 lignes (f3b) | DEPLACE |
| `StoreHitBox` | `EntityScriptProxy.HitPass.cs:87` (anc. :84) ; appel `EntityScriptProxy.cs:1610` | DEPLACE |
| `ResyncControllerFromFlags`, `WalkabilityMaskFor` | `EntityScriptProxy.cs:670` ; `Alundra/Physics/CellsCollisionField.cs:147` | OK |
| `TickScriptedNpc` (clé de vitesse, D-F5-5) | `Alundra/Physics/ScriptedMotion.cs:171-176` (règle : `JoinsBirthTick && CurrentAnimationId == ~0u ? Target : Current`) | DEPLACE |
| `SyncAnimation` | `Alundra/Maps/Loop/FrameSyncPasses.cs:90` ; `IsZForceApplied = 0` :242 | DEPLACE |
| épingle P3 d'e0k1 | `Alundra.Tests/AlundraMapStateTests.cs:150` ; sa soeur `AlundraRecordBirthTickTests.cs:364` (`B6_R_...`) | OK |
| `DamagedTickCounter` « écrit seulement par 0x86 » | écriture `EventProgramRunner.cs:793` ; décrément du passage des compteurs `WorldProxy.cs:3012` ; copie du clone `EntityScriptProxy.cs:2682` | OK nuancé |
| D-F4-7 `NativeSpriteTypeFilterForTests` | n'existe pas (`rg` sans résultat dans `Alundra`, `Alundra.Tests`, `tools`) : f4 non faite | ABSENT (attendu) |

## 2. Affirmations sur la DLL : vraies au HEAD ?

| # | Affirmation (plan) | Verdict | Preuve |
|---|---|---|---|
| 1 | `UpdateGroundSlope` ne tourne que par `RunHeroProbes` (plan:6097) | VRAI | `rg UpdateGroundSlope(` : appel unique `EntityScriptProxy.cs:1644` ; le reste = 6 appels directs dans `AlundraGroundSlopeTests.cs:178-500` |
| 2 | « L'égalité vaut pour un Z logique au repos » | VRAI par lecture, INFERENCE à confirmer par la sonde de T0 | `ScriptedMotion.cs:495` (`PosZ = landingHeight - ModZ`, `landingHeight` = terrain ou dessus d'une entité) ; `TerrainProbe.cs:51` (`height == moddedPosZ`) |
| 3 | « 14 sprites locaux (444 instances de carte) et 12 globaux portent `DestroyOnSlidingSlope` » | 14 et 12 VRAIS ; « 444 instances » MAL LIBELLE | `alundra-project/Data/sprite-records.json` : 12 globaux (1, 2, 10..17, 23, 234) dont 17 (`0x132014`) et 23 (`0x102074`) **sans** bit gravité d'en-tête ; 14 locaux (34..38, 48, 107, 111..113, 134, 137, 174, 182). 444 = somme des « maps= » du recensement (nombre de couples carte/sprite) ; **instances placées** dans les `.tmj` de `alundra-project/Maps` : 2 276 (mon comptage, filtre `SpriteDirection & 0x80`) |
| 4 | `CreateForProduction` appelle les classes d'IA une à une | VRAI | `NativeSpriteRegistry.cs:36-43` |
| 5 | Enregistrer C60 pour 12 rend les flammes de la 309 vivantes en production | VRAI par lecture | données : `.tmj` entités 6, 7, 8 = sprite 12, `SpriteDirection` 65 (global), `EventCodesC_TickIndex 0` ; `RunPickedEvent` -> `RunSpriteEvent` si l'index scripté vaut 0 (`EntityScriptProxy.cs:1783-1786`) ; registre consulté `EventProgramRunner.cs:296-306`, `NativeHandlersEnabled` vrai par défaut :56. Atteignabilité de la 309 en jeu/`ArcRun` : INCONNU (P-F6-2) |
| 6 | Programme A des flammes = `1A 02`, `63 80 F0 00` | VRAI | `events.json` de la 309 : `EventCodesATable[7] = 76`, `Codes[76..] = 26,2,99,128,240,0,255` |
| 7 | Types 10 à 15 : drapeaux `0x132154`, codes A0, C60, E9 ; corps 21×15×15 (12) et 21×15×16 (15) | VRAI | `sprite-records.json` (bank `alundra_10..15`) : flags `0x132154`, `ProgramTick 60`, `ProgramDeactivate 9`, `ProgramLoad 0`, tailles (21,15,8) sauf 12 : ×15, 15 : ×16, `Contents 0`, `BreakEffect 0` |
| 8 | Fiches 134/4, 134/12, 135/4, 135/12 ; w4/w5 : types 11-12 et 14-15, délai 10, z 32 ; 10 et 13 sans fiche | VRAI | `balance.json` (AttackAttribute 134/135, Power 4/12) ; `WeaponTables.Records` (octets `0B 0A 20`, `0C`, `0E`, `0F`). Nuance : les fiches 11 et 12 ont 5 attaques (`AttackForAnimation` : `Attacks[anim+1]`, `BalanceRecord.cs:58-69`), la 5e (attribut 6, puissance 1) n'est pas citée |
| 9 | Pas d'impulsion (l'octet est effacé à chaque appel) | VRAI | `FrameSyncPasses.cs:242` |
| 10 | Naissance : aucun tirage (`Contents 0`) | VRAI | `LootRoll.cs` : code 0 -> aucun tirage ; `WorldProxy.cs:3566` `RollLootAtCreation(..., hasRecord: false, 0)` ; en-têtes `Contents 0` |
| 11 | Z DLL = Z oracle − 1 unité brute | VRAI | `WorldProxy.cs` (création : `unchecked(z + 1)`) ; ADR-0026 ; `NativeEffects.cs` (doc de `CreateBreakEffect`) |
| 12 | Tirages des effets à l'application (D-E0B-3a) | VRAI | `EffectStage.cs:192`, `:232-250` |
| 13 | Le héros n'a pas de cas pour la pente 1 | VRAI | `PlayerManager.cs:372` (pente 4), :394 (pente 6) seulement |
| 14 | « inerte en production : aucune entité marquée à gravité et à `DestroyOnSlidingSlope` n'existe » (f6a) | VRAI au HEAD, **faux après g1a** | seule naissance par l'interface en production = 27-29 (`PlayerManager.cs:1218`), drapeaux `0x205c` (ni `0x100` ni `0x100000`) ; g1a pose `HasLogicalZ` sur des caisses `0x3323E4` (bloc 1.24) |
| 15 | f6a ne nourrit que `+0x18C` ; lecteurs de `+0x180/+0x184/+0x190` à recenser en T0 | recensement partiel fait ici | `CombinedVramFlagsOR` lu : `EntityScriptProxy.cs:1713` (`DestroyOnVramFlags`, toutes entités), `ScriptedMotion.cs:302` et :650 (héros), `PlayerManager.cs:963`, :1536 (héros), `PortalTrigger.cs:68` (AND, héros) ; non nourri hors héros (`UpdateVramFlags` appelé par `RunHeroProbes` seul) |
| 16 | Prérequis e0k1, e0k2, e0k3, f3b, e0b1 faits | VRAI | commits listés en tête ; marqueur :1383 ; clamp de carte ; `AttachHitEffect` ; `AdvanceRandomPerTick` |
| 17 | « Réponses de l'auteur : aucune » ; Q-K1 option A | PERIME | Q-K1 et Q-K2 confirmées par l'auteur le 2026-10-16 (D-E20-37, D-E20-38, `docs/plan-e20-heros.md:169-172`) |
| 18 | Effets globaux 17, 18, 25, 6 existent | VRAI | `Data/effects/effects-global.json` : 29 effets, indices 6, 17, 18, 25 présents |
| 19 | « Z de l'explosion/arène R = 101 » | NON ETABLI | voir Manques M4 |

Rappel `PickEventTrigger` : `Status` Normal seulement (:1703-1711) ; une boule en statut 3 (Deactivated) retombe, la pente doit donc être mise à jour aussi en statut 3 (`EntityStatus.IsActive()` = Normal ou Deactivated, `EntityStatusExtensions.cs`), sinon `fire11_wall_then_water` (branche A d'E9) échoue.

## 3. Tests qui épinglent ce que la tranche remplace (au-delà des listes du plan)

Épingles du registre (phase B de f6b, +2 clés) : `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:55` et `Alundra.Tests/AlundraWaveSwordLifeTests.cs:181-183` (`V3_TheProductionRegistryHoldsSevenKeys_...`). Le plan §1.22 (m3) dit seulement « l'épingle du registre » (singulier) ; le bloc 1.24 (plan:6392) en compte **deux**.

Épingles du jeu câblé (phase C) : `AlundraWeaponSpawnSeamTests.cs:243` (égalité à {27,28,29}), :244 (`Assert.Empty(Unhandled...(Production))`, doit rester vide avec {11,12,14,15}), :247, :251, :256 (clauses f4/f5). `AlundraWaveSwordWiringTests.cs:244-292` (P5 : témoin du tir de l'arc sous l'ensemble explicite {27,28,29}, indépendant du changement) et :64 (arène de charge : relâché à 91, S = 112).

Tests qui font vivre des entités de types 2 ou 11 par l'interface sur des images complètes, **natives allumées** (`NativeHandlersEnabled` non touché), donc candidats à bouger à la phase B (C60 et E9 deviennent actifs, E9 tire 10 mots et change statut, drapeaux et position après S+11) et à vérifier à la phase A de f6a (pente nourrie) : `Alundra.Tests/AlundraLogicalZWorldTests.cs` (`FireBall` :211, :238, :322, :369, :435, :518, :562, :570 ; `Bomb` :292, :398), `Alundra.Tests/AlundraMapStateWorldTests.cs` (`Fly(FireBall...)` :140-141, :154-155 ; boucle `arc.OneFrame()` :66). Effet à mesurer en T0 (INFERENCE : les cases `FlatCells` ont `ground_property` 0, donc pente 0 pour f6a). Hors de cause : `AlundraLogicalZStepTests.cs` (appelle `RunLogicalZPreXyStep` à la main, pas `TickPhysics`), `AlundraSpawnBySpriteIndexInterfaceTests`, `AlundraBirthBoxTests` (spawn puis recyclage sans tick).

Tests qui pincent la mort par pente 4 (restent verts, servent de positifs accolés à (s3)) : `AlundraDestroyEntityBreakEffectTests.cs:248` (`ThePickPhase_CallsTheSlopeAndTheVramDestruction_WithTheirEffects`, pente posée à la main) et `AlundraWorldProxyEventPassTests.cs:285`. Tests de la règle de pente du héros (non touchés) : `AlundraGroundSlopeTests.cs` (6 appels directs), `AlundraTickStructureTests.cs:217-230`.

Épingles de la clé de vitesse (D-F5-5, prérequis de f6b) : `AlundraMapStateTests.cs:150` (P3) et `AlundraRecordBirthTickTests.cs:364` (B6_R) ; `ScriptedMotion.cs:175` est la ligne modifiée par f5 puis étendue par g2a.

Tests de la recette de f6b : `AlundraTestSavesTests.cs:67` (liste exacte des préréglages ; `arsenal-a-m14` et `fire-m303` existent déjà, aucun préréglage à ajouter) ; `AlundraFireSwitchRegressionTests.cs` (l'épée n'allume pas l'interrupteur de feu de la 303 : à accoler au positif (p2)).

Aide de test pour (s1) : `FlatCells.Create(..., groundProperty: ...)` `Alundra.Tests/AlundraJumpTestSupport.cs:451-` (le paramètre `groundProperty` donne la pente : `0x08` -> 4, `0x02` -> 1, `0x04` -> 2, `0x06` -> 3 via `((gp << 8) & 0xE00) >> 9`, `TerrainProbe.cs:54` et `EntityScriptProxy.cs:1997`). Piège : la doc de `FlatCells` appelle « 0x08 water » le bit de **marchabilité** (`CombinedVramFlagsOR & 8`), qui est autre chose.

## 4. Conséquences des tranches suivantes

Ordre (textes du plan) : e0c (1.26) puis E20.e (1.27, « exécutable après E20.e0c ») ; §1.25 propose e **avant** f4 ; §1.20 dit que e ferme le bouchon après f4 (contradiction hors f6) ; ordre ferme f4 -> f5 -> f6a -> f6b (plan:6131) ; g1a, g2a, g2b, g3, g1b après (bloc 1.24, point 3) ; f6a est prérequis nommé de g1a ((w8) : caisse relâchée dans l'eau profonde détruite par le choix du programme), de g2b ((b6) : effet 6 au lieu du 10) et de h3/h4 ; f6b n'est prérequis d'aucune tranche g.

Clés du registre de production : 7 aujourd'hui ; f4 +1 (C,94), f5 +1 (E,17), f6b +2 (C,60) (E,9), e0c +2 (C,255) (A,255), g1a +1 (C,23), g2b +1 (E,2) : 15 au total, **ordre libre** ; chaque tranche met les deux épingles dans sa liste prédite et les réécrit par dérivation (bloc 1.24 point 4, plan:6392-6397). Aucune clé en double entre elles (vérifié : (C,60) et (E,9) libres ; (D,9) du Muruta est un autre slot).

Fichiers partagés : `EntityScriptProxy.cs` (f6a appel dans `TickMotion`, doc périmée :1956-1962 ; g1a `PushLogicalPositionToRoot` :2375/:2380, doc du marqueur :1376-1383 ; e0c accès 32 bits) ; `NativeSpriteRegistry.cs` (f4, f5, f6b, e0c, g1a, g2b ; docs :29 et :35 à tenir à jour) ; `PlayerManager.cs:1226` (f4, f5, f6b) ; `ScriptedMotion.cs:175` (f5 puis g2a, qui lit `HasLogicalZ`) ; `EventProgramRunner.cs` (f4 : interrupteur D-F4-7 près de :56/:301 ; g2b : doc :47-50) ; `NativeEffects.cs` (créateurs de f4 et de f6b si placés là).

Appel du bouchon d'herbe par C94 : propriété de f4 (D-F4-3), pas de f6. C60 et E9 n'appellent pas `CheckAndTriggerTileEffect` (quatre appelants au binaire : héros, C61, C94, E12, plan:6843 et :7062). Bouchons actuels : `Alundra/Ai/Projectiles/WaveSwordAi.cs:34-40` et `Alundra/Hero/PlayerAnimationEffects.cs:218` ; E20.e (§1.27) ne remplace que ces deux : un troisième appelant (C94) ajouté par f4 avant e serait hors de son périmètre. Aucune action pour f6, sauf : les tests de f6b sur la carte 14 ne doivent attendre aucune coupe (la boule ne coupe pas).

Règle d'inscription des tests (bloc 1.24 point 4) : un test qui inscrit une clé dans le registre du runner le fait **seulement si la clé est absente**, par l'`IDisposable` que rend `Register` (`NativeSpriteRegistry.cs:54`, :84), libéré en fin de test. **f6b en a besoin** : T1 « les tests enregistrent les clés eux-mêmes » est correct avant T3 et lève `InvalidOperationException` après T3, car `CreateForProduction` inscrira alors (C,60) et (E,9) (`NativeSpriteRegistry.cs:60-63`). Le plan §1.22 n'énonce pas la règle ; même défaut dans f4 (`AlundraWeaponSpawnSeamTests.cs:251` inscrit (E,17) dans le registre de production).

Garde des fichiers neufs (Q-R-7, `docs/plan-restructuration-dll.md:1234`, encore « proposition ») : `check_names.py:96-98` émet `[hors table] file ... is not in the table` pour tout `.cs` ajouté sous `Alundra/`. `Alundra/Ai/Projectiles/BallAi.cs` (f6b) la déclenche. f6a n'ajoute aucun fichier de DLL. Les fichiers de test neufs ne sont pas scannés (`--tree` ne lit que `Alundra/` : 207 fichiers = 205 + 2). Le bloc 1.24 et §1.27 mettent Q-R-7 en prérequis ; §1.22 ne le dit pas.

Effets de e0c sur f6 : `DestroyEntity` (`WorldProxy.cs:3301`) branchera `SpawnContents` ; la boule détruite dans l'eau a `Contents 0` : (s3) « aucun tirage » doit se lire avec e0c (à confirmer par le plan e0c : la ligne 1.26 ne traite que `ContentsItemId = 69`). Effets de g1a : `HasLogicalZ` sera aussi posé par `PutInHands` sur des entités nées d'un enregistrement ; l'appel de f6a (porté par le marqueur) les nourrit alors (voulu par g1a/g2b), et la phrase « inerte en production » de f6a devient fausse.

## 5. Noms nouveaux proposés par la tranche (règles ADR-0075)

| Plan | Nom final conforme | Contrôle |
|---|---|---|
| `AlundraBallAi` (`Alundra/Scripts/AlundraBallAi.cs`) | `BallAi`, `Alundra/Ai/Projectiles/BallAi.cs`, `namespace Alundra.Ai.Projectiles`, `internal static class` ; constantes du modèle `WaveSwordAi.cs:17-23` (`SpriteTypes` = 10..15, `TickCode` = 60, `DeactivateCode` = 9), `Register(NativeSpriteRegistry)` | `BallAi` : aucune collision (moteur, MGUI, NvgSharp, paquets, BCL, DLL) ni segment d'espace de noms (script `scan_types` : `BallAi`, `ChargedArrowAi`, `ArrowAi`, `LiftableAi`, `CarryRules` libres) ; fichier hors table (Q-R-7) |
| « trois créateurs » (traînée 17/18, cinq effets d'explosion, bouffée) | non nommés par le plan ; emplacement non dit (`BallAi.cs` ou `Alundra/Rendering/Effects/NativeEffects.cs`) | à nommer ; si `NativeEffects.cs`, il manque au périmètre |
| gestionnaires C60 / E9 | non nommés (modèles : `WaveSwordAi.Tick`, `BeannoideAi.Tick`) | à nommer |
| interrupteur d'arène | `EventProgramRunner.NativeSpriteTypeFilterForTests` (D-F4-7), membre, hérité de f4 | n'existe pas encore |
| fichiers de test | « `Alundra.Tests/` » sans nom ; règle : à plat `Alundra.Tests/Alundra<Sujet>Tests.cs` (R5 non passée, bloc 1.24 point 1) ; exemples non écrits dans le plan : `AlundraBallSlopeTests.cs` (f6a), `AlundraBallLifeTests.cs`, `AlundraBallFlamesTests.cs` (f6b) | propositions, non plan |
| annexe | `docs/plan-e20-annexe/e20-f4f6/` (créée par f4) | absente |
| ADR | « prochain numéro libre, un ADR commun à f4, f5, f6 » ; dernier ADR parent : 0075 | numéro à relever à l'écriture |

## 6. Manques bloquants (pour exécuter §1.22 tel qu'écrit)

Bloquants
- M1. Piège d'inscription : T1 de f6b inscrit (C,60)/(E,9) dans le registre du runner de test ; T3 les inscrit en production ; `Register` lève (`NativeSpriteRegistry.cs:60-63`) -> T1 rouge après T3 alors que T3 attend « Verts ». La règle « inscrire seulement si absente, par l'`IDisposable` » (bloc 1.24 point 4) n'est pas dans §1.22.
- M2. Liste prédite incomplète et jamais recomptée : l'acceptation de f6b dit « tests déplacés ⊂ liste prédite (registre, S5 et témoins si f5 n'est pas faite) ». Elle ne nomme ni les **deux** épingles du registre, ni les tests monde à boules/bombes (AlundraLogicalZWorldTests, AlundraMapStateWorldTests, section 3), et le T0 de f6b n'a **aucune** tâche « liste prédite recomptée au HEAD de départ » (f4 en a une, f6a et f6b non). L'arrêt « un test hors de la liste prédite bouge » se déclencherait.
- M3. Fichier neuf sans garde : `BallAi.cs` fait échouer `check_names.py --tree --stage final` (`check_names.py:96-98`) ; Q-R-7 (R7) n'est ni validée ni planifiée. Aucun arrêt de f6b ne la mentionne.
- M4. « 112 pour R = 101 de l'arène » (plan:6077, :6220 (p1)) est incohérent : l'arène de charge relâche à **91** (oracle 96) (`AlundraWaveSwordSupport.cs:18`, `AlundraWaveSwordWiringTests.cs:64`, `docs/plan-e20-annexe/e20-f3/README.md:30`) ; l'oracle des boules a R = 96, S = 107 (`run_balls.py:2`, scratchpad) ; S = R + 11 donne 102 avec l'arène actuelle. 112 est le S de la vague de l'épée (91 + 21). Aucune annexe versionnée ne porte R = 101.
- M5. Conditionnelles caduques : « (P-F6-3) position de l'explosion à 1, 2, 3 ticks par image **avant la règle de vitesse** » (T0) suppose f5 non faite, alors que l'ordre ferme met D-F5-5 avant f6b ; « S5 et témoins si f5 n'est pas faite » idem (le plan dit lui-même que ces mentions sont sans objet, plan:6131-6136). La sonde est inexécutable telle quelle sans retirer la ligne de f5.
- M6. Preuves non versionnées : `docs/plan-e20-annexe/e20-f4f6/` n'existe pas ; les 30 scénarios (`fire11_*`, `fire12_*`, `ice14_*`, `ice15_*`), le modèle des tirages d'E9, les 22 mutants, les recensements ne sont que dans le scratchpad. Le T0 de f4 ne dit de copier que `e20-f3/oracle/tools` et d'écrire « un script par tranche » ; rien n'importe `disc-e20f4f6/bin/*.py` ni `bin/out/json`.
- M7. Créateurs sans nom ni propriétaire de fichier (section 5) : « trois créateurs » en phase A, aucun fichier d'`NativeEffects.cs` dans le périmètre.

Non bloquants (à corriger au passage)
- M8. Toutes les citations de §1.20 à §1.22 sont en noms d'avant restructuration (`AlundraX.cs`, `Alundra/Scripts/`) ; le bloc « Révision au HEAD » ne traduit que 1.24.
- M9. Acceptation « suite en Release et en Debug » sans « copie neutralisée » ni « Debug en dernier » (§1.27 les dit ; règle de mémoire : une Release sans copie neutralisée écrase la DLL Debug).
- M10. `Réponses de l'auteur : aucune` et « Q-K1 option A » : confirmées par l'auteur (D-E20-37/38).
- M11. f6a T0 : le recensement des lecteurs de `+0x180/+0x184/+0x190` et des types `0x200000` (plan:6141-6142, « Ajout de la révision 9 ») n'est pas une tâche de T0 ; le bloc 1.24 (O-G-2, D-G-14) attend que le T0 d'f6a « nomme leur propriétaire », sinon g2b les prend. f6a est écrite « ne nourrit que `+0x18C` » tandis que le bloc 1.24 rattache la branche morte `+0x184 & 4` d'E2 « à f6a » (ambigu).
- M12. f6a : prédiction écrite de la sonde de T0 (« `Slope_18c` d'une boule marquée posée sur chaque pente ») absente (valeurs implicites 4/1/2/3/0 de (s1)) ; la portée du statut de l'appel (Normal et Deactivated) n'est pas dite ; « case 0x08 » = `groundProperty` de `FlatCells`, pas la marchabilité.
- M13. (p4) « l'ensemble de production égale les octets de type non nuls des fiches d'arme » : prédicat non nommé ; `WeaponTables` n'expose pas d'énumération publique des octets de type (les types non nuls : 27, 28, 29, 11, 12, 14, 15, 3, 4, 7 = exactement l'ensemble final f3c+f4+f5+f6b, vérifié sur `WeaponTables.Records`).
- M14. « 444 instances » (plan:6097) : voir section 2, ligne 3.
- M15. Doc périmée à tenir à jour par f6a : `EntityScriptProxy.cs:1956-1962` (« called for the PLAYER ONLY ... Every NPC's Slope_18c stays 0 », et cite une branche `Update`/`IsPlayer` qui n'existe plus), `PlayerManager.cs:72-73`, `CellsCollisionField.cs:196-198`.
- M16. Docs des clés de production à tenir à jour par f6b : `NativeSpriteRegistry.cs:29`, `:35`, `EventProgramRunner.cs:47-50`.

## 7. Inconnus

- La 309 se construit-elle sous `ArcRun` / le monde de test et ses flammes tournent-elles (P-F6-2) : aucun test existant ne charge la carte 309 (`rg 309` ne trouve que des constantes sans rapport).
- Valeur de R pour les baguettes dans l'arène de la DLL (91 ? 101 ?) et donc S des charges : à mesurer en T0.
- Effet réel de la phase B sur `AlundraLogicalZWorldTests` et `AlundraMapStateWorldTests` (tests non lancés : interdit ici).
- Chronologie de la fin d'animation de l'explosion (`+0xAC`, S+47/S+43/S+67) en DLL à 1, 2 et 3 ticks par image : le bloc 1.24 (g2b) note que `+0xAC` vient d'un événement de fin d'animation du moteur (`Alundra/Entities/Spawning/EntitySpawnFactory.cs:308-325`), non vérifié ici pour les boules.
- Comportement de `SpawnContents` (e0c) pour l'objet 0 : tirage ou non.
- Les chiffres de recensement (pente 1 : 8 146 cases/49+ cartes ; pente 4 : 10 644/54+ ; 22 mutants ; 30 scénarios ; toutes les valeurs [oracle]) viennent du scratchpad et n'ont pas été rejoués.
- Nombre de tests de la suite au HEAD (non mesuré, exécution interdite).
- Ordre final e / f4 (contradiction entre §1.20, qui laisse le bouchon à e, et §1.25/§1.27, qui placent e avant f4) : sans effet sur f6, à trancher ailleurs.
