# Rapport : l'achat en boutique et le code natif C4 (lecture seule, 26 articles, 5 cartes)

## 0. Provenance et limites
- **Dépôt.** Lecture seule. HEAD au moment de la lecture : `4f1ad619` (E20.g3 T2). Je n'ai lancé aucun build, aucun test et aucune commande git qui écrit. `git status` ne montre que les modifications de l'autre tranche (g3).
- **Binaire.** `ALUN_CD.EXE` (France) lu avec capstone. La décompilation est citée seulement quand elle est confirmée par le binaire ou par un rejeu.
- **Étiquettes.**
  - FAIT : lu dans le code, les programmes exportés ou le binaire.
  - MESURÉ : rejoué sur le vrai code MIPS, dans la machine d'oracle copiée sous `work/orc`. La DLL n'est pas lancée.
  - INFÉRENCE : déduit, non exécuté.
- **Unités.**
  - Tuile : `TileX = px/24`, `TileY = px/16`, `TileZ = Z/16` (`WorldProxy.cs:2160-2162`).
  - Hauteur de case : 1 = 16 px.
  - Hauteur d'enregistrement : `h*8` px.
- **Dossier de travail** : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/shop-disc/` (noté `SD/` ci-dessous).
  - `SD/SHA1SUMS.txt` liste les scripts et sorties.
  - Les sorties sont dans `SD/out/`. Les scripts sont à la racine de `SD/`.

## 1. Synthèse

| Q | Réponse courte | Preuve |
|---|---|---|
| **S1** | **Oui, il faut soulever l'article avec Carré et le lancer sur le comptoir.** Le texte du jeu le dit : « jette-le sur le comptoir ! » (258, id 163). Un programme de carte (B) regarde chaque tick si l'article repose dans une zone de tuiles (`0x07`) et au sol (`0x70` ou `0x8D`). Alors : prix, Oui/Non, `0x9A`, `0x82`, `0x99`. Ensuite l'article est détruit et réapparaît sur son étagère. Sortir de l'Inoa 258/298 avec l'article en main est bloqué par un dialogue, via `0xB2` avec la recherche 139. | FAIT, MESURÉ pour la mécanique de levée |
| **S2** | **Trois conditions** : genre de ramassage 1 dans `+0x6C`, bit `0x80` (Collidable), et code natif C = 4 pour relâcher l'objet. Le genre n'est pas dans l'en-tête (`CanPickup = 1` donne le genre 0). C'est le script C (`0x62 [128,128,3]`) qui l'écrit. **C4 ne casse pas** ces articles : pas de E2, `BreakEffect = 0`. Il ne les ramène pas non plus : c'est le script de carte qui les remet en place. Jeté dans un trou, l'article disparaît. | FAIT et MESURÉ (replays A à I) |
| **S3** | **Au HEAD, `0x41 [C,4]` est appliqué et la clé `(C,4)` est absente du registre.** `PlayerTryAction` est un bouchon (`PlayerManager.cs:955`), donc rien ne se soulève. Les articles sont des blocs inertes. Aucun autre chemin d'achat n'existe. **Défaut déjà présent, indépendant de g2c1 :** `0x61` n'a pas de `case`. Dans la boutique de Lurvy (398), le flot de contrôle part dans le dialogue de réprimande 166. | FAIT, INFÉRENCE solide pour le défaut de la 398 (modèle du flot, non rejoué dans la DLL) |
| **S4** | **Non, la boutique de la 163 ne bloque pas l'histoire.** Sa salle n'est pas atteignable à pied : aucun portail du jeu n'y mène. Ses dialogues sont vides ou hors sujet. Aucun programme de la chaîne ne teste un objet qu'on n'obtient qu'en achetant. | FAIT (cellules, portails, chaînes, opcodes) |
| **S5** | **(A) Porter C4 pour les 11 types** : le coût marginal pour le code C4 est faible. Le vrai coût est la boutique elle-même : opcodes `0xB2` et `0x61`, tests de monde sur la 258 et la 398, et g1b. **(B) Les laisser immobiles** : les boutiques ne servent à rien, et la 398 a besoin de `0x61` quand même. Aucune des deux options ne change la chaîne d'histoire. | FAIT pour les coûts listés, INFÉRENCE pour l'estimation |

Corrections au cadrage précédent :
- La 163 est sur la chaîne, mais elle est la maison de Jess. Sa « boutique » est un reste de gabarit.
- Les boutiques qui fonctionnent sont la 258 et la 298 (Inoa), la 398 (Lurvy) et la 134 (église). Aucune n'est sur la chaîne.

## 2. S1 : comment l'achat se fait

### 2.1 Les 26 articles
Source : `SD/scan_shop.py`, sortie `SD/out/scan_shop.txt`. Tous les placements dont un programme exécute `0x41 [2,4]`. Les en-têtes globaux (`data-extracted/data/map_alundra.json`) sont identiques :
- `MoreFlags 0x80`
- `CanPickup 1`
- `FlagsPortraitShadowType 0x23`
- programmes A et C = 255
- D, E et F = 0
- boîte 21x15x16
- `BreakEffect 0`

| Carte | Rôle | Articles (rec: type) |
|---|---|---|
| 134 | église, gag à 10 000 florins | 17:113, 18:81, 19:83, 20:85 |
| 163 | maison de Jess, salle de boutique inaccessible | 3:66, 4:71, 5:68, 6:113, 8:50 |
| 258 | boutique d'Inoa, fonctionne | mêmes 5 enregistrements |
| 298 | boutique d'Inoa détruite | mêmes 5 enregistrements |
| 398 | maison de Lurvy | 1:66, 2:71, 3:67, 4:68, 5:48, 6:113, 7:69 |

Les 11 types sont bien 48, 50, 66, 67, 68, 69, 71, 81, 83, 85 et 113. Les 4 blocs de glace (cartes 103, 105, 108) et la carte 302 (cadeaux à l'interaction, sans C4) sont hors sujet.

### 2.2 Programmes des articles (décodés en entier)
Sorties : `SD/out/events163_all.txt`, `events258_all.txt`, `events298_all.txt`, `events398_all.txt`, `events134_all.txt`. Script : `SD/dumpall.py`.

**A (chargement).** Les index A131 ou A129 donnent `41 [0,0]`, soit le natif A mis à 0. En 258 et 298, ils ajoutent `65 [128,254,255,…]`, un décalage de -2 px en X. En 398, `65 [128,2,…]`, soit +2 px.

**C (premier tick).**
- **En 163** (`C[4]` @724) : `62 [128,128,3]`, puis `41 [2,4]` (natif C = 4), puis `40 [2,0]` (script C effacé).
- **En 258, 298, 398 et 134** : même début, puis :
  - `70` : sauter si l'article est déjà au sol ;
  - sinon `BD 55` (son), `25` (attendre le sol ou une collision en Z), `BD 24` (son d'atterrissage) ;
  - puis `41 [2,4]`, `40 [2,0]`, fin.

`62 [128,128,3]` fait `flags |= 0x380`. Cela donne Collidable `0x80`, Gravity `0x100` et le bit `0x200`, c'est-à-dire le genre de ramassage 1. L'opcode est `EventProgramRunner.cs:1253`, `SetEntitiesFlagsLow16:2514`.

**D, E, F des articles** : 0 (en-tête). Les articles n'ont pas de programme d'interaction.

### 2.3 Programmes de carte (B) : l'achat
Les cartes 163, 258, 298 et 398 ont leurs « MapEvents » en carte entière (`0..51 x 0..59`). Les programmes B s'exécutent donc à chaque tick pour le héros (`WorldProxy.cs:3216-3254`). L'exception est la zone de la 298/B[9] (`0..18 x 37..59`), qui n'est pas la boutique.

**Gabarit des B d'achat** (258 B[2] @116-241 ; 163 et 298 B[2] avec `8D` ; 398 B[1]). Déroulé de la 258, pour la herbe (rec 3) :
1. `37 [1]` (attendre un tick).
2. `07 [3, 3,7, 41,43, 2,2]` : l'entité d'identifiant d'enregistrement 3 est-elle dans la boîte TileX 3..7, TileY 41..43, TileZ 2 ? Sinon on reboucle. La recherche sans le bit `0x80` est un test `EntityRefId == 3` (`EntitySearchService.cs`, `GetMatchingEntitiesBySearchType`).
3. L'article est-il posé ? `43 [3]` (entité logique = l'article), `70` (IsOnGround), `42` (retour au héros). La 163 et la 298 utilisent `8D [3]` à la place, c'est-à-dire `PosZ <= TerrainHeight + 1`.
4. `10` (le joueur perd le contrôle), `05 [244,129]`, `59 [129,0]`.
5. `5C [1,129,1]` : dialogue prononcé par l'entité 1 (la vendeuse). Le texte finit par `[flag id=100]` et un Oui/Non. Le flux est : `50 [4]`, `36 [100,128]`, `06 [100,128]`, `44`, `51`, `39`.
6. **Non** : dialogue 138, puis `2E [3]`, `2D [3]`, `64 [3, 248,632,160]` (réapparition à z=160 px), `59 [3,0]`, `11`.
7. **Oui** :
   - `9A [5,0]` (assez d'argent ?) ; sinon dialogues 134 et 138, puis la remise en place.
   - `82 [36]` (donner l'objet 36) ; si le sac est plein, dialogues 133 et 138, puis la remise en place.
   - `99 [5,0]` (dépenser), dialogue 137 (« Merci d'être venu »), `11`, puis la remise en place.

La remise en place est `2E` (détruire), `2D` (recréer depuis l'enregistrement) et `64` (repositionner). L'article revient donc en chute libre : stock infini.

**Récapitulatif** (script : `SD/shopsummary.py`, sortie `SD/out/shopsummary.txt`) :

| Carte | Article → objet donné | Prix (florins) |
|---|---|---|
| 258 | herbe → 36 | 5 |
| 258 | potion S → 41 | 15 |
| 258 | extrait magique → 38 | 30 |
| 258 | récipient de vie (rec 6) → 83 | 500, unique |
| 258 | veste en argent (rec 8) | non vendue |
| 298 | herbe → 36, potion S → 41, extrait → 38 | 4, 12, 24 |
| 298 | récipient de vie → 83 | 400 |
| 398 | herbe → 36 / potion S → 41 / potion L → 37 / extrait → 38 | 4 / 12 / 18 / 24 |
| 398 | veste en cuir → 18 / récipient → 83 / breuvage → 39 | 120 / 400 / 200 |
| 134 | les quatre articles | aucun prix : refus systématique |
| 163 | herbe 5, potion S 15, extrait 30 (code présent) | voir §5 |

Détails complémentaires :
- Le récipient de vie de la 258 : `5D [6]` met l'article au statut 3. Le programme E0 en ligne (`EventProgramRunner.cs`, début de `RunSpriteEvent`) le détruit. Le drapeau `[196,2]` empêche sa réapparition (B[1] @108).
- En 398, les enregistrements 5, 6 et 7 sont créés par `8A` sous conditions de drapeaux (`[241,3]` puis `[84,4]`). Ils sont d'abord garés hors étagère.
- En 134 (B[1] à B[4]), les dialogues 135 et 151 sont toujours le refus. Il n'y a ni `9A`, ni `82`, ni `99`.

### 2.4 Zones de déclenchement
Zones de comptoir en tuiles, puis en px (script : `SD/cellmap.py`, tracé du plan : `SD/out/shopsummary.txt`) :

| Carte | Tuiles | Pixels |
|---|---|---|
| 163, 258, 298 | x 3..7, y 41..43, z 2 | x 72..191, y 656..703, z 32..47 |
| 398 | x 12..14, y 23..25, z 2 | x 288..359, y 368..415 |
| 134 | x 46..47, y 50, z 2 | x 1104..1151, y 800..815 |

L'article doit reposer à TileZ 2, c'est-à-dire sur une case de hauteur 2 :
- **258 :** (3,42), (5,42), (6,42), (6,41). Le bloc transparent est en (4,42).
- **398 :** (13,24) et (14,24).
- **134 :** (46,50) et (47,50).

Les articles reposent sur une étagère d'une hauteur de 16 px au-dessus du sol (cases de hauteur 2 contre 1 pour le sol), par exemple (8..13,39) et (14,40..43) en 258. La 398 a, pour chaque article, une case d'exclusion : sa case d'origine, par exemple (10,21) pour l'herbe.

### 2.5 Détection de l'article « porté » ou « posé »

| Besoin | Opcode et condition |
|---|---|
| **Posé dans la zone** | `07` avec la boîte de tuiles, plus `70` (258, 398) ou `8D` (163, 298) |
| **Porté** | `B2 [rec, 139]` : l'entité d'enregistrement `rec` fait partie du groupe 139. 139 = `0x80 | 11`, la recherche n°11 = « toutes les entités avec `+0x28` non nul, hors joueur ». **FAIT binaire** : `0x8003CE74-0x8003CED0`. Table des cas en `0x80023C30`, entité 1 en `0x80127FC4`, `+0x28` testé en `0x8003CEB0`. |
| **Posé sur le vendeur** (398) | `43 [rec]`, `61 [0]` (RidingEntity de l'article = l'entité d'enregistrement 0), `42` |

Le lien `+0x28` est écrit par `TryLift` en `0x8002EE1C` : `entité.+0x28 = héros`. Dans le replay B, `carrier28 = 80127D30` (le héros) du tick 11 au tick 61.

### 2.6 Si on sort avec l'article ou si on le jette

**Inoa 258 et 298** (B[6], @632 à 1141 en 258) :
- La boucle teste `B2 [3,139]`, `[4,139]`, `[5,139]`, `[6,139]` ; en 298 la vérification du rec 6 est dupliquée sur `[5,139]` (@744), ce qui est une particularité des données. Elle teste aussi `[8,139]`, la veste.
- Si un article des recs 3 à 6 est en main, on saute au bloc @1056 (258).
  - Il y a `55 [5,46,0,128]`, puis le test « joueur sur la tuile (5,46) » et le bouton Bas (`2F [0,64,0]` = masque `0x4000`).
  - Le héros perd le contrôle, hoche la tête (anim 12 et `5A`).
  - Dialogue 135 (258) : « …si tu essaies de sortir d'ici sans payer, j'ai bien peur de devoir te briser les genoux. »
  - Dialogue 141 (298) : « C'est d'un coup de pied au derrière que je punis les voleurs, moi monsieur ! »
  - Le programme de sortie (`53` « changer de carte », choix par ère) n'est jamais atteint.
- Sans article en main, la sortie exige : héros sur la tuile (5,46), bouton Bas tenu (`2F`) et `6E` (ForceAdjusted, il pousse contre le mur) ; alors une série de `31`/`53` choisit la carte extérieure par ère (drapeaux 201 à 224).
- **La veste (rec 8) en main** :
  - **258** : dialogue 139 « armure de famille, pas à vendre » (drapeau `[144,129]`). Posée dans la zone, B[7] affiche le dialogue 140 et la remet en place.
  - **298** : c'est un cadeau. Le héros reçoit l'objet 20 (« Armure d'argent reçue »). Cela se joue en portant l'article, avec `Destroy 8`, `Give [20]` et drapeau `[135,5]`.
- Un article jeté ailleurs que dans la zone est un objet au sol ordinaire. Il n'y a pas de garde : on peut sortir sans lui.
- À la réentrée, la carte est rechargée et l'article est réinstancié. C'est une INFÉRENCE : la réinstanciation est le comportement du chargement par enregistrement.

**Lurvy 398** :
- Pas de garde à la sortie.
- Tout article posé hors zone et hors case d'origine déclenche `05 [132,131]`, `10`, `1A [0]` et le dialogue 165 « N'mets pas la pagaille, fiston ! ». Puis `2E`, `2D`, `64` remettent l'article à sa place (z 320 px).
- Posé **sur Lurvy** (`61 [0]`) : dialogue 166 « J'te conseille d'arrêter tes bêtises… », puis remise en place.
- Sortir par le portail (7,31) avec un article en main : **non établi**. Le plan note que le chargement de carte réécrit les 64 cases d'entités. Je n'ai pas retrouvé comment le héros est remis à zéro dans ce cas.

**Église 134** : l'article jeté dans la zone donne toujours le refus (dialogues 135 puis 151) puis la remise en place. Aucune garde de sortie.

**Mécanique de l'objet lancé** (MESURÉ, §3.4) : l'article jeté sur place atterrit à environ 33,5 px. En marchant (3 px/tick), il atterrit à environ 70 px (178 contre 110,5). Il ne se brise pas.

## 3. S2 : ce que fait le binaire

### 3.1 Qu'est-ce qui rend un article soulevable
`TryLift` @`0x8002EDBC` (désassemblage conservé dans `g2c1-disc/critic/trylift.txt`) :
- `0x8002EDCC-0x8002EDDC` : `genre = (+0x6C & 0x600) >> 9`.
- Genre 1 : levée directe. Genre 0 : refus. Genres 2 et 3 : exigent `GetNumberOfItem(0x3B)` (`0x8002EDF8`).
- `0x8002EE1C` : `entité.+0x28 = héros`.

`PlayerTryAction` @`0x8002EEAC` :
- Il prend l'entité de contact `hero.+0x130` si `(0x80126F2A & 0x80)` est vrai (front de Carré).
- Sinon il boucle sur les entités dont `+0x12C` est le héros (`0x8002EF64-0x8002EFC0`).

La liste de contact (collidables) vient de `0x800384F4-0x800385D0` : `(+0x6C & 0x80)`, `(+0xB4 & 0x80) == 0`, `+0x28 == 0`.

**MESURÉ** sur la vraie `InitializeEntity` (replay A) : l'en-tête donne `flags = 0x230180`. Le genre est donc 0, ni `0x200` ni `0x400`. `CanPickup = 1` ne fait que lever Gravity (`0x100`). L'article n'est pas soulevable tel qu'exporté. Appuyer sur Carré à son contact déclenche le coup d'épée (son 29). C'est le script C qui fait `|= 0x380` et passe à `0x230380`.

Les trois conditions, rejouées une à une (`SD/shop_replay.py`) :

| Replay | Conditions | Résultat |
|---|---|---|
| A | genre 0 (en-tête seul) | pas de levée, coup d'épée (son 29) |
| I | genre 1 + C4 mais Collidable effacé (`0x230300`) | pas de contact, pas de levée |
| C | genre 1 + Collidable, mais natif C resté à 255 (bouchon inerte) | **levée à t11 ; lancer à t61 ; `+0x28` jamais effacé** : « collé aux mains ». À t120 l'objet reste à (109, 100, 32), `t24 = 2`, `carrier28 = héros`, animation du héros 12. |
| B | genre 1 + Collidable + C4 réel | levée à t11, lancer à t61, atterrissage à t83 en (178,100,0), animation 0 à t84 (son 24), statut 2, drapeaux inchangés `0x230380` |

C4 est donc la condition qui relâche l'objet. Sans lui, un objet soulevé reste lié au héros.

### 3.2 Ce que fait C4 d'un article posé ou lancé
Désassemblage `0x80066984-0x80066BF4` (`g2c1-disc/critic/c4_dis.txt`) :
- `0x80066994-0x800669A0` : test `+0x28` (porté).
- Porté : `+0x24 == 0` donne l'animation 0 (garde le lien).
- `+0x24 == -1` : animation 3 ; si `+0x80 == 2`, `drapeaux = (drapeaux | 0x10) & ~0x80` (`0x800669BC-0x800669E8`). Les articles ont `+0x80 = 0` (E = 0), donc **leurs drapeaux ne changent pas**.
- Sinon : animation = `mot[0x800270C0 + 4*(+0x24)]`, `+0x24 = 0`, `héros.+0x2C = 0`, `+0x28 = 0` (`0x800669EC-0x80066A1C`).
- Libre (non porté) : rebond contre un mur pendant la montée, son 0x18 à l'atterrissage, glissade sur les cases `0x1001`.

C4 ne touche ni au statut, ni à la destruction, ni à la position d'origine. Il ne casse pas l'article. Le retour à l'étagère est entièrement fait par les scripts B (§2.3).

### 3.3 Les articles se brisent-ils (E2) ?
**Non.**
- Les en-têtes ont E = 0, c'est-à-dire un E0 en ligne (`RunSpriteEvent` : `DestroyEntity(e,-1)` seulement si le statut est « Désactivé »). Le programme E 2 n'est pas dans l'en-tête.
- La DLL n'enregistre E2 que pour `BreakSpriteTypes = {234, 290, 291, 292, 293, 294, 297, 304, 373, 438}` (`LiftableAi.cs:53`), aucun type global.
- `BreakEffect = 0` : aucun effet de casse dans le journal de mes replays.

Le statut 3 vient seulement de `5D [6]` (récipient de vie vendu en 258/298) ou de C4 « bloqué au sol sur une bande `0x1001` ». Dans les deux cas l'article est détruit sans casse.

### 3.4 Replays sur le vrai code
Script : `SD/shop_replay.py`. Sorties : `SD/out/shop_replay_*.txt`, `shop_b_table.txt`, `shop_h_table.txt`. Montage : vraie `MovePlayer`, vraie `SpawnWarpEntity` (type global 66), vrai C4 et vrai E0, bouchons inertes pour le reste. Le script C est simulé par deux écritures du harnais : `flags |= 0x380` et `+0x78 = 4`.

| Replay | Situation | Résultat mesuré |
|---|---|---|
| D | jeté sur une case `wk 0x04` (colonnes 7-8) | **disparaît** : statut 4 à t84, libéré à t85, sans effet de casse |
| E | jeté contre un mur (colonne 6) | s'arrête à 133, anim 0 à t84, reste posé |
| F | jeté sur place | atterrit à 143,5 (du départ à 110,5) |
| G | article sur un rebord de 16 px, héros au sol | **pas de levée** : le héros reste bloqué à x = 109 par le rebord, Carré donne le coup d'épée |
| H | le héros saute (Croix) sur le rebord puis lève | levée à t40, lancer à t81, atterrissage à t103 en (188,100,16), anim 0 à t104 |

G et H utilisent un rebord synthétique de 16 px (colonnes 5-7), même hauteur que les étagères des boutiques (case de hauteur 2 contre 1 pour le sol). **Conséquence pour le joueur** : il doit sauter sur l'étagère avant de soulever. G est un rejeu au sol ; H est le rejeu avec saut.

## 4. S3 : la DLL au HEAD (`4f1ad619`)

### 4.1 Faits
- **`0x41 [C,4]` est appliqué.** `EventProgramRunner.cs:865-873` (`SpriteProgramIndexes[v1] = v2`) et `:850-863` pour `0x40`. Le test d'arc `InoaAwakeningArcTests.cs:148-183` et `:287-290` épingle, sur les enregistrements 3, 4, 5, 6 et 8 de la 163 : `ProgramIndexes[C] == 0`, `SpriteProgramIndexes[0] == 0`, `SpriteProgramIndexes[2] == 4`, avant la frame 132.
- **`(C,4)` est absente du registre.** `NativeSpriteRegistry.CreateForProduction` (`NativeSpriteRegistry.cs:36-49`) enregistre : Beannoïde, Muruta, onde d'épée, objets ramassables A255/C255 (types 31 à 118 sauf 72, donc les 11 types), flèche chargée, flèche, pilier, C23 des 7 types et E2 des 10 types. Quand un article est à `(C,4)`, la clé est absente : c'est une opération sans effet comptée, avec une trace de débogage par clé (`RunSpriteEvent`).
- **Les articles sont des blocs inertes.**
  - Ils sont solides.
  - Après le script C, ils ont les drapeaux `0x380`.
  - `PlayerTryAction` est un bouchon (`PlayerManager.cs:955`).
  - Pas de levée, pas de ramassage par contact (le natif C255 est remplacé par `(C,4)`).
- **Pas d'autre chemin d'achat.**
  - Les seuls sites `0x99` des cinq cartes sont dans les B (`SD/itemops.py all`) : 163 B[2-4], 258 B[2-5], 298 B[2-5], 398 B[1-7]. Une exception hors-lecture : 398/C[2] @2256 `[115,8]` = 2 163 florins (réparation d'armure), qui passe par `83`/`84` sur l'objet 33 (dialogue, pas de levée).
  - Les programmes F de la vendeuse (258 : `05 [0,128]`) ne font que lancer des dialogues d'ambiance.
- **Tests existants** : `rg -i "shop|boutique|merchant|Lurvy"` ne trouve que `InoaAwakeningArcTests`. `rg Jess` ne trouve que des tests de boîte de dialogue. Rien sur les programmes B d'achat. Rien sur la 258, la 298, la 398 ou la 134 (hors un test de chargement sur une autre carte « Well … Lurvy unused »).
- **Sauvegardes de test** (noms seulement, `tools/Saves/`, 123 fichiers) : aucun nom ne contient boutique, shop, Lurvy, église, church ou well. Le seul nom d'un lieu Inoa est `131-inoa.sav`. Sa carte n'est pas lue (règle des noms seulement).

### 4.2 Opcodes des boutiques absents de la DLL
Script : `SD/opcov.py`, sortie `SD/out/opcov.txt`. Sur les programmes de boutique, trois opcodes n'ont pas de `case` dans `EventProgramRunner.cs` :
- `0x61` (« Is riding entity 2 »), employé par la 398 B[1] à B[7] ;
- `0xB2` (« Compare entity groups »), employé par la 258 et la 298 B[6] ;
- `0xB9` (« Start CD streaming »), employé par la 258, la 298 et la 398 pour le jingle du récipient.

Les autres opcodes sont portés : `07`, `70`, `8D`, `43`, `42`, `5C`, `44`, `50`, `51`, `36`, `82`, `99`, `9A`, `2D`, `2E`, `64`, `65`, `5D`, `55`, `85`, `8A`, `3B`, `2F`, `6E`, `53`, `B8`, `BA`. `0xBA` est implémenté (`:1445`).

Un opcode sans `case` passe par `UnknownOpcode` (`EventProgramRunner.cs:3109-3134`) : saut de la taille du table, `Result` inchangé.

### 4.3 Défaut déjà présent : la maison de Lurvy (carte 398)
INFÉRENCE solide. Modèle du flot de contrôle sur les octets exportés : `SD/sim398.py`, sorties `SD/out/sim398_head.txt` et `sim398_implemented.txt`.

Chemin pour B[1] (identique pour B[2] à B[4]) quand le héros est au sol :
1. `37`.
2. `70` donne `Result = 1`.
3. `04` ne saute pas.
4. `43`.
5. **`61` est sauté, `Result` reste 1.**
6. `42`.
7. `03` saute (`Result` = 1) vers @191.
8. @191 : `05 [132,131]`, `10`, `1A [0]`, puis `5C [0,166,1]` (« J'te conseille d'arrêter tes bêtises… »).
9. Retour à @152 : `2E [1]` détruit l'article, `2D [1]`, `64` le replace en z=320, puis on reboucle.

Avec `0x61` implémenté (le résultat 0 quand l'article n'est pas sur Lurvy), le même modèle donne : l'article est sur sa case d'origine, la case d'exclusion `07` est vraie, on reboucle sans dialogue (B[1] saute de @107 vers @80). Les cases d'origine des quatre articles correspondent aux cases d'exclusion des données.

Effet probable au HEAD : quatre programmes B se disputent le héros avec le même dialogue de réprimande, avec perte de contrôle à chaque cycle. Ce n'est pas rejoué dans la DLL (aucun test ni build autorisé). Il y a un `Wait [1]` par cycle et `RunMapEventsPass` n'ouvre pas d'autre dialogue pendant l'ouverture d'un premier (reentrancy gard). Je n'ai pas évalué si le joueur reprend la main entre deux cycles. Ce défaut existe quelle que soit l'option de g2c1 : il demande l'implémentation de `0x61` (ou de `0x60`/`0x61` par E19.sm1).

La 258 et la 298 n'ont pas de défaut équivalent : sans `0xB2`, les `Result` périmés ne mènent au garde que si le drapeau `0x81F4` est levé pendant que le héros est sur la tuile de porte, ce qui est improbable.

## 5. S4 : la boutique de la 163 bloque-t-elle l'histoire ?
**Non.** Quatre preuves :
1. **Texte.** La carte 163 n'a que 11 dialogues (`Inoa (inner)-163.yarn` : M163_S000 à S010 ; la 258 en a 58). Les identifiants 139 à 144 des B d'achat pointent des chaînes vides. Le 135 est « #Disuse ». Les 136 et 137 sont des phrases de Jess sans rapport. De plus, la vendeuse de la 163 (rec 1, F130) dit « Je m'appelle Jess… ». C'est un reste du gabarit « Inoa (inner) » copié d'une carte à l'autre.
2. **Atteignabilité.** Remplissage 4-connexe des cases libres (`SD/reach.py`, `reach2.py`). Les portails du jeu qui arrivent en 163 sont : 162 (30,26) vers (5,18) ; et par script, 416 C[1] vers (40,9) (`SD/out/warps_to_shops.txt`, `portals_163_258_298.txt`). Les portails 1 et 2 de la 163 relient seulement la maison de Jess à un second volume. Les portails 3 et 4 relient seulement la salle de boutique (x 3..14, y 39..55) à un autre volume. **La salle de boutique n'est reliée à aucune arrivée.** Elle ne rejoint la maison de Jess que si l'on ignore tous les bits de marchabilité (masque 0). La porte de la 162 en (31,34) mène en **258**, pas en 163.
3. **Chaîne.** Parmi les 30 cartes de la chaîne : aucun `0x83` (« nombre d'objets >= »). Les `0x84` (« utiliser ») visent les objets 74, 88 et 89, qui ne sont pas vendus. Les `0x82` visent 35, 73 et 87, jamais un article de boutique (`SD/out/itemops_chain.txt`). Les entrées de table 0 (« A[0] » à « F[0] ») dans cette sortie sont du bruit de balayage de l'en-tête de la carte, pas des programmes.
4. **Tout le jeu.** Les seuls `0x83` portant sur des articles vendus (36, 37, 38, 39, 41, 18, 20, 83, 51, 53, 55, 7) sont en 327 B[4] et 482 B[4]. Ce sont des choix de dialogue avant le combat final, pas des portes (`SD/out/itemcond.txt`).

Les boutiques qui fonctionnent sont la 258 (Inoa) et la 298/302 (Inoa détruite). La 258 est atteinte par la porte (31,34) de toutes les cartes extérieures d'Inoa. Aucune n'est dans la chaîne définie.

## 6. S5 : le coût en termes de jeu

### Option A : porter C4 pour les 11 types avec les 7 de g2c1
**Ce que le joueur voit :**
- **258, 298 et 398.** Il saute sur l'étagère, soulève un article (Carré), le porte, le jette sur le comptoir (2 à 4 cases). La vendeuse annonce le prix. Il répond Oui ou Non. Il paie. L'article réapparaît sur l'étagère (chute depuis z 160 ou 320 px).
- **Inoa 258/298.** Il ne peut pas sortir avec un article en main : mise en garde. C'est vrai seulement si `0xB2` est porté.
- **298.** La veste d'argent est gagnée en la soulevant.
- **398.** Réprimande quand il jette un article ailleurs que sur le comptoir. Les trois derniers articles n'apparaissent qu'après le drapeau 1009.
- **134.** Il peut soulever et jeter les quatre gags. Il entend toujours le refus.
- **163.** Rien ne change, la salle est inaccessible.

**Coût en code** (pour la partie C4 des 11 types) :
- Ajouter les 11 types globaux (et le 304 des 4 blocs de glace) à la clé `(C,4)`. Même code C4, goldens réutilisables (`g2c1-disc/c4_goldens.json`, 1 244 vecteurs).
- Étendre l'ensemble câblé de g1b (`ProductionWiredLiftableTypes`, pas encore au HEAD).
- Mettre à jour les épingles de somme du registre (`NativeSpriteRegistryTests.cs:57`, `LiftableAiRegistrationTests`, `ChargedArrowAiTests`, `WaveSwordLifeTests`).
- La fermeture D-G-8 est sans nouveau code C, D ou E : les articles ont D = 0, E = 0, F = 0.

**Coût réel pour que les boutiques marchent :**
- Portage de `0xB2` (petit : recherche + comparaison, `EntitySearchService` a déjà la fonction 11). `0x61` (et `0x60`) par E19.sm1 ou isolément. `0xB9` (jingle, peut rester ignoré avec une perte de son).
- Tests de monde sur la 258 et la 398 (aucun test d'achat n'existe).
- g1b actif (levée), g2a/g2b (lancer, E2).
- Un point de vérification de l'altitude : l'article doit atterrir à `TileZ == 2` et être mesuré `IsOnGround` sur l'étagère.

**Risques notés :**
- Tant qu'`UpdateVramFlags` n'est pas nourri pour les objets marqués (O-G-2), un article jeté dans un trou resterait posé, alors que l'original le détruit (replay D). Les cinq boutiques n'ont pas de trou.
- C4 ne retire pas Collidable : un article jeté dans un couloir étroit (la tuile de porte (5,46) de l'Inoa) le bloque. On peut le soulever à nouveau.

### Option B : les laisser immobiles (écart documenté)
**Ce que le joueur voit :**
- Les articles sont sur les étagères, solides, sans effet. Carré à leur contact donne le coup d'épée.
- **Aucun achat n'est possible** : plus d'herbes (36), de potions (41, 37, 39), d'extrait (38), de veste en cuir (18, 398 seulement) ni de récipient de vie (83) par les boutiques.
- Le récipient 83 reste obtenable par le troc de faucons de la 134 (dialogues, sans levée : `giveids` C[4], C[9], C[10], C[11]).
- Les sites d'autres cartes qui donnent 36, 37, 38, 39, 41, 18, 20, 83 existent (`SD/out/giveids.txt`). Je ne les ai pas qualifiés comme alternatives équivalentes.
- **298** : la veste d'argent gagnée en la portant ne peut pas être gagnée. D'autres sites donnent l'objet 20 (cartes 17, 302, 439).
- **398** : le défaut de `0x61` (§4.3) subsiste et doit être traité de toute façon.
- **134** : le gag à 10 000 florins n'est jamais vu.

**Coût en code** : zéro côté C4. Un texte d'écart (par exemple « O-G-boutique ») et, pour la 398, l'implémentation de `0x61`.

### Effet sur la chaîne d'histoire
Aucun, dans les deux cas : la salle de la 163 est inaccessible. Les 5 articles de la 163 passent à `(C,4)` au premier tick et ne sont pas visités.

### Avis (INFÉRENCE, hors périmètre de la question)
Si l'objectif est la conversion totale, (A) est la voie fidèle. La partie C4 des 11 types coûte presque rien en plus des 7 de g2c1. Les opcodes `0xB2`, `0x61` et les tests de monde sont une tranche à part. Les boutiques n'étant requises par aucun programme de la chaîne, rien ne force à les inclure dans g2c1. Une option intermédiaire : inscrire les 11 types dans `(C,4)` et dans l'ensemble câblé avec g2c1 (fermeture propre, aucun objet « collé »), et traiter les opcodes de boutique dans une tranche qui suit.

## 7. Écarts avec les rapports précédents (`g2c1-disc/report-main.md`, `critic-main.md`)
1. « La 163 (boutique de Jess) est sur la chaîne » : vrai pour la carte, **faux pour la boutique**. Sa salle est inaccessible et ses textes sont vides (§5).
2. **Défaut présent au HEAD** : la 398 passe par une réprimande en boucle à cause de `0x61` (§4.3). Absent des rapports précédents.
3. **Le genre de ramassage n'est pas dans l'en-tête** : `CanPickup = 1` donne le genre 0. Le script C l'écrit (`0x62`).
4. **Soulever demande de monter sur l'étagère** (replays G et H).
5. **Le critique** parlait de « 163 : 5 articles » ; il y a en fait 26 articles sur 5 cartes dont 4 fonctionnelles (258, 298, 398, 134 pour le gag).
6. `0xB2` et `0xB9` s'ajoutent aux manques d'opcodes.

## 8. Non établi
- Ce qui arrive au héros et à l'article quand on sort par un portail avec l'article en main (398, portail (7,31)) : aucune lecture du code de chargement de carte.
- Si le défaut de la 398 laisse au joueur une marge de contrôle entre deux cycles (il faudrait rejouer la DLL).
- Le sens exact du bit `0x8000` posé par la 258 A[4] (`62 [128,0,128]`) sur le bloc transparent : sans doute « cible d'interaction ». Je ne l'ai pas vérifié.
- Si la DLL gère à l'identique les marqueurs `[flag id=100/]` pour les 4 textes de prix de la 258 (le Yarn exporté les contient, 4 occurrences). Le mécanisme `[flag]` existe (E19.f0).
- Les sauvegardes de test qui arrivent sur ces cartes : seules les noms sont lus.
- Les sites alternatifs pour l'objet 20 (cartes 17, 302, 439) ne sont pas qualifiés.

## 9. Fichiers
Scripts (`SD/`) : `scan_shop.py`, `dumpall.py`, `dumpmap.py`, `strings.py`, `portals.py`, `warps.py`, `reach.py`, `reach2.py`, `cellmap.py`, `itemops.py`, `itemcond.py`, `giveids.py`, `opcov.py`, `shopsummary.py`, `sim398.py`, `g66.py`, `shop_replay.py`. `SD/SHA1SUMS.txt` : 59 fichiers.

Sorties clés (`SD/out/`) :
- `scan_shop.txt`, `shopsummary.txt`, `opcov.txt`, `portals_163_258_298.txt`, `portals_398_134.txt`, `warps_to_shops.txt`
- `events163_all.txt`, `events258_all.txt`, `events298_all.txt`, `events398_all.txt`, `events134_all.txt`
- `itemops_chain.txt`, `itemcond.txt`, `giveids.txt`
- `sim398_head.txt`, `sim398_implemented.txt`
- `dis_search_case11.txt`, `dis_8003C954.txt`
- `shop_replay_a_header_only.txt` à `shop_replay_i_kind1_but_not_collidable.txt`, `shop_b_table.txt`, `shop_h_table.txt`

Copie du harnais d'oracle : `SD/work/orc` et `SD/work/emu`. L'outil capstone vient de `C:/Users/casad/AppData/Roaming/Python/Python314/site-packages`. Commande de rejeu, depuis `SD/` : `python -B -I -X utf8 shop_replay.py <scénario>`.

Lectures de la DLL citées : `Alundra/Events/EventProgramRunner.cs` (lignes 289-330, 850-873, 1253, 1522-1558, 2398, 2514-2556, 3109-3144, 3306), `Alundra/Entities/EntitySearchService.cs`, `Alundra/Ai/NativeSpriteRegistry.cs:36-49`, `Alundra/Ai/Objects/LiftableAi.cs:53`, `Alundra/Ai/Objects/PickupAi.cs`, `Alundra/Hero/PlayerManager.cs:955`, `Alundra.Tests/Arcs/InoaAwakeningArcTests.cs`.