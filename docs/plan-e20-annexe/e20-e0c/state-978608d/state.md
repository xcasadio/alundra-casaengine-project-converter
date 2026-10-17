# E20.e0c (butin, objet ramassable) : état de la DLL et des tests à HEAD `978608d`

Lecture seule. Branche `chantier/restructuration-dll-r3`, HEAD `978608d`. Les rapports (`docs/plan-e20-annexe/e20-e0c/report.md`, `check.md`) ont été
écrits à `2010ec3` (puis versés par `b35a2c4`, docs seules). Sous-module analyseur inchangé (`fc16872`) ; pointeur moteur passé de `781d3527` à
`92d8e847`.

**Méthode.** Chaque ligne citée est lue à `2010ec3` (`git show 2010ec3:<ancien chemin>`), puis retrouvée à HEAD par un appariement de lignes
(`difflib`) après avoir : retiré les lignes `using` et `namespace`, remplacé les anciens noms de types par les noms finaux de
`tools/dll-structure/table.json`. Scripts : `.../e20-e-state/e0c/map_lines.py` et `norm_diff.py`. Puis `rg` pour confirmer. Les étiquettes sont
FAIT (sortie de commande ou ligne lue), INFÉRENCE, INCONNU. Aucun build, aucun test lancé.

**Résultat d'ensemble (FAIT).** Entre `2010ec3` et HEAD, 131 commits. Dans `Alundra/`, `Alundra.Tests/` et `tools/AlundraTestSaves`, deux commits ont un
message qui n'est pas un déplacement, un changement de namespace ou un renommage : `146e1d8` (R0 côté parent) et `15804ae` (R4) ; les autres sont de
ces trois sortes d'après leur message (`git log -- Alundra Alundra.Tests tools/AlundraTestSaves`). Pour les fichiers cités, je l'ai vérifié : le diff
normalisé des 13 fichiers de DLL et des 7 fichiers de tests ne laisse, hors commentaires, que : les trois `catch (ScriptTypeResolutionException)` de `WorldProxy.cs` ; trois
`typeof(EntityScriptProxy).FullName!` dans `EntitySpawnFactory.cs` ; le même motif dans deux tests (`AlundraBeannoideOracleTests.cs:139`,
`AlundraMurutaOracleTests.cs:160`). S'y ajoutent des écarts de pure restructuration : des qualifications de namespace dans `WorldProxy.cs` (`Audio.BgmFadeDirector`,
`Transitions.ScreenFadeDirector`, `Dialogues.DialogueDirector`, `Entities.SpriteRecordCatalog`, là où un membre porte le nom du type) et les types coupés vers leur propre fichier
(`EventTraceKind`/`EventTraceRecord` hors d'`EventProgramRunner.cs`, `NoOpEntityWorldContext` hors d'`IEntityWorldContext.cs`). Le code de butin cité n'a donc pas changé de sens.
Les numéros de ligne ont tous bougé (`EventProgramRunner.cs` de −50, `WorldProxy.cs` de +28 à +46, les autres de +2 à +13).

---

## 1. Citations traduites

Chemins DLL à HEAD : `Alundra/Items/LootRoll.cs` (anc. `Scripts/AlundraLootRoll.cs`), `Alundra/Maps/WorldProxy.cs`, `Alundra/Rng/GameRandom.cs`,
`Alundra/Entities/EntityScriptProxy.cs`, `Alundra/Entities/Spawning/EntitySpawnFactory.cs`, `Alundra/Maps/IEntityWorldContext.cs`,
`Alundra/Events/EventProgramRunner.cs`, `Alundra/Hero/PlayerManager.cs`, `Alundra/Rendering/Effects/NativeEffects.cs`,
`Alundra/Ai/NativeSpriteRegistry.cs`, `Alundra/Ai/Projectiles/WaveSwordAi.cs`, `Alundra/Ai/Objects/SaveBook.cs`, `Alundra/Items/ItemTables.cs`.
Les tests gardent leurs noms de fichier (rangement plat, R5 non exécutée).

### 1.a `report.md`

