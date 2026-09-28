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

## Limites

- Le sens des drapeaux ne se valide pas : une sauvegarde éditée peut casser l'histoire, bloquer le
  joueur ou figer un script.
- Une tuile dans les bornes mais dans un mur est acceptée.
- `Hp = 0` est accepté jusqu'à E18.
- Le temps de jeu n'avance pas pendant le chargement d'un monde.
