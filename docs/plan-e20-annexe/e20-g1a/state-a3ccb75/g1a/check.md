# Contre-vérification du rapport « E20.g1a : état de la DLL et des tests au HEAD »

Base : HEAD `a3ccb759`, branche `chantier/restructuration-dll-r3`. Lecture seule : aucune écriture dans le dépôt, aucun build ni test.
Méthode : chaque ligne citée a été relue au HEAD et comparée à l'ancienne ligne par `git show 9ecc7d5:` ou `git show 2010ec3:`, avec le script
`.../scratchpad/g1-state/g1a-verif/show.py` (entrées `set1.txt`, `set1b.txt`, `set2.txt`, `set3.txt`, `set4.txt`). Les recherches ont été faites
avec `rg` et `git grep`.

## Erreurs

1. **Le registre de production est épinglé par deux tests, pas par un seul.** Le rapport (§3.1) dit que `AlundraNativeSpriteRegistryTests…:51/:55`
   est le « seul test qui bouge de certitude, 7 vers 8 à la phase C ». C'est faux. Un second test épingle aussi le compte en valeur absolue :
   `Alundra.Tests/AlundraWaveSwordLifeTests.cs:167`, `V3_TheProductionRegistryHoldsSevenKeys_TheWaveTypesHaveC61AndE10InLineAndNoOtherProgram_TheInvariantHoldsForThem`.
   - Le runner vient de `new EventProgramRunner(null, GameState.Instance)` (`:169`).
   - Le tuple `:181-183` commence par `(3 + 3 + 1, …)` contre `registry.Count`.
   - La phase C (T4) le fait passer au rouge, et son nom (« SevenKeys ») devient faux.
   - Le plan lui-même nomme ces deux épingles dans la section e0c (`docs/plan-e20-heros.md:6740` : « `AlundraNativeSpriteRegistryTests.cs:55` et
     `AlundraWaveSwordLifeTests.cs:181-183`, 7 »).
   - Conséquence : la liste prédite de 1.24.1 (« l'épingle du registre, phase C ; rien d'autre n'est attendu ») déclencherait l'arrêt « un test
     existant hors de la liste prédite bouge ».
2. **Inconnu 10 (ordre de `_spawnedEntities` après recyclage) : ce n'est pas un inconnu.** Le code tranche la question.
   - `InsertBySlot` (`Alundra/Maps/WorldProxy.cs:216-232`) insère chaque entité dans l'ordre des cases.
   - 1.28 l'écrit aussi (`docs/plan-e20-heros.md`, faits DLL, « `_spawnedEntities` dans l'ordre des cases, `WorldProxy.cs:216-232` »).
   - L'affirmation de `critic-complet/` M4 (« pas l'ordre des slots du binaire ») est donc contredite par le code et par `critic-recon/` (1e).
3. **« Q-G1A-1 : sous 1.28 … (option A de fait) »** (§4.2) va trop loin. Q-G1A-1 est toujours une question ouverte à l'auteur (section 2 du
   plan). `critic-complet/report.md:131` dit que les options B ou C « déplaceraient cet ordre ». Seul le défaut A tient tant que l'auteur n'a pas
   répondu.
4. **« g1a ne touche ni `XCollisionEntity` … »** (§4.2) est imprécis.
   - Vrai pour le seul écrivain du contact (`Alundra/Entities/EntityScriptProxy.cs:2424-2431`), que g1a ne modifie pas.
   - Mais la branche de suivi de D-G-3 met à nul `XCollisionEntity` de l'objet porté. C'est un des mutants de T5 (« `XCollisionEntity` non
     annulé »). Le binaire fait de même : la branche « porté » met `+0x130` à 0 (`docs/plan-e20-annexe/e20-g3/critic/report.md:35`, `0x80037E80`).
