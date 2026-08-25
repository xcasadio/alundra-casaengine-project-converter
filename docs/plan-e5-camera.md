# Plan E5 — Caméra suivant l'entité désignée

Date : 2026-08-25. Étape E5 de [plan-conversion-totale.md](plan-conversion-totale.md) §4. Chantier
**DLL** (aucune modification moteur prévue : les capacités nécessaires existent). Verifier frais
après la tranche.

## 0. Décisions de l'utilisateur (2026-08-25, ne pas re-débattre)

| # | Décision |
|---|---|
| E5-1 | **Cadrage fidèle** : la caméra montre la zone visible d'origine (320×240 unités logiques autour du point suivi), mise à l'échelle pour remplir la fenêtre, `PixelSnap` actif, et le clamp aux bords de map reprend la formule originale. Les décors sont dessinés pour ce cadre ; c'est la seule option où la chorégraphie de l'intro se lit comme dans le jeu d'origine. |
| E5-2 | ~~**Lissage flottant continu**, au même taux de rattrapage que l'original mais **exprimé en fonction du temps** — `facteur = 1 − (15/16)^(dt × 50)` — donc identique à 50, 123 ou 240 im/s (on ne réintroduit pas la dépendance à la cadence corrigée en E4). Écart assumé : pas d'arrêt résiduel entier (l'original s'arrête quand l'écart passe sous 16 px, notre convergence est asymptotique).~~ **REMPLACÉE par E5-3** (2026-08-26) — voir la tranche E5.c : la mesure a montré qu'un état flottant, quantifié seulement au rendu, continue de scintiller d'un pixel logique. |
| E5-3 | **Port entier fidèle** (2026-08-26, remplace E5-2) : `scroll += (cible − scroll) >> 4` en entiers, **une fois par tick logique 50 Hz** et non par image rendue. L'indépendance de la cadence d'affichage est conservée (le tick est fixe). Écart assumé, désormais **celui de l'original** : la caméra s'arrête quand l'écart passe sous 16 px (zone morte du décalage), donc l'entité suivie peut être jusqu'à 15 px hors du centre exact. |

## 1. Faits qui bornent le plan

**Original** (`alundra-datas-analyser/AlundraTools/AlundraEngine/`) :

- `g_entityFollowedByCamera` : initialisé au joueur (`GameEngine.cs:644`) ; écrit par 0x67
  (`EntityEventHandlers.cs:2073`, première entité du résultat de recherche — donc `null` si la
  recherche ne trouve rien), remis à `null` par 0x68 (`:2080`) et 0x69 (`:2087`).
- Suivi par frame (`GameEngine.cs:1747-1752`, dans `UpdateEntities`) : **seulement si la cible est
  non nulle ET `IsLoadedNormalOrDeactivated`** → `g_cameraLookAtX/Y/Z = cible.PosX/Y/Z >> 16`. Une
  cible détruite **fige la caméra** sur sa dernière position et n'est jamais remise à `null`
  automatiquement.
- 0x69 (7 octets) : `null` + position caméra imposée `g_cameraLookAtX/Y/Z` depuis `variables[1..6]`.
- Lissage (`GraphicManager.cs:75-92`) : à l'entrée de map `g_isCameraScrolling = 1` → **snap**
  `scrollX = X − 0xa0`, `scrollY = (Y − Z) − 0x88` ; ensuite, chaque frame,
  `scrollX += (X − (scrollX + 0xa0)) >> 4` et `scrollY += ((Y − Z) − (scrollY + 0x88)) >> 4`.
  Centre écran (0xa0, 0x88) = (160, 136) — noter que 136 n'est pas la moitié de la hauteur visible :
  **le point suivi n'est pas au centre géométrique de l'écran**, il est décalé ; ce biais doit être
  porté tel quel.
- Clamp (`GraphicManager.cs:97-122`) : `scrollX ∈ [0, 0x39f]` (927), `scrollY ∈ [0, 0x2cf]` (719) —
  cohérent avec une vue 320×240 dans une map de 1248×960 px (52×60 cellules de 24×16), qui est
  exactement la taille de la 389. **À vérifier dans la tranche** : ces bornes sont-elles des
  constantes globales ou dérivées de la taille de map ? Notre port les **dérive de la taille de map**
  (`TileMapData.MapSize` × 24/16 − taille visible) et documente l'écart si les constantes de
  l'original s'avéraient figées.
- Projection : `Y_écran = Y − Z` — même transformation que notre politique `TopDownElevation`
  (`DeriveRenderPosition` = `(X, −(Y − Z), 0)`, au signe d'axe près que la politique gère déjà).

**Moteur** (`CasaEngineMonogame/`) :

- La vue runtime résout la **première `CameraComponent`** trouvée
  (`DefaultRuntimeViewBootstrapper`) : c'est le `Camera2dComponent` de l'entité `AlundraCamera`,
  celui que le pan de debug pilote déjà.
- `Camera2dComponent` : **orthographique**, `Target` (Vector3, sûr à écrire chaque frame), `Zoom`
  (zone visible = viewport / zoom), `PixelSnap` (accroche la vue à la grille de texels **sans**
  modifier `Target`). Aucun clamp aux limites : c'est à l'appelant de le faire.
- `CameraTargeted2dComponent` dérive de `Camera3dComponent` : **perspective**, donc incompatible
  pixel-perfect — **non utilisé** (le point « à valider » du plan maître est ainsi tranché).
- Le rendu lit `ViewMatrix`/`ProjectionMatrix` après tous les `Update` de la frame.

**DLL** (ce repo) : `AlundraWorldProxy.UpdateDebugCameraPan` maintient déjà `Target = base + offset`
et **adopte comme nouvelle base toute écriture externe** (travail du 2026-08-24, `bca9338`) — le
suivi scripté écrit donc la base sans que le stick droit ne le perturbe, et `ALUNDRA_DEBUG_CAMERA_ENABLED`
neutralise l'offset.

## 2. Tranche E5.a — Suivi scripté de la caméra ✅ (cc1fc60 + correctifs 1507afc, verifier CONFIRMED)

- **But** : la caméra suit la cible désignée par les scripts, avec le cadrage et le rattrapage de
  l'original ; au bout de l'intro elle revient au héros.
- **Scope** :
  1. **État** : `AlundraWorldProxy.EntityFollowedByCamera` (port de `g_entityFollowedByCamera`),
     initialisé sur le héros à l'adoption du pawn (port de `GameEngine.cs:644`).
  2. **Opcodes** : 0x67 (2 octets, première entité de `EntitySearchService` ; `null` si aucune —
     fidèle), 0x68 (1 octet, `null`), 0x69 (7 octets, `null` + position imposée). Retirés du
     décompte « non implémentés » du harnais.
  3. **Suivi par tick logique** : si la cible est non nulle ET son `Status` est
     Loaded/Normal/Deactivated → `lookAt = (cible.PosX, PosY, PosZ) >> 16` ; sinon **la caméra ne
     bouge pas** (cible détruite = gel, port fidèle, pas de repli sur le joueur).
  4. **Cadrage (E5-1)** : à `InitializeWithWorld`, la DLL pose sur le `Camera2dComponent`
     `Zoom = viewportHeight / 240` (la fenêtre 1280×944 donne exactement 4 → zone visible 320×236,
     à confirmer sur les valeurs réelles) et `PixelSnap = true`. **Réglage runtime, pas d'asset
     modifié** → aucun export complet nécessaire ; si l'asset `AlundraCamera.entity` porte déjà un
     zoom, la DLL l'écrase et le documente.
  5. **Position caméra** : `Target` = pose de rendu du `lookAt` via la politique, **plus le biais de
     centre de l'original** (le point suivi est à (160, 136) du coin, pas au centre géométrique) ;
     la formule exacte est à dériver et à figer par un test chiffré sur la position New Game.
  6. **Lissage (E5-2)** : snap à l'entrée de map (port de `g_isCameraScrolling = 1`), puis
     rattrapage `cur += (cible − cur) × (1 − (15/16)^(dt × 50))` — identique à toute cadence.
  7. **Clamp** : vue maintenue dans la map, bornes dérivées de `TileMapData.MapSize`
     (largeur × 24, hauteur × 16, moins la zone visible), appliquées **après** le lissage comme
     l'original.
  8. **Debug** : le pan du stick droit reste un offset par-dessus la base (déjà en place) ; le suivi
     scripté écrit la base.
- **Non-goals** : `InitializeScrollingMode` complet (modes de scroll par map), effets de warp/fondu
  (E10), tri de profondeur (E8), transitions de map.
- **Acceptation** :
  - Tests DLL (patron `AlundraWorldProxyTests`/`AlundraEventProgramRunnerTests`) : 0x67 sur une
    recherche réelle de la 389 désigne la bonne entité ; 0x67 sans résultat met `null` ; 0x68/0x69
    (valeurs réelles si un programme de la 389 en contient) ; cible détruite → la caméra garde sa
    position (échoue si l'on replie sur le joueur) ; formule de cadrage : à la position New Game du
    héros, `Target` vaut la valeur calculée à la main (biais de centre compris) ; lissage : partant
    d'un écart connu, la position après N ticks est celle du taux 1/16 par tick de 50 Hz, **et
    identique à dt = 1/50, 1/123 et 1/240** (échoue avec un lissage par frame) ; clamp : une cible
    près d'un bord de la 389 ne fait pas sortir la vue de la map.
  - Harnais d'intro : les six changements de cible apparaissent comme `Implemented` ; **jalons
    inchangés** (554 / 555-678-801 / 1034 / 1202 / 1704, arrêt condition (a)) — la caméra n'influe
    sur aucun flag. Trace régénérée et commitée si les kinds changent.
  - Suites : build 0 erreur ; `Alundra.Tests` 454 + nouveaux ; convertisseur 137 inchangé.
  - Runtime (utilisateur) : la caméra suit la mouette, le bloc 10 qui descend, les marins 11 puis
    12, le bloc 18, puis revient sur Alundra ; le cadrage ressemble à l'original ; le stick droit
    décale toujours la vue sans la perturber.
- **Rollback** : revert du commit. **Budget** : un commit DLL. **Arrêts** : si le biais de centre ne
  peut pas être reproduit sans toucher au moteur ; si le clamp dérivé de la taille de map contredit
  les constantes de l'original sur la 389.

### Réalisé — écarts (2026-08-25)

- **Formule de cadrage dérivée et figée par test** : `Target = (X, −(Y − Z) + 16, 0)` — pas de biais
  en X (0xa0 = 160 = la moitié de 320), biais de +16 en Y parce que 0x88 = 136 dépasse de 16 la
  moitié de la hauteur de clamp. Position New Game du héros (804, 952, 0) → `Target (804, −936, 0)`.
- **Clamp validé par recoupement** : les bornes dérivées de `MapSize` retombent EXACTEMENT sur les
  constantes codées en dur de l'original sur la 389 (`scrollX ∈ [0, 0x39f]`, `scrollY ∈ [0, 0x2cf]`),
  y compris son « −1 » de borne inclusive — c'est la meilleure preuve que le port est juste.
- **Deux hauteurs distinctes dans l'original, les deux portées** (fait relevé au correctif) : la
  hauteur RENDUE vaut **236** (`StaticVariables.cs:56`, utilisée par tous les blits/clips de
  `GraphicManager`), tandis que l'arithmétique de clamp et de scroll raisonne sur **240**
  (`GraphicManager.cs:97-121` et `:817`). L'original clampe donc comme si 240 lignes étaient
  visibles alors qu'il n'en dessine que 236. Notre port fait pareil : le **zoom** vient de la hauteur
  d'affichage (236 → zoom **4 exact** sur la fenêtre 1280×944, donc pixel-perfect), le **clamp** garde
  240. Aucune modification d'asset ni export nécessaire.
- **Correctif de fidélité (P3 du verifier)** : l'original réinjecte la valeur CLAMPÉE dans son état de
  scroll ; notre première version gardait l'état non clampé et repartait de lui — 97 px de dépassement
  caché dès l'entrée de map sur la 389, et une caméra qui restait collée à un bord avant de repartir.
  Corrigé : l'état lissé EST l'état clampé, comme l'original (vérifié avant/après : −839 figé pendant
  10 ticks avant, −836,5625 dès le premier tick après — **valeur devenue −836 depuis E5.c**, le pas
  entier donnant `ceil(39/16) = 3` là où le lissage flottant donnait 39/16 ; la propriété testée, elle,
  est inchangée).
- **Écarts documentés** : lissage flottant continu (décision E5-2) — pas d'arrêt résiduel entier ;
  0x67 sans résultat met `null` alors que l'original garde l'entrée précédente de son buffer de
  recherche (jamais atteint sur la 389, les six opérandes matchent tous).
- **Tests** : DLL 475 (454 + 21) ; convertisseur 137 ; trace d'intro byte-identique, jalons intacts
  (554 / 555-678-801 / 1034 / 1202 / 1704), seuls les six 0x67 passent en `Implemented`.
- **Différé (P4)** : le test « pas de repli sur le joueur » n'exerce que la fonction pure ; un repli
  introduit dans `UpdateCameraFollow` lui-même ne le ferait pas échouer.

## 2 bis. Tranche E5.b — Sprites projetés au pixel entier ⏳ (moteur, plan-verifier)

- **Symptôme utilisateur (2026-08-25)** : les entités suivies par la caméra deviennent floues /
  vibrent PENDANT leur mouvement et redeviennent nettes à l'arrêt (le marin dans l'escalier, la
  mouette en vol).
- **Cause établie (investigation, faits)** : le filtrage n'est PAS en cause —
  `SpriteRendererComponent` force déjà `SamplerState.PointClamp` au GPU
  (`Application/Components/SpriteRendererComponent.cs:142` et `:269`), et le `sampler_state` des
  assets texture n'est lu que par le chemin des modèles 3D
  (`StaticModelMaterialResolver.cs:28,53`) — le correctif convertisseur `e4cca85` (PointClamp au
  lieu d'AnisotropicWrap) reste juste mais c'est de l'hygiène de données, sans effet visuel ici.
  La cause réelle est une **position de rendu fractionnaire** : `AlundraWorldProxy.SyncTransform`
  (`:2306-2320`) écrit la position tronquée à l'entier (`ResolveLogicalPosition`, `:1157`,
  `Pos >> 16`) pour les entités SANS contrôleur — donc nettes — mais la saute pour celles qui en ont
  un, dont la racine vient du `Move()` flottant du moteur
  (`AlundraEntityScriptProxy.MoveControllerAndPullPosition:1253-1280` →
  `CharacterControllerComponent.Move:413`, aucun arrondi). Ni
  `RenderProjectionComponent.UpdateProjection` (`:49-76`) ni la construction de la matrice monde du
  sprite (`SpriteRendererComponent.cs:614-621`) ne tronquent. À zoom 4, un décalage de 0,5 px
  logique = 2 px écran : la grille de texels du sprite ne coïncide plus avec celle de l'écran.
  L'original ne connaît pas ce défaut : il garde ses positions en 16.16 et **tronque uniquement au
  rendu**.
- **Scope (moteur, API additive)** : accrochage de la pose de RENDU à l'unité entière, opt-in
  (défaut **false** → aucun comportement existant ne change), appliqué une seule fois là où la pose
  est dérivée (`RenderProjectionComponent.UpdateProjection`, juste après `DeriveRenderPosition`).
  La pose LOGIQUE et la physique continue restent intactes.
  **Convention FIXÉE (correctif plan-verifier, ne pas re-déléguer)** : le résultat doit égaler le
  plancher de l'original dans SON espace Y-vers-le-bas (`X >> 16`, `(Y − Z) >> 16`). Comme notre
  pose de rendu vaut `(X, −(Y − Z), 0)`, cela donne **plancher sur X et PLAFOND sur Y**
  (`ceil(renderY) = −floor(−renderY) = −floor(Y − Z)`). Un plancher naïf en espace de rendu
  décalerait chaque valeur non entière d'une ligne vers le bas par rapport au décor (qui, lui, est
  positionné en entiers), et un arrondi au plus proche basculerait à .5 au lieu de .0 et donnerait
  un X faux.
  **Où vit la connaissance d'axe** : la politique d'espace possède déjà l'inversion `−(Y − Z)`, donc
  le snap lui appartient — ajouter à `SimulationSpacePolicy` une méthode d'accrochage (défaut :
  plancher sur les trois axes) surchargée par `TopDownElevationSimulationSpacePolicy` (plancher X,
  plafond Y). `RenderProjectionComponent` se contente de l'appeler quand le drapeau est posé ; il
  n'embarque aucune connaissance d'axe.
