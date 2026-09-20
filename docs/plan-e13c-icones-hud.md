# Plan — E13.c : les icônes d'arme et d'accessoire du HUD

**État** : 🚧 **approuvé par l'auteur le 2026-09-19**, exécution en cours.
**Naissance** : l'auteur, ayant vu la jauge en jeu, a demandé les deux cases de gauche ; la
reconnaissance a établi que leurs icônes ne sont **pas exportées** (voir `docs/plan-e13-hud.md`,
D-E13-1 amendée, et `docs/plan-conversion-totale.md` §E13). Les fonds gouraud sont la tranche C6 du
plan E13 ; ce plan-ci ne couvre que les icônes.
**Branches** : `chantier/e13-hud` dans le parent, où ce plan vit déjà, et
`chantier/e13c-portraits` dans l'analyseur, créée depuis `master` le 2026-09-19.

---

## 1. Les faits établis

Reconnaissance à trois surfaces le 2026-09-19, puis une seconde ciblée sur la chaîne d'extraction.
Tout est cité ; ce qui reste déduit est signalé.

### 1.1 D'où vient une icône dans l'original

`HudManager.DisplayHudWeaponAndItem` (`HudManager.cs:491-602`) résout l'icône de l'arme par
`GetItemIdFromCurrentWeapon()` (`PlayerManager.cs:4463-4465`) puis
`GetItemTextureIdByItemId(itemId) = g_itemsProperties[itemId * 5 + 4]`
(`GraphicManager.cs:1911-1914`), puis `GetAnimationImageByIndex(index)` qui lit
`SpriteInfo.SpriteRecords[index].GetPortraitImageset(br).Images[0]` (`:1786-1793`). L'icône est
donc **l'image « portrait » de l'enregistrement de sprite** désigné par la colonne 4 de la table des
objets, la même banque de portraits que les personnages (`MainInventoryManager.cs:486-487`).

`SpriteRecord.GetPortraitImageset` lit **toujours** l'image au pointeur `FramesPointer + 0`
(`SpriteRecord.cs:43-52`, « its the first one »), alors que les images d'animation lisent la même
table à un `ImageSetPointer` calculé (`SiFrame.cs:33, 37-38`). Une seule image, taille prise de la
source, placée aux mêmes abscisses que le fond, 16 et 48, sans décalage interne
(`GraphicManager.cs:1917-1939`, `HudManager.cs:515-524`, `:556-565`). L'icône est **reconstruite à
chaque image** ; le fond, lui, est posé une fois à l'armement.

### 1.2 Ce qui est équipé au départ

`WeaponId` (`PlayerStats.cs:9`, un `short`) n'a qu'un site d'écriture, `SetPlayerWeaponId`
(`PlayerManager.cs:1833-1845`). **Tel que translittéré**, son paramètre est un `ushort` : la
branche `weaponId == 0xffffffff` est inatteignable, et `weaponId - 1 < 6` est évalué en `int`, donc
**0 est accepté** et la plage réelle du code cité est 0 à 6. Sur la PSX, la même comparaison sur
un registre 32 bits non signé rejetterait 0 ; l'écart entre l'original et la translittération est
une **question pour l'auteur**, §6 point 4, pas un fait établi. S4 porte ce que le code cité fait,
avec ce point marqué. La nouvelle
partie pose `SetPlayerWeaponId(1)` hors du `if/else`, donc pour la partie réelle comme pour le jeu
de débogage (`GameInitializer.cs:395-410`). `GetItemIdFromCurrentWeapon` = `GetWeaponIdBySlotId(
WeaponId - 1)` (`:4433-4460`, cas 0 à 5), qui passe par `GetItemIdFromSlotId` (`:4370-4419`) sur les
objets **possédés**. Au départ, seuls les objets 1, 17 et 25 sont déverrouillés (bit haut de
`g_itemDropProperties[i].Field3`, boucle commune aux deux branches) ; parmi eux seul l'objet 1 a le
slot 1. **Résultat : itemId 1, l'épée de base, icône 0x1F = 31** (`StaticVariables.cs:739`).

`ItemId` (`PlayerStats.cs:10`) n'est jamais posé à la nouvelle partie ; `SetItemIdFromCurrentItemId`
(`:4351-4366`) rend la sentinelle quand `GetNumberOfItem` vaut 0 ; **la case d'accessoire reste
vide, fond seul** (`HudManager.cs:551-553`). Fidélité stricte décidée : la recette ne force rien.

### 1.3 La table des objets

