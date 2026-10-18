# Critique adverse de la découverte E20.h4c1 (terre 1, objet 0x2B)

Lecture seule, HEAD `023b33ce`. Légende : FAIT (vu dans le code, les données ou le binaire), INFÉRENCE (déduit), INCONNU. Verdicts : CONFIRMÉ, RÉFUTÉ, NON ÉTABLI.

- Le binaire `ALUN_CD.EXE` (France) a bien le SHA-1 `a705f8da…`.
- `git diff --stat f81304b HEAD -- Alundra Alundra.Tests` est vide : la DLL lue est celle de `state-f81304b`.
- Je n'ai rien écrit hors de mon dossier de sortie. La DLL n'a été ni construite ni exécutée, donc tout fait « DLL » ci-dessous est une lecture de code.
- Je n'ai pas écrit `report.md` ni `check.md` (règle des sous-agents). Les preuves brutes sont dans `evidence/`, ce rapport est le message.

`OUT` désigne `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c1-critic`.

## 0. Verdict : REVISE (2 P1, 6 P2, remarques P3)

Le comportement du binaire décrit par le rapport est exact. Le découpage et l'acceptation proposés contiennent deux défauts bloquants et plusieurs erreurs de détail.

1. **Binaire : CONFIRMÉ.**
   - J'ai redésassemblé moi-même l'appelant, le pilote, la table, la garde, l'annulation et E3.
   - Un modèle écrit par moi rejoue les 689 vecteurs pilote et les 224 vecteurs E3 de l'autre agent avec 0 écart.
2. **Écart C1 (harnais h4 sans index de case) : CONFIRMÉ.** 14 rochers sur 32 sont décalés d'un tick, et la cible 6 de `targets_inv0` passe de 3 à 4 touches.
3. **P1 – La section 1.8, C3, Q-H4C1-3, la sonde P2 et le test (w7) sont RÉFUTÉS.**
   - Une case recyclée n'est jamais « périmée ». La passe des cases `0x80038634` recopie le gabarit vierge sur toute case en statut 4, et l'initialisation de carte `0x8003B24C` fait de même.
   - La rangée de départ `+0x98` vaut donc toujours 0 dans le binaire, comme dans la DLL. Il n'y a aucun écart à documenter.
   - La question à l'auteur sur « 64 entiers de mémoire par case dans le moteur » doit être retirée : elle demanderait de porter un comportement que le binaire n'a pas.
