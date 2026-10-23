# E20.f6a, T0 (2026-10-23) : résultats

Plan : `docs/plan-e20-heros.md` §1.22 (bloc « Révision au HEAD », points 1 à 10, puce « f6a ») et §1.22.1. HEAD de départ `e295875a`, branche `chantier/theme1-porter-plateforme`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`.
Prérequis relus : E20.e0k1 et e0k2 faites (le marqueur `EntityScriptProxy.HasLogicalZ`, posé avec `JoinsBirthTick` pour toute entité née par l'interface), E20.f4 et E20.f5 faites (`ChargedArrowAi`, `ArrowAi`, la règle de la clé de vitesse D-F5-5, `SyntheticField`, `CutBench`, ADR-0084), g5a à g5d faites. Dernier ADR au départ : à recompter à l'écriture (T2).
T0 ne change aucun code de la DLL : la sonde de la DLL tournait dans un fichier de test jetable (`probes/ZzF6aT0Probes.cs.txt`), retiré avant le commit ; le câblage temporaire de la prédiction a été annulé (`git diff` vide sur `Alundra/`).

## 1. L'oracle rejoué (la machine MIPS, le vrai code de `ALUN_CD.EXE`)

- Les scénarios du plan (`fire11_gp8_deepwater`, `fire11_gp2_slope1`, `fire11_gp4_slope2`, `fire11_gp6_slope3`, `fire11_mixed_footprint_water`, `ice14_gp8_deepwater`) et **toutes** les clés de boules et de flammes gelées de `bin/out/json/` et `bin/out/traces/` (`fire*`, `ice*`, `flames*`),
  rejouées depuis une copie par `oracle/run_all.sh` (`run_balls.py`, `run_balls_mixed.py`, `run_flames.py`) : **identiques octet pour octet** aux fichiers versés.
- Le recensement du plan : `bin/scan_offs.py 180 184 18C 190 188` et `bin/census_flags.py` rejoués : sorties **identiques** à `bin/out/scan_offs_180_190.txt` et `bin/out/census_flags_100000_200000.txt`.
- **Manifeste identique sur deux exécutions complètes** depuis deux copies (`oracle/manifest.txt`, 79 lignes : les clés et traces de boules de `bin/out` réécrites par la copie et `f6a/oracle/out`).
- Les six scénarios du plan portent déjà les clés `slope_18C`, `status` et `effects` (les colonnes de `run_balls.BALL_COLS` et le champ `effects` de chaque tick) : aucune clé à refaire pour eux. **Valeurs lues** (S = 12, tick 22 = S+10 : atterrissage) :

| Clé | `slope_18C` au tick 22 | Issue |
|---|---|---|
| `fire11_gp8_deepwater` | 4 | statut 4 au tick 23 (S+11), `attach(id=6,target=slot1,mode=1)` ce tick-là, case libre au tick 24, aucun tirage |
| `ice14_gp8_deepwater` | 4 | idem (x = 163) |
| `fire11_gp2_slope1` | 1 | idem (la branche B d'E9 en production, f6b ; ici, le choix du programme) : statut 4 au tick 23, `attach(6)`, libre au 24 |
| `fire11_gp4_slope2` | 2 | explosion : statut 3 au tick 23, 10 tirages, son 39 |
| `fire11_gp6_slope3` | 3 | idem |
| `fire11_mixed_footprint_water` | 0 | idem (le pied moitié sur l'eau : minimum 0) |

  Le tick du statut 4 est S+11 = 23 et la case est libre à S+12 = 24 ; le choix du programme est celui du binaire (`DestroyEntity(e, 6)` dans la passe de choix du début du tick 23, après que la passe d'attributs du tick 22 a posé 4).
- **Clés neuves** (`oracle/run_f6a.py`, écrites sous `oracle/out/json` et `oracle/out/traces`) : les quatre coins un à un, et la marche.

| Clé | Résultat de la machine |
|---|---|
| `fire11_dry_corner_c6r5`, `_c7r5`, `_c6r6`, `_c7r6` | eau profonde (0x08) sur les colonnes 5 à 9 sauf **une** case sèche sous un des quatre coins du corps (la boule se pose en x = 166 : coins dans les colonnes 6 et 7, les rangées 5 et 6) : `slope_18C` 0 aux quatre, explosion normale (statut 3 au tick 23, 10 tirages). Chacun tue le mutant « coin ignoré » (SL-3) par le coin qu'il rend sec |
| `fire11_step16_water` | une marche de 16 px (octet de hauteur 1) à partir de la colonne 6, eau partout : la boule se pose **sur la marche** en x = 148 (tick 19 = S+7), deux coins sur la colonne 5 ne sont pas posés : `slope_18C` 0, explosion au tick 20 |
| `ice14_step16_water` | idem avec la baguette de glace (x = 145) |

- **Les 10 mutants du test des quatre coins** (`oracle/out/mutants_f6a.txt`, la passe `0x80038064`, sur la vraie machine, RAM patchée) : **10 tués**.
  SL-1 (test « posé » inversé), SL-2 (minimum → maximum, tué par `mixed` et les quatre coins secs), SL-3 (boucle de trois coins au lieu de quatre, tué par les quatre scénarios de coin sec ; il casse aussi les mots `+0x180` et `+0x184` de la pile, d'où une boule qui disparaît au lieu d'un simple `+0x18C` faux : le test de la DLL ne lit que le `+0x18C`),
  SL-4 (un coin non posé ne remet plus à 0, tué par la marche), SL-5 (décalage 8 au lieu de 9), SL-6 (sentinelle `0xE00` → `0x600`), SL-7 (le « +1 » de l'égalité), SL-8 (la porte de gravité), SL-9 et SL-10 (le masque de la cellule).

## 2. Les sondes de la DLL (HEAD de départ, sans le code de f6a ; `probes/`)

Le banc : `BallRun` (neuf, entre dans T1 ; le modèle d'`ArrowRun` : `HitArena` sur la carte 14 réelle, le champ synthétique et son magasin sur les mêmes enregistrements avec la propriété de sol de chaque cellule (`CutBench.Cell.GroundProperty`, ajouté), les autres entités retirées, la baguette `Give(16)` ou `Give(14)` équipée dans l'emplacement qui la porte, Carré au tick 1, la graine de l'oracle au tick 1, la boule née par le crochet de test) et `BallOracle` (les clés lues par nom).

| Sonde | Attendu (écrit avant) | Mesuré |
|---|---|---|
| Naissance | S = P + 11 = 12 ; type 11 ou 14 ; drapeaux `0x132154` | **S = 12** aux douze scénarios, type 11 (feu) ou 14 (glace), drapeaux `0x132154` |
| Vol libre, S à S+10 | la ligne (statut, X, Z) égale la clé à chaque tick (Z de la DLL = Z du binaire − 1, ADR-0026) | **égal** aux douze scénarios : x, Z et « posé » identiques de S à l'atterrissage (S+10 à plat, S+7 sur la marche) ; le statut 3 de l'explosion (DeactivateOnImpact) arrive au même tick |
| Pas par image | un pas du générateur par tick, soit « tirages de la clé (0) + 1 » | **1 pas par tick** jusqu'à l'atterrissage, aux douze |
| `Slope_18c` d'une boule marquée posée, SANS le code | 0 à tout tick (rien ne l'alimente) | **0** à tout tick, aux douze, alors que la clé donne 4, 1, 2, 3, 0, 0, 0, 0, 0, 0, 0 (eau, pente 1, pente 2, pente 3, pied moitié sur l'eau, quatre coins secs, marche) : `UpdateGroundSlope` appelée à la main à l'atterrissage donne exactement la valeur de la clé |
| Effet 6 attaché | absent sans le code | **jamais vu** sur la scène |
| Après l'atterrissage | sans E9, la boule du banc continue d'avancer de 6 px par tick (statut 3 pour une explosion, 2 pour l'eau) | confirmé : les ticks d'après l'atterrissage ne se comparent pas à la clé (la boule n'a pas de gestionnaire en f6a) ; **les tests comparent de S à l'atterrissage, et le choix du programme au tick S+11 sur l'eau profonde** |
| Chaîne causale du choix du programme | `PickEventTrigger` du tick 23 lit `Slope_18c == 4` en statut Normal (`EntityScriptProxy.cs`, branche `case EntityStatus.Normal`) | à lire par le code de T1 ; avant lui, la boule passe au statut 3 (`DeactivateOnImpact`) au lieu de mourir |

## 3. Recensement des lecteurs (puce T0 du plan)

Binaire (`bin/out/scan_offs_180_190.txt`, 14, 10, 26 et 3 sites pour `+0x180`, `+0x184`, `+0x18C`, `+0x190`) : la passe `0x80038064` écrit `+0x180` (OU des quatre coins), `+0x184` (ET), `+0x18C` (pente, `t1 >> 9`) et `+0x190` (l'ancien `+0x18C`).
Les lecteurs hors de la passe, par adresse (la découverte ne les a pas tous rattachés à une fonction ; seuls les rattachements écrits au plan ou lus dans `bin/disasm/` sont nommés) : `+0x180` : `0x8002EB1C`, `0x80031AE4` et `0x80031EE4` (autour de `MovePlayer`, `0x80031B50`), `0x80032368`, `0x80032450`, `0x800368A8`, `0x80036954`, `0x800369CC` ; `+0x184` : `0x8002F14C`, C61 (`0x8007A958`, lecture en `0x8007A988`), E1 (`0x8007ED10`, lecture en `0x8007ED58`), E10 (`0x8007F23C`, lecture en `0x8007F2B0`) ;
`+0x18C` : `0x8002FB88` à `0x80031B98`, `0x80034248` à `0x800352F4`, `0x80066BA8`, `0x80066BD0`, E9 (`0x8007EF50`, lectures en `0x8007EF84` et `0x8007F0F0`), et `0x8007F578` dans la boucle de recherche d'E17 ; `+0x190` : `0x8007F5B0` dans la même boucle. Les trois lectures de la boucle de recherche d'E17 (`0x8007F540`, `0x8007F578`, `0x8007F5B0`) lisent `+0x188`, `+0x18C` et `+0x190` **d'une cible** comme bornes d'une boîte
(`+1`, comparées à des différences de positions de la boîte lue en `+0x170` à `+0x178`) : le désassemblage ne les lie pas à la pente d'une boule ; f5 les a portées avec la boîte stockée et son oracle est vert, et f6a n'écrit `+0x18C` que sur les entités marquées à gravité (les cibles de ces tests sont des enregistrements).
- **Propriétaires dans le plan** : `+0x18C` d'une boule : f6a (l'alimentation) et f6b (E9, qui le lit) ; `+0x184` : C61 et E10 (faits en f3, n'existent que sur les sprites 27 à 29, plan §1.20) et E1 (`DestroyEntity(e, -1)`, fait), **f6a ne nourrit pas `+0x184`** (il reste 0 pour une entité marquée, comme aujourd'hui) ; `+0x180` : `UpdateVramFlags` pour le héros seul (inchangé), et la branche `DestroyOnVramFlags` du choix du programme (`CombinedVramFlagsOR & 0x8004`), qui ne tourne pour une entité marquée que si son type porte `0x200000`.
- **DLL** (`rg`) : `Slope_18c` n'est lu hors du héros que par `EntityScriptProxy.PickEventTrigger` (`DestroyOnSlidingSlope` et `Slope_18c == 4`, statut Normal) ; `CombinedVramFlagsOR` par `PickEventTrigger` (`DestroyOnVramFlags`), `ScriptedMotion` (une ligne) et le héros ; `CombinedVramFlagsAND` par `PortalTrigger` (héros).
- **Types qui portent `0x200000`** (`bin/out/census_flags_100000_200000.txt`, 107 en-têtes) : f6a ne nourrit que `+0x18C` (D-F6-2) ; ces types ne sont pas touchés. **Types qui portent `0x100000` avec la gravité** : globaux 1, 2, 10 à 17, 23 (sans gravité), 234 ; locaux 34 à 38, 48, 107, 111 à 113, 134, 137, 174, 182. **Nés par l'interface en production** : les armes `{3, 4, 7, 27, 28, 29}` et le butin `0x1E + objet` (objet < 98, soit des types < 128) : **aucune intersection** avec les types à `0x100000` hors des boules (10 à 15, nées seulement par le crochet de test avant f6b) : le changement est **inerte en production**, comme l'annonce le plan.

## 4. Liste prédite des tests qui bougent, recomptée au HEAD de départ

Mesurée en posant temporairement, sur le HEAD, la ligne de f6a (`if (HasLogicalZ) UpdateGroundSlope();` dans la branche `IsActive` de `TickMotion`, après `ScriptedMotion.TickScriptedNpc`), puis la suite complète Release (copie neutralisée) en trois partitions (une passe complète dépasse dix minutes) :

| Partition (filtre sur `FullyQualifiedName`) | Tests | Échecs avec le câblage |
|---|---|---|
| `Arcs`, `Ai.`, `Audio`, `Cameras`, `Cells`, `Combat`, `Data`, `Dialogues` | 1 210 | 0 |
| `Entities`, `Events`, `ExportGuards`, `Hero`, `Items` | 2 607 | 0 |
| tout le reste (`Maps`, `Physics`, `Probes`, `Rendering`, `Rng`, `SaveGames`, `Session`, `Support`, `Transitions`, `UI`) | 1 789 | 0 |

Soit 5 606 tests, **0 échec : la liste prédite est vide** (le plan n'en attendait aucun). Un test existant qui bouge en T1 est un arrêt. Le câblage a été annulé (`git diff` vide sur `Alundra/`), les quatre `docs/hero-trace-389-*.txt` réécrits par les suites ont été restaurés (`git diff --ignore-cr-at-eol` vide) et jamais indexés.

## 5. Ce que T1 écrit (lu au HEAD, par symbole)

- **Point d'appel** : `EntityScriptProxy.TickMotion`, branche des autres entités que le héros : dans le `if (Status.IsActive())` qui contient `ScriptedMotion.TickScriptedNpc(this)`, juste après, `if (HasLogicalZ) { UpdateGroundSlope(); }` (statuts 2 et 3, pas dans `EvaluateEntitySupport`, qui tourne pour tous les statuts). **Marqueur** : `HasLogicalZ` (révision au HEAD ; `JoinsBirthTick` se pose avec lui pour toute entité née par l'interface, mais seul `HasLogicalZ` dit « la vertical appartient au tick »).
- **Docs que f6a rend fausses** (point de la puce « Docs ») : `EntityScriptProxy.UpdateGroundSlope` (« RESTRICTION… PLAYER ONLY… Every NPC's Slope_18c stays 0 », et la phrase « nothing reads it yet besides… »), la doc de `TickPhysics` et de `TickMotion`, `RunHeroProbes`.
- **Bancs** : `BallRun`, `BallOracle` (dossier miroir `Alundra.Tests/Ai/Projectiles/`, comme `ArrowRun`) ; tests dans `Alundra.Tests/Entities/` (le code testé est `EntityScriptProxy`).
- **Comparaisons** : de S à l'atterrissage (S+10 à plat, S+7 sur la marche) par la ligne de la clé ; au tick S+11 sur l'eau profonde : statut 4, effet 6 vu sur la scène à la fin de l'image qui contient S+11, libre à S+12, 1 pas du générateur par tick (aucun tirage).
- **(s2)** : une entité née par enregistrement et dotée de la gravité (un Beannoïde de la carte 14, sprite 79, que `HitArena` garde comme cible) debout sur la même eau garde `Slope_18c` 0 quand la boule marquée à côté vaut 4 (même monde, mêmes cellules) ; **écart au texte du plan** : le plan dit « la même boule née par enregistrement » ; aucun enregistrement de carte ne porte un type 10 à 15 (`bin/out/census_global_page_records.txt`), le témoin est donc l'entité à gravité née par enregistrement la plus proche.

## 6. Écarts relevés

1. Pas d'écart sur le périmètre. (s2) : voir ci-dessus.
2. `CutBench.Cell` reçoit un quatrième champ optionnel `GroundProperty` (fichier d'aide de test partagé, défaut 0, aucun appel existant ne change) : l'eau profonde du champ synthétique en a besoin.
