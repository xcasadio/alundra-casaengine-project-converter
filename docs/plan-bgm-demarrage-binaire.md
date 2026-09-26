# Plan — BGM : démarrer, arrêter et relancer la musique comme l'exécutable

**État** : ✅ phases 0 à 3 **closes le 2026-09-26, validées en jeu par l'auteur** ; 🚧 phase 4 (suites, pointeur, merge) demandée le 2026-09-26. Rédigé le 2026-09-25, relu par des relecteurs frais (REVISE, REVISE, puis **READY** en clôture) ;
arbitrages P1, P2, P3, P7 tranchés par l'auteur (D5 à D8), les autres points à valider restent des propositions.
**Approuvé par l'auteur le 2026-09-25, mode AUTO** (travail réversible dans le périmètre de ce plan, un commit par
tâche sur les branches dédiées, ni push ni merge). T0.1 bloquée puis débloquée le même jour, après le merge du plan
audio par l'auteur.
**Naissance** : suite S1 du plan `docs/plan-audio-mix-exact-muet.md` (worktree `nostalgic-kapitsa-11fb13`, branche
`chantier/audio-mix-exact` ; **approuvé et en exécution en mode AUTO** depuis le 2026-09-25, sa T2.1 analyseur pas
encore faite au moment de cette rédaction : analyseur de ce worktree encore à `bbf3396`) et sa décision D5 : « Les `PlaySeq` absents du binaire et `0xA7` sont consignés en
suite dédiée ». Consigne de l'auteur pour cette suite (2026-09-25) : établir dans le binaire comment l'original lance
la BGM après `0xA7`, `LoadMapSequence` et l'entrée de carte, dire si l'analyseur et la DLL divergent, proposer un plan
d'alignement (analyseur et DLL), validation en jeu par l'auteur.
**Modèle** : sections du modèle du skill `plan` (identique à `CasaEngineMonogame/ai-agent/plan-template.md`). Le dépôt
parent n'a ni `AGENTS.md` ni `ai-agent/` ; ses plans vivent dans `docs/plan-*.md`, ce plan aussi.

Decisions: see ADR-0004 (`docs/decisions/0004-bgm-follows-the-executables-sequence-state.md`).

Ce fichier doit être mis à jour pendant le travail : l'icône au début de chaque tâche indique son statut courant.

## Réponse courte

**Oui, l'analyseur et la DLL divergent de l'exécutable, et la DLL plus largement que la seule `0xA7`.**

Dans `ALUN_CD.EXE`, la musique ne démarre **que** par `StopAllSound` (`0x80049af4`), seul appelant BGM de `PlaySeq`.
Charger une séquence ne la joue jamais. `InitializeBgm` est un **arrêt** (rembobiné), pas une relance. D'où, dans
l'original :

| Événement | Original [binaire] | DLL aujourd'hui |
|---|---|---|
| Entrée de carte, index changé | charge, pose `g_resetSoundFlag`, et `StopAllSound` joue la piste à la fin de la première frame | joue à l'installation du monde |
| Entrée sur une des 21 cartes d'index `-1` | charge la piste 1 **sans la jouer** : `StopAllSound` sort car l'index courant vaut `-1` → silence | joue la piste 1, et la **relance du début** à chaque passage entre deux cartes `-1` |
| `0xA7 n,0` (54 sites) | arrête l'ancienne, charge `n`, **ne joue pas** | joue `n` tout de suite |
| `0xA7 n,1` (12 sites) | charge `n` puis `StopAllSound` → joue | joue (deux fois, sans effet audible) |
| `0xA5` (99 sites) | joue la séquence chargée si elle ne joue pas ; **ne touche pas** une séquence qui joue | **relance du début** la piste courante |
| `0xA6 0` (58 sites) | **arrête** la musique | la **relance** |
| `0xA6 ≠0` (79 sites), bas de rampe | **arrête** la musique ; le volume maître revient 57 tics plus tard, sur du silence | relance la piste à volume nul ; elle redevient audible au retour du maître |
| Départ de warp (portail ou `0x53`) vers un index musical différent | ne charge rien : arme le fondu (son de warp muet) ou arrête la musique (son de warp jouable) ; la piste de destination part à l'arrivée | charge et joue la piste de destination **au départ**, pendant le fondu d'écran |

Le schéma le plus fréquent du corpus le montre (22 sites, fin de boss ou de rêve) : `0xA6 1` (fondu puis arrêt) →
attente → `0xA7 42,0` (chargement muet) → boucle `0xA8` (attendre la fin du chargement) → `0xA5` (**c'est là que la
piste 42 démarre**) → … → `0xA7 0,0` (arrêt). Dans la DLL, la musique du boss revient pendant le fondu, puis la 42
part à `0xA7` et repart du début à `0xA5`.

## Objectif

1. **La DLL modélise la séquence BGM comme l'exécutable** : un index courant brut (`g_currentMapSoundIndex`), une
   séquence chargée ou non, qui joue ou non, et le drapeau `g_resetSoundFlag`. Charger ≠ jouer ; arrêter ≠ relancer ;
   `StopAllSound` joue sans relancer ce qui joue déjà, et ne fait rien quand l'index courant est négatif.
2. **`0xA5`, `0xA6`, `0xA7`, l'entrée de carte et le départ de warp suivent ce modèle**, sans changer la
   répartition des opcodes dans le runner.
3. **L'analyseur perd les deux `PlaySeq` que l'exécutable n'a pas** (`LoadMapSequenceCore`, cas 5 du streaming) et le
   `SetSeqVolume` de `LoadMapSequenceCore`, et teste le son de warp sur les bons champs de la fiche.
4. La doc des plans E11.b et E11.c est corrigée là où elle décrit l'ancienne lecture.

Ce que le chantier ne livre pas est dans « Hors périmètre ».

## État vérifié du dépôt (2026-09-25)

Base de la reconnaissance : `main` `b2cdca7` (moteur `43688074`, analyseur `bbf33962`, `git submodule status`).
Worktree `silly-mayer-47d565` propre, sous-modules non initialisés. Les citations `fichier:ligne` ci-dessous valent à
cette base ; T0.1 les relit sur la base d'exécution (P1). Dans le checkout principal, le moteur a une modification de
l'auteur (`git status` : ` m CasaEngineMonogame`) : jamais indexée, jamais touchée.

### Faits [binaire]

Source : `ALUN_CD.EXE` (France), capstone, texte chargé en `0x80020000`, décalage de fichier `0x800`. Script de
lecture : `mips.py` du scratchpad de la session (désassemblage d'une plage, appelants d'une adresse, paires
`lui`/décalage vers une globale), recopié au §« Outils » en fin de plan.

- **B1 — `PlaySeq` (`0x8008f188`) a deux appelants.** `0x80049b60`, dans `StopAllSound`. `0x80049428`, dans
  `PlaySoundEffect` (`0x800490fc`) : `PlaySeq(g_loadedSequenceHandles[fiche.SeqNum], 1, 1)` (table `0x80175d00`), la
  **séquence d'un bruitage**, sans rapport avec la BGM. Aucun pointeur vers `0x8008f188` dans l'exécutable (recherche du
  mot).
- **B2 — `StopAllSound` (`0x80049af4`)** : si `g_currentMapSoundIndex` (`0x80173844`, `lh`) est négatif, **retour
  immédiat** (`bltz` en `0x80049b04`). Sinon : si `g_soundEffectState` (`0x80175850`) ≠ 0, extinction des 24 voix SPU
  (`FUN_80097ef0(0, 0xffffff)`), `FUN_8008a718(0)`, état à 0 ; puis volume maître `0x7f`/`0x7f` ; puis
  `SetSeqVolume(g_requestedSeqId, 0x7f, 0x7f)` ; puis `PlaySeq(g_requestedSeqId, 1, 1)` **sans condition**.
- **B3 — Appelants de `StopAllSound`** : `0xA5` (`0x800410d0`) ; `LoadMapSequence` si index > 0 et drapeau
  stop-all (`0x80049d00`) ; `HandleMapSoundStreaming` si `g_resetSoundFlag` (`0x8004b234`) ; cas 5 du streaming si
  `g_forceStopAllSound` (`0x8004b5cc`) ; `AI_FUN_8007bd8c` (`0x8007c718`).
- **B4 — `PlaySeq` → `StartSequencePlayback` (`0x8008f088`)** : efface les bits `0x200` et `0x4` des drapeaux de la
  séquence, pose le compte de boucles, et en mode 1 pose le bit `0x1` (joue), remet `+0x48` à 0 et `+0x2b` à 1, pousse
  le volume. **Ne touche pas à la position** : sur une séquence qui joue, rien d'audible ; sur une séquence arrêtée
  (rembobinée) ou fraîchement ouverte, départ du début.
- **B5 — `InitializeBgm` (`0x8008f458` → `0x8008f2e8`) est un arrêt** : efface les bits `0x1`, `0x2`, `0x8`, pose
  `0x4`, appelle `FUN_80093ef4`, remet la position (`+0x4` et `+0xc` ← `+0x8`, début de séquence) et l'état des 16
  canaux. Ce n'est **pas** une relance.
- **B6 — Le séquenceur ne fait avancer qu'une séquence dont le bit `0x1` est posé** (`0x8008e474`-`0x8008e488` :
  `andi 1` puis `FUN_8008ebf8`). **Ouvrir une séquence ne la joue pas** : relevé de toutes les écritures en `+0x90`
  de l'exécutable (`flag_writers.py`, fin de plan) ; `LoadSeq` et son initialisation (`0x8008b8c8`-`0x8008bcc0`) n'en
  contiennent aucune ; seuls deux sites posent le bit `0x1` : `StartSequencePlayback` (`0x8008f124`) et
  `FUN_8008da70` (`0x8008db6c`). Ce dernier n'est appelé qu'en `0x8008d88c`, dans la fin de séquence du séquenceur,
  quand le champ `+0x3c` (séquence chaînée) ≠ `0xff` ; or les seules écritures de `+0x3c` sur l'état de séquence y
  mettent `0xff` (`0x8008de20`, `0x8008ef74`) : ce chemin ne sert pas.
- **B7 — `LoadMapSequence` (`0x80049be0`)** : index courant ← index demandé ; `InitializeBgm` (arrêt),
  `ResetSomethingSound` (fermeture), `FreeLoadedVab` de l'ancienne ; si l'index vaut `-1`, il vaut 1 pour le
  chargement ; chargement du VAB et de la séquence (`LoadSeq`) ; puis **index courant ← index demandé brut**
  (`0x80049cf4`, donc `-1` reste `-1`) ; si index > 0 et drapeau stop-all : `StopAllSound` ; enfin
  `g_resetSoundFlag = 1` (`0x80049d10`). **Ni `PlaySeq`, ni `SetSeqVolume`.** Un échec de `LoadSeq` n'imprime qu'un
  message, la suite s'exécute quand même.
- **B8 — `g_resetSoundFlag` (`0x8016512c`)** : seul `LoadMapSequence` le met à 1. Remis à 0 par
  `InitializeSoundSystem`, `LoadBgm` (les deux branches, `0x80049b8c`), le gestionnaire de `0xA7` quand son drapeau
  vaut 0 (`0x8004b194`), et `HandleMapSoundStreaming` après usage (`0x8004b23c`).
- **B9 — `HandleMapSoundStreaming` (`0x8004b1d4`)** : après `FUN_80048fcc` et deux tests de bits sur `0x801eb39c`,
  si `g_resetSoundFlag` : `StopAllSound`, puis drapeau à 0 ; **ensuite** `FinalizeAudioBuffers` (vidage de
  l'anti-doublon) ; puis la machine de streaming (état 1 à 5, table de sauts `0x80026538` lue en `0x8004b25c` :
  `0x8004b280`, `0x8004b330`, `0x8004b388`, `0x8004b40c`, `0x8004b568`). **L'état 1** (`0x8004b280`-`0x8004b2a4`)
  appelle `InitializeBgm(g_requestedSeqId)`, `ResetSomethingSound(g_requestedSeqId)` et `FreeLoadedVab` : l'ancienne
  séquence est arrêtée et fermée dès le premier appel qui suit la demande. Le cas 5 (`0x8004b578`-`0x8004b5d8`) : `LoadSeq`,
  `SetSeqVolume(0x7f, 0x7f)`, `StopAllSound` seulement si `g_forceStopAllSound`, état à 0. **Pas de `PlaySeq`.**