4. **P1 – Le plan ignore Q-K2, D-E20-38 et D-F4-9.**
   - Contre un mur ou une marche, la DLL lève le drapeau « bloqué » un tick trop tôt dans environ un cas sur trois (accepté par l'auteur).
   - L'arrêt « un tick de (w1) diffère de la clé (aucune tolérance) » est donc incohérent pour (w3), et pour les clés de relief à ajouter.
5. **P2 – Les preuves de retrait sont fausses** pour l'inscription d'E3, pour le pilote et pour le corps d'E3 (§5).
6. **P2 – La sonde P3 est fausse.** La boîte 23×23×15 dure de l'animation tick 10 à 13, pas 10–11.
7. **P2 – Banc de (w5) et clés (w6) à corriger.**
   - (w5) sur `HitArena` utilise le relief réel de la carte 14, alors que l'oracle est sur champ plat.
   - Les clés `interrupt_*` et `vs_beannoid_84_104` contiennent des réactions du héros que la DLL ne produira pas.
8. **P2 – (p1) est ambigu** sur les tomes 43 et 44.
9. **P2 – (p3) n'a pas d'oracle**, et D-E20-37 interdit qu'il en ait un tick par tick.
10. **P2 – Couverture de relief absente** (marche, rocher collé à un mur). J'ai généré les clés candidates.
11. **Prérequis : CONFIRMÉS** (f5, f4, h0a, h4a, f6a, fin de la restructuration).
12. **Hors périmètre, même cause que le point 3 :** O-E0A5-2 (`+0x1C` périmé) et O-E0A5-4 reposent sur la même fausse prémisse. Je les signale à l'auteur pour fermeture.

## 1. Ce que j'ai refait

- **Outils à moi :** `OUT/tools/mydis.py` (capstone, indépendant des outils de h4) et `OUT/tools/my_driver_model.py` (modèle du pilote, de l'appelant et d'E3, écrit depuis mes listings).
- **Listings :** `OUT/lst_8003634C.txt`, `lst_80035320.txt`, `lst_800351B8.txt`, `lst_800350C0.txt`, `lst_E3.txt`, `lst_init.txt` (0x80039D04), `lst_spawnwarp.txt`.
- **Machine de l'autre agent** (MIPS réel), rejouée sur des copies dans `OUT/work/` avec mes propres scripts : `crit_template.py`, `crit_relief.py`, `crit_pointblank.py`.
- **Données lues :** `map_alundra.json` (enregistrement 16), `BALANCE.BIN.json`, `map_14.json`, `map_388.json`, `sprite-records.json`, le prefab de la terre 1.
- **Contrôles croisés :** mon modèle de vol analytique, les tables DLL comparées à l'exécutable, le recensement des sprites à E=3.
- **Constat :** ma clé plate régénérée est identique à `earth1_flat_east.json` sur les 140 lignes.

## 2. Faits décisifs un par un

| # | Fait du rapport | Verdict | Preuve |
|---|---|---|---|
| F1 | Appelant `0x8003634C` : `idx = item − 0x1F`, saut `0x800235F4[12] = 0x800363D4` pour `0x2B`, compteur +1 après le pilote (saturé à `0x7FFFFFFF`), libération si retour non nul | CONFIRMÉ | `lst_8003634C.txt` ; table lue (`0x2B` donne `0x800363D4`) |
| F2 | Pilote `0x80035320` : phase 0 (garde, boucle de la table, `ForceZ = s16 << 8` seulement si la naissance rend une entité, phase 1 à la sentinelle) ; phase 1 (garde, `0x34` à compteur ≥ 100) ; phase 2 sans garde | CONFIRMÉ | `lst_80035320.txt` ; modèle propre 689/689 (`evidence/vectors_replay_my_model.txt`) |
| F3 | Table `0x8002343C` : 32 entrées de 6 octets et sentinelle `0xFFFF`, ticks 20 à 95, vitesse Z 1024/1280, deltas `(3,−6),(0,6),(3,2),(8,14),(3,10),(16,22),(3,18),(24,30)` ×2 | CONFIRMÉ | lecture directe de l'exécutable |
| F4 | Garde `0x800351B8` (pente 4 ou animation hors `0x32`/`0x33` : annulation, retour 1) ; annulation de `0x2B` = `0x800351A0` (neutre, libère `[0x80127164]`) | CONFIRMÉ | `lst_800351B8.txt`, `lst_800350C0.txt` ; table `0x800233EC` |
| F5 | E3 `0x8007EDA0` : `+0x88 == 1 && +0xAC == 1` donne `DestroyEntity(self, −1)`, sinon `+0x88 = 1` et `Flags = (Flags \| 0x40) & ~0x30` | CONFIRMÉ | `lst_E3.txt` ; modèle propre 224/224 |
| F6 | Calendrier : Rond au tick 1, pilote au tick 2 (compteur 0), naissances 22…97 (une fois tous les 5 ticks, deux rochers), phase 1 au tick 97, `0x34` au 102, héros libre au 128, objet libéré au 129 | CONFIRMÉ | clés ; `trace_summaries.txt` de h4 |
| F7 | Sprite 16 : flags `0x132114`, programmes A0 C0 D0 E3, `BreakEffect` 0, `Contents` 0, corps (−10,−7,0) 21×15×16 ; animations 0 (12 images, 24 ticks, vitesse 512, chaîne vers 2), 1 (6 images, vitesse 0, son 46, Hold), 2 (boucle) ; fiche 16 (niveau 255, PV 0, type 8, attribut 8, puissance 32) | CONFIRMÉ | `map_alundra.json` rec. 16 ; `BALANCE.BIN.json` ; `sprite-records.json` |
| F8 | Boîte 23×23×15 : animation 1, rangée 0 (« down »), image 6 seulement | CONFIRMÉ pour l'emplacement, RÉFUTÉ pour la durée (voir R3) | `evidence/box_ticks.txt` |
| F9 | Vol : atterrissage au 16ᵉ pas (groupe 1) ou 20ᵉ (groupe 2), sommets 14 et 22,5 px, pas XY = `OffsetX/Y[dir] × 512`, statut 3 à L+1, `+0xAC = 1` à L+13, statut 4 à L+14, case libre au tick suivant | CONFIRMÉ | modèle analytique propre : 28 positions finales sur 32 exactes, les 4 autres sont le bord ouest x=10 ; tables DLL `OffsetXList`, `OffsetYList`, `AnimationDirectionTable` égales à l'exécutable |
| F10 | C1 : le harnais h4 n'écrit pas l'index de case `+0` | CONFIRMÉ | voir R0 ci-dessous |
| F11 | Section 1.8 : « la rangée de départ dépend de l'ancien `+0x98` de la case » | **RÉFUTÉ** | voir R1 |
| F12 | « 28 des 32 directions » non cardinales | RÉFUTÉ | 24 directions pour les 8 bases multiples de 4 (`DirectionByButtons`), 16 pour les 4 cardinales |
| F13 | Prérequis manquants au tableau : f5, f4, h0a, fin de restructuration | CONFIRMÉ | au HEAD : `ScriptedMotion.cs:175` sans `HasLogicalZ` ; ni `NativeSpriteTypeFilterForTests`, ni `ItemStartForTests` |
| F14 | Inscription `{16}` en un seul `Register` ; le compte passe de 7 à 8 ; deux épingles | CONFIRMÉ | `NativeSpriteRegistry.cs:36-43,54-65` ; `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`) ; `AlundraWaveSwordLifeTests.cs:183` (`3 + 3 + 1`) ; aucun autre test ne fige le compte |
| F15 | E3 sert aussi 23, 150-153 et 98 enregistrements de 75 cartes | CONFIRMÉ | recensement : sprites globaux à E=3 = {16, 23, 150, 151, 152, 153} ; 104 enregistrements en 76 fichiers moins `map_alundra` donnent 98 en 75 cartes |
| F16 | 4 placements du sprite 16 sur la carte 388 (Hauteur 20, programmes 0) | CONFIRMÉ ; rôle dans l'histoire NON ÉTABLI | entités 18 à 21 de `map_388.json` ; la 388 n'est atteinte que par le portail de la carte 35 en (44,26) vers (18,25) |
| F17 | La terre 1 n'utilise aucune brique de h4b | CONFIRMÉ | |
| F18 | Eau profonde : `Slope_18c = 4` à L, statut 4 à L+1 avec l'effet attaché 6, 14 morts et 18 sons | CONFIRMÉ | clé `earth1_deepwater_cols7_9` |
| F19 | Touches : 3 étincelles, 9 tirages, `+0x1D0 = 25`, le rocher traverse ; les rochers ne sont pas modifiés par la cible | CONFIRMÉ | 8 clés `vs_beannoid` : 0 rocher différent de la clé plate |
| F20 | Bord ouest : « E3 vient par `ForceAdjusted`, un tick avant l'atterrissage normal » | RÉFUTÉ dans la lettre | E3 vient 3 ou 4 ticks avant (94, 103, 109 au lieu de 97, 107, 112) ; le décompte « 3 rochers sur 32 » est confirmé |
| F21 | Interruption par `0x31` : objet libéré au tick T exactement | CONFIRMÉ | clé `interrupt_60` : objet 0 au tick 60 |
| F22 | Citations DLL (`WorldProxy.cs:3488`, `:3549`, `:3566`, `:3572-3574`, `ScriptedMotion.cs:171-176`, `EventProgramRunner.cs:264-324`, `PlayerManager.cs:932`, `:1507-1509`, `:740-742`, etc.) | CONFIRMÉ | relues ; chemins courts du rapport, vrais dossiers `Entities/Spawning/`, `Maps/Loop/` |

