# Plan — E13 : la jauge permanente (HUD)

**État** : ⏳ rédigé, en attente d'approbation. Aucune ligne de code écrite.
**Rédigé le** : 2026-09-18, à partir d'une reconnaissance mesurée (25 agents, 6 surfaces, 18 faits
passés en vérification indépendante, 1 critique de complétude).
**Branche** : `chantier/e13-hud` (dépôt parent).

---

## 0. Ce que la reconnaissance a corrigé de nos propres hypothèses

Trois hypothèses de départ étaient fausses. Elles sont corrigées ici avant toute décision, parce que
deux d'entre elles auraient orienté le chantier dans une impasse.

1. **« Les fonds sont des tuiles mises bout à bout. »** Faux pour la jauge permanente.
   `UIBoxHud` a `Width = 0`, ce qui désactive le mécanisme générique de grille de sprites
   (`SpritesA`/`SpritesB`). Le HUD repositionne chaque élément à la main. Il n'y a pas de cadre ni de
   bordure à assembler. Le fond des cases arme et objet, lui, n'a aucune source pixel : c'est un quad
   gouraud non texturé (`HudManager.cs:587,591`), et ces cases sont hors périmètre de toute façon.

2. **« Il faut cuire les images. »** Sans objet. Les 33 tuiles de la jauge sont déjà extraites dans
   `data-extracted/ui/wind.png` par **deux sites, et non un seul** : les 10 tuiles de chiffres par
   `alundra-datas-analyser/AlundraTools/AlundraDataExtractor/Program.cs:1034-1043`, les 23 autres par
   `AddHudTiles` (`Program.cs:1059-1125`), soit 10 + 23 = 33. Elles sont cataloguées dans
   `ui/wind.json` (277 entrées) et déjà converties en `.sprite` individuels par `UiWriter`
   (`Writers/UiWriter.cs:91-128`) : `alundra-project/UI/` porte `wind_000` à `wind_276`.
   Le convertisseur n'a rien à ajouter.

   > Décompte et présence revérifiés en relecture adverse le 2026-09-18. La première rédaction
   > attribuait les 33 tuiles au seul `AddHudTiles`, qui n'en pose que 23 : un exécutant vérifiant
   > cette citation aurait conclu à tort à un trou d'extraction et déclenché l'arrêt de §5.

3. **« Animation2d portera les sprites animés. »** Mesuré comme le mauvais outil. MGUI sait animer
   des images de planche nativement depuis le programme animation V5 : `MGTextureFillBrush.FrameGrid`
   et `FrameIndex` (`MGTextureFillBrush.cs:50-80, 133-138`) plus la cible animable publique
   `Background.Texture.Frame` (`UIExtraAnimationTargets.cs:37`), avec 51 tests. À l'inverse, aucun
   pont n'existe entre `AnimatedSpriteComponent` et un contrôle MGUI, et `UiWriter` n'émet aucune
   donnée `Animation2dData`. Les deux seuls éléments animés du périmètre sont des cycles de 4 images
   déjà posés en grille régulière dans `wind.png`.

---

## 1. Les faits établis

Tout ce qui suit est mesuré dans la décompilation, dans les données extraites ou dans le code cible.
Ce qui reste déduit est signalé comme tel.

### 1.1 Les points d'entrée d'origine

Un seul couple de fonctions porte la jauge permanente.

| Rôle | Fonction | Adresse |
|---|---|---|
| Rendu et mise à jour | `HudManager.Fun_8004bea4` | `0x8004bea4` |
| Construction des sprites | `HudManager.FUN_8004b770` | `0x8004b770` |

Les deux sont enregistrées comme le callback d'identifiant 1, commenté `//hud`, dans
`g_initialCallbackTable` (`StaticVariables.cs:11397-11401`). Les quatre afficheurs sont
`DisplayLife` (`HudManager.cs:716`), `DisplayMp` (`:605`), `DisplayMoney` (`:857`) et
`DisplayHpMaxWithNumber` (`:910`), plus l'initialisation `InitializeHpAndMp` (`:17`).

Le callback n'est armé que par `SetTransitionType(1)`, appelé depuis un seul site.
`FUN_8004b770` ne s'exécute donc qu'à cet armement, pas à chaque image.

### 1.2 La machine d'ouverture et de fermeture

L'état vit dans `g_drawFrameFlags` (`0x80176310`). Quatre valeurs mesurées.

| Valeur | Signification |
|---|---|
| 0 | repos : la jauge n'est ni dessinée ni mise à jour |
| 5 | ouverture armée ou en cours |
| 1 | affichée, état stable |
| 3 | fermeture armée ou en cours |

Le déplacement passe par le tween générique `UIManager.UpdateUiBoxesPosition`
(`0x80047dd0`, `UIManager.cs:968-993`), partagé avec l'inventaire, le dialogue et la carte mémoire.
`(x, y)` est la position de départ, `(startX, startY)` la cible, malgré ce que les noms suggèrent.

`UIBoxHud` vaut `{ X = 0, Y = 0x10, Width = 0, Height = 5 }`. La position hors écran vaut
`~(Height << 3)`, soit **-41**. Les deux fonctions d'armement posent `mode = 2` et `speed = 0xf`.

> **Piège relevé par deux lectures indépendantes.** `InitializeHudPositionBeforeHide` (`0x8004be0c`)
> anime en réalité une **apparition**, et `InitializeHudPosition` (`0x8004bd9c`) une **disparition** :
> l'inverse de ce que disent leurs noms. Les noms d'états du portage ne doivent pas être calqués sur
> eux. Ce point est **déduit** du sens de `(x,y)` contre `(startX,startY)` et reste à reconfirmer sur
> le désassemblage brut en C0.

### 1.3 La suite exacte des positions

L'interpolation est en **arithmétique entière** (`UIManager.cs:988-989`), avec troncature **vers
zéro**. Chaque transition a donc **sa propre suite**, et la fermeture n'est **pas** l'ouverture à
l'envers : sur un delta négatif la troncature penche dans l'autre sens.

**Ouverture** — `y = -41`, `startY = 16`, `speed = 15` :

```
-41  -38  -34  -30  -26  -22  -19  -15  -11  -7  -3  0  4  8  12    puis 16
```

**Fermeture** — `y = 16`, `startY = -41`, `speed = 15` :

```
16  13  9  5  1  -3  -6  -10  -14  -18  -22  -25  -29  -33  -37    puis -41
```

La valeur finale est atteinte au 16e appel et tenue deux appels de plus, dans les deux sens. Les pas
font 3 ou 4 pixels selon la troncature, **jamais uniformes**. Total : **18 images rendues** par
transition.

C'est cette suite, et non la seule durée, qui décide si le verbatim est atteignable. Une
interpolation continue ne la reproduit pas.

> **Correction de relecture adverse (2026-09-18).** La première rédaction ne donnait qu'une table et
> déclarait « ouverture et fermeture confondues ». C'est faux. Les deux suites ci-dessus ont été
> recalculées à la main depuis la formule et les valeurs d'armement, indépendamment de la
> reconnaissance. Une seule table aurait donné un test de fermeture sans référence juste.

### 1.4 Les cadences des compteurs

Toutes les cadences se comptent en images rendues du HUD, via le compteur `INT_800a827c` incrémenté
une fois par rendu (`HudManager.cs:319`).

