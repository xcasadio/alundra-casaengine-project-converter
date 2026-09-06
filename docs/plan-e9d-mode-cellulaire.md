# Plan — E9.d : le mode cellulaire des fonds

Chantier ouvert le 2026-09-06, inscrit devant E13 sur ta décision. E9 a porté **un** des deux modes de
rendu des fonds. Le second, le mode **cellulaire** (`Mode == 2`), n'a jamais été porté :
`docs/formats/backdrops.md:117` l'annonce — « paramètres bruts exportés, rendu non implémenté ». Les
cartes concernées n'affichent aucun fond.

**Reconnaissance** : cinq surfaces disjointes lues en parallèle (rendu d'origine, données mesurées,
convertisseur, moteur, DLL), puis quatre relecteurs adverses chargés de réfuter les conclusions
porteuses. Deux ont corrigé des chiffres, **aucun n'a réfuté la conclusion d'architecture**. Tout ce
qui suit porte sa preuve.

**Aucune ligne de code n'a été écrite.** Ce plan est soumis avant exécution.

**Révision 2** — la révision 1 a été relue et a reçu deux blocages, tous deux retenus. (1) Le §1.5
affirmait que le seau d'avant-plan était « l'inverse d'E9.b » : c'est faux, 86 des 132 couches du mode
1 l'utilisent déjà et la politique existe. La section est corrigée et un septième point, P7, dispose
explicitement de cette politique au lieu de la laisser sans propriétaire. (2) Le chiffrage de la
réserve de P1 était **inversé** — la dérive continue concerne 17 cartes et non 8 — et aucune des trois
cartes proposées n'exerçait le piège du OU de signes que le plan documente lui-même.

---

## 0. Ce que le tri a corrigé de nos propres plans

**Le nombre de cartes concernées est faux partout où il est écrit : ce sont 90 cartes, pas 84.**
Deux sources indépendantes concordent — le champ `ScrollParameters.Infos.ModeLayer` sur les 483
cartes de `data-extracted`, et les 330 compagnons `*.backdrop.json` produits par le convertisseur —
et un relecteur adverse a refait les deux comptages lui-même. Précisément : **90 cartes, 92 couches
cellulaires, 8360 cellules**. À corriger dans `plan-e9-backdrops-residus.md:38` et dans
`plan-conversion-totale.md` (section E9.d et tableau §6).

---

## 1. Les faits établis

### 1.1 Ce qu'est une cellule

Le mode 2 n'est pas une grille. Chaque couche porte jusqu'à 200 rectangles indépendants, découpés
dans la **même planche de tuiles 256×256 en 4 bits par pixel** que le mode 1, chacun dessiné comme
son propre sprite et entrant dans la **même liste de sprites triés par profondeur** que tout le reste
du jeu.

- **Champs d'une cellule** (`ScrollScreen.cs:487-533`) : `PalDex`, `U0/V0/U1/V1` (rectangle en octets
  dans la planche), `Type`, `X0/Y0` (position nominale, `Int16`), `CamXNum/CamXDen/CamYNum/CamYDen`
  (parallaxe caméra **par cellule**), `DX/PeriodX/DY/PeriodY` (dérive et période, **par cellule**),
  plus deux octets morts `Unused0/Unused1`.
- **Taille à l'écran** = `U1-U0+1` × `V1-V0+1`, par cellule, jamais fixe
  (`GraphicManager.cs:1097-1098`, `:1183-1184`, `:1197-1198` — les trois branches calculent pareil).
- **Nombre de cellules** = `min(200, CountBase + Divisions)` (`ScrollScreen.cs:228`). Mesuré :
  `CountBase = 0` sur **toutes** les couches du corpus, donc le compte vaut `Divisions`, entre 15 et
  120, mode 120 sur 65 couches. Le chemin de code qui gère l'écart `CountBase`/`Divisions` n'est
  jamais exercé par les données livrées.

### 1.2 Quatre types, dont un seul compte vraiment

`CellType` (`ScrollScreen.cs:369-375`) : `Normal = 0`, `ScriptTrack = 1`, `FallRespawn = 2`,
`WaveX = 4` — **il n'y a pas de valeur 3**.

