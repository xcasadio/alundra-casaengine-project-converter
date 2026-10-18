# Critique adverse de la découverte E20.h1 (les cinq objets instantanés)

Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h1/h1-critic/`, noté `C/`.

Je n'ai pas écrit `report.md` : la consigne du système interdit les fichiers de rapport. Ce message en tient lieu.

**Dépôt lu.** HEAD `317b3360`, alors que la découverte lisait `e8087d74`. `git diff --stat e8087d74 HEAD -- Alundra Alundra.Tests tools` est vide. Le dernier commit de code reste `15804ae3`, donc les citations de la découverte se lisent au même code. `git status` est identique à celui du départ.

**Binaire.** SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` vérifié. Mes scripts refusent tout autre fichier.

**Légende.**
- FAIT-C : établi par ma critique (désassemblage, interpréteur, expérience).
- FAIT-D : code ou donnée lu au HEAD.
- INFÉRENCE : déduit, source dite.
- INCONNU : non établi.

---

## 0. Verdict

La découverte tient. Tous les faits de comportement du binaire sont CONFIRMÉS, et je les ai refaits sans réutiliser leur code.

Je n'ai trouvé aucun P0 ni P1. Je trouve 1 P2, 7 P3 et 8 P4, tous à reporter au plan (§3 et §6). Aucun ne touche à un comportement du binaire. Ils portent sur l'adaptateur de vecteurs, les preuves de retrait, des noms de clés, des cas non couverts et des formulations.

Deux points deviennent mesurés : l'eau profonde (§4) et le mouvement du héros.

---

## 1. Ce que j'ai refait

1. **Mes propres listings.** Capstone sur mon chargeur (`C/c_exe.py`, `C/c_lst.py`), 17 fonctions dans `C/lst/` : `0x8002ED64`, `0x8003499C`, les 5 départs, les 6 soins, `0x8004E5C4`, `0x8004E0F8`, `0x8003BDD8`, `0x8002BAEC`. Le nombre d'instructions de chaque fonction égale celui de la découverte. Lus avant les leurs.
2. **Mon interpréteur MIPS, écrit de zéro** (`C/c_cpu.py`).
   - Il implémente les délais de branche et de chargement R3000.
   - La mémoire est stricte : un octet non listé fait lever une erreur, et tout octet fourni doit être effectivement lu.
   - Rejeu depuis une mémoire vide : **2 649/2 649** vecteurs et **31/31** vecteurs de la fonction par image (`C/out/replay_vec_out.txt`, `replay_vec_ss_out.txt`).
   - Le rejeu compare le retour, `v1`, les écritures, le journal des appels et l'ensemble exact des octets lus.
   - 0 danger de délai de chargement.
   - Couverture : 100 % des instructions des 13 entrées. `UseItem` : 31/31 avec tous les vecteurs, 25/31 avec les seuls vecteurs « jeu ». C'est le chiffre de la découverte.
   - Mon interpréteur n'est pas vide : 16 patches d'une instruction dans l'image font échouer le rejeu, et un patch neutre passe (`C/out/binmut_out.txt`).
3. **Mon modèle des 5 départs et des 6 soins** (`C/c_keycheck.py`, écrit de ma lecture). Appliqué aux 30 clés, soit 1 250 lignes, il a **0 écart**. Contrôlés : événements, champs d'effet (position, forces), état (PV, PM, compte), `rng1`, `rng2`, `rng0(t+1)`, `draws`, mots d'objet, miroir des stats.
4. **Rejeux de leur machine, depuis une copie** (`C/rerun/`).
   - `keys_h1.py` deux fois : manifestes identiques entre eux et à ceux versés. `cmp` des 30 fichiers : 0 différence.
   - `run_all.sh` complet : `SHA1SUMS_vectors_keys.txt` identique à celui de la découverte.
   - Ces rejeux prouvent le déterminisme, pas l'indépendance. L'indépendance vient de 2 et 3.
