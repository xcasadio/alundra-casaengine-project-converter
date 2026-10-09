# Plan — E20, le héros : course, attaque et coups

Étape E20 de [plan-conversion-totale.md](plan-conversion-totale.md). Elle porte ce que le héros fait
de ses boutons d'action : la course de Triangle, l'attaque de Carré, porter et lancer, les objets du
Rond. Elle porte aussi la passe de coups commune à tout le jeu, les dégâts, et la réaction du héros
quand il est touché. E14 garde l'IA native des ennemis et des PNJ.

**Statut** : proposé le 2026-10-09, sur la branche `chantier/e20-heros` (base : §0.3), avec l'enveloppe (§1.1) et
la première tranche exécutable, E20.0 (§1.2). Les tranches suivantes se planifient et s'approuvent
chacune à part, comme en E19. Décisions de l'auteur : §0.1, ADR-0052 et ADR-0053.

**Avancement (2026-10-09)** : enveloppe et E20.0 approuvées par l'auteur (mode ASK) ; E19 fusionnée dans `main`
(parent `8e023d8`, moteur `b2205d9f`), qui est donc la base de la branche. E20.0 : D1 à D9 faites et contre-vérifiées
(§0.2.10). Réponses de l'auteur aux questions de la découverte : D-E20-11 à D-E20-18. E20.a (§1.3), approuvée par l'auteur,
est faite et CONFIRMED par un verifier frais le 2026-10-09 ; reste sa recette en jeu. Sur la demande de l'auteur (« enchaîne
directement »), les tranches suivantes s'enchaînent sans lui être soumises : E20.b est détaillée au §1.4.

| Unité | Revue avant approbation | Après exécution |
|---|---|---|
| Enveloppe (ce plan) + E20.0 | plan-verifier | contre-vérification indépendante des faits, critique de complétude |
| E20.a à E20.i | plan-verifier, tranche par tranche | verifier frais |

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

**Révision 3 (2026-10-09).** Après les réponses de l'auteur (D-E20-11 à D-E20-18) : enveloppe **REVISE** (la ligne d'E20.a
n'avait pas suivi les décisions ; les prérequis d'E20.e0, E20.h et E20.i manquaient ; le wagonnet avait un propriétaire que
l'auteur n'a pas nommé) et E20.a **REVISE** (aucune preuve que le rendu de l'image reste à jour ; aucune règle pour les entités
qui apparaissent en cours d'image). Tous corrigés : ligne d'E20.a, revue étendue à E20.i, recette d'E20.d sans la nage,
prérequis par tranche, wagonnet sans propriétaire (§3), deux tests de plus dans A8, règle d'appartenance au tick, O-E20-11.
Relectures neuves de la révision 3 : enveloppe **READY**, E20.a **READY**. E20.a attend l'approbation de l'auteur.

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

Après E20.0, l'auteur a répondu le 2026-10-09 aux questions O-E20-1 à O-E20-4 et O-E20-7 à O-E20-10 (« 1 a, 2 a, 3 a, 4a,
5 oui, 6 a, 7 a, 8 a ») :

- **D-E20-11** — **L'ordre des entités et des événements de carte suit le binaire** : à chaque tick, les événements de carte
  passent avant le bloc des entités. E20.a le corrige, après avoir mesuré les épingles qui bougent. Cette décision remplace
  l'écart d'un tick que l'auteur avait accepté pour la boîte de texte (D-E19-64, ADR-0029).
- **D-E20-12** — **La DLL pilote elle-même une boucle de ticks dans l'ordre du binaire** ; pas de pas fixe dans le moteur (la
  migration M3 reste différée).
- **D-E20-13** — **La passe des compteurs n'efface pas `RidingEntity`** tant que la DLL ne recalcule le porteur qu'une fois par
  image. Écart documenté.
- **D-E20-14** — **`UpdateItemEffectState` et le pilote `0x8003634C`** sont des bouchons dans E20.a et sont portés dans E20.c2.
- **D-E20-15** — **Règle du stick** (précise D-E20-8) : seulement au départ et à la reprise de la course, quand le clavier et la
  croix ne donnent aucune direction et que le stick donne une diagonale, la course prend l'axe dominant du stick ; à moins de ε
  de 45°, elle garde l'axe du regard actuel. ε = 1/16, à régler en E20.d d'après une mesure du stick. La marche ne change pas.
- **D-E20-16** — **La nage forme la tranche E20.i** ; le wagonnet reste hors d'E20 (il n'a rien dans `MovePlayer`). Il n'a pas
  de propriétaire : l'auteur le désignera plus tard.
- **D-E20-17** — **Une tranche dédiée, E20.e0, avant E20.e** : l'apparition d'une entité par indice de sprite global
  (`SpawnWarpEntity`), le butin et l'objet ramassable. Elle sert aussi à E14.
- **D-E20-18** — **Projectiles partagés** : E20.f porte l'apparition et les gestionnaires propres au héros (vague de l'épée C61,
  flèches chargées C94) ; la bombe (E12) va en E20.h ; les gestionnaires que des ennemis partagent peut-être (flèches E17,
  glace C60, feu C54) vont à E14.

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
  (`0x800307E8`) ; le détail, lu dans le binaire, est au §0.2.10.
- [données] Seuil d'équilibre : la fiche d'un ennemi se choisit par l'octet `+0xB` de l'en-tête de
  carte (`0x8002C39C`, `0x80044550`) [binaire]. Le champ exporté `BalanceLevel` est l'octet `+9` (2 sur
  toutes les cartes, l'effet de bris des tuiles) ; le vrai seuil est exporté sous `D` (0 à 11).
- [binaire] Le héros touché (`0x80031A68`) : avec un `TouchingEntity`, `ComputeNewHp(héros)` et la
  direction du toucheur vers le héros ; sinon, si `+0x1CC == 0` et `[0x80127000] < 3`, les cases
  dangereuses coûtent 0 à 3 PV (table `0x80028B8C`), sans armure. Puis `0x31` (`0x3A` sur pente 4),
  `+0x1CC = 0x78`, objet porté lâché (`0x8002F884(2)`), appel de `0x8003634C` (pilote de l'objet en cours d'usage, §0.2.10).
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

#### 0.2.10 Mesures d'E20.0 (2026-10-09)

Source : `docs/plan-e20-annexe/` (`d1-oracle-moveplayer/`, `d2-oracle-coups/`, `d6-boites/`, `e20-0-rapports/`). Chaque fait porteur
venu du binaire a été recalculé par un second agent ; les corrections de ces contre-vérifications sont prises ici.

- **Oracles.**
  - [binaire] D1 : l'oracle de `MovePlayer` (vrai code, toutes les passes du tick sauf les effets et les coups) redonne S-A et S-B
    champ par champ, 216 valeurs chacun, 0 écart, recontrôlé par un second interpréteur et par des mutations. Limites : l'annexe de
    référence vient elle-même d'une émulation du même binaire (E19.d2c) ; seul le saut est validé.
  - [binaire] D2 : l'oracle de la passe de coups passe ses six cas de validation, 11 cas de plus, 3000 ensembles tirés au hasard
    contre un modèle indépendant, et 13 mutations.
  - [données] D6 : les `collision_keyframes` exportées redonnent exactement les enregistrements décodés, 30 585 images sur 30 585,
    sur tout le corpus ; les 21 161 images sans boîte n'en ont pas non plus dans l'export. **D-E20-6 est tranchée : la DLL lit
    l'export, sans changement du convertisseur.** Limite : D6 ne relit pas les octets du disque. Non prouvé : la correspondance
    au tick d'un changement d'animation (la DLL réserve ce tick ; le binaire montre déjà l'image 0 de la nouvelle animation) et
    d'un changement de direction (le binaire relance l'animation) ; c'est à E20.a et E20.c1.
- **Rythme.**
  - [binaire] Une image du jeu : rendu, lecture du pad (`0x8002BB5C`, routine `0x8002E250`, égale à `AlundraTickPad.Update`),
    `UpdateWorld` (`0x8002BC4C`), déclencheur du menu, puis une avance du générateur pseudo-aléatoire (`0x8002BD14`). Le pad
    avance donc une fois par tick, juste avant `UpdateWorld`.
  - [binaire] `UpdateWorld` (`0x8002E058`) : événements de carte (`0x8003C67C`), puis entités (`0x8003B388`), puis la réserve
    d'effets animés (`0x8003C410`, 128 fiches à `0x80138608`).
  - [binaire] Le masque `0x48` saute les événements de carte et toute mise à jour des entités sauf les listes ; caméra et dessin
    continuent.
  - [binaire] `0x80038634` précède les événements et tient le nombre de cases ; la passe des compteurs (`0x80038998`) couvre les
    cases 0 à `[0x80134360]` sans filtre de statut. Dans `0x800386D0`, les entités commencent à la case 1, et la passe se répète
    tant qu'un gestionnaire a tourné.
