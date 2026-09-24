# Plan — Écrans d'Alundra en assets du projet, liés à des view-models

Tranches consommatrices du programme moteur
[`CasaEngineMonogame/ai-agent/tasks/bound-screens-tasks.md`](../CasaEngineMonogame/ai-agent/tasks/bound-screens-tasks.md)
(décisions D1 → D12, points à valider P1 → P9). Décision du dépôt :
[ADR-0002](decisions/0002-game-screens-are-project-assets-bound-to-view-models.md), qui remplace l'ADR-0001.
**Ce plan applique les décisions, il ne les rediscute pas.**

Statuts : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Objectif

Les écrans d'Alundra (inventaire, HUD, boîte de dialogue) deviennent des assets du projet, versionnés dans
`alundra-project/UI/Screens/`, ouvrables et prévisualisables dans l'éditeur. Ils se lient à des view-models
écrits par les présentateurs. Le curseur, les pastilles de magie et la pièce deviennent des animations d'asset
écrites par le convertisseur. Le HUD passe en XAML. Plus aucun XAML n'est embarqué dans `Alundra.dll`.

## État vérifié (2026-09-24)

**Git** : branche `chantier/bound-screens` créée depuis `main` à `084d3e8`. Modifications préexistantes à ne pas
indexer : aucune dans le parent en dehors du sous-module moteur (`CasaEngine.Launcher/Program.cs` de l'auteur).

**Projet généré**
- `alundra-project/` est ignoré en bloc (`.gitignore:63`). Un motif de négation seul ne réinclut pas un chemin sous
  un dossier ignoré (vérifié le 2026-09-23 par `git check-ignore -v`, fichier de test retiré ensuite).
- L'export ne supprime aucun fichier qu'il n'écrit pas, mais réécrit `AssetInfos.json` en entier
  (`alundra-casaengine-project-converter/Writers/ProjectWriter.cs`, `CreateEmptyProject`).
- Le 2026-09-24, `alundra-project/UI/` avait disparu hors de git ; régénéré par un export complet depuis
  `data-extracted/`, à la demande de l'auteur : manifeste avant/après = 336 fichiers ajoutés sous `UI/` et
  `report.json`, rien d'autre ; 0 erreur ; recette 389 → 390 → 389 repassée. La copie `data-extracted/` est la bonne
  source : l'extraction du remaster du 2026-09-19 a le texte non décodé.

**Écrans** (`Alundra/Scripts/`)
- Inventaire : `AlundraInventoryScreen : XamlUIScreenBase` sur le XAML embarqué `Alundra/Screens/InventoryScreen.xaml`
  (`Alundra/Alundra.csproj:23`) ; 42 `Image` dont aucune ne porte de source. 8 statiques (6 boîtes, 2 cadres de
  sélection), 24 icônes et 9 chiffres pilotés par l'état, 1 curseur piloté par le temps.
- HUD : `AlundraHudScreen : UIScreenBase` (`AlundraHudScreen.cs:68`), construit en C# : réserve de 26 images, 2 fonds
  d'équipement en dégradé, 2 icônes d'équipement.
- Présentateurs : `AlundraInventoryPresenter.cs:67-80` et `AlundraHudPresenter.cs:112-135` poussent tout le modèle à
  chaque tick logique. Écrans poussés par `AlundraWorldProxy.cs` (HUD), `AlundraInventoryPresenter.cs:89` et
  `AlundraDialoguePresenter.cs` (écran de dialogue du moteur).
- Cycles d'origine, à 50 Hz : curseur 4 × 10 ticks (200 ms, `AlundraInventoryDirector.cs:520-524`), avec un décalage
  d'un pixel selon la phase (`AlundraInventoryComposer.cs:105-106`) ; pastilles 4 × 10 ticks, phase décalée par
  pastille (`AlundraHudDirector.cs:461-468`) ; pièce 4 × 6 ticks (120 ms), seulement pendant le défilement de
  l'argent, figée sur sa première image sinon (`AlundraHudDirector.cs:594-634`).

## Règles

- Branche `chantier/bound-screens`, jamais de commit sur `main`, jamais de push.
- Un commit par tâche, avec la mise à jour de ce plan. Message en anglais `type(area): summary`.
- Indexation fichier par fichier ; ni `.serena/`, ni les modifications de l'auteur dans les sous-modules.
- Build `Alundra/Alundra.csproj` et `dotnet test Alundra.Tests/Alundra.Tests.csproj` avant toute tâche ✅ ; tests du
  convertisseur (`alundra-casaengine-project-converter.Tests`) dès que le convertisseur change.
- Toute modification du convertisseur se valide par un **export complet en place** depuis `data-extracted/`, sans
  jamais supprimer `alundra-project/` avant, avec un manifeste avant et après ; jamais pendant une suite de tests.
- Recettes en jeu : prédiction écrite avant chaque exécution ; captures par le back-buffer (`GetBackBufferData`),
  harnais hors dépôt.

## Tâches

### ✅ B1 — Écrans versionnés et animations d'interface

**Prérequis :** phases 1 à 4 du programme moteur closes ; la référence du moteur passe à leur dernier commit.

**Étapes :**
1. `.gitignore` : remplacer `alundra-project/` par une exclusion en cascade qui ne versionne que
   `alundra-project/UI/Screens/` (`alundra-project/*`, `!alundra-project/UI/`, `alundra-project/UI/*`,
   `!alundra-project/UI/Screens/`).
2. Convertisseur, passe des écrans : enregistrer au catalogue chaque `.uiscreen` trouvé sous `UI/Screens/`, avec
   l'identifiant écrit dans l'enveloppe ; ne jamais écrire dans ce dossier. Enregistrer les fichiers voisins comme
   le fait RPGDemo (à relever au début de la tâche).
3. Convertisseur, animations d'interface : écrire `UI/Animations/` avec trois `.anim2d` en boucle, une partie, une
   piste de sprite en paliers et une image de fin qui répète la dernière (convention de `SpriteWriter.cs:510-514`) :
   curseur (sprites `wind_159/182/210/237`, 200 ms, avec le décalage de pixel par phase), pastille pleine et pièce
   (sprites relevés dans `AlundraHudComposer.cs` au début de la tâche ; 200 ms et 120 ms). Identifiants stables par
   `Ids.For`.
4. `AssetVerifier` : vérifier aussi les `.uiscreen`.

**Validation :**
- `git check-ignore -v` : un fichier sous `UI/Screens/` n'est pas ignoré ; un fichier sous `UI/` hors `Screens/` et
  un fichier ailleurs dans `alundra-project/` le sont.
- Tests du convertisseur, dont un test du writer d'animations (durées, image de fin, identifiants stables).
- Export complet : le diff des manifestes ne contient que `UI/Animations/`, `AssetInfos.json` (les nouvelles
  entrées) et `report.json` ; 0 erreur ; un second export ne change que `report.json`.

**Commits :** `chore(submodules): point at the engine with bound screens` ;
`feat(convert): catalogue versioned screens and write the UI animation cycles`.

**Fait (2026-09-24) :**
- Relevé demandé à l'étape 2 : RPGDemo ne catalogue que le `.uiscreen` (`CasaEngineMonogame/Projects/RPGDemo/AssetInfos.json`) ;
  le `.xaml`, désigné par l'enveloppe, n'est pas un asset. Le convertisseur fait de même, et ne catalogue pas non plus
  le fichier de conception.
- `.gitignore` en cascade. `git check-ignore -v` : `UI/Screens/*.xaml` et `*.uiscreen` ne sont pas ignorés ;
  `UI/wind_001.sprite`, `UI/Animations/*.anim2d`, `AssetInfos.json` et `Maps/…` le sont. Preuve sur un vrai
  fichier : un témoin créé sous `UI/Screens/` est le seul fichier d'`alundra-project/` que `git status` montre, puis
  il est retiré.
- `UiWriter` : `WindSpriteId(index)` partagé ; `RegisterVersionedScreens` catalogue chaque `.uiscreen` de
  `UI/Screens/` avec l'identifiant de son enveloppe, sans jamais écrire dans ce dossier (aucun écran pour l'instant :
  B2 y met l'inventaire).
- `UiAnimationWriter` : `UI/Animations/ui_inventory_cursor`, `ui_hud_magic_pip` et `ui_hud_coin.anim2d`, une partie,
  une piste de sprite en paliers et une image de fin qui répète la dernière ; identifiants `Ids.For("anim2d-ui:…")`.
  Une animation dont un sprite n'a pas été écrit est sautée avec un avertissement.
  - Pastille `wind_001/003/010/017` et pièce `wind_126/130/134/139`, relevés dans `AlundraHudScreen.cs:473-481`.
  - **Convention constatée dans le code du moteur** : le lecteur d'animation de l'interface ajoute la position de la
    partie, arrondie, à la position de dessin de l'image, en pixels d'écran, Y vers le bas
    (`CasaUIAssetProvider.CasaUIAnimatedImage.CurrentDrawOffset`). Les décalages du curseur sont donc écrits Y vers le
    bas, contrairement à une animation du monde (Y vers le haut). Rien ne documente cette convention côté moteur :
    point à écrire dans la doc de T7.1.
  - L'original déplace le curseur un tick après avoir changé son sprite ; ici les deux changent ensemble, soit un
    décalage de 20 ms sur la position, dans la tolérance du programme.
- `AssetVerifier` : un `.uiscreen` est chargé par `UIScreenAsset.Load`, et le XAML qu'il nomme (et son fichier de
  conception s'il en nomme un) doit exister.
- Tests du convertisseur : 188/188 (+6). Nouveaux : cadence et décalages des trois cycles, image de fin, durée, deux
  identifiants de sprite recoupés avec ceux que la DLL cite déjà, octets identiques d'un export à l'autre,
  animation sautée sans ses sprites, écran versionné catalogué avec son identifiant et jamais réécrit, et deux tests
  du vérificateur. Une mutation de la cadence de la pièce fait échouer un test.
- Export complet en place depuis `data-extracted/` : 0 erreur, vérification PASSED (19 520 chargés). Manifeste avant
  et après : 3 ajouts (`UI/Animations/*.anim2d`), `AssetInfos.json` et `report.json` modifiés, rien d'autre ; le
  catalogue privé de ses 3 nouvelles entrées est identique octet pour octet à la référence. Second export : seul
  `report.json` change.

### ✅ B2 — L'inventaire en asset lié

**Étapes :**
1. Déplacer `Alundra/Screens/InventoryScreen.xaml` vers `alundra-project/UI/Screens/InventoryScreen.xaml` (ses
   commentaires restent) ; écrire son `.uiscreen` (identifiant fixe) et son fichier de conception
   `InventoryScreen.design.json`.
2. Balisage : `SourceName` des 6 boîtes et des 2 cadres de sélection (GUID fixes) ; bindings des icônes, chiffres,
   textes, visibilités et positions ; le curseur nomme l'animation du curseur.
3. `AlundraInventoryViewModel` observable, qui ne notifie que ce qui change ; le présentateur écrit le view-model au
   lieu d'appeler la vue.
4. L'écran charge l'asset par le catalogue et pose le view-model comme contexte de la fenêtre ; retirer la ressource
   embarquée d'`Alundra.csproj` et le code qui assignait sources et positions.
5. Tests : ceux du présentateur passent sur les propriétés du view-model ; ceux du XAML lisent l'asset versionné ; un
   test de liaison sans affichage ; un test de timing du curseur : sur 25 cycles, chaque changement d'image à moins
   de 20 ms de k × 200 ms.

**Validation :**
- Build, `Alundra.Tests` verts.
- Recette en jeu, prédiction écrite avant : l'inventaire affiche boîtes, icônes, chiffres et textes en `font3` comme
  la capture de référence D5.f, le curseur s'anime ; aucun avertissement.
- Éditeur : l'écran s'ouvre, l'aperçu montre les boîtes et les données de conception ; un enregistrement sans
  modification laisse `git diff` vide.
- **Vérificateur frais.**

**Commit :** `feat(inventory): move the inventory screen to a project asset bound to a view model`

**Fait (2026-09-24) :**
- `alundra-project/UI/Screens/InventoryScreen.xaml` (déplacé par `git mv`, commentaires gardés, l'en-tête devenu faux
  réécrit), `InventoryScreen.uiscreen` (identifiant fixe `d79fc172-…`) et `InventoryScreen.design.json`, produit à
  partir d'un vrai inventaire au repos (état de début de partie, tables réelles) par un test jetable non commité.
- Balisage : les sources fixes sont nommées dans le XAML (6 boîtes, 2 cadres `wind_039`, le curseur = l'animation
  `ui_inventory_cursor`) ; tout le reste est lié (`{dataBinding:MGBinding …}`) : visibilité du canevas, positions,
  visibilités, sources des icônes et des chiffres, textes. `Stretch="None"` : chaque image prend la taille de sa
  source, comme les tailles que le code posait.
- `AlundraInventoryViewModel` : des sous-view-models nommés par élément (`IconSlot0`, `MoneyDigit0`…), types
  identiques à leurs cibles (`int?`, `Visibility`, `string`) pour le chemin de copie typée sans allocation ;
  chaque setter ne notifie que sur changement, un identifiant n'est formaté que s'il change. Les icônes restent
  centrées par le compositeur existant (D-E13D-10) : l'écran lit la taille des sprites d'icône (données tenues,
  ADR-0037 moteur).
- L'écran charge l'asset par son identifiant (`XamlUIScreenBase(AssetContentManager, id)`), pose le view-model comme
  contexte de la fenêtre, et ne garde que le dimensionnement, l'échelle et le filtrage des images (manque G8 du
  moteur : non déclarable en XAML). Le présentateur écrit le view-model ; `IAlundraInventoryView` disparaît ; la
  ressource embarquée est retirée d'`Alundra.csproj`.