| Compteur | Cadence mesurée | Citation |
|---|---|---|
| Rattrapage vie et magie | 1 point toutes les 8 images | garde `(INT_800a827c & 1) == 0` à `HudManager.cs:328`, cycle 0..3 de `g_playerDataHud[5]` à `:341-351` |
| Roulement de l'argent | ±10 puis ±1 vers la valeur cible | `HudManager.cs:438-476` |
| Clignotement de l'icône de pièce | 1 image toutes les 6 images, seulement pendant le roulement | `HudManager.cs:478-486`, garage sur l'image 0 à `:462` |
| Frétillement du pip de magie | **1 image toutes les 10 images**, cycle de 4 | `iVar1 = INT_800a827c / 10` puis `iVar4 % 4` à `HudManager.cs:634-646` |

L'icône de pièce se gare sur son image 0 dès que l'argent est stabilisé.
L'état animé vit dans `g_playerDataHud[10]`, distinct des vraies valeurs de jeu.

> **Correction de relecture adverse (2026-09-18).** La première rédaction annonçait le pip de magie à
> une image toutes les 4 images. C'est faux : la division par 10 donne la cadence, le modulo 4 donne
> la longueur du cycle. La valeur fausse aurait été reprise telle quelle par l'acceptation de C1.
>
> **Réserve à lever en C0** : la ligne active de `:634-646` est une reconstruction du translittérateur,
> l'original `BYTE_ARRAY_800a3238` étant commenté à `:644-645`. La cadence de 10 est donc à
> reconfirmer sur le désassemblage brut en même temps que l'inversion de noms de §1.2.
>
> **Levée en C0 le 2026-09-19, autant qu'elle peut l'être sans Ghidra.** Le `/ 10` et le `% 4` sont
> bien d'origine : `if (x < 0) x += 3` puis `% 4` est l'idiome Ghidra du modulo 4 signé, la ligne
> commentée `:645` en porte la forme canonique, et `:650-654` recalculent le même index sans
> l'utiliser, code mort typique du décompilateur. Ce qui est reconstruit, c'est la correspondance
> phase → image, voir le point ouvert 4 de §6.

### 1.5 Ce qui varie, et de combien

C'est la mesure qui tranche la question de la cuisson.

| Élément | Variantes | Plafond |
|---|---|---|
| Vie maximale | pas de 1, plus l'opcode `0xBC` toujours d'opérande 2 | 50, départ 10 |
| Magie maximale | 4 emplacements fixes | 4, départ 0 |
| Cristal de vie | 2 états statiques | — |
| Pip de vie | 2 états statiques | — |
| Pip de magie | 4 images animées plus 1 état vide | 5 variantes |
| Icône de pièce | 4 images en boucle | — |
| Argent | toujours exactement 4 chiffres | clamp `[0, 9999]` |

**Le nombre de tuiles par barre est une constante de compilation.** Il ne dépend jamais d'une valeur
d'exécution. Seul le contenu de chaque tuile change.

La zone argent occupe toujours exactement 5 tuiles de 8×16 contiguës, aux abscisses
`UIBoxHud.X + 0x100 + i*8` pour `i` de 0 à 4 : quatre chiffres puis l'icône. Aucune suppression des
zéros de tête, une somme nulle s'affiche `0000`. Une feuille de glyphes partagée porte les chiffres
et la barre oblique, pour la vie maximale comme pour l'argent.

**Aucun état visuel d'empoisonnement n'existe.** La vie basse ne produit qu'un signal sonore. Un bloc
commenté suggère un flash de palette au gain ou à la perte : non confirmé actif, gelé par D-E13-7.

### 1.5 bis Ce qui contourne les bornes, et le piège des deux objets

Relevé en relecture adverse puis revérifié ligne à ligne le 2026-09-18. C'est la liste que
l'acceptation de C0 doit couvrir, ni plus ni moins.

**Les deux sites sont sous garde de mode invincible**, et seulement là. Ils sont hors périmètre d'E13
(voir la note de C0), et relevés ici pour qui portera le combat.

| Site | Garde | Ce qu'il fait |
|---|---|---|
| `Gameplay/EntityManager.cs:371-375` | `if (IsGodMode)` à `:371` | écrit `PlayerEntity.Hp = HpMax` et `g_playerStats.Mp = MpMax` **en direct**, sans passer par les setters, donc sans clamp |
| `Gameplay/PlayerManager.cs:1725-1728` et `:1751-1754` | `if (IsGodMode)` | surcharge **à l'intérieur** des setters : remet `Hp` à `HpMax` après coup |

> **Le vrai piège, et il est l'inverse de ce que j'avais écrit.** `g_playerStats` et
> `g_saveData.PlayerStats` sont **le même objet**. `GameInitializer.cs:444-445` alloue
> `g_saveData.PlayerStats = new PlayerStats()` puis pose immédiatement
> `g_playerStats = g_saveData.PlayerStats`, et `PlayerStats` est un type référence
> (`Gameplay/PlayerStats.cs:3`). Les écritures qui suivent en `:446-450` portent donc sur l'unique
> instance partagée, et elles valent `HpMax = 1`, `Hp = 1`, `MpMax = 0`, `Mp = 0`, `MoneyAmount = 0`.
>
> **Règle pour le portage** : une seule instance possédée, partagée entre la sauvegarde et l'état
> courant. Toute divergence serait un écart délibéré à justifier, pas un détail d'implémentation.
>
> La première rédaction affirmait deux objets distincts et des valeurs à zéro. Les deux étaient faux,
> déduits d'une fenêtre de lecture qui commençait une ligne trop bas. Relecture adverse du
> 2026-09-18, revérifiée ligne à ligne.

**Le jeu de valeurs de débogage** dont dépend D-E13-6 vit dans la branche marquée
`// unused, only for debugging` de `GameInitializer.cs:378-392` :

| Grandeur | Valeur |
|---|---|
| Vie | 38 sur 45 |
| Magie | 2 sur 3 |
| Argent | 2163 |

Elles exercent exactement ce qu'il faut : une barre de cœurs longue et partielle, une jauge de magie
partielle sous son plafond, et quatre chiffres non nuls. La branche appelle `InitializeHpAndMp()`
juste après.

### 1.6 Le déclencheur, qui n'était pas où on le cherchait

La machine complète est en `GraphicManager.cs:1646-1685`. Le bit persistant
`GameFlags[0x33] |= 0x40000000` est posé au seul site `:1664`, en réponse à la demande
`GameFlags[0x38] & 0x200000`, et effacé en `:1670` par `0x400000`. Tant que le bit persistant vaut 0,
la fermeture est réarmée à chaque image.

L'écrivain a été cherché dans les **données**, pas dans le code : il est présent dans les programmes
d'évènement de trois cartes, `map_31`, `map_327` et `map_393`. Le portage **dispatche déjà** ces
opcodes (`AlundraEventProgramRunner.cs:437-451`, `AlundraGameState.cs:185-200`). Ce qui manque n'est
donc pas le déclencheur mais **son consommateur**.

### 1.7 Ce que le portage a déjà, et ce qu'il n'a pas

**Il n'a pas** : aucune des cinq grandeurs. `Hp` et `HpMax` existent comme champs de la structure
d'entité générique `AlundraEntityScriptProxy` mais ne sont jamais alimentés pour le joueur.
`Mp`, `MpMax` et `Money` n'existent nulle part dans `Alundra/Scripts`. Aucun code de HUD n'existe.

**Il a** : `AlundraGameState.Instance` comme porte-état de session. Le patron directeur de session et
présentateur livré par E12, entièrement réutilisable. Un cycle de tick logique dans
`AlundraWorldProxy.Update` auquel tout directeur s'accroche.

Dans l'original, les grandeurs vivent dans `g_playerStats` et une nouvelle partie démarre à
`10 / 10 / 0 / 0 / 0` (`GameInitializer.cs:374`). Un jeu de valeurs de débogage existe en `:387-391`.

