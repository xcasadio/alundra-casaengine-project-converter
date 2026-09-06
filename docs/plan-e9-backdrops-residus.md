# Plan — E9.a : les résidus des backdrops

Première moitié d'E9, selon la décision utilisateur du 2026-09-03 (« les résidus d'abord, la migration
ensuite ») : corriger, dans la DLL actuelle, ce que la clôture d'E10 a laissé comme résidus de rendu
des fonds défilants (`docs/plan-e10-fondu.md:349-351`). La migration vers un composant moteur de
couches défilantes (E9.b, `docs/plan-conversion-totale.md:506-511`) est un chantier séparé, planifié
après celui-ci, pour lequel les corrections ci-dessous servent de référence visuelle.

**Révision 2** — la révision 1 a été relue par quatre relecteurs indépendants (enveloppe, B1, B2, B3) ;
huit blocages levés, tracés en §1.5.

**Révision 3 — relecture de clôture (2026-09-04)** : enveloppe et B1 **READY** ; B2 et B3 **REVISE**,
un blocage chacun. Les deux prescriptions minimales sont appliquées ci-dessous (§0.4, §1.4, §1.5,
D-E9-8, B2, B3, §4) et **ne sont pas re-relues** : le cap de relecture est atteint, le plan est
présenté à l'utilisateur en l'état.

---

## 0. Cadre

### 0.1 Les trois résidus tels que la clôture d'E10 les a nommés

> la parallaxe lit la cible caméra au lieu du défilement **clampé** de l'original (écart constant aux
> bords de carte — E9) ; l'animation d'offset V (`AnimNum`) et le `WaveLut` ne sont pas rejoués (E9 —
> la 159 ne luit que sur son quart supérieur pour ça)

### 0.2 Correction de périmètre, à trancher par l'utilisateur (**D-E9-U1**)

La reconnaissance établit (§1.3) que **le `WaveLut` n'a aucun lecteur dans le chemin des couches de
tuiles**. Ses seuls consommateurs sont les cellules de type `WaveX` du mode **cellulaire** (mode 2),
un système entier différé par conception depuis la phase 9 du convertisseur
(`docs/formats/backdrops.md`, section « Différé »). Ni la 389 ni la 159 n'ont de couche cellulaire.
Le « quart lumineux » de la 159 est dû à `AnimNum` **seul**. La clôture d'E10 a attribué au
`WaveLut` une part d'un symptôme qu'il ne produit pas. Confirmé en relecture indépendante par
recherche exhaustive : trois sites de lecture, tous dans `case CellType.WaveX`.

**Recommandation** : sortir le `WaveLut` de ce chantier et l'inscrire au chantier du mode cellulaire
(84 cartes du corpus en ont une couche). Ce plan est écrit sous cette hypothèse ; si l'utilisateur
préfère l'y garder, il faut porter le mode cellulaire entier, ce qui est un autre chantier.

### 0.3 Référence de suites AVANT chantier (mesurée le 2026-09-03)

| Suite | Résultat |
|---|---|
| `Alundra.Tests` | 764 / 764 vertes |
| Convertisseur (`alundra-casaengine-project-converter.Tests`) | 152 / 152 vertes |
| `CasaEngine.Tests` | 18 échecs préexistants connus (inchangés depuis le chantier transitions) |

### 0.4 Ce que ce chantier touche, et ce qu'il ne touche pas

