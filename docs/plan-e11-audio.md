# Plan — E11 audio : rendre l'intro sonore

Date : 2026-08-30. Origine : réserve remontée par l'utilisateur en validant E7 — « je valide tout mais
il n'y a pas encore de son ». Étape choisie par l'utilisateur le 2026-08-30 parmi E9/E10/E11/E12.

**Tout le §1 est MESURÉ ou LU dans la décompilation, jamais supposé.** Deux agents de reconnaissance
se sont contredits sur des faits comptables ; les chiffres retenus ci-dessous ont été recomptés en
session principale sur le golden et sur le manifeste, et c'est cette version-là qui fait foi.

## 1. Les faits établis — à ne pas redécouvrir

### 1.1 Ce que l'intro doit produire, dispatch par dispatch

`0xBD` est dispatché **23 fois** dans `docs/intro-trace-389.txt` (recompté ; le « 15 » de
`intro-roadmap.md:299` est le compte du seul paramètre `[44,1]`, et ses frames « 704-740 » datent de
la fenêtre à 926 frames — même classe de péremption que le `4624`→`8514` d'E7). `sfxId` se dérive
comme dans l'original : `(v[2] << 8) | v[1]` (`EntityEventHandlers.cs:3597-3602`).

| params | sfxId | dispatches | frames | rôle |
|---|---|---|---|---|
| `[44,1]` | **300** | **15** | 78→1589 | boucle d'ambiance **pilotée par script** (map-event B 135 = `0xBD` + attentes + `0x49`) |
| `[45,1]` | **301** | **6** | | mouettes |
| `[46,1]` | **302** | **1** | **frame 1**, entité 6 slot A(Load) | **lit sonore en boucle** (le seul échantillon `repeat: true`) |
| `[61,0]` | **61** | **1** | **frame 1087**, entité 15 slot C | **la trappe** — suivie de 5× `0x85` aux frames 1087/1096/1113/1118/1123 |

C'est le son 61 que l'utilisateur attend depuis E7.

### 1.2 Les quatre enregistrements, lus dans `Sounds/sfx-manifest.json`

| id | vab | prog/tone | note | maxVoices | numTones | fichiers | rate | boucle |
|---|---|---|---|---|---|---|---|---|
| 300 | 56 | 0/0 | 60 | 2 | 1 | `sfx_0300.wav` | 11025 | non |
| 301 | 56 | 0/1 | 61 | 2 | 1 | `sfx_0301.wav` | 11025 | non |
| 302 | 56 | 0/2 | 62 | **1** | **2** | `sfx_0302_0.wav` + `sfx_0302_1.wav` | 11025 / **10401** | **oui, 1820..28055** |
| 61 | **−1** | 5/6 | 64 | 1 | 1 | `sfx_0061.wav` | 18142 | non |

`302` est donc **deux voix simultanées légèrement désaccordées** (11025 et 10401 Hz), et son fichier
fait exactement 28056 échantillons — soit `loop_end + 1`.

### 1.3 Comment l'original joue un son (lu dans la décompilation)

- **Plusieurs tonalités à la fois, pas une.** `SoundManager.cs:3969-3985` boucle
  `for (toneOffset = 0; toneOffset < ToneCount; toneOffset++)` avec `toneIndex = ToneNumber + toneOffset`
  et alloue **une voix par tonalité**. `MaxVoices` n'est pas un sélecteur : c'est un plafond de
  polyphonie testé **avant** la boucle (`:3919`, comptage `:4025`).
- **`Note` ne sélectionne rien.** `ToneNumber` est un **index direct** dans le programme VAB
  (`:4605-4620`, `:4738-4744`). Le sélecteur par plage de touches existe (`FUN_80091204`, `:4213`)
  mais son seul appelant est le chemin **séquenceur/MIDI**, jamais `TriggerVoice`. `Note` est
  uniquement l'argument de hauteur.
- **La hauteur est transposée — et l'export l'a déjà appliquée.** `CalculateVoicePitch`
  (`SoundManager.cs:4750-4785`) transpose par `Note − Center`. L'extracteur utilise **la même
  formule** (`CalculateToneSampleRate` → `CalculateToneRawPitch`, `SoundBin.cs:428/:1973/:1909`) et
  écrit le WAV à ce taux (`AlundraDataExtractor/Program.cs:302-306`). **Donc : jouer chaque WAV à
  plat, à la fréquence de son propre en-tête, est correct. Transposer une seconde fois serait faux.**
- **Anti-doublon par frame audio** : `IsSoundEffectAlreadyPlaying` (`:3888`/`:3843`), table vidée à
  chaque frame (`:3810-3813`) — deux demandes du même id dans la même frame, la seconde est jetée.
- **Volume et pan** viennent des attributs VAB (`VagAtr.Vol`/`Pan`, `ProgAtr`, `FUN_80090c58`
  `:4820-4877`) — **ces champs ne sont PAS exportés** (le manifeste porte `sample_rate`,
  `loop_start`, `loop_end`, `repeat`, `asset_id`, et rien d'autre).

### 1.4 La résolution d'un id (globale, mais jouable seulement sous le bon groupe)

`TryResolveSoundEffectRecord` (`SoundManager.cs:4423-4453`) : `VabId == -2` → son désactivé, on jette ;
`VabId == -1` → VAB **global** ; `VabId == g_currentSoundGroup` → VAB **de la carte** ; sinon on suit
la chaîne `RefSfxId` (`FindSfxRecordForSoundGroup`, `:4166-4191`) à la recherche d'un frère du bon
groupe, et on abandonne à `RefSfxId == 0`. Le groupe vient de la carte :
`VabIndexByMapId[389] = 56`. Donc 300/301/302 ne sont jouables que sous la 389, et 61 partout.

**Et voici le trou, trouvé en relecture : cette table n'est PAS exportée.** `VabIndexByMapId` n'existe
que dans la décompilation (`SoundBin.cs:2317+`) ; vérifié en session principale, rien sous
`alundra-project/` ne porte un groupe de sons par carte — les deux seuls fichiers contenant `vab_id`
sont les manifestes eux-mêmes, au niveau de l'enregistrement, pas de la carte. **La production n'a donc
aucune source pour le groupe courant**, et le §4 interdit de toucher au convertisseur. Voir D-E11-6.

**Pourquoi ça n'a aucun effet sur la 389** (et c'est mesuré, pas espéré) : le groupe ne sert qu'à
choisir **entre** l'enregistrement demandé et un frère de la chaîne `RefSfxId`. Or les quatre seuls ids
dispatchés dans l'intro ont `vab_id = 56` (le groupe de la 389 lui-même) ou `−1` (global) : dans les
deux cas la résolution rend **le même enregistrement, avec ou sans groupe**. La divergence n'apparaît
que pour un id appartenant au groupe d'une AUTRE carte, cas qui ne se produit pas ici.

### 1.5 La musique est BLOQUÉE, et il faut le dire avant de commencer

Map 389 → index musical **25**, sans condition (`GetMapSoundIndex`, `GameEngine.cs:1095-1131` : la 389
n'est **pas** dans la table d'override conditionnée aux flags, donc `g_defaultSoundOffsetList[389]` =
`0x19` = 25). La correspondance index → fichier est **démontrée, pas supposée** : l'extracteur boucle
`for soundIndex = 1..46`, appelle `LoadMapSequence(soundIndex, 1)` et écrit `bgm_{soundIndex:D3}.wav`
(`AlundraDataExtractor/Program.cs:164-178`). Donc index 25 ⇔ `Musics/bgm_025.wav`.

**Or `bgm_025.wav` est 5 secondes de silence** — comme **tous les index 21 à 46** (300 frames,
`peak 1`, `first_audible_frame: -1`, 882044 octets), et l'index 19 n'existe pas. Ce n'est **pas** une
propriété des données du jeu : le créneau 25 de `MusicSeqVabOffsets` est pleinement peuplé (SEQ 12288
octets, en-tête VAB 10240, corps 124928) et les index 21-46 sont assignés en masse à de vraies cartes.
C'est un **échec d'extraction**, dont la cause n'est pas établie statiquement — la boucle de l'extracteur
avale ses exceptions (`Program.cs:181-184`).

**Conséquence assumée : l'intro n'aura pas de lit musical à l'issue de ce plan.** Le corriger vit dans
`alundra-datas-analyser`, pas ici. C'est une tranche séparée et explicitement hors de la première
livraison — mieux vaut le dire maintenant que laisser l'utilisateur le découvrir en jeu.

### 1.6 Ce qui existe déjà, et qu'il ne faut pas rebâtir

- **Le moteur est fini** : `AudioService.PlayClip/PlaySound/Stop/FadeVoice`, `MusicPlayer`, bus,
  fondus. Aucun `TODO`, aucun `NotImplementedException` dans `Framework/Audio`.
- **Les données sont exportées** : 996 SFX + 45 BGM en `.wav`, enregistrés dans `AssetInfos.json`
  avec leurs guids, et le manifeste SFX porte **les ids d'origine 1..961 sans trou**.
- **Rien n'est câblé côté DLL** : `0xBD` est le seul opcode audio avec un `case`, dégradé
  (`AlundraEventProgramRunner.cs:627-629`) ; `0x12/0x75/0xA5/0xA6/0xA7/0xA8/0xAB/0xBE/0xBF/0xB9/0xBA`
  tombent dans `UnknownSkipped`. `SpriteRecordCatalog.Sfx` est parsé et jamais lu.

## 2. Les contraintes qui décident de la conception

### 2.1 Les goldens sont AVEUGLES à ce travail — c'est la contrainte n°1

Dix des douze opcodes audio sont de **purs effets de bord à avance constante** ; ils ne touchent jamais
`state.Result`. Le seul dispatché dans l'intro (`0xBD`, 23×) en fait partie. **Implémenter les douze
correctement ou les laisser tous en no-op produit un flux d'instructions identique et des numéros de
frame identiques.** La seule différence observable serait la colonne `Kind` passant de `Degraded` à
`Implemented` — et aucun test n'assère ça.

C'est mot pour mot la leçon du `0x3B` d'E7, cette fois connue **avant** d'écrire le plan. **E11 doit
donc porter son propre oracle**, et aucun critère d'acceptation ne peut reposer sur les traces dorées.

### 2.2 Ce qu'un test headless peut prouver — et la limite est plus étroite que je ne l'ai d'abord écrit

**Correction de relecture, et c'était une vraie erreur de ma part.** La première rédaction concluait
« aucun oracle n'est possible » en s'appuyant sur le fait qu'`AudioService` est `sealed`. L'argument ne
tient pas : on n'a jamais eu besoin d'en hériter. `IAudioBackend` et `IAudioClipProvider` sont des
interfaces **publiques** du moteur, et `new AudioService(backend) { ClipProvider = provider }` est
exactement la forme qu'emploient les propres tests du moteur (`AudioServicePlaySoundTests.cs:16`).
Que les fakes du moteur vivent dans `CasaEngine.Tests` (non référencé) impose de les réécrire dans
`Alundra.Tests` — c'est un coût, pas un empêchement.

La vraie limite, elle, tient : `SoundEffectLoader` appelle `SoundEffect.FromStream` (`:26`), attrape
`NoAudioHardwareException` (`:30-34`) et rend `null`. **Sur le chemin de chargement réel, rien ne
distingue « ça a marché » de « pas de matériel audio ».**

**Donc l'oracle se scinde en deux, et les deux sont obligatoires** : (a) la **demande** — quel id, à
quelle frame, par le vrai site d'appel, seam factice ; (b) le **lecteur lui-même** — combien de clips,
lesquels, avec quels paramètres, contre un vrai `AudioService` monté sur un faux backend. Sans (b),
toute la mécanique du lecteur (une voix par tonalité, plafond, anti-doublon, bouclage) partirait non
prouvée. Seule reste hors de portée la preuve que du son sort d'un haut-parleur : c'est la validation
en jeu par l'utilisateur, critère d'acceptation explicite.

### 2.3 Le précédent à suivre : `IAlundraCellMutator`

Interface déclarée **en tête du fichier de son unique implémenteur** (`AlundraCellStore.cs:18`),
accrochée à `IEntityWorldContext` en **membre d'interface par défaut** rendant `null`
(`IEntityWorldContext.cs:94`) pour que tous les implémenteurs existants compilent inchangés, consommée
dans `Dispatch` par un `is { }` avec **branche dégradée** en `else` (`AlundraEventProgramRunner.cs:637-646`),
installée par une méthode `internal` extraite d'`InitializeWithWorld` pour qu'un test l'appelle
directement (`InstallCellAndOverlaySystems`, `:599`), et neutralisable par un drapeau du harnais
(`IntroTraceHarnessTests.cs:1036`).

**Règle du dépôt sur le choix du seam** : ce que l'**interpréteur** appelle va sur `IEntityWorldContext` ;
ce que l'**`Update` d'une entité** appelle va sur `IAlundraScriptHost`. Les opcodes audio sont
dispatchés par l'interpréteur → `IEntityWorldContext`.

## 3. Les décisions

- **D-E11-1 — Le seam est `IAlundraSoundPlayer`, dans le vocabulaire d'Alundra (`int sfxId`), pas
  `AudioService`.** Même forme que `IAlundraCellMutator`. Raison : `AudioService` est `sealed` et
  inutilisable en test, et le vocabulaire d'Alundra est ce que les opcodes manipulent.
- **D-E11-2 — AUCUN asset `.sound`, aucune modification du convertisseur, aucun ré-export.**
  Vérifié en session principale : `AudioService.PlayClip(IAudioClip, busName, AudioVoiceParameters,
  owner)` est public (`AudioService.cs:62`) et les `.wav` se chargent déjà comme `IAudioClip`
  (`AssetLoaderRegistry.cs:30` → `SoundEffectLoader`, extensions `{ ".wav" }`). Les 1041 WAV sont
  **déjà** dans `AssetInfos.json` avec leurs guids. Générer 1041 documents `.sound` serait du travail
  pur perte — et nous priverait du pan et des paramètres par appel, que `SoundAsset` fige
  (`CreateVoiceParameters` force le pan à 0). Le commentaire d'`AudioWriter.cs:13-20` (« CasaEngine n'a
  pas de type audio ») est périmé mais **n'a pas besoin d'être corrigé pour autant** : on ne passe pas
  par ce type. *(La musique, elle, exigera un `.sound` streaming — voir E11.c, bloquée.)*
