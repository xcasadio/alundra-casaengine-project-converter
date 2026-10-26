# Critique fraîche de la découverte « g2c3 » (gouttes d'eau, A19 et C71)

Lecture seule. Je n'ai écrit ni dans le dépôt, ni lancé de construction ou de test dotnet. Le HEAD a avancé jusqu'à `0d3e7e14`, et les écarts depuis `22ddf0be` ne touchent ni `Alundra/` ni `Alundra.Tests/`. Les `M` de `git status` (`TestSaves.cs`, `TestSavesTests.cs`, `ArcRun.cs`) sont ceux de l'executor.

Le rapport soumis est tronqué en plein §7, à « Q-G2C3-3 — Alundra sans torche. Une goutt… ». La suite (Q3 et suivantes, le défaut éventuel, la liste finale des chemins) est NON VÉRIFIABLE. Le seul texte de méta dans le rapport est l'étiquette du harnais « matched instruction-shaped pattern(s) ». Je l'ai traitée comme une donnée et n'en ai rien exécuté.

**Sources.**
- Binaire : `ALUN_CD.EXE` (France), lu avec `bx.py` et capstone.
- Code : `git show HEAD:` et `git grep HEAD`.
- Données : `data-extracted/data/map_*.json`, `alundra-project/Data/sprite-records.json`, `BALANCE.BIN.json`, les `.tileMap` de `alundra-project/Maps`.
- Rejeux : copie du dossier du découvreur, sorties comparées aux siennes.

## 1. Verdict par fait porteur

### Binaire

