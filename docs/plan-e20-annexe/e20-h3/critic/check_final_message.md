# Contre-vérification adverse du rapport h3bin (bombe 0x23, graines 0x1F)

Rien n'a été écrit dans le dépôt (`8887a488`) : aucune commande Git en écriture, aucun build, aucun test du dépôt. Tout est sous `C` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h/h3-critic/`. Le binaire est `ALUN_CD.EXE` France, SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, vérifié. `git status --short` montre deux sous-modules marqués ` m`, état qui ne vient pas de moi. Le même texte est dans `C/check.md`.

**Étiquettes.** FAIT = vu dans le binaire, les données ou le code. INFÉRENCE = déduit, source dite. INCONNU = non établi. Verdicts sur les affirmations du rapport : **CONFIRMÉ**, **RÉFUTÉ**, **NON ÉTABLI**. Preuves :
- [L] désassemblage relu par moi.
- [V] vrai code MIPS contre mes modèles sur vecteurs aléatoires.
- [M] mesuré par mes scénarios dans le jeu émulé.
- [D] donnée.
- [DLL] code du dépôt.
- [DEC] décompilation, lue et jamais corrigée.

## 0. Verdict

**Le rapport est fiable sur tout ce qui porte une décision de comportement.** Je n'ai trouvé aucun écart sur les 12 fonctions natives, la chronologie, la limite de 3 bombes, la fusée, la zone et les dégâts de l'explosion, l'herbe, ce que font les graines, les annulations et les refus. Trois points sont à corriger avant d'écrire les tâches. Mon verdict prévaut.

1. **RÉFUTÉ (partiel) : la liste des états d'où Rond démarre l'objet (§2.5) est incomplète, et la phrase « pas depuis les états d'attaque, de blessure » est fausse.**
   - Rond démarre l'objet depuis 63 états sur 80, dont les 25 états des deux groupes d'attaque et l'état de blessure `0x31`.
   - Coup d'épée en cours puis Rond 2 ticks plus tard : la bombe naît et le coup est interrompu.
   - Rond 1 tick après un lancer, ou 1 tick après avoir été touché : une deuxième bombe naît.
   - La DLL le sait déjà (`PlayerManager.cs:596-619`, `:1025-1031`) : l'erreur est documentaire, mais elle ferait écrire un faux test d'acceptation.
2. **L'arc du couvercle de la 362 (E20.A362) n'a besoin ni de D30 ni de E12 pour que le couvercle réagisse. Il lui manque `(E, 19)`, gestionnaire absent du plan (sprites globaux 241 à 243).**
   - Le rapport écrit « avec h3a elle peut tourner dès que D30 et E12 existent » : faux ou insuffisant.
   - E12 ne sert qu'à retirer la bombe d'Olen après l'explosion ; D30 n'y sert pas. Voir A2.
3. **`goldens_h3.json` n'est pas rejouable depuis une mémoire vide.**
   - 61 vecteurs sur 480 divergent. Ses mots d'entrée omettent tout mot égal à un état de base non nul (sol du héros, lien de portage, variable d'objet en cours).
   - J'en fournis un autonome : `C/results/goldens_critic.json` (300 vecteurs, tous les mots lus listés, rejeu depuis une mémoire vide : 0 écart, 0 lecture hors liste).

Le découpage proposé tient (§5), avec trois ajustements :
- A362 avant h3a, sans prétendre dépendre de D30.
- `(E, 19)` ajouté à h3a ou à A362.
- Câblage des 63 états dans h0a, avec les bons critères d'acceptation.

## 1. Ce que j'ai fait

