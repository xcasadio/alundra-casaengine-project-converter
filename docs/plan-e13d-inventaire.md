# Plan — E13.d : l'inventaire principal

**État** : ⏳ rédigé le 2026-09-21, relu **READY** ; décisions de l'auteur du 2026-09-21 (§2.2), dont la
dernière, les boîtes en **image cuite** (D-E13D-13), a fait réécrire D3 et clore D0.1 et D0.2 ; cette
révision, corrigée après une relecture **REVISE** puis relue **READY** (§7), attend l'approbation de
l'auteur. Aucune ligne de code écrite ; D0 attend le
feu vert de l'auteur.
**Naissance** : `docs/plan-conversion-totale.md` §E13, E13.d — « porter l'original tel quel » ; découpage
décidé par l'auteur le 2026-09-19 : **l'inventaire principal d'abord** (ouverture, fermeture, équipement),
puis le sous-inventaire et la bascule L1/R1. Ce plan ne couvre que l'inventaire principal ; le
sous-inventaire aura son propre plan, écrit après la recette de celui-ci.
**Dépend de** : E13.c S3 (`AlundraItemTables`, les compteurs d'objets, `SetPlayerWeaponId` et la chaîne
d'équipement portée ligne à ligne), sur `chantier/e13c-suite`, non mergée.
**Branche** : `chantier/e13d-inventaire`, créée depuis `chantier/e13c-suite` (empilée) ; rebasée sur
`main` quand E13.c y sera.

---

## 1. Les faits établis

Reconnaissance du 2026-09-21 : cinq relevés en lecture seule et une critique de complétude, puis des
mesures reprises en session principale (marquées **[mesuré]**). Tout est cité.

### 1.1 L'ouverture et la fermeture dans l'original

- **Déclencheur** (`GameEngine.cs:1567-1576`) : les drapeaux de contrôle du joueur valent **exactement 0**,
  personne ne tient le joueur, aucun verrou ni délai de warp, aucune transition globale, `Select` n'est
  **pas** maintenu, et l'un quelconque de `Start`, `L2` ou `R2` vient d'être pressé (`PadState.cs:22`,
  masque lu par ET binaire). **Il ne vérifie pas que le héros est au sol** : on peut ouvrir en plein saut.
- **Ouverture** (`MainInventoryManager.DisplayInventory`, `:443-499`) : glissement du HUD
  (`HudManager.InitializeHudPosition`), armement de l'emplacement de rappel 6, portrait d'Alundra préparé
  pour l'effet d'ouverture, noms de l'équipement, **son 4**.
- **Mise en place** (`FUN_80054f1c`, `:503-776`) : `MenuOpen` levé (`:507`), sept boîtes armées pour
  glisser de hors-écran à leur place en **15 ticks** ; puis la fonction de rendu est remplacée par
  `FUN_80056598` (`:775`).
- **Chaque image** (`FUN_80056598`, `:779-923`) : pendant un glissement, seules les boîtes bougent ;
  sinon, lecture de la manette. **Fermeture** par `Start`/`L2`/`R2` (`:846-851`) : glissement inverse,
  **son 5** (`FUN_800556dc`, `:1901-2158`), et `MenuOpen` n'est retiré qu'à la fin du glissement (`:896-899`).
  `L1`/`R1` (`:853-859`) passent au sous-inventaire : **hors de ce plan**.

### 1.2 Le gel du monde — déjà porté **[mesuré]**

- **Original** : `MenuOpen` fait partie de `GameplayBlockedMask`. `EntityManager.UpdateEntities`
  (`EntityManager.cs:367-390`) saute alors, **pour toutes les entités, héros compris**, les évènements,
  compteurs, animations, la physique et les effets ; les évènements de carte s'arrêtent aussi
  (`GameEngine.cs:1667-1671`).
- **Portage** : **la même porte existe depuis T2**. `AlundraEntityScriptProxy.Update` calcule
  `gameplayBlocked` sur `GameplayBlockedMask` et n'appelle `RunGameplayBlockableUpdate` — où vivent
  `MovePlayer`, la physique et la synchronisation d'animation — que s'il est faux
  (`AlundraEntityScriptProxy.cs:828-867`, `:874-1020`) ; le monde porte la sienne
  (`AlundraWorldProxy.cs:1787-1856`). Un dialogue qui lève `MenuOpen` fige déjà héros et entités, constaté
  en jeu (`AlundraEntityScriptProxy.cs:852-853`). **Lever `MenuOpen` suffit donc à figer le héros.**
- **Reste à mesurer** : ce que la porte ne couvre pas, parce que le moteur le fait lui-même en dehors du
  proxy — l'intégration de vitesse et de gravité du contrôleur de personnage (la porte n'y touche pas,
  `AlundraPlayerManager.cs:476-482`), et la lecture d'animation de sprite côté moteur. D0 le mesure,
  notamment pour une ouverture en plein saut.
- **Commentaire périmé** : `AlundraPlayerManager.cs:555-558` affirme encore « our pipeline has no such
  global gate ». Il a trompé la reconnaissance puis la première rédaction de ce plan ; D4 le corrige,
  puisqu'elle touche ce fichier.

### 1.3 La grille, la navigation, l'équipement

- **Grille** de 24 cases, 6 colonnes × 4 rangées ; rangée 0 les armes, rangées 1-3 les objets
  (`MainInventoryManager.cs:1292-1456`). Contenu par case : `g_ItemIdBySlotIndex`
  (`StaticVariables.cs:12355-12361`) — `-1` veut dire « résoudre par la case », `0` vide, sinon un objet
  fixe. Décalages : `g_uiBoxesInventoryAnimationOffsetX/Y` (`:12363-12377`).
- **Navigation** (`:785-829`) : les quatre directions avec bouclage par rangée et par colonne, **son 1**,
  et remise à zéro du texte déroulant. **Croix** (`:831-844`) : case d'arme → `FUN_8005795c`, case d'objet
  → `FUN_80057854`. **Aucun bouton d'annulation** : fermer et annuler sont la même action.
- **Ces lectures utilisent la répétition des touches**, `ButtonsJustPressedByInterval`
  (`:785`, `:798`, `:810`, `:822`, `:834`, `:846`, `:853`) : un premier appui, puis après **20 images**
  maintenues une répétition toutes les `RepeatInterval` images, tant que l'état complet des boutons ne
  change pas (`PadManager.cs:26-74`, `PadState.cs:24-31`). **Le portage ne calcule pas ce champ** :
  `AlundraPadState` n'a que `ButtonsHold` et `ButtonsJustPressed` (`AlundraPlayerController.cs:34-39`),
  et un opcode d'évènement le signale déjà comme manquant (`AlundraEventProgramRunner.cs:1723-1739`).
