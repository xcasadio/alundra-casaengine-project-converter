# Plan — E13.d : l'inventaire principal

**État** : ⏳ rédigé le 2026-09-21, **en attente d'approbation de l'auteur**. Aucune ligne de code écrite.
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
  un espaceur vide, argent/faucons/clés (3×9), plus la boîte de description. Chaque case dessine **deux
  couches**, `SpritesA` puis `SpritesB` (`GraphicManager.cs:1828-1879`).
- **[mesuré] Les boîtes ne sont pas des cadres réguliers** : le fond des armes compte **59 tuples
  distincts** `(u0, v0, w, h, clut)`, dont 32 à l'intérieur ; celui des objets **81**, dont 48 à
  l'intérieur ; argent/faucons/clés **21** sur 27 cases. Ce sont des images dessinées case par case, pas
  des cadres en 9 tranches : **le patron case par case doit voyager comme donnée**.
- **Les pixels seraient déjà exportés** : la reconnaissance affirme que les ~2700 cases de ces tableaux
  ont toutes leur tuple parmi les 277 `wind_NNN.sprite`, mais sans script ni journal. **D0 le reproduit**
  avant que quoi que ce soit en dépende. **Le patron** (quelle boîte, où, de quelle taille, quelle case
  dans quel ordre) n'existe que dans la décompilation.
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
| D-E13D-1 | **Porter l'original tel quel**, fonctions citées ligne à ligne, comme E13.c. |
| D-E13D-2 | **L'inventaire principal d'abord** ; le sous-inventaire et L1/R1 dans un second plan. |
| D-E13D-3 | **L'écran se déclare en XAML** (ADR-0035) ; le code retrouve par nom et pousse des valeurs. |
| D-E13D-4 | **Aucun contournement** : un manque de MGUI ou du moteur se consigne et arrête la tranche. |

### 2.2 À trancher par l'auteur — voir §6, points 1 à 3

1. **Le patron des boîtes** : export depuis l'analyseur puis composition dans la DLL (recommandé), cuisson
   d'une image par boîte dans le convertisseur, ou littéraux recopiés dans la DLL.
2. **Les touches** de `L1`, `L2`, `R1`, `R2`, `Select`.
3. **Le portrait d'ouverture** : dans la première passe (recommandé) ou reporté.

### 2.3 Proposées, sauf avis contraire

| Réf | Décision proposée | Pourquoi |
|---|---|---|
| D-E13D-5 | `Falcon` et `FalconTemp` portés sur `AlundraPlayerStats` **à 0**, la valeur de la nouvelle partie, et affichés ; aucune mécanique de faucon | fidèle à l'état réel du jeu à ce stade, sans inventer de système |
| D-E13D-6 | Un **directeur** d'inventaire pur (machine à états au tick, sans MGUI), un **présentateur** qui pousse vers une vue, un **écran XAML** — le découpage du dialogue et du HUD | testable sans tête, déjà prouvé deux fois dans ce dépôt |
| D-E13D-7 | L'écran en `UILayer.Menu`, `IsModal = true` | la couche que le moteur destine à l'inventaire, et le blocage des écrans inférieurs |
| D-E13D-8 | **La répétition des touches est portée**, pas contournée : `ButtonsJustPressedByInterval` calculé comme `PadManager.UpdatePad` | la navigation de l'original en dépend ; l'opcode qui le lit en profite aussi |
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
| D0.1 | **Rapprochement des cases avec les `wind_NNN`** : un script relit tous les `new SPRT{…}` des tableaux `SpritesA`/`SpritesB` des sept boîtes et cherche chaque tuple `(u0, v0, w, h, clut)` **à l'identique** dans `alundra-project/UI/wind-sprites.json` `(u0, v0, width, height, palette_index)` ; la règle d'égalité, et toute table de passage entre `clut` et `palette_index` si elle existe, sont écrites avant de lancer le script | nombre de cases par boîte, nombre de tuples introuvables, et chaque tuple introuvable listé | le script a tourné sur les sept boîtes et son résultat est au §1.4 |
| D0.2 | **Le mode de mélange** des couches A et B (bits de `code`/`tag` des `SPRT`, et ce que `Renderer.AddSprite` en fait) | le mode par couche, cité | chaque couche des sept boîtes a son mode |
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