`g_itemsProperties` (`StaticVariables.cs:729-736` pour la documentation des colonnes, tableau
`ushort[]` de **100 lignes** de 5 entre `:737` et `:838`, la ligne 0 étant l'objet 0 avec l'icône
`0xFFFF`) : colonne 0 le slot d'inventaire, 1 un drapeau de remplacement, 2 la priorité de
remplacement, 3 le nombre maximal, **4 l'icône**. La ligne de l'objet 1 :
`0x0001, 0x0001, 0x0000, 0x0001, 0x001F`. **Trois bornes distinctes**, à ne pas confondre :
100 lignes dans la table ; `g_itemsCount = 99` (`GameInitializer.cs:470`, `PlayerManager.cs:4669`) ;
`GetItemIdFromSlotId` balaie les indices 0 à 99 (`PlayerManager.cs:4382`, `while (currentIndex <
100)`) ; et **98** est la borne de la boucle de déverrouillage sur `g_itemDropProperties`
(`GameInitializer.cs:408`, `while (iconIndex < 0x62)`, dernier indice `[97]`,
`StaticVariables.cs:1234`). `g_itemDropProperties` (`:841`, champs `Field0`…`Field4` non documentés,
`Field3 & 0x80` = déverrouillé au départ). **Ces deux tables n'existent que dans la
décompilation** : rien dans `data-extracted/`.

**L'espace d'index de la colonne d'icône** : l'original indexe `SpriteInfo.SpriteRecords[index]`
par **position dans le tableau** (`GraphicManager.cs:1791`), alors que le convertisseur clé les
banques par `Sector5Id` et **ignore en silence** tout enregistrement ultérieur qui répète un
`Sector5Id` (`SpriteBankReader.cs:275-289`, `:211`). La correspondance `itemId → .sprite` ne peut se
construire qu'en passant par position → `Sector5Id` → clé de banque, et seulement si aucun
enregistrement désigné n'est un doublon ignoré. S0 le mesure.

### 1.4 Ce que l'extracteur fait, et ne fait pas

`GameMapHelper.EnumerateImages` (`AlundraDataExtractor/GameMapHelper.cs:229-269`) ne parcourt que
`SpriteRecords → AnimSets → PreloadedAnims → Frames → Images` ; **jamais `GetPortraitImageset`**.
`SaveSpriteSheet` (`:109-156`) déduplique sur `Signature` (planche + palette + source + taille) et
stampe `AtlasX/AtlasY` sur chaque `SiImage` de même signature. **Disposition par défaut
`Original`** (`Program.cs:133`) : les huit pages VRAM de 256×256 empilées, chaque image dessinée
**à sa propre fenêtre VRAM**, position `(SourceX, (Spritesheet & 7) * 256 + SourceY)` (`:157-174`).
Ajouter des images n'en **déplace** donc aucune autre. **Mais la position ignore la palette alors
que la signature l'inclut** : deux signatures distinctes peuvent partager une cellule, et l'ordre
de dessin est celui de première rencontre, « la dernière dessinée gagne et les autres découpent la
mauvaise couleur », dit le code lui-même (`GameMapHelper.cs:162-167`, `:137-142`). Un portrait à une
autre palette sur la fenêtre d'un sprite déjà exporté **repeindrait** ce sprite sans qu'un diff de
fichiers le voie. Et comme chaque signature est dessinée **entière à sa propre origine et à sa
propre taille** (`:174`, `:137-141`), deux signatures se recouvrent **partiellement** sans jamais
partager une cellule exacte : le test de collision est une **intersection de rectangles par page**,
`(Spritesheet & 7, [SourceX, SourceX + Swidth) × [SourceY, SourceY + Sheight))`, contre toute
signature existante. Une intersection **à même palette est bénigne**, les mêmes texels VRAM décodés
avec la même palette donnent les mêmes pixels (`GameMap.cs:139-152`) ; une intersection **à palette
différente** est l'arrêt de D-E13C-3. Enfin **l'ordre de dessin est celui de première rencontre**
(`:111-124`, `:137-141`) : les portraits doivent entrer **strictement après** toute la séquence
d'`EnumerateImages`, sans quoi une signature déjà produite par les animations changerait de rang et
pourrait renverser le gagnant d'une cellule partagée. S0 devait mesurer les intersections, S1 figer l'ordre,
S2 et S3 prouver au pixel. **Tout cela est devenu sans objet** : S1.a a mesuré que les 88 portraits
ont une signature **déjà présente** dans la planche, donc aucun rectangle nouveau, aucune
intersection, aucun pixel ajouté. Cette section reste pour expliquer pourquoi le régime de preuve
était si lourd, et pourquoi il ne l'est plus.

`SiImage` (`SiImage.cs:5-22`) n'a **aucun marqueur** « portrait ». `SiImageSet` sait construire un
jeu de portrait (`isPortrait: true`, `SiImageSet.cs:11, 18-21`, une seule image).

L'extracteur se lance depuis `AlundraTools/AlundraTools` (résolution d'`EntityNames.csv`) :
`dotnet ..\AlundraDataExtractor\bin\Debug\net9.0-windows\AlundraDataExtractor.dll "<extraction du
disque>" "D:\development\repo\Alundra Remake\remaster-data-extracted"`
(`alundra-datas-analyser/docs/alundra-tiled-map-exporter-usage.md:5-27`). Le chemin d'entrée réel
est dans le `launchSettings.json` de l'extracteur, à relire en S0.