- **Deux horloges** : l'original met la manette à jour une fois par image de sa boucle fixe à **50 Hz**
  (`GameEngine.cs:1518`). Le portage tourne avec `IsFixedTimeStep` à faux (`ProjectWriter.cs:63`) : l'état
  des boutons est reconstruit **par image rendue** (`AlundraEntityScriptProxy.cs:855-862`), alors que la
  logique avance en **ticks à 50 Hz**, zéro, un ou plusieurs par image rendue (`AlundraLogicClock.cs:5-16`).
  Un front d'appui et un compteur de répétition calculés par image rendue dépendraient donc de la cadence
  d'affichage, et un directeur qui tourne au tick verrait un même front deux fois, ou pas du tout.
- **Équiper une arme** (`FUN_8005795c`, `:1618-1747`) : cinq appels à `SetPlayerWeaponId`, **son 2** à
  l'équipement, **son 3** pour une case vide. **Équiper un objet** (`FUN_80057854`, `:1796-1855`) :
  `SetCurrentItemId`, son 2. Toute la chaîne de données est **déjà portée** par E13.c S3.

### 1.4 Ce qui est dessiné

- **Sept boîtes** de fond, dont six propres à l'inventaire principal (`StaticVariables.cs:11196-11267`) :
  fond des armes (21×6 cases de 8 px), fond des objets (21×13), nom d'arme (18×4), nom d'objet (18×4),
  un espaceur vide, argent/faucons/clés (3×9), plus la boîte de description. Chaque boîte a **deux
  copies** de ses cases, `SpritesA` et `SpritesB`, dont une seule est montrée par image (voir plus bas).
- **[mesuré] Les boîtes ne sont pas des cadres réguliers** : le fond des armes compte **59 tuples
  distincts** `(u0, v0, w, h, clut)`, dont 32 à l'intérieur ; celui des objets **81**, dont 48 à
  l'intérieur ; argent/faucons/clés **21** sur 27 cases. Ce sont des images dessinées case par case, pas
  des cadres en 9 tranches : **le patron case par case doit voyager comme donnée**.
- **[mesuré] Les pixels sont déjà exportés** (D0.1, script et sortie au §7) : les six boîtes à dessiner
  comptent **822 cases** par copie, 1644 pour les deux copies A et B (point suivant) ; **chacune** a son
  tuple `(u0, v0, w, h, clut)` à l'identique
  parmi les entrées de `alundra-project/UI/wind-sprites.json` `(u0, v0, width, height, palette_index)` ;
  toutes font 8×8, toutes tombent dans le rectangle de leur boîte, **exactement** en `X + 8 × col`,
  `Y + 8 × rang`, dans l'ordre de la boucle de dessin : **les cases d'une copie ne se recouvrent pas**. Palettes : `clut` 3 pour la pierre (armes, objets),
  0 pour le parchemin (noms, description), 5 et 6 pour argent/faucons/clés. `wind.png` n'a que deux
  valeurs d'alpha, 0 et 255. La septième boîte, `UIBoxConfiguration_800b9a10`, fait 0×0 case : rien à
  dessiner. **Le patron** (quelle boîte, où, de quelle taille, quelle case dans quel ordre) n'existe que
  dans la décompilation (`StaticVariables.cs:11196-11267`).
- **[lu] A et B sont deux copies de la boîte, pas deux couches** (D0.2, corrigée après relecture, §7).
  `FUN_800548a4` (`GraphicManager.cs:1829-1879`) ne fait dans l'original qu'**initialiser** les deux
  copies : les seuls appels d'origine, en commentaire, sont les macros PsyQ `SetSprt`,
  **`SetSemiTrans(sprite, 0)`** et **`SetShadeTex(sprite, 1)`** (`:1858-1860`) ; l'appel à
  `Renderer.AddSprite` (`:1867`) est propre au portage de la décompilation, qui dessine ainsi les deux.
  Le dessin par image est `DisplayUiBoxes` (`0x80055d78`, `MainInventoryManager.cs:1227-1265`, appelée
  pour les sept boîtes `:914-920`) : il ajoute **une** copie à la table d'ordre, à l'adresse du tampon
  courant `g_drawModes[0x14].tag` (`:1237`, `:1247`). Le pointeur vers la copie choisie est perdu dans la
  transcription, mais c'est l'usage PsyQ du double tampon : une copie par tampon d'affichage, une seule
  montrée par image. **Le portage de la décompilation ne lit que `SpritesA`** (`:1254`). Semi-transparence
  coupée et pas de modulation : un texel d'alpha 0 laisse voir la scène, tout autre texel s'affiche tel
  quel ; `wind.png` n'ayant que les alphas 0 et 255, **une image cuite reproduit exactement une copie**.
- **[mesuré] A contre B** (script et sortie au §7) : identiques case pour case pour les objets, le nom
  d'objet, argent/faucons/clés et la description ; le B du nom d'arme n'a pas de données propres dans la
  décompilation, c'est un `Clone()` de A (`StaticVariables.cs:11206`) ; **la boîte des armes diffère sur 26
  cases sur 126**. Dessinée seule, **A donne un cadre complet** ; B seule perd la bordure biseautée de
  droite et le bas du bord gauche, et la superposition de A puis B, celle de la maquette montrée à
  l'auteur, a le même défaut. D'où D-E13D-14.
- **Derrière l'inventaire** (relevé du 2026-09-21, non revérifié en session principale) : la scène reste
  dessinée, figée, sans effacement ni assombrissement (`MainInventoryManager.cs:779-923`) ; les parties
  transparentes des boîtes, les bords roulés du parchemin, la laissent voir.
- **Icônes** : même chaîne que le HUD (E13.c), déjà portée. **Curseur** : un sprite qui rebondit sur 4
  phases de 10 images, pris dans sa propre table (`g_inventoryCursorTextureUVs`,
  `MainInventoryManager.cs:108-138`), pas dans celles des boîtes. **Cadre de sélection** sur l'arme et sur
  l'objet équipés (`u0 = 48`, `v0 = 0x98` dans l'atlas d'interface). **Aucune de ces sources n'est encore
  rapprochée d'un actif exporté** : D0.9 le fait, pour elles, les chiffres et le portrait.
