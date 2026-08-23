# Plan E3 — Collisions sur le moteur (champ de hauteur `AlundraCells` + mover conscient de la politique)

Date : 2026-08-23. Étape E3 de [plan-conversion-totale.md](plan-conversion-totale.md) (décision D4 :
tout sur le moteur physique, `PhysicsEngine.cs` non porté). Chantier **moteur** architectural :
chaque tranche passe par un plan-verifier avant exécution et un verifier frais après.

## 0. Décisions de l'utilisateur (2026-08-23, ne pas re-débattre)

| # | Décision |
|---|---|
| E3-1 | **Pose logique ≠ pose de rendu** : la racine des prefabs porte `(X, Y profondeur, Z élévation)` en pixels Alundra ; un `RenderProjectionComponent` enfant porte le sprite et dérive la pose de rendu via la politique `TopDownElevation` (`SimulationSpacePolicy.cs:135` : rendu `(X, −(Y − Z), 0)`). Les 395 prefabs sont restructurés par le convertisseur ; la DLL écrit la pose logique. |
| E3-2 | Le mover accepte une fixture **Box ou Capsule** ; avec une boîte, le sol est échantillonné aux **4 coins** (max), comme `ComputeEntityGroundHeight` (`PhysicsEngine.cs:956-1073`). |
| E3-3 | **Le moteur intègre la verticale** (gravité le long de l'axe « haut » de la politique, vitesse terminale, atterrissage), réglages en px/s² dérivés des propriétés de map (`Gravity`, `ZViscosity`). Écart assumé : arcs de saut non identiques au tick 50 Hz. |
| E3-4 | **Horizontal = `Move(déplacement)` par tick** : la DLL garde l'intégration fidèle d'E2 (`IncrementForce`, vitesse par animation) et passe le déplacement au mover, qui résout murs, marchabilité et sol. Même chemin pour les PNJ (E4). |

## 1. Faits qui bornent le plan

Moteur (`CasaEngineMonogame/`) :
- `TopDownElevation` : espace logique `X est, Y profondeur, Z élévation` ; `DeriveRenderPosition` =
  `(X, −(Y − Z), 0)` (`CasaEngine/Engine/Physics/SimulationSpacePolicy.cs:125-137`) ;
  `RenderProjectionComponent.UpdateProjection` place le composant à la position de rendu dérivée de la
  racine (`Components/RenderProjectionComponent.cs:51-77`) ; un `AnimatedSpriteComponent` peut vivre
  sous une projection (phase E, `docs/engine/collision-2d-3d-architecture.md:500-507`) ; le tri visuel
  reste à `DepthSortable2DComponent` (« la politique fournit l'élévation, elle ne trie pas »).
- `ICollisionField` : contrat **Y-up, X/Z horizontaux** (`ICollisionField.cs:9`),
  `TrySampleGround(worldPosition, maxDrop, out GroundSample{HasGround, GroundHeight, Normal, IsWalkable, SurfaceTag})` ;
  `HeightGridCollisionField(origin, cellSize, width, depth, heights[], walkable[]?, tags[]?)` à
  cellules **carrées** ; slot `World.CollisionField` (`World.cs:59`) **sans consommateur**.
- `CharacterControllerComponent` : `Vector3.Up` codé en dur (gravité `:580`, snap `:815`), sol par
  sweep Bepu (`:817-818`, `:903-912`), capsule exigée (`ValidateDependencies :240-243`,
  `FindCapsuleFixture :865-877`), `Move(Vector3)` pour un déplacement externe (`:338-360`),
  `IsGrounded` (`:93`), modes `Player/AI/Script/Cutscene/Disabled`, réglages
  (`CharacterControllerSettings.cs:40-73` : Radius, Height, SkinWidth, Gravity 9.81, StepHeight 0,
  MaxSlopeAngle 45, GroundSnapDistance 0.15, pas de vitesse de chute max). Prérequis documentés :
  mover conscient de la politique, helper pied/demi-hauteur, équilibre `groundY + Height/2`
  (`docs/engine/character-controller-features.md:310-351`).
- Physique : bepuphysics2 3D ; `ShapeSweep` ; tests existants `CasaEngine.Tests/Physics/CollisionFieldTests.cs`,
  `CharacterControllerComponentTests.cs`, `SimulationSpacePolicyTests.cs`.

Original (`alundra-datas-analyser/…/PhysicsEngine.cs`) :
- Unités : positions 16.16 ; `MapTile.Height × 16` px (`:1007`) ; `TileZ = PosZ >> 20` (`:1700`) ;
  boîte de collision `Mod*`/`Width/Height/Depth` (`EntityManager.cs:160-199`, `ModdedPos = Pos + Mod`).
- Sol : 4 coins de la boîte (`:960-971`), cellule par coin (clamp 0..51/0..59), hauteur selon
  `Slope & 3` : 0 plat, 1 escalier `(H−1)×16 + 16 − (y % 16)`, 2/3 échelles via `g_heights_800236d4`
  (`:1007-1061`), **max** des 4 coins.
- Verticale : `ForceZ −= Gravity << 8` par tick, borne `ZViscosity << 8` (`:1385-1400`, `:1462-1472`) ;
  `IsOnGround = FloorHeight ≥ PosZ` (`:1704`) ; atterrissage `PosZ = TerrainHeight + 1`, `ForceZ = 0`
  (`:123-135`) ; clamp au sol au spawn (`EntityManager.cs:127-136`).
- Horizontale : une cellule bloque si `((Walkability | GroundProperty << 8) & classe) != 0` ou si sa
  hauteur `≥ ModdedPosZ` (`GetCollisionFlags :1137-1166`, classe = `0x40` | `0x01` ClassB | `0x1000`
  ClassA) ; marche autorisée si la nouvelle hauteur ≤ `0x30000` (3 px) au-dessus (`:436-475`) ;
  recherche dichotomique + glissement par axe (`:364-844`) ; glissade par attribut de tuile
  (`XForceTable/YForceTable >> SlideEffectId`, `:1514-1548`). Les piles de murs (`WallTiles`) sont
  **rendu seulement**.
- Map 389 : `Gravity 128`, `ZViscosity 4096`, `SlideEffectId 0` ; `AlundraCells` par cellule :
  `walkability`, `ground_property`, `slope`, `height`, `flags` ; ex. (18,57) `height 5` = 80 px.

Données/DLL (ce repo) : les prefabs G2 ont racine `AnimatedSpriteComponent` + enfant
`CollisionComponent` (boîte du header, ex. Alundra 21×15×32, `local_position z 16`) ; la DLL écrit la
**pose de rendu** dans la racine (`AlundraWorldProxy.ResolveWorldPosition :785-792`,
`worldY = −pixelY + élévation`) ; `PlayerStart` des worlds en pose de rendu `(804, −952, 0)`.

## 2. Enveloppe du programme

- **Résultat** : sur la map 389, Alundra (puis les PNJ en E4) se déplace par le mover du moteur : les
  cellules non marchables et les dénivelés > 3 px bloquent, la hauteur de sol est suivie, la gravité
  et l'atterrissage sont calculés par le moteur ; la cinématique horizontale reste celle d'E2.
- **Non-objectifs** : collisions entité-entité (`TouchingEntity`, plateformes, `RidingEntity` →
  slots D/F, E4/E14) ; glissade par attribut de tuile (`XForceTable`, `SlideEffectId`) — à traiter en
  E4 avec les pentes d'autres maps ; mutation de tuiles (E7) ; saut du joueur (E4+) ; IA.
- **Propriétaires** : convertisseur (ce repo), DLL (ce repo), moteur (submodule — commits propres,
  puis bump du pointeur). Un seul committeur par repo à la fois.
- **Prérequis** : E1/E2 livrés (`7e40549`), fins d'animation (`bf63341`).
- **Acceptation globale** : E3.a → 389 visuellement identique ; E3.b → tests de champ sur les
  cellules réelles ; E3.c → tests moteur du mover sous `TopDownElevation` ; E3.d → Alundra marche sur
  le pont, bloquée par les bastingages/mâts, suit les hauteurs ; 304+ tests DLL, 129+ convertisseur,
  moteur sans nouvel échec (18 préexistants).
- **Rollback** : une tranche = un commit ; revert du commit (+ pointeur de submodule) ; les prefabs se
  régénèrent par export.
- **Budget/arrêts** : une tranche = un commit + un verifier, au plus deux tours de correctifs par tranche ; arrêt si la restructuration des prefabs casse le tri de profondeur (E3.a) ou si
  le mover moteur ne peut pas accepter un champ en espace logique sans casser les tests existants
  (E3.c) ; question à l'utilisateur dans ces deux cas.

## 3. Tranches

### E3.0 — Composant racine inerte `TransformComponent` ✅ (moteur ab4314e1, verifier CONFIRMED ; différé P3 : `AddChildComponent` n'invalide pas le cache de politique, hors chemin de chargement)

- **Pourquoi** : `SceneComponent` est abstrait (`SceneComponent.cs:16`) et aucune sous-classe concrète
  n'est inerte (AnimatedSprite, ChildActor, Light, ParticleSystem, PlayerStart, RenderProjection,
  TileMap, StaticSprite, Camera*, Physics*, Primitive) ; une entité dont la racine porte une **pose
  logique pure** n'a donc pas de type de racine aujourd'hui.
- **Scope** : nouveau `TransformComponent : SceneComponent` dans
  `CasaEngine/Framework/Scene/Entities/Components/` — aucun état propre, aucun visuel propre ;
  constructeur public sans paramètre + constructeur de copie, `Clone` ; résolu par `ElementFactory`
  par nom simple (`Activator.CreateInstance`, pas de `[Browsable(false)]` sinon la sérialisation
  l'ignore, `EditorEntityJsonSerializer.cs:415-418`) ; sérialisé par la branche générique
  `case SceneComponent` (`:247-260`, enfants via `:263-285`) et chargé par `SceneComponent.Load`
  (`:396-412`). **Contrat de bornes** : l'AABB d'une entité n'est construite que depuis la racine et
  les composants de niveau entité, jamais depuis les enfants (`Entity.cs:358-371`), et l'héritage
  rend une boîte de 0,5 unité autour du composant lui-même (`SceneComponent.cs:227-232`) ; la
  requête de frustum du `World.Draw` (`World.cs:711-732`) ne dessinerait donc rien. Décision
  utilisateur (2026-08-23) : **bornes de rendu seules** — le `TransformComponent` surcharge
  `GetBoundingBox` pour renvoyer l'union des boîtes de ses descendants **dessinables**
  (`IComponentDrawable`/`IBoundingBoxable` visuels, récursivement ; les composants physiques
  — `PhysicsBaseComponent` et dérivés — sont exclus : ils vivent dans le monde physique), repli sur
  la boîte héritée s'il n'a aucun descendant dessinable. **Invalidation** : l'index spatial
  enregistre la boîte à l'ajout et efface le drapeau (`World.cs:711-715`), et
  `World.IsBoundingBoxDirty` n'inspecte que la racine et les composants de niveau entité
  (`World.cs:494-510`) ; `RenderProjectionComponent.UpdateProjection` doit donc **marquer la racine
  de son entité « dirty »** (`IsBoundingBoxDirty`) chaque fois que sa position projetée change — y
  compris au premier update après l'ajout, où la boîte indexée date d'avant toute projection.
  **Interdiction de surcharger `Update` et `Draw`** : la propagation héritée aux enfants
  (`SceneComponent.cs:351-374`, `Entity.Draw → RootComponent.Draw`, `Entity.cs:506-513`) est ce qui
  fait tourner `RenderProjectionComponent.Update` et dessiner le sprite ; « inerte » = sans état ni
  visuel propre,
  pas sans propagation. **Politique d'entité** : sans composant physique, une entité projetée résout
  en `StaticMaterialAnimated` (index statique, tick conditionnel — `EntityPolicyResolver.cs:113-139`,
  `EntityPolicies.cs:47-57`) : `WorldIndex.Move` n'est alors jamais appelé et `Update` n'est pas
  garanti. `RenderProjectionComponent` implémente donc `IEntityPolicyDefaultsProvider` et contribue
  `EntityPolicySet.DynamicDefault` (index dynamique + tick chaque frame) — une pose de rendu dérivée
  bouge par construction par rapport à sa racine ; `TransformComponent` ne contribue rien. Vérifier
  que la combinaison retenue ne déclenche pas `GetSuspectCombinationReason`
  (`EntityPolicyResolver.cs:52-62`). **API additive** (fichiers du scope : `SceneComponent.cs`,
  `RenderProjectionComponent.cs`, nouveau `TransformComponent.cs`) : `public void
  MarkBoundingBoxDirty()` sur `SceneComponent` (`IsBoundingBoxDirty` est `protected set`,
  `SceneComponent.cs:223`) ; `RenderProjectionComponent.UpdateProjection` l'appelle sur
  `Owner.RootComponent` uniquement quand la position projetée change (y compris au premier update).
  Documenter l'addition d'API selon `.github/copilot-instructions.md` (« Public API and compatibility »).
- **Acceptation** : tests moteur — (1) load/clone d'une entité `TransformComponent` →
  `RenderProjectionComponent` → `AnimatedSpriteComponent` préservant la hiérarchie, round-trip JSON,
  `ElementFactory.Create<SceneComponent>("TransformComponent")` ; (2) **bornes** : racine
  `TransformComponent` en (0, 952, 0) avec projection + sprite (fixture **sans** composant physique)
  sous un monde `TopDownElevation`, **après un `Update`** → `Entity.GetBoundingBox()` contient
  (0, −952, 0) et ne contient pas (0, 952, 0) ; avec une `CollisionComponent` ajoutée sous la racine,
  la boîte est inchangée (exclusion des physiques) ; (3) **propagation** : un composant sonde enfant
  observe exactement un `Update` et un `Draw` par appel sur la racine ; (4) **index** : l'entité de
  (2) ajoutée au world et **jamais déplacée** est, après un `World.Update`, renvoyée par
  `SpatialServices.WorldIndex.Query` pour un frustum autour de la pose de rendu et absente d'un
  frustum autour de la pose logique (échoue sans le marquage dirty de la projection **ou** sans la
  contribution de politique : un test montre `ResolveRuntimePolicies(entity).UsesDynamicSpatialMaintenance == true`) ;
  (5) `MarkBoundingBoxDirty()` appelée de l'extérieur rend `IsBoundingBoxDirty == true` sur la racine ;
  `CasaEngine.Tests` sans nouvel échec (18 préexistants).
- **Rollback** : revert dans le submodule. **Budget** : un commit, ≤ 1 demi-journée ; arrêt si la
  sérialisation générique n'accepte pas une sous-classe sans champ.

### E3.a — Pose logique dans les prefabs et la DLL ✅ (verifier CONFIRMED ; tri murs/sprites à confirmer à l'œil par l'utilisateur)

- **Prérequis** : E3.0 commité et pointeur de submodule bumpé (le convertisseur référence le
  moteur pour écrire les prefabs).
- **Scope** : convertisseur — chaque prefab `.entity` devient : racine **`TransformComponent`**
  (pose logique) → enfants : `RenderProjectionComponent` → `AnimatedSpriteComponent` (le sprite ;
  `DepthSortable2DComponent` reste sur l'entité, il lit la position monde du composant sprite,
  `AnimatedSpriteComponent.cs:320-325`), et `CollisionComponent` sous la racine (données inchangées ;
  sa pose effective devient la pose logique — effet voulu, sans consommateur runtime aujourd'hui) ;
  `PlayerStart` émis en pose logique `(804, 952, 0)` (`World.CreateLocalPlayerController` copie la
  transform du PlayerStart sur la racine du pawn, `World.cs:361-364`). DLL — **seuls les trois sites
  qui écrivent `RootComponent.LocalTransform.Position = ResolveWorldPosition(...)`** changent
  (spawn des records, `AdoptPlayerPawn`, `SyncTransform` — `AlundraWorldProxy.cs:749-752, :1056-1059,
  :1555-1557`) : ils écrivent la pose logique `(pixelX, pixelY, élévation px)` ; `ResolveWorldPosition`
  devient `ResolveLogicalPosition`. **Re-projection même frame** : `RenderProjectionComponent.
  UpdateProjection()` tourne dans la passe des composants, **avant** `GameplayProxy.Update`
  (`Entity.cs:473-504`) ; après avoir écrit la racine, `SyncTransform` appelle
  `UpdateProjection()` (méthode publique, `RenderProjectionComponent.cs:51-77`) sur la projection
  de l'entité, sinon le sprite rend la pose de la frame précédente. `WallPlacementOverlay.
  ApplyEntitySortKey` continue de lire le `PosY` logique (`WallPlacementOverlay.cs:368-402`) ; caméra
  debug et backdrop (cible caméra) sont intouchés.
- **Non-goals** : aucune collision, aucun `CharacterControllerComponent`.
- **Acceptation** : export complet 0 erreur ; un prefab généré montre `root_component.type =
  TransformComponent` → `RenderProjectionComponent` → `AnimatedSpriteComponent` ; test DLL de
  **formule** (`policy.DeriveRenderPosition(logique) == ancien ResolveWorldPosition` sur les 19
  records de la 389 et le héros) ; test d'**ordre** : après un `Entity.Update` au cours duquel le
  proxy a changé la pose logique, la position monde du `AnimatedSpriteComponent` vaut
  `DeriveRenderPosition(nouvelle pose)` dans la même frame (échoue sans la re-projection) ; test de
  **bornes/index** : après cette frame, `Entity.GetBoundingBox()` contient la pose de rendu du sprite
  et l'entité est renvoyée par `SpatialServices.WorldIndex.Query` autour de cette pose (sinon le
  frustum du world culle l'entité) ; renommage `ResolveWorldPosition → ResolveLogicalPosition` avec
  mise à jour des 4 sites de test en pose de rendu (`AlundraWorldProxyTests.cs:198-278`,
  `AlundraWorldProxyEntityManipulationTests.cs:185`, `AlundraWorldProxySpawnInitializationTests.cs:387`) ; tri murs/sprites inchangé (capture avant/après par
  l'utilisateur) ; 304 tests DLL et 129 convertisseur verts ; harnais à 926. Les tests d'ordre et de
  bornes exigent un `World` dont `PhysicsWorld.SpacePolicy` est `TopDownElevation` : réutiliser le
  montage de `Alundra.Tests/WallPlacementOverlayTests.cs:566-580`.
- **Rollback** : revert + export. **Budget** : un commit convertisseur + DLL ; arrêt si le tri de
  profondeur change à l'écran.

#### Réalisé — écarts (2026-08-23)

- **Structure des prefabs** : `Writers/SpriteWriter.cs` (`WriteEntityPrefab`) construit maintenant
  `TransformComponent` (racine) → `RenderProjectionComponent` → `AnimatedSpriteComponent` ; quand le
  header déclare une boîte de corps positive, `CollisionComponent` est ajouté comme enfant de la
  RACINE (frère de la projection, pas du sprite) — vérifié sur l'export complet :
  `alundra-project/Entities/Alundra/Alundra.entity` montre exactement cette chaîne
  (`root_component.type = TransformComponent` → `RenderProjectionComponent` →
  `AnimatedSpriteComponent`, plus `CollisionComponent` en second enfant de la racine) ;
  `DepthSortable2DComponent` reste au niveau entité, inchangé. Les 11 prefabs sprite-only obtiennent
  la même racine + projection, sans `CollisionComponent`.
- **Effet de bord de la boîte de collision** : conforme au plan — la pose effective de
  `CollisionComponent` est désormais la pose LOGIQUE de l'entité (racine directe), pas sa pose de
  rendu ; aucun consommateur runtime aujourd'hui (E3.b/E3.c la brancheront).
- **`PlayerStart`** : `Writers/WorldWriter.cs` (`ResolveTileCentreSpawn`) émet la pose logique
  `(X pixelX, Y pixelY non inversé, Z élévation en px)`. Map 389 → `(804, 952, 0)`, vérifié sur
  l'export complet (`Ship Klark (beginning)-389.world`, entité `PlayerStart`). Le placeholder
  centre-de-map (les 482 autres worlds) suit la même règle (`(centreX, centreY, 0)`, non inversé).
- **DLL — trois sites de pose** : `AlundraWorldProxy.ResolveWorldPosition` renommée
  `ResolveLogicalPosition`, ne négocie plus Y et laisse l'élévation sur Z (au lieu de la replier dans
  Y) ; les trois sites (`CreateEntityFromPrefab` ~:749-762, `AdoptPlayerPawn` ~:1056-1074,
  `SyncTransform` ~:1548-1587) écrivent la pose logique puis ré-projettent.
- **Re-projection même frame** : un champ engine-only `AlundraEntityScriptProxy.RenderProjection`
  (résolu une fois par `entity.GetComponent<RenderProjectionComponent>()` au spawn/adoption, jamais
  par frame) est mis en cache et sa `UpdateProjection()` est appelée explicitement juste après
  chaque écriture de racine — y compris dans `SyncTransform`, le site qui tourne chaque frame depuis
  `AlundraEntityScriptProxy.Update` (donc APRÈS que la mise à jour naturelle des composants ait déjà
  projeté l'ancienne pose plus tôt dans le même `Entity.Update`). Vérifié négativement : l'appel
  retiré temporairement dans `SyncTransform` fait échouer les deux tests d'ordre/bornes
  (`AlundraEntityLogicalRenderPoseTests`), remis en place ensuite (326/326 verts).
- **`WallPlacementOverlay.ApplyEntitySortKey`**, caméra debug, cible caméra (backdrop) : intouchés,
  comme prévu — seul un commentaire de la caméra debug a été corrigé (il citait l'ancienne
  négation directement dans `ResolveWorldPosition`, qui n'existe plus sous ce nom/cette forme).
  Recherche (`grep`) confirmée : aucun AUTRE lecteur de `RootComponent...Position` comme coordonnée
  de rendu dans `Alundra/Scripts` en dehors des trois sites listés ci-dessus.
- **Ids déterministes des nouveaux composants** : NON appliqué — écart documenté, pas deviné.
  `ObjectBase.Id` a un setter privé, assignable uniquement via `Load(JObject)` ; `SpriteWriter`
  construit les composants en mémoire (jamais via `Load`), donc aucun composant du prefab —
  ni les anciens (`AnimatedSpriteComponent`, `CollisionComponent`) ni les deux nouveaux
  (`TransformComponent`, `RenderProjectionComponent`) — ne reçoit d'id `Ids.For(...)` ; seul
  `entity.Id` lui-même reste non déterministe, exactement comme documenté avant cette tranche (voir
  le commentaire de classe de `SpriteWriter`, section « Asset ids are NOT forced deterministic
  here »). Aucun consommateur ne dépend d'un id de composant stable aujourd'hui.
- **Tests** : convertisseur 129/129 verts (structure de prefab et `PlayerStart` mis à jour dans
  `SpriteWriterBodyPrefabTests.cs`/`WorldWriterTests.cs`, sans test supplémentaire) ; DLL 326/326
  verts (304 existants, mis à jour sur les 5 sites `ResolveWorldPosition`/pose de rendu attendue
  identifiés par le plan, + 22 nouveaux dans `Alundra.Tests/AlundraEntityLogicalRenderPoseTests.cs` :
  19 cas de formule sur les enregistrements réels de la 389, 1 cas de formule sur le héros, 1 test
  d'ordre, 1 test de bornes/index) ; harnais intro toujours à la frame 926.
- **Export complet** : 0 erreur, compteurs inchangés (`Worlds` 483, `Entities.Prefabs` 395,
  `Sprites.QuadsRead` = `Sprites.QuadsConverted` 160355, `SpriteRecords.IdsvAnimDirs`/
  `Verify.Loaded.anim2d` 9620).
- **Tri visuel** : non re-vérifié à l'écran par l'utilisateur dans cette session (capture
  avant/après demandée par le plan mais hors du périmètre d'un agent headless) — signalé comme
  écart à valider, pas deviné.

### E3.b — Champ de collision Alundra ✅ (moteur c8798c59 + DLL, verifiers CONFIRMED)

- **Découpage** : deux commits ordonnés, un seul committeur par repo — (1) **moteur** (submodule) :
  contrat d'axes + masque ; bump du pointeur dans le parent ; (2) **DLL** (parent) :
  `AlundraCellsCollisionField` + installation dans `World.CollisionField`. La moitié DLL dépend de la
  nouvelle signature : elle ne démarre qu'après le bump.
- **Moteur — contrat d'axes (option a)** : `ICollisionField` : « haut = axe d'élévation déclaré par le
  champ, qui doit coïncider avec celui de la politique du monde ; `GroundHeight` est mesurée le long
  de cet axe ». `HeightGridCollisionField` reçoit un paramètre **additif** `Vector3 up` (ou la
  politique) dont la valeur par défaut reste `Vector3.Up` — les 14 tests de `CollisionFieldTests.cs`
  restent verts sans changement ; sous `up = Vector3.UnitZ` (`TopDownElevation`), les deux axes
  horizontaux sont X et Y et la hauteur est lue/retournée sur Z. Un test moteur échantillonne une
  grille Z-up et obtient l'élévation sur Z. E3.c consomme cette grille Z-up pour ses tests.
- **Moteur — masque (additif, sans rupture)** : méthode d'interface **par défaut**
  `bool TrySampleGround(in Vector3 p, float maxDrop, uint walkabilityMask, out GroundSample s)
  => TrySampleGround(p, maxDrop, out s)` ; l'ancienne signature reste l'unique méthode abstraite ;
  `HeightGridCollisionField` et `SquareCollisionField` (tests) ne changent pas. Note d'API additive
  selon `.github/copilot-instructions.md`.
- **DLL — `AlundraCellsCollisionField : ICollisionField`** (espace logique X/Y/Z, `up = UnitZ`),
  construit une fois par world dans `InitializeWithWorld` depuis `AlundraCells` (parseur tolérant sur
  le modèle de `WallPlacementOverlay.TryParse`, `:115-135` ; largeur/hauteur = dimensions du
  `TileMapData` (52 × 60, `cell_count` = 3 120) ; table `g_heights_800236d4` copiée de
  `StaticVariables.cs:532-541` avec adresse), installé dans `World.CollisionField` (le `World.Clear()`
  le remet à null : réinstallation à chaque chargement). Règles, toutes en pixels absolus :
  - cellule = `(x / 24, y / 16)`, **clampée** à `[0,51] × [0,59]` (original `PhysicsEngine.cs:979-989`) ;
    un point hors grille a donc un sol (écart assumé vis-à-vis du contrat générique « hors grille =
    pas de sol », documenté) ;
  - hauteur (port de la branche « premier coin » de `ComputeEntityGroundHeight`, `:1007-1061`) selon
    `slope & 3` : 0 → `h × 16` ; 1 (escalier) → `(h − 1) × 16 + 16 − (y % 16)` ; 2 (échelle entrante)
    → `(h − 1) × 16 + table[(23 − x % 24) % 24]` ; 3 (échelle sortante) → `(h − 1) × 16 + table[x % 24]`.
    **Écart documenté** : l'original accumule `slopesHit` sur les 4 coins et ajoute 16 aux coins
    suivants d'une empreinte à cheval sur plusieurs cellules en pente ; le champ par point ne le fait
    pas (le mover d'E3.c prend le max des 4 coins) ;
  - `HasGround` = vrai en toute position (clamp) ; `GroundHeight` retournée **même si elle est
    au-dessus du point échantillonné** (l'original renvoie toujours la hauteur et le mover décide :
    `IsOnGround = FloorHeight ≥ PosZ`, atterrissage par clamp, marche bloquée au-delà de 3 px) ;
    `maxDropDistance` **ignoré** (pas de limite de chute dans l'original) — les deux écarts sont notés
    dans le doc du champ et repris par E3.c ;
  - `IsWalkable` = `((walkability | ground_property << 8) & walkabilityMask) == 0`
    (`GetCollisionFlags :1137-1166`, même formule dans `GetCollisionFlagsWithPlayer :1087-1099`) ;
    `SurfaceTag` = `ground_property` (texte) ; `Normal` = `UnitZ`.
  - **Masque** (arrêté, plus de question ouverte) : fourni par l'appelant, dérivé par entité :
    `0x40 | (ClassB ? 0x01 : 0) | (ClassA ? 0x1000 : 0)` depuis `proxy.Flags` (bits `EntityFlags` à
    lire dans `Gameplay/EntityFlags.cs` ; la DLL renseigne déjà `Flags` au spawn —
    `AlundraWorldProxy.cs:894, :1042` — et la note « Flags non renseigné » de
    `Alundra/Scripts/EntityFlags.cs:19-22` est périmée, à corriger). Le champ ne fige aucune constante.
  - **Hors champ** (règles du mover, E3.c/E3.d) : « cellule bloquante si hauteur ≥ Z de l'entité »
    (`:1159`), clauses `g_gravityFlag`/`(tileFlags & 0xE00) == 0x0800` (`:1113-1124`) et
    `g_warpLockTimer` (`:1108`).
- **Non-goals** : consommateur runtime (E3.c), glissade par attribut, mutation de cellules (E7).
- **Acceptation** (tests nommés, valeurs calculées à la main depuis `AlundraCells` de la 389) :
  - moteur : grille Z-up échantillonnée → élévation sur Z ; les 14 tests existants inchangés ; la
    surcharge par défaut du masque délègue à l'ancienne méthode ; `CasaEngine.Tests` sans nouvel échec.
  - DLL, cellules réelles de la 389 : (18,57) plat h5 → **80 px** ; escalier (13,27) `slope 5` h10 →
    y = 27×16 + 0 → **160 px**, y = 27×16 + 8 → **152 px** ; échelle entrante (17,40) `slope 6` h8 →
    x = 17×24 + 0 → 112 + table[23] = **128 px**, x = 17×24 + 23 → 112 + table[0] = **113 px** ;
    échelle sortante (18,48) `slope 7` h6 → x = 18×24 + 0 → 80 + table[0] = **81 px**, x = 18×24 + 23
    → **96 px** ; marchabilité (18,15) `walkability 1` : masque `0x40` → marchable, masque `0x41`
    (classe B) → non marchable ; (18,38) `ground_property 128` : masque `0x8040` → non marchable ;
    hors grille (x = −10, y = 5 000) → clamp (0, 59), `HasGround` vrai ; point sous la surface
    ((18,57) à Z = 10 px) → `GroundHeight` 80 ; `maxDrop` 0 → même résultat.
- **Rollback** : revert du commit moteur + pointeur ; revert du commit DLL. **Budget** : deux commits,
  ≤ 1 journée. **Arrêt** : si le paramètre d'axe additif ne peut pas garder les 14 tests verts, ou si
  `World.CollisionField` n'est pas accessible au `InitializeWithWorld` du proxy.

#### Réalisé — écarts (2026-08-23)

- **Moteur** : déjà livré avant cette tranche (`ICollisionField.TrySampleGround(in Vector3, float,
  uint, out GroundSample)` par défaut, `HeightGridCollisionField(..., Vector3? up = null)`,
  `World.CollisionField` settable/reset par `World.Clear()`) — aucune modification moteur nécessaire
  dans cette moitié DLL.
- **`Alundra/Scripts/AlundraCellsCollisionField.cs`** : deux types — `AlundraCellsRecords` (parseur
  tolérant de la propriété custom `AlundraCells`, même patron que
  `WallPlacementOverlay.TryParse`/`WallPlacementRecords`, colonnes `walkability`/`ground_property`/
  `slope`/`height` seulement — `tile_id`/`palette`/`tile`/`flags`/`wall_tiles_offset`/`wall_tiles` sont
  ignorées par `JsonSerializer.Deserialize`, non consommées par les règles de cette tranche) et
  `AlundraCellsCollisionField : ICollisionField` (espace logique, `up = Vector3.UnitZ` implicite via
  `Normal = UnitZ`). `TryCreate(TileMapData, worldName, out field)` vérifie en plus que
  `cell_count`/longueur des colonnes correspondent à `TileMapData.MapSize.Width * Height` (52×60=3120
  sur la 389) — un mismatch dégrade (avertissement, pas de champ) exactement comme un JSON absent ou
  malformé.
- **Mapping case 2 / case 3 lu dans la décompilation** (`PhysicsEngine.cs:1030-1035` pour le cas 2,
  `:1046-1051` pour le cas 3) : cas 2 (`slope & 3 == 2`, "ladders entering or stair side down") →
  `xIndex = (23 - x % 24) % 24` (`:1034`) ; cas 3 (`slope & 3 == 3`, "ladders exiting") →
  `xIndex = x % 24` directement (`:1050`) — confirmé conforme à l'énoncé du plan, aucun écart. La
  table `g_heights_800236d4` (24 octets, `StaticVariables.cs:531-541`) est copiée dans
  `AnimationTables.HeightsTable_800236d4` avec le commentaire d'adresse `0x800236d4`.
- **Bit ClassB** : `0x00000008` (`Gameplay/EntityFlags.cs:53` côté décompilation, `Alundra/Scripts/
  EntityFlags.cs:62` côté DLL — bit 3, "the entity belongs to collision/damage class B" ; ClassA reste
  `0x00000001`, bit 0). `WalkabilityMaskFor` : `0x40 | (ClassB ? 0x01 : 0) | (ClassA ? 0x1000 : 0)`,
  port exact de `GetCollisionFlagsWithPlayer` (`PhysicsEngine.cs:1085-1099`) et `GetCollisionFlags`
  (`:1139-1149`) — les deux méthodes partent de `flag = 0x40`, passent à `0x41` sous ClassB, OR `0x1000`
  sous ClassA.
- **Note périmée corrigée** : `Alundra/Scripts/EntityFlags.cs` (doc de classe) affirmait que `Flags`
  n'était pas encore renseigné au spawn ; corrigé — `AlundraWorldProxy.CreateEntityFromPrefab` (~:894)
  et `AdoptPlayerPawn` (~:1042) le renseignent bien depuis le header (`EntityManager.cs:92-93`), donc
  toutes les branches qui lisent un bit de `EntityFlags` sont actives, pas dormantes.
- **Installation** : `AlundraWorldProxy.InitializeWithWorld` construit le champ juste après la
  résolution de `tileMapData` (avant l'application des overlays mur/sol et avant `AdoptPlayerPawn`/le
  spawn des records) et l'installe à la fois sur `world.CollisionField` et sur une propriété publique
  `AlundraWorldProxy.CollisionField` (pour les tests) ; en mode dégradé les deux valent `null` — un
  seul avertissement déjà logué par `AlundraCellsCollisionField.TryCreate`/`AlundraCellsRecords.TryParse`.
- **Valeurs de test confirmées sur les données réelles de la 389** (`AlundraCells` de
  `Ship Klark (beginning)-389.tileMap`, indexées `y*52+x`) : (18,57) `slope=4` (`&3=0` plat) `h=5` →
  **80 px** ; (13,27) `slope=5` (`&3=1` escalier) `h=10` → y=432→**160 px**, y=440→**152 px** ;
  (17,40) `slope=6` (`&3=2` échelle entrante) `h=8` → x=408→**128 px**, x=431→**113 px** ; (18,48)
  `slope=7` (`&3=3` échelle sortante) `h=6` → x=432→**81 px**, x=455→**96 px** ; (18,15)
  `walkability=1` → masque `0x40` marchable, `0x41` non marchable ; (18,38) `ground_property=128` →
  masque `0x8040` non marchable ; hors grille (−10, 5000) → clamp (0,59), `HasGround` vrai ; sous la
  surface (Z=10 sur (18,57)) → `GroundHeight` 80 inchangé ; `maxDrop=0` vs `maxDrop` large → même
  résultat (le paramètre est ignoré, écart documenté ci-dessous). **Aucun désaccord** entre les valeurs
  à la main du plan et la formule portée — pas d'arrêt nécessaire.
- **Écarts documentés** (identiques à ceux annoncés dans le plan, confirmés à l'implémentation) :
  bump `slopesHit` multi-coins (+16 sur les coins suivants d'une empreinte à cheval sur plusieurs
  cellules en pente, `PhysicsEngine.cs:1021-1024/:1037-1040/:1053-1056`) non reproduit — le champ ne
  voit qu'un point, E3.c prendra le max des 4 coins sans reproduire ce bump exact ; `HasGround` toujours
  vrai (clamp hors grille) ; `GroundHeight` renvoyée même au-dessus du point échantillonné ;
  `maxDropDistance` ignoré. `SurfaceTag` = `ground_property` en texte, mis en cache par valeur distincte
  au constructeur (`string?[256]`), aucune allocation par appel.
- **Tests** : `Alundra.Tests/AlundraCellsCollisionFieldTests.cs`, 18 nouveaux (3 parsing synthétique,
  2 `TryCreate` synthétiques, 4 `WalkabilityMaskFor`, 9 sur les données réelles de la 389 listées
  ci-dessus) — 344/344 verts (326 existants + 18). `dotnet build` solution 0 erreur ; `dotnet build
  Alundra/Alundra.csproj` 0 erreur ; harnais intro non affecté (aucune logique d'event program ni de
  spawn touchée par cette tranche).

### E3.c — Mover conscient de la politique ✅ (moteur dbda6359, verifier CONFIRMED ; différés : composition de l'empreinte si la CollisionComponent n'est pas sous la racine (P3), repli de RequestDash en X/Z (P4), coût de ResolveFootUpCoordinate sans champ (P4), assertion du plateau −800 indirecte (P4))

Propriétaire : moteur seul (un commit dans le submodule, puis bump du pointeur). Unités = unités
monde (pixels pour Alundra) ; les défauts « mètres » existants restent.

- **C1 — Axe haut de la politique (API additive)** : `SimulationSpacePolicy` gagne
  `public virtual Vector3 Up => Vector3.Up;` (`SimulationSpacePolicy.cs`), surchargé en
  `Vector3.UnitZ` dans `TopDownElevationSimulationSpacePolicy`. Le mover résout
  `Owner?.World?.PhysicsWorld?.SpacePolicy?.Up ?? Vector3.Up` au début de chaque `Update` et en
  dérive sa **base** `(up, h1, h2)` = axe haut + les deux autres axes dans l'ordre X→Y→Z (up = Y →
  h1 = X, h2 = Z ; up = Z → h1 = X, h2 = Y). Sans monde/politique : base Y-up = comportement actuel.
  Note d'API additive selon `.github/copilot-instructions.md`.
- **C2 — Changement de base complet** (pas seulement les sites `Vector3.Up`) : toutes les méthodes
  qui codent X/Z horizontaux et `velocity.Y` vertical passent par la base : `ApplyHorizontalVelocity`
  (`CharacterControllerComponent.cs:532-542`), `GetDesiredHorizontalVelocity` (`:946-949` : intent.X
  sur h1, −intent.Y sur h2 — même signe qu'aujourd'hui ; sous `TopDownElevation` Y logique croît vers
  le bas de l'écran, donc stick haut = Y décroissant), `ApplyDashVelocity` (`:544-558`),
  `ApplyVerticalVelocity` (`:560-586`, gravité et saut sur la composante `up`), `TryStepMove`
  (`:751`), `UpdateGround` (`:794-838`) et les 7 sites `Vector3.Up` (`:664, :762, :775, :784, :815,
  :832, :925`). Sous `Identity3d`/`Planar2d` la base est exactement (Y, X, Z) : comportement
  inchangé, `CharacterControllerComponentTests` inchangés et verts ; `TopDownElevationDemo` inchangée.
- **C3 — Fixture Box ou Capsule** : `ValidateDependencies` (`:240-244`) et
  `TryResolveCollisionDependencies` (`:855-859`) acceptent une fixture `Box` **ou** `Capsule` (la
  première trouvée, capsule prioritaire si les deux). Forme de requête : Capsule → inchangé
  (`Settings.Radius/Height`, `GetSweepShape :879-893`) ; **Box → forme `Box` de la taille de la
  fixture rétrécie de `SkinWidth` sur chaque face** (le backend l'accepte,
  `BepuQueryShapeBackend.cs:54-67`) ; `Settings.Radius/Height` restent propres à la capsule (la
  validation `Height ≥ 2×Radius` subsiste — E3.d fournit des valeurs plausibles). **Espace du pied et
  de l'empreinte** : pose de la fixture = transform locale de la `CollisionComponent` ∘
  `ColliderFixture.LocalPosition`, exprimée **dans l'espace de la racine** ; `FootOffset` = (centre
  de la fixture · up) − (demi-étendue de la fixture le long de up) ; empreinte = les 4 coins
  (centre ± demi-étendues sur h1/h2) à la hauteur du pied. Boîte G2 d'Alundra (`local_position
  (0.5, 0.5, 16)`, taille 21×15×32, transform de la `CollisionComponent` identité) → pied z = 0.
  Capsule : pied = centre − (Height/2), empreinte = 4 points du rayon.
- **C4 — Sol depuis le champ (remplace le sweep)** : quand `World.CollisionField != null`, le
  champ est la **seule** source de sol d'`UpdateGround` (le sweep de snap `:815-827` est sauté ;
  chemin sans champ inchangé). Sonde de sol : pour chaque coin de l'empreinte, origine =
  coin + `StepHeight`·up, `maxDropDistance = StepHeight + GroundSnapDistance`, masque =
  `Settings.WalkabilityMask` ; sol = **max** des `GroundHeight` des coins avec `HasGround` (aucun →
  pas de sol). **Origine de sonde couvrant le pas** : `origine = pied_avant_pas + max(StepHeight,
  |Δ vertical du pas|)` (pied avant le déplacement vertical de ce tick), `maxDropDistance =
  (origine − pied_après_pas) + GroundSnapDistance`. Règle : si `pied_après_pas − GroundSnapDistance ≤
  sol ≤ origine` → au sol : la racine est déplacée le long de up de `sol − pied_après_pas` (remontée
  au sol traversé, ou snap), vitesse verticale annulée, `IsGrounded = true` ; sinon en l'air
  (gravité). Un sol traversé pendant le pas est ainsi toujours retrouvé (origine au-dessus de lui :
  HeightGrid le renvoie ; Alundra renvoie `GroundHeight ≥ pied`) — port du clamp inconditionnel
  de l'original (`PosZ = TerrainHeight + 1`, `:123-135`). La branche champ est évaluée **avant** la
  sortie anticipée `GroundSnapDistance <= 0` d'`UpdateGround` (`:807-811`). L'empreinte est celle de
  la fixture **pleine** (non rétrécie de `SkinWidth`). Les deux conventions de champ donnent la même décision : un sol
  ≤ pied + StepHeight est sous la sonde (HeightGrid le renvoie ; Alundra aussi), un sol plus haut que
  l'origine (plus de `StepHeight` au-dessus du pied, sans chute dans ce pas) est au-dessus de la
  sonde (HeightGrid « pas de sol », Alundra hauteur > sonde) — traité « pas au sol » ici et
  « bloqué » par C5. **Séquence** : `Move(d)` ne résout que le déplacement horizontal (`:338-360`,
  inchangé) ; la résolution de sol reste dans `Update` (`UpdateGround`), donc un `Move` est toujours
  suivi d'un `Update(dt)` pour voir son effet sur le sol.
- **C5 — Règle horizontale depuis le champ** : tout déplacement horizontal (`Move(d)` ou vitesse
  d'intention) est résolu **axe par axe, h1 puis h2** : pour l'axe courant, calculer les 4 coins de
  l'empreinte à la position cible ; sonder chaque coin avec origine = coin + `StepHeight`·up,
  `maxDropDistance = float.MaxValue` (pour qu'une descente de corniche reste autorisée avec
  HeightGrid), masque des réglages ; l'axe est **bloqué** (déplacement sur cet axe mis à 0 — pas de
  déplacement partiel, simplification documentée de la recherche dichotomique
  `PhysicsEngine.cs:364-475`) si un coin a `!HasGround` (hors champ) ou `!IsWalkable` ou
  `GroundHeight > pied + StepHeight` (port de « cellule bloquante si hauteur ≥ Z » `:1159`, avec la
  marche de 3 px `:436-475`). Puis sweeps contre les corps physiques comme aujourd'hui.
- **C6 — Verticale** : `v_up −= Gravity × dt` ; si `MaxFallSpeed > 0`, `v_up ≥ −MaxFallSpeed` ;
  atterrissage par C4 ; saut inchangé (sur up).
- **C7 — Réglages** (`CharacterControllerSettings.cs` : propriété, constructeur de copie
  `:16-38`, `Load :97-121`, `Validate :123-184`, `Clone`) : `uint WalkabilityMask` (clé
  `walkability_mask`, **défaut 0 = aucune classe ne bloque** dans la polarité du champ Alundra
  `(flags & mask) == 0` ; E3.d fournit le masque par entité) ; `float MaxFallSpeed` (clé
  `max_fall_speed`, **défaut 0 = non borné**, `Validate : ≥ 0`). `StepHeight` garde son défaut 0
  (E3.d pose 3).
- **Non-goals** : pentes (`MaxSlopeAngle` inchangé), pathfinding, entité-entité (sweeps existants
  inchangés), `slopesHit` multi-coins, glissade par attribut.
- **Acceptation** (tests `CasaEngine.Tests/Physics/`, monde `TopDownElevation` construit comme dans
  `CharacterControllerComponentTests.cs:723-737`/`:787` ; champ `HeightGridCollisionField(origin 0,
  cellSize 16, width 8, depth 8, up: UnitZ)` ; pawn Box 16×16×32, fixture `local_position z 16` →
  pied 0 ; réglages `StepHeight 3`, `GroundSnapDistance 4`, `Gravity 1250`, `MaxFallSpeed 800`,
  `SkinWidth 0.5`, `WalkabilityMask 0`, `Radius 8`, `Height 32`) :
  1. **Politique** : `Up == UnitZ` pour `TopDownElevation`, `Vector3.Up` pour `Identity3d` et
     `Planar2d` ; un contrôleur sans `World` reste Y-up (test existant inchangé).
  2. **Base** : sous Z-up, **sans champ installé**, racine (24, 24, 500) (hors de portée de toute
     sonde), après un `Update` avec intention horizontale (1, 0) et vitesse verticale initiale −10 :
     la composante Z de la vitesse vaut −10 − 1250·dt (pas remise à 0), la composante X a
     progressé, Y inchangée.
  Chaque scénario 3/4/5/7 appelle `Move(...)` **puis exactement un `Update(1/50)`** (intention
  nulle) avant ses assertions.
  3. **Plat** : hauteurs 0 ; racine (24, 24, 0) ; `Move(8, 0, 0)` + `Update` → racine **(32, 24, 0)**,
     `IsGrounded == true`.
  4. **Falaise** : colonnes x ≥ 64 à hauteur 32 ; racine (48, 24, 0) ; `Move(20, 0, 0)` (coins cibles
     jusqu'à x = 76 → cellule 4, sol 32 > 0 + 3) → racine **inchangée (48, 24, 0)**.
  5. **Marche** : colonnes x ≥ 64 à hauteur 2 ; même déplacement → racine **(68, 24, 2)**
     (autorisé, pied snappé à 2), `IsGrounded == true`.
  6. **Chute, clamp et atterrissage** : hauteurs 0 ; racine (24, 24, **400**), pas au sol ;
     `Update(1/50)` répété 2 s (chute ≈ 0,8 s) : la composante Z de la vitesse **atteint −800** (à la
     précision près) puis **y reste** jusqu'à l'atterrissage ; à la fin racine z == **0**,
     `IsGrounded == true` (échoue sans le clamp).
  6 bis. **Sol traversé en un pas** : hauteurs 0 ; racine (24, 24, 8), pas au sol, vitesse verticale
     initiale −16 px/tick (−800 px/s) ; un `Update(1/50)` : le pas nominal mène à z = −8, la sonde
     (origine 8 + 16 = 24, `maxDrop` 24 + 8 + 4) retrouve le sol 0 → racine z == **0**,
     `IsGrounded == true`, vitesse verticale 0 (échoue avec une sonde à `pied + StepHeight` seul).
  7. **Non marchable** : `walkable[]` faux pour x ≥ 64 ; `Move(20, 0, 0)` depuis (48, 24, 0) →
     **bloqué**, racine inchangée.
  8. **Box** : avec la fixture ci-dessus, `FootOffset` = 0 le long de up ; avec une fixture Box un
     `Move` contre un corps physique statique est bien **bloqué par le sweep Box** (pas le repli
     « passe-à-travers » `:677-682`) — le `FakePhysicsWorldContext` du fichier de tests est étendu
     (additif) pour accepter une forme `Box` et enregistrer ses dimensions
     (`CharacterControllerComponentTests.cs:862-867` n'accepte qu'une capsule aujourd'hui).
  9. **Réglages** : round-trip `Load`/`Clone` des deux nouveaux réglages ; `MaxFallSpeed < 0` → rejet.
  10. Les tests existants de `CharacterControllerComponentTests` (Y-up, capsule, sweep) inchangés et
      verts ; `CasaEngine.Tests` sans nouvel échec (18 préexistants) ; `CasaEngine.MonoGame.sln` 0
      erreur.
- **Rollback** : revert dans le submodule + pointeur. **Budget** : un commit, ≤ 2 journées ; au plus
  deux tours de correctifs. **Arrêt** : si le changement de base casse un test existant sans
  solution additive, ou si le backend ne supporte pas la forme de requête Box.

### E3.d — Branchement Alundra ⏳ (moteur sérialisation, puis convertisseur + DLL)

- **Découpage** (un seul committeur par repo, ordre strict) : (1) **E3.d.0 moteur** —
  `EditorEntityJsonSerializer` (CasaEngine.EditorServices) ne sait pas sauver un
  `CharacterControllerComponent` (`EntityComponent`, branche `default:` → `type` seul,
  `EditorEntityJsonSerializer.cs:211-261`) alors que `Load` lit `settings` et `control_mode`
  (`CharacterControllerComponent.cs:477-489`) : ajouter `case CharacterControllerComponent` écrivant
  `settings` (mêmes clés que `CharacterControllerSettings.Load :97-132`, nouveaux réglages compris) et
  `control_mode` ; test de round-trip `SaveEntity → Entity.Load` sur chaque réglage ; commit moteur puis
  bump du pointeur ; (2) **convertisseur** ; (3) **DLL** (parent, un commit pour 2+3).
- **Convertisseur** (`Writers/SpriteWriter.cs`, `WriteEntityPrefab`) : sélecteur du héros =
  `bank.IsAlundraBank && bank.Sector5Id == 0` (clé `alundra_0`, dossier `Entities/Alundra`,
  `SpriteBankReader.cs:187, :211`) ; **ce seul prefab** reçoit un `CharacterControllerComponent` (les
  394 autres : aucun — E4 décidera pour les PNJ). Réglages à l'export, en pixels : `Radius 7.5`,
  `Height 32` (≥ 2×Radius ; contraintes capsule de `Validate :137-199`, sans effet sur le sweep Box
  qui lit la fixture 21×15×32), `SkinWidth 0.5` (< Radius), `StepHeight 3` (`PhysicsEngine.cs:436-475`),
  `GroundSnapDistance 4`, `MaxSlopeAngle` défaut, `Gravity 0`, `MaxFallSpeed 0`, `WalkabilityMask 0`,
  `control_mode Player` — `Gravity`/`MaxFallSpeed`/`WalkabilityMask` sont **écrasés au runtime** par la
  DLL (ci-dessous) car ils dépendent de la map et de l'entité. Test convertisseur : exactement un
  prefab porte le composant, avec ces valeurs ; export complet 0 erreur, compteurs d'E3.a inchangés.
- **DLL — adoption** (`AdoptPlayerPawn`) : `controller = pawn.GetComponent<CharacterControllerComponent>()`
  (absent → avertissement unique, comportement E2 conservé) ; `controller.Settings.Gravity =
  (mapGravity << 8) / 65536 × 2500` (389 : **1 250 px/s²**), `MaxFallSpeed = (mapZViscosity << 8) /
  65536 × 50` (**800 px/s**), `WalkabilityMask = AlundraCellsCollisionField.WalkabilityMaskFor(proxy.Flags)`
  (propriétés `Gravity`/`ZViscosity` du tilemap, lues comme `AlundraCells`) ; `ControlMode` laissé à
  `Player` tel que posé par `Possess` (`ControllerPossessionTests.cs:20-29`, `World.cs:350-364`) ; **aucun
  `SetMoveIntent` n'est jamais appelé** : l'intention reste nulle, `Move(d)` est la seule source
  horizontale ; pendant `ControlLocked`, `MovePlayer` n'appelle pas `Move` (gate E2) — pas de changement
  de mode. **Clamp au spawn** (`EntityManager.cs:127-136`) : après l'écriture de la pose New Game,
  `field.TrySampleGround(pied, maxDrop ∞)` → si `GroundHeight > PosZ`, `PosZ = GroundHeight` ; puis
  `controller.Teleport(pose logique)` ; `IsOnGround = 1` jusqu'au premier update du contrôleur
  (`CharacterMotionSystem` l'enregistre à la frame suivante, `CharacterMotionSystem.cs:96-98`).
- **DLL — propriété de la racine par frame** (ordre moteur : `World.Update` → `RuntimeSystems.Update`
  → `CharacterMotionSystem.UpdateControllers` (gravité/sol, déplace la racine) → `Entity.Update`
  (saute le contrôleur, piloté par système, `Entity.cs:480`) → `GameplayProxy.Update`) : pour une entité
  **pilotée par contrôleur** (le héros), **la racine est la source de vérité** :
  1. en tête de `AlundraEntityScriptProxy.Update` : `Pos* ← racine` (pull, conversion `(int)
     MathF.Round(px × 65536)`, arrondi au plus proche pour éviter la dérive) ; `IsOnGround ←
     controller.IsGrounded` (après le premier update contrôleur) ;
  2. `MovePlayer` puis `Tick` : pour chacun des 0..4 sous-pas 50 Hz, l'intégration fidèle d'E2 produit
     `ΔPosX/ΔPosY` (16.16) → `controller.Move((ΔX >> 16) en float px, (ΔY >> 16) en float px, 0)` (on
     passe la fraction : `Δ / 65536f`) → `Pos* ← racine` (re-pull) avant le sous-pas suivant ; un axe
     bloqué laisse `Force*` inchangés (pas de `ForceAdjusted` — écart documenté) ;
  3. `SyncTransform` : si l'entité a un contrôleur, **ne réécrit pas la racine** ; elle ne fait que
     re-projeter (`RenderProjection.UpdateProjection()`) ; pour les entités sans contrôleur (PNJ jusqu'à
     E4) comportement E3.a inchangé (racine ← `Pos*`).
  4. **Écritures scriptées** de `Pos*` (0x64/0x65 `SetEntitiesPosition`/`AddEntitiesPositionOffset`, et
     tout autre site qui écrit `PosX/PosY/PosZ` — grep) : après l'écriture, `proxy.PushLogicalPositionToRoot()`
     : racine ← `Pos*` puis, si contrôleur, `controller.Teleport(racine)` (`:452-458`, remet vitesse et
     état de collision à zéro) ; re-projection.
- **Non-goals** : **flag de debug ignorant 0x10 — décision utilisateur en attente, NON implémenté** ;
  PNJ sur le contrôleur (E4) ; saut ; glissade par attribut ; entité-entité.
- **Acceptation** :
  1. Moteur (E3.d.0) : round-trip de tous les réglages + `control_mode` ; `CasaEngine.Tests` sans nouvel
     échec (18 préexistants).
  2. Convertisseur : un seul prefab avec `CharacterControllerComponent` (test sur `Alundra.entity` et sur
     un prefab non-héros) ; valeurs ci-dessus relues par `Entity.Load` ; export complet 0 erreur ;
     compteurs `Worlds 483`, `Entities.Prefabs 395`, `Sprites.QuadsRead == QuadsConverted 160355`,
     `Assets.Animation2d 9620` inchangés.
  3. DLL headless (monde `TopDownElevation` réel construit comme dans `AlundraEntityLogicalRenderPoseTests`,
     champ `AlundraCellsCollisionField` de la 389, pawn = prefab Alundra exporté chargé tel quel) :
     - **pose au sol** : héros adopté puis téléporté en (18,57) → `PosZ` = 80 px après le clamp ; après
       2 `World.Update(1/50)`, racine z == 80 et `IsOnGround == 1` ;
     - **mur** : depuis (18,15)… non : depuis la cellule (17,15) (marchable, h11 → z 176), `Move` de +24 px
       en X vers (18,15) (`walkability 1`) avec masque ClassB (`0x41`) → racine inchangée ; avec masque
       `0x40` → avancée ;
     - **marche du pont** : depuis (18,57) z 80 vers (18,56) : lire `AlundraCells` des deux cellules ;
       si `height` diffère de plus de 3 px × (1/16) → bloqué, sinon franchi — le test affiche les deux
       hauteurs lues et assert la règle (valeur attendue calculée depuis la donnée, pas devinée) ;
     - **propriété de la racine** : une frame où `Move` a été appliqué laisse racine ET `Pos*` à la
       position résolue par le mover (échoue si `SyncTransform` réécrit l'ancienne pose) ; deux frames
       consécutives sans dérive ;
     - **téléport scripté** : 0x64 vers (804, 872, 0) → racine, `Pos*` et vitesse verticale du
       contrôleur cohérents (vitesse 0) ; la frame suivante ne ramène pas le héros ;
     - **sans Move** : un `World.Update` sans `Move` ne déplace pas le héros horizontalement (intention
       nulle) ;
     - harnais intro inchangé (frame 926 — il n'a ni contrôleur ni champ : chemin E2).
  4. Runtime (utilisateur) : Alundra marche sur le pont, bloquée par bastingages/mâts, suit les
     marches ; ne traverse pas les cellules non marchables. Contrôle disponible seulement après 0x11
     (fin de l'intro, qui s'arrête au bloc 18 jusqu'à E4) — voir non-goal « flag de debug ».
- **Rollback** : revert du commit moteur + pointeur ; revert du commit parent (+ export). **Budget** :
  trois commits, ≤ 1 journée ; au plus deux tours de correctifs. **Arrêt** : si le harnais change de
  trajectoire (IsOnGround/LoadingMap), si le mode `Player` ne peut pas être tenu après `Possess` sans
  intention parasite, ou si la relecture racine → `Pos*` fait dériver l'intégration 16.16 d'E2 (test de
  deux frames).

## 4. Ordre et dépendances

E3.0 → E3.a → E3.b → E3.c → E3.d. E3.0, E3.c et E3.d.0 sont des commits moteur, E3.b un commit moteur
puis un commit DLL (plan-verifier pour chaque tranche moteur) ; E3.a et E3.d (convertisseur + DLL) sont
des commits du repo parent. Après chaque commit moteur : rappel du fetch/merge du checkout
standalone.