- Le compositeur expose la position de base du curseur (`BaseNativeX/Y`), sans le décalage de phase que porte
  désormais l'animation.
- Moteur (`afa1bd4b`) : un identifiant d'écran est résolu par le gestionnaire d'assets et non plus par le catalogue
  global, pour que les tests de la DLL construisent l'écran ; (`0b32f971`) l'automatisation de l'éditeur prend une
  capture finale.
- Tests `Alundra.Tests` 1059/1059 (+9) : présentateur sur le view-model ; XAML lu depuis l'asset versionné ;
  enveloppe et données de conception ; sources fixes ; **liaison sans affichage par chemins imbriqués, qui confirme
  O1** (une mutation qui retire le contexte fait échouer le test) ; aucune notification quand le même modèle revient ;
  cadence du curseur (100 changements sur 25 cycles, tous à moins de 20 ms de k × 200 ms, image et décalage justes).
- Export complet en place : seuls `AssetInfos.json` (+1 entrée, l'écran) et `report.json` changent ; vérification
  PASSED (19 521 chargés, dont le `.uiscreen`).
- Recette en jeu (`d6-font`, prédiction `prediction-b2.md` écrite avant) : `font3` sur 389, 390 et 389 ;
  curseur sur l'animation à la position de base, sur des images différentes au fil du temps ; aucune ligne
  au-dessus d'Info ; un seul chargement de l'enveloppe et de `font3`. Captures comparées à `run-m2-hud` sur les seuls
  pixels de l'inventaire (masque pris sur fond noir, hors boîte du curseur) : `inv-b` et `inv-d` (au repos, +300)
  **identiques au pixel**. Les captures précoces (`inv-389` à +120, `inv-a`/`inv-c` à +90) diffèrent de 5 456 pixels,
  tous dans la ligne de description, qui montre encore « Poignard » au lieu de « Petit poignard. » : la révélation du
  texte est en retard. La prédiction 2, qui attendait `inv-389` identique, était fausse sur ce point. Deux faits,
  dont la part de chacun n'est pas isolée (voir O3) : le run de référence tournait environ trois fois plus lentement
  par frame (un changement de monde y prend 11 frames contre 32 à 33 ici), donc ses captures à +90 et +120 frames
  couvrent environ trois fois plus de temps de jeu ; et la première ouverture coûte ici deux frames de 150,7 ms et
  80,8 ms, alors que l'horloge logique plafonne à 4 ticks par frame (`AlundraScriptedMotion.MaxTicksPerFrame`).
  Même effet de cadence sur la capture du HUD de la 390, où l'argent défile encore (2140 contre 2163).
- Éditeur (automatisation, projet Alundra) : l'écran s'ouvre, l'aperçu montre les boîtes (avec les icônes de la pièce,
  du faucon et de la clé que porte leur image), les chiffres des données de conception et le curseur, sans erreur ;
  aucune icône d'objet, les données de conception décrivant un début de partie.
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le parent `79c0099`, le moteur `0b32f971` et MGUI `09d0462`, sans
  constat P0 à P2 : 172 chemins de binding confrontés au view-model (aucun manquant, aucun type différent) ; zéro
  octet alloué sur 10 000 `Apply` au repos ; `Alundra.dll` sans ressource embarquée ; suites et builds reproduits ;
  recette rejouée (mêmes captures que `run-b2`). Remarques :
  - A1 (P3) : l'attribution d'O3 n'était pas isolée (écart de cadence entre les runs) : note et O3 corrigées ;
  - A2 (P4, reportée) : si `fonts` est null, le constructeur lève après avoir pris l'enveloppe, qui reste tenue ;
    erreur de programmation seulement ;
  - A3 (P4) : la note de l'aperçu disait « icônes » : corrigée ci-dessus ;
  - A4 (P4) : le curseur change d'image et de décalage sur la même frame, l'original un tick plus tard : déjà
    documenté dans `UiAnimationWriter`, dans la tolérance de 20 ms.
