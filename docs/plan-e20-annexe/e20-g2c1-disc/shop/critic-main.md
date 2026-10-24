**Rapport du critique indépendant : boutiques et code natif C4 (lecture seule)**

**Méthode.**
- J'ai re-dérivé seul S1, S3 et S4 depuis les programmes exportés (`decode_events.py` du dépôt), le binaire (capstone), la DLL et les plans.
- Rien n'est écrit hors de `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/shop-disc/critic/`. Ni build ni test n'ont été lancés.
- HEAD à la lecture : `4f1ad619`, puis `5f371e9c` (g3 seulement, sans effet ici).
- J'ai rejoué dans `critic/rp` les scénarios A, B, C, D, G et H du lecteur : sorties identiques aux siennes.

**Verdicts : S1, S2, S3, S4 et S5 sont CONFIRMÉS, avec une réfutation partielle en S3 (point 8) et des compléments.**

**S1, l'achat : CONFIRMÉ.**
1. FAIT : mon scan indépendant retrouve les 26 articles (134 : 4, 163 : 5, 258 : 5, 298 : 5, 398 : 7) et les 11 types.
   - Il trouve en plus 4 blocs de glace (103, 105, 108) et la carte 441 rec 33. Ce rec 33 est un tonneau de type 34 déjà en C4 dans son en-tête, donc le `41 [2,4]` y est redondant. Le lecteur ne l'avait pas listé ; ce n'est pas une boutique.
2. FAIT : le programme B[2] de la 258 se décode comme le dit le lecteur.
   - Séquence : `07 [3,3,7,41,43,2,2]` (l'article est dans la zone du comptoir), puis `43 [3]`, `70`, `42` (l'article est au sol), puis dialogue 129 avec Oui/Non, puis `9A`, `82 [36]`, `99 [5,0]`.
   - Ensuite `2E`, `2D` et `64 [3,248,0,120,2,160,0]` replacent l'article sur l'étagère : stock infini.
3. FAIT : le dialogue 163 (chaîne 35 de la 258) dit « jette-le sur le comptoir ». Les B tournent à chaque tick sur toute la carte (zones 0..51 x 0..59 relues).
4. FAIT : le comptoir de la 258 est fait de quatre petits plots de hauteur 2, en (6,41), (3,42), (5,42) et (6,42) ; le sol est à 1.
   - L'achat est donc un lancer de précision (une case = 24x16 px).
5. FAIT, binaire : le `0xB2 [rec,139]` du garde de sortie (B[6] de la 258 et de la 298) teste « portée ».
   - La table `0x80023C30` renvoie le cas 11 à `0x8003CE74`. Cette boucle part de l'entité 1 et retient les statuts 1 à 3 avec `+0x28 ≠ 0`.
   - L'article porté en main est bien celui visé ; le garde affiche le dialogue 135 (« briser les genoux »).
6. Précision : en 398, l'article 5 (veste) existe d'emblée, l'article 6 n'apparaît qu'après le drapeau 1009 (veste achetée), et l'article 7 après 1009 et 1108 (récipient acheté). Le lecteur écrivait « les trois derniers après 1009 ».

**S2, ce qui rend un article soulevable : CONFIRMÉ.**
1. FAIT, binaire : `TryLift` (`0x8002EDBC`) lit le genre `(+0x6C & 0x600) >> 9`.
   - Genre 1 : soulève. Genre 0 : refus. Genres 2 et 3 : exigent l'objet `0x3B`.
   - L'écriture `+0x28 = héros` est en `0x8002EE1C`.
2. FAIT : le genre n'est pas dans l'en-tête.
   - Les flags sont `MoreFlags | CanPickup << 8 | FPST << 16` (`EntitySpawnFactory.cs:740`), soit `0x230180`, genre 0.
   - C'est le programme C `62 [128,128,3]` qui pose `|= 0x380` et donc le genre 1.