- **Générateur pseudo-aléatoire** [binaire] : son état est `0x80098708` (valeur de l'image `0xB017C93D`), pas `0x800A8708` ;
  corrigé dans D1, D2 et ce plan. Il avance une fois par image après `UpdateWorld`, en plus de chaque tirage (environ 600 sites).
- **Équipement** [binaire] (`UpdateItemEffectState`, `0x800307E8`, appelé à chaque tick par `MovePlayer` en `0x80032914`, suivi de
  `SetPlayerHpMax` et `SetPlayerHp`) :
  - fiche [0] : l'armure (catégorie 2) possédée de plus haut numéro ; fiche [1] : l'arme équipée (catégorie 1) ; fiche [2] : l'objet
    équipé (catégorie 3), avec réécriture d'`ItemId` (`0x8004E51C`) ;
  - recopie 255 octets de la fiche [0] (ou de `+0x1C4`) dans la table de défense du héros `0x80127008`, combine les 11 réponses des
    trois fiches, pose le niveau des bottes dans `[0x80127000]` (0 en nouvelle partie), et fait la régénération, d'une période de
    `MaxHp + 1` ticks (le binaire teste `MaxHp < compteur`, la décompilation `<=`) ;
  - kit de la nouvelle partie : l'épée de base ajoute 1 de puissance aux seules attaques qui ont `attr & 0x80` ; la veste en tissu
    donne des réponses 0x10 (dégâts × 1) ; les bottes normales ne comptent pas.
- **`0x8003634C`** [binaire] : il fait avancer, à chaque tick, l'objet en cours d'usage (`[0x80127164]` : graines, cape, bombe,
  livres, rouleaux) et rend 1 sans rien faire quand aucun objet n'est en cours. Le chemin des dégâts le fait seulement avancer d'un
  tick ; l'annulation est `0x800350C0`, appelée par d'autres chemins. Second appel à chaque tick normal (`0x80031CC4`).
- **Coups, précisions** [binaire] :
  - les effets 4 et 5 ne s'attachent que si la réponse n'est pas `0x80` ; une réponse 0 n'est pas une immunité (1 PV au moins) ;
    `0x40` (tué net) apparaît 1058 fois dans le corpus ;
  - la passe de coups lit la réponse du héros dans `+0x1C4` (fiche 0, jamais immunisée), `ComputeNewHp(héros)` dans `0x80127008` ;
  - le héros, comme cible, n'est que de classe B : seuls les attaquants qui ont `HitsClassB` l'atteignent ;
  - `+0x1CC = 0x78` après un coup exclut le héros des cibles pendant 120 ticks : c'est son invulnérabilité ; `+0x1D0 = 0x19` ne
    bloque que 25 ticks entre deux coups ;
  - `ComputeNewHp` lit l'attaque de l'attaquant au moment de l'appel, au tick N+1 ; des gestionnaires natifs d'ennemis lisent aussi
    le `TouchingEntity` du héros (E14) ;
  - un délai d'image nul fige l'image jusqu'au changement d'animation (`0x80038BE0`).
- **Dégâts au contact** [données, binaire] : 2600 enregistrements de 211 cartes blessent le héros par simple contact une fois
  E20.c1 faite, dès l'attente ou la marche pour 96,6 % d'entre eux. Carte 14 : 16 sur 25 (Beannoïde, guêpes, poissons, ronces,
  rochers) ; carte 15 : 8 sur 25 (orques, ronces, Muruta). E20.c1 seule montre des étincelles au contact, sans perte de PV ; la
  perte vient avec E20.c2.
- **Tick de la DLL** [DLL] : le moteur met à jour chaque entité avec tous ses ticks de l'image, puis le monde
  (`World.cs:517-585`) ; `MovePlayer` passe une fois par image même sans tick ; les sondes `Slope_18c` et `FloorHeight` du héros
  sont rafraîchies une fois par image ; 433 appels `.Update(` dans 72 fichiers de tests dépendent de cette forme. Proposition de
  D7 : une boucle de ticks pilotée par le monde, dans l'ordre du binaire, autour d'un corps de tick par entité partagé
  (`e20-0-rapports/D7-tick-de-la-dll.md`, avec un brouillon de contrat d'E20.a que la critique juge incomplet).
- **Course au stick** [binaire] : seules les consultations du départ (`0x80031FD0`) et de la reprise (`0x8003219C`) refusent un
  stick près d'une diagonale ; le cône de la course accepte déjà les deux diagonales voisines.
- **Nage** [binaire] : la branche de pente 4 (`0x8003260C`) lit le front de bouton (`0x80126F2A`) pour plonger (`& 0xD0` donne
  `0x28`) ; la DLL fait aujourd'hui marcher le héros sur l'eau. 54 cartes ont de l'eau profonde.
- **Butin** [binaire] : tiré à l'apparition de l'entité (`0x80039D04` puis `0x80032A40`, résultat en `+0x3C`), lâché à sa mort
  (`0x8003A59C` puis `0x80032B90`), avec un veto au lâcher pour les objets `0x26`, `0x51` et `0x52`. `SpawnWarpEntity` a 102
  appelants.

---

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Preuve | Recette en jeu |
|---|---|---|---|
| **E20.0** (§1.2) | Découverte complémentaire, sans toucher au code : oracles d'émulation de `MovePlayer` et de la passe de coups, dégâts au contact des ennemis, puissance de l'équipement, `0x8003634C`, preuve des boîtes exportées (D-E20-6), inventaire du passage par tick (D-E20-2), règle du stick (D-E20-8) | recalcul indépendant, contre-vérification du binaire, critique | — |
| E20.a (§1.3) | Le tick de la DLL suit le binaire, dont E20.a est propriétaire : le monde pilote la boucle des ticks (D-E20-12) ; à chaque tick, le pad, les passes de la boîte, les événements de carte, puis le bloc des entités, l'inversion étant corrigée après la mesure A0 (D-E20-11) ; `MovePlayer` à chaque tick sur `AlundraTickPad` (D-E20-2) ; passe des compteurs (`+0x1C`, effacements, décomptes, `RidingEntity` gardé, D-E20-13) ; point d'accroche de la passe de coups (vide jusqu'à E20.c1) ; les trois appels dans l'ordre du binaire, les appels du prologue, `UpdateItemEffectState` et `0x8003634C` en bouchons (D-E20-14) ; O-E19-80 ; retrait des contournements du rythme par image | tests de structure (0, 1 et 2 ticks par image, coup écrit au tick N lu au tick N+1 puis effacé, rendu de l'image à jour, entités apparues en cours d'image) ; oracle D1 ; épingles justifiées ; traces re-mesurées ; sonde du vrai jeu | Marche, saut, échelle, dialogues, inventaire, livre : rien ne change, sauf le décalage d'un tick entre les entités et les événements de carte (D-E20-11) |
| E20.b | Coup d'épée sans dégâts : table des armes en entier, `PlayerTryAttack` (relâchement d'abord, puis appui), garde `0x2000`, son 3, attaques au sol et en l'air, états d'attaque, priorités (Carré sur Croix, interaction d'abord) | oracle de `MovePlayer` ; arcs au vrai préfab | Carré frappe au sol et en l'air, avec le son ; le héros freine |
| E20.c1a (§1.5) | La passe de coups : fiches d'équilibre (seuil `D`), PV des entités, `CurrentAttack` et boîte de l'image au tick (D-E20-6, règle de D6), `HitBox`, liste des cibles, `UpdateBalanceRecords` au point d'accroche d'E20.a (immunité, premier attaquant, plusieurs cibles), étincelles (effet 9) ; les effets 4 et 5 vont à E20.f | oracle de la passe avec le héros et un vrai Beannoïde (`e20-c1/oracle/`) ; les six cas de D2 ; un arc au vrai préfab sur la 14 | Un coup sur un Beannoïde de la 14 : étincelles ; au contact d'un ennemi, étincelles toutes les 25 ticks, sans perte de PV |
| E20.c1b | Les conséquences : `ComputeNewHp` et `ResolveBalanceTarget` (fonctions pures, oracle D2), les trois fiches d'équipement (la partie d'`UpdateItemEffectState` dont dépendent les dégâts), `0x86`, `0x87`, `0x95`, `0x71`, les réactions scriptées (leviers de la 115, de la 116, bascule de la 117, leviers de la 44) | oracle D2 ; arcs des leviers posés directement | Les leviers réagissent au coup d'épée (quand on peut les atteindre : après le hall de la 115) |
| *E14.a* (autre étape) | Muruta et Beannoïde (D-E20-4), planifiée après E20.c1 | plan d'E14 | Le hall de la 115 |
| E20.c2 | Le héros touché : entrée `0x80031A68` (toucheur et cases dangereuses), `0x31`/`0x3A`, invulnérabilité `0x78` et clignotement, PV vers `PlayerStats` et le HUD, `UpdateItemEffectState` (table de défense, bottes, régénération) et le pilote `0x8003634C` en vrai (D-E20-14), chemin `controlFlags == 0x20`, pas de mort (D-E20-5) | oracle de `0x80031A68` ; arcs | Les cœurs baissent au contact, le héros clignote, il ne meurt pas |
| E20.d | Course : les cinq états, entrée et sorties, cône, reprise, rebond, charge `0x04` (ses coups viennent d'E20.c1), escalade prioritaire, arrondi du stick (D-E20-8) ; le test épinglé « Triangle seul donne Idle » s'inverse | oracle de `MovePlayer` ; arcs | Courir, s'arrêter, reprendre, foncer, buter contre un mur, sauter d'un rebord ; sur l'eau profonde, le héros marche encore (la nage est E20.i) |
| E20.e0 | Apparition d'entités et butin (D-E20-17) : `SpawnWarpEntity` (`0x80039F58`, 102 appelants) par indice de sprite global, tirage du butin à l'apparition (`0x80032A40`), lâcher à la mort (`0x80032B90`) avec son veto, objet ramassable (gestionnaire C255), avance du générateur pseudo-aléatoire une fois par tick après la mise à jour du monde (`0x8002BD14`) | oracle ; arcs | Rien de visible seul ; recette avec E20.e |
| E20.e | Effets du héros et herbe coupée : `CreatePlayerAnimationEffects` (pas, poussière de marche, de course et d'atterrissage), `CheckAndTriggerTileEffect`, butin tiré par E20.e0 | oracle ; arcs | Pas et poussière ; l'herbe se coupe à l'épée et à la charge ; argent et cœurs tombent et se ramassent |
| E20.f | Autres armes (D-E20-7) : charge et aura, fléau et rebond, baguettes, arcs, projectiles par l'apparition d'E20.e0 et les gestionnaires propres au héros, vague de l'épée C61 et flèches chargées C94 (D-E20-18), `0xC0`/`0xC1` | oracle ; arcs | Sauvegarde de test avec les armes (à créer) |
| E20.g | Porter, poser, lancer (`PlayerTryAction`, `UpdatePlayerCarriedEntity`) ; caisses et jarres redeviennent des obstacles (fin de l'écart D-E19-28/D-E19-46) ; barricade de la 392 (O-E20-5) | oracle ; arcs | Soulever et lancer une caisse ; la 392 |
| E20.h | Objets du Rond (`TryUseItem`) : graines, bombes (gestionnaire E12), magie, potions ; le couvercle de la 362 | oracle ; arcs | La 362 |
| E20.i | Nage (D-E20-16) : branche de pente 4 (`0x8003260C`), états `0x0F`, `0x1D`, `0x28`, `0x3A` et `0x3B`, plongée au front de bouton | oracle ; arcs | Nager et plonger en eau profonde (la 416 en a) |

- **Ordre** : E20.0 → E20.a → E20.b → E20.c1a → E20.c1b → E20.c2 → E20.d → E20.e0 → E20.e → E20.f → E20.g → E20.h → E20.i.
  E14.a se planifie dès qu'E20.c1b est faite (ses gestionnaires appellent `ComputeNewHp`). Le hall de la 115 devient jouable avec
  E20.a, E20.b, E20.c1a, E20.c1b et E14.a. Découpage d'E20.c1 en c1a et c1b décidé le 2026-10-09 sur la proposition de la
  contre-vérification de la découverte (`docs/plan-e20-annexe/e20-c1/check.md`).
- **Dépendances** : tout dépend d'E20.a (le rythme par tick, le relâchement, et la structure du tick, dont
  E20.a est propriétaire ; E20.c1 branche la passe de coups à la place qu'E20.a a créée). E20.c1 dépend
  d'E20.0 (D6) pour les boîtes. La charge `0x04` d'E20.d ne touche rien sans E20.c1. E20.e a besoin des
  boîtes d'E20.c1 pour couper l'herbe et du butin d'E20.e0. E20.f a besoin d'E20.c1, d'E20.e0 et d'E20.e.
  - **E20.e0** : E20.a (le tick et l'avance du générateur une fois par tick) ; son lâcher à la mort ne se voit qu'avec des morts,
    donc avec E20.c1 et E14.
  - **E20.h** : E20.e0 (faire apparaître la bombe), E20.c1 (`0x87` et le couvercle de la 362), E20.e (la coupe de l'herbe par la
    bombe) et E20.g (poser ou lancer la bombe, si la découverte de la tranche le confirme).
  - **E20.i** : E20.a ; E20.c2 pour l'état blessé dans l'eau (`0x3A`).
- **Mise à jour de ce plan** : chaque tranche remet au §0.2 ce qu'elle a mesuré, puis fait détailler,
  relire et approuver la suivante. Une tranche qui touche le convertisseur fait un export complet.

### 1.2 E20.0 — Découverte complémentaire 🚧 (D1 à D9 faites et contre-vérifiées le 2026-10-09 ; reste le détail d'E20.a, après les réponses de l'auteur)

**But.** Lever les inconnues qui changeraient E20.a à E20.c2, et construire les oracles qui
serviront de preuve aux tranches de code.

**Périmètre.** En lecture seule sur le code : rien ne change dans `Alundra/`, `Alundra.Tests/`, le
convertisseur, le moteur ni l'export. Écritures permises : `docs/plan-e20-annexe/` (notes, scripts des
oracles, sorties), ce plan, le plan maître et un ADR si une décision est prise.

**Hors périmètre d'E20.0.** Tout code de production ; les tranches E20.d à E20.h au-delà de ce qui
change leur découpage.

#### Tâches

- ✅ **D1 — Oracle de `MovePlayer`.**
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
    pseudo-aléatoire du binaire (`0x80098708`) tourne en vrai code, avec une graine notée.
  - Pad : le scénario donne le mot tenu de chaque tick. Le harnais écrit tenu, appuyé et relâché en
    `0x80126F28`, `0x80126F2A` et `0x80126F2C`, calculés comme `AlundraTickPad.Update`
    (`AlundraTickPad.cs:56-58`) ; D1 relit la routine de pad du binaire et note tout écart.
    `[0x800DC4B8]` vaut 0.
  - Case du héros : les champs de physique posés comme dans `zsim.py`, plus les pointeurs de sprite
    (`+0x64`) et de fiche d'équilibre (`+0x1C4` ; `0x80127008` est la table de défense du héros, refaite à chaque tick, §0.2.10). Valeurs de départ, pour
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
- ✅ **D2 — Oracle de la passe de coups** : `0x80039300`, `0x8003A374` et `0x8004464C` en vrai code
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
- ✅ **D3 — Dégâts au contact.** Pour chaque ennemi des cartes 14, 15, 115 à 117, 44 et 362, et en
  total sur le corpus : quelles animations ont une boîte et une attaque non nulle, donc blessent le
  héros par simple contact une fois E20.c1 faite. Le résultat dit ce qu'E20.c1 change sur les cartes
  que l'auteur traverse déjà.
- ✅ **D4 — Puissance de l'équipement** : dans le binaire, `UpdateItemEffectState` (`0x800307E8`),
  quels objets remplissent les trois fiches de `0x80146E00`, et la part de l'épée de base, de la veste
  et des bottes.
- ✅ **D5 — `0x8003634C`** : ce que le chemin des dégâts annule.
- ✅ **D6 — Boîtes par image (D-E20-6)**, sur tout le corpus.
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
- ✅ **D7 — Inventaire du tick de la DLL (D-E20-2).**
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
- ✅ **D8 — Règle d'arrondi du stick (D-E20-8)** : relire le chemin d'entrée
  (`AlundraPlayerController`, `ComputePadState`) et proposer une règle exacte pour le départ et la
  reprise de la course au stick ; elle est soumise à l'auteur.
- ✅ **D9 — Propriétaires restants** : butin et objet ramassable (O-E20-3), projectiles du héros
  (O-E20-4), nage et wagonnet (O-E20-1) ; ce que chacun demande, pour que l'auteur tranche.

#### Résultats (2026-10-09)

Rapports et contre-vérifications : `docs/plan-e20-annexe/e20-0-rapports/` ; faits retenus : §0.2.10.

- **D1** ✅ : PASS sur S-A et S-B (216 valeurs chacun, 0 écart) ; `0x80038634` tourne en vrai code ; la routine de pad du binaire
  égale `AlundraTickPad.Update`. Outil : `docs/plan-e20-annexe/d1-oracle-moveplayer/` (`python -B tools/run_all.py`, code de sortie
  0 si PASS), et non `docs/plan-e20-annexe/tools/` : chaque oracle garde ses outils dans son dossier.
- **D2** ✅ : PASS sur les six cas ; `docs/plan-e20-annexe/d2-oracle-coups/`.
- **D3** ✅ : voir « Dégâts au contact » au §0.2.10.
- **D4, D5** ✅ : voir « Équipement » et « `0x8003634C` » au §0.2.10.
- **D6** ✅ : exact sur tout le corpus ; D-E20-6 tranchée ; `docs/plan-e20-annexe/d6-boites/`.
- **D7** ✅ : inventaire du tick et proposition de structure ; ses faits du binaire sont confirmés (une précision sur le masque
  `0x48`). Le brouillon de contrat d'E20.a doit encore fixer : le sort d'`UpdateItemEffectState` et de `0x8003634C`, le pad avancé
  juste avant le tick, la porte par tick, l'ordre des listes (premier attaquant), la place des effets, les contraintes de l'horloge
  d'animation transmises à E20.b et E20.c1, la comparaison de la trace par tick de la DLL à l'oracle D1, un plafond chiffré de
  réécritures de montages de test.
- **D8** ✅ : règle proposée au §2 (O-E20-2).
- **D9** ✅ : options au §2 (O-E20-1, O-E20-3, O-E20-4) ; détail corrigé sur la nage (front de bouton).

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

### 1.3 E20.a — Le tick de la DLL suit le binaire 🧪 (faite et CONFIRMED le 2026-10-09, `b68fefc` et `ce15e7a` ; reste la recette en jeu de l'auteur)

**But.** Chaque tick logique de la DLL suit l'ordre du binaire (§0.2.1, §0.2.10) : le pad, les passes de la boîte de dialogue et
de l'écran de sauvegarde (inchangées), les événements de carte, puis le bloc des entités (`MovePlayer`, les programmes des
entités, la passe des compteurs, les listes, l'horloge d'animation, la physique, le point d'accroche de la passe de coups), puis
le bloc de l'inventaire (inchangé). Le héros lit le pad du tick. Pour le joueur, rien ne change, sauf l'écart d'un tick qui
séparait les entités des événements de carte (D-E20-11).

**Décisions appliquées** : D-E20-2, D-E20-11, D-E20-12, D-E20-13, D-E20-14. **Faits porteurs** : §0.2.1, §0.2.2, §0.2.10 et
le rapport D7 (`docs/plan-e20-annexe/e20-0-rapports/D7-tick-de-la-dll.md`).

**Périmètre.** DLL `Alundra/Scripts/` : `AlundraWorldProxy.cs` (boucle des ticks), `AlundraEntityScriptProxy.cs` (corps de tick
par entité, branche du héros, mode autonome), `AlundraPlayerManager.cs`, `AlundraPlayerController.cs` (`AlundraPadState`),
`AlundraTickPad.cs`, `AlundraEventProgramRunner.cs` (`0x2F`, modes 0 et 1), `AlundraFrameSyncPasses.cs` si les synchronisations
de fin d'image changent de place. Tests `Alundra.Tests/` : montages, épingles justifiées, nouveaux tests. Traces
`docs/hero-trace-389-*.txt` et `docs/intro-*` re-mesurées. Ce plan et un ADR si le contrat d'exécution prend une décision que
l'ADR-0053 ne couvre pas. Ni le moteur, ni le convertisseur, ni l'export, ni `CasaEngine.Launcher/Program.cs`.

**Hors périmètre.** L'attaque, la course, l'usage d'objet (bouchons) ; la passe de coups (E20.c1) et l'entrée des dégâts du héros
(E20.c2) ; `UpdateItemEffectState` et le pilote `0x8003634C` (bouchons, D-E20-14) ; l'effacement de `RidingEntity` (D-E20-13) ;
la réserve d'effets (`_effectStage.RunFrame`, inchangée) ; la synchronisation d'animation par image (D-E19-13, gardée) ; le pas
fixe du moteur (D-E20-12) ; les modes 2 et suivants de `0x2F`.

#### Tâches

- ✅ **A0 — Mesure de l'inversion, avant tout commit de code.** Dans la copie de travail, faire passer les événements de carte avant
  le pas des entités, au plus près de ce qu'E20.a fera, sans rien d'autre ; lancer `Alundra.Tests` ; relever chaque test, épingle
  et point de trace (intro, arcs de la chaîne, `AlundraDialogueBoxOrderTests`, `AlundraAnimationSoundTests`) qui change, avec
  l'ancienne valeur, la nouvelle et la raison du binaire. Puis restaurer les seuls fichiers touchés (`git restore <fichier>`).
  Livrable : `docs/plan-e20-annexe/e20-a/a0-mesure.md`. Un changement sans raison du binaire est un arrêt.
- ✅ **A1 — Le pad du tick.** `AlundraPadState` gagne `ButtonsReleased` ; une construction depuis `AlundraTickPad` (tenu, appuyé,
  relâché) ; `MovePlayer` et `CheckEntityInteraction` lisent ce pad ; `0x2F` modes 0 et 1 lisent aussi le pad du tick (ils lisent
  aujourd'hui le front de l'image, une fois par tick, `AlundraEventProgramRunner.cs:1738-1764`).
- ✅ **A2 — Un corps de tick par entité, piloté par le monde.**
  - Extraire d'`AlundraEntityScriptProxy.Update` un corps de tick unique par entité : choix du programme, programme, horloge
    d'animation, mouvement et appui pour les entités ; `MovePlayer`, horloge d'animation, `Tick` et sondes pour le héros.
  - En production, le monde déroule les ticks (A3). L'entité garde, une fois par image, l'échantillon du pad (héros) et la
    récupération de la racine ; `SyncAnimation` et `SyncTransform` voient les ticks de l'image en cours : aucune image de retard
    visuel de plus qu'aujourd'hui. Le mécanisme est au choix de l'exécutant, décrit dans le code et dans ce plan.
  - Sans monde pilote (montages, faux hôtes), l'entité garde sa propre boucle de ticks, sur le même corps, avec `MovePlayer` à
    chaque tick : les montages existants continuent de marcher.
- ✅ **A3 — L'ordre du tick k dans le monde** (`AlundraWorldProxy.Update`), dans l'ordre du binaire :
  1. le pad (`TickPad.Update`, inchangé) ;
  2. la boîte de dialogue, le texte, le choix, l'écran de sauvegarde (inchangés, ADR-0029, ADR-0047) ;
  3. la porte (`GameplayGateNow`) ;
  4. héros présent et porte ouverte : les événements de carte, puis le recyclage (inchangés) ;
  5. **le bloc des entités**, porte ouverte : `MovePlayer` du héros sur le pad du tick (A1) ; les programmes des entités dans
     l'ordre des cases, et le ré-examen des déclencheurs en attente (`RunPendingEventTriggers`, déplacé de l'après-boucle dans le
     tick, comme la répétition de `0x800386D0`) ; la passe des compteurs (A5) ; les listes ; l'horloge d'animation de chaque
     entité ; la physique (héros : `Tick`, puis `UpdateVramFlags`, `UpdateGroundSlope` et `UpdateFloorHeight` dans le tick ; les
     autres : mouvement scripté et appui) ; un point d'accroche vide pour la passe de coups. Porte fermée : seules les listes
     tournent (`0x8003B408`) ;
  6. le bloc de l'inventaire (inchangé, ADR-0050).

  L'ordre des cases est celui de `_spawnedEntities`, le héros d'abord ; le « premier attaquant » d'E20.c1 en dépend.

  **Entités qui apparaissent ou disparaissent en cours d'image** : la règle d'aujourd'hui est gardée. `World.AddEntity` ne fait que
  mettre en file (`AlundraWorldProxy.cs:860-868`) ; une entité qui apparaît pendant une image (par un événement de carte ou un
  programme) fait son premier tick dans l'image suivante, une fois intégrée par le moteur (`Owner.World` posé) ; une entité
  détruite ou retirée ne fait plus de tick. Le binaire pourrait faire tourner une entité neuve dans le même tick, par la
  répétition de `0x800386D0` (non vérifié, O-E20-11) : ce n'est pas l'objet d'E20.a.

  **Publication de l'image** : D-E19-13 reste vrai. L'animation qu'un programme demande n'est appliquée au sprite qu'en fin
  d'image (`SyncAnimation`) ; dans les ticks d'une image, le mouvement lit l'animation courante telle que l'image précédente l'a
  laissée. Mais `SyncAnimation` et `SyncTransform` publient l'état d'après le dernier tick de l'image en cours : le moteur met à
  jour les entités avant le monde (`World.cs:533-583`), donc les laisser dans `AlundraEntityScriptProxy.Update`, avant les ticks
  du monde, ajouterait une image de retard visible.
- ✅ **A4 — Le héros à chaque tick.**
  - `MovePlayer` passe une fois par tick, et zéro fois dans une image sans tick.
  - Retirer `JumpStartTickStamp`, sa garde et le cachet du saut (`AlundraPlayerManager.cs:294-301`, `:443`), devenus sans objet.
  - Idle, Moving et les états de saut appellent `TryUseItem` (bouchon : rend 1), `PlayerTryAction` (rend 0),
    `CheckEntityInteraction` (existant, Idle et Moving seulement) et `PlayerTryAttack` (rend 0), dans l'ordre du binaire (§0.2.2).
  - Le prologue appelle `UpdatePlayerWeaponEffect`, `UpdateWeaponStepProgression` et `UpdatePlayerCarriedEntity(0)`, bouchons à
    leur place (`0x80031D68-0x80031D78`) ; la fin appelle `UpdateItemEffectState`, suivi des copies de PV existantes, et le pilote
    `0x8003634C` à son second site (`0x80031CC4`), bouchons (D-E20-14).
  - Chaque bouchon est une méthode nommée d'après le binaire, documentée, sans effet.
- ✅ **A5 — La passe des compteurs** (`0x80038998`), une fois par tick, sur toutes les entités, après les programmes : `+0x1C` + 1,
  `DamagedTickCounter` et `FrameCollisionTickCounter` décrémentés s'ils ne sont pas nuls, `TouchingEntity` et `HitCounter`
  effacés ; `RidingEntity` gardé (D-E20-13). Inventorier d'abord les lecteurs de ces champs dans la DLL (`0x47` et `0x48` lisent
  `HitCounter` ; le choix E/D lit `TouchingEntity` ; `+0x1C` et `+0x1CC` : à relever).
- ✅ **A6 — Premières images (O-E19-80).** Sonder le vrai jeu (méthode d'E19.v2) sur une nouvelle partie et sur une arrivée par
  `0x52` ou `0x53` suivie d'un verrou ; appliquer la règle du plancher à la queue commune seulement si le cas se montre.
- ✅ **A7 — Tests, traces, plan.**
  - Chaque épingle qui change est listée ici, avec l'ancienne valeur, la nouvelle et sa raison (le binaire, ou l'oracle D1 quand il
    couvre le cas). Attendues d'après D7 : SJ6b (155136 au second tick, à vérifier sur l'oracle), SJ5b et SJ5c (Croix tenue
    jusqu'au premier tick), SJ4 et SJ4b retirés avec le cachet, et ce que A0 relève.
  - Nouveau fichier `AlundraTickStructureTests` (A8).
  - Traces de référence du héros et de l'intro re-mesurées, chaque écart expliqué.

#### Acceptation

- Build Release puis Debug ; `Alundra.Tests` sans échec ; aucun changement dans le moteur.
- **A8, structure du tick** (`AlundraTickStructureTests`), avec des images de 0, 1 et 2 ticks :
  - le nombre d'appels à `MovePlayer` égale le nombre de ticks ; une image sans tick ne change pas le héros ;
  - l'ordre d'un tick est : pad, passes de la boîte, événements de carte, `MovePlayer`, programmes des entités, compteurs, listes,
    horloge, physique, point d'accroche, inventaire ;
  - une valeur posée au point d'accroche au tick k (un `TouchingEntity`) est vue par le choix des programmes au tick k+1, puis
    effacée par la passe des compteurs du même tick k+1 ; cela tient aussi quand k et k+1 tombent dans la même image ;
  - porte fermée, le bloc des entités ne fait tourner que les listes ;
  - **rendu à jour**, par un vrai `World.Update` (la boucle des entités du moteur, puis le monde), avec des images de 1 et de 2
    ticks : après l'appel, la transformation publiée et l'animation résolue (`CurrentAnimationId`, ou l'image du sprite) du héros
    et d'une entité égalent leur état après le dernier tick de cette même image. Le test échoue sur une variante qui laisse
    `SyncAnimation` et `SyncTransform` dans l'`Update` de l'entité, avant les ticks du monde ;
  - **entité neuve** : une entité qu'un événement de carte fait apparaître au tick k d'une image de 2 ticks fait son premier tick
    au premier tick de l'image suivante, avant comme après E20.a.
- **Oracle D1** : UH-1 et UH-2 (`AlundraHeroFallAndPadJumpTests.cs`) restent verts ; leurs valeurs égalent la sortie de l'oracle
  (`docs/plan-e20-annexe/d1-oracle-moveplayer/out/`), champ par champ, avec la convention du +1. Chaque épingle du saut qui change
  (A7) est re-dérivée de l'oracle.
- **Épingles** : la liste d'A7 est complète ; aucun autre test ne change.
- **Traces** : celles du héros et de l'intro sont re-mesurées, chaque écart expliqué dans ce plan.
- **Vrai jeu** : la sonde d'E19.v2 sur une nouvelle partie montre Alundra immobile au début ; A6 est rendue.
- **Vérification** : un verifier frais confirme ce qui précède.
- **Recette en jeu de l'auteur** : la marche, le saut, l'échelle, les dialogues, l'inventaire et le livre de sauvegarde se
  comportent comme avant, de la nouvelle partie jusqu'au premier livre (163).

#### Arrêts, budget, retour arrière

- **Arrêts** :
  - un point épinglé de l'intro ou un arc de la chaîne qui bouge sans raison du binaire (A0 ou A7) ;
  - une régression des premières images du vrai jeu ;
  - un test qui change sans raison du binaire ;
  - plus de dix fichiers de test à réécrire en dehors des épingles listées en A7 : le mode autonome (A2) ne remplit pas son rôle,
    la question revient à l'auteur ;
  - tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Budget** : un `executor` sous contrat, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de
  correction et de re-vérification, chacune sur un état réellement changé.
- **Retour arrière** : `git revert` des commits d'E20.a.

#### Résultats (2026-10-09)

- **Commits** : `b68fefc` (A0 à A5, A7, A8 : le code, les épingles et la trace de l'intro) et `ce15e7a` (A6). Écart au contrat : deux
  commits au lieu d'un par tâche, A1 à A5 touchant les mêmes fonctions sans état intermédiaire vert ; la mention de co-auteur de ces
  commits nomme le modèle de l'exécutant (« Claude Sonnet 5.5 »).
- **Structure** : `AlundraWorldProxy.Update` déroule, à chaque tick, le pad, les passes de la boîte, la porte, les événements de carte
  et le recyclage, puis `RunEntityBlock` (`MovePlayer` sur `AlundraPadState.FromTickPad`, programmes dans l'ordre des cases avec le
  ré-examen des déclencheurs, `RunCountersPass`, listes, horloge, physique avec les sondes du héros, `RunHitPassHook`), puis
  l'inventaire ; porte fermée, seules les listes tournent ; une image sans tick ne fait rien au héros. `PublishEntityFrame`
  publie l'état d'après le dernier tick. Le monde pilote les ticks quand il a un `Game` (`DrivesEntityTicks`) ; un montage sans jeu
  garde la boucle de l'entité (`RunSelfDrivenTicks`), sans passe des compteurs.
- **A0** : sans l'inversion, la nouvelle structure laisse la suite verte (3265 sur 3265). Avec elle, 13 tests bougent, chacun avec
  son mécanisme dans l'ordre du binaire (`docs/plan-e20-annexe/e20-a/a0-mesure.md`), dont A17 : T1 posé par la carte et effacé
  par Ronan dans le même tick, pas un blocage. A14 et A5r gardent la réserve acceptée par l'auteur (leur compte exact repose sur
  du code porté avant E20.a).
- **Épingles** : la liste complète est dans `docs/plan-e20-annexe/e20-a/epingles.md` ; SJ6b égale le tick 2 de l'oracle D1 ;
  l'intro avance de 1 image sur quatre repères (`flag83EA` 1034 → 1033, `flag83E9` 1202 → 1201, `flag860` 1704 → 1703, son 61
  1087 → 1086), la trace de l'intro est re-mesurée (les lignes des événements de carte passent avant celles des entités) ; les
  traces du héros ne changent pas. Correction du verifier : la course de Giles d'A10J ne s'est pas allongée, elle est seulement
  décalée d'une image.
- **A6** : la sonde du vrai jeu montre Alundra immobile au début d'une nouvelle partie (`0x36` puis `0x0`, jamais une pose de saut)
  et après une arrivée suivie d'un verrou ; la règle du plancher n'est pas appliquée (`docs/plan-e20-annexe/e20-a/a6-sonde.md`).
  Non sondé : une arrivée sans verrou.
- **A8** : `AlundraTickStructureTests`, 10 tests sur un vrai `World.Update` ; le test du rendu échoue bien quand les synchronisations
  reviennent avant les ticks du monde.
- **Tests** : `Alundra.Tests` 3274 sur 3274 en Release et en Debug.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2). Trois P4 reportés à E20.b, qui touche les mêmes endroits : la
  phrase fausse sur A10J dans le commentaire de `AlundraHeroJumpArcTests.cs` (vers 173-176) ; le commentaire périmé de
  `AlundraPlayerManager.cs` (vers 284-288) qui appelle encore les bouchons « no-ops non portés » ; une assertion sur la position
  publiée du héros dans le test du rendu.
- **Reste** : la recette en jeu de l'auteur (de la nouvelle partie au premier livre, rien ne change sauf le décalage d'un tick).

#### Contraintes transmises aux tranches suivantes

- **E20.b et E20.c1** : l'horloge d'animation de la DLL réserve le tick d'un changement d'animation, alors que le binaire montre
  l'image 0 de la nouvelle animation dès ce tick, avec sa boîte et son attaque (`0x80038BAC`) ; un changement de direction relance
  l'animation dans le binaire (`0x80038B08`). Les deux tranches le vérifient contre l'oracle D1.
- **E20.c1** : la passe de coups se branche au point d'accroche d'A3, dans l'ordre des cases.
- **E20.e** : la réserve d'effets reste par image jusque-là.

### 1.4 E20.b — Le coup d'épée, sans dégâts ✅ (faite et CONFIRMED le 2026-10-09, `0766cf8`, `0295395`, `768a6f6` ; recette en jeu validée par l'auteur le 2026-10-09)

**Révision 1 (2026-10-09).** Relecture : **REVISE** (pas de trace d'oracle du coup en l'air en marchant ; pas de test sur le vrai
préfab ni sur les tables d'objets du monde ; scénarios comparés mal nommés). Corrigé : trace `AT12` ajoutée à l'oracle (même
montage, sortie stable, `run_e20b.py` rend 0), faits rattachés chacun à sa trace, B5 et l'acceptation nomment chaque fichier, test du
vrai préfab et test des tables du monde ajoutés.
Relecture neuve de la révision 1 : **READY** (2026-10-09). Exécution lancée sans soumission à l'auteur, à sa demande.

**But.** Carré frappe comme dans le binaire : au sol, en l'air, avec l'arme équipée, aux mêmes ticks que l'oracle, avec le même son,
le même freinage et les mêmes priorités (Carré sur Croix ; interaction d'abord). Le coup ne touche encore rien (E20.c1).

**Mode d'exécution** : l'auteur a demandé le 2026-10-09 d'enchaîner les tranches sans lui soumettre chaque plan (relecture,
exécution et vérification restent obligatoires ; arrêt sur une décision qui lui revient).

**Faits porteurs** (découverte du 2026-10-09, `docs/plan-e20-annexe/e20-b/`, contre-vérifiée ; §0.2.4) :
- [binaire] Aiguillage de `MovePlayer` (table `0x80022D3C`) : le groupe au sol (`0x80031E18`) reçoit `0x04`, `0x08`, `0x09`, `0x10` à
  `0x13`, `0x18`, `0x19`, `0x25`, `0x3F`, `0x40`, `0x44`, `0x49` ; le groupe en l'air (`0x80031DF8`) reçoit `0x0A`, `0x14` à `0x17`, `0x1F`,
  `0x26`, `0x41`, `0x42`, `0x46`, `0x4B`. Au sol, un héros en l'air passe en `0x2D` ; en l'air, un héros au sol passe en `0` ; les deux
  appellent ensuite `TryUseItem` (`0x8002ED64`, résultat ignoré). Ni l'un ni l'autre ne lit le pad ni ne touche la direction.
- [binaire] `PlayerTryAttack` (`0x8002EAF4`) : garde `+0x180 & 0x2000` ; relâchement d'abord (minuterie ≥ 60 et au sol : attaque
  chargée, E20.f ; sinon 0) ; puis l'appui : fiche `0x80028C79 + 13 w`, plus 4 en l'air ; animation 0 : son 3, rend 0 ; sinon
  `0x80127110` = la fiche, `0x80127114` = 0, `+0x88` = l'animation, rend 1. La fiche courante `0x8012710C` est posée en tête de
  `MovePlayer` (`0x80031B70-0x80031B8C`, `byte[0x80028C14 + objet]`) et par l'initialisation (`0x80031A4C`).
- [binaire] Tables : `0x80028C14` (100 octets, objet vers fiche ; sans arme, `0x80028C13` = 0 donne la fiche 0), `0x80028C78` (12 fiches
  de 13 octets), `0x80028D14` (deux drapeaux par animation, `0x50` animations) ; valeurs dans `e20-b/faits-binaire-dll.md` §1.
- [binaire] `UpdateWeaponStepProgression` (`0x8002F768`) est du vrai code dans l'oracle : il efface le pointeur au tick qui suit l'appui
  pour une arme de mêlée, et compte jusqu'au délai de la fiche pour un tir (l'apparition est E20.e0 et E20.f).