| Fait | Verdict | Ce que j'ai vérifié |
|---|---|---|
| F1. A19 (`0x800619D0`, 3 mots) copie l'octet bas de `+0x54` dans `+0x275` | CONFIRMÉ | Désassemblage identique. La table maîtresse `0x80098F4C` donne A `0x800C4B34`, C `0x800C4F34`, E `0x800C5734`. L'entrée A19 est `0x800C4B80` → `0x800619D0` et l'entrée C71 `0x800C5050` → `0x8007B834`. Aucune autre entrée de A ou de C ne pointe vers ces handlers. |
| F2. C71 (89 mots) et ses états 0 à 4 | CONFIRMÉ | Mon désassemblage donne le même graphe que le modèle `c71_full.py`. Détail : le test de `+0xAC` à l'état 4 est « ≠ 0 », alors que celui d'E20 est « == 1 ». |
| F2. Rejeu 6000 vecteurs, 89/89, 89 mutants tués sur 90 | CONFIRMÉ | `out_c71_full.txt` rejoué : sortie identique et sha256 des goldens `40071ab5…6214b` identique. |
| F3. Le store `[0x8013DA20]=1` de C71 est mort | CONFIRMÉ | Balayage large (tout accès à l'immédiat `0xDA20`) : `0x8003E7CC` (opcode 0x40), `0x8004226C` (remise à zéro à l'entrée de `RunScript`), `0x800422FC` et `0x80042310` (test et remise à zéro après chaque opcode), `0x8007B95C` (C71). C71 tourne hors de `RunScript`. |
| F4. DestroyEntity met `porteur.+0x2C = 0` | CONFIRMÉ | Binaire `0x8003A61C-0x8003A62C`. La DLL fait de même (`WorldProxy.cs`, `ClearCarrierOf`). |
| F5. Attente `0x37` : 1er appel suspend, N appels suspendent, le (N+2)ᵉ avance | CONFIRMÉ | `0x8003E3DC` lu. Le marqueur `[a3+8]` est remis à zéro après chaque opcode qui avance (`0x8004232C`), et la DLL remet `Parameters[1]=0` de la même façon. Un redémarrage du script repart donc bien à zéro. |
| F5. `0x40` : `ProgramIndexes[v1]=v2` et fin de programme qui efface l'état | CONFIRMÉ | Lu au binaire et à la DLL (`RunOneScriptCall`, `EndCall`). |
| F6. Chronologie du cycle natif, période 88 | CONFIRMÉ | Rejouée : restaurations aux ticks 89, 177, 265, 353. Atterrissage R+65, état 3 en R+66, `+0xAC` en R+87. Gravité 128 et viscosité 4096 sont identiques sur les 7 cartes, donc le cycle ne dépend pas de la carte. |
| F8. Période N+90 avec script, restauration de `+0x54` | CONFIRMÉ | Même modèle, cohérent avec ma lecture de `Wait` et de `RunScript`. Précisions en §2.4. |
| F9. Test de la torche à l'état 3 (support = héros → objet porté, type `0x189`, statut 3 et `+0x274=1`) | CONFIRMÉ | Désassemblage `0x8007B8E4-0x8007B93C`. Seul site du binaire qui compare à `0x189` (balayage des `addiu/sltiu/ori`). |
| F10 à F12. Torche portée, héros sans torche, torche posée | CONFIRMÉ | Rejeux identiques sur le préfixe stocké. Mes sorties sont plus longues : le découvreur les avait tronquées avant le 2ᵉ cycle. |
| F13. Une torche lancée ou en flamme ne peut pas servir d'appui | CONFIRMÉ | La liste des collidables (`0x800384F4`) exige `flags&0x80`, `+0xB4&0x80==0` et `+0x28==0`. Les drapeaux `(0x3323E2|0x34)&~0x80 = 0x332376` puis `0x332346` n'ont pas 0x80. |
| F14. Fenêtre de contact 24×17 px | CONFIRMÉ | `out_s3_hit_window.txt` identique. |
| F15. La goutte traverse le héros après le contact | CONFIRMÉ | Le rejeu montre `Gravity` conservé et `Collidable` retiré, avec une chute de 31,5 à 0. |
| F16. Course avec le bouton Carré | CONFIRMÉ | Rejeux à 65, 66, 67, 69 et 70 : 65 et 66 sauvent la torche (une flamme, slot de type `0xc`) ; 67 à 70 lancent à vide, sans flamme. |
| F17. Le héros est debout après la destruction | CONFIRMÉ | `CarryRules.CarryGroup` : sans objet, `IdleState` ou `FallState`. Le rejeu donne l'état 0 et 10 PV. La sonde S-D4 reste à faire sur la DLL. |
| F19. 55 programmes C scriptés : 33 + 8 + 14 | CONFIRMÉ | Recensé indépendamment. La distribution des 33 attentes est identique, et 55 + 1 gabarit = 56. |
| F19. Drapeaux temporaires vidés à chaque carte | CONFIRMÉ | `GameState.InstallForMapEntry` : `Array.Clear(TemporaryFlags)`. |
| F20, F21. La restauration rend le script, pas la chute ; les gouttes sont inertes au HEAD | CONFIRMÉ | `RunSpriteEvent` ne trouve rien : non-opération comptée, log unique. |
| F22. Données du sprite de carte 135 = type 391 | CORRIGÉ | Drapeaux, corps, équilibre (PV 0, 11 réponses à 128, `Attacks` nul) exacts, et aucun bit de classe de coup sur la cible. Précisions en §2.3. |
| F23. Recensement par carte | CONFIRMÉ | Gouttes : 6, 13, 7, 9, 7, 14. Torches : 8, 3, 4, 16, 8, 0. Les deux ensembles d'en-têtes (A19 et C71 pour `135`, C70 et E20 pour `137`) ne concernent que ces cartes, y compris dans `sprite-records.json`. |
| F24. Les quatre `0x8A` de B[1] tombent exactement sur les torches 43 à 46 | CONFIRMÉ (la durée est corrigée en §2.1) | Coordonnées `(756|804|852|900, 168)`, Z 240. Torches 46, 45, 44, 43 aux mêmes `(x, y)`, Z 80. Je retrouve les mêmes voisinages pour les autres cartes : 24 px minimum à la 88, 32 px à la 90, 48 px ailleurs. Le corps de la torche s'étend de −12 à +13 px autour de l'axe de la goutte, donc aucun chevauchement. |
| F25. Aucune sauvegarde ni aucun préréglage n'arrive en carte de gouttes | CONFIRMÉ | 31 préréglages `TestSaves` relus (pas de carte 86 à 94 ni 158). |
| F26. Briques de la DLL (registre, `PillarAi`, `AIValues`, `Bytes`) | CONFIRMÉ | `CreateForProduction` : 12 inscriptions. `Bytes[0..3]` = `+0x274..+0x277`, `AIValues[1]` = `+0x282` (même usage dans `MurutaAi`/`BeannoideAi`). |
| F27. Ordre d'un tick de la DLL | CONFIRMÉ | `RunEntityBlock` : MovePlayer, programmes, re-scan, compteurs, listes (qui recalculent `RidingEntity` via `UpdateRidingEntities`), horloge, physique, passe de coups. Le binaire calcule le même résultat dans la physique (`0x800383E4`), avec les positions de fin de tick précédent. |
| F28. `PushLogicalPositionToRoot` pour la montée de 160 px | NON VÉRIFIABLE | Dépend d'une sonde de la DLL (S-D6). |

### Tests existants

Les cinq sommes de clés sont CONFIRMÉES aux lignes citées :
- `NativeSpriteRegistryTests.cs:58`
- `LiftableAiRegistrationTests.cs:78`
- `ChargedArrowAiTests.cs:240`
- `WaveSwordLifeTests.cs:185`
- `BallFlamesTests.cs:239`

