# Rapport de découverte E20.g5c : l'arrêt du son (lecture seule)

**Ce qui a été touché.** Rien n'a été construit, modifié, indexé ni commité dans le dépôt `D:/development/repo/alundra-casaengine-project-converter`. Le code du son est identique entre le HEAD de ma lecture (`b36a78a7`) et le HEAD courant (`1a98ef5d`, les commits de l'executor g2b). L'executor a modifié l'infobulle de documentation de `EventProgramRunner.cs` dans le répertoire de travail, rien de plus.

**Binaire.** ALUN_CD.EXE (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` vérifié.

**Copies de travail.** `probe/` et `probe2/` viennent de `git archive` du HEAD (contenu commité seulement). L'export et le moteur y sont des jonctions vers `g5b-disc/probe/`, donc l'export du dépôt n'est jamais écrit. `probe2` est construit avec `skipcopy.targets`.

**Dossier de travail** (appelé `G5C/` plus bas) : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g5c-disc/`. Les empreintes SHA-256 sont dans `G5C/MANIFEST.sha256`.

**Étiquettes.** FAIT : lu dans le binaire, le code ou les données. MESURÉ : exécuté sur la machine MIPS ou sur la vraie DLL dans une copie. PROTOTYPE : patch jetable dans `probe2`. INFÉRENCE : déduit.

## 0. Verdicts

1. **MESURÉ, une erreur plus grave que « la DLL rejoue ».** 371 des 392 sites `0xBE` visent un son qui boucle. Le rejeu est refusé par le plafond de voix (`MaxVoices` 1). Le raclement ne s'arrête donc jamais, ni pour les statues, blocs et boutons scriptés, ni pour le pilier livré par g5a. Les 16 sites sur des sons non bouclés, eux, rejouent bien le son.
2. **MESURÉ sur la vraie DLL.** Le pilier de la carte 26 lance son raclement à l'image 34 de la poussée. Il joue encore 10 s après le relâchement. Le bouton de Torla (carte 426) lance le sien à l'image 52. Il joue encore à l'image 310, après le `0xBE` de l'image 215. Avec l'arrêt branché, le pilier s'arrête à l'image 103 et le bouton à l'image 215.
3. **FAIT, le moteur n'a rien à gagner.** `ISoundPlayer` n'a pas d'arrêt d'un son précis, mais le moteur sait arrêter une voix par poignée (`AudioService.Stop`, `StopWithFade`). Le lecteur DLL range déjà ses voix par id demandé. Un `StopSfx(int)` s'écrit en 12 lignes dans `SoundPlayer`, sans toucher le moteur.
4. **MESURÉ, les tests qui bougent.** Seuls 2 tests de la suite (5913) bougent : `PlaySound2Bis_0xBE_…` et `PlaySoundEffect_0x75_…`. Le pilier demande trois fichiers de test à adapter, sans changement d'assertion.
5. **FAIT, un défaut jumeau non nommé au plan.** L'opcode `0x75` appelle lui aussi `StopSoundEffect` dans le binaire (id sur 8 bits). La DLL le joue. Il n'a aucun site dans les données, donc aucun effet audible, mais un test l'épingle comme « jouer ».
6. **FAIT.** Le binaire a sept appels à `StopSoundEffect`. La décompilation définit `StopSoundEffect` (`SoundManager.cs:4111`) et ne l'appelle nulle part : elle rend les sept par `PlaySoundEffect`.
7. **MESURÉ, manque à consigner.** L'original coupe par un key-off, donc une extinction de 0,006 à 0,74 s selon le son (0,19 s pour le raclement 25). Cette extinction n'est ni exportée ni portée. Un `Stop` du moteur est un arrêt net.

## 1. Binaire : les gestionnaires et `StopSoundEffect`

**Les gestionnaires** (FAIT, rejoués contre le vrai code : 400 vecteurs par gestionnaire, 0 écart, `G5C/handlers_oracle.py`, `handlers_oracle_out.txt`) :

| Opcode | Adresse | Appel | Id | Longueur |
|---|---|---|---|---|
| `0xBD` | `0x80041C38` | `PlaySoundEffect` (`0x800490FC`) | `(b2<<8)\|b1` | 3 |
| `0xBE` | `0x80041C6C` | `StopSoundEffect` (`0x80049634`) | `(b2<<8)\|b1` | 3 |
| `0x12` | `0x8003D6C0` | `PlaySoundEffect` | `b1` | 2 |
| `0x75` | `0x8003FA9C` | `StopSoundEffect` | `b1` | 2 |

`0xBE` et `0x75` conservent donc les longueurs et dérivations d'id déjà portées par la DLL ; seule la fonction appelée change.

**`StopSoundEffect(id)`** (`0x80049634`, 36 mots, plus le chercheur de voix `0x80049714`). Le listing est dans `G5C/dis_stop_800.txt`.

- **Table des rangs** : `0x800A82E8 + 22·id`. Champs : vab `+0`, programme `+2`, tonalité `+4`, note `+6`, drapeaux `+8`, banque `+0xA`, réf `+0xC`, max voix `+0x10`, nb tonalités `+0x14`.
- **Table des voix** : `0x80175858`, 24 voix. Octet d'état `[i]`, id **demandé** à `+0x18+4i`, vab à `+0x78`, tonalité à `+0xD8`.

L'algorithme :
```
si id == 0 : retour (le rang n'est même pas touché)
si (rang.drapeaux & 2) == 0 :                 // « banque en cours de chargement » : on saute
    tant qu'une voix porte cet id :           // TOUTES les voix de l'id, quel que soit l'objet qui l'a lancée
        key-off de la voix (0x80094F20)
        voix.vab = -2 ; voix.id = 0           // la voix est libérée tout de suite
rang.drapeaux = 0                             // demi-mot entier mis à zéro
```
- **Key-off.** `0x80094F20` appelle `0x80091134`. Celle-ci pose seulement le bit de la voix dans les masques de key-off de libsnd (`0x801F7F08`/`0x801F7F10`), retire les key-on en attente et remet à zéro des champs de la voix. C'est un key-off différé, donc une phase de relâchement, pas une coupure.
- **Ce que Stop ne fait pas** (FAIT : aucune de ces adresses n'est lue dans `0x80049634-0x80049748`) :
  - il ne lit pas `g_soundEffectState` (`0x80175850`). Il n'est donc pas bloqué pendant les 120 images du compte à rebours qui bloquent `Play` ;
  - il ne touche pas la table anti-doublon (`0x80165028`, 64 mots, vidée une fois par image par `0x80048CD4`) ;
  - il ne touche pas la machine de fondu ;
  - il ne vérifie pas `id <= 0x3C2`. Les ids arrêtés dans les données vont jusqu'à 499, donc sans effet ici.

**`PlaySoundEffect`** (`0x800490FC`, pour comparaison). Ordre des gardes :
1. compte à rebours `!= 0` ;
2. `id <= 0` ;
3. `id > 0x3C2` ;
4. anti-doublon de l'image (enregistre l'id) ;
5. rang `-2` ;
6. rafraîchissement de la table des voix depuis l'état SPU (`0x80048FCC`) ;
7. plafond d'instances (`0x80049060`, comparé au max voix) ;
8. un key-on par tonalité (`0x80094660`).

**Modèle rejoué contre le vrai code** (`G5C/stop_oracle.py`, sorties `stop_oracle_out.txt`, `stop_oracle_mut_out.txt`, `stop_goldens.json`). Les stubs libsnd sont journalisés : key-on, key-off, état SPU, attributs de tonalité.
- 3000 séquences aléatoires de Play, Stop, image, fin naturelle, compte à rebours sur les ids 25, 85, 2, 55, 24, 46, 98, 229, 1, 7 : **0 écart**.
- Couverture : `Stop` plus chercheur **70/70** instructions ; façade `Play` 120/137.
- 88 mutants d'un mot de `Stop` : **69 tués, 19 survivants, tous équivalents** : sauvegardes de pile, restaurations de registres, délais déjà nuls, un `jr ra` qui retombe dans le chercheur pur. J'ai ajouté une classe de vecteurs après avoir vu survivre un mutant « 25e voix ».
- 7 scénarios nommés en goldens (SHA-256 `f75208112cbd5b0dba1635b0544d2ed7e63734c89c14de1f8fdb1023908d3e06`) :
  - le pilier (Play à chaque tick puis Stop) ;
  - deux objets pour le son 25 (le plafond est de 1, un seul Stop coupe tout) ;
  - le son 85 à deux tonalités ;
  - Stop d'un son qui ne joue pas ;
  - Play, Stop, Play dans la même image (le deuxième Play est refusé) ;
  - Stop pendant le compte à rebours (non bloqué, alors que Play l'est) ;
  - une voix finie mais pas rafraîchie (Stop la retrouve par l'id).

**Les sept appels** (FAIT) :

| Adresse | Où | Id |
|---|---|---|
| `0x80041C88` | opcode `0xBE` | `(b2<<8)\|b1` |
| `0x8003FAB0` | opcode `0x75` | `b1` |
| `0x8007C0B0` | C76, le pilier | 25 |
| `0x8006FCAC` | C39, native | 0x55 |
| `0x800705CC` | C40, native | 0x55 |
| `0x80076C60` | C49, native | 0x62 |
| `0x80076CD4` | C49, native | 0x62 |

C39 et C40 arrêtent le grondement 0x55 en fin de compteur ; C49 arrête le 0x62. Ni l'un ni l'autre n'est enregistré dans la DLL (thème IA ennemis), mais l'API leur servira.

**L'écart avec la décompilation** (FAIT) :
- `Script_190_0BE` (`EntityEventHandlers.cs:3627-3632`) copie `0xBD` : `PlaySoundEffect`.
- `Script_117_075` (`EntityEventHandlers.cs:2208-2212`) : `PlaySoundEffect(variables[1])`.
- La fin de C76 (`FunctionTypeC.cs:7189-7192`) : `PlaySoundEffect(0x19)`.
- Sa `StopSoundEffect` est fidèle, à deux détails près : pas de test `drapeaux & 2`, et la serrure `g_voiceCommandLock` testée en tête au lieu de l'être dans le key-off. Aucun des deux n'est observable côté jeu.

## 2. DLL au HEAD et moteur

**`ISoundPlayer`** : `PlaySfx`, `RemixVoice`, `FlushFrameSounds` (vide la table anti-doublon une fois par image rendue), `StopAllSfx`. Pas d'arrêt d'un son précis.

**`SoundPlayer`** (`Alundra/Audio/SoundPlayer.cs`) :
- porte la table anti-doublon (64 emplacements) ;
- un état « armé » (`BgmFadeDirector.IsArmed`) qui bloque `PlaySfx` ;
- le plafond de voix `MaxVoices`, compté par vab demandé ;
- `_liveVoicesBySfxId`, rangé par id demandé, avec une poignée par voix.

C'est la même clé que la voix du binaire (`+0x18` vaut l'id demandé, avant redirection), donc `StopSfx(id)` retrouve exactement les voix que `Stop(id)` retrouve.

**Moteur** (`CasaEngineMonogame/CasaEngine/Framework/Audio/AudioService.cs`, lecture seule) :
- `Stop(handle)` : arrêt net, la poignée périmée est ignorée.
- `StopWithFade(handle, secondes)`.
- `StopVoicesOwnedBy(owner)`.
- `IsAlive`.

On peut donc arrêter par voix. Aucune branche du moteur n'est nécessaire.

**Manques du moteur à consigner** (ne pas contourner) :
- **G1.** Pas de key-off avec relâchement ADSR par tonalité. Le relâchement (`ADSR2 & 0x1F`, bit 5 = mode exponentiel) n'est pas dans `sfx.json` ni dans `Sounds/sfx-manifest.json`. Même `StopWithFade` n'a donc pas de durée à utiliser.
- **G2 (déjà connu, hors g5c).** `StereoVoiceMixer.WriteFrames` reboucle à l'échantillon 0 sur tout le clip, sans la fenêtre `loop_start`/`loop_end`. Cela touche le caractère de la boucle, pas son arrêt.

**Le relâchement mesuré** (`G5C/vab_adsr.py`, `vab_release_all.py`, `vab_release_all_out.txt`). J'ai lu le vab depuis `SOUND.BIN` ; la section d'un `vab_id` k est la (k+1)-ième `pBAV`, validée par volume et pan sur chaque enregistrement. Temps de chute linéaire à pleine échelle, ou constante de temps en exponentiel :

| Id | Sites `0xBE` | Relâchement |
|---|---|---|
| 25 | 204 | 0,186 s |
| 85 | 54 | 0,743 s |
| 353 | 31 | 0,046 à 0,093 s |
| 229 | 17 | 0,186 s |
| 341 | 6 | 0,006 s |
| 349 | 6 | 0,372 s |
| 360 | 6 | 0,093 s |
| 316 | 5 | 0,093 s |

Les sons non bouclés à long relâchement (123, 232, 64, 55, 102) sont presque toujours déjà finis quand le `0xBE` arrive : sans objet audible.

**Pourquoi le raclement ne finit jamais** (FAIT + MESURÉ) :
- Le rang 25 est `repeat: true`, `max_voices: 1`.
- `StartVoice` passe `tone.Repeat` à `AudioVoiceParameters`, et une voix bouclée du `StereoVoiceMixer` ne finit pas toute seule.
- `PlaySfx(25)` (donc le `0xBE` actuel) est refusé tant que la voix vit. Le test existant `PlaySfx_Id302_MaxVoicesOne_RefusesSecondRequestWhileVoicesLive` en est la preuve.
- Seuls un `StopAllSfx`, un changement de musique ou la sortie de la carte (`StopVoicesOwnedBy`) la coupent.

## 3. Les sites

Recensement (`G5C/sites_stop.py`, `sites_stop.json`, `sites_stop_summary.txt`). Sur les 483 cartes exportées, il **reproduit exactement** les chiffres du rapport g5 :
- 392 sites `0xBE` sur 119 cartes ;
- 43 ids distincts ;
- 12 sites sur la chaîne d'histoire (4 cartes : 10 ×2, 135 ×3, 178 ×1, 391 ×6).

La répartition par id est dans `sites_stop_summary.txt` (25 : 204, 85 : 54, 353 : 31, 229 : 17…).

| Quoi | Résultat |
|---|---|
| `0xBD` | 3312 sites sur 310 cartes |
| `0x75` et `0x12` | **0 site** dans les programmes d'événements |
| `0xBE` par emplacement | C 251, B 140, A 1 |
| `0xBE` précédé d'un `0xBD` du même id dans le même programme | 327 |
| `0xBE` sans `0xBD` correspondant | 65 (surtout 229 ×17, 353 ×12, 25 ×10, 85 ×6) |
| Cartes avec un `0xBD` 25 / avec un `0xBE` 25 | 74 / 75 |

**Ce que fait la DLL aujourd'hui par id** (`classify_sites.py`, `classify_sites_out.txt`) :

| Nature de l'id | Ids | Sites | Effet actuel |
|---|---|---|---|
| Boucle | 29 | 371 | `PlaySfx` refusé, la boucle continue |
| Une seule fois | 9 | 16 | rejoué à la fin |
| Sans tonalité | 5 | 5 | rien |

Les 12 sites de la chaîne visent tous des sons bouclés.

**Les quatre familles de g5** (`families_sites_out.txt`) : union de 23 cartes, 85 sites `0xBE` sur 18 d'entre elles (60 sont des 25). Détail par famille :
- piliers : 30 sites ;
- statues : 32 ;
- blocs : 27 ;
- boutons : 8.

**Animations et ids arrêtés** : seuls 3 jeux d'animation (ids 226 et 229) portent un son de changement d'animation qui est un id arrêté ; aucun ne porte 25 ni 85.

## 4. Tests qui bougent

**Mesuré** (`G5C/cmp_trx.py`, `cmp_trx_out.txt`, `G5C/trx2/base.trx`, `wired.trx`) :
- Base : 5913 tests, 5878 passés, 35 en échec. Les 35 sont des échecs d'environnement de la copie (données non suivies par git : `MarkedEntityGroundSlopeTests` 21, `WarpDepartureTests` 7, `ChargedArrowLifeTests` 4, `MusicPlayerTests` 2, `ArrowLifeTests` 1). Ils sont identiques dans les deux passes.
- Variante câblée : 5918 tests (mes 5 sondes de plus), 37 en échec, soit les 35 plus 2.
- Le câblage : `0xBE` et `0x75` appellent un `StopSfx` par défaut qui ne fait rien, avec `SoundPlayer.StopSfx` réel et les enregistreurs de test qui ignorent le stop. C'est la mesure du « tout ce qui épingle un rejeu ».
- **Aucun arc ne bouge.** Les arcs des leviers (cartes 44, 115-117), ceux de la chaîne (10, 135, 178), l'intro 389, `AnimationSoundTests`, `ScriptedPush*` et `ButtonAndBlockSitesTests` restent verts. Aucun test de données non plus (`Data/` n'épingle pas `0xBE`).

**Liste prédite** :

| Fichier | Nature |
|---|---|
| `Alundra.Tests/Events/EventProgramRunnerTests.cs` : `PlaySound2Bis_0xBE_Implemented_SameTwoByteDerivationAs0xBD` (ligne ~3182) | attend `Requests [301]`, obtient `[]` : à réécrire en `StopRequests [301]`, plus un jumeau pour `0xBD` |
| `EventProgramRunnerTests.cs` : `PlaySoundEffect_0x75_Implemented_DerivesIdFromSingleByte_NotTwo` | attend `[42]` : idem avec `StopRequests` |
| `EventProgramRunnerTests.FakeSoundPlayer` | gagne `StopRequests` et `StopSfx` |
| `Alundra.Tests/Ai/Objects/PillarRun.cs` (lignes 54 et 97) et `PillarAiGoldenTests.cs` (lignes 87-94) | **compilation** : ils utilisent `PillarAi.StopSoundStubCallsForTests`, qui disparaît |
| `Alundra.Tests/Support/RecordingSoundPlayer.cs` | gagne `StopRequests` |
| Commentaires : `PillarAiWorld26Tests.cs:187`, `ButtonAndBlockSitesTests.cs:111`, docs de `ISoundPlayer` et `IEntityWorldContext` | documentation seule |

**Prototype du pilier** (`prototype_pillar_wiring.diff`). Les 52 tests `Pillar*` restent verts avec :
- `context.SoundPlayer?.StopSfx(PushSfx)` à la place du bouchon ;
- `PillarRun.Stops` lu depuis `Sounds.StopRequests` ;
- le golden lu depuis `sound.StopRequests`.

## 5. Le bouchon de C76

**Où le brancher.** Dans `Alundra/Ai/Objects/PillarAi.cs`, `Tick`, ligne 108-111, `StopSoundEffect(PushSfx)` devient :
```
context.SoundPlayer?.StopSfx(PushSfx);
```
- La condition est déjà la bonne : `step.StopSound` vaut vrai quand l'animation valait 1, et l'ordre est déjà celui du binaire (arrêt avant remise à zéro).
- À supprimer : `StopSoundStubCallsForTests` (champ `[ThreadStatic]`) et la méthode privée `StopSoundEffect`.
- Les commentaires de `PillarAi.cs` sont à réécrire. Le texte « le son de la poussée est court » est faux : le son 25 boucle.

## 6. Ce qui s'entend en jeu après le port

(MESURÉ pour le pilier et le bouton, `G5C/G5cProbeTests.cs.txt`, `G5cWorldProbeTests.cs.txt`.)
1. **Le raclement de pierre (id 25) s'arrête à la fin de chaque mouvement** : la glissade d'une statue, d'un bloc ou d'un bouton, la poussée d'un pilier. Aujourd'hui il tourne en boucle jusqu'à ce qu'on quitte la salle.
   - Pilier de la carte 26 : stop à l'image 103 après relâchement ; le son n'était ni arrêté avant, ni audible dans les 32 premières images.
   - Bouton de Torla : la voix est vivante à l'image 214, coupée à l'image 216.
2. **Les 16 sites sur sons non bouclés** perdent leur rejeu de fin. S'il joue encore, le son est coupé.
3. **Les 12 sites de la chaîne** (cartes 10, 135, 178, 391) rendent aussi audible la fin de leurs sons bouclés. La carte 391 enchaîne `BE 306, 85, 57` puis `BD 306, 85, 57` ; l'arrêt-puis-jeu libère le plafond de voix et fait donc rejouer chaque son, comme l'original.
4. **L'arrêt est net**, sans l'extinction de 0,19 s (raclement 25) à 0,74 s (son 85) de l'original. Si on l'entend comme un clic, il faudra une tranche de plus : exporter le relâchement par tonalité (extracteur du sous-module, convertisseur) et appeler `StopWithFade`.
5. **Ce qui peut surprendre, fidèle à l'original :**
   - **Un seul son 25 à la fois** (plafond de voix 1). Quand deux objets le demandent, le deuxième se déplace en silence, et l'arrêt du premier coupe aussi le sien (prototype `Prototype_TwoObjectsOneSound_ASingleStopSilencesBoth_LikeTheOriginal`). La carte 51, avec 20 statues, en est l'exemple le plus exposé.
   - **Arrêt puis jeu dans la même image :** le jeu est refusé (anti-doublon) si l'id a déjà été demandé dans cette image.
   - **Un objet de la carte et une animation** qui portent le même id (226, 229) se coupent mutuellement.

## 7. Découpage proposé pour g5c

**Ce qu'il faut poser.**
- `ISoundPlayer.StopSfx(int sfxId)` en **membre par défaut qui ne fait rien**, comme le précédent d'`IEntityWorldContext` ; `SoundPlayer` et les enregistreurs qui l'observent le surchargent.
- Raison : l'interface a **25 implémenteurs** au HEAD (1 de production, 24 de test), dont `BreakRun.Sounds` de l'executor g2b ; un membre abstrait casserait leur compilation.
- Sémantique de `StopSfx` : arrête toutes les voix de l'id **demandé**, libère le plafond, ne dépend pas de `IsArmed`, ne touche pas l'anti-doublon.

**Tâches.**
- **T0** (docs, annexe `docs/plan-e20-annexe/e20-g5/g5c/`) : relecture au HEAD de départ (sites 392/119/12, les 2 tests, 25 implémenteurs), rejeu de l'oracle et des goldens (`stop_oracle.py`, `handlers_oracle.py`), base 5913 tests.
- **T1, phase A** (tests d'abord, rouges sur un squelette qui compile : `StopSfx` vide, opcodes encore en `PlaySfx`) :
  - `SoundPlayer` : arrête les voix de l'id, pas les autres ; les deux tonalités du son 85 ; le plafond libéré à l'image suivante ; Play après Stop dans la même image refusé ; no-op pour l'id 0, l'id inconnu et la voix déjà finie ; non bloqué par `IsArmed` ; redirection par `RefSfxId` ; un seul `Stop` coupe le son d'un autre objet.
  - Dispatch : `0xBE` → `StopSfx((b2<<8)|b1)` avec `b2 != 0`, `0x75` → `StopSfx(b1)`, `0xBD` et `0x12` inchangés, dégradé sans lecteur.
  - Pilier : l'arrêt à la fin de la poussée, une fois, avec 25.
  - Statique : un test lit les 119 cartes et compte 392 sites `0xBE`, 0 `0x75`.
- **T2, phase B** (verte) :
  - `SoundPlayer.StopSfx` ;
  - `case 0xBE` et `case 0x75` ;
  - `PillarAi`, bouchon retiré ;
  - tests déplacés (section 4) ;
  - tests de monde avec le vrai `SoundPlayer` sur `FakeAudioBackend` : pilier de la carte 26 (voix vivante pendant la poussée, arrêtée au relâchement, image 103) et bouton de Torla (carte 426, `BE` à l'image 215, `alive@214 = 1`, `alive@216 = 0`) ; les deux sondes sont dans les fichiers `.txt` ci-dessus.
- **T3, mutants et suites.** Mutants proposés :
  - `0xBE` encore en `PlaySfx` ;
  - `0xBE` en `StopAllSfx` ;
  - `StopSfx` qui coupe aussi les autres ids ;
  - `StopSfx` qui ne vide pas la liste (le plafond reste plein) ;
  - une seule tonalité arrêtée ;
  - id 8 bits pour `0xBE` ;
  - `StopSfx` conditionné par `IsArmed` ;
  - `StopSfx` qui enregistre l'id dans l'anti-doublon ;
  - le `0x75` oublié ;
  - C76 qui arrête sans condition, qui n'arrête jamais, ou avec un autre id.
  Puis les suites Release (copie neutralisée) et Debug en dernier, `cmp` de la DLL, `check_names.py --rules`. Ensuite l'ADR (qui complète l'ADR-0086, numéro libre au moment de l'écrire) et les manques G1 et G2 au rapport des manques du moteur.
- **T4.** Verifiers frais, l'un sur la tranche et l'autre sur les épingles.
- **Recettes de l'auteur** (non bloquantes) :
  - `92-ancien-sanctuaire-bug-pillier.sav` : pousser le pilier, il s'arrête au relâchement ;
  - `234-torla.sav` : pousser le bouton, le raclement finit avec la glissade ;
  - `280-murgg.sav` : la statue de la carte 2.
  Écouter aussi le raclement en boucle avant (DLL de `main`) et après.

**Moteur : pas touché.** Le seul vrai besoin de moteur est G1, hors g5c.

**Arrêts à prévoir :**
- un test existant hors de la liste prédite ;
- un recensement qui ne retrouve pas 392/119/12 ;
- un hang ;
- un besoin de changer le moteur ou les données.

**Ordre.**
- g5c avant g5d, pour que les arcs des statues (cartes 2 et 352) épinglent le bon son.
- Fichiers partagés avec l'executor g2b : `EventProgramRunner.cs` (régions distinctes) et les implémenteurs de `ISoundPlayer` (couverts par le membre par défaut).

## 8. Questions

**Pour le plan (défaut proposé) :**
- **Q-G5C-1.** Inclure `0x75` : même défaut dans le binaire, 1 ligne, 0 site. Défaut : oui.
- **Q-G5C-2.** `StopSfx` en membre d'interface par défaut plutôt qu'abstrait. Défaut : oui.
- **Q-G5C-3.** Arrêt net par `AudioService.Stop`. Défaut : oui, avec G1 consigné.
- **Q-G5C-4.** Où écrire le rapport des manques du moteur. Défaut : un fichier dans l'annexe `e20-g5/g5c/`, car le dépôt n'a pas de fichier unique pour cela.
- **Q-G5C-5.** Corriger le texte du plan (§1.24.6) et l'ADR-0086 : le son du pilier n'est pas « court », il boucle ; et « la DLL rejoue » est faux pour 371 des 392 sites.
- **Q-G5C-6.** Les natives C39, C40 et C49 (arrêts de 0x55 et 0x62) restent hors g5c. Défaut : à noter au plan du thème IA ennemis.
- **Q-G5C-7.** Les libellés de `EventOpcodeSizeTable.cs` (« Play sound 2 (bis) », « Play sound effect ») viennent de la décompilation, qu'on ne corrige pas. Défaut : les laisser, corriger seulement les commentaires du code.

**Pour l'auteur, en termes de jeu :**
- **QA-1.** Après le correctif, le raclement de pierre s'arrête net à la fin du mouvement. L'original le laisse mourir sur environ 0,2 s, et les autres sons de 0,01 à 0,7 s. Garder l'arrêt net, ou ajouter plus tard une extinction fidèle ? Il faudrait lire le temps d'extinction dans les données du jeu, soit un chantier de plus (export puis DLL). Défaut : arrêt net.
- **QA-2.** Quand deux objets demandent le raclement en même temps, un seul son joue (le deuxième objet bouge en silence), et il se coupe dès que le premier objet s'arrête, comme dans l'original. On garde ce comportement ? Défaut : oui, fidèle.
- **Info.** Le pilier livré par g5a laisse aujourd'hui son raclement en boucle jusqu'à la sortie de la salle ; g5c le corrige.

## 9. Fichiers (tous sous `G5C/`)

**Binaire, rejeu**
- `stop_oracle.py`, `stop_oracle_out.txt`, `stop_oracle_mut_out.txt`, `stop_goldens.json`
- `handlers_oracle.py`, `handlers_oracle_out.txt`
- Listings : `dis_stop_800.txt`, `dis_play_800490fc.txt`, `dis_sound_core.txt`, `dis_keyoff.txt`, `dis_80091134.txt`
- Outils : `disasm_range.py`, `callers.py`, `refs.py`, `tab.py`, `lib.py`, `work/` (la machine copiée)

**Données**
- `sites_stop.py`, `sites_stop.json`, `sites_stop_summary.txt`
- `classify_sites.py`, `classify_sites_out.txt`, `families_sites_out.txt`
- `vab_adsr.py`, `vab_release_all.py`, `vab_release_all_out.txt`, `decode_events.py`

**DLL, sondes**
- `prototype_stopsfx.diff`, `prototype_pillar_wiring.diff`
- `G5cProbeTests.cs.txt`, `G5cWorldProbeTests.cs.txt`
- `cmp_trx.py`, `cmp_trx_out.txt`, `trx2/base.trx`, `trx2/wired.trx`, `probe_build_base.log`, `probe2_build.log`, `probe2_test_wired.log`
- `make_probe_dst.ps1`, `extract_docs.ps1`
- `probe/`, `probe2/`

**Notes sur le dossier**
- `trx1-partial-docs-unreliable/` est la première passe, lancée avant que la copie reçoive tous les documents : non fiable.
- Ne jamais supprimer `probe/` ni `probe2/` récursivement : leurs sous-dossiers `CasaEngineMonogame` et `alundra-project` sont des jonctions vers `g5b-disc/probe/`.

**Reproduire**
- Machine : `python -B -X utf8 stop_oracle.py 3000 mut gold`, puis `python -B -X utf8 handlers_oracle.py 400`. Sans `-I` : capstone est dans le site-packages utilisateur.
- DLL : dans `probe2/`, `dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug -p:CustomAfterMicrosoftCommonTargets=<scratchpad>/skipcopy.targets`, puis `dotnet test --no-build --blame-hang-timeout 8m`.