# Données diverses (`balance.json`, `wind-sprites.json`, `hero_effects.json`)

Trois petits formats compagnons qui ne justifiaient pas chacun leur propre page.

## `Data/balance.json`

Code : [`Writers/UiWriter.cs`](../../alundra-casaengine-project-converter/Writers/UiWriter.cs)
(`ConvertBalance`, Phase 7).

**Ce que c'est** : la table d'équilibrage du jeu (512 enregistrements par niveau — points de vie,
11 `Values` non nommées, les paires `AnimVals` dont le nombre est donné par `NumAnimVals`,
`OffsetToNextLevel`, `Offset`, `Next`).

**Où c'est écrit** : `Data/balance.json`.

**Pourquoi ici et pas comme asset CasaEngine** : c'est une recopie structurée de
`data/BALANCE.BIN.json`, structure et noms de champ inchangés — le sens de ces champs est encore
inconnu, les renommer détruirait la seule prise qu'une future DLL de gameplay a dessus. La seule
exception est le `FileName` de premier niveau : c'est le chemin absolu du `.BIN` sur la machine de
l'extracteur (une trace de provenance, pas une donnée de jeu), et le garder rendrait la sortie du
convertisseur dépendante de la machine où l'extraction a eu lieu.

**Schéma** : identique à `BALANCE.BIN.json` moins `FileName`. Champ notable :

| Champ | Type | Signification |
|---|---|---|
| `BalanceRecords[]` | tableau | 512 enregistrements par niveau |
| `BalanceRecords[].Level` | int | Numéro de niveau |
| `BalanceRecords[].Hp` | int | Points de vie |
| `BalanceRecords[].Values[]` | int[11] | Valeurs non nommées |
| `BalanceRecords[].NumAnimVals` | int | Nombre de paires dans `AnimVals` |
| `BalanceRecords[].AnimVals[]` | tableau de `{Val, U2}` | Paires de valeurs d'animation |
| `BalanceRecords[].OffsetToNextLevel`, `Offset`, `Next` | int | Champs d'adressage internes au `.BIN` |

**Extrait réel** :

```json
{
  "BalanceRecords": [
    {
      "Level": 255,
      "OffsetToNextLevel": 169,
      "Hp": 10,
      "Values": [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      "NumAnimVals": 77,
      "AnimVals": [{ "Val": 0, "U2": 0 }, { "Val": 133, "U2": 2 }]
    }
  ]
}
```

---

## `UI/wind-sprites.json`

Code : [`Writers/UiWriter.cs`](../../alundra-casaengine-project-converter/Writers/UiWriter.cs)
(`ConvertWindSprites`, Phase 7).

**Ce que c'est** : la table source des 277 découpes de `ui/wind.json` (cadres de fenêtre, pièces de
HUD, icônes) enrichie de l'asset id du `.sprite` que chaque découpe a produit.

**Où c'est écrit** : `UI/wind-sprites.json`.

**Pourquoi ici et pas seulement dans les `.sprite`** : `SpriteData` n'a pas d'emplacement pour
`PaletteIndex` (pas de sac de propriétés personnalisées, et le PNG extrait est déjà résolu en RGBA),
donc la table source complète est préservée à côté des sprites, indexée par le même index que le
jeu utilise pour adresser une découpe d'UI.

**Schéma** :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `index` | int | Position dans `ui/wind.json` (l'identité que le jeu utilise) | position dans le tableau |
| `name` | string | Nom du `.sprite` produit (`wind_NNN`) | dérivé de `index` |
| `asset_id` | guid | Id catalogue du `.sprite` | dérivé |
| `u0`, `v0` | int | Coordonnées de la page de texture PSX du coin haut-gauche de la découpe | `U0`, `V0` |
| `width`, `height` | int | Dimensions de la découpe | `Width`, `Height` |
| `palette_index` | int | CLUT utilisée par le jeu pour dessiner cette découpe | `PaletteIndex` |

**Extrait réel** :

```json
{
  "index": 0,
  "name": "wind_000",
  "asset_id": "459cbd12-3411-4cdf-be4b-d82f65afaf3f",
  "u0": 0,
  "v0": 40,
  "width": 8,
  "height": 16,
  "palette_index": 5
}
```

---

## `Sprites/hero/hero_effects.json`

Code : [`Writers/SpriteWriter.cs`](../../alundra-casaengine-project-converter/Writers/SpriteWriter.cs)
(`PreserveHeroEffects`, Phase 3).

**Ce que c'est** : les `SpriteEffectRecords` du héros (`data/map_alundra.json`,
`SpriteInfo.SpriteEffectRecords`) — des effets visuels liés au héros dont les index `Spritesheet`
dépassent la plage normale 0–7, suggérant une source graphique différente de celle que le correctif
d'empaquetage d'atlas couvre.

**Où c'est écrit** : `Sprites/hero/hero_effects.json`, recopie brute de la valeur JSON source
(`effectsElement.GetRawText()` — aucun renommage, aucune restructuration).

**Pourquoi ici et pas converti en sprites/animations** : en V1 ces enregistrements ne sont pas
convertis en `.sprite`/`.anim2d` comme le reste des banques (voir le "known gap" correspondant dans
le [`README.md`](../../README.md) racine) ; ils sont préservés bruts pour qu'une passe future puisse
les traiter une fois leur source graphique comprise.

**Schéma** : identique à la structure source `SpriteEffectRecords`, non retraitée — se référer au
JSON de `data/map_alundra.json` pour le détail des champs (`BinOffset`, `SpriteInfoMemoryAddress`,
`AnimationOffsets[]`, …).

**Extrait réel** :

```json
{
  "BinOffset": 4508,
  "SpriteInfoMemoryAddress": 1394172,
  "AnimationOffsets": [12, 16, 20, 24, 28, 32, 0, 0, 0, 0]
}
```