3. FAIT : le seul `status =` de C4 est `sw 3,0x10(s0)` en `0x80066AF8`, quand les quatre coins lisent `& 0x1001 == 0x1001` (fosse).
   - Pas de casse : en-têtes E = 0 et `BreakEffect = 0`, aucun type global dans `BreakSpriteTypes` (`LiftableAi.cs:53`).
   - C4 ne ramène pas l'article : les B le font.
4. MESURÉ (rejoué) :
   - B : levée au tick 11, lancer au tick 61, atterrissage au tick 83 en (178, 100, 0).
   - C (sans C4) : l'objet reste lié au héros.
   - D (trou) : l'objet disparaît au tick 85.
   - G (levée depuis le sol) : coup d'épée, pas de levée. H (saut sur l'étagère) : levée au tick 40.
   - Limite : le script C est simulé par deux écritures du harnais.

**S3, la DLL au HEAD : CONFIRMÉ, avec une réfutation sur la nouveauté du défaut 398.**
1. FAIT : `0x41` s'applique (`EventProgramRunner.cs:865`).
   - Les clés du registre sont A0/C10/D9, A1/C1/D1, C61, A255/C255, C94, E17, C76, C23 et E2 : aucune clé `(C,4)`. L'arc d'Inoa (`InoaAwakeningArcTests.cs:148-183`) épingle d'ailleurs les 5 articles de la 163 à `(C,4)`.
   - `PlayerTryAction` est un bouchon qui renvoie 0 (`PlayerManager.cs:955`) : rien ne se soulève.
   - D-G-6 et D-G-8 (`plan-e20-heros.md`, vers l. 6780) font que `TryLift` refuse un type hors de l'ensemble câblé. Laisser les 11 types immobiles est donc le mécanisme existant (écart O-G-6), à coût nul.
2. FAIT : aucun autre chemin d'achat.
   - Sur les cinq cartes, les opcodes `0x82/83/84/97/98/99/9A` sont tous dans les B d'achat.
   - Exceptions : la réparation en 398 C[2], à 2 163 florins, et les trocs de faucons de la 134, qui sont sans transport.
3. FAIT : trois opcodes sans `case` dans le runner. Le défaut est `UnknownOpcode` : saut de la taille, `Result` inchangé (`:2147`, `:3109`).
   - `0x61` : 398 B[1] à B[7].
   - `0xB2` : 258 et 298 B[6].
   - `0xB9` : 258 B[5], 298 B[5] et B[6], 398 B[5], B[6] et C[2], 134 C[4] à C[11].
4. INFÉRENCE (non rejoué dans la DLL) : le défaut 398 se confirme à la main.
   - À chaque tick avec le héros au sol, `43 [1]` met `Result = 1`, `61` est sauté, et `03` mène à `@191`, soit le dialogue 166 puis la remise en place de l'article.
   - `GameplayBlockedMask` n'inclut pas `ControlLocked` (`GameState.cs:99`), donc les B continuent : le dialogue repart au tick suivant (boucle de dialogue).
