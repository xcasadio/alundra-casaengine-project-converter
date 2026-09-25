# Plan — Audio : mixage stéréo exact, son coupé, retrait d'`IsBgmActivated`

**État** : 🚧 **approuvé et en exécution** (mode AUTO) depuis le 2026-09-25. Rédigé et révisé le même jour ; enveloppe
**READY** (relecteur frais, après la révision de P6) ; tranche des phases 0 et 1 **READY** (relecteur frais, après la
correction du chemin éditeur de T1.3).
**Naissance** : décision de l'auteur du 2026-09-25, D-E13D-37 (`docs/plan-e13d-sous-inventaire.md` et
`docs/plan-e11b-opcodes-audio.md`, « Suites ouvertes », écrits en `c79455c` et mergés dans `main`) : trois suites
audio, dans une tâche dédiée hors E13.d.
**Approbation** : le 2026-09-25, l'auteur autorise P10 et choisit le mode **AUTO** (travail réversible dans le
périmètre de ce plan, un commit par tâche sur les branches dédiées, ni push ni merge ; seules écritures hors dépôt :
P10). O1 et P1 à P11 n'ont reçu aucune objection : ils sont appliqués tels quels.
**Modèle** : sections du modèle de plan (`CasaEngineMonogame/ai-agent/plan-template.md`, identique au modèle du skill
`plan`). Le dépôt parent n'a ni `AGENTS.md` ni `ai-agent/` ; ses plans vivent dans `docs/plan-*.md`, ce plan aussi.
Le plan propre au moteur est écrit dans `CasaEngineMonogame/ai-agent/tasks/` en T1.1 (règle de l'`AGENTS.md` du moteur,
précédents `bound-screens-tasks.md` et `asset-handles-migration-tasks.md`, qui ont chacun un plan parent).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Objectif

1. **Le mixage stéréo des bruitages devient exact.** Au départ d'une voix comme au remixage par `0xAB`/`0xBF`, chaque
   tonalité reçoit les volumes gauche et droit que l'original écrit dans le SPU, calculés par les formules de
   l'original, et le moteur les applique tels quels, canal par canal. Fin de la projection sur (volume, pan) de B1
   ([B1-a]) et de la déviation n°1 d'E11.a (toutes les voix à volume 1, centrées).
2. **Le moteur sait couper le son sur réglage du projet** : un réglage « son coupé » dans les réglages du projet
   (`AlundraGame.json` pour Alundra), appliqué au démarrage par le runtime et à l'ouverture du projet par l'éditeur,
   une propriété `IsMuted` sur `AudioSystemComponent`, et un convertisseur qui ne l'efface plus à l'export.
3. **`IsBgmActivated` disparaît de l'analyseur**, sans changer ce que fait l'analyseur.
4. Au passage, l'id de `0xBF` est lu sur deux octets comme dans l'exécutable (DLL et analyseur).

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-09-25)

Base : `main` `2e3506e` (moteur `43688074`, analyseur `bbf33962`). Le plan a été rédigé sur `a40e375` (moteur
`939109dc`, analyseur `9adba14d`) ; entre les deux, **aucun fichier du périmètre n'a changé**. Mesuré par
`git diff --stat` : côté parent, seuls `docs/plan-e11b-opcodes-audio.md` et `docs/plan-conversion-totale.md` (le
texte de D-E13D-37) ; côté moteur, le seul pointeur MGUI ; côté analyseur, le CSV du sous-inventaire (`8f403d5`) et
le pointeur MGUI. Dans le checkout principal, le moteur porte une modification de l'auteur
(`CasaEngine.Launcher/Program.cs`) : jamais indexée, jamais touchée. Le travail se fait dans ce worktree.

### Côté DLL (parent)

- Chaque tonalité démarre à volume 1, centrée : `new AudioVoiceParameters(AudioVoiceParameters.MaxVolume, 0f, 0f,
  tone.Repeat)` (`Alundra/Scripts/AlundraSoundPlayer.cs:193-194`). C'est la « déviation assumée n°1 » d'E11.a
  (`docs/plan-e11-audio.md:190-191`), faute de volume et de pan dans l'export.
- `RemixVoice` (`AlundraSoundPlayer.cs:219`) remixe **toutes** les voix vivantes de l'id demandé avec
  `ProjectMixToVolumeAndPan` (`:283`) : volume = max(G, D)/127, pan = (D−G)/(D+G), sans tonalité, sans programme.
- Dispatch : `0xAB` → `RemixVoice(v[1], v[2], v[3])` (`AlundraEventProgramRunner.cs:784-796`) ; `0xBF` →
  `RemixVoice(v[1], v[3], v[4])` (`:856-867`).
- Le faux backend des tests de la DLL gère les voix en flux mais `GetPendingBufferCount` y rend toujours 0
  (`Alundra.Tests/FakeAudioBackend.cs:158-189`). Une boucle « remplir tant que la file est courte » ne s'y
  arrêterait jamais.

### Côté moteur (`CasaEngineMonogame`)

- Couper un bus existe déjà : `AudioBus.IsMuted` (`CasaEngine/Framework/Audio/Mixing/AudioBus.cs:56`) met à 0 le gain
  effectif de tout ce qui descend du bus (`AudioMixer.cs:116`). C'est testé (`CasaEngine.Tests/Audio/AudioMixerTests.cs:47`)
  et utilisé par la démo audio (`CasaEngine.Demos/Demos/AudioDemo.cs:341`). **Aucune commande** ne l'expose : pas de
  réglage, pas de bouton, pas d'option. `AudioSystemComponent` n'offre que `MasterVolume` (`AudioSystemComponent.cs:46`).
  `ProjectSettings` n'a aucun champ audio. Aucun panneau de l'éditeur n'édite `ProjectSettings`.
- Réglages du projet : `ProjectSettings` (`CasaEngine/Framework/Configuration/Project/ProjectSettings.cs`), lus et
  écrits champ par champ par `ProjectSettingsHelper` (`ProjectSettingsHelper.cs:10-51` et `:53-93`, `JObject`). Un
  champ optionnel s'y ajoute sans casser les fichiers existants : `DialogueScreenAsset` est lu avec `?? string.Empty`
  et **n'est écrit que s'il est renseigné** (`:29`, `:80-83`).
- Chargement : le runtime charge le projet dans `CasaEngineGame.Initialize` **avant** de créer `AudioSystemComponent`
  (`CasaEngineGame.cs:342-366`). L'éditeur ouvre un projet plus tard, par `EditorProjectAuthoringService.LoadProject`,
  qui lève `ProjectLoaded` (`CasaEngine.EditorServices/EditorProjectAuthoringService.cs:15-24`). Il enregistre
  `GameSettings.ProjectSettings` (`:174`). Deux instances de `ProjectSettings` peuvent coexister :
  `ApplyDisplaySettings` met à jour les deux (`CasaEngineGame.cs:180-193`).
- `SetVoicePan` (`AudioService.cs:274`) ne change que le pan d'une voix mono. **Mesuré sur MonoGame 3.8.5.1
  DesktopGL décompilé (`ilspycmd`)** : pour une source mono, `PlatformSetPan` fait tourner la position OpenAL de
  `pan × π/3` sur le cercle unité. Les gains gauche/droite réels viennent donc de la loi de panoramique d'OpenAL
  Soft : **aucun (volume, pan) ne donne des gains G/D choisis**. Pour une source stéréo, `Pan = 0` pose les angles
  stéréo à ±π/6, le rendu de la musique aujourd'hui.
- Un clip résident est un `SoundEffect` MonoGame (`MonoGameAudioClip.cs`) : **les échantillons ne sont pas
  accessibles**, et `SampleRate`/`ChannelCount` y valent 0. Le chargeur passe par `SoundEffect.FromStream`
  (`SoundEffectLoader.cs`).
- Voix en flux : `AudioService.PlayStream` / `SubmitStreamBuffer` / `StartVoice` / `GetPendingBufferCount`
  (`AudioService.cs:148-205`), utilisés par `MusicPlayer` (tampons de 16 Kio, file cible de 3, `MusicPlayer.cs:23-26`,
  remplissage `:273-298`). Une voix en flux n'est jamais recyclée par `Update` : son nourrisseur la libère (`AudioService.cs:479-485`).
- **MonoGame refuse une voix en flux hors de 8 000 à 48 000 Hz** (`DynamicSoundEffectInstance`, décompilé :
  `ArgumentOutOfRangeException("sampleRate")`) ; `SoundEffect.FromStream` ne fait pas ce contrôle.
- `CasaEngine.MonoGame.sln` n'inclut pas `CasaEngine.Tests` : le builder explicitement avant `dotnet test --no-build`.

### Côté analyseur (`alundra-datas-analyser`)

- `IsBgmActivated` : déclaré `StaticVariables.cs:33` (`= true`), lu à `SoundManager.cs:433, :572, :652, :3749, :5580`.
  **Personne ne l'écrit** (`rg` sur tout l'analyseur) : le panneau de debug lie ses cases une par une
  (`AlundraGame/FrmGameDebugPanelController.cs:555-567`) et n'en a pas pour lui.
- Gestionnaire `0xBF` : `EntityEventHandlers.cs:3612-3617` (`// 80041CA0`), appelle
  `PlaySoundEffectWithToneVolumeMix(v[1], v[3], v[4])`.
- L'extracteur écrit `sound/sfx.json` (`AlundraDataExtractor/Program.cs:314-316`, `:413`). Par tonalité il ne garde que
  `ToneIndex, File, SampleRate, LoopStart, LoopEnd, Repeat` ; **ni volume ni pan de tonalité, ni volume ou pan de
  programme, ni volume maître du VAB** (vérifié aussi sur `data-extracted/sound/sfx.json` avec `jq`). Il a déjà des
  sous-commandes (`--render-bgm`, `--probe-portraits`…, `Program.cs:81-118`), mais aucune pour les seuls bruitages.
- **`Alundra.sln` ne builde pas à la base `bbf33962`, avant tout changement** (mesuré en T0.1) : 4 erreurs NU1605
  sur `AlundraGame.csproj` et `AlundraTools.csproj`. Le MGUI amené par `bbf3396` « update MGUI » exige MonoGame
  3.8.5.1 (`MGUI.Core`, `MGUI.MonoGame.LegacyRenderer`), et ces deux projets référencent encore 3.8.4.1. Hors
  périmètre, signalé à l'auteur (O5), non corrigé. Les projets que la tâche touche, `AlundraEngine` et
  `AlundraDataExtractor`, buildent seuls sans erreur. La validation de T2.1 à T2.3 est donc le build de ces deux
  projets, et l'échec de la solution doit rester à l'identique (mêmes 4 erreurs, rien de plus).
- L'analyseur n'a **aucun projet de test** (`AlundraTools/Alundra.sln`). Sa lecture de référence mixe en logiciel :
  `SpuMixerSoundPlaybackBackend`, gain = volume SPU `>> 14` (`SpuMixerSoundPlaybackBackend.cs:23, :229-230`). Le rendu
  des musiques passe par le même mélangeur (`AlundraDataExtractor/Program.cs:179, :569, :652`), sans normalisation
  trouvée (`rg -i normali`).