- **Touche** : la DLL (rendu des backdrops) ET **le convertisseur** (phase 9, export des trames
  d'animation V). Un changement du convertisseur impose un **export complet** puis la preuve du
  **double export** (`docs/plan-nettoyage-convertisseur.md`, D-N-7 : diff de manifeste ⊆
  `{report.json}`), avec l'outil `manifest.py` du scratchpad.
- **`alundra-project/` est hors git** (`.gitignore:63`) et **l'export est in-place** : l'arbre de
  sortie n'a aucune sauvegarde versionnée. Toute comparaison « avant/après » exige donc de **capturer
  la référence avant de toucher au convertisseur** (D-E9-8, B2 étape 0).
- **Provenance de la référence (mesurée le 2026-09-04)** : l'arbre `alundra-project/` actuel est la
  sortie du **second export** de la preuve de déterminisme du nettoyage (`report.json` du 2026-09-02
  10:54 ; deux grappes de dates, 08 h et 10 h, le même jour). Depuis, seuls `Alundra.dll`/`Alundra.pdb`
  ont bougé (dépôt de build, §1.4). Sa dérive par rapport au manifeste du 1er septembre est l'adoption
  des ids déterministes (~19 500 JSON ; sons, PNG, `.tmj`, chaînes inchangés), pas une réécriture
  parasite. Aucun commit n'a touché le convertisseur depuis cet export.
- **Ne touche pas** : le moteur (sous-module), l'analyseur, le mode cellulaire, le tremblement
  d'écran scripté (`g_scrollingParameters.OffsetX/Y`, opcode non porté), `Program.cs` du lanceur,
  aucune suppression manuelle sous `alundra-project/`.

---

## 1. Faits établis, vérifiés en session principale

### 1.1 Résidu 1 — la parallaxe lit la mauvaise valeur de caméra

- **§1.1.a** L'original alimente ses couches de fond avec **`g_cameraScrollingX/Y`**
  (`GraphicManager.cs:54`, appel de `RenderAllTileLayers`), et non avec le point suivi. C'est la
  position du **coin haut-gauche** de la fenêtre 320×240, entière, lissée par `>> 4` vers
  `(lookAt.X − 0xa0, (lookAt.Y − lookAt.Z) − 0x88)` et **clampée** à `[0, 0x39f]` × `[0, 0x2cf]`
  (`GraphicManager.cs:75-122`). Le pan de débogage (`g_cameraDebugOffsetX/Y`) et le tremblement
  scripté (`g_scrollingParameters.OffsetX/Y`) s'y ajoutent avant le clamp.
- **§1.1.b** La parallaxe d'une couche est **`ParallaxOffsetX = cameraX * FactorXNum / FactorXDenom`
  en division entière**, puis `screenPos = OffsetX + ParallaxOffsetX` enroulé dans `[0, 640)`
  (`[0, 480)` en Y) par des boucles `while` qui mutent l'accumulateur d'auto-défilement
  (`GraphicManager.cs:868-899`). Modulo le canevas, c'est équivalent à un enroulement de la somme :
  le calcul actuel de `BackdropOffsetMath.ComputeLayerOffset` est donc juste **sauf sur son entrée**.
  Après clamp, `scroll ≥ 0` sur les deux axes : la troncature C# et celle de l'original coïncident.
- **§1.1.c** La DLL alimente `BackdropRenderer.Draw` avec **`resolvedCamera.Target`**
  (`AlundraBackdropStage.cs:129`), puis `Draw` utilise `cameraPosition.X` et **`−cameraPosition.Y`**
  (`BackdropRenderer.cs:291-303`, négation locale avec son commentaire) comme « défilement », en
  **flottant**.
- **§1.1.d** `Target` est déjà l'état lissé et clampé de l'original, mais exprimé en **espace de
  rendu centré** (E5, `AlundraCameraMath.cs:119-127`) : les relations gelées par E5 sont
  **`renderX = scrollX + 160`** et **`renderY = −scrollY − 120`**, confirmées par les bornes de la
  389 : scroll `[0, 0x39f]` ↔ rendu `[160, 1087]`, scroll `[0, 0x2cf]` ↔ rendu `[−120, −839]`.
  `Target` est entier par construction (E5.c).
- **§1.1.e** **Conséquence exacte** : la DLL nourrit la parallaxe avec `scrollX + 160` et
  `scrollY + 120` au lieu de `scrollX` et `scrollY`. Pour la couche 1/1 de la 389, enroulée sur
  640×480, c'est un **déphasage constant de (160, 120)** du fond par rapport à l'original — l'« écart
  constant » de la clôture d'E10. Le pan de débogage est bien inclus dans `Target`
  (`AlundraCameraDirector.cs:79,261,325-328`), comme dans l'original.
- **§1.1.f** Le seul test de parallaxe existant (`BackdropRendererTests.Draw_Factor1Layer_StaysWorldGlued_…`)
  compare deux positions caméra **entre elles** ; il ne pinne pas la valeur absolue, donc il ne peut
  pas détecter ce déphasage. Aucun test ne pilote `AlundraBackdropStage.UpdateAndDrawBackdrop`.

### 1.2 Résidu 2 — l'animation d'offset V (`AnimNum`) n'est pas rejouée

- **§1.2.a** L'original, par couche de tuiles et par frame 50 Hz (`GraphicManager.cs:871-879`) :
  `animNum = Infos.AnimNum <= 0 ? 1 : Infos.AnimNum` ;
  `if (++AnimFrameTimer > LayerInfos.AnimTimer) { if (++AnimFrameCounter >= animNum) AnimFrameCounter = 0; AnimFrameTimer = 0; }`.
  **Les compteurs avancent AVANT le dessin de la même frame** (`:871-879` précède `:943`). La trame
  avance donc **toutes les `AnimTimer + 1` frames**, et le **premier palier ne dure que `AnimTimer`
  frames** (le premier tick consomme déjà `AnimFrameTimer = 1`). Avec `AnimNum ≤ 1`, le compteur
  retombe toujours à 0 : pas d'animation.
- **§1.2.b** Puis `vAnim = (AnimFrameCounter << 8) / animNum` (`:943`) et chaque tuile échantillonne
  la feuille 256×256 en **`V = ((tileVal & 0xF0) + vAnim) & 0xFF`** (`:979`). Avec `AnimNum = 4`,
  `vAnim ∈ {0, 64, 128, 192}` : **quatre bandes d'un quart de feuille**. C'est le « quart » de la 159.
- **§1.2.c** Le convertisseur cuit **une seule** texture 640×480 par couche, à `vAnim = 0`
  (`BackdropImageBuilder.cs:12` et `:77` : `sheetV = tileVal & 0xF0`, sans terme d'animation). La DLL
  charge `AnimNum` dans `BackdropDocument` mais ne le lit nulle part.
- **§1.2.d** **Corpus mesuré** (330 compagnons, recompté en relecture) : 17 cartes ont `AnimNum > 1`.
  Parmi elles, **7 sont en mode tuiles** — les seules concernées par ce résidu — toutes avec
  `AnimNum = 4`, chacune avec **une seule** couche de tuiles exportée : **159 et 160**
  (`AnimTimer = 6`, la grotte des fées sous l'eau), **321, 322, 323, 327, 480** (`AnimTimer = 4`,
  arènes de boss). Les 10 autres sont cellulaires. Toutes les autres cartes de tuiles ont
  `AnimNum ≤ 1`, dont la 389 (`AnimNum = 1`, `AnimTimer = 1`).
- **§1.2.e** Nommage et identité déterministes du convertisseur : la texture d'une couche s'appelle
  `{FileBaseName}-layer{N}.png` (`MapCatalogReader.cs:52`) et son id est `Ids.For("texture-raw:" +
  cheminRelatif)` (`TextureAssetWriter.cs:51-54`), **dérivé du chemin**. Renommer les 132 textures
  existantes changerait 132 ids : les nouvelles trames doivent porter un nom **nouveau** et laisser
  intact le nom de la trame 0.
- **§1.2.f** `BackdropImageBuilder.DrawTile` renvoie vrai dès qu'un pixel non transparent est écrit
  (`:100-143`) ; `Build` renvoie `null` quand aucune tuile n'a écrit de pixel. Une trame `vAnim ≠ 0`
  peut donc théoriquement être entièrement transparente alors que la trame 0 ne l'est pas.
- **§1.2.g** **Piège de sérialisation, vérifié sur l'export** : `BackdropWriter.SerializerOptions` ne
  pose que `WriteIndented = true` (`:154`), donc **une propriété `null` est ÉCRITE** — le compagnon de
  la 159 contient bien `"TextureAssetId": null` et `"Cellular": null` sur sa couche 1 (`:297-299`).
  Une nouvelle propriété laissée `null` sur les couches non animées **modifierait les 330
  compagnons** au lieu des 7 concernés.

### 1.3 Résidu 3 — `WaveLut` : hors du chemin des tuiles

- **§1.3.a** `WaveLut` (256 entiers, `ScrollScreen.cs:38,174-179`) n'est lu **que** dans
  `GraphicManager.cs:1192,1202,1209`, dans la branche `case CellType.WaveX` du rendu **cellulaire**
  (mode 2), avec `cellular.AWaveAmp`/`BWaveWeight`. **Aucune occurrence** dans le chemin des tuiles
  (`GraphicManager.cs:813-1000`), dont la seule entrée d'animation est `vAnim`.
- **§1.3.b** Le mode cellulaire est différé en bloc depuis la phase 9 : aucune texture n'est produite
  pour ces couches, seuls les paramètres bruts sont exportés (`docs/formats/backdrops.md`).
- **§1.3.c** La 389 et la 159 ont leur couche 0 en mode **tuiles** et leur couche 1 **désactivée**
  (compagnons exportés). Le `WaveLut` exporté au niveau carte y est inerte, comme sur les 102 cartes
  de tuiles seules qui en portent un.

### 1.4 Le pipeline actuel de la DLL et ses coutures

- `AlundraWorldProxy.Update` calcule `ticksThisFrame` en `:1400` — il peut valoir **0** (frame rendue
  sans tick logique) ou **> 1** (rattrapage) — et appelle
  `_backdropStage.UpdateAndDrawBackdrop(elapsedTime, _world, _cameraDirector.ResolvedCamera)` en
  `:1520` : **le compte de ticks logiques est en portée** au site d'appel.
- `AlundraBackdropStage.UpdateAndDrawBackdrop(float, World?, Camera2dComponent?)` (`:114-131`) a
  **deux retours anticipés** (`!HasContent || world?.Game == null`, puis `SpriteRendererComponent`
  introuvable) avant `Draw(spriteRenderer, resolvedCamera.Target, largeur, hauteur)`.
  `_backdropRenderer` est un champ `private readonly` initialisé inline ; `BackdropRenderer` est
  `internal sealed` : **aucune couture d'injection**, seule la réflexion permet de substituer ou
  d'observer (les tests existants le font déjà pour `_layers`, `BackdropRendererTests.cs:218-291`).
- `BackdropRenderer.Tick` accumule `_elapsedTicks += elapsedTime × 50` (`:226-229`) — une **seconde
  horloge**, flottante, distincte de `AlundraLogicClock`. L'auto-défilement en dépend. Ce chantier
  **ne la change pas** (hors résidus) mais la consigne pour E9.b (D-E9-6).
- `BackdropRenderer.Load` (`:100-190`) charge **une** `Texture2D` par couche via
  `Load<CasaEngineTexture>` + `Load(assetContentManager)`, **saute la couche entière** sur tout échec
  (`continue`, `:135-173`), et construit `LayerRuntime(scrollar, texture, sortKey, tint, blendMode)`
  (`:63`). Cette méthode exige un `GraphicsDevice` vivant : **aucun test headless ne l'atteint**
  (commentaire en place `:146-156`).
- `BackdropDocument`/`BackdropLayerData` (DLL) reflètent champ pour champ les documents du
  convertisseur ; un champ JSON absent est laissé à sa valeur par défaut. `AnimNum` est au niveau
  **document**, `AnimTimer` au niveau **couche** (`BackdropDocument.cs:40,65`).
- Le montage réel de la 389 (`AlundraWorldProxyGlobalFreezeTests.cs:236-241`) franchit
  `InitializeWithWorld` avec un `CasaEngineGame` **non initialisé** (seul `_components` est posé) :
  `AssetContentManager` y est null, donc `Load` ne construit aucune couche, `HasContent` est faux,
  et `GetGameComponent<SpriteRendererComponent>()` renvoie null.
- Sur ce même montage, `proxy.Update` **n'atteint pas** `UpdateAndDrawBackdrop` : l'appel frère
  `_backdropStage.ApplyOriginalBackgroundClearColorOnce(_world)` (`AlundraWorldProxy.cs:1519`)
  déréférence `world.Game.GameManager.ViewManager.Views` (`AlundraBackdropStage.cs:90`) derrière un
  garde `_clearColorApplied || world?.Game == null` (`:85`) — or `world.Game` **est** posé sur le
  montage (`:241`) et `GameManager`, get-only affecté au constructeur (`CasaEngineGame.cs:31,122`), y
  est null → `NullReferenceException`. Aucun test existant ne pilote `proxy.Update` sur ce montage
  (les appels d'`AlundraWorldProxyGlobalFreezeTests.cs:111,120` tournent sur `BuildHeadlessWorld()`,
  sans `Game`). C'est le **seul** déréférencement non gardé de `GameManager` dans toute la DLL
  (balayage `.GameManager.` hors `?.` : une occurrence, `AlundraBackdropStage.cs:90`).
- Compte de ticks par frame : `LogicTicksThisFrame` applique un **plancher collant de première frame**
  `Math.Max(ticks, 1)` tant que `_firstFrameStillOpen` (`AlundraWorldProxy.cs:469-473`), levé par
  `CloseFrame` (`:1582`). `AlundraScriptedMotion.FixedTickSeconds = 1f/50f` ; `0.04f` vaut exactement
  `2 × 0.02f` en binaire, donc après une frame d'amorçage `Update(0.02f)` (accumulateur remis à zéro
  exactement), `Update(0f)` donne 0 tick et `Update(0.04f)` exactement 2 (`AlundraLogicClock.cs:62-69`).
- `alundra-project/` contient trois fichiers qui **ne sont pas des sorties du convertisseur** :
  `Alundra.dll` et `Alundra.pdb`, redéposés à la racine par la cible `CopyGameplayDllToProject`
  (`Alundra/Alundra.csproj:20-25`, `AfterTargets="Build"`, `SkipUnchangedFiles`) à chaque compilation
  d'`Alundra` — donc par B1 et par les tests de B2 — et `.casaeditor/viewport.editor.json`, écrit par
  l'éditeur seul (`CasaEngine.Editor/ContentBrowser/ContentBrowserConfig.cs`, `GameEditor.cs`).

### 1.5 Corrections tracées (révision 2)

Huit blocages de la relecture indépendante, tous levés : B1 — l'acceptation contredisait D-E9-1 sur
le signe de Y (`scroll = Target − (160, −120)` donnait −719 au lieu de 719) et ne disait pas ce que
devient la négation locale de `Draw` (une double négation serait passée au vert) ; enveloppe — B1 et
B3 réécrivent les mêmes fichiers mais le tableau de prérequis les laissait avancer en parallèle ;
B3 — la suite de trames sur 40 ticks était décalée d'un tick (les compteurs avancent AVANT le dessin,
§1.2.a), la source du nombre de trames n'était pas fixée (un compagnon ancien à `AnimNum = 4` sans ids
aurait indexé hors bornes), le repli du chargeur n'était détectable par aucun test (`Load` exige un
`GraphicsDevice`), et le pin du site de production reposait sur un « renderer factice » impossible
(`sealed`, champ `private readonly`) et sur un montage où `UpdateAndDrawBackdrop` sort avant `Draw` ;
B2 — le « baseline » du diff n'était ni défini ni capturé alors que l'arbre de sortie est hors git et
exporté in-place, la sérialisation écrit les `null` (§1.2.g), et aucun retour arrière n'était écrit.
Référence `:975` corrigée en `:979` (§1.2.b).