Aucun test ne cite 391 ni les cartes 88, 90, 92, 94 ou 158. Les 30 cartes de la chaîne d'histoire n'incluent aucune carte de gouttes. En revanche, `C4WorldTests` charge la carte 87 (arrivée à la ligne 35 au plus, hors zone de naissance des gouttes) : mon recensement des arrivées montre que les six gouttes y naissent bien pour une entrée normale depuis la 86, en tuile (41,47).

## 2. Corrections et manques

### 2.1 Les quatre gouttes de la carte 92 : ni 606 ticks, ni dix secondes, ni « à l'arrivée » sans condition

**Durée : 603 ticks, pas 606.** Après l'avance d'une attente, la boucle poursuit dans le même appel, et le premier appel de l'attente suivante a lieu au même tick. Le total est donc (240+1) + (240+1) + (120+1) = 603. Le 606 additionne N+2 par attente.

**Secondes : environ 12 s, pas 10.** La logique tourne à 50 Hz (`EntitySpawnFactory.LogicTicksPerSecond = 50`). Compter la chute puis le contact (état 3 vers 66 ticks après la naissance), l'extinction des torches se situe vers 670 ticks, soit 13,4 s. La question Q-G2C3-1 doit dire « une douzaine de secondes ».

**Déclenchement.** B[1] n'est pas lancé à l'arrivée. C'est l'événement de carte 0 (`EventCodesBIndex` 129), de zone tuiles x 26..42, y 8..28.
- Il tourne à chaque tick où le héros est dans la zone.
- Hors zone, `RunMapEventsPass` remet le programme à zéro à chaque tick, et le compte repart de zéro au retour.
- Il est suspendu quand le jeu est bloqué (`GameplayBlockedMask`).

**Existence des torches.** Les torches 43 à 46 n'existent que si l'arrivée est dans leur boîte de naissance (x 26..42, y 0..28). Seule l'arrivée en tuile (38,10), depuis la 91, satisfait les deux conditions. Les autres arrivées (9,15), (3,47) et (48,40) ne déclenchent pas le piège. Le préréglage `torche-m92` doit viser (38,10), et non le couloir bas (16,51) suggéré en F25.

**Cumul (cas oublié).** À chaque sortie puis retour dans la zone, quatre gouttes de plus naissent aux mêmes points : B[1] n'a pas de garde par drapeau, et les gouttes natives ne sont jamais détruites. La table d'entités compte 64 emplacements (`EntitySlotTable.SlotCount`), avec au plus 20 entités nées au chargement sur cette carte. Après une dizaine de passages, elle sature et les flammes ne naissent plus. C'est fidèle au binaire, mais l'auteur a une règle « un défaut avéré de l'original se corrige ». Cela mérite une question, avec pour défaut « comme l'original ».

### 2.2 Cas oublié : une torche lancée peut toucher une goutte (le second site 391 du binaire)

E20 (`0x8007F6C8`) lit `+0x130` et, si le type de cette entité est `0x187` (391), écrit `+0x274 = 1` (pas de flamme). Le test est à `0x8007F7A8`, le seul endroit du binaire qui connaît le type 391. D'après la DLL, `+0x130` est `XCollisionEntity`, l'obstacle du déplacement XY du tick (même usage que le pilier). C'est donc le cas « torche lancée qui bute sur une goutte suspendue ou en chute, éteinte sans flamme ».

Cela relativise F13 et le point 3 du §0 : la torche lancée ne peut pas servir d'appui, mais elle peut toucher une goutte. La branche est déjà au plan de g2c2, en vecteur de test synthétique. g2c3 doit ajouter un test de monde. Il est rare en jeu : une torche lancée monte à environ 67 px alors que les gouttes sont posées à 136–224 px du sol, donc seule la fin de chute est à portée.

### 2.3 Précisions de données et de durée

- **Fin de l'animation 2 : `Hold`, pas « Once »** (`sprite-records.json`, `End: "Hold"`). C'est ce qui lève `ForceResetAnimationFlag` (`+0xAC`) dans la DLL (`OnAnimationFinished`). Ce drapeau est remis à zéro à chaque changement d'animation.
- **Durée de l'animation 2 : 21 ticks, pas 22.** Ce sont 7 images de délai 131 (3 ticks chacune), soit 0,42 s à 50 Hz. Le « 22 » du rapport compte les deux bornes. La crainte « 22 ticks mesurés contre 0,42 s exportés » (risque 4) est donc plus faible qu'annoncé.
- **Test à écrire.** Le test de monde du cycle doit couvrir au moins 3 cycles, pour détecter un `+0xAC` périmé (un 2ᵉ cycle écourté).
- **Nommage du tick de contact.** Le test de la torche a lieu dans le gestionnaire de l'état 3, au tick dont le journal montre déjà l'état 4. Dans F9 « l'état 3, le tick d'entrée » prête à confusion pour qui écrira `Step`.
- **S-D3.** Le support est le `RidingEntity` de la goutte, pas celui du héros.

