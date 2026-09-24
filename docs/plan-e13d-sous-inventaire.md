# Plan — E13.d, seconde partie : le sous-inventaire et la bascule L1/R1

**État** : 🚧 rédigé le 2026-09-24 (soir), relu **READY** au premier passage (relecteur frais), exécution en
cours. **Mode d'exécution** : l'auteur a demandé, le
2026-09-24 au soir, que la tâche soit faite en autonomie pendant son absence (« il faut que tu puisses faire
tout ça tout seul »). Ce plan tient donc lieu d'approbation une fois relu **READY** : mode **AUTO** (travail
réversible dans le périmètre écrit ici, un commit par tranche sur des branches dédiées, ni push, ni merge, ni
action externe). Toute question qui relève de l'auteur est écrite au §6 et n'empêche pas les tranches qui n'en
dépendent pas.
**Naissance** : `docs/plan-conversion-totale.md` §E13, E13.d ; découpage de l'auteur du 2026-09-19 (D-E13D-2) :
l'inventaire principal d'abord, puis le sous-inventaire et la bascule L1/R1, « dont le plan s'écrira après la
recette D6 ». D6 est validée par l'auteur le 2026-09-24 (`docs/plan-e13d-inventaire.md` §3 D6).
**Dépend de** : l'inventaire principal (`docs/plan-e13d-inventaire.md`, D0 à D6) et le programme des écrans liés
(`docs/plan-bound-screens.md`, ADR-0002 du dépôt) : l'inventaire principal est un asset du projet lié à un
view-model.
**Branches** : parent `chantier/e13d-sub-inventory`, créée depuis `chantier/bound-screens` `6edb7cd` (la
validation D6 y est enregistrée ; `main` ne l'a pas encore), dans un **worktree isolé**
`.claude/worktrees/e13d-sub-inventory` : une autre session travaille ce soir dans le checkout principal
(T4.7 de MGUI) et y basculera les branches. Sous-modules du worktree : clones locaux aux commits enregistrés
(moteur `21d629c9`, MGUI `d47c8e1`, NvgSharp `9c0da03`, analyseur `9adba14`). Analyseur : branche
`chantier/e13d-sub-inventory-boxes` depuis `9adba14`, rapatriée à la fin dans le dépôt de l'analyseur du
checkout principal par `git fetch` (aucun push). Le moteur et MGUI ne changent pas.

---

## 1. Les faits établis

Lecture intégrale de `SubInventoryManager.cs` (1496 lignes) et des passages de `MainInventoryManager.cs`,
`GraphicManager.cs`, `UIManager.cs` et `StaticVariables.cs` qu'il appelle, en session principale ; mesures
marquées **[mesuré]**, avec leur script au §7. **[binaire]** : vérifié dans l'exécutable de la version
France (`Alundra (France)_extracted/ALUN_CD.EXE`, texte chargé en `0x80020000`, décalage de fichier `0x800`),
dont les adresses sont exactement celles de la décompilation (vérifié sur la fonction `0x80052c64`, §1.6).

### 1.1 La bascule dans l'original

- **Principal → sous-inventaire** (`MainInventoryManager.cs:853-859`) : `L1` ou `R1`
  (`PadState.OpenSubInventory = R1 | L1`, `PadState.cs:23`), lu sur `ButtonsJustPressedByInterval`, appelle la
  fermeture de l'inventaire principal (`FUN_800556dc` : glissement de sortie des sept boîtes, **son 5**,
  `g_forbiddenWarpFlag |= 2`), relève `MenuOpen`, appelle `UpdateHudTransitionState` (état propre au portrait,
  non porté, §1.5) et pose **`g_postProcessState = 1`**. **Il n'appelle pas `InitializeHudPositionBeforeHide`** :
  la jauge reste cachée.
- **Fin du glissement de sortie** (`:878-902`) : `g_forbiddenWarpFlag = 0`, origines des boîtes rendues, et
  `MenuOpen` n'est retiré **que si `(g_postProcessState & 1) == 0`** (`:896-899`) ; le rappel de l'emplacement 6
  est libéré (`FUN_80047cb0`, `UIManager.cs:643-646` : `Flags = 0`).
- **Le post-traitement** (`GraphicManager.cs:1691-1706`), à la fin de la mise à jour de l'interface, **après**
  la boucle des treize rappels (`:1677-1689`) :
  - `g_postProcessState == 1` et rappel 6 libre → `g_postProcessState = 0` puis **`StartFadeOut()`**
    (`:1767-1783`), qui, malgré son nom, ouvre le sous-inventaire : `HudManager.InitializeHudPosition()`,
    `SetTransitionType(4)`, portrait préparé, **son 4** ;
  - `g_postProcessState == 2` et rappel 4 libre → `g_postProcessState = 0` puis
    **`MainInventoryManager.DisplayInventory()`** (`MainInventoryManager.cs:443-499`), l'ouverture ordinaire de
    l'inventaire principal, gardes comprises.
- **`SetTransitionType(4)`** (`GraphicManager.cs:1710-1742`) copie l'emplacement 4 de la table initiale
  (`StaticVariables.cs:11416-11421` : `InitializeFunc = InitializeSubInventory`, `RenderFunc =
  DisplaySubInventory`), lève `Flags |= 1` et **appelle `InitializeFunc` immédiatement** (`:1737-1740`),
  contrairement à l'emplacement 6 dont l'`InitializeFunc` est nul.
- **Sous-inventaire → principal** (`SubInventoryManager.cs:371-377`) : `L1` ou `R1` ferme le sous-inventaire
  (`FUN_800526cc`, `:856-1116` : **son 5**, `g_subInventoryState |= 2`, glissement de sortie), relève `MenuOpen`,
  `UpdateHudTransitionState` (non porté) et pose **`g_postProcessState = 2`**.
