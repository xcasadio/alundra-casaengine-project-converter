# Révisions de h4c2 (§1.31.4) et h4d2 (§1.31.6) après D-E20-55, -56, -57 et E20.c2b avant h4

Lecture seule, HEAD `8174c0cb`. Je n'ai rien écrit dans le dépôt, ni construit, ni lancé de test. Tous les rejeux sont faits depuis des copies dans mon dossier. L'arbre est modifié en ce moment par l'executor (`Alundra/Events/EventProgramRunner.cs`, `TestSaves*`, traces du héros, `docs/test-saves.md`). Rien de cela ne vient de moi.

Légende : [FAIT] vu dans le plan, le code ou le binaire ; [MESURÉ] sorties de mes rejeux ; [INFÉRENCE] déduit, non mesuré.

## 0. Résumé

- **h4c2** : D-E20-56 est une vraie correction, avec trois conséquences.
  - E13 n'agit plus tant que le rocher n'est pas au sol.
  - Les clés du rocher de bord et du rocher du plateau ne sont plus des oracles de la DLL.
  - Un rocher arrêté en l'air reste au statut 2, donc l'annulation l'atteint encore.
- **h4d2** : D-E20-57 supprime du travail (réglage `ShortestTurn`, loi corrigée, `(k3)`, `(w2c)`, six clés `_allfix`). D-E20-55 déplace les accès 32 bits hors d'`EntityScriptProxy`.
- **c2b avant h4** :
  - Les sorts ne blessent jamais le héros (`HitsClassA` seul), donc aucun banc de monde ne change sur ce point.
  - Seule R-EL-6 est fausse. Les colonnes du héros après T deviennent comparables : le recul de 33,09 px est déjà dans les clés d'h4d2.
- **Deux questions nouvelles à l'auteur** (§8). Les deux révisions sont chacune une nouvelle époque de disponibilité : un `plan-verifier` frais pour clore chacune.

## 1. Faits numérotés

### Plan

1. [FAIT] D-E20-55/56/57 sont aux lignes 219-224 de `docs/plan-e20-heros.md`. Les lignes de questions Q-H4B-5, Q-H4C2-3, Q-H4D2-1 sont aux lignes 12209, 12216, 12220.
2. [FAIT] Le tableau de découpage (lignes 11108 et 11110) est déjà à jour. Les sections détaillées ne le sont pas :
   - Ligne 11446 : Q-H4C2-3 « fidèle ».
   - Ligne 11558 : arrêt sur toute réponse autre que le défaut à Q-H4C2-3.
   - Lignes 11693-11694 : « corriger le tour » et « accesseurs communs ».
   - Ligne 11881 : arrêt sur toute réponse autre que le défaut à Q-H4D2-1 et Q-H4B-5.
3. [FAIT] R-EL-6 (lignes 11328-11330) dit : « la DLL n'a pas la réaction du héros (E20.c2b) : les champs du héros après T et son son 23 ne sont jamais comparés ». c2b passe avant h0a (D-E20-83, D-C2B-1), donc c'est faux pour h4.

### h4c2 : le rocher arrêté en l'air

4. [FAIT] La passe des événements du binaire (`e20-h4c2/disc/dis/events_pass_800386D0.txt`, 0x800387D4-0x80038834) passe au statut 3 quand le drapeau `0x10` est levé et que `+0x13C` ou `+0x144` n'est pas nul. La DLL a la même condition (`Alundra/Entities/EntityScriptProxy.cs:1953`).
5. [FAIT] E13 (`disc/dis/E13_8007F30C.txt`) écrit `+0x88 = 1` (animation cible 1), `[0x800DD874] = 2` et les drapeaux. Le son 45 est le son de l'animation 1 : mes rejeux vérifient « ticks du son 45 = ticks de `ta == 1` ».
6. [MESURÉ] Dans l'original (rejeu de mon sous-dossier `h4c2/out/`, MD5 identiques aux clés versées) :
   - Rocher de bord, id 11 (héros en (100,100)) : naît au 162 à x = 10 avec `+0x13C` levé, statut 3 au 163 à 240 px de haut, sol au 194, `+0xAC` au 209, statut 4 au 210 (b + 48).
   - Rocher 8 du plateau d'octet 16 : bute au 142, statut 3 et coup au 143 à 16 px du sol, sol au 146, statut 4 au 190.
