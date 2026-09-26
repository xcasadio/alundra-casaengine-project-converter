# Plan — Le portrait d'ouverture de l'inventaire (report de D-E13D-12)

**État** : 🚧 **approuvé par l'auteur le 2026-09-26, P1 à P5 compris, en mode AUTO** (« oui AUTO ») : travail
réversible dans le périmètre écrit ici, un commit par tranche sur les branches du chantier, ni commit sur `main` ou
`develop`, ni push, ni fusion, ni action externe hors de celles que D4 autorise. Rédigé le 2026-09-26, relu **REVISE**
puis révisé, relu **READY** par un second relecteur frais (§7).
**Naissance** : demande de l'auteur du 2026-09-26 (« on fait le portrait maintenant »). Le portrait avait été
**reporté** le 2026-09-21 : D-E13D-12 amendée, tranches D3.c (extraction) et D5.p (affichage) retirées
(`docs/plan-e13d-inventaire.md`, §3 et §6 point 6). E13.d est close sans lui ; ce plan reprend ce report.
**Dépend de** : E13.d close (inventaire principal, sous-inventaire, bascule L1/R1 ;
`docs/plan-e13d-inventaire.md`, `docs/plan-e13d-sous-inventaire.md`) ; les écrans liés
(`docs/plan-bound-screens.md`, ADR-0002 du dépôt) ; le système d'animation de MGUI (ADR-0006 de MGUI).
**Branches** (créées le 2026-09-26 à l'approbation) : parent `chantier/portrait-inventaire` depuis `main`
`3b47367` ; analyseur `chantier/portrait-inventaire` depuis `master` `d1c9de7` ; MGUI
`chantier/render-transform-bindings` depuis `develop` `ac60978` ; moteur `chantier/portrait-inventaire` depuis `main`
`a318530c` (pointeur de MGUI et fiche G9 de l'audit). Le moteur enregistre MGUI `a8a57dd` alors que le checkout de MGUI
est sur `develop` `ac60978` (état de l'auteur, laissé tel quel). Travail dans le checkout principal. Le fichier
`CasaEngineMonogame/CasaEngine.Launcher/Program.cs` de l'auteur n'est jamais ouvert, touché ni indexé.

---

## 1. Les faits établis

Reconnaissance en lecture seule du 2026-09-26 (sept agents, puis vérifications en session principale). **[binaire]** :
vérifié dans l'exécutable France `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE`
(texte chargé en `0x80020000`, décalage de fichier `0x800`), dont les adresses sont celles des commentaires de la
décompilation (`alundra-datas-analyser/AlundraTools/AlundraEngine`). **[décompilation]** : lu dans la décompilation
seulement. Les faits [binaire] du §1.1 sont **contre-vérifiés par un agent indépendant** en PI1 avant toute tranche de
la DLL.

### 1.1 Le portrait dans l'original [binaire]

- **Un système partagé.** Le portrait est une instance d'un système « portrait de personnage » à un seul quad, que les
  portraits des dialogues utilisent aussi. Bloc d'état `0x80180070..0x80180100` : l'état (`short`, +0), deux
  `POLY_FT4` en double tampon, cinq **pointeurs** vers les positions du joueur et de la caméra, la cible, la position
  courante, l'écart, le pas, la taille (48×56 en `+0x84`/`+0x88` ; la première rédaction disait « demi-taille »,
  corrigé par PI1), le repos. Point d'entrée de l'inventaire `0x80057c18` : repos
  `(0xf8, 0x68)` = **(248, 104)**, taille **48×56** en dur. Point d'entrée des dialogues `0x80057c84` (repos (8, 116),
  appelé de `0x8003d634`, `0x8003f0f8`, `0x80041e84`) : **hors périmètre** (E12.c, §6).
- **Démarrage à l'ouverture de l'inventaire principal.** `DisplayInventory` (`0x80055570`) appelle, dans l'ordre :
  `InitializeHudPosition` (`0x8004bd9c`), `SetTransitionType(6)`, `GetAnimationImageByIndex(0)`, **le départ du
  portrait** (`0x80057c18`, appel en `0x800556b0`), puis `DisplayIconNames` (`0x80055c84`) et le son 4 (`0x800490fc`).
- **Démarrage à l'ouverture du sous-inventaire.** Le post-traitement (état 1, `0x800481a8`) appelle `StartFadeOut`
  (`0x80052618`), qui ouvre en réalité le sous-inventaire : `InitializeHudPosition`, `SetTransitionType(4)`,
  `GetAnimationImageByIndex(0)`, **le même départ** (`0x800526ac`), puis le son 4. Retour du sous-inventaire vers le
  principal : le post-traitement (état 2, `0x800481d4`) appelle `DisplayInventory`, qui relance le portrait.
- **Garde et valeurs initiales** (`0x80057cf0`). Le départ n'a lieu **que si l'état vaut 0** (`bnez` en
  `0x80057d30`). Il pose état = 5, pas = 15, position courante = repos (248, 104), taille 48×56, et la **cible = le point
  de la tête du joueur à l'écran** : `(PosX_hi − scrollX, PosY_hi − scrollY − PosZ_hi − 32)`, où `PosX/Y/Z` est la
  position 16.16 du joueur (`0x80127e44/48/4c`, partie entière lue par `lh 2(ptr)`) et `scrollX/Y` =
  `g_cameraScrollingX/Y` (`0x800e4328`/`0x800e432c`, `StaticVariables.cs:12859-12860`).
- **Vol d'ouverture (état 5)**, un pas par image, `s` = 15 … 1 :
  `X = 248 + trunc(s·(tx − 248)/15)`, `Y = 104 + trunc(s·(ty − 104)/15)`, `W = trunc(48·(15 − s)/15)`,
  `H = trunc(56·(15 − s)/15)` ; divisions signées tronquées vers zéro (multiplication magique `0x88888889`). À `s = 0`,
  l'état passe à 4 (repos). Suite des tailles par appel : 1 : 0×0 · 2 : 3×3 · 3 : 6×7 · 4 : 9×11 · 5 : 12×14 · 6 : 16×18 ·
  7 : 19×22 · 8 : 22×26 · 9 : 25×29 · 10 : 28×33 · 11 : 32×37 · 12 : 35×41 · 13 : 38×44 · 14 : 41×48 · 15 : 44×52 · puis
  48×56 (`0x80057f24-0x80057ffc`, `0x80058070-0x8005812c`).
- **Au repos (état 4), le portrait reste affiché** en 48×56 en (248, 104)–(296, 160) **tant que l'inventaire principal
  ou le sous-inventaire est ouvert** (`0x80057eec-0x80057f20`, garde de dessin `0x8005817c-0x800581e4`). La description
  du plan d'E13.d (« grandit puis se résorbe à l'ouverture », `docs/plan-e13d-inventaire.md:215`, `:671`) était
  **inexacte**.
- **Vol de retour (état 2).** `UpdateHudTransitionState` (`0x80057b84`, qui ne touche **que** le bloc du portrait,
  malgré son nom) ne s'exécute que si l'état est non nul ; elle **relit** les positions du joueur et de la caméra au
  moment de la sortie, pose position courante = point de la tête, cible = repos, état = 2, pas = 15. Pour `s` = 15 … 1 :
  `X = tête + trunc(s·(repos − tête)/15)` (à `s = 15`, au repos), `W = trunc(48·s/15)`, `H = trunc(56·s/15)` : 48×56,
  44×52, … 6×7, 3×3 ; au 16ᵉ appel l'état revient à 0 (quad 0×0, puis plus rien).
- **Appelée à chaque sortie.** Fermeture par le masque `0x813` (Start|L2|R2|Triangle) : principal `0x80056924` →
  glissement de sortie `0x800556dc`, puis `0x80056938` (retour du portrait), puis `InitializeHudPositionBeforeHide` ;
  sous-inventaire `0x80053634` → `0x800526cc`, puis `0x80053648`, puis `0x80053650`. Bascule L1/R1 : principal
  `0x80056950` → `0x800556dc`, puis `0x80056974` (retour), post-traitement = 1 ; sous-inventaire `0x80053660` →
  `0x800526cc`, puis `0x80053684`, post-traitement = 2. `0x800556dc` et `0x800526cc` n'ont pas d'autre appelant.
- **L'horloge** (précisée par PI1). Chaque itération de la boucle principale exécute `RenderScene` (`0x8002c3fc`)
  **avant** `Update` (`0x8002c404`). `RenderScene` lance d'abord les rappels et le post-traitement (`0x80048054`,
  appelée en `0x8002be5c` : le rappel du principal `0x80056598`, celui du sous-inventaire `0x80053328`, puis le
  post-traitement), **puis** `DisplayUserInterface` (`0x80044c5c`, appelée en `0x8002be64`) →
  `DisplayInventoryCharacterPortrait` (`0x80058134`) : si l'état est non nul, un pas (`0x80057ebc`) puis l'ajout du quad.
  Deux cas donc :
  - **l'ouverture principale par le déclenchement** est appelée par `Update` (`0x8002bcec`) : un départ à l'image N
    fait son premier pas (0×0) dans le rendu de N+1, croissance visible dès N+2, repos dès N+16 ;
  - **les deux ouvertures par le post-traitement** (`0x800481a8`, `0x800481d4`) **et les quatre retours** (rappels
    `0x80056598`, `0x80053328`) s'exécutent dans `RenderScene`, avant `DisplayUserInterface` : leur premier pas tombe
    dans **la même image**.

  Un retour lancé à l'image M revient à l'état 0 dans le dessin de M+15 ; le départ suivant, exécuté lui aussi avant le
  dessin, ne trouve l'état à 0 que s'il a lieu à M+16 ou plus tard. Le glissement de sortie dure ~18 images d'après la
  décompilation (non revérifié dans le binaire par PI1) : marge d'environ deux images, testée en PI8.
- **Géométrie.** Ancre = **coin haut-gauche** : sommets (X, Y), (X+W, Y), (X, Y+H), (X+W, Y+H) ; UV fixes
  (u, v)–(u+48, v+56) : l'image entière est mise à l'échelle, jamais retournée. Au repos, elle est affichée 1:1.
- **Couleur.** Quad texturé **non semi-transparent** (`SetPolyFT4`, code `0x2c`), modulé par la couleur du sommet :
  255 → 128 à l'ouverture, 127 → 246 au retour (éclaircissement jusqu'à ~2×). **Écarté par l'auteur** (D2 : teinte
  normale en permanence). Transparence = texel d'index 0 (palette 16, entrée 0 = `0x0000`).
- **Ordre de dessin.** Table d'ordre de l'interface (10 entrées, `0x80146f58`), dessinée après le monde, entrée 0 en
  premier : 0 = les boîtes de l'inventaire et la jauge du HUD ; 1 = la description ; 2 = les noms ; **3 = le
  portrait** ; 4 = les icônes des objets et du HUD ; 5 = les chiffres et le curseur. Le portrait passe donc
  **au-dessus** des boîtes et des textes, **sous** les icônes, les chiffres et le curseur. Au repos, son rectangle ne
  recouvre aucun autre élément (`AlundraTools/AlundraTools/UiBoxes.csv`) : l'ordre ne se voit que pendant les vols.
- **Aucun son** propre au portrait (les seuls `jal` de `0x80057b40-0x80058200` visent `0x80057cf0`, `0x800843b0`,
  `0x800859c8`, `0x80085a70`, `0x80057ebc`) ; les sons 4 et 5 appartiennent aux menus et sont déjà portés.
- **Écarts décompilation ↔ binaire.** (1) La décompilation ajoute **2 pixels en X** (`MainInventoryManager.cs:283-284`,
  `:407`) : dans le binaire, le « +2 » est le décalage de `lh 2(ptr)` qui lit la partie entière ; **le portage ne
  l'ajoute pas**. (2) La décompilation mémorise des valeurs à l'ouverture, le binaire des pointeurs relus à la sortie :
  équivalent tant que le monde est gelé (`MenuOpen`), ce qui est le cas pendant tout le menu (D-E13D-15).
- **Artefacts sans effet** : premier appel d'ouverture et dernier appel de retour en 0×0 ; la garde « état = 0 »
  ignorerait un départ demandé pendant un retour, ce que les durées des menus ne produisent jamais. Aucun défaut de
  l'original à corriger.

### 1.2 L'image

