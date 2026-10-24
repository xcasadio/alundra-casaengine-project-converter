RAPPORT DU CRITIQUE (step-search-disc). Lecture seule respectée : aucun build ni test, rien écrit dans le dépôt, aucun `git` qui écrit. Mes scripts et sorties sont sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/step-search-disc/critic/` (`work/`, `out/`). Désigné `CR/` plus bas.

VERDICTS
- S1 (algorithme et mesures) : CONFIRMÉ.
- S2 (moteur et DLL) : CONFIRMÉ, avec des omissions.
- S3 : le diagnostic « racine float32 » est CONFIRMÉ et maintenant chiffré. La recommandation C est NON ÉTABLIE telle quelle, avec des risques absents et une option manquante (D).
- S4 : deux formulations sont fausses ou contredites par le rapport lui-même.

S1 : re-dérivé
- J'ai relu le listing `0x80037730` moi-même (`CR/work/crit_emu2.py`) :
  - la boucle est conjointe sur X et Y ;
  - un refus restaure la position et divise par `sra 1` (−1 donne 0) ;
  - un pas accepté part de la nouvelle position ;
  - le retour est `[sp+0x34]`, soit `s4` en tête de la dernière tentative ;
  - `ForceAdjusted` n'est levé que si `[sp+0x2C]==0` ;
  - la branche sol laisse `s4` intact quand le Z accroché est libre.
- Scan de l'EXE (`CR/work/callers.py`) : le seul appelant est `0x80037F00`, dans `MoveEntity` `0x80037E34`, qui range `v0` en `+0x130`. Pour une entité portée (`+0x28`), `MoveEntity` n'appelle pas `ComputeXY`, il recopie la position du porteur. Le rapport ne le dit pas.
- Rejeu du fuzz du lecteur sur des copies, 2 graines neuves (301 : 1500 génériques ; 302 : 1500 ciblés) : 0 écart, 449/449 instructions de `ComputeXY`. Les 14 mutants sont retués (5 min, `CR/out/xymut_rerun.txt`).
- Second interpréteur (`emu_g1a`, autre code que `mips.py`) et scénarios écrits par moi, sans `xyscen` ni `xymodel` : les nombres du rapport tombent à l'unité près (`CR/out/crit_emu2.txt`) :
  - déficit à l'est, pas 2,4375 px : {0:852, 1:105, 2:54, 3:28}, premier `ForceAdjusted` tick 2:852, tick 3:187 ;
  - pas 3 px et 6 px : {0:692, 1:347} ;
  - contact caisse au tick 1, air ou appui : 0,408 (2,4375 px), 0,501 (3 px), 0,340 (6 px) ; sol : 1,000 ;
  - ouest : déficit 0 sur 60 départs ; sud : 0 à 3 (`crit_emu2_dirs.py`).
- Le modèle égale les valeurs brutes de l'oracle indépendant de t2-proj (`crit_sweep.txt`, `crit_rawcheck.py`) : 10/10, dont 7143423 pour x0=107 et `adj@2`. C'est plus probant que `studyF`, dont les xS sont en pixels à 3 décimales et ne distinguent pas l'unité brute (tolérance 0,001 px = 65 unités). Les 12 clés d'E7/C7 : 12/12 relancé.
- Détails à corriger :
  - la diagonale 56473 a un déficit maximal de 8 dans `studyA.txt` (le rapport dit « jusqu'à 6 »).
  - les drapeaux de coins sont remis à 0 à chaque tentative, donc le glissement lit ceux de la dernière tentative, pas ceux de « la dernière qui a atteint le mur ».
  - 18 à 19 tentatives valent pour un épisode ; un glissement relancé peut en ajouter autant.

S2 : relu dans le code
- Confirmé : `Move` (:438), `MoveWithCollisions` (:1045, porte :1056-1061 sans test), `TryResolveCollisionDependencies` (:1601), résolveur axe par axe h1 puis h2 depuis la position avancée, bissection de 24 itérations plus 4 retraits d'un ULP (:1231), contact `H2Obstacle ?? H1Obstacle`. Côté DLL : `MoveControllerAndPullPosition` (:2642), seuil de `ForceAdjusted` de 655 unités (:2791), `PosX = round(root*65536)`.
- Omissions :
  - Après l'étage champ, le moteur enchaîne `Sweep`, `TryStepMove` et `Slide` avec `SkinWidth`. Le rapport n'en parle pas. Il est probablement inerte (le commentaire F3 de `PullRootPose` dit que le moteur ne voit pas les boîtes d'entités), mais c'est NON ÉTABLI.
  - La DLL module déjà `StepHeight` (`ScriptedMotion.cs:415` et `:551`) pour approcher la fenêtre de 3 px. L'écart 7 est donc déjà partiellement traité.
  - `PullRootPose` re-tire `PosX` et `PosY` à chaque image (`EntityScriptProxy.cs:1597`), en plus du site `:2669`.
  - `SlideAlongWall` réécrit `ForceAdjusted`.
  - O-E19-28 (d)(e) et O-E19-42 (`plan-e19-opcodes.md:13254` et `:13268`) consignaient déjà « 1 à 3 unités avant à l'est et au sud », « `ForceAdjusted` 0 à 2 ticks plus tôt » et « `XCollisionEntity` à 0 dans la moitié des ticks raccourcis ». Le rapport les re-dérive et les chiffre sans le dire. Il range aussi la « division conjointe » sous O-E19-28 alors que c'est O-E19-42.

S3 : est-ce qu'une vérité entière est nécessaire ?
- Expérience (`CR/work/crit_f32truth_repr.py`, modèle déjà validé). Deux déplaceurs rejouent les mêmes ticks contre un mur à l'est : l'un en vérité entière, l'autre en float32 relu par `round(root*65536)`. On ne compare que les départs représentables en float32.
  - À 229 px : 0 écart.
  - À 349 px et 709 px : le premier `ForceAdjusted` diffère d'un tick (float32 un tick plus tôt) dans 10 à 37 % des phases : 33 % pour 3 px, 37 % pour 6 px, 10 à 22 % pour 2,4375 px (peu d'échantillons).
  - Donc A et B ne peuvent pas être exacts au tick au-delà de 256 px : MESURÉ. Le rapport ne chiffrait que « 1 unité non représentable ».
- Nuance : B (« résolveur tenu par le jeu ») et C se rejoignent dès que la vérité entière vit dans la DLL. La vraie question est où loge la boucle, pas l'exactitude.
- Risques de C absents du rapport :
  1. Suivi de la racine. `Move` re-teste avec les prédicats du moteur (règle de hauteur différente) et peut refuser un pas accepté par la boucle. `Teleport(Vector3)` est exact mais appelle `Stop()` (vitesse et verrou vertical remis à zéro). `PushLogicalPositionToRoot` tronque au pixel. Il faut une API moteur de pose exacte sans effet de bord, donc un plan et une ADR moteur.
  2. Les re-tirages XY de `PullRootPose` et `:2669` écrasent la vérité entière. Précédent identique pour `PosZ` (effondrement 26214401 → 26214400, gardes `WasEntitySupportedLastTick` et `HasLogicalZ`). Il faut une garde analogue.
  3. D-E0K-4 / O-F3-2 (« X et Y restent rapatriés ») sont à renverser et manquent à la liste du §4.3.
  4. La promesse « exact au tick » reste bornée par O-E19-28 (e)(f) : liste des collidables avec un tick de retard, ordre de mise à jour par entité. Les contacts entre entités mobiles resteront approximatifs.
  5. « 3 à 4 tranches » : NON ÉTABLI. La phase héros (air, échelle, support, porteurs) pèse plus que le reste.
- Option D, manquée : une « ombre de contact seul ». Le moteur garde son déplaceur à contact exact. La DLL calcule le `+0x130` du binaire avec une copie pure de `ComputeXY` depuis sa position d'avant le pas, puis écrit `XCollisionEntity`. Mesuré avec le modèle, `CR/work/crit_shadow.py`, INFÉRENCE pour le jeu réel :
  - Désaccord de contact au tick 1 en mode appui, DLL telle quelle : 59 % (2,4375 px), 50 % (3 px), 66 % (6 px). Avec l'ombre : 0 %.
  - Reste 7,8 % au tick 2 au pas de marche 2,4375 px (les positions divergent du déficit).
  - Mode sol : déjà 0 %.
  - Elle ferme le tick 1 d'O-G3-3 sans toucher D-E19-58 ni D-E20-38. Elle ne règle ni `ForceAdjusted` (W4.5) ni O-E20-24.
  - C'est une règle de jeu, pas un défaut moteur : à arbitrer avec la règle « un défaut se corrige dans le moteur ».
- O-E20-24 : le diagnostic « `Move` non testé avant intégration » est CONFIRMÉ (`:1056-1061`, `ReadCollisionField` :1526). Le clamp du tick de naissance doit porter la boucle entière et lever le drapeau explicitement (accord avec le critique t2).

S4 : formulations à corriger
- Q-STEP-1 se contredit : « Alundra qui marche : Carré au tick d'arrivée peut soulever la caisse voisine ». Le §3.2 du rapport (et mon rejeu) donne un contact identique au sol (1,000), et `PlayerTryActionCore` (`PlayerManager.cs:1015`) ne lit que `XCollisionEntity`. Le cas ne vaut que pour un héros sur une caisse ou en l'air (O-G3-3). Pour le héros au sol il reste `ForceAdjusted` +1 tick (18 à 33 %) et 0 à 3 unités, invisibles.
- Q-STEP-3 est imprécise : contre une entité le binaire ne glisse pas. `s4 != 0` mène à `ForceAdjusted=1` sans table (`0x800379D0`) : l'original « s'arrête net », comme O-E19-42 le dit. La table ne vaut que pour les coins de cellules.
- Les « défauts comme l'original » renversent des réponses explicites de l'auteur (D-E19-58, D-E20-38, Q-H4C1-5, Q-G3-d) : à poser comme question, pas à appliquer par défaut.

NON ÉTABLI, ou non refait
- Le modèle float32 du moteur (déficit 0) n'a pas été remesuré sur le vrai moteur.
- Le recensement des tests est statique : 68 fichiers lisent les champs (le rapport dit ~70), liste des arcs probablement minorée (`FlailBounceArcTests`, `Ship*`, `MurutaHallArcTests`, `BergusJumpArcTests` non cités). Les traces `hero-trace-389-*` sont des goldens de la DLL (`HeroTraceHarnessTests.cs:702`), pas des clés du binaire : leur régénération est cohérente.
- Je n'ai pas relu ligne à ligne chacune des 32 entrées de la table de glissement : couvertes par le rejeu (couverture d'instructions complète), pas par lecture.