### Côté convertisseur

- La phase 0 **recrée `AlundraGame.json` à neuf** à chaque export : un `ProjectSettings` neuf, rempli de constantes,
  passé à `ProjectSettingsHelper.Save` (`Writers/ProjectWriter.cs:46-84`). Un réglage posé à la main dans le projet
  serait donc effacé par l'export suivant. Les autres écritures du fichier lisent puis réécrivent le `JObject`
  (`SetFirstWorldLoaded` `:96-115`, `SetDialogueScreenAsset` `:124-139`). Le fichier actuel n'a aucun champ audio.
- L'éditeur héberge un runtime qui a son propre `AudioSystemComponent` : l'aperçu des sons passe par
  `_editorRuntime?.AudioSystemComponent?.Service` (`CasaEngine.Editor/Controls/SoundAssetInspectorPanel.cs:289`) et la
  session de jeu par `game.AudioSystemComponent` (`CasaEngine.Editor/PlayMode/EditorPlaySessionController.cs:65, :101`).
- `SoundManifestReader` lit `sfx.json` dans `SfxRecord`/`SfxTone` (`Readers/SoundManifestReader.cs:33-63`) ;
  `AudioWriter` réécrit ces objets en `Sounds/sfx-manifest.json` (snake_case, `Writers/AudioWriter.cs:104-143`).
  **Un champ ajouté aux lecteurs passe donc tel quel dans le manifeste.**

### Données (`data-extracted/sound/sfx.json`, mesuré avec `jq`)

- 996 tonalités. Fréquences de 3 370 à 172 610 Hz : **72 sous 8 000 Hz et 1 au-dessus de 48 000 Hz, soit 73 hors de
  la plage des voix en flux de MonoGame**.
- 181 tonalités bouclées (`Repeat`). 171 enregistrements jouables ont `MaxVoices > 1`. 9 ont une séquence
  (`SeqNum >= 0`).
- `data-extracted/` et `alundra-project/` (hors `UI/Screens/`) sont ignorés par git. Le remaster ré-extrait le
  2026-09-19 a régressé (texte non décodé) : **aucun miroir `robocopy /MIR`** (mémoire du dépôt, 2026-09-24).

### Faits [binaire] (`ALUN_CD.EXE` France, capstone, 2026-09-25, en session principale)

- **Remix** `0x80049794` : relu instruction par instruction, **identique** à
  `SoundManager.PlaySoundEffectWithToneVolumeMixCore` (`:5229-5270`). Les constantes `0x80020009`/`>>13` (÷ 16 383) et
  `0x8418828d`/`>>11` (÷ 3 969) sont présentes. `(x+1)²−1` est appliqué au mix G/D, au volume de tonalité et au volume
  du programme (octet 1 de `ProgAtr`). Les poids de pan de tonalité sont élevés au carré. Tonalités et programme sont
  ceux de la fiche **résolue**, les voix sont cherchées par l'id **demandé** et l'index de tonalité. `SetVoiceVolume`
  (`0x80095298`) a un seul appelant, cette fonction.
- **`0xAB`** (`0x80041290`) : `(v[1], v[2], v[3])`, taille 4. **`0xBF`** (`0x80041ca0`) : **id = `v[1] | (v[2] << 8)`**,
  mix `(v[3], v[4])`, taille 5. La décompilation, le port et la consigne du 2026-09-25 ignorent `v[2]`.
- **Départ d'un bruitage** : `TriggerVoice` (`0x80094660`) n'a que deux appelants, tous deux dans `PlaySoundEffect`
  (`0x80049280`, `0x8004952c`), avec `(fine 0, 0x7f, 0x7f)`. Le volume de départ suit donc une seule formule :
  `FUN_80090c58` (`SoundManager.cs:4851-4935`), ou `FUN_800914cc` pour une tonalité `Vag == 0xff`. **Leur relecture
  instruction par instruction reste à faire** (T0.2).
- **`IsBgmActivated` n'existe pas dans l'exécutable** :
  - `FUN_8004b114` (`0x8004b114`) et `LoadBgm` (`0x80049b7c`) n'ont pas la sortie anticipée
    `!IsBgmActivated && …` ;
  - `StopAllSound` (`0x80049af4`) appelle `SetSeqVolume` puis `PlaySeq` **sans condition** ;
  - **`LoadMapSequence` (`0x80049be0`) et le cas 5 de `HandleMapSoundStreaming` (`0x8004b580-0x8004b5d8`) n'appellent
    jamais `PlaySeq`** : le `else if (IsBgmActivated) PlaySeq(...)` de la décompilation n'a pas de contrepartie.
    `LoadMapSequence` n'appelle pas non plus `SetSeqVolume`.
  - `PlaySeq` (`0x8008f188`) n'a que deux appelants : `0x80049428` et `StopAllSound`.
