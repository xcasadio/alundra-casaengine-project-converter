Le rapport `report.md` n'a pas pu être écrit : l'outil d'écriture refuse les fichiers de rapport, comme pour h4. Le rapport complet est donc ce message. Les scripts, listings et sorties sont dans `H3/`, disposition et commandes dans `H3/README.md`.

`H3` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h/h3bin/`

# Rapport h3bin : la bombe (objet 0x23) et les graines (objet 0x1F), lues dans ALUN_CD.EXE (France)

Lecture seule. Rien n'a été écrit dans le dépôt (lu à `8887a488`), dans `data-extracted/`, dans `alundra-project/` ni dans le binaire (SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`).

**Étiquettes.**
- FAIT : vu dans le binaire, les données ou le code, avec adresse ou fichier.
- INFÉRENCE : déduit, avec la source.
- INCONNU : non établi.

**Preuves.**
- [L] : lu dans le désassemblage (capstone).
- [V] : comparé au vrai code MIPS sur vecteurs aléatoires (`H3/run/h3_vec.py`, mémoire comparée octet par octet).
- [M] : mesuré dans le jeu émulé. MovePlayer, `SpawnWarpEntity`, passes d'entités, animation, physique, passe de coups et cadre d'usage d'objet sont réels. Seuls les sons et les appels d'effets sont journalisés.
- [D] : donnée (cartes extraites, `BALANCE.BIN.json`, `map_alundra.json`).
- [DLL] : code du dépôt.
- [DEC] : décompilation, lue et jamais corrigée.

Le binaire tranche. Les positions sont en pixels (brut 16.16 divisé par 65536). `T` est le tick de l'appui sur Rond, `Ts` celui du Carré. `H` est le héros (`0x80127D30`), `E` une entité (`0x80127D30 + 0x294·k`).

## 0. Ce qui change le plan

1. **Ni la bombe ni les graines ne coûtent ni ne se consomment.** FAIT [L].
   - Les deux départs n'appellent que `SpawnWarpEntity` (`0x80039F58`) et le son (`0x800490FC`). Aucun coût en PM (`0x8004DF3C`), aucune consommation (`0x8004E5C4`).
   - Octet 7 de la fiche `0x800C5F7C + 8·id` : 1 pour `0x1F`, `0x23` et `0x2B`, 9 pour la plante `0x24`. INFÉRENCE : c'est le compte maximal.
   - Seule limite : 3 bombes à la fois.
2. **Le départ ne regarde ni l'animation du héros ni les PM.** FAIT [L][V].
   - Refus si le héros porte déjà quelque chose, si sa pente vaut 4, si un objet est « en cours » (`[0x80127164] != 0`), ou, pour la bombe, s'il y a déjà 3 entités de sprite 2.
   - Départ possible en plein saut : l'objet naît avec l'animation `0x0B` [M].
   - Liste des états au §2.5.
3. **h3 n'exige ni g1b, ni g2b, ni g3.** FAIT [L][M].
   - C62, C73, E21, D30 et E12 ne lisent que `+0x24`, `+0x28`, `+0x2C` : le lien de portage et le mode de lâcher écrits par `UpdatePlayerCarriedEntity` et le Carré. Jamais `TryLift`.
   - La bombe posée n'est pas relevable. Ses indicateurs `0x13234E` n'ont pas le bit Collidable `0x80` : le héros la traverse, et un Carré devant elle donne un coup d'épée [M, `results/scenarios/bomb_beans_relift.txt`].
   - Prérequis réels : **g1a** (noyau `PutInHands`, modes 0/1/2, états `0x05/0x0B/0x0C…`, effacement du lien) et **g2a** (Carré qui lance). C'est la proposition D-G-1, que l'enveloppe n'a pas appliquée.
4. **La bombe explose seule, même dans les mains.** FAIT [L][M].
   - À la fin de la fusée (241 décrémentations), C62 passe en statut 3 et efface `H+0x2C` et `E+0x28`.
   - La bombe tombe de ses 32 px et explose : statut 3 à `T+242`, animation d'explosion et son 46 à `T+243`.
5. **L'explosion frappe les classes A et B.** FAIT [L][M].
   - Indicateurs `0x4E` : ClassB `0x08`, HitsClassA `0x04`, HitsClassB `0x02`, DeactivateOnAnimationEnd `0x40`.
   - Attaque unique : attribut 4, puissance 5. Dégâts = `max(1, (5·r) >> 4)`, `r` étant la réponse de la cible au type 4. Réponse `0x40` : PV à 0. Réponse `0x80` : rien.
   - Vrai `ComputeNewHp` sur les 147 fiches de monstres (`results/bomb_damage_table.txt`) : 74 prennent 5, **45 meurent d'un coup**, 18 ne sont pas touchées, 6 prennent 1, 2 prennent 3, 1 prend 9, 1 prend 10.
   - Le héros du kit de départ perd **5 PV sur 10** dans la zone [M].
   - Une bombe touchée par un attribut 4, 6 ou 10 explose (chaîne, D30) [M].
6. **La bombe ne casse rien elle-même.** FAIT [L][D].
   - Murs et rochers réagissent par des programmes de carte (`0x87` et `0x95` avec l'indice de type 3, soit l'attribut 4) ; la seule autre réaction native est D32 (racine, sprite global 253).
   - E12 appelle `CheckAndTriggerTileEffect` (`0x8003A7B0`, en `0x8007F2D4`) à chaque tick d'explosion. L'herbe est coupée pendant les 15 ticks où une boîte existe [M] : 8 cases sur 25 dans le test.
7. **Les graines ne sont pas une attaque.** FAIT [D][M].
   - Fiche sans attaque (`AttackCount` 0), animations sans boîte de coup.
   - Leur seul usage est de réveiller le « Pot magique » (type `0x199`, sprite de carte 153) en tombant dessus (E21 `0x8007F7CC`). Sinon elles éclatent 3 ticks après l'atterrissage avec l'effet 8.
   - Le pot est un tremplin natif (C74 `0x8007BB9C`, A20 `0x800619DC`). 39 pots sur 16 cartes, **aucune sur la chaîne de l'histoire**. C74 et A20 ne sont dans aucune tranche du plan.
8. **Menu Start refusé tant que l'objet est en main** (`[0x80127164] != 0`, garde `0x8002BC90`). FAIT [L].
   - L'état « en cours » dure 243 ticks au plus pour la bombe [M], sans limite pour les graines (aucun minuteur).
