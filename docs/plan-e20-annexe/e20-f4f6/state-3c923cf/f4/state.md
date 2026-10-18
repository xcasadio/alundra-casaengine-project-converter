# E20.f4 (plan §1.20) : état au HEAD `e1a68676`, branche `chantier/restructuration-dll-r3` (2026-10-18, lecture seule)

## 0. Provenance et méthode

- Section lue : `docs/plan-e20-heros.md:5709-5892` (§1.20 : en-tête, faits porteurs, écarts tranchés, D-F4-1 à D-F4-10 en `:5773-5790` ; §1.20.1 `:5799-5890`). Introduite par `f8f9f09e` (2026-10-15 21:01). Depuis, un seul changement de fond sur la section : l'ordre ferme f4, f5, f6a, f6b (`6a5465d5`, `git diff f8f9f09e HEAD` sur la section) ; les autres ajouts du plan sont ailleurs.
- « Ancienne » base de lignes : `9ecc7d5` (DLL = f3a phases A et B faites ; les deux citations ligne-à-ligne du plan, `AlundraEventProgramRunner.cs:336-342` et `AlundraNativeSpriteRegistryTests.cs:51`, s'y lisent exactement au bon endroit : vérifié par `git show 9ecc7d5:...`). Pour ce que f3c a créé après (`WaveSwordAi`), la base est `5434cf6` (base de `tools/dll-structure/table.json`, fin de R0).
- Commits qui touchent les fichiers cités depuis `be9a067e`, hors `refactor(dll)` (R2/R3/R4) : f3a-B `1b01353e` ; f3b `5cfac5b8`, `2f16e610` ; e0k1 `b4f3938e`, `81b68b5f` ; e0k2 `181954f0`, `82a979da` ; e0k3 `65f20873`, `bef323b4` ; e0a5 `71b209d0`, `0630f054` ; e0b2 `4753357c`, `b5b2c888` ; f3c `073de215`, `f616dc83` ; e0a4 `683b9dcf`, `0ff26e4a`, `bda82292` ; R0 `146e1d81`. Aucun commit DLL après R4 (`15804ae3`, 2026-10-17) : `git log` sur `Alundra` et `Alundra.Tests`.
- Annexes de preuve de la section : (a) `docs/plan-e20-annexe/e20-f4f6/` : **absente** du dépôt (créée par le T0 d'f4, `:5838`) ; (b) `docs/plan-e20-annexe/e20-f3/oracle/tools` : présente, 56 fichiers, `oracle/checksums.txt` 66 lignes ; (c) le scratchpad de la session `.../b00d1a72-.../scratchpad/disc-e20f4f6/` (`bin/`, `dll/`, `tests/`, `critic/`) : **présent sur cette machine**, hors dépôt, et c'est le seul endroit où existent `bin/out/json/charged7_*.json` (9 fichiers : `charged7_east_wall`, `_free_{west,north,south}`, `_grass`, `_map_edge`, `_step16`, `_step32`, `_vs_target`), le harnais `bin/fw.py` et `bin/run_charged.py`. Aucune de ces clés n'est lisible depuis le dépôt aujourd'hui.
- Vérifié moi-même dans les données : `alundra-project/Data/sprite-records.json`, clé `alundra_7` : `ProgramLoad 0, ProgramTick 94, ProgramDeactivate 1, BreakEffect 8, Contents 0`, corps `(-10,-7,0)` de `21x15x8`, `AnimSets[0]` `Sfx 33` ; c'est le **seul** en-tête des 395 dont `ProgramTick == 94`. `Data/effects/effects-global.json` : l'effet global 25 (0x19) a 4 animations, l'effet 8 existe. Le recensement du scratchpad (`bin/out/census_global_page_records.txt`, 9 741 enregistrements) ne montre aucun enregistrement d'indice 7 (seulement 12, 2, 16).
- Aucun fichier du dépôt n'a été écrit. `git status` est identique au départ. Preuves faites dans le scratchpad `f-state/f4/` (`tree-proof/` : copie `git archive` du HEAD plus un fichier ajouté).

## 1. Citations traduites

Chemins : `tools/dll-structure/table.json` (`Alundra/Scripts/X.cs` vers le chemin final), puis `rg`. Colonne « ancienne » : citation du plan, puis ligne à `9ecc7d5` si elle diffère.

| Ancienne (plan) | HEAD file:line | Statut |
|---|---|---|
| `AlundraEventProgramRunner.cs:336-342` (E0/E1 en ligne, `:5756`) | `Alundra/Events/EventProgramRunner.cs:286-292` (bloc `if (entity.EventTrigger == ProgramEDeactivate && ... IsInlineDeactivateCode(...)) { DestroyEntity(entity, -1); return; }`) | TROUVÉ, même code. `IsInlineDeactivateCode` est en `:328` et vaut `(uint)code <= 1u \|\| code == WaveSwordAi.DeactivateCode` (E10 ajouté par f3c ; ancienne ligne `:378`, `<= 1u` seul) |
| `AlundraNativeSpriteRegistryTests.cs:51` (épingle, `:5757`) | `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:55` (`Assert.Equal(6 + 1, runner.NativeSprites.Count)` ; nom du test `:51` : `...HoldsTheSevenHandlers...`) | CHANGÉ : l'assertion était `Assert.Equal(6, ...)` à `:51` (9ecc7d5). f3c l'a passée à `6 + 1` |
| (non citée) 2e épingle du registre | `Alundra.Tests/AlundraWaveSwordLifeTests.cs:181-183` (`(3 + 3 + 1, ...)` et `registry.Count`, test `V3_TheProductionRegistryHoldsSevenKeys_...` `:166-189`) | NOUVEAU depuis f3c ; absent du plan §1.20 |
| `AlundraNativeSpriteRegistry.cs` (périmètre) | `Alundra/Ai/NativeSpriteRegistry.cs` : `CreateForProduction` `:36-43`, `Register` `:54-67` (doublon : `InvalidOperationException` `:60-63`), `TryGet` `:70`, `Registration` (`IDisposable`) `:84-99`, docs du contenu `:29` et `:35` | TROUVÉ (table : `AlundraNativeSpriteRegistry` vers `NativeSpriteRegistry`) |
| `AlundraEventProgramRunner.cs` (périmètre) | `Alundra/Events/EventProgramRunner.cs` (`NativeSprites` `:51`, `NativeHandlersEnabled` `:56`, lecture `:300-301`, doc du registre `:47-50`) | TROUVÉ (`AlundraEventProgramRunner` vers `EventProgramRunner`) |
| `AlundraPlayerManager.cs` (périmètre) | `Alundra/Hero/PlayerManager.cs` | TROUVÉ (`AlundraPlayerManager` vers `PlayerManager`) |
| `Alundra/Scripts/AlundraChargedArrowAi.cs` (nouveau) | n'existe pas. `Alundra/Scripts/` n'existe plus. Chemin final conforme : `Alundra/Ai/Projectiles/ChargedArrowAi.cs` | À CORRIGER dans le plan |
| `AlundraEventProgramRunner.NativeSpriteTypeFilterForTests` (D-F4-7) | NOT FOUND (`rg` sur `Alundra` et `Alundra.Tests` : 0 occurrence ; à créer près de `EventProgramRunner.cs:56`) | À CRÉER ; nom de classe à lire `EventProgramRunner` |
| `AlundraEffectStage.Emit` (`:5760`) | `Alundra/Rendering/Effects/EffectStage.cs:192` (`Emit => Queue(_sourceTick, _sourceRank, operation)`) ; ancienne `:190` | TROUVÉ. `BeginSource` `:178` |
| `CreateHitSparks` (modèle) | `Alundra/Rendering/Effects/NativeEffects.cs:200` ; ancienne `AlundraNativeEffects.cs:190` | TROUVÉ. Autres modèles du même fichier : `CreateBreakEffect` `:175`, `CreateHitAttachedEffect` `:182`, `CreateSprintBounce` `:161`, `CreatedForTests` `:96` |
| `AlundraDamage.ResolveBalanceTarget` (p3) | `Alundra/Combat/Damage.cs:24` ; ancienne `AlundraDamage.cs:21` ; modèle de test : `Alundra.Tests/AlundraWaveSwordDamageTests.cs:15` (`P7a_...`) | TROUVÉ (`AlundraDamage` vers `Damage`) |
| `NativeHandlersEnabled` | `EventProgramRunner.cs:56` (ancienne `:106`), lu `:300` (ancienne `:350`) | TROUVÉ |
| `HitArena` (« éteint tous les natifs ») | `Alundra.Tests/AlundraHitArena.cs:27` (classe), `realRunner.NativeHandlersEnabled = false;` `:70` (ancienne `:68`) | TROUVÉ |
| `ArcRun` | `Alundra.Tests/AlundraArcSupport.cs:94` (ancienne `:84`) ; propriété `Runner` `:127` | TROUVÉ |
| `ResolveSpriteType` (donne 7) | `Alundra/Entities/Spawning/EntityRecordMapper.cs:227-237` (`\| 0x100` si `SpriteDirection & 0x80`) ; ancienne `:226` | TROUVÉ, inchangé |
| `ProductionWiredWeaponTypes` (« vide ») | `Alundra/Hero/PlayerManager.cs:1226` = `{ 27, 28, 29 }` ; ancienne `:1213` (vide) | CHANGÉ par f3c `f616dc83` |
| `UnhandledWeaponSpriteCodes(types, catalogue, registre)` | `PlayerManager.cs:1240-1266` ; ancienne `:1227` | TROUVÉ, même contrat (C et E non nuls ; E en ligne exempté par `IsInlineDeactivateCode`) |
| « crochet de test » | `PlayerManager.WeaponSpawnBirthHookForTests` `:1271` (appelé `:1210-1214`, remplace l'appel de production) ; `WiredWeaponTypesForTests` `:1230` ; `WiredWeaponTypes` `:1233` ; `SpawnWeaponProjectile` `:1199` | TROUVÉ (ancienne `:1258`) |
| `S5_...` | `Alundra.Tests/AlundraWeaponSpawnSeamTests.cs:238` ; clause `Contains((ProgramCTick, 94), seven)` `:257` ; ancienne `:233` et `:252` | CHANGÉ : réécrit par f3c (égalité de l'ensemble de production `{27,28,29}` en `:243`) |
| « premier bloc de S1 » | `AlundraWeaponSpawnSeamTests.cs:83` (méthode), bloc `s1-empty` `:85-95` ; ancienne `:78` | TROUVÉ |
| « (p5) de f3c » | `Alundra.Tests/AlundraWaveSwordWiringTests.cs:244` (`P5_R_...`, ensemble de test **explicite** `{27,28,29}` `:251`) | TROUVÉ ; déjà réécrit par f3c en témoin durable |
| `CheckAndTriggerTileEffect(entité)` (D-F3-6, bouchon de f3c) | `Alundra/Ai/Projectiles/WaveSwordAi.cs:40` ; journal `[ThreadStatic] StubCallsForTests` `:25-27` ; appelé par `Tick` `:34` ; à `5434cf6` : `AlundraWaveSwordAi.cs:24`, `:31`, `:37` | TROUVÉ dans le fichier de la vague : l'extraction de D-F4-3 est requise |
| `FrameCounter` (`+0x1C`) | `Alundra/Entities/EntityScriptProxy.cs:98` ; incrémenté par la passe des compteurs `Alundra/Maps/WorldProxy.cs:3009` (ancienne `:2944`), après les programmes, sans filtre de statut | TROUVÉ |
| `BoxDirection` / `AnimationDirection` / `TargetDirection` | `EntityScriptProxy.HitPass.cs:39` (ancienne `:37`) / `EntityScriptProxy.cs:162` / `:159` ; précédent de lecture de `+0x98` : `Alundra/Ai/EnemyHelpers.cs:68-71` | TROUVÉ |
| `HasLogicalZ` | `EntityScriptProxy.cs:1383` ; posé `WorldProxy.cs:3573` (naissance par l'interface) ; lu `ScriptedMotion.cs:212`, `:685`, `GameplayFreeze.cs:142` | TROUVÉ (absent à 9ecc7d5 : e0k2) |
| `TouchingEntity`, `BreakEffect` | `EntityScriptProxy.cs:499` (ancienne `:480`) ; `:154` (ancienne `:135`) ; `BreakEffect` posé `Alundra/Entities/Spawning/EntitySpawnFactory.cs:746` (ancienne `:723`), commun aux deux chemins de création (`ApplyHeaderInitialization` `:736`) | TROUVÉ |
| `DestroyEntity(e, fx)` (E14.a1b, E1) | `WorldProxy.cs:3301` (ancienne `:3226`) | TROUVÉ ; bouchons (1), (4), (6) toujours nommés |
| `SpawnEntityBySpriteIndex` | `WorldProxy.cs:3488` (ancienne `:3376`) ; roule le butin `:3566` (e0b2) | TROUVÉ |
| `docs/plan-e20-annexe/e20-f3/oracle/tools` | présent (56 fichiers) | TROUVÉ |
| `docs/plan-e20-annexe/e20-f4f6/` | absent | À CRÉER par T0 |
| `ADR-0026` | `docs/decisions/0026-absolute-z-writes-follow-the-dll-convention-at-spawn-0x8a-and-0x64.md` | TROUVÉ |
| `Alundra.Tests/` (périmètre) | à plat, préfixe `Alundra` (R5 bloquée sur Q-R-3 : `docs/plan-restructuration-dll.md:861`) | TROUVÉ |

## 2. Affirmations sur la DLL : toujours vraies ?

| # | Affirmation du plan | HEAD | Preuve |
|---|---|---|---|
| 1 | E0 et E1 en ligne, hors du registre ; D-F4-4 « aucun changement de runner » pour E1 | VRAI, élargi : E10 aussi (f3c) | `EventProgramRunner.cs:286-292`, `:328` ; `DestroyEntity(entity, -1)` lit `BreakEffect` de l'en-tête (`WorldProxy.cs:3301-3317`, lecture `:3308`) ; le type 7 a `BreakEffect 8` dans `sprite-records.json` |
| 2 | `ResolveSpriteType` donne 7 pour un enregistrement global | VRAI | `EntityRecordMapper.cs:227-237` |
| 3 | « Le registre a 6 clés » | FAUX : 7 clés (Beannoïde A1/C1/D1, Muruta A0/C10/D9, vague C61) | `NativeSpriteRegistry.cs:36-43` ; `AlundraNativeSpriteRegistryTests.cs:55` |
| 4 | Une clé en double lève une exception | VRAI | `NativeSpriteRegistry.cs:60-63` ; test `...Tests.cs:239-250` |
| 5 | Épinglé par un seul test | FAUX : deux (`AlundraNativeSpriteRegistryTests.cs:55`, `AlundraWaveSwordLifeTests.cs:181-183`) | cités plus haut |
| 6 | `ProductionWiredWeaponTypes` est vide | FAUX : `{27, 28, 29}` | `PlayerManager.cs:1226` |
| 7 | `UnhandledWeaponSpriteCodes` vérifie les codes C et E | VRAI (ne regarde ni A, ni D, ni F) ; vide pour `{7}` dès que `(C,94)` est inscrite (en-tête : C94, E1 en ligne, A 0) | `PlayerManager.cs:1240-1266` ; `sprite-records.json` |
| 8 | S5 contient « quatre clauses qui basculent » | À CORRIGER : S5 a été réécrit par f3c ; trois clauses basculent (`:257` à f4 ; `:247` et `:251` à f5) ; le plan en nomme trois ; S5 épingle aussi l'ensemble exact `{27,28,29}` (`:243`) qui bouge à la phase B | `AlundraWeaponSpawnSeamTests.cs:238-259` |
| 9 | `Emit` met en file, les tirages ont lieu à l'application, avant la mise à jour du réservoir | VRAI | `EffectStage.cs:20-26` (doc), `:192` ; seuls les créateurs de `NativeEffects` tirent (`NativeEffects.cs:55,116,209`) ; `EffectReservoir` ne tire pas |
| 10 | Le Z des créateurs est celui du binaire (`PosZ + 1`, ADR-0026) | VRAI pour les créateurs du héros ; `CreateBreakEffect` prend le Z de l'ancre (`IEffectAnchor.PosZ`) | `NativeEffects.cs:100`, `:158`, `:175` ; **aucun créateur existant ne prend une entité non héros avec `+ 0x80000`** |
| 11 | `HitArena` éteint tous les natifs | VRAI | `AlundraHitArena.cs:67-70` ; précédent de réactivation par test : `AlundraWaveSwordLifeTests.cs:128` (`run.Arena.Arc.Runner.NativeHandlersEnabled = true`, sans cible) |
| 12 | Le runner expose déjà un interrupteur par type | FAUX (à créer, D-F4-7) | 0 occurrence |
| 13 | C94 atteint le contexte du monde et la scène d'effets | VRAI : le handler reçoit `IEntityWorldContext` ; `EffectStage` est un membre par défaut `null` | `NativeSpriteRegistry.cs:20`, `IEntityWorldContext.cs:210`, `IScriptHost.cs:156` ; les scripts d'entités marquent leur tick (`EntityScriptProxy.cs:1521-1524`, `BeginSource(tick, 1)`; héros `:1543`) |
| 14 | Le bouchon de l'herbe est « celui de f3c, dans un fichier neutre » | FAUX au HEAD : il est dans le fichier de la vague ; quatre références de test le nomment (`AlundraWaveSwordWiringTests.cs:51`, `AlundraWaveSwordLifeTests.cs:53`, `:130`, `:149`) | `WaveSwordAi.cs:25-40` |
| 15 | La création par l'interface applique l'en-tête et ne tire pas pour `Contents 0` | VRAI : `RollLootAtCreation(hasRecord:false)` appelle `LootRoll.Roll`, `code = header & 0xFF = 0`, `Choose` rend 0 sans tirage ; e0b2 est postérieur au texte du plan mais cohérent avec `draws = 0` | `WorldProxy.cs:3566`, `LootRoll.cs` (`Choose`, `Roll`) |
| 16 | L'indice de direction de l'effet vient de la direction cible, « table inverse de `{0,0x10,8,0x18}` », pas d'`AnimationDirection` | NON ÉTABLI : le HEAD a un précédent plus simple, `BoxDirection` (= `+0x98`, D-E14-6), lu par les ennemis (`EnemyHelpers.cs:68-71`) et par le tir (`PlayerManager.cs:1204`, `SpawnDirections = {0,0x10,8,0x18}` `:1282`, privé) ; la sonde P-F4-1 doit trancher | voir §6, manques 8 et 10 |
| 17 | Le drapeau de mur après un pas se lit un tick plus tard dans 1 tir sur 3 (D-F4-9, « Q-K2 défaut A ») | VRAI, et plus ferme : Q-K1 et Q-K2 option A **confirmées par l'auteur** le 2026-10-16 (D-E20-37, D-E20-38, `:171-172`, `:4947`) | plan |
| 18 | Le bord de la carte borne le pas des entités marquées (`x = 1 237` à S+14, `charged7_map_edge`) | CONSISTANT : e0k3 épingle `X = 1 237` au tick 7 avec le drapeau le même tick pour la flèche 3 (`AlundraMapEdgeWorldTests.cs:116`) ; non rejoué pour le type 7 (INFERENCE) | e0k3 `65f20873`, `bef323b4` |
| 19 | La flèche survit à S+1 (« l'unité de Z est perdue : e0k2 ne tient pas ») | NON MESURÉ pour le type 7 ; la règle de choix de E est `DeactivateOnImpact && (ForceAdjusted != 0 \|\| IsOnGround != 0)` (`EntityScriptProxy.cs:1718-1720`) ; f3c a prouvé la même mécanique pour la vague | arrêt déjà écrit dans le plan |
| 20 | Le compte de départ de la suite est « 4 857 + ... » | PÉRIMÉ : `Alundra.Tests` 5 097 / 0 à la fin de R4 (Release et Debug) | `docs/plan-restructuration-dll.md:803`, `:826`, `:851` ; f3c avait mesuré 5 010 au départ de sa phase A |
| 21 | « A0 est une donnée morte » | COHÉRENT : l'en-tête 7 a `ProgramLoad 0` ; `(A, 0)` n'est inscrite que pour le Muruta (type 347) ; le vérificateur ignore A | `MurutaAi.cs:47`, `PlayerManager.cs:1240-1266` |
| 22 | Le tir chargé de William est la fiche `w9`, type 7, animation `0x13`, délai 8, z 8 | VRAI | `Alundra/Items/WeaponTables.cs` (octets du record 9 : `01 13 04 08 10 | 17 04 08 10 | 13 07 08 08`) ; item 6 = William (`WeaponTables.cs:53`, `table[6] = 9`) |
| 23 | Recette : `arsenal-b-m14`, arc de William en emplacement 2 | VRAI | `Alundra/SaveGames/TestSaves.cs:125`, `docs/test-saves.md:78,84` |

## 3. Tests qui épinglent ce que la tranche remplace (au-delà des listes du plan)

**A. Les deux épingles du registre (phase A : (C,94) fait passer 7 à 8)**
- `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:51-66` : `Assert.Equal(6 + 1, ...)` à `:55`. Le nom de la méthode, la doc de classe `:17` et celle de `NativeSpriteRegistry.cs:29,:35` / `EventProgramRunner.cs:47-50` disent « sept » / les trois clés de chaque IA : à corriger.
- `Alundra.Tests/AlundraWaveSwordLifeTests.cs:166-189` (`V3_...HoldsSevenKeys...`) : `(3 + 3 + 1, true, ...)` `:181-183`. Le nom encode « Seven ».
- Les autres tests du registre sont stables : `...Tests.cs:239-250` compte en relatif, `:85-97` ne regarde que le type −1, les clés libres utilisées sont `(C,25)`, `(D,25)`, `(E,0/1)`.

**B. L'ensemble câblé (phase B : `{27,28,29}` devient `{7,27,28,29}`)**
- `AlundraWeaponSpawnSeamTests.cs:243` : `Assert.Equal(new[] {27,28,29}..., ProductionWiredWeaponTypes...)`. **Bouge à la phase B**, ce qui contredit « phase B : aucun test existant » (`:5848`) et « retirer la ligne `7` rend rouges exactement (p1) à (p3) » (`:5880`).
- `:244` (`Assert.Empty(Unhandled...(ProductionWiredWeaponTypes ...))`) : devient rouge si la phase B précède l'inscription de (C,94), donc l'ordre phase A puis phase B est ferme.
- `:246-258` : les clauses `(E,17)` (`:247`, `:251`, f5) et `(C,94)` (`:257`, f4). Les deux premières ne bougent pas à f4.
- `AlundraWaveSwordWiringTests.cs:244-285` (`P5_R_...`) : ensemble de test explicite `{27,28,29}` `:251` : **ne bouge ni à f4 ni à f5** ; la réécriture que D-F4-8 lui prescrit est déjà faite par f3c.
- `AlundraWeaponSpawnSeamTests.cs:85-95` (premier bloc de S1, flèche 3, ensemble de production) : ne bouge pas à f4 (3 n'entre dans l'ensemble qu'à f5).

**C. Le bouchon de l'herbe, si D-F4-3 le déplace** : `AlundraWaveSwordWiringTests.cs:51`, `AlundraWaveSwordLifeTests.cs:53`, `:130`, `:149` (`WaveSwordAi.StubCallsForTests`), le test V2 (`:118-160`, 25 appels de S+1 à S+25) et `AlundraWaveSwordSupport.cs` (`GrassCallTicks`). « Les tests de f3c ne changent pas » n'est vrai que si `WaveSwordAi` garde le membre (renvoi) ; sinon ils changent.

**D. Tests qui font naître un type 7 par l'interface, natifs allumés, et font tourner des images** (le C94 de production tournera pour eux dès la phase A ; recensement du HEAD, à reprendre en T0) :
- `AlundraBirthTickTests.cs:217-250` (`MountCarrier`, `childIndex = 7` ; deux tests `BT_H_BornInTheNativeProgramOfAnotherEntity_...` à `:243` et `:262`) : le décorateur `SpriteEventRunner` (`:113-133`) délègue à `_inner.RunSpriteEvent`, donc C94 s'exécute ; ils lisent `Status`, `FrameCounter`, `ChildRuns`.
- `AlundraBirthTickTests.cs:386-396` (`TheSoundOfTheCreation_...`, `Shot(arc, spriteIndex: 7)`, 4 images, `player.Ids` un seul `0x21`).
- `AlundraSpawnBySpriteIndexInterfaceTests.cs:209` (`E_...`, sans image) et `:292` (`G_...`, son `0x21`, sans image).
- Vecteurs des 151 préfabs (`AlundraBirthBoxTests.cs:131`, `AlundraSpawnBySpriteIndexInterfaceTests.cs:55,:102`) : l'indice 7 y est, mais `Status = FlagToDestroy` tout de suite, sans tick : stables.
- Les bancs de flèche 3, boule 11, vague 27 (`AlundraLogicalZ*`, `AlundraMapState*`, `AlundraMapEdgeWorldTests`, `AlundraCreationSwitchClockTests`) n'utilisent pas le type 7.
- Les tests qui tirent le tir chargé de William dans un montage (`AlundraHeroChargeTests.cs:642`, `:659`, `:698-760`, pointeur `0x80028CF6`) tournent sur un hôte qui n'est pas un `IEntityWorldContext` : aucun effet de la phase B (`PlayerManager.cs:1216`). Aucun test existant ne fait tirer le tir chargé dans un monde réel (`rg Give(6)` : 0).

**E. Pins de format du plan** : `AlundraTestSavesTests` (liste exacte de 9 préréglages) ne bouge pas (la recette utilise `arsenal-b-m14`, déjà là).

## 4. Conséquences des tranches suivantes (§1.24 bloc « Révision au HEAD », §1.26, §1.27, §1.28)

1. **Rang.** Ordre ferme d'enveloppe : f4, f5, f6a, f6b (`:634-649`) ; f4, f5 et f6a sont des prérequis fermes de g (`:6392-6395`, `:6446`, `:6618`, `:6673`). **Contradiction non résolue sur E20.e** : §1.25 « Ordre proposé » (`:6837-6838`) place E20.e **avant** f4 (« que C61, C94 et la bombe appellent ») ; §1.27 `:7059` dit que C94 « n'aura que l'appel à ajouter » ; mais §1.20 (D-F4-3, D-F4-10, (c8), `charged7_grass`, O-F4-1) suppose f4 **avant** E20.e avec un bouchon, et le tableau d'enveloppe ne lie pas f4 à E20.e. Décision à prendre avant d'exécuter.
2. **Herbe, appel de C94.** §1.27 : périmètre = `PlayerAnimationEffects.cs` et `WaveSwordAi.cs` (deux bouchons qui appellent `Alundra/Cells/TileEffect.cs`) ; « Hors périmètre : C94 et E12 (leurs tranches) » (`:7136`). Si f4 passe avant, personne ne possède l'appel réel de C94 : O-F4-1 (`:6249`) l'attribue à E20.e, E20.e l'exclut. Si f4 déplace le bouchon dans un fichier neutre, le périmètre d'E20.e (`WaveSwordAi.cs`, « leurs journaux d'appels restent ») et ses faits DLL (`WaveSwordAi.cs:40`) ne tiennent plus. Test d'E20.e qui supposent « aucune coupe » et que (c8) d'f4 contredit à son tour (« aucune case coupée, le flux égale l'avance pure ») : (c8) devient rouge dès que le port est branché sur C94.
3. **Épingles du registre.** §1.24 point 4 (`:6392-6397`) : sept clés, deux épingles, E20.e0c en ajoute deux (C255, A255 ; `:7014` « de 7 à 9 »), g1a une ((C,23)), g2b une ((E,2)) ; ordre libre ; chaque tranche réécrit « par dérivation (le compte d'avant plus ses clés) ». f4 ajoute (C,94). Total attendu une fois tout passé (INFERENCE) : 7 + 1 (f4) + 1 (f5 : (E,17) pour 3, 4, 5, 6) + 2 (f6b : C60, E9) + 2 (e0c) + 1 + 1 = 15. D-F4-5 prescrit une autre dérivation (somme des clés exposées par les classes d'IA) : voir manque 4.
4. **Règle d'inscription de test** (`:6395-6397` ; §1.24.3 « Partage des phases ») : une clé inscrite par un test l'est dans un registre neuf, ou dans celui du runner seulement si absente, par l'`IDisposable` de `Register` (`NativeSpriteRegistry.cs:54`, `:84`). f4 n'inscrit en test aucune clé : (C,94) est inscrite en production dès la phase A (D-F4-1), les tests de vie passent par le crochet. La règle ne s'applique à f4 que pour les cas où un test voudrait une clé avant la phase A (non prévu). Elle impose en revanche à D-F4-8 d'utiliser `new NativeSpriteRegistry()` (accessible : classe `internal sealed`, constructeur implicite) pour le registre vide de S5.
5. **Interrupteur d'arène pour g2b** (`:6444-6446`, `:6618`, (b3), (b6)) : g2b attend de D-F4-7 que « l'arène garde les caisses vivantes et le Beannoïde inerte » : il faut donc un ensemble de types choisi par l'appelant (les 10 types de E2 allumés, 335 éteint), pas seulement « les types de projectiles ». Incohérence interne à §1.24 : le bloc « Révision au HEAD » (`:6444-6446`) dit que D-F4-7 « n'existe pas (f4 ⏳) » et joue (b3) sur le banc d'E20.e (autres entités détruites, natifs allumés : `:7169`), alors que §1.24.3 (`:6618`, arrêt `:6654`) fait d'f4 un prérequis ferme et lit D-F4-7 pour (b3) : deux techniques d'arène coexistent dans le plan, et f4 doit fixer la forme que g2b consommera.
6. **Fichiers partagés** : `NativeSpriteRegistry.cs` (e0c, g1a, g2b, f5, f6b), `EventProgramRunner.cs` (g1a corrige `:47-50`, `:6402`), `PlayerManager.cs:1226` (f5, f6b, h3), `WaveSwordAi.cs` (E20.e), `AlundraHitArena.cs` (g2b ; g4a cite `:74` et `:235` : toute insertion dans ce fichier décale ces citations), les deux tests d'épingle, `AlundraWeaponSpawnSeamTests.cs` (S5, S1, f5), l'annexe `e20-f4f6` (f5, f6a, f6b).
7. **Garde des fichiers neufs (Q-R-7)** : `docs/plan-restructuration-dll.md:1234` est une **proposition** non tranchée (R7, `check_names.py --rules`, pas écrit). Prouvé sur une copie `git archive` du HEAD plus `Alundra/Ai/Projectiles/ChargedArrowAi.cs` : `check_names.py --tree --stage final --root ...` sort `[hors table] file ... is not in the table at stage final`, 1 finding, code 1 (HEAD nu : 0 finding, 207 fichiers, 220 types). g1a, g1b, e0c, e, g3 listent cette garde comme prérequis (`:6386`, `:6674`, `:6969`, `:7124`, `:7266`) ; f4 ne la nomme pas, ni aucune étape `check_names.py` dans T1 ou T4.
8. **Chemins de tests** : à plat, `Alundra<Sujet>Tests.cs`, tant que R5 n'est pas passée (`:6384`).
9. **Numéro d'ADR** : le dernier est `0075` (`docs/decisions`), donc 0076 si f4 passe en premier ; g1a et e0c prennent aussi « le prochain libre ».

## 5. Noms nouveaux (règles de l'ADR-0075)

| Élément proposé | Nom du plan | Nom final conforme | Contrôle |
|---|---|---|---|
| Classe d'IA de C94 | `AlundraChargedArrowAi` dans `Alundra/Scripts/AlundraChargedArrowAi.cs` | `ChargedArrowAi`, `namespace Alundra.Ai.Projectiles`, `internal static class`, fichier `Alundra/Ai/Projectiles/ChargedArrowAi.cs`, modèle `WaveSwordAi.cs:14-41` (`Register(NativeSpriteRegistry)`, `TickCode`, `SpriteTypes`) | `scan_types.external_types()` : aucune collision pour `ChargedArrowAi`, `ChargedArrow`, `ArrowAi`, `ProjectileAi` ; fichier hors table tant que Q-R-7 n'est pas tranchée |
| Clés exposées (D-F4-5) | « expose ses clés » | membre non nommé (ex. une liste statique) | non nommé |
| Interrupteur d'arène | `AlundraEventProgramRunner.NativeSpriteTypeFilterForTests` | `EventProgramRunner.NativeSpriteTypeFilterForTests` (membre ; instance ou statique non dit) | pas un type |
| Créateur de la traînée | « créateur » | non nommé ; précédents : `NativeEffects.CreateHitSparks` `:200`, `CreateBreakEffect` `:175` | à placer (`NativeEffects.cs` ou `ChargedArrowAi.cs`) |
| Bouchon « neutre » | « un fichier neutre » | non nommé ; E20.e prévoit `Alundra/Cells/TileEffect.cs` (`:7126`) pour le port | à coordonner |
| Aides de test | `ChargedOracle`/`ChargedRun` non cités | à créer sur le modèle de `WaveOracle` (`AlundraWaveSwordSupport.cs:20`) et `WaveRun` (`:138`) ; `WaveOracle.Path` cherche `e20-f3/oracle/out` en dur (`:39`) | non nommés ; le dossier `out` d'`e20-f4f6` n'est pas nommé |
| Annexe | `docs/plan-e20-annexe/e20-f4f6/` | idem (les annexes ne sont pas renommées, ADR-0075 règle 7) | — |
| ADR | « prochain numéro libre » | `docs/decisions/0076-...` si f4 est la première | — |

## 6. Manques bloquants et incohérences internes

Gravité : B = bloque l'exécution telle quelle ; C = à corriger dans le plan avant ; m = mineur.

1. **(B)** Périmètre et noms périmés : `Alundra/Scripts/` n'existe plus, trois fichiers du périmètre et la classe ont changé de chemin et de nom (§1). Aucune étape `check_names.py` (règle 4 de l'ADR-0075) en T1 ni T4 ; la garde Q-R-7 n'est pas un prérequis d'f4 alors qu'f4 ajoute un fichier à la DLL (preuve §4.7).
2. **(B)** Ordre f4 / E20.e contradictoire entre §1.20, §1.25 et §1.27 ; propriétaire de l'appel réel de C94 non défini (§4.1-4.2).
3. **(B)** D-F4-3 : « bouchon neutre, déplacement pur, tests de f3c inchangés » est faux au HEAD : le bouchon est dans `WaveSwordAi.cs:25-40` avec un journal `StubCallsForTests` référencé quatre fois par deux fichiers de test de f3c ; D-F3-6 donne à chaque appelant son bouchon et son journal (précédent `PlayerAnimationEffects.StubCallsForTests`, `PlayerAnimationEffects.cs:51`). Soit un bouchon propre à `ChargedArrowAi` (pas de déplacement), soit un déplacement avec renvois ou tests modifiés ; nom et fichier du bouchon neutre non fixés.
4. **(B)** D-F4-5 « le test du registre somme les clés des classes d'IA (dérivation) » : aucune classe d'IA du HEAD n'expose ses clés (`BeannoideAi.cs:30-35`, `MurutaAi.cs:44-49`, `WaveSwordAi.cs:30-31` n'ont que `Register`) ; la somme exige de modifier trois fichiers hors périmètre. Le bloc « Révision au HEAD » (`:6394-6395`) prescrit une autre dérivation (compte d'avant plus ses clés). Une seule règle à écrire.
5. **(B)** Phase B et épingles : `AlundraWeaponSpawnSeamTests.cs:243` épingle l'ensemble exact `{27,28,29}` et bouge à la phase B ; le T0 prédit « phase B : aucun test existant » et la preuve de retrait « exactement (p1) à (p3) ». Il faut soit réécrire `:243` en phase A (dérivé de l'ensemble de production), soit corriger la prédiction et la preuve.
6. **(C)** D-F4-8 / acceptation / T1 : D-F4-8 réécrit « une fois, par la première » (S5 négatives, premier bloc de S1, (p5)) ; l'acceptation dit « premier bloc de S1 si f5 est déjà faite » ; T1 `(c3)` ne cite que S5. (p5) est déjà durable (`AlundraWaveSwordWiringTests.cs:251`). S1 bloc 1 ne bouge qu'à f5. Le plan de f5 (`:5980`, périmètre) réécrit aussi S5, S1, (p5), P3. Une répartition unique à écrire.
7. **(C)** D-F4-7 sous-spécifié : instance (comme `NativeHandlersEnabled`) ou `[ThreadStatic]` (comme les autres graines de test) ; type (`IReadOnlySet<int>?`) ; sémantique avec `NativeHandlersEnabled` (ET, ou l'ensemble rallume malgré `false` posé par `HitArena.cs:70`) ; `null` contre ensemble vide ; clé = `entity.SpriteType` (indice + 0x100 pour une page de carte) ; « option de HitArena, ou ArcRun plus l'interrupteur » laisse au T1 une forme que g2b consomme (b3) sans la décrire.
8. **(C)** O-E0A5-2 (`:4487`) : « `+0x1C` périmé d'une entité née dans une case libre sous la plus haute ; la DLL part de 0 ; touche la phase de C94 (`+0x1C & 3`) » est **assigné au T0 d'E20.f4** ; le T0 d'§1.20 ne le contient pas (seulement P-F4-1 et P-F4-2). O-E0A5-4 (direction de création non cardinale, « E20.f4 à f6 si un tir non cardinal existe », `:4489`) n'est pas traité non plus.
9. **(C)** T0, sources de l'oracle : les 9 fichiers `charged7_*`, `.effects[]/.draws/.rng0/.rng1/.snd[]` et le harnais qui les produit (`disc-e20f4f6/bin/fw.py`, `run_charged.py`, `sweep_wall_charged.py`, qui importent `run_arrows.py`) n'existent que dans le scratchpad ; T0 ne dit de copier que les outils de `e20-f3/oracle/tools` et d'écrire « un script par tranche ». UNKNOWN : si `pworld.py`/`hitfx.py` d'e20-f3 produisent déjà ces champs. Le dossier `out` d'`e20-f4f6` que les tests liraient « par clé » n'est pas nommé.
10. **(C)** P-F4-2 : « résultat de la sonde v4 de f3c » : (v4) est un test de f3c (`AlundraWaveSwordLifeTests.cs:214`), pas une sonde ; aucune valeur attendue n'est écrite pour P-F4-1 ni P-F4-2 ; aucun arrêt ne dit quoi faire s'ils contredisent la prédiction (les arrêts de `:5887-5888` ne les citent pas).
11. **(C)** Périmètre incomplet : `docs/plan-e20-heros.md` (T4 « plan ») et l'index des ADR ne sont pas listés ; `NativeEffects.cs` non plus si le créateur y va ; le fichier du bouchon neutre n'est pas nommé.
12. **(C)** Valeurs périmées dans le texte : « 6 clés », « ensemble vide », « 4 857 », « Q-K1/Q-K2 défaut A » (confirmées), « quatre clauses » (trois nommées), `Alundra/Scripts/` et préfixes partout.
13. **(m)** Le tir chargé du type 7 : le record `w9` n'a qu'une variante chargée (animation `0x13`, `WeaponTables.cs` record 9) ; « Hors périmètre : le tir en l'air de la flèche chargée » est donc sans objet ou un arrêt non écrit (INFERENCE).
14. **(m)** Banc de William non nommé : (p1) devrait dire `Give(6)` + `Equip(2)` (item 6, emplacement des arcs ; précédent `AlundraShotDirectionTests.cs:30-31`, `:55`).
15. **(m)** L'observation de la traînée : `NativeEffects.CreatedForTests` (`:96`) n'est appelé que par les créateurs du héros ; la traînée n'a pas de point d'observation nommé ; (c1) « entité faite à la main » doit aussi fournir un hôte avec `EffectStage`.
16. **(m)** « Cinq traces » : expression de la maison (remise en état des fichiers de trace après les tests), non définie dans §1.20.

## 7. Inconnus

- Compte exact de `Alundra.Tests` au HEAD : non mesuré (je n'ai rien lancé) ; dernier relevé connu 5 097 / 0 (R4, 2026-10-17).
- Si le type 7 reste vivant à S+1 avec e0k2 + e0a5 + e0k3 (mesure du T0/T1 d'f4) ; si `x = 1 237` est atteint à S+14 pour la flèche chargée.
- Si `BoxDirection` du type 7 vaut l'indice de direction de création aux quatre directions au premier appel de C94 (P-F4-1).
- Si le `+0x1C` du binaire pour la case choisie par l'oracle est frais (O-E0A5-2) et combien de tests nés par l'interface changent réellement (T0 le recompte).
- La durée de vie du scratchpad `disc-e20f4f6` (seul porteur des clés `charged7_*`).
- Si l'auteur veut E20.e avant f4 (§1.25 le propose, rien ne le tranche).
- L'état de la garde Q-R-7 : proposition ouverte, aucun code.
