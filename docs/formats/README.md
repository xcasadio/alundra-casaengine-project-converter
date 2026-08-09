# Formats compagnons

Le convertisseur produit, en plus des assets CasaEngine proprement dits (`.tileMap`, `.sprite`,
`.anim2d`, `.texture`, `.world`, …), une série de fichiers JSON qui ne correspondent à aucun type
d'asset du moteur. Ce sont des données de gameplay qu'aucune classe CasaEngine ne sait charger
aujourd'hui, conservées telles quelles pour qu'une future DLL de gameplay puisse les exploiter,
plutôt que d'être perdues au moment de la conversion.

Chaque fichier ci-dessous documente : ce qu'il contient, où il est écrit, pourquoi il existe en
dehors du système d'assets, son schéma champ par champ (avec le champ source Alundra dont il
provient), et un extrait réel tiré d'une conversion complète.

| Format | Description |
|---|---|
| [`cells-companion.md`](cells-companion.md) | Métadonnées de gameplay par cellule (walkabilité, pente, murs empilés), fusionnées dans `TileMapData.CustomProperties["AlundraCells"]`. |
| [`audio-manifests.md`](audio-manifests.md) | `Sounds/sfx-manifest.json` et `Musics/bgm-manifest.json` : les tables BGM/SFX de l'extracteur, enrichies de l'id catalogue de chaque WAV. |
| [`text-tables.md`](text-tables.md) | `Dialogues/global-strings.json`, les `*.strings.json` par map (sous `Maps/{Zone}/{Name}-{id}/dialogues/`), et `Dialogues/control-codes.json`. |
| [`font.md`](font.md) | `UI/font3.fnt` (BMFont) et `UI/font3-charset.json` : la police bitmap et la table code brut → point de code Unicode. |
| [`events.md`](events.md) | `Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json` : le bytecode d'évènements de map, non interprété. |
| [`world-index.md`](world-index.md) | `Maps/world-index.json` : la table MapId → chemin du `.world`. |
| [`misc-data.md`](misc-data.md) | `Data/balance.json`, `UI/wind-sprites.json`, `Sprites/hero/hero_effects.json`. |

Voir aussi le [`README.md`](../../README.md) racine pour l'usage du CLI et la disposition générale
d'un projet converti.
