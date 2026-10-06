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
| [`dialogues-yarn.md`](dialogues-yarn.md) | Les `.yarn` et `.dialogue` catalogués : tout le texte en Yarn (une par carte, `Dialogues/Shared`, `Dialogues/Etc`), codes de contrôle en marqueurs, commandes et fonctions. |
| [`font.md`](font.md) | `UI/font3.fnt` (BMFont) et `UI/font3-charset.json` : la police bitmap et la table code brut → point de code Unicode. |
| [`events.md`](events.md) | `Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json` : le bytecode d'évènements de map, non interprété. |
| [`backdrops.md`](backdrops.md) | `Maps/{Zone}/{Name}-{id}/backdrop/{Name}-{id}.backdrop.json` : les couches de décor défilant PSX (parallaxe, auto-scroll, cellulaire différé), plus les textures pré-rendues. |
| [`effects.md`](effects.md) | `Maps/{Zone}/{Name}-{id}/effects/{Name}-{id}.effects.json` et `Data/effects/effects-global.json` : les enregistrements et les tables d'effets (quads libres, animations, mode PS1 par quad), plus leurs planches. |
| [`sprite-records.md`](sprite-records.md) | `Data/sprite-records.json` : une fiche par préfab d'entité (en-tête de sprite, animations, volume de corps) et, pour les 25 banques à portrait, le lien `DialoguePortrait` vers le `.sprite` du portrait de dialogue. |
| [`world-index.md`](world-index.md) | `Maps/world-index.json` : la table MapId → chemin du `.world`. |
| [`misc-data.md`](misc-data.md) | `Data/balance.json`, `UI/wind-sprites.json`, `Sprites/hero/hero_effects.json`. |
| [`save-game.md`](save-game.md) | La sauvegarde de partie (`AlundraSaveGame`) : écrite en jeu par la DLL à travers le service du moteur, pas par le convertisseur ; champs, domaines, métadonnées. |

Voir aussi le [`README.md`](../../README.md) racine pour l'usage du CLI et la disposition générale
d'un projet converti.
