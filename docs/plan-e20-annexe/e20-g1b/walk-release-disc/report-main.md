# Rapport walk-release-disc : lâcher de la marche (W1 à W5)

Lecture seule respectée. Je n'ai rien écrit sous `D:/development/repo/alundra-casaengine-project-converter`, et je n'ai lancé ni build ni test. Côté git, seulement `status`, `log` et `diff` (lecture). La machine d'oracle MIPS (`ALUN_CD.EXE` France) a été rejouée depuis des copies. `git status` est identique au début : `m CasaEngineMonogame`, `m alundra-datas-analyser`, plus les fichiers non suivis `tools/…` qui existaient déjà.

Légende : FAIT = lu dans le code ou les données. MESURÉ = rejoué sur la machine d'oracle, ou valeur consignée dans l'annexe de g1b. INFÉRENCE = déduit, pas mesuré.

## 0. Synthèse

**Résultat central.** Le « demi-pas au lâcher » n'est pas un défaut de la DLL de production. L'écart constaté par le T0 de g1b (« la DLL s'arrête net ») vient du montage de test, pas du jeu.

- Le montage `JumpHeroRig` / `CarryRig` donne à l'animation Idle l'octet d'accélération 0.
- La vraie fiche du héros, identique au binaire sur les 94 animations (MESURÉ, 0 écart), donne 1 à l'Idle.
- Avec 1, la DLL fait le même calcul que le binaire. Un modèle transcrit de la DLL égale l'oracle sur 9 scénarios de lâcher.
- Avec 0, ce même modèle redonne exactement les valeurs des sondes de g1b : x reste à 120,7188, force 0 au tick 10.

**W1.** Il n'existe pas de gestionnaire de lâcher dans le binaire. Le demi-pas est l'accélération générique de la physique, appliquée avec l'animation Idle (Speed 0, octet d'accélération 1).
- Fonction : `UpdateEntityPhysics` à `0x80036614`, plus `IncrementForce` à `0x800367E4`.
- Règle : `Step = |Cible − Force| >> (octet & 0xF)`, recalculé seulement quand la vitesse, la direction ou l'accélération changent.
- Effet : la force passe de F à F/2 puis 0. Le pas du tick de lâcher est déplacé dans le même tick.
- Cette règle ne dépend pas de la marche seule : elle vaut pour tout état, selon l'animation vers laquelle on passe (tableau au §2).

**W2.**
- La DLL ne met la force à zéro nulle part au lâcher. Elle suit la formule du binaire (`ScriptedMotion.PlanForce`).
- Le moteur (`CharacterControllerComponent`) ne reçoit que le pas final : rien à changer côté moteur.
- Le seul « correctif » est une ligne de montage de test : Idle avec accélération 1.
- Aucune décision du plan n'accepte un arrêt net en production. Une note du plan (§E20.d1) et le commentaire de `RunMontage.cs` reconnaissent seulement que le montage garde 0.

**W3.**
- Aucun test de production ne change, puisque rien ne change en production.
- Les traces `hero-trace-389-*` ne contiennent aucun lâcher : le pad y est toujours Gauche ou Droite.
- Les arcs et montages qui épinglent déjà un arrêt utilisent la vraie fiche (ou Idle corrigé à 1), donc contiennent déjà le demi-pas.
- Aligner le montage Idle à 1 ne devrait déplacer aucun test existant (0 à 2 au pire, INFÉRENCE ; détail au §4).

**W4.** O-G3-3 n'a pas la même cause que le demi-pas.
- Les forces et les positions de la DLL égalent celles du binaire aux ticks 1 et 2.
- Le décalage vient de la règle du contact `+0x130` quand le héros n'est pas dans la branche « marche au sol ».
- Je l'ai reproduit au binaire avec une force déjà stable depuis 20 ticks : même retard d'un tick.

**W5.**
- Aujourd'hui en jeu, le demi-pas existe déjà. Lâcher puis Carré au tick suivant soulèvera comme l'original, sans correctif.
- Le seul écart visible restant est O-G3-3 : un Carré pressé à 1/50 s près, depuis le dessus d'une caisse.
- Coût du montage : 1 à 2 lignes de test, 0 ligne de production.
- Coût d'O-G3-3 : 1 écriture dans `EntityScriptProxy.cs`, 1 ADR amendé, quelques tests.

## 1. W1 : le binaire