- [oracle] Chaque fait ci-dessous a sa trace dans `docs/plan-e20-annexe/e20-b/oracle/out/` :
  - épée de base, appui au tick 1 : `0x10` et son 29 dès le tick 1, retour à `0` au tick 22 (21 ticks) (`AT1`) ; un appui au tick 22
    est perdu, le premier accepté est au tick 23 (`AT6`, `at6-sweep.txt`) ; Carré tenu ne refrappe pas (`AT5`) ;
  - en marchant : ForceX 79872 au tick de l'appui, puis 0, Moving au tick 23 si la direction reste tenue (`AT2`) ;
  - en l'air (`0x14`) : 10 ticks, retour à `0x2D` (`AT3`) ; un coup en l'air encore en cours à l'atterrissage est coupé ce tick-là
    (`AT3b`, `AT3c`) ; en marchant, l'arc vertical ne change pas, mais ForceX décroît de 4 par tick pendant le coup (150524 au tick 6),
    vaut 75244 au tick du retour puis 112886, et le héros va un peu moins loin (`AT12`) ;
  - Carré et Croix au même tick : le coup (`AT4`) ;
  - une case de propriété de sol `0x20` sous un des quatre coins de l'empreinte empêche le coup ; une case éloignée non (`AT7`, `AT7c`,
    `at7-cellmap.txt`) ; Carré et Croix sur une telle case : le saut (`AT7d`) ;
  - coup au bord d'un rebord : `0x2D` au tick qui suit la perte du sol (`AT8`, témoin `AT8n`) ; sol qui s'abaisse pendant le coup
    (`AT8b`) ;
  - sans arme : la fiche 0 (`AT10`) ; la direction est celle du tick de l'appui (`AT11`).
