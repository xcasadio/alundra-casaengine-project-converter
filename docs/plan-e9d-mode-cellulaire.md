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

**Révision 3** — relecture de clôture, deux blocages de plus, tous deux retenus. (1) Parmi les deux
quatrièmes cartes proposées, **la 20 n'exerce pas la divergence de signes** : toutes ses valeurs sont
positives. D1 épingle la 443. (2) **La fenêtre source d'une cellule est animée** — `(V0 + phase) &
0xFF` — mécanisme entier absent de la révision 2, désormais §1.5 bis, et qui tranche D2. Quatre
corrections mineures de la relecture sont portées au fil du texte. Les sept points sont devenus les
six décisions D1-D6 plus P6.

**Plafond de relecture atteint** : deux verdicts REVISE consécutifs. Conformément à la règle, les
blocages sont dispositionnés — tous en correction — et le plan n'est pas resoumis à un troisième
tour ; il passe à l'auteur.

---

## 0. Ce que le tri a corrigé de nos propres plans

**Le nombre de cartes concernées est faux partout où il est écrit : ce sont 90 cartes, pas 84.**
Deux sources indépendantes concordent — le champ `ModeLayer` — `ScrollParameters.Infos` dans les données brutes, `LiningInfos.ModeLayer` côté convertisseur (`BackdropReader.cs:79`) — sur les 483
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

### 1.5 bis La fenêtre source est animée, elle aussi

**Manqué par la première rédaction, relevé par la relecture de clôture.** Les trois branches de
dessin n'échantillonnent pas la planche à `V0`, mais à `(V0 + phase) & 0xFF`, avec
`phase = (AnimFrameCounter[layerId] << 8) / AnimNum` — calculée en `GraphicManager.cs:1010`, appliquée
en `:1099`, `:1185` et `:1219`.

**`V0` seul ne détermine donc pas la fenêtre source** : elle défile verticalement et **enroule modulo
256**. Le mécanisme est vivant sur deux des quatre cartes de recette retenues : la **271**
(`AnimNum = 2`, phases 0 et 128) et la **391** (`AnimNum = 4`, phases 0, 64, 128, 192).

**C'est le fait qui tranche la cuisson (D2).** Un atlas des seuls rectangles utilisés ne survivrait
pas à un décalage V qui enroule — il faudrait re-packer à chaque phase. La planche entière l'encaisse
sans rien remapper. La phase appartient à la tranche **C2**, qui la calcule par couche et par frame.

### 1.6 Un effet, pas quatre-vingt-dix

Comparaison **octet par octet** des blocs `(Cellular + Cells[])` : **7 blocs distincts** dans tout le
corpus, répartis en 65 / 13 / 8 / 2 / 2 / 1 / 1 **couches** (la somme fait 92 : les cartes 123 et 124 en portent deux chacune). Le bloc `WaveX` est réutilisé **à l'identique
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

- **Le moteur ne connaît pas le mot.** Aucune occurrence de `cellular` dans le **code** de `CasaEngineMonogame` — la seule du dépôt est en prose, `docs/engine/scrolling-layers.md:10`.
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

## 2. Décisions verrouillées (2026-09-06) et le point qui reste

Arbitrées par l'auteur après lecture de la révision 2. **Ce plan les applique, il ne les rediscute
pas.**

| Réf | Décision |
|---|---|
| **D1** | **Quatre cartes de recette** : **420** (bandeau `WaveX`, le bloc réutilisé sur 65 couches), **391** (pluie `FallRespawn`, même zone que la 389 d'entrée de jeu), **271** (`Normal` période seule), et **443** pour la dérive continue. |
| **D2** | **Cuisson planche entière par palette utilisée** : décoder la planche 256×256 une fois par `PalDex` employé, jusqu'à 8 textures par carte. Aucun remappage d'UV. L'atlas des seuls rectangles utilisés est écarté. |
| **D3** | **Couple service + composant distinct** (`CellularLayerService` / `CellularLayerComponent`), partageant avec E9.b les seules fonctions pures et la plomberie de tri et de fusion. Le mécanisme d'E9.b n'est pas déformé. |
| **D4** | **La cuisson est une tranche préalable**, prouvée par manifeste et double export avant qu'une ligne de moteur ne soit écrite. |
| **D5** | **`ScriptTrack` est gelé et documenté** — zéro occurrence sur 8360 cellules, `case` au corps vide dans l'original — comme D-E9b-14 a gelé `0xA4`. |
| **D6** | **La politique de passe et de fusion d'E9.b est réemployée telle quelle** : `Ground = true` → `ResolveGroundLayerBlend` + `RenderPass2D.Effects`, déjà exercée par 86 couches du mode 1. |
| **D7** | **`FallRespawn` partage le flux aléatoire global de l'original** (`Random.cs:5,14`, graine `0xB017C93D`, `seed = seed * 0x7d2b89dd + 0xe06a02e7`), comme `GraphicManager.cs:1172` le fait. L'acceptation n'épingle pas les positions absolues mais les **invariants** : une cellule qui passe le bas réapparaît en haut, à une abscisse dans les bornes de l'écran. |

**Correction portée à D1 par la relecture de clôture** : la révision 2 proposait « la 443 **ou** la
20 ». **La 20 ne convient pas** — ses 15 cellules ont toutes `DX` dans {1..5} avec `PeriodX = 3`, et
`DY` dans {1..4} avec `PeriodY = 4` : toutes positives, donc le OU et le OU EXCLUSIF donnent le même
résultat et le piège du §1.4 n'a aucun témoin. Les cartes qui l'exercent réellement sont 55-60,
293, 443-448 et 461. **D1 retient la 443.**

## 3. Tranches — figées, D1-D7 tranchées

Statuts : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.
Une seule tranche à la fois ; la mise à jour de ce fichier va dans le commit de la tranche.

### ✅ C0 — Le compte corrigé

- Objectif : 84 → 90 partout où le nombre est écrit, avec la source du comptage.
- Fichiers : `docs/plan-e9-backdrops-residus.md`, `docs/plan-conversion-totale.md`.
- Validation : plus aucune occurrence de « 84 » désignant les cartes cellulaires.
- Commit : `docs(alundra): correct the cellular map count to the measured 90`
- **Fait le 2026-09-07.** Quatre occurrences corrigées : `plan-e9-backdrops-residus.md:38`, et
  `plan-conversion-totale.md` lignes 611, 618, 676. Plus aucun « 84 » ne désigne les cartes cellulaires.

### ✅ C1 — Convertisseur : la cuisson (D2, D4)

- Objectif : produire les pixels qui manquent. Sans eux, rien n'est observable.
- Fichiers : `alundra-casaengine-project-converter/Writers/BackdropImageBuilder.cs`,
  `.../Writers/BackdropWriter.cs`, `.../Readers/BackdropReader.cs`, `docs/formats/backdrops.md`,
  et les tests du convertisseur.
- Étapes :
  1. Généraliser le décodage : `DrawTile` (`:127-129`) est figé en 16×16 et n'a pas de paramètres de
     taille. Lui en donner, ou extraire un décodeur de rectangle arbitraire, **sans changer le chemin
     du mode 1** — sa sortie doit rester identique au bit près.
  2. Cuire, pour chaque carte portant une couche cellulaire, **une texture par `PalDex` employé** :
     la planche 256×256 décodée entière. Nom **nouveau** (les 132 textures existantes ne changent pas
     de nom : leur id dérive du chemin, les renommer changerait 132 ids).
  3. Porter la ou les références de texture sur la couche cellulaire du document ; documenter le
     champ dans `docs/formats/backdrops.md`, section « Différé » à réviser.
  4. Tests : au moins un témoin de couleur sur le décodage généralisé, et un test qui prouve que le
     mode 1 n'a pas bougé.
- Validation : tests du convertisseur verts ; **export complet en place**, diff classé conforme à un
  ensemble prédit à l'avance, **second export ⊆ `{report.json}`**, six traces d'or identiques au bit
  près.
- Commit : `feat(backdrops): bake the cellular tile sheet per used palette`

**Ligne de base du manifeste** capturée avant toute modification, le 2026-09-07 :
`e9d-baseline-manifest.sha256`, **23 013 fichiers**.

**Diff prédit — écrit AVANT l'export, pas après.** Le classement devra correspondre exactement à cet
ensemble ; toute entrée hors de cette liste est un arrêt.

1. **Ajoutés** : `{FileBaseName}-cellsheet{palDex}.png` et leur asset, pour les 90 cartes
   cellulaires et pour chaque `PalDex` réellement employé par leurs cellules — soit une à deux
   planches par carte.
2. **Modifiés** : les **90** compagnons `*.backdrop.json` des cartes cellulaires, qui gagnent
   `CellularSheetTextureAssetIds`.
3. **Modifié** : `report.json`, qui gagne ses compteurs.
4. **Rien d'autre.** Aucun PNG existant, aucun `.tileMap`, et **aucun des 393 autres compagnons
   `backdrop.json`** ne doit bouger d'un octet. Le mode 1 est inchangé au bit près : c'est le
   critère d'acceptation.

Puis **second export** : diff ⊆ `{report.json}`.

---

**Fait le 2026-09-07.** Tests du convertisseur **156 / 156** (153 + 3 nouveaux). Export complet en
place, **vérification PASSED** (19 505 chargés, 2 378 vérifiés en existence), 23 197 fichiers.

**Diff classé contre la prédiction :**

| | Prédit | Mesuré | |
|---|---|---|---|
| Ajoutés | planches cellulaires | **184**, toutes `cellsheet` | ✅ |
| Modifiés | 90 `backdrop.json` | **90** | ✅ |
| Modifiés | `report.json` | 1 | ✅ |
| Supprimés | aucun | **0** | ✅ |
| Modifiés | — | `AssetInfos.json` | ⚠️ non prédit |
| Modifiés | — | `AlundraGame.json` | ⚠️ non prédit |

**Aucun PNG existant, aucun `.tileMap`, aucun des 393 autres compagnons n'a bougé.** Le mode 1 est
inchangé au bit près : le critère d'acceptation est tenu.

**Les deux entrées non prédites, élucidées plutôt qu'excusées :**

- `AssetInfos.json` — légitime et de ma faute de prédiction : enregistrer 92 nouvelles textures
  ajoute leurs entrées au registre. Vérifié : **368 occurrences de `cellsheet`**, soit exactement 92
  assets à quatre champs. Rien d'autre.
- `AlundraGame.json` — **sans rapport avec ce chantier.** Le convertisseur y écrit une constante
  (`ProjectWriter.cs:72-73`, `AlundraDisplay.WindowWidth/Height`), mais le **moteur réécrit ce même
  fichier au runtime** depuis l'affichage réel (`CasaEngineGame.cs:171,178`). Lancer le jeu modifie
  donc le fichier de projet, et l'export suivant y remet la constante. Comportement préexistant, à
  connaître : **tout export révoque silencieusement les réglages de fenêtre posés en jouant.**

**Second export** : diff = `{report.json}` **exactement**. Déterminisme prouvé ; `AlundraGame.json`
n'a pas rebougé, ce qui confirme l'explication ci-dessus.

**Note de conception ajoutée à l'exécution** : le nouveau champ porte
`[JsonIgnore(WhenWritingNull)]`, comme `FrameTextureAssetIds`. C'est ce qui garantit que les 393
compagnons non cellulaires restent identiques à l'octet près — le diff mesuré le confirme.

### ⏳ C2 — Moteur : le couple service et composant (D3, D5, D6)

- Objectif : `CellularLayerService` (sans MonoGame) et `CellularLayerComponent`.
- Étapes : les formules pures d'abord — le pas de période en **OU** de signes (§1.4), la phase de
  fenêtre source `(V0 + phase) & 0xFF` (§1.5 bis), la combinaison `WaveX` du `WaveLut` (§1.3) — puis
  la soumission par la plomberie existante, en réemployant la politique de passe de D6.
- Validation : `dotnet test CasaEngine.Tests` à zéro échec ; tests dédiés sur chaque formule.
- Commit : `feat(rendering): cellular scrolling layers`

### ⏳ C3 — DLL : la liaison

- Objectif : les propriétés manquantes sur le `BackdropDocument` de la DLL, la construction des
  cellules, la poussée par frame.
- Validation : `Alundra.Tests` sans régression ; production épinglée headless.
- Commit : `feat(alundra): feed the engine the cellular backdrop layers`

### ⏳ C4 — Recette en jeu (D1)

- Cartes **420**, **391**, **271**, **443**. Numérique d'abord, puis visuel avec la discipline de
  capture d'E9.b : rafale, médiane par pixel, garde de fenêtre au premier plan.

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
