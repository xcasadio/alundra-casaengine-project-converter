# Critique adverse de la découverte E20.h4e1 (vent 1, pilote `0x80035EB0`)

**Verdict : REVISE.** La découverte est solide sur le pilote, la requête, le tri, les clés et les vecteurs. Mon propre interpréteur et mon propre modèle les confirment. Une affirmation qui porte une décision est **réfutée par le binaire** (D9 « branche morte »). C'est un P1 pour la tranche, car le port proposé serait faux près des falaises. Il y a aussi trois manques P2 et quelques erreurs P3.

Le dépôt est resté intact (`git status` inchangé). Je n'ai écrit que dans `h4e1-critic/`. Je n'ai pas écrit de `report.md` ni de `check.md` : la consigne de session interdit les fichiers de rapport, donc ce message en tient lieu.

## 1. Ce que j'ai refait

- **Interpréteur MIPS à moi.** `mymips.py` exécute le vrai `ALUN_CD.EXE`, dont j'ai relu moi-même l'en-tête PS-X (base `0x80020000`) et le SHA-1. Il ne partage aucun code avec le harnais d'h4e1.
- **Rejeu des 800 vecteurs d'h4e1 sur cet interpréteur.**
  - Résultat : 800 sur 800, depuis une mémoire vide.
  - Il exécute 444 adresses distinctes, soit 218 + 19 + 22 + 94 + 91 : pilote, garde, annulation, requête, tri.
  - Cinq patchs du binaire (porte `0x33` en nop, `slt` en `sltu`, `slti 0x6400`, dernière cible en nop, comparaison non signée) font échouer 21 à 56 vecteurs. Le rejeu discrimine donc.
- **Modèle du pilote écrit à partir de ma lecture du listing.** Fuzzé contre le vrai code sur mon propre générateur : 3 × 12 000 états, 0 écart. Le tri réel est appelé par l'interpréteur. Sur 8 mutants de mon modèle, 7 sont tués. Le survivant est le seuil non signé : mon générateur n'atteint pas le coin dx = dy = −32 768 que leurs 22 vecteurs atteignent.
- **Vecteurs régénérés** par `wind1_vec.py 800 20261018` : SHA-1 identique (`c0a0ca51…`).
- **Mutants supplémentaires sur leurs états** : décalage tronqué `/65536` (257 sur 800 tués), arrondi (320), positions non signées (79). Leur modèle passe à 48 mutants sur 48 tués.
- **Les 42 clés refaites** depuis une copie. Les 84 fichiers sont octet pour octet identiques à ceux d'h4e1.
- **Index écrit.** `+0 = i` et `+0x48 = −1` sur les 63 cases, `+0x48 = −1` sur le gabarit : lu dans `wind1_key.py` et dans le code d'init `0x8003B24C-0x8003B2D8`. Le gabarit n'a qu'un écrivain, `0x8003B21C`.
- **Contrôles sur les 42 clés.**
  - Pas par image : `rng0(t+1) = pas(rng1(t))` partout (139 paires de ticks pour `cast`, vérifié aussi sur chacune des 42).
  - `faithful`, `pf`, graine `0x12345678`, objet 49, 1er `rng0` égal à la graine : vérifiés partout.
  - PV du héros constants dans les 42 clés.
- **Comparaison avec et sans index** (`FAITHFUL=0`).
  - Frappe au statut 1 au lieu de 2 à sa naissance.
  - Vie 42..80 au lieu de 42..79.
  - Première touche au tick 52 au lieu de 51.
  - 11 tirages au lieu de 9 à certaines touches.
  - R-EL-1 est donc bien établi pour le vent 1.
- **Tableau de `expected_tables.txt`** recalculé en forme close (graine, 41 pas, `Below(0xC00001)` puis `Below(0x800001)`). Les 7 positions de `cast_pf` et les 8 de `cast_pf_gateoff` concordent.

## 2. Verdicts sur les faits qui portent une décision