5. **Erreurs mineures de texte :**
   - **A24 :** les 10 sites `PlatformEntity` listés comprennent 4 lectures (Beannoïde `:360`, `:368` ; Muruta `:440`, `:448`). Les « 12
     écritures, comme `check.md` » se comptent 6 + 3 + 3 (`PlatformEntity` `:85`, `:84`, `:98`, `:2217`, `:138`, `:247` ; `CarriedEntity` 3 ;
     `RelativeWarpOffsetX` 3), pas 10 + 3 + 3.
   - **A9 et la ligne `WorldProxy.cs:2860-2920` de §1.1 :** les « ajouts » (re-scan, `BuildHitList`, `RunActiveEffectsPass`, coups) existaient
     déjà à `9ecc7d5:2879-2921`. Ce ne sont pas des ajouts au code, ce sont des passes que le texte du plan ne décrit pas. Le re-collect après
     `MovePlayer` (`WorldProxy.cs:2932-2936`) n'est pas cité.
   - **A4 :** « 0x05 à 0x0C sans cas » ne vaut pas pour 0x08. Comme 0x09, l'état 0x08 est dans `IsGroundAttackGroup`
     (`Alundra/Hero/PlayerManager.cs:1027`, et déjà à `9ecc7d5:1014`). Les états de g1a sans cas sont 0x05, 0x06, 0x07, 0x0B, 0x0C et 0x2E à 0x30.
     Constantes et chaîne vérifiées : `PlayerManager.cs:92-222` et `:414-609`.
   - **Ligne `EntityFlags.cs:274-290` de §1.1 et A12 :** la doc inversée se trouve en `EntityPickupKind.cs:17` (genre 2) et `:20-23` (genre 3).
     Les lignes `:14-15` (genre 1) sont justes.
   - **§0 :** `146e1d81` n'est pas « hors restructuration ». C'est R0, la première tranche du chantier (message « R0 parent part »). Il change
     bien un comportement (le `catch`).
   - **§3.1, test d'échelle :** « l'assertion reste vraie tant que `PlayerTryAction` est le bouchon » est trop prudent. Avec g1b, un héros qui
     porte passe à 0x0C ou 0x30, ce qui reste différent de `ClimbingAnimationId` (0x0E, `PlayerManager.cs:216`). L'assertion reste donc vraie
     après g1b aussi, si rien ne lève. INFERENCE.

## Omissions

1. **Le verrou du roulis de la 392 et le mode 1** (`critic-complet/report.md:44`, Inconnu 4 `:109`). Le rapport n'en dit rien.
   - Le roulis pose le bit 0x04, compris dans le masque 0x34. La DLL a le même masque : `InputBlockedMask` = 0x04 | 0x10 | 0x20
     (`Alundra/Session/GameState.cs:79`, `:103`).
   - Il fait donc entrer le héros dans la branche verrouillée, où W2 placera le mode 1.
   - Avec un objet en main, 5 ticks verrouillés incrémentent le compteur ; un 6ᵉ ferait descendre l'objet de 2 px (INFERENCE du critique).
   - Dans la DLL, A5r mesure 5 ticks verrouillés (images 221 à 225, `Alundra.Tests/AlundraShipCorridorArcTests.cs:226-227`). La durée côté
     binaire est inconnue.
   - Cela touche W2 et (w7) de g1a, et la recette et l'acceptation de g4b.