| Type | Cellules | Couches | Comportement |
|---|---|---|---|
| `WaveX` | **7800** | 65 | bandes de 4 px de haut, position X calculée depuis le `WaveLut` |
| `Normal` | 450 | 25 | dérive + période, parallaxe caméra, enroulement 320×240 |
| `FallRespawn` | 110 | 2 | même dérive, réapparition en X aléatoire en haut |
| `ScriptTrack` | **0** | 0 | `case` au corps vide : **ne dessine rien** (`GraphicManager.cs:1104-1105`) |

**`WaveX` pèse 94 % des cellules et fait toujours exactement 4 pixels de haut.** L'usage réel du mode
est donc un **bandeau ondulant** — une pile de fines bandes horizontales déphasées — et non un semis
de sprites.

### 1.3 Le `WaveLut`, enfin situé

`int32[256]` par carte, lu une fois au chargement (`ScrollScreen.cs:18,38,171-181`). **Trois sites de
lecture dans tout le moteur, tous dans le `case WaveX`** (`GraphicManager.cs:1192,1202,1209`), et
**zéro lecteur dans le chemin des tuiles**. C'est ce qui justifiait de le sortir d'E9 : il
n'appartient pas au mode 1.

`WaveX` est **sans état** : son X vient de deux échantillonnages du `WaveLut` — l'un indexé par le
`Y0` propre à la cellule, l'autre par un tic de couche — combinés par une formule fixe
(`GraphicManager.cs:1200-1219`). Il ne déplace **jamais** la cellule en Y.

### 1.4 Deux pièges de fidélité, relevés par la relecture adverse

- **Le sens du pas de période n'est pas celui du mode 1.** Le mode cellulaire calcule
  `(DX < 0 || PeriodX < 0) ? -1 : +1` — un **OU** de signes, **recalculé à chaque frame**
  (`GraphicManager.cs:1033,1044`). Le mode 1, dans le même arbre source, utilise un **OU EXCLUSIF**
  de signes calculé **une fois au chargement**. Transposer la formule du mode 1 donnerait un
  comportement faux.
- **Les cadences ne tournent pas inconditionnellement.** Le `WaveTick` de couche et le compteur
  d'animation n'avancent que lorsque `RenderLayerToBuffer` est appelé pour cette couche à cette
  frame, ce qui est conditionné. Le premier lecteur l'avait énoncé sans condition ; le relecteur l'a
  corrigé.

### 1.5 Profondeur

**Les 92 couches cellulaires du corpus ont `Ground = true`** : elles atterrissent dans le seau
d'avant-plan et sont dessinées **après** toutes les entités, tuiles et murs, juste sous l'interface.

**Correction de relecture** : la première rédaction en concluait que c'était « l'inverse d'E9.b, où
les fonds passaient derrière ». **C'est faux.** Mesuré sur les compagnons exportés : **86 des 132
couches du mode 1 portent déjà `Ground = true`** (contre 46 à `false`), et `AlundraBackdropStage`
route déjà ce cas — `Alundra/Scripts/AlundraBackdropStage.cs:196-197` :

```csharp
var (blendMode, tint) = ResolveGroundLayerBlend(layer.Ground, layer.BlendMode);
var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;
```

Le seau d'avant-plan est donc le **chemin majoritaire déjà livré et validé en jeu**, pas une
nouveauté. La politique de passe, de fusion et de teinte existe et fonctionne ; E9.d n'a pas à la
réinventer. Voir P7.

### 1.6 Un effet, pas quatre-vingt-dix

Comparaison **octet par octet** des blocs `(Cellular + Cells[])` : **7 blocs distincts** dans tout le
corpus, répartis en 65 / 13 / 8 / 2 / 2 / 1 / 1 cartes. Le bloc `WaveX` est réutilisé **à l'identique
sur 65 cartes** ; le bloc « pluie » `FallRespawn` est partagé par exactement deux cartes, la 31 et la
391 (égalité vérifiée par comparaison directe, pas par similitude de forme).

**Conséquence de cadrage** : faire marcher le bloc dominant fait marcher 65 cartes d'un coup.

### 1.7 Ce que le convertisseur fait, et ne fait pas

