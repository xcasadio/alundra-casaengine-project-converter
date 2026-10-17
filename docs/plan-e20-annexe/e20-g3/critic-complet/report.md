# Critique de complétude des découvertes disc-e20g3 : g3 (solidité des soulevables) et g4 (barricade de la 392)

J'ai travaillé en lecture seule. Mes scripts et leurs sorties sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g3/critic-complet/` (fichiers `c1_*` à `c11_*`, copies inchangées des scripts de la découverte DLL dans `dllcopy/`), tous lancés avec `python -B -I`. Je n'ai pas écrit de `report.md` : l'environnement interdit à un sous-agent d'écrire un fichier de rapport. Ce message est le rapport.

Le HEAD était `600b938c` au départ et `b6a2f921` à la fin. Entre les deux, seul `docs/plan-e20-heros.md` a changé. Le §1.24 n'a pas bougé ; le tableau des points ouverts s'est décalé (O-E20-5 en l. 7002, O-E20-21/22 et Q-G1A-* en l. 7019-7021). Aucune source de la DLL n'a changé depuis le `c84fd72` du relevé DLL (`git diff --name-only`).

---

## Confirmé

1. **Le seul site de déviation.** C'est `Alundra/Physics/MovementObstacleProbe.cs:48-51`, qui passe `EntityFlags.PickupKindMask`. Le binaire n'a aucune règle propre aux soulevables (relevé binaire A1.2). g3 se réduit donc, côté code, au retrait de cet argument et aux textes qui en dépendent.
2. **Le contact `XCollisionEntity`.** Son seul écrivain est `EntityScriptProxy.cs:2424-2431` (relevé DLL B1.2). Tant que le masque existe, le contact ne peut jamais être un soulevable. D-E20-39 (plan l. 173-175) supprime le détecteur provisoire. Conséquence : g1b dépend de g3.
3. **La « liste prédite » du relevé DLL, recoupée avec le code.**
   - `TRLift` (`AlundraMovementObstacleProbeTests.cs:338-364`) passe aujourd'hui à travers la caisse (160f, l. 352). Il deviendra rouge.
   - `TR1_SkippedCandidateFlags` (l. 125-133) et `TH4` (`AlundraEntityContactArcTests.cs:484-485`, la l. 484 est déjà la garde stricte) restent verts.
   - Le miroir de `AlundraDialogueOpcodesProductionTests.cs:383-388` reste vert, sur la 389 qui n'a aucun soulevable.
   - Le commentaire de A5/A5r devient faux : `AlundraShipCorridorArcTests.cs:22-23` dit « the crates … do not block in the DLL, a known gap kept for E14 ».
   - Recoupement indépendant (`c1_maps_tests.out`) : j'ai listé tous les fichiers de test qui nomment une carte à soulevables. Les noms que le relevé DLL n'avait pas sont des faux positifs (`"D1-24"`, `"TN-3"`, `"UJ-7"`, des noms factices de `AlundraMusicPlayerTests`, une chaîne de commentaire 224), des tests qui ne lisent que les octets d'événements (`MapEventProgramLoader` sur 27, 28, 140, 164, 167, 411, 445), un fond (159), ou TN-3 sur la 346. Sur la 346, la seule cruche (rec25) ne naît qu'au script et se trouve à 183 px du trajet (`c2_346.out`). **Aucun test rouge n'a été manqué.**
4. **Les six animations « hors collisions » du héros** (ids 32-36 et 39) :
   - la DLL lit l'octet `0xD` dans le champ exporté `Acceleration` (`Maps/Loop/FrameSyncPasses.cs:153-160`) ;
   - pour le héros `192c2eeb`, l'export donne 208, 208, 212, 209, 208, 208 (lu dans `sprite-records.json`), soit `0xD0`, `0xD0`, `0xD4`, `0xD1`, `0xD0`, `0xD0` : les octets mêmes du relevé binaire A1.5.