5. Réfutation partielle : le lecteur dit que ce défaut est « absent des rapports précédents ».
   - Il est déjà connu : `docs/plan-e19-opcodes.md` §1.4.2 (l. 12606-12627) et `plan-e19-sm-annexe/sm1/report.md` (AR-04, l. 374 et 405-406), ainsi que la critique `check.md` D12 (confirmé par décodage, INFÉRENCE à l'exécution).
   - Son propriétaire est E19.sm1 (READY, « à réviser », option A par défaut).
   - `0xB2` appartient à E19.sm3b, après g1a (D-E19-111).
   - La critique sm1 (l. 111) notait aussi qu'aucun achat n'est possible : ces objets ne sont pas soulevables.
6. FAIT : la 258 se quitte normalement au HEAD.
   - Le `Result` périmé n'atteint le garde que si le drapeau `0x81F4` est levé alors que le héros est sur la tuile (5,46), ce qui est improbable.
   - Le jingle `0xB9` est sauté sans blocage : `0xBA` renvoie 0.
7. Autres faits :
   - Tests : seul `InoaAwakeningArcTests` touche aux boutiques.
   - Sauvegardes de test (noms seulement) : aucune pour ces cartes ; `163-magyscar-boss.sav` porte le numéro de sauvegarde 163, pas la carte 163.
   - Depuis g3 T1 (`d23804cf`), les soulevables sont solides pour le héros.

**S4, la boutique de la 163 et l'histoire : CONFIRMÉ.**
1. FAIT, atteignabilité : mon remplissage utilise le masque héros `0x40` (la DLL ajoute `0x01` seulement pour ClassB, `CellsCollisionField.cs`), les portails internes comme arêtes, et plusieurs tolérances de hauteur, en 4 et 8 voisins.
   - Départ en (5,18) : la zone atteinte reste dans x 3..40, y 7..27. La salle de boutique (x 3..14, y 39..55) reste à 0 case, et (5,46) n'est jamais atteint.
   - Elle ne se relie qu'avec le masque 0 et les diagonales (murs ignorés).
2. FAIT, entrées dans la 163 : le portail 162#0 vers (5,18) ; le `0x53` de la 416 C[1] vers (40,9,z2) ; le `64` du joueur en 163 B[1] @73, qui le pose en tuile (40,9). La porte (31,34) de la 162 mène en 258, pas en 163.
   - Aucune remise à zéro `0x38` ne vise 163, 258, 298, 398 ou 134 : toutes sont `table[162]` (variante d'Inoa extérieur), plus `17 → 439` et `12 → 407`.
3. FAIT : les dialogues d'achat de la 163 pointent des chaînes vides ou « #Disuse ». C'est un reste de gabarit.
4. FAIT : les drapeaux persistants posés par les achats sont 708, 1009 et 1108. Aucun des 30 cartes de la chaîne ne les lit (seule la 302 lit 708). Les objets consommés dans la chaîne sont 74, 88 et 89 ; il n'y a aucun `0x83` dans la chaîne.
   - Réserve : la liste des 30 cartes est héritée de g2c1, je ne l'ai pas re-dérivée.

**S5, le coût des options : CONFIRMÉ, avec ces compléments.**
1. L'option B se fait par l'ensemble câblé de D-G-8 : coût nul, déjà prévu comme O-G-6. Mais elle laisse la 398 bouclée tant que `0x61` n'est pas porté (E19.sm1).
2. L'option A : les 11 types vont dans `(C,4)` et dans l'ensemble câblé.
   - Un type ne doit jamais être câblé sans `(C,4)` : l'objet resterait collé aux mains (replay C).
   - L'objet est soulevable par entité (flags posés par le script) et le câblage se fait par type, donc câbler les types 66 ou 71 ne rend pas soulevables les objets lâchés par les ennemis (flags de genre 0).
   - Dépendances de l'achat : `0xB2` (E19.sm3b), `0x61` (E19.sm1), g1b, et le `70` des 258 et 398 (alors que les 163 et 298 utilisent `8D`).
   - Risques pour la 258 : `70` lit `IsOnGround` tiré d'un contrôleur, à prouver en test de monde ; le lancer doit tomber sur un plot de 24x16 px, à prouver aussi.
3. Cohérence avec l'auteur : O-E20-21 (« oui », 2026-10-22) porte déjà les blocs poussables C4 (g2c) ; les articles de boutique n'y figuraient pas.

**NON ÉTABLI.**
- Le sort d'un article porté lors d'un changement de carte (portail (7,31) de la 398).
- L'exécution réelle de la boucle 398 dans la DLL (interdit de construire).
- La marche à pied sur une marche de 16 px : le replay G est MESURÉ, mais l'application aux étagères réelles (hauteur 2 contre 1) reste une analogie.
- Le script C reste simulé par deux écritures dans tous les replays.

**Fichiers (dans `.../shop-disc/critic/`).** `dp.py`, `scan_c4.py`, `scan_warps.py`, `scan_portals.py`, `reach3.py`, `flagw.py`, `flagread.py`, `opcov2.py`, `ops5.py`, `hgrid.py`, `strs.py`, `mev.py`, `bdis.py`, `c4.txt` et `rp/` (copie du harnais).