### 1.8 La cible MGUI

| Besoin | Moyen | Vérifié |
|---|---|---|
| Durée exacte | `TimeSpan` .NET partout | oui |
| Courbe neutre | `UIEasing.Linear`, défaut des images clés | oui |
| Animer un élément quelconque | `MGElement.EnterExit` sur la classe de base | oui |
| Translation publique | `RenderTransform.Translation` | **élément seulement** |
| Images de planche | `FrameGrid` / `FrameIndex`, cible `Background.Texture.Frame` | oui |

> **Contrainte structurante.** Une fenêtre n'honore pas `RenderTransform` : son glissement passe par
> `UIWindowEnterExitTranslationTarget`, déclaré `internal`, donc inatteignable depuis la DLL du
> portage. Le verbatim n'est atteignable que si **le HUD est un élément**, pas une fenêtre.

L'horloge `UIAnimationClock` avance en **temps réel**, jamais depuis `GameTime`. Elle expose
`IsPaused` et `TimeScale` en public. La constante de conversion existe déjà dans le dépôt :
`PsxFrameSeconds = 1f / 50f`, la source étant PAL (`SpriteWriter.cs:110`).

### 1.8 bis Pourquoi les images clés MGUI ne suffisent pas, et ce qui les remplace

**Le défaut, relevé en relecture adverse.** Une piste d'images clés **interpole chaque segment**
(`MGUI.Core/UI/Animation/KeyFrames/UIKeyFrameTrack.cs:8-13`) et `UIEasing` n'expose **aucune fonction
en marche d'escalier** (`Easing/UIEasing.cs:14-55`). Seize clés linéaires échantillonnées sur une
horloge temps réel ne produisent donc pas les 18 valeurs entières de §1.3 : elles produisent un
continu qui passe par elles. D-E13-3 n'était pas atteignable par le moyen que la première rédaction
avait retenu.

**Le remplacement, vérifié.** `UIRenderTransform.Translation` est une propriété publique modifiable
avec notification de changement (`UIRenderTransform.cs:32-41`). Le portage n'a donc besoin d'aucune
animation MGUI pour la transition : **le directeur de C1 pose lui-même la translation à chaque tick
logique**, en lisant la table de §1.3. Dix-huit ticks, dix-huit valeurs entières, exactement celles
du jeu d'origine. Le verbatim devient vrai par construction, et non par réglage.

Même raisonnement pour les deux cycles de 4 images : le directeur pose `FrameIndex`, MGUI dessine la
grille. D-E13-4 tient sur le fond, puisque aucun pont Animation2d n'est écrit et que la grille
d'images de MGUI reste le moyen de rendu.

**Repli documenté** si la mesure invalidait ce chemin : enregistrer une interpolation en escalier via
`UIEasing.Register(string, IUIEasingFunction)` (`Easing/UIEasing.cs:86`), qui est public.

### 1.9 Le moteur autour

Deux patrons de HUD existent déjà et servent de modèle : `MainHUDScreen` du RPGDemo et `HudScreen`
des Demos. `UIScaler` calcule une échelle qui **n'est appliquée à rien**. Le projet Alundra est écrit
en 320×236 natif magnifié 4 fois (`AlundraDisplay.cs`), alors que MGUI met en page en pixels de
viewport bruts.

Deux pièges de rendu mesurés : `MGImage.UseLinearFilteringWhenDownscaling` vaut `true` par défaut, et
une translation animée est un `Vector2` flottant, donc des positions non entières.

---

## 2. Décisions verrouillées

Arbitrées avec l'auteur le 2026-09-18, avant rédaction. Ne pas re-débattre.

| Réf | Décision | Conséquence |
|---|---|---|
| D-E13-1 | **Périmètre : la jauge permanente seule** — cœurs, magie, argent | Les écrans qui s'ouvrent, l'inventaire, les cases arme et objet sont hors périmètre. **Amendée le 2026-09-19** : l'auteur, après avoir vu la jauge en jeu, découpe E13 en quatre étapes dans la feuille de route ; ce plan couvre E13.a (la jauge, livrée) et **E13.b, les fonds des cases arme et accessoire, tranche C6**. Les icônes (E13.c) et l'inventaire déclenché par L1/R1 (E13.d) auront leurs propres plans, après reconnaissance, parce qu'ils exigent une extraction et une cuisson que ce plan exclut |
| D-E13-2 | **Aucune cuisson** : réemployer les `.sprite` déjà exportés | Le convertisseur n'est pas touché. Pas de nouvel actif, pas de preuve d'export à produire |
| D-E13-3 | **Fidélité verbatim** de la machine d'ouverture et de fermeture | Les 18 images et la suite entière de positions sont reproduites, pas approchées |
| D-E13-4 | **MGUI porte toutes les animations**, y compris les deux cycles de 4 images | Animation2d est écarté pour E13. Aucun pont, aucun émetteur à écrire. **Précision apportée par C2 et C3 (2026-09-19)** : la grille d'images de MGUI n'est finalement pas employée, parce que les quatre images de chaque cycle sont des sprites distincts et non contigus dans l'atlas (pip 1/3/10/17, pièce 126/130/134/139) ; le composeur choisit le sprite par image, le tick pose l'index, MGUI dessine. L'esprit de la décision tient : aucune animation MGUI n'est jouée, aucun pipeline Animation2d n'est engagé |
| D-E13-5 | **Le HUD est un élément dans une fenêtre hôte**, jamais une fenêtre | Une fenêtre n'honore pas `RenderTransform` et sa cible de translation est `internal` (§1.8). **Amendée le 2026-09-18** : la mention « animé par images clés » est retirée, supersédée sur ce point par D-E13-8. Seule la structure élément-dans-fenêtre-hôte subsiste |
| D-E13-6 | **Recette par les valeurs de débogage** de l'initialisation | Aucun script ne crédite d'argent avant E14 |
| D-E13-7 | **Le flash de palette commenté reste gelé** | Non confirmé actif, et ses tuiles intermédiaires sont absentes des pixels de `wind.png` |
| D-E13-9 | **L'échelle pixel appartient à l'écran du HUD, dérivée du viewport** | Tranchée en C2 le 2026-09-19 : facteur entier `max(1, largeur du viewport / 320)`, 320 cité à `AlundraDisplay.cs:32`, confirmé à 4 en capture 1280×944. Si `PixelScale` change dans le convertisseur, le HUD suit sans troisième copie. `UIRoot.UIScale` n'est pas consommé |
| D-E13-10 | **Recette : une variable d'environnement lue à la nouvelle partie**, `ALUNDRA_HUD_DEBUG=1` | Arbitrée le 2026-09-19. Si elle est posée, la nouvelle partie démarre avec le jeu de valeurs de débogage de D-E13-6 et lève la demande d'affichage, exactement comme un script de carte le ferait. Rien de pilotable en cours de partie. Tranche C5.a |
| D-E13-12 | **Recette : la touche F1 bascule la jauge à volonté**, et prime sur D-E13-10 | Décidée par l'auteur le 2026-09-19 après un essai infructueux de la variable : le journal du jeu, qui trace la lecture de la variable, n'en portait aucune trace, le processus du lanceur ne l'avait pas dans son environnement. F1 sur front montant : premier appui charge le jeu de débogage et lève la demande d'affichage 1813 ; appui suivant efface le verrou 1662 comme un script « Flag off », ce qui déclenche la fermeture animée ; appui suivant relève 1813 sans toucher aux stats. Le chemin par variable reste en place. Tranche C5.a bis |
| D-E13-11 | **C4 : changement moteur, l'effet d'écran se dessine au-dessus de l'interface** | Arbitrée le 2026-09-19 après réfutation de la prémisse (§6 point 6). Fidélité exacte : la jauge s'assombrit avec le décor comme dans l'original. Chantier `CasaEngineMonogame`, plan dans `ai-agent/tasks/`, branche et verifier propres, approbation de l'auteur. E13 attend sa livraison pour clore C4 |
| D-E13-8 | **Le tick logique possède tout le temps du HUD. MGUI dessine.** | Ajoutée le 2026-09-18 après relecture adverse, voir §1.8 bis. Le directeur pose la translation et l'index d'image à chaque tick ; aucune animation MGUI n'est jouée pour la jauge. Rend D-E13-3 atteignable par construction, supprime la dérive entre l'horloge temps réel et le tick, et rend inutile le câblage de `IsPaused` |