- [oracle] Par arme : animations, sons, fiches, ticks de fin et appels d'apparition dans `e20-b/oracle/out/weapons.txt`.
- [DLL] Le son du coup vient déjà du changement d'animation (E19.t) ; la fin d'animation de la DLL devrait tomber au même tick que
  l'oracle (horloge logique, réservation du tick de l'appui), sauf un risque : dans une image de deux ticks où le premier est le tick de
  retour et le second un nouvel appui, `ZImpulseTaken` pourrait faire perdre le son du second coup (`AlundraFrameSyncPasses.cs:231`,
  `:257-258`).

**Périmètre.** DLL `Alundra/Scripts/` : `AlundraPlayerManager.cs` (portage de `PlayerTryAttack` et de `UpdateWeaponStepProgression`
sans l'apparition, groupes d'attaque, fiche courante), une classe de tables d'armes, `IAlundraScriptHost.cs` (accès aux tables
d'objets), `AlundraFrameSyncPasses.cs` seulement si le son d'une image de rattrapage est faux (B6) ; tests `Alundra.Tests/` ; ce plan.

**Hors périmètre.** Boîtes, `CurrentAttack` et dégâts (E20.c1) ; minuterie de charge, aura, attaque chargée, rebond du fléau (E20.f) ;
apparition des tirs (E20.e0, E20.f) ; herbe coupée (E20.e) ; le second appelant de `PlayerTryAttack`, en `0x29` (E20.d).