- **Textes** : le nom équipé (`DisplayIconNames`, `:1750-1793`) ; nom puis deux lignes de description de
  la case survolée, **déroulés un caractère toutes les 3 images** (`:929-1154`, états 0 à `0xcf`). Source :
  `EtcRes` (`IndexTable[id + 0x200 / 0x280 / 0x300]`), **déjà exportée** en `Dialogues/etc-index.json` et
  `Dialogues/global-strings.json` ; police `UI/font3.fnt` déjà exportée. **Si la DLL sait déjà les lire,
  on l'ignore** : D0 le mesure.
- **Chiffres** : argent (4), clés (2, nombre de l'objet 61), faucons (2) — **le faucon n'est pas porté**
  (`AlundraPlayerStats.cs`).
- **Portrait** : à l'ouverture, un quad du portrait d'Alundra grandit puis se résorbe (`:209-440`) ; ce
  n'est pas une capture de la scène.

### 1.5 Le HUD autour de l'inventaire

À l'ouverture, `InitializeHudPosition` fait **glisser la jauge vers sa place** si elle est cachée
(`HudManager.cs:26-38`) ; à la fermeture, `InitializeHudPositionBeforeHide` la fait **ressortir** selon
le verrou persistant et l'état du HUD (`:42-56`). Côté portage, `AlundraHudDirector` n'offre que deux
drapeaux, 1813 (ouverture animée) et 1814 (fermeture **instantanée**) : la correspondance avec les
conditions de l'original n'est **pas vérifiée**, D0 la mesure.

### 1.6 Côté portage : ce qui existe

- **Manette** : 9 boutons seulement (`AlundraPlayerController.cs:23-31`) ; `Start` existe déjà (action
  « Menu », Échap / Start). **`L1`, `L2`, `R1`, `R2`, `Select` n'existent nulle part** : ni bit, ni action,
  ni touche dans `Data/Alundra.buttonsMapping`, que le convertisseur écrit (`PlayerSetupWriter.cs:116-137`).
  Touches clavier déjà prises : flèches, Espace, X, C, Maj gauche, Échap.
- **Écran modal** : le dialogue est le précédent — `AlundraDialoguePresenter` pousse un `DialogueScreen`
  XAML, et le monde est gelé par un bit de `PlayerControlFlags`, pas par la pile d'écrans. `UILayer.Menu`
  est documenté pour « menu pause, inventaire, carte » ; `IsModal` doit être levé à part.
- **Règle** : un écran se déclare en XAML (ADR-0035), se teste avec un `MGDesktop` headless, jamais un
  `UIRoot`.

---

## 2. Décisions

### 2.1 Verrouillées (plan maître et auteur)

| Réf | Décision |
|---|---|
| D-E13D-1 | **Porter l'original tel quel**, fonctions citées ligne à ligne, comme E13.c — **pour la logique**. Amendée par l'auteur le 2026-09-21 pour le dessin : « on ne va pas respecter au pixel près le jeu original » (§2.2, point 1). |
| D-E13D-2 | **L'inventaire principal d'abord** ; le sous-inventaire et L1/R1 dans un second plan. |
| D-E13D-3 | **L'écran se déclare en XAML** (ADR-0035) ; le code retrouve par nom et pousse des valeurs. |
| D-E13D-4 | **Aucun contournement** : un manque de MGUI ou du moteur se consigne et arrête la tranche. |

### 2.2 Tranchées par l'auteur le 2026-09-21

| Réf | Décision de l'auteur | Conséquence |
|---|---|---|
| D-E13D-10 | « **Les icônes doivent être centrées dans la boîte parente. On ne va pas respecter au pixel près le jeu original.** » | Une icône se centre dans sa case par alignement, au lieu d'être calée à la position de l'original ; **l'auteur l'étend aux deux cases du HUD** (§6 point 2, appliqué à S3 par `cdb7097`). Le patron case par case des boîtes n'est plus une obligation ; la façon de dessiner les boîtes est tranchée par D-E13D-13. |
| D-E13D-11 | **Touches** : `Y` = `L2`, `U` = `L1`, `I` = `R1`, `O` = `R2`, `P` = `Select` ; à la manette, `LeftShoulder` = `L1`, `LeftTrigger` = `L2`, `RightShoulder` = `R1`, `RightTrigger` = `R2`, `Back` = `Select`. | D1 lie ces touches. |
| D-E13D-12 | **Le portrait d'ouverture dès la première passe.** | D5 le porte. |
| D-E13D-13 | « **Comment dessiner les boîtes : 1 avec image cuite.** » Le fond de chaque boîte a l'aspect de l'original, **cuit une fois par le convertisseur en une image par boîte**, après que l'auteur a vu la maquette des trois façons (§6 point 1). | D3 devient D3.a (le patron en CSV dans l'analyseur) et D3.b (la cuisson et six sprites dans le convertisseur) ; D5 affiche une image par boîte ; D0.1 et D0.2 sont closes (§1.4). La maquette superposait les copies A et B ; l'image cuite n'en prend qu'une (D-E13D-14). |

### 2.3 Proposées, sauf avis contraire

| Réf | Décision proposée | Pourquoi |
|---|---|---|
| D-E13D-5 | `Falcon` et `FalconTemp` portés sur `AlundraPlayerStats` **à 0**, la valeur de la nouvelle partie, et affichés ; aucune mécanique de faucon | fidèle à l'état réel du jeu à ce stade, sans inventer de système |
| D-E13D-6 | Un **directeur** d'inventaire pur (machine à états au tick, sans MGUI), un **présentateur** qui pousse vers une vue, un **écran XAML** — le découpage du dialogue et du HUD | testable sans tête, déjà prouvé deux fois dans ce dépôt |
| D-E13D-7 | L'écran en `UILayer.Menu`, `IsModal = true` | la couche que le moteur destine à l'inventaire, et le blocage des écrans inférieurs |
| D-E13D-8 | **La répétition des touches est portée**, pas contournée : `ButtonsJustPressedByInterval` calculé comme `PadManager.UpdatePad` | la navigation de l'original en dépend ; l'opcode qui le lit en profite aussi |
| D-E13D-14 | **Cuire la copie A seule** de chaque boîte, sans la superposer à B | le portage de la décompilation ne lit que A (`MainInventoryManager.cs:1254`) ; A et B sont identiques pour cinq boîtes sur six ; pour la boîte des armes, seule A donne un cadre complet (§1.4). Écart visible avec la maquette montrée à l'auteur : la bordure droite de la boîte des armes, que la superposition abîmait |
| D-E13D-9 | **Fronts et répétition comptés au tick logique**, l'équivalent de l'image à 50 Hz de l'original : l'état des boutons reste échantillonné par image rendue, mais l'inventaire lit des fronts et une répétition mis à jour une fois par tick | un appui donne exactement un front, et 20 ticks valent 20 images de l'original, quelle que soit la cadence d'affichage |