5. **Les 219 soulevables de la chaîne** (18 cartes) naissent tous au chargement de la carte (`c6_spawn_gate.out`).
6. **Les conventions Z aux bords plats.**
   - Dans la DLL, les caisses sont à `Height*8` exactement (`AlundraAbsoluteZWritesTests.cs:99-108` : 4194304 puis 5242880).
   - Le héros posé sur un dessus d'objet est au sommet exact (`AlundraHeroObjectTopsTests.cs:168` : 1048576 sur un objet de 16 px).
   - INFERENCE : debout sur rec5, le héros affleure rec6 et recouvre rec10, comme dans le binaire. Le contact par le dessus vaut donc rec10, comme dans la route R7 (A3.4).
7. **Le dessus d'objet, le plafond et le calage** comptent déjà les soulevables (relevé DLL B1.5, `EntitySupport.cs:109-340`). Le saut sur une caisse existe donc déjà dans la DLL.

## Réfuté ou corrigé

1. **« Les deux routes sont fermées de bout en bout sur le vrai code » (relevé binaire A3.5) ne vaut que pour la physique, sans le roulis du navire.**
   - Le monde de la 392 n'exécute aucun programme de carte (`bin/lib392.py:8-9` : seuls `MovePlayer`, le spawner, C23 et E2 sont réels). La route aligne Y en comptant 49 ticks de « bas » (`a3_fullroute.py`, phase `down`).
   - Or B1 de la 392 secoue le héros (`c5_392_B1.out`) :
     - `@105 0x37 [15]` attend 15 images ;
     - `@107 0x2F [0,240,0]` teste si une direction est tenue ;
     - `@114 0x70` teste si le héros est au sol (`EventProgramRunner.cs:1782-1787`) ;
     - `@118 0x10` verrouille le contrôle ;
     - `@119 0x0C` tire une direction cardinale au hasard (`:620-623`) ;
     - `@120 0x08 [2]` ajoute 2/32 à la direction (`:603-606`) ;
     - `@122` attend 4 images, `@124 0x11` rend la main, `@125` reboucle sur `@105`.
   - A5r le mesure dans la DLL (`AlundraShipCorridorArcTests.cs:184-242`) : un sondage toutes les 16 images (204 → 220, 225 → 241), puis 5 à 6 ticks poussés par la force (-30576, 102440).
   - INFERENCE : les directions possibles sont 2, 10, 18 et 26. Chaque roulis déplace Y d'au moins 2,4 px environ, donc fait sortir le héros de la fenêtre de 2,0 px, sauf s'il est calé contre le mur nord de la porte (y = 295, relevé binaire A3.1).
   - **La route « compter 49 ticks » n'est pas reproductible en jeu ; seule la route « se caler au mur nord » l'est.**
   - Le verrou du roulis pose le bit `0x04`, qui fait partie du masque `0x34` (`Session/GameState.cs:79,103`). C'est la branche verrouillée qui appelle le mode 1 de `UpdatePlayerCarriedEntity` (plan l. 6308, 6315). INFERENCE : un verrou de 5 ticks du héros incrémente le compteur sans rien écrire ; un 6ᵉ tick ferait descendre l'objet de 2 px. À mesurer pour la route « soulever ».
2. **Deux « à mesurer » du relevé DLL se tranchent sans exécution.**
   - Les statues de la 115 : A115 affirme `EntityByRecord(0)` et `(1)` nuls (`AlundraLeverArcTests.cs:57-58`). Elles n'existent pas dans l'arc.
   - Le Beannoïde de G116 : la carte 116, rec0, a les drapeaux `0x103`. Il n'est pas collisionnable, donc jamais sujet (`EntitySupport.cs:73-76`, relevé binaire A1.2). Les deux arcs sont inchangés.
3. **D-G-7 (`LiftContactEntity`, `requiredCandidateFlags`) est caduque depuis D-E20-39.** Le plan dit encore « E20.g3 (retire la sonde de contact de g1b ; exige g1b et g2b) » (l. 6368). La phase B de g1b, les témoins k1 à k4, les preuves de retrait qui la citent (l. 6572) et O-G-1 (l. 6588) reposent sur elle. **L'arête est inversée : g1b exige g3.**
4. **L'écart de compte 2 252 / 2 251** (g1a, `report.md:252`, « non investigué ») vient de **la carte 231, rec3** (`c7_count_diff.out`).
   - Dans `data-extracted`, l'index 35 pointe l'en-tête local de la cruche (`CanPickup` 35, genre 1).
   - L'export y voit « I05_Arc du chasseur » (banque `alundra_35`, drapeaux `0x230180`, genre 0, collisionnable).
   - g3 n'y change rien (l'objet est déjà solide dans l'export). Ce qu'il est au spawn dans le binaire est INCONNU ; c'est une question de convertisseur.