### R0. L'écart C1 en détail (CONFIRMÉ)

- **Mécanisme (FAIT).**
  - `InitializeEntity` relève le plus haut emplacement : `[case+0]` contre `[0x80134360]` (`0x80039D24-0x80039D3C`).
  - Le monde réel écrit l'index à `0x8003B2BC`, dans la boucle des 64 cases de `0x8003B24C`. La passe `0x80038634` le réécrit au recyclage.
  - `world.py:106` du harnais de h4 met à zéro les 64 cases, et `spellworld.make` n'écrit aucun index. `pworld.setup` écrit `m.poke(ENT0 + STRIDE*i, i)`.
- **Mesure (FAIT).** Mon rejeu de `c1_cast.py` sans et avec `--slotidx` (`evidence/index_effect_h4_harness.txt`) donne 14 rochers sur 32 avec une impact et une mort un tick plus tard : les 14 premières naissances en cases de première utilisation.
- **Touches (FAIT).** Pour `targets_inv0`, les premiers ticks de touche passent de 25, 33, 36, 28, 40, 32 à 24, 32, 35, 27, 39, 31, et la cible (140,140) passe de 3 à 4 touches `[31, 56, 81, 106]`.

## 3. Défauts du rapport et corrections

### R1 (P1) – La rangée de départ ne dépend d'aucune case périmée