### 1.5 Le miroir et la preuve d'export

`data-extracted/` du dépôt est une **copie** de `Alundra Remake/remaster-data-extracted`,
rafraîchie par `robocopy <remaster> <data-extracted> /MIR` **depuis PowerShell** (Git Bash casse
les chemins avec espace), identité prouvée par `diff -rq`. L'export du convertisseur est **en place**,
jamais précédé d'une suppression manuelle d'`alundra-project/` ; il se prouve par manifeste :
baseline capturé **avant** la modification, diff prédit écrit **avant** l'export, diff mesuré inclus
dans le prédit, puis double export dont le diff est inclus dans `{report.json}`. Un fichier
chargeable sans entrée de catalogue est une **erreur** sur un run complet (`AssetVerifier.cs:96-106`,
D-N-3). Jamais d'export pendant que `Alundra.Tests` tourne.

### 1.6 Les précédents à copier

- **Table de la décompilation vers le convertisseur** : `MapSoundGroupIndex.csv` dans
  `alundra-datas-analyser/AlundraTools/AlundraTools/`, généré depuis le tableau décompilé, lu par
  `MapSoundGroupIndexCatalogReader.cs:1-54` (séparateur `;`, en-tête ligne 0), republié **brut** en
  `Maps/sound-group-index.json` par `WorldWriter`, sans interprétation, « linked, not copied ».
- **Émission de sprites** : `SpriteWriter.cs:16-106`, un `.sprite` par `(planche, Signature)`
  unique, identifiants déterministes `Ids.For(<clé stable>)` (D-N-6, `:98-105`), un prefab par banque
  sous `Entities/<nom>/`. Le convertisseur donne **déjà** un prefab à chaque enregistrement de la
  banque globale (`SpriteBankReader.cs:223-250`), avec le seul sprite « posé au sol ».
- **Chargement par identifiant d'actif dans le HUD** : C2 charge chaque glyphe par
  `AssetContentManager.Load<SpriteData>` sur un identifiant de `.sprite` (`plan-e13-hud.md`, C2) ;
  la composition ne suppose pas une texture unique.

### 1.7 Ce que la DLL n'a pas

`AlundraPlayerStats` n'a que cinq champs ; sa doc dit que `WeaponId`, `ItemId`, `FalconTemp` et
`Falcon` ne sont pas portés (`AlundraPlayerStats.cs:9-10`). Aucun compteur d'objets, aucune table
d'objets, aucune résolution d'icône. Le composeur de C2 ne connaît que 24 glyphes statiques.

---

## 2. Décisions verrouillées

| Réf | Décision | Conséquence |
|---|---|---|
| D-E13C-1 | **Tous les portraits des objets dont la colonne d'icône est renseignée** sont extraits (arbitrage auteur du 2026-09-19) | Une passe déterministe ; l'inventaire (E13.d) en héritera sans ré-extraction. S0 établit la convention « pas d'icône » dans la colonne 4 avant de compter |
| D-E13C-2 | **La table des objets entière** (5 colonnes) et le drapeau de déverrouillage au départ sortent de la décompilation par **CSV dans l'analyseur**, sur le précédent `MapSoundGroupIndex.csv`, et le convertisseur les republie **bruts** en JSON | E13.c n'a besoin que de la colonne 4 pour l'icône, mais la résolution fidèle de l'arme équipée lit les colonnes 0 à 3 et les objets possédés ; E13.d aussi |
| D-E13C-3 | **La disposition d'atlas reste `Original`** ; S0 prouve qu'aucune image existante ne bouge et qu'aucun rectangle de portrait **n'intersecte, sur sa page, une signature existante de palette différente** ; une intersection à même palette est autorisée, texels identiques (§1.4) ; **les portraits sont énumérés strictement après la séquence existante**, l'ordre de première rencontre des signatures existantes étant inchangé | Si S0 mesure un déplacement, une intersection à palette différente, ou si S1 change le rang d'une signature existante, arrêt : chacun repeindrait un sprite déjà exporté sans qu'un diff de fichiers le voie |
| D-E13C-4 | **Un `.sprite` par portrait**, clé déterministe `(planche, Signature)` comme tout sprite (D-N-6), catalogué (D-N-3), plus un JSON brut `Data/item-icon-index.json` reliant chaque `itemId` à l'identifiant de son `.sprite`, **la correspondance passant par la position dans `SpriteRecords` puis le `Sector5Id`** (§1.3) | Le HUD charge l'icône par identifiant d'actif, comme les 24 glyphes de C2. La déduplication des sprites est **globale entre banques** et le fichier vit sous le dossier de la **première** banque qui a rencontré la signature (`SpriteWriter.cs:151, :819-839`) : un portrait déjà émis ailleurs n'est pas un nouveau fichier, et peut en **déplacer** un existant sans changer son identifiant ; S0 le mesure, S3 le prédit |
| D-E13C-5 | **La ré-extraction et le miroir sont exécutés ou explicitement autorisés par l'auteur** : ils écrivent hors du dépôt, dans `Alundra Remake/remaster-data-extracted` | Aucun agent ne lance l'extracteur ni `robocopy` de lui-même |
| D-E13C-6 | **La DLL porte la chaîne fidèle** : `WeaponId` et sa règle de validité, les compteurs d'objets, la boucle de déverrouillage de la nouvelle partie, `GetWeaponIdBySlotId` et `GetItemIdFromSlotId`, puis la colonne d'icône | Pas de raccourci « arme 1 donc icône 31 » : ce socle est aussi celui d'E13.d |
| D-E13C-7 | **Fidélité stricte pour la case d'accessoire** : vide au départ, la recette ne force aucun objet (arbitrage auteur) | La seconde case ne se valide à l'œil qu'après E13.d |
| D-E13C-8 | **Aucun changement moteur.** L'icône est une image MGUI de plus dans l'écran de C2, au-dessus du fond de C6 | Si un besoin moteur apparaît, arrêt et retour à l'auteur |

