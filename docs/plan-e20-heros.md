# Plan — E20, le héros : course, attaque et coups

Étape E20 de [plan-conversion-totale.md](plan-conversion-totale.md). Elle porte ce que le héros fait
de ses boutons d'action : la course de Triangle, l'attaque de Carré, porter et lancer, les objets du
Rond. Elle porte aussi la passe de coups commune à tout le jeu, les dégâts, et la réaction du héros
quand il est touché. E14 garde l'IA native des ennemis et des PNJ.

**Statut** : proposé le 2026-10-09, sur la branche `chantier/e20-heros` (base : §0.3), avec l'enveloppe (§1.1) et
la première tranche exécutable, E20.0 (§1.2). Les tranches suivantes se planifient et s'approuvent
chacune à part, comme en E19. Décisions de l'auteur : §0.1 et ADR-0052.

| Unité | Revue avant approbation | Après exécution |
|---|---|---|
| Enveloppe (ce plan) + E20.0 | plan-verifier | contre-vérification indépendante des faits, critique de complétude |
| E20.a à E20.h | plan-verifier, tranche par tranche | verifier frais |

Aucune tranche ne touche aux secrets ni à une entrée non fiable. E20.c2 change les PV du héros, que la
sauvegarde garde déjà et valide (E16.c) : pas de nouveau format, pas de revue de sécurité. Si une
tranche en ajoute, sa ligne ajoute la revue.

**Exécution** : la session principale garde la synthèse, les plans et les décisions. Les tranches de
code passent à un `executor` sous un contrat approuvé, puis à un `verifier` frais. Les découvertes
passent par des agents en lecture seule, avec une contre-vérification du binaire.

