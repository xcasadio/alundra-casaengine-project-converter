# Rapport t2-next / a362-h3a : l'arc E20.A362 et la révision de h3a (lecture seule, 2026-10-25)

**Cadre.**
- HEAD lu : `8174c0cb`, branche `chantier/theme2-attaques-objets`.
- Code de la DLL et des tests lu par `git show HEAD:` et `git grep HEAD`. Docs, `data-extracted/` et annexes lus directement.
- Aucun build, aucun test dotnet, rien écrit dans le dépôt.
- L'arbre de travail montre des modifications d'un executor : `Alundra/Events/EventProgramRunner.cs`, `Alundra/SaveGames/TestSaves.cs`, `Alundra.Tests/SaveGames/TestSavesTests.cs`, `docs/test-saves.md`, les traces 389. Mes numéros de ligne sont ceux de HEAD et bougeront.
- Je n'ai pas relu le binaire au désassemblage. Les faits binaires viennent de l'annexe `e20-h3` (la critique fait foi) et du rapport c2b. J'ai rejoué leur oracle depuis une copie.
- Étiquettes : [FAIT] lu dans le code, les données ou le binaire cité ; [MESURÉ] sorti de l'oracle MIPS rejoué ; [INFÉRENCE] déduit, base donnée.

## 0. En bref

1. **A362.** La DLL du HEAD exécute toute la scène : les 48 opcodes de la carte 362 ont un `case` (A8).
   - Je prédis que le couvercle réagit déjà : le drapeau T6 est posé et le couvercle passe en statut 3 (B3 à B6).
   - Il ne disparaît pas : `(E, 19)` est absent (B5).
   - La bombe d'Olen reste en statut 3, invisible : `(E, 12)` est absent (B6).
   - Je n'ai pas trouvé d'autre manque au niveau du code.
   - Le point non établi est l'atterrissage de la bombe dans la DLL (B9, R2).
2. **Le héros est loin de l'explosion pendant la scène** : il est promené de 48 px vers le bas et a perdu le contrôle (A7). Il n'est pas blessé, avant comme après c2b.
3. **D-H-7 est faux, et je l'ai mesuré sur l'oracle lui-même (C1).**
   - Avec l'octet de réponse au type 4 posé à `0x80`, les PV du héros restent à 10, mais il est quand même touché.
   - Il passe en `0x31` et recule de 33 px, exactement comme sans le réglage.
   - La DLL ne peut produire cet état dans aucune version.
4. **Les chiffres de h3a ne changent pas.**
   - Les cinq nombres de (w1) (+1, +17, +52, +53, +54) sont identiques avec le héros loin ou dessous.
   - Il faut seulement écarter le héros dans (w1) à (w5).
   - Il faut ajouter une clé neuve « héros dans la zone » (w6), produite ici (`k_w1_17east.txt`).
5. **Clés de h3b : aucune ne dépend de D-H-7.**
   - Les deux clés où le héros est blessé (`bomb_timeline`, `hurt_circle` prolongée) se rejouent telles quelles.
   - Trouvaille : lancée vers le bas puis laissé sur place, le héros se blesse avec sa propre bombe, à 0,5 px près (C9).
6. **Le plan compte « deux épingles du registre » ; le HEAD en a quatre** (D4).

## 1. L'arc E20.A362 (ligne 9b)

### 1.1 La scène (données de `map_362.json`, programmes décodés par `h3critic/my_maps2.py`, sortie dans `out/map362_programs.txt`)