| Fait | Verdict | Preuve |
|---|---|---|
| Pilote : phase 0 sans test d'animation, garde avant la porte `0x33`, parcours de la table jusqu'au tick supérieur, fin posée dans l'appel de la frappe 100, distance signée, Z strict | **CONFIRMÉ** | listing relu (`0x80035EB0-0x80036214`), rejeu, fuzz |
| Dernière cible = pointeur, posée même si la naissance échoue, effacée en phase 0 et en branche aléatoire | **CONFIRMÉ** | `0x80036070` : `sw s0, 0x712c` dans le délai du saut. Mutants « posée seulement si la naissance réussit » : 58 vecteurs. Mutant « jamais posée » : 171 vecteurs |
| Deux tirages avant la naissance, X d'abord (`0xC00001`, `0x800001`) ; `Below` de la DLL équivaut | **CONFIRMÉ** | `0x80036074-0x800360EC` ; `GameRandom.Next` avance puis rend le nouvel état |
| D8 : le premier candidat non sauté et trop loin arrête la recherche | **CONFIRMÉ** | `0x80036004` ; clé `d8_far_first_dec`. **Sans effet en production** (voir §7) |
| Table `{30..100, 0xFFFF}`, compteur lu avant l'incrément, annulation neutre `0x800351A0` | **CONFIRMÉ** | tables `0x800235E0`, `0x800235F4`, `0x800233EC` ; wrapper `0x80036480` |
| Première frappe (compteur 30) perdue par la porte `0x33` | **CONFIRMÉ** | clé `gateoff` : frappe au tick 32 avec le héros en `0x32` |
| Requête : balayage `1..compte−1`, filtres, clé, tri dès 3 | **CONFIRMÉ** | `0x8003AF70` relu ; 94/94 et 91/91 |
| Sprite 22 : drapeaux `0x2144` (pas de bit classe A, ni de `DestroyOnSlidingSlope` ni de `Collidable`), E1, `BreakEffect` 0, fiche (11, 32) | **CONFIRMÉ** | `map_alundra.json`, `sprite-records.json`, `BALANCE.BIN.json`, `EntityFlags.cs` |
| E1 en ligne sans clé de registre ; registre à 7 clés | **CONFIRMÉ** | `EventProgramRunner.cs:283-292`, `:328` ; `AlundraNativeSpriteRegistryTests.cs:55` |
| Vie d'une frappe : S..S+37, atterrissage à S+11, boîte dès S+7, première touche à S+9, libre à S+38, aucun effet ni son | **CONFIRMÉ** (oracle) | lu dans la clé `cast_pf` |
| Le Beannoïde (335, 9 PV, réponse vent 16) meurt d'une frappe | **CONFIRMÉ** | dégâts `(32 × 16) >> 4` = 32, PV max 9 |
| Ni f4, f5, f6a, h4c1 ni E14.b0 ne sont prérequis | **CONFIRMÉ** | vitesse d'animation 0 ; drapeaux sans `DestroyOnSlidingSlope` ni `DestroyOnVramFlags` ; E1 en ligne |
| **« La branche `IsOnGround` est morte » (D9, §0.3, F4, D-H4E1-5, P7)** | **RÉFUTÉ** | §3 |
| « Équivalent déclaré : clé en 64 bits » | **RÉFUTÉ** (P3) | §6 |
| Comportement de la DLL | **NON ÉTABLI** | rien n'est exécuté ; les sondes P1 à P8 restent à faire |

## 3. R1 (P1) : la branche « détruit sans effet » est vivante

**Ce que dit le rapport.** « Une entité née dans le pas du héros n'est pas encore passée par `0x80038064` ; le seul écrivain de `+0x144` est `0x8003810C`. » Il en déduit que le test est inatteignable et que `IsOnGround` d'un nouveau-né vaut 0 (P7).

**Ce que montre le binaire.**
- **Le seul écrivain est bien `0x8003810C`**, et mon balayage le confirme. Mais la création le déclenche : la fin d'`InitializeEntity` (`0x80039D04`) contient `0x80039F30 : jal 0x80038064`.
- **`0x80038064`** appelle `FloorHeight` (`0x80037F28`). Ce dernier rend `[+0x138] + 1`, soit le terrain plus 1, sans recherche de support pour un sprite non `Collidable`.
- **`0x8003810C : sw v0, 0x144(s0)`** est dans le délai d'un `beqz` : il s'exécute toujours. Il écrit `+0x144 = (plancher ≥ Z)`.
- **Juste avant (`0x80039EE8-0x80039EF4`)**, la création relève Z à terrain + 1 quand `terrain + 1 ≥ Z`. Donc à la naissance `IsOnGround = 1` exactement quand le point de naissance est dans le relief ou à sa surface, c'est-à-dire quand `terrain(X, Y) ≥ Z_héros + 128 px` (+1 unité brute).