9. **La décompilation est fidèle sur les gestionnaires.**
   - 0 écart de comportement sur C62, D30, E12, C73, E21 et les deux départs.
   - Exception : les deux annulations testent `+0x20` dans la décompilation, `+0x24` dans le binaire (D1, §8). L'effet se voit à la mort du héros.
10. **Dans l'histoire actuelle, seule la carte 10 donne la bombe** (`0x82 0x23`, programme de chargement B[8], octet 995). Aucune carte de la chaîne ne donne les graines (§9).

## 1. Cadre

- **Monde.** Le jeu émulé est le monde de `docs/plan-e20-annexe/e20-h4/work/oracle`, copié dans `H3/run/`.
  - Champ plat, sauf quand une grille de cases est écrite : herbe, mur, eau profonde.
  - Héros du kit de départ à 10 PV, pad scripté.
  - `ComputeNewHp` (`0x8003A374`) est appelée en vrai pour les cibles posées par le banc.
- **Fonctions lues** (adresse, instructions) :
  - Départ des graines `0x80034224` (63) ; départ de la bombe `0x8003453C` (81).
  - Petits pilotes `0x80035204` et `0x800352C4` (23 chacun) ; annulations `0x80034ACC` et `0x80034B54` (34 chacune) ; aiguillage `0x800350C0`.
  - C62 `0x8007A978` (186) ; D30 `0x8007E79C` (18) ; E12 `0x8007F27C` (36) ; C73 `0x8007BB30` (27) ; E21 `0x8007F7CC` (43).
  - Pot : C74 `0x8007BB9C` (124), A20 `0x800619DC` (3) ; racine D32 `0x8007E828` (17).
- **Listings** : `H3/run/lst/`.

## 2. Les départs

### 2.1 Appel et tables
FAIT [L].
- `TryUseItem` `0x8002ED64` appelle `UseItem` `0x8003499C`, qui saute par la table `0x8002339C` : `0x1F` vers `0x800349FC` (`jal 0x80034224`), `0x23` vers `0x80034A1C` (`jal 0x8003453C`).
- Le retour est testé en `0x80034A88`. S'il vaut 0 : `[0x80127164] = objet`, `[0x8012715C] = 0`, `[0x80127160] = 0`.
- Pilote : table `0x800235F4` (`0x1F` vers `0x8003638C`, `0x23` vers `0x800363BC`). Annulation : table `0x800233EC` (`0x1F` vers `0x80035100`, `0x23` vers `0x80035118`).

### 2.2 Départ des graines `0x80034224`
FAIT [L][V] : 63/63 instructions, 0 écart.
1. Échec (son 3, retour 1) si `H+0x2C != 0` (`0x80034238`), ou `H+0x18C == 4` (`0x80034248`), ou `[0x80127164] != 0` (`0x80034258`).
2. `SpawnWarpEntity(H, 0, 1, H.+0x114 ; pile : +0x10 = H.+0x118, +0x14 = H.+0x11C, +0x18 = 0)` (`0x80034264-0x80034288`).
   - `a1 = 0` choisit la page globale.
   - Résultat nul : retour 1 **sans son** (`0x80034290`).
3. Lien (`0x80034298-0x800342FC`) :
   - `H+0x2C = objet` ; `objet+0x28 = H`.
   - `H+0x88 = 5` si `H+0x144 != 0`, sinon `0x0B`.
   - `H+0x30/34/38 = objet.pos - H.pos` sur les trois axes.
   - Retour 0.
4. Rien ne teste l'animation du héros, les PM ou l'inventaire.

### 2.3 Départ de la bombe `0x8003453C`
FAIT [L][V] : 81/81 instructions.
1. Mêmes trois tests (`0x80034550-0x80034578`).
2. **Au plus 3 bombes** (`0x8003457C-0x800345B0`) :
   - Boucle sur les **64 emplacements** depuis le héros (pas `0x294`). Elle compte ceux dont `+0x68 == 2`, quel que soit leur statut ; `slti 3`, donc 3 ou plus donne un échec avec son 3.
   - Une bombe détruite compte jusqu'au recyclage de sa case (`T+296` pour la sienne). La bombe d'Olen de la carte 362 compte aussi.
   - [M] : le 4e départ est refusé (son 3 à `t178`, `results/scenarios/bomb_limit3.txt`).
3. `SpawnWarpEntity(H, 0, 2, …)`. Résultat nul : retour 1 sans son.
4. `objet+0x274 = 0xF0` (`0x800345F0-0x800345F4`, **après** la naissance), puis le même lien : `H+0x2C` en `0x800345F8`, `objet+0x28` en `0x800345FC`. Retour 0.

### 2.4 Ce qui naît
FAIT [M] (`results/scenarios/birth_state.txt`). Le départ est appelé seul ; il ne tire pas le générateur.

| | graines (sprite 1) | bombe (sprite 2) |
|---|---|---|
| `+0x68` | 1 | 2 |
| indicateurs `+0x6C` | `0x332380` : Collidable, Gravity, genre 1, sans glissade, ombre 3, `DestroyOnSlidingSlope`, `DestroyOnVramFlags` | `0x13234E` : ClassB, HitsClassA, HitsClassB, DeactivateOnAnimationEnd, Gravity, genre 1, sans glissade, ombre 3, `DestroyOnSlidingSlope` |
| codes A, C, D, E, F | 0, 73, 0, 21, 0 | 0, 62, 30, 12, 0 |
| fiche d'équilibre | `AttackCount` 0 | niveau 255, 1 attaque (attribut 4, puissance 5). Réponses 0x80 aux types 1 à 3, 5, 7 à 9, 11 ; 0x10 aux types 4, 6, 10 |
| boîte | 21×15×16, décalage (-10,-7,0) | idem |
| naissance | position du héros, statut 1 puis 2 | idem, `+0x274 = 240` |
| effet de bris (en-tête +0x1E) / contenu | 8 / 0 | 0 / 0 |

Animations ([D], `map_alundra.json`) :
- Bombe : anim 0 repos (4 images de 4 ticks) ; anim 1 explosion (51 ticks, son 46, boîtes aux images 4 à 10) ; anim 2 vol (vitesse 128, impulsion 1024, aucune boîte).
- Graines : anim 0 repos ; anim 1 une image d'un tick ; anim 2 vol (vitesse 128, impulsion 1024).