7. [MESURÉ] Binaire modifié en mémoire. `fixpatch.py` ajoute un trou de code à l'adresse 0x801F0000, appelé depuis 0x8007F348. Dans la première passe d'E13, si `+0x144 == 0`, le statut est remis à 2 et rien d'autre n'est écrit. Deux exécutions donnent le même MD5.
   - Rocher de bord : glisse le long du bord (x = 10, y de 101,23 à 140,44), sol au 194, statut 3 et coup au 195 (b + 33), `+0xAC` au 241, statut 4 au 242 (b + 80, comme un rocher intérieur).
   - Rocher 8 : reste collé au mur à (157,00 ; 72,02) pendant les ticks 141 à 146, statut 3 et coup au 147, statut 4 au 194.
   - Ids 13, 14, 15 (octet 16) : changement d'indice de case, car les cases sont libérées plus tard.
   - La clé « milieu de carte » est identique au bit près (MD5 `877bb812…`). Les interruptions T = 60 et 100 aussi.
   - À T = 170 et 190, le rocher de bord est annulé : un son 45 de plus, mort à T + 47 au lieu de 210.
8. [FAIT] Précédent dans la DLL : E17 des flèches (`Alundra/Ai/Projectiles/ArrowAi.cs`) termine par `entity.Status = EntityStatus.Normal`. `NativeSpriteRegistry.Register` lève sur un doublon de clé.
9. [MESURÉ] `vec_e13_8007F30C.json` (e20-h4b) compte 40 vecteurs, dont 20 de première passe (ils écrivent le coup). Aucun ne lit `+0x144`.
10. [INFÉRENCE] Le glissement après contact dans la DLL (`Alundra/Physics/ScriptedMotion.cs`, `SlideAlongWall`, branche oblique : « no second step, the engine already advanced the free axis »).
    - `NoObstacleSlide` ne fait que lever `ForceAdjusted`.
    - Le binaire s'arrête net en diagonale ; la DLL glisse. C'est « la division conjointe en diagonale », gardée par D-E20-80.
    - Avant D-E20-56, le rocher cessait d'avancer à l'impact, donc l'écart n'apparaissait pas. Il apparaît maintenant : environ 0,7 px par tick restant pour le rocher 8, jusqu'à 1,73 px par tick au maximum.
    - Contre le bord, `ClampStepToMapEdge` agit par axe pour les `HasLogicalZ` : même glissement que le binaire.

### h4d2 : le tour reproduit et les aides 32 bits

11. [MESURÉ] `h4d2/turn_law.py` (arithmétique pure sur la loi du plan) :
    - 240 paires sur 1 024 ont un écart numérique > 16, comme dit.
    - Sur ces paires : 5 à 8 pas (moyenne 5,83) pour le binaire, contre 1 à 4 (2,97) pour le plus court.
    - 31 → 0 fait huit pas (31, 27, 23, 19, 15, 11, 7, 3, 0). 0 → 31 aussi.
    - [INFÉRENCE] Un pas tous les 3 ticks (`+0x274 = 2` après un virage) : le pire surcoût est de 21 ticks, environ 0,35 s.