- **D-E11-3 — La musique est hors de la première livraison** (§1.5), et l'utilisateur en est prévenu
  avant approbation, pas après.
- **D-E11-4 — Fidélité : ce qu'on porte et ce qu'on assume.**
  - Porté : toutes les tonalités jouées **simultanément** (`ToneNumber + offset`, `ToneCount` voix) ;
    lecture **à plat** au taux de l'en-tête (§1.3) ; résolution `VabId` avec chaîne `RefSfxId` (§1.4) ;
    plafond `MaxVoices`.
  - **Non porté dans E11.a, et c'est une décision, pas un oubli — l'anti-doublon par frame**
    (blocage P2 de la seconde relecture). L'original vide sa table à chaque frame audio
    (`SoundManager.cs:3810-3813`). Le porter exige un **propriétaire de frame** : quelqu'un doit dire
    au lecteur qu'une frame a commencé. Rien dans le seam `PlaySfx(int)` ni dans l'installation ne le
    fournit — et une implémentation qui garderait un ensemble « déjà demandé » **permanent**
    satisferait tous les critères écrits tout en faisant perdre à l'intro **14 de ses 15** lectures du
    son 300 et 5 de ses 6 du son 301 : le lecteur se tairait après le premier son, acceptation au vert.
    **Vérifié en session principale : aucun id n'est dispatché deux fois dans une même frame** sur les
    23 dispatches (les deux de la frame 1 sont les ids 302 et 300). Le filtre est donc **strictement
    inerte sur la 389** — le retirer d'E11.a supprime tout le mode de défaillance au lieu de tester
    autour. Il part en E11.b, avec son propriétaire de frame nommé, le jour où une carte le rend
    observable.
  - **Déviation assumée n°1 — volume et pan par tonalité** : absents de l'export (§1.3). On joue à
    volume unité, pan centré. À rouvrir seulement si un son sonne manifestement faux.
  - **Déviation assumée n°2 — la boucle du son 302** : `AudioVoiceParameters.IsLooped` reboucle **tout
    le tampon**, alors que la vraie boucle est `1820..28055` — soit **165 ms d'attaque rejouée à chaque
    cycle**. Le moteur n'a pas de points de boucle et MonoGame non plus au niveau `SoundEffectInstance`.
    Assumé et documenté pour cette tranche ; c'est le seul son bouclé de l'intro.
