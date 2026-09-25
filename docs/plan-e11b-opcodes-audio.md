# Plan E11.b — le reste des opcodes audio (0xA5, 0xAB/0xBF, 0xA6/0xA7, anti-doublon, table carte→groupe)

Date : 2026-09-02. Le périmètre différé par E11.a (`docs/plan-e11-audio.md` §4/§6), E11.c étant
livrée : un vrai backend musique existe, « inertes tant qu'E11.c est bloquée » ne vaut plus.

Décisions utilisateur (2026-09-02) : **oracle simulé** (aucun de ces opcodes ne s'exécute sur la
389 — les goldens sont aveugles ET la carte l'est aussi ; en jeu, seule l'absence de régression
sonore sur le bateau est vérifiée) ; **le son de départ (0x53 + portails) est DIFFÉRÉ au chantier
transitions** — l'opcode 0x53 n'a aucun case dans le runner et le canal consommateur
(`HandleMapSoundEffects` au départ du warp, avec sa logique fondu/coupure selon le changement de
musique) est indissociable du warp lui-même. Décision de session : **0xB9 (streaming CD) est HORS
périmètre** — absent de la liste de différés d'E11.a, et la route streaming a été délibérément
écartée en E11.c (le faux backend la gèlerait en test).

## §1 — Les faits (reconnaissance décompilation + DLL, 2026-09-02, file:line vérifiés)