---

## 3. Tranches

Un commit par tranche, un vérificateur frais par tranche à risque, régime de preuve par double export dès
que le convertisseur est touché. **Chaque tranche ne commence que lorsque ses prérequis sont clos.**

### ⏳ D0 — Les mesures (lecture seule)

**Prérequis** : aucun. **Livrable commun** : chaque résultat est reporté au §1 de ce plan, marqué
**[mesuré]**, avec sa citation ; les scripts de mesure sont recopiés en entier dans le journal (§7), avec
leur sortie, pour que la mesure soit refaisable.

| # | Mesure | Livrable | Close quand |
|---|---|---|---|
| D0.1 | ✅ *close le 2026-09-21, [mesuré] au §1.4, script au §7* — **Rapprochement des cases avec les `wind_NNN`** : un script relit tous les `new SPRT{…}` des tableaux `SpritesA`/`SpritesB` des sept boîtes et cherche chaque tuple `(u0, v0, w, h, clut)` **à l'identique** dans `alundra-project/UI/wind-sprites.json` `(u0, v0, width, height, palette_index)` ; la règle d'égalité, et toute table de passage entre `clut` et `palette_index` si elle existe, sont écrites avant de lancer le script | nombre de cases par boîte, nombre de tuples introuvables, et chaque tuple introuvable listé | le script a tourné sur les sept boîtes et son résultat est au §1.4 |
| D0.2 | ✅ *close le 2026-09-21, [lu] et [mesuré] au §1.4 : deux copies, pas deux couches ; comparaison A/B au §7* — **Le mode de mélange** des couches A et B (bits de `code`/`tag` des `SPRT`, et ce que `Renderer.AddSprite` en fait) | le mode par couche, cité | chaque couche des sept boîtes a son mode |
| D0.3 | **Ce qui tourne hors de la porte T2** pendant `MenuOpen` : vitesse et gravité du contrôleur de personnage côté moteur, lecture d'animation de sprite côté moteur ; et le cas d'une ouverture en plein saut | pour chacun : figé ou non, cité dans le code du moteur | la liste est complète et citée ; elle décide si une tranche de gel existe (voir D2) |
| D0.4 | **La répétition des touches** : où l'original fixe `RepeatInterval` et sa valeur ; l'ordre de mise à jour dans la boucle | la cadence exacte | la valeur est citée |
| D0.5 | **Le HUD** : correspondance entre `InitializeHudPosition`/`InitializeHudPositionBeforeHide` et leurs conditions, et les drapeaux 1813/1814 du directeur du portage | une table condition par condition | chaque condition a son équivalent, ou est déclarée manquante |
| D0.6 | **`StartFadeOut`** et **`g_forbiddenWarpFlag`** : effet réel, sens des valeurs 0, 2, 5 et du test `& 6` | le sens de chaque bit, cité | chaque valeur a un sens établi par le code, ou est déclarée inconnue |
| D0.7 | **Ce que la DLL sait déjà faire** : lire `etc-index.json`/`global-strings.json` (un équivalent d'`EtcRes`), jouer les sons 1 à 5 de l'interface | présent ou absent, cité | les deux questions ont une réponse |
| D0.8 | **Les objets 92 à 97** (nom sans propriétés ni icône) : peuvent-ils atteindre une case de la grille ? | oui ou non, par la table et la résolution par case | la réponse est citée |
| D0.9 | **Les graphismes hors des boîtes** : curseur (`g_inventoryCursorTextureUVs`), cadres de sélection, chiffres (`g_numbersSpriteSheetUVs`), portrait d'ouverture — leur source dans l'original, et l'actif exporté qui porte chacun | pour chaque graphisme : source citée et actif exporté, ou « absent → extraction » | chaque graphisme dessiné par D5 a sa ligne |
| D0.10 | **Les horloges du portage** : où l'état des boutons est échantillonné, où chaque consommateur (héros, dialogue, opcode `0x2F`) le lit, sur quelle horloge ; et ce qu'un tick nul ou double fait à un front d'appui aujourd'hui | la table consommateur par consommateur, citée | la table est complète ; elle fixe où D1 met à jour fronts et répétition |

**Arrêt propre à D0** : si une mesure contredit un fait du §1 ou une recommandation du §6, le plan est
révisé et **resoumis à l'auteur avant** que D1, D2 ou D3 ne commence.

### ⏳ D1 — Manette : cinq boutons et la répétition

**Prérequis** : D0.4, D0.10 ; touches fixées par D-E13D-11.
Cinq bits sur `AlundraPadState` (`L1`, `L2`, `R1`, `R2`, `Select`), cinq lignes d'`ActionBits`, cinq
liaisons dans `PlayerSetupWriter` (clavier `U`, `Y`, `I`, `O`, `P` et manette `LeftShoulder`,
`LeftTrigger`, `RightShoulder`, `RightTrigger`, `Back`, pour `L1`, `L2`, `R1`, `R2`, `Select`, D-E13D-11) ; et `ButtonsJustPressedByInterval` porté ligne à ligne depuis
`PadManager.UpdatePad` (D-E13D-8), **mis à jour une fois par tick logique** (D-E13D-9). Tests, à cadence
de rendu variable, avec des images à zéro tick et à deux ticks : un appui donne exactement un front ;
une touche maintenue répète après 20 ticks, puis tous les `RepeatInterval` ticks ; un changement de
l'état des boutons remet à zéro. Le convertisseur change :
**régime de preuve complet**, diff prédit = `Data/Alundra.buttonsMapping` + `report.json`.

### ⏳ D2 — Le gel, pour ce que la porte T2 ne couvre pas (conditionnelle)

**Prérequis** : D0.3. **N'existe que si D0.3 trouve un mécanisme du moteur qui continue de tourner hors
de la porte** (par exemple la gravité du contrôleur pendant une ouverture en plein saut). Sinon elle est
retirée du plan, avec la mesure qui le justifie.

### ⏳ D3.a — Analyseur : le patron des boîtes en CSV

**Prérequis** : D-E13D-13, D-E13D-14 ; D0.1 et D0.2 (closes).
**Branche** : dans l'analyseur, empilée sur `chantier/e13c-drop-properties` comme le parent l'est sur
`chantier/e13c-suite`, pour ne pas croiser la ligne de `ItemDropProperties.csv` dans `AlundraTools.csproj`.
Deux fichiers dans `AlundraTools/AlundraTools/`, liés au projet comme `ItemsProperties.csv`, générés depuis
les tableaux décompilés et **bruts**, sans interprétation (précédent S1.b et S1.c) :
- `UiBoxes.csv`, `box;x;y;width;height` : les **sept** boîtes de `StaticVariables.cs:11196-11267`, `box`
  étant le nom de la variable de la décompilation, l'espaceur 0×0 compris ;
- `UiBoxCells.csv`, `box;cell;x0;y0;u0;v0;w;h;clut` : les cases de la copie **A** (`SpritesA`,
  D-E13D-14), **822 lignes**, `cell` l'indice dans le tableau. La copie B n'est pas exportée ; sa
  comparaison avec A reste au §7.

**Acceptation** : un script indépendant relit `StaticVariables.cs` et retrouve chaque ligne des deux CSV,
et rien de plus ; 7 boîtes, 822 cases.

### ⏳ D3.b — Convertisseur : une image cuite par boîte

**Prérequis** : D3.a.
- **Lecture** : `UiBoxLayoutReader` lit les deux CSV, liés dans le `.csproj` du convertisseur comme ceux
  de S2 (précédent `ItemsPropertiesCatalogReader`, avertissements par ligne mal formée).
- **Cuisson** : une nouvelle phase, `Phase7.UiBoxes`, juste après `Phase7.Ui`, lit les mêmes entrées
  qu'elle, `data-extracted/ui/wind.png` et `wind.json`. Pour chaque boîte de taille non nulle, une image
  RGBA transparente de `8 × width` sur `8 × height` ; chaque case de la copie A est recopiée **telle
  quelle, alpha compris**, de l'atlas depuis `(u0, v0)` vers `(x0 − X, y0 − Y)` : les cases ne se
  recouvrant pas (§1.4), il n'y a rien à mélanger, et l'alpha 0 de l'atlas reste la transparence qui
  laisse voir la scène (D0.2). Une case qui sortirait du rectangle de sa boîte, ou en recouvrirait une
  autre, est une **erreur** du rapport, et sa boîte n'est pas émise. Une case dont le tuple
  `(u0, v0, w, h, clut)` n'a pas d'entrée `(U0, V0, Width, Height, PaletteIndex)` égale dans `wind.json`
  est une **erreur** du rapport, et sa boîte n'est pas émise. Composition en `System.Drawing`, comme
  `BackdropImageBuilder`.
- **Émission** : pour chaque boîte, `UI/Textures/<box>.png` et son `.texture` par
  `TextureAssetWriter.EnsureTexture`, puis `UI/<box>.sprite` couvrant l'image entière, identifiant
  `Ids.For("sprite-ui:" + box)`. **Piège** : `UiWriter` sauve déjà le catalogue (`UiWriter.cs:60`) ; la
  nouvelle phase le sauve à son tour, comme `BackdropWriter` (`:64-68`).
- **Compteurs** : `UiBoxes.Boxes` = 6, `UiBoxes.Cells` = 822, `UiBoxes.CellsWithoutTile` = 0.
- **Tests** : le lecteur sur des CSV synthétiques ; la cuisson sur un atlas synthétique (décalage
  `x0 − X`, alpha 0 et 255 recopiés tels quels, boîte 0×0 ignorée, case sans tuile en erreur, case hors
  de sa boîte ou recouvrant une autre en erreur).

**Régime de preuve complet** : manifeste de référence avant toute modification ; **diff prédit** =
6 × (`.png`, `.texture`, `.sprite`) nouveaux + `AssetInfos.json` + `report.json` ; export complet en place ;
mesuré ⊆ prédit ; double export ⊆ `{report.json}`. **Preuve au pixel** : chaque image cuite est égale,
pixel pour pixel, à une composition indépendante faite par script depuis les `SpritesA` de
`StaticVariables.cs` et `wind.png`, sans passer par les CSV ni par le code du convertisseur — ce qui
prouve aussi D3.a. Et **la boîte des armes cuite montre sa bordure droite** : le choix de A (D-E13D-14) se
voit dans l'image, pas seulement dans le code.

### ⏳ D4 — DLL : le directeur de l'inventaire

**Prérequis** : D0.5, D0.6, D0.7, D0.8, D0.10, D1 ; D2 si elle existe.
Le directeur lit les fronts et la répétition de D1, au tick, jamais l'instantané par image rendue.
Machine à états au tick, portée ligne à ligne : déclencheur et ses gardes, glissement des sept boîtes en
15 ticks, `MenuOpen` et son retrait en fin de glissement, curseur et bouclages sur la répétition des
touches, équipement d'arme et d'objet par les fonctions d'E13.c, texte déroulant, sons 1 à 5, drapeaux du
HUD, `Falcon`/`FalconTemp` à 0 (D-E13D-5). Corrige au passage le commentaire périmé de
`AlundraPlayerManager.cs:555-558`. Aucune dépendance MGUI.

### ⏳ D5 — DLL : l'écran XAML, le présentateur, la capture

**Prérequis** : D0.9, D3.b, D4.
Écran XAML (**six images de boîte**, les sprites de D3.b, chacune à son `(X, Y)` natif et glissant pour son
compte, grille d'icônes **centrées dans leur case** (D-E13D-10), curseur,
cadres de sélection, textes en `font3`, chiffres, **portrait d'ouverture** (D-E13D-12)), présentateur
branché comme celui du dialogue. Le XAML ne sait pas désigner un actif par identifiant : il nomme les
images, le code leur donne leur source, avec les identifiants des six sprites en constantes, comme les 24
glyphes du HUD (`AlundraHudScreen.cs:447-478`). Tests headless sur `MGDesktop` ; capture en
processus de l'inventaire ouvert, **prédite avant d'être prise**.

### ⏳ D6 — Recette en jeu (l'auteur)

**Prérequis** : D5.
Ouvrir par `Start`, `L2` ou `R2` ; naviguer, y compris en maintenant une direction ; équiper une arme et
un objet ; lire le texte déroulant ; fermer ; le héros et le monde sont figés pendant ; le HUD se comporte
comme dans l'original.

---

## 4. Acceptation d'ensemble

E13.d (principal) est close quand, en jeu : l'inventaire s'ouvre et se ferme comme l'original, avec ses
glissements, ses sons et le HUD ; on navigue sur les 24 cases, avec la répétition en maintenant une
direction ; on équipe une arme et un objet, et la jauge reflète l'arme ; le monde et le héros sont figés
tant qu'il est ouvert, y compris après une ouverture en plein saut ; les boîtes ont l'aspect de
l'original, chacune une image cuite (D-E13D-13) ; suites vertes ; chaque export prouvé par double export.

## 5. Arrêts

- Une mesure de D0 qui contredit le §1 ou le §6 : plan révisé et resoumis avant D1-D3.
- Un manque de MGUI ou du moteur :
  consigné dans `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`, et la tranche s'arrête.
- Un diff d'export hors du prédit, un double export hors de `{report.json}`.
- Un graphisme de l'inventaire sans actif exporté — une case de boîte dont le tuple n'a pas de
  `wind_NNN`, le curseur, un cadre, un chiffre ou le portrait : l'hypothèse « pixels déjà exportés »
  tombe, et il faut une extraction.

## 6. Points ouverts

1. *Tranché par l'auteur le 2026-09-21 : la façon (a), cuite par le convertisseur, D-E13D-13.* Le fond de chaque boîte
   de l'inventaire (pierre pour les armes et les objets, parchemin pour les noms et la description) n'est
   pas une image : l'original le pose case par case, 822 tuiles de 8×8 prises dans l'atlas `wind` (la
   maquette superposait par erreur les deux copies A et B, 1644 tuiles, voir D-E13D-14). Une maquette, composée le 2026-09-21 avec les tuiles exportées et les positions
   décompilées (aucune case sans tuile, aucune position à palette ambiguë), montre trois façons de faire :
   (a) **l'original** : soit la DLL pose les tuiles une à une d'après un patron exporté, soit le
   convertisseur cuit une image par boîte et l'écran en affiche une seule — même rendu, pierre et
   parchemin variés ; (b) **un cadre à neuf tranches** (quatre coins, quatre bords, une tuile de fond
   répétée) : la pierre devient un motif répétitif et le parchemin des rayures, parce que ces fonds ne sont
   pas réguliers (59 et 81 tuples distincts, mesure antérieure) ; (c) **des panneaux MGUI simples** (fond
   uni et bordure) : rien à exporter, mais l'aspect d'Alundra est perdu.
2. *Le centrage vaut aussi pour le HUD : tranché par l'auteur le 2026-09-21 (« oui ») et appliqué à S3
   par `cdb7097` ; voir `docs/plan-e13c-icones-hud.md`, amendement de S3.*
3. *Touches et portrait : tranchés, D-E13D-11 et D-E13D-12.*
4. **Défaut latent du dialogue, consigné et non corrigé** : `AlundraDialogueDirector` tourne au tick
   mais lit le front d'appui de l'instantané par image rendue (`AlundraDialogueDirector.cs:239`,
   `AlundraWorldProxy.cs:1940-1943`) : sur une image à deux ticks il le voit deux fois, sur une image à
   zéro tick jamais. D0.10 le mesure ; le corriger n'est pas l'objet de ce plan.
5. **Deux remarques P4 d'E13.c S3, reportées** : `InitializeNewGameInventory` ne remet pas `ItemId` à 0
   (sans effet dans une vraie session) ; la citation de la remise à zéro des compteurs dit `:473-479` pour
   `:469-479`.

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-21 | Reconnaissance à cinq surfaces (inventaire principal, sous-inventaire et bascule, déclencheur et gel, côté portage, ressources exportées) et une critique de complétude. Faits repris en session principale : la grille de 24 cases et sa table ; les touches déjà prises ; **les boîtes ne sont pas des cadres réguliers** (59 et 81 tuples distincts). |
| 2026-09-21 | Première relecture adverse : **REVISE**, un P1 et trois P2, tous acceptés. (1) **P1, une erreur de la première rédaction** : elle affirmait, marqué « mesuré », que lever `MenuOpen` ne figeait pas le héros. C'est faux : la porte d'`UpdateEntities` est portée depuis T2, et `MovePlayer` n'est appelé que hors d'elle. La mesure avait lu l'appel sans remonter à la fonction qui le contient, et un commentaire périmé l'y avait poussée. La question du gel posée à l'auteur est retirée ; D2 devient conditionnelle, sur la mesure de ce que la porte ne couvre pas (D0.3). (2) La navigation lit la répétition des touches, que le portage ne calcule pas : ajoutée à D1 (D-E13D-8). (3) D0 reçoit un livrable et un critère de clôture par mesure, la règle d'égalité du rapprochement, et son propre arrêt. (4) Chaque tranche reçoit ses prérequis. |
| 2026-09-21 | Relecture de clôture : **REVISE**, deux P2, tous deux acceptés en **FIX**. Les quatre blocages précédents y sont confirmés résolus. (1) **L'horloge de la manette** : l'original compte à 50 Hz, le portage échantillonne par image rendue et fait tourner sa logique au tick ; une répétition portée telle quelle dépendrait de l'affichage. Décision D-E13D-9, mesure D0.10, tests de D1 à cadence variable, et un défaut latent du dialogue consigné sans être corrigé. (2) **Les graphismes hors des boîtes** (curseur, cadres, chiffres, portrait) n'avaient pas de source établie : mesure D0.9, prérequis de D5, arrêt élargi à tout graphisme. **Deuxième REVISE : plafond atteint.** Disposition en session principale, une seule relecture de clôture ensuite, sans nouvelle boucle. |
| 2026-09-21 | Relecture de clôture, la seule autorisée après le plafond : **READY**. Le plan part à l'auteur avec trois questions (§6, points 1 à 3). |
| 2026-09-21 | **Décisions de l'auteur.** Icônes centrées dans leur boîte, et pas de fidélité au pixel près pour le dessin (D-E13D-10, qui amende D-E13D-1 pour le dessin seulement) ; touches `Y`/`U`/`I`/`O`/`P` pour `L2`/`L1`/`R1`/`R2`/`Select` (D-E13D-11) ; portrait dès la première passe (D-E13D-12). Deux questions restent ouvertes, que l'auteur n'a pas tranchées : la façon de dessiner les boîtes, et le centrage dans le HUD de S3. |
| 2026-09-21 | **L'auteur tranche le §6 point 2 : le centrage vaut aussi pour les deux cases du HUD**, appliqué à S3 par `cdb7097` sur `chantier/e13c-suite`, puis cette branche réempilée dessus. Le point 1 reste ouvert : l'auteur n'a pas compris la question, reformulée avec une maquette des trois façons de dessiner les boîtes. |
| 2026-09-21 | **L'auteur tranche le §6 point 1 : « 1 avec image cuite » (D-E13D-13).** Reconnaissance en lecture seule à trois surfaces (analyseur, convertisseur, DLL et moteur), puis faits porteurs de décision revérifiés en session principale. Un relevé disait `clut` 3 partout : faux, la mesure trouve 0, 3, 5 et 6 ; un autre ne voyait pas de mode de mélange dans le code : les appels de l'original sont en commentaire, `SetSemiTrans(sprite, 0)`. D0.1 close par mesure (1644 cases sur 1644 retrouvées), D0.2 par lecture ; D3 réécrite en D3.a et D3.b ; D5 ajustée. Relecture à venir. |
| 2026-09-21 | Relecture de la révision : **REVISE**, un blocage, accepté en **FIX**. La révision affirmait, marqué [lu], que la couche B se pose sur la couche A. Faux : `FUN_800548a4` n'appelle dans l'original que des macros d'initialisation ; A et B sont les deux copies d'un double tampon, et l'original n'en montre qu'une par image (`DisplayUiBoxes`, `0x80055d78`). Mesure ajoutée (script et sortie ci-dessous) : A et B égales pour cinq boîtes, le B du nom d'arme est un clone de la décompilation, et **la boîte des armes diffère sur 26 cases** ; dessinées séparément, A donne un cadre complet et B non, et la maquette montrée à l'auteur superposait les deux, avec ce défaut. D-E13D-14 : cuire A seule ; D3.a exporte 822 cases, D3.b recopie sans mélange, compteurs et tests ajustés. |
| 2026-09-21 | Relecture neuve après la correction : **READY**. La révision part à l'auteur : D-E13D-13 enregistrée, D-E13D-14 proposée, D0.1 et D0.2 closes, D3.a et D3.b prêtes à l'approbation avec D0. |

### D0.1 — le script de mesure et sa sortie (2026-09-21)

Refaisable tel quel avec Python 3 et Pillow :

```python
"""D0.1 of docs/plan-e13d-inventaire.md: every cell of the inventory boxes against the exported wind atlas.

Rule, written before the run: a cell (u0, v0, w, h, clut) of a SpritesA/SpritesB array is FOUND when
alundra-project/UI/wind-sprites.json has an entry with (u0, v0, width, height, palette_index) equal to it.
Also measured: every cell lies inside its box's rectangle, the cells tile the rectangle in the drawing
loop's own order (index = row * Width + col, GraphicManager.cs:1855-1856), and the alpha values of wind.png.
"""
import io, json, os, re
from collections import Counter
from PIL import Image

ROOT = r'D:\development\repo\alundra-casaengine-project-converter'
src = io.open(os.path.join(ROOT, r'alundra-datas-analyser\AlundraTools\AlundraEngine\StaticVariables.cs'),
              encoding='utf-8-sig').read()
wind = json.load(io.open(os.path.join(ROOT, r'alundra-project\UI\wind-sprites.json'), encoding='utf-8-sig'))
atlas = Image.open(os.path.join(ROOT, r'alundra-project\UI\Textures\wind.png')).convert('RGBA')

BOXES = [  # StaticVariables.cs:11196-11267 - name, X, Y, Width, Height (cells), SpritesA, SpritesB
    ('weapons', 0x08, 0x10, 0x15, 0x06, 'g_MainInventoryWeaponBackgroundSpritesA', 'g_MainInventoryWeaponBackgroundSpritesB'),
    ('items', 0x08, 0x40, 0x15, 0x0D, 'g_MainInventoryItemBackgroundSpritesA', 'g_MainInventoryItemBackgroundSpritesB'),
    ('weapon-name', 0xB0, 0x10, 0x12, 0x04, 'SPRT_ARRAY_800b8370', 'SPRT_ARRAY_800b8370'),  # :11206, B = A.Clone()
    ('item-name', 0xB0, 0x40, 0x12, 0x04, 'SPRT_ARRAY_800b8ec0', 'SPRT_ARRAY_800b9460'),
    ('money-falcon-key', 0xB0, 0x60, 0x03, 0x09, 'g_moneyFalconKeyIconSpritesA', 'g_moneyFalconKeyIconSpritesB'),
    ('description', 0x10, 0xA8, 0x24, 0x07, 'g_dialogMessageBackgroundSpritesA', 'g_dialogMessageBackgroundSpritesB'),
]  # the seventh box, UIBoxConfiguration_800b9a10, is 0x0 cells: nothing to draw.

SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def cells(array_name):
    start = src.index('[', src.index(array_name + ' ='))
    depth, end = 0, start
    for end in range(start, len(src)):
        depth += {'[': 1, ']': -1}.get(src[end], 0)
        if depth == 0:
            break
    return [{k: int(v, 0) for k, v in FIELD.findall(m.group(1))} for m in SPRT.finditer(src[start:end])]


known = {(e['u0'], e['v0'], e['width'], e['height'], e['palette_index']) for e in wind}
total = found = outside = misplaced = 0
for name, bx, by, bw, bh, arr_a, arr_b in BOXES:
    per_layer, cluts = [], Counter()
    for layer in (cells(arr_a), cells(arr_b)):
        per_layer.append(len(layer))
        for i, c in enumerate(layer):
            total += 1
            found += (c['u0'], c['v0'], c['w'], c['h'], c['clut']) in known
            cluts[c['clut']] += 1
            outside += not (bx <= c['x0'] and c['x0'] + c['w'] <= bx + 8 * bw and by <= c['y0'] and c['y0'] + c['h'] <= by + 8 * bh)
            misplaced += (c['x0'], c['y0'], c['w'], c['h']) != (bx + 8 * (i % bw), by + 8 * (i // bw), 8, 8)
    print(f'{name}: {bw}x{bh} cells, A={per_layer[0]} B={per_layer[1]}, clut {dict(cluts)}')
print(f'cells: {total}; found in wind-sprites.json: {found}; outside their box: {outside}; '
      f'not at X + 8*col, Y + 8*row: {misplaced}')
print(f'wind.png alpha values: {sorted(Counter(p[3] for p in atlas.getdata()).items())}')
```

Sortie :

```text
weapons: 21x6 cells, A=126 B=126, clut {3: 252}
items: 21x13 cells, A=273 B=273, clut {3: 546}
weapon-name: 18x4 cells, A=72 B=72, clut {0: 144}
item-name: 18x4 cells, A=72 B=72, clut {0: 144}
money-falcon-key: 3x9 cells, A=27 B=27, clut {6: 36, 5: 18}
description: 36x7 cells, A=252 B=252, clut {0: 504}
cells: 1644; found in wind-sprites.json: 1644; outside their box: 0; not at X + 8*col, Y + 8*row: 0
wind.png alpha values: [(0, 48907), (255, 16629)]
```

### D0.2 — la comparaison des copies A et B et sa sortie (2026-09-21)

Refaisable tel quel avec Python 3 :

```python
"""D0.2 of docs/plan-e13d-inventaire.md: are SpritesA and SpritesB of each inventory box the same cells?

Rule, written before the run: for each box, cell i of SpritesA and cell i of SpritesB are EQUAL when their
full tuples (x0, y0, u0, v0, w, h, clut) are equal. Every differing cell is listed with both tuples.
The weapon name's SpritesB, SPRT_ARRAY_800b8910, has no data of its own in the decompilation: it is a
Clone() of SpritesA at initialisation (StaticVariables.cs:11206), so its equality is a transcription fact,
not a fact of the original; it is reported apart.
"""
import io, os, re

ROOT = r'D:\development\repo\alundra-casaengine-project-converter'
src = io.open(os.path.join(ROOT, r'alundra-datas-analyser\AlundraTools\AlundraEngine\StaticVariables.cs'),
              encoding='utf-8-sig').read()

BOXES = [  # StaticVariables.cs:11196-11267 - name, SpritesA, SpritesB
    ('weapons', 'g_MainInventoryWeaponBackgroundSpritesA', 'g_MainInventoryWeaponBackgroundSpritesB'),
    ('items', 'g_MainInventoryItemBackgroundSpritesA', 'g_MainInventoryItemBackgroundSpritesB'),
    ('weapon-name', 'SPRT_ARRAY_800b8370', None),  # B = A.Clone(), :11206
    ('item-name', 'SPRT_ARRAY_800b8ec0', 'SPRT_ARRAY_800b9460'),
    ('money-falcon-key', 'g_moneyFalconKeyIconSpritesA', 'g_moneyFalconKeyIconSpritesB'),
    ('description', 'g_dialogMessageBackgroundSpritesA', 'g_dialogMessageBackgroundSpritesB'),
]
KEYS = ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')
SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def cells(array_name):
    start = src.index('[', src.index(array_name + ' ='))
    depth, end = 0, start
    for end in range(start, len(src)):
        depth += {'[': 1, ']': -1}.get(src[end], 0)
        if depth == 0:
            break
    out = []
    for m in SPRT.finditer(src[start:end]):
        v = dict(FIELD.findall(m.group(1)))
        out.append(tuple(int(v[k], 0) for k in KEYS))
    return out


for name, arr_a, arr_b in BOXES:
    a = cells(arr_a)
    if arr_b is None:
        print(f'{name}: {len(a)} cells; SpritesB is a Clone() of SpritesA in the decompilation (no original data)')
        continue
    b = cells(arr_b)
    diff = [(i, a[i], b[i]) for i in range(min(len(a), len(b))) if a[i] != b[i]]
    print(f'{name}: A={len(a)} B={len(b)} cells; equal {min(len(a), len(b)) - len(diff)}; different {len(diff)}')
    for i, ta, tb in diff[:12]:
        print(f'    cell {i}: A {dict(zip(KEYS, ta))}  B {dict(zip(KEYS, tb))}')
    if len(diff) > 12:
        print(f'    ... {len(diff) - 12} more')
```

Sortie :

```text
weapons: A=126 B=126 cells; equal 100; different 26
    cell 19: A {'x0': 160, 'y0': 16, 'u0': 160, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 160, 'y0': 16, 'u0': 200, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}
    cell 20: A {'x0': 168, 'y0': 16, 'u0': 168, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 16, 'u0': 208, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}
    cell 41: A {'x0': 168, 'y0': 24, 'u0': 168, 'v0': 80, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 24, 'u0': 208, 'v0': 80, 'w': 8, 'h': 8, 'clut': 3}
    cell 42: A {'x0': 8, 'y0': 32, 'u0': 176, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 8, 'y0': 32, 'u0': 176, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}
    cell 62: A {'x0': 168, 'y0': 32, 'u0': 248, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 32, 'u0': 208, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}
    cell 83: A {'x0': 168, 'y0': 40, 'u0': 248, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 40, 'u0': 208, 'v0': 96, 'w': 8, 'h': 8, 'clut': 3}
    cell 84: A {'x0': 8, 'y0': 48, 'u0': 176, 'v0': 120, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 8, 'y0': 48, 'u0': 176, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}
    cell 104: A {'x0': 168, 'y0': 48, 'u0': 248, 'v0': 120, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 48, 'u0': 208, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}
    cell 106: A {'x0': 16, 'y0': 56, 'u0': 216, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 16, 'y0': 56, 'u0': 184, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 107: A {'x0': 24, 'y0': 56, 'u0': 200, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 24, 'y0': 56, 'u0': 192, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 108: A {'x0': 32, 'y0': 56, 'u0': 208, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 32, 'y0': 56, 'u0': 200, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 109: A {'x0': 40, 'y0': 56, 'u0': 216, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 40, 'y0': 56, 'u0': 208, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    ... 14 more
items: A=273 B=273 cells; equal 273; different 0
weapon-name: 72 cells; SpritesB is a Clone() of SpritesA in the decompilation (no original data)
item-name: A=72 B=72 cells; equal 72; different 0
money-falcon-key: A=27 B=27 cells; equal 27; different 0
description: A=252 B=252 cells; equal 252; different 0
```