- **Les paramètres sortent, complets.** Un `BackdropLayerDocument` de mode `"Cellular"` porte les 8
  champs de `Cellular` et les 16 champs utiles de chaque `Cell`, copiés 1:1 (`BackdropReader.cs`
  `:311-353`, `:330-348`) ; le `WaveLut` sort au niveau document (`:260-261`). Seuls les deux octets
  morts sont écartés, et c'est documenté.
- **Aucun pixel ne sort.** `TextureAssetId` / `Width` / `Height` valent `null` / `0` / `0` sur
  **toutes** les couches cellulaires. Ce n'est pas conditionnel : `BackdropWriter.cs:138-141` passe
  son chemin sur toute couche non-`"Tiles"` **avant** le moindre travail de texture.
- **La planche et les palettes n'atteignent jamais le JSON.** `TileSheetImageData` (32 768 octets) et
  `PaletteWords` (8 × 16) sont lus dans `BackdropReadResult` (`:258-259`, déclarés `:179-180`), mais
  `BackdropDocument` — le seul type sérialisé (`BackdropWriter.cs:205`) — n'a **aucune propriété**
  pour eux. Sans cette table, le `PalDex` d'une cellule ne veut rien dire.

### 1.8 Ce que le moteur et la DLL font aujourd'hui

- **Le moteur ne connaît pas le mot.** Zéro occurrence de `cellular` dans tout `CasaEngineMonogame`.
- **Le mécanisme d'E9.b ne peut pas porter des cellules.** `ScrollingLayerDefinition` et
  `ScrollingLayerService.LayerRuntime` portent **un** accumulateur et **un** jeu de textures entières
  **par couche** ; `ScrollingLayerComponent.Submit` dessine toujours `frame.Bounds` — la texture
  entière — pavée 2×2 sur une toile fixe de 640×480. Une cellule a besoin d'un état par sprite, d'un
  rectangle source arbitraire et d'une parallaxe propre. **Le relecteur chargé de démolir ce verdict
  ne l'a pas démoli ; il l'a renforcé.**
- **La DLL ne voit même pas les données.** Son `BackdropDocument` n'a aucune propriété `Cellular` ni
  `WaveLut` : `System.Text.Json` les jette silencieusement à la désérialisation, avant qu'aucun code
  Alundra ne tourne. `AlundraBackdropStage.BuildDefinitions` passe son chemin sur toute couche non
  `"Tiles"`, **sans log et sans risque d'exception**.

---

## 2. Points à valider — je propose, tu tranches

Ce plan ne va pas plus loin que ces **sept** points : les tranches en dépendent.

- **P1 — Les cartes de recette.** Aucune carte déjà validée n'exerce ce mode. Je propose **420**
  (maison de la côte, Nava : cellulaire pure, 120 cellules `WaveX` de 160×4 empilées, fusion
  additive — **c'est le bloc des 65 cartes**), **391** (Ship Klark nuit : 55 cellules `FallRespawn`
  de 1×64, `DY` entre 7 et 10 — de la pluie, et **même zone que la 389** d'entrée de jeu), et **271**
  (Inoa : 15 cellules `Normal` de 40×40, parallaxe 1:1).
  **Réserve, rectifiée par la relecture — le premier chiffrage était inversé.** `Normal` (450 cellules,
  25 cartes) se scinde en **période seule : 120 cellules sur 8 cartes** (96, 97, 98, 99, **271**, 289,
  357, 481) et **dérive continue : 330 cellules sur 17 cartes** (20, 55-60, 123, 124, 293, 443-448,
  461). C'est donc la variante **majoritaire** qui serait non couverte, pas une variante marginale :
  la geler gèlerait 17 cartes, pas 8.
  **Et une seconde lacune, plus gênante** : le piège du OU de signes (§1.4) ne diverge du mode 1 que
  lorsque `DX < 0 && PeriodX < 0` — 214 cellules sur 14 cartes en X, 215 sur 14 cartes en Y. **Aucune
  des trois cartes proposées ne l'exerce** (la 271 a `DX = 0, PeriodX = -3`, où les deux formules
  coïncident). Le piège que le plan documente n'aurait donc **aucun témoin en jeu**.
  Je propose d'ajouter une **quatrième carte de recette prise parmi les 17**, la **443** ou la **20**,
  choisie pour exercer à la fois la dérive continue et la divergence de signes.
