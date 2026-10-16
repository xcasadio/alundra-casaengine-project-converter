# Vérification de e20e (E20.e, la coupe de l'herbe, `0x8003A7B0`)

Je n'ai pas écrit `report.md`, car la consigne système interdit les fichiers de rapport. Ce texte est donc le rapport. Mes scripts et listings sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e20e/`. Les principaux sont `cate.txt`, `emu2.py`, `emu3.py`, `callers2.py`, `callgraph.py`, `loot.py`, `census2.py`, `fieldscan.py`. Je n'ai rien écrit dans le dépôt ni dans le binaire. Le HEAD est bien `9502f75`.

## Verdict

**Le rapport est fiable pour écrire les tâches de la tranche.** Le cœur (pseudo-code de CATE, appelants, effet, butin, son, données) est confirmé par une relecture indépendante du binaire. Les erreurs trouvées sont mineures. Elles sont listées en 3 et 4, avec quelques manques en 5.

## 1. Confirmé (adresses relues dans `ALUN_CD.EXE`)

- **CATE.** Je relis toute la fonction `0x8003A7B0-0x8003A9DC` :
  - Le garde `+0x1D4 == 0` est à `0x8003A7E8`.
  - L'ordre des 4 coins est (colA,rowA), (colB,rowA), (colA,rowB), (colB,rowB), d'après le tableau de pile `0x18..0x34(sp)` et le pas de 4.
  - `colA = T[lh 0x1FE]` et `colB = T[(+0x1FC + +0x214) >> 16]`, sans borne. `rowA = +0x200 >> 20` et `rowB = (+0x200 + +0x218) >> 20`.
  - Le clamp est 0..0x33 pour les colonnes et 0..0x3B pour les rangées, par `blez` et `slti`.
  - L'adresse de la case est `[0x800DD86C] + 0x604 + r*0x1A0 + c*8`.
  - Le seul bit testé est `andi 2` (`0x8003A8F4`).
  - `zc = (u8[+3] << 20) + 0x80000`. On saute si `zc < bz` ou si `bz + bh < zc`, en `slt` signé.
  - Écritures : `sh (flags & 0xFFFD)` puis `sh 0xFFFF` en `+6`. Rien d'autre ne change.
  - Position : `X = col*0x180000 + 0xC0000` et `Y = row*0x100000 + 0x80000`. La colonne et la rangée utilisées sont celles **non bornées**, rechargées depuis le tableau de pile.
  - `CreateEffectEntity(0, [[0x800E4334]+9], 0, X, Y, z0)` reçoit `z0` **sans** le +0x80000.
  - Puis `0x80032C7C(0xFF, X, Y, z0)`, puis `0x800490FC(0x1F)`.
- **Émulation indépendante.** J'ai écrit mon propre interpréteur MIPS (`emu2.py`), qui décode les mots bruts. Sur 6000 vecteurs il donne 0 écart avec le pseudo-code du rapport, avec 4146 coupes. Les vecteurs incluent des Z exactement sur les bornes (±1, ±bh), des cases hors table, des coordonnées négatives et des flags variés. Le « 0 écart » du rapport est donc corroboré.
- **Quatre appelants, pas d'autre.** `jal` à `0x8002FDC0`, `0x8007A960`, `0x8007C0E4`, `0x8007F2D4`. Je n'ai trouvé aucun `j`, aucune paire `lui/addiu` et aucun mot de données pointant vers `0x8003A7B0`.
- **Contexte de ces appelants.**
  - La table `0x800228AC` envoie bien 15 états vers `0x8002FDBC` : 04, 10, 12, 14, 16, 18, 19, 3F, 40, 41, 42, 44, 46, 49, 4B. Elle correspond exactement à `IsGrassCutState` (`AlundraPlayerAnimationEffects.cs:191`) et à l'énumération du décompilé.
  - Le test du mode (`beqz s5` à `0x8003020C`) vient après CATE. `0x8002FB14` est appelée à `0x80031BDC` (mode 1) et `0x80031C3C` (mode 0).
  - `exelib.handler` donne C61 = `0x8007A958`, C94 = `0x8007C0D8`, E12 = `0x8007F27C`, C255 = `0x8007C174` et A255 = `0x80061BD4`. `sprite-records.json` confirme les sprites 27-29, 7 et 2.
  - C61 est bien une enveloppe de 8 instructions. C94 et E12 sont corrects dans tous leurs détails.
- **Boîte de coup.**
  - `0x80038064` est le seul écrivain de `+0x1FC/+0x200/+0x204` pour les entités du jeu. Les deux autres écrivains (`0x80061580`, `0x80097C78`) travaillent sur d'autres structures.
  - Appels à `0x800384BC` (dernière boucle de `0x80038364`) et à `0x80039F30`.
  - Le chargeur de frame `0x80038C44-0x80038CB4` pose les offsets en octets signés `<<16` et les tailles en `(n<<16)-1`.