- **Activation côté Alundra** : la DLL pose la propriété au spawn sur la `RenderProjection` déjà
  mise en cache (même patron que `Zoom`/`PixelSnap` d'E5.a) — **pas de changement d'asset, pas
  d'export**.
- **Acceptation** : moteur — défaut false : tests existants inchangés, `CasaEngine.Tests` sans
  nouvel échec (18 préexistants) ; drapeau posé : **cas chiffré discriminant** — pose logique
  `(10.7, 20.3, 0)` → pose de rendu **exactement `(10, −20, 0)`** (échoue avec un plancher en espace
  de rendu, qui donnerait `(10, −21, 0)`, ET avec un arrondi au plus proche, qui donnerait
  `(11, −20, 0)`) ; au moins un second cas à Z non nul (l'élévation entre dans `Y − Z`) ; une
  progression lente et continue de la pose logique produit une suite de positions de rendu monotone,
  sans oscillation entre deux entiers. DLL — les entités à contrôleur ont une position de rendu
  entière ; valeurs chiffrées d'E4/E5 inchangées (mouette 171 ticks / 209,25 px, escalier, pin du Z,
  cadrage caméra, clamp) ; trace d'intro byte-identique ; suites vertes. Runtime (utilisateur) :
  plus de flou ni de vibration sur les entités en mouvement.
- **Rollback** : revert du commit moteur + pointeur, revert du commit DLL. **Budget** : un commit
  moteur + un commit parent. **Arrêt** : si accrocher la pose de rendu casse le tri de profondeur
  (`DepthSortable2DComponent` lit la position monde du sprite) ou la re-projection d'E3.a.

## 2 ter. Tranche E5.c — Caméra cadencée au tick logique, port entier du scroll ✅

- **Symptôme résiduel (2026-08-25)** : après E5.b et l'accrochage de la cible caméra (`445594e`), les
  entités suivies **vibraient toujours** pendant leur mouvement. Mesuré en jeu : tout tombe sur des
  pixels écran entiers, mais l'écart vertical oscille en dents de scie d'environ 4 px écran.
- **Cause racine (DLL, pas moteur)** : un **battement de cadence**. La position d'une entité ne change
  qu'au **tick logique 50 Hz** (`AlundraEntityScriptProxy.Update`, boucle `for tick`), alors que
  `AlundraWorldProxy.UpdateCameraFollow` lissait la caméra **une fois par image rendue** (~123 Hz).
  Entre deux ticks le sprite est figé mais la caméra franchit seule des frontières de pixel entier :
  le sprite recule de 4 px écran puis ressaute au tick suivant. Reproduit numériquement avec les
  formules de production : **24 inversions de sens par 60 images** à 1,22 px/tick, **48** à 3,7 px/tick,
  amplitude croissant avec la vitesse (8/12/16 px écran) — ce qui explique aussi « net sur le plat,
  flou dans l'escalier » (`renderY = −(Y − Z)` bouge ~2× plus vite quand Y et Z varient en sens
  opposés) et « nette une fois posée » (à l'arrêt la caméra converge, plus aucun franchissement).
  L'original ne peut pas battre : `GameEngine.cs:225-229` fait `RenderScene()` (qui lisse le scroll)
  puis `Update(0)` (qui bouge les entités), une fois chacun par image de jeu.
- **Hypothèses écartées par la mesure, à ne pas rouvrir** : pas de render target intermédiaire (le
  `LinearClamp` de `BackBufferPresenter` n'est jamais atteint, `RenderTarget` est nul sur
  `BackBufferSurface`) ; 1280/4 = 320 et 944/4 = 236 exacts, aucun demi-pixel dans l'ortho de
  `Camera2dComponent` ; son `PixelSnap` (pas de 1/Zoom) était bien devenu un no-op ;
  `WallPlacementOverlay` n'émet ses tuiles **qu'une fois au chargement** et leur clé de tri ne dépend
  d'aucune position d'entité.
- **Pourquoi le lissage flottant au tick ne suffisait pas** (mesuré, régime établi sur 1500 ticks) : un
  état flottant converge vers un décalage **non entier**, donc le `ceil()` du rendu bascule dès que la
  cible — qui avance par pas irréguliers 1‑1‑2, étant `PosY >> 16` — le pousse d'un côté ou l'autre de
  la frontière : **480 inversions** à 1,22 px/tick, **748** à 1,75, **1197** à 2,4 — c'est-à-dire aux
  vitesses de marche, précisément celles du bug rapporté. (Aux vitesses plus élevées le flottant se
  stabilise par hasard : 1 inversion à 3,7 px/tick, 0 à 5 et 8 — d'où l'importance de ne pas mesurer
  qu'à une seule vitesse.) Le bug aurait donc seulement été ralenti de 123 Hz à 50 Hz. Le `>> 4` entier
  a une **zone morte** (incrément nul tant que |écart| < 16) qui verrouille l'état sur un entier :
  **0 inversion à toutes les vitesses testées, de 1,22 à 8 px/tick**.
- **Convention d'axe (le point fragile)** : `>> 4` est un plancher, donc non symétrique, et notre
  espace de rendu inverse le Y (`renderY = −scrollY − 120`, `renderX = scrollX + 160`, les deux
  recoupés par les bornes de clamp figées en E5.a). Un écart Δ en rendu vaut −Δ en scroll, d'où
  **incrément X = `Δ >> 4` (plancher), incrément Y = `−((−Δ) >> 4)` = `ceil(Δ/16)` (plafond)** —
  exactement la convention « plancher X, plafond Y » d'E5.b. Vérifiée contre l'original sur tous les
  signes et figée par un `Theory` de 10 cas.
- **Réalisé** : `LogicTicksThisFrame` remonté en tête d'`Update` ; `UpdateCameraFollow(int ticksThisFrame)` ;
  deux coutures pures testables `StepCameraScroll` et `AdvanceCameraSmoothing` ;
  `ComputeCameraSmoothingFactor`, `ApplyCameraSmoothing` et `SnapCameraRenderTarget` supprimées (l'état
  est entier par construction, donc il EST la valeur rendue, comme `g_cameraScrollingX/Y`).
- **Écarts documentés** : arrêt résiduel sous 16 px (décision E5-3, comportement de l'original) ; le pan
  de debug intègre toujours son offset par image et peut réintroduire le battement tant que le stick
  est dévié (debug-only) ; la ligne de câblage `UpdateCameraFollow(ticksThisFrame)` reste hors de
  portée des tests headless (même forme que le P4 différé d'E5.a).
- **Tests** : DLL **487** (477 − 7 retirés + 17 ajoutés) ; convertisseur 138 ; trace d'intro
  byte-identique, jalons intacts. Aucun fichier moteur touché.
- **Observation hors scope relevée au passage** : l'accumulateur `float` d'`AlundraLogicClock` rend
  **49** ticks par seconde réelle à dt = 1/123 (accumulateur à 0,019999985, juste sous 0,02) contre 50
  à dt = 1/50 et 1/240. Préexistant, sans effet sur cette tranche (entités et caméra partagent la même
  horloge) ni sur le harnais (dt fixe).

## 3. Suivi

| Tranche | Statut | Commit |
|---|---|---|
| E5.a suivi scripté de la caméra | ✅ (verifier CONFIRMED) | cc1fc60 + 1507afc |
| E5.b sprites projetés au pixel entier | ✅ | moteur 6384bf4d + parent eab5f17 |
| E5.c caméra cadencée au tick logique, port entier | ✅ | (ce commit) |
