# Plan — E13.c : les icônes d'arme et d'accessoire du HUD

**État** : ⏳ rédigé le 2026-09-19, en attente d'approbation. Aucune ligne de code écrite.
**Naissance** : l'auteur, ayant vu la jauge en jeu, a demandé les deux cases de gauche ; la
reconnaissance a établi que leurs icônes ne sont **pas exportées** (voir `docs/plan-e13-hud.md`,
D-E13-1 amendée, et `docs/plan-conversion-totale.md` §E13). Les fonds gouraud sont la tranche C6 du
plan E13 ; ce plan-ci ne couvre que les icônes.
**Branches** : `chantier/e13c-icones-hud` dans le parent, l'analyseur (`alundra-datas-analyser`,
sous-module, branche depuis `master`) et le convertisseur, chacune créée à sa première tranche.

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
pourrait renverser le gagnant d'une cellule partagée. S0 mesure les intersections, S1 fige l'ordre,
S2 et S3 exigent une preuve au pixel sur toute la planche.

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

### ⏳ S0 — La mesure préalable (lecture seule)

**But** : ne dimensionner qu'après avoir compté.

**Contenu** : (1) relever les **trois bornes** de §1.3 avec leurs lignes : lignes de la table,
`g_itemsCount`, entrées de déverrouillage ; lire la colonne 4 pour **toutes** les lignes et établir
la convention « pas d'icône » (la ligne 0 porte `0xFFFF`, à confirmer sur l'ensemble) ; compter les
portraits à extraire ; (2) pour chaque valeur d'icône : la **position** dans `SpriteRecords`, le
`Sector5Id` de cet enregistrement, la clé de banque du convertisseur, et si ce `Sector5Id` est
dupliqué dans `map_alundra.json`, donc ignoré par `SpriteBankReader` ; (3) pour chaque portrait :
`Signature`, fenêtre VRAM, palette, taille ; marquer **nouvelle** ou **déjà présente** dans la planche
et, si présente, quel dossier de banque possède aujourd'hui son `.sprite` ; et surtout **toute
signature existante dont le rectangle intersecte, sur la même page, celui du portrait**, en
distinguant même palette, autorisée, et **palette différente, arrêt** (§1.4) ; ainsi que le rang de
première rencontre de chaque signature existante, pour que S1 prouve qu'il ne change pas ;
(4) confirmer sur `CreateOriginalSpriteSheetLayout` qu'une image supplémentaire ne déplace aucune
autre ; (5) relire le `launchSettings.json` de l'extracteur pour le chemin d'entrée réel ;
(6) capturer le **manifeste baseline** d'`alundra-project/`, une copie de la planche
`map_alundra_spritesheet.png` de référence, et un `diff -rq` de `data-extracted/` contre le
miroir, avant toute modification.

**Acceptation** : une table dans ce plan portant, par icône, position / `Sector5Id` / clé de banque
/ doublon, et par portrait, signature / nouvelle ou présente / dossier possesseur / intersections
à même palette et à palette différente ; les trois bornes citées ; la preuve de stabilité de
l'atlas ; l'ordre de première rencontre des signatures existantes ; le manifeste et la planche
baseline datés. **Arrêts** : atlas instable ; position et `Sector5Id` qui divergent, ou un
enregistrement désigné qui est un doublon ignoré ; une intersection à palette différente → retour à
l'auteur.

### ⏳ S1 — Analyseur : l'extracteur lit les portraits et exporte la table des objets

**But** : après ré-extraction, `map_alundra.json` porte le portrait de chaque objet à icône et la
planche en contient les pixels ; la table des objets sort en CSV.

**Contenu** : un marqueur portrait sur `SiImage` (ou une collection dédiée sur `SpriteRecord`,
sérialisée), une énumération sœur d'`EnumerateImages` qui visite `GetPortraitImageset` pour les
enregistrements désignés par la colonne 4, **concaténée strictement après** la séquence existante
d'`EnumerateImages` pour que chaque signature existante garde son rang de première rencontre, donc
sa place dans l'ordre de dessin (§1.4) ; `SaveSpriteSheet` les inclut sous la disposition
`Original` ; deux CSV générés depuis les tableaux décompilés, `ItemsProperties.csv` (5 colonnes) et
`ItemDropProperties.csv` (le drapeau de déverrouillage au moins), liés au projet comme
`MapSoundGroupIndex.csv`. Tests si l'analyseur en a pour l'extracteur ; sinon, le dire.