12. [FAIT] Virages longs dans les clés (`critic/results/turn_stats_my_cpu.txt`) : 14/69 (`tgt_one`), 24/121 (`tgt_three`), 18/117 (`tgt_two_wrong_order`), 1/14 (`tgt_vanish75`), soit 57/321 (17,8 %). Le mode corrigé diffère du binaire sur 1 433 des 3 524 vecteurs de C63 (`critic/check.md:152`).
13. [FAIT] Inventaire des clés (`disc/keys/manifest.txt`, `SENSITIVITY.txt`) : 30 clés, dont 2 `_qfix` (`two_wrong_order`, `topslot`) et 6 `_allfix` (requête corrigée plus tour par le plus court).
14. [MESURÉ] Production après D-E20-57 : `InclusiveBound` vrai, `SortThreshold` 2, tour du binaire. J'ai régénéré les `_pf` (MD5 égaux au manifeste), puis les mêmes scénarios avec les trois mots de la requête modifiés. `tgt_one`, `tgt_three`, `tgt_vanish75`, `col_hit` et `cast_pf` donnent exactement les mêmes lignes. La production se compare donc à `_pf` pour ces cinq, et à `_qfix` pour les deux sentinelles.
15. [FAIT] Aucun des 36 + 18 mutants de la découverte et de la critique ne concerne la loi corrigée. Seuls deux mutants du plan n'existent qu'avec elle (lignes 11846-11847).
16. [FAIT] La critique d'h4d2 (§5, point 13) dit : l'accesseur 32 bits de `+0x274` est nécessaire au rejeu des vecteurs, pas à la production (valeurs ≤ 16, un octet).
17. [FAIT] Précédents d'aides propres au HEAD :
    - `Alundra/Ai/Objects/PickupAi.cs:420-430` (`ReadWord274`/`WriteWord274`, `Bytes[0..3]` petit-boutiste ; `ChestAi.cs:95` les appelle).
    - `Alundra/Ai/Objects/PillarAi.cs:85-94` (D-G5-2).
    - `Alundra/Ai/Items/` n'existe pas au HEAD.
18. [FAIT] Erreur de texte à corriger : la ligne 11732 dit que les rochers « à gravité » de h4c1 et h4c2 n'exercent pas le chemin sans gravité. Or le sprite 17 a les drapeaux `0x132014`, sans `0x100` (ligne 11463). La découverte d'h4d2 dit elle-même « h4c2 en dépend déjà » (`disc/report.md:401`).

### c2b et les bancs

19. [FAIT] Drapeaux d'attaque : le sprite 17 vaut `0x132014`, la colonne `0x2044`, la boule `0x102074`. Tous ont `HitsClassA` (`0x4`), aucun `HitsClassB` (`0x2`). Le héros est ClassB (`0x3118C`), donc aucun sort ne le blesse.
20. [FAIT] Bloc de blessure de c2b : il appelle le pilote `0x8003634C` avant de ranger `+0x88 = 0x31` (`0x80031C6C`, F29 de `c2b-disc/binary/report-main.md`). La garde de h4a ne voit `0x31` qu'au tick suivant. Un `0x31` posé avant le tick T équivaut donc à une blessure réelle au tick T − 1.
21. [MESURÉ] La clé `fire1_hurt60_pf.json` porte déjà les colonnes du héros après T : `0x31` pendant 4 ticks (60 à 63), `0x1C` pendant 11 ticks (64 à 74), repos au 75, X de 100 à 133,09375 px, son 23 au tick 60. C'est le recul de c2b, soit 2 168 832 unités brutes. Les clés d'h4c2 (`cancel_*`) ne portent que `hero_ta`.
22. [INFÉRENCE] Au HEAD, la DLL n'a pas de `case 0x31` sur terre (§1.12.1 « La DLL au HEAD »). Sans c2b, un `0x31` posé ne passerait pas par la garde de h4a. Le bon ordre, c2b → h0a → h4, rend R-EL-6 fidèle.

## 2. Passages qui deviennent faux

### h4c2 (§1.31.4)

