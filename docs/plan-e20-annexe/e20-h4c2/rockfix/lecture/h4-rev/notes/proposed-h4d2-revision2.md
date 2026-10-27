# Texte proposé : E20.h4d2, révision 2 (D-E20-55 aides 32 bits propres aux tomes ; D-E20-57 le tour reproduit)

## En-tête (ligne 11689)

`#### 1.31.6 E20.h4d2 — Le feu 1 : … ⏳ (détaillée le 2026-10-18 ; **READY** à la première relecture, `b20eb8c` ; **révision 2 du 2026-10-25** : D-E20-55 et D-E20-57 ; relecture de clôture à refaire)`

## Paragraphe de révision (avant « Prérequis »)

**Révision 2 (2026-10-25).** Deux réponses de l'auteur du 2026-10-22 changent la tranche. (1) Q-H4D2-1, B (D-E20-57) : la boule tourne comme
l'original, boucles comprises ; plus de réglage `ShortestTurn`, plus de loi corrigée, plus de clés `_allfix` comme oracle ; la production
(`InclusiveBound` vrai, `SortThreshold` 2) se compare à `_pf` ou à `_qfix`, jamais à `_allfix`. (2) Q-H4B-5 (D-E20-55) : les accès 32 bits sont
propres aux tomes, `EntityScriptProxy` ne reçoit aucun accesseur ; la tranche écrit ses aides (motif de D-E0C-13 et de D-G5-2). Deux erreurs de
texte sont corrigées en passant (fait « Le sprite 23 » et prérequis).

## Prérequis (lignes 11691 à 11696)

… ; la fin du chantier et Q-R-7 ; les défauts retenus de Q-H4B-2 et Q-H4B-3 (corriger) et de Q-H4C1-1 (A), et les réponses de l'auteur à
Q-H4D2-1 (reproduire le tour, boucles comprises, B, D-E20-57) et à Q-H4B-5 (aides 32 bits propres aux tomes, D-E20-55). Ni h4b1b (abandonnée,
D-E20-54), ni h4b2 (fondue dans E20.i2, D-E20-61). **h4d1 avant ou après** … (inchangé).

## But (lignes 11698 à 11701), une proposition à compléter

… et tourne vers l'ennemi de classe A le plus proche **par le sens numérique des directions (0 à 31), comme l'original : quand l'ennemi est de
l'autre côté du passage de 31 à 0, la boule fait de 5 à 8 pas de 4 (un pas tous les 3 ticks) au lieu d'un petit pas** (attaque 10, puissance 32 ;
elle meurt à sa première touche) …

## Faits : le fait du tour (lignes 11742 à 11743) et une correction (lignes 11731 à 11733)

- **Le tour sans plus court chemin, reproduit** (Q-H4D2-1 B, D-E20-57) : sur la grille 32 × 32 du vrai code, 240 paires sur 1 024 ont un écart
  numérique > 16 (le binaire y tourne par le long côté : 5 à 8 pas de 4, 5,83 en moyenne, au lieu de 1 à 4) ; dans les clés avec cible, 57 virages
  sur 321 (17,8 % : 14 sur 69 pour `tgt_one`, 24 sur 121 pour `tgt_three`, 18 sur 117 pour `tgt_two_wrong_order`, 1 sur 14 pour `tgt_vanish75`) ;
  de 31 vers 0, huit pas (31, 27, 23, 19, 15, 11, 7, 3, 0), de 0 vers 31 de même. La DLL le reproduit : aucun réglage, aucune loi corrigée.
- Dans le fait « Le sprite 23 » : remplacer « un chemin que les rochers à gravité de h4c1 et h4c2 n'exercent pas » par « un chemin que les rochers de
  h4c1 (à gravité) n'exercent pas ; h4c2 l'exerce déjà avec le type 17 (`0x132014`, sans `Gravity`) ».

## Décisions (lignes 11752 à 11772)

- **D-H4D2-1** — Un fichier neuf `Alundra/Ai/Items/FireTome1Ai.cs` (Q-R-7) : `Drive(héros)`, `Cancel(héros)`, `BallTick(entité, contexte, état)`
  (C63), `SpriteTypes = {23}`, `Register(registre)` pour `(ProgramCTick, 63)` et le type 23 seul. Le tour est celui du binaire : comparaison
  numérique de `d` et de la direction courante, pas de 4, `d` si l'écart est inférieur à 4, jamais de bouclage (D-E20-57).
- **D-H4D2-3** — Les aides 32 bits de `+0x274` (octets `Bytes[0..3]`, petit-boutiste) sont **propres aux tomes** (D-E20-55), sur le motif de
  D-E0C-13 (`PickupAi.ReadWord274`/`WriteWord274`, `Alundra/Ai/Objects/PickupAi.cs:420`) et de D-G5-2 : une classe `internal static` neuve
  d'`Alundra/Ai/Items/` (nom libre, contrôlé par `check_names.py`), que h4f1 réutilise ; `EntityScriptProxy` ne reçoit aucun accesseur. Elles sont
  nécessaires au rejeu des vecteurs ; en production, les valeurs restent ≤ 16 (critique §5, point 13). E14 fait les siennes.
