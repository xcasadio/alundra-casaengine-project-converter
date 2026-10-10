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

Après la découverte d'E20.c2, l'auteur a répondu le 2026-10-09 « fait toutes les recommandations » (questions 1 à 5 de
`docs/plan-e20-annexe/e20-c2/check.md` §d, ADR-0054), puis a donné l'ordre de la suite :

- **D-E20-19** (modifie D-E20-14) — `UpdateItemEffectState` est porté dans E20.c2 ; le pilote d'objet `0x8003634C` et
  `UpdatePlayerCarriedEntity(2)` restent des bouchons nommés à leur place exacte jusqu'à E20.h et E20.g, qui les portent.
- **D-E20-20** — Avant E18, à 0 PV, le héros affiche 0 cœur, garde la main, et sa régénération s'arrête, comme l'original ; seule
  la branche de mort manque.
- **D-E20-21** — Le clignotement d'invulnérabilité vaut pour toute entité, comme l'original, celles que `0x86` rend invulnérables
  comprises.
- **D-E20-22** — E20.c2 porte `0xC3` (soin complet) et `0xBC` (PV max + n), en écrivant dans l'entité du héros.
- **D-E20-23** — Touché en eau profonde, le héros garde la règle de l'original (`0x3A` sur la pente 4) jusqu'à E20.i ; la blessure
  brève qui en résulte dans la DLL est un écart documenté.
- **D-E20-24** — **Ordre de la suite** (« Je veux dans l'ordre : les armes, l'utilisation des objets et les collisions ») : E20.f
  (armes), puis E20.h (objets du Rond), puis les collisions, que la session principale lit comme E20.g (porter et lancer ; caisses et
  jarres redeviennent des obstacles, fin des écarts D-E19-28 et D-E19-46) — lecture à confirmer par l'auteur. Les prérequis de chaque
  tranche passent avant elle (par exemple l'apparition d'E20.e0 avant les projectiles) ; E14.a et la blessure d'E20.c2 suivent.
  Mode AUTO accordé par l'auteur pour la nuit du 2026-10-09 (ni push ni merge).

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
- [DLL] La course est portée par E20.d1 (2026-10-10, §1.13) ; le test « Triangle seul donne Idle » (`AlundraHeroJumpStatesTests.cs`,
  SJ3) est inversé. Vitesses, fins d'animation,
  son à chaque changement d'animation et impulsion Z marchent déjà pour toute animation.
  `AnimCompleteCounter` n'est pas remis à zéro au changement d'animation, comme dans le binaire
  (`0x80038AB4-0x80038E10`) ; la décompilation le remet à zéro (`EntityManager.cs:222`).