5. **Analyse statique indépendante des états** (`C/c_reach.py`) : accessibilité de `jal 0x8002ED64` depuis chaque gestionnaire de la table `0x80022D3C`. Exactement 63 états l'atteignent. Les 17 autres sont `03 0D 1A 1B 1E 27 36 37 38 3D 43 45 47 48 4A 4C 4D`. C'est l'ensemble de la découverte. Pour la table de nage `0x80022E7C`, les 15 états `Skip` sont les seuls sans appel.
6. **Promotion `0x8004E0F8` exécutée sur le vrai code**, avec la vraie table `alundra-project/Data/items-properties.json` (`C/c_promo.py`).
7. **Expériences sur leur machine copiée** : eau profonde et héros en mouvement (`C/rerun/oracle_copy/c_swim.py`, `c_move.py`, sorties dans `C/out/`).
8. **Citations DLL.** Une quarantaine vérifiées au HEAD. Aucune fausse (voir §2, F16).

---

## 2. Verdicts par fait décisif

| # | Fait | Verdict | Preuve |
|---|---|---|---|
| F1 | Les cinq départs rendent la constante 1 (`0x80034A38…` puis `0x80034AB8`) et n'écrivent jamais l'objet en cours, le compteur ni la phase (`0x80034A88-A9C` sautés). | CONFIRMÉ | FAIT-C : mon listing du répartiteur. Clés : mots d'objet à 0 sur 1 250 lignes. |
| F2 | Les départs ne lisent que PV/PM, la position du héros, le compte et le générateur. | CONFIRMÉ | FAIT-C : listings des 5 départs. Seuls `+0x14`, `+0x18`, `+0x114/118/11C`, stats `+4/+6`, le compte et `0x80098708`. |
| F3 | Les gardes du cadre. | CONFIRMÉ (voir C-2) | FAIT-C : listings de `0x8002ED64` et `0x8003499C`. Table `0x8002339C` relue : trous `0x21 0x22 0x28 0x2A`, entrées `0x24→A2C … 0x29→A6C`. |
| F4 | Les 63 états qui boivent et les 17 sans réaction. | CONFIRMÉ | FAIT-C : statique indépendant ci-dessus, plus leur scan dynamique. Cohérent avec le DLL : `SprintTick` (état 03) n'appelle pas `TryUseItem`, et Rond tenu en course donne le plaquage (`AlundraHeroRunStatesTests.cs:307`). Mesuré par moi, la course ne boit pas (PV 3 inchangés, état 04). |
| F5 | Les soins écrivent les PV de l'entité (`+0x14`), les PM par `0x8004DEBC`. Les stats suivent par le miroir de fin de `MovePlayer`. | CONFIRMÉ | FAIT-C : listings. `0x8004DEA4` = Mp, `0x8004DDDC` = MpMax, `0x8004DEBC` = SetMp bornée. FAIT-D : `PlayerManager.cs:634-645`. Mon modèle : `stats_hp == hero_hp` sur chaque ligne. |
| F6 | Formules et conditions des 6 soins. Herbe : `min(max, PV + max(5, max/4))`. Potion S et eau 1 : `max(10 ou 5, max/2)`. Potion L : PV = max. Extrait : PM = max. Breuvage : les deux, vie d'abord. | CONFIRMÉ | FAIT-C : 2 649 vecteurs sur mon interpréteur ; 30 clés sur mon modèle. |
| F7 | Effets : effet global 14 ; anneaux (`<<11`, force `×448`, `ForceZ 0x30000`) ; étincelles ; animations 0/2 ; Z `+0x80000` ou `+0x100000`. | CONFIRMÉ | FAIT-C : listings. Tables `0x80023654/94` identiques à `BurstTableX/Y` de la DLL (comparées). |
| F8 | Tirages : 0, 0, 0, 16 (Breuvage), 8 (Potion S), soit 2 par étincelle obtenue. La note « 3 tirages » de `h-g-deps.md:89` est fausse. | CONFIRMÉ / RÉFUTÉ (la note) | FAIT-C : listings et clés. `draws` = 2 par étincelle obtenue, 0 si l'effet est refusé. |
| F9 | Sons : `0x31` (Herbe, Potion S), `0x32` (Potion L), `0x30` (Extrait, Breuvage), `0x3` au refus. | CONFIRMÉ | FAIT-C. Les ids 48, 49, 50 et 3 existent dans `alundra-project/Sounds/sfx-manifest.json` avec `skip_reason` nul (C-13). |
| F10 | `UseItem` : hors table → 0 sans écriture ; compte nul → -1 sans écriture ; sinon -1. Le DLL est identique. | CONFIRMÉ | FAIT-C. FAIT-D : `PlayerManager.cs:2263`. |
| F11 | Le second site de garde `0x8002BB94` est Start + Select (`0x900`) tenu 121 images. Il consomme un Breuvage s'il y en a un, met les PV du héros à 0 et `+0x1CC = 0x78`. Ce n'est pas une potion de secours. | CONFIRMÉ | FAIT-C : mon listing de `0x8002BAEC` et 31 vecteurs rejoués. La décompilation (`GameEngine.cs:1523-1553`) concorde, sauf l'écriture de `+0x1CC` (C-12). L'intention reste INFÉRENCE. |
| F12 | Les appelants des fonctions de soin. | CONFIRMÉ | FAIT-C : mon `jal`-scan donne les mêmes appelants. `0x80033F00` : le soin sur place n'a lieu que si le sac refuse l'objet (`0x80033EC8`). `0x800347D4` est aussi appelé par la mort (`0x80032818`). Le rôle de C255 et le lien de `0x80041B14` avec l'opcode `0xBB` restent INFÉRENCE. |
| F13 | Les soins n'appellent pas `0x8004B730` (pas de calage du HUD). | CONFIRMÉ | FAIT-C : aucun `jal` correspondant dans les 17 listings. Le HUD a un rattrapage dans les deux sens (`HudDirector.cs:559`). |
| F14 | Écarts de la décompilation : `0x29` envoyé au corps du Breuvage ; position des anneaux de la Potion L. | CONFIRMÉ | FAIT-D : `PlayerManager.cs` (décompilé) lignes 1958-1974, 2299, 4918-4944. |
| F15 | Corrections des annexes : 3 tirages faux ; Fortifiant « PV et PM » faux ; Essence magique « cinématique ». | CONFIRMÉ | FAIT-C pour la Potion S : PV seulement. Nuance : le Breuvage soigne sur place, et la mort réutilise ce même départ (F12). |
| F16 | Citations DLL du §3. | CONFIRMÉ | FAIT-D : toutes vérifiées. Une quarantaine, dont les 13 sites `TryUseItem` (432, 451, 575, 607, 617, 669, 675, 679, 700, 726, 756, 861, 915) et `ItemsCount = 99` en `:2033`. `PlayerStats.cs:37` et `WorldProxy.cs:2018` désignent l'en-tête du symbole cité. Au binaire : 15 `jal 0x8002ED64`, mon scan en donne bien 15. |
| F17 | Clés conformes à R-EL-1 et au pas par image `_pf`. | CONFIRMÉ | FAIT-C : `keys_h1.py:106-114` écrit `+0 = i` et `+0x48 = -1` sur les cases 1 à 63 et sur le gabarit, comme `fire2_key_c.py` FAITHFUL ; le pas est ajouté après `rng1`. Les cases 1-63 comme R-EL-1 : sans effet ici, aucune entité ne naît. |
| F18 | Pas de défaut avéré de l'original dans la tranche. | NON ÉTABLI (rien trouvé non plus) | Une particularité est relevée en C-2. |