**Révision 1 (2026-10-09).** Première relecture : **REVISE** pour l'enveloppe (deux P2) et pour E20.0
(quatre points). Corrections :
- E20.a est propriétaire de la structure du tick : passes communes à toutes les entités une fois par
  tick (compteurs, point d'accroche de la passe de coups), avec un test de 0, 1 et 2 ticks par image ;
  E20.c1 prouve le coup écrit au tick N, lu au tick N+1 puis effacé ; D7 inventorie tout le tick ;
- base de la branche et partage des fichiers du héros avec E19 (§0.3) ; O-E19-80 passe à E20.a, noté
  dans les deux plans ; la référence fausse du §0.2.1 est corrigée ;
- D1 nomme chaque passe du tick, vraie ou bouchonnée, la source des données d'animation, et un critère
  exact (S-A et S-B, champ par champ, convention du +1) ;
- D2 reçoit six cas de validation ; D6 couvre tout le corpus, avec sa limite.

**Révision 2 (2026-10-09).** Relecture neuve de la révision 1 : enveloppe **READY** ; E20.0
**REVISE**, deux points, tous deux corrigés (FIX) :
- D1 écrit son état de départ : sol synthétique plat aux constantes de la carte 10, départs de S-A et de
  S-B (chauffe de S-B et premier tick comparé), valeurs de départ du héros (PV, drapeaux, arme) ;
- le cas de recouvrement de D2 est écrit en unités brutes 16.16, des deux côtés.
C'est le second REVISE d'E20.0 : une seule relecture de clôture suit ; un nouveau REVISE met la tranche
en pause et passe la question à l'auteur.

Relecture neuve de clôture de la révision 2 : E20.0 **READY** ; l'enveloppe était **READY** depuis la
révision 1. L'enveloppe et E20.0 attendent l'approbation de l'auteur.

---

## 0. Cadre

### 0.1 Décisions de l'auteur (2026-10-09)

Question de l'auteur : « Quelle est la tâche qui s'occupe des attaques et du sprint ? ». Réponse : aucune
tâche planifiée. Le plan d'E19 les renvoyait à E14 (`plan-e19-opcodes.md`, périmètre d'E19.d2c :
« nage (pente 4), transport, course, attaque en l'air, saisie et coups (E14) »), alors qu'E14, au plan
maître, ne parle que de l'IA des ennemis et des PNJ. Les décisions suivantes ont été prises avec
l'auteur le 2026-10-09, après une découverte en lecture seule (§0.2).

- **D-E20-1** — **Tâche séparée pour le héros** : la course, l'attaque, porter et lancer, les objets du
  Rond forment l'étape E20. E14 reste l'IA native : les gestionnaires C, D et E de chaque type de
  sprite. Cette décision remplace, pour la course, l'attaque, la saisie et les coups, le renvoi à E14
  du plan d'E19.
- **D-E20-2** — **Le héros tourne à chaque tick logique**, comme le binaire : `MovePlayer` passe une
  fois par tick, sur le pad de tick (`AlundraTickPad`), qui a le relâchement de bouton. Le saut déjà
  validé et les traces de référence du héros sont re-mesurés.
- **D-E20-3** — **La passe de coups commune est dans E20** : chargement des fiches d'équilibre, boîtes
  d'attaque par image, `UpdateBalanceRecords`, calcul des dégâts, réaction du héros touché, et les
  prédicats `0x86`, `0x87` et `0x95`. E14 garde les gestionnaires propres à chaque ennemi.
- **D-E20-4** — **Les Muruta et les Beannoïde forment la première tranche d'E14** (E14.a), juste après
  la passe de coups (E20.c1). Pas de sauvegarde de test placée après le hall de la 115.
- **D-E20-5** — **Avant E18, le héros perd des PV mais ne meurt pas.** C'est un écart provisoire : la
  branche `Hp == 0` de `MovePlayer` (`0x800327A4`) reste non portée jusqu'à E18.
- **D-E20-6** — **Boîtes d'attaque par image** : prouver d'abord que les `collision_keyframes` déjà
  exportées redonnent exactement les enregistrements de 6 octets du binaire. Si oui, la DLL les lit ;
  sinon, le convertisseur exporte les enregistrements exacts (décision de format, nouvel ADR).
- **D-E20-7** — **L'épée de base d'abord.** La table des armes est portée en entier, comme donnée ; la
  charge, le fléau, les baguettes et l'arc viennent dans une tranche ultérieure (E20.f).
- **D-E20-8** — **Arrondi d'angle pour le stick analogique**, ajout du port. Le clavier et la croix
  directionnelle gardent la règle d'origine : la course ne part que sur une direction cardinale pure.
  Pour le stick, la règle exacte est proposée par E20.0 (D8) et soumise à l'auteur.
- **D-E20-9** — **Deux comportements de l'original sont gardés** : une attaque chargée relâchée en
  l'air est perdue ; un appui suivi d'un relâchement de Carré dans le même tick ne frappe pas. Rien ne
  prouve un défaut.
- **D-E20-10** — **Porter, lancer et les objets du Rond sont dans E20**, après le combat (E20.g et
  E20.h). La 362 demande une bombe posée sur un couvercle, et la barricade de caisses de la 392 se passe
  dans l'original en soulevant ou en grimpant.

Règles reconduites : `ALUN_CD.EXE` (France) tranche sur la décompilation, qui se lit et ne se corrige
pas ; un défaut avéré de l'original se corrige ; tout écart du port est documenté dans le code et ici.

### 0.2 Faits établis (découverte du 2026-10-09)

Source : [plan-e20-annexe/decouverte-2026-10-09/](plan-e20-annexe/decouverte-2026-10-09/README.md),
cinq relevés, trois contre-vérifications du binaire et une critique de complétude. Étiquettes :
**[binaire]** décodé dans `ALUN_CD.EXE` et confirmé par la contre-vérification ; **[données]** lu dans
le projet exporté (`alundra-project/`) ; **[DLL]** lu dans le code du port ; **[décompilation]** lu
seulement dans la décompilation ; **[inférence]** déduit, avec sa base. Là où un relevé et sa
contre-vérification divergent, la contre-vérification est retenue (critique, §1).

#### 0.2.1 Rythme et ordre des passes

- [binaire] La passe des entités `0x8003B388` tourne quand `[0x800DC4B8] & 0x48 == 0`, dans cet
  ordre : `0x80038634`, `0x800386D0` (événements, qui appelle `MovePlayer` `0x80031B50` en premier, à
  `0x800386EC`), `0x80038998` (compteurs), `0x800384F4` (listes), `0x80038E18`, `0x80038364`
  (physique), `0x80038E84`, `0x80039300` (coups). Un coup écrit à la fin du tick N est lu par les
  événements du tick N+1, puis effacé par la passe des compteurs.
- [DLL] `MovePlayer` tourne une fois par image rendue (`AlundraEntityScriptProxy.cs:1408`, branche du
  joueur `1384-1457`) ; le pad du héros `LastPadState` est refait à chaque image. Seul
  `AlundraTickPad` (`AlundraTickPad.cs:44`, `:58`) a le relâchement de bouton ; le runner le note pour
  `0x2F` (`AlundraEventProgramRunner.cs:3154`).
- [DLL] La DLL fait passer les entités avant les événements de carte, à l'inverse du binaire ; c'est
  la cause du bug du début de partie corrigé par E19.v2 (§1.2z du plan d'E19).
- [DLL] Chaque entité fait tous les ticks de l'image dans sa propre boucle (choix du programme,
  programme, horloge d'animation, mouvement) avant que l'entité suivante commence
  (`AlundraEntityScriptProxy.cs:1312-1325`) ; le héros fait ses ticks dans son propre `Update`
  (`:1399-1426`). Aucune passe n'est commune à toutes les entités une fois par tick, comme la passe
  des compteurs ou la passe de coups du binaire.

#### 0.2.2 `MovePlayer` : prologue et ordre des appels

- [binaire] Avant l'aiguillage d'état (table `0x80022D3C`, `sltiu 0x50` à `0x80031D8C`), chaque tick
  sur un terrain appelle `UpdatePlayerWeaponEffect` `0x8002F49C`, `UpdateWeaponStepProgression`
  `0x8002F768` et `UpdatePlayerCarriedEntity` `0x8002F884(0)` (`0x80031D68-0x80031D78`).
- [binaire] Sorties anticipées, prioritaires sur tout état :
  - entrée des dégâts `jal 0x80031A68` à `0x80031C44` : un résultat ≠ −1 met l'état blessé (§0.2.5) ;
  - `+0x20 ≠ 0` finit le tick (`0x80031BAC`) ;
  - `[0x800DC4B8] & 0x34` : branche verrouillée, `CreatePlayerAnimationEffects(1)` seulement ;
  - `+0x14 == 0` (PV nuls) : `0x800327A4`, branche de mort (E18) ;
  - `+0x188 & 0x80` : `0x80032768`.
- [binaire] Idle et Moving (`0x80031E38`) : `TargetDirection = dir`, puis `TryUseItem` `0x8002ED64`
  (0 finit le tick), `PlayerTryAction` `0x8002EEAC` (≠ 0 finit le tick), `CheckEntityInteraction`
  `0x8002E910` (2 donne Idle et finit le tick, une autre valeur ≠ 0 finit le tick), puis la queue.
  Les états de saut 2, `0x2B`, `0x2C`, `0x2D` (`0x80031E84`) font de même sans
  `CheckEntityInteraction`.
- [binaire] Queue `0x80031EA8` : `PlayerTryAttack` (≠ 0 finit le tick, donc Carré l'emporte sur la
  Croix) ; en l'air (`+0x144 == 0`) vers `0x80031F44` ; front de Croix `& 0x40` vers le saut (sauf
  `+0x180 & 0x4000`) ; Triangle tenu `& 0x10` (`0x80031F1C`) vers `0x29`, avec `+0xB0 = 0`.
- [binaire] Valeurs des appels tant qu'ils ne sont pas portés : `TryUseItem` rend 1 sans front de Rond
  (`0x8002EDA8`) ; `PlayerTryAction` rend 0 quand le héros ne porte rien et n'a rien à soulever
  (`0x8002EFC8`). Un bouchon qui rendrait 0 pour `TryUseItem` tuerait toute attaque, tout saut et
  toute course (critique, C10 et C11).
- [DLL] La porte d'escalade passe déjà avant l'aiguillage (`AlundraPlayerManager.cs:243-255`).

#### 0.2.3 La course

- [binaire] Cinq états dans `MovePlayer` : `0x29` PrepareSprint (`0x80031F54`), `0x03` Sprint
  (`0x80031FF4`), `0x2A` StopSprint (`0x80032118`), `0x04` SprintDash (groupe `0x80031E18`) et `0x3E`
  SprintAgainstWall (`0x80032558`, partagé avec `0x1C`).
- [binaire] Entrée : Triangle **tenu** (pas de front), au sol, depuis Idle, Moving ou un état de saut,
  après l'attaque et le saut du même tick. Dans `0x29`, chaque tick appelle `TryUseItem`,
  `PlayerTryAction` et `PlayerTryAttack`, puis teste l'état en l'air (vers `0x2D`) ; la direction est
  écrite même ce tick-là. Triangle relâché donne Idle. Le départ vers `0x03` demande
  `AnimCompleteCounter ≠ 0` et une direction de la table `0x80022CAC`, qui ne connaît que les quatre
  cardinales (haut `0x10`, droite `0x18`, bas `0x00`, gauche `0x08`, −1 ailleurs, diagonales
  comprises).
- [binaire] `0x03` : appelle seulement `PlayerTryAction`. En l'air, `0x2D`. `ForceAdjusted ≠ 0` crée
  l'effet 9 à Z + `0x100000` (`0x80032048`), puis passe en `0x3E`, direction retournée. Plus aucune
  direction tenue : `0x2A` avec le compteur `0x80126FF4` à 1 (reprise possible). Triangle relâché, ou
  direction hors du cône `0x80022CEC + direction × 12` : `0x2A` avec le compteur à 0. `Hold & 0xE0`
  (Rond, Croix ou Carré tenu, `0x80032104`) : `0x04`. La course ne change jamais `TargetDirection`.
- [binaire] `0x2A` : `TryUseItem`, `PlayerTryAction`, puis l'état en l'air. Le compteur `0x80126FF4`
  monte tant que Triangle reste tenu ; au-delà de 10 (`slti 0xb`), une direction cardinale relance la
  course, éventuellement dans une autre direction. Seul `MovePlayer` touche ce compteur.
- [binaire] `0x04` : en l'air, `0x2D` ; puis `TryUseItem`, dont le résultat est ignoré. `0x3E` et
  `0x1C` finissent quand `+0xAC == 1` : direction retournée, Idle au sol, **la valeur 1 en l'air**
  (`0x800325A0`) ; la décompilation écrit Jump (`PlayerManager.cs:769-770`).
- [binaire] Escalade prioritaire : pente 6, Haut tenu, regard `0x10`, `ForceAdjusted ≠ 0` et
  `+0x2C == 0` donnent `0x0E` avant l'aiguillage (`0x80031D20-0x80031D64`). Une course vers le haut
  contre une case de pente 6 grimpe au lieu de rebondir.
- [binaire] `CheckEntityInteraction` n'est appelé que depuis Idle et Moving : la course ne pousse ni
  ne saisit rien.
- [données] Fiches d'animation du héros (`Data/sprite-records.json`) : `0x03` vitesse 336,
  accélération 2, son 13, boucle ; `0x04` vitesse 0, accélération 6, son 83, enchaîne sur 0 ; `0x29`
  vitesse 0, boucle ; `0x2A` vitesse 0, accélération 5, son 14, enchaîne sur 0 ; `0x3E` vitesse 256,
  octet `0x40` (invulnérable), son 28, impulsion Z 1024, tenue. La course va 1,6 fois plus vite que la
  marche (258048 contre 159744 par tick en horizontal) [binaire, `0x80036614`].
- [binaire] Pendant `0x3E`, le héros est invulnérable : `AnimFlags & 0x40` le fait sauter par la passe
  de coups (`0x8003943C`). `0x04` porte l'attaque « Tackle » (fiche du héros, `Attacks[5]`, type 5,
  puissance 2) et coupe l'herbe (§0.2.6).