5. **La prémisse de D-E19-29 est réfutée** pour la liste des collidables. D-E19-29 dit « comme dans le binaire » (plan E19 l. 126-128 ; `EntitySupport.cs:53-55`). Or le relevé binaire A1.7 et `out/a1_destroyed.txt` montrent qu'une caisse au statut 4 reste dans la liste jusqu'à la fin du tick. Le relevé DLL reprend D-E19-29 comme fidèle. Les deux relevés se contredisent sans se répondre. L'impact est d'un tick ; g3 doit corriger ou consigner.
6. **73 soulevables ne naissent pas au chargement** (`SpriteDirection & 0x40` nul, `EntitySpawnFactory.cs:84-99`, `c6_spawn_gate.out`). Les « 2 250 / 2 252 placements » des deux relevés les comptent. Exemple : la pierre très lourde de la carte 3, rec12, que deux arrivées recouvrent en XY (Inconnu 5 du relevé DLL), ne naît qu'au script.
7. **La « fenêtre de 3 px »** (plan E19, vers l. 9572) fait en réalité 2,0 px continus, de [295,0 ; 297,0] (relevé binaire A3.2). Le document est à corriger.

## Manques

**Pour g3**

- **M1 — Le périmètre réel.** Retirer le masque rend solides tous les soulevables collisionnables, pas seulement les caisses et les jarres (`c3_lift_by_name.out`).
  - 248 placements hors chaîne ne sont pas de type C23 : C4 (76 tonneaux, 56 troncs, 27 pierres, 32 « objets ») et C70 (57 torches). Ils ne seront soulevables qu'avec g2c : l'ensemble câblé de D-G-8 ne contient que les 7 types C23.
  - S'ils sont seuls solides, le modèle grossier montre que le saut suffit à atteindre toutes les sorties, sur toutes les cartes (`c9_reach_nonwired.out`).
  - Les 7 pierres très lourdes de la carte 4 (Overworld 0,3) coupent, même avec le saut, de 2 à 12 sorties depuis 7 arrivées (`c9_reach_all.out`). C'est le verrou du Gant de l'original : le genre 3 exige l'objet `0x3B`. C'est un changement visible sur le monde extérieur, que le plan doit écrire.
- **M2 — L'accessibilité de la chaîne après g3.** Aucun relevé ne l'a vérifiée. J'ai écrit un modèle par cases, grossier (INFERENCE), `c8b_reach.out`. Il passe le cas de contrôle : la 392 perd la sortie 0 à pied et la retrouve avec le saut.
  - Sur les 17 autres cartes de la chaîne, aucune sortie n'est perdue, ni à pied ni avec le saut.
  - Il apparaît seulement des poches de sol vides : 173/180 (15 cases derrière les cruches rec5-8, aucun enregistrement dedans), 175/182 (1 case), 174 (2 cases, (46,40) et (47,40), perdues même avec le saut, vides).
  - **Sur la chaîne, seule la 392 exige de franchir une caisse.**
