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
| P9 | Nouveaux champs de `sfx.json` : par enregistrement `VabMasterVolume`, `ProgramVolume`, `ProgramPan` ; par tonalité `Volume`, `Pan` (snake_case dans `sfx-manifest.json`). Si le manifeste n'a pas ces champs, la DLL revient au jeu d'aujourd'hui (volume 1, centré) et l'écrit une fois dans le journal. | Format de données : ADR parent en T3.3. Un champ absent ne doit pas rendre le jeu muet en silence. |
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

### ⏳ T0.2 — Mesures dans le binaire et dans le corpus

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

---

## Phase 1 — Moteur (sous-module `CasaEngineMonogame`)

### ⏳ T1.1 — Plan moteur et ADR

- Objectif : le plan moteur exigé par l'`AGENTS.md` du moteur, et les deux décisions en ADR.
- Fichiers (moteur) : `ai-agent/tasks/audio-stereo-voices-mute-tasks.md` (copie des tâches T1.2 et T1.3,
  consommateur : ce plan) ; `ai-agent/README.md` (ligne du tableau) ; `docs/decisions/` : une ADR « voix stéréo
  logicielles » (D3, P3, P4, P5), une ADR « son coupé, réglage du projet » (D4, P6 : champ additif de
  `ProjectSettings`, écrit seulement s'il est vrai), par le skill `adr`.
- Validation : relecture ; les index sont à jour.
- Commit (moteur) : `docs(audio): plan and record the software stereo voices and the persisted mute`

### ⏳ T1.2 — Voix stéréo logicielle

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

### ⏳ T1.3 — Son coupé, réglage du projet

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

### ⏳ T1.4 — Vérification du moteur et pointeur

- Objectif : un `verifier` frais sur T1.2 et T1.3 (contrat ci-dessus, diff du moteur, tests), puis le pointeur du
  moteur dans le parent.
- Étapes : verdict traité par priorité ; puis bump du pointeur ; build du parent ; `Alundra.Tests` inchangé.
- Validation : verdict **CONFIRMED** consigné ; comptes du parent identiques à T0.1.
- Commit : `chore(submodules): point at the engine with software stereo voices and the project mute setting`

### ⏳ T1.5 — Le convertisseur garde le réglage « son coupé » à l'export

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

---

## Phase 2 — Analyseur (sous-module `alundra-datas-analyser`)

### ⏳ T2.1 — Retrait d'`IsBgmActivated`

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
- Commit (analyseur) : `refactor(sound): remove the IsBgmActivated switch the executable never had`

### ⏳ T2.2 — Id de `0xBF` sur deux octets

- Fichiers : `AlundraTools/AlundraEngine/Gameplay/Scripts/EntityEventHandlers.cs:3612-3617`.
- Étapes : `variables[1] | (variables[2] << 8)`, commentaire [binaire] `0x80041ca0`.
- Validation : `AlundraEngine` builde ; `Alundra.sln` garde son échec préexistant à l'identique.
- Commit (analyseur) : `fix(scripts): opcode 0xBF reads a 16-bit sound effect id as the executable does`

### ⏳ T2.3 — Attributs VAB dans `sfx.json`, et extraction des seuls bruitages

- Objectif : P9 côté source, et une sous-commande qui n'écrit que `sound/sfx.json` et les WAV des bruitages.
- Fichiers : `AlundraTools/AlundraDataExtractor/Program.cs`.
- Étapes :
  1. `SfxExportRecord` gagne `VabMasterVolume`, `ProgramVolume`, `ProgramPan`, et `SfxToneExport` gagne `Volume` et
     `Pan`. Lus sur l'en-tête VAB, le `ProgAtr` et le `VagAtr` qu'utilise déjà `DecodeSfxTones`, du même VAB que les
     échantillons. Pour une tonalité, relever aussi `Vag == 0xff` (chemin `FUN_800914cc`) si T0.2 l'exige.
  2. Sous-commande `--extract-sfx <gamePath> <outputPath>`, sur le modèle de `--render-bgm`, qui ne fait que l'export
     des bruitages.
- Validation : `AlundraEngine` et `AlundraDataExtractor` buildent ; la sous-commande tourne vers le scratchpad (chemin du jeu relu dans le
  `launchSettings.json` de l'extracteur). Les nouveaux champs sont présents sur les 996 tonalités ; l'histogramme des
  valeurs est écrit sous la tâche.
- Commit (analyseur) : `feat(extractor): export the VAB volume and pan attributes of every sound effect`

---

## Phase 3 — Données et convertisseur (parent)

### ⏳ T3.1 — Ré-extraction ciblée et copie prouvée (P10)

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

### ⏳ T3.2 — Pointeur de l'analyseur

- Validation : parent buildé ; `Alundra.Tests` et convertisseur inchangés.
- Commit : `chore(submodules): point at the analyser without IsBgmActivated and with the VAB attributes`

### ⏳ T3.3 — Le convertisseur porte les attributs, export prouvé

- Fichiers : `alundra-casaengine-project-converter/Readers/SoundManifestReader.cs` (`SfxRecord`, `SfxTone`) ; tests du
  convertisseur ; ADR parent `docs/decisions/` (format de `sfx-manifest.json`, P9), par le skill `adr`.
- Étapes :
  1. Tests : lecture puis écriture d'un enregistrement portant les cinq champs, et d'un ancien `sfx.json` sans eux.
  2. Champs ajoutés.
  3. Prédiction écrite **avant** l'export : modifiés `Sounds/sfx-manifest.json` et `report.json` ; possible
     `AlundraGame.json` (leçon de B3) ; rien d'autre.
  4. Export complet, diff du manifeste, second export.
- Validation : tests du convertisseur = ligne de base + n ; diff mesuré ⊆ prédit ; second export ⊆ `{report.json}`.
- Commit : `feat(audio): carry the VAB volume and pan attributes into the sound effect manifest`

### ⏳ T3.4 — Vérification de la frontière de données

- Objectif : un `verifier` frais sur la chaîne extracteur → `sfx.json` → manifeste : les champs sont ceux de
  l'exécutable pour quelques bruitages tirés au hasard, lus dans le VAB.
- Étapes : sur **CONFIRMED** seulement, copier le `sfx.json` de T3.1 dans `remaster-data-extracted/sound/` et vérifier
  que son SHA-1 est égal à celui de `data-extracted/sound/sfx.json`. Sur tout autre verdict : retour arrière de T3.1,
  puis ⚠️ Blocked.
- Validation : verdict et SHA-1 consignés. Pas de commit si rien ne change (note versée au commit suivant).

---

## Phase 4 — DLL (`Alundra`)

### ⏳ T4.1 — La banque de sons lit les attributs

- Fichiers : `Alundra/Scripts/AlundraSoundBank.cs` (`SfxToneRecord`, `SfxResolution`, enregistrement du manifeste) ;
  `Alundra.Tests/AlundraSoundBankTests.cs`.
- Étapes : tests d'abord (lecture des cinq champs, fiche résolue par `RefSfxId`, manifeste sans les champs → mode
  dégradé de P9), puis code.
- Validation : `Alundra.Tests` vert.
- Commit : `feat(audio): read the VAB volume and pan attributes of each sound effect`

### ⏳ T4.2 — Le départ d'une voix suit l'original

- Objectif : D2, première moitié.
- Fichiers : `Alundra/Scripts/` (calcul pur des volumes SPU, sans état : départ et remix, fonctions et tests
  séparés) ; `AlundraSoundPlayer.cs` (`PlaySfx` → `PlayClipStereo`, les voix vivantes gardent leur index de tonalité)
  ; `Alundra.Tests/FakeAudioBackend.cs` (le faux clip expose ses échantillons ; la file de flux compte ses tampons, ou
  la borne de P4 suffit, à constater) ; tests.
- Étapes :
  1. Porter `FUN_80090c58` pour une voix de bruitage : clé `0x21`, volume `0x7f`, pan de voix `0x40`, stéréo (P11).
     Les éventuels écarts ou défauts relevés en T0.2 sont appliqués selon D2. Gains = registre / 16 384 (P2).
  2. Valeurs attendues **calculées à la main dans ce plan avant le code**, pour trois tonalités réelles : les
     bruitages du bateau (300, 301, 302) avec leurs attributs issus de T3.1. Plus un cas `tonePan < 0x40`, un cas
     `> 0x40` et un `ProgramPan ≠ 0x40`.
  3. Tests : ces valeurs ; `PlaySfx` crée une voix stéréo par tonalité avec ces gains ; plafond de polyphonie,
     anti-doublon, garde de fondu 0xA6 et arrêt par monde inchangés (tests existants verts).
  4. Mutations en vrai : sans mise au carré ; pan du programme ignoré ; ÷ 0x3fff au lieu de ÷ 0x4000.
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
     reçoit `SetVoiceStereoGains`. Toujours aucune lecture déclenchée.
  2. Valeurs attendues calculées à la main dans ce plan avant le code (deux mixes, dont un asymétrique).
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
  - la carte de T0.2 où `0xBF` s'exécute ;
  - `"IsAudioMuted": true` ajouté à `alundra-project/AlundraGame.json` → silence, même après un nouvel export ; clé
    retirée → son ;
  - le même projet ouvert dans l'éditeur du worktree : l'aperçu d'un son est muet, puis s'entend une fois la clé
    retirée et le projet rouvert.

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Lecture de « corriges aussi le jeu original » (D2) : à confirmer par l'auteur à l'approbation. | toutes |
| O2 | Écarts ou défauts du chemin de départ dans le binaire. | T0.2 → T4.2 |
| O3 | Carte de recette pour `0xBF`. | T0.2 → T5.3 |
| O5 | `Alundra.sln` (analyseur) ne builde pas à `bbf33962` : NU1605, MonoGame 3.8.4.1 dans `AlundraGame`/`AlundraTools` contre 3.8.5.1 exigé par le MGUI amené par « update MGUI ». Préexistant, hors périmètre ; à trancher par l'auteur (aligner les paquets de l'analyseur). | signalé |
| O4 | `FUN_800914cc` (tonalité `Vag == 0xff`) croise les canaux : `rightVolume = leftVolume × pan / 0x3f` (`SoundManager.cs:4971`, `:4981`) et `leftVolume = rightVolume × … ` (`:4985`), là où `FUN_80090c58` garde chaque canal. Défaut de l'original ou de la décompilation ? À trancher en T0.2 si une tonalité de bruitage est concernée. | T0.2 → T4.2 |

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