Enregistrement de sprite **0** de la banque globale, image « portrait » lue par `GetAnimationImageByIndex(0)`
(`0x80057b40`, première image du bloc de frames, comme `SpriteRecord.GetPortraitImageset`,
`AlundraEngine/DatasBin/SpriteRecord.cs:43-52`) : page 2, palette 16, source (200, 56), **48×56**, 4 bpp, signature
`61779762221058` (`docs/plan-e13d-inventaire.md` §7 D0.9). Décodage indépendant en lecture seule : 565 texels
transparents sur 2688 ; un buste aux cheveux blonds et aux vêtements bleu-gris, cohérent avec Alundra
(identification visuelle à confirmer par l'auteur, PI10).

### 1.3 L'extraction et l'export aujourd'hui

- **L'atlas ne contient que les images des animations.** `GameMapHelper.EnumerateImages`
  (`AlundraDataExtractor/GameMapHelper.cs:229-269`) parcourt enregistrements → jeux d'animation → animations → frames →
  images, jamais `GetPortraitImageset`. `SaveSpriteSheet` (`:109-156`) déduplique sur la signature et pose
  `AtlasX/AtlasY` sur chaque `SiImage` ; la disposition `Original` place une image en
  `(SourceX, page·256 + SourceY)`, ici (200, 568), zone **vide** de la planche exportée (0 pixel opaque sur 2688,
  D0.9). Les 88 icônes d'objets d'E13.c n'avaient demandé aucune extraction : leurs signatures figuraient déjà dans
  des animations (`docs/plan-e13c-icones-hud.md` S1.a).
- **La carte globale** (`datasBin.AlundraGameMap`, `AlundraDataExtractor/Program.cs:1365`) est écrite par
  `SaveAlundraMap` (`Program.cs:1386-1396`) : `SaveSpriteSheet(gameMap, "map_alundra_spritesheet.png")` **puis** la
  sérialisation complète du `GameMap` dans `map_alundra.json`. **Toutes** les cartes numérotées passent par la même
  sérialisation (`Program.cs:1409`), avec les mêmes options (`IncludeFields = true`, **sans** condition d'omission des
  valeurs nulles, `Program.cs:17`) : un champ public ajouté au `GameMap` (`AlundraEngine/DatasBin/GameMap.cs:7-25`,
  classe à champs publics dont le nullable `ScrollScreen?` est déjà écrit) apparaîtrait dans **chaque** `map_<n>.json`,
  même nul. `SpriteRecord` n'a **aucun** champ portrait (`SpriteRecord.cs:5-9`) : un portrait ajouté seulement à
  l'atlas n'apparaîtrait pas dans le JSON.
- **Le convertisseur** ne lit que les quads des frames d'animation (`Readers/SpriteBankReader.cs:259-325`, `:455-509`,
  signature lue en `:548`) ; un `.sprite` par signature, nommé `sprite_<signature>`, d'identifiant
  `SpriteWriter.SpriteAssetId(planche, signature) = Ids.For("sprite:<planche>:<signature>")`
  (`Writers/SpriteWriter.cs:820-857`). Les icônes d'objets passent par `ItemPortrait.csv` → `ItemsWriter`
  (`Writers/ItemsWriter.cs:141-189`) → `Data/item-icon-index.json` → `AlundraItemTables.TryGetIconAssetId` dans la DLL
  (`Alundra/Scripts/AlundraItemTables.cs:23-38`). **Un portrait sans frame d'animation demande donc une modification
  du convertisseur** (PI5).
