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

### 🧪 B2 — L'inventaire en asset lié

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

### 🧪 B3 — Le HUD en XAML

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
  - Captures de l'inventaire : `inv-a` à `inv-d` identiques ; `inv-389` diffère de 4 720 pixels sur toute l'image
    (animation du monde), le même genre d'écart qu'entre deux runs du HUD en C# (`run-b2` contre `run-b2-times` :
    6 720 pixels sur `inv-hud-1`).
  - Le run précédent (`run-b3`) montrait l'argent un tick en avance aux deux captures où il défile (1010 et 2150
    contre 1000 et 2140) ; le run avec le log montre l'argent du directeur à 1000 puis 2140 et les mêmes chiffres à
    l'écran. C'est la cadence, pas le rendu : le harnais ne fixe pas le nombre de ticks à une frame donnée. La
    prédiction 1 (« l'argent aux mêmes valeurs ») tient pour le run retenu seulement.
  - Aucune ligne au-dessus d'Info, aucune exception ; `HudScreen.uiscreen` chargé une seule fois.
- Éditeur (automatisation, projet Alundra) : `HudScreen.uiscreen` s'ouvre sans erreur (« Loaded HudScreen.xaml »),
  l'aperçu montre les fonds en dégradé, les petits cœurs, « /10 », « 0000 » et la pièce des données de conception,
  la hiérarchie liste les rôles.
- **Reste en 🧪** : l'enregistrement sans modification, comme B2 (O4).

### ⏳ B4 — La boîte de dialogue en asset

**Prérequis :** tâche moteur T6.1 close.

**Étapes :** écrire `UI/Screens/DialogueScreen.*` à partir du XAML par défaut du moteur, avec son fichier de
conception ; renseigner le champ de réglage du projet qui le désigne (écrit par le convertisseur dans
`AlundraGame.json`).

**Validation :** recette : un dialogue de la 389 s'affiche comme avant (capture comparée) ; l'écran s'ouvre et se
prévisualise dans l'éditeur. **Vérificateur frais.**

**Commit :** `feat(dialogue): ship Alundra's dialogue screen as a project asset`

### ⏳ B5 — Clôture

Plan clos, mémoire à jour, rapport final ; merges laissés à l'auteur.

**Commit :** `docs(plan): close the bound screens program`

## Points ouverts

| Réf | Sujet |
|---|---|
| O1 | ~~Chemins de binding imbriqués (`Slot0.SourceName`) : à confirmer par un test.~~ **Confirmé en B2** (test de liaison sans affichage, `IconSlot3.SourceName`, `MoneyDigit1.Left`, et un changement du sous-view-model seul suivi). |
| O2 | L'ordre des merges est la décision de l'auteur (plan moteur, O2). |
| O3 | **Observation, à arbitrer.** La première ouverture de l'inventaire dans un monde coûte deux frames longues (150,7 ms puis 80,8 ms, et 152,4 puis 97,1 ms au run du vérificateur) : construction de la fenêtre depuis l'asset, bindings, images et animation. L'horloge logique plafonnant à 4 ticks par frame, ces frames perdent des ticks. Leur effet sur la recette n'est pas isolé : le run de référence tournait environ trois fois plus lentement par frame, ce qui suffit à expliquer le retard du texte aux captures précoces. Pour trancher : refaire la référence (base `221185b`) avec la même cadence et la mesure des frames. Piste si le coût se confirme : construire la fenêtre au câblage de l'écran plutôt qu'à sa première poussée. **Lié, vu en B3 :** les pastilles de magie tournent sur l'horloge de l'UI depuis leur redémarrage, alors que le compteur du directeur perd les ticks des frames plafonnées ; c'est l'explication probable, non isolée, de leur phase différente sur `inv-hud-3` (un cycle décoratif, sans autre effet visible). |
| O4 | **Question posée à l'auteur le 2026-09-24 (plan moteur, O6).** L'éditeur n'enregistre aucun écran ; la validation « enregistrement sans modification » de B2 et B3 attend sa décision. |

## Hors périmètre

- Les glissements d'ouverture et les défilements d'argent et de PV : ils restent calculés par les directeurs.
- Le rafraîchissement de `data-extracted/` depuis le remaster, dont l'extraction du 2026-09-19 a régressé.
- La recette D6 d'E13.d et les vérifications visuelles 🧪 du chantier des handles, qui restent à l'auteur.