- **Prémisse du rapport (§1.8, C3, Q-H4C1-3).** `InitializeEntity` n'écrit pas `+0x98`, donc l'ancienne valeur de la case serait lue.
- **Ce que dit le binaire (FAIT).**
  - Passe des cases `0x80038634` : pour chaque case en statut 4, elle recopie les `0x294` octets du gabarit `0x80134368` puis réécrit l'index (`0x80038654-0x800386A4`). Elle est appelée à `0x8003B3A0`, en tête de la porte du tick `0x8003B388`, avant la passe des événements où tourne `MovePlayer`.
  - Réinitialisation de carte `0x8003B24C` : même recopie pour les 64 cases (`0x8003B284-0x8003B2BC`).
  - Recherche d'une case libre `0x80039AD0` : première case de statut 0.
  - Gabarit : zone BSS mise à zéro au démarrage (`0x8008B538-0x8008B554`, de `0x800CA0E8` à `0x801F7F20`). Mon balayage des références (`tools/scan_tmpl.py`) ne trouve qu'une écriture, `0x8003B21C` (`+0x48 = −1`). Donc `+0x98` et `+0x1C` du gabarit valent 0.
  - La DLL le sait déjà : `WorldProxy.cs:3255` (doc de `RecycleDestroyedEntities`) cite le gabarit `0x80134368`.
- **Expérience sur la machine (FAIT, `evidence/template_recycle_machine.txt`).**
  - Juste après la vraie passe des cases, 10 cases libérées valent toutes `row = 0`, `cnt = 0`, `status = 0`, avec leur index.
  - Les anciens occupants valaient `row` 3 (case 3) et 2 (case 8), `cnt` 30.
  - Les deux dernières lignes de ce fichier (« freed cases seen: 0 », « idx words ») sont des restes d'un contrôle abandonné ; seul le premier bloc compte.
- **Données (FAIT, `evidence/rows_vs_template.txt`).**
  - Les 224 rochers de 7 clés ont exactement la rangée `table[0][col]` (`col = ((dir+2) & 0x1C) >> 2`).
  - Sur 128 rochers nés en case recyclée, un seul départage « périmé » et « vierge » : il vaut la valeur vierge.
- **Côté DLL (FAIT).** `AnimationDirection` part de 0 (`EntityScriptProxy.cs:162`), est assigné à `FrameSyncPasses.cs:152`, par la même table (`:505-507`). La DLL est identique au binaire.
- **Conséquences.**
  - Supprimer : la sonde P2, le test (w7) « rangée de départ », la question Q-H4C1-3 et l'option « mémoire par case ».
  - Remplacer P2 par un test d'égalité : la rangée de chaque rocher de (w1) vaut `table[0][col]`. C'est aussi la clôture d'O-E0A5-4 pour cette tranche : la direction de création non cardinale se traite sans écart.

### R2 (P1) – L'écart documenté du drapeau « bloqué » contre un mur n'est pas pris en compte

- **FAIT.**
  - D-E20-38 (`plan:171`) accepte l'écart ; Q-K2 (`:5020`) le décrit : 1 tick d'avance dans environ un cas sur trois.
  - D-E0K-6 (`:5012`) : le mur et la hauteur passent par le mover du moteur.
  - D-F4-9 (`:6022`) fixe la règle « arrivée + 1 » : égale à l'oracle pour 8 départs sur 12, en avance d'un tick pour les 4 autres.
  - Le bord de carte, lui, est exact (D-E0K-7 ; `AlundraMapEdgeWorldTests` B1).