- **Fermeture du sous-inventaire** (`:364-369`) : `Start`, `L2` ou `R2` (`PadState.OpenInventory`) ferme
  (`FUN_800526cc`), `UpdateHudTransitionState` (non porté), puis **`HudManager.InitializeHudPositionBeforeHide()`**
  (la jauge revient, gardes de l'original, déjà portées : `AlundraHudDirector.InitializeHudPositionBeforeHide`).
- **[binaire] `Triangle` ferme aussi, dans les deux inventaires.** Le masque de fermeture de l'exécutable est
  **`0x813`** = `Start | Triangle | R2 | L2` (`andi $v0, $v0, 0x813` en `0x80053634`, sous-inventaire, et en
  `0x80056924`, principal), alors que la décompilation écrit `PadState.OpenInventory` = `0x803`
  (`PadState.cs:22`). Le déclencheur d'**ouverture**, lui, teste bien `0x803` sur `ButtonsJustPressed`
  (`0x8002bcac`, `GameEngine.cs:1567-1576`) : `Triangle` ferme mais n'ouvre pas. L'inventaire principal porté en
  D4 ne ferme donc pas sur `Triangle` : écart de fidélité hérité de la décompilation (D-E13D-29, SI3.a).
- **Fin du glissement du sous-inventaire** (`:389-421`) : `g_subInventoryState = 0`, origines rendues, `MenuOpen`
  retiré **seulement si `(g_postProcessState & 2) == 0`**, rappel 4 libéré, et **retour immédiat** : rien n'est
  dessiné à cette image.
- **Écrivains de `g_postProcessState`** (recherche dans toute la décompilation) : `1` à `MainInventoryManager.cs:858`,
  `2` à `SubInventoryManager.cs:376`, `0` à `GraphicManager.cs:1696` et `:1703` ; le reste est l'inspecteur de
  débogage de l'analyseur, hors du jeu. **`MenuOpen` ne retombe donc jamais pendant une bascule** : le monde reste
  gelé d'un inventaire à l'autre (gel D2, D-E13D-15).
- **Aucun bouton d'action dans le sous-inventaire** : `DisplaySubInventory` ne lit que les quatre directions,
  `OpenInventory` et `OpenSubInventory` (`:336-377`). On n'y équipe rien : l'armure et les bottes sont celles que
  `GetItemIdFromSlotId` résout (meilleur objet possédé de la case 7, de la case 9).

### 1.2 L'horloge de la bascule

Un tick du portage vaut un `Update` de l'original suivi du rendu de l'image suivante (`docs/plan-e13d-inventaire.md`,
D4, « Ordre établi »). Le déclencheur est dans l'`Update`, les rappels et le post-traitement dans le rendu, le
post-traitement **après** tous les rappels. D'où, tick par tick :

| Tick | Principal → sous-inventaire | Sous-inventaire → principal |
|---|---|---|
| T0 | principal : `L1`/`R1` lu, fermeture armée, son 5, `g_postProcessState = 1` ; la queue de dessin tourne | sous-inventaire : `L1`/`R1` lu, fermeture armée, son 5, `g_postProcessState = 2` ; la queue tourne |
| T0+1 … Tc−1 | glissement de sortie du principal | glissement de sortie du sous-inventaire |
| Tc | fin du glissement : drapeau à 0, `MenuOpen` **gardé**, rien de dessiné ; **post-traitement** : ouverture du sous-inventaire (`InitializeSubInventory` complet, noms de l'armure et des bottes, son 4) | fin du glissement : état à 0, `MenuOpen` **gardé**, rien de dessiné ; **post-traitement** : tête de `DisplayInventory` (gardes, `InitializeHudPosition`, noms équipés, son 4), l'emplacement 6 armé |
| Tc+1 | premier `DisplaySubInventory` : glissement d'entrée, tout est dessiné | `FUN_80054f1c` (mise en place du principal : drapeau 5, texte remis à zéro, glissements armés) ; rien de dessiné |
| Tc+2 | … | premier `FUN_80056598` : glissement d'entrée, dessiné |

Il y a donc **une image sans aucun inventaire** au tick Tc (le décor gelé seul), et deux dans le sens
sous-inventaire → principal. C'est le comportement de l'original, porté tel quel.

### 1.3 L'ouverture du sous-inventaire

`InitializeSubInventory` (`SubInventoryManager.cs:21-294`) : `g_subInventoryState = 5` (bits 0 et 2),
curseur de texte `INT_8017f788 = 0`, `MenuOpen` levé, sept glissements de **15 ticks** (`mode 2`, `speed 0xf`),
puis les noms de l'armure et des bottes (`FUN_80052f24(0)` et `(1)`, `:297-323` : `GetItemIdFromSlotId(7)` et
`(9)`, `UINT_ARRAY_800b44f0 = {7, 9}`), calculés **une fois**, à l'ouverture. La case du curseur
(`INT_8017f734`) n'est **pas** remise à zéro : elle se garde d'une ouverture à l'autre, comme
`g_inventorySelectedSlotId` dans le principal.

| Boîte (variable décompilée) | Place de repos | Taille | Entrée | Sortie |
|---|---|---|---|---|
| armurerie `UIBoxConfiguration_800af664` | (8, 16) | 15 × 14 cases | par la gauche, `x = ~(Width << 3)` | vers la gauche |
| objets-clés `UIBoxConfiguration_800b06dc` | (8, 128) | 21 × 5 | par la gauche | vers la gauche |
| nom de l'armure `UIBoxConfiguration_800b122c` | (176, 16) | 18 × 4 | par la droite, `x = 0x140` | vers la droite |
| nom des bottes `UIBoxConfiguration_800b1d7c` | (176, 64) | 18 × 4 | par la droite | vers la droite |
| icônes armure et bottes `UIBoxConfiguration_800b287c` | (136, 16) | 5 × 14 | par la droite | vers la droite |
| description `g_uiBoxesInventoryDescriptionBackground` (partagée) | (16, 168) | 36 × 7 | par le bas, `y = 0xf0` | vers le bas |
| argent, faucons, clés `g_UiBoxesInventoryMoneyFalconKeyIcons` (partagée) | (176, 96) | 3 × 9 | par la droite | vers la droite |

Deux curiosités de transcription sans effet : la hauteur utilisée pour la source `y` de la boîte des bottes est
celle de la boîte de l'armure (`:145`, `:165`), et ce terme n'intervient que si `Y < 0`, jamais vrai ici.

### 1.4 Chaque image (`DisplaySubInventory`, `:326-465`)

- **Pendant un glissement** (`(g_subInventoryState & 6) != 0`) : les sept `UpdateUiBoxesPosition` dans cet ordre
  : objets-clés, nom de l'armure, nom des bottes, icônes, description, argent, **puis l'armurerie, la dernière,
  dont seul le retour décide de la fin** (`:381-387`). Même fonction et même forme « 18 appels » que dans le
  principal, déjà portée (`AlundraInventoryDirector.AdvanceBoxTween`, `:231-255`).
- **Sinon, la manette** (`ButtonsJustPressedByInterval`, répétition comprise) : `Right`, `Left`, `Up`, `Down`
  déplacent le curseur par quatre tables (§1.5), **son 1**, texte remis à zéro ; puis `Start`/`Triangle`/`L2`/`R2`
  (masque `0x813` de l'exécutable, §1.1) ; puis `L1`/`R1` (§1.1). Les deux dernières branches sont deux `if`
  successifs : les deux dans le même tick font les deux, comme dans le principal.
- **La queue de dessin**, à chaque image sauf celle de la fin d'un glissement de sortie : le curseur, les noms de
  l'armure et des bottes, leurs icônes et leurs cadres, les icônes de l'armurerie, celles des objets-clés, les
  sept fonds, la description déroulée, les chiffres.

### 1.5 Le contenu

**Le curseur a 14 positions** (`INT_8017f734`, 0 à 13). Chaque position a sa boîte de référence, son décalage
de curseur et son objet ; tables `StaticVariables.cs:12301-12340`, **[binaire] égales** dans l'exécutable (§7) :

| Position | Boîte | Décalage du curseur | Objet décrit (`INT_ARRAY_800b4330`) |
|---|---|---|---|
| 0 à 6 | armurerie | (0x42,−4) (0x1A,0x0C) (0x6A,0x0C) (0x1A,0x3C) (0x6A,0x3C) (0x42,0x4C) (0x42,0x24) | objets `0x3E` à `0x44`, s'ils sont possédés |
| 7, 8 | icônes armure et bottes | (0x1C, 0x10), (0x1C, 0x34) | `−1` → armure (case 7), `−2` → bottes (case 9) |
| 9 à 13 | objets-clés | (0x1C,0) (0x3C,0) (0x5C,0) (0x72,0) (0x92,0) | `−3` à `−7` → le i-ème objet-clé trouvé |

Navigation (le nombre donne la position d'arrivée, par position de départ 0 à 13) :
`Right` = 2,0,7,6,8,4,2,1,3,10,11,12,13,9 ; `Left` = 1,7,0,8,5,3,1,2,4,13,9,10,11,12 ;
`Up` = 10,9,11,1,2,6,0,13,7,3,5,4,4,8 ; `Down` = 6,3,4,9,11,10,5,8,13,1,0,2,2,7.

- **L'armurerie** (`FUN_80052dd8`, `:1203-1236`) : les sept **armoiries** `0x3E` à `0x44` (Rubis, Saphir,
  Topaze, Agathe, Grenat, Émeraude, Diamant, **[mesuré]**), chacune dessinée si son compteur n'est pas nul, en
  (boîte + `INT_ARRAY_800b42f8[i]`, boîte + `INT_ARRAY_800b4314[i]`) = (0x30,4) (8,0x14) (0x58,0x14) (8,0x44)
  (0x58,0x44) (0x30,0x54) (0x30,0x2C).
- **Les objets-clés** (`FUN_80052c64`, `:1239-1281`) : les cinq premiers objets possédés dont la colonne
  « case d'inventaire » vaut `0x1C` (`FUN_8004e640`, `:1284-1310`, compare `g_itemsProperties[id * 5]` et la
  quantité), en (boîte + i × 0x20 + 8, boîte + 4). Douze objets du jeu ont cette case **[mesuré]**. **[binaire]
  Une fois la recherche épuisée, les positions suivantes restent vides** : l'exécutable teste l'identifiant
  **avant** d'appeler la recherche (`0x80052cc0 : beq $s0, $s6` avec `$s6 = −1`), garde que la décompilation a
  perdue — telle quelle, elle recommencerait depuis l'objet 0 et répéterait les objets-clés aux positions
  suivantes. **Le portage suit l'exécutable** (D-E13D-24). La table `INT_ARRAY_8017f628` est recalculée à chaque
  image, avant la description qui la lit.
- **L'armure et les bottes** (`FUN_80052fb4`, `:1157-1200`, `FUN_80053144`, `:1119-1154`) : pour chacune dont
  `GetItemIdFromSlotId` rend un objet, son icône en (boîte des icônes + 8, + position × 0x28 + 0x18) et un cadre
  24 × 32 à la même place (`SPRT_ARRAY_8017f790`, `u0 = 0x30`, `v0 = 0x98` = **`wind_039`**, le cadre de
  sélection du principal, creux : 92 pixels opaques sur 768, aucun à l'intérieur **[mesuré]**) ; son nom dans sa
  boîte de nom, en (X + 0x10, Y + 8). Nouvelle partie : **armure 17 « Armure en tissu », bottes 25 « Bottes
  courtes »**, les deux objets que l'inventaire principal ne montre pas **[mesuré]**.
- **La description** (`FUN_80053fdc`, `:468-629`) : la même machine que celle du principal
  (`MainInventoryManager.cs:929-1064` ; états 0, 1..0x10, 0x11..0x4c, 0x4d, 0x4e..0x8d, 0x8e, 0x8f..0xce, 0xcf ;
  un caractère toutes les trois images, compteur propre `INT_8017f78c`), dans la même boîte, aux mêmes places ;
  seule la résolution de l'objet change. **Un objet non possédé ou une position vide ne fait rien, et le texte
  n'avance pas** (retour avant la machine). Différence sans effet dans le portage : pendant la pause 0x11..0x4c,
  le sous-inventaire remet la largeur de la bande du nom à 8 × longueur (`:555`), le principal non ; le portage
  dessine des chaînes, pas des bandes de VRAM.
- **[binaire] La seconde ligne de description n'apparaît jamais dans le sous-inventaire.** Aux états
  0x8f..0xce, l'exécutable lit `ligne2[c − 0x90]` (`lbu -0x90(a1)` en `0x80054314` et `0x80054330`), un octet
  trop tôt ; le principal lit `ligne2[c − 0x8f]` (`lbu -0x8f(v1)` en `0x80056274`), comme la décompilation des
  deux (`SubInventoryManager.cs:608/614`). À l'état 0x8f, l'octet lu est celui qui précède la chaîne, le zéro
  final de la chaîne précédente : **mesuré égal à 0 pour les 98 objets** de `DATA/ETC_RES.R` (§7). L'état passe
  donc directement à 0xcf, et la ligne 1 est dessinée avec une largeur 0 : rien. **Dans le jeu français, le
  sous-inventaire montre le nom puis la première ligne de description, jamais la seconde** (D-E13D-28).
- **[binaire] Autres écarts de transcription, sans effet sur ce qui est dessiné** (contre-vérification du §7) :
  les chiffres du sous-inventaire écrivent dans les sprites du principal au lieu des siens (même palette 5,
  mêmes places) ; `FUN_80052f24` passe 0 au lieu de `0x140` comme abscisse initiale des noms, que le dessin
  réécrit ; la source `y` de la boîte des bottes lit la hauteur de la boîte de l'armure (terme mort, `Y ≥ 0`) ;
  des indices de sprites à double tampon repliés. Aucun ne change le portage.
- **[binaire] Écart du portage entier, hors de ce plan** : l'original déroule le texte **octet par octet** dans
  la chaîne brute, où une lettre accentuée est une paire (`}Y` pour « é ») ; le portage, principal compris,
  déroule la chaîne **décodée**, caractère par caractère. Une lettre accentuée y coûte 3 images au lieu de 6, et
  la fin du nom (fenêtre de 16) se compte en caractères et non en octets — sans effet visible aujourd'hui (aucun
  nom décodé ne dépasse 16). Consigné au §6, point 4.
- **Les chiffres** (`DisplayAmountOfMoneyFalconKeys2`, `:742-854`, « même fonction que celle du principal, avec
  d'autres sprites ») : argent sur 4 chiffres en (X + 0x18 + 8i, Y + 4), clés (objet `0x3D`) et faucons sur 2
  chiffres en (X + 0x28 + 8i, Y + 0x34) et (X + 0x28 + 8i, Y + 0x1c) : les places du principal
  (`AlundraInventoryComposer.cs:207-209`).
- **Le curseur** : `UpdateCursorSpritePosition` et `DisplayInventoryCursor` du principal, sur une animation
  propre (`InventoryCursorAnimation_8017f704`), placée en (boîte de la position + décalage) sans le `+0x12, −8`
  du principal.
- **[binaire] L'ordre de dessin** : les fonds et les deux cadres vont dans la même entrée de la table d'ordre
  (`0x80146f58`), les icônes dans une autre (`0x80146f68`), les chiffres dans une troisième (`0x80146f6c`). Les
  icônes étant visibles sur les fonds, les cadres sont **sous** les icônes ; comme ils sont creux et les icônes
  centrées dedans, l'ordre ne se voit pas. Ordre retenu : fonds, cadres, icônes, textes et chiffres, curseur.
- **Pas de portrait** : `StartFadeOut` prépare le portrait (`GraphicManager.cs:1775-1780`) comme
  `DisplayInventory` ; absent de l'export et reporté (D-E13D-12), il n'est pas porté ici non plus, ni
  `UpdateHudTransitionState` (état du portrait seul, voir la doc de classe d'`AlundraInventoryDirector`,
  `:43-53`).

### 1.6 Les graphismes et les textes sont déjà exportés **[mesuré]**

- **Les cinq nouvelles boîtes** : 529 cases (210 + 105 + 72 + 72 + 70), **toutes** retrouvées à l'identique dans
  `alundra-project/UI/wind-sprites.json`, toutes en `X + 8 × col`, `Y + 8 × rang`, aucune hors de sa boîte ;
  **copies A et B égales case pour case** sur les cinq boîtes ; palettes 3 (pierre) et 0 (parchemin). Les
  fonds de ces boîtes sont des images dessinées (82, 51, 64, 64 et 40 tuples distincts), pas des cadres
  réguliers : même cas que le principal, même solution (D-E13D-13).
- **La description et l'argent/faucons/clés** sont les boîtes du principal : leurs images cuites existent
  (D3.b, sprites `973a9208…` et `e8d58247…`).
- **Les icônes** de toutes les armoiries (7), de tous les objets-clés (12), des quatre armures et des quatre
  bottes ont leur sprite dans `Data/item-icon-index.json` ; leurs noms et lignes de description sont dans
  `Dialogues/etc-index.json` + `global-strings.json` (quelques secondes lignes valent `null`, déjà lues comme
  vides depuis D4).
- **Les tables et les boîtes** du sous-inventaire (onze tables et les cinq en-têtes `(X, Y, Width, Height)`) sont
  **[binaire] égales** dans l'exécutable France.
- **Le convertisseur n'a rien à changer** : `UiBoxWriter` cuit **chaque** boîte de `UiBoxes.csv`
  (`alundra-casaengine-project-converter/Writers/UiBoxWriter.cs:65`), sans nom codé en dur. Il suffit d'ajouter
  les cinq boîtes aux deux CSV de l'analyseur.

### 1.7 Côté portage

- `AlundraInventoryDirector` lit `L1`/`R1` sans rien en faire (`:586-588`, test
  `AlundraInventoryDirectorTests.cs:562-581`, qui vérifie aussi l'absence de son) ; il porte un crochet jamais
  armé, `_pendingSubInventoryTransition` (`:307`, lu seulement à `:627`), qui tient la place du bit 0 de
  `g_postProcessState`.
- La boucle de la manette au tick (`AlundraWorldProxy.cs:1925-1938`) : `TickPad.Update`, directeur, présentateur.
  Tout consommateur des fronts du tick doit y tourner.
- Le présentateur du principal empile l'écran au premier tick dessiné et le retire quand le directeur devient
  inactif (`AlundraInventoryPresenter.cs:70-79`).
- Les écrans d'Alundra sont des assets du projet liés à un view-model (ADR-0002) : XAML, `.uiscreen` à
  identifiant fixe et `.design.json` dans `alundra-project/UI/Screens/`, que le convertisseur catalogue
  (`UiWriter.RegisterVersionedScreens`) sans jamais y écrire.

---

## 2. Décisions

### 2.1 Reprises du plan de l'inventaire principal (elles valent ici)

D-E13D-1 (porter l'original tel quel pour la logique ; pas au pixel près pour le dessin), D-E13D-3 et ADR-0002
(l'écran est un asset XAML lié à un view-model), D-E13D-4 (aucun contournement : un manque de MGUI ou du moteur se
consigne dans `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md` et arrête la tranche),
D-E13D-8/9 (fronts et répétition au tick), D-E13D-10 (icônes centrées), D-E13D-11 (touches `U` = `L1`, `I` = `R1`,
déjà liées), D-E13D-12 amendée (pas de portrait), D-E13D-13/14 (boîtes en image cuite, copie A), D-E13D-15 (gel sur
tout `MenuOpen`), D-E13D-18 (police `font3` tenue par le registre du moteur).

### 2.2 Proposées, sauf avis contraire (l'auteur les lira au réveil)

| Réf | Décision proposée | Pourquoi |
|---|---|---|
| D-E13D-20 | **Un directeur, un compositeur, un view-model, un présentateur et un écran propres au sous-inventaire** (`AlundraSubInventory…`, `SubInventoryScreen.xaml`), à côté de ceux du principal, sans mode dans les classes existantes | l'original a deux gestionnaires distincts, deux états (`g_forbiddenWarpFlag`, `g_subInventoryState`) et deux rappels (6 et 4) ; le découpage directeur/présentateur/écran est celui du principal |
| D-E13D-21 | **`g_postProcessState` porté tel quel**, dans une petite classe de session `AlundraInventoryPostProcess` (état et passe), exécutée dans la boucle de la manette **après** les deux directeurs ; elle remplace le crochet `_pendingSubInventoryTransition` | un seul état partagé par les deux inventaires, comme l'original ; la passe tourne après les rappels, comme `GraphicManager.cs:1691-1706` |
| D-E13D-22 | **L'horloge exacte du §1.2** : dans le sens sous-inventaire → principal, la tête de `DisplayInventory` tourne au tick Tc et la mise en place `FUN_80054f1c` au tick suivant ; le directeur du principal sépare donc ces deux moitiés, liées au même tick seulement pour le déclencheur ordinaire | le tick près est tenu partout ailleurs dans ce port ; l'écart serait de 20 ms, dans la tolérance de l'interface, mais il se tient sans coût |
| D-E13D-23 | **Icônes du sous-inventaire centrées dans une case de 24 × 32 dont le coin est la position d'origine** : armoiries et objets-clés à leur place d'origine, armure et bottes dans leur cadre `wind_039` | extension directe de D-E13D-10 : dans le principal, la case est le cadre de 24 × 32 ; ici seules l'armure et les bottes ont un cadre dessiné, les autres ont leur place d'origine comme coin |
| D-E13D-24 | **Objets-clés : l'exécutable, pas la décompilation** : une fois la recherche épuisée, plus de recherche (§1.5) | mesuré dans le binaire ; la décompilation telle quelle ferait apparaître des doublons |
| D-E13D-25 | **Le texte déroulant du sous-inventaire a sa propre copie** de la machine, dans son directeur, sans refactoriser celle du principal | l'original a deux fonctions et deux jeux de globales ; le principal est validé en jeu, on ne le touche que pour la bascule. Une factorisation éventuelle est un point de réflexion (§6) |
| D-E13D-26 | **Couche `Menu`, modal**, comme le principal ; les deux écrans ne sont **jamais** empilés ensemble | la bascule passe par des ticks sans aucun inventaire (§1.2) |
| D-E13D-27 | **Le curseur du sous-inventaire réutilise l'animation `ui_inventory_cursor`** (mêmes images, mêmes décalages de phase) | même fonction de curseur dans l'original, sur une animation propre dont seul le compteur diffère ; l'animation d'interface tourne sur son horloge depuis B2 |
| D-E13D-28 | **La seconde ligne de description du sous-inventaire n'est jamais montrée**, comme dans le jeu français (§1.5) : la machine lit l'octet d'avant la chaîne, toujours nul, et passe à 0xcf | fidélité de comportement (D-E13D-1) à ce que l'exécutable fait vraiment ; le contraire serait une amélioration. **À confirmer par l'auteur** (§6, point 3) : la montrer comme le principal tient en une constante |
| D-E13D-29 | **`Triangle` ferme aussi les deux inventaires** (masque `0x813`), sans les ouvrir ; pour le principal, correction de D4 dans sa propre tranche (SI3.a) | mesuré dans l'exécutable (§1.1) ; la décompilation avait perdu le bit |

---

## 3. Tranches

Un commit par tranche, avec la mise à jour de ce plan ; un vérificateur frais par tranche à risque ; régime de
preuve par manifeste et double export dès que l'export change. **Chaque tranche ne commence que lorsque ses
prérequis sont clos.** Suites de référence mesurées dans le worktree avant toute modification :
`Alundra.Tests` **1089/1089**, convertisseur **190/190** ; export du worktree identique à celui du checkout
principal hors fins de ligne des six fichiers d'écrans versionnés et `report.json` (manifeste §7).

### ✅ SI0 — Les mesures (lecture seule) — close à la rédaction

Tout ce qui est marqué [mesuré] ou [binaire] au §1, scripts au §7 : les cinq boîtes contre l'atlas (529/529,
A = B) ; les onze tables et les cinq en-têtes contre l'exécutable ; la garde perdue des objets-clés ; les objets,
icônes, noms et descriptions ; le cadre `wind_039` creux. **Contre-vérification décompilation ↔ exécutable** des
fonctions du sous-inventaire, de la branche `L1`/`R1` du principal et du post-traitement : résultat au §7.

### ✅ SI1 — Analyseur : les cinq boîtes du sous-inventaire en CSV — faite le 2026-09-24 (analyseur `8f403d5`)

**Prérequis** : SI0. **Dépôt** : l'analyseur, branche `chantier/e13d-sub-inventory-boxes` depuis `9adba14`.
- Ajouter **à la fin** de `AlundraTools/AlundraTools/UiBoxes.csv` les cinq boîtes propres au sous-inventaire, dans
  l'ordre de leurs appels de dessin (`SubInventoryManager.cs:456-460`) : `UIBoxConfiguration_800af664`,
  `UIBoxConfiguration_800b06dc`, `UIBoxConfiguration_800b122c`, `UIBoxConfiguration_800b1d7c`,
  `UIBoxConfiguration_800b287c` ; et à la fin de `UiBoxCells.csv` leurs **529** cases de la copie A, brutes, même
  format. Les deux boîtes partagées ne sont pas répétées. Les lignes existantes ne changent pas d'un octet.
- Le commentaire de `AlundraTools.csproj` qui décrit les deux CSV (« the main inventory's seven … ») est complété
  pour les cinq boîtes du sous-inventaire. Le projet est **chargé** après la modification (`dotnet build` de
  `AlundraTools.csproj`, piège des commentaires XML, voir la mémoire des exécuteurs).
- Générateur : le script de D3.a (§7 de `docs/plan-e13d-inventaire.md`) étendu, recopié au §7 de ce plan.

**Acceptation** : un vérificateur **indépendant** (ne reprend rien du générateur) relit `StaticVariables.cs` et
retrouve chaque ligne : 12 boîtes, 1351 cases (822 + 529), 0 manquante, 0 en trop, même ordre ; `git diff` ne montre
que des lignes ajoutées en fin de fichier (et le commentaire du `.csproj`). Commit :
`feat(tables): export the sub-inventory's box layout as CSV (E13.d SI1)`.

**Fait** : générateur et vérificateur au §7. Le générateur régénère les sept premières boîtes et leurs 822 cases
**à l'octet près** puis ajoute les cinq nouvelles (5 lignes, 529 cases) ; le vérificateur indépendant rend
`boxes: 12; cells in csv: 1351; cells re-read: 1351; missing from csv: 0; extra in csv: 0; same order: True`, et,
sur une case corrompue exprès puis rendue à l'octet près, `missing 1; extra 1; same order: False` (contrôle
négatif). `git diff` : 534 lignes ajoutées, aucune retirée, plus le commentaire du `.csproj`. Le projet est
évalué par MSBuild et voit les deux CSV (`dotnet msbuild -getItem:None`) ; son build complet échoue dans ce
worktree sur `AlundraGame.csproj`, faute du sous-module MGUI propre à l'analyseur, non cloné ici — sans lien avec
la modification.

### ✅ SI2 — Parent : la référence de l'analyseur et l'export prouvé — faite le 2026-09-24

**Prérequis** : SI1. Aucune ligne du convertisseur ne change (§1.6).
- **Prédiction écrite avant** : diff d'export = 15 fichiers ajoutés (`UI/Textures/<boîte>.png`,
  `UI/Textures/<boîte>.texture`, `UI/<boîte>.sprite` pour les cinq boîtes) + `AssetInfos.json` + `report.json` ;
  compteurs `UiBoxes.Boxes` = 11, `UiBoxes.Cells` = 1351, `UiBoxes.CellsWithoutTile` = 0 ; aucune erreur.
- Export complet **en place** dans le worktree, contre le manifeste de référence (§7) ; mesuré ⊆ prédit ;
  **double export** ⊆ `{report.json}` ; `AssetVerifier` PASSED.
- **Preuve au pixel** : chaque image cuite égale, pixel pour pixel en RGBA, à une composition indépendante faite
  depuis les `SpritesA` de `StaticVariables.cs` et `wind.png`, sans passer par les CSV ni par le convertisseur.
- Suites : convertisseur 190/190.
- Commit : `chore(submodules): point at the analyser with the sub-inventory boxes (E13.d SI2)`, avec ce plan
  (identifiants des cinq sprites relevés dans le `.sprite` exporté, pour SI4).

**Fait** :

| Preuve | Résultat |
|---|---|
| Prédiction écrite avant | 15 ajouts + `AssetInfos.json` + `report.json` |
| Export complet en place | **exactement** la prédiction ; `UiBoxes.Boxes` 11, `UiBoxes.Cells` 1351, `UiBoxes.CellsWithoutTile` 0 ; 0 erreur ; vérification PASSED (19 533 chargés) |
| Catalogue | 15 entrées ajoutées, toutes au nom d'une des cinq boîtes, aucune retirée, l'ordre des anciennes gardé |
| Double export | `report.json` seul |
| Preuve au pixel | **0 pixel différent** sur les cinq images, en RGBA complet, contre une composition faite depuis `StaticVariables.cs` et le `wind.png` de l'extracteur ; méthode contrôlée sur deux boîtes du principal (0 pixel) |
| Suites | convertisseur 190/190 (aucune ligne du convertisseur changée) |

Identifiants des sprites, pour SI4 : armurerie `UIBoxConfiguration_800af664` = `10eb721a-628f-549c-bcaa-2ab2f670dbc3` ;
objets-clés `UIBoxConfiguration_800b06dc` = `af453798-29af-5757-aaa5-10f1ff92af49` ; nom de l'armure
`UIBoxConfiguration_800b122c` = `d5a10996-bb74-5e48-967b-be8bdde2c148` ; nom des bottes
`UIBoxConfiguration_800b1d7c` = `f7468bc4-faa0-5222-947b-037b12200759` ; icônes `UIBoxConfiguration_800b287c` =
`1070d22e-b7e8-50d5-a58c-ccf80667185e` ; boîtes partagées : description `973a9208-c867-57fe-bee3-cf30237221ef`,
argent/faucons/clés `e8d58247-f02b-57cb-9ebf-f1df2b9be874`.

### ⏳ SI3.a — DLL : `Triangle` ferme aussi l'inventaire principal (correction de D4)

**Prérequis** : SI0. Tranche à part, pour pouvoir être relue et, au besoin, annulée seule.
- `AlundraInventoryDirector.RunInput` : le masque de fermeture devient `Start | Triangle | L2 | R2` (`0x813`,
  §1.1), avec la citation de l'exécutable en commentaire ; le déclencheur d'ouverture ne change pas (`0x803`).
- **Tests** : `Triangle` ferme (son 5, jauge rappelée, `MenuOpen` retiré à la fin du glissement) ; `Triangle`
  n'ouvre pas ; les tests existants de fermeture par `Start`/`L2`/`R2` inchangés et verts.
- Build, `Alundra.Tests`. Pas de vérificateur séparé : SI3.a entre dans la vérification de SI3. Commit :
  `fix(inventory): Triangle closes the main inventory as in the executable (E13.d SI3.a)`.

### ⏳ SI3 — DLL : le directeur du sous-inventaire et la bascule

**Prérequis** : SI0, SI3.a. Indépendante de SI1/SI2 (aucun graphisme).
- **`AlundraSubInventoryDirector`** (singleton de session, pur, sans MGUI, même forme que le principal) :
  - `Open()` = `InitializeSubInventory` (§1.3) + noms de l'armure et des bottes ; `Tick()` = `DisplaySubInventory`
    (§1.4) : glissement (ordre et fin du §1.4, `AdvanceBoxTween` du principal réutilisé ou recopié), manette
    (quatre tables du §1.5, son 1, texte remis à zéro), `Start`/`Triangle`/`L2`/`R2` (masque `0x813` ; fermeture,
    son 5, `AlundraHudDirector.InitializeHudPositionBeforeHide`), `L1`/`R1` (fermeture, son 5, `MenuOpen`,
    `g_postProcessState = 2`), fin de glissement (état à 0, origines, `MenuOpen` retiré seulement si
    `(g_postProcessState & 2) == 0`, pas de dessin ce tick) ;
  - la queue par tick : table des objets-clés (règle de l'exécutable, D-E13D-24), puis le texte déroulant
    (sa propre copie de la machine, D-E13D-25 ; résolution de l'objet du §1.5 ; « non possédé » = rien, texte
    figé ; **seconde ligne jamais révélée**, D-E13D-28 : à l'état 0x8f la machine lit l'octet d'avant la chaîne,
    porté comme « fin de chaîne », sans lire d'autre chaîne) ;
  - ce que l'écran lit, en propriétés simples : `IsActive` (`g_subInventoryState != 0`), `IsDrawn` (comme le
    principal : faux au tick d'ouverture et au tick de fin), `State`, `SelectedPosition`, `BoxPosition(i)` pour
    les sept boîtes, `ArmorName`, `BootsName`, `KeyItemIds` (5), `DrawnDescriptionLine0/1` (ce qui est dessiné
    au tick, règle de D5).
- **`AlundraInventoryPostProcess`** (D-E13D-21) : `State` (0, 1, 2) et `Run()`, qui fait le §1.1 :
  état 1 et principal libre → 0, puis `HudDirector.InitializeHudPosition()` (sans effet quand la jauge est
  cachée, gardes déjà portées), ouverture du sous-inventaire, **son 4** ; état 2 et sous-inventaire libre → 0,
  puis la tête de `DisplayInventory` du principal. « Libre » = le drapeau du rappel : pour le principal,
  `ForbiddenWarpFlag == 0` **et** aucune mise en place en attente ; pour le sous-inventaire, `State == 0`.
- **`AlundraInventoryDirector`** : branche `L1`/`R1` de `RunInput` (fermeture par `RunCloseSetup`, `MenuOpen`,
  `PostProcess.State = 1`, **pas** d'`InitializeHudPositionBeforeHide`) ; la fin de glissement lit
  `(PostProcess.State & 1)` au lieu de `_pendingSubInventoryTransition`, qui disparaît ; `RunDisplayInventory`
  séparé en **tête** (`DisplayInventory`) et **mise en place** (`FUN_80054f1c`) : le déclencheur fait les deux au
  même tick, comme aujourd'hui ; le post-traitement ne fait que la tête et arme une mise en place que le `Tick`
  suivant exécute avant tout le reste (D-E13D-22). Le commentaire de classe « L1/R1 : OUT OF SCOPE » est
  remplacé. Aucun autre comportement du principal ne change.
- **`AlundraWorldProxy`** : `AttachToWorld` du nouveau directeur à côté du principal ; dans la boucle de la
  manette : `TickPad.Update`, directeur principal, directeur du sous-inventaire, `PostProcess.Run()`, puis les
  présentateurs.
- **Tests** (`Alundra.Tests`), écrits pour que le chemin non visé donne une autre valeur :
  - aller-retour complet au tick près, sur les deux directeurs réels et la vraie passe : T0, Tc, Tc+1, Tc+2 du
    §1.2 dans les deux sens ; les sons 5 puis 4 ; **`MenuOpen` jamais retiré** d'un bout à l'autre (relevé à
    chaque tick) ; la jauge ni rappelée ni re-cachée pendant la bascule ; fermeture finale par `Start` : jauge
    rappelée, `MenuOpen` retiré au tick de fin ;
  - navigation : les 14 × 4 transitions contre les tables, son 1, texte remis à zéro ; répétition au tick ;
  - objets-clés : 0, 1, 2 et 6 objets possédés → positions remplies et vides, **aucun doublon** (le test échoue
    avec la règle de la décompilation) ; armoiries non possédées non décrites, texte figé ;
  - description : nom puis première ligne, **jamais la seconde**, même pour un objet qui en a une (l'armure 17) :
    le test échoue si la machine lit `ligne2[c − 0x8f]` comme le principal ;
  - `Triangle` ferme le sous-inventaire (masque `0x813`) ;
  - nouvelle partie : noms « Armure en tissu » et « Bottes courtes » ;
  - le test `SubInventoryShoulderButtons_Ignored_NoStateChange` est remplacé par celui de la bascule ;
  - câblage par le vrai `AlundraWorldProxy.Update` : `R1` dans le principal ouvre le sous-inventaire ; une
    mutation qui retire la passe du post-traitement de la boucle fait échouer un test.
- Build, `Alundra.Tests` (tous verts). **Vérificateur frais.** Commit :
  `feat(inventory): port the sub-inventory director and the L1/R1 switch (E13.d SI3)`.

### ⏳ SI4 — L'écran du sous-inventaire (asset lié) et son présentateur

**Prérequis** : SI2 (identifiants des cinq sprites), SI3.
- `alundra-project/UI/Screens/SubInventoryScreen.xaml`, `.uiscreen` (identifiant fixe, nouveau GUID) et
  `.design.json` (données de conception d'une nouvelle partie au repos, produites comme celles du principal) :
  sept images de boîtes (cinq nouvelles, deux partagées, par leurs identifiants fixes), 7 icônes d'armoiries,
  5 d'objets-clés, 2 d'armure et bottes, 2 cadres `wind_039`, 2 noms et 2 lignes de description en `font3`,
  8 chiffres (4 + 2 + 2), le curseur (`ui_inventory_cursor`, D-E13D-27). Ordre de dessin (§1.5) : boîtes,
  cadres, icônes, textes et chiffres, curseur.
- `AlundraSubInventoryComposer` (pur), `AlundraSubInventoryViewModel` (observable, notification sur changement
  seulement, types identiques aux cibles), `AlundraSubInventoryPresenter` (empile au premier tick dessiné,
  retire à la fin), `AlundraSubInventoryScreen` (`XamlUIScreenBase`, couche `Menu`, modal, `font3` tenue par le
  registre comme le principal, libérée dans `Dispose`) ; câblage et libération dans `AlundraWorldProxy`
  (`TryWire…Once`, `OnEndPlay`), comme l'écran du principal.
- Icônes centrées (D-E13D-23) par la formule d'`AlundraHudIcon` sur la taille réelle du sprite.
- **Tests** : XAML lu depuis l'asset versionné, éléments nommés trouvés, liaison sans affichage par chemins
  imbriqués (une mutation qui retire le contexte fait échouer), compositeur (places du §1.5, centrage, chiffres,
  visibilités), présentateur ; **jamais les deux écrans empilés** pendant une bascule (tick par tick).
- Export : le convertisseur catalogue le nouveau `.uiscreen` ; diff prédit = `AssetInfos.json` (+1 entrée) +
  `report.json` ; double export ⊆ `{report.json}` ; vérification PASSED.
- Build, `Alundra.Tests`, convertisseur. **Vérificateur frais.** Commit :
  `feat(inventory): show the sub-inventory as a project asset bound to a view model (E13.d SI4)`.

### ⏳ SI5 — Recette en jeu par capture, prédite avant d'être prise

**Prérequis** : SI4. Harnais hors dépôt, copie de `d6-font` (session du 2026-09-21), référence au moteur du
worktree, projet du worktree. Parcours sur la 389 après la prise de contrôle : `Start` (principal), `R1`
(sous-inventaire), attente, capture A ; `Right` ×2 et attente de la description, capture B ; `L1` (principal),
capture C ; `R1`, puis `Start` : fermeture, jauge revenue, capture D. Journal au tick : `MenuOpen`, états des deux
directeurs et du post-traitement, sons demandés, famille de police des textes.
**Prédiction écrite avant** (fichier dans le scratchpad) : A : sept boîtes à leur place, « Armure en tissu » et
« Bottes courtes » dans leurs boîtes de nom, leurs icônes centrées dans deux cadres, armurerie et objets-clés
vides, chiffres à zéro, curseur en position 0 ; B : curseur sur la position atteinte par la table `Right`, la
description qui s'y rapporte (ou rien pour une position vide), jamais de seconde ligne ; C : l'inventaire
principal comme à D5 ; `MenuOpen` jamais nul entre le premier `Start` et la fermeture finale ; sons, dans
l'ordre : 4 (ouverture), 5 puis 4 (vers le sous-inventaire), 1 par déplacement, 5 puis 4 (vers le principal),
5 puis 4 (de nouveau vers le sous-inventaire), 5 (fermeture) ; `font3` partout.
Puis **un vérificateur frais sur l'ensemble** (directeur, bascule, écran, recette), la tranche touchant la
logique, l'asset et l'export.

### 🧪 SI6 — Recette de l'auteur

`Start` puis `R1` (touche `I`) ou `L1` (touche `U`) : le sous-inventaire glisse ; naviguer dans les 14 positions,
en maintenant une direction ; lire la description de l'armure et des bottes ; revenir au principal par `L1`/`R1`,
y retourner ; fermer par `Start` depuis le sous-inventaire : la jauge revient ; le monde reste figé du début à la
fin, sans une image de reprise pendant la bascule ; après un changement de carte, les textes restent en `font3`.
Et trancher les points du §6.

---

## 4. Acceptation d'ensemble

E13.d est close quand, en jeu : `L1`/`R1` passent de l'inventaire principal au sous-inventaire et retour, avec
les glissements, les sons et l'horloge de l'original ; le sous-inventaire montre l'armurerie, les objets-clés,
l'armure, les bottes, leurs noms, la description déroulée et les chiffres ; `Start`/`L2`/`R2` le ferment et la
jauge revient ; le monde est gelé sans interruption ; les boîtes ont l'aspect de l'original (images cuites) ;
suites vertes ; chaque export prouvé par double export.

## 5. Arrêts

- Une contre-vérification binaire (SI0) qui contredit le §1 : le plan est corrigé et relu avant SI3.
- Un manque de MGUI ou du moteur : consigné, la tranche s'arrête (D-E13D-4).
- Un diff d'export hors du prédit, un double export hors de `{report.json}`, une image cuite qui diffère de sa
  composition indépendante.
- Un test existant de l'inventaire principal qui doit changer pour une autre raison que la bascule ou
  `Triangle` (SI3.a).
- `alundra-project/` supprimé à la main, `CasaEngine.Launcher/Program.cs` indexé, une écriture dans le checkout
  principal.

## 6. Points ouverts (pour l'auteur)

1. **D-E13D-20 à D-E13D-29** (§2.2) sont appliquées sauf avis contraire ; D-E13D-23 (centrage des armoiries et des
   objets-clés dans une case de 24 × 32 à leur place d'origine) est la plus visible.
2. **Factoriser le texte déroulant** du principal et du sous-inventaire (D-E13D-25) : deux copies aujourd'hui,
   comme l'original.
3. **La seconde ligne de description du sous-inventaire** (D-E13D-28) : le jeu français ne la montre jamais, par
   un décalage d'un octet de l'exécutable. Le portage fait de même. (a) Garder cette fidélité ; (b) la montrer comme
   le principal (une constante). Recommandé : (a), la règle du portage, sauf si l'auteur tient cet écart pour un
   défaut que le portage doit corriger.
4. **Le déroulé du texte octet par octet** (§1.5, dernier point) : l'original compte les paires d'accent pour deux,
   le portage (principal compris, validé en D6) pour un. Écart de rythme (une lettre accentuée s'affiche en 3 images
   au lieu de 6), sans autre effet aujourd'hui. Le corriger toucherait les deux inventaires et la lecture des
   chaînes ; pas fait ici.
5. **`Triangle` ferme l'inventaire principal** depuis SI3.a (D-E13D-29) : un changement de comportement de
   l'inventaire principal déjà validé, à revoir en recette.

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-24 | Worktree isolé créé (une autre session travaille dans le checkout principal) ; build, export complet et suites de référence dans le worktree ; export identique à celui du checkout principal hors fins de ligne des écrans versionnés. |
| 2026-09-24 | Reconnaissance : lecture intégrale du sous-inventaire en session principale ; carte du portage et audit de T7/E11.b par deux relevés relus par un contradicteur. **T7 et E11.b sont livrées et validées en jeu depuis le 2026-09-07** (`docs/plan-transitions-carte.md` §T7, `docs/plan-e11b-opcodes-audio.md` dernière section) ; le plan maître était resté à « en cours ». |
| 2026-09-24 | Mesures SI0 (scripts ci-dessous). **Garde perdue par la décompilation** dans la boucle des objets-clés, établie dans l'exécutable France. |
| 2026-09-24 | **Contre-vérification décompilation ↔ exécutable** de quinze fonctions (ouverture, fermeture, image par image, texte, icônes, armurerie, chiffres, branche `L1`/`R1` et fin de glissement du principal, post-traitement, `StartFadeOut`) : quatre relevés, chacun repris par un contradicteur qui a refait les lectures. Résultat ci-dessous. |
| 2026-09-24 | Relecture du plan par un relecteur frais : **READY** au premier passage. Exécution lancée en mode AUTO, sur la demande d'autonomie de l'auteur. |
| 2026-09-24 | **SI1 faite** (analyseur `8f403d5`, branche `chantier/e13d-sub-inventory-boxes`) : 12 boîtes, 1351 cases, vérificateur indépendant et contrôle négatif. **SI2 faite** : diff d'export exactement prédit, double export = `report.json`, 0 pixel différent sur les cinq images cuites. |

### SI0 — la contre-vérification décompilation ↔ exécutable (2026-09-24)

Désassemblage de `ALUN_CD.EXE` (France) par un script de lecture (`capstone`, ci-dessous). Chaque écart a été
confirmé par un second relevé indépendant.

| Fonction | Verdict | Disposition |
|---|---|---|
| `FUN_80052c64` (objets-clés) | **écart** : garde `beq $s0, $s6` en `0x80052cc0` perdue | le portage suit l'exécutable (D-E13D-24) |
| `DisplaySubInventory`, `FUN_80056598` | **écart** : masque de fermeture `0x813` (`Triangle` en plus) en `0x80053634` et `0x80056924` | D-E13D-29, SI3.a et SI3 |
| `FUN_80053fdc` (texte) | **écart** : ligne 2 lue à `c − 0x90` (`0x80054314`), octet précédent nul pour les 98 objets | D-E13D-28 |
| `InitializeSubInventory` | écart mort : hauteur de la mauvaise boîte pour la source `y` des bottes, `Y ≥ 0` | aucun effet |
| `FUN_80052f24` | argument `0x140` au lieu de 0 pour l'abscisse initiale des noms, réécrite au dessin | aucun effet |
| `DisplayAmountOfMoneyFalconKeys2` | écrit dans les sprites du principal au lieu des siens ; même palette 5 | aucun effet |
| `FUN_80053144`, `FUN_80052fb4`, `FUN_80053270` | indices de double tampon repliés (A seule, A = B mesuré) | aucun effet |
| `StartFadeOut` | pointeurs vivants et `+2` en X pour le portrait | portrait non porté |
| `FUN_800526cc`, `FUN_80053f3c`, `FUN_80053e54`, `FUN_80052dd8`, `FUN_8004e640`, `InitializeSubInventorySprite`, post-traitement de `GraphicManager` (`0x80048054`), branche `L1`/`R1` et fin de glissement du principal | **équivalents** | — |

Faits confirmés au passage : `ButtonsJustPressedByInterval` est bien le champ lu (décalage `0x16` de
`g_padState1`) ; la table des boîtes du curseur (`0x800b44b8`) donne armurerie pour 0-6, icônes pour 7-8,
objets-clés pour 9-13 ; le post-traitement teste l'emplacement 6 pour l'état 1 et l'emplacement 4 pour l'état 2,
et remet l'état à 0 avant l'appel ; `FUN_80047cb0` n'efface que le demi-mot bas des drapeaux du rappel.

Script de désassemblage (lecture seule) :

```python
"""Read-only MIPS disassembly of one function of an Alundra executable, to check a decompiled loop against
the binary. Usage: python disasm.py <exe path> <start_hex> <end_hex>"""
import struct
import sys
import capstone

path = sys.argv[1]
start = int(sys.argv[2], 16)
end = int(sys.argv[3], 16)
data = open(path, 'rb').read()
t_addr, t_size = struct.unpack_from('<II', data, 0x18)
print(f'{path}: text 0x{t_addr:08x}..0x{t_addr + t_size:08x}')
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
addr = start
while addr < end:
    off = 0x800 + (addr - t_addr)
    word = data[off:off + 4]
    ins = next(md.disasm(word, addr), None)
    text = f'{ins.mnemonic:8s} {ins.op_str}' if ins else '(invalid)'
    print(f'  {addr:08x}: {struct.unpack("<I", word)[0]:08x}  {text}')
    addr += 4
```

Vérification de l'octet qui précède la seconde ligne (sortie : `98 prev=0x0`) :

```python
import struct, sys
b = open(sys.argv[1], 'rb').read()  # DATA/ETC_RES.R of the France version
idx = struct.unpack_from('<1024h', b, 0)
def s(o):
    e = b.index(0, o)
    return b[o:e]
for i in range(0x62):
    o2 = idx[0x300 + i]; o1 = idx[0x280 + i]
    prev = b[o2 - 1] if o2 > 0 else None
    print(i, hex(o1), hex(o2), 'prev=%s' % (hex(prev) if prev is not None else None), s(o1)[:30], s(o2)[:30])
```

Tables du sous-inventaire contre l'exécutable (sortie : onze tables et cinq en-têtes `CONFIRMED`) : script
`d7_tables_binary.py`, lecture des `int32` en `0x800b42dc` … `0x800b44f0` et des quatre `int16` d'en-tête des cinq
boîtes, comparés aux littéraux de `StaticVariables.cs:12301-12340` et `:11279-11327`.

Cases des cinq boîtes contre l'atlas (sortie : 210, 105, 72, 72 et 70 cases, toutes trouvées, aucune hors de sa
boîte ni mal placée, A = B) : script `d7_boxes_measure.py`, même règle que D0.1/D0.2 du plan principal, avec un
lecteur qui accepte aussi la forme `new SPRT[] { … }` des tableaux du sous-inventaire.

### SI1 — le générateur des CSV et son vérificateur indépendant (2026-09-24)

Générateur (lancé avec `alundra-datas-analyser/AlundraTools` en entrée et `AlundraTools/AlundraTools` en sortie) :

```python
"""SI1 of docs/plan-e13d-sous-inventaire.md: writes UiBoxes.csv and UiBoxCells.csv from the decompilation.

Extension of D3.a's generator (docs/plan-e13d-inventaire.md section 7). The first seven boxes are the seven
DisplayUiBoxes calls of the main inventory's per-frame function, in their drawing order
(MainInventoryManager.cs:914-920) - unchanged. Then the sub-inventory's own boxes: the FUN_80053270 calls of
DisplaySubInventory (SubInventoryManager.cs:456-462), in their order, skipping the two boxes the main
inventory already lists (description, money/falcon/key). For each box, its UIBoxConfiguration literal gives
X, Y, Width, Height and SpritesA; the cells are the SPRT literals of SpritesA, raw, in array order (copy A
only, D-E13D-14). Decimal integers, ';' separator, header row, CRLF line endings, no BOM.
Usage: python si1_generate.py <analyser AlundraTools dir> <output dir>
"""
import io
import os
import re
import sys

ROOT = sys.argv[1]
OUT = sys.argv[2]
SRC = os.path.join(ROOT, 'AlundraEngine', 'StaticVariables.cs')
MAIN = os.path.join(ROOT, 'AlundraEngine', 'UI', 'MainInventoryManager.cs')
SUB = os.path.join(ROOT, 'AlundraEngine', 'UI', 'SubInventoryManager.cs')

src = io.open(SRC, encoding='utf-8-sig').read()
main_calls = re.findall(r'DisplayUiBoxes\(_gameEngine\.StaticVariables\.(\w+)\)', io.open(MAIN, encoding='utf-8-sig').read())
assert len(main_calls) == 7, main_calls
sub_calls = re.findall(r'FUN_80053270\(_gameEngine\.StaticVariables\.(\w+)\)', io.open(SUB, encoding='utf-8-sig').read())
assert len(sub_calls) == 7, sub_calls
calls = main_calls + [c for c in sub_calls if c not in main_calls]
assert len(calls) == 12, calls

CONFIG_FIELD = re.compile(r'\b(X|Y|Width|Height|SpritesA)\s*=\s*([^,\n}]+)')
SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
SPRT_FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def box_config(name):
    start = src.index(name + ' = new UIBoxConfiguration')
    body = src[src.index('{', start):src.index('};', start)]
    fields = {k: v.strip() for k, v in CONFIG_FIELD.findall(body)}
    return {k: (int(fields[k], 0) if k != 'SpritesA' else fields[k]) for k in ('X', 'Y', 'Width', 'Height', 'SpritesA')}


def sprt_cells(array_name):
    # Two literal forms coexist in StaticVariables.cs: "X =\n[ ... ];" and "X = new SPRT[]\n{ ... };".
    marker = re.search(r'\b' + re.escape(array_name) + r'\s*=\s*(new\s+SPRT\s*\[\s*\]\s*)?', src)
    rest = src[marker.end():marker.end() + 40].lstrip()
    opener = rest[0]
    assert opener in '[{', (array_name, rest[:20])
    closer = ']' if opener == '[' else '}'
    start = src.index(opener, marker.end())
    depth = 0
    for end in range(start, len(src)):
        depth += {opener: 1, closer: -1}.get(src[end], 0)
        if depth == 0:
            break
    cells = []
    for m in SPRT.finditer(src[start:end]):
        fields = dict(SPRT_FIELD.findall(m.group(1)))
        cells.append([int(fields[k], 0) for k in ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')])
    return cells


box_rows = ['box;x;y;width;height']
cell_rows = ['box;cell;x0;y0;u0;v0;w;h;clut']
for name in calls:
    config = box_config(name)
    box_rows.append(f"{name};{config['X']};{config['Y']};{config['Width']};{config['Height']}")
    if config['SpritesA'] == 'null':
        assert config['Width'] * config['Height'] == 0, name
        continue
    cells = sprt_cells(config['SpritesA'])
    assert len(cells) == config['Width'] * config['Height'], (name, len(cells))
    for index, cell in enumerate(cells):
        cell_rows.append(';'.join([name, str(index)] + [str(v) for v in cell]))


def write(file_name, rows):
    path = os.path.join(OUT, file_name)
    with io.open(path, 'wb') as handle:
        handle.write(('\r\n'.join(rows) + '\r\n').encode('utf-8'))
    print(f'{file_name}: {len(rows) - 1} rows')


write('UiBoxes.csv', box_rows)
write('UiBoxCells.csv', cell_rows)
```

Vérificateur, sans rien reprendre du générateur (liste des douze boîtes écrite à la main) :

```python
"""SI1 acceptance, independent of si1_generate.py: re-reads StaticVariables.cs line by line (no bracket
matching, no shared regex, no call-site parsing: the expected box list is written out by hand from the
decompiled call sites MainInventoryManager.cs:914-920 and SubInventoryManager.cs:456-462) and checks that
every row of UiBoxes.csv and UiBoxCells.csv is found there, that nothing is missing or extra, and that the
order is the same. Handles both literal forms: one SPRT per several lines, and "/*[i]*/ new SPRT { ... },"
on one line. Usage: python si1_verify.py <analyser AlundraTools dir>"""
import io
import os
import sys

ROOT = sys.argv[1]
lines = io.open(os.path.join(ROOT, 'AlundraEngine', 'StaticVariables.cs'), encoding='utf-8-sig').read().splitlines()

EXPECTED_BOXES = [
    'g_UiBoxesInventoryWeaponBackground', 'g_UiBoxesInventoryItemBackground',
    'g_UiBoxesInventoryWeaponNameBackground', 'g_UiBoxesInventoryItemNameBackground', 'UIBoxConfiguration_800b9a10',
    'g_UiBoxesInventoryMoneyFalconKeyIcons', 'g_uiBoxesInventoryDescriptionBackground',
    'UIBoxConfiguration_800af664', 'UIBoxConfiguration_800b06dc', 'UIBoxConfiguration_800b122c',
    'UIBoxConfiguration_800b1d7c', 'UIBoxConfiguration_800b287c',
]


def strip_comments(s):
    while '/*' in s and '*/' in s:
        a = s.index('/*')
        b = s.index('*/', a) + 2
        s = s[:a] + s[b:]
    if '//' in s:
        s = s[:s.index('//')]
    return s


def number(text):
    text = text.strip().rstrip(',').strip()
    for prefix in ('unchecked(', '(short)', '(byte)', '(ushort)', '(uint)'):
        text = text.replace(prefix, '')
    text = text.rstrip(')').strip()
    return int(text, 16) if text.lower().startswith('0x') else int(text)


def fields_of(chunk):
    out = {}
    for part in chunk.replace('{', ',').replace('}', ',').split(','):
        if '=' in part:
            key, value = part.split('=', 1)
            key = key.strip().split()[-1] if key.strip() else ''
            try:
                out[key] = number(value)
            except ValueError:
                out[key] = value.strip()
    return out


def config(name):
    i = next(k for k, l in enumerate(lines) if l.strip().startswith(name + ' = new UIBoxConfiguration'))
    block = []
    for l in lines[i + 1:]:
        block.append(strip_comments(l))
        if l.strip().startswith('};'):
            break
    return fields_of(' '.join(block))


def array_cells(name):
    i = next(k for k, l in enumerate(lines) if (name + ' =') in l and 'SPRT[]' in l)
    cells, current = [], None
    for l in lines[i + 1:]:
        s = strip_comments(l).strip()
        if s.startswith('new SPRT'):
            body = s[len('new SPRT'):]
            if '}' in body:  # one-line literal
                f = fields_of(body)
                cells.append(tuple(f[k] for k in ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')))
                continue
            current = body
            continue
        if current is not None:
            current += ' ' + s
            if s.startswith('}'):
                f = fields_of(current)
                cells.append(tuple(f[k] for k in ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')))
                current = None
            continue
        if s.startswith('];') or s.startswith('};'):
            break
    return cells


def rows(file_name):
    data = io.open(os.path.join(ROOT, 'AlundraTools', file_name), 'rb').read()
    assert not data.startswith(b'\xef\xbb\xbf') and data.endswith(b'\r\n') and b'\n' not in data.replace(b'\r\n', b'')
    text = data.decode('utf-8').split('\r\n')[:-1]
    return text[0], [r.split(';') for r in text[1:]]


header, boxes = rows('UiBoxes.csv')
assert header == 'box;x;y;width;height', header
cell_header, cells = rows('UiBoxCells.csv')
assert cell_header == 'box;cell;x0;y0;u0;v0;w;h;clut', cell_header
assert [b[0] for b in boxes] == EXPECTED_BOXES, [b[0] for b in boxes]

expected_cells = []
for name, x, y, w, h in boxes:
    c = config(name)
    assert (c['X'], c['Y'], c['Width'], c['Height']) == (int(x), int(y), int(w), int(h)), (name, c)
    if c['SpritesA'] == 'null':
        continue
    got = array_cells(c['SpritesA'])
    assert len(got) == c['Width'] * c['Height'], (name, len(got))
    for index, cell in enumerate(got):
        expected_cells.append([name, str(index)] + [str(v) for v in cell])

missing = [r for r in expected_cells if r not in cells]
extra = [r for r in cells if r not in expected_cells]
print(f'boxes: {len(boxes)}; cells in csv: {len(cells)}; cells re-read: {len(expected_cells)}; '
      f'missing from csv: {len(missing)}; extra in csv: {len(extra)}; same order: {cells == expected_cells}')
```