Sémantiques originales (sous-module `alundra-datas-analyser`) :
1. **0xA5 « Stop all sound »** (taille 1, handler `EntityEventHandlers.cs:3127-3131`) →
   `SoundManager.StopAllSound()` : si `g_currentMapSoundIndex>=0`, stoppe les 24 voix SFX SEULEMENT
   quand `g_soundEffectState!=0` (l'état de la machine de fondu de 0xA6 !) et le remet à 0, restaure
   le volume maître 0x7f/0x7f, met le volume séquenceur à 0x7f, et **RELANCE le BGM** (`PlaySeq`) si
   `IsBgmActivated`.
2. **0xA6 « Load bgm »** (taille 2, `:3134-3138`) → `LoadBgm(v[1])` ; `LoadBgmCore`
   (`SoundManager.cs:640-668`) ne distingue que 0/non-0 : 0 → arrêt BGM immédiat ; non-0 →
   `g_soundEffectState=0x78`, qui arme la **machine de fondu maître à 120 pas** (`FUN_8004b674`,
   `:3762-3802` : rampe descendante frames 0x78..0x3d, à 0x3d stoppe le BGM et key-off les 24 voix,
   au compteur 3 restaure le maître 0x7f). La valeur de l'opérande au-delà de 0/non-0 est inutilisée.
3. **0xA7 « Play music »** (taille 3, `:3141-3145`) → `FUN_8004b114(v[1]=index musique,
   v[2]=drapeau stop-all)` ; Core (`:423-470`) : index<0 ignoré ; ==0 stoppe la séquence et libère le
   VAB musique ; >0 charge (synchrone si `g_cdIsReady==0`, sinon streaming asynchrone consommé par
   `HandleMapSoundStreaming`, qui applique `StopAllSound` si le drapeau est posé, `:5576-5579`).
   `LoadMapSequenceCore` (`:514-581`) lit `SoundBin.MusicSeqVabOffsets` — **le MÊME espace d'index
   brut que `bgm-manifest.json`** (l'extracteur E11.c a rendu ces triplets) — puis volume 0x7f, puis
   StopAllSound (drapeau) ou PlaySeq. **Écart de sémantique vs la table par-carte d'E11.c** : ici
   0 = stop (pas « ne rien toucher »), pas de remap −1→1 ni 45→stop — ne PAS réutiliser
   `ResolvePlaybackDirective`.
4. **0xAB** (taille 4, `:3201-3205`) → `PlaySoundEffectWithToneVolumeMix(v[1]=sfxId, v[2]=mix G,
   v[3]=mix D)` ; **0xBF** (taille 5, `:3613-3617`) → LA MÊME fonction avec `v[1], v[3], v[4]` —
   **v[2] est ignoré par le handler décompilé** (par analogie avec 0xBD, plausiblement l'octet haut
   de l'id — étiqueté INFÉRENCE ; le port suit le décompilé verbatim, règle du repo, et consigne
   l'incertitude). La fonction (`SoundManager.cs:5220-5274`) **NE JOUE RIEN** : elle résout la fiche
   pour le groupe courant, cherche pour chaque tonalité une voix DÉJÀ EN LECTURE de ce
   sfxId+toneIndex (`FindVoiceBySfxIdAndToneIndex`, `:4425-4442`), recalcule le volume stéréo
   (constantes magiques MIPS, `:5276-5298`) et l'applique (`SetVoiceVolume`, `:5300-5320`). Sfx non
   audible → no-op total.
5. **Anti-doublon par frame** : `IsSoundEffectAlreadyPlaying` (`:3842-3861`), table de **64 slots**
   (`INT_ARRAY_80165028`) : id déjà présent → doublon (refus) ; premier slot 0 → enregistre et joue ;
   **table pleine + id absent → JOUE quand même** (le débordement autorise les doublons — sémantique
   à porter telle quelle). Consulté par `PlaySoundEffectCore` (`:3872-3892`) pour TOUS les appelants.
   **La remise à zéro appartient à la boucle de JEU, pas au tick audio 60 Hz** :
   `FinalizeAudioBuffers` (`:3804-3813`) vidé uniquement depuis `HandleMapSoundStreamingCore`, appelé
   une fois par frame par `GameEngine.Update` (`GameEngine.cs:1579`) et par frame de transition de
   warp (`:286`).
6. **Table carte→groupe de sons** : `SoundBin.VabIndexByMapId` (`SoundBin.cs:2334-2335`, 483
   entrées ; carte 389 → groupe 0x38 `:2726`). Consommation : `GetSoundGroupByMapId`
   (`SoundManager.cs:5201-5218`) ; au changement, `LoadMapSoundGroup` recharge le VAB de carte.
   Résolution par fiche : `TryResolveSoundEffectRecord` (`:4444-4475`) — VabId −2 invalide, −1 = VAB
   global, ==groupe courant = VAB de carte, sinon marche de la chaîne `RefSfxId`
   (`FindSfxRecordForSoundGroup`, `:4165-4191`) sinon ABANDON du son. **La table n'existe dans AUCUNE
   sortie extraite** (l'extracteur ne l'emploie que pour énumérer les VAB distincts) ; `sfx.json`
   porte déjà le côté fiche (VabId/RefSfxId).
7. **La clé du plafond de polyphonie originale (CORRIGÉE en relecture — ma première dérivation
   était fausse)** : `CountActiveVoicesForSfx(sfxId)` indexe `g_soundEffectData` avec l'id DEMANDÉ
   et filtre les voix par le VabId de CETTE fiche — la fiche **DEMANDÉE, non résolue**
   (`SoundManager.cs:4025-4034`, `:4049`) ; seul `MaxVoices` est lu sur la fiche RÉSOLUE (`:3911`,
   `:3920`) ; or les voix sont ENREGISTRÉES avec le VabId de la fiche RÉSOLUE (`:3990-3995`).
   Conséquence fidèle : **dans le cas d'une redirection par la chaîne `RefSfxId`, le filtre ne
   matche jamais — le plafond ne mord PAS** dans l'original. C'est ce comportement-là (clé =
   (id demandé, VabId de la fiche demandée), plafond inopérant sous redirection) que B3 porte tel
   quel.
8. **La garde d'état de fondu coupe TOUS les SFX** (fait ajouté en relecture) : la toute première
   garde de `PlaySoundEffectCore` retourne immédiatement si `g_soundEffectState != 0`
   (`SoundManager.cs:3872-3877`, même garde à `:4124`) — pendant les 120 pas armés par 0xA6, aucun
   SFX ne part ; 0xA5 (qui remet l'état à 0) les rouvre. C'est la moitié OBSERVABLE de 0xA6.

État DLL (parent) :
9. Implémentés avec dégradé compté : 0xBD/0xBE (id=(v2<<8)|v1, avance 3), 0x12/0x75 (id=v1, avance
   2) (`AlundraEventProgramRunner.cs:725-782`) ; prédicats 0xA8/0xBA à Result=0 forcé (D-E11-5,
   `:784-795`). **0xA5/0xA6/0xA7/0xAB/0xBF n'ont AUCUN case** — chemin `UnknownOpcode`, kind
   `UnknownSkipped`, avance par la table (1/2/3/4/5, `EventOpcodeSizeTable.cs:196-198`).
10. **Goldens aveugles ET carte aveugle** : zéro occurrence des cinq opcodes dans les six goldens et
   dans `docs/intro-programs-389.txt` (seul 0xBD apparaît, 23 dispatches déjà `Implemented`) — les
   implémenter ne peut déplacer AUCUN octet doré, pas même un kind. Chaque tranche porte son oracle.
11. Vocabulaire des seams actuel : `IAlundraSoundPlayer` n'expose que `PlaySfx(int)`
    (`AlundraSoundPlayer.cs:16-23`) ; `IAlundraMusicPlayer` n'expose que
    `PlayMapMusic(mapId)/StopMusic()`, `PlayFromRawIndex` est PRIVÉ (`AlundraMusicPlayer.cs:21-36`,
    `:127`). Substrat voix existant : `_liveVoicesBySfxId` (par id demandé — la bonne moitié de la
    clé du fait 7 ; le code note lui-même que l'égalité demandé==résolu ne tient que sans groupe).
    Moteur : `AudioService.SetVoiceVolume(handle, volume)` + `AudioVoiceParameters.Pan`/`WithPan`
    existent (`AudioService.cs:247`, `AudioVoiceParameters.cs:14-42`) — le mix G/D de 0xAB/0xBF doit
    se PROJETER sur (volume, pan) ; le mapping exact ((G,D) → volume=max, pan=(D−G)/(D+G) ou le
    modèle du backend) est une dérivation d'exécution, déviation documentée si le modèle du backend
    ne reproduit pas exactement des gains G/D indépendants.
12. Anti-doublon : retiré d'E11.a par décision D-E11-4 (« supprimer le mode de défaillance »),
    consigné dans le plan (`plan-e11-audio.md:174-189`) et le code (`AlundraSoundPlayer.cs:46-49`),
    avec l'exigence explicite pour E11.b : « son propriétaire de frame nommé : qui appelle la remise
    à zéro, depuis quel site de la frame, et ce qui se passe sans lecteur installé ».
13. Le précédent d'export de table compagnon : `MapMusicIndex.csv` (E11.c, analyseur → lien csproj →
    lecteur convertisseur → `Maps/music-index.json` → chargeur DLL) ; et la leçon E11.c : l'état de
    garde vit où vit l'état de l'original (globales → portée session).

## §2 — Décisions

- **D-B-1 (utilisateur)** — oracle simulé ; bateau = non-régression sonore seulement.
- **D-B-2 (utilisateur)** — son de départ → chantier transitions (les faits §1 du canal —
  `g_warpSoundEffectId`, table `g_warpBehaviorTable` des portails, `HandleMapSoundEffects` — sont
  consignés dans le rapport de reconnaissance pour ce chantier futur).
- **D-B-3** — 0xB9 hors périmètre (voir en-tête).
- **D-B-4 — anti-doublon : port de la table 64 slots, propriété par le LECTEUR, vidage UNE fois par
  frame RENDUE au site de fermeture de frame — DÉVIATION DE CADENCE ASSUMÉE ET PROUVÉE (rondes 1+2
  de relecture)**. La ronde 1 exigeait le tick logique ; la ronde 2 a prouvé que c'est
  STRUCTURELLEMENT inimplémentable ici : l'original vide UNE fois par frame, EN FIN de frame, après
  tout le dispatch (`GameEngine.cs:1560→:1579` → `FinalizeAudioBuffers`), mais notre port n'a AUCUNE
  boucle de frame unifiée — chaque passe du proxy possède SA boucle de ticks
  (`AlundraWorldProxy.cs:1265-1268` et `:1288-1291`) et chaque ENTITÉ tick 1..N dans son propre
  `Update` : un vidage « par tick » serait soit équivalent au par-frame (boucle autonome après les
  passes), soit incohérent (vidage ENTRE deux passes du même tick). Le vidage vit donc au site de
  fermeture de frame du proxy (à côté de l'unique `_logicClock.CloseFrame()`, après TOUTES les
  passes de dispatch de la frame — exactement la position du `FinalizeAudioBuffers` original dans SA
  frame). **Équivalence** : en libre ≥50 Hz (la config livrée), une frame rendue porte ≤1 tick —
  fenêtre ≡ originale ; sur une frame de rattrapage (2+ ticks) la fenêtre s'élargit à N ticks
  (<N×20 ms audibles), et aucun programme réel ne rejoue le même id à deux ticks CONSÉCUTIFS (les
  blocs 0xBD du corpus sont séparés par des 0x37 Wait multi-frames — `intro-programs-389.txt`).
  La table vit dans `AlundraSoundPlayer` (par monde — l'équivalent du reset d'entrée de carte),
  filtre DANS `PlaySfx` (tous les appelants, fait 5) ; PAS de modification d'`AlundraLogicClock`
  (arrêt permanent d'E11.a). Sans lecteur installé : no-op. Sémantique de débordement (plein →
  joue) portée telle quelle et testée. **Deux tests de cadence ÉPINGLENT LA DÉVIATION ET LA
  FIDÉLITÉ** : (a) frame à ticksThisFrame ≥ 2, même id à deux ticks → UNE seule voix — assumé
  comme la déviation documentée (la mutation « vidage dans une boucle de ticks autonome » ne change
  rien à ce test — c'est le test (b) qui discrimine le site) ; (b) MÊME tick, deux passes de
  dispatch (map-events + rattrapage) jouant le même id → UNE voix, et l'id REJOUE à la frame rendue
  suivante — les mutations « vidage en tête de passe de dispatch » et « vidage jamais appelé »
  font tomber ce test chacune dans un sens.
- **D-B-5 — l'état partagé 0xA5/0xA6 (la machine de fondu `g_soundEffectState`) est UNE SEULE pièce
  d'état, portée en session** (leçon E11.c : globale originale → singleton de session), avancée par
  tick logique depuis la passe de frame du proxy (le patron fondu écran/dialogue). 0xA5 et 0xA6/0xA7
  vivent donc dans la MÊME tranche (B2) — les découper séparerait un état couplé.
  **Dépendance nommée (relecture)** : `PlaySfx` CONSULTE cet état (fait 8 — fondu armé ⇒ aucun SFX
  ne part ; 0xA5 rouvre). B1 pose le point d'interrogation du seam (l'état n'existe pas encore →
  lecture dégradée « jamais armé ») ; B2 branche la garde réelle et porte le test nommé
  « 0xA6 armé ⇒ PlaySfx ne joue rien ; 0xA5 ⇒ rejoue », avec la mutation « garde d'état supprimée
  dans PlaySfx » qui le fait tomber.
- **D-B-6 — vocabulaire des seams, additif** : `IAlundraSoundPlayer` gagne
  `StopAllSfx()` (0xA5, la moitié SFX) et `RemixVoice(sfxId, left, right)` (0xAB/0xBF, résolution de
  la fiche + projection (volume, pan) sur les voix vivantes de `_liveVoicesBySfxId`) ;
  `IAlundraMusicPlayer` gagne un point d'entrée par index brut (0xA7 — `PlayFromRawIndex` passe
  d'implémentation privée à membre du seam, sémantique du fait 3 : <0 ignoré, 0 stop, >0 joue) et
  `RestartIfActive()` (0xA5). Chaque nouveau membre a son dégradé compté (patron D-E11-5).
- **D-B-7 — table carte→groupe** : export `VabIndexByMapId` en CSV analyseur (précédent
  MapMusicIndex, brut, 483 entrées) → lien csproj → lecteur convertisseur →
  `Maps/sound-group-index.json` → chargeur DLL dans `InstallAudioSystems`, groupe passé au
  `AlundraSoundPlayer` du monde qui le donne à `TryResolve` (le chemin chaîne `RefSfxId` existe et
  est déjà testé). **Le plafond de polyphonie passe à la clé fidèle du fait 7 corrigé** : compte
  par id demandé, filtré par le VabId de la fiche DEMANDÉE (`MaxVoices` lu sur la résolue) — sous
  redirection le plafond est inopérant, comme dans l original. Ré-export complet obligatoire (règle permanente), baselines par manifeste
  avant/après : seuls `Maps/**/sound-group-index.json` (nouveau), `report.json` — et rien d'autre —
  peuvent bouger (si le nettoyage N1-N3 est livré avant, le double-export est déjà l'oracle).
- **D-B-8** — 0xBF : port du handler décompilé verbatim (v[2] ignoré), incertitude consignée au code.

## §3 — Trois tranches, exécution par agents

- **B1 — anti-doublon + 0xAB/0xBF (DLL + MOTEUR — corrigé en relecture : le primitif est CERTAIN,
  pas éventuel)** : `AudioService` n'expose AUCUN setter de pan par handle vivant (seulement
  `SetVoiceVolume`/`GetVoiceVolume`, `AudioService.cs:225-268` ; `Pan` n'est qu'un paramètre
  initial) alors que le backend sait le faire (`IAudioBackend.cs:33-37`,
  `MonoGameAudioBackend.cs:114-135`). B1 ajoute donc au moteur la méthode ADDITIVE
  `AudioService.SetVoicePan(AudioVoiceHandle voice, float pan)` — **qui PRÉSERVE le gain de bus
  (CORRECTION ronde 2)** : `SetVoiceVolume` pousse `BaseParameters.Volume ×
  Mixer.GetEffectiveGain(bus)` via le contrat `ApplyGain` (`AudioService.cs:247-256/:574-577`) ;
  pousser `BaseParameters` brut par `SetParameters` écraserait ce gain (régression moteur partagée,
  invisible à gain 1). `SetVoicePan` met à jour le pan de `BaseParameters` puis réapplique par le
  même contrat que `ApplyGain` (volume effectif inchangé). Test moteur obligatoire : bus à gain ≠ 1
  → `SetVoicePan` change le pan ET laisse le volume appliqué identique — la mutation « pousser
  BaseParameters brut » le fait tomber. Un commit moteur et le bump de pointeur — arrêts dédiés au
  sous-module (voir « Arrêts »). Ensuite, DLL :
  la table 64 slots dans `AlundraSoundPlayer` + le vidage UNE fois par frame rendue au site de
  fermeture de frame du proxy (à côté de `_logicClock.CloseFrame()`, après toutes les passes de
  dispatch — le site de D-B-4) ;
  `RemixVoice` + dispatch 0xAB/0xBF (dégradés comptés sans lecteur). **Dispositif de test unique
  (corrigé en relecture — le harnais d'intro EST lui-même le faux lecteur, sans backend : rien à y
  observer et pas question d'y dupliquer la table)** : les tests anti-doublon passent par le VRAI
  chemin `AlundraWorldProxy` + `AudioService(FakeAudioBackend)` (le précédent
  `AlundraWorldProxyAudioInstallationTests.cs:60-74` + `AlundraSoundPlayerTests.cs:74-84`),
  observable = les voix du backend factice ; le faux lecteur du harnais reste un simple enregistreur
  SANS table. Tests : la table refuse le doublon intra-frame et l'autorise à la frame rendue
  suivante ; les DEUX tests de cadence de D-B-4 (déviation épinglée + même-tick-deux-passes) ; le
  DÉBORDEMENT (65e id distinct) joue ; production-site : deux requêtes du même id dans la même
  frame via le vrai `Update` → UNE voix backend ; 0xAB/0xBF ne déclenchent RIEN sur sfx non audible
  (zéro voix backend créée) ; remix d'une voix vivante → pan changé ET volume effectif intact sur
  le handle backend (gain de bus ≠ 1). **Mutations** : vidage supprimé → « rejoue à la frame
  suivante » tombe ; vidage en tête de passe de dispatch → le test même-tick-deux-passes tombe ;
  site d'appel du vidage supprimé dans le proxy → le test au vrai `Update` tombe ; RemixVoice qui
  déclenche une lecture → le no-op tombe ; SetVoicePan poussant BaseParameters brut → le test de
  gain moteur tombe.
**B1 LIVRÉE le 2026-09-07.** Moteur **1584 / 1584**, `Alundra.Tests` **791 / 791**, convertisseur
intact à 156. L'exécuteur a vérifié ses mutations en vrai : vidage déplacé en tête de passe → les
tests de cadence tombent, puis remis.

**Le piège du gain est plus dur que le plan ne le disait, et la recette est donc plus précise.**
`IAudioBackend` n'a **aucun `SetPan`** : le seul chemin pour changer le pan d'une voix vivante est
`SetParameters`, qui pousse **tous** les paramètres, donc le volume — alors qu'`ApplyGain` ne pousse
que le volume. `SetVoicePan` écrit donc explicitement `Volume × GetEffectiveGain(bus)` dans l'appel
qui change le pan. Sans ça le gain de bus est écrasé, **invisible tant que le gain vaut 1** : d'où le
test obligatoire à gain 0,5.

**[B1-a] La projection du mix stéréo en (volume, pan) est une dérivation, et le restera.** Vérifié en
session principale : l'original (`SoundManager.PlaySoundEffectWithToneVolumeMixCore`, `:5221-5275`)
ne mixe pas une voix, il pilote le **matériel PSX tonalité par tonalité** — `SquarePlusOne(x) = (x+1)²`
sur le mix gauche/droite et sur le volume de tonalité, multiplication par le volume de programme,
poids de panoramique par tonalité (`tonePan < 0x41 ? 0x3f : 0x7f - tonePan`), puis deux mises à
l'échelle en virgule fixe MIPS à constantes magiques (`0x80020009 >> 13`, `0x8418828d >> 11`), et
enfin deux volumes matériels gauche/droite distincts.

**CORRECTION de première rédaction** : j'avais écrit « le port n'a qu'une voix par sfx ». **C'est
faux.** `AlundraSoundPlayer.PlaySfx` (`:133-148`) boucle sur `resolution.Tones` et démarre **une voix
par tonalité** ; le convertisseur exporte d'ailleurs chaque tonalité dans son propre fichier
(`sfx_0057_0.wav`, `sfx_0057_1.wav`…). La **structure par tonalité de l'original est donc reproduite**.
Mesuré sur le manifeste : 767 effets sur 961 n'ont qu'une seule tonalité, 120 en ont deux, 4 en ont
trois.

**Ce qui reste approximé est plus étroit que ça** : la projection, pour CHAQUE voix, du couple
matériel (volume gauche, volume droite) sur le couple moteur (volume, pan) — et avec elle la réponse
quadratique, le poids de panoramique par tonalité et les mises à l'échelle en virgule fixe. Chaque
échantillon exporté est **mono 16 bits**, à sa fréquence naturelle (la hauteur PSX est cuite dans la
fréquence), donc un pan est bien la seule façon de le placer dans l'image stéréo. **Noter la réponse
quadratique** : appliquer la seule mise au carré sans le reste de la chaîne pourrait éloigner autant
que rapprocher, donc rien n'est changé au jugé — l'oracle reste l'oreille sur le bateau (D-B-1), et la
chaîne d'origine est écrite ici pour qui devra y revenir.

**La machine de fondu, transcrite en session principale le 2026-09-07** (`FUN_8004b674`,
`SoundManager.cs:3763-3801`) — le résumé du fait 2 était juste sur les jalons mais imprécis sur ce qui
se passe en bas de rampe :

```
si etat == 0            -> rien, la machine est au repos
n = etat - 1
si n == 3      -> etat = 3    ; maitre(0x7f, 0x7f)                       // restauration
si n == 0x3c   -> etat = 0x3c ; maitre(0, 0) ; InitializeBgm(seq EN COURS) ; key-off des 24 voix
si n <  0x3d   -> etat = n                                                // descente silencieuse
sinon          -> v = (etat - 0x3d) * 0x7f / 0x3c ; etat = n ; maitre(v, v)   // rampe
```

**Correction, en deux temps.** (1) Le fait 2 dit « à 0x3d stoppe le BGM » : le code n'arrête pas, il
appelle `InitializeBgm(g_requestedSeqId)`. (2) Et `g_requestedSeqId` n'est PAS « la piste demandée »
— c'est **la séquence actuellement chargée**, écrite au chargement de séquence
(`SoundManager.cs:558`) et par le streaming (`:5573`). Donc en bas de rampe l'original **relance la
piste en cours**.

**`0xA6` n'est donc pas un changement de piste** : c'est un **fondu de sortie sur 59 tics, une relance
de la même musique, puis le retour du volume**. Un reset musical, pas une transition. Il ne charge
rien au moment où on l'appelle : il pose `etat = 0x78`, et tout se passe plus tard. La rampe se calcule sur l'état **avant** décrément, et ne
tourne que tant que `etat >= 0x3e` : à `0x78` elle vaut 124, à `0x3e` elle vaut 2.

Et le point observable : **`etat != 0` bloque tout départ de SFX** pendant les 120 tics ; `0xA5`, qui
remet l'état à 0, les rouvre. C'est la moitié visible de `0xA6`.

- **B2 — le trio BGM 0xA5/0xA6/0xA7 (DLL seule)** : la machine de fondu 120 pas en session (avance
  par tick depuis la passe de frame du proxy, patron E10/E12), `LoadBgm(0/non-0)`, `StopAllSound`
  fidèle (SFX stoppés SEULEMENT si l'état de fondu est armé, volumes restaurés, BGM relancé si
  actif), `PlayFromRawIndex` public + drapeau stop-all. Dispatch des trois opcodes, dégradés
  comptés. Tests : la rampe 120 pas aux jalons du fait 2 (0x78→0x3d→3), 0xA6 opérande 0 vs non-0,
  0xA5 conditionnel sur l'état de fondu + relance BGM (backend factice), 0xA7 <0/0/>0 + drapeau ;
  production-site par simulation (programme synthétique dispatchant les trois par le vrai runner).
  **Mutations** : machine avançant par frame au lieu de par tick → jalons faux ; 0xA5
  inconditionnel → le test « fondu non armé = SFX intacts » tombe.
**B2 LIVRÉE le 2026-09-07.** `Alundra.Tests` **808 / 808** (791 + 17), moteur et convertisseur
intacts. Machine de fondu dans son propre directeur de session, avancée **par tic** depuis la passe de
frame du proxy, à côté du directeur de fondu d'écran. Mutations vérifiées en vrai par l'exécuteur :
avance par frame au lieu de par tic → les jalons tombent ; garde d'état retirée de `0xA5` → « fondu
non armé, SFX intacts » tombe ; garde `IsArmed` neutralisée dans `PlaySfx` → le blocage tombe.

**Deux simplifications déclarées, à connaître :**

- **`IsBgmActivated`** — la bascule « musique activée/désactivée » de l'original — **n'est modélisée
  nulle part dans la DLL** ; le port la traite comme toujours vraie. La branche
  `LoadBgmCore:653-659` qu'elle commande est donc inatteignable.
- **La garde externe `g_currentMapSoundIndex >= 0`** de `StopAllSound` n'est pas portée : l'index
  correspondant du portage est un entier qui ne devient jamais négatif par construction, donc la
  garde est toujours satisfaite.

- **B3 — table carte→groupe (analyseur + convertisseur + DLL, ré-export complet)** : D-B-7. Tests :
  lecteur CSV/écrivain JSON (141+n convertisseur) ; DLL : groupe chargé à l'install, `PlaySfx`
  redirigé par la chaîne pour un VabId étranger (fixture existante), **plafond porté à la
  clé fidèle du fait 7 corrigé : (id demandé, VabId de la fiche DEMANDÉE)** — test aligné sur le
  décompilé : sous redirection, le compte reste 0 et le plafond NE BLOQUE PAS (l'original ne bloque
  pas) ; sans redirection, le plafond mord comme aujourd'hui ; dégradé sans fichier (log compté).
  Manifeste avant/après le ré-export (D-B-7). **Mutations** : groupe jamais passé (null) → le test
  de redirection au site d'install tombe ; plafond filtrant par le VabId RÉSOLU → le test
  « redirection ⇒ plafond inopérant » tombe.
**B3 — preuve par export, prédite AVANT exécution le 2026-09-07.** Ligne de base
`b3-baseline-manifest.sha256`, **23 197 fichiers**. Attendu, et rien d'autre :

1. **Ajouté** : `Maps/sound-group-index.json`, un fichier plat unique — le chemin a été résolu sur le
   seul précédent réel, `Maps/music-index.json`, la formulation `Maps/**/…` du plan n'étant réalisée
   nulle part dans le code.
2. **Modifié** : `report.json`.
3. **Possiblement modifié, et prévu cette fois** : `AlundraGame.json`. Le moteur y réécrit la taille de
   fenêtre au runtime (`CasaEngineGame.cs:171,178`) et l'export y remet la constante du convertisseur —
   or **le jeu a tourné depuis le dernier export** (recette en jeu d'E9.d). Ce n'est pas un effet de
   B3. Leçon retenue de C1, où il avait échappé à la prédiction.
4. **Possiblement modifié** : `AssetInfos.json`, **si et seulement si** le nouveau fichier passe par le
   registre d'assets — auquel cas il doit contenir exactement **une** entrée de plus.
5. **Rien d'autre.** Aucun PNG, aucun `.tileMap`, aucun `.backdrop.json`, aucun `.world`.

Puis **second export** : diff ⊆ `{report.json}`.

**Résultat, le 2026-09-07 — la prédiction tient, et plus serrée qu'elle.** Export complet,
**vérification PASSED** (19 505 chargés, 2 378 vérifiés), 23 198 fichiers.

| | Prédit | Mesuré |
|---|---|---|
| Ajouté | `Maps/sound-group-index.json` | **1**, exactement celui-là |
| Modifié | `report.json` | **1** |
| Modifié | `AlundraGame.json` *possible* | **aucun** |
| Modifié | `AssetInfos.json` *possible* | **aucun** |
| Supprimé | aucun | **0** |

Les deux entrées prévues « au cas où » ne se sont pas produites : le jeu n'a pas réécrit son fichier
de projet depuis le dernier export, et le nouveau JSON **ne passe pas par le registre d'assets** —
comme `music-index.json`, il est écrit directement. **Second export : diff = `{report.json}`
exactement.**

**B3 LIVRÉE.** Convertisseur **159 / 159**, `Alundra.Tests` **815 / 815**, moteur intact.
L'exécuteur a prouvé sa correction du plafond en vrai : filtre remis sur la fiche **résolue**, le test
« sous redirection le plafond ne mord pas » tombe (`Expected: 3, Actual: 1`), puis remis correct.

**Chemin résolu** : `Maps/sound-group-index.json`, **fichier plat unique**. La formulation
`Maps/**/…` du plan n'est réalisée nulle part dans le code ; le seul précédent réel,
`Maps/music-index.json`, est un fichier unique écrit une fois, indépendant du filtre `--maps`.

## E11.b CLOSE — **validée en jeu par l'utilisateur le 2026-09-07**

« pour moi c'est bon ». L'oracle choisi en D-B-1 est tenu : le bateau sonne comme avant.

| Tranche | Livrée | Preuve |
|---|---|---|
| B1 | pan de voix vivante + anti-doublon + `0xAB`/`0xBF` | moteur 1584, portage 791, mutations vérifiées en vrai |
| B2 | trio BGM `0xA5`/`0xA6`/`0xA7` | portage 808, jalons de rampe calculés à la main |
| B3 | table carte→groupe + clé de plafond fidèle | convertisseur 159, portage 815, diff d'export prédit tenu |

**Trois lectures corrigées en cours de route, toutes par retour au décompilé :** le piège du gain de
bus est plus dur que le plan ne le disait (pas de `SetPan` du tout côté backend) ; `0xA6` **n'est pas
un changement de piste** mais un fondu, une relance de la même musique et un retour ; et la projection
du mix stéréo restera une approximation, l'original pilotant le matériel tonalité par tonalité.

**Une mesure vaut d'être retenue : `0xAB` n'existe sur AUCUNE carte du corpus.** Des deux opcodes de
remix portés par B1, seul `0xBF` sert réellement (71 cartes). Le port de `0xAB` est correct et
inoffensif, mais c'est du code mort.

**Suites ouvertes** : [B1-a] la projection du mix, et les deux simplifications déclarées de B2
(`IsBgmActivated` non modélisée, garde d'index de carte toujours satisfaite). Aucune ne bloque.

**Décision de l'auteur du 2026-09-25** (`docs/plan-e13d-sous-inventaire.md`, D-E13D-37), à faire dans une tâche
dédiée, hors E13.d : ne plus approcher le mixage stéréo ([B1-a], appliquer le mix tonalité par tonalité comme
l'original) ; apprendre au moteur à rendre le son muet ; supprimer `IsBgmActivated` (analyseur :
`AlundraEngine/Sound/SoundManager.cs`, `AlundraEngine/StaticVariables.cs`), qui ne servait qu'à couper la musique
quand elle n'était encore que du bruit.

- **Acceptation** : suites au vert (`Alundra.Tests` 711+n, convertisseur 141+n, moteur inchangé ou
  +n si primitif pan), six goldens byte-identiques avec preuve d'exécution, verifiers de clôture par
  tranche ; **en jeu (utilisateur)** : le bateau sonne comme avant (ronflements, mouettes, trappe,
  musique) — c'est l'oracle de non-régression choisi en D-B-1.

**Budget** : B1 = un commit moteur (SetVoicePan + test) + pointeur + un commit DLL ; B2 = un
commit DLL ; B3 = un commit
analyseur + un commit convertisseur/ré-export + un commit DLL. Ordre B1 → B2 → B3 (B3 touche la clé
de plafond que B1 exerce). Exécution par agents, verifier de clôture avant chaque commit.

**Arrêts** : un golden qui bouge ; un fichier de ré-export hors liste D-B-7 qui bouge ;
`AlundraLogicClock` modifié ; `Program.cs` du Launcher stagé ; `alundra-project/` supprimé ;
**côté sous-module CasaEngineMonogame (ronde 2)** : toute édition moteur HORS l'ajout additif
`AudioService.SetVoicePan` + son test ; tout changement d'API publique ou de comportement moteur
existant ; suite moteur avec un NOUVEL échec ; bump de pointeur sans le commit moteur correspondant.