---

## 3. Tranches

Un committeur par dépôt, ordre strict. Une tranche égale un commit plus un verifier frais.

### ✅ C0 — DLL : l'état joueur (verifier CONFIRMED, 840/840)

**But** : les cinq grandeurs existent et se comportent comme l'original.

**Contenu** : porter `PlayerStats` sur `AlundraGameState` — `Hp`, `HpMax`, `Mp`, `MpMax`, `Money` —
avec les bornes mesurées. Vie maximale plafonnée à 50, plancher 0. Magie maximale plafonnée à 4,
plancher 0, clamp des deux côtés. Argent clampé à `[0, 9999]`. Initialisation d'une nouvelle partie à
`10 / 10 / 0 / 0 / 0`. Exposer le jeu de valeurs de débogage pour la recette.

**Reconfirmer ici** : l'inversion de noms de §1.2, sur le désassemblage brut, avant que C1 ne fige les
noms d'états.

**Hors périmètre, explicitement** : le mode invincible et les deux contournements de §1.5 bis. Ils ne
sont pas requis pour afficher la jauge, et C0 ne porte pas le drapeau qui les active. Ils restent
documentés en §1.5 bis pour le chantier qui portera le combat.

**Acceptation** : tests unitaires sur chaque borne de §1.5. Un test montrant que la sauvegarde et
l'état courant **partagent la même instance**, conformément à §1.5 bis. Un test posant le jeu de
valeurs de débogage et relisant 38 sur 45, 2 sur 3 et 2163. Suite `Alundra.Tests` verte.

### ✅ C1 — DLL : le directeur de session du HUD (verifier CONFIRMED, 853/853)

**But** : l'état animé du HUD vit et avance au tick logique, indépendamment de tout écran.

**Contenu** : un `AlundraHudDirector` sur le patron E12, accroché au cycle `ticksThisFrame` de
`AlundraWorldProxy.Update`. Il porte les compteurs roulants de §1.4, la machine d'états de §1.2, et
**la position de la jauge** : sous D-E13-8 c'est lui qui avance dans la table de §1.3, un cran par
tick. Il consomme le déclencheur déjà dispatché de §1.6.

Le directeur ne connaît pas MGUI : il expose un état lisible. C2 et C3 le branchent sur l'écran.

**Pourquoi un directeur et pas l'écran** : `DialogueScreen.IsModal` vaut `true`, et la pile d'écrans
gèle la mise à jour des écrans inférieurs. Dans l'original, la jauge continue de tourner pendant un
dialogue. Faire dépendre son état de `IUIScreen.Update` figerait les cœurs en plein rattrapage à
chaque conversation.

**Acceptation** : tests de cadence sur chaque compteur, test de la machine d'états sur les quatre
valeurs, test montrant que le directeur avance alors qu'un écran modal est poussé.

### ✅ C2 — Écran MGUI : la composition statique (verifier CONFIRMED à la quatrième passe, 863/863)

**But** : la jauge s'affiche, juste, au bon endroit, nette.

**Contenu** : un écran non modal composant les `.sprite` déjà exportés aux décalages mesurés dans
`Fun_8004bea4`. Le HUD est un élément dans une fenêtre hôte transparente, conformément à D-E13-5.
L'écran ne fait que dessiner : le directeur lui pousse les valeurs.

**Les deux pièges de netteté** : poser `UseLinearFilteringWhenDownscaling = false` sur chaque image,
et n'agrandir que d'un facteur entier. Trancher au passage le propriétaire de l'échelle pixel, entre
`UIScaler` inutilisé et le `PixelScale` du projet.

**Acceptation** : capture en jeu par lecture du back-buffer en processus, comparée aux décalages
mesurés. Aucun filtrage linéaire.

### ✅ C3 — Les transitions et les deux cycles, au tick (verifier CONFIRMED, 869/869)

**But** : les deux transitions et les deux cycles, verbatim, sous D-E13-8.

**Contenu** : le directeur de C1 pose `RenderTransform.Translation` à chaque tick logique en lisant la
table de §1.3, et pose `FrameIndex` pour le pip de magie et l'icône de pièce aux cadences de §1.4.
La grille d'images de MGUI (`FrameGrid`) reste le moyen de rendu des deux cycles. **Aucune animation
MGUI n'est jouée**, donc aucune horloge à câbler et aucune interpolation à neutraliser.

**Acceptation** : **deux** tests de position, un par sens, comparant les 18 valeurs successives
produites par le directeur à **sa** table de §1.3 — celle d'ouverture pour l'ouverture, celle de
fermeture pour la fermeture. **Égalité exacte exigée**, ce sont des entiers, et les deux suites
diffèrent. Un troisième test relève les index d'image sur 40 ticks et vérifie les cadences de §1.4,
pip de magie compris à sa valeur corrigée.

### ⏳ C4 — La jauge s'assombrit avec le décor (réécrite le 2026-09-19 sous D-E13-11)

**Ce que la première rédaction disait, et pourquoi c'était faux** : « l'original ferme le HUD avant
le fondu ». Réfuté, voir §6 point 6 : au warp, l'original capture l'image avec la jauge et fond
dessus. La jauge ne glisse pas, elle s'assombrit avec le décor.

**But** : au warp, la jauge s'assombrit avec la scène, comme dans l'original.

**C4.moteur** — chantier `CasaEngineMonogame`, hors de ce plan : l'effet d'écran obtient un mode qui
se dessine **après l'interface**. Plan propre dans `CasaEngineMonogame/ai-agent/tasks/`, branche
propre, plan-verifier, approbation de l'auteur, verifier de sortie, archivage. Le portage n'écrit
rien dans le moteur depuis ce plan.

**C4.dll** — une fois C4.moteur livré et le pointeur de sous-module bumpé : le directeur de fondu du
portage arme le mode « au-dessus de l'interface » pour le fondu de warp. Rien d'autre : le directeur
du HUD reste intact, la jauge reste `Displayed` pendant le warp et réapparaît à l'arrivée sans
glissement, exactement comme `g_drawFrameFlags` reste à 1 dans l'original.

**Acceptation** : en jeu, à un warp, la jauge s'assombrit avec le décor et n'est jamais lisible sur
fond noir ; à l'arrivée elle est déjà là, sans glissement. Capture en processus à mi-fondu.

**Arrêt** : si C4.moteur révèle que l'interface MGUI n'est pas dessinée par le pipeline 2D mais après
lui, le mode « au-dessus » doit se poser à cet endroit-là et non dans une passe ; c'est le plan
moteur qui le dira, pas celui-ci.

### ✅ C5.a — DLL : l'activation de recette par variable d'environnement (D-E13-10) (verifier CONFIRMED, 873/873)

**But** : l'auteur peut voir la jauge sans attendre une carte qui la demande.

