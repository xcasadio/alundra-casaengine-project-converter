# Tables de texte (`global-strings.json`, `*.strings.json`, `control-codes.json`)

Code : [`Readers/StringTableReader.cs`](../../alundra-casaengine-project-converter/Readers/StringTableReader.cs),
[`Writers/TextWriter.cs`](../../alundra-casaengine-project-converter/Writers/TextWriter.cs)
(Phase 5, première moitié).

## Ce que c'est

Le texte d'Alundra est déjà décodé en Unicode français par l'extracteur, mais ses codes de contrôle
sont des octets bruts d'un langage de script partiellement compris. Ces fichiers déplacent le texte
sans le réinterpréter.

## Où c'est écrit

- `Dialogues/global-strings.json` — la table globale (menus, chapitres, objets).
- `Maps/{Zone}/{Name}-{id}/dialogues/{Name}-{id}.strings.json` — les 128 lignes de dialogue de
  chaque map, dans le dossier propre à cette map, à côté de son `tilemap/`, de son `events/` et de
  son `.world`. La disposition est définie par `MapLocation` (`Readers/MapCatalogReader.cs`).
  `Dialogues/` ne garde que les deux tables globales ci-dessus et ci-dessous.
- `Dialogues/control-codes.json` — un inventaire des tokens de contrôle rencontrés dans les deux
  types de table ci-dessus.

## Pourquoi ici et pas comme asset CasaEngine

Aucun vrai graphe de dialogue (Yarn ou autre) n'est produit : quelle ligne est prononcée quand vit
dans le bytecode de map, qui n'est pas encore décodé (voir [`events.md`](events.md)). Une table est
ce que représente la source, donc une table est ce qui est écrit — la structure de dialogue viendra
une fois le bytecode compris.

## `global-strings.json`

Un objet JSON qui reflète `data/ETC_RES.R.json` : mêmes clés, mêmes valeurs.

| Élément | Type | Signification | Champ source |
|---|---|---|---|
| clé | string | Offset d'octet décimal dans `ETC_RES.R`, c'est le handle que le jeu utilise pour adresser la chaîne | clé de `ETC_RES.R.json` |
| valeur | string ou null | Le texte, espaces de complément inclus (enregistrement de largeur fixe), ou `null` | valeur de `ETC_RES.R.json` |

Rien n'est retiré : le padding d'espaces final fait partie de l'enregistrement à largeur fixe, et
les 562 lignes `null` (sur 916) sont conservées car un emplacement vide est une donnée (le slot
existe) plutôt qu'une absence. Les lignes sont ordonnées par la valeur numérique de leur clé, pour
que deux exécutions produisent des fichiers identiques octet pour octet.

## `{Name}-{id}.strings.json`

Un tableau JSON de 128 chaînes (ou moins si la source en a moins), une par map.

Un **tableau**, pas un objet : l'identité d'une ligne est sa position dans le tableau — c'est
l'index que les scripts de la map utilisent — et un tableau ne peut pas dériver de cet index comme
le pourrait un objet trié différemment. Aucun tri, aucune compaction ; les valeurs `null` sont
conservées à leur position.

## `control-codes.json`

Un inventaire, pas une traduction : chaque token de deux caractères commençant par `\`, `{` ou `}`
est compté, avec un exemple de chaîne complète. La tokenisation suit le lecteur du jeu
(`AlundraEngine.Text.TextDecoder.TextInterpreter`) : il distingue sur le caractère qui suit `\`, et
`{`/`}` consomment toujours exactement un caractère suivant. Les codes prenant un argument (`\W2`,
`\C#`) apparaissent donc comme leur tête à deux caractères, l'argument restant dans l'exemple —
volontairement, car regrouper l'argument suppose de savoir quels codes en prennent un. Rien n'est
filtré : un échappement d'apparence inconnue est exactement ce que ce fichier existe à révéler.

| Champ | Type | Signification |
|---|---|---|
| `code` | string | Le token de deux caractères (ex. `\C`, `\W`, `{0`) |
| `count` | int | Nombre d'occurrences sur l'ensemble des tables converties |
| `example` | string | Une chaîne entière (non tronquée) contenant ce token |

Sur les 483 maps + la table globale, 27 codes distincts ont été trouvés (`Text.ControlCodesDistinct`
dans `report.json`) pour des dizaines de milliers d'occurrences — trop volumineux pour vivre en
compteurs `report.json`, d'où un fichier dédié.

## Extraits réels

`Dialogues/global-strings.json` :

```json
{
  "2048": null,
  "2049": "Un Nouveau Départ              "
}
```

`Maps/Ancient Shrine/Ancient Shrine - Golem-34/dialogues/Ancient Shrine - Golem-34.strings.json` :

```json
[
  "\\B#Disuse",
  "\\BSi ton courage ne forme qu'un\\Navec nous quatre\\W2 La voie te sera\\Nfacilement ouverte\\W2"
]
```

`Dialogues/control-codes.json` :

```json
{
  "code": "\\3",
  "count": 22,
  "example": "\\CVeux-tu que j'actionne l'interrupteur ?\\301\\Y"
}
```
