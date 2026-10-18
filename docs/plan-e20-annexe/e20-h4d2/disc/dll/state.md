# État de la DLL au HEAD `ed60a0eb` pour E20.h4d2 (lecture seule, 2026-10-18)

`git diff --stat f81304b HEAD -- Alundra Alundra.Tests alundra-project/Data` est vide : le code lu est celui de `docs/plan-e20-annexe/e20-h3/state-f81304b/`. Les chemins sont relatifs à la racine du dépôt.
Légende : FAIT = lu à la ligne citée ; INFÉRENCE = déduit, de quoi dit ; INCONNU.

## 1. Ce qui existe et que h4d2 réutilise

| Brique | Où (FAIT) | Usage par h4d2 |
|---|---|---|
| Naissance par indice | `Alundra/Maps/WorldProxy.cs:3488` (`SpawnEntityBySpriteIndex`), Z `unchecked(z + 1)` puis `- ModZ` `:3549` (D-E0A-1), balance `:3554`, marqueurs `JoinsBirthTick`/`HasLogicalZ` posés en dernier `:3572-3574` ; interface `Alundra/Maps/IEntityWorldContext.cs:81` (défaut `null`) | la colonne (type 20) et la boule (type 23) ; le retour peut être `null` et l'appelant doit le tester |
| Z du préfab à `OffsetZ` | `Alundra.Tests/AlundraSpawnBySpriteIndexInterfaceTests.cs:90-91` épingle déjà `PosZ = z + 1 + (18 << 16)` pour le type 20 | la colonne naît 18 px plus haut : clé `z = 1179650` (repère binaire) |
| Direction 0..31 sans normalisation | `Alundra/Entities/Spawning/EntitySpawnFactory.cs:834-836` | le tirage `Below(32)` passe tel quel |
| Mouvement : rampe d'accélération | `Alundra/Physics/ScriptedMotion.cs:620-638` (`Speed`, `Acceleration & 0xF`, `ForceStepX = abs(cible - force) >> accélération`) ; `RunOneKinematicTick :614` | la boule (vitesse 256, accélération 68 → 4) et l'anim 1 (vitesse 0, accélération 4) : la clé `fx` descend de 8688 par tick |
| Bord de carte | `ScriptedMotion.cs ClampStepToMapEdge` (E20.e0k3, D-E0K-7), seulement pour une entité `HasLogicalZ` | la clé de base touche les bords ouest et nord (exact) |
| Passe de coups | `Alundra/Combat/HitPass.cs:43` (`ClassMaskOf`), `:105`, `:167` (`attacker.HitCounter++`), `EntityScriptProxy.HitPass.cs:27` (`CurrentAttack`), `:30` (`HitAnimByte`) ; effet attaché 4 pour la nibble 10 (`EffectOfNibble`) | les touches de la colonne (10, 64) et des boules (10, 32) ; la boule porte `DeactivateOnHit` : `EntityScriptProxy.cs:1719` (`HitCounter != 0` → statut 3) |
| Statuts de l'`Update` | `EntityScriptProxy.cs:1703-1760` (branche Normal : `DeactivateOnImpact`/`OnHit`/`OnAnimationEnd` → `Deactivated`, programme E) ; l'`E1` en ligne `EventProgramRunner.cs:285-291` + `IsInlineDeactivateCode :328` (codes 0, 1, 10) | la colonne : fin d'animation → E1 en ligne → `DestroyEntity(e, -1)` (`BreakEffect` 0 : aucun effet) ; aucun gestionnaire à écrire |
| Registre natif | `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (`CreateForProduction`, 7 clés), `:54-65` (`Register`, doublon → exception `:60-63`), `TryGet` ; `EventProgramRunner.RunSpriteEvent :264-324` | `(C, 63)` pour {23} ; `(E, 3)` déjà inscrit par h4c1 pour {16} |
| Destruction | `WorldProxy.cs:3329` (`DestroyEntity(e)`, forme à un argument = `0x8003A774` `DestroyEntityNoFx` : statut `FlagToDestroy`, `EventTrigger` vidé) ; `:3301` (forme à deux arguments) ; `:3265` (`RecycleDestroyedEntities`, appelé `:2572` avant `RunEntityBlock`) ; `Interface :90` | l'annulation de la colonne ; l'identité changée = `Status == Destroyed \|\| Slot < 0` après recyclage (D-H4D1-3) |
| Créateur d'effets | `Alundra/Rendering/Effects/NativeEffects.cs:96` (`CreatedForTests`), modèle `CreateSprintBounce :161` ; file d'attente `player.ScriptHost?.EffectStage` (`IEntityWorldContext.cs:210`, `PlayerManager.cs:813-818`) | l'effet `(19, animation 2)` de l'annulation, à la position de la colonne ; **commun à h4d1 (E28)** : le créateur de D-H4D1-4 si h4d1 est passée, sinon h4d2 le crée au même nom |
| Tirage | `Alundra/Ai/EnemyHelpers.cs:35` (`Below(n)` = `hi(Next() * n)`), `Alundra/Rng/GameRandom.cs:34/54/65` (`RandomSeed`, `Next`, `AdvanceFrame`), `Alundra/Probes/RandomShadow.cs:27` | `Below(32)` (direction), `Below(9)` (délai) |
| Direction vers la cible | `Alundra/Events/ScriptHelper.cs:77` (`GetDirectionToTarget(int, int)`), déjà comparé au binaire par E14.a0b | C63 (différences 16.16 de 32 bits) |
| Entités du monde | `IEntityWorldContext.cs:35` (`SpawnedEntities`, liste compacte dans l'ordre des emplacements, héros en tête) ; `EntityScriptProxy.Slot :82` | l'annulation (toutes les entités vivantes) et la requête (`Slot`) |
| Champs | `EntityScriptProxy.cs` : `Slot :82`, `Status :88`, `BlockedByEntity :99` (= `+0x20`), `TargetDirection :159` (= `+0x8C`), `ForceZ :338`, `Slope_18c :474`, `DamagedTickCounter :483` (= `+0x1CC`), `FrameCollisionTickCounter :484` (= `+0x1D0`), `HitCounter :498`, `Bytes :1226` (`byte[4]` = `+0x274`), `DelayOrAngleOrEntityId :1228` (`int` = `+0x278`), `ItemState :1229` (`int` = `+0x27C`), `AIValues :1230` (`short[10]`) ; zone « Hero only » `:432-442` | voir §3 |
| Point d'appel du pilote | `Alundra/Hero/PlayerManager.cs:353` (appel de `UpdateItemUseDriver`), `:1507-1509` (corps **vide**) ; annulation `CancelItemMode :740-742` (vide), appelée `:664` par le groupe `Skip` ; `TryUseItem :932` (bouchon) | les trois entrées de h0a (départ, pilote, annulation) pour l'objet `0x2F` |

## 2. Ce qui n'existe pas encore (prérequis d'autres tranches)

- `Alundra/Combat/TargetQuery.cs` (h4b1) : `TargetQuery.Find` et ses réglages `InclusiveBound` / `SortThreshold`. h4d2 l'appelle ; **sans h4b1 faite, arrêt**.
- Les trois coutures `ItemStartForTests` / `ItemDriverForTests` / `ItemCancelForTests` et les champs `ItemInUse` / `ItemUseCounter` / `ItemUsePhase` (h0a), la fonction de départ des tomes, les états `0x32`-`0x34` et la garde de pente (h4a).
- `Alundra/Ai/Items/EarthTome1Ai.cs` (h4c1) : `ImpactDeactivate` (E3), `SpriteTypes = {16}`, `Register`.
- Le champ « Hero only » `ItemSpawnedEntity` (D-H4D1-3, h4d1) : h4d2 le crée au même nom si h4d1 n'est pas passée.
- Les accesseurs communs 32 bits de `+0x274` (Q-H4B-5, défaut : « communs, avec la première tranche qui en a besoin » = h4d2).
- Le membre de l'interface qui donne le plus haut emplacement : `WorldProxy.HighestLiveSlot()` (`:182`) est **privé** ; h4d2 n'en a pas besoin (cf. rapport §3.3 : le maximum à la demande des `Slot` des entités vivantes égale le compte stocké du binaire sur 494 appels de la requête).

## 3. Où va chaque pièce de h4d2 (proposition ; noms libres, contrôlés par `check_names.py`)

| Pièce | Emplacement | Remarque |
|---|---|---|
| Pilote `0x80035A84` | `Alundra/Ai/Items/FireTome1Ai.cs` (neuf, Q-R-7), méthode `Drive(héros)` | lit `ItemUsePhase` / `ItemUseCounter` (avant incrément, a5 de h0a) ; garde de h4a ; naissances par `SpawnEntityBySpriteIndex` |
| Annulation `0x80034D2C` | `FireTome1Ai.Cancel(héros)` | boucle sur `SpawnedEntities` (toutes les entités vivantes, `Slot ≥ 1`) ; `SpriteType` 20 au statut `Normal` : effet `(19, 2)` en `(PosX, PosY, PosZ + 1)` puis `DestroyEntity(e)` ; `SpriteType` 23 au statut `Normal` : `Status = Deactivated`, `BlockedByEntity = null` |
| C63 `0x8007AC60` | `FireTome1Ai.BallTick(entité, contexte, état)` + `Register(registry)` : `(ProgramCTick, 63)` pour `{23}` | la requête via `TargetQuery.Find` ; le tour via `ScriptHelper.GetDirectionToTarget` |
| E3 pour le type 23 | `EarthTome1Ai.SpriteTypes` : `{16}` → `{16, 23}` (aucun `Register` neuf : un doublon lève une exception) | le corps d'E3 ne change pas |
| Sprite 20, E1 | rien (E1 en ligne) | |
| Sprite 23, A254 | rien (compté, sans gestionnaire, comme A0 du 16) | |
| Réglage du tour | `FireTome1Ai.ShortestTurn` (valeur de production : défaut de Q-H4D2-1), couture `[ThreadStatic] ... ForTests` | comme `InclusiveBound` / `SortThreshold` de D-H4B1-2 |
| Champs | `EntityScriptProxy` : accesseur 32 bits de `+0x274` ; `SpellTarget` (référence propre à la boule, non clonée) ; `DelayOrAngleOrEntityId` reçoit le `Slot` de la cible (0 = aucune) | `ItemSpawnedEntity` partagé avec h4d1 |
| Câblage de production | `PlayerManager.cs` : les trois entrées `0x2F` des tables de h0a (départ = fonction de h4a, pilote = `Drive`, annulation = `Cancel`) ; `NativeSpriteRegistry.CreateForProduction :36-43` : `FireTome1Ai.Register(registry)` | phase B |

## 4. Tests existants qui bougent ou non

- **Bougent (liste prédite)** : `Alundra.Tests/AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`, titre `:50`) et `Alundra.Tests/AlundraWaveSwordLifeTests.cs:181-183` (`3 + 3 + 1`) : **+1** (la clé `(C, 63)`), réécrits par dérivation (« le compte d'avant plus les clés de la tranche », bloc du §1.24 point 4) ; au HEAD de départ de h4d2 ils portent déjà les clés de h4c1 (`(E, 3)`) et des tranches passées avant.
- **Tests de h4c1 qui changent** : (e2) « `(E, 3)` pour le type 16 seul (pas 17 ni 23) » devient « `(E, 3)` pour `{16, 23}` (pas 17, 24, 150) » ; le mutant de h4c1 « E3 inscrit pour `{16, 23}` » n'est plus un mutant (il devient la vérité de production) ; (p4) de h4c1 (`UnhandledWeaponSpriteCodes({16}, …)`) est inchangé.
- **Ne bougent pas** (FAIT, lus) : `AlundraWeaponSpawnSeamTests.cs:83-107` (S1) et `:238-259` (S5) (les tomes ne sont pas des armes : `ProductionWiredWeaponTypes` reste `{27, 28, 29}`, `PlayerManager.cs:1226`) ; `AlundraBirthTickTests.cs:474-477` (type 20 : un seul `OneFrame`, aucun gestionnaire inscrit pour le 20) ; `AlundraSpawnBySpriteIndexInterfaceTests.cs:90` (Z du 20) ; `AlundraBirthBoxTests.cs:131` (naissance de tous les préfabs sans tick) ; les tests de Z logique (`FireBall = 11`, `AlundraLogicalZStepTests.cs:28`) : le type 11, pas le 23 ; `AlundraNativeSpriteRegistryTests.cs:242-249` (compte relatif). Aucun test ne fait naître le type 23 ni ne tombe sur `(C, 63)`.
- La liste prédite se recompte au HEAD de départ (T0).

## 5. Les sprites de l'export (`alundra-project/Data/sprite-records.json`, `BankKey`)

| Clé | GUID | En-tête | Animations | Boîtes (`map_alundra.json`, `CollisionData`) |
|---|---|---|---|---|
| `alundra_20` (colonne) | `71a00844-709a-5a06-845d-17bd4400b0e8` ; préfab `alundra-project/Entities/Magie de feu Niv.1/` | `MoreFlags` 68 (0x44), `CanPickup` 32, `FlagsPortraitShadowType` 0 → drapeaux **`0x2044`** = `HitsClassA` + `DeactivateOnAnimationEnd` + `NoObstacleSlide` ; programmes A0 C0 D0 **E1** ; `BreakEffect` 0, `Contents` 0 ; corps (-10, -7, -18), 21×15×26 | 1 jeu d'animation (vitesse 0, accélération 192), 29 images (+1 terminale), fin `Hold`, **87 ticks** | (-12, -8, -18), **23×16×47** à toutes les images |
| `alundra_23` (boule) | `c51a4b3f-e968-5237-9962-de746d78d5cd` ; préfab `Projectile de magie de feu Niv.1/` | `MoreFlags` 116 (0x74), `CanPickup` 32, `FlagsPortraitShadowType` 16 → **`0x102074`** = `HitsClassA` + `DeactivateOnImpact` + `DeactivateOnHit` + `DeactivateOnAnimationEnd` + `NoObstacleSlide` + `DestroyOnSlidingSlope`, **sans** `Gravity` ; programmes **A254 C63 D0 E3** ; `BreakEffect` 0, `Contents` 0 ; corps (-7, -5, 0), 14×12×12 | anim 0 : vitesse **256**, accélération 68, 4 images, `Loop`, 9 ticks ; anim 1 : vitesse 0, accélération 4, 5 images, `Hold`, 10 ticks | anim 0 : (-12, -8, 0) 23×15×15 ; anim 1 : idem, sauf la rangée « up » qui a 23×23×15 (-12, -12, 0) aux deux premières images |
| fiches d'équilibrage (`BALANCE.BIN.json`, `alundra-project/Data/balance.json`) | | 20 : niveau 255, PV 0, réponses toutes `0x80`, une attaque type 10 / attribut 10 / **puissance 64** ; 23 : idem, **puissance 32** ; 25 (la cible de test) : PV 1 (posés à 5000 par le banc), réponses toutes 16, aucune attaque | | |

## 6. INCONNU

- Le verdict de `check_names.py --tree` pour les nouveaux fichiers (non lancé : le mode `--tree` lit l'arbre) ; `collision_findings` sur les noms candidats `FireTome1Ai`, `TargetQuery`, `EarthTome1Ai`, `FireTome2Ai` ne rend aucune collision (FAIT, lancé en lecture seule).
- La sortie de la DLL : rien n'a été construit ni exécuté (règle de la tâche).