- **Reste en 🧪** : « un enregistrement sans modification laisse `git diff` vide » n'est pas vérifiable, l'éditeur
  n'enregistrant aucun écran (O4).
- **Levé le 2026-09-24 par B6** : l'éditeur enregistre les écrans (T4.4) ; sur l'inventaire, un enregistrement
  sans modification n'écrit rien et une modification remise à l'origine laisse `git diff` vide (CONFIRMED).

### ✅ B3 — Le HUD en XAML

**Étapes :**
1. `alundra-project/UI/Screens/HudScreen.xaml`, `.uiscreen` et `HudScreen.design.json` : 26 emplacements `Image`
   nommés, 2 `Rectangle` en `GradientFillBrush`, 2 icônes ; pixels natifs et une seule mise à l'échelle, comme
   l'inventaire.
2. `AlundraHudViewModel` : par emplacement, source, visibilité, position, décalage d'animation et lecture ; les
   pastilles nomment l'animation de pastille avec un décalage de 200 ms par pastille ; la pièce joue pendant le
   défilement de l'argent et revient à sa première image ensuite.
3. Le présentateur écrit le view-model ; l'écran charge l'asset ; retirer la construction en C#.
4. Tests : présentateur sur le view-model ; timing des pastilles (décalages) et de la pièce (120 ms, retour à la
   première image) à moins de 20 ms de l'original.