- **INFÉRENCE.** Les rochers (`NoObstacleSlide`, même mover) sont concernés comme les flèches.
- **Défaut.** Le rapport n'en parle pas et exige « aucune tolérance » (arrêts de la section 5). Le rocher de `earth1_wall_col9` (E3 au tick 86 dans la clé) peut sortir à 85 dans la DLL sans que ce soit un défaut.
- **Correction.** (w3) mur, ainsi que les clés de marche et de collage au mur (§6), dérivent leur attendu comme f4/f5 : tick d'arrivée au mur + 1, puis chronologie relative au tick d'E3 observé. L'« aucune tolérance » ne vaut que pour le vol libre (plat, `x200`, orientations, eau profonde, touches, interruptions).

### R3 (P2) – Durée de la boîte 23×23×15

- **FAIT** (`evidence/box_ticks.txt`) : pour les 16 rochers de rangée 0, la boîte est petite aux ticks 10, 11, 12 et 13 après le premier tick de statut 3.
  - La Hold garde l'image 6 jusqu'à la mort ; le tick 13 est le tick de mort.
  - Les 16 rochers de rangées 1 à 3 gardent 42×28×11.
- **Correction de P3 :** « présente en rangée 0 aux ticks 10 à 13 de l'animation 1, absente ailleurs ».
- **INFÉRENCE :** seuls trois ticks comptent pour la passe de coups, car l'entité de statut 4 sort de la liste.
- **INCONNU :** la DLL garde-t-elle la boîte de la dernière image en Hold ? C'est ce que la sonde doit mesurer.

### R4 (P2) – Les preuves de retrait ne suivent pas les définitions des tests

- **Retirer l'appel d'inscription.**
  - Le rapport annonce « exactement (p3) et les épingles ». C'est faux.
  - (p1) exige « la clé de (w1) tient par le chemin de production ». Sans E3, aucun rocher n'entre en animation 1, ne joue le son 46 et ne meurt : (p1) est rouge aussi.
  - Correction : « (p1), (p3), les deux épingles et le test (p4) proposé en §6 ».
- **Retirer l'entrée du pilote.**
  - Le rapport annonce « (p1) et (p2) ».
  - (p2) (table de cases pleine : aucune entité, aucune exception) reste vert avec un bouchon : avec ou sans pilote, rien ne naît.
  - Correction : (p2) doit observer que le pilote a tenté la naissance (journal de refus ou compteur de la couture). Sinon la preuve devient « (p1) seulement ».
- **Retirer le corps d'E3.** La liste « (e1), (w1), (w2), (w5), (w6), (p1) » oublie (w3), (w4) et (p4) : dans (w3) et (w4), les rochers qui atterrissent normalement exigent le son 46 et la mort.

### R5 (P2) – Bancs et colonnes comparées

- **(w5) sur `HitArena`.**
  - C'est le monde réel de la carte 14 (`AlundraHitArena.cs:26`).
  - D'après `map_14.json`, les rangées 0 à 7 sont plates. À partir de la rangée 8 (y ≥ 128), les colonnes 7 à 10 portent des murs `0x41` et des hauteurs 9/18, et la rangée 12 est murée.
  - Les rochers qui retombent à l'est ou au sud-est (x ≥ 168, y ≥ 124) heurtent ce relief : leur chronologie ne peut pas suivre l'oracle sur champ plat.
  - Correction : (w5) sur le banc de (w1) (`ArcRun` + champ plat + `Map(128, 4096)` + un Beannoïde posé, natifs coupés, filtre `{16}`), comme `AlundraMapEdgeWorldTests.cs:43-50`. Sinon, limiter la comparaison aux touches de rochers qui n'ont survolé que des cases plates, et le dire.
- **Clés `interrupt_*` (FAIT).**
  - Après `0x31` en tête du tick T, le héros réel recule de 33 px en x (`px` 6553600 vers 8722432, animations 49 puis 28) et joue le son 23.
  - La DLL n'a pas la blessure (E20.c2b).
  - (w6) ne compare que l'objet, les naissances et les rochers, et exclut les champs du héros après T et `snd(23)`. À écrire dans le plan.
- **Clé `vs_beannoid_84_104` (FAIT).**
  - Dès le tick 129, le Beannoïde à 16 px du héros le blesse : animation 49 puis 28, `snd(23)`, déplacement.
  - La clé doit être coupée au tick 128, ou la cible déplacée à au moins 24 px du héros.
  - Les touches 22, 47, 72 et 97 restent valables.

