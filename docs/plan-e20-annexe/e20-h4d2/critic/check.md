# Critique adverse de la découverte E20.h4d2 (feu 1, objet `0x2F`)

Dépôt lu à `ed60a0eb`. Aucun changement sous `Alundra/`, `Alundra.Tests/` ni `alundra-project/Data` depuis `f81304b`. `ALUN_CD.EXE` France, SHA-1 `a705f8da…` vérifié. Le dépôt est intact (`git status` inchangé). Rien n'a été écrit hors du dossier de sortie, appelé `OUT/` dans la suite.

`OUT/` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el2/h4d2-critic`

Je n'ai pas écrit `report.md` ni `check.md` : la règle des sous-agents fait du message final le rapport. Les preuves sont dans `OUT/results/`, `OUT/scripts/`, `OUT/lst/`, `OUT/keys_proposed/`.

Légende : FAIT, INFÉRENCE, INCONNU. Verdicts : CONFIRMÉ, RÉFUTÉ, NON ÉTABLI. Gravité : P0 à P4.

## 0. Verdict

- **Les 22 faits du binaire (F1 à F22) sont CONFIRMÉS** par mes propres listings, mon propre interpréteur MIPS et mes propres comptes. Aucun n'est réfuté.
- **Les clés refaites sont correctes** : index de case écrit, `+0x48 = −1`, pas par image `_pf`. Je les ai rejouées sur deux processeurs : manifeste identique pour les 30 clés.
- **Aucun P0 ni P1.** Deux P2 (§5, points 1 et 2) et onze P3 à corriger dans le plan.
- **Mon verdict sur les tâches : READY sous réserve de deux corrections P2** (REVISE léger, pas de nouvelle découverte). Le détail est au §7.

## 1. Ce que j'ai refait moi-même

1. **Listings capstone à moi** (`OUT/lst/`, outil `OUT/tools/cdis.py`), lus avant ceux de l'autre agent :
   - pilote `0x80035A84`, C63 `0x8007AC60`, annulation `0x80034D2C`, E3, E1 ;
   - enveloppe `0x8003634C`, aiguillage `0x800350C0`, garde `0x800351B8` ;
   - requête `0x8003AF70`, `GetDirectionToTarget` `0x8002E814`, `DestroyEntityNoFx` ;
   - chargement de carte `0x8003B24C`, passe des cases `0x80038634`, création `0x80039D04`, allocation `0x80039AD0` ;
   - tables de pilotes, d'annulations et de gestionnaires natifs.
2. **Interpréteur MIPS à moi** (`OUT/tools/myemu.py`, écrit de zéro, ne vient pas de `emu.py`) :
   - rejeu de 7 482 vecteurs (3 270 + 400 + 288 + 3 524) depuis une mémoire vide : 0 écart sur le retour, les écritures et les journaux, 0 lecture hors liste ;
   - le banc d'oracle d'e20-h4 tourne sur lui à la place de `emu.py` : les 30 clés ont le même manifeste MD5 ;
   - les rejeux des scripts d'e20-h4 (1 500 + 1 500 vecteurs) : 0 écart ;
   - la sonde du compte stocké : 494 appels de la requête, 0 écart.
3. **Réexécutions à partir de copies avec leur code** : le manifeste des 30 clés est identique, les 4 MD5 de vecteurs sont identiques, leurs 36 mutants sont tous tués.
4. **Délai de chargement du R3000** :
   - Audit dynamique de mon interpréteur : 0 aléa réel sur le code des vecteurs. Un faux positif sur les clés (pc `0x80030B2C`) : l'interception de `lwl/lwr/swl/swr` par le banc contourne la comptabilité de mon interpréteur ; l'instruction qui précède est un store, pas un load, et le `lb a1` du load est 16 instructions plus haut.
   - Balayage statique de tout le segment de code : 10 sites « load puis lecture du registre chargé », aucun dans les 14 fonctions de la tranche.
   - Conséquence : « résultat de load visible aussitôt » vaut le matériel pour ce code.
5. **Expériences contrôlées** : clés avec et sans index ou pas par image, les trois défauts séparés, relief, balayage des bords de la colonne, 18 mutants supplémentaires (§4).

## 2. Verdicts sur les faits

Tous sont des FAITS que j'ai relus au listing ou aux données.

| # | Verdict | Mon contrôle |
|---|---|---|
| F1 | CONFIRMÉ | Table `0x800235F4` : `[0x2F] = 0x8003641C`, qui fait `jal 0x80035A84`. Le compteur est incrémenté après l'appel, saturé à `0x7FFFFFFF`. L'objet est remis à 0 si le pilote rend non nul. |
| F2 | CONFIRMÉ | Table `0x800233EC` : `[0x2F] = 0x80035160`, qui fait `jal 0x80034D2C`, puis `[0x80127164] = 0`. |
| F3 | CONFIRMÉ | Phase 1 → phase 1, 0 → phase 0, 2 → phase 2, autre → rend 0. |
| F4 | CONFIRMÉ | Garde, `0x33` requis, pointeur rangé avant le test de nullité, nul → rend 1, identité `[ptr+4]`, phase + 1, enchaînement dans le même tick. |
| F5 | CONFIRMÉ | Garde non nulle → rend 1 sans restauration. Identité différente → `0x34`. Sinon `compteur & 7`. |
| F6 | CONFIRMÉ | Tirage `hi(s·32)` avant la naissance. Z passé `PosZ + 0x100000` (la correction de lecture (a) est juste : les 16 px sont en Z). `ForceZ 0x80000`, `+0x274 = 0x10`, `+0x278 = 0`, `+0x27C` non écrit. |
| F7 | CONFIRMÉ | Phase 2 sans garde. |
| F8 | CONFIRMÉ | Voir les calendriers de §3. |
| F9 | CONFIRMÉ | Boucle `1..compte` inclus, statut 2 seul. Type `0x14` : effet 19/2 à la position brute, puis destruction. Type `0x17` : statut 3 et `+0x20 = 0`. |
| F10 | CONFIRMÉ | Ordre de C63 relu. Corrections de lecture (b) et (c) justes. |
| F11 | CONFIRMÉ | Comparaison numérique, pas de 4, aucun bouclage. |
| F12 | CONFIRMÉ | Voir ci-dessous. |
| F13 | CONFIRMÉ | E3 : `(+0x88 == 1 && +0xAC == 1)` → `DestroyEntity(self, −1)`, sinon `+0x88 = 1` et `(flags | 0x40) & ~0x30`. |
| F14 | CONFIRMÉ | Voir ci-dessous. |
| F15, F16 | CONFIRMÉ | Voir ci-dessous. |
| F17 | CONFIRMÉ | Trace de la boule 1 relue. |
| F18 | CONFIRMÉ | Voir ci-dessous. |
| F19 | CONFIRMÉ | Voir ci-dessous. |
| F20 | CONFIRMÉ | Voir ci-dessous. |
| F21 | CONFIRMÉ | Les 10 clés d'interruption relues. |
| F22 | CONFIRMÉ | Voir ci-dessous. |

Détails des faits qui demandent une précision :

- **F12 (requête).** Balayage `1..compte−1` exclusif (le `slt` en `0x8003AF98` et `move t6,v1` en `0x8003AFA8`) et tri seulement si `s0 ≥ 3` (`slti` en `0x8003B0B4`). Les trois mots à patcher des variantes `_qfix` et `_allfix` sont les bons : `0x0163102A`, `0x00607021`, `0x2A020003`. Le héros est ClassB (flags `0x3118C`) : il n'est jamais candidat ni touché. La colonne et les boules n'ont pas le bit ClassA.
- **F14 (gestionnaires).** Tables par pointeurs : A `0x800C4B34`, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`. A254 est en `0x800C4F2C` et vaut `jr ra ; nop` : le ne pas porter est fidèle. C63 est en `0x800C5030` (index 63). E3 est à l'index 3 de la table E. E1 est aux index 0 et 1 de la table E. D0 est vide.
- **F15 et F16 (données).** `map_alundra.json` donne bien : colonne `MoreFlags 68`, `CanPickup 32`, programmes A0 C0 D0 E1. Boule `116`, `32`, portrait `16`, programmes A254 C63 D0 E3. Indicateurs `0x2044` et `0x102074`, donc `Gravity` (`0x100`) absent. `balance.json` : puissance 64 pour la 20, 32 pour la 23, type 10. Les durées de 87 et 10 ticks ne sont lues que sur les clés. Je ne les ai pas recalculées sur les `Delay` des images.
- **F18 (bords de la colonne).** Rejoués : touché à x = 76 à 122 et y = 85 à 115, pas à 75, 123, 84, 116, tous au tick 34 (`results/edge_sweep_my_cpu.txt`).
- **F19 (sons et effets).** Les événements de toutes les clés ne contiennent que le `snd(23)` du héros blessé à T. Les effets sont les étincelles 9 et l'attaché 4 aux touches, et le `fx(19,2,6553600,6553600,1179650)` à T = 35, 60, 100, 121. XA : l'octet `0x80023365` d'indice `0x2F` vaut 2.
- **F20 (eau).** Même encodage `(0,0,0,0x08)` que la clé des rochers de h4c1, qui meurent dedans. Ici cinq boules atterrissent dans la bande d'eau et ont une vie identique à la clé plate. La boule n'a pas `Gravity`.
- **F22 (compte stocké).** 494 appels, 0 écart, aussi sur mon processeur. Le chargement de carte, la passe des cases et la création confirment le mécanisme : `0x8003B2BC` écrit `+0 = i`, `0x800386A4` le réécrit au recyclage, `0x80039D24-3C` relève le compte à `[case+0]`, `0x80039AD0` prend la plus basse case de statut 0.