**Validation :** build, tests ; recette en jeu avec la bascule F1 : HUD identique aux captures de référence
`run-m2-hud` sur les trois étapes, pastilles et pièce animées ; éditeur comme en B2. **Vérificateur frais.**

**Commit :** `feat(hud): rewrite the HUD as a project asset bound to a view model`

**Note de validation (2026-09-24) :**
- Livré : `alundra-project/UI/Screens/HudScreen.xaml`, `HudScreen.uiscreen` (identifiant fixe `37d8200e-…`) et
  `HudScreen.design.json`, tiré d'une vraie jauge affichée par un test jetable non commité. Le XAML déclare les deux
  fonds d'équipement (`Rectangle` en `GradientFillBrush`, valeurs du compositeur), les deux icônes et 26 images, une
  par rôle de tuile (`HpMaxSlash`, `LifeBig0`…, `MagicPip0`…, `MoneyDigit0`…, `Coin`), en pixels natifs sous une
  seule mise à l'échelle ; sources, positions et visibilités liées, plus le décalage et la lecture de l'animation
  pour les pastilles ; la pièce nomme l'animation `ui_hud_coin`.
- `AlundraHudViewModel` implémente `IAlundraHudView` : il range les tuiles du compositeur par rôle (d'après leur
  glyphe) et ne notifie que sur changement. Les pastilles jouent `ui_hud_magic_pip`, redémarrée en phase avec
  `FrameCounter` (décalage `(image × 10 + FC mod 10) × 20 ms`) quand leur ensemble change ou que la jauge réapparaît ;
  la pièce joue tant que le directeur signale le défilement (`IsMoneyRolling`, nouveau), avec le décalage
  `(image × 6 + FC mod 6) × 20 ms`, et reste sinon sur sa première image. Le présentateur pousse l'horloge du
  directeur avant les tuiles ; la construction en C# est retirée.
- Icônes d'équipement : centrées en pixels d'écran comme avant (`AlundraHudIcon.ScreenLeft`), donc une position
  native plus un reste inférieur au pixel natif (`SubPixelOffset`), que l'écran pose sur le `RenderTransform` de
  l'image. La première recette montrait l'épée décalée d'un demi-pixel natif, corrigé ainsi. Le `RenderTransform`
  n'étant pas liable, l'écran pose aussi à la main l'échelle et le glissement du canevas (manque G9, moteur
  `e78e14a9`). Moteur `afbb3cdc` : une image animée libérée n'est plus retenue par son fournisseur.
- Tests `Alundra.Tests` 1082/1082 (+23) : équivalence de la grille avec les tuiles du compositeur (table de sprites
  indépendante) ; pas de notification quand rien ne change ; redémarrages des pastilles ; réapparition ; pièce ;
  icônes (dont la position à l'écran aux échelles 1, 3 et 4) ; timing des pastilles (trois compteurs) et de la
  pièce (trois cas) confronté aux `.anim2d` exportés à moins de 20 ms (une mutation fait échouer trois tests) ;
  XAML lu depuis l'asset versionné (enveloppe, données de conception, éléments, fonds, liaisons, colle de l'écran) ;
  `IsMoneyRolling` du directeur.