**Contenu** : la variable d'environnement **`ALUNDRA_HUD_DEBUG`**, active quand elle vaut exactement
`1`, toute autre valeur ou son absence étant inactive. Si elle est posée à la nouvelle partie, la DLL
charge le jeu de valeurs de débogage de C0 et lève la demande d'affichage par le même drapeau qu'un
script de carte, identifiant 1813, via `AlundraGameState.AddFlag`. Rien d'autre ne change : le
directeur voit une demande ordinaire. Sans la variable, comportement d'origine, la jauge n'apparaît
que sur demande de script.

**Livré** : le site de nouvelle partie existait déjà, documenté par le chantier des transitions, une
entrée de carte sans enregistrement d'arrivée dans `AdoptPlayerPawn` ; un verrou de session
`DebugHudRecipeApplied` interdit tout rejeu. La lecture suit le seam déjà en usage dans le fichier
pour les autres variables de débogage, avec une surcharge pour les tests qui ne touchent jamais
l'environnement du processus. Le drapeau est levé par les constantes de C1, `1813` et `0x200000`,
identiques à ce que produit l'opcode `0x05` de `AlundraEventProgramRunner.cs:437-441`.

**À savoir pour la recette** : la vie maximale ne saute pas à 45, elle y monte par le rattrapage
d'origine de §1.4, un point toutes les huit images, soit environ cinq secondes et demie avant que la
jauge n'atteigne 38 sur 45 et 2163.

**Essai de l'auteur, 2026-09-19** : jauge absente. Le journal du jeu montre une DLL postérieure aux
sources, l'écran câblé à la vue, et **aucune** ligne de lecture de la variable, alors que
`AlundraWorldProxy.cs:169` en écrit une dès qu'elle vaut `1`. Le processus du lanceur ne l'avait pas
dans son environnement. Décision D-E13-12 : F1, tranche C5.a bis ci-dessous.

### ✅ C5.a bis — DLL : la touche F1 (D-E13-12) (verifier CONFIRMED, 878/878 ; **validée en jeu par l'auteur le 2026-09-19, « ça marche bien »**)

**But** : l'auteur fait apparaître et disparaître la jauge quand il veut, sans rien poser avant de
lancer.

**Contenu** : F1 détectée sur front montant par le chemin d'entrée que la DLL utilise déjà, le
gestionnaire de correspondances du moteur. Premier appui de la session : jeu de débogage de C0 puis
demande d'affichage 1813, comme un script. Appui suivant, jauge affichée : effacement du verrou 1662
comme l'opcode `0x06` d'un script, ce qui fait rejouer l'armement de disparition par la branche du
loquet de §1.6, donc la fermeture animée. Appui suivant : demande 1813 sans recharger les stats.
Chaque appui journalisé. Le chemin par variable reste.

**Acceptation** : tests sans tête sur les trois appuis et sur le front montant, par un seam
d'entrée ; en jeu, F1 fait apparaître la jauge à 38 sur 45, 2 sur 3 et 2163, F1 la referme par le
glissement, F1 la rouvre avec les mêmes valeurs.

**Acceptation** : test sans tête sur les deux branches ; en jeu, avec la variable, la jauge apparaît
sur la carte 389 à 38 sur 45, 2 sur 3 et 2163 ; sans, elle n'apparaît pas.

### ✅ C6 — Les fonds des cases arme et accessoire (E13.b, D-E13-1 amendée) (verifier CONFIRMED, 884/884)

**Faits mesurés le 2026-09-19** (`HudManager.cs`, reconnaissance à trois surfaces) : chaque case a
pour fond un `POLY_G4`, quad gouraud non texturé de **24×32** (`0x18×0x20`), à
`X = UIBoxHud.X + g_inventoryWeaponIconX[8 + i]`, soit **16** pour l'arme et **48** pour l'accessoire,
`Y = UIBoxHud.Y` (`:180-187`). Les quatre couleurs de sommet, posées une fois à l'armement dans
`FUN_8004b770` (`:189-201`) et jamais changées : haut-gauche **(0,0,0)**, haut-droit **(255,255,0)**,
bas-gauche **(0,255,255)**, bas-droit **(0,0,255)**. Dessiné par
`AddQuadColor(polyG4, SpriteDepth.BackgroundUI, 0.5f)` (`:587`, `:591`) : **alpha 0,5**, sous les
icônes par profondeur. Repositionné avec le glissement de la jauge (`:301-316`), **toujours dessiné**
tant que la jauge l'est, case vide ou non (`:584-592`, hors des deux `if`), sans animation propre.

**Cible** : `MGGradientFillBrush` (`MGUI.Core/UI/Brushes/FillBrushes/MGGradientFillBrush.cs:12-61`),
quatre couleurs de coin indépendantes, rendu par `FillQuadrilateralLinearClamp`
(`DrawTransaction.cs:947-968`) en deux triangles gouraud GPU, l'équivalent exact du `POLY_G4`.
`MGElement.Background` accepte ce pinceau et se dessine avant le contenu (`MGElement.cs:5137-5201`).

**Contenu** : deux éléments enfants du même canevas porteur que les tuiles, ajoutés **avant** le pool
d'images pour passer dessous, aux positions natives (16, 16) et (48, 16) dans la convention des
tuiles (le composeur cuit `BoxY = 16`, la translation du canevas fait le reste), taille 24×32, le tout
multiplié par le facteur entier de D-E13-9, opacité 0,5, visibles si et seulement si la jauge est
dessinée. Les valeurs vivent dans le composeur comme une fonction pure, source de vérité testable ;
l'écran consomme. Le pool de 26 tuiles ne change pas : ce ne sont pas des tuiles.

**Acceptation** : tests sans tête sur les deux rectangles natifs, les huit couleurs et l'alpha ;
en jeu, capture en processus jauge affichée : deux quads aux bonnes positions, coins aux bonnes
teintes, mélange à moitié avec le décor, sous la jauge, qui suivent le glissement.

### ⏳ C5.b — Recette en jeu

**Contenu** : avec la variable de C5.a, vérifier l'affichage, le rattrapage, le roulement de
l'argent, les deux transitions, la persistance pendant un dialogue, et, après C4, l'assombrissement
de la jauge avec le décor au warp.

---

## 4. Acceptation d'ensemble

E13 est close quand, en jeu : la jauge s'affiche aux bonnes positions avec les bons glyphes ; les
compteurs rattrapent aux cadences de §1.4 ; **chaque transition reproduit sa propre table de §1.3**,
l'ouverture la sienne et la fermeture la sienne ; la jauge continue de tourner pendant un dialogue ;
elle se referme avant un fondu. Suites `Alundra.Tests` et `CasaEngine.Tests` vertes.

---

## 5. Arrêts

- Si l'inversion de noms de §1.2 ne se confirme pas sur le désassemblage brut, **arrêter C1** et
  reprendre la machine d'états avant d'écrire quoi que ce soit.
- Si `RenderTransform.Translation` ne s'avère pas modifiable au tick sur l'élément hôte retenu,
  **arrêter C3** et prendre le repli de §1.8 bis, l'interpolation en escalier enregistrée. Si ce repli
  ne tient pas non plus, **arrêter le chantier** et revenir vers l'auteur : D-E13-3 devient
  inapplicable et doit être re-arbitrée. Ne jamais livrer une interpolation continue en la présentant
  comme verbatim.
- Si le pip de magie ne se confirme pas à une image toutes les 10 images sur le désassemblage brut
  (§1.4), **arrêter C1** et corriger §1.4 avant d'écrire les tests de cadence.
- Si la recette exige une tuile absente des pixels de `wind.png`, **arrêter** et remonter le trou
  d'extraction plutôt que de le combler à la main.

