# Police bitmap (`font3.fnt`, `font3-charset.json`)

Code : [`Writers/FontWriter.cs`](../../alundra-casaengine-project-converter/Writers/FontWriter.cs)
(Phase 5, seconde moitié).

## Ce que c'est

La police bitmap d'Alundra (`ui/font3.png` + `ui/font3.json`, 256 glyphes dans une grille de cases de
16×16) convertie en un fichier **BMFont** (`.fnt`) qu'une bibliothèque de rendu de texte standard sait
charger, plus une table compagnon qui garde tout ce qu'un `.fnt` ne peut pas représenter.

## Où c'est écrit

- `UI/font3.fnt` — le fichier BMFont (format texte).
- `UI/Textures/font3.png` — la page de la police, importée comme tout autre texture UI
  (copie brute + wrapper `.texture` catalogué), via `TextureAssetWriter`.
- `UI/font3-charset.json` — la table compagnon des 256 glyphes source.

`UI/font3.fnt` lui-même est catalogué comme une simple entrée fichier : ce n'est pas un type
d'asset CasaEngine, mais un runtime a besoin de pouvoir l'adresser par id.

## Pourquoi un `.fnt` et pas un asset CasaEngine natif

CasaEngine n'a pas de type "police" en propre ; BMFont est un format texte standard qu'une
bibliothèque de rendu de texte (FontStashSharp, par exemple, déjà utilisée par MGUI) sait charger
directement, ce qui évite d'inventer un format propriétaire pour une donnée par ailleurs bien
standardisée.

## `char id` = point de code Unicode, pas le code brut du jeu

Les chaînes extraites sont déjà en Unicode (l'extracteur convertit les paires d'échappement `{c` et
`}c` en UTF-8, `TextDecoder.DecodeString`) : le `.fnt` doit donc associer chaque case de l'atlas au
caractère Unicode qu'elle dessine. Decisions: see ADR-0009 (`docs/plan-e15-yarn.md`, E15.e,
D-E15-14 à D-E15-16).

- **En dessous de 128** : le code brut est son propre point de code (identité), glyphes 16 à 29
  compris (les marqueurs `[glyph id=N/]`, ADR-0008).