**Révision 3 (clôture, non re-relue)** — B2 : le manifeste de D-E9-8 couvrait l'arbre entier, dont
`Alundra.dll`/`.pdb` que B1 (en parallèle) et la compilation des tests de B2 redéposent (§1.4) — le
diff aurait déclenché un faux arrêt §4 ou une dérogation à la main ; le périmètre du manifeste est
désormais « sorties du convertisseur seulement », et le tableau de parallélisme dit que B1 et B2
partagent l'arbre par ce dépôt. B3 : le pin du site de production n'était pas exécutable,
`proxy.Update` levant une `NullReferenceException` à `:1519` avant `UpdateAndDrawBackdrop` (§1.4) ;
la recette du pin reçoit les deux accommodations du montage (`_clearColorApplied` posé par
réflexion ; frame d'amorçage à cause du plancher de première frame), D-E9-5 inchangée. Le baseline a
été **recapturé au périmètre corrigé** le 2026-09-04 (22 968 entrées ; la capture pleine, 22 971, est
conservée en `e9-baseline-manifest.full.sha256`).

---

## 2. Décisions de conception

- **D-E9-1 — La parallaxe reçoit exactement `g_cameraScrollingX/Y`, et la conversion de signe vit à un
  seul endroit.** `AlundraCameraMath.ToOriginalScrollSpace(Vector3 target) → (int X, int Y)` porte les
  deux relations d'E5 : **`X = (int)target.X − 160`**, **`Y = −(int)target.Y − 120`**. C'est **la
  seule** expression du défilement dans tout le chantier. `AlundraBackdropStage` l'applique avant
  `Draw` ; `Draw` reçoit `(int scrollX, int scrollY)` **et les passe tels quels** à
  `ComputeLayerOffset` — la négation locale `−cameraPosition.Y` de `BackdropRenderer.cs:301-302` et
  son commentaire **disparaissent**. `ComputeParallaxOffset` calcule en **division entière tronquée**
  (`cameraX * FactorXNum / FactorXDenom`, entrées entières). `Draw` garde par ailleurs la position
  caméra de rendu, encore nécessaire pour placer les quads en espace monde (`:309-311`).
- **D-E9-2 — Le convertisseur exporte une texture par trame d'animation V, sans renommer l'existant.**
  Pour une couche de tuiles dont la carte a `AnimNum > 1` : trame 0 = fichier et id **inchangés**
  (`-layer{N}.png`) ; trames `f ≥ 1` = **nouveaux** fichiers `-layer{N}-frame{f}.png`, cuits avec
  `vAnim = (f << 8) / AnimNum` appliqué à chaque tuile (`V = ((tileVal & 0xF0) + vAnim) & 0xFF`,
  §1.2.b). Une carte à `AnimNum ≤ 1` produit **exactement ce qu'elle produit aujourd'hui**. Les 132
  textures et ids existants restent byte-identiques (ids dérivés du chemin, §1.2.e).
- **D-E9-3 — Le compagnon gagne `FrameTextureAssetIds`, omis quand il est nul, et garde
  `TextureAssetId`.** Sur une couche de tuiles, `FrameTextureAssetIds` est un tableau de `AnimNum` ids
  (le premier égal à `TextureAssetId`) quand `AnimNum > 1`, **absent** sinon — ce qui, avec la
  sérialisation en place (§1.2.g), exige **`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`**
  sur la propriété. Sans cet attribut, 330 compagnons bougeraient. `TextureAssetId` reste la trame 0 :
  un compagnon ancien, ou une carte non animée, se charge exactement comme avant.
- **D-E9-4 — Une trame est toujours émise dès que la trame 0 l'est.** Si `Build` renvoie `null` pour
  une trame `f ≥ 1` (§1.2.f), le convertisseur émet tout de même une texture **entièrement
  transparente** de 640×480 (un `Bitmap` vide de la même taille, sauvé et enregistré par le même
  `EnsureTexture`), pour que le tableau reste dense et fidèle : l'original dessine bien cette trame-là,
  vide. Compteur `Backdrop.FramesExported` ajouté (attendu sur un run complet : 132 + 7 × 3 =
  **153**, recompté en relecture), avec l'invariant `CheckInvariant` correspondant en mode run complet,
  sur le modèle de `WorldWriter.cs:630-637`.
- **D-E9-5 — La DLL rejoue l'animation par tick logique, avec la cadence et l'ordre exacts de
  l'original.** `LayerRuntime` porte `Texture2D[] Frames`, un `AnimTimer` et deux compteurs.
  **Le `animNum` de la boucle est `Frames.Length`**, jamais `document.AnimNum` — il vaut donc au
  moins 1 et le compteur est toujours indexable. `UpdateAndDrawBackdrop` reçoit `ticksThisFrame` et,
  **par tick, avance d'abord les compteurs** (§1.2.a, `> AnimTimer` puis `>= Frames.Length`, ordre
  timer-puis-compteur) **puis** `Draw` dessine `Frames[AnimFrameCounter]` — avance-puis-dessin dans le
  même tick, comme `:871-879` avant `:943`. `ticksThisFrame = 0` n'avance rien ; `= 2` avance deux
  fois. Une couche à une seule trame est un cas particulier de la même boucle (compteur toujours 0).
  **L'avance des compteurs est la première instruction d'`UpdateAndDrawBackdrop`, avant ses deux
  retours anticipés** : elle ne dépend ni du `Game` ni du `SpriteRendererComponent`, et c'est ce qui
  rend le site de production observable dans le montage réel de la 389 (§1.4).