- **M3 — Les PNJ et objets qui se déplacent par script (hors chaîne).**
  - Le balayage du relevé DLL couvrait 8 cartes de la chaîne et les cartes 115 à 117.
  - Sur les 242 cartes : 115 marcheurs d'enregistrement sont à portée statique d'un soulevable (`c10_walkers_near.out`), dont 31 collisionnables avec un recouvrement en Z (`c11_walkers_z.out`).
  - 28 de ces 31 sont hors chaîne : les jours suivants d'Inoa (217, 220, 221, 240, 253, 290, 292, 295, 359, 360, 361, 372), les plateformes de 24, 27 et 441, les Mini-Nilude de 424, et les cartes 147, 115, 117.
  - Sur les 10 cartes de la chaîne sans test (170 à 184), seuls deux déplacements existent (`c4_walkers_untested_chain.out`) :
    - Myra sur 174/181, à 219 px, hors de portée ;
    - la marche de B[1] `@58` sur 174/181 (`0x0B` de 128 px après `0x09 [16]`, `c5_174_B1.out`). INFERENCE (direction 16 lue comme « nord ») : aucune des arrivées de `portal_arrivals.out` n'y croise un soulevable.
  - Il faut une règle de recensement à la D-E19-21 ou D-E19-24 quand ces cartes entreront dans la chaîne.
- **M4 — Des sondes DLL à faire avant le code.** Les relevés donnent les valeurs du binaire ; aucune n'est encore mesurée dans la DLL.
  - L'arrêt du héros à x = 657,000 et 615,000 en y = 296.
  - L'ordre du contact dans une pile (caisse du bas). Dans la DLL, le contact vient de `H2Obstacle ?? H1Obstacle`, et `_collidables` suit l'ordre de `_spawnedEntities` (`WorldProxy.cs:3687-3697`), pas l'ordre des slots du binaire.
  - La sortie d'un chevauchement. Le binaire (`out/a1_overlap.txt`) donne : de 0,5 à 1,2 px, sortie au tick 1 ; de 1,3 à 2,0 px, bloqué puis sortie ; à 5 et 10 px, bloqué pour toujours. La sonde de la DLL ne teste que le point d'arrivée de chaque pas (`IMovementObstacleProbe.cs:14-16`).
  - Un PNJ collisionnable réel qui s'arrête contre une caisse (référence binaire : `a1_scen`).
  - Le moment où le bit `0x80` d'animation prend effet. La DLL le charge à la fin de l'image ; le binaire le charge dans la passe d'animation, après les listes et avant la physique du tick. Il peut y avoir un décalage d'un tick (INCONNU).
- **M5 — Les décisions de conception à écrire.**
  - Remplacer `TRLift` par un test positif « la caisse est un obstacle », accolé au mur à boule de fer.
  - Garder ou retirer `skippedCandidateFlags`, qui a 3 sites d'appel (l. 132, TH4 l. 485, Dialogue l. 388).
  - Corriger les textes (relevé DLL B1.4, point 2).
  - Écrire un ADR qui remplace D-E19-28 et D-E19-46 (ADR-0021 et ADR-0022) et la prémisse de D-E19-23.
- **M6 — La chute de la caisse du haut.** Le binaire la fait tomber de 80 à 64 en 8 ticks (A3.4). Dans la DLL, les entités posées par la carte gardent l'ancienne règle de chute jusqu'à E19.h2 (D-E20-37, l. 169-170). Le marqueur n'est posé que sur l'objet saisi par `PutInHands` (D-G-13), pas sur celui du dessus. Le comportement de la DLL est INCONNU ; aucun relevé n'en parle.
- **M7 — Les sauvegardes de recette.** Il n'en existe aucune sur la 392 ni sur 170-184 (`docs/test-saves.md` : seulement `day3-after-dream` sur la 179 et `day4-meeting` sur la 185).
  - Proposition : `barricade-m392`, carte 392, case (29,13), z 4, drapeau G1641 (repris de l'ArcSpec A5, `AlundraShipCorridorArcTests.cs:32-33` ; B1 replace le héros en (720,216) à l'entrée).
  - Un préréglage par famille : 163/170/177/184, 164/171/178, 165/172/179, 173/180, 174/181, 175/182. Les drapeaux de chaque jour sont INCONNUS.

**Pour g4**

- **M8 — Les arcs de franchissement doivent tourner avec B1.** L'oracle ne sait pas produire de valeurs tick par tick sous le roulis.
  - Il faut une acceptation par résultat : le portail est atteint (`HasPendingArrival`).
  - Il faut un pilote réactif dans `ArcRun` ; les crochets existent (`OnInstruction`, `OnFrame`, l. 99-100). Il se cale au mur nord, puis saute ou soulève dans une fenêtre sans roulis : le sondage tombe toutes les 16 images, et aucun roulis ne part en l'air (`0x70`).
  - Le générateur aléatoire est réinitialisé par l'arc (doc l. 20-21), donc l'arc est déterministe.
