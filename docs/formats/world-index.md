# Index des mondes (`world-index.json`)

Code : [`Writers/WorldWriter.cs`](../../alundra-casaengine-project-converter/Writers/WorldWriter.cs)
(Phase 6, `WriteWorldIndex`).

## Ce que c'est

La table qui associe un `MapId` Alundra au chemin relatif du `.world` correspondant.

## Où c'est écrit

`Maps/world-index.json`, à la racine de `Maps/` : c'est là que vivent désormais les mondes qu'il
indexe, chacun à la racine du dossier de sa map
(`Maps/{Zone}/{Name}-{id}/{Name}-{id}.world`, disposition définie par `MapLocation`).

## Pourquoi ici et pas comme asset CasaEngine

`docs/demarrage-nouvelle-partie.md` (étape E6) demande ce fichier pour que le futur système de
portails runtime puisse résoudre le `DestMapId` d'un portail vers un monde sans parcourir tout le
catalogue. Ce n'est pas un type d'asset CasaEngine, juste une table d'indirection pour la DLL de
gameplay.

## Détails

Les valeurs sont **exactement** les chaînes enregistrées dans `AssetInfos.json`, car
`AssetCatalog.GetByFileName` — que `GameManager.UpdateWorld` utilise pour résoudre le nom d'un
monde — est une recherche de dictionnaire ordinale, pas une comparaison de chemin ; il faut donc
la même chaîne, séparateurs de dossier compris.

`MapId` est égal à l'index de fichier de la map pour les 483 maps (vérifié contre `Info.MapId`),
ce qui correspond aussi à ce que suggère la table d'identité du jeu
`MapIdToInternalMapIndexTable`.

## Schéma

Un objet JSON, clé = `MapId` (texte), valeur = chemin relatif du `.world` :

| Élément | Type | Signification | Source |
|---|---|---|---|
| clé | string | `MapId` Alundra (identique à l'index de fichier `map_N`) | index de map |
| valeur | string | Chemin relatif du `.world`, tel qu'enregistré dans `AssetInfos.json` | `AssetInfo.FileName` du `.world` de cette map |

## Extrait réel

```json
{
  "0": "Worlds\\Test Map\\Test Map-0.world",
  "1": "Worlds\\Overworld\\Overworld 0,0-1.world",
  "2": "Worlds\\Overworld\\Overworld 0,1-2.world"
}
```
