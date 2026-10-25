# Thème 2, passe projet : O-E20-24 (tir à bout portant) et l'Objet 008 (#8)

## 0. Cadre

- **Contrainte.** Lecture seule du dépôt. Aucun build, aucun test, aucune écriture git. Mes scripts et sorties sont sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/`. Les oracles sont rejoués depuis des copies. J'ai supprimé un `/tmp/_unused` créé par erreur.
- **État du dépôt.** HEAD `86d421b5`. L'arbre de travail porte des modifications non commitées de g1b (`TestSaves.cs`, `CarryRig.cs`, etc.). Les fichiers:lignes ci-dessous sont ceux lus le 2026-10-24. Les lignes de `PlayerManager.cs` ont bougé depuis l'inventaire initial (g1b y ajoute des lignes).
- **Étiquettes.** FAIT : lu dans le code, le plan ou une annexe, source citée. MESURÉ : rejoué par moi sur `ALUN_CD.EXE` (France) ou sur les données exportées. INFÉRENCE : non vérifié, car je ne pouvais pas exécuter la DLL. Je dis toujours quand la DLL n'est pas remesurée.
- **Réserve.** Les affirmations sur le comportement de la DLL à l'exécution viennent de la lecture du code, plus une mesure antérieure de f5 T1 (arrow3_point_blank, 111 px). Je ne les ai pas rejouées.

---

## 1. Synthèse

### O-E20-24

- **Cause (FAIT).** Ce n'est ni `ClampStepToMapEdge`, ni la logique de `ForceAdjusted`, ni E17. Le bornage du pas contre une cellule de mur ne vient pas de la DLL. Il vient de `CharacterControllerComponent.Move` du moteur, et il est absent tant que l'entité n'est pas intégrée au monde.
  - Une entité née par l'interface est mise en file par `World.AddEntity`. `Entity.World` reste nul jusqu'à `InternalAddEntities`, en tête du `World.Update` suivant.
  - La DLL la fait pourtant tourner dès son tick de naissance, puis à tous les ticks de la même image.
  - Tant que `World` est nul, `TryResolveCollisionDependencies` rend faux, et `MoveWithCollisions` ajoute le déplacement sans aucun test (`rootComponent.Position += requestedDisplacement`).
  - `ClampStepToMapEdge` fonctionne, lui, avant intégration. C'est une fonction DLL pure : le cas du bord de carte à la naissance est déjà juste.
- **Binaire (MESURÉ).** Il n'a aucune règle spéciale pour le premier pas. C'est le même contact que partout : le pas est raccourci jusqu'à la plus grande position libre. `ForceAdjusted` n'est levé que si aucun sous-pas n'est accepté. E17, E1 ou E9 tournent au pick suivant.
- **Étendue (MESURÉ pour le binaire, INFÉRENCE pour la DLL).**
  - Sont touchées : la flèche du chasseur, la flèche normale de William, la **flèche chargée** (déjà branchée en production), les boules de feu et de glace normales et chargées (f6b, pas encore branché), sur les quatre côtés.
  - Ne sont pas touchées : la vague de l'épée, qui ne se déplace pas.
  - La fenêtre est de quelques pixels devant le mur : héros collé au mur ou à moins de 2 à 5 px.
- **Correctif.** Rien ne se corrige dans le moteur sans contredire D-E20-35. Il faut, au choix, une tranche DLL étroite (contact du tick de naissance, que je propose en défaut), le port complet de la boucle de pas (e0k4, déjà décrit au plan), ou un changement moteur.

### Objet 008

- **Ce que c'est (MESURÉ).** « Objet 008 » n'est que le nom de l'export. L'objet 8 s'appelle **« Epée légendaire »** en jeu (`Etc_0520`). C'est l'épée du « Roi Neige » (Snow King), donnée dans une scène d'Overworld 1,2 (carte 7).
- **Condition d'obtention (MESURÉ).** Elle n'est donnée **que si le compteur de reprises après mort (`SaveSlotIndex`) vaut 20 ou plus**. L'opcode `0xC2 [20]` fait ce test. C'est une épée de « pitié ».
- **Ce que devient le coup (MESURÉ, binaire rejoué).**
  - Le créneau 1 donne toujours #8 dès qu'il est possédé (rang 4 > Elnard).
  - Le coup est celui de l'épée de base : animations `0x10` et `0x14`, sans charge, sans aura, sans onde.
  - Les dégâts sont de 51 contre une cible à réponse ×1,0, soit (1+50). Élnard donne 16.
  - Ce n'est donc pas un « coup dégradé » : on perd la charge et la portée, on gagne la puissance.
- **Côté DLL.** Le coup et les dégâts sont déjà portés par les données : table item vers enregistrement, enregistrement 11, sélection par rang, fiche d'équipement, `Damage`.
  - Les animations de balayage de #8 sont couvertes par l'oracle (AT9, `item08` sol et air).
  - La **chaîne d'obtention** est absente : l'opcode `0xC2` n'est pas porté, le compteur n'est jamais incrémenté (E18 non planifiée), et le héros ne peut pas mourir (c2b).
  - Aucun test ne couvre la sélection de #8 ni ses dégâts.
- **Moteur.** Rien à demander pour #8.

### Corrections de l'inventaire initial

1. « Les ramassages des cartes 6, 52, 80 » est **faux**. L'inventaire a confondu un identifiant d'enregistrement (38) avec un type de sprite (38). Ces spawns sont un « Bloc transparent » (carte 6) et des sprites locaux de carte (52, 80).
2. #8 existe aussi en pickup posé sur la carte 11 (Debug zone, enregistrement 24) et en coffre de la carte 0 (Test Map). L'inventaire ne les listait pas.
3. « Objet 008 : aucun test » est faux pour le balayage : `AT9a-item08` et `AT9g-item08` existent. Il est vrai pour la sélection et les dégâts.
4. La condition `0xC2 [20]` n'était pas vue.

---

## 2. Partie A : O-E20-24, flèche et boules à bout portant

### A.1 Le binaire (MESURÉ, oracle rejoué)

**Rejeu.** `bin/run_pointblank.py` a été rejoué depuis une copie. `arrow3_point_blank.txt` et `fire11_point_blank.txt` sont **identiques octet pour octet** aux clés gelées.

**Le héros collé au mur.** Le héros marchant vers l'est contre une colonne 5 (x ≥ 120) s'arrête à **x = 109,000** (`out/pb_hero_wall.txt`). C'est la position « collée ». Avec le mur ouest et les murs nord et sud, le schéma est le même (`out/pb_dirs.txt`, `out/pb_dirs_west.txt`).

**Étendue des projectiles du héros** (`pb_sweep.py`, `out/pb_sweep_east.txt`) :
- L'apparition se fait à x héros + pas. Les pas vers l'est sont : flèche du chasseur et de William 3 px, flèche chargée 6, boule de feu 6 (normale et chargée), boule de glace 3 (normale et chargée).
- Le mur borne la position finale à 109,0 pour tous.
- Le pas non borné sortirait de 109 pour x0 ≥ 107 (pas de 3 px) et x0 ≥ 104 (pas de 6 px).

Extraits, mur colonne 5, héros face à l'est (xS = x en fin du tick de naissance S ; « drapeau » = premier tick où `+0x13C` est levé ; « statut 3 » = tick où le projectile est désactivé et où E17, E1 ou E9 agissent) :

| Projectile | x0 héros | xS binaire | x sans test (x0+pas) | drapeau | statut 3 |
|---|---|---|---|---|---|
| Flèche du chasseur (3) | 106 | 109 | 109 | S+1 | S+2 |
| | 107 | 109 | 110 | S+2 | S+3 |
| | 108 (clé gelée) | 109 | 111 | S+1 | S+2 |
| | **109 (collé)** | 109, 0 px parcouru | 112 | **S+0** | S+1 |
| Flèche chargée (7) | 104 | 109 | 110 | S+2 | détruite |
| | 105 | 109 | 111 | S+1 | |
| | 108 | 109 | 114 | S+1 | |
| | **109** | 109 | 115 | **S+0** | détruite à S+1 |
| Boule de feu (11/12) | 104 | 109 | 110 | S+2 | S+3 |
| | **109** | 109 | 115 | **S+0** | S+1 |
| Boule de glace (14/15) | 107 à 109 | 109 | 110 à 112 | 2, 1, 0 | |

La flèche chargée n'a pas de statut 3 : elle passe directement à 4 (détruite) avec l'effet de bris `attach(id=8)`, comme dans `charged7_east_wall`.

**Mêmes lois sur les quatre côtés** (`out/pb_dirs.txt`) :
- Nord : héros collé à y = 71 (mur ligne 3).
- Sud : y = 120 (mur ligne 8).
- Ouest : x = 82 (mur colonne 2), après correction de ma première mise en place dégénérée.
- Pas en Y : 2 px pour la flèche et la glace, 4 px pour la flèche chargée et le feu.

**Règle du binaire** (FAIT : plan §1.18 « Pas horizontal » et « Rognage », critique d'e0k) :
- Le pas est affiné par moitiés jusqu'à la plus grande position libre. Une fraction de pas est acceptée.
- `ForceAdjusted` est levé seulement si **aucun** sous-pas n'est accepté et que `NoObstacleSlide` (`0x2000`, présent dans les drapeaux de tous ces projectiles) le permet.
- La fonction de blocage des non-héros est `0x800373E4` : un coin bloque si `mot & (0x40 | 0x01 si ClassB | 0x1000 si ClassA)` est non nul, ou si la hauteur du coin est ≥ `ModZ`.
- Le tick de naissance n'a **pas de règle propre** : l'entité née passe par la même passe physique.

**Rebond de la flèche collée** (clé `arrow3_flush`, générée par moi) :
- Au tick S, x = 109, drapeau levé.
- Au tick S+1, statut 3. Le rebond a lieu au même tick : x = 107,125.
- La flèche recule de 1,875 px par tick pendant 16 ticks, avec un saut vertical.

### A.2 La chaîne DLL et la cause (FAIT, fichier:ligne à la date de lecture)

1. **Naissance.** `Maps/WorldProxy.cs:3544` `SpawnEntityBySpriteIndex` appelle `_world.AddEntity` (`:3613`), puis pose les marqueurs `JoinsBirthTick` et `HasLogicalZ` (`:3628-3629`).
2. **Moteur.** `CasaEngine/Framework/Scene/World/World.cs:168-175` : `AddEntity` met en file (`_baseObjectsToAdd`). `InternalAddEntities` (`:822`) tourne en tête de `Update` (`:563`) et pose `Entity.World` via `InitializeWithWorld`.
3. **Participation au tick.** `WorldProxy.cs:2874-2893` `CollectTickEntities` retient `proxy.JoinsBirthTick && entity.World == null`. Le nouveau-né tourne donc à son tick de naissance et à **tous les ticks suivants de la même image**.
4. **Pas.** `Physics/ScriptedMotion.cs:192` `RunOneMotionTick`, puis `:635` `RunOneKinematicTick`.
   - Les forces sont planifiées par `PlanForce`.
   - Le rognage de bord `ClampStepToMapEdge` (`:662-670`, `:813`) s'applique, sans moteur.
   - Puis `entity.MoveControllerAndPullPosition` (`:681`) appelle `Controller.Move`.
5. **Passage dans le moteur.**
   - `EntityScriptProxy.cs:2642` appelle `Controller.Move` à `:2655`.
   - `CharacterControllerComponent.cs:438` `Move` appelle `MoveWithCollisions` (`:1045`).
   - `:1056-1061` : si `!TryResolveCollisionDependencies(...)`, alors `rootComponent.Position += requestedDisplacement; return requestedDisplacement`.
   - `:1601-1627` : `TryResolveCollisionDependencies` rend faux dès que `owner?.World?.PhysicsWorld == null`.
   - Le test de cellules (`ResolveHorizontalDisplacementAgainstField`, `:1134`) lit `Owner?.World?.CollisionField` (`:1153`) et n'est donc **jamais atteint** avant intégration.
6. **Suite du tick.** `SlideAlongWall` (`ScriptedMotion.cs:853`) voit un pas complet (`:917-921`). `ForceAdjusted` reste à 0.
7. **Épinglé par un test existant.** `Alundra.Tests/Entities/BirthTickProbePaTests.cs:143-179` (`PA2_ControllerMoveBeforeTheIntegration_AddsTheDisplacementWithoutATest_AndAfterIt`). Il n'a **pas de mur** sur le trajet, donc il ne montre pas le cas.
8. **Écrit dans le plan** (e0a3b, §1.16.3 « Faits propres », ligne ~3371 : « Avant l'intégration `Entity.World` est nul : `CharacterControllerComponent.Move` ajoute alors le déplacement sans test »).
9. **Écrit aussi dans les tests.** Le commentaire de `ArrowLifeTests.cs:162-163` documente l'écart.
10. **Lecture du champ par la DLL avant intégration.** `EntityScriptProxy.cs:1526` `ReadCollisionField` rend le champ de l'hôte pour une entité qui `JoinsBirthTick`. L'infrastructure d'un bornage côté DLL existe donc (`BlockedCorners`, `ScriptedMotion.cs:972`, l'utilise déjà).

### A.3 Ce que fait la DLL (INFÉRENCE, cohérente avec la mesure f5 T1)

Cas hero 108, mur colonne 5, flèche du chasseur : la mesure de f5 T1 (`t1-results.md` §5 point 6) donne x = 111 et « le drapeau tenu ». Mon modèle la reproduit.

- **Tick S.** x = 111, pas borné, `ForceAdjusted` = 0 (le pas a progressé).
- **Tick S+1.** La flèche est déjà dans la cellule du mur. `IsCandidateBlocked` est vrai, la sonde d'un ULP est bloquée, `AdvanceBlockedAxisToContact` rend 0. `Move` ne progresse pas, d'où `ForceAdjusted` = 1.
- **Tick S+2.** Le pick passe en Désactivée, E17 fait le rebond. Le pas de rebond vers l'ouest de 1,875 px laisse le bord droit dans la cellule du mur (x − 1,875 + 11 > 120 pour x > 110,875). Il est bloqué, donc **la flèche reste à 111** avec le drapeau tenu.
- **Fin.** Le saut vertical se joue sur place. La flèche est détruite à la fin des 16 ticks de l'animation, à peu près au tick de l'oracle (S+19).

Cas collé (hero 109) : x = 112 au tick S, drapeau à S+1, statut 3 à S+2 (au lieu de S+0 et S+1 dans l'original).

Pour la **flèche chargée**, la destruction par E1 arrive un tick plus tard (S+2 au lieu de S+1), avec l'effet de bris à x0+6 dans le mur.

Pour les **boules** (une fois f6b branchée) : explosion un tick plus tard et 6 px dans le mur (x = 115 contre 109).

### A.4 Étendue du défaut

- **Flèche chargée.** Déjà branchée en production (`PlayerManager.cs:1319` `ProductionWiredWeaponTypes = {3, 4, 7, 27, 28, 29}`). Elle est donc touchée **aujourd'hui**. Fenêtre en X : x0 ∈ {104..109}.
- **Flèches normales (types 3 et 4).** Touchées aujourd'hui aussi : fenêtre x0 ∈ {107, 108, 109} (3 px).
- **Boules de feu et de glace (11, 12, 14, 15).** Non branchées en production, donc **latent**. Elles seront touchées dès le branchement de f6b : fenêtre x0 ∈ {104..109} pour le feu (6 px), {107..109} pour la glace (3 px). Le texte de f6b (§1.22.2) ne cite pas cette clé et ses clés de mur (`*_wall_col6`) sont loin du lanceur.
- **Vague de l'épée (types 27 à 29).** Non touchée : sa position reste (100,100) pendant toute sa vie, seule l'animation balaie la boîte (trace `trace_wave_steel_east.txt`).
- **Ticks par image (INFÉRENCE).**
  - Avec N ticks par image, un projectile né au tick j de l'image reste non intégré jusqu'à la fin de cette image, donc N − j + 1 ticks sans test de mur.
  - Un seul tick à 60 Hz d'affichage (la logique tourne à 50 Hz). Deux ticks ou plus sur les images lentes (30 fps, ou rattrapage jusqu'à 4).
  - Non testé : les tests E7 à 1, 2 et 3 ticks par image ont leur mur à la colonne 15, loin du tir.
- **Autres entités nées par l'interface (INFÉRENCE, non examinées).** La bombe lancée (h3) et les sorts (h4) naissent par la même voie. Les entités nées par script (0x2D, 0x8A, 0x8B) passent aussi par `JoinsBirthTick` depuis e0a4, mais démarrent à force nulle.

### A.5 Ce qu'il faut ajouter et ce que demande le moteur

- **Gap moteur à consigner.** `CharacterControllerComponent.Move` d'une entité hors monde déplace sans test, sans signal. À inscrire avec les gaps connus (`docs/plan-e20-annexe/e20-g5/g5c/engine-gaps.md` est le registre le plus proche : G1 clés de relâchement, G2 boucle audio). Le contenu à y mettre : cette ligne, son effet, le contournement DLL choisi.
- **Option 1 (ma recommandation, défaut) : tranche DLL étroite « e0k5 », contact du tick de naissance.**
  - Quand `Controller != null && Owner?.World == null && HasLogicalZ`, borner le pas à la plus grande position libre sur le champ de l'hôte (`ReadCollisionField()`) avant `MoveControllerAndPullPosition`.
  - Passer le déplacement borné à `Move` et à `SlideAlongWall`. Pour des projectiles `NoObstacleSlide`, un pas nul lève alors `ForceAdjusted` au tick S (`:923-926`).
  - Il faut un contact à la précision du binaire (plus grande position libre en unités brutes), des coins comme `BlockedCorners` et la fonction `0x800373E4` (mot `& (0x40 | …)`, hauteur du coin ≥ `ModZ`).
  - Le DLL-only reste cohérent avec D-E20-35, qui refuse l'intégration immédiate et le changement du moteur.
  - Il laisse l'écart d'un tick du drapeau de mur (D-E20-38) tel quel.
- **Option 2 : e0k4 complet (§1.18.4 du plan).** Port entier de la boucle d'affinage de `0x80037730` pour les entités marquées, en remplacement du mover du moteur. Plus grosse, ferme aussi l'écart D-E20-38. Elle est conditionnelle à Q-K2 option B, que l'auteur n'a pas choisie.
- **Option 3 : moteur.** Rendre le champ lisible avant intégration, ou intégrer immédiatement. Contredit D-E20-35 : il faudrait un ADR du moteur.
- **Option 4 : accepter.** Le plan classe O-E20-24 en P2 « visible en jeu ».

### A.6 Tests et recettes possibles

**Clés d'oracle.**
- Déjà gelées : `arrow3_point_blank` (et `_pf` dans `f5/oracle/out/json/`), `fire11_point_blank`.
- Générées par moi pour la tranche (`out/keys/`, scénarios `pb_keys.py`) :
  - `arrow3_flush`, `arrow4_flush`, `arrow3_near105`
  - `charged7_flush`, `charged7_near105`
  - `fire11_flush`, `fire12_flush`, `ice14_flush`, `ice15_flush`
- Les variantes `_pf` (pas du générateur par image) manquent : il faut les produire avec `f5/oracle/run_f5.py`, que lit `ArrowOracle.Load(..., stepByImage: true)`.

**Tests DLL à écrire (T0 d'abord, rouges sur le code actuel) :**
- **Sonde de cause.** Copie de PA2 avec une cellule de mur à 3 px : `Move((5,0,0))` avant intégration doit rendre `(5,0,0)`. Elle prouve le trou.
- **Test du banc `ArrowRun`.** `heroX` 108 et 109, `Column(5)`, clé `arrow3_point_blank_pf` (ou `arrow3_flush_pf`), de S à S+19, à 1, 2 et 3 ticks par image.
- **Idem pour la flèche chargée.** `ChargedArrowRun` avec `charged7_flush` : la destruction à S+1 doit égaler la clé.
- **Banc de boules.** Pour f6b : `fire11_flush`, `ice14_flush`, à utiliser une fois les types 11 à 15 câblés.
- **Quatre côtés.** Nord, sud, ouest à partir de `pb_dirs.txt`.
- **Mutants.** Le contact d'un pixel de trop ; le pas de la naissance laissé libre ; le drapeau levé à S+1 au lieu de S.
- **Tests existants à surveiller.** `ArrowLifeTests` E7, `ChargedArrowLifeTests` c7 (murs loin, ne devraient pas bouger). PA2 reste vrai (comportement du moteur).

**Recette en jeu.** Héros collé à un mur (tout mur plein), tir de l'arc du chasseur, de l'arc de William chargé, du bâton de feu une fois f6b faite. Attendu : la flèche rebondit tout de suite de 1,875 px par tick pendant 16 ticks, la flèche chargée éclate au mur, la boule explose au contact. À observer : la DLL actuelle montre la flèche à l'intérieur du mur, immobile.

**Position collée dans la DLL.** Non vérifiée : 109 est celle du binaire. À mesurer en T0.

### A.7 Questions à l'auteur (en termes concrets)

- **Q-O24-1.** Quand on tire à bout portant contre un mur :
  - Dans l'original, la flèche rebondit aussitôt et saute en arrière sur 16 ticks (0,32 s) puis disparaît. La flèche chargée éclate au contact. La boule explose au contact, sur la face du mur.
  - Dans la DLL actuelle, la flèche apparaît 3 px dans le mur, ne bouge pas pendant environ 17 ticks (0,35 s), puis disparaît sans rebond. La flèche chargée éclate un tick plus tard, dans le mur. La boule exploserait un tick plus tard, 6 px dans le mur.
  - Options : **A** laisser tel quel (écart connu) ; **B** corriger seulement le tick de naissance, petite tranche ; **C** porter toute la boucle de pas de l'original (grosse tranche, supprime aussi l'écart d'un tick de drapeau de mur) ; **D** demander au moteur de tester les murs avant intégration.
  - **Défaut proposé : B**, qui retrouve l'original sur ce cas précis.
- **Q-O24-2.** La faire avant le branchement de production de f6b (balles de feu et de glace) ? Défaut : oui, pour ne pas livrer f6b avec cet écart.
- **Q-O24-3.** Le correctif s'applique à tous les ticks non intégrés d'une image, pas seulement au premier ? Défaut : oui.

### A.8 Squelette de tranche : E20.e0k5

- **But.** Le tick de naissance (et les ticks non intégrés de la même image) borne le pas aux cellules comme le binaire.
- **Prérequis.** e0k3 ✅, f5 ✅. Dépendants : f6b (phase de branchement), vérification de h3 et h4.
- **T0.** Sonde rouge (PA2 + mur), clés d'oracle gelées, liste prédite des tests qui bougent.
- **T1 (phase A).** Fonction pure : plus grande position libre sur un pas cardinal, sur le champ de l'hôte, coins de `BlockedCorners`, test du mot de cellule et de la hauteur du coin. Tests rouges d'abord.
- **T2 (phase B).** Branchement dans `RunOneKinematicTick`, avant le `Move` pré-intégration. Pas nul : `ForceAdjusted` au tick S.
- **T3.** Mutants, ADR (gap moteur consigné), mise à jour du plan et de `engine-gaps.md`.
- **Arrêt.** Un besoin de toucher au moteur.

---

## 3. Partie B : l'Objet 008 (« Epée légendaire »)

### B.1 Le binaire : sélection de l'arme par créneau (MESURÉ, désassemblé)

**`0x8004E18C` (`GetItemIdFromSlotId`).** Pseudocode :
- `slot ≥ 0x20` : erreur journalisée, rend -1.
- Sinon, parcours `i = 0..0x7F` de la table de propriétés à `0x800B9FE8` (entrées de 10 octets : créneau s16, drapeau u16, rang s16, max s16, icône s16) et du tableau des comptes `*(0x8017631C)` (4 octets par objet, compte à +2).
- Pour chaque objet possédé du créneau demandé, le premier trouvé devient le candidat.
- **Détail du delay slot (`0x8004E20C`).** Si le drapeau de remplacement (bit 0) est nul, retour **immédiat**. S'il est 1, on continue.
- Pour les suivants, le candidat est remplacé **seulement si son rang est strictement supérieur** (`slt` en `0x8004E230`).

**Créneau 1 (table du binaire, identique à `items-properties.json` à part l'icône -1 / 65535) :**

| Objet | Nom exporté | Rang | Drapeau |
|---|---|---|---|
| 1 | Épée de base | 0 | 1 |
| 2 | Lame d'acier | 1 | 1 |
| 3 | Destructeur de démons | 2 | 1 |
| 4 | Épée sacrée Elnard | 3 | 1 |
| 8 | Objet 008 | **4** | 1 |

**Chaîne complète :**
- `0x8004E030` (`GetItemIdFromCurrentWeapon`) lit `[*(0x80176318)+0xA]` : le créneau (1 à 6, -1 aucun). Il l'envoie par la table `0x800265D4` vers l'une des enveloppes `0x8004E268…0x8004E308`, qui appellent `0x8004E18C` avec 1 à 6.
- `MovePlayer` en tête (`0x80031B70-0x80031B8C`) range `[0x8012710C] = octet[0x80028C14 + objet]`.
- `PlayerTryAttack` au pressé (`0x8002EC8C-0x8002ED2C`) utilise `[0x8012710C] × 13 + 0x80028C79 (+4 en l'air)`.
- Le menu d'équipement choisit un **créneau**, jamais un objet : le joueur ne peut pas revenir à Elnard quand il possède #8.

**Enregistrement 11 (MESURÉ, `t4_records.py`).** Octets : `00 | 10 00 00 00 | 14 00 00 00 | 00 00 00 00`. Il est **identique octet pour octet à l'enregistrement 0** (épée de base) : non chargeable (octet 0 = 0), sol `0x10`, air `0x14`, aucune charge. Les objets 11, 12, 13, 15 pointent aussi vers l'enregistrement 0.

**Fiche d'équipement entrée 38 (`balance.json`, MESURÉ).**
- 1 attaque : type 6, attribut 6, **puissance 50**, sans bit d'équipement ; catégorie 1 (arme) ; réponses `0x40…`.
- Les fiches des épées 1 à 4 ont une attaque chacune, de puissance 1, 2, 4, 8.

**Calcul des dégâts du héros** (`ResolveBalanceTarget`, `0x8004464C`, lu aussi dans `Damage.cs:24`) : pour une attaque avec le bit `0x80`, `puissance += Attacks[g+1 < count ? g+1 : 0].Power` pour chacun des trois enregistrements d'équipement (armure, arme, accessoire), `g` = le verrou d'animation du héros. Avec une fiche à une seule ligne, c'est toujours `Attacks[0].Power`.

**Rejeu du binaire avec #8** (c1b/K1/K2 copiés, inventaires ajoutés par `c1b_legend.py`, sortie `out/c1b_legend.txt`) :
- `GetItemIdFromCurrentWeapon` = **8** pour `legend` (kit + #8) et pour `legend_holy` (kit + Elnard + #8).
- Animation du coup `0x10`, ligne héros (attribut `0x81`, puissance 1).
- Équipement : enregistrement [1] = objet 8, `[(6, 50)]`.
- Dégâts contre un Beannoïde à réponse `0x10` (×1,0) :

| Inventaire | Coup | Puissance héros + fiche | Dégâts | PV 100 → | PV 9 → |
|---|---|---|---|---|---|
| kit (épée de base) | `0x10` | 1 + 1 | 2 | 98 | 7 |
| acier | `0x3F` | 2 + 2 | 4 | 96 | 5 |
| Elnard | `0x49` | 8 + 8 | 16 | 84 | mort |
| **#8** | `0x10` | **1 + 50** | **51** | **49** | **mort** |
| #8 + Elnard | `0x10` | 1 + 50 | 51 | 49 | mort |

- Les 7 autres inventaires de la clé gelée (K2) sont retrouvés par le même code. Tous les calculs de #8 sont EQUAL au modèle indépendant de l'oracle. Les 11 lignes de contrôle (réponses 0x00, 0x40, 0x80, 0xC0, sans bit d'équipement, attaque synthétique) sont toutes EQUAL.
- **La feuille de défense du héros est inchangée** : elle est identique pour kit, Elnard et #8 (`c1b_cmp.py`). #8 n'a d'effet que côté attaque.
- Pour mémoire (INFÉRENCE par la formule, non rejoué) : la vague d'Elnard niveau 3 fait (38+8)×16>>4 = 46, donc moins qu'un coup ordinaire de #8.

### B.2 Ce que devient le coup quand on possède #8

- Le créneau 1 donne #8 tout le temps : les épées 1 à 4 sont masquées.
- Animation de sol `0x10` (21 ticks, son 29), animation d'air `0x14` (10 ticks) : celles de l'épée de base. Trace d'oracle : `e20-b/oracle/out/at9/AT9g-item08.trace.txt` (sol) et `AT9a-item08` (air), `weapon_rec 11` dès le tick du pressé.
- Pas de charge : `0x10` n'est pas dans la liste des animations qui comptent la charge (`b0`) ; l'octet « chargeable » vaut 0. Pas d'aura, pas d'étincelle, pas d'onde (la vague naît de la charge relâchée de `0x18`).
- Dégâts de 51 par coup normal à réponse ×1,0 (voir B.1).
- Aucun changement pour la coupe d'herbe et les leviers (même attribut `0x81`).
- Perte réelle par rapport à Elnard : la charge et la vague à distance. Gain : puissance de mêlée.

### B.3 Où #8 s'obtient (MESURÉ : `t6_sources.py`, `t7_maplocal.py`, `t8_rid38.py`, `t10_flag.py`, `t11_prog.py`)

**Source d'histoire : un seul point.** Carte 7 « Overworld 1,2 », programme `B[10]`, `0x82 [8]` à pc **1623**, branche de la phrase 130 (texte `M7_S002`).
- Les cinq opérations clés de `B[10]` :
  - pc 1411 et pc 1483 : `0x8A [31, …]` fait naître l'enregistrement 31 (pickup de l'objet 8, sprite global 38) comme accessoire visuel.
  - pc 1508 : `0xC2 [20]`.
  - pc 1510 : `0x03` (« If true goto » vers 1510+61 = 1571).
  - pc 1571 : `Dialog [130]` puis le don à 1623, puis `Dialog [131]` « Epée légendaire reçue ! ! ! », puis `Flag on [251, 6]` (drapeau 1787).
  - La branche refus passe par `Dialog [129]` (« Personne n'est digne de ma lame ») puis saute à la fin sans poser le drapeau : **elle se rejoue**.
- **La condition.** L'opcode `0xC2` (`0x80041D34` désassemblé) lit un octet de paramètre (ici 20) et pose `Result = 1` si `byte[0x801EBA3E] >= paramètre`, sinon 0. Cet octet est la copie de `SaveSlotIndex`, le **compteur de reprises après la mort**. Le plan E16 le confirme : « un seul `0xC2`, Overworld 1,2-7, décalage 1508, seuil 20 ».
- **Comment la scène se déclenche.** `F[7]` (interaction) de la carte 7 est portée par deux « Bloc transparent » (sprite global 25) aux tuiles (50,32) et (52,32). Son code : `If flag on [251,6]` (déjà reçu ?), puis `Goto by hero animation direction` qui ne saute vers `Flag on [0,128]` que pour la direction 1, **le héros faisant face au nord**. Ce drapeau `[0,128]` déclenche `B[10]` (événement de carte 9, toute la carte).
- Les textes viennent de `alundra-project/Maps/Overworld/Overworld 1,2-7/dialogues/Overworld 1,2-7.yarn` : le Roi Neige, un fabricant d'épées légendaire, qui refuse puis accorde l'épée « à qui a tant enduré pour le retrouver ».

**Sources de développement :**
- Carte 11 « Debug zone », enregistrement 24 : pickup d'objet 8 posé, activé, tuile (54, 54), hauteur 20.
- Carte 0 « Test Map » : coffre enregistrement 8, contenu 8.
- Aucun autre cas (pas de loot, pas d'autre `0x82 [8]`, pas de `0x83` ni `0x84` sur #8).

**Réfutation précise de l'inventaire** (`t8_rid38.py`) : les `0x8A [38]` des cartes 6, 52 et 80 font naître, respectivement, un « Bloc transparent » (sprite global 25), le sprite local 37 de la carte 52 et le sprite local 126 de la carte 80. Aucun pickup de l'objet 8.

**Aucun appel de fonction objet avec la constante 8** dans le binaire (`t12_const8.py`) : #8 est purement piloté par les données.

**Pickup (si on le ramasse).** `ItemEffects.cs:39` `GiveItem` : cas par défaut `AddOneItemIfUnlocked`. Message : `Etc_0520` « Epée légendaire » + « gagné! ».

### B.4 Ce que fait la DLL aujourd'hui (FAIT, fichier:ligne)

**Porté par les données :**
- `Items/WeaponTables.cs:86` `table[8] = 11`. Les rangées 0 et 11 (`:57`, `:68`) sont identiques. Commentaire `:24-27`.
- Sélection : `Hero/PlayerManager.cs:2298` `GetItemIdFromSlotId` (port exact de `0x8004E18C`, boucle jusqu'à 100 au lieu de 0x80), `:2257` `GetItemIdFromCurrentWeapon`.
- Équipement : `PlayerManager.cs:1649` `FillEquipmentRecords` (arme = `GetItemIdFromCurrentWeapon` si catégorie 1, fiche `SelectEquipment(item)`).
- Dégâts : `Combat/Damage.cs:24` ; en production, `Ai/EnemyHelpers.cs:232` `HitReaction` (Beannoïde D1, Muruta D9) l'appelle avec `context.EquipmentRecords` : **le +50 est donc effectif aujourd'hui sur ces deux ennemis** dès que #8 est possédé. Les autres ennemis ne sont pas portés.

**Tests existants :**
- `Alundra.Tests/Hero/HeroAttackTests.cs:373-386` : AT9 compte **22 traces** (11 objets, sol et air) dont `item08`. Le balayage de #8 est donc déjà confronté au binaire.
- `Alundra.Tests/Combat/DamageTests.cs:299-323` : K2 pour kit, steel, holy, ring, leather, noweapon, all. **Pas de « legend »**.
- `Alundra.Tests/Items/ItemInventoryTests.cs:188-201` : la sélection par rang est testée avec les objets 1, 3, 4. **Pas #8.**
- `Alundra/SaveGames/TestSaves.cs:124-129` : aucune sauvegarde de test ne contient #8.

**Absent (chaîne d'obtention) :**
- Opcode `0xC2` : présent dans `Events/EventOpcodeSizeTable.cs:242` sous « Check something save », **aucun `case` dans `EventProgramRunner.cs`**. Il tombe dans le repli « not implemented; skipping by its known size » (`Result` inchangé). Le `If true goto` suivant (pc 1510) lit donc un `Result` périmé, vraisemblablement 0, donc la branche du refus (INFÉRENCE, à mesurer).
- `GameState.cs:281-286` : `DeathRetryCount` existe, mais « Nothing increments it before E18 ». E18 est « non planifiée » (`plan-conversion-totale.md:908`).
- Le héros ne peut pas mourir (c2b absente, plan §2.1 du rapport initial).
- `0xB9` (Start CD streaming, dans `B[10]`) : pas de `case`, c'est du son (E19.sm7).
- Coffre `F255` : absent (la carte 0 n'est pas atteignable sans lui).

**Le plan.** D-E20-45 écrit « est porté ; sa tranche reste à placer ». C'est vrai pour le coup et les dégâts (par les données). C'est faux pour l'obtention.

### B.5 Ce qu'il faut ajouter (moteur : rien)

1. **`0xC2` dans `EventProgramRunner`** : `Result = DeathRetryCount >= param ? 1 : 0`, un octet de paramètre (pas deux comme la décompilation C#, qui n'a pas la garde). Quelques lignes, tests à 19, 20, 21.
2. **Un champ `DeathRetryCount` dans `TestSaves.Preset`** pour pouvoir fabriquer une sauvegarde de test (`SaveGame.cs:143`/`:192` le sérialise déjà).
3. **Préréglages de test.**
   - `legend-m14` = `WeaponPreset("legend-m14", 14, 8, 6, 0, 1, 8)` (créneau 1 équipé, kit + #8) pour essayer le coup dans l'arène des Beannoïdes.
   - `legend-m7` : carte 7, tuile (50, 33) ou (52, 33) sous un des blocs, compteur de reprises ≥ 20, drapeau 1787 éteint ; le héros marche vers le nord pour faire face au bloc (une sauvegarde ne porte pas la direction, toujours le sud).
4. **Tests de sélection et de dégâts :** voir B.6.
5. **E18** (reprise après mort, incrémentation du compteur) reste un prérequis pour l'obtention en partie normale ; ce n'est pas du ressort de la tranche.
6. **Rien à demander au moteur.**

### B.6 Tests et recettes possibles

**Oracle.** Ajouter à la clé gelée du c1b (`K1.json`, `K2.json`) un inventaire « legend » et « legend_holy » (j'ai montré par `c1b_legend.py` que l'oracle les accepte). Valeurs attendues : 51, PV 100 → 49, PV 9 → mort, enregistrement d'équipement [1] = objet 8, `[(6, 50)]`.

**Tests DLL :**
- Sélection : possédant {1, 4, 8}, le créneau 1 rend 8 ; avec {1, 8}, 8 (dans `ItemInventoryTests`, tables réelles).
- Table : `WeaponTables.RecordIndexOfItem(8) == 11` et les octets de l'enregistrement 11 (déjà dans `B1_TheTablesAreTheBinarys`).
- Balayage : AT9 `item08` déjà couvert.
- Équipement : `FillEquipmentRecords` avec {1, 17, 25, 8}, créneau 1 : arme = 8, `[(6, 50)]`.
- Dégâts : K2 `legend` (49, 0) ; un test de production sur un Beannoïde réel (D1) : 51 de dégâts avec #8, 2 avec l'épée de base.
- Pas de charge ni de vague : avec #8, un appui maintenu ne compte pas de charge et ne produit ni aura ni projectile.
- `0xC2` : retours à 19, 20, 21.
- Scène de la carte 7 (arc de bout en bout) : avec compteur 20, la scène donne 1 exemplaire, pose le drapeau 1787 et ne se rejoue pas ; avec 19, rien n'est donné et la scène se rejoue.

**Recettes en jeu :**
- Sans E18 : préréglage `legend-m14`, frapper un Beannoïde ; ou carte 11 (`pickup-m11`, tuile de départ (27,18,10)), marcher jusqu'à (54,54) hauteur 20 pour ramasser l'épée (« Epée légendaire gagné! »).
- Avec le préréglage `legend-m7` : réussir l'interaction face au nord et lire la scène.
- Attendus : coup de base avec les animations de l'épée de base, pas de charge, 51 de dégâts.

**Sauvegardes de `tools/Saves/` (noms seulement).** Je n'y ai rien trouvé qui nomme la carte 7 ou le Roi Neige. Aucun nom ne laisse penser qu'un compteur de reprises ≥ 20 y est stocké : il faudra un préréglage.

### B.7 Questions à l'auteur (en termes concrets)

- **Q-O8-1.** Dans l'original, le Roi Neige ne donne l'épée qu'à un joueur qui a repris la partie après la mort 20 fois ou plus, et l'épée devient le coup par défaut, sans charge ni vague, avec 51 de dégâts au lieu de 2 à 16. Porter cette condition telle quelle ? **Défaut : oui, comme l'original.**
- **Q-O8-2.** Porter dès maintenant l'opcode `0xC2` (quelques lignes) et le champ de préréglage `DeathRetryCount`, même si E18 n'est pas faite ? Aujourd'hui l'épée n'est donc obtenable qu'avec un préréglage ou en carte 11. **Défaut : oui.**
- **Q-O8-3.** Ajouter les préréglages `legend-m14` et `legend-m7` ? **Défaut : oui.**
- **Q-O8-4.** Garder la perte de la charge et de la vague pour un joueur qui possède #8, même s'il avait déjà Elnard (comportement de l'original) ? **Défaut : oui.**
- **Q-O8-5.** Cartes de développement (11 Debug zone, 0 Test Map) : garder les objets posés tels quels ? **Défaut : oui, ne rien changer.**

### B.8 Squelette de tranche : « Epée légendaire (objet 8) » avec les armes (D-E20-45)

- **But.** Rendre #8 vérifiable de bout en bout : sélection, coup, dégâts, obtention par la scène de la carte 7.
- **Prérequis.** Aucun côté E20 ; `0xC2` relève du domaine opcodes (E19). L'obtention naturelle exige E18, hors tranche.
- **T0.** Clés d'oracle « legend » (K1/K2), liste prédite des tests qui bougent (aucun attendu), sonde de `0xC2` sur le binaire.
- **T1.** Tests de sélection, d'équipement, de dégâts (rouges d'abord s'il y a un écart ; sinon verts et nommés).
- **T2.** `0xC2` + tests 19/20/21 ; champ `DeathRetryCount` du préréglage ; préréglages `legend-m14` et `legend-m7`.
- **T3.** Arc de la carte 7 de bout en bout, recette de l'auteur.
- **T4.** Plan, ADR si le choix de l'auteur le justifie, rapport.
- **Arrêt.** Un besoin de toucher au moteur ; un doute sur la lecture de `SaveSlotIndex` (copie `g_saveData` ou `g_saveDataInRam`) au moment du test.

---

## 4. Points non établis

- Le comportement exact de la DLL (x des projectiles, tick du drapeau, fin de vie) n'a pas été rejoué : tout vient de la lecture du code et de la mesure f5 T1 (arrow3_point_blank). Les tests T0 de la tranche e0k5 doivent le fixer.
- La position « collée » du héros dans la DLL (109 dans le binaire) : à mesurer.
- Le nombre réel de ticks non intégrés par image en production (N ≥ 2) : à mesurer.
- La valeur de `Result` au pc 1508 de la carte 7 dans la DLL (opcode `0xC2` sauté) : déduction (0).
- Que la direction 1 de `Goto by hero animation direction` soit bien « nord » (table `0x80023644` = 0, 0x10, 8, 0x18 lue dans le binaire : 0x10 = nord). À confirmer par le test d'arc.
- Si d'autres entités nées par l'interface (bombe lancée, sorts) naissent avec une force dans leur tick de naissance : non examiné.

---

## 5. Fichiers et scripts produits

Dossier de base : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/`

- `runpy_cap.py` : enveloppe `python -I` qui ajoute capstone.
- `f4f6bin/` : copie de l'oracle du dépôt (rejoué, jamais en place). `f4f6bin/out/traces_orig/` : les clés gelées d'origine, comparées octet pour octet aux rejeux.
- O-E20-24 :
  - `pb_probe.py`, `pb_sweep.py` → `out/pb_sweep_east.txt`
  - `pb_hero_wall.py` → `out/pb_hero_wall.txt`
  - `pb_dirs.py` → `out/pb_dirs.txt` et `out/pb_dirs_west.txt`
  - `pb_keys.py` → `out/keys/` (traces et JSON `arrow3_flush`, `arrow4_flush`, `arrow3_near105`, `charged7_flush`, `charged7_near105`, `fire11_flush`, `fire12_flush`, `ice14_flush`, `ice15_flush`, plus copies de `arrow3_point_blank` et `fire11_point_blank`)
- Objet 8 :
  - `c1b/` (copie de l'oracle de dégâts), `c1b_legend.py` → `out/c1b_legend.txt`, `c1b_cmp.py`
  - `o8/bx.py`, `o8/scan_local.py`
  - `o8/t1_slots.py` : table de propriétés `0x800B9FE8` (binaire contre export)
  - `o8/t2_callers.py`, `o8/t3_scanimm.py` : appelants et accès aux variables d'arme
  - `o8/t4_records.py` : table item → enregistrement et enregistrements de 13 octets
  - `o8/t5_fiche.py` : fiches d'équipement
  - `o8/t6_sources.py` → `o8/out_sources_item8.txt` et `out/out_sources_item8.txt`
  - `o8/t7_maplocal.py`, `o8/t8_rid38.py` : réfutation des cartes 6, 52, 80
  - `o8/t9_map11.py` : pickup de la carte 11
  - `o8/t10_flag.py` : usages des drapeaux `[0,128]` et `[132,131]`
  - `o8/t11_prog.py` → `out/out_map7_B10.txt` (programme `B[10]` de la carte 7)
  - `o8/t12_const8.py` : appels du binaire avec la constante 8

Aucun fichier du dépôt n'a été modifié par moi. Les modifications visibles dans `git status` (`Alundra.Tests/Hero/CarryRig.cs`, `Alundra/SaveGames/TestSaves.cs`, `Alundra.Tests/Arcs/LiftArcTests.cs`, etc.) sont celles de la tranche g1b.