- [binaire] Pas, poussière et sons de la course passent par `CreatePlayerAnimationEffects`
  (`0x8002FB14`) : sons 12, 21, 19, 16 selon la pente toutes les 8 images en `0x29`, poussière toutes
  les 4 images au sol ; sur les pentes 1, 2 et 4, poussière à −0,25 fois la force et aucune force pour
  `0x04` et `0x2A` (contre-vérification, ligne 12).
- [DLL] Rien de la course n'est porté (`AlundraPlayerManager.cs:63-69`, `:418-424`) ; un test épingle
  « Triangle seul donne Idle » (`AlundraHeroJumpStatesTests.cs:216-219`). Vitesses, fins d'animation,
  son à chaque changement d'animation et impulsion Z marchent déjà pour toute animation.
  `AnimCompleteCounter` n'est pas remis à zéro au changement d'animation, comme dans le binaire
  (`0x80038AB4-0x80038E10`) ; la décompilation le remet à zéro (`EntityManager.cs:222`).

#### 0.2.4 L'attaque

- [binaire] `PlayerTryAttack` (`0x8002EAF4`) n'a que deux appelants : la queue `0x80031EA8` et
  `0x29` (`0x80031F74`). Il rend 0 si `+0x180 & 0x2000`. Il teste **d'abord** le relâchement de Carré
  (`0x80126F2C & 0x80`, `0x8002EB38`) : avec un compteur de charge sous 60, ou en l'air, il rend 0 ;
  sinon, c'est l'attaque chargée. Seulement sans relâchement, il teste l'appui (`0x80126F2A & 0x80`,
  `0x8002EC8C`). Aucun test de PM, d'inventaire ni de munitions.
- [binaire] L'arme : `g_playerStats.WeaponId` (emplacement 1 à 6 ou −1), puis l'objet, puis la fiche
  `byte[0x80028C14 + objet]` ; sans arme, l'octet `0x80028C13` vaut 0, donc la fiche 0. Fiches de 13
  octets à `0x80028C78` : chargeable, puis animation, type, délai et Z au sol, en l'air, chargée. Douze
  fiches, détaillées dans l'annexe (`2-attack.md`, §3) ; l'épée de base est la fiche 0 : `0x10` au sol,
  `0x14` en l'air, pas de charge. L'animation 0 (bâton) joue le son 3 et rend 0.
- [binaire] Réussite : `0x80127110` reçoit un pointeur sur la fiche, `0x80127114 = 0`,
  `TargetAnimationId` reçoit l'animation (`0x8002ED14-0x8002ED2C`).
- [binaire] États d'attaque : le groupe au sol (`0x80031E18`) passe en `0x2D` en l'air ; le groupe en
  l'air (`0x80031DF8`) passe en Idle au sol ; les deux appellent `TryUseItem`. Ils ne lisent pas le
  pad : pas de combo, la reprise est la fin de l'animation.
- [binaire] Charge (`0x8002F49C`) : le compteur `0x80126FFC` monte jusqu'à 60 quand Carré est tenu ou
  relâché, l'arme chargeable et l'octet 0 de `0x80028D14 + 2 × animation` non nul ; sinon il revient à
  0. Aura (effet 3) dès 11, sons `0x2A`, étincelle `0x10` et sons `0x1AD`/`0x1AC` à 60 ; le relâchement
  chargé fait 16 étincelles et le son `0x2B`. Tirs différés (`0x8002F768`) : `SpawnWarpEntity`
  `0x80039F58` après le délai de la fiche, pour les baguettes, l'arc et l'épée chargée.
- [données] Durées exportées : épée au sol 0,42 s, en l'air 0,20 s ; son 29 pour l'épée. Les
  animations d'attaque ont une vitesse nulle.
- [binaire] Nouvelle partie : `SetPlayerWeaponId(1)` (`0x80031894`) ; le kit vient des objets dont
  l'octet 6 a le bit `0x80` : épée de base, veste en tissu, bottes normales [données].
- [DLL] L'arme équipée existe (`AlundraPlayerStats.WeaponId`, `GetItemIdFromCurrentWeapon`,
  `AlundraPlayerManager.cs:936`) ; Carré est déjà lié (clavier X, manette X,
  `PlayerSetupWriter.cs:133-148`).

#### 0.2.5 Les coups