- **Table T.** Elle est bâtie à `0x8003B190-0x8003B1D4` avec T[p] = p/24 pour 0..1247 (52×24).
- **Qui lit le bit 0x02.**
  - Je retrouve les 13 sites `lw -0x2794` et les 13 lecteurs de `+0x148..+0x154`.
  - Les masques vus sont `0x40|0x01|0x1000`, `0xE00` et `0x1001`.
  - Seuls CATE et l'opcode 0x9E testent le bit 2. La table d'opcodes est à `0x80098FAC` et 0x9E vaut `0x80040BB4` (test à `0x80040C1C`).
  - Les opcodes 0x54/0x55 (`0x8003ED24`, `0x8003EDC4`) font OR/AND sur le mot de 16 bits.
- **Rendu.** `0x8002CDA0`, appelée une fois par image à `0x8002BDB8`, vide puis reconstruit la liste des tuiles depuis les cases vivantes. Elle saute la pile quand `+6 == 0xFFFF` (`0x8002D288`). La disparition du buisson est donc immédiate.
- **CreateEffectEntity.**
  - `0x8003BDD8 → 0x8003B9C4 (128 emplacements, base 0x80138608, pas 0x80) → 0x8003BB14`.
  - Le gabarit est à `0x8013C608` (0x80 octets). Les écritures sont celles du rapport : `+0x30=-1`, `+0x34=0`, `+0x68=2`, `+0x6C..+0x71`, `+0x3C/40/44`.
  - Pas de nombre aléatoire et pas de son. Elle retourne 0 si la table est pleine, et CATE ignore ce retour.
- **Le butin.**
  - `0x80032C7C` est conforme, y compris `+0xB8 = 0xA0000` (posé dans le delay slot) et `600` si l'octet 4 de la table est non nul (l'octet 4 est non nul pour 14 objets, comme annoncé).
  - Le veto `MpMax` (`0x80032A00`) ne s'applique qu'aux objets 0x26, 0x51 et 0x52. Les objets de l'herbe ne le déclenchent jamais, donc ce veto est du code mort pour CATE.
  - `0x80032968` : un seul pas du générateur par maillon (`0x7D2B89DD`, `0xE06A02E7`, mot `0x80098708`, valeur initiale `0xB017C93D`), puis `table 0x80028E0C[(c&0x7F)*16 + (s>>28)]`.
  - La distribution recalculée est 224/256 rien, 9/5/2 pour 0x45/46/47 et 12/4 pour 0x54/55.
  - `0x80032B90` n'est pas appelée par CATE. Ses appelants sont `0x8003A5B0`, `0x8003A6D0` et `0x800670F8`.
- **RNG.** Mon parcours du graphe d'appels (`callgraph.py`) montre que la seule fonction qui touche au générateur, dans tout le graphe de CATE, est `0x80032968`. SpawnWarpEntity, CreateEffectEntity et PlaySoundEffect n'en consomment pas. Sur les chemins directs, une case coupée coûte donc 1 ou 2 mots, et rien de plus pour ces 5 objets.
- **Données.**
  - Je retrouve en indépendant : 483 cartes en 52×60, octet 9 = 2 partout, 9089 cases dans 165 cartes, 8989 avec pile (100 sans), jamais de bit 0x40 avec le bit 2, hauteurs 0..29.
  - La carte 14 a 179 cases dont 177 avec pile. Les formes de piles sont 8/8630/332/19. Il y a 163 881 piles au total.
  - L'effet global 2 a bien 1 animation de 9 images de 5 ticks, fin `Destroy`.
  - Les sprites 99, 100, 101, 114 et 115 ont `Contents=0`, `ProgramLoad=255` et `ProgramTick=255`.
  - Les 483 `.tileMap` portent `BalanceLevel: "2"`.
- **Divergences avec le décompilé.**
  - D1 (garde de table), D2, D3 (`rowB` divisé deux fois par 16), D4 (+0x80000 en trop), D5, D6 (toutes les cartes font 52×60), D7 (deux tirages par maillon dans `ChooseRandomlyAnItem`) et D8 sont tous exacts.
  - Pour D3 et D4, j'ai transcrit le décompilé dans `emu3.py`. Corriger D3 et D4 efface tous les écarts sur colonnes dans la table, sauf un cas (D9, ci-dessous). Les deux sont donc bien les causes dominantes.
- **Citations DLL.** J'en ai vérifié environ 25, dont `AlundraWaveSwordAi.cs:31/34-37`, `AlundraPlayerAnimationEffects.cs:61-63/191/214`, `AlundraPlayerManager.cs:324/337/1203-1206`, `AlundraCellStore.cs` (clamp 0x33/0x3B) et `IEntityWorldContext.cs:70/105/199`. Aucune n'est fausse.

## 2. Non vérifiable ou à reclasser en INFÉRENCE