### 2.4 La période n'est pas N+90 pour 22 programmes sur 55

- **Deux programmes à drapeau** (carte 87, enregistrement 53, programme C[54] ; carte 92, enregistrement 25, programme C[26]) sautent leurs deux attentes quand le drapeau est posé. Après le premier cycle, la période est 1 + 88.
- **Les 14 programmes de la 158** commencent par un `00` (break). Avec le héros dans la boîte en continu, la période est N+91.
- **Les 6 autres à drapeau** (8 moins les 2 ci-dessus) font : si drapeau posé, attendre B, sinon attendre A puis poser le drapeau.

### 2.5 Autres ajouts

- **Statut écrit sans condition.** C71 écrit `statut=3` et `+0274=1` même sur une torche déjà en statut 4 (vecteurs « statut aléatoire » du modèle). Le port peut l'imiter ou documenter l'écart.
- **État 3 : l'avance est écrite avant le test de la torche.** `Bytes[0]=4` et `Flags &= ~0x80` sont posés avant la sortie anticipée. Un port qui ne les pose qu'en cas de torche bloquerait la goutte : mutant à ajouter.
- **Passage à travers le héros.** Après l'état 3, la goutte tombe dans le héros dans le binaire. La DLL doit confirmer que le collisionneur du moteur suit le drapeau `Collidable` : probe à ajouter (S-D8).
- **Chemin de chute de la DLL.** Le prefab n'a pas de contrôleur, donc la chute passe par `StepVerticalWithoutController`, qui reprend les règles du binaire. L'atterrissage y est strict. Ce n'est pas un risque, mais le nommer évite la confusion avec la branche du contrôleur.
- **Arrivées par entrée normale.** Les gouttes naissent en jeu depuis la 86 vers la 87 (tuile 41,47), depuis la 87 ou la 88 vers la 88, depuis la 89 ou la 90 vers la 90, depuis la 91 vers la 92, dans la boîte du boss Uma (94), et depuis les cartes 159, 160 et 161 vers la 158. Les 14 gouttes de la 158 sont donc visibles en jeu normal.
- **Mutants.** Sans compter l'ajout ci-dessus, le rapport en a 16, ce qui suffit.

## 3. Synthèse

| Famille | Résultat |
|---|---|
| Binaire A19, C71, destruction, attente, `0x40`, tables, cycle, période | CONFIRMÉ |
| Contacts (porté, posé, hors torche), fenêtre, course Carré | CONFIRMÉ |
| Données et recensement | CONFIRMÉ, avec la précision `Hold` et 21 ticks |
| Programmes C | CONFIRMÉ en nombre ; CORRIGÉ en formule (N+90 ne vaut pas pour 22 programmes) |
| Carte 92, B[1] | CORRIGÉ : 603 ticks, 12 s, déclenchement par zone et re-armement, arrivée (38,10) |
| Tests existants | CONFIRMÉ |
| Probes S-D1 à S-D7 | NON VÉRIFIABLE sans construction |
| §7 à partir de Q3 | NON VÉRIFIABLE (rapport tronqué) |

Questions à poser à l'auteur en termes de jeu :
1. Les quatre gouttes de la salle 92 apparaissent environ 12 s après qu'Alundra est restée dans la zone du fond, sans en sortir. Défaut : comme l'original, elles éteignent les torches non prises.
2. Quand Alundra quitte puis revient dans la zone, quatre gouttes de plus s'ajoutent au même endroit (invisible). Après une dizaine de passages, la salle n'a plus de place pour les flammes. Défaut : comme l'original.
3. Une torche lancée qui heurte une goutte s'éteint sans flamme (cas rare). Défaut : comme l'original.

## 4. Chemins

Dossier de travail :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/g2c3-critic/`

- Désassemblages : `dis_a19.txt`, `dis_c71.txt`, `dis_c70.txt`, `dis_e20.txt`, `dis_destroy.txt`, `dis_script.txt`, `dis_runscript.txt`, `dis_riding.txt`, `dis_listbuild.txt`
- Scripts : `tables.py`, `scanref.py`, `e2dis.py`, `e20dis.py`, `census.py`, `census2.py`, `classify.py`, `arrivals.py`, `bx.py`
- Sorties : `out_arrivals.txt`, `rerun/` (rejeux : `out_s1_native.txt`, `out_s2_carry.txt`, `out_s2_nocarry.txt`, `out_s2_posed.txt`, `out_s3_hit_window.txt`, `out_c71_full.txt`, et `s4_66.txt` à `s4_70.txt`)
- Copie de l'annexe du découvreur : `copy/`
- Copies du code de la DLL au HEAD : `EntityScriptProxy.HEAD.cs`, `EventProgramRunner.HEAD.cs`
- Sommes de contrôle : `SHA1SUMS.txt`