- **Corrections d'E20.d1 (2026-10-10, mesurées sur l'oracle de la course)** : la préparation dure 25 ticks, et un changement de
  regard la relance (une préparation retournée au tick 10 démarre au 35, au 20 au 45) ; poussière de `0x29` : aucune pendant la
  première boucle, puis toutes les 8 images (`+0xB0` à 1 ou 2), puis toutes les 4 (E20.e) ; `0x3E` dure 11 ticks puis passe à `0x01`
  en l'air, c'est le chemin normal ; sans bottes de niveau ≥ 2, l'eau profonde est un mur (O-E19-22).

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
| E20.c1b-1 (§1.6) | Dégâts, équipement et opcodes : `ComputeNewHp` et `ResolveBalanceTarget` (fonctions pures), les trois fiches d'équipement (la partie d'`UpdateItemEffectState` dont dépendent les dégâts) et leur effacement au chargement de carte, `HitBox` tenue à chaque tick, `0x71`, `0x86`, `0x87`, `0x95` ; ferme la régression de c1a (programmes D qui décident sur un `Result` périmé faute de `0x87`) | oracle K1 à K3 (`e20-c1b/oracle/`) ; cas de D2 sur les PV ; `0x87` et `0x95` dans de vrais programmes D | Sur la 14, l'entité qui n'accepte que la bombe ne réagit plus à l'épée |
| E20.c1b-2 (§1.7) | Les réactions scriptées : leviers de la 115 nord-ouest et de la 116, bascule et levier 22 de la 117, leviers de la 44, par des arcs qui partent d'une arrivée dans la zone du levier (sans oracle du binaire : attentes tirées des programmes) ; le P3 reporté de c1b-1 ; le couvercle de la 362 attend la bombe (E20.h) | arcs | Les leviers réagissent au coup d'épée, quand on peut les atteindre (après le hall de la 115) |
| *E14.a* (autre étape) | Muruta et Beannoïde (D-E20-4), planifiée après E20.c1 | plan d'E14 | Le hall de la 115 |
| E20.c2 | Le héros touché : entrée `0x80031A68` (toucheur et cases dangereuses), `0x31`/`0x3A`, invulnérabilité `0x78` et clignotement, PV vers `PlayerStats` et le HUD, `UpdateItemEffectState` (table de défense, bottes, régénération) et le pilote `0x8003634C` en vrai (D-E20-14), chemin `controlFlags == 0x20`, pas de mort (D-E20-5) | oracle de `0x80031A68` ; arcs | Les cœurs baissent au contact, le héros clignote, il ne meurt pas |
| E20.d | Course : les cinq états, entrée et sorties, cône, reprise, rebond, charge `0x04` (ses coups viennent d'E20.c1), escalade prioritaire, arrondi du stick (D-E20-8) ; le test épinglé « Triangle seul donne Idle » s'inverse | oracle de `MovePlayer` ; arcs | Courir, s'arrêter, reprendre, foncer, buter contre un mur, sauter d'un rebord ; sur l'eau profonde, le héros marche encore (la nage est E20.i) |
| E20.e0 | Apparition d'entités et butin (D-E20-17) : `SpawnWarpEntity` (`0x80039F58`, 102 appelants) par indice de sprite global, tirage du butin à l'apparition (`0x80032A40`), lâcher à la mort (`0x80032B90`) avec son veto, objet ramassable (gestionnaire C255), avance du générateur pseudo-aléatoire une fois par tick après la mise à jour du monde (`0x8002BD14`) | oracle ; arcs | Rien de visible seul ; recette avec E20.e |
| E20.e | Effets du héros et herbe coupée : `CreatePlayerAnimationEffects` (pas, poussière de marche, de course et d'atterrissage), `CheckAndTriggerTileEffect`, butin tiré par E20.e0 | oracle ; arcs | Pas et poussière ; l'herbe se coupe à l'épée et à la charge ; argent et cœurs tombent et se ramassent |
| E20.f | Autres armes (D-E20-7) : charge et aura, fléau et rebond, baguettes, arcs, projectiles par l'apparition d'E20.e0 et les gestionnaires propres au héros, vague de l'épée C61 et flèches chargées C94 (D-E20-18), `0xC0`/`0xC1` | oracle ; arcs | Sauvegarde de test avec les armes (à créer) |
| E20.g | Porter, poser, lancer (`PlayerTryAction`, `UpdatePlayerCarriedEntity`) ; caisses et jarres redeviennent des obstacles (fin de l'écart D-E19-28/D-E19-46) ; barricade de la 392 (O-E20-5) | oracle ; arcs | Soulever et lancer une caisse ; la 392 |
| E20.h | Objets du Rond (`TryUseItem`) : graines, bombes (gestionnaire E12), magie, potions ; le couvercle de la 362 | oracle ; arcs | La 362 |
| E20.i | Nage (D-E20-16) : branche de pente 4 (`0x8003260C`), états `0x0F`, `0x1D`, `0x28`, `0x3A` et `0x3B`, plongée au front de bouton | oracle ; arcs | Nager et plonger en eau profonde (la 416 en a) |

- **Ordre** (d'origine ; pour les tranches qui restent après E20.c1b-2, il est **remplacé** par la révision du 2026-10-10 ci-dessous,
  seule à faire foi) : E20.0 → E20.a → E20.b → E20.c1a → E20.c1b-1 → E20.c1b-2 → E20.c2 → E20.d → E20.e0 → E20.e → E20.f → E20.g → E20.h → E20.i.
  E14.a se planifie dès qu'E20.c1b-1 est faite (ses gestionnaires appellent `ComputeNewHp`). Le hall de la 115 devient jouable avec
  E20.a, E20.b, E20.c1a, E20.c1b-1 et E14.a. E20.c1b est découpée en c1b-1 et c1b-2 le 2026-10-09, sur la proposition de la
  contre-vérification de sa découverte (`docs/plan-e20-annexe/e20-c1b/check.md`). Découpage d'E20.c1 en c1a et c1b décidé le 2026-10-09 sur la proposition de la
  contre-vérification de la découverte (`docs/plan-e20-annexe/e20-c1/check.md`).
- **Dépendances** (d'origine ; pour les tranches qui restent, **remplacées** par le tableau de la révision du 2026-10-10 ; les
  arêtes qui n'y figurent plus y ont un bouchon nommé) : tout dépend d'E20.a (le rythme par tick, le relâchement, et la structure du tick, dont
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

#### Révision de l'enveloppe pour les armes, les objets et les collisions (2026-10-10, mode AUTO)

**Révision 1.** Relecture : **REVISE**, deux points corrigés : le passage d'E17, C60 et E9 à E20.f changeait D-E20-18 sans
l'auteur (Q-N1 devient bloquante pour E20.f5 et E20.f6, les trois enregistrements `0x0C` de la 309 sont notés) ; l'ordre et les
dépendances d'origine sont marqués comme remplacés, et la bombe E12 rejoint les appelants de la coupe de l'herbe.
**Révision 2.** Seconde relecture : **REVISE**, un point corrigé (FIX) : E20.h3 et E20.h4 exigent E20.h0 (leur départ passe par
`0x8003499C` et le pilote `0x8003634C`) et, par lui, E20.c2a (le soin de l'eau). Une seule relecture de clôture suit.
**Révision 3 et pause.** La relecture de clôture rend **REVISE** (troisième) : E20.f3 doit exiger E20.f1, E20.g2 doit exiger
E20.e0b ; les deux points sont corrigés ci-dessus (FIX, preuves : `f-weapons.md` ligne 32, `h-g-deps.md` lignes 177, 195, 212). La règle
de relecture plafonne la boucle : la révision n'est **pas READY** et reste **en pause** jusqu'au retour de l'auteur, qui doit de toute
façon répondre à Q-N1 à Q-N3. Cette nuit ne tournent que les tranches sans prérequis de la ligne E20.f déjà approuvée (E20.f0, f1,
f2), chacune relue à part ; chaque relecture de tranche vérifie aussi ses prérequis contre les points d'entrée du binaire.

L'auteur a fixé l'ordre de la suite (D-E20-24 : armes, objets, collisions ; les prérequis avant chaque tranche). Une découverte en
lecture seule l'a préparé : `docs/plan-e20-annexe/e20-armes/` (cinq rapports et trois contre-vérifications : `e0-check.md`,
`f-check.md`, `order-critic.md`). Elle change l'enveloppe ainsi.

**Faits porteurs** (contre-vérifiés) :
- [données, binaire] **Les gestionnaires des projectiles du héros ne servent qu'au héros.** E17 (flèches), C60 et E9 (boules de feu
  et de glace), C61 et E10 (vague), C94 (flèche chargée) ne sont utilisés que par les sprites communs 3 à 15 et 27 à 29 ; aucun sprite
  de carte ne les utilise. Le seul autre lanceur, la torche (E20 `0x8007F6C8`), efface les six mots de programme de sa copie
  (`0x8007F740`-`0x8007F74C`) : C60 et E9 n'y tournent jamais. C54 (`0x80078E34`) est un gestionnaire d'ennemi (carte 326), sans
  rapport avec la baguette de feu. Trois enregistrements de la carte 309 placent le sprite commun `0x0C` (C60/E9) : en portant C60
  et E9 pour ce type, ils prendraient vie (fidèle, mais point de vigilance). Le critère de D-E20-18 (« propres au héros » → E20.f ;
  « que des ennemis partagent » → E14) donnerait donc E17, C60 et E9 à E20.f ; mais D-E20-18 les nomme pour E14 : changer leur
  propriétaire revient à l'auteur. **Question bloquante Q-N1** pour E20.f5 et E20.f6 ; C54 reste à E14.
- [binaire, données] **Rien n'associe un indice de sprite global à un préfab dans le projet exporté** : le convertisseur garde la
  correspondance en mémoire (`SpriteWriter.cs:394`, `:494`). Les bancs de carte sont dédupliqués globalement
  (`SpriteBankReader.cs:174-217`) : une seule table de 256 entrées par page. Un export qui ajoute `BankKey` et `BreakEffect` (octet
  `+0x1E` de l'en-tête, lu par `DestroyEntity` `0x8003A5E8`) à chaque entrée de `Data/sprite-records.json` sert la page globale, la page
  des cartes et D-E14-2 (avancé ici, prérequis de la flèche chargée, effet 8 ; à confirmer, Q-N2).
- [binaire] **Une entité née dans un tick y tourne aussitôt** : statut, programme A, compteurs, listes, animation, physique ; une
  flèche avance de 3 px dans son tick de naissance. La DLL la fait tourner une image plus tard (`TookPartInFrame`,
  `AlundraWorldProxy.cs:2617-2638`) ; la DLL peut l'inclure elle-même dans le reste du tick, sans changement du moteur (`e0-check.md`
  point 1). Cela répond à O-E20-11.
- [binaire] Le binaire réutilise la première case libre (1 à 63, plafond 63) ; la DLL ajoute en fin de liste (O-E20-13).
- [binaire] L'avance du générateur (`0x8002BD0C`-`0x8002BD34`) a lieu une fois par appel de la fonction d'image, même quand les
  entités sont gelées ; la DLL n'en a pas. E9 et l'étincelle de charge tirent dans le générateur seulement quand l'effet est créé.
- [DLL] Seul le héros a un pas vertical par tick (`AlundraScriptedMotion.cs:194`, `:198`) ; les flèches volent à Z constant avec
  `NoObstacleSlide`, les boules tombent, une flèche qui rebondit prend la gravité : il faut un pas vertical et de vol pour les autres
  entités (K0).
- [DLL] `RunSpriteEvent` ne tourne que le livre et E0/E1 ; le registre par type de sprite (D-E14-1) est un prérequis d'E20.f, d'E20.g et
  d'E20.h.
- [binaire] Porter précède une partie des objets : les graines et la bombe naissent portées (`hero+0x2C`, `0x80034224`, `0x8003453C`).
  Les soins écrivent les PV de l'entité du héros (`+0x14`), recopiés vers `PlayerStats` en fin de `MovePlayer` : ils demandent le
  modèle de PV d'E20.c2a.
- [binaire] La coupe de l'herbe (`CheckAndTriggerTileEffect`), appelée par le héros (`0x8002FDC0`), C61, C94 et la bombe E12
  (`0x8007F2D4`), reste à E20.e : bouchon nommé à sa place dans E20.f3, E20.f4 et E20.h3, porté en vrai par E20.e. Le
  rebond du fléau est une fin autonome de `CreatePlayerAnimationEffects` (`0x80030434`-`0x80030654`) : il n'attend pas E20.e.
- [DLL] La branche verrouillée de `MovePlayer` n'efface ni la charge ni le pointeur d'arme (`AlundraPlayerManager.cs:220-223`), alors
  que le binaire le fait (`0x80031BEC`, `0x80031BF8`) : un tir en attente part après le déverrouillage. Corrigé par E20.f1.

**Tranches, dans l'ordre de la nuit** (chacune détaillée, relue, exécutée et vérifiée avant la suivante ; elles touchent les mêmes
fichiers, donc l'une après l'autre) :

| Tranche | Contenu | Prérequis |
|---|---|---|
| E20.f0 | Moyens de test : `Preset` gagne l'arme, les PM et les PV ; aides `Give`/`Equip` de `ArcRun` ; opcodes `0xC0`/`0xC1` | — |
| E20.f1 | Charge et relâchement : `UpdatePlayerWeaponEffect`, branche de relâchement de `PlayerTryAttack`, gerbe de 16 étincelles, aura (effet 3 attaché) par un accès de l'hôte aux effets, effacements de la charge et du pointeur dans les branches gelée et verrouillée ; l'arc et la baguette arment le pointeur, le tir reste un bouchon compté | — |
| E20.f2 | Rebond du fléau : squelette de `CreatePlayerAnimationEffects(mode)`, fin du fléau réelle, le reste en bouchons nommés pour E20.e | — |
| E20.e0x | Convertisseur : `BankKey` et `BreakEffect` dans `Data/sprite-records.json` ; export complet, preuve par manifeste et double export ; ADR de format | — |
| E20.e0a | Apparition par indice de sprite (`0x80039F58`) : table des préfabs par page, entité sans enregistrement (`EntityRefId = -1`, parent, racine), cases réutilisées (première libre, plafond 63), participation au tick de naissance ; le registre des natifs par type et son interrupteur (D-E14-1) viennent d'E14.a0, qui les possède (`docs/plan-e14-ia-native.md` ; les aides des ennemis sont E14.a0b, plus tard) | E20.e0x, E14.a0 |
| E20.e0b | Générateur avancé une fois par tick ; tirage du butin à la création (écrit le contenu, rien ne tombe) ; `DestroyEntity(e, fx)` avec l'effet de bris | E20.e0a |
| E20.e0k | Pas vertical et vol des entités autres que le héros (gravité `0x100`, impulsion, atterrissage, vol à Z constant, `NoObstacleSlide`, butée sur mur, bord et hauteur) | E20.e0a |
| E20.f3 | Tir : `SpawnWeaponProjectile` branché, un type à la fois, jamais avant son gestionnaire E ; vague C61 (coupe d'herbe en bouchon) et E10 ; effets de coup 4 et 5 de la passe de coups (porte `(b & 0xC0) != 0x80`, `0x800395E8`) | E20.f1 (le relâchement chargé lance la vague et la flèche chargée), E20.e0a, e0b |
| E20.f4 | Flèche chargée : C94, E1 avec l'effet 8 | E20.f3, e0k |
| E20.f5 | Flèches : E17 | E20.f3, e0k ; **réponse à Q-N1** |
| E20.f6 | Boules de feu et de glace : C60, E9 ; vigilance sur les trois enregistrements `0x0C` de la carte 309 | E20.f3, e0b, e0k ; **réponse à Q-N1** |
| E20.c2-0, E20.c2a | Oracle d'E20.c2 ; modèle de PV sur l'entité, fin de `MovePlayer` sur toutes les sorties, `UpdateItemEffectState` (D-E20-19 à D-E20-22) — pas la blessure | — |
| E20.h0, E20.h1 | Cadre de l'usage d'un objet (`0x8002ED64`, `0x8003499C`, pilote `0x8003634C`, annulation `0x800350C0`) ; les cinq objets instantanés (soins) | E20.c2a |
| E20.g1, E20.g2 | Soulever et porter ; lancer et briser | E20.e0a, e0k ; E20.e0b pour g2 (`DestroyEntity(e, -1)` avec l'effet de bris et l'effacement du porteur `+0x2C`) |
| E20.h3, E20.h4 | Graines et bombe (coupe de l'herbe par la bombe en bouchon nommé jusqu'à E20.e) ; magies par élément | E20.h0 (cadre de l'usage : `0x8002ED64`, `0x8003499C`, pilote `0x8003634C`), donc E20.c2a ; E20.g1, g2 |
| E20.g3, E20.g4 | Caisses et jarres redeviennent des obstacles ; la barricade de la 392 | E20.g1, g2 ; Q-N3 |

Sans réponse à Q-N1, la nuit passe d'E20.f4 à E20.c2-0 et laisse E20.f5 et E20.f6 en attente. Ensuite : butin et objet ramassable
(E20.e0c, avec E20.e ou E14.a), E20.e, E14.a, la blessure (E20.c2b), E20.d, E20.i.

**Questions pour l'auteur.**
- **Q-N1 (bloque E20.f5 et E20.f6)** : D-E20-18 donne E17 (flèches) et C60 (glace) à E14, sur la prémisse que des ennemis les
  partagent ; le recensement montre qu'ils ne servent qu'au héros (avec E9), C54 étant un gestionnaire de boss. Passent-ils à E20.f ?
  Recommandé : oui ; sinon, l'arc et les baguettes restent inutilisables jusqu'à E14.
- **Q-N2 (non bloquante)** : l'export de l'effet de bris (D-E14-2) avance d'E14.a à E20.e0x, dans le même export que `BankKey` : c'est
  un prérequis d'E20.f4 (C94 et E1, déjà à E20.f par D-E20-18). La session principale l'avance et le note. Recommandé : oui.
- **Q-N3 (bloque E20.g3 seulement)** : « collisions » désigne-t-il bien E20.g (caisses et jarres solides), ou aussi l'eau profonde
  qui doit bloquer le héros sans les bottes (O-E19-22, sans propriétaire) ?
- Par défaut fidèle, sans question : le plafond de 63 cases, la participation au tick de naissance (dans la DLL), la flèche normale qui
  vole 16 px au-dessus du sol du héros et passe au-dessus des ennemis bas, la cape de sable reportée (elle n'est dans aucune tranche).

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

### 1.5 E20.c1a — La passe de coups 🧪 (faite et CONFIRMED le 2026-10-09, `7c1b0b3`, `42d2e15` ; reste la recette en jeu de l'auteur)

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

- ✅ **C1 — Fiches d'équilibre.** Charger `Data/balance.json` une fois (table mise en cache, accès par `IAlundraScriptHost`, comme les
  tables d'objets) ; choisir la fiche comme `0x80044550` avec le seuil `D` de la carte ; à la création de chaque entité, poser la fiche,
  `Hp` et `HpMax` ; le héros prend la fiche 0. Le cas où la DLL ne trouve pas l'en-tête du sprite est documenté.
- ✅ **C2 — Attaque courante et boîte de l'image au tick.** À chaque changement d'animation que l'horloge voit (`StepAnimationClock`,
  fin enchaînée, changement de direction, animation de départ), recharger `CurrentAttack` et l'octet d'animation (`+0xB4`) pour la passe
  de coups ; tenir, par entité, le curseur (animation, tick logique) qui donne la boîte de l'image courante d'après les
  `collision_keyframes` (règle de D6), y compris au tick du changement et dans les ticks d'une image de rattrapage. Ce qui change en fin
  d'image (`SyncAnimation`, D-E19-13) ne change pas.
- ✅ **C3 — La liste et la passe.** Construire la liste au pas « listes » du bloc des entités (statut 2 ou 3, `BlockedByEntity` nul, le
  héros d'abord, ordre de `_spawnedEntities`) ; ajouter, entre la physique et le point d'accroche, une place vide pour `0x80038E84` ;
  porter `UpdateBalanceRecords` au point d'accroche : figer l'animation du héros (`0x80146DF8`) même avec une liste vide, en copiant
  le `TargetAnimationId` du héros tel qu'il est au point d'accroche, après l'horloge d'animation du tick (jamais `CurrentAnimationId`,
  qui ne change qu'en fin d'image) ; masque de
  classes réordonné ; cibles sautées ; recouvrement élargi d'une unité, `HitBox` de l'attaquant contre `Pos + Mod` de la cible ;
  réponse de la cible (`(r & 0xC0) == 0x80` : pas de `TouchingEntity`) ; `+0x1D0 = 0x19` ; compteur de l'attaquant ; plusieurs cibles,
  premier attaquant.
- ✅ **C4 — Étincelles.** Trois effets 9 au centre du recouvrement, par la réserve d'effets et l'étage d'effets du tick, avec les trois
  tirages du générateur pour chacun quand l'effet a pu être créé (même formule que le vase de vie, les trois axes à `0x20001`).
- ✅ **C5 — Tests.**
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

#### Résultats (2026-10-09)

- **Commits** : `7c1b0b3` (C1, C2) et `42d2e15` (C3, C4, C5), regroupés parce que C3 à C5 ne sont pas verts séparément.
- **Code** : `AlundraBalanceTables` (chargée une fois par projet, accès par `IAlundraScriptHost.BalanceTables` et `BalanceThreshold`, seuil
  `D` lu dans la carte) ; `InitializeBalance` aux trois lieux de création (héros, carte, `0x2D`) ; un fichier partiel
  `AlundraEntityScriptProxy.HitPass.cs` (fiche, attaque courante, octet `+0xB4`, boîte, curseur de l'image) ; `AlundraFrameBoxTimeline`
  (règle de D6) ; `StepAnimationClock` recharge l'attaque, l'octet et le curseur au changement, au tick dû, à la fin enchaînée et à la
  création, sans toucher à `SyncAnimation` ; `AlundraHitPass` (passe pure) ; la liste au pas « listes » ; une place vide pour
  `0x80038E84` (`RunActiveEffectsPass`) ; `AlundraNativeEffects.CreateHitSparks` ; `TouchingEntity` reçoit l'entité du moteur de
  l'attaquant (`LogicContextEntity ?? Owner`).
- **Oracle** : égal sur `H1`, `H1-near`, `H1-edge`, `H2`, `H4a`, `H4b`, `H5`, `H7`, `H8` en entier ; `H3` (ticks 1 à 18) et `H1-both`
  (ticks 1 et 2) avec les exclusions prévues ; `H6` avec un Beannoïde reconfiguré sur la fiche 382 (le sprite de la carte 69 manque à la
  14), sans comparer la boîte ni le programme de la cible ; les bords de la portée (aire décalée d'une case pour éviter un mur). Un
  héros touché par un Beannoïde l'est aux ticks 1, 26, 51 et 76, `+0x1CC` restant à 0 (attente de c1a jusqu'à E20.c2). À dx −10, la DLL
  touche au tick 12 alors que l'oracle ne touche pas : le héros de l'oracle est blessé au tick 2, ce qui coupe son coup (E20.c2).
- **D2** : cas 1, 2, 5, 6 et la partie de 3 sur la passe. **Rattrapage** : `H1` et `H1-near` rejoués à 2, 3 et 4 ticks par image et en
  calages mêlés ; le curseur égale le tick logique du sprite après chaque image. **Octet `0x40`** au tick du contact, dans les deux sens,
  aussi par le tick dû et en images de deux ticks. **Arc réel** sur la 14 : un Beannoïde touché au tick 5, D au tick 6, PV inchangés.
- **Tests** : `Alundra.Tests` 3402 sur 3402 en Release et en Debug ; traces du héros et de l'intro inchangées ; un seul test existant
  modifié, l'ordre du tick d'`AlundraTickStructureTests`, qui gagne la place de `0x80038E84` entre la physique et la passe.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : règles redécodées dans le binaire, trois mutations attrapées.
  **Coups dans le vrai jeu** : aucun dans l'intro ni sur les arcs de la chaîne (389 jusqu'au drapeau 860, 390, 391, 392, 179), et aucun
  possible : sur ces cartes, aucun masque de classe n'atteint le héros ; le seul coup de toute la suite est la petite boule de fer de
  la 127 sur le héros, fidèle au binaire. La sonde du vrai jeu montre la même nouvelle partie. P4 reportés : l'élargissement d'une unité
  n'est prouvé que par le cas 2 de D2 ; un test parcourt la plus courte des deux listes de cibles ; les tirages des étincelles se font
  quand l'étage les applique (E20.e0) ; `Pos + Mod` lu à la passe plutôt que la copie de la physique.
- **Reste** : la recette en jeu de l'auteur sur la carte 14 (sans risque : l'entité de la 14 dont le programme D teste `0x87` est
  immunisée contre l'épée). La découverte de c1b a trouvé une régression introduite par c1a (P2) : un coup fait tourner le programme D
  de la cible, et des programmes D commencent par `0x87`, que la DLL sautait ; frappés à l'épée, les interrupteurs de feu des cartes 92,
  110, 303, 337 et 343 décidaient donc sur un `Result` périmé. E20.c1b-1 la ferme (§1.6).

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un test existant qui change sans raison du binaire ; un besoin de
  toucher au moteur ; un changement de l'ordre de fin d'image (D-E19-13) qui ne serait pas propre à la passe.
- **Budget** : un `executor`, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de correction.
- **Retour arrière** : `git revert` des commits d'E20.c1a.

### 1.6 E20.c1b-1 — Dégâts, équipement et opcodes des coups 🧪 (faite et CONFIRMED le 2026-10-09, `745c27d`, `3051332`, `80c71b3` ; reste la recette en jeu de l'auteur, quand une carte à interrupteurs de feu sera atteignable)

**Révision 1 (2026-10-09).** Relecture : **REVISE** : la cible du test de régression (l'entité de la 14 au programme D `@544`) est
immunisée contre l'épée, le test ne pouvait pas échouer. Corrigé : cible réelle touchable (l'interrupteur de feu de la 303, entité 4),
`Result` partagé non nul posé avant le coup, échec constaté avant K4, recette réécrite.
Relecture neuve de la révision 1 : **READY** (2026-10-09). Exécution lancée sans soumission à l'auteur, à sa demande.

**But.** Les fonctions de dégâts du binaire existent dans la DLL et donnent les mêmes PV que l'oracle ; les trois fiches d'équipement
sont tenues comme dans le binaire ; les opcodes `0x71`, `0x86`, `0x87` et `0x95` sont portés. Surtout, la régression introduite par E20.c1a
est fermée : depuis c1a, un coup pose `TouchingEntity`, donc le programme D de la cible tourne ; 181 de ces programmes commencent par
`0x87`, que la DLL sautait, si bien qu'ils décidaient sur un `Result` périmé (P2 introduit par c1a, trouvé par la contre-vérification de
la découverte de c1b, bloquant : corrigé ici en priorité).

**Faits porteurs** (découverte du 2026-10-09, `docs/plan-e20-annexe/e20-c1b/`, contre-vérifiée) :
- [binaire] `ResolveBalanceTarget` (`0x8004464C`) : fiche nulle, attaque nulle ou attribut 0 : PV inchangés ; `r` = octet `2 + (attr &
  0xF)` de la fiche de la cible ; `r & 0xC0 == 0x40` : 0 ; `0x80` ou `0xC0` : inchangés ; sinon `((puissance + somme des fiches
  d'équipement si attr & 0x80) × r) >> 4`, au moins 1, PV planchers à 0. Pour chaque fiche d'équipement non nulle :
  `Attacks[g + 1 < count ? g + 1 : 0].Power`, `g` étant l'animation figée `0x80146DF8`. Le terme d'équipement vaut pour tout attaquant,
  pas seulement le héros. `ComputeNewHp` (`0x8003A374`) : la fiche est `0x80127008` pour le héros (E20.c2), `+0x1C4` sinon ; l'attaque
  est le `+0x1C8` du `TouchingEntity` au moment de l'appel, sans test de nullité ; il rend `PV == 0`.
- [binaire] Fiches d'équipement (`0x800307E8`, étapes 2 à 5) : effacement des six mots `0x80146E00`-`0x80146E14` ; fiche 0 = l'armure
  possédée de plus haut numéro (catégorie 2, balayage de `0x61` à 0) ; fiche 1 = l'arme (`0x8004E030`, catégorie 1) ; fiche 2 = l'objet
  (`0x8004E0F8`, catégorie 3), qui réécrit `ItemId` ; catégorie = `byte[0x800C5F7C + 8 n + 6] & 0x7F`, déjà exportée
  (`AlundraItemTables.DropField3`, égale au binaire sur les 98 lignes) ; fiche de l'objet `n` = entrée `n + 0x1E` sans le remappage de
  `0x80044550` (`0x800445C0`, garde `n < 0x62`) ; les trois pointeurs sont remis à zéro à chaque chargement de carte (`0x80044520`).
- [binaire] Opcodes : `0x71` (taille 1) : `Result` = `HitCounter` de l'entité ; `0x86` (taille 4, `[recherche, lo, hi]`) **pose**
  `+0x1CC = lo | hi << 8` sur chaque entité trouvée, sans toucher `Result` ; `0x87` (taille 3, `[recherche, typeIdx]`) : `Result` 1 si une
  entité trouvée a un `TouchingEntity` dont la boîte et l'attaque existent, l'attribut non nul et le quartet égal à
  `byte[0x80098FA4 + typeIdx]`, sinon 0 ; `0x95` (taille 10, `[x, y, z, wx, hy, _, maskLo, maskHi, typeIdx]`) : rectangle X = x·24,
  Y = y·16, Z = z·16 px, tailles X et Z = wx·24, Y = hy·16 ; masque nul : 0 ; balaie les cases 0 à max (statut 2 ou 3, `+0x20 == 0`,
  boîte, attaque non nulle, `Flags & masque`, quartet) avec la `HitBox` **rangée** (`+0x1FC`, écrite par `0x80038064` après le mouvement
  et à la création) et le recouvrement élargi d'une unité. La table des quartets a 12 octets lus : `01 02 03 04 05 05 06 07 58 D1 03
  80` (les indices 8 à 11 de la carte 226 lisent au-delà ; seul l'indice 10 peut correspondre, comme le quartet 3).
- [binaire] Les programmes A, D, E et F partagent un état global (`0x8013D9E8`, `Result` en `0x8013DA14`) ; la DLL le modélise déjà
  (`_slotAScratchState`) ; 181 des 182 sites de `0x87` sont dans des programmes D.
- [oracle] `docs/plan-e20-annexe/e20-c1b/oracle/out/` (`python -B docs/plan-e20-annexe/e20-c1b/oracle/tools/run_c1b.py`) : K1 (fiches et
  feuille du héros pour six inventaires), K2 (PV d'un Beannoïde touché pour chaque inventaire, réponses 0, `0x40`, `0x80`, `0xC0`,
  attaque sans le bit `0x80`, attaquant autre que le héros, règle d'index sur une fiche de trois attaques), K3 (les quatre opcodes, cas
  limites compris). Avec l'épée d'acier ou l'épée sacrée, le héros frappe avec `0x3F` ou `0x49` : la puissance de l'arme compte deux
  fois (attaque et fiche).

**Périmètre.** DLL `Alundra/Scripts/` : une classe des fonctions de dégâts (pures, entrées passées en paramètres), la sélection d'une
fiche d'équipement, les trois fiches tenues par le monde avec un accès par l'hôte et par le contexte d'entité, leur remplissage dans
`UpdateItemEffectState` (étapes 2 à 5 seulement) et leur effacement au chargement de carte, la tenue de `HitBox` à chaque tick pour
toute entité qui a une boîte et à la création, les quatre opcodes, la table des quartets ; l'étiquette fausse de `0x86`
(`EventOpcodeSizeTable.cs`) et les commentaires périmés (`AlundraEventProgramRunner.cs` vers 747, `AlundraWorldProxy.cs` vers 2727) ;
tests ; ce plan.

**Hors périmètre.** Les arcs des leviers (E20.c1b-2) ; la feuille du héros `0x80127008`, sa combinaison, les bottes, la régénération et
les PV du héros (E20.c2) ; tout appel de `ComputeNewHp` en production (gestionnaires D natifs : E14.a ; entrée du héros : E20.c2) ;
`0xB9` (reste sauté, question déjà ouverte en E19) ; le couvercle de la 362 (E20.h).

#### Tâches

- ✅ **K1 — Fonctions de dégâts.** `ResolveBalanceTarget` et `ComputeNewHp` en fonctions pures (fiche de la cible, attaque, PV, les trois
  fiches d'équipement, l'animation figée) ; l'octet 2 de la fiche pour un quartet 0.
- ✅ **K2 — Fiches d'équipement.** Sélection sans remappage (`n + 0x1E`, rien au-delà de `0x61`) ; les trois fiches remplies à chaque
  tick dans `UpdateItemEffectState` (le reste du bouchon demeure pour E20.c2) ; le tick devient un second appelant de la réécriture
  d'`ItemId`, comme le binaire ; effacement au chargement de carte.
- ✅ **K3 — `HitBox` rangée.** Écrite après le mouvement de chaque tick pour toute entité qui a une boîte (`0x80038064`, appelée par
  `0x80038364`) et à la création (`0x80039F30`) ; la passe de coups de c1a garde son résultat (le verifier frais vérifie qu'aucun test de
  c1a ne bouge).
- ✅ **K4 — Opcodes.** `0x71`, `0x86` (pose), `0x87`, `0x95` (avec la `HitBox` rangée, le +1 de Z de l'ADR-0026 sur le seul rectangle, la
  table de 12 octets) dans `Dispatch`, tailles 1, 4, 3 et 10 ; corriger l'étiquette de `0x86` et les commentaires périmés.
- ✅ **K5 — Tests.**
  - Contre l'oracle : K1 (les trois fiches de chaque inventaire), K2 (chaque valeur de PV, avec l'état réel de chaque arme), K3 (chaque
    cas des quatre opcodes, indices 8 à 11 compris).
  - Les cas de D2 sur les PV : la moitié de 3 (`0xC0` laisse les PV) et 4 (dégât minimal).
  - `0x87` et `0x95` exécutés dans de vrais programmes D, par l'interpréteur et son état partagé.
  - **Régression de c1a fermée**, sur une vraie cible que l'épée touche : l'interrupteur de feu de la carte 303, entité 4 (sprite 51 de
    la carte, fiche d'équilibre 307, réponse 16 au quartet 1 de l'épée, donc ni `0x80` ni `0xC0` ; `MoreFlags` 129, classe A, que le
    masque du héros atteint ; programme D `@700` = `0x87 [128, 6]` (le feu), puis un saut si `Result` est nul, puis la pose d'un drapeau
    et `0x2B` ; données : `data-extracted/data/map_303.json`, `alundra-project/Data/balance.json`). Le test place d'abord un `Result`
    partagé non nul (l'état partagé des programmes A, D, E et F, `_slotAScratchState`), puis le héros frappe l'interrupteur à l'épée :
    sur le code d'avant K4, le drapeau est posé (on le constate avant d'ajouter K4) ; avec K4, `0x87` rend 0 et rien n'est posé. Mêmes
    interrupteurs sur les cartes 92, 110, 337 et 343 (recensement par `0x87` en tête du programme D, cible touchable par l'épée : 60
    entités sur 143).

#### Acceptation

- Build Release puis Debug ; `Alundra.Tests` sans échec ; aucun changement du moteur ; traces du héros et de l'intro inchangées ;
  aucun test de c1a ne change.
- K5 égale l'oracle sur chaque valeur nommée.
- Verifier frais.
- Recette en jeu de l'auteur : rien de nouveau à voir tant qu'aucune carte à interrupteurs de feu (92, 110, 303, 337, 343) n'est
  atteignable ; alors, un coup d'épée sur un interrupteur de feu fait des étincelles et ne l'allume pas.

#### Résultats (2026-10-09)

- **Commits** : `745c27d` (K1, K2), `3051332` (K3), `80c71b3` (K4, K5).
- **Code** : `AlundraDamage` (`ResolveBalanceTarget` et `ComputeNewHp`, pures) ; `AlundraEquipmentRecords`, tenues par le monde et
  lues par l'hôte et le contexte d'entité ; `AlundraBalanceTables.SelectEquipment` (entrée `n + 0x1E` sans remappage, rien au-delà de
  `0x61`) ; `FillEquipmentRecords` dans `UpdateItemEffectState` (étapes 2 à 5, le reste du bouchon demeure pour E20.c2) ; effacement au
  chargement de carte ; `StoreHitBox` après la physique de chaque tick et à la création ; `AlundraHitOpcodes` (`0x87`, `0x95`, table de
  12 octets) ; les cas `0x71`, `0x86`, `0x87`, `0x95` de `Dispatch` ; l'étiquette de `0x86` et les commentaires périmés corrigés.
- **Oracle** : K1, K2 et K3 égaux sur chaque valeur nommée (les 96 vérifications de K3 dans l'oracle, deux interpréteurs) ; cas 3 (la
  moitié sur les PV) et 4 de D2 égaux.
- **Régression de c1a fermée** : le test des interrupteurs de feu de la 303 (programmes D `@700` et `@724`) échoue sans le cas `0x87`
  (constaté par l'exécuteur avant K4, puis par le verifier en désactivant le cas : `0x87` sauté, `Result` partagé 7, classe A perdue) et
  passe avec.
- **Tests** : `Alundra.Tests` 3480 sur 3480 en Release et en Debug ; traces du héros et de l'intro inchangées ; aucun test de c1a
  modifié ; trois lignes de la carte 10 retirées de `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` (convention d'E19 : un opcode
  porté sort de la liste, qui est maintenant vide).
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : règles redécodées dans le binaire (`0x8004464C`, `0x8003A374`,
  `0x800307E8`, `0x800445C0`, `0x80044520`, `0x8003FA10`, `0x8003FFD4`, `0x80040048`, `0x800407C0`, `0x80038064`) ; trois mutations
  attrapées (cas `0x87`, +1 de Z de `0x95`, `StoreHitBox`) ; tests d'oracle non tautologiques ; sonde du vrai jeu inchangée.
- **Écarts acceptés** : `0x95` balaie toutes les entités apparues, le héros d'abord (même ensemble que les cases 0 à la plus haute) ;
  `ComputeNewHp` sans `TouchingEntity` laisse les PV (le binaire lirait l'adresse `0x1C8` ; aucun appelant en production) ; les
  positions des entités de K3 sont posées par l'exécuteur, chaque résultat attendu étant une ligne de K3.
- **Reportés** :
  - P3 : `MapsWithoutSkippedSite` (`Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:49`) compte 28 cartes au lieu de 30 (la 164 manque
    depuis E19.l1, la 10 depuis c1b-1) ; le site `(10, C, 75, 6418)` de `KnownScenePredicateSites` et les commentaires « 0x95 not
    ported » (`AlundraEntityContactArcTests.cs:173`, `AlundraHeroJumpArcTests.cs:66`) sont périmés. Tests seulement : E20.c1b-2.
  - P4 : la `HitBox` rangée est écrite pour toute entité dont la physique tourne, le binaire ne le fait que pour la liste de physique
    (`0x80127B28`) ; la passe de c1a recalcule la boîte au lieu de lire la copie rangée (même valeur tant que `0x80038E84` ne déplace
    rien) : à revoir en E20.e ou E14.
  - P4 : `AlundraEquipmentRecords.Clear()` efface aussi les mots d'objet, `0x80044520` seulement les pointeurs : E20.c2.
  - P4 : un index de type au-delà de 11 rend −1, le binaire relirait plus loin (aucun site connu au-delà de 11).
  - P4 : un changement d'animation annulé en fin d'image entre le coup et le programme D remettrait `HasFrameBox` à faux (spéculatif,
    non reproduit).
- **Reste** : la recette en jeu de l'auteur, quand une carte à interrupteurs de feu (92, 110, 303, 337, 343) sera atteignable.

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un test existant qui change sans raison du binaire ; un besoin de
  toucher au moteur.
- **Budget** : un `executor`, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de correction.
- **Retour arrière** : `git revert` des commits d'E20.c1b-1.

### 1.7 E20.c1b-2 — Les leviers réagissent au coup d'épée 🧪 (faite et CONFIRMED le 2026-10-09, `faaf00f` à `1c2de24` ; reste la recette en jeu de l'auteur, quand les salles seront atteignables, O-E20-14)

Relecture : **READY** dès la première lecture (2026-10-09). Exécution lancée sans soumission à l'auteur, à sa demande.

**But.** Prouver, par des arcs au vrai préfab et au vrai contrôleur, que les leviers de la 115 nord-ouest et de la 116, la bascule et
le levier 22 de la 117 et les leviers de la 44 réagissent à un coup d'épée exactement comme leurs programmes le disent : drapeaux,
animations, réécritures de cases, perte et reprise de la main, au tick près quand le bytecode le fixe. Aucune ligne de production
n'est attendue : depuis E20.c1a et E20.c1b-1, tous les opcodes de ces chemins sont portés ; un écart trouvé est un défaut à
diagnostiquer, jamais une attente à refaire. La tranche ferme aussi le P3 reporté d'E20.c1b-1.

**Faits porteurs** (découverte du 2026-10-09, `docs/plan-e20-annexe/e20-c1b2/` : trois rapports et leur contre-vérification
`check.md`, qui les corrige ; elle corrige aussi `e20-c1b/faits-leviers-opcodes-dll.md`, dont les « cases » sont les cases d'affichage
`XPos/2`, `YPos/2`, et dont le « placement direct » est impossible) :
- [DLL, binaire] Un enregistrement n'apparaît au chargement que si le bit `0x40` de `SpriteDirection` est posé et que la case du héros,
  à ce moment, est dans la zone `XMin..XMax × YMin..YMax` de l'enregistrement (`AlundraEntitySpawnFactory.cs:79-155`, appelé par
  `AlundraWorldProxy.cs:857-875` ; binaire `0x8003A20C`-`0x8003A278`, seul l'appel du chargement `0x8003B308` passe `a2 = 0`).
  `ArcSpec.HeroTileX/Y` est écrit après `InitializeWithWorld` (`AlundraArcSupport.cs:209`, puis `:249-261`) : chaque arc part donc d'un
  `Arrival` dans la zone. Zones : 115 enregistrement 2 : x 0..9, y 5..16 ; 116 enregistrements 39 et 40 : x 0..7, y 0..27 ; 117
  enregistrement 0 (bascule) : x 17..34, y 4..16 ; 117 enregistrement 22 : x 0..16, y 33..59 ; 44 : toute la carte. Les
  enregistrements 0 et 1 de la 115 (sprite 42) et 7 et 8 de la 116 (plaques) n'apparaissent jamais (bit `0x40` effacé, aucune
  activation).
- [DLL, binaire] Position d'un enregistrement : `(XPos·12 + 12, YPos·8 + 8, Height·8)` px (`EntityRecordMapper.cs:177-204`, binaire
  `0x8003A2B0`-`0x8003A2E0`). Leviers : 115 enregistrement 2 en (60,120), Z 24 ; 116 enregistrements 39 et 40 en (864,424) et (1104,424),
  Z 128 ; 117 bascule en (528,192), Z 16 ; 117 enregistrement 22 en (96,640), Z 32 ; 44 enregistrements 7 à 12 en (144,440), (432,472),
  (384,936), (672,376), (1080,904), (936,408), Z 128. Corps 24 × 16 × 16, décalage (−12, −8, 0) ; la bascule est un obstacle, les
  leviers non.
- [binaire, DLL] `0x37 Wait n` passe n + 1 ticks après son premier appel (`0x8003E3DC`, `Wait` `AlundraEventProgramRunner.cs:2601`) ;
  un drapeau temporaire `Tn` est `0x8000 + n`, effacé à chaque entrée de carte (`AlundraGameState.cs:351`), donc `ArcSpec.Flags` ne
  peut pas en poser.
- [DLL] Ordre du tick : événements de carte, puis `MovePlayer` et les programmes des entités, compteurs, listes, horloge, physique,
  passe de coups. Le programme D de la cible tourne au tick qui suit le coup (H + 1), C reprend à H + 2 ; un drapeau posé par C ou D est
  vu par un programme B au tick suivant. `0x1C [1]` après un `0x1A` du même tick s sur une animation tenue de L ticks passe à s + L + 1
  (épingle T-D1 d'E19).
- [données] Masque de coups du héros `0x801` ; fiches 305 (leviers) et 308 (bascule) : réponse 16 aux quartets 1 à 7. L'épée de base
  touche à l'appui même aux placements retenus (modèle de portée qui redonne le balayage de l'oracle de c1a : contact au tick 1 pour
  dx −9 à 38).
- [DLL] Aucun opcode sauté sur ces chemins ; le seul opcode non porté de ces quatre cartes est `0xB9` (117 B[1] `@171`, scène du
  livre), qu'aucun arc n'atteint.
- [DLL] Ce qui tourne à la charge : 115, l'événement 0 seul (B[1] referme la porte au premier tick) ; 116, B[1] (le pont, muet tant que
  T1 et T2 ne sont pas posés tous deux ; sa première écriture `0x85` change la case (42,24), de GP 18 à GP 2) et B[2] (son 149) ; 117
  salle de la bascule, B[2] prend la main 24 ticks et B[5] joue des sons ; 117 salle du levier 22, B[6] (sons) ; 44 avec G1633 et sans
  G1634, rien près des leviers (la vague B[3] ne part que sur la case (43..44, 20)).
- [décompilation, à confirmer au binaire] Les cases autour des leviers de la 116 ont la propriété de sol 2, que la DLL lit comme une
  pente 1 (`Slope_18c`) ; le `switch` de `MovePlayer` n'en fait rien et le coup reste permis. Le binaire tranche (tâche L0).

**Périmètre.** Tests seulement (`Alundra.Tests/`) : un arc par réaction, des aides de test si besoin (enregistreur de sons horodaté,
lecture d'un rectangle de cases, noms des mondes) ; le P3 reporté d'E20.c1b-1 ; ce plan.

**Hors périmètre.** Le couvercle de la 362 (E20.h) ; les plaques et la grille de la 116 et les Beannoïdes (E14.a) ; la vague de la 44 ;
l'atteignabilité en jeu des quatre salles (O-E20-14) ; la pente des cases (aucun accès de lecture : ce serait un changement de
production) ; le Z du pilier de la 117 ; `0xB9`.

#### Règles communes des arcs

- `ArcSpec(…, RealController: true, Prefabs: true, Arrival: …)`, animation d'arrivée `ResetAnimationId` ; nom unique par arc
  (`E20c1b2-…`) ; `AssertNoUnexpectedError` et `AssertNothingSkippedOrExceeded` dans chaque arc.
- Avant l'appui : héros `IsOnGround == 1`, `PlayerControlFlags == 0`, `TargetAnimationId == 0`, position égale au placement.
- H est `arc.Frame` avant `Press(Square)`. Les images de la trace sont absolues : on affirme les écarts entre images de trace.
- Drapeaux temporaires lus par `ArcChecks.IsTemporarySet(n)` ou par les pc de la trace (`FramesOf`), jamais par `GetFlag(0x8000 + n)`,
  qui rend le mot entier. Cases lues par `AlundraCellStore` (`GetFloorTileId`, `GetHeight`, `GetWalkability`, `GetGroundProperty`,
  `GetWallTileStack`). Les sons sont facultatifs (enregistreur injecté, `Contains`).
- Toute valeur est écrite avant l'exécution. Les valeurs marquées INFERENCE reposent sur l'horloge d'animation ou la marche des
  portes ; un écart sur elles arrête aussi (constat d'horloge ou de mouvement hors c1b-2), on ne refait jamais l'attente sur le
  résultat.

#### Tâches

- ✅ **L0 — Fumée et contrôle du binaire.** (2026-10-09. Fumée : `AlundraLeverSmokeTests`, cinq salles, chacune construite en 0,5 à 1,3 s (carte 117 : 0,7 à 1,3 s), six images en 0,05 à 0,07 s de plus, aucun écart. Binaire : le coup d'épée est permis sur la pente 1, voir `e20-c1b2/l0-pente.md` : l'entrée 1 de la table de `MovePlayer` (`0x80022D1C`) va à `0x80031D68` comme les entrées 0, 2, 3, 5 et 7 ; `PlayerTryAttack` ne lit jamais `+0x18C`.)
  - Un arc de fumée par salle (115, 116, 117 bascule, 117 levier 22, 44) : construction, 6 images, `AssertNoUnexpectedError`,
    enregistrements attendus présents, absents ceux qui ne doivent pas apparaître ; temps d'un arc mesuré.
  - Décoder dans `ALUN_CD.EXE` le traitement de la pente du héros (`+0x18C`) par `MovePlayer` (`0x80031B50`) et `PlayerTryAttack` pour la
    valeur 1 : si le binaire y empêche le coup d'épée, arrêt. Le résultat va dans l'annexe `e20-c1b2/`.
- ✅ **L1 — 115, levier nord-ouest** (arc A ci-dessous). (2026-10-09. `AlundraLeverArcTests.A_Map115_…` : toutes les valeurs de l'arc A égales à l'attente écrite d'avance, y compris les INFERENCE (Z du levier 24, passage de `0x1C` à H+33, écarts 21, 21, 21) ; un arc : 0,5 s.)
- ✅ **L2 — 116, les deux leviers et le pont** (arc B). (2026-10-09. `AlundraLeverBridgeArcTests.B_Map116_…` : toutes les valeurs de l'arc B égales à l'attente, y compris H1+33 (INFERENCE) ; un arc : 0,5 s. Nuance de lecture : les pcs de `B[1]` sont filtrés sous 300, car l'`End` de `B[2]` (`@308`) se répète à chaque tick dans l'emplacement B ; ce n'est pas un écart du plan.)
- ✅ **L3 — 117, la bascule, quatre coups** (arc C). (2026-10-09. `AlundraLeverBasculeArcTests.C_Map117_…` : toutes les valeurs de l'arc C égales à l'attente, y compris N+9 (INFERENCE), les quatre états des plateformes, les directions finales 24, 0, 8, 16 et les tuiles des piles de murs des gabarits. Les coups 3 et 4 sont frappés exactement 27 ticks après le précédent ; le coup 2, 28 ticks après le 1er, dont on a lu la classe A à N+27. Lecture de la convention : le plan écrit « T0 = 1 », le premier TICK de la carte ; l'image de trace est numérotée depuis 0, donc le programme de chargement `B[2]` passe son pc 284 à l'image de trace 0 (la première image jouée) ; toutes les autres valeurs sont relatives à T0 et sont égales. Pas une divergence de comportement.)
- ✅ **L4 — 117 levier 22 et 44 levier 7** (arcs D et E). (2026-10-09. `AlundraLeverPillarArcTests.D_Map117_…` et `AlundraLeverDoorsArcTests.E_Map44_…`, plus la variante sans G1633 : toutes les valeurs égales à l'attente. Arc D : pc 711 à N+34, 713 à N+55, 717 à N+96, pilier avant le levier dans l'ordre des cases. Arc E : D de 2320 à 2353 à N+1 (six `0x63`), pc 1556/1559/1562 à N+2, 1563 et 1584 à N+3, 1586 à N+34, T81 à N+35, `0x1A`/`0x1E` des sept portes à N+36, pc 1630 à N+101 exactement, 1631 à 1676 à N+102, portes à ±2 px de la colonne « P1 end ».)
- ✅ **L5 — P3 reporté d'E20.c1b-1.** (2026-10-09. `MapsWithoutSkippedSite` à 30 cartes (10 et 164) ; `KnownScenePredicateSites` vide (le site `(10, C, 75, 6418)` sort selon la convention d'E19, les tests de la règle 4 réécrits en conséquence) ; commentaire périmé « 0x95 not ported » de `AlundraEntityContactArcTests` réécrit et `(0x95, 6418)` retiré des trois ensembles `AssertSkippedWithin` (`AlundraEntityContactArcTests` ×2, `AlundraHeroJumpArcTests`). Les entrées `(0x2B, 6413)` et `(0x58, …)` de ces ensembles sont de vieilles tolérances (ces opcodes sont portés), laissées telles quelles : hors de ce P3.) `MapsWithoutSkippedSite` (`Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:49`) passe à 30 cartes
  (la 10 et la 164 y entrent) ; le site `(10, C, 75, 6418)` de `KnownScenePredicateSites` est retiré selon la convention d'E19 (un
  opcode porté sort des listes), ou gardé avec sa raison écrite ; les commentaires « 0x95 … not ported »
  (`AlundraEntityContactArcTests.cs:173`, `AlundraHeroJumpArcTests.cs:66`) et les ensembles `AssertSkippedWithin` qui autorisent encore
  `(0x95, 6418)` sont mis à jour.

#### Acceptation

**Arc A — 115, levier nord-ouest (enregistrement 2).**
- Monde `Tarn's Manor-115` (zone `Tarn's Manor`), aucun drapeau, arrivée au pixel (84,120), Z `1 << 20`, direction 8 (ouest).
- Charge : enregistrements 0 et 1 absents ; enregistrement 2 en (60,120), classe A posée, Z lu et affirmé (24 attendu, INFERENCE ; 16
  donnerait aussi le contact) ; après la première image, cases (4,6) et (5,6) en image A : W1, H4, GP16, tuiles 8514 et 8515.
- H (contact au tick de l'appui) : `TouchingEntity` du levier = le héros, `FrameCollisionTickCounter` 25, `HitCounter` du héros 1, un
  appel d'étincelles.
- H+1 : D (case 3) aux pc 1476, 1477, 1480 ; classe A effacée ; T2 posé.
- H+2 : C aux pc 1200, 1203, 1206, 1208 ; T2 effacé ; animation cible 2.
- H+33 (INFERENCE) : pc 1210, 1212 ; T1 posé ; animation 1.
- H+34 : B[1] passe pc 135 ; cases en image B (W1, H3). H+55 : image C (W1, H2). H+76 : image D (W0, H1, GP144, franchissable). H+97 :
  pc 168 (Break). Écarts exacts 21, 21, 21 ; image du T1 + 1 = image du passage de pc 135.
- Un second appui après H+25 : `HitCounter` 0 et aucun programme D (usage unique). La main n'est jamais perdue.

**Arc B — 116, leviers 39 et 40, le pont.**
- Monde `Tarn's Manor-116`, arrivée case (3,20) (pixel (84,328)), direction 8 ; puis `PlaceHero(866,424,128)`, regard ouest, assise.
- Charge : enregistrements 39 et 40 présents, 7 et 8 absents ; références des cases du pont lues après la première image.
- Levier 39, coup H1 : H1+1, D[5] aux pc 1076, 1077, 1080, 1083, T1 et T101 posés ; H1+2, C aux pc 702, 703, 720, animation 2, et B[1]
  aux pc 164, 169, 163 (pas d'extension) ; H1+33 (INFERENCE), classe A reposée, T101 effacé.
- Héros libre (H1+21 au plus tôt), `PlaceHero(1106,424,128)`, regard ouest, assise.
- Levier 40, coup H2 : H2+1, T2 posé ; t0 = H2+2, B[1] pc 174, main perdue, animation cible du héros 0, cases (40..42, 20) =
  [H10, GP0, 16574 | H10, GP0, 16575 | H8, GP2, 58146], piles de murs [49979, 49989] et [49980, 49990] en x 40 et 41 ; à t0+9, +18, +27,
  +36, rangées 21, 22, 23, 24 depuis la source (29,27), troisième tuile 58173 ; t0+45, main rendue (pc 225).
- Retrait : nouvel appui sur le levier 40 à H3 ≥ H2+48 ; r = H3+2, pc 227, 232, 241, rangée 24 rendue depuis (30,23) ; rangées 23, 22,
  21, 20 à r+9, +18, +27, +36 ; main rendue à r+45 ; cases finales égales aux références de la première image.

**Arc C — 117, la bascule (enregistrement 0).**
- Monde `Tarn's Manor (unused broken)-117`, arrivée au pixel (500,192), Z `1 << 20`, direction 24 (est).
- Charge : image T0 = 1, pc 284, main perdue ; les plateformes S0, S1, S2 (rectangles 2 × 4 en (21,4), (25,4), (29,4)) reçoivent F0 à
  T0, F1 à T0+6, F2 à T0+12, F3 à T0+18 (modèles 2 × 4 en x 42, 44, 46, 48, y 4) ; pc 380 à T0+24, puis main libre ; A[1] a posé T1 et
  effacé T2 à T5.
- Coup N : N+1, D aux pc 1184, 1185, 1209, 1212, 1215, 1236, 1239 (T1 effacé, T4 et T5 posés) ; N+2, direction cible 16, animation 1 ;
  N+9 (INFERENCE), direction 24, animation 0, T5 effacé, S1 ← F2 ; N+15, S1 ← F1 ; N+21, S1 ← F0, GP de (25,6) et (26,6) = 130 ; N+27,
  classe A posée. S1 lu par la pile de murs de la rangée 5 (F0 : `…8678, 8688, 8698, 8708` / `…8679, 8689, 8699, 8709`) et le GP de la
  rangée 6.
- Coups 2 à 4, chacun au moins 27 ticks après le précédent : (S0, S1, S2) = (F0, F3, F3), puis (F3, F3, F0), puis (F3, F3, F3) ;
  directions finales 0, 8, 16.

**Arc D — 117, levier 22 et pilier.**
- Même monde, arrivée au pixel (72,640), Z `2 << 20`, direction 24.
- Avant le coup : le pilier (enregistrement 2) précède le levier 22 dans l'ordre des cases.
- N+1 : T230 posé, classe A effacée ; N+2 : T230 effacé, animation 2 ; N+33 (INFERENCE) : T231 posé, animation 1.
- Pilier : `0x36` passe à N+34, animation 3 à N+55, fin du programme à N+96 (INFERENCE : l'animation 3 enchaîne sur la 0,
  `AnimationEndClassifier.cs:51-63`) ; son Z n'est pas affirmé.

**Arc E — 44, levier 7.**
- Monde `Wendels Nightmare-44`, drapeau G1633, arrivée au pixel (120,440), Z `8 << 20`, direction 24.
- N+1 : D aux pc 2320 à 2353 ; T80 posé, main perdue, `Flags & 1 == 0` sur les enregistrements 7 à 12.
- N+2 : pc 1556, 1559, 1562 (Break) ; T80 effacé. N+3 : pc 1563 (animation cible du héros 0), animation 2 sur les six leviers, premier
  appel de pc 1584. N+34 (INFERENCE) : pc 1584 passe, animation 1 sur les six. N+35 : T81 posé. N+36 : chaque porte exécute son
  `0x1A [2]` et son premier `0x1E`.
- N+101 ± 1 (INFERENCE : 64 ticks de marche à 1,5 px par tick d'est en ouest et 1 px du nord au sud, plus le tick de retard de
  D-E19-13) : T10 à T70 posés, pc 1630. Exactement un tick plus tard : pc 1631 à 1676, classe A sur les six leviers, T10 à T70
  effacés, main rendue. Positions finales des portes à ±2 px (`e20-c1b2/maps-117-44.md` §3.1, colonne « P1 end »).
- Variante sans G1633 : le coup ne fait tourner que 2320 puis 2353 ; ni main perdue ni T80.

**Pour toute la tranche.**
- Build Release puis Debug ; `Alundra.Tests` sans échec ; aucun changement de production ni du moteur ; traces du héros et de l'intro
  inchangées ; aucun test existant ne change hors de L5.
- Verifier frais.
- Recette en jeu de l'auteur, quand les salles seront atteignables (O-E20-14) : un coup d'épée anime le levier et ouvre la porte vers
  la 116 (115) ; les deux leviers étendent le pont, un troisième coup le retire (116) ; la bascule tourne et réécrit les plateformes,
  le levier 22 fait bouger le pilier (117) ; les sept trappes s'ouvrent (44).

#### Résultats (2026-10-09)

- **Commits** : `faaf00f` (L0), `712edd2` (L1), `4488943` (L2), `b4abc3d` (L3), `07e1191` (L4), `1c2de24` (L5) ; tests et documents
  seulement, aucun changement de production ni du moteur.
- **Tests** : `AlundraLeverSmokeTests` (cinq salles), `AlundraLeverArcTests` (A), `AlundraLeverBridgeArcTests` (B),
  `AlundraLeverBasculeArcTests` (C), `AlundraLeverPillarArcTests` (D), `AlundraLeverDoorsArcTests` (E, avec et sans G1633) ; aides de
  test dans `AlundraLeverArcSupport.cs` (enregistreur de sons horodaté, lecture d'un rectangle de cases, noms des mondes).
  `Alundra.Tests` 3491 sur 3491 en Release et en Debug ; traces du héros et de l'intro inchangées ; un arc dure 0,5 à 1 s.
- **Acceptation** : toutes les valeurs d'A à E égalent le plan, écrites avant l'exécution, y compris les INFERENCE (fin d'animation
  tenue à s + L + 1 ; Z 24 du levier de la 115 ; pilier de la 117 à N+96 ; portes de la 44 à N+101, le plan admettant ± 1 ; positions
  des portes à ± 2 px). Aucun défaut du DLL trouvé : les leviers réagissent déjà au coup d'épée depuis E20.c1a et E20.c1b-1.
- **L0** : le binaire permet le coup sur la pente 1 (`MovePlayer` lit `+0x18C` une seule fois, `0x80031B98` ; la table `0x80022D1C`
  n'envoie ailleurs que les pentes 4 et 6 ; `PlayerTryAttack` `0x8002EAF4` ne lit pas la pente) : `e20-c1b2/l0-pente.md`.
- **L5** : `MapsWithoutSkippedSite` compte 30 cartes ; `KnownScenePredicateSites` est vide ; les commentaires « 0x95 not ported » et
  les tolérances `(0x95, 6418)` ont disparu.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : valeurs recoupées au bytecode, deux mutations du DLL attrapées
  (`0x85` sauté sur une case du pont, `Wait` décalé d'un tick), chaque commit de tests compile, verdict de la pente redécodé.
- **Écarts acceptés** : l'arc C compte T0 à l'image de trace 0 (le plan disait « T0 = 1 », numérotation seulement) ; l'arc B lit les pc
  de B[1] sous 300, B[2] répétant son `End` en 308.
- **Reportés (P4)** : des tolérances `(0x2B, 6413)` et `(0x58, …)` d'opcodes déjà portés restent dans `AssertSkippedWithin` (sans effet
  sur le résultat, détection un peu affaiblie ; antérieures à c1b-2) ; la doc de `AssertHeroReady` est collée au-dessus d'`AssertTuples`
  (`AlundraLeverArcSupport.cs:145`) ; le retrait du pont de l'arc B compare les rangées 23 à 20 aux rectangles sources lus pendant
  l'exécution (l'égalité finale aux références de la première image compense).
- **Reste** : la recette en jeu de l'auteur, quand les salles seront atteignables (O-E20-14).

#### Arrêts, budget, retour arrière

- **Arrêts** : une carte qui ne se charge pas dans `ArcRun`, ou un chargeur qui demande un `GraphicsDevice` ou un changement du moteur ;
  une réaction qui diffère d'une valeur fixée par le bytecode ; un passage de `0x1C` autre que s + L + 1, ou une marche des portes autre
  que 64 ticks ± 1 ; un opcode sauté sur un chemin de levier ; le binaire qui empêche le coup sur la pente 1 (L0) ; le besoin d'un
  changement de production. Chaque arrêt remonte à la session principale, qui diagnostique.
- **Budget** : un `executor`, au plus deux tentatives au même niveau ; un verifier frais ; au plus cinq passes de correction.
- **Retour arrière** : `git revert` des commits d'E20.c1b-2.

### 1.8 E20.f0 — Moyens de test des armes et opcodes `0xC0`/`0xC1` 🧪 (faite et CONFIRMED le 2026-10-10, `41c8d23` à `ab20420` ; reste la recette de l'auteur : générer les sauvegardes hors de l'app)

Relecture : **READY** dès la première lecture (2026-10-10). Mise en œuvre faite le 2026-10-10 (T1 à T4 ✅, build Release et Debug, 3563 tests sans échec dans les deux configurations, traces inchangées) ; reste le verifier frais et la recette de l'auteur.

**But.** Donner aux tranches des armes et des objets leurs moyens de test, dans la DLL et en jeu, et porter les deux opcodes qui
imposent une arme : des sauvegardes de test qui portent l'arme choisie, les PM et les PV ; des aides d'arc pour donner et équiper une
arme ; `0xC0` et `0xC1`.

**Faits porteurs** (`docs/plan-e20-annexe/e20-armes/test-means.md` §1, §2, §7 ; `f-weapons.md` §10 ; `f-check.md`, lignes 37 à 39, 43
à 46, et correction C10) :
- [binaire] `0xC0` (`0x80041CDC`, taille 2) : `SetPlayerWeaponId(opérande + 1)` (`0x8004E484`), puis `[0x800DC4B8] |= 0x80` ;
  `0xC1` (`0x80041D18`, taille 1) : `[0x800DC4B8] &= ~0x80`. Sites : carte 472 B[5] `@327` (opérande 0) et `@378` ; carte 473 B[1]
  `@198` (opérande 1) et `@289`. Le bit `0x80` n'arrête ni `PlayerTryAttack` ni la branche `0x34` de `MovePlayer` ; l'ouverture de
  l'inventaire (`0x8002BB68`), la porte de téléportation (`0x8002F1C4`) et `TryUseItem` (`0x8002ED80`, son 3, rend 1) testent la valeur
  entière `!= 0`. La DLL a la constante (`AlundraGameState.cs:86`), pas les opcodes.
- [DLL] Donner une arme dans un arc : `state.NumberOfItems[2 * item + 1] = 1`, puis `AlundraPlayerManager.SetPlayerWeaponId(state,
  tables, slot)` (précédent `AlundraHeroAttackTests.cs:484-488`) ; PM par `SetPlayerMpMax` puis `SetPlayerMp` ; PV, argent et objet du
  Rond par `SetPlayerHp`, `SetMoney`, `SetCurrentItemId`. Dans un emplacement, le rang le plus haut l'emporte (toutes les lignes d'arme
  ont le bit 0 posé) : l'objet 1 du kit ne gêne jamais l'épée d'acier (`f-check.md` ligne 43, correction C10).
- [DLL] `AlundraTestSaves.Preset` n'a ni arme, ni PM, ni PV, ni argent (`AlundraTestSaves.cs:36-46`) ; `TryBuild` part du kit de
  nouvelle partie (`:112-134`) ; deux noms seulement, épinglés par `AlundraTestSavesTests.cs:69-72` ; l'outil se lance hors de l'app
  Claude (`docs/test-saves.md:9-10`).

**Périmètre.** `Alundra/Scripts/` : les cas `0xC0` et `0xC1` de `Dispatch` (et leurs étiquettes de `EventOpcodeSizeTable.cs`) ; les
portes de l'inventaire et de la téléportation si elles ne testent pas déjà la valeur entière (à vérifier, voir T2) ; `AlundraTestSaves`
(champs `Weapon`, `Item`, `Hp`, `HpMax`, `Mp`, `MpMax`, `Money`, facultatifs, appliqués par les vrais setters, bornés par
`CheckPreset`) et les nouveaux préréglages ; `Alundra.Tests/` (aides `Give`, `Equip`, `SetMp`, `SetItem` de `ArcRun`, tests) ;
`docs/test-saves.md` ; ce plan.

**Hors périmètre.** `TryUseItem` reste un bouchon (E20.h, qui portera son test du bit) ; la création des fichiers de sauvegarde (action
de l'auteur, hors de l'app) ; tout comportement d'arme.

#### Tâches

- ✅ **T1 — Opcodes `0xC0` et `0xC1`.** Ports exacts, tailles 2 et 1 ; tests sur les vrais octets des cartes 472 et 473 (arme
  équipée = emplacement 1 puis 2, bit `0x80` posé puis effacé). Fait : binaire relu (`0x80041CDC` : `lbu` de l'octet 1, `+1`, `jal 0x8004E484`, puis `|= 0x80`, rend 2 ;
  `0x80041D18` : `&= ~0x80`, rend 1) ; `AlundraWeaponLockOpcodesTests` (15 tests) : octets réels 472 `@327`/`@378` et 473 `@198`/`@289`, emplacement 1 puis 2
  (objets 1 et 5), `0x80` posé puis effacé sans toucher aux autres bits, `Result` intact, tailles 2 et 1, opérande hors 1..6 refusé par le setter, aucun opcode sauté sur
  les programmes B[5] de la 472 et B[1] de la 473.
- ✅ **T2 — Portes de la valeur entière.** Lire comment la DLL ouvre l'inventaire et déclenche une téléportation ; si l'une d'elles
  ne teste pas la valeur entière de `PlayerControlFlags` comme le binaire, l'aligner ; un test pose `0x80` seul et vérifie que
  l'inventaire ne s'ouvre pas et qu'aucune téléportation ne part. Si la porte de la DLL teste déjà la valeur entière, le test seul. Constat : les deux portes de la DLL testent déjà la valeur entière
  (`AlundraInventoryDirector.TryTrigger` : `PlayerControlFlags != 0` ; `AlundraPortalTrigger`, branche du sol de portail : `!= 0`, la branche du trou n'en teste aucune,
  comme le binaire `0x8002F180`-`0x8002F1BC` ; binaire relu : `lw` puis `bnez` à `0x8002BB68` et `0x8002F1C4`) : aucun changement de production, trois tests avec `0x80` seul
  (inventaire fermé puis ouvert une fois le bit effacé, téléportation refusée puis déclenchée, `PlayerTryAttack` frappe encore alors que le verrou `0x04` l'arrête).
- ✅ **T3 — Préréglages.** Champs facultatifs de `Preset`, appliqués après le kit par les setters de la DLL, bornés par `CheckPreset` ;
  préréglages `arsenal-a-m14` (armes 1, 5, 9, 14, 16, 7), `arsenal-b-m14` (1, 4, 6, 10, 14, 16, 7, livres 44, 46, 48, 50), `steel-m14`
  (arme 2), `demon-m14` (arme 3), `fire-m303` (comme `arsenal-a`, emplacement d'arme 5, la baguette de feu), `arrow-m16` (comme `arsenal-a`, emplacement d'arme 2,
  l'arc du chasseur) ; les autres choisissent l'emplacement 1, avec
  PV 4/10, PM 4/4, bombe 35 (1), herbes 36 (9), élixirs 37 et 38, fortifiant 41, parchemins 43, 45, 47, 49 (`test-means.md` §7) ; les
  cases d'arrivée passent `CheckInitialMap` et les bornes de caméra, sinon prendre une destination de portail de la même carte et le
  noter ; l'épingle des deux noms devient la liste complète ; `docs/test-saves.md` à jour. Fait : champs `Weapon`, `Item`, `Hp`, `HpMax`, `Mp`, `MpMax`, `Money` (facultatifs, appliqués après
  le kit et les compteurs par `SetPlayerHpMax`, `SetPlayerHp`, `SetPlayerMpMax`, `SetPlayerMp`, `SetMoney`, `SetPlayerWeaponId`, `SetCurrentItemId` ; `CheckPreset` refuse hors bornes, 16 cas) ;
  les six préréglages aux cases du plan, toutes acceptées par les vraies règles de sauvegarde (14 : (8, 6, 0) ; 303 : (38, 11, 1) ; 16 : (34, 31, 3)), aucune destination de portail
  de remplacement nécessaire ; `item` laissé à 0 dans les préréglages (le plan n'en choisit aucun) ; l'outil affiche aussi les stats (`--dry-run` non lancé : action de l'auteur).
- ✅ **T4 — Aides d'arc.** `Give(params int[] items)`, `Equip(int slot)`, `SetMp(int max, int mp)`, `SetItem(int item)` dans
  `AlundraArcSupport.cs`, par les vrais setters. Fait : `Give` passe par `AddOneItemIfUnlocked` (le donneur de l'opcode `0x82`), `Equip` par `SetPlayerWeaponId`, `SetMp` par
  `SetPlayerMpMax` puis `SetPlayerMp`, `SetItem` par `SetCurrentItemId` ; `AlundraArcInventoryHelpersTests` (5 tests) : sur la 10 depuis une arrivée, `Give(5)` puis `Equip(2)` donne
  l'objet 5 comme arme courante et Carré tire l'animation `0x13` ; plafond 4 des PM ; un identifiant d'objet hors table est refusé par le setter.

#### Acceptation

- `0xC0` à la 472 `@327` équipe l'emplacement 1 et pose `0x80` ; à la 473 `@198`, l'emplacement 2 ; `0xC1` efface `0x80` ; tailles 2 et
  1 ; aucun opcode sauté sur ces programmes.
- Avec `0x80` seul, l'inventaire ne s'ouvre pas et aucune téléportation ne part ; `PlayerTryAttack` frappe encore.
- Chaque préréglage se construit, se recharge (aller-retour de sauvegarde) et donne l'arme choisie (`GetItemIdFromCurrentWeapon`), les
  PV, les PM et les objets attendus ; `CheckPreset` refuse une valeur hors bornes.
- Un arc donne l'arc du chasseur (objet 5) et l'équipe : l'arme courante est l'objet 5.
- Build Release puis Debug ; `Alundra.Tests` sans échec ; traces inchangées ; verifier frais.
- Recette de l'auteur (matin) : générer les sauvegardes hors de l'app (`docs/test-saves.md`) ; elles serviront aux recettes des armes.

#### Résultats (2026-10-10)

- **Commits** : `41c8d23` (T1), `4c3111a` (T2), `d537100` (T3), `ab20420` (T4).
- **T1** : `0xC0` et `0xC1` portés (`AlundraEventProgramRunner.cs`, étiquettes de `EventOpcodeSizeTable.cs`) ; le binaire pose `0x80`
  sans condition, même quand le setter refuse l'opérande, et la DLL de même ; tests sur les vrais octets des cartes 472 et 473
  (`AlundraWeaponLockOpcodesTests`, 15 tests), programmes parcourus sans opcode sauté.
- **T2** : les portes de l'inventaire (`AlundraInventoryDirector.cs:395`) et du sol de portail (`AlundraPortalTrigger.cs:114`) testaient
  déjà la valeur entière, comme le binaire (`0x8002BB68`, `0x8002F1C4`) : aucun changement de production, trois tests avec `0x80` seul
  (inventaire fermé, téléportation refusée, `PlayerTryAttack` frappe encore).
- **T3** : `Preset` gagne `Weapon`, `Item`, `Hp`, `HpMax`, `Mp`, `MpMax`, `Money` (facultatifs, vrais setters, bornes du binaire :
  argent 0 à 9999, `0x8004DF80` ; objet 0 à 98, `0x8004E4D8`) ; six préréglages, cases d'arrivée du plan validées par les vraies règles ;
  l'épingle porte les huit noms ; `docs/test-saves.md` à jour ; une ligne `stats:` dans l'affichage de l'outil.
- **T4** : `Give`, `Equip`, `SetMp`, `SetItem` dans `AlundraArcSupport.cs`, par les vrais setters.
- **Tests** : `Alundra.Tests` 3563 sur 3563 en Release et en Debug ; traces inchangées.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : opcodes et portes redécodés, deux mutations attrapées.
- **Reportés** : P3 : la case d'arrivée (8, 6) de la 14 est libre et plate mais sans tuile de sol (TileId 65535, comme l'arène de
  `HitArena`) : le héros pourrait y apparaître sur fond noir ; à voir à la recette, sinon choisir une autre case. P4 : sans tables
  d'objets (hors monde réel), `0xC0` n'enregistre pas l'arme ; `Give` ne signale pas un objet déjà à son maximum (sans effet sur les armes).
- **Reste** : l'auteur génère les sauvegardes hors de l'app (`dotnet run --project tools/AlundraTestSaves -c Debug -- <projet>
  <préréglage>`, `docs/test-saves.md`).

#### Arrêts, budget, retour arrière

- **Arrêts** : une règle de sauvegarde qui refuse une combinaison sans case de remplacement ; une porte qui demanderait un changement
  du moteur.
- **Budget** : un `executor` ; un verifier frais.
- **Retour arrière** : `git revert` des commits d'E20.f0.

### 1.9 E20.f1 — Charge et relâchement : E20.f1a 🧪 (faite et CONFIRMED le 2026-10-10, `53ff27f` à `ce804b3`), E20.f1b l'aura (en attente de l'auteur)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, cinq points : la condition de l'aura était inversée ; l'oracle ne journalisait ni
le réservoir ni le générateur ; le moment de création des effets n'était pas choisi ; l'aura n'avait ni ancre ni clé de profondeur ;
E20.f0 n'était pas un prérequis. Une découverte ciblée les a résolus (`docs/plan-e20-annexe/e20-f1/` : `depth-key.md`,
`effect-model.md`, `oracle-ext.md`, contre-vérifiés par `check.md`). Elle montre que l'aura demande trois décisions d'architecture :
une création immédiate des effets par le héros, qui fait exception à ADR-0046 (les scripts ne lisent pas le réservoir) ; une règle de
tri des effets attachés, qui amende ADR-0049 ; la correction d'un défaut de l'original (poignée périmée, O-E20-15). En mode AUTO, la
session principale ne change pas l'architecture sans l'auteur : **E20.f1 est coupée** en E20.f1a (la charge sans l'aura, dans le modèle
d'effets existant, cette nuit) et E20.f1b (l'aura, en attente de l'auteur ; projet de section prêt dans `e20-f1/check.md`).
Erratum : `f-weapons.md` ligne 122 inverse le test de `+0x7D` (voir ci-dessous).
**Révision 2 (E20.f1a).** Seconde relecture : **REVISE**, trois points corrigés (FIX) : le bouchon de l'aura est spécifié sans
lecture du réservoir ; l'écart d'ordre du son de l'étincelle et sa règle de comparaison sont écrits ; E20.f2 devient un prérequis
(même fichier), les ancres de ligne deviennent des ancres de symbole. Une seule relecture de clôture suit.
Relecture de clôture d'E20.f1a : **READY** (2026-10-10).

#### E20.f1a — La charge et le relâchement, sans l'aura

**Prérequis.** E20.f0 (CONFIRMED : `Give`, `Equip` ; la sauvegarde `steel-m14` pour la recette) ; **E20.f2 terminée** (commits faits) :
les deux tranches modifient `AlundraPlayerManager.cs` dans les mêmes branches de `MovePlayer` (branche `+0x20`, branche verrouillée,
site de `UpdatePlayerWeaponEffect`) ; f1a part de l'état laissé par f2.

**But.** Le héros charge son arme en tenant Carré, entend les sons de charge, voit l'étincelle de maintien quand la charge est pleine,
et relâche une attaque chargée exactement comme le binaire : gerbe de 16 étincelles, son, animation chargée qui frappe par la passe de
coups ; l'arc de William et les baguettes arment leur tir, qui reste un bouchon compté jusqu'à E20.f3. Les branches gelée et
verrouillée de `MovePlayer` effacent la charge et le tir en attente. L'aura (effet 3 attaché) n'est pas dans cette tranche : son appel
reste un bouchon nommé, compté, à sa place exacte.

**Faits porteurs** (`f-weapons.md` §1 à §3 avec l'erratum, `f-check.md` lignes 5 à 19, 33, 35, corrections C2 à C5 et C13 ;
`e20-f1/`) :
- [binaire] `UpdatePlayerWeaponEffect` (`0x8002F49C`, appelé en `0x80031D68`, avant `PlayerTryAttack`) : compteur `0x80126FFC`, +1
  plafonné à 60 quand `(Hold | Released) & 0x80`, l'octet 0 de la fiche d'arme non nul et le drapeau b0 de l'animation courante
  (`0x80028D14 + 2 × (+0x88)`), sinon 0. Compteur < 11 : libération de l'aura (`0x8002F730`-`0x8002F744`). Compteur ≥ 11, à chaque tick :
  création de l'aura si la poignée est nulle **ou si le `+0x7D` (drapeau de destruction) de son emplacement est non nul**
  (`0x8002F570`-`0x8002F58C` ; erratum de `f-weapons.md` ligne 122) ; puis, seulement si `+0x1C & 3 == 0` (valeur d'avant la passe des
  compteurs) : son `0x2A` si le compteur est < 60 ; à 60, l'étincelle (effet `0x10` en X, Y, Z + `0x100000`) ; si elle n'est pas créée,
  rien d'autre (`0x8002F61C`) ; sinon son `0x1AD` si `+0x1C & 7 == 0`, sinon `0x1AC`, puis deux tirages `r1`, `r2` (état = `r2`),
  `jx = hi(r1 × 0x60001) − 0x30000`, `jy = hi(r2 × 0x40001) − 0x20000`, X += 20·jx, Y += 20·jy, forces = forces du héros
  (`+0xC4`/`+0xC8`) − (jx, jy).
- [binaire] `PlayerTryAttack` (`0x8002EAF4`) : relâchement testé avant l'appui ; compteur < 60 ou héros en l'air : rien (D-E20-9) ;
  sinon gerbe de 16 effets `0x10` (tables `0x80023654`/`0x80023694`, Z + `0x100000`, aucun tirage ; un effet nul saute ses seules
  écritures, `0x8002EBBC`), son `0x2B`, pointeur `0x80028C79 + 13w + 8`, compteur de pas 0, `+0x88` = l'animation chargée. Au tick du
  relâchement, une étincelle puis la gerbe peuvent naître (C10, C14, C16 au tick 101 : 17 créations).
- [binaire] Premier tick compté = S + D + 1 ; compteur à 60 à S + D + 60 ; premier relâchement réussi à S + D + 60 ; relâchement en
  l'air perdu. Compteur et pointeur effacés ensemble ; dans le périmètre : la branche `+0x20` (`0x80031BB4`, `0x80031BC0`) et la
  branche verrouillée (`0x80031BEC`, `0x80031BF8`) ; la DLL n'efface que le pointeur dans la première (`AlundraPlayerManager.cs:211`)
  et rien dans la seconde (`:220-223`).
- [DLL, ADR-0046, ADR-0048] Les effets passent par la file du stade (`Emit`), appliquée par `RunFrame` au tick d'émission ; les
  étincelles de coup et la fiole de vie tirent dans le générateur au moment de l'application (`AlundraNativeEffects.cs:52-53`,
  `:83-85`). L'étincelle de maintien et la gerbe suivent ce modèle : l'opération en file crée l'effet et, **seulement s'il est créé**,
  joue le son (`0x1AC`/`0x1AD`) et fait les deux tirages de l'étincelle. Écart documenté, déjà admis par ADR-0048 (écart 2) : les
  tirages et le son de l'étincelle ont lieu à l'application, dans l'ordre de la file ; l'ordre des tirages entre l'étincelle (émise par
  `MovePlayer`) et les étincelles de coup (émises par la passe de coups, plus tard dans le tick) reste celui du binaire. **Écart
  d'ordre des sons** : le son de l'étincelle (`0x1AC`/`0x1AD`) est joué à l'application, donc après tous les sons joués tout de suite
  pendant les ticks logiques de l'image (son `0x2B` du relâchement, sons de changement d'animation) ; le binaire le joue avant. Ticks
  concernés dans les scénarios : C10, C14 et C16 au tick 101 (étincelle puis relâchement).
- [convention Z] `PosZ` de la DLL = binaire − 1 (ADR-0026) ; les effets prennent le Z du binaire (`PosZ + 1`, ADR-0049) : le Z d'un
  effet de la DLL égale celui de l'oracle ; le bouchon de tir garde la convention de la DLL (`Z oracle = Z DLL + 1`).
- [oracle] `scratchpad/disc-f1/oracle-ext/` (vrai code de création du binaire, réservoir réel de 128 emplacements, mot du générateur
  avant et après chaque ligne, `+0x1C`, poignée de l'aura avec `+0x68`/`+0x7D`, champs de chaque effet créé à la fin de la logique du
  tick, avant toute mise à jour) ; colonnes communes égales à la découverte ; sorties identiques d'une relance à l'autre ; pas d'avance
  du générateur par image (comme la DLL d'aujourd'hui).

**Bouchon de l'aura (`AuraStub`), contrat exact.** Il ne lit jamais le réservoir ni `Destroying`/`+0x7D` (la condition corrigée est
écrite pour E20.f1b, pas évaluée ici). Il tient un seul état, un booléen « aura vivante en principe », et deux compteurs :
- appel `Check` à chaque tick où le compteur de charge est ≥ 11, au site de la création (`0x8002F570`) ; si le booléen est faux, il
  compte une « création » et le passe à vrai (modèle de la poignée nulle) ;
- appel `Release` au premier tick où le compteur est < 11 alors que le booléen est vrai (`0x8002F730`, poignée non nulle) : il compte
  une libération et repasse le booléen à faux ;
- il ne modélise pas le renouvellement par `+0x7D` (tous les 97 ticks d'une charge tenue) : c'est E20.f1b.

**Périmètre.** `Alundra/Scripts/` : `UpdatePlayerWeaponEffect` (bouchon aujourd'hui, dans `AlundraPlayerManager`) sans l'aura (le
bouchon `AuraStub` ci-dessus) ; la branche de relâchement de
`PlayerTryAttack` ; l'étincelle et la gerbe par des opérations en file, sur le modèle d'`AlundraNativeEffects.CreateHitSparks` ; les
deux effacements ; le bouchon de tir qui compte ses appels et leurs arguments ; tests ; l'oracle copié dans
`docs/plan-e20-annexe/e20-f1/oracle/`.

**Hors périmètre.** L'aura et tout ce qui lit le réservoir depuis `MovePlayer` (E20.f1b) ; l'apparition des projectiles (E20.e0a,
E20.f3) ; l'avance du générateur par image (E20.e0b) ; les autres effacements (blessure, E7, mort).

##### Tâches

- ✅ **T1 — Oracle.** Copier dans `docs/plan-e20-annexe/e20-f1/oracle/`, depuis `scratchpad/disc-f1/oracle-ext/` : `ext.py`,
  `probe_ext.py`, `probe_orig.py`, `tools/`, `out/`, `out_orig/`, `summary.txt` ; deux chemins seulement changent (`EFFECTS_GLOBAL`,
  `probe_ext.py:303`, relatif à la racine du dépôt ; la section `DISCOVERY`, `:63`, sautée si le dossier manque). Relancer
  `python probe_ext.py` ; les 17 fichiers de `out/` égalent au bit près ceux de `oracle-ext/out/`. **Résultat (2026-10-10)** : copie sous `docs/plan-e20-annexe/e20-f1/oracle/` (`ext.py`, `probe_ext.py`, `probe_orig.py`, `tools/`, `out/`, `out_orig/`, `summary.txt`) ; `EFFECTS_GLOBAL` est calculé depuis la racine du dépôt et la section `DISCOVERY` est sautée (`os.path.isdir`) ; `python -B probe_ext.py` relancé : les 17 fichiers de `out/` sont identiques au bit près à ceux de `oracle-ext/out/` (`diff -rq`), le `summary.txt` régénéré ne diffère que par la section `DISCOVERY` sautée ; `out_baseline/` (la sonde d'origine relancée, 5 fichiers) est ajouté, il est écrit par le script.
- ✅ **T2 — Charge.** `UpdatePlayerWeaponEffect(player, pad, host)` à sa place exacte (le site de son appel dans `MovePlayer`, après
  le pad et la saisie d'échelle, avant `UpdateWeaponStepProgression`) : compteur sur le héros, `AuraStub`, sons, étincelle en file. **Résultat (2026-10-10)** : `AlundraPlayerManager.UpdatePlayerWeaponEffect(player, in pad, host)` (port de `0x8002F49C`, au site de son appel, avant `UpdateWeaponStepProgression`) ; `AlundraAuraStub` (le bouchon : un booléen, créations, libérations, et `CheckCallsForTests` pour les tests ; il ne lit jamais le réservoir) ; `AlundraNativeEffects.CreateChargeSpark` (l'étincelle en file : le son `0x1AC`/`0x1AD` et les deux tirages seulement si l'effet est créé, à l'application) ; le héros porte `WeaponChargeTimer` et `AuraStub` ; `IAlundraScriptHost.EffectStage` (membre par défaut, nul) ; la boucle auto-pilotée du héros marque ses opérations (`BeginSource(tick, 1)`), comme le fait déjà `RunEntityBlock` du monde. Point d'accès de test pour lire les effets avant leur première mise à jour : `AlundraNativeEffects.CreatedForTests` (par thread, appelé par la fonction pure une fois l'effet créé et ses champs écrits, dans la fermeture appliquée par le stade avant la mise à jour du tick). Tests : `AlundraHeroChargeTests` (7) ; les cinq sondes C2-steel, C6-william, C10-crusher, C14-ice et C16-fire égalent l'oracle tick par tick jusqu'au tick qui précède le relâchement (compteur, animations cible et courante, pointeur et compteur de pas, sons selon la règle d'ordre, bouchon de l'aura, effets créés : identifiant, X, Y, Z, forces ; mots du générateur en paires). Valeurs de C2-steel écrites avant la relance et mesurées : compteur 1, 0, 1, 11 et 60 aux ticks 1, 2, 23, 33 et 82 ; première « création » du bouchon au tick 33 (58 appels `Check` du 33 au 90, 59 avec le tick 91 du relâchement, T3) ; son 42 aux ticks 33, 37, ... 81 ; étincelle du tick 85 : mot `12345678 > 24D2C78A`, position (8274740, 4686300, 1048577), forces (−86057, 93365), son 428 ; étincelle du tick 89 : `24D2C78A > AFBE9EAC`, position (5488860, 7531420, 1048577), forces (53237, −48891), son 429. Mutation (réelle, par script) : `20 * jitterX` remplacé par `21 *` : 6 des 7 tests rouges. Build Release puis Debug, `Alundra.Tests` 3717 sans échec dans les deux configurations, traces inchangées.
- ✅ **T3 — Relâchement.** Branche de relâchement de `PlayerTryAttack` : gerbe en file (Z = `PosZ + 1 + 0x100000`), son `0x2B`,
  pointeur, animation chargée ; relâchement en l'air perdu ; bouchon de tir au tick et avec les arguments du binaire (direction
  `{0, 0x10, 8, 0x18}[direction de la boîte]`, D-E14-6). **Résultat (2026-10-10)** : `AlundraPlayerManager.ReleaseChargedAttack` (la branche de relâchement de `PlayerTryAttack`, `0x8002EB38-0x8002EC88` : compteur < 60 ou héros en l'air, rien ; sinon la gerbe en file `AlundraNativeEffects.CreateChargeBurst`, le son `0x2B` tout de suite, le pointeur sur `AlundraWeaponTables.ChargeAnimationAddress`, le compteur de pas à 0, l'animation chargée) ; le bouchon de tir enregistre ses appels (`WeaponSpawnCallsForTests` : type `ptr[1]`, X, Y, Z + `ptr[3]` pixels, direction `{0, 0x10, 8, 0x18}[AnimationDirection]`, lus en `0x8002F7D4-0x8002F818`). Tests : `AlundraHeroChargeTests` (20, dont les cinq sondes tick par tick sur toute leur durée, relâchement et tir compris : animations, pointeur et compteur de pas, sons, bouchon de l'aura, effets créés avant leur première mise à jour, mots du générateur en paires, appels du bouchon de tir avec Z oracle = Z DLL + 1). Valeurs de C2-steel écrites avant la relance et mesurées : au tick 91 animation `0x18`, pointeur `0x80028C8E`, compteur de pas 0 (1 au tick 92), son 43 seul, 16 effets aux positions et forces de `summary.txt` (§ « C2-steel release tick »), générateur inchangé (`AFBE9EAC`), 59 appels `Check` (ticks 33 à 91), libération au tick 92 ; un seul tir dans la sonde, au tick 112 : type 27, (6553600, 6553600), Z DLL 0 (oracle 1), direction `0x18`. C10-crusher au tick 101 : 17 créations (l'étincelle `24D2C78A > AFBE9EAC` en (5488860, 7531420) puis la gerbe), sons `43, 36, 428`. **Erratum de l'exemple de l'acceptation** : la liste de l'oracle au tick 101 de C10 est `428, 43, 36`, pas `428, 43, 43` : `out/C10-crusher.txt` ligne 105 imprime les événements fusionnés par `collapse()` (`sound(43) x2` couvre `sound(43)` puis `sound(36)`, le son de l'ensemble d'animation de `0x19`) ; la liste dépliée est dans `oracle/out_events/` (`events_dump.py`). La règle de comparaison est inchangée, la liste attendue de la DLL est `43, 36, 428` (au tick 91 de C2 : `43`, le `sound(0)` n'est pas un son). Relâchement à S + D + 59 (tick 81) : échoue (compteur 59, pas de gerbe, pas de `0x2B`, animation Idle) ; à S + D + 60 (tick 82) : réussit. Relâchement en l'air : perdu (animation `0x2D`, rien créé), le même héros au sol : réussit. Réservoir plein au tick 85 de C2 : ni son 428 ni tirage (mot `12345678` inchangé), l'étincelle du tick 89 part de `12345678` (tuple de l'oracle au tick 85, son 429) ; cinq emplacements libres au tick 91 : les 5 premiers effets de la gerbe dans les emplacements 123 à 127, son 43, pointeur et `0x18` présents. Mutations (réelles, par script) : seuil 59 au lieu de 60, relâchement en l'air non refusé, force Y de la gerbe × 447 : un, un et huit tests rouges. Build Release puis Debug, `Alundra.Tests` 3730 sans échec dans les deux configurations, traces inchangées.
- ✅ **T4 — Effacements.** Compteur et pointeur dans la branche `+0x20` (`BlockedByEntity`) et la branche verrouillée (masque `0x34`)
  de `MovePlayer`, telles que les laisse E20.f2 (ancres de symbole, pas de numéros de ligne). **Résultat (2026-10-10)** : la branche `BlockedByEntity` efface `WeaponChargeTimer` en plus du pointeur (`0x80031BB4`, `0x80031BC0`) ; la branche verrouillée les efface tous deux juste après `AlundraPlayerAnimationEffects.Create(player, LockedMode)` (`0x80031BDC`, puis `0x80031BEC` et `0x80031BF8`), le bouchon de l'aura n'est pas touché (le binaire ne touche pas la poignée). Tests (4, dans `AlundraHeroChargeTests`, 24 au total) : la branche gelée efface les deux ; la verrouillée aussi, après l'appel de la fonction en mode 1 (l'état `0x2D` appelle le bouchon d'atterrissage, observable) ; la branche libre n'efface rien ; arc de William (C6) : relâchée au tick 91, la charge pendante (pointeur `0x80028CF6`, tir de type 7 attendu au tick 100 par l'oracle) est effacée au premier tick verrouillé (`0x04`, de 93 à 102), compteur et pointeur à 0 pendant tout le verrou, et aucun tir n'est demandé après le déverrouillage jusqu'au tick 130. Mutations (réelles, par script) : retirer l'effacement du compteur de la branche gelée : 1 test rouge ; retirer celui du pointeur de la branche verrouillée : 2 tests rouges. Build Release puis Debug, `Alundra.Tests` 3734 sans échec dans les deux configurations, traces inchangées.
- ✅ **T5 — Tests.** **Résultat (2026-10-10)** : deux classes de tests (28 tests) et un support. `AlundraHeroChargeTests` (24 : les cinq sondes tick par tick sur toute leur durée, les exemples de C2-steel et de C10-crusher, S + D + 59 et 60, le relâchement en l'air, le réservoir plein, cinq emplacements libres, les effacements, la charge interrompue par un verrou) sur le montage du héros de E20.b avec une scène d'effets sur la banque de la carte 163 ; `AlundraHeroChargeArenaTests` (4) sur le monde réel de la carte 14 (`HitArena`) : l'épée d'acier tenue contre un Beannoïde à 40 pixels le touche au tick 5 (balayage `0x3F`) puis, relâchée au tick 91, **le coup `0x18` le touche (mesuré au tick 113, pendant les ticks 91 à 136 de l'animation)** ; les effets de la charge sont créés dans le réservoir du monde (étincelles entre les ticks 82 et 85 puis tous les 4 ticks, 16 effets de la gerbe au tick 91 aux positions de la table, Z du binaire) ; le réservoir, après la charge et le relâchement, ne dépend pas du nombre de ticks par image (images de 1, 2 et 3 ticks : mêmes emplacements, positions, forces, images et même mot du générateur). `AlundraChargeSupport` charge l'oracle (rangées de `out/*.txt` et `*.json`, événements dépliés de `out_events/*.events.json`, écrits par `events_dump.py` : `out/*.txt` fusionne les événements voisins). Point d'accès de test pour lire les effets avant leur première mise à jour : `AlundraNativeEffects.CreatedForTests`. Mutations réelles par script : `20 * jitterX` en `21 *` (6 des 7 tests de la charge rouges), seuil de relâchement 59, relâchement en l'air accepté, force Y de la gerbe × 447 (8 rouges), effacements retirés (1 et 2 rouges). Commits : `53ff27f` (T1), `ca946e4` (T2), `52d52b8` (T3), `976995f` (T4), T5 dans le commit qui porte ce texte. Build Release puis Debug, `Alundra.Tests` 3738 sans échec dans les deux configurations, `Alundra.dll` du projet identique à celle de Debug, traces `docs/hero-trace-389-*` et `docs/intro-*` inchangées, aucun test existant modifié. Écarts et points signalés : (1) l'erratum de l'exemple du son (ci-dessus) ; (2) l'étincelle et la gerbe tirent leurs nombres et jouent le son 428/429 à l'application de la file (écart admis par ADR-0048), la DLL joue ce son après les autres sons du tick ; (3) la boucle auto-pilotée du héros marque ses opérations (`BeginSource(tick, 1)`), ce que le monde faisait déjà ; (4) la fiche d'arme `Records` de `AlundraWeaponTables` reste celle d'E20.b ; (5) `out_baseline/` ajouté à l'oracle ; (6) reste à E20.f1b : le renouvellement de l'aura toutes les 97 ticks et tout ce qui lit le réservoir depuis `MovePlayer`.

##### Acceptation

- Contre l'oracle, tick par tick, pour C2-steel, C6-william, C10-crusher, C14-ice et C16-fire : compteur, animation, pointeur, compteur
  de pas, sons, `AuraStub` (C2-steel : `Check` du tick 33 au tick 91, soit 59 appels, une seule « création » au tick 33, `Release` au
  tick 92, comme le `aura=` de l'oracle effacé au tick 92), appels du bouchon de tir (tick, type, X, Y, Z avec
  `Z oracle = Z DLL + 1`, direction).
- Pour chaque étincelle et chaque effet de gerbe, lus après leur création et avant leur première mise à jour (par un point d'accès de
  test du stade) : identifiant, X, Y, Z (égal à l'oracle), forces ; les indices d'emplacement ne sont pas comparés (l'aura de l'oracle
  occupe un emplacement que la DLL n'occupe pas encore). Mots du générateur en paires (entrée, sortie), le générateur posé sur le mot
  d'entrée de l'oracle.
- Règle de comparaison des sons : chaque tick, liste ordonnée ; la liste attendue de la DLL est celle de l'oracle, sauf aux ticks qui
  portent une étincelle et d'autres sons, où le son de l'étincelle passe en dernier (écart d'ordre ci-dessus). Exemple : C10 au tick
  101, oracle `428, 43, 43` (d'après `oracle-ext/out/C10-crusher.txt` ligne 105) → DLL `43, 43, 428`. **Erratum (constaté en T3)** : la ligne 105 imprime les événements fusionnés par `collapse()` ; la liste dépliée de l'oracle est `428, 43, 36` (le 36 est le son de l'ensemble d'animation de `0x19`), la liste attendue de la DLL `43, 36, 428`. La règle est inchangée.
- Exemples tirés de C2-steel : compteur 1 au tick 1, 0 au tick 2, 1 au tick 23 ; premier `Check` et « création » de l'aura au tick 33 ; son 42 tous les 4 ticks ;
  compteur 60 au tick 82 ; étincelle au tick 85, mot `12345678 > 24D2C78A`, position (8274740, 4686300), forces (−86057, 93365), son 428 ;
  tick 89 : son 429, `24D2C78A > AFBE9EAC` ; relâchement au tick 91 : animation `0x18`, 16 effets aux positions de `summary.txt` §4,
  son 43 ; tir de type 27 au tick 112 (Z oracle 1, direction `0x18`).
- Tick de relâchement de C10 (tick 101) : étincelle puis gerbe, 17 créations dans l'ordre de l'oracle.
- Réservoir plein : l'étincelle non créée n'a ni son ni tirage, la suivante part du mot inchangé ; cinq emplacements libres : 5 effets
  de gerbe, 11 nuls, son 43, pointeur et `0x18` présents.
- Relâchement à S + D + 59 : échoue ; à S + D + 60 : réussit. Relâchement en l'air : perdu. Charge interrompue par `0x34` : pas de tir
  après le déverrouillage, compteur et pointeur à 0. Le coup `0x18` de l'épée d'acier touche un Beannoïde de la 14.
- Build Release puis Debug ; `Alundra.Tests` sans échec ; traces inchangées ; aucun test existant ne change sans raison du binaire ;
  verifier frais.
- Recette de l'auteur : avec `steel-m14`, tenir Carré : les sons de charge et l'étincelle de maintien, puis la gerbe et le coup chargé au
  relâchement (l'aura viendra avec E20.f1b).

##### Résultats (2026-10-10)

- **Commits** : `53ff27f` (T1, oracle étendu copié : les 17 sorties identiques au bit près), `ca946e4` (T2), `52d52b8` (T3), `976995f`
  (T4), `ce804b3` (T5).
- **Code** : `UpdatePlayerWeaponEffect(player, in pad, host)` à sa place ; `AlundraAuraStub` (un booléen et des compteurs, aucune lecture
  du réservoir) ; `AlundraNativeEffects.CreateChargeSpark` et `CreateChargeBurst` en file (son et tirages de l'étincelle à
  l'application, seulement si elle est créée) ; `ReleaseChargedAttack` dans `PlayerTryAttack` ; bouchon de tir qui enregistre ses
  arguments ; effacements dans la branche `BlockedByEntity` et dans la branche verrouillée (après `CreatePlayerAnimationEffects(1)`) ;
  `IAlundraScriptHost.EffectStage` (membre par défaut nul) ; la boucle autonome du héros marque sa source d'effets comme le monde.
- **Acceptation** : les cinq sondes égales à l'oracle tick par tick sur toute leur durée ; C2-steel : compteur 1/0/1/11/60 aux ticks
  1/2/23/33/82, `AuraStub` 59 `Check` (33 à 91), une création au 33, `Release` au 92, étincelles aux ticks 85 et 89 (mots, positions,
  forces, sons 428 et 429), relâchement au 91 (`0x18`, pointeur `0x80028C8E`, son 43, gerbe de 16, générateur inchangé), tir au 112
  (type 27, Z oracle 1, direction `0x18`) ; C10 au tick 101 : 17 créations, sons DLL `43, 36, 428` ; relâchement à S + D + 59 refusé, à
  S + D + 60 accepté ; relâchement en l'air perdu ; réservoir plein et cinq emplacements libres ; tir en attente perdu après un
  verrouillage ; le coup `0x18` touche un Beannoïde de la 14 ; le réservoir du monde ne dépend pas du nombre de ticks par image (1, 2, 3).
- **Erratum du plan** : au tick 101 de C10, l'oracle donne `428, 43, 36` (le `sound(43) x2` des sorties `.txt` est un artefact de
  regroupement) ; la DLL donne `43, 36, 428` ; la règle d'ordre est inchangée.
- **Tests** : 28 tests de la charge ; `Alundra.Tests` 3738 sur 3738 en Release et en Debug ; traces inchangées ; aucun test existant changé.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : `UpdatePlayerWeaponEffect`, la branche de relâchement et
  `UpdateWeaponStepProgression` redécodés ; oracle relancé à l'identique ; six mutations attrapées ; rien ne lit le réservoir depuis
  `MovePlayer`.
- **Reporté (P3, à corriger obligatoirement par E20.f3)** : le bouchon de tir indexe la direction par `AnimationDirection` (fin d'image)
  au lieu de `BoxDirection` (valeur du tick, D-E14-6, `+0x98` lu en `0x8002F7F8`) ; sans effet tant que le tir est un bouchon, faux dans
  une image de plusieurs ticks où le regard change avant un tir à délai court (arc du chasseur en l'air) ; E20.f3 passe à `BoxDirection`
  avec un test d'image à plusieurs ticks.
- **Reste** : la recette de l'auteur (`steel-m14` : sons de charge, étincelle de maintien, gerbe et coup chargé ; l'aura viendra avec
  E20.f1b).

##### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un effet dont les données manquent ; un besoin de changer le moteur ;
  un besoin de lire le réservoir depuis `MovePlayer` (c'est E20.f1b) ; E20.f2 pas terminée.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E20.f1a.

#### E20.f1b — L'aura (en attente de l'auteur)

Projet de section prêt (`docs/plan-e20-annexe/e20-f1/check.md`, « Draft revision ») ; il demande trois décisions de l'auteur :
- **Q-N4 (une nouvelle ADR ; ADR-0056 est prise par E20.c2a, le projet de `e20-f1/check.md` qui la cite est à renuméroter)** : le héros crée et libère ses effets tout de suite (`CreateNow`, `CreateAttachedNow`, `FreeNow`), garde
  l'emplacement comme poignée et lit `Destroying` au tick même ; tout le reste reste en file (ADR-0046). Exact à un tick par image ; écarts
  pour N ≥ 2 (une image sans aura au renouvellement, ordre des tirages, indice d'emplacement). Recommandé : oui.
- **Q-N5 (amendement d'ADR-0049)** : un effet attaché se trie sur la coordonnée du sprite de son ancre plus `DepthOffset`, pour que
  l'aura se dessine au-dessus du héros comme dans le binaire (sinon elle passe dessous). Recommandé : oui.
- **O-E20-15 (défaut de l'original)** : si l'aura expire pendant un verrouillage et que son emplacement est repris, le premier tick
  normal libère l'effet qui l'occupe (`0x8002F730`-`0x8002F744`, aucun test). Reproduire, ou corriger par un numéro de génération
  (recommandé : corriger, c'est un pointeur pendant avéré).
- Faits déjà établis : clé du héros `((PosY + (IDSV << 16)) & 0xFFFF0000) + (short)(PosZ >> 16)`, égale au tri du binaire
  (`0x800399B8`) sur les 150 ticks de C2-steel ; ancre par `IAlundraEffectAnchor` sur le proxy, sans changement du moteur.

### 1.10 E20.f2 — Rebond du fléau 🧪 (faite et CONFIRMED le 2026-10-10, `a0248ff`, `8748e0f`, `02725d5` ; reste la recette de l'auteur)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, deux points corrigés : les nombres de la 14 et de la 69 viennent d'un autre
pilote de la découverte (`test-means/weapons_vs_target.py`, `weapons_vs_post.py` et leur oracle), désormais copié par T1, et leur
géométrie est écrite ; les événements de l'oracle que produit la tête de `CreatePlayerAnimationEffects`, laissée en bouchons, sont
nommés et exclus par une règle écrite, et l'alarme des PV bas reçoit un nom et un propriétaire.
**Révision 2.** Seconde relecture : **REVISE**, un point corrigé (FIX) : la géométrie du pilote du poteau et du cas sans cible
est réécrite telle qu'il l'a simulée (champ plat de l'oracle de c1, dx 40 ; cible hors de portée à 160 px) ; l'arc sur la vraie 69
n'affirme plus de nombres de l'oracle, seulement les relations fixées par le binaire. Une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-10).

**But.** Le fléau de fer et le fléau d'acier rebondissent comme dans le binaire, quand ils touchent une cible ou un mur : le héros
passe dans l'état de rebond (`0x08`, `0x1F`, `0x25`, `0x26`) au tick qui suit. La fonction `CreatePlayerAnimationEffects(mode)` entre
dans la DLL comme squelette à sa place exacte : la fin du fléau est réelle, le reste reste en bouchons nommés : les pas et les sons
du cycle de marche, l'atterrissage (son 11 et poussières, effet 7 par `0x8003BDD8`, qui tire dans le générateur ;
`0x80030224`-`0x800303CC`) et la coupe de l'herbe (`0x8002FDC0`) pour E20.e ; l'alarme des PV bas (`0x800303DC`-`0x80030430`, mode 0
seulement : quand PV max ≥ 5 × PV, décrément de `[0x800A8F2C]`, à −1 son `0x190` et remise à `0x2D`) pour E20.c2a, qui porte le modèle
de PV.

**Faits porteurs** (`docs/plan-e20-annexe/e20-armes/f-weapons.md` §4, `test-means.md` lignes 130, 153 à 156 et 241, contre-vérifiés par
`f-check.md` ligne 20 et correction C14) :
- [binaire] `CreatePlayerAnimationEffects` (`0x8002FB14`) est appelé en mode 0 en `0x80031C3C` (après la sortie `+0x20` et la gravité,
  avant l'entrée de la blessure `0x80031C44`) et en mode 1 dans la branche verrouillée ; sa fin `0x80030434`-`0x80030654`, gardée par
  `bnez $s5` en `0x800303D8` (mode 0 seulement), n'agit que pour `+0x88` (l'état choisi au tick précédent) = `0x12`, `0x16` (fer,
  indice 0) ou `0x40`, `0x42` (acier, indice 1). Rien avant cette fin n'écrit `+0x88` : elle se porte seule.
- [binaire] Déclencheurs : `HitCounter` (`+0x220`) non nul ; ou, si une boîte d'image existe, un mur sous un coin de la boîte (les quatre
  coins X, X + taille, Y, Y + taille de la `HitBox`, Z = Z de la `HitBox` ; colonne par `0x80127168` (px / 24, bornée 0 à `0x33`),
  rangée `y >> 20` (bornée 0 à `0x3B`), cellule `*[0x800DD86C] + 0x604`, pas de rangée 416 ; mur si `HitBoxZ < hauteur << 20` ou si
  la praticabilité a le bit `0x40` ; le premier coin en mur l'emporte). Résultat : `+0x88 = octet[0x800228A4 + 2 × indice + (en l'air ?
  1 : 0)]`, octets `08 1F 25 26`. Le rebond n'ajoute aucune force.
- [binaire] Ordre : la passe de coups écrit `HitCounter` à la fin du tick N ; `MovePlayer` le lit au tick N+1 ; la passe des compteurs
  l'efface ensuite.
- [données] États de rebond : vitesse 0, son 28 ; `0x08` et `0x25` durent 16 ticks puis enchaînent sur 0 ; `0x1F` et `0x26` durent 11
  ticks puis enchaînent sur `0x2D` ; tous sont déjà dans les groupes d'attaque d'E20.b.
- [oracle] Sondes de la découverte (`scratchpad/disc-weapons/f-weapons/out/F-*.txt`) : `F-iron-ground` (`0x08` au tick 6, son 28, repos
  au tick 22), `F-steel-ground` (`0x25` au tick 6), `F-iron-air` (`0x1F` au tick 8, `0x2D` au tick 19, atterrissage au tick 22),
  `F-steel-air`, `F-iron-walk` (aucune force), `F-iron-wall1`, `F-iron-wall2`, `F-steel-wall2`, `F-iron-wk40` (murs : rebond au tick 10),
  `F-iron-nohit` (rien). Pilote `scratchpad/disc-weapons/test-means/weapons_vs_target.py` (oracle `test-means/oracle/`, monde de
  la 14 de l'oracle de c1, héros 40 px à l'ouest d'un Beannoïde, regard 24, Carré au tick 1, comme `H1`) : fer, contact au tick 11,
  `0x08` du 12 au 27, repos au 28 ; acier, contact au 12, `0x25` du 13 au 28, repos au 29 ; sans cible (dx 100) : repos au 32 (fer) ou
  au 35 (acier) — dans le pilote, la cible hors de portée est à la case 30, soit 160 px du héros (le libellé « dx=100 » du pilote est
  inexact). Pilote `weapons_vs_post.py` : le sprite 126 de la 69 (le poteau) est posé dans le champ plat de l'oracle de c1 (monde de la
  14, `c1world.py:84`, `:177-178`), à la case (20, 11, 0), soit 252 px ; héros en (212, 96), regard 24, dx 40 : contact au tick 9,
  `0x08` ou `0x25` du 10 au 25, repos au 26. La géométrie de la vraie 69 (poteau en (132, 232), héros à 22 px) n'a pas été simulée.

**Périmètre.** `Alundra/Scripts/` : le squelette de `CreatePlayerAnimationEffects(mode)` à ses deux places (mode 0 entre la gravité et
l'entrée de la blessure, mode 1 dans la branche verrouillée), la fin du fléau réelle sur la `HitBox` rangée (E20.c1b-1) et le magasin
de cellules (`AlundraCellStore`) ; les autres parties en bouchons nommés ; tests ; l'oracle copié dans
`docs/plan-e20-annexe/e20-f2/oracle/`.

**Hors périmètre.** Les pas, poussières et la coupe de l'herbe (E20.e) ; les poteaux de la 69 restent inertes (leurs gestionnaires
natifs D31/E15 sont à E14).

#### Tâches

- ✅ **T1 — Oracle.** Copier dans `docs/plan-e20-annexe/e20-f2/oracle/` : `f-weapons/probe.py` et `f-weapons/tools/` (sondes `F-*`),
  `test-means/weapons_vs_target.py`, `test-means/weapons_vs_post.py` et `test-means/oracle/` (dossiers de la découverte sous
  `scratchpad/disc-weapons/`) ; relancer et garder les sorties (`out/`) ; elles égalent celles de la découverte sur les colonnes
  communes, et contiennent les lignes 11/12..27/28, 12/13..28/29, 32/35 et 9/10..25/26 citées plus haut. **Résultat (2026-10-10)** : copie sous `docs/plan-e20-annexe/e20-f2/oracle/` (`f-weapons/probe.py`, `f-weapons/tools/`, `test-means/weapons_vs_*.py`, `test-means/oracle/`) ; sorties dans `f-weapons/out/` (les dix sondes `F-*`, identiques octet pour octet à celles de la découverte) et `test-means/out/` (`weapons_vs_target.txt`, `weapons_vs_post.txt`) ; lignes citées retrouvées : fer 11/`0x08` 12..27/repos 28, acier 12/`0x25` 13..28/repos 29, sans cible 32 (fer) et 35 (acier), poteau 9/`0x08` ou `0x25` 10..25/repos 26.
- ✅ **T2 — Squelette et fin du fléau.** Port exact ; lecteur de cellule aligné sur l'indexation du binaire (colonne, rangée, bornes) ;
  tests. **Résultat (2026-10-10)** : `Alundra/Scripts/AlundraPlayerAnimationEffects.cs` : le squelette de `CreatePlayerAnimationEffects(mode)` (trois aiguillages sur l'état, tables `0x800228AC`, `0x800229CC`, `0x80022A74` lues dans l'exécutable ; le verrou d'atterrissage `0x800A8F30`, `LandingLatch`) appelé en mode 0 juste après le drapeau de gravité (`AlundraPlayerManager.cs`, avant `UpdateItemUseDriver`) et en mode 1 dans la branche verrouillée ; la fin du fléau réelle (`0x80030434-0x80030654`) ; bouchons nommés `PlayerRunStepEffects` (`0x8002FB6C`), `PlayerWalkStepEffects` (`0x8002FE24-0x800301D4`), `PlayerLandingEffects` (`0x80030224-0x800303CC`) et `PlayerGrassCut` (`0x8002FDC0`) pour E20.e, `PlayerLowHpAlarm` (`0x800303DC-0x80030430`) pour E20.c2a, chacun enregistrant ses appels (`StubCallsForTests`). Lecteur de cellule : `AlundraCellsCollisionField.TryReadBinaryCell(colonne, rangée)` (grille de 52 colonnes, colonne bornée à `0x33`, rangée à `0x3B`), posé sur le champ et non sur `AlundraCellStore` : le magasin mute les tableaux que le champ aliase (test avec mutation en direct sur la 14) et le héros n'atteint que le champ ; Z comparé avec le +1 de l'ADR-0026, comme l'opcode `0x95`. Tests : `AlundraFlailBounceTests` (133), `AlundraFlailBounceWallProbeTests` (5) ; les dix sondes `F-*` sont égales à l'oracle tick par tick (animation cible et courante, forces, hauteur, sol, pointeur et compteur d'arme, sons). **Correction à la révision 1** : seuls l'atterrissage (tick 22 des sondes en l'air : son 11 et effet 7) et le `sound(0)` du pas de marche (tick 30) viennent de la fonction (adresse de retour dans `0x8002FB14-0x80030678`, nouvelle sortie `oracle/f-weapons/out/F-sound-callers.txt` par `callers.py`) ; les `sound(0)` des ticks 22 et 23 de la marche, 22 du sol, 2, 19 et 22 de l'air sont ceux des changements d'animation (`UpdateAnimation`, `0x80038BD0`), que la DLL ne joue pas pour un jeu d'animation sans son : le test affirme le bouchon du pas au tick 30 et non au 23.
- ✅ **T3 — Arcs.** Sur la 14 (`HitArena`), les deux fléaux contre un Beannoïde, dans la géométrie du pilote, et sans cible (cible à
  160 px) ; sur la 69 (`Coastal Cave-69`, poteau de l'enregistrement 0), après une fumée de chargement, un arc qui n'affirme que les
  relations du binaire (voir l'acceptation). **Résultat (2026-10-10)** : `AlundraFlailBounceArcTests` (9). Sur la 14 (héros en (212, 96) regard 24, Beannoïde en (252, 96), Carré au tick 1) : fer, contact au tick 11, `0x08` du 12 au 27, repos au 28 ; acier, contact au 12, `0x25` du 13 au 28, repos au 29 ; `HitCounter` vaut 1 au tick du contact et 0 après, aucune force pendant le rebond. Sans cible (Beannoïde à 160 px) : aucun contact, aucun rebond, repos au tick 32 (fer) et 35 (acier). Sur la 69 : le chargement réussit (poteau de l'enregistrement 0, sprite 126, en (132, 232, 0), héros libre au sol en (110, 232)) ; contact **mesuré** au tick 9 dans les quatre cas (fer et acier, depuis (110, 232) regard est et depuis (154, 232) regard ouest), cause : le poteau (`HitCounter` non nul au tick 9, aucun mur avant), rebond du tick 10 au 25, repos au 26 : les relations fixées par le binaire (c + 1 à c + 16, repos à c + 17) tiennent ; le 9 est une mesure de la DLL, il coïncide avec la sortie du pilote du poteau, dont la géométrie n'est pas celle de la 69 (aucun nombre de l'oracle n'est affirmé sur cet arc). Build Release puis Debug, `Alundra.Tests` 3710 sans échec dans les deux configurations, `Alundra.dll` du projet identique à celle de Debug, traces `docs/hero-trace-389-*` et `docs/intro-*` inchangées.

#### Acceptation

- Contre l'oracle, tick par tick : animation cible, son, forces, pour chaque sonde `F-*`. Sont exclus, par une règle écrite dans le
  test, les seuls événements de la tête laissée en bouchons : le son 11 et les poussières d'atterrissage (effet 7), et les `sound(0)`
  des pas et du cycle de marche ; le test affirme à la place que le bouchon nommé correspondant est appelé à ces ticks-là. Exemples :
  `F-iron-air` tick 22 (atterrissage), `F-iron-walk` ticks 23 et 30.
- Sur la 14 (`HitArena`, héros 40 px à l'ouest du Beannoïde, regard 24, Carré au tick 1 ; sortie du pilote) : fer, contact au 11, `0x08`
  du 12 au 27 et repos au 28 ; acier, contact au 12, `0x25` du 13 au 28 et repos au 29.
- Sans cible à portée (cible à 160 px, comme le pilote) ni mur : aucun rebond ; repos au tick 32 (fer) ou 35 (acier).
- Le poteau dans la géométrie du pilote (champ plat, dx 40) n'est pas rejoué dans la DLL : l'arène de la DLL ne porte pas le sprite 126
  ; la sortie du pilote reste une preuve de l'oracle seulement.
- Sur la vraie 69 (poteau de l'enregistrement 0 ; héros en (110, 232) regard est, puis en (154, 232) regard ouest ; valeurs
  INFERENCE de `test-means.md` ligne 241) : le fléau touche le poteau (contact au tick c, mesuré), puis, fixé par le binaire : rebond
  (`0x08` ou `0x25`) du tick c + 1 au tick c + 16, repos au tick c + 17 ; si un coin de la boîte touche un mur avant le poteau, le rebond
  part du mur au tick suivant, ce que le test distingue par `HitCounter`. Aucun nombre de l'oracle n'est affirmé sur cet arc.
- Les états de rebond ne changent ni la marche ni le saut au-delà de l'oracle (`F-iron-air` : atterrissage au tick 22).
- Build Release puis Debug ; `Alundra.Tests` sans échec ; traces inchangées ; verifier frais.
- Recette de l'auteur : avec `arsenal-a-m14`, emplacement 3 : le fléau rebondit sur un Beannoïde et contre un mur.

#### Résultats (2026-10-10)

- **Commits** : `a0248ff` (T1, oracle copié et relancé : les 10 sondes `F-*` identiques au bit près, lignes des pilotes retrouvées),
  `8748e0f` (T2), `02725d5` (T3).
- **Code** : `AlundraPlayerAnimationEffects.cs` : squelette de `CreatePlayerAnimationEffects(mode)` avec les trois aiguillages du
  binaire (`0x800228AC`, `0x800229CC`, `0x80022A74`) et le verrou d'atterrissage `0x800A8F30` ; mode 0 juste après le drapeau de gravité,
  mode 1 en tête de la branche verrouillée (les deux seuls sites) ; fin du fléau réelle ; bouchons nommés qui comptent leurs appels :
  pas de course et de marche, atterrissage, coupe de l'herbe (E20.e), alarme des PV bas (E20.c2a). Lecteur de cellule
  `AlundraCellsCollisionField.TryReadBinaryCell` (grille du binaire, 52 × 60 pour les 483 cartes, bornes du binaire).
- **Acceptation** : les 10 sondes `F-*` égales à l'oracle tick par tick, aux exclusions nommées près ; sur la 14, fer contact 11, `0x08` du
  12 au 27, repos 28 ; acier contact 12, `0x25` du 13 au 28, repos 29 ; cible à 160 px : repos 32 et 35 ; sur la 69, le poteau touché au
  tick 9 dans les quatre cas, rebond du 10 au 25, repos au 26 (relations c + 1 à c + 16, c + 17).
- **Correction du plan** : une partie des `sound(0)` attribués aux bouchons vient du changement d'animation (`UpdateAnimation`, retour
  `0x80038BD0`) ; seuls l'atterrissage (tick 22 en l'air : son 11 et effet 7) et le son de pas (tick 30 de la marche) viennent de la
  fonction ; le test affirme le bouchon de pas au tick 30.
- **Tests** : 147 tests du fléau ; `Alundra.Tests` 3710 sur 3710 en Release et en Debug ; traces inchangées ; aucun test existant
  changé (un commentaire).
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : fin du fléau, tables et sites redécodés ; oracle relancé à
  l'identique ; trois mutations attrapées ; les six écarts de l'exécuteur justifiés par le binaire.
- **Reportés** : P3 : les sondes de mur ne comparent que l'animation cible (forces et sol vérifiés à part par le verifier : égaux) ;
  P4 : la branche verrouillée n'efface pas encore la charge ni le pointeur (fait par E20.f1a T4) ; P4 : le verrou d'atterrissage est
  par proxy, global dans le binaire (sans effet tant que le bouchon d'atterrissage est vide ; à revoir avec E20.e).
- **Reste** : la recette de l'auteur (`arsenal-a-m14`, emplacement 3 : le fléau rebondit sur un Beannoïde et contre un mur).

#### Arrêts, budget, retour arrière

- **Arrêts** : le lecteur de cellule de la DLL ne peut pas égaler l'indexation du binaire ; la 69 ne se charge pas (garder la 14 et le
  noter) ; un écart à l'oracle sans raison du binaire, hors des exclusions nommées.
- **Budget** : un `executor` ; un verifier frais.
- **Retour arrière** : `git revert` des commits d'E20.f2.

### 1.11 E20.c2-0 — Oracle du héros touché ✅ (faite et CONFIRMED le 2026-10-10, `1451fbb` à `fd29c18`)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, un point corrigé : le harnais de base refait en Python le répartiteur du tick
(`0x8003B388`) sans la porte `[0x800DC4B8] & 0x48`, qui ne laisse tourner que la passe des listes ; D1 l'aurait enregistré comme le
binaire. Un répartiteur propre à `hworld` suit maintenant la porte, avec sa règle dans le modèle et sa mutation de contrôle.
Relecture neuve : **READY** (2026-10-10). Exécution lancée en parallèle d'E20.f2 (dossier d'oracle seulement, aucun build).

**But.** Construire l'oracle d'E20.c2 (le vrai code d'`ALUN_CD.EXE` dans l'émulateur MIPS des tranches précédentes), sans toucher à la
DLL : l'entrée de la blessure `0x80031A68` (toucheur et cases dangereuses), les états `0x31`, `0x1C` et `0x3A`, l'invulnérabilité
`0x78` et le clignotement, les PV portés par l'entité et leur miroir dans les stats, `UpdateItemEffectState` (bottes, fiche du héros,
régénération) et le chemin `controlFlags == 0x20`. Il sert d'acceptation à E20.c2a (modèle de PV) et à E20.c2b (la blessure), quel que
soit l'ordre que l'auteur retiendra. Cette tranche relève de la ligne E20.c2 approuvée (« oracle de `0x80031A68` ») ; le découpage en
c2-0, c2a et c2b vient de la contre-vérification de sa découverte (`docs/plan-e20-annexe/e20-c2/check.md` §c).

**Faits porteurs** (`docs/plan-e20-annexe/e20-c2/` : `c2-binary.md`, `c2-dll.md`, `c2-oracle.md`, contre-vérifiés par `check.md`) :
- [binaire] Les faits de `c2-oracle.md` §3 (chemin normal, bloc de la blessure, branche du toucheur, branche des cases dangereuses,
  queue sans coup, table de la mort `0x80022FBC`, branche verrouillée, épilogue `UpdateItemEffectState`), confirmés par `check.md`
  lignes 1 à 39 et 41 à 47.
- [binaire] L'ordre des passes du tick (`check.md` ligne 73, `0x8003B388`-`0x8003B408`) ; la liste des entités dessinées (statut 2
  ou 3 et `(+0x1CC & 3) != 3`, `0x800385D0`-`0x8003860C`), d'où le clignotement : caché au tick de la blessure, puis un tick sur quatre.
- [oracle] La base est l'oracle d'E20.c1b (`docs/plan-e20-annexe/e20-c1b/oracle/tools/`, qui contient déjà `world.py`, `c1world.py`,
  `kworld.py`, `kmodel.py`, `kmut.py`, `kcheck.py`, `mips.py`, `interp2.py`) plus `scen.py` d'E20.c1 ; les ajouts de `c2-oracle.md` §2
  (PV du héros pris des stats par les vrais getters, échauffement de la fiche `0x80127008` par un vrai appel de `0x800307E8`, mots BSS,
  peintre de cellules dangereuses et d'eau, l'Orque de la 15 et le Poisson de la 14 comme cibles, attaques synthétiques, inventaires,
  colonnes) se font sans modifier un fichier copié.

**Périmètre.** Un dossier neuf `docs/plan-e20-annexe/e20-c2/oracle/` : copies identiques au bit près des outils de base, et les
fichiers neufs `hworld.py`, `hscen.py`, `hmodel.py`, `hmut.py`, `run_c2.py`, leurs sorties (`out/`, JSON compact) et un résumé. Ce plan.
**Hors périmètre** : toute modification de la DLL, des tests C# ou du convertisseur ; les scénarios A3, A5, A7, B3, B4, C2, C3, D2, F3,
F6 et le groupe H de `c2-oracle.md` (retirés par `check.md` §c ; A3 et A5 pourront revenir avec E20.c2b si elle en a besoin).

#### Tâches

- ✅ **T1 — Base.** Copier les outils de base ; relancer la porte D1 (S-A et S-B : 216 valeurs chacun, 0 écart) et `H3` d'E20.c1 : les
  colonnes communes égalent celles de c1 au bit près.
- ✅ **T2 — Monde et colonnes.** `hworld.py` selon `c2-oracle.md` §2, avec un répartiteur du tick propre, utilisé par les deux
  interprètes (sans modifier `world.py` ni `interp2.py`) : quand `[0x800DC4B8] & 0x48` est posé, seule la passe des listes `0x800384F4`
  tourne, comme le binaire (`0x8003B388`-`0x8003B408`, `check.md` ligne 73) ; sinon les huit passes ; colonnes de `c2-oracle.md` §4 plus `visible` (le héros dans la
  liste `0x80134250` à la fin du tick) et le miroir des stats (`stats_hp`, `stats_hpmax`).
- ✅ **T3 — Scénarios.** `hscen.py`, d'après `c2-oracle.md` §2 et `check.md` §c : A1 (`H3` prolongé à 200 ticks, et une variante au
  pad 0 au contact à 20 px), A2 (armure × PV contre l'Orque), A4 (relèvements), A6 (fenêtre d'invulnérabilité, bord du tick
  hurt + 118 à + 122), B1 et B2 (cases dangereuses × bottes, plancher de PV), D1 (balayage de `[0x800DC4B8]`, `0xA0` compris), E2 et E3
  (l'entité l'emporte sur les stats ; création depuis les stats), F1, F2, F4, F5 (régénération), G (bottes et fiche, façon K1) ; C1
  (eau, `0x3A`) et E1 (PV 0) à titre d'information pour E20.i et E18. L'oracle ne contient aucune valeur attendue, seulement des entrées.
- ✅ **T4 — Modèle indépendant.** `hmodel.py`, écrit d'après le plan et les rapports (pas d'après le code émulé), nourri des seules
  entrées et colonnes de géométrie : dégâts par la règle de `ResolveBalanceTarget` (E20.c1b) sur la fiche du héros ; coût et plancher
  des cases dangereuses ; suite de `+0x1CC` et phase du clignotement ; porte `& 0x48` : compteur `+0x1C`, `+0x1CC`, position et PV du
  héros figés d'un tick à l'autre, la colonne `visible` encore tenue par la passe des listes ; bornes des stats ; compteurs de régénération ; bottes.
- ✅ **T5 — Second interprète, mutations, couverture, manifeste.** Tous les scénarios aussi par `mips.py`/`interp2`, comparés texte à
  texte ; mutations de `c2-oracle.md` §5 qui portent sur les scénarios gardés (chacune avec son changement attendu écrit d'avance dans
  `hmut.py`), plus un contrôle du répartiteur : sans la porte, la ligne D1 à `0x48` doit changer ; couverture de `0x80031A68`, du bloc de régénération et de l'épilogue ; deux relances donnent le même manifeste ;
  `run_c2.py` enchaîne le tout.

#### Acceptation

- La porte D1 est inchangée ; les colonnes communes de `H3` égalent celles d'E20.c1 au bit près ; la ligne du tick 18 d'A1 égale celle de
  `H3`.
- D1 à `0x48` (et à `0x08`, `0x40`, `0x28`) : `+0x1C`, `+0x1CC`, position et PV du héros inchangés d'un tick à l'autre, `visible` tenue,
  identiquement dans les deux interprètes.
- Le modèle indépendant égale l'oracle sur chaque règle listée en T4, pour chaque scénario concerné.
- Le second interprète égale le premier sur chaque scénario.
- Chaque mutation retenue est attrapée par le scénario désigné.
- Deux relances donnent le même manifeste ; `python -B docs/plan-e20-annexe/e20-c2/oracle/run_c2.py` sort 0.
- Aucun fichier de la DLL, des tests C# ou du convertisseur ne change ; verifier frais.

#### Résultats (2026-10-10)

- **Commits** : `1451fbb` (T1), `df46fc3` (T2), `c6f5f3e` (T3), `8b84ef2` (T4), `fd29c18` (T5), tous sous
  `docs/plan-e20-annexe/e20-c2/oracle/` ; `python -B docs/plan-e20-annexe/e20-c2/oracle/run_c2.py` sort 0 en environ 300 s.
- **Base** : outils copiés au bit près dans `tools/` ; porte D1 inchangée (216 valeurs chacun, 0 écart) ; `H3` égal à celui d'E20.c1 ;
  les 45 premières lignes d'A1 égalent `H3` (4005 valeurs), tick 18 compris.
- **Scénarios** : 105 parcours, 7979 ticks (A1, A2, A4, A6, B1, B2, D1 avec 12 valeurs de drapeaux plus le relâchement de `0x20` et trois
  cas `0x48`, E2, E3, F1, F2, F4, F5, G ; C1 et E1 à titre d'information) ; répartiteur du tick qui suit la porte `& 0x48`.
- **Contrôles** : modèle indépendant 1412 règles, 0 écart ; second interprète identique sur les 105 parcours ; 14 mutations attrapées,
  plus un contrôle de patch et le contrôle du répartiteur ; couverture de `0x80031A68` 58/58, de la fin de `MovePlayer` 9/9, de la
  régénération 71/72 (la seule instruction manquée est inatteignable) ; deux relances, même manifeste.
- **Mesures utiles à E20.c2a et E20.c2b** : blessure d'A1 au tick 18 (PV 10 → 9, `0x31`, direction 24 → 8, `+0x1CC` 119, son 23), héros
  caché 30 ticks sur 120 ; prochain coup possible à blessure + 119 (avec un contact forcé, blessures aux ticks 2, 122, 242) ; dégâts de
  l'Orque selon l'armure : 4, 3, 2, 1, 1, 3 ; cases dangereuses : coût 1, 2, 3, bottes de niveau 3 jamais touchées ; seul le mot exact
  `0x20` atteint l'entrée dans la branche verrouillée (PV − 1, `+0x1CC`, sans animation ni son), `0x24`, `0x30`, `0x10`, `0x04`, `0xA0`
  perdent le coup, `0x80` seul suit le chemin normal, `0x08`, `0x28`, `0x40`, `0x48` figent tout sauf la passe des listes ; la fin de
  `MovePlayer` tourne sous tous les verrouillages ; l'entité l'emporte sur les stats (fiole : 11/11 depuis 5/10, 51/51 depuis 50/50 avec
  des stats bornées à 50/50) ; régénération + 1 aux ticks 201 et 402, retardée à 301 par un appui au tick 100 ; eau (C1) : `0x3A` au tick
  de la blessure, `0x3B` à + 4, `0x1D` à + 13, le héros se déplace (l'inconnue U3 de `c2-binary.md` le donnait immobile) ; PV 0 (E1) :
  `0x39` au tick 17, `0x4E` au tick 67, drapeau `0x4` au tick 76.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : sorties reproduites au bit près depuis une copie, porte `& 0x48` et
  ordre des passes redécodés, les deux prédicats de mutation corrigés après le premier passage justifiés par le binaire (plus stricts,
  pas plus faibles), modèle indépendant de l'émulateur.
- **Reportés (P4)** : la ligne d'A1 de `summary.txt` affiche les deux premiers sons du parcours au lieu du son de la blessure (23) ;
  `hmodel.py` lit les drapeaux de la ligne précédente de l'émulateur plutôt que des entrées (sans effet hors de la branche de mort) ;
  F4-zero n'isole plus la porte `Hp != 0` après le tick 61 environ (résultat juste).

#### Arrêts, budget, retour arrière

- **Arrêts** : la ligne du tick 18 d'A1 diffère de `H3` ; la porte D1 bouge ; un outil copié devrait être modifié ; un scénario demande
  un appel non bouchonné qui sort du périmètre du héros (le noter et le retirer, pas l'improviser).
- **Budget** : un `executor` ; un verifier frais (avec un redécodage du binaire).
- **Retour arrière** : `git revert` des commits d'E20.c2-0.

### 1.12 E20.c2a — Les PV portés par le héros, la fin de `MovePlayer` et `UpdateItemEffectState` 🧪 (faite et CONFIRMED le 2026-10-10, `c7da0c9` à `259c76d` ; reste la recette de l'auteur)

Relecture : **READY** dès la première lecture (2026-10-10). Exécution après E20.f1a et la vérification d'E20.c2-0.

**But.** Les PV du héros vivent sur son entité (`+0x14`, `+0x18`) comme dans le binaire : repris des stats à chaque installation du
monde, recopiés vers les stats à la fin de `MovePlayer` sur **toutes** ses sorties ; `UpdateItemEffectState` est porté en entier sauf
le pilote d'objet (bottes, fiche du héros, régénération) ; la fiole de vie, `0xBC` et `0xC3` écrivent l'entité ; l'alarme des PV bas
sonne. Ce n'est pas la blessure (E20.c2b, après E14.a, D-E14-4) : le héros ne perd encore aucun PV au contact. Tranche de la ligne
E20.c2 approuvée, découpage de `docs/plan-e20-annexe/e20-c2/check.md` §c ; décisions D-E20-19, D-E20-20, D-E20-22 (ADR-0054).

**Prérequis.** E20.f1a terminée (elle touche les mêmes branches de `MovePlayer`) ; E20.c2-0 CONFIRMED (son oracle sert d'acceptation).

**Faits porteurs** (`c2-binary.md` §1, §5, §6 ; `c2-dll.md` §1.4, §1.8, §1.9, §5 ; `check.md` points 1, 2, 5, 8, 9 et lignes 1 à 7, 33 à
40 ; oracle `docs/plan-e20-annexe/e20-c2/oracle/`) :
- [binaire] La fin de `MovePlayer` (`0x80032914`) appelle `UpdateItemEffectState`, puis `SetPlayerHpMax(héros +0x18)`, puis
  `SetPlayerHp(héros +0x14)` ; toutes les sorties y passent, la branche gelée (`+0x20 != 0`) et la branche verrouillée (`& 0x34`)
  comprises. La DLL sort plus tôt de ces deux branches. Les stats ne sont reprises vers l'entité qu'à la création du héros, juste après
  `InitializeEntity` (`0x800319EC`, puis `GetPlayerHpMax` `0x800319F8` → `+0x18`, `GetPlayerHp` `0x80031A00` → `+0x14`) ; la DLL pose
  `Hp = HpMax = 10` dans `InitializeBalance` (`AlundraEntityScriptProxy.HitPass.cs`) à chaque `AdoptPlayerPawn` : un miroir sans reprise
  remettrait les stats à 10/10 à chaque carte.
- [binaire] `UpdateItemEffectState` (`0x800307E8`) : étape 1, niveau des bottes `[0x80127000]` = 3 si l'objet `0x1C` est possédé, sinon 2
  pour `0x1B`, 1 pour `0x1A`, sinon 0 ; étapes 2 à 5 faites en E20.c1b-1 ; étape 6, copie de 255 octets (fiche 0 de l'armure, ou
  `+0x1C4` du héros sans armure) vers la fiche du héros `0x80127008`, puis combinaison des réponses 1 à 11 (règle de `c2-binary.md`
  §5.3 : `0x80` l'emporte ; sinon `b` ou `0x10`, plus `A − 0x10` et `B − 0x10` des fiches d'arme et d'accessoire sans drapeau, bornée à 0
  à `0x1F`) ; étape 7, régénération (portes `[0x800DC4B8] & 0x7C == 0`, `+0x20 == 0`, mot de maintien du pad `0x80126F28` nul, `Hp != 0`,
  `Hp < HpMax` ; sinon les trois compteurs `0x80126FE8/EC/F0` à 0 ; sinon pour chaque fiche d'octet 2 non nul, compteur + 1, et au-delà
  de l'octet 2, remise à 0 et PV + 1 ; `Hp = min(HpMax, a)`). Ces compteurs et la fiche sont des globales que rien ne remet à zéro au
  chargement d'une carte.
- [binaire] `0x80044520` ne remet à zéro que les trois pointeurs d'équipement ; `AlundraEquipmentRecords.Clear()` efface aussi les mots
  d'objet (P4 reporté d'E20.c1b-1).
- [binaire] Fiole de vie `0x800333AC` : `+0x18 += 1` sans plafond, `+0x14 = +0x18` (le plafond de 50 n'est que dans `SetPlayerHpMax`,
  appliqué au miroir) ; la DLL écrit les stats directement (`IncreaseHpMaxAndFill`, ADR-0048).
- [binaire] `0xBC` (`0x80041C00`, taille 2) : `+0x18` du héros += l'opérande, plafonné à `0x32` ; les PV ne changent pas. `0xC3`
  (`0x80041D6C`, taille 1) : `+0x14 = +0x18` ; `SetPlayerMp(GetPlayerMpMax())` (`0x8004DDDC`, `0x8004DEBC`) ; puis `0x8004B730`, qui
  cale les compteurs affichés du HUD (`[0x800A827C]` + 8, + 0xC sur `GetPlayerHpMax`, + 0x10, + 0x14 sur `GetPlayerMpMax`). Ni l'un ni
  l'autre n'est porté aujourd'hui.
- [binaire] Alarme des PV bas (`0x800303DC`-`0x80030430`, `CreatePlayerAnimationEffects` mode 0 seulement, aujourd'hui le bouchon
  `PlayerLowHpAlarm` d'E20.f2) : quand PV max ≥ 5 × PV, décrément de `[0x800A8F2C]`, à −1 son `0x190` et remise à `0x2D` ; la règle
  exacte des autres cas (PV plus hauts) est à relire dans le binaire en T5.
- [binaire] Le pilote `0x8003634C` reste un bouchon nommé (D-E20-19) ; la branche de mort `Hp == 0` (`0x80031CA8`) reste non portée
  (D-E20-5, D-E20-20 : à 0 PV le héros affiche 0 cœur et garde la main ; la régénération s'arrête, sa porte `Hp != 0`).
- [oracle] `docs/plan-e20-annexe/e20-c2/oracle/` (E20.c2-0) : groupes E (l'entité l'emporte sur les stats ; création depuis les stats),
  F (régénération : + 1 aux ticks 201 et 402, retardée à 301 par un appui au tick 100), G (bottes et fiche par inventaire), D1
  (l'épilogue tourne sous tous les drapeaux de verrouillage).

**Périmètre.** `Alundra/Scripts/` : la reprise des PV après `InitializeBalance` dans `AdoptPlayerPawn` ; l'épilogue de `MovePlayer` sur
toutes les sorties (par les ancres de symbole : branche `BlockedByEntity`, branche du masque `0x34`, chemin normal) avec le miroir
gardé pour les héros qui ont repris leurs PV (les montages de test nus sans reprise ne sont pas touchés, documenté) ; une fiche du héros
réutilisable (dans `AlundraEquipmentRecords` ou à côté) calculée aussi à l'installation ; les trois compteurs dans l'état de session ;
le niveau des bottes partagé avec `AlundraScriptedMotion` ; `UpdateItemEffectState(player, pad, host)` ; `Clear()` corrigé ; la fiole,
`0xBC`, `0xC3` (avec le calage du HUD) ; l'alarme des PV bas ; amendement d'ADR-0048 (la fiole écrit l'entité) ; tests ; ce plan.

**Hors périmètre.** La blessure, l'entrée `0x80031A68`, les états `0x31`/`0x1C`/`0x3A`, l'invulnérabilité et le clignotement (E20.c2b) ;
le pilote d'objet (E20.h) ; la mort (E18).

#### Tâches

- ✅ **T1 — Reprise et miroir.** Reprise des stats vers l'entité dans `AdoptPlayerPawn` après `InitializeBalance` ; épilogue sur toutes
  les sorties de `MovePlayer` : `UpdateItemEffectState`, `SetPlayerHpMax(héros.HpMax)`, `SetPlayerHp(héros.Hp)`. Fait : la reprise pose
  `LifePulledFromStats` sur le héros adopté, et `EndOfMovePlayer` (miroir gardé par ce drapeau : les montages nus des tests, de vie 0, ne
  sont pas touchés) est appelé aux trois sorties, la branche `BlockedByEntity`, la branche du masque `0x34` et le chemin normal. Suite
  complète Release 3738/3738 sans test modifié ; les épingles d'arcs et les traces sont inchangées (stats à 10/10).
- ✅ **T2 — `UpdateItemEffectState`.** Étapes 1, 6 et 7 ; fiche du héros ; compteurs en session ; bottes partagées ; `Clear()`.
- ✅ **T3 — Fiole, `0xBC`, `0xC3`.** Écritures dans l'entité (repli sur les stats sans héros, comme aujourd'hui) ; calage du HUD de
  `0xC3` ; amendement d'ADR-0048. Fait : la fiole (`IncreaseHpMaxAndFill(état, héros)`), `0xBC` et `0xC3` écrivent l'entité d'un héros qui
  a repris ses PV (`LifePulledFromStats`), sinon les stats (repli, mêmes bornes) ; `0xC3` cale les quatre compteurs affichés du HUD
  (`AlundraHudDirector.SnapDisplayedLifeAndMagic`, `0x8004B730`). ADR-0056 (nouveau fichier ; ADR-0048 marqué « partiellement
  remplacé » dans sa ligne d'état et dans l'index, jamais réécrit). Tests : les lignes P de la fiole sont réépinglées « après la fin de
  `MovePlayer` » (entité `HpMax + 1` sans plafond, stats inchangées juste après l'opcode, puis les valeurs du binaire après un tick
  verrouillé) ; fiole de 50/50 : entité 51/51, stats 50/50 ; `0xBC` plafonné à 50, `0xC3` et le calage du HUD, avec leurs replis.
- ✅ **T4 — Alarme des PV bas.** Relire `0x800303DC`-`0x80030430` en entier ; remplacer le bouchon `PlayerLowHpAlarm` par le port exact. Fait.
  Règle relue dans le binaire (mode 0 seulement, `bnez $s5` en `0x800303D8`) : `Hp` de l'entité du héros (`+0x14`) non nul et `HpMax` (`+0x18`)
  non inférieur à `5 × Hp` (comparaison signée sur 32 bits, `0x800303EC`-`0x80030400`) ; alors le compteur `[0x80098F2C]` (l'adresse
  `0x800A8F2C` du plan était une coquille : `lui 0x800A` avec `-0x70D4` donne `0x80098F2C`) est décrémenté et rangé, et à −1 le son
  `0x190` joue et le compteur vaut `0x2D` (période 46). Sinon rien n'est lu ni écrit : le compteur garde sa valeur et reprend où il en
  était. Le compteur est un global de session (`AlundraGameState.LowHpAlarmCounter`, 0 au départ, trois accès dans l'image, tous ici).
  Mesuré sur le vrai code par `docs/plan-e20-annexe/e20-c2/oracle/alarm.py` (fichier neuf, sorties dans `out-c2a/`, valeurs attendues
  écrites avant le run, 8 parcours PASS) : PV 2/12, 200 ticks, sons aux ticks 1, 47, 93, 139, 185 ; bornes `HpMax == 5 Hp` (2/10, 10/50, 1/50
  sonnent, 3/10, 11/50, 0/10 non) ; compteur gelé pendant que la vie est haute (sons aux ticks 1 et 67). Les lignes A2-*-5-12 de
  l'oracle c2-0 portent le premier son (tick 3, le tick après la blessure). Le bouchon enregistrait son appel pour les tests de
  répartition de `AlundraFlailBounceTests` : l'appel est toujours enregistré (nom conservé), aucun de ces tests ne change.
- ✅ **T5 — Tests.** `Alundra.Tests/AlundraHeroLifeTests.cs` (94 tests, 2 classes) : rejeux des groupes G (13 inventaires : niveau des bottes et
  octets 0 à 14 de la fiche, ticks 1 à 3), F (6 parcours : PV, PV max, stats et trois compteurs à chaque tick), E2 (5), D1 (8 valeurs de
  verrouillage par les vraies sorties de `MovePlayer`), E3 (4, par `ArcRun`), l'alarme (8, contre `out-c2a/alarm.txt`), plus les règles écrites
  du désassemblage (`CombineResponse` 14 cas, régénération, fiche réutilisable, `Clear`/`ClearAll`, sorties de `MovePlayer`, bornes du
  miroir). Le montage `ArcRun` appelle `PullHeroLifeFromStats` après son `InitializeBalance` (l'ordre du jeu ; sans cela le montage, dont
  l'en-tête de sprite vient après l'adoption, remettrait les PV à 10/10). Six mutations (régénération `<` en `<=`, masque du mot de
  maintien, plafond de la combinaison, rechargement de l'alarme, reprise des stats oubliée, fiole plafonnée) sont toutes attrapées.

#### Acceptation

- Rejeux des fonctions pures de la DLL contre l'oracle d'E20.c2-0 : groupe G (niveau des bottes et octets 3 à 13 de la fiche pour chaque
  inventaire), groupe F (régénération : ticks des + 1, compteurs, portes), groupe E (l'entité l'emporte ; création depuis les stats, bornes
  du miroir) ; D1 (épilogue sous verrouillage : stats tenues).
- Un `ArcRun` qui traverse deux cartes avec les stats à 4/12 garde 4/12 ; un chargement à 4/12 survit au premier tick ; la fiole, `0xBC`
  et `0xC3` donnent sur l'entité puis sur les stats les valeurs du binaire (fiole de 50/50 : entité 51/51, stats 50/50) ; les lignes de
  la fiole des tests existants sont réépinglées « après la fin de `MovePlayer` », avec la raison.
- L'alarme : le son `0x190` aux ticks de la règle relue (et contre l'oracle si l'une de ses lignes la porte ; sinon un scénario ajouté à
  l'oracle d'E20.c2-0, fichier neuf seulement).
- Build Release puis Debug ; `Alundra.Tests` sans échec ; tests de sauvegarde (E16) verts ; traces du héros et de l'intro inchangées ;
  verifier frais.
- Recette de l'auteur : les cœurs suivent d'une carte à l'autre ; avec un anneau de régénération, + 1 cœur toutes les 201 images sans
  toucher au pad ; à 1 PV sur 5 ou moins, l'alarme.

#### Résultats (2026-10-10)

- **Commits** : T1 `c7da0c9`, T2 `c8c99e0`, T3 `b5e100e` (ADR-0056), T4 `f95989b`, T5 `259c76d`.
- **Suite** : Release et Debug 3841/3841 (3738 avant la tranche, 103 tests neufs), `Alundra.dll` de `alundra-project` identique à celle de
  Debug. Aucune épingle d'arc ou de trace n'a bougé (stats à 10/10), les tests de sauvegarde E16 sont verts, `docs/hero-trace-389-*` et
  `docs/intro-*` inchangés au contenu près (bruit de fin de ligne restauré).
- **Tests existants modifiés** : `AlundraDamageTests.K2_TheRecordsAreFilledByTheHeroTick...` (un `Clear()` à la main ne vide plus que les
  pointeurs, comme `0x80044520` : le mot d'objet de l'armure est conservé) ; `AlundraNativeEffectTests` lignes P de la fiole (réépinglées
  « après la fin de `MovePlayer` » : la fiole écrit l'entité, `HpMax + 1` sans plafond, les stats suivent au tick) ;
  `AlundraArcSupport` (la reprise des PV après son `InitializeBalance`).
- **Valeurs d'acceptation mesurées** : G, boots 0/0/1/2/3/3/2/0/0/0/0/0/0 et fiche `10 10 10 10 10 10 10 10 10 10 10` (kit), leather `0C`,
  ancient `08`, silver `04`, noarmour `00`, bracelet `0F`, charm `0F` avec `80` à la 6e réponse, tous égaux à l'oracle aux ticks 1, 2, 3 ;
  F, +1 aux ticks 201 et 402 (F1, F5-ring37), 301 avec un appui au tick 100 (F2), jamais pour F4-full, F4-zero, F5-armour-weapon, compteurs
  `[0, 0, 200]` au plus, égaux à chaque tick ; E2, l'entité l'emporte (stats 3 reprises à 10 au tick), fiole 5/10 → 11/11, 49/49 → 50/50,
  50/50 → entité 51/51 et stats 50/50 ; E3, 4/12 → 4/12, 70/60 → 50/50, 9/5 → 5/5, −3/−1 → 0/0 ; D1, les stats pokées à 6 reviennent à 10
  sous `0x00 0x20 0x24 0x30 0x10 0x04 0x80 0xA0` ; carte 14 à 4/12 : le héros est créé à 4/12 et les 30 images suivantes gardent 4/12, puis
  une seconde carte (179, arrivée) reprend 4/12 et le garde (les stats du montage sont redonnées à la main entre les deux arcs : le
  montage remet les singletons à zéro à chaque arc) ; `0xBC` 10/8 → HpMax 15, 48 → 50, 50 → 50, `0xC3` 3/12 → 12 puis stats 12 au tick,
  PM remplis, compteurs du HUD 12/12/4/4.
- **Alarme** (`oracle/alarm.py`, valeurs attendues écrites avant le run, 8 PASS) : PV 2/12 sur 200 ticks, sons aux ticks 1, 47, 93, 139, 185 ;
  bornes `HpMax == 5 Hp` (2/10, 10/50, 1/50 sonnent ; 3/10, 11/50, 0/10 non) ; compteur gelé : sons aux ticks 1 et 67. Règle relue :
  `Hp != 0` et non `HpMax < 5 Hp` sur l'entité, mode 0 seulement, compteur global `[0x80098F2C]` (et non `0x800A8F2C`) −1, son `0x190` à
  −1 puis rechargement à `0x2D`.
- **Écarts** : le miroir est gardé par `LifePulledFromStats` (les montages nus des tests ne sont pas touchés) ; la fiole, `0xBC` et `0xC3`
  écrivent les stats en repli quand le héros n'a pas repris ses PV (et pas seulement quand il n'y a pas de héros) ; l'adoption
  recalcule la fiche et les bottes à l'installation sans l'effet de bord de `ItemId` et laisse les pointeurs vides comme le chargement de
  carte ; le niveau des bottes est aussi recalculé à la demande par la physique (le retard d'un tick du binaire est sans effet) ; le pilote
  d'objet et la mort restent non portés.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : fin de `MovePlayer`, reprise, étapes 1, 6, 7, `0x80044520`,
  `0xBC`, `0xC3`, `0x8004B730`, fiole et alarme redécodés (compteur de l'alarme en `0x80098F2C`, coquille du plan corrigée) ; oracle de
  l'alarme relancé à l'identique ; mutations attrapées (fin de `MovePlayer` retirée de la sortie verrouillée : 21 échecs ; comparaisons
  de la régénération et de l'alarme ; `Clear()` ; calage du HUD) ; persistance des PV dans une même session prouvée par un test
  temporaire du verifier (ré-adoption, verrouillage, chargement, sauvegarde) ; ADR-0056 et la ligne d'état d'ADR-0048 conformes.
- **Reportés** : P3 : aucun test livré n'épingle la reprise de production (`AlundraWorldProxy.cs`, `AdoptPlayerPawn`) : le montage
  `ArcSupport` la refait et masque son oubli ; à ajouter : un test qui passe par le vrai `AdoptPlayerPawn` après l'`InitializeBalance` du
  montage (modèle de la ré-adoption du verifier). P4 : le résumé de sauvegarde lit le PV max des stats, le binaire celui du héros
  (`0x80031198`) : « 50 » au lieu de « 51 » après une fiole bue à 50/50 (sans doute inatteignable) ; P4 : formulation « BSS » du compteur
  de l'alarme (il est dans l'image chargée, à 0).

#### Arrêts, budget, retour arrière

- **Arrêts** : une épingle d'arc ou de trace qui bouge alors que les stats sont à 10/10 ; un test de sauvegarde qui échoue ; un écart à
  l'oracle sans raison du binaire ; un besoin de changer le moteur.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E20.c2a.

### 1.13 E20.d — La course : E20.d1 🧪 (faite et CONFIRMED le 2026-10-10, `af42753` à `0b1d3f3` ; reste la recette de l'auteur), E20.d2 ⏳ (attend la mesure du stick, Q-D1)

**Révision 1 (2026-10-10).** Relecture d'E20.d1 : **REVISE**, deux points corrigés : le prérequis est E20.c2a CONFIRMED (arrêt
sinon, base des épingles nommée) ; la tolérance d'O-E19-42 ne vaut que pour le mur est (± 1 tick), les autres murs sont exacts.
Relecture neuve d'E20.d1 : **READY** (2026-10-10). Exécution après le verdict CONFIRMED d'E20.c2a.

Découverte en lecture seule du 2026-10-10 (`docs/plan-e20-annexe/e20-d/` : `run-binary.md`, `run-dll.md`, `run-oracle.md`, contre-vérifiées
par `check.md` : handlers redécodés, G1, G4, G5, G6 de l'oracle rejoués à l'identique). Cette section reprend le projet de `check.md`
§3. Les traces iront dans `docs/plan-e20-annexe/e20-d/oracle/` (T0). Cette nuit : E20.d1 seulement ; E20.d2 attend la mesure du
stick par l'auteur (Q-D1).

**But.** Le héros court comme dans le binaire :
- Triangle tenu, 25 ticks de préparation, puis la course à 258048 par tick ;
- l'arrêt glissé, la reprise, la charge `0x04`, le rebond contre un mur ;
- la chute d'un rebord et l'escalade, prioritaire sur le rebond ;
- au stick, l'arrondi de D-E20-15.

Le test « Triangle seul donne Idle » s'inverse.

**Découpage.**
- **E20.d1 — la course au clavier et à la croix.** Tout ce que fait le binaire, prouvé tick par tick par l'oracle.
- **E20.d2 — l'arrondi du stick (D-E20-15).** Un ajout du port, limité aux deux lectures de la table de départ. Il demande que le pad
  transporte l'échantillon du stick, et une mesure du stick par l'auteur. Après E20.d1.

**Prérequis.**
- Exigés : E20.a, E20.b, E20.c1a, E20.f1a (le relâchement chargé passe aussi par `0x29`), E20.f2, et **E20.c2a CONFIRMED par son
  verifier frais**, ses éventuelles corrections commitées (mêmes branches de `MovePlayer`, épilogue). Aucune tâche d'E20.d1, T0
  comprise, ne part avant ce verdict ; la base des épingles « inchangées » est la tête de la branche après la synthèse d'E20.c2a.
- Non exigés :
  - E20.e : sans elle, le son 12, la poussière et l'atterrissage (son 11, effet 7) restent des bouchons et ne sont pas comparés ;
  - E20.c2b : sans elle, les colonnes de la blessure ne sont pas comparées.

**Faits porteurs.**
- [binaire] Table des états `0x80022D3C` :
  - `0x29` va à `0x80031F54`, `0x03` à `0x80031FF4`, `0x2A` à `0x80032118` ;
  - `0x04` va au groupe au sol `0x80031E18`, déjà porté ;
  - `0x3E` et `0x1C` vont à `0x80032558`.
- [binaire] La pente 4 (table `0x80022D1C`) mène à `0x8003260C`. Cette branche n'atteint ni la queue ni ces états.
- [binaire] Queue `0x80031EA8`, dans cet ordre :
  - `PlayerTryAttack` ;
  - en l'air, `0x2C` ou `0x2D` ;
  - front de Croix (sauf `VramOR & 0x4000`) ;
  - Triangle **tenu** (`hold & 0x10`) : `0x29` et `+0xB0 = 0` (`0x80031F14`-`0x80031F30`) ;
  - sinon Moving ou Idle.
- [binaire] `0x29`, dans cet ordre :
  - `TryUseItem` (0 finit le tick), `PlayerTryAction` (≠ 0 finit le tick), `PlayerTryAttack` (≠ 0 finit le tick) ;
  - `TargetDirection` = la direction à 8 voies, écrite même au tick en l'air ;
  - en l'air, `0x2D` ; Triangle relâché, Idle ; `+0xB0 == 0`, fin ;
  - `0x80022CAC[nibble]` (haut `0x10`, droite `0x18`, bas `0`, gauche `8`, −1 ailleurs) : −1 finit le tick, sinon `TargetDirection`
    prend la valeur et l'état passe à `0x03`.
- [binaire] `0x03`, dans cet ordre :
  - `PlayerTryAction`, puis en l'air `0x2D` ;
  - `ForceAdjusted` : effet 9, `0x8003BDD8(0, 9, 0, X, Y, Z + 0x100000)`, sans force ; puis `0x3E` et `TargetDirection += 0x10` ;
  - nibble nul : `0x2A`, compteur `0x80126FF4 = 1` ;
  - Triangle relâché, ou `dir` hors de la ligne `0x80022CEC + 12 × regard` (bas {1C,0,4}, haut {C,10,14}, gauche {4,8,C},
    droite {14,18,1C}) : `0x2A`, compteur 0 ;
  - `hold & 0xE0` : `0x04`.

  Pas de `TryUseItem`. `TargetDirection` n'est jamais écrite hors du rebond.
- [binaire] `0x2A`, dans cet ordre :
  - `TryUseItem`, `PlayerTryAction`, puis en l'air `0x2D` ;
  - compteur nul : fin ;
  - `ForceAdjusted` ou Triangle relâché : compteur 0 ;
  - sinon compteur + 1. Sous 11, un bit de direction le remet à 0. À 11 et plus, la table de départ : −1 finit le tick et garde le
    compteur, sinon reprise.

  Seul `MovePlayer` touche le compteur (cinq sites).
- [binaire] `0x3E` et `0x1C` :
  - `TryUseItem` à 0 donne `TargetDirection += 0x10` (`0x8003270C`) ;
  - sinon, si `+0xAC == 1` : `TargetDirection += 0x10`, puis Idle au sol et **1 en l'air**.
- [binaire] Regard : `UpdateAnimation` le recalcule à chaque tick (`0x80038AD8`). Un changement de regard relance l'animation à
  l'image 0 sans toucher `+0xB0` : tourner pendant la préparation la retarde.
- [binaire] La porte d'escalade de pente 6 (`0x80031D20`) précède l'aiguillage ; elle est déjà portée.
- [binaire] Eau profonde :
  - sans bottes de niveau ≥ 2, c'est un mur (`0x80037504`, O-E19-22) ;
  - avec, la pente 4 donne `0x1D` puis `0x0F`, la nage (E20.i).
- [données] Durées exportées, dans les quatre directions, égales aux fiches du binaire : `0x29` 24 ticks en boucle, `0x03` 30 en
  boucle, `0x04` 32 enchaîné sur 0, `0x2A` 16 enchaîné sur 0, `0x3E` 10, tenu.
- [DLL] Rien de la course n'est porté ; `0x04` est déjà dans `IsGroundAttackGroup`. Déjà génériques :
  - vitesses ; sons de changement d'animation (13, 14, 83, 28) ; impulsion Z de `0x3E` (1024) ;
  - invulnérabilité `0x40` ; charge comptée ; attaque Tackle rechargée au changement d'animation.

  `AnimationDirection` n'est posé qu'en fin d'image.

**Défauts proposés (écarts écrits dans le code et ici).**
- **Eau profonde** (recette de l'enveloppe : « le héros marche encore ») :
  - sur la pente 4, la queue ne teste pas Triangle ;
  - un état de course (`0x29`, `0x03`, `0x2A`, `0x04`, `0x3E`, `0x1C`) passe à Idle, à la place de `0x1D`, avec l'écriture de
    direction du binaire : retournée pour `0x3E` et `0x1C` (`0x80032658`), inchangée sinon ;
  - E20.i remplacera ce garde par la branche `0x8003260C` ;
  - le mur d'eau profonde sans bottes (O-E19-22) reste hors d'E20.d (Q-N3).
- **Regard du tick.** Le cône lit le regard que le binaire aurait posé au tick précédent (et en E20.d2, l'égalité du stick aussi) :
  - il se calcule une fois en tête de `MovePlayer`, avant toute écriture, par `TryResolveAnimationTarget` ;
  - il est exact à 1 et 2 ticks par image, et quel que soit le nombre de ticks dans `0x03` (direction cardinale, table idempotente) ;
  - c'est la valeur du binaire, pas un écart.
- **Compteur de reprise** : un champ du héros, à côté de `WeaponChargeTimer`. Il équivaut au global du binaire, puisque `0x03` le
  pose toujours avant d'entrer dans `0x2A`.
- **`0x1C`** : le gestionnaire est partagé avec `0x3E`. La première tranche qui arrive, d'E20.c2b ou d'E20.d, branche les deux
  numéros.
- **O-E20-12 hors d'E20.d.**
  - À 1 tick par image, les décisions sont exactes (AT8, AT8n). Le rebord s'épingle comme AT8 : la colonne `on_ground` est comparée
    au tick d'avant, au tick de la perte.
  - Une lecture par tick demande la règle de recollement au sol de 3 px (`0x80037848`). Elle déplacerait UH7, AT8, AT8n, AT8b et les
    traces highground : tranche à part.

**Hors périmètre.**
- Pas, poussière, son 12 et atterrissage (E20.e) ; blessure et entrée des dégâts (E20.c2b) ; nage et mur d'eau profonde (E20.i,
  O-E19-22) ; porter (E20.g, `PlayerTryAction` reste un bouchon) ; objets du Rond (E20.h).
- Rognage au bord de la carte (O-E19-17) ; glissement exact (O-E19-42).
- Rebond contre une entité solide : inconnu dans l'oracle, à observer en recette.

#### E20.d1 — Tâches

- ✅ **T0 — Oracle dans l'annexe (commit docs).**
  - Copier `base/` et `out/` du dossier de `run-oracle` (`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20d/run-oracle/`) dans
    `docs/plan-e20-annexe/e20-d/oracle/`, sans `work/` ni `d1/`.
  - Rendre `tools/compare.py` identique à `e20-c2/oracle/tools/compare.py`.
  - Écrire un README court : commande de rejeu, groupes G1 à G10, colonnes, limites.
  - Preuve : `manifest.py` redonne `out/manifest.txt`.
- ✅ **T1 — Tables et champs.**
  - Tables `0x80022CAC` et `0x80022CEC`, avec un test octet par octet contre l'exe.
  - Champ du compteur `0x80126FF4`.
  - Constantes `0x03`, `0x04`, `0x29`, `0x2A`, `0x3E`, `0x1C`.
  - Aide « regard du tick ».
- ✅ **T2 — Entrée et états.**
  - Triangle dans `ApplyGroundOrAirTail`, après le bloc de la Croix, avec `AnimCompleteCounter = 0`.
  - Branches `0x29`, `0x03`, `0x2A`, `0x3E`/`0x1C`, dans l'ordre du binaire, y compris le second appel de `PlayerTryAttack` et la
    branche `TryUseItem == 0`.
  - Effet 9 du rebond par l'étage d'effets, à Z = `PosZ + 1 + 0x100000`.
  - Garde de la pente 4.
  - SJ3 inversé : « Triangle seul » donne `0x29`, `AnimCompleteCounter` 0, direction gardée.
- ✅ **T3 — Moyens de test.**
  - `HeroAttackClock` gagne les cinq états (durées ci-dessus) et la fin Hold ; aucun test d'E20.b ne doit bouger.
  - Un lecteur du format de l'oracle d'E20.c2-0 : celui d'E20.c2a T5 s'il existe, sinon un neuf.
- ✅ **T4 — Tests** (acceptation ci-dessous) **et arcs** au vrai préfab (carte 10 ou 14 ; l'échelle de la 389 en courant vers le
  haut). Fait le 2026-10-10 : `AlundraHeroRunTests` (montage d'E20.b, oracle tick par tick), `AlundraHeroRunArenaTests` (carte 14,
  vrai préfab, vraie passe de coups), `AlundraHeroRunStatesTests` (branches hors oracle). Seul manque : l'échelle de la 389 au vrai
  préfab (l'intro de la carte verrouille et déplace le héros, aucun arc de la 389 n'existe) ; elle est couverte par G8 (oracle) et
  par la recette.
- ✅ **T5 — Plan.** Résultats, §0.2.3 corrigé, ligne d'O-E20-12 mise à jour. Fait le 2026-10-10.

#### E20.d1 — Acceptation

À 1 tick par image, champ par champ contre l'oracle : `anim_target`, `anim_cur`, `dir_target`, forces, positions relatives,
`pos_z_dll`, `on_ground` (règle d'AT8 au tick de la perte), `sprint_cnt`, `charge_cnt`, sons de changement d'animation, effet 9.

- **G1, départs.**
  - Quatre cardinales : `0x29` aux ticks 1-25, course au 26 ; ForceX 64512, 129024, 193536, 258048 aux ticks 26-29 (Y ±43008 à
    ±172032) ; son 13 au 26.
  - T seul, Gauche+Droite et les quatre diagonales : `0x29` sans fin, forces nulles ; direction 0x14, 0x1C, 0x04, 0x0C ; regard
    3, 3, 2, 2 depuis l'est.
  - Direction ajoutée au tick 10, 24, 25, 30 ou 40 : départ au 26, 26, 26, 30, 40.
  - Demi-tour au 10 : départ au 35 ; au 20 : départ au 45.
  - Diagonale 1-29 puis Droite : départ au 30 ; diagonale 1-9 puis Droite : départ au 26.
  - Marche puis Triangle : `0x29` au 20, force 0 dès ce tick, départ au 45.
  - Saut et Triangle : `0x29` au 22, départ au 47.
  - Triangle relâché au 10 : Idle au 10, Moving dès le 11.
- **G3, cône (52 cas).** La course continue pour sa direction, les deux diagonales voisines, Gauche+Droite et Haut+Bas. Sinon `0x2A`
  au 41, compteur 0 ; compteur 1 si toutes les directions sont relâchées.
- **G4, arrêt et reprise.**
  - Arrêt : `0x2A` aux ticks 41-56 ; ForceX 249984, puis −8064 par tick jusqu'à 129024 ; Idle au 57 (64512) ; 0 au 58.
  - Reprise au tick même de l'appui pour k = 10 à 16 (ticks 51 à 57), avec la force reprise : k = 12 donne 161280, puis 185472,
    209664, 233856, 258048, et le son 13.
  - k ≤ 9 ou k ≥ 17 : Idle au 57, `0x29` 58-82, course au 83.
  - Haut ou Gauche à k = 10-12 : direction 0x10 ou 0x08. Haut+Droite : pas de reprise, compteur gardé.
- **G5, rebond.**
  - Mur ouest : contact au 76, `ForceAdjusted` au 77.
    - `0x3E` aux ticks 78-88 : direction 08→18, ForceX +196608, ForceZ 262144, effet 9 à Z + 16 px, son 28, recul de 33 px,
      sommet à 18 px.
    - `0x01` au 89 (ForceX 18432), `0x2C` 90-94, `0x29` au 95.
  - Mur nord : `0x3E` 55-65. Mur sud : `0x3E` au 95.
  - Mur est : `0x3E` au 87 dans le binaire. Si la DLL rebondit au 86, c'est l'écart O-E19-42 / D-E19-58, écrit.
  - Course partie collée au mur : `0x3E` au 27. Pad relâché : `0x2D` 99-103, puis Idle.
  - Une marche montante de 16 ou 32 px fait rebondir comme un mur ; en marchant, Moving sans rebond. Carré pendant le rebond : rien.
- **G6, rebord.** `0x2D` au 90 (ForceX 129024), `0x2C` 91-100 (139776, puis 150528), atterrissage au 100, `0x29` au 101.
  - Chute de 16 px : `0x29` au 98. Chute de 64 px : au 106.
  - Triangle relâché en l'air : `0x01` au 101. Tout relâché : Idle.
  - Carré en l'air : `0x14` 93-100, Idle au 101, `0x29` au 102.
- **G7, charge `0x04`.**
  - Carré, Croix ou Rond pendant un tick au 40 : `0x04` 40-71, son 83, ForceX 254016 puis −4032 par tick (93,52 px) ; Idle au 72,
    `0x29` 73-97, course au 98.
  - Dans `0x2A` : rien. Dans la préparation : Carré donne `0x10` aux ticks 10-30, Idle au 31, course au 57 ; Croix et Rond : rien.
  - Épée sacrée, Carré tenu dès le 30 : compteur 1, 16, 31, 60 aux ticks 30, 45, 60, 90.
  - Beannoïde de la 14 (s = 44) : coup au 61 avec 3 étincelles, comparé jusqu'au tick 64 (blessure au 65, E20.c2b) ; balayage
    s = 30 à 62 aux ticks de l'oracle.
- **G8, échelle.** Pente 6 au 51, `ForceAdjusted` au 53, `0x0E` au 54, sans `0x3E`. Sans échelle : `0x3E` au 54.
- **G9.**
  - Eau peu profonde : ForceX 129024 sans bottes, 258048 avec.
  - Glace : 20160, 60480, 141120 aux ticks 30, 40, 60.
  - Eau profonde (garde, écart) : au tick où les quatre coins sont sur la pente 4, Idle au tick suivant, puis la marche.
- **2 ticks par image.** Un départ qui change le regard (diagonale puis Haut) ne sort pas du cône. Les épingles de rattrapage
  d'E20.c1a ne changent pas.
- **Épingles et preuves générales.**
  - Seule SJ3 change. Traces du héros et de l'intro inchangées ; sonde du vrai jeu inchangée.
  - Build Release puis Debug ; `Alundra.Tests` sans échec ; verifier frais.
- **Recette de l'auteur, au clavier et à la croix.**
  - Courir, s'arrêter, reprendre entre 10 et 16 images après l'arrêt, foncer, buter contre un mur (petit saut en arrière), sauter
    d'un rebord, grimper en courant vers le haut.
  - Sur l'eau profonde, le héros marche.
  - Fermer l'inventaire par Triangle en le gardant tenu lance la préparation, comme l'original.

#### E20.d1 — Résultats (2026-10-10, branche `chantier/e20-heros`)

Commits : T0 `af42753` (oracle dans l'annexe), T1 `fbc3ecc` (tables, champ, noms, regard du tick), T2 `aaa238c` (Triangle, quatre
gestionnaires, effet 9, garde de la pente 4, SJ3 inversé), T3 `b5c9556` (moyens de test), T4 `358f958` (tests), T5 (ce plan).
`Alundra.Tests` : 3851 avant la tranche, 4195 après (+344), sans échec en Release et en Debug ; `Alundra.dll` du projet égale celle de
`Alundra/bin/Debug`. Traces du héros et de l'intro inchangées (aucune différence hors fins de ligne). Seul SJ3 change parmi les
tests existants (inversé : Triangle seul donne `0x29`, compteur `0`, direction gardée).

- **Valeurs écrites avant la première exécution, mesurées sur la DLL, toutes tenues** : G1 (26 traces, départs 26, 30, 40, 35, 45,
  47 ; forces 64512 à 258048 aux ticks 26-29, Y ±43008 à ±172032 ; diagonales, T seul et Gauche+Droite sans fin ; Idle au 10 puis
  Moving au 11), G3 (les 52 cas : la course continue pour sa direction, ses deux diagonales, Gauche+Droite et Haut+Bas ; sinon `0x2A`
  au 41, compteur 0, ou 1 sans direction), G4 (arrêt 41-56 de 249984 à 129024, Idle au 57 à 64512, reprise de k = 10 à 16 avec la force
  de la glissade, k ≤ 9 ou ≥ 17 perdu, Haut et Gauche prennent leur direction, Haut+Droite garde le compteur), G5 (mur ouest exact :
  `0x3E` 78-88, +196608, ForceZ 262144, effet 9 à Z + 16 px, son 28, recul de 33 px, sommet à 18 px, `0x01` au 89, `0x2C` 90-94,
  `0x29` au 95 ; mur nord 55-65 ; mur sud 95 ; course collée au mur 27 ; marches de 16 et 32 px ; marche sans rebond), G6 (`0x2D` au
  90, `0x2C` 91-100, `0x29` au 101, 98 à 16 px, 106 à 64 px, `0x01` Triangle relâché, Idle tout relâché, `0x14` 93-100 au Carré),
  G7 (Tackle 40-71 pour Carré, Croix ou Rond : son 83, 254016 puis −4032, 93,52 px, Idle au 72, `0x29` 73-97, course au 98 ;
  rien dans `0x2A` ; Carré dans la préparation `0x10` 10-30, Idle 31, course 57 ; compteur de l'épée sacrée 1, 16, 31, 60 ;
  Beannoïde de la 14 au vrai préfab, coup au 61 avec 3 étincelles, comparé jusqu'au tick qui précède la blessure, balayage s = 30 à
  62 : 17 traces), G8 (pente 6 au 51, `ForceAdjusted` au 53, `0x0E` au 54, sans `0x3E` ; sans échelle `0x3E` au 54), G9 (eau peu
  profonde 129024 sans bottes, 258048 avec ; glace 20160, 60480, 141120), deux ticks par image (le départ qui change le regard ne
  sort pas du cône). Mutation réelle (regard de l'image à la place du regard du tick) : seul le test des deux ticks par image
  devient rouge.
- **Mur est** : la DLL rebondit au tick 86, l'oracle au 87 (O-E19-42 / D-E19-58 : elle s'arrête au contact au 84, à 14745600, quand
  le binaire s'arrête une unité avant, à 14745599, et fait cette unité au 85). Les traces du mur est, de ses quatre variantes et des
  marches sont comparées avec le pad de l'oracle décalé d'un tick à partir du 85 : tout le reste est exact, à une unité près au 84.
- **Garde de l'eau profonde** : au tick où les quatre coins sont sur la pente 4 (le 89 de `R9-deepwater-boots2`), le tick suivant
  met Idle (le binaire met `0x1D`), le suivant Moving ; Triangle sur l'eau profonde ne lance pas la préparation (le héros marche) ;
  `0x3E` et `0x1C` retournent la direction d'abord. Le mur d'eau profonde sans bottes (`R9-deepwater-noboots`) et la nage (départs dans
  l'eau) ne sont pas portés : E20.i et O-E19-22.
- **Écarts écrits, hors périmètre ou en bouchon** : le pas, la poussière et l'atterrissage (sons 12 et 11, effet 7 : E20.e) ne sont
  pas comparés ; le son 3 du Rond dans la préparation est `TryUseItem` (E20.h) ; la fin de la glace diffère de 16 unités (cause
  établie par le verifier : le contrôleur de personnage du moteur ignore un pas plus petit que `MinMoveDistance`, 0,001 px, soit
  environ 65 unités, `CharacterControllerComponent.cs` ; préexistant ; avec `MinMoveDistance = 0` la trace est exacte ; la mention
  « le mover travaille en flottants » du test est à corriger) ; le bord de la carte (`R2-mapedge`, O-E19-17) ne rebondit pas dans la DLL ;
  la colonne `on_ground` d'un rebord est comparée au tick d'avant, au tick de la perte (O-E20-12, comme AT8) ; la blessure du héros
  après le coup du Tackle est E20.c2b.
- **Moyens de test** : l'Idle de la fiche du héros porte l'octet d'accélération 1 (la glissade finit à 64512 au tick 57, pas à 0) ; la
  horloge des coups d'E20.b garde son 0 pour ne rien déplacer, et le montage de la course le corrige pour lui seul. `TryUseItem` a un
  point d'accès de test (branches « résultat 0 » du binaire, sinon inatteignables).
- **Non fait** : l'échelle de la 389 au vrai préfab (l'intro de la carte verrouille et déplace le héros) ; la recette de l'auteur.
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : tables et gestionnaires `0x29`, `0x03`, `0x2A`, `0x3E`/`0x1C`, queue
  et table de la nage redécodés ; manifeste de l'oracle reproduit (557 fichiers), G5 et G9 régénérés à l'identique ; huit mutations
  attrapées (rangée du cône, borne 11 de la reprise, `AnimCompleteCounter`, porte b0, Z de l'effet 9, retournement de l'eau profonde,
  masque du Tackle, compteur de glissade) ; **sonde du vrai jeu** relancée deux fois : la nouvelle partie est inchangée (seuls des écarts
  de rythme d'images).
- **Reportés** : P3 : l'écart de la glace a une cause moteur (`MinMoveDistance`) et non le flottant ; son allocation n'était pas dans les
  écarts du plan ; à corriger dans le commentaire du test, et à juger côté moteur (règle : un défaut se corrige dans le moteur) ; P3 :
  l'échelle de la 389 au vrai préfab reste à tester (la fixture `AlundraLadderClimbTests.cs` tourne la vraie carte sans l'intro) ; P4 :
  le commentaire « le 0 de ses oracles » de l'horloge d'E20.b est inexact (l'Idle exporté a l'accélération 1 ; l'horloge d'E20.b garde 0
  à cause de son montage, sans effet en production) ; P4 : le cas de deux ticks par image vérifie le comportement, pas l'oracle champ
  par champ (ce que le plan demandait).

#### E20.d2 — Tâches

- ⏳ **S1 — L'échantillon du stick.**
  - `AlundraPadState` gagne : les bits de direction du clavier et de la croix (D), les bits du stick (S), et x, y signés
    (`ButtonState.Value` des actions `Move*Stick`). Valeurs par défaut nulles : les initialiseurs existants ne changent pas de sens.
  - `ComputePadState` garde le OU dans `ButtonsHold`.
  - `AlundraTickPad` ne change pas. L'échantillon de l'image est recopié dans chaque pad de tick : au `FromTickPad` du bloc des
    entités, et dans le mode autonome.
- ⏳ **S2 — La règle**, aux deux lectures seulement : le départ dans `0x29`, la reprise dans `0x2A` à 11 et plus.
  - Elle ne joue que si D est vide et S est une diagonale.
  - \|x\| − \|y\| > ε : bit horizontal ; \|y\| − \|x\| > ε : bit vertical ; sinon l'axe du regard du tick. ε = 1/16 (D8).
  - Tout le reste garde le OU : la marche, le cône, la fenêtre courte, les sorties.
- ⏳ **S3 — La sonde.** Une variable d'environnement, comme `DebugIgnoreControlLock`, journalise x, y, D, S et le nibble choisi à
  chaque lecture. Pas de `Program.cs`.
- ⏳ **S4 — Tests.**
  - `ComputePadState` avec des valeurs analogiques.
  - Balayage de 0 à 359° aux rayons 1, 0,75 et 0,6, contre la règle écrite.
  - Les 48 départs et les 15 reprises de G10 : le nibble choisi par la règle égale celui de l'oracle.
  - G1 rejoué au clavier, inchangé.
- ⏳ **S5 — Mesure de l'auteur.** ε réglé ou gardé ; D-E20-15 complétée.

**Acceptation d'E20.d2.** S4 vert ; E20.d1 inchangée ; verifier frais. Recette au stick : une diagonale franche part sur son axe
dominant, une diagonale à 45° sur l'axe du regard, et la marche ne change pas.

#### Arrêts, budget, retour arrière (E20.d1 et E20.d2)

- **Arrêts.**
  - Un écart à l'oracle que ni O-E19-42 (mur est seulement : `0x3E` au tick 86 ou 87, les ticks suivants décalés d'autant ; les murs
    ouest, nord et sud sont exacts), ni la colonne `on_ground` (O-E20-12), ni E20.e ou E20.c2b non portées n'expliquent.
  - E20.c2a revient REFUTED ou INCONCLUSIVE de son verifier : E20.d1 ne part pas.
  - Un test qui change autre que SJ3 sans raison du binaire.
  - Un besoin de toucher au moteur ; un repère de l'intro ou un arc de la chaîne qui bouge.
  - En E20.d2, une modification de la marche.
  - Tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Budget**, par sous-tranche : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits de la sous-tranche.

**Questions pour l'auteur.**
- **Q-D1** (bloque seulement la clôture d'E20.d2) : la mesure du stick. Avec quelle manette ? Vous lancez le jeu avec la sonde de S3
  et renvoyez x, y aux huit directions. On garde ε = 1/16 si la mesure ne montre rien ?
- **Q-D2** (non bloquante, le défaut s'applique) : sur l'eau profonde, le garde fait marcher le héros à la place de la course.
  Préférez-vous porter d'abord le mur d'eau profonde sans bottes (O-E19-22, Q-N3), qui rendrait la course fidèle avec l'équipement
  de départ ?

**Corrections du §0.2.3** (à reporter) :
- la préparation dure 25 ticks, et un changement de regard la relance ;
- poussière de `0x29` : aucune pendant la première boucle, puis toutes les 8 images (`+0xB0` à 1 ou 2), puis toutes les 4 (E20.e) ;
- `0x3E` dure 11 ticks puis passe à `0x01` en l'air : c'est le chemin normal ;
- sans bottes de niveau ≥ 2, l'eau profonde est un mur (O-E19-22).

**Ligne O-E20-12** : « à 1 tick par image, les décisions sont exactes ; l'écart ne se voit qu'à 2 ticks et plus par image ; la
lecture par tick demande la règle de recollement au sol de 3 px `0x80037848` et la re-dérivation de UH7, AT8, AT8n, AT8b et des
traces highground ; tranche à part après E20.d ».

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
| O-E20-12 | Le contact au sol du héros est lu une fois par image, en tête d'image (E19.d2c1 R6) : la perte du sol au bord d'un rebord et un sol qui s'abaisse en cours d'image arrivent un tick après le binaire (`AT8`, `AT8n`, `AT8b` d'E20.b). L'aligner demanderait de relire le contact à chaque tick | Tranché le 2026-10-10 (E20.d) : à 1 tick par image, les décisions sont exactes (le rebord de la course est épinglé comme AT8) ; l'écart ne se voit qu'à 2 ticks et plus par image ; la lecture par tick demande la règle de recollement au sol de 3 px `0x80037848` et la re-dérivation de UH7, AT8, AT8n, AT8b et des traces highground : tranche à part après E20.d |
| O-E20-13 | La DLL ajoute les entités neuves en fin de liste, alors que le binaire réutilise la première case libre (`FindFreeEntitySlot`) : après un recyclage en cours de carte, l'ordre de la passe de coups (« premier attaquant ») peut différer | à mesurer avec E20.e0 (apparitions) ou E14 |
| O-E20-11 | Une entité neuve tourne-t-elle dans le même tick dans le binaire (répétition de `0x800386D0`, contrôle de D7 : non vérifié) ? E20.a garde la règle d'aujourd'hui (premier tick à l'image suivante) | à mesurer, avec E20.e0 |
| O-E20-14 | Atteignabilité en jeu des salles des leviers : la 115 nord-ouest après le hall (E14.a) ; la 116 derrière la grille (E14.a) ; la salle de la bascule de la 117, où la bascule n'apparaît que si l'on arrive par les portails internes 2, 4 ou 6 de la 117 (les arrivées depuis la 115 et la 116 sont hors de sa zone) ; la 44, dont on ne sait pas si le chemin vers les leviers passe par la case de la vague (43..44, 20) | recette de l'auteur, après E14.a |
| O-E20-15 | Poignée d'aura périmée (`0x8002F730`-`0x8002F744`) : reproduire le défaut de l'original, ou le corriger par un numéro de génération (recommandé) | l'auteur, avec E20.f1b |
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