- **Relecture capstone de toutes les fonctions citées**, sans lire les listings de l'autre agent. Départs `0x80034224` et `0x8003453C`, `UseItem` `0x8003499C`, `TryUseItem` `0x8002ED64`, pilotes, enveloppe `0x8003634C`, aiguillage `0x800350C0`, annulations, C62, D30, E12, C73, E21, pot `0x8007BB9C` et `0x800619DC`, `CheckAndTriggerTileEffect` `0x8003A7B0`, `ComputeNewHp` `0x8003A374`, `0x8004464C`, `0x80044550`, `InitializeEntity` `0x80039D04`, `SpawnWarpEntity` `0x80039F58`, la passe des événements `0x800386D0`, le répartiteur de tick `0x8003B388`, `MovePlayer` `0x80031B50` et ses gestionnaires d'états, E19 `0x8007F690`. Listings dans `C/lst/`.
- **Modèles écrits par moi** (`C/run/my_models.py` : les 12 fonctions plus A20 et C74), comparés au vrai code dans l'interpréteur MIPS (copie du harnais d'oracle de h4). Toute la RAM est comparée octet par octet, ainsi que le journal des appels simulés (son, effet, naissance, destruction, coupe d'herbe). Je n'ai importé aucun modèle de l'autre agent.
  - Série finale : 6 000 vecteurs par fonction, 0 écart (`my_diff_final_6000.txt`). Série précédente : 20 000 par fonction, 0 écart.
  - Couverture à 100 % des instructions : start_beans 63/63, start_bomb 81/81, pilotes 23/23, annulations 34/34, C62 186/186, D30 18/18, E12 36/36, C73 27/27, E21 43/43, A20 3/3, C74 124/124.
  - Aiguillage 38/62 : les 24 mots non couverts sont les entrées des annulations de tomes `0x2C` à `0x30` (`0x80035130`-`0x8003518C`), domaine de h4.
  - Piège relevé : dans C74, le `sra 1` s'applique après la soustraction enveloppée sur 32 bits, `(repos − Z) & 0xFFFFFFFF` puis décalage signé. Un port qui soustrait en 64 bits diverge.
- **Sensibilité de mes tests.**
  - Modèles mutés un mot à la fois : 59 tués sur 67, les 8 survivants sont équivalents.
  - Vrai code muté un mot à la fois : 1 156 mutants. 227 survivants avec 120 vecteurs, 194 après relance à 700 vecteurs, puis une dernière relance des mots hors aiguillage avec le générateur final. Deux bornes de boucle de C62 ont été relancées avec 3 000 vecteurs.
  - Les survivants sont tous expliqués :
    - 123 de pile, registres sauvegardés et `move` par `addu/subu` avec `$zero`.
    - 43 dans l'aiguillage : arguments d'annulation jamais lus, chutes dans les entrées de tomes, gardes sur variable nulle au même résultat.
    - 13 dans C62 : écritures mortes des 32 bits hauts, première écriture du générateur écrasée par la seconde, gardes de pointeur nul qui n'écrivent que zéro à l'adresse `0x2C`, delay slot d'`addiu $v0,$zero,4` qui doit seulement différer de `$v1`, `move $a2,$a0` redondant, seuil `< 61` au lieu de `< 60` (60 n'est pas multiple de 8).
    - 12 ailleurs : `move $a0,$s0` redondant d'E12, gardes nulles équivalentes de C73, E21, pilotes et annulations, `jr $ra` qui retombe dans l'entrée suivante sans effet.
  - Les mutants de bornes de boucle (4 coins → 5, 64 emplacements → 65) n'ont été tués qu'après renforcement du générateur (5e mot pointeur, emplacement 64, mode « aucun coin sur un mur ») : environ 1 % des vecteurs les voient.
- **Scénarios dans le jeu émulé** : vraies passes d'entités, d'animation, de physique et de coups, vraie `SpawnWarpEntity`, vrai `ComputeNewHp`. Seuls les sons et les effets sont journalisés (`C/run/scen_*.py`, sorties dans `C/results/`).
- **Données** : 483 cartes, `BALANCE.BIN.json`, `map_alundra.json`, `effects-global.json`, table d'opcodes `EventOpcodeSizeTable.cs`, `ChainMaps`.
- **Délai de chargement R3000** : aucune lecture d'un registre chargé par l'instruction précédente dans les 17 fonctions étudiées (`my_loaddelay.py`, 0 cas). L'absence d'émulation du délai de chargement est donc sans effet ici. Les `lwl/lwr/swl/swr` de l'interpréteur sont corrects (relus).

## 2. Verdicts, affirmation par affirmation