#### Tâches

- ✅ **B1 — Tables d'armes et fiche courante.** Une classe de tables (`AlundraWeaponTables`, nom au choix de l'exécutant) recopie les
  trois tables du binaire, chacune avec son adresse, comme `AnimationTables` le fait déjà pour d'autres tables. La fiche courante
  (`0x8012710C`) est un champ d'état posé en tête de `MovePlayer` et à l'initialisation. Les tables d'objets (`AlundraItemTables`)
  arrivent par `IAlundraScriptHost` (membre par défaut nul ; le monde les fournit ; un montage peut les poser) ; sans tables, l'arme
  vaut « aucune », donc la fiche 0, comme le binaire sans arme.
- ✅ **B2 — `PlayerTryAttack`**, porté tel quel : garde `0x2000`, relâchement d'abord (la minuterie reste à 0 jusqu'à E20.f, donc ce
  chemin rend 0), appui, fiche au sol ou en l'air, animation 0 avec le son 3 (`ScriptHost.SoundPlayer`), écriture du pointeur, du
  compteur et de l'animation. Les deux champs `0x80127110` et `0x80127114` deviennent des champs d'état.
- ✅ **B3 — Les groupes d'attaque** dans `MovePlayer`, exactement les identifiants de l'aiguillage du binaire, avec `TryUseItem`
  appelé ensuite (bouchon, résultat ignoré). `0x04`, `0x08`, `0x09`, `0x0A`, `0x1F`, `0x25` et `0x26` entrent aussi dans les groupes,
  comme dans le binaire, même si E20.b ne les atteint pas encore.
