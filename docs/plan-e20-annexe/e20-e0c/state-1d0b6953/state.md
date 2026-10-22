# E20.e0c revision 3: DLL and tests at HEAD `1d0b6953` (2026-10-22), kept verbatim

Sections 1.5 to 1.7 of the reading (`../posed-rev3/report.md`), copied here as the state the plan's file:line citations refer to.

## 1.5 DLL au HEAD `1d0b6953` (fichier:ligne)

**Lâcher et apparition**
- Bouchon du lâcher : `Alundra/Maps/WorldProxy.cs:3301-3303`, doc `:3293`. Destruction légère : `:3329` et `Alundra/Maps/IEntityWorldContext.cs:90`.
- Apparition par indice : `IEntityWorldContext.cs:81`, `WorldProxy.cs:3488` et `:3601`. Z + 1 à `:3549`. Refus d'un monde nu à `:3498-3500`. Marqueurs de naissance à `:3570-3575`.
- Tirage à la création : `Alundra/Items/LootRoll.cs:54`, `:80`, `:111` ; `WorldProxy.cs:1119`, `:3440`, `:3566`, `RollLootAtCreation` à `:3469`.

**Champs de l'entité** (`EntityScriptProxy.cs`)
- `BlockedByEntity` à `:99`.
- `ContentsItemId` et `ContentsGameFlag` à `:106-107`.
- `ForceZ` à `:338`, `IsOnGround` à `:417`, `DamagedTickCounter` à `:483`.
- `ModdedPosX/Y/Z` à `:486`, `Width/Height/Depth` à `:488`.
- `Bytes` (`+0x274`, `byte[4]`) à `:1226`, `DelayOrAngleOrEntityId` (`+0x278`) à `:1228`, `ItemState` (`+0x27C`) à `:1229`, `AIValues` (`short[10]`, `+0x280`) à `:1230`.
- `ModdedPos` n'est écrit que par `Alundra/Entities/Spawning/EntitySpawnFactory.cs:825-827` et la copie `:2684-2686`. Aucun lecteur, et rien ne le rafraîchit (héros compris).

**Blocage** : le champ existe, il est lu mais jamais écrit hors tests.
- Lecteurs : `EntityScriptProxy.cs:1680` (choix du programme), `WorldProxy.cs:3062` et `:3072` (liste des coups), `Alundra/Hero/PlayerManager.cs:321` et `:1424`, `EventProgramRunner.cs:2644`.
- Non lu par l'horloge d'animation ni la physique (`WorldProxy.cs:2973` et `:2979`, `RunEntityBlock` `:2905-2990`).
- Aucun `Block` ni `Unblock`.

**Dialogue**
- `Alundra/Dialogues/DialogueDirector.cs:48` (masque par défaut 3), `:107` (`IsOpen`), `:220` (`Open(asset, node, mode)`), `:236-253` (mode 1 : `MessageBox` 0x10, sinon `MenuOpen` 0x08), `:385` (`SetCloseMask`).
- `Alundra/Dialogues/EtcStringTable.cs:122`, `:132`, `:143`. Précédent complet : `Alundra/Ai/Objects/SaveBook.cs:248` et `:261`.
- Manque : ouvrir une boîte sur un texte composé (nom + « gagné! »). `Open` exige un nœud Yarn.

**Portail et tick**
- Portail du monde : `Alundra/Session/GameState.cs:82`, `:85`, `:99` (`GameplayBlockedMask = MenuOpen | 0x40`).
- La BGM expose `IBgmFadeDirector.LoadBgm` (`Alundra/Audio/IBgmFadeDirector.cs:45`) et `StopAllSound` (`:33`) ; aucune coupe dédiée à la fanfare.

**Registre et effets**
- Registre natif : `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (production), `:54` (`Register`). Le créneau E, index 11, reste un no-op compté (`EventProgramRunner.cs:296-317`).
- Effets en file : `Alundra/Rendering/Effects/EffectStage.cs:192` (`Emit`). Précédent : `Alundra/Rendering/Effects/NativeEffects.cs:31` et `EventProgramRunner.cs:3146` (`UseLifeVessel`, ADR-0048), tirages à l'application.

**Aides existantes**
- Test d'axe de C255 : `Alundra/Combat/HitPass.cs:63` (`AxisOverlaps`).
- Garde de cible par `DamagedTickCounter` : `HitPass.cs:127`.
- Colonnes 0 et 1 de la table jetées : `Alundra/Items/ItemTables.cs:57-58`, `:81-90`, `:140`.
- Fonctions d'état : `PlayerManager.cs:1875`, `:1893`, `:1910`, `:1970`, `:2181`, `:2238`, `:2263`, `:2285`.

## 1.6 Tests d'aujourd'hui touchés (noms et chemins du HEAD)

**Phase B**
- `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:51-55` : `6 + 1`, à dériver.
- `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:169-190` : `3 + 3 + 1`, à dériver.

**Phase C**
- `Alundra.Tests/Rendering/Effects/DestroyEntityBreakEffectTests.cs:207-219` (`TheDestruction_DrawsNoNumber_AndCreatesNoEntity_EvenWithContents`) : épingle le bouchon.
- `Alundra.Tests/Ai/Enemies/BeannoideOracleTests.cs:373` (`Assert.Empty(SpawnedEntities)`), `:407-418` (160 entrées exclues, `spawn_39F58` et son 84), doc `:30`.
- `Alundra.Tests/Ai/Enemies/MurutaOracleTests.cs:453`, `:486-497`.
- `RunE1Call` fabrique un `new WorldProxy()` sans `InitializeWithWorld` : l'apparition y est refusée, il faut un monde initialisé.
- À relancer, prévision non mesurée : `Alundra.Tests/Arcs/BeannoideGrateArcTests.cs` et `MurutaHallArcTests.cs`.

**Hors tranche**
- `Alundra.Tests/Items/ItemAndMoneyOpcodesTests.cs:149`, `:164`, `:300` (opcode 0x82 inchangé).
- `Alundra.Tests/Items/LootRollWorldTests.Rolls.cs:204` (appel par réflexion de `RollLootAtCreation`, nom et signature épinglés).

## 1.7 Noms d'aujourd'hui

- La DLL est en un dossier = un namespace, sans préfixe `Alundra`. Les classes de test sont en miroir (ADR-0075, ADR-0080).
- La garde est `python -B tools/dll-structure/check_names.py --rules`. Elle est verte : 0 constat, 1 avertissement (`TextBoxGpu.cs:266`).
- Les noms proposés (`LootDrop`, `ItemEffects`, `PickupAi`, `HeroHeals`) sont libres dans la DLL, les tests et le moteur. La garde complète les valide à l'exécution.
- Prochain ADR libre : 0082, à revérifier sur toutes les branches au moment du commit.