---

## 3. Tranches

Un committeur par dépôt, ordre strict, une tranche = un commit + un verifier frais. Trois dépôts :
l'analyseur (S1), le parent avec le convertisseur (S3) et la DLL (S4). Chaque dépôt sur sa branche.

### ✅ S0 — La mesure préalable (lecture seule) — faite le 2026-09-19

**Résultats mesurés**, table à jour, tout vérifié en session principale sur les données réelles :

| Fait | Valeur | Source |
|---|---|---|
| Lignes de `g_itemsProperties` | **100** | `StaticVariables.cs:737-838` |
| `g_itemsCount` | **99** | `GameInitializer.cs:470` |
| Borne de la boucle de déverrouillage | **0x62 = 98**, dernier indice 97 | `GameInitializer.cs:408` |
| Convention « pas d'icône » | **`0xFFFF` seule**, 11 lignes : 0 et 90 à 99 | `StaticVariables.cs:738`, `:828-837` |
| Lignes à icône | **89**, contiguës : `icône = itemId + 30` | `:739` (0x1F), `:827` (0x77) |
| Emplacements d'enregistrements | 257, dont **151 remplis** et 106 nuls | `map_alundra.json` |
| Position contre `Sector5Id` | **identiques**, 0 divergence | mesuré sur les 151 |
| `Sector5Id` dupliqués | **0**, donc aucun enregistrement ignoré | `SpriteBankReader.cs:275-289` |
| Clé de banque | `alundra_<Sector5Id>` | `SpriteBankReader.cs:211`, `:276` |
| Signatures uniques déjà dans la planche | **693** | ordre d'`EnumerateImages` |
| Digest de l'ordre de première rencontre | `88616994556bab06a847ba03e37832cfeb1cdc49` | SHA-1 des 693 signatures dans l'ordre |
| Recouvrements existants, même palette | **205** | entre signatures existantes |
| Recouvrements existants, palettes différentes | **225** | idem, **état de base**, voir ci-dessous |
| Planche | **256 × 2048**, taille **constante** | `GameMapHelper.cs:177`, `:226-227` |
| SHA-1 de la planche de référence | `879197ed1695d366fa2ccf720e9826da601c5799` | copie dans le scratchpad |
| Entrée de l'extracteur | `…\Alundra (France)\Alundra (France)_extracted` | `launchSettings.json:5` |

**Stabilité de la disposition : GO, prouvée par le code.** `GameMapHelper.cs:172-175` pose chaque
signature à sa seule origine VRAM, sans curseur ni accumulateur, contrairement à la disposition
compacte (`:197-212`) ; et la planche a une taille fixe (`:177`). Ajouter une image n'en déplace
aucune et ne redimensionne pas la planche, donc aucun décalage de fichier ne bouge.

> **La règle d'arrêt sur les palettes, corrigée par la mesure.** La planche porte **déjà** 225
> recouvrements à palettes différentes entre images existantes : « le dernier dessiné gagne » est son
> régime normal, documenté par l'extracteur lui-même. Arrêter sur toute intersection à palette
> différente aurait arrêté le chantier sur l'état de base. Ce qui compte est qu'un **portrait**,
> dessiné en dernier, **change le gagnant** d'une cellule déjà occupée. D'où la règle effective : un
> portrait ne doit intersecter aucune signature existante, sauf à palette identique, où les texels
> sont les mêmes. Les recouvrements entre existantes ne bougent pas tant que le préfixe d'ordre est
> préservé, ce que S1.a prouve par le digest ci-dessus.

