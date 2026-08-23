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

### E3.b — Champ de collision Alundra ⏳ (moteur API + DLL, plan-verifier)

- **Scope** : moteur — `ICollisionField.TrySampleGround` reçoit un **masque de marchabilité** opaque
  (`uint walkabilityMask`, défaut « tout ») et `GroundSample` expose `IsWalkable` pour ce masque ;
  `HeightGridCollisionField` inchangé sinon (masque ignoré). DLL — `AlundraCellsCollisionField :
  ICollisionField` construit une fois par world depuis `AlundraCells` : hauteur = `height × 16` avec
  les cas `Slope & 3` (table `g_heights_800236d4` copiée de `StaticVariables.cs` avec adresse),
  marchabilité `((walkability | ground_property << 8) & masque) == 0`, `SurfaceTag` =
  `ground_property`, clamp des coordonnées à la grille ; installé dans `World.CollisionField` au
  `InitializeWithWorld`. Espace du champ = **espace logique de la politique** (X, Y profondeur,
  Z élévation) — le contrat Y-up d'`ICollisionField.cs:9` est remplacé par « axes de la politique du
  monde » (doc + tests moteur mis à jour).
- **Acceptation** : tests DLL sur les cellules réelles de la 389 ((18,57) → 80 px ; une cellule
  non marchable ; un escalier d'une autre map si présent) ; tests moteur du masque ; aucun
  consommateur runtime encore (le mover arrive en E3.c).
- **Question ouverte à trancher en E3.b** : valeur du masque pour le joueur et les PNJ (classe A/B
  selon `EntityFlags`, à lire dans `GetCollisionFlagsWithPlayer`).

### E3.c — Mover conscient de la politique ⏳ (moteur, plan-verifier)

- **Scope** (`CharacterControllerComponent`) : axe « haut » = `policy.Up` (Z sous
  `TopDownElevation`, Y sinon) pour la gravité, le snap et le pied ; fixture **Box ou Capsule** ;
  pied = racine + min de la fixture le long de l'axe haut (helper `FootOffset`) ; sol : si
  `World.CollisionField != null`, échantillonner l'empreinte (4 coins de la boîte, ou 4 points du
  rayon de la capsule) avec le masque des réglages et prendre le **max** ; sinon sweep actuel ;
  horizontal : pour un `Move(d)`, tenter X puis Y séparément ; un coin cible non marchable ou
  dont le sol dépasse `pied + StepHeight` bloque l'axe ; vertical : gravité le long de l'axe haut,
  nouveau réglage `MaxFallSpeed`, atterrissage = snap au sol et `IsGrounded`. Réglages en unités
  monde (px pour Alundra). Modes de contrôle inchangés.
- **Non-goals** : pentes (`MaxSlopeAngle` reste tel quel), pathfinding, entité-entité.
- **Acceptation** : tests moteur sur un monde `TopDownElevation` + `HeightGridCollisionField`
  synthétique + pawn Box : marche sur plat, bloqué par falaise > `StepHeight`, monte une marche ≤
  `StepHeight`, tombe avec `MaxFallSpeed` et atterrit (`IsGrounded`), non marchable bloque ; les
  tests existants (`CharacterControllerComponentTests`, Y-up/capsule/sweep) restent verts ; démo
  `TopDownElevationDemo` inchangée.
- **Rollback** : revert dans le submodule.

### E3.d — Branchement Alundra ⏳ (convertisseur + DLL)

- **Scope** : convertisseur — `CharacterControllerComponent` sur le prefab Alundra (réglages : Box du
  header, `StepHeight 3`, `GroundSnapDistance`, `Gravity`/`MaxFallSpeed` calculés depuis les
  propriétés de map au runtime par la DLL : `Gravity 128` → `(128 << 8) / 65536` px/tick² × 50² =
  **1 250 px/s²**, `ZViscosity 4096` → `(4096 << 8) / 65536` px/tick × 50 = **800 px/s**) ; DLL —
  `AlundraPlayerManager.Tick` passe `Move(dx, dy, 0)` au contrôleur au lieu d'écrire `PosX/PosY` ;
  après l'update du composant, le proxy relit la racine logique → `PosX/PosY/PosZ` (16.16) ;
  `IsOnGround ← controller.IsGrounded` (remplace le stub) ; clamp au sol au spawn
  (`EntityManager.cs:127-136`) ; les téléports scriptés (0x64/0x65) écrivent la racine. Mode
  `Script` pendant `ControlLocked`.
- **Acceptation** : runtime 389 — Alundra marche sur le pont, bloquée par les bastingages et les
  mâts, suit les marches du pont, ne traverse pas les cellules non marchables ; harnais inchangé ;
  tests DLL verts. Pour tester avant la fin de l'intro : **flag de debug** (à valider par
  l'utilisateur) ignorant le verrou 0x10, écart explicite hors fidélité.

## 4. Ordre et dépendances

E3.0 → E3.a → E3.b → E3.c → E3.d. E3.0, E3.b et E3.c sont des commits moteur (plan-verifier chacun) ;
E3.a et E3.d sont des commits du repo parent. Après chaque commit moteur : rappel du fetch/merge du checkout
standalone.