- **B10 — Entrée de carte.** `LoadMapSounds` (`0x8004a09c`) : si l'index de la carte vaut 0 ou l'index courant, rien.
  Sinon : si une séquence existe, `InitializeBgm`, `FUN_8008a718(0)`, `ResetSomethingSound` ; si l'index ≠ 45,
  `LoadMapSequence(index, 0)` ; puis `FUN_8008f808(seq, 0x7f, 10)` ; puis le groupe de sons. Le bloc d'entrée
  appelle ensuite `FUN_8002baec(1)` (`0x8002c3e4`), la fonction de frame (l'`Update` de l'analyseur), qui exécute
  `UpdateWorld` (`0x8002bc4c`, entités et scripts) **puis** `HandleMapSoundStreaming` (`0x8002bd04`, appel sans
  condition). **La musique d'entrée démarre donc là, par le drapeau, à la fin de la première frame** ; un `0xA6`
  exécuté par un script pendant cette frame efface le drapeau avant.
- **B11 — `FUN_8008f808` (appelé en `0x8004a12c`)** → `FUN_8008f760(seq, 0, 0x7f, 10)` → `FUN_8008f690` : un
  crescendo de séquence (paramètres en `+0x3e`/`+0x40`/`+0x94`/`+0x98`/`+0x42`, non posés si la séquence porte le
  bit `0x4` ou `0x100`), puis bit `0x10` posé et `0x20` effacé. **Sans effet audible** : `StopAllSound` pose le volume
  de séquence à `0x7f` avant `PlaySeq`. La conclusion D-C-3 d'E11.c (plein volume, pas de fondu) tient, pour cette
  raison-là.
- **B12 — Opcodes** (gestionnaires aux adresses des commentaires de `EntityEventHandlers.cs:3126-3150`) : `0xA5`
  (`0x800410c8`) → `StopAllSound`, taille 1. `0xA6` (`0x800410e8`) → `LoadBgm(lbu v[1])`, taille 2. `0xA7`
  (`0x80041114`) → `FUN_8004b114(lbu v[1], lbu v[2])`, taille 3 : **les opérandes sont des octets non signés**, la
  branche « index < 0 ignoré » est inatteignable depuis `0xA7`. `0xA8` (`0x80041144`) → `Result = IsSoundLoading()`.
- **B13 — `FUN_8004b114`** : 0 → index courant 0, `InitializeBgm`, `ResetSomethingSound`, `FreeLoadedVab`. > 0 et
  `g_cdIsReady` nul → `LoadMapSequence(n, drapeau)`, puis drapeau 0 → `g_resetSoundFlag = 0`. > 0 sinon →
  `FUN_8005a724` (commandes CD `0xb` et `9` quand une piste XA joue, `0x8005a724`-`0x8005a798`, sans effet sur la
  séquence), `g_forceStopAllSound = drapeau`, `g_soundLoadState = 1`, index courant ← n (streaming, B9). **Dans
  les deux chemins, drapeau 0 = chargée sans être jouée.** **Le chemin actif est le streaming** : la fonction
  principale appelle sans condition `FUN_800815c4` (`0x8002c03c`), qui appelle l'initialisation CD `0x8004e7bc`, qui
  écrit `g_cdIsReady = 1` (`0x8004e85c`) ; plus tard, `InitializeSoundSystem` (`0x8002c240`) ne remet 0
  (`0x80048514`) que si le mot `0x8009a858` est non nul (`0x800484ec`-`0x800484f8`), or ce mot vaut 0 dans
  l'exécutable et **aucune instruction ne l'écrit** (aucune écriture en `-0x57a8`) ; `0x80048514` et `0x8004e85c`
  sont les deux seules écritures de `g_cdIsReady` (`flag_writers.py`). Conséquence : `0xA7 n` n'arme jamais
  `g_resetSoundFlag` ; si l'entrée de carte l'a armé dans la même frame, le `StopAllSound` qu'il déclenche joue
  l'**ancienne** séquence, que l'état 1 du streaming arrête aussitôt dans le même appel de
  `HandleMapSoundStreaming` (`0x8004b234` puis `0x8004b288`, B9) : silence net.
- **B14 — `LoadBgm` (`0x80049b7c`)** : `g_resetSoundFlag = 0` ; opérande ≠ 0 → `g_soundEffectState = 0x78` ;
  opérande 0 → `InitializeBgm` (arrêt).
- **B15 — Machine de fondu `FUN_8004b674`** : l'état est décrémenté en tête ; à `n == 0x3c` : maître (0, 0),
  `InitializeBgm` (**arrêt**), extinction des 24 voix ; à `n == 3` : maître `0x7f`. Rampe calculée sur l'état avant
  décrément (constante `0x88888889`), conforme à `AlundraBgmFadeDirector`.
- **B16 — Table carte → musique** (`0x800c659c`, 483 `int32`, `GetMapSoundIndex` en `0x80049d3c` la lit par `lw`,
  signée) : **21 entrées `-1`** (cartes 183-185, 271-274, 289-292, 311, 357-361, 381, 393, 416, 481), **une `45`**
  (476), **aucune `1`**. Même contenu que `alundra-project/Maps/music-index.json` (`jq`). Table de surcharge par
  drapeau (`0x800a81e4`, 7 entrées : cartes 331, 10 ×4, 163, 476) : **aucune carte `-1` n'y figure.**
- **B17 — Départ de warp : `HandleMapSoundEffects` (`0x80049f1c`)**, appelé au départ avec la carte visée et le son
  de warp : `ResetSoundEffectRuntime` ; si la fiche du son (table `0x800a82e8`, pas `0x16`) a `SeqNum == -1`
  (`+0xa`) **et** `MaxVoices == 0` (`+0x10`), le son vaut 0 (`0x80049f58`-`0x80049f78`) ; index visé ← index de la
  carte visée, ou l'index courant s'il vaut 0 ; **index visé ≠ index courant** : son nul → `g_soundEffectState =
  0x78` écrit directement (fondu armé, drapeau de reset intact, `0x80049fb0`-`0x80049fb8`), sinon `LoadBgm(0)`
  (arrêt, drapeau effacé) puis le son ; **index égal** : le son seul s'il n'est pas nul. **Aucun chargement, aucune
  lecture de musique.** La décompilation (`HandleMapSoundEffectsCore`, `SoundManager.cs:5363-5364`) teste les octets
  0 et 6 de la fiche (`VabId`, `Note`) au lieu de `SeqNum` et `MaxVoices` : erreur de transcription.
- **B18 — Autres entrées BGM du binaire, hors de ce plan** : `AI_FUN_8007bd8c` (`LoadBgm(0)` en `0x8007c65c`,
  `StopAllSound` en `0x8007c718`) ; `AI_FUN_8007f6c8` (`LoadBgm(0)` en `0x8007fdd4`) ; `0x8004a9e0` (`LoadBgm(0)` ou
  `LoadMapSequence(x, 1)` selon `0x801660d0`, rôle non établi). Aucun n'est porté dans la DLL (`rg` sur `Alundra/`).

**Conséquence de B2 + B7 + B16** : sur les 21 cartes `-1`, la piste 1 est chargée mais `StopAllSound` sort sans rien
jouer (index courant `-1`) : **l'original y est muet**, sauf si un script change l'index par `0xA7`. La lecture
d'E11.c (« `-1` joue la piste 1 », `docs/plan-e11c-musique.md` §1.1, sa « correction de fond ») ne regardait que le
chargement.

### Corpus [mesuré]

Script `bgm_corpus.py` (scratchpad de la session, recopié en fin de plan) : descente récursive depuis chaque entrée
des tables A-F de chaque `Maps/**/events/*.events.json` d'`alundra-project/`, au pas réel de chaque opcode
(`EventOpcodeSizeTable.cs`), en suivant `0x02`/`0x03`/`0x04` ; borne basse (sauts dynamiques `0x7D`-`0x81` non
suivis). 483 fichiers.

| Opcode | Sites | Cartes | Opérandes |
|---|---|---|---|
| `0xA5` | 99 | 52 | — |
| `0xA6` | 137 | 68 | `1` : 79 ; `0` : 58 |
| `0xA7` | 88 | 33 | `v[2] = 0` : 76, dont 22 `(0,0)` ; `v[2] = 1` : 12 |

Enchaînements relevés (extraits, `--context 14`) : `Kline's Nightmare-123` : `A6(1)` @56, `37(130)`, `A7(5,0)` @60,
…, boucle `A8`/`03`, `A5` @129. `Lars' Crypt-25` : `A6(1)` @111, `37(125)`, `A7(5,0)` @115, …, `A8`/`03`, `A5` @134 ;
puis `A6(1)` @427, `37(140)`, `A7(42,0)` @432, …, `A5` @450, attentes 240+240+200, `A7(0,0)` @474. `Inoa-226` :
`A6(0)` @417 puis `A7(10,1)` @419 ; `A7(4,0)` @1119, `37(60)`, `A8`/`03`, `A5` @1131. `A7(42,0)` : 22 sites sur 22
cartes (boss et rêves), **chacun suivi d'un `A5`** dans les 14 instructions décodées suivantes (vérifié sur la sortie
`--context 14`) ; bloc `A6(1)` / `A7(42,0)` / `A5` / `A7(0,0)` relu en entier sur `Lars' Crypt-25` et
`Murgg Golem (Boss)-399`.

### Côté analyseur (`alundra-datas-analyser`, `bbf33962`, `AlundraTools/AlundraEngine/Sound/SoundManager.cs`)

- `LoadMapSequenceCore` (`:523-581`) : `SetSeqVolume(0x7F, 0x7F)` (`:566`) et `else if (IsBgmActivated) PlaySeq(...)`
  (`:572-575`), absents de B7 ; le bloc stop-all est dans le `else` de « séquence invalide » (`:560-576`), alors que
  B7 continue après le message.
- Cas 5 du streaming (`:5572-5586`) : `else if (IsBgmActivated) PlaySeq(...)` (`:5580-5583`), absent de B9.
- `HandleMapSoundStreamingCore` (`:5446-5452`) consomme `g_resetSoundFlag` comme B9 ; `GameEngine.cs:216-217` appelle
  `LoadMapSounds` puis `Update(1)`, qui appelle `HandleMapSoundStreaming` (`GameEngine.cs:1579`) : une fois les deux
  `PlaySeq` retirés, la musique d'entrée démarre encore dans le bloc d'entrée, comme B10.
- `IsBgmActivated` : retiré par la tâche T2.1 du plan audio, qui **garde volontairement** `else PlaySeq(...)` aux deux
  sites avec un renvoi à S1. Les deux plans touchent les mêmes lignes (voir P1).
- **`Alundra.sln` ne builde pas à `bbf33962`, avant tout changement** : 4 erreurs NU1605 sur `AlundraGame.csproj` et
  `AlundraTools.csproj` (MonoGame 3.8.4.1 contre 3.8.5.1 exigé par le MGUI de « update MGUI »), mesuré par la T0.1 du
  plan audio (`plan-audio-mix-exact-muet.md:97-102`, point ouvert O5 de ce plan-là). `AlundraEngine.csproj` et
  `AlundraDataExtractor.csproj` buildent seuls. `AlundraGame` (le lecteur de référence) ne se construit donc pas tant
  que l'auteur n'a pas tranché O5.
- Écarts vus en passant, **non traités ici** : `InitializeSoundSystemCore` pose `g_cdIsReady = 1` sans condition
  (`:331`), le binaire ne l'écrit qu'à 0 (`0x80048514`, après une réinitialisation CD) et à 1 dans l'initialisation CD
  `0x8004e7bc` ; la tête de `HandleMapSoundStreaming` (B9 : `FUN_80048fcc`, bits de `0x801eb39c`) manque dans
  `HandleMapSoundStreamingCore`. L'analyseur n'a **aucun projet de test** (`AlundraTools/Alundra.sln`).

### Côté DLL (`Alundra/Scripts/`)

