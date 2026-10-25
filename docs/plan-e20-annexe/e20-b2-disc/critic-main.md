**Passe critique, thème 2 : O-E20-24 et Objet 008**

Lecture seule respectée. Mes scripts et copies sont sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-proj/critic/w/`. Les oracles ont été rejoués sur des copies. Le HEAD est maintenant `42cd834a` (g1b a avancé depuis `86d421b5`), donc les numéros de ligne de `PlayerManager.cs` et de `ScriptedMotion.cs` ont bougé.

## A. O-E20-24

**CONFIRMÉ**
- **Cause.** MESURÉ, par lecture du code moteur : `World.AddEntity` ne fait que mettre en file (`World.cs:168-175`), l'intégration est en tête de `Update` (`:563`, `:822`). Tant que `Owner.World.PhysicsWorld` est nul, `TryResolveCollisionDependencies` (`CharacterControllerComponent.cs:1601-1627`) rend faux, et `MoveWithCollisions` (`:1056-1061`) ajoute le déplacement sans test.
- `CollectTickEntities` (`WorldProxy.cs:2889`) fait tourner le nouveau-né avant intégration. `ClampStepToMapEdge` est pur et ne gère que le bord de carte, donc il n'est pas en cause.
- Le commentaire de `ArrowLifeTests.cs:162-163` et la note f5 T1 (§5 point 6) disent la même chose : x = 111 pour un héros à 108.
- **Rejeu binaire.** `run_pointblank.py` rejoué depuis ma copie : `arrow3_point_blank` et `fire11_point_blank` sont identiques aux clés gelées (`cmp`).
- **Étendue.** Mon propre balayage (`crit_sweep.py`, murs colonnes 5 et 6, tous les projectiles) confirme les pas : 3 px pour flèches et glace, 6 px pour la chargée et le feu. Dans tous les cas le binaire borne à 109 (ou 133 pour la colonne 6).
- Le drapeau tombe à S+0 pour le héros collé (x0=109), la destruction ou le statut 3 à S+1. La flèche chargée n'a pas de statut 3.
- La vague de l'épée est immobile (position constante dans `trace_wave_steel_east.txt`).
- Les types 3, 4, 7 sont câblés en production (`PlayerManager.cs:1328`), 11, 12, 14, 15 ne le sont pas.
- Le héros du binaire s'arrête à 109,0 contre ce mur, donc le cas collé (x0=109) est le cas courant, pas un cas limite.
- D-E20-35 (pas d'intégration immédiate, pas de changement du moteur) et D-E20-38 (e0k4 non faite) sont cités fidèlement. Les options 2 et 3 restent donc bloquées.

**RÉFUTÉ**
1. **« Plus grande position libre en unités brutes » n'est pas la règle du binaire.**
   - Le binaire demi-pas par `sra 1` (`0x80037950`, `0x8003795C`, `0x80037D74`, `0x80037D80`) et accumule les moitiés acceptées.
   - Mon modèle de ce calcul égale les 10 cellules rejouées sur 10. La règle « plus grande position libre » se trompe de 1 unité brute sur 3 de ces 10 cellules (`halving_model.py`).
   - Cas concrets : x0=107 avec un pas de 3 px, et x0=104 ou 107 avec un pas de 6 px, donnent 7143423 (109 − 1 unité brute). La règle simple donnerait 7143424.
   - `ArrowOracle.Line` compare `x=` en entier brut. Un clamp « plus grand libre » échouerait donc sur ces clés. C'est aussi l'origine du drapeau à S+2 au lieu de S+1 (le « une fois sur trois » de D-E20-38).
   - Le contact du moteur est une bissection flottante (`CharacterControllerComponent.cs:1231-1296`), pas la boucle du binaire.
   - Conséquence : « e0k5 » T1 doit porter la boucle de demi-pas (un sous-ensemble d'e0k4), sinon les clés x0=104 et 107 ne peuvent pas passer. Sinon, se limiter aux clés x0 ∈ {105, 106, 108, 109}.
2. **Le mécanisme du drapeau à S+0 est faux tel que décrit.** Le squelette dit qu'un pas nul lève `ForceAdjusted` par `:923-926`.
   - Si le clamp écrit `FinalForceX = 0`, `SlideAlongWall` sort avant, parce que `agrees` (`ScriptedMotion.cs`, `forceX > 0` pour la direction 24) est faux. Le drapeau reste à 0.
   - Il faut soit garder `FinalForce` non borné et passer le pas borné à part, soit lever le drapeau explicitement après `SlideAlongWall`, comme `edgeAdjusted` (`:690-694`). Le cas collé (S+0) est celui qui tomberait dans ce piège.

**NON ÉTABLI**
- **Valeurs DLL.** Je n'ai pas exécuté la DLL. Les valeurs qui l'engagent (x=112 et statut 3 à S+2 pour le cas collé, destruction vers S+19, 2 ticks ou plus par image) viennent du modèle du lecteur. Seul x=111 pour x0=108 est appuyé par f5 T1 et le commentaire de `ArrowLifeTests`.
- **Axes Y.** Les axes nord, sud et ouest (pas de 2 et 4 px) ne sont pas rejoués par moi.

**MANQUES**
- Les clés `*_flush` du lecteur ne sont que dans son scratchpad : elles sont à régénérer dans une annexe avec manifeste.
- La clé principale doit être x0=109 (collé), avec les variantes `_pf`.

## B. Objet 008

**CONFIRMÉ**
- **`0x8004E18C` désassemblé à la main.**
  - Le premier objet possédé du créneau devient candidat (le delay slot `move a2,a3` s'exécute toujours). Si le bit 0 du drapeau est nul, retour immédiat.
  - Sinon, un objet suivant remplace le candidat seulement si son rang est strictement supérieur (`slt`).
  - Table à `0x800B9FE8` : créneau 1, objets 1, 2, 3, 4, 8, rangs 0, 1, 2, 3, 4, drapeau 1. Le port DLL (`PlayerManager.cs:2298`) est équivalent.
- **Enregistrement.** `0x80028C14[8]` = 11, et l'enregistrement 11 est identique à l'enregistrement 0 (non chargeable, sol `0x10`, air `0x14`). Les animations `0x10` et `0x14` ne comptent pas la charge. Seule `0x18` lance l'onde.
- **Dégâts rejoués par moi** (oracle c1b, `crit_legend.py`). Contre un Beannoïde à PV 100 :

| Inventaire | Arme choisie | Dégâts |
|---|---|---|
| épée de base | 1 | 2 |
| Elnard | 4 | 16 |
| #8 seul | 8 | 51 |
| {1, 4, 8} | 8 | 51 |
| {1, 8} | 8 | 51 |

  Le modèle indépendant `kmodel` est EQUAL dans chaque cas. La fiche d'équipement 38 (`balance.json`, `item + 0x1E`) a une seule attaque, puissance 50.
- **`0xC2`.** Le handler `0x80041D34` pose `Result = (octet[0x801EBA3E] >= param)`.
  - `0x801EBA3E` est `g_saveData + 0x756`, la base `0x801EB2E8` apparaissant dans `InitializeMapWarpPosition`.
  - Sites touchant `+0x756` : incrément et plafond à `0x800315C0/D4` (appelant unique `0x8002C59C`), remise à 0 à `0x80031860`, lectures `0x80041B44/58`.
  - L'opcode `0x03` est bien « si vrai, saut ».
- **Scène de la carte 7.** `B[10]` : `C2 [20]` en 1508, saut vers 1571 (dialogue 130), `0x82 [8]` en 1623.
  - La branche refus passe par le dialogue 129 et saute par-dessus `Flag on [251,6]` (1651) : elle se rejoue.
  - `F[7]` n'enchaîne sur `Flag on [0,128]` que pour la direction d'animation 1, soit nord. Elle est portée par les enregistrements 28 et 29 (type 25, tuiles (50,32) et (52,32)).
- **Sources de #8.** Scan indépendant de tous les programmes : un seul `0x82 [8]` (carte 7, pc 1623), aucun `0x83` ni `0x84` sur 8.
  - Un seul coffre à contenu 8 (carte 0, enregistrement 8).
  - Seulement deux ramassages de la page globale de type 38 : carte 7 enregistrement 31 et carte 11 enregistrement 24. Les 20 autres cartes avec le type 38 ont `SpriteTable[38]` local.
  - Aucun en-tête de sprite avec `Contents` direct, aucune entrée de la table de butin égale à 8.
  - Le lecteur avait raison contre le premier critique : `0x8A [38]` des cartes 6, 52 et 80 désigne un enregistrement 38 de type 25, 37 et 126.
- **DLL.** Pas de `case 0xC2` dans `EventProgramRunner` (repli par la taille, `Result` inchangé). Aucun test de sélection ni de dégâts pour #8. Le balayage `AT9*-item08` existe.

**À AJOUTER (manque)**
- L'enregistrement 31 de la carte 7 est un ramassage vivant : `IsEnabled` à 1, tuile (4,8), programme `C[5]` = « effacer le drapeau de gravité `0x100` » puis `Fly [0,0]`.
  - Les deux copies créées par `0x8A [31]` (pc 1411 et 1483) sont détruites par `0x2E [31]` dans les deux branches (pc 1539 et 1600).
  - Qu'un héros puisse toucher et ramasser ces objets, dans l'original comme dans la DLL, n'est pas établi. Ce serait une voie vers #8 qui contourne `0xC2`. À sonder en T0.
- Le `+50` s'applique à toute attaque du héros portant le bit d'équipement, pas seulement au coup `0x10`. Lignes 17, 21, 25 à 28, 56, 64 à 76 de la fiche du héros, dont certaines à puissance 100, soit 150 avec #8. Il faut tester aussi l'animation d'air `0x14`.
- `Result` est un registre persistant, jamais remis à zéro par `InitializeEventData`. Avec `0xC2` ignoré, la valeur à 1510 est celle laissée avant, pas forcément 0. Aucun opcode de 1268 à 1507 ne l'écrit, mais la valeur d'exécution n'est pas mesurée. La DLL pourrait donc déjà donner #8 par erreur. Sonde T0 à faire.

**NON ÉTABLI**
- Le menu d'équipement choisit-il un créneau ? La structure (`0x80057964` appelle les enveloppes de créneau) va dans ce sens, sans test en jeu.
- Je n'ai pas lu les `.sav` de `tools/Saves/` (consigne : noms seulement).

## Recommandation pour la tranche
- **O-E20-24.** La défaut B (clamp du tick de naissance) est bon, mais il doit porter la boucle de demi-pas du binaire et lever le drapeau explicitement. Clé principale x0=109, puis 106 et 108.
- **Objet 008.** Le squelette B.8 tient. Ajouter les sondes T0 sur `Result` à 1508 et sur la collectabilité de l'enregistrement 31.