### R6 (P2) – (p1) et les tomes 43 et 44

- « objets 43 et 44 possédés : le niveau 2 passe d'abord » contredit « le type 16 naît au tick 22 ».
- Cause : `0x8004E0F8` rend l'ancien `stats+0xC` puis le remplace par le meilleur objet de la case d'inventaire, donc 44 une fois promu (`report.md` §2 de h4 ; (t5) de h4a).
- Correction : (p1) ne possède que l'objet 43 ; accolé, (t5) montre la promotion. Si les deux sont possédés, le test doit appuyer au tick 1 avant toute promotion.

### R7 (P2) – (p3) n'a pas d'oracle

- D-E20-37 (`plan:169`) : les entités posées par la carte gardent l'ancienne règle de chute. Aucun tick exact n'existe.
- (p3) doit être qualitatif : E3 a tourné (statut 3 puis 4), le son 46 est joué, aucune exception. Le plan doit le dire.
- Ces 4 rochers n'étant pas marqués, le test est aussi un témoin que `HasLogicalZ` ne les touche pas.

### R8 (P3) – Mutants

- Deux mutants du rapport ne portent pas sur du code de cette tranche : « clé de vitesse revenue à `CurrentAnimationId` » (f5) et « compteur incrémenté avant le pilote » (h0a). Ce sont des mutants d'intégration, pas du code de h4c1.
- Il en reste 16 propres. Pour tenir « au moins 18 » sans eux, ajouter par exemple :
  - E3 inscrit pour `{16, 23}` ;
  - une garde de phase 1 retirée (tuée par les interruptions 98 et 101) ;
  - le test de la table lisant des mots de 32 bits au lieu de `u16/s16/s16` ;
  - le retour de naissance non testé (null déréférencé).
- Mutant « E3 avec `DestroyEntity(e, 0)` » : équivalent pour le sprite 16 (`BreakEffect` 0), il ne se tue que par le vecteur (e1), pas par le monde. À ne pas compter comme tué par une clé.

### R9 (P3) – Formulations

- Le « But » dit « deux par tick de 22 à 97 ». C'est deux rochers tous les 5 ticks (16 ticks de naissance).
- « 28 des 32 directions » doit devenir 24 (8 bases) ou 16 (4 cardinales).
- Bord ouest : « un tick avant » doit devenir « 3 ou 4 ticks avant ».
- P5 (« le bord ouest ») n'a pas d'attendu écrit.
- (d1) : les sorties `counter` et `inuse` des vecteurs sont celles de l'appelant (h0a), pas du pilote. Le test unitaire du pilote compare retour, phase, animation, naissances et `ForceZ` ; le compteur et la libération se testent par le chemin du tick.
- La table se lit en `u16, s16, s16`, pas en mots (le modèle `AlundraHeroRunTablesTests.cs:42` lit des mots).

### R10 – O-E0A5-2 et O-E0A5-4 (hors périmètre, à porter à l'auteur)