- « La carte est relue du disque à chaque chargement » est présenté comme FAIT. Je confirme seulement que CATE n'écrit aucun état persistant. Le rechargement lui-même est une inférence.
- Les noms I69 à I71 et I84/I85, les coûts d'overlay (1816+2360, 1360) et la liste des 100 cartes sans pile ne sont pas dans le binaire. Je n'ai pas contesté ces points (les 100 cases sans pile sont confirmées).
- Les chiffres « 414 / 264 / 150 » du mode `decomp` ne sont pas reproduits tels quels. Mes propres chiffres vont dans le même sens (cf. D3 et D4).
- Le nom `MpMax` pour le demi-mot `+6` de `[0x80176318]` est une inférence. Il est sans conséquence pour CATE.

## 3. Erreurs et imprécisions du rapport

1. « 149 instructions » est faux. Il y a **140** instructions (149 est le nombre de lignes du listing, labels compris).
2. « Hors de la plage, BSS non définie » est imprécis. Juste après T (`0x80127B28`, soit l'index 1248) commence le **tableau de pointeurs d'entités** (64 entrées, compteur à `0x80127D28`). Il est reconstruit à chaque image par `0x800384F4`. Juste avant T (`0x80127164`) se trouve un mot global lu par plus de 30 sites. Pour `(bx+bw)>>16 >= 1248`, `colB` est donc une moitié de pointeur d'entité : par exemple un nombre positif `>0x33` qui se borne à 51, ou une moitié haute négative qui se borne à 0, c'est-à-dire la colonne de gauche. C'est déterministe pour une disposition donnée. Cela précise Q-E7. Pour les index négatifs, il s'agit du mot `0x80127164`, valeur inconnue.
3. Le masque de marche « `0x40|0x01|0x1000` aux trois sites » est faux pour le troisième, à `0x8003AA68`. Là, le masque est `(flags&8 ? 1 : 0) | (flags&1 ? 0x1000 : 0)`, sans base `0x40` (calcul de hauteur du sol, `0x8003A9E0`). La conclusion « le bit 0x02 n'y est pas » reste vraie.
4. La liste « Tests qui épinglent les bouchons » oublie `AlundraHeroChargeTests.cs:661-669`, qui pose aussi `StubCallsForTests`.

## 4. Divergence manquante (D9)

La division du décompilé est tronquée vers zéro (`/16`). Le binaire utilise un décalage arithmétique (`>>20`), donc un plancher. Pour une boîte dont `by < 0` (au-dessus de la rangée 0), la rangée vaut −1 dans le binaire. Les cases testées sont les mêmes (borne à 0), mais la **position Y de l'effet et du butin** vaut `-1*0x100000 + 0x80000 = -0x80000`, contre `+0x80000` dans le décompilé. Le pseudo-code du rapport est juste (rangée non bornée), mais D9 est absente de sa liste.

Exemple de mon résidu : `box = [0x28f4322, 0xfff2998e, 0x132079, 0xfffff, 0xfffff, 0x400000]`. Le binaire donne Y = 4294443008, le décompilé corrigé donne 524288.

## 5. Ce qui manque au rapport

- **Ordre dans l'image.** L'ordre est `0x8003B388 : 0x800386D0` (tick du héros, qui appelle `0x80031B50` à `0x800386EC`), puis `0x800384F4`, puis `0x80038E18` (avance de frame `0x80038AB4`, appelée à `0x80038E4C`), puis `0x80038364` (stockage des boîtes). Dans les ticks des appelants (héros, C61, C94, E12), CATE lit donc la boîte stockée à la **fin de l'image précédente**. L'oracle de e-T0 doit couvrir cette latence d'une image. Une entité née dans l'image courante a sa boîte stockée à la création (`0x80039F30`).
- **Boîtes exportées (INCONNU du rapport).** Les `.anim2d` de l'épée d'onde Niv.1, des flèches chargées et de l'animation 1 de la bombe contiennent des données de collision. L'existence est donc établie. Leur cohérence reste à contrôler dans e-T0.
- **C255, rebond.** Le rapport omet le seuil d'arrêt : `+0x284 = (+0x284*12)>>4`, puis remis à 0 si le résultat `≤ (u16 [0x800E4334]+4) << 8`. Ce point appartient à e0c-T0, mais le rapport le donne comme complet « en tête ».
- **A255 à la création.** Quand l'objet naît, `+0x274` vaut encore 0 (SpawnRandomLoot ne l'écrit qu'ensuite). Le chemin de `0x80061C60` s'exécute donc, puis les écritures de SpawnRandomLoot l'écrasent. Pas de conséquence visible, mais à noter pour e0c.

## 6. Conclusion pour la rédaction des tâches

On peut écrire les tâches e-T0 à e-T5 à partir de ce rapport, avec les corrections 3.1 à 3.4 et 4. Deux compléments sont à intégrer à e-T0 : la latence d'une image (5) et le comportement exact hors table (3.2), qui ferme Q-E7. Q-E6 (repère de Z) reste ouverte, mais elle est bien posée.

Fichiers de référence : `.../check-e20e/emu2.py` et `emu3.py` (émulation et ablation), `callers2.py`, `callgraph.py`, `fieldscan.py`, `loot.py`, `census2.py`.