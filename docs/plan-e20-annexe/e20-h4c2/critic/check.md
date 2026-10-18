# Critique adverse de la découverte E20.h4c2 (terre 2, objet 0x2C, sprite 17, E13, annulation 0x80034BDC, coup de caméra)

Je n'ai pas écrit `report.md` ni `check.md` : la consigne système interdit d'écrire des fichiers de rapport. Ce message est donc le rapport complet.

Dossier de sortie : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c2-critic/`

- Je n'ai écrit que sous ce dossier.
- Rien n'a été écrit dans le dépôt, ni build, ni test lancé.
- Tous les scripts Python ont tourné avec `-B`. Ceux de l'annexe ont tourné depuis des copies (`work/oracle`).
- HEAD `023b33ce`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58` recalculé. Le binaire prime sur la décompilation, que je n'ai pas corrigée.
- Légende : FAIT (vu dans le code, le binaire ou les données), INFÉRENCE (déduit, de quoi dit), INCONNU. Verdicts : CONFIRMÉ / RÉFUTÉ / NON ÉTABLI. Mon verdict prime là où il diffère.

## 0. Verdict

1. **Tous les faits de comportement du binaire de la découverte tiennent.** Je les ai refaits avec mon propre interpréteur MIPS et mes propres modèles, écrits depuis mon désassemblage (`tools/myemu.py`, `tools/t_*.py`).
   - Je n'ai pas réutilisé `machine.py` de h4 ni `cmips.py` de h4b.
   - Aucun fait de comportement n'est réfuté.
2. **L'artefact des emplacements (+0 = i) et le pas par image du générateur sont CONFIRMÉS.**
   - Je les ai relus au binaire et rejoués avec d'autres graines et d'autres positions du héros.
   - J'ai aussi rejoué les autres tomes. L'eau 2 (0x2E) est touchée elle aussi : fin d'objet 394 → 393 (la découverte ne l'avait pas rejoué).
3. **Quatre défauts de la proposition, non-binaires** (§4 et §5) :
   - Le test du terrain (comparaison stricte du pilote) n'est pas atteignable à l'unité brute par un champ de collision. Les mutants « `<=` » et « terrain sans +1 » ne sont donc pas tués par ce que le plan décrit.
   - Le coup de caméra n'est pas un « sursaut de 2 px qui revient ». Hors du cas où l'écart caméra/cible est nul, il laisse un décalage permanent de 1 à 2 px, qui s'accumule rocher après rocher jusqu'à résorber l'écart (jusqu'à 15 px). Le texte de Q-H4C2-1, la recette et le test pur (k1) sont à corriger.
   - La preuve de retrait de la phase B est incohérente avec la définition des tests de monde : (w6) manque dans la liste des rouges, et le banc de (w1) à (w7) est ambigu (production ou coutures).
   - (w1) lie toute l'acceptation à un rocher de bord de carte (héros en (100,100)) dont le comportement en DLL est une sonde non faite.
4. **Dépendances :** h0a, h4a, f5 (D-F5-5) CONFIRMÉ. Ni f6a pour le comportement, ni h4b1/h4b1b/h4b2. Le témoin de (w6) demande pourtant f6a ou une entité à gravité.
5. **Verdict sur les tâches :** T0 à T3 acceptables avec les amendements du §6. Pas de blocage de fond.
   - À corriger avant exécution : les points 2 et 3 ci-dessus.
   - Quatre clés manquantes sont fournies dans `keys/` (§3.2).

## 1. Ce que j'ai fait (preuves rejouables)

| Preuve | Résultat |
|---|---|
| Désassemblage capstone relu par moi : pilote `0x800354D0`, garde `0x800351B8`, enveloppe `0x8003634C`, tables `0x800235F4` et `0x800233EC`, E13 `0x8007F30C`, E3 `0x8007EDA0`, annulation `0x80034BDC`/`0x800350C0`, `0x8003A59C`/`0x8003A774`, `0x80032B90`, `0x800370C4`, création `0x80039D04`, passe de slots `0x80038634`, passe d'événements `0x800386D0`, chargement de carte `0x8003B24C`, pas de caméra `0x8002CDA0`, boucle principale `0x8002C3F4`, fonction de trame `0x8002BAEC` | voir §2 |
| Mon interpréteur + mon modèle du pilote, 8 000 états aléatoires (deux graines) | 0 écart. Couverture 176/176 instructions du pilote, annulation réelle incluse |
| Enveloppe (1 500 états), annulation seule (2 000), E13 (896 combinaisons) | 0 écart. L'annulation n'écrit que `+0x10`, `+0x20`, `+0x88` |
| Rejeu des **609 cas** de `h4c2/keys/driver_2c_cases.json` sur mon interpréteur (`tools/t_replay_cases.py`) | 609/609 identiques : journal, générateur, phase, compteur, objet, animation du héros, statuts et ForceZ, annulations |
| Rejeu des vecteurs h4b sur mon interpréteur (`tools/t_replay_vec.py`) | `vec_camera` 100/100, `vec_sway` 80/80, `vec_e13` 40/40, bit à bit. Pile en `0x801FFF00` comme les vecteurs |
| Balayage de l'EXE pour `[0x800DD874]` | 4 références : E13 `sw` en `0x8007F350` (le seul écrivain non nul), deux remises à zéro (`0x8002CD7C`, `0x8002CE98`), un lecteur (`0x8002CE6C`) |
| Balayage de l'EXE pour `[0x800DD870]` | 3 références, toutes des zéros ou la lecture du pas de caméra : le coup X n'a pas d'écrivain |
| Rejeu de `cast_rich.py --pf` | md5 `16bc521d3d47cba4ebf2f7187aba87f7`, identique à `cast_2c_pf.json` |

## 2. Faits re-dérivés

### 2.1 Pilote, garde, enveloppe

**Verdict : CONFIRMÉ.**

- **Table** `0x80023504`, lue dans l'EXE : 15 entrées (tick, p). Ticks 16 à 192 par pas de 16, puis 224, 224, 240. p = 512 à 7680 par pas de 512. Puis la sentinelle `0xFFFF`.
- **Parcours.** On reparcourt la table depuis son début tant que `compteur >= tick`. Une entrée n'agit que si `tick == compteur`.
  - Précision : la phase passe à 1 dès que **toutes** les entrées ont `tick <= compteur`, donc pour tout compteur ≥ 240 en phase 0. Elle passe à 1 dans le même appel qui traite l'entrée 240.
- **Deux tirages par rocher** (r1 puis r2), chacun `état' >> 27`, faits avant l'appel de naissance quel qu'en soit le résultat. `EnemyHelpers.Below(32)` (`Ai/EnemyHelpers.cs:35`) est la même formule.
- **Naissance** par `SpawnWarpEntity`, avec :
  - parent = héros, indicateur 0, indice `0x11` ;
  - position = héros + `X32[r1]`, `Y32[r1]` × (p + 0xA00) ;
  - Z = Z brut du héros + `0x1000000` ;
  - direction = r2.
- Les tables `0x80023654` et `0x80023694` lues dans l'EXE sont identiques à `Entities/AnimationTables.cs:71` et `:82`.
- **Après la naissance.** Pas de rocher : rien d'autre. Sinon, comparaison signée stricte `Z_héros_brut + 0x1000000 < [rocher+0x138]` :
  - vrai : `DestroyEntity` à un argument ;
  - faux (égalité comprise) : ForceZ `0xFFF80000`.
- **Phase 1** : garde, puis `compteur < 0x118` rend 0. Sinon `hero+0x88 = 0x34`, phase 2, rend 0.
- **Phase 2** : rend 0 tant que `hero+0x88 == 0x34`, 1 sinon. Toute autre phase rend 0.
- **Garde** : pente 4 → annulation et 1 ; sinon animation 0x32 ou 0x33 → 0 ; sinon annulation et 1. Elle n'est appelée qu'aux phases 0 et 1.
- **Enveloppe** : compteur incrémenté APRÈS le pilote, saturé à `0x7FFFFFFF`. L'objet est remis à 0 si le pilote rend non nul.
- Pour les objets 0x1F à 0x32 non portés, l'enveloppe rend 1 et libère l'objet (confirmé par exécution).

### 2.2 E13

**Verdict : CONFIRMÉ.**

- Si `+0x88 == 1` : détruit si `+0xAC == 1` (`DestroyEntity(self,-1)`), sinon rien. Pas de coup, pas de changement de drapeaux.
- Sinon : `[0x800DD874] = 2` (`sw`, assignation), `+0x88 = 1`, `+0x6C = (+0x6C | 0x40) & ~0x30`.
- E3 diffère : il teste les deux conditions ensemble et refait son `else` à chaque tick. Il n'y a pas de coup de caméra dans E3.
- Table maître : E (slot 4) code 13 = `0x8007F30C`. Code 3 = `0x8007EDA0`.

### 2.3 Annulation

**Verdict : CONFIRMÉ.**

- Table `0x800233EC` : 0x2C → `0x80035130` → `0x80034BDC`. 0x2B n'a pas d'annulation propre.
- Boucle des emplacements 1 à `[0x80134360]` **inclus**. Type `+0x68 == 0x11` et statut 2 → statut 3, `+0x20 = 0`, `+0x88 = 1`. Rien d'autre.
- Elle équivaut à « toutes les entités vivantes » : la création relève le compte à `[ent+0]`.

### 2.4 Caméra et calendrier

**Verdict : CONFIRMÉ**, avec une réserve sur la « vie d'un coup » (§4.3).

- Pas de caméra :
  - `scrollY += ((cibleY − (scrollY+0x88)) >> 4) + OffsetY(sway,+0x18) + coup`.
  - Le coup est remis à 0 à chaque pas (`0x8002CE98`).
  - Au premier appel après un chargement (drapeau `[0x800E42B8]`), le coup est effacé sans être appliqué (`0x8002CDF4-0x8002CE0C`).
- Boucle principale : RenderScene (donc la caméra, `0x8002BDB8`) puis Update(0) (`0x8002BAEC`, l'entité block, puis le pas du générateur). Le coup du tick N est consommé par la caméra de l'itération suivante.
- La fonction `0x8002BAEC` n'a aucune sortie anticipée avant `0x8002BD0C` : le pas du générateur a lieu à chaque appel.
- Dans la DLL, `ComputeSmoothedCameraTarget` ajoute `(swayX, −swayY, 0)` avant le clamp (`CameraMath.cs:223`). Le signe est cohérent (INFÉRENCE appuyée par la doc de `StepCameraScroll`).

### 2.5 Artefact des emplacements et pas du générateur

**Verdict : CONFIRMÉ** (lecture du binaire et rejeu).

- Chargement de carte (`0x8003B24C-0x8003B2CC`) : copie du gabarit dans les 64 cases **et** `+0 = i`.
- Création : relève `[0x80134360]` à `[ent+0]` s'il est plus grand (`0x80039D24-3C`).
- Le banc de h4 fait `m.zero(ENT0, 64*STRIDE)`. Une case jamais utilisée garde `+0 = 0`, donc le nouveau-né n'est pas visité au tick de sa naissance.
- **Rejeu avec héros en (600,480) et graine `0xCAFEBABE`** :
  - avec `+0 = i`, les 15 rochers font st3 = b+33, sol = b+32, `+0xAC` = b+79, st4 = b+80 ;
  - sans `+0 = i`, les 6 premiers sont à b+34, z0 = 256, statut 1 à la naissance.
- **Autres tomes**, avec puis sans `--slotinit` : 0x2D 344 → 343 ; 0x2F 150 → 149 (boules 12 → 11) ; 0x30 183 → 182 ; 0x32 243 → 242 ; 0x2B médiane 31 → 30 ; 0x31 38-39 → 38. **Nouveau :** 0x2E 394 → 393.
- Générateur : l'écart de tirages par tick vaut 2 × naissances à chaque tick (aucun tirage de destruction ni de butin), et `rng0(t+1) = pas(rng1(t))`. Aucun écart sur 400 ticks.

### 2.6 Chronologie d'un rocher

**Verdict : CONFIRMÉ.**

- Clé reproduite à l'octet. J'ai ajouté la clé « milieu de carte » (§3.2).
- `Alundra/Physics/ScriptedMotion.cs:604-640` : `Acceleration & 0xf` = 0 pour l'accélération 64, donc la vitesse est atteinte tout de suite, comme dans la clé (x + 113 600 par tick).

### 2.7 Autres faits

| Fait | Verdict |
|---|---|
| Sprite 17 : drapeaux `0x132014` (sans `Gravity`), animations 0 / 1 / 2 (vitesses 160 / 0 / 160, sons 55 / 45 / 0), 16 / 23 / 72 images, Chain→2 / Hold / Loop, attaque (8, 64) | CONFIRMÉ (`sprite-records.json`, `balance.json`) |
| Boîte d'attaque 72×48×39 en (−1, 0, 3,5), identique aux 12 combinaisons animation × direction | CONFIRMÉ (12 fichiers `.anim2d`) |
| Les quatre directions du sprite 17 ont les mêmes images et les mêmes fins | CONFIRMÉ. Donc O-E0A5-4 (direction de création non cardinale, ligne d'animation lue sur l'ancienne case) est sans effet pour ce sprite. La découverte ne le disait pas |
| Le sprite 17 n'est pas tué par l'eau profonde | CONFIRMÉ : chronologie des 15 rochers identique avec une bande d'eau (`cast_rich_water.py`) |
| A0 du sprite 17 | **Nouveau.** `A[0] = 0x8006174C` écrit seulement `+0x280 = PosZ>>16` ; C0, D0 et F0 sont nuls ; E13 ne lit pas `+0x280`. Ne pas inscrire A0 est fidèle, comme D-F3-7 pour la vague |
| Appel du pilote au tick qui suit le départ, compteur 0 ; sort mesuré 0x34 au tick 282, retour à 0 au 308, objet libre au 309 | CONFIRMÉ (rejeu) |
| `SpriteType` : 17 pour la page globale, 0x100 + indice pour une page de carte | CONFIRMÉ (`EntityScriptProxy.cs:132-141`) |
| Deux seuls en-têtes avec E13 : `alundra_17` et le sprite de carte 213 | CONFIRMÉ (`sprite-records.json`) |

## 3. Les clés d'oracle

### 3.1 Existence et adéquation

| Clé | Existe | Adéquate au banc | Remarque |
|---|---|---|---|
| `cast_2c_pf.json` | oui, reproduite à l'octet | oui pour (w2) (rocher de bord) | héros en (100,100) : 1 rocher de bord sur 15 |
| `targets_2c_pf_inv0.json` | oui | NON ÉTABLI | voir §5.4 |
| `cancel_2c_pf_t100`/`t60` | oui | oui | sans les sons (fournis ci-dessous) |
| `driver_2c_cases.json` | oui, 609/609 reproduits sur mon interpréteur | oui avec un banc d'hôte factice | le terrain `eq` ne se fabrique pas avec un champ de collision (§5.1) |
| h4b `vec_e13`, `vec_camera`, `vec_sway` | oui | oui | adaptateur à écrire, comme prévu au T0 (d) |

### 3.2 Clés que j'ai ajoutées (dans `keys/`, `README.txt` et `MANIFEST.md5`)

- **`cast_2c_pf_mid_x600y480.json`** : héros en (600,480), graine standard. 15 rochers intérieurs, aucun rocher de bord, 14 ticks de coup. Elle découple l'acceptation principale du cas du bord.
- **`cast_2c_pf_plateau_hb16.json` et `_hb17.json`** : plateau pour les colonnes ≥ 7, héros en (100,100).
  - La hauteur de terrain vaut octet de hauteur × 16 px (16 → 256 px, 17 → 272 px). Cela répond à l'INCONNU « hauteur de case qui donne un terrain à plus de 256 px ».
  - Octet 16 (terrain = 256 px) : les rochers 9, 13, 14, nés sur le plateau, sont **gardés**. Ils sont au sol à b, en statut 3 à b+1, en statut 4 à b+48.
  - Octet 17 (272 px) : ces trois rochers sont **détruits sur place** (statut 4 au tick de naissance, son 55 joué, pas de son 45, pas de coup). Les tirages sont faits quand même, et les rochers suivants sont identiques à la clé sèche.
  - Le rocher 8 bute contre le mur du plateau **en l'air** : drapeau à 142 (b+28), statut 3 à 143 (b+29), sol à 146, statut 4 à 190 (b+76). C'est la clé « rocher qui bute contre un mur » qui manquait.
  - Un octet 16 ou 17 ne permet jamais l'égalité des deux côtés de la comparaison, puisque le Z du héros est un multiple de 16 px plus 1 unité brute.
- **`cancel_2c_pf_t100_sounds.txt` et `_t60_sounds.txt`** : l'annulation fait jouer le son 23 du héros et **un son 45 par rocher annulé** au tick de l'annulation. La clé de la découverte ne les contenait pas.

## 4. Ce qui est réfuté ou corrigé

### 4.1 « Terrain atteignable » : NON ÉTABLI

Le plan lit `ComputeTerrainHeight()` du nouveau-né.

- La hauteur d'un champ vient d'un `float` (`TerrainProbe.cs:36`).
- À 256 px, l'espacement du float32 vaut 2 unités brutes. Le terrain `0x1000001` ou `0x1000002 − 1` ne se représente pas.
- Le cas `eq` des cas du pilote (terrain = Z brut du héros + `0x1000000`, soit 1 unité au-dessus d'un multiple de 16 px) est inatteignable par un champ, et inatteignable en jeu.
- Pour tuer `<=` et « terrain sans +1 », il faut une couture ou une fonction pure (`ShouldDestroy(heroPosZ, terrain)`), à nommer dans la conception. Sinon ces deux mutants sont équivalents dans tout monde atteignable (§5.1).

### 4.2 Incohérence de la preuve de retrait

- « Retirer l'appel `Register` rend rouges exactement (w1) à (w5), (w7), (r1) et les deux épingles ». **(w6) manque** : sa définition est « chronologie identique à (w1) », donc elle est rouge aussi, sauf si elle ne compare que la survie.
- (w1) à (w5) rouges sans `Register` signifie qu'ils utilisent le registre de production. Alors (w7), « la même chose sans registre de test ni couture », est redondant. Si au contraire (w1) à (w6) utilisent des coutures, ils sont verts en phase A, ce qui contredit « rouges avant la phase B ». Il faut fixer le banc de chaque test.

### 4.3 La vie d'un coup de caméra (§1.6 et Q-H4C2-1)

**Verdict : RÉFUTÉ en généralité.**

- `[302, 301, 300, 300…]` n'est vrai que si l'écart au repos est exactement 0 (cible = défilement + 0x88).
- Pour un écart au repos d, le coup +2 laisse un décalage **permanent** : d = 1 → +1 ; d ≥ 2 → +2.
- Les coups suivants s'accumulent. Simulation avec 14 coups espacés de 16 ticks :

  | Écart d au repos | Décalage cumulé après chaque coup |
  |---|---|
  | 5 | 2, 4, 5, 5… |
  | 12 | 2, 4, 6, 8, 10, 12, 12… |
  | 15 | jusqu'à 15 |

- Donc la caméra dérive de d pixels sur le sort, par pas de 2 px, avant de ne plus bouger.
- Le texte de Q-H4C2-1 (« décale l'image de 2 pixels pendant un tick, puis la ramène en deux ticks ») et la recette sont faux hors écart nul.

## 5. Tests proposés, mutants, manques

### 5.1 Réfutabilité par test

| Test | Verdict |
|---|---|
| (t1) table lue dans l'EXE | réfutable (précédent `AlundraHeroRunTablesTests.cs:42`) |
| (t2) 609 cas, un test | réfutable pour tout sauf le terrain. Les cas ont 2 positions de héros avec x ≠ y (273 cas), 6 graines, 3 `lt` et 2 `eq`. Le terrain exige la couture du §4.1 |
| (t3) à (t6), (e1) à (e4), (c1) | réfutables avec ce qui précède |
| (r1) registre | réfutable |
| (k1) pur | **insuffisant**, voir §6 : manquent les écarts d = 1, 2, 15, l'accumulation de coups successifs, et un cas de calage avec un coup à l'indice ≥ 1 (décalage d'indice dû au tick de calage de `AdvanceCameraSmoothing`) |
| (k2) monde | à préciser. Il ne reste vert sans `Register` que s'il appelle `SetCameraKick` sans passer par les rochers |
| (w1) | réfutable, mais voir ci-dessous et §5.3 |
| (w2) | dépend de la sonde (s3) : création hors grille (x = −20 px). `ClampAxisToMapEdge` (`ScriptedMotion.cs:~720`) ramène bien une entité déjà hors borne, mais la création peut déjà modifier la position |
| (w3) | réfutable à 1 tick par image |
| (w4) | réfutable, mais sans les sons 45 et 23 de l'annulation (clés fournies) |
| (w5) | **fragile**, voir §5.4 |
| (w6) | le témoin « type à gravité qui meurt » n'existe en DLL qu'avec f6a |

### 5.2 Mutants

- **Tués** par le plan tel que décrit : r1/r2 échangés, direction r1, `p` sans 0xA00, X32/Y32 échangés, Z +128 px, ForceZ −4 px, naissance nulle qui détruit, entrée 224 décalée, un seul tirage, garde absente en phase 1, seuil 0x119, phase 2, phase 1 non enchaînée, tous les mutants d'E13, d'annulation (sauf le suivant), de caméra et de registre.
- **Équivalents sans couture** : `<=` et « terrain lu sans le +1 » (§4.1).
- **Mal défini** : « tirage après la naissance ». r2 est un argument de la naissance, donc ce mutant ne se construit pas tel quel.
- **À ajouter** : le décalage d'indice du calage, la suppression de la règle D-F5-5, l'oubli de la cible de vitesse au tick d'E13 (tué par (w1) si la comparaison est tick par tick).
- **Équivalent confirmé** : `ResyncControllerFromFlags` d'E13. Il ne lit que `ClassA`, `ClassB`, `Gravity` (`CellsCollisionField.cs:147-162`).

### 5.3 Manques

1. Clé milieu de carte pour (w1) (fournie), le bord restant à (w2).
2. Test d'un rocher qui bute contre un mur ou une marche : clé `plateau_hb16/17` (fournie). Cela arrive dès que le héros est à moins de 120 × 80 px d'un mur, pas seulement au bord de la grille.
3. Tests de destruction sur place avec plateau (octets 16 et 17), pour tuer `<` et la lecture du terrain par un chemin de monde.
4. Sons de l'annulation dans (w4).
5. Garde de contexte nul : `EventProgramRunner(null, …)` donne un `_worldContext` nul (`AlundraNativeSpriteRegistryTests.cs:40`). E13 doit tolérer un contexte nul.
6. Banc non piloté (`DrivesEntityTicks` faux) : E13 y tourne hors `RunEntityBlock` (`WorldProxy.cs:2655`), donc l'indice du tick posé en tête de `RunEntityBlock` est périmé. À spécifier.
7. Q-H4C2-1 et la recette à corriger (§4.3).

### 5.4 (w5) et le couplage du générateur

La passe de coups tire dans le générateur à chaque touche : trois étincelles, par `EffectStage`. Le nombre de touches décale les tirages des rochers suivants.

- Preuve dans les chiffres du rapport : sans artefact et sans pas par image, les touches des six cibles valent (7, 9, 0, 6, 9, 6). Avec les corrections, elles valent (10, 6, 7, 6, 3, 6).
- Un seul écart de touche fait diverger tout le reste. Le test est un oracle de bout en bout correct, mais diagnostiquable seulement si une sonde (s8) précède : un rocher qui touche une cible, comparaison du nombre de pas du générateur par touche.
- La coutille d'observation des touches sur le banc plat n'est pas spécifiée. `HitArena` est bâti sur la carte 14, pas sur le champ synthétique. NON ÉTABLI.

## 6. Verdict sur les tâches

**T0** : accepté, avec :
- la sonde (s2) à remplacer par la hauteur d'octet de hauteur × 16 px (réponse fournie) ;
- (s3) à garder (création hors grille) ;
- (s8) à ajouter : tirages du générateur par touche ;
- la décision sur la couture de terrain à prendre ici.

**T1** :
- Ajouter la couture ou la fonction pure du terrain à la conception de `RockFallAi`.
- Compléter (k1) avec des écarts au repos d = 1, 2, 15, l'accumulation de plusieurs coups, et le cas calage avec indice décalé.
- Garder le contexte nul et le banc non piloté dans (e2), (k2).

**T2** :
- (w1) sur la clé milieu de carte ; (w2) sur la clé de bord actuelle, qui garde la sonde (s3).
- Ajouter (w8) sur les clés plateau (octets 16 et 17 : gardés / détruits ; mur).
- Fixer le banc de chaque test (production ou coutures), puis corriger la liste des rouges sans `Register` (ajouter (w6) et (w8)).
- (w5) en dernier et non bloquant, derrière (s8).

**T3** : les 30 mutants se réduisent à 28 tués plus 2 à reformuler. Les deux revues fraîches sont maintenues.

**Prérequis** : h0a, h4a, f5. Ils sont déjà impliqués par l'ordre ferme f4, f5, f6a, f6b, puis h. La ligne h4c2 du tableau du §1.31 (prérequis « h4a ») est à compléter.

## 7. Questions pour l'auteur (reformulées)

- **Q-H4C2-1** : le sursaut n'est pas « 2 px puis retour ». La caméra dérive vers sa cible par pas de 2 px à chaque rocher, jusqu'à résorber l'écart au repos (0 à 15 px), puis oscille de 2 px avec retour. Porter (défaut) ou ignorer.
- **Q-H4C2-2** : la 321 ne contient aucun octet 0x2D, 0x8A ou 0x8B dans ses 152 octets de code (FAIT). Son enregistrement 1 (sprite de carte 213) a `SpriteDirection` 128 (bit 0x40 clair) : il n'est jamais créé au chargement (FAIT). Il naît donc d'un gestionnaire natif de la 321, probablement un boss (INFÉRENCE). L'inscription de `(E,13)` pour 469 relève de la tranche de ce boss en E14. Prévoir la clé commune, comme E3 (Q-H4-8).
- **Q-H4C2-3** : le rocher qui bute contre un mur ou le bord éclate en l'air à 240 px, avec son et coup. Ce n'est pas rare. Le défaut « fidèle » est à arbitrer avec la règle de l'auteur du 2026-09-25 (un défaut avéré se corrige). Clé fournie.
- **Q-H4C2-4** : refaire les clés de h4 de tous les tomes. Le périmètre est plus large que dit : l'eau 2 (0x2E) est touchée aussi.

## 8. INCONNU restant

- Comportement de la DLL pour une création hors grille et pour un rocher qui bute contre un mur (sondes (s3), (w8)).
- Observation des touches sur le banc plat (§5.4).
- Rendu en jeu du coup de caméra (non vu).
- Le gestionnaire natif exact de la 321.

## 9. Fichiers

Tout est sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c2-critic/` :

- `tools/` : `mydis.py`, `myemu.py` (mon interpréteur), `scanref.py`, `t_driver.py`, `t_e13.py`, `t_replay_cases.py`, `t_replay_vec.py`, `cast_rich.py`, `my_plateau.py`, `my_cancel_snd.py`.
- `keys/` : les clés du §3.2, `README.txt`, `MANIFEST.md5`.
- `out/` : sorties des rejeux.
- `work/oracle/` : copie de travail du harnais.