- O-E0A5-2 (`+0x1C` périmé d'une case libre, `plan:4487`, assigné à P-F4-3) et O-E0A5-4 (`plan:4489`) reposent sur la même prémisse que R1.
- Preuve : `+0x1C` vaut 0 juste après la passe des cases (même expérience), et `charged7_east_wall` de f4 montrait déjà `cnt1c = 1` à S.
- Proposition : les fermer comme sans objet, en citant `0x80038634-0x800386A4`.

## 4. Clés d'oracle et bancs

| Clé | Existe | Fidèle à son banc | Remarque |
|---|---|---|---|
| `earth1_flat_east` | oui (140 lignes) | oui | index écrits, E3 réel ; ma régénération est identique |
| `earth1_flat_{south,west,north}`, `x200` | oui | oui | à comparer sans tolérance |
| `earth1_wall_col9` | oui | partiel | attendu à dériver par D-F4-9 (R2) |
| `earth1_deepwater_cols7_9` | oui | oui, après f6a | exige `FlatCells.Create(groundProperty: …)` à `0x08` |
| `earth1_vs_beannoid_*` (8) | oui | non sur `HitArena` (R5) | cible correcte (335), rochers inchangés ; `84_104` à couper à 128 |
| `earth1_interrupt_{30,60,98,101}` | oui | oui pour objet, naissances et rochers | exclure héros et `snd(23)` |

- **Banc de (w1).** `ArcRun` + champ plat de `InstallFlatField` + `Map(128, 4096)` existe (`AlundraMapEdgeWorldTests.cs:43-50`). L'`ArcRun` accepte aussi une paire de cartes quelconque (`AlundraLootRollWorldTests.cs:66`), donc (p3) sur la 388 est faisable.
- **Gravité des cartes.** Elle vaut 128/4096 sur 481 cartes ; seules 2 cartes ont 3/256. L'oracle à 128 est représentatif.
- **Fidélité à la DLL (FAIT, code).**
  - Ordre du tick : événements, recyclage, bloc des entités (`WorldProxy.cs:2572`, `:2582`), comme l'oracle.
  - Rochers à la naissance : `JoinsBirthTick` et `HasLogicalZ` (`:3572-3574`).
  - Accélération : `Acceleration & 0xF = 0`, force XY instantanée.
  - Liste des coups : statuts 2 et 3.

## 5. Tests proposés : réfutabilité

- **(t1), (d1)–(d4), (e1), (e2), (c1) : réfutables**, sauf les remarques R8 et R9. Le modèle de (d1) et (e1) a été vérifié (0 écart).
- **(w1), (w2) : réfutables, sans tolérance.** Premier test à écrire : il valide la chaîne entière (naissance, clé de vitesse de f5, Hold à L+13, réutilisation des cases).
- **(w3) : réfutable seulement avec la fenêtre de D-F4-9.** Voir R2.
- **(w4) :** vert après f6a ; sans le corps d'E3, les 18 rochers hors eau échouent aussi.
- **(w5) :** voir R5.
- **(w6) :** voir R5.
- **(p1) à (p3) :** voir R4, R6, R7.

## 6. Ce qui manque

1. **Clés de relief.** Je les ai générées avec la même machine (scripts `crit_relief.py`, `crit_pointblank.py`, sorties dans `OUT/work/fcopy/_crit_out/*.json` et `evidence/relief_runs.txt`, `pointblank_run.txt`) :
   - **Pente `gp 0x02`, colonnes 7–9 :** aucun changement de chronologie, seul `Slope_18c = 1` apparaît. Utile pour f6a, pas pour h4c1.
   - **Marche de 16 px (hauteur 1), colonnes 7–9 :** 14 rochers ont leur E3 3 à 5 ticks plus tôt ; 7 meurent posés sur la marche (pz 16).
   - **Marche de 32 px :** 14 rochers E3 3 à 9 ticks plus tôt ; aucun ne se pose dessus.
   - **Mur à 9 px du héros (colonne 5) :** 18 rochers E3 11 à 17 ticks plus tôt ; les rochers montent jusqu'à 14 px collés au mur, restent en statut 3 en l'air, et 9 meurent en l'air (pz 4,5 à 12). Le drapeau « bloqué » n'est levé qu'après plusieurs ticks d'avance partielle.
   - Ce sont les cas les plus fréquents en jeu (lancer près d'un mur ou d'un rebord). À ajouter à T0 et à (w3), avec la fenêtre de R2.
2. **Test d'invariant (p4)** sur le modèle de D-F3-4 : `PlayerManager.UnhandledWeaponSpriteCodes({16}, catalogue, registre de production)` doit être vide après la phase B, et rendre `[(E, 3)]` avant. C'est la vraie preuve de retrait de l'inscription.
3. **Sondes de T0 à ajouter ou corriger :**
   - P2 : rangée = `table[0][col]` (remplace l'ancienne P2) ;
   - P3 corrigée (ticks 10 à 13) ;
   - P5 : écrire l'attendu (E3 3 ou 4 ticks avant l'atterrissage normal pour les 3 rochers) ;
   - nouvelle P7 : `+0xAC = 1` à L+13 et mort à L+14 dans la DLL ;
   - nouvelle P8 : les rochers nés en cases de première utilisation (cases 1 à 14) agissent bien au tick de leur naissance ;
   - nouvelle P9 : la case morte au tick 51 est reprise au tick 52 (même numéro de case).
4. **Couverture des vecteurs :** la direction de base du héros prend les 8 valeurs multiples de 4 ; les vecteurs de h4 n'en ont que 6 (0, 5, 8, 16, 24, 31). Ajouter 4, 12, 20 et 28.
5. **Colonnes comparées de chaque clé,** à lister dans le plan : héros exclu après T pour `interrupt_*`, sons 23 exclus.
6. **Rangée des plans et du tableau 1.31 :** ajouter f5 (D-F5-5), f4 (D-F4-7 pour (w5)), h0a, la fin de la restructuration et Q-R-7 aux prérequis de h4c1. Le tableau actuel ne cite que h4a et f6a.

## 7. Verdict sur les tâches

- **T0 : accepter après corrections.**
  - Garder (a), (b), (c), (d) ; (a) doit citer la machine à index écrits et non `trace_summaries.txt`.
  - Ajouter les clés de relief de §6 et corriger les sondes.
  - Les clés `vs_beannoid_84_104` et `interrupt_*` sont versées avec leurs colonnes comparées.
- **T1 : accepter après corrections.**
  - Retirer (w7) (rangée de départ) ; le remplacer par l'égalité des rangées.
  - (w3) avec la fenêtre D-F4-9 ; (w5) sur le banc de (w1).
  - Écrire (w1) en premier.
- **T2 : accepter après corrections.** Corriger (p1), (p2), (p3) et les listes de la preuve de retrait (R4, R6, R7), ajouter (p4).
- **T3 : accepter après corrections.** Mutants (R8). Garder les deux vérificateurs frais.
- **Acceptation, arrêts, budget, retour arrière :** conserver, avec l'exception de R2 sur « aucune tolérance ».

## 8. Questions pour l'auteur (termes concrets)

- **Q-H4C1-1 (E3 partagé).** Défaut A (type 16 seul) : correct, les types 23, 150-153 et les cartes viendront avec leurs tranches. Rappel : h4d2 réédite la liste.
- **Q-H4C1-2 (388).** À garder. Précision : ces 4 rochers de la carte suivent l'ancienne règle de chute (D-E20-37), donc ils tombent puis disparaissent sans que le tick soit celui de l'original. La 388 n'est atteinte que par le portail de la carte 35.
- **Q-H4C1-3 (rangée de départ).** **À retirer.** Elle ne devrait pas arriver à l'auteur : la prémisse est fausse (R1).
- **Q-H4C1-4 (sauvegarde de test avec le tome).** À garder, défaut A (nouveau préréglage).
- **Q-H4C1-5 (nouvelle).** Un rocher qui heurte un mur en l'air peut s'écraser 1/50 s plus tôt dans un cas sur trois, comme les flèches déjà acceptées (D-E20-38). Défaut A : accepter, documenté.
- **Information, pas question :** proposer de fermer O-E0A5-2 et O-E0A5-4 (R10).

## 9. Fichiers (tous sous `OUT`)

- **Outils :** `tools/mydis.py`, `tools/my_driver_model.py`, `tools/scan_tmpl.py`, `tools/compare_idx.py`, `tools/rows_check.py`, `tools/box_check.py`, `tools/portals388.py`.
- **Listings :** `lst_*.txt`.
- **Preuves brutes :**
  - `evidence/vectors_replay_my_model.txt` (689/689 et 224/224) ;
  - `evidence/template_recycle_machine.txt` (R1) ;
  - `evidence/rows_vs_template.txt` ;
  - `evidence/index_effect_h4_harness.txt` (C1) ;
  - `evidence/box_ticks.txt` (R3) ;
  - `evidence/relief_runs.txt` et `pointblank_run.txt`.
- **Copies de travail (rejeu depuis une copie) :**
  - `work/h4copy/` (harnais h4) ;
  - `work/fcopy/` (harnais f4f6) avec mes scripts `crit_template.py`, `crit_relief.py`, `crit_pointblank.py` ;
  - `work/crit_idx.json` et `work/crit_noidx.json`.
- **Clés candidates de relief :** `work/fcopy/_crit_out/` (`crit_step16_cols7_9.json`, `crit_step32_cols7_9.json`, `crit_slope_gp2_cols7_9.json`, `crit_pointblank_wall_col5.json`, `crit_flat.json`).
- **Rejeu :** `python -B` depuis une copie ; chaque script de `work/fcopy` tourne en quelques secondes.