- Export complet en place : seuls `AssetInfos.json` et `report.json` changent ; vérification PASSED, les deux
  `.uiscreen` chargés.
- Recette en jeu (`d6-font`, prédiction `prediction-b3.md` écrite avant). Référence : `run-b2` (même harnais, même
  cadence, HUD encore construit en C#), et non `run-m2-hud`, qui tournait environ trois fois plus lentement par frame
  et diffère sur toute l'image (animation du monde, voir B2). Run retenu : `run-b3-clock`, harnais inchangé sauf une
  ligne de log qui donne le `FrameCounter` et l'argent du directeur à chaque capture.
  - `inv-hud-1` et `inv-hud-2` **identiques au pixel**, pastilles et pièce comprises ; `inv-hud-3` identique hors de
    la boîte des pastilles, qui montrent une autre phase de leur cycle (1 008 pixels, même écart aux deux runs B3 ;
    voir O3).
  - Captures de l'inventaire : `inv-a` à `inv-d` identiques ; `inv-389` diffère de 4 720 pixels hors des boîtes des
    pastilles et de la pièce, sur toute l'image, et de 240 dedans (4 960 en tout ; animation du monde), le même genre
    d'écart qu'entre deux runs du HUD en C# (`run-b2` contre `run-b2-times` : 6 720 pixels sur `inv-hud-1`).
  - Le run précédent (`run-b3`) montrait l'argent un tick en avance aux deux captures où il défile (1010 et 2150
    contre 1000 et 2140) ; le run avec le log montre l'argent du directeur à 1000 puis 2140 et les mêmes chiffres à
    l'écran. C'est la cadence, pas le rendu : le harnais ne fixe pas le nombre de ticks à une frame donnée. La
    prédiction 1 (« l'argent aux mêmes valeurs ») tient pour le run retenu seulement.
  - Aucune ligne au-dessus d'Info, aucune exception ; `HudScreen.uiscreen` chargé une seule fois.
- Éditeur (automatisation, projet Alundra) : `HudScreen.uiscreen` s'ouvre sans erreur (« Loaded HudScreen.xaml »),
  l'aperçu montre les fonds en dégradé, les petits cœurs, « /10 », « 0000 » et la pièce des données de conception,
  la hiérarchie liste les rôles.
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le parent `b9f3133`, le moteur `e78e14a9` et MGUI `09d0462`,
  sans constat P0 à P2 : 122 chemins de binding confrontés au view-model (aucun manquant, types identiques à leurs
  cibles) ; table des sprites et `.anim2d` confrontés à l'ancienne table C# ; `Alundra.Tests` 1082/1082 et
  `CasaEngine.Tests` 1843/1843 reproduits ; recette rejouée (`vrun-b3`), identique au pixel à `run-b3-clock` et
  aux mêmes `FrameCounter`/argent ; éditeur rejoué. Remarques :
  - A1 (P3, reportée) : les pastilles restent déphasées entre deux redémarrages quand des ticks logiques sont
    perdus ; décoratif, déjà consigné en hypothèse dans O3 ;
  - A2 (P4) : le compte de `inv-389` ne donnait que les pixels hors boîtes : corrigé ci-dessus ;
  - A3 (P4) : `IsMoneyRolling` reste vrai le tick où la pièce revient à l'image 0 et ne tombe qu'au tick suivant
    (branche soldée), comme sa documentation le dit : la pièce joue un tick de plus sur sa première image, sans
    effet visible, dans la tolérance de 20 ms.
- **Constat postérieur (session principale, 2026-09-24), corrigé** : un écran libéré laissait ses bindings dans le
  registre statique de MGUI, qui gardait l'ancienne fenêtre et son view-model atteignables ; l'inventaire (B2) et
  le HUD (B3) étant reconstruits à chaque changement de monde, le registre grossissait à chaque changement. Corrigé
  par la tâche moteur T5.2 (MGUI `0559e6b`, moteur `25400d4e`, manque G10) ; deux tests de la DLL (inventaire et
  HUD libérés sans binding restant) échouent sans la correction. `Alundra.Tests` 1084/1084 ; recette rejouée
  (`run-t52`) identique à `run-b3-clock`, sauf `inv-389` (même écart d'animation du monde qu'entre deux runs).
- **Reste en 🧪** : l'enregistrement sans modification, comme B2 (O4).
- **Levé le 2026-09-24 par B6**, comme B2, sur le HUD (CONFIRMED).

### ✅ B4 — La boîte de dialogue en asset

**Prérequis :** tâche moteur T6.1 close.

**Étapes :** écrire `UI/Screens/DialogueScreen.*` à partir du XAML par défaut du moteur, avec son fichier de
conception ; renseigner le champ de réglage du projet qui le désigne (écrit par le convertisseur dans
`AlundraGame.json`).

**Validation :** recette : un dialogue de la 389 s'affiche comme avant (capture comparée) ; l'écran s'ouvre et se
prévisualise dans l'éditeur. **Vérificateur frais.**

**Commit :** `feat(dialogue): ship Alundra's dialogue screen as a project asset`

**Note de validation (2026-09-24) :**
- Livré : `alundra-project/UI/Screens/DialogueScreen.xaml`, copie du balisage embarqué du moteur (mêmes éléments,
  mêmes valeurs) avec trois différences : ses commentaires, le nom de fenêtre `AlundraDialogue` (invisible, il
  distingue le remplacement du balisage embarqué dans les tests et la hiérarchie de l'éditeur) et l'absence de
  `btnClose`, qu'Alundra retire de toute façon (`ShowCloseButton` faux) et que le contrat rend facultatif dans ce
  cas ; `DialogueScreen.uiscreen`, identifiant fixe `91273d6d-…`, aperçu 1280 × 944 (la fenêtre du jeu).
- **Écart au plan : pas de fichier de conception.** Le format des données de conception exige un type de
  view-model à peupler, et la boîte de dialogue n'en a pas : son code remplit des éléments nommés, rien n'y est lié.
  Un fichier de conception n'y montrerait rien ; l'aperçu de l'éditeur montre la boîte vide.
- Convertisseur : `UiWriter` pointe le projet sur `UI/Screens/DialogueScreen.uiscreen` quand il le catalogue
  (`ProjectWriter.SetDialogueScreenAsset`, même lecture-modification-écriture du JSON que `SetFirstWorldLoaded`) :
  l'identifiant vient de l'enveloppe, jamais d'une constante recopiée. Sans ce fichier, le réglage reste absent.
- DLL : `AlundraDialoguePresenter` reçoit le gestionnaire d'assets du jeu et prend alors le nouveau constructeur
  du moteur (T6.1) ; il devient `IDisposable` et rend son écran. Le proxy garde son présentateur et le libère dans
  `OnEndPlay`, comme le HUD et l'inventaire ; il était jusque-là recréé à chaque monde sans être libéré.
- Tests : `Alundra.Tests` 1089/1089 (+5 : enveloppe, balisage du projet utilisé et piloté, boîte embarquée sans
  gestionnaire, libération par `Dispose` et par `OnEndPlay`) ; convertisseur 190/190 (+2 : le réglage écrit avec
  l'identifiant de l'enveloppe, absent sans elle) ; trois mutations (présentateur sans gestionnaire, `OnEndPlay`
  sans libération, XAML sans `lblLine`) font chacune échouer les tests qui les visent.
- Export complet en place : seuls `AlundraGame.json` (+ `DialogueScreenAsset`), `AssetInfos.json` (+1 écran) et
  `report.json` changent ; vérification PASSED, trois `.uiscreen` chargés.
- Recette en jeu (harnais `dialogue-ab`, prédiction `prediction-b4.md` écrite avant) : même build, mêmes données,
  deux runs qui ouvrent le premier message local de la 389 par `AlundraDialogueDirector.Open`, l'un avec le réglage
  exporté, l'autre avec le réglage vidé juste après le chargement du projet.
  - Le premier run utilise le remplacement (`AlundraDialogue`), le second le balisage embarqué ; mêmes bornes de
    fenêtre (716 × 150 en 280, 746).
  - À +60 frames, les deux captures de ce run sont identiques au pixel : coïncidence de cadence (les runs du
    vérificateur y diffèrent de 7 219 pixels dans la boîte). À +300, la boîte diffère de 7 404 pixels, tous d'au
    plus 13 niveaux par canal : c'est le monde animé, qui diffère ailleurs de 146 064 pixels, vu à travers la boîte
    translucide (alpha 235) ; aucun écart de texte ni de cadre. La preuve tient à ce que l'écart A/B dans la boîte
    reste dans le bruit de deux runs de la même configuration (au plus 13 niveaux). La prédiction 2 n'avait pas
    prévu cette transparence.
  - Aucune erreur ni exception ; un seul avertissement par run, identique dans les deux, sans lien avec B4 (O5).
    `DialogueScreen.uiscreen` chargé une fois dans le premier run, jamais dans le second.
- Éditeur (automatisation) : l'écran s'ouvre (« Loaded DialogueScreen.xaml »), la hiérarchie liste
  `AlundraDialogue`, `pnlContent`, `lblLine` et `pnlChoices`, l'aperçu montre la boîte « Dialogue » vide ; journal sans
  avertissement.
- Le constat P3 de la vérification de T6.1 (un identifiant d'un autre type d'asset déjà en cache lève au lieu d'un
  repli) a désormais un appelant ; le convertisseur écrit l'identifiant de l'enveloppe, donc le cas ne se produit
  que par une retouche fautive d'`AlundraGame.json`.
- **Vérificateur frais (2026-09-24) : CONFIRMED** sur le parent `6327948` et le moteur `04a870b6`, sans constat P0 à
  P2 : XAML comparé au balisage embarqué (trois différences exactement : nom de fenêtre, commentaire, `btnClose`) ;
  raison de l'absence de fichier de conception confirmée dans `UIScreenDesignTimeDataLoader` ; `AlundraGame.json` et
  `AssetInfos.json` ramenés au sha256 de référence en retirant le seul ajout ; manifeste de l'export revérifié
  (23 227 fichiers) ; cycle de vie du présentateur tracé entre `OnEndPlay` et l'`AttachToWorld` suivant (aucun tick
  ni opcode ne touche le présentateur libéré) ; trois mutations reproduites ; recette A/B et éditeur rejoués.
  Remarques :
  - P3 : la cause décrite dans O5 était inexacte (les icônes de docking sont dans le contenu ; c'est le runtime du
    jeu qui ne les charge pas) : O5 corrigé ;
  - P4 : l'identité au pixel à +60 était une coïncidence de cadence : note corrigée ci-dessus.

### ✅ B5 — Clôture

Plan clos, mémoire à jour, rapport final ; merges laissés à l'auteur.

**Commit :** `docs(plan): close the bound screens program`

**Note de clôture (2026-09-24) :**
- Tranches : B1 ✅, B2 🧪, B3 🧪, B4 ✅. B2 et B3 ne restent en 🧪 que pour l'enregistrement d'un écran depuis
  l'éditeur (O4). Côté moteur, toutes les tâches sont faites ; T2.3, T4.3 et T5.1 attendent une vérification
  visuelle ou la réponse à O6 du plan moteur (tableau de `CasaEngineMonogame/ai-agent/README.md`).
- Chaque tranche et chaque tâche moteur à risque a eu son vérificateur frais, CONFIRMED au dernier passage. Un
  défaut introduit par le programme a été trouvé après B3 et corrigé par la tâche moteur T5.2 : les bindings d'un
  écran libéré restaient dans le registre de MGUI (G10).
- Branches `chantier/bound-screens`, rien poussé ni mergé : parent (depuis `main`), moteur (depuis `main`), MGUI
  (depuis `develop`). Le parent référence le moteur `14575337`, qui référence MGUI `0559e6b`. L'ordre des merges
  revient à l'auteur (O2).
- À arbitrer par l'auteur : O2 à O6 ci-dessous, et les points ouverts du plan moteur (O3 allocation des événements
  faibles de WPF, O5 manque G7, O6 enregistrement des écrans dans l'éditeur).

## Suite après les réponses de l'auteur (2026-09-24)

Réponses de l'auteur aux questions de clôture, décisions D13 à D18 du plan moteur (phase 8) : « Save » enregistre
les écrans (D13) ; un dialogue de remplacement incomplet reste affiché sans la partie manquante (D14) ; pas de
fichier de conception pour le dialogue (D15, écart de B4 validé) ; merges MGUI `develop`, moteur `main`, parent
`main` après cette suite, sur feu vert (D16) ; confirmation avant de perdre un écran modifié (D17) ; Ctrl+S (D18).
Plan relu (READY) et approuvé le 2026-09-24, mode AUTO. Le programme est rouvert pour cette suite.

### ✅ B6 — Enregistrer les écrans d'Alundra depuis l'éditeur

**Prérequis :** tâche moteur T4.4 close.

**Étapes :** avec l'éditeur réel et les options d'automatisation de T4.4, pour `HudScreen`, `InventoryScreen` et
`DialogueScreen` : (1) ouvrir puis enregistrer sans modification : le `.xaml` n'est pas écrit ; (2) changer une
propriété simple puis enregistrer : `git diff` du fichier d'écran = exactement cet attribut, le panneau n'est pas
rechargé ; (3) remettre la valeur d'origine puis enregistrer : `git diff` vide. Autour de **chaque** lancement :
manifeste de tout `alundra-project` avant et après, copie octet pour octet des fichiers que « Save » réécrit aussi
(`AlundraGame.json`, `AssetInfos.json`, le monde `FirstWorldLoaded`) et restauration ; tout autre fichier modifié
arrête B6 (remise en état par un export complet en place, jamais par suppression). Mettre à jour le commentaire de
`UI/Screens/DialogueScreen.xaml` pour D14.

**Validation :** manifeste final identique au manifeste initial ; B2, B3 → ✅ ; T5.1 (moteur) → ✅. **Vérificateur
frais** (avec T4.4).

**Commit :** `docs(plan): validate saving Alundra's screens from the editor`

**Note de validation (2026-09-24) :** moteur `99e66ee0` (T4.4, précédé de `96ceb976`), éditeur construit depuis ce
code. Neuf lancements de l'éditeur réel (`--open-asset`, `--set-screen-property`, `--save-project`), chacun encadré
par le script `scratchpad/b6_run.py` : manifeste sha256 de tout `alundra-project` avant et après, copie octet pour
octet des fichiers que « Save » réécrit aussi et des écrans versionnés, restauration, troisième manifeste.
- HUD (`WeaponBoxBackground.Opacity` 0.5 -> 0.6 -> 0.5), inventaire (`BoxWeapon.Stretch` None -> Uniform -> None),
  dialogue (`lblLine.FontSize` 16 -> 18 -> 16) :
  - (1) sans modification : le `.xaml` n'est pas écrit (octets et date inchangés) ; journal `dirty=False` ;
  - (2) une propriété changée : `git diff` du fichier = exactement cette ligne, commentaires, bindings et mise en
    forme intacts ; journal : marque effacée (`dirty=False`) et **même instance de document** 60 frames après
    l'enregistrement (le panneau ne s'est pas rechargé depuis sa propre écriture) ;
  - (3) valeur d'origine remise : `git diff` vide, le fichier est identique au fichier versionné.
