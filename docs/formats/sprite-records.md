# Fiches de sprite (`Data/sprite-records.json`)

Code : [`Writers/SpriteWriter.cs`](../../alundra-casaengine-project-converter/Writers/SpriteWriter.cs)
(`WriteSpriteRecords`, Phase 3), lu par la DLL dans
[`SpriteRecordCatalog.cs`](../../Alundra/Scripts/SpriteRecordCatalog.cs). Décisions :
[ADR-0005](../decisions/0005-inventory-portrait-data-path.md)
(portrait de l'inventaire) et [ADR-0039](../decisions/0039-the-dialogue-portraits-are-exported-and-linked-from-sprite-records.md)
(portraits de dialogue), plan [E19.f4a](../plan-e19-opcodes.md).

**Ce que c'est** : une fiche par préfab d'entité (395), qui garde les champs de l'en-tête d'un enregistrement de sprite
(`SpriteRecord.Header`) que le convertisseur n'interprète pas, pour que la DLL termine l'apparition d'une entité comme le fait
`EntityManager.InitializeEntity` : composer les drapeaux de l'entité, résoudre les emplacements de programme, dimensionner le volume
de corps.

**Où c'est écrit** : `Data/sprite-records.json`, un objet dont les clés sont les identifiants de préfab (ceux que portent les
enregistrements `PrefabAssetId` des cartes).

**Pourquoi ici et pas comme asset CasaEngine** : aucun type du moteur ne sait charger ces champs ; le fichier n'est pas catalogué.

## Schéma d'une fiche

| Champ | Type | Signification |
|---|---|---|
| `MoreFlags`, `CanPickup`, `FlagsPortraitShadowType` | int | Les trois octets d'en-tête que le jeu empaquette dans les drapeaux de l'entité (bits 0-7, 8-15, 16-23). Le bit `0x80` de `FlagsPortraitShadowType` (bit `0x800000` des drapeaux) dit que l'entité a un portrait de dialogue |
| `ProgramLoad`, `ProgramTick`, `ProgramTouch`, `ProgramDeactivate`, `ProgramInteract` | int | Indices d'emplacements de script |
| `OffsetX`, `OffsetY`, `OffsetZ`, `SizeX`, `SizeY`, `SizeZ` | int | Volume de corps : coin minimal relatif aux pieds, puis étendues en X, Y, Z |
| `Contents` | int | Lu par `GameEngine.InitializeContents` |
| `IdsvAnimDirs[]` | tableau | Une entrée par (animation, direction) convertie : `Anim`, `Direction`, `Frames[]` (valeur de tri en profondeur de chaque image affichée), `End` (`Loop`, `Hold` ou `Chain`), et `ChainTo` seulement quand `End` vaut `Chain` |
| `AnimSets[]` | tableau | Un en-tête par jeu d'animations : `Anim`, `Speed`, `Acceleration`, `IsZForceApplied`, `Sfx`, `Flags`, `Unknown` |
| `DialoguePortrait` | objet, facultatif | `{ SpriteAssetId, Width, Height }` : le `.sprite` du portrait de dialogue du locuteur et sa taille vraie en pixels (48 × 56, ou 48 × 72 pour les banques 122 et 162). **Omis** quand le bit `0x80` est éteint (370 fiches sur 395) ; présent exactement sur les 25 autres. Dernier champ de la fiche |

## `DialoguePortrait`

Le portrait vient du champ `DialoguePortrait` de l'enregistrement canonique de la banque (la première carte qui la porte), un quad de
la feuille de cette carte. Le convertisseur en fait un `.sprite` sous `UI/Portraits/sprite_<signature>.sprite` (même dossier, même
nom et même identifiant déterministe que le portrait de l'inventaire : `SpriteAssetId(feuille, signature)`), catalogué, et enregistre
son identifiant et sa taille ici. La banque 15 (Bonaire) a son portrait déjà exporté comme sprite d'animation : sa fiche pointe vers
cet identifiant et aucun second fichier n'est écrit. Les 25 identifiants sont ceux de
[`portraits_table.tsv`](../plan-e19-f4-annexe/portraits_table.tsv). Compteur du rapport : `Sprites.DialoguePortrait` (25). Un bit
`0x80` posé sans champ est un avertissement du convertisseur (aucun sur l'extraction actuelle), pas une erreur. La DLL lit le champ en
tolérant son absence (`SpriteRecordHeader.DialoguePortrait`, `DialoguePortraitRef`).

**Extrait réel** (banque 3, Yustel ; les tableaux `IdsvAnimDirs` et `AnimSets` sont abrégés) :

```json
{
  "24147828-80a5-59d9-b3b1-af4dfe9c3361": {
    "MoreFlags": 128,
    "CanPickup": 161,
    "FlagsPortraitShadowType": 131,
    "ProgramLoad": 0,
    "ProgramTick": 0,
    "ProgramTouch": 0,
    "ProgramDeactivate": 0,
    "ProgramInteract": 0,
    "OffsetX": -10,
    "OffsetY": -7,
    "OffsetZ": 0,
    "SizeX": 20,
    "SizeY": 14,
    "SizeZ": 32,
    "Contents": 0,
    "IdsvAnimDirs": [ { "Anim": 0, "Direction": 0, "Frames": [3, 3, 3, 3], "End": "Loop" } ],
    "AnimSets": [ { "Anim": 0, "Speed": 0, "Acceleration": 0, "IsZForceApplied": 0, "Sfx": 0, "Flags": 0, "Unknown": 0 } ],
    "DialoguePortrait": {
      "SpriteAssetId": "d2849bcc-5849-5e9b-b501-ace16721ed6a",
      "Width": 48,
      "Height": 56
    }
  }
}
```