- **D-E9-6 — L'auto-défilement garde son horloge flottante ; le smell est consigné pour E9.b.**
  Unifier `_elapsedTicks` sur `AlundraLogicClock` serait un changement de comportement hors des
  résidus. E9.b, qui porte le rendu dans un composant moteur, possédera **une seule** horloge ; d'ici
  là les deux horloges coexistent, et c'est écrit. Effet observable accepté : l'animation V et
  l'auto-défilement peuvent dériver l'un par rapport à l'autre de moins d'un tick par frame de
  rattrapage — invisible sur les sept cartes concernées, dont aucune n'auto-défile (leur `Scrollar`
  est nul sur les deux axes).
- **D-E9-7 — `WaveLut` sort du chantier** (D-E9-U1, sous réserve de l'utilisateur). Le document de
  format reste inchangé sur ce point ; la note « Différé » gagne une phrase qui renvoie explicitement
  le `WaveLut` au chantier cellulaire.
- **D-E9-8 — Preuve du convertisseur = baseline capturé AVANT, puis export complet, double export,
  goldens — sur le périmètre des seules sorties du convertisseur.** **Périmètre du manifeste** : tout
  `alundra-project/` **sauf** `Alundra.dll`, `Alundra.pdb` (dépôt de build, §1.4) et `.casaeditor/`
  (état de l'éditeur) ; ces trois chemins sont retirés de la capture et **de chaque diff**
  (`grep -vE '(Alundra\.(dll|pdb)|\.casaeditor/.*)$'`), jamais tolérés à la main. **Étape 0, avant
  toute modification du convertisseur** : `manifest.py alundra-project <scratchpad>/e9-baseline-manifest.sha256`
  puis filtrage au périmètre — ce fichier, hors de l'arbre de sortie, **est « le baseline »** (capturé
  le 2026-09-04, 22 968 entrées, §1.5). Après B2 : un export complet in-place depuis `data-extracted` ;
  son manifeste (même périmètre) doit différer du baseline **exactement** par les 21 nouvelles trames
  PNG + leurs 21 `.texture` (ajouts), les 7 compagnons concernés, `AssetInfos.json` et `report.json`
  (modifiés), et **aucun des 132 PNG/`.texture` existants avec un hash modifié**, ni aucun des 323
  autres compagnons. Une compilation d'`Alundra` intercalée (B1, tests de B2) ne peut ainsi produire
  aucun diff hors de l'ensemble attendu. Puis un second export in-place : diff des deux manifestes
  **⊆ { report.json }** (D-N-7), sans dérogation. Puis les six goldens d'`Alundra.Tests`
  byte-identiques. **Retour arrière** : revert du commit convertisseur + un export complet de
  restauration, dont le manifeste (même périmètre) doit redevenir identique au baseline hors
  `report.json`.
- **D-E9-9 — Règle d'échec partiel du chargeur.** Si une trame `f ≥ 1` échoue à charger, **la couche
  retombe sur la seule trame 0** (`Frames = [frame0]`) avec un avertissement journalisé — mode dégradé
  visible, cohérent avec la convention du dépôt (« une couche = un échec = `continue` » reste vrai si
  c'est la trame 0 qui échoue). Un `BackdropLayerData` sans `FrameTextureAssetIds` se résout à
  `[TextureAssetId]` par une fonction **pure et statique** `ResolveFrameAssetIds(BackdropLayerData)`,
  appelée par `Load`, seule testable sans `GraphicsDevice`.

---

## 3. Découpage en tranches

| Tranche | Prérequis | Surface |
|---|---|---|
| B1 — parallaxe sur le défilement clampé | aucun | DLL : `BackdropRenderer`, `AlundraBackdropStage`, `BackdropOffsetMath`, `AlundraCameraMath`, `AlundraWorldProxy.Update` |
| B2 — export des trames d'animation V | aucun | **convertisseur seul** + export complet |
| B3 — rejeu de l'animation dans la DLL | **B1 et B2** | DLL : `BackdropRenderer`, `AlundraBackdropStage`, `BackdropDocument`, `AlundraWorldProxy.Update` |
| B4 — validation en jeu | B1, B2, B3 | — |

**Seules B1 et B2 avancent en parallèle** (fichiers sources disjoints — mais **ils partagent l'arbre
`alundra-project/`** : chaque compilation d'`Alundra` par B1 y redépose `Alundra.dll`/`.pdb`, §1.4, ce
qui est précisément pourquoi le diff de B2 est borné au périmètre de D-E9-8). **B3 attend B1** — les deux
réécrivent `BackdropRenderer.Draw`, `AlundraBackdropStage.UpdateAndDrawBackdrop` et l'appel
`AlundraWorldProxy.Update:1520` ; un seul rédacteur à la fois sur ces fichiers — **et B2** (les trames
doivent exister). B4 attend tout.

### B1 — Parallaxe sur le défilement clampé (D-E9-1)

**Contenu** :
1. `AlundraCameraMath.ToOriginalScrollSpace(Vector3 target) → (int X, int Y)`, pure.
2. `AlundraBackdropStage.UpdateAndDrawBackdrop` l'applique et appelle
   `Draw(spriteRenderer, scroll.X, scroll.Y, cameraPositionRendu, largeur, hauteur)`.
3. `BackdropRenderer.Draw` : signature `(int scrollX, int scrollY, Vector3 renderCamera, …)` ; les
   deux `ComputeLayerOffset` reçoivent `scrollX` et **`scrollY` tel quel** ; la négation
   `−cameraPosition.Y` et son commentaire (`:294-302`) sont **supprimés** ; `renderCamera` ne sert
   plus qu'au placement des quads (`:309-311`) et au quad de teinte (`:270`).
4. `BackdropOffsetMath.ComputeParallaxOffset(int scroll, int num, int denom)` en division entière.

**Acceptation** :
- pure : `ToOriginalScrollSpace((160, −120, 0)) == (0, 0)` et
  `ToOriginalScrollSpace((1087, −839, 0)) == (0x39f, 0x2cf)` — les bornes gelées par E5 sur la 389 ;
- site de production (`UpdateAndDrawBackdrop`, montage `BackdropRendererTests` étendu jusqu'au stage
  par réflexion) : couche 1/1 de la 389, auto-défilement nul, **`Target = (1087, −839, 0)`** →
  alignement des quads **`offsetX = 927 mod 640 = 287`** et **`offsetY = 719 mod 480 = 239`** —
  valeur **absolue** pinnée, calculée par la formule de D-E9-1 et nulle autre ;
- division entière : facteur 1/3 et `scrollX = 5` donnent `1`, pas `1,666`.
**Mutations** : nourrir `Target` brut → `offsetX = 1087 mod 640 = 447`, l'alignement tombe ;
**négation résiduelle dans `Draw`** (`−scrollY`) → le test de site de production observe
`offsetY = (−719 mod 480) = 241` au lieu de `239` et tombe ; division flottante → le cas 1/3 tombe ;
signe de Y inversé dans `ToOriginalScrollSpace` → le test pur aux bornes tombe.
**En jeu** : la 389 ne doit montrer **aucune régression** visible (le déphasage corrigé est de l'ordre
d'un demi-écran sur un motif de nuages qui se répète ; l'œil ne le tranchera pas — c'est le test
numérique qui fait foi, dit honnêtement).

### B2 — Export des trames d'animation V (D-E9-2, D-E9-3, D-E9-4, D-E9-8) *(convertisseur)*

**Étape 0 — obligatoire, avant toute modification** : capturer le baseline
`e9-baseline-manifest.sha256` avec `manifest.py`, **filtré au périmètre de D-E9-8** (hors
`Alundra.dll`/`.pdb` et `.casaeditor/`). **Fait le 2026-09-04** (22 968 entrées, §1.5) ; l'exécutant
de B2 n'a pas à le recapturer, mais tout diff qu'il produit s'exprime sur ce même périmètre. Un
exécutant qui touche au convertisseur sans ce fichier a violé un arrêt.

**Contenu** : `BackdropImageBuilder.Build` gagne un paramètre `vAnim` (défaut 0) appliqué dans
`DrawTile` comme §1.2.b, et une surcharge produisant une texture transparente 640×480 (D-E9-4) ;
`MapLocation.BackdropLayerFrameTextureFileName(layerId, frame)` ; `BackdropWriter.ConvertMap` boucle
sur les trames quand `AnimNum > 1`, remplit `FrameTextureAssetIds`, incrémente
`Backdrop.FramesExported` et vérifie l'invariant en run complet ; `BackdropLayerDocument` gagne la
propriété **avec `[JsonIgnore(WhenWritingNull)]`** ; `docs/formats/backdrops.md` gagne la ligne de
schéma, le compteur, et retire « Différé » l'animation de tuile.
**Acceptation** :
- tests convertisseur : fixture à `AnimNum = 4` → quatre fichiers, la trame 0 sous son **ancien** nom,
  `FrameTextureAssetIds` de longueur 4 dont `[0] == TextureAssetId`, compteur `FramesExported = 4` ;
  fixture 389-like (`AnimNum = 1`) → **strictement les mêmes assertions qu'aujourd'hui**, aucun
  fichier nouveau, **et la chaîne JSON du compagnon ne contient pas `FrameTextureAssetIds`** ;
- pixel : feuille synthétique où la bande V `[64, 128)` est d'une couleur connue → la trame 1 d'une
  couche `AnimNum = 4` est cette couleur là où la trame 0 montre la bande `[0, 64)` ;
- trame vide : une trame `f ≥ 1` dont `Build` renvoie `null` produit tout de même un fichier, et le
  tableau reste de longueur `AnimNum` ;
- export complet réel : `Backdrop.LayersExported = 132` inchangé, `Backdrop.FramesExported = 153`,
  la 159 possède `-layer0.png` + `-layer0-frame1..3.png`, la 389 n'a rien de nouveau ;
- **diff contre le baseline** (périmètre D-E9-8, les trois chemins hors sorties filtrés des deux
  côtés) : exactement les ajouts/modifications de D-E9-8, **aucun hash existant modifié** ;
- **double export** (même périmètre) : diff des deux manifestes ⊆ `{report.json}` ;
- goldens `Alundra.Tests` byte-identiques ; suites 152 + nouveaux, 764.
**Mutations** : `vAnim` ignoré dans `DrawTile` → le test pixel tombe ; renommer la trame 0 → le test
389-like tombe sur le nom de fichier ; omettre la trame vide → la longueur du tableau tombe ;
retirer `[JsonIgnore(WhenWritingNull)]` → le test 389-like tombe sur la chaîne JSON, et le diff
contre le baseline montre 330 compagnons.
**Retour arrière** : revert du commit + export complet de restauration, manifeste (périmètre D-E9-8)
identique au baseline hors `report.json`.

### B3 — Rejeu de l'animation dans la DLL (D-E9-5, D-E9-9)

**Contenu** : `BackdropLayerData.FrameTextureAssetIds` ; `BackdropRenderer.ResolveFrameAssetIds`
(pure, statique) ; `Load` charge toutes les trames résolues, avec la règle d'échec partiel de D-E9-9 ;
`LayerRuntime` porte `Frames`, `AnimTimer` et les deux compteurs ; `BackdropRenderer.AdvanceAnimation(int ticks)`
applique §1.2.a par tick sur chaque couche ; `UpdateAndDrawBackdrop(float elapsedTime, int ticksThisFrame, …)`
appelle `AdvanceAnimation` **en première instruction**, puis `Draw` dessine `Frames[AnimFrameCounter]` ;
`AlundraWorldProxy.Update:1520` passe `ticksThisFrame`.
**Acceptation** :
- cadence pure (`AdvanceAnimation` + lecture du compteur, sans texture) : avec `Frames.Length = 4`,
  `AnimTimer = 6`, la suite des trames **dessinées** sur 40 ticks, l'avance précédant le dessin, est
  **`0×6, 1×7, 2×7, 3×7, 0×7, 1×6`** ; avec `AnimTimer = 4` : **`0×4` puis 5 par trame** ; avec
  `Frames.Length = 1` : toujours 0 ;
- montage headless (`BackdropRendererTests`, quatre `Texture2D` distinctes injectées par réflexion,
  texture soumise relue dans les `SpriteData`) : au tick 7 c'est `Frames[1]`, au tick 28 `Frames[0]`
  — **la même suite** que le test de cadence ;
- repli (`ResolveFrameAssetIds`, sans `GraphicsDevice`) : une couche sans `FrameTextureAssetIds` se
  résout à exactement `[TextureAssetId]` ; un document `AnimNum = 4` sans ids, avancé de 40 ticks,
  n'observe que la trame 0, **sans exception** ;
- échec partiel (D-E9-9) : une trame `f ≥ 1` qui échoue laisse une couche à une trame et un
  avertissement ;
- site de production (montage réel 389 d'`AlundraWorldProxyGlobalFreezeTests`, renderer réel avec
  une couche synthétique injectée dans `_layers` et substitué dans `AlundraBackdropStage._backdropRenderer`
  par réflexion — le stage s'atteint par `AlundraWorldProxy._backdropStage`, `internal readonly` —,
  compteurs relus par réflexion après `proxy.Update`) : **`ticksThisFrame = 0` → aucune avance ;
  `= 2` → deux avances**, mesurées à ce site malgré les deux retours anticipés. **Recette imposée par
  le montage (§1.4)** : (a) **avant le premier `proxy.Update`**, poser
  `AlundraBackdropStage._clearColorApplied = true` par réflexion (même hop que la substitution du
  renderer), sinon `ApplyOriginalBackgroundClearColorOnce` lève à `:1519` avant d'atteindre le site ;
  (b) une **frame d'amorçage** `proxy.Update(0.02f)` d'abord (plancher collant `max(ticks, 1)` de
  première frame), puis les mesures **en delta de compteurs** : `proxy.Update(0f)` → delta 0 ;
  `proxy.Update(0.04f)` → exactement 2 ticks → delta 2. Tout **autre** site nul rencontré sur le
  chemin `proxy.Update` de ce montage est un arrêt (§4) à rapporter — jamais une raison de rabattre
  le pin sur un appel direct au stage, qui ne prouverait plus rien du site de production.
**Mutations** : `>=` à la place de `>` sur le timer → la cadence tombe (premier palier 5, période 6) ;
compteur avancé par frame rendue au lieu de par tick → les cas `0` et `2` du site de production
tombent ; boucle sur `document.AnimNum` au lieu de `Frames.Length` → le test « sans ids, 40 ticks,
sans exception » tombe ; repli supprimé → le test de `ResolveFrameAssetIds` tombe ; avance placée
après les retours anticipés → le pin du site de production tombe.

### B4 — Validation en jeu

- **389** : mer et ombres de nuages **inchangées à l'œil**, aucune régression (B1).
- **159** (grotte des fées sous l'eau) : la lueur anime **toute** la caverne en cycle de quatre
  trames, au lieu du seul quart supérieur fixe (B2 + B3). Le chemin d'accès en jeu à la 159 n'est pas
  disponible par les portails actuels ; la validation passe par le lancement direct de ce monde
  (`FirstWorldLoaded` temporairement pointé sur la 159 **par l'utilisateur**, jamais par une
  modification de `Program.cs` par moi).

---

## 4. Arrêts

- toute modification du moteur (sous-module) ou de l'analyseur ;
- **le convertisseur touché sans que `e9-baseline-manifest.sha256` existe** (D-E9-8, étape 0) ;
- un fichier supprimé à la main sous `alundra-project/` ;
- `CasaEngine.Launcher/Program.cs` modifié ou indexé ;
- une texture ou un id **existant** qui change de nom, d'id ou de contenu après B2 (contredit D-E9-2) ;
- un compagnon **non animé** modifié par l'export (contredit D-E9-3) ;
- un diff de double export **hors** `report.json` sur le périmètre de D-E9-8 — ou tout diff « toléré à
  la main » hors de ce périmètre ;
- un golden qui bouge ;
- `Backdrop.LayersExported ≠ 132` ou `Backdrop.FramesExported ≠ 153` sur le run complet ;
- un nouvel échec dans l'une des trois suites ;
- la nécessité de toucher au mode cellulaire ou au `WaveLut` (contredit D-E9-7) ;
- la nécessité d'unifier l'horloge d'auto-défilement (contredit D-E9-6 — c'est E9.b) ;
- un site nul **autre** qu'`ApplyOriginalBackgroundClearColorOnce` sur le chemin `proxy.Update` du
  montage réel 389 (B3, §1.4) — à rapporter, jamais à contourner par un appel direct au stage.

---

## 5. Journal d'exécution

**Approbation utilisateur le 2026-09-04** : exécuter B1∥B2 → B3 → B4 ; D-E9-U1 tranché — le
`WaveLut` sort du chantier (→ chantier cellulaire) ; mode AUTO jusqu'à B4. Ordonnancement retenu en
session : **séquentiel** B1 → B2 → B3 (deux exécutants dans le même dépôt compileraient les projets
moteur partagés en concurrence ; un export in-place pendant une suite `Alundra.Tests`, qui lit la
389 réelle, serait instable ; les worktrees n'ont ni le sous-module ni `alundra-project/`). Les
exports complets sont lancés **en session principale**, jamais par un exécutant.

**B1 — CONFIRMED, commit `82ad020`** (2026-09-04). `Alundra.Tests` 768/768 (764 + 4). Mutation
`−scrollY` vérifiée à la main par l'exécutant (239 → 241, test tombé, réverté). Trois avis P4 du
vérificateur, **différés** : (1) `BackdropRenderer.LastLayerOffsetForTests`, seam d'observation
écrit à chaque couche de chaque `Draw` — coût nul en pratique, à revoir quand E9.b porte le rendu
dans un composant moteur ; (2) le test delta préexistant
`Draw_Factor1Layer_IsGluedToWorld_WhenCameraMovesVertically` dérive désormais ses scrolls de
`ToOriginalScrollSpace` et ne discrimine plus seul une erreur de signe — couvert par les deux tests
purs et le pin absolu ; (3) la doc de `ComputeParallaxOffset` dit « déjà clampé non négatif » alors
qu'une ligne `InlineData(-40, …)` exerce un négatif — nuance de doc, troncature vers zéro conforme.

**B2 — CONFIRMED, commit `75dc032`** (2026-09-04). Le vérificateur a refait un **troisième export
complet** depuis l'extraction corrigée et re-haché l'arbre entier lui-même (23 013 entrées, seul
`report.json` diffère du second export), relu les quatre trames 640×480 de la 159 et la résolution de
leurs ids dans `AssetInfos.json`, confirmé l'absence de `FrameTextureAssetIds` sur la 389 et sur
tous les compagnons sauf sept, re-haché cinq PNG existants contre le baseline, 153/153 et 768/768.
Deux avis P4 différés : `isFullRun` calculé en O(n²) sur 483 cartes (microsecondes) ; la constante
de corpus `ExpectedFramesExported = 153` codée en dur, convention `WorldWriter` assumée par D-E9-4.
Nuance relevée sur la table de mutations : « trame vide omise » est attrapé par les assertions de
fichiers et d'ids autant que par la longueur (le tableau est pré-dimensionné à `AnimNum`).
Convertisseur 153/153
(152 + 1 ; le test 389-like étendu en place : `FramesExported == 1`, chaîne JSON sans
`FrameTextureAssetIds`, aucun fichier `-frame*`). Mutations `vAnim` ignoré et `[JsonIgnore]` retiré
vérifiées à la main par l'exécutant. **Découverte de chantier, hors B2** : le premier export complet
depuis `data-extracted/` a montré, en plus de l'ensemble attendu, **~300 sorties de dialogue
modifiées** (`Dialogues/control-codes.json` + les `*.strings.json`), byte-identiques à l'export du
1er septembre — c'est-à-dire **au texte non décodé** (`o}i` pour « où »). Cause mesurée :
`data-extracted/` du dépôt est l'extraction du **30 août, antérieure au correctif de décodage du 2
septembre** (`a8598f4`) ; l'extraction corrigée vit dans `D:/development/repo/Alundra
Remake/remaster-data-extracted` (2026-09-02 08:46 ; 300 JSON de `data/` diffèrent des 3386), et c'est
elle qui a nourri l'export de référence du 2 septembre — preuve empirique : l'export depuis cette
entrée reproduit **exactement** l'ensemble attendu de D-E9-8. **Déviation consignée** : la preuve a
été faite avec `<inputDir> = …/remaster-data-extracted`, pas `data-extracted` comme l'écrit D-E9-8.
Résultat (deux exports, 57 s et 62 s) : 42 ajouts (21 PNG + 21 `.texture`, tous sous les sept cartes
animées), 0 suppression, 9 modifiés (7 compagnons + `AssetInfos.json` + `report.json`), 0 texture
existante modifiée, double export ⊆ {`report.json`}, `Backdrop.LayersExported = 132`,
`Backdrop.FramesExported = 153`, 0 erreur, 7 avertissements inchangés, `Alundra.Tests` 768/768
(goldens). **À trancher par l'utilisateur** : rafraîchir `data-extracted/` depuis l'extraction
corrigée (ou en faire une jonction) pour que la commande documentée redevienne juste.

**B3 — CONFIRMED après une passe de récupération, commit `394cf55`** (2026-09-04). Livraison :
`LayerRuntime` passé de `readonly struct` à `sealed class` (compteurs mutables), `Frames`/`AnimTimer`
par couche, `ResolveFrameAssetIds` pure, `LoadLayerFrames(ids, loader, …)` — seam de chargement par
délégué prévu par le plan, seul moyen de tester D-E9-9 sans `GraphicsDevice` —, `AdvanceAnimation`
en première instruction du stage, `Draw` sur `Frames[AnimFrameCounter]`, site d'appel `:1520`.
Mutations vérifiées à la main par l'exécutant : `>=` sur le timer (palier 5), avance après les deux
gardes (pin 389 tombe). **Complément demandé en session** : la mutation « avance entre les deux
gardes » ne tombait pas, le premier garde (`HasContent`/`Game`) ne s'activant pas sur le montage 389 ;
second pin ajouté sur un monde sans `Game` (premier garde actif) — il tombe seul sur cette mutation.
**Première vérification : REFUTED, F1 P2 introduit** — sur l'échec d'une trame `f ≥ 2`,
`LoadLayerFrames` faisait `break` et rendait le préfixe partiel `[f0..f-1]` au lieu de `[frame0]`
(D-E9-9) ; le test `f = 1` était aveugle par coïncidence. Corrigé en session principale (`return
new[] { frames[0] }`), test `LoadLayerFrames_FrameThreeFails_FallsBackToFrameZeroOnly_NotToThePartialPrefix`
ajouté (aurait rendu trois trames sur l'ancien code), **revérification fraîche : CONFIRMED**,
`Alundra.Tests` **781/781** (768 + 13). Avis P4 différés : double avertissement quand l'id de la
trame 0 est illisible ; `LoadLayerFrames` sur un tableau vide rendrait `Texture2D[0]` (inatteignable,
`ResolveFrameAssetIds` garantit ≥ 1 et est le seul appelant). Huit aides de
`AlundraWorldProxyGlobalFreezeTests` passées `internal` pour réutiliser le montage 389.

**État du chantier** : B1, B2, B3 commis ; l'arbre `alundra-project/` est l'export corrigé (trames +
texte décodé) et `Alundra.dll` y est déposée par le dernier build. `data-extracted/` rafraîchi depuis
le remaster (décision utilisateur, miroir `robocopy` depuis PowerShell, identité prouvée 0/4450).

**B4 (2026-09-04)** : **389 validée** par l'utilisateur, aucune régression. **159 : « juste un bout de
l'effet »** — enquête en session, règle du journal d'abord : le journal est propre (les quatre
trames de la couche 0 chargées, aucun repli). Les trames cuites n'ont du contenu que dans les 96 px
du haut, dans les quatre phases. Mesuré sur la source : la grille 40×30 de la 159 n'a que **six
lignes de tuiles** (rows 0-5, tuiles `05`/`01-04`/`11-14`/`21-24`/`31-34`, V-rows 0/16/32/48, le
reste à zéro), et l'original saute lui aussi `tileVal == 0` (`GraphicManager.cs:971-973`) ; le
calque est fixé à l'écran (`Scrollar` 0/1 sur les deux axes, aucun auto-défilement), `Ground = 1`,
`BlendMode = 2` (overlay additif, `RenderPass2D.Effects`) ; les phases V décalent à peine le bord
ondulé de la bande. `WaveLut` re-vérifié sous toutes les orthographes : lecteurs cellulaires
seulement (`:1190-1209`, tick `:1011`). Ancrage DLL = original (tuile (0,0) en haut à gauche de
l'écran, décalage 0). **Conclusion provisoire** : d'après les données, ce calque *est* une bande
scintillante de 96 px en haut de l'écran ; l'attente « la lueur anime toute la caverne » héritée de
la clôture d'E10 n'est **pas** soutenue par les données. Suite suspendue à l'utilisateur : capture du
port, et ce que montre l'original sur la 159.

**B5 — la toile ancrée sur l'écran de l'original (ajout de chantier, 2026-09-04).** L'utilisateur
a fourni la référence (jeu C# décompilé) : brume **en haut** de l'écran, pleine largeur, bord
ondulé animé — ce que les données décrivent. Capture du port par la session (lanceur lancé depuis
PowerShell, `CopyFromScreen`) et **profil de luminosité par ligne** : la bande du port est complète
(88 px, mélange additif juste : 98,7 sur le vide, ~130 sur le sol) mais commence à la ligne d'écran
**126** au lieu de 0. Racine : `AlundraBackdropStage.UpdateAndDrawBackdrop` passait
`world.Game.ScreenSizeWidth/Height` — la taille de la fenêtre en **pixels** (1280×944,
`CasaEngineGame.ScreenSizeHeight` = BackBufferHeight) — que `BackdropRenderer.Draw` emploie comme
demi-étendues en **unités monde** (le zoom `944 / 236 = 4` ramène la fenêtre sur la vue 320×236).
D'où `halfHeight = 472` au lieu de 120, 2×2 copies de toile (`ComputeCoveringOrigins1D`), et la copie
décalée de 480 dont la ligne 0 tombe à `camera.Y − 8`, soit la ligne d'écran 118 + 8 = 126 — au
pixel près. Sur la 389 (nuages périodiques 640×480), les décalages de 640 en X et 8 en Y étaient
invisibles. **Pourquoi le pin de B1 ne l'a pas vu** : son montage `BuildWorldWithGame(…, 320, 240)`
donnait au jeu de test une taille « en unités monde » — famille « vert et inerte ».
**Correction** : le stage passe `AlundraCameraMath.CameraVisibleWidth/Height` (320×240, passés
`internal`) — coin haut-gauche de la toile = coin du framebuffer PSX = `Target + (−160, +120)` (E5) ;
le quad de teinte suit. **Pin** `UpdateAndDrawBackdrop_ProductionSite_AnchorsTheCanvasOnTheOriginalScreen_NotOnTheWindowPixels`
: jeu de test à **1280×944 pixels**, couche à facteur 0/1, `Target = (1087, −839)` → exactement **un**
quad, coin haut-gauche (927, −719), offsets (0, 0). Mutation à la main (retour aux pixels) : le pin
tombe — `Assert.Single` sur 4 éléments, `(447, −367), (1087, −367), (447, −847), (1087, −847)` —
les valeurs prédites. `Alundra.Tests` **782/782**. Capture du port corrigé : bande de la ligne 0 à ~84,
superposable à la référence. Le seam `LastLayerOffsetForTests` de B1 reste ; la mesure par capture
d'écran est la méthode à réutiliser pour tout bug visuel de placement.
**Vérification fraîche : CONFIRMED** (dérivation indépendante du coin depuis
`ToOriginalScrollSpace` seule ; un seul quad prouvé par `ComputeCoveringOrigins1D(320, 0, 640)` ;
ancrage à +120 et non +118 justifié — c'est l'origine du défilement passé au même appel ; quad de
teinte = exactement le rectangle 320×240 de `RenderTileOverlayLayer` ; plus aucun lecteur de
`ScreenSizeWidth/Height` dans la DLL hors commentaire). Deux avis différés : **A1 P3, introduit,
inerte** — `Draw` passe `fullViewport = (0, 0, 320, 240)` comme rectangle de **ciseaux** aux sprites,
un rectangle en pixels-machine qui ne signifie plus « tout l'écran » ; sans effet tant que
`CullCounterClockwise` laisse `ScissorTestEnable` faux (capture : pixels bien au-delà de la ligne
240), à corriger en prenant le rectangle du périphérique plutôt que la taille de vue monde ; **A2 P4,
préexistant** — le port affiche 236 des 240 lignes **centrées** (zoom `/236`, `Target` au centre
d'une fenêtre de 240), l'original recadre peut-être asymétriquement : au plus deux lignes.

---

## 6. Clôture d'E9.a (2026-09-04)

**Validé en jeu par l'utilisateur** : 389 sans régression, 159 conforme à la référence du jeu C#
décompilé (brume en haut, pleine largeur, vague animée). Tranches livrées : B1 `82ad020`, B2
`75dc032`, B3 `394cf55`, **B5 `14d94e0`** (ajout de chantier : ancrage de la toile, racine trouvée
par capture d'écran et profil de luminosité). Suites : `Alundra.Tests` 782/782, convertisseur
153/153 ; export corrigé en place (trames + texte décodé), `data-extracted/` rafraîchi et prouvé
identique au remaster. `WaveLut` : hors périmètre, inscrit au chantier du mode cellulaire (D-E9-7).

**Chasse contradictoire post-B5** (3 lentilles lecture seule, 2 sceptiques par candidat) : **aucun
autre défaut pixels/monde visible** dans la DLL. Réfutés comme défauts visibles (mécanique exacte,
effet nul) : le rectangle de ciseaux 320×240 (A1) ; l'overlay moteur `ScreenEffectComponent`
(`SubmitOverlay(…, ScreenSizeWidth, ScreenSizeHeight)`) dimensionné en pixels pris pour des unités
monde — correct par sur-couverture, chemin du fondu E10.b compris ; le zoom caméra dérivé une fois
du viewport pixel et jamais rafraîchi (`OnScreenResized` ne réécrit que `_viewport`) — sans
déclencheur atteignable dans le runtime Alundra.

**Suites différées (à reprendre, par priorité)** :
- **P3 — reprise par E9.b, close** : le rectangle de ciseaux de `BackdropRenderer.Draw` (A1) est
  désormais reçu **en paramètre** par le mécanisme moteur (`ScrollingLayerComponent.Submit`), résolu
  par l'appelant sur le périphérique quand un device existe (D-E9b-6) — plus de rectangle en unités
  monde codé en dur. `ScreenEffectComponent` sur-dimensionné en pixels-monde est corrigé côté moteur
  par la couture pure `TryGetCameraViewSize` (D-E9b-12), avec repli explicite si la caméra active est
  nulle ; sa doc ne prétend plus une parité qui n'était plus vraie depuis B5. Voir
  `docs/plan-e9b-backdrops-moteur.md` §2 (D-E9b-6, D-E9b-12) et §3 (S0).
- **Restent hors périmètre** (décision utilisateur D-E9b-U4) : le biais de centrage E5 (2 px,
  propriété de la caméra, pas du fond) et le fond noir via `environment` (changement convertisseur).
- **P4** — fenêtre redimensionnable (`AllowUserResizing`) : la couverture codée 320×240 suppose le
  rapport 320:236 ; un rapport plus large laisserait les bords découverts ; le zoom n'est pas
  recalculé. Décider si le port suit le redimensionnement ou verrouille le rapport.
- **P4** — marge horizontale nulle : un `Target` fractionnaire (pan de débogage) peut découvrir une
  colonne de 1 pixel-machine ; sans effet avec les `Target` entiers du suivi caméra.
- **P4 (moteur, latent)** — overlay à z = 0 testé en profondeur contre les plans de tuiles
  (0.1/0.2/0.3), sûr parce qu'E7 vide ces plans ; overlay mis en file une fois par `Update`, sûr
  parce qu'`IsFixedTimeStep` est faux (1 Update : 1 Draw).
- Les P4 de B1 (seam d'observation, test delta, doc de troncature), B2 (`isFullRun` O(n²),
  constante de corpus 153) et B3 (double avertissement, tableau d'ids vide inatteignable).

**E9.b, clos** : le rendu des fonds a été migré vers le composant moteur de couches défilantes
(`docs/plan-e9b-backdrops-moteur.md`), avec **une seule horloge** (D-E9-6/D-E9b-3) ; les corrections
B1–B5 en ont été la référence visuelle et les pins de site de production (B1, B3, B5) ses tests de
non-régression, re-hébergés dans `CasaEngine.Tests` et `Alundra.Tests` (D-E9b-11). Voir le journal
d'exécution de ce plan, §5.
