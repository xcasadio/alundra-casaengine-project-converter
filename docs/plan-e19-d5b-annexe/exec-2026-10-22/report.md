# Découverte D5b d'E19.d2c2, en lecture seule : plateformes qui bougent en Z, tous les passagers

Dépôt : HEAD `1d0b6953`, branche `chantier/theme1-porter-plateforme`. Mon travail n'a rien modifié dans le dépôt. Les modifications visibles dans `git status` (`BalanceTables`, `CellMetadataWriter`, traces du héros) viennent de l'executor.

Tout a été fait hors dépôt, dans `R` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d5b-disc`. Les chemins complets des fichiers cités sont en §9.

Étiquettes : **FAIT** (relu dans le code ou le binaire), **MESURÉ** (exécuté sur la vraie DLL ou la machine MIPS), **PROTOTYPE** (patch jetable dans une copie hors dépôt), **INFÉRENCE**.

## 0. Résumé

1. **Le code n'a pas bougé fonctionnellement depuis l'étude du 17/10.** FAIT : entre `2010ec3` et HEAD, seuls des renommages (R2–R8) ont touché ces fichiers, y compris sous leurs anciens noms. Les quatre écarts M1–M4 et le plafond sont toujours là. Les lignes d'aujourd'hui sont au §1.

2. **État mesuré sur la vraie DLL (copie du HEAD, ordre de production).** MESURÉ, arc réel sur la carte 22, levier 2 « frappé » :
   - la plateforme monte de 24 à 89 px ;
   - le héros reste à 32 px à la première image, perd son porté à la deuxième et finit à 16 px, le fond du creux ;
   - un tonneau posé sur la plateforme tombe de la même façon, que l'objet suive le chemin des entités placées par la carte ou celui des entités marquées `HasLogicalZ`.

3. **Un prototype hors dépôt, 238 lignes ajoutées et 17 retirées, change tout cela.**
   - La suite complète (Debug, environ 5 100 tests, copie du HEAD) n'a qu'un seul test rouge de plus qu'avant : **UH-14**, l'épingle de phase, comme prévu. Les 9 échecs de base sont des échecs d'environnement (dossier `Musics` absent), identiques avant et après.
   - Les quatre traces du héros et la trace d'intro restent identiques à l'octet.
   - Sur l'ascenseur, l'écart pied du héros − dessus de la plateforme vaut 0 à chaque image. Le tonneau suit aussi. Une pile héros / tonneau / plateforme qui avance de 1 px par tick en X et en Z suit sans écart.

4. **Quatre trouvailles corrigent ou précisent l'étude du 17/10.**
   - La force copiée au passager doit être **récursive** pour les piles. Le porteur du milieu d'une pile donne ce qu'il reçoit, pas sa propre gravité. Sans cela, la plateforme ne monte plus (§3).
   - Le plafond des porteurs est **dangereux seul**. Il exige « déplacer la cible d'abord » et une règle d'entrée pour un héros porté mais au sol du moteur. Sinon la plateforme se **bloque** sous son propre passager (observé, §5.4).
   - Les montages de tests (`ContactHost`, `JumpHeroRig`) sont en mode autonome. Ils ne calculent pas `RidingEntity` des non-héros et n'ont pas d'identifiant de passe. Les arcs avec `RealController` tournent en ordre de production.
   - L'état au repos d'un passager sur une plateforme sans gravité change : `CollidedWithEntityZ` passe de 1 à 0, valeur du binaire.

5. **Corpus.** Les piles qui reçoivent la double force XY n'existent pas au chargement (§4). La correction ne se prouvera que par des témoins écrits.

6. **Recette.** Carte 22, préréglage `elevator-m22` (carte 22, case (46, 8), niveau 2). Il faut frapper le levier de droite, tomber dans le creux, sauter sur la plateforme, puis attendre. Le script de manette est mesuré et fonctionne (§6).

## 1. Où vivent les écarts au HEAD

Fichiers sous `Alundra/` : `Physics/ScriptedMotion.cs` = SM, `Physics/EntitySupport.cs` = ES, `Entities/EntityScriptProxy.cs` = EP, `Maps/WorldProxy.cs` = WP.

| Écart | Où au HEAD | Remarque |
|---|---|---|
| **M1** force du tick du porteur | SM:313-325 : `hero.ForceZ = carrier.ForceZ` et `hero.FinalForceZ = carrier.TickForceZ`, valeur du tick précédent. `TickForceZ` : EP:294, écrit en EP:799-803 et SM:476. XY : SM:676-681 (`+= carrier.LastTickDeltaX/Y`), écrit en SM:717-718, champs EP:283-286. | `LastTickDelta` sert aussi aux portes de l'aimant : SM:427 et SM:569. Il faut le garder. |
| **M2** déplacer la cible d'abord | Héros : SM:330-342 (`TryFindSupport` rend déjà l'entité, `out _`) et SM:360 (plafond du héros avec `Collidables`). Marqués : SM:484-491 et SM:504 (`Array.Empty`, O-E0K-6). Contrôleur : EP:883-884 et la queue « pas trouvé », EP:1018-1019. Sans contrôleur : EP:1100-1115 (montée sans plafond d'entités), EP:1121 et EP:1143. | Quatre sites verticaux : héros, contrôleur, marqués, sans contrôleur. Boucle physique : WP:2977-2980 (`TickPhysics()` sans estampille). Pilotes autonomes : EP:1527 et EP:1546. Aucun `PhysicsPassId` : `IScriptHost.cs:96` n'a que `DrivesEntityTicks`. |
| **M3** `IsOnGround` en passe de fin | Héros : SM:225-230. Marqués : SM:548. Contrôleur : EP:900, 1005, 1019. Sans contrôleur : EP:1148. Tirage de tête : EP:1487 et EP:1497-1501. | Rien après la boucle physique de WP:2977-2980. |
| **M4** `RidingEntity` aux compteurs | WP:3000-3023 : `RunCountersPass` n'efface pas `RidingEntity`. Le commentaire WP:2994-2995 est périmé : le recalcul est par tick (WP:2963-2966, WP:3687-3700). Recalcul : ES:277-289 (tout collidable à `Gravity` sans `NoRiders`, héros compris), ES:297-308 (héros, appelé en SM:195-198), ES:311-367. | Clôt O-E20-9 et la note D-SM1-2. |
| **Plafond des porteurs** | SM:504 (liste vide), EP:1018-1019 et EP:1100-1115 (aucun test d'entités au-dessus). `ES.TryFindCeiling` (ES:200-263) existe mais ne rend pas l'entité. | |
| **Modes** | WP:821 : `DrivesEntityTicks => _world?.Game != null`. `Alundra.Tests/Support/ContactHost.cs:109` : `Rebuild()` ne fait que `BuildCollidables`. `JumpHeroRig.cs:69` : `Update` = Rebuild, `World.Update`, Rebuild. | Les arcs `RealController` ont un `Game` (`ArcRun.cs:187`) et suivent donc l'ordre de production. |

FAIT : l'ordre du binaire est
1. compteurs `0x80038998` (efface `+0x12C`, en `0x800389DC`) ;
2. listes ;
3. physique `0x80038364`, qui enchaîne :
   - la prépasse,
   - le balayage des passagers `0x800364C8`,
   - les forces de tous `0x80036828`,
   - la copie `0x80037364`,
   - les déplacements `MoveEntity` dans l'ordre des cases,
   - la passe de fin `0x80038064`.

## 2. « Tous les passagers » dans le binaire et dans la DLL

### Dans le binaire (FAIT, relu et rejoué)

- **Qui est passager.**
  - Il faut `(flags & 0x4100) == 0x100` : `Gravity` sans `NoRiders` (`0x800364FC`).
  - Il faut être dans la liste des collidables : bit `0x80`, `AnimFlags & 0x80 == 0`, `+0x28 == 0`.
  - Cette liste n'a **aucun test de statut**, et ne contient pas un objet tenu en main.
- **Quel porteur.** Tout autre membre de cette liste dont `dessus + 1 == ModdedZ` et dont les boîtes XY se recouvrent avec les fenêtres à demi-ouvert `+1`. Le premier trouvé gagne (`0x80036554-5C`).
- **La copie** `0x80037364` (appelée sur chaque entrée de la liste à `+0x12C ≠ 0`, `0x80038400-40`) :
  - elle fait d'abord une récursion sur le porteur du porteur, sans mémo (`0x80037388`) ;
  - elle ajoute `AdjustedForce` XY du porteur à la force finale du passager (`0x8003739C-B0`) ;
  - sans impulsion (`+0xF8 == 0`), `ForceZ` et `FinalForceZ` du passager reçoivent `FinalForceZ` du porteur (`0x800373BC-C8`).
- **Autres sites** :
  - un atterrissage sur une cible qui n'a pas bougé la déplace d'abord (`0x800376B8-DC`) ;
  - un plafond de même (`0x80037630-50`) ;
  - le plafond `0x80036D94` bloque une montée sous tout collidable au-dessus, **passagers compris**. Le passager doit donc avoir bougé avant, ce que la récursion garantit.

### Dans la DLL, par sorte de passager

| Passager | Chemin | Ce que fait la DLL aujourd'hui |
|---|---|---|
| Héros | SM:251-400 | Copie avec un tick de retard, état tenu par le tick. MESURÉ : il traverse la plateforme (§5.1). |
| Entités placées par la carte : caisses, jarres, coffres, PNJ, ennemis | `EvaluateEntitySupport` (EP:739-1044) | Le support n'agit que sur une plateforme **statique** : `candidateTop < pieds`. Une plateforme qui monte dépasse l'entité, qui tombe à travers. MESURÉ avec un tonneau (`P10`, `P2`). |
| Objets lâchés, projectiles, objets lancés (marqués `HasLogicalZ`) | SM:465-532 | Même résultat. MESURÉ (`P13`). |
| 12 préfabs sans contrôleur | EP:1092-1149 | Même règle par lecture. INFÉRENCE : non sondé. |
| Objet tenu (`+0x28`) | `IsEligibleSubject` | Exclu de la liste (ES:73-76), comme le binaire. g1a l'écrira. |
| XY d'un passager non héros | — | Aucune prise en charge. Seul le héros ajoute `LastTickDelta`. MESURÉ : le tonneau et le héros ne suivent pas un déplacement en X (§5). |

### Qui est passager dans le corpus (MESURÉ, 483 cartes, 9 741 enregistrements)

- **4 591 enregistrements, 137 types** sont passagers au sens du binaire (collidables à gravité sans `NoRiders`). Principaux types :
  - Caisse en bois générique : 995
  - Cruche générique : 914
  - Coffre au trésor : 518
  - Statue d'homme-lézard : 150
  - Ronces enflammées : 157
  - Tonneau : 76
  - Colonne de glace : 73
  - Les villageois (environ 25 types).
  - Les objets posés au sol de flags `0x230180`, `0x220180` et `0x200180`, qui sont des passagers du binaire.
  - Les projectiles ne sont pas collidables, donc pas passagers.
- **102 cartes ont un porteur en Z** (273 enregistrements). 85 d'entre elles ont au moins un passager possible au chargement. 53 ont des objets soulevables, 60 des coffres.

## 3. Les piles XY : le défaut du binaire, mesuré

**Défaut du binaire (MESURÉ, `vec_d5b_out.txt`, section PILE_XY).** Héros sur caisse sur plateforme qui avance de 1 px par tick en X :
- la caisse reçoit `FinalForceX = 131072`, soit 2 px par tick, parce que la récursion repasse et que le balayage la traite aussi ;
- l'écart caisse − plateforme croît de 65 536 par tick ;
- le héros reçoit 0 et reste sur place.

En Z il n'y a pas de défaut : la copie est une affectation, donc idempotente. La récursion y est même nécessaire : le porteur du milieu transmet la force de la plateforme, pas sa gravité.

**Forme corrigée, par PROTOTYPE.** Chaque passager ajoute, une fois, l'`AdjustedForce` de chaque porteur de sa chaîne ; la force Z d'un porteur lui-même porté est celle de son propre porteur (récursion). Résultat sur la vraie carte 22 (`P14`) : plateforme, tonneau et héros avancent et montent identiquement pendant 14 images (écarts 0 en X, en Z).

**Combien de piles réelles ?** (`census3.json`, `edges.py`, règle de repos validée : elle retrouve les empilements 390 (6 sur 4), 163 (15 sur 14) et 179 (12 sur 11) des gardes h1b1) :
- 426 « passager posé sur un autre enregistrement au chargement », réparties sur 132 cartes.
- **1 seule pile à 3 niveaux** : carte 67, caisse 24 sur caisse 22 sur coffre 16. Elle est statique, le bas ne bouge pas, donc le défaut n'y joue pas. Avec une tolérance de 8 px de hauteur, le compte reste à 1.
- **0 passager au chargement sur un porteur XY.**
- 3 passagers au chargement sur des porteurs Z : carte 344 (deux coffres sur de petites plateformes flottantes qui bougent de 1 px) et carte 444 (une pierre sur une trappe qui descend de 0,5 px par tick). INFÉRENCE : à HEAD, ces trois objets sont dépassés puis tombent.
- Les piles dynamiques (le joueur lance ou pousse un objet sur une plateforme mobile) ne se comptent pas statiquement. Borne haute heuristique : environ 30 cartes cumulent un porteur XY présumé et des objets soulevables.

## 4. Tests touchés

- **Un test change de valeur** : `Alundra.Tests/Hero/HeroObjectTopsTests.cs`, UH-14 (lignes 448-484). `hero.PosX − heroStart == −(tick − 1) × 65536` devient `−tick × 65536`. MESURÉ : le test rouge dit `tick 1: hero -65536`. Voisins à relire : (w5) de g1a, qui épingle « un tick de phase » (§8).
- **Gardes qui restent verts avec le prototype complet (MESURÉ)**, ce sont les garde-fous du chantier :
  - `HeroObjectTopsTests` : UH-6, UH-10, UH-11, UH-15, UH-17, UH-18 (×2), SJ-12, SJ-13.
  - `Entities/AbsoluteZWritesTests.cs:74-104` (piles de caisses des cartes 390, 163, 179 ; cas du plateau de la carte 83).
  - `Entities/NpcCharacterControllerMoverTests.cs` : les tests de support aux lignes 1809, 1930, 1973, 2002, 2127, 2186, 3087 et 3155.
  - `Entities/LogicalZStepTests.cs`, `Entities/LogicalZWorldTests.cs`, `Entities/ControllerlessVerticalStepTests.cs`.
  - `Entities/FloorHeightTests.cs`, `TerrainHeightTests.cs`, `NoGravityLandingTests.cs`, `AnimationImpulseTests.cs:366`.
  - `Hero/HeroApexAndCeilingTests.cs`, `Hero/HeroFallAndPadJumpTests.cs`, `Hero/LadderClimbTests.cs`.
  - `Entities/TickStructureTests.cs` (A8, liste des passes inchangée : pas de nouveau nom de passe).
  - `Events/EventProgramRunnerTests.cs`, `Events/ClassAndBranchOpcodesTests.cs`, `Entities/EntitySearchServiceTests.cs`.
- **Piège de support à ne pas reproduire.** Le premier essai faisait calculer `RidingEntity` de tout le monde dans `ContactHost.Rebuild`, héros compris. Il a fait passer au rouge UH-10, UH-15 et UH-17, parce que le `Rebuild` de fin d'image écrase la valeur de tête de tick du héros que ces tests observent. Corrigé dans le prototype en laissant le héros intact. Autre option, déjà écrite au plan pour UH-16 : le test appelle lui-même `EntitySupport.UpdateRidingEntities`.
- **Témoins à écrire, rouges d'abord** (valeurs : binaire rejoué, §7) :
  - UH-16, réservé par D5a et **absent** de `HeroObjectTopsTests.cs`. Son texte du plan dit « un tick de phase » et doit être réécrit en phase 0.
  - Un test d'atterrissage sur plateforme qui monte (nom suggéré UH-19).
  - UH-14 à valeur neuve.
  - Passagers : caisse avant ou après la plateforme, en montée et en descente ; PNJ ; pile Z ; pile XY corrigée ; passager à impulsion ; passager au repos sur plateforme sans gravité.
  - L'arc de la carte 22.
- **Test de préréglage** : `Alundra.Tests/SaveGames/TestSavesTests.cs`, liste des noms ligne 74 (`AllPresetNames`) et test de l'ordre ligne 90. Plusieurs tranches d'E20 ajoutent aussi des préréglages : recompter au HEAD de départ.

## 5. Mesures sur la vraie DLL

Détail dans `final_baseline_probe_out.txt` (HEAD) et `final_prototype_probe_out.txt` (prototype). Sondes dans `D5bProbeTests.cs`.

### 5.1 Ascenseur de la carte 22, production, levier 2 déjà frappé

| | HEAD | Prototype |
|---|---|---|
| Image 12, la plateforme monte de 25 px | héros à 32, écart −1 px | héros à 33, **écart 0** |
| Image 13 | porté perdu | porté |
| Image 24 | héros à 16 (creux), écart −29 px | écart 0 |
| Image 76, `G129` levé, avertissement | héros à 16, écart −80 px | héros à 97, **écart 0**, `IsOnGround` 1 |

La plateforme va de 24 à 89 px en 64 images, identique dans les deux cas. Dans le prototype, `IsOnGround` ne vaut 1 pendant la montée qu'avec la passe de fin (M3).

### 5.2 Descente (la plateforme est pilotée par le test)

- HEAD : le héros flotte à +1 puis +1,5 px au-dessus (F2 de l'étude).
- Prototype : écart 0 à chaque image, porté, `CollidedWithEntityZ` 0, comme le binaire. Cela exige M2 : un premier essai « M2 faux » flottait encore.

### 5.3 Piles et passagers

- Tonneau sur la plateforme :
  - HEAD : écart −1, −3,5, −6 px… jusqu'à −81 px, il tombe dans le creux.
  - Prototype : écart 0 pendant les 64 px.
- Le même tonneau marqué `HasLogicalZ` (`P13`) : même résultat dans les deux cas.
- Pile héros / tonneau / plateforme, avance de 1 px en X et en Z (`P14`) : prototype, écarts 0 pendant 14 images. HEAD : rien ne suit.

### 5.4 Plafond des porteurs et gel

- Héros qui tombe sur une plateforme qui monte (`P11` à 1 px par tick, `P12` à 3 px par tick) :
  - HEAD : atterrissage sur l'ancien dessus, la plateforme le traverse, il tombe au fond.
  - Prototype `P12` : à l'image 4 la plateforme **s'arrête sous ses pieds** (montée de 2 px au lieu de 3, contact 1), puis elle le porte (écart 0, `ForceZ` copiée 196608). C'est le motif du binaire (critique : plateforme 3 px par tick, héros 1 px au-dessus : montée de 32 768 puis 196 608).
- **Gel observé** :
  - Montage du quai avec le plafond mais **sans** la règle d'entrée (`P15`) : le héros à cheval sur le sol et la plateforme n'avance pas, et la plateforme **ne monte pas du tout** (bloquée sous lui).
  - Avec la règle d'entrée, il suit.
  - Un autre gel est apparu avec une force non récursive dans la pile.
  - Lecture binaire : `0x80036D94` compte les passagers comme des plafonds. Ils ne bloquent pas la plateforme parce qu'ils ont déjà bougé, d'où la récursion « déplacer la cible d'abord ».

### 5.5 Dock

HEAD, montage UH-17 avec un objet qui monte (`P15`) : le héros à cheval reste au sol, l'objet le traverse. Binaire (`t5`, rejoué) : il monte avec l'objet. La règle d'entrée du prototype reste à valider (un montage où l'objet précède le héros dans l'ordre du monde garde 1 px de décalage, artefact de montage sans passe).

## 6. Recette : l'ascenseur de la carte 22

**Géométrie (FAIT).**
- Carte « Lars' Crypt-22 », 52 × 60 cases.
- Enregistrement 2 : « Plateforme flottante (grande) », 48 × 32 × 8, en (1 008 ; 176), z = 24, dessus à 32 px, sprite 57.
- La plateforme flotte au milieu d'un creux de 6 × 4 cases à 16 px de profondeur. Son dessus est **à fleur** du sol de la salle (32 px).
- Le héros n'a donc jamais de sol moteur à 32 px sous lui sur la plateforme, mais il en aurait un en bord de creux : c'est le cas du quai.
- Leviers : enregistrement 0 en (900 ; 120), enregistrement 1 en (1 116 ; 120), z = 32.

**Programmes (FAIT).**
- Levier 2 (enr. 1) : toucher bascule `T3` (D[2], pc 1596).
- Levier 1 (enr. 0) : bascule `T1`.
- Plateforme C[3] (pc 720-765) :
  - attend `T1` éteint, `T3` allumé et `0x3E` vrai (le héros la chevauche) ;
  - puis `0x10`, `0x37 Wait 10`, `T5`, `0x1B [0,1]` (ForceZ = 65 536, 1 px par tick), `0x20 [64,0]`, `T5` éteint, `G129` allumé ;
  - puis `0x53 [22,0,15,20,5,…]` (retour dans la même carte, case (15, 20)).
- `0x20` mesure le Z de la plateforme elle-même : le script se termine avec ou sans héros dessus.

**Sauvegarde de test.**
- Les drapeaux temporaires (`T1`, `T3`) ne se sauvent pas. Il faut donc frapper le levier en jeu.
- Préréglage proposé : `new Preset("elevator-m22", "test-elevator-m22", 22, 46, 8, 2, [], [], [], [])` dans `Alundra/SaveGames/TestSaves.cs`.
- Arrivée au centre de la case (1 116 ; 136), z = 32, soit juste au sud du levier de droite. L'épée est celle de la nouvelle partie (`PlayerManager.cs:2065`, arme 1).
- Mise à jour : `TestSavesTests.cs`, `docs/test-saves.md`, et le test des noms.

**Arc réel mesuré avec manette (prototype et HEAD identiques jusqu'à la montée) :**
1. Arrivée en (1 116 ; 136, niveau 2), direction nord (16).
2. 30 images au repos, puis Carré : `T3` et `T4` s'allument.
3. Maintenir Bas jusqu'à y ≥ 186 : 32 images.
4. Maintenir Gauche jusqu'à x ≤ 1 094 : 10 images.
5. Croix + Gauche : le héros atterrit dans le creux à x = 1 042, z = 16 (23 images).
6. Croix + Gauche de nouveau : il atterrit sur la plateforme à l'image 17 du second saut, porté à l'image 18.
7. La plateforme part 12 images plus tard (`T5` allumé), 64 px de montée, puis `G129` et l'avertissement à l'image 53 d'attente.

Résultat attendu, avec le prototype : écart pied − dessus = 0 à chaque image de la montée.

Pour l'auteur : « au sud du levier de droite, le frapper, descendre dans le creux, sauter sur la plateforme, attendre : Alundra monte avec elle sans glisser ni retomber ; aujourd'hui elle reste en bas ».

## 7. Valeurs écrites d'avance (binaire rejoué)

Fichier : `R/vec_d5b_out.txt`, généré par `R/annex/work/vec_d5b.py`. Les valeurs relatives sont identiques dans la convention de la DLL, qui est celle du binaire moins une unité brute. Elles ont été relancées avec de nouvelles graines : 3 000 vecteurs Z et 300 vecteurs XY, 0 écart du modèle de l'annexe. Mutants tués : 12 sur 13 ; `no_recursion` survit seulement sur les vecteurs Z, il est tué par les vecteurs XY de pile.

- **E22** : plateforme 48 × 32 × 8, héros dessus, `+65536` à partir du tick 3 pendant 64 ticks. Héros rel 0, porté, contact 0, `IsOnGround` 1, `FinalForceZ` 65 536 puis 0 à l'arrêt.
- **DESC** : même chose à −65 536 pendant 24 ticks.
- **CRATE** : caisse 21 × 15 × 16 de flags `0x3323E4`, avant ou après la plateforme dans l'ordre des cases. Écart 0, porté, contact 0, `IsOnGround` 1, `ForceZ` copiée 65 536 pendant la montée.
- **NPC** : même règle, flags `0x03A180`.
- **PILE_Z** : tous les écarts valent 0.
- **PILE_XY** : défaut décrit au §3.
- **Atterrissage sur plateforme qui monte** (critique, rejoué) : héros 1 px au-dessus, plateforme +3 px par tick → montée de 32 768 au tick 0 puis 196 608, écart 0, porté dès le tick 1. Avec 0,25 px et +1 px par tick : montée de 16 384, contact 1 au tick 0.
- **Quai** (`t5`, rejoué) : écart 0, porté, quelle que soit la position à cheval.
- **Téléportation** (`t7`) : atterrissage au tick 11 en 2 228 224.

## 8. Découpage proposé

Notation : T = tâche, un commit par tâche, plan mis à jour dans le même commit, statuts à icônes. Prototype de référence : `R/proto/prototype.diff`.

**T0, docs**
- Annexe `docs/plan-e19-d5b-exec-annexe/` (nom à fixer). Contenu : `vec_d5b.py` et sa sortie, les scripts de recensement et leurs sorties, les sondes et les deux sorties de sonde, le diff de prototype comme preuve de faisabilité et non à appliquer tel quel, `SHA1SUMS.txt`.
- ADR (prochain numéro libre, 0082 au HEAD, à recompter) : « les passagers suivent leur porteur dans l'ordre forces puis déplacements ».
- Acceptation : le README liste les mesures et leurs commandes.

**T1, témoins rouges d'abord, nouveaux fichiers**
- Aides de test :
  - `ContactHost.PhysicsPassId` émulé ;
  - `Rebuild` qui calcule `RidingEntity` des non-héros, **héros exclu** ;
  - appel de `EndPhysicsTick` par le montage à la fin de l'image ;
  - les pilotes autonomes doivent appeler `TickPhysicsOnce`.
- Témoins du §4. Arc A-D5b-22 sur le script du §6 et un arc de pile (tonneau, `P14`).
- Acceptation : tous rouges sur HEAD sauf les gardes ; leur ensemble est la « liste rouge ».
- Arrêt : un témoin vert sur HEAD sans l'avoir prévu.

**T2, M4**
- `RunCountersPass` efface `RidingEntity` ; correction des commentaires périmés.
- Acceptation : suite inchangée, A8 inchangé.
- Retour arrière : revert.

**T3, M1 + récursion + règle d'entrée + M3 pour le héros**
- `PeekTickForceZ`, `CarrierTickForceZ` (récursif, utilise `TickForceZ` si la cible a déjà bougé dans la passe), règle d'entrée « porté par un porteur dont la force du tick est non nulle », `EndPhysicsTick` après la boucle physique, passe unique de la force (une seule fonction de force partagée par le tick et le peek).
- Acceptation : UH-16 montée, E22 (arc : écart 0 de l'image 12 à 76), UH-18 inchangé.

**T4, M2 et plafond des porteurs, dans le même commit**
- `PhysicsPassId` (membre par défaut de `IScriptHost`, `WorldProxy` l'incrémente avant la boucle physique), `TickPhysicsOnce` dans le monde et les pilotes autonomes, `EnsureMovedThisPass`, nouvel essai du support et du plafond après déplacement de la cible. À poser sur les quatre sites verticaux.
- Acceptation : UH-16 descente, UH-19 (héros sur plateforme qui monte), tests de gel : une plateforme avec un passager et une avance non nulle ne reste pas immobile.
- Arrêt : toute plateforme immobile alors que `ForceZ ≠ 0` plus de 2 ticks avec un passager qui la chevauche.

**T5, M5 : XY planifié pour le héros**
- `PeekAdjustedForce` en pur, remplace `LastTickDelta` dans la copie du porté. `LastTickDelta` est conservé pour les portes de l'aimant.
- Acceptation : UH-14 réépinglé (`−tick × 65536`, valeur du binaire, `t3`).
- Arrêt : toute autre épingle qui bouge.

**T6, passagers non héros**, un commit par chemin :
- A : contrôleur ;
- B : marqués ;
- C : sans contrôleur.

Pour chaque chemin :
- copie de la force, nouvel essai après déplacement, plafond, `IsOnGround` en passe de fin ;
- XY transitif et une seule fois par porteur de la chaîne ;
- témoins CRATE, NPC, PILE_Z et PILE_XY corrigée, passager à impulsion, passager au repos (contact 0, `IsOnGround` 1).

Remarque : le chemin A est provisoire si E19.h2 porte un jour les entités de la carte vers le modèle B.

**T7, préréglage, arc et documentation**
- `elevator-m22`, test des noms, `docs/test-saves.md`.
- Arc A-D5b-22 vert, comptes vérifiés.
- Commentaires périmés corrigés : ES:291-296, WP:2994-2995, SM:193-194, EP:279-281.

**T8, vérification**
- Vérificateur frais sur la tranche entière.
- Mutations par mécanisme, avec ce qu'elles font passer au rouge :
  - copie de force retirée → UH-16a rouge ;
  - récursion retirée → pile Z rouge ;
  - « déplacer d'abord » retiré → descente rouge ;
  - plafond retiré → UH-19 rouge ;
  - plafond sans « déplacer d'abord » → gel rouge ;
  - passe de fin retirée → `IsOnGround` rouge ;
  - règle d'entrée retirée → quai rouge ;
  - M4 retiré → `RidingEntity` périmé rouge ;
  - mémo XY retiré → pile XY rouge ;
  - `LastTickDelta` remis → UH-14' rouge.
- Suite entière en Debug puis Release. Release **avec copie neutralisée** (sinon la DLL Debug du jeu est écrasée). Les traces du héros sont réécrites par la suite : garde d'octets par `git checkout`.

**T9, recette de l'auteur** : §6. Elle ne bloque pas le merge.

**Arrêts généraux** : un écart au binaire sur un vecteur du §7 ; une trace qui change ; plus d'un test rouge inattendu.

**Retour arrière** : chaque tâche se revert seule. T4 ne se retire pas sans T5 ni T6 qui en dépendent.

**Dépendances avec les tranches en cours**
- **f6a**
  - Fichiers communs : `EntityScriptProxy.cs`, région `TickMotion` (lignes d'aujourd'hui EP:1614-1635, EP:1594-1611, docs EP:1956-1962 et EP:1915-1918).
  - f4 et f5 touchent SM autour du speed rule.
  - Aucun lien sémantique : D5b doit venir **après** f6a, avec les lignes relues au HEAD de départ.
- **g1a**
  - `PutInHands` vide `RidingEntity` de l'objet et pose le marqueur (D-G-12, D-G-13).
  - La branche de suivi de `TickMotion` pour `PlatformEntity != null` est exclue de la liste des collidables : pas de conflit.
  - (w5) de g1a épingle « un tick de phase ». **Si g1a passe avant D5b**, T5 réépingle aussi (w5) ; **si D5b passe avant**, g1a l'écrit en phase 0. Les objets lâchés ou lancés marqués deviennent des passagers du chemin B.
  - Recommandation : D5b après f6a et avant g1a.

## 9. Questions

### Au plan, techniques

1. `PhysicsPassId` comme membre par défaut de `IScriptHost`, et `TickPhysicsOnce` dans les deux pilotes : accepté ?
2. Peek pur (prototype) ou découpage fidèle forces / pas du tick cinématique ? Dans les deux cas, une seule fonction de force pour éviter la divergence.
3. Règle d'entrée du héros porté mais au sol du moteur : condition « porteur avec force du tick non nulle » (prototype) ou « toujours quand `RidingEntity` ≠ nul » ?
4. Passagers au repos sur plateforme sans gravité : `CollidedWithEntityZ` passe de 1 à 0 (valeur du binaire, mesurée). Impact des scripts de PNJ à vérifier à l'exécution.
5. Statut 4 dans la liste des collidables : le binaire ne l'exclut pas, `ES.cs:53-55` l'exclut. À garder hors D5b, comme Q-D5B-6 le laisse ?
6. Planifié ou réalisé pour le XY du porteur arrêté par un mur : le binaire passe l'`AdjustedForce` planifiée. Le prototype fait de même.
7. Chemin C (sans contrôleur) non prototypé ; non sondé en jeu.
8. Les piles de profondeur supérieure à 8 : borne du prototype.

### À l'auteur, en termes de jeu

1. Pour la recette de la carte 22, le départ proposé est au sud du levier de droite : il faut frapper le levier, tomber dans le creux, sauter sur la plateforme. Un départ déjà sur la plateforme est impossible, puisque l'état du levier ne se sauvegarde pas. Cela vous convient-il ?
2. Une plateforme qui monte et sur laquelle on se tient à cheval entre le sol et son bord emporte le joueur (l'original). C'est le comportement retenu, sauf avis contraire.
3. Après correction, les coffres de la carte 344 posés sur de petites plateformes qui bougent de 1 px bougeront avec elles, au lieu de tomber. Même chose pour la pierre de la carte 444 sur la trappe qui descend.

## 10. Limites

- Le chemin C n'est pas prototypé. Le mode autonome des montages n'a pas de passe : l'émulation du plan est à écrire.
- Le recensement des porteurs XY est une borne haute heuristique ; le décompte des piles au chargement est exact.
- Rien n'a été fait sur D5c.
- Le prototype n'est qu'une preuve de faisabilité : lignes avec marqueurs « PROTOTYPE », pas de mutation, Debug seulement.
- La suite de la copie a 9 échecs d'environnement identiques avant et après.

## 11. Fichiers

- Binaire, recensements :
  - `R/vec_d5b_out.txt`, `R/annex/work/vec_d5b.py`
  - `R/census1.py`, `R/census2.py`, `R/census3.py`, `R/census3.json`, `R/edges.py`, `R/agg.py`
- Cartes :
  - `R/map22.py`, `R/map22_dump.txt`, `R/map83_dump.txt`, `R/map344_dump.txt`
- Sondes et mesures :
  - `R/probe/Alundra.Tests/Hero/D5bProbeTests.cs`
  - `R/final_baseline_probe_out.txt`, `R/final_prototype_probe_out.txt`
  - `R/trx0/base.trx`, `R/trx5/proto5.trx`, `R/trxsum.py`
- Prototype :
  - `R/proto/prototype.diff`
  - `R/proto/proto_ceiling.py`, `R/proto/proto_m5.py`, `R/proto/proto_pathb.py`, `R/proto/proto_xy.py`
- Copies de travail : `R/probe` (prototype complet), `R/probe0` (baseline).

`R` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d5b-disc`