**Prérequis** : D0.4, D0.10 ; décision de l'auteur sur les touches (§6 point 2).
Cinq bits sur `AlundraPadState` (`L1`, `L2`, `R1`, `R2`, `Select`), cinq lignes d'`ActionBits`, cinq
liaisons dans `PlayerSetupWriter` (manette `LeftShoulder`, `LeftTrigger`, `RightShoulder`,
`RightTrigger`, `Back`) ; et `ButtonsJustPressedByInterval` porté ligne à ligne depuis
`PadManager.UpdatePad` (D-E13D-8), **mis à jour une fois par tick logique** (D-E13D-9). Tests, à cadence
de rendu variable, avec des images à zéro tick et à deux ticks : un appui donne exactement un front ;
une touche maintenue répète après 20 ticks, puis tous les `RepeatInterval` ticks ; un changement de
l'état des boutons remet à zéro. Le convertisseur change :
**régime de preuve complet**, diff prédit = `Data/Alundra.buttonsMapping` + `report.json`.

### ⏳ D2 — Le gel, pour ce que la porte T2 ne couvre pas (conditionnelle)

**Prérequis** : D0.3. **N'existe que si D0.3 trouve un mécanisme du moteur qui continue de tourner hors
de la porte** (par exemple la gravité du contrôleur pendant une ouverture en plein saut). Sinon elle est
retirée du plan, avec la mesure qui le justifie.

### ⏳ D3 — Le patron des boîtes

**Prérequis** : D0.1, D0.2 ; décision de l'auteur sur le patron (§6 point 1).
Si l'export est retenu : l'analyseur sort les boîtes de l'inventaire principal et celle de description
(position, taille en cases, couches A et B case par case, mode de mélange) en CSV brut, sur le précédent
de S1.b et S1.c ; le convertisseur publie un JSON brut où chaque case désigne l'identifiant d'actif du
`wind_NNN` qui porte son tuple, comme S2 le fait pour les icônes. Régime de preuve complet.

### ⏳ D4 — DLL : le directeur de l'inventaire

**Prérequis** : D0.5, D0.6, D0.7, D0.8, D0.10, D1 ; D2 si elle existe.
Le directeur lit les fronts et la répétition de D1, au tick, jamais l'instantané par image rendue.
Machine à états au tick, portée ligne à ligne : déclencheur et ses gardes, glissement des sept boîtes en
15 ticks, `MenuOpen` et son retrait en fin de glissement, curseur et bouclages sur la répétition des
touches, équipement d'arme et d'objet par les fonctions d'E13.c, texte déroulant, sons 1 à 5, drapeaux du
HUD, `Falcon`/`FalconTemp` à 0 (D-E13D-5). Corrige au passage le commentaire périmé de
`AlundraPlayerManager.cs:555-558`. Aucune dépendance MGUI.

### ⏳ D5 — DLL : l'écran XAML, le présentateur, la capture

**Prérequis** : D0.9, D3, D4 ; décision de l'auteur sur le portrait (§6 point 3).
Écran XAML (boîtes composées depuis D3, grille d'icônes, curseur, cadres de sélection, textes en `font3`,
chiffres), présentateur branché comme celui du dialogue. Tests headless sur `MGDesktop` ; capture en
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
tant qu'il est ouvert, y compris après une ouverture en plein saut ; suites vertes ; chaque export prouvé
par double export.

## 5. Arrêts

- Une mesure de D0 qui contredit le §1 ou le §6 : plan révisé et resoumis avant D1-D3.
- Un manque de MGUI ou du moteur (par exemple un mode de mélange PSX que MGUI ne sait pas dessiner) :
  consigné dans `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`, et la tranche s'arrête.
- Un diff d'export hors du prédit, un double export hors de `{report.json}`.
- Un graphisme de l'inventaire sans actif exporté — une case de boîte dont le tuple n'a pas de
  `wind_NNN`, le curseur, un cadre, un chiffre ou le portrait : l'hypothèse « pixels déjà exportés »
  tombe, et il faut une extraction.

## 6. Points ouverts

1. **Le patron des boîtes — QUESTION POUR L'AUTEUR.** (a) CSV dans l'analyseur, JSON brut publié par le
   convertisseur, boîtes composées case par case dans la DLL : le précédent de S1.b/S2, indifférent au mode
   de mélange. (b) Même CSV, mais le convertisseur **cuit** une image par boîte : plus simple côté DLL, mais
   impossible si D0.2 trouve un mélange PSX qui n'est pas un simple alpha. (c) Recopier les tableaux
   décompilés dans la DLL : aucune donnée à exporter, mais environ 2700 littéraux dans le code.
   **Recommandé : (a).**
2. **Les touches — QUESTION POUR L'AUTEUR.** Proposition, sans conflit avec les touches actuelles :
   `L1` = A, `R1` = S, `L2` = Q, `R2` = W, `Select` = Tab.
3. **Le portrait d'ouverture — QUESTION POUR L'AUTEUR.** Le porter dès D5 (fidèle, recommandé), ou le
   reporter pour livrer l'inventaire plus tôt.
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
