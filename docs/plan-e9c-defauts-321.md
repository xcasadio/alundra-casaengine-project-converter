# Plan — E9.c : les deux défauts de la carte 321

Chantier correctif ouvert le 2026-09-04 après la validation en jeu d'E9.b par l'utilisateur : « tout
est bon sauf pour la map 321 ». Deux défauts distincts, tous deux **antérieurs à E9.b** (le rendu
d'avant la bascule les montrait déjà), diagnostiqués par une enquête en lecture seule à trois angles
et **confirmés par réfutation contradictoire** — chaque sceptique a re-dérivé les chiffres lui-même
et aucun n'a pu réfuter.

**Révision 2** — la révision 1 a été relue : deux blocages, tous deux corrigés. (1) D-E9c-4 exigeait
« aucun pixel opaque de bleu nul », prédicat qu'un correctif JUSTE falsifie — le mot de palette
`0x0020` a ses quintets rouge et bleu nuls et reste `(0, 8, 0)` après l'échange ; le prédicat est
remplacé par l'énumération des 15 couleurs attendues. (2) Deux tests moteur verts épinglent le z = 0
que C2 change ; ils sont désormais nommés dans C2 comme mises à jour volontaires et exclus de
l'arrêt correspondant.

---

## 0. Cadre

### 0.1 Les deux défauts, tels que l'utilisateur les a vus

> la fumée (les sortes de nuages) sont bleus dans le jeu original. C'est peut-être l'export qui n'est
> pas bon. / Les terrains sont entourés d'os de doigt de main, les nuages passent au-dessus des
> doigts sur la ligne du haut. On voit un nuage coupé par un doigt.

### 0.2 Périmètre

- **Touche** : le **convertisseur** (baker des fonds, phase 9) et ses tests ; le **moteur**
  (profondeur des couches de fond) et ses tests ; la DLL seulement si la configuration poussée doit
  porter la profondeur ; les documents (`docs/plan-e9b-backdrops-moteur.md` D-E9b-4, `docs/formats/backdrops.md`).
- **Ne touche pas** : l'analyseur, le mode cellulaire, `Program.cs` du lanceur, `AlundraLogicClock`,
  aucune suppression sous `alundra-project/`.
- Mode AUTO comme E9.b : enchaînement sans sollicitation sauf arrêt §4, P0/P1 ou décision produit ;
  jamais de push ; **arrêt avant la validation en jeu**, qui est à l'utilisateur.

### 0.3 Références AVANT chantier

| Suite / état | Valeur |
|---|---|
| `Alundra.Tests` | 752 / 752 |
| Convertisseur | 153 / 153 |
| `CasaEngine.Tests` | 17 échecs préexistants, noms dans `scratchpad/e9b-casaengine-baseline.txt` |
| Dépôt parent | 16 commits non poussés ; sous-module moteur `dcbb55ff` ; `CasaEngine.Launcher/Program.cs` modifié non stagé |
| `alundra-project/` | export du 2026-09-04 depuis `data-extracted` (rafraîchi et prouvé identique au remaster) |

---

## 1. Faits établis (enquête du 2026-09-04, mesurés et réfutés sans succès)

### 1.1 Couleur — une inversion rouge/bleu propre au baker des fonds

- **§1.1.a** Le PNG exporté est **déjà** rouge : `alundra-project/Maps/Arena Zorgia/Arena Zorgia
  (Boss)-321/backdrop/Arena Zorgia (Boss)-321-layer0.png` a 43 636 texels opaques, **15 couleurs
  distinctes, canal bleu nul sur les 15**, dominante `RGB(200, 56, 0)` sur 6 071 px (13,91 %) ; les
  trames 1 à 3 ont le même histogramme. Le moteur n'y est pour rien : la couche est `Ground=false,
  BlendMode 0`, donc `(Opaque, Color.White)`, et le nuanceur rend `texel * Color`
  (`SpriteBatch.fx:42`) — aucune transformation de couleur.
- **§1.1.b** La source n'a **aucun rouge** : `data-extracted/data/map_321.json`,
  `ScrollParameters.PaletteWords`, palettes 0, 1 et 4 = rampes de 16 mots dont les **bits 0-4 sont
  tous nuls**. Le mot dominant `0xE4E0` = bit 15 (STP), bits 10-14 = 25, bits 5-9 = 7, bits 0-4 = 0.
  Convention PSX (rouge en bits bas) → `RGB(0, 56, 200)`, **bleu franc**. Convention du baker (rouge
  en bits hauts) → `RGB(200, 56, 0)`, exactement la dominante mesurée dans le PNG.
- **§1.1.c** La cause, dans `alundra-casaengine-project-converter/Writers/BackdropImageBuilder.cs`
  (`FromPsxColor`, autour de `:173-178`) : `r` est pris dans `(paletteWord & (0x1F << 10)) >> 7`,
  c'est-à-dire les bits **hauts**, et `b` dans les bits bas — l'inverse du format. Les octets sont
  ensuite écrits dans le bon ordre BGRA (`:160-163`). Les chemins **tilesets et sprites**, eux,
  écrivent `R, G, B, A` dans un tampon `Format32bppArgb` (donc BGRA en mémoire) : ils inversent une
  seconde fois et s'annulent. D'où un défaut **limité aux fonds**.
- **§1.1.d** Portée mesurée sur les 153 PNG de fond exportés : **86 contiennent au moins un pixel
  opaque où `R != B`** (le swap y est visible), 67 sont insensibles (`R == B` partout). La 389
  (une seule couleur, `RGB(40,48,40)`) et la 159 (12 gris purs) sont insensibles — c'est pourquoi
  aucune validation en jeu ne l'avait attrapé. Les cinq arènes 321/322/323/327/480 partagent le
  **même** histogramme au pixel près ; le pire cas est la 471 (écart max 248 sur 163 368 px).
- **§1.1.e** Deux constantes de test consacrent l'inversion :
  `alundra-casaengine-project-converter.Tests/BackdropWriterTests.cs:873`
  `BluePsxWord = 0x001F` (bits 0-4 → en réalité **rouge** pur sous la bonne convention) et le
  commentaire `:871-872`. Le témoin vert `GreenPsxWord = 0x03E0` (`:22`) est invariant par échange
  R/B et doit rester `(0, 248, 0)` avant comme après.

### 1.2 Ordre — une égalité de profondeur contre le lot statique

- **§1.2.a** Après le retrait d'E7 (`Alundra/Scripts/WallPlacementOverlay.cs:245-283`, `:300-345`),
  la 321 conserve **exactement 166 tuiles** dans `Render_0` (`z_offset 0.0`) — les autres plans sont
  vidés à 100 % (`Render_1` 473 → 0, `Render_2` 49 → 0). Vérifié en rejouant la règle de retrait
  sur les données réelles : 1 715 enregistrements de placement, **zéro écart**.
- **§1.2.b** Ces 166 tuiles sont **les rangées 13 à 19**, c'est-à-dire la bande horizontale la plus
  haute des structures d'os — exactement « les doigts horizontaux les plus hauts ». Les rangées 13 à
  18 sont résiduelles à 100 %, la rangée 19 l'est à 8/60, et tout ce qui est en dessous passe par la
  surimpression.
- **§1.2.c** Mécanique : `TileMapComponent.cs:433-437` calcule `worldZ = translation.Z + zOffset`
  (l'entité `tileMap` de la 321 est à `(0,0,0)`) et `SpriteRendererComponent.cs:266-320`
  `DrawStaticBatch` dessine **immédiatement**, pendant `World.Draw`, donc **avant** le vidage des
  sprites (`DefaultViewPipeline.cs:35` puis `:42-45`), avec le même état de profondeur
  `LessEqual` + écriture (`:92-97`). Le fond est soumis à z = 0 lui aussi
  (`ScrollingLayerComponent.cs:230-242`, passe `Background` via `AlundraBackdropStage.cs:197`). **À
  égalité de profondeur, le dernier dessiné gagne** : le nuage recouvre l'os.
- **§1.2.d** Les autres tuiles sont dans la surimpression triée, soumises à la passe `YSortedWorld`
  (300) contre `Background` (0) pour le fond (`WallPlacementOverlay.cs:356-357`, `:370-371` ;
  `RenderPass2D.cs:5,8`) : dans le même vidage, elles sont dessinées **après** le fond et gagnent la
  même égalité. D'où le nuage **coupé net à la rangée 19**, là où le même os change de chemin.
  Le nuanceur écarte les texels d'alpha ≤ 0,01 (`SpriteBatch.fx:41-44`), donc le nuage **coupe**
  au lieu d'effacer, ce qui est précisément le symptôme décrit.
- **§1.2.e** Ce n'est **pas** une régression d'E9.b : `git show e808568^:Alundra/Scripts/BackdropRenderer.cs`
  construisait la même clé (`Ground ? Effects : Background`, `SortingLayer 0`, `OrderInLayer =
  DepthOrder`) et passait le même `0f` en z, dans la même file. Les captures d'avant la bascule
  montrent déjà le symptôme.
- **§1.2.f** L'original place les couches `Ground=false` à la profondeur `−0x10000000 + ordre`
  (`GraphicManager.cs:825-826`), c'est-à-dire **derrière tout sol, mur et entité** — pas à égalité.
  La décision **D-E9b-4** (« z = 0 pour tous les quads, comme aujourd'hui ») a été figée sur une
  mesure qui ne regardait que le **vide** au-dessus du sol de la 321 ; elle est fidèle au port
  d'avant, pas à l'original. Ce plan la corrige.

---

## 2. Décisions

- **D-E9c-1 — La conversion de palette des fonds adopte la convention PSX.** Dans
  `BackdropImageBuilder.FromPsxColor` : `r = (paletteWord & 0x1F) << 3` ;
  `g = ((paletteWord >> 5) & 0x1F) << 3` ; `b = ((paletteWord >> 10) & 0x1F) << 3`. L'écriture des
  octets (`:160-163`) et l'expansion `<< 3` (sans réplication de bits) **restent inchangées** : c'est
  ce que font les tilesets et les sprites livrés, et changer l'expansion désynchroniserait les fonds
  du reste des assets. Le commentaire fautif de `:171-172` est repris.
- **D-E9c-2 — Les constantes de test suivent.** `BluePsxWord = 0x001F` devient le bleu réel
  `0x7C00` (bits 10-14), ou est renommé en rouge avec l'attente `(248, 0, 0)` ; le commentaire
  `:871-872` est corrigé. Le témoin vert `0x03E0 → (0, 248, 0)` reste **inchangé** et sert de preuve
  que seul l'axe R/B bouge.
- **D-E9c-3 — Preuve du convertisseur, prédicat exact.** Baseline de manifeste capturé **avant** la
  modification (périmètre D-E9-8 : hors `Alundra.dll`/`.pdb` et `.casaeditor/`). Après : un export
  complet in-place depuis `data-extracted`, dont le diff doit être **exactement les 86 PNG de fond
  dont un pixel opaque vérifie `R != B`, plus `report.json`** — la liste des 86 est établie **avant**
  l'export et écrite dans le scratchpad ; **aucun** autre fichier, en particulier aucun `.texture`,
  aucun compagnon `.backdrop.json`, ni `AssetInfos.json` (les ids sont dérivés du chemin, rien n'est
  renommé), et aucun des 67 PNG insensibles. Puis un second export : diff ⊆ `{report.json}`. Puis les
  six goldens byte-identiques et les trois suites vertes. **Retour arrière** : revert + export de
  restauration redonnant le baseline hors `report.json`.
- **D-E9c-4 — Vérification numérique de la couleur.** Après ré-export, les **15 couleurs opaques**
  du PNG de la 321 doivent être **exactement l'échange R↔B des 15 mesurées avant, effectif pour
  effectif** : dominante `RGB(0, 56, 200)` sur 6 071 pixels, puis `(0,56,176)`, `(0,48,160)`,
  `(0,48,112)`, `(0,32,88)`, `(0,32,80)`, `(0,32,72)`, `(0,24,64)`, `(0,16,56)`, `(0,8,56)`,
  `(0,0,40)`, `(0,0,32)`, `(0,0,24)`, `(0,0,16)` et `(0,8,0)`. **Ne pas exiger « aucun bleu nul »** :
  la quinzième couleur vient du mot `0x0020`, dont les quintets rouge et bleu sont tous deux nuls,
  donc elle est **invariante** par l'échange et reste `(0, 8, 0)` — un tel prédicat serait faux même
  avec un correctif juste (relecture du 2026-09-04). Prédicat falsifiable, mesuré par script.
- **D-E9c-5 — Les fonds `Ground=false` passent derrière les plans de tuiles.** Le mécanisme moteur
  soumet une couche de passe `Background` à **z = `cameraTarget.Z − BackgroundDepth`** avec
  `BackgroundDepth` porté par `ScrollingLayerConfiguration` (défaut 1, donc z = −1 en Alundra) ;
  toute autre passe et la teinte restent à `cameraTarget.Z`. Justification : l'original les met
  derrière tout (§1.2.f), et le sens de profondeur du moteur fait qu'un z plus petit est **plus
  loin** — le fond, dessiné avant, écrit une profondeur lointaine que les tuiles (z = 0, plus près)
  franchissent, tandis que le fond est correctement **rejeté** là où une tuile du lot statique a déjà
  écrit la sienne. Le fondu (`ScreenEffects`, z = 0) continue de tout couvrir. **D-E9b-4 est amendée
  en conséquence** dans le plan E9.b, avec la mesure qui l'avait mal fixée.
- **D-E9c-6 — Acceptation.** Moteur : un test épingle le z soumis par passe (`Background` derrière,
  autres à la profondeur caméra) et un test épingle que le fondu couvre toujours un fond ainsi
  reculé ; les 17 échecs préexistants restent identiques nom pour nom. DLL : la configuration poussée
  porte la profondeur attendue. Convertisseur : 153 + tests ajustés, verts. **En jeu (utilisateur)** :
  sur la 321 les nuages sont **bleus** et passent **derrière** les os des rangées hautes ; la 389 et
  la 159 sont inchangées.

---

## 3. Tranches

| Tranche | Propriétaire | Prérequis | Commits |
|---|---|---|---|
| C1 — couleur | convertisseur | baseline de manifeste + liste des 86 | 1 (+ export prouvé) |
| C2 — profondeur | moteur (sous-module) puis DLL si besoin | baseline `CasaEngine.Tests` | 1 sous-module + 1 bump (+ 1 DLL) |
| C3 — documents | docs | C1 et C2 validées en jeu | 1 |

Séquentiel strict : un seul build à la fois, et **aucun export pendant qu'une suite tourne**.

### C1 — La couleur (D-E9c-1 à D-E9c-4)

**Étape 0** : capturer le baseline de manifeste et écrire la liste des 86 PNG attendus (script dans
le scratchpad, périmètre D-E9-8).
**Contenu** : le correctif de `FromPsxColor` ; les constantes et commentaires de test ; la ligne de
`docs/formats/backdrops.md` qui décrit la conversion, si elle existe.
**Acceptation** : suite convertisseur verte ; export complet, diff = exactement les 86 PNG +
`report.json` ; second export ⊆ `{report.json}` ; goldens intacts ; `Alundra.Tests` 752/752 ;
dominante de la 321 = `RGB(0, 56, 200)` sur 6 071 px.
**Mutations** : garder l'ancienne convention → le témoin de la 321 tombe ; inverser aussi le vert →
`GreenPsxWord` tombe ; toucher l'expansion `<< 3` → les valeurs 200/248 tombent.

### C2 — La profondeur (D-E9c-5, D-E9c-6)

**Contenu** : `ScrollingLayerConfiguration` gagne la profondeur de fond ; `ScrollingLayerComponent`
l'applique aux couches de passe `Background` ; `AlundraBackdropStage` la pousse (valeur 1) ; tests
moteur et DLL ; amendement de D-E9b-4 dans `docs/plan-e9b-backdrops-moteur.md` avec la raison.
**Deux tests moteur existants épinglent le comportement que cette tranche change et sont mis à jour
volontairement** (`CasaEngine.Tests/Rendering/ScrollingLayers/ScrollingLayerComponentSubmissionTests.cs`) :
`Submit_TranslationZ_IsZeroForBothBackgroundAndEffectsPasses` (`:118-139`) et
`Submit_ScreenFixedLayer_AtTarget1087Minus839_...` (`:46-64`, dont la couche d'aide `:346-366` est de
passe `Background`). Leur pin devient : **une couche `Background` est reculée de `BackgroundDepth`,
les autres passes et la teinte restent à la profondeur caméra**. Ce sont les **seuls** tests
existants que C2 a le droit de modifier ; toute autre édition d'un test vert est un arrêt.
**Acceptation** : tests moteur verts, 17 échecs préexistants identiques ; `Alundra.Tests` 752 + les
tests ajoutés ; commit sous-module par chemins nommés, bump du pointeur.
**Mutations** : profondeur laissée à 0 → le pin de z tombe ; profondeur appliquée à toutes les passes
→ le pin de la teinte et du fondu tombe.

### C3 — Documents

`docs/plan-e9b-backdrops-moteur.md` (D-E9b-4 amendée, §1.5 corrigée : la mesure ne regardait que le
vide), `docs/formats/backdrops.md` (convention de palette), journal de ce plan, mémoire.

---

## 4. Arrêts

- un diff d'export autre que les 86 PNG attendus + `report.json` ;
- un `.texture`, un compagnon ou `AssetInfos.json` modifié ;
- un des 67 PNG insensibles modifié ;
- un golden qui bouge ;
- `CasaEngine.Launcher/Program.cs` modifié ou stagé ; `git add -A` dans le sous-module ;
- un échec `CasaEngine.Tests` hors de la liste nommée des 17 préexistants — **exception** : les deux
  tests de soumission nommés dans C2, dont la mise à jour est prévue et dont l'échec avant mise à
  jour est attendu ;
- la 389 ou la 159 visuellement changée après C1 ou C2 ;
- la nécessité de toucher à l'expansion `<< 3` ou aux chemins tileset/sprite (ils sont corrects).

---

## 5. Journal d'exécution

### C1 — La couleur (2026-09-06)

- **Commit** `71c57da` (convertisseur). `BackdropImageBuilder.FromPsxColor` lit désormais le rouge
  dans les bits 0-4, le vert dans les bits 5-9, le bleu dans les bits 10-14 du mot de palette 16
  bits ; l'écriture des octets et l'expansion `<< 3` restent inchangées. Aucun autre chemin de
  décodage n'a été touché : les chemins tileset et sprite inversent deux fois et s'annulaient déjà,
  donc étaient déjà corrects.
- **Correction faite pendant la tranche** : la constante de test qui déclarait `0x001F` comme
  « bleu » a été corrigée (le bleu pur est `0x7C00`) ; le témoin vert `0x03E0 → (0, 248, 0)` est
  resté inchangé et sert de preuve que seul l'axe rouge/bleu a bougé.
- **Preuve** : suite convertisseur 153/153 ; export complet in-place dont le diff contre le
  manifeste d'avant modification est exactement les 86 PNG de fond prédits par un balayage
  pré-modification (sur 153 au total ; les 67 autres ont `R == B` partout et sont restés
  bit-identiques), plus `report.json` ; second export ne différant que par `report.json` ; les six
  goldens inchangés ; `Alundra.Tests` 752/752.
- **Prédicat couleur** : le `-layer0.png` de la map 321 garde ses 15 couleurs opaques et ses
  43 636 texels opaques, la dominante devenant `RGB(0, 56, 200)` sur 6 071 pixels — l'échange
  rouge/bleu exact de l'ancien `RGB(200, 56, 0)` — et `RGB(0, 8, 0)`, qui vient du mot de palette
  `0x0020` dont les quintets rouge et bleu sont tous deux nuls, reste invariant et présent.
- **Mutations vérifiées à la main** : restaurer l'ancienne convention fait tomber le témoin bleu ;
  inverser aussi le vert fait tomber le témoin vert.
- **Verdict** : conforme à D-E9c-1 à D-E9c-4 et à l'acceptation de la tranche C1.

### C2 — La profondeur (2026-09-06)

- **Commits** : sous-module moteur `0be1e9d2`, commit parent `0458c6b` (bump du pointeur plus la
  moitié gameplay).
- **Contenu** : `ScrollingLayerConfiguration` gagne une profondeur de fond (`BackgroundDepth`,
  défaut 1) ; `ScrollingLayerComponent` soumet une couche de passe `Background` à
  `cameraTarget.Z − BackgroundDepth`, alors que toute autre passe et la teinte de couche restent à
  la profondeur caméra ; `AlundraBackdropStage.BuildDefinitions` pousse la valeur, épinglée contre
  le compagnon réel de la map 321. Une configuration à profondeur 0 reproduit le comportement
  d'avant, ce qui prouve que la valeur est de la donnée et non une constante figée.
- **Correction faite pendant la tranche** : `ScreenEffectComponent` codait en dur une profondeur
  nulle pour le fondu plein écran ; il suit désormais la profondeur caméra comme la teinte de
  couche, avec un nouveau test qui l'épingle à une profondeur caméra non nulle.
- **Tests mis à jour délibérément** (comme prévu par le plan, §3 C2) : les deux tests moteur qui
  épinglaient l'ancienne égalité de profondeur ; aucun autre test vert n'a été touché.
- **Preuve** : suite moteur 1528 en succès avec les mêmes 17 échecs préexistants, nom pour nom ;
  `Alundra.Tests` 752/752 ; les deux mutations du plan vérifiées à la main (profondeur laissée à
  zéro, et profondeur appliquée à toutes les passes).
- **Verdict** : conforme à D-E9c-5 et D-E9c-6.

### Attribution

Les deux défauts sont **antérieurs à la bascule E9.b** : les captures prises avant la bascule
montraient déjà des nuages rouges, et le renderer retiré construisait la même clé de tri et
passait la même profondeur nulle.

### Encore ouvert

L'utilisateur doit valider en jeu la map 321 (nuages bleus, et passant derrière les os de la
rangée du haut) ainsi que l'absence de régression sur les maps 389 et 159. La comparaison
automatisée de captures avant/après reste indisponible dans cet environnement : toute capture
d'écran de la fenêtre du jeu revient noire, de façon reproductible, avec trois méthodes de
capture différentes, y compris sur le code d'avant la bascule.

### Avis différés (P4, aucun bloquant)

- Le test de soumission côté moteur s'appuie sur la profondeur de fond par défaut plutôt que de
  l'épingler explicitement (atténué par le test à profondeur nulle et par l'épingle côté
  gameplay).
- Hérités des tranches précédentes : les assertions de sous-chaîne trop larges du test de
  journalisation, le harnais d'équivalence qui n'exerce jamais la teinte de surimpression, et la
  ligne de mutation du plan « `SetLayers` sans `Clear` » qui ne mord pas.