- Dispatch (`AlundraEventProgramRunner.cs:801-853`) : `0xA5` → `BgmFadeDirector.StopAllSound()` ; `0xA6` →
  `LoadBgm(v[1])` ; `0xA7` → `MusicPlayer.PlayFromRawIndex(v[1])` puis, si `v[2] ≠ 0`, `StopAllSound()`. **Cette
  répartition est déjà celle de B12/B13** ; ce sont les implémentations qui divergent.
- `AlundraMusicPlayer` (singleton de session) : `PlayFromTableIndex` (`:157-179`) stocke `_lastResolvedIndex =
  directive.PlayIndex` (`:177`), donc **1 pour une carte `-1`** (B7 garde `-1`) et démarre la voix tout de suite ;
  `PlayFromRawIndex` (`:188-202`) démarre la voix pour tout index > 0 ; `RestartIfActive` (`:205-214`) arrête puis
  redémarre du début.
- `AlundraBgmFadeDirector` : `StopAllSound` (`:152-167`) appelle `RestartIfActive` sans garde d'index (doc `:26-28` :
  « jamais négatif par construction ») ; `LoadBgm(0)` (`:172-178`) et le bas de rampe (`:208-215`) appellent
  `RestartIfActive`, là où B14/B15 arrêtent.
- Entrée de carte : `InstallAudioSystems` → `TriggerMapEntryMusic` (`AlundraWorldProxy.cs:1005`, `:1462-1475`) →
  `PlayMapMusic`, à l'installation du monde. Site de fermeture de frame : `_logicClock.CloseFrame()`
  (`AlundraWorldProxy.cs:2226`) puis `SoundPlayer?.FlushFrameSounds()` (`:2234`), le portage de
  `FinalizeAudioBuffers` (D-B-4 d'E11.b). Le drapeau de B9 se consomme juste avant ce vidage.
- **Départ de warp** : `AlundraWarpDirector.PlayDepartureSound` (`AlundraWarpDirector.cs:456-462`, portail) et le
  départ par `0x53` (`:377-381`) appellent `AlundraMusicPlayer.Instance.PlayMapMusic(carte visée)` puis
  `PlaySfx(son)`. La doc (`:443-455`) le présente comme le portage de `HandleMapSoundEffects` : le port **charge et
  joue la piste de destination au départ** ; l'installation de la destination tombe ensuite sur la garde « index
  égal ». B17 dit le contraire. Table des sons de warp : `WarpBehaviorTable` (`:65-69`).
- `AlundraSoundBank` lit `max_voices` (`AlundraSoundBank.cs:57`, `:216`, `:255`) mais **pas `seq_num`** ; le
  manifeste `Sounds/sfx-manifest.json` porte les deux (`jq`) : 60 fiches sur 961 ont `seq_num == -1` et
  `max_voices == 0`, dont le son 69 (comportement de warp 1) ; les sons 73, 74, 76 ont une séquence.
- `0xA8` rend `Result = 0` (D-E11-5) : les chargements du port sont instantanés, la boucle d'attente sort tout de
  suite. Cohérent, gardé.
- Tests qui touchent l'ancienne lecture (`rg`, noms relevés) : `AlundraBgmFadeDirectorTests.cs`, huit tests, dont
  ceux qui assertent une relance (`Advance_FullyPastState0x3d_TheSwapTick_RestartsWhateverBgmIsCurrentlyResolved`,
  `LoadBgm_ZeroOperand_RestartsImmediately_NeverArms_NoMasterCall`, `StopAllSound_NotArmed_…_RestartsBgm`,
  `StopAllSound_Armed_…_RestartsBgm`) et ceux qui passent par elle (`Advance_FromArmed0x78_…`,
  `PlaySfx_WhileArmed_…`, `ProductionSite_SyntheticProgram_…`) ; `AlundraMusicPlayerTests.cs` : T1, T1 bis et T4
  démarrent la musique à l'installation, T2 et T5 sont à relire ; `AlundraMusicIndexTableTests.cs:87`
  (`Map183_RawIndexMinusOne_ResolvesToPlayIndex1`, résolution de table, qui peut rester vraie) ;
  `AlundraEventProgramRunnerTests.cs:2685-2797` (dispatch `0xA5`-`0xA7` sur un faux lecteur qui implémente
  `RestartIfActive`, `:99`, `:115`).
- Goldens : aucun `0xA5`-`0xA7` sur la 389 (`docs/plan-e11c-musique.md` §1.5) ; le harnais d'intro remplace le lecteur
  de musique par son propre faux (`AlundraMusicPlayer.cs:87-89`). Ils ne doivent pas bouger.

## Décisions verrouillées

| Réf | Décision |
|---|---|
| D1 | Aligner l'analyseur **et** la DLL sur le binaire pour le démarrage de la BGM ; validation en jeu par l'auteur ; aucun code avant approbation (consigne de l'auteur, 2026-09-25). |
| D2 | Un défaut avéré de l'original se corrige, preuve écrite en commentaire et au plan ; entre défaut et choix voulu, dans le doute, question à l'auteur (règle de l'auteur du 2026-09-25). |
| D3 | Les `PlaySeq` absents du binaire et `0xA7` relèvent de cette suite, pas du plan audio (D5 du plan audio, 2026-09-25). |
| D4 | Tout fait dont dépend une tranche est vérifié dans `ALUN_CD.EXE` et marqué [binaire] (règle retenue le 2026-09-24/25). |
| D5 | P1 : ce plan s'exécute **après le merge du plan audio dans `main`** ; T0.1 le vérifie et s'arrête sinon (auteur, 2026-09-25). |
| D6 | P2 : les 21 cartes `-1` sont **muettes, comme l'original** (auteur, 2026-09-25). |
| D7 | P3 : `g_resetSoundFlag` est **porté**, consommé au site de fermeture de frame avant `FlushFrameSounds`, effacé par `LoadBgm` (auteur, 2026-09-25). |
| D8 | P7 : le départ de warp suit **l'original** : aucun chargement ; fondu si le son de warp est nul, `LoadBgm(0)` sinon ; la piste de destination part à l'arrivée (auteur, 2026-09-25). |
| D9 | Merge et suites S1 à S4 demandés ; le merge répare aussi le pointeur du moteur de `main`, sur `b02d3e86` (auteur, 2026-09-26). |

## Points à valider par l'auteur (arbitrages proposés)

| Réf | Proposition | Pourquoi |
|---|---|---|
| P1 | **Ordre avec le plan audio, en cours d'exécution.** Il possède les lignes `SoundManager.cs:572-575` et `:5580-5583` (sa T2.1 les réécrit en `else PlaySeq(...)` avec un renvoi à S1), et touche aussi la DLL (`AlundraSoundBank.cs` en T4.1, le cas `0xBF` du runner en T4.3). **Proposé : ce plan s'exécute après le merge du plan audio dans `main` par l'auteur.** Base : `main` à ce moment ; T1.1 retire alors les deux `else PlaySeq(...)` et leur renvoi. T0.1 le vérifie et s'arrête sinon. **Variante** si l'auteur veut commencer avant : base `main` `b2cdca7`, phase DLL d'abord ; T1.1 ne démarre que depuis le commit de la branche d'analyseur `chantier/audio-mix-exact` qui porte la T2.1 audio (récupéré par `git fetch` depuis le clone d'analyseur du worktree audio), arrêt s'il n'existe pas ; les ajouts disjoints dans `AlundraSoundBank.cs` et les tests du runner se réconcilient au merge, par l'auteur. Dans les deux cas, jamais deux branches ne modifient ces lignes indépendamment. | Deux plans approuvés ne doivent pas posséder les mêmes lignes ; attendre le merge supprime tout chevauchement. |
| P2 | **Cartes `-1` : muettes, comme l'original.** Le binaire est net (B2, B7, B16) ; c'est très probablement un choix (valeur sentinelle « pas de musique », le remappage vers 1 ne servant qu'à ne pas lire la table d'offsets en `-3`). Variante si l'auteur y voit un défaut : ces cartes jouent la piste 1 (le port d'aujourd'hui), mais sans la relancer entre deux cartes `-1`. | Question D2 : défaut ou choix. L'auteur peut comparer avec le jeu réel sur Inoa de nuit (289). |
| P3 | **Porter `g_resetSoundFlag`** : l'entrée de carte charge et pose le drapeau ; le site de fermeture de frame, juste avant `FlushFrameSounds`, appelle `StopAllSound` si le drapeau est posé, puis l'efface ; `LoadBgm` l'efface. La musique d'entrée démarre donc à la fin de la première frame (même frame qu'aujourd'hui, un peu plus tard dans la frame). Variante plus simple : `StopAllSound` tout de suite à l'installation ; seul écart, un `0xA6` exécuté par un script dans la première frame n'annulerait plus le départ. | B10 : c'est le mécanisme réel, et le site de fermeture existe déjà (D-B-4). |
| P4 | **Noms conservés** : `PlayMapMusic`, `PlayFromRawIndex`, `StopAllSound`, `LoadBgm` gardent leur nom, leur sémantique et leur doc XML sont corrigées. `IAlundraMusicPlayer.RestartIfActive` est **remplacé** par `StopSequence()` (port d'`InitializeBgm`) et `PlaySequence()` (port de `PlaySeq`), plus `CurrentMapSoundIndex`, `ResetSoundFlag` et `ClearResetSoundFlag()`. | La sémantique de `RestartIfActive` n'existe pas dans le binaire ; renommer les autres membres n'apporterait rien. |
| P5 | Extinction des 24 voix (B2 si fondu armé, B15) : le port arrête les voix de bruitage (`StopAllSfx`) et, en B15, la voix de musique par `StopSequence`. En B2, la voix de musique qui joue n'est pas coupée. **Déviation déclarée** : dans l'original, les notes en cours de la séquence sont coupées mais la séquence continue ; un WAV ne peut pas couper « ses notes ». | Pas de note séparable dans un rendu audio. |
| P6 | Branches : `chantier/bgm-demarrage-binaire` dans le parent et dans l'analyseur, depuis la base de P1. Aucune dans le moteur, qui n'est pas touché. | Tâche indépendante d'E13.d. |
| P7 | **Départ de warp fidèle à B17** : le départ ne charge plus rien ; il compare l'index de la carte visée (0 → index courant) à `CurrentMapSoundIndex` ; différent et son de warp nul (id 0, ou fiche `seq_num == -1` et `max_voices == 0`) → fondu armé directement (drapeau de reset intact) ; différent et son non nul → `LoadBgm(0)` ; puis le son comme aujourd'hui. La piste de destination part à l'arrivée (P3). Variante minimale : retirer seulement l'appel `PlayMapMusic` du départ (ni fondu ni arrêt au départ, déviation déclarée). La partie « bruitages » du départ (`ResetSoundEffectRuntime`, choix du son) reste celle du chantier transitions. | Le port actuel lance la piste de destination pendant le fondu de départ ; l'original l'éteint (fondu ou arrêt) et la lance à l'arrivée. |
| P8 | **`0xA7 n>0` efface `ResetSoundFlag`** : le port charge instantanément ; dans le chemin actif (streaming, B13), un `StopAllSound` en attente ne peut jouer que l'ancienne séquence, arrêtée aussitôt. **Déviation déclarée** : dans ce cas rare (entrée de carte et `0xA7` dans la même frame), les effets de bord de ce `StopAllSound` (désarmement du fondu, maître restauré) ne sont pas reproduits ; pas de latence de chargement ; `0xA8` reste à 0. | Le résultat audible (silence) est celui de l'original ; le reste demanderait de porter la machine de streaming. |

## Règles d'exécution pour l'agent

- **Branches dédiées** (P6) dans ce worktree. Jamais de commit sur `main`/`master`. **Jamais de push.** À la fin, la
  branche de l'analyseur est rapatriée par `git fetch` dans le dépôt du checkout principal (l'auteur lance lui-même les
  fetch de sous-module si la garde d'isolation le refuse, mémoire du dépôt).
- **Sous-modules du worktree** : `git clone --no-checkout` depuis le checkout local de l'auteur, puis `checkout` du
  commit enregistré, sous-modules imbriqués compris (MGUI, NvgSharp), précédent `e13d-sub-inventory`. Vérifier
  `git -C <sous-module> rev-parse --show-toplevel` avant tout travail. Le moteur ne sert qu'à builder.
- **Une seule tâche à la fois** : `⏳` → `🚧`. En fin de tâche : validation, puis `✅`, `🧪` ou `⚠️`, note de validation
  sous la tâche, **commit dédié** qui inclut ce plan. Message en anglais `type(area): summary`. `git add` fichier par
  fichier.
- **Jamais de commit avec `cd /d/...` devant** (le hook anti-`main` refuse) : `git -C <chemin>`.
- **Tests au premier plan**, `--blame-hang-timeout 60s`, timeout Bash 600 000. **Tests d'abord**, valeurs attendues
  écrites à la main dans ce plan avant le code. **Mutations faites en vrai** par un script du scratchpad qui remplace un
  extrait, builde, lance le filtre et rend le fichier à l'octet.
- **Commandes** : parent `dotnet build alundra-casaengine-project-converter.slnx`, `dotnet test
  Alundra.Tests/Alundra.Tests.csproj --blame-hang-timeout 60s` ; analyseur `dotnet build
  alundra-datas-analyser/AlundraTools/AlundraEngine/AlundraEngine.csproj` et `dotnet build
  alundra-datas-analyser/AlundraTools/AlundraDataExtractor/AlundraDataExtractor.csproj`, sans erreur ; plus
  `dotnet build alundra-datas-analyser/AlundraTools/Alundra.sln`, dont l'échec préexistant (ensemble d'erreurs relevé
  en T0.1, 4 × NU1605 attendues) doit rester identique.
- **Pas d'export** : ni l'extracteur, ni le convertisseur, ni les données ne changent. Un export n'est lancé que si une
  tâche le demande, et aucune ne le demande.
- **Vérification indépendante** : un `verifier` frais sur l'intégration DLL (T2.3), verdict traité par priorité avant
  la recette.
- Scripts contenant des `\` : écrits avec l'outil Write dans le scratchpad, jamais en heredoc (mémoire du dépôt).

## Arrêts

Un golden qui bouge. Un fichier du moteur, du convertisseur ou de `data-extracted/` modifié. `AlundraLogicClock`
modifié. `CasaEngine.Launcher/Program.cs` indexé. Un fait de ce plan contredit par une relecture du binaire. Un
comportement dont la nature (défaut ou choix) est douteuse et non tranchée par P2. Un test existant qui tombe pour une
raison autre que l'ancienne lecture listée plus haut. Base de P1 non conforme en T0.1 (plan audio non mergé alors que
la proposition par défaut est retenue), ou, dans la variante, aucun commit d'analyseur ne porte la T2.1 audio au moment
de T1.1. `Alundra.sln` qui échoue avec d'autres erreurs que celles relevées en T0.1. Un appelant de `PlayMapMusic`, `PlayFromRawIndex`, `RestartIfActive`,
`StopAllSound` ou `LoadBgm` trouvé hors de la liste de T2.1 (`rg` en début de T2.1). **Sur arrêt : ⚠️ Blocked, question dans « Points ouverts »,
fin.**

## Légende des statuts

- ⏳ Todo : pas encore commencé.
- 🚧 In progress : en cours de modification locale.
- 🧪 Needs testing : code écrit, validation incomplète ou en attente.
- ✅ Done : code validé, build/tests OK, commit effectué.
- ⚠️ Blocked : bloqué par une erreur non résolue ou une décision manquante.

## Validation globale

- Builds : parent sans erreur ; `AlundraEngine` et `AlundraDataExtractor` sans erreur ; `Alundra.sln` avec exactement
  les erreurs de T0.1.
- `Alundra.Tests` : ligne de base de T0.1, moins les tests réécrits, plus les nouveaux, **tous verts** ; six goldens
  identiques à l'octet.
- Tests du convertisseur et du moteur : non lancés (non touchés) ; leurs fichiers sont hors du diff (`git diff --stat`).
- Verdict **CONFIRMED** du `verifier` (T2.3).
- Recette de l'auteur (T3.2).

---

## Phase 0 — Préparation

### ✅ T0.1 — Environnement et ligne de base

> Bloquée le 2026-09-25 (plan audio non mergé), débloquée le même jour : l'auteur a mergé le plan audio (O3 levé).

- **Validation (2026-09-25)** :
  - `git merge-base --is-ancestor chantier/audio-mix-exact main` : vrai. Base parent `main` `063594b`, branche
    `chantier/bgm-demarrage-binaire`. Analyseur enregistré `4c0542b`, branche `chantier/bgm-demarrage-binaire` ;
    son sous-module `MGUI` à `7a801e88`.
  - **Écart constaté dans `main`, non corrigé** : le pointeur du moteur y vaut `43688074` (commit `fbe8cf5 update
    submodule`, qui l'a fait reculer depuis `716c02c7`, celui du plan audio en `6e151ba`). Or la DLL de `main`
    appelle `PlayClipStereo` (`AlundraSoundPlayer.cs:254`), absent de `43688074`. Le moteur du worktree est donc
    extrait à **`716c02c7`** pour builder (descendant de `43688074`, ancêtre du `main` du moteur `b02d3e86`), MGUI
    `c5d5a09`, NvgSharp `9c0da03` ; le pointeur enregistré n'est **jamais indexé** par ce plan (le `M
    CasaEngineMonogame` du `git status` est attendu). À signaler à l'auteur.
  - Contrôle de la base de l'analyseur : `rg IsBgmActivated AlundraTools/AlundraEngine` vide ; les deux
    `else PlaySeq(...)` avec renvoi à S1 présents (`SoundManager.cs:566`, `:5566`).
  - Builds : parent 0 erreur ; `AlundraEngine` et `AlundraDataExtractor` 0 erreur ; `Alundra.sln` **4 erreurs
    NU1605** (MonoGame 3.8.4.1 contre 3.8.5.1 via MGUI, `AlundraGame` et `AlundraTools`), la ligne de base.
  - `Alundra.Tests` : **1264 / 1264**.
  - Citations relues sur la base d'exécution : contenus inchangés, numéros déplacés par le plan audio. Analyseur
    `SoundManager.cs` : `LoadMapSequenceCore` `:512` (était `:523`), son `else PlaySeq` `:566` (était `:572-575`),
    cas 5 `:5553`, son `else PlaySeq` `:5566` (était `:5580-5583`), `HandleMapSoundStreamingCore` `:5427`,
    `HandleMapSoundEffectsCore` `:5340` et son test `:5344-5345` (était `:5363-5364`), `StopAllSoundCore` `:3717`,
    `PlaySeq` du bruitage `:4511` (était `:4530`) ; `SoundBin.cs` `SfxRecord` `:2018-2046` (était `:1996-2025`).
    DLL : `AlundraSoundBank.cs` `MaxVoices` `:65`, `:235`, `:277`, toujours sans `seq_num` ; tests du runner
    `0xA5`-`0xA7` `AlundraEventProgramRunnerTests.cs:2706-2818`. `AlundraMusicPlayer.cs`,
    `AlundraBgmFadeDirector.cs`, `AlundraWorldProxy.cs`, `AlundraWarpDirector.cs`, `AlundraMusicIndexTable.cs` :
    inchangés depuis `b2cdca7`.

- Objectif : branches, sous-modules du worktree, comptes de tests, avant toute modification.
- Fichiers : ce plan.
- Étapes :
  1. Base selon P1. **Proposition par défaut** : `main` contient le merge du plan audio (`git log main` montre ses
     commits) ; sinon arrêt. `git switch -c chantier/bgm-demarrage-binaire` depuis cette base (variante : depuis
     `b2cdca7`).
  2. Clones locaux des sous-modules au commit enregistré, imbriqués compris ; branche `chantier/bgm-demarrage-binaire`
     dans l'analyseur depuis le commit enregistré. Contrôle de la base de l'analyseur : `rg -n "IsBgmActivated"
     AlundraTools/AlundraEngine` ne montre rien, et les deux `else PlaySeq(...)` de `LoadMapSequenceCore` et du cas 5
     sont présents avec leur renvoi à S1 ; sinon arrêt. (Variante : ce contrôle se fait au début de T1.1, sur le
     commit de `chantier/audio-mix-exact` récupéré.)
  3. Relire chaque citation `fichier:ligne` de la DLL et de l'analyseur faite dans ce plan sur la nouvelle base ; un
     numéro qui glisse se corrige dans le plan, un **contenu** qui a changé est un arrêt.
  4. Builds du parent et de l'analyseur (commandes des règles d'exécution) : noter l'ensemble exact des erreurs de
     `Alundra.sln` ; `Alundra.Tests` : noter le compte et les échecs préexistants éventuels.
- Validation : comptes, erreurs de `Alundra.sln` et SHA de base du parent et de l'analyseur écrits sous la tâche.
- Commit : `docs(plan): plan the executable-faithful BGM start, stop and restart`

---

## Phase 1 — Analyseur (sous-module `alundra-datas-analyser`)

### ✅ T1.1 — Les deux `PlaySeq` et le `SetSeqVolume` absents du binaire, le test du son de warp

- Objectif : `LoadMapSequenceCore` et le cas 5 du streaming comme B7 et B9 ; le test « son nul » de
  `HandleMapSoundEffectsCore` comme B17.
- Fichiers : `AlundraTools/AlundraEngine/Sound/SoundManager.cs`.
- Étapes :
  1. `LoadMapSequenceCore` : retirer `SetSeqVolume` et la branche `PlaySeq` ; le stop-all (`0 < soundIndex &&
     stopAllSound != 0`) s'exécute même après un échec de `LoadSeq` ; l'index courant reçoit l'index brut **avant**
     l'appel à `StopAllSound`, comme `0x80049cf4` ; `g_resetSoundFlag = 1` inchangé. Commentaire `// 80049be0` avec
     les adresses de B7.
  2. Cas 5 : retirer la branche `PlaySeq` ; commentaire avec `0x8004b580`-`0x8004b5d8`.
  3. Les branches retirées aux étapes 1 et 2 sont les deux `else PlaySeq(...)` laissés par la T2.1 audio, avec leur
     renvoi à S1 (base de P1).
  4. `HandleMapSoundEffectsCore` (`:5344-5345` à la base d'exécution) : le son vaut 0 si la fiche a `SeqNum == -1` et `MaxVoices == 0`
     (`SoundBin.SfxRecords[id]`, champs déjà lus par `SfxRecord`, `SoundBin.cs:2018-2046`), au lieu des octets 0 et 6.
     Commentaire avec `0x80049f58`-`0x80049f78`.
- Validation : `AlundraEngine` et `AlundraDataExtractor` buildent sans erreur ; `Alundra.sln` échoue avec exactement
  les erreurs de T0.1 ; `rg -n "PlaySeq\(" SoundManager.cs` ne montre plus que `StopAllSoundCore`, la séquence de
  bruitage (`:4511`) et `PlayLoaderBgm` (`:180`, lecteur du `LOADER.EXE`, propre à l'analyseur).
  L'analyseur n'a pas de tests ; pas de recette dans `AlundraGame` tant que O5 du plan audio n'est pas tranché.
- Commit (analyseur) : `fix(sound): load map sequences without playing them and test warp sounds as the executable does`
- **Validation (2026-09-25)** : analyseur `e495d7f` sur `chantier/bgm-demarrage-binaire` (depuis `4c0542b`).
  `AlundraEngine` et `AlundraDataExtractor` : 0 erreur ; `Alundra.sln` : les 4 erreurs NU1605 de T0.1, à l'identique
  (`diff` des messages). `rg -n "PlaySeq\(" SoundManager.cs` : `:180` (`PlayLoaderBgm`), `:3725` (`StopAllSoundCore`),
  `:4503` (séquence de bruitage), plus la définition `:3556`. Le test du son de warp lit `SfxRecords[id].SeqNum` et
  `.MaxVoices`.

### ✅ T1.2 — Pointeur de l'analyseur

- Validation : parent buildé ; `Alundra.Tests` au compte de T0.1.
- Commit : `chore(submodules): point at the analyser whose sequence loads no longer play`

---

## Phase 2 — DLL (`Alundra`)

### ✅ T2.1 — Le modèle de séquence de l'exécutable

- Objectif : D1 côté DLL, selon P2, P3, P4, P5, P7, P8.
- Fichiers : `Alundra/Scripts/AlundraMusicPlayer.cs`, `AlundraBgmFadeDirector.cs`, `AlundraWorldProxy.cs` (fermeture
  de frame seulement), `AlundraWarpDirector.cs` (musique du départ, P7), `AlundraSoundBank.cs` (lecture additive de
  `seq_num`), `AlundraMusicIndexTable.cs` (doc de `Play` pour `-1` : « charger », plus « jouer ») ;
  `Alundra.Tests/AlundraMusicPlayerTests.cs`, `AlundraBgmFadeDirectorTests.cs`, `AlundraEventProgramRunnerTests.cs`
  (faux lecteur), `AlundraMusicIndexTableTests.cs` (nom du test `-1` si sa doc change), tests du départ de warp
  (fichier existant `AlundraWarpDepartureTests.cs`).
- Préalable : `rg -n "PlayMapMusic|PlayFromRawIndex|RestartIfActive|\.StopAllSound\(|\.LoadBgm\(" Alundra` ne montre
  que les sites de ce plan (`AlundraWorldProxy.cs:1474`, `AlundraWarpDirector.cs:379` et `:458`, le runner, les deux
  classes) ; sinon arrêt.
- Contrat (état de session, dans le lecteur de musique) :
  - `CurrentMapSoundIndex` : port brut de `g_currentMapSoundIndex`, 0 au départ, **jamais remappé**.
  - séquence chargée : un index de piste, ou aucune (fermée) ; elle joue si et seulement si la voix est vivante.
  - `ResetSoundFlag` : port de `g_resetSoundFlag`.
  - `StopSequence()` (`InitializeBgm`, B5) : arrête la voix ; la séquence reste chargée, rembobinée.
  - `PlaySequence()` (`PlaySeq`, B4) : voix vivante → rien ; séquence chargée sans voix → voix du début ; aucune
    séquence → rien.
  - `PlayMapMusic(mapId)` (B10, B7) : index 0 ou égal à `CurrentMapSoundIndex` → rien. Sinon : arrêt et fermeture de
    l'ancienne ; si l'index ≠ 45 : chargement de la piste (`-1` → 1), `CurrentMapSoundIndex` ← index brut,
    `ResetSoundFlag` posé. Si 45 : rien d'autre (`CurrentMapSoundIndex` inchangé). **Aucune voix ne démarre ici.**
  - `PlayFromRawIndex(n)` (`0xA7`, B13, chemin streaming ramené à un chargement instantané) : 0 →
    `CurrentMapSoundIndex` ← 0, arrêt, fermeture, drapeau inchangé. > 0 → arrêt et fermeture de l'ancienne,
    chargement de `n`, `CurrentMapSoundIndex` ← n, **aucune voix**, `ClearResetSoundFlag()` (P8). (Le runner garde
    l'appel à `StopAllSound` quand `v[2] ≠ 0`, le `g_forceStopAllSound` du cas 5.)
  - Départ de warp (B17, P7), appelé par les deux chemins de départ à la place de `PlayMapMusic` : index visé ← index
    brut de la carte visée dans la table, ou `CurrentMapSoundIndex` s'il vaut 0 (ou si la table n'a pas la carte) ;
    différent de `CurrentMapSoundIndex` → son de warp nul (id 0, fiche absente, ou `seq_num == -1` et
    `max_voices == 0`) : fondu armé à `0x78` **sans** toucher au drapeau ; son non nul : `LoadBgm(0)`. Égal → rien
    pour la musique. Le `PlaySfx` du départ reste où il est.
  - `AlundraBgmFadeDirector.StopAllSound()` (B2) : `CurrentMapSoundIndex < 0` → **rien du tout** ; sinon fondu armé →
    `StopAllSfx` et désarmement ; maître à pleine échelle ; `PlaySequence()`.
  - `LoadBgm(v)` (B14) : `ClearResetSoundFlag()` ; v ≠ 0 → armement ; v = 0 → `StopSequence()`.
  - Bas de rampe (B15) : maître 0, `StopSequence()`, `StopAllSfx`.
  - Fermeture de frame (B9, P3) : juste avant `SoundPlayer?.FlushFrameSounds()`, si `ResetSoundFlag` →
    `StopAllSound()` puis `ClearResetSoundFlag()`. Pas d'allocation.
- Étapes :
  1. **Tests d'abord**, valeurs attendues :
     - a. entrée 389 (index 25) : après l'installation, aucune voix ; après la première fermeture de frame, **une**
       voix, piste 25, bouclée, bus `Music` (T1 réécrit) ;
     - b. 389 puis 390 (25) : aucune nouvelle voix, la voix de 25 est la même et vivante (T1 bis réécrit) ;
     - c. 389 puis 183 (`-1`) : la voix de 25 est arrêtée ; après la fermeture de frame, aucune voix (P2) ; puis 184
       (`-1`) : rien ; puis 226 (16) : une voix, piste 16 ;
     - d. `0xA7 5,0` sur une carte qui joue : aucune voix après l'opcode ni après la fermeture de frame ; puis `0xA5` :
       une voix, piste 5 ;
     - e. `0xA7 5,1` : une voix, piste 5, dès l'opcode ;
     - f. `0xA7 0,0` puis `0xA5` : aucune voix ;
     - g. `0xA5` pendant qu'une piste joue : **même** poignée de voix, toujours vivante ;
     - h. `0xA6 0` : voix arrêtée ; puis `0xA5` : une nouvelle voix, même piste ;
     - i. `0xA6 1` : au tic du bas de rampe, la voix est arrêtée ; au tic de restauration, maître à 1 et toujours
       aucune voix ; puis `0xA5` : une voix ;
     - j. entrée de carte puis `0xA6 1` dans la même frame : après la fermeture de frame, aucune voix, fondu armé ;
     - k. sur une carte `-1`, `0xA6 1` puis `0xA5` : le fondu reste armé, maître inchangé par `0xA5` ;
     - l. entrée sur une carte d'index différent pendant un fondu armé : après la fermeture de frame, fondu désarmé,
       maître à 1, une voix ;
     - m. entrée sur 389 puis `0xA7 5,0` dans la même frame : après la fermeture de frame, aucune voix (P8) ; puis
       `0xA5` : une voix, piste 5 ;
     - n. 389 qui joue, départ par portail vers une carte d'index différent avec le comportement de warp 1 (son 69,
       nul) : au départ et après la fermeture de frame du monde quitté, **aucune voix de la piste de destination**,
       fondu armé ; installation de la destination puis sa première fermeture de frame : fondu désarmé, maître à 1,
       une voix de la piste de destination (valeurs relevées en T0.1 dans `music-index.json`, carte choisie par
       l'exécuteur et écrite sous la tâche) ;
     - o. même départ avec un son de warp non nul (comportement 3, son 55, `max_voices` 4) : au départ, voix de
       musique arrêtée, fondu non armé ; à l'arrivée, une voix de la destination ;
     - p. départ vers une carte de même index (389 → 390) : la voix de 25 est la même et vivante avant et après.
     Les tests existants listés dans « État vérifié » sont réécrits sur ces valeurs, un par un ; aucun n'est supprimé
     sans remplaçant.
  2. Implémentation.
  3. Mutations en vrai, chacune doit faire tomber au moins un test : `PlaySequence` qui relance une voix vivante (g) ;
     `StopSequence` qui relance (h, i) ; index remappé stocké (c) ; garde `< 0` retirée de `StopAllSound` (c, k) ;
     drapeau jamais consommé (a) ; drapeau consommé après le vidage au lieu d'avant (à vérifier exécutable ; sinon
     consigné comme non discriminé) ; `LoadBgm` qui n'efface pas le drapeau (j) ; `PlayFromRawIndex` qui démarre une
     voix (d) ; `PlayFromRawIndex` qui n'efface pas le drapeau (m) ; `PlayMapMusic` gardé au départ de warp (n) ;
     fondu armé par `LoadBgm(1)` au lieu d'une écriture directe, qui efface le drapeau (à vérifier exécutable, sinon
     consigné) ; test « son nul » sur le nombre de tonalités au lieu de `seq_num`/`max_voices` (à vérifier
     exécutable avec un son à séquence, 74 ou 76, sinon consigné).
- Validation : parent buildé ; `Alundra.Tests` vert ; six goldens identiques.
- Commit : `fix(audio): start, stop and restart background music as the executable does`
- **Validation (2026-09-25)** : parent 0 erreur, 0 avertissement ; `Alundra.Tests` **1281 / 1281** (1264 + 17),
  treize passages verts consécutifs au total, goldens verts (fichiers dorés hors du diff). Exécution par workflow :
  un exécuteur (tests d'abord), trois relecteurs frais en lecture seule (fidélité, tests, régressions), corrections,
  puis deux relecteurs frais (fidélité et ordre, tests et isolation) : aucun constat P0-P2 au second tour.
  - Tests par résultat attendu : a et b `AlundraMusicPlayerTests.T1_…`, `T1bis_…` ; c `MapEntry_NegativeIndexMaps_AreSilent_ThenARealIndexPlays` ;
    d à i et k `AlundraBgmFadeDirectorTests` (`PlayMusic_0xA7_…`, `StopAllSound_TrackAlreadyPlaying_NeverRestartsIt`,
    `LoadBgm_…`, `OnANegativeIndexMap_…`) ; j, l, m `AlundraMusicPlayerTests.MapEntry_…` ; n, o, p et le cas 74
    `AlundraWarpDepartureTests.WarpDeparture_…`. Dix tests existants réécrits sur les nouvelles valeurs et le faux
    lecteur du runner adapté ; aucun test supprimé.
  - **Décisions prises pendant la tâche, depuis le binaire** : (1) la moitié musique du départ de warp est
    **différée** à la fermeture de frame, **après** la consommation du drapeau : dans l'exécutable,
    `HandleMapSoundEffects` (`0x8002c46c`) ne s'exécute qu'après la boucle de frame, dont la fonction de frame
    consomme `g_resetSoundFlag` (`0x8002bd04`) ; le son de warp reste joué au moment du départ, comme avant.
    (2) `0xA7` d'index 0 n'appelle plus `StopAllSound` même avec `v[2] ≠ 0` : la branche 0 de `FUN_8004b114` ne
    lit pas le drapeau (`0x8004b130`-`0x8004b168`) ; seul changement de logique du runner. (3) Sans table de
    musique attachée (chemin dégradé), le départ n'enregistre rien.
  - Isolation : `AlundraWarpArrivalTests` et `AlundraHudDebugRecipeTests` rejoignent la collection des singletons
    musicaux ; elles, et `AlundraWorldProxyAudioInstallationTests`, remettent à zéro le fondu et le lecteur ;
    `AlundraMusicPlayer.ResetForTests` vide aussi sa table.
  - **Mutations en vrai** (`mutate.py` et `t21_mutations.py` du scratchpad, fichiers rendus à l'octet) : 15 tuées
    sur 15 attendues — `PlaySequence` qui relance, `StopSequence` qui relance, index remappé stocké, garde `< 0`
    retirée, drapeau jamais consommé, `LoadBgm` qui garde le drapeau, `PlayFromRawIndex` qui joue, qui garde le
    drapeau, départ qui joue la destination, son de warp muet sans `seq_num`, départ évalué avant le drapeau,
    `0xA7 0,1` qui appelle `StopAllSound`, garde « même index » retirée, bas de rampe qui relance, départ qui agit
    à index égal. **Deux équivalentes, consignées** : drapeau consommé après le vidage de l'anti-doublon (les deux
    touchent des états disjoints) ; fondu du départ armé par `LoadBgm(1)` au lieu de l'écriture directe (le drapeau
    est toujours déjà consommé quand le départ est évalué).
  - Différés, avec raison, dans « Suites consignées » : S1 et S2.

### ✅ T2.2 — Le schéma canonique du corpus, au site de production

- Objectif : prouver le bloc `A6(1)` / `A7(42,0)` / `A5` / `A7(0,0)` par le vrai runner, le vrai lecteur et le faux
  backend du moteur (précédent : `ProductionSite_SyntheticProgram…` d'E11.b).
- Fichiers : `Alundra.Tests/` (nouveau test, ou extension du test de production existant).
- Étapes : précondition, une piste joue (entrée de carte et fermeture de frame, ou `A7(n,1)`) ; programme
  synthétique `A6(1)`, `37(140)`, `A7(42,0)`, `37(30)`, `A8`, `03(254,255)`, `A5`, `37(10)`, `A7(0,0)` ; à chaque
  étape, voix attendue : piste courante jusqu'au bas de rampe, aucune ensuite (y compris après la
  restauration du maître), aucune après `A7(42,0)`, piste 42 après `A5`, aucune après `A7(0,0)`. Mutation en vrai :
  `PlayFromRawIndex` qui démarre une voix → le test tombe.
- Validation : `Alundra.Tests` vert, goldens identiques.
- Commit : `test(audio): pin the corpus fade, silent load and 0xA5 start sequence through the real runner`
- **Validation (2026-09-25)** : test
  `AlundraBgmFadeDirectorTests.ProductionSite_CorpusFadeSilentLoadThenA5Start_TheMostFrequentBlock_DrivenThroughTheRealRunner`,
  vrai runner, vrais singletons, faux backend du moteur, un tic de fondu par appel de script comme en production.
  Relevés : voix initiale vivante jusqu'au tic 60 (bas de rampe), puis morte, y compris après la restauration du
  maître (tic 117) et à la fin de l'attente de 140 ; aucune voix après `A7 42,0` ni après l'attente de 30 ; après
  `A5` une voix, piste 42, bouclée, bus `Music`, deux lectures au total ; après `A7 0,0`, aucune voix, toujours deux
  lectures. `Alundra.Tests` **1282 / 1282** sur trois passages. Mutations en vrai, filtrées sur ce test :
  `PlayFromRawIndex` qui joue → tombe ; bas de rampe qui relance → tombe (`PlaySequence` qui relance une voix
  vivante ne concerne pas ce scénario et reste tuée par le test g de T2.1).

### ✅ T2.3 — Vérification indépendante

- Objectif : un `verifier` frais sur T2.1 et T2.2 : la revendication est le tableau « Réponse courte », colonne
  « Original », pour la DLL ; il reçoit ce plan, le diff et les commandes.
- Validation : verdict **CONFIRMED** consigné ; tout autre verdict traité par priorité avant T3.
- **Validation (2026-09-25)** : deux regards frais en parallèle sur `6012bb0` et `417d766`. **Vérificateur :
  CONFIRMED**, les huit lignes tiennent, chacune par un test qui passe par le site de production et qui tomberait
  sous le mauvais comportement ; build 0 erreur, `Alundra.Tests` 1282 / 1282 sur deux passages, goldens identiques
  à l'octet (réécrits par les tests de trace, `git status` propre), diff limité à `Alundra/`, `Alundra.Tests/` et
  ce plan ; il a relu dans `ALUN_CD.EXE` l'ordre de la boucle principale (fonction de frame, sortie de boucle en
  `0x8002c45c`, `HandleMapSoundEffects` en `0x8002c46c`, `StartWarpTransition` en `0x8002c478`, désormais dans
  `bgm_disasm_4.txt`). **Critique de complétude : CONFIRMED** : un seul `PlayClip` sur le bus `Music`, atteint par
  `PlaySequence` seul ; toutes les autres voies passent par les coutures modélisées. Constats : P3 et P4 sans
  retouche du candidat confirmé (règle : pas de boucle de correction pour un P3/P4), consignés en S3 et S4 ; les
  autres P4 (son 379, déjà S1 ; deux départs dans une frame, impossible par le chemin de production) sans suite.

---

## Phase 3 — Documentation et recette

### ✅ T3.1 — Doc, ADR, plans corrigés

- Fichiers : `docs/decisions/` (ADR « BGM playback follows the executable's sequence state », par le skill `adr`) ;
  `docs/plan-e11c-musique.md` (§1.1 ligne `-1`, §1.3 origine du plein volume, §1.4 le passage par le drapeau est le
  seul départ : notes datées, texte d'origine gardé) ; `docs/plan-e11b-opcodes-audio.md` (faits 1-3, B2 : `InitializeBgm`
  est un arrêt) ; `docs/plan-transitions-carte.md` (D-T-8 : le départ ne charge plus la musique) ;
  `docs/plan-conversion-totale.md` (ligne E11) ; ce plan (bilan).
- Validation : relecture ; index des ADR à jour ; aucun caractère de contrôle dans les fichiers écrits.
- Commit : `docs(audio): record the executable's BGM sequence model and correct the E11 plans`
- **Validation (2026-09-25)** : ADR-0004 « Background music follows the executable's sequence state » (Accepted),
  index de `docs/decisions/` à jour. Notes datées, texte d'origine gardé : `plan-e11c-musique.md` (§1.1 ligne `−1` :
  cartes muettes ; §1.3 : pas de `SetSeqVolume` dans `LoadMapSequence`, conclusion tenue ; §1.4 : le drapeau est le
  seul départ), `plan-e11b-opcodes-audio.md` (faits 1 à 3 ; `InitializeBgm` est un arrêt ; S1 fermée),
  `plan-transitions-carte.md` (D-T-8 : le départ ne charge jamais la musique), `plan-conversion-totale.md`
  (paragraphe et ligne E11). Aucun caractère de contrôle dans les fichiers écrits. Rédaction en session principale.

### ✅ T3.2 — Recette de l'auteur

- Lancement : Launcher du worktree sur `alundra-project/AlundraGame.json` du checkout principal (projet inchangé).
  **Corrigé à l'exécution (2026-09-25)** : tel qu'écrit, ce lancement aurait chargé l'**ancienne** DLL. Le jeu charge
  `Alundra.dll` depuis le dossier du projet (`GameplayDllName`), le build copie la nouvelle dans
  `alundra-project/` **du worktree** (`Alundra.csproj`, cible `CopyGameplayDllToProject`), et celle du checkout
  principal (SHA-1 `314d8a66…`) est celle de `main`. Préparé : copie du projet exporté du checkout principal dans
  `alundra-project/` du worktree (`robocopy /E`, sans `/MIR`, sans `Alundra.dll`/`.pdb` ni `UI/Screens` suivis ;
  23 235 fichiers, 0 échec ; `diff -rq` : identique hors DLL et fins de ligne des écrans), la DLL du worktree
  (SHA-1 `50a22c1e…`, celle du dernier build) en place. Rien n'est écrit dans le checkout principal. Lancement :

  ```
  D:\development\repo\alundra-casaengine-project-converter\.claude\worktrees\silly-mayer-47d565\CasaEngineMonogame\CasaEngine.Launcher\bin\Debug\net9.0-windows\CasaEngine.Launcher.exe "D:\development\repo\alundra-casaengine-project-converter\.claude\worktrees\silly-mayer-47d565\alundra-project\AlundraGame.json"
  ```

  Le son n'est pas coupé (`IsAudioMuted` absent). Pour démarrer sur une autre carte, changer `FirstWorldLoaded`
  dans `AlundraGame.json` **de la copie du worktree** : 289 `Maps\Inoa\Inoa (night)-289\Inoa (night)-289.world`,
  393 `Maps\Water Mill\Water Mill-393\Water Mill-393.world`, 416 `Maps\Coast\Coast beginning-416\Coast beginning-416.world`,
  123 `Maps\Kline's Nightmare\Kline's Nightmare-123\Kline's Nightmare-123.world`,
  25 `Maps\Lars' Crypt\Lars' Crypt-25\Lars' Crypt-25.world`, 226 `Maps\Inoa\Inoa-226\Inoa-226.world`,
  138 `Maps\Cave\Cave-138\Cave-138.world` (les séquences de corpus dépendent des drapeaux de l'histoire : elles ne
  se déclenchent pas forcément en démarrant directement sur la carte).
- À écouter :
  - le bateau (389, 390) : la musique démarre à l'entrée, ne redémarre pas entre les deux cartes ;
  - une carte `-1` (Inoa de nuit 289, moulin 393 ou côte du début 416) : silence (P2), à comparer au jeu réel si
    possible ;
  - une séquence de corpus (O1) : la musique s'éteint au fondu et **ne revient pas** ; la nouvelle piste part au
    `0xA5`, pas au `0xA7`, et ne repart pas du début ;
  - un warp entre deux cartes de musiques différentes : l'ancienne piste baisse (fondu) ou s'arrête pendant le départ,
    la nouvelle part à l'arrivée, plus pendant le fondu de départ.
  - `AlundraGame` (analyseur) : indisponible tant que `Alundra.sln` ne builde pas (O5 du plan audio) ; si l'auteur l'a
    réparé entre-temps, les mêmes cartes, facultativement.
- Commit : `docs(plan): close the executable-faithful BGM start, stop and restart`
- **Validation (2026-09-26)** : **validé en jeu par l'auteur** (« ok valider »), sur la recette ci-dessus.
- **État (2026-09-25)** : recette préparée. Branches : parent
  `chantier/bgm-demarrage-binaire` (dans le dépôt partagé), analyseur `chantier/bgm-demarrage-binaire` `e495d7f`
  rapatrié par `git fetch` dans le checkout principal. Rien n'est poussé ni mergé.

---

## Phase 4 — Suites S1 à S4, pointeur du moteur, merge

Demande de l'auteur du 2026-09-26 : « merge et fais les suites S1 à S4 » ; pointeur du moteur : « Oui, moteur
`b02d3e86` » (D9). Les remèdes sont ceux de « Suites consignées ». Ordre : S4, S2, S3, puis S1, qui s'appuie sur le
test de S3. Même branche `chantier/bgm-demarrage-binaire`.

### ✅ T4.1 — S4 : commentaire d'`InstallAudioSystems`

- Objectif : le commentaire en ligne d'`InstallAudioSystems` (`AlundraWorldProxy.cs:997-1004`) dit que la musique
  d'entrée est chargée ici et démarre à la première fermeture de frame (B9, B10), et qu'un `0xA6` de cette frame peut
  l'annuler.
- Fichiers : `Alundra/Scripts/AlundraWorldProxy.cs` (commentaire seul).
- Validation : build 0 erreur ; `Alundra.Tests` inchangé.
- Commit : `docs(audio): the map-entry comment says the music starts at the first frame close`
- **Validation (2026-09-26)** : build 0 erreur ; `Alundra.Tests` 1282/1282, aucun test ajouté ni modifié (commentaire seul).

### ✅ T4.2 — S2 : le test passe par le code de fermeture de frame de production

- Objectif : `SimulateFrameClose` (`AlundraMusicPlayerTests.cs:87`) ne recopie plus le bloc de production : le bloc
  « drapeau de reset » de `AlundraWorldProxy.Update` est extrait dans une méthode `internal` sans allocation,
  appelée par `Update` et par l'utilitaire de test. Ordre et comportement de `Update` inchangés.
- Fichiers : `Alundra/Scripts/AlundraWorldProxy.cs`, `Alundra.Tests/AlundraMusicPlayerTests.cs`.
- Validation : build ; `Alundra.Tests` vert, même compte ; goldens identiques ; mutation en vrai : la méthode extraite
  qui ne consomme plus le drapeau fait tomber au moins un test **qui passait par l'utilitaire**.
- Commit : `refactor(audio): tests close the frame through the production reset-flag code`
- **Validation (2026-09-26)** : build 0 erreur ; `Alundra.Tests` 1282/1282 (même compte) ; mutation en vrai (méthode
  extraite vidée) fait tomber 11 tests, dont `SimulateFrameClose`-driven et `Update`-driven ; aucun test ajouté,
  `SimulateFrameClose` modifié pour appeler `AlundraWorldProxy.ConsumeMusicResetSoundFlagOnFrameClose`.

### ✅ T4.3 — S3 : la moitié musique du départ par `0x53`, testée

- Objectif : un test au niveau du proxy qui fait partir un warp par `0x53` (`BeginDepartureFromChangeMapOpcode`,
  `AlundraWarpDirector.cs:387`) vers une carte d'index musical différent, avec un son muet puis avec un son audible,
  et qui vérifie après la fermeture de frame du monde quitté : fondu armé et voix vivante (muet), voix arrêtée et
  fondu non armé (audible) ; à l'arrivée, la piste de destination.
- Fichiers : `Alundra.Tests/` (fichier des tests de départ ou du runner).
- Validation : `Alundra.Tests` vert ; mutation en vrai : `PlayMapMusic` à la place de `HandleWarpDeparture` en
  `:387` seulement fait tomber ce test.
- Commit : `test(audio): pin the music half of the 0x53 warp departure`
- **Validation (2026-09-26)** : build 0 erreur ; `Alundra.Tests` 1284/1284 (1282 + 2 nouveaux) ; mutation en vrai
  (`PlayMapMusic` à la place de `HandleWarpDeparture` en `:387`) fait tomber exactement les 2 nouveaux tests ; tests
  ajoutés dans `Alundra.Tests/AlundraWarpDepartureTests.cs` :
  `Warp0x53Departure_SilentWarpSound_ArmsFadeOnly_NeverLoadsDestination_ThenArrivalPlaysIt` et
  `Warp0x53Departure_AudibleWarpSound_StopsTheDepartingBgm_NeverArmsTheFade_ThenArrivalPlaysDestination`.

### ✅ T4.4 — S1 : un son de warp muet n'est jamais joué

- Objectif : aux deux sites de départ, le son de warp n'est joué que s'il n'est pas muet au sens de B17 : l'original
  met le son à 0 (`0x80049f78`) et ne le joue dans aucune branche. Seul cas réel : 379 (`seq_num` -1, `max_voices`
  0, une tonalité).
- Fichiers : `Alundra/Scripts/AlundraWarpDirector.cs` ; test sur le chemin `0x53` de T4.3 avec le son 379 : aucune
  voix de 379 ; un son audible (55) joue toujours.
- Validation : `Alundra.Tests` vert ; mutation en vrai : `PlaySfx` sans la garde fait tomber le test.
- Commit : `fix(audio): a silent warp sound is never played, as the executable does`
- **Validation (2026-09-26)** : build 0 erreur ; `Alundra.Tests` 1285/1285 (1284 + 1 nouveau) ; mutation en vrai
  (retrait de la garde `if (!isSilent)` au site `0x53`) fait tomber le nouveau test ; test ajouté dans
  `Alundra.Tests/AlundraWarpDepartureTests.cs` :
  `Warp0x53Departure_SilentWarpSound379_NeverStartsItsVoice_AudibleWarpSoundStillDoes` (sons 379 et 55, manifeste
  réel) ; test existant modifié dans `Alundra.Tests/AlundraEventProgramRunnerTests.cs` :
  `ChangeMap_0x53_DecodesMapIdTileEffectAndSfx_ExactValues` (son 69 muet remplacé par 55 audible + chemin projet
  réel, sinon la garde neuve le fait tomber puisque 69 est muet dans le vrai manifeste).

### ✅ T4.5 — Pointeur du moteur sur `b02d3e86` (D9)

- Objectif : `main` compile une fois mergé. Le gitlink du moteur passe de `43688074` à `b02d3e86` (`main` du moteur,
  descendant de `716c02c7`), enregistré par `update-index --cacheinfo`, **sans `git add`** (le moteur du checkout
  principal est sur le chantier d'une autre session ; mémoire du dépôt).
- Étapes : moteur du worktree extrait à `b02d3e86`, MGUI et NvgSharp aux commits qu'il enregistre ; build du parent ;
  `Alundra.Tests` et tests du convertisseur ; puis le commit du pointeur.
- Validation : build 0 erreur ; `Alundra.Tests` et convertisseur verts ; `git ls-tree HEAD CasaEngineMonogame` =
  `b02d3e86`.
- Commit : `chore(submodules): point at the engine main that carries the stereo voices`
- **Validation (2026-09-26)** : moteur du worktree extrait à `b02d3e86` (MGUI `7a801e8`, NvgSharp `9c0da03`, les
  commits qu'il enregistre ; 17 commits après `716c02c7`, aucun dans `CasaEngine/Framework/Audio`). Build du parent
  0 erreur ; `Alundra.Tests` **1285 / 1285** ; tests du convertisseur **194 / 194** ; goldens identiques (contenu
  normalisé égal à l'index, fins de ligne rendues). Gitlink enregistré par `update-index --cacheinfo`, sans
  `git add`.
- **Contrôles de la session principale sur T4.1 à T4.4** : deux relecteurs frais, aucun constat P0-P2 ; mutations
  refaites en vrai (`t4_mutations.py`) : méthode extraite qui ne consomme plus le drapeau → 6 tests de
  `AlundraMusicPlayerTests` tombent ; site `0x53` qui charge la destination → les 2 tests `Warp0x53Departure_*`
  tombent ; son muet joué au site `0x53` → le test 379 tombe. **Les tests de trace réécrivent les fichiers
  dorés en LF** : `git status` les montre modifiés alors que leur contenu normalisé est égal à l'index (vérifié
  par `git hash-object --path`) ; ils ont été rendus par `git checkout`. Artefact préexistant du harnais, à
  connaître.

### ⏳ T4.6 — Vérification et merge

- Objectif : un vérificateur frais sur T4.1 à T4.5, puis les avances rapides : `master` de l'analyseur sur
  `e495d7f`, `main` du parent sur la tête de la branche. Aucun push.
- Validation : verdict CONFIRMED ; `git merge --ff-only` réussi des deux côtés ; `main` = tête de la branche.
- Commit : `docs(plan): record the follow-ups, the engine pointer and the merge` (avant les avances rapides).

---

## Points ouverts

| Réf | Sujet | Tâche concernée |
|---|---|---|
| O1 | Carte de recette atteignable pour la séquence de corpus : candidates `Inoa-226` (`A7(4,0)` @1119 puis `A5` @1131 ; `A6(0)` + `A7(10,1)` @417), `Cave-138` (`A6(1)` @620, `A7(36,0)` @624 ; `A7(12,1)` @773), `Kline's Nightmare-123`, `Lars' Crypt-25`. L'auteur choisit. | T3.2 |
| O2 | P1, P2, P3, P7 tranchés (D5 à D8, 2026-09-25) ; P4, P5, P6, P8 appliqués tels quels par l'approbation du 2026-09-25. | T0.1, T2.1 |
| O3 | ~~Le plan audio n'est pas mergé dans `main`.~~ Levé le 2026-09-25 : mergé (`063594b`). | T0.1 |
| O4 | Le pointeur du moteur dans `main` (`43688074`, `fbe8cf5`) est antérieur à l'API stéréo que la DLL de `main` appelle : `main` ne builde pas tel qu'enregistré. Ce plan builde contre `716c02c7` sans toucher au pointeur ; à corriger par l'auteur dans `main`. | toutes |

## Suites consignées (hors de cette tâche)

- **S1** (fait : T4.4, `253e36d`) — Un son de warp « muet » au sens de B17 mais jouable (seul cas du manifeste : 379, une tonalité) est encore
  joué par le port au départ, via `0x53` ; l'original met le son à 0 (`0x80049f78`) et ne le joue pas. Moitié
  « bruitages » du départ, hors périmètre ; le port le jouait déjà avant ce chantier. Remède : ne pas appeler
  `PlaySfx` quand `IsWarpSoundSilent` est vrai.
- **S2** (fait : T4.2, `62faa3e`) — `SimulateFrameClose` (`AlundraMusicPlayerTests`) recopie le bloc de fermeture de frame pour trois tests
  qui pilotent les singletons ; le site réel reste couvert par les tests qui passent par `AlundraWorldProxy.Update`.
- **S3** (fait : T4.3, `3f8dc59`) — Aucun test ne couvre la moitié musique du départ par `0x53` (`AlundraWarpDirector.cs:387`) ; son code est
  identique à celui du portail (`:466`), couvert par les tests n, o, p et 74. Remède : un test de départ `0x53` au
  niveau du proxy, son muet puis son audible, et la mutation « `PlayMapMusic` en `:387` » qui doit le faire tomber.
- **S4** (fait : T4.1, `5a59de7`) — Le commentaire en ligne d'`InstallAudioSystems` (`AlundraWorldProxy.cs:997-1004`) dit encore que la
  musique d'entrée démarre à cet endroit ; elle démarre à la fermeture de frame (B9, B10). La doc XML de
  `TriggerMapEntryMusic` est juste. Documentation seule.
- **S5** — La garde « son muet non joué » du site portail (`AlundraWarpDirector.cs:478`) n'a pas de test : avec les
  données réelles, les sons muets des portails (69, 75) n'ont aucune tonalité, rien n'est observable ; seul un faux
  manifeste le permettrait. Aussi : un son absent du manifeste compte désormais comme muet et n'est plus joué
  (mode dégradé seulement ; le lecteur de sons ne pourrait pas le jouer non plus). P4, sans suite prévue.

## Hors périmètre

- La moitié « bruitages » de `HandleMapSoundEffects` (`ResetSoundEffectRuntime`, choix et lecture du son de warp) :
  inchangée, chantier transitions.
- Les appels BGM des IA (`AI_FUN_8007bd8c`, `AI_FUN_8007f6c8`) et `0x8004a9e0` (B18).
- La table de surcharge par drapeau (7 entrées, B16), déjà différée par E11.c.
- La latence du streaming CD : les chargements restent instantanés et `0xA8` rend 0 (D-E11-5).
- `g_cdIsReady` et la tête de `HandleMapSoundStreaming` dans l'analyseur ; `IsBgmActivated` (plan audio).
- Le moteur, le convertisseur, l'extracteur, les données exportées.

## Budget

Parent 6 commits (T0.1, T1.2, T2.1, T2.2, T3.1, T3.2), analyseur 1. Un `verifier` (T2.3).

## Outils (lecture seule, 2026-09-25)

`mips.py` (désassemblage et références dans `ALUN_CD.EXE`) :

```python
"""Read-only MIPS helper for ALUN_CD.EXE (France).

Usage:
  python mips.py dis <start_hex> <end_hex>        disassemble a range, jal targets named
  python mips.py callers <target_hex>             every jal/j to target in the text section
  python mips.py word <value_hex>                 every aligned 32-bit word equal to value (pointer tables)
  python mips.py refs <global_hex>                lui/addiu|lw|sw|lh|sh|lb|sb pairs that address the global
"""
import os
import re
import struct
import sys

import capstone

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
SRC = r"D:/development/repo/alundra-casaengine-project-converter/alundra-datas-analyser/AlundraTools/AlundraEngine"

data = open(EXE, 'rb').read()
t_addr, t_size = struct.unpack_from('<II', data, 0x18)
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)
md.detail = True


def load_names():
    names = {}
    pat1 = re.compile(r'GHIDRA:\s*(\w+)\s*@\s*0x([0-9A-Fa-f]{8})')
    for root, _, files in os.walk(SRC):
        for f in files:
            if not f.endswith('.cs'):
                continue
            try:
                text = open(os.path.join(root, f), encoding='utf-8', errors='replace').read()
            except OSError:
                continue
            for m in pat1.finditer(text):
                names.setdefault(int(m.group(2), 16), m.group(1))
    return names


def word_at(addr):
    off = 0x800 + (addr - t_addr)
    return data[off:off + 4]


def dis(start, end, names):
    addr = start
    while addr < end:
        w = word_at(addr)
        ins = next(md.disasm(w, addr), None)
        text = f'{ins.mnemonic:8s} {ins.op_str}' if ins else '(invalid)'
        note = ''
        if ins and ins.mnemonic in ('jal', 'j'):
            tgt = ins.operands[0].imm
            note = '   ; ' + names.get(tgt, f'FUN_{tgt:08x}')
        label = f'  <{names[addr]}>' if addr in names else ''
        print(f'  {addr:08x}: {struct.unpack("<I", w)[0]:08x}  {text}{note}{label}')
        addr += 4


def callers(target, names):
    fn_starts = sorted(names)
    for i in range(0, t_size, 4):
        w = struct.unpack_from('<I', data, 0x800 + i)[0]
        op = w >> 26
        if op in (2, 3):
            addr = t_addr + i
            tgt = ((addr + 4) & 0xF0000000) | ((w & 0x03FFFFFF) << 2)
            if tgt == target:
                owner = max((s for s in fn_starts if s <= addr), default=None)
                oname = f'{names[owner]}@{owner:08x}' if owner is not None else '?'
                print(f'  {addr:08x}: {"jal" if op == 3 else "j"} -> {target:08x}   (nearest named fn below: {oname})')


def word(value):
    for i in range(0, len(data) - 0x800 - 3, 4):
        w = struct.unpack_from('<I', data, 0x800 + i)[0]
        if w == value:
            print(f'  {t_addr + i:08x}')


def refs(glob, names):
    fn_starts = sorted(names)
    hi = (glob + 0x8000) >> 16 & 0xFFFF
    lo = glob & 0xFFFF
    for i in range(0, t_size, 4):
        w = struct.unpack_from('<I', data, 0x800 + i)[0]
        if w >> 26 == 0x0F and (w & 0xFFFF) == hi:  # lui
            reg = (w >> 16) & 31
            base = t_addr + i
            for j in range(1, 12):
                a2 = base + 4 * j
                if a2 >= t_addr + t_size:
                    break
                w2 = struct.unpack_from('<I', data, 0x800 + (a2 - t_addr))[0]
                op2 = w2 >> 26
                rs = (w2 >> 21) & 31
                if rs == reg and (w2 & 0xFFFF) == lo and op2 in (0x09, 0x20, 0x21, 0x23, 0x24, 0x25, 0x28, 0x29, 0x2B):
                    ins = next(md.disasm(struct.pack('<I', w2), a2), None)
                    owner = max((s for s in fn_starts if s <= a2), default=None)
                    oname = f'{names[owner]}@{owner:08x}' if owner is not None else '?'
                    print(f'  {a2:08x}: {ins.mnemonic} {ins.op_str}   ({oname})')


if __name__ == '__main__':
    names = load_names()
    cmd = sys.argv[1]
    if cmd == 'dis':
        dis(int(sys.argv[2], 16), int(sys.argv[3], 16), names)
    elif cmd == 'callers':
        callers(int(sys.argv[2], 16), names)
    elif cmd == 'word':
        word(int(sys.argv[2], 16))
    elif cmd == 'refs':
        refs(int(sys.argv[2], 16), names)
```

Limite connue de `refs` : il ne voit que les accès dont le `lui` précède l'accès de moins de douze instructions ; le
« nom de fonction le plus proche » n'est qu'un indice (les fonctions sans commentaire `GHIDRA:` n'ont pas de nom).
Chaque fait cité ci-dessus a été relu dans le désassemblage de la fonction entière.

`bgm_corpus.py` (corpus des opcodes BGM) :

```python
"""Read-only corpus scan: every reachable 0xA5 / 0xA6 / 0xA7 site in the exported event programs.

Recursive descent from every program-table entry at the real size of each opcode (EventOpcodeSizeTable.cs),
following 0x02 (goto), 0x03/0x04 (conditional goto) targets; 0x00 (Break) continues, 0xFF (End) stops,
a size-0 or unknown opcode stops. Dynamic jumps (0x7D-0x81) cannot be followed: the counts are a lower bound.

Usage: python bgm_corpus.py <alundra-project root> <EventOpcodeSizeTable.cs> [--context N]
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict

root = sys.argv[1]
size_table_path = sys.argv[2]
context = int(sys.argv[4]) if len(sys.argv) > 4 and sys.argv[3] == '--context' else 6

sizes = {}
names = {}
for m in re.finditer(r'\{\s*0x([0-9A-Fa-f]{2}),\s*new\((\d+),\s*"([^"]*)"\)\s*\}', open(size_table_path, encoding='utf-8').read()):
    sizes[int(m.group(1), 16)] = int(m.group(2))
    names[int(m.group(1), 16)] = m.group(3)

WATCH = {0xA5, 0xA6, 0xA7}
AUDIO = {0xA5, 0xA6, 0xA7, 0xAB, 0xBF, 0xBD, 0xBE, 0x12, 0x75, 0xB9, 0xA8, 0xBA, 0x53}


def s16(v):
    return v - 0x10000 if v & 0x8000 else v


def descend(codes, starts):
    seen = {}
    stack = list(starts)
    while stack:
        i = stack.pop()
        while 0 <= i < len(codes) and i not in seen:
            op = codes[i]
            size = sizes.get(op, 0)
            if op == 0xFF:
                seen[i] = (op, [])
                break
            if size < 1:
                seen[i] = (op, None)
                break
            params = codes[i + 1:i + size]
            seen[i] = (op, params)
            if op == 0x02 and len(params) >= 2:
                stack.append(i + s16((params[1] << 8) | params[0]))
                break
            if op in (0x03, 0x04) and len(params) >= 2:
                stack.append(i + s16((params[1] << 8) | params[0]))
            i += size
    return seen


def fmt(op, params):
    if params is None:
        return f'{op:02X}(?)'
    return f'{op:02X}' + ('(' + ','.join(str(p) for p in params) + ')' if params else '')


sites = []
per_map_ops = defaultdict(Counter)
files = glob.glob(os.path.join(root, 'Maps', '**', 'events', '*.events.json'), recursive=True)
for path in sorted(files):
    doc = json.load(open(path, encoding='utf-8'))
    codes = doc.get('Codes') or doc.get('codes') or []
    starts = []
    for key in ('EventCodesATable', 'EventCodesBTable', 'EventCodesCTable', 'EventCodesDTable', 'EventCodesETable', 'EventCodesFTable'):
        for v in doc.get(key) or []:
            if 0 <= v < len(codes):
                starts.append(v)
    seen = descend(codes, set(starts))
    order = sorted(seen)
    name = os.path.basename(path)[:-len('.events.json')]
    for idx_pos, i in enumerate(order):
        op, params = seen[i]
        if op in WATCH:
            per_map_ops[name][op] += 1
            lo = max(0, idx_pos - context)
            hi = min(len(order), idx_pos + context + 1)
            ctx = []
            for j in order[lo:hi]:
                o, p = seen[j]
                mark = '>>' if j == i else ''
                if o in AUDIO or j == i or o in (0x02, 0x03, 0x04, 0x37, 0x00, 0xFF):
                    ctx.append(f'{mark}{j}:{fmt(o, p)}')
                else:
                    ctx.append(f'{mark}{j}:{o:02X}')
            sites.append((name, i, op, params, ' '.join(ctx)))

tot = Counter()
maps = defaultdict(set)
operands = defaultdict(Counter)
for name, i, op, params, ctx in sites:
    tot[op] += 1
    maps[op].add(name)
    operands[op][tuple(params or [])] += 1

print(f'files scanned: {len(files)}')
for op in sorted(WATCH):
    print(f'0x{op:02X} {names.get(op)}: {tot[op]} sites / {len(maps[op])} maps')
    for k, v in operands[op].most_common():
        print(f'    operands {k}: {v}')
print()
for name, i, op, params, ctx in sites:
    print(f'{name} @{i} {fmt(op, params)} | {ctx}')
```

`flag_writers.py` (écritures du mot de drapeaux de séquence et des deux mots du chemin CD) :

```python
"""Read-only: every store at offset 0x90 (sequence-state flag word) in ALUN_CD.EXE, with the 6 preceding
instructions, to show which sites can set bit 0x1 (play). Also every store at -0x7d50 / -0x57a8 (any base)."""
import struct
import capstone

EXE = r"D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE"
d = open(EXE, 'rb').read()
t_addr, t_size = struct.unpack_from('<II', d, 0x18)
md = capstone.Cs(capstone.CS_ARCH_MIPS, capstone.CS_MODE_MIPS32 + capstone.CS_MODE_LITTLE_ENDIAN)


def ins_at(a):
    w = d[0x800 + a - t_addr:0x800 + a - t_addr + 4]
    i = next(md.disasm(w, a), None)
    return f'{a:08x}: {i.mnemonic} {i.op_str}' if i else f'{a:08x}: (invalid)'


for off, label in ((0x0090, 'sw/sh/sb at +0x90'),):
    print(f'== {label}')
    for i in range(0, t_size, 4):
        w = struct.unpack_from('<I', d, 0x800 + i)[0]
        if (w >> 26) in (0x2B, 0x29, 0x28) and (w & 0xFFFF) == off:
            a = t_addr + i
            print('--')
            for k in range(6, 0, -1):
                print('   ', ins_at(a - 4 * k))
            print(' >>', ins_at(a))
for off, label in ((0x82b0, 'sw at -0x7d50 (g_cdIsReady 0x800a82b0 with lui 0x800b)'), (0xa858, 'sw at -0x57a8 (0x8009a858 with lui 0x800a)')):
    print(f'== {label}')
    for i in range(0, t_size, 4):
        w = struct.unpack_from('<I', d, 0x800 + i)[0]
        if (w >> 26) == 0x2B and (w & 0xFFFF) == off:
            print('   ', ins_at(t_addr + i))
print('== static words')
for a in (0x800a82b0, 0x8009a858):
    print(f'   {a:08x} = {struct.unpack_from("<i", d, 0x800 + a - t_addr)[0]}')
```

## Journal

| Date | Événement |
|---|---|
| 2026-09-25 | Relevé [binaire] en session principale (B1-B17), corpus mesuré, plan rédigé. |
| 2026-09-25 | Relecture fraîche de l'enveloppe : **REVISE**, quatre blocages. (1) **Retenu** : le départ de warp appelle `PlayMapMusic` (`AlundraWarpDirector.cs:379`, `:458`), manqué par la reconnaissance ; B17 récrit d'après le binaire (qui révèle aussi une erreur de champs dans la décompilation), P7, contrat et tests n-p ajoutés. (2) **Rejeté sur le fond, retenu sur le cas** : le relecteur jugeait le chemin synchrone actif ; le binaire montre le streaming (`0x8002c03c` → `0x8004e85c` ; mot `0x8009a858` jamais écrit), fait ajouté à B13 ; le cas « entrée + `0xA7 n,0` dans la même frame » était bien non couvert : P8 et test m. (3) **Retenu** : preuve de B6 complétée par le relevé de toutes les écritures en `+0x90` (`flag_writers.py`). (4) **Retenu** : P1 récrit, option B par défaut, prérequis et arrêt pour l'option A. |
| 2026-09-25 | Second relecteur frais : **REVISE**, trois blocages, tous **retenus**. (1) Le plan audio est désormais approuvé et en exécution (AUTO) : P1 récrit, exécution après son merge dans `main` par défaut, contrôle et arrêt en T0.1, variante encadrée. (2) `Alundra.sln` échoue déjà à `bbf33962` (4 × NU1605, mesuré par le plan audio) : validation ramenée au build d'`AlundraEngine` et d'`AlundraDataExtractor`, échec de la solution inchangé, recette `AlundraGame` indisponible. (3) L'état 1 du streaming n'avait pas de preuve binaire : désassemblé (`0x8004b280`-`0x8004b2a4`, table `0x80026538`), il confirme l'arrêt de l'ancienne séquence ; ajouté à B9 et B13. Deux REVISE : la révision suivante ouvre une seule relecture de clôture. |
| 2026-09-25 | Relecteur frais de clôture : **READY**. Plan soumis ; l'auteur retient les options proposées de P1, P2, P3 et P7 (D5 à D8). |
| 2026-09-25 | Plan approuvé, mode AUTO. T0.1 ⚠️ Blocked à l'étape 1 : plan audio non mergé dans `main` (O3). Arrêt. |
| 2026-09-25 | Plan audio mergé par l'auteur ; reprise en AUTO. T0.1 à T3.1 ✅ (`acba5ea`, analyseur `e495d7f`, `7ebc93e`, `6012bb0`, `417d766`, `cb1387a`), T2.3 CONFIRMED (vérificateur et critique de complétude). Écart de `main` consigné (O4). T3.2 🧪 : recette préparée dans le worktree. |
| 2026-09-26 | Recette validée en jeu par l'auteur. T3.2 ✅, **chantier clos**. Restent à l'auteur : le pointeur moteur de `main` (O4), le merge des branches `chantier/bgm-demarrage-binaire` (parent et analyseur), et les suites S1 à S4. |
