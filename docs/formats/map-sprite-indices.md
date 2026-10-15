# Indices de sprite valides par carte (`Data/map-sprite-indices.json`)

Code : [`Writers/SpriteWriter.cs`](../../alundra-casaengine-project-converter/Writers/SpriteWriter.cs) (`WriteMapSpriteIndices`),
collecte dans [`Readers/SpriteBankReader.cs`](../../alundra-casaengine-project-converter/Readers/SpriteBankReader.cs)
(`CollectValidSpriteIndices`). Décision : [ADR-0062](../decisions/0062-the-sprite-table-key-of-each-prefab-and-the-valid-indices-of-each-map-are-exported.md),
plan [E20.e0x](../plan-e20-heros.md), D-E20-33.

**Ce que c'est** : pour chaque carte native, la liste des indices (0 à 255) de la « page des cartes » de la table de sprites dont la
fiche `SpriteInfo.SpriteRecords[i]` n'est pas nulle. Le binaire (`0x80039B28`, appelé par `SpawnWarpEntity`) ne fait apparaître une
entité que si l'indice demandé est dans la table de la carte courante et différent de −1 : sinon il retourne 0 et rien n'apparaît,
sans erreur (exemple : Zorgia, indice 213, sur les cartes 388, 432 et 451). La DLL a besoin de cet ensemble pour reproduire ces
apparitions qui échouent, carte par carte ; elle fait la correspondance indice → préfab avec le `BankKey` de
[`sprite-records.md`](sprite-records.md).

**Où c'est écrit** : `Data/map-sprite-indices.json`, hors du catalogue des assets (aucun type du moteur ne le charge).

## Schéma

Un objet racine ; une clé par carte, **le numéro seul en chaîne** (`"388"`, jamais `"map_388"`), pour chacun des 483 fichiers
`map_<N>.json` (N de 0 à 482, `map_alundra` exclu), en ordre numérique croissant. La valeur est le tableau, en ordre croissant, des
indices de la page des cartes à fiche non nulle ; `[]` pour une carte sans aucun indice (65 cartes, dont la 0).

Règle de validité : fiche `SpriteRecords[i]` non nulle. Dans les données, c'est exactement la même chose que `SpriteTable[i] != -1` ;
le convertisseur contrôle l'égalité carte par carte et la traite comme une erreur du rapport s'il y a un écart. Au total 2 507 indices
(244 nombres distincts, 22 au plus par carte).

Texte : celui de `JsonSerializer.Serialize` avec les mêmes options que `sprite-records.json` : UTF-8 sans BOM, fins de ligne CRLF,
2 espaces par niveau, un nombre par ligne, `[]` pour un tableau vide, aucun saut de ligne après l'accolade finale.

**Extrait réel** (début du fichier et carte 388) :

```json
{
  "0": [],
  "1": [
    …
  ],
  "388": [
    121,
    122,
    237,
    253,
    254,
    255
  ],
  …
}
```

L'indice 213 n'est valide sur aucune des cartes 388, 432 et 451. La page globale (indice 0 de `SpawnWarpEntity` = table globale) n'a
pas de validité par carte et n'est pas dans ce fichier ; elle est entièrement décrite par les fiches `alundra_N` de
`sprite-records.json`.
