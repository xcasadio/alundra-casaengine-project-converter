# Sauvegarde de partie (`AlundraSaveGame`)

Code : [`AlundraSaveGame.cs`](../../Alundra/Scripts/AlundraSaveGame.cs),
[`AlundraSaveGameRules.cs`](../../Alundra/Scripts/AlundraSaveGameRules.cs),
[`AlundraMapSizeReader.cs`](../../Alundra/Scripts/AlundraMapSizeReader.cs),
[`AlundraChapterFlags.cs`](../../Alundra/Scripts/AlundraChapterFlags.cs). Décisions :
[ADR-0012](../decisions/0012-alundra-save-game-content-and-validation.md), plan
[E16](../plan-e16-etat-partie.md) (tranche E16.c).

## Ce que c'est

L'objet que la DLL confie au service de sauvegarde du moteur (`ISaveGameData`, ADR-0044 du moteur,
[`save-games.md`](../../CasaEngineMonogame/docs/engine/save-games.md)). Contrairement aux autres
formats de ce dossier, il n'est pas écrit par le convertisseur : c'est la DLL qui l'écrit, en jeu.

## Où c'est écrit

Par le service du moteur, dans `LocalApplicationData/AlundraGame/SaveGames/<emplacement>.sav`
(`ProjectName` d'`AlundraGame.json`), en JSON lisible ou en binaire compact. La DLL n'écrit ni ne
lit aucun fichier elle-même.

## Contenu

Version de données 1. L'ordre compte pour le binaire, qui est positionnel.

| Champ | Type | Domaine accepté au chargement | Origine (`g_saveData`) |
|---|---|---|---|
| `gameTime` | `uint` | 0 à `0x14996C4` (99:59:59) ; 60 unités par seconde réelle | `GameTime` |
| `initialMapId` | `int` | clé de `Maps/world-index.json`, monde présent au catalogue, `.tileMap` lisible | `InitialMapId` |
| `cameraTileX` | `int` | 0 ≤ X < largeur de la carte, et X ≤ 1364 | `CameraTileX` |
| `cameraTileY` | `int` | 0 ≤ Y < hauteur de la carte, et Y ≤ 2047 | `CameraTileY` |
| `cameraTileZ` | `int` | 0 à 256 | `CameraTileZ` |
| `gameFlags` | `uint[64]` | toute valeur | `GameFlags` |
| `mapIdToInternalMapIndexTable` | `ushort[500]` | chaque valeur est une clé de `world-index.json` ou son propre indice | `MapIdToInternalMapIndexTable` |
| `playerStats.hpMax` | `short` | 0 à 50 | `PlayerStats` |
| `playerStats.hp` | `short` | 0 à `hpMax` | |
| `playerStats.mpMax` | `short` | 0 à 4 | |
| `playerStats.mp` | `short` | 0 à `mpMax` | |
| `playerStats.money` | `short` | 0 à 9999 | |
| `playerStats.weaponId` | `short` | −1, ou 1 à 6 | |
| `playerStats.itemId` | `short` | 0 à 98 | |
| `playerStats.falconTemp`, `playerStats.falcon` | `short` | 0 à 50 | |
| `numberOfItems` | `short[256]` | indice impair `id × 2 + 1` : de 0 au maximum de l'objet (`Data/items-properties.json`, colonne 3) ; indices pairs et indices ≥ 198 : 0 | `NumberOfItems` |
| `deathRetryCount` | `byte` | 0 à 255 | `SaveSlotIndex` (compteur de reprises après la mort) |

Absents : `TemporaryFlags`, l'indice de catégorie du texte et les variables `\V` (hors de
`g_saveData` dans l'original), `SlotData`, `LastMapId`, `Field_757` et `Offset`.

## Métadonnées

Lues par la liste des emplacements sans décoder la sauvegarde. Ce sont du texte non fiable pour qui
les relit.

- `chapter` : l'indice du chapitre en cours, sur quatre chiffres (`"0000"` à `"0041"`), calculé par
  le parcours des 41 drapeaux de fin de chapitre (`GetFirstEnabledFlagIndex`, `0x800813B0`) ;
- `summary` : `"  HP xx       TIME hh:mm:ss   "`, construit comme `UpdateMenuStatusText`
  (`0x80030FC8`) sur `hpMax` et `gameTime`.

## Chargement

Le service rend un objet neuf ; la DLL le valide en entier (`TryValidate`) avant de toucher l'état
vivant. Une seule valeur hors domaine, ou une version de données autre que 1, refuse tout le
chargement avec un message qui nomme le champ. L'application (`ApplyTo`) efface les 1024 mots de la
banque `GameFlags` avant de copier les 64 sauvegardés.

Le chargement passe par le chemin des warps (ADR-0013, `AlundraSaveGameDirector`) :
1. préconditions : héros présent, aucun chargement en attente, aucun dialogue, aucun inventaire,
   post-traitement et portrait inactifs, aucune transition, aucun fondu maître de la musique armé,
   `PlayerControlFlags == 0` ;
2. emplacement lisible le plus récent, lecture, puis validation ;
3. contrôle du départ : warp actif, `GameManager` attaché, carte de départ résolue par la table du
   directeur des warps ;
4. le chargement est mis en attente, puis un départ part vers `InitialMapId` à la tuile sauvegardée
   (animation `0x36`, direction 0, sans son) ;
5. à l'entrée de la carte d'arrivée, juste après `GameState.InstallForMapEntry()`, donc avant le
   premier tick : l'attente est vidée, la carte vérifiée, la session remise à zéro (indicateurs de
   contrôle, verrou d'interaction, indice de catégorie et variables `\V`, inventaires), puis
   `ApplyTo` et la remise de la jauge sur les valeurs chargées.

Un refus, une erreur du service, un départ avorté ou une arrivée sur une autre carte laissent la
partie intacte, avec une ligne de journal.

## Touches de recette

Actives quand `Alundra.dll` est compilée en Debug (D-E16-33), inactives en Release ; le journal
note une fois qu'elles sont actives. La DLL recopiée dans `alundra-project/` est celle du dernier
build.

| Touche | Action | Emplacement |
|---|---|---|
| F5 | sauvegarde en binaire | `debug-binary` |
| F6 | sauvegarde en JSON | `debug-json` |
| F9 | charge l'emplacement lisible le plus récent | le plus récent par date d'écriture |

Une sauvegarde n'est écrite que si le héros est au sol, sans dialogue, menu ni transition, et si
l'objet capturé passe la validation.

## Limites

- Le sens des drapeaux ne se valide pas : une sauvegarde éditée peut casser l'histoire, bloquer le
  joueur ou figer un script.
- Une tuile dans les bornes mais dans un mur est acceptée.
- `Hp = 0` est accepté jusqu'à E18.
- Le temps de jeu n'avance pas pendant le chargement d'un monde.
- « Le plus récent » de F9 : une date future gagne toujours, et un emplacement récent dont l'en-tête
  s'ouvre mais dont les données sont abîmées bloque F9, sans repli sur le précédent.
- Si l'inventaire s'ouvre pendant le fondu d'un chargement, la jauge peut rester cachée à
  l'arrivée, jusqu'à la prochaine ouverture de l'inventaire ou demande d'un script. Le même trou
  existe sur les warps ordinaires.
