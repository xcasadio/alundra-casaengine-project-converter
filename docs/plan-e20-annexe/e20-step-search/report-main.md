# Rapport : recherche du pas du binaire (`ComputeXYPosition` `0x80037730`) et écarts O-G3-3 / O-E20-24

Je n'ai lancé aucun build ni test, et je n'ai écrit aucun fichier du dépôt. Tout est sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/step-search-disc/` (désigné `SD/` plus bas).

Étiquettes : FAIT = lu dans le code, le plan ou une annexe. MESURÉ = rejoué sur `ALUN_CD.EXE` (France), ou sur mon modèle rejoué contre le vrai code. INFÉRENCE = non exécuté. La DLL et le moteur n'ont pas été exécutés : tout ce qui les concerne est FAIT (lecture) ou INFÉRENCE.

## 0. Synthèse

**S1.** Le binaire n'avance pas axe par axe. Il tente le vecteur (ForceX, ForceY) en entier, puis le divise par deux pour les deux axes à la fois.
- Un pas accepté part de la nouvelle position avec la moitié du reste. Un pas refusé repart de l'ancienne position avec la moitié.
- Un pas entier accepté au premier essai termine tout de suite.
- Le retour de la fonction est `s4` tel qu'il était **en tête de la dernière tentative**, soit le verdict « entité » de l'avant-dernière (confirme la lecture du critique de g1b). C'est ce que `MoveEntity` range en `+0x130`.
- `ForceAdjusted` (`+0x13C`) n'est levé que si aucun sous-pas n'a été accepté.
- J'ai écrit un modèle de la fonction et de ses quatre auxiliaires, rejoué contre le vrai code sur 55 000 vecteurs et 9 120 ticks de scénario. 0 écart. Couverture : 449/449 instructions de `ComputeXY`, 100/100, 41/41 et 86/86 pour les autres, 164/168 pour la hauteur du sol (les 4 restantes sont inatteignables). 14 mutants du modèle sur 14 sont tués.

**S2.** Les écarts du moteur et de la DLL à la fonction du binaire sont de sept ordres : division du pas, contact, `ForceAdjusted`, vérité flottante de la position, branche « marche au sol », règle de mur, glissement. Le détail est en §3.

Deux écarts sont mesurés sur une structure précise.
- **Reste entier du demi-pas.** Aux directions positives (est, sud) le binaire s'arrête 0 à 6 unités brutes avant le contact exact, puis finit au tick suivant. Aux directions négatives (ouest, nord) il n'y a aucun déficit. Un contact exact, comme celui du moteur et de la DLL, décale `ForceAdjusted` d'un tick dans 18 à 33 % des phases, et le contact d'un tick dans 50 à 66 % des phases hors branche sol.
- **Position flottante.** La DLL relit `PosX` depuis la racine `float32` (`EntityScriptProxy.cs:2668-2670`). Au-delà de 256 px un résidu d'une unité brute n'est plus représentable : à x = 349 px l'unité 22872063 devient 22872064.

**Verdict sur la « racine commune ».** Oui pour O-G3-3, pour W4.5, pour D-E20-38 / D-F4-9 / Q-K2 et pour R-EL-5 : c'est le reste entier de la recherche par moitiés et la sémantique du retour. Mon modèle prédit seul les 12 départs des clés gelées de `ArrowLifeTests.E7` et `ChargedArrowLifeTests.C7`, et les 10 lignes de la table du tir à bout portant de t2-proj. Pour O-E20-24 la recherche est nécessaire pour les nombres exacts mais n'est pas la cause du défaut. La cause est que `Move` avant l'intégration au monde ne teste rien, ce qui fait entrer la flèche dans le mur.

**S3.** Un correctif fidèle doit porter toute la boucle et aussi rendre à la DLL la vérité entière de XY. Une tranche moteur seule (option du contrôleur, ou « résolveur tenu par le jeu ») ne peut pas être exacte, parce que la racine du moteur est en `float32`. Ma recommandation est un déplaceur DLL (`BinaryXyMover`) en entiers bruts, câblé par étapes (entités marquées, puis héros), avec un test de rejeu du modèle. Il inverse plusieurs réponses déjà données par l'auteur, donc il demande son accord (§5).

**S4.** Questions à l'auteur, avec défaut « comme l'original » (§6) : porter la recherche entière ou non, périmètre des entités, division conjointe du pas en diagonale, où loger le déplaceur, et le sort du défaut moteur « `Move` non testé avant l'intégration ».

## 1. S1 : l'algorithme exact (FAIT, listing `SD/lst/computexy_full.txt`)

**Champs de l'entité**
- Drapeaux `+0x6C` : `0x100` gravité, `0x2000` NoObstacleSlide, `0x80` collidable, `0x08` ClassB, `0x01` ClassA.
- Direction `+0x8C`, forces finales `+0xE4` (X) et `+0xE8` (Y), position `+0x114`/`+0x118`/`+0x11C`.
- Force Z `+0xB8`, hauteur de terrain `+0x138`, contact `+0x130` (écrit par l'appelant), `ForceAdjusted` `+0x13C`.
- Position modifiée `+0x1D8`/`+0x1DC`/`+0x1E0`, décalages `+0x1E4`/`+0x1E8`/`+0x1EC`, tailles `+0x1F0`/`+0x1F4`/`+0x1F8`.
- Pointeurs de cellules `+0x148` + 4k, hauteurs de coins `+0x158` + 4k.

**Variables locales**
- `s2` et `s1` : copies de ForceX et ForceY, qu'on divise.
- `s4` : dernier verdict « entité » écrit.
- `s5` : compteur de tentatives.
- `[sp+0x28]` : glissement déjà appliqué.
- `[sp+0x2C]` : un sous-pas a été accepté.
- `[sp+0x24]` : `(dir & 7) == 0`.
- `[sp+0x34]` : valeur de retour.

**Entrée.** La fonction de mur est `0x80037488` pour le héros (`0x80127D30`) et `0x800373E4` sinon. Si les deux forces sont nulles, la fonction rend 0 sans rien changer et sans lever `ForceAdjusted` (elle recalcule seulement `+0x138` et la boîte `+0x1D8…`, `0x80037DC0`).

**Une tentative** (`0x800377C0`)
1. Sauver la position (`s7`, `fp`, `[sp+0x30]`) et `[sp+0x34] = s4`.
2. Remettre à 0 les quatre drapeaux de coins `[sp+0x10..0x1C]`.
3. `PosX += s2`, `PosY += s1`, recalculer la boîte, appeler `0x800370C4` (hauteur de sol), qui range aussi les cellules et hauteurs de coins. Le résultat va en `+0x138`.
4. **Branche « marche au sol »** (`0x80037834-0x80037884`), si `+0x6C & 0x100`, `ForceZ == 0` et `|terrain − (PosZ+ModZ) − 1| < 0x30000` (ou `0x30003` si le terrain est plus bas) :
   - `PosZ = terrain + 1`, recalcul de la boîte, puis test d'entité (`0x80036F34`) à ce Z accroché.
   - Si ce test est libre, on saute au test de mur à `0x8003791C` avec `s4` **inchangé**.
   - Si le test trouve une entité, on remet le Z d'origine et on continue comme hors branche (cas suivant).
5. Hors branche, `0x80037908-0x80037914` fait `s4 = FindEntityCollisionCandidate(...)`. Si `s4 != 0` la tentative échoue. Sinon le test de mur détermine l'acceptation.
6. **Refus** (`0x80037938`) : restaurer X, Y, Z, puis `s2 = (s2 == -1) ? 0 : s2 >> 1`, idem `s1` (décalage arithmétique, les négatifs arrondissent vers −∞). Un pas cardinal continue tant que l'un des deux est non nul. Un pas oblique continue tant que les deux sont non nuls. Sinon sortie en `0x8003799C`.
7. **Acceptation** (`0x80037D68`) : `[sp+0x2C] = 1`. Si `s5 == 0` : retour immédiat de `[sp+0x34]` (qui vaut 0), sans recalcul. Sinon halving, même test de continuation, puis tentative suivante depuis la nouvelle position.

**Sortie en `0x8003799C`**
- Si un sous-pas a été accepté : recalcul de la boîte et de `+0x138`, retour de `[sp+0x34]`. `ForceAdjusted` reste à 0.
- Sinon `ForceAdjusted = 1` dans quatre cas : glissement déjà fait, NoObstacleSlide, `s4 != 0` (le dernier `s4`, pas celui sauvé), direction ≥ `0x20`.
- Sinon, le glissement par la table `0x80023734` (32 entrées) :
  - Il lit les drapeaux de coins de la **dernière** tentative qui a atteint le test de mur. C'est un pas de 1 unité, ce que `BlockedCorners` de la DLL imite.
  - Il aboutit à `ForceAdjusted = 1` ou à une réécriture de ForceX/ForceY (annulation de l'axe bloqué, ou ±`0xC000` / ±`0x8000` de côté). Dans ce second cas la boucle **repart** avec `s4 = 0`, `s5 = 0`, et `[sp+0x28] = 1`.
  - Si la réécriture annule les deux composantes, la fonction rend 0 sans lever `ForceAdjusted`.

**Test de mur non-héros** (`0x800373E4`) : coin bloqué si `(attr_u16 & (0x40 | 0x01 si ClassB | 0x1000 si ClassA)) != 0`, ou si `hauteur_coin >= ModdedPosZ`.

**Test de mur du héros** (`0x80037488`) : mêmes règles, plus deux ajouts, et toutes les données viennent de l'entité héros.
- Si `g_gravityFlag < 2` et `(attr & 0xE00) == 0x800`, le coin est bloqué.
- Si le minuteur de verrouillage vaut `0x20`, le coin est bloqué si `ModZ != hauteur+1` ou `(attr & 0xE00) != 0x600`.

**Test d'entité** (`0x80036F34`) : premier de la liste des collidables, dans l'ordre, dont la boîte recouvre celle du sujet. Le recouvrement est semi-ouvert, à la `(taille<<16)-1`.
- Le sujet doit avoir `0x80`, ne pas avoir `+0xB4 & 0x80`, et ne pas avoir de porteur (`+0x28`).
- Aucun autre test de genre ni de statut sur le candidat.

**Nombre de tentatives, pire cas** (MESURÉ, `SD/out/attempts.txt`)
- Est 2,4375 px (159744) : 18 tentatives. Ouest : 19. 3 px : 18. 6 px : 19. 1,21875 px : 17.
- Diagonale (56473, 37652) : 16 à 17. 7 unités brutes : 3.
- La dernière tentative d'une recherche est toujours un pas de ±1 unité. Le retour correspond donc à la tentative de 2 ou 3 unités.

**Ce que valent contact et ForceAdjusted** (MESURÉ sur 12 000 vecteurs, `SD/out/studyG.txt`, classes déduites de la trace du modèle)

| Cas | Contact `+0x130` | `ForceAdjusted` |
|---|---|---|
| Aucune force | 0 | inchangé |
| Pas entier accepté au premier essai | 0 | inchangé |
| Recherche avec au moins un sous-pas accepté, hors branche sol | verdict entité de l'avant-dernière tentative | 0 |
| Même cas dans la branche sol | l'entité dès qu'une tentative **antérieure** (hors la dernière) a été bloquée : `s4` n'est jamais remis à 0 | 0 |
| Aucun sous-pas accepté : NoObstacleSlide, ou `s4 != 0` final, ou direction ≥ `0x20` | sauvé en tête de la dernière tentative | 1 |
| Aucun sous-pas accepté, glissement par la table | issu de l'épisode relancé | 1, ou 0 si le glissement annule les deux forces |

Pour un mobile de statut « entité » le contact du binaire est donc souvent nul au premier tick raccourci (voir §4.2).

**Preuves de la fidélité du modèle**
- `SD/work/xymodel.py` est écrit depuis le désassemblage, pas depuis la décompilation C#. `SD/work/xyfuzz.py` le rejoue contre le vrai code et compare le retour plus les 0x294 octets de chaque entité.
- Rejeu : 4 × 8 000 génériques plus 3 000 + 4 × 5 000 ciblés « branche sol / caisses », soit 55 000 vecteurs, 0 écart. Plus 9 120 ticks de scénario (`SD/work/spotcheck.py`), 0 écart.
- Couverture : voir S1 ci-dessus. Les 4 instructions inatteignables de `0x800370C4` sont `0x80037210`, `0x80037214`, `0x80037224` et `0x80037228` (défauts d'un `switch` sur une pente déjà dans 1 à 3).
- Mutation (`SD/out/xymut_out.txt`), 14 mutants tués :
  - retour pris sur le dernier `s4` au lieu du sauvé ;
  - demi-pas de −1 non spécial ;
  - `s4` écrit ou remis à 0 dans la branche sol ;
  - tolérance du sol symétrique ;
  - `ForceAdjusted` levé malgré un sous-pas accepté ;
  - glissement autorisé deux fois ;
  - premier essai accepté qui continue à diviser ;
  - règle du minuteur de verrouillage supprimée ;
  - exemption du porteur supprimée ;
  - NoObstacleSlide ignoré ;
  - Z non restauré après refus ;
  - boucle oblique qui continue si une seule moitié est non nulle ;
  - `s4` non conservé d'une tentative à l'autre.

## 2. S2 : ce que fait le moteur, ce que lit la DLL

**Moteur** (`CasaEngineMonogame/CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs`, FAIT)
- `Move` (`:438-471`) appelle `MoveWithCollisions` (`:1045-1114`).
- Porte d'entrée `:1056-1061` : si `!TryResolveCollisionDependencies` (`:1601-1627`, faux dès que `Owner.World.PhysicsWorld == null`), le moteur fait `Position += requestedDisplacement` **sans aucun test**. C'est le trou d'O-E20-24.
- Sinon, `ResolveHorizontalDisplacementAgainstField` (`:1134-1200`) traite l'axe h1 (X), puis l'axe h2 (Y) **depuis la position avancée**. Pour chaque axe il appelle `IsCandidateBlocked` (`:1307-1326`) : la sonde d'entité de la DLL d'abord, puis le champ de cellules. Les deux axes sont indépendants.
- Si un axe est bloqué, `AdvanceBlockedAxisToContact` (`:1231-1298`, ADR-0045 du moteur) fait :
  - une sonde à un ULP ;
  - 24 itérations de bissection flottante sur `t ∈ [0,1]` (`:17`) ;
  - jusqu'à 4 retraits d'un ULP (`:18`).
- Le contact rendu est « l'obstacle du **dernier** test bloqué » de l'axe, h2 avant h1 (ADR-0047 du moteur). Il est non nul sur tout tick raccourci par une entité.
- Le test de champ `IsHorizontalMoveBlocked` (`:1328-1363`) bloque un coin si le sol est absent, non marchable pour le masque, ou si `GroundHeight > pied + StepHeight`.

**DLL** (FAIT)
- `ScriptedMotion.RunOneKinematicTick` (`:635-707`) appelle `ClampStepToMapEdge` (entités marquées seulement) puis `MoveControllerAndPullPosition` (`:2642-2689`) puis `SlideAlongWall` (`:853-964`).
- `MoveControllerAndPullPosition` envoie le déplacement en pixels flottants `Δ/65536f`. Le contact devient `XCollisionEntity = (H2Obstacle ?? H1Obstacle)?.GameplayProxy` (`:2654-2661`), écrit à chaque pas pour tout mobile, même si le pas avance.
- `ForceAdjusted = 1` si, sur un axe, la demande dépasse 0,01 px (655 unités brutes) et le résultat est inférieur ou égal à 0,01 px (`AxisMadeNoProgress`, `:2796-2800`, seuil `:2791`).
- `PosX/PosY = round(root × 65536)` : la vérité de la position est la racine `float32`. `PosZ` n'est pas relue si l'entité est portée ou marquée (`:2683-2686`).
- La sonde d'entité (`MovementObstacleProbe.cs:40-59`) convertit la racine candidate en brut et teste au `PosZ` logique, sans le Z accroché de la branche sol.

**Liste précise des écarts** (la DLL qui suit le moteur partage les écarts 1 à 5)

1. **Division du pas.** Binaire : dichotomie entière par moitiés, conjointe en X et Y. Moteur : bissection flottante par axe. Position finale :
   - Sur un mur de cellules, déficit du binaire vers l'est ou le sud : 0 à 3 unités (pas 2,4375 px), 0 à 1 (pas 3 et 6 px), 0 à 6 (composantes de diagonale). MESURÉ, `SD/out/studyA2.txt`.
   - Vers l'ouest et le nord : déficit nul pour les quatre forces testées, 1 028 départs chacune.
   - Mon modèle `float32` du moteur donne un déficit nul partout. INFÉRENCE sur le moteur réel, car j'ai supposé le prédicat exact à la frontière.
   - O-E19-28 / D-E19-58 est l'écart de division conjointe déjà accepté. Je ne le chiffre pas ici : ce n'est pas la source du déficit mesuré.
2. **Contact.**
   - Binaire : une seule valeur, celle de l'avant-dernière tentative. Hors branche sol (en l'air, ou debout sur une caisse) elle est non nulle au premier tick raccourci dans 34 à 50 % des phases est/sud (§4.2). Dans la branche sol, elle est collante et vaut l'entité dès le premier tick raccourci.
   - Moteur et DLL : toujours l'entité au premier tick raccourci.
3. **ForceAdjusted.**
   - Binaire : levé au tick qui suit l'arrivée exacte, soit +1 ou +2 ticks après le premier tick raccourci.
   - DLL : toujours +1, sauf distance ≤ 655 unités, où c'est +0.
   - `SlideAlongWall` ne s'applique que si TargetDirection et la force « s'accordent » (`:855-885`), alors que le binaire glisse selon la table de la direction.
4. **Vérité flottante de la position.** Voir S2 plus haut. Les tests de héros portent une tolérance de 16 unités brutes (`HeroSwimTests.cs:65-68`, `FloatUnits = 16`, et `HeroRunTests` G9) à cause de cela. À x = 349 le résidu d'une unité de l'arc E7/C7 est irreprésentable. MESURÉ (calcul `float32` en numpy).
5. **Naissance (O-E20-24).** Avant l'intégration au monde, le moteur n'applique aucun test (FAIT, `:1056-1061`). `ClampStepToMapEdge` fonctionne, lui, avant intégration.
6. **Branche « marche au sol ».** Le Z accroché est déjà porté, mais **après** le pas (`SnapAtApex` dans `ScriptedMotion.cs:578-609`). La DLL teste donc l'entité et le mur au Z courant, jamais au Z accroché. INFÉRENCE d'effet : seule une différence de Z de quelques pixels (marche, pente) change l'issue.
7. **Règle de mur.** Binaire : `(attr & masque) != 0 || hauteur_coin ≥ ModZ+PosZ`, sans `StepHeight`, avec les ajouts du héros ci-dessus. Moteur/DLL : marchabilité par le masque, puis `GroundHeight > pied + StepHeight`. Même masque (`CellsCollisionField.cs:147-162`). La règle de hauteur diffère dans la forme ; je n'ai pas chiffré son effet.

## 3. Mesures sur le comportement du binaire (modèle rejoué contre le vrai code)

### 3.1 Déficit et ticks du contact exact (`SD/out/studyA.txt`)
Mur de cellules à l'est, contact exact PosX = 109,0, boîte de héros.

| Pas | Déficit à l'est | Premier `ForceAdjusted` |
|---|---|---|
| 159 744 (2,4375 px) | {0:845, 1:104, 2:53, 3:26} | tick 2 : 82 %, tick 3 : 18 % |
| 196 608 (3 px) | {0:685, 1:343} | tick 2 : 67 %, tick 3 : 33 % |
| 393 216 (6 px) | {0:685, 1:343} | tick 2 : 67 %, tick 3 : 33 % |
| 106 496 (1,625 px) | {0, 1, 2} | ticks 2, 3, 4 : 62 %, 31 %, 8 % |
| 56 473 | jusqu'à 6 | ticks 2, 3, 4 |

### 3.2 Contact contre une caisse (`SD/out/studyB.txt`)
Part des départs où le contact du binaire est non nul au premier tick raccourci :

| Pas | Air | Debout sur une caisse | Sol |
|---|---|---|---|
| 2,4375 px | 0,408 | 0,408 | 1,000 |
| 1,21875 px | 0,410 | 0,410 | 1,000 |
| 1,625 px | 0,462 | 0,462 | 1,000 |
| 3 px | 0,501 | 0,501 | 1,000 |
| 6 px | 0,340 | 0,340 | 1,000 |

- Au deuxième tick le contact est non nul dans 92 à 100 % des phases, au troisième dans 100 %.
- Les 13/32 du critique correspondent aux 0,408 du pas de marche en l'air.
- Le contact d'une entité « debout sur la caisse » (z = terrain + 16 px) est celui de l'air, parce que la fenêtre de 3 px de la branche sol est dépassée. C'est la scène R7 du critique (pas 1,625 : 0,462).
- Pour un mobile au sol, le contact du binaire est déjà identique à celui de la DLL. L'écart concerne le contact « air/appui ».

### 3.3 Les clés gelées de l'oracle sont prédites par le modèle seul
- `ArrowLifeTests.E7` / `ChargedArrowLifeTests.C7` (mur à 349, pas de 6 px, `late = {98, 101, 104, 107}`) : 12/12 (`SD/out/studyE.txt`). Le motif « en retard » est la distance restante D ∈ {2, 5} px pour un pas de 6 px (`SD/out/studyD.txt`).
- Tir à bout portant, table de t2-proj (`pb_sweep_east.txt`, naissance à 3 px puis 6 px par tick) : 10/10 pour `xS` à 0,001 près et `adj@`, soit les valeurs 2, 3, 2, 2, 3, 2, 1, 2, 1, 0 (`SD/out/studyF.txt`). Le départ x0 = 107 donne `xS = 108,99998`.
- Le gap du critique W4.5 (« 25 phases sur 32 à +1 tick, 6 à +2, 1 à 0 ») est la même mécanique : 78 % / 19 % / 3 % mesurés par lui, 82 % / 18 % pour moi.

## 4. S3 : correctif fidèle

### 4.1 Ce qu'il change
| Écart | Cause réelle | Fermé par une recherche fidèle ? |
|---|---|---|
| O-G3-3 | contact de l'avant-dernière tentative, hors branche sol | oui |
| W4.5 (`ForceAdjusted` +1/+2) | reste entier de la recherche, est/sud | oui |
| D-E20-38 / D-F4-9 / Q-K2, R-EL-5, Q-H4C1-5 | même reste entier | oui |
| O-E20-24 | `Move` non testé avant intégration | oui, à condition de tester avant intégration (lecture du champ de l'hôte, ce que fait déjà `ReadCollisionField`, `EntityScriptProxy.cs:1526`) |
| Position à l'unité brute près | vérité `float32` | non, il faut une vérité entière de XY |

### 4.2 Où : trois options
- **A. Option du contrôleur du moteur** (« recherche par moitiés »). Ne peut pas être exacte : la racine est `float32`. Ne règle pas non plus la naissance.
- **B. Résolveur horizontal tenu par le jeu** (la formule d'O-E19-42 : « un plan moteur … et une ADR »). Même limite : la racine reste `float32`. Il faudrait en plus déplacer la porte `:1056-1061` avant la ressource physique.
- **C. Déplaceur DLL `BinaryXyMover`** en entiers 16.16, qui porte toute la boucle (branche sol, glissement, retour, `ForceAdjusted`) et rend à la DLL la vérité entière de `PosX/PosY` ; la racine du moteur ne fait que suivre, arrondie. Les prédicats viennent de la DLL : `EntityCollision.FindEntityCollisionCandidate` (existant), `ComputeTerrainHeight`, `CellsCollisionField` et le test de mur porté de `0x800373E4` / `0x80037488`.

**Recommandation : C**, par étapes.
- Phase A : fonction pure et rejeu du modèle (vecteurs exportés en JSON, dans l'esprit du fuzz à 20 000 cas prévu pour e0k4, plan `:5435-5437`).
- Phase B : câblage pour les entités marquées (flèches, boules, objets lancés ou lâchés). Cela couvre O-E20-24, D-E20-38 et les rochers.
- Phase C : le héros, avec ses tests.
- Phase D (plus tard) : PNJ, monstres, avec E19.h2.

Raisons : seul C peut tenir la position exacte et le contact du binaire. Il suit les précédents DLL (`ClampStepToMapEdge`, `SlideAlongWall`, `SnapAtApex`, le plan e0k4 écrit comme tel) et l'ADR-0047 du moteur, D1 : « the rule belongs to the game ».

Points d'attention (INFÉRENCE) :
- Double ajustement du Z (`SnapAtApex` / `SnapHeroAtApex` après le pas, alors que la boucle accroche déjà `PosZ`).
- Interfaces entre `LastTickDeltaX/Y`, la vérité de la position et ce que les autres lecteurs de la racine attendent.
- Coût : une tentative en pas libre, comme aujourd'hui ; au pire une vingtaine de tentatives par tick bloqué.
- Estimation : 3 à 4 tranches (INFÉRENCE).

**Défaut moteur à consigner (règle de l'auteur).** « `CharacterControllerComponent.Move` d'une entité hors monde déplace sans test, sans signal » est un manque du moteur. Le registre `docs/plan-e20-annexe/e20-g5/g5c/engine-gaps.md` est le plus proche (cité par t2-proj). Le déplaceur DLL ne l'utilise plus pour ces entités : si l'auteur lit la règle strictement, c'est un contournement.

### 4.3 Ce qu'il ne doit pas changer (ou qu'il renverse)
- **D-E19-58** (2026-10-03, l'auteur) : « Le glissement de la DLL est gardé ; pas de résolution exacte des contacts (étape 2) pour l'instant » (`docs/plan-e19-opcodes.md:205-206`). Le correctif est précisément cette étape 2 : décision à renverser explicitement.
- **Q-G3-d** (2026-10-22, réponse A : garder l'écart de la poussée diagonale contre une caisse), **D-E20-38** (Q-K2, A), **Q-H4C1-5** (accepter), **O-E19-28**. La boucle est conjointe par nature : un port « par axe » ne serait pas fidèle. Elle renverse ces quatre réponses pour les entités concernées.
- **D-E20-35** (pas d'intégration immédiate) : inchangé.
- **O-E19-17** (le héros n'est pas rogné au bord de la carte ; `ClampStepToMapEdge` ne touche que les entités marquées) : inchangé.
- **ADR-0045 / ADR-0047** du moteur : inchangées pour les entités qui gardent le déplaceur actuel.

### 4.4 Recensement statique des tests (INFÉRENCE, aucune exécution)
- **A. Valeurs qui changent** (une dizaine de classes, environ 15 à 25 méthodes) :
  - `Physics/CrateSolidityMap392Tests.OG33_…PinnedNotFollowed` (`:182`) ;
  - `Ai/Projectiles/ArrowLifeTests.E7_TheFlagOfTheWall…` (`:221`, tableau `late`) ;
  - `Ai/Projectiles/ChargedArrowLifeTests.C7_TheFlagOfTheWall…` (`:257`) ;
  - `Hero/HeroRunTests.G5_TheEastWall_BouncesOneTickEarly…` (`:452`) et `G5_TheEastVariants…` (5 `InlineData`), plus le commentaire de `G5_APadReleasedDuringTheBounce` ;
  - `Hero/HeroSwimTests.W5_AWall_…` (`:516`) et `W5_ADiagonalIntoAWall_…` (`:526`, tolérance 7 424) ;
  - `Hero/HeroRunTests.G9_…` (tolérance 16, pourrait descendre à 0) ;
  - `Hero/HeroSlideTests.TSL3_…` (`:148`) et le reste de `HeroSlideTests` ;
  - `Physics/MovementObstacleProbeTests.TR9_…` (`:499`, « le binaire le garde immobile ») ;
  - `Entities/NpcCharacterControllerMoverTests.ForceAdjusted_ClearedEachFrameTop_…` (`:1457`) et les tests de `ForceAdjusted` de E19.a2 / a3 ;
  - le paramètre `shift` de `Support/RunMontage.cs`.
- **B. Arcs qui peuvent glisser d'au plus un tick** (environ 10 fichiers) : `Arcs/Sailor12EndOfIntroTests`, `HeroJumpArcTests` (A10J `0x24 @2462`), `HeroTraceHarnessTests` (carte 389), `CabinWalkTests` (vers le nord : déjà exact), `InoaAwakeningArcTests`, `InoaDayOneArcTests`, `Day3SceneArcTests`, `ShipBarricadeArcTests`, `EntityContactArcTests`, `LiftArcTests`, `LiftMap72ArcTests`, `Hero/Carry*`, `Ai/Objects/PillarAiGoldenTests`.
- **C. Traces d'or** `docs/hero-trace-389-*.txt` : à régénérer. Le précédent de l'ADR-0017 est que le premier drapeau de contact bouge d'un cadre.
- Environ 70 fichiers de test lisent `ForceAdjusted`, `XCollisionEntity`, `EntityBlockCount` ou `LastContact`, sans que cela dise qu'ils bougent (relevé par `rg`). Le comptage fin demande une exécution : un **tick d'ombre** (le mouvement actuel et le nouveau en parallèle, différences listées) au T0 de la tranche, comme la phase d'ombre d'e0b.

## 5. S4 : questions à l'auteur (termes concrets, défaut « comme l'original »)

**Q-STEP-1 : porter la recherche du pas entière ?**
- *Vu en jeu :* un écart de 1/50 s pour une flèche ou un rocher qui rebondit contre un mur à l'est ou au sud (1 fois sur 3 pour une flèche). Pour Alundra qui marche : appuyer sur Carré exactement au tick d'arrivée peut soulever la caisse voisine au lieu de donner un coup d'épée. Une flèche tirée à bout portant contre un mur rebondit tout de suite au lieu de rester plantée.
- *Coût :* 3 à 4 tranches, des tests d'arcs à réécrire par dérivation.
- *Défaut :* oui, comme l'original. À renverser : D-E19-58, D-E20-38, Q-H4C1-5, Q-G3-d.

**Q-STEP-2 : en deux temps ?** D'abord O-E20-24 seule (la flèche plantée dans le mur, seul écart visible à l'œil), puis la recherche entière. *Défaut :* oui, en commençant par les entités marquées et le test de naissance.

**Q-STEP-3 : division conjointe du pas.** Pousser en diagonale contre un coin ou une caisse : l'original ne fait qu'avancer d'une fraction conjointe, puis glisse seulement par la table, un ou deux ticks plus tard ; le portage glisse tout de suite. *Défaut :* comme l'original, donc retrait de Q-G3-d (A).

**Q-STEP-4 : la valeur de contact `XCollisionEntity` rendue par le binaire**, y compris 0 au premier tick raccourci, sans correction. Les lecteurs sont Carré/soulèvement (`PlayerManager.cs:1015`), les interactions (`:1942-1989`), l'opcode `0xA9`, la recherche d'entités 7 et 8, `PillarAi`. *Défaut :* oui.

**Q-STEP-5 : vérité de la position en entier dans la DLL**, la racine du moteur arrondie qui suit. Les tolérances de 16 unités des tests de héros tombent. *Défaut :* oui.

**Q-STEP-6 : le défaut moteur « `Move` avant intégration ne teste rien ».**
- La règle de l'auteur le range dans le moteur, avec son plan et son ADR.
- D-E20-35 refuse l'intégration immédiate (donc ce n'est pas le chemin).
- Le déplaceur DLL le contourne pour les entités concernées.
- *Défaut :* le consigner comme manque du moteur ; ne pas le corriger dans cette tranche, et faire lire le champ de l'hôte au déplaceur DLL avant intégration.

**Q-STEP-7 : où loger le déplaceur** (moteur A/B, ou DLL C). Ma recommandation est C pour les raisons du §4.2. *Défaut selon la règle stricte :* moteur. Mais A et B ne peuvent pas être exacts, donc la règle seule ne tranche pas : à arbitrer.

## 6. Limites et points non établis
- La DLL et le moteur n'ont pas été exécutés. Le modèle `float32` du moteur suppose un prédicat exact à la frontière ; le vrai `IsHorizontalMoveBlocked` n'a pas été remesuré.
- Mon modèle couvre `ComputeXY` et ses auxiliaires, pas l'amont (forces, `0x800366FC`) ni l'aval de `MoveEntity` en dehors du retour.
- Le recensement de tests est statique.
- Le rôle de l'arrondi du Z accroché dans la DLL n'est pas chiffré.
- Le modèle de la fonction a été validé sur la table du glissement `0x80023734` entière par le rejeu, pas par lecture ligne à ligne de chaque bloc.

## 7. Fichiers
- Index complet : `SD/INDEX.txt`.
- Modèle : `SD/work/xymodel.py` ; rejeu : `SD/work/xyfuzz.py` (`python -I xyfuzz.py N GRAINE [focus] [-v]`) ; mutants : `SD/work/xymut.py` ; rejeu des scénarios : `SD/work/spotcheck.py`.
- Études : `SD/work/studyA.py`, `studyA2.py`, `studyB.py`, `studyD.py`, `studyE.py`, `studyF.py`, `studyG.py`, `attempts.py` ; sorties dans `SD/out/` (`studyA.txt`, `studyA2.txt`, `studyB.txt`, `studyD.txt`, `studyE.txt`, `studyF.txt`, `studyG.txt`, `attempts.txt`, `xymut_out.txt`, `fuzz_seed11..14.txt`, `fuzz_focus_seed41..44.txt`).
- Listings du binaire : `SD/lst/computexy_full.txt`, `find_candidate_80036f34.txt`, `groundheight_800370c4.txt`, `wall_800373e4.txt`, `wall_80037488.txt`.
- Lu dans le dépôt, sans modification :
  - `CasaEngineMonogame/CasaEngine/Framework/Scene/Entities/Components/CharacterControllerComponent.cs` ;
  - `CasaEngineMonogame/docs/decisions/0045-…`, `0047-…` ;
  - `Alundra/Physics/ScriptedMotion.cs`, `MovementObstacleProbe.cs`, `EntityCollision.cs`, `CellsCollisionField.cs` ;
  - `Alundra/Entities/EntityScriptProxy.cs` ;
  - `docs/plan-e20-heros.md:164-172, 290-294, 5435-5437, 11150` ;
  - `docs/plan-e19-opcodes.md:205-206, 13254, 13268` ;
  - `docs/plan-e20-annexe/e20-g1b/walk-release-disc/critic-main.md` et `report-main.md` ;
  - les tests nommés au §4.4.