**A1 [FAIT]** Carte 362 : zone « Coal Mine _ Olen's Dream », monde `Coal Mine _ Olen's Dream-362`.
- Grille 52 × 60 cases ; sol de niveau 2 (32 px) autour d'Olen et du couvercle (cases lues).
- On y arrive de la 185 : B[2] `0x53 @147`, arrivée (252, 264, z 32) = tuile (10, 16) niveau 2 (`Alundra.Tests/Arcs/EntityContactArcTests.cs:126-135`).
- On en sort par B[2] `0x53 @124` vers la 366.
- Ses portails mènent à la 363.
- Elle est dans `ExemptCombatMaps` (`Alundra.Tests/Support/StoryChainOpcodeAudit.cs:34`), pas dans les 30 cartes de la chaîne.

**A2 [FAIT]** Enregistrements.
- Position d'un enregistrement : `(XPos·12+12, YPos·8+8, Height·8)` px.
- Au chargement, seuls ceux dont `SpriteDirection & 0x40` est posé apparaissent.

| rec | sprite | position px (z) | rôle | présent au chargement |
|---|---|---|---|---|
| 0 | global 243 | (216,104) z32 | couvercle ; A[1] C[3] D[1] ; E natif 19 | oui |
| 1 | global 26 | (192,200) | bloc sans programme | oui |
| 2, 3 | carte 10 et 9 | (228,200), (420,168) | PNJ (C[1], C[2], F[1], F[2]) | oui |
| 4 | carte 8 | (204,120) z32 | Olen ; C[4] ; F[3] | oui |
| 5 | global 2 | (396,40) | bombe ; C[5] ; D et E natifs 30 et 12 | non (naît par `0x8B`) |
| 6 à 9 | carte 91 (fiche 347) | nés par `0x8A` de B[4] | 4 Murutas, C[6..9] | non |
| 10 | global 30 | né par `0x8A` de B[7] | coffre, contenu 79 | non |

**A3 [FAIT]** Couvercle.
- D[1] @728 : `0x87 [128,3]`, puis `If false goto 738` (End), puis `0x05` T6, puis `0x19`.
- C[3] @472 : `0x89` décale le couvercle de (−4,−4) px, une seule fois. Il est donc en (212,100). Puis 3 `Break`, puis `End`, qui reste collé.
- En-tête du sprite 243 :
  - drapeaux `0xA1` = Collidable + DeactivateOnHit + ClassA ;
  - corps 48×8×44, décalage (−23,0,0) ;
  - E natif 19, effet de bris 11.
- Fiche d'équilibre 243 : PV 1, réponse `0x80` à tous les types sauf le type 4 (`0x10`).
  - L'épée (type 1) ne le « touche » donc pas. Seule une attaque d'attribut 4 déclenche D.

**A4 [FAIT]** Bombe, sprite global 2.
- En-tête : drapeaux `0x13234E` = ClassB + HitsClassA + HitsClassB + DeactivateOnAnimationEnd + gravité ; programmes natifs C62, D30, E12.
- L'enregistrement a C = 133, donc le script C[5] remplace C62.
- Fiche 2 : une attaque (attribut 4, puissance 5).
- Animation 1 :
  - 17 images, son 46 ;
  - boîtes aux images 4 à 10, de 16×8×11 à 48×32×31 ;
  - `AnimFlags = 0x40` (intouchable pendant l'explosion) ;
  - la dernière image n'a aucun dessin : la bombe devient invisible à la fin.
- Animation 2 : vitesse 128, impulsion 1024.

**A5 [FAIT]** Script d'Olen, C[4] @488 (ticks de `Wait n` = n+1).
- Il boucle en attendant T2, posé par F[3] quand le héros lui parle.
- Sans G248, il dit le dialogue 131 et retourne à la boucle. Avec G248, dialogue 132, puis T3.
- G248 n'est posé que par la 363 (C[1] @198), seul écrivain sur 483 cartes (`out/scan_flag248_setters.txt`).
- B[3] @144 promène le héros de 48 px vers le bas, puis efface T3.
- Olen marche 16 px vers le bas (anim 1, 1,5 px/tick, ≈ 11 ticks).
- Il se tourne vers le haut, anim 14, son 44.
- **`0x8B [128,5,0,0,0,0,32,0]` @569, notons son tick S.** La bombe naît à la position d'Olen, z + 32 px, regard `CardinalDirectionTable[SpriteDirection&3 = 1] = 0x10` = vers le haut.
- `Wait 30` jusqu'à S+31, puis anim 15 et **T4** (pc 582).
  - L'anim 15 dure 35 ticks.
  - Olen marche 72 px vers l'est (anim 9, 2,44 px/tick ≈ 30 ticks), puis attend 11 et 31 ticks.
  - **T5** (pc 608) tombe à environ S+140, à quelques ticks près [INFÉRENCE : somme des attentes et des durées d'animation].
- La bombe C[5] @628 :
  - gravité basse (`0x17`, flag Gravity effacé), `Fly 0`, attente de T4 ;
  - anim 2 et gravité haute ;
  - attente de T5, puis anim 1.

**A6 [FAIT]** Programmes de carte.
- B[4] @168 attend T6 : balancement de caméra 60 ticks, quatre naissances à 30 ticks d'intervalle, dialogue 133, `Player gain control`, **T7** @228.
- B[2] @64 attend T7, puis 756 ticks, dialogue 130, `0x53` vers la 366.
- B[7] fait naître le coffre quand les 4 Murutas ont disparu.
- B[6] @284 : `0x3B [10,10,16,16,2,2]` n'est vrai que sur la tuile (10,16) niveau 2, l'arrivée de la 185. Il ouvre alors le dialogue 84.
  - Un arc qui arrive ailleurs ne le déclenche pas.

**A7 [FAIT]** Pendant la séquence, le héros n'a plus le contrôle : `0x10` @514 jusqu'à B[4] @227.
- Il est promené de 48 px vers le bas par B[3].
- Il est donc à plus de 60 px de l'explosion (centre ≈ y 113 contre y ≥ 150).
- Même dans la zone, un coup serait perdu : branche verrouillée (D-C2B-13, §1.12.1).

**A8 [FAIT]** `out/scan_opcodes_362.txt` : les programmes de la 362 emploient 48 opcodes distincts (Break et End compris). Tous ont un `case` dans `Dispatch` au HEAD.

### 1.2 Ce que fait la DLL au HEAD

**B1 [FAIT]** `0x8B` (`EventProgramRunner.cs:1249`, `2461-2487`).
- Naissance par `SpawnEntityByRecordId`, sans garde de zone.
- Position : celle de la cible plus `(v3|v4<<8, v5|v6<<8, v7|v8<<8) << 16`, puis `JoinsBirthTick` en dernier (e0a4).

**B2 [FAIT]** Recherche `128` = l'entité de référence elle-même (`Alundra/Entities/EntitySearchService.cs`, doc, cas 0).
- `0x87 [128,3]` se lit donc « le couvercle est touché par une attaque d'indice 3 ».
- `HitOpcodes.TypeNibbles[3] = 4` (`Alundra/Combat/HitOpcodes.cs:15-22`) : l'attribut 4 de la bombe.
- `IsTouchedByAttackType` exige que l'attaquant ait encore une boîte (`:29`, `:24-26`).

**B3 [FAIT]** Passe de coups (`Alundra/Combat/HitPass.cs:88-165`, appelée à `Alundra/Maps/WorldProxy.cs:3137`).
- La bombe a la classe `HitsClassA|HitsClassB`, donc le masque ClassA et ClassB.
- Le couvercle (ClassA, compteurs à 0, `AnimFlags` sans `0x40`) est donc une cible valide.
- Chevauchement en entiers, élargi d'une unité brute, sur les trois axes.
- Réponse `0x10`, donc non immune : `TouchingEntity = bombe`, `FrameCollisionTickCounter = 25`, 3 étincelles.

**B4 [FAIT]** Tick suivant (`Alundra/Entities/EntityScriptProxy.cs:1911-2005`).
- Le couvercle est à l'état Normal. `HitCounter` vaut 0 (il n'attaque pas). `TouchingEntity` n'est pas nul : slot D.
- Les slots A, D, E, F repartent d'un état neuf à chaque appel ; B et C reprennent. Break avance, End colle (`EventProgramRunner.cs:171-253`, `410-422`).
- D[1] s'exécute donc en entier : T6 posé, `0x19` met `Status = Deactivated` (`:718-720`).

**B5 [FAIT]** Aux ticks suivants le couvercle est en slot E.
- `ProgramIndexes[E] & 0x7f == 0`, donc `RunSpriteEvent` (`:272-331`).
- Ce n'est pas le livre de sauvegarde.
- `IsInlineDeactivateCode(19)` est faux (`:336` : 0, 1 et 10).
- La clé `(E, 19, type 243)` n'est pas dans le registre : `CreateForProduction` (`Alundra/Ai/NativeSpriteRegistry.cs:36-52`) n'appelle aucune inscription pour elle.
- Résultat : no-op compté. Le couvercle reste visible et solide en statut 3.

**B6 [FAIT]** Même mécanisme pour la bombe.
- `(D, 30)` et `(E, 12)` sont absents.
- À la fin de l'anim 1, `DeactivateOnAnimationEnd` la met en statut 3 (`EntityScriptProxy.cs:1952-1958`), puis no-op : elle reste dans le monde, invisible.
- Elle compterait aussi dans le plafond de 3 bombes de h3b.

**B7 [FAIT]** La scène continue sans le couvercle : T6 posé, donc B[4] part (balancement, 4 Murutas) ; T7 libère leur IA native C10, enregistrée par E14.a1c.

**B8 [FAIT]** Aucun test ne charge la 362 comme monde.
- `git grep` ne trouve qu'un arrêt à l'enregistrement d'arrivée (TA19), la garde d'export des coffres (`ExportGuards/ChestExportGuardTests.cs:105`) et `StoryChainOpcodeAudit`.

**B9 [INFÉRENCE]** Prédiction au HEAD.
- Le vol part vers le nord depuis (204 ± 1, ≈136). Il atterrit à ≈ 23,5 px, d'après l'oracle de lancer vers le haut (`throw_dirs.txt` : −23,00 puis 0,5 de glissade).
- Le centre de l'explosion tombe vers y ≈ 113 à 115 (le corps du couvercle, Collidable, peut arrêter le vol 2 px plus bas).
- Les boîtes finales couvrent y de 97 à 131 (profondeur 32) et x de 180 à 228 (largeur 48), et recouvrent le couvercle (x 189 à 237, y 100 à 108, z 32 à 76).
- La première boîte (16×8) ne touche pas, d'un pixel, d'après cette géométrie. Le contact vient donc à la boîte de 28×20 ou à la suivante, soit de A+19 à A+25.
- **C'est le point à mesurer** : l'atterrissage dans la DLL (contacts entre entités E19.d2b, entités nées par script) n'est pas prouvé par la lecture.

**B10 [INFÉRENCE]** Chronologie prédite, avec A le tick où C[5] pose l'anim 1 et H le tick du coup (fin de passe).

| tick | événement |
|---|---|
| A (≈ S+140) | anim 1 et son 46 |
| A+16 à A+30 | boîtes ; H est le premier tick où elles recouvrent le couvercle |
| H+1 | D[1] aux pc 728, 731, 734, 737 ; T6 posé ; couvercle en statut 3 |
| H+2 | B[4] voit T6 (les événements de carte passent avant les programmes d'entités) ; `0x8E` @172 ; au HEAD, `(E,19)` no-op |
| H+2 (après h3a) | son `0xE6` et effet de bris 11 ; statut 4 |
| H+3 (après h3a) | case libre (même séquence que `k_w5_lid.txt` : K+1 puis K+2) |
| A+51 | drapeau de fin de l'anim 1 (51 ticks après le départ, la 52e image étant le terminateur, comme la clé : +1 puis +52) |
| A+52 (après h3a) | E12 : `DestroyEntity(E,-1)`, statut 4 |
| A+53 (après h3a) | case libre |

Au HEAD, le couvercle et la bombe restent en statut 3.

### 1.3 Le banc

**B11 [FAIT]** Précédents à copier (A362 est « comme E20.c1b-2 »).
- Banc `ArcRun` avec `ArcSpec(Name, Zone, WorldName, Flags[], tuile, FrameLimit, RealController: true, Prefabs: true, Arrival: …)` (`Alundra.Tests/Support/ArcSpec.cs`).
- Exemple d'`ArcArrival` : `Alundra.Tests/Ai/Objects/C4WorldTests.cs:370`.
- Parler à un PNJ : `((IScriptHost)arc.Proxy).ActiveCollisionEntity = arc.EntityByRecord(4)` (`Arcs/VisionAndCoastArcTests.cs:484`, `Arcs/InoaDayOneArcTests.cs:373`). Il se trouve qu'Olen est aussi l'enregistrement 4.
- Fermer les boîtes de dialogue : `RunUntilPressingTheButtonOnEveryDialogueFrame`, `CloseDialogueWithTheButton` (`ArcRun.cs:491`, `511`).
- Assertions : `ArcChecks.AssertGap`, `FrameOf`, `AssertNothingSkippedOrExceeded`, `AssertNoUnexpectedError`.
- Prefabs : `Bombe`, `Couvercle de tige-bombe (dessus|horizontal|vertical)` existent dans `alundra-project/Entities`. Le dossier de la carte a ses `dialogues`.

**B12 [FAIT]** Les préréglages sont `Alundra/SaveGames/TestSaves.cs`. Il n'y en a aucun sur la 362 ; la recette du plan est « par Change map ».
- Sans G248, une arrivée directe dans la 362 ne montrerait que le premier dialogue en boucle (A5).

### 1.4 Ce que l'arc doit asserter

Règle de c1b-2 : toute valeur est écrite avant l'exécution, sans refaire une attente sur le résultat.

**Proposition d'arc.**
- `ArcSpec("E20A362", "Coal Mine _ Olen's Dream", "Coal Mine _ Olen's Dream-362", Flags [248], …)`.
- Arrivée au pixel (204, 144), niveau 2, regard haut. Cela évite B[6], qui n'est vrai que sur la tuile d'arrivée de la 185.
- `FrameLimit` environ 3000.

**Séquence.**
- Deux images ; `ActiveCollisionEntity = EntityByRecord(4)` ; boîtes fermées au bouton.
- Départ au sol d'Olen jusqu'à `0x8B`.
- L'arc s'arrête quand B[4] a posé `0x8E` @172 et ses 4 naissances. Il s'arrête avant T7 (@228), qui lâche les Murutas et l'horloge de 756 ticks.

**Assertions.**

| point | au HEAD | après h3a |
|---|---|---|
| chargement | records 0 à 4 présents, 5 à 10 absents | idem |
| talk | F[3] pc 740, T2 posé | idem |
| `0x8B` @569 | record 5 présent en (Olen.X, Olen.Y, Olen.Z + 32 px), regard `0x10`, anim 0 | idem |
| T4 | écart exact à S+31 (pc 582) | idem |
| vol | anim 2, atterrissage mesuré (et non assumé) | idem |
| T5 → anim 1 | son 46 à A, boîtes de A+16 à A+30 | idem |
| contact | `lid.TouchingEntity == bombe` à H, 3 étincelles (effet 9), `FrameCollisionTickCounter = 25` | idem |
| D[1] | pc 728, 731, 734, 737 à H+1 ; T6 posé ; couvercle en statut 3 | idem |
| couvercle | **manque** : statut 3 maintenu N ticks, toujours dans `SpawnedEntities`, aucun son `0xE6`, aucun effet 11 ; épinglé par un test nommé | statut 4 à H+2 puis absent à H+3 ; son `0xE6` ; effet 11 attaché |
| bombe | **manque** : statut 3 à la fin de l'anim 1, toujours présente | `DestroyEntity(e,-1)` à A+52, libre à A+53 |
| B[4] | `0x8E` @172 à H+2, `0x8F` @179, naissances @182, 192, 202, 212 | idem |
| hors de la zone | héros intact (`TouchingEntity` nul, PV 10) | idem |

**Témoins négatifs possibles** (sans le moindre code de production).
- Sans G248, pas d'enregistrement 5 (dialogue 131, retour à la boucle).
- Un coup d'épée sur le couvercle ne pose pas `TouchingEntity` (réponse `0x80` au type 1).

**Mutations utiles pour le verifier frais** (style c1b-2, le HEAD change déjà ces lignes).
- `TypeNibbles[3]` de 4 à 5 : le couvercle ne réagit plus.
- `0x19` supprimé.
- `IsInlineDeactivateCode(19)` vrai : le couvercle serait détruit sans son.

### 1.5 Périmètre probable et fichiers

- Aucun fichier de production.
- Nouveau fichier de test : `Alundra.Tests/Arcs/BombLidArcTests.cs`, éventuellement avec une aide de spec ; nom à contrôler par `tools/dll-structure/check_names.py`.
- Annexe : `docs/plan-e20-annexe/e20-a362/` (table du bytecode, chronologie, mesures, les scripts de cette lecture).
- Plan : ligne 9b, un paragraphe « Résultats ».
- Pas d'ADR : c1b-2 n'en avait pas.
- Option d'un préréglage `olen-m362` : `TestSaves.cs`, `docs/test-saves.md`, `TestSavesTests.cs`. Mêmes fichiers que b2 (`legende-m14`) et c2b (`blessure-m15` et `blessure-m14`). Ordre à tenir, changement mécanique du test de liste (D-E0D-12).
- Budget : un `executor` (deux tentatives), un `plan-verifier` avant, un verifier frais après.
- Ordre : A362 est indépendante de e0d, b2, c2b, e0k5, f6b, h0a. Elle peut passer dès maintenant, avant h3a.

### 1.6 Tests existants qui bougeraient

- Aucun pour l'arc lui-même (B8).
- À h3a (phase B), le test d'arc d'A362 se réécrit (déjà prévu au plan).

### 1.7 Risques

- **R1.** La 362 n'a jamais été chargée par `ArcRun` ; commencer par une fumée (style L0 de c1b-2).
- **R2.** L'atterrissage de la bombe dans la DLL (B9) peut manquer le couvercle. Ce serait un « manque autre » et un arrêt de h3a (déjà prévu au plan).
- **R3.** Dialogues 132 et 133 : assets à injecter par `ArcRun`.
- **R4.** Après T7, les 4 Murutas attaquent. Avec c2b, ils blessent le héros ; l'arc doit s'arrêter avant T7 (ou garder le héros hors de portée).
- **R5.** Conventions de tick (S, A, H) à fixer en T0, avant d'écrire des valeurs.
- **R6.** `ActiveCollisionEntity` est une couture de test, pas le chemin de contact de production.

## 2. Révision de h3a (et de h3b qui le suit)

### 2.1 D-H-7 est faux, mesuré

**C1 [MESURÉ]** Scénario de `scen_bomb1.py` (`bomb_timeline.txt` : Circle au tick 1, bombe portée puis lâchée sous les pieds du héros), avec l'octet de réponse au type 4 de `0x80127008` (`+2+4` = `0x8012700E`) reposé à `0x80` après chaque tick.

| | PV du héros | état | position |
|---|---|---|---|
| sans réglage | 10, puis 5 au tick 261 | `0x31` au 261, `0x1C` au 265, `0x00` au 276 | x = 133,09 |
| avec D-H-7 | **10 en permanence** | **identique** | **identique** |

- Le contact `+0x224` est bien posé au 260 dans les deux cas.
- Cause : la passe lit l'immunité sur la fiche d'équilibre du héros (`+0x1C4`), pas sur `0x80127008` (c2b, critique C2).
- Sorties : `out/k_dh7_check.txt`, `out/k_dh7_trace.txt`.
- Au passage, `0x8012700E` vaut 0 au démarrage et 0x10 dès les premiers ticks : le réglage n'a de sens que reposé à chaque tick.

**C2 [MESURÉ]** Le bon niveau, si un héros immune devait un jour être refait dans l'oracle.
- Octet `+6` de la fiche du héros (`+0x1C4`, record `0x801441F8`) posé à `0x80`.
- Alors le contact n'est pas posé, PV 10, pas de `0x31`, x inchangé (`out/k_dh7_prime.txt`).
- C'est l'équivalent exact du précédent de la DLL, `HitPassTests.H6` (le `BalanceRecord` de la cible remplacé).
- Inutile si c2b passe d'abord. Repli seulement si l'ordre changeait.

### 2.2 Les clés, une par une

Le héros de l'oracle est en (100,100) dans le champ plat.

| clé | script / fichier | où est le héros | sort du héros | verdict |
|---|---|---|---|---|
| h3a (w1) | `scen_bomb1.py` / `bomb_timeline.txt` | porte la bombe 243 ticks, qui retombe à ses pieds | touché à T+260 (PV 10→5, `0x31` à 261, recul de 33 px) | les cinq chiffres relatifs ne dépendent pas du héros ; refaire la clé « bombe posée, héros loin » (C5) |
| h3a (w2) | `scen_zone.py` / `explosion_zone_xy.txt` | (100,100), bombe en (300,300) | jamais concerné | inchangée ; placer le héros à plus de 100 px dans la DLL |
| h3a (w3) | `sc_misc.py chain` / `bomb_chain.txt` | 35,24 px de la bombe 1 | jamais touché (PV 10), mais 0,74 px en dehors de 34,5 | refaire en bombes posées, héros loin (C6) |
| h3a (w4) | `sc_misc.py grass` | même géométrie que (w3) | idem | héros loin dans la clé `grass_placed_pf` à produire |
| h3a (w5) | A362 / `k_w5_lid.txt` | promené, loin | indemne | aucun souci |
| h3b (w1) | `bomb_timeline.txt` | idem (w1) | blessé à T+260 | l'oracle non modifié est la cible telle quelle |
| h3b (w2) | `bomb_limit3.txt` | immobile en x = 100, bombes lancées à l'est | jamais blessé sur 640 ticks (`out/k_limit3_hero.txt`) | inchangée |
| h3b (w3) | `throw_dirs.txt` | atterrissages seuls | non concerné | voir C9 |
| h3b (w4)-(w6) | `bomb_wall`, `bomb_wall2`, `water`, `bomb_air_throw`, `bomb_locked` | à 41 px, ou pas d'explosion | jamais blessé | inchangées |
| h3b (w8) | `scen_hurt_circle.py` | blessé à 261 ; Circle à 262 coupe le recul (x reste 103,00) | PV 5 | la clé non modifiée prolongée jusqu'à la fin de la seconde bombe (C7) |

**C3 [MESURÉ]** Rejeu depuis ma copie : 16 clés stockées, toutes identiques bit à bit à leurs fichiers de l'annexe.
- `bomb_timeline`, `hero_hit`, `hurt_circle`, `bomb_limit3`, `explosion_zone_xy` ;
- `throw_dirs`, `water`, `grass` côté critique ;
- `bomb_chain`, `bomb_grass`, `bomb_fuse`, `bomb_limit3` côté `bin` ;
- `bomb_wall`, `bomb_wall2`, `bomb_locked`, `bomb_air_throw`, `bomb_hurt_while_carrying_bomb`.
- Dans `sc_misc.py chain` et `grass`, le héros reste à PV 10.

**C4 [FAIT]** Le plan emploie D-H-7 à :
- `docs/plan-e20-heros.md:10474` (définition) ;
- `:10603` (« toutes les clés de (c) suivent D-H-7 ») ;
- `:10671` et `:10672` (clés de h3b, dont la clé de (w8)).

### 2.3 Propositions pour refaire les clés

**C5 [MESURÉ]** (w1), héros loin. Bombe née posée par la vraie `SpawnWarpEntity` en (300,300), statut 3 posé après le tick K ; héros à (100,100). Sortie : `out/k_w1_away.txt`.
- Statut 3 à K.
- Anim 1 et son 46 à K+1.
- Boîtes de K+17 à K+31, avec les changements de taille à +17, +20, +24, +26, +32.
- Drapeau de fin à K+52, statut 4 à K+53, case libre à K+54.
- Héros intact sur toute la clé.
- **Les chiffres du plan sont donc inchangés.**

**C6 [MESURÉ]** (w3), deux bombes posées 11 px d'écart, héros loin. Sortie : `out/k_w3_chain_away.txt`.
- La bombe 1 touche la 2 à K+17 (`HitCounter` = 1).
- La bombe 2 passe en statut 3 à K+18.
- Anim 1 et son 46 de la bombe 2 à K+19.
- Bombe 1 libre à K+54, bombe 2 libre à K+72.

**C7 [MESURÉ]** (w6, neuve) : le héros dans la zone, fiche non modifiée. Bombe posée 17 px à l'est du héros, statut 3 à K. Sortie : `out/k_w1_17east.txt`.
- Contact `+0x224` à K+17 ; trois étincelles (effet 9) ; `+0x1D0` = 25 à K+17, redescend à 0 en K+42.
- À K+18 : PV 10→5, état `0x31`, son 23, `+0x1CC` = 119. Il est donc invulnérable jusqu'à environ K+137.
- `0x31` de K+18 à K+21, `0x1C` de K+22 à K+32, repos à K+33.
- x : 100→97 à K+18, recul total −33,09375 px (66,91 à la fin), vers l'ouest puisque la bombe est à l'est.
- Bombe sur le héros (0 px) : mêmes ticks, recul vers l'est (`out/k_w1_onhero.txt`).
- Ces chiffres sont le cas A1 de c2b : contact → blessure au tick suivant.
- À vérifier en T0 : l'armure du banc. Les arcs à `Arrival` démarrent sans inventaire. L'oracle est sans armure (réponse 16 par défaut), d'où 5 PV.

**C8 [MESURÉ]** (w5), couvercle seul. Couvercle (global 243) né par la vraie `SpawnWarpEntity`, statut 3 posé après K comme le fait `0x19`. Sortie : `out/k_w5_lid.txt`.
- À K+1 : son 230 (= 0xE6) et effet 11 attaché au couvercle (appel `CreateAttachedEffectCore`), statut 4.
- Case libre à K+2.

**C9 [MESURÉ]** Trouvaille pour h3b : le lancer vers le bas blesse le héros resté sur place. Sortie : `out/k_throw_explode.txt`.
- La bombe lancée vers le bas se pose à +23,50 px ; la zone de 32 de profondeur recouvre le corps du héros (15 de profondeur) de 0,5 px.
- Il est blessé à t273 (PV 5, `0x31`).
- Vers la droite, la gauche et le haut, il passe à ±0,25 px (droite, gauche) ou 0,5 px (haut) près et n'est pas blessé.
- Les keys déjà stockées s'arrêtent avant l'explosion (`throw_dirs`) ou ne lancent que vers l'est : aucune n'a révélé ce cas.
- Risque : une marge de 0,25 px est un risque pour h3b si la DLL arrondit un cheveu autrement ; avec c2b, le héros se blesserait.

**C10 [MESURÉ]** (w8). `out/k_w8_explode_in_hands.txt`, sans réglage D-H-7, jusqu'à la fin de la seconde bombe.
- t261 : PV 5, `0x31`, x = 103, son 23.
- t262 : Circle coupe le recul (état 5, x reste 103) ; seconde bombe née dans la boîte (son 44).
- t264 : D30 la passe en statut 3. t265 : anim 1, son 46.
- t316 : drapeau de fin. t317 : statut 4, le lien (`H+0x2C`) est effacé. t318 : case libre, objet libéré.
- La seconde explosion ne blesse pas : le héros est protégé par `+0x1CC` (119 → 0 vers t380).

**Autre clé disponible** (non citée au plan) : `bomb_hurt_while_carrying_bomb.txt` (rejoué à l'identique). Le héros est blessé à t264 en portant la bombe 2, laquelle est lâchée au tick suivant (anim 2), part sur 35 px et n'est pas brisée. C'est une candidate de (w10) pour h3b, d'autant que c2b dit que l'objet porté « se brise ».

### 2.4 Texte du plan à corriger

1. D-H-7 : marquer caduque (D-C2B-11) et renvoyer à C1 ; supprimer ses trois emplois (`:10603`, `:10671`, `:10672`).
2. h3a, en-tête et Prérequis : ajouter c2b (D-E20-83) et ôter « tant qu'E20.c2b manque ».
3. h3a, Hors périmètre : « la réaction du héros à la bombe (E20.c2b) » devient « épinglée seulement par (w6) ».
4. T0 (c) : (w1) est refaite en bombe posée (C5) ; ajouter (w6) (C7) ; (w2), (w3), (w4) avec le héros loin, garder le critère de (w3) (C6).
5. T0 (b) : remplacer « 2 épingles » par le recomptage (D4).
6. h3b T0 (b) et (w8) : plus de « selon D-H-7 ».
   - Sa clé de (w8) est `hurt_circle.txt` prolongée (C10).
   - `bomb_timeline.txt` à T+260 reste tel quel.
7. Q-H3-2 et Q-H3-1 : le coût « tant qu'E20.c2b manque » tombe.

### 2.5 Tests existants qui bougeraient (recensement HEAD)

**D1.** Aucun test ne code D-H-7 (réglage jamais posé). Sa suppression ne déplace rien.

**D2.** Si c2b est fait avant h3a, tout banc de h3a où une bombe explose près du héros blesse le héros. D'où l'écartement ou (w6).

**D3.** Liste de T0 (d) à recompter au HEAD de départ, hors du périmètre de cette lecture.

**D4.** Épingles du registre : quatre au HEAD, pas deux.
- `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` ;
- `Alundra.Tests/Ai/Objects/LiftableAiRegistrationTests.cs:77` (avec `sum.Count == production.Count` à `:76`) ;
- `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs:239` (avec `:238`) ;
- `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185`.
- Elles montent de +3 clés à la phase B de h3a (`(D,30)`, `(E,12)`, `(E,19)`) ; les deux listes `sum` doivent ajouter les deux nouveaux `Register`.

**D5.** `LogicalZWorldTests.cs:272` (Z4) et `:397` (Z7) : bombe née par l'interface, sans héros dans la zone. Neutre pour h3a, à revoir pour h3b.

**D6.** Recensement de `(E, 19)` : `out/scan_e19.txt`.
- 23 enregistrements d'en-tête avec E = 19 : les 3 couvercles globaux (241 effet 11, 242 effet 11, 243 effet 11) et 20 sprites de page de carte (58 ou 59, effet de bris 10, drapeau Collidable seul) sur 19 cartes (2, 7, 17, 33, 47, 62, 67, 74, 97, 107, 113, 119, 120, 122, 126, 158, 159, 426, 439).
- Aucune de ces cartes n'est dans la chaîne de 30 cartes. L'arrêt de h3a « E19 sert un sprite de la chaîne hors des couvercles » ne se déclenche pas.
- [INFÉRENCE] Ce sont les murs et blocs cassables par un programme de carte : prefab non vérifié.

### 2.6 Risques propres à la révision

- Marge d'un demi-pixel (C9) et marge de 0,74 px des clés (w3) et (w4) stockées : à éviter en les refaisant avec le héros à plus de 60 px.
- Le héros du banc DLL doit avoir la même fiche que celui de l'oracle (sans armure, sans inventaire) pour retrouver 5 PV.
- Un banc DLL qui place le héros dans la boîte et ne le veut pas blessé n'a plus de repli de type D-H-7 : il faut l'éloigner, ou remplacer sa fiche d'équilibre (C2).

## 3. Questions à l'auteur (en termes de jeu, défaut « comme l'original »)

**Q-A362-1. Le couvercle de la tige-bombe, aujourd'hui.**
- Quand la bombe d'Olen explose dans son rêve, le couvercle doit éclater en pierre avec un bruit, puis les 4 Murutas sortent.
- Ce que je prédis aujourd'hui : le couvercle reste en place après l'explosion, les 4 Murutas sortent quand même, et la bombe d'Olen reste là, invisible.
- Options :
  - (A, défaut, comme le plan) A362 est un arc de test seul et ne change rien au jeu ; le correctif (couvercle qui disparaît, bombe retirée) vient avec h3a.
  - (B) livrer le correctif dans A362 : le couvercle disparaît tout de suite ; coût, A362 n'est plus un arc sans code et h3a perd son premier test.

**Q-A362-2. Voir la scène.**
- La bombe d'Olen n'apparaît qu'après un détour par la carte 363, qui pose un drapeau. Sans lui, Olen répète sa première conversation.
- Options :
  - (A, défaut) une sauvegarde de test `olen-m362` devant Olen avec ce drapeau posé ; coût, une ligne de préréglage et le test de liste, dans les mêmes fichiers que b2 et c2b.
  - (B) rien : Change map plus le drapeau à la main.

**Q-H3B-1. Lancer la bombe vers le bas.**
- Si Alundra lance sa bombe vers le bas (sud) et reste sur place, la zone de 32 px de profondeur l'atteint de 0,5 px : il perd 5 cœurs sur 10 sans armure. Vers la gauche, la droite et le haut, il passe tout juste à côté.
- Options :
  - (A, défaut, comme l'original) garder tel quel ;
  - (B) l'éviter, c'est-à-dire s'écarter du binaire, qui est la vérité ici.

## 4. Fichiers

Base : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/a362-h3a/` (je note `BASE`).