---

## 6. Points ouverts

1. **Le propriétaire de l'échelle pixel.** `UIScaler` calcule une échelle appliquée à rien, MGUI met
   en page en pixels bruts, le projet est en 320×236 magnifié 4 fois. À trancher en C2.
2. **L'inversion de noms** de §1.2 — **reconfirmée en C0 le 2026-09-19**, mais sur le C# translittéré,
   pas sur le désassemblage brut : aucun export texte n'existe dans le dépôt, seul le projet Ghidra
   binaire. Trois dérivations indépendantes du tween convergent : les six appelants de
   `InitializeHudPositionBeforeHide` sont tous des retours au jeu normal et les quatre de
   `InitializeHudPosition` des sorties du jeu normal ; seule la première arme le rendu par
   `SetTransitionType(1)` ; le loquet `GameFlags[0x33] & 0x40000000` est posé par la demande
   d'activation qui appelle la première. Risque résiduel, non levable sans Ghidra : que le
   translittérateur ait interverti les deux corps, peu crédible vu le commentaire d'adresse
   `HudManager.cs:41`. **Les états du portage sont nommés par effet**, jamais par ces deux noms.
3. **Le flash de palette commenté**, gelé par D-E13-7, à rouvrir seulement si la recette le réclame.
4. **La phase des quatre pips de magie — TRANCHÉE PAR L'AUTEUR le 2026-09-20 : « oui les animations
   sont décalées ».** L'ondulation est donc confirmée, l'unisson porté par C1 est un écart. La table
   de phase constante que C1 a posée derrière son index d'image par pip passe de `{0, 0, 0, 0}` à la
   rotation déduite de `g_inventoryWeaponIconX` (`StaticVariables.cs:12272-12275`) relu en octets :
   ses seize premiers octets donnent `0,1,2,3 / 1,2,3,0 / 2,3,0,1 / 3,0,1,2`, c'est-à-dire
   `image = (phase + pip) % 4`, donc **un décalage égal à l'indice du pip**. Reste à vérifier en jeu
   le SENS de l'ondulation, qui dépend de quel pip porte l'indice 0 : si elle court à l'envers, les
   quatre constantes s'inversent. Tranche à écrire, le changement est de quatre constantes.

   Contexte historique, conservé : **la question telle qu'elle se posait avant la réponse.** La cadence
   (1 image / 10, cycle de 4) est bien d'origine : idiome Ghidra du modulo signé, code mort résiduel
   à `HudManager.cs:650-654`. Mais la correspondance phase → image, elle, est une reconstruction :
   la ligne active `u0 = phase * 8` remplace deux indirections commentées à `:644-645`,
   `g_inventoryWeaponIconX[i * 4 + phase] * 20` dans `BYTE_ARRAY_800a3238`, cette dernière déclarée
   commentée `//useless` à `StaticVariables.cs:12253-12254`. Or `g_inventoryWeaponIconX` existe
   (`:12272-12275`) et ses seize premiers octets, relus en petit-boutien, donnent
   `0,1,2,3 / 1,2,3,0 / 2,3,0,1 / 3,0,1,2` : une table de rotation, donc **un décalage de phase par
   pip**, une ondulation. La reconstruction, elle, fait frétiller les quatre pips à l'unisson. Cette
   relecture en octets d'un `short[]` est une **déduction**, pas une lecture. C1 fige la cadence et
   le cycle et expose un index d'image **par pip** derrière une table de phase constante ; C3 ne
   remplit cette table qu'après réponse de l'auteur sur `0x800a3238` et sur le type réel de
   `g_inventoryWeaponIconX`. Par défaut, C1 porte ce que la ligne active dit : l'unisson.
5. **Les deux bruitages du rattrapage — hors périmètre d'E13 tel qu'écrit, à décider.** Relevé
   pendant C1 : l'original joue `SoundEffect(8)` et `SoundEffect(9)` pendant le rattrapage de la vie
   et de la magie (`HudManager.cs`, blocs de `DisplayLife` et `DisplayMp`). Ni la reconnaissance ni
   le plan ne les avaient listés. Le directeur ne les joue pas ; le portage a pourtant un système
   audio depuis E11. Une tranche courte suffirait, après E13 ou dedans si l'auteur le demande.
6. **C4 : la prémisse « l'original ferme le HUD avant le fondu » est fausse pour le warp — DÉCISION
   AUTEUR.** Vérification de niveau opus le 2026-09-19 sur les fonctions d'origine que le directeur
   de warp cite comme sources. `PlayerManager.HandleWarpTransition` (`:3488-3541`, `0x80031340`) et
   `Script_ChangeMap_053` (`EntityEventHandlers.cs:1554-1586`) n'ont **aucun contact avec le HUD** :
   ni armement de glissement, ni remise à zéro, ni écriture des bits `0x38/0x200000`,
   `0x38/0x400000` ou `0x33/0x40000000`. La fonction `StartFadeOut` (`GraphicManager.cs:1767-1783`)
   que le critique avait citée ferme bien le HUD avant d'armer un fondu, mais ses deux seuls
   appelants sont un menu de débogage (`MainInventoryManager.cs:474`) et un contrôle d'état
   (`GraphicManager.cs:1697`), pas le warp. **Ce que l'original fait vraiment** : la dernière image
   normale est rendue avec la jauge, puis `StartWarpTransition` (`GameEngine.cs:258`) appelle
   `CaptureWarpTransitionFrame` (`GraphicManager.cs:30-34`, `Renderer.CaptureFrameBuffer()`) et arme
   le fondu par `InitStandardWarpEffect` (`GameEngine.cs:1375-1385`, tpage 2, 16 ticks, ce que le
   portage reproduit). Chaque image suivante passe par `AdvanceWarpTransitionFrame`
   (`GameEngine.cs:277-294`), commentée « Warp rendering bypasses RenderScene() » (`:283`) : ni scène
   ni interface ne sont redessinées, le fondu s'applique à la capture. **La jauge s'assombrit avec le
   décor, en pixels figés, et n'est jamais peinte par-dessus le noir.** À l'arrivée, `WarpPlayer`
   cas 0 (`GameEngine.cs:895-905`) rallume depuis `0xff0000` et la jauge réapparaît à la première
   vraie image, sans glissement, puisque `g_drawFrameFlags` est resté à 1.

   Le portage ne peut pas reproduire cela tel quel : `RenderPass2D.ScreenEffects = 750` se dessine
   sous `UI = 1000` (`RenderPass2D.cs:13-17`), et `ScreenEffectComponent.cs:147` y pousse le fondu.
   Une jauge MGUI flotterait donc au-dessus du noir pendant les 16 ticks. Trois voies, à trancher :
   **(a)** changement moteur, un mode « au-dessus de l'interface » pour l'effet d'écran, fidélité
   exacte, un chantier dans `CasaEngineMonogame/ai-agent/tasks/` ; **(b)** suppression au niveau du
   présentateur pendant le fondu de warp, le directeur intact, la jauge disparaît au premier tick au
   lieu de s'assombrir sur 16, aucun changement moteur, écart documenté ; **(c)** accepter le
   flottement, écart visible. La tranche C4 telle qu'écrite, un glissement avant le fondu, est
   abandonnée : l'original ne glisse pas au warp.

   Réserve annexe : en image normale, la translittération place le fondu sous l'interface
   (`SpriteDepth.FadeTransitionEffect = BackgroundUI - 1`, `SpriteDepth.cs:5-11`), mais ces
   constantes sont un ajout du translittérateur, pas la table d'ordonnancement PSX. Pour les fondus
   scriptés hors warp (opcode `0xAF`), l'ordre réel jauge/fondu n'est **pas établissable** depuis la
   translittération.