- **D-E11-5 — Hygiène des prédicats**, corrigée au passage parce qu'elle est dans le même fichier et
  la même famille : `0xA8` (« un son est-il en cours de chargement ») et `0xBA` sont des **prédicats**
  aujourd'hui sautés, et `UnknownOpcode` **ne remet pas `Result` à zéro** — le script branche donc sur
  la valeur laissée par le prédicat précédent. Aucun n'est atteint sur la 389, mais c'est du
  branchement sur déchet ailleurs dans le corpus. On les implémente pour de vrai : nous ne streamons
  jamais depuis un CD, donc les deux rendent **faux**, écrit explicitement dans `Result`.
- **D-E11-6 — Le groupe de sons courant n'a pas de source, et on assume de s'en passer** (blocage P1
  de la relecture, §1.4). Les trois options étaient : exporter `VabIndexByMapId` (change le
  convertisseur, donc impose un ré-export complet, pour **zéro** effet sur la 389) ; coder 56 en dur
  (inacceptable) ; ou résoudre sans groupe. **Retenu : sans groupe.** `TryResolve` prend le groupe en
  paramètre **optionnel** ; quand il est absent, un enregistrement de `VabId >= 0` joue **ses propres
  tonalités** au lieu d'être redirigé par la chaîne `RefSfxId`. La chaîne est implémentée et testée,
  mais aucun appelant de production ne la déclenche dans E11.a.
  **Déviation assumée n°3, et sa portée exacte** : un id appartenant au groupe d'une AUTRE carte
  jouerait son propre échantillon au lieu du frère prévu. **Inerte sur la 389** — les quatre ids de
  l'intro ont `vab_id` 56 ou −1, donc résolution identique avec ou sans groupe (§1.4). L'export de la
  table carte → groupe est un item d'**E11.b**, à faire quand une seconde carte le rendra observable :
  le faire maintenant coûterait un ré-export complet pour rien.