- **Ligne de commande de l'extracteur** : `AlundraDataExtractor <gamePath> <extractionPath> [--tiled-tileset-layout
  original|compact] [--spritesheet-layout original|compact]` (`Program.cs:119`).

### 1.4 Le texte non décodé du remaster (cause probable, prouvée en PI2)

- Le remaster `D:/development/repo/Alundra Remake/remaster-data-extracted` a été ré-extrait le **2026-09-19** : ses
  `map_*.json` ont le texte **non décodé** (`o}i` pour « où », par exemple `map_475.json`) ; la copie du dépôt
  `data-extracted/` (2026-09-02) est la bonne (mémoire du dépôt, 2026-09-24 ; `docs/plan-bound-screens.md:26-31`).
- Le correctif du décodage est le commit de l'analyseur `a8598f4` (2026-09-02, « complete the escape-pair decode
  table »). Le sous-module `alundra-datas-analyser` le contient (`master` `d1c9de7`) ; **l'autre checkout de
  l'analyseur**, `D:/development/repo/alundra-datas-analyser`, est resté à `926dcb8` (2026-08-30) et **ne le contient
  pas** (`git merge-base --is-ancestor`, 2026-09-26).
- Les `FileName` de `data/BALANCE.BIN.json` diffèrent aussi (barres obliques dans `data-extracted/`, contre-obliques
  dans le remaster) : les deux extractions ne viennent pas du même build.
- **Hypothèse** : l'extraction du 2026-09-19 a été lancée depuis l'ancien checkout. **Conséquence** : une extraction
  complète faite avec l'extracteur **du sous-module** doit redonner le texte décodé. PI2 le prouve avant toute
  modification.

### 1.5 Côté MGUI : la transformation de rendu n'est pas liable (manque G9)

- `UIRenderTransform` (`MGUI.Core/UI/Animation/UIRenderTransform.cs:15-96`) porte `Translation`, `Scale` (`Vector2`),
  `Rotation`, `Origin` ; ses setters notifient sans allocation ; elle est **rendu seul** : elle ne relance jamais la mise
  en page (ADR-0006 de MGUI).
- Le XAML la déclare par un DTO `RenderTransform` (`MGUI.Core/UI/XAML/Animation.cs:238-291`, propriétés **chaînes**
  analysées par `AnimationXamlParser.ParseVector2`, `:273-288`, `:403`) appliqué une fois au chargement
  (`Element.cs:822-824`). Ce DTO n'est pas `XAMLBindableBase`, et **un objet imbriqué ne reçoit des bindings que si son
  objet d'exécution est lui-même `XAMLBindableBase`** (`Element.cs:895-921`, `:1084-1098`) : `UIRenderTransform`
  (`sealed`, `INotifyPropertyChanged` seulement) ne l'est pas. La voie des pinceaux imbriqués ne s'applique donc pas
  telle quelle.
- **La voie qui existe déjà** : un binding d'élément peut viser une **propriété imbriquée** de l'élément. Le chemin
  cible est parcouru depuis l'élément et la dernière étape est la propriété écrite (`DataBinding.cs:336-338`,
  `ResolvePath`) ; `BindingPathMappings` renomme un attribut XAML en chemin imbriqué (`Element.cs:1018-1028`, par
  exemple `Background` → `BackgroundBrush.NormalValue`) ; `MGBinding` enregistre le binding sur le DTO sous le nom de
  l'attribut (`MGBinding.cs:92-108`). `MGElement.RenderTransform` est une instance **stable**, allouée au premier accès
  et jamais remplacée (`MGElement.cs:4922-4940`). Quand la source et la cible ont le même type, la poussée passe par
  une copie typée compilée (`DataBinding.cs:507-511`, `TypedAccessorCache.GetOrBuildCopy`,
  `TypedAccessorCache.cs:104-124`) : ni boxing ni allocation. Le DTO `Element` a déjà un attribut `RenderScale`
  (`float?`, `Element.cs:292`, l'échelle d'état) : les nouveaux noms doivent l'éviter.
- Fiche du manque : `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md` §G9 (consignée le 2026-09-24,
  non corrigée). Le HUD d'Alundra contourne aujourd'hui ce manque par une glue C# (`AlundraHudScreen`) : **hors
  périmètre** (§6).
- `Width`/`Height` sont liables mais relancent la mise en page ; écarté par l'auteur (D3).

### 1.6 Côté portage

- Les deux directeurs : `AlundraInventoryDirector` (ouverture `RunDisplayInventory` `:422`, `RunDisplayInventorySetup`
  `:501`, chemin du post-traitement `RunDisplayInventoryHeadFromPostProcess` `:486`, bascule L1/R1 `:650-659`) ;
  `AlundraSubInventoryDirector` (ouverture `OpenFromPostProcess` `:415-422`, port de `StartFadeOut` **sans** le
  portrait ; bascule `:524-525` ; fermeture `RunCloseSetup` `:583`). La doc de classe du directeur principal consigne le
  portrait comme non porté (`AlundraInventoryDirector.cs:41-51`, `:469-470`).
- L'ordre par tick : `AlundraWorldProxy.cs:2000-2016` exécute les directeurs (`AlundraInventoryDirector.Instance.Tick`,
  `AlundraSubInventoryDirector.Instance.Tick`) puis les présentateurs, qui poussent le view-model ; MGUI dessine ensuite.
- **Positions.** Le joueur : `PosX/PosY/PosZ` en 16.16 (`AlundraEntityScriptProxy`, champs `:134-136`). La caméra : le
  seul passage autorisé de la cible de la caméra du rendu vers `g_cameraScrollingX/Y` est
  `AlundraCameraMath.ToOriginalScrollSpace` (`Alundra/Scripts/AlundraCameraMath.cs:295-309`, « every caller must go
  through this method »), déjà utilisé par les fonds (`AlundraBackdropStage.cs:437-441`).
- **Les écrans** sont des assets versionnés : `alundra-project/UI/Screens/InventoryScreen.xaml`,
  `SubInventoryScreen.xaml` (et leurs `.uiscreen`, `.design.json`). Le canevas racine porte l'échelle entière de la
  fenêtre (`AlundraInventoryScreen.cs:114-127`) ; les éléments sont placés en pixels natifs 320×240.
- Suites de référence à relever au début de l'exécution (PI2) : `Alundra.Tests`, convertisseur, `MGUI.Tests`,
  `CasaEngine.Tests` (dernier relevé, 2026-09-26 : 1285, 194, 3090, 1957).

---

## 2. Décisions

### 2.1 Tranchées par l'auteur le 2026-09-26

| Réf | Décision |
|---|---|
| D1 | **Fidèle au binaire** : vol d'aller depuis la tête d'Alundra à chaque ouverture (principal et sous-inventaire), portrait fixe en 48×56 en (248, 104) tant que le menu est ouvert, vol de retour vers la tête à chaque fermeture et à chaque bascule L1/R1. |
| D2 | **La teinte reste normale en permanence** : pas d'éclaircissement pendant les vols. |
| D3 | **Corriger le manque G9 dans MGUI** : la transformation de rendu d'un élément (translation et échelle) devient liable depuis le XAML ; pas de liaison de `Width`/`Height`. Le HUD n'est pas modifié dans ce chantier. |
| D4 | **Ré-extraction complète** autorisée, ce qui écrit hors du dépôt (remaster, `data-extracted/`, export) ; le problème du texte non décodé est réglé d'abord. |

### 2.2 Reprises des plans précédents

D-E13D-4 (un manque de MGUI ou du moteur se consigne, jamais de contournement) ; D-E13D-15 (monde gelé sur tout
`MenuOpen`) ; D-E13C-3 (disposition `Original` de l'atlas, aucun rectangle ne recoupe sur sa page une signature de
palette différente) ; la règle de l'auteur du 2026-09-25 (un défaut avéré de l'original se corrige) ; régime de
preuve de l'export : manifeste avant/après, double export ⊆ `{report.json}`, jamais de suppression manuelle
d'`alundra-project/`.

### 2.3 Proposées, à valider à l'approbation

Decisions: see ADR-0005 (P2) of this repository and ADR-0020 of MGUI (P1).

| Réf | Proposition | Raison |
|---|---|---|
| P1 | **API MGUI** : deux nouveaux attributs XAML de l'élément, `RenderTransformTranslation` et `RenderTransformScale`, de type chaîne au DTO (littéral `"x,y"` analysé par `AnimationXamlParser.ParseVector2` et appliqué à `MGElement.RenderTransform` après le DTO `RenderTransform`). Liés, ils sont renommés par `BindingPathMappings` en `RenderTransform.Translation` et `RenderTransform.Scale`. **Cible d'exécution** : l'instance `UIRenderTransform` de l'élément, atteinte par le chemin imbriqué (aucun changement de hiérarchie de type, `UIRenderTransform` reste `sealed`). **Type de valeur** : `Vector2` côté view-model comme côté cible, donc copie typée sans conversion. Le DTO `RenderTransform` et l'attribut `RenderScale` ne changent pas. ADR de MGUI. | La voie des objets imbriqués liables exigerait que `UIRenderTransform` dérive de `XAMLBindableBase` (§1.5) ; celle-ci réutilise le chemin cible imbriqué qui existe déjà et pousse sans allocation (ADR-0016 de MGUI). |
| P2 | **Chemin des données** : l'extracteur ajoute au `GameMap` un champ `InventoryPortrait` (le `SiImage` de l'enregistrement 0), rempli **pour la carte globale seulement** et marqué `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` : il est écrit dans `map_alundra.json` et **omis** de chaque `map_<n>.json`, qui reste identique à l'octet. Le portrait entre aussi dans l'atlas. Le convertisseur en fait un `.sprite` par le chemin existant et écrit `Data/inventory-portrait.json` (l'identifiant du sprite), que la DLL lit comme `item-icon-index.json`. ADR du dépôt parent, qui consigne aussi l'omission des valeurs nulles. | Changement minimal ; aucune autre carte ni aucun autre portrait ne bouge ; même contrat que les icônes d'objets. |
| P3 | **Un seul état de portrait**, partagé par les deux directeurs, comme le bloc unique de l'original. | Un départ du sous-inventaire doit voir l'état laissé par le retour du principal. |
| P4 | **Révisée après PI1.** Le pas du portrait se fait **une fois par tick, juste après `AlundraInventoryPostProcess.Run` et avant les présentateurs**, comme `DisplayUserInterface` après les rappels et le post-traitement dans `RenderScene`. Le tick du portage est « moitié Update, puis moitié rendu » (`AlundraWorldProxy.cs:1991-1999`) ; les présentateurs poussent la valeur du pas. *(Première rédaction : « au début du tick, avant les directeurs », ce qui aurait retardé d'une image les retours et l'ouverture du sous-inventaire.)* | Ouverture principale déclenchée au tick T : 0×0 au tick T, visible dès T+1, repos dès T+15, comme N+1, N+2, N+16 de l'original. Retours et ouvertures par le post-traitement : premier pas dans leur propre tick, comme dans l'original. |
| P5 | **Rien n'est supprimé hors du dépôt** : l'ancien remaster est renommé `remaster-data-extracted.bak-2026-09-19` ; `data-extracted/` et `alundra-project/` sont sauvegardés avant d'être réécrits ; les dossiers d'extraction restent en place (§5.2). | Retour arrière possible à chaque étape. |

---

## 3. Tranches

Un commit par tranche, avec la mise à jour de ce plan ; message en anglais. **Chaque tranche ne commence que lorsque
ses prérequis sont clos.** Vérificateur frais aux frontières à risque (PI3, PI6, PI9). Statuts : ⏳ à faire · 🚧 en
cours · 🧪 à tester · ✅ fait · ⚠️ bloqué.

### ✅ PI0 — Les mesures (lecture seule) — close à la rédaction

Tout le §1. Scripts de mesure de la reconnaissance dans le scratchpad de la session (`portrait-discovery/`) ; celui
de la contre-vérification (PI1) sera recopié au §7.

### ✅ PI1 — Contre-vérification indépendante des faits [binaire] (lecture seule) — faite le 2026-09-26

**Prérequis** : approbation. **Aucun dépôt modifié** (sauf ce plan).
- Un agent **qui n'a pas produit le §1.1** relit `ALUN_CD.EXE` avec son propre script (capstone) et retrouve, avec
  les adresses : les quatre départs et les quatre retours ; la garde « état = 0 » ; le repos (248, 104) et le 48×56 ; la
  formule du point de la tête (dont le `− 32` et les deux variables de défilement) ; les deux tables de tailles
  (ouverture et retour, 16 appels chacune) et les positions pour un écart positif et un écart négatif ; l'ancre
  haut-gauche ; l'absence de semi-transparence ; l'entrée 3 de la table d'ordre ; l'absence du « +2 » en X ; l'ordre
  `RenderScene` avant `Update`.
- Son script est recopié au §7.

**Acceptation** : chaque fait retrouvé, ou l'écart écrit. **Arrêt** : un écart qui change D1 ou une tranche → le plan
est corrigé et relu avant PI7. **Budget et retour** : §5.1, §5.2 (aucune écriture hors de ce plan). Commit : `docs(plan): cross-check the inventory portrait against the executable`.

**Fait le 2026-09-26.** Agent indépendant (sans accès aux scripts de la reconnaissance).
- **Méthode.** Il exécute le code MIPS d'origine (départ, pas, retour, `DisplayUserInterface`, `ClearOTag`) dans un
  petit interpréteur R3000, compare le résultat aux formules du plan, et décode appels, immédiats et encodages.
- **Résultat : 50 vérifications, 0 échec.** Script recopié au §7 ; la session principale l'a relancé et retrouve une
  sortie identique. Tout le §1.1 est confirmé : les quatre départs et les quatre retours et leur ordre, la garde, les
  valeurs initiales, la formule du point de la tête sans « +2 », les deux tables de 16 appels pour un écart positif
  et négatif (troncature vers zéro), l'ancre, les UV fixes, le code `0x2c`, l'entrée 3 de la table d'ordre (parcours
  exécuté : OT[0] > OT[1] > OT[2] > OT[3] > portrait > OT[4] > OT[5]), aucun son.
- **Deux écarts, corrigés dans ce plan** :
  1. libellé « demi-taille » (§1.1) : les champs `+0x84`/`+0x88` portent la taille entière ;
  2. horloge incomplète : seuls les départs appelés par `Update` font leur premier pas à l'image suivante ; ceux du
     post-traitement et les quatre retours le font dans la même image (§1.1). **P4 est révisée en conséquence** et PI8
     ajustée. La tranche PI8 révisée repasse devant un relecteur frais avant son exécution. PI7 (logique pure) n'en
     dépend pas.
- **Remarques hors périmètre** :
  - `0x80052618` a un second appelant, `0x800555fc`, une branche de débogage de `DisplayInventory` normalement morte ;
  - `0x80057b84` a un cinquième appelant, `0x80045f08`, dans les dialogues (E12.c).

### ✅ PI2 — Analyseur : la cause du texte non décodé et l'extraction de référence (écrit hors du dépôt, dossier neuf) — faite le 2026-09-26

**Prérequis** : approbation (D4). **Aucun code modifié.**
1. Relever les suites de référence (`Alundra.Tests`, convertisseur, `MGUI.Tests`, `CasaEngine.Tests`) et les têtes
   des quatre dépôts ; créer les branches du chantier.
2. Construire l'extracteur **du sous-module** (tête de `master`) et lancer une extraction complète, disposition
   `Original` (celle de l'atlas actuel), vers un **dossier neuf**
   `D:/development/repo/Alundra Remake/extraction-reference-2026-09-26/`. Rien d'existant n'est écrasé.
3. Comparer ce dossier à `data-extracted/` (`diff -rq`, puis contenu de chaque fichier qui diffère). **Prédiction
   écrite avant** : identique, sauf les fichiers qu'expliquent les commits de l'analyseur postérieurs à l'extraction du
   2026-09-02 qui touchent `AlundraDataExtractor` ou `AlundraEngine`
   (`git log a8598f4..HEAD -- AlundraTools/AlundraDataExtractor AlundraTools/AlundraEngine`), chacun nommé avec son
   fichier attendu.
4. Comparer aussi au remaster : le texte décodé ici et non décodé là-bas confirme l'hypothèse du §1.4.

**Acceptation** : l'écart mesuré ⊆ l'écart prédit, texte décodé partout (recherche de `o}i` et `s}kr` : 0), cause
écrite. **Arrêt** : un écart inexpliqué → ⚠️, question à l'auteur. **Budget et retour** : §5.1, §5.2 (dossier neuf,
rien d'écrasé). Commit (parent) :
`docs(plan): establish the reference extraction and the cause of the undecoded remaster text`.

**Prédiction, écrite le 2026-09-26 avant l'extraction.**
- Commits de l'analyseur postérieurs à `a8598f4` qui touchent l'extracteur ou `AlundraEngine` : `6176ea3` (sonde
  `--probe-portraits`, sans effet sur l'extraction), `18a8546` et `e495d7f` (moteur de son à l'exécution), `8348d7f`
  (lecture de l'opcode `0xBF` à l'exécution), `b79b45a` (attributs VAB de `sound/sfx.json`, déjà recopiés dans
  `data-extracted/` par T3.1 du plan audio le 2026-09-25). **Aucun ne change la sortie de l'extraction face à
  `data-extracted/`.**
- Le `FileName` de `data/BALANCE.BIN.json` recopie le chemin du jeu tel qu'il est passé à l'extracteur ;
  `data-extracted/` porte `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted\\DATA\\…`
  (barres obliques) : l'extraction est lancée avec ce chemin écrit ainsi, sans le profil de lancement (qui l'écrit avec
  des contre-obliques et vise directement le remaster).
- **Écart prédit face à `data-extracted/` : aucun** (4450 fichiers identiques). Face au remaster : ses ~301 fichiers
  au texte non décodé et `BALANCE.BIN.json`.
- Indices déjà relevés : la DLL Debug de l'extracteur de l'**ancien checkout** (`926dcb8`, sans `a8598f4`) a été
  construite le 2026-09-19 à 11:46, la minute des fichiers du remaster (`map_1.json` 11:46), et son profil de
  lancement écrit dans le remaster ; la DLL Release du sous-module date du 2026-09-02 à 08:45, la minute de
  `data-extracted/data/BALANCE.BIN.json`.

**Fait le 2026-09-26.**
- Extracteur du sous-module construit en Release (`chantier/portrait-inventaire` = `master` `d1c9de7`, 0 erreur), puis
  `dotnet run --no-build --no-launch-profile -c Release --project AlundraDataExtractor/AlundraDataExtractor.csproj --
  "D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted"
  "D:/development/repo/Alundra Remake/extraction-reference-2026-09-26"` : 47 s, code 0 (870/961 bruitages, 46/46
  musiques).
- Comparaison fichier par fichier (SHA-1, script `compare_trees.py` recopié au §7) :

| Comparaison | Fichiers | Seulement d'un côté | Différents | `o}i` / `s}kr` |
|---|---|---|---|---|
| Référence ↔ `data-extracted/` | 4450 / 4450 | 0 | **0** | 0 / 0 des deux côtés |
| Référence ↔ remaster | 4450 / 4450 | 0 | **301** : `data/BALANCE.BIN.json` et 300 `data/map_*.json` | remaster 234 / 143 (207 fichiers) ; référence 0 / 0 |

- **Prédiction tenue** : écart nul face à `data-extracted/`.
- **Cause établie** : l'extraction du remaster du 2026-09-19 vient de l'ancien checkout de l'analyseur, sans le
  correctif `a8598f4`. Preuves : sa DLL date de la même minute que les fichiers du remaster ; son profil de lancement
  écrit dans le remaster avec des contre-obliques, ce qui donne le `FileName` du remaster. L'extracteur du sous-module
  redonne exactement `data-extracted/`.
- **Suites de référence** : non relevées ici, parce que PI3 construit le moteur et MGUI en parallèle dans le même
  checkout. Dernier relevé connu, le 2026-09-26 au matin sur `eb9d6c8` : `Alundra.Tests` 1285, convertisseur 194,
  `MGUI.Tests` 3090, `CasaEngine.Tests` 1957. Le relevé fait après PI3 sert de base aux tranches suivantes.
- Aucun code modifié ; le dossier `extraction-reference-2026-09-26/` reste en place (§5.2).

### ⏳ PI3 — MGUI : la transformation de rendu liable (G9)

**Prérequis** : approbation (D3, P1). **Dépôt** : MGUI, branche `chantier/render-transform-bindings` depuis `develop`.
- Le DTO `Element` (`MGUI.Core/UI/XAML/Element.cs`) reçoit les attributs `RenderTransformTranslation` et
  `RenderTransformScale` (chaînes, P1) : littéral → `ParseVector2` → `Element.RenderTransform.Translation`/`.Scale`,
  appliqué **après** le DTO `RenderTransform` (`Element.cs:822-824`) ; binding → `BindingPathMappings` :
  `RenderTransformTranslation` → `RenderTransform.Translation`, `RenderTransformScale` → `RenderTransform.Scale`.
  Aucun autre type public ne change ; `UIRenderTransform` reste `sealed`.
- Poussée par la copie typée `Vector2` → `Vector2` (`PushStrategy.TypedCopy`) : aucune allocation ; aucune relance de
  la mise en page (transformation de rendu seule, ADR-0006) ; les bindings sont libérés avec l'écran (précédent G10).
- Tests `MGUI.Tests` : les deux bindings poussent dans `RenderTransform` d'une `Image` et d'un `Canvas` ; la stratégie
  de poussée est la copie typée ; composition sous l'échelle entière d'un canevas parent (le cas des écrans
  d'Alundra) ; échelle 0 ; ancre haut-gauche (origine par défaut) ; **aucune allocation** pendant 1 000 poussées
  (fenêtre d'allocation vide, comme les tests d'ADR-0016) ; aucune invalidation de mise en page ; les littéraux
  (nouveaux attributs, DTO `RenderTransform`, `RenderScale`) donnent les mêmes valeurs qu'avant ; un attribut littéral
  invalide lève la même erreur d'analyse que le DTO. Chaque nouveau test échoue sous sa mutation.
- Sample `MGUI.Samples` (règle de MGUI : toute nouvelle fonctionnalité a un sample), avec son XAML **embarqué**
  (`SampleXamlEmbeddingTests`).
- ADR de MGUI (`Docs/decisions`) ; doc de MGUI mise à jour.
- Moteur, branche `chantier/portrait-inventaire` : pointeur de MGUI ; fiche G9 de l'audit marquée corrigée, comme G10
  l'a été le 2026-09-24.

**Acceptation** : `MGUI.Tests` complet vert, `MGUI.Samples` construit, les deux solutions du moteur à 0 erreur,
`CasaEngine.Tests` vert ; **vérificateur frais CONFIRMED**. **Budget et retour** : §5.1, §5.2 (branches MGUI et
moteur abandonnables, `develop` et `main` intacts). Commits : MGUI
`feat(xaml): bind the translation and scale of a render transform`, puis `docs(decisions): …` ; moteur
`chore(submodules): point MGUI at bindable render transforms`.

### ✅ PI4 — Analyseur : le portrait dans l'atlas et dans `map_alundra.json` — faite le 2026-09-26 (analyseur `e4f3033`)

**Prérequis** : PI2. **Dépôt** : l'analyseur, branche `chantier/portrait-inventaire`.
- Un champ `InventoryPortrait` (`SiImage`, nul par défaut) sur le `GameMap` (P2), marqué
  `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, rempli **pour la carte globale seulement**, là où
  `datasBin.AlundraGameMap` est construite (le `BinaryReader` du chargement y est disponible), depuis
  `SpriteInfo.SpriteRecords[0].GetPortraitImageset(br).Images[0]`.
- `EnumerateImages` le rend en plus des images des animations quand il est présent : il entre dans l'atlas, reçoit
  `AtlasX/AtlasY`, et `map_alundra.json` le porte (sérialisation existante).
- Aucune autre carte ne change : le champ nul est omis de chaque `map_<n>.json`.

