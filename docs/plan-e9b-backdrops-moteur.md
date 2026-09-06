# Plan — E9.b : le rendu des fonds défilants passe dans le moteur

Seconde moitié d'E9, ouverte le 2026-09-04 après la clôture d'E9.a (`docs/plan-e9-backdrops-residus.md`
§6). Le plan maître (`docs/plan-conversion-totale.md:506-511`) demande de **retirer `BackdropRenderer`
de la DLL** au profit d'un **composant moteur de couches défilantes** ; E9.a a ajouté l'exigence d'**une
seule horloge** (D-E9-6) et légué des suites P3/P4. Ce plan porte le *mécanisme* de rendu dans le
moteur et laisse la *politique* Alundra dans la DLL, selon le patron établi par E10 (D-E10-4).

**Révision 3** — deux tours de relecture indépendante (enveloppe, S0, S1, S2) : douze blocages au tour
1, dix au tour 2, tous **corrigés** (dispositions en §1.7). **Relecture de clôture (2026-09-04)** :
enveloppe, S1 et S2 **READY** ; S0 **REVISE** sur un seul blocage (réalisabilité headless des tests de
la source de taille de `ScreenEffectComponent`) dont la prescription minimale est appliquée ci-dessous
(D-E9b-12, S0 items 4 et 6, mutation) **sans nouvelle relecture** — cap atteint, plan présenté à
l'utilisateur en l'état. Clarification ajoutée au même moment, non relue : D-E9b-3, une seconde
poussée sans `Advance()` intermédiaire **écrase** la frame en attente.

**Amendement du 2026-09-04 (§1.8), pendant l'exécution** : le critère visuel de D-E9b-13 a été
mesuré avant d'être utilisé et sa première rédaction s'est révélée **non discriminante** ; il est
remplacé par un protocole de rafale + médiane, calibré, dont les seuils sont désormais très
inférieurs aux tailles d'effet à attraper. Le reste du plan est inchangé.

---

## 0. Cadre

### 0.1 Décisions de l'utilisateur (2026-09-04), prises sur les faits de la découverte