### 2.5 États du héros où Rond peut démarrer l'objet
FAIT [L]. Appels de `TryUseItem` recensés par `tools/scan_jal.py`, rapportés aux états par `run/lst/state_table.txt`.
- États : `00 01` ; `02 2B 2C 2D` ; `29` ; `2A` ; `0E 35` (échelle) ; `0B` ; `05` ; `22 23` ; `20 24` ; `1C 3E` ; `32 33` ; `21 31 34`.
- Sorties de nage qui rejoignent la voie d'Idle : `0F 1D 28 39 3C 4E 4F 3A 3B`.
- Dans les états de portage (`06 07 0C 2E 2F 30`) le départ est refusé (`H+0x2C != 0`).
- Pas depuis les états d'attaque, de blessure, de mort.

## 3. Pilotes et annulations

### 3.1 Petits pilotes `0x80035204` (graines) et `0x800352C4` (bombe)
FAIT [L][V] : 23/23 instructions chacun.
- Appelés chaque tick libre par l'enveloppe `0x8003634C` (en `0x80031CC4`) et sur le chemin de blessure (`0x80031C6C`).
- Rendent **1** (objet libéré) si `H+0x2C == 0` ou si `(H+0x2C).+0x68 != 1` (bombe : 2).
- Rendent **0** (en cours) si la pente n'est pas 4. Sinon ils appellent l'aiguillage `0x800350C0` et rendent 1.
- L'enveloppe remet alors `[0x80127164]` à 0 (h4 §1).
- [M] : l'objet est « en cours » du départ jusqu'au tick qui suit celui où il quitte les mains.

### 3.2 Aiguillage `0x800350C0` et annulations
FAIT [L][V].
- Rien si `[0x80127164] == 0`. Sinon table `0x800233EC`, puis `[0x80127164] = 0`.
- Sites d'appel : `0x80031BFC` (contrôle verrouillé, `[0x800DC4B8] & 0x34`), `0x80032758` (états sans gestionnaire), `0x80032770` (`+0x188 & 0x80`), `0x800327B4` (mort), et les pilotes en pente 4.

**Annulation des graines `0x80034ACC`** (34/34 [V]).
- Sort si `C = H+0x2C` est nul, si `C.+0x68 != 1`, ou si `C.+0x24 != 0` (objet déjà lâché).
- Sinon : `H+0x2C = 0` (`0x80034B10`) ; effet **8** à la position de `C` (`CreateEffectEntity(0, 8, 0, x, y, z)`, z inchangé) ; `DestroyEntity` à un argument (`0x8003A774`), sans effet ni butin.

**Annulation de la bombe `0x80034B54`.** Même chose avec le type 2 et l'effet **12** (`H+0x2C = 0` en `0x80034B98`).
- Ni l'une ni l'autre n'efface `C+0x28`.
- Le `DestroyEntity` à un argument efface `H+0x2C` par sa queue (`0x8003A7A4`), sans test d'identité.

Mesures [M] :
- Contrôle verrouillé (`[0x800DC4B8] = 0x20` pendant le portage) : au tick suivant, effet 12 à z = 32 px, bombe détruite, `H+0x2C = 0`, **aucune explosion** (`bomb_locked.txt`).
- Entrée en eau profonde avec la bombe (cases de propriété de sol `0x08`, bottes `0x1B`) : le pilote annule en pente 4, effet 12, objet libéré, héros en `0x1D` (`bomb_water.txt`). Même chose pour les graines avec l'effet 8 (`beans_water.txt`).

## 4. La bombe : C62, D30, E12

### 4.1 C62 `0x8007A978` (tick)
FAIT [L][V] : 186/186 instructions, 0 écart. Les deux sites qui effacent le lien sont `0x8007AAB8` (fin de fusée) et `0x8007AAF0` (lâcher). Dans l'ordre :
1. `E+0x184 & 4` (case de trou) : `DestroyEntity` à un argument, sortie.
2. `v = E+0x274` ; `E+0x274 = v-1` (toujours écrit).
   - Si `v == 0` : `E+0x10 = 3` (seul E tourne ensuite) ; si porté, `H+0x2C = 0` et `E+0x28 = 0` ; puis le cas « libre ».
   - Sinon, si `v < 60` et `v % 8 == 0` (v = 56 à 8 : **7 étincelles**) : `CreateEffectEntity(0, 9, 0, x, y, z+0x80000)`. Si l'effet est obtenu, **deux pas du générateur** : `effet+0x54 = hi(s1·0x30001) - 0x18000` et `effet+0x58 = hi(s2·0x20001) - 0x10000`.
3. Porté (`E+0x28 != 0`) :
   - `E+0x24 == 0` : `E+0x88 = 0` (repos).
   - Sinon (lâché) : `E+0x88 = 2` (vol), `E+0x24 = 0`, `H+0x2C = 0`, `E+0x28 = 0`. Le mode (1, 2, 3, -1) n'a pas d'importance : l'animation est toujours 2.
4. Libre : rien si `E+0x88 == 0`.
   - `ForceAdjusted (+0x13C) != 0` en l'air, avec `ForceZ > 0` et `E+0x278 == 0` : `E+0x278 = 1`, direction +0x10 (demi-tour, **un seul rebond sur mur**), effet 9.
   - `ForceAdjusted != 0` au sol : si un des quatre pointeurs `+0x148+4i` désigne un mot `& 0x1001 == 0x1001` (case de mur pour la classe B), statut 3 (explosion sur place). Dans les deux cas du sol, `E+0x88 = 0`.
   - `ForceAdjusted == 0` : rien tant que ni `IsOnGround` ni `HitCounter` ; sinon **son 0x18**, `E+0x278 = 0`, et `E+0x88 = 0` sauf si un coin est sur une case de mur.

Vérifié en jeu [M] :
- Lancer à l'est ou l'ouest : 34,5 px ; au nord ou au sud : 23 px. Arc de 22 ticks, sommet à 50 px, son 24 à l'atterrissage (`bomb_throw_4dirs.txt`).
- Rebond contre un mur en vol montant (`bomb_wall.txt`).
- Atterrissage au pied d'un mur sans explosion quand aucun coin n'est dans une case de mur (`bomb_wall2.txt`).
- Eau profonde : §5.2.