| Ancienne citation (ligne du rapport) | HEAD file:line | Statut |
|---|---|---|
| `AlundraWorldProxy.cs:3269` (l.11, l.244, l.287) | `Alundra/Maps/WorldProxy.cs:3303` (`// (1) loot: E20.e0.`) | TROUVÉ, même sens ; la méthode est `DestroyEntity(EntityScriptProxy, int)` à `:3301` |
| `AlundraLootRoll.cs:108` (l.23) | `Alundra/Items/LootRoll.cs:111` (`TableBytes`) | TROUVÉ ; md5 de la table recalculé = `7c6b97ac50de72f436a6fbf08ed6bcd7` |
| `FunctionTypeE.cs:291-345` (l.98) | `alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/FunctionTypeE.cs:291-345` (`FUN_8003a648`) | HORS DLL, inchangé (même pointeur de sous-module) |
| `PlayerManager.cs:4562-4800`, `:5070` (l.157) | `alundra-datas-analyser/.../Gameplay/PlayerManager.cs:4562` (`AddLifeToEntity`), `:5070` (`FUN_80033f00`) | HORS DLL, inchangé |
| `AlundraLootRoll.cs` `:22`, `Choose :51`, `Roll :77`, table `:108` (l.232) | `LootRoll.cs:25` (classe), `:54`, `:80`, `:111` | TROUVÉ |
| `AlundraWorldProxy.cs:1091, :3406, :3526` (l.232) | `WorldProxy.cs:1119`, `:3440`, `:3566` (trois `RollLootAtCreation(...)`) | TROUVÉ ; les trois sites sont maintenant suivis d'un `catch (ScriptTypeResolutionException)` (`:1122`, `:3444`, `:3580`) |
| `RollLootAtCreation :3429`, `RecordContentsByte :3420` (l.232) | `WorldProxy.cs:3469`, `:3460` | TROUVÉ |
| `AlundraRandom.cs:52` (l.232) | `Alundra/Rng/GameRandom.cs:54` (`Next()`) | TROUVÉ, type renommé `GameRandom` |
| `AlundraEntityScriptProxy.cs:100` (`HeaderContents`) | `EntityScriptProxy.cs:113` | TROUVÉ |
| `AlundraEntitySpawnFactory.cs:743` | `EntitySpawnFactory.cs:747` (`proxy.HeaderContents = header.Contents;`) | TROUVÉ |
| `EntityScriptProxy.cs:93-94` (l.233) | `:106-107` (`ContentsItemId` uint, `ContentsGameFlag` int) | TROUVÉ |
| `ForceZ :325`, `IsOnGround :404`, `DamagedTickCounter :470` | `:338`, `:417`, `:483` | TROUVÉ |
| `Bytes/DelayOrAngleOrEntityId/ItemState/AIValues :1214-1217` | `:1227-1230` (`byte[4]`, `int`, `int`, `short[10]`) | TROUVÉ |
| `Width/Height/Depth :475` | `:488` | TROUVÉ |
| `EntitySpawnFactory.cs:858-860` (posent Width/Height/Depth) | `EntitySpawnFactory.cs:862-864` | TROUVÉ |
| `IEntityWorldContext.cs:70` (l.234) | `Alundra/Maps/IEntityWorldContext.cs:81` (membre d'interface par défaut, rend `null`) | TROUVÉ |
| `AlundraWorldProxy.cs:3448`, `:3555` (impl.) | `WorldProxy.cs:3488` (méthode interne), `:3601` (implémentation explicite) | TROUVÉ |
| `AddMoney :2272`, `AddOneItemIfUnlocked :2225`, `UseItem :2250`, `SetPlayerMp :1957`, `IncreaseHpMaxAndFill :1862`, `hero.Hp/HpMax :1441` (l.235, fichier non nommé) | `Alundra/Hero/PlayerManager.cs:2285`, `:2238`, `:2263`, `:1970`, `:1875`, `:1454` | TROUVÉ (le rapport ne nomme pas le fichier ; `check.md` corrige : `PlayerManager`, pas `WorldProxy`) |
| `AlundraEventProgramRunner.cs:3150` (opcode `0x82`), `UseLifeVessel :3196` (l.236) | `Alundra/Events/EventProgramRunner.cs:3100` (`HandleMapTriggerCommand`), `:3146` | TROUVÉ |
| `AlundraNativeEffects.CreateLifeVesselEffects :28` (l.237) | `Alundra/Rendering/Effects/NativeEffects.cs:31` | TROUVÉ |
| `Sounds/sfx-manifest.json` (sons 52, 53, 54, 84) | `alundra-project/Sounds/sfx-manifest.json` : ids 52, 53, 54, 84 présents (lus) | TROUVÉ |
| `AlundraNativeSpriteRegistry.Register :47` ; `AlundraWaveSwordAi.cs:28` (l.238) | `Alundra/Ai/NativeSpriteRegistry.cs:54` ; `Alundra/Ai/Projectiles/WaveSwordAi.cs:31` | TROUVÉ |
| `AlundraSaveBook.cs:232-244` (l.239) | `Alundra/Ai/Objects/SaveBook.cs:239-251` ; le test `dialogue.IsOpen \|\| dialogue.IsAwaitingChoice` est à `:248` | TROUVÉ |
| `AlundraLootRollTests.cs`, `AlundraLootRollWorldTests.cs`, `AlundraLootRollWorldTests.Rolls.cs` (l.240) | mêmes chemins sous `Alundra.Tests/` | TROUVÉ (classes toujours préfixées `Alundra`) |
| `AlundraDestroyEntityBreakEffectTests.cs:199-214` (l.244, l.287) | `Alundra.Tests/AlundraDestroyEntityBreakEffectTests.cs:204-219` | TROUVÉ, même test |
| `AlundraWorldProxy.cs:3273` (« (4) libération de l'effet actif », l.244) | `WorldProxy.cs:3307` ; l'étape (6) « carrier » est à `:3315` | TROUVÉ |
| `AlundraEventProgramRunner.cs:335` (slot E d'index 11 no-op, l.245) | `EventProgramRunner.cs:285` (c'est un commentaire) ; le code qui prouve est `:286-290` (E index 0/1/10 en ligne) et `:296-317` (registre, sinon « counted no-op ») | TROUVÉ, mais la ligne citée est un commentaire qui ne nomme pas l'index 11 |
| `AlundraEntitySpawnFactory.cs:821-823` (`ModdedPos`, l.248) | `EntitySpawnFactory.cs:825-827` | TROUVÉ |
| `AlundraWorldProxy.cs:2958-2978` (compteur décrémenté, l.249) | `WorldProxy.cs:2992-3012` ; `RunCountersPass` à `:3003`, décrément à `:3010-3012` | TROUVÉ |
| `Data/item-drop-properties.json` (l.164) | `alundra-project/Data/item-drop-properties.json` existe | TROUVÉ |

### 1.b `check.md`

| Ancienne citation (ligne) | HEAD file:line | Statut |
|---|---|---|
| `AlundraLootRoll.cs:108` (l.13) ; `:22/51/77/108` (l.70) | `LootRoll.cs:111` ; `:25/54/80/111` | TROUVÉ |
| `GameMapInfo.cs` (Gravity à `+4`, l.24) | `alundra-datas-analyser/.../DatasBin/GameMapInfo.cs:11` (`Gravity //4`), `:38` | HORS DLL, inchangé |
| `GameEngine.cs:720` (l.38) | `alundra-datas-analyser/.../GameEngine.cs:720` (`isMapSprite = (entityRecord.SpriteDirection & 0x80) != 0`) | HORS DLL, inchangé |
| `AlundraWorldProxy.cs:3267-3273` (l.70) | `WorldProxy.cs:3301-3307` | TROUVÉ |
| `AlundraWorldProxy.cs` `1091`, `3406`, `3420`, `3429`, `3448`, `3526`, `3555` (l.70) | `1119`, `3440`, `3460`, `3469`, `3488`, `3566`, `3601` | TROUVÉ |
| `AlundraEntityScriptProxy.cs` `93/94/100/325/404/470/473/475/1214-1217` (l.70) | `106/107/113/338/417/483/486/488/1227-1230` | TROUVÉ (`473` = `ModdedPosX, ModdedPosY, ModdedPosZ`, HEAD `:486`) |
| `AlundraEntitySpawnFactory.cs` `743/821-823/858-860` | `747/825-827/862-864` | TROUVÉ |
| `AlundraEventProgramRunner.cs` `3150/3196` et `:335` | `3100/3146` et `:285` | TROUVÉ (voir la remarque sur `:335` plus haut) |
| `AlundraSaveBook.cs:232` ; `AlundraWaveSwordAi.cs:28` ; `AlundraNativeSpriteRegistry.cs:47` | `SaveBook.cs:239` (test à `:248`) ; `WaveSwordAi.cs:31` ; `NativeSpriteRegistry.cs:54` | TROUVÉ |
| `AlundraDestroyEntityBreakEffectTests.cs:199-214` (l.70, l.89) | `AlundraDestroyEntityBreakEffectTests.cs:204-219` | TROUVÉ |
| `AlundraPlayerManager.cs` `2272/2225/2250/1957/1862` (l.71) | `PlayerManager.cs:2285/2238/2263/1970/1875` | TROUVÉ |
| `AlundraBeannoideOracleTests.cs:361` (l.90) | `AlundraBeannoideOracleTests.cs:370` (`Assert.Empty(SpawnedEntities)`, C7) | TROUVÉ |
| `AlundraMurutaOracleTests.cs:439` (l.91) | `AlundraMurutaOracleTests.cs:450` (C7) | TROUVÉ |
| `AlundraEnemyHandlerOracle.cs` « entrées X1/X2 exclues (`spawn_39F58`, son 84) » (l.92) | le fichier existe, inchangé, mais ne contient que le type `HandlerJournalEntry` (`:14-15`) et le lecteur ; **l'exclusion est dans les deux tests d'oracle** : `AlundraBeannoideOracleTests.cs:404-415` (`excluded += ...`, `Assert.Equal(160, excluded)`) et `AlundraMurutaOracleTests.cs:483-494` | PARTIEL : fichier mal désigné, la réalité est dans les deux tests |
| « lignes BT-L1/BT-L3 du plan » (l.92) | `docs/plan-e20-heros.md:3595, 3626, 3677, 3785` ; `docs/plan-e20-annexe/e20-e0a/README.md:95, 113, 115` | TROUVÉ (ce sont des lignes de tableau de docs, pas du code) |

Aucune citation INTROUVABLE. Aucune dont le sens ait changé.

---

## 2. Affirmations sur la DLL : encore vraies à HEAD ?

« Changé depuis » = autre chose qu'un déplacement/renommage pur entre `2010ec3` et HEAD (voir le résultat d'ensemble).

| # | Affirmation (source) | Verdict | Preuve à HEAD |
|---|---|---|---|
| A1 | La DLL a le tirage et la table, octet pour octet (report §0, §1.1, §5 ; check §1) | VRAI | `LootRoll.cs:54-71` (`Choose`), `:111` (table) ; md5 de 2048 octets recalculé par script = `7c6b97ac…` ; lignes non nulles `0x81-0x83`, `0xF6-0xFF` ; `AlundraLootRollTests.cs:55` épingle le même md5 |
| A2 | Le tirage est appelé aux trois sites de création, une fois, en dernier, sur le flux réel (§5) | VRAI | `WorldProxy.cs:1119`, `:3440`, `:3566` ; `RollLootAtCreation :3469-3477` appelle `LootRoll.Roll(..., static () => (uint)GameRandom.Next())` ; `GameRandom.Next :54` |
| A3 | `HeaderContents` est lu de l'en-tête et posé par la fabrique (§5) | VRAI | `EntityScriptProxy.cs:113` ; `EntitySpawnFactory.cs:747` |
| A4 | Pas de lâcher, pas de veto MP, pas de C255/A255, pas de `GiveItem` (§0, §5 « Manque ») | VRAI | `rg SpawnRandomLoot\|SpawnContents\|GiveItem\|ApplyItem` dans `Alundra/` : seulement des commentaires (`LootRoll.cs:11`, `:52`) ; registre de production à 7 clés (`NativeSpriteRegistry.cs:36-43`, `AlundraNativeSpriteRegistryTests.cs:55`) : A1/C1/D1 Beannoïde, A0/C10/D9 Muruta, C61 vague |
| A5 | Le bouchon de `DestroyEntity` est `WorldProxy.cs:3269` et `:3273`, bouchons nommés (§0, §5) | VRAI | `:3303` « (1) loot: E20.e0. », `:3307` « (4) … E20.e. », `:3315` « (6) … E20.g. » ; doc `:3293-3299` écrit « NAMED STUB » pour les trois ; changé depuis : non |
| A6 | Le test épingle le bouchon (aucun tirage, aucune entité) (§5) | VRAI | `AlundraDestroyEntityBreakEffectTests.cs:204-219` : `RandomSeed` inchangée (`:215`), `SpawnedEntities` vide (`:216`), `ContentsItemId` reste 69 (`:217`), un seul slot d'effet (`:218`) |
| A7 | `ModdedPos` (`+0x1D8..`) n'est écrit qu'à la création (§0, §5 ; check §2) | VRAI | `rg ModdedPos[XYZ] Alundra` : écritures seulement `EntitySpawnFactory.cs:825-827` et la copie du clone `EntityScriptProxy.cs:2684-2686` ; le reste est lecture ou commentaire |
| A8 | Aucun rendu ne cache une entité selon `(DamagedTickCounter & 3) == 3` (§0, §5) | VRAI | `rg DamagedTickCounter Alundra` : champ `:483`, décrément `WorldProxy.cs:3010-3012`, écriture opcode `0x86` `EventProgramRunner.cs:793`, clone `:2682`, garde de cible du coup `HitPass.cs:127` ; aucun lecteur de rendu ; `rg blink\|clignot\|flicker` ne trouve rien côté entités. Nuance : le coup lit ce compteur (`HitPass.cs:127`), donc une écriture `+0x1CC = 0x78` par C255 rendrait l'objet non ciblable, comme le binaire (INFÉRENCE) |
| A9 | `AlundraItemTables` ne garde que la colonne 2 (`DropField3`) (§0, §5) | VRAI | `ItemTables.cs:57-58` (`DropColumnCount = 4`, `DropField3Column = 2`), `:81-90` (le chargeur lit les 4 colonnes et jette 0, 1, 3), `:140` (seul accesseur) ; doc `:22-24` |
| A10 | Pas d'accesseur 32 bits pour `AIValues` (mots `+0x280/+0x284/+0x288`) (§5 ; check §2) | VRAI | `EntityScriptProxy.cs:1230` `short[10]` ; `rg AiWord\|AIValue32` vide ; usages existants en 16 bits (`MurutaAi.cs:56`, `EnemyHelpers.cs:221`) |
| A11 | L'apparition par indice existe : `SpawnEntityBySpriteIndex` (§5) | VRAI | `IEntityWorldContext.cs:81`, `WorldProxy.cs:3488`, `:3601`. Un seul appelant de production : `PlayerManager.cs:1218` (le tir du héros). Le doc `IEntityWorldContext.cs:79-80` dit encore « Not called by any production path in E20.e0a1 » (périmé) |
| A12 | Le Z de T1 (« z−1 ») est cohérent : l'interface ajoute 1 (check §2) | VRAI | `WorldProxy.cs:3549` `unchecked(z + 1)` ; doc `IEntityWorldContext.cs:77-79` « a constant of the binary is passed minus one » |
| A13 | Un échec de table pleine rend `null` sans son (§1.4, T1) | VRAI côté DLL | `WorldProxy.cs:3528-3532` (`_slots.Allocate()` < 0 → `ReportSlot(-1)` + `Refuse`), `:3494` avertissement ; aucun son à cet endroit |
| A14 | Les fonctions d'état d'objet existent (§5) | VRAI | `PlayerManager.cs:2285` (`AddMoney`, plafond 9999 à `:2288`), `:2238`, `:2263`, `:1970` (`SetPlayerMp`), `:1875` (`IncreaseHpMaxAndFill`) |
| A15 | L'opcode `0x82` fait `0x45-0x48`, `0x4F`, `0x53` ; les autres `0x50-0x56` sont dégradés (O-E19-39) (§5) | VRAI | `EventProgramRunner.cs:3100-3139` : `0x45-0x48` → `AddMoney` ; `0x4F` → `FalconTemp` (plafond 50) + drapeau 1450 ; `0x53` → `UseLifeVessel :3146` ; `>= 0x50 and <= 0x56` → dégradé « answers given, writes nothing » ; défaut : `AddOneItemIfUnlocked`. Test `AlundraItemAndMoneyOpcodesTests.cs:292-311` |
| A16 | Modèle d'étincelles `CreateLifeVesselEffects`, effet global 14 à 3 animations, sons 52/53/54/84 (§5) | VRAI | `NativeEffects.cs:31` ; `alundra-project/Data/effects/effects-global.json` : `Effects[14].Animations` = 3 ; `alundra-project/Sounds/sfx-manifest.json` : ids 52, 53, 54, 84. Précision : `UseLifeVessel` passe par `stage.Emit` (file), pas `EffectStage.CreateNow` (`EffectStage.cs:199`, ADR-0057) |
| A17 | Le registre natif est clé par (slot, code) et filtre sur le type de sprite ; l'onde d'épée en est le précédent (§5 ; check §2) | VRAI | `NativeSpriteRegistry.cs:54-70` (une clé ne s'enregistre qu'une fois, sinon exception ; liste de types par clé) ; `WaveSwordAi.cs:31` |
| A18 | Test de dialogue ouvert : `dialogue.IsOpen \|\| IsAwaitingChoice` (§5) | VRAI | `SaveBook.cs:248` |
| A19 | Le slot E, index 11 (récursion) est un no-op (§5, check §2) | VRAI | `EventProgramRunner.cs:288` : en ligne seulement si `IsInlineDeactivateCode(code)` (`:328` : codes 0, 1 et, depuis E20.f3c, 10) ; sinon `NativeSprites.TryGet(slot E, 11, type)` (`:300-301`) ; `rg \.Register\(` ne trouve aucune inscription en slot E ; chemin restant « counted no-op » `:310-317`. (Le rapport dit « index 0 et 1 » ; le code accepte aussi 10.) |
| A20 | Les 151 objets posés sont inertes aujourd'hui (§5) | VRAI (chiffre à corriger) | catalogue `alundra-project/Data/sprite-records.json` : 87 types `alundra_31..118` sauf 72 avec A = C = 255, `Contents` 0 ; coffre `alundra_30` A = F = 255, C = 0 ; aucune inscription (A,255) ni (C,255). `check.md` §1 corrige le chiffre en **152 entités / 53 types** ; `docs/plan-e20-heros.md:6656` et `:6705` disent encore 151 |
| A21 | Les sprites objets du catalogue de la DLL ont les mêmes programmes 255 (check §2) | VRAI | idem A20 |
| A22 | Les 9 sprites d'objets lâchables portent `DestroyOnVramFlags`, pas la pente ; la DLL ne nourrit `CombinedVramFlagsOR` que pour le héros (check §3.1) | VRAI | flags calculés sur le catalogue (`MoreFlags \| CanPickup<<8 \| FlagsPortraitShadowType<<16`) : types 66, 99, 100, 101, 102, 111, 114, 115, 116 = `0x230180`, `0x220180`, `0x220180`, `0x220180`, `0x230180`, `0x220180`, `0x210180`, `0x220180`, `0x230180`, tous avec le bit `0x200000`, aucun `0x100000` ; `EntityFlags.cs:169` ; `EntityScriptProxy.cs:1642-1646` (`RunHeroProbes` → `UpdateVramFlags`, seul appelant) ; la destruction `EntityScriptProxy.cs:1713` lit `CombinedVramFlagsOR` |
| A23 | Les tests de Beannoïde et Muruta épinglent aussi le bouchon (check §3.2) | VRAI | voir §3 |
| A24 | Divergences de la branche coffre avec la décompilation (check §3.3) | NON VÉRIFIÉ ici (décompilation, tranche à part) | hors DLL |
| A25 | T2a : « enregistrer (C,255) pour 87 types et (A,255) pour 88 » (§8) | FAISABLE tel quel | `Register(slot, code, types, handler)` accepte un ensemble de types par clé ; clé unique par (slot, code) ; aucune des deux clés n'est prise |
| A26 | T1 : exposer les colonnes 0 et 1 (§8) | FAISABLE | le chargeur lit déjà les 4 colonnes (`ItemTables.cs:81-90`) |
| A27 | T3 : `SpawnContents` en première instruction de `DestroyEntity` (§8) | FAISABLE à `WorldProxy.cs:3303` ; voir §4 pour la contrainte du monde nu des tests |

---

## 3. Tests qui épinglent les bouchons que la tranche remplace

Numéros HEAD. Les fichiers de test sont dans `Alundra.Tests/` (rangement plat).

### 3.a Épinglent le bouchon de `DestroyEntity` (cassent ou deviennent creux quand le lâcher est branché)

| Test | file:line | Ce qu'il épingle |
|---|---|---|
| `TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents` | `AlundraDestroyEntityBreakEffectTests.cs:204-219` | aucun tirage, aucune entité créée avec `ContentsItemId = 69` ; doc de classe `:15-18` (« named stubs ») |
| `RunE1Call` (C7) | `AlundraBeannoideOracleTests.cs:338-396`, C7 `:370` ; appelants `:408` (`E1_TheVectors_FollowTheProjectionOfThePlan :399-416`, `Assert.Equal(160, excluded)` `:415`, `108` `:414`) et `:426` (`E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan :419-432`) | aucune entité née du butin ; entrées `spawn_39F58` et `sound` du journal de l'oracle **exclues** et comptées (160 = 80 + 80) ; doc de classe `:26-28` |
| `RunE1Call` (C7) | `AlundraMurutaOracleTests.cs:420-475`, C7 `:450` ; appelants `:487` (`:478-495`, `Assert.Equal(160, excluded)` `:494`) et `:505` (`:498-511`) | idem pour le Muruta |
| Docs de classe qui disent « butin non assertée » | `AlundraBeannoideGrateArcTests.cs:19`, `AlundraMurutaHallArcTests.cs:21` | pas une assertion : ces deux arcs tuent des Beannoïdes / Muruta sur les cartes 116 / 115 et n'assertent rien sur les entités nées (aucun `SpawnedEntities` dans ces fichiers) ; ils passeront par le nouveau chemin (INFÉRENCE : à relancer) |

Faits de l'oracle (FAIT, lus dans `docs/plan-e14-annexe/e14a/oracle-a1/out/vectors-{beannoide,muruta}.json`) : sur les 144 vecteurs E1 de chacun, 128 ont
`contents_3C ≠ 0` ; 80 portent `spawn_39F58 [0, 0, type, X, Y, Z, 0, 0]` suivi de `sound 84` (types `item + 0x1E` = 99, 100, 101, 114, 115 pour les
contenus 69, 70, 71, 84, 85) ; les 48 des contenus 38, 81, 82 (`0x26`, `0x51`, `0x52`) n'en portent jamais (veto MP) ; 16 ont un contenu 0. Scénarios :
4 appels E1 pour `BN-` dont 3 avec lâcher ; 7 pour `MU-` dont 6. Le lâcher et le veto sont donc déjà dans l'oracle, à comparer au lieu d'exclure.

### 3.b Épinglent d'autres manques que la tranche comble probablement

| Test | file:line | Ce qu'il épingle |
|---|---|---|
| `ProductionRunner_RegistryHoldsTheSevenHandlers...` | `AlundraNativeSpriteRegistryTests.cs:51-55` (`Assert.Equal(6 + 1, runner.NativeSprites.Count)`) | le registre de production a 7 clés : inscrire (C,255) et (A,255) dans `CreateForProduction` le fait passer à 9 |
| `Give_0x82_TheOtherHeroSubCases_StayDegraded_AnswerOne_AndWriteNothing` | `AlundraItemAndMoneyOpcodesTests.cs:293-311` (`0x50, 0x51, 0x52, 0x54, 0x55, 0x56`) | l'état dégradé de O-E19-39 : à changer si un `ItemEffects` partagé sert aussi `0x82` |
| `Give_0x82_Item36AtCount8_GivesTheLastOne_ThenRefuses`, `...Item41AtItsMaximum_Refuses...` | `AlundraItemAndMoneyOpcodesTests.cs:149`, `:164` | `0x82` sur `0x24`/`0x29` = inventaire seul, refuse au maximum ; le `GiveItem` du binaire utilise l'herbe `0x24` quand l'inventaire est plein (report §1.7) : ne pas partager sans trancher |
| `AlundraItemTablesTests` | `AlundraItemTablesTests.cs:36-39, 56, 71-79` | seule la colonne `DropField3` est testée, y compris le mode dégradé ; à étendre aux colonnes 0 et 1 |
| `M7_WithoutARecord_TheFlagField_IsWrittenZero_OverALeftover` | `AlundraLootRollWorldTests.Rolls.cs:198-212` | appelle par réflexion la méthode privée `WorldProxy.RollLootAtCreation` à trois paramètres (`:203`) : son nom et sa signature sont épinglés |
| Tests de création : `ModdedPos` posé à la création | `AlundraWorldProxySpawnInitializationTests.cs:242-244`, `AlundraSpawnBySpriteIndexTests.cs:35, 50`, `AlundraSpawnBySpriteIndexInterfaceTests.cs:87, 242` | valeurs à la création ; restent vrais si `ModdedPos` est en plus rafraîchi à chaque tick (Q-E0C-4) |

### 3.c Rien ne les épingle

- Le slot E d'index 11 / la récursion : aucun test n'affirme le no-op (`rg ProgramEDeactivate` dans les tests : seuls les codes 0, 1, 17 sont testés).
- `CheckAndTriggerTileEffect` (bouchon de la vague, `WaveSwordAi.cs:37-40`) appartient à E20.e, pas à e0c.
- Aucun test n'affirme que les objets posés sont inertes.

---

## 4. Nouveautés depuis le rapport, ou absentes du rapport, qui comptent pour écrire la tranche

1. **R0 (`146e1d8`, 2026-10-17)** : trois `catch (ScriptTypeResolutionException) { AbortCreation(...); throw; }` avant le `catch (Exception)` de chaque site de création
   (`WorldProxy.cs:1122`, `:3444`, `:3580`) ; la fabrique écrit `GameplayProxyClassName = typeof(EntityScriptProxy).FullName!` (`EntitySpawnFactory.cs:442, 513, 592`).
   Conséquence (FAIT sur le code, INFÉRENCE sur l'usage) : une création par `SpawnEntityBySpriteIndex` dont le nom de script ne se résout pas relance l'exception au lieu de
   « log and skip » ; un lâcher branché dans `DestroyEntity` la verrait remonter.
2. **Noms finaux (R3/R4, table.json)** à utiliser dans la tranche : `Alundra.Items.LootRoll`, `Alundra.Items.ItemTables`, `Alundra.Maps.WorldProxy`,
   `Alundra.Entities.EntityScriptProxy`, `Alundra.Hero.PlayerManager`, `Alundra.Rng.GameRandom`, `Alundra.Events.EventProgramRunner`,
   `Alundra.Rendering.Effects.NativeEffects`, `Alundra.Ai.NativeSpriteRegistry`, `Alundra.Session.GameState`. Les noms proposés par le rapport
   (`AlundraLootDrop`, `AlundraItemEffects`) violent la règle 2 de l'ADR-0075 (pas de préfixe `Alundra`) ; un nouveau nom doit aussi passer la règle 4
   (unique parmi DLL, moteur, MGUI, MonoGame, paquets, BCL ; ne porte pas le nom d'un segment de namespace : `check_names.py --table`).
   `check_names.py --tree --stage final` à HEAD : 207 fichiers, 220 types, **0 constat**, 1 avertissement (`TextBoxGpuHarness.cs:265`). INCONNU : si un fichier
   ajouté après R4 sera signalé `[hors table]` (README de l'outil et ADR-0075, Conséquences) et qui met la table à jour.
3. **ADR** : seul l'ADR-0075 du dépôt est postérieur (`docs/decisions/0075-the-gameplay-dll-is-restructured-...md`, ajouté par `f2bdb4b` le 2026-10-17, en-tête
   daté 2026-10-16). Prochain numéro libre côté DLL : 0076. Côté moteur, le pointeur a avancé de `781d3527` à `92d8e847` : ADR moteur 0075 (objets de script par
   nom complet), **0076 (une entité vit dans la liste d'un monde ou sous un parent, jamais les deux ; `World.AddEntity` refuse un enfant)**, 0077 (inspecteur du
   monde). Pour le lâcher : le parent passé à `SpawnEntityBySpriteIndex` est nul pour `SpawnRandomLoot` (`parent 0`) ; pour `SpawnContents` le binaire passe aussi
   `parent 0` (report §1.5) ; la DLL pose `proxy.ParentEntity` en `EntitySpawnFactory.cs:536` (parenté logique `+0x0C`, pas `Entity.Parent`). INFÉRENCE : ADR moteur 0076 ne gêne pas.
4. **ADR antérieurs au rapport mais jamais cités par lui ni par la critique, qui cadrent la tranche** : 0060 (l'effet de bris est créé pour tout appelant de
   `DestroyEntity`, c'est la même fonction), 0063 (apparition par indice), 0064 (table de cases, plafond 63), 0065 et 0074 (participation au tick de naissance),
   0066 (le flux avance d'un pas par tick), 0068 à 0070 (entités nées par l'interface : état de carte, Z logique, pas vertical, bord de carte), 0071 (horloge d'animation
   et boîte du tick de naissance), 0073 (tirage à la création), 0057 (effets créés tout de suite par `EffectStage.CreateNow`). Les prérequis du plan (e0b2, e0a5,
   e0k1 à e0k3) sont tous faits (`docs/plan-e20-heros.md:605, 624, 729`).
5. **Tick de naissance du lâcher (point ouvert, non traité par le rapport)** : `WorldProxy.cs:2930-2936` : seule une entité née pendant `MovePlayer` rejoint le tick en cours ;
   une entité née dans la passe des programmes d'une autre entité (c'est le cas d'un lâcher à la mort, E1 dans `TickPrograms`) est dans le bloc du tick suivant.
   Le binaire a une pleine participation pour un emplacement plus haut (lignes `BT-L1`, `BT-L3` : `docs/plan-e20-annexe/e20-e0a/README.md:95, 113, 115` ; plan
   `:3626, :3677`, « relève d'e0b et e0c »), et le défaut « A deux fois » n'est pas porté (D-E0A-6, point O-E0A-1, plan `:3277`, `:4293`). La tranche doit le
   décider ou le consigner. `WorldProxy.cs:3572-3573` pose `JoinsBirthTick` et `HasLogicalZ` sur toute entité née par l'interface : un objet lâché est déjà dans la loi
   de gravité des entités marquées (ADR-0069).
6. **Aide réutilisable non citée** : `Alundra/Combat/HitPass.cs:63` `AxisOverlaps(boxPosition, boxSize, bodyPosition, bodySize)` fait exactement le test d'axe de C255
   décrit au report §1.6 (`d = box − body ; d ≥ 0 ? d < bodySize + 1 : −d < boxSize + 1`). Autres précédents du point Q-E0C-7 : `PlayerManager.cs:1893` (`IncreaseHpMax`) et
   `:1910` (`RestoreLifeAndMagic`) suivent la même règle `LifePulledFromStats` que `IncreaseHpMaxAndFill :1875`.
7. **Tests : R5 pas encore exécutée.** `docs/plan-restructuration-dll.md:861-1232` : R5 range tous les tests en miroir (333 fichiers changent de dossier, 306 aussi de
   nom) et attend les réponses de l'auteur à Q-R-3. Les chemins de test de cette liste changeront. Proposition non appliquée (session, hors dépôt :
   `scratchpad/r5-disc2/r5-files.tsv`) : `AlundraLootRollTests` → `Alundra.Tests/Items/LootRollTests.cs` ; `AlundraLootRollWorldTests` (+ `.Rolls`) →
   `Items/LootRollWorldTests*.cs` ; `AlundraDestroyEntityBreakEffectTests` → `Rendering/Effects/DestroyEntityBreakEffectTests.cs` ; `AlundraBeannoideOracleTests`,
   `AlundraMurutaOracleTests`, `AlundraEnemyHandlerOracle` → `Ai/Enemies/` ; `AlundraNativeSpriteRegistryTests` → `Ai/` ; `AlundraItemAndMoneyOpcodesTests` → `Events/` ;
   `AlundraItemTablesTests` → `Items/` ; les arcs → `Arcs/`. Aucun de ces fichiers n'est une classe partielle sur deux fichiers sauf `AlundraLootRollWorldTests`.
8. **Nouveaux tests depuis le rapport** : seulement `Alundra.Tests/AlundraScriptAssemblyRegistration.cs` et `Alundra.Tests/AlundraScriptNamesInTheDataTests.cs` (R0). Rien sur le butin.
9. **Monde nu dans les tests (INFÉRENCE sur le code lu)** : `AlundraDestroyEntityBreakEffectTests.cs:27-32` et `RunE1Call` fabriquent `new WorldProxy()` sans `InitializeWithWorld`.
   `SpawnEntityBySpriteIndex` refuse alors (`WorldProxy.cs:3498-3500`, « called before InitializeWithWorld ») : le test `:204-219` resterait vert mais creux, et la
   comparaison du `spawn_39F58` / `sound 84` de l'oracle exigera un monde initialisé avec un catalogue de sprites.
10. **Chiffres du plan non corrigés** : `docs/plan-e20-heros.md:6656` et `:6705` gardent « 151 » ; la critique donne 152 entités / 53 types, 4 200 tirés (pas 4 206), M252 non tiré.
11. **VRAM des objets lâchés** : confirmé (A22). Un objet lâché dans l'eau ou un trou ne serait pas détruit tant que `CombinedVramFlagsOR` n'est nourri que pour le héros
    (O-E0K-2, D-G-14, `docs/plan-e20-heros.md:6358`). Le plan a aussi O-E0K-8 (`:5319`, liste des collidables par image, « à mesurer par e0c ou g2 ») et P3 d'e0b2 (`:4924-4926`,
    test d'un parent qui porte un code de butin et crée une entité, « à faire avec E20.e0c ou E14 »).

---

## 5. Inconnus

- INCONNU : la suite de tests passe-t-elle à HEAD ? Non relancée ici (interdit). Le plan de restructuration annonce R3 et R4 « CONFIRMED » (`docs/plan-restructuration-dll.md:4`).
- INCONNU : l'ensemble exhaustif des tests qui détruisent, par le vrai `WorldProxy`, une entité à `ContentsItemId ≠ 0`. Trouvés : le test `:204-219`, les deux oracles, les deux arcs
  (116 et 115). Les autres arcs qui tuent ou font disparaître un ennemi par E0/E1, la pente ou la VRAM n'ont pas été énumérés (il faudrait les lancer).
- INCONNU : le premier `IsOnGround` d'un objet lâché dans la physique de la DLL, donc la séquence de rebond exacte (INFÉRENCE du report §1.9, jamais mesurée ; aucun test d'objet lâché n'existe).
- INCONNU : la forme que prendra R5 (réponses Q-R-3) ; les chemins de tests ci-dessus sont provisoires.
- INCONNU : comment l'outil `check_names.py --tree` traite un `.cs` ajouté après R4 (voir §4 point 2).
- INCONNU (hérité du report §6, côté binaire) : sens de `[0x800C480C]`, valeur du registre `[0x80149CBC]`, contenu de `0x801ECA3C`, corps de cinq fonctions `GiveItem` non atteignables par le butin.
- INCONNU : si le `GiveItem` du binaire et le chemin `0x80034108` (opcode `0x82`) partagent le corps pour `0x24`/`0x29` ; la DLL actuelle (`EventProgramRunner.cs:3131-3138`) refuse au maximum, ce que `AlundraItemAndMoneyOpcodesTests.cs:149, 164` épinglent comme issu du binaire.