- **De 128 à 255** : seuls les 17 caractères non ASCII du texte ont une case, celle de leur octet
  CP1252 — c'est la case que dessine le jeu original (`}c` → case `0x90 + c`, `{c` → case
  `0x50 + c`), et l'atlas le confirme (case 233 = « é », case 130 = une virgule) :

  | Caractère | Case (octet CP1252) | Point de code |
  |---|---|---|
  | é à è ê ç î ô â | 0xE9 0xE0 0xE8 0xEA 0xE7 0xEE 0xF4 0xE2 | identique à la case |
  | û ù ï | 0xFB 0xF9 0xEF | identique à la case |
  | Ç É ° « » | 0xC7 0xC9 0xB0 0xAB 0xBB | identique à la case |
  | œ | 0x9C | U+0153 (0x9C n'est pas un caractère en Unicode) |

- **Toute autre case de 128 à 255** n'a aucun caractère : elle n'a pas de ligne `char` dans le
  `.fnt` et reste listée dans `font3-charset.json` avec `codepoint` à `null` et `reason` à
  `"no proven character"`. Rien n'est deviné : un caractère que le texte n'utilise pas encore n'a
  pas de glyphe tant que sa case n'est pas prouvée.

L'ancienne correspondance CP850 (portage de `TextDecoder.ConvertCp850ToLatin1`) est abandonnée :
elle prenait « é » dans la case 130, une virgule, dessinait faux 15 des 17 caractères non ASCII du
texte et n'avait aucun glyphe pour « œ » ; seul « ° » était juste. Comme les 17 points de code sont distincts et hors ASCII, aucune collision n'est
possible : `duplicate_of_raw_code` vaut toujours `null`. Sur les 256 glyphes source, 145 ont une
ligne `char` (`Font.Glyphs` = 145 dans `report.json`).

## Rectangles : la table de glyphes du binaire

Le jeu original ne dessine pas la case 16×16 de la grille : `RenderTextBitmap` (`0x800478C4`) copie,
pour chaque code brut, les `w × h` texels que donne la table de `0x800993C4` (256 entrées de 20 octets
`{w, h, srcX, srcY, yoff}`) depuis `(srcX, srcY)` de `FONT3.TIM`, puis avance la plume de `w`. Le
convertisseur lit cette table dans `FontGlyphTable.csv` (livré avec lui, relevé par l'analyseur sur
`g_fontCharWidthTable`, colonnes `code;width;height;srcX;srcY;yOffset`, 256 lignes) et en tire, par le
code brut de la case, `x`, `y`, `width`, `height` et `yoffset` de chaque ligne `char` ainsi que
`x`, `y`, `width`, `height` de chaque entrée de `font3-charset.json`. Sans cela, 129 des 145 lignes
`char` désignaient une autre fenêtre que le binaire et 16 glyphes se dessinaient autrement (virgule,
tiret, « 1 », deux-points, « œ », guillemets, « ° », pixels de la case voisine après « y », « z », « é »).
Décision : ADR-0036 (`docs/plan-e19-opcodes.md`, E19.f2b0).

Le contrôle de grille sur les enregistrements de `ui/font3.json` reste (un écart est un avertissement) ;
si `FontGlyphTable.csv` ou une de ses lignes manque, le rectangle de l'enregistrement source est repris,
avec un avertissement dans `report.json`. `xadvance` et le choix des caractères ne changent pas.

## Largeurs proportionnelles

Chaque `xadvance` vient de `FontCharWidths.csv` (livré avec le convertisseur, portage brut de la
table `g_fontCharWidthTable` de l'exécutable, `docs/plan-e12-dialogues.md`, E12.b), lu par le code
brut de la case et non par son point de code ; il égale la colonne `width` de `FontGlyphTable.csv`
sur les 145 lignes écrites. Un code absent du CSV retombe sur la largeur de
cellule (16px) avec un avertissement dans `report.json`.

## Schéma — `font3.fnt` (BMFont, format texte)

Sections standard BMFont :

- `info` — `face="font3" size=16 ...`.
- `common` — `lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 ...`.
- `page id=0 file="Textures/font3.png"` — chemin relatif au `.fnt`, avec des slashs directs.
- `chars count=N` — recalculé à partir des lignes réellement écrites.
- une ligne `char id=... x=... y=... width=... height=... xoffset=0 yoffset=0 xadvance=... page=0 chnl=15`
  par case qui a un caractère, `id` étant le point de code Unicode, `x`, `y`, `width`, `height` et
  `yoffset` le rectangle de la table du binaire (voir plus haut ; `yoffset` vaut 0 sur les 256 entrées
  du binaire) et `xadvance` la largeur proportionnelle de la case.

## Schéma — `font3-charset.json`

Un tableau, une entrée par glyphe source (256 entrées, triées par code brut) :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `raw_code` | int | Code brut du jeu (0–255) | `Code` (`ui/font3.json`) |
| `codepoint` | int ou null | Point de code Unicode de la case (règle ci-dessus), `null` si la case n'a pas de caractère prouvé | dérivé de `Code` |
| `x`, `y` | int | Position du glyphe dans l'atlas 256×256 : `srcX`, `srcY` de la table du binaire | `FontGlyphTable.csv` (repli : `X`, `Y`) |
| `width`, `height` | int | Dimensions du glyphe : `w`, `h` de la table du binaire (jusqu'à 16×16) | `FontGlyphTable.csv` (repli : `Width`, `Height`) |
| `palette` | int | Palette source (n'a nulle part où vivre dans un `.fnt`) | `Palette` |
| `advance` | int | Largeur proportionnelle de la case (`xadvance` du `.fnt`) | `FontCharWidths.csv` |
| `in_font` | bool | Ce code a-t-il produit une ligne `char` dans le `.fnt` | dérivé (faux si `codepoint` est `null`) |
| `duplicate_of_raw_code` | null | Toujours `null` depuis E15.e (plus de collision possible), gardé pour la forme du fichier | — |
| `reason` | string ou null | Pourquoi la case n'a pas de ligne `char` (`"no proven character"`), `null` sinon | dérivé |

## Extraits réels

`UI/font3-charset.json` :

```json
{
  "raw_code": 0,
  "codepoint": 0,
  "x": 0,
  "y": 0,
  "width": 16,
  "height": 16,
  "palette": 8,
  "advance": 16,
  "in_font": true,
  "duplicate_of_raw_code": null,
  "reason": null
}
```

Une entrée de `UI/font3-charset.json` où le rectangle n'est plus la case de la grille (`A`, code brut 65 :
case de la grille en (16, 64), 16×16 ; table du binaire : 7 de large) :

```json
{
  "raw_code": 65,
  "codepoint": 65,
  "x": 16,
  "y": 64,
  "width": 7,
  "height": 16,
  "palette": 8,
  "advance": 7,
  "in_font": true,
  "duplicate_of_raw_code": null,
  "reason": null
}
```

`UI/font3.fnt` (en-tête) :

```
info face="font3" size=16 bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0
common lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0
page id=0 file="Textures/font3.png"
chars count=145
```
