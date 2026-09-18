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
| D-E13-1 | **Périmètre : la jauge permanente seule** — cœurs, magie, argent | Les écrans qui s'ouvrent, l'inventaire, les cases arme et objet sont hors périmètre |
| D-E13-2 | **Aucune cuisson** : réemployer les `.sprite` déjà exportés | Le convertisseur n'est pas touché. Pas de nouvel actif, pas de preuve d'export à produire |
| D-E13-3 | **Fidélité verbatim** de la machine d'ouverture et de fermeture | Les 18 images et la suite entière de positions sont reproduites, pas approchées |
| D-E13-4 | **MGUI porte toutes les animations**, y compris les deux cycles de 4 images | Animation2d est écarté pour E13. Aucun pont, aucun émetteur à écrire |
| D-E13-5 | **Le HUD est un élément dans une fenêtre hôte**, jamais une fenêtre | Une fenêtre n'honore pas `RenderTransform` et sa cible de translation est `internal` (§1.8). **Amendée le 2026-09-18** : la mention « animé par images clés » est retirée, supersédée sur ce point par D-E13-8. Seule la structure élément-dans-fenêtre-hôte subsiste |
| D-E13-6 | **Recette par les valeurs de débogage** de l'initialisation | Aucun script ne crédite d'argent avant E14 |
| D-E13-7 | **Le flash de palette commenté reste gelé** | Non confirmé actif, et ses tuiles intermédiaires sont absentes des pixels de `wind.png` |
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

### ⏳ C2 — Écran MGUI : la composition statique

**But** : la jauge s'affiche, juste, au bon endroit, nette.

**Contenu** : un écran non modal composant les `.sprite` déjà exportés aux décalages mesurés dans
`Fun_8004bea4`. Le HUD est un élément dans une fenêtre hôte transparente, conformément à D-E13-5.
L'écran ne fait que dessiner : le directeur lui pousse les valeurs.

**Les deux pièges de netteté** : poser `UseLinearFilteringWhenDownscaling = false` sur chaque image,
et n'agrandir que d'un facteur entier. Trancher au passage le propriétaire de l'échelle pixel, entre
`UIScaler` inutilisé et le `PixelScale` du projet.

**Acceptation** : capture en jeu par lecture du back-buffer en processus, comparée aux décalages
mesurés. Aucun filtrage linéaire.

### ⏳ C3 — Les transitions et les deux cycles, au tick

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

### ⏳ C4 — Le HUD sous le fondu

**But** : la jauge ne flotte pas au-dessus du fondu noir.

**Contenu** : l'ordre de rendu du moteur place les effets d'écran sous l'interface, donc aucun écran
MGUI ne peut passer sous le fondu. L'original ferme le HUD **avant** le fondu. Porter cet appel et le
rattacher au warp et au fondu du portage.

**Acceptation** : en jeu, la jauge se referme avant que l'écran noircisse, à un warp.

### ⏳ C5 — Recette en jeu

**Contenu** : poser les valeurs de débogage, vérifier l'affichage, le rattrapage, le roulement de
l'argent, les deux transitions, la persistance pendant un dialogue, et la fermeture avant fondu.

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
4. **La phase des quatre pips de magie — QUESTION À L'AUTEUR, seule Ghidra tranche.** La cadence
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