- [binaire] **Une seule passe pour tout le jeu**, `UpdateBalanceRecords` (`0x80039300`), appelée
  seulement à `0x8003B3F0`. Pour chaque attaquant qui a une boîte (`+0x1D4`), une attaque (`+0x1C8`)
  et un attribut non nul, elle teste chaque cible de la liste (statut 2 ou 3, `+0x20 == 0`) :
  - la cible est sautée si `+0x1D0 ≠ 0`, `+0x1CC ≠ 0`, `AnimFlags & 0x40`, ou si
    `Flags & masque == 0`, avec un masque **réordonné** depuis les drapeaux de l'attaquant :
    `((F >> 2) & 1) | ((F << 2) & 8) | (F & 0x1000 ? 0x800 : 0)` (`0x8003939C`) ;
  - recouvrement entier inclusif, élargi d'une unité (`0x80039464-0x8003951C`) ;
  - réponse `r = fiche de la cible[(attr & 0xF) + 2]` ; `(r & 0xC0) == 0x80` : immunisée, pas de
    `TouchingEntity` ; `0xC0` reçoit `TouchingEntity` mais aucun dégât ;
  - attributs 6 et 10 : effet 4 attaché à la cible ; 7 et 9 : effet 5 ;
  - toujours : `+0x1D0 = 0x19` sur la cible, `HitCounter` (`+0x220`) de l'attaquant +1, et 3
    étincelles (effet 9) au centre du recouvrement (`0x80039188`) ;
  - un attaquant peut toucher plusieurs cibles dans le même tick ; une cible n'accepte que le premier
    attaquant de la liste.
- [binaire] `CurrentAttack` est rechargé à chaque changement d'animation (`0x80038B74-0x80038BAC`) :
  `Attacks[animation + 1]` de la fiche, ou `Attacks[0]`, ou rien. La boîte de l'image courante
  (`0x80038C44-0x80038CB8`) : décalages signés `<< 16`, tailles `(n << 16) − 1`, décalage `0xFFFF`
  donne rien. `HitBox` = position + décalage (`0x80038064-0x800380B4`).
- [binaire] Passe des compteurs (`0x80038998`, toutes les cases) : efface `TouchingEntity`,
  `RidingEntity` et `HitCounter`, incrémente le compteur d'images `+0x1C`, décrémente `+0x1CC` et
  `+0x1D0`.
- [binaire] Choix du programme (`0x800386D0`, depuis la case 1) : les déclencheurs E l'emportent sur D,
  et un coup reçu le même tick qu'un déclencheur E est perdu ; `TouchingEntity` choisit D.
- [binaire] Dégâts (`ComputeNewHp` `0x8003A374`, 37 appelants dont le héros, puis
  `ResolveBalanceTarget` `0x8004464C`) : `dégât = ((puissance + somme des trois fiches d'équipement si
  attr & 0x80) × r) >> 4`, au moins 1 ; `r & 0xC0 == 0x40` tue ; `0x80` et `0xC0` ne changent rien.
  L'animation du héros lue ici (`0x80146DF8`) est figée par la passe de coups du tick N, et lue au tick
  N+1. Les trois fiches d'équipement (`0x80146E00`) sont remplies par `UpdateItemEffectState`
  (`0x800307E8`) ; quels objets les remplissent : [décompilation] seulement (E20.0, D4).
- [données] Seuil d'équilibre : la fiche d'un ennemi se choisit par l'octet `+0xB` de l'en-tête de
  carte (`0x8002C39C`, `0x80044550`) [binaire]. Le champ exporté `BalanceLevel` est l'octet `+9` (2 sur
  toutes les cartes, l'effet de bris des tuiles) ; le vrai seuil est exporté sous `D` (0 à 11).
- [binaire] Le héros touché (`0x80031A68`) : avec un `TouchingEntity`, `ComputeNewHp(héros)` et la
  direction du toucheur vers le héros ; sinon, si `+0x1CC == 0` et `[0x80127000] < 3`, les cases
  dangereuses coûtent 0 à 3 PV (table `0x80028B8C`), sans armure. Puis `0x31` (`0x3A` sur pente 4),
  `+0x1CC = 0x78`, objet porté lâché (`0x8002F884(2)`), appel de `0x8003634C` (rôle inconnu, E20.0 D5).
  Clignotement : l'entité n'est pas dessinée quand `(+0x1CC & 3) == 3` (`0x800385F0`). PV nuls testés
  seulement un tick sans dégât : la mort part un tick après le coup.
- [binaire] Prédicats : `0x86` **pose** `+0x1CC` (`0x8003FFD4`, la décompilation dit « ajoute ») ;
  `0x87` (`0x80040048`) et `0x95` (`0x800407C0`) lisent la table `0x80098FA4` = épée, marteau, flèche,
  bombe, charge, charge, feu, glace ; les indices 8 à 11 de `0x95` sur la carte 226 lisent les octets
  qui suivent la table, à copier tels quels. Corpus : `0x87` 182 sites, `0x95` 43, `0x86` 76.
- [données] Classes : l'en-tête du héros (`0x3118C`) touche les classes A et C ; les ennemis (A et
  touche B) touchent le héros. Caisses et jarres n'ont aucune classe ; les coffres s'ouvrent par le
  programme F ; aucun canal du héros n'atteint les PNJ.
- [DLL] Rien de la passe n'existe : `Hp` du héros jamais posé (`AlundraWorldProxy.cs:1790`) ;
  `HitCounter`, `TouchingEntity` et `DamagedTickCounter` déclarés et jamais écrits ; `CurrentAttack`,
  `BalanceRecord` et `FrameCollision` commentés (`AlundraEntityScriptProxy.cs:399-403`) ;
  `balance.json` exporté et lu par personne ; le compteur `+0x1C` déclaré
  (`AlundraEntityScriptProxy.cs:72`) et jamais incrémenté. Le choix E/D est déjà porté
  (`AlundraEntityScriptProxy.cs:1498-1531`), et `0x47`/`0x48` lisent déjà `HitCounter`
  (`AlundraEventProgramRunner.cs:743-753`). Le HUD lit `PlayerStats.Hp`.

#### 0.2.6 Herbe coupée et butin

- [binaire] `CheckAndTriggerTileEffect` (`0x8003A7B0`) tourne pour 15 animations du héros, dont `0x04`
  et les coups d'épée et de fléau (pas l'arc ni les baguettes) : une case de drapeau `0x02` à la bonne
  hauteur sous un coin de la boîte perd ce drapeau et sa tuile de mur, crée l'effet de l'octet 9 de
  l'en-tête de carte, tire un butin (`0x80032C7C(0xFF)`) et joue le son `0x1F`. Butin : une chance sur
  16 d'argent, une sur 16 de cœur (lignes `0x7D`, `0x7E`, `0x7F`).
- [données] 9089 cases de 165 cartes ont ce drapeau.

#### 0.2.7 La chaîne de l'histoire

