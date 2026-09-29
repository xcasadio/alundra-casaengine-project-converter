# Dialogues Yarn (`*.yarn`, `*.dialogue`)

Code : [`Text/YarnTextEmitter.cs`](../../alundra-casaengine-project-converter/Text/YarnTextEmitter.cs)
(le texte original vers le source Yarn),
[`Text/AlundraYarnFunctions.cs`](../../alundra-casaengine-project-converter/Text/AlundraYarnFunctions.cs)
(les déclarations des fonctions), [`Writers/YarnDialogueWriter.cs`](../../alundra-casaengine-project-converter/Writers/YarnDialogueWriter.cs)
(phase `Phase5.Yarn`), [`Writers/RawTextCleanup.cs`](../../alundra-casaengine-project-converter/Writers/RawTextCleanup.cs)
(retrait des tables brutes d'un export précédent), et le chargeur `dialogue` d'`AssetVerifier` (phase 8).
Décisions : [ADR-0006](../decisions/0006-alundra-text-is-authored-as-yarn.md),
[ADR-0007](../decisions/0007-falcon-update-keeps-the-state-it-replaces.md) ; plan :
[`plan-e15-yarn.md`](../plan-e15-yarn.md).

## Ce que c'est

Tout le texte d'Alundra (dialogues des cartes, table partagée `map_alundra`, table ETC des menus et
des objets) est écrit en source [Yarn Spinner](https://yarnspinner.dev) 3.2.1, puis compilé en un
`DialogueAsset` du moteur. Les codes de contrôle de l'original deviennent des marqueurs, des
commandes et des fonctions que la DLL Alundra interprète ; le moteur ne connaît rien d'Alundra.

C'est le seul format du texte dans le projet exporté (D-E15-4) : les anciennes tables de texte brut
(`Dialogues/global-strings.json`, `Dialogues/etc-index.json`, `Dialogues/control-codes.json` et les
`{Nom}-{id}.strings.json` de chaque carte) ne sont plus écrites, et un export sur place retire celles
d'un export précédent, par une liste fermée (compteur `Yarn.RawTextFilesRemoved`). La DLL lit
dialogues, OUI/NON et textes de l'inventaire dans ces assets (E15.c).

## Où c'est écrit

| Table | Source Yarn | Asset compilé | Nom au catalogue | Id (`Ids.For`) |
|---|---|---|---|---|
| Carte `n` (128 chaînes) | `Maps/{Zone}/{Name}-{n}/dialogues/{Name}-{n}.yarn` | même dossier, `{Name}-{n}.dialogue` | `dialogue_{n}` | `dialogue:map:{n}` |
| `map_alundra` (128 chaînes, bit `0x80` du numéro de texte à 0) | `Dialogues/Shared.yarn` | `Dialogues/Shared.dialogue` | `dialogue_shared` | `dialogue:shared` |
| ETC (1 024 index résolus par `EtcIndexTable.csv`) | `Dialogues/Etc.yarn` | `Dialogues/Etc.dialogue` | `dialogue_etc` | `dialogue:etc` |

Seul le `.dialogue` est catalogué ; le `.yarn` est sa source lisible, écrite en UTF-8 sans BOM avec
des fins de ligne LF. Sur le corpus complet : 485 fichiers de chaque (483 cartes, `Shared`, `Etc`).

## Nœuds et lignes

- **Un nœud par chaîne non vide** (`#Disuse` compris) : `M{n}_S{nnn}` (index 000-127),
  `Shared_S{nnn}`, `Etc_{iiii}` (index ETC en décimal, 0000-1023). Un emplacement vide n'a pas de
  nœud : la DLL ouvre alors une boîte vide, comme l'original.
- **Une ligne Yarn par page** (pages séparées par `\A`), d'identifiant `#line:{nœud}_p{k}` ; dans
  l'asset compilé, la clé est la forme de Yarn, `line:{nœud}_p{k}`.
- Les **commandes** d'une page précèdent sa ligne, dans l'ordre d'apparition de leur code.
- Le nœud de départ de l'asset est son premier nœud ; la DLL démarre toujours un nœud précis.

## Correspondance des codes

Chaque marqueur autofermant porte `trimwhitespace=false` (sans lui, Yarn avale l'espace qui suit).

| Original | Yarn |
|---|---|
| `\A` | fin de page (nouvelle ligne Yarn) |
| `\N` | `[br trimwhitespace=false/]` |
| `\<chiffres>` | `<<flag n>>` avant la ligne, `n` normalisé (`\0999` = `\999`) ; une commande par code, même répété |
| `\Y` | rien |
| `\B` `\C` `\D` `\E` `\F` `\G` | `[voice id=-1/]` … `[voice id=4/]` (entier) |
| `\H`, `\T` | `[center/]`, `[slow/]` |
| `\W<c>` | `[glyph id=N/]`, `N = c − 0x20` (chiffre) ou `c − 0x27` (lettre), formule de l'exécutable ; dessiné par la DLL |
| octets bruts `0x1A`, `0x1C` (descriptions d'objets) | `[glyph id=26/]` □, `[glyph id=28/]` ○ |
| `\V<n>` | `{game_var(n)}` |
| page avec au moins un `\X` | **une** `<<falcon_update>>` avant la ligne |
| `\X0` … `\X5` | fonction choisie par le code et sa place dans la page (tableau suivant) |
| page sans texte (fin de chaîne par `\A`, page qui ne pose qu'un drapeau) | `[empty trimwhitespace=false/]` |
| `:` `#` `[` `]` `{` `}` `/` `<` `>` | `\:` `\#` `\[` `\]` `\{` `\}` `\/` `\<` `\>` |
| espaces en bord de page | retirés (D-E15-8) ; un saut de ligne en bord de page est gardé |

Un code sans correspondance (absent du corpus) écarte son nœud avec une erreur de `report.json` ;
l'export ne lève jamais d'exception pour une donnée.

## Fonctions et commandes

Les fonctions sont déclarées au compilateur par `AlundraYarnFunctions.CreateDeclarations()` et
enregistrées à l'exécution par la DLL avec les mêmes types.

| Fonction | Code | Valeur | Délégué C# |
|---|---|---|---|
| `falcon_temp()` | `\X0` en tête de page | faucons temporaires gardés par `falcon_update` avant sa mise à jour | `Func<float>` |
| `falcon()` | `\X1` | faucons après la mise à jour | `Func<float>` |
| `category_item_name_before()` | `\X2`, `\X4` en tête de page | nom d'objet de l'indice de catégorie d'avant la mise à jour | `Func<string>` |
| `category_item_name()` | `\X2`, `\X4` après un autre `\X` | nom d'objet de l'indice courant | `Func<string>` |
| `category_threshold()` | `\X3` | seuil de l'indice courant | `Func<float>` |
| `category_remaining()` | `\X5` | seuil moins faucons | `Func<float>` |
| `game_var(n)` | `\V<n>` | `INT_ARRAY_80191908[n]` | `Func<float, float>` |

Commandes (non déclarées au compilateur) : `<<flag n>>` pose le drapeau temporaire `n` ;
`<<falcon_update>>` garde l'état qu'elle va changer, puis lance `UpdateNumberOfFalcon` et
`UpdatePlayerProgressState` (ADR-0007).

## Variables

Les variables Yarn sont les drapeaux d'Alundra (ADR-0010, ADR-0011) : ni Yarn ni le moteur ne gardent
de valeur à eux. `AlundraDialogueDirector` pose `AlundraYarnVariableStorage` sur chaque runner qu'il
crée.

| Nom | Drapeau | Type |
|---|---|---|
| `$flag_n` | drapeau `n` de `GameFlags` | booléen |
| `$tmp_flag_n` | drapeau `n \| 0x8000`, dans `TemporaryFlags` | booléen |

- **`n`** : décimal de 0 à 2047, en chiffres ASCII, sans zéro de tête ni signe, pour qu'un drapeau
  n'ait qu'un nom.
- **Lecture** : le bit `1 << (n & 0x1f)` du mot, le même test que les opcodes `0x30` et `0x31`. La
  machine de Yarn 3.2.1 demande la valeur avec `T = IConvertible` ; le stockage rend le booléen pour
  `bool`, `IConvertible` et `object`, et refuse les autres types.
- **Écriture** : `true` pose le bit comme l'opcode `0x05`, `false` l'efface comme `0x06` ; les autres
  bits du mot ne changent pas.
- **Refus** : chacun est journalisé une fois par nom, sans exception et sans changement d'état.
  - Cas refusés : tout autre nom, variables internes de Yarn comprises ; un `n` hors bornes ou écrit
    autrement ; une valeur texte ou nombre rangée sous un nom de drapeau ; la lecture d'un nom refusé.
  - Après une lecture refusée, la machine de Yarn prend la valeur initiale du programme compilé,
    déclarée ou posée implicitement par le compilateur. Elle ne lèverait que pour un nom absent de
    ces valeurs initiales, ce que le compilateur ne produit pas (tests d'E16.f T1).
- **`GetVariableKind`** : `Stored` pour un nom de drapeau, `Unknown` sinon.
- **`Clear()`** : ne touche aucune banque ; l'appel est journalisé. Le runner ne l'appelle pas.
- **Cycle de vie** : celui des banques. `TemporaryFlags` est vidée à chaque entrée de carte ;
  `GameFlags` est gardée, et sauvegardée sur 64 mots par `AlundraSaveGame` (ADR-0012). Rien de propre
  à Yarn n'est sauvegardé.
- **Corpus exporté** : aucun des 485 `.dialogue` ne lit ni n'écrit de variable
  (`AlundraYarnVariableCorpusTests`).
- **Limite** : le chemin dégradé de l'interpréteur, qui joue un nœud sans boîte
  (`AlundraEventProgramRunner.PlayNodeHeadlessToEnd`), crée son propre runner sans ce stockage. C'est
  sans effet tant que le corpus n'a pas de variable.

## Lire une ligne

- Dans une boîte de dialogue : `YarnDialogueRunner.Start(asset, nœud)`, avec les commandes et les
  fonctions enregistrées ; chaque ligne arrive au présentateur avec son texte et ses marqueurs.
- Hors dialogue (OUI/NON, noms et descriptions d'objets) : `DialogueAsset.TryGetLineText("line:Etc_0067_p0")`
  rend le texte **brut**, échappements compris (`\:` y reste) ; le passer par `YarnLineTextParser`
  (substitutions, puis `Parse`) comme le fait le runner.

## Compteurs de `report.json`

`Yarn.Files`, `Yarn.Nodes`, `Yarn.Lines`, `Yarn.EmptyPages`, `Yarn.GlyphMarkers`,
`Yarn.FlagCommands`, `Yarn.FalconUpdateCommands`, `Yarn.FunctionCalls`, `Yarn.EmptySlots`,
`Yarn.RawTextFilesRemoved` (tables brutes retirées d'un export précédent, toujours écrit), et
l'inventaire des codes `Yarn.Code.<code>` (`Yarn.Code.\A`, `Yarn.Code.\W2`, `Yarn.Code.U+001A`…),
qui remplace l'ancien `Dialogues/control-codes.json`. Valeurs sur le corpus complet : 485 fichiers,
24 784 nœuds, 31 757 lignes, 95 pages vides, 11 182 glyphes, 932 `flag`, 7 `falcon_update`,
20 appels de fonctions, 38 192 emplacements vides sans nœud. La phase 8 charge chaque `.dialogue`
(programme compilé et textes de ligne exigés) : `Verify.Loaded.dialogue`.

## Preuve d'équivalence

`YarnCorpusEquivalenceTests` rejoue chaque `.dialogue` exporté et compare chaque page (texte,
marqueurs, commandes, appels, `Speaker`) au décodeur de référence du texte original
(`ReferenceTextDecoder`, qui suit `TextDecoder.cs`) : 0 écart sur les 31 757 pages.

## Extrait réel

Carte 134 (église), chaîne 19. Original :

```text
\CRamène-moi \X3 Statuettes de faucons\Net je te récompenserai avec cela :\N\X4.\0100\Y
```

Source Yarn émis :

```text
title: M134_S019
---
<<falcon_update>>
<<flag 100>>
[voice id=0 trimwhitespace=false/]Ramène-moi {category_threshold()} Statuettes de faucons[br trimwhitespace=false/]et je te récompenserai avec cela \:[br trimwhitespace=false/]{category_item_name()}. #line:M134_S019_p0
===
```

Asset compilé `Maps/Church/Church (lobby)-134/dialogues/Church (lobby)-134.dialogue` (JSON écrit par
`DialogueAssetJsonSerializer`, id remplacé par `Ids.For("dialogue:map:134")` ; programme tronqué,
une seule entrée de `line_texts` sur 33) :

```json
{
  "id": "a53ca151-077e-5d6e-9e56-b75caa87f99c",
  "name": "dialogue_134",
  "type": "DialogueAsset",
  "version": 1,
  "schema_version": 1,
  "start_node": "M134_S000",
  "program_base64": "EnYKCU0xMzRfUzAwMBJpCglN…",
  "line_texts": {
    "line:M134_S019_p0": "[voice id=0 trimwhitespace=false/]Ramène-moi {0} Statuettes de faucons[br trimwhitespace=false/]et je te récompenserai avec cela \\:[br trimwhitespace=false/]{1}."
  }
}
```

Joué avec les fonctions, cette ligne donne le texte « Ramène-moi *seuil* Statuettes de faucons ↵
et je te récompenserai avec cela : ↵ *nom de l'objet*. », un marqueur `voice` d'identifiant 0 en
tête, et aucun `Speaker`.

## Limites

- Les espaces en bord de page sont perdus (Yarn les retire) ; aucun lecteur actuel n'en dépend.
- Les marqueurs `voice`, `center`, `slow` sont gardés pour la fidélité des dialogues (E12.c) ; la DLL
  les ignore à l'affichage tant qu'E12.c n'est pas faite.
- Le pluriel français intégré de Yarn Spinner 3.2.1 est faux : ne pas s'en servir.