### 4.2 D30 `0x8007E79C` (touche)
FAIT [L][V] : 18/18.
- `(E+0x224).+0x1C8` est l'attaque de l'entité qui touche. Si son octet `& 0xF` vaut 4, 6 ou 10 (bombe, boule de feu, magie de feu), `E+0x10 = 3`.
- Les autres attaques sont ignorées : réponses 0x80, donc la passe de coups ne pose même pas `TouchingEntity`.
- [M] : une seconde bombe à 11 px est touchée dès le premier tick de boîte de la première (`T+259`). D30 la passe en statut 3 à `T+260`, son animation d'explosion commence à `T+261` (`bomb_chain.txt`).

### 4.3 E12 `0x8007F27C` (explosion)
FAIT [L][V] : 36/36.
- Si `E+0x88 == 1` et `E+0xAC == 1` : `DestroyEntity(E, -1)`. Le butin `0x80032B90` avec contenu 0 ne donne rien, et l'effet de bris (octet `+0x1E`) vaut 0 pour la bombe. Idem si `E+0x184 & 4`.
- Sinon : **`CheckAndTriggerTileEffect(E)`**, puis `E+0x88 = 1` et `E+0x6C = (E+0x6C | 0x40) & ~0x30`.

Chronologie [M] (`bomb_fuse.txt`, `bomb_throw_4dirs.txt`) :

| Tick | Événement |
|---|---|
| `T` | naissance (statut 1), son 44 (levée du héros), `[0x80127164] = 0x23`, `+0x274 = 240` |
| `T+1` | statut 2 ; la main monte de 1 px par tick |
| `T+2` | première décrémentation de la fusée |
| `T+32` | la main atteint 32 px |
| `T+36` | le héros passe en portage `0x0C` (geste de 36 ticks) |
| `T+186` à `T+234`, pas de 8 | 7 étincelles (effet 9) |
| `T+242` | fin de fusée : statut 3, mains libres |
| `T+243` | E12 : animation 1, **son 46** ; la bombe tombe si elle était portée |
| `T+259` à `T+273` | boîtes de coup actives (15 ticks) |
| `T+294` / `T+295` / `T+296` | fin d'animation / statut 4 / case libre |

Boîtes (`map_alundra.json`, animation 1) : une boîte par image, centrée sur la bombe, Z de 0 à la hauteur. Dimensions largeur × profondeur × hauteur, en px.

| Ticks de l'animation | Boîte |
|---|---|
| 16 à 18 | 16×8×11 |
| 19 à 22 | 28×20×19 |
| 23 et 24 | 44×28×27 |
| 25 à 30 | 48×32×31 |

Zone mesurée [M] contre un bloc 24×16×32 (`explosion_grid.txt`) :
- Touché jusqu'à 36 px en X et 20 px en Y du centre, pas à 40 en X ni à 24 en Y.
- **Un seul coup par cible et par explosion** : la protection de 25 ticks (`+0x1D0 = 0x19`) dépasse les 15 ticks de boîte.
- Premiers dégâts calculés à `T+260` au centre, `T+263` à 24 px, `T+267` à 30 px, `T+269` à 36 px (la boîte grandit).

### 4.4 Dégâts
FAIT [V].
- Règle : `r = fiche[2+4]` ; `(r & 0xC0) == 0x40` donne PV 0 ; `(r & 0xC0) != 0` donne rien ; sinon `max(1, (5·r) >> 4)`.
- Calcul avec le vrai `ComputeNewHp` sur les 512 fiches (`bomb_damage_table.txt`, par fiche dans `bomb_damage_by_sheet.json`). `h4_damage.py` rejoué : 20 480 cas, 0 écart.
- Fiches à mort immédiate (réponse 0x40) : 329, 335, 338, 341, 344, 347, 350, 356, 362, 365, 371, 377, 386, 389, 392, 395, 401, 404, 410, 423, 425, 428, 431, 437, 440, 443, 449, 450, 452, 453, 455, 456, 458, 461, 467, 473, 478, 484, 486, 487, 496, 502, 503, 504, 505.
  - Indice de fiche = 256 + indice de sprite de carte.
  - Identification par le recensement d'e0c : INFÉRENCE. 329 slime, 335 Beannoïde, 338 à 344 orcs, 347 à 356 Murutas.
- Le héros [M] : 10 PV puis 5 (une explosion à 17 px), recul, animation `0x31`. La règle du héros est celle de `ComputeNewHp` avec sa fiche de défense (`docs/plan-e20-annexe/e20-c2/c2-binary.md` §2.2). La réaction est E20.c2b.

### 4.5 Murs, rochers, herbe
- **Herbe** [M] : avec 25 cases d'herbe autour de l'explosion, 8 sont coupées (toutes sauf une).
  - Effet global 2 et son `0x1F` par case, à `T+260`, `T+263` et `T+269` (`bomb_grass.txt`).
  - CATE n'examine que les quatre coins de la boîte de l'image précédente (e20-e), donc seulement pendant les 15 ticks de boîte.
- **Entités** [D][L] :
  - Couvercles de tige-bombe (sprites globaux 241, 242, 243) : 30 placements sur les cartes 6, 14, 61, 63, 64, 67, 80, 85, 90, 104, 151, 362, 382, 429. 29 d'entre eux testent la bombe par `0x87 [recherche, 3]` dans leur programme D (« touché par l'attribut `byte[0x80098FA4+3] = 4` »).
  - 3 blocs transparents D testent aussi `0x87` (cartes 154 et 352).
  - Racine destructible (sprite global 253, 2 placements, carte 8) : D32 natif `0x8007E828`. Attribut 4 : `E+0x88 += 1` et `|= 0x40`.
- **Secrets de carte** [D] :
  - 28 sites `0x95 [x, y, z, w, h, _, 8, 8, 3]` (masque `0x0808`, type 3) dans 25 cartes : 1 à 10, 12 à 17, 19, 20, 226, 331, 407, 439, 479. Ils sont portés par un bloc transparent invisible (sprite global 25).
  - Carte 10 (sur la chaîne) : le bloc attend tant qu'aucune bombe n'est dans le rectangle (9, 26). Ensuite : secousse de caméra, attente de 24 ticks, naissance de l'enregistrement 105, petit saut de l'entité 137.
- La bombe elle-même ne modifie aucune case. FAIT [L] : E12 n'écrit que l'herbe, par CATE.