- ✅ **B4 — `UpdateWeaponStepProgression`**, porté sans l'apparition : effacement du pointeur et compteur jusqu'au délai, d'après les
  drapeaux de `0x80028D14` ; l'apparition reste un bouchon nommé (E20.e0, E20.f).
- ✅ **B5 — Tests contre l'oracle.**
  - Étendre un montage (par exemple `JumpHeroRig`) avec l'horloge d'animation et le pont des fins pour les animations d'attaque, dans
    les quatre directions.
  - Comparer, tick par tick et champ par champ, la DLL à ces traces de `docs/plan-e20-annexe/e20-b/oracle/out/` : `AT1`, `AT2`, `AT3`, `AT3b`, `AT3c`,
    `AT4`, `AT5`, `AT6`, `AT7`, `AT7c`, `AT7d`, `AT8`, `AT8b`, `AT8n`, `AT10`, `AT11`, `AT12` ; plus la règle des coins d'`at7-cellmap.txt`
    et le balayage d'`at6-sweep.txt` (appui au tick 22 perdu, au tick 23 un second coup). Pour chaque arme d'AT9 (`at9/AT9g-item*`,
    `at9/AT9a-item*`, `weapons.txt`) : l'animation choisie, le son, le pointeur et le compteur ; la minuterie de charge est exclue
    (E20.f), comme `X1` et `X2`.
  - **Vrai préfab** : au moins un test sur le préfab exporté du héros (`ArcRun` avec `Prefabs: true` et un héros libre, ou un montage
    bâti sur le préfab et les `.anim2d` exportés), qui montre un coup au sol et un coup en l'air à l'épée de base : son 29 au tick de
    l'appui S, `0x10` de S à S+20, Idle à S+21 (le tick 22 de l'oracle) ; en l'air, `0x14` sur 10 ticks puis `0x2D`.
  - **Tables du monde** : un test avec `AlundraWorldProxy` pour hôte montre que ses tables d'objets ne sont pas nulles et qu'une arme
    équipée autre que l'épée de base choisit sa propre fiche (l'épée d'acier donne `0x3F`).
  - Ajouter : la prise d'échelle qui l'emporte sur un état d'attaque (pente 6, `0x80031D20`) ; une image de deux ticks dont le premier
    est le tick de retour et le second un nouvel appui (le son 29 doit sonner, comme le binaire).
- ✅ **B6 — Le son d'un coup dans une image de rattrapage.** Si B5 montre le son manquant, corriger `TakeZImpulse` pour que chaque
  changement d'animation d'une image de rattrapage joue son son, comme le binaire (`0x80038BB0` à chaque changement) ; ce code sert à
  toutes les entités : les tests de son existants restent verts, et le changement est expliqué ici.
- ✅ **B7 — Les trois P4 d'E20.a** : le commentaire faux d'A10J (`AlundraHeroJumpArcTests.cs`, vers 173-176), les commentaires périmés
  des bouchons (`AlundraPlayerManager.cs`, en-tête de classe, vers 284-288 et 478-479), une assertion sur la position publiée du héros
  dans le test du rendu (`AlundraTickStructureTests.cs`, vers 295-325).

#### Acceptation

- Build Release puis Debug ; `Alundra.Tests` sans échec dans les deux ; aucun changement du moteur.
- B5 : chaque trace nommée en B5 est égalée tick par tick sur les champs comparés (animation courante et cible, direction, ForceX,
  ForceY, ForceZ, PosX, PosY, PosZ avec la convention du +1, IsOnGround, pointeur et compteur), avec le son 29 (ou 3 pour le bâton) au
  bon tick ; le test du vrai préfab et celui des tables du monde passent.
- Les traces de référence du héros et de l'intro ne changent pas (aucune ne presse Carré).
- Verifier frais.
- Recette en jeu de l'auteur : Carré (touche X, ou X à la manette) frappe à l'épée au sol, avec son son, et le héros freine ; pendant un
  saut, le coup en l'air ; tenir Carré ne refrappe pas ; un coup ne traverse ni ne blesse rien encore.

#### Résultats (2026-10-09)

- **Commits** : `0766cf8` (B1 à B4, et le P4 des commentaires des bouchons), `0295395` (B5, B6), `768a6f6` (les deux autres P4). Écart :
  les tâches sont regroupées, B1 à B4 ne pouvant pas être vertes séparément et B5 ayant besoin de B6.
- **Code** : `AlundraWeaponTables` recopie les trois tables du binaire (vérifiées octet par octet par le verifier) ; la fiche courante,
  le pointeur et le compteur sont des champs du héros (`CurrentWeaponRecord`, `WeaponRecordPointer`, `WeaponStepCounter`) ; les tables
  d'objets arrivent par `IAlundraScriptHost.ItemTables` (le monde les fournit) ; `PlayerTryAttack`, les deux groupes d'attaque et
  `UpdateWeaponStepProgression` (apparition en bouchon, `SpawnWeaponProjectile`) suivent le désassemblage ; la branche `+0x20` efface
  aussi le pointeur, comme le binaire (`0x80031BB0-0x80031BC0`).