- **D-E9b-U1 — Horloge** : *la DLL pousse les ticks au moteur*. `AlundraLogicClock` reste intouchable
  ; chaque frame la DLL pousse (ticks logiques, défilement X/Y de l'original) ; le mécanisme moteur
  avance **tout** par tick entier — animation V **et** auto-défilement — en accumulateurs par couche
  comme l'original (§1.3). L'horloge flottante `_elapsedTicks` disparaît.
- **D-E9b-U2 — Forme** : *objet de rendu moteur alimenté par la DLL* (service sans état GPU + composant
  de jeu, comme `ScreenEffectComponent`). Le compagnon `.backdrop.json` reste le format de données,
  lu par la DLL : **zéro changement convertisseur**, pas de sérialisation ni d'éditeur en V1. La
  promotion des compagnons en assets reste le chantier différé de `docs/editeur-couverture-dll.md:57-59`.
- **D-E9b-U3 — Acceptation** : *numérique + visuelle sur 389, 159 et 321* — pins re-hébergés sur les
  coutures moteur, pins DLL du contrat de poussée au site de production, coexistence des deux chemins
  le temps d'une comparaison offset par offset, captures avant/après (méthode B5, critère décidable §2).
- **D-E9b-U4 — Périmètre annexe** : **entrent** le P3 « rectangle de ciseaux depuis le périphérique »
  et le P3 « `ScreenEffectComponent` en pixels-monde + doc de parité » ; **restent hors** le biais de
  centrage E5 (2 px, propriété de la caméra) et le fond noir via `environment` (changement convertisseur).

### 0.2 Hypothèses posées par ce plan

- Les comportements de l'original jamais portés par la DLL — opcode `0xA4` `SetScrollingMode`
  (activation par couche + banque d'animation, `GameEngine.cs:363-368`, `EntityEventHandlers.cs:3119-3124`),
  décalage scripté `g_scrollingParameters.OffsetX/Y` (secousse, `GraphicManager.cs:77-85`), marcheur
  `OvrTick/OvrHold` de la teinte (`GraphicManager.cs:1271-1299`), couche sautée à dénominateur 0
  (`GraphicManager.cs:863-866`, inatteignable sur le corpus) — sont **gelés et documentés**, pas portés.
- Mode AUTO comme E9.a : enchaînement des tranches sans sollicitation sauf arrêt §4, P0/P1 ou décision
  produit ; jamais de push ; **arrêt avant la validation en jeu** (S2), qui est à l'utilisateur.
- L'aperçu éditeur ne dessine pas les fonds : la DLL n'y pousse rien (`UpdateGameplayScripts = false`)
  et le composant **ne soumet rien sans poussée reçue** (D-E9b-3) — comportement inchangé, limite V1
  documentée comme pour `ScreenEffectComponent`.
- **Invariant `Target.Z == 0`** : la caméra Alundra construit sa cible avec `0f` en Z
  (`AlundraCameraMath.cs:262`, propagé par `StepCameraScroll` `:163`) ; les quads de fond sont soumis à
  z = 0 (§1.2) et le restent (D-E9b-4).
- Les tests sur données réelles de `Alundra.Tests` suivent la convention locale (`FindProjectRoot`
  avec auto-saut, `SpriteRecordCatalogTests.cs:273-285`) ; ce plan demande qu'ils **échouent** plutôt
  que de se sauter (règle §2.8 de `docs/plan-oracle-heros.md`), écart de convention assumé.

### 0.3 Références AVANT chantier

| Suite / état | Valeur |
|---|---|
| `Alundra.Tests` | 782 / 782 (clôture E9.a) |
| Convertisseur | 153 / 153 |
| `CasaEngine.Tests` | 18 échecs préexistants au HEAD `ef4bc472` (plans), **17** au 2026-09-03 (mémoire) — **à re-mesurer nommément avant S0**, flake connu `StaticModelMaterialOverrideResolverTests` |
| `alundra-project/` | export corrigé du 2026-09-04 (trames + texte décodé), `data-extracted/` prouvé identique au remaster |
| Dépôt parent | 10 commits non poussés (`origin/main = 2b5dcfe`) ; sous-module moteur `529d3896` (main), `CasaEngine.Launcher/Program.cs` modifié non stagé (ne jamais le stager) |
| `AlundraGame.json` | `FirstWorldLoaded` = 389 (valeur de l'utilisateur, à restaurer après toute capture) |

### 0.4 Ce que ce chantier touche, et ce qu'il ne touche pas

- **Touche** : le **moteur** (sous-module : nouveau service + composant de jeu + page `docs/engine/` +
  tests `CasaEngine.Tests` ; correctif `ScreenEffectComponent`) et la **DLL** (`AlundraBackdropStage`
  devient l'adaptateur ; `BackdropRenderer`, `BackdropOffsetMath` et leurs tests disparaissent en S2).
- **Ne touche pas** : le convertisseur (arrêt), l'export (aucun diff attendu ; le baseline
  `e9-baseline-manifest` reste la référence), l'analyseur, `AlundraLogicClock`, le mode cellulaire et
  le `WaveLut` (D-E9-7), `Program.cs` du lanceur, aucune suppression sous `alundra-project/`.

---

## 1. Faits établis (découverte lecture seule, 4 lecteurs + critique, 2026-09-04)

### 1.1 Le moteur

- **Aucun** composant de défilement, de parallaxe ou de fond n'existe (grep `ScrollParameters|Parallax|
  Backdrop|Scrolling` : seulement des commentaires, `SpriteRendererComponent.cs:55`, `SpriteBlendMode.cs:29`,
  `ScreenEffectComponent.cs:71`).
- **Aucune horloge à ticks** : `FrameTime` ne porte que des deltas flottants (`FrameTime.cs:5-53`) ;
  le seul pas fixe (`CharacterMotionSystem.FixedTimeStep`, `CharacterMotionSystem.cs:39-69`) est privé
  à ce système et activé nulle part ; `WorldRuntimeSystems` n'a pas d'API d'enregistrement
  (`WorldRuntimeSystems.cs:7-31`).
- **Un composant d'entité ordinaire n'est pas dessiné** : `EntityComponent` n'a pas de `Draw`
  (`EntityComponent.cs:97-100`) ; `Entity.Draw` ne dessine que `RootComponent` et les
  `PrimitiveComponent` attachés (`Entity.cs:511-534`) ; `World.Draw` ne dessine que les entités à racine
  visibles au frustum (`World.cs:752-779`). Un `EntityComponent` sans cas dans
  `EditorEntityJsonSerializer.SaveComponent` perd ses données en silence (`:187-267`). → D-E9b-U2 évite
  les deux pièges : composant de **jeu**, pas d'entité.
- **Précédent E10** : `ScreenEffectComponent : GameComponent` (`ScreenEffectComponent.cs:25-110`),
  instancié par `CasaEngineGame.Initialize` (`CasaEngineGame.cs:344-355`, propriété `:54`),
  `UpdateOrder = ComponentUpdateOrder.ScreenEffects` (dernier ; `ComponentOrder.cs`) ;
  `CasaEngineGame.Update` exécute `UpdateWorld` **avant** les `GameComponent` (`CasaEngineGame.cs:503-520`)
  et `World.Update` finit par `GameplayProxy.Update` (`World.cs:523-525`, appelé seulement sous
  `UpdateGameplayScripts`) : un état poussé par la DLL dans `Update` est consommé par le composant **la
  même frame**. Il lit la caméra par `_game.GameManager?.ViewManager?.ActiveView?.Camera as
  Camera2dComponent` (`:53-56`, nullable) — la même instance que la caméra résolue de la DLL
  (`AlundraScreenFadeCameraWiringTests.cs:58-106`) — et passe `ScreenSizeWidth/Height` en **pixels** comme
  demi-étendues monde (`:56`) : c'est le P3 hérité. Signature réelle :
  `SubmitOverlay(SpriteRendererComponent renderer, Vector3 cameraPosition, int viewportWidth, int
  viewportHeight, Texture2D overlayTexture = null)` (`:77`) ; retour sans soumettre si
  `viewportWidth <= 0 || viewportHeight <= 0` (`:79-82`) ; **`fullViewport` est une variable locale**
  `new Rectangle(0, 0, viewportWidth, viewportHeight)` (`:95`) passée comme ciseaux (`:108`) ; texture
  1×1 créée sous garde (`:112-140`). Trois des quatre tests existants passent la texture en **5e argument
  positionnel** (`ScreenEffectComponentSubmissionTests.cs:77`, `:91`, `:104`). La DLL l'atteint par
  `world.Game?.ScreenEffectComponent?.Service` à `InitializeWithWorld` (`AlundraWorldProxy.cs:870-882`,
  `AlundraScreenFadeDirector.AttachToWorld`, service nul = no-op silencieux).
- **Rendu trié** : `SpriteRendererComponent : DrawableGameComponent` (`:14`) met en file des
  `SpriteDisplayData` (plafond 10 000, `:31-33`) ; `Flush` trie par `RenderSortKey2D` dès qu'une entrée
  a une clé (`:383-387`) puis dessine avec `_depthStencilState` (`LessEqual`, écriture ON, `:92-97`,
  `:155-206`) ; les chunks statiques du `TileMapComponent` sont dessinés **immédiatement** dans
  `World.Draw` par `DrawStaticBatch`, même état de profondeur, **avant** ce flush
  (`TileMapComponent.cs:437-453`, `:1378-1467`). Passes : `Background = 0`, `YSortedWorld = 300`,
  `Effects = 500`, `ScreenEffects = 750` (`RenderPass2D.cs:5-19`). Fusion : `Opaque`, `AlphaBlend`
  (NonPremultiplied), `Additive`, `Subtractive` (`SpriteBlendMode.cs:8-40`).
- **Ciseaux — contrainte headless** : la surcharge `DrawSprite(…, in sortKey, effects, blendMode)`
  (`SpriteRendererComponent.cs:573-579`) lit `GraphicsDevice.ScissorRectangle` **à la mise en file**
  (`:577`) — `DrawableGameComponent.GraphicsDevice` lève sur un jeu non initialisé ; tout montage
  headless du dépôt passe donc un rectangle explicite à la surcharge `:586-592`
  (`SpriteRendererComponentBlendModeTests.cs:36-154`, `ScreenEffectComponentSubmissionTests.cs:35-128`).
  `position` est le coin haut-gauche (Y vers le haut) : translation = position + (w/2, −h/2) (`:651-656`).
- **Textures** : chargement en deux temps `AssetContentManager.Load<Texture>(id)` puis
  `texture.Load(acm)` puis `.Resource` (`Texture.cs:62-67`, `:107-113` ; `TileMapComponent.cs:909-913`) ;
  alpha droit (`Texture2DLoader.cs:9-13`).
- **Caméra 2D** : `Target` (`Camera2dComponent.cs:30`), `Zoom` (`:41`), `Viewport` (`CameraComponent.cs:45`,
  rempli seulement par `InitializeWithWorld`/`OnScreenResized`, `:95-111`) ; projection orthographique
  `viewport / zoom` centrée sur `Target`, caméra à `Target.Z + 500`, fenêtre de profondeur
  `[Target.Z − 500, Target.Z + 499]` (`Camera2dComponent.cs:82-108`). Zoom Alundra = `viewportHeight / 236`
  (`AlundraCameraMath.cs:323`) → vue de 320 × 236 unités monde.
- **Visibilité** : `CasaEngine` n'ouvre ses internes qu'à `CasaEngine.Tests` (`InternalsVisibleTo.Tests.cs:3`)
  — tout ce qu'`Alundra.Tests` ou la DLL consomme doit être **public**.
- **Gabarit de test headless** : `ScreenEffectComponentSubmissionTests.cs:12-60` (jeu non initialisé +
  `_components`, `Texture2D` non initialisée, `SpriteRendererComponent` construit sans device, lecture de
  `_spriteDatas` par réflexion : `WorldMatrix`, `SortKey`, `BlendMode`, `Color`, `Texture`,
  `ScissorRectangle`).
- Règles du dépôt moteur (`.github/copilot-instructions.md`) : API additive, zéro allocation ni
  réflexion dans `Update`/`Draw`, états GPU restaurés, doc courte, un commit par sous-tâche.

### 1.2 La DLL aujourd'hui

- Propriété : `AlundraWorldProxy._backdropStage` (`internal readonly`, `:433`) ; `Load` à
  `InitializeWithWorld` (`:506-513`) ; dans `Update` : ticks (`:1400`) → porte de gel calculée
  (`:1413-1414`) → … → caméra résolue/suivie (`:1504-1510`) → `ApplyOriginalBackgroundClearColorOnce`
  (`:1519`) → `UpdateAndDrawBackdrop(elapsed, ticks, world, camera)` (`:1520`, **inconditionnel**) →
  fondu (`:1529-1530`) → `CloseFrame` (`:1577`). Le bloc est **hors** de la porte de gel
  (`docs/plan-transitions-carte.md:259-284`).
- `AlundraBackdropStage.UpdateAndDrawBackdrop` (`:131-162`) : `AdvanceAnimation(ticks)` en première
  instruction ; retour si `!HasContent || Game == null` (`:135`) ; `Tick(elapsed)` (horloge flottante) ;
  retour si pas de `SpriteRendererComponent` ; `scroll = ToOriginalScrollSpace(Target)` ;
  `Draw(renderer, scroll, Target, 320, 240)`. `ApplyOriginalBackgroundClearColorOnce` (`:60-99`) : retry
  par frame, garde posée au succès — politique Alundra (port de `AlundraGame.Draw` `Clear(Black)`).
- `BackdropRenderer` : `LayerRuntime` (Scrollar, `Texture2D[] Frames`, `AnimTimer`, `SortKey`, `Tint`,
  `BlendMode`, deux compteurs ; `:73-106`) ; `ResolveFrameAssetIds` (`string?[]`, `:133-141`) /
  `LoadLayerFrames` (D-E9-9, seam par délégué ; `:153-186` : id nul/vide → texture nulle `:161`,
  trame 0 nulle → couche ignorée, trame `f ≥ 1` nulle → `[frame0]`) ; `LoadFrameTexture` : id
  **inanalysable** → avertissement + null (`:231-239`) ; `Load` (`:198-279` : teinte → texture 1×1, clé
  `Effects`/sorting −1 (`:209-212`) ; **couche sautée** sauf `Mode == "Tiles" && Scrollar != null &&
  TextureAssetId non vide** (`:226`) ; clé `(Ground ? Effects : Background, 0, DepthOrder, 0,0,0,
  LayerId)` (`:272-273`) ; `ResolveGroundLayerBlend` `:281-318`, appelé `:275`) ; `Tick` (`:323-326`) ;
  `AdvanceAnimation` (`:340-359`, modulo = `Frames.Length` `:350`) ; `Draw` (`:385-474` : ciseaux
  `(0,0,w,h)` `:399-403`, quad de teinte `:405-426` **à z = `0f`** `:418`, offsets `:432-437`, origines
  couvrantes `:447`, quads de couche **à z = `0f`** `:467`).
- `BackdropOffsetMath` : `CanvasWidth/Height` 640/480, `TicksPerSecond` 50 ; parallaxe entière
  `scroll * num / denom` (`:35-38`) ; auto-défilement en **forme close** sur un compte absolu
  `speed·t + (t / |period|)·dir` (`:46-57`) ; `WrapOffset` (`:60-64`) ; `ComputeLayerOffset` (`:72-78`) ;
  origines couvrantes (`:86-103`, `:112-128`, **allouent** deux listes par couche et par frame).
- `BackdropDocument` (`:13-23`, `:33-56`, `:72-82`) et `BackdropLoader` (`:37-108` ; `TryParseMapIndex`
  `:110-120` **réutilisé par la musique**, `AlundraWorldProxy.cs:966`) : contrat JSON du compagnon,
  chemins Alundra (`Maps/world-index.json`, `-{id}`), dégradés silencieux. Les trois compagnons réels
  portent une couche 1 `Mode: "Disabled"` (à filtrer) et `OverlayEnabled: false`.
- `AlundraCameraMath` : `CameraVisibleWidth/Height` 320/240 (`:54-55`, internes depuis B5),
  `ToOriginalScrollSpace = ((int)X − 160, −(int)Y − 120)` (`:308-309`), `ComputeCameraZoom` (`:323`),
  cible construite avec `Z = 0f` (`:262`).
- Deux horloges : `AlundraLogicClock` (50 Hz, plafond 4 ticks/frame, plancher collant de première frame
  dans `LogicTicksThisFrame`, `AlundraWorldProxy.cs:469-473`) pour l'animation V, et `_elapsedTicks`
  flottant non plafonné pour l'auto-défilement. **La justification de D-E9-6 était fausse** : les cinq
  arènes 321/322/323/327/480 animent (AnimTimer 4, 4 trames) **et** auto-défilent (`ScrollX/YSpeed 2`,
  facteurs 1/8) — la dérive entre horloges y est observable sur toute frame de rattrapage.
  `elapsedTime = k × 0.02f` donne exactement `k` ticks flottants (`0.02f × 50f`, `0.04f × 50f`,
  `0.08f × 50f` arrondissent à 1f/2f/4f) : un harnais peut piloter les deux horloges en phase.
- Inventaire des tests touchés par S2 (noms exacts, `Alundra.Tests`, vérifié par deux relecteurs) :
  `BackdropRendererTests.cs` — **22** `[Fact]` (`BackdropDocument_DeserializesOverlayTintFields`,
  `BackdropDocument_WithAbsentOverlayFields_DefaultsToTintDisabled`, `TintSortKey_IsStrictlyBelowAGround1LayerKey_RegardlessOfDepthOrder`,
  `HasContent_IsTrueFromTintAlone_WithZeroLayers`, `Draw_WithTintAndZeroLayers_SubmitsOneAlphaBlendQuadAtTheTintSortKey`,
  `ResolveGroundLayerBlend_MapsAllFourGroundBlendModes_ExactBlendAndTintPairs`,
  `ResolveGroundLayerBlend_GroundFalseBlendMode1_StaysOpaque_OutOfScopeBucketUntouched`,
  `ResolveGroundLayerBlend_UnknownGroundBlendMode_FallsBackToOpaqueWhite`, `Draw_Factor1Layer_StaysWorldGlued_WhenCameraMovesVertically`,
  `UpdateAndDrawBackdrop_ProductionSite_PinsAbsoluteParallaxOffset_OnClampedScroll`,
  `UpdateAndDrawBackdrop_ProductionSite_AnchorsTheCanvasOnTheOriginalScreen_NotOnTheWindowPixels`,
  `AdvanceAnimation_FourFramesTimer6_ProducesExactCadence_OverFortyTicks`, `AdvanceAnimation_FourFramesTimer4_FirstPlateauFour_ThenFivePerFrame`,
  `AdvanceAnimation_OneFrameLayer_CounterAlwaysZero`, `Draw_AfterAdvancingTicks_SubmitsTheExpectedFrameTexture`,
  `ResolveFrameAssetIds_LayerWithNoFrameIds_ResolvesToExactlyTheTextureAssetId`, `ResolveFrameAssetIds_LayerWithFrameIds_ReturnsThemAsGiven`,
  `AdvanceAnimation_LayerResolvedFromNoFrameIds_NeverLeavesFrameZero_NoException`,
  `LoadLayerFrames_FrameOneFails_FallsBackToFrameZeroOnly`, `LoadLayerFrames_FrameThreeFails_FallsBackToFrameZeroOnly_NotToThePartialPrefix`,
  `LoadLayerFrames_FrameZeroFails_SkipsTheWholeLayer`, `LoadLayerFrames_AllFramesSucceed_ReturnsTheFullDenseArray`) ;
  `BackdropOffsetMathTests.cs` — 4 `[Fact]` + 4 `[Theory]` à 21 `[InlineData]` = **25** cas ;
  `BackdropAnimationReplayProductionTests.cs` — **2**. Conservés tels quels : `BackdropLoaderTests.cs`
  (5), `AlundraCameraMathTests.cs` (2).

### 1.3 L'original : une seule horloge, des accumulateurs par couche

- Une itération de boucle = `RenderScene()` puis `Update(0)` puis `FrameNumber++` à 50 Hz
  (`GameEngine.cs:225-231`) ; les fonds lisent le défilement clampé **de la même frame**
  (`GraphicManager.cs:37-68`, clamp `:75-122`) ; le fondu est dessiné après eux.
- Par couche et par frame, dans `RenderLayerToBuffer` (`GraphicManager.cs:868-925`) : parallaxe entière
  (`:868-869`) → cadence V `if (++AnimFrameTimer > AnimTimer) { if (++AnimFrameCounter >= animNum) = 0;
  AnimFrameTimer = 0; }` (`:871-880`) → auto-défilement **accumulé** `TimerX++; OffsetX += ScrollXSpeed;
  if (|period| > 0 && TimerX >= |period|) { OffsetX += ScrollDirX; TimerX = 0; }` (idem Y ; `:882-898`)
  → enroulement dans [0,640)/[0,480) qui mute `OffsetX/Y` (`:903-925`, retranche un multiple du canevas :
  résultat enroulé identique à la forme non mutante) → dessin avec `vAnim = (counter << 8) / animNum`
  (`:943`, `:979`). `ScrollDirX/Y` est fixé au chargement : 0 si période 0, sinon `((Speed >= 0) ^
  (Period < 0)) ? +1 : −1` (`ScrollScreen.cs:246-270`). La forme close de la DLL et cet accumulateur
  coïncident pour tout compte de ticks entier (`BackdropOffsetMathTests.cs:31-41`), enroulement compris.
- État par instance de carte, remis à zéro au chargement (`ScrollScreen.cs:86-95`, `:207-212`).

### 1.4 Le corpus

- 132 couches `Tiles` exportées : **86 `Ground=true`** (overlays), **46 `Ground=false`** (vrais fonds,
  21 cartes : arènes 321/322/323/327/480/471/324/326, Overworld 1/13/17, Nightmares 48-54/435-438,
  Nestus 109-113, 153, 439, 355/356, 469/470). 7 cartes animées (159/160 `AnimTimer 6`, statiques ;
  321/322/323/327/480 `AnimTimer 4` + `Speed 2` + facteurs 1/8, **`Ground=false`, `BlendMode 0`**).
  12 compagnons à auto-défilement non nul. 16 teintes plein écran. 389 : `Ground=true`, `BlendMode 1`,
  facteurs 1/1, `ScrollXPeriod 10` / `ScrollYPeriod 5` (+1 px tous les 10 / 5 ticks).
- **Plans de tuiles** (`.tileMap`, valeur vide **−1** ; comptage `select(. == -1)` par couche
  `Render_*`, reproductible par `jq`) : **389** — plan 0 : 16 vides / 3104 tuiles ; plans 1-3 : 418 / 51 /
  5 tuiles. **159** — plan 0 : 214 vides (le vide noir de la caverne) / 2906 tuiles ; plans 1-5 : 535 /
  163 / 81 / 38 / 1. **321** — plan 0 : **1761 vides / 1359 tuiles** (le sol de l'arène occupe la moitié
  basse ; le vide au-dessus est la région où le fond se voit) ; plans 1-2 : 473 / 49. Les plans ≥ 1
  portent murs et sols surélevés, ceux qu'E7 retire vers l'overlay trié à z = 0 (`WallPlacementOverlay.cs:270,327`).

### 1.5 Profondeur : mesuré, pas déduit

- La révision 1 déduisait qu'un fond `Ground=false` à z = 0, flushé après le batch statique à égalité
  de profondeur avec le plan 0, **recouvrirait le sol**. **La mesure le réfute** : capture du port
  actuel sur la 321 (`scratchpad/port-321-before-a/b.png`, `FirstWorldLoaded` basculé le temps du
  lancement puis restauré à 389, journal sans avertissement) — sol du plan 0 **intact** (moitié basse de
  la zone client, à partir de la ligne ≈ 616 sur 944), nuages rouges du fond visibles **uniquement**
  dans le vide au-dessus, animés (trames différentes à 350 ms) et défilants. Le port actuel est donc
  correct sur les trois cartes d'acceptation ; la mécanique exacte qui fait passer le sol devant un
  quad de fond à même z (état de profondeur du batch statique `SpriteRendererComponent.cs:266-300` vs
  flush `:155-206`, ou routage des tuiles) n'a pas été lue jusqu'au bout et **n'a pas à être changée** :
  la politique Z du mécanisme est **z = 0 pour tous les quads, exactement comme aujourd'hui**
  (`BackdropRenderer.cs:418`, `:467` ; D-E9b-4), pinée par la translation Z des soumissions ; S0
  documente la mécanique observée dans la page `docs/engine/` après lecture.
- Le fondu (`ScreenEffects`, z = 0) couvre tous les quads (égalité, `LessEqual`).

- **AMENDEMENT (E9.c, `docs/plan-e9c-defauts-321.md`, §1.2, D-E9c-5, 2026-09-06).** La mesure
  ci-dessus ne regardait que le **vide** au-dessus du sol de la 321 : le sol du plan 0 « intact »
  observé n'était que sa **moitié basse**, jamais la rangée la plus haute des os. E9.c a mesuré plus loin et trouvé que la 321 conserve 166 tuiles en
  `Render_0` (z_offset 0.0, rangées 13-19, les doigts d'os les plus hauts) routées par
  `TileMapComponent`/`SpriteRendererComponent.DrawStaticBatch` dans le lot statique **immédiat**,
  dessiné pendant `World.Draw` **avant** le vidage de la file triée (où vit le fond) — à égalité de
  profondeur, le dernier dessiné gagne, donc le fond gagnait déjà sur cette rangée-là, invisible
  seulement parce que le reste des tuiles est dans la surimpression triée et gagne la même égalité
  après le fond. **z = 0 pour tous les quads était donc un sous-correctif** : juste sur le vide, faux
  sur toute tuile partageant le lot statique immédiat. E9.c corrige la politique Z (une couche
  `Background` recule à `cameraTarget.Z − BackgroundDepth`, configuration `BackgroundDepth`, défaut 1)
  et **D-E9b-4 est remplacée** par cette politique — voir `docs/engine/scrolling-layers.md` §4 pour le
  détail et la justification par l'original (`GraphicManager.cs:825-826`). L'historique ci-dessus reste
  tel quel : la mesure d'alors était honnête sur ce qu'elle a effectivement regardé.

### 1.6 Suites héritées qui entrent au périmètre

- **Ciseaux** (A1) : `Draw` passe `(0,0,320,240)` comme rectangle de découpe en pixels-machine
  (`BackdropRenderer.cs:399-403`, `:470`) ; inerte tant que `ScissorTestEnable` est faux. Le mécanisme
  moteur reçoit le rectangle **en paramètre** (D-E9b-6), résolu par l'appelant là où un device existe.
- **`ScreenEffectComponent`** : demi-étendues en pixels (`:56`, `:90-95`), doc affirmant une parité avec
  le bloc de teinte de la DLL fausse depuis B5 (`:70-76`). Correctif D-E9b-12 avec repli.

### 1.7 Corrections tracées

**Tour 1 (révision 2), douze blocages levés** : enveloppe — surcharge sans ciseaux lisant le device à
la mise en file (→ rectangle en paramètre) ; tolérance visuelle par ligne indécidable sur des motifs
qui défilent (→ critère structurel calibré) ; comptage « 321 : plan 0 = 3119 » faux, mon `jq` comptant
les `−1` comme des tuiles (→ §1.4 recompté) ; S0 — même point ciseaux ; repli absent quand la caméra
active est nulle (→ D-E9b-12) ; S1 — coutures moteur inaccessibles depuis `Alundra.Tests` (→ API
publiques) ; `ResolveGroundLayerBlend` « déplacé » alors que le renderer l'appelle (→ délégation) ;
preuve « diff vide » vacue (→ trois contrôles) ; S2 — acquisition du service non nommée (→
`AttachService`) ; table des tests absente (→ table complète) ; « hors porte de gel » sans pin (→ pin
`MenuOpen`) ; protocole de capture (→ restauration d'`AlundraGame.json`, S1 inerte). Hors relecture :
politique Z « −1 » retirée sur mesure (§1.5) ; couches `Disabled` filtrées.

**Tour 2 (révision 3), dix blocages, dispositions** — tous **FIX** :
- enveloppe : « z = `cameraTarget.Z` comme aujourd'hui » contredisait le `0f` réel → **z = 0**, invariant
  `Target.Z == 0` cité (§0.2), trajectoire du harnais S1 à Z constant ; arithmétique 749 fausse → comptes
  recalculés terme à terme (S1 791, S2 752) ; « paramètre `fullViewport` » inexistant (variable locale)
  → paramètre optionnel **final** `Rectangle? scissorRectangle = null` (D-E9b-12) ;
- S0 : même paramètre final (les tests passent la texture en 5e argument positionnel) ; consommation
  des ticks non spécifiée (double avance sans poussée, aperçu éditeur) → D-E9b-3 : `SetFrame` arme,
  `Advance` consomme, `Submit` inerte sans poussée reçue, tests et mutation ; `LayersVersion` sans
  sémantique → strictement croissant, `Clear()` compris, test et mutation ;
- S1 : `BuildDefinitions` sans les deux autres gardes de `Load` ni la conversion `string?[]` → `Guid[]`
  → D-E9b-8 : couche sautée si `Scrollar == null` ou `TextureAssetId` vide ; id nul/vide/inanalysable →
  `Guid.Empty`, que le chargeur résout en null (D-E9b-7 inchangée, même `Frames.Length` qu'aujourd'hui) ;
  deux tests ; compte S1 791 ;
- S2 : compte faux et deux tests de la matrice de mutations absents du contenu → tests nommés
  (avertissement « service nul », « second monde »), somme terme à terme = 752 ; pins non construisibles
  faute d'état de dernière poussée public → D-E9b-1 : `LastPushedScrollX/Y`, `PendingTicks`,
  `CameraTarget`, `FramesPushed`, livrés en S0 ; critère « classes rouges » de la 321 indéfini → région,
  grandeur `R − max(G, B) > 60`, seuil calibré (D-E9b-13).

**Clôture (non re-relue)** : enveloppe, S1, S2 READY ; S0 REVISE — les trois cas de test de la source
de taille de `ScreenEffectComponent` étaient placés dans `Update`, qui sort avant tout calcul sur le
montage headless et dont le repli `ScreenSizeWidth/Height` lève sur un jeu non initialisé
(`CasaEngineGame.cs:80-108`) → couture pure `TryGetCameraViewSize` (D-E9b-12), repli côté appelant, cas
et mutation réécrits comme des appels directs. Clarification simultanée : `SetFrame` écrase la frame en
attente (D-E9b-3).

### 1.8 Mesure du critère visuel (2026-09-04, pendant l'exécution)

Le critère visuel de D-E9b-13 a été **calibré avant usage**, sur le HEAD de S1 (visuellement inerte).
Première rédaction : **non discriminante**. Deux exécutions du MÊME binaire donnaient
`band_last_row` = [327, 327, 335, 943] sur la 159, un écart de 11 unités sur `sea_mean` de la 389 et
de 21 sur les lignes pourtant statiques du sol de la 321 ; appliquer la clause d'élargissement du
plan aurait produit des seuils vides de sens (un « après » entièrement faux serait passé).

Diagnostic : l'animation est **déterministe**, pas bruitée. Deux clichés pris au **même décalage**
après l'apparition de la fenêtre sont identiques au chiffre près (`red_fraction` de la 321 :
0,040803571428571425 dans les deux runs) ; l'écart venait de comparer un cliché à 10 s avec un
cliché à 20 s du même cycle. L'aberration `943` de la 159 n'était pas un fondu mais la sensibilité du
seuil `> 8` à une dérive de quelques unités du noir de fond (4,7 → 10,3), qui reclasse d'un coup
toutes les lignes sous la bande. Les lignes « statiques » du sol de la 321 portent en fait le boss
animé (journal : entité `◆Zorgia`).

Remède retenu : rafale de 12 images à 250 ms à partir de 12 s, réduite en **médiane par pixel**
(déviation moyenne par trame à la médiane : 0,9 sur la 159, 5,2 sur la 321, 25,5 sur la 389 — la
réduction a bien de quoi moyenner). Sur les médianes, la dispersion entre deux rafales tombe à 0
(`band_last_row`), 0,007 (`band_plateau`), 0,020 (`sea_mean`) et 0,045 (`floor_row_means`), pour des
tailles d'effet de 8, 4,07, 1,54 et 3,79 sur une toile décalée de 8 lignes : les seuils de D-E9b-13
sont désormais dix à quarante fois inférieurs à ce qu'ils doivent attraper. Exception assumée : la
médiane **détruit** le critère des nuages de la 321 (ils défilent et ne couvrent un pixel qu'une
minorité de la rafale, la médiane rend 0,0) — il se lit donc sur les images brutes, où il est
reproductible au chiffre près.

---

---

## 2. Décisions de conception

- **D-E9b-1 — Deux objets moteur, un contrat de poussée, coutures publiques.** `ScrollingLayerService`
  (namespace `CasaEngine.Framework.Rendering.ScrollingLayers`, **sans type GPU** : définitions de
  couches, état par tick, trame courante par couche, dernière poussée) et `ScrollingLayerComponent :
  GameComponent` (résout les textures par id via `AssetContentManager`, soumet les quads dans `Update`
  via `SpriteRendererComponent`). Instancié par `CasaEngineGame.Initialize` à côté de
  `ScreenEffectComponent` (`:355`), propriété publique `ScrollingLayerComponent`, `UpdateOrder` =
  nouvelle valeur `ScrollingLayers` **ajoutée en fin d'enum**. **API publiques** (tout ce que la DLL et
  `Alundra.Tests` consomment) — sur le service : `SetConfiguration(ScrollingLayerConfiguration)`,
  `SetLayers(ReadOnlySpan<ScrollingLayerDefinition>)` (copie dans un tableau interne dimensionné une
  fois), `SetTint(ScrollingTintDefinition?)`, `Clear()`, `SetFrame(int scrollX, int scrollY, int ticks,
  Vector3 cameraTarget)`, `Advance()`, `int LayersVersion` (**démarre à 0, strictement croissant** :
  incrémenté par `SetLayers`, `SetTint` et `Clear()`, jamais remis à zéro), `int LayerCount`,
  `bool TryGetLayerState(int index, out ScrollingLayerState state)` (struct : `AnimFrameTimer`,
  `AnimFrameCounter`, `OffsetX/Y`, `TimerX/Y`, dernier `LayerOffsetX/Y` calculé, `int`), et l'**état de
  dernière poussée**, modifié par `SetFrame` seul : `int LastPushedScrollX`, `int LastPushedScrollY`,
  `int PendingTicks` (armé par `SetFrame`, remis à 0 par `Advance()`), `Vector3 CameraTarget`,
  `int FramesPushed` (compteur, +1 par `SetFrame`, remis à 0 par `Clear()`), `bool HasPendingFrame`
  (vrai entre un `SetFrame` et l'`Advance()` qui le consomme). Sur le composant : `Service`,
  `void ResolveTextures(Func<Guid, Texture2D?> loader)` (résolution explicite, celle qu'`Update` appelle
  avec le chargeur `AssetContentManager` quand `LayersVersion` change ; `Guid.Empty` → le chargeur n'est
  pas appelé, trame nulle), `void Submit(SpriteRendererComponent renderer, Vector3 cameraTarget,
  Rectangle scissorRectangle)`. Une **définition de couche** porte : `Guid[] FrameTextureAssetIds`
  (≥ 1), `FactorXNum/Denom`, `FactorYNum/Denom`, `ScrollXSpeed/Period`, `ScrollYSpeed/Period`, `AnimTimer`,
  `RenderPass2D Pass`, `int SortingLayer`, `int OrderInLayer`, `int StableId`, `SpriteBlendMode Blend`,
  `Color Tint`. Une **définition de teinte** : couleur, alpha, clé. La **configuration** : taille de la
  toile (640×480), taille de vue en unités monde (320×240).
- **D-E9b-2 — Contrat par frame et acquisition du service.** La DLL atteint le service par
  `AlundraBackdropStage.AttachService(ScrollingLayerService? service)`, appelé dans
  `AlundraWorldProxy.InitializeWithWorld` **avant** `Load` avec `world.Game?.ScrollingLayerComponent?.Service`
  (même geste que `InstallScreenFadeSystems`, `:870-882`) ; les tests injectent un service réel par le
  même `AttachService` (interne, `InternalsVisibleTo Alundra.Tests`) sur un montage dont le `Game` non
  initialisé n'a pas de composant. **Service nul** : `Load` et `PushFrame` sont des no-op, **mais** si
  `world.Game != null` au moment de l'attachement, un avertissement `Logs.WriteWarning` est émis une
  fois (règle « lire le journal d'abord ») et c'est un **arrêt** §4 en production. Chaque frame,
  `PushFrame(ticks, camera)` = `service.SetFrame(scroll.X, scroll.Y, ticks, Target)` — **au même site**
  que `UpdateAndDrawBackdrop` aujourd'hui (`AlundraWorldProxy.cs:1520`), inconditionnel, hors porte de
  gel, après la caméra résolue, avant le fondu ; ne dépend ni de `world.Game`, ni de `HasContent`, ni du
  nombre de couches. `ticks` vient de `LogicTicksThisFrame` (plancher collant compris) ; `scrollX/Y` de
  `ToOriginalScrollSpace(Target)` ; `cameraTarget` = `ResolvedCamera.Target` (poussé plutôt que lu sur
  `ActiveView` : site de production pinable headless, aucune vue requise). Le composant consomme l'état
  à son `Update` de la même frame (§1.1) : `Advance()` **puis** `Submit(...)` — avance-puis-dessin comme
  `GraphicManager.cs:871-943`.
- **D-E9b-3 — Une seule horloge, par tick, accumulateurs par couche ; consommation des ticks.**
  `SetFrame` **arme** une frame en attente (`PendingTicks`, `HasPendingFrame`) ; une seconde poussée
  sans `Advance()` intermédiaire **écrase** la précédente (la dernière poussée fait foi, jamais de
  cumul — c'est ce que lisent les pins de S2 sur un montage sans composant). `Advance()` la
  **consomme** : par tick et par couche, dans cet ordre — cadence V (`++timer > AnimTimer` puis
  `++counter >= Frames.Length` puis `timer = 0`) ; auto-défilement `TimerX++; OffsetX += SpeedX; if
  (|PeriodX| > 0 && TimerX >= |PeriodX|) { OffsetX += DirX; TimerX = 0; }` (idem Y), `Dir` fixé à la
  définition comme `ScrollScreen.cs:246-270` — puis `PendingTicks = 0`. `Submit` **ne soumet rien tant
  qu'aucun `SetFrame` n'a été reçu depuis le dernier `Clear()`/`SetLayers`** (`FramesPushed == 0`) : une
  frame moteur sans poussée de la DLL (aperçu éditeur, `UpdateGameplayScripts = false`) ne fait ni
  avancer ni dessiner. À la soumission : `offset = Wrap(parallax(scroll) + OffsetAccumulé, canvas)`.
  `ticks = 0` n'avance rien ; `ticks = 4` avance quatre fois. La forme close et l'horloge flottante
  disparaissent. **Conséquence assumée** : sur une frame de rattrapage ou de saturation,
  l'auto-défilement suit le tick logique (au plus un tick d'écart avec l'ancienne horloge) — fidèle à
  l'original ; aux frames nominales (1 tick), identique bit pour bit à aujourd'hui (§1.3).
- **D-E9b-4 — Politique Z : z = 0, exactement comme aujourd'hui** (§1.5, `BackdropRenderer.cs:418`,
  `:467`) pour tous les quads (couches et teinte), sous l'invariant `Target.Z == 0` (§0.2). Pinée par la
  translation Z des soumissions (= 0). La page `docs/engine/` consigne la mécanique observée qui fait
  passer le sol devant un fond à même z, après lecture en S0.
  **AMENDÉE par D-E9c-5 (`docs/plan-e9c-defauts-321.md`) : une couche `Background` recule à
  `cameraTarget.Z − BackgroundDepth` (configuration, défaut 1) ; les autres passes et la teinte restent
  à `cameraTarget.Z`.** La mesure qui avait fixé z = 0 pour tout ne regardait que le vide au-dessus du
  sol de la 321 (§1.5, amendement) ; la rangée de tuiles la plus haute, dans le lot statique immédiat,
  était overpaintée depuis toujours. Voir §1.2.f et D-E9c-5 du plan E9.c pour la mesure et la
  justification par l'original.
- **D-E9b-5 — Ancrage et couverture inchangés (B1/B5).** Coin haut-gauche de la toile = `cameraTarget +
  (−viewWidth/2, +viewHeight/2)` avec la taille de vue **poussée en configuration** (320×240 : politique
  Alundra, E5) ; origines couvrantes = port de `ComputeCoveringOrigins1D` **sans allocation** (bornes
  calculées, boucle sur entiers, au plus 2×2 quads par couche). Division entière de la parallaxe.
- **D-E9b-6 — Ciseaux en paramètre, résolus par l'appelant.** `Submit(renderer, cameraTarget,
  scissorRectangle)` passe le rectangle reçu à la surcharge à ciseaux explicite
  (`SpriteRendererComponent.cs:586-592`) ; `ScrollingLayerComponent.Update` le résout **une fois par
  frame** — `GraphicsDevice.ScissorRectangle` sous le même garde que la texture 1×1
  (`ScreenEffectComponent.cs:112-140`), à défaut `(0, 0, ScreenSizeWidth, ScreenSizeHeight)` en pixels.
  Les tests headless passent leur rectangle et le relisent sur `_spriteDatas`. `Submit` n'accède jamais
  au `GraphicsDevice`. Plus jamais un rectangle en unités monde.
- **D-E9b-7 — Textures et repli côté moteur (D-E9-9 portée).** `ResolveTextures(loader)` résout chaque
  trame par le délégué (en production : `Load<Texture>(id)` + `Load(acm)` + `.Resource` ; un id
  `Guid.Empty` donne une trame nulle sans appeler le délégué) ; trame 0 nulle → couche ignorée avec
  avertissement ; trame `f ≥ 1` nulle → `[frame0]` seule, jamais de préfixe partiel — donc le modulo
  d'animation `Frames.Length` est le même qu'aujourd'hui pour la même entrée (`LoadLayerFrames`, §1.2).
  `Update` appelle `ResolveTextures` à la première frame où `LayersVersion` change (jamais par frame),
  et **remet à zéro** les compteurs de couche à cette occasion ; textures possédées par
  l'`AssetContentManager` (rien à disposer) ; texture 1×1 de teinte créée paresseusement sous garde et
  disposée dans `Dispose`. **Zéro couche et une teinte → un quad** (les 16 cartes à teinte seule) ;
  zéro couche et pas de teinte → aucune soumission ; trame courante nulle → couche non soumise.
- **D-E9b-8 — La politique reste dans la DLL.** `AlundraBackdropStage.BuildDefinitions(BackdropDocument)`
  (**pure**) traduit **exactement comme `BackdropRenderer.Load` aujourd'hui** : une couche n'est
  traduite que si `Mode == "Tiles" && Scrollar != null && TextureAssetId non vide` (`:226` ; `Disabled`,
  `Cellular`, `Scrollar` nul, id vide → absente) ; `Ground/BlendMode` → (`Pass`, `Blend`, `Tint`) par
  `ResolveGroundLayerBlend` (dont la **définition** passe sur le stage ; T8 conservé : `(false, 1)` →
  `Opaque`) ; `Ground ? Effects : Background` ; `SortingLayer 0`, `OrderInLayer = DepthOrder`,
  `StableId = LayerId` ; teinte `(R,G,B,128)`, clé `Effects` sorting −1 ; trames = `FrameTextureAssetIds
  ?? [TextureAssetId]` converties en `Guid[]` **de même longueur**, chaque id nul, vide ou inanalysable
  devenant `Guid.Empty` (jamais d'exception, jamais de raccourcissement : D-E9-9 s'applique ensuite côté
  moteur, D-E9b-7) ; configuration 640×480 / 320×240. `ApplyOriginalBackgroundClearColorOnce` reste tel
  quel. `BackdropLoader.TryParseMapIndex` reste (musique).
- **D-E9b-9 — Par monde.** `Service.Clear()` puis `SetConfiguration/SetLayers/SetTint` à chaque
  `AlundraBackdropStage.Load` (un `InitializeWithWorld` par monde) ; `LayersVersion` strictement
  croissant garantit que le composant ré-résout les textures et remet les compteurs à zéro. Rien n'est
  partagé entre mondes (leçon `CreateWorldWorkingCopy`).
- **D-E9b-10 — Coexistence prouvée, puis retrait.** S1 livre l'adaptateur **sans le brancher** en
  production et un **harnais d'équivalence** (`Alundra.Tests`, qui référence moteur et DLL, via les API
  publiques de D-E9b-1) qui, pour les compagnons réels 389, 159 et 321, compare tick à tick sur 2 000
  ticks (frames à 0, 1, 2 et 4 ticks ; ancienne horloge pilotée par `elapsedTime = k × 0.02f`, ticks
  exacts §1.2 ; trajectoire de `Target` clampée aux bornes de la carte, **Z = 0 constant**) l'ancien chemin
  (`BackdropOffsetMath` + `AdvanceAnimation` + `Draw` headless, textures `Texture2D` non initialisées
  injectées par réflexion dans `_layers`) et le nouveau (`ScrollingLayerService` + `Submit` headless,
  **les mêmes instances** `Texture2D` fournies par `ResolveTextures(id => tableau[id])`) : offsets, index
  de trame, `Assert.Same` sur les textures, positions, clés, blend, couleurs, z (= 0 des deux côtés)
  **égaux**, au seul delta documenté près (rectangle de ciseaux : `(0,0,320,240)` ancien vs rectangle
  passé par le test nouveau). S2 branche l'adaptateur et **supprime** `BackdropRenderer`,
  `BackdropOffsetMath`, `LayerRuntime` et leurs tests.
- **D-E9b-11 — Pins re-hébergés** (table complète en S2). Dans `CasaEngine.Tests` (mécanisme) :
  parallaxe entière `5·1/3 = 1` ; cadence `0×6,1×7,2×7,3×7,0×7,1×6` et `0×4` puis 5 ; une trame →
  compteur toujours 0 ; auto-défilement `Speed 0 / Period 10` → +1 px aux ticks 10, 20, … et `Speed 2 /
  Period 0` → +2 px/tick ; ancrage : vue 320×240, `Target (1087, −839)`, couche facteur 0/1 → **un** quad,
  coin `(927, −719)`, et facteur 1/1 → offsets `(287, 239)` ; delta vertical (« glued ») ; z des
  soumissions = 0 ; teinte seule → un quad `AlphaBlend` à `(Effects, −1)` ; texture soumise = trame
  courante après 7 et 28 ticks ; repli D-E9-9 (f0, f1, f3, dense) et `Guid.Empty` ; `ticks = 0` → aucune
  avance ; **consommation** : un `SetFrame(ticks 2)` puis deux `Advance()` → deux ticks, pas quatre ;
  aucune soumission avant le premier `SetFrame` ; `LayersVersion` strictement croissant sur
  `Clear()`/`SetLayers`, ré-résolution et compteurs remis à zéro ; état de dernière poussée relu après
  `SetFrame` seul ; ciseaux = rectangle passé. Dans `Alundra.Tests` (politique + site de production) :
  traduction des compagnons 389/159/321 (valeurs exactes, couches `Disabled` absentes) ; gardes
  `Scrollar` nul / id vide ; id inanalysable → `Guid.Empty` et `[frame0]` ; `ResolveFrameAssetIds` ×2 ; T8
  ×3 ; clé de teinte sous la clé `Ground=1` ; `BackdropDocument` ×2 ; **site de production** sur le
  montage réel 389 (`proxy.Update`, recette B3 : `_clearColorApplied`, frame d'amorçage, service injecté
  par `AttachService`) → `FramesPushed` incrémenté, `PendingTicks == 0` puis `2`, `LastPushedScrollX/Y ==
  ToOriginalScrollSpace(Target)`, `CameraTarget == Target` ; **porte de gel** : même montage,
  `GameplayBlockedMask.MenuOpen` posé → `PendingTicks == 2` quand même ; **monde sans `Game`** : service
  injecté → `FramesPushed` incrémenté ; **avertissement** : `Load` avec `Game` non nul et service nul →
  un avertissement journalisé ; **second monde** : deux `Load` successifs → `LayerCount` de la seconde
  carte seulement, `LayersVersion` strictement croissant.
- **D-E9b-12 — `ScreenEffectComponent` corrigé dans S0.** La résolution de la taille de vue est une
  **couture pure, testable sans périphérique** : `internal static bool TryGetCameraViewSize(
  Camera2dComponent? camera, out int width, out int height)` (`InternalsVisibleTo("CasaEngine.Tests")`,
  `CasaEngine/InternalsVisibleTo.Tests.cs:3`) — vrai avec `(Viewport.Width / Zoom, Viewport.Height /
  Zoom)` quand `camera` est non nul et son viewport non vide (320 × 236 en Alundra), faux sinon (caméra
  nulle, viewport 0). `Update` l'appelle avec `ActiveView?.Camera as Camera2dComponent` et, sur
  `false`, **replie** sur `ScreenSizeWidth/Height` fournis par l'appelant — le fondu ne cesse jamais
  d'être soumis ; ni la couture ni `SubmitOverlay` ne lisent `_game`. Signature étendue
  par un paramètre optionnel **en dernière position** : `SubmitOverlay(SpriteRendererComponent renderer,
  Vector3 cameraPosition, int viewportWidth, int viewportHeight, Texture2D overlayTexture = null,
  Rectangle? scissorRectangle = null)` ; `null` → `new Rectangle(0, 0, viewportWidth, viewportHeight)`
  (comportement actuel, correct tant que les tailles reçues sont des pixels — les quatre tests
  existants) ; `Update` passe le rectangle du périphérique résolu sous garde (D-E9b-6). Changement
  **purement additif** : les quatre `ScreenEffectComponentSubmissionTests` (`:35`, `:77`, `:91`, `:104`)
  compilent et passent **sans modification**. Doc `docs/engine/screen-effects.md` : parité retirée,
  renvoi vers la page des couches défilantes.
- **D-E9b-13 — Preuve.** Aucun changement convertisseur (arrêt si un fichier du convertisseur bouge ;
  aucun export). Moteur : baseline `CasaEngine.Tests` re-mesurée **avant S0** (« mêmes noms d'échecs,
  aucun nouveau ») ; `dotnet build CasaEngine.Tests` explicite avant tout `--no-build`. DLL :
  `Alundra.Tests` verte à chaque tranche, comptes écrits d'avance (S1 : **791** ; S2 : **752**, sommes
  terme à terme en §3).
  **Visuel (S2), protocole MESURÉ le 2026-09-04 — remplace la première rédaction (voir §1.8).**
  L'animation du jeu est **déterministe** : deux captures prises au **même décalage** après
  l'apparition de la fenêtre sont reproductibles au chiffre près, et toute la « dispersion » de la
  première calibration venait de comparer des clichés de phases différentes (10 s contre 20 s du même
  cycle). Protocole : `scratchpad/e9b_capture.ps1` bascule `FirstWorldLoaded` en **sauvegardant et
  restaurant** `AlundraGame.json`, lance le jeu, **vérifie que `GetForegroundWindow` est la fenêtre du
  jeu avant chaque cliché** (garde obligatoire : sans elle une capture peut prendre une autre fenêtre —
  incident réel du 2026-09-04) et prend une **rafale de 12 images à 250 ms, à partir de 12 s** ;
  `e9b_median.py` en tire la **médiane par pixel** (le défilement et les cycles s'annulent, la
  géométrie fixe demeure). **avant** = HEAD de S1 (visuellement inerte), **après** = S2, mêmes
  décalages. Critères, avec la dispersion mesurée entre deux rafales « avant » et la taille d'effet
  d'une toile décalée de 8 lignes : **159** — sur la médiane, `band_last_row` (dispersion 0, effet 8)
  à **±2 lignes** et `band_plateau` (dispersion 0,007, effet 4,07) à **±0,5** ; **389** — `sea_mean`
  de la médiane (dispersion 0,020, effet 1,54) à **±0,1** ; **321** — `floor_row_means` des lignes
  [640, 944) de la médiane (dispersion 0,045, effet 3,79) à **±0,5 par ligne**, et **présence des
  nuages** mesurée sur les **images brutes au décalage correspondant** (`red_fraction` = fraction des
  pixels des lignes [0, 560) vérifiant `R − max(G, B) > 60`, identique au chiffre près entre deux
  runs) à **±0,005** — la médiane détruit ce critère (les nuages défilent et ne couvrent un pixel
  qu'une minorité de la rafale), il se lit donc sur les images brutes. **Limites écrites** : sur la
  389 le fond occupe toute la vue, aucune région n'en est exempte, donc `sea_mean` prouve l'ancrage
  mais **pas** « le fond n'est plus dessiné du tout » ; sur la 321 les lignes du sol portent aussi le
  boss animé. Ces deux trous sont couverts par le vrai oracle numérique, `BackdropEquivalenceTests`
  (2 000 ticks, trame par trame, les trois cartes) — les captures sont un filet de sécurité.
  Validation finale en jeu par l'utilisateur.
- **D-E9b-14 — Gels documentés** (§0.2) : `0xA4`, secousse scriptée, marcheur de teinte, dénominateur
  0 ; aperçu éditeur sans fond ; vue unique (`ActiveView`) ; plafond 10 000 sprites (≤ 4 quads par
  couche + 1 teinte : sans risque).

---

## 3. Découpage en tranches

| Tranche | Propriétaire | Prérequis | Commits |
|---|---|---|---|
| S0 — mécanisme moteur inerte | moteur (sous-module) | baseline `CasaEngine.Tests` re-mesurée | 1 commit sous-module + 1 bump du pointeur |
| S1 — adaptateur DLL non branché + harnais d'équivalence | DLL | S0 (pointeur bumpé, checkout standalone fusionné) | 1 |
| S2 — bascule : branchement, retrait du renderer, pins, captures | DLL | S1 | 1 (+ docs) |
| S3 — documentation et clôture | docs | S2 validée en jeu | 1 |

Séquentiel strict (un seul rédacteur, un seul build à la fois — leçon E9.a). Après chaque commit
moteur : rappel du `fetch` + `merge` du checkout standalone `D:\development\repo\CasaEngineMonogame`
(le lanceur y tourne).

### S0 — Le mécanisme dans le moteur (D-E9b-1..7, 12)

**Contenu** (sous-module `CasaEngineMonogame`) :
1. `CasaEngine/Framework/Rendering/ScrollingLayers/` : `ScrollingLayerDefinition`, `ScrollingTintDefinition`,
   `ScrollingLayerConfiguration`, `ScrollingLayerState` (structs publics), `ScrollingLayerService`
   (API publique complète de D-E9b-1, état de dernière poussée compris ; `Advance()` D-E9b-3 ;
   `LayersVersion` monotone ; fonctions **pures statiques** publiques `ComputeParallaxOffset`,
   `WrapOffset`, `CoveringOriginStart/Count` — port sans allocation de `BackdropOffsetMath`).
2. `CasaEngine/Framework/Application/Components/ScrollingLayerComponent.cs` : `GameComponent`, propriété
   `Service`, `ResolveTextures(loader)` public, `Submit(renderer, cameraTarget, scissorRectangle)`
   public ; `Update` = si `LayersVersion` a changé → `ResolveTextures` avec le chargeur
   `AssetContentManager` et remise à zéro des compteurs ; si `HasPendingFrame` → `Service.Advance()` ;
   rectangle de ciseaux résolu sous garde (D-E9b-6) ; si `FramesPushed > 0` →
   `Submit(_game.SpriteRendererComponent, Service.CameraTarget, scissor)` : teinte puis couches, quads
   couvrants à `position = cameraTarget + (origin.X − halfW, halfH − origin.Y)`, z = 0, clé `(Pass,
   SortingLayer, OrderInLayer, 0, 0, 0, StableId)`, `Blend`, `Tint`, surcharge à ciseaux explicite.
   Aucune allocation ni accès au device dans `Submit` ; `Dispose` libère la texture 1×1.
3. `CasaEngineGame` : propriété + instanciation à côté de `ScreenEffectComponent` (`:54`, `:355`) ;
   `ComponentUpdateOrder.ScrollingLayers` ajouté en fin d'enum.
4. `ScreenEffectComponent` : D-E9b-12 (couture pure `TryGetCameraViewSize(camera, out w, out h)`
   appelée par `Update` avec repli `ScreenSizeWidth/Height` côté appelant, paramètre optionnel final
   `Rectangle? scissorRectangle = null`, ciseaux résolus dans `Update`, doc).
5. `docs/engine/scrolling-layers.md` (gabarit `screen-effects.md` : vue d'ensemble, service, contrat de
   poussée et consommation des ticks, formule de placement, horloge par ticks, politique Z et mécanique
   observée §1.5, ciseaux, fusion, limites V1 : vue active unique, pas d'éditeur, pas de sérialisation)
   + entrée dans `docs/README.md`.
6. Tests `CasaEngine.Tests/Rendering/ScrollingLayers/` : théories des fonctions pures (division entière,
   enroulement, origines couvrantes pour vue 320×240 / offsets 0 et 287/239) ; cadence (deux tables + une
   trame → 0) ; auto-défilement (`0/10` → +1 aux ticks 10, 20 ; `2/0` → +2/tick ; signes → `Dir`) ;
   `ticks = 0` / `= 4` ; **consommation** (un `SetFrame(2)` puis deux `Advance()` → deux ticks ;
   `PendingTicks` 2 puis 0 ; `HasPendingFrame`) ; **état de dernière poussée** relu après `SetFrame`
   seul (`LastPushedScrollX/Y`, `CameraTarget`, `FramesPushed`) ; **`LayersVersion`** (0 au départ ;
   `SetLayers`, `SetTint`, `Clear()` incrémentent ; deux cycles `Clear()`/`SetLayers` → deux versions
   distinctes, deux appels du chargeur, compteurs remis à zéro) ; `Submit` headless (montage
   `ScreenEffectComponentSubmissionTests`, textures par `ResolveTextures(id => …)`) : **aucune soumission
   avant le premier `SetFrame`** ; un quad à `(927, −719)` pour facteur 0/1 à `Target (1087, −839)` ;
   offsets `(287, 239)` pour facteur 1/1 (`TryGetLayerState`) ; delta vertical « glued » ; translation
   z = 0 pour `Background` et `Effects` ; `ScissorRectangle` soumis = rectangle passé ; teinte seule → un
   quad `AlphaBlend` à `(Effects, −1)` ; aucune soumission sans couches ni teinte ; texture soumise =
   trame courante après 7 et 28 ticks ; repli par délégué (f0 nulle → couche ignorée ; f1 et f3 nulles →
   `[frame0]` ; dense ; `Guid.Empty` → délégué non appelé, trame nulle) ; `ScreenEffectComponent`, par
   appels **directs à la couture** `TryGetCameraViewSize` (aucun accès à `_game.ScreenSizeWidth` ni au
   `GraphicsDevice`) : un `Camera2dComponent` dont le viewport est posé à 1280×944 (`SetPrivateField`
   ou `OnScreenResized`) et `Zoom = 4` → vrai, `(320, 236)` ; caméra nulle → faux ; viewport 0 → faux ;
   puis `SubmitOverlay(renderer, cameraPosition, 320, 236, texture, scissor)` sur le montage headless →
   coin `cameraPosition + (−160, +118)`, ciseaux = rectangle passé, `null` → `(0,0,320,236)`.
**Acceptation** : `dotnet build CasaEngine.Tests` puis `dotnet test --no-build` : nouveaux tests verts,
échecs préexistants **nommément identiques** à la baseline, aucun nouveau ; les quatre
`ScreenEffectComponentSubmissionTests` **non édités** et verts ; `Alundra.Tests` 782/782 inchangée
(rien ne la consomme encore) ; diff du sous-module purement **additif** (le seul changement de
`ScreenEffectComponent` est un paramètre optionnel final et la source de taille) ; `git -C
CasaEngineMonogame status --short` montre toujours `Program.cs` modifié et absent du commit ; chemins
stagés nommément (jamais `-A`). Bump du pointeur parent (seul chemin `CasaEngineMonogame`).
**Mutations** : `>=` sur le timer → table de cadence ; accumulateur avancé par appel de `Submit` au lieu
de par tick → `ticks = 0`/`= 4` ; **ticks non consommés** (`PendingTicks` non remis à 0) → « un
`SetFrame(2)` puis deux `Advance()` » tombe (quatre ticks) ; `Clear()` remet `LayersVersion` à 0 → le
test des deux cycles tombe ; `+halfH` oublié → ancrage ; rectangle de ciseaux relu du device dans
`Submit` → le test headless lève ; `TryGetCameraViewSize` renvoyant la taille en pixels (division par
`Zoom` oubliée) → `(320, 236)` tombe ; couture renvoyant vrai avec caméra nulle → « caméra nulle »
tombe. Vérifiées exécutables.
**Retour arrière** : revert du commit sous-module + du bump.

### S1 — L'adaptateur DLL, non branché, et le harnais d'équivalence (D-E9b-8..11)

**Contenu** (DLL, `Alundra/Scripts`, `Alundra.Tests`) :
1. `AlundraBackdropStage` gagne `AttachService(ScrollingLayerService?)` (interne), `BuildDefinitions(
   BackdropDocument) → (layers, tint?, configuration)` (**pure**, règles complètes de D-E9b-8) et
   `PushFrame(ticks, camera)`. La **définition** de `ResolveGroundLayerBlend` passe sur le stage ;
   `BackdropRenderer.ResolveGroundLayerBlend` devient un **relais** vers elle (signature conservée) :
   les quatre pins T8 restent verts **inchangés** (un seul `switch (blendMode)` dans la DLL). Aucun site
   de production n'appelle `AttachService`, `BuildDefinitions` ni `PushFrame` ; `UpdateAndDrawBackdrop`
   est **textuellement inchangé**.
2. `Alundra.Tests/BackdropStageDefinitionTests.cs` — **6** tests : traduction exacte des compagnons
   **réels** 389, 159, 321 (ids de trames, facteurs, passes, blend, teinte, `DepthOrder`→`OrderInLayer`,
   couches `Disabled` absentes) ; filtrage d'une couche `Disabled` synthétique ; gardes — un document
   synthétique avec une couche `Tiles` à `Scrollar` nul et une à `TextureAssetId` vide → toutes deux
   absentes, sans exception ; id de trame `f ≥ 1` inanalysable → `FrameTextureAssetIds` de même longueur
   avec `Guid.Empty`, et `ResolveTextures(id => …)` sur le composant laisse exactement `[frame0]` (même
   compte que `BackdropRenderer.LoadLayerFrames` aujourd'hui pour la même entrée). Les tests sur
   données réelles **échouent** (message nommant l'export manquant) si `alundra-project/` est absent.
3. `Alundra.Tests/BackdropEquivalenceTests.cs` (D-E9b-10) — **3** tests (une carte chacun) : 2 000 ticks,
   trajectoire de `Target` clampée aux bornes de la carte à `Z = 0`, frames à 0, 1, 2 et 4 ticks ; ancien
   chemin vs nouveau (API publiques de D-E9b-1, mêmes instances `Texture2D`) ; assertions par frame :
   offset (`LastLayerOffsetForTests` vs `TryGetLayerState`), index de trame, `Assert.Same` textures,
   positions, clés, blend, couleurs, z = 0 ; ciseaux `(0,0,320,240)` vs rectangle passé.
**Acceptation** : `Alundra.Tests` **791 / 791** (782 + 6 + 3) ; l'équivalence tient sur les trois
cartes ; **aucun changement en jeu**, prouvé par (a) `git diff` : `UpdateAndDrawBackdrop` textuellement
identique, (b) `grep -rn "AttachService\|BuildDefinitions\|PushFrame" Alundra/Scripts` ne renvoie que
les définitions, (c) `BackdropAnimationReplayProductionTests` et les pins B1/B3/B5 de
`BackdropRendererTests` passent **sans avoir été édités**.
**Mutations** : décalage d'un tick dans la traduction (`AnimTimer − 1`) → équivalence tombe (159/321 ;
indétectable sur 389, `AnimNum 1`, dit honnêtement) ; `Ground` inversé → passes ; `DepthOrder` ignoré →
clés ; couche `Disabled` traduite → test de filtrage ; `Guid.Parse` levant sur un id inanalysable → le
test des gardes lève ; id inanalysable **retiré** du tableau → longueur et `[frame0]` tombent.
**Retour arrière** : revert.

### S2 — La bascule (D-E9b-2, 9, 10, 11, 13)

**Contenu** :
1. `AlundraWorldProxy.InitializeWithWorld` : `_backdropStage.AttachService(world.Game?.ScrollingLayerComponent?.Service)`
   avant `Load` ; `AlundraBackdropStage.Load` : `Service.Clear()`, `SetConfiguration/SetLayers/SetTint`
   depuis `BuildDefinitions` (avertissement unique si service nul avec `Game` non nul) ;
   `UpdateAndDrawBackdrop` remplacé par `PushFrame` (même site `:1520`, même position dans l'ordre de
   frame, hors porte de gel) ; `ApplyOriginalBackgroundClearColorOnce` inchangé.
2. Suppression : `BackdropRenderer.cs`, `BackdropOffsetMath.cs`, `LayerRuntime`, `LastLayerOffsetForTests`,
   `BackdropRendererTests.cs` (22), `BackdropOffsetMathTests.cs` (25), `BackdropAnimationReplayProductionTests.cs`
   (2), `BackdropEquivalenceTests.cs` (3). **Table retirés → remplaçants** :

   | Test retiré (`Alundra.Tests`) | Devenir |
   |---|---|
   | `BackdropDocument_DeserializesOverlayTintFields`, `…DefaultsToTintDisabled` | conservés, déplacés dans `BackdropDocumentTests.cs` (**+2**) |
   | `TintSortKey_IsStrictlyBelowAGround1LayerKey_RegardlessOfDepthOrder` | `BackdropStageDefinitionTests` : clé de teinte sous toute clé `Ground=1` (**+1**) |
   | `HasContent_IsTrueFromTintAlone_WithZeroLayers`, `Draw_WithTintAndZeroLayers_SubmitsOneAlphaBlendQuad…` | S0 `CasaEngine.Tests` « teinte seule → un quad » |
   | `ResolveGroundLayerBlend_*` ×3 (T8) | `BackdropStageBlendTests.cs` (**+3**), même table |
   | `Draw_Factor1Layer_StaysWorldGlued_WhenCameraMovesVertically` | S0 « delta vertical » |
   | `UpdateAndDrawBackdrop_ProductionSite_PinsAbsoluteParallaxOffset…` (287/239) | S0 « facteur 1/1 → (287, 239) » |
   | `UpdateAndDrawBackdrop_ProductionSite_AnchorsTheCanvas…` (927, −719) | S0 « un quad à (927, −719) » |
   | `AdvanceAnimation_*` ×3, `Draw_AfterAdvancingTicks_SubmitsTheExpectedFrameTexture` | S0 cadence + « trame courante après 7 et 28 ticks » |
   | `ResolveFrameAssetIds_*` ×2 | `BackdropStageDefinitionTests` (**+2**) |
   | `AdvanceAnimation_LayerResolvedFromNoFrameIds_NeverLeavesFrameZero` | S0 « une trame → 0 » + traduction `[TextureAssetId]` |
   | `LoadLayerFrames_*` ×4 | S0 repli par délégué (f0, f1, f3, dense) |
   | `BackdropOffsetMathTests` (25 cas) | S0 théories des fonctions pures |
   | `Update_AdvancesTheBackdropAnimationCounter_…AtTheProductionCallSite`, `…OnAWorldWithNoGame…` | `BackdropPushProductionTests.cs` (**+3**) : site réel 389 ; porte de gel `MenuOpen` ; monde sans `Game` |
   | *(exigés par les mutations)* | `BackdropStageLoadTests.cs` (**+2**) : avertissement « service nul avec `Game` non nul » ; second monde (`Clear` + `SetLayers`, `LayerCount` de la seconde carte seulement, `LayersVersion` strictement croissant) |
   | `BackdropEquivalenceTests` ×3 (S1) | supprimés : l'ancien chemin n'existe plus |

   Compte attendu, terme à terme : 791 − 22 − 25 − 2 − 3 = 739 ; 739 + 2 + 1 + 3 + 2 + 3 + 2 = **752**.
3. Pins de site de production (`BackdropPushProductionTests.cs`, montage réel 389 de
   `AlundraWorldProxyGlobalFreezeTests`, recette B3 : `_clearColorApplied = true`, frame d'amorçage
   `Update(0.02f)`, service **réel** injecté par `AttachService` avant le premier `Update` ; `Load` y a
   tourné avec un service nul puisque `InitializeWithWorld` précède l'injection — sans importance :
   `PushFrame` ne dépend ni des couches ni de `HasContent`) : (a) `Update(0f)` → `FramesPushed`
   incrémenté, `PendingTicks == 0` ; `Update(0.04f)` → `PendingTicks == 2`, `LastPushedScrollX/Y ==
   ToOriginalScrollSpace(Target)`, `CameraTarget == Target` ; (b) **porte de gel** :
   `GameState.PlayerControlFlags |= MenuOpen` avant `Update(0.04f)` → `PendingTicks == 2` quand même ;
   (c) monde sans `Game` (`BuildHeadlessWorld`), service injecté → `FramesPushed` incrémenté.
4. **Captures** (session principale, protocole D-E9b-13 avec calibration et script de mesure consigné) :
   **avant** (HEAD de S1) et **après** sur 389, 159 et 321 ; `AlundraGame.json` sauvegardé/restauré par le
   script ; critères structurels par carte ; 321 « après » : sol des lignes [640, 944) inchangé, nuages
   présents dans les lignes [0, 560) (fraction `R − max(G, B) > 60` ≥ 50 % de la plus petite fraction
   « avant »), index de trame et offsets conformes aux pins.
**Acceptation** : `Alundra.Tests` **752 / 752** ; `CasaEngine.Tests` inchangée ; captures conformes ;
journal du lanceur sans avertissement de service nul ni de texture ; **validation en jeu par
l'utilisateur** (389, 159, 321 — arrêt du mode AUTO ici).
**Mutations** : `PushFrame` placé dans le bloc `!gameplayBlocked` → le pin (b) tombe ; `scroll` non
converti (Target brut) → (a) tombe ; `AttachService` oublié dans `InitializeWithWorld` → le test
d'avertissement tombe (aucun avertissement émis) ; `SetLayers` sans `Clear` → « second monde » tombe.
**Retour arrière** : revert (le renderer revient avec ses tests) ; le sous-module reste à S0 (inerte).

### S3 — Documentation et clôture

`docs/formats/backdrops.md` (consommateur : DLL → service moteur, § « Différé » inchangé),
`docs/plan-conversion-totale.md` (E9 ✅, ligne de suivi), `docs/plan-e9-backdrops-residus.md` §6
(suites reprises), README : phase 9 dans la table (P4 hérité), journal §5 de ce plan, mémoire.

---

## 4. Arrêts

- un fichier du convertisseur modifié, ou un export lancé ;
- `CasaEngine.Launcher/Program.cs` modifié ou stagé ; `git add -A` dans le sous-module ;
- un échec `CasaEngine.Tests` hors de la liste nommée de la baseline, ou un flake attribué à la tranche
  sans re-preuve par restauration ;
- `AlundraLogicClock` touché ; un golden qui bouge ;
- la nécessité d'un composant d'**entité** ou d'un cas sérialiseur (contredit D-E9b-U2) ;
- une allocation ou une réflexion par frame dans `ScrollingLayerComponent.Update`/`Submit` ; un accès
  au `GraphicsDevice` dans `Submit` ;
- l'équivalence S1 qui ne tient pas sur une des trois cartes sans explication tick-exacte ;
- **service nul à `Load` en production** (`world.Game != null`) — avertissement dans le journal ;
- un site nul sur le chemin `proxy.Update` du montage 389 autre que `ApplyOriginalBackgroundClearColorOnce` ;
- la 389, la 159 ou la 321 hors des critères calibrés de D-E9b-13 après S2 ;
- la nécessité de porter `0xA4`, la secousse, le marcheur de teinte ou le mode cellulaire (contredit
  D-E9b-14 / D-E9-7).

---

## 5. Journal d'exécution

### S0 — Le mécanisme dans le moteur

Sous-module `CasaEngineMonogame`, commit `dcbb55ff` ; pointeur parent bumpé en `29a84e2`.
`ScrollingLayerService` (sans type GPU : définitions de couches, état par tick, arithmétique
d'offset pure, contrat de poussée `SetFrame`/`Advance` avec `PendingTicks`/`FramesPushed`/
`LastPushedScrollX/Y`/`CameraTarget`/`HasPendingFrame`, `LayersVersion` strictement croissant) et
`ScrollingLayerComponent` (résolution des textures par id d'asset avec le repli trame 0 / trame `f`
et un avertissement sur les deux branches, `Advance` puis `Submit` par frame, rectangle de ciseaux
reçu en paramètre, aucune soumission avant la première poussée reçue) livrés ensemble ;
`CasaEngineGame.ScrollingLayerComponent` et `ComponentUpdateOrder.ScrollingLayers` ajoutés ;
`ScreenEffectComponent` corrigé (taille de vue lue sur la caméra active via la couture pure
`TryGetCameraViewSize`, repli sur la taille écran, paramètre de ciseaux optionnel en fin de
signature). Nouvelle page `CasaEngineMonogame/docs/engine/scrolling-layers.md`, indexée dans le
`docs/README.md` de ce dépôt.

`CasaEngine.Tests` : 1524 verts, les 17 échecs préexistants inchangés nom pour nom par rapport à la
baseline re-mesurée avant la tranche.

Relecture de clôture : **REVISE** sur un seul blocage (le chemin de texture en échec était silencieux ;
la prescription minimale — avertissement journalisé, testé par assertion sur le texte — a été
appliquée sans nouvelle relecture, cap atteint). Verdict outcome-`verifier` : **CONFIRMED** après un
tour de correction.

### S1 — L'adaptateur DLL, non branché, et le harnais d'équivalence

Commit `3798b75`. `AlundraBackdropStage` gagne `AttachService`, la fonction pure `BuildDefinitions`
(traduction reprenant exactement les règles de `BackdropRenderer.Load`) et `PushFrame` ; la
définition de `ResolveGroundLayerBlend` déménage sur le stage, le renderer devenant un simple relais.
Rien n'est branché : le jeu en production est inchangé.

Un harnais d'équivalence a fait tourner l'ancien chemin et le nouveau côte à côte sur les compagnons
réels des maps 389, 159 et 321, sur 2000 ticks logiques, avec des frames de 0, 1, 2 et 4 ticks, en
comparant offsets, identité de texture soumise, positions de quad, clés de tri, blend, couleurs et
profondeur.

`Alundra.Tests` : 791/791. Verdict outcome-`verifier` : **CONFIRMED** au premier tour.

### Amendement du critère visuel

Commit `975248c`. Le critère visuel de D-E9b-13 a été calibré avant usage et s'est révélé **non
discriminant** : l'animation des fonds est en réalité déterministe, pas bruitée. Le critère a été
remplacé par une rafale de 12 clichés réduite en médiane par pixel, avec des seuils désormais dix à
quarante fois inférieurs aux tailles d'effet à attraper (détail de la mesure en §1.8). Le reste du
plan est inchangé.

### S2 — La bascule

Commit `e808568`. Le service est attaché à l'entrée dans le monde, le stage l'alimente une fois par
monde, et le site de production pousse désormais le contrat de frame au lieu de dessiner —
inchangé dans l'ordre de frame, toujours hors de la porte de gel. `BackdropRenderer` et
`BackdropOffsetMath` sont supprimés avec leurs tests ; chaque pin numérique survit, côté moteur ou
dans la DLL, dont trois pins qui font tourner le montage réel de la map 389 à travers le site de
production.

`Alundra.Tests` : 752/752, exactement l'arithmétique prédite par le plan. Verdict outcome-`verifier` :
**CONFIRMED** au premier tour.

### Comparaison visuelle avant/après : non produite

Toute capture de la fenêtre du jeu est revenue **noire**, de façon reproductible, avec trois méthodes
de capture indépendantes — y compris après retour au code d'avant la bascule. Diagnostic retenu : une
limitation d'environnement/affichage de la session, pas une régression du chantier (le journal du jeu
ne montre aucun avertissement de texture, de fond ou de défilement absent). L'utilisateur a validé les
trois maps (389, 159, 321) directement en jeu à la place de cette comparaison.

### Suites différées (aucune bloquante, toutes P3/P4)

- **S0** — les assertions d'identification de couche du test de journalisation sont des
  correspondances de sous-chaîne larges ; un commentaire surestime ce que `Logs.Close()` détache
  réellement.
- **S1** — la boucle d'équivalence n'a pas d'assertion explicite de non-vacuité (non vacue en
  pratique) ; le harnais n'exerce jamais le quad de teinte en surimpression (aucune map
  d'acceptation ne l'active) ; sa trajectoire réutilise les bornes de la map 389 pour les trois
  cartes.
- **S2** — la ligne de mutation du plan « `SetLayers` sans `Clear()` » **n'est pas discriminante** :
  le service remplace son tableau de couches plutôt que d'y ajouter, donc retirer `Clear()` laisse le
  pin vert ; `Clear()` reste nécessaire malgré tout car il remet à zéro l'état de poussée. Quelques
  mentions en prose des types retirés subsistent comme notes historiques.

Ces suites sont consignées pour qui les reprendra ; aucune n'entre dans le périmètre d'un chantier
dédié à ce stade.

**Validation en jeu par l'utilisateur (2026-09-04).** Un premier essai a donné « tout est bon sauf
pour la map 321 », qui a ouvert le chantier correctif E9.c (`docs/plan-e9c-defauts-321.md`) : deux
défauts **antérieurs à cette migration** — une inversion rouge/bleu dans le décodage des palettes des
fonds, et une égalité de profondeur qui laissait le fond recouvrir les tuiles restées dans le lot
statique. Après leurs correctifs, l'utilisateur a validé l'ensemble : « tout est ok ». **E9 est CLOS,
validé en jeu sur les trois cartes d'acceptation.**