2. **Docs « jamais affecté » que g1a rend fausses.** Elles disent que `PlatformEntity` n'est jamais affecté ; g1a l'affecte.
   - `Alundra/Physics/EntitySupport.cs:10-11` (« never assigned by this runtime »), `:36` (« always true in this runtime's current scope »)
     et `:68-70`.
   - `Alundra.Tests/AlundraTerrainHeightTests.cs:33-36` : « never assigned anywhere », et « no test » de l'héritage de `TerrainHeight`, que
     D-G-3 introduit.
   - 1.28 met `:36` et `:68-70` dans le périmètre de g3 (`docs/plan-e20-heros.md:7069-7070`), donc après g1a. `:10` et le test n'ont pas de
     tranche.
   - `critic-recon/report.md` (Réfuté 3) relève `:36` et `:68-70`. Le rapport ne relève aucune de ces lignes.
3. **Recouvrement avec E20.e0c (§1.26, READY), dont l'ordre par rapport à g1a n'est pas fixé.** e0c est « hors de l'ordre des armes, objets et
   collisions », `docs/plan-e20-heros.md:726-729`. Elle modifie :
   - `WorldProxy.DestroyEntity`, avec le butin en tête (`:6774`, phase C `:6785`), le même site que W5 ;
   - `NativeSpriteRegistry.cs`, avec deux clés : le compte passe de 7 à 9, et les deux épingles ci-dessus sont dérivées (`:6814`) ;
   - `AlundraDestroyEntityBreakEffectTests.cs:204-219` ;
   - `ModdedPos`, rafraîchi en fin de passe (`:6751`, `:6783`), alors que D-G-3 « recalcule les modules ».
   - Si e0c passe avant g1a, la phase C va de 9 à 10, pas de 7 à 8. Le texte « 7 vers 8 » du rapport ne vaut que dans l'ordre inverse.
4. **Tests oubliés dans le recensement du T0 :**
   - `Alundra.Tests/AlundraEntityDestructionTests.cs:30-46`, `TR2_BuildCollidables_DropsAnEntityFlaggedForDestruction_AndKeepsTheOthers` : il
     contient un soulevable (`EntityFlags.Collidable | 0x200u`, `:36`), c'est donc une méthode « `Flags & 0x600` ». Il reste vert en g1a.
   - `Alundra.Tests/AlundraLootRollWorldTests.Rolls.cs:92-97`, `M3_…` : il fait naître l'enregistrement 4 de la carte 390, une « Caisse en bois
     générique » (`SpriteTableIndex` 36, `SpriteDirection` 192, donc type 292, un des 7 types câblés), par `SpawnEntityByRecordId`. La phrase
     « aucun test ne nomme l'un des 7 types câblés » ne vaut que pour les affectations littérales `SpriteType = …`.
   - Plus largement, les arcs `ArcRun` sur des cartes à caisses font naître des types câblés. Le rapport ne les compte pas comme « atteignant un
     type de l'ensemble câblé ».
5. **Contradiction non relevée sur le compte des placements.**
   - `check.md` (§2, `docs/plan-e20-annexe/e20-g1a/check.md:83`) attribue l'écart 2 251 / 2 252 à la bombe.
   - Or le plan compte déjà 1 bombe dans ses 2 251 (`docs/plan-e20-heros.md:6320` : 2 002 + 191 + 57 + 1).
   - `critic-complet/` (Réfuté 4) attribue l'écart à la carte 231, enregistrement 3.
   - `check.md:86` propose d'ajouter le type 35 à (C, 23) et (E, 2). Mais l'export lit ce placement comme « I05_Arc du chasseur » (genre 0,
     banque `alundra_35`). Enregistrer (C, 23) sur ce type n'est donc pas établi. Le rapport cite le point (g) sans cette contradiction.
6. **Signature du bouchon.** `UpdatePlayerCarriedEntity(EntityScriptProxy player, int mode)` (`PlayerManager.cs:1285`) n'a pas d'hôte. Le mode 1
   (destruction par `IEntityWorldContext.DestroyEntity`) devra le recevoir aux trois sites (`:408`, `:659` et W2), ou passer par
   `player.ScriptHost is IEntityWorldContext`. `WorldProxy` implémente les deux interfaces (`WorldProxy.cs:82`) ; `ScriptHost` est un
   `IScriptHost?` (`EntityScriptProxy.cs:76`).
7. **Autres implémentations de `IEntityWorldContext.DestroyEntity`** que W5 ne touche pas : `Alundra/Maps/NoOpEntityWorldContext.cs:28` et le
   `PlayerContext` de test (`AlundraHeroObjectTopsTests.cs:97-99`). Ce sont des no-op, sans effet attendu ; elles manquent au recensement.
8. **Historique (§2) :** pour `AlundraWorldProxy.cs`, il manque `2fb42f9c` (e0k2) et `bda82292` (`git log 9ecc7d5..2010ec3`).
   `AlundraGameplayFreeze.cs` (`82a979da`, le lecteur du marqueur) manque au tableau, même s'il est cité en A16.
9. **Plan à récrire, au-delà de la liste du rapport :**
   - le fait « Le trou du contact » (`docs/plan-e20-heros.md:6322`) et l'écart 8 (`:6337`), qui renvoient à Q-G1 ;
   - la ligne 12 de l'enveloppe (`:723`), dont les prérequis « E20.g1, E20.g2 » comprennent g1b (`critic-complet/` (1) demande l'inversion) ;
   - le périmètre de 1.28, qui attribue à g3 la correction du commentaire `WorldProxy.cs:2994-2995`, que le rapport relève seulement comme
     « périmé ».

## Confirmé (comptes)

- **Tableau 1.1 (citations du plan, base `9ecc7d5`) : 49 lignes sur 49 justes.** Chaque ancienne ligne et sa ligne HEAD désignent le même code.
  Deux sont imprécises : `WorldProxy.cs:2860-2920` et `EntityFlags.cs:274-290` (Erreurs 5).
  - Exemples relus : `PlayerTryAction` `9ecc7d5:927` → `PlayerManager.cs:940` ; `XCollisionEntity` `9ecc7d5:2355-2362` →
    `EntityScriptProxy.cs:2424-2431` ; étape (6) `9ecc7d5:3240` → `WorldProxy.cs:3315`.
  - Les renommages concordent avec `tools/dll-structure/table.json`.
  - `Alundra/Scripts/` n'existe plus.
- **Tableau 1.2 (`report.md` et `check.md`, base `2010ec3`) : 22 lignes sur 22 justes.** Elles ont été relues une à une sur `2010ec3` ; décalages
  de +13 (PlayerManager, EntityScriptProxy), +34 (WorldProxy), +6 (ScriptedMotion), +1 (EntitySupport).
  - Les citations existent bien dans les annexes (`report.md:250-271`, `check.md:96-101`).
