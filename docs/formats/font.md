# Police bitmap (`font3.fnt`, `font3-charset.json`)

Code : [`Writers/FontWriter.cs`](../../alundra-casaengine-project-converter/Writers/FontWriter.cs)
(Phase 5, seconde moitié).

## Ce que c'est

La police bitmap d'Alundra (`ui/font3.png` + `ui/font3.json`, 256 glyphes de 16×16) convertie en un
fichier **BMFont** (`.fnt`) qu'une bibliothèque de rendu de texte standard sait charger, plus une
table compagnon qui garde tout ce qu'un `.fnt` ne peut pas représenter.

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

Alundra indexe son atlas par un octet proche de CP850, mais les chaînes déjà extraites sont en
Unicode : un `.fnt` indexé sur les codes bruts du jeu ne pourrait afficher aucune d'entre elles — la
recherche de `'é'` (U+00E9) manquerait le glyphe stocké au code 130. La conversion est un portage de
`AlundraEngine.Text.TextDecoder.ConvertCp850ToLatin1` : identité en dessous de 128, table CP850 →
Latin-1 au-dessus. Seule cette branche est portée (celle de la version France/PAL) ; la même
fonction a une branche différente (`cp850 - 0x10`) pour la version USA, non pertinente pour les
données converties ici.

Les codes au-delà de 127 absents de la table CP850 gardent leur propre valeur comme point de code
(c'est ce que fait la fonction du jeu). Certains entrent alors en collision avec un code que la
table mappe déjà (le code brut 130 et le code brut 233 signifient tous deux U+00E9 'é') ; un `char
id` dupliqué serait un BMFont invalide, donc un des deux doit céder. Un code que la table nomme
l'emporte sur un code qui ne fait que garder sa propre valeur par défaut (la table est une preuve,
le repli est une supposition) ; entre deux codes de même statut, le plus petit l'emporte. Les
perdants ne produisent pas de ligne `char` mais restent visibles dans `font3-charset.json` avec
`duplicate_of_raw_code` renseigné. Sur les 256 glyphes source, 42 sont ainsi des doublons non
atteignables via le `.fnt` (`Font.Glyphs` = 214 dans `report.json`).

## Limitation connue : rendu monospace

Chaque `xadvance` du `.fnt` vaut 16px fixe, la largeur de cellule de l'atlas. Alundra dessine le
texte en proportionnel, en avançant de `g_fontCharWidthTable[code * 5]` — une table qui vit dans
l'exécutable du jeu et ne fait **pas** partie de `data-extracted`. C'est un vrai écart de fidélité,
pas un détail d'arrondi : du dialogue mis en page avec ces avances sera bien plus large et plus
lâchement espacé que l'original. Extraire cette table est le correctif.

## Schéma — `font3.fnt` (BMFont, format texte)

Sections standard BMFont :

- `info` — `face="font3" size=16 ...`.
- `common` — `lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 ...`.
- `page id=0 file="Textures/font3.png"` — chemin relatif au `.fnt`, avec des slashs directs.
- `chars count=N` — recalculé à partir des lignes réellement écrites (donc jamais faux même si des
  doublons ont été abandonnés).
- une ligne `char id=... x=... y=... width=... height=... xoffset=0 yoffset=0 xadvance=16 page=0 chnl=15`
  par glyphe retenu, `id` étant le point de code Unicode.

## Schéma — `font3-charset.json`

Un tableau, une entrée par glyphe source (256 entrées, triées par code brut) :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `raw_code` | int | Code brut du jeu (0–255) | `Code` (`ui/font3.json`) |
| `codepoint` | int | Point de code Unicode produit par la conversion CP850 → Latin-1 | dérivé de `Code` |
| `x`, `y` | int | Position du glyphe dans l'atlas 256×256 | `X`, `Y` |
| `width`, `height` | int | Dimensions du glyphe (16×16 attendu) | `Width`, `Height` |
| `palette` | int | Palette source (n'a nulle part où vivre dans un `.fnt`) | `Palette` |
| `in_font` | bool | Ce code a-t-il produit une ligne `char` dans le `.fnt` | dérivé (faux si un autre code a déjà pris ce point de code) |
| `duplicate_of_raw_code` | int ou null | Si `in_font` est faux, le code brut gagnant qui porte ce point de code | dérivé |

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
  "in_font": true,
  "duplicate_of_raw_code": null
}
```

`UI/font3.fnt` (en-tête) :

```
info face="font3" size=16 bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0 outline=0
common lineHeight=16 base=16 scaleW=256 scaleH=256 pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0
page id=0 file="Textures/font3.png"
chars count=214
```