- [données] Jour 1 (389 → 164) : ni attaque ni course n'est demandée.
- [données] Carte 15 : le chemin passe par de simples sauts ; les Murruta de la scène se détruisent
  seuls ; trois orques sont facultatifs.
- [données] **Premier combat obligatoire : le hall du manoir de Tarn (115).** `B[2] @172` active les
  entités 24 à 27, quatre Muruta (`0x2D @401-408`), puis attend la destruction de chacune
  (`0x2C @414-433`, vérifié dans l'export) avant de poser G569 et de rouvrir les portails
  (`@434`, `@493-503`).
- [données] Ensuite : le levier de la 115 nord-ouest, les leviers et la grille de la 116 (deux
  Beannoïde), la bascule de la 117, la 44 (huit Beannoïde, six leviers, le slime géant), puis la 362,
  dont le couvercle n'accepte qu'un coup de bombe (`0x87 [128,3] @728`).
- [données] Rien de ce qui a été relevé n'exige la course ; la géométrie de la 116, de la 117, de la 44
  et de la 362 n'a pas été entièrement vérifiée.
- [données] Jusqu'au jour 4, le héros n'a que l'épée de base : aucun `0xC0` sur la chaîne, et les
  coffres ne donnent pas d'arme.
- [données] Barricade de caisses de la 392 : la seule passe est couverte de caisses empilées ;
  l'original la franchit en soulevant ou en grimpant (inconnu), la DLL la traverse (D-E19-46).

#### 0.2.8 Décompilation et binaire, dans ce périmètre

Le port suit le binaire sur chacun de ces points :

1. fin de `0x3E` et `0x1C` en l'air : le binaire écrit 1 (Moving), la décompilation Jump ;
2. `AnimCompleteCounter` jamais remis à zéro au changement d'animation dans le binaire ;
3. `0x29` : l'état en l'air se teste après les trois appels ;
4. le refroidissement des tirs différés est un pointeur sur la fiche (`0x8002ED18`), pas un tableau ;
5. étincelles du relâchement chargé : Z `+0x100000`, position décalée de `× 3 << 10`, vitesse
   d'indice `(2i + 0x18) & 0x1F` ;
6. étincelles de la charge : la décompilation omet leur dispersion ;
7. 3 étincelles par coup, pas 2 (`0x800391A8`) ;
8. herbe coupée : un seul `sra 0x14` (`0x8003A860`), effet à la hauteur `<< 20`, effet de l'octet 9 ;
9. tirage du butin : un seul tirage par ligne (`0x80032980-0x800329B0`) ;
10. `0x86` pose au lieu d'ajouter ;
11. l'entrée des dégâts du héros est nommée `FindWarpFacingDirection` dans la décompilation ;
12. sans arme, la décompilation indexe avec −1 (`PlayerManager.cs:24-28`), le binaire lit
    `0x80028C13`.

#### 0.2.9 Outils et tests

- [DLL] `docs/plan-e19-h2-annexe/tools/` émule des fonctions MIPS du binaire (`mips.py`, `emu.py`,
  `zsim.py` pour la physique et les gestionnaires Z). Aucun outil n'exécute `MovePlayer`. La
  découverte a fait tourner `PlayerTryAttack`, `0x8002F49C` et `0x8002F768` dans `emu.py`, sans
  garder les commandes.
- [DLL] `JumpHeroRig` ne déclenche pas les fins d'animation ; `ArcRun` avec les vrais préfabs les
  déclenche, mais résout le sol dès l'image 1 (E19.v2). Les premières images du vrai jeu se vérifient
  par un harnais hors dépôt qui dérive `CasaEngineGame` (méthode d'E19.v2).
- [DLL] Traces de référence : quatre traces du héros (`docs/hero-trace-389-*.txt`) et deux de l'intro.

### 0.3 Base et partage avec E19

- **Base** : la branche `chantier/e20-heros` part de `chantier/e19-suite3` au commit `8e023d8`. Ce
  commit contient E19.v2 (`4f86666`, le héros immobile au début de partie), E19.m13 (`c6557fc`) et la
  découverte D1 d'E19.h2 (`492eb6c`). Si l'auteur fusionne d'abord E19 dans `main`, la base devient ce
  commit de `main`, au contenu identique.
- **Fichiers du héros** : de l'approbation d'E20.a à sa fusion, E20 est seule à modifier
  `Alundra/Scripts/AlundraPlayerManager.cs`, la branche du héros et la boucle des ticks
  d'`AlundraEntityScriptProxy.cs`, `AlundraTickPad.cs` et l'ordre du tick d'`AlundraWorldProxy.cs`.
- **O-E19-80** (pose de saut figée possible à une arrivée hors `LoadingMap`) passe à E20.a : le passage
  du héros par tick change ses premières images. Le plan d'E19 le note.
- Les points d'E19 encore ouverts qui touchent au mouvement du héros (l'approximation de la porte 2
  d'E19.m13, dans `AlundraScriptedMotion.cs`) restent à E19, mais attendent la fusion d'E20.a.

---

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Preuve | Recette en jeu |
|---|---|---|---|
| **E20.0** (§1.2) | Découverte complémentaire, sans toucher au code : oracles d'émulation de `MovePlayer` et de la passe de coups, dégâts au contact des ennemis, puissance de l'équipement, `0x8003634C`, preuve des boîtes exportées (D-E20-6), inventaire du passage par tick (D-E20-2), règle du stick (D-E20-8) | recalcul indépendant, contre-vérification du binaire, critique | — |
| E20.a | Passe du héros par tick (D-E20-2) et **structure du tick**, dont E20.a est propriétaire : `MovePlayer` à chaque tick logique sur `AlundraTickPad`, à la place du binaire ; les passes communes à toutes les entités, une fois par tick, à la place du binaire : la passe des compteurs (`+0x1C`, effacements, décomptes) et le point d'accroche de la passe de coups (vide jusqu'à E20.c1) ; l'inversion entités / événements de carte corrigée ou gardée comme écart, selon E20.0 (D7) et l'auteur ; les trois appels dans l'ordre du binaire, bouchonnés (`TryUseItem` 1, `PlayerTryAction` 0, `PlayerTryAttack` 0) ; les appels du prologue bouchonnés ; O-E19-80 ; retrait des contournements du rythme par image | tous les tests ; un test avec 0, 1 et 2 ticks dans une image : chaque passe commune tourne une fois par tick, dans l'ordre du binaire ; traces du héros re-mesurées avec l'écart expliqué ; sonde du vrai jeu sur la nouvelle partie et sur une arrivée `0x52`/`0x53` | Marche, saut, échelle, dialogues, inventaire, livre : rien ne change |
| E20.b | Coup d'épée sans dégâts : table des armes en entier, `PlayerTryAttack` (relâchement d'abord, puis appui), garde `0x2000`, son 3, attaques au sol et en l'air, états d'attaque, priorités (Carré sur Croix, interaction d'abord) | oracle de `MovePlayer` ; arcs au vrai préfab | Carré frappe au sol et en l'air, avec le son ; le héros freine |
| E20.c1 | Passe de coups et dégâts aux cibles (D-E20-3) : fiches d'équilibre (seuil `D`), PV des entités, `CurrentAttack`, boîtes par image (D-E20-6), `HitBox`, `UpdateBalanceRecords` au point d'accroche d'E20.a, avec effets 4, 5 et 9, animation figée, dégâts avec l'équipement, `0x86`, `0x87`, `0x95`, réactions scriptées (leviers) | oracle de `0x80039300` et `0x8004464C` sur des ensembles tirés au hasard ; un test où un coup écrit au tick N est lu par les événements du tick N+1 puis effacé par la passe des compteurs, avec deux ticks dans une image ; le premier attaquant l'emporte ; arcs des leviers posés directement | Un coup sur un Beannoïde de la 14 : étincelles (sa réaction vient d'E14) |
| *E14.a* (autre étape) | Muruta et Beannoïde (D-E20-4), planifiée après E20.c1 | plan d'E14 | Le hall de la 115 |
| E20.c2 | Le héros touché : entrée `0x80031A68` (toucheur et cases dangereuses), `0x31`/`0x3A`, invulnérabilité `0x78` et clignotement, PV vers `PlayerStats` et le HUD, `0x8003634C`, chemin `controlFlags == 0x20`, pas de mort (D-E20-5) | oracle de `0x80031A68` ; arcs | Les cœurs baissent au contact, le héros clignote, il ne meurt pas |
| E20.d | Course : les cinq états, entrée et sorties, cône, reprise, rebond, charge `0x04` (ses coups viennent d'E20.c1), escalade prioritaire, arrondi du stick (D-E20-8) ; le test épinglé « Triangle seul donne Idle » s'inverse | oracle de `MovePlayer` ; arcs | Courir, s'arrêter, reprendre, foncer, buter contre un mur, sauter d'un rebord, entrer dans l'eau |
| E20.e | Effets du héros et herbe coupée : `CreatePlayerAnimationEffects` (pas, poussière de marche, de course et d'atterrissage), `CheckAndTriggerTileEffect`, butin (propriétaire de l'objet ramassable : O-E20-3) | oracle ; arcs | Pas et poussière ; l'herbe se coupe à l'épée et à la charge ; argent et cœurs tombent |
| E20.f | Autres armes (D-E20-7) : charge et aura, fléau et rebond, baguettes, arcs, `SpawnWarpEntity` par indice global, projectiles (O-E20-4), `0xC0`/`0xC1` | oracle ; arcs | Sauvegarde de test avec les armes (à créer) |
| E20.g | Porter, poser, lancer (`PlayerTryAction`, `UpdatePlayerCarriedEntity`) ; caisses et jarres redeviennent des obstacles (fin de l'écart D-E19-28/D-E19-46) ; barricade de la 392 (O-E20-5) | oracle ; arcs | Soulever et lancer une caisse ; la 392 |
| E20.h | Objets du Rond (`TryUseItem`) : graines, bombes, magie, potions ; le couvercle de la 362 | oracle ; arcs | La 362 |

- **Ordre** : E20.0 → E20.a → E20.b → E20.c1 → E20.c2 → E20.d → E20.e → E20.f → E20.g → E20.h.
  E14.a se planifie dès qu'E20.c1 est faite. Le hall de la 115 devient jouable avec E20.a, E20.b,
  E20.c1 et E14.a.
- **Dépendances** : tout dépend d'E20.a (le rythme par tick, le relâchement, et la structure du tick, dont
  E20.a est propriétaire ; E20.c1 branche la passe de coups à la place qu'E20.a a créée). E20.c1 dépend
  d'E20.0 (D6) pour les boîtes. La charge `0x04` d'E20.d ne touche rien sans E20.c1. E20.e a besoin des
  boîtes d'E20.c1 pour couper l'herbe. E20.f a besoin d'E20.c1 et d'E20.e. E20.h a besoin d'E20.g
  pour poser la bombe, si la découverte de la tranche le confirme.
- **Mise à jour de ce plan** : chaque tranche remet au §0.2 ce qu'elle a mesuré, puis fait détailler,
  relire et approuver la suivante. Une tranche qui touche le convertisseur fait un export complet.

### 1.2 E20.0 — Découverte complémentaire ⏳

**But.** Lever les inconnues qui changeraient E20.a à E20.c2, et construire les oracles qui
serviront de preuve aux tranches de code.

**Périmètre.** En lecture seule sur le code : rien ne change dans `Alundra/`, `Alundra.Tests/`, le
convertisseur, le moteur ni l'export. Écritures permises : `docs/plan-e20-annexe/` (notes, scripts des
oracles, sorties), ce plan, le plan maître et un ADR si une décision est prise.

**Hors périmètre d'E20.0.** Tout code de production ; les tranches E20.d à E20.h au-delà de ce qui
change leur découpage.

#### Tâches

- ⏳ **D1 — Oracle de `MovePlayer`.**
  - Copier dans `docs/plan-e20-annexe/tools/` les outils de `docs/plan-e19-h2-annexe/tools/` et
    `docs/plan-e19-o9-annexe/run_update_animation.py`, sans modifier les annexes d'E19. Ce dernier
    importe `emu` d'un dossier `lib/` absent de son annexe : la copie le fait pointer sur le `emu.py`
    copié d'E19.h2.
  - Passes du tick `0x8003B388`, dans l'ordre du binaire :

    | Passe | Dans D1 | Règle |
    |---|---|---|
    | `0x80038634` | à décoder d'abord | vrai code s'il lit ou écrit la case du héros, sinon bouchon sans effet ; le choix et sa preuve vont dans l'annexe |
    | `0x800386D0`, événements | vrai code, pour le héros seul | `MovePlayer` `0x80031B50` et tous ses appelés en vrai code (`PlayerTryAttack`, `TryUseItem`, `PlayerTryAction`, `CheckEntityInteraction`, `0x8002F49C`, `0x8002F768`, `0x8002F884`, `0x80031A68`, `CreatePlayerAnimationEffects`) ; aucune autre entité dans les scénarios de D1 |
    | `0x80038998`, compteurs | vrai code | toutes les cases |
    | `0x800384F4`, listes | vrai code | comme `zsim.py` |
    | `0x80038E18`, animation | vrai code | `0x80038AB4` sur la case du héros, avec des octets de sprite construits depuis la banque décodée du héros (celle que lit `SpriteBankReader` pour `alundra_0` : fiches d'animation de 14 octets, images de 5 octets, enregistrements de collision), posés à la disposition lue dans `docs/plan-e19-o9-annexe/dis_update_animation.txt` (`+0x64` : table des animations, images, collisions, jeux d'images) |
    | `0x80038364`, physique | vrai code | comme `zsim.py` (il lit aussi la vitesse et l'accélération de la fiche d'animation), sur le sol du scénario (voir la validation) |
    | `0x80038E84`, effets | bouchon sans effet | — |
    | `0x80039300`, coups | bouchon sans effet | aucune cible dans D1 ; le vrai code sert dans D2 |

  - Appels sortants bouchonnés, chacun journalisé : son `0x800490FC` (note l'identifiant) ; effets
    `0x8003BDD8` et `0x8003BE74` (notent l'identifiant et les arguments, rendent l'adresse d'une case
    d'effet factice remise à zéro) ; apparition `0x80039F58` (note, rend 0). Le générateur
    pseudo-aléatoire du binaire (`0x800A8708`) tourne en vrai code, avec une graine notée.
  - Pad : le scénario donne le mot tenu de chaque tick. Le harnais écrit tenu, appuyé et relâché en
    `0x80126F28`, `0x80126F2A` et `0x80126F2C`, calculés comme `AlundraTickPad.Update`
    (`AlundraTickPad.cs:56-58`) ; D1 relit la routine de pad du binaire et note tout écart.
    `[0x800DC4B8]` vaut 0.
  - Case du héros : les champs de physique posés comme dans `zsim.py`, plus les pointeurs de sprite
    (`+0x64`) et de fiche d'équilibre (`+0x1C4`, et `0x80127008` pour le héros). Valeurs de départ, pour
    que `MovePlayer` ne prenne aucune sortie anticipée : PV `+0x14` = 10 et PV max = 10, dans le
    champ que lit `SetPlayerHpMax` à la fin de `MovePlayer` (`0x80032920`) (nouvelle partie) ; `+0x20` = 0 ; `+0x180` et `+0x188` = 0 ; `+0x224` (`TouchingEntity`) et `+0x1CC` = 0 ;
    `[0x80127000]` = 0 ; arme : `WeaponId` (`+0xA` des statistiques pointées par `0x80176318`) = 1,
    l'épée de base ; statut 2.
  - Sortie : une trace par tick (animation courante et cible, direction, ForceX, ForceY, ForceZ, PosX,
    PosY, PosZ, IsOnGround, `+0xB0`, `+0x1C`, compteurs `0x80126FF4` et `0x80126FFC`).
  - **Validation de l'oracle** : les scénarios S-A et S-B de `docs/plan-e19-d2c-valeurs.md` §B.1,
    mesurés sur le vrai code.
    - Sol : un champ de cases synthétique et plat, de hauteur 0 et sans propriété (la fonction de
      cases de `zsim.py`), assez grand pour les 56 px que S-B parcourt vers l'est ; en-tête de carte
      avec les constantes de la carte 10, gravité 128 et ZViscosity 4096.
    - S-A : héros au repos, Idle (`+0x88` = 0), forces nulles, posé au sol (PosZ = 1, convention du
      binaire). Le premier tick comparé est le tick 1, celui du front de Croix ; aucune autre touche.
    - S-B : même départ, puis des ticks de chauffe avec Droite tenue, jusqu'à deux ticks de suite en
      Moving avec ForceX = 159744 ; le premier tick comparé est le tick suivant, celui du front de
      Croix, Droite toujours tenue. Les positions de départ sont prises à la fin de la chauffe.
    - À chacun des 24 ticks comparés, l'oracle doit donner exactement l'animation courante, ForceX,
      ForceY, ForceZ, PosX − départ, PosY − départ et IsOnGround de l'annexe, et le PosZ de l'annexe
      plus 1 (l'annexe suit la convention de la DLL, sans le +1 du binaire, D-E19-59). Un seul écart
      fait échouer D1.
- ⏳ **D2 — Oracle de la passe de coups** : `0x80039300`, `0x8003A374` et `0x8004464C` en vrai code
  sur des ensembles d'entités synthétiques, tirés au hasard avec une graine notée. Les listes sont
  construites par `0x800384F4` en vrai code ; les fiches d'équilibre viennent de `Data/balance.json`.
  - **Validation de l'oracle**, sur des cas écrits à la main, dont le résultat attendu vient du §0.2.5 :
    - masque réordonné : un attaquant qui n'a que `HitsClassA` (`EntityFlags.cs:53`) touche une cible
      `ClassA` et pas une cible `ClassB` ; un attaquant qui n'a que `HitsClassB` touche `ClassB` et pas
      `ClassA` ;
    - recouvrement, en unités brutes 16.16 (le binaire compare `d = boîte de l'attaquant − ModdedPos
      de la cible` ; `d ≥ 0` demande `d < taille de la cible + 1`, `d < 0` demande `−d < taille de la
      boîte de l'attaquant + 1`, et une taille de n px est rangée `(n << 16) − 1`) : sur un axe, avec
      une cible de n px, `d = (n << 16) − 1` touche et `d = n << 16` ne touche pas ; avec une boîte
      d'attaquant de m px, `d = −((m << 16) − 1)` touche et `d = −(m << 16)` ne touche pas ;
    - réponse `0x80` : pas de `TouchingEntity`, mais `+0x1D0 = 0x19` sur la cible et `HitCounter` +1
      sur l'attaquant ; réponse `0xC0` : `TouchingEntity` posé, puis PV inchangés par `ComputeNewHp` ;
    - dégât minimal : quand puissance × réponse >> 4 vaut 0, la cible perd quand même 1 PV ;
    - premier attaquant : deux attaquants sur la même cible au même tick ; seul le premier de la liste
      la touche, et le `HitCounter` du second reste à 0 ;
    - plusieurs cibles : un attaquant sur deux cibles les touche toutes les deux, `HitCounter` 2.
  - Un seul cas faux fait échouer D2.
- ⏳ **D3 — Dégâts au contact.** Pour chaque ennemi des cartes 14, 15, 115 à 117, 44 et 362, et en
  total sur le corpus : quelles animations ont une boîte et une attaque non nulle, donc blessent le
  héros par simple contact une fois E20.c1 faite. Le résultat dit ce qu'E20.c1 change sur les cartes
  que l'auteur traverse déjà.
- ⏳ **D4 — Puissance de l'équipement** : dans le binaire, `UpdateItemEffectState` (`0x800307E8`),
  quels objets remplissent les trois fiches de `0x80146E00`, et la part de l'épée de base, de la veste
  et des bottes.
- ⏳ **D5 — `0x8003634C`** : ce que le chemin des dégâts annule.
- ⏳ **D6 — Boîtes par image (D-E20-6)**, sur tout le corpus.
  - Pour chaque banque de sprites des données décodées (`data-extracted/data`, celles que lit
    `SpriteBankReader`) et chaque image qui a un enregistrement de collision : retrouver, depuis les
    `collision_keyframes` exportées, les six valeurs (décalages X, Y et Z ; largeur, profondeur et
    hauteur) et les comparer à l'enregistrement décodé.
  - Contrôler aussi, sur tout le corpus, ce que le binaire suppose : des décalages signés sur un
    octet et des tailles non signées sur un octet. L'index de collision `0xFFFF` d'une image (« pas de
    boîte ») se contrôle s'il reste visible dans les données décodées ; si l'extracteur le rend déjà
    comme une image sans collision, D6 le note comme non vérifiable.
  - Dire comment la DLL connaît l'image courante à un tick logique (horloge d'E19.c2).
  - Sortie : par banque, le nombre d'images comparées et la liste des écarts, ou zéro.
  - Verdict : exact sur tout le corpus, ou non. Si non, décrire l'export à ajouter ; la décision de
    format passe par un ADR avant E20.c1.
  - Limite : D6 compare l'export aux enregistrements décodés par l'extracteur, sans relire les octets
    du disque. Un défaut de l'extracteur passerait inaperçu ; les recettes d'E20.c1 restent le filet.
- ⏳ **D7 — Inventaire du tick de la DLL (D-E20-2).**
  - L'ordre réel d'un tick dans la DLL : la boucle de ticks de chaque entité
    (`AlundraEntityScriptProxy.cs:1312-1325`), les ticks du héros (`:1399-1426`), le tick du monde, les
    événements de carte, la mise à jour d'`AlundraTickPad`, les passes du dialogue et de l'inventaire
    (ADR-0050) ; pour des images de 0, 1, et 2 ticks ou plus.
  - Tout ce qui suppose `MovePlayer` par image : `JumpStartTickStamp` et le cachet du saut, les
    lecteurs de `LastPadState`, les fronts de Carré lus par l'interaction et les dialogues, les
    premières images (O-E19-80).
  - Les tests qui épinglent un effet du rythme par image (par exemple SJ-6b), chacun avec la valeur
    que le binaire donne.
  - Une proposition de place, rapportée à l'ordre du binaire (§0.2.1), pour `MovePlayer`, la passe des
    compteurs et la passe de coups, ces deux dernières communes à toutes les entités et une fois par
    tick ; et, pour l'inversion entités / événements de carte, ce que changerait sa correction dans
    E20.a et ce que coûterait de la garder comme écart documenté. L'auteur tranche avec le plan
    d'E20.a.
  - Livrable : le contrat d'E20.a, prêt à relire.
- ⏳ **D8 — Règle d'arrondi du stick (D-E20-8)** : relire le chemin d'entrée
  (`AlundraPlayerController`, `ComputePadState`) et proposer une règle exacte pour le départ et la
  reprise de la course au stick ; elle est soumise à l'auteur.
- ⏳ **D9 — Propriétaires restants** : butin et objet ramassable (O-E20-3), projectiles du héros
  (O-E20-4), nage et wagonnet (O-E20-1) ; ce que chacun demande, pour que l'auteur tranche.

#### Acceptation

- Chaque tâche rend ses faits étiquetés comme au §0.2, avec les adresses ; chaque fait porteur venu du
  binaire est recalculé par un second agent qui cherche à le réfuter.
- D1 : les deux scénarios de validation sont égaux à l'annexe à chaque tick, champ par champ ; sinon,
  D1 est déclaré échoué, et les tranches de code gardent les oracles fonction par fonction de la
  découverte.
- D2 : les six cas de validation donnent le résultat attendu ; sinon, D2 est déclaré échoué, et E20.c1
  ne peut pas s'en servir comme preuve.
- D6 : le décompte par banque couvre tout le corpus, avec ses écarts.
- D7 : la proposition couvre `MovePlayer`, la passe des compteurs, la passe de coups et l'inversion
  entités / événements de carte.
- Une critique de complétude passe sur l'ensemble.
- Le §0.2 de ce plan est mis à jour, et E20.a est détaillée puis soumise au plan-verifier avant
  l'auteur.
- `git status` ne montre aucun changement hors de `docs/`.

#### Budget, arrêts, retour arrière

- Un workflow en lecture seule d'au plus dix agents pour D2 à D9, et le travail d'outil de D1.
- Arrêt : D1 qui ne reproduit pas S-A et S-B, ou D2 qui rate un de ses cas, après deux tentatives ; une décision qui contredit D-E20-1 à
  D-E20-10 ; un besoin d'écrire hors de `docs/`.
- Retour arrière : `git revert` des commits de documentation.

---

## 2. Points ouverts

| Point | Question | Où elle se tranche |
|---|---|---|
| O-E20-1 | La nage (pente 4) et le wagonnet : le plan d'E19 les donnait à E14 ; l'auteur ne s'est pas encore prononcé | E20.0, D9 |
| O-E20-2 | La règle exacte de l'arrondi du stick (D-E20-8) | E20.0, D8, puis l'auteur |
| O-E20-3 | Le butin et l'objet ramassable (`0x80032968`, `0x80032B90`, `0x80032C7C`, gestionnaire C255) servent aussi aux ennemis et aux coffres : E20.e ou E14 | E20.0, D9, puis l'auteur |
| O-E20-4 | Les gestionnaires natifs des projectiles du héros (flèches, boules de feu et de glace, vague de l'épée, bombe) : E20.f ou E14 | E20.0, D9, puis l'auteur |
| O-E20-5 | La barricade de la 392 : l'original la franchit-il en soulevant ou en grimpant ? | E20.g |
| O-E20-6 | Une fois E18 faite, D-E20-5 tombe ; le prérequis SC8 d'E18 (PV ≥ 1 au chargement) reste à trancher | E18 |

## 3. Hors périmètre

- L'IA native des ennemis, des PNJ et des boss (E14), dont les Muruta et les Beannoïde (E14.a,
  D-E20-4) et leurs morts et butins propres.
- La mort, « Réessayer », `0xBB`, l'objet `0x27` et Start+Select (E18).
- La caméra : aucune dépendance à la course trouvée dans la décompilation, non vérifiée dans le
  binaire.

## 4. Arrêts, budgets et retours arrière

- **Budgets** : au plus deux tentatives par tranche au même niveau d'exécutant, jamais une troisième
  identique ; au plus cinq passes de correction et re-vérification par tranche, chacune sur un état
  réellement changé.
- **Arrêts** :
  - un fait mesuré qui contredit une décision D-E20 ou un fait marqué [binaire] ;
  - un arc de la chaîne ou un point épinglé de l'intro qui bouge pour une autre raison que la tranche ;
  - une régression des premières images du vrai jeu (sonde d'E19.v2) ;
  - un test existant qui devrait changer sans justification du binaire ;
  - une modification de l'auteur indexée par erreur ; un commit sur `main` ; un push ;
  - tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Retours arrière** :
  - DLL et tests : abandon de la branche ; rien n'est fusionné sans l'auteur ;
  - convertisseur : abandon de la branche et nouvel export complet ;
  - docs et ADR : `git revert` de leur commit.