| Ligne | Passage | Pourquoi | Action |
|---|---|---|---|
| 11443 | état « READY… à réviser : D-E20-56 » | époque nouvelle | révision 3, relecture de clôture à refaire |
| 11446 | « Q-H4C2-3 (le rocher arrêté en l'air, fidèle) » | réponse B | réécrire |
| 11448-11450 | But | complète, ne contredit pas | ajouter « au sol seulement » |
| 11459-11460, 11477-11480 | E13 = binaire ; D-H4C2-1 | E13 d'origine joue l'impact en l'air | D-H4C2-1 amendée, D-H4C2-3 neuve |
| 11462, 11523, 11531 | « son 23 du héros exclu » | c2b (R-EL-6) | comparer le héros |
| 11512 | (e1) 40 vecteurs | 20 vecteurs de première passe sans sol | adaptateur « au sol » |
| 11521-11522 | (w2) rocher de bord contre `cast_2c_pf` | clé fausse pour la DLL | `…_rockfix` |
| 11523 | (w4) | incomplet | `cancel_…_t170_rockfix` |
| 11526-11527 | (w8) rocher 8 « R-EL-5 » | statut 3 ne dépend plus du drapeau | `plateau_hb16_rockfix` |
| 11533-11539 | mutants (28) | manque la garde | +4 mutants |
| 11546-11549 | preuve de retrait | manque la garde | ajout |
| 11550-11552 | recette | ajouter le bord | ajout |
| 11558 | arrêt sur Q-H4C2-3 « autre que le défaut » | le défaut était A | corriger |

### h4d2 (§1.31.6)

| Ligne | Passage | Action |
|---|---|---|
| 11689 | état | révision 2 |
| 11693-11694 | prérequis « corriger le tour », « accesseurs communs » | réécrire |
| 11698-11701 | But : le tour | compléter |
| 11731-11733 | « rochers à gravité de h4c1 et h4c2 » | corriger (fait 18) |
| 11742-11743 | fait du tour (Q-H4D2-1 A) | « reproduit » |
| 11752-11755 | D-H4D2-1 : `ShortestTurn`, loi corrigée | réécrire |
| 11760-11761 | D-H4D2-3 : accesseur commun sur `EntityScriptProxy` | réécrire (propres aux tomes) |
| 11771-11772 | D-H4D2-6 : trois réglages | deux réglages |
| 11774-11776, 11780 | périmètre et phase A1 : « l'accesseur » dans `EntityScriptProxy.cs` | déplacer |
| 11813, 11819-11820 | (t1), (k3), (k4) | (k3) retiré |
| 11832-11835 | (w2), (w2c), (w3), (w3c) : `_allfix` | réécrire |
| 11846-11847 | deux mutants du tour corrigé | remplacer |
| 11861-11867 | preuve de retrait citant (k3), (w2c) | recompter |
| 11868-11871 | recette | ajouter la boucle visible |
| 11881 | arrêt sur Q-H4D2-1 et Q-H4B-5 « autre que le défaut » | corriger |
| 11883 | retour arrière : l'accesseur | classe neuve |
| 11694 | « Ni h4b1b, ni h4b2 » | périmé (abandonnée, fondue) |

Hors de ces deux sections :
- R-EL-6 (lignes 11328-11330).
- L'en-tête du §1.31 (ligne 11055).
- Les lignes de questions : ajouter « appliquée à h4c2 (rév. 3) / h4d2 (rév. 2) ».

## 3. Effet de la blessure du héros (c2b) sur les bancs

- **Le héros dans la zone d'un sort ou d'un rocher** ne change rien : voir le fait 19.
- Le banc R-EL-4 (`ArcRun` de la carte 14, entités de la carte retirées) n'a pas d'ennemi. [INFÉRENCE] Une seule condition peut le casser : la case sous (100,100) serait dangereuse (`n = (+0x180 & 0x180) >> 7 ≠ 0`). Cela blesserait le héros pendant la pose et annulerait le sort par la garde. À vérifier en T0, sonde (s12) ; le T0 (d) de c2b mesure aussi tous les arcs.
- **R-EL-6 est à reprendre**, texte en §4.
- **Ordre h0a/c2b** : c2b laisse le pilote d'objet en bouchon nommé, appelé « à sa place » dans le bloc de blessure (D-C2B-4). h0a doit remplir ce même point. Un test de blessure réelle en dépend : à vérifier à la relecture de h0a.
- **Autre tranche, h4c1** : les clés `earth1_vs_beannoid_*` coupées « avant que le Beannoïde blesse le héros » pourront s'étendre. À noter pour elle.
- **e0k5** (avant h0a) borne le premier pas des entités nées en jeu, aux ticks non intégrés. Aucun rocher des clés ne naît contre un mur ; (s3) relit le tick de naissance du rocher de bord.

## 4. Textes proposés

Les versions complètes, avec les numéros de ligne, sont dans `notes/proposed-h4c2-revision3.md` et `notes/proposed-h4d2-revision2.md` (§9).

### R-EL-6 (remplace les lignes 11328-11330)

> **R-EL-6 — Une interruption** (révision 1, reprise le 2026-10-25 après D-E20-83) se pose comme dans le banc des clés (`e20-h4/work/oracle/interrupt_check.py`) : `TargetAnimationId = 0x31` posé sur le héros avant l'image du tick T ; la garde de h4a l'annule au tick T. C'est une blessure réelle au tick T − 1 : le bloc de blessure de c2b appelle le pilote avant de ranger l'état (`0x80031C6C`), la garde ne voit `0x31` qu'au tick suivant. E20.c2b étant faite avant h4 (D-E20-83), la DLL a l'état `0x31`, le recul de 33,09 px et la chaîne vers `0x1C` (D-C2B-6, E20.d1) : **les colonnes du héros après T (animation, X, Y, Z) et son son 23 se comparent à la clé** (celles d'h4d2 les portent ; celles d'h4c1 et d'h4c2 se complètent d'une colonne héros). Ni les PV, ni `+0x1CC`, ni le clignotement, ni la charge ne se comparent : le banc n'entre pas par le bloc de blessure. Chaque tranche d'élément ajoute un test de blessure réelle (un ennemi qui touche le héros à T − 1 ; mêmes entités et mêmes sons qu'à T), non bloquant, qui dépend de l'appel du pilote dans le bloc de blessure par h0a.

### h4c2, révision 3 (extraits)

**En-tête** : `… READY à la relecture de clôture, ae1fe95 ; révision 3 du 2026-10-25 : D-E20-56 (Q-H4C2-3, B) et E20.c2b avant h4 (D-E20-83) ; relecture de clôture à refaire`.

**Prérequis** : « …les défauts retenus de Q-H4B-4, Q-H4C2-1 et Q-H4C2-2, et la réponse de l'auteur à Q-H4C2-3 (B : le rocher arrêté en l'air par un mur ou le bord n'écrase et n'a son et coup de caméra qu'au sol, D-E20-56). E20.c2b faite avant h0a (D-E20-83) : requise par R-EL-6 révisée, pas par le code. »

**Fait à insérer après E13** :
> **Un rocher arrêté en l'air** (révision 3). *Original* : un rocher dont le pas est borné en l'air (bord de carte, mur de plateau) passe au statut 3 au tick suivant et E13 joue son impact tout de suite (animation 1, son 45, coup de caméra) ; le rocher de bord à 240 px de haut, statut 3 à b + 1, mort à b + 48 ; le rocher 8 du plateau d'octet 16 au 143 pour un sol au 146. Un rocher ainsi arrêté est au statut 3 : l'annulation ne l'atteint plus. *Corrigé* (D-E20-56, INFÉRENCE dérivée du binaire modifié en mémoire, `e20-h4c2/rockfix/`) : statut 3 et coup au tick du sol + 1 (195 et 147), mort à b + 80 pour le bord ; clés « milieu de carte » et interruptions T = 60 et T = 100 inchangées au bit près ; un rocher en l'air est annulable.

**Décisions** :
> **D-H4C2-1** (fin) — … E13 tolère un contexte nul **et n'agit dans sa première passe que si le rocher est au sol (D-H4C2-3)**.
>
> **D-H4C2-3** (D-E20-56) — Le rocher arrêté en l'air attend le sol. Première passe d'E13 (`+0x88 != 1`), si `IsOnGround` est faux : `Status = Normal` et rien d'autre. **Le statut vu à la fin du tick reste 2**, pour que `Cancel` l'atteigne encore. La garde ne porte que sur la première passe ; le second branchement n'en a pas. Elle lit `IsOnGround`, jamais `ForceAdjusted`. Précédent : E17 des flèches. Écart consigné dans le commentaire d'E13 et dans l'ADR ; valable pour le type 17 seul.
>
> **D-H4C2-4** — Clés de l'écart : `cast_2c_pf_rockfix`, `…_plateau_hb16_rockfix`, `…_hb17_rockfix`, `cancel_2c_pf_t170_rockfix`, « indicatives, non fidèles à l'octet », deux exécutions au manifeste identique. Les clés d'origine restent l'oracle de l'original.

**T0** — ajouter :
- (a) les clés de `rockfix/` et la preuve d'invariance du milieu de carte et de T = 60 et 100.
- Sondes : (s9) un rocher arrêté en l'air n'a ni statut 3 vu à la fin d'un tick, ni son 45, ni coup avant le sol ; (s10) position finale du rocher de bord et du rocher 8, glissement oblique mesuré et consigné en R-EL-5 étendue ; (s11) c2b présente et la garde annule un `0x31` posé ; (s12) la case sous (100,100) n'est pas dangereuse.

**T1** — ajouter :
- (e1) : les 20 vecteurs de première passe rejoués au sol (adaptateur `IsOnGround`).
- (e6) : première passe en l'air → statut 2, rien d'autre.
- (e7) : au sol avec `ForceAdjusted` levé → l'impact.
- (w2) : contre `cast_2c_pf_rockfix`.
- (w4) : + `…_t170_rockfix`, héros et son 23 comparés.
- (w8) : contre `…_hb16_rockfix`, impact à 147.

**T3** — quatre mutants de plus (au moins 32) :
- E13 sans la garde du sol ; garde lue sur `ForceAdjusted` ; statut laissé à 3 en l'air ; garde aussi au second branchement.

**Preuve de retrait** : neutraliser la garde rend rouges exactement (e6), (w2), (w8) et (w4) à T = 170 ; (e1), (e7), (w1), (p1), (p4) restent verts.

**Recette** : Alundra à moins de 120 px du bord ouest de l'arène : un rocher poussé contre le bord ne fait son bruit et sa secousse qu'au sol.

**Arrêts** : réponse autre que B à Q-H4C2-3, ou autre que le défaut à Q-H4C2-5 ou -6 ; sonde (s9) à (s12) contredite.

### h4d2, révision 2 (extraits)

**Prérequis** : « …les défauts retenus de Q-H4B-2 et Q-H4B-3 (corriger) et de Q-H4C1-1 (A), et les réponses de l'auteur à Q-H4D2-1 (reproduire le tour, boucles comprises, B, D-E20-57) et Q-H4B-5 (aides 32 bits propres aux tomes, D-E20-55). Ni h4b1b (abandonnée), ni h4b2 (fondue dans E20.i2). »

**But** : « …tourne vers l'ennemi de classe A le plus proche par le sens numérique des directions (0 à 31), comme l'original : de l'autre côté du passage de 31 à 0, la boule fait 5 à 8 pas de 4 au lieu d'un petit pas. »

**Fait du tour** : « Le tour sans plus court chemin, reproduit (Q-H4D2-1 B) : 240 paires sur 1 024 ; 5 à 8 pas ; 57 virages sur 321 des clés (17,8 %) ; 31 → 0 en huit pas. La DLL le reproduit : aucun réglage. » Corriger aussi la ligne 11732 (fait 18).

**Décisions** :
> **D-H4D2-1** — `FireTome1Ai.cs` : `Drive`, `Cancel`, `BallTick`, `SpriteTypes = {23}`, `Register` pour `(ProgramCTick, 63)` et le type 23 seul. Le tour est celui du binaire : comparaison numérique, pas de 4, `d` si l'écart est inférieur à 4, jamais de bouclage (D-E20-57).
>
> **D-H4D2-3** — Les aides 32 bits de `+0x274` (`Bytes[0..3]`, petit-boutiste) sont propres aux tomes (D-E20-55), sur le motif de `PickupAi.cs:420` et `PillarAi.cs:85` : une classe `internal static` neuve d'`Alundra/Ai/Items/` (nom libre, contrôlé par `check_names.py`) que h4f1 réutilise ; `EntityScriptProxy` ne reçoit aucun accesseur. Nécessaires au rejeu des vecteurs ; la production reste ≤ 16. E14 fait les siennes.
>
> **D-H4D2-6** — Chaque test pose `InclusiveBound` et `SortThreshold` ; plus de réglage de tour.
>
> **D-H4D2-7** — Clés de production : `_qfix` pour `tgt_two_wrong_order` et `tgt_topslot`, `_pf` pour les autres (MESURÉ, fait 14). Les six `_allfix` ne sont plus des oracles ; versées, marquées « non utilisées ».

**Périmètre** : `EntityScriptProxy.cs` ne garde que « la référence de cible » ; ajouter la classe des aides.

**T1** :
- (t1) les aides 32 bits.
- (k1) C63 contre 3 524 vecteurs, dont les 1 433 que la loi corrigée aurait changés.
- (k2) grille 32 × 32 : le long côté pour les 240 paires, cas nommés 31 → 0 et 0 → 31 (huit pas), écarts de 3 et 4.
- (k3) retiré.
- (k4) valeurs de production de deux réglages.

**T1 bis** :
- (w2) en production et en mode binaire, contre `_pf`.
- (w2c) retiré.
- (w3) binaire, `tgt_two_wrong_order` et `tgt_topslot`, contre `_pf`.
- (w3c) production, contre `_qfix`.
- (w6) héros comparé (R-EL-6).

**T3** : retirer `ShortestTurn` inversé et « tour corrigé aussi à l'écart 16 » ; ajouter « tour par le plus court (la correction refusée) » et « `& 31` ajouté à la comparaison (bouclage) ». La liste reste à au moins 30.

**Preuve de retrait** :
- `Drive` neutralisé laisse verts (t1), (c1), (c2), (e1), (e2), (k1), (k2), (k4), (k5), (p4).
- `BallTick` neutralisé rend rouges (k1), (k2), (k5), (w1) à (w5) (dont (w3c)), (w6) pour T ≥ 60, (w8) à (w11), (p1), (p3).

**Recette** : ajouter « de l'autre côté du passage de 31 à 0 (environ un virage sur six), la boule décrit presque un tour avant de viser, comme l'original ».

**Arrêts** : réponse autre que B à Q-H4D2-1 ou que « propres aux tomes » à Q-H4B-5.

**Retour arrière** : la classe des aides à la place de « l'accesseur ».

## 5. Ce que la révision doit faire

Aucun code, aucun test existant ne bouge.

**Fichiers**
- `docs/plan-e20-heros.md` :
  - §1.31.4 et §1.31.6.
  - R-EL-6.
  - En-tête du §1.31.
  - Trois lignes de questions.
- Annexe h4c2 : verser `rockfix/` (`fixpatch.py`, `cast_rich_fix.py`, `my_plateau_fix.py`, `cancel_fix.py`, les clés, le manifeste, un `README` qui distingue fidèles et indicatives).
- Annexe h4d2 : verser `qfix_make_keys.py`.
- `SHA1SUMS` des deux annexes à mettre à jour.

**Décisions de la session** (réversibles)
- Où vivent les aides 32 bits (classe commune aux tomes).
- Statut remis à Normal plutôt que laissé à 3 en l'air.

**Tests existants qui bougeraient à l'exécution** : seulement les deux épingles du registre (`Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:55-56` et `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:~190`), +1 par clé. Cela ne change pas avec D-E20-55, -56, -57.

**Oracles à refaire**
- h4c2 : les quatre clés `rockfix` (faites ici, indicatives), plus une colonne héros dans les clés d'interruption.
- h4d2 : aucune clé à refaire.
- e20-h4 et e20-h4b : rien (vecteurs binaires inchangés). Seul l'adaptateur de (e1) change.

## 6. Risques

1. Le glissement oblique de la DLL (fait 10) est mesuré côté binaire, non sondé côté DLL. R-EL-5 doit s'étendre à la position, sinon (w8) échoue sur quelques pixels.
2. Les clés `rockfix` sont non fidèles à l'octet. Le choix « statut remis à 2 » est structurant : laisser 3 rendrait l'annulation aveugle aux rochers en l'air et changerait les attendus à T = 170 et 190.
3. La garde dépend de `IsOnGround` pour une entité sans `Gravity` qui tombe. C'est la sonde P5 d'h4d2, et h4c2 en dépend déjà pour le tick b + 33.
4. L'équivalence « `0x31` posé à T = blessure à T − 1 » repose sur l'ordre des appels du pilote (fait 20). L'appel du pilote dans le bloc de blessure est à confirmer à h0a.
5. Même défaut non traité pour E3 : rochers de la terre 1 et boules du feu 1 éclatent aussi en l'air contre un mur. C'est la question Q-H4C2-6.
6. Plusieurs familles écrivent leurs propres aides 32 bits (e0c, g5a, tomes). Voulu par l'auteur.

## 7. Notes et corrections de texte

- Ligne 11694 : « Ni h4b1b, ni h4b2 » est périmé.
- Ligne 11732 : voir fait 18.
- D-H4D2-5 (le compte) et D-H4E1-4 ne bougent pas.

## 8. Questions à l'auteur, en termes de jeu

**Q-H4C2-5 — Après correction, qu'advient-il d'un rocher qui touche un mur ou le bord de la carte pendant sa chute ?**
Cela arrive environ une fois sur quinze près d'un bord, plus souvent près d'un mur.
- **(A, défaut : comme l'original, sauf l'instant de l'impact)** Le rocher continue de pousser pendant le reste de sa chute et s'écrase au sol.
  - Contre le bord, il glisse le long du bord. Dans la scène témoin il se pose 39 px plus loin.
  - Contre un mur, il reste collé, à quelques pixels près en oblique.
  - L'impact (bruit, secousse) a lieu au sol.
- **(B)** Il reste exactement où il a touché et tombe tout droit, donc là où l'original le laissait. Coût : une règle de plus propre au rocher (vitesse mise à zéro au contact), un test et une clé en plus. Visible surtout au bord.

**Q-H4C2-6 — Même règle pour les rochers de la terre 1 et les boules du feu 1 ?**
Ils éclatent aussi en l'air contre un mur dans l'original (même code E3).
- **(A, défaut : comme l'original, ce que tu as répondu)** Seule la terre 2 change.
- **(B)** La terre 1 aussi : h4c1, prête, est rouverte ; E3 sert 22 sprites, donc la règle serait par type.
- **(C)** Les boules aussi : elles n'éclateraient plus au mur, ce qui est étrange pour une boule de feu.

## 9. Scripts et sorties

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/h4-rev/`

- Lanceur : `run.py` (pour `python -I`). Aide de désassemblage : `bx.py`.
- Rejeux et variantes h4c2 :
  - Dossier `h4c2/oracle/` : copie du banc avec `fixpatch.py`, `cast_rich_fix.py`, `my_plateau_fix.py`, `cancel_fix.py`.
  - Sorties : `h4c2/out/` (rejeux et variantes, y compris `cancel_t170_FIX.json`, `cancel_t190_FIX.json`).
  - Clés indicatives : `h4c2/keys_rockfix/` (trois fichiers + `MANIFEST.md5`).
  - Aides : `h4c2/diffrows.py` (comparaison de clés), `h4c2/probe_cave.py` (RAM libre).
- h4d2 :
  - `h4d2/h4o/qfix_make_keys.py` (copie du banc).
  - `h4d2/out_qfix/` (dix fichiers + `manifest.txt`).
  - `h4d2/turn_law.py` (arithmétique du tour).
- Textes complets : `notes/proposed-h4c2-revision3.md`, `notes/proposed-h4d2-revision2.md`.