- **Volume SPU** : « Voice volume/2 (-4000h..+3FFFh) », appliqué `(ENVX × VOLX) >> 15`
  ([psx-spx, SPU](https://psx-spx.consoledev.net/soundprocessingunitspu/)). **Gain linéaire = registre / 0x4000**, la
  même échelle que le mélangeur de l'analyseur (`>> 14`).

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Trois suites audio de D-E13D-37, dans une tâche dédiée hors E13.d : mix stéréo exact, son coupé par le moteur, retrait d'`IsBgmActivated` (auteur, 2026-09-25). |
| D2 | « Exact » couvre **le départ et le remix** : le départ d'une voix suit `FUN_80090c58` (ou `FUN_800914cc`), le remix suit `0x80049794`. Le son du bateau change donc (niveau et pan par tonalité). Précision de l'auteur : « corriges aussi le jeu original ». **Lecture retenue, à confirmer à l'approbation** : sa règle du 2026-09-25 s'applique à ce chemin. Un défaut avéré de l'original y est corrigé, preuve binaire écrite en commentaire et au plan. Dans le doute (défaut ou choix voulu), question à l'auteur. |
| D3 | Mécanisme moteur : **voix stéréo logicielle**. Le moteur garde les échantillons mono et nourrit une voix stéréo en flux, gains G/D appliqués à chaque échantillon. Décision d'architecture moteur, avec ADR (auteur, 2026-09-25). |
| D4 | Son coupé : **réglage persistant** lu au démarrage par le runtime, que le jeu soit lancé par le Launcher ou par l'éditeur, plus `AudioSystemComponent.IsMuted` (bus `Master`). Ni bouton d'éditeur, ni raccourci, ni option de ligne de commande (auteur, 2026-09-25). **Le réglage vit dans le projet**, pas dans les fichiers locaux de l'utilisateur (auteur, 2026-09-25, second échange). |
| D5 | Écarts binaire/décompilation (auteur, 2026-09-25) : l'id de `0xBF` passe à `v[1] \| (v[2] << 8)` dans la DLL et dans l'analyseur. `IsBgmActivated` est retiré **sans changer le comportement de l'analyseur**. Les `PlaySeq` absents du binaire et `0xA7` sont consignés en suite dédiée. |
| D6 | Un défaut du moteur se corrige dans le moteur, jamais contourné dans la DLL ou le convertisseur. Un manque de MGUI ou du moteur se consigne, il ne se contourne pas (consigne de l'auteur, mémoires du dépôt). |
| D7 | Validation : `Alundra.Tests`, `CasaEngine.Tests` (buildé explicitement), tests du convertisseur, six goldens identiques à l'octet, recette de l'auteur en jeu sur le bateau (ronflements, mouettes, trappe, musique). Depuis D2, **l'oracle du bateau change de nature pour les bruitages** : « fidèle à l'original », et non plus « comme avant ». La musique, elle, n'est pas touchée et reste un oracle de non-régression. |

## Points à valider par l'auteur (arbitrages proposés)

| Réf | Proposition | Pourquoi |
|---|---|---|
| P1 | Branches `chantier/audio-mix-exact`, créées depuis `main` `2e3506e` (parent), moteur `43688074`, analyseur `bbf33962`, c'est-à-dire les commits enregistrés par `main`. | Tâche indépendante d'E13.d, qui est mergée dans `main` avec le texte de D-E13D-37. |
| P2 | Gain moteur = registre SPU / 16 384. | psx-spx et l'échelle `>> 14` du mélangeur de l'analyseur, avec lequel la musique a été rendue : bruitages et musique restent sur la même échelle. |
| P3 | Les 73 tonalités hors de 8 000 à 48 000 Hz sont rééchantillonnées par un facteur entier : plus petit k tel que fréquence × k ≥ 8 000, par interpolation linéaire ; au-dessus de 48 000 Hz, moyenne de k échantillons. Les 923 autres jouent à leur fréquence d'origine, rééchantillonnées par OpenAL comme aujourd'hui. **Déviation déclarée.** | MonoGame refuse une voix en flux hors de cette plage. Changer le chemin des 923 autres serait un changement sans demande. |
| P4 | Tampons d'environ 20 ms, file cible de 3, **au plus 3 tampons soumis par voix et par `Update`**. Un changement de gain s'entend donc jusqu'à environ 60 ms plus tard. **Déviation déclarée** : l'original change le registre immédiatement. | Voix en flux. La borne par `Update` protège aussi contre un backend dont la file ne se remplit jamais (le faux backend de la DLL). |
| P5 | `MonoGameAudioClip` garde en mémoire les échantillons 16 bits des WAV PCM mono au chargement, à côté du `SoundEffect`. | Simple et déterministe, allocation au chargement seulement. Coût : la taille PCM des clips chargés (26 Mo pour les 996 WAV d'Alundra, si tous étaient chargés). |
| P6 | Champ `ProjectSettings.IsAudioMuted` (catégorie « Audio », faux par défaut, hors `#if !FINAL`). Lu avec `?? false` ; **écrit seulement s'il est vrai**, comme `DialogueScreenAsset`, pour qu'un projet non coupé garde un fichier identique à l'octet. Appliqué au bus `Master` à la construction d'`AudioSystemComponent` (runtime) et à l'ouverture d'un projet dans l'éditeur. Le setter `IsMuted` coupe le bus et met à jour les réglages du projet en mémoire, les deux instances comme `ApplyDisplaySettings`, pour qu'un enregistrement de l'éditeur le garde. Le runtime n'écrit jamais le fichier. On bascule le réglage en éditant `AlundraGame.json` (aucune interface, D4). Couper `Master` coupe aussi l'aperçu des sons dans l'éditeur (le bus `Editor` en descend). **Le convertisseur garde le réglage** : sa phase 0 relit `IsAudioMuted` dans le fichier existant avant de le recréer (T1.5). | D4 : le réglage vit dans le projet. Sans la relecture en phase 0, chaque export l'effacerait (`ProjectWriter.cs:46-84`). |
| P7 | Plusieurs instances vivantes du même bruitage : le remix touche, pour chaque tonalité, **la voix vivante la plus ancienne** de ce (id demandé, tonalité). Approximation de « le premier emplacement SPU » de l'original, que le port ne peut pas reproduire : les voix de la séquence musicale y occupent aussi les emplacements. T0.2 mesure si une cible de `0xBF` a `MaxVoices > 1`. Si aucune, la question ne se pose pas. | Aujourd'hui le port remixe toutes les instances, ce que l'original ne fait pas. |
| P8 | La boucle reste « tout le clip » (déviation n°2 d'E11.a inchangée), même si le nourrisseur rendrait les points de boucle faciles. | Hors des trois suites demandées. Consigné en suite. |
| P9 | Nouveaux champs de `sfx.json` : par enregistrement `VabMasterVolume`, `ProgramVolume`, `ProgramPan` ; par tonalité `Volume`, `Pan` (snake_case dans `sfx-manifest.json`). **Valeurs de la fiche résolue** : celles du VAB, du programme et des tonalités dont les échantillons ont été exportés (T2.3). **Représentation de l'absence** : entiers nullables d'un bout à l'autre. `null` dans `sfx.json` pour une fiche **non résolue** (`SkipReason` « invalid », « map VAB not resolvable », « sequence-triggered »), alors qu'une fiche résolue sans tonalité (« no tones ») garde les vraies valeurs de son programme ; `null` dans `sfx-manifest.json` quand le `sfx.json` source n'a pas le champ ; jamais 0 par défaut. Même forme que `skip_reason: null` aujourd'hui. Si un champ est `null` pour une fiche jouée, la DLL revient au jeu d'aujourd'hui (volume 1, centré) et l'écrit une fois dans le journal. | Format de données : ADR parent en T3.3. Un champ absent ne doit pas rendre le jeu muet en silence (relecture de la tranche 2-3). |
| P10 | **Écritures hors dépôt** (précédent D-E13C-5), avec sauvegarde et retour arrière : l'agent lance l'extraction des seuls bruitages vers le scratchpad ; il sauvegarde les deux `sfx.json` actuels avec leur SHA-1 ; il vérifie qu'ils sont identiques ; il copie **le seul `sound/sfx.json`** dans `data-extracted/sound/` (T3.1), puis dans `Alundra Remake/remaster-data-extracted/sound/` **seulement après le verdict CONFIRMED de T3.4**. Jamais de `robocopy /MIR`. L'auteur l'autorise en approuvant ce plan, ou exécute T3.1 lui-même. | Le remaster a régressé ; seul ce fichier doit bouger, et il doit pouvoir revenir à l'octet près. |
| P11 | Le drapeau mono de l'original (`DAT_sound_801f7658 == 1`, `FUN_80090c58`) n'est pas porté : stéréo toujours. | Option du jeu, sans équivalent dans le port. |

## Règles d'exécution pour l'agent

- **Branches dédiées** (P1) dans ce worktree : parent `chantier/audio-mix-exact`, et la même dans chaque sous-module
  touché. Jamais de commit sur `main`/`master`. **Jamais de push**. À la fin, les branches des sous-modules sont
  rapatriées par `git fetch` dans les dépôts du checkout principal.
- **Sous-modules du worktree** : `git clone --no-checkout` depuis le checkout local de l'auteur, puis `checkout` du
  commit enregistré, sous-modules imbriqués compris (MGUI, NvgSharp). C'est le précédent `e13d-sub-inventory`. Vérifier
  `git -C <sous-module> rev-parse --show-toplevel` avant tout travail. Pour écrire dans un sous-module, un script dans
  le scratchpad au besoin (mémoire « worktree du sous-module moteur »).
- **Une seule tâche à la fois** : `⏳` → `🚧`. En fin de tâche : validation, puis `✅`, `🧪` ou `⚠️`, note de validation
  sous la tâche, **commit dédié** qui inclut ce plan (ou le plan moteur pour une tâche moteur). Message en anglais
  `type(area): summary`. `git add` fichier par fichier.
- **Jamais de commit avec `cd /d/...` devant** (le hook anti-`main` refuse) : `git -C <chemin>`.
- **Tests au premier plan**, `--blame-hang-timeout 60s`, timeout Bash 600 000. Si un run dure des minutes, un test
  boucle : le corriger, ne jamais attendre. **Mutations faites en vrai** par un script qui remplace un extrait, builde,
  lance le filtre et rend le fichier à l'octet. Jamais simulées dans le test.
- **Commandes** :
  - parent : `dotnet build alundra-casaengine-project-converter.slnx` ;
    `dotnet test Alundra.Tests/Alundra.Tests.csproj --blame-hang-timeout 60s` ;
    `dotnet test alundra-casaengine-project-converter.Tests --blame-hang-timeout 60s`.
  - moteur : `dotnet build CasaEngineMonogame/CasaEngine.MonoGame.sln`, `dotnet build
    CasaEngineMonogame/CasaEngine.Editor.MonoGame.sln`, `dotnet build CasaEngineMonogame/CasaEngine.Tests/CasaEngine.Tests.csproj`,
    puis `dotnet test CasaEngineMonogame/CasaEngine.Tests/CasaEngine.Tests.csproj --no-build --blame-hang-timeout 60s`.
  - analyseur : `dotnet build alundra-datas-analyser/AlundraTools/AlundraEngine/AlundraEngine.csproj` et
    `dotnet build alundra-datas-analyser/AlundraTools/AlundraDataExtractor/AlundraDataExtractor.csproj` ; plus
    `Alundra.sln`, dont l'échec préexistant (4 × NU1605, T0.1) doit rester identique.
- **Export** : en place dans `alundra-project/` **du worktree**, depuis `data-extracted/` du checkout principal ; jamais
  précédé d'une suppression. Preuve par manifeste SHA-1 (`manifest.py` des chantiers précédents, dans un scratchpad
  antérieur) : ligne de base avant, diff prédit écrit avant, diff mesuré inclus dans le prédit, second export ⊆
  `{report.json}`. Jamais d'export pendant qu'`Alundra.Tests` tourne.
- **Moteur** : pas d'allocation, de LINQ ni de closure dans `Update` ; changements d'API additifs seulement ;
  sérialisation additive ; doc XML courte sur les nouveaux membres publics.
- **Vérifications indépendantes** (unité à risque : format de données et acceptation entre composants) : un
  `verifier` frais à la fin du moteur (T1.4), à la frontière de l'export (T3.4) et sur l'intégration finale (T5.1).
  Chaque verdict se traite selon ses priorités avant de continuer.

## Arrêts

Un golden qui bouge. Un fichier de l'export hors de la prédiction. Un seul WAV qui diffère entre la ré-extraction et
`data-extracted/`. Un `sfx.json` qui change autrement que par les champs ajoutés. `AlundraLogicClock` modifié.
`CasaEngine.Launcher/Program.cs` indexé. `alundra-project/` supprimé. Un `robocopy /MIR`. Côté moteur : une API
publique existante modifiée ou un comportement existant changé, une suite moteur avec un nouvel échec, un changement
nécessaire dans MGUI (à consigner), un bump de pointeur sans son commit de sous-module. Un défaut probable de
l'original dont la nature est douteuse (D2). Deux `sfx.json` qui diffèrent avant la copie (T3.1). **Sur arrêt : si
T3.1 a déjà copié, retour arrière de T3.1 d'abord ; puis ⚠️ Blocked, question dans « Points ouverts », fin.**

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Builds : parent, moteur (deux solutions et `CasaEngine.Tests`), analyseur, sans erreur.
- `CasaEngine.Tests` : ligne de base de T0.1 + les nouveaux tests, **zéro nouvel échec**.
- `Alundra.Tests` : ligne de base + nouveaux tests, verts ; **six goldens identiques à l'octet** (aucun n'exécute
  `0xAB`/`0xBF`, fait 10 d'E11.b ; les goldens enregistrent des ids de son, pas des gains).
- Tests du convertisseur : ligne de base + n, verts.
- Export complet prouvé (T3.3), second export ⊆ `{report.json}`.
- Recette de l'auteur (T5.3).

---

## Phase 0 — Préparation et mesures

### ✅ T0.1 — Environnement et lignes de base — faite le 2026-09-25

- Objectif : les branches, les sous-modules du worktree, les comptes de tests et l'export de référence, avant toute
  modification.
- Fichiers : ce plan.
- Étapes :
  1. `git switch -c chantier/audio-mix-exact main` dans le worktree (depuis `2e3506e` ; le plan, non suivi, suit).
  2. Clones locaux des sous-modules au commit enregistré, imbriqués compris. Branche `chantier/audio-mix-exact` dans le
     moteur (depuis `43688074`) et dans l'analyseur (depuis `bbf33962`).
  3. Builds et suites : noter les comptes de `CasaEngine.Tests`, `Alundra.Tests` et du convertisseur, et les échecs
     préexistants éventuels.
  4. Premier export complet vers `alundra-project/` du worktree, depuis `data-extracted/` du checkout principal, puis
     manifeste de référence. Vérifier avant le **sens** de tout écart entre `data-extracted/` et le remaster (mémoire
     du 2026-09-24).
- Validation : comptes et empreinte du manifeste écrits sous la tâche.
- Commit : `docs(plan): plan the exact stereo mix, the project mute setting and the IsBgmActivated removal`

**Note de validation (2026-09-25).**

- **Branches.** Parent `chantier/audio-mix-exact` depuis `2e3506e`. Moteur, cloné localement à `43688074` (MGUI
  `c5d5a09e`, NvgSharp `9c0da031`), branche `chantier/audio-mix-exact`. Analyseur, cloné à `bbf33962` (MGUI
  `c5d5a09e`), même branche. `rev-parse --show-toplevel` pointe chaque clone. Script : `scratchpad/setup_wt.sh`.
- **Builds.** `alundra-casaengine-project-converter.slnx` : 0 erreur. `CasaEngine.MonoGame.sln` : 0 erreur.
  `CasaEngine.Editor.MonoGame.sln` : 0 erreur. `CasaEngine.Tests.csproj` : 0 erreur. `Alundra.sln` de l'analyseur :
  **4 erreurs NU1605 préexistantes** (O5), alors qu'`AlundraEngine` et `AlundraDataExtractor` buildent seuls sans
  erreur.
- **Lignes de base.** `Alundra.Tests` **1228 / 1228**. Convertisseur **190 / 190**. `CasaEngine.Tests` **1888 / 1888**.
  Aucun échec préexistant.
- **Export de référence.** Vers `alundra-project/` du worktree, depuis `data-extracted/` du checkout principal, avec
  `dotnet run --no-build --no-launch-profile` (le profil de lancement vise le projet du checkout principal).
  Vérification **PASSED** (19 534 chargés, 2 389 contrôlés), 7 avertissements de données habituels. Manifeste de
  référence `scratchpad/baseline/m_ref.tsv` : **23 245 fichiers**.
  - Face au projet du checkout principal, seuls `report.json` et 6 fichiers de `UI/Screens/` diffèrent. Ces écrans
    sont suivis par git et ne diffèrent que par les retours chariot.
  - Sens de l'écart vérifié avant l'export : `data-extracted/data/map_389.json` a le texte décodé, le remaster a
    `o}i`. C'est bien le remaster qui a régressé.

### ✅ T0.2 — Mesures dans le binaire et dans le corpus — faite le 2026-09-25

- Objectif : fermer les faits dont dépendent T2.3, T4.2 et T4.3, avant tout code.
- Sources : `ALUN_CD.EXE` (France) avec capstone, scripts dans le scratchpad ; décompilation ; programmes d'événements
  du corpus.
- Étapes :
  1. Relire instruction par instruction `FUN_80090c58` (`0x80090c58`), la préparation du volume et du pan de
     `TriggerVoice` (`0x80094660`), et `FUN_800914cc` (`0x800914cc`). Contre la décompilation : ordre des
     opérations, `div` contre `divu`, largeurs, constantes. Consigner chaque écart. Un écart qui est un défaut de
     l'original : proposer la correction (D2). En cas de doute : question.
  2. Relever `DAT_sound_801f7658` (drapeau mono, P11) : qui l'écrit dans l'exécutable.
  3. Corpus : toutes les occurrences de `0xBF` et de `0xAB`. Pour chacune : `v[2]` (combien sont non nuls, donc
     combien d'ids changent avec D5), l'id cible, son `MaxVoices` (P7), la carte. Script jetable, méthode et
     résultat écrits sous la tâche.
  4. Choisir une carte atteignable où `0xBF` s'exécute, pour la recette de l'auteur (O3).
- Validation : chaque fait marqué [binaire] ou [mesuré], avec adresse ou commande.
- Commit : `docs(plan): record the binary checks of the voice key-on path and the 0xBF corpus`

**Note de validation (2026-09-25).** Méthode : un workflow de cinq agents en lecture seule. Deux lecteurs à l'aveugle
par groupe de fonctions binaires, un balayeur de corpus ; scripts et relevés dans `scratchpad/t02/`. Rapports
complets : `scratchpad/t02-readers.txt`. Réconciliation et conclusions en session principale ; les deux lecteurs de
chaque groupe concordent sur chaque point retenu.

1. **Départ d'une voix de bruitage [binaire].**
   - `TriggerVoice` (`0x80094660`) : les deux appels de bruitage passent `(0, 0x7f, 0x7f)`. La branche « égal » pose
     donc toujours volume de voix `0x7f` et pan de voix `0x40`. Volume et pan de programme sont lus aux octets +1 et
     +4 de `ProgAtr`, volume et pan de tonalité aux octets +2 et +3 de `VagAtr`, **comme dans la décompilation**.
   - `FUN_80090c58` (`0x80090c58`) : volume maître du VAB = octet `+0x18` de l'en-tête (`lbu`). Chaîne
     `(0x7f × ((Mvol << 14) − Mvol)) / 0x3f01`, puis `× ProgMvol × ToneVol / 0x3f01`, puis les pans de tonalité, de
     programme et de voix, chacun sur son propre canal, puis le drapeau mono, puis `v² / 0x3fff` sur chaque canal.
     **Identique à la décompilation, étape par étape.**
   - Écarts sans effet sur des entrées de 0 à 0x7f : la première division est signée (`div`) et les autres non
     signées (`divu`) ; la garde d'entrée de la décompilation (`voiceId`, `TryGetCurrentVabContext`) n'existe pas
     dans le binaire ; la branche `sequenceKey != 0x21` ajoute `(clé >> 8) × 172` au pointeur, mais une voix de
     bruitage ne la prend jamais (`TriggerVoice` pose la clé `0x21`).
   - **O2 fermé : aucun défaut, aucune perte à corriger sur ce chemin.**
2. **Drapeau mono `DAT_sound_801f7658` [binaire].** Trois écritures dans tout l'exécutable :
   - `FUN_8009299c` à `0x80092c84`, qui le met à 0 ;
   - `FUN_8008f994`, qui le met à 0, appelé une seule fois par `InitializeSoundSystem` à `0x800485b4` (la
     décompilation a commenté cet appel) ;
   - `FUN_8008f980`, qui le met à 1, mais **n'a aucun appelant** (ni `jal`, ni `j`, ni pointeur littéral dans le
     fichier).

   Le chemin mono est donc mort dans cet exécutable : **P11 (stéréo toujours) est fidèle.**
3. **`FUN_800914cc` (tonalité `Vag == 0xff`) [binaire].** La décompilation se trompe :
   - le binaire atténue chaque canal par lui-même (`0x800915f8`, `0x80091664`, `0x80091698`) là où le C# croise les
     canaux (`SoundManager.cs:4971`, `:4981`, `:4985`) ;
   - le binaire n'a **aucune** mise au carré, alors que le C# en fait une dans le bloc mono (`:5009-5010`).

   **O4 fermé : défaut de décompilation, pas de l'original.** Le port ne rencontre pas ce chemin : pour
   `Vag == 0xff`, l'extracteur ne trouve pas d'échantillon (`TryGetVagBodyRange`) et abandonne la tonalité
   (`SoundBin.cs:411-415`). T2.3 le confirme par un comptage.
4. **Corpus [mesuré].** 483 fichiers d'événements, balayage structuré (entrées A à F, branches suivies, tailles lues
   dans `EventOpcodeSizeTable.cs`).
   - **`0xAB` : 0 occurrence atteignable.** Les 68 octets 0xAB du balayage naïf sont tous des opérandes ou du code
     mort.
   - **`0xBF` : 672 occurrences sur 68 cartes.** `v[2]` vaut 0 dans 643 cas, 1 dans 21, 2 dans 8. Six ids changent
     avec D5 : 302 (le port lit 46), 303 (47), 306 (50), 352 (96), 391 (135), 514 (2).
   - 21 ids distincts sont ciblés. `MaxVoices > 1` pour trois d'entre eux, 30 (3), 45 (4) et 154 (2) : P7 s'applique
     à ceux-là.
5. **Scène de recette (O3) [mesuré, vérifié à l'octet en session principale].**
   - **Ship Klark (intérieur) — 390**, la pièce atteinte depuis le pont du bateau : un seul `0xBF` à l'octet 735,
     `2E 01 40 40`, donc **id 302** (le son bouclé du bateau) et mix `0x40`/`0x40`. Le port actuel remixe l'id 46.
   - **Inoa — 162** : les ambiances 200 et 203 descendent par paliers (`C8 00 50 50` → `3C` → `28` → `14` → `00`) ;
     `0xBF` y sert de fondu de sortie.
6. **À part, hors périmètre.** `TriggerVoice` range dans `DAT_801f76aa/ab` les octets +6/+7 de `VagAtr`, que
   `SoundBin.cs` nomme `Min`/`Max`, alors que la décompilation les étiquette `Pbmin`/`Pbmax` (+0x0C/+0x0D). Sans effet
   sur le volume ; consigné pour la plage de pitch-bend.

---

## Phase 1 — Moteur (sous-module `CasaEngineMonogame`)

### ✅ T1.1 — Plan moteur et ADR — faite le 2026-09-25 (moteur `7968a608`)

- Objectif : le plan moteur exigé par l'`AGENTS.md` du moteur, et les deux décisions en ADR.
- Fichiers (moteur) : `ai-agent/tasks/audio-stereo-voices-mute-tasks.md` (copie des tâches T1.2 et T1.3,
  consommateur : ce plan) ; `ai-agent/README.md` (ligne du tableau) ; `docs/decisions/` : une ADR « voix stéréo
  logicielles » (D3, P3, P4, P5), une ADR « son coupé, réglage du projet » (D4, P6 : champ additif de
  `ProjectSettings`, écrit seulement s'il est vrai), par le skill `adr`.
- Validation : relecture ; les index sont à jour.
- Commit (moteur) : `docs(audio): plan and record the software stereo voices and the persisted mute`

### ✅ T1.2 — Voix stéréo logicielle — faite le 2026-09-25 (moteur `7c017ae5`, note dans le plan moteur)

- Objectif : jouer un clip mono sur une voix stéréo dont les gains gauche et droit sont exacts, et modifiables
  pendant la lecture.
- Fichiers (moteur) : `CasaEngine/Framework/Audio/` (nouvelle interface d'accès aux échantillons ; nourrisseur
  interne, à la manière de `MusicPlayer`, possédé et mis à jour par `AudioService`) ;
  `Backends/MonoGameAudioClip.cs` et `Assets/Loaders/SoundEffectLoader.cs` (P5) ; `AudioService.cs` ;
  `CasaEngine.Tests/Audio/` ; `docs/engine/audio-system.md`.
- Contrat public (additif) :
  - `AudioVoiceHandle PlayClipStereo(IAudioClip clip, string busName, in AudioVoiceParameters parameters, float
    leftGain, float rightGain, object owner = null)`. `Volume` et `IsLooped` s'appliquent ; `Pan` et `Pitch` sont
    ignorés (doc). Rend `None`, avec un journal limité, si le clip n'expose pas ses échantillons, si le backend ne
    fait pas de flux, ou s'il refuse la voix.
  - `void SetVoiceStereoGains(AudioVoiceHandle voice, float leftGain, float rightGain)` et
    `GetVoiceStereoGains`. Gains bornés à [0, 1], appliqués à partir du prochain tampon soumis (P4).
  - Échantillon produit : `G = arrondi(s × leftGain)`, `D = arrondi(s × rightGain)`, saturés en 16 bits. Le
    `Volume × gain de bus` reste appliqué par le chemin existant (`ApplyGain`, volume du backend), donc à l'identique
    sur les deux canaux.
  - Fréquence d'origine si elle est dans [8 000, 48 000] Hz, sinon rééchantillonnage entier (P3).
  - Fin d'un clip non bouclé : quand le dernier tampon est joué, la voix est arrêtée et libérée (`IsAlive` faux).
    `Stop`, `StopVoicesOwnedBy`, `StopAll`, `StopAllExceptBus`, `Pause`, `Resume` : comme pour toute voix ; le
    nourrisseur oublie les voix mortes.
  - `Update` sans allocation (tampons de travail préalloués), au plus 3 tampons par voix et par appel (P4).
- Étapes :
  1. Tests d'abord, sur `FakeAudioBackend` et `FakeAudioClip` du moteur (ce dernier gagne les échantillons) :
     - échantillons G/D exacts, gains asymétriques et nuls ;
     - un changement de gain n'affecte que les tampons suivants ;
     - gain de bus 0,5 : volume du backend = volume × 0,5, échantillons inchangés ;
     - boucle ; fin et libération ;
     - clip à 3 370 Hz et à 172 610 Hz ;
     - arrêt par propriétaire ;
     - un backend dont la file ne se remplit jamais ne fait pas boucler `Update` ;
     - clip sans échantillons → `None`.
  2. Implémentation.
  3. Mutations en vrai : G et D inversés ; gain appliqué au tampon déjà en file ; volume de bus cuit dans les
     échantillons ; borne par `Update` retirée. Chacune doit faire tomber au moins un test.
- Validation : les deux solutions du moteur buildent ; `CasaEngine.Tests` = ligne de base + n, zéro nouvel échec.
- Commit (moteur) : `feat(audio): play mono clips on software stereo voices with exact left/right gains`

### 🧪 T1.3 — Son coupé, réglage du projet — faite le 2026-09-25 (moteur `af5246ca`) ; contrôle en direct de l'éditeur non observable par l'agent, reporté à T5.3 (note dans le plan moteur)

- Objectif : D4 avec P6, côté moteur.
- Fichiers (moteur) :
  - `CasaEngine/Framework/Configuration/Project/ProjectSettings.cs` (`IsAudioMuted`) et `ProjectSettingsHelper.cs`
    (lecture `?? false`, écriture seulement si vrai) ;
  - `CasaEngine/Framework/Application/Components/AudioSystemComponent.cs` (réglage appliqué à la construction depuis
    `CasaEngineGame.RuntimeContext.ProjectSettings`, public, `CasaEngineGame.cs:70` ; propriété `IsMuted` sur le bus `Master`, qui met à jour les réglages en
    mémoire, les deux instances) ;
  - côté éditeur (jamais dans le runtime, `AGENTS.md` §9.9) :
    - un petit abonné dans `CasaEngine.EditorServices`, constructible dans un test : il reçoit l'`AudioMixer` à couper,
      s'abonne à `EditorProjectAuthoringService.ProjectLoaded` et se désabonne à sa libération (`IDisposable`,
      l'événement est statique) ;
    - à chaque ouverture de projet, il applique `IsAudioMuted` au bus `Master`, lu sur l'instance de `ProjectSettings`
      que `LoadProject` a remplie. Attention : `LoadProject` charge dans `runtimeContext?.ProjectSettings` mais lève
      l'événement avec `GameSettings.ProjectSettings` (`EditorProjectAuthoringService.cs:18-24`). L'exécuteur vérifie
      quelle instance `GameEditor` passe, et le test reproduit cet appel ;
    - `CasaEngine.Editor/GameEditor.cs` crée cet abonné avec `_editorRuntime.AudioSystemComponent.Mixer` juste après
      le runtime hébergé (`:1029`) et le libère avec l'éditeur. C'est une ligne de câblage que les tests ne peuvent
      pas atteindre (`GameEditor` ne se construit pas dans un test) : elle est prouvée par le contrôle en direct
      ci-dessous et par la recette T5.3 ;
  - tests ; `docs/engine/audio-system.md` (comment couper le son d'un projet).
- Étapes :
  1. Tests. La logique testable ne dépend pas d'un `Game` : une petite fonction qui applique un `ProjectSettings` à
     un `AudioMixer`, appelée par le composant et par l'abonné.
     - Aller-retour `Save`/`Load` avec `IsAudioMuted` vrai.
     - Projet sans le champ → faux. Et `Save` d'un projet non coupé ne contient pas la clé : le fichier est identique
       à celui d'avant le changement.
     - Réglage vrai appliqué → gain effectif de `Master` à 0, volume 0 poussé aux voix vivantes par `Update` ; retour
       à faux → volumes restitués.
     - Setter `IsMuted` → les réglages en mémoire suivent, puis `Save` écrit la clé.
     - **Preuve unique du chemin éditeur** : dans `CasaEngine.Tests/EditorServices/`, collection
       `ProjectEnvironmentCollection` comme `EditorProjectAuthoringServiceTests`. Un abonné sur un `AudioMixer` neuf ;
       le vrai `EditorProjectAuthoringService.LoadProject` sur un projet coupé → `Master` coupé ; puis sur un projet non
       coupé → rétabli. Après libération de l'abonné, un nouveau `LoadProject` ne change plus rien.
  2. Implémentation.
  3. Mutations en vrai, chacune doit faire tomber un test : réglage lu mais non appliqué au bus ; clé écrite même quand
     elle est fausse ; abonnement à `ProjectLoaded` retiré de l'abonné ; valeur lue sur la mauvaise instance de
     `ProjectSettings` si les deux diffèrent dans l'appel de `GameEditor`. La ligne de câblage de `GameEditor` n'est
     couverte par aucun test : c'est dit dans la note de validation.
- Validation : les deux solutions du moteur buildent ; `CasaEngine.Tests` vert. En direct, sur le projet du worktree
  avec `"IsAudioMuted": true` puis sans la clé :
  - Launcher du worktree : silence, puis son ;
  - éditeur du worktree, projet ouvert : l'aperçu d'un son reste muet, puis s'entend.
  Si l'un des deux contrôles ne peut pas être fait par l'agent, la tâche reste 🧪 avec ce qui manque, et le contrôle
  passe à la recette T5.3.
- Commit (moteur) : `feat(audio): mute the whole mix from the project settings`

### ✅ T1.4 — Vérification du moteur et pointeur — faite le 2026-09-25 (moteur `716c02c7`)

- Objectif : un `verifier` frais sur T1.2 et T1.3 (contrat ci-dessus, diff du moteur, tests), puis le pointeur du
  moteur dans le parent.
- Étapes : verdict traité par priorité ; puis bump du pointeur ; build du parent ; `Alundra.Tests` inchangé.
- Validation : verdict **CONFIRMED** consigné ; comptes du parent identiques à T0.1.
- Commit : `chore(submodules): point at the engine with software stereo voices and the project mute setting`

**Note de validation (2026-09-25).**

- **Vérificateur frais : CONFIRMED.** Portée : les commits moteur `7968a608`, `7c017ae5` et `af5246ca` contre la base
  `43688074`. Il a rejoué les builds (0 erreur) et les tests (`CasaEngine.Tests` 1914 / 1914). Aucun constat P0 à P2.
- **Deux remarques reportées**, consignées en O1 et O2 du plan moteur (`716c02c7`) :
  - A1, P3 : `GetVoiceStereoGains` peut encore répondre `true` pour une voix arrêtée, jusqu'au `Update` suivant. Le
    portage écarte les voix mortes avant tout remix : sans effet ici.
  - A2, P4 : l'abonné de l'éditeur manque le tout premier chargement de projet. Le constructeur
    d'`AudioSystemComponent` applique alors le réglage déjà chargé : comportement juste.
- **Parent sur le nouveau moteur.** `alundra-casaengine-project-converter.slnx` : 0 erreur. `Alundra.Tests`
  **1228 / 1228** et convertisseur **190 / 190**, inchangés depuis T0.1.
  - `HeroTraceHarnessTests` réécrit les quatre `docs/hero-trace-389-*.txt` à chaque passage. Le contenu est
    identique, seules les fins de ligne changent ; les fichiers sont remis à leur état suivi.

### ✅ T1.5 — Le convertisseur garde le réglage « son coupé » à l'export — faite le 2026-09-25

- Objectif : P6, côté convertisseur : un `"IsAudioMuted": true` posé à la main dans `AlundraGame.json` survit à
  l'export.
- Fichiers (parent) : `alundra-casaengine-project-converter/Writers/ProjectWriter.cs` (`CreateEmptyProject` relit le
  champ dans le fichier existant avant de le recréer ; sans fichier, ou sans champ, faux) ; tests du convertisseur.
- Étapes :
  1. Tests : fichier existant avec `IsAudioMuted` vrai → vrai après `CreateEmptyProject` ; sans fichier ou sans champ →
     clé absente du fichier recréé ; fichier existant illisible → recréé sans la clé, avec un avertissement dans le
     rapport.
  2. Implémentation (lecture du `JObject`, comme `SetFirstWorldLoaded`, sans `ProjectSettingsHelper.Load` et ses effets
     de bord).
  3. Mutation en vrai : relecture retirée → le premier test tombe.
  4. Export complet. Prédiction écrite avant : seul `report.json` change (le projet actuel n'a pas la clé, donc
     `AlundraGame.json` reste identique à l'octet). Puis second export ⊆ `{report.json}`. Puis la même chose avec
     `"IsAudioMuted": true` posé à la main : la clé est toujours là après l'export.
- Validation : tests du convertisseur = ligne de base + n ; les preuves d'export écrites sous la tâche.
- Commit : `feat(project): keep the project's mute setting across exports`

**Note de validation (2026-09-25).**

- **Code.** `ProjectWriter.CreateEmptyProject` relit `IsAudioMuted` dans le fichier existant avant de le recréer
  (`ReadExistingAudioMute`, lecture du `JObject` comme `SetFirstWorldLoaded`). Un fichier illisible est recréé sans la
  clé, avec un avertissement dans le rapport ; seules les exceptions de lecture sont capturées.
- **Tests.** Les trois tests échouaient ou passaient comme prévu avant le changement (2 échecs, 1 déjà vert).
  Convertisseur **193 / 193** (190 + 3). Mutation réelle « relecture retirée » : tuée.
- **Exports.** Trois exports complets vers le projet du worktree, prédiction écrite avant
  (`scratchpad/export_t15.sh`, relevés dans `scratchpad/t15/`). Vérification PASSED à chaque export.
  - Base → A : `{report.json}`.
  - A → B : `{report.json}`.
  - `AlundraGame.json` identique à l'octet, puisque le projet n'a pas la clé.
  - Avec `"IsAudioMuted": true` posé à la main, l'export C garde la clé (`True`), puis le fichier est restauré à
    l'octet (SHA-1 `65e33ec4…`).
- **Trace à part.** Le manifeste de base comptait un fichier de plus que celui de T0.1 : `editor-diagnostics.txt`,
  écrit dans le projet par les lancements de l'éditeur en automatisation de T1.3. Ce n'est pas une sortie du
  convertisseur ; il a été supprimé.

---

## Phase 2 — Analyseur (sous-module `alundra-datas-analyser`)

### ✅ T2.1 — Retrait d'`IsBgmActivated` — faite le 2026-09-25 (analyseur `18a8546`)

- Objectif : D5, sans changer le comportement de l'analyseur.
- Fichiers : `AlundraTools/AlundraEngine/StaticVariables.cs` ; `AlundraTools/AlundraEngine/Sound/SoundManager.cs`.
- Étapes :
  1. Supprimer la propriété.
  2. Supprimer les deux sorties anticipées `!IsBgmActivated && …` (`:433`, `:652`), absentes de l'exécutable.
  3. `StopAllSoundCore` : `PlaySeq` sans condition (`:3749`), comme `0x80049b60`.
  4. `LoadMapSequenceCore` (`:572`) et cas 5 du streaming (`:5580`) : `else PlaySeq(...)`, avec un commentaire qui
     dit que l'exécutable n'a pas cet appel (adresses), renvoyant à la suite S1.
- Validation : `rg IsBgmActivated alundra-datas-analyser` vide ; `AlundraEngine` et `AlundraDataExtractor` buildent ;
  `Alundra.sln` garde son échec préexistant à l'identique.

**Note de validation (2026-09-25).** `rg IsBgmActivated alundra-datas-analyser` ne rend rien. `AlundraEngine` et
`AlundraDataExtractor` : 0 erreur. `Alundra.sln` : les mêmes 4 erreurs NU1605 qu'à la base, sur `AlundraGame` et
`AlundraTools`, rien d'autre. Diff : `SoundManager.cs` (−26/+9), `StaticVariables.cs` (−1).
- Commit (analyseur) : `refactor(sound): remove the IsBgmActivated switch the executable never had`

### ✅ T2.2 — Id de `0xBF` sur deux octets — faite le 2026-09-25 (analyseur `8348d7f`)

- Fichiers : `AlundraTools/AlundraEngine/Gameplay/Scripts/EntityEventHandlers.cs:3612-3617`.
- Étapes : `variables[1] | (variables[2] << 8)`, commentaire [binaire] `0x80041ca0`.
- Validation : `AlundraEngine` builde ; `Alundra.sln` garde son échec préexistant à l'identique.

**Note de validation (2026-09-25).** `AlundraEngine` : 0 erreur ; `Alundra.sln` : les mêmes 4 erreurs NU1605. Une ligne
changée, avec un commentaire qui cite `0x80041CA0`.
- Commit (analyseur) : `fix(scripts): opcode 0xBF reads a 16-bit sound effect id as the executable does`

### ✅ T2.3 — Attributs VAB dans `sfx.json`, et extraction des seuls bruitages — faite le 2026-09-25

- Objectif : P9 côté source, et une sous-commande qui n'écrit que `sound/sfx.json` et les WAV des bruitages.
- Fichiers :
  - `AlundraTools/AlundraEngine/Sound/SoundBin.cs` : la résolution vit là (`TryResolveSfxVab` est privée et suit la
    chaîne `RefSfxId`) ;
  - `AlundraTools/AlundraDataExtractor/Program.cs`.
- Mécanisme (relecture de la tranche 2-3) :
  - Côté `SoundBin.cs`, en ajouts seulement :
    - `SfxToneSample` gagne `Volume` et `Pan`, les octets `VagAtr.Vol`/`Pan` de la tonalité décodée ;
    - une surcharge `DecodeSfxTones(int sfxid, out SfxProgramAttributes attributes, bool is8Bit = false)` rend aussi,
      pour la fiche **résolue**, son id, `VabHdr.Mvol` du VAB dont viennent les échantillons, et
      `ProgAtr.Mvol`/`Mpan` de son programme ;
    - la signature existante est gardée et appelle la surcharge ;
    - `TryResolveSfxVab` rend en plus l'index de la fiche résolue (paramètre privé ajouté).
  - Côté `Program.cs` :
    - `SfxToneExport` gagne `int? Volume, int? Pan` ;
    - `SfxExportRecord` gagne `int? VabMasterVolume, int? ProgramVolume, int? ProgramPan`, ajoutés **en fin de
      record** pour ne pas déplacer les champs existants ;
    - les valeurs viennent de la surcharge, donc de la fiche résolue, jamais de `SfxRecords[sfxid]` non résolue ;
    - une fiche non résolue (`SkipReason`) reçoit `null` partout ;
    - l'extraction affiche le nombre de fiches exportées depuis une fiche sœur de la chaîne (id résolu ≠ id
      demandé), et combien ont changé de VAB en route. Mesure seulement : l'ordre d'ouverture des VAB qui décide de
      la sœur est un comportement préexistant, consigné en O6 s'il y a des cas.
- Étapes :
  1. Code `SoundBin.cs`, puis `Program.cs`.
  2. Sous-commande `--extract-sfx <gamePath> <outputPath>`, sur le modèle de `--render-bgm`, qui ne fait que l'export
     des bruitages (même fonction `ExtractDataFromSoundBin`).
- Validation :
  - `AlundraEngine` et `AlundraDataExtractor` buildent ; `Alundra.sln` garde son échec préexistant à l'identique.
  - La sous-commande tourne vers le scratchpad (chemin du jeu relu dans le `launchSettings.json` de l'extracteur).
  - Les cinq champs sont présents et non nuls sur toutes les fiches qui ont des tonalités, et `null` sur les fiches
    `SkipReason`. L'histogramme des valeurs et le compte des fiches sœurs sont écrits sous la tâche.
  - Contrôle croisé indépendant (harnais hors dépôt, lecture des en-têtes VAB par des offsets bruts) sur au moins une
    fiche directe et une fiche redirigée par la chaîne : les cinq champs égalent les octets du VAB, du programme et
    des tonalités de la fiche résolue.
  - Tonalités abandonnées : aucune aujourd'hui. Mesuré le 2026-09-25 : chaque fiche résolue exporte exactement
    `NumTones` tonalités, donc aucune tonalité `Vag == 0xff` ; le compte est refait sur le nouveau `sfx.json`.
- Commit (analyseur) : `feat(extractor): export the VAB volume and pan attributes of every sound effect`

**Note de validation (2026-09-25).**

- **Code.**
  - `SoundBin.cs` : `SfxToneSample` gagne `Volume`/`Pan` ; `SfxProgramAttributes` ; surcharge
    `DecodeSfxTones(sfxid, out attributes)`, la signature d'origine étant gardée ; `TryResolveSfxVab` rend l'index
    résolu (l'ancienne surcharge privée, devenue sans appelant, est retirée).
  - `Program.cs` : les cinq champs ajoutés en fin de record, avec `null` si la fiche n'est pas résolue ; sous-commande
    `--extract-sfx`, qui passe par le même `CreateGameEngine`/`InitializeEngine`/`ExtractDataFromSoundBin` que
    l'extraction complète ; ligne de console qui compte les fiches sœurs.
- **Builds.** `AlundraEngine` et `AlundraDataExtractor` : 0 erreur. `Alundra.sln` : les mêmes 4 erreurs NU1605.
- **Extraction** vers `scratchpad/t31-extract`, depuis `AlundraTools/AlundraTools` (le `log.txt` qu'elle y laisse est
  supprimé). Résultat : 870 / 961 bruitages, 996 WAV, les mêmes comptes qu'avant.
- **Contrôle croisé indépendant** (`scratchpad/t23_crosscheck.py`, octets bruts de `SOUND.BIN` aux offsets du format
  VAB). Les cinq champs égalent le VAB, le programme et les tonalités de la fiche résolue sur :
  - des fiches directes : 1 et 61 (VAB global), 300 et 302 (VAB 56), 162 (VAB 12) ;
  - des fiches redirigées : 790→869 (VAB 63) et 795→854 (VAB 60).
- **Présence des champs.** Les 870 fiches à tonalités ont les cinq champs non nuls. Les 82 fiches non résolues sont
  `null` partout. Les 9 fiches résolues sans tonalité gardent leurs valeurs, d'où la précision apportée à P9. Chaque
  fiche résolue exporte exactement `NumTones` tonalités, donc aucune tonalité abandonnée.
- **Histogramme [mesuré].**
  - `VabMasterVolume`, `ProgramVolume` et `ProgramPan` valent **127, 127 et 64 sur toutes les fiches**.
  - Seuls le volume et le pan de tonalité varient. Volume de tonalité : de 0 à 127 ; **15 tonalités à 0**, dont la
    seconde du son 162 à 172 610 Hz, que le port joue aujourd'hui à plein volume. Pan de tonalité : 64 sur 768
    tonalités, de 0 à 127 sur les autres.
- **O6 [mesuré].** 6 fiches ont reçu les échantillons d'une sœur d'un autre VAB : 791→803, 793→810, 792→832, 800→834,
  795→854, 790→869, toutes du VAB 50 vers les VAB 52 à 63. **Aucune carte n'utilise le groupe 50**
  (`Maps/sound-group-index.json`). Ces entrées ne sont donc jamais jouées telles quelles : l'original comme la DLL
  suivent la chaîne jusqu'au groupe courant. O6 est fermé sans décision de l'auteur.

---

## Phase 3 — Données et convertisseur (parent)

### ✅ T3.1 — Ré-extraction ciblée et copie prouvée (P10) — faite le 2026-09-25

- Objectif : un `sfx.json` enrichi dans `data-extracted/`, sans rien toucher d'autre, et restaurable à l'octet près.
- Fait du 2026-09-25 : les deux copies actuelles sont identiques, SHA-1 `d5f5ae023276beeeb10b1ded955265f00fec0630`
  (`data-extracted/` daté du 2026-09-02, remaster du 2026-09-19).
- Étapes :
  1. `--extract-sfx` vers le scratchpad.
  2. **Sauvegarde** : copier `data-extracted/sound/sfx.json` et `remaster-data-extracted/sound/sfx.json` dans
     `<scratchpad>/t3.1-backup/` (`data-extracted.sfx.json`, `remaster.sfx.json`), et écrire leurs SHA-1 dans
     `<scratchpad>/t3.1-backup/SHA1SUMS` et sous cette tâche.
  3. **Garde** : les deux SHA-1 sont égaux entre eux. Sinon : ⚠️ Blocked, question à l'auteur, aucune copie.
  4. Preuve : chaque WAV identique à l'octet à `data-extracted/sound/sfx/` ; le nouveau `sfx.json`, sans les champs
     ajoutés, identique à l'actuel (`jq`, clés triées).
  5. Copie du seul `sfx.json` dans `data-extracted/sound/`. **Pas encore dans le remaster** : il n'est copié qu'après
     le verdict CONFIRMED de T3.4.
- **Retour arrière** : sur tout arrêt ultérieur (section « Arrêts ») ou tout verdict de T3.4 autre que CONFIRMED,
  remettre `data-extracted/sound/sfx.json` (et le remaster s'il a déjà été copié) depuis la sauvegarde. Vérifier que le
  SHA-1 est revenu à celui de l'étape 2, et l'écrire sous la tâche.
- Validation : SHA-1 avant et après la copie, et les deux preuves, écrits sous la tâche. Pas de commit propre
  (données hors git) ; la note part dans le commit de T3.2.

**Note de validation (2026-09-25).**

1. **Extraction.** `--extract-sfx` (analyseur `b79b45a`) vers `scratchpad/t31-extract` : 870 / 961 bruitages,
   996 WAV.
2. **Sauvegarde.** `scratchpad/t3.1-backup/` : `data-extracted.sfx.json` et `remaster.sfx.json`, avec `SHA1SUMS`.
3. **Garde.** Les deux SHA-1 sont égaux : `d5f5ae023276beeeb10b1ded955265f00fec0630`.
4. **Preuves.** `diff -rq` ne trouve aucune différence entre les 996 WAV régénérés et ceux de
   `data-extracted/sound/sfx/`. Le nouveau `sfx.json`, une fois retirés les cinq champs, est identique à l'ancien
   (`jq -S`).
5. **Copie** dans `data-extracted/sound/sfx.json` seulement : SHA-1 `de01c95f1a003b78e013f5fdadf022bcc970f8d4`. Le
   remaster reste à `d5f5ae02…` jusqu'au verdict de T3.4.

### ✅ T3.2 — Pointeur de l'analyseur — faite le 2026-09-25

- Validation : parent buildé ; `Alundra.Tests` et convertisseur inchangés.
- Commit : `chore(submodules): point at the analyser without IsBgmActivated and with the VAB attributes`

**Note de validation (2026-09-25).** Analyseur `b79b45a`. Parent : 0 erreur ; `Alundra.Tests` **1228 / 1228** ;
convertisseur **193 / 193**. Les traces `docs/hero-trace-389-*.txt`, réécrites en LF par les tests, sont remises à
leur état suivi.

### ✅ T3.3 — Le convertisseur porte les attributs, export prouvé — faite le 2026-09-25

- Fichiers : `alundra-casaengine-project-converter/Readers/SoundManifestReader.cs` (`SfxRecord`, `SfxTone`) ; tests du
  convertisseur ; ADR parent `docs/decisions/` (format de `sfx-manifest.json`, P9), par le skill `adr`.
- Étapes :
  1. Tests :
     - un enregistrement portant les cinq champs → `sfx-manifest.json` porte les valeurs exactes, y compris un vrai 0 ;
     - un ancien `sfx.json` sans ces champs → `sfx-manifest.json` les écrit à `null`, jamais à 0 ;
     - une fiche `SkipReason` avec `null` → `null`.
  2. Champs ajoutés, en `int?` dans `SfxRecord`/`SfxTone`, avec un lecteur qui rend `null` pour une propriété absente
     ou `null` (le `GetInt32` actuel rend 0). L'ADR parent enregistre cette représentation (P9).
  3. Prédiction écrite **avant** l'export : modifiés `Sounds/sfx-manifest.json` et `report.json` ; possible
     `AlundraGame.json` (leçon de B3) ; rien d'autre.
  4. Export complet, diff du manifeste, second export.
- Validation : tests du convertisseur = ligne de base + n ; diff mesuré ⊆ prédit ; second export ⊆ `{report.json}`.
- Commit : `feat(audio): carry the VAB volume and pan attributes into the sound effect manifest`

**Note de validation (2026-09-25).**

- **Code.** `SfxRecord` gagne `VabMasterVolume`, `ProgramVolume` et `ProgramPan`, et `SfxTone` gagne `Volume` et
  `Pan`, tous en `int?`. `GetNullableInt32` rend `null` pour une propriété absente ou `null`.
- **Tests.** Convertisseur **194 / 194**, dont `ConvertAudio_CarriesTheVabAttributesAndKeepsMissingOnesNull` sur une
  fiche non résolue, un ancien format sans les champs, et une fiche avec un vrai 0.
- **Mutations réelles**, toutes tuées : absent lu comme 0 ; volume de tonalité non lu.
- **ADR.** ADR-0003 du parent enregistre le format.
- **Exports.** Prédiction écrite avant (`scratchpad/export_t33.sh`, relevés dans `scratchpad/t33/`). Vérification
  PASSED aux deux exports.
  - Base → A : `Sounds/sfx-manifest.json` et `report.json` seulement.
  - A → B : `{report.json}`.
- **Manifeste.**
  - 162 : `(127, 64)` puis `(0, 0)`, la tonalité muette de l'original.
  - 302 : `(100, 34)` et `(100, 94)`.
  - 790 : `(90, 64)`, valeurs de la sœur 869.
  - Aucune fiche jouée n'a de champ `null`.

### ✅ T3.4 — Vérification de la frontière de données — faite le 2026-09-25

- Objectif : un `verifier` frais sur la chaîne extracteur → `sfx.json` → manifeste. Les cinq champs doivent égaler
  les octets du VAB, du programme et des tonalités de la **fiche résolue**, lus indépendamment, pour quelques
  bruitages tirés au hasard. Parmi eux, au moins une fiche directe et une fiche redirigée par la chaîne. Et les
  `null` doivent aller là où P9 les attend.
- Étapes : sur **CONFIRMED** seulement, copier le `sfx.json` de T3.1 dans `remaster-data-extracted/sound/` et vérifier
  que son SHA-1 est égal à celui de `data-extracted/sound/sfx.json`. Sur tout autre verdict : retour arrière de T3.1,
  puis ⚠️ Blocked.
- Validation : verdict et SHA-1 consignés. Pas de commit si rien ne change (note versée au commit suivant).

**Note de validation (2026-09-25).**

- **Vérificateur frais : CONFIRMED.** Son propre script (`scratchpad/t34-verify/check.py`) lit les octets bruts de
  `SOUND.BIN` et refait la résolution de l'extracteur. Couverture complète, pas un échantillon : les 961 fiches (879
  résolues, 82 non résolues) et les 996 tonalités, **0 écart**.
  - Les chaînes redirigées se limitent aux six connues. Les 82 fiches non résolues ont `null` ; les 9 fiches sans
    tonalité ont leurs vraies valeurs.
  - WAV identiques ; le fichier sans les cinq champs redonne `d5f5ae02…` ; le manifeste est égal à `sfx.json` champ
    par champ ; convertisseur 194 / 194.
- **Deux remarques P4, reportées.** Les valeurs au niveau de la fiche sont uniformes (127/127/64) et ne peuvent donc
  pas trahir un mauvais programme ; seules les tonalités le peuvent, et elles concordent toutes. Le manifeste n'est pas
  suivi par git, mais son contenu est celui que produit `ea1dbfd`.
- **Sur CONFIRMED, copie vers le remaster.** `Alundra Remake/remaster-data-extracted/sound/sfx.json` passe de
  `d5f5ae02…` à `de01c95f1a003b78e013f5fdadf022bcc970f8d4`, identique à `data-extracted/`. Plus aucune écriture hors
  dépôt n'est prévue ; la sauvegarde reste dans `scratchpad/t3.1-backup/`.

---

## Phase 4 — DLL (`Alundra`)

### ✅ T4.1 — La banque de sons lit les attributs — faite le 2026-09-25

- Fichiers : `Alundra/Scripts/AlundraSoundBank.cs` (`SfxToneRecord`, `SfxResolution`, enregistrement du manifeste) ;
  `Alundra.Tests/AlundraSoundBankTests.cs`.
- Étapes : tests d'abord, puis code.
  - Les cinq champs sont lus en `int?`, comme ils sont écrits (ADR-0003).
  - `SfxResolution` porte les attributs de programme de la fiche **résolue**, celle dont les tonalités sont jouées. Sous
    redirection par `RefSfxId`, ce sont ceux de la sœur, jamais ceux de la fiche demandée. C'est la fiche que
    l'original passe à `FUN_800901a8` et à `TriggerVoice`.
  - Tests : lecture des cinq champs ; fiche résolue par la chaîne, dont les attributs sont ceux de la sœur ; manifeste
    sans les champs ou avec `null`, qui donne des attributs absents (le mode dégradé de P9 est appliqué en T4.2).
- Validation : `Alundra.Tests` vert.
- Commit : `feat(audio): read the VAB volume and pan attributes of each sound effect`

**Note de validation (2026-09-25).**

- **Code.** `SfxToneRecord.Volume`/`Pan` et `SfxResolution.VabMasterVolume`/`ProgramVolume`/`ProgramPan`, en `int?`,
  pris sur la fiche résolue.
- **Tests.** Tests d'abord. `Alundra.Tests` **1235 / 1235** (1228 + 7) :
  - sur le vrai manifeste : 302 (pans 34/94) ; 303 sous le groupe 56, qui prend les pans 34/94 de sa sœur 835 et non
    ses propres 0/127 ; 162, dont la tonalité muette reste à 0 ;
  - sur un jeu de données synthétique (valeurs de programme distinctes, absentes des vraies données) : attributs de
    la sœur sous redirection, attributs propres sans redirection, champs absents ou `null` qui restent absents.
- **Mutations réelles**, toutes tuées : attributs de programme pris sur la fiche demandée ; volume de tonalité non lu.

### ⏳ T4.2 — Le départ d'une voix suit l'original

- Objectif : D2, première moitié.
- Fichiers : `Alundra/Scripts/` (calcul pur des volumes SPU, sans état : départ et remix, fonctions et tests
  séparés) ; `AlundraSoundPlayer.cs` (`PlaySfx` → `PlayClipStereo`, les voix vivantes gardent leur index de tonalité)
  ; `Alundra.Tests/FakeAudioBackend.cs` (le faux clip expose ses échantillons ; la file de flux compte ses tampons, ou
  la borne de P4 suffit, à constater) ; tests.
- Étapes :
  1. Porter `FUN_80090c58` pour une voix de bruitage : clé `0x21`, volume `0x7f`, pan de voix `0x40`, stéréo (P11).
     Les éventuels écarts ou défauts relevés en T0.2 sont appliqués selon D2. Gains = registre / 16 384 (P2).
  2. Valeurs attendues **calculées à la main dans ce plan avant le code**, pour des tonalités réelles du bateau avec
     leurs attributs issus de T3.1. `scratchpad/spu_expected.py` recopie la décompilation vérifiée en T0.2, et la
     tonalité 0 du son 302 a été refaite à la main. VAB 127, programme 127 et pan de programme 64 pour toutes.

     | Son, tonalité | Volume, pan de tonalité | SPU gauche, droite | Gains (/16 384) |
     |---|---|---|---|
     | 300 t0 | 80, 64 | 6 500, 6 500 | 0,396729 / 0,396729 |
     | 301 t0 | 110, 64 | 12 290, 12 290 | 0,750122 / 0,750122 |
     | 302 t0 | 100, 34 (`< 0x40`) | 10 157, 2 957 | 0,619934 / 0,180481 |
     | 302 t1 | 100, 94 (`> 0x40`) | 2 786, 10 157 | 0,170044 / 0,619934 |
     | 162 t0 | 127, 64 | 16 383, 16 383 | 0,999939 / 0,999939 |
     | 162 t1 | 0, 0 | 0, 0 | 0 / 0 (tonalité muette de l'original) |

     Détail pour 302 t0 : 127 × ((127 << 14) − 127) / 16 129 = 16 383, puis 16 383 × 127 × 100 / 16 129 = 12 900,
     D = 12 900 × 34 / 63 = 6 961, G = 12 900² / 16 383 = 10 157, D = 6 961² / 16 383 = 2 957.

     Un `ProgramPan ≠ 0x40` n'existe pas dans les données (64 partout, T2.3). Il est testé par un cas synthétique, en
     plus des cas réels : programme 127, pan de programme 32, tonalité 127/64. Valeur attendue calculée par le même
     script dans le test, et écrite à la main dans le test.
     Valeurs synthétiques, contrôlées à la main (programme 127, tonalité 127/64) :
     - pan de programme 32 : SPU 16 383 / 4 226 (D = 16 383 × 32 / 63 = 8 321, puis 8 321² / 16 383 = 4 226) ;
     - pan de programme 100 : SPU 3 008 / 16 383.
  3. Replis, jamais le silence :
     - un attribut `null` sur la fiche jouée ;
     - un clip qui n'expose pas ses échantillons (`IAudioClipSamples` absent ou vide ; aucun cas en production, où
       tous les WAV d'Alundra sont en PCM 16 bits mono).

     Dans ces deux cas, la voix part comme aujourd'hui (`PlayClip`, volume 1, centrée), et une ligne est écrite une
     seule fois dans le journal. Le choix du chemin se fait avant l'appel : la DLL teste le clip, sans déduire la cause
     d'un `None`.
  4. Registre des voix vivantes : chaque entrée garde son index de tonalité (rang dans la fiche résolue) et un numéro
     d'ordre de départ, pour P7.
  5. Tests : ces valeurs ; `PlaySfx` crée une voix stéréo par tonalité avec ces gains ; les deux replis ; plafond de
     polyphonie, anti-doublon, garde de fondu 0xA6 et arrêt par monde inchangés. Les tests existants qui figent
     l'ancien comportement (volume 1 et pan 0 au départ) sont mis à jour et listés dans la note de validation.
  6. Mutations en vrai : sans mise au carré ; pan du programme ignoré ; ÷ 0x3fff au lieu de ÷ 0x4000 ; repli du
     `null` retiré (la voix partirait muette).
- Validation : `Alundra.Tests` vert, six goldens identiques.
- Commit : `feat(audio): start sound effect voices with the executable's per-tone stereo volumes`

### ⏳ T4.3 — Le remix suit l'original, et l'id de `0xBF`

- Objectif : D2, seconde moitié, et D5 côté DLL.
- Fichiers : le calcul pur ; `AlundraSoundPlayer.cs` (`RemixVoice` ; suppression de `ProjectMixToVolumeAndPan`) ;
  `AlundraEventProgramRunner.cs` (`0xBF` : `v[1] | (v[2] << 8)`) ; tests.
- Étapes :
  1. Porter `0x80049794` avec ses constantes : `(x+1)²−1`, ÷ 16 383 et ÷ 3 969 par multiplication magique comme le
     binaire, poids de pan au carré. Résolution de la fiche avec le groupe courant ; rien si aucune tonalité ou pas
     de programme ; pour chaque tonalité de la fiche résolue, la voix la plus ancienne de (id demandé, tonalité) (P7)
     reçoit `SetVoiceStereoGains`. Toujours aucune lecture déclenchée. Les attributs de programme et de tonalité sont
     ceux de la fiche résolue.
     - Une voix partie par un repli de T4.2 est mono et ne peut pas recevoir de gains G/D. Elle n'est pas remixée ;
       une ligne est écrite une seule fois dans le journal. Même chose si un attribut de la fiche est `null`.
  2. Valeurs attendues calculées à la main dans ce plan avant le code (`scratchpad/spu_expected.py`, même source).

     | Son, tonalité | Mix G, D | SPU gauche, droite | Gains |
     |---|---|---|---|
     | 302 t0 | 0x40, 0x40 (carte 390) | 2 629, 765 | 0,160461 / 0,046692 |
     | 302 t1 | 0x40, 0x40 (carte 390) | 721, 2 629 | 0,044006 / 0,160461 |
     | 200 t0 | 0x50, 0x50 (Inoa 162) | 6 560, 6 560 | 0,400391 / 0,400391 |
     | 200 t0 | 0x14, 0x14 | 440, 440 | 0,026855 / 0,026855 |
     | 200 t0 | 0, 0 | 0, 0 | 0 / 0 |
     | 302 t0 | 0x7f, 0x20 (asymétrique) | 10 200, 197 | 0,622559 / 0,012024 |
     | 302 t1 | 0x7f, 0x20 (asymétrique) | 2 798, 677 | 0,170776 / 0,041321 |

     Contrôle : pour une tonalité 127/64, un mix 0x7f/0x7f redonne exactement les volumes du départ (16 383, 16 383).
  3. Tests : valeurs ; seule la voix la plus ancienne d'une tonalité change ; id inaudible → aucun appel ; `0xBF`
     avec `v[2] = 1` vise l'id `v[1] + 256` par le vrai runner ; `0xAB` inchangé.
  4. Mutations en vrai : `v[2]` ignoré ; toutes les instances remixées ; `>> 12` au lieu de `>> 11`.
- Validation : `Alundra.Tests` vert, six goldens identiques.
- Commit : `feat(audio): remix live voices tone by tone as the executable does, with 0xBF's 16-bit id`

---

## Phase 5 — Clôture

### ⏳ T5.1 — Validation globale et vérification finale

- Étapes : validation globale ; `verifier` frais sur l'intégration :
  - un bruitage réel joué par le vrai `Update` du monde donne dans le backend les gains de T4.2, puis `0xBF` ceux de
    T4.3 ;
  - un projet dont `AlundraGame.json` porte `"IsAudioMuted": true` garde la clé à l'export (T1.5), et le runtime
    démarre avec le bus `Master` coupé (T1.3).
- Validation : verdict **CONFIRMED** consigné.

### ⏳ T5.2 — Documentation et rapatriement

- Fichiers : ce plan (bilan) ; `docs/plan-e11b-opcodes-audio.md` (« Suites ouvertes » : [B1-a] fermé,
  `IsBgmActivated` supprimé, déviation n°1 d'E11.a fermée ; P3 et P4 nouvelles déviations déclarées ; S1 ouverte) ;
  `docs/plan-conversion-totale.md` (ligne E11).
- Étapes : puis `git fetch` des branches des sous-modules dans les dépôts du checkout principal (aucun push).
- Commit : `docs(plan): close the exact stereo mix, the persisted mute and the IsBgmActivated removal`

### ⏳ T5.3 — Recette de l'auteur

- Lancement : Launcher du worktree sur `alundra-project/AlundraGame.json` du worktree (précédent SI6).
- À écouter :
  - le bateau (389) : ronflements, mouettes, trappe fidèles à l'original (référence : l'analyseur, `AlundraGame`, dont
    le mélangeur SPU applique les mêmes volumes) ; musique comme avant ;
  - Ship Klark (intérieur) 390 : le son bouclé 302 baisse de moitié à l'entrée (`0xBF 302, 0x40, 0x40`, jusqu'ici
    appliqué par erreur à l'id 46) ; puis Inoa 162 : les ambiances 200 et 203 s'éteignent par paliers ;
  - `"IsAudioMuted": true` ajouté à `alundra-project/AlundraGame.json` → silence, même après un nouvel export ; clé
    retirée → son ;
  - le même projet ouvert dans l'éditeur du worktree : l'aperçu d'un son est muet, puis s'entend une fois la clé
    retirée et le projet rouvert.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Lecture de « corriges aussi le jeu original » (D2) : à confirmer par l'auteur à l'approbation. | toutes |
| O2 | ~~Écarts ou défauts du chemin de départ dans le binaire.~~ **Fermé en T0.2** : identique à la décompilation pour une voix de bruitage. | T0.2 → T4.2 |
| O3 | ~~Carte de recette pour `0xBF`.~~ **Fermé en T0.2** : Ship Klark (intérieur) 390, id 302, mix `0x40`/`0x40` ; puis Inoa 162 (fondus de 200 et 203). | T0.2 → T5.3 |
| O5 | `Alundra.sln` (analyseur) ne builde pas à `bbf33962` : NU1605, MonoGame 3.8.4.1 dans `AlundraGame`/`AlundraTools` contre 3.8.5.1 exigé par le MGUI amené par « update MGUI ». Préexistant, hors périmètre ; à trancher par l'auteur (aligner les paquets de l'analyseur). | signalé |
| O6 | ~~Une fiche de carte peut recevoir les échantillons d'une sœur d'un autre VAB.~~ **Fermé en T2.3** : 6 cas, tous du VAB 50, qu'aucune carte n'utilise ; ces entrées ne sont jamais jouées telles quelles. | T2.3 |
| O4 | ~~`FUN_800914cc` croise les canaux : défaut de l'original ou de la décompilation ?~~ **Fermé en T0.2** : défaut de décompilation (le binaire atténue chaque canal par lui-même, sans mise au carré). Le port ne rencontre pas ce chemin : aucune tonalité abandonnée dans `sfx.json` (mesuré le 2026-09-25), refait en T2.3. | T0.2 → T2.3 |

## Suites consignées (hors de cette tâche)

- **S1** — `PlaySeq` est absent de `LoadMapSequence` et du cas 5 du streaming dans l'exécutable. Par déduction, `0xA7`
  dans la DLL (`AlundraEventProgramRunner.cs:842`) jouerait la musique là où l'original ne fait que la charger, sauf
  drapeau stop-all. Inférence à vérifier dans une suite dédiée, avec l'analyseur (D5).

## Hors périmètre

- Enveloppes ADSR, réverbération SPU, plafond de 24 voix et vol de voix par priorité : non portés, inchangés.
- Points de boucle (P8), séquences de bruitages (`SeqNum >= 0`), option mono (P11).
- Toute interface pour le son coupé (D4). Tout changement de MGUI.

## Budget

16 commits : parent 10 (dont les deux bumps de pointeur), moteur 3, analyseur 3. Trois `verifier` (T1.4, T3.4, T5.1).
Exports complets : la référence en T0.1, puis T1.5 et T3.3, chacun prouvé par manifeste et suivi d'un second export.

## Journal

| Date | Événement |
|---|---|
| 2026-09-25 | Reconnaissance en lecture seule, faits [binaire] relevés en session principale, réponses de l'auteur D2 à D5, plan rédigé. |
| 2026-09-25 | Relecture fraîche de l'enveloppe : **REVISE**. Les écritures hors dépôt de P10 n'avaient ni sauvegarde, ni garde d'égalité, ni retour arrière. Corrigé dans P10, T3.1, T3.4 et les arrêts ; la copie vers le remaster attend désormais T3.4. |
| 2026-09-25 | Relecteur frais, second passage de l'enveloppe : **READY**. Relecteur frais sur la tranche des phases 0 et 1 : **READY**. Plan soumis à l'auteur. |
| 2026-09-25 | Retour de l'auteur : « le réglage doit vivre dans le projet et non dans mes fichiers locaux ». D4 complétée, P6 réécrite (champ de `ProjectSettings`), T1.3 réécrite, T1.5 ajoutée (le convertisseur garde le réglage à l'export), recette T5.3 ajustée. Nouvelle époque de relecture. |
| 2026-09-25 | Relecteur frais de l'enveloppe révisée : **READY**. Contre-vérification des faits cités : trois plages de lignes corrigées (`ProjectSettingsHelper`, `ProjectWriter`) ; un « faux » (l'éditeur sans runtime audio) réfuté en session principale, le fichier existe (`SoundAssetInspectorPanel.cs:289`). L'auteur autorise P10, choisit le mode AUTO. `main` a avancé à `2e3506e` (merge d'E13.d) : bases mises à jour, aucun fichier du périmètre changé. |
| 2026-09-25 | Relecteur frais de la tranche des phases 0 et 1 : **REVISE**. Le chemin éditeur de T1.3 n'avait pas de preuve unique : son repli rendait la mutation « branchement retiré » impossible. Corrigé par l'option (a) : un abonné de `CasaEngine.EditorServices`, testé par le vrai `LoadProject`. La ligne de câblage de `GameEditor` est déclarée hors tests et prouvée en direct (T1.3, sinon 🧪) puis par la recette T5.3. Nouvelle époque ; relecture de clôture. |
| 2026-09-25 | Relecteur frais de clôture sur la tranche des phases 0 et 1 : **READY**. Début de l'exécution (T0.1). |
| 2026-09-25 | Relecteur frais de la tranche des phases 2 et 3 : **REVISE**, deux constats. (1) T2.3 ne disait pas d'où viennent les attributs : `DecodeSfxTones` résout par une fonction privée et ne rend que les échantillons. Désormais `SoundBin.cs` est dans le périmètre de T2.3, qui rend les attributs de la fiche résolue, avec un contrôle croisé fiche directe et fiche redirigée. (2) Le lecteur du convertisseur rendait 0 pour un champ absent. Désormais champs nullables de bout en bout, `null` jamais 0 (P9, T3.3). Mesures ajoutées : aucune tonalité abandonnée ; 443 fiches à chaîne (O6). Nouvelle époque de relecture pour la tranche. |
| 2026-09-25 | Relecteur frais de clôture sur la tranche des phases 2 et 3 : **READY**. Début de T2.1. |
