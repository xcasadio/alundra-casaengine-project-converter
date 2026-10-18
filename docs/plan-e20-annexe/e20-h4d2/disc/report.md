# Rapport E20.h4d2 — le feu 1 (objet `0x2F`) : pilote `0x80035A84`, C63, E3 pour le type 23, annulation `0x80034D2C`

Découverte en lecture seule du 2026-10-18. Dépôt lu à `ed60a0eb`. Aucun changement sous `Alundra/`, `Alundra.Tests/`, `alundra-project/Data` depuis `f81304b` (`git diff --stat` vide). Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8da…` vérifié.

Le tool a refusé l'écriture de `report.md` et de `check.md` : ce message en tient lieu. Les preuves sont sous le dossier de sortie, appelé `OUT/` :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el2/h4d2/`.

Rien n'a été écrit hors de `OUT/`. Aucun build, test, export ni `git` d'écriture. Python est toujours lancé avec `-B`, depuis des copies.

Légende : **FAIT** (vu dans le code, le binaire ou les données, avec adresse ou fichier:ligne), **INFÉRENCE** (déduit, de quoi dit), **INCONNU**. `h4/` = `docs/plan-e20-annexe/e20-h4/`. `S` = tick de naissance de la colonne (34).

## 0. Résumé : ce que cette découverte change au plan

1. **Les faits du binaire du feu 1 tiennent** (`h4/report.md` §3-§5, `h4/check.md`). Je les ai relus avec mes propres listings capstone et mes propres modèles (pilote, garde, annulations, C63 avec requête et tri, E3).
   - Les modèles rendent 0 écart contre le vrai code MIPS sur 7 482 vecteurs.
   - Ils couvrent 100 % des instructions de chaque fonction (§3.4).
   - Quatre précisions de lecture corrigent le texte de la demande ou du plan (§1, « Corrections de lecture »).
2. **Les clés du feu 1 de h4 sont fausses d'un tick** (R-EL-1). J'ai refait **30 clés** : index de case écrit, `+0x48 = −1`, pas par image `_pf`. Le manifeste est identique sur deux exécutions.
   - Colonne et première boule au tick `S = 34`.
   - 11 boules (ticks 34, 42, … 114).
   - Colonne morte au tick 121, héros en `0x34` au 122.
   - Objet libéré au **149** (h4 disait 150).
   - Dernière entité du sort disparue au 193.
3. **Le champ stocké de h4b1b n'est pas nécessaire pour rejouer ces clés en mode binaire.** Sur 494 appels de la requête, le compte stocké `[0x80134360]` égale le plus haut emplacement vivant calculé à la demande (0 écart, §3.3).
4. **Un seul réglage neuf pour l'auteur** : le tour de C63 sans plus court chemin (Q-H4D2-1, défaut : corriger).
   - Il touche toutes les clés où une boule vise une cible : 57 tours sur 321 (≈ 18 %) prennent le long chemin.
   - Les deux autres défauts (borne exclusive, tri dès 3) ne changent que **deux** scénarios construits pour eux (§3.2).
5. **Dépendances** : f5 (D-F5-5), mais ni f6a ni f4.
   - La boule (`0x102074`, `DestroyOnSlidingSlope` **sans** `Gravity`) n'est pas détruite sur l'eau profonde. La clé sur l'eau est identique à la clé plate.
   - Au tick d'E3, la décélération de l'animation 1 s'applique tout de suite : c'est la règle D-F5-5.
6. **E3 pour le type 23 ne s'inscrit pas par un second `Register`** : un doublon lève une exception (`NativeSpriteRegistry.cs:60-63`). h4d2 passe `EarthTome1Ai.SpriteTypes` de `{16}` à `{16, 23}`.
7. **Interruption après la mort de la colonne** : la phase 2 n'a pas de garde. Une blessure aux ticks 124 ou 140 libère l'objet mais **n'annule pas** les boules en vol, qui vivent jusqu'au tick 193.

## 1. Faits décisifs du binaire

Colonne « relu » : L = mon listing `OUT/lst/*.txt` ; M = mes modèles `OUT/tools/f1model.py` contre le vrai code ; V = vecteurs ; K = clé du monde. Tous FAIT sauf mention.

