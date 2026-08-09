# Manifestes audio (`bgm-manifest.json`, `sfx-manifest.json`)

Code : [`Readers/SoundManifestReader.cs`](../../alundra-casaengine-project-converter/Readers/SoundManifestReader.cs),
[`Writers/AudioWriter.cs`](../../alundra-casaengine-project-converter/Writers/AudioWriter.cs)
(Phase 4).

## Ce que c'est

Les tables descriptives des 45 pistes de musique (BGM) et des 996 échantillons d'effets sonores
(SFX) extraits, avec chaque champ de la table d'origine plus l'id catalogue du WAV copié.

## Où c'est écrit

- `Musics/bgm-manifest.json` — un tableau, un élément par piste BGM.
- `Sounds/sfx-manifest.json` — un tableau, un élément par enregistrement SFX (`SfxRecord`),
  chacun pouvant contenir plusieurs « tones » (le jeu choisit un tone en fonction de la note jouée,
  donc le nombre de WAV copiés dépasse le nombre d'enregistrements).

Les WAV eux-mêmes sont copiés tels quels (aucun réencodage) sous `Musics/` et `Sounds/`.

## Pourquoi ici et pas comme asset CasaEngine

CasaEngine n'a **aucun type d'asset audio** : `AssetLoaderRegistry` n'enregistre aucun loader pour
`.wav`, et `CasaEngine.Framework.Audio.Sound` n'est qu'un wrapper runtime autour de `SoundEffect`,
sans forme sérialisée. Il n'y a donc rien à convertir les WAV *en*. Conformément à la règle du
convertisseur de ne jamais perdre de donnée source, ils sont copiés tels quels et enregistrés dans
le catalogue d'assets, pour qu'une future DLL de gameplay puisse résoudre un id de son vers un
fichier via le même catalogue que tout autre asset. Le manifeste est ce qui garde vivantes les
données que le moteur ne pourrait de toute façon pas représenter même s'il avait un type audio :
les points de boucle par tone (`SoundEffect` de MonoGame n'expose aucun point de boucle),
l'adressage VAB/SEQ, et l'analyse de niveau sonore des BGM.

## Schéma — `bgm-manifest.json`

Un enregistrement par piste, champs en `snake_case` :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `sound_index` | int | Index de la piste | `SoundIndex` (`sound/bgm.json`) |
| `file` | string | Nom du WAV copié dans `Musics/` | `File` |
| `frames` | int | Nombre de frames audio | `Frames` |
| `duration_seconds` | double | Durée en secondes | `DurationSeconds` |
| `loop_detected` | bool | La piste boucle-t-elle (détecté par l'extracteur) | `LoopDetected` |
| `peak_left` / `peak_right` | int | Amplitude crête par canal | `PeakLeft` / `PeakRight` |
| `rms_left` / `rms_right` | double | RMS par canal | `RmsLeft` / `RmsRight` |
| `first_audible_frame` | int | Première frame non silencieuse | `FirstAudibleFrame` |
| `asset_id` | guid | Id catalogue du WAV copié | ajouté par le convertisseur (`Ids.For("sound/bgm/{file}")`) |

Les champs d'analyse (`peak_*`, `rms_*`, `first_audible_frame`, `loop_detected`) sont des mesures
faites par l'extracteur sur le WAV décodé, pas des valeurs lues dans les données du jeu — mais
peu coûteuses à garder et potentiellement utiles à une future DLL (sauter le silence de tête, etc.).

## Schéma — `sfx-manifest.json`

Un enregistrement par `SfxRecord` (`sound/sfx.json`), noms de champ inchangés car leur sens vient
des structures VAB/SEQ du jeu :

| Champ | Type | Signification | Champ source |
|---|---|---|---|
| `id` | int | Id du sfx | `Id` |
| `vab_id` | int | Id de banque VAB | `VabId` |
| `program_number` | int | Numéro de programme VAB | `ProgramNumber` |
| `tone_number` | int | Numéro de tone dans le programme | `ToneNumber` |
| `note` | int | Note MIDI associée | `Note` |
| `seq_num` | int | Numéro de séquence SEQ | `SeqNum` |
| `ref_sfx_id` | int | Id de sfx référencé (alias) | `RefSfxId` |
| `max_voices` | int | Voix simultanées maximum | `MaxVoices` |
| `num_tones` | int | Nombre de tones attendus | `NumTones` |
| `skip_reason` | string ou null | Raison si l'extracteur n'a pas pu décoder ce record | `SkipReason` |
| `tones[]` | tableau | Les échantillons décodables de ce record | `Tones` |
| `tones[].tone_index` | int | Index du tone | `ToneIndex` |
| `tones[].file` | string | Nom du WAV copié dans `Sounds/` | `File` |
| `tones[].sample_rate` | int | Fréquence d'échantillonnage | `SampleRate` |
| `tones[].loop_start` / `loop_end` | int | Points de boucle VAG (offsets d'échantillon) | `LoopStart` / `LoopEnd` |
| `tones[].repeat` | bool | Le tone boucle-t-il | `Repeat` |
| `tones[].asset_id` | guid | Id catalogue du WAV copié | ajouté par le convertisseur |

Les 91 enregistrements que l'extracteur n'a pas pu décoder (`skip_reason` non nul) sont conservés
avec une liste `tones` vide plutôt que supprimés : les scripts du jeu adressent un sfx par id, donc
l'espace des ids doit rester complet, et la raison de l'échec est elle-même une donnée à conserver.

## Extrait réel

`Musics/bgm-manifest.json` :

```json
{
  "sound_index": 1,
  "file": "bgm_001.wav",
  "frames": 7966,
  "duration_seconds": 132.76666666666668,
  "loop_detected": true,
  "peak_left": 32767,
  "peak_right": 28689,
  "rms_left": 3756.873914551415,
  "rms_right": 3811.29227648468,
  "first_audible_frame": 34,
  "asset_id": "5e06303c-1cf6-5224-ad16-868aa09eaea4"
}
```

`Sounds/sfx-manifest.json` :

```json
{
  "id": 1,
  "vab_id": -1,
  "program_number": 0,
  "tone_number": 0,
  "note": 60,
  "seq_num": -1,
  "ref_sfx_id": 0,
  "max_voices": 2,
  "num_tones": 1,
  "skip_reason": null,
  "tones": [
    {
      "tone_index": 0,
      "file": "sfx_0001.wav",
      "sample_rate": 9604,
      "loop_start": 28,
      "loop_end": 1679,
      "repeat": false,
      "asset_id": "675b9a59-3c7b-507e-b0df-411cf8ec8e4f"
    }
  ]
}
```