## 4. Tranches

### E11.a — Le seam, la résolution, et les quatre sons de l'intro *(la seule à approuver maintenant)*

Portée : `Alundra/` et `Alundra.Tests/` uniquement. Ni moteur, ni convertisseur, ni `alundra-project/`.

1. **`AlundraSoundBank`** — lit `Sounds/sfx-manifest.json` depuis `EngineEnvironment.ProjectPath`
   (même chemin que `SpriteRecordCatalog`, sans `Game`), expose `TryResolve(int sfxId, int? soundGroup,
   out resolution)` portant la logique du §1.4 **et** la liste des tonalités avec leurs guids.
   Le groupe est **optionnel** (D-E11-6). Échoue en douceur sur les 91 ids sans tonalité.
2. **`IAlundraSoundPlayer`** — déclarée en tête du fichier de son implémenteur, `PlaySfx(int sfxId)`.
   Accrochée à `IEntityWorldContext` en membre par défaut `=> null` (D-E11-1, §2.3).
3. **`AlundraSoundPlayer`** — implémente le seam sur `AudioService.PlayClip` (D-E11-2) : une voix par
   tonalité, plafond `MaxVoices`, `IsLooped` depuis `repeat`. **Pas d'anti-doublon par frame**
   (D-E11-4) : il exigerait un propriétaire de frame que ce seam ne fournit pas, et il est inerte ici.
   **Livrable de test associé, non optionnel** : un faux `IAudioBackend` et un faux
   `IAudioClipProvider` dans `Alundra.Tests` — les interfaces sont publiques, mais les fakes du moteur
   vivent dans `CasaEngine.Tests`, que ce projet ne référence pas (§2.2). C'est ce qui rend T5
   possible, et T5 est ce qui prouve tout ce point 3.