| # | Fait | Source | Relu |
|---|---|---|---|
| F1 | Le pilote `0x8003634C` envoie l'objet `0x2F` à `0x80035A84` (table `0x800235F4[16] = 0x8003641C`, `jal 0x80035A84`). Il incrémente le compteur `[0x8012715C]` **après** l'appel (saturé à `0x7FFFFFFF`) et remet `[0x80127164]` à 0 si le pilote rend non nul. | h4 report §1, check §2 | L `wrapper_8003634C.txt`, `tables_checked.txt` ; K (compteur lu 0 au premier appel, tick 2) |
| F2 | L'annulation générale `0x800350C0` aiguille par `0x800233EC` : `0x2F` donne `0x80035160`, soit `jal 0x80034D2C`, puis `[0x80127164] = 0`. | report §1 | L `cancel_dispatch_800350C0.txt` ; M |
| F3 | Phase `[0x80127160]` : 1 → phase 1 ; 0 → phase 0 ; 2 → phase 2 ; toute autre (négative, ≥ 3) → rend 0. | report §3 | L, M, V |
| F4 | **Phase 0.** Garde `0x800351B8` : pente `+0x18C == 4` ou animation hors `{0x32, 0x33}` → annulation générale, rend 1. Animation ≠ `0x33` → rend 0 (la pose `0x32` dure 32 ticks). Sinon `SpawnWarpEntity(héros, table 0, type 0x14, PosX, PosY, PosZ, direction 0)`. Le **pointeur est rangé en `[0x8012711C]` avant le test de nullité**. Nul → rend 1 (objet libéré, animation inchangée, PM non rendus). Sinon l'identité `[ptr+4]` va en `[0x80127120]`, phase + 1, et la **phase 1 s'enchaîne dans le même tick** (D7). | report §3, check §2 | L, M, V, K (tick 34) ; `fire1_table_full_pf` (libéré au 34) |
| F5 | **Phase 1.** Garde d'abord (non nulle → rend 1, **sans** restauration : le feu 1 n'a pas de tampon). Puis `[[0x8012711C]+4]` est comparé à `[0x80127120]` : **différents** → animation du héros `0x34`, phase + 1, rend 0 ; égaux → `compteur & 7 != 0` rend 0, sinon le tirage. Le compteur lu est celui d'**avant** l'incrément. | report §3 | L, M, V, K |
| F6 | **Tirage et boule.** `s ← s·0x7D2B89DD + 0xE06A02E7` rangé ; direction = `hi(s·32)` (0..31, non normalisée). `SpawnWarpEntity(héros, 0, type 0x17, PosX, PosY, PosZ + 0x100000, direction)` : les 16 px sont **en Z** (`0x80035BD4-0x80035BE0`), la boule naît à l'(X, Y) du héros. Non nul → `ForceZ(+0xB8) = 0x80000`, `+0x274 = 0x10`, `+0x278 = 0` (`+0x27C` non écrit) ; rend 0. Le tirage est consommé **même si la naissance est refusée**. | report §3 ; la demande dit « hero + 16 px » | L, M, V, K `fire1_table_one_free_pf` (11 tirages, aucune boule) |
| F7 | **Phase 2** sans garde : animation `0x34` → rend 0, sinon rend 1. | report §3 | L, M, V |
| F8 | **Calendrier** (K `fire1_cast_pf`, Rond au tick 1) : voir la liste ci-dessous. | report §3, plan R-EL-1 | K |
| F9 | **Annulation `0x80034D2C`** : emplacements 1 à `[0x80134360]` **inclus** (signé, ≤ 0 → rien), entités au **statut 2** seulement. Type `0x14` : `CreateEffectEntity(0, 0x13, 2, X, Y, Z)` à la position brute de la colonne, **puis** `DestroyEntityNoFx` (`0x8003A774` : statut 4, `+0x228 = −1`, effet attaché `+0x1B8`, porteur `+0x28`). Type `0x17` : statut 3, `+0x20 = 0`. Autres statuts et types intacts. | report §1 | L, M, V (400, 55/55 mots), K `hurt{35,60,100,121}` : effet `(19, 2, 6553600, 6553600, 1179650)` au tick T |
| F10 | **C63 `0x8007AC60`, ordre.** Voir la liste ci-dessous. | report §4 ; le plan dit « puis `+0x274 = 2` » | L, M, V (3 524, 100 %), K |
| F11 | **Le tour** (`0x8007AD58-0x8007ADBC`). Voir la liste ci-dessous. | report §4, plan Q-H4-2 | L, M, V (grille 32×32), K |
| F12 | **Requête** `0x8003AF70` (h4b, critique faisant foi) : 4 appelants. Balaie `1..compte−1` (borne exclusive) ; garde les statuts 2 et 3, `+0x20 == 0`, `flags & masque`, `+0x1D0 == 0`, `+0x1CC == 0`, `+0xB4 & 0x40 == 0`, fiche `+0x1C4` non nulle, réponse `octet[fiche+type+2] & 0xC0 != 0x80`. Clé `dx²+dy²` (`>>16`). Tri de Hoare **dès 3 résultats** seulement. Propre au feu 1 : une cible touchée (`+0x1D0 = 25`) disparaît de la liste 25 ticks ; la colonne et les boules ne sont pas candidates (bit ClassA `0x1` clair). | h4b `critic/check.md` §4 | L, M ; K `fire1_tgt_one_pf` (touches aux ticks 93, 121, 146) |
| F13 | **E3 `0x8007EDA0`** : `+0x88 == 1 && +0xAC == 1` → `DestroyEntity(self, −1)` ; sinon `+0x88 = 1` et `flags = (flags \| 0x40) & ~0x30` (`0x102074 → 0x102044`). | report §4 | L, M, V (288, dont `0x102074`), K |
| F14 | **Registre** : A254 = `0x80061BCC` (vide), C63 = `0x8007AC60`, E1 = `0x8007ED10`, E3 = `0x8007EDA0`. La colonne n'a **aucun** gestionnaire à porter (E1 en ligne). | report §4 | `tables_checked.txt` |
| F15 | **Sprite 20 (colonne).** Drapeaux `0x2044` (`HitsClassA`, `DeactivateOnAnimationEnd`, `NoObstacleSlide`) ; A0 C0 D0 E1 ; `BreakEffect` 0. Z de naissance = Z du héros + 1 unité brute + 18 px (`OffsetZ −18`). Une animation de 87 ticks, fin `Hold`. Boîte 23×16×47. Fiche 20 : type 10, **puissance 64**. | report §5 | export + `map_alundra.json` ; K |
| F16 | **Sprite 23 (boule).** Drapeaux `0x102074` (`HitsClassA`, `DeactivateOnImpact`, `DeactivateOnHit`, `DeactivateOnAnimationEnd`, `NoObstacleSlide`, `DestroyOnSlidingSlope`), **sans `Gravity`** ; A254 C63 D0 E3. Anim 0 : vitesse 256, accélération 68 (`&0xF` = 4), `Loop`. Anim 1 : vitesse 0, accélération 4, `Hold`, 10 ticks. Boîte 23×15×15. Fiche 23 : type 10, **puissance 32**. | report §5 | export + extraction ; K |
| F17 | **Physique de la boule** : voir la liste ci-dessous. | — | K `cast_pf`, `wall_col6` |
| F18 | **Touches.** La colonne touche une cible tous les 25 ticks (hit-pass aux ticks 34, 59, 84, 109 ; dégâts 64). La boule meurt à sa **première** touche (`DeactivateOnHit`). Chaque touche = 3 étincelles (effet 9) + effet attaché 4. Bords de la colonne pour un bloc 25 ClassA : touché pour x ∈ [76, 122] à y = 100 et y ∈ [85, 115] à x = 100 ; pas à 75, 123, 84, 116. | report §5 | K `fire1_col_hit_pf` + balayage |
| F19 | **Aucun son** (naissance, E3, annulation). Effets : `(19, 2)` à l'annulation de la colonne ; étincelles (9) et attaché (4) aux touches. XA du départ : octet `0x80023365 + 0x2F` = **2** (Q-H4-1, bouchon de h4a). | report §1 | K ; `tables_checked.txt` |
| F20 | **Eau profonde** : la boule ne reçoit jamais la pente 4. La clé sur l'eau est identique à la clé plate (0 tick × entité différent). | plan §1.22.1 (s1 « sans gravité (0) ») | K `water_cols6_12` |
| F21 | **Annulation après la colonne.** T = 122 : colonne déjà recyclée (pas d'effet), boules au statut 2 → 3. T = 124 et 140 : objet libéré, boules **non** annulées (dernière entité au 193). | report §6 ; nuance de check §2.5 | K `hurt{122,124,140}` |
| F22 | **Compte stocké** `[0x80134360]` = plus haut emplacement de statut ∉ {0, 4} à chaque appel de la requête : 494 appels, 0 écart. | check §3.1 | `h4o/probe_count.py` |

**F8, calendrier** (graine `0x12345678` au tick 1, 1 tick par image) :
- Tick 1 : objet en cours, PM 4→3, animation `0x32`.
- Ticks 2-33 : phase 0, compteur lu 0..31.
- Tick 33 : animation `0x33`.
- **Tick 34 (S), compteur lu 32** : colonne (emplacement 1) et boule 1 (emplacement 2). Tous deux sont au statut 2 à la fin du tick.
- Boules aux ticks 34 + 8k, k = 0..10 : **11 boules**.
- Colonne : `+0xAC` au tick 120 (S+86), statut 4 au 121 (S+87, E1 en ligne, aucun effet de bris), case recyclée et identité changée au 122 (S+88) → héros `0x34`, phase 2.
- Héros en animation 0 au 148. **Objet libéré au 149** (S+115).
- Dernière entité du sort disparue au 193 (193 aussi avec la graine B).
- Totaux : 115 tirages (11 directions + 104 essais sans cible).

**F10, ordre de C63** :
- `+0x274 != 0` : `+0x274 −= 1` et c'est tout, sans écrire `ForceZ`.
- Sinon `ForceZ = 0xFFFD0000` (−3 px/tick, à chaque tick où le délai est nul).
- `+0x278 == 0` : requête `(self, masque 1 = ClassA, type = ([+0x1C8] octet 0) & 0xF)`.
  - Aucun résultat : `+0x274 = hi(s·9)` (0..8, un tirage) et fin.
  - Un résultat : `+0x278` = le **premier**, `+0x27C` = son identité, et **le tour s'exécute dans le même tick**.
- `+0x278 != 0` : cible au statut 2 **et** identité égale, sinon `+0x278 = 0`. `+0x274` reste 0 : la requête repart au tick suivant, sans délai.
- La naissance du tick S ne fait pas tourner C63 (statut 1) : `+0x274` vaut 16 à S, 15 à S+1, 0 à la fin de S+16. **Première action à S+17.**

**F11, le tour** :
- `d = GetDirectionToTarget(dxcible − dxboule, dycible − dyboule)` sur les différences brutes 16.16.
- `d` égale à la direction courante `+0x8C` : **rien n'est écrit, `+0x274` reste 0**.
- Sinon comparaison **numérique** : `d < cur` donne `d` si `cur − d < 4`, sinon `cur − 4` ; `d > cur` donne `d` si `d − cur < 4`, sinon `cur + 4`. Puis `+0x8C` est écrit et `+0x274 = 2`.
- Jamais de bouclage : de 31 vers 0 la boule fait 31→27→…→3→0, huit pas.

**F17, physique de la boule** :
- Montée à +8 px/tick de S à S+16 (`ForceZ` constant, pas de gravité), puis −3 px/tick dès S+17.
- XY : rampe `fx` de −8 688 par tick jusqu'au plafond de la direction.
- Atterrissage (`IsOnGround`) au tick L ; statut 3 et E3 au tick L+1.
- `+0xAC` à statut 3 + 10, statut 4 au tick suivant.
- Mur et bord de carte : statut 3 en l'air, z de 14 à 98 px dans les clés.

**Corrections de lecture** :
- (a) F6 : les 16 px sont en Z.
- (b) F10-F11 : `+0x274 = 2` n'est posé que si la direction **change**, dans le tick de l'acquisition.
- (c) F5 : la mort de la colonne se lit au tick **qui suit** son statut 4.
- (d) `h4/report.md` §3 donne 12 boules et 150 : après R-EL-1, 11 boules et 149.

**Défaut du tour** (pour Q-H4D2-1). Sur la grille 32×32 du vrai code, 240 paires sur 1 024 (23,4 %) ont un écart numérique > 16 : le binaire y tourne dans le mauvais sens. Dans les clés avec cible, c'est 57 tours sur 321 (`tools/turn_stats.py`).
- La loi corrigée proposée est une **INFÉRENCE** de la règle « un défaut avéré se corrige » : identique au binaire quand |écart| ≤ 16, sinon par le côté opposé avec `& 31`.
- Elle diffère du binaire exactement sur ces 240 paires.

## 2. Pas d'autre fait décisif non listé

Le stockage de la cible vit sur `+0x278` (pointeur) et `+0x27C` (identité). La colonne mémorise `[0x8012711C]`/`[0x80127120]`, propres au feu 1 (balayage de h4d1 : chaque pilote a ses globaux).

## 3. L'oracle

### 3.1 Les clés refaites (R-EL-1, `_pf`, banc R-EL-4)

Machine : copie du banc e20-h4 corrigé par la critique d'h4d1 (`OUT/h4o/`, nouveau `fire1_lib.py`).
- Réels : `SpawnWarpEntity`, passes d'entités, programmes natifs A/C/D/E, passe de coups.
- Piles journalisées : sons, `CreateEffectEntity`, effets attachés, passe d'effets.
- Corrections : `+0 = i` des 64 cases, `+0x48 = −1` du gabarit, pas par image `s ← s·0x7D2B89DD + 0xE06A02E7` après l'enregistrement de `rng1` et avant `rng0(t+1)`.
- Banc : champ plat 52×60, héros (100, 100) vers l'est (direction 24), graine avant le tick 1, aucune autre entité, Rond au tick 1, 1 tick par image.
- `uninit_reads` vide pour les 30 clés.

Colonnes de chaque tick dans `keys/*.json` :
- héros : `anim_t`, `anim_c`, x, y, z ;
- objet : `inuse`, `phase`, `counter`, `mp` ;
- `ptr_slot`, `ident`, `count`, `rng0/1/2`, `draws` ;
- entités : statut, id, type, drapeaux, x, y, z, `fx/fy/fz`, animations, directions, rangée, `animend`, `b274`, `b278_slot`, `b27c`, `c20`, `c1cc`, `c1d0`, `touch`, `slope`, `adj13c`, `ground`, boîte de coup, attaque ;
- `hits`, `resolved` (`ComputeNewHp` réel), `events` (sons ≠ 0, effets).

### 3.2 Les 30 clés (`keys/manifest.txt`, MD5 ; deux exécutions : manifeste identique)

| Famille | Clés | Ce qu'elles figent |
|---|---|---|
| Lancer nu | `fire1_cast_pf` (200 ticks), `fire1_cast_pf_seedB` (graine `0xA5A5A5A5`) | F8, F10, F17 ; 115 / 118 tirages ; bords ouest et nord, exacts |
| Interruptions (R-EL-6) | `fire1_hurt{10,33,34,35,60,100,121,122,124,140}_pf` | objet libéré au tick T ; effet `(19, 2)` à T = 35, 60, 100, 121 ; rien de né pour T ≤ 34 ; T = 122 : boules seules annulées ; T ≥ 124 : boules non annulées |
| Cibles (blocs 25 ClassA aux premiers emplacements libres, PV 5 000) | `tgt_one` (140, 100) ; `tgt_three` (140, 100)-(100, 140)-(180, 100) ; `tgt_vanish75` (cible retirée au tick 75) ; `col_hit` (cible à (108, 100) dans la boîte de la colonne) | acquisition, tours, touches, oubli ; colonne : touches aux ticks 34, 59, 84, 109 |
| Sentinelles de la requête (mode **binaire**) | `tgt_two_wrong_order` (emplacement 1 à (180, 100), emplacement 2 à (130, 100) : la plus lointaine est visée) ; `tgt_topslot` (11 leurres retirés au tick 1, cible au plus haut emplacement 12 : jamais visée) | F12 |
| Variantes corrigées (**INFÉRENCE**) | 2 `…_qfix_pf` : trois mots du binaire changés en mémoire (borne inclusive, tri dès 2). 6 `…_allfix_pf` : la requête patchée **et** le tour par le plus court (accroche à `0x8007AD6C`, vrai code ailleurs). | ce que la DLL doit donner avec les trois défauts de production |
| Relief | `wall_col6` (mur colonne 6), `water_cols6_12` | mur : fenêtre R-EL-5 ; eau : F20 |
| Table pleine | `table_full` (63 leurres : colonne refusée), `table_one_free` (62 : colonne née, aucune boule, 11 tirages) | F4, F6 |

**Sensibilité** (`keys/SENSITIVITY.txt`, FAIT) :
- Seules `two_wrong_order` et `topslot` dépendent de la borne et du tri. `cast`, `one`, `three`, `vanish75` et `col_hit` sont identiques sous les deux modes de la requête.
- `one`, `three`, `two_wrong_order`, `topslot` et `vanish75` dépendent du tour (premier écart au tick 51). `col_hit` n'en dépend pas.

**Paires de bord de la colonne** (bloc 25, 40 ticks) : touché à x = 76 et 122, non touché à 75 et 123 ; touché à y = 85 et 115, non touché à 84 et 116. À verser comme paires de bord, comme celles d'h4d1.

### 3.3 Le compte stocké

`h4o/probe_count.py` enveloppe la vraie requête pas à pas, sans modèle. À chaque appel il note `[0x80134360]` et le plus haut emplacement de statut ∉ {0, 4}. Sur cast, seedB, one, three, two_wrong_order, topslot, vanish75 et table_one_free : **494 appels, 0 écart**.
- En mode binaire, `compte` = maximum à la demande des `Slot` vivants suffit pour ces clés.
- h4b1b ne serait requis que pour reproduire la borne exclusive **en production** (Q-H4B-2).

### 3.4 Les vecteurs (format de `e20-h4b/bin/vec/README.txt`)

Fichiers dans `OUT/vec/` :
- `vec_fire1_driver_80035A84.json` (3 270)
- `vec_cancel_fire1_80034D2C.json` (400)
- `vec_e3_8007EDA0.json` (288)
- `vec_c63_8007AC60.json` (3 524 = 2 500 mondes aléatoires + 1 024 de la grille `kind: "turn"`)

Pour le pilote : garde, annulation générale, annulation du feu 1 et `DestroyEntityNoFx` sont le vrai code. `SpawnWarpEntity` et `CreateEffectEntity` sont des piles journalisées (`stub_spawn_ret`).

Chaque vecteur porte `reads`, `writes` (hors pile `0x801FF000-0x801FFF00`), `ret` et `journal`.

- **Rejeu depuis une mémoire vide** : 3 270/3 270, 400/400, 288/288, 3 524/3 524.
- **Mes modèles** (`f1model.py`, écrits d'après mes listings) : 0 écart sur les quatre fichiers.
  - Couverture : pilote 120/120, garde 19/19, annulation 55/55, `DestroyEntityNoFx` 15/15, E3 25/25, C63 95/95, requête 94/94, tri 91/91, `GetDirectionToTarget` 35/35.
  - e20-h4 avait : pilote 118/120, annulation 53/55.
- **Mutants** (`vec_selftest.py`) : 36 mutants des modèles, **tous tués**, par 20 à 3 211 vecteurs chacun. L'adaptateur simule celui du C# : reads → mémoire creuse → modèle → writes/journal/ret.
- **Déterminisme** : MD5 des quatre fichiers identiques sur deux exécutions.
- **`binary_only`** : 87 vecteurs du pilote et 81 de l'annulation lisent un `+0x1B8` ou un `+0x28` non nul. Le `DestroyEntity(e)` à un argument de la DLL ne porte pas ces deux écritures (talons E20.e et E20.g, `WorldProxy.cs:3301-3329`).
  - L'adaptateur les saute par ce drapeau ; aucune clé n'y touche.
  - Comparables à la DLL : 3 183 + 319 + 288 + 3 524.
- **Classes de lecture** (règle de D-H4B1-3) :
  - (i) entrées de l'appel (compte, argument de pile `[0x801FFF10]`) ;
  - (ii) octets d'entité avec un membre de la DLL ;
  - (iii) rien d'autre.
  - Les tableaux de sortie `0x800CC058/0x800CC158` de la requête ne sont pas de l'état de C63 : seul le premier pointeur compte (`+0x278`). L'adaptateur les ignore en écriture.
- **Rejeu des scripts d'e20-h4** (`h4_vec_drivers.py 1500`, `h4_vec_handlers.py 1500`) : 0 écart, feu 1 118/120, C63 95/95.

## 4. Atterrissage dans la DLL

Détail et lignes : `OUT/dll/state.md`.

**Existe** :
- `SpawnEntityBySpriteIndex` (`WorldProxy.cs:3488`, Z + 1 puis − `ModZ`, marqueurs `:3572-3574` ; interface `IEntityWorldContext.cs:81`, retour `null` possible).
- Le Z du préfab du 20, déjà épinglé (`AlundraSpawnBySpriteIndexInterfaceTests.cs:90-91`).
- La direction 0..31 sans normalisation (`EntitySpawnFactory.cs:834-836`).
- La rampe d'accélération (`ScriptedMotion.cs:620-638`).
- La passe de coups (`HitPass.cs:43`, `:105`, `:167` ; `CurrentAttack` `EntityScriptProxy.HitPass.cs:27`).
- `DeactivateOnHit` (`EntityScriptProxy.cs:1719`).
- L'E1 en ligne (`EventProgramRunner.cs:285-291`, `:328`).
- `IEntityWorldContext.DestroyEntity(e)` = `0x8003A774` (`WorldProxy.cs:3329`).
- `EnemyHelpers.Below` (`:35`) et `ScriptHelper.GetDirectionToTarget` (`:77`).
- Le registre (`NativeSpriteRegistry.cs:36-43`, `:54-65`).
- `EffectStage` / `NativeEffects.CreatedForTests` (`PlayerManager.cs:813-818`, `NativeEffects.cs:96`, `IEntityWorldContext.cs:210`).

**Manque** (prérequis d'autres tranches) :
- `TargetQuery.Find` et ses réglages (h4b1).
- Coutures et champs d'usage d'objet (h0a) ; fonction de départ, états et garde (h4a).
- `EarthTome1Ai` (h4c1).
- `ItemSpawnedEntity` (D-H4D1-3, h4d1).
- Les accesseurs 32 bits (Q-H4B-5) : **h4d2 est la première tranche qui en a besoin**.
- Le pilote est vide (`PlayerManager.cs:1507-1509`) et l'annulation est vide (`:740-742`, appelée `:664`).

**Où va quoi** (noms libres, contrôlés par `check_names.py`) :
- **`Alundra/Ai/Items/FireTome1Ai.cs`** (neuf) :
  - `Drive(héros)`, `Cancel(héros)`, `BallTick(entité, contexte, état)` (C63) ;
  - `SpriteTypes = {23}` ;
  - `Register(registry)` = `(ProgramCTick, 63)` pour `{23}` ;
  - le réglage `ShortestTurn` (valeur de production = défaut de Q-H4D2-1) avec couture `[ThreadStatic] …ForTests` ;
  - la requête par `TargetQuery.Find(context.SpawnedEntities, compte, boule, ClassA, attaque & 0xF)`, avec `compte` = maximum des `Slot` vivants (statut ∉ {Destroyed, FlagToDestroy}).
- **E3 pour le type 23** : `EarthTome1Ai.SpriteTypes` `{16}` → `{16, 23}`, sans `Register` neuf.
- **`EntityScriptProxy`** :
  - un accesseur 32 bits de `+0x274` (octets `Bytes[0..3]`, `:1226`) ;
  - la cible de la boule : une **référence** propre à la boule (`SpellTarget`, non clonée). « Identité inchangée » ⇔ même référence et `Status == Normal`.
  - `DelayOrAngleOrEntityId` (`:1228`) reçoit le `Slot` de la cible (0 = aucune, le héros n'est jamais candidat), pour comparer `b278_slot`.
  - `+0x27C` n'est pas reflété : les clés comparent « même entité que l'entité d'`id` N ».
  - La colonne utilise `ItemSpawnedEntity`. « Morte » ⇔ `Status == Destroyed || Slot < 0` après recyclage, jamais `Slot < 0` seul.
- **`NativeEffects.cs`** : le créateur de l'effet `(19, animation 2)` à une position, commun à h4d1 (D-H4D1-4). Réutilisé s'il existe, sinon créé au même nom.
- **Phase B**, `PlayerManager.cs` : les trois entrées `0x2F` des tables de h0a. `CreateForProduction` : `FireTome1Ai.Register`.

**Tests existants qui bougent** :
- `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`, titre `:50`) et `AlundraWaveSwordLifeTests.cs:181-183` (`3 + 3 + 1`) : **+1** (`(C, 63)`), réécrits par dérivation (bloc du §1.24, point 4).
- **Tests de h4c1 qui changent** :
  - (e2) « `(E, 3)` pour le type 16 seul (pas 17 ni 23) » devient « pour `{16, 23}` (pas 17, 24, 150) ».
  - Le mutant de h4c1 « E3 inscrit pour `{16, 23}` » devient la vérité de production.
  - (p4) de h4c1 est inchangé.

**Ne bougent pas** (lus) :
- S1 et S5 (`AlundraWeaponSpawnSeamTests.cs:83-107`, `:238-259`) : les tomes ne sont pas des armes, `ProductionWiredWeaponTypes` reste `{27, 28, 29}`.
- `AlundraBirthTickTests.cs:474-477` (type 20, un seul `OneFrame`).
- `AlundraBirthBoxTests.cs:131` (naissance sans tick).
- Les tests de Z logique (type 11, `AlundraLogicalZStepTests.cs:28`).
- `AlundraNativeSpriteRegistryTests.cs:242-249` (compte relatif).
- Aucun test ne fait naître le type 23.

**Recette de l'auteur** (non bloquante) :
- Sauvegarde `arsenal-a-m14` (`TestSaves.cs:72-75` et `:124` : le kit porte le parchemin 47, aucun livre 48 ne le coiffe, PM 4/4). `arsenal-b-m14` (`:125`) porte le livre 48, qui passe devant.
- Le tome 47 mis en objet du Rond par l'inventaire.
- À voir : pose de 32 ticks (0,64 s) ; colonne de feu aux pieds d'Alundra 87 ticks (1,74 s) ; une boule toutes les 8 ticks (11 en 1,8 s) qui monte, retombe et vise l'ennemi le plus proche ; héros bloqué ≈ 3 s (149 ticks) ; PM −1.
- Un ennemi touché perd 32 par boule et 64 par coup de colonne tous les 25 ticks.

## 5. Tâches proposées

**Prérequis** :
- h0a, h4a (coutures, départ, états, garde), h4b1 (`TargetQuery` et réglages, défauts Q-H4B-2/3), h4c1 (E3 et son inscription), **f5** (D-F5-5).
- Défaut de Q-H4B-5, fin du chantier et Q-R-7, défaut de Q-H4D2-1.
- Pas f4, pas f6a, pas h4b2, pas h4d1 (partage du créateur d'effet, deux ordres admis).

**Phases** :
- **A**, inerte en production, trois coutures de R-EL-4. `EarthTome1Ai.SpriteTypes` `{16, 23}` y est posé : inerte, car aucun type 23 ne naît en production avant la phase B.
- **B** : les trois entrées `0x2F` et `Register` dans `CreateForProduction`.

### ⏳ T0 — Annexe, oracle, sondes, liste prédite (aucun code)

- **(a)** Verser `OUT/` sous `docs/plan-e20-annexe/e20-h4d2/`. Rejouer depuis des copies :
  - `fire1_make_keys.py` ×2 (manifeste de 30 clés identique) ;
  - `vec_fire1.py` ×2 (4 MD5 identiques, 0 écart des modèles, couvertures ci-dessus) ;
  - `vec_selftest.py` (36 mutants tués) ;
  - `probe_count.py` (494 appels, 0 écart) ;
  - `h4_vec_drivers.py` et `h4_vec_handlers.py` d'e20-h4 (0 écart).
- **(b)** La table de correspondance des mots lus par les vecteurs vers les membres de la DLL (règle de D-H4B1-3). Un octet d'entité sans membre est nommé et **arrête** la tranche. Les vecteurs `binary_only` sont sautés par leur drapeau.
- **(c)** Sondes de la DLL, prédiction écrite avant :
  - **P1** : la colonne naît à S = 34, emplacement 1, `PosZ` = Z du héros + 1 + 18 px, statut 2 à la fin de S.
  - **P2** : la boule naît à S dans l'emplacement 2, `ForceZ 0x80000`. Le mot `+0x274` vaut 16 à S, 15 à S+1 (C63 absent du tick de naissance). Première action à S+17.
  - **P3** : après chaque image, l'état du générateur égale `rng0(t+1)` de `fire1_cast_pf` et le nombre de pas de la DLL pendant l'image égale `draws + 1`, sur 200 ticks.
  - **P4** : les bords ouest et nord arrêtent les boules aux ticks de la clé (statut 3 en l'air, 86 pour la première).
  - **P5** : une boule sans gravité qui tombe à −3 px/tick pose `IsOnGround` au tick L de la clé (109 pour la boule d'`id` 4). Elle passe au statut 3 à L+1 avec `TargetAnimationId = 1`, drapeaux `0x102044`, `fx` décroissant de 8 688 par tick.
  - **P6** : `+0xAC` à (statut 3) + 10, `FlagToDestroy` au tick suivant.
  - **P7** : la colonne : `ForceResetAnimationFlag` à S+86, `FlagToDestroy` à S+87 par l'E1 en ligne **sans effet de bris**, `Slot < 0` et `Status == Destroyed` à S+88.
  - **P8** : boîte de la colonne 23×16×47, et les paires de bord du §3.2 pour un bloc 25.
  - **P9** : `CurrentAttack.Attribute & 0xF == 10` pour la boule en animation 0 à S+17.
  - **P10** : l'effet de l'annulation `(19, 2, 6553600, 6553600, 1179650)` au tick de la garde (clé `hurt60`), observé à `CreatedForTests`.
  - **P11** : `Slope_18c` de la boule sur l'eau profonde reste 0 ; la clé de l'eau égale la clé plate.
  - **P12** : mêmes emplacements dans la clé et dans la DLL (plus basse case libre).
  - **P13** : la requête de la DLL en mode binaire, appelée avec `compte` = maximum des `Slot` vivants, rend les mêmes listes que `probe_count.py`.
- **(d)** Liste prédite : les deux épingles et (e2) de h4c1. Recomptée au HEAD de départ.

### ⏳ T1 — Phase A (tests d'abord ; R-EL-4 ; requête et tour en mode binaire pour les tests « bin »)

**Tests unitaires** :
- **(t1)** Accesseur 32 bits : aller-retour, petit-boutiste, octets hauts, négatifs.
- **(d1)** Pilote contre `vec_fire1_driver_80035A84.json` (3 183 comparables) en **un seul test**. Compare : retour, phase, animation du héros, compteur lu, naissances (parent, type, X, Y, Z, direction), `ForceZ`, `+0x274`, `+0x278`, générateur, entités annulées. La garde passe par `ItemCancelForTests` = `Cancel` **seulement quand l'objet en cours est `0x2F`**, neutre sinon.
- **(d2)** Colonne refusée : pointeur nul rangé, rend 1, rien d'autre. Boule refusée : rien d'écrit, tirage consommé.
- **(c1)** Annulation contre `vec_cancel_fire1_80034D2C.json` (319 comparables) en un seul test.
- **(c2)** Cas nommés :
  - type 20 au statut 2 → effet `(19, 2, X, Y, Z)` **puis** `FlagToDestroy` ;
  - type 23 au statut 2 → `Deactivated` et `BlockedByEntity` vidé ;
  - accolés, intacts : statuts 1, 3, 4 ; types 17, 21, 22, 24 au statut 2 ; entité de carte d'indice 20 ou 23 (`SpriteType` 0x114 / 0x117).
- **(c3)** Annulation par la garde : objet libéré, sort annulé, rend 1.
- **(e1)** E3 contre `vec_e3_8007EDA0.json` (288).
- **(e2)** Inscriptions sur un registre neuf : `(E, 3)` → `{16, 23}` (pas 17, 24, 150) ; `(C, 63)` → `{23}` seul ; `(E, 1)` absent du registre.
- **(k1)** bin : C63 contre `vec_c63_8007AC60.json` (3 524) en un seul test. Les tableaux de sortie de la requête sont ignorés.
- **(k2)** bin : grille 32×32 du tour.
- **(k3)** Corrigé : la même grille donne un résultat égal au binaire sur 784 paires, par le plus court sur 240 ; jamais plus de 4 pas ; ne dépasse jamais.
- **(k4)** Valeurs de production des trois réglages.
- **(k5)** Argument de la requête : octet d'attaque `0x8A` → 10, ClassA, entité sans `CurrentAttack`.

**Tests de monde** (R-EL-4, sans tolérance sauf (w4)) :
- **(w1)** `fire1_cast_pf` et `…_seedB`. Colonnes du §3.1 : héros, objet, entités, générateur ; pas `count` ni `ident`.
- **(w2)** bin : `one`, `three`, `vanish75`, `col_hit`.
- **(w2c)** Mêmes scénarios contre les `_allfix` **aux réglages de production**.
- **(w3)** bin : `two_wrong_order` et `topslot`.
- **(w3c)** Leurs `_qfix` et `_allfix` aux réglages de production.
- **(w4)** `wall_col6`, par la règle « arrivée + 1 » de R-EL-5, puis la chronologie relative.
- **(w5)** `water_cols6_12` ≡ `cast_pf`.
- **(w6)** Les 10 `hurt` : héros après T et son 23 exclus (R-EL-6) ; objet, entités, effets, générateur comparés.
- **(w7)** `table_full` et `table_one_free`.
- **(w8)** 2 et 3 ticks par image : seulement les ticks de naissance, de fin (S+86), d'annulation, la libération et les comptes d'effets et de tirages.

### ⏳ T2 — Phase B et tests de production, rouges avant

- **(p1)** Rond avec le seul tome 47, PM 4/4 : la clé de (w1) par le chemin de production.
- **(p2)** Table pleine : PM perdu, objet en cours puis libéré au tick 34, rien né (`table_full`).
- **(p3)** Rond puis blessure au tick 60 (R-EL-6) : clé `hurt60` et effet `(19, 2)`.
- **(p4)** `UnhandledWeaponSpriteCodes({20, 23}, catalogue, registre de production)` vide après la phase B, `[(C, 63), (E, 3)]` avant. Épingles réécrites par dérivation.

### ⏳ T3 — ADR, plan, deux verifiers frais (l'un sur les clés et leur initialisation)

Mutants (≥ 30, tous tués) :
- Les 36 des modèles :
  - **pilote** : `& 7`, plage du tirage, Z + 16 px, `ForceZ` de la boule, `+0x274 = 0x10`, `+0x278` non remis à 0, mort de la colonne → `0x33`, identité ignorée, phase 1 sans garde, pas d'enchaînement, colonne nulle rend 0, phase 2 attend `0x33`, garde acceptant `0x34`, garde sans la pente ;
  - **annulation** : types 20/23 permutés, effet 19→18, variante 2→1, boules → `FlagToDestroy`, `+0x20` non vidé, borne exclusive, statut 3 aussi annulé ;
  - **E3** : sans `| 0x40`, `~0x10`, sans la fin d'animation ;
  - **C63** : délai non décrémenté, `ForceZ −2 px`, tirage 9→8, `& 0xF`→`& 0x7`, masque ClassA→5, identité non rangée, cible morte non oubliée, `+0x274 = 3`, pas 4→3, seuil < 3, pas +5, dernier résultat au lieu du premier.
- **Plus** :
  - `(E, 3)` encore `{16}` ;
  - `(C, 63)` inscrit pour 17 ;
  - `ShortestTurn` inversé ou sans `& 31` ;
  - tour corrigé aussi sur 16 exactement ;
  - `compte` fixé à 63 en mode binaire ;
  - requête appelée malgré la cible ;
  - cible mémorisée par `Slot` seul (tué par `vanish75` puis recyclage) ;
  - `ItemSpawnedEntity` testé par `Slot < 0` seul ;
  - annulation qui touche aussi les entités de page de carte ;
  - mort de la colonne lue un tick trop tôt ;
  - naissance de la boule avant la garde.

### Acceptation

- T1 vert en phase A ((w4) par sa fenêtre).
- (p1) à (p4) rouges avant la phase B, verts après.
- Suite complète en Release (copie neutralisée) et en Debug, zéro échec.
- Tests déplacés ⊂ liste prédite ; `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.

### Preuve de retrait

Chacune se déduit des définitions des tests ci-dessus.
- Retirer l'appel `FireTome1Ai.Register` de `CreateForProduction` rend rouges exactement (p1), (p3), (p4) et les deux épingles.
  - (p2) reste vert : aucune boule ne naît.
  - (e2) reste vert : il appelle `Register` lui-même.
- Remettre les trois entrées `0x2F` au bouchon rend rouges exactement (p1), (p2) (PM non perdu, objet non ouvert) et (p3). (p4) reste vert.
- Remettre seulement l'entrée d'annulation au bouchon rend rouge exactement (p3).
- Neutraliser `Cancel` rend rouges :
  - (d1) (ses vecteurs à garde échouée avec l'objet `0x2F`), (c1), (c2), (c3) ;
  - (w6) pour T ∈ {35, 60, 100, 121, 122} ;
  - (p3).
  - (w6) T ∈ {10, 33, 34, 124, 140} reste vert.
- Neutraliser `Drive` rend rouges (d1), (d2), (c3) et tous les (w) et (p). (e1), (e2), (k1)-(k5) et (t1) restent verts.
- Neutraliser `BallTick` rend rouges (k1)-(k3), (w1)-(w5), (w6) pour T ≥ 35, (w8), (p1), (p3). (d1), (c1), (w6) T ≤ 34, (w7) et (p2) restent verts.
- Remettre `EarthTome1Ai.SpriteTypes` à `{16}` rend rouges (e2), (p4) et tous les (w) où une boule touche le sol, un mur, un bord ou une cible (le statut 3 reste, E3 ne tourne pas). Les (k*) et (d*) restent verts.

### Arrêts

- Une clé diffère de sa lecture au tick près (hors fenêtre R-EL-5).
- Une sonde P1 à P13 diffère.
- Un octet lu par un vecteur n'a pas de membre (hors `binary_only`).
- Un test existant hors de la liste prédite bouge.
- `(C, 63)` déjà inscrit, ou `Register` cumulatif au HEAD de départ (E14.b0 passé avant).
- h4b1, h4c1, f5, h4a ou h0a non faits.
- La DLL ne pose pas `IsOnGround` pour une entité sans gravité qui tombe (P5 : écart moteur).
- Un besoin de toucher au moteur ; un nom refusé par `check_names.py`.
- Une réponse de l'auteur autre que le défaut à Q-H4D2-1, Q-H4B-2/3/5 ou Q-H4C1-1.

### Budget et retour arrière

- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B (les trois entrées et l'appel). Phase A : le fichier, `SpriteTypes` `{16}`, l'accesseur, la référence de cible, et le créateur d'effet s'il est à h4d2.

## 6. Questions pour l'auteur

**Q-H4D2-1 — le tour des boules de feu.** Dans l'original, une boule qui vise un ennemi tourne en comparant des numéros de direction (0 à 31) sans tenir compte du bouclage. Quand l'ennemi est de l'autre côté du « 0/31 » (par exemple boule en 31, ennemi en 0), elle fait presque un tour complet avant de viser, au lieu d'un petit pas. Dans mes mesures, 18 % des virages sont dans ce cas.
- **(A, défaut, règle de l'auteur) corriger** : la boule prend toujours le plus court.
  - Coût : un réglage et des clés « corrigées ».
  - Visible : les boules rejoignent leur cible plus vite, avec moins de boucles.
- **(B) reproduire** : exactement l'original, boucles comprises. Coût : aucun, les clés binaires suffisent.

**Hérités avec leur défaut** (rien de neuf) :
- Q-H4B-2 : le plus haut emplacement n'est jamais visé. Défaut corriger. Visible seulement si un ennemi est seul au plus haut emplacement.
- Q-H4B-3 : avec deux cibles, la plus lointaine est visée une fois sur deux. Défaut corriger.
- Q-H4B-5 : accesseurs 32 bits communs, h4d2 les crée.
- Q-H4-1 : piste XA de l'octet 2.
- Q-H4C1-1 : E3 pour les rochers puis la boule. Si l'auteur a répondu B, h4d2 n'a rien à faire pour E3.

**Pas une question d'auteur** : l'ordre avec h4d1 (le créateur de l'effet `(19, 2)` est commun ; h4d2 réutilise ou crée) et le nom des membres.

## 7. INCONNU et INFÉRENCE

**INCONNU** :
- La sortie de la DLL : rien n'a été construit ni exécuté.
- Si la DLL pose `IsOnGround` pour une entité marquée sans gravité qui tombe (P5). h4c2 en dépend déjà pour les rochers du type 17.
- Le verdict de `check_names.py --tree` (non lancé). `collision_findings` sur `FireTome1Ai`, `TargetQuery`, `EarthTome1Ai` et `FireTome2Ai` ne rend aucune collision (FAIT).
- Le rendu en jeu ; la piste XA de l'octet 2 ; les tomes sur la chaîne (Q-H4-7).

**INFÉRENCE** :
- La loi du tour corrigé et les clés `_qfix` / `_allfix` (binaire patché de 3 mots, accroche à `0x8007AD6C`).
- « Identité inchangée ⇔ même référence et statut 2 » équivaut au test d'identité du binaire, car la DLL ne réutilise pas les objets : un recyclage pose `Status == Destroyed` et `Slot = −1`.
- La boule hors ClassA n'est jamais candidate (drapeaux `0x102074`, bit `0x1` clair) : vérifié par les clés, non par la DLL.

## 8. Fichiers (tous sous `OUT/`)

- `README.md` : index et commandes de rejeu.
- `dll/state.md` : la DLL au HEAD, ligne par ligne.
- `lst/` : mes listings capstone (`driver_80035A84`, `c63_8007AC60`, `cancel_80034D2C`, `e3_8007EDA0`, `cancel_dispatch_800350C0`, `guard_800351B8`, `wrapper_8003634C`, `e1`, `dirto`, `destroy_nofx`) et `tables_checked.txt`.
- `tools/` :
  - `f1model.py` : mes modèles.
  - `vec_fire1.py` : générateur, comparaison aux modèles, rejeu depuis une mémoire vide.
  - `vec_selftest.py` : 36 mutants des modèles.
  - `turn_stats.py`, `mydis.py`.
- `vec/` : les 4 fichiers de vecteurs. `vec_manifest_run1.txt` et `vec_manifest_run2.txt` : MD5, identiques. `vec_selftest.txt` : sortie.
- `keys/` : les 30 clés, `manifest.txt`, `SENSITIVITY.txt`. `keys_run2/manifest.txt` : seconde exécution, identique.
- `h4o/` : banc e20-h4 corrigé. Nouveaux : `fire1_lib.py`, `fire1_make_keys.py`, `probe_count.py`.
- `vech/` : machine et outils de vecteurs d'e20-h4b, copie.
- `fcopy/` : machine du banc f4f6, copie de référence (non utilisée pour les clés finales).
- `h4_vec_drivers_replay.log`, `h4_vec_handlers_replay.log` : rejeu des scripts d'e20-h4.