- **D-H4D2-6** — Les réglages dans les tests : chaque test pose explicitement `InclusiveBound` et `SortThreshold` et les restaure en
  `try/finally` (…) ; il n'y a plus de réglage de tour.
- **D-H4D2-7** — Les clés de production. Le tour étant celui du binaire, la production se compare à `_qfix` pour `tgt_two_wrong_order` et
  `tgt_topslot` (où la borne et le tri changent la cible) et à `_pf` pour les autres scénarios : MESURÉ (`e20-h4d2/disc/h4o/qfix_make_keys.py`),
  `tgt_one`, `tgt_three`, `tgt_vanish75`, `col_hit` et `cast_pf` donnent exactement les mêmes lignes avec les trois mots de la requête modifiés.
  Les six clés `_allfix` ne sont plus des oracles (elles portent le tour corrigé) ; elles restent versées, marquées « non utilisées ».

## Périmètre et phases (lignes 11774 à 11782)

`EntityScriptProxy.cs` : « la référence de cible » seule (plus d'accesseur) ; ajouter `Alundra/Ai/Items/` (la classe des aides 32 bits, neuve).
**A1** : les aides 32 bits, la référence de cible, `Cancel`, l'élargissement d'E3, `BallTick` et leurs tests unitaires.

## T0 (lignes 11786 à 11812)

(a) … ajouter : `qfix_make_keys.py` (les quatre scénarios et le lancer nu avec la requête corrigée, lignes identiques à `_pf`). (b) la table
d'adaptation : `+0x274` ↔ les aides 32 bits de la classe neuve (octets `Bytes[0..3]`) ; le reste inchangé.

## T1 (lignes 11813 à 11822)

(t1) les aides 32 bits (aller-retour, petit-boutiste, octets hauts, négatifs). (k1) C63 contre `vec_c63_8007AC60.json` (3 524) en un seul test,
**dont les 1 433 vecteurs que la loi corrigée aurait changés**. (k2) le tour sur la grille 32 × 32 : le long côté pour les 240 paires d'écart
numérique > 16, `& 31` jamais appliqué ; cas nommés 31 → 0 et 0 → 31 (huit pas), écart de 3 (saut à la cible) et de 4 (un pas). (k3) **retiré**
(D-E20-57). (k4) les valeurs de production des deux réglages (`InclusiveBound` vrai, `SortThreshold` 2).

## T1 bis (lignes 11823 à 11839)

(w2) aux réglages de production **et** en mode binaire (`InclusiveBound` faux, `SortThreshold` 3) : `tgt_one`, `tgt_three`, `tgt_vanish75`,
`col_hit` contre leurs clés `_pf` (D-H4D2-7) ; (w2c) **retiré** ; (w3) en mode binaire : `tgt_two_wrong_order` (le tri) et `tgt_topslot` (la borne
seule) contre `_pf` ; (w3c) aux réglages de production : leurs `_qfix`. Les virages longs (57 sur 321) sont dans (w2), (w3), (w3c). (w6) : les
colonnes du héros après T se comparent (R-EL-6 révisée).

## T3 : deux mutants remplacés (la liste reste à au moins 30)

Retirer « `ShortestTurn` inversé ou sans `& 31` » et « le tour corrigé aussi à l'écart 16 exactement » ; ajouter « le tour par le plus court (la
correction refusée ; tué par (k2), (w2) `tgt_one`, (w3)) » et « un `& 31` ajouté à la comparaison (le bouclage ; tué par (k2) 31 → 0) ».
Équivalents déclarés inchangés (seuil du tour `<= 4` ou `< 5`).

## Acceptation, preuve de retrait (lignes 11858 à 11867)

`Drive` neutralisé : « laisse verts (t1), (c1), (c2), (e1), (e2), (k1), (k2), (k4), (k5) et (p4) » ; `BallTick` neutralisé : « rend rouges (k1),
(k2), (k5), (w1) à (w5) (dont (w3c)), (w6) pour T ≥ 60, (w8) à (w11), (p1) et (p3) ».

## Recette de l'auteur (lignes 11868 à 11871) : à ajouter

… une boule toutes les 0,16 s (onze), qui monte, retombe et vise l'ennemi le plus proche **; quand l'ennemi est de l'autre côté du passage de 31 à 0
(environ un virage sur six), la boule décrit presque un tour avant de viser, comme dans l'original**.

## Arrêts (lignes 11877 à 11881)

… une réponse de l'auteur autre que le défaut à Q-H4B-2, Q-H4B-3 ou Q-H4C1-1, ou autre que B à Q-H4D2-1 et que « propres aux tomes » à Q-H4B-5
(révision 2).

## Retour arrière (ligne 11883)

… la phase A : le fichier, `SpriteTypes` `{16}`, **la classe des aides 32 bits**, la référence de cible, …