- **M9 — Les critères « y = 298 » et « pile »** du relevé binaire (g4, critères b et c) doivent se synchroniser sur B1 : placer le héros juste après `0x11 @124` (15 images libres), ou relâcher les directions au moment du sondage.
- **M10 — Le calage au mur de la porte dans la DLL** est INCONNU (relevé binaire, g4 point 2). Dans le binaire, le héros s'arrête à y = 295,000 et dérive de 0,75 px par tick vers l'est (`out/a3_hug.txt`). Le glissement de la DLL contre les cases existe (`ArcsThatSlide`) ; seul le second pas contre une entité est interdit (`ScriptedMotion.cs:819`).
- **M11 — Garder A5 et A5r** (qui testent B1 et le portail, avec le téléport de D-E19-23) et ajouter de nouveaux arcs de franchissement, hors de `ArcsWithoutEntityContact` puisqu'ils touchent des caisses. Corriger la doc l. 22-23.
- **M12 — La route « soulever »** exige g1b, g2a et g2b : la caisse lancée se brise en x = 588 au tick 88 (`out/a3_r3b.txt`). Sans g2b, elle retomberait et resterait, non solide, puisque C23 efface `0x80`.

## Inconnus

1. Si la machine MIPS peut exécuter le programme de carte B1, ce qui permettrait de mesurer le roulis sur le binaire.
2. Le comportement de la DLL pour M4, M6 et M10.
3. Le contact de la DLL quand deux caisses recouvrent le même pas (ordre de spawn contre ordre des slots, notamment après recyclage).
4. La durée du verrou du roulis dans le binaire (5 ou 6 ticks du héros), qui décide de l'effet du mode 1 sur un objet porté. Dans la DLL, A5r montre 5 ticks verrouillés plus un tick libre (l. 222-226).
5. Ce que devient la carte 231, rec3 au spawn (cruche ou arc).
6. Les directions des 31 marcheurs hors chaîne (non décodées).
7. Que l'entité qui marche dans B[1] `@58` de 174/181 soit le héros (INFERENCE, par analogie avec la 392 où B1 fait marcher le héros).

## Conséquences pour les tranches g3 et g4

**(1) Ce qu'il faut pour écrire g3 et g4 comme tranches exécutables.** M1 à M12, plus trois mises à jour du plan :
- les corrections Réfuté 1, 3, 5 et 7 ;
- la réécriture de g1b sans phase B ;
- l'inversion des prérequis dans le tableau de découpage, à la l. 6368 et dans la ligne 12 de l'enveloppe (l. 723).

**(2) L'ordre.**
- **g1a → g2a → g2b → g3 → g1b tient, et devient obligatoire sur un point : g1b vient après g3**, puisque le seul contact possible est `XCollisionEntity`.
- g3 *peut* précéder g1a : aucune dépendance de code entre eux, car g1a, g2a et g2b posent l'objet par `PutInHands` (plan l. 6387, 6612-6613). Mais il *ne doit pas* le précéder. Cela ouvrirait en jeu une longue période où les caisses sont solides et ne se soulèvent pas.
- Ce que g3 seul change pour le joueur :
  - le blocage, du héros et des PNJ ;
  - plus de glissement d'angle contre une caisse (`ScriptedMotion.cs:819`) ;
  - le verrou d'interaction vidé au contact d'une caisse (`PlayerManager.cs:1806-1810`, fidèle au portage branche par branche) ;
  - le saut sur une caisse est inchangé.