Différence de couverture : sur `GetDirectionToTarget`, mon listing a 63 instructions dont 59 exécutées. Les 4 restantes (`0x8002E8C0/C4/D8/DC`) sont des sorties par défaut inatteignables, donc 100 % du code atteignable.

## 3. Les clés

**R-EL-1 (index et `+0x48`).** FAIT au binaire :
- `0x8003B2BC` écrit `+0 = i` ;
- `0x8003B218-21C` écrit `−1` dans le gabarit `+0x48` (fonction d'initialisation `0x8003B0E8` appelée en `0x8002DF80`) ;
- le gabarit est recopié à chaque chargement et à chaque recyclage.

**Comparaison avec et sans l'index** (`results/faithful_vs_not_their_cpu.txt`, même banc, 200 ticks) :

| Réglage | Boules | Objet libéré | Dernière entité | Tirages |
|---|---|---|---|---|
| Avec index, avec pas par image | 11 | 149 | 193 | 115 |
| Avec index, sans pas par image | 11 | 149 | 185 | 111 |
| Sans index, avec pas par image | 12 | 150 | 178 | 124 |
| Sans index, sans pas par image | 12 | 150 | 200 | 118 |

La ligne « sans index » retrouve les valeurs de h4 (12 boules, 150). Les clés refaites sont donc bien celles du jeu. Sur les 30 clés, `uninit_reads` vaut 0, et `rng0(t+1) = pas(rng1(t))` sur toutes les lignes.

**Calendrier refait de zéro sur les JSON.**

| Événement | Tick |
|---|---|
| Colonne (case 1) et boule 1 (case 2) nées, statut 2 à la fin du tick | S = 34 |
| Naissances des 11 boules | 34, 42, 50, …, 114 |
| `+0xAC` de la colonne levé | 120 |
| Statut 4 de la colonne (E1 en ligne, aucun effet) | 121 |
| Colonne recyclée, héros en `0x34`, phase 2 | 122 |
| Héros en animation 0 | 148 |
| Objet libéré | 149 |
| Dernière entité disparue | 193 |

Tirages : 115 pour la graine A (11 + 104 essais sans cible), 118 pour la graine B, 11 pour la clé `table_one_free`. Boule 1 : `+0x274` vaut 16 à S, 15 à S+1, 0 à S+16, première action à S+17 (`ForceZ −3 px`, requête, tirage 2). Atterrissage de la boule d'`id` 4 : L = 109, statut 3 en 110, `+0xAC` en 120, statut 4 en 121. Rampe `fx` : −8 688 par tick jusqu'au plafond, +8 688 par tick en animation 1.

**Interruptions** (clés hurt10 à hurt140) :
- T = 10, 33, 34 : rien de né, objet libéré à T.
- T = 35, 60, 100, 121 : colonne au statut 4 avec l'effet `(19, 2, …, 1179650)`, boules au statut 3.
- T = 122 : colonne déjà recyclée donc pas d'effet, boules au statut 3.
- T = 124, 140 : objet libéré, boules non annulées, vivantes jusqu'au 193.

**Séparation des trois défauts** (`results/sensitivity_split_their_cpu.txt`, premier tick d'écart, `None` = identique) :

| Scénario | Borne seule | Tri seul | Tour seul |
|---|---|---|---|
| one, three, vanish75 | None | None | 51 |
| col_hit | None | None | None |
| two_wrong_order | None | 51 | 51 |
| topslot | 51 | None | None |

L'affirmation de la dernière ligne du rapport est donc imprécise : en mode binaire `topslot` n'a aucune acquisition, il ne dépend pas du tour (§5, point 9).

**Tour qui prend le long chemin.** Avec mon `GetDirectionToTarget` : 321 virages, 57 par le long chemin, soit 17,8 %. Chacun des 321 est égal à ma lecture de `0x8007AD6C-0x8007ADB8` (`results/turn_stats_my_cpu.txt`).

**Phase A inerte** (élargissement de `EarthTome1Ai.SpriteTypes` à `{16, 23}`) :
- CONFIRMÉ : 0 enregistrement statique de page globale pour les sprites 20 et 23 sur 484 cartes (le sprite 16 en a 4, sur la carte 388, comme le dit le relevé de h4b).
- CONFIRMÉ : la table d'armes du binaire ne contient que les types 0, 1, 3, 4, `0xB`, `0xE`.
- CONFIRMÉ : sur les 102 appels de `0x80039F58`, 46 ont un type constant, et seuls `0x80035B0C` (0x14) et `0x80035BF4` (0x17) visent 20 et 23.
- NON ÉTABLI : 56 appels ont un type dynamique, comme pour le sprite 154 dans `check.md` d'e20-h4.

## 4. Vecteurs, couverture, mutants

- **Couverture recomptée par moi** :

| Fonction | Instructions exécutées |
|---|---|
| pilote | 120/120 |
| garde | 19/19 |
| annulation | 55/55 |
| `DestroyEntityNoFx` | 15/15 |
| E3 | 25/25 |
| C63 | 95/95 |
| requête | 94/94 |
| tri | 91/91 |
| dirto | 59/63 (4 inatteignables) |

- **Drapeaux `binary_only`** : je les ai dérivés par un critère indépendant (le vrai code exécute les stores de l'enfant ou du porteur de `DestroyEntityNoFx`). Accord total : 87 sur 3 270 pour le pilote, 81 sur 400 pour l'annulation.
- **Mutants** : leurs 36 sont tous tués (de 20 à 3 211 vecteurs). J'en ai ajouté 18 (`results/extra_mutants_vs_vectors.txt`) : 17 tués, 1 survivant.
  - Le survivant, « colonne détruite avant la création de l'effet », est équivalent : l'effet prend des coordonnées, pas un ancrage.
  - Autres équivalents à déclarer : seuil du tour `<= 4` ou `< 5` (écart de 4 : saut à la cible ou pas de 4, même résultat).
- **Défaut du tour en mode corrigé** : sur la grille 32×32, 240 paires sur 1 024 ont un écart numérique supérieur à 16. Le mode corrigé diffère du binaire sur 1 433 des 3 524 vecteurs de C63.

## 5. Affirmations du rapport à corriger

Aucune ne touche un fait de comportement du binaire.

1. **P2, mots d'identité sans membre dans la DLL.** Les vecteurs lisent et écrivent `+0x04`, `+0x27C`, `[0x80127120]` et `[ptr+4]`. Aucun n'a de membre : la DLL n'a pas de compteur de naissance. `ItemState` existe à `+0x27C` mais n'est jamais utilisé, et `EntityRefId` est l'équivalent de `+0x48`, pas de `+4`. La règle de D-H4B1-3, reprise par T0 (b) (« un octet d'entité sans membre arrête la tranche »), arrêterait donc la tranche dès T0 (b). Le rapport dit seulement que `+0x27C` « n'est pas reflété », sans trancher.
   - À écrire dans le plan : l'identité est « même référence et statut Normal ». L'adaptateur de vecteurs construit une référence morte (proxy détruit) quand l'identité lue diffère. Pointeur ↔ `Slot` pour `+0x278` et `[0x8012711C]`.
   - Deuxième règle de l'adaptateur à écrire dans (b) : le Z du binaire vaut le Z de la DLL plus 1 (ADR-0026). Il joue sur les lectures du pilote et de l'annulation, et sur la position de l'effet `(PosX, PosY, PosZ + 1)`. La convention de `NativeEffects` (Z « en repère du binaire ») le confirme.
2. **P2, aucune clé de relief.** Les 30 clés ne couvrent que le plat, un mur (colonne 6), l'eau et les bords de carte. La boule est la première entité sans `Gravity` qui tombe et atterrit : dans la DLL elle passe par `RunLogicalZPreXyStep` avec `gravity = false` (atterrissage « `PosZ + ModZ + F < T` » strict, `StepHeight` 0, `ComputeFloorHeight`), un chemin distinct de celui des rochers à gravité de h4c1 et h4c2 (qui, eux, ont des clés de relief). Le relief change la vie des boules (`results/relief_sensitivity_their_cpu.txt`) : plateau de 16 px, 5 boules sur 11 changent ; plateau de 32 px, 6 sur 11.
   - Les recettes de l'auteur ont lieu dans l'arène plate de la carte 14 : elles ne le verraient pas.
   - J'ai produit 3 clés de relief sur la machine refaite (deux exécutions au MD5 identique, `uninit` 0, dans `OUT/keys_proposed/`) :

| Clé | MD5 |
|---|---|
| `fire1_relief_plateau_hb1_cols6_12_pf` | `67573c5991a2fd9abc7a46acefc95db8` |
| `fire1_relief_plateau_hb2_cols6_12_pf` | `ae285b07423a8b0300560c43f1ccc8fc` |
| `fire1_relief_hero_on_plateau_hb2_pf` | `d777cef6043d80fef2957fd617adb18b` |

   - La dernière met le héros sur le plateau (Z = 32 px + 1). Colonne à z = 50 px, boules nées à 56 px, certaines tombent à 0 hors du plateau.
3. **P3, prérequis.** « Pas f4, pas f6a » est trompeur : h4c1 exige f4, f5 et f6a et h4d2 exige h4c1, donc h4d2 les exige par transitivité. À inscrire dans le tableau de découpage et dans les arrêts. L'autre ordre est de faire passer h4d2 avant h4c1 en lui faisant porter `ImpactDeactivate` et `(E, 3)` pour `{23}` ; il change alors le texte de h4c1, qui fixe `{16}` seul.
4. **P3, `ItemSpawnedEntity`.** `dll/state.md` dit que h4d2 le crée s'il est absent ; le corps du rapport le liste seulement comme prérequis. Aligner (« deux ordres admis » vaut aussi pour ce champ).
5. **P3, preuve de retrait de `BallTick`.** Elle dit « (w6) pour T ≥ 35 » : c'est faux pour T = 35. La boule née au tick 34 est annulée à l'en-tête du tick 35, avant son premier passage de C63. `hurt35` : `b274` reste à 16, la boule monte de 8 px par tick pendant ses 10 ticks d'animation 1 (z = 112 au dernier). Neutraliser `BallTick` laisse (w6) T = 35 vert. Corriger en « T ≥ 60 ».
6. **P3, preuves de retrait manquantes ou ambiguës :**
   - (w8) n'a pas de scénarios nommés. S'il inclut une interruption, « neutraliser `Cancel` » le rougit aussi.
   - « `SpriteTypes` remis à `{16}` » rougit aussi (w6) pour T = 35, 60, 100, 121, 122 (les boules annulées ont besoin d'E3), (p1) et (p3), pas seulement « une boule qui touche ».
7. **P3, « aucun test ne fait naître le type 23 » est faux à la lettre.**
   - Les boucles sur les 151 préfabs globaux du vecteur v1 (dont `g20` et `g23`) en font naître : `AlundraSpawnBySpriteIndexInterfaceTests.cs` B2 (chemin complet, naît puis recyclé), `AlundraBirthBoxTests.cs` B2, `AlundraSpawnBySpriteIndexTests.cs` B.
   - Aucun ne fait tourner d'image : l'inscription de `(C, 63)` et l'élargissement d'E3 ne les changent pas. La liste prédite doit dire « née, jamais tickée », et T0 (d) la recompte.
8. **P3, (p4).** Après la phase A (`SpriteTypes` déjà `{16, 23}`), la liste « avant » de `UnhandledWeaponSpriteCodes({20, 23}, …)` est `[(C, 63)]`, pas `[(C, 63), (E, 3)]`. `UnhandledWeaponSpriteCodes` ne regarde que les programmes C et E (A254 n'y compte pas).
9. **P3, `topslot`.** Il ne discrimine pas le tour (aucune acquisition en mode binaire, §3). Il ne tue que « borne ».
10. **P3, (k5).** Le cas « entité sans `CurrentAttack` » n'a pas d'oracle : le binaire lit l'octet à l'adresse 0. La boule garde son enregistrement d'attaque (attribut 10, puissance 32) à tous les ticks, statuts 3 et 4 compris. Soit définir le comportement de la DLL, soit retirer le cas.
11. **P3, texte de la recette.** « Un ennemi perd 32 par boule et 64 par coup » ne vaut que pour une réponse neutre (16, la cible du banc). Les dégâts sont fonction de la puissance et de la réponse de la fiche de l'ennemi.
12. **P3, colonnes de (w1).** « Colonnes du §3.1 » inclut `id`, `b27c`, `count`, `ident`, sans membre dans la DLL. Nommer les colonnes comparées et exclure celles-là, la relation « même entité » les remplaçant.
13. **P4, divers :**
    - Le « plus de f6a » du rapport est exact au sens direct (la boule n'a pas `Gravity`, la pente n'est pas nourrie).
    - Le rapport note que l'accesseur 32 bits de `+0x274` est nécessaire. Il l'est pour rejouer les vecteurs, pas pour le comportement de production (valeurs ≤ 16 et un seul octet utilisé).

## 6. Ce qui manque

- Les 3 clés de relief ci-dessus avec leurs tests (w9, w10, w11) et une sonde de DLL (P14) : la boule atterrit sur un plateau à `z = 16 / 32` et tombe hors du plateau à `z = 0`, avec les ticks des clés.
- La règle d'adaptation des identités et pointeurs (§5, point 1) dans T0 (b), plus le décalage de Z de 1.
- Une cible ClassA et Collidable (sprite 25, flags `0x6081`, comme le héros) sous la boule qui tombe : le support « atterrir sur une entité » du chemin sans gravité de la DLL n'est pas exercé par les clés, car les boules touchent avant d'atterrir. INCONNU côté binaire pour un sujet sans `Gravity`.
- Un recast après la fin du premier sort (pointeur et identité globaux restants), optionnel : la phase 0 les remplace avant lecture, donc risque faible.
- Les scénarios nommés de (w8).
- Les décisions D-H4D2-n à numéroter dans le plan (fichier et nom, accesseur, référence de cible, élargissement d'E3 de `{16}` à `{16, 23}`, règle du compte = maximum des `Slot` vivants, réglage `ShortestTurn`).

## 7. Verdict sur les tâches

- **T0 (annexe, oracle, sondes, liste prédite) : accepter**, avec :
  - (b) complété (identités, pointeurs, Z) ;
  - la sonde de relief ;
  - une sonde « `GetDirectionToTarget` de la DLL égal au binaire » sur des différences brutes 16.16 de tout signe ;
  - la liste prédite recomptée avec les boucles v1.
  - P1 à P13 : toutes réfutables et cohérentes avec les clés. Je vérifie P1, P2, P4, P5 (L = 109 et statut 3 en 110), P6, P7, P8 (paires de bord rejouées), P9, P10, P11, P12, P13.
- **T1 (phase A) : accepter**, avec :
  - le point 1 et le point 12 de §5 ;
  - (k5) corrigé ;
  - (w8) nommé ;
  - (w9) à (w11) ajoutés ;
  - option : scinder la phase A en A1 (champs, accesseur, `Cancel`, élargissement d'E3, tests de vecteurs) et A2 (`Drive`, `BallTick`, clés de monde), vu la taille pour un exécuteur à deux tentatives.
- **T2 (phase B) : accepter**, avec (p4) corrigé.
- **T3 : accepter.** Ajouter mes 18 mutants de vecteurs, les équivalents déclarés (§4), et ces mutants de monde :
  - C63 qui tourne au tick de naissance ;
  - E3 élargi à 24 ;
  - le relief.
- **Preuves de retrait** : exactes sauf §5, points 5 et 6.
  - Retirer `FireTome1Ai.Register` : (p1), (p3), (p4) et les deux épingles rouges, (p2) et (e2) verts. Exact.
  - Remettre les trois entrées `0x2F` au bouchon : (p1), (p2), (p3) rouges, (p4) vert. Exact.
  - Remettre la seule annulation au bouchon : (p3) seul. Exact.
  - Neutraliser `Cancel` : exact, sous réserve de (w8).
  - Neutraliser `Drive` : exact.

## 8. Questions pour l'auteur

La formulation de Q-H4D2-1 est exacte (17,8 % de virages longs, à vérifier sur les 321 virages). Les défauts hérités (Q-H4B-2, Q-H4B-3, Q-H4B-5, Q-H4-1, Q-H4C1-1) restent à leurs défauts. Je n'ajoute aucune question. Si l'auteur répond B (reproduire), les clés binaires suffisent ; ses autres réponses obligent à refaire les clés `_allfix`, comme le dit déjà la liste d'arrêts.

## 9. NON ÉTABLI et INCONNU

- **INCONNU** : la sortie de la DLL (rien n'a été construit ni exécuté). `IsOnGround` pour une boule sans `Gravity` qui tombe (P5), le support « atterrir sur un Collidable » sans `Gravity`, le rendu en jeu, la piste XA, le comportement de la création d'effet quand la réserve est pleine.
- **NON ÉTABLI** : 56 sites de `0x80039F58` à type dynamique ; les durées de 87 et 10 ticks recalculées sur les `Delay` ; l'équivalence entre le banc et le jeu réel hors du banc (terrain plat, hero Z = 1, cibles immobiles).

## 10. Fichiers

Tous sous `OUT/` :

- `lst/` : mes listings (`driver_80035A84.txt`, `c63_8007AC60.txt`, `cancel_80034D2C.txt`, `e3_8007EDA0.txt`, `query_8003AF70.txt`, `dirto_8002E814.txt`, `destroy_nofx_8003A774.txt`, `f_800350C0.txt`, `f_800351B8.txt`, `f_8003634C.txt`, `f_8007ED10.txt`).
- `scripts/` (copies de `tools/` et des scripts de `run_theirs/` et `run_mycpu/`) :
  - mes outils : `myemu.py` (interpréteur), `cdis.py` (listings), `replay_mine.py` (rejeu des vecteurs), `turn_stats_mine.py`, `keyfacts.py` ;
  - mes expériences : `sens.py`, `relief.py`, `nofaithful.py`, `edge_sweep.py`, `extra_mutants.py`, `make_relief_keys.py`.
- `results/` :
  - `manifest_rerun_their_cpu.txt`, `manifest_rerun_my_cpu.txt` et leurs `_diff_` : identiques aux leurs ;
  - `vec_regen_manifest.txt` ;
  - `replay_my_cpu.txt`, `load_delay_hazards_keys.txt`, `probe_count_my_cpu.log` ;
  - `faithful_vs_not_their_cpu.txt`, `sensitivity_split_their_cpu.txt`, `relief_sensitivity_their_cpu.txt` ;
  - `turn_stats_my_cpu.txt`, `edge_sweep_my_cpu.txt` ;
  - `extra_mutants_vs_vectors.txt`, `their_selftest_36_mutants_rerun.txt` ;
  - `h4_vec_drivers_my_cpu.log`, `h4_vec_handlers_my_cpu.log`, `relief_keys_manifest.txt`.
- `keys_proposed/` : les 3 clés de relief.
- `run_theirs/` et `run_mycpu/` : copies de travail du banc. `run_mycpu/h4o/emu.py` est mon interpréteur, `run_mycpu/keys_b/` contient les 30 clés régénérées avec lui. Rejouer à partir de copies, jamais sur place.