- **Oracle** : la DLL égale l'oracle tick par tick sur `AT1`, `AT2`, `AT3`, `AT3b`, `AT3c`, `AT4`, `AT5`, `AT6`, `AT7`, `AT7c`, `AT7d`,
  `AT10`, `AT11`, `AT12`, les 22 traces d'AT9, `at6-sweep.txt` et `at7-cellmap.txt` ; les tests chargent les fichiers de traces.
  Exceptions documentées, toutes dues à du code antérieur à E20.b :
  - `AT8` et `AT8n` : le contact au sol est lu un tick après le binaire (lecture de la racine en tête d'image, E19.d2c1 R6) ; seule cette
    colonne est décalée, les décisions sont exactes ;
  - `AT8b` (sol abaissé au milieu de l'image) : la chute est d'un tick en retard (ticks 7 à 15) et le retour à Idle vient au tick 16
    au lieu de 15 ; ces colonnes ne sont pas comparées ; la décision du coup (gardé au tick 7, `0x2D` au tick 8) est exacte ;
  - le son 11 de l'atterrissage n'est pas comparé (non porté : `CreatePlayerAnimationEffects`, E20.e).
- **B6** : nécessaire. Dans une image de deux ticks dont le premier ramène le héros à Idle et le second lance un nouveau coup, le son 29
  manquait et le second coup finissait un tick trop tôt. `StepAnimationClock` retient maintenant la cible de l'impulsion
  (`ZImpulseTakenForTarget`) : un changement vers une autre cible dans la même image prend sa propre impulsion et son son, et relance
  la réservation de ticks, comme `UpdateAnimation` à chaque changement. Sans B6, seuls les deux tests de B6 échouent : le reste de la
  suite n'en dépend pas. Ce cas ne se produit pas par une vraie image (le pad n'a qu'un front par image) ; le test pilote les passes à
  la main.
- **Autres tests** : le vrai préfab (carte 10, arrivée par portail, pad libre) montre le coup au sol (son 29 en S, `0x10` de S à S+20,
  Idle en S+21) et le coup en l'air (`0x14` sur 10 ticks puis `0x2D`) ; le monde fournit ses tables (l'épée d'acier donne la fiche 1,
  `0x3F`) ; la prise d'échelle l'emporte sur un coup en cours.
- **Tests** : `Alundra.Tests` 3325 sur 3325 en Release et en Debug ; traces du héros et de l'intro inchangées ; la sonde du vrai jeu
  montre la même nouvelle partie qu'avant.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) ; P3 : les exclusions d'AT8 étaient à écrire dans le plan (fait
  ici) ; P4 : les points ci-dessus, et un changement de direction seul après un changement de cible dans la même image ne reçoit
  toujours pas de seconde impulsion (antérieur).
- **Recette en jeu** : validée par l'auteur le 2026-10-09 (« recette en jeu d'E20.b OK »).

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un test existant qui change sans raison du binaire ; un besoin de
  toucher au moteur.
- **Budget** : un `executor`, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de correction.
- **Retour arrière** : `git revert` des commits d'E20.b.

### 1.5 E20.c1a — La passe de coups ⏳

**Révision 1 (2026-10-09).** Relecture : **REVISE**, quatre points, tous corrigés : les cas de D2 qui demandent `ComputeNewHp`
passent à E20.c1b ; la source de l'animation figée du héros est nommée et comparée ; les fenêtres de comparaison de `H3` et de
`H1-both` sont bornées, avec ce que c1a attend après ; des rejeux à deux ticks par image et un test de l'octet `0x40` au tick même du
contact sont ajoutés.

**Révision 2 (2026-10-09).** Seconde relecture : **REVISE**, un point, tranché **FIX** : l'animation figée du héros n'est plus comparée à
l'oracle au tick 18 de `H3` ni au tick 2 de `H1-both`, où le héros de l'oracle est déjà blessé (E20.c2) ; c1a y attend son propre
`TargetAnimationId`. C'est le second REVISE de la tranche : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-09). Exécution lancée sans soumission à l'auteur, à sa demande.

**But.** Chaque tick, après la physique, la DLL fait la passe de coups du binaire (`UpdateBalanceRecords`, `0x80039300`) : les boîtes
d'attaque de l'image courante contre les corps des cibles, avec les mêmes règles et le même résultat que l'oracle. Un coup pose
`TouchingEntity`, le délai de 25 ticks (`+0x1D0`), le compteur de coups de l'attaquant, et fait trois étincelles. Les dégâts eux-mêmes
viennent avec E20.c1b ; aucune barre de vie ne bouge dans cette tranche.

**Faits porteurs** (découverte du 2026-10-09, `docs/plan-e20-annexe/e20-c1/`, contre-vérifiée ; §0.2.5, §0.2.10) :
- [binaire] La liste de la passe est celle de `0x800384F4`, refaite après les programmes et les compteurs : statut 2 ou 3, `+0x20 == 0`,
  ordre des cases, le héros d'abord. Entre la physique et la passe de coups tourne `0x80038E84` (les effets attachés aux entités).
- [binaire] En tête de la passe, même avec une liste vide, l'animation du héros est figée dans `0x80146DF8` : c'est `+0x88` du héros
  (`TargetAnimationId`, `0x80039314` puis `0x80039348`), tel qu'il est à ce moment du tick, donc après l'horloge d'animation (qui peut
  l'avoir changé par une fin enchaînée, `0x80038D60`) ; ce n'est pas l'animation courante.
- [binaire] À chaque changement d'animation (`0x80038B18-0x80038BAC`, y compris la fin enchaînée `0x80038D54` et la relance par
  changement de direction `0x80038B08`), `+0xB4` reçoit l'octet `0xD` de la fiche d'animation et `CurrentAttack` (`+0x1C8`) est rechargé :
  `Attacks[animation cible + 1]`, sinon `Attacks[0]`, sinon rien. La boîte de l'image (`+0x1D4`) suit l'image courante : décalages signés
  `<< 16`, tailles `(n << 16) − 1` (0 pour une cible de taille nulle), index `0xFFFF` : pas de boîte. `HitBox` = position + décalage.
- [binaire] À la création (`0x80039E30-0x80039E4C`) : `+0x1C4` = la fiche d'équilibre (sprite, remappage `0x1F`-`0x7F` vers `0x1E`, chaîne
  par niveau jusqu'au seuil de la carte, `0x80044550`), `+0x14` = `+0x18` = MaxHp ; l'animation et la boîte existent dès ce tick.
- [données] Le seuil est la propriété `D` de la carte exportée (texte) ; `Data/balance.json` porte les 512 chaînes, lues par personne
  aujourd'hui.
- [oracle] Épée de base sur un Beannoïde de la 14 (fiche 335, MaxHp 9) : contact au tick 5 du coup (au tick 1 si la cible est assez
  proche) ; 3 étincelles au centre du recouvrement ; au tick 6, le Beannoïde choisit D, puis la passe des compteurs efface ; un second
  contact dans le même coup est bloqué par `+0x1D0` ; portée de dx −9 à 59 px de face ; deux cibles dans la portée : toutes deux
  touchées ; un Beannoïde en animation à octet `0x40` n'est jamais touché ; le héros qui marche dans un Beannoïde est touché au contact
  (tick 17 de `H3`).
- [DLL] Aucun de ces champs n'est écrit aujourd'hui ; `CurrentAttack`, `BalanceRecord` et `FrameCollision` sont commentés ;
  `ModdedPos` n'est écrit qu'à la création ; `AnimFlags` et l'animation courante ne changent qu'en fin d'image (`SyncAnimation`,
  D-E19-13) ; `TouchingEntity` est une `Entity` du moteur ; les tailles s'appellent `Width/Height/Depth` pour une cible (Y, puis Z) et
  `CollisionWidth/CollisionDepth/CollisionHeight` pour un attaquant.

**Périmètre.** DLL `Alundra/Scripts/` : une table des fiches d'équilibre (chargée une fois, comme `AlundraItemTables`, avec un accès
par l'hôte), l'initialisation des entités et du héros, les champs `+0x1C4`, `+0x1C8`, `+0x1D4` et la boîte de l'image, le
rechargement à chaque changement d'animation, la liste de la passe, `UpdateBalanceRecords`, les étincelles ; tests ; ce plan.

**Hors périmètre.** `ComputeNewHp`, `ResolveBalanceTarget`, fiches d'équipement, `0x86`, `0x87`, `0x95`, `0x71`, leviers (E20.c1b) ;
l'entrée des dégâts du héros (E20.c2) ; les effets 4 et 5 attachés à la cible (E20.f : ils demandent une ancre d'effet et une clé de
profondeur que la DLL n'a pas ; l'épée ne les déclenche jamais) ; la passe `0x80038E84` elle-même (E20.e) ; l'avance du générateur
pseudo-aléatoire une fois par image (E20.e0) ; la réutilisation des cases libres (O-E20-13).

#### Tâches

- ⏳ **C1 — Fiches d'équilibre.** Charger `Data/balance.json` une fois (table mise en cache, accès par `IAlundraScriptHost`, comme les
  tables d'objets) ; choisir la fiche comme `0x80044550` avec le seuil `D` de la carte ; à la création de chaque entité, poser la fiche,
  `Hp` et `HpMax` ; le héros prend la fiche 0. Le cas où la DLL ne trouve pas l'en-tête du sprite est documenté.
- ⏳ **C2 — Attaque courante et boîte de l'image au tick.** À chaque changement d'animation que l'horloge voit (`StepAnimationClock`,
  fin enchaînée, changement de direction, animation de départ), recharger `CurrentAttack` et l'octet d'animation (`+0xB4`) pour la passe
  de coups ; tenir, par entité, le curseur (animation, tick logique) qui donne la boîte de l'image courante d'après les
  `collision_keyframes` (règle de D6), y compris au tick du changement et dans les ticks d'une image de rattrapage. Ce qui change en fin
  d'image (`SyncAnimation`, D-E19-13) ne change pas.