**Acceptation** (dans PI6, par l'extraction) : l'atlas ne diffère de la référence de PI2 que par le rectangle
(200, 568)–(248, 624), et ce rectangle est égal, texel par texel, au décodage indépendant de §1.2 (histogramme des
index au §7) ; `map_alundra.json` ne diffère que par le champ ajouté ; les `map_<n>.json` des autres cartes sont
identiques ; le rectangle ne recoupe sur la page 2 aucune signature de palette différente (D-E13C-3, script au §7).
Build de l'extracteur. Commit : `feat(extractor): export the inventory portrait of sprite record 0`.

**Fait le 2026-09-26** (analyseur `e4f3033`, trois fichiers) :
- `GameMap.InventoryPortrait` (`AlundraEngine/DatasBin/GameMap.cs`), `[JsonIgnore(Condition = WhenWritingNull)]` ;
- rempli après `datasBin.AlundraGameMap.Load(br)` (`AlundraDataExtractor/Program.cs:1365-1367`) ;
- rendu en dernier par `EnumerateImages` (`GameMapHelper.cs`).

Build de l'extracteur : 0 erreur ; ses avertissements sont préexistants. L'extraction de l'étape 1 de PI6, faite dès
PI4 vers un dossier neuf, sert d'acceptation (script `pi4_proof.py` au §7) :

| Preuve | Résultat |
|---|---|
| Portrait ↔ référence, fichier par fichier | 4450 / 4450 ; **2 différents** : `data/map_alundra.json`, `data/map_alundra_spritesheet.png` (prédit) ; `o}i`/`s}kr` : 0 |
| Atlas hors du rectangle (200, 568)–(248, 624) | 0 pixel différent (RGBA) |
| Atlas de référence dans le rectangle | 0 pixel opaque |
| Rectangle ↔ décodage indépendant de `DATAS.BIN` | **0 texel différent sur 2688** ; histogramme des index {0: 565, 1: 189, 2: 189, 3: 207, 4: 203, 5: 153, 6: 179, 7: 144, 8: 126, 9: 99, 10: 52, 11: 68, 12: 75, 13: 71, 14: 88, 15: 280} |
| `map_alundra.json` sans le champ ajouté | identique à la référence |
| Champ ajouté | page 2, palette 16, source (200, 56), 48×56, signature `61779762221058`, atlas (200, 568) |
| D-E13C-3, page 2 | 88 images distinctes, **aucune** ne recoupe le portrait |

**Mesuré au passage** : l'atlas écrit ses couleurs dans l'ordre natif de la PS1 (rouge = bits 0-4).
- La première version de la preuve supposait l'ordre qu'on lit dans `ImageHelper.FromPsxColor(int)` (rouge =
  bits 10-14), et chaque texel opaque différait.
- Les 15 couleurs du rectangle sont ensuite apparues dans l'ordre natif, chacune avec le compte exact de son index.
  C'est le même chemin (`GenerateSpriteBitmap`) que tous les sprites déjà validés en jeu.

### ✅ PI5 — Convertisseur : le sprite du portrait et son index — faite le 2026-09-26

**Prérequis** : PI4. **Dépôt** : parent.
- `SpriteBankReader` lit `InventoryPortrait` de `map_alundra.json` ; `SpriteWriter` en fait un `.sprite` par le chemin
  existant (même nom `sprite_<signature>`, même identifiant `SpriteAssetId`, même catalogue).
- Un écrivain produit `Data/inventory-portrait.json` avec l'identifiant du sprite ; champ absent → aucun index et un
  avertissement dans `report.json` (pas d'erreur : la DLL affiche alors l'inventaire sans portrait).
- Tests du convertisseur sur fixture : portrait présent → sprite, identifiant déterministe, index ; absent → pas
  d'index, avertissement ; identifiant stable d'un export à l'autre. Mutations.
- ADR du dépôt parent (`docs/decisions/`) : le champ `InventoryPortrait` et `Data/inventory-portrait.json` (P2).

**Acceptation** : suite du convertisseur verte. Commits : `feat(converter): export the inventory portrait sprite and
its index`, `docs(adr): record the inventory portrait data path`, puis le pointeur de l'analyseur.

**Fait le 2026-09-26.**
- `SpriteBankReader.ReadInventoryPortrait` (+ `InventoryPortraitPropertyName`) et `SpriteWriter.ConvertInventoryPortrait`,
  appelé après la boucle des banques et avant l'enregistrement du catalogue : le `.sprite` va dans `UI/Portraits/`
  (`sprite_61779762221058.sprite`, identifiant `SpriteAssetId`, texture de la planche `map_alundra`), l'index dans
  `Data/inventory-portrait.json` (`{ "SpriteAssetId": "…" }`), compteur `Sprites.InventoryPortrait` ; champ absent →
  avertissement, pas d'index.
- Tests `SpriteWriterInventoryPortraitTests` (3) : portrait présent (sprite, rectangle (200, 568, 48, 56), catalogue,
  texture de la planche, index), absent (avertissement, ni index ni dossier), identique à l'octet d'un export à l'autre.
  Convertisseur **197/197** (194 + 3).
- Mutations réelles (script `mutate.py`, fichier restauré à l'octet puis reconstruit) : l'appel retiré → les 3 tests
  échouent ; l'avertissement retiré → le test « absent » échoue.
- ADR-0005 du dépôt (`docs/decisions/0005-inventory-portrait-data-path.md`). Le pointeur de l'analyseur est déjà
  enregistré (`d0bd4a7`, PI4).

### ⏳ PI6 — Ré-extraction complète, copie, export prouvé (écrit hors du dépôt, D4)

**Prérequis** : PI4, PI5.
1. Extraction complète avec l'extracteur de PI4 vers un dossier neuf
   `D:/development/repo/Alundra Remake/extraction-portrait-2026-09-26/`. Comparaison avec la référence de PI2 :
   **prédit** = `data/map_alundra_spritesheet.png` et `data/map_alundra.json` seulement ; preuves au texel et au
   rectangle de PI4.
2. Le remaster actuel est renommé `remaster-data-extracted.bak-2026-09-19` (P5), puis la nouvelle extraction devient
   `remaster-data-extracted`.
3. **Sauvegarde** de `data-extracted/` (seule copie au texte décodé à ce moment) vers
   `D:/development/repo/Alundra Remake/data-extracted.bak-2026-09-26/` (`robocopy /E` depuis PowerShell), prouvée par
   `diff -rq` = 0. Puis copie vers `data-extracted/` : écart **prédit** = l'écart de PI2 (s'il en reste) plus les deux
   fichiers du portrait ; `robocopy <remaster> <data-extracted> /MIR` **lancé depuis PowerShell**, puis `diff -rq` = 0.
4. **Sauvegarde** d'`alundra-project/` vers `D:/development/repo/Alundra Remake/alundra-project.bak-2026-09-26/`
   (`robocopy /E`, `diff -rq` = 0). Manifeste d'`alundra-project/` avant ; export complet **en place** ; **prédit** : le `.sprite` du portrait ajouté,
   `Data/inventory-portrait.json` ajouté, `Sprites/Textures/map_alundra_spritesheet.png` modifié, `AssetInfos.json`,
   `report.json` (plus ce qu'aurait expliqué l'écart de PI2) ; mesuré ⊆ prédit ; **double export** ⊆ `{report.json}` ;
   0 erreur ; vérification des assets PASSED ; texte des dialogues toujours décodé.
5. Suites : `Alundra.Tests` (après l'export, jamais pendant), convertisseur.

**Acceptation** : chaque prédiction tenue ; **vérificateur frais CONFIRMED** sur la chaîne PI4-PI6. **Arrêt** : tout
écart hors prédiction. Commit (parent) : `docs(plan): record the re-extraction and the export with the inventory
portrait`.

### ✅ PI7 — DLL : la machine du portrait (logique pure, testée) — faite le 2026-09-26

**Prérequis** : PI1. **Dépôt** : parent.
- Une classe `AlundraInventoryPortrait`, port du bloc `0x80057b64..0x800581fc` : états 0/5/4/2 ; `Start(tête)` avec la
  garde « état = 0 » ; `BeginReturn(tête)` seulement si l'état est non nul ; `Step()` une fois par tick → X, Y, W, H,
  visible ; formules du §1.1 en arithmétique entière tronquée vers zéro ; pas de « +2 » ; teinte ignorée (D2).
- Le calcul du point de la tête : `(PosX >> 16) − scrollX`, `(PosY >> 16) − scrollY − (PosZ >> 16) − 32`, avec le
  défilement tiré de `AlundraCameraMath.ToOriginalScrollSpace` (§1.6) ; commentaires d'adresse.
- Tests `Alundra.Tests` : les deux tables de tailles appel par appel ; les positions pour un écart positif et négatif
  (troncature vers zéro) ; la garde ; le repos qui dure ; le retour relu à la sortie ; le point de la tête sur des
  valeurs choisies. Chaque test échoue sous sa mutation (script, jamais simulée dans le test).

**Acceptation** : `Alundra.Tests` vert. Commit : `feat(inventory): port the opening portrait's state machine`.

**Fait le 2026-09-26.** `Alundra/Scripts/AlundraInventoryPortrait.cs` :
- états 0/5/4/2, `Start` gardé par « état = 0 », `BeginReturn` ignoré à l'état 0, `Step` ;
- `ComputeHeadPoint` : parties entières par décalage de 16, sans « +2 » ;
- une instance partagée `Instance` (P3).

Tests `AlundraInventoryPortraitTests` (7) :
- les deux tables de 16 appels écrites en dur, identiques à celles que PI1 a exécutées dans le binaire ;
- les positions pour les têtes (160, 120) et (300, 60) (troncature vers zéro) ;
- le repos qui dure 100 appels ;
- le retour vers la tête relue ;
- la garde ;
- le retour ignoré à l'état 0 ;
- le point de la tête.

`Alundra.Tests` **1292/1292** (1285 + 7). Mutations réelles, fichier restauré puis reconstruit à chaque fois :

| Mutation | Test qui échoue |
|---|---|
| Arrondi par défaut en Y | troncature (tête (300, 60)) et retour |
| Garde du départ retirée | garde |
| « +2 » en X | point de la tête |
| Taille de retour à l'ouverture | table d'ouverture |

### ⏳ PI8 — DLL : le branchement et les deux écrans

**Prérequis** : PI3, PI6, PI7. **Dépôt** : parent (et les écrans versionnés d'`alundra-project/UI/Screens/`).
- Un seul état partagé (P3), attaché au monde comme les directeurs. Départ aux deux ouvertures (principal : après
  l'équivalent d'`InitializeHudPosition`/`SetTransitionType(6)`, avant les noms et le son 4 ; sous-inventaire :
  `OpenFromPostProcess`, même place). Retour aux quatre sorties (deux fermetures, deux bascules), après le glissement de
  sortie et avant `InitializeHudPositionBeforeHide`, comme l'original.
- Le départ du principal se place dans sa tête (`RunDisplayInventoryHead`), qui sert les deux chemins (déclenchement
  et post-traitement), après l'équivalent d'`InitializeHudPosition`/`SetTransitionType(6)`.
- Le pas une fois par tick, juste après `AlundraInventoryPostProcess.Run` et avant les présentateurs, dans la boucle
  par tick d'`AlundraWorldProxy` (P4 révisée) ; le présentateur pousse X, Y, W, H, visible.
- **L'ordre des éléments des deux écrans** suit la table d'ordre de l'original. Dans les écrans actuels, les textes
  (noms, description) sont déclarés **après** les icônes et les chiffres. L'original les dessine avant (entrées 1-2
  contre 4-5, PI1). Ils sont donc déplacés **avant** les icônes, et le portrait se place entre eux et les icônes. Au
  repos, textes et icônes ne se recouvrent pas : rien ne change à l'image hors des vols ; à vérifier par capture (PI9).
- Les deux écrans : une `Image` du portrait placée dans l'ordre de dessin du §1.1 (après les boîtes et les textes,
  avant les icônes, les chiffres et le curseur), en `CanvasLeft = 248`, `CanvasTop = 104`, sa source liée à
  l'identifiant de `Data/inventory-portrait.json`, sa translation (X − 248, Y − 104) et son échelle (W/48, H/56) liées
  (PI3), sa visibilité liée ; données de conception mises à jour.
- Tests : l'ouverture principale déclenchée au tick T dessine 0×0 au tick T, est visible dès T+1, au repos dès T+15 ;
  un retour et une ouverture par le post-traitement font leur premier pas dans leur propre tick ; le retour se termine
  (état 0) avant que l'écran ne soit retiré ; bascule dans les deux sens (le départ du menu suivant trouve l'état à 0,
  avec la marge mesurée) ; portrait
  absent de l'index → inventaire sans portrait, sans exception ; tests existants des directeurs et présentateurs
  inchangés.
- La doc de classe du directeur (« Not ported ») est mise à jour.

**Acceptation** : `Alundra.Tests` vert ; build de la DLL. Commit : `feat(inventory): fly the opening portrait in both
inventories`.

### ⏳ PI9 — Recette par capture, prédite avant d'être prise

**Prérequis** : PI8. Harnais **hors dépôt** (précédent de SI5 du sous-inventaire), captures par le back-buffer en
processus seulement.
- Sur la 389, position du joueur connue : prédire puis capturer le rectangle du portrait (à l'échelle entière de la
  fenêtre) aux ticks N+2, N+8, N+16 de l'ouverture, au repos, au milieu du retour, pendant une bascule L1/R1 dans chaque
  sens, et après la fermeture (plus rien).
- Comparer le rectangle mesuré au rectangle prédit ; vérifier l'ordre de dessin pendant un vol (le portrait passe
  sous une icône, sur une boîte).

**Acceptation** : chaque capture tient sa prédiction ; **vérificateur frais CONFIRMED** sur le branchement et la
recette. Commit : `docs(plan): record the predicted captures of the inventory portrait`.

### ⏳ PI10 — Recette de l'auteur