**Preuve sur le vrai code**, avec le harnais d'h4e1 et seul le terrain changé (hauteur 9 × 16 px = 144 px pour x ≥ 120 px ; script `work/cliff_scen.py`) :

| Terrain | Destructions sans effet | Détail |
|---|---|---|
| hb 2 (32 px) | aucune | |
| hb 8 (128 px) | aucune | mot `+0x144` vu à 1 en fin de tick, jamais au retour de la naissance |
| hb 9 (144 px) | ticks 72 (x = 145,8 px) et 102 (x = 171,6 px) | nouveau-né au statut 4, Z 9 437 185, mot `+0x144` à 1 |

Dans tous les cas les deux tirages sont faits et la dernière cible est effacée.

**Conséquence pour la DLL.**
- `EntitySpawnFactory` relève déjà `PosZ` au terrain (`EntitySpawnFactory.cs:809-816`, même condition que `0x80039EF0`).
- Mais elle n'écrit jamais `IsOnGround` à la création : les seuls écrivains sont les pas verticaux de tick (`EntityScriptProxy.cs:900, 1005, 1019, 1148, 1487`).
- Le port « littéral » `if (r.IsOnGround != 0)` de D-H4E1-5 ne détruirait donc jamais rien. Le pilote est vert sur les 800 vecteurs (l'hôte de test y fournit `IsOnGround`) et faux en jeu.
- Il serait faux près d'une falaise, ou d'un mur de 144 px et plus, dans le rectangle de tirage (±96 × ±64 px) : l'éclair naîtrait dans le décor et blesserait ce qui est dessus.
- INFÉRENCE (balayage approximatif de `data-extracted`, cases de 16 px de haut et 24 de large) : 224 cartes sur 483 ont une case praticable avec, à portée du rectangle, une case à 144 px et plus au-dessus.

**Remède.**
- Après la naissance, tester le plancher de naissance : `r.PosZ <= r.FloorHeight`. Après le relèvement de la création, c'est équivalent à `terrain + 1 ≥ Z` du binaire. `FloorHeight` est posé à la création par `UpdateEnemyGroundHeights`. Le sprite 22 n'est pas un sujet éligible, donc plancher = terrain.
- Réécrire D-H4E1-5 en conséquence. Réécrire P7 : « au retour de `SpawnEntityBySpriteIndex`, `PosZ <= FloorHeight` vaut 1 pour hb 9 et 0 pour hb 8 ».
- Ajouter le test de monde (w10), qui seul tue ce défaut en production : clés `wind1_cliff_hb2_pf`, `_hb8_pf` et `_hb9_pf`, plus `_hb9_gateoff_pf` (§4). Le test (d1) doit aussi donner au nouveau-né une position, pas un mot `IsOnGround`.
- Les clés `edge_west_x20` et `edge_north_y10` supposent une mémoire à zéro hors de la grille : la colonne de terrain est lue en `0x80127168 + 2·X`, et pour X < 0 ce sont des globaux. INCONNU : le comportement réel hors grille sur terrain non plat. Documenter que ces clés valent pour un champ plat.

## 4. Clés : état et manques

**Existent et collent à leur banc** (R-EL-4, 42 sur 42) : toutes celles que citent (w1) à (w8) et (p1) à (p3).

**Manquantes (P2)** si le défaut B de Q-H4E1-1 est gardé en production.
- (p2) cite `wind1_full_table_pf`, (p4) cite `hurt60` : les deux sont en réglage fidèle. Sous le défaut B de production, il y a une frappe de plus au tick 32 et tous les tirages décalent.
- (w3) a le même défaut pour les interruptions T ≥ 33.
- Il faut soit fixer explicitement `StrikeDuringPose = false` dans ces tests, soit utiliser les clés ci-dessous.

**Clés faites avec le harnais d'h4e1, dans `h4e1-critic/critic_keys/`** (24 fichiers, `MANIFEST.sha1`, déterministes) :
- `wind1_cliff_hb2_pf`, `_hb8_pf`, `_hb9_pf`, `_hb9_gateoff_pf`.
- `wind1_full_table_gateoff_pf`.
- `wind1_hurt33_gateoff_pf`, `_hurt41`, `_hurt42`, `_hurt60`, `_hurt102`, `_hurt103`.
- `wind1_cast_pf_seed2` (graine `0xA468A517`, pour que (w1) ne dépende pas d'une graine chanceuse).

**Trois points de présentation (P3).**
- `edge_east161_frac` est un doublon de `edge_east161` : `xraw = 261 << 16` n'est pas une fraction. Le discriminant plancher/troncature est porté par les vecteurs (mutant tronqué tué par 257 vecteurs), pas par les clés.
- Chaque test de monde doit fixer ses trois réglages (`StrikeDuringPose`, `InclusiveBound`, `SortThreshold`). Les clés `*_dec_*` supposent un tri dès 3, alors que le défaut de production est 2.
- Ces réglages doivent être par thread, ou restaurés en `try/finally` : `GameRandom` est un état statique et xunit exécute les classes en parallèle.

## 5. Tests, preuves de retrait, phases

**Réfutabilité.**
- Chaque test cité tue des mutants nommés, et les vecteurs sont discriminants (§1). (d7d) avec `slot_reuse` accolé à `slot_control` est un bon couple : une comparaison par référence choisirait A′ de la case 1 (clé 3400 < 5000).
- **(d1) ne peut pas détecter R1** : l'hôte de test fournit `IsOnGround`. Il faut (w10).
- **(c1)** : « libère l'objet » est fait par l'annulation générale de h0a, pas par `Cancel` (corps vide). Il passe par l'annulation générale ; sans bouchon `Cancel`, il reste vert, ce qui est cohérent avec l'équivalent déclaré.

**Preuves de retrait.** Je les ai vérifiées famille par famille (bouchons des 3 entrées, `Drive` neutralisé, requête retirée, garde neutralisée, porte rétablie, `DistanceKey`, clé `(E,1)` ajoutée) : elles découlent des définitions de tests. Il manque la ligne R1 : neutraliser le test du plancher de naissance rend rouges (w10 hb9, fidèle et gateoff) et (d1) s'il est reformulé.

**Séparation phase A / phase B.** Conforme à R-EL-4 : coutures `ItemStartForTests`, `ItemDriverForTests`, `ItemCancelForTests` avec les signatures de D-H-4 ; aucune clé de registre à inscrire ; (p1) à (p4) rouges avant la phase B, (p5) vert avant et après. Le seul réglage à corriger est ci-dessus : (p2) et (p4) doivent être cohérents avec le défaut de production.

## 6. Autres écarts

- **Mutant « clé en 64 bits » non équivalent (P3).** Au coin dx = dy = −32 768, la clé vaut 2³¹. En int32 elle devient négative et la cible est retenue ; en 64 bits elle est positive. Les 22 vecteurs du coin tuent ce mutant. `DistanceKey` doit rendre un `int` calculé en `unchecked`, comparé en `(int)clé < 0x6401`.
- **P6 faux d'une unité.** Les clés donnent Z = 4 194 305 sur 64 px (convention binaire). En unités DLL c'est 4 194 304 (64 px exactement), pas « 64 px − 1 unité ».
- **`TargetQuery.AllSlots`** n'existe nulle part dans le plan : h4b1 passe « le compte par l'appelant ». Il faut dire qui le définit. Valeur 63 = `EntitySlotTable.SlotCount − 1`.
- **Plomberie du `compte`.** Comment (d1) passe-t-il le compte du vecteur ? (surcharge ou couture interne) : à préciser.
- **Blocs 25** : leur fiche donne 1 PV max alors que l'oracle les pose à 5000. Le banc doit poser `Hp = 5000`. Le leurre (bloc 25 non classe A, en (500, 300)) doit aussi être posé pour retrouver les numéros de case des clés `*_dec_*`.
- **(e1)** doit aussi affirmer qu'aucun effet ni son n'est émis à la mort (`BreakEffect` 0).
- **Recette.** « Du premier à 0,82 s (tick 42) » vaut pour le défaut fidèle ; sous B, le premier éclair est à 0,62 s (tick 32).
- **Noms.** `check_names.collision_findings` appelée sur `WindTome1Ai`, `TargetQuery` et `EarthTome1Ai` : aucune collision (le segment d'espace de noms `Items` n'est pas en conflit non plus). Le contrôle de l'arbre complet reste à la charge de l'exécutant.
- **Effets de coup (INCONNU).** Le harnais court-circuite le passage des effets (`0x80038E84`). Les tirages qu'un programme d'effet ferait à sa mise à jour (interpréteur `0x8003CFC8`, `0x8003D308`) ne sont donc pas dans les clés. Les 9 tirages par touche sont ceux de la passe de coups réelle. C'est le risque partagé de toutes les tranches qui touchent.

## 7. Verdict sur Q-H4E1-1 et sur les tâches

**Q-H4E1-1 (première frappe perdue).**
- Faits confirmés : 7 éclairs aux ticks 42 à 102 en fidèle ; 8 aux ticks 32 à 102 avec la porte retirée ; tous les tirages au hasard changent avec B.
- INCONNU : ce que les auteurs du jeu ont voulu. La correction « retirer la porte pour `0x32` » n'est qu'une des corrections possibles. Une table décalée à 32 ou un rattrapage à 33 en sont d'autres.
- Défaut B acceptable, mais à poser à l'auteur avec ces alternatives dans la question, en termes visibles.
- Les clés `_gateoff` et `_fixgate` sont dérivées d'un binaire modifié par un seul nop ; je les juge sûres, avec la mention « non fidèle à l'octet ».

**Q-H4B-3 et D8.** Avec le tri dès 2 retenu par défaut, la liste est triée et D8 n'a aucun effet observable en production. Il ne se teste que sous `SortThreshold = 3`. À dire dans (d7c) et (w7).

**Tâches proposées.**
- **T0 : OK avec ajouts.**
  - Ajouter les clés du §4 (déjà versables) et les sondes P9 (naissance dans le relief : `PosZ <= FloorHeight` vaut 1 pour hb 9, 0 pour hb 8) et P10 (un nouveau-né détruit à la naissance ne produit ni effet ni son, et sa case est reprise au tick suivant).
  - Corriger P6 et réécrire P7.
  - Rejouer mes scripts versés (ils sont reproductibles).
- **T1 : à corriger** :
  - D-H4E1-5 devient le test du plancher de naissance ;
  - (d1) donne une position au nouveau-né ;
  - ajouter (w10) ;
  - (e1) vérifie l'absence d'effet à la mort ;
  - réglages explicites et thread-safe ;
  - décoy et PV du banc.
- **T2 : à corriger** :
  - (p2) et (p4) cohérents avec le défaut de production ;
  - ajouter (p6) : falaise hb 9 par le chemin de production.
- **T3 : à corriger** :
  - le mutant 64 bits est tué ;
  - ajouter un mutant « test du plancher de naissance retiré » (tué par (w10)) ;
  - ajouter un mutant « hb 8 détruit » (tué par la clé hb 8).
- **Prérequis, arrêts, budget, retour arrière : conformes.**
- **Plan** : ne pas reporter « branche morte » ; reporter R1 et la règle du plancher de naissance.

## 8. INCONNU

- Le comportement de la DLL (rien n'est exécuté) : P1 à P8 plus P9 et P10 restent à faire.
- La fréquence réelle de R1 en jeu : mon balayage de cartes est approximatif.
- Le terrain lu hors grille côté ouest, sur terrain non plat.
- Les tirages éventuels des effets d'étincelles à leur mise à jour.
- Le contenu de la piste XA 4 (Q-H4-1) et la disponibilité du tome 49 sur la chaîne (Q-H4-7).

## 9. Fichiers (sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el2/h4e1-critic/`)

- **Outils :** `mylib.py`, `mymips.py` (interpréteur MIPS à moi), `mymodel_fuzz.py` (mon modèle du pilote ; résultats dans `fuzz_mine.txt`), `replay_mine.py` (rejeu de leurs 800 vecteurs ; résultat : tous OK), `replay_mut.py` (cinq patchs du binaire).
- **Scans :** `scan144.py` (accès à `+0x144`), `scan_tmpl.py` (accès au gabarit), `scan_rng.py` (utilisateurs du générateur).
- **Clés :**
  - `critic_keys/` : les 12 clés du §4, avec `MANIFEST.sha1` ; script `work/critic_keys.py`.
  - `keys_rerun/` : relance complète des 42 clés d'h4e1, identique octet pour octet ; manifeste `MANIFEST_rerun.sha1`.
  - `keys_stale/` : `FAITHFUL=0`, 2 clés.
  - `work/cliff_scen.py` : script de falaise.
- **Vecteurs :** `work2/` (leurs outils avec 3 mutants de requête en plus), `vec_regen.json` (SHA-1 identique).