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
  courante, l'écart, le pas, la demi-taille, le repos. Point d'entrée de l'inventaire `0x80057c18` : repos
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
- **L'horloge.** Chaque itération de la boucle principale exécute `RenderScene` (`0x8002c3fc`) **avant** `Update`
  (`0x8002c404`). `RenderScene` → `DisplayUserInterface` (`0x80044c5c`) → `DisplayInventoryCharacterPortrait`
  (`0x80058134`) : si l'état est non nul, un pas (`0x80057ebc`) puis l'ajout du quad. Un départ demandé pendant
  l'`Update` de l'image N fait donc son premier pas (0×0) dans le rendu de l'image N+1 ; la croissance est visible dès
  N+2 et le repos dès N+16. Le retour dure lui aussi 16 appels ; il se termine avant la fin du glissement de sortie
  (~18 images), si bien que le départ du menu suivant trouve toujours l'état à 0.
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

| Réf | Proposition | Raison |
|---|---|---|
| P1 | **API MGUI** : deux nouveaux attributs XAML de l'élément, `RenderTransformTranslation` et `RenderTransformScale`, de type chaîne au DTO (littéral `"x,y"` analysé par `AnimationXamlParser.ParseVector2` et appliqué à `MGElement.RenderTransform` après le DTO `RenderTransform`). Liés, ils sont renommés par `BindingPathMappings` en `RenderTransform.Translation` et `RenderTransform.Scale`. **Cible d'exécution** : l'instance `UIRenderTransform` de l'élément, atteinte par le chemin imbriqué (aucun changement de hiérarchie de type, `UIRenderTransform` reste `sealed`). **Type de valeur** : `Vector2` côté view-model comme côté cible, donc copie typée sans conversion. Le DTO `RenderTransform` et l'attribut `RenderScale` ne changent pas. ADR de MGUI. | La voie des objets imbriqués liables exigerait que `UIRenderTransform` dérive de `XAMLBindableBase` (§1.5) ; celle-ci réutilise le chemin cible imbriqué qui existe déjà et pousse sans allocation (ADR-0016 de MGUI). |
| P2 | **Chemin des données** : l'extracteur ajoute au `GameMap` un champ `InventoryPortrait` (le `SiImage` de l'enregistrement 0), rempli **pour la carte globale seulement** et marqué `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` : il est écrit dans `map_alundra.json` et **omis** de chaque `map_<n>.json`, qui reste identique à l'octet. Le portrait entre aussi dans l'atlas. Le convertisseur en fait un `.sprite` par le chemin existant et écrit `Data/inventory-portrait.json` (l'identifiant du sprite), que la DLL lit comme `item-icon-index.json`. ADR du dépôt parent, qui consigne aussi l'omission des valeurs nulles. | Changement minimal ; aucune autre carte ni aucun autre portrait ne bouge ; même contrat que les icônes d'objets. |
| P3 | **Un seul état de portrait**, partagé par les deux directeurs, comme le bloc unique de l'original. | Un départ du sous-inventaire doit voir l'état laissé par le retour du principal. |
| P4 | **Le pas du portrait se fait au début du tick, avant les directeurs**, comme `RenderScene` avant `Update` ; le présentateur pousse la valeur du pas après les directeurs. | Premier pas au tick N+1 d'un départ au tick N, croissance visible dès N+2, repos dès N+16, comme l'original. |
| P5 | **Rien n'est supprimé hors du dépôt** : l'ancien remaster est renommé `remaster-data-extracted.bak-2026-09-19` ; `data-extracted/` et `alundra-project/` sont sauvegardés avant d'être réécrits ; les dossiers d'extraction restent en place (§5.2). | Retour arrière possible à chaque étape. |

---

## 3. Tranches

Un commit par tranche, avec la mise à jour de ce plan ; message en anglais. **Chaque tranche ne commence que lorsque
ses prérequis sont clos.** Vérificateur frais aux frontières à risque (PI3, PI6, PI9). Statuts : ⏳ à faire · 🚧 en
cours · 🧪 à tester · ✅ fait · ⚠️ bloqué.

### ✅ PI0 — Les mesures (lecture seule) — close à la rédaction

Tout le §1. Scripts de mesure de la reconnaissance dans le scratchpad de la session (`portrait-discovery/`) ; celui
de la contre-vérification (PI1) sera recopié au §7.

### ⏳ PI1 — Contre-vérification indépendante des faits [binaire] (lecture seule)

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

### ⏳ PI4 — Analyseur : le portrait dans l'atlas et dans `map_alundra.json`

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

### ⏳ PI5 — Convertisseur : le sprite du portrait et son index

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

### ⏳ PI7 — DLL : la machine du portrait (logique pure, testée)

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

### ⏳ PI8 — DLL : le branchement et les deux écrans

**Prérequis** : PI3, PI6, PI7. **Dépôt** : parent (et les écrans versionnés d'`alundra-project/UI/Screens/`).
- Un seul état partagé (P3), attaché au monde comme les directeurs. Départ aux deux ouvertures (principal : après
  l'équivalent d'`InitializeHudPosition`/`SetTransitionType(6)`, avant les noms et le son 4 ; sous-inventaire :
  `OpenFromPostProcess`, même place). Retour aux quatre sorties (deux fermetures, deux bascules), après le glissement de
  sortie et avant `InitializeHudPositionBeforeHide`, comme l'original.
- Le pas au début du tick, avant les directeurs (P4) ; le présentateur pousse X, Y, W, H, visible.
- Les deux écrans : une `Image` du portrait placée dans l'ordre de dessin du §1.1 (après les boîtes et les textes,
  avant les icônes, les chiffres et le curseur), en `CanvasLeft = 248`, `CanvasTop = 104`, sa source liée à
  l'identifiant de `Data/inventory-portrait.json`, sa translation (X − 248, Y − 104) et son échelle (W/48, H/56) liées
  (PI3), sa visibilité liée ; données de conception mises à jour.
- Tests : le tick du premier pas (départ en N → 0×0 en N+1, visible en N+2, repos en N+16) ; le retour se termine avant
  que l'écran ne soit retiré ; bascule dans les deux sens (le départ du menu suivant trouve l'état à 0) ; portrait
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