- Chaque lancement a réécrit `AlundraGame.json` et le monde `Ship Klark (beginning)-389.world` (écriture existante
  de `SaveCurrentProject`, hors de D13) ; rien d'autre n'a bougé ; tous deux restaurés à chaque fois. Écarts
  consignés : `AlundraGame.json` ne change que par l'ordre des champs (`DialogueScreenAsset` déplacé) ; le monde est
  réécrit par l'écrivain de mondes de l'éditeur avec ses champs par défaut (politiques d'entité, bloc de script),
  2 655 -> 5 446 octets. `AssetInfos.json` n'a pas changé.
- Fin de B6 : `git status` du parent sans changement dans `alundra-project/`, et manifeste final identique à celui
  de l'export de B4 (23 227 fichiers, 0 écart).
- Commentaire de `UI/Screens/DialogueScreen.xaml` mis à jour pour D14 ; `AlundraDialogueScreenAssetTests` 5/5.
- La vérification d'une sortie automatisée avec un écran modifié (T4.5) est faite avec T4.5, pas ici.
- **Vérificateur frais sur T4.4 et B6 (2026-09-24) : CONFIRMED** sur le moteur `99e66ee0` (avec `96ceb976`) et le parent
  `45de7e4`, sans constat P0 à P2 : writer déplacé tel quel (aucun test existant modifié) ; gardes de
  `SaveCurrentProject` et titre sans astérisque constatés ; sondes ajoutées puis retirées, avec le vrai
  `EditorDirtyStateService`, la vraie pile de commandes et le vrai `FileSystemWatcher` (annuler après un
  enregistrement remarque l'écran, le second enregistrement réécrit l'original à l'octet ; une modification externe
  après un enregistrement recharge ; un fichier en lecture seule donne un message sans exception ; deux panneaux
  modifiés sont écrits, un propre garde sa date) ; mutation de `ShouldReload` reproduite ; `CasaEngine.Tests`
  1876/1876 (trois passes), deux solutions, `Alundra.Tests` 1089/1089 ; **les neuf lancements de B6 rejoués** (mêmes
  résultats, aucune alerte, `git status` de `alundra-project` vide). Remarques :
  - P3 : le manifeste final de B6 avait été pris juste avant le commit qui change le commentaire de
    `DialogueScreen.xaml` (D14) ; le fichier sur disque est bien la version commitée, et les lancements du
    vérificateur ont porté sur elle. Référence désormais : `scratchpad/v-b6-manifest.sha256` (état à `45de7e4`) ;
  - P4 (reportée, sans rapport) : `AudioServiceFadeTests.FadingVoices_DoNotAllocateDuringUpdate` a échoué une fois
    sur une passe complète (7 888 octets au lieu de 0), vert seul et aux deux passes suivantes ;
  - P4 (reportée, non reproduite) : si la relecture du fichier juste après une écriture réussie échouait,
    `TrySaveDocument` signalerait un échec et l'écran resterait modifié ; le prochain enregistrement le réécrirait ;
  - P4 (reportée, non reproduite) : `TrySaveDocument` ne rattrape que `IOException` et
    `UnauthorizedAccessException` ; une autre exception du sérialiseur sortirait de `SaveCurrentProject`, que le
    menu appelle sans `try`.

## Points ouverts

| Réf | Sujet |
|---|---|
| O1 | ~~Chemins de binding imbriqués (`Slot0.SourceName`) : à confirmer par un test.~~ **Confirmé en B2** (test de liaison sans affichage, `IconSlot3.SourceName`, `MoneyDigit1.Left`, et un changement du sous-view-model seul suivi). |
| O2 | L'ordre des merges est la décision de l'auteur (plan moteur, O2). |
| O3 | **Observation, à arbitrer.** La première ouverture de l'inventaire dans un monde coûte deux frames longues (150,7 ms puis 80,8 ms, et 152,4 puis 97,1 ms au run du vérificateur) : construction de la fenêtre depuis l'asset, bindings, images et animation. L'horloge logique plafonnant à 4 ticks par frame, ces frames perdent des ticks. Leur effet sur la recette n'est pas isolé : le run de référence tournait environ trois fois plus lentement par frame, ce qui suffit à expliquer le retard du texte aux captures précoces. Pour trancher : refaire la référence (base `221185b`) avec la même cadence et la mesure des frames. Piste si le coût se confirme : construire la fenêtre au câblage de l'écran plutôt qu'à sa première poussée. **Lié, vu en B3 :** les pastilles de magie tournent sur l'horloge de l'UI depuis leur redémarrage, alors que le compteur du directeur perd les ticks des frames plafonnées ; c'est l'explication probable, non isolée, de leur phase différente sur `inv-hud-3` (un cycle décoratif, sans autre effet visible). |
| O4 | ~~Question posée à l'auteur le 2026-09-24 (plan moteur, O6).~~ **Répondue le 2026-09-24 : D13, tâche moteur T4.4 puis B6.** L'éditeur n'enregistre aucun écran ; la validation « enregistrement sans modification » de B2 et B3 attend sa décision. |
| O5 | **Observation (B4), introduite par le programme.** Au premier dialogue, `CasaUIAssetProvider: cannot resolve UI image 'DockClose'` est journalisé. L'icône de fermeture de la barre de titre (`MGCloseIcon`, `MGUI/MGUI.Core/UI/UISymbolElements.cs:460-477`) essaie la texture facultative `DockClose`, et dessine sinon une croix vectorielle. MGUI n'enregistre `DockClose` et les autres icônes de docking que dans `MGDesktop.LoadDefaultResources` (`MGDesktop.cs:1287`), qu'appellent l'éditeur, `MGUI.Editor.Host` et `MGUI.Samples`, mais pas le runtime du jeu ; les icônes sont pourtant dans le contenu (`Content/Icons/docking/`). Depuis T3.1 (moteur `4d6906ae`), un nom inconnu est demandé à l'hôte, qui avertit. La texture manquant, `MGCloseIcon` dessine sa croix vectorielle (d'après son code ; même rendu dans les deux runs) : bruit de journal seulement. Pistes : ne pas demander à l'hôte un nom que MGUI sonde comme facultatif, ou charger ces icônes dans le runtime du jeu. |
| O6 | **Observation (B4), préexistante, hors programme (E12).** Le premier message de la 389 s'affiche « bonne mine2222 » : `AlundraDialogueTextParser` ne traite que `\A`, `\N` et les codes numériques ; un code inconnu (`\W`, `\T`) est sauté sur deux caractères, mais le paramètre de `\W2` reste dans le texte (« 2 »). Identique avec le balisage embarqué. |

## Hors périmètre

- Les glissements d'ouverture et les défilements d'argent et de PV : ils restent calculés par les directeurs.
- Le rafraîchissement de `data-extracted/` depuis le remaster, dont l'extraction du 2026-09-19 a régressé.
- La recette D6 d'E13.d et les vérifications visuelles 🧪 du chantier des handles, qui restent à l'auteur.