**Deux blocages remontés à l'auteur, voir §6 points 5 et 6** : l'objet 42 désigne un enregistrement
nul, et les rectangles des portraits ne sont pas mesurables avant l'instrumentation de l'extracteur,
ce qui impose de scinder S1.

### ✅ S1.a — Relevé des portraits depuis le binaire — faite le 2026-09-19

**Le relevé.** Une commande de relevé ajoutée à l'extracteur derrière l'argument dédié
`--probe-portraits`, sur le précédent des arguments existants `--trace-bgm` et `--extract-movies`
(`AlundraDataExtractor/Program.cs:83-103`). Elle ouvre le binaire, appelle `GetPortraitImageset` pour
les 89 valeurs d'icône et écrit son relevé **hors dépôt**. Elle ne modifie aucune sortie
d'extraction : ni `data-extracted/`, ni le dossier du remaster, prouvé par les dates de leurs
fichiers. C'est un instrument de mesure, il reste dans l'analyseur pour que la mesure soit refaisable.

**Ce qu'elle a mesuré, recalculé indépendamment par un verifier :**

| Mesure | Valeur |
|---|---|
| Icônes sondées | 89, valeurs 31 à 119 |
| Portraits exploitables | **88** |
| Signatures distinctes | **85**, trois icônes partagent un portrait |
| Signatures **déjà** parmi les 693 de la planche | **88 sur 88** |
| Rectangles nouveaux | **0** |
| Intersections à palette différente | **0** |
| Verdict de D-E13C-3 | **GO** |

**Et le fait qui change le chantier** : chaque signature de portrait a **déjà son fichier `.sprite`
émis** par le convertisseur, 85 sur 85, toutes sous `alundra-project/Entities/`. Vérifié en session
principale sur les 6908 `.sprite` du projet. L'objet 1, l'épée de base, a pour portrait exactement
son sprite du monde, `sprite_34187962752519.sprite`, même signature.

> **Correction d'une affirmation de la reconnaissance.** Elle soutenait que le portrait, lu à
> `FramesPointer + 0`, était « une image distincte, absente de tout frame converti », et que
> l'extraction ne le portait pas. La mesure dit le contraire : pour ces objets, le portrait coïncide
> avec une image déjà atteinte par le parcours des animations. La reconnaissance raisonnait sur la
> structure, la sonde a lu le binaire.

> **Correction du point ouvert 5, l'objet 42.** La sonde a lu l'emplacement 72 dans le binaire :
> `SpriteInfo.cs:93-101` ne construit un enregistrement que si son entrée de table vaut autre chose
> que 0 ou -1, et à cet indice la condition est fausse. **L'enregistrement n'existe pas dans les
> données du jeu** ; ce n'est pas l'extraction qui l'aurait omis. L'objet 42 n'a donc réellement pas
> d'icône, ce qui rejoint l'arbitrage de l'auteur : on livre les 88.

**Conséquence, actée** : l'extraction, la ré-extraction, le miroir et la preuve au pixel de la
planche **n'ont plus d'objet**. Les tranches S1.b, S2 et S3 d'origine sont remplacées par les deux
ci-dessous. D-E13C-2 sur les CSV, D-E13C-4 sur la correspondance et D-E13C-6 sur la chaîne fidèle
tiennent ; D-E13C-1 se lit désormais « les 88 portraits exploitables » ; D-E13C-3 et D-E13C-5 sont
**sans objet**, leur mesure ayant rendu GO et aucune ré-extraction n'étant nécessaire.

### ✅ S1.b — Analyseur : les deux tables en CSV — faite le 2026-09-19

**But** : ce que seule la décompilation sait sort de l'analyseur, comme les précédents.

**Contenu** : deux fichiers dans `AlundraTools/AlundraTools/`, liés au projet comme
`MapSoundGroupIndex.csv`, générés depuis les tableaux décompilés et **bruts**, sans interprétation :
- `ItemsProperties.csv`, séparateur `;`, en-tête ligne 0, **100 lignes**, colonnes
  `item_id;slot;replace_flag;priority;max_count;icon`.
- `ItemPortrait.csv`, `item_id;icon;signature`, **88 lignes**, la signature venant du relevé de S1.a.
  C'est elle qui relie un objet à un `.sprite` déjà émis, et non le dossier de son prefab : l'objet
  89 a son portrait sous `Entities/I34_Objet 034/`, parce que la déduplication des sprites est
  globale entre banques et que le fichier vit sous la première banque qui a rencontré la signature.

**Acceptation** : les deux CSV relus contre `StaticVariables.cs:737-838` et contre le relevé ;
`ItemPortrait.csv` ne contient aucune signature absente des 85 mesurées ; l'objet 42 est absent.

### ✅ S2 — Convertisseur : les deux catalogues — faite le 2026-09-20

**But** : le projet exporté porte la table des objets et la correspondance objet vers sprite.

