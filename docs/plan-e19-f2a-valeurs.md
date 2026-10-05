# Annexe d'E19.f2a — table des épingles, valeurs écrites d'avance (F2A-0)

Annexe du plan `docs/plan-e19-opcodes.md`, §1.2j.3, tranche E19.f2a (tâche F2A-0, à commiter avant F2A-2). Une ligne par
assertion de classe R de la liste fermée (acceptation 2), par budget de classe C, par ligne de manette ou de budget du harnais
d'intro et par test dont le sort dépend d'un choix de conception ; puis les valeurs propres de l'oracle (F2A-1) et celles des
tests nouveaux de F2A-3. Date : 2026-10-05. Audit des valeurs à HEAD `5c26993`, arcs simulés (D-E19-77) à HEAD `87b4f02` ;
sources d'`Alundra/` et d'`Alundra.Tests/` identiques entre les deux, lignes du dépôt vérifiées à `7b12de0`.

**Sources.**
- Annexe d'audit des valeurs (brouillon de 128 entrées) et ses notes (`f2a-pins/` du scratchpad), avec sa contre-vérification
  indépendante (`f2a-pins-verify/verify.md`, corrections 1 à 8, toutes appliquées ici).
- Simulation hors dépôt des arcs (`f2a-sim/` : `sim_values.md`, `sim_pins_table.md`), avec sa contre-vérification
  (`f2a-sim-verify/verify.md`, corrections 1 à 7, toutes appliquées ici). Les arrêts S1 à S5 du brouillon sont remplacés par les
  valeurs simulées ; S6 est tranché (V-17).
- Plan, section « E19.f2a » (F2A-0 : budget = max(budget d'aujourd'hui, ⌈1,2 × fin prédite⌉), arcs simulés D-E19-77, forme
  exacte de la manette des arcs, acceptation 2) ; patron : `docs/plan-e19-d2c-valeurs.md`.

**Règle d'arrêt.** Toute valeur mesurée par F2A-3 qui diffère de sa ligne, hors de la marge écrite sur la ligne quand il y en a
une, est un arrêt : jamais une ré-épingle.