- Lanceur : `BASE/runpy.py`. Il ajoute le site utilisateur de capstone et le dossier du script, car `python -I` retire les deux.
  - Exemple : `python -I BASE/runpy.py BASE/h3a-new/k_w1_posed.py 0 0 5 90`.
- Copies de l'oracle : `BASE/h3critic/` (dont `my_maps2.py`, `my_maps3.py`), `BASE/h3bin/`, `BASE/bx.py`.
- Sources de la DLL lues à HEAD : `BASE/head/`.
- Scripts nouveaux (dans `BASE/h3a-new/`) :
  - `k_dh7_check.py`, `k_dh7_trace.py`, `k_dh7_prime.py` (C1, C2) ;
  - `k_w1_posed.py DX DY [K] [N]` (C5, C7) ;
  - `k_w3_chain.py` (C6) ;
  - `k_w5_lid.py` (C8) ;
  - `k_throw_explode.py` (C9) ;
  - `k_limit3_hero.py` ;
  - `k_w8_explode_in_hands.py` (C10).
- Recensements : `BASE/scan_flag.py`, `BASE/scan_e19.py`, `BASE/scan_changemap.py`, `BASE/scan_opcodes_362.py`.
- Sorties : `BASE/out/`.
  - `k_dh7_check.txt`, `k_dh7_trace.txt`, `k_dh7_prime.txt` ;
  - `k_w1_away.txt`, `k_w1_17east.txt`, `k_w1_onhero.txt`, `k_w3_chain_away.txt`, `k_w5_lid.txt`, `k_w8_explode_in_hands.txt` ;
  - `k_throw_explode.txt`, `k_limit3_hero.txt` ;
  - les 16 rejeux `*.replay.txt` ;
  - `map362_programs.txt`, `scan_opcodes_362.txt`, `scan_e19.txt`, `scan_flag248_setters.txt`, `scan_changemap_362.txt`.
- Empreintes : `BASE/MANIFEST.sha1.txt`.