4. **Opcodes** `0xBD`, `0xBE`, `0x12`, `0x75` dans `Dispatch`, avec branche dégradée en `else` — la
   forme exacte de `0x54` (`AlundraEventProgramRunner.cs:637-646`). Tailles inchangées (3/3/2/2).
5. **Installation** : `internal void InstallAudioSystems(World world)` sur `AlundraWorldProxy`,
   extraite comme `InstallCellAndOverlaySystems`, résolvant `world.Game?.AudioSystemComponent?.Service`
   et laissant le seam **null** sans `Game`.
6. **Prédicats** `0xA8` et `0xBA` (D-E11-5).

**Les tests, écrits AVANT et vus ÉCHOUER** (§2.1 : ils portent toute la preuve) :

- **T1 — les 23 dispatches, au site de production.** Un faux `IAlundraSoundPlayer` enregistre
  `(frame, sfxId)` ; on pilote `RunMapEventsPass` par `HeadlessIntroSimulation` comme
  `AlundraCellStoreProductionTests.cs:48-70`. Assertions **chiffrées**, pas des compteurs `> 0` :
  **23 demandes au total ; 15× id 300 ; 6× id 301 ; 1× id 302 à la frame 1 ; 1× id 61 à la frame 1087.**
- **T2 — le jumeau de neutralisation** (règle n°2 du dépôt) : `installSoundPlayer: false` → **zéro**
  demande **et** `DegradedOpcodeCounts[0xBD] > 0`, exactement comme `AlundraCellStoreProductionTests.cs:103-104`.