Ouvrir l'inventaire, basculer L1/R1 dans les deux sens, fermer ; vérifier que le portrait est bien Alundra, qu'il part
de sa tête, reste à droite au milieu et y revient. Validation → ✅ et clôture : plan maître
(`docs/plan-conversion-totale.md`, E13.d : le portrait n'est plus reporté).

---

## 4. Acceptation d'ensemble

Le chantier est clos quand, en jeu : le portrait d'Alundra vole depuis sa tête à chaque ouverture de l'inventaire
principal et du sous-inventaire, reste affiché au repos, revient vers sa tête à chaque fermeture et à chaque bascule,
avec les durées et l'ordre de dessin de l'original ; la teinte reste normale ; le remaster et `data-extracted/` ont le
texte décodé ; chaque export est prouvé par double export ; toutes les suites sont vertes.

## 5. Arrêts, budgets et retours arrière

### 5.1 Budgets

- **Enveloppe** : chaque tranche a au plus **deux tentatives d'exécution** au même niveau (exécutant), puis la session
  principale reprend ou l'exécution monte d'un niveau ; aucune troisième tentative identique. Chaque frontière à risque
  (PI3, PI6, PI9) a au plus **cinq passes** correction puis re-vérification, chacune sur un état réellement changé.
  Disque : au plus ~12 Go sous `D:/development/repo/Alundra Remake/` (deux extractions de 3,4 Go, la sauvegarde de
  `data-extracted/` de 3,4 Go, celle d'`alundra-project/` de 1,3 Go ; 500 Go libres mesurés le 2026-09-26).
- **PI1** : une passe de contre-vérification ; un écart donne une seule re-mesure ciblée ; au-delà, arrêt.
- **PI2** : une extraction de référence ; une seule relance, et seulement après une panne d'environnement (build,
  disque), jamais pour « voir si l'écart disparaît ».
- **PI3** : deux tentatives d'exécutant, cinq passes de vérification au plus.
- **Budget épuisé** : la tranche passe en ⚠️, la question est écrite au §6, et le travail s'arrête ; les tranches qui
  n'en dépendent pas peuvent continuer.

### 5.2 Retours arrière

| Mutation | Tranche | Source de restauration (existante à ce moment) | Retour |
|---|---|---|---|
| Dossier `extraction-reference-2026-09-26/` | PI2 | — (dossier neuf, rien d'écrasé) | Laissé en place ; l'auteur décide de sa suppression. |
| Dossier `extraction-portrait-2026-09-26/` | PI6 | — (dossier neuf) | Idem. |
| Remaster renommé | PI6 | `remaster-data-extracted.bak-2026-09-19` | Renommer le dossier courant, puis rendre son nom à la sauvegarde. |
| `data-extracted/` recopié (`/MIR`) | PI6 | `data-extracted.bak-2026-09-26` (prouvé identique avant le `/MIR`) | `robocopy <bak> data-extracted /MIR` depuis PowerShell, puis `diff -rq` = 0. |
| `alundra-project/` réexporté en place | PI6 | `alundra-project.bak-2026-09-26` et le manifeste d'avant l'export | `robocopy <bak> alundra-project /E` depuis PowerShell, puis manifeste identique au manifeste d'avant ; à défaut, export depuis `data-extracted/` restauré avec le convertisseur de `main`, prouvé contre ce manifeste. Jamais de suppression manuelle d'`alundra-project/`. |
| Branches MGUI, moteur, analyseur, parent | PI1-PI8 | `develop` (MGUI), `main` (moteur, parent), `master` (analyseur), jamais modifiées par ce chantier | Abandon : revenir sur la branche d'origine (`git switch`) ; les branches du chantier sont gardées, rien n'est fusionné sans l'auteur. |
| Écrans versionnés `alundra-project/UI/Screens/` | PI8 | leur version sur `main` du parent | `git restore --source=main -- alundra-project/UI/Screens/…` sur la branche du chantier. |

### 5.3 Arrêts

- Une contre-vérification (PI1) qui contredit le §1.1 : plan corrigé et relu avant PI7.
- Un écart d'extraction ou d'export hors prédiction ; un double export hors de `{report.json}` ; un rectangle d'atlas
  qui diffère du décodage indépendant ou qui recoupe une signature de palette différente.
- Un manque de MGUI ou du moteur autre que G9 : consigné, la tranche s'arrête (D-E13D-4).
- Un test existant qui doit changer pour une autre raison que le portrait.
- `alundra-project/` supprimé à la main ; le remaster supprimé au lieu d'être renommé ; `Program.cs` indexé ; un
  commit sur `main` ou `develop` ; un push.

## 6. Points ouverts et hors périmètre

1. **Les portraits des dialogues** (même système, point d'entrée `0x80057c84`) : E12.c, hors périmètre. Si E12.c les
   porte, l'état unique du portrait devra être partagé aussi avec eux (un seul quad dans l'original).
2. **La glue du HUD** (`AlundraHudScreen`, translation recopiée en C#) pourra passer au binding de PI3 : suite
   possible, hors périmètre (D3).
3. **La décompilation** garde son « +2 » en X : l'analyseur n'est pas corrigé sur ce point dans ce chantier (le
   portage suit le binaire) ; à signaler dans son dépôt si l'auteur le souhaite.

## 7. Journal et annexes

| Date | Événement |
|---|---|
| 2026-09-26 | Demande de l'auteur ; reconnaissance en lecture seule (original et binaire, extracteur, convertisseur, DLL et écrans, MGUI, flux de données, point de la tête) ; réponses de l'auteur D1-D4 ; rédaction. |
| 2026-09-26 | Relecture fraîche : **REVISE**, trois blocages, tous acceptés (FIX). (1) Un champ nul ajouté au `GameMap` serait écrit dans chaque `map_<n>.json` : P2 et PI4 l'omettent quand il est nul (`JsonIgnoreCondition.WhenWritingNull`, `AlundraEngine` cible `net9.0-windows`). (2) La voie des objets imbriqués liables ne s'applique pas à `UIRenderTransform` : P1 et PI3 retiennent deux attributs d'élément renommés vers le chemin cible imbriqué `RenderTransform.Translation`/`.Scale`, cible et type de valeur nommés, poussée par copie typée (§1.5, lu en session principale). (3) Ni retour arrière ni budget : §5.1 et §5.2 ajoutés, sauvegardes de `data-extracted/` et d'`alundra-project/` avant réécriture (PI6). |
| 2026-09-26 | Second relecteur frais, sur le plan révisé : **READY**. Soumis à l'auteur. |
| 2026-09-26 | **Approuvé par l'auteur, P1 à P5 compris, mode AUTO.** Branches créées (en-tête). |
| 2026-09-26 | PI2 faite : extraction de référence identique à `data-extracted/` ; cause du remaster établie. PI4 faite (analyseur `e4f3033`), prouvée par l'étape 1 de PI6. PI1 et PI3 lancées en parallèle (agent indépendant, exécutant). |
| 2026-09-26 | PI1 faite : 50 vérifications, 0 échec ; deux écarts (libellé, horloge). §1.1, P4 et PI8 révisés ; relecture fraîche de la tranche PI8 révisée avant son exécution. |

### PI2, PI6 — `compare_trees.py` (comparaison de deux extractions)

```python
"""Compare two extraction trees file by file (SHA-1), and count undecoded-text markers.

Usage: python compare_trees.py <left> <right> [--markers]
Prints: counts, files only in left/right, files that differ. Exit code 0 when identical.
"""
import hashlib
import os
import sys

MARKERS = ['o}i', 's}kr']


def sha1(path):
    h = hashlib.sha1()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def walk(root):
    files = {}
    for dirpath, _, names in os.walk(root):
        for name in names:
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, root).replace('\\', '/')
            files[rel] = full
    return files


def main():
    left, right = sys.argv[1], sys.argv[2]
    lf, rf = walk(left), walk(right)
    only_l = sorted(set(lf) - set(rf))
    only_r = sorted(set(rf) - set(lf))
    common = sorted(set(lf) & set(rf))
    differ = [rel for rel in common if os.path.getsize(lf[rel]) != os.path.getsize(rf[rel]) or sha1(lf[rel]) != sha1(rf[rel])]
    print(f'left={len(lf)} right={len(rf)} common={len(common)} only_left={len(only_l)} only_right={len(only_r)} differ={len(differ)}')
    for rel in only_l:
        print('ONLY_LEFT ', rel)
    for rel in only_r:
        print('ONLY_RIGHT', rel)
    for rel in differ:
        print('DIFFER    ', rel)
    if '--markers' in sys.argv:
        for label, files in (('left', lf), ('right', rf)):
            counts = {m: 0 for m in MARKERS}
            hit_files = 0
            for rel, full in files.items():
                if not rel.endswith('.json'):
                    continue
                with open(full, 'rb') as f:
                    text = f.read().decode('utf-8', errors='replace')
                hit = False
                for m in MARKERS:
                    c = text.count(m)
                    counts[m] += c
                    hit = hit or c > 0
                hit_files += 1 if hit else 0
            print(f'MARKERS {label}: ' + ', '.join(f'{m!r}={c}' for m, c in counts.items()) + f', files with markers={hit_files}')
    sys.exit(0 if not (only_l or only_r or differ) else 1)


if __name__ == '__main__':
    main()
```

### PI4, PI6 — `pi4_proof.py` (preuve au texel, JSON, règle D-E13C-3)

```python
"""PI4/PI6 step 1 proof, read-only. Compares the reference extraction and the portrait extraction:
1. the two atlases are identical outside the portrait rectangle (RGBA, pixel by pixel);
2. the reference atlas is fully transparent inside the rectangle;
3. inside the new atlas, each texel equals an INDEPENDENT decode of sprite record 0's portrait from
   DATAS.BIN (index map read straight from the file), coloured by the extractor's own convention
   (DATAS.BIN little-endian palette words in the native PS1 order, as the atlas stores them);
4. map_alundra.json differs only by the added InventoryPortrait field;
5. D-E13C-3: on page 2, no other image rectangle of a different palette intersects the portrait's.
Usage: python pi4_proof.py <reference_dir> <portrait_dir>"""
import json
import struct
import sys

from PIL import Image

REF, NEW = sys.argv[1], sys.argv[2]
DATAS = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/DATA/DATAS.BIN"

# ---- independent decode of record 0's portrait (layout as in the decompilation's readers) ----
f = open(DATAS, 'rb')


def u32(off):
    f.seek(off)
    return struct.unpack('<I', f.read(4))[0]


def i32(off):
    f.seek(off)
    return struct.unpack('<i', f.read(4))[0]


spr_rec, sheet_off, sheet_end = u32(0), u32(4), u32(0x14)
table_ptr, effects_ptr, pal_ptr = i32(spr_rec + 0xc), i32(spr_rec + 0x10), i32(spr_rec + 0x14)
table = [i32(spr_rec + table_ptr + 4 * k) for k in range((effects_ptr - table_ptr) // 4)]
f.seek(spr_rec + pal_ptr)
raw = f.read(41 * 32)
pals = [[struct.unpack_from('<H', raw, (p * 16 + c) * 2)[0] for c in range(16)] for p in range(41)]
f.seek(sheet_off + 6)
data = f.read(sheet_end - sheet_off - 6)
vram = bytearray(256 * 256 * 8 // 2)
i = b = 0
while i < len(vram) and b < len(data):
    v = data[b]
    b += 1
    if v == 0xad:
        seek = data[b]
        b += 1
        if seek == 0:
            vram[i] = v
            i += 1
        else:
            ln = data[b]
            b += 1
            s = i - seek
            for _ in range(ln):
                vram[i] = vram[s]
                i += 1
                s += 1
    else:
        vram[i] = v
        i += 1

rec = spr_rec + table[0]
frames_ptr = i32(rec + 0xc)
f.seek(spr_rec + frames_ptr)
f.read(2)
img = f.read(14)
page, pal, sx, sy, w, h = img[:6]
page &= 7
print(f'record 0 portrait: page={page} palette={pal} source=({sx},{sy}) size={w}x{h}')
assert (page, pal, sx, sy, w, h) == (2, 16, 200, 56, 48, 56), 'D0.9 measurement not reproduced'


def extractor_rgba(c):
    # Measured on the written atlas (2026-09-26): its texels follow the native PS1 order, R = bits 0-4,
    # G = bits 5-9, B = bits 10-14, each << 3, and the colour 0x0000 is transparent. (A first run of this
    # script assumed ImageHelper.FromPsxColor's R = bits 10-14 and found every opaque texel off; the
    # 15 colours then matched the native order with the exact index counts.)
    return ((c & 0x1f) << 3, ((c >> 5) & 0x1f) << 3, ((c >> 10) & 0x1f) << 3, 255 if c != 0 else 0)


expected = {}
hist = {}
for yy in range(h):
    for xx in range(w):
        px, py = sx + xx, page * 256 + sy + yy
        byte = vram[py * 128 + px // 2]
        idx = (byte >> 4) if (px & 1) else (byte & 0xf)
        hist[idx] = hist.get(idx, 0) + 1
        expected[(px, py)] = extractor_rgba(pals[pal][idx])
print('index histogram', dict(sorted(hist.items())))

# ---- atlases ----
ra = Image.open(f'{REF}/data/map_alundra_spritesheet.png').convert('RGBA')
na = Image.open(f'{NEW}/data/map_alundra_spritesheet.png').convert('RGBA')
assert ra.size == na.size, (ra.size, na.size)
rx0, ry0, rx1, ry1 = sx, page * 256 + sy, sx + w, page * 256 + sy + h
rp, np_ = ra.load(), na.load()
outside_diff = inside_ref_opaque = inside_mismatch = 0
for y in range(ra.size[1]):
    for x in range(ra.size[0]):
        inside = rx0 <= x < rx1 and ry0 <= y < ry1
        if not inside:
            if rp[x, y] != np_[x, y]:
                outside_diff += 1
        else:
            if rp[x, y][3] != 0:
                inside_ref_opaque += 1
            e = expected[(x, y)]
            got = np_[x, y]
            # A transparent texel carries no colour: compare alpha only there.
            if (e[3] == 0 and got[3] != 0) or (e[3] != 0 and got != e):
                inside_mismatch += 1
print(f'atlas size={ra.size} rect=({rx0},{ry0})-({rx1},{ry1})')
print(f'outside rectangle: {outside_diff} differing pixels (expected 0)')
print(f'reference inside rectangle: {inside_ref_opaque} opaque pixels (expected 0)')
print(f'new inside rectangle vs independent decode: {inside_mismatch} mismatching texels of {w * h} (expected 0)')

# ---- JSON ----
rj = json.load(open(f'{REF}/data/map_alundra.json', encoding='utf-8'))
nj = json.load(open(f'{NEW}/data/map_alundra.json', encoding='utf-8'))
portrait = nj.pop('InventoryPortrait', None)
print('map_alundra.json: InventoryPortrait present:', portrait is not None)
print('map_alundra.json: identical once the field is removed:', rj == nj)
if portrait is not None:
    keys = ['Spritesheet', 'Palette', 'SourceX', 'SourceY', 'Swidth', 'Sheight', 'Signature', 'AtlasX', 'AtlasY']
    print('InventoryPortrait:', {k: portrait.get(k) for k in keys})

# ---- D-E13C-3 on page 2 ----
hits = []


def walk_images(node):
    if isinstance(node, dict):
        if 'Signature' in node and 'Spritesheet' in node and 'SourceX' in node:
            yield node
        for v in node.values():
            yield from walk_images(v)
    elif isinstance(node, list):
        for v in node:
            yield from walk_images(v)


seen = set()
for im in walk_images(rj):
    if im['Signature'] in seen or (im['Spritesheet'] & 7) != page:
        continue
    seen.add(im['Signature'])
    ax0, ay0, ax1, ay1 = im['SourceX'], im['SourceY'], im['SourceX'] + im['Swidth'], im['SourceY'] + im['Sheight']
    if ax0 < sx + w and sx < ax1 and ay0 < sy + h and sy < ay1:
        hits.append((im['Signature'], im['Palette'], (ax0, ay0, ax1, ay1)))
print(f'page {page}: {len(seen)} distinct images; intersecting the portrait: {len(hits)}')
for s_, p_, r_ in hits:
    print('   signature', s_, 'palette', p_, 'rect', r_, 'DIFFERENT PALETTE' if p_ != pal else 'same palette')
```

### PI1 — `pi1_crosscheck.py` (contre-vérification dans `ALUN_CD.EXE`, sha256 `b638ff45…827a`)

```python
#!/usr/bin/env python3
# PI1 - independent cross-check of docs/plan-portrait-inventaire.md section 1.1 against ALUN_CD.EXE (France).
# Read only: the executable is opened for reading, nothing is written anywhere.
# Two kinds of evidence:
#   1. static: raw decoding of the instructions (jal targets, immediates, encodings), capstone for the text;
#   2. dynamic: the ORIGINAL MIPS code of the portrait (start, step, return, draw, UI ordering table) is executed
#      in a small R3000 interpreter (load-delay hazards are detected, not silently accepted), and the results are
#      compared with the formulas of the plan.
# Usage: python pi1_crosscheck.py [--listing]
import struct
import sys
from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
RAW = open(EXE, "rb").read()
assert RAW[:8] == b"PS-X EXE"
T_ADDR, T_SIZE = struct.unpack_from("<II", RAW, 0x18)
assert (T_ADDR, T_SIZE) == (0x80020000, 0xAA800), (hex(T_ADDR), hex(T_SIZE))
TEXT = RAW[0x800:0x800 + T_SIZE]            # file offset = 0x800 + addr - 0x80020000
M32 = 0xFFFFFFFF
MD = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 + CS_MODE_LITTLE_ENDIAN)

NAMES = {  # names from the //8xxxxxxx comments of the decompilation (labels only, never used as evidence)
    0x8004BD9C: "InitializeHudPosition", 0x80047F94: "SetTransitionType", 0x80057B40: "GetAnimationImageByIndex",
    0x80057C18: "PortraitStart(inventory)", 0x80057C84: "PortraitStart(dialogue)", 0x80057CF0: "PortraitInit",
    0x80055C84: "DisplayIconNames", 0x800490FC: "PlaySoundEffect", 0x80052618: "StartFadeOut(opens sub-inventory)",
    0x80055570: "DisplayInventory", 0x800556DC: "MainSlideOut", 0x800526CC: "SubSlideOut",
    0x80057B84: "UpdateHudTransitionState(portrait return)", 0x8004BE0C: "InitializeHudPositionBeforeHide",
    0x8002BD60: "RenderScene", 0x8002BAEC: "Update", 0x80044C5C: "DisplayUserInterface",
    0x80058134: "DisplayInventoryCharacterPortrait", 0x80057EBC: "PortraitStep", 0x80048054: "UiCallbacksAndPostProcess",
    0x8008511C: "ClearOTag", 0x800852CC: "DrawOTag", 0x800843B0: "SetPolyFT4", 0x800859C8: "libgpu 800859c8",
    0x80085A70: "SetDrawArea", 0x80057B64: "PortraitReset",
}


def word(a):
    return struct.unpack_from("<I", TEXT, a - T_ADDR)[0]


def text(a):
    ins = next(MD.disasm(TEXT[a - T_ADDR:a - T_ADDR + 4], a))
    return (ins.mnemonic + " " + ins.op_str).strip()


def s16(v):
    v &= 0xFFFF
    return v - 0x10000 if v & 0x8000 else v


def s32(v):
    v &= M32
    return v - 0x100000000 if v & 0x80000000 else v


def jal_target(a):
    w = word(a)
    if (w >> 26) != 3:
        return None
    return ((a + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2)


def jals(lo, hi):
    return [(p, jal_target(p)) for p in range(lo, hi, 4) if jal_target(p) is not None]


ALL_JALS = {}
for _k in range(T_SIZE // 4):
    _p = T_ADDR + 4 * _k
    _t = jal_target(_p)
    if _t is not None:
        ALL_JALS.setdefault(_t, []).append(_p)


def callers(t):
    return sorted(ALL_JALS.get(t, []))


def delay_a0(p):
    """Constant put in $a0 by the delay slot of the jal at p, or None."""
    w = word(p + 4)
    if w == 0x00002021:                      # move $a0, $zero
        return 0
    if (w >> 16) == 0x2404:                  # addiu $a0, $zero, imm
        return s16(w)
    return None


def func_start(a):
    p = a
    while p > T_ADDR + 8:
        w = word(p)
        if (w & 0xFFFF8000) == 0x27BD8000 and word(p - 8) == 0x03E00008:
            return p
        p -= 4
    return None


def fmt_call(p, t):
    a0 = delay_a0(p)
    return "%08x jal %s%s" % (p, NAMES.get(t, "%08x" % t), "" if a0 is None else "(a0=%d)" % a0)


RESULTS = []


def check(item, cond, msg):
    RESULTS.append((item, bool(cond), msg))
    print("  %s [%s] %s" % ("OK  " if cond else "FAIL", item, msg))


def listing(lo, hi):
    for p in range(lo, hi, 4):
        print("    %08x: %08x  %s" % (p, word(p), text(p)))


# ----------------------------------------------------------------------------------------------------------
# A small R3000 interpreter (MIPS I subset used by the code below), executing the bytes of the executable.
# ----------------------------------------------------------------------------------------------------------
class Hazard(Exception):
    pass


class CPU:
    SENT = 0xFFFFFFF0

    def __init__(self):
        self.r = [0] * 32
        self.hi = self.lo = 0
        self.mem = {}                         # written bytes; unwritten text reads the file, BSS reads 0

    def rb(self, a):
        a &= M32
        v = self.mem.get(a)
        if v is not None:
            return v
        if T_ADDR <= a < T_ADDR + T_SIZE:
            return TEXT[a - T_ADDR]
        return 0

    def wb(self, a, v):
        self.mem[a & M32] = v & 0xFF

    def rh(self, a):
        return self.rb(a) | (self.rb(a + 1) << 8)

    def rw(self, a):
        return self.rh(a) | (self.rh(a + 2) << 16)

    def wh(self, a, v):
        self.wb(a, v)
        self.wb(a + 1, v >> 8)

    def ww(self, a, v):
        self.wh(a, v)
        self.wh(a + 2, v >> 16)

    def snapshot(self, lo, hi):
        return bytes(self.rb(a) for a in range(lo, hi))

    def run(self, entry, stop=(), stubs=None, sp=0x801FF000, max_steps=200000):
        stubs = stubs or {}
        r = self.r
        r[31] = self.SENT
        r[29] = sp
        pc, npc = entry, entry + 4
        last_load = 0
        for _ in range(max_steps):
            if pc == self.SENT or pc in stop:
                return pc
            if pc in stubs:                   # stub = the callee returns at once (after its effect, if any)
                stubs[pc](self)
                pc, npc = r[31], r[31] + 4
                last_load = 0
                continue
            w = self.rw(pc)
            op, rs, rt, rd = w >> 26, (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31
            sa, fn, imm = (w >> 6) & 31, w & 63, w & 0xFFFF
            simm = s16(imm)
            if op == 0:
                reads = (rt,) if fn in (0, 2, 3) else (rs,) if fn in (8, 9, 0x11, 0x13) else () if fn in (0x10, 0x12) else (rs, rt)
            elif op in (1, 6, 7) or 8 <= op <= 0xE or 0x20 <= op <= 0x25:
                reads = (rs,)
            elif op in (4, 5) or op >= 0x28:
                reads = (rs, rt)
            else:
                reads = ()
            if last_load and last_load in reads:
                raise Hazard("load-delay hazard at %08x" % pc)
            a, b = r[rs], r[rt]
            target = None
            load = 0
            if op == 0:
                if fn == 0: r[rd] = (b << sa) & M32
                elif fn == 2: r[rd] = b >> sa
                elif fn == 3: r[rd] = (s32(b) >> sa) & M32
                elif fn == 4: r[rd] = (b << (a & 31)) & M32
                elif fn == 6: r[rd] = b >> (a & 31)
                elif fn == 7: r[rd] = (s32(b) >> (a & 31)) & M32
                elif fn == 8: target = a
                elif fn == 9: r[rd] = pc + 8; target = a
                elif fn == 0x10: r[rd] = self.hi
                elif fn == 0x11: self.hi = a
                elif fn == 0x12: r[rd] = self.lo
                elif fn == 0x13: self.lo = a
                elif fn == 0x18:
                    prod = s32(a) * s32(b)
                    self.lo, self.hi = prod & M32, (prod >> 32) & M32
                elif fn == 0x19:
                    prod = a * b
                    self.lo, self.hi = prod & M32, (prod >> 32) & M32
                elif fn in (0x20, 0x21): r[rd] = (a + b) & M32
                elif fn in (0x22, 0x23): r[rd] = (a - b) & M32
                elif fn == 0x24: r[rd] = a & b
                elif fn == 0x25: r[rd] = a | b
                elif fn == 0x26: r[rd] = a ^ b
                elif fn == 0x27: r[rd] = ~(a | b) & M32
                elif fn == 0x2A: r[rd] = int(s32(a) < s32(b))
                elif fn == 0x2B: r[rd] = int(a < b)
                else: raise NotImplementedError("special %x at %08x" % (fn, pc))
            elif op == 1:
                if rt in (0x10, 0x11): r[31] = pc + 8
                if (s32(a) < 0) if rt in (0, 0x10) else (s32(a) >= 0):
                    target = (pc + 4 + (simm << 2)) & M32
            elif op in (2, 3):
                if op == 3: r[31] = pc + 8
                target = ((pc + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2)
            elif op in (4, 5, 6, 7):
                if {4: a == b, 5: a != b, 6: s32(a) <= 0, 7: s32(a) > 0}[op]:
                    target = (pc + 4 + (simm << 2)) & M32
            elif op in (8, 9): r[rt] = (a + simm) & M32
            elif op == 0xA: r[rt] = int(s32(a) < simm)
            elif op == 0xB: r[rt] = int(a < (simm & M32))
            elif op == 0xC: r[rt] = a & imm
            elif op == 0xD: r[rt] = a | imm
            elif op == 0xE: r[rt] = a ^ imm
            elif op == 0xF: r[rt] = imm << 16
            elif op in (0x20, 0x21, 0x23, 0x24, 0x25):
                ea = (a + simm) & M32
                if op == 0x20: v = self.rb(ea); v = (v - 0x100 if v & 0x80 else v) & M32
                elif op == 0x21: v = s16(self.rh(ea)) & M32
                elif op == 0x23: v = self.rw(ea)
                elif op == 0x24: v = self.rb(ea)
                else: v = self.rh(ea)
                r[rt] = v
                load = rt
            elif op == 0x28: self.wb(a + simm, b)
            elif op == 0x29: self.wh(a + simm, b)
            elif op == 0x2B: self.ww(a + simm, b)
            else:
                raise NotImplementedError("op %x at %08x" % (op, pc))
            r[0] = 0
            last_load = load
            pc, npc = npc, (target if target is not None else npc + 4)
        raise RuntimeError("step limit")


PORT = 0x80180070          # portrait block (state halfword at +0)
IDX = 0x80146F50           # double-buffer index
UI_OT = 0x80146F58         # UI ordering table, 2 x 10 entries
POS = 0x80127E44           # player PosX/PosY/PosZ (16.16)
SCROLL_X, SCROLL_Y = 0x800E4328, 0x800E432C
REST = (248, 104)
NOP = lambda cpu: None
STUBS = {0x800859C8: lambda c: c.r.__setitem__(2, c.r[4]),   # libgpu helpers of the draw path: not modelled
         0x80085A70: NOP,
         0x800481F8: NOP}                                     # the rest of the UI (boxes, texts, icons): not modelled


def place_head(cpu, hx, hy, sx=37, sy=21, pz=9):
    """Player/camera such that PosX_hi - scrollX = hx and PosY_hi - scrollY - PosZ_hi - 32 = hy.
    The low (fractional) halfwords are non-zero on purpose."""
    cpu.ww(POS, (((hx + sx) << 16) | 0x8000) & M32)
    cpu.ww(POS + 4, (((hy + 32 + pz + sy) << 16) | 0x4000) & M32)
    cpu.ww(POS + 8, ((pz << 16) | 0x1234) & M32)
    cpu.ww(SCROLL_X, sx & M32)
    cpu.ww(SCROLL_Y, sy & M32)


def install_image(cpu, u=0x40, v=0x80):
    """Minimal data for GetAnimationImageByIndex(0): [[[0x80126ecc]] + 0xc] + 2 -> image record."""
    cpu.ww(0x80126ECC, 0x80190000)
    cpu.ww(0x80190000, 0x80190100)
    cpu.ww(0x8019010C, 0x80190200)
    for i, b in enumerate((0x03, 0x05, u, v)):
        cpu.wb(0x80190202 + i, b)


def start_main(cpu):          # DisplayInventory from GetAnimationImageByIndex(0) to just after the portrait start
    cpu.run(0x80055634, stop={0x800556B8})


def start_sub(cpu):           # StartFadeOut (sub-inventory open), same stretch
    cpu.run(0x80052630, stop={0x800526B4})


def fields(cpu):
    g = lambda o: s32(cpu.rw(PORT + o))
    return dict(state=s16(cpu.rh(PORT)), ptrs=[cpu.rw(PORT + o) for o in (0x54, 0x58, 0x5C, 0x60, 0x64)],
                p68=(g(0x68), g(0x6C)), p70=(g(0x70), g(0x74)), delta=(g(0x78), g(0x7C)), step=g(0x80),
                size=(g(0x84), g(0x88)), rest=(g(0x8C), g(0x90)))


def quad(cpu, idx):
    p = PORT + 4 + idx * 40
    xs = [s16(cpu.rh(p + o)) for o in (8, 16, 24, 32)]
    ys = [s16(cpu.rh(p + o)) for o in (10, 18, 26, 34)]
    uv = [(cpu.rb(p + o), cpu.rb(p + o + 1)) for o in (12, 20, 28, 36)]
    return dict(p=p, xs=xs, ys=ys, uv=uv, rgb=(cpu.rb(p + 4), cpu.rb(p + 5), cpu.rb(p + 6)), code=cpu.rb(p + 7),
                X=xs[0], Y=ys[0], W=xs[1] - xs[0], H=ys[2] - ys[0])


def walk(cpu, ot):
    nodes, a = [], ot
    for _ in range(100):
        nodes.append(a)
        nxt = cpu.rw(a) & 0xFFFFFF
        if nxt == 0xFFFFFF:
            break
        a = 0x80000000 | nxt
        if a == 0x800C81F0:                    # ClearOTag's terminator
            nodes.append(a)
            break
    return nodes


def label(a):
    if UI_OT <= a < UI_OT + 80:
        return "OT[%d]" % (((a - UI_OT) % 40) // 4)
    if a in (PORT + 4, PORT + 44):
        return "PORTRAIT"
    if 0x80180108 <= a < 0x80180120:
        return "drawarea"
    if 0x80146E60 <= a < UI_OT:
        return "dr%d" % (((a - 0x80146E60) % 120) // 12)
    if a == 0x800C81F0:
        return "END"
    return "%08x" % a


def render_frame(cpu):
    """ClearOTag of the current UI buffer (0x80044f48), then DisplayUserInterface (0x80044c5c) for real,
    which calls DisplayInventoryCharacterPortrait (0x80058134) -> PortraitStep (0x80057ebc) and flips the buffer."""
    idx = cpu.rw(IDX)
    cpu.run(0x80044F48)
    cpu.run(0x80044C5C, stubs=STUBS)
    ot = cpu.r[2]
    assert ot == UI_OT + idx * 40 and cpu.rw(IDX) == idx ^ 1
    return idx, ot


def tdiv(a, b):
    q = abs(a) // abs(b)
    return q if (a < 0) == (b < 0) else -q


def open_formula(k, hx, hy):
    s = 16 - k
    if s == 0:
        return (248, 104, 48, 56, 128)
    return (248 + tdiv(s * (hx - 248), 15), 104 + tdiv(s * (hy - 104), 15),
            tdiv(48 * (15 - s), 15), tdiv(56 * (15 - s), 15), 127 + tdiv(128 * s, 15))


def return_formula(k, hx, hy):
    s = 16 - k
    if s == 0:
        return (248, 104, 0, 0, 0)
    return (hx + tdiv(s * (248 - hx), 15), hy + tdiv(s * (104 - hy), 15),
            tdiv(48 * s, 15), tdiv(56 * s, 15), 127 + tdiv(128 * (15 - s), 15))


def floor_open(k, hx, hy):
    s = 16 - k
    if s == 0:
        return (248, 104)
    return (248 + (s * (hx - 248)) // 15, 104 + (s * (hy - 104)) // 15)


def frame_row(cpu, k, formula, hx, hy):
    idx, ot = render_frame(cpu)
    q = quad(cpu, idx)
    f = fields(cpu)
    nodes = walk(cpu, ot)
    linked = q["p"] in nodes
    got = (q["X"], q["Y"], q["W"], q["H"], q["rgb"][0])
    exp = formula(k, hx, hy)
    anchor = q["xs"] == [q["X"], q["X"] + q["W"], q["X"], q["X"] + q["W"]] and \
        q["ys"] == [q["Y"], q["Y"], q["Y"] + q["H"], q["Y"] + q["H"]]
    grey = q["rgb"][0] == q["rgb"][1] == q["rgb"][2]
    return dict(k=k, s=16 - k, state=f["state"], step=f["step"], got=got, exp=exp, linked=linked, anchor=anchor,
                uv=q["uv"], code=q["code"], grey=grey, nodes=nodes)


def print_table(title, rows):
    print("\n%s\n" % title)
    print("| call | s | state after | X | Y | W | H | colour | = plan formula |")
    print("|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        X, Y, W, H, c = r["got"]
        print("| %d | %d | %d | %d | %d | %d | %d | %d | %s |" % (r["k"], r["s"], r["state"], X, Y, W, H, c,
                                                               "yes" if r["got"] == r["exp"] else "NO %s" % (r["exp"],)))


def scenario(hx, hy):
    cpu = CPU()
    install_image(cpu)
    cpu.ww(IDX, 0)
    place_head(cpu, hx, hy)
    start_main(cpu)
    f0 = fields(cpu)
    opening = [frame_row(cpu, k, open_formula, hx, hy) for k in range(1, 17)]
    rest_rows = [frame_row(cpu, 16, open_formula, hx, hy) for _ in range(30)]
    rest_nodes = rest_rows[-1]["nodes"]
    # guard: a start while state == 4 changes nothing
    snap = cpu.snapshot(PORT, PORT + 0x94)
    place_head(cpu, 10, 20)
    start_main(cpu)
    guard4 = cpu.snapshot(PORT, PORT + 0x94) == snap
    place_head(cpu, hx, hy)
    cpu.run(0x80057B84)                                   # the return, called directly
    f2 = fields(cpu)
    ret = []
    guard2 = None
    for k in range(1, 17):
        ret.append(frame_row(cpu, k, return_formula, hx, hy))
        if k == 5:                                        # a start during the return is ignored
            snap = cpu.snapshot(PORT, PORT + 0x94)
            start_main(cpu)
            guard2 = cpu.snapshot(PORT, PORT + 0x94) == snap
    after = [render_frame(cpu) for _ in range(2)]
    gone = all(PORT + 4 + i * 40 not in walk(cpu, ot) for i, ot in after) and fields(cpu)["state"] == 0
    snap = cpu.snapshot(PORT, PORT + 0x94)
    cpu.run(0x80057B84)
    guard_ret0 = cpu.snapshot(PORT, PORT + 0x94) == snap
    return dict(f0=f0, opening=opening, rest_rows=rest_rows, rest_nodes=rest_nodes, guard4=guard4, f2=f2, ret=ret,
                guard2=guard2, gone=gone, guard_ret0=guard_ret0)


def abs_refs(lo, hi, kinds):
    """Instructions that address [lo, hi) through a lui-based register (linear sweep, walks through j, stops
    after the delay slot of jr, forgets caller-saved registers at jal)."""
    n = T_SIZE // 4
    out = []
    for k in range(n):
        w = word(T_ADDR + 4 * k)
        if (w >> 26) != 0x0F:
            continue
        regs = {(w >> 16) & 31: (w & 0xFFFF) << 16}
        last = False
        for j in range(k + 1, min(n, k + 48)):
            p = T_ADDR + 4 * j
            x = word(p)
            op, rs, rt = x >> 26, (x >> 21) & 31, (x >> 16) & 31
            if op in kinds and rs in regs and lo <= ((regs[rs] + s16(x)) & M32) < hi:
                out.append((p, (regs[rs] + s16(x)) & M32))
            if last:                            # delay slot of a jr processed: stop
                break
            if op == 0x09 and rs in regs:
                regs[rt] = (regs[rs] + s16(x)) & M32
                continue
            if op == 0 and (x & 63) == 8:       # jr: end of the function; a j inside the function is walked through
                last = True
                continue
            if op == 3:
                for q in (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 24, 25, 31):
                    regs.pop(q, None)
            if op == 0:
                regs.pop((x >> 11) & 31, None)
            elif op in (0x20, 0x21, 0x23, 0x24, 0x25) or 0x08 <= op <= 0x0F:
                regs.pop(rt, None)
            if not regs:
                break
    return sorted(set(out))


def lui_addiu_refs(t):
    n = T_SIZE // 4
    out = []
    for k in range(n):
        w = word(T_ADDR + 4 * k)
        if (w >> 26) != 0x0F:
            continue
        rt, hi = (w >> 16) & 31, (w & 0xFFFF) << 16
        for j in range(k + 1, min(n, k + 12)):
            x = word(T_ADDR + 4 * j)
            if (x >> 26) == 0x09 and ((x >> 21) & 31) == rt and ((hi + s16(x)) & M32) == t:
                out.append(T_ADDR + 4 * j)
    return sorted(set(out))


def main():
    if "--listing" in sys.argv:
        for lo, hi in ((0x80055624, 0x800556C8), (0x80052618, 0x800526C8), (0x80048178, 0x800481DC),
                       (0x80056918, 0x8005698C), (0x8005362C, 0x8005369C), (0x80057B64, 0x80058200),
                       (0x80044C5C, 0x80044CC4), (0x8002C3F4, 0x8002C460), (0x8002BE54, 0x8002BE70)):
            print("  ---- %08x-%08x" % (lo, hi))
            listing(lo, hi)

    print("(a) the four portrait starts")
    exp = [(0x80055624, 0x8004BD9C), (0x8005562C, 0x80047F94), (0x80055634, 0x80057B40), (0x800556B0, 0x80057C18),
           (0x800556B8, 0x80055C84), (0x800556C0, 0x800490FC)]
    got = jals(0x80055624, 0x800556C8)
    check("a", got == exp and [delay_a0(p) for p, _ in got] == [None, 6, 0, None, None, 4],
          "DisplayInventory: " + "; ".join(fmt_call(p, t) for p, t in got))
    print("    DisplayInventory, earlier jals (guards and debug branches): " +
          "; ".join(fmt_call(p, t) for p, t in jals(0x80055570, 0x80055624)))
    exp = [(0x80052620, 0x8004BD9C), (0x80052628, 0x80047F94), (0x80052630, 0x80057B40), (0x800526AC, 0x80057C18),
           (0x800526B4, 0x800490FC)]
    got = jals(0x80052618, 0x800526C8)
    check("a", got == exp and [delay_a0(p) for p, _ in got] == [None, 4, 0, None, 4],
          "StartFadeOut 0x80052618: " + "; ".join(fmt_call(p, t) for p, t in got))
    got = jals(0x80048178, 0x800481DC)
    check("a", got == [(0x800481A8, 0x80052618), (0x800481D4, 0x80055570)]
          and word(0x80048188) == 0x24020001 and word(0x80048190) == 0x24020002
          and text(0x8004818C) == "bne $v1, $v0, 0x800481bc" and text(0x800481BC) == "bne $v1, $v0, 0x800481dc",
          "post-process [0x80153194]: ==1 -> %s ; ==2 -> %s" % (fmt_call(*got[0]), fmt_call(*got[1])))
    check("a", callers(0x80057C18) == [0x800526AC, 0x800556B0], "callers of 0x80057c18: %s" % [hex(c) for c in callers(0x80057C18)])
    check("a", callers(0x80052618) == [0x800481A8, 0x800555FC],
          "callers of 0x80052618: %s (0x800555fc = DisplayInventory debug branch)" % [hex(c) for c in callers(0x80052618)])
    check("a", callers(0x80055570) == [0x8002BCEC, 0x800481D4], "callers of 0x80055570: %s" % [hex(c) for c in callers(0x80055570)])

    print("(b) the four returns")
    for item_lo, item_hi, mask_at, mask, exp, extra in (
            (0x80056924, 0x80056948, 0x80056924, 0x813, [(0x80056930, 0x800556DC), (0x80056938, 0x80057B84), (0x80056940, 0x8004BE0C)], None),
            (0x80056950, 0x8005698C, 0x80056950, 0x00C, [(0x8005695C, 0x800556DC), (0x80056974, 0x80057B84)], (0x80056980, 1)),
            (0x80053634, 0x80053658, 0x80053634, 0x813, [(0x80053640, 0x800526CC), (0x80053648, 0x80057B84), (0x80053650, 0x8004BE0C)], None),
            (0x80053660, 0x8005369C, 0x80053660, 0x00C, [(0x8005366C, 0x800526CC), (0x80053684, 0x80057B84)], (0x80053690, 2))):
        got = jals(item_lo, item_hi)
        ok = got == exp and word(mask_at) == (0x30420000 | mask)
        post = ""
        if extra:
            ok = ok and word(extra[0]) == (0x24020000 | extra[1]) and word(extra[0] + 4) == 0xAC623194
            post = "; %08x post-process [0x80153194] = %d" % (extra[0] + 4, extra[1])
        check("b", ok, "%08x andi 0x%x -> %s%s" % (mask_at, mask, "; ".join(fmt_call(p, t) for p, t in got), post))
    check("b", callers(0x800556DC) == [0x80056930, 0x8005695C] and callers(0x800526CC) == [0x80053640, 0x8005366C],
          "0x800556dc callers %s ; 0x800526cc callers %s" % ([hex(c) for c in callers(0x800556DC)], [hex(c) for c in callers(0x800526CC)]))
    print("    callers of 0x80057b84: %s ; 0x80045f08 lies in function %08x (not an inventory path)"
          % ([hex(c) for c in callers(0x80057B84)], func_start(0x80045F08)))
    print("    callers of 0x8004be0c: %s" % [hex(c) for c in callers(0x8004BE0C)])

    print("(c)-(h) execution of the original code")
    check("c", text(0x80057D08) == "lh $v0, 0x70($v1)" and text(0x80057D30) == "bnez $v0, 0x80057e88"
          and text(0x80057E88) == "lw $ra, 0x3c($sp)", "guard: 0x80057d08 lh state; 0x80057d30 bnez -> 0x80057e88 (epilogue)")
    check("c", word(0x80057C38) == 0x240200F8 and word(0x80057C44) == 0x24020068 and word(0x80057C4C) == 0x24020030
          and word(0x80057C54) == 0x24020038 and word(0x80057D74) == 0x24020005 and word(0x80057E5C) == 0x2403000F
          and word(0x80057E4C) == 0x24030030 and word(0x80057E54) == 0x24030038,
          "0x80057c38 rest X 0xf8, 0x80057c44 rest Y 0x68, 0x80057c4c/54 args 0x30/0x38, 0x80057d74 state 5, "
          "0x80057e5c step 15, 0x80057e4c/54 size 0x30/0x38")
    check("d", all(text(p).startswith("lh ") and ", 2($" in text(p) for p in
                   (0x80057E20, 0x80057E40, 0x80057E48, 0x80057BAC, 0x80057BC4, 0x80057BCC))
          and all(text(p).startswith("lw ") and ", ($" in text(p) for p in (0x80057E24, 0x80057E3C, 0x80057BB0, 0x80057BC8))
          and word(0x80057E78) == 0x2484FFE0 and word(0x80057BFC) == 0x2463FFE0,
          "Pos read by lh 2(ptr) at 80057e20/40/48 and 80057bac/c4/cc; scroll by lw 0(ptr) at 80057e24/3c and "
          "80057bb0/c8; -0x20 only on Y at 80057e78 / 80057bfc")
    cpu = CPU()
    install_image(cpu)
    place_head(cpu, 160, 120)
    start_main(cpu)
    f = fields(cpu)
    check("d", f["ptrs"] == [0x80127E44, 0x80127E48, 0x80127E4C, 0x800E4328, 0x800E432C],
          "pointers stored by the start from DisplayInventory's real argument set-up: %s" % [hex(x) for x in f["ptrs"]])
    cpu2 = CPU()
    install_image(cpu2)
    place_head(cpu2, 160, 120)
    start_sub(cpu2)
    check("a", cpu2.snapshot(PORT, PORT + 0x94) == cpu.snapshot(PORT, PORT + 0x94),
          "sub-inventory open (0x80052630..) leaves the block byte-identical to the main open")

    tables = {}
    for hx, hy in ((160, 120), (300, 60)):
        sc = scenario(hx, hy)
        tables[(hx, hy)] = sc
        f0, f2 = sc["f0"], sc["f2"]
        tag = "head (%d,%d)" % (hx, hy)
        check("c", f0["state"] == 5 and f0["step"] == 15 and f0["rest"] == REST and f0["size"] == (48, 56),
              "%s after start: state %d, step %d, rest %s, size %s" % (tag, f0["state"], f0["step"], f0["rest"], f0["size"]))
        check("d", f0["p68"] == (hx, hy) and f0["p70"] == REST and f0["delta"] == (hx - 248, hy - 104),
              "%s start: +0x68 = %s (head point, no +2), +0x70 = %s, +0x78 = %s" % (tag, f0["p68"], f0["p70"], f0["delta"]))
        check("e", all(r["got"] == r["exp"] for r in sc["opening"]), "%s opening: 16 calls equal the plan formula" % tag)
        check("e", f2["state"] == 2 and f2["step"] == 15 and f2["p70"] == (hx, hy) and f2["p68"] == REST
              and f2["delta"] == (248 - hx, 104 - hy),
              "%s return: state %d, step %d, +0x70 = %s (head re-read), +0x68 = %s" % (tag, f2["state"], f2["step"], f2["p70"], f2["p68"]))
        check("e", all(r["got"] == r["exp"] for r in sc["ret"]), "%s return: 16 calls equal the plan formula" % tag)
        fl = [r["k"] for r in sc["opening"] if floor_open(r["k"], hx, hy) != r["got"][:2]]
        print("    %s: a floor division would give other X/Y at opening calls %s" % (tag, fl))
        check("f", all(r["state"] == 4 and r["got"] == (248, 104, 48, 56, 128) and r["linked"] for r in sc["rest_rows"])
              and sc["opening"][-1]["state"] == 4,
              "%s: state 4 from call 16, then 30 more frames unchanged at (248,104) 48x56, drawn" % tag)
        check("f", sc["ret"][-1]["state"] == 0 and all(r["state"] == 2 for r in sc["ret"][:-1]) and sc["gone"],
              "%s: state 2 for calls 1-15, 0 at call 16, then nothing linked" % tag)
        check("c", sc["guard4"] and sc["guard2"] and sc["guard_ret0"],
              "%s: start ignored in state 4 and in state 2; return ignored in state 0" % tag)
        rows = sc["opening"] + sc["rest_rows"] + sc["ret"]
        check("g", all(r["anchor"] for r in rows) and all(r["uv"] == [(0x40, 0x80), (0x70, 0x80), (0x40, 0xB8), (0x70, 0xB8)] for r in rows),
              "%s: vertices (X,Y) (X+W,Y) (X,Y+H) (X+W,Y+H); UVs fixed (u,v)-(u+48,v+56) on every call" % tag)
        check("h", all(r["code"] == 0x2C and r["grey"] for r in rows),
              "%s: code byte 0x2c on every call; colour ramps open %s, return %s" %
              (tag, [r["got"][4] for r in sc["opening"]], [r["got"][4] for r in sc["ret"]]))
        check("i", [label(a) for a in sc["rest_nodes"]][:13] ==
              ["OT[0]", "dr0", "OT[1]", "dr1", "OT[2]", "dr2", "OT[3]", "drawarea", "dr3", "PORTRAIT", "OT[4]", "dr4", "OT[5]"],
              "%s: UI OT walk from entry 0: %s" % (tag, " > ".join(label(a) for a in sc["rest_nodes"])))

    cpu = CPU()                                           # re-read at exit: the head moves before the return
    install_image(cpu)
    place_head(cpu, 160, 120)
    start_main(cpu)
    for _ in range(20):
        render_frame(cpu)
    place_head(cpu, 40, 200, sx=-5, sy=300, pz=-3)
    cpu.run(0x80057B84)
    rows = [frame_row(cpu, k, return_formula, 40, 200) for k in range(1, 17)]
    check("e", fields(cpu)["state"] == 0 and all(r["got"] == r["exp"] for r in rows),
          "return re-reads the player and the camera at exit: head (160,120) at open, (40,200) at exit -> return "
          "table of (40,200); call 15 = %s" % (rows[14]["got"],))

    print("(g)/(h) static")
    check("h", word(0x800843B0) == 0x34020009 and word(0x800843B8) == 0x3402002C and word(0x800843C0) == 0xA0820007
          and jal_target(0x80057D80) == 0x800843B0,
          "SetPolyFT4 0x800843b0: len 9, code 0x2c (bit1 = 0: no semi-transparency; bit0 = 0: modulated)")
    print("    jal targets inside 0x80057b40-0x80058200: %s" % sorted({hex(t) for _, t in jals(0x80057B40, 0x80058200)}))
    st = [p for p, a in abs_refs(PORT, PORT + 2, (0x29,))]
    check("f", st == [0x80057B68, 0x80057B98, 0x80057BE8, 0x80057D78, 0x80057F04, 0x80057F20],
          "all 'sh' to the state 0x80180070: %s" % [hex(p) for p in st])
    print("    0x80057b64 (reset: state 0, rest (8,120)) callers %s in function %08x, itself called from %s"
          % ([hex(c) for c in callers(0x80057B64)], func_start(0x80044C40), [hex(c) for c in callers(func_start(0x80044C40))]))

    print("(i) the UI ordering table")
    check("i", text(0x80044C88) == "lui $s0, 0x8014" and text(0x80044C90) == "addiu $s0, $s0, 0x6f58"
          and text(0x80044CAC) == "addiu $s1, $s0, 0xc" and jal_target(0x80044CBC) == 0x80058134
          and text(0x80044CC0) == "addu $a0, $a0, $s1",
          "DisplayUserInterface passes OT 0x80146f58 + 0xc (entry 3) + idx*40 to 0x80058134 (0x80044cac/0x80044cbc)")
    ot_ok = RAW[0x800 + 0x8002A1B0 - T_ADDR:].startswith(b"ClearOTag(") and RAW[0x800 + 0x8002A1E0 - T_ADDR:].startswith(b"DrawOTag(")
    check("i", ot_ok and text(0x8008517C) == "addiu $a0, $s0, 4" and jal_target(0x80044F70) == 0x8008511C
          and word(0x80044F5C) == 0x2405000A,
          "0x8008511c = ClearOTag (libgpu string at 0x8002a1b0), forward: ot[i] -> &ot[i+1] (0x8008517c); UI OT cleared with n = 10")
    check("i", [jal_target(p) for p in (0x8002BA6C, 0x8002BA74, 0x8002BA7C, 0x8002BA84, 0x8002BA8C, 0x8002BA9C)] == [0x800852CC] * 6
          and text(0x8002BA98) == "lw $a0, -0x3f68($v0)" and text(0x8002BE70) == "sw $v0, -0x3f68($v1)"
          and jal_target(0x8002BE64) == 0x80044C5C,
          "0x800852cc = DrawOTag (string at 0x8002a1e0): five world OTs, then DrawOTag(UI OT entry 0) at 0x8002ba9c "
          "(pointer 0x800dc098 = DisplayUserInterface's return, stored at 0x8002be70)")
    per_entry = {k: [] for k in range(10)}
    for p, a in abs_refs(UI_OT, UI_OT + 80, (0x09,)):
        if (a - UI_OT) % 4 == 0:
            per_entry[((a - UI_OT) % 40) // 4].append(p)
    for k in range(10):
        print("    constant address of OT[%d] (lui + addiu chain): %s" % (k, " ".join("%08x" % p for p in per_entry[k])))
    check("i", per_entry[3] == [0x80044CAC], "OT[3] is formed as a constant only at 0x80044cac (DisplayUserInterface)")
    check("i", word(0x800851A8) == 0x3C02800D and word(0x800851AC) == 0x244281F0,
          "ClearOTag closes the table on 0x800c81f0 (0x800851a8/ac)")

    print("(j) the clock")
    check("j", jal_target(0x8002C3FC) == 0x8002BD60 and jal_target(0x8002C404) == 0x8002BAEC and word(0x8002C408) == 0x00002021
          and text(0x8002C45C) == "beqz $v0, 0x8002c3f4" and jal_target(0x8002C3E4) == 0x8002BAEC and delay_a0(0x8002C3E4) == 1,
          "loop 0x8002c3f4..0x8002c45c: 0x8002c3fc RenderScene, then 0x8002c404 Update(0); Update(1) once before at 0x8002c3e4")
    check("j", jal_target(0x8002BE5C) == 0x80048054 and jal_target(0x8002BE64) == 0x80044C5C
          and text(0x80058180) == "lh $v1, 0x70($v0)" and text(0x80058188) == "beqz $v1, 0x800581e8"
          and jal_target(0x80058190) == 0x80057EBC,
          "RenderScene: 0x8002be5c UiCallbacksAndPostProcess, 0x8002be64 DisplayUserInterface -> 0x80058134: state != 0 -> step 0x80058190")
    check("j", func_start(0x8002BCEC) == 0x8002BAEC and func_start(0x800481A8) == 0x80048054
          and func_start(0x800481D4) == 0x80048054,
          "the main open (jal DisplayInventory at 0x8002bcec) is inside Update 0x8002baec; the post-process starts "
          "(0x800481a8, 0x800481d4) are inside 0x80048054, called by RenderScene at 0x8002be5c")
    check("j", text(0x80048138) == "addiu $s0, $v0, 0x3028" and text(0x80048150) == "lw $v0, 0x14($s0)"
          and text(0x80048160) == "jalr $v0" and word(0x8004816C) == 0x2A22000D
          and text(0x80048174) == "addiu $s0, $s0, 0x1c" and lui_addiu_refs(0x80056598) == [0x80055560]
          and text(0x80055564) == "sw $v0, 0x14($v1)" and word(0x800A73A0) == 0x80053328
          and func_start(0x80056924) == 0x80056598 and func_start(0x80053634) == 0x80053328,
          "0x80048054 runs 13 callback slots (0x80153028, stride 0x1c, fn at +0x14, jalr 0x80048160) BEFORE the post-process; "
          "the close/L1R1 code lives in callbacks 0x80056598 (installed at 0x80055564) and 0x80053328 (descriptor 0x800a73a0)")

    print("\nsummary: %d checks, %d failed" % (len(RESULTS), sum(1 for r in RESULTS if not r[1])))
    for (hx, hy), sc in tables.items():
        print_table("Opening, head point (%d,%d), span X %+d Y %+d" % (hx, hy, hx - 248, hy - 104), sc["opening"])
        print_table("Return, head point (%d,%d) re-read at exit" % (hx, hy), sc["ret"])


if __name__ == "__main__":
    main()
```