- ⏳ **C3 — La liste et la passe.** Construire la liste au pas « listes » du bloc des entités (statut 2 ou 3, `BlockedByEntity` nul, le
  héros d'abord, ordre de `_spawnedEntities`) ; ajouter, entre la physique et le point d'accroche, une place vide pour `0x80038E84` ;
  porter `UpdateBalanceRecords` au point d'accroche : figer l'animation du héros (`0x80146DF8`) même avec une liste vide, en copiant
  le `TargetAnimationId` du héros tel qu'il est au point d'accroche, après l'horloge d'animation du tick (jamais `CurrentAnimationId`,
  qui ne change qu'en fin d'image) ; masque de
  classes réordonné ; cibles sautées ; recouvrement élargi d'une unité, `HitBox` de l'attaquant contre `Pos + Mod` de la cible ;
  réponse de la cible (`(r & 0xC0) == 0x80` : pas de `TouchingEntity`) ; `+0x1D0 = 0x19` ; compteur de l'attaquant ; plusieurs cibles,
  premier attaquant.
- ⏳ **C4 — Étincelles.** Trois effets 9 au centre du recouvrement, par la réserve d'effets et l'étage d'effets du tick, avec les trois
  tirages du générateur pour chacun quand l'effet a pu être créé (même formule que le vase de vie, les trois axes à `0x20001`).
- ⏳ **C5 — Tests.**
  - Contre l'oracle `docs/plan-e20-annexe/e20-c1/oracle/out/` (`python -B docs/plan-e20-annexe/e20-c1/oracle/tools/run_c1.py`), tick par
    tick, sur les champs de la passe : contact, `TouchingEntity`, `+0x1D0`, compteurs de coups, animation figée du héros
    (`0x80146DF8`, colonne du journal de l'oracle ; au tick 1 de `H1-near`, 16, soit `0x10`), programme choisi au tick suivant, nombre,
    effet et centre des étincelles.
    - Traces comparées en entier : `H1`, `H1-near`, `H1-edge`, `H2`, `H4a`, `H4b`, `H5`, `H6`, `H7`, `H8`, et les bords de la portée de
      `reach-sweep.txt`.
    - `H3` : ticks 0 à 18, champs de la passe seulement, effacement et choix du programme du tick 18 compris ; l'animation figée du
      héros n'est comparée que jusqu'au tick 17 : au tick 18, le héros de l'oracle est déjà dans l'état blessé (`0x31`, entrée
      `0x80031A68` dans `MovePlayer`), qui est E20.c2, et c1a y attend son propre `TargetAnimationId`, 1 (la marche). Au-delà du tick
      18, le héros de l'oracle est blessé (`+0x1CC = 0x78`, recul) : ces ticks sont exclus. Ce que c1a attend ensuite, vérifié par un
      test à part : tant que le contact dure, un nouveau coup sur le héros tous les 25 ticks, `+0x1CC` restant à 0.
    - `H1-both` : ticks 0 à 2, même règle ; l'animation figée du héros n'est comparée que jusqu'au tick 1, et c1a attend 16 (`0x10`,
      le coup) au tick 2 ; au-delà du tick 2, exclu, avec la même attente pour c1a.
    - Les vitesses des étincelles, tirées du générateur, sont exclues tant que son avance par image n'est pas portée (E20.e0).
  - Les cas de validation de D2 (`docs/plan-e20-annexe/d2-oracle-coups/`) qui relèvent de la passe, refaits sur la passe de la DLL : 1
    (masque réordonné), 2 (recouvrement en unités brutes), 5 (premier attaquant), 6 (plusieurs cibles), et la partie de 3 sur
    `TouchingEntity`, `+0x1D0` et le compteur de coups. Le cas 4 (dégât minimal) et la partie de 3 sur les PV vont à E20.c1b.
  - **Images de rattrapage** : `H1` et `H1-near` rejoués avec deux ticks par image, dans deux calages (le tick de l'appui en premier tick
    d'image ; puis, après une image d'un tick, le tick du contact et la fin du coup en second tick d'image) : mêmes ticks de contact,
    mêmes boîtes, mêmes étincelles que l'oracle, tick par tick.
  - **Octet `0x40` au tick du contact** : une cible qui passe dans une animation à octet `0x40` au tick même où son corps recouvre pour
    la première fois la boîte d'un attaquant n'est pas touchée ; dans le sens inverse, une cible qui quitte une telle animation ce
    tick-là est touchée. Attente tirée de l'ordre du binaire : `+0xB4` est posé par l'horloge (`0x80038B68`) avant le test de la passe
    (`0x80039444`).
  - La chaîne du coup avec la vraie passe : écrit au tick N, lu par le choix des programmes au tick N+1, effacé par les compteurs, dans
    une image de deux ticks.
  - Un arc au vrai préfab sur la carte 14 : le héros frappe un Beannoïde réel ; étincelles, `TouchingEntity`, D choisi au tick suivant
    (le D natif du Beannoïde reste sans effet jusqu'à E14).

#### Acceptation

- Build Release puis Debug ; `Alundra.Tests` sans échec ; aucun changement du moteur ; traces du héros et de l'intro inchangées.
- C5 égale l'oracle sur chaque trace nommée, champ par champ, avec les exclusions écrites ci-dessus.
- Verifier frais.
- Recette en jeu de l'auteur, carte 14 : un coup d'épée sur un Beannoïde fait des étincelles et ne le blesse pas encore ; en marchant
  dans un Beannoïde, des étincelles sur le héros toutes les 25 ticks, sans perte de cœurs.

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un test existant qui change sans raison du binaire ; un besoin de
  toucher au moteur ; un changement de l'ordre de fin d'image (D-E19-13) qui ne serait pas propre à la passe.
- **Budget** : un `executor`, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de correction.
- **Retour arrière** : `git revert` des commits d'E20.c1a.

---

## 2. Points ouverts

| Point | Question | Où elle se tranche |
|---|---|---|
| O-E20-1 | **Tranché le 2026-10-09 (D-E20-16).** La nage (pente 4) et le wagonnet : le plan d'E19 les donnait à E14. D9 propose : la nage en tranche E20.i ; le wagonnet hors d'E20 (il n'a rien dans `MovePlayer`) | — |
| O-E20-2 | **Tranché le 2026-10-09 (D-E20-15).** La règle exacte de l'arrondi du stick (D-E20-8). Proposition de D8 : seulement au départ et à la reprise de la course, quand clavier et croix ne donnent aucune direction et que le stick donne une diagonale, prendre l'axe dominant du stick ; à moins de ε de 45° (ε = 1/16, à régler par une mesure du stick en E20.d), garder l'axe du regard actuel | — |
| O-E20-3 | **Tranché le 2026-10-09 (D-E20-17).** Le butin et l'objet ramassable (`0x80032968`, `0x80032B90`, `0x80032C7C`, gestionnaire C255) servent aussi aux ennemis. D9 propose une tranche dédiée avant E20.e, qui porte aussi `SpawnWarpEntity` (102 appelants, partagé avec E14) | — |
| O-E20-4 | **Tranché le 2026-10-09 (D-E20-18).** Les gestionnaires natifs des projectiles du héros. D9 propose de partager : E20.f porte l'apparition et les gestionnaires propres au héros (vague de l'épée C61, flèches chargées C94 ; la bombe E12 en E20.h) ; ceux que des ennemis partagent peut-être (flèches E17, glace C60, feu C54) vont à E14 | — |
| O-E20-5 | La barricade de la 392 : l'original la franchit-il en soulevant ou en grimpant ? | E20.g |
| O-E20-6 | Une fois E18 faite, D-E20-5 tombe ; le prérequis SC8 d'E18 (PV ≥ 1 au chargement) reste à trancher | E18 |
| O-E20-7 | **Tranché le 2026-10-09 (D-E20-11).** Ordre événements de carte / entités : le corriger dans E20.a (ordre du binaire : pad, événements de carte, entités ; mesure préalable des épingles qui bougent), ou le garder comme écart documenté | — |
| O-E20-8 | **Tranché le 2026-10-09 (D-E20-12).** Structure du tick : boucle de ticks pilotée par le monde dans la DLL (proposition de D7), ou pas fixe de 1/50 s du moteur (migration M3, différée par l'auteur le 2026-08-26) | — |
| O-E20-9 | **Tranché le 2026-10-09 (D-E20-13).** La passe des compteurs efface `RidingEntity` (`+0x12C`) ; la DLL ne recalcule le porteur qu'une fois par image : ne pas l'effacer, écart documenté, jusqu'à un recalcul par tick | — |
| O-E20-12 | Le contact au sol du héros est lu une fois par image, en tête d'image (E19.d2c1 R6) : la perte du sol au bord d'un rebord et un sol qui s'abaisse en cours d'image arrivent un tick après le binaire (`AT8`, `AT8n`, `AT8b` d'E20.b). L'aligner demanderait de relire le contact à chaque tick | à placer (E20.d, où la course quitte souvent un rebord, ou une tranche à part) |
| O-E20-13 | La DLL ajoute les entités neuves en fin de liste, alors que le binaire réutilise la première case libre (`FindFreeEntitySlot`) : après un recyclage en cours de carte, l'ordre de la passe de coups (« premier attaquant ») peut différer | à mesurer avec E20.e0 (apparitions) ou E14 |
| O-E20-11 | Une entité neuve tourne-t-elle dans le même tick dans le binaire (répétition de `0x800386D0`, contrôle de D7 : non vérifié) ? E20.a garde la règle d'aujourd'hui (premier tick à l'image suivante) | à mesurer, avec E20.e0 |
| O-E20-10 | **Tranché le 2026-10-09 (D-E20-14).** `UpdateItemEffectState` (table de défense, bottes, régénération, PV) et `0x8003634C` : bouchons dans E20.a et portage dans E20.c2, ou portage dès E20.a | — |

## 3. Hors périmètre

- Le wagonnet (cartes 61 à 68 et 329) : sans propriétaire, à désigner par l'auteur (D-E20-16).
- Les gestionnaires de projectiles que des ennemis partagent peut-être (flèches E17, glace C60, feu C54) : E14 (D-E20-18).
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