---

## 3. Constats et corrections à appliquer

### C-1 (P2) — « le fichier de vecteurs entier » est impossible pour 15 vecteurs

Dans chacun des 5 fichiers de départ, 3 vecteurs synthétiques font varier la taille de la table d'inventaire `[0x8017638C]` :

| Fichier | Ids de vecteur | Valeurs de la taille |
|---|---|---|
| Herbe | 72-74 | 0x24, 0, 0x10 |
| Potion L | 72-74 | 0x25, 0, 0x10 |
| Extrait | 44-46 | 0x26, 0, 0x10 |
| Breuvage | 186-188 | 0x27, 0, 0x10 |
| Potion S | 72-74 | 0x29, 0, 0x10 |

La DLL a `public const int ItemsCount = 99` (FAIT-D `PlayerManager.cs:2033`). L'adaptateur de §3.g ne peut pas injecter ces valeurs. La règle d'arrêt (« une adresse sans membre arrête la tranche ») se déclencherait donc à T1.

**Correction.** Classer ces 15 vecteurs « binaire seul ». Le chemin d'erreur de `UseItem` est déjà testé dans la DLL : `AlundraItemAndMoneyOpcodesTests.cs:126-129` (ids 99 et -1). Écrire « (v1)/(v2) : le fichier, moins ces 15 ».