- **T3 — la résolution, comme donnée pure** : id 302 → **deux** fichiers `sfx_0302_0/1.wav` aux taux
  **11025 et 10401**, bouclés ; id 61 → VAB **global** (`vab_id −1`) ; un des 91 ids sans tonalité
  échoue en douceur ; **et la chaîne `RefSfxId`** — un id du groupe 56 interrogé **sous un autre
  groupe** résout vers le frère de la chaîne, pas vers ses propres tonalités (le seul cas où le groupe
  change quoi que ce soit, D-E11-6).
- **T4 — les prédicats** : `0xA8` et `0xBA` écrivent `Result = 0`. **Le test doit d'abord montrer la
  valeur périmée** : poser `Result = 1` par un prédicat précédent, dispatcher `0xA8`, assérer 0.
- **T5 — le lecteur lui-même, sans lequel toute sa mécanique partirait non prouvée** (blocage P1 de la
  relecture, §2.2b). Un vrai `AlundraSoundPlayer` sur un vrai `AudioService` monté sur un faux
  `IAudioBackend` + faux `IAudioClipProvider` réécrits dans `Alundra.Tests` — la forme des tests du
  moteur (`AudioServicePlaySoundTests.cs:16`). Assertions chiffrées : id 302 → **deux** clips, tous
  deux `IsLooped` ; id 300 → **un** clip, non bouclé ; le **plafond `MaxVoices`** refuse le second
  déclenchement de 302 tant que ses voix vivent, **et l'autorise à nouveau une fois ses voix
  terminées** (sans quoi un plafond permanent passerait le test) ; **deux demandes successives du
  son 300 produisent deux clips** — l'assertion qui tue un lecteur muet après le premier son
  (D-E11-4) ; id 61 joue depuis le guid du manifeste.