**Provenance** (écrite sur chaque ligne, entre crochets) : **modèle** = oracle (`model.py` du binaire corrigé de D-E19-62 et
D-E19-63, vérifié par l'oracle indépendant) ; **simulation** = lue sur la simulation des arcs (D-E19-77), « = modèle » quand la
simulation complète retrouve la valeur pure de la boîte ; **aujourd'hui lu** = lue dans le test ou dans les journaux d'arcs
d'aujourd'hui (A.4) ; **conception** = choix énoncé avec sa valeur.

**Points ouverts.** Aucun sur les valeurs : toutes les lignes sont réglées. Deux choix de conception tranchés ici (V-17 :
passe 93, le directeur demande la page au coureur Yarn au relâchement du `\A` ; V-38 : la boîte continue de tourner après
`AttachToWorld(null)`). Restent, comme choix de réécriture et non comme valeurs : S7 (instants d'appui des tests synthétiques)
et les risques R1 à R4 de la section L.

**Règle de lecture.** Une nouvelle valeur est écrite d'avance par l'oracle ou par la simulation (`model.py` du binaire, corrigé
de D-E19-62 et D-E19-63, sous la nouvelle manette, avec l'origine de chaque ouverture et l'ordre de l'hôte de F2-R1) ; elle
n'est jamais mesurée sur le directeur. Les valeurs « d'avant » sont lues dans les tests ou mesurées sur la DLL d'aujourd'hui
(journaux d'arcs instrumentés, harnais de mesure hors dépôt, section A.4). Les valeurs du modèle sont celles du binaire ; celles
de la DLL suivent la table d'origines de F2-R1 (D-E19-64).

## A. Conventions

### A.1 Ticks, passes et origines

- **N** : tick de l'ouverture (l'opcode `0x0D`/`0x5C`/`0xC4` qui réussit, ou l'`Open` d'un test). **E** : passe où la frappe
  finit (lecture de l'octet 0). **T** : passe du déclenchement de la fermeture. **R = T+18** : passe de la libération (drapeau
  d'activité, `MessageBox`, `MenuOpen` tombent). **G** : pas qui pose un drapeau du texte. **L** : tick d'un `0x51`.
- **Origine et première mise à jour** (F2-R1) : événement de carte (programme B), déclencheur en attente, rappel d'un test,
  `Open` d'un test avant la première passe : première mise à jour à N+1, premier glyphe à N+19, libération vue à R ; script
  d'entité (programme C ou F lancé par la mise à jour des entités du moteur ; livre de sauvegarde, C) et scripts de `RunFrame`
  du harnais : première mise à jour à N, premier glyphe à N+18, libération vue à R+1 par un script de cette origine ; écriture
  `0x4C`-`0x51` : effet à k+1 (après la passe) ou à k (avant la passe).
- **Passe k d'un test unitaire** : la k-ième passe de la boîte après l'`Open` du test (un `Tick()` direct, ou un tick logique du
  mandataire). L'`Open` du test précède la passe 1 (N = 0, première mise à jour à la passe 1).
- **Appui d'un test** : `ButtonsHold = Carré` écrit pour un tick, puis 0 ; enregistré par la passe de la manette de ce tick, vu
  par la boîte à la passe suivante (F2-R1 : la boîte lit le Carré du tick précédent). « Appui vu à la passe k » = maintien écrit
  avant la passe k−1.
- **Images de rattrapage** : boîte et événements de carte s'entrelacent tick par tick (B0 M0 B1 M1 …) ; les déclencheurs en
  attente tournent après la boucle ; les scripts d'entité, une fois par image, avant elle.

### A.2 Manettes

- **Nouvelle aide des arcs** (`AlundraArcSupport.cs:468-486`, et ses copies A17 `AlundraDay3SceneArcTests.cs:211-234`, contre-preuve
  de T-B9 `AlundraEntityContactArcTests.cs:340-348`) : au début de chaque image f, elle lit le directeur (ouvert ; « attend un
  appui » = curseur `\A` affiché, ou frappe finie avec `closeMode & 2`) et pose le maintien de l'image f :
  `H(f) = ouvert ∧ ¬(attend un appui ∧ H(f−1))` ; Carré est donc tenu pendant la frappe, et une attente reçoit un relâchement
  d'une image puis un appui. La boîte de l'image f+1 lit H(f) (deux images de retard sur l'état lu) : un curseur `\A` affiché à
  la passe a est relâché à a+3, une frappe finie à E (masque 3) se ferme à T = E+3. L'oracle prend cette manette exacte. **Forme exacte du plan** : Carré est tenu à l'image f si une boîte est
  ouverte et pas (la boîte attend un appui et Carré était tenu à f−1). Elle donne un second front de Carré deux images après
  chaque appui consommé (A11 `M164_S002` voit des appuis aux images 140 et 142 ; la fermeture à T 390 est suivie d'un front à
  392), sans effet sur les 13 arcs simulés (chaque `\A` est relâché à a+3 et chaque fermeture vient à T = E+3 ; aucune
  attente ne commence dans les deux images qui suivent un relâchement, sauf A9, qui utilise `CloseDialogueWithTheButton`) ;
  l'aide de F2A-3 la reproduit telle quelle.
- **Règle d'appui des tests du harnais** (F2-R1) : la même règle, décidée par le rappel de l'image f après la passe de la boîte de
  f, lue par la passe de f+1 (une image de retard) : curseur à a, relâché à a+2 ; frappe finie à E, T = E+2.
- **`CloseDialogueWithTheButton`** (`AlundraArcSupport.cs:450-462`, inchangée : l'audit ne la change pas) : à partir de l'image s
  de l'appel, `Press` aux images s, s+2, s+4 … tant que la boîte est ouverte au début de l'image, une image sans bouton entre
  deux ; la boîte fidèle se ferme avec elle (A1, A1c, A9 : valeurs ci-dessous).
- **Manette A du binaire** (tenu et appui naissant à chaque image, impossible sur une vraie manette) : seulement pour les valeurs
  de `selftest()`.

### A.3 Natures

- **(a)** valeur de la boîte : la nouvelle valeur est un nombre écrit d'avance (les attentes en aval sont pures).
- **(b)** ré-ancrage (A10, A11) : lecture « image de libération de la dernière boîte traversée (vue par l'acteur qui
  l'attend) + l'écart d'aujourd'hui » ; la valeur écrite est la valeur absolue simulée (D-E19-77), qui remplace la forme (b) là
  où le chemin critique change (A10 : libération de `M165_S030` ; A11 : vie de `M164_S003`). Chaque ligne (b) porte sa marge,
  égale à la période de l'attente périodique de l'acteur qui suit (Wendell 3, Meade 2, Bergus 2, Septimus 2) ; une mesure hors de
  valeur ± marge est un arrêt.
- **budget** : `FrameLimit` ou longueur de `RunFramesForTest` = max(budget d'aujourd'hui, ⌈1,2 × fin prédite⌉) : un budget ne
  baisse jamais. Fin prédite = `Frame` à `Dispose` simulé (A6, A8, A9, A10, A11, A12, A20, T-A19, T-B9, A14, A15, A18), sinon
  `Frame` d'aujourd'hui (mesuré, A.4) + Σ des écarts des boîtes du chemin.
- **harnais**, **conception** : lignes de manette du harnais ; choix de conception énoncé avec sa valeur.

### A.4 Mesures d'aujourd'hui (provenance)

Harnais de mesure hors dépôt : un projet de test de brouillon compile les sources de `Alundra.Tests` sans les changer, avec une
copie instrumentée de `AlundraArcSupport.cs` (opcodes de dialogue, phase moteur ou mandataire, transitions d'`IsOpen`, `Frame` à
`Dispose`) et une réplique journalisée du montage du marin 12. 53 tests d'arcs passés, journaux par arc ; aucun fichier suivi
touché. Les « ouverture », « fermeture » et « 0x39 » d'aujourd'hui des tables ci-dessous en viennent.

## B. Valeurs propres de l'oracle (F2A-1)

### B.1 `selftest()` (ordre du binaire : la boîte avant les scripts ; toutes reproduites par l'oracle, boîte du binaire)

- O-01 Glissement d'entrée, y de N+1 à N+18 : 240, 236, 231, 226, 221, 216, 212, 207, 202, 197, 192, 188, 183, 178, 173, 168,
  168, 168 ; premier glyphe N+19. [prov. : modèle]
- O-02 `AB` sans manette : glyphes N+19, N+23 ; E N+27 ; minuterie T N+387 ; libération et `0x39` N+405. [prov. : modèle]
- O-03 `AB`, manette A : glyphes N+19, N+20 ; E N+21 ; T N+22 ; libération N+40. [prov. : modèle]
- O-04 `A\TB` sans manette : glyphes N+19, N+31. [prov. : modèle]
- O-05 `a\Nb\Nc\Nd` sans manette : glyphes N+19, N+27, N+35, N+60 ; défilement de 2 à 16 px aux passes N+49 à N+56. [prov. : modèle]
- O-06 `a\Ab\Nc\Nd`, un appui vu à N+24 : binaire N+19, N+28, N+36, **N+52** (bit 8 résiduel) ; **corrigé (D-E19-63) N+19,
  N+28, N+36, N+61** ; curseur image 0 à N+23. [prov. : modèle]
- O-07 `a\N\Ab`, un appui vu à N+28 : lignes `a` / `` / `b` à N+40. [prov. : modèle]
- O-08 `ABC`, `0x4C 4` puis `0x4D` à N, `0x4D` à N+5 et N+30, manette A : glyphes N+19, N+31 (binaire et corrigé). [prov. : modèle]
- O-09 Curseur de `a\Ab` sans manette : 9 images de l'image 0, puis 10 de 1, 10 de 2, 10 de 3, 10 de 0, puis 1. [prov. : modèle]
- O-10 (ajout, D-E19-62) `ABCDEFGH`, `0x4C 4` à N, `0x4D` puis `0x4C 3` au tick N+25, `0x4C 4` à N+40, sans manette : binaire
  N+26, N+30, N+34, N+38, **N+41** (verrou resté en attente) ; **corrigé N+26, N+30, N+34, N+38**. [prov. : modèle]
- O-11 (ajout, nouvelle manette des arcs) `AB` : ordre du binaire N+19, N+20, E N+21, T N+24, libération N+42 ; ouverture par un
  script d'entité : N+18, N+19, E N+20, T N+23, libération N+41, `0x39` d'entité N+42. [prov. : modèle]

### B.2 Les trois textes réels (pages Yarn exportées, corrigés, nouvelle manette des arcs)

Pages Yarn = texte brut de la carte, sauf `M391_S022` (espace avant `\999` coupée par D-E15-8 : un pas de moins) et
`M164_S003` (`\0200` écrit `200`, sans effet).

- O-20 389 marin 12 (`C[12]` @1390 : `0x0D [0x81,1]`, `0x50 4`, `0x36 T999`, `0x44` résolu au tick suivant, `0x51`, `0x0D`
  réessayé, `0x39`) :
  - binaire, manette A : `M389_S001` premier glyphe N+19, dernier N+93, T999 N+94, E N+95, T N+96, `M389_S002` ouvert à N+114 ;
  - corrigé, nouvelle manette, ordre du binaire : `M389_S001` identique ; `M389_S002` (N2 = N+114) premier glyphe N2+19, `\A`
    N2+106 relâché N2+109, défilements N2+141 et N2+166, E N2+184, T N2+187, libération N2+205 ; `0x39` N+319 ;
    D-E19-63 joue ici (boîte du binaire sous la même manette : défilement N2+157, E N2+175, libération N2+196) ;
  - corrigé, ordre de la DLL (`C[12]` est un script d'entité) : `M389_S001` premier glyphe N+18, T999 posé à N+93 (vu N+94),
    E N+94, T N+95, libération N+113 ; `M389_S002` ouvert à N+114, premier glyphe N2+18, `\A` N2+105 relâché N2+108, E N2+183,
    T N2+186, libération N2+204 ; `0x39` N+319. [prov. : modèle]
- O-21 391 bloc (`B[1]`, sous-programme @706 : `0x4C 2`, `0x50 4`, attente de T999, `0x37 60`, `0x51`, `0x39`), indépendant de
  la manette (`0x4C 2` ôte l'accélération, `0x50 4` le bouton) ; DLL = binaire (événement de carte) :
  - `M391_S019` : T999 N+351, E N+355, T N+413, libération et `0x39` N+431 ;
  - `M391_S020` (ouvert à N1+463) : T999 N+499, E N+503, T N+561, libération N+579 ;
  - `M391_S022` (ouvert à N1+1152) : T1000 N+87, T999 N+407, E N+411, T N+469, libération N+487 (texte brut : N+411, N+415,
    N+473, N+491). [prov. : modèle]
- O-22 164 Septimus (`C[2]`, `M164_S003` ; attentes physiques à zéro comme le modèle) :
  - binaire, manette A : T200 N+29, `\A` N+63 relâché N+64, T201 N+65, E N+172, T N+173, libération N+191 ;
  - corrigé, nouvelle manette, ordre du binaire : T200 N+29, `\A` N+63 relâché N+66, T201 N+68, E N+176, T N+179, libération
    N+197 ; D-E19-62 joue (`0x4D` @350 puis `0x4C 3` @351 au même tick ; boîte du binaire sous la même manette : E N+175,
    libération N+196) ;
  - corrigé, ordre de la DLL (script d'entité) : premier glyphe N+18, T200 N+28, `\A` N+62 relâché N+65, T201 N+67, E N+175,
    T N+178, libération N+196, `0x39` N+197. [prov. : modèle]

## C. Classe R — tests de la boîte (synthétiques)

Textes : `bonjour` (7 glyphes), `hello` (5), `first` (5), `box` (3), `menu box` (8), drapeaux par défaut (`textFlags` 3,
`closeMode` 3), sans manette sauf les appuis nommés. Aujourd'hui : une page par appui vu au tick de l'appui, fermeture au tick
de l'appui ou du `0x51`, minuterie de 360 depuis l'ouverture ou la page. Fidèle : `bonjour` sans manette, glyphes 19 à 43,
E 47, T 407, R 425 ; avec un appui vu à 48, T 48, R 66.

- V-01 `AlundraDialogueFramePassTests.cs:98` — appui écrit en `ButtonsJustPressed` seul — avant : appui sans maintien — boîte
  `bonjour` mode 0, ouverte par le test — harnais de test — nouvelle valeur : maintien `ButtonsHold = Carré` écrit avant l'Update
  47, 0 avant l'Update 48 (appui vu à la passe 48). [prov. : modèle ; avant : aujourd'hui lu]
- V-02 `:93`, `:101-104` — fermée et drapeaux à 0 après l'Update de l'appui — avant : fermée à l'Update 2 — durée aujourd'hui 2,
  fidèle 66 — (a) — nouvelle valeur : un appui écrit avant l'Update 2 (vu à la passe 3, pendant le glissement) ne change rien ;
  ouverte après l'Update 65 ; fermée, `MenuOpen | MessageBox` à 0 après l'Update 66 (appui vu à 48, T 48). [prov. : modèle ; avant : aujourd'hui lu]
- V-03 `:121-126` — ouverte après 100 images de 3 ticks — avant : ouverte à 300 ticks (seuil 360 depuis l'ouverture) — (a) —
  nouvelle valeur : libération à la passe 425 (minuterie armée à E 47) ; avec l'écart d'environ 60 voulu par le test : ouverte
  après 121 images (363 passes), fermée après 41 de plus (162 images, 486 passes). [prov. : modèle ; avant : aujourd'hui lu]
- V-04 `:128-134` — fermée après 34 images de plus (402 ticks) — avant : fermée — fidèle : encore ouverte à 402 — (a) — nouvelle
  valeur : ligne V-03 (fermée après 41 images de plus, 162 images au total). [prov. : modèle ; avant : aujourd'hui lu]
- V-05 `:171-180` — appui avalé à l'ouverture, appui frais qui ferme au `Tick` suivant — avant : D-E12D-6, fermée au 2e `Tick` —
  (a) — nouvelle valeur : le glissement ne lit pas la manette (le saut d'appui est sans objet) ; ouverte après les 2 `Tick` ;
  un appui vu à la passe 48 ferme : T 48, R 66 (fermée au 66e `Tick`). [prov. : modèle ; avant : aujourd'hui lu]
- V-06 `:184-194` — ouverte après 359 `Tick`, fermée au 360e — avant : minuterie depuis l'ouverture — (a) — nouvelle valeur :
  ouverte après 424 `Tick`, fermée au 425e (E 47 + 360 = T 407, R 425). [prov. : modèle ; avant : aujourd'hui lu]
- V-07 `:217-221` — un appui après ré-attache tourne à la page 2 — avant : au 1er `Tick` — `page un\Apage deux` — (a) — nouvelle
  valeur : curseur `\A` à la passe 47 ; 47 `Tick` sans appui, puis l'appui vu à la passe 48 relâche : ouverte, page `page deux`
  dès la passe 48 (premier glyphe de la page 2 à 52). [prov. : modèle ; avant : aujourd'hui lu]
- V-08 `AlundraDialogueOpcodeDispatchTests.cs:144-147` — un appui et un `Tick` ferment (masque 3) — avant : fermée au `Tick` 1 —
  `hello` mode 1 — durée aujourd'hui 1, fidèle 58 — (a) — nouvelle valeur : E 39 ; appui vu à la passe 40 : T 40 ; ouverte
  jusqu'à la passe 57, fermée à la passe 58. [prov. : modèle ; avant : aujourd'hui lu]
- V-09 `:160-166` — masque 4 : l'appui ne ferme pas ; `RequestScriptClose` ferme aussitôt — avant : fermée à l'appel — (a) —
  nouvelle valeur : `:162` inchangée (ouverte) ; `:165` vrai (verrou `0x51` posé, `closeMode & 4`) ; `:166` ouverte jusqu'à la
  passe 57, fermée à la passe 58 (verrou posé après la passe 1, T = max(2, E+1) = 40, R 58). [prov. : modèle ; avant : aujourd'hui lu]
- V-10 `:178-179` — `first`, masque 4, `RequestScriptClose` puis fermée — avant : fermée à l'appel — (a) — nouvelle valeur :
  verrou avant la passe 1, T 40, R 58 : ouverte jusqu'à 57, fermée à 58 ; la seconde `Open` (`:184`, masque remis à 3) après la
  passe 58. [prov. : modèle ; avant : aujourd'hui lu]
- V-11 `:276-293` — `MessageBox` puis `MenuOpen` à 0 aussitôt après `RequestScriptClose` — avant : au même appel — (a) — nouvelle
  valeur : `box` mode 1, verrou avant la passe 1 : E 31, T 32, `MessageBox` tombe à la passe 50 ; `menu box` mode 0 : E 51, T 52,
  `MenuOpen` tombe à la passe 70 (passes comptées depuis chaque ouverture). [prov. : modèle ; avant : aujourd'hui lu]
- V-12 `AlundraDialogueFlagMarkerTests.cs:117-129` — drapeau 10 posé à l'affichage de la page 0, drapeau 20 à celui de la page 2 —
  avant : à l'ouverture et au 2e appui — texte `Salut\10\Y ami\Apage one\A\20page two` — (a) (fin de D-E12-4) — nouvelle
  valeur : `:117` faux après l'`Open`, drapeau 10 posé à la passe 39 (pas du `\Y`) ; `:119` inchangée (page entière chez le
  directeur, F2-R6) ; curseur à 59, appui vu à 60 : `PageIndex` 1 dès la passe 60 (`:123`), drapeau 20 faux (`:124`) ; curseur à
  96, appui vu à 97 : page 2 (`:129` « page two ») dès 97 ; drapeau 20 posé à la passe 101 (premier pas de la page), faux de 97
  à 100 (`:128`). [prov. : modèle ; avant : aujourd'hui lu]
- V-13 `:144-146` — drapeaux 1004 et 999 à l'ouverture — (a) — nouvelle valeur : 1004 à la passe 39, 999 à la passe 43, E 47 ;
  texte `Fin ?` inchangé. [prov. : modèle ; avant : aujourd'hui lu]
- V-14 `:161-162` — drapeau 999 à l'ouverture d'une page vide — (a) — nouvelle valeur : 999 à la passe 19, E 23 ; texte vide. [prov. : modèle ; avant : aujourd'hui lu]
- V-15 `:176-180` — aucun drapeau, texte `abc` — (a) — nouvelle valeur : inchangée (le marqueur inconnu ne pose rien, page entière
  chez le directeur). [prov. : modèle ; avant : aujourd'hui lu]
- V-16 `:215` — `flag100=True;falcon=4` au `ShowLine` du présentateur de capture — (a) (F2-R6 : le présentateur de capture ne
  pose plus les drapeaux) — nouvelle valeur : `{"flag100=False;falcon=4"}` ; `:216` inchangée. [prov. : modèle ; avant : aujourd'hui lu]
- V-17 `AlundraDialogueYarnRenderingTests.cs:160-173` — commandes `<<flag>>` de chaque page à son affichage — `page zero` (9),
  `page one` (8), `page two` — (a) — **choix de conception tranché** : le directeur demande au coureur Yarn la page suivante au
  relâchement du `\A`, comme le supposent les lignes `:166` et `:173` — nouvelle valeur : `<<flag 10>>` à l'`Open` (`:160`
  inchangée, comme `TheOldFlagCommand_IsStillServed` `:194`) ; curseur 55, appui vu à 56 : page 1 dès 56 (`:166`), 20 faux
  (`:167`) ; curseur 92, appui vu à 93 : page 2 dès 93 (`:173`) ; `<<flag 20>>` à la passe **93**. [prov. : modèle ; avant : aujourd'hui lu]

## D. Classe R — livre de sauvegarde (`AlundraSaveBookTests.cs`)

`Tick()` = programme C du livre (script d'entité : ouverture, puis verrou) puis la passe. `Etc_0064` « Enregistrer tes progrès? »
(24 glyphes), mode 1, masque 4 posé à l'ouverture, sans manette, ouvert au `Tick` 1 : premier glyphe `Tick` 19, dernier 111, E 115.
Verrou posé au `Tick` L (OUI ou NON au `Tick` 63, abandons du `Tick` 2 au `Tick` 125) : T = max(L, 116) ; pour L ≤ 116,
**T 116, libération au `Tick` 134** (L = 124 donnerait 142, mais le verrou de l'état 4 est déjà posé au `Tick` 63). Aujourd'hui
la boîte tombe au `Tick` du verrou.

- V-36 `:157`, `:159` — après OUI et un `Tick` (`Tick` 63) : `IsOpen` faux, drapeaux = `ControlLocked` — (a) — nouvelle valeur :
  au `Tick` 63 `IsOpen` vrai, drapeaux `ControlLocked | MessageBox` ; `IsOpen` faux et `MessageBox` à 0 dès le `Tick` 134 ;
  `:175` (`Tick` 174) et `:179` (`Tick` 175) inchangées. [prov. : modèle ; avant : aujourd'hui lu]
- V-37 `:193` `AssertReleased` (NON, `Tick` 63) — relâché aussitôt — (a) — nouvelle valeur : valable dès le `Tick` 134 (71
  `Tick` de plus). [prov. : modèle ; avant : aujourd'hui lu]
- V-38 `:267` (`State2_NoPresenter`, `Tick` 62) — (a) — **décision** : la boîte continue de tourner après `AttachToWorld(null)`
  (machine de la boîte sans présentateur, page connue depuis l'`Open`, F2-R2) — nouvelle valeur : dès le `Tick` 134. [prov. : modèle ; avant : aujourd'hui lu]
- V-39 `:285` (`State2_ChoiceTextsUnresolved`, `Tick` 62) — (a) — nouvelle valeur : dès le `Tick` 134. [prov. : modèle ; avant : aujourd'hui lu]
- V-40 `:339` (`SlotF_Repeated…`, `Tick` 125) — (a) — nouvelle valeur : dès le `Tick` 134. [prov. : modèle ; avant : aujourd'hui lu]
- V-41 `:353` (`State5_ScreenAlreadyActive…`, `Tick` 124) — (a) — nouvelle valeur : dès le `Tick` 134. [prov. : modèle ; avant : aujourd'hui lu]
- V-42 `:427` (`State5_CaptureRefused_EndsInBoundedTicks…`) — (a) — nouvelle valeur : inchangée (la boucle finit après la chaîne
  d'échec de 40 ticks commencée au `Tick` 124, donc après 134 ; `:425` inchangée ; risque R2). [prov. : modèle ; avant : aujourd'hui lu]
- V-43 `:439` (`State5_NoScreenWired`, `Tick` 124) — (a) — nouvelle valeur : dès le `Tick` 134. [prov. : modèle ; avant : aujourd'hui lu]
- V-44 `:179` — inchangée (`Tick` 175). Appels `:227`, `:241`, `:253` : classe U. Appels `:211`, `:305` : conception, V-146. [prov. : modèle ; avant : aujourd'hui lu]

## E. Classe R — harnais d'intro (389)

Ordre du harnais : `RunFrame` (scripts) → passe de la boîte → rappel. Manette : règle d'appui du harnais (A.2). `M389_S005`
(page 1 : 3 lignes, page 2 : 2 lignes et un `\N` final, mode 0).

**Marin 12** (`SailorTwelve_*`, ouverture par `C[12]` dans `RunFrame`, première mise à jour à N). Aujourd'hui mesuré : `S001`
ouverte à 2, choix posé à 2, OUI à 2, `S002` ouverte à 3 (appui à 3, page 2 à 4), fermée par la minuterie à 364, drapeaux à 0
à 365. Fidèle : `S001` ouverte à 2, premier glyphe 20, T999 posé à 95 (vu par `RunFrame` à 96), choix à 96, OUI par le rappel
de 96, résultat de `0x44` et `0x51` à 97, T 97, libération 115 ; `S002` ouverte à 116 (le `0x0D` réessayé voit la libération),
curseur `\A` 221, relâché 223, E 298, T 300, libération 318 ; `0x39` @1417 et `0x06` à 319. Maintiens du rappel : de 2 à 114,
0 à 115 (`S001` libérée), de 116 à 220, relâché à 221, appui de 222 à 297, relâché à 298, appui de 299 à 317, 0 dès 318 ; fronts
montants aux images 2, 116, 222, 299.

- V-18 `AlundraDialogueOpcodesProductionTests.cs:96` — budget `RunFramesForTest(400)` — budget — nouvelle valeur : **400
  inchangé** (max(400, ⌈1,2 × 319⌉ = 383) ; fin 319). [prov. : modèle ; avant : aujourd'hui lu]
- V-19 `:111`, `:152` — manette remise à 0 à chaque image, un appui `ButtonsJustPressed` à l'ouverture de `S002` — harnais —
  nouvelle valeur : règle d'appui (maintiens ci-dessus). [prov. : modèle ; avant : aujourd'hui lu]
- V-20 `:167` — `S002` fermée à la fin — (a) — nouvelle valeur : vraie dès l'image 318 (inchangée à la fin du budget). [prov. : modèle ; avant : aujourd'hui lu]

**Marin 13 mono-ligne** (`SailorThirteen_MonoLine_*`) et **gel global** (`AlundraGlobalFreezeEntityUpdateTests`) : ouverture par
le rappel de l'image 2 (`RunScript` du programme F 1660), première mise à jour à 3. Fidèle : premier glyphe 21, curseur `\A` 106,
relâché 108, défilements 108, 149, 186, E 204, T 206, libération 224 (vue par le rappel de 224, par les scripts à 225). Maintiens :
dès le rappel de 2, relâché à 106, appui 107, relâché 204, appui 205. Aujourd'hui : ouverte à 2, appuis de 39 à 47, fermée
entre 40 et 49.

- V-21 `:210` — budget 60 — budget — nouvelle valeur : 270 (fin 225, réouverture). [prov. : modèle ; avant : aujourd'hui lu]
- V-22 `:236` — ouverte à l'image 38 — (a) — nouvelle valeur : inchangée (ouverte à 38). [prov. : modèle ; avant : aujourd'hui lu]
- V-23 `:244-247` — appuis de 39 à 47 — harnais — nouvelle valeur : règle d'appui (maintiens ci-dessus). [prov. : modèle ; avant : aujourd'hui lu]
- V-24 `:249` — fermée entre 40 et 49 — (a) — nouvelle valeur : fermée, `MenuOpen` à 0, vue par le rappel de 224. [prov. : modèle ; avant : aujourd'hui lu]
- V-25 `:257` — réouverture à 50 — (a) — nouvelle valeur : 225. [prov. : modèle ; avant : aujourd'hui lu]
- V-32 `AlundraGlobalFreezeEntityUpdateTests.cs:96` — budget 60 — budget — nouvelle valeur : 269 (fin 224). [prov. : modèle ; avant : aujourd'hui lu]
- V-33 `:98` — manette remise à 0 — harnais — nouvelle valeur : règle d'appui. [prov. : modèle ; avant : aujourd'hui lu]
- V-34 `:174-177` — appuis de 39 à 47 — harnais — nouvelle valeur : maintiens ci-dessus. [prov. : modèle ; avant : aujourd'hui lu]
- V-35 `:178-187` — fermeture entre 40 et 49 — (a) — nouvelle valeur : 224 ; `:161` (gel des images 3 à 38) inchangée. [prov. : modèle ; avant : aujourd'hui lu]
- V-35b `:188` — `Assert.False(director.IsOpen)` à la fin — (a) — nouvelle valeur : vraie dès 224 (budget 269). [prov. : modèle ; avant : aujourd'hui lu]

**Marin 13, chaîne complète** (`SailorThirteen_FullInteractionChain_*`) : appui d'interaction à l'image 10 (inchangé), ouverture
par la vraie sélection du programme F dans `RunFrame` de 11, première mise à jour à 11. Fidèle : premier glyphe 29, curseur 114,
relâché 116, défilements 116, 157, 194, E 212, T 214, libération 232 (rappel de 232 ; scripts 233). Maintiens : dès le rappel de
11, relâché 114, appui 115, relâché 212, appui 213.

- V-26 `:307` — budget 120 — budget — nouvelle valeur : 358 (fin 298 : fenêtre sans réouverture gardée à 65 images, 234 à 298,
  conception). [prov. : modèle ; avant : aujourd'hui lu]
- V-27 `:357-365` — appui à 10 et appuis de 40 à 48 — harnais — nouvelle valeur : appui à 10 inchangé, puis la règle. [prov. : modèle ; avant : aujourd'hui lu]
- V-28 `:386-393` — ouverte à 11 avec `MenuOpen` et `PageIndex` 0 — (a) — nouvelle valeur : inchangée. [prov. : modèle ; avant : aujourd'hui lu]
- V-29 `:397` — ouverte à 39 — (a) — nouvelle valeur : inchangée. [prov. : modèle ; avant : aujourd'hui lu]
- V-30 `:402` — fermée entre 41 et 54 — (a) — nouvelle valeur : 232. [prov. : modèle ; avant : aujourd'hui lu]
- V-31 `:410-412` — aucune réouverture après 55 — (a) — nouvelle valeur : aucune après 233 (observée de 234 à 298). [prov. : modèle ; avant : aujourd'hui lu]

## F. Classe R — arcs

Boîtes mesurées aujourd'hui (journaux d'arcs), boîte fidèle de l'oracle sous la nouvelle manette des arcs, origine de F2-R1.
« vu » = image où le `0x39` de l'ouvreur rend 1 ; Δ = vu fidèle − vu d'aujourd'hui.

### F.1 A6 — 391 `B[1]` (programme @228), événements de carte, mode 1, fermées par le sous-programme @706

| Boîte | Ouverture auj. → nouv. | Aujourd'hui (`0x51` = `0x39`) | Fidèle (relatif à N) | Δ |
|---|---|---|---|---|
| `M391_S019` @295 | 180 → 180 | 241 (N+61) | T999 N+351, `0x51` N+412, T N+413, R et `0x39` N+431 | +370 |
| `M391_S020` @306 | 273 → 643 | 334 (N+61) | T999 N+499, `0x51` N+560, T N+561, R N+579 | +518 |
| `M391_S022` @327 | 444 → 1332 | 505 (N+61) | T1000 N+87, T999 N+407, `0x51` N+468, T N+469, R N+487 | +426 |

Provenance du tableau : modèle (relatif à N), égal à la simulation ; « aujourd'hui » lu.

Boîtes simulées : `M391_S019` ouverte 180, T999 posé 531, E 535, `0x51` 592, T 593, libération 611 ; `M391_S020` 643, T999 1142,
E 1146, `0x51` 1203, T 1204, libération 1222 ; `M391_S022` 1332, T1000 1419, T999 1739, E 1743, `0x51` 1800, T 1801,
libération 1819. La branche @716-@733 du sous-programme @706 (`0x8E` @721, `0x5A [130, 65]` @726, `0x5B [6, 4, 66]` @729,
`0x02` @733) tourne **321 fois, ticks 1419 à 1739** (au tick 1739 le script reprend à @716 et passe par la branche avant que
@710 voie T999). Aujourd'hui T1000 et T999 sont posés ensemble à l'ouverture et @716 n'est jamais atteint. [prov. : simulation]

- V-51 `AlundraShipBlockArcTests.cs:31` — budget 1500 — budget — nouvelle valeur : **2957** (fin simulée 2464). [prov. : simulation]
- V-52 `:199` — @540 à 1149 — (a) — nouvelle valeur : **2463**. [prov. : simulation]
- V-53 `:230` — ouvertures (180, 273, 444) — (a) — nouvelle valeur : **(180, 643, 1332)**. [prov. : simulation]
- V-54 `:231` — `0x51` @739 (241, 334, 505) — (a) — nouvelle valeur : **(592, 1203, 1800)**. [prov. : simulation]
- V-55 `:232` — écarts (61, 61, 61) — (a) — nouvelle valeur : **(412, 560, 468)**, avec les ouvertures de la ligne ré-épinglées
  comme `:230` ; telle qu'écrite aujourd'hui (ouvertures 180, 273, 444 en dur), la ligne calcule (412, 930, 1356). [prov. : simulation]
- V-56 `:237` @321 413 → 1301 ; V-57 `:239` @368 619 → 1933 ; V-58 `:241` @377 640 → 1954 ; V-59 `:243` @497 854 → 2168 ;
  V-60 `:246` @347 568 → 1882 ; V-61 `:247` @388 702 → 2016 ; V-62 `:248` @390 702 → 2016 ; V-63 `:251` @449 703 → 2017 ;
  V-64 `:256` @470 728 → 2042 ; V-65 `:258` @491 793 → 2107 ; V-66 `:262` @514 987 → 2301 ; V-67 `:265` `0x65` @517 de 988
  à 1027 → de 2302 à 2341 ; V-68 `:267` @528 1027 → 2341 ; V-69 `:268` @529 1028 → 2342 — (a), attentes pures de `B[1]`. [prov. : simulation]
- V-70 `:275` — @761 de `B2` à 633 — (a), cycle de `B2` — nouvelle valeur : **1899**. `B2` (@552) relit T0 aux images
  s+61, s+183, s+205, s+447, s+509, s+631, s+693 de chaque cycle de 694 images (s = 1, 695, 1389 …) ; T0 posé à 1882 → test à
  1898 → @760 → @761 à 1899 (aujourd'hui T0 568 → test 632 → 633). [prov. : simulation = modèle]
- V-70b `:192` (hors liste fermée du brouillon, dans celle du plan : A6 `:172-284`) — opcodes `0x8E` exécutés {260, 335, 342} —
  (a) — nouvelle valeur : **{260, 335, 342, 721}** : `M391_S022` pose T1000 à son pas (N3+87 = 1419), avant T999 (1739) ; la
  branche @716-@733 tourne **321 fois, ticks 1419 à 1739** (`0x8E [3,1,6,2]` @721, `0x5A [130,65]` @726, `0x5B [6,4,66]` @729).
  [prov. : simulation]
- V-70c `:208` `arrival.DirectionId` — 0 — (a) — nouvelle valeur : **16** : `0x5A [130, 65]` @726 tourne à chaque tick de la
  branche sur toutes les entités, héros compris (recherche 0x82, `EntitySearchService.cs:134-140`), vers
  `CardinalDirectionTable[1]` = 0x10 (`AnimationTables.cs:20`) ; `0x53` @540 recopie `player.TargetDirection`
  (`AlundraWarpDirector.cs`, `BeginDepartureFromChangeMapOpcode`, `directionId: player.TargetDirection`). `:207` (`AnimationId`
  0) et `:202-206` inchangées. [prov. : simulation]
- `:186` (classe L) 3 → 0 (`0x4C` @706 exécuté, plus sauté). [prov. : simulation]
- Inchangées : `:190`, `:193-196` (balancement, drapeau armé jusqu'à la fin), `:200`, `:202-207`, `:211-213`, `:216-219`,
  `:222-223`, `:226-227`, `:233` (3), `:234`, `:238`, `:240`, `:242`, `:244` (`ForceZ`), `:249-250`, `:252-253`, `:257`, `:259`
  (bloc (46137344, 44)), `:263-264` (40, 40), `:266` (51904512), `:269` (121), `:272-274` (62, 184, 206, 448, 510 : premier
  cycle de `B2`, avant T0), `:278-282` (0x04, T0, T999 et T1000 à 0, 512). [prov. : simulation]
- Garde « A6 » de `Dispose` (`AlundraArcSupport.cs:274`) : aucun contact (`TotalEntityBlockCount` 0), aucune glissade (section M.3).
  [prov. : simulation]

### F.2 A8 — 163 `B[1]` (programme @60), événements de carte, mode 1, aide des arcs

| Boîte | Ouverture auj. → nouv. | Aujourd'hui vu (écart) | Fidèle (relatif à N) | Δ |
|---|---|---|---|---|
| `M163_S000` @117 (2 p.) | 516 → 516 | 519 (3) | premier glyphe N+19, E N+170, T N+173, R et vu N+191 | +188 |
| `M163_S005` @123 (3 p.) | 565 → 753 | 569 (4) | E N+288, T N+291, R N+309 | +305 |
| `M163_S006` @129 (3 p.) | 615 → 1108 | 619 (4) | E N+285, T N+288, R N+306 | +302 |
| `M163_S008` @163 (3 p.) | 833 → 1628 | 837 (4) | E N+350, T N+353, R N+371 | +367 |
| `M163_S009` @169 (2 p.) | 868 → 2030 | 871 (3) | E N+241, T N+244, R N+262 | +259 |

Provenance du tableau : modèle (relatif à N), égal à la simulation ; « aujourd'hui » lu.

- V-71 `AlundraInoaAwakeningArcTests.cs:34` — budget 1300 — budget — nouvelle valeur : 2937 (fin 1026 + 1421 = 2447). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-72 `:200` — @201 à 1025 — (a) — nouvelle valeur : 2446 (et `0x05`/`0x06` @198-@208 à la même image). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-73 à V-77 `:260-264` — `AssertGap` 117→121 3, 123→127 4, 129→133 4, 163→167 4, 169→173 3 — (a) — nouvelle valeur : 191,
  309, 306, 371, 262. [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-78 à V-87 `:276-277` — (123, 565), (129, 615), (135, 665), (143, 690), (157, 815), (163, 833), (169, 868), (181, 913),
  (184, 997), (190, 998) — (a) — nouvelle valeur : (123, 753), (129, 1108), (135, 1460), (143, 1485), (157, 1610),
  (163, 1628), (169, 2030), (181, 2334), (184, 2418), (190, 2419). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- Inchangées : (98, 253), (106, 413), (115, 516) ; les écarts `:226-227` (99, 41), `:236` (25), `:241` (98), `:244` (18),
  `:249` (84), `:257` (27), `:265-271` (46, 46, 46, 31, 31, 11, 241). [prov. : simulation]

### F.3 A20 — 162 `B[7]` (programme @572), événement de carte, mode 1

`M162_S005` @592 (2 pages) : ouverte à 131, vue aujourd'hui à 134 ; fidèle E N+182, T N+185, R et vu N+203 = 334 ; Δ +200. [prov. : aujourd'hui lu et simulation = modèle]

- V-88 `AlundraInoaDayOneArcTests.cs:31` — budget 160 — budget — nouvelle valeur : 402 (fin 335). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-89 `:85` — @596 à 134 — (a) — nouvelle valeur : 334 (`0x05` @597 à la même image). [prov. : simulation = modèle ; avant : aujourd'hui lu]

### F.4 A12 — 179 `B[2]` (programme @328), événements de carte, mode 1 (F ré-ancré)

| Boîte | Ouverture auj. → nouv. | Auj. vu | Fidèle : R et vu | Δ |
|---|---|---|---|---|
| `M179_S029` @369 (3 p.) | 109 → 109 | 113 | N+390 | +386 |
| `M179_S030` @374 | 113 → 499 | 115 | N+160 | +158 |
| `M179_S031` @379 | 115 → 659 | 117 | N+140 | +138 |
| `M179_S017` @384 | 117 → 799 | 119 | N+91 | +89 |
| `M179_S018` @418 | 310 → 1081 | 312 | N+77 | +75 |
| `M179_S019` @425 | 343 → 1189 | 345 | N+102 | +100 |
| `M179_S020` @432 (2 p.) | 366 → 1312 | 369 | N+265 | +262 |
| `M179_S021` @437 (4 p.) | 369 → 1577 | 374 | N+498 | +493 |
| `M179_S032` @442 (2 p.) | 374 → 2075 | 377 | N+201 | +198 |

Provenance du tableau : modèle (relatif à N), égal à la simulation ; « aujourd'hui » lu.

- V-112 `AlundraBergusJumpArcTests.cs:23` — budget 2500 — budget — nouvelle valeur : 2734 (fin 379 + 1899 = 2278). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-113 `:61-62` — `f` = 287 (@411) — (a) — nouvelle valeur : 1058 (S017 vue à 890 + 168) ; `f` ne doit plus être écrit en dur (`const int f = 287`) : `:62-66` et `:78-82` liraient
  d'anciennes images. [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-114 `:66` — @451 à 359 + 19 = 378 — (a) — nouvelle valeur : 2277. [prov. : simulation = modèle ; avant : aujourd'hui lu]
- Inchangées relativement à F : `:63-65` (F+4, F+4 à F+23, F+23), `:75-82` (vol de Bergus). [prov. : simulation]

### F.5 A9 — 172 `C[6]` (programme @504), script d'entité, mode 1, `CloseDialogueWithTheButton`

`M172_S002` @534 (2 pages) ouverte à f530 = 4 ; aujourd'hui fermée à 7 (`frameAfterTheBoxClosed` 8), `0x39` à 95 (attente
d'animation `0x1C`, f530+91) ; fidèle sous l'aide alternée (appuis aux images 5, 7, 9 …) : premier glyphe 22 (N+18), E 102,
T 104, libération 122, `0x39` d'entité 123. [prov. : aujourd'hui lu et simulation = modèle]

- V-115 `AlundraVisionAndCoastArcTests.cs:425` — budget 400 — budget — nouvelle valeur : **400 inchangé** (fin 124 ;
  ⌈1,2 × 124⌉ = 149 < 400, un budget ne baisse jamais). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-116 `:503` — `frameAfterTheBoxClosed <= f530 + 91` — (a) — nouvelle valeur : s'inverse : `frameAfterTheBoxClosed` = 123 =
  f530 + 119. [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-117 `:504` `0x39` @543 — f530 + 91 = 95 — (a) — nouvelle valeur : **95 inchangée** : `FrameOf` rend la première exécution du
  `0x39` (le `0x39` attend de 95 à 123, R4) ; la ligne du brouillon qui la passait à f530 + 119 était fausse. [prov. : simulation = modèle ; avant : aujourd'hui lu]
- V-118 `:505` `0x06` @544 et V-119 `:506` `0x11` @547 — f530 + 91 — (a) — nouvelle valeur : f530 + 119 (123). [prov. : simulation = modèle ; avant : aujourd'hui lu]
- Inchangées : `:490-498` (f530 dans [1, 10], @534, @537, @539 à f530, @541 à f530+91, animations). [prov. : simulation]

### F.6 A10 — 165, valeurs absolues simulées, marges (b)

Chaîne mesurée aujourd'hui : `M165_S027` (Nestus, `C` @935, entité) ouverte 345, `0x39` @938 à 347 ; @943 `0x24` (physique)
de 347 à 478 ; T103 effacé 478 ; `B[2]` @264 ouvre `M165_S030` (événement de carte) et pose T104 à 479 ; Bergus (`C` @804,
relit les drapeaux aux images impaires) @834 à 481, `0x25` @838 de 484 à 503, @843 de 506 à 525, @846 `0x39` et @847 à 525 ;
`B[2]` @276 T102 à 526 ; Wendell (relit aux multiples de 3) ouvre `M165_S017` à 528, @774 à 619 (attente d'animation de 91) ;
`B[2]` @283 T105 à 620 ; Meade (relit aux images impaires) @1005 à 621, @1026 à 774 ; `B[2]` @290 `S016` à 775, `S006` 778,
marches (147 images), `S009` 928, `S000` 949, @354 à 960. [prov. : aujourd'hui lu]

| Boîte | Origine | Aujourd'hui | Fidèle (relatif à N) |
|---|---|---|---|
| `M165_S017` @764 (Wendell, ×2) | entité | fermée N+1, `0x39` N+91 (animation) | vu N+54 < 91 : sans effet |
| `M165_S027` @935 (Nestus) | entité | vu 347 (N+2) | E N+93, T N+96, R N+114, vu N+115 (+113) |
| `M165_S030` @264 | événement de carte | fermée 480 | E N+82, T N+85, R N+103 |
| `M165_S016` @290 | événement de carte | vu N+2 | R N+118 |
| `M165_S006` @296 | événement de carte | vu N+2 | R N+107 |
| `M165_S009` @323 | événement de carte | vu N+2 | R N+120 |
| `M165_S000` @337 (3 p.) | événement de carte | vu N+4 | R N+308 |

Provenance du tableau : modèle (relatif à N), égal à la simulation ; « aujourd'hui » lu.

Chaîne simulée (physique et scripts d'aujourd'hui, boîte fidèle) : `M165_S017` (Wendell) 231 → 284 ; `M165_S027` (Nestus) 345,
E 438, T 441, libération 459 (vue 460) ; `M165_S030` (événement de carte) 592, E 674, T 677, libération 695 (vue par `B[2]` 695,
par Bergus 696) ; `M165_S017` 699 → 752 ; `M165_S016` 947 → 1065 ; `M165_S006` 1066 → 1173 ; `M165_S009` 1321 → 1441 ;
`M165_S000` 1460, E 1747, T 1750, libération 1768 ; @354 à 1775 ; fin 1776. Bergus (`C` @804) quitte `0x35` @827 à 592, puis
`0x02` @830 et `0x37` @804 de 592 à 594 : @834 à **594**, soit 2 images après T104 comme aujourd'hui (479 → 481). `M165_S030`
dure plus que les deux sauts de Bergus : @846/@847, T104, T102, T105 et la fin passent par sa libération, ce qui abandonne la
forme (b) « dernière boîte + écart d'aujourd'hui » (elle donnerait @847 à 740) au profit des valeurs absolues simulées.
[prov. : simulation]

- V-90 `AlundraInoaDayOneArcTests.cs:99` — budget 1100 — budget — nouvelle valeur : **2132** (fin simulée 1776). [prov. : simulation]
- V-91 `:191` — `0x25` @838 de 484 à 503 — (b), marge 2 (Bergus) — nouvelle valeur : de **597 à 616** (20 exécutions).
  [prov. : simulation]
- V-92 `:192` @839 et V-93 `:193` @841 — 503 — (b), marge 2 (Bergus) — nouvelle valeur : **616**. [prov. : simulation]
- V-94 `:194` — `0x25` @843 de 506 à 525 — (b), marge 2 (Bergus) — nouvelle valeur : de **619 à 638**. [prov. : simulation]
- V-95 `:197` — boucle sur @844, @846, @847 à 525 — (b), marge 2 (Bergus) — nouvelle valeur : **638, 638, 696** : @846 est un
  `0x39`, `FrameOf` rend sa première exécution (638), il attend la libération de `M165_S030` vue par Bergus (696) ; `0x06` @847 à
  696 (R4). [prov. : simulation]
- V-96 `:201` — @354 à 960 — (b), marge 0 — nouvelle valeur : **1775** (libération de `S000` vue par `B[2]` + 7). [prov. : simulation]
- V-97 `:250` — T104 effacé (bord) 526 — (b), marge 2 (Bergus) — nouvelle valeur : **697**. [prov. : simulation]
- V-98 `:251` — T102 (posé, effacé) (527, 620) — (b), marge 2 pour le posé (Bergus), 3 pour l'effacé (Wendell) — nouvelle
  valeur : **(698, 791)**. [prov. : simulation]
- V-99 `:252` — T105 (posé, effacé) (621, 775) — (b), marge 3 pour le posé (Wendell), 2 pour l'effacé (Meade, qui relit aux
  images impaires) — nouvelle valeur : **(792, 947)**. [prov. : simulation]
- V-100 `:259-267` — hauteurs de Bergus : repos lu à l'ancre `HeightAfterImage(480)`, vols à 481+i et 503+i, repos à 525 et 526 —
  (b), marge 2 (Bergus) — nouvelle valeur : **ancre `HeightAfterImage(593)` (repos), vols à 594+i et 616+i (liste d'UJ-1
  exacte), repos à 638 et 639 (`:266-267`)** ; la forme à 593+i et 615+i du brouillon est un tick trop tôt. [prov. : simulation]
- Inchangées : `:202`, `:204-207`, `:212-218`, `:221-226`, `:229-232`, `:235` et `:238` (ordre des drapeaux et bornes ; bords de
  `FlagEdges` : T103 effacé 592, T104 posé 593, comptés après l'image), `:279-280` et `:283` (acteurs, `0x06` @774 deux fois).
  `:266-267` ne figurent pas ici : V-100 les ré-épingle (638 et 639). [prov. : simulation]
- Garde « A10 » : aucun contact, aucune glissade (section M.3). [prov. : simulation]

### F.7 A11 — 164 `C[2]` (Septimus, programme @240), scripts d'entité, mode 1, valeurs simulées, marge 2 (Septimus)

Aujourd'hui : `M164_S002` @276 ouverte 5, fermée 8, vue 9 ; T6/T2 à 9 ; héros placé ; `B[1]` @86 à 10 ; `M164_S003` @325 ouverte
11 (T200 posé à l'ouverture) ; @331 `0x4C 4` 11 ; @341/@346 `0x24` (physique) : @350 `0x4D`, @351 `0x4C 3`, @353 à 48 (écart
37) ; `0x0B` @386 : 18 ticks ; @404 atteint et franchi à 150 ; `M164_S004` @417 ouverte 175 (13 pages), fermée 188, vue 189 ;
@437 et @451 à 189. [prov. : aujourd'hui lu]

Fidèle (modèle, physique mesurée inchangée : 37 et 18) : `S002` premier glyphe 23, curseurs 137 et 293 (N+132 et N+288, N = 5),
E 387, T 390, libération 408, vue 409 ; T2 409 ; @86 410 ; `S003` (N3 = 411) T200 posé N3+28, vu N3+29 (@331), @353 atteint
N3+66, curseur N3+92 relâché N3+95, T201 posé N3+97 (vu N3+98), @404 atteint N3+200, E N3+223, T N3+226, libération N3+244, vue
N3+245 = 656 ; `S004` ouverte 681 (+25), E N4+1368, T N4+1371, libération N4+1389, vue N4+1390 = 2071. [prov. : modèle]

Simulé (scripts d'entité) : `M164_S002` 5, E 387, T 390, libération 408 ; `M164_S003` 411, T200 posé au pas 439 (vu 440), `0x4C 4`
440, `0x4D` puis `0x4C 3` 477 (D-E19-62 : le `0x4D` en attente est effacé), curseur `\A` 503 relâché 506, T201 posé 508 (vu 509),
E 634, T 637, libération 655 ; `M164_S004` 681 (13 pages), E 2049, T 2052, libération 2070. Contacts de Septimus (rec1) : arrêté
par rec4 aux images 442 et 443, par le héros aux images 474, 475 et 476 (aujourd'hui 13, 14 ; 45, 46, 47) : mêmes paires, mêmes
positions, 5 pas ; les attentes physiques de Septimus (37 et 18 ticks) sont inchangées. [prov. : simulation]

- V-101 `AlundraInoaDayOneArcTests.cs:292` — budget 240 — budget — nouvelle valeur : **2488** (fin simulée 2073). [prov. : simulation]
- V-102 `:395` @437 189 et V-103 `:405` @451 189 — (b), marge 2 (Septimus) — libération de `S004` vue par `C[2]` + 0 — nouvelle
  valeur : **2071**. [prov. : simulation]
- V-104 `:425` — `FirstSet[200]` = @325 + 1 — (a) (pas du drapeau) — nouvelle valeur : @325 + 29 (440). [prov. : simulation]
- V-105 `:426` — `FirstSet[201]` = `FirstSet[200]` + 1 — (a) avec la physique simulée — nouvelle valeur : `FirstSet[200]` + 69
  (509). [prov. : simulation]
- V-106 (C, 276, 5) — inchangée (ouverture de la première boîte). [prov. : simulation]
- V-107 (C, 289, 9) — (b), marge 2 (Septimus) : `S002` vue + 0 — nouvelle valeur : **409**. [prov. : simulation]
- V-108 (B, 86, 10) — (b), marge 0 (le test place le héros après T2) : `S002` + 1 — nouvelle valeur : **410**. [prov. : simulation]
- V-109 (C, 325, 11) — (b), marge 2 (Septimus) : `S002` + 2 — nouvelle valeur : **411** (`0x37 [1]` @240 lancé au tick de T2, sans
  phase). [prov. : simulation]
- V-110 (C, 353, 48) — (b), marge 2 (Septimus), **dans la vie de `S003`** : @325 + 29 (T200) + 37 (physique) — nouvelle valeur :
  **477**. [prov. : simulation]
- V-111 (C, 417, 175) — (b), marge 2 (Septimus), **libération de `S003` vue + 25** (aujourd'hui @404 était retenu par le script ;
  la forme (b) simple donnerait 817) — nouvelle valeur : **681**. [prov. : simulation]
- Classe L `:364` : 1 pour chacun des 13 couples `0x4C`/`0x4D` → **0** (exécutés, plus sautés). [prov. : simulation]
- Inchangées : `:351`, `:361`, `:369-372`, `:376`, `:379-381`, `:384-386`, `:389-391`, `:394`, `:396-404`, `:407`, `:410-411`,
  contacts et positions de Septimus `:414-422` (« hero », « rec4 » ; (71565312, 7995392) contre rec4, (66650112, 7995392) contre
  le héros, `ForceAdjusted` 1, x ≥ 1041 px à @386). [prov. : simulation]

## G. Classe C — budgets (aucune autre valeur ne doit bouger)

Règle : nouveau budget = max(budget d'aujourd'hui, ⌈1,2 × fin⌉) ; un budget ne baisse jamais. Fin d'aujourd'hui = `Frame` à
`Dispose` (mesuré) ; fin simulée = `Frame` à `Dispose` de la simulation (D-E19-77, plus de borne haute pour T-A19, T-B9, A14, A15,
A18). Δ par boîte : table de l'oracle sous la nouvelle aide (journal `boxes_out.txt` du brouillon).

- V-120 A2 `AlundraVisionArcTests.cs:34` — 1500 — `M476_S101`/`S102`/`S103` (`B[4]`, événements de carte, sous-programme @112
  `4C[2] 50[4] 36 T999 37[60] 51 39`) : Δ +170, +106, +597 (chaîne d'un seul programme) — fin 1209 → 2082 — **2499**
  (⌈1,2 × 2082⌉ > 1500). [prov. : modèle ; fin d'aujourd'hui lue]
- V-121 A4 `:36` et V-122 A4p `:39` — 2500 — huit boîtes `M476_S104` à `S111` (`B[5]`) : Δ +939, +486, +467, +1107, +1002,
  +1066, +1305, +623 — fin 2059 → 9054 — **10865** ; `T1_Map476` (`AlundraAnimationSoundTests.cs:245`, même budget) : fin
  400 → 1339, sous le budget. [prov. : modèle ; fin d'aujourd'hui lue]
- V-123 T-A19 `AlundraEntityContactArcTests.cs:36` — 1700 — onze boîtes d'entités de 185 — fin 1117 → 3359 (simulée) —
  **4031** ; aucune des 37 assertions ne bouge. [prov. : simulation]
- V-124 T-A10v `:136` — 6500 — **aucune boîte ouverte** dans la mesure d'aujourd'hui (5101 images) — **inchangé**. [prov. :
  aujourd'hui lu]
- V-125 T-B9 `:259` — 3200 — six boîtes — fin 2116 → 3634 (simulée) — **4361** ; aucune des 25 assertions ne bouge. [prov. :
  simulation]
- V-126 contre-preuve `:336` — 1500 — **1500 inchangé** ; V-127 sa borne `:340` `arc.Frame < 1400` — **1400 inchangée** :
  sous la boîte fidèle, Septimus fait son dernier pas à l'image 1089 (aujourd'hui 855 : +234, boîte `M10_S028` ouverte 212,
  libération 447) ; la simulation bornée à 1400 tient les six assertions (`:370-375`) ; `FrameLimit` n'est lu par aucune boucle de
  ce test (boucle propre `:340-348`). Décision : bornes gardées (la marge d'aujourd'hui de 545 images serait rendue par 1634 et
  1961, non requis par la simulation). [prov. : simulation]
- V-128 A13 `AlundraDay3SceneArcTests.cs:61` — 1500 — `M176_S012` +156 — fin 344 → 500 — max(1500, 600) = **1500 inchangé**.
  [prov. : modèle ; fin d'aujourd'hui lue]
- V-129 A14 `:100` — 1000 — fin 257 → 755 (simulée) — max(1000, ⌈1,2 × 755⌉ = 906) = **1000 inchangé** ; aucune des 21
  assertions ne bouge. [prov. : simulation]
- V-130 A15 `:150` — 2500 — fin 664 → 1489 (simulée) — max(2500, 1787) = **2500 inchangé** ; aucune des 29 assertions ne bouge.
  [prov. : simulation]
- V-131 A17 `:205` — 2500 — `M135_S001` (`0x50 4`, T0 à la fin de la page 2, choix au tick suivant, `0x51`) : choix à N+255,
  `0x51` N+256, libération N+274, `0x39` N+275 (+272) ; `S002` +154 — fin 732 → 1158 — max(2500, 1390) = **2500 inchangé**.
  [prov. : modèle ; fin d'aujourd'hui lue]
- V-132 A18 `:303` — 4000 — fin 1069 → 3193 (simulée) — max(4000, 3832) = **4000 inchangé** ; aucune des 18 assertions ne bouge.
  [prov. : simulation]
- V-133 `T1_Map178` `AlundraAnimationSoundTests.cs:291` — 4000 — `S019` +103, `S014` +1676 ; le premier `RunUntil` finit à 1971 —
  fin 250 → 1981 — max(4000, 2378) = **4000 inchangé** ; aides `:296`, `:298` : nouvelle aide ; épingles relatives `:306`,
  `:308` inchangées. « @735 du livre déjà passé (vers 343) » est une déduction (fin d'aujourd'hui vers 240 + 103), non lue ;
  le budget tient dans les deux cas. [prov. : modèle ; fin d'aujourd'hui lue]
- V-134 A1 `AlundraShipArcTests.cs:129` — 900 — `M390_S120` (mode 0, événement de carte, aide alternée dès l'image 6) : premier
  glyphe 25, E 47, T 49, libération et reprise des événements de carte à 67 (aujourd'hui 7 : +60) — fin 346 → 406 —
  max(900, 488) = **900 inchangé** ; `:165` `Frame < 900` inchangée. [prov. : modèle ; fin d'aujourd'hui lue]
- V-135 A1c `:194` — 900 — même boîte (+60) — fin 326 → 386 — max(900, 464) = **900 inchangé** ; `:207` inchangée. [prov. :
  modèle ; fin d'aujourd'hui lue]
- V-136 A10J `AlundraHeroJumpArcTests.cs:25` — 450 — `M10_S061` +46 — fin 342 → 388 — max(450, ⌈1,2 × 388⌉ = 466) = **466**.
  [prov. : modèle ; fin d'aujourd'hui lue]
- V-137 à V-141 `AlundraSaveBookEndToEndTests.cs` `:165` (200), `:170` (300), `:182` (400), `:191` (200), `:198` (300) —
  **inchangés** : la question arrive au 62e tick et l'écran 61 ticks après OUI, sans attendre la libération de la boîte
  (`Tick` 134 depuis l'ouverture) ; risque R2. [prov. : modèle]
- V-141b `AlundraSaveScreenDirectorTests.cs:782-832`, `:834-871` — `BookTick(62)` ×2, boucles `< 400` et `< 100`, `BookTick(60)` —
  **inchangés** (le choix de l'écran arrive au `Tick` 185, après la libération 134) ; risque R2. [prov. : modèle]
- `T1_Map476` (`:238-270`) : budget de A4p ; une boîte traversée avant @553 (`S104`) ; `S105` s'ouvre dans l'image de @553, comme
  aujourd'hui ; épingles `:266`, `:267`, `:269` inchangées. Gardes de `Dispose` « A4p » et « A18 » inchangées. [prov. : modèle]

## H. Aides de test

- `AlundraArcSupport.cs:468-486` — aide des arcs — nouvelle règle A.2 ; elle a besoin d'un accès en lecture seule « attend un
  appui » (F2-R6) ; `Press` et `HoldDirections` gardent leur sens (`AlundraPrefabArcSupportTests.cs:197-218` inchangé si l'aide
  tient Carré par un champ à elle).
- `AlundraArcSupport.cs:450-462` — `CloseDialogueWithTheButton` — **inchangée** (A1, A1c, A9 se ferment ; valeurs F.5, V-134,
  V-135).
- A17 `AlundraDay3SceneArcTests.cs:211-234` et contre-preuve T-B9 `AlundraEntityContactArcTests.cs:340-348` — même règle que
  l'aide (A17 garde sa sélection du choix au début de l'image).
- Harnais `IntroTraceHarnessTests.cs:587-590` — miroir de `TickPad` mis à jour avant la passe de la boîte depuis
  `LastPadState.ButtonsHold` ; valeurs section E.

## I. Choix de conception (tranchés ici, valeur avec le choix)

- V-142 `AlundraDialoguePresenterWiringTests.cs:179-180` et V-143 `:257-258` — écran poussé dès `Open` — choix : l'`Open` envoie
  au présentateur le préfixe vide (F2-R6 : la boîte apparaît, glissement dès N+1) — **inchangées**. [prov. : conception ; avant : aujourd'hui lu]
- V-144 `AlundraDialogueOutOfBandCloseTests.cs:67-68` — fermeture par la fenêtre — choix : remise à zéro immédiate, comme
  `InstallForMapEntry` (pas de glissement de sortie) — **inchangées** ; sinon `IsOpen` faux et masque à 0 18 passes après. [prov. : conception ; avant : aujourd'hui lu]
- V-146 `AlundraSaveBookTests.cs:211`, `:305` (`AssertReleased` après `NotifyPresenterClosed()`) — même choix — **inchangés**. [prov. : conception ; avant : aujourd'hui lu]
- V-145 `AlundraAnimationSoundTests.cs:338` — liste exacte `[204]` — choix F2-R5 : le directeur reçoit le lecteur du monde à
  l'installation (avant `Inject`) — **inchangée** ; sinon `[204, 6]` (`M179_S029` ouverte à l'image 109, mesurée ; son 6 au tick
  N ; premier glyphe à 128, après la fin à 122 : aucune voix). [prov. : conception ; avant : aujourd'hui lu]
- Tests du présentateur qui supposeraient une ligne par page : aucun ne passe par le directeur, hors V-16 et V-142/143
  (`AlundraDialoguePresenterWiringTests.cs:67-103`, `AlundraFont3GlyphTests.cs:368-517`, `AlundraYarnBindingsTests.cs:143-302`
  appellent `ShowLine` ou le coureur directement) : classe U.

## J. Classe L (rappel, valeurs de liste)

- Les 32 lignes `0x4C`/`0x4D` de `Data/story-chain-skipped-opcodes.tsv` retirées (règle 2, `AlundraStoryChainSkippedOpcodesTests.cs:49-55`) ;
  `TheMapsWithTwoProgramsAtOnePcAreTwoLines` (`:88-94`) : compte de la 476 @112 de 2 à 0. [prov. : aujourd'hui lu]
- A6 `AlundraShipBlockArcTests.cs:38` : (0x4C, 706) retiré ; `:186` : 3 → 0. [prov. : aujourd'hui lu]
- A11 `AlundraInoaDayOneArcTests.cs:356-364` : plus aucun `0x4C`/`0x4D` sauté (ensemble vide). [prov. : aujourd'hui lu]
- `AlundraEventProgramRunnerTests.cs:322-334` : `0x4C` n'est plus sauté par sa taille (porté). [prov. : aujourd'hui lu]

## K. Tests nouveaux de F2A-3 (valeurs écrites d'avance)

Cas canonique : `bonjour`, mode 1 sauf mention, drapeaux par défaut, sans manette sauf mention, une image = un tick, N = 10.

- K-1 Ouverture par un script d'entité (avant la passe) : première mise à jour 10 (N), premier glyphe 28 (N+18), E 56,
  minuterie T 416, libération 434, `0x39` du script d'entité 435 (T+19). Même texte par un événement de carte ou un déclencheur
  en attente : première mise à jour 11, premier glyphe 29, E 57, T 417, libération et `0x39` 435 (T+18). [prov. : modèle]
- K-2 Libération d'une boîte `MenuOpen` vue par un `0x39` d'événement de carte : mode 0, ouverture par un événement de carte à 10,
  appui vu à 58 (E+1) : T 58, libération 76, `0x39` à 76 (T+18, la porte est relue après la passe du même tick). [prov. : modèle]
- K-3 Entrelacement sur une image de rattrapage (3 ticks par image, tick t = 3·image + j) : mode 0 ouverte par un événement de
  carte au tick 0 ; première mise à jour au tick 1 ; E au tick 47 ; maintien écrit pour l'image 16 (front au tick 48) ; appui vu
  au tick 49 (image 16, j = 1) : T 49 ; libération au tick 67 (image 22, j = 1) ; événements de carte de l'image 22 : gelés à
  j = 0, tournent à j = 1 (leur `0x39` rend 1 au tick 67) et j = 2 ; déclencheurs en attente de l'image 22 : les 3 itérations
  tournent (l'itération 0 la voit tôt) ; scripts d'entité : image 23. [prov. : modèle]
- K-4 `0x51` posé par un script d'entité au tick k = 100 (masque 4 posé à l'ouverture) : E 56, T 100 (= k), libération 118,
  `0x39` d'entité 119. Par un événement de carte : E 57, T 101 (= k+1), libération et `0x39` 119. [prov. : modèle]
- K-5 Frappe finie pendant un choix en attente : ouverture par un script d'entité à 10, masque 4, `0x44` posé au tick 30 :
  glyphes 28, 32, 36, 40, 44, 48, 52 (la boîte tape pendant le choix), E 56 ; résultat de `0x44` et `0x51` au tick 90 : T 90,
  libération 108, `0x39` 109. [prov. : modèle]
- K-6 Préfixe reçu par le présentateur, `ab\Ncd` ouvert par le test (N = 0) : `ShowLine` à l'`Open` (vide), puis à la passe 19
  (`a`), 23 (`ab`), 31 (`ab` / `c`), 35 (`ab` / `cd`) ; le pas `\N` (27) n'envoie rien ; `CurrentLineForTests` rend `ab\ncd`
  pendant toute la frappe. [prov. : modèle]
- K-7 Ouverture par un script d'entité à 10 suivie d'un appui vu à E+1 = 57 : T 57, libération 75, `0x39` d'entité 76. [prov. : modèle]

## M. Boîtes simulées et gardes (D-E19-77)

### M.1 Provenance de la simulation

**(simulation)** : valeur lue sur la simulation hors dépôt `f2a-sim/` : copie des sources d'`Alundra/` à `87b4f02` où ne changent
que (1) l'ordre d'un tick de F2-R1 dans `AlundraWorldProxy.Update` (la passe de la manette note Carré à chaque tick ; une boucle
des ticks qui tourne toujours : passe de la boîte sur le Carré du tick k−1, porte relue, événements de carte puis recyclage ;
déclencheurs en attente après la boucle, porte relue ; l'ancienne passe de dialogue retirée), (2) un directeur bouchon : port
C# du modèle de la boîte (`model.py` corrigé de D-E19-62 et D-E19-63), qui lit la page Yarn exportée (`LineTexts`, F2-R3), avec
`0x4C` à `0x4F` portés (F2-R4), `0x50`, `0x51` en verrou, `0x39` sur la libération, drapeaux posés à leur pas, sons 6/7 et voix
notés, (3) la manette des arcs de A.2 dans l'aide `RunUntilPressingTheButtonOnEveryDialogueFrame` et dans la boucle de la
contre-preuve de T-B9. Scripts, physique, contacts, animations : code d'aujourd'hui. Une image = un tick (aucune image de
rattrapage dans ces arcs).

Validation : le port reproduit l'oracle Python à l'identique (59 scénarios, 475 parcours aléatoires, octets de 475 nœuds) ; les
valeurs « pures boîte » de l'annexe sont retrouvées dans la simulation complète (A8 @201 2446, A20 @596 334, A12 F 1058 et @451
2277, A9 `0x06`/`0x11` 123, A6 @761 1899 et @540 2463) ; la même copie avec le directeur et l'ordre d'aujourd'hui rejoue les 13
tests sans écart (fins 1150, 1026, 135, 96, 961, 191, 379, 1117, 2116, 1400, 257, 664, 1069) ; seconde exécution identique à
l'octet (contre-vérification).

### M.2 Boîtes des arcs simulés (ouverture → libération)

- A8 (163) : `M163_S000` 516 → 707, `S005` 753 → 1062, `S006` 1108 → 1414, `S008` 1628 → 1999, `S009` 2030 → 2292 ; fin 2447.
  [prov. : simulation = modèle]
- A20 (162) : `M162_S005` 131, E 313, T 316, libération 334 ; fin 335. [prov. : simulation = modèle]
- A9 (172) : `M172_S002` 4, premier glyphe 22, E 102, T 104, libération 122 ; fin 124. [prov. : simulation = modèle]
- T-A19 (185, scripts d'entité) : `M185_S032` 331 → 391, `S025` 454 → 546, `S026` 594 → 798, `S001` 834 → 1146, `S027` 1179 →
  1377, `S014` 1442 → 1845, `S028` 1878 → 2008, `S002` 2043 → 2276, `S018` 2311 → 2410, `S029` 2443 → 2654, `S030` 2717 → 3060 ;
  fin 3359. [prov. : simulation]
- T-B9 : `M10_S028` 212 → 447, `S032` 1090 → 1807, `S034` 2000 → 2238, `S035` 2443 → 2597, `S033` 2775 → 2984 (`0x4C 4` 2840,
  `0x4C 3` et `0x4D` 2929, T513 posé 2839), `S043` 3321 → 3385 ; fin 3634. [prov. : simulation]
- A14 : `M179_S023` 31 → 408, `S027` 473 → 530, `S028` 531 → 618 ; fin 755. [prov. : simulation]
- A15 : `M176_S006` 181 → 237, `S007` 433 → 696, `S008` 698 → 1126, `S009` 1170 → 1255 ; fin 1489. [prov. : simulation]
- A18 : `M178_S019` 1 → 105, `S014` 278 → 1969, `S015` 2443 → 2525, `S011` 2648 → 2797, `S017` 2909 → 3026 ; fin 3193.
  [prov. : simulation]
- A6, A10, A11 : voir F.1, F.6 et F.7.

### M.3 Gardes « aucun contact » (D-E19-77)

Simulé, à la fin de chaque arc (`TotalEntityBlockCount`, `AlundraArcSupport.cs:274`, `:277`) et image par image.

| Arc | Contact | Glissade |
|---|---|---|
| A6 | aucun (0) | 0 |
| A8 | aucun (0) | 0 |
| A9 | aucun (0) | 0 |
| A10 | aucun (0) | 0 |
| A20 | aucun (0) | 0 |
| A15 (`AlundraDay3SceneArcTests.cs:193`, `Assert.Equal(0, arc.TotalEntityBlockCount)` ; `:187` est un commentaire) | aucun (0) | 0 |

Provenance : simulation. Arcs hors garde, mêmes paires et mêmes positions qu'aujourd'hui, images décalées : A11 (F.7) ; T-A19
rec6 contre rec7 à 95 et 96 (avant toute boîte, inchangé), le héros contre rec7 à 3084, 3085 et 3086 (aujourd'hui 842 à 844) ;
T-B9 rec39 contre rec41 à 1936, le héros contre rec41 à 1966, 1967 et 1968, contre rec39 à 1989 (aujourd'hui 991, 1021 à 1023,
1044) ; A14 glissade du héros à 473 (aujourd'hui 107), le héros contre rec0 à 753 et 754 (aujourd'hui 255, 256) ; A18 glissade
à 278 (aujourd'hui 175), le héros contre rec2 à 2850 à 2852, contre rec5 à 2873 à 2875 (aujourd'hui 842 à 844, 865 à 867),
garde « A18 » une glissade : 1. [prov. : simulation]

## L. Arrêts levés, choix tranchés et risques

- **S1 — A6, branche T1000 atteinte : résolu** (F.1) : `:192` {260, 335, 342, 721} (V-70b), `:208` 16 (V-70c), 321 fois,
  ticks 1419 à 1739 ; aucune autre assertion d'A6 ne bouge hors celles de F.1 ; garde « A6 » : aucun contact.
- **S2 — A10, chemin critique changé : résolu** (F.6) : valeurs absolues simulées, marge écrite sur chaque ligne ; la forme (b)
  simple n'est plus utilisée pour A10.
- **S3 — A11, deux lignes hors de la forme (b) : résolu** (F.7) : (C, 353) 477 et (C, 417) 681 ; physique et contacts de Septimus
  inchangés (vérifié par la simulation).
- **S4 — gardes de contact : résolu** (M.3) : aucun contact dans A6, A8, A9, A10, A20, A15.
- **S5 — budgets en borne haute : résolu** (G, M.2) : fins simulées pour T-A19, T-B9, A14, A15, A18 ; aucune valeur de classe C
  ne bouge.
- **S6 — commandes de page Yarn : tranché** (V-17) : `<<flag 20>>` à la passe 93.
- **S7 — scénarios des tests synthétiques** (section C) : les instants d'appui proposés (au plus tôt : E+1, curseur+1) sont un
  choix de réécriture ; les valeurs sont celles de ces scénarios.
- **R1 — 476** : les drapeaux 1001, 1002, 1004, 1005 de `S104` à `S111` tombent au milieu du texte ; `B[2]` @288 les relit tous les
  33 ticks (fondus `0xAF`, effets) : moments décalés dans A4/A4p/`T1_Map476` ; aucune épingle de classe C identifiée qui les lise.
- **R2 — écran de sauvegarde** : la libération de la boîte du livre (`Tick` 134 depuis l'ouverture) efface aussi `MenuOpen`, posé
  par l'écran vers le `Tick` 124 (`AlundraDialogueDirector.cs:293` ; le binaire efface aussi les bits 0x18 à `DialogClosed`) ;
  aucune assertion ne le lit.
- **R3 — T-A10v** : la mesure d'aujourd'hui n'ouvre aucune boîte ; le recensement l'attendait (boîtes `C`).
- **R4 — épingles sur un `0x39`** : deux épingles d'arcs visent un `0x39` (A9 `:504` @543, A10 `:197` @846) : `FrameOf` rend la
  première exécution, pas la libération ; les valeurs de V-117 et V-95 en tiennent compte.