- **P2 — La cuisson.** Je propose la **planche entière par palette utilisée** : décoder une fois la
  planche 256×256 par `PalDex` employé, jusqu'à 8 textures par carte. Aucun remappage d'UV — les
  `U0/V0/U1/V1` existants restent valides tels quels. L'alternative, un atlas des seuls rectangles
  utilisés, exigerait d'écrire un empaqueteur de rectangles qui **n'existe nulle part** dans le
  convertisseur. Correction de relecture à porter au budget : `BackdropImageBuilder.DrawTile`
  (`:127-129`) n'a **pas** de paramètres largeur/hauteur — c'est une tuile 16×16 fixe. Sa
  généralisation est un vrai petit changement, pas une réutilisation gratuite.
- **P3 — L'architecture moteur.** Je propose un **couple service + composant distinct**
  (`CellularLayerService` / `CellularLayerComponent`), partageant avec E9.b les seules fonctions
  pures et la plomberie `RenderSortKey2D` / `SpriteBlendMode`. Étendre `ScrollingLayerDefinition`
  pour porter un tableau de sous-sprites déformerait un mécanisme livré, validé en jeu et couvert.
- **P4 — Le découpage.** Je propose la cuisson en **tranche préalable**, prouvée par manifeste et
  double export avant qu'une ligne de moteur ne soit écrite : sans pixels, rien n'est observable.
- **P5 — `ScriptTrack`.** Zéro occurrence sur 8360 cellules, et un `case` vide dans l'original. Je
  propose de le **geler et le documenter**, comme D-E9b-14 a gelé `0xA4`.
- **P6 — Le générateur aléatoire de `FallRespawn`.** La réapparition tire un X au hasard. Il faut
  décider d'une source reproductible avant d'écrire la moindre acceptation sur la 391, sinon aucun
  test ne pourra épingler quoi que ce soit.
- **P7 — La politique de passe et de fusion** (point ajouté par la relecture). Les 92 couches
  cellulaires sont toutes `Ground = true`, cas déjà routé par `ResolveGroundLayerBlend` et
  `RenderPass2D.Effects` pour 86 couches du mode 1, livré et validé en jeu. Je propose de **réemployer
  cette politique telle quelle**, sans la dupliquer ni la paramétrer autrement. Elle devient un point
  parce que c'est un choix, pas une évidence : si tu préfères que le mode cellulaire ait sa propre
  politique, il faut le dire avant C2.

---

## 3. Tranches — forme prévue, à figer après P1–P7

- **C0 — le compte corrigé** : 84 → 90 dans les deux plans. Documentaire, sans code.
- **C1 — convertisseur** : cuisson de la planche et des palettes, référence de texture sur la couche
  cellulaire, format documenté. Preuve par export complet et manifeste, double export ⊆ `{report.json}`.
- **C2 — moteur** : le couple service + composant, sans MonoGame pour le service, testé sur les
  formules pures — le pas de période en **OU** de signes (§1.4) mérite son propre test.
- **C3 — DLL** : les propriétés manquantes sur son `BackdropDocument`, la construction des cellules,
  la poussée par frame.
- **C4 — recette en jeu** sur les cartes retenues en P1.

## 4. Acceptation

À chiffrer une fois P1 tranché. Le principe : **numérique d'abord** — les formules du mode cellulaire
sont pures et se testent sans écran — **puis visuel** sur les cartes de recette, avec la même
discipline de capture qu'E9.b (rafale, médiane par pixel, garde de fenêtre au premier plan).

## 5. Arrêts

- Une modification devient nécessaire dans `alundra-project/` à la main.
- `CasaEngine.Launcher/Program.cs` se retrouve modifié ou indexé.
- La cuisson pleine planche fait exploser la taille de l'export au-delà du raisonnable.
- Un test moteur vert devient rouge sans lien immédiat avec le chantier.
- La fidélité exige de porter le tri global des sprites de l'original, et non de s'appuyer sur
  `RenderSortKey2D`.

## 6. Journal

_(à remplir en cours de chantier)_