- **T6 — les trois opcodes que l'intro ne dispatche jamais** (blocage P2 de la relecture). `0x12`,
  `0x75` et `0xBE` sont livrés sans aucune couverture sinon, et **leur dérivation d'id n'est PAS celle
  de `0xBD`** : `0x12`/`0x75` prennent `variables[1]` **seul** sur une instruction de 2 octets
  (`EntityEventHandlers.cs:694-698`, `:2208-2212`), alors que `0xBD`/`0xBE` composent
  `(v[2] << 8) | v[1]` sur 3 octets (`:3597-3610`). Copier la dérivation de `0xBD` lirait **au-delà de
  l'instruction**. Test sur document synthétique : id remis au seam **et** avance, pour chacun des
  trois. **L'encodage est épinglé, sinon la mutation ne mord pas** (blocage P2 de la seconde
  relecture) : l'octet qui SUIT l'opérande de `0x12`/`0x75` doit être **non nul et différent de
  l'opérande**, faute de quoi `(v[2] << 8) | v[1]` rend le même id et le test passerait sous
  mutation. La mutation appariée doit faire échouer l'assertion **d'id**, pas seulement celle
  d'avance.

**Cinq mutations obligatoires, chacune appariée au test qu'elle doit casser** — et chacune vérifiée
exécutable avant d'être imposée (le corollaire d'E7 : une neutralisation vide rend son critère vacu) :

| mutation | test qui doit tomber |
|---|---|
| ne jouer que la première tonalité (boucle par tonalité du **lecteur**) | **T5** : 302 rend un clip au lieu de deux |
| retirer la branche `is { }` du `0xBD` | **T1** : zéro demande |
| laisser `0xA8` en `UnknownOpcode` | **T4** : la valeur périmée revient |
| appliquer la dérivation de `0xBD` à `0x12` | **T6** : id faux |
| ignorer le groupe dans la chaîne `RefSfxId` | **T3** : le frère n'est plus trouvé |

**Non-régression, chaque point étant un arrêt s'il bouge** : `Alundra.Tests` **599 + n** ; convertisseur
**138** ; les six goldens **inchangés** avec preuve positive d'exécution — **sauf** `intro-trace-389.txt`,
dont les 23 lignes `0xBD` passent de `Degraded` à `Implemented` : ce ré-étiquetage doit être prouvé
**pur** (normaliser la colonne `Kind` rend le fichier byte-identique au précédent), exactement comme
E7.a l'a fait.

**Acceptation** : build 0 erreur, les suites ci-dessus, **plus un point de recette de bout en bout
exigé par la relecture** — sur le chemin d'installation réel pour le monde
`Ship Klark (beginning)-389`, les **quatre** ids 300/301/302/61 se résolvent vers leurs fichiers de
tonalités, et ce même test échoue si la banque n'est pas installée. **Et la validation en jeu par
l'utilisateur** : il doit entendre le lit sonore dès l'entrée sur la carte, les mouettes, et **le son
de la trappe**.

### E11.b — Le reste des opcodes *(à approuver plus tard)*

`0xA5` (stop all + reprise du BGM), `0xAB`/`0xBF` (remix L/R d'une voix **déjà jouée** — ils ne jouent
rien), `0xA6`/`0xA7` (BGM, inertes tant qu'E11.c est bloquée), le son de départ de `0x53`, **et
l'export de la table carte → groupe de sons** (D-E11-6), à faire quand une seconde carte rendra la
déviation n°3 observable — pas avant, parce qu'elle impose un ré-export complet pour zéro effet ici.
**Plus l'anti-doublon par frame** (D-E11-4), avec son propriétaire de frame nommé : qui appelle la
remise à zéro, depuis quel site de la frame, et ce qui se passe sans lecteur installé.

### E11.c — La musique *(BLOQUÉE, ne pas approuver)*

Prérequis dur : ré-extraire les BGM 19 et 21-46 dans `alundra-datas-analyser` (§1.5). Ensuite
seulement : `.sound` streaming pour le BGM, `MusicPlayer.Play` à l'entrée de carte, index 25 pour la 389.

## 5. Budget et arrêts

**Budget** : E11.a en un commit, ≤ 4 tours (relevé d'un tour après la relecture : les faux backend et
fournisseur de clips de T5 sont un livrable à part entière). **Arrêts** : tout point du §4 qui bouge ; toute modification
du convertisseur, d'`alundra-project/`, du sous-module ou d'`AlundraLogicClock` ; toute tentative de
faire porter la preuve par les traces dorées (§2.1) ; et si un test T1-T4 passe **avant** le correctif,
il ne teste pas ce qu'il prétend — arrêt.