- **Affirmations A1 à A25 : 25 verdicts sur 25 justes.** Trois ont un texte imprécis : A4, A9 et A24 (Erreurs 5).
  - Vérifié notamment : les 9 lecteurs de `HasLogicalZ` (`ScriptedMotion.cs:212`, `:685` ; `EntityScriptProxy.cs:794`, `:800`, `:852`, `:1480`,
    `:1485`, `:2382` ; `GameplayFreeze.cs:142`) et son poseur unique (`WorldProxy.cs:3573`) ;
  - la doc du marqueur, « ONLY », « never by the creations from a record » (`EntityScriptProxy.cs:1379-1380`) ;
  - `ClampToGround` puis `Teleport` dans `PushLogicalPositionToRoot` (`:2375`, `:2380`) ;
  - `EntitySearchService.cs:163` et `:249` ;
  - l'usage de `EntityDepthKey` limité à `IEffectAnchor.DepthKey` (`EntityScriptProxy.cs:463`) ;
  - le registre à 7 clés (`NativeSpriteRegistry.cs:39-41`) ;
  - le type de page `index | 0x100` (`EntitySpawnFactory.cs:541`) ;
  - les fiches 1, 2, 5, 6, 7, 43, 46, 47 et 48 du héros dans `alundra-project/Data/sprite-records.json` (5, 6, 7, 46, 47 et 48 ont
    `Acceleration` 0 ; 1, 2 et 43 ont `Acceleration` 1) ;
  - aucun site de code pour `0x80031C64`, `0x80032778`, `0x800327AC` ;
  - `NativeSpriteTypeFilterForTests` absent ;
  - prochain ADR libre : 0076.
- **Tests épinglés (§3) : 11 points sur 12 justes.** Le seul faux est « seul test » (Erreurs 1).
  - Comptes refaits identiques : `PlayerManager.MovePlayer(` dans 13 fichiers, 39 sites ; `ArcRun` 63 fichiers ; `JumpHeroRig` 17 ;
    `ContactWorld` 15 ; 307 fichiers `.cs` à la racine de `Alundra.Tests` ; `DestroyEntity(` 13, 5, 4, 4, 4, 3 ; 7 fichiers « crate / caisse » ;
    0 test qui nomme `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `TryLift` ou `PutInHands`.
- **Conséquences de 1.28 (§4) : 15 points sur 18 justes.** Les trois faux sont les Erreurs 2, 3 et 4.
  - Justes : l'ordre g1a, g2a, g2b, g3, g1b ; D-G-7 et O-G-1 caducs ; Q-G1 tranché B (`docs/plan-e20-heros.md:6264`) ; les prérequis de
    restructuration et Q-R-7 (`docs/plan-restructuration-dll.md:1234`, `check_names.py:96-98`) ; M3 de `critic-recon/` (plafond, saut en
    portant) ; le tick de `MovePlayer` (`:2927`) avant les listes (`:2962`) ; l'effacement de 0x80 par C23 dès g1a.
  - Les annexes sont bien en place : `goldens_g1a.json` = 950 vecteurs (8 clés), `goldens_carry_group.json` = 700 ; `c1_equality.py`,
    `c2_platform.py` et `c3_contact.py` absents de `docs/`, présents seulement dans `scratchpad/disc-e20g/critic/` ; pas d'annexe
    `docs/plan-e20-annexe/e20-g/`.
- **Noms (§5) : 9 lignes sur 9 justes.**
  - `Alundra/Ai/Objects/` existe (`SaveBook.cs`, namespace `Alundra.Ai.Objects`) ; `WaveSwordAi` est `internal static` avec `Register` et `Tick` ;
    l'arbre `Ai/Objects` et `Hero` est celui de l'ADR-0075 (D-R-1).
  - Aucun type `LiftableAi` ni `CarryRules` dans `Alundra/` ni `CasaEngineMonogame/` (`rg`).
  - Réserve : `check_names.py --table` ne contrôle que les collisions avec l'extérieur et les segments de namespace (`collision_findings`,
    `check_names.py:26-46`), pas les doublons dans la DLL. Le « 0 collision » repose donc aussi sur la recherche `rg`.
- **Restructuration pure :** contrôlée sur 5 fichiers (PlayerManager, EntityScriptProxy, WorldProxy, EntitySupport, ScriptedMotion). Dans le
  `--word-diff` de `146e1d81` à HEAD, aucun mot retiré ne manque du préfixe « Alundra ».

## Inconnus

1. Si e0c passera avant ou après g1a. Cela décide du compte du registre à la phase C (8 ou 10) et de la forme de `DestroyEntity` au moment de W5.
2. La durée du verrou du roulis dans le binaire (5 ou 6 ticks), donc l'effet de W2 sur un objet porté sur la 392.
3. Le type au chargement du placement 231 / enregistrement 3 (cruche ou arc), donc si le type 35 doit entrer dans (C, 23) ou en sortir.
4. Les points non relus ici, comme dans le rapport : les faits du binaire (adresses, oracle, mutants) et les chaînes d'animation 5→12, 6→47,
   etc. Je n'ai pas non plus relu les 5 autres fichiers du contrôle de pureté (MovementObstacleProbe, NativeSpriteRegistry, FrameSyncPasses,
   EntityDepthKey, EntityFlags).