---

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-18 | Reconnaissance mesurée : 6 surfaces, 18 faits vérifiés indépendamment, 1 critique de complétude. Trois hypothèses de départ réfutées, voir §0. |
| 2026-09-18 | D-E13-1 à D-E13-7 arbitrées avec l'auteur. Plan rédigé. |
| 2026-09-18 | Relecture adverse de clôture : **REVISE**, un P1 et trois P2, **tous acceptés et corrigés**. (1) P1 — `g_playerStats` et `g_saveData.PlayerStats` sont le **même** objet, aliasé à `GameInitializer.cs:445` : ma mise en garde disait l'inverse, et les valeurs citées étaient fausses. §1.5 bis réécrite, acceptation de C0 retournée. (2) Les deux contournements sont sous garde `IsGodMode` : garde ajoutée, et C0 les met explicitement hors périmètre plutôt que de porter un drapeau qu'elle n'a pas. (3) D-E13-5 prescrivait encore les images clés que §1.8 bis réfute : amendée et supersédée sur ce point. (4) La fermeture ne produit **pas** l'ouverture à l'envers, la troncature entière penchant vers zéro : seconde table ajoutée en §1.3, recalculée à la main, et §4 et C3 pointent désormais une table par sens. **Deuxième REVISE consécutif : plafond atteint, pas de nouvelle soumission.** Le plan part à l'auteur avec ces corrections. |
| 2026-09-18 | Première relecture adverse : **REVISE**, quatre blocages P2, tous acceptés et corrigés. (1) Cadence du pip de magie fausse, 10 images et non 4, §1.4 corrigé et citée. (2) Acceptation de C0 pointant un objet non défini, §1.5 bis ajoutée et revérifiée ligne à ligne. (3) Les images clés MGUI interpolent, donc le verbatim n'était pas atteignable par le moyen retenu : D-E13-8 ajoutée, §1.8 bis ajoutée, C1 et C3 réécrites, arrêts élargis. (4) Les 33 tuiles viennent de deux sites d'extraction et non d'un, §0.2 corrigé. Aucun blocage écarté. |
| 2026-09-19 | **Exécution lancée** sur approbation de l'auteur, une tranche par workflow, executor sonnet, verifier opus, un commit par tranche. |
| 2026-09-19 | **C0 livrée, verifier CONFIRMED, 840/840** (815 + 25). Nouveau type `AlundraPlayerStats`, une seule instance `readonly` sur `AlundraGameState` (aliasing de §1.5 bis reproduit par construction) ; les cinq setters bornés sur `AlundraPlayerManager`, là où l'original les met, transcrits avec `0x33`/`0x32` littéraux et l'ordre des tests d'origine ; `InitializeNewGameStats` et `LoadDebugStats` pour la recette. Verifier : chaque borne comparée ligne à ligne, sonde indépendante hors dépôt sur les bords, rien d'indexé, périmètre respecté. **P3 corrigé en session principale** : cinq citations décalées d'une ligne, vérifiées de mes yeux, suite relancée. **P4 différés** : identité d'instance non testée après remise à zéro ; abaisser un plafond ne re-borne pas la valeur courante, fidèle mais non verrouillé par un test. **Reconfirmation** de l'inversion et de la cadence faite en parallèle sur le C# translittéré, voir §6 points 2 et 4 : la cadence tient, la phase des pips est une question pour l'auteur. |
| 2026-09-19 | **C1 livrée, verifier CONFIRMED, 853/853** (840 + 13). `AlundraHudDirector` sur le patron E12, états nommés par effet (`Idle` 0, `Displayed` 1, `Closing` 3, `Opening` 5), les trois branches du déclencheur dans l'ordre d'origine, la branche `0x400000` instantanée et la branche du loquet réarmée à chaque image, le tween entier avec troncature vers zéro, les quatre compteurs aux cadences de §1.4, index d'image par pip derrière la table d'unisson de §6 point 4. Accroche dans `AlundraWorldProxy` par `InstallHudSystems` et une boucle `Tick()` par tick logique, à la suite du dialogue. **Verifier** : sonde hors dépôt réimplémentant le tween depuis `UIManager.cs:968-993`, les deux tables de §1.3 retrouvées à l'entier près dans les deux sens et prouvées produites par le tween et non recopiées ; machine d'états comparée ligne à ligne à `GraphicManager.cs:1656-1672` et `HudManager.cs:223-239` ; cadences rejouées sans partager de constante. **P3 corrigés en session principale** : grappe de citations décalées dans le directeur, et résumé de documentation d'`InstallDialogueSystems` déplacé par erreur sous `InstallHudSystems`. **P4 accepté** : le test « écran modal » ouvre le directeur de dialogue plutôt qu'un vrai `IUIScreen`, la pile d'écrans n'étant pas pilotable sans tête ; la condition tient sur le fond, la boucle du HUD vivant hors de la pile. **Découverte** : bruitages 8 et 9 du rattrapage non portés, voir §6 point 5. |
| 2026-09-19 | **C4 bloquée ⚠️ avant exécution : prémisse réfutée.** Une reconnaissance haiku puis une vérification opus sur les sources d'origine du directeur de warp établissent que l'original ne ferme pas la jauge au départ d'un warp : il capture l'image et fond dessus. Voir §6 point 6 pour les lignes et les trois voies. Décision auteur demandée ; C2 et C3 continuent. |
| 2026-09-19 | **Trois décisions actées** : D-E13-9 échelle pixel dérivée du viewport (tranchée par C2) ; D-E13-10 variable d'environnement pour la recette, l'auteur ayant constaté que la jauge n'apparaît pas sur la 389, ce qui est le comportement d'origine ; D-E13-11 C4 devient un changement moteur, l'effet d'écran au-dessus de l'interface. C4 réécrite, C5 scindée en C5.a et C5.b. |
| 2026-09-19 | **C2 en troisième passe.** Deux verifiers REFUTED sur le même P2 : l'ordre de recouvrement des grands cristaux, qui se chevauchent de 8 px, corrigé pour les pleins au premier tour mais pas pour les vides. La troisième passe traite aussi une erreur du brief de la session principale, le nombre de cristaux vides doit suivre la vraie vie maximale (`HudManager.cs:767`) et non la valeur roulée, et un débordement d'une tuile observé à 50 sur 50. Verdict en attente. |
| 2026-09-19 | **C2 livrée, verifier CONFIRMED à la quatrième passe, 863/863** (853 + 10). Quatre passes, chacune matériellement différente. Passe 1 : REFUTED, ordre de recouvrement des grands cristaux pleins inversé, ils se chevauchent de 8 px. Passe 2 : REFUTED, même défaut non corrigé pour les vides. Passe 3 : REFUTED, les deux ordres exacts au sous-pixel, mais le clamp ajouté pour la rangée à 50 sur 50 n'existait pas dans l'original et le cas jugé inatteignable l'est, à pleine vie quand la vie maximale augmente. Passe 4 : clamp retiré, arithmétique de `HudManager.cs:727-782` portée sans borne, transitoire élucidé — l'original écrit un SPRT avant le tableau des cristaux, dans la seconde banque doublement tamponnée du tableau voisin, désaligné d'un en-tête, jamais dessiné comme cristal ; le composeur ne rend rien pour cet indice, avec la citation. `MaxTileCount = 26` désormais dérivé des cinq tableaux d'origine (3 + 4 + 10 + 4 + 5) ; `Debug.Assert` et compteur, plus aucune tuile jetée en silence. **Établi au passage** : parmi les quatre afficheurs, une seule lecture d'une vraie stat, `GetPlayerHpMax()` pour le nombre de cristaux vides (`:767`), tout le reste lit les valeurs roulées ; le directeur expose `TrueHpMax`. **D-E13-9 tranchée** : facteur entier dérivé du viewport. **Verifier** : les 24 index de sprite croisés avec `wind.json` et les UV, deux recaptures indépendantes à quatre pleins et à quatre vides, zéro écart au sous-pixel sur la zone des cristaux, `PointClamp` et facteur 4 confirmés. **P3 corrigé en session principale** : le commentaire justifiant l'indice négatif affirmait un trou mémoire non étiqueté ; les tableaux sont doublement tamponnés (`:760`, `:802`, pas d'adresses de `StaticVariables.cs:13148-13153`), commentaire réécrit sur ce que la source dit. **P4 rejeté** : le verifier tenait la citation `DrawSettings.cs:82` pour décalée ; vérifiée de mes yeux, la déclaration commence en 81 et le défaut `PointClamp` est bien en 82, la citation est juste. **P4 différé** : la boucle des vides n'a pas la garde de slot négatif de celle des pleins ; hors domaine, la vie maximale étant bornée à 50 par C0, et désormais attrapé par le compteur. **P3 de la passe 1, différé** : l'écran ne relit le directeur que dans son propre `Update`, gelé sous un écran modal ; c'est C3 qui fait pousser les valeurs par le tick, comme le plan l'exige. |
| 2026-09-19 | **C3 livrée, verifier CONFIRMED, 869/869** (863 + 6). `AlundraHudPresenter` sur le patron E12, tiqué depuis la boucle de ticks de `AlundraWorldProxy.Update` juste après le directeur ; il pousse visibilité, translation et tuiles recomposées dans une vue `IAlundraHudView` que `AlundraHudScreen` implémente ; l'`Update` de l'écran est désormais vide, donc un dialogue modal ne gèle plus la jauge. Translation = (Y − 16) × facteur, en entier jusqu'au `Vector2` final, le composeur cuisant `BoxY = 16` : le haut de la jauge est à Y × facteur au tick k, dans les deux sens, prouvé par deux tests à facteur 3 sur les 18 valeurs de chaque table. Aucune API d'animation MGUI appelée, figé par un test. Captures en processus à l'index 9 de l'ouverture (Y = −7, écrêté au bord) et à l'état affiché (64 = 16 × 4, exact), déclenchées sur l'état du directeur lu par réflexion et non sur un compte d'images. **Verifier** : recapture indépendante, tables recalculées, grep des API d'animation, sonde dialogue ouvert. **P3 différé** : le test à 40 ticks est une identité présentateur-composeur plus un échantillon de cadence ; les cadences elles-mêmes sont figées par les tests du directeur en C1, la couverture est transitive. **P4 différés** : la translation n'est pas semée à l'initialisation, sans effet en pratique, le câblage précédant la boucle de ticks dans le même `Update` ; le littéral `0x10` de la boîte est redéclaré dans le présentateur et les tests. |
| 2026-09-20 | **Ondulation des pips de magie livrée.** L'auteur a joué l'original et tranché le point ouvert 4 : « oui les animations sont décalées ». La table de phase de C1 passe de l'unisson à `{0, 1, 2, 3}`, soit `image = (phase + pip) % 4`, la rotation que `g_inventoryWeaponIconX` conserve. Le test qui figeait l'unisson est remplacé par un test d'ondulation qui exige les quatre pips **tous différents** à chaque instant et chacun exactement à son propre indice d'avance. 884/884. Reste à confirmer en jeu le **sens** de l'ondulation, qui dépend de quel pip porte l'indice 0 : s'il court à l'envers, les quatre constantes s'inversent, rien d'autre. |
| 2026-09-19 | **C5.a livrée, verifier CONFIRMED, 873/873** (869 + 4). `ALUNDRA_HUD_DEBUG=1` lue au site de nouvelle partie déjà documenté par T5, verrou de session, seam de test conforme au style du fichier, drapeau levé par les constantes de C1. Deux captures en processus par le seul chemin de production, sans aucune réflexion : avec la variable, la 389 avec la jauge à 38 sur 45, 2 sur 3, 2163 ; sans, la 389 sans jauge. **P3 corrigé en session principale** : le nom de la variable manquait au plan, ajouté ici et dans D-E13-10. **P4 sans action** : citation décalée dans le rapport de l'exécuteur, pas dans le code. |
| 2026-09-19 | **C6 livrée, verifier CONFIRMED, 884/884** (878 + 6). Fonction pure `ComposeEquipmentBackgrounds` dans le composeur, un enregistrement dédié `AlundraHudBackgroundQuad` et un `HudRgb` sans dépendance de rendu ; deux `MGRectangle` à `MGGradientFillBrush` ajoutés au canevas porteur avant le pool, donc dessous, suivant la translation par héritage et la visibilité par celle du canevas ; opacité par `MGElement.Opacity = 0,5`, le pinceau multipliant chaque couleur de coin par l'opacité de l'élément, l'équivalent exact de l'alpha d'`AddQuadColor`. L'exécuteur a attrapé et corrigé son propre défaut : l'ajout au canevas écrasait la position, les quads tombaient à l'origine. **Verifier** : les huit coins et un point intérieur mesurés contre le décor seul capturé au même tick, à ±6/255 du mélange prédit ; positions 64..160 et 192..288 sur 64..192 à l'échelle 4. **P3 différé, fidélité fine** : MGUI triangule sur la diagonale haut-gauche/bas-droit, la PSX sur l'autre ; coins exacts, intérieur du dégradé légèrement différent ; se corrigerait dans MGUI, à décider par l'auteur. **P4 différé** : tableau des deux rectangles conservé sans lecteur, réservé aux icônes d'E13.c. |
| 2026-09-19 | **Essai de l'auteur : jauge absente avec la variable.** Journal lu : DLL fraîche, écran câblé, aucune trace de lecture de la variable alors que le code en écrit une. Le processus du lanceur ne l'avait pas. **D-E13-12** : F1, décidée par l'auteur. |
| 2026-09-19 | **C5.a bis livrée, verifier CONFIRMED, 878/878** (873 + 5). F1 lue par le `KeyboardManager` du moteur, API publique existante, atteinte comme le directeur de caméra atteint déjà le `GamePadManager` ; état brut lu une fois par image rendue avant la boucle de ticks, front montant refait dans la DLL pour être exerçable par un seam de test. Trois branches : premier appui, jeu de débogage puis 1813 ; jauge affichée, effacement du verrou 1662 comme l'opcode `0x06`, fermeture animée prouvée sur les 18 valeurs de la table ; jauge cachée, 1813 seul. Un appui pendant une transition est ignoré et journalisé, garde nécessaire : le chemin mort d'`ArmDisappearance` accepte aussi la phase `Opening`. Chaque appui journalisé. Trois captures en processus par la bascule interne : affichée, en fermeture à Y = 5, cachée. **P3 corrigé en session principale** : deux chaînes de journal en français, passées en anglais. **P3 différé** : le test « touche maintenue dix images » ne discrimine pas une rafale ; le comportement est établi par lecture de l'idiome et par l'essai en jeu de l'auteur, test à renforcer. **P4 différés** : appuis perdus pendant un glissement, environ 0,36 s, garde justifiée ; le harnais de l'exécuteur n'appuyait que deux fois, le verifier a étendu à trois et confirmé ; le rapport de l'exécuteur niait à tort l'existence de lectures clavier dans les démos, le choix du `KeyboardManager` reste le bon. |