### C-2 (P3) — « l'objet du Rond promu » est inexact

`0x8004E0F8` charge `s0 = stats+0xC` avant l'appel de promotion et rend `s0` (listing `0x8004E10C`, `0x8004E14C`). Le répartiteur dispatche donc sur l'id d'avant la promotion, alors que `stats+0xC` reçoit l'id promu.

FAIT-C, exécuté sur le vrai code avec les vraies données :
- Équipé `0x2A`, possédé `0x2A` et `0x26` : rend `0x2A`, puis l'équipé devient `0x26`.
- `0x2B` avec `0x2C` : rend `0x2B`, équipé `0x2C`.

Dans `items-properties.json`, `0x26` et `0x2A` partagent l'emplacement 15 (priorités 0 et 2, indicateur de remplacement 0).

La DLL a déjà le bon comportement (`PlayerManager.cs:2209-2231`). Reste la formulation du plan. Le test des trous (p7) doit poser `0x2A` possédé sans `0x26`, sinon l'effet de bord de promotion masque ce que le test vérifie.

### C-3 (P3) — les preuves de retrait ne suivent pas « exactement » des définitions

1. **« Retirer un effet d'un soin »** nomme (v1), (e1), (w), (p). Il faut aussi, pour les objets appelants :
   - (v2), car les vecteurs de départ journalisent les appels d'effet ;
   - (e2), si le soin crée des anneaux ou des étincelles ;
   - (g1), s'il s'agit d'une étincelle.

   Pour l'eau 1 (sans appelant dans h1), seul (v1) devient rouge.
2. **Les ensembles « verts » de la preuve de retrait des entrées** ne tiennent que si (r), (e1), (e2), (g1) appellent le départ ou le soin directement ou par la couture. Si l'un passe par la table, il devient rouge au retrait. À écrire dans le plan.
3. **(p1)…(p5) « mêmes clés »** : dire quelles clés (une par objet ou toutes). Toutes doubleraient les 30 tests de monde.

### C-4 (P3) — la clé `spotion_two_uses` n'a pas deux usages

Le maximum de possession de la Potion S est 1. Au tick 35 le compte est 0 : refus, son 3. Mon modèle l'établit (`uses=1`, 0 écart).

Aucune des 30 clés n'a deux usages réussis consécutifs. L'Herbe a pourtant un maximum de 9.

**Correction.**
- Renommer la clé en `spotion_use_then_refused`.
- Ajouter une clé `herb_two_uses` (compte 3, PV abaissés au tick 29 par `relower`).

### C-5 (P3) — `herb_circle_held_30` ne s'écrit pas avec `Press`

`Press` pose `_hold`, que `AlundraArcSupport.cs:236` reporte toujours comme `ButtonsJustPressed`. Trente `Press(Circle)` donneraient trente fronts, donc trente boissons.

Il faut `HoldDirections(PadState.Circle)` (`:463`), qui accepte n'importe quels bits de pad et ne signale le front qu'à la première image, puis `ReleaseDirections()`. À écrire dans (w) de cette clé.

### C-6 (P3) — le minimum 10 de la Potion S n'est épinglé que par les vecteurs

Les montants écrits à la main {10, 25, 30} et les trois clés `spotion_hp3of10/25/30` ne distinguent pas `max(10, ·)` de `max(9, ·)` ou `max(11, ·)` :
- à 10, le plafond (3+9=12 ou 3+11=14, tous deux ramenés à 10) masque la différence ;
- à 25 et 30, la formule domine (12 et 15).

Un PVmax de 19 avec PV 3 donne 13 contre 12 ou 14. Les vecteurs tuent ce mutant (ex. `hp=0 hpmax=10`), mais la couche « à la main » et les clés non.

**Correction.** Ajouter PVmax 19 à (r-potionS) et une clé `spotion_hp3of19`. Pour l'Herbe, le minimum 5 est déjà épinglé (`hp3of10` : 3→8).

### C-7 (P3) — la famille de mutants « ordre soin/son/consommation » n'est pas tuable telle quelle

Dans la DLL, les effets passent par `Emit` (appliqués plus tard) et le son est immédiat. Le soin ne lit pas le compte. L'ordre effet/son n'est donc pas observable. Le journal du binaire le montre, pas celui de la DLL.

**Correction.** Retirer cette famille, ou la spécifier par un faux `ISoundPlayer` qui lit PV et compte à l'appel de `PlaySfx` (PV déjà soignés, compte pas encore décrémenté).

### C-8 (P3) — le sixième soin (eau 1) et le cas `a0` ≠ héros

- `Half(5)` pour l'eau 1 n'a aucun appelant avant h4f1 : code mort dans h1, couvert par (v1) seul.
- Les vecteurs synthétiques à `a0` ≠ héros (6 à 9 accès `other_entity` par fichier de soin) imposent une API (cible, héros) sans sens en production.

**Correction.** Décider : soit reporter l'eau 1 à h4f1, soit la garder « testée par vecteurs seulement » ; et classer le cas `a0` ≠ héros « binaire seul » ou nommer le second paramètre.

### P4

- **C-9 — héros en mouvement.** Aucune clé ne couvre un héros qui bouge.
  - FAIT-C : à pied vers l'est, au tick 30, l'effet naît à X = 11 106 304, la position d'avant le tick, alors que le héros finit à 11 266 048. En saut, Z de l'effet = Z d'avant le tick + `0x100000`.
  - FAIT-D : la DLL lit la même position, car `MovePlayer` passe d'abord dans le tick, avant l'horloge d'animation et la physique (`WorldProxy.cs:2918-2928`, `EntityScriptProxy.cs:1540-1547`).
  - À ajouter : une clé « marche » avec relation test « X de l'effet = X au début du tick ».
- **C-10 — eau profonde.** Voir §4. Ajouter deux clés (au repos dans l'eau, en nage).
- **C-11 — héros touché au même tick.** Pas de clé possible (E20.c2b absent). INFÉRENCE : la passe de coups `0x80039300` est la dernière de `0x8003B388`, après la passe des entités. Le soin précède donc les dégâts. À consigner comme point ouvert pour c2b.
- **C-12 — décompilation.** Dans le bloc Start + Select, la ligne qui écrit `+0x1CC = 0x78` est commentée (`GameEngine.cs`, `DamagedTickCounter`). À ajouter aux écarts relevés (E18).
- **C-13 — sons.** Les ids 48, 49, 50 sont présents et résolubles dans le manifeste (FAIT-D). La sonde P5 devient une assertion de non-régression.
- **C-14 — précédent de repli et `unchecked`.**
  - `IncreaseHpMaxAndFill` et `RestoreLifeAndMagic` retombent sur les stats quand le héros est « nu » (`PlayerManager.cs:1875`, `:1910`). Le plan refuse au contraire (r-nu). Le justifier explicitement.
  - Les vecteurs d'addition avec PV = 2 147 483 640 exigent un enroulement 32 bits : écrire `unchecked` explicitement (pas de `CheckForOverflowUnderflow` dans les projets).
- **C-15 — code d'étincelle déjà présent.** `CreateLifeVesselEffects` (`NativeEffects.cs:31`) contient la même formule `hi(r1×0x20001)−0x10000` / `hi(r2×0x30001)−0x18000`. Si un assistant privé partagé est introduit, ne pas changer le comportement de la fiole et épingler sa non-régression.
- **C-16 — ordre.** L'ordre ferme du plan (§1.20, point 4 : f4, f5, f6a, f6b, puis h0 et h1) subsiste même si h1 n'en dépend pas ; le rapport dit « prérequis » sans mentionner l'ordre.

---

## 4. Les cas demandés

| Cas | Résultat | Étiquette |
|---|---|---|
| Valeurs de PV limites | Couvert par les vecteurs : PVmax 1-5, 8-12, 18-26, 30, 49-51 ; PV de PVmax-1 à 0 ; négatifs et énormes. Trou de la couche « à la main » : C-6. | FAIT-C |
| Réservoir d'effets plein | Clés `herb_pool_full`, `spotion_pool_full`, `spotion_pool_2_free`, `lpotion_pool_3_free`, `cureale_pool_5_free`. Mon modèle les reproduit. Pas de clé de réservoir pour l'Extrait : même code que la Potion L, couvert par vecteurs. | FAIT-C |
| Compte 0 | Clé Herbe (`last_one_then_none`) et refus de la Potion S. Les trois autres : vecteurs et répartiteur. | FAIT-C |
| Second site de garde | Voir F11. Rien à porter dans h1. La garde « aucun objet en cours » ne coupe jamais le geste tant que les cinq objets ne restent pas en cours. | FAIT-C |
| Objet déjà en cours | Le répartiteur refuse (son 3) si `[0x80127164]` est non nul et différent de `s0`. Vecteurs `dispatch` : 204. Un instantané ne peut pas être « en cours ». Pas de clé de monde avant h3 et h4. | FAIT-C |
| Eau profonde | Mesuré par moi, cellules `0x0800` (pente 4), bottes de nage `0x1B`. Les cinq objets boivent dans l'eau, à l'arrêt (état `0x1D`) et en nageant (`0x0F`), avec les mêmes sons et le même nombre d'effets qu'à terre. L'animation ne change pas, la pente reste 4, Z du héros = 1. Herbe : PV 3→8, compte 1→0, son 49, 1 effet aux mêmes arguments qu'à sec. Le DLL (`Steer`, `PlayerManager.cs:697-700`) appelle `TryUseItem` dans ces états. La découverte l'avait en INFÉRENCE. | FAIT-C (`C/out/swim_out.txt`) |
| Héros touché au même tick | Non mesuré. Voir C-11. | INCONNU / INFÉRENCE |
| En l'air | La découverte a mesuré les 63 états en l'air. Jeu mesuré par moi : effet à Z d'avant le tick + `0x100000`. | FAIT-C |
| Alarme de PV bas | Elle précède le soin dans le tick (son 400 avant 49). Le DLL joue l'alarme en tête de `MovePlayer` (`PlayerManager.cs:350`), avant les sites d'appel (≥ 432). Ordre préservé. | FAIT-D |

---

## 5. Verdicts par décision et par tâche

| Élément | Verdict | Sévérité des corrections |
|---|---|---|
| D-H1-1 (HeroHeals, InstantItems, créateurs d'effets) | CONFIRMÉ avec corrections | C-8, C-15 |
| D-H1-2 (lit et écrit l'entité, pas de repli stats) | CONFIRMÉ | C-14 |
| D-H1-3 (effets en file, tirages à l'application) | CONFIRMÉ (écart ADR-0048 accepté, réaffirmé) | — |
| D-H1-4 (pas de calage HUD) | CONFIRMÉ | — |
| D-H1-5 (soin sur place du ramassage → e0c) | CONFIRMÉ | — |
| D-H1-6 (Start + Select → E18) | CONFIRMÉ comme fait. L'assignation est la décision de l'auteur (Q-H1-2). | — |
| D-H1-7 (adaptateur adresses → membres) | CONFIRMÉ avec correction | **C-1 (P2)** |
| T0 (annexe, oracle, sondes, liste prédite) | CONFIRMÉ. (a) est déjà vrai : SHA-1 identiques. | C-13 |
| T1 phase A (tests puis code) | CONFIRMÉ avec corrections | C-1, C-3, C-4, C-5, C-6, C-7, C-9 |
| T2 phase B (cinq entrées, préréglage) | CONFIRMÉ avec corrections | C-2 (p7), C-3 ((p) quelles clés) |
| T3 (ADR, plan, verifiers frais) | CONFIRMÉ | C-7 (familles de mutants) |
| Q-H1-1, Q-H1-2, Q-H1-3 | Défauts raisonnables | — |

**Réfutabilité des tests.** Les mutants que j'ai posés sur le binaire (minimum, plafond, constantes, signe, sons, animations, seuils du répartiteur) font tous échouer le rejeu des vecteurs. Sans les vecteurs, les clés pinnent déjà plafond, arrondi impair, anneaux, forces et tirages. Elles ne pinnent pas le minimum 10 (C-6).

---

## 6. Corrections récapitulatives à appliquer au plan et aux annexes

1. **§1.30.5, T1 et D-H1-7** : retirer les 15 vecteurs « taille de table » des vecteurs rejoués par l'adaptateur (C-1). Le chemin d'erreur de `UseItem` est couvert par `AlundraItemAndMoneyOpcodesTests.cs:126-129`.
2. **Fait du répartiteur** : « `s0` est l'id d'avant promotion ; `stats+0xC` reçoit l'id promu ». (p7) : `0x2A` sans `0x26` (C-2).
3. **Preuves de retrait** : récrire les ensembles rouges (C-3).
4. **Clés** : renommer `spotion_two_uses` ; ajouter `herb_two_uses`, `spotion_hp3of19`, une clé « marche » (relation « X de l'effet = X du début du tick »), deux clés d'eau profonde ; `herb_circle_held_30` par `HoldDirections` (C-4, C-5, C-6, C-9, C-10).
5. **T3** : retirer ou spécifier la famille de mutants « ordre » (C-7).
6. **HeroHeals** : décider du sixième soin et de `a0` ≠ héros (C-8).
7. **Plan §2** : O-H-3 → E18 (si l'auteur suit Q-H1-2). D-H-3 : lire `0x24, 0x25, 0x26, 0x27, 0x29`. Ajouter le point ouvert « héros touché au même tick » (C-11).
8. **Annexes** : `h-g-deps.md` §2.1 (2 tirages par étincelle ; 2 trous dans la table) ; `test-means.md` §7 (rangées 39 et 41) ; écarts de décompilation (C-12).
9. **Sonde P5** : assertion sur les ids 48, 49, 50 du manifeste (C-13).
10. **Enveloppe** : ajouter la note d'ordre du §1.20, point 4 (C-16).

---

## 7. Limites et INCONNU

- **Comportement de l'effet 14 après sa création** (programme, rendu) dans la DLL : non mesuré. Le modèle de réservoir de la DLL (`fxpool.py` d'E19.g) n'est pas revérifié ici.
- **États d'échelle et de grimpe dans leur contexte** : non mesurés (g1a).
- **États de portage avec `+0x2C` réellement posé** : mes mesures et celles de la découverte posent `+0x2C = 0`. Le départ de l'Herbe ne lit pas `+0x2C` (FAIT-C), mais le gestionnaire d'état peut avoir un chemin différent avec un objet porté (g1a).
- **Intention du geste Start + Select** et **rôle de C255**, **lien de `0x80041B14` avec l'opcode `0xBB`** : INFÉRENCE.
- Mon désassemblage couvre les fonctions du périmètre. Je n'ai pas rejoué l'ensemble de `0x8002BAEC` en mémoire hors des 31 vecteurs de la découverte.

---

## 8. Fichiers (`C/`)

- **Scripts** : `c_exe.py`, `c_lst.py`, `c_dump.py`, `c_xref.py`, `c_cpu.py`, `c_replay.py`, `c_replay_core.py`, `c_binmut.py`, `c_keycheck.py`, `c_reach.py`, `c_promo.py`, `c_swim.py`, `c_swim2.py`, `c_move.py`.
- `lst/` : mes 17 listings.
- `out/` : `replay_vec_out.txt`, `replay_vec_ss_out.txt`, `binmut_out.txt`, `keycheck_out.txt`, `reach_out.txt`, `promo_out.txt`, `coverage_useitem_out.txt`, `swim_out.txt`, `move_out.txt`.
- `rerun/` : copies de leur harnais (`oracle_copy` avec `runA/` et `runB/` ; `oracle_full` avec la sortie complète de `run_all.sh`).
- Les scripts `c_swim*.py` et `c_move.py` importent les modules `h4o` de `rerun/oracle_copy`.