### 1.1 Mécanisme (FAIT, listings `docs/plan-e20-annexe/e20-g3/bin/lst/phys_36400_38000.txt` et `phys_37f28_38e18.txt`)

Ordre du tick :
- `MovePlayer` (passe d'événements `0x800386D0`) écrit `TargetAnimation` (+0x88) = Idle.
- La passe d'animation (`0x80038E18` → `UpdateAnimation` `0x80038AB4`) voit +0x88 ≠ +0x90. Elle réécrit le pointeur du set d'animation (+0x9C) en `0x80038B34`, ainsi que +0xB4 et +0xF8.
- La passe de physique (`0x80036828`, branche héros) lit déjà le set d'Idle.

Dans la branche héros :
- `jal 0x80036614` à `0x8003694C`.
- Réglages de glace (`+0x180 & 0x20` : pas × 1/16) et d'eau (`& 0x08` sans bottes : cible × 1/2) de `0x80036954` à `0x80036A54`.
- `jal 0x800367E4` pour X à `0x80036A5C` et pour Y à `0x80036A70`.
- Puis `ApplyEntityForces` (`0x800366FC`, via `0x80036B9C`) : `Final = Force + table de pente + PréviousAdjusted`.

`UpdateEntityPhysics`, de `0x80036614` à `0x800366F8` :
- Elle lit Speed du set (hu @+8) contre +0xF4, la direction cible +0x8C contre +0x94, et l'octet @+0xD & 0xF contre +0xF0.
- Si un des trois diffère, elle met en cache Speed, Direction et Acc.
- Elle pose `Target(+0xBC/+0xC0) = tableX/Y[dir] × Speed` (tables `0x80023654` et `0x80023694`).
- Elle pose `Step(+0xD4/+0xD8) = |Target − Force(+0xC4/+0xC8)| >> Acc` (`srav` en `0x800366D4` et `0x800366EC`).
- Sinon elle ne fait rien : le Step en cache reste.
- Puis `Force = IncrementForce(Force, Target, Step)` : `Force ± Step`, bornée à `Target`.

### 1.2 Le set d'Idle (MESURÉ, `out/w2_catalog_vs_binary.txt`)

- Dans la RAM de l'oracle, la table des sets du héros est en `0x801A0020` + 14 octets par animation.
- Anim 0 (Idle) : Speed 0, octet 0xD = 1. Anim 1 (marche) : Speed 208, octet 1.
- La comparaison avec `alundra-project/Data/sprite-records.json` (fiche `192c2eeb-…`, ce que lit la DLL) donne 94 animations comparées, **0 écart**.

### 1.3 Valeurs par tick (MESURÉ, `out/w1_trace.txt`, `out/w1_states.txt`, `out/oracle_release.txt`)

Marche vers l'est, lâcher au tick 10. Dernier tick tenu : t9.

| Tick | Animation | Speed / Acc | Step | ForceX | x |
|---|---|---|---|---|---|
| t09 | 1 | 208 / 1 | 1,2188 | 2,4375 | 120,7188 |
| t10 | 0 | 0 / 1 | 1,2188 | 1,2188 | 121,9375 |
| t11 | 0 | 0 / 1 | 1,2188 (cache) | 0 | 121,9375 |

Les autres cas rejoués, tous au binaire :

| Cas | Observation |
|---|---|
| Ouest, nord, sud | Même schéma. En nord et sud la force vaut 1,625 puis 0,8125 puis 0. |
| Diagonale bas-droite | Par axe, 1,7234 puis 0,8617 puis 0. |
| Tap d'un tick | 1,2188 puis 0,6094 puis 0 : 1,8281 px au total. |
| Tap de deux ticks | 4,875 px au total. |
| Inversion de direction sans lâcher | Pas d'Idle : Step = 2,4375, la force passe 2,4375 puis 0 puis −2,4375. |
| Lâcher un tick puis ré-enfoncer | Le Step est recalculé à 0,6094 : 1,2188 puis 1,8281 puis 2,4375. |
| Poussée contre une caisse (x=130) | Au tick 10 : `fx` 1,2188, `adj=1`, contact caisse. Au tick 11 : force 0, contact nul. |
| Mur de terrain | Même chose : demi-pas refusé au lâcher, `adj=1`. |
| Porté (objet en mains) | Marche d'anim 7 (208 / Acc 0) : pleine vitesse dès le 1er tick, pas de rampe. Au lâcher, anim 12 (0 / 1) : 2,4375 puis 1,2188 puis 0. |
| Course | Voir ci-dessous. |
| Saut | Lâcher en l'air (anim 44 → 45) : 2,2969 puis 1,1484 puis 0. |
| Pente | Voir ci-dessous. |

Course (Triangle + Droite tenus 40 ticks) :
- Préparation `0x29` (0 / 0) aux ticks 1-25.
- Course `0x03` (336 / 2) à partir du tick 26.
- Au lâcher, glissade `0x2A` (0 / 5) : Step 0,1230, soit 16 ticks.
- Idle au tick 57 : 1,9688 → 0,9844. Zéro au tick 58.
- Cela égale `HeroRunTests.cs:316` (64512) et le plan, `docs/plan-e20-heros.md:2741`.

Pente (cellule d'octet de pente 100, soit TileAttributes 4) :
- La table de pente ajoute −0,9375 px à chaque pas final.
- La force de marche décroît pareil : 2,4375 → 1,2188 → 0.
- Le héros glisse ensuite indépendamment.

Nage : non rejouée. FAIT cité de `docs/plan-e20-annexe/e20-i/swim-oracle.md:35-39` : `0x1D` (0 / 3) décélère de |F|>>3, soit 8 ticks.

### 1.4 Table par état (FAIT, `sprite-records.json`, identique au binaire)

| État | Anim | Speed / Acc & 0xF | Effet au lâcher ou à l'entrée |
|---|---|---|---|
| Idle | 0 | 0 / 1 | Demi-pas puis 0 (2 ticks). |
| Marche | 1 | 208 / 1 | Rampe à l'entrée : 1,2188 puis 2,4375. |
| Saut en marche | 2 | 208 / 1 | — |
| Course | 3 | 336 / 2 | — |
| Charge | 4 | 0 / 6 | — |
| Marche avec objet | 6 et 7 | 208 / 0 | Pas de rampe. |
| Objet tenu | 12 | 0 / 1 | Demi-pas au lâcher. |
| Nage | 15 | 144 / 4 | — |
| Flotter | 29 | 0 / 3 | Décélération sur 8 ticks. |
| Glissade de course | 42 | 0 / 5 | 16 ticks. |
| Saut en l'air | 44 et 45 | 196 / 1 et 0 / 1 | Demi-pas en l'air. |

## 2. W2 : la DLL

### 2.1 Où est la force (FAIT)

| Élément | Emplacement |
|---|---|
| Entrée du tick du héros | `Alundra/Physics/ScriptedMotion.cs:88-92`, `TickPlayer` appelle `RunOneMotionTick(player, player.TargetAnimationId)`. |
| Tick cinématique | `RunOneKinematicTick :635`, qui appelle `PlanForce :714` (cible, pas, glace, eau) puis `IncrementForce :1010`. |
| Pas final vers le moteur | `MoveControllerAndPullPosition(FinalForceX/65536f, …)` à `:681`, défini dans `EntityScriptProxy.cs:2642`. |

Il n'y a aucune mise à zéro de `ForceX` / `ForceY` dans `PlayerManager.cs`. Les seules écritures de `ForceX = 0` sont dans :
- le reset de l'entité,
- l'opcode `0x10` (`EventProgramRunner.cs:226`),
- l'IA ennemie (`Alundra/Ai/EnemyHelpers.cs`).

La force n'atteint 0 que si l'accélération de l'Idle vaut 0.

### 2.2 D'où vient le « zéro » des sondes de g1b (FAIT)

- `Alundra.Tests/Support/JumpHeroRig.cs:133` : `[0] = new AnimSetEntry { Anim = 0, Speed = 0 }`, donc Acc 0.
- `Alundra.Tests/Support/JumpNpcRig.cs:225` : la table `HeroAttackClock` (`new(0x00, 0, 0, 0, -1, 50)`) écrase l'Idle avec Acc 0 pour tout montage `attackClock: true`.
- `CarryRig.cs:188` est un tel montage, donc `CarryRig` a Idle à 0.
- La production applique la vraie fiche : `WorldProxy.cs:1999-2013`, `ApplyHeroSpriteHeader` → `proxy.AnimSetsByAnim = header.AnimSets`.

### 2.3 Preuve de l'équivalence (MESURÉ puis INFÉRENCE)

Le modèle `critic/dll_model.py` transcrit `PlanForce` et `IncrementForce`, nourri des deux jeux de données.
- Avec l'Idle **réel** (Acc 1), il égale la force X et Y, et la position, de l'oracle, tick par tick, sur 9 scénarios. Sortie : `ALL oracle == REAL model: True`, voir `out/dll_model.txt`.
- Avec l'Idle **du montage** (Acc 0), il diffère aux ticks 10 à 13 et redonne exactement `probes/dll-release-probe.txt` (x 120,7188 et `fx 0,0000` au tick 10).
- Ce modèle est une transcription, pas une exécution de la DLL : INFÉRENCE forte.

Preuves indirectes existantes, qui épinglent l'Idle à 1 contre des traces du binaire :
- `HeroRunTests.cs:316` : 64512 au tick 57, via `RunMontage.cs:64-65` (Idle corrigé à Acc 1).
- `HeroSwimTests.cs:458-466` : l'Idle « halves the force » (−55296).
- `HeroFallAndPadJumpTests.cs:249` (UH3a) : demi-pas en l'air.

### 2.4 Décisions du plan (FAIT)

Aucune décision n'accepte un arrêt net du héros en production. Recherche faite dans `docs/plan-*.md` et `docs/decisions/` sur décélération, friction, lâcher, release, stop, `D-E20-*`, `D-E19-*`, acceleration : aucun résultat sur ce sujet.

Ce qui existe est une dérogation de montage de test :
- `docs/plan-e20-heros.md:2819`, section « E20.d1 — Résultats », paragraphe « Moyens de test » : « l'Idle de la fiche du héros porte l'octet d'accélération 1 … la horloge des coups d'E20.b garde son 0 pour ne rien déplacer, et le montage de la course le corrige pour lui seul ».
- `docs/plan-e20-annexe/e20-i/check.md:37`, point 7.
- `RunMontage.cs:63-65` et `HeroWaterIceTests.cs:62`.

Le `CarryRig` de g1a, construit après, a hérité du 0 sans que l'avertissement le suive.

### 2.5 Le correctif est-il dans la DLL seule ou demande-t-il le moteur ?

Il n'y a rien à corriger côté DLL ni côté moteur. Rien n'est à consigner comme manque du moteur. Le correctif est un réglage de montage de test (§6).

## 3. W3 : recensement statique

### 3.1 Épinglent déjà le demi-pas (aucun changement)

- **Traces** `docs/hero-trace-389-{spawn,highground}-{fixedstep,freestep}.txt`, au nombre de 4. Générateur : `Alundra.Tests/Arcs/HeroTraceHarnessTests.cs`.
  - Il lit la vraie fiche (`:189-273`).
  - Le pad vaut toujours `Left` ou `Right` (`:485-487`). L'animation cible ne passe jamais par 0 (vérifié sur les 4 fichiers). Aucun arrêt n'est donc épinglé.
- **Arcs** sur `ArcRun` (vraie fiche, `ArcRun.cs:249-252`), qui relâchent sans épingler de position d'arrêt :
  - `CrateSolidityMap392Tests.cs:62,104`
  - `ShipCorridorArcTests.cs:210`
  - `LiftMap22ArcTests.cs:141`
  - `PrefabArcSupportTests.cs:216`
  - `CabinWalkTests`, `Sailor12EndOfIntroTests` (vraie fiche).
- **Montages avec Idle corrigé** : `HeroRunTests`, `HeroSwimTests`, `HeroRunArenaTests` (`RunMontage`), `HeroWaterIceTests` (`:62`).
- **Demi-pas en l'air, montage par défaut** (sets 44 et 45 déjà à Acc 1) : `HeroFallAndPadJumpTests.cs:249-286` (UH3a, UH3b).

### 3.2 Montages avec Idle à 0 (ce qu'un alignement toucherait)

- `JumpHeroRig.cs:133`.
- `JumpNpcRig.cs:225` (`HeroAttackClock`), utilisé par :
  - `CarryRig.cs:188`
  - `FlailBounceTests.cs:557,601`
  - `HeroAttackTests.cs:88,571`
  - `HeroChargeTests.cs:136`
  - `WeaponSpawnSeamTests.cs:167`
  - `RunMontage.cs:62` (déjà corrigé).
- Tables propres avec Idle à 0 :
  - `InteractionPassTests.cs:163`
  - `TickStructureTests.cs:109`
  - `PlayerManagerTests.cs` (Idle absent)
  - `CarryThrowWorldTests.cs:190,491,540` (sets de caisses, pas du héros).

### 3.3 Y a-t-il des lâchers sous force sur ces montages ?

Non, d'après une lecture d'environ 35 fichiers (greps ciblés + lecture des tests qui relâchent). Les cas rencontrés :
- `TickStructureTests.cs:214` : `Hold = 0` au repos, force nulle.
- `HeroSlideTests` : un `hold` constant sur toute la série.
- `CarryWalkAndJumpTests` et `CarryTopsAndPlatformTests` : `Right` tenu jusqu'au bout.

Balayage des traces d'oracle rejouées par ces montages (MESURÉ, `out/scan_traces.txt`, `out/scan_traces2.txt`) : un changement vers l'Idle sous force, c'est-à-dire sans effet pour l'Idle à 0.

| Famille | Traces scannées | Avec Idle sous force |
|---|---|---|
| Traces de coups E20.b (`HeroAttackTests`) | 43 | 0 |
| Sondes de fléau E20.f2 (`FlailBounceTests`) | 10 | 0 |
| Autres traces plates | 10 | 0 |
| Traces de coup `hit` et clés h4e1 | 68 | 2 + 14 |

Les 16 hits restants sont tous du même type : la blessure `0x1C` → Idle, force 61440 → 30720.
- Les 2 traces `hit` concernées (`H1-both`, `H3`) sont rejouées par `HitPassTests` (`:278-312`). Ces tests coupent avant la blessure (`ignore` aux ticks 18 et 2), et `HitArena` tourne sur `ArcRun` (vraie fiche).
- Les 14 des clés h4e1 sont des fichiers de découverte, pas des entrées de test.

### 3.4 Estimation (INFÉRENCE)

- Alignement du `CarryRig` seul : **0** test existant déplacé. Les tests g1b neufs (m1 à m8) se construisent directement juste.
- Alignement des deux tables : **0 à 2**.
- Option « corriger la DLL » : 0 test, puisque 0 ligne de production.
- La mesure viendra d'un lancement à faire quand le checkout sera libre.

## 4. W4 : O-G3-3 et le demi-pas

### 4.1 Rejeu de R7 (MESURÉ, `out/w4_r7.txt`, `out/og33_square_replay.txt`)

Le script `og33_square.py` rejoué depuis une copie égale `e20-g1b/t0/out/og33-square.txt`. Détail par tick avec les forces :

| Tick | Binaire : y / ForceY / contact / adj | DLL (`probes/dll-probe.txt` §b3) : y / contact / adj |
|---|---|---|
| t37 | 296,8125 / 0,8125 / nul / 0 | 296,8125 / nul / 0 |
| t38 | 297,0000 / 1,625 (pas final) / **nul** / 0 | 297,0000 / **rec10** / 0 |
| t39 | 297,0000 / 1,625 / rec10 / 1 | 297,0000 / rec10 / 1 |

La position et `adj` sont égaux. Le contact est en avance d'un tick dans la DLL, au tick 38.

### 4.2 Ce n'est pas la même cause (MESURÉ, `critic/w4_steady.py`, `out/w4_steady.txt`)

La force de R7 est déjà stable (1,625) au tick d'arrivée. La rampe de départ n'explique donc rien. J'ai monté une scène à moi sur le vrai code du binaire :
- Une rangée de 3 caisses.
- Une pile de 2 caisses au bout.
- Le héros au sommet (z=16) marche vers l'est.

Résultats :
- Départ à 50 px, force 2,4375 stable depuis ~20 ticks : le pas est raccourci à x=255,0 au **tick 27** avec contact nul. Le contact (`Bup`) n'apparaît qu'au **tick 28**, `adj` 1 au tick 29.
- Départ à 4 px de la face : même retard d'un tick (flush au tick 8, contact au tick 9).
- **Contrôle au sol**, vers la caisse basse : flush **et** contact au même tick (t20), `adj` 1 au tick 21. Cela retrouve la règle de g3 (160 sur 160).

Le retard dépend donc de l'endroit où se tient le héros, pas de la force.

### 4.3 Mécanisme au binaire (FAIT, listing `ComputeXYPosition` `0x80037730`)

Condition de la branche « marche au sol », de `0x80037834` à `0x80037884` : flag de gravité `+0x6C & 0x100`, `ForceZ (+0xB8) == 0`, et |terrain − (PosZ+ModZ) − 1| < `0x30003` (environ 3 px).

- **Dans la branche sol** : les tentatives sans candidat sautent en `0x8003791C` sans écrire `s4`. Le candidat reste collé d'une tentative à l'autre : contact au tick qui raccourcit le pas.
- **Hors branche** (debout sur une caisse, ou en l'air) : `0x80037908-0x80037914` fait `jal 0x80036f34 ; move s4,v0` à chaque tentative. `s4` est le candidat de la dernière tentative. Un pas raccourci finit sur une tentative acceptée, donc `s4` vaut 0 et le contact est nul à ce tick. Le tick suivant, le pas entier est refusé, donc le contact apparaît.

Cela confirme R1 du rapport critique de g3 (`critic/report.md:91-100`), qui donnait la partie « debout sur une caisse » en INFÉRENCE.

### 4.4 Côté DLL (FAIT)

`EntityScriptProxy.MoveControllerAndPullPosition`, `Alundra/Entities/EntityScriptProxy.cs:2654-2659` : `XCollisionEntity = (H2Obstacle ?? H1Obstacle)` à chaque pas, même si le pas progresse.

Le correctif d'O-G3-3 est donc **dans la DLL** (même fichier, `EntityScriptProxy.cs`), sans moteur :
- Hors branche sol, laisser le contact nul si le pas a progressé.
- Le moteur fournit déjà `LastContact` et le déplacement réel.
- La règle du binaire est générique, valable pour toute entité à gravité.
- Le périmètre de g1b (`docs/plan-e20-heros.md:7202`) interdit de toucher `EntityScriptProxy.cs` : il faudrait l'amender.

### 4.5 Observation latérale (MESURÉ, `out/w4_adj_sweep.txt`)

- Dans la scène c3 (héros partant de x=100,0 vers la caisse à 130), le binaire donne `ForceAdjusted` au tick 7. La sonde DLL donne 1 au tick 6.
- Sur le balayage de la phase sous-pixel (x0 = 100 + k/32), l'écart entre le contact et `adj` vaut 1 tick pour 25 phases sur 32, 2 ticks pour 6, et 0 pour 1. La scène c3 (k=0) est dans le cas à 2 ticks.
- Le plan ne compare que le contact, pas `+0x13C` : hors question, mais à savoir.

## 5. W5 : en termes de jeu

**Sans aucun correctif de la DLL.**
- Quand le joueur lâche la direction, Alundra glisse encore d'environ 1,2 px (est ou ouest), 0,8 px (nord ou sud), puis s'arrête. C'est déjà ce que fait le jeu, comme l'original.
- Après g1b, pousser une caisse, lâcher la direction et presser Carré au tick suivant soulève la caisse, comme l'original. Les lignes B, D et D2 devraient donc passer sans toucher au jeu (INFÉRENCE, voir ci-dessous).

**Écart réel restant : O-G3-3.**
- Debout sur le dessus d'une caisse (ou en l'air), en marchant contre une caisse voisine plus haute, et Carré pressé exactement au tick de l'arrivée (1/50 s) : l'original donne un coup d'épée, le portage soulèverait la caisse voisine.
- Imperceptible à la main. Impossible au sol, et limité à des piles comme celle de la barricade de la 392.

**Coût.**

| Option | Fichiers | Effet |
|---|---|---|
| Montage Idle à 1, dans `CarryRig` seul | 1 ligne dans `Alundra.Tests/Hero/CarryRig.cs` : `rig.Rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, Acceleration = 1 })` après `JumpHeroRig.Build` | 0 production, 0 test existant déplacé. |
| Montage Idle à 1, global | `JumpHeroRig.cs:133` et `JumpNpcRig.cs:225` | 0 à 2 tests touchés. À mesurer. Risque : les oracles d'attaque qui ont gardé 0 (le plan l'avait écrit, sans que j'aie trouvé de test concerné). |
| Corriger O-G3-3 | `EntityScriptProxy.cs:2654-2659` (production) | 1 test déplacé (`CrateSolidityMap392Tests` S2 gagne le tick) ; ADR-0091 amendé ; plan amendé ; 2 à 3 tests neufs et mutants. Risque principal : tous les movers à gravité partagent la règle (NPC, ennemis). |
| Accepter et consigner O-G3-3 | — | Décision de l'auteur. |

Les tests qui lisent `XCollisionEntity` hors sol (`HeroJumpTests.cs:202-226`, `HeroObjectTopsTests.cs:327,405-410`, `MovementObstacleProbeTests.cs:446-457`) paraissent sans effet à la lecture (INFÉRENCE, à relire au moment du T).

## 6. Conséquences pour le plan

- **Q-G1B-1 est probablement caduque.** L'option A (« corriger le lâcher dans une tranche à part ») cherche un défaut qui n'existe pas en production. Le texte « la DLL s'arrête net » (`docs/plan-e20-heros.md:7272-7276` et `e20-g1b/README.md:73-77`) devrait être corrigé : le montage s'arrête net, pas le jeu.
- **Vérification décisive, à faire quand le checkout est libre.** Rejouer `G1bT0ProbeTests` et `G1bT0ReleaseProbeTests` (conservés en `.cs.txt` dans `e20-g1b/t0/probes/`), avec `rig.Rig.SetAnimSet(Idle, Acceleration = 1)` juste après `CarryRig.Build`. Attendu :
  - dans le cas ouvert, tick 10 : x=121,9375 et `fx` 1,2188 ;
  - contre la caisse : `fx` 1,2188, contact caisse et `adj=1` au tick 10, force 0 et contact nul au tick 11 ;
  - lignes B, D et D2 égales à l'oracle.
- **Q-G1B-2 (O-G3-3) reste ouverte et indépendante de Q-G1B-1.** La recommandation du plan (« A seulement si (1) est faite, car les deux suivent la force du tick ») repose sur une prémisse fausse : les deux écarts n'ont pas la même cause.

## 7. Limites

- La DLL n'a pas été exécutée (interdiction de build et de test). Les affirmations sur la DLL reposent sur la lecture du code, le modèle transcrit, et les tests existants contre le binaire.
- Nage non rejouée (citée de `e20-i`).
- Pente rejouée seulement avec l'octet de pente 100 (TileAttributes 4), pas avec une rampe cohérente.
- Les familles de traces de charge (E20.f) et les `AttackOracleTrace` hors dossier `e20-b/oracle/out` n'ont pas toutes été balayées.
- Les estimations de tests qui bougent (§3.4, §5) sont des inférences : seule une exécution les confirmera.

## 8. Fichiers

Tout sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/walk-release-disc/` :

**Scripts** (copies de l'oracle sans modification, plus les miens) :
- `critic/w1_trace.py` — états du héros tick par tick pour les lâchers en terrain libre.
- `critic/w1_states.py` — porté, course, mur, saut, pente, nage.
- `critic/w1_slope.py`, `critic/w1_slope2.py` — balayage des octets de cellule pour trouver une pente qui glisse.
- `critic/dll_model.py` — modèle transcrit de la DLL contre l'oracle.
- `critic/w2_catalog_vs_binary.py` — fiche du héros dans la RAM contre `sprite-records.json`.
- `critic/w4_steady.py`, `critic/w4_adj_sweep.py` — contact depuis le dessus d'une caisse à force stable, et décalage de `ForceAdjusted`.
- `g3bin/w4_r7.py` et `g3bin/og33_square.py` — rejeu de R7 avec forces par tick.
- `scan_traces.py`, `scan_traces2.py` — balayage des traces d'oracle.
- `critic/oracle_release.py` — script de lâcher de g1b rejoué.

**Sorties** dans `out/` : `oracle_release.txt`, `w1_trace.txt`, `w1_states.txt`, `w1_slope_scan.txt`, `dll_model.txt`, `w2_catalog_vs_binary.txt`, `w4_r7.txt`, `og33_square_replay.txt`, `w4_steady.txt`, `w4_adj_sweep.txt`, `scan_traces.txt`, `scan_traces2.txt`.

**Fichiers du dépôt cités** :
- `Alundra/Physics/ScriptedMotion.cs`
- `Alundra/Entities/EntityScriptProxy.cs`
- `Alundra/Maps/WorldProxy.cs`
- `Alundra.Tests/Support/JumpHeroRig.cs`
- `Alundra.Tests/Support/JumpNpcRig.cs`
- `Alundra.Tests/Hero/CarryRig.cs`
- `Alundra.Tests/Support/RunMontage.cs`
- `docs/plan-e20-heros.md`
- `docs/plan-e20-annexe/e20-g1b/README.md`
- `docs/plan-e20-annexe/e20-g3/bin/lst/*.txt`