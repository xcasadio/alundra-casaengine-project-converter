# Bytecode d'évènements (`*.events.json`)

Code : [`Readers/EventCodeReader.cs`](../../alundra-casaengine-project-converter/Readers/EventCodeReader.cs),
[`Writers/EventCodeWriter.cs`](../../alundra-casaengine-project-converter/Writers/EventCodeWriter.cs)
(Phase 6, seconde moitié).

## Ce que c'est

Le bytecode d'évènements de map d'Alundra : six tables de programmes (A à F) qui pointent, par
offset, dans un unique flux d'octets (`Codes`), plus les champs d'en-tête nécessaires pour résoudre
ces offsets en index dans ce flux.

## Où c'est écrit

`Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json`, dans le dossier propre à la map, à côté
de ses sous-dossiers `tilemap/` et `dialogues/` et de son `.world` : toutes les données d'une même
map sont regroupées au même endroit. La disposition est définie par `MapLocation`
(`Readers/MapCatalogReader.cs`), seule autorité sur les chemins de sortie d'une map.

## Pourquoi ici et pas comme asset CasaEngine

Ce fichier est une simple donnée compagnon JSON, **pas** enregistrée dans le catalogue d'assets :
aucun type du moteur ne saurait la charger. C'est une donnée pour le futur interpréteur de la DLL
de gameplay, exactement comme `dialogues/*.strings.json` est une donnée pour un futur système de
dialogue.

## Pourquoi rien n'est interprété

Les opcodes des programmes d'évènement A–F d'Alundra ne sont pas encore décodés ; imposer une
structure maintenant reviendrait à figer une supposition dans le projet converti. Les six tables et
le blob `Codes` sont donc copiés avec leurs noms de champ et leur ordre source, sous forme
losslessly re-dérivable.

`SpriteInfo.Header` porte les champs pointeur/taille des `EventCodes` : les tables contiennent des
offsets relatifs à l'adresse de chargement PSX du blob, donc sans `EventCodeAddress` et les paires
pointeur/taille par programme, un offset dans une table ne peut pas être retraduit en index dans
`Codes`. Ces champs voyagent avec les tables (voir `Header` ci-dessous) pour cette raison.

`Codes` contient des octets dans la donnée source (plage observée 0–255 sur les 483 maps) mais est
écrit comme un tableau de nombres JSON, pas comme un blob base64, pour rester greppable et diffable
pendant que le bytecode est rétro-ingénié. Le compteur `Events.CodeWords` de `report.json` compte
donc un élément par octet brut de code.

## Schéma

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `map_index` | int | Index de la map Alundra | (paramètre du writer) |
| `header` | objet | Voir ci-dessous | `SpriteInfo.Header` (partie EventCodes) |
| `event_codes_a_table[]` … `event_codes_f_table[]` | int[] | Table du programme A à F (offsets dans `codes`) | `SpriteInfo.EventCodes.EventCodesXTable` |
| `codes[]` | int[] | Le flux d'octets du bytecode, brut | `SpriteInfo.EventCodes.Codes` |

`header` :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `event_code_address` | int | Adresse de chargement PSX du blob `Codes` | `Header.EventCodeAddress` |
| `event_codes_a_pointer` / `_size` … `event_codes_f_pointer` / `_size` | int | Pointeur et taille du programme correspondant | `Header.EventCodesXPointer` / `EventCodesXSize` |
| `event_codes_f_and_remaining_size` | int | Taille de F plus le reste du blob | `Header.EventCodesFAndRemainingSize` |

Un champ absent produit un tableau vide (ou zéro) plutôt qu'une exception : une map sans évènement
est légitime.

Notez que la sérialisation de ce fichier n'utilise **pas** de politique de nommage snake_case (à la
différence des autres formats compagnons) — les noms de champ ci-dessus reflètent donc les clés
réelles du JSON écrit (`PascalCase`), volontairement identiques à ceux de l'extracteur : le but de
ce fichier est justement de garder les noms de champ de la source.

## Extrait réel

`Maps/Ancient Shrine/Ancient Shrine - Golem-34/events/Ancient Shrine - Golem-34.events.json` :

```json
{
  "MapIndex": 34,
  "Header": {
    "EventCodeAddress": 1500140,
    "EventCodesAPointer": 1532,
    "EventCodesASize": 6,
    "EventCodesBPointer": 1538,
    "EventCodesBSize": 10,
    "EventCodesCPointer": 1548,
    "EventCodesCSize": 6,
    "EventCodesDPointer": 1554,
    "EventCodesDSize": 2,
    "EventCodesEPointer": 1556,
    "EventCodesESize": 2,
    "EventCodesFPointer": 1558,
    "EventCodesFSize": 2,
    "EventCodesFAndRemainingSize": 298
  },
  "EventCodesATable": [0, 0, 28],
  "EventCodesBTable": [0, 36, 68, 172, 180],
  "EventCodesCTable": [0, 240, 312],
  "EventCodesDTable": [0],
  "EventCodesETable": [0],
  "EventCodesFTable": [0],
  "Codes": [0, 0, 0, 0, 28, 0, 0, 0, 36, 0, 68, 0]
}
```