- Sur la chaîne, seule la 392 se ferme, et seulement si le saut échoue dans la DLL. A5 et A5r ne le verraient pas : ils téléportent le héros (l. 166, 212).
- g1b exige toujours g2a : sans lancer, le joueur garde l'objet sans pouvoir le poser (D-E20-41). Avec un objet en main, l'échelle est refusée (`0x80031D54`), les états de portage n'appellent ni l'interaction ni l'attaque, et l'objet n'est avalé qu'après 21 ticks de verrou continu.
- **Recommandation :** sur une seule branche, g1a, g2a, g2b, puis g3 avec l'arc de franchissement par le saut (g4a), puis g1b avec l'arc soulever-lancer (g4b), et une seule fusion. Q-G1A-1 (l. 7021, B ou C) déplacerait cet ordre.

**(3) Les tests et arcs qui bougent.**
- Rouge certain : `TRLift`.
- Verts, mais à retoucher : TR1, TH4 l. 485, le miroir Dialogue, la doc de A5/A5r, `TR2`, la doc de `AlundraMovementObstacleProbeTests`.
- À sortir des « à mesurer » (inchangés) : A115 et G116 (Réfuté 2), TN-3 (346), et l'arc à l. 214 de `AlundraDay3SceneArcTests.cs` (carte 176, sans soulevable).
- Toujours à mesurer : les marches de PNJ de A10, A11, A14 et A18 (écarts ≥ 65 px), H115 (séparé en Z).
- Aucun arc ni harnais n'est oublié (c1).
- Les 10 cartes 170-184 n'ont aucun test. D'après c4 et c8b, aucune marche de script et aucune sortie n'y est touchée ; seules des poches vides sont fermées. Elles relèvent de la recette (M7).

**(4) Questions à l'auteur, en termes de jeu.**
- **Q-G3-a — Portée de g3.** Après g3, les tonneaux, pierres, troncs, torches et les quatre objets du sanctuaire (248 placements, hors de l'histoire actuelle) arrêtent Alundra comme des caisses, mais ne se soulèvent qu'avec g2c.
  - *A* (fidèle aux collisions) : tous solides. Aucun code de plus. D'après le modèle grossier, le saut suffit à atteindre chaque sortie. Les énigmes qui demandent de les porter restent impossibles, comme aujourd'hui.
  - *B* : les laisser traversables jusqu'à g2c. C'est un nouvel écart dans la sonde, à retirer plus tard, avec des tests écrits deux fois.
  - *C* : faire g2c avant g3. Cela demande d'abord un T0 qui décode le corps de C4.
  - Dans tous les cas : sur la carte 4 (monde extérieur), les grosses pierres bloquent des chemins jusqu'au Gant, comme dans l'original.
- **Q-G3-b — g3 peut-il être fusionné seul ?** Entre g3 et g1b, les caisses sont des murs : on peut sauter dessus, pas les soulever. Sur le navire (392), il faut alors se caler contre le mur nord de la porte, puis sauter à gauche sur la seule caisse basse, malgré le roulis.
  - *A* (recommandé) : une seule fusion de g3 et g1b.
  - *B* : fusionner g3 d'abord. La recette de g3 ne montre que le blocage, et la 392 n'a que la route par le saut.
- **Q-G4-a (O-E20-5) — Les deux passages de la 392.** L'original permet de sauter sur la caisse basse et de soulever-lancer.
  - *A* : la recette et l'acceptation couvrent les deux, sans code propre à la 392 ; il faut deux arcs.
  - *B* : un seul passage.
- **Q-G3-c — La caisse du dessus d'une pile.** Quand on soulève celle du bas, l'original fait tomber celle du dessus de 16 px en 8 images. Le portage garde l'ancienne règle de chute des objets de carte jusqu'à E19.h2 : la chute peut différer, en durée ou par un saut d'un coup. À poser après une sonde de la DLL.
  - *A* : écart documenté jusqu'à E19.h2.
  - *B* : donner tout de suite la loi du binaire à la caisse qui perd son appui. C'est un nouveau site de marqueur, hors de D-G-13, avec ses tests.
  - *C* : attendre E19.h2, qui n'est qu'une esquisse avec 4 questions ouvertes ; le critère « pile » de g4 est alors repoussé.

Il n'y a pas de question à poser sur le roulis : il est fidèle, et ce n'est qu'une note de recette. Pas de question non plus sur le tick de `FlagToDestroy` : c'est une décision de conception, à consigner, qui annule D-E19-29.