**Acceptation** : build de l'extracteur ; un run **à blanc vers un dossier temporaire hors dépôt**
montre les nouveaux portraits dans le JSON et la planche, et **aucune autre différence** contre le
miroir actuel ; le préfixe d'ordre de première rencontre des signatures existantes est **inchangé**,
relevé contre celui de S0 ; et la planche produite est identique au pixel à la planche baseline
**hors de l'ensemble exempté** défini en S2. Branche analyseur, remote vérifié avant tout push par l'auteur.

### ⏳ S2 — Ré-extraction et miroir (par l'auteur, D-E13C-5)

**Contenu** : l'auteur lance l'extracteur vers le remaster, puis `robocopy … /MIR` depuis PowerShell
vers `data-extracted/`, puis `diff -rq` ; l'agent relit le diff.

**Acceptation** : le diff de `data-extracted/` se limite à `map_alundra.json` et
`map_alundra_spritesheet.png`, et aux seuls ajouts prédits par S0 ; **et une comparaison au pixel
sur toute la planche** contre la planche baseline de S0 montre **chaque pixel identique, sauf ceux
de l'ensemble exempté** : les rectangles des signatures de portrait que S0 a marquées **nouvelles et
n'intersectant aucune signature existante**. Un portrait déjà présent n'ajoute aucun pixel ; un
portrait qui intersecte une signature à même palette n'en change aucun. **Arrêt** : toute autre
différence, au fichier ou au pixel.

### ⏳ S3 — Convertisseur : lecteurs, émission, catalogue, preuve

**But** : l'export produit un `.sprite` par portrait, catalogué, et `Data/item-icon-index.json` plus
`Data/items-properties.json` bruts.

**Contenu** : `ItemsPropertiesCatalogReader` et le lecteur de déverrouillage sur le modèle de
`MapSoundGroupIndexCatalogReader`, republication brute ; `SpriteWriter` émet le portrait de chaque
banque concernée avec la clé D-N-6 ; `AssetVerifier` satisfait (D-N-3) ; compteurs de
`ConversionReport` ; tests (précédent `MapSoundGroupIndexCatalogReaderTests`).

**Acceptation, régime de preuve** : diff prédit écrit **avant** l'export : les N `.sprite`
nouveaux, les deux JSON, **le catalogue d'actifs `AssetInfos.json`** que chaque nouveau `.sprite`
réécrit (`SpriteWriter.cs:164`, `AssetVerifier.cs:96-105`), `report.json`, et **les déplacements de
fichiers `.sprite` existants** que S0 a prédits quand un portrait est une signature déjà émise par
une autre banque, chacun nommé ; rien d'autre. Export complet en place ; diff mesuré ⊆ prédit ;
double export ⊆ `{report.json}` ; intégrité référentielle des nouveaux `.sprite` vers la texture de
la planche ; et la planche exportée identique au pixel à celle de S2 hors de l'ensemble exempté
défini en S2.

### ⏳ S4 — DLL : l'arme équipée et son icône dans la case

**But** : F1 en jeu montre l'épée de base dans la case de gauche, sur son fond.

**Contenu** : `WeaponId` sur `AlundraPlayerStats` avec `SetPlayerWeaponId` et sa règle ; compteurs
d'objets et boucle de déverrouillage de la nouvelle partie (objets 1, 17, 25) ; `GetWeaponIdBySlotId`
et `GetItemIdFromSlotId` portés ligne à ligne ; lecture de `Data/items-properties.json` et
`Data/item-icon-index.json` ; le composeur émet une tuile d'icône dynamique à (16, 16) natif, taille
prise du sprite, au-dessus du fond de C6 ; l'écran charge le `.sprite` par identifiant, comme C2.
La case d'accessoire : rien, fond seul (D-E13C-7). La recette F1 ne change pas.

**Acceptation** : tests sur la règle de `SetPlayerWeaponId`, la résolution arme → objet → icône à
la nouvelle partie (1 → 1 → 31), la sentinelle sans arme, la case d'accessoire vide ; suite verte ;
capture en processus F1 : l'icône aux bonnes coordonnées sur le fond gouraud, nette.

### ⏳ S5 — Recette en jeu

**Contenu** : F1, l'épée de base apparaît dans la case de gauche ; la case de droite reste un fond
vide ; les deux suivent le glissement.

---

## 4. Acceptation d'ensemble

E13.c est close quand : le double export est prouvé ; `data-extracted/` est prouvé identique au
miroir ; en jeu, F1 montre l'icône de l'épée de base sur son fond, nette, qui glisse avec la jauge ;
suites `Alundra.Tests` et convertisseur vertes.

## 5. Arrêts

- S0 : atlas instable sous `Original` ; **intersection à palette différente** sur une page ;
  position et `Sector5Id` qui divergent ou enregistrement désigné ignoré comme doublon ; convention
  « pas d'icône » indéterminable → auteur.
- S1 : le run à blanc change autre chose que les portraits, **ou le rang de première rencontre d'une
  signature existante a changé**, ou un pixel change hors de l'ensemble exempté → arrêt avant tout
  miroir.
- S2 : le diff du miroir dépasse le prédit, **ou un pixel change hors de l'ensemble exempté** →
  ne pas exporter.
- S3 : diff mesuré hors du prédit, ou double export non inclus dans `{report.json}` → arrêt.
- S4 : un besoin moteur → arrêt (D-E13C-8).

## 6. Points ouverts

1. La convention « pas d'icône » de la colonne 4 : la ligne 0 porte `0xFFFF` ; S0 confirme que
   c'est la seule valeur d'absence sur les 100 lignes.
2. Le format exact du portrait dans `map_alundra.json` : champ sur l'enregistrement, ou image
   marquée dans la liste ? S1 choisit, en visant la plus petite surface pour `SpriteBankReader`.
3. `g_itemDropProperties` : n'exporter que le drapeau de déverrouillage, ou les cinq champs ? S1,
   après lecture de ce que `GetItemIdFromSlotId` consomme réellement.
4. **`SetPlayerWeaponId` et la valeur 0 — question pour l'auteur.** La translittération accepte 0
   (§1.2) là où l'original, comparaison non signée sur 32 bits, le rejetterait probablement. À
   vérifier dans Ghidra sur `0x8004ddf4` et voisins avant que S4 ne fige la règle ; en attendant S4
   porte le code cité et marque la ligne.

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-19 | Reconnaissance à trois surfaces (original, exports, MGUI) puis ciblée sur la chaîne d'extraction. Quatre arbitrages de l'auteur : E13.c avant E13.d, tous les portraits à icône, inventaire principal d'abord pour E13.d, fidélité stricte pour l'accessoire. Plan rédigé. |
| 2026-09-19 | Première relecture adverse : **REVISE**, un P1 et quatre P2, tous acceptés. (1) P1 — la disposition `Original` place une image à sa fenêtre VRAM sans la palette alors que la signature l'inclut : un portrait pourrait repeindre un sprite exporté sans qu'un diff de fichiers le voie ; §1.4 corrigé, S0 mesure les collisions, D-E13C-3 en fait un arrêt, S2 et S3 exigent une preuve au pixel hors des rectangles ajoutés. (2) La table a 100 lignes et non 98, `g_itemsCount` vaut 99, 98 est la borne de la boucle de déverrouillage ; §1.3 corrigé, S0 relève les trois bornes. (3) L'original indexe les enregistrements par position, le convertisseur par `Sector5Id` en ignorant les doublons ; S0 mesure la correspondance, arrêt en cas de divergence. (4) `SetPlayerWeaponId` translittéré prend un `ushort`, la sentinelle est inatteignable et 0 est accepté ; §1.2 restaté, question auteur en §6 point 4. (5) Le diff prédit omettait le catalogue d'actifs et les déplacements de `.sprite` par déduplication globale entre banques ; S3 les prédit, S0 les mesure. |
| 2026-09-19 | Relecture de clôture : **REVISE**, un P1 et un P2, tous deux acceptés. (1) P1 — le test de collision par égalité de cellule manquait les recouvrements partiels, chaque signature étant dessinée entière à sa propre origine et taille ; et l'acceptation au pixel exemptait justement les rectangles où le dommage tombe. Corrigé : intersection de rectangles par page contre toute signature existante, intersection à même palette autorisée avec sa justification, à palette différente arrêt ; ensemble exempté redéfini comme les seuls rectangles des portraits nouveaux et sans intersection, comparaison sur toute la planche. (2) P2 — l'ordre de dessin est celui de première rencontre et n'était pas figé ; S1 concatène les portraits strictement après la séquence existante, S0 relève les rangs, S1 prouve le préfixe inchangé, arrêt ajouté. **Deuxième REVISE consécutif, plafond atteint : disposition en session principale, pas de nouvelle soumission.** Le plan part à l'auteur pour approbation avec ces corrections. |