**Ce qui a été fait.** Deux lecteurs sur le modèle de `MapSoundGroupIndexCatalogReader.cs`
(`ItemsPropertiesCatalogReader.cs` et `ItemPortraitCatalogReader.cs`), les deux CSV liés au csproj
« linked, not copied », un `ItemsWriter` appelé en `Phase6.Items`, et **deux extractions pures** qui ne
changent aucun comportement : le littéral de la planche d'Alundra devient
`SpriteBankReader.AlundraSpritesheetFileName` (`SpriteBankReader.cs:230`), et la formule
d'identifiant déterministe devient `SpriteWriter.SpriteAssetId`. **La signature de `ConvertSprites`
n'a pas bougé**, donc aucun fichier de test existant n'a été touché.

**Le fait qui a réduit la tranche** : toutes les banques lues dans `map_alundra.json` partagent **une
seule planche**. La clé de déduplication d'un portrait est donc `(map_alundra_spritesheet.png,
signature)`, sans ambiguïté — y compris pour les quatre objets dont le `.sprite` vit sous une autre
banque que celle de leur icône (12, 57, 71 et 89), la déduplication étant globale et le fichier
appartenant à la première banque rencontrée.

**Ce qui n'est jamais supposé** : l'identifiant recalculé n'est publié que si le catalogue le porte
**et** que l'entrée s'appelle `sprite_<signature>`. Un identifiant que nul sprite n'aurait enregistré
devient une erreur du rapport, jamais une entrée fantôme.

**Formes livrées**, arbitrées par l'auteur le 2026-09-20 : `Data/items-properties.json` est un tableau
de **100 tableaux de 5**, l'index externe étant l'`item_id` et l'ordre interne celui des colonnes de
l'original ; `Data/item-icon-index.json` est un objet à **88 entrées**, clé `item_id` en décimal
croissant, valeur l'identifiant d'actif du `.sprite`, **objet 42 absent**.

**Mesuré**

| Preuve | Résultat |
|---|---|
| Build, suite du convertisseur | vert, **167/167**, aucun test existant modifié |
| `Alundra.Tests` | **884/884** — le 888 du chantier inclut les 4 tests de C4, qui vivent sur sa branche |
| Baseline, capturé avant toute modification | **23195 entrées** (23198 fichiers moins `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`) |
| Diff prédit, écrit avant l'export | les deux JSON ajoutés, `report.json` modifié, `AssetInfos.json` toléré |
| Diff mesuré | **2 ajouts, 0 suppression, 1 modification : `report.json`** — ⊆ prédit |
| `AssetInfos.json` | **inchangé au bit près** : la tranche n'enregistre aucun actif |
| `.sprite` et PNG touchés | **0 et 0** |
| Double export | diff ⊆ `{report.json}` (D-N-7) |
| Rapport | **0 erreur**, `Items.PropertiesRows` 100, `Items.IconsIndexed` 88, `Items.IconsUnresolved` 0 |
| Vérification interne | PASSED, 19505 chargés, 2378 vérifiés par existence |
| **Appariement, indépendant de la formule** | **88/88** : l'identifiant publié désigne une entrée dont le nom de fichier porte la signature de **cette** ligne |
| Signatures partagées | les trois paires (10/12, 21/57, 34/89) rendent bien **un seul** identifiant |
| `items-properties.json` contre le CSV | 100 × 5, **0 écart** ; ligne 0 = `[0,0,0,0,65535]`, ligne 1 = `[1,1,0,1,31]` |

> **Pourquoi l'appariement se prouve sans recalculer la formule.** Le nom d'un `.sprite` est
> `sprite_<signature>` (`SpriteWriter.cs:830`). Rapprocher l'identifiant publié de ce nom relie donc
> chaque objet à **sa** signature. Une colonne croisée, un dictionnaire clé par `icon` au lieu de
> `item_id`, ou un décalage de ligne échouent, là où un simple « l'identifiant existe » passait.

### ⏳ S3 — DLL : l'arme équipée et son icône dans la case

**But** : F1 en jeu montre l'épée de base dans la case de gauche, sur son fond de C6.

**Contenu** : `WeaponId` sur `AlundraPlayerStats` avec `SetPlayerWeaponId` et sa règle (§1.2 et le
point ouvert 7) ; compteurs d'objets et boucle de déverrouillage de la nouvelle partie, objets 1, 17
et 25 ; `GetWeaponIdBySlotId` et `GetItemIdFromSlotId` portés ligne à ligne ; lecture des deux JSON ;
le composeur émet une tuile d'icône dynamique à (16, 16) natif, taille prise du sprite, au-dessus du
fond ; l'écran charge le `.sprite` par identifiant, comme les 24 glyphes de C2. La case d'accessoire
reste vide, fond seul (D-E13C-7).

**Acceptation** : tests sur la règle du setter, sur la chaîne arme vers objet vers icône à la
nouvelle partie, 1 vers 1 vers 31, sur la sentinelle sans arme, sur la case d'accessoire vide ;
suite verte ; capture en processus F1 montrant l'icône aux bonnes coordonnées, nette, sur son fond.

### ⏳ S4 — Recette en jeu

**Contenu** : F1, l'épée de base apparaît dans la case de gauche ; la case de droite reste un fond
vide ; les deux suivent le glissement.

## 4. Acceptation d'ensemble

E13.c est close quand : le double export du convertisseur est prouvé et se limite aux deux JSON, au
catalogue d'actifs et au rapport ; en jeu, F1 montre l'icône de l'épée de base sur son fond, nette,
qui glisse avec la jauge ; suites `Alundra.Tests` et convertisseur vertes.

## 5. Arrêts

- S1.b : une signature du relevé absente des 85 mesurées, ou une ligne de CSV qui ne correspond pas à
  `StaticVariables.cs` → arrêt, le CSV est la source de vérité du convertisseur.
- S2 : diff mesuré hors du prédit, en particulier **tout `.sprite` créé ou déplacé, ou toute
  modification de la planche** : aucun n'est attendu, chacun signifierait que l'export fait autre
  chose que ce que la mesure annonce → arrêt.
- S3 : un besoin moteur → arrêt (D-E13C-8).

## 6. Points ouverts

1. La convention « pas d'icône » de la colonne 4 : la ligne 0 porte `0xFFFF` ; S0 confirme que
   c'est la seule valeur d'absence sur les 100 lignes.
2. Le format exact du portrait dans `map_alundra.json` : champ sur l'enregistrement, ou image
   marquée dans la liste ? S1 choisit, en visant la plus petite surface pour `SpriteBankReader`.
3. `g_itemDropProperties` : n'exporter que le drapeau de déverrouillage, ou les cinq champs ? S1,
   après lecture de ce que `GetItemIdFromSlotId` consomme réellement.
5. **L'objet 42 désigne un enregistrement nul — QUESTION POUR L'AUTEUR, bloquante pour lui seul.**
   Mesuré : `StaticVariables.cs:780` donne à l'objet 42 l'icône `0x48 = 72`, or l'emplacement 72 des
   enregistrements de sprites est **nul** dans `map_alundra.json`, au milieu d'une plage pourtant
   remplie (71 et 73 existent). Les 88 autres icônes désignent chacune un enregistrement existant.
   Deux lectures possibles, non tranchables sans Ghidra ou sans le `.BIN` : soit l'enregistrement
   n'existe pas dans le jeu et l'objet 42 n'a réellement pas d'icône, soit l'extracteur ne l'a pas
   peuplé et l'icône existe. **88 portraits sûrs, le 89e en suspens.**
6. **Le relevé des portraits doit précéder le test d'intersection — corrigé par le découpage.** Les
   rectangles des portraits n'existent que dans le `.BIN` : S1.a les relève avant que la mesure
   décisive ne puisse s'exécuter. Le plan initial demandait cette mesure à S0, c'était impossible.
7. **`SetPlayerWeaponId` et la valeur 0 — question pour l'auteur.** La translittération accepte 0
   (§1.2) là où l'original, comparaison non signée sur 32 bits, le rejetterait probablement. À
   vérifier dans Ghidra sur `0x8004ddf4` et voisins avant que S4 ne fige la règle ; en attendant S4
   porte le code cité et marque la ligne.
8. **`g_itemDropProperties` ne sort d'AUCUN des deux CSV — à traiter avant S3.** Le drapeau de
   déverrouillage de la nouvelle partie (`Field3 & 0x80`, `StaticVariables.cs:841`, `:1234`) n'est
   ni dans `ItemsProperties.csv` ni dans `ItemPortrait.csv`, alors que S3 en a besoin pour porter la
   boucle de déverrouillage **sans raccourci** (D-E13C-6). Arbitrage de l'auteur du 2026-09-20 :
   **S2 reste à deux catalogues** ; le manque se comble avant S3 par une tranche analyseur (un
   troisième CSV) puis une extension du convertisseur sur le modèle exact de S2.

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-19 | Reconnaissance à trois surfaces (original, exports, MGUI) puis ciblée sur la chaîne d'extraction. Quatre arbitrages de l'auteur : E13.c avant E13.d, tous les portraits à icône, inventaire principal d'abord pour E13.d, fidélité stricte pour l'accessoire. Plan rédigé. |
| 2026-09-19 | Première relecture adverse : **REVISE**, un P1 et quatre P2, tous acceptés. (1) P1 — la disposition `Original` place une image à sa fenêtre VRAM sans la palette alors que la signature l'inclut : un portrait pourrait repeindre un sprite exporté sans qu'un diff de fichiers le voie ; §1.4 corrigé, S0 mesure les collisions, D-E13C-3 en fait un arrêt, S2 et S3 exigent une preuve au pixel hors des rectangles ajoutés. (2) La table a 100 lignes et non 98, `g_itemsCount` vaut 99, 98 est la borne de la boucle de déverrouillage ; §1.3 corrigé, S0 relève les trois bornes. (3) L'original indexe les enregistrements par position, le convertisseur par `Sector5Id` en ignorant les doublons ; S0 mesure la correspondance, arrêt en cas de divergence. (4) `SetPlayerWeaponId` translittéré prend un `ushort`, la sentinelle est inatteignable et 0 est accepté ; §1.2 restaté, question auteur en §6 point 4. (5) Le diff prédit omettait le catalogue d'actifs et les déplacements de `.sprite` par déduplication globale entre banques ; S3 les prédit, S0 les mesure. |
| 2026-09-19 | **S1.a exécutée, verifier CONFIRMED, et elle réduit le chantier de moitié.** Une commande de relevé derrière `--probe-portraits` lit le binaire et mesure les 89 icônes : 88 portraits exploitables, 85 signatures distinctes, **toutes déjà présentes** parmi les 693 de la planche, donc zéro rectangle nouveau et zéro intersection. Vérifié ensuite en session principale : **les 85 ont déjà leur `.sprite` émis**, sur les 6908 du projet. L'extraction, la ré-extraction, le miroir et la preuve au pixel n'ont plus d'objet ; S1.b, S2 et S3 d'origine sont remplacées par trois tranches : deux CSV, deux catalogues, la DLL. **Deux corrections de fond** : la reconnaissance affirmait que le portrait était une image distincte absente de tout frame converti, la mesure montre qu'il coïncide avec une image déjà extraite, l'épée de base ayant pour portrait exactement son sprite du monde ; et l'emplacement 72 n'existe pas dans les données du jeu, `SpriteInfo.cs:93-101` ne construisant un enregistrement que si son entrée de table n'est ni 0 ni -1, donc l'objet 42 n'a réellement pas d'icône. |
| 2026-09-19 | **Approuvé par l'auteur. S0 exécutée**, partie en agent en lecture seule, partie en session principale sur les données réelles. Trois bornes confirmées, convention d'absence d'icône établie, position et identifiant de secteur identiques sans doublon, 693 signatures existantes avec leur digest d'ordre, planche de taille constante et sa référence copiée. **Trois corrections au plan** : la règle d'arrêt sur les palettes était trop large, la planche portant déjà 225 recouvrements à palettes différentes entre images existantes ; le relevé des portraits doit précéder le test d'intersection, d'où le découpage de S1 en S1.a et S1.b ; et l'objet 42 désigne un enregistrement nul, question remontée à l'auteur. |
| 2026-09-20 | **S2 exécutée, et une mesure l'a simplifiée avant qu'une ligne ne soit écrite.** Toutes les banques de `map_alundra.json` partageant une seule planche, `Ids.For("sprite:map_alundra_spritesheet.png:<signature>")` résout **88/88** contre l'`AssetInfos.json` réel : la correspondance se lit, elle ne s'extrait pas. Première relecture adverse : **REVISE**, trois P2, tous acceptés. (1) Changer le type de retour de `ConvertSprites` cassait quatre fichiers de tests existants que la portée ne listait pas — **conception changée** plutôt que rapiécée : la signature n'est plus touchée, le writer recalcule l'identifiant et le **prouve** contre le catalogue. (2) `banks.First(b => b.IsAlundraBank)` levait une exception sur les fixtures à `SpriteRecords` vide — disparu avec la conception. (3) L'acceptation pouvait passer alors que chaque objet pointait un sprite existant mais **faux** — remplaçee par une preuve d'appariement indépendante de la formule, par le nom de fichier. Relecture de clôture : **READY**. Export prouvé : diff mesuré = deux JSON + `report.json`, `AssetInfos.json` inchangé, zéro `.sprite`, double export ⊆ `{report.json}`. Trois arbitrages de l'auteur : branche neuve depuis `main`, `items-properties.json` en 100 tableaux de 5, S2 reste à deux catalogues (point ouvert 8). |
| 2026-09-19 | Relecture de clôture : **REVISE**, un P1 et un P2, tous deux acceptés. (1) P1 — le test de collision par égalité de cellule manquait les recouvrements partiels, chaque signature étant dessinée entière à sa propre origine et taille ; et l'acceptation au pixel exemptait justement les rectangles où le dommage tombe. Corrigé : intersection de rectangles par page contre toute signature existante, intersection à même palette autorisée avec sa justification, à palette différente arrêt ; ensemble exempté redéfini comme les seuls rectangles des portraits nouveaux et sans intersection, comparaison sur toute la planche. (2) P2 — l'ordre de dessin est celui de première rencontre et n'était pas figé ; S1 concatène les portraits strictement après la séquence existante, S0 relève les rangs, S1 prouve le préfixe inchangé, arrêt ajouté. **Deuxième REVISE consécutif, plafond atteint : disposition en session principale, pas de nouvelle soumission.** Le plan part à l'auteur pour approbation avec ces corrections. |