| # | Affirmation du rapport | Verdict | Preuve |
|---|---|---|---|
| 0.1 | Ni la bombe ni les graines ne coûtent ni ne se consomment | **CONFIRMÉ** | [L] `UseItem` aiguille `0x1F` vers `0x80034224` et `0x23` vers `0x8003453C` (table `0x8002339C`) ; les deux départs n'appellent que `SpawnWarpEntity` et le son 3. Les seuls appelants de la consommation `0x8004E5C4` sont les objets `0x24`-`0x29`, la potion de secours `0x27` (`0x8002BAEC`) et l'opcode `0x84` (`0x8003FF34`). Précision : le compte maximal lu par `AddItem` est dans la table `0x800B9FE8 + 10·id + 6` (demi-mot) : 1 pour `0x1F`, `0x23`, `0x2B`, 9 pour `0x24`, comme l'octet 7 de `0x800C5F7C + 8·id` cité par le rapport. |
| 0.2 | Refus si le héros porte, pente 4, objet en cours, ou 3 bombes ; ordre des tests | **CONFIRMÉ** | [L][V] 20 000 vecteurs. [M] son 3 au Rond pendant la levée (t10), pendant le portage (t60, t62) et au 4e départ (t211). Mutants `>= 3`→`> 3` et `>= 2` tués. |
| 0.2 | Le départ ne regarde ni l'animation ni les PM ; possible en plein saut | **CONFIRMÉ** | [M] Rond à t12 en l'air → héros `0x0B`, l'objet naît à Z 27,5 ; atterrit en portant (`air_start.txt`). |
| 2.5 / 0.2 | Liste des états de départ ; « pas depuis les états d'attaque, de blessure, de mort » | **RÉFUTÉ (partiel)** | Voir R1. Mort : CONFIRMÉ, la branche `HP == 0` (`0x80031CA0`) précède l'aiguillage d'états. |
| 0.3 | h3 n'exige ni g1b, ni g2b, ni g3 ; la bombe posée n'est pas relevable | **CONFIRMÉ** | [L][V] C62, C73, E21, D30, E12 ne lisent que `+0x24` et `+0x28`, et écrivent `+0x2C` du porteur ; jamais `TryLift`. [M] Carré devant une bombe ou une graine posée : coup d'épée (état 16), aucun soulèvement (`relift.txt`). |
| 0.4 | Fusée : 240 puis 241 appels, 7 étincelles, statut 3 à T+242 | **CONFIRMÉ** | [V] ; [M] tick par tick identique (tableau 3). |
| 0.5 | Indicateurs `0x4E`, attaque (4, 5), dégâts `max(1,(5·r)>>4)`, réponse `0x40` tue, `0x80` rien | **CONFIRMÉ** | [L] `0x8004464C` ; [V] règle contre le vrai code sur 512 fiches × 8 PV, 0 écart. La liste des 45 fiches à mort immédiate est identique à la mienne. |
| 0.5 | « 147 fiches : 74 / 45 / 18 / 6 / 2 / 1 / 1 » | **NON ÉTABLI** (population) | Je retrouve exactement les 45 morts. Pour les fiches de cartes à PV max > 0, j'en compte 133 : 68 prennent 5, 45 meurent, 11 immunisées, 5 prennent 1, 2 prennent 3, 1 prend 9, 1 prend 10. Le filtre des « 147 » n'est pas reproductible. Non décisif. |
| 0.5 | Une bombe touchée par un attribut 4, 6 ou 10 explose | **CONFIRMÉ** [L][V][M] | Précision A3 sur les classes. |
| 0.6 | La bombe ne casse rien ; E12 appelle CATE à chaque tick ; herbe coupée pendant les 15 ticks de boîte | **CONFIRMÉ** [M] | Voir R5 pour les précisions (effet, butin). |
| 0.7 | Graines : aucune attaque ; seul usage = réveiller le pot ; 39 pots, 16 cartes, aucune sur la chaîne ; C74 et A20 dans aucune tranche | **CONFIRMÉ** | [D] 39 placements, 16 cartes, 0 sur les 30 cartes de la chaîne. [M] pot réel (fiche de la carte 12), vol, réveil de 22 ticks. [V] C74 et A20 : 10 000 vecteurs, 0 écart, 124/124 et 3/3. Ni le plan ni la DLL ne citent C74, A20 ni `0x199`. |
| 0.8 | Menu Start refusé tant que l'objet est en main (`0x8002BC90`) | **CONFIRMÉ** [L] | Garde exacte : `[0x800DC4B8] == 0`, `H+0x20 == 0`, `[0x80127164] == 0`, front `0x12 & 0x803`, `[0x800DC4B4] == 0`, maintien `& 0x100 == 0`, `[0x800C4980] == 0`. Un second site `0x8002BB94` refuse aussi la potion de secours (appui `0x900`). |
| 0.9 | Décompilation fidèle sauf D1 (annulations testent `+0x20`) | **CONFIRMÉ** | [DEC] `PlayerManager.cs:4177-4222` teste `BlockedByEntity` (`+0x20`) ; le binaire teste `+0x24`. [M] mort avec une bombe : elle est lâchée, tombe, continue sa fusée sans fumée. D2 : `AnimateWarpEffect` (`PlayerManager.cs:3983`) est bien l'aiguillage `0x800350C0` et `g_warpLockTimer` est `[0x80127164]`. |
| 0.10 / 9 | Sur la chaîne, seule la carte 10 donne la bombe, aucune les graines | **CONFIRMÉ** | [D] Recensement par mes soins (opcodes décodés avec la table de tailles du dépôt) : `0x82 0x23` en 10, 17, 31, 77, 80, 158, 331, 334, 365, 439, 443 ; `0x82 0x1F` en 17, 144, 439 ; octet 995 du programme B[8] de la 10. Bombe posée par une carte : la 362 seule (enregistrement 5). |
| 2.2 / 2.3 | Détail des deux départs | **CONFIRMÉ** [L][V] | |
| 3.1 / 3.2 | Pilotes, aiguillage, annulations, sites d'appel | **CONFIRMÉ** [L][V] | Sites `0x80031BFC`, `0x80032758`, `0x80032770`, `0x800327B4` ; pilotes `0x80035244`, `0x800352A8`, `0x80035304`, `0x800351E8`. |
| 3.2 | Contrôle verrouillé `& 0x34` : annulation, effet 12 à 32 px, aucune explosion ; eau profonde | **CONFIRMÉ** [M] | Verrous `0x20`, `0x10`, `0x04` : annulation au tick suivant. Voir A1 pour le gel du monde `& 0x48`. Eau : bombe et graines lancées dans des cases de pente 4 meurent à l'atterrissage avec l'effet 6 (`water.txt`). |
| 4.1 | C62 en détail | **CONFIRMÉ** [L][V] | |
| 4.2 | D30 | **CONFIRMÉ** [L][V] | Chaîne [M] : les bombes 2 et 3 explosent avec la 1re (t262), à +18 ticks du début de l'animation de la 1re, comme le rapport. |
| 4.3 | E12 ; chronologie ; boîtes | **CONFIRMÉ** | [D] Boîtes relues dans `map_alundra.json` : ticks 16 à 18 → 16×8×11 ; 19 à 22 → 28×20×19 ; 23-24 → 44×28×27 ; 25 à 30 → 48×32×31 ; animation de 51 ticks. |
| 4.3 | Zone : « touché jusqu'à 36 px en X et 20 px en Y, pas à 40 ni 24 » | **NON ÉTABLI** tel quel | Avec une vraie cible (racine, sprite 253, boîte 24×16) : touché si \|dx\| ≤ 35 et \|dy\| ≤ 23, non touché à 36 et 24. Règle : recouvrement strict entre la boîte finale 48×32 et la boîte de la cible, donc le bord dépend de la cible. Les premiers ticks de contact coïncident (+17, +24, +26 depuis le début de l'animation). Un seul coup par cible : [M] le héros perd 5 PV une fois (10 puis 5). |
| 4.4 | Héros : 5 PV sur 10 | **CONFIRMÉ** [M] | Feuille de travail du héros `0x80127008` : réponses 16 aux types 1 à 11 (kit de départ). Dépend de l'armure équipée. |
| 4.5 | Couvercles, racine, secrets | **CONFIRMÉ** [D] | 30 couvercles (243 : 8, 242 : 18, 241 : 4), 29 programmes D avec `0x87` indice 3 dans 13 cartes, racine 253 en carte 8 (enregistrements 17 et 22). Voir R7 pour les sites `0x95`. |
| 5.1 / 5.2 | C73, E21, chronologie des graines | **CONFIRMÉ** [V][M] | Atterrissage Ts+22, statut 3 Ts+23, fin d'animation Ts+24, statut 4 et effet 8 Ts+25, case libre Ts+26. |
| 5.3 | Pot : A20, C74 | **CONFIRMÉ** [V] | Pot réveillé en 22 ticks avec un effet 14 tous les 4 ticks, graine détruite sans effet attaché (`beans_pot.txt`). |
| 6 | Interactions (marche, saut, Rond refusé, Triangle sans effet, lancer, mort, case de sortie, eau) | **CONFIRMÉ** pour ce que j'ai rejoué | Portées de lancer : 34,5 px à l'est et à l'ouest, 23 px au nord et au sud, identiques pour les graines (`throw_dirs.txt`). Blessure en portant : non rejoué, donc non établi par moi. |
| 7 | Rejeu de référence (`goldens_h3.json`) | **RÉFUTÉ** (autonomie) | Voir R3. |
| 8 | D2 et D3 (noms) | **CONFIRMÉ** pour D2 ; D3 non relu | Sans effet. |
| 10 | État de la DLL | **CONFIRMÉ** | `TryUseItem` `PlayerManager.cs:932`, `PlayerTryAction` `:940`, `UpdateItemUseDriver` `:1507` vide. `IsInlineDeactivateCode` : `<= 1` ou code 10 seulement (`EventProgramRunner.cs:328`). Registre `CreateForProduction` : Beannoïde, Muruta, vague. Garde de menu absente (`InventoryDirector.cs:419`). `CheckAndTriggerTileEffect` est un bouchon (`WaveSwordAi.cs:40`). |
| 13/14 | Sons des effets, cadence | **NON ÉTABLI** (je ne l'ai pas non plus) | `effects-global.json` ne porte aucun son. Je n'ai pas trouvé le `VSync(n)` de la boucle de jeu : 50 ticks par seconde reste une INFÉRENCE. |

**Tableau 3 : chronologie de la bombe (T = tick de Rond), mesurée par moi, identique à celle du rapport.**
- T : statut 1, sons 0 et 44, `+0x274 = 240`.
- T+1 : statut 2, Z de l'objet 1,0.
- T+2 : première décrémentation (239).
- T+32 : Z 32.
- T+36 : héros `0x0C`.
- T+186, +194, … +234 : étincelles (7).
- T+242 : statut 3, mains libres ; l'objet reste « en cours » jusqu'à T+243.
- T+243 : animation 1 et son 46.
- T+259 : premier tick de boîte. T+260 : premier dégât (héros 10 → 5, état 49).
- T+294 : fin d'animation. T+295 : statut 4. T+296 : case libre.

## 3. Réfutations et corrections

### R1. États d'où Rond démarre l'objet (RÉFUTÉ, partiel)

**FAIT [M]** (`scen_allstates.py`, `states_circle_all.txt`). Pour chacun des 80 états (clé `TargetAnimationId`, table `0x80022D3C`), Rond est pressé un tick, bombe équipée, rien en main, objet non en cours, au sol et en l'air. Conditions : état du banc (héros sur sol plat) ; je n'ai pas exploré les gardes propres aux gestionnaires d'échelle, d'escalade ou de sort.
- **63 états démarrent l'objet** : `00 01 02 04 05 06 07 08 09 0A 0B 0C 0E 0F 10 11 12 13 14 15 16 17 18 19 1C 1D 1F 20 21 22 23 24 25 26 28 29 2A 2B 2C 2D 2E 2F 30 31 32 33 34 35 39 3A 3B 3C 3E 3F 40 41 42 44 46 49 4B 4E 4F`.
  - Les états `05 06 07 0B 0C 2E 2F 30` ne démarrent que sans objet porté ; en jeu `+0x2C != 0` donne le refus, son 3.
- **17 états sans réaction** : `03 0D 1A 1B 1E 27 36 37 38 3D 43 45 47 48 4A 4C 4D`.
  - Leur gestionnaire est `0x80032758`, `0x80031FF4` (état 03) ou `0x800325E8` (état 36) ; `0x80032758` appelle l'annulation d'objet.
- **Les 25 états absents de la liste du rapport** sont ceux des groupes d'attaque :
  - `0x80031E18`, au sol : `04 08 09 10 11 12 13 18 19 25 3F 40 44 49`.
  - `0x80031DF8`, en l'air : `0A 14 15 16 17 1F 26 41 42 46 4B`.
  - Ces gestionnaires règlent l'animation puis sautent à `0x80032680`, qui appelle `TryUseItem` et termine le tick, résultat ignoré. La DLL a la même structure.

**Mesures :**
- Coup d'épée à t2, Rond à t4 : l'objet naît à t4, état 16 → 5.
- Lancer puis Rond 1, 2, 3, 5 ou 10 ticks plus tard : seconde bombe née au tick de Rond, état 9 → 5.
- Héros touché par l'explosion (état 49 à t261), Rond à t262 : seconde bombe née (voir A4).

**Conséquence.** Le test d'acceptation « Rond ne fait rien pendant un coup d'épée ou un lancer » serait faux. À écrire : « Rond démarre l'objet depuis ces 63 états ; il interrompt un coup d'épée ou la pose du lancer ».

### R3. `goldens_h3.json` (RÉFUTÉ comme artefact autonome)

- **FAIT.** Le champ `inn` ne liste que les mots qui diffèrent d'un état de base (`h3_goldens.py`, fonction `main`, comparaison à `base`). Cet état de base n'est pas nul : héros posé par le vrai code, entités du banc.
  - Les mots égaux à la base sont absents : sol du héros `0x80127E74`, lien de portage `0x80127D5C`, variable `0x80127164`, 8 mots d'entités, etc.
  - Rejoué par mes modèles depuis une mémoire vide : 61 vecteurs sur 480 divergent, 2 104 lectures de mots absents (`my_goldcheck.py`, `goldens_h3bin_replay.txt`).
  - La docstring de `h3_goldens.py` dit « diffèrent de la base nulle » : faux, la base est l'état du monde du banc.
- **Remède.** `goldens_critic.json`, généré par `my_goldens.py` : 300 vecteurs, 25 par fonction. Chaque vecteur donne tous les mots lus par le vrai code (hors lecture d'instructions), tous les mots écrits, le retour, le journal, les retours simulés de naissance et d'effet. Rejeu par mes modèles depuis une mémoire vide : 0 écart, 0 lecture hors liste.

### R4. Zone d'explosion : formulation

Règle exacte : recouvrement strict de la boîte de l'image courante (centrée sur la bombe, 48×32 au maximum) avec la boîte de la cible. Pour une cible de 24×16 : touchée si \|dx\| < 36 et \|dy\| < 24. Hauteur non mesurée : la racine a la gravité et retombe (`explosion_zone_z_INVALID_target_falls.txt` est conservé comme contre-exemple de méthode).

### R5. Herbe : CONFIRMÉ avec ajouts

- **FAIT [L]** `0x8003A7B0` :
  - Sort si `E+0x1D4 == 0`.
  - Quatre coins de la boîte `+0x1FC`/`+0x200`/`+0x204` (taille `+0x214`…`+0x21C`) ; colonne par la table `0x80127168`, ligne par `>> 20`, bornes 0 à 51 et 0 à 59.
  - La case coupée a le bit `0x0002` de son premier demi-mot ; fenêtre en Z `(hauteur << 20) + 0x80000` dans `[+0x204, +0x204 + +0x21C]`.
  - Elle efface le bit, écrit `0xFFFF` en `+6`, appelle `CreateEffectEntity(0, octet +9 de l'en-tête de carte, 0, x, y, z)`, **puis `0x80032C7C(0xFF, x, y, z)` (le butin)**, puis joue le son `0x1F`.
- Ajouts au rapport :
  - (a) L'identifiant d'effet est l'octet `+9` de l'en-tête de carte (le JSON le nomme `BalanceLevel`, valeur 2 sur les 483 cartes). « Effet 2 » est donc vrai partout mais n'est pas une constante du code.
  - (b) Le butin de l'herbe coupée par la bombe est appelé comme pour l'épée : il dépend d'E20.e0b2 et e0c.
  - (c) [M] Sur une grille 7×7 d'herbe : 7 cases coupées aux ticks 22, 25 et 29 (`grass.txt`).

### R6. Dégâts selon le niveau d'équilibrage

- **FAIT [L][M].** La fiche est choisie par `0x80044550` selon le niveau de la carte (`[0x80143DF0]`, écrit par `0x8002C3A0` depuis l'octet `+0xB` de l'en-tête de carte, valeurs 0 à 11, 344 cartes à 0).
- Pour la bombe, une seule fiche change de résultat entre le niveau 0 et les niveaux 8 à 11 : la 497, de 5 à 3 PV. Les 45 morts sont les mêmes à tous les niveaux (`damage_levels.txt`).

### R7. Sites `0x95` : CONFIRMÉ avec correction de décompte

Mon recensement donne 28 sites `0x95` d'indice 3 dans 28 cartes :
- masque `0x0808` dans 27 cartes : 1 à 10, 12 à 17, 19, 20, 82, 84, 331, 335, 407, 409, 411, 439, 479 ;
- masque `0x0809` en 226.

Le rapport en nomme 23 et écrit « 25 cartes ». Il manque 82, 84, 335, 409, 411, hors chaîne ; 409 et 411 reprennent les programmes de 82 et 84. Sans conséquence pour la chaîne : la 10 est seule.

## 4. Ce que le rapport avait manqué

**A1. Le gel du monde `& 0x48`. FAIT [L].**
- `0x8003B388` : si `[0x800DC4B8] & 0x48` est non nul, seule la passe des listes `0x800384F4` tourne (puis le préambule d'affichage). Ni `MovePlayer`, ni les événements des entités, ni la physique, ni l'animation, ni les coups.
- La fusée est donc du temps de jeu : elle s'arrête menu ouvert (bit `0x08`) et reprend ensuite.
- Les verrous `0x04` (opcode `0x10`), `0x10` (boîte de message qui garde le contrôle) et `0x20` (séquences forcées) (noms : `GameState.cs:77-98`, [DLL]) ne gèlent pas le monde. Une bombe lancée continue sa fusée ; une bombe portée est annulée (`& 0x34`).
- Mon harnais appelle les passes sans ce garde-fou : le gel est établi par la lecture, non rejoué. La DLL porte déjà le garde (`GameState.cs:97`, `GameplayBlockedMask`).

**A2. Couvercles : `(E, 19)`. FAIT [L][D].**
- `0x8007F690` joue le son `0xE6` puis `DestroyEntity(E, -1)` (effet de bris de l'en-tête : 11 pour 241 à 243).
- Après le `0x87 [128,3]` vrai, le programme D de la 362 fait `Flag on 6` puis `Deactivate entity` (opcode `0x19`, la DLL pose `Status = Deactivated`). Seul `(E, 19)` détruit alors le couvercle.
- 6 des 29 programmes de couvercles utilisent `0x19`, dont la 362. Les 23 autres n'ont pas `0x19` et passent par `0x2B`, `0x05`, etc. (INFÉRENCE : le retrait se fait par un autre programme, non lu).
- `(E, 19)` est absent du registre (`CreateForProduction`) et de tout le plan.
- **Pour A362 :** le couvercle réagit à l'explosion sans D30 ni E12 (INFÉRENCE : programme D de la 362 et fiche 243, réponse `0x10` au type 4 ; non rejoué, le banc n'exécute pas les programmes de carte ; le plan dit déjà « probable »). Pour qu'il disparaisse, il faut `(E, 19)`.
- E12 ne retire que la bombe d'Olen (animation 1 finie, drapeau `0x40`, `E+0xAC == 1`).

**A3. Classes de coup. FAIT [D][L].**
- La bombe est ClassB (`0x08`, comme le héros `0x03118C`) avec HitsClassA et HitsClassB. Elle blesse monstres (ClassA) et héros (ClassB).
- Pour qu'une attaque la touche, l'attaquant doit porter HitsClassB (`0x02`) et l'attribut doit être 4, 6 ou 10 (réponses `0x10` de sa fiche).
- Parmi les sprites globaux, seuls 2 (bombe), 238, 239, 251 et 252 portent HitsClassB. Aucun projectile, sort ni arme du héros (sprites 3 à 24) ne l'a.
- Les explosions en chaîne viennent donc d'autres bombes ou d'attaques de monstres de cartes (non recensées ici).
- Sur les 30 cartes de la chaîne, les seules entités touchables par l'explosion sont des doubles du héros (sprite global 0, cartes 164, 165, 172, 179) ; la 10 n'en a aucune (`my_chain_targets.py`).

**A4. Bombe en main touchée par une explosion. FAIT [M].**
- Rond à t262, héros dans l'explosion de la 1re bombe (état 49 depuis t261) : la seconde bombe naît en main dans la boîte active de la première.
- Deux ticks après sa naissance (t264), D30 la passe en statut 3 (C62 ne tourne plus) ; son 46 à t265.
- E12 tourne bombe tenue : le lien `H+0x2C` et l'objet « en cours » restent jusqu'à la fin de l'animation, puis `DestroyEntity(E, -1)` efface le porteur.
- Les PV du héros ne bougent pas dans cette fenêtre (5 sur 5).
- À couvrir par un test : `scen_hurt_circle.py`, sortie `hurt_circle.txt`.

**A5. RNG des étincelles. FAIT [V].**
- Chaque étincelle de C62, et chaque effet 14 du pot, consomme deux tirages seulement si `CreateEffectEntity` rend un effet. Les 7 étincelles font 14 tirages quand le réservoir d'effets n'est pas plein.
- Le port doit décider quand « un effet est obtenu », sous peine de désaligner le générateur partagé avec le butin et l'IA.

**A6. Le pot, complet. FAIT [V][M][D].**
- Au repos le pot est en animation 0 (dormant) : C74 ne fait rien (`anim − 1 >= 5`).
- Les graines le réveillent : animation 1 (22 ticks, son 31, un effet 14 tous les 4 ticks), que la fiche enchaîne sur l'animation 2 (repos).
- Héros posé dessus (`H+0x12C == pot`) : `ForceZ = −0x8000` et animation 3 (28 ticks, enchaînée par la fiche sur l'animation 4, maintien).
- En animation 4, si le héros est parti avec `ForceZ > 0` : `H.ForceZ = 0x98000` (9,5 px par tick) et animation 5 (18 ticks, puis 2). Sinon animation 2.
- A20 copie le Z de naissance en `+0x274`. Le repos des animations 2 et 5 rappelle le Z vers `+0x274` par `(repos − Z) >> 1`.

**A7. Dépendance non établie pour h3c et h3d. INCONNU.**
- E21 lit `E+0x12C` de la graine (l'appui). Le binaire le renseigne pour toute entité dans la passe de physique.
- La DLL calcule `RidingEntity` pour les entités à contrôleur (`EntitySupport.EvaluateEntitySupport`). Je n'ai pas établi qu'elle le fait pour une entité née par l'interface. À vérifier au T0 de h3c.

**A8. Décompilation. FAIT [DEC].** `GameEngine.cs:1570` (garde de menu) lit `g_warpLockTimer` ; c'est `[0x80127164]`. La DLL le déclare « sans équivalent » (`InventoryDirector.cs:419`) alors que h0a le fournit.

## 5. Jugement sur le découpage proposé (§11)

**Ce qui tient.**
- Les gestionnaires de la bombe ne dépendent que du registre, de la passe de coups et de la destruction (h3a).
- Ce qui exige les mains (départ, C62, annulation) vient après g1a et g2a (h3b).
- Les graines suivent (h3c), le pot est optionnel (h3d), les arcs ferment (h3e).
- Les prérequis nommés (ni g1b, ni g2b, ni g3) sont exacts.
- Les vecteurs des 12 fonctions que h3.0 doit rejouer sont valides, à condition de remplacer le fichier d'or (R3).

**Ajustements demandés.**
1. **A362 d'abord, sans dépendre de h3a.** L'arc ne demande rien de D30 (la bombe d'Olen explose par son script C, animation 1) et rien de nouveau pour la réaction du couvercle. Le lancer avant h3a est le moyen le moins cher de savoir ce qui manque (probablement `(E, 19)` et la suppression de la bombe). Corriger la ligne 9b du plan, qui dit « relancé après E20.h3 ».
2. **`(E, 19)` dans h3a** : le couvercle est le débouché de la bombe, 6 programmes, un gestionnaire de 12 instructions, aucune autre dépendance qu'`E14.a1b`. Sinon dans A362, qui cesse alors d'être un arc sans code. Je recommande h3a.
3. **h0a : câbler et tester les 63 états** (R1). Les appels existent dans la DLL. Il faut des tests sur les groupes d'attaque (Rond interrompt un coup d'épée), après un lancer, en blessure, et le refus par `H+0x2C != 0` dans les six états de portage.
4. **h3b : tests supplémentaires nommés.**
   - Le 4e départ est refusé jusqu'à T+296 de la 1re bombe : une bombe détruite compte tant que sa case n'est pas recyclée, la bombe d'Olen comptant aussi.
   - A4 (explosion en main).
   - Le gel `& 0x48` de la fusée.
   - La politique des étincelles (A5).
   - L'écrasement du lien par `DestroyEntity` sans test d'identité (`0x8003A7A4`).
5. **h3c / h3d : T0 sur l'appui `+0x12C`** des entités nées par l'interface (A7). Par ailleurs h3c sans le pot reste invisible : confirmer le défaut (C) de Q-H3-3.
6. **D32 (racine) dans h3a** : laisser (carte 8 seule, hors chaîne). Accord avec Q-H3-7.

**Questions de l'auteur.**
- Q-H3-1 (défaut oui) : sans objection ; ajouter que Rond interrompt un coup d'épée.
- Q-H3-2 : A (livrer ainsi) ; le coût de 5 PV dépend de l'armure équipée.
- Q-H3-3 : défaut C.
- Q-H3-4 : fidèle (refus) ; deux sites de garde, Start `0x8002BC90` et secours `0x8002BB94`.
- Q-H3-5 : rien à décider, le binaire gagne (D1 vérifiée).
- Q-H3-6 : « oui » pour h3a, mais pour une autre raison (A362 ne l'exige pas).
- Q-H3-7 : laisser.
- Q-H3-8 : toujours INCONNU.

## 6. Reproduire

Tout depuis `C/run/`, avec `python -B`. Les scripts lisent `ALUN_CD.EXE` au chemin absolu de `lib.py`. L'interpréteur et le monde sont la copie de `docs/plan-e20-annexe/e20-h4/check/o/` (`lib.py`, `emu.py`, `machine.py`, `world.py`, `bank.py`, `oracle.py`, `spellworld.py`). Mon code est `my*.py` et `scen_*.py`.

| But | Commande |
|---|---|
| Vrai code contre mes modèles (12 fonctions) | `python -B my_diff.py 6000 424242` ; une fonction : ajouter son nom |
| Pot A20 et C74 | `python -B my_diff_pot.py 10000` |
| Sensibilité : modèles mutés | `python -B my_mut.py` |
| Sensibilité : vrai code muté | `python -B my_wmut.py all 120 5`, puis `python -B my_wmut_retest.py 700` |
| Goldens autonomes | `python -B my_goldens.py 25` puis `python -B my_goldcheck2.py` ; goldens de h3bin : `python -B my_goldcheck.py` |
| Chronologie bombe | `python -B scen_bomb1.py` ; limite de 3 : `scen_limit3.py` |
| Zone, dégâts, herbe | `scen_zone.py`, `my_damage.py`, `my_damage_levels.py`, `scen_grass.py`, `scen_herohit.py` |
| Annulations, refus, états | `scen_cancels.py <lock20\|lock10\|lock04\|death\|exitcell> 0x23 60`, `scen_refusals.py`, `scen_allstates.py`, `scen_states.py`, `scen_airstart.py`, `scen_hurt_circle.py` |
| Graines, pot, eau, lancers | `scen_beans.py`, `scen_water.py`, `scen_throwdirs.py`, `scen_relift.py` |
| Corpus des cartes | `my_maps.py`, `my_maps2.py <carte> <enreg…>`, `my_maps3.py <carte> <table> <i>`, `my_chain_targets.py`, `my_dscan2.py 3 45` |
| Listings et références croisées | `mydis.py 0x… [n]`, `myxref.py 0x…`, `my_scan68.py`, `my_loaddelay.py` |

**Limites.** Je n'ai pas rejoué la blessure en portant, les murs (rebond, atterrissage au pied d'un mur) ni le gel `& 0x48` au niveau du répartiteur. Je ne tranche ni la cadence en secondes ni les sons des effets.

**Fichiers** (chemins sous `C`) : `check.md`, `README.md`, `run/` (scripts), `lst/` (listings), `results/` (sorties dont `goldens_critic.json`, `my_diff_final_6000.txt`, `states_circle_all.txt`, `bomb_timeline.txt`, `bomb_limit3.txt`).