### 4.6 Ce qui ne s'applique pas
- Une bombe lancée ne blesse pas en vol : animations 0 et 2 sans boîte, `+0x1D4 == 0` [D][M].
- Un objet porté ne blesse personne (g1a §1.9).

## 5. Les graines : C73, E21, le pot

### 5.1 C73 `0x8007BB30` (tick)
FAIT [L][V] : 27/27.
- Porté : `E+0x24 == 0` donne `E+0x88 = 0`. Sinon : `E+0x88 = 2`, `E+0x24 = 0`, `H+0x2C = 0`, `E+0x6C |= 0x10` (DeactivateOnImpact), `E+0x28 = 0`.
- Libre : `IsOnGround` donne `E+0x10 = 3`. Le statut 3 vient aussi du choix de programme sur un impact de mur, grâce au bit `0x10`.

### 5.2 E21 `0x8007F7CC` (deactivate)
FAIT [L][V] : 43/43.
- `S = E+0x12C` (l'appui). Si `S != 0`, `S.+0x68 == 0x199` et `S.+0x88 == 0` : `S.+0x88 = 1` (le pot se réveille) et `DestroyEntity` à un argument (la graine disparaît **sans effet**).
- Puis : `E+0x88 == 1` et `E+0xAC == 1` donne `DestroyEntity(E, -1)` (effet de bris 8 attaché). Sinon : `E+0x88 = 1` et `E+0x6C = (E+0x6C | 0x40) & ~0x30`.

Chronologie [M] (`beans_basic.txt`, `beans_pot.txt`) :
- Vol de 22 ticks (même arc que la bombe). Atterrissage `Ts+22`, statut 3 `Ts+23`, fin d'animation 1 `Ts+24`, statut 4 et effet 8 `Ts+25`, case libre `Ts+26`.
- Sur un pot (bloc retypé `0x199` dont le haut est à 32 px) : la graine se pose à `Ts+16` et passe en statut 3 à `Ts+17`. Le pot passe en animation 1 et la graine est détruite sans effet à `Ts+18`.
- Dans l'eau profonde (quatre coins en pente 4), bombe comme graines sont détruites au tick d'atterrissage avec l'effet 6 (`DestroyOnSlidingSlope`), sans explosion [M] (`bomb_thrown_in_water.txt`).

### 5.3 Le pot magique (page de carte 153, type 0x199)
FAIT [L][D]. C74 n'a pas de vecteurs (lecture seule).
- En-tête : indicateurs 0x80 + 0x6000, taille 24×16×4, A = 20, C = 74, D = E = F = 0. Aucun programme de carte pour les 39 placements.
- A20 : `+0x274 = PosZ` (hauteur de repos).
- C74, selon `TargetAnim` :
  - 1 (réveil, 22 ticks, son 31) : un effet 14 (deux tirages) tous les 4 ticks.
  - 2 (repos) : rappel du Z vers `+0x274` ; si le héros est dessus (`H+0x12C == pot`), `ForceZ = -0x8000` et anim 3.
  - 3 : retour en 2 si le héros part.
  - 4 : si le héros est parti avec `ForceZ > 0`, **`H.ForceZ = 0x98000`** (impulsion de 9,5 px par tick) et anim 5 ; sinon anim 2.
  - 5 : rappel.
- INFÉRENCE : le pot réveillé est un tremplin à super-saut. INCONNU : l'usage dans les niveaux.
- Placements : cartes 12, 14, 15, 17 (2), 60 (6), 77 (2), 139, 144, 145 (2), 342, 356, 380 (8), 407, 431, 439 (2), 447 (8). Nom « Pot magique (boïng) ».

### 5.4 Les graines sont-elles une attaque ?
FAIT : non.
- `AttackCount` 0 (`BALANCE.BIN.json`, fiche 1), aucune boîte de coup dans leurs trois animations, aucun test d'attaque dans C73 ni E21.
- Elles ne blessent ni en vol ni à l'atterrissage.

## 6. Interactions

FAIT [M], sauf mention (`results/scenarios/`).
- **Tenir l'objet.**
  - Le héros peut marcher (état `0x07`, vitesse 208 sans rampe, g1a).
  - Il peut sauter : `Cross` donne `0x2E`, son 10, sommet 27,5 px, retour en `0x0C` avec son 11 et poussière. L'objet suit et la fusée continue.
  - Il ne peut jamais attaquer.
  - Rond donne le son 3 (refus, `H+0x2C != 0`). Triangle ne fait rien (`bomb_carry_actions.txt`).
- **Lancer (Carré).**
  - Dans `0x0C/0x07/0x30…`, l'objet part au tick du Carré (animation 2, `+0x24` consommé) et le héros passe en `0x09` (sol) ou `0x0A` (air) pendant 19 ticks, son 26.
  - Portée : 34,5 px (est, ouest) ou 23 px (nord, sud), plus 0,75 ou 0,5 de glissade. Identique debout ou en marchant.
  - Lancé en l'air (héros à 27,5 px) : 40,5 px, la bombe part à 36 px au-dessus de lui (`bomb_air_throw.txt`).
  - Pas de « poser » (g1a). Pas pendant le geste de levée (`0x05/0x0B` ne gèrent pas le Carré).
- **Blessure.**
  - `UpdatePlayerCarriedEntity(2)` (`0x80031C64`) pose `E+0x24 = -1`, la direction du héros et la position `H + (0,0,32)`.
  - L'objet est lâché au tick suivant (animation 2) et part droit devant sur 35 px (`bomb_hurt_while_carrying_bomb.txt`).
  - Pas d'annulation sur ce chemin ; l'état « en cours » tombe un tick plus tard.
- **Mort** (`0x800327AC` mode 2, `0x800327B4` annulation) : la bombe est lâchée, se pose et **continue sa fusée**, sans fumée, car l'annulation sort sur `E+0x24 != 0` (`bomb_death.txt`).
- **Dialogue ou contrôle verrouillé** : la bombe disparaît au premier tick (fumée 12), sans explosion (§3.2).
- **Case de sortie** (`+0x188 & 0x80`, `0x80032770` puis mode 1 `0x80032778`) : même annulation. FAIT [L], non rejoué.
- **Changement de carte.** FAIT [L] : `0x8002DFE4` (chargement) appelle `0x8003B24C`.
  - Les 64 emplacements reçoivent le gabarit vierge `0x80134368`, puis le héros est recréé (`0x80031974`). `[0x80127164]` est remis à zéro (`0x8003295C`).
  - Rien ne survit à un chargement.
  - Arrivée dans la même carte (type 3, `0x80031498`) : l'objet porté est décalé du même vecteur que le héros (g1a §1.7).
- **Bombe posée.** Le héros ne la pousse pas et la traverse [M].

## 7. Rejeu de référence

- Les fichiers `H3/results/scenarios/*.txt` sont les rejeux de référence : changements seulement.
- `H3/results/goldens_h3.json` contient 480 vecteurs (40 par fonction) pour un rejeu en C#. Chacun donne les mots d'entrée, les mots changés, le retour et le journal des appels simulés.

## 8. Divergences avec la décompilation

- **D1 (comportement). Les annulations testent le mauvais champ.**
  - `FUN_80034b54` et `FUN_80034acc` (`PlayerManager.cs:4177`, `:4200`) sortent si `carriedEntity.BlockedByEntity != null`, soit `+0x20` (`Entity.cs:21`).
  - Le binaire (`0x80034B00`, `0x80034B88`) teste `+0x24`, le mode de lâcher (`Flags2`, `Entity.cs:22`).
  - Conséquence : à la mort du héros (mode 2 pose `+0x24 = -1`, puis annulation), le binaire ne fait rien et la bombe lâchée continue sa fusée [M] ; la décompilation la détruirait avec une fumée.
  - Au contrôle verrouillé (`+0x24 == 0`) les deux se valent.
- **D2 (nom).** `AnimateWarpEffect` (`PlayerManager.cs:3983`) est l'aiguillage d'annulation d'objet `0x800350C0`, pas un effet de saut. Même erreur que `IsFadeActive` (h4 D11). Comportement identique.
- **D3 (nom).**
  - `PlatformEntity` (`+0x28`) est le **porteur**, toujours le héros.
  - `Flags2` (`+0x24`) est le mode de lâcher.
  - `Bytes` et `DelayOrAngleOrEntityId` sont `+0x274` et `+0x278`.
  - `RidingEntity` (`+0x12C`) est bien l'appui.
  - Sans effet.
- **Sans écart** (relu ligne à ligne) :
  - `TrySpawnWarpEntity` (`:2008`), `TryUseBomb` (`:2183`), les petits pilotes (`:3120`, `:3083`).
  - C62 (`FunctionTypeC.cs:6214`), C73 (`:6954`), D30 (`FunctionTypeD.cs:788`), E12 (`FunctionTypeE.cs:349`), E21 (`:648`).
  - Mêmes tests et mêmes valeurs, dont l'ordre des deux tirages des étincelles.
  - La décompilation nomme le pot `MagicPotTick` (`FunctionTypeC.cs:6987`).
- **Corrections aux notes antérieures** (pas à la décompilation) :
  - `h-g-deps.md:44` et §0.7 disent qu'un objet porté qui a des attaques est tiré dans le héros et détruit après 5 ticks. Pour la bombe en contrôle verrouillé, l'annulation la détruit dès le premier tick, avant que le mode 1 ait compté 5.
  - `h-g-deps.md:84` omet la garde `+0x24` de l'annulation.
  - La ligne « D-E20-10, le couvercle de la 362 demande une bombe posée » s'éclaire avec §4.5 et §9 : la bombe d'Olen n'a besoin que de D30 et E12.

## 9. Où le jeu donne la bombe et les graines (lecture seule)

[D] `data-extracted/data/map_*.json` : 483 cartes, programmes décodés avec la table de tailles `Alundra/Events/EventOpcodeSizeTable.cs`. Contre-vérifié avec `alundra-project/Maps` : 14 sites `0x82`, mêmes cartes. Scripts : `H3/tools/corpus/scan1.py` à `scan6.py`.
- **`0x82` (donner l'objet)** :
  - Bombe : cartes **10** (B[8], octet 995), 17 (B[1]), 31 (B[4]), 77 (C[100]), 80 (B[1]), 158 (C[1]), 331 (B[8]), 334 (C[2]), 365 (C[2]), 439 (B[1]), 443 (B[1]).
  - Graines : cartes 17 (B[1]), 144 (C[14], le vieil homme du cap), 439 (B[1]).
  - Aucun `0x83` ni `0x84` sur ces objets.
- **Coffres** : `Contents = 0x23` et `0x1F` seulement sur la carte 0, une carte de débogage avec un coffre par objet.
- **Objets posés** : carte 11 (graines et bombe), cartes 334 et 365 (bombe) ; sprites globaux 61 et 65.
- **Sur la chaîne** (`AlundraStoryChainOpcodeAudit.ChainMaps`, 30 cartes) :
  - La carte **10** donne la bombe quand G215 et G712 sont levés et G714 baissé. INFÉRENCE sur le sens des opcodes `0x30` « si levé » et `0x31` « si baissé ».
  - Sa roche facultative (secret `0x95`, rectangle 9, 26) réagit à la bombe.
  - Aucune autre carte de la chaîne ne donne ni n'utilise la bombe ou les graines. Ni couvercle, ni racine, ni pot.
- **Bombe posée par une carte** : une seule, la carte 362 (enregistrement 5, sprite global 2).
  - Son programme C est un programme de carte (indice 133) qui la fait voler (anim 2) puis exploser (anim 1).
  - Ses D (30) et E (12) sont natifs : E12 la détruit à la fin de l'animation.
- **Clés de registre** (`scan6.py`, toutes les pages) : (C,62), (D,30) et (E,12) ne servent que le sprite global 2 ; (C,73) et (E,21) que le sprite global 1. Pas de collision avec une autre clé.

## 10. État de la DLL utile à h3 (HEAD `8887a488`, FAIT [DLL])

- **Stubs.**
  - `TryUseItem` et `PlayerTryAction` sont des bouchons (`Alundra/Hero/PlayerManager.cs:932`, `:940`).
  - `UpdateItemUseDriver` (`:1507`) et `UpdatePlayerCarriedEntity` sont nommés et vides.
  - `CarriedEntity` et `PlatformEntity` ne sont jamais écrits (g1a §4).
  - `CheckAndTriggerTileEffect` est un bouchon nommé (`Alundra/Ai/Projectiles/WaveSwordAi.cs:40`).
  - Garde du menu : `Alundra/UI/Inventory/InventoryDirector.cs:419` (« NO PORT EQUIVALENT »).
- **Ce qui existe.**
  - `world.SpawnEntityBySpriteIndex` (e0a1 à e0a5, `Alundra/Maps/WorldProxy.cs:3482`).
  - `DestroyEntity(e, effectId)` (`:3301`, E14.a1b) et `DestroyEntity(e)` (`:3329`).
  - Passe de coups, règle de dégâts, opcodes `0x87` et `0x95` : `Alundra/Combat/HitPass.cs`, `Damage.cs`, `HitOpcodes.cs`.
  - Prefabs `Bombe` et `Haricots de Jack` exportés, animations 0 à 2 en quatre directions. L'animation 1 de la bombe porte des `collision_keyframes`.
- **Registre** (`Alundra/Ai/NativeSpriteRegistry.cs`) : clés (slot, code), liste de types par entrée, doublon refusé. Il contient Beannoïde, Muruta et la vague (C61).
- **(E,12) n'est pas géré.** `IsInlineDeactivateCode` ne couvre que E0, E1 et E10. INFÉRENCE (lecture du code, non exécutée) : la bombe de la 362 n'est jamais détruite après son explosion.

## 11. Découpage proposé de h3

Les gestionnaires de la bombe ne dépendent que du registre, de la passe de coups et de la destruction. Ce qui exige les mains (g1a, g2a) vient après. Tailles en instructions MIPS lues : D30 18, E12 36, C62 186, C73 27, E21 43, départs 63 et 81, pilotes 2×23, annulations 2×34.

| Tranche | Contenu | Prérequis | Preuve |
|---|---|---|---|
| **h3.0** | Lecture : rejouer `goldens_h3.json` en C#, relire les lignes de la DLL | aucun | vecteurs |
| **h3a** bombe sans les mains | Clés (D,30) et (E,12) pour le type 2. E12 : destruction en fin d'animation, appel du bouchon CATE à chaque tick, animation 1. D30 : chaîne. Option : D32 (sprite 253) | registre (E14.a0), passe de coups (c1a), `DestroyEntity(e,-1)` (E14.a1b) | vecteurs D30 et E12 ; arc E20.A362 (bombe d'Olen détruite, couvercle) ; chaîne de deux bombes ; `0x95` de la carte 10 |
| **h3b** bombe dans les mains | Départ `0x8003453C` (échecs et son 3, plafond de 3, naissance par l'interface, `+0x274 = 0xF0`, `PutInHands`), pilote, annulation, entrées de table de h0a. C62 : fusée, étincelles, lâcher, vol, rebond, atterrissage, mur | h0a (Rond, `[0x80127164]`, tables, aiguillage, garde du menu), g1a (noyau, modes 0/1/2, états, effacement du lien), g2a (Carré), règle de vitesse d'f5 pour une entité née par l'interface (Q-G1A-4) | vecteurs ; chronologies `bomb_*.txt` ; mutants |
| **h3c** graines | Départ `0x80034224`, pilote, annulation ; C73 ; E21 | h3b | vecteurs ; `beans_basic.txt`, `beans_pot.txt` |
| **h3d** pot magique (optionnel) | A20 et C74 (tremplin), clé de type `0x199` | h3c | vecteurs C74 à écrire (non faits ici) ; arc sur une carte à pot, hors chaîne |
| **h3e** arcs | Carte 10 (don de la bombe, roche) ; héros-bombe-ennemi (Beannoïde : 1 coup) ; herbe coupée ; blessure du héros par sa bombe | E20.e, E20.c2b, f6a | acceptation |

Notes sur les prérequis nommés :
- **g2b** (E2, briser) ne sert pas h3.
- **g1b** (relever) ne sert pas h3 : la bombe n'est pas relevable (§0.3).
- **g3** : aucun lien.
- **f6a** : bombe et graines portent `DestroyOnSlidingSlope` avec la gravité. La mort dans l'eau profonde (effet 6) est mesurée [M]. La DLL a déjà la destruction par pente 4 pour tous les appelants (E14.a1b) ; à confirmer en T0 de h3b. Les graines portent aussi `DestroyOnVramFlags` (effet 8).
- **E20.e** : E12 appelle le bouchon nommé, rien à attendre.
- **Registre** : cinq clés, restreintes aux types 1 et 2.

Ordre conseillé : h3a dès que possible (il débloque E20.A362), puis h0a, g1a, g2a, h3b, h3c ; h3d et h3e à la demande.

## 12. Preuves et rejeu

Tout se lance depuis `H3/run/` (`python -B …`).

**Par fonction** :
- `python -B h3_vec.py 20000 100 <nom>`
- Noms : `start_beans`, `start_bomb`, `drv_beans`, `drv_bomb`, `cancel_beans`, `cancel_bomb`, `dispatch`, `C62`, `D30`, `E12`, `C73`, `E21`.
- Sans nom : les 12 d'un coup. La couverture est imprimée par la même commande.

**Vecteurs** (`h3_vec.py`).
- Mondes aléatoires : statuts, 64 emplacements, bombes aux emplacements 0, 62, 63, 64, mots périmés de pile, cases à motif de mur, pointeurs au-delà des quatre coins, valeurs de `+0x184`, fusées aux bornes 0, 1, 7, 8, 56 à 61, 240.
- Mémoire comparée octet par octet (emplacements, page des variables, générateur, réservoirs) ; journal des appels simulés (naissance, effet, son, destruction, coupe de l'herbe).
- Résultat : **20 000 vecteurs (graine 100), 0 écart** (`results/vec_final.txt`).
  - Par fonction : C62 1671, C73 1639, D30 1619, E12 1644, E21 1651, cancel_beans 1748, cancel_bomb 1618, dispatch 1648, drv_beans 1715, drv_bomb 1712, start_beans 1678, start_bomb 1657.
  - Auparavant : 3 000 vecteurs sur chacune des graines 2, 3, 11, 21, 0 écart.

**Couverture.**
- 100 % des instructions : start_beans 63/63, start_bomb 81/81, pilotes 23/23, annulations 34/34, C62 186/186, D30 18/18, E12 36/36, C73 27/27, E21 43/43.
- Aiguillage `0x800350C0` : 50 des 62. Les 12 restantes sont les entrées des tomes, domaine de h4.

**Mutants du code réel** (`h3_wmut.py`, un mot changé en mémoire, `classify_survivors.py`).
- 629 mutants : sens des branches, immédiats ±1, slt/sltu, décalages de champ, mots supprimés.
- 513 tués, 116 survivants, **tous expliqués équivalents** (`results/mut_words_summary.txt`) : 40 tailles de pile, 36 copies de registre, 20 sauvegardes de registres, 7 arguments jamais lus des annulations, 13 mots morts ou bornes sans effet, 0 inexpliqué.
- Le premier passage avait montré des vecteurs trop peu sensibles : boucle de 64 emplacements, mots périmés de pile, champ voisin de la pente, `+0x184` à bit 0 seul, pointeurs après les quatre coins. Les vecteurs ont été renforcés et toute la mesure refaite.

**Mutants des modèles** (`h3_mut.py`, 79 mutations des modèles Python).
- 78 détectées. La seule survivante, « fenêtre d'étincelles 60 → 64 », est équivalente : les multiples de 8 inférieurs à 60 et à 64 sont les mêmes (56 au plus).
- Lancer par tranches : `h3_mut.py --shard K/8 --n 2500`.

**Autres rejeux.**
- `h3_goldens.py 40` pour les vecteurs de référence C#.
- `sc_throw.py`, `sc_misc.py`, `sc_beans.py`, `sc_hurt.py`, `probe_birth.py` pour les chronologies.
- `sc_explosion.py` pour la grille de coups.
- `sc_damage_table.py` pour les dégâts sur les 512 fiches.

## 13. Questions pour l'auteur

**Q-H3-1. Porter la bombe avant que les caisses redeviennent solides ?** Suite de Q-G1A-1, option B.
- Si oui, après h0a, g1a et g2a, le joueur voit ceci :
  - il appuie sur Rond, un geste de levée de 36 ticks suit ;
  - il marche et saute avec la bombe, puis la lance à 34 px (23 px verticalement) ;
  - elle explose 242 ticks après le départ, soit environ 4,8 s à 50 ticks par seconde (INFÉRENCE sur la cadence) ;
  - elle tue d'un coup 45 types d'ennemis, dont la Beannoïde, coupe l'herbe et ouvre le secret de la roche de la carte 10.
- Coût : les sites de blessure, de mort et de case de sortie de g1a restent des bouchons nommés tant que E20.c2b et E18 manquent. Une bombe tenue reste alors en main si le héros est touché.
- Si non (h3 après g1b et g3) : rien de visible avant, le premier objet portable reste la caisse.
- Défaut proposé : oui, car h3 n'a besoin ni de g1b ni de g3 (§0.3).

**Q-H3-2. Le héros et sa propre bombe.**
- L'original lui retire 5 PV sur 10, le repousse et le met en `0x31`.
- Sans E20.c2b, aucune de ces réactions n'existe : le héros resterait immobile dans l'explosion sans rien perdre, alors que les ennemis meurent.
- Options : (A, défaut) livrer ainsi, la blessure arrive avec c2b ; (B) attendre c2b avant d'ouvrir h3b au joueur. Coût de B : l'ordre.

**Q-H3-3. Les graines servent-elles maintenant ?**
- Sans le pot (39 pots, 16 cartes, aucune sur la chaîne) elles ne font rien de visible : elles éclatent 3 ticks après l'atterrissage.
- Options : (A) h3c sans pot (27 + 43 instructions, aucun effet visible) ; (B) h3c + h3d, le tremplin (+127 instructions, un arc sur une carte à pot) ; (C, défaut) reporter graines et pot ensemble au moment où une carte à pot devient atteignable.

**Q-H3-4. Le menu Start tant que l'objet est dans les mains.**
- Original : refusé, jusqu'à 243 ticks pour la bombe, indéfiniment pour des graines tenues.
- Reproduire (défaut, fidèle) ou laisser ouvrir le menu ? L'alternative est un écart visible à documenter. La garde existe déjà comme bouchon (`InventoryDirector.cs:419`).

**Q-H3-5. Mort du héros avec une bombe.**
- Le binaire la lâche et elle continue sa fusée ; la décompilation la détruirait en fumée (D1).
- On suit le binaire (règle : le binaire gagne). Rien à décider, sauf si l'auteur préfère l'autre rendu.

**Q-H3-6. Découper h3a en tête ?**
- E12 et D30 servent la bombe d'Olen (carte 362) sans qu'aucune main n'intervienne. E20.A362 est annoncée « après h3 » ; avec h3a elle peut tourner dès que D30 et E12 existent.
- Défaut : oui.

**Q-H3-7. La racine destructible (D32, carte 8, 2 placements, hors chaîne).**
- L'inclure dans h3a (17 instructions) ou la laisser à E14 ?
- Défaut : la laisser, aucun chemin de jeu actuel.

**Q-H3-8. Sons des effets.** Les sons éventuels des effets 2, 6, 8, 9, 12 ne sont pas décodés (même question que Q-H4-5) : INCONNU. Sont lus : 46 (explosion), 24 (atterrissage), 26 (lancer), 44 (levée), 31 (herbe), 3 (refus).

## 14. INCONNU

- Les sons que les programmes d'effets 2, 6, 8, 9 et 12 pourraient jouer.
- Ce que la bombe fait aux autres entités de classe B (PNJ) : cela dépend de leurs indicateurs et de leurs programmes D, hors de cette lecture.
- Le comportement des cartes à pot au-delà de C74 : aucun programme de carte pour le sprite 153, et l'usage du super-saut dans les niveaux est une INFÉRENCE.
- La suite du programme B[8] de la carte 10 après le don (drapeaux G714 et suivants).
- Les cadences en secondes (50 ticks par seconde : INFÉRENCE, reprise des notes du plan).

**Fichiers utiles (absolus).**
- `H3/README.md`
- `H3/run/h3_models.py`, `h3_vec.py`, `h3_mut.py`, `h3_wmut.py`, `h3_goldens.py`, `h3_game.py`
- `H3/run/sc_throw.py`, `sc_misc.py`, `sc_beans.py`, `sc_hurt.py`, `sc_explosion.py`, `sc_damage_table.py`, `probe_birth.py`
- `H3/run/lst/` (listings)
- `H3/tools/` et `H3/tools/corpus/`
- `H3/results/` : `scenarios/`, `goldens_h3.json`, `mut_models_all.txt`, `mut_words_summary.txt`, `bomb_damage_table.txt`, `explosion_grid.txt`, `vec_final.txt`