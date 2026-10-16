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
  brève qui en résulte dans la DLL est un écart documenté. **Caduque (2026-10-10)** : E20.i précède E20.c2b et porte les gestionnaires
  de `0x3A` et `0x3B` ; E20.c2b n'aura qu'à ajouter l'entrée (`+0x88 = (pente 4) ? 0x3A : 0x31`), la « blessure brève » n'existera jamais.
- **D-E20-24** — **Ordre de la suite** (« Je veux dans l'ordre : les armes, l'utilisation des objets et les collisions ») : E20.f
  (armes), puis E20.h (objets du Rond), puis les collisions, que la session principale lit comme E20.g (porter et lancer ; caisses et
  jarres redeviennent des obstacles, fin des écarts D-E19-28 et D-E19-46) — lecture à confirmer par l'auteur. Les prérequis de chaque
  tranche passent avant elle (par exemple l'apparition d'E20.e0 avant les projectiles) ; E14.a et la blessure d'E20.c2 suivent.
  Mode AUTO accordé par l'auteur pour la nuit du 2026-10-09 (ni push ni merge).

Le 2026-10-10 au matin, l'auteur a répondu aux questions de la nuit (« 1 a, 2 b, 3 a, 4 a, 5 a, 6 a, 7 a, sonde, 9 a » ; ADR-0057 et
ADR-0058) :

- **D-E20-25** (modifie D-E20-18) — E17, C60 et E9 passent à E20.f (E20.f5, E20.f6) ; C54 reste à E14. Q-N1 tranchée.
- **D-E20-26** — L'export de l'effet de bris reste dans E14.a (D-E14-2) ; la flèche chargée (E20.f4) s'en sert une fois E14.a faite.
  Q-N2 tranchée.
- **D-E20-27** — « Collisions » désigne E20.g (porter, lancer ; caisses et jarres solides). Q-N3 tranchée.
- **D-E20-28** (modifie ADR-0046 pour le héros) — Le héros crée et libère ses effets tout de suite et lit `Destroying` au tick. Q-N4.
  Précisé par ADR-0059 (proposée, à confirmer par l'auteur) : la poignée est effacée tout de suite mais l'emplacement est rendu en file
  à son tick, et l'aura est recréée aussi quand son emplacement ne la porte plus.
- **D-E20-29** (modifie ADR-0049) — Un effet attaché se trie sur la coordonnée du sprite de son ancre plus `DepthOffset`. Q-N5.
- **D-E20-30** — La poignée d'aura périmée est corrigée (numéro de génération). O-E20-15 tranché.
- **D-E14-8** (plan maître) — E14.a passe en premier (E14.a0, E14.a0b, E14.a1), son plan est approuvé. O-E14-0 tranché.
- **D-E20-31** — Le mur d'eau profonde (O-E19-22) et les courants (O-E20-16) forment ensemble une tranche E20.i2. Q-I1 tranchée
  (lecture de « 9 a », à confirmer par l'auteur s'il voulait seulement les courants).
- **D-E20-32** — Pour l'arrondi du stick (E20.d2), une sonde est construite d'abord ; l'auteur mesure sa manette avec, puis la règle
  est close. Q-D1.
- **D-E20-33** (2026-10-10, après-midi) — L'export d'E20.e0x ajoute aussi l'ensemble des indices valides de la page des cartes,
  carte par carte, pour que la DLL puisse reproduire les apparitions qui échouent en silence dans l'original (un indice absent de la
  table de la carte courante : `0x80039B28` rend 0 ; exemple : Zorgia, C44, indice 213 sur les cartes 388, 432 et 451).
- **D-E20-34** (2026-10-15, réponse à Q-A2) — « A deux fois » (une apparition dans le programme d'une autre entité, sur une case
  au-dessus du compteur, fait tourner A deux fois dans le binaire) reste un écart documenté de la DLL (E20.e0a3, D-E0A-6, O-E0A-1).
- **D-E20-35** (2026-10-15, réponse à Q-A3) — Une entité née pendant un tick tourne dès ce tick mais n'est dessinée qu'à l'image
  suivante (intégration du moteur) ; pas d'intégration immédiate, pas de changement du moteur.

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
- **La nage (E20.i, 2026-10-10, §1.14, mesurée sur l'oracle `out-swim/`)** :
  - sur la pente 4, `MovePlayer` quitte tout au profit de la branche `0x8003260C` : charge et pointeur de tir à 0, `UpdatePlayerCarriedEntity(2)`,
    puis la table `0x80022E7C` (80 états, six genres) ; ni les trois appels avant l'aiguillage, ni `PlayerTryAttack`, ni `PlayerTryAction` ;
  - flotter `0x1D` (36 ticks, décélération `>> 3`), nager `0x0F` (110592 en X, 73728 en Y, diagonales 78192 et 52128, rampe de 6912 par
    tick depuis le repos, son 22 à chaque enchaînement sur lui-même, 42 ticks), élan `0x28` (au front de Triangle, Croix ou Carré, `& 0xD0` ;
    135168 en X, 90112 en Y, 42 ticks, son 21, direction figée, rien n'est lu pendant l'élan), blessure `0x3A` (4 ticks, son 23) puis `0x3B`
    (8 ticks, tenu), sortie `0x1D` à `+0xAC == 1` avec la direction retournée ;
  - toute entrée dans l'eau coûte un tick de `0x1D` (un front de plongée sur ce tick est perdu) ; l'élan de la marche ou de la course est conservé
    (`R9-deepwater-boots2` : 258048 au 89, 225792 au 90, 218592 au 91, `-7200` par tick jusqu'à 110592) ;
  - la sortie vers la terre est géométrique (une case hors de l'eau suffit : pente 0), par la table générale `0x80022D3C` : `0x0F`, `0x1D`,
    `0x28` donnent Idle au sol ou `0x2D` en l'air, `0x3A` et `0x3B` retournent la direction d'abord (`0x80031DB4`) ;
  - hors de la DLL aujourd'hui : le mur d'eau profonde sans bottes (O-E19-22), les courants (O-E20-16), l'éclaboussure, le sillage et la ride de
    l'élan (E20.e).

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
| E20.d | Course : les cinq états, entrée et sorties, cône, reprise, rebond, charge `0x04` (ses coups viennent d'E20.c1), escalade prioritaire, arrondi du stick (D-E20-8) ; le test épinglé « Triangle seul donne Idle » s'inverse | oracle de `MovePlayer` ; arcs | Courir, s'arrêter, reprendre, foncer, buter contre un mur, sauter d'un rebord ; sur l'eau profonde, le héros nage (E20.i, 2026-10-10 : le garde d'E20.d1 a disparu) |
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
  - **E20.i** : E20.a, E20.c2a, E20.d1, E20.f1a. L'arête « E20.c2 pour l'état blessé dans l'eau (`0x3A`) » s'est **inversée** (2026-10-10) :
    E20.i porte les gestionnaires de `0x3A` et `0x3B`, c'est la branche de l'eau d'E20.c2b qui dépend d'E20.i.
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
**Révision 4 (2026-10-10, après les réponses de l'auteur ; nouvelle époque de relecture).** L'auteur a tranché Q-N1 (D-E20-25 : E17,
C60 et E9 passent à E20.f), Q-N2 (D-E20-26 : l'export de l'effet de bris reste dans E14.a, fait par E14.a1a) et Q-N3 (D-E20-27 :
« collisions » = E20.g ; le mur d'eau profonde va avec les courants dans E20.i2, D-E20-31) ; il a avancé E14.a avant les armes
(D-E14-8). Depuis : E20.f0, f1a, f1b, f2, c2-0, c2a, d1, d2a et i sont CONFIRMED ; E14.a0, a0b, a1a et a1b sont CONFIRMED (le registre
par type, l'octet de bris exporté, `DestroyEntity(e, fx)` avec l'effet de bris pour tous ses appelants) ; E14.a1c est en cours. Le
tableau ci-dessous est remplacé par celui de la révision 4 : tranches restantes seulement, prérequis remis à jour, ordre « armes,
objets, collisions » avec les prérequis d'abord (D-E20-24). Une seule relecture de clôture suit.
**Révision 5 et pause (2026-10-10).** La relecture de clôture de la révision 4 rend **REVISE** : le couvercle de la 362, renvoyé à
E20.h par E20.c1b-1 et E20.c1b-2 et sur la chaîne de l'histoire, n'avait plus de tranche. Corrigé (FIX) : tranche E20.A362 (arc de
test seul, bombe d'Olen ; `e20-armes/h-g-deps.md` §0.1, `order-critic.md` A362). Points non bloquants corrigés : textes d'avant les
réponses de l'auteur marqués tranchés dans les faits porteurs, titre du §1.9, appels de `DestroyEntity` des projectiles, ordre
interne de g3/g4, repli d'E14.a1c. La règle de relecture ne permet pas une nouvelle relecture automatique : l'enveloppe est **en
pause** jusqu'à l'accord de l'auteur pour une relecture de clôture de plus.
L'auteur l'a accordée (2026-10-10) ; relecture de clôture de la révision 5 : **READY**. L'enveloppe fait foi ; la suite commence par
E20.e0x.
**Révision 6 (2026-10-15, après la découverte d'E20.e0b ; nouvelle époque de relecture).** L'auteur a accordé (Q-B3, §1.17) le
découpage d'E20.e0b en e0b1 (l'avance du générateur par tick) puis e0b2 (le tirage du butin à la création), et les prérequis qui en
découlent : E20.f3 et E20.f6 exigent e0b1 (E9 tire à l'impact et toutes les 8 images dans le flux avancé ; f3 ne tire rien à la
création, ses sprites communs ont `Contents` 0, `f-projectiles.md` point 7 : il lui faut le flux que verront les étincelles de coup) ;
E20.g2 et E20.e0c exigent e0b2 (le lâcher, à la destruction, lit `+0x3C` écrit par le tirage, `order-critic.md` ligne 22 du tableau ;
l'arête « E20.g2 → E20.e0b » de la révision 3, perdue par le tableau de la révision 4, est rétablie). Lignes 3, 5, 8 et 10 du tableau
et paragraphe « Ensuite » mis à jour. Une seule relecture de clôture suit.
Relecture de clôture de la révision 6 : **READY** (2026-10-15).
**Révision 7 (2026-10-15, mode AUTO, après la découverte d'E20.f3 ; nouvelle époque de relecture).** La découverte d'E20.f3 (§1.19) la
découpe en f3a (direction du tir, couture de branchement sans type activé), f3b (effets de coup 4 et 5) et f3c (la vague de l'épée,
C61 et E10). f3a et f3b n'ont besoin ni d'E20.e0k ni d'E20.e0b2 ; f3c a besoin d'une règle du domaine vertical d'E20.e0k (le Z logique
et `IsOnGround` positionnel des entités nées par l'interface sans gravité : sans elle, la vague perd l'unité brute qui la tient
au-dessus du sol et meurt en 2 ticks au lieu de 26). Ligne 5 du tableau mise à jour ; ordre : f3a et f3b avant E20.e0k (E20.e0b2 est
en pause), f3c après E20.e0k. Choix de la session principale en mode AUTO, réversible, signalé à l'auteur.
Relecture de la révision 7 : **REVISE**, un point corrigé (FIX) : f3b lit l'oracle que seul le T0 d'E20.f3a (commit de l'oracle commun `docs/plan-e20-annexe/e20-f3/oracle/` : `hitfx_table` en JSON avec ses clés, `hitfx_mut`, sonde de la flamme) produit ;
f3b exige donc ce T0 (pas les phases de code d'f3a). Une seconde relecture suit.
Seconde relecture de la révision 7 : **READY** (2026-10-15).
**Révision 8 (2026-10-15, mode AUTO, après la découverte d'E20.e0k ; nouvelle époque de relecture).** La découverte d'E20.e0k (§1.18)
corrige la prémisse de la ligne 4 : les entités autres que le héros ont déjà un pas vertical par tick ; ce qui manque est l'état de
carte des entités nées par l'interface (e0k1), leur Z logique et le pas vertical du binaire (e0k2), le bord de la carte (e0k3) ; e0k4
est conditionnelle (Q-K2, option B). Q-K1 et Q-K2 : défaut A appliqué par la session principale jusqu'à la réponse de l'auteur.
Prérequis : f3c exige e0k1 et e0k2 (la règle du Z logique ; D-F3-8 corrigé : marqueur d'e0k2, plus `EntityRefId == −1`) ; f4, f5 et
f6 exigent e0k1 à e0k3 ; g1 et g2 exigent e0k2 (g1 pose le marqueur sur une entité par enregistrement qu'il soulève) ; h3 exige e0k2 ;
h4 exige e0k1 à e0k3 ; E20.e0c (le butin lâché et l'objet ramassable, nés par l'interface avec la gravité) exige e0k1 à e0k3, et E20.e
(l'herbe coupée, dont le butin naît de même) exige E20.e0c. Ordre :
f3a, f3b, e0k1, e0k2, f3c, e0k3, puis la suite du tableau. Choix de la session principale en mode AUTO,
réversible, signalé à l'auteur.
Relecture de la révision 8 : **REVISE**, un point corrigé (FIX) : E20.e0c fait naître par l'interface des objets à gravité (30 à 64) et
n'exigeait pas e0k ; elle exige maintenant e0k1 à e0k3 (paragraphe « Ensuite » et dépendants de §1.18). Une seconde relecture suit.
Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures automatiques atteint) : **FIX**
— E20.e (l'herbe coupée) fait naître son butin par le même `SpawnWarpEntity` d'un objet à gravité et n'avait aucun prérequis ; elle exige
maintenant E20.c1 ✅ et E20.e0c (donc E20.e0b2 et e0k1 à e0k3). Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture de la révision 8 : **READY** (2026-10-15).
**Révision 9 (2026-10-15, mode AUTO, après la découverte d'E20.f4 à E20.f6 ; nouvelle époque de relecture).** La découverte commune des
trois tranches de projectiles (§1.20 à §1.22) scinde E20.f6 en f6a (les attributs de tuile des entités marquées à gravité, `+0x18C` :
la mort des boules dans l'eau) et f6b (C60, E9, le branchement des types 11, 12, 14, 15) ; elle corrige la ligne 8 : les trois
enregistrements `0x0C` de la carte 309 (sprite 12) émettent des étincelles dès que C60 est enregistré, E9 ne tournant jamais pour eux.
Prérequis de f4 inchangés. Ordre inchangé (f4, f5, f6a, f6b après e0k3). Choix de la session principale en mode AUTO, réversible,
signalé à l'auteur.
Relecture de la révision 9 : **REVISE**, deux points corrigés (FIX) : (1) f6a nourrit `+0x18C` des entités marquées à gravité ; les
types qui portent `DestroyOnSlidingSlope` (`0x100000`) avec la gravité (recensement : 1, 2, 10 à 17, 23, 234) n'ont leur mort dans
l'eau profonde qu'avec elle : E20.h3 (graines 1, bombe 2) exige f6a ; E20.e0c n'en a pas besoin (les objets 30 à 64 ne portent pas ce
drapeau) ; O-E0K-2 nomme un propriétaire par attribut ; (2) l'ordre f4, f5, f6a, f6b devient une chaîne ferme (f5 exige f4, f6b exige
f4 et f5), parce que f5 et f6b reprennent l'interrupteur d'arène, les témoins négatifs et l'annexe d'f4, et la règle de vitesse d'f5.
Une seconde relecture suit.
Seconde relecture : **REVISE**, deux blocages ; disposition de la session principale (cap de deux relectures automatiques atteint) :
**FIX** — (1) E20.h4 fait naître les types 16, 17 (terre) et 23 (feu), qui portent `DestroyOnSlidingSlope` avec la gravité : h4 exige
aussi f6a ; (2) les phrases de §1.20 à §1.22 qui parlaient de tranches indépendantes ou d'une « première exécutée » sont réécrites
selon l'ordre ferme (f4 propriétaire des éléments communs, f5 de la règle de vitesse ; f6b exige f4 et f5). Nouvelle époque de relecture :
une seule relecture de clôture suit.
Relecture de clôture de la révision 9 : **READY** (2026-10-15).

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
  propriétaire revient à l'auteur. ~~Question bloquante Q-N1 pour E20.f5 et E20.f6~~ **tranchée : D-E20-25** (E17, C60, E9 à E20.f) ;
  C54 reste à E14.
- [binaire, données] **Rien n'associe un indice de sprite global à un préfab dans le projet exporté** : le convertisseur garde la
  correspondance en mémoire (`SpriteWriter.cs:394`, `:494`). Les bancs de carte sont dédupliqués globalement
  (`SpriteBankReader.cs:174-217`) : une seule table de 256 entrées par page. Un export qui ajoute `BankKey` et `BreakEffect` (octet
  `+0x1E` de l'en-tête, lu par `DestroyEntity` `0x8003A5E8`) à chaque entrée de `Data/sprite-records.json` sert la page globale, la page
  des cartes et D-E14-2 (**tranché : D-E20-26**, l'octet de bris reste dans E14.a, exporté par E14.a1a ; E20.e0x ne fait que `BankKey`).
- [binaire] **Une entité née dans un tick y tourne aussitôt** : statut, programme A, compteurs, listes, animation, physique ; une
  flèche avance de 3 px dans son tick de naissance. La DLL la fait tourner une image plus tard (`TookPartInFrame`,
  `AlundraWorldProxy.cs:2617-2638`) ; la DLL peut l'inclure elle-même dans le reste du tick, sans changement du moteur (`e0-check.md`
  point 1). Cela répond à O-E20-11.
- [binaire] Le binaire réutilise la première case libre (1 à 63, plafond 63) ; la DLL ajoute en fin de liste (O-E20-13).
- [binaire] L'avance du générateur (`0x8002BD0C`-`0x8002BD34`) a lieu une fois par appel de la fonction d'image, même quand les
  entités sont gelées ; la DLL n'en a pas. E9 et l'étincelle de charge tirent dans le générateur seulement quand l'effet est créé.
- [DLL] ~~Seul le héros a un pas vertical par tick~~ (**corrigé par la découverte d'E20.e0k, §1.18** : seul le héros a un état en l'air
  tenu par le tick ; toute entité active a déjà un pas vertical) (`AlundraScriptedMotion.cs:194`, `:198`) ; les flèches volent à Z constant avec
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

**Tranches restantes (révision 4), dans l'ordre** (chacune détaillée, relue, exécutée et vérifiée avant la suivante ; elles touchent
les mêmes fichiers, donc l'une après l'autre). Faites et CONFIRMED, hors du tableau : E20.f0, E20.f1a, E20.f1b, E20.f2, E20.c2-0,
E20.c2a, E20.d1, E20.d2a, E20.i ; et, côté E14 : E14.a0 (registre par type et interrupteur), E14.a0b, E14.a1a (`BreakEffect` exporté,
ADR-0061), E14.a1b (`DestroyEntity(e, fx)` avec l'effet de bris pour tous ses appelants, ADR-0060).

| # | Tranche | Contenu | Prérequis |
|---|---|---|---|
| 1 | E20.e0x (§1.15) | Convertisseur : `BankKey` dans `Data/sprite-records.json` (le `BreakEffect` y est déjà, E14.a1a) et les indices valides par carte (D-E20-33) ; export complet en place, preuve par manifeste et double export ; ADR de format | E14.a1a ✅ |
| 2 | E20.e0a (§1.16 : e0a1, e0a2, e0a3 ✅ ; e0a4 décidée le 2026-10-15, après E20.e0b2, ne bloque rien) | Apparition par indice de sprite (`0x80039F58`) : table des préfabs par page, entité sans enregistrement (`EntityRefId = -1`, parent, racine), cases réutilisées (première libre, plafond 63), participation au tick de naissance | E20.e0x ; E14.a0 ✅ (registre) |
| 3 | E20.e0b (§1.17 : e0b1, puis e0b2) | e0b1 : générateur avancé une fois par tick (`0x8002BD0C`-`0x8002BD34`) ; e0b2 : tirage du butin à la création (écrit le contenu, rien ne tombe) | E20.e0a1, E20.e0a2 (§1.16) ; e0b1 avant e0b2 (ordre) |
| 4 | E20.e0k (§1.18 : e0k1, e0k2, e0k3 ; e0k4 conditionnelle) | État de carte des entités nées par l'interface ; leur Z logique et le pas vertical du binaire (gravité `0x100`, impulsion, atterrissage strict, vol à Z constant, aimant, plafond) ; bord de la carte ; butée sur mur et hauteur par le mover existant | e0k1 : E20.e0a1 à E20.e0a3b ✅ ; e0k2 et e0k3 : e0k1 ; e0k4 : e0k2, e0k3 et l'option B de Q-K2 |
| 5 | E20.f3 (§1.19 : f3a, f3b, f3c) | Tir : `SpawnWeaponProjectile` branché, un type à la fois, jamais avant son gestionnaire E ; direction du tir par `BoxDirection` (P3 d'E20.f1a) ; vague C61 (coupe d'herbe en bouchon) et E10 ; effets de coup 4 et 5 de la passe de coups (porte `(b & 0xC0) != 0x80`, `0x800395E8`) | f3a : E20.f1 ✅, E20.e0a1 à E20.e0a3 ✅ ; f3b : le T0 d'E20.f3a (oracle commun), E20.c1a ✅, E14.a1b ✅, E20.e0b1 ✅ ; f3c : f3a, E20.e0b1 ✅, E20.e0k1 et E20.e0k2 (la règle du Z logique, D-F3-8) ; f3a et f3b peuvent passer avant E20.e0k |
| 6 | E20.f4 (§1.20) | Flèche chargée : C94, E1 avec l'effet 8 (l'effet de bris vient d'E14.a1b, D-E20-26) | E20.f3, E20.e0k1 à E20.e0k3, E14.a1b ✅ |
| 7 | E20.f5 (§1.21) | Flèches : E17 (D-E20-25) ; `ResyncControllerFromFlags` quand E17 change les drapeaux (§1.18, N9) | E20.f4 (interrupteur d'arène, témoins négatifs, annexe commune), E20.f3, E20.e0k1 à E20.e0k3 |
| 8 | E20.f6 (§1.22 : f6a, f6b) | Boules de feu et de glace : C60, E9 (D-E20-25) ; f6a : les attributs de tuile des entités marquées à gravité ; les trois enregistrements `0x0C` de la carte 309 (sprite 12) émettent des étincelles dès l'enregistrement de C60 (E9 ne tourne jamais pour eux) | f6a : E20.e0k1, E20.e0k2 ; f6b : f6a, E20.f4, E20.f5 (règle de vitesse D-F5-5), E20.f3 (dont f3b), E20.e0b1 (§1.17), E20.e0k1 à E20.e0k3 |
| 9 | E20.h0, E20.h1 | Cadre de l'usage d'un objet (`0x8002ED64`, `0x8003499C`, pilote `0x8003634C`, annulation `0x800350C0`) ; les cinq objets instantanés (soins) | E20.c2a ✅ |
| 9b | E20.A362 | Le couvercle de la 362 (enregistrement 0, sprite 243, `D[1]` `0x87 [128,3]`) face à la bombe d'Olen (enregistrement 5, sprite 2, lancée par son script `0x8B [128,5,…]`, explosion animation 1, attaque attribut 4 puissance 5) : arc de test seul, comme E20.c1b-2 ; il tranche si le couvercle réagit déjà (probable depuis E20.c1a et E20.c1b-1) ou ce qui manque ; relancé après E20.h3 | E20.c1b-1 ✅ |
| 10 | E20.g1, E20.g2 | Soulever et porter ; lancer et briser (effacement du porteur `+0x2C`, laissé en bouchon par E14.a1b) — avant les graines et la bombe, qui naissent portées ; g1 pose le marqueur du Z logique (§1.18, D-E0K-1) sur l'entité soulevée | E20.e0a1 (§1.16), E20.e0k2 ; E14.a1b ✅ et E20.e0b2 (§1.17) pour g2 |
| 11 | E20.h3, E20.h4 | Graines et bombe (coupe de l'herbe par la bombe en bouchon nommé jusqu'à E20.e) ; magies par élément | E20.h0 ; E20.g1, E20.g2 ; E20.e0a1 à E20.e0a3 (§1.16), E20.e0k2 et E20.f6a (mort des graines et de la bombe dans l'eau profonde) pour h3 ; E20.e0k1 à E20.e0k3 et E20.f6a (types 16, 17, 23) pour h4 |
| 12 | E20.g3, E20.g4 | Caisses et jarres redeviennent des obstacles ; la barricade de la 392 (D-E20-27) ; ordre interne fixé au détail (g3 dépend aussi de la géométrie de la barricade de g4 et d'un choix de l'auteur, `order-critic.md`) | E20.g1, E20.g2 |

Les projectiles d'E20.f3, f5 et f6 (E10, E17, E9) appellent aussi `DestroyEntity(e, fx)` : fait par E14.a1b ✅.

Ensuite, hors de l'ordre des armes, objets et collisions : la blessure du héros (E20.c2b, après E14.a, qui remesure ses arcs), le mur
d'eau profonde avec les courants (E20.i2, D-E20-31 ; lecture de « 9 a » à confirmer par l'auteur), le butin et l'objet ramassable
(E20.e0c, qui exige E20.e0b2 et E20.e0k1 à E20.e0k3 : les objets lâchés tombent), les effets du héros et l'herbe coupée (E20.e, qui exige
les boîtes d'E20.c1 ✅ et E20.e0c, donc E20.e0b2 et E20.e0k1 à E20.e0k3 : la coupe fait naître son butin par `0x80032C7C`, le même
`SpawnWarpEntity` d'un objet à gravité, ramassé par C255), la règle du stick (E20.d2b, après la mesure de l'auteur). Si E14.a1c s'arrête
sur la chute des Muruta du hall (arrêt écrit de sa tranche : la chute des entités autres que le héros relève d'E20.e0k), la question
va à l'auteur comme le dit cet arrêt ; recommandation : avancer la part « chute » d'E20.e0k, qui ne dépend pas de l'apparition par
indice (les Muruta sont placés sur la carte), puis reprendre E14.a1c.

**Questions de la nuit, tranchées le 2026-10-10.** Q-N1 → D-E20-25 ; Q-N2 → D-E20-26 ; Q-N3 → D-E20-27 (avec D-E20-31 pour l'eau
profonde). Par défaut fidèle, sans question : le plafond de 63 cases, la participation au tick de naissance (dans la DLL), la flèche
normale qui vole 16 px au-dessus du sol du héros et passe au-dessus des ennemis bas, la cape de sable reportée (elle n'est dans aucune
tranche).

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

> Note (2026-10-10, E14.a1b) : `AlundraBalanceRecord.ByteAt` lit maintenant, au-delà de la taille d'une fiche, la fiche suivante de
> `BALANCE.BIN` (chaînage par le `FileOffset` exporté), comme le binaire ; avant, il rendait 0. Exigé par 60 vecteurs D1 de l'oracle
> d'E14.a1 (attribut 15) ; sans effet avec les données réelles (quartets 0 à 11, fiches de 15 octets au moins).
> Voir `docs/plan-e14-ia-native.md` §1.3b, « Vérification ».

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

### 1.9 E20.f1 — Charge et relâchement : E20.f1a 🧪 (faite et CONFIRMED le 2026-10-10, `53ff27f` à `ce804b3`), E20.f1b l'aura 🧪 (faite et CONFIRMED le 2026-10-10, `3e01c11` à `4cb98b8`)

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

#### E20.f1b — L'aura 🧪 (faite et CONFIRMED le 2026-10-10, `3e01c11` à `4cb98b8` : D-E20-28 à D-E20-30, ADR-0057, ADR-0059 proposée ; recette de l'auteur en attente)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, un point corrigé : une libération immédiate rendait l'emplacement de l'aura avant
l'application des créations en file des ticks précédents de la même image (la gerbe du tick 91 prenait l'emplacement 0 dans une image
de 2 ou 3 ticks). La libération garde maintenant l'ordre du binaire : la poignée et la génération sont effacées tout de suite, l'emplacement
est rendu par une opération en file à la position du tick.
**Révision 2 (2026-10-10).** Seconde relecture : **REVISE**, deux points corrigés (FIX) : à 3 ticks par image, au renouvellement, une
étincelle pouvait prendre l'emplacement de l'aura et la poignée resterait liée à l'étincelle : la recréation teste aussi la génération
de l'emplacement (écart corrigé pour N ≥ 3, identique au binaire à un tick par image), avec un cas d'acceptation ; le mécanisme de
libération est consigné dans ADR-0059 (proposée) et au §0.1. Une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-10). Exécution après E14.a0b et la sonde du stick (builds en série).

**Prérequis.** E20.f1a (CONFIRMED : charge, sons, étincelle, gerbe, effacements ; le bouchon `AlundraAuraStub` est remplacé ici) ; E20.f0.
Le projet de section de `e20-f1/check.md` (« Draft revision ») est la base ; ce qui suit l'adapte aux décisions de l'auteur et à
l'existant d'E20.f1a.

**But.** Le héros voit l'aura de sa charge (effet 3 attaché) comme dans le binaire : créée au compteur 11, recréée quand son
emplacement se détruit (tous les 97 ticks d'une charge tenue), libérée sous 11, dessinée juste au-dessus du sprite du héros ; une
aura expirée pendant un verrouillage ne libère plus l'effet qui reprend son emplacement (défaut corrigé, D-E20-30).

**Faits porteurs** (`e20-f1/` : `depth-key.md`, `effect-model.md`, `oracle-ext.md`, `check.md`, contre-vérifiés) :
- [binaire] Compteur ≥ 11, à chaque tick : l'effet 3 attaché est créé si la poignée `0x80126FF8` est nulle **ou si le `+0x7D`
  (drapeau de destruction) de son emplacement est non nul** (`0x8002F570`-`0x8002F58C`), par `0x8003BE74(0, 3, 0, héros, 1, 0, 0,
  0x100000)` ; la poignée reçoit le retour, même nul (`0x8002F5BC`). Compteur < 11 : si la poignée est non nulle, `+0x68` de son
  emplacement à 0, poignée à 0, sans autre test (`0x8002F730`-`0x8002F744`).
- [binaire] Renouvellement : l'animation 0 de l'effet 3 dure 96 ticks et finit par `Destroy` ; la mise à jour c + 96 pose `+0x7D`,
  `MovePlayer` du tick c + 97 recrée l'aura, la passe des effets du même tick libère l'ancienne (créations aux ticks 33, 130, 227).
- [binaire] Clé `+0x1BC` du héros sans porteur ni voisin collidable plus bas qui le chevauche :
  `((PosY + (IDSV << 16)) & 0xFFFF0000) + (short)(PosZ >> 16)`, égale au tri `0x800399B8` sur les 150 ticks de C2-steel (`0x00690000`
  pendant la charge) ; l'aura prend la clé + 1 (le binaire la dessine juste après le héros).
- [DLL] Le réservoir (`AlundraEffectReservoir`) a `CreateAttached`, `Free`, `Destroying` (`+0x7D`), et `IAlundraEffectAnchor` sans
  implémentation de production ; `RunFrame` applique la file puis met à jour les emplacements, tick par tick ; l'aura passerait sous le
  sprite du héros avec la règle d'ADR-0049.
- [convention Z] Les effets prennent le Z du binaire (`PosZ + 1`) ; l'ancre expose `PosZ + 1`.

**Décisions (ADR-0057).**
- **D-E20-28** : le héros crée ses effets tout de suite (`CreateNow`, `CreateAttachedNow` du stade), garde l'emplacement comme
  poignée, lit `Destroying` au tick ; sa libération efface tout de suite sa poignée et note la génération, mais rend l'emplacement par
  une opération en file au tick de la libération (`FreeAtTick`, gardée par la génération), appliquée par `RunFrame` à sa place dans
  l'ordre des ticks : une création en file d'un tick antérieur de la même image ne peut donc pas prendre l'emplacement de l'aura ; un effet créé au tick k d'une image n'est mis à jour par `RunFrame` qu'à partir
  du tick k (marque `FirstUpdateTick`, locale à l'image, effacée à la fin de `RunFrame`, même avec 0 tick) ; le reste reste en file.
  Exact à un tick par image ; écarts pour N ≥ 2, écrits dans le code : (1) une image qui contient c + 96 et c + 97 montre une image
  sans aura ; (2) les tirages de l'étincelle restent à l'application (E20.f1a) ; (3) l'indice d'emplacement du héros passe avant les
  opérations en file de l'image.
- **D-E20-29** : un emplacement ancré (modes 1 et 3) se trie sur la coordonnée du sprite de son ancre plus `DepthOffset` ; l'ancre
  l'expose par un membre par défaut de l'interface ; le mode 0 garde la règle d'ADR-0049.
- **D-E20-30** : la libération ne vise que l'emplacement qui porte encore l'aura (numéro de génération posé par la création du
  réservoir) ; c'est l'écart corrigé, écrit dans le code.
- Ancre et clé (choix de la session principale) : `AlundraEntityScriptProxy` implémente `IAlundraEffectAnchor` (`PosX`, `PosY`,
  `PosZ + 1`, `Status`, `DepthKey` par `AlundraEntityDepthKey.Compute`), l'IDSV venant d'une seule aide partagée avec le sprite du héros
  (`AlundraFrameSyncPasses`, règle `~Target` comprise) ; pas de levée par support, voisin ou porteur (écart écrit, hors des arcs de f1b).

**Périmètre.** `Alundra/Scripts/` : `AlundraEffectStage` (création et libération immédiates, marque) ; `AlundraEffectReservoir`
(numéro de génération) ; l'ancre et `AlundraEntityDepthKey` ; la règle des emplacements ancrés d'`AlundraEffectQuadBuilder` ;
l'aura dans `UpdatePlayerWeaponEffect` à la place d'`AlundraAuraStub` ; tests ; `docs/plan-e20-annexe/e20-f1/oracle/keyprobe.py` copié de
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-f1/f1-check/oe/keyprobe.py` (tri `0x800399B8` après chaque tick, clé du héros contre la formule).

**Hors périmètre.** La levée de clé par voisin ou porteur ; l'IDSV par image (celle de l'image 0 du couple animation-direction est
exacte pour l'animation 0 de la charge) ; l'avance du générateur par tick (E20.e0b).

##### Tâches

- ✅ **B1 — Oracle de la clé.** Copier `keyprobe.py` dans `docs/plan-e20-annexe/e20-f1/oracle/` (chemins relatifs au dépôt), le relancer :
  0 écart sur les 150 ticks de C2-steel.
- ✅ **B2 — Stade.** `CreateNow`, `CreateAttachedNow`, `FreeAtTick` (opération en file, gardée par la génération) ; marque
  `FirstUpdateTick` respectée puis effacée par `RunFrame` ; numéro de génération du réservoir.
- ✅ **B3 — Ancre et clé.** `AlundraEntityDepthKey.Compute`, l'aide d'IDSV partagée, l'implémentation explicite de l'ancre.
- ✅ **B4 — Aura.** Dans `UpdatePlayerWeaponEffect` : création selon la condition (poignée nulle ou `Destroying`, ou, ajout de
  l'ADR-0059, emplacement inactif ou qui ne porte plus la génération de l'aura), poignée gardée même nulle, libération < 11 avec le garde de génération ; `AlundraAuraStub` retiré (ses tests deviennent ceux de l'aura réelle).
- ✅ **B5 — Ordre de dessin.** Test d'abord : chaque quad de l'aura a une clé strictement plus grande que celle du sprite du héros (vrai
  `DepthSortable2DComponent.BuildSortKey` sur la position de son `RenderProjectionComponent`), même seau ; puis la règle des
  emplacements ancrés.
- ✅ **B6 — Tests.**

##### Acceptation

- Contre l'oracle (`e20-f1/oracle/out/`), pour C2-steel, C6, C10, C14, C16 : aura au tick 33 (C2-steel), emplacement, mode 1, ancre =
  héros, `OffsetZ` `0x100000`, `DepthOffset` 1, position, lus après la logique du tick et avant `RunFrame` ; les indices d'emplacement de
  l'étincelle et de la gerbe redeviennent comparables (l'aura occupe l'emplacement 0 comme dans l'oracle).
- Charge tenue 300 ticks à un tick par image : auras créées aux ticks 33, 130 et 227, une seule vivante après chaque mise à jour ;
  `Destroying` posé à la main avant le tick 50 : nouvelle aura à ce tick (scénario A1 de l'oracle) ; avant les ticks 60 et 70 : créations
  aux ticks 33, 60 et 70, emplacement 0 réutilisé (A2) ; réservoir plein : l'aura est retentée à chaque tick (P1).
- Une image de 2 puis de 3 ticks sur le passage du compteur de 10 à 12 donne le même état final que des images d'un tick (une seule
  aura) ; mutations rouges : création par `Emit` → deux auras ; sans marque → une mise à jour de trop ; marque non effacée → une mise
  à jour manquée à l'image suivante. L'écart (1) est épinglé : une seule image affichée sans aura.
- Poignée périmée : aura expirée pendant un verrouillage, emplacement repris par un autre effet : le premier tick normal ne libère pas
  cet autre effet (D-E20-30) ; sans le garde de génération, le test est rouge.
- Renouvellement à 3 ticks par image (ADR-0059) : charge tenue en images de 3 ticks calées pour que c + 96 soit le premier tick d'une
  image et qu'une étincelle naisse à c + 98 (C2-steel : aura créée au 227, renouvellement au 324, étincelle au 325) : après la logique
  de l'image suivante, une aura (effet 3, mode 1, ancre = héros) est vivante et la poignée porte sa génération ; une mutation qui retire
  le test de génération de la condition de création rend le test rouge.
- Clé de l'ancre du héros = sortie de `keyprobe` sur les ticks de charge de C2-steel (`0x00690000`) ; `Compute` contre des valeurs faites
  à la main (`PosZ` entier < 0, IDSV 43).
- L'aura est dessinée au-dessus du sprite du héros (B5).
- Les tests d'E20.f1a restent verts, sauf ceux du bouchon de l'aura, remplacés ; en particulier
  `TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame` reste vert sans changement à 2 et 3 ticks par image
  (la gerbe du tick 91 garde ses emplacements, l'aura étant rendue à la position du tick 92) ; une mutation qui libère l'emplacement
  tout de suite fait rougir ce test ; build Release puis Debug ; `Alundra.Tests` sans échec ;
  traces inchangées ; verifier frais.
- Recette de l'auteur : avec `steel-m14`, tenir Carré : l'aura au-dessus du héros, renouvelée sur une charge longue.

##### Résultats (2026-10-10)

Commits : B1 `3e01c11` ; B2 `a523304` ; B3 et B4 `65fd5cb` ; B5 `4fc8e28` ; B6 (les tests) répartis dans ces commits, ce texte dans le commit de clôture. Build Release puis Debug : `Alundra.Tests`
**4694 sans échec** dans les deux configurations (4661 avant : +33 tests), `Alundra.dll` du projet identique à celle de Debug, traces `docs/hero-trace-389-*` et `docs/intro-*`
inchangées (garde `git diff --ignore-cr-at-eol` passée).

- **B1** : `oracle/keyprobe.py` copié (il tourne depuis le dépôt, `keyprobe.txt` écrit à côté) : 150 ticks de C2-steel, 0 écart entre le tri `0x800399B8` et la formule ;
  clé `0x00680000` pendant la volée `0x3F` (IDSV 4), `0x00690000` pendant la charge (IDSV 5).
- **B2** : `CreateNow`, `CreateAttachedNow`, `FreeAtTick(slot, génération)` du stade ; `FirstUpdateTick` posé par la source courante, respecté par `Tick(frozen, tick)` puis effacé par
  `RunFrame` (même sans tick) ; `AlundraEffectSlot.Generation` posé par la création du réservoir (`FreeIfGeneration`). Le membre par défaut `SortCoordinate` de l'ancre y est aussi (pour B5).
- **B3** : `AlundraEntityDepthKey.Compute(posY, idsv, posZ)` (et `Compute(proxy)`), `AlundraFrameSyncPasses.ResolveIdsv` partagée avec la passe du sprite (règle `~Target` comprise),
  `IAlundraEffectAnchor` implémentée explicitement par `AlundraEntityScriptProxy` (n'importe quelle entité ; `PosZ + 1`, `Status`, `DepthKey`, `SortCoordinate` du sprite). Rien de propre au héros.
  Le héros réel du monde (carte 14, pixel (100, 100)) expose, tick par tick de 1 à 90, la clé de `keyprobe` (`0x00680000` puis `0x00690000`).
- **B4** : l'aura dans `UpdatePlayerWeaponEffect` (`CreateAuraIfNeeded`, `ReleaseAura`), poignée = l'emplacement (`AuraSlot`, `AuraGeneration`) ; `AlundraAuraStub` retiré (son fichier a été
  supprimé par `6f9131e`, commit de la session principale qui avait ma suppression dans l'index partagé).
- **B5** : le test est rouge sans la règle (mutation « règle retirée » ci-dessous) : chaque quad de l'aura a une clé strictement plus grande que celle du sprite (vrai `DepthSortable2DComponent.BuildSortKey`
  sur la position du `RenderProjectionComponent` du héros), même passe, calque, ordre et élévation ; la règle des emplacements ancrés est dans `AlundraEffectQuadBuilder`
  (coordonnée du sprite de l'ancre + `DepthOffset`). B5 n'a pas eu besoin de passer en ⚠️.

**Acceptation, mesurée.**
- Contre l'oracle, pour C2-steel, C6, C10, C14, C16 (`AlundraHeroChargeTests`, 5 + 5 cas) : l'aura est créée aux ticks 33, 35, 46, 43, 43, emplacement 0, mode 1, ancre = héros,
  `OffsetZ` `0x100000`, `DepthOffset` 1, position de départ = celle du héros (Z du binaire), lus après la logique du tick et avant `RunFrame` ; les indices d'emplacement de l'étincelle et de la
  gerbe sont maintenant comparés à l'oracle et égaux (l'aura occupe l'emplacement 0), ainsi que sa présence (poignée non nulle) à chaque tick, et sa libération au tick 92 ou 102.
- Charge tenue 300 ticks à un tick par image (`AlundraHeroAuraTests`) : auras aux ticks **33, 130, 227**, emplacements **0, 5, 4** (A5 de l'oracle), une seule vivante après chaque mise à jour ;
  `Destroying` posé avant le tick 50 : créations **(33, 0), (50, 1)** (A1) ; avant les ticks 60 et 70 : **(33, 0), (60, 1), (70, 0)**, l'emplacement 0 réutilisé (A2) ; réservoir plein de avant le
  tick 30 à avant le 60 : l'aura est retentée à chaque tick (poignée nulle de 33 à 59) et créée au tick 60 dans l'emplacement 0 (P1) ; libéré avant le 45 : créée au tick 45.
- Frames de plusieurs ticks, dans le monde réel de la carte 14 (`AlundraHeroAuraWorldTests`) : 2 puis 3 ticks sur le passage du compteur de 10 à 12 donne exactement l'état d'images d'un tick
  (emplacements, positions, images, compteurs d'animation), une seule aura ; de même pour une image de 3 ticks qui commence aux ticks 31, 32 ou 33. L'écart (1) est épinglé : une image de
  2 ticks (129, 130) montre zéro aura, la suivante la recrée, et une seule image sans aura dans toute la suite.
- Poignée périmée (D-E20-30) : aura expirée pendant un verrouillage, emplacement repris par un autre effet : le premier tick normal ne libère pas cet autre effet ; rouge sans le garde.
- Renouvellement à 3 ticks par image (ADR-0059) : charge tenue en images de 1 tick jusqu'au tick 128, puis une image de 3 ticks (129, 130, 131) : l'ancienne aura est libérée par l'image,
  l'étincelle du tick 131 prend son emplacement (la poignée pointe un effet 16), et après l'image suivante (132 à 134) une aura vivante (effet 3, mode 1, ancre = héros) est la poignée,
  avec sa génération. **Le cas écrit dans le plan (aura créée au 227, renouvellement au 324, étincelle au 325) n'a pas été reproduit tel quel** : mesuré dans l'arène (aura troisième dans
  l'emplacement 5, étincelles dans les emplacements 1 à 4), l'étincelle née dans l'image prend l'emplacement qu'une étincelle plus ancienne libère à la même mise à jour, plus bas que celui de
  l'aura. C'est la première aura (création au 33, emplacement le plus bas libre) qui réalise le cas (renouvellement 130, étincelle 131) ; la phase des étincelles de l'arène coïncide avec celle de
  l'oracle (étincelles aux ticks congrus à 3 modulo 4).
- Clé de l'ancre du héros = sortie de `keyprobe` (voir B3) ; `Compute` contre sept valeurs faites à la main (dont `PosZ` entier négatif, IDSV 43, plus petit et plus grand short) et contre les 150 lignes de `keyprobe.txt`.
- L'aura est dessinée au-dessus du sprite du héros (B5).
- Les tests d'E20.f1a restent verts : `AlundraHeroChargeTests` n'a changé que là où le bouchon est remplacé (l'aura réelle), où l'emplacement 0 est pris par l'aura
  (`AFullReservoir_AtTheSparkTick…`, `Fill`) et par l'état `Normal` du héros du montage (une ancre `Destroyed` gèlerait l'effet) ;
  `TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame` est vert sans changement à 2 et 3 ticks par image.

**Mutations réelles** (appliquées au fichier, construites, puis restaurées ; aucune n'est dans un commit) :
- création par `Emit` au lieu de `CreateAttachedNow` : 29 tests rouges (dont A1, A2, P1, le renouvellement, les cinq sondes, les images de 2 et 3 ticks) ;
- sans la marque `FirstUpdateTick` : 7 rouges (`ASlotCreatedAtOnce_AtTheTickK…` ×2, `…AtTheLastTick…`, `TheMarkOf…IsCleared…`, les trois comparaisons d'images groupées) ;
- marque non effacée : 5 rouges (`…AtTheLastTick…DoesNotMissAnUpdate…`, `TheMarkOf…IsCleared…`, les trois comparaisons d'images groupées) ;
- libération immédiate de l'emplacement : 3 rouges (`FreeAtTick_ReleasesTheSlotAtThePositionOfItsTick…`, `TheReservoirAfterTheChargeAndTheRelease_DoesNotDependOnTheNumberOfTicksPerFrame` à 2 et à 3 ticks) ;
- libération sans le garde de génération : 2 rouges (`FreeAtTick_DoesNotFreeAnEffectThatTookTheSlot…`, `AStaleHandle_AfterALock…`) ;
- création sans le test de génération/activité de l'emplacement : 2 rouges (`AHandleWhoseSlotWasTaken_IsRecreated…`, `TheRenewalInFramesOfThreeTicks_WhereASparkTakesTheFreedSlot…`) ;
- règle des emplacements ancrés retirée : 1 rouge (`TheAuraQuads_AreSortedStrictlyAfterTheHeroSprite…`) ;
- `DepthOffset` de l'aura à 0 : 11 rouges (les cinq sondes ×2 et B5) ; clé décalée de `0x10000` : 9 rouges (`Compute` à la main, `keyprobe`, ...).

**Écarts et points signalés.**
(1) Le cas « renouvellement à 3 ticks par image » du plan s'exécute sur la première aura (voir ci-dessus), pas sur la troisième.
(2) Le montage de la charge (`AlundraHeroChargeTests.Start`) met le héros en `Normal` : sans cela l'aura se gèle (mode 2) à sa première mise à jour, ce que le héros du jeu ne fait pas.
(3) Dans les montages à un tick par image, un Carré relâché compte encore le tick de relâchement : le test de la poignée périmée attend donc un tick sans appui avant de libérer.
(4) Un hôte sans stade ne crée pas d'aura ; l'indice d'emplacement du héros passe avant les opérations en file de l'image (écart (3) de D-E20-28).
(5) ADR-0059 reste **Proposée** (l'auteur la confirme) ; aucune ADR nouvelle. La levée de clé par voisin ou porteur reste hors périmètre (écart écrit dans `AlundraEntityDepthKey`).
(6) Restent : le verifier frais, la recette de l'auteur (avec `steel-m14`, tenir Carré : l'aura au-dessus du héros, renouvelée sur une charge longue).

**Vérification (2026-10-10) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre). Rejeu : `Alundra.Tests` 4694/4694 en Release et en Debug,
`cmp` de la DLL, traces inchangées, chaque point de l'acceptation relu contre le code et les tests. Adversarial : suite Debug 4694/4694, `keyprobe.py` relancé
(sortie identique au bit près, 150 lignes OK), `FreeAtTick`, marque `FirstUpdateTick` (effacée même sans tick, y compris au départ de warp), ordre de dessin,
clé générique pour toute entité (rien de propre au héros), couverture du bouchon retiré : aucun contre-exemple. Remarques P4, dispositions :
- l'écart (3) de D-E20-28 existe **aussi à un tick par image** : un effet mis en file par un événement de carte du même tick (rang 0) est appliqué par
  `RunFrame` après l'aura créée tout de suite par `MovePlayer` ; seuls les indices d'emplacement sont permutés (avec un seul emplacement libre, c'est
  l'effet de la carte qui échoue au lieu de l'aura) : **écart consigné ici**, D-E20-28 se lit « exact à un tick par image, sauf l'ordre des
  emplacements face aux effets en file du même tick » ;
- un Z du héros négatif d'au moins 2 px sur une ligne frontière mettrait l'aura un seau d'élévation sous le sprite (la clé porte le Z signé, le seau du
  sprite non) : **limite consignée**, cas improbable (hauteurs de terrain positives ou nulles) ;
- commentaires périmés (`AlundraEffectReservoir.cs` « No entity of the DLL implements it yet », `AlundraPlayerManager.cs` « The aura stub is left as it
  is », ticks 323-325 dans `AlundraHeroAuraWorldTests.cs`) : **reportés à E14.a1b**, qui travaille dans ces fichiers ;
- l'exemple littéral du cas « renouvellement à 3 ticks par image » (227/324/325) n'est pas celui du test (33/130/131, écart (1) ci-dessus) : **gardé tel
  quel**, l'écart est écrit.
**Retour arrière** : la suppression d'`AlundraAuraStub.cs` est dans le commit de plan `6f9131e` de la session principale (index partagé pendant
l'exécution) ; un retour arrière d'E20.f1b doit aussi restaurer ce fichier.

##### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que le binaire n'explique pas ; un besoin de changer le moteur ; une clé du héros qui demanderait la
  levée par voisin pour égaler l'oracle ; B5 seule : si la coordonnée du sprite du héros ne peut pas être égalée dans la DLL, B5 passe ⚠️
  sans bloquer le reste.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E20.f1b.

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

### 1.13 E20.d — La course : E20.d1 🧪 (faite et CONFIRMED le 2026-10-10, `af42753` à `0b1d3f3` ; reste la recette de l'auteur), E20.d2 ⏳ (E20.d2a faite, 🧪 recette de l'auteur ; E20.d2b attend la mesure du stick, Q-D1)

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
  - E20.i a remplacé ce garde par la branche `0x8003260C` (2026-10-10) ;
  - le mur d'eau profonde sans bottes (O-E19-22) reste hors d'E20.d et d'E20.i (Q-N3).
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
- **Garde de l'eau profonde (remplacé par la nage d'E20.i, 2026-10-10)** : au tick où les quatre coins sont sur la pente 4 (le 89 de `R9-deepwater-boots2`), le tick suivant
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

#### E20.d2 — Découpage (D-E20-32, 2026-10-10)

- **E20.d2a — la sonde** (S1 et S3, plus le test de `ComputePadState` de S4) : l'échantillon du stick voyage dans le pad, et une sonde
  le journalise pour que l'auteur mesure sa manette. Aucun comportement ne change : la course garde le OU.
- **E20.d2b — la règle** (S2, le reste de S4, S5) : après la mesure de l'auteur.

**E20.d2a, précisions.**
- **Prérequis** : E20.d1 et E20.i CONFIRMED (le pad du tick et la queue de la course sont dans l'état qu'elles ont laissé).
- **S1** comme ci-dessous, avec cette dérivation (révision 1) : x = `Value` de `MoveRightStick` (= axe X du stick gauche ; l'action
  `MoveLeftStick` porte le même axe inversé), y = `Value` de `MoveUpStick` (= axe Y, haut positif ; `MoveDownStick` porte le même axe
  inversé) (`CasaEngineMonogame/CasaEngine/Framework/Input/InputMapping.cs:36-58`, `PlayerSetupWriter.cs:129-132`) ; jamais une
  différence des deux moitiés, qui doublerait les valeurs. Le journal écrit x et y avec le point décimal (culture invariante), x
  positif à droite, y positif en haut.
- **S3, la sonde** : une variable d'environnement `ALUNDRA_DEBUG_STICK_PROBE` (même idiome que `ALUNDRA_DEBUG_IGNORE_CONTROL_LOCK`,
  `AlundraPlayerManager.cs`, avec un forçage pour les tests) ; quand elle vaut `1`, chaque image journalise une ligne `x y D S` (x et y
  signés du stick, D et S en hexadécimal), et chaque lecture de la table de départ de la course (départ dans `0x29`, reprise dans
  `0x2A` à 11 et plus) une ligne de plus avec le quartet que la DLL utilise aujourd'hui (le OU). Le journal va là où la DLL écrit déjà
  ses traces de débogage (l'exécuteur le nomme dans `docs/test-saves.md` ou une note dédiée, avec la marche à suivre pour l'auteur).
  Pas de `Program.cs`, pas de changement du moteur.
- **Acceptation d'E20.d2a** : `ComputePadState` avec des valeurs analogiques (x, y, D, S, et le OU inchangé dans `ButtonsHold`), le
  faux `getButtonState` du test suivant la sémantique du moteur (`MoveLeftStick.Value = −MoveRightStick.Value`,
  `MoveDownStick.Value = −MoveUpStick.Value`, appui seulement à partir de 0,5), avec au moins : un cas à 45° (≈ 0,707, 0,707 : |x| ≈ |y|),
  un cas à mi-course sous la zone morte de 0,5 (x et y journalisés, S vide), un cas dans le quadrant négatif, et une poussée à fond à
  droite qui donne x = 1 (pas 2) ; le pad
  de tick porte l'échantillon de l'image, dans le bloc des entités et dans le mode autonome ; sans la variable, rien n'est journalisé ;
  avec, le format est celui écrit ; E20.d1 et E20.i inchangées (G1 rejoué au clavier) ; Build Release puis Debug ; `Alundra.Tests` sans
  échec ; traces inchangées ; verifier frais. **Recette de l'auteur** : lancer le jeu avec `ALUNDRA_DEBUG_STICK_PROBE=1`, tenir le stick
  dans les huit directions (à fond, puis à mi-course) et dans les quatre diagonales à 45° exacts, puis envoyer le journal : `log.txt`
  dans le dossier de travail du jeu, réécrit à chaque lancement (le copier avant de relancer).
- **Révision 1 (2026-10-10)** : relecture REVISE, un point corrigé : la dérivation de x et y depuis les actions du moteur, et la
  sémantique du faux `getButtonState` du test, sont écrites.
- Relecture neuve : **READY** (2026-10-10). Exécution après E14.a0 et E14.a0b (même dépôt, builds en série).

#### E20.d2 — Tâches

- ✅ **S1 — L'échantillon du stick** (E20.d2a, fait le 2026-10-10, `2c16358`).
  - `AlundraPadState` gagne : les bits de direction du clavier et de la croix (D), les bits du stick (S), et x, y signés
    (`ButtonState.Value` des actions `Move*Stick`). Valeurs par défaut nulles : les initialiseurs existants ne changent pas de sens.
  - `ComputePadState` garde le OU dans `ButtonsHold`.
  - `AlundraTickPad` ne change pas. L'échantillon de l'image est recopié dans chaque pad de tick : au `FromTickPad` du bloc des
    entités, et dans le mode autonome.
- ⏳ **S2 — La règle**, aux deux lectures seulement : le départ dans `0x29`, la reprise dans `0x2A` à 11 et plus.
  - Elle ne joue que si D est vide et S est une diagonale.
  - \|x\| − \|y\| > ε : bit horizontal ; \|y\| − \|x\| > ε : bit vertical ; sinon l'axe du regard du tick. ε = 1/16 (D8).
  - Tout le reste garde le OU : la marche, le cône, la fenêtre courte, les sorties.
- ✅ **S3 — La sonde** (E20.d2a, faite le 2026-10-10, `2c16358` ; marche de l'auteur dans `docs/stick-probe.md`). Une variable d'environnement, comme `DebugIgnoreControlLock`, journalise x, y, D, S et le nibble choisi à
  chaque lecture. Pas de `Program.cs`.
- ⏳ **S4 — Tests** (E20.d2a : le test de `ComputePadState` analogique est fait, `2c16358` ; le reste est E20.d2b).
  - `ComputePadState` avec des valeurs analogiques.
  - Balayage de 0 à 359° aux rayons 1, 0,75 et 0,6, contre la règle écrite.
  - Les 48 départs et les 15 reprises de G10 : le nibble choisi par la règle égale celui de l'oracle.
  - G1 rejoué au clavier, inchangé.
- ⏳ **S5 — Mesure de l'auteur.** ε réglé ou gardé ; D-E20-15 complétée.

**Acceptation d'E20.d2.** S4 vert ; E20.d1 inchangée ; verifier frais. Recette au stick : une diagonale franche part sur son axe
dominant, une diagonale à 45° sur l'axe du regard, et la marche ne change pas.

#### E20.d2a — Résultats (2026-10-10, branche `chantier/e20-heros`) — 🧪 faite et CONFIRMED, mesure de l'auteur en attente

Commits : S1 et S3 avec leurs tests `2c16358` ; ce plan et `docs/stick-probe.md` (T finale). `Alundra.Tests` : 4646 avant, 4661 après (+15),
sans échec en Release et en Debug ; `Alundra.dll` du projet égale celle de `Alundra/bin/Debug` (`cmp`). Traces du héros et de l'intro
inchangées (aucune différence hors fins de ligne). Aucune modification de la marche : `ButtonsHold` garde le OU, G1 à G10 et E20.i
(rejoués au clavier par la suite) sont verts sans toucher un test existant.

- **Livré.** `AlundraPadState` : `DigitalDirectionBitsHold` (D), `StickBitsHold` (S), `StickX` (`Value` de `MoveRightStick`), `StickY`
  (`Value` de `MoveUpStick`), nuls par défaut. `FromTickPad(tickPad, in frameSample)` recopie l'échantillon de l'image (`LastPadState`)
  dans le pad de chaque tick, au bloc des entités (`AlundraWorldProxy`) et en mode autonome (`AlundraEntityScriptProxy`) ;
  `AlundraTickPad` ne change pas. Sonde : `AlundraStickProbe` (variable `ALUNDRA_DEBUG_STICK_PROBE`, lue une fois, forçage et puits par
  thread pour les tests) ; journal par `Logs.WriteInfo`, donc `log.txt` du dossier de travail du processus.
- **Format.** `StickProbe x y D S` par image (4 décimales, culture invariante, D et S en hexadécimal) ; `StickProbe start 29 N` au départ
  dans `0x29`, `StickProbe start 2A N` à la reprise dans `0x2A` à 11 et plus (N = quartet du OU, en hexadécimal). Le préfixe `StickProbe`
  permet de filtrer `log.txt` (`rg StickProbe log.txt`).
- **Acceptation, mesurée.** `ComputePadState` analogique avec le faux `getButtonState` de la sémantique du moteur : 45° (0,7071 ; 0,7071),
  mi-course sous la zone morte (0,4 ; 0,3 : x et y gardés, S vide), quadrant négatif (−0,6 ; −0,8), plein droite (x = 1, pas 2), plein
  bas-gauche, clavier seul (D, S vide), clavier et stick ensemble (OU gardé) ; le pad de tick porte l'échantillon (mode autonome, deux
  ticks par image ; bloc des entités d'un monde piloté, `AlundraTickStructureTests`) ; sans la variable rien n'est écrit, même sur une
  course de 30 images ; avec elle, le format écrit (culture fr-FR forcée), une ligne par image (30 pour 30), la première ligne `start 29`
  à la 26e image (fin de la préparation), `start 2A` dans la reprise de `g4/R4-rel12-R`.
- **Mutations réelles** (copie sauvegardée, restaurée, jamais commitée) : x pris comme `MoveRightStick − MoveLeftStick` : cinq tests
  `ComputePadState` rouges (plein droite, plein bas-gauche, mi-course, 45°, quadrant négatif) ; pad de tick sans l'échantillon de l'image
  (les deux sites) : `TheSelfDrivenMode_HandsTheFramesSampleToMovePlayer_AtEachTick` et
  `TheEntityBlock_HandsTheFramesStickSampleToMovePlayer_AtEachTick` rouges.
- **Marche de l'auteur** : `docs/stick-probe.md`. La recette de l'auteur (mesure du stick, Q-D1) reste à faire ; elle ouvre E20.d2b.
- **Non fait, par le plan** : S2 (la règle), le reste de S4, S5 : E20.d2b. Le verifier frais d'E20.d2a reste à lancer par la session
  principale.
- **Vérification (2026-10-10) : CONFIRMED** par deux verifiers frais (workflow). Rejeu : `Alundra.Tests` 4661/4661 en Release et en Debug,
  `cmp`, traces ; dérivation de x et y relue contre `InputMapping` et le mapping exporté (`MoveDownStick`/`MoveLeftStick` inversés, zone morte
  0,5). Adversarial : aucun axe doublé ni inversé, échantillon présent dans chaque pad de tick, sonde sans allocation sans la variable, décisions
  de départ et de reprise inchangées, journal bien dans `log.txt`. Remarques P4 : le `<summary>` d'`ActionBits` se retrouve sur `DirectionMask`
  (**reporté** à E20.d2b, qui retouche ce fichier) ; les lignes réelles de `log.txt` portent l'heure et `[Info]` avant `StickProbe`, et le filtre
  `rg` garde aussi la ligne d'avertissement du démarrage : **corrigé dans `docs/stick-probe.md`** (doc seule).

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
- **Q-D2** (non bloquante, le défaut s'applique ; depuis E20.i le héros nage au lieu de marcher, le mur reste à décider, Q-N3) : sur l'eau profonde, le garde fait marcher le héros à la place de la course.
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

### 1.14 E20.i — La nage 🧪 (faite et CONFIRMED le 2026-10-10, `7278069` à `ab672e1` ; reste la recette de l'auteur avec `swim-m14`)

Découverte en lecture seule du 2026-10-10 (`docs/plan-e20-annexe/e20-i/` : `swim-binary.md`, `swim-dll.md`, `swim-oracle.md`,
contre-vérifiées par `check.md` : tables et gestionnaires redécodés, W1 à W10 de l'oracle rejoués à l'identique). Cette section
reprend le projet de `check.md` ; le point des courants y devient O-E20-16 (O-E20-15 est la poignée de l'aura).
Relecture : **READY** dès la première lecture (2026-10-10).

**But.** Le héros nage comme dans le binaire sur la pente 4 : il flotte (`0x1D`), nage (`0x0F`), plonge au front de Triangle, Croix
ou Carré (`0x28`, un élan de 42 ticks en surface, touchable), et sort de l'eau par la terre ; les gestionnaires de `0x3A` et `0x3B`
sont prêts pour la blessure (E20.c2b). Le garde d'E20.d1 disparaît.

**Découpage.** Une tranche. Le mur d'eau profonde sans bottes (O-E19-22) et les courants (O-E20-16) restent dehors (Q-N3, Q-I1) ; s'ils
reviennent à E20, ils forment E20.i2, dont l'oracle est déjà mesuré (traces `*-L0`, `*-L1`, `R9-deepwater-noboots`).

**Prérequis.** E20.a, E20.c2a (épilogue, bottes), E20.d1 (garde à remplacer, montage de la course), E20.f1a (charge). Pas E20.c2b :
l'ordre s'inverse (E20.c2b, branche de l'eau, dépend d'E20.i) ; l'écart de D-E20-23 (« blessure brève ») n'existera jamais.

**Faits porteurs.**
- [binaire] `MovePlayer` lit la pente au tout début (`0x80031B98`) ; pente 4 → `0x8003260C` après le pilote `0x8003634C` et la
  lecture du pad, à la place de la porte d'échelle et du trio `0x80031D68`-`0x80031D78`. La branche met la charge (`0x80126FFC`) et le
  pointeur d'arme (`0x80127110`) à 0, appelle `UpdatePlayerCarriedEntity(2)`, puis aiguille par `0x80022E7C` (80 entrées) et finit par
  `0x80032914`.
- [binaire] Six groupes : `0x80032674` (tous les états non cités : `0x1D`, puis `TryUseItem`, sans écrire la direction) ;
  `0x80032658` (`0x1C`, `0x31`, `0x3E` : direction + 0x10, puis `0x1D`, puis `TryUseItem`) ; `0x80032680` (`0x28`, `0x3A` :
  `TryUseItem` seul) ; `0x80032690` (`0x0F`, `0x1D`) ; `0x800326FC` (`0x3B`) ; `0x80032758` (`0x0D 1A 1B 1E 27 37 38 3D 43 45 47 48
  4A 4C 4D` et ≥ `0x50` : annulation `0x800350C0`, état inchangé).
- [binaire] `0x80032690` : direction = celle du pad (écrite avant `TryUseItem`, même sans direction) ; `TryUseItem` à 0 finit le tick ;
  `CheckEntityInteraction` 2 → `0x1D`, 1 → fin ; front `& 0xD0` (Triangle 0x10, Croix 0x40, Carré 0x80) → `0x28` ; sinon quartet de
  direction non nul (même contradictoire) → `0x0F`, sinon `0x1D`.
- [binaire] `0x800326FC` : `TryUseItem` à 0 → direction + 0x10 ; sinon, `+0xAC == 1` → `0x1D` et direction + 0x10.
- [binaire] Côté terre (`0x80022D3C`) : `0x0F`, `0x1D`, `0x28` → `0x80031DD0` (Idle au sol, `0x2D` en l'air, puis `TryUseItem`) ;
  `0x3A`, `0x3B` → `0x80031DB4` (direction + 0x10 d'abord).
- [données, oracle] Fiches : `0x1D` 0/3, boucle 36 ; `0x0F` 144/4, son 22, 42 enchaîné sur lui-même ; `0x28` 176/0, son 21, 42 →
  `0x1D` ; `0x3A` 256/0x40, son 23, 4 → `0x3B` ; `0x3B` 0/0x44, 8, tenu. La physique générique de la DLL suffit (déduit, à prouver).
- [DLL] Le son d'une animation rejoue sur un enchaînement sur elle-même (`TakeZImpulse` → `GiveImpulse`) ; `ForceResetAnimationFlag`
  est `+0xAC` ; les bits du pad égalent ceux du binaire (`AlundraPlayerController.cs:27-30`).

**Défauts proposés (écarts écrits dans le code et ici).**
- Sans bottes de niveau ≥ 2, le héros nage (l'original : mur, O-E19-22, Q-N3).
- Pas d'éclaboussure (effet 6) à l'entrée et à la sortie, pas de sillage (effet 0x15), pas de ride de la plongée, pas d'atterrissage
  dans l'eau (son 21, effet 6) : passe `0x80038E84` et `CreatePlayerAnimationEffects`, E20.e (aucun oracle ne les mesure ; `Slope_190`
  non tenu par la DLL).
- Courants (`+0x188`, branche `0x80032768`, dérive) non portés (O-E20-16).
- `0x800350C0` et `UpdatePlayerCarriedEntity(2)` : bouchons nommés à leur place (E20.h0, E20.g).
- `0x3A` et `0x3B` ne sont atteints que par les tests jusqu'à E20.c2b.

**Hors périmètre.** L'entrée de la blessure et le clignotement (E20.c2b), la mort dans l'eau (`0x3C`, E18), le mur et les courants,
les effets d'eau (E20.e), les objets du Rond dans l'eau et le son 3 (E20.h), l'aura (E20.f1b), le rognage au bord de carte (O-E19-17).

#### Tâches

- ✅ **T0 — Oracle dans l'annexe (commit docs).**
  - Copier, depuis `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20i/swim-oracle/tree/docs/plan-e20-annexe/e20-d/oracle/`,
    `base/sscen.py`, `base/sverify.py`, `base/sindex.py` (fichiers neufs, à côté de `rworld.py`) et `out-swim/` (786 fichiers) dans
    `docs/plan-e20-annexe/e20-d/oracle/` ; aucun fichier existant modifié, sauf une section « Nage » du README.
  - Relancer `sverify.py` (il a tourné sur une version antérieure de `sscen.py`).
  - Preuves : les dix groupes relancés depuis l'annexe redonnent `out-swim/manifest-swim.txt` (785 lignes) ; G1 à G10 relancés
    après l'import de `sscen` redonnent `out/manifest.txt` (557). La porte D1 ne se rejoue pas en place (P4 hérité d'E20.d1 T0 :
    `compare.py` vise un niveau trop haut, `d1/out` absent) : la rejouer depuis une copie, sans modifier d'outil copié.
- ✅ **T1 — Table et noms.** Constantes des cinq états ; la table `0x80022E7C` en six genres, avec un test entrée par entrée contre
  l'exe ; masque de plongée = `0xD0` ; bouchon nommé `0x800350C0`.
- ✅ **T2 — La branche et les sorties.**
  - Dans `MovePlayer`, après le pilote et la direction, avant la porte d'échelle : pente 4 → charge 0, pointeur 0,
    `UpdatePlayerCarriedEntity(2)`, les six gestionnaires dans l'ordre du binaire, `EndOfMovePlayer`, retour.
  - Côté terre : `0x0F`, `0x1D`, `0x28` → Idle ou `0x2D` puis `TryUseItem` ; `0x3A`, `0x3B` → direction + 0x10 d'abord.
  - Retirer le garde d'E20.d1, `IsRunState` et la clause de la pente 4 de la queue ; documentation de la classe.
- ✅ **T3 — Moyens de test.** Le montage gagne l'état de départ (`hero_anim`), les inventaires `b1b` et `holy_b1b`, les cinq états
  (durées, fins, fiches) avec l'Idle à l'accélération 1 ; le lecteur charge `out-swim/` ; un point d'accès qui pose `0x3A` et la
  direction avant le `MovePlayer` d'un tick donné (la sortie du bloc de blessure, sans l'entrée).
- ✅ **T4 — Tests et arcs.** Acceptation ci-dessous ; un arc au vrai préfab sur l'étang de la 14 (bottes 0x1B) : tomber de la berge,
  `0x1D` puis `0x0F`, plonger, buter contre la berge ; le même sans bottes, épinglé comme écart (il nage).
- ✅ **T5 — Sauvegarde de test** `swim-m14` : carte 14, bottes 0x1B, tuile proposée (40, 48, 1) sur la berge (à vérifier : marchable,
  hors zone d'événement).
- ✅ **T6 — Plan.** Résultats, §0.2, lignes O-E19-22 et O-E20-16, D-E20-23 caduque, arête de dépendance d'E20.c2b, recette d'E20.d
  (« sur l'eau profonde, le héros nage »).

#### Acceptation

Champ par champ contre l'oracle, à 1 tick par image (`anim_target`, `anim_cur`, `dir_target`, forces, positions relatives,
`on_ground`, `b0`, `charge_cnt`, sons de changement d'animation). Les effets 6 et 0x15, les sons 21 et 6 d'atterrissage et le son 3 du
Rond ne sont pas comparés (E20.e, E20.h).
- `R9-deepwater-boots2` en entier : pente 4 au 89 (`0x03`, 258048) ; `0x1D` au 90 (225792, son 0) ; `0x0F` au 91 (218592, son 22),
  −7200 par tick jusqu'à 110592 au 106.
- `R9-allwater-start-R` et `-T-R` : `0x1D` au 1 ; `0x0F` au 2 (6912, son 22), + 6912 par tick, 110592 au 17 ; son 22 et `b0 = 1` au
  44 ; aucune préparation de course. `R9-allwater-start-T` : `0x1D` de 1 à 40, forces nulles, `b0 = 1` au 37.
- W1 (bottes 2 et 3) : marche, pente 4 au 20, `0x1D` au 21 (139776), `0x0F` au 22 (137952, son 22) ; saut `s03`, `0x2C` jusqu'au 23,
  `0x1D` au 24 (131712), `0x0F` au 25 (130392) ; chute de 32 px, `0x2C` 21-31, `0x1D` au 32, `0x0F` au 33.
- W2 : les huit directions (cardinales 110592 ou 73728, diagonales 78192 et 52128 ; rampe 6912, 4608, 4887, 3258) ; demi-tour au 41
  (96768) ; son 22 aux 5, 47, 89, 131 et 173 (`W2-long-R`).
- W3 : `W3-T-swim`, `0x0F` 5-19, `0x28` 20-61 (135168, son 21 au 20), `0x1D` au 62 (118272, `b0 = 1`), `0x0F` au 63 (117792) ;
  Croix et Carré identiques au Triangle ; Rond sans plongée ; maintien sans seconde plongée (`W3-T-held`, `0x1D` au 52) ; appuis
  toutes les 5 ticks, plongées 10-51 et 55-96 ; diagonale (95568, −63712) ; sans direction ou Gauche+Droite, direction gardée ;
  entrée (`W3-enter-T+0`, `0x29` au 12, Idle 13, Moving 14, `0x1D` 15, `0x0F` 16 ; `+1`, appui perdu ; `+2`, `0x28` 14-55).
- W4 : sortie à gauche, pente 0 au 103, Idle au 104 (−55296), Moving au 105 (−107520) et 106 (−159744) ; Triangle tenu, Idle 122,
  `0x29` 123-147, `0x03` au 148 ; plongée coupée, `0x28` 5-31, Idle au 32 (−67584) ; berge de 16 px, x 346,00, `ForceAdjusted` dès
  le 104, `0x0F` gardé.
- W5 : mur, `ForceAdjusted` dès le 78, x 709,00, `0x0F` à 110592 (mur est : tolérance d'O-E19-42, ± 1 unité, ± 1 tick) ; glissement
  en diagonale ; plongée contre le mur jusqu'à sa fin ; bords de carte : écart O-E19-17 écrit si la DLL diffère.
- W6 : charge 60 au 83, 0 dès le 84, Carré tenu ; jamais comptée dans l'eau ; recomptée à terre (3 au 90, 60 au 150).
- W7, la blessure posée par le point d'accès au tick de l'oracle : `W7-hurt-idle-L2`, `0x3A` 2-5 (−196608, son 23), `0x3B` 6-14
  (−184320 puis −12288 par tick), `0x1D` au 15 (−75264, direction 08 → 18), x 229,00, 217,19, 200,29 aux 2, 6, 15 ;
  `W7-hurt-leaving-x352`, Idle au 5 (−98304, direction 18) ; `W7-hurt-dive-s45`, `0x3A` au 81.
- W8 : les 240 traces (80 états × repos, Droite, front de Triangle), ticks 1 à 3. W9 côté terre : Idle au tick 1, direction
  retournée pour `0x3A` et `0x3B`. W10 : quartets contradictoires, canal, eau peu profonde vers profonde.
- 2 ticks par image : un front de plongée ne plonge qu'une fois.
- Tests qui changent, et eux seuls : `DeepWater_EveryStateOfTheRun_…` (six cas → `0x1D` ; direction retournée pour `0x3E` et `0x1C`
  seulement) ; `DeepWater_TheTailDoesNotStartTheRun_…` (Idle + Triangle + Droite sur la pente 4 → `0x1D` ; sec et peu profond
  inchangés) ; `G9_OnDeepWater_…` et `G9_ATriangleOnDeepWater_…` (traces entières) ; `AlundraTestSavesTests` (le préréglage).
  Traces du héros et de l'intro inchangées ; sonde du vrai jeu inchangée ; Release puis Debug sans échec ; verifier frais.
- Recette de l'auteur, avec `swim-m14` : tomber dans l'étang de la 14, nager dans les huit directions, flotter, plonger au
  Triangle, à la Croix et au Carré (pas au Rond), buter contre la berge ; sortir par une rive de même niveau sur la 416 si on y
  accède (pas par la cascade). Écarts attendus : ni éclaboussure ni sillage ; sans bottes, le héros nage.

#### Résultats (2026-10-10)

Commits : T0 `7278069`, T1 `5e863ea`, T3 `fd9f5e5`, T2 `bdc5a84`, T4 `8ac6925`, T5 `34523bd` (T2 vient après T3 : les deux tests de l'oracle de la course qu'il
change comparent des traces entières). Suite complète : Release puis Debug, 4605 tests, 0 échec ; `Alundra.dll` de `alundra-project/` identique à celui de Debug ;
traces du héros (389) et de l'intro, sonde du vrai jeu : inchangées. Mutations attrapées (chacune par au moins 2 tests) : masque `0xD0` privé du Carré,
direction écrite après `TryUseItem`, sortie de terre à `0x2D` au sol, retournement oublié (`0x3B` côté terre, `0x3B` côté eau), charge non remise à 0,
trio non sauté, groupe `0x80032758` réécrit en `0x1D`, une entrée de la table retirée.
- **T0** : les dix groupes rejoués depuis une copie avec `--cross` redonnent `manifest-swim.txt` (785 lignes) octet pour octet ; `sverify.py` : porte D1 PASS
  (S-A `1ae0cc02`, S-B `975c342a`, 216 valeurs chacune), 549 fichiers de `out/` régénérés identiques. La porte se rejoue dans la copie avec le document des valeurs
  placé en `docs/plan-e20-annexe/` et les traces D1 de référence en `oracle/d1/out/` (aucun outil copié modifié ; README « Swim »).
- **T1** : la table `0x80022E7C` du code égale celle de l'exécutable entrée par entrée (80 mots), six genres ; masque `0xD0`.
- **T2** : la branche, les six gestionnaires, les sorties vers la terre ; le garde d'E20.d1, `IsRunState` et la clause de la queue sont retirés.
- **T3** : montage : état de départ de la trace, inventaires `b1c` et `holy_b1b`, les cinq états dans l'horloge de sprites, filtres de sons et du compteur de fins.
  Un héros qui commence dans l'eau profonde est ramené à l'état de départ de la trace après l'image de mise en place (le binaire n'exécute pas `MovePlayer` avant le tick 1).
- **T4** : 109 tests de traces (R9, W1 à W7, W9, W10), 295 de branche (les 240 traces de W8 par état, les six gestionnaires, les sorties), 2 arcs au vrai préfab
  (étang de la 14, tuile (40, 48, 1) : marcher, tomber de 16 px, `0x2C` aux ticks 11 à 18, pente 4 au 18, `0x1D` au 19 (87808), `0x0F` au 20 (86928), `-880` par tick
  jusqu'à 73728 au 35 ; élan de 90112 pendant 42 ticks ; butée contre la berge sud ; sans bottes, identique).
- **T5** : `swim-m14`, **tuile (40, 48, 1) retenue sans changement** : hauteur 1, marchable, `ground_property 0x10`, aucune case de portail sur la carte 14, l'arc y a marché
  sans événement.
- Valeurs de l'acceptation toutes retrouvées : R9 (pente 4 au 89, `0x1D` 225792 au 90, `0x0F` 218592 au 91, `-7200` jusqu'à 110592 au 106 ; départ dans l'eau `0x1D` au 1,
  `0x0F` au 2 à 6912, +6912, 110592 au 17, son 22 et `b0 = 1` au 44 ; `start-T` `0x1D` de 1 à 40, `b0 = 1` au 37), W1 (marche : pente 4 au 20, 139776 au 21, 137952 au 22 ;
  saut `s03` : `0x1D` au 24 (131712), `0x0F` au 25 (130392) ; chute de 32 px : `0x2C` 21-31, `0x1D` 32, `0x0F` 33), W2 (huit directions et rampes, demi-tour au 41 à 96768, son 22
  aux 5, 47, 89, 131, 173), W3 (`0x28` 20-61 à 135168, son 21 au 20, `0x1D` au 62 à 118272 avec `b0 = 1`, `0x0F` au 63 à 117792 ; Croix et Carré identiques, Rond sans plongée, maintien
  sans seconde plongée, appuis toutes les 5 ticks : plongées 10-51 et 55-96, entrées `+0`, `+1`, `+2`), W4 (pente 0 au 103, Idle 104 à -55296, -107520, -159744 ; Triangle tenu :
  Idle 122, `0x29` 123-147, `0x03` au 148 ; élan coupé : Idle au 32 à -67584 ; berges de 16 et 32 px : x 346,00), W5 (mur est : **égal sans tolérance**, `ForceAdjusted` dès le 78, x 709,00),
  W6 (charge 60 au 83, 0 dès le 84, 3 au 90 et 60 au 150 à terre), W7 (`0x3A` 2-5, `0x3B` 6-14, `0x1D` au 15 à -75264, x 229,00 / 217,19 / 200,29 ; sortie coupée : Idle au 5 à -98304 ;
  `0x3A` au 81 pendant l'élan), W8 (240 traces, ticks 1 à 3), W9 (Idle au tick 1, direction retournée pour `0x3A` et `0x3B`), W10 ; 2 ticks par image : une plongée une seule fois.
- **Écarts à l'oracle utilisés (chacun écrit dans le code des tests)** :
  - positions X et Y à 16 unités près (la tolérance de la glace d'E20.d1) : le mouvement de la DLL calcule en flottants (4 unités de mantisse à x 600 px) ; sur les diagonales
    l'écart reste de 9 unités au plus (W2 diagonales, W3 plongées en diagonale, W4 `leave-UL`) ;
  - `on_ground` lu un tick plus tard au tick de la perte, pour les trois chutes de W1 (O-E20-12, déjà admis) ;
  - mur en diagonale (`W5-wall-UR`) : au contact de x 709,17 → 709,00 le binaire réduit aussi le pas de Y, la DLL ne ramène que X : Y en avance de 7424 unités (0,113 px),
    constant ensuite (la famille d'O-E19-42, à confirmer) ;
  - bords de la carte (`W5-edge-E`, `W5-edge-N`) : égaux jusqu'au rognage (ticks 63 et 58), puis la DLL franchit le bord (x 1237,00 et y 7,00 du binaire) : O-E19-17, épinglé ;
  - sons 21 et 6 d'un atterrissage dans l'eau (E20.e) et son 3 du Rond (E20.h) non comparés ; effets 6 et 0x15 non comparés ; effet 9 des traces W7 (le Beannoid que la DLL n'a pas) non comparé ;
  - le compteur de fins `b0` n'est comparé que sur les traces entièrement dans l'eau (les boucles d'Idle et de Moving du montage sont des substituts de 50 ticks) ;
  - le point d'accès de la blessure (W7) pose l'état `0x3A` et la direction que l'oracle tient au tick de l'entrée, avant `MovePlayer` : la sortie du bloc de blessure, sans l'entrée (E20.c2b) ;
  - non couverts : `CheckEntityInteraction` rendant 1 ou 2 dans l'eau (décodé seulement, aucune entité touchable dans l'oracle), les objets du Rond dans l'eau (E20.h),
    `W6-poke-state*` (injection d'un pointeur d'aura : E20.f1b), `W7-hurt-keep` (la trace réinitialise la position à chaque tick), `W7-hurt-shore` (entrée sur la pente 0 : E20.c2b).
- **Vérification** : verifier frais **CONFIRMED** (aucun P0 à P2) : répartition par pente, branche `0x8003260C`, les 80 mots de la table de
  nage, les six gestionnaires et les sorties vers la terre redécodés ; manifeste de la nage rejoué à l'identique ; quatre mutations
  attrapées ; chaque commit de code compile ; sonde du vrai jeu inchangée.
- **Causes établies par le verifier** : la tolérance de 16 unités couvre la quantification des flottants de la DLL (au plus 9 unités, en X,
  seulement sur les traces en diagonale ou en rampe ; 98 traces sur 108 sont exactes ; `MinMoveDistance` n'y joue aucun rôle, contrairement
  à la glace d'E20.d1) : le commentaire qui la rattache à O-E19-42 et à la glace est à corriger (P4) et la tolérance peut être resserrée
  à 9 sur ces traces ; l'avance de 7424 unités en Y au mur en diagonale est bien l'écart O-E19-42 / D-E19-58 déjà accepté (division
  conjointe en 16.16 de `0x80037730` dans le binaire, glissement par axe dans la DLL ; `SwimTick` ne touche pas la position) ; la
  tolérance pourrait ne valoir qu'à partir du tick du contact (P4).
- **Reportés (P4)** : le test des bords de carte rejoue le pad depuis le tick 1 à partir de l'état du tick 63 (il ne modélise pas la suite
  de la trace) ; `b0` n'est comparé que sur les traces entièrement dans l'eau (limite du montage) ; la suite complète n'a tourné que sur
  l'état final.
- **À confirmer par l'auteur** : la recette décrite ci-dessus avec `swim-m14` ; Q-N3 et Q-I1 inchangées.

#### Arrêts, budget, retour arrière

- **Arrêts** : un écart à l'oracle que ni O-E19-42, ni O-E19-17, ni E20.e, E20.h ou E20.f1b n'expliquent ; un test qui change hors de
  la liste ; une trace du héros ou de l'intro, ou la sonde, qui bouge ; un besoin de toucher au moteur ou de porter le mur, les
  courants, l'entrée de la blessure ou `0x80038E84` ; tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus. Mutations à attraper :
  masque `0xD0` → `0x50`, direction écrite après `TryUseItem`, sortie de terre `0x2D` au sol, retournement oublié pour `0x3B`, charge
  non remise à 0, trio non sauté, groupe `0x80032758` réécrit en `0x1D`.
- **Retour arrière** : `git revert` des commits d'E20.i.

**Questions pour l'auteur.**
- **Q-N3, complétée** (non bloquante pour E20.i) : la nage n'a besoin d'aucun mur ; sans lui, le héros sans bottes nage, dès la mer
  de la 416. Recommandé : le mur en E20.i2, juste après (une égalité sur `0xE00`, pas un bit de masque : les échelles valent `0xC00`).
- **Q-I1** (non bloquante) : les courants (`+0x188`, 16 cartes, dont la cascade de la 416) : E20.i2 avec le mur, ou plus tard ?

---

### 1.15 E20.e0x — `BankKey` et indices valides par carte dans l'export ✅ (détaillée le 2026-10-10 ; faite et CONFIRMED le 2026-10-15, `3e136c1` à `aad4fe8`, ADR-0062)

Découverte en lecture seule du 2026-10-10 (conception, puis critique adversarial qui a tout recalculé de son côté ; rapports dans le
scratchpad de la session : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20/e0x_design.md` et `e0x_critic.md`, scripts sous
`disc-e20e0x/`). Risque : format de données (comme E14.a1a).

**Révision 1 (2026-10-10).** Relecture : **REVISE**, un point corrigé (FIX) : le texte exact de `Data/map-sprite-indices.json` est
fixé (clés `"N"`, extrait octet pour octet d'une carte vide et d'une carte avec indices, `[]`, fins de ligne, ni BOM ni saut de ligne
final, liste des cartes de `DiscoverNativeMapIndices`) ; la règle de validité qui fait foi est écrite, avec son contrôle croisé.
Seconde relecture : **READY** (2026-10-10).

**But.** Le projet exporté dit, pour chaque préfab, de quelle table de sprites et de quel indice il vient (`BankKey`), et, pour chaque
carte, quels indices de la page des cartes sont valides (D-E20-33) : E20.e0a pourra traduire l'indice de `SpawnWarpEntity` en préfab, et
refuser un indice absent de la carte courante, comme le binaire.

**Faits porteurs.**
- [binaire] `SpawnWarpEntity` `0x80039F58` appelle `0x80039B28(a1, a2, &pal, &sheet)` : `a1 == 0` lit la table globale (bloc
  `0x80126EC0`, palette `0xB`, feuille `0x60`), sinon la table de la carte (bloc `0x80126E70`, palette 0, feuille `0x20`) ; il faut
  `0 <= a2 < [bloc+0x10]` et une entrée différente de −1, sinon rien n'apparaît (retour 0 avant toute case). L'identifiant de l'entité
  vaut `a2` (page globale) ou `0x100 + a2` (page des cartes). Le chargeur `0x8002D808` garde les −1 ; l'indice 0 de la page globale est le
  héros. 102 sites d'appel (21 page globale, 81 page des cartes, dont 5 à indice calculé) ; le second consommateur de la table,
  `SpawnChildEntity` (`0x8003A048`), sert le mille-pattes (page des cartes, indices 233 à 236).
- [données] 484 fichiers, 257 entrées de table chacun ; une entrée −1 est exactement une fiche nulle (0..255), l'entrée 256 vaut 0.
  `Header.Sector5Id` égale l'indice de la table partout. Page globale : 151 indices valides (0..254). Page des cartes : 244 nombres
  valides au moins sur une carte (manquent 64, 75, 78, 96, 97, 179, 180, 183, 186, 195, 201, 210), de 0 à 22 par carte, 65 cartes sans
  aucun ; 2 507 paires (carte, indice) en tout. 146 nombres sont valides sur les deux pages : la page fait partie de la clé. L'en-tête et
  les animations d'un indice de la page des cartes sont identiques sur toutes les cartes où il existe : une table indice → préfab par
  page suffit, la validité est, elle, propre à chaque carte.
- [convertisseur] Les clés existent déjà : `SpriteBankReader.cs` (vers `:329`) forme `alundra_{sector5Id}` (table globale) ou
  `{sector5Id}` (banques des cartes), exposé par `SpriteBank.BankKey` ; le préfab vaut `Ids.For("entity:{BankKey}/{dossier}")`
  (`SpriteWriter.cs`, vers `:397`) ; `prefabAssetIdsByBankKey` n'existe qu'en mémoire (`ConvertSprites`, vers `:163`), complet quand
  `WriteSpriteRecords` est appelé (vers `:210`) ; `EntityPrefabLinkWriter.cs` (vers `:148`) résout déjà ces chaînes. `ReadMapSpriteRecords`
  lit chaque carte mais ne garde aujourd'hui que la première fiche de chaque `Sector5Id`.
- [arbre] Base : `alundra-project/` à 24 103 fichiers hors `Alundra.dll`, `Alundra.pdb` et `.casaeditor/`, manifeste SHA-1
  `662056cebb0395f014099d1bd5ffc8f071a6fd58` (égal au `m3` d'E14.a1a hors `report.json`) ; `Data/sprite-records.json` à 2 561 505 o,
  SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf` ; pointeur du moteur `781d3527`, inchangé depuis le dernier export.

**Décisions (format, ADR-0062).**
- `BankKey` : chaîne JSON, **premier membre** de chaque entrée de `Data/sprite-records.json`, toujours écrite ; valeurs
  `"alundra_N"` (151) et `"N"` (244, sans zéros), exactement `SpriteBank.BankKey`. Une entrée sans clé est une erreur dure du
  rapport (comme l'invariant `SpriteRecords.Exported == Entities.Prefabs`). `SpriteRecordHeader` ne change pas.
- Nouveau fichier `Data/map-sprite-indices.json`, hors du catalogue des assets :
  - objet racine ; une clé par carte, **le numéro seul en chaîne** (`"388"`, jamais `"map_388"`), pour **chaque** carte que liste
    `DiscoverNativeMapIndices` (les 483 fichiers `map_<N>.json`, N de 0 à 482 ; `map_alundra` exclu), en ordre numérique croissant ;
  - valeur : le tableau, trié en ordre croissant, des indices i (0 à 255) de la page des cartes **dont la fiche `SpriteRecords[i]` n'est
    pas nulle** (règle qui fait foi ; dans les données, c'est exactement `SpriteTable[i] != -1`, que T0 vérifie pour les 483 cartes et
    que le convertisseur contrôle aussi : un écart entre les deux est une erreur dure du rapport) ; tableau vide `[]` pour une carte sans
    indice ou un fichier sans `SpriteInfo.SpriteRecords` ;
  - texte : celui de `JsonSerializer.Serialize` avec les options de `sprite-records.json` (`WriteIndented = true` seul), écrit par
    `File.WriteAllText` : UTF-8 sans BOM, fins de ligne CRLF, 2 espaces par niveau, un nombre par ligne, `[]` pour un tableau vide, aucun
    saut de ligne après l'accolade finale. Début exact du fichier (CRLF à chaque fin de ligne) et entrée de la carte 388 :

    ```
    {
      "0": [],
      "1": [
    …
      "388": [
        121,
        122,
        237,
        253,
        254,
        255
      ],
    ```
- Pas de nouveau compteur dans `report.json`.

**Périmètre.** `alundra-casaengine-project-converter/` : `Writers/SpriteWriter.cs` (`SpriteRecordJson.BankKey` en premier,
`WriteSpriteRecords` reçoit la table des clés, erreur dure, écriture du nouveau fichier, doc de classe) ; `Readers/SpriteBankReader.cs`
(collecte des indices valides par carte, sans changer la déduplication) ; tests du convertisseur (`SpriteWriterSpriteRecordsTests.cs`
et un test du nouveau fichier ; la fixture actuelle n'a qu'une banque de carte : il faut un paramètre pour une banque globale) ;
`docs/formats/sprite-records.md` (en français : phrase d'introduction, ligne du schéma, exemple de Yustel avec `"BankKey": "3"` en premier)
et une page de format pour le nouveau fichier (ou une section de la même page) ; `docs/formats/README.md` ; ADR-0062 ; annexe
`docs/plan-e20-annexe/e20-e0x/` (prédiction et scripts, écrits **avant le code**).

**Hors périmètre.** La DLL (E20.e0a lit ces données) ; la palette et la feuille par page (à trancher par E20.e0a) ; tout miroir de
`data-extracted/` ; tout autre champ.

##### Tâches

- ✅ **T0 — Prédiction et état de départ** (commit de docs, avant tout code). Annexe `docs/plan-e20-annexe/e20-e0x/README.md` avec :
  un script de prédiction indépendant du code du convertisseur (copié du critique, `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0x/critic/my_predict.py`,
  ou réécrit, versé dans `e20-e0x/`) qui produit `sprite-records.json` prédit et `map-sprite-indices.json` prédit ; leurs tailles et
  SHA-1 ; les comptes ; les valeurs témoins. Attendu pour `sprite-records.json` : **2 571 585 o (+10 080), SHA-1
  `88d723066fd2e6232a6f815dad8de9b0c05c64e4`**, insertion pure de 395 lignes `    "BankKey": "…",` juste après chaque ligne
  `  "<guid>": {`. Pour `map-sprite-indices.json` : 483 clés (toutes les cartes), 2 507 indices en tout, 65 tableaux vides ; taille et
  SHA-1 calculés par le script selon le texte exact fixé ci-dessus (Décisions) ; le script vérifie aussi, carte par carte, que les
  fiches non nulles et les entrées de table différentes de −1 coïncident. Outils de manifeste (`manifest.py`, `diff_manifests.py`) copiés dans l'annexe. Manifeste `m0` : 24 103 fichiers, SHA-1
  `662056ce…` (un écart s'explique avant d'aller plus loin). Compte de tests du convertisseur (849) et d'`Alundra.Tests` relevés.
- ✅ **T1 — Export de référence au HEAD**, avant toute modification du convertisseur. Fenêtre calme : aucun build, aucune suite, aucun
  Launcher ni éditeur sur `alundra-project/` ; `Alundra.dll` déverrouillé. Depuis la racine :
  `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project` ; sortie 0 et
  `Verification: PASSED` ; `m1` contre `m0` : seulement `report.json`. Valeurs de référence relevées dans le `report.json` de T1.
- ✅ **T2 — Tests rouges.** `BankKey` premier membre, valeur d'une banque de carte (`"N"`) et d'une banque globale (`"alundra_N"`) ;
  erreur dure si une entrée n'a pas de clé ; `map-sprite-indices.json` : clés des cartes en ordre numérique, indices triés, carte sans
  indice → `[]`, une entrée −1 exclue ; les deux fichiers restent hors catalogue.
- ✅ **T3 — Code et docs.** `dotnet test alundra-casaengine-project-converter.Tests --blame-hang-timeout 60s`, au premier plan : 849 plus
  les nouveaux, tous verts.
- ✅ **T4 — Export 1, puis export 2 tout de suite.** `m2` contre `m1` : changé `Data/sprite-records.json`, ajouté
  `Data/map-sprite-indices.json`, changé `report.json`, rien d'autre ; `cmp` de chaque fichier avec sa prédiction ; `m3` contre `m2` :
  `report.json` seul.
- ✅ **T5 — Suites.** `Alundra.Tests` en Release puis en Debug : même compte, sans échec (la DLL ignore la clé et le fichier jusqu'à
  E20.e0a) ; `cmp` de la DLL ; traces inchangées.
- ✅ **T6 — ADR-0062 (format), plan, verifier frais** (le verifier est lancé par la session principale).

##### Acceptation

- `Data/sprite-records.json` égal à la prédiction au bit près (2 571 585 o, SHA-1 `88d72306…`) : 395 entrées, `BankKey` premier membre,
  151 `alundra_N` et 244 `N`, toutes distinctes ; chaque objet égal à l'ancien, moins `BankKey` ; valeurs témoins : `"91"` →
  `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08` (Muruta), `"79"` → `1ef8f16c-605f-58c3-af19-b6bb602fc40f` (Beannoïde), `"alundra_7"` →
  `62670cc6-953d-5fb2-ad61-8f130b96bf1f` (flèche chargée), `"alundra_2"` → `4d0c2294-87b6-50da-99ca-d789281cc580` (bombe),
  `"alundra_0"` → `192c2eeb-…` (premier global), `"255"` → `14fa90f8-4764-531c-b751-091b644580d7`.
- `Data/map-sprite-indices.json` égal à sa prédiction au bit près : 483 clés (`"0"` à `"482"`), 2 507 indices, 65 tableaux vides
  (dont `"0"`) ; `"388"` = [121, 122, 237, 253, 254, 255] et `"451"` identique, `"432"` = [29, 253, 254, 255] (aucun ne contient 213) ;
  `"95"` et `"322"` contiennent 233 à 236.
- Tout ce qui suit est relatif à l'export de référence de T1 : `AssetInfos.json` égal à celui de `m1` (`a09917c3…` attendu) ; `report.json`
  de l'export 1 : `Errors` 0, mêmes `Warnings`, mêmes `Verify.*`, mêmes compteurs (aucun ajouté ni retiré), `OutputFileCount` de T1 + 1 ;
  `OutputSizeBytes` = valeur de T1 + 10 080 + taille de `map-sprite-indices.json` + écarts de la DLL, du PDB et du `report.json` présent
  (mesurés ; un reste inexpliqué est un arrêt). Export 2 : `report.json` seul.
- Rien ne change sous `alundra-project/UI/Screens/` ; convertisseur vert ; `Alundra.Tests` sans échec en Release et en Debug ; verifier
  frais.

##### Arrêts, budget, retour arrière

- **Arrêts** : T1 change autre chose que `report.json` (attribuer, rebaser comme E14.a1a, sinon question) ; après le code, tout autre
  chemin changé, ajouté ou retiré ; `AssetInfos.json` qui bouge ; un fichier différent de sa prédiction ; un second export différent hors
  `report.json` ; une erreur ; un compteur ou un avertissement non prévu ; un process sur l'arbre pendant un export ; toute idée de
  supprimer `alundra-project/`, d'un `robocopy /MIR`, d'un miroir de `data-extracted/` ou d'indexer `CasaEngineMonogame/`.
- **Budget** : un `executor`, deux tentatives au plus ; `plan-verifier` avant ; deux verifiers frais après (rejeu et adversarial).
- **Retour arrière** : `git revert` des commits d'E20.e0x, puis un export complet en place.

##### Résultats (2026-10-15)

Commits : T0 `3e136c1` (faite le 2026-10-10), T1 à T3 `f2e67bb` (tests, code, docs de format ; T1 n'a rien de versionné), T4 à T6 (ce texte, l'annexe `docs/plan-e20-annexe/e20-e0x/` et ADR-0062). Le verifier frais est lancé par la session principale. Premier passage coupé par une limite d'usage le 2026-10-10 après T0 ; `m0` revérifié au départ de celui-ci : **24 103 fichiers, `662056ce…`** (identique).

**Empreintes** (manifeste `manifest.py`, hors `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`) : `m0` `662056cebb0395f014099d1bd5ffc8f071a6fd58` (24 103) ; `m1` (export de référence T1) `d33b3fbaf97c5a592770988b4c5dec3963f6163c` (24 103, `report.json` seul change : `diff-m0-m1.txt`) ; `m2` (export 1) `b78cdf725918db8cd7c3491b4721d7c0623e1efc` (24 104 : `Data/map-sprite-indices.json` ajouté, `Data/sprite-records.json` et `report.json` changés, rien d'autre : `diff-m1-m2.txt`) ; `m3` (export 2, tout de suite) `9cc962671d586674cf7f1bd60b78dd1b59c2cd92` (24 104, `report.json` seul : `diff-m2-m3.txt`) ; un manifeste pris après les builds et suites de T5 est identique à `m3`. Les trois exports : code de sortie 0, `Verification: PASSED (20139 loaded, 2477 existence-checked)`.

**Acceptation, mesurée.**
- `Data/sprite-records.json` : **2 571 585 o, SHA-1 `88d723066fd2e6232a6f815dad8de9b0c05c64e4`, `cmp` identique à la prédiction** ; 395 entrées, `BankKey` premier membre de chacune, 151 `alundra_N` et 244 `N`, 395 clés distinctes, ordre des entrées inchangé, chaque objet égal à l'ancien moins `BankKey` ; les cinq valeurs témoins et `"alundra_0"` (`192c2eeb-bdda-5bb4-92d6-39a4ae298307`) sont celles attendues.
- `Data/map-sprite-indices.json` : **30 913 o, SHA-1 `63a3e6189d1f1375232bb2c54e1d1dcb385e360d`, `cmp` identique à la prédiction** ; 483 clés (`"0"` à `"482"` dans l'ordre), 2 507 indices, 65 tableaux vides (dont `"0"`) ; `"388"` = `"451"` = [121, 122, 237, 253, 254, 255], `"432"` = [29, 253, 254, 255], aucun ne contient 213 ; `"95"` et `"322"` contiennent 233 à 236.
- `AssetInfos.json` : `a09917c31694925076d1fc1aba5fca773f6cada2`, égal à `m1`. `report.json` de l'export 1 contre T1 : `Errors` 0, mêmes `Warnings` (6), mêmes catégories, mêmes `Messages`, mêmes compteurs (aucun ajouté ni retiré), `OutputFileCount` 24 106 puis **24 107** (+1).
- `OutputSizeBytes` : T1 1 955 182 584 ; export 1 1 955 223 576 ; export 2 1 955 223 575. Écart T1 → export 1 : **+40 992 = 10 080 + 30 913 + (9 283 - 9 284)**, le dernier terme étant l'écart du `report.json` présent sur le disque au moment de la mesure (le rapport de T1 pèse 9 283 o, celui d'avant 9 284 o) ; `Alundra.dll` (574 464 o) et `Alundra.pdb` (204 800 o) n'ont pas bougé : aucun reste inexpliqué. Export 1 → export 2 : -1 (9 283 → 9 282 o). Valeur de `OutputSizeBytes` de départ (avant T1) : 1 955 259 509.
- Rien ne change sous `alundra-project/UI/Screens/` (manifestes ; `git status` propre sur ce dossier).
- Tests du convertisseur : **854 sans échec** (849 + 5 : `SpriteWriterBankKeyTests` : `BankKey` premier membre pour une banque de carte et la banque globale, erreur dure sans clé, clé connue, indices par carte dans l'ordre numérique avec nulls et fichier sans `SpriteRecords` et texte exact, erreur dure d'un écart avec `SpriteTable`). `Alundra.Tests` : **4 752 sans échec en Release et en Debug** (même compte qu'avant) ; `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique ; garde des traces (`git diff --ignore-cr-at-eol --exit-code`) passée, le bruit de fins de ligne des quatre `hero-trace-389-*` restauré par `git checkout --`.

**Décisions d'exécution.** `ReadAllBanks` reçoit un paramètre facultatif (dictionnaire de sortie) : la déduplication des banques ne change pas, la collecte des indices valides se fait avant elle (une fiche déjà vue sur une autre carte compte pour cette carte). `ResolveBankKey` et `WriteMapSpriteIndices` sont `internal` (accessibles aux tests par `InternalsVisibleTo` existant). Le contrôle croisé avec `SpriteTable` ne s'applique que si la carte porte une `SpriteTable`.

**Écarts, arrêts.** Aucun arrêt du plan n'a joué. Test rouge de T2 : compilation en échec (`SpriteWriter.ResolveBankKey` inexistant), les autres tests du fichier étaient rouges par construction ; T2 et T3 sont dans un seul commit pour rester constructible.

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre ; la première exécution, le 2026-10-10, a
été coupée par la limite d'usage après T0, sans export). Lecture seule : diff du convertisseur conforme (seuls `SpriteBankReader.cs`,
`SpriteWriter.cs` et les tests changent ; `BankKey` déclaré en premier ; erreur dure sans clé ; clés des cartes en ordre numérique et indices
triés ; contrôle croisé avec `SpriteTable` en erreur dure ; aucun compteur nouveau) ; annexe versée avant le code (`3e136c1`) ; ADR-0062 et
docs de format ; recalcul indépendant des 395 clés et de `map-sprite-indices.json` octet pour octet, 0 écart. Rejeu : un export de plus ne
change que `report.json`, les deux fichiers égaux à leurs prédictions, compteurs égaux à ceux de T1, convertisseur 854/854, `Alundra.Tests`
4 752/4 752 en Release et en Debug, `cmp` de la DLL, traces. Remarques P4, dispositions :
- T2 et T3 dans un même commit : l'ordre « tests rouges d'abord » n'est attesté que par le rapport de l'exécuteur (compilation en échec) :
  **noté**.
- En cas d'écart avec `SpriteTable`, le message d'erreur dit que `map-sprite-indices.json` ne peut pas être écrit alors que le fichier est
  écrit quand même ; l'erreur reste dure, l'export échoue : **reporté** à la prochaine tranche qui touche le convertisseur (message à
  corriger, ou écriture à conditionner).
- `Alundra.dll` et `Alundra.pdb` ont changé de taille après les builds de T5 (redéposés par la suite de tests), sans effet sur la preuve
  (le manifeste les exclut) : **noté**.

### 1.16 E20.e0a — Apparition par indice de sprite : e0a1 (oracle, catalogue, interface), e0a2 (table de cases), e0a3 (tick de naissance) ✅ (détaillée le 2026-10-15 ; e0a1 à e0a3 faites et CONFIRMED le 2026-10-15 ; e0a4 conditionnelle, selon Q-A1)

Découverte en lecture seule du 2026-10-15 : trois relevés (binaire, impact sur la DLL, moyens de test), puis un critique adversarial qui a revérifié plus de trente affirmations
(désassemblage de `ALUN_CD.EXE`, rejeu des sondes de l'oracle, lecture de la DLL et du moteur) et mené sa propre batterie de mutants. Scripts et sorties dans le
scratchpad de la session : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0a/`
(`bin/`, `tools/`, `tests/`, `dll/`, `critic/`). Aucune modification du moteur (`CasaEngineMonogame/`) ni du convertisseur ; E20.e0x est close (✅ CONFIRMED le 2026-10-15,
`0107cfc` : `BankKey` dans `Data/sprite-records.json`, `Data/map-sprite-indices.json`, ADR-0062). La tranche est découpée en trois, plus une quatrième conditionnelle,
parce que ses trois effets n'ont pas le même rayon d'action : e0a1 ne change aucun comportement existant, e0a2 peut déplacer l'ordre des passes, e0a3 touche la structure du tick.

**But.** Faire apparaître une entité par `SpawnWarpEntity` (`0x80039F58`) et la moitié sans enregistrement d'`InitializeEntity` (`0x80039D04`), comme le binaire, pour les armes du
héros (E20.f3 à f6), l'usage des objets (E20.h3), le butin (E20.e0b, E20.e0c) et les gestionnaires d'ennemis (E14). Rien de ceux-là n'est branché ici : cette tranche ne livre
que l'interface, la table de cases et la participation au tick de naissance, prouvées contre le binaire.

**Faits porteurs.** Étiquettes : [binaire] décodé ou rejoué sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS des oracles d'E14.a1 et d'E20.c ;
[DLL] lu dans le code du port, au HEAD `0107cfc` ; [données] lu dans les cartes extraites ou l'export d'E20.e0x.
- [binaire] `0x80039F58` appelle d'abord `0x80039B28(page, indice, &palette, &feuille)` ; un échec rend 0 avant toute case, sans toucher à la série `[0x80134600]`, au compteur
  `[0x80134360]` ni au générateur (`0x80039FA0`). Page 0 : bloc `0x80126EC0`, palette `0xB`, feuille `0x60` ; page des cartes : bloc `0x80126E70`, palette 0, feuille `0x20`. Un indice est
  valide si `0 <= i < compte (256)` et si l'entrée est dans `0x80000000..0x801FFFFF` (−1 et 0 sont invalides). Palette et feuille sont écrites avant la validation. Puis
  `FindFreeEntitySlot` `0x80039AD0` : cases 1 à 63 (départ `0x80127FC4`, pas `0x294`, test `+0x10 == 0`, borne `slti 0x40` en `0x80039AFC`) ; table pleine : message `0x800239B0`, retour 0.
  La case 0 (le héros) n'est jamais rendue. L'identifiant `+0x68` vaut l'indice, plus `0x100` pour la page des cartes.
- [binaire] Sans enregistrement, `0x80039D04` écrit : compteur `[0x80134360] = max(., case)` ; `+0x0C` = parent (toujours, même nul) ; `+0x08` = `parent.+8` s'il est non nul, sinon le parent,
  et seulement si le parent est non nul ; `+0x44 = 0` ; `+0x48 = −1` ; état 1 ; série `++[0x80134600]` ; drapeaux, six codes, palette, feuille, fiche d'équilibre, PV ; `+0x230` = la case elle-même
  (`0x8004201C`, dans le délai du saut en `0x80042028`, donc même sans enregistrement) ; boîte du corps ; `+0x114/+0x118` = X, Y tels que donnés ; `+0x11C = Z − ModZ + 1` (`0x80039EA0-0x80039EA4`),
  relevé à `terrain + 1` si `terrain + 1 >= PosZ` (`0x80039EF0`) ; premier changement d'animation `0x80038AB4`, qui joue le son de l'animation 0 (`0x800490FC`) ; enfin le tirage du butin `0x80032A40`.
  L'argument Z est donc la base de la boîte du corps, pas `PosZ`.
- [binaire, données] Le son de création : 7 préfabs de la page globale (7, 8, 9 → `0x21` ; 17 → `0x37` ; 27, 28, 29 → `0x1E`) et 9 numéros de la page des cartes (159, 170, 192, 194, 213, 242, 249, 254, 255 ; 192, 194 et 249 portent le
  bit `0x20` : identifiant + `0x100`). Le tirage du butin à la création concerne 54 numéros (`Contents & 0x80` : 6 de la page globale, 48 de la page des cartes) ; il n'est pas porté ici (e0b).
- [binaire] Passe des cases `0x80038634` (cases 0 à 63) : une case d'état 4 reçoit le patron `0x80134368` (zéros, sauf `+0x48 = −1`) et son indice en `+0`, puis le compteur devient l'indice de la plus haute case non libre. Une case d'état 4 reste donc
  **occupée** jusqu'à la passe suivante (oracle E2 : la case 1 en état 4, l'apparition va en case 2). La passe, les événements, les compteurs, les listes, l'animation, la physique, les effets et les coups s'enchaînent dans cet ordre derrière la
  porte `[0x800DC4B8] & 0x48` (`0x8003B388-0x8003B3F4`).
- [binaire] 102 sites `jal 0x80039F58`, tous entre `0x8002F818` et `0x8007FD30` ; l'interpréteur `0x8004205C` n'a que deux appelants (`0x8003890C`, `0x8003C790`) : les opcodes `0x2D`, `0x8A` et `0x8B` n'atteignent que l'apparition par enregistrement
  `0x8003A1B8` (10 appelants, dont le chargement de carte `0x8003B308`), qui fait la recherche de table **avant** la case. Le tir du héros ignore le retour (`0x8002F818-0x8002F828` : le pointeur d'arme est effacé même si l'apparition échoue).
- [oracle, rejoué] Participation au tick de naissance. Apparition dans `MovePlayer` (M1 à M3), dans la boucle 1 sur une case plus haute (L1, L3) : l'entité passe en état 2, son programme A tourne une fois, `+0x1C = 1`, elle est dans les listes, l'horloge et la physique la voient. Boucle 2 sur une case
  au-dessus du compteur dont le déclencheur vaut 0 (H1, H4, H6) : état 1 pendant tout le tick, hors des listes, mais A tourne **dans la même passe**, puis encore au tick suivant. Boucle 2 sur une case dont le déclencheur vaut −1 (H2, H3, H5) ou boucle 1 sur une case plus basse (L2) : tout est reporté
  d'un tick. Rejoué par le critique : les 12 lignes de la sonde (M1 à M3, H1 à H6, L1 à L3) sont identiques.
- [oracle, rejoué] Validité : sur les 483 cartes × 256 indices, 2 507 paires valides, 65 cartes vides, au plus 22 par carte, 244 numéros ; le jeu valide de chaque carte est égal aux fiches non nulles, donc à `Data/map-sprite-indices.json` ; un numéro a **une seule** image de création sur toutes les cartes où il existe :
  395 vecteurs (151 + 244) suffisent, pas 2 507. Table pleine : 63 apparitions acceptées, la 64e et la 65e rendent 0 avec un message chacune ; les libérations 40, 9, 21 sont reprises dans l'ordre 9, 21, 40.
- [DLL] `_spawnedEntities` est une `List<Entity>` privée, ajoutée en trois endroits seulement : le chargement (`AlundraWorldProxy.cs:898`), l'adoption du héros (`:2088`) et `SpawnEntityByRecordId` (`:3161`). Six classes de tests la lisent par réflexion (`AlundraDestroyedEntityRecyclingTests:87`,
  `AlundraGameplayFreezeTests:210`, `AlundraTickStructureTests:91`, `AlundraWarpDepartureTests:146`, `AlundraWorldProxyAudioInstallationTests:354`, `AlundraWorldProxyUpdateCharacterizationTests:530`) : nom et type sont figés. `RecycleDestroyedEntities` (`:3017`) retire les entités d'état `FlagToDestroy` juste après les événements
  de carte (`:2356`), sous la même porte (`GameplayBlockedMask`, 0x48) que la passe des cases du binaire : l'instant est déjà le bon. Chaque monde a son propre proxy (`Clone` rend une instance neuve), donc un masque de cases par instance repart à zéro à chaque carte.
- [DLL] `CollectTickEntities` (`:2647`) ne retient que les entités `TookPartInFrame`, que le moteur positionne à l'image qui suit l'intégration (`World.AddEntity` met en file, `InternalAddEntities` tourne en tête de `World.Update`, le tick de la DLL est le dernier acte de `World.Update` : `World.cs:168-173`, `:528`, `:581-584`). Les compteurs
  (`:2766`), les listes et `BuildHitList` (état `Normal` ou `Deactivated`, `EntityStatus.cs:55`) parcourent toutes les entités. Avant l'intégration `Entity.World` est nul : `CharacterControllerComponent.Move` ajoute alors le déplacement sans test (`CharacterControllerComponent.cs:1050-1056`, `:1601-1625`) et
  neuf lectures de `Owner.World.CollisionField` ne trouvent rien (`AlundraEntityScriptProxy.cs:746`, `:1724`, `:1795`, `:1886`, `:1960`, `:2076`, `:2240`, `AlundraPlayerAnimationEffects.cs:142`, `AlundraScriptedMotion.cs:695`).
- [DLL] Trois épingles tiennent la règle « première image à l'image suivante » pour les apparitions par enregistrement : `AlundraTickStructureTests.cs:367-392`, `AlundraVisionArcTests.cs:348-353` (`0x8A` à 63 : le bloc n'est pas dans le monde au moment de l'apparition), `AlundraShipBlockArcTests.cs:235-240` (`0x2D` à 265 : `C[100]` à l'image 2).
- [DLL] `Index` et `Index2` existent (`AlundraEntityScriptProxy.cs:65-66`) mais personne ne les écrit ; `Index2` n'a qu'un lecteur (le verrou d'interaction, `AlundraPlayerManager.cs:1697`, `:1715`). Le champ `ChildEntity` (`:67`, le nom de la décompilation pour `+0x08`) existe aussi et n'est ni écrit ni lu.
  `EntityRefId` vaut 0 par défaut (`:99`) et `EntitySearchService.cs:110` compare sans filtre d'état : une entité sans `−1` répondrait à `0x2C [0]` et `0x2E [0]`. `ApplyRecord` met `EventTrigger = −1` (`AlundraEntitySpawnFactory.cs:529`).
- [DLL] La fabrique donne déjà `Z − ModZ` sans le « +1 » du binaire (`AlundraEntitySpawnFactory.cs:674`, D-E19-59, ADR-0026) et relève à `T` avec un champ de collision passé en paramètre (`:679-689`) : l'apparition par enregistrement n'a pas besoin de `Owner.World`. Le tir du héros passe `Z = PosZ + (ptr[3] << 16)` dans le repère de la DLL
  (`AlundraPlayerManager.cs:1186-1196`) ; le binaire passe `+0x11C` du héros, soit une unité brute de plus.
- [DLL] `SpawnWeaponProjectile` (`AlundraPlayerManager.cs:1186`) ne fait que compter. L'enveloppe (§1.1, ligne E20.f3) en réserve le branchement à E20.f3, un type à la fois, jamais avant son gestionnaire E : une flèche née sans E17 ne serait jamais détruite et remplirait les 63 cases.

**Écarts entre les trois relevés, tranchés par la critique.**
1. *Z.* Le relevé de la DLL dit « Z de la DLL, `z − ModZ`, sans +1 (D-E19-59) » ; celui des moyens de test dit que l'interface doit ajouter l'unité. Le second a raison pour un Z dérivé du héros : binaire `PosZ_brut = Z_arg − ModZ + 1` avec `Z_arg = hero_brut + p`, et `hero_brut = hero_DLL + 1`, donc
   `PosZ_DLL = (Z_DLL + 1) − ModZ`, puis `max(., T)`. Sans l'unité, l'entité est une unité brute (1/65536 px) trop basse devant tous les autres ; elle ne l'est pas quand le Z est une constante du binaire. Décision D-E0A-1.
2. *Racine `+0x08`.* Le relevé de la DLL dit « aucun champ » : `ChildEntity` existe (voir ci-dessus). L'écrire coûte une ligne et rend la projection de l'oracle exacte, sans lecteur connu (la décompilation ne la lit nulle part : `GameEngine.cs:1694` l'efface, `EntityManager.cs:57-64` l'écrit).
3. *Branchement du tir du héros.* Les relevés de conception (tâche 7 de `e0-check.md`, étape 6 de l'impact DLL) le mettent dans e0a ; l'enveloppe le met dans E20.f3 : l'enveloppe fait foi (une flèche sans E17 est immortelle).
4. *Interface.* L'impact DLL met la méthode sur `IAlundraScriptHost`, les moyens de test sur `IEntityWorldContext`. Les gestionnaires natifs reçoivent un `IEntityWorldContext` (`AlundraNativeSpriteRegistry.cs:13`) et le héros l'atteint par le motif déjà employé (`ScriptHost as IEntityWorldContext`, `AlundraEntityScriptProxy.cs:1428`) : la méthode va sur `IEntityWorldContext`, membre d'interface par défaut (29 implémentations de `IAlundraScriptHost`, le monde compris, et une quarantaine
   de `IEntityWorldContext`, 49 fichiers de tests concernés).
5. *Position de la flèche (3 px, puis 6 px).* Les relevés la donnent comme critère. La DLL règle la vitesse sur `CurrentAnimationId` (`AlundraScriptedMotion.cs:167`) ; une entité neuve le porte à `~0` jusqu'à sa première publication : sa vitesse est nulle. Ce critère relève de la cinématique (E20.e0k, E20.f3) ; ici, seule la sonde P-A le tranche.
6. *Passe de réparation du critique sur l'oracle.* L'ensemble « 10 projectiles + objets 1 et 2 » du plan de test ne mord pas sur `Z − ModZ` : tous ont `OffsetZ = 0`, et à Z = 0 le relèvement au terrain masque `OffsetZ` pour les autres. Le mutant `0x80039EA0` (la soustraction remplacée par rien) survit. Il est tué par un préfab à `OffsetZ ≠ 0` apparu **au-dessus** du sol : page 0 n° 20 (−18) à 50 px, n° 64 (−8), n° 242 (+14) ; page des cartes n° 153 (+12) à 40 px.

**Décisions de conception (proposées, à relire avec ce plan ; ADR à numéroter à l'écriture, prochain libre 0063).**
- **D-E0A-1** — L'interface prend le Z dans le repère de la DLL (celui que l'appelant lit dans `PosZ`) et ajoute l'unité brute en interne : `PosZ = (z + 1) − ModZ`, puis relevé à `T` (`max`). Une constante du binaire est passée moins 1. Épinglé par le vecteur « flèche au Z du héros = Z du héros + 1 unité brute » (oracle : brut 2, DLL 1) et par le tir de la trace AT9 (binaire `z = 1 048 577`, appel du stub de la DLL `1 048 576`).
- **D-E0A-2** — `SpawnEntityBySpriteIndex(parent, estCarte, indice, x, y, z, direction)` est un membre par défaut de `IEntityWorldContext` (rend `null`) ; `AlundraWorldProxy` l'implémente. `null` rend aussi pour un indice invalide, une table de cases pleine (e0a2) ou un préfab absent, avec une ligne de journal et aucune case prise ; l'appelant doit tester `null` (le binaire écrit à travers le pointeur 0 : A11 `0x80061870`, A21 `0x80061A2C`).
- **D-E0A-3** — Pas de tirage du butin à la création (e0b) ni de bruit de générateur : les 54 numéros concernés sont exclus de la comparaison de `+0x3C`, `+0x40` et du mot du générateur, écart écrit dans le code et ici, fermé par e0b.
- **D-E0A-4** — La racine `+0x08` est écrite dans `ChildEntity` (`parent.ChildEntity ?? parent`, seulement si le parent est non nul), avec un commentaire qui dit que la décompilation la nomme ainsi et qu'aucun lecteur n'est connu.
- **D-E0A-5** — La participation au tick de naissance est portée par un marqueur de l'entité (`JoinsBirthTick`), posé seulement par la nouvelle interface. Elle ne change rien aux apparitions par enregistrement (opcodes `0x2D`, `0x8A`, `0x8B`, chargement) tant que Q-A1 n'est pas tranchée. Le repli sur le champ de collision de l'hôte (`CollisionFieldNow`) est **limité au marqueur** : sinon `ClampToGround`, appelé par
  `PushLogicalPositionToRoot` à l'apparition, cesserait d'être inerte pour les entités par enregistrement (O-E19-6) et pourrait déplacer des épingles (E19.h1b1 a retiré l'appui à l'apparition ; non mesuré).
- **D-E0A-6** — Le défaut « A deux fois » (apparition en boucle 2 sur une case au-dessus du compteur : A tourne dans la passe, puis encore au tick suivant) n'est pas porté : l'entité née dans un programme natif d'une autre entité tourne à partir du tick suivant (écart épinglé par un test nommé ainsi, point ouvert O-E0A-1). Les apparitions en boucle 1 (butin à la mort) relèvent d'e0b et e0c.
- **D-E0A-7** — Les pointeurs périmés ne sont pas portés : une entité recyclée garde l'identité de son objet (`Destroyed`), alors que le binaire laisse un pointeur périmé voir le nouvel occupant de la case. La série `+0x04` n'est pas écrite (son seul lecteur, le verrou d'interaction, compare une valeur à elle-même).
- Par défaut fidèle, sans question (liste d'enveloppe) : plafond de 63 cases, case réutilisée = la première libre, case d'état détruit occupée jusqu'au recyclage, participation au tick de naissance dans la DLL.

**Questions à l'auteur** (aucune ne bloque e0a1 et e0a2 ; Q-A1 attend la mesure P-B d'e0a3) :
- **Q-A1** — Étendre la participation au tick de naissance aux apparitions par enregistrement (opcodes `0x2D`, `0x8A`, `0x8B`, événements de carte), ce qui ferme O-E20-11 mais déplace des épingles d'arcs (liste mesurée par la sonde P-B d'e0a3) ? Recommandation : décider après la mesure ; en faire alors la tranche e0a4, jamais dans e0a1 à e0a3.
  **Tranchée par l'auteur le 2026-10-15, après la mesure P-B** : oui, fidèle au binaire ; la tranche e0a4 se fait **après E20.e0b2**
  (même fichier `AlundraWorldProxy.cs`), les huit tests de P-B recalculés un à un.
- **Q-A2** — **Tranchée le 2026-10-15 (D-E20-34)** : « A deux fois » reste un écart documenté (D-E0A-6), épinglé par un test nommé ainsi ;
  rouvert par e0b, e0c ou E14 si un gestionnaire A en dépend (O-E0A-1).
- **Q-A3** — **Tranchée le 2026-10-15 (D-E20-35)** : une entité née dans un tick est dessinée à l'image suivante (intégration du moteur),
  sa logique ayant déjà tourné ; aucun changement de moteur.

**Prérequis et dépendants (révision 1 de l'enveloppe, 2026-10-15).** Ordre des sous-tranches : e0a1 (prérequis : E20.e0x ✅, E14.a0 ✅)
→ e0a2 (prérequis : e0a1) → e0a3 (en deux : e0a3a, sonde et oracle, prérequis e0a2 ; e0a3b, le code, prérequis e0a3a) ; e0a4 (prérequis : e0a3 et la réponse de l'auteur à Q-A1) **ne bloque aucun
dépendant**. Ce que chaque tranche dépendante exige d'E20.e0a :
- **E20.e0b** (générateur par tick, tirage du butin à la création) : e0a1 et e0a2 (la création, et l'ordre des cases qui fixe l'ordre des
  tirages dans les passes) ;
- **E20.e0k** (pas vertical et vol des entités autres que le héros) : e0a1 (une entité créée par l'interface pour ses tests) ;
- **E20.f3** (tir) : e0a1 à e0a3 (les projectiles naissent dans `MovePlayer` et tournent dans leur tick de naissance ; le plafond de 63
  cases compte) ;
- **E20.g1, E20.g2** (porter, lancer) : e0a1 ;
- **E20.h3** (graines et bombe, nées portées dans `MovePlayer`) : e0a1 à e0a3.

Relecture de l'enveloppe : REVISE (prérequis), révisée ; seconde relecture : **READY** (2026-10-15).

**Hors périmètre de toute la section.** Le branchement de `SpawnWeaponProjectile` et les gestionnaires A0, A254, E17 (E20.f3) ; le tirage du butin, l'avance du générateur, `DestroyEntity` avec lâcher, C255 (e0b, e0c) ; le pas vertical et le vol (e0k) ; `SpawnChildEntity` (`0x8003A048`) et la réinitialisation en place (`0x8003AC78`), cinquième appelant d'`InitializeEntity` (E14) ; la lecture de `+0x08` ; la série `+0x04`.

---

#### 1.16.1 E20.e0a1 — Oracle d'apparition, catalogue et interface, sans changement de comportement ✅ (faite et CONFIRMED le 2026-10-15, `4e15fb9` à `026932a`, ADR-0063)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, un point corrigé (FIX) : les 244 vecteurs de la page des cartes n'avaient pas
d'hôte (un numéro n'est valide que sur ses propres cartes ; `ArcRun` ne construit que quelques cartes). La projection de création des
395 vecteurs se compare maintenant au niveau du **noyau sans enregistrement** de la fabrique (sans monde) ; la validité par carte est
prouvée à part ; le chemin complet de l'interface est éprouvé sur la carte 14 seulement, avec un compte mesuré en T1. Points non
bloquants corrigés : chemins sources exacts du harnais, chemin de données relatif, chargeur paresseux, n° 79 de la page des cartes,
vecteur « table pleine » de l'oracle, formulation de l'arrêt sur la fabrique.
**Révision 2 (2026-10-15).** Seconde relecture : **REVISE**, un blocage ; dispositions de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — le chemin complet (b′) tourne sur le vrai terrain de la carte 14, l'oracle sur un champ plat : (b′)
exclut nommément les champs qui dépendent du terrain, prouvés par (b) ; **FIX** des points non bloquants : `InitializeBalance(tables, 0)`
nommé, place de l'unité de D-E0A-1 (dans l'interface, jamais dans le noyau partagé), repère DLL = binaire − 1 écrit à T0, mots de la
carte (`tileMapData`) laissés à `ApplySpawnInitialization`. Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-15).

**Prérequis.** E20.e0x ✅ (`BankKey`, `Data/map-sprite-indices.json`), E14.a0 ✅ (registre natif, pour les tests d'A).

**But.** Donner à la DLL l'interface `SpawnEntityBySpriteIndex` et ses données, avec l'oracle qui la prouve, sans qu'aucun chemin de production ne l'appelle : la suite existante ne doit pas bouger.

**Périmètre.** `docs/plan-e20-annexe/e20-e0a/oracle/` (nouveau ; outils copiés octet pour octet de `docs/plan-e14-annexe/e14a/oracle-a1/tools`, plus le harnais du scratchpad, qui n'existe aujourd'hui nulle part dans le dépôt : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0a/tests/espawn.py` et
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0a/tests/tools/` (`exp_spawn.py`, `exp_spawn2.py`, `pworld.py` ; les copies ailleurs sont identiques), leurs
dépendances (`machine`, `world`, `bank`, `c1world`, `emu`) venant de `docs/plan-e14-annexe/e14a/oracle-a1/tools` ; le chemin `DATA` codé
en dur à `espawn.py:20` devient relatif à la racine du dépôt) ; `Alundra/Scripts/SpriteRecordCatalog.cs` (`BankKey`, table (page, indice) → préfab) ; un chargeur de `Data/map-sprite-indices.json`, **paresseux** (lu au premier appel de l'interface, jamais au chargement d'un monde : aucun
monde de test existant ne voit d'avertissement) ; la carte courante est celle que donne le nom du monde
(`BackdropLoader.TryParseMapIndex`, comme `AlundraWorldProxy` le fait déjà) ; `AlundraEntitySpawnFactory.cs` (extraction d'un **noyau sans enregistrement**, appelable sans monde : préfab, page, indice, parent, X,
Y, Z (déjà dans le repère de la DLL), direction, champ de collision facultatif → l'entité et sa projection ; `ApplySpawnInitialization`
l'appelle, garde son comportement et garde seule les lignes qui lisent `tileMapData` (mots de gravité de la carte, `WalkabilityMask`,
`IsVerticalOwnedExternally`, `MinMoveDistance`) ; l'interface ne les pose pas, ce qui reste à trancher par E20.e0k ; **l'unité brute de
D-E0A-1 est ajoutée par l'interface**, jamais par le noyau, que le chemin par enregistrement partage sans cette unité) ; `IEntityWorldContext.cs` ; `AlundraWorldProxy.cs` (l'implémentation, **ajoutée en fin de `_spawnedEntities`**, sans case) ; `Alundra.Tests/` ; ADR.

**Hors périmètre.** Case, plafond, ordre (e0a2) ; participation au tick (e0a3) ; branchement du tir ; E17 ; butin ; la série ; la sonde de la première animation (P-A, e0a3). L'interface laisse les champs de l'animation en attente (`CurrentAnimationId = ~0`, comme une entité par enregistrement) : la projection de l'oracle ne compare donc pas encore `Current*` ni `AnimationDirection`.

##### Tâches

- ✅ **T0 — Prédiction et état de départ** (commit de docs, avant tout code). Annexe `docs/plan-e20-annexe/e20-e0a/README.md` : liste des champs comparés (nom DLL ← décalage du binaire, ≈ 28, avec les conversions : `PosZ` et `ModdedPosZ` de la DLL = binaire − 1, ADR-0026), comptes attendus (151, 244, 395 vecteurs ; 123 648 appels de validité, 2 507 vrais, 65 cartes vides, au plus 22 par carte, 244 numéros distincts ; 54 numéros avec tirage ; 7 + 9 sons de création dont 3 à `+0x100`), SHA-1 de `Data/sprite-records.json` (`88d72306…`, 2 571 585 o) et de `Data/map-sprite-indices.json` (`63a3e618…`, 30 913 o) relevés une fois, en fenêtre calme (aucun export ni suite en cours sur l'arbre), comptes de tests (`Alundra.Tests` 4 752, convertisseur 854). Les outils de l'oracle y sont versés avec une somme de contrôle.
- ✅ **T1 — Oracle.** `run_e0a.py` : V1 (151 indices de la page 0, parent héros, plat, direction `0x10`, Z 0), V2 (244 numéros de la page des cartes : une seule image de création par numéro sur toutes les cartes où il existe, carte source notée
pour la trace), V3 géométrie (au moins 40 vecteurs : les 10 types de projectiles du héros et les objets 1 et 2 ; Z ∈ {0, Z du héros (1 brut), 16 px, **−5 px sous le sol**, **au-dessus du sol pour un `OffsetZ ≠ 0` : page 0 n° 20 à 50 px, n° 64 à 30 px, n° 242, page des cartes n° 153 à 40 px**, terrain relevé de 32 px autour du point} ; directions 0, 8, `0x10`, `0x18` ; parents {aucun, héros, une entité apparue}), V4 (483 cartes × 256 indices et les sondes −1, 256, 300, `0xFFFFFFFF`, `0x80000000`), V5′ (table pleine : 63 apparitions puis une 64e,
pour que les mutants du plafond et du test d'occupation mordent ; la DLL n'en a l'équivalent qu'en e0a2), V6′ (la liste des numéros de
la page des cartes valides sur la **carte 14**, pour le chemin complet de l'interface), V7 (les 8 appels d'apparition journalisés par les traces AT9 : types 3, 4, 11 et 14, au sol et en l'air, `docs/plan-e20-annexe/e20-b/oracle/out/at9/`, lus par `AlundraHeroAttackTests.cs` vers `:356-363`, `WeaponTraces`). Sorties déterministes : `summary.txt`, `manifest.txt` (md5 de chaque fichier ; deux exécutions identiques), `v1.json`, `v2.json`, `v3.json`, `v4-pairs.txt`, `v7.json`, journal des sons. Seconde interpréteuse (`mips.py`) identique sur au moins 159 appels. Durée visée : moins de 2 minutes.
- ✅ **T2 — Mutants de l'oracle** (`mutations.txt`). Au moins 23 mutants d'un mot, tous CAUGHT : plafond 64 et 62, test d'occupation inversé, racine, série, `+0x48` (vecteur de **case sale** requis), `Z − ModZ`, `+1`, relèvement du terrain (magasin supprimé et comparaison inversée), `+0x100` de la page, sélection de page inversée, direction (cible et complément), parent, compteur de cases, palette, feuille, `+0x230`, tirage du butin, premier changement d'animation, son, PV, drapeaux. Un mutant qui survit est un vecteur manquant, pas un arrêt.
- ✅ **T3 — Tests rouges** (`Alundra.Tests`, projet inchangé hors ajouts) : (a) donnée : les 395 `BankKey` donnent un préfab du catalogue, uniques ; la validité de la page des cartes des 483 cartes égale `v4-pairs.txt` (123 648 comparaisons) ; (b) projection de création des **395 vecteurs au niveau du noyau sans enregistrement** (sans monde : champ de collision plat ou relevé
comme dans V3 ; fiche d'équilibre posée par `InitializeBalance(tables, 0)`, seuil 0 comme l'oracle, `espawn.py`), égale à l'oracle (hors `+0x3C`, `+0x40`, générateur pour les 54, D-E0A-3 ; hors `Current*` et `AnimationDirection`) ;
(b′) **chemin complet de l'interface** dans un `ArcRun` de la carte 14 : les 151 numéros de la page globale (valides sur toute carte) et
les numéros de la page des cartes valides sur la 14 (liste V6′, compte écrit dans l'annexe en T1 ; l'entrée `"14"` du fichier vaut
[79, 136, 144, 153, 199]) : même projection que (b) **hors les champs qui dépendent du terrain** (`PosZ`, `ModdedPosZ`, hauteur du
terrain, hauteurs de coin `MapHeights`, `FloorHeight`), qui restent prouvés par (b) sur le champ de l'oracle ; plus l'ajout à
`_spawnedEntities` et l'unité brute de D-E0A-1 (un appel avec le Z du héros donne `PosZ` = Z du héros + 1 unité − `ModZ`, avant relevé) ; (c) contrat `null` : indice négatif, 256, entrée nulle, carte inconnue du fichier, `parent` nul accepté, préfab absent : aucune entité ajoutée, une ligne de journal ; (d) `EntityRefId == −1` et `0x2C [0]`, `0x2E [0]` ne la trouvent pas ; `EventTrigger == −1` ; (e) parent et racine (parent héros, enfant d'un enfant, parent nul : 4 cas) ; (f) règle de Z (au moins 8 vecteurs, dont ceux du T1) ; (g) son de création, au niveau du noyau : n° 7 → `0x21`, n° 27 → `0x1E`, n° 3 rien, n° 192 de la page des cartes → son + `0x100` ; (b bis) la case et la série ne sont pas encore écrites (e0a2), elles sont exclues de la projection ; (h) équivalence avec l'enregistrement : le n° 79 **de la page des cartes** par l'interface et l'enregistrement 0 de la carte 14 par le chemin de production ne diffèrent que par la liste fermée (case, série, racine, parent, état, `+0x3C`, pointeur d'enregistrement, `EntityRefId`, soi).
- ✅ **T4 — Code.** Catalogue (`BankKey` lu ; absent = tolérant), chargeur des indices (fichier absent : permissif, un avertissement), extraction de la fabrique, interface et implémentation (`LoadCopy`, noyau sans enregistrement, `InitializeBalance`, `AddEntity`, ajout en fin de liste, `RequestAppearanceSound`, `PushLogicalPositionToRoot`, racine dans `ChildEntity`). `dotnet test Alundra.Tests --blame-hang-timeout 60s`, au premier plan, en Release puis en Debug.
- 🧪 **T5 — ADR** (cadre de Z, interface, racine, validité par carte), plan, verifier frais. ADR-0063 (`docs/decisions/`) et le plan sont faits ; reste le verifier frais, lancé par la session principale.

##### Acceptation

- Oracle : V1 151/151, V2 244/244, V5′ (63 acceptées, la 64e refusée), V4 2 507 paires, 65 cartes vides, mêmes jeux que `Data/map-sprite-indices.json` (SHA-1 `63a3e618…`), V7 8/8 ; sondes invalides : un seul message en `0x80039BB8`, série, compteur et générateur inchangés ; ≥ 23 mutants CAUGHT ; deux exécutions identiques au manifeste ; seconde interpréteuse identique.
- DLL : tous les tests de T3 verts (395 projections au niveau du noyau ; chemin complet sur la carte 14 : 151 + le compte V6′) ; `Alundra.Tests` en Release et en Debug : 4 752 + les nouveaux, **zéro échec, aucun test existant modifié** (`git diff --stat Alundra.Tests/` : ajouts seulement) ; convertisseur et moteur intacts ; `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique ; traces inchangées (`git diff --ignore-cr-at-eol --exit-code`).
- Verifier frais (rejeu de l'oracle et lecture adversariale du diff).

##### Résultats (2026-10-15, branche `chantier/e20-heros`, base `1ccc3c3`)

Annexe : `docs/plan-e20-annexe/e20-e0a/` (README, `oracle/`, `oracle/out/`). Aucun arrêt n'a joué ; aucune image de l'oracle ne contredit un fait de la section.
- **Oracle** (`run_e0a.py`, 41 s, trois exécutions au manifeste identique) : V1 151/151 ; V2 244/244 (une seule image de création par numéro sur toutes les cartes où il est valide, carte source = la plus basse) ; V3 109 vecteurs de géométrie
  (12 sprites × Z {0, héros = 1 brut, 16 px, −5 px sous le sol}, les quatre directions, les trois parents, `OffsetZ ≠ 0` au-dessus du sol : page 0 n° 20, 64, 242, page des cartes n° 153, 251, 194, 199, 223, 142, 215, 84, 108, 47, terrain relevé de 32 px) ;
  V4 123 648 appels → **2 507 paires valides**, 65 cartes vides, au plus 22 par carte, 244 numéros, jeux égaux à `Data/map-sprite-indices.json` (SHA-1 `63a3e618…`, 30 913 o) ; V5′ 63 apparitions acceptées puis la 64e et la 65e refusées (un message chacune en
  `0x80039B10`, série, compteur de cases et générateur inchangés), 9, 21, 40 repris dans l'ordre, case sale (`+0x44`, `+0x48` réécrits) ; 13 sondes invalides (entrée −1, 0 et `0x1000`, 256, 300, −1, `0x80000000`, numéro absent de la carte) : retour 0,
  un seul message en `0x80039BB8`, série, compteur et générateur inchangés ; **V6′ = 5 numéros** sur la carte 14 (`[79, 136, 144, 153, 199]`) ; V7 8/8 (types 3, 4, 11, 14) ; tirage du butin à la création sur 54 numéros (6 + 48) ; son de création
  sur 7 + 9 numéros dont 3 à `+0x100` (192, 194, 249) ; seconde interpréteuse (`mips.py`) identique sur 159 appels ; couverture : `SpawnWarpEntity` 60/60, recherche de page 42/42, `FindFreeEntitySlot` 22/22, `InitializeEntity` 146/149 (les 3 mots
  non exécutés, `0x80039D74-0x80039D7C`, sont la moitié « avec enregistrement », inatteignable avec `a3 = 0`).
- **Mutants de l'oracle** (`mutants.py`, 6 min) : 38 mutants d'un mot, 38 CAUGHT. Un premier passage en laissait survivre un (l'entrée non négative de `0x80039B8C`) : vecteurs ajoutés (entrées 0 et `0x1000`, pointeur valide à 256, 300 et −1)
  puis 38/38 ; l'ensemble qui ne mordait pas sur `Z − ModZ` est mordu par les préfabs `OffsetZ ≠ 0` au-dessus du sol, la case sale mord sur `+0x48 = −1`.
- **Tests** : `Alundra.Tests` 4 752 → **4 775** (+23) en Release et en Debug, zéro échec, aucun test existant modifié (`git diff --stat 1ccc3c3..HEAD -- Alundra.Tests/` : 3 fichiers ajoutés). T3 rouge avant T4 : 18 des 21 premiers tests en échec avec des
  squelettes qui ne font que compiler (commit `73d1f45`). 12 mutants du code de la DLL tous CAUGHT (Z sans l'unité, `EntityRefId` à 0, racine = parent, `+0x100` oublié, `EventTrigger`, pas de champ de collision, parent non écrit, carte ignorée, pages de la clé
  échangées, fiche d'équilibre, ajout à la liste, son : ce dernier survivait, d'où le test G de l'interface). `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique ; les cinq traces inchangées ; convertisseur (854 tests),
  moteur et `data-extracted/` sans diff.
- **DLL** : 395 projections au niveau du noyau (151 + 244, hors champs exclus), 109 de géométrie et les 8 appels AT9 égaux à l'oracle (une trentaine de champs, dont l'attaque courante et l'octet d'animation de la fiche d'équilibre) ; chemin complet sur la carte 14 : 151 + 5
  appels, même projection hors `PosZ`, `ModdedPosZ`, terrain, sol et hauteurs de coin ; l'unité brute de D-E0A-1 pincée (Z du héros → `PosZ = z + 1 − ModZ`, AT9 : 1 048 577 dans le binaire, 1 048 576 pour le stub) ; contrat `null` (négatif, 256, sans clé, numéro absent
  de la carte, carte inconnue du fichier, préfab illisible) : rien d'ajouté, une ligne de journal ; `EntityRefId = −1` : `0x2C [0]` et `0x2E [0]` ne le trouvent pas ; parent et racine (4 cas) ; le n° 79 de la page des cartes par l'interface égale l'enregistrement 0 de la
  carte 14 par le chemin de production, hors la liste fermée (`EntityRefId`, hauteur de l'enregistrement, parent et racine, soi) ; lecture paresseuse du fichier (rien au chargement d'un monde).
- **Écarts et décisions de l'exécution** : (1) palette et feuille de la page (`0xB`/`0x60`, `0`/`0x20`) ne sont pas stockées dans la DLL (aucun lecteur) ; l'oracle les fixe et vérifie par page ; (2) `TileZ` n'est pas comparé (la DLL laisse ce que laisse le chemin par
  enregistrement, la physique le réécrit à chaque tick), `TileX` et `TileY` le sont ; (3) l'attaque courante (`+0x1C8`) et l'octet d'animation (`+0xB4`) sont comparés en plus de la liste du plan ; (4) la clé `BankKey` de la page des cartes est le numéro nu (`"79"`),
  celle de la page globale `alundra_N` ; (5) `mutations.txt` est hors du manifeste de l'oracle (fichier d'un autre script, 6 min) ; (6) `x` et `y` de l'interface sont les entiers 16.16 bruts (comme `PosX`/`PosY`), `z` dans le repère de la DLL ; (7) le noyau clone le préfab
  déjà lu par `LoadCopy` (comme le chemin par enregistrement), un coût mesuré en E20.f, pas ici.
- **Hors tranche, signalé** : le parent d'une apparition est `parent.LogicContextEntity` (comme `SpawnEntityByRecordId`) : un proxy sans `LogicContextEntity` (un pion de test nu) donne un parent nul et une racine nulle.

##### Arrêts, budget, retour arrière

- **Arrêts** : une image de l'oracle qui contredit un fait de cette section (attribuer, puis question) ; un test existant qui doit changer ; un SHA-1 de `sprite-records.json` ou de `map-sprite-indices.json` différent de celui d'E20.e0x (ne pas ré-exporter : question) ; la fabrique extraite qui déplace une épingle d'apparition par enregistrement (revenir à `ApplySpawnInitialization` d'origine, intacte) ; tout contact avec `CasaEngineMonogame/` ou `CasaEngine.Launcher/Program.cs`.
- **Budget** : un `executor` par ensemble de tâches, deux tentatives au plus ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : abandon de la branche pour la DLL, `git revert` pour les docs et l'oracle.

---

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre). Rejeu : oracle relancé (tout PASS,
manifeste identique, aucun fichier versé changé), `Alundra.Tests` 4 775/4 775 en Release et en Debug, `cmp` de la DLL, traces ; aucun test
existant modifié (`git diff --stat 1ccc3c3..026932a -- Alundra.Tests/` : ajouts seulement) ; aucun appelant de production de
`SpawnEntityBySpriteIndex`. Adversarial : extraction de la fabrique relue ligne à ligne (chemin par enregistrement inchangé), chargeur
paresseux, carte courante par le nom du monde, cas limites de page et d'indice, unité brute dans l'interface seule. Remarques P4,
dispositions de la session principale :
- si une exception survient après `_world.AddEntity` (son de création, poussée de position), l'interface rend `null` alors que l'entité est
  déjà ajoutée : **reporté à E20.e0a2**, qui retouche les chemins de création (le contrat « `null`, rien d'ajouté » doit tenir aussi sur
  ce chemin d'erreur) ;
- la racine `ChildEntity` d'une entité dont le parent vient du chemin par enregistrement (`0x2D` avec parent) diffère du binaire, ce
  chemin n'écrivant pas `+0x08` : **reporté à E20.e0a2** (écrire `ChildEntity` aussi sur ce chemin, sans lecteur connu : aucun effet
  visible attendu) ;
- commentaire devenu inexact dans `ApplyHeaderInitialization` (« from the catalog entry already fetched above ») : **reporté à E20.e0a2** ;
- assertion tautologique dans le test F du noyau (`dllZ = v.Z - 1` puis `Assert.Equal(v.Z, dllZ + 1)`) : **notée** (l'unité de D-E0A-1
  est épinglée par le test de l'interface et par la trace AT9) ;
- bruit de fins de ligne des traces après chaque suite : **noté** (connu).

#### 1.16.2 E20.e0a2 — Table de cases : première libre, plafond de 63, ordre des passes ✅ (faite et CONFIRMED le 2026-10-15, `03c3863` à `c5eba5d`, ADR-0064)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, deux points corrigés (FIX) : la phase d'ombre a un prédicat exact, un canal de relevé
par test, une commande et un fichier versé, et le plafond est placé dans la phase B (l'ombre ne refuse rien) ; les trois points reportés
d'E20.e0a1 ont leurs tâches, leurs tests et leurs lignes d'acceptation, et la fabrique entre dans le périmètre. Points non bloquants repris :
règle d'insertion autour des entrées sans case, commentaires « creation order » à mettre à jour, ADR-0064, lignes de V5 à ajouter.
Seconde relecture : **READY** (2026-10-15).

**Prérequis.** E20.e0a1 ✅ (l'interface, l'oracle et son annexe, le compte de tests : 4 775).

**Reporté d'E20.e0a1 (vérification du 2026-10-15)**, porté par T2 et T3 ci-dessous : le contrat « `null`, rien d'ajouté » sur le chemin
d'une exception après `_world.AddEntity` ; `ChildEntity` écrit aussi par le chemin par enregistrement (racine du binaire `+0x08`) ; le
commentaire inexact d'`ApplyHeaderInitialization`.

**But.** Donner à chaque entité une case (héros 0, autres 1 à 63), la rendre au recyclage, refuser la 64e, et faire suivre l'ordre de
`_spawnedEntities` à celui des cases, comme les passes du binaire (événements, compteurs, listes, physique, « premier attaquant » des coups,
recherches `0x2C`). Ferme O-E20-13.

**Faits propres à la tranche.**
- [DLL] Toutes les passes de la DLL parcourent `_spawnedEntities` ou une liste qui en dérive (`CollectTickEntities`, `_pendingScan`,
  `RunCountersPass`, `BuildHitList`, `RefreshUpdateProxiesAndCollidables`/`_updateProxies`, `PublishEntityFrame`, `GetSpawnedEntityProxies`) :
  réordonner `_spawnedEntities` suffit, **aucune passe n'est modifiée** ; la liste du moteur (`world.Entities`) garde l'ordre d'insertion,
  ce qui ne compte que pour les égalités de dessin. Les commentaires « creation order » de ces passes sont mis à jour.
- [DLL] Les trois chemins qui ajoutent puis appellent le son et la poussée de position (`SpawnEntityBySpriteIndex`,
  `SpawnEntityByRecordId`, la boucle de chargement) le font dans un `try` après `_world.AddEntity` : une exception laisse l'entité dans le
  monde et dans la liste.
- [DLL] Les suites de tests tournent sans parallélisme (`parallelizeTestCollections: false`) ; une apparition se fait toujours dans la
  pile d'appels du test (ArcRun → `World.Update` → la passe), ce qui permet d'attribuer un événement à son test par la pile.

**Périmètre.** `AlundraEntityScriptProxy` (`Slot = −1`, non cloné) ; `AlundraWorldProxy` (masque de 64 bits par instance, la case 0
tenue dès la construction, `AllocateSlot`, `ReleaseSlot` — sans effet pour −1 —, insertion triée, libération dans
`RecycleDestroyedEntities`, contrat d'erreur des trois chemins, relevé d'ombre) ; les **quatre** chemins de création (adoption du héros =
case 0, chargement de carte, `SpawnEntityByRecordId`, `SpawnEntityBySpriteIndex`) ; `AlundraEntitySpawnFactory.cs` (`ChildEntity` sur le
chemin par enregistrement, commentaire d'`ApplyHeaderInitialization`) ; `IEntityWorldContext.SpawnedEntities` (doc : « dans l'ordre des
cases ») ; l'oracle (V5 complété) ; tests ; ADR-0064.

**Hors périmètre.** Aucune passe n'est modifiée. L'alias des pointeurs périmés (D-E0A-7). Le tick de naissance (e0a3).

**Règles de la table.** Case attribuée = la plus basse libre de 1 à 63 ; la case 0 est réservée au héros (même dans un monde sans héros) ;
une entité `FlagToDestroy` garde sa case jusqu'à `RecycleDestroyedEntities` (porte 0x48 fermée : rien n'est libéré). Insertion triée : une
entité de case s est placée juste avant la première entrée de case supérieure à s, en sautant les entrées sans case (−1, ajoutées par
réflexion par les six classes de tests), qui restent où elles sont ; à défaut, en fin de liste.

##### Tâches

- ✅ **T0 — Prédiction par l'ombre** (commit de code A, puis commit de docs du relevé).
  - Phase A (ombre) : cases attribuées, masque tenu, **sans réordonner et sans rien refuser** (une 64e entité reçoit −1 et est ajoutée
    comme aujourd'hui). Deux événements sont relevés :
    - **insertion hors fin** : la case attribuée est inférieure à la plus haute case des entrées vivantes de `_spawnedEntities` (la phase B
      l'aurait placée ailleurs qu'en fin de liste) ;
    - **dépassement** : aucune case libre de 1 à 63 (la phase B refuserait cette apparition).
  - Canal : un puits statique, actif seulement si la variable d'environnement `ALUNDRA_SLOT_SHADOW_LOG` donne un chemin de fichier
    (absent : rien n'est écrit, rien n'est alloué en plus) ; une ligne par événement :
    `<événement> test=<méthode> world=<nom du monde> slot=<s> maxLive=<m>`, la méthode étant le cadre le plus extérieur de la pile dont le
    type est dans l'espace de noms `Alundra.Tests`.
  - Commande : `ALUNDRA_SLOT_SHADOW_LOG=<scratch>/shadow.txt dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build
    --blame-hang-timeout 300s` (une fois, après le build de la phase A ; la suite doit rester verte en A).
  - Fichier versé : `docs/plan-e20-annexe/e20-e0a/e0a2-shadow.txt` (par test : nombre d'insertions hors fin, de dépassements, plus haute
    case vue), écrit **avant** la phase B ; un test absent du fichier ne peut pas bouger en B.
  - Prédiction de la découverte, à confronter au relevé : les arcs qui détruisent (`0x2E`, E0/E1) puis font apparaître (`0x2D`, `0x8A`,
    `0x8B`) : `AlundraDay3SceneArcTests` (A14 : enregistrements 8 à 10 par `0x2D` à 219-223, `0x2E` à 272), `AlundraEntityContactArcTests`,
    `AlundraInoaDayOneArcTests`, `AlundraTarnMurggArcTests`, `AlundraMurutaHallArcTests`.
- ✅ **T1 — Oracle V5 complété** (annexe d'e0a1, `out/v5.json` et son manifeste) : les lignes déjà versées (5 apparitions → cases 1 à 5 ;
  case 2 en état 4, apparition avant la passe → case 6, après → case 2 ; 63 acceptées, 64e et 65e à 0 avec un message en `0x80039B10` ;
  libérations 40, 9, 21 → 9, 21, 40 ; case sale) **plus** deux lignes qui manquent : indice invalide avec table pleine (seul le message
  « Race », `0x800239CC`) et apparition en boucle 2 dans une case recyclée.
- ✅ **T2 — Tests rouges.**
  - Allocateur pur, sans moteur (au moins 12 cas, chacun recopié d'une ligne de V5) ; `ReleaseSlot(−1)` sans effet ; case 0 tenue dans un
    monde sans héros.
  - Le masque libère à `RecycleDestroyedEntities` et pas avant ; porte fermée : rien n'est libéré.
  - Phase B : la 64e apparition rend `null` avec une ligne de journal et sans entité ajoutée, pour les trois chemins qui peuvent échouer
    (chargement : l'enregistrement est sauté et journalisé ; `SpawnEntityByRecordId` et `SpawnEntityBySpriteIndex` : `null`).
  - Ordre de `_spawnedEntities` = ordre des cases ; entrées sans case laissées en place.
  - **Contrat d'erreur (reporté d'e0a1)**, pour chacun des trois chemins : une exception levée après `_world.AddEntity` (son de création
    ou poussée de position, déclenchée par un point d'accroche de test) donne : aucune entité dans `_spawnedEntities` ni dans le monde
    (retirée), la case rendue au masque, `null` pour les deux méthodes, l'enregistrement sauté et journalisé pour le chargement.
  - **`ChildEntity` (reporté d'e0a1)** : une apparition par `0x2D` avec un parent écrit la racine comme le binaire (`parent.ChildEntity ??
    parent`) ; un test parent → enfant → petit-enfant par le chemin par enregistrement, et un mélange enregistrement → interface.
- ✅ **T3 — Code en deux temps.** (A) l'ombre (T0) ; (B) l'insertion triée, le refus de la 64e, le contrat d'erreur, `ChildEntity` sur le
  chemin par enregistrement, le commentaire d'`ApplyHeaderInitialization` et ceux des passes ; suite complète : chaque test dont le
  résultat bouge est dans `e0a2-shadow.txt` et justifié par l'ordre des cases ou par un refus (preuve écrite dans le plan), puis corrigé ;
  rien d'autre ne change.
- 🧪 **T4 — ADR-0064** (table de cases, plafond, ordre, contrat d'erreur), plan : ADR-0064 et le plan sont faits ; reste les verifiers frais ; verifiers frais lancés par la session principale, dont
  un sur les épingles (liste prédite contre liste mesurée).

##### Acceptation

- Oracle V5 : toutes les lignes de `out/v5.json` (y compris les deux ajoutées) reproduites par les tests de l'allocateur ; le manifeste
  de l'oracle mis à jour et identique sur deux exécutions.
- DLL : au moins 12 tests de l'allocateur ; les tests du contrat d'erreur (trois chemins) et de `ChildEntity` verts ; `Alundra.Tests` en
  Release et en Debug : 4 775 plus les nouveaux, aucun échec, et **chaque test dont le résultat bouge est dans `e0a2-shadow.txt` et
  justifié** ; au chargement de carte, la plus haute case vue dans le relevé d'ombre ne dépasse pas 39 pour les cartes chargées par la
  suite (recensement de la découverte : 39 au plus avec la zone d'apparition ; les cartes 75 et 92, qui dépasseraient 62 sans la zone, ne
  sont chargées par aucun test : recherche des noms de monde `-75` et `-92`) ; aucun dépassement dans le relevé, sinon il est expliqué.
- `cmp` de la DLL ; traces inchangées ; verifiers frais.

##### Résultats (2026-10-15, branche `chantier/e20-heros`, base `d9831b2`)

Aucun arrêt n'a joué. Commits : phase A `03c3863` (table, ombre, 16 tests de l'allocateur) ; relevé `b67447c` + `801f572` (`docs/plan-e20-annexe/e20-e0a/e0a2-shadow.txt`, écrit avant tout code de la phase B) ; oracle V5 `8bc0504` ; phase B `5e36903` (insertion triée, plafond, contrat d'erreur, `ChildEntity`, deux tests corrigés, 16 tests) ; ADR-0064 et ce plan.
- **Ombre (phase A).** Suite verte en Release et en Debug sans test modifié (4 775 → 4 791, les 16 ajoutés sont l'allocateur seul), `cmp` de la DLL identique, traces inchangées. Relevé, Release, une fois : 179 tests créent des entités (5 502 attributions), **11 insertions hors fin dans 8 tests**,
  **112 dépassements dans un seul test** (le test B2 de l'interface d'e0a1 : 156 apparitions dans un monde), plus haute case d'un test qui ne fait que charger des cartes et lancer des scripts : **36** (borne de la découverte : 39 ; aucun test ne charge les cartes 75 et 92 : recherche de `-75"` et `-92"` sans résultat dans `Alundra.Tests/`).
  Les 8 tests à insertion hors fin : `AlundraInoaDayOneArcTests.A11` (case 1 sous 12), `AlundraEntityContactArcTests.TA19` (7 sous 10), `.TB9_CounterProof` (21 sous 27), `.TB9_SeptimusAndTheBlock` (21 et 22 sous 27), `AlundraHeightTargetSitesTests.TheWaitOfTheBinary` (7, 8, 9 sous 12),
  et trois adoptions d'un second pion (case 0 sous les cases vivantes) : `AlundraHeroFallAndPadJumpTests.S6c`, `AlundraAnimationSoundTests.T1`, `AlundraAnimationImpulseSpawnTests.UJ7`.
- **Prédit contre mesuré.** La découverte prédisait cinq arcs (Day3 A14, EntityContact, InoaDayOne, TarnMurgg, MurutaHall). Le relevé en confirme deux (EntityContact, InoaDayOne), n'en voit aucun événement pour Day3, TarnMurgg et MurutaHall (la case libérée n'est pas reprise par une apparition avant la fin
  du test), et en ajoute deux familles que la découverte n'avait pas vues : les adoptions d'un second pion (case 0) et `HeightTargetSites`. **Mesuré en phase B : deux tests bougent, tous deux dans le relevé.** Les sept autres tests à insertion hors fin passent sans changement.
  1. `AlundraInoaDayOneArcTests.A11` (relevé : une insertion hors fin, case 1 sous 12). Preuve par l'ordre des cases : la carte 164 détruit l'enregistrement 0 (case 1) ; l'enregistrement 4, né de `0x8B @95`, prend la PLUS BASSE case libre, 1, et passe donc AVANT Septimus (case 2) dans les passes, comme dans le tableau plat du binaire.
     Son programme `C[3]` (`@448`, attente du drapeau T3) tourne avant que `C[2]` de Septimus pose T3 à `@437` dans le même tick ; il sort de l'attente un tick plus tard et désactive l'enregistrement 4 à la trame 2072 au lieu de 2071 (comparaison des traces des trames 2060 et suivantes avec et sans tri : le programme `start448` passe avant `start240` dans chaque trame). Correction : une trame de plus avant `Assert.True(arc.Has(C, 451, 0x19))`
     (la tolérance de `AssertFrame` est de ±3 : la ligne `AssertFrame(arc, C, 451, 2071)` reste vraie), le reste du test, dont les contacts de Septimus, est inchangé.
  2. `AlundraSpawnBySpriteIndexInterfaceTests.B2` (relevé : 112 dépassements). Preuve par un refus : 156 apparitions dans un monde dépassent la table de 63 ; la 64e est maintenant refusée, comme dans le binaire. Correction : chaque vecteur est détruit et recyclé une fois contrôlé (le binaire rend la case d'une entité détruite à la passe suivante), le suivant reprend la même case la plus basse et reste le dernier de la liste ;
     l'assertion « ajoutée en fin de liste » est gardée, et la case est assertée.
- **Oracle V5.** Deux lignes ajoutées à `run_e0a.py` : indice invalide avec table pleine (retour 0, seul le message de la recherche de page `0x80039BB8` / « Race » `0x800239CC`, jamais celui de la table pleine `0x800239B0`, série, compteur et générateur inchangés) et apparition en boucle 2 de la passe des événements dans une case recyclée par la passe des cases du même tick
  (cases 2 et 4 mortes depuis le tick d'avant, libérées par la passe, l'apparition de la boucle 2 prend la case 2, les suivantes 4 puis 6). Tout PASS, deux exécutions au manifeste identique. Les mutants de l'oracle (`mutants.py`, 6 min) n'ont pas été relancés : ils portent sur les mots du binaire et leur signature (`mutants.py`) inclut les nouvelles lignes de V5 sans qu'un mot du binaire change.
- **Tests de la phase B.** 17 tests de l'allocateur au total (une ligne de V5 chacun pour les 6 premiers, puis case 0, `Release(-1)`, hors table, double libération…), 15 tests du monde sur la carte 14 : cases des entités chargées, ordre après recyclage, entrées sans case laissées en place, libération au recyclage (porte fermée : rien n'est libéré), plafond sur les trois chemins, indice invalide avec table pleine,
  chargement avec table presque pleine (deux cases libres : le héros et deux enregistrements, les autres sautés avec une ligne chacun), contrat d'erreur sur les trois chemins (chargement, `SpawnEntityByRecordId`, interface : aucune entité dans la liste, `ToBeRemoved`, case rendue, `null` ou enregistrement sauté et journalisé, la case est reprise par l'apparition suivante), `ChildEntity` sur les deux chemins.
- **Comptes.** `Alundra.Tests` : 4 775 → 4 791 (phase A) → **4 807** (phase B), zéro échec, en Release et en Debug ; `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique ; les cinq traces inchangées (bruit de fins de ligne restauré).
- **Écarts.** (1) Le puits d'ombre écrit aussi une ligne `seen` par attribution (pour la plus haute case) et compte l'adoption du héros comme une attribution de case 0 : d'où les trois adoptions d'un second pion dans la liste hors fin (en phase B elles s'insèrent avant la première entrée de case supérieure à 0, après l'ancien pion). (2) Les deux points d'accroche de test sont des champs statiques `internal` (`CreationFaultHookForTests`, `SlotTableFactoryForTests`), nuls en production, remis à nul par les tests ;
  l'entité mise en file puis retirée par `World.RemoveEntity` reste dans la file d'ajout du moteur jusqu'à l'intégration suivante (`ToBeRemoved` vrai), le test vérifie ce drapeau et l'absence de la liste, pas l'absence de la file du moteur. (3) Le commentaire inexact d'`ApplyHeaderInitialization` est corrigé, `ChildEntity` du chemin par enregistrement passe par `ResolveRootEntity`, partagé avec l'interface. (4) Aucun mutant du code de la DLL n'a été lancé par cette exécution : à la charge des verifiers frais.

##### Arrêts, budget, retour arrière

- **Arrêts** : un déplacement absent du relevé d'ombre ; la porte de `RecycleDestroyedEntities` à changer ; le plafond atteint au
  chargement dans un test (erreur à lire, pas à contourner) ; un test existant à réécrire sans justification du binaire ; l'ordre d'une
  passe à toucher ; la phase A qui fait bouger un test (l'ombre doit être neutre).
- **Budget** : un `executor`, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : les commits de la phase B (insertion triée, refus, contrat d'erreur, `ChildEntity`, avec leurs corrections de tests
  justifiées) se retirent seuls ; ceux de la phase A restent sans changer aucun comportement (cases et masque tenus, rien de réordonné ni
  refusé, puits inactif sans la variable) ; sinon abandon de la branche.

---

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre). Rejeu et épingles : `Alundra.Tests`
4 807/4 807 en Release et en Debug, `cmp`, traces ; les deux tests existants modifiés (`AlundraInoaDayOneArcTests` A11, une désactivation
une image plus tard parce que l'enregistrement 4 prend la case 1 avant Septimus, comme le binaire ; `AlundraSpawnBySpriteIndexInterfaceTests`
B2, 156 apparitions refusées au-delà de 63) figurent dans `e0a2-shadow.txt`, versé avant le premier commit de la phase B ; phase A neutre
(rien de réordonné ni refusé, puits inerte sans la variable) ; oracle stable sur deux exécutions. Adversarial : libération au recyclage
seulement, porte fermée, entrées sans case, `ReleaseSlot(−1)`, monde sans héros, table neuve par monde, chemin d'erreur, refus au
chargement, `ChildEntity` sur le chemin par enregistrement, toutes les passes dans l'ordre des cases : aucun contre-exemple. Remarques,
dispositions de la session principale :
- **P3** : la ligne V5 `invalid_full` (et les lignes `dirty` et `probes`) n'est lue par aucun test de l'allocateur, alors que l'ADR-0064 dit
  que les tests de l'allocateur l'ont recopiée : **reporté à E20.e0a3**, qui ajoute un test qui lit ces lignes dans `out/v5.json` (l'ADR
  devient exacte sans être réécrite).
- P4 : deux commentaires de tests devenus faux (`AlundraSpawnBySpriteIndexInterfaceTests.cs` « appended at the end of the list… no slot
  yet : e0a2 » ; `AlundraInoaDayOneArcTests.cs` « at the frame of the end… ») : **reportés à E20.e0a3**.
- P4 : une entité dont la création a échoué reste dans la file d'ajout du moteur jusqu'à l'`Update` suivant, où elle est intégrée puis
  retirée dans la même passe ; un son déjà demandé avant une faute de la poussée de position part quand même : **notés** (sans effet
  observable dans la logique ; le son ne concerne qu'un chemin d'erreur).

#### 1.16.3 E20.e0a3 — Participation au tick de naissance : e0a3a (sonde P-A, oracle V6, reports), e0a3b (le code, puis P-B) ✅ (les deux CONFIRMED le 2026-10-15)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, quatre points corrigés (FIX) par un découpage : la conception du code dépend de la
sonde P-A, donc **e0a3a** fait P-A, l'oracle V6 et les points reportés d'e0a2, sans changement de comportement en production, et s'arrête
une fois le résultat de P-A versé ; **e0a3b** (le code) est détaillée après P-A et relue avant tout code ; la mesure P-B se fait dans
e0a3b, **après** le code, avec le vrai marqueur, sur un arbre propre, et sa liste a un endroit et un format ; la source de V6 est
désignée (`p3_tick.py`, préfixe `BT-` pour éviter la collision avec les scénarios H d'E20.c de `oracle/tools/scen.py`) et chaque ligne a
son écart attendu ; les reports d'e0a2 ont leurs tâches et l'acceptation permet seulement ces deux commentaires dans les tests
existants. Points non bloquants repris : ADR-0065, numéros de ligne relevés au HEAD, compte de départ, `cmp` et traces, motif du point
d'accroche (`[ThreadStatic]`, comme `WeaponSpawnCallsForTests`), `EventTrigger = −1` déjà posé par e0a1 (retiré du périmètre).
Seconde relecture (e0a3a) : **READY** (2026-10-15).

**But commun.** Une entité née dans `MovePlayer` (tir, usage d'un objet) tourne dans le tick qui la voit naître : statut, programme A,
compteurs, listes, horloge, physique, coups ; une entité née dans un programme natif d'une autre entité tourne au tick suivant (D-E0A-6,
D-E20-34). Elle est dessinée à l'image suivante (D-E20-35). Les entités par enregistrement ne changent pas (D-E0A-5, Q-A1). Ferme O-E20-11
pour les entités de l'interface.

##### 1.16.3a E20.e0a3a — Sonde P-A, oracle V6, reports d'e0a2 (sans changement de comportement en production) ✅ (faite et CONFIRMED le 2026-10-15, `3afbba7` à `89fb2aa` ; voie de P-A : (i) restreinte à `Current = Target`)

**Prérequis.** E20.e0a2 ✅ (la table de cases ; `Alundra.Tests` 4 807).

**Reporté d'E20.e0a2 (vérification du 2026-10-15)**, porté par T3 ci-dessous : un test de l'allocateur qui lit les lignes `invalid_full`,
`dirty` et `probes` de `docs/plan-e20-annexe/e20-e0a/oracle/out/v5.json` (ce que l'ADR-0064 affirme déjà) ; les commentaires devenus faux
de `AlundraSpawnBySpriteIndexInterfaceTests.cs` (« appended at the end of the list… no slot yet: e0a2 ») et `AlundraInoaDayOneArcTests.cs`
(« at the frame of the end… »).

**Périmètre.** L'annexe `docs/plan-e20-annexe/e20-e0a/` (oracle V6) ; un fichier de tests nouveau pour la sonde P-A ; le test de lecture
de `v5.json` ; les deux commentaires ; ce plan. **Aucun fichier de `Alundra/Scripts/` ne change.**

##### Tâches

- ✅ **T1 — Oracle V6.** Verser `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0a/bin/p3_tick.py` (les quatre copies du scratchpad sont identiques,
  md5 `927741c62e0d0b90efc9c9c3bce6ab89`) dans `docs/plan-e20-annexe/e20-e0a/oracle/`, avec sa somme de contrôle dans `checksums.txt` et ses
  dépendances déjà versées ; le relancer par le lanceur de l'oracle ; sortie `out/v6.json` (lignes `BT-M1` à `BT-M3`, `BT-H1` à `BT-H6`,
  `BT-L1` à `BT-L3` ; pour chacune, par passe : statut, déclencheur `+0x228`, exécutions de A, `+0x1C`, présence dans les listes, X, et le
  tick où chaque chose arrive) ; entrée au manifeste ; deux exécutions, même manifeste.
- ✅ **T2 — Sonde P-A** (un fichier de tests nouveau, qui ne fait que mesurer : il n'affirme que ce qui est vrai aujourd'hui, et ses
  mesures sont versées dans le plan) :
  - (1) `AnimatedSpriteComponent.SetCurrentAnimation(forceReset: true)` appelé sur une entité apparue par l'interface, **avant** son
    intégration par le moteur : sans exception, et état du sprite relevé (animation, image, compteurs) ;
  - (2) `Controller.Move` avant l'intégration (`Entity.World` nul) : déplacement ajouté sans test (lu dans le moteur), mesuré ;
  - (3) la cinématique générique de la DLL, pour le numéro 3 de la page globale en direction `0x18` : X après le tick de naissance et
    après le tick suivant, avec `CurrentAnimationId` tel qu'il est (~0) et avec `Current = Target` posé à la main ;
  - résultat versé dans une sous-section « Résultat de P-A » ci-dessous, qui dit quelle voie retenir : (i) poser `Current = Target` et
    l'animation du sprite à la création, **dans l'interface seule** ; (ii) laisser ~0 et faire lire la cible au pas de mouvement pour les
    entités marquées seulement (`AlundraScriptedMotion.cs`) ; ou aucune des deux (arrêt).
- ✅ **T3 — Reports d'e0a2.** Test qui lit `invalid_full`, `dirty` et `probes` dans `out/v5.json` et les rejoue sur l'allocateur (ou sur
  le monde pour `invalid_full`) ; les deux commentaires corrigés.
- ✅ **T4 — Plan** (résultat de P-A, résultats, icônes). Puis **arrêt** : la session principale détaille e0a3b selon P-A et la fait
  relire avant tout code.

##### Résultats (2026-10-15, base `58c9ef1`)

- **T1 — Oracle V6** (`3afbba7`). `p3_tick.py` versé tel quel (md5 `927741c62e0d0b90efc9c9c3bce6ab89`, SHA-1 dans `checksums.txt`) dans `oracle/` ; le lanceur
  `run_e0a.py` l'appelle par `oracle/v6.py` (qui enregistre, par tick et par passe, ce que le code réel écrit : statut, déclencheur `+0x228`, exécutions de A, `+0x1C`,
  `+0xA8`, X, présence dans les listes de mise à jour, de collisions et de dessin, mot de compte des cases) et écrit `oracle/out/v6.json` (douze lignes, md5
  `ecdbd7c059d621b808fff8674d85975e`). `run_e0a.py` : TOUT PASS (42 s), deux exécutions au manifeste identique, le md5 de chaque autre fichier de `out/` inchangé. La colonne
  « écart attendu » a été écrite dans `docs/plan-e20-annexe/e20-e0a/README.md` **avant** la première exécution. Les douze lignes la confirment, sans exception :

  | Ligne | case | A au tick 1 / au tick 2 | statut 2 | dans la liste de mise à jour | X après les ticks 1 / 2 | Mesuré contre attendu |
  |---|---|---|---|---|---|---|
  | `BT-M1`, `BT-M2`, `BT-M3` | 1, 1, 2 | 1 / 0 | tick 1, passe des événements | tick 1, passe des listes | 191 / 191 | participation complète, comme attendu |
  | `BT-H1`, `BT-H4`, `BT-H6` | 2 | 1 / 1 | tick 2 | tick 2 | 190 / 191 | A deux fois (statut 1 et hors des listes pendant le tick 1), comme attendu : écart D-E20-34 |
  | `BT-H2`, `BT-H3`, `BT-H5` | 2, 1, 2 | 0 / 1 | tick 2 | tick 2 | 190 / 191 | tout reporté d'un tick, comme attendu |
  | `BT-L2` | 1 | 0 / 1 | tick 2 | tick 2 | 120 / 120 | tout reporté d'un tick, comme attendu |
  | `BT-L1`, `BT-L3` | 2, 2 | 1 / 0 | tick 1 | tick 1 | 120 / 120 | participation, relève d'e0b et e0c, comme attendu |

  Dans toutes les lignes `+0x1C` passe à 1 à la passe des compteurs du tick 1 (y compris les lignes reportées, dont l'entité est en état 1) et la liste de dessin suit la liste de mise à jour.
- **T2 — Sonde P-A** (`bc2e17e`, `Alundra.Tests/AlundraBirthTickProbePaTests.cs`, 5 tests, tous verts, qui n'affirment que ce qui est vrai aujourd'hui et écrivent leurs mesures
  dans la sortie avec le préfixe `PA:`) : voir « Résultat de P-A » ci-dessous.
- **T3 — Reports d'e0a2** (`9d54f0e`, `Alundra.Tests/AlundraEntitySlotV5RowsTests.cs`, 3 tests) : `invalid_full` (indice 18 de la page globale, table pleine : `null`, une seule ligne de
  journal qui n'est pas celle de la table pleine, rien d'ajouté ni de pris), `dirty` (l'allocateur donne la case 1 puis la reprend après libération ; sur le monde, une case recyclée
  d'une entité salie — `EntityRefId` 7, PV 9 — donne une entité dont `EntityRefId` vaut −1 et les PV ceux de la création) et `probes` (les 13 indices invalides de l'oracle,
  vérifiés invalides pour le catalogue de la DLL et la carte 14, chacun rend `null` avec une ligne de journal et ne prend rien). Les deux commentaires devenus faux sont corrigés
  (`AlundraSpawnBySpriteIndexInterfaceTests.cs` : « last of the list: the list follows the order of the slots… » ; `AlundraInoaDayOneArcTests.cs` : « one frame after the end… ») : ce sont
  les deux seules lignes modifiées des tests existants (`git diff --numstat` : 1/1 et 1/1).
- **Comptes.** `Alundra.Tests` : 4 807 → **4 815** (5 + 3 nouveaux), zéro échec, en Release **et** en Debug. `git diff 58c9ef1..HEAD -- Alundra/Scripts/` vide ; les cinq traces inchangées
  (bruit de fins de ligne restauré, jamais indexé).
- **Écart de procédure : `cmp` de la DLL.** La construction Debug ne peut pas déployer `alundra-project/Alundra.dll` : le fichier est verrouillé par `CasaEngine.Launcher.exe` (PID 28772, lancé
  par l'auteur) et Visual Studio (`MSB3027`, dix essais). Le Debug a donc été construit avec la cible de copie neutralisée par un fichier de scratch
  (`dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug -p:CustomAfterMicrosoftCommonTargets=<scratch>/skipcopy.targets`), puis testé (4 815). Le `cmp` demandé (Debug contre projet)
  n'est pas possible tant que le Launcher tourne : le DLL du projet est celui de la dernière construction Release (identique au `cmp` à `Alundra/bin/Release`), et aucun fichier de
  `Alundra/` n'a changé depuis la base, donc le code compilé est le même. À refaire par le vérificateur si le Launcher est fermé.

##### Résultat de P-A (2026-10-15)

Flèche (numéro 3 de la page globale), direction `0x18`, créée par l'interface sur la carte 14 avant que le moteur l'intègre (`Entity.World` nul).

1. **Sprite avant l'intégration.** `AnimatedSpriteComponent.Animations` est **vide** (0) et `CurrentAnimation` nul : le composant cloné du préfab n'a pas encore ses animations (le moteur les construit à
   `InitializeWithWorld`, `AnimatedSpriteComponent.cs:171-209`, qui vide la liste, remet `CurrentAnimation` à nul puis appelle `SetCurrentAnimation(0, true)`). `SetCurrentAnimation(forceReset: true)` n'a donc rien
   à choisir : la recherche par suffixe ne trouve rien, `SetCurrentAnimation(0, true)` lève `ArgumentOutOfRangeException`, `SetCurrentAnimation(nom, true)` rend `false` sans exception ; aucun état du sprite ne
   change. Même appelé, il serait effacé par l'intégration. Après **une** image du moteur : monde posé, 8 animations, animation courante `bankalundra_3_anim0_right`, `CurrentAnimationId` 0,
   `AnimationDirection` 3 : le premier `SyncAnimation` de l'image qui suit la naissance pose le sprite (Q-A3, D-E20-35). Avec `Current = Target` posé à la main, l'état après une image est **identique** : un
   changement reste en attente parce que `AnimationDirection` (0) n'est pas celui que la table dérive (3).
2. **`Controller.Move` avant l'intégration.** Sans exception ; `Move((5, 0, 0))` rend `(5, 0, 0)` et la racine bouge de `(5, 0, 0)` : le déplacement est ajouté sans test (lu dans le moteur,
   `CharacterControllerComponent.cs:1050-1056`, confirmé). Après l'intégration le même appel rend et déplace aussi `(5, 0, 0)` à ce point de la carte 14 (rien dans le passage).
3. **Cinématique générique** (`AlundraScriptedMotion.TickScriptedNpc`, trois ticks de suite, avant toute intégration, X de départ 150 px, jeu d'animations : animation 0, vitesse 512) :
   - `CurrentAnimationId` tel que l'interface le laisse (~0, pas d'entrée dans le jeu d'animations) : vitesse 0, X = **150, 150, 150**.
   - `Current = Target` posé à la main (0) : la force monte à 3 px puis 6 px, X = **153, 159, 165** (naissance : 3 px, puis 6 px par tick), sans exception.

**Voie retenue : (i), restreinte à `Current = Target`.** Dans `CreateEntityFromSpriteIndex` seulement (après `ApplyHeaderInitialization`, donc jamais dans le noyau partagé avec le chemin par enregistrement),
poser `CurrentAnimationId = TargetAnimationId` et `CurrentDirection = TargetDirection`, **sans** toucher `AnimationDirection` ni le sprite. Raison : la moitié « animation du sprite » de (i) est
impossible (rien à choisir avant l'intégration) et inutile (le premier `SyncAnimation` pose le sprite à l'image suivante, état identique à celui d'aujourd'hui, mesure 1) ; la partie qui reste donne
exactement les 3 px puis 6 px du binaire pour une flèche (mesure 3) ; elle n'a pas besoin de modifier `AlundraScriptedMotion.cs`, partagé avec le chemin par enregistrement, ce que (ii) exigerait. Pas
d'arrêt : ni la sonde du sprite ni celle du contrôleur ne montrent d'instabilité (aucun appel à faire, aucune exception dans le flux réel), et (i) est praticable.
À vérifier par e0a3b avant d'écrire le code (rien de cela n'est mesuré ici) : le jeu d'animations et `AnimFlags` sont lus au `SyncAnimation` (donc `AnimFlags` reste 0 jusqu'à l'image qui suit, alors que le
binaire pose le sien à la première bascule de la création) ; ce que fait `Current = Target` à `SpawnAnimationActive`, à l'horloge de bascule d'animation (`AnimationSwitchTicksReserved`) et aux épingles d'e0a1 qui
ne comparent pas `CurrentAnimationId` (`AlundraSpawnBySpriteIndexInterfaceTests`) ; l'oracle du binaire pour X de la flèche au tick de naissance (la sonde V6 règle X de la ligne par un
`ForceX` posé à la main, elle ne le donne pas).

##### Acceptation

- V6 : les douze lignes `BT-*` dans `out/v6.json`, manifeste stable sur deux exécutions ; la colonne « écart attendu » écrite à l'avance
  dans l'annexe : `BT-M1` à `BT-M3` (naissance dans `MovePlayer`) : participation complète au tick de naissance, la DLL doit l'égaler ;
  `BT-H1`, `BT-H4`, `BT-H6` (boucle 2, case au-dessus du compteur, déclencheur 0) : A tourne deux fois dans le binaire, une fois au tick
  suivant dans la DLL (écart D-E20-34) ; `BT-H2`, `BT-H3`, `BT-H5` (déclencheur −1) et `BT-L2` (boucle 1, case plus basse) : tout reporté
  d'un tick dans le binaire comme dans la DLL ; `BT-L1`, `BT-L3` (boucle 1, case plus haute, butin à la mort) : participation dans le
  binaire, relève d'e0b et e0c (hors e0a3).
- P-A : les trois mesures versées, la voie retenue et sa raison.
- Reports : le test de `v5.json` vert, les trois lignes lues et vérifiées.
- `Alundra.Tests` en Release et en Debug : 4 807 plus les nouveaux, aucun échec ; **aucune assertion existante modifiée**, seuls les deux
  commentaires nommés changent dans les tests existants ; aucun fichier de `Alundra/Scripts/` modifié ; `cmp` de la DLL ; traces
  inchangées.

##### Arrêts, budget, retour arrière

- **Arrêts** : P-A qui montre `SetCurrentAnimation` instable avant l'intégration et aucune des deux voies praticable ; une ligne V6 qui
  contredit l'écart attendu ; un besoin de changer le moteur ; un fichier de `Alundra/Scripts/` à toucher.
- **Budget** : un `executor`, deux tentatives ; `plan-verifier` avant ; un verifier frais après.
- **Retour arrière** : `git revert` (docs et tests seulement).

**Vérification (2026-10-15) : CONFIRMED** par un verifier frais : oracle relancé deux fois (manifeste stable, `out/v6.json` et ses douze
lignes conformes à l'écart attendu), sonde P-A relue et ses mesures confirmées, test de `v5.json` qui mordrait si les lignes changeaient,
tests existants changés seulement par les deux commentaires, `git diff 58c9ef1..89fb2aa -- Alundra/Scripts/` vide, `Alundra.Tests` 4 815
en Release et en Debug. Le `cmp` de la DLL n'a pas pu être fait : `alundra-project/Alundra.dll` était verrouillé par le Launcher de
l'auteur et par Visual Studio (le build Debug a été fait avec la copie neutralisée par une cible du scratchpad) ; sans effet ici, aucun
fichier de `Alundra/` n'ayant changé. P4 notés : la colonne « écart attendu » est arrivée dans le même commit que `v6.json` (l'ordre
n'est attesté que par le rapport) ; bruit de fins de ligne des traces.

##### 1.16.3b E20.e0a3b — Le code du tick de naissance, puis la mesure P-B (détaillée le 2026-10-15 après P-A) ✅ (faite et CONFIRMED le 2026-10-15, `681c87b` à `5d438ff`, ADR-0065)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, trois points corrigés (FIX) : la construction de P-B neutralise toujours la copie de
la DLL vers `alundra-project/` et le `cmp` se fait après la remise en état, contre le code commité ; le marqueur est posé à la fin de
`SpawnEntityBySpriteIndex`, après la poussée de position (aucun relèvement de repli pendant la création) ; chaque issue de T0 a sa suite
(arrêt et nouvelle relecture pour tout code hors des décisions ou tout écart sur une entité de l'interface). Points non bloquants
repris : implémentation explicite du champ de collision par le monde, nom aligné (`CollisionField`, au lieu du `CollisionFieldNow` de
D-E0A-5), déclencheur `+0x228` dans T1, mécanisme du rassemblement nommé, numéros de ligne relevés au HEAD `c4247cf`.
**Révision 2 (2026-10-15).** Seconde relecture : **REVISE**, un blocage ; dispositions de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — la décision `Current = Target` change de façon prévisible deux tests existants, nommés ci-dessous avec
leur nouvelle attente (tout autre test qui bougerait reste un arrêt) ; **FIX** du point non bloquant : contenu de `skipcopy.targets`
écrit. Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-15).
**Révision 3 (2026-10-15, après l'arrêt de T0).** La porte T0 a arrêté l'exécution avant tout code (« Résultat de T0 » ci-dessous) :
`Current = Target` à la création laisse une entité tirée vers le bas sans bascule en attente (`AnimFlags` 0 pour toujours,
`SpawnAnimationActive` jamais validé). Décision de la session principale (D-E0A-8, remplace la voie (i) de P-A pour cette tranche) :
l'interface **ne pose plus** `Current = Target` (la bascule reste en attente dans les quatre directions et la première validation de
`SyncAnimation` se fait comme aujourd'hui) ; elle charge `AnimFlags` à la création comme la première bascule du binaire
(`0x80038AB4`) ; la cinématique générique prend la clé de son jeu d'animations sur `TargetAnimationId` pour une entité marquée dont
l'animation n'est pas encore validée. La piste « fausser `AnimationDirection` » de l'exécuteur est écartée (état artificiel). Le test H
d'e0a1 ne change plus ; seule la sonde PA3 change. Nouvelle époque de relecture : une seule relecture de clôture suit.
**Relecture de clôture de la révision 3 (2026-10-15) : REVISE**, un point (P2) : la ligne « Arrêts » interdisait ce que D-E0A-8 impose
(la ligne de `TickScriptedNpc`) ; **corrigé mot pour mot** comme le demandait la relecture (exception nommée dans les arrêts), avec le texte
périmé du prérequis. Le reste a été confirmé contre le code (bascule en attente dans les quatre directions, `AnimFlags` rechargé à
l'identique, ligne sans effet après validation, PA3 seul test touché). La règle de relecture ne permet pas une relecture de plus sans
l'auteur : la tranche est **en pause** jusqu'à son accord.
L'auteur a accordé une relecture de clôture de plus (2026-10-15) ; relecture de clôture de la révision 3 : **READY** (2026-10-15).

**Prérequis.** E20.e0a3a ✅ (sonde P-A ; sa voie (i) est remplacée par D-E0A-8, révision 3 ; `Alundra.Tests` 4 815).

**But.** Les entités nées par l'interface dans `MovePlayer` participent au tick de leur naissance (`BT-M1` à `BT-M3`) ; nées dans un
programme natif, elles tournent au tick suivant (écart D-E20-34, épinglé) ; les entités par enregistrement ne changent pas. Puis mesurer
pour l'auteur (Q-A1) ce que ferait la même règle sur les apparitions par enregistrement (P-B).

**Faits propres à la tranche** (relevés au HEAD `89fb2aa` ; numéros de ligne à relire à l'exécution) :
- [DLL, P-A] Une entité née par l'interface a `CurrentAnimationId` à ~0 : la cinématique générique (`AlundraScriptedMotion.TickScriptedNpc`)
  lit la vitesse sur l'animation courante et donne 0 ; avec `Current = Target`, X = 153, 159, 165 pour le n° 3 en direction `0x18` depuis
  150 px (3 px au tick de naissance, puis 6 px), ce que l'annexe d'E20.e0 donne pour le binaire (`e20-armes/e0-spawn.md`, point 4 :
  une flèche née à X = 120 avance de 3 px dans son tick de naissance, puis de 6 px par tick).
- [DLL, P-A] Avant l'intégration par le moteur, le sprite n'a aucune animation ; le premier `SyncAnimation` de l'image suivante le pose
  (D-E20-35) ; `Controller.Move` ajoute le déplacement sans test.
- [DLL] `CollectTickEntities` (`AlundraWorldProxy.cs`, vers `:2785-2801`) ne retient que les entités `TookPartInFrame` ; l'appel de
  `MovePlayerOnTickPad` est vers `:2839` ; `SpawnEntityBySpriteIndex` enchaîne `AddEntity`, `HoldSlot`/`InsertBySlot`, le son, puis
  `PushLogicalPositionToRoot` (vers `:3410-3417`), qui appelle `ClampToGround` (lecture du champ de collision) ; les lectures de
  `Owner.World.CollisionField` d'`AlundraEntityScriptProxy.cs` sont vers `:752`, `:1730`, `:1801`, `:1892`, `:1966`, `:2082`, `:2246`
  (plus `AlundraPlayerAnimationEffects.cs:142` et `AlundraScriptedMotion.cs:695`) ; la propriété existante `AlundraWorldProxy.CollisionField`
  est de type `AlundraCellsCollisionField?` (vers `:610`) ; `PublishEntityFrame` ignore une entité qui n'a pas `TookPartInFrame` et
  `SyncAnimation` remet à zéro `AnimationSwitchTicksReserved` une fois par image.

**Décisions de la tranche** (ADR-0065) :
- marqueur `JoinsBirthTick` du proxy (interne, non cloné), posé **seulement** par `SpawnEntityBySpriteIndex`, **juste avant son
  `return`, après `PushLogicalPositionToRoot`** : la création ne lit donc jamais le champ par le repli (le seul relèvement de la création
  reste celui du noyau, comme `0x80039EF0` ; pas de second relèvement par les coins de la boîte du `CollisionComponent`) ;
- **D-E0A-8 (révision 3)** — dans `CreateEntityFromSpriteIndex` seulement, après `ApplyHeaderInitialization` (jamais dans le noyau
  partagé) : `AnimFlags = AnimSetsByAnim[TargetAnimationId].Acceleration` (0 si l'animation n'est pas dans le jeu), la valeur que la
  première bascule du binaire charge à la création et que `SyncAnimation` rechargera à l'identique à sa validation ;
  `CurrentAnimationId`, `CurrentDirection`, `AnimationDirection` et `SpawnAnimationActive` restent ceux que laisse le noyau (bascule
  en attente dans les quatre directions) ;
- **D-E0A-8, suite** — `AlundraScriptedMotion.TickScriptedNpc` prend la clé du jeu d'animations sur `TargetAnimationId` quand
  l'entité porte `JoinsBirthTick` et que `CurrentAnimationId` vaut encore la valeur d'une animation non validée (celle que laisse le
  noyau, ~0), sur `CurrentAnimationId` sinon (une seule ligne, gardée par le marqueur ; aucune entité par enregistrement ne la voit) ;
- `CollectTickEntities` retient `TookPartInFrame || (JoinsBirthTick && Owner.World == null)` ; le monde compte les apparitions par
  l'interface du tick (compteur remis à zéro en tête de tick) ; si ce compteur est non nul juste après l'appel de `MovePlayerOnTickPad`,
  `CollectTickEntities` est relancé, ce qui ajoute le nouveau-né aux entités du tick dans l'ordre des cases ;
- repli sur le champ de collision de l'hôte : membre par défaut `ICollisionField? CollisionField => null` d'`IAlundraScriptHost`,
  implémenté **explicitement** par `AlundraWorldProxy` (la propriété existante est d'un autre type : sans implémentation explicite, le
  membre par défaut rendrait `null`) ; ce nom remplace le `CollisionFieldNow` de D-E0A-5 ; les lectures de `Owner.World.CollisionField` l'emploient **seulement** quand `JoinsBirthTick` est vrai
  et `Owner.World` est nul ;
- point d'accroche de test dans `SpawnWeaponProjectile`, `[ThreadStatic]`, nul en production (motif de `WeaponSpawnCallsForTests`), qui
  appelle l'interface à l'endroit exact de `MovePlayer` ; le tir n'est pas branché (E20.f3).

**Périmètre.** `AlundraEntityScriptProxy.cs` (marqueur, repli des lectures), `AlundraEntitySpawnFactory.cs` (`CreateEntityFromSpriteIndex`
seulement), `AlundraWorldProxy.cs` (collecte, rassemblement, `CollisionField`, marqueur dans `SpawnEntityBySpriteIndex`),
`IAlundraScriptHost.cs`, `AlundraPlayerManager.cs` (point d'accroche), `AlundraPlayerAnimationEffects.cs` **seulement** pour la lecture du champ de collision,
`AlundraScriptedMotion.cs` **seulement** pour la lecture du champ de collision et la ligne de D-E0A-8 dans `TickScriptedNpc` ; tests ; ADR-0065 ; plan.

**Test existant que la décision change** (le seul autorisé à changer, révision 3 ; sa nouvelle attente est justifiée par D-E0A-8, par
P-A et par l'annexe `e20-armes/e0-spawn.md`, point 4) :
- `AlundraBirthTickProbePaTests.PA3_TheGenericKinematicsOfTheBirthTick_WithCurrentAsTheInterfaceLeavesIt` (e0a3a) : l'entité de
  l'interface porte maintenant le marqueur, la cinématique lit la cible ; la mesure devient X = **153, 159, 165** et son nom ou son
  commentaire dit qu'il mesure l'état **après** e0a3b ; la mesure historique (~0, X = 150 ×3) reste écrite dans « Résultat de P-A ».
`AlundraSpawnBySpriteIndexInterfaceTests` (dont H) ne change pas. T0 (3) recense par recherche tout test qui compare `CurrentAnimationId`,
`CurrentDirection` ou `AnimFlags` d'une entité née par l'interface ; un test trouvé autre que PA3 est un arrêt (révision de la tranche).

**Hors périmètre.** Le branchement du tir ; l'extension aux apparitions par enregistrement (e0a4) ; l'intégration immédiate (D-E20-35) ;
`PublishEntityFrame` (la première publication reste celle de l'image suivante) ; les lignes `BT-L1`, `BT-L3` (butin à la mort, e0b et e0c).

##### Tâches

- ✅ **T0′ — Vérifications de la révision 3, avant le code** (sonde jetable, résultats versés dans le plan) : pour le n° 3 né par
  l'interface dans `MovePlayer`, dans les **quatre** directions (`0x00`, `0x08`, `0x10`, `0x18`), avec `AnimFlags` posé à la création et
  la clé de la cinématique gardée par le marqueur (posés à la main dans la sonde) : `AnimFlags` égal à la valeur du jeu d'animations dès
  le tick de naissance ; à la fin de la première image, la validation de `SyncAnimation` a lieu (bascule en attente), recharge la même
  valeur et fait tomber `SpawnAnimationActive` ; aucune impulsion ni son ajouté ou perdu par rapport à aujourd'hui ; X = 153 puis 159
  en direction `0x18` ; et le recensement de T0 (3). Toute autre issue est un arrêt, comme pour T0.
- ⚠️ **T0 — Vérifications avant le code** (ARRÊT du 2026-10-15 : écart sur une entité de l'interface, voir « Résultat de T0 » ;
  décision de la révision 3 ; T0′ ci-dessus la remplace) (versées dans le plan, sans commit de code) : (1) `AnimFlags` à la création : ce que le binaire
  pose à la première bascule de la création (`0x80038AB4`, annexe d'e0a1) et ce que lit la passe d'animation du tick de naissance ;
  (2) l'effet de `Current = Target` sur `SpawnAnimationActive`, sur l'horloge `AnimationSwitchTicksReserved` et sur le son et l'impulsion
  de la première animation, pour une entité née dans `MovePlayer` (mesure par un test de sonde) ; (3) les tests d'e0a1 qui ne comparent pas
  `CurrentAnimationId` (`AlundraSpawnBySpriteIndexInterfaceTests`) restent vrais. **Suites** : si (1) et (2) ne demandent aucun code hors
  des décisions ci-dessus et ne montrent aucun écart sur une entité de l'interface, T1 commence ; sinon — un code à ajouter (par exemple
  poser `AnimFlags` dans l'interface), une réservation de bascule reportée à l'image suivante, une impulsion ou un son perdu ou doublé, ou
  tout effet sur une entité par enregistrement — **arrêt** : le résultat est versé dans le plan et la session principale révise la tranche,
  qui est relue avant T2.
- ✅ **T1 — Tests rouges.** (a) Trace des passes d'une naissance dans `MovePlayer` (point d'accroche) calquée sur `BT-M1` à `BT-M3` : après
  la passe des programmes l'état est `Normal` et le déclencheur (`EventTrigger`, `+0x228`) celui de la ligne, le gestionnaire A (inscrit par le test dans `AlundraNativeSpriteRegistry`) a tourné une fois
  au tick 1 et zéro au tick 2, `FrameCounter == 1` après les compteurs, présente dans `_updateProxies` et dans la liste des coups après les
  listes, horloge et physique appelées une fois (deux dans une image à deux ticks), X = 153 puis 159 pour le n° 3 en direction `0x18`
  depuis 150 px ; (b) naissance dans un programme natif : état `Loaded` à la fin du tick 1, A au tick 2 seulement (test nommé d'après
  D-E20-34, écart de `BT-H1`, `BT-H4`, `BT-H6`) ; (c) monde à ticks propres : aucune participation ; (d) détruite au tick de sa naissance
  dans une image à deux ticks : recyclée en tête du tick suivant, case rendue, aucune exception à l'intégration par le moteur ; (e) son de
  création demandé une seule fois ; (f) repli du champ de collision : employé par une entité marquée avant intégration, jamais par une
  entité par enregistrement, et **jamais pendant la création** (un champ espion compte zéro lecture entre l'appel et le retour de
  `SpawnEntityBySpriteIndex`) ; `AlundraSpawnBySpriteIndexInterfaceTests` passe sans modification ; (g) les quatre directions de T0′
  épinglées (`AnimFlags` dès la naissance, validation de la première image, `SpawnAnimationActive` qui tombe).
- ✅ **T2 — Code** (les décisions ci-dessus). Suites complètes : aucune épingle des apparitions par enregistrement ne bouge
  (`AlundraTickStructureTests`, `AlundraVisionArcTests`, `AlundraShipBlockArcTests`, `AlundraAbsoluteZWritesTests`,
  `AlundraControllerlessVerticalStepTests`, `AlundraAnimationImpulseSpawnTests`, et toute la suite).
- ✅ **T3 — Mesure P-B (après le code).** Précondition : `git status --porcelain` vide sur les fichiers que la sonde touche (sinon arrêt) ;
  forcer le seul drapeau `JoinsBirthTick` dans les chemins par enregistrement (changement jetable, jamais commité) ; construction
  **toujours avec la copie de la DLL neutralisée** (`-p:CustomAfterMicrosoftCommonTargets=<scratch>/skipcopy.targets`, fichier du
  scratchpad qui redéfinit une cible vide de même nom : `<Project><Target Name="CopyGameplayDllToProject" /></Project>`, importé après
  le corps du projet, donc prioritaire ; la cible
  `CopyGameplayDllToProject` d'`Alundra.csproj` copiant sinon `Alundra.dll` et `Alundra.pdb` dans `alundra-project/` à toute construction),
  puis une suite complète en Release ; remise en état par `git checkout -- <fichiers touchés par la sonde>` et des cinq traces ; puis
  reconstruction Release et Debug du code commité ; liste versée dans la sous-section « Mesure P-B » du §1.16.4, au format
  `Classe.Méthode — raison observée`. Aucune construction ne dépose une DLL qui porte le marqueur forcé.
- ✅ **T4 — ADR-0065, plan** (résultats, icônes) ; les verifiers frais sont lancés par la session principale (non faits ici).

##### Résultat de T0 (2026-10-15, base `e6207ae`) : ARRÊT, aucune ligne de code de production écrite

Mesure par un test de sonde jetable (jamais commité ; `ArcRun` de la carte 14, flèche n° 3 de la page globale par l'interface, `Current = Target` posé à la main juste après l'apparition, comme
`PA1b`, puis trois images du moteur d'un tick), pour les quatre directions, avec et sans `Current = Target` :

| Direction | `Current = Target` | `AnimFlags` aux images 1, 2, 3 | `SpawnAnimationActive` | animation du sprite | `HitAnimByte` |
|---|---|---|---|---|---|
| `0x10` (haut), `0x08` (gauche), `0x18` (droite) | non | `0x41` | faux dès l'image 1 | `anim0_up`, `_left`, `_right` | `0x41` |
| `0x10`, `0x08`, `0x18` | oui | `0x41` (identique) | faux dès l'image 1 | identique | `0x41` |
| `0x00` (bas) | non | `0x41` | faux dès l'image 1 | `anim0_down` | `0x41` |
| **`0x00` (bas)** | **oui** | **`0` aux trois images, pour toujours** | **vrai aux trois images (jamais validé)** | `anim0_down` (celle par défaut du moteur) | `0x41` |

Cause (lue puis mesurée) : `AlundraFrameSyncPasses.TryResolveAnimationTarget` ne voit une bascule en attente que si `Current != Target` ou si `AnimationDirection` n'est pas la direction que la table
dérive de la cible. Pour la direction `0x00`, la table (`AnimationDirectionTable[0 * 8 + 0]`) rend 0 et `AnimationDirection` vaut 0 à la création : avec `Current = Target` plus aucune bascule n'est en attente,
`SyncAnimation` rend sans rien faire (`AnimFlags` jamais chargé, `SpawnAnimationActive` jamais retombé, sprite jamais choisi explicitement). Pour les trois autres directions la dérivée n'est pas 0, la
bascule reste en attente et l'état après une image est celui d'aujourd'hui (c'est ce que P-A avait mesuré pour la droite). Un tir vers le bas (`SpawnDirections[0] = 0`) est un cas réel, et les
sprites de l'interface ont tous une animation 0 dont l'octet `AnimFlags` n'est pas nul (`0x40`/`0x41` pour les armes du héros ; `0xC0`/`0xC1` pour les n° 20, 21, 27, 28, 29, dont le bit `0x80` sort l'entité de la
liste des collisions et du soutien entre entités).

Autres éléments de T0 :
- (1) Le binaire recharge `+0xB4` (le nom de `HitAnimByte` dans la passe des coups) et l'octet de la fiche d'animation (`AnimFlags` de la DLL, bit `0x80`) au bloc de bascule de `UpdateAnimation`
  (`0x80038B18-0x80038BAC`), donc dès la première bascule de la création (`0x80038AB4`). Le noyau de la DLL charge déjà `HitAnimByte` et l'attaque courante à la création (vérifié par
  `SpawnProjectionAsserts`) ; `AnimFlags`, lui, n'est chargé que par `SyncAnimation`, à la fin de la première image : pendant le tick de naissance il vaut 0 (écart de fond, pour tout sprite de l'interface,
  déjà présent avant e0a3b, visible seulement pour les n° dont l'octet porte `0x80` : 20, 21, 27, 28, 29).
- (2) Pour les directions 8, `0x10` et `0x18`, `Current = Target` ne change aucun des champs relevés (`SpawnAnimationActive`, `AnimationSwitchTicksReserved`, `AnimationSwitchTickOwed`, impulsion, son,
  sprite) : la bascule reste en attente comme avant. Le seul changement est pour la direction 0 (tableau ci-dessus).
- (3) Aucun autre test existant ne compare `CurrentAnimationId` ou `CurrentDirection` d'une entité née par l'interface que les deux déjà nommés (`PA3` de `AlundraBirthTickProbePaTests`, `H` de
  `AlundraSpawnBySpriteIndexInterfaceTests`) : recherche dans `Alundra.Tests/` des cinq fichiers qui appellent l'interface ou la fabrique.

**Issue : arrêt prévu par T0** (« un code à ajouter [...] ou tout effet [...] écart sur une entité de l'interface »). La décision `Current = Target` seule laisse une entité tirée vers le bas sans
`AnimFlags` ni validation de la bascule d'apparition. Aucun code n'a été écrit, aucun test existant modifié. Pistes pour la révision de la tranche (à trancher par la session principale, relecture avant T2) :
(a) garder `Current = Target` et poser aussi `AnimationDirection` sur une rangée dont la dérivée diffère (la bascule reste alors en attente pour les quatre directions, `SyncAnimation` charge `AnimFlags` et
fait retomber `SpawnAnimationActive`, comme pour les directions 8, `0x10`, `0x18` ; à vérifier que la dérivée de la cible ne change pas avec la rangée choisie) ; (b) faire la bascule entière à la création
(`AnimFlags`, `SpawnAnimationActive`, `AnimationDirection`) et choisir le sprite par un autre moyen, ce que l'absence d'animations avant l'intégration rend impossible dans l'interface (P-A, mesure 1) ;
(c) voie (ii) de P-A (lire la cible au pas de mouvement pour les entités marquées, `AlundraScriptedMotion.cs`), écartée par la tranche. Recommandation : (a), puis ajouter à T1 un test des quatre
directions (états identiques à l'image 1 avec et sans `Current = Target` hors la cinématique).

##### Résultat de T0′ (2026-10-15, base `0d9d8cb`) : issue conforme, pas d'arrêt

Sonde jetable (`ZzProbeT0PrimeTests`, jamais commitée ; `ArcRun` de la carte 14, entité par l'interface sur le monde réel, trois images d'un tick), pour les n° 3 (flèche, octet `0x41`) et 20 (octet `0xC0`) de la page
globale, dans les quatre directions `0x00`, `0x08`, `0x10`, `0x18`. Chaque cas est joué deux fois : « ancien » (`AnimFlags` remis à 0 et marqueur retiré à la main : l'état d'avant e0a3b) et « nouveau » (`AnimFlags` posé à la
création, marqueur posé : l'état de la révision 3). La clé de la cinématique (ligne de `TickScriptedNpc`) et le marqueur sont ceux du code de T2.

| Mesure | Résultat |
|---|---|
| `AnimFlags` à la naissance (nouveau) | `0x41` (n° 3) ou `0xC0` (n° 20) dans les quatre directions, égal à l'octet du jeu d'animations (ancien : 0) |
| Validation de `SyncAnimation` à la fin de la première image | a lieu dans les quatre directions, y compris `0x00` : `SpawnAnimationActive` tombe, `Current` passe à 0, `AnimationDirection` à 0/2/1/3 (bas/gauche/haut/droite), `AnimFlags` rechargé à l'identique (`0x41`, `0xC0`), `HitAnimByte` identique |
| Différence ancien/nouveau après la naissance (images 1 à 3) | **aucune** : `AnimFlags`, `SpawnAnimationActive`, `Current`, `AnimationDirection`, `IsZForceApplied`, verrou et dette d'impulsion, ticks réservés, tick dû, `HitAnimByte` et liste des sons demandés (un seul son, 162, le même dans les deux) sont égaux ; aucune impulsion ni son ajouté ou perdu |
| Cinématique du n° 3 en direction `0x18` depuis 150 px, trois ticks, marqueur posé et `Current` à `~0` | X = **153, 159, 165** |
| Recensement de T0 (3) | inchangé : les seuls tests qui comparent `CurrentAnimationId` ou `CurrentDirection` d'une entité née par l'interface sont `PA3` et `H` ; `H` ne change pas (la décision ne touche plus `Current`) |

Issue : celle qui est écrite (aucun écart sur une entité de l'interface, aucun code hors des décisions). T1 commence.

##### Résultats (2026-10-15, base `0d9d8cb`)

- **T0′** (`681c87b`) : issue conforme, voir « Résultat de T0′ ».
- **T1 et T2** (`7b9b096`). Tests : `Alundra.Tests/AlundraBirthTickTests.cs` (18 tests nouveaux, théorie comprise). Code : marqueur `JoinsBirthTick` (posé en dernier par `SpawnEntityBySpriteIndex`, jamais par le chemin
  par enregistrement, non cloné), compteur des naissances par l'interface remis à zéro en tête du bloc d'entités et nouvelle collecte après `MovePlayer`, collecte `TookPartInFrame || (JoinsBirthTick && Owner.World == null)`,
  repli `IAlundraScriptHost.CollisionField` (implémentation explicite du monde) lu par `ReadCollisionField()` aux neuf lectures, `AnimFlags` chargé dans `CreateEntityFromSpriteIndex` seulement, une ligne gardée par le marqueur dans
  `TickScriptedNpc`, point d'accroche `AlundraPlayerManager.WeaponSpawnBirthHookForTests`. Les tests ont été écrits avec le code dans l'arbre de travail (pas de passage rouge séparé) ; le mordant est prouvé par les deux
  mutations réelles ci-dessous.
- **Preuves de l'acceptation.** `BT-M1` à `BT-M3` (naissance dans `MovePlayer`) : état `Loaded` à l'entrée de la passe des programmes puis `Normal`, déclencheur −1, A une fois au tick 1 et zéro au tick 2, `FrameCounter` 0 puis 1 aux
  compteurs, présente dans la liste de mise à jour et dans la liste des coups après les listes, horloge d'animation passée (une fois, deux ticks comptés dans une image à deux ticks), non intégrée par le moteur pendant son tick
  de naissance, X +3 px puis +9 px au tick 2 (153, 159) ; image à deux ticks : A une fois, FrameCounter 2, X +9 px. Écart de `BT-H1`, `BT-H4`, `BT-H6` : naissance dans le programme natif d'une autre entité, `Loaded` à la fin du
  tick 1, A au tick suivant seulement (images d'un tick et image de deux ticks), `FrameCounter` 1 au tick 1 comme l'oracle (la passe des compteurs n'a pas de filtre d'état). Détruite au tick de sa naissance (image à deux ticks) :
  A a tourné, recyclée en tête du tick suivant, case rendue (reprise par l'apparition suivante), deux images du moteur sans exception. Son de création : demandé une seule fois (le n° 7, `0x21`) sur quatre images.
  Espion : zéro lecture de repli entre l'appel et le retour de `SpawnEntityBySpriteIndex` ; des lectures de repli pendant le tick de naissance ; zéro une fois l'entité intégrée ; jamais pour une entité par enregistrement
  (`SpawnEntityByRecordId` : pas de marqueur, `ReadCollisionField()` nul avant l'intégration). Quatre directions de T0′ épinglées (théorie de huit cas : n° 3 et 20) : `AnimFlags` dès la naissance, bascule en attente (`Current` ~0), validation
  à la première image, `SpawnAnimationActive` qui tombe, `AnimFlags` et `HitAnimByte` identiques, aucune impulsion. Garde de la cinématique : une entité sans marqueur reste immobile (150, 150, 150). Hôte à ticks propres : `ReadCollisionField()` nul.
  Les six épingles (`AlundraTickStructureTests`, `AlundraVisionArcTests`, `AlundraShipBlockArcTests`, `AlundraAbsoluteZWritesTests`, `AlundraControllerlessVerticalStepTests`, `AlundraAnimationImpulseSpawnTests`) et toute la suite : vertes.
- **Deux mutations réelles**, appliquées par script sur le changement non commité puis restaurées, jamais commitées. (1) Marqueur posé sur le chemin par enregistrement (`ApplyRecord`) : rouges
  `TheMarker_IsPosedByTheInterfaceOnly_AndNotCloned` et `TheCreation_NeverReadsTheCollisionFieldOfTheHostByTheFallback_…_AndAnEntityFromARecordNever`. (2) Condition `JoinsBirthTick` retirée de la ligne de `TickScriptedNpc` : rouge
  `AnEntityFromARecord_KeysItsKinematicsOnItsCurrentAnimation_NeverOnTheTarget_…` (X = 153 au lieu de 150).
- **Comptes.** `Alundra.Tests` : 4 815 → **4 833**, zéro échec, en Release **et** en Debug (`--blame-hang-timeout 300s`). Tests existants : seul `AlundraBirthTickProbePaTests.PA3_…` change (renommé `…_MeasuredAfterE0a3b`, attente 153, 159, 165) ;
  `AlundraSpawnBySpriteIndexInterfaceTests` (H compris) inchangé. Cinq traces inchangées (bruit de fins de ligne restauré, jamais indexé). `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` : identiques,
  après la remise en état de P-B (la DLL du projet n'était pas verrouillée ; les constructions de P-B ont toutes neutralisé la copie).
- **Mesure P-B** (T3) : voir la sous-section du §1.16.4. Aucune construction n'a déposé une DLL qui porte le marqueur forcé.

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais, l'un après l'autre. Rejeu : `Alundra.Tests` 4 833 en Release et en
Debug, zéro échec ; `git diff 0d9d8cb..5d438ff -- Alundra.Tests/` ne change que PA3 et ajoute `AlundraBirthTickTests.cs` ; les six
épingles vertes ; traces inchangées ; mesure P-B versée, aucun reste de la sonde ; ADR-0065 indexé ; `cmp` de la DLL identique ;
mutation 2 rejouée (elle mord). Adversarial : neuf mutations réelles (marqueur sur le chemin par enregistrement, marqueur cloné, marqueur
posé avant la poussée de position, chargement d'`AnimFlags` retiré, condition du marqueur retirée de `TickScriptedNpc`, repli sans
condition de marqueur, nouvelle collecte neutralisée, collecte réduite à `TookPartInFrame`), toutes tuées sauf une (ci-dessous) ;
aucun appelant de production ; aucune DLL au marqueur forcé dans `alundra-project/`.
Écart de procédure noté par l'exécuteur : les tests de T1 ont été écrits avec le code dans l'arbre de travail, sans passage rouge
séparé ; leur mordant est prouvé par les mutations des deux verifiers.
**Reportés** (P3 et P4, sans changer le candidat confirmé) :
- **P3** — la moitié `CurrentAnimationId == ~0u` de la garde de D-E0A-8 (`AlundraScriptedMotion.TickScriptedNpc`) n'est épinglée par
  aucun test : la remplacer par la seule condition du marqueur laisse la suite verte (mutation M3 de l'adversarial). Le code est juste à
  la lecture (après validation, `Current` n'est plus ~0). Épingle à ajouter par la prochaine tranche qui touche cette cinématique
  (E20.e0k) ou par e0a4 : une entité marquée qui change d'animation après la validation garde la clé de son animation courante.
- **P4** — le test « hôte à ticks propres » ne joue pas un monde à ticks propres (l'absence de participation découle de la structure :
  `RunEntityBlock` ne tourne que si le monde pilote les ticks) ; après une reconstruction Debug d'un verifier, la DLL du projet et celle
  de `bin/Debug` ne diffèrent que par leurs champs d'identité (horodatage PE, MVID, identifiant du PDB), code et métadonnées identiques.

##### Acceptation

- T1 vert ; `Alundra.Tests` en Release et en Debug : 4 815 plus les nouveaux, aucun échec, **aucun test existant modifié hors de PA3**
  de `AlundraBirthTickProbePaTests` (nouvelle attente 153, 159, 165) ; les six
  épingles nommées intactes ; aucun appelant de production de `SpawnEntityBySpriteIndex` (le tick de naissance n'a donc aucun effet en
  jeu tant qu'E20.f3 ou E20.h3 ne branchent rien) ; **après la remise en état de P-B**, `cmp` de `alundra-project/Alundra.dll` avec le
  build Debug du HEAD commité (si la DLL du projet est verrouillée par le Launcher de l'auteur, les builds se font avec la copie
  neutralisée et le `cmp` est noté comme non fait, avec la raison) ; traces inchangées.
- Mesure P-B versée au format écrit, l'arbre remis en état (`git status` sans changement hors des sous-modules de l'auteur).
- Verifiers frais.

##### Arrêts, budget, retour arrière

- **Arrêts** : une épingle d'apparition par enregistrement qui bouge (le marqueur fuit) ; T0 qui montre un effet de `Current = Target` sur
  une entité par enregistrement ; un besoin de modifier le noyau partagé, la cinématique générique ailleurs que la lecture du champ et la seule ligne de D-E0A-8 dans `TickScriptedNpc` (clé gardée par
  `JoinsBirthTick` et `CurrentAnimationId == ~0u`), ou le
  moteur ; la précondition de P-B non tenue ; une issue de T0 qui demande du code hors des décisions ou montre un écart sur une entité
  de l'interface ; une épingle d'e0a1 sur `PosZ` qui bouge.
- **Budget** : un `executor`, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : `git revert` des commits d'E20.e0a3b (la mesure P-B ne laisse aucun code).

#### 1.16.4 E20.e0a4 — Participation au tick de naissance des apparitions par enregistrement ⏳ (décidée le 2026-10-15, Q-A1)

**Prérequis.** E20.e0a3b ✅ ; Q-A1 tranchée le 2026-10-15 (oui, après la mesure P-B) ; ordre : après E20.e0b2 (même fichier). Ne bloque
aucune autre tranche.

**Mesure P-B** (versée par E20.e0a3b, 2026-10-15, base `7b9b096`). Changement jetable, jamais commité : `proxy.JoinsBirthTick = true` à la fin de `AlundraEntitySpawnFactory.ApplyRecord` (donc pour la charge d'une carte
et pour `SpawnEntityByRecordId`, c'est-à-dire les opcodes `0x2D`, `0x8A`, `0x8B`), précondition `git status --porcelain` vide sur les fichiers touchés ; construction Release avec la copie de la DLL neutralisée, suite complète
(4 833 tests) : **8 tests existants rouges** (les 2 autres rouges sont les épingles du marqueur d'e0a3b, attendues). Remise en état par `git checkout --` du seul fichier touché, puis reconstruction Release et Debug du code commité (4 833 verts
dans les deux). Format `Classe.Méthode — raison observée` :

- `AlundraInoaDayOneArcTests.A10_TheFirstVisitOnMap165_TheActorsPassTheSceneOnByFlags_ThePlayerGetsTheHandBack` — la liste de numéros d'image attendue se décale d'une image (596, 597, … au lieu de 597, 598, …) : une apparition par événement de carte tourne dans son tick au lieu de l'image suivante.
- `AlundraShipBlockArcTests.A6_TheCameraBlockLandsAndWalksSouth_ThenTheWarpToMap416Departs` — les images relevées (`C[100]` du bloc d'après la `0x2D` à 265) arrivent une image plus tôt : [180, 643, 1332] au lieu de [181, 644, 1333].
- `AlundraHeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite(id: "s36c4_rec3")` — la fin de l'attente est vue une image plus tôt : (165, 325, 160, 11534335) au lieu de (166, 326, 160, 11534335).
- `AlundraHeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite(id: "s115_rec19")` — même décalage d'une image : (756, 1077, 321, 3145727) au lieu de (757, 1078, 321, 3145727).
- `AlundraNpcCharacterControllerMoverTests.GullEntitySix_RealFlightAtRenderRateAndAtFiftyHertz_ClimbsAndDriftsWithFaithfulTickQuantizedPhysics(dt: 0,00416666688)` — l'apogée du goéland vaut 210,73 au lieu de l'intervalle 208,25 à 210,25 : un tick de plus de vol dès la naissance de l'entité.
- `AlundraNpcCharacterControllerMoverTests.GullEntitySix_RealFlightAtRenderRateAndAtFiftyHertz_ClimbsAndDriftsWithFaithfulTickQuantizedPhysics(dt: 0,008130081)` — même apogée 210,73, hors de l'intervalle.
- `AlundraNpcCharacterControllerMoverTests.GullEntitySix_RealFlightAtRenderRateAndAtFiftyHertz_ClimbsAndDriftsWithFaithfulTickQuantizedPhysics(dt: 0,0199999996)` — même apogée 210,73, hors de l'intervalle.
- `AlundraNpcCharacterControllerMoverTests.GullEntitySix_RealFlightWithFrameTimeHitchesInjectedMidClimb_StillReachesTheFaithfulApexAndDrift` — même apogée 210,73, hors de l'intervalle.

Lecture : les quatre échecs d'arcs et de sites sont un décalage d'exactement une image (la règle « première image à l'image suivante » que les épingles tiennent aujourd'hui) ; les quatre du goéland montrent un tick de plus à la naissance de l'entité par enregistrement.

Non détaillée. Elle ne démarre qu'avec l'accord de l'auteur après la liste de P-B (sous-section « Mesure P-B » ci-dessous, versée
par e0a3b). Chaque épingle qui bouge est re-dérivée de l'oracle (une apparition par événement de carte tourne dans le tick, D-E20-11) ; les arcs listés au §1.16 (A, B) sont remesurés un à un.

##### Points ouverts ajoutés

| Point | Question | Où elle se tranche |
|---|---|---|
| O-E0A-1 | « A deux fois » : une apparition en boucle 2 sur une case dont le déclencheur vaut 0 fait tourner A dans la passe puis au tick suivant ; la DLL n'a que le tick suivant | e0b, e0c ou E14, si un gestionnaire A en dépend |
| O-E0A-2 | Lecteur de `+0x08` : aucun trouvé dans la décompilation ; recherche dans le binaire à faire si E20.g ou E14 en montre un | E20.g, E14 |
| O-E0A-3 | Coût de `LoadCopy` à chaque apparition (une lecture de disque) | mesure avant E20.f3 |
| O-E0A-4 | **Tranché le 2026-10-15 (révision 3 d'e0a3b, D-E0A-8)** : plus de `Current = Target` à la création ; `AnimFlags` chargé à la création ; la cinématique lit la cible pour une entité marquée non validée | — |

### 1.17 E20.e0b — Le générateur pseudo-aléatoire : e0b1 (avance par tick), e0b2 (tirage du butin à la création) ⏳ (détaillée le 2026-10-15 ; e0b1 faite et CONFIRMED le 2026-10-15)

**Réponses de l'auteur (2026-10-15).** Q-B1 : `ArcRun` re-sème le flux avant **et** après le chargement. Q-B2 : les écarts de D-E0B-3
sont acceptés, documentés. Q-B3 : le découpage e0b1 / e0b2 et le changement de prérequis de l'enveloppe sont accordés (révision 6 de
l'enveloppe, §1.1, avec sa relecture de clôture). Q-B4 : défaut fidèle, sans question (l'avance tourne dans les menus, le livre et
l'inventaire, comme le binaire). Q-B5 n'a pas été posée : les puits d'ombre restent dans le code de production, inertes sans leur
variable d'environnement, comme le canal d'ombre d'e0a2 (ADR-0064, défaut du précédent ; l'auteur peut le changer).

Découverte en lecture seule du 2026-10-15, au HEAD `4d17824` : trois relevés (binaire, impact sur la DLL, inventaire des tests), puis un critique adversarial qui a revérifié plus de trente
affirmations (désassemblage de `ALUN_CD.EXE` avec son propre capstone ; 6 011 vecteurs du tirage rejoués sur la machine MIPS contre un modèle écrit à part ; 168 mutants de sa façon ;
boucle de chargement réelle rejouée sur 42 cartes ; recensement des données refait ; lecture du code de la DLL et des tests). Scripts et sorties dans le scratchpad de la session :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0b/`
(`bin/`, `dll/`, `tests/`, `critic/`). Aucune modification du moteur, du convertisseur ni des données. La tranche est découpée en deux, parce que ses deux moitiés n'ont ni les mêmes
dépendants, ni le même rayon d'action : e0b1 (l'avance) touche chaque test qui fait tourner un monde, e0b2 (le butin) ne touche que les chargements de cartes et les apparitions.

**But.** e0b1 : reproduire `0x8002BD0C-0x8002BD34`, un pas du générateur à la fin de chaque tick logique, porte fermée ou non, jamais pendant le départ d'un warp, sans changer aucune
autre consommation. e0b2 : reproduire le tirage de `0x80032A40` à chaque création (`ContentsItemId` tiré, `ContentsGameFlag` posé, le mot du générateur avancé de 0, 1 ou 2 pas) ;
rien ne tombe (le lâcher, son veto et l'objet ramassable sont E20.e0c). Ferme D-E0A-3.

**Découpage et changement d'enveloppe (accordés par l'auteur le 2026-10-15, Q-B3 ; enveloppe révisée au §1.1, révision 6).** L'enveloppe (§1.1, ligne 3) fait d'E20.e0b une seule tranche. La critique la scinde parce que les dépendants n'exigent pas
la même moitié : E20.f6 (E9 tire à l'impact et toutes les 8 images) exige l'avance ; E20.g2 et E20.e0c (le butin tiré à la création, lu à la mort) exigent le tirage ; E20.f3 (flèches,
vague, effets de coup) ne tire rien à la création (`Contents` 0 des sprites communs 3 à 15 et 27 à 29, `f-projectiles.md` point 7) : la raison pour laquelle sa ligne 5 exige « e0b » n'est écrite nulle part, et elle est ramenée à e0b1 (le flux que verront
les étincelles de coup ; proposition de la critique, accordée avec Q-B3). L'ordre est e0b1 puis e0b2 : l'avance est sur le chemin d'E20.f6, le tirage ne l'est pas ; le tirage reste après, pour que l'ombre
d'e0b2 se mesure sur un code qui avance déjà (mêmes mots que dans le jeu). L'`order-critic.md` avait déjà ce découpage (nœuds E0r1 et E0r2).

**Faits porteurs.** Étiquettes : [binaire] décodé ou rejoué sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS ; [DLL] lu dans le code au HEAD `4d17824` (numéros de ligne à relire à l'exécution : E20.e0a3b touche aussi `AlundraWorldProxy.cs`) ; [données] lu dans les
cartes extraites ou l'export ; [critique] établi ou corrigé par la critique.
- [binaire] **L'avance** (`0x8002BD0C-0x8002BD34`) : lecture du mot (`lw v0,-0x78f8(a0)`, `a0 = 0x800a0000`), `mult` par `0x7D2B89DD` (`lui 0x7d2b` / `ori 0x89dd`), `mflo`, `addu` de `0xE06A02E7` (`lui 0xe06a` / `ori 0x02e7`), `sw` dans le délai du `beqz s3` :
  `s ← s·0x7D2B89DD + 0xE06A02E7 (mod 2³²)`, un pas, résultat jamais lu (aucun `mfhi`), magasin toujours exécuté ; `s3` (= a0) ne commande que l'effacement de `[0x800DC4C4]`.
  Le mot est `[0x80098708]`, valeur d'image `0xB017C93D` (octet `0x78F08` du fichier), celle d'`AlundraRandom.Reset()`. Vecteurs : 0 → `E06A02E7`, `FFFFFFFF` → `633E790A`, `80000000` → `606A02E7`,
  `B017C93D` → `35E36190`, `12345678` → `B806E07F`, quatre pas → `AFBE9EAC`.
- [binaire, critique] **Une avance par appel de la fonction d'image `0x8002BAEC`**, sur tous les chemins : 134 instructions relues, aucun retour avant `0x8002BD04` ; la porte `[0x800DC4B8] & 0x48`, un menu,
  un menu refusé, a0 = 0 ou 1 n'y changent rien (seule la passe des listes tourne sous la porte). Appels : deux `jal` (`0x8002C3E4` avec a0 = 1, une fois après chaque chargement de carte ;
  `0x8002C404` avec a0 = 0, à chaque tour), ni `j`, ni mot de pointeur, ni formation d'adresse par `lui`+`addiu` dans toute l'image ; `UpdateWorld` (`0x8002E058`) n'a que `0x8002BC4C`.
  [oracle] la sonde de la fonction d'image rejouée (10 lignes, md5 `4943e0d29796bdee75e0a876f82b6b48`, identique à la découverte) : porte `0x48` → 1 pas ; porte ouverte → 4 pas (trois tirages-marqueurs des passes bouchonnées, plus l'avance, qui est la dernière opération de l'image).
- [binaire] **La boucle de transition `0x8002C490-0x8002C4C0` n'appelle ni la fonction d'image ni l'avance** ; l'image qui a levé la demande a, elle, avancé (`0x8002C454` lit la demande après
  l'appel de `0x8002C404`). Les effets de transition 4, 5 et 6 tirent dans le même générateur (1 200 tirages à l'initialisation, 240 par pas, 600) : 393 portails sur les données (374 pour l'effet 5),
  que la DLL traite comme l'effet 0 (`AlundraWarpDirector.cs:331-334`, D-T-7) : point ouvert O-E0B-1.
- [binaire, critique] **Rien ne réinitialise ni ne copie le générateur** : 599 accès (`lw`/`sw` d'immédiat −0x78F8, 89 fonctions) ; les 267 lectures alimentent toutes un `mult` et les 332 écritures sont des pas ; aucune formation d'adresse de `0x80098708`
  (ni `addiu`, ni `ori`) ; la seule adresse voisine formée (`0x8009870C`, en `0x80039558` et `0x8003959C`) est une table lue par la passe de coups. Le mot est hors du remplissage à zéro `0x800CA0E8..0x801F7F20` ; il n'est donc ni sauvegardé ni affiché
  (ferme l'inconnue « copies par bloc » du relevé, au risque résiduel d'un pointeur calculé). Le son, le changement d'animation, `DisplayInventory`, la liste des effets et le lecteur de pad n'atteignent aucune fonction qui tire (accessibilité par `jal` seulement).
- [binaire, oracle] **Le tirage** : `0x80032A40` (un seul appelant, `0x80039F38`, dernière action d'`InitializeEntity` ; cinq appelants d'`InitializeEntity` : héros, `SpawnWarpEntity`, réinitialisation en place
  `0x8003A048`, apparition par enregistrement `0x8003A1B8`, réinitialisation au foyer `0x8003ABCC`). Avec enregistrement : drapeau `f = hu[rec+0x10]`, mis à 0 si `(f & 0x7FFF) >= 0x800`, écrit en `+0x40` même nul ;
  s'il est non nul et que son bit est levé (banque `0x801EBA40` si `f & 0x8000`, sinon `0x801EB344`, mot `(f>>5)&0x3FF`, bit `f & 31`) : code 0, aucun tirage ; sinon code = `rec[0x12]` s'il est non nul, sinon `header[0x1F]`.
  Sans enregistrement : `+0x40 = 0`, code = `header[0x1F]`. `0x80032968` : `tant que code ≥ 0x100 → 0 ; si code & 0x80 : s ← pas(s) ; code ← table[(code & 0x7F)·16 + (s >> 28)] ; sinon code < 0x62 ? code : 0`.
  Table `0x80028E0C` (octet `0x960C` du fichier, 128 × 16 = 2 048 octets) : 13 rangées non nulles (`0x81..0x83`, `0xF6..0xFF`) ; `0x82`, `0xF7`, `0xF8` tirent exactement une fois ; `0xFA`, `0xFB`, `0xFC`, `0xFF` une ou deux fois
  selon l'état. [critique] 6 011 vecteurs rejoués sur la vraie fonction (11 de la découverte + 6 000 tirés au hasard avec drapeaux, deux banques, codes d'enregistrement et d'en-tête) : 0 écart avec un modèle écrit à part.
- [données, critique] **Population** : 54 numéros de sprites tirent (`Contents & 0x80` : 6 de la page globale, 48 de la page des cartes), leur `Contents` d'en-tête est le même sur toutes les cartes ; aucun en-tête n'a de code direct ;
  aucun enregistrement n'a `Contents` ≥ 0x100 (le binaire lit l'octet `+0x12`) ; 396 enregistrements portent un drapeau, tous en banque persistante, tous < 0x800, tous avec un code d'enregistrement. Au chargement :
  7 240 enregistrements actifs (`IsEnabled` ≠ 0 et bit `0x40`), dont 3 355 tirent, 374 ont un code direct, 3 511 un code nul ; 288 cartes sur 483 ont au moins un tirage (zone du héros ignorée).
- [binaire, critique] **Chargement** : `0x8003B2EC-0x8003B354` appelle `0x8003A1B8(0, i, 0)` pour i = 0..127 dans l'ordre et **s'arrête au premier enregistrement dont le premier mot (XMin, YMin, XMax, YMax) est nul** ; `0x80039BD0` rend 0
  quand `IsEnabled` est nul (corrige `e0-check.md` point 7). Aucun enregistrement réel ne suit un enregistrement vide dans les 483 cartes, et l'ordre des objets de la couche « Entities » exportée est l'ordre des `Index` dans les 483 cartes :
  l'ordre des tirages de la DLL est celui du binaire.
- [oracle, critique] **V8, chargement réel** (`critique/p8_loadmap.py`, boucle de chargement et vrai `0x8003A1B8` sur 128 enregistrements, graine `0xB017C93D`) : carte 14 à la tuile (8, 6) : 25 enregistrements appelés, 19 entités, 11 tirages, mot final
  `D5F4D64A` (entités à tirage : enregistrements 0 à 6 → objets 0, 71, 0, 0, 0, 0, 71 en 1, 2, 1, 1, 1, 1, 2 tirages ; 16 → 84 ; 17 → 0) ; cartes 390 et 392 (tuiles (0, 0) et (25, 29), même résultat) : 16 et 12 entités, 12 tirages, `BBE29AC9`. Sur 42 cartes de la suite (tuile arbitraire au centre de la fenêtre
  du premier enregistrement), 25 sont identiques au modèle qui ignore la fenêtre du héros ; les 17 autres diffèrent **par la fenêtre du héros** (carte 115 à la tuile (25, 29) : 0 entité), et **avec la fenêtre appliquée le modèle égale la boucle réelle sur les 42 cartes** (mot final et tirages, `critique/p12_window.py`) :
  le nombre de tirages au chargement dépend de la tuile du héros, donc les mots attendus se mesurent par couple (carte, tuile).
- [DLL] `AlundraEffectStage.RunFrame(int, IReadOnlyList<bool>)` n'a qu'un appelant de production, `AlundraWorldProxy.cs:2612` (`IsTransitionInProgress ? 0 : ticksThisFrame`), et 70 appels de test dans 15 fichiers ;
  `Update` n'a aucun `return` avant `:2612`. `RunActiveEffectsPass` (`:2931`) est vide et son commentaire (« le générateur … avancé par image ») est trompeur. `_effectStage` est un champ jamais nul.
- [DLL] `AlundraRandom` n'a que `Next()` et `Reset()` ; `RandomSeed` est un champ statique public `ulong` (état sur 64 bits, seuls les 32 bits bas comptent) ; aucune lecture de production, aucune réflexion, aucun autre projet ; `Reset()` n'est appelé que par les tests, `ArcRun` compris.
  Tirages de la DLL aujourd'hui : étincelle de charge (2), de coup (3 par étincelle créée, 9 au plus), de vaisseau (2) — **différés dans `RunFrame`** (ADR-0046, ADR-0048) ; opcodes `0x0C`, `0x8C`, `0x5A`/`0x5B` modes 4 et 5 et IA Beannoïde et Muruta — **immédiats**.
- [DLL] `ContentsItemId` (`AlundraEntityScriptProxy.cs:93`) n'a aucun lecteur de production ; `ContentsGameFlag` en a un (`0x9F`, `AlundraEventProgramRunner.cs:1602`), et le mapper le pose déjà avec le même masque que le binaire (`EntityRecordMapper.cs:174`).
  `AlundraGameState.GetFlag` choisit la banque par `0x8000` et le mot par `(f>>5)&0x3FF`, comme le binaire. Les trois créations sont des blocs `try` jumeaux : boucle de chargement (appel d'`InitializeBalance` en `:1026`), `SpawnEntityByRecordId` (`:3309`),
  `SpawnEntityBySpriteIndex` (`:3407`) ; le héros est adopté avant la boucle (la zone lit sa tuile).
- [DLL, critique] `IsFixedTimeStep` est faux et `AlundraLogicClock` rend `⌊accumulateur / 0,02 s⌋` ticks (4 au plus) par image rendue : sous 50 images par seconde des images de deux ticks reviennent régulièrement (à 30 images par seconde, l'accumulateur alterne 1 et 2),
  au-dessus de 50 seulement après une saccade (INFERENCE ; la fréquence réelle en jeu est INCONNUE). `ArcRun.OneFrame(0.02f)` donne exactement un tick ; le plancher d'un tick de la première image (`:795`) joue le rôle de l'appel `a0 = 1` du binaire quand cette image n'a qu'un tick.
- [tests, critique] La suite tourne sans parallélisme (`xunit.runner.json`). Dix-huit fichiers de `Alundra.Tests` touchent le flux. `RunCharge` (`AlundraHeroChargeArenaTests`) affecte la graine **après** la construction de l'arène : indifférent aux tirages du chargement et aux avances de construction (les autres tests d'arène sont à lire de la même façon par l'ombre).
  Les tests d'étage directs (`RunFrame` sans monde : `AlundraNativeEffectTests`, `AlundraHeroChargeTests`, `AlundraDestroyEntityBreakEffectTests`, `AlundraEffectOpcodeTests`…) ne passent pas le rappel : inchangés. Les tests d'oracle des Beannoïdes et des Muruta fixent la graine à chaque appel : inchangés.
  À risque (statique, à confronter à l'ombre) : `AlundraShipCorridorArcTests` (A5r : le `0x0C` de la 220ᵉ image), `AlundraPrefabArcSupportTests.ArcRun_ResetsTheSharedRandomStream…` (sa graine est lue juste après la construction), les arcs à IA réelle (14 hors arène, 44, 115, 116, 117) et l'arc de la 167 (`0x5A` mode 4).
- [DLL] Les 483 mondes exportés portent tous `AlundraWorldProxy` et sont tous des cartes (rien hors de `Maps/`) : l'avance ne tourne jamais dans un écran de titre ou de carte mémoire, comme le binaire.

**Écarts tranchés par la critique** (la découverte et les annexes antérieures sont corrigées ainsi ; les audits restent en lecture seule).
1. *Place de l'avance.* « N pas après `RunFrame` » (`e0-spawn.md` §7, `e0-check.md` point 6) est **REFUTÉ** : il casse l'invariance au groupement en ticks (`AlundraHeroChargeArenaTests`, cas à 3 ticks par image : l'étincelle du tick 89 tombe au deuxième tick de l'image
   88-90 et ne voit pas l'avance du tick 88). Un pas **sans condition** dans `RunFrame` casse `AlundraHeroChargeTests` (mot inchangé hors des ticks 85 et 89), `AlundraDestroyEntityBreakEffectTests` (`RunFrame(world, 2)` puis graine intacte) et `AlundraNativeEffectTests`.
   Un nom de passe de plus dans la boucle des ticks casse `AlundraTickStructureTests` (`OpenTick`). Retenu : un rappel facultatif par tick dans `RunFrame` (D-E0B-1).
2. *« `0x8B` tire une fois avant d'apparaître »* (relevé du binaire) est **REFUTÉ** : `0x8004033C-0x80040434` ne tire pas ; le « site » `0x80040440` est la fonction suivante (`0x80040438`, le branchement aléatoire) ; les seuls tirages de `0x8B` sont ceux du tirage de l'enregistrement créé.
3. *Le mapper* : la proposition « `RecordContents` et `HasRecord` posés par `EntityRecordMapper.Map` » (relevé de la DLL) est **REFUTÉE** : `Contents` est une clé volontairement non mappée (documentation de la classe, `:34-38` et `:87`) et
   `Map_UnmappedKeys_DoNotAffectAnyProxyField` en tire sa prémisse. Le code d'enregistrement se lit au site de création (`TryGetRecordInt(record, "Contents")`), le code d'en-tête se pose dans `ApplyHeaderInitialization` à côté de `BreakEffect` (D-E0B-5).
4. *Test H* (`AlundraSpawnBySpriteIndexInterfaceTests`, « liste fermée ») : l'inventaire dit qu'il faudra y ajouter `ContentsItemId` : **REFUTÉ**, il compare une liste de champs qui ne l'inclut pas et ne bouge pas ; y ajouter `ContentsItemId` serait faux (les deux créations ne tirent pas aux mêmes endroits du flux).
5. *« Réactiver les comparaisons des vitesses d'étincelles de c1 »* (`e0-check.md`, tâche 4 d'e0b) : **REFUTÉ** comme but d'e0b. L'oracle de c1 n'avance pas le générateur (`e20-c1/oracle/notes.md` lignes 48 et 110) : le mot de départ est `0x24D2C78A` (graine + deux tirages du butin du Beannoïde) ;
   avec l'avance, la DLL ne l'atteint jamais. Les vitesses restent non comparées ; leur formule est épinglée par `AlundraNativeEffectTests`.
6. *Modèle de `A5r`* : le nombre de tirages au chargement de la 392 est **exactement 12** (douze `0x82`, une rangée sans maillon), pas « 12 à 24 ». De la graine : tirage seul → direction 16 (l'épingle `0x02` casse) ; 12 tirages + 219 avances → 8, + 220 → 24 ; avance seule → 0 pour 219, 220 et 221 avances (l'épingle survit par coïncidence).
7. *« 0xFB tire une fois »* (`e0-spawn.md` §9) est **REFUTÉ** : 0xFA, 0xFB, 0xFC et 0xFF tirent une ou deux fois.
8. *« Rare en jeu »* (recommandation du relevé de la DLL pour les images de plusieurs ticks) n'est pas établi (voir le fait [DLL, critique] sur l'horloge) ; la recommandation d'accepter reste valable, sans l'adjectif.
9. *Moyens de l'oracle* : 168 mutants d'un mot du tirage (`0x80032968-0x800329FC`, `0x80032A40-0x80032B24`) : 119 tués, **les mêmes 119 par les vecteurs de la découverte et par ceux de la critique** ; les 49 survivants sont équivalents ou morts
   (stockage de pile, `nop`, arrondi à 32 dont seuls les 5 bits bas comptent à cause de `sllv`, `slti 0x100` pour un octet) sauf deux faiblesses de harnais, à corriger en T0 d'e0b2 : le « bruit » préécrit en `+0x40` (`0xDEAD`) a des octets hauts nuls (le mutant `sw zero,0x3f` survit), et aucun code ≥ `0x100` n'est essayé.
10. *Commentaires périmés* à corriger avec e0b1 : `AlundraWorldProxy.cs:2931` (l'avance n'est pas dans cette passe) et l'en-tête d'`AlundraHitPassTests` (« E20.e0 advances it per frame »).

**Décisions de conception (ADR à numéroter à l'écriture : l'ADR-0065 est réservé à E20.e0a3b, les suivants libres sont 0066 et 0067,
à revérifier).**
- **D-E0B-1** — L'avance est un rappel facultatif par tick de `AlundraEffectStage.RunFrame(ticks, frozenOfTick, Action? afterTick = null)`, appelé une fois par tick **après** `_reservoir.Tick`, que le tick soit gelé ou non ; `null` par défaut, donc les 70 appels de test
  directs ne bougent pas ; le monde passe un délégué statique (aucune allocation par tick) qui appelle `AlundraRandom.AdvanceFrame()` (même expression `ulong` que `Next()`, état 64 bits conservé). Ni après `RunFrame`, ni sans condition dans `RunFrame`, ni dans la boucle des ticks.
- **D-E0B-2** — Pas d'avance sans tick (image à 0 tick) ni pendant le départ d'un warp (`RunFrame` reçoit 0 tick) ; avance sous la porte `0x48`, dans les menus, le livre et l'inventaire (fait du binaire, défaut fidèle, Q-B4) ; la première image d'un monde compte au moins un tick (plancher existant, `:795`), donc au moins une avance, comme l'appel `a0 = 1` du binaire.
- **D-E0B-3** — Écarts documentés (acceptés par l'auteur le 2026-10-15, Q-B2) : (a) dans une image de plusieurs ticks, les tirages immédiats des ticks suivants (IA, `0x0C`, `0x8C`, butin) ne voient pas les avances intermédiaires, les tirages des effets, eux, les voient (intercalage exact) ; (b) le tick qui lève un warp n'avance pas
  (le binaire avance : une avance de moins par warp) ; (c) la première image d'un monde peut compter jusqu'à 4 ticks après une longue pause de chargement (INFERENCE ; le binaire : un seul, `a0 = 1`). Aucun placement n'est exact dans les trois cas sans déplacer la passe des effets dans la boucle des ticks (hors tranche).
- **D-E0B-4** — Le tirage est une classe pure `AlundraLootRoll` (table de 2 048 octets en données, comme `AlundraWeaponTables`) appelée par le monde, **pas** par le noyau statique de la fabrique (qui n'a ni l'état de la partie ni le chemin dégradé) ni par le mapper. Entrées : code d'enregistrement `& 0xFF`
  (lu au site de création), code d'en-tête (`HeaderContents`), `ContentsGameFlag` déjà posé, banques de `AlundraGameState`. Sorties : `ContentsItemId`, `ContentsGameFlag`, pas du générateur. Rien ne tombe.
- **D-E0B-5** — Un seul champ de proxy est ajouté : `HeaderContents`, posé par `ApplyHeaderInitialization` à côté de `BreakEffect` et copié par `Clone`. Pas de `RecordContents`, pas de `HasRecord`.
- **D-E0B-6** — Le tirage est la **dernière** instruction du bloc `try` de chacun des trois sites (après le son et la poussée de position ;
  à `SpawnEntityBySpriteIndex`, juste avant le bloc du marqueur `JoinsBirthTick` d'ADR-0065, qui ne peut pas échouer — révision 1 d'e0b2), pour qu'une création avortée (contrat d'erreur d'e0a2) ne consomme aucun tirage. Le héros ne tire pas (`Contents` 0, pas d'appel). Les réinitialisations `0x8003A048` et `0x8003ABCC` (qui retirent) ne sont pas portées (E14, O-E0B-2).
- **D-E0B-7** — `AlundraRandom.RandomSeed` devient une propriété (source-compatible : aucun `ref`, aucune réflexion) pour que l'ombre voie les affectations et les lectures ; `Next()` n'emploie que le champ privé. Les puits d'ombre d'e0b1 et d'e0b2 sont des classes de production inertes sans variable d'environnement, comme le canal d'ombre de la
  table de cases, et le restent après la clôture (précédent ADR-0064).
- **D-E0B-8** — Chaque épingle déplacée est réécrite par une **dérivation** (le test calcule la valeur attendue avec un pas local `s·0x7D2B89DD + 0xE06A02E7` à partir de la graine et du nombre de ticks et de tirages), jamais par une constante collée.

**Questions à l'auteur** : tranchées le 2026-10-15 (voir « Réponses de l'auteur » en tête de section). Pour mémoire, Q-B1 tranche
entre une re-semence d'`ArcRun` avant le chargement seulement (les tirages des chargements décalent les épingles des arcs : 12 pas sur la
392, `A5r` casse ; `ArcRun_ResetsTheSharedRandomStream` échouerait sur la 390) et une re-semence avant et après (retenue : seule l'avance
et les tirages des apparitions en cours d'arc déplacent les arcs, et un test dédié lit le mot d'avant la re-semence,
`ArcRun.SeedAfterTheLoad`).

**Prérequis et dépendants.** e0b1 : aucun prérequis technique (le plan indique E20.e0a1 et E20.e0a2, faites) ; elle ne dépend pas d'E20.e0a3b mais touche aussi `AlundraWorldProxy.cs` :
elle s'exécute après e0a3b (l'une après l'autre, dans le même checkout) et relit ses numéros de ligne au HEAD d'exécution. e0b2 : E20.e0a1, E20.e0a2 ✅ et e0b1 (ordre, non technique).
Dépendants : E20.f3, E20.f6 (e0b1) ; E20.e0c, E20.g2 (e0b2) ; l'avance ferme les points laissés par E20.f1a et E20.f1b (« l'avance du générateur par tick »).

---

#### 1.17.1 E20.e0b1 — L'avance du générateur, une fois par tick ✅ (faite et CONFIRMED le 2026-10-15, `c2952ef` à `f384135`, ADR-0066)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, deux points corrigés (FIX) : (1) le branchement du rappel appartient à une seule
phase, la phase A (paramètre de `RunFrame`, délégué du monde, `AdvanceFrame()` qui n'écrit que le registre virtuel) ; la phase B ne
fait qu'écrire l'état réel ; les tests de T2 rouges avant T3 et ceux verts par construction dès T1 sont nommés ; T3 et le retour
arrière suivent ce partage ; (2) l'ombre attribue **chaque** événement au cadre le plus extérieur d'`Alundra.Tests` par une remontée
de pile, comme le canal de la table de cases ; la liste prédite est celle des tests dont `divergent_draws + divergent_seed_reads +
ambient_draws > 0`, et l'acceptation comme les arrêts portent sur l'appartenance à cette liste, non sur la présence dans le fichier.
**Révision 2 (2026-10-15).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — les cas « 0 pas » de T2 (c) (image sans tick, départ de warp) passaient déjà en phase A, où l'avance
n'écrit pas l'état réel ; chacun est maintenant écrit dans la même assertion qu'un cas positif (au moins un pas réel attendu), donc
rouge avant T3, et les mutants de T4 qui les tuent sont nommés ; l'arrêt « vert avant T3 » ne vise que des tests qui attendent au
moins un pas. Nouvelle époque de relecture : une seule relecture de clôture suit.
**Relecture de clôture de la révision 2 (2026-10-15) : REVISE**, un point : le test d'inertie de T1 (« `AdvanceFrame()` sans effet »),
lu à la lettre, affirmait que `RandomSeed` ne bouge pas, ce que la phase B change ; **corrigé** (FIX, texte seulement) : le test
d'inertie ne vérifie que le puits (aucun fichier, aucune allocation, ni `V` ni compteurs touchés), **jamais** l'état réel, et il est
sauté quand l'ombre est active (lancement de la suite avec la variable) ; il ne bouge donc ni en A ni en B. Point non bloquant
repris : les constructeurs et `Dispose` des classes de test ont leur propre clé dans l'attribution ; une classe qui pose la graine
dans son constructeur garde `ambient_draws > 0` sur ses méthodes, et la justification écrite suffit. La règle de relecture ne permet
pas une relecture de plus sans l'auteur : la tranche est **en pause** jusqu'à son accord.
L'auteur a accordé une relecture de clôture de plus (2026-10-15) ; relecture de clôture : **READY** (2026-10-15).

**Prérequis.** Voir ci-dessus. Compte de tests de départ : à relever en T0 (4 815 à la clôture d'e0a3a).

**But.** Un pas du générateur par tick logique, en fin de tick, après les effets du tick ; aucun autre changement de consommation.

**Périmètre.** `Alundra/Scripts/AlundraRandom.cs` (`RandomSeed` en propriété, `AdvanceFrame()`), `AlundraEffectStage.cs` (paramètre facultatif), `AlundraWorldProxy.cs` (`:2612` et le commentaire de `:2931`), le puits `AlundraRandomShadow` (nouveau fichier),
`Alundra.Tests/` (nouveaux tests ; épingles déplacées et justifiées), `docs/plan-e20-annexe/e20-e0b/` (README, `oracle/`, `e0b1-shadow.txt`), un ADR.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : `RandomSeed` en propriété (D-E0B-7) ; le paramètre facultatif
  `RunFrame(int ticks, IReadOnlyList<bool> frozenOfTick, Action? afterTick = null)`, appelé une fois par tick après `_reservoir.Tick`
  (D-E0B-1) ; le monde passe son délégué statique à l'appel de production de `RunFrame` ; `AlundraRandom.AdvanceFrame()`, qui en
  phase A **n'écrit que le registre virtuel `V` de l'ombre** et ne fait rien quand l'ombre est inactive (variable absente) ; l'état
  réel `R` n'est jamais touché par l'avance.
- **Phase B** (T3) : `AdvanceFrame()` écrit aussi l'état réel (`R := pas(R)`, même expression `ulong` que `Next()`) ; commentaires
  corrigés. Rien d'autre ne change en B : ni le branchement, ni la place de l'appel, ni l'ordre des opérations de `RunFrame`.

**Hors périmètre.** Le tirage du butin (e0b2) ; déplacer la passe des effets dans la boucle des ticks ; le tick d'armement d'un warp ; les effets de transition 4, 5, 6 ; le flux de `rand()` de la bibliothèque (`AlundraLibcRandom`, inchangé) ; `RunActiveEffectsPass` reste vide.

##### Tâches

- ✅ **T0 — Prédiction et oracle** (commit de docs, avant tout code). `docs/plan-e20-annexe/e20-e0b/README.md` : les valeurs attendues ci-dessus (les six mots de l'avance, une avance par appel sur les dix lignes de la sonde, a0 sans effet sur le nombre de pas), la prédiction **qualitative** de la découverte
  (familles de tests à risque, voir « Acceptation ») et les comptes de départ ; la liste prédite **mesurée** s'y ajoute en T1, avant la phase B. `oracle/` : `s7_frame.py` et les outils copiés octet pour octet avec leurs sommes de contrôle ; sortie `frame_vectors.json` (10 lignes) ; deux exécutions au manifeste identique ; au moins 12 mutants d'un mot (constantes, polarité du `beqz s3`,
  test de la porte, résultat de `DisplayInventory`, magasin supprimé, effacement de `[0x800DC4C4]`), tous tués ou équivalents prouvés par écrit.
- ✅ **T1 — Phase A, le branchement et l'ombre, sans changement de comportement** (commit de code A, puis commit de docs du relevé). Le
  code de la phase A tel que décrit dans « Partage des phases » ; `AlundraRandomShadow` (inactif sans `ALUNDRA_RNG_SHADOW_LOG`). **Test
  d'inertie** (propre à la tranche) : sans la variable, `AdvanceFrame()` et les tirages n'écrivent aucun fichier, n'allouent rien et ne
  touchent ni `V` ni les compteurs de l'ombre ; il n'affirme **rien** sur `RandomSeed` (l'état réel, que la phase B fait avancer) et il
  est sauté quand l'ombre est active (le chemin du puits est lu une fois de la variable, comme pour `AlundraSlotShadow`) ; il ne bouge
  donc ni en A ni en B. **Prédicat exact** : `R` = l'état réel, `V` = ce que serait `R`
  avec l'avance. Une affectation de `RandomSeed` ou `Reset()` ouvre une époque (`V := (uint)valeur`) ; chaque `AdvanceFrame()` fait
  `V := pas(V)` ; chaque tirage réel fait `R := pas(R)` et `V := pas(V)` et est **divergent** ssi `V ≠ (uint)R` ensuite (le pas est une
  bijection modulo 2³², le multiplicateur étant impair : c'est exactement « le tirage rendrait un autre nombre en phase B ») ; une lecture
  de `RandomSeed` par un test (hors `Next()` et hors puits) est **divergente** ssi `V ≠ (uint)R`.
  **Attribution** : **chaque** événement (avance virtuelle, tirage réel, affectation ou `Reset()`, lecture par un test) est attribué au
  cadre le plus extérieur de `Alundra.Tests` par une remontée de pile **à l'événement**, comme `AlundraSlotShadow.Report`
  (`AlundraEntitySlotTable.cs`, vers `:82-108`) ; les compteurs sont tenus en mémoire par test et écrits (une ligne par test) quand le
  test attribué change et à la sortie du processus (la suite tourne sans parallélisme). Par test : `virtual_advances`,
  `divergent_draws` (et le type déclarant du premier tirage divergent : IA, opcode, effet, autre), `divergent_seed_reads`, `epochs`,
  `ambient_draws` (tirages attribués au test avant toute époque **de ce test** : état hérité de l'ordre des tests). Commande :
  `ALUNDRA_RNG_SHADOW_LOG=<scratch>/shadow.txt dotnet test Alundra.Tests/Alundra.Tests.csproj -c Release --no-build --blame-hang-timeout 300s`.
  **Liste prédite** : les tests où `divergent_draws + divergent_seed_reads + ambient_draws > 0` ; un test hors de cette liste ne lit du
  flux, en phase B, que des valeurs identiques à celles de la phase A, donc ne peut pas bouger du fait de l'avance. Fichier versé
  `e0b1-shadow.txt` (toutes les lignes) et liste prédite recopiée dans le README **avant** la phase B. Chaque test marqué `ambient_draws`
  reçoit une affectation explicite de la graine dans sa fixture, ou une justification écrite, avant B (si une affectation est ajoutée,
  l'ombre est relancée et le fichier versé de nouveau) ; une classe qui pose déjà la graine dans son constructeur garde
  `ambient_draws > 0` sur ses méthodes (le constructeur a sa propre clé) : la justification écrite suffit.
- ✅ **T2 — Tests** (`AlundraRandomPerTickTests`, dans `AlundraRandomStaticStateCollection`, `Reset()` au constructeur), écrits après T1 :
  **rouges avant T3** : (a) les six mots de l'avance (par `AdvanceFrame()` puis lecture de `RandomSeed`) ; (c) au niveau du monde : 1 tick →
  1 pas, 2 ticks → 2, `MenuOpen` (`0x48`) → 1 par tick, première image d'un monde → 1, deux mondes successifs sans double compte ; les
  deux cas « 0 pas » ne s'écrivent **jamais seuls** : une image sans tick suivie d'une image d'un tick → **exactement 1 pas au total** ;
  une image qui part en warp (`RunFrame` reçoit 0 tick) entre deux images normales d'un tick → **exactement 2 pas au total** (le mot
  attendu est calculé par un pas local) ; (d) l'ordre dans le tick : l'étincelle de charge des ticks 85 et 89 (C2-steel) tire avant l'avance de son
  tick, mot attendu calculé par un pas local, identique en images de 1, 2 et 3 ticks, mot final = graine avancée de (ticks comptés par la
  passe `inventory` + 4 tirages). **Verts par construction dès T1** (ils épinglent le branchement de la phase A ; les mutants de T4 prouvent
  qu'ils mordent) : (b) `RunFrame` appelle le rappel exactement `ticks` fois (0, 1, 2, 4), après le tick du réservoir, aussi sur un tick
  gelé, jamais pour 0 tick ; (e) `RunFrame` sans rappel n'appelle rien et n'avance rien (garde des 70 appels directs). Chaque test de (a), (c)
  et (d) attend au moins un pas réel ; un tel test vert avant T3 est un arrêt (il ne prouve rien).
- ✅ **T3 — Phase B** (commit de code B) : `AdvanceFrame()` écrit `R` (la seule ligne de comportement de la phase) ; commentaires corrigés
  (`RunActiveEffectsPass`, en-tête d'`AlundraHitPassTests`) ; suite complète ; **chaque test qui bouge appartient à la liste prédite**
  et est réécrit par une dérivation (D-E0B-8) ; un test de la liste qui ne bouge pas est noté avec la raison (la divergence n'atteint
  aucune assertion) ; rien d'autre ne change.
- ✅ **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre, dont un sur les épingles : liste prédite contre liste mesurée ; mutants du code de la DLL : au moins 8, tous tués — rappel non passé, passé avant le tick du réservoir, sauté sur un tick gelé, appelé une fois par image, appelé aussi pour une image sans
  tick (tué par le cas « image sans tick puis un tick » de T2 (c) et par (b)), `IsTransitionInProgress` inversé ou ignoré (tué par le cas
  « départ de warp entre deux images » de T2 (c)), plancher retiré, constantes).

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais, l'un après l'autre. Rejeu : oracle relancé deux fois sur une
copie (22 lignes PASS, `frame_trace.json` au md5 de la découverte, manifeste identique, 20 mutants tous tués) ; `Alundra.Tests` 4 852 en
Release et en Debug, zéro échec ; la ligne de la phase B retirée, la suite complète rend exactement les 15 tests (a), (c), (d) rouges et
rien d'autre (la phase A ne déplace rien, la phase B non plus) ; liste prédite recalculée depuis `e0b1-shadow.txt` (66 tests, identique au
README) ; aucun test existant modifié hors d'un commentaire (`AlundraHitPassTests`) ; ADR-0066 indexé ; `cmp` de la DLL identique.
Adversarial : 13 mutants du code de la DLL (dont un ajouté : rappel avant les opérations du tick), tous tués ; aucun nouvel appel de
`Next()`, `AlundraLibcRandom` intact, `RunActiveEffectsPass` vide ; ombre relancée sur le code de la phase B : `divergent_draws` et
`divergent_seed_reads` nuls partout (V = R).
**Constat de la mesure** : la prédiction qualitative de la découverte (« une douzaine de tests bougent », dont `A5r`) est démentie, aucun
test existant ne bouge ; les justifications par famille (H115, G116, `A5r`) sont des inférences de lecture, non mesurées assertion par
assertion. **Écart de chiffres** documenté : T2 (d) épingle les étincelles de maintien de l'arène aux ticks 83, 87 et 91 (les ticks 85 et
89 du plan sont ceux du montage d'`AlundraHeroChargeTests`), principe inchangé.
**Reportés** (P4) : les 12 mutants de T4 de l'exécuteur vivent dans un script du scratchpad (non livré ; l'adversarial les a refaits) ;
incident de procédure d'un verifier, sans effet : une sortie de l'oracle régénérée au bit près dans le dépôt et trois fichiers écrits puis
supprimés dans `D:/development/repo/` (absents, vérifié par la session principale).

##### Acceptation

- Oracle : dix lignes PASS ; manifeste identique sur deux exécutions ; au moins 12 mutants tués ou prouvés équivalents.
- DLL : tous les tests de T2 verts ; **les deux cas du test de groupement (2 et 3 ticks par image), les tests de groupement de `AlundraHeroAuraWorldTests`, `AlundraTickStructureTests` et les 70 appels directs de `RunFrame` ne bougent pas** ; `Alundra.Tests` en Release **et** en Debug : compte de départ plus les nouveaux, zéro échec ;
  **tests déplacés ⊂ liste prédite** (`e0b1-shadow.txt`, règle de T1), chacun justifié par sa dérivation ; `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` ; les cinq traces inchangées (`git diff --ignore-cr-at-eol --exit-code`) ; moteur, convertisseur et données sans diff.
- Prédiction à confronter (qualitative, de la découverte ; le chiffre « une douzaine » est une estimation de la critique, non mesurée) : au plus une douzaine de tests bougent, parmi `AlundraShipCorridorArcTests` (A5, A5r), les arcs des Beannoïdes et des Muruta réels (cartes 14 hors arène, 44, 115, 116, 117), l'arc de la 167 (`0x5A` mode 4) et les arcs des cartes à `0x0C`/`0x8C`.

##### Arrêts, budget, retour arrière

- **Arrêts** : la phase A fait bouger un test ; plus de 25 tests bougent (question à l'auteur : interrupteur d'`ArcRun` ou accord) ; un test de groupement, de structure ou un appel direct de `RunFrame` bouge ; un test **hors de la liste prédite** bouge ; un test de
  T2 (a), (c) ou (d) (tous attendent au moins un pas réel) vert avant T3 ; une avance observée là où le binaire n'en a pas ; un test existant à réécrire sans dérivation ; tout contact avec `CasaEngineMonogame/` ou `CasaEngine.Launcher/Program.cs`.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (`AdvanceFrame()` cesse d'écrire `R`) ; la phase A reste sans effet (le rappel n'écrit
  que le registre virtuel, le puits est inerte sans sa variable).

##### Résultats (2026-10-15)

- **Commits** : phase A `fcec5c6`, `fa1c15c`, `ce4be4b` ; phase B `3f00274` (T3) ; ADR-0066, README et ce plan (T4).
- **Comptes** : départ 4 833 ; phase A 4 837 (inertie + tests (b) et (e)) ; phase B **4 852** en Release et en Debug, zéro échec. `cmp` de `alundra-project/Alundra.dll` avec la DLL Debug : identique. Les cinq traces : inchangées (`git diff --ignore-cr-at-eol`).
- **T2 avant la phase B** : 15 tests rouges (a x6, c x6, d x3), 3 verts ((b) x2, (e)) ; après la ligne de la phase B : les 18 verts.
- **Liste prédite contre liste mesurée** : prédite 66 tests (`e0b1-shadow.txt`, README d'`e20-e0b`) ; **mesurée : 0 test existant ne bouge**, aucun hors liste, aucun test de groupement, de structure ni d'appel direct de `RunFrame`. Aucune épingle n'a donc été réécrite, donc aucune dérivation à consigner pour un test existant (D-E0B-8 s'applique aux nouveaux tests, qui calculent leurs mots par un pas local). La prédiction qualitative (« au plus une douzaine », dont `A5`, `A5r`) n'est pas confirmée : `A5r` ne bouge pas (l'épingle `DirectionAfter120 = 0x02` survit, note 6 de 1.17 : coïncidence connue). Les raisons par famille sont dans le README d'`e20-e0b` (les tirages divergents n'atteignent aucune assertion ; vérifié par la lecture des tests, pas assertion par assertion).
- **Écart de chiffres de T2 (d)** : les étincelles de maintien de l'arène de charge sont aux ticks 83, 87 et 91 (3 étincelles, 6 tirages), non 85 et 89 (montage de `AlundraHeroChargeTests`) ; le principe est inchangé.
- **Mutants de la DLL** : 12, tous tués (rappel non passé, avant le tick du réservoir, sauté sur un tick gelé, une fois par image, aussi pour une image sans tick, `IsTransitionInProgress` inversé et ignoré, plancher retiré, deux constantes, état réel non écrit, appel double) ; détail dans le README d'`e20-e0b`.
- **Commentaires corrigés** : `RunActiveEffectsPass` (`AlundraWorldProxy.cs`), en-tête d'`AlundraHitPassTests`.
- **Reste** : les deux verifiers frais (l'un sur les épingles : liste prédite contre liste mesurée) sont lancés par la session principale.

---

#### 1.17.2 E20.e0b2 — Le tirage du butin à la création ⏳

**Révision 1 (2026-10-15).** Relecture : **REVISE**, quatre points corrigés (FIX) : (1) les couples de V8 se mesurent sur la case que le
héros de la DLL occupe **pendant la boucle de chargement** (celle d'`AdoptPlayerPawn`, avant que l'arc ne pose la case de son `ArcSpec`),
et un test la vérifie ; (2) la seconde re-semence d'`ArcRun` et `SeedAfterTheLoad` passent en phase A, avant le relevé de l'ombre, et le
tirage virtuel avance le registre de l'ombre, jamais une copie de l'état réel ; (3) une seule ligne du noyau partagé de la fabrique est
permise (`HeaderContents`), le reste du noyau et tout appel du tirage depuis la fabrique ou le mapper restent interdits ; (4) l'appel de
l'assistant aux trois sites appartient à la phase A seulement, la phase B ne fait qu'écrire ; les tests rouges avant la phase B sont nommés,
chaque cas « sans tirage » est accolé à un cas qui tire, et les arrêts « la phase A fait bouger un test » et « un test nommé rouge vert
avant T2 » sont ajoutés.
**Révision 2 (2026-10-15).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — le contrôle « ligne d'écriture retirée » attendait (m1) à (m5) seuls, alors que T2 ajoute les
comparaisons de D-E0A-3 au test du chemin complet et réécrit les tests déplacés, qui dépendent aussi de l'écriture : l'ensemble rouge
attendu est maintenant fermé et nommé ; les comparaisons vont dans ce seul test, jamais dans l'assertion partagée avec les tests du
noyau d'e0a1. Nouvelle époque de relecture : une seule relecture de clôture suit.
**Relecture de clôture de la révision 2 (2026-10-15) : REVISE**, un point : T2 lisait les attendus de `+0x3C` et `+0x40` dans `contents`
de l'oracle d'e0a, qui est l'octet de code de l'en-tête (`header[0x1F]`), et parlait d'un « mot d'entrée du vecteur » qui n'existe pas ;
**corrigé** (FIX, texte seulement, vérifié dans `run_e0a.py` : `'loot': [loot_item, loot_flag]`, `SEED = 0x12345678` écrit avant chaque
apparition, `rng_out` relu après) : `p.loot[0]` → `ContentsItemId`, `p.loot[1]` → `ContentsGameFlag`, `rng_out` → le mot, graine
d'entrée `0x12345678` ; le lecteur `AlundraSpawnIndexOracle.cs` est étendu à `loot` et `rng_out` sans toucher aux comparaisons de
`SpawnProjectionAsserts.Check`. La règle de relecture ne permet pas une relecture de plus sans l'auteur, absent cette nuit (mode AUTO) :
la tranche est **en pause** jusqu'à son accord ; E20.e0a4 (ordonnée après e0b2), E20.g2 et E20.e0c attendent avec elle.

**Prérequis.** E20.e0a1 ✅, E20.e0a2 ✅, E20.e0b1 ✅ (ADR-0066 ; l'ombre `AlundraRandomShadow` et son registre virtuel `V` existent).
Q-B1 tranchée : `ArcRun` re-sème avant et après le chargement.

**But.** À chaque création (chargement de carte, `SpawnEntityByRecordId` : `0x2D`, `0x8A`, `0x8B` ; `SpawnEntityBySpriteIndex`), tirer `ContentsItemId` et poser `ContentsGameFlag` comme `0x80032A40`, dans l'ordre du binaire, sans rien lâcher. Les 54 numéros exclus de la comparaison par D-E0A-3 y rentrent.

**Fait propre à la tranche** (relevé par la relecture au HEAD `4c91e8f`, à relire à l'exécution) : la porte de zone de la boucle de
chargement (`ShouldSpawnRecord(…, PlayerEntity.TileX, PlayerEntity.TileY, …)`, `AlundraWorldProxy.cs` vers `:1002`) lit la case posée
par `AdoptPlayerPawn` (vers `:964`, `:2074-2088`) : la case de Nouvelle Partie (`AlundraGameState.CameraTileX/Y` = (33, 59), position
au centre de la case, `ClampToGround` ne touche que `PosZ`) ou la case de l'enregistrement d'arrivée. `ArcRun` ne pose la case de son
`ArcSpec` (`HeroTileX/Y`) qu'**après** `InitializeWithWorld` (`AlundraArcSupport.cs` vers `:238` puis `:279-285`), et seulement pour un arc
sans arrivée. Les couples mesurés par la critique (14 à (8, 6) ; 390 et 392 à (0, 0) et (25, 29)) ne sont donc pas ceux que la DLL charge.

**Périmètre.** `Alundra/Scripts/AlundraLootRoll.cs` (nouveau, pur), `AlundraEntityScriptProxy.cs` (`HeaderContents`, `Clone`),
`AlundraEntitySpawnFactory.cs` (**une seule ligne** dans le noyau partagé : `HeaderContents = header.Contents` à côté de `BreakEffect` dans
`ApplyHeaderInitialization`), `AlundraWorldProxy.cs` (les trois sites et un assistant privé), `AlundraRandomShadow.cs` (le tirage virtuel),
`Alundra.Tests/AlundraArcSupport.cs` (Q-B1 : seconde re-semence après `InitializeWithWorld` ; propriétés `SeedAfterTheLoad`, le mot lu juste
avant la seconde re-semence, et `HeroTileAtLoad`, la case du héros lue juste après `InitializeWithWorld`, avant que l'arc ne pose la sienne),
tests, `docs/plan-e20-annexe/e20-e0b/` (oracle, README, `e0b2-shadow.txt`), un ADR.

**Hors périmètre.** Le lâcher à la mort, son veto (`MpMax`, objets `0x26`, `0x51`, `0x52`) et `0x80032C7C` (e0c, avec C255 : un lâcher sans
C255 crée des objets inertes permanents) ; les réinitialisations qui retirent ; le héros ; **toute modification du noyau de la fabrique autre
que la ligne `HeaderContents`** ; **tout appel du tirage depuis la fabrique ou depuis le mapper** ; `EntityRecordMapper` (inchangé).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : `AlundraLootRoll` et ses tests ; `HeaderContents` (champ, `Clone`, la ligne du noyau) ;
  un assistant privé du monde, appelé **une fois par création** comme **dernière instruction du `try`** de chacun des trois sites, après
  `PushLogicalPositionToRoot` (site de chargement : à la fin du `try` ; `SpawnEntityByRecordId` : juste avant `return spawnedProxy` ;
  `SpawnEntityBySpriteIndex` : juste **avant** le bloc du marqueur `JoinsBirthTick`, qui reste la dernière écriture de la création comme le
  veut ADR-0065, et ne peut pas échouer) — une création avortée n'atteint donc jamais l'assistant ; en phase A l'assistant ne fait que le
  **tirage virtuel** : `AlundraLootRoll` rejoué sur le registre `V` de l'ombre (et non sur une copie de l'état réel), `V` avance de 0, 1 ou 2
  pas selon le tirage, et rien d'autre n'est écrit (ni `ContentsItemId`, ni `ContentsGameFlag`, ni l'état réel) ; sans la variable de
  l'ombre, l'assistant ne fait rien ; la seconde re-semence d'`ArcRun` après `InitializeWithWorld`, `SeedAfterTheLoad` et `HeroTileAtLoad`.
  La seconde re-semence est sans effet en phase A **si** rien ne tire pendant le chargement aujourd'hui (le mot après le chargement vaut la
  graine) : un test qui bouge à cause d'elle est un arrêt (ce fait serait faux).
- **Phase B** (T2) : l'assistant écrit `ContentsItemId` et `ContentsGameFlag` et avance l'état réel par le même tirage (l'ombre voit alors
  `V = R`). Rien d'autre ne change : ni la place des appels, ni `ArcRun`.

##### Tâches

- ⏳ **T0 — Prédiction et oracle** (commit de docs, avant tout code). `oracle/` : `s1_table`, `s2_choose`, `s3_roll`, `s5_spawn`,
  `s6_record`, `s8_veto` de la découverte, plus `critique/p1_roll.py` (6 000 vecteurs au hasard, graine `20261015`,
  `roll_fuzz_vectors.json` md5 `087b48f326be28c6eb90763d933d4054`, identique sur deux exécutions) et `critique/p8_loadmap.py` (V8 : boucle
  de chargement réelle), outils copiés octet pour octet avec leurs sommes de contrôle ; sorties : `choose_vectors.json` (2 626),
  `roll_vectors.json` (52), `spawn_vectors.json` (183), `record_vectors.json` (12), `roll_fuzz_vectors.json` (6 000), `v8.json` ; harnais
  corrigé (bruit préécrit `0xA5A5A5A5`, codes `0x100..0x1FF`) ; mutants : au moins 12 plus la batterie de 168 de la critique, survivants
  équivalents écrits un à un.
  **V8 par la case de chargement** : au moins 12 couples (carte, case) **tirés des `ArcSpec` de la suite**, dont les cartes 14, 390, 392,
  115, 116 et 167 ; pour chacun, `v8.json` donne la case du héros **au chargement** et la façon dont le test de la DLL l'atteint : (33, 59)
  pour un arc sans arrivée (la case de Nouvelle Partie d'`AdoptPlayerPawn`), la case de l'enregistrement d'arrivée pour un arc avec arrivée ;
  puis les entités créées dans l'ordre, leurs objets et `+0x40`, le nombre de tirages et le mot final. Les couples de la critique (14 à
  (8, 6), 390 et 392 à (0, 0)) sont relancés pour prouver que le harnais reproduit ses valeurs (`D5F4D64A`, `BBE29AC9`), sans valeur
  d'acceptation pour la DLL. README : ces valeurs, la prédiction qualitative (Q-B1 « avant et après » : les tirages des chargements ne
  déplacent plus les arcs ; restent les tirages des apparitions en cours d'arc, `0x2D`, `0x8A`, `0x8B` et l'interface ; au plus 3 tests
  selon la critique, estimation statique) et les comptes de départ (4 852 après e0b1).
- ⏳ **T1 — Phase A** (commit de code A, puis commit de docs du relevé), tel que « Partage des phases ». `AlundraLootRollTests` écrits
  d'abord et rouges sur un squelette qui compile, puis **verts à la fin de T1** (la classe est pure et sans appelant d'écriture) : ils
  rejouent **tous** les vecteurs. La suite complète en Release et en Debug ne bouge pas (hors des tests nouveaux). Puis l'ombre, sur ce
  commit (qui contient la seconde re-semence) : en tête de `e0b2-shadow.txt`, le prédicat — un tirage virtuel avance `V` de 0, 1 ou 2 pas
  calculés sur `V` ; une époque (affectation, `Reset()`, donc aussi la seconde re-semence) remet `V` ; tirages réels et lectures comme en
  e0b1 — ; par test, les compteurs d'e0b1 plus `virtual_rolls` (créations et pas virtuels) ; **liste prédite** = règle d'e0b1
  (`divergent_draws + divergent_seed_reads + ambient_draws > 0`), recopiée dans le README avant la phase B.
  **Tests de monde**, écrits en T1 après le code de la phase A, **rouges jusqu'à T2** (chacun attend au moins un objet écrit ou un pas réel
  du tirage) : (m1) carte 14, numéros 254 et 230 de la page globale et 79 de la page des cartes × {`12345678`, `4F1427FB`, `985CF02A`} :
  objet, `+0x40` et mot égaux aux neuf lignes de `spawn_vectors.json` ; (m2) chargement, pour chaque couple de V8 : `HeroTileAtLoad`
  égale la case du couple, les entités créées égalent la liste de V8, leurs objets et `+0x40` et `SeedAfterTheLoad` égaux à `v8.json` (un
  couple sans tirage s'accole, dans la même assertion, à un couple qui tire) ; (m3) drapeaux : cinq cas (banque persistante levée / non
  levée, temporaire levée, mauvaise banque, `0x0800` ramené à 0), chaque cas « levé, aucun tirage » accolé dans la même assertion à une
  création qui tire ; (m4) création avortée (faute après `AddEntity`, point d'accroche `CreationFaultHookForTests`) suivie d'une création
  qui tire : exactement les pas de la seconde ; (m5) le héros ne tire pas, accolé à une création qui tire. **Verts dès T1** :
  `AlundraLootRollTests` ; `SeedAfterTheLoad` égal à la graine sur un arc d'une carte **sans tirage au chargement** (la critique en compte
  sept, dont la 389), donc vert dans les deux phases ; le test d'inertie de l'assistant sans la variable de l'ombre (il ne vérifie que le
  puits, jamais l'état réel ni les champs du proxy, comme celui d'e0b1).
- ⏳ **T2 — Phase B** (commit de code B) : l'assistant écrit et avance l'état réel (seul changement) ; (m1) à (m5) passent au vert ; suite
  complète ; chaque test existant qui bouge appartient à la liste prédite et est réécrit par une dérivation (D-E0B-8) ; les comparaisons
  de `+0x3C`, `+0x40` et du mot (exclues par D-E0A-3) s'ajoutent au **seul** test du chemin complet de la carte 14,
  `AlundraSpawnBySpriteIndexInterfaceTests.B2_TheFullPath_On151NumbersOfTheGlobalPage_…` (graine `0x12345678` posée avant chaque apparition,
  comme l'oracle d'e0a, `run_e0a.py` ; attendus : `p.loot[0]` → `ContentsItemId`, `p.loot[1]` → `ContentsGameFlag`, `rng_out` → le mot ;
  le lecteur `AlundraSpawnIndexOracle.cs` est étendu à `loot` et `rng_out`, sans changer les comparaisons de `SpawnProjectionAsserts.Check`), dans une assertion propre à ce test, **jamais** dans
  `SpawnProjectionAsserts.Check`, partagée avec les tests du noyau d'e0a1 qui ne passent pas par l'assistant du monde ; les tests
  existants réécrits sont nommés dans les résultats de T2.
- ⏳ **T3 — ADR, plan, verifiers frais** (deux ; mutants du code de la DLL : au moins 12, tous tués — banque inversée, masque `0x7FFF`
  oublié, `& 0xFF` oublié, priorité du code d'enregistrement, repli sur l'en-tête, test du bit `0x80`, limite `0x62`, décalage de la rangée,
  `>> 28` changé, maillon non suivi, drapeau levé qui tire quand même, `+0x40` non écrit sans enregistrement, tirage placé avant l'abandon
  (tué par (m4)), le héros qui tire (tué par (m5)), assistant appelé deux fois par création).

##### Acceptation

- Classe pure : 2 626 + 52 + 183 + 12 + 6 000 vecteurs, **0 écart** ; la table de la DLL égale (md5) aux 2 048 octets de l'exécutable à
  l'octet `0x960C`.
- Monde : (m1) à (m5) verts ; pour chaque couple de V8, la case de chargement vérifiée et les valeurs égales à `v8.json` ; la ligne
  d'écriture de la phase B retirée, la suite rend **exactement** l'ensemble fermé : (m1) à (m5), `B2_TheFullPath_…` (ses comparaisons
  de D-E0A-3) et chaque test existant réécrit en T2 (nommés dans ses résultats, tous dans la liste prédite) ; rien d'autre (même preuve
  qu'e0b1) ; les tests du noyau d'e0a1 (`AlundraSpawnBySpriteIndexTests`, sans monde) sans diff.
- Suite : `Alundra.Tests` en Release et en Debug, compte de départ plus les nouveaux, zéro échec ;
  **`EntityRecordMapperTests.Map_UnmappedKeys_DoNotAffectAnyProxyField`, `AlundraSpawnBySpriteIndexInterfaceTests` (test H) et les tests
  du noyau d'e0a1 passent sans changement** ; `git diff` d'`AlundraEntitySpawnFactory.cs` : la seule ligne `HeaderContents` ; tests
  existants déplacés ⊂ liste prédite de `e0b2-shadow.txt`, chacun justifié par sa dérivation ; ombre relancée sur le code de la phase B :
  `divergent_draws` et `divergent_seed_reads` nuls (`V = R`) ; `cmp` de la DLL ; cinq traces inchangées ; moteur, convertisseur et
  données sans diff.

##### Arrêts, budget, retour arrière

- **Arrêts** : une image de V8 qui contredit le modèle (attribuer au harnais ou au binaire, puis question) ; la boucle de chargement de la
  DLL qui ne crée pas, **à la case de chargement vérifiée**, les mêmes entités que V8 ; la phase A fait bouger un test (dont la seconde
  re-semence) ; un test de (m1) à (m5) vert avant T2 ; un test existant qui bouge hors de la liste prédite ; plus de 10 tests existants qui
  bougent ; une modification du noyau de la fabrique autre que la ligne `HeaderContents`, ou un appel du tirage depuis la fabrique ou le
  mapper ; `EntityRecordMapper` à modifier ; un test existant à réécrire sans dérivation.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (l'assistant cesse d'écrire) ; la phase A (classe pure, champ, ligne du noyau, appel de
  l'assistant qui n'écrit que le registre de l'ombre, seconde re-semence d'`ArcRun`) ne change aucun comportement.

---

**Points ouverts ajoutés.**

| # | Question | Tranchée par |
|---|---|---|
| O-E0B-1 | Les effets de transition 4, 5, 6 (393 portails) tirent dans le même flux (1 200, 240 par pas, 600 tirages) ; la DLL les traite comme 0 (D-T-7) ; s'ils sont portés, ils tirent hors de toute avance | E19.m (transitions) |
| O-E0B-2 | `0x8003A048` et `0x8003ABCC` retirent le butin à chaque réinitialisation (C22) ; non portés | E14 |
| O-E0B-3 | Fréquence réelle des images de deux ticks ou plus, et première image après une longue pause (jusqu'à 4 ticks) | recette de l'auteur (compteur de l'ombre) |
| O-E0B-4 | Treize fonctions qui tirent (`0x80032F84`, `0x800330FC`, `0x800333AC`, `0x800335AC`, `0x8003382C`, `0x80033A2C`, `0x80034EC4`, `0x800354D0`, `0x80039188`, `0x80042FDC`, `0x800432A4`, `0x8004392C`, `0x80065204`) ne figuraient pas dans la liste antérieure des sites ; la plupart ne sont pas portées : chacune consommera le flux avancé (`0x80039188` est déjà porté : étincelles de coup) | tranches E20.e, E20.h, E14 |

### 1.18 E20.e0k — Pas vertical et vol des entités autres que le héros : e0k1 (état de la carte), e0k2 (Z logique et pas vertical), e0k3 (bord de la carte), e0k4 (conditionnelle) ⏳ (e0k1 faite et CONFIRMED le 2026-10-16, ADR-0068 ; détaillée le 2026-10-15)

**Réponses de l'auteur.** Aucune à ce jour (2026-10-15, mode AUTO, l'auteur absent). Q-K1 et Q-K2 ont chacune un défaut réversible,
**appliqué par la session principale jusqu'à la réponse de l'auteur** (option A dans les deux cas : périmètre étroit, aucun élargissement).
Aucune ne bloque e0k1. Ordre retenu par la session principale (révision 8 de l'enveloppe) : e0k1, e0k2, puis E20.f3c, puis e0k3.

Découverte en lecture seule du 2026-10-15, au HEAD `4a2b5a7` : trois relevés (binaire, impact sur la DLL, inventaire des tests), puis un critique adversarial. Le critique a redésassemblé les sites qui portent les décisions : la passe `0x80038364`, la force des non-héros `0x80036A84`, le rognage `0x800366FC`, `ComputeZ` `0x800375E0`, le test d'atterrissage `0x80036BFC`, le plafond `0x80036D94`, `ComputeXY` `0x80037730` avec son aimant, la fonction de blocage `0x800373E4`, le sol `0x80037F28`, les porteurs `0x800364C8`, `SetEntityDimensions` `0x80039C40` et les listes `0x800384F4`. Il a rejoué les 88 sorties de l'oracle du relevé binaire (manifeste identique, 20 000 cas du fuzz sans écart) et refait le recensement des mots d'en-tête des 483 cartes et des drapeaux des sprites. Il a écrit un modèle à part de la boucle d'affinage (20 000 tirages), relu le code de la DLL (pas vertical, racine, mur, bord, gel, marqueurs) et recompté la suite (309 fichiers, 2 525 faits et 354 théories). Scripts et sorties dans le scratchpad de la session : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20e0k/` (`bin/`, `dll/`, `tests/`, `critic/`). Aucune modification du moteur, du convertisseur ni des données. La DLL n'a pas été exécutée : ce qui la concerne est lu dans le code ou inféré. Les sondes de T2 d'e0k1 le mesurent.

**But.** Donner aux entités nées par l'interface (flèches, boules, vague, bombe, objets lâchés) l'état de carte, le Z logique, le pas vertical et les butées du binaire. Dans l'ordre : l'état dépendant de la carte (e0k1) ; le Z logique et le pas vertical (e0k2) ; le bord de la carte (e0k3). Les entités posées par la carte ne changent pas (Q-K1, défaut A).

**La prémisse de l'enveloppe est fausse, et la tranche rétrécit.** « Seul le héros a un pas vertical par tick » (§1.1, fait [DLL] et ligne 4) est faux. Seul le héros a un **état en l'air tenu par le tick** (`RunHeroVerticalTick`). Toute entité active a déjà un pas vertical : le chemin du contrôleur (`EvaluateEntitySupport`, 383 préfabs sur 395) ou le chemin sans contrôleur (`StepVerticalWithoutController`, 12 préfabs). Ce qui manque est plus étroit :
1. Une entité née par l'interface n'a pas les quatre lignes de l'état de carte. Une boule reste en l'air, une flèche traverse les murs, un rebond est annulé par le moteur.
2. Le Z d'une entité à contrôleur passe par la racine, qui tronque au pixel : l'unité brute est perdue (D-F3-8).
3. Les écarts de règle au binaire (ordre Z puis XY, atterrissage strict, aimant) existent aussi pour les entités par enregistrement. C'est E19.h2, hors de cette section.
4. Le rognage au bord n'est porté pour personne (O-E19-17).

**Découpage.**

| Sous-tranche | Contenu | Prérequis |
|---|---|---|
| E20.e0k1 | État de carte des entités nées par l'interface ; sondes de la DLL ; épingle P3 d'e0a3b | E20.e0a1 ✅, e0a2 ✅, e0a3b ✅ |
| E20.e0k2 | Marqueur « Z logique », pas vertical du binaire (Z avant XY, atterrissage strict, impulsion, plafond, aimant), règle de la hauteur de marche, gel et dégel | e0k1 |
| E20.e0k3 | Bord de la carte pour les entités marquées | e0k1 |
| E20.e0k4 | Conditionnelle (Q-K2 option B) : boucle d'affinage entière de `ComputeXY` pour les entités marquées | e0k2, e0k3 |

Ordre : e0k1, e0k2, e0k3 (e0k4 seulement si l'auteur la demande). Les dépendants n'exigent pas la même moitié : f3c exige e0k1 et e0k2 ; f4 et f5 exigent e0k1 à e0k3 ; f6 aussi, plus les attributs de tuile (O-E0K-2) ; g2 et h3 exigent e0k2 ; g1 exige e0k2 (il pose le marqueur à l'instant où il soulève une entité par enregistrement) ; h4 exige e0k1 à e0k3. Retenu par la session principale : f3c passe juste après e0k2, avant e0k3 (révision 8 de l'enveloppe).

**Faits porteurs.** Étiquettes : [binaire] décodé sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS ; [DLL] lu dans le code au HEAD `4a2b5a7` (numéros de ligne à relire à l'exécution : E20.e0b2 et E20.f3 touchent aussi `AlundraWorldProxy.cs`) ; [données] cartes extraites et export ; [inférence] déduit, non exécuté ; [critique] établi ou corrigé par la critique.
- [binaire, critique] **Une seule passe pour tous.** `0x80038364` : pré-passe (`+0x168`, `+0x140`, `+0x13C` mis à 0), porteurs `0x800364C8`, forces `0x80036828`, recopie des porteurs `0x80037364`, `MoveEntity` `0x80037E34` (Z `0x800375E0` puis XY `0x80037730`), passe de fin `0x80038064`. Liste A : statut 2 ou 3 et `+0x20` nul. Liste B (collidables) : `+0x6C & 0x80`, `+0xB4 & 0x80` nul, `+0x28` nul, **aucun test de statut**. Le héros ne diffère que par la branche de la passe des forces (adresse `0x80127D30`, `0x8003687C`) et par la fonction de blocage (`0x80037488`).
- [binaire, critique] **Loi de force des non-héros** (`0x80036A84-0x80036BBC`). Porté (`+0x28` non nul) : toutes les forces à 0. Impulsion `+0xF8` non nulle : `ForceZ = IZF << 8`, ou 0 si `IZF & 0xFFFF == 0x8000` sans gravité, sans décroissance. Sinon avec gravité (`+0x6C & 0x100`) : `F ← F − (G << 8)` puis borné des deux côtés à `±(V << 8)`, `G` et `V` lus **à chaque tick** dans l'en-tête de la carte (`*(0x800E4334)` `+4`, `+6`). Sinon `ForceZ` reste tel quel : c'est le vol à Z constant. Le décalage de l'octet `+8` de l'en-tête s'applique à tout le XY de toute entité (cartes 159 et 160, O-E19-31 : déjà connu).
- [binaire, critique] **Pas vertical** (`0x800375E0`). Montée (`F > 0`) : plafond à 1 920 px (`0x7800000` ramené à `0x77FFFFF`, contact), puis balayage des entités si sujet. Chute ou repos (`F ≤ 0`, zéro compris) : atterrissage **strict** `T ≥ ModZ + F` (`0x80036C20-0x80036C34`), puis `PosZ = T + 1 − ModZ`, `+0x140 = 1`, `ForceZ = 0` avec gravité ; un sujet se pose aussi sur le haut d'une entité. Sujet = `0x80`, `AnimFlags & 0x80` nul, `+0x28` nul : une flèche, une boule, la vague et une caisse lancée (C23 efface `0x80`) ne sont pas sujets.
- [binaire, critique] **Pas horizontal** (`0x80037730`). Boucle d'affinage par moitiés. `ForceAdjusted` n'est levé que si **aucun** sous-pas n'a été accepté et que `NoObstacleSlide` (`0x2000`), une entité touchée ou une glissade déjà faite le permet. Il est aussi levé par le rognage (passe des forces) et par `MoveEntity` pour un porté. L'aimant (`0x80037848-0x80037904`, gravité, `ForceZ == 0`, force XY non nulle) pose `PosZ = T + 1` si le sol est à moins de 3 px au-dessus (`d ≥ 0`, fenêtre `< 0x30000`) ou à moins de 3 px et 3 unités au-dessous. Il précède le test de blocage : c'est le **pas de marche** d'une entité à gravité. Une entité sans gravité, ou avec `ForceZ ≠ 0`, n'a aucune tolérance.
- [binaire] **Fonction de blocage non-héros** (`0x800373E4`) : un coin bloque si le mot de la cellule `& (0x40 | 0x01 si ClassB | 0x1000 si ClassA)` est non nul, ou si la hauteur du coin est `≥ ModZ`. Sol de fin de passe : `T + 1`, relevé pour un sujet ; `IsOnGround = !(sol < PosZ)`, sur `PosZ`.
- [binaire, critique] **Rognage** (`0x800366FC`) : le pas est borné à `[−(offX<<16) ; 0x4E00000 − ((offX+sizeX)<<16)]` en X et `[… ; 0x3C00000 − …]` en Y, et `ForceAdjusted` est levé **au même tick**, même si l'entité avance encore. La DLL stocke déjà `NegMod*` et `ScreenClip*` (`AlundraEntitySpawnFactory.cs:826-841`) sans les lire.
- [binaire, oracle] **Création.** `PosZ = Z_arg − offZ + 1`, relevé à `T + 1` (`0x80039E80-0x80039F2C`).
- [données, critique] **Drapeaux.** Flèches 3 à 6 : `0x012074` (ni collidable ni gravité, `NoObstacleSlide`). Flèche chargée 7 : `0x032054`. Boules 10 à 15 : `0x132154` (gravité, `NoObstacleSlide`, pas collidables). Vague 27 à 29 : `0x00205C` (ClassB, `NoObstacleSlide`, sans gravité). Bombe 2 : `0x13234E` (ClassB, gravité). Objets 30 à 64 : collidables à gravité, sans `NoObstacleSlide`. Caisse (carte 392, numéro 36) : `0x3323E4`. Beannoïde et Muruta : ClassA et gravité, sans `NoObstacleSlide`. En-têtes de cartes : 481 cartes à (128, 4096, 0), cartes 159 et 160 à (3, 256, 1).
- [oracle] **Vecteurs** (`k0_*.py` de la découverte binaire, rejoués par le critique). Boule du feu depuis Z + 32 px : Z = 31,5 ; 30,5 ; 29 ; 27 ; 24,5 ; 21,5 ; 18 ; 14 ; 9,5 ; 4,5 puis 0 au tick 11 (atterrissage, `+0x140` et `+0x144` à 1). Flèche : Z constant, y compris au-dessus d'un bord de plateau (48 px, 29 ticks). Mur : la flèche atteint X = 349 exactement ; `ForceAdjusted` au tick 43 ou 44 selon le départ. Flèche à Z + 16 px : traverse une marche de 16 px, s'arrête contre une marche de 32 px (tick 43). Boule contre une marche de 32 px : `ForceAdjusted` au tick 7, puis tombe au tick 11 ; sur une marche de 16 px : pose sur la marche au tick 8. Bord : tick 7, X = 1 237,0, drapeau le même tick. Caisse lancée (impulsion 1 024 depuis 32 px) : atterrit au tick 23 à plat, 21 sur une marche de 16 px, 18 sur 32 px ; mur ou marche de 48 px au tick 4. Muruta du hall : 128 px en 23 ticks, 160 px en 25.
- [critique] **Le drapeau de mur arrive 1 ou 2 ticks après le premier tick dont le pas entier ne passe plus.** Modèle écrit à part de la boucle d'affinage (20 000 écarts bruts tirés, 6 px par tick) : +1 tick dans 13 359 cas (66,8 %), +2 ticks dans 6 641 (33,2 %), jamais 0. Les familles de l'oracle donnent 6 fois +1 et 4 fois +2 sur 10 départs entiers. [DLL, inférence] La règle de la DLL (E19.a3, D-E19-12 : un pas raccourci qui avance ne lève rien) donne toujours +1.
- [DLL, critique] **Le pas vertical des non-héros existe.** `TickMotion` (`AlundraEntityScriptProxy.cs:1550-1571`) : XY (`TickScriptedNpc`) puis `EvaluateEntitySupport` (`:720-1039`). Chemin du contrôleur : atterrissage `<=` (`:940`), `IsOnGround` lu sur le contrôleur, `PosZ` rapatrié de la racine. Chemin sans contrôleur : atterrissage strict, plafond, `IsOnGround` par la règle `PosZ <= sol` (`:1053-1110`).
- [DLL, critique] **L'état de carte manque à l'interface.** `CreateEntityFromSpriteIndex` (`AlundraEntitySpawnFactory.cs:500-552`) n'a pas les lignes de `ApplySpawnInitialization` (`:679-703`) : mots de gravité de la carte, masque de marche, propriété du vertical, `MinMoveDistance`. L'appel du monde (`AlundraWorldProxy.cs:~3417`) ne passe que le champ. Le convertisseur exporte `Gravity`, `MaxFallSpeed` et `WalkabilityMask` à 0 (`SpriteWriter.cs:437, 480`). [inférence] Gravité nulle : `ForceZ` est borné à 0 à chaque tick (`:783-798`, plafond 0), la boule reste en l'air. Masque nul : toute cellule est marchable (`AlundraCellsCollisionField.cs:293-296`), la flèche traverse les murs de hauteur nulle. Sans propriété du vertical, le contrôleur du moteur ré-ancre un rebond qui monte.
- [DLL, critique] **Le va-et-vient de la racine.** `PushLogicalPositionToRoot` écrit la racine en pixels entiers (`>> 16`). `PullRootPose` (`:1409-1419`) et `MoveControllerAndPullPosition` (`:2384-2387`) remettent `PosZ` depuis la racine tant que l'entité n'est ni portée ni tenue. L'unité brute est perdue au premier pas horizontal. [inférence] Une flèche née à Z + 16 px au-dessus d'une marche de 16 px « atterrit » (`IsOnGround` à 1) ; le binaire a `PosZ = sol + 2 unités`, donc 0.
- [DLL] **Le modèle du héros existe.** `RunHeroVerticalTick` (`AlundraScriptedMotion.cs:239-388`) : Z avant XY, atterrissage strict, plafond, `FollowPosZOnRoot` (`:2396`), sentinelle de déclaration externe (`:75`), `StepHeight` à 0 quand `ForceZ ≠ 0` (`:382-384`). `SnapHeroAtApex` (`:401-434`) est l'aimant de 3 px (seuils `0x30000` et `0x30001`, la fenêtre du binaire dans le repère de la DLL).
- [DLL, critique] **`SlideAlongWall` réécrit `ForceAdjusted`** (0 quand l'axe a progressé : `:620`, `:640-644`) : un drapeau de rognage posé plus tôt dans le tick serait effacé.
- [DLL, critique] **Gel et dégel.** `AlundraGameplayFreeze.OwnerExternalVerticalDisplacement` (`:117-135`) rend `FinalForceZ / 65536` pour un non-héros : une entité marquée au repos (`F = 0`) serait ré-ancrée par le moteur au dégel. Il lui faut la sentinelle.
- [DLL, critique] **Marqueurs.** `JoinsBirthTick` (`:1335`) n'est jamais effacé ni cloné : c'est déjà « née par l'interface ». `EntityRefId == −1` est aussi celui du héros adopté (`AlundraWorldProxy.cs:2046`) et des entités recyclées (`:3207`).
- [DLL] **Aucun appelant de production** de `SpawnEntityBySpriteIndex`. Six fichiers de tests créent des entités nées par l'interface : `AlundraBirthTickProbePaTests` (5 méthodes), `AlundraBirthTickTests` (11), `AlundraEntitySlotV5RowsTests` (3), `AlundraEntitySlotWorldTests` (14), `AlundraSpawnBySpriteIndexInterfaceTests` (11), `AlundraSpawnBySpriteIndexTests` (12), soit 56 méthodes.
- [DLL] Les Muruta du hall (enregistrements 24 à 27 de la carte 115) tombent par le **chemin du contrôleur**, non par `StepVerticalWithoutController`. La chute n'est pas un arrêt d'E14.a1c (les 44 images de son arc comptent les attentes du script jusqu'à `@1469`).

**Écarts tranchés par la critique** (les relevés et le plan sont corrigés ainsi ; les audits restent en lecture seule).
1. *« Seul le héros a un pas vertical par tick »* (§1.1, ligne 4 et fait [DLL]) est **REFUTÉ**. Vrai du seul état en l'air tenu par le tick.
2. *Critère `EntityRefId == −1` de D-F3-8* est **CORRIGÉ** : il choisit aussi le héros et les entités recyclées. Marqueur dédié (D-E0K-1).
3. *Une caisse lancée naît de l'interface* (hypothèse implicite des trois relevés) est **REFUTÉ** : elle vient de la carte. Le marqueur doit pouvoir être posé par g1 au soulèvement.
4. *« Certains tests bougent à coup sûr »* (UJ1b, UJ1c, A10J, A10, A6, A3 : inventaire des tests) est **CORRIGÉ** : ils utilisent des entités par enregistrement ou un PNJ de banc d'essai. Ils ne bougent que si e0k absorbe E19.h2 (Q-K1, option B). L'ensemble fermé des tests qui peuvent bouger est les 56 méthodes ci-dessus.
5. *« La DLL accroche à 4 px, le binaire à 3 px »* est **PRÉCISÉ** : la fenêtre de 3 px est l'aimant, seulement pour une entité à gravité avec `ForceZ == 0` et une force XY, avant le test de blocage. Elle est déjà portée pour le héros.
6. *« `ForceAdjusted` arrive 1 ou 2 ticks après l'arrivée au mur »* (binaire) est **QUANTIFIÉ** : +1 dans 2 cas sur 3, +2 dans 1 sur 3 ; la DLL donne +1 (inférence).
7. *« Cartes 159 et 160 : découverte »* et *« rognage au bord : découverte »* sont **CORRIGÉS** : O-E19-31 et O-E19-17, déjà ouverts.
8. *Chute des Muruta* : **sans objet** pour e0k (hors marqueur, hors arrêt). La recommandation d'avancer une « part chute » d'e0k tombe. `plan-e14-ia-native.md:698` dit « `StepVerticalWithoutController` » : à corriger en « chemin du contrôleur ».
9. *Commentaire de `EntityFlags.NoRiders`* (« ne porte jamais de passagers ») : **inversé**. `0x4000` interdit le rôle de passager. Le code (`EntitySupport.cs:281, 304`) est juste. Le commentaire « 6 préfabs sans contrôleur » est périmé : 12 sur 395.
10. *Le Z logique se limite aux entités sans gravité* (repli B de f3c) est **ÉLARGI** : la même règle sert les entités à gravité. Une seule règle (D-E0K-4).

**Décisions de conception (ADR : le prochain numéro libre au moment de l'écriture, revérifié dans `docs/decisions/README.md`).**
- **D-E0K-1** — Le marqueur `HasLogicalZ` est posé par `SpawnEntityBySpriteIndex` seulement, dans le bloc du marqueur d'ADR-0065 (qui reste la dernière écriture de la création, après l'assistant de tirage d'e0b2). Jamais cloné, jamais posé par les créations par enregistrement ni pour le héros. Ce n'est ni `EntityRefId == −1` ni `JoinsBirthTick` (e0a4 peut l'étendre aux enregistrements). E20.g1 le pose sur une entité par enregistrement qu'il soulève. L'état de carte (e0k1) n'a pas besoin du marqueur : il est écrit à la création.
- **D-E0K-2** — L'état de carte est écrit par un assistant commun (`ApplyMapDependentState`), appelé par `ApplySpawnInitialization` (mêmes lignes, même ordre) et par le noyau sans enregistrement quand une carte est donnée. Le chemin par enregistrement ne change pas d'un octet.
- **D-E0K-3** — Une seule loi de force. Les lignes `:776-798` sont extraites en une fonction commune, appelée par le chemin du contrôleur et par le pas du Z logique. Pas de copie (le commentaire « un seul site de décroissance » reste vrai).
- **D-E0K-4** — Z logique d'une entité marquée (remplace D-F3-8, même effet, critère corrigé) : `PosZ` appartient au tick ; la racine le suit par un déplacement vertical (`FollowPosZOnRoot`) ; ni `PullRootPose` ni `MoveControllerAndPullPosition` ne rapatrient `PosZ` ni `IsOnGround` ; `IsOnGround = (PosZ ≤ sol)` dans le repère de la DLL (binaire − 1 unité). Le pas se fait avant le XY (`RunOneMotionTick`), avec la loi de D-E0K-3, l'atterrissage strict (`PosZ + ModZ + F < T`), le plafond absolu, le haut des entités pour un sujet, l'aimant de 3 px (corps de `SnapHeroAtApex`, sans modifier la fonction du héros), `TerrainHeight` recalculé au moment de l'appel. X et Y restent rapatriés (écart sous le pixel, O-F3-2).
- **D-E0K-5** — Hauteur de marche : `StepHeight` du contrôleur = la valeur du préfab seulement si l'entité a la gravité et `ForceZ == 0`, sinon 0 (S6 du héros généralisée ; le binaire n'a aucune tolérance hors de l'aimant). Une entité marquée déclare la sentinelle en l'air à chaque tick, y compris après chaque `Teleport` et chaque dégel ; le moteur ne l'ancre pas.
- **D-E0K-6** — Mur : le mover du moteur et `SlideAlongWall` (E19.a2, a3, h4) restent. L'écart de tick du drapeau (fait « 1 ou 2 ticks ») est documenté dans le code et ici (Q-K2 : défaut A). La sonde de T2 d'e0k1 le mesure sur les 12 départs de l'oracle.
- **D-E0K-7** — Bord : les entités marquées seulement ; le pas est borné avant le mover, le drapeau est levé le même tick et **conservé** à travers `SlideAlongWall` ; les entités par enregistrement et le héros restent sous D-E19-25 (O-E19-17).
- **D-E0K-8** — Hors périmètre, avec propriétaire proposé (liste en fin de section, O-E0K-1 à O-E0K-9).
- **D-E0K-9** — Pas de phase d'ombre. Le rayon d'action est borné par le marqueur : aucun appelant de production, six fichiers de tests créent des entités nées par l'interface. La liste prédite est statique et fermée sur les 56 méthodes de ces six fichiers ; une ombre ne mesurerait que celles-ci. La seule extraction qui touche le chemin large (e0k1, phase A, assistant de l'état de carte ; e0k2, phase A, loi de force) est gardée par la suite entière inchangée et par des mutants. Si l'auteur choisit l'option B de Q-K1, l'ombre d'e0b est le modèle de la tranche E19.h2.
- **D-E0K-10** — Chaque épingle déplacée est réécrite par une **dérivation** (valeur calculée dans le test, ou clé de l'oracle moins une unité brute), jamais par une constante collée (D-E0B-8).

**Questions à l'auteur** (aucune ne bloque e0k1 ; Q-K1 pèse sur e0k2).
- **Q-K1 — Qui suit exactement les règles de chute de l'original dès maintenant ?** *L'original :* tout suit la même règle (mouvement vertical avant l'horizontal, pose seulement quand le pas franchit le sol, « aimant » de 3 px en haut d'un saut). *Le port :* ce que la carte place (monstres, villageois, caisses, bombe de la carte 362) a une règle plus ancienne ; il peut se poser 1/50 s trop tôt dans un cas d'effleurement, et l'aimant est de 4 px. Ce qui naît pendant le jeu (flèche, boule, bombe lancée, objet lâché) n'existe pas encore. *Visible en jeu :* presque rien (1/50 s). *Option A (défaut, recommandée) :* seules les choses créées pendant le jeu suivent les règles exactes ; ce que la carte place garde l'ancienne règle jusqu'à E19.h2 (en pause). Une bombe lancée par le héros se posera comme l'original, la bombe posée par la carte non. Coût faible, réversible. *Option B :* tout d'un coup (E19.h2 absorbée). Environ 6 épingles sûres et jusqu'à 126 possibles à réenregistrer, les quatre questions de h2 à trancher, conflit de fichiers avec D5b en pause. Coût élevé.
- **Q-K2 — Le tick du « bloqué » d'une flèche contre un mur.** *L'original :* le signal « bloqué » arrive 1 tick (20 ms) après le dernier pas complet dans 2 tirs sur 3, et 2 ticks après dans le troisième (selon la fraction de pixel du départ). *Le port :* toujours 1 tick. *Visible en jeu :* rien ; la flèche rebondit ou meurt 1/50 s plus tôt dans un tir sur trois. *Option A (défaut, précédent D-E19-12) :* accepter l'écart, documenté. *Option B :* porter la boucle entière pour les entités marquées (e0k4, une tranche de plus, qui remplace le mover du moteur pour elles ; preuve par les 20 000 cas du fuzz).
- Sans question (défaut fidèle) : le rognage au bord pour les entités par enregistrement et le héros reste sous D-E19-25 ; les attributs de tuile et la dérive vont à leurs consommateurs (O-E0K-2, O-E0K-3).

**Prérequis et dépendants.** Voir le tableau de découpage. e0k1 : E20.e0a1 ✅ à e0a3b ✅ ; elle ne dépend ni d'E20.e0b2 (en pause) ni d'E20.f3, mais touche aussi `AlundraWorldProxy.cs` : elle s'exécute l'une après l'autre avec elles, dans le même checkout, et relit ses numéros de ligne au HEAD d'exécution. Dépendants : E20.f3c (règle du Z logique, remplace le repli B de Q-F3-1 ; D-F3-8 corrigé dans §1.19), E20.f4, E20.f5, E20.f6, E20.g1 (pose le marqueur), E20.g2, E20.h3, E20.h4, E20.e0c (objets lâchés à gravité ; e0k1 à e0k3), E20.e (par E20.e0c : le butin de l'herbe coupée). E20.f5 doit appeler `ResyncControllerFromFlags` quand E17 change les drapeaux (masque de marche ; **une assurance** : la découverte d'f4 à
f6 montre l'appel sans effet pour les drapeaux que change E17, §1.21). Révision 8 de l'enveloppe écrite (§1.1 : ligne 4 scindée en e0k1 à e0k3 ; prérequis ci-dessus).

---

#### 1.18.1 E20.e0k1 — L'état de carte des entités nées par l'interface, les sondes de la DLL ✅ (faite et CONFIRMED le 2026-10-16, `5430014` à `94b3cd2`, ADR-0068)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, quatre points : (1) la liste prédite des tests qui peuvent bouger était figée à six
fichiers au HEAD de la découverte ; e0k1 passe après f3a et f3b, qui ajoutent des tests nés par l'interface : la liste est recomptée en
T0 au HEAD de départ, et l'acceptation comme les arrêts d'e0k1 à e0k3 la citent (FIX) ; (2) (k1) nomme un mur que **seul le masque**
bloque (FIX) ; (3) l'épingle P3 d'e0a3b doit tuer le mutant M3 (FIX) ; (4) l'enveloppe révision 8 doit être READY avant toute exécution
(en cours ; l'exécution attend son verdict).
Seconde relecture : **READY** (2026-10-15).

**Prérequis.** Voir ci-dessus.

**But.** Les quatre lignes de `ApplySpawnInitialization` qui lisent la carte (mots de gravité et de viscosité, masque de marche, propriété du vertical, `MinMoveDistance` à 0) s'appliquent aussi à une entité née par l'interface. Le chemin par enregistrement ne change pas. Les sondes mesurent ce que la DLL fait, avant e0k2. L'épingle P3 d'e0a3b est posée.

**Périmètre.** `AlundraEntitySpawnFactory.cs` (assistant `ApplyMapDependentState` ; paramètre facultatif `TileMapData?` de `CreateEntityFromSpriteIndex`, nul par défaut), `AlundraWorldProxy.cs` (`SpawnEntityBySpriteIndex` : un argument), `Alundra.Tests/` (nouveaux tests ; commentaires périmés), `docs/plan-e20-annexe/e20-e0k/` (README, `oracle/`), un ADR.

**Hors périmètre.** Tout comportement du Z (e0k2), le bord (e0k3), les chemins par enregistrement, le héros, `Clone`.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : l'assistant extrait ; `ApplySpawnInitialization` l'appelle (mêmes lignes, même ordre) ; le noyau sans enregistrement accepte la carte facultative (nulle : rien) ; l'interface **ne la passe pas encore**.
- **Phase B** (T3) : `SpawnEntityBySpriteIndex` passe `_tileMapData`. C'est le seul changement de comportement, et le seul branchement.

##### Tâches

- ✅ **T0 — Prédiction et oracle** (commit de docs, avant tout code), pour les trois sous-tranches. `docs/plan-e20-annexe/e20-e0k/README.md` : les vecteurs de « Faits porteurs » avec leurs clés, les comptes de départ de la suite (à relever ; 4 852 après e0b1), les chiffres de la critique (68 / 33 % du modèle de mur). `oracle/` : les outils de la découverte binaire (`exe.py`, `k0lib.py`, `run_all.py`, `k0_*.py`, le dossier `oracle/`) et `residual_stats.py`, copiés octet pour octet avec leurs sommes de contrôle ; sorties en texte **et** en JSON ; deux exécutions donnent un manifeste identique (88 fichiers). **Liste prédite recomptée** au HEAD de départ (après f3a et f3b), écrite au README avant tout code : toutes les méthodes de
  `Alundra.Tests/` qui atteignent `SpawnEntityBySpriteIndex` ou `CreateEntityFromSpriteIndex` (directement ou par un support, dont
  `AlundraWeaponSpawnSeamTests` et les tests nouveaux d'f3a et d'f3b), avec leur compte ; elle remplace les « 56 méthodes des six fichiers »
  de la découverte pour e0k1, e0k2 et e0k3 (e0k2 et e0k3 la recomptent à leur tour si des tests nés par l'interface ont été ajoutés
  entre-temps). Au moins 12 mutants d'un mot sur les scénarios (blocage `≥` changé en `>`, fenêtres de l'aimant, test `0x2000`, test de gravité `0x100`, marqueur `0x8000`, `slt` de l'atterrissage inversé, plafond, `+1` de la création, bornes du rognage), tous tués ou prouvés équivalents un à un.
- ✅ **T1 — Phase A** (tests d'abord, rouges sur un squelette qui compile, puis le code). `AlundraMapStateTests` : (a) sur une carte à (128, 4096) : `MapGravityRaw`, `MapZViscosityRaw`, masque (flèche `0x40`, vague ClassB `0x41`, entité ClassA `0x1040`), `IsVerticalOwnedExternally`, `MinMoveDistance` ; chaque cas « sans carte, rien ne change » est accolé dans la **même assertion** à un cas avec carte ; (b) garde : une entité créée par enregistrement a les mêmes valeurs qu'avant (vert dès T1, par construction : extraction). La suite entière ne bouge pas.
- ✅ **T2 — Sondes de la DLL** (écrites, lancées au HEAD de départ, puis après la phase B, versées au README, puis retirées ; prédiction écrite avant). Chaque sonde est accolée à son témoin :
  - P1 : boule (11) née à Z + 32 px sur un champ plat. Prédiction : sans l'état, elle reste à 32 px (`ForceZ` borné à 0). Avec l'état, Z égal à l'oracle moins une unité brute jusqu'à l'atterrissage.
  - P2 : flèche (3) contre la colonne de mur 15. Prédiction : sans le masque, elle traverse ; avec lui, elle s'arrête en X = 349.
  - P3 : flèche à Z + 16 px au-dessus d'une marche de 16 px. Prédiction : `IsOnGround` = 1 dès le premier pas (unité perdue). C'est le test rouge d'e0k2.
  - P4 : tick du drapeau de mur sur les 12 départs de l'oracle (C1). Prédiction : toujours « +1 » ; le nombre d'écarts d'un tick alimente Q-K2.
  - P5 : entité montante (impulsion 1 024) avec et sans la propriété du vertical : annulée ou non par le moteur ?
  - P6 : écart entre la racine et le Z logique d'une boule qui finit sa chute (ancrage du moteur à moins de 4 px).
- ✅ **T3 — Phase B** (commit de code) et tests de monde, écrits **avant** B et rouges jusqu'à B (chacun attend un effet que la phase A ne produit pas) : (k1) la flèche née contre un mur **que seul le masque bloque** — un champ synthétique (support `ContactWorld` ou équivalent) qui reproduit la
  colonne 15 de l'oracle (`arrows2`, cas C1) : cellules au mot `& 0x40` non nul et de hauteur nulle, donc sous le pied de la flèche plus
  `StepHeight` ; un mur que la hauteur suffit à bloquer est **exclu** — s'arrête en X = 349, `ForceAdjusted` levé, jamais de l'autre côté ;
  le rouge avant B est la traversée (position au-delà du mur), consignée comme telle ; accolée dans la même assertion à la même flèche dans
  un couloir libre qui vole toute la distance ; (k2) la boule née à Z + 32 px tombe et se pose, accolée à un témoin « sans carte » qui reste en l'air ; (k3) sur la carte 159 (3, 256), la boule tombe à peine, accolée à la carte 14 ; (k4) les champs posés sur l'entité née et inchangés sur une entité de même sprite par enregistrement. **Vert par construction dès T1** : P3 d'e0a3b (une entité marquée qui change d'animation après la validation garde la clé de son animation courante).
- ✅ **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre). Mutants du code de la DLL, au moins 8, tués ou prouvés équivalents : assistant non appelé par l'interface, `_tileMapData` nul, masque calculé sur de mauvais drapeaux, propriété du vertical oubliée, `MinMoveDistance` oublié, mots gravité et viscosité permutés, assistant appelé deux fois, une ligne perdue du chemin par enregistrement (tuée par la garde de T1), et **M3** (la garde de D-E0A-8 dans
  `AlundraScriptedMotion.TickScriptedNpc` réduite à la seule condition du marqueur, sans `CurrentAnimationId == ~0u`) : tué par l'épingle P3
  d'e0a3b, rouge sous M3 et verte sans lui.

**Vérification (2026-10-15/16) : CONFIRMED** par deux verifiers frais, l'un après l'autre (rejeu : oracle relancé sur une copie, manifeste
de 88 fichiers identique, 18 mutants de l'oracle tués ; liste prédite recomptée ; suites complètes en Release et en Debug, 4 898, zéro échec ;
preuve de retrait : exactement K1 à K4 rouges ; aucun test existant déplacé ; M3 tué par la seule épingle P3 ; adversarial : mutants de
l'assistant tous tués sauf MG, équivalent prouvé ; aucun reste des sondes ; aucun diff hors périmètre).
**Décision de la session principale sur P1 (2026-10-16, avant e0k2).** La sonde P1 donne, pendant la chute, l'oracle moins **deux** unités
brutes (au repos : moins une). La cause est établie : la racine tronque la position au pixel à la naissance et `PosZ` en est tiré dès le tick 1,
ce qui perd l'unité brute de la création — le défaut même que corrige e0k2 (D-E0K-4). Le critère d'e0k2 reste donc « oracle moins **une**
unité brute » (ADR-0026) ; aucun test d'e0k1 n'épingle la série « moins deux ». P4 : dix départs (la famille C1 de l'oracle), non douze ;
drapeau toujours à l'arrivée + 1 dans la DLL, écart d'un tick avec l'oracle pour 5 départs sur 10 (2 sur les 6 départs entiers) : donnée pour
Q-K2 (défaut A en vigueur).
**Reportés** (P4) : MG (assistant appelé deux fois) est équivalent, d'où « tués ou prouvés équivalents » ; aucun test de monde n'épingle l'effet
de la propriété du vertical (seule la sonde P5 le montre ; e0k2 le couvre par (z4)) ; le résumé XML de `SpawnEntityBySpriteIndex` ne cite pas
la carte transmise ; une ligne « T2 ⚠️ (première moitié) » reste dans les résultats (T2 est close).

##### Acceptation

- Oracle : manifeste identique sur deux exécutions ; au moins 12 mutants tués ou prouvés équivalents.
- DLL : tous les tests de T1 et T3 verts ; (k1) à (k4) **rouges avant B** (nommés) ; `Alundra.Tests` en Release **et** en Debug, compte de départ plus les nouveaux, zéro échec ; **tests existants déplacés ⊂ la liste prédite recomptée au HEAD de départ de la tranche (T0 d'e0k1)**, chacun réécrit par dérivation ; M3 tué par l'épingle P3 d'e0a3b ; le chemin par enregistrement, le héros et les cinq traces inchangés (`git diff --ignore-cr-at-eol --exit-code`) ; `cmp` de la DLL ; moteur, convertisseur et données sans diff.

##### Arrêts, budget, retour arrière

- **Arrêts** : la phase A fait bouger un test ; (k1) à (k4) vert avant B ; un test hors de la liste prédite recomptée au HEAD de départ de la tranche (T0 d'e0k1) bouge ; plus de 10 tests existants bougent ; une sonde contredit une prédiction (écrire, puis décider avant e0k2) ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (l'interface cesse de passer la carte). La phase A ne change aucun comportement.

##### Résultats (exécution du 2026-10-16, première moitié ; phase A `b4f3938`)

- **T0 ✅** (`5430014`) : `docs/plan-e20-annexe/e20-e0k/` (README, `oracle/`). Manifeste de 88 fichiers identique sur deux exécutions (et identique à celui du critique) ; 18 mutants d'un mot, tous tués ; liste prédite recomptée au HEAD `9d5f5f2` : 7 classes, 62 méthodes, **70 cas** (les 56 méthodes de la découverte plus les 6 de `AlundraWeaponSpawnSeamTests`) ; départ de la suite **4 886** en Release et en Debug.
- **T1 ✅** (`b4f3938`) : `ApplyMapDependentState` extrait, paramètre facultatif de la carte, non passé par l'interface. `AlundraMapStateTests` (8 cas) : 4 rouges sur le squelette (état absent), verts ensuite ; la garde (b) et l'épingle P3 vertes par construction ; **M3 tué par P3**. Suite 4 894 sur 4 894 en Release et Debug, aucun test existant déplacé, `cmp` de la DLL identique, cinq traces inchangées.
- **T2 ⚠️ (première moitié)** : sondes P1 à P6 au HEAD de la phase A, chacune en A0 (comportement de départ) et A1 (état posé à la main, répétition de la phase B). **P1 avec l'état contredit sa prédiction** : la série de Z est celle de l'oracle **moins deux** unités brutes pendant la chute (moins une au repos), non moins une ; cause : la racine tronque au pixel (D-F3-8), que e0k2 corrige. Les autres sondes confirment (P2, P3, P4 : drapeau toujours à « arrivée + 1 » ; P5 : la montée est annulée sans la propriété du vertical) ou ne tranchent pas (P6 : écart de 0, le Z logique est tiré de la racine). Le détail est dans le README de l'annexe. **À trancher avant e0k2** (la phase B n'est pas bloquée) : la prédiction « oracle moins une unité » devient le critère de e0k2, et après e0k1 seule la série est « moins deux ».
- **T3 (tests avant B)** : `AlundraMapStateWorldTests` (k1 à k4) écrits et **rouges avant B pour la raison dite** (k1 : la flèche traverse le mur du masque, X maximal 457 px, aucun drapeau ; k2 : la boule ne se pose jamais ; k3 : la boule ne tombe pas non plus sur la carte 14 ; k4 : la flèche née a les valeurs par défaut) ; la répétition de la ligne de branchement les rend verts (non conservée). Ils sont commités avec la phase B.
- **T3 ✅ (phase B, `81b68b5`)** : `SpawnEntityBySpriteIndex` passe `_tileMapData` (une ligne). (k1) à (k4) verts ; suite **4 898 sur 4 898** en Release et en Debug (4 894 + 4), **aucun test existant déplacé** (la liste prédite de 70 cas n'est pas touchée, aucune épingle à réécrire : D-E0K-10 sans objet), `cmp` de `Alundra.dll` identique, cinq traces inchangées. **Preuve de retrait** : la ligne retirée (copie neutralisée), la suite entière en Release donne exactement **4 échecs, (k1), (k2), (k3), (k4)**, rien d'autre ; fichier restauré par `git checkout --`, `git status` revenu.
- **T2 (seconde moitié) ✅** : sondes P1 à P6 rejouées sur le code de la phase B (jetables, retirées) : l'entité née par l'interface (A0) donne désormais exactement les sorties de la répétition manuelle de la phase A (A1) pour P1, P2/P4, P3, P5, P6 (comparaison ligne à ligne). **Aucune nouvelle contradiction** : P1 reste « oracle moins deux unités brutes » (cause : la racine, e0k2) ; P2 s'arrête à X = 349,0 ; P3 `IsOnGround` 1 dès le tick 1 (test rouge d'e0k2) ; P4 drapeau au tick 43 pour les 10 départs (écart d'un tick avec l'oracle : 5 sur 10 tels que donnés, 2 sur 6 départs entiers) ; P5 montée conservée (propriété du vertical vraie, atterrissage au tick 17) ; P6 écart 0 aux 30 ticks. Tableau avant/après dans le README de l'annexe. La décision de P1 reste à prendre avant e0k2.
- **T4 🧪 (mutants et ADR ; les deux verifiers frais sont à lancer par la session principale)** : ADR-0068 (`docs/decisions/`). Mutants réels du code de la DLL (script `mut_e0k1.py`, build neutralisé, filtre `AlundraMapState`) : MA assistant non appelé par l'interface (8 rouges : (a) deux cas, (k1) à (k4)) ; MB `_tileMapData` nul ((k1) à (k4)) ; MC masque sur de mauvais drapeaux ((a), (b)) ; MD propriété du vertical oubliée ((a), (b), (k4)) ; ME `MinMoveDistance` oublié ((a), (b), (k4)) ; MF mots gravité et viscosité permutés (10 rouges) ; MH appel perdu du chemin par enregistrement ((b), (k4)) ; MI ligne du masque perdue ((a), (b), (k1), (k4)) ; **M3** (garde de D-E0A-8 réduite au marqueur) : **un seul rouge, l'épingle P3 d'e0a3b** (`MarkedEntityPins.P3_...`), vert sans M3. **MG** (assistant appelé deux fois) **survit : équivalent prouvé** (le helper n'écrit que des affectations de valeurs qui ne dépendent que de la carte et des drapeaux, et `ApplyGravitySettingsToController` affecte deux constantes ; aucune lecture-modification-écriture). 9 tués, 1 équivalent.

---

#### 1.18.2 E20.e0k2 — Le Z logique et le pas vertical des entités marquées ⏳ (en cours : T0 et T1 faites)

**Révision 1 (2026-10-16).** Relecture : **REVISE**, quatre points corrigés (FIX), vérifiés dans le code : (1) W2 doit aussi sauter, pour
une entité marquée, le bloc de la loi de force de `EvaluateEntitySupport` (`AlundraEntityScriptProxy.cs`, vers `:774-798`) et l'écriture de
`TickForceZ` (vers `:802-805`), sinon la loi s'applique deux fois par tick ; le pas logique écrit `TickForceZ` ; (2) l'aimant et `IsOnGround`
agissent **après** le pas horizontal, comme chez le héros (`SnapHeroAtApex` et `ZHeldByTick`, `AlundraScriptedMotion.cs` vers `:207-218`) :
le pas logique a deux points d'appel ; un test de monde (z9) épingle l'aimant ; (3) un T0 recompte la liste prédite au HEAD de départ
d'e0k2 (les tests d'e0k1 y entrent) ; (4) l'arrêt sur l'écart de racine porte sur une mesure faite dans e0k2, après la phase B ; l'arrêt sur
P3 d'e0k1 est satisfait (P3 a confirmé l'unité perdue).
**Révision 2 (2026-10-16).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — la sentinelle après un `Teleport` n'avait aucun test : le seul `Teleport` de la DLL est dans
`PushLogicalPositionToRoot`, qui remet le verrou vertical à 0, et W2 fait rendre la main à une entité marquée avant la redéclaration de
`EvaluateEntitySupport` ; les opcodes `0x64`, `0x65` et `0x8B` l'appellent en cours de vie. La redéclaration pour une entité marquée se fait
dans `PushLogicalPositionToRoot` (même fichier, dans le périmètre ; les opcodes restent hors périmètre), et le test de monde (z10) l'épingle.
Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-16).

**Prérequis.** E20.e0k1.

**But.** D-E0K-1, D-E0K-3 à D-E0K-5. Une entité née par l'interface garde son unité brute, ne revient pas de la racine, se pose comme le binaire (Z avant XY, atterrissage strict, plafond, aimant), et le moteur ne l'ancre pas.

**Périmètre.** `AlundraEntityScriptProxy.cs` (marqueur ; extraction de la loi de force ; garde de `EvaluateEntitySupport`, de `PullRootPose` et de `MoveControllerAndPullPosition`), `AlundraScriptedMotion.cs` (deux appels dans `RunOneMotionTick`, avant et après le XY ; fonctions du pas logique ; règle de `StepHeight`), `AlundraGameplayFreeze.cs` (sentinelle), `AlundraWorldProxy.cs` (le marqueur), `Alundra.Tests/`, `docs/plan-e20-annexe/e20-e0k/`, un ADR.

**Hors périmètre.** Le chemin du héros (aucun diff de `RunHeroVerticalTick` ni de `SnapHeroAtApex`) ; les entités par enregistrement (E19.h2) ; le porté `+0x28` et les passagers (g1) ; les plafonds formés par des entités (O-E0K-6) ; les attributs de tuile (O-E0K-2) ; la boucle d'affinage (e0k4).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : le champ `HasLogicalZ` (non cloné), posé par l'interface dans le bloc du marqueur d'ADR-0065 (**aucun lecteur**) ; l'extraction de la loi de force (D-E0K-3), comportement conservé ; le pas logique en **deux fonctions**, **non appelées par le tick** : avant le XY (loi de force, écriture de `TickForceZ` avec la force du tick avant l'atterrissage, pas vertical, plafond, atterrissage strict, haut des entités pour un sujet, sentinelle) et après le XY (aimant de 3 px avec le pas XY de ce tick et le terrain à la position d'arrivée, puis `IsOnGround = (PosZ ≤ sol)`) ; tests directs de ces fonctions sur une entité marquée appelée à la main.
- **Phase B** (T3) : les quatre branchements, ensemble, car ils n'ont de sens qu'ensemble : (W1) les deux appels du pas logique dans `RunOneMotionTick` pour une entité marquée : la première fonction avant `RunOneKinematicTick`, la seconde après (au même endroit que `SnapHeroAtApex` et la dérivation de `IsOnGround` du héros) ; (W2) pour une entité marquée, `EvaluateEntitySupport` **saute** le bloc de la loi de force (vers `:774-798`) et l'écriture de `TickForceZ` (vers `:802-805`), garde les écritures de `TerrainHeight` et des hauteurs du sol (`UpdateEnemyGroundHeights`, vers `:844-850`), puis rend la main ; (W3) `PullRootPose` et `MoveControllerAndPullPosition` ne rapatrient ni `PosZ` ni `IsOnGround` ; (W4) règle de `StepHeight`, sentinelle redéclarée **dans `PushLogicalPositionToRoot`** pour une entité marquée (après son `Teleport`), et `OwnerExternalVerticalDisplacement` qui rend la sentinelle au dégel.

##### Tâches

- ✅ **T0 — Liste prédite et oracle de l'aimant** (commit de docs, avant tout code). Au README de l'annexe : la liste recomptée au HEAD de
  départ d'e0k2 de toutes les méthodes de `Alundra.Tests/` qui atteignent `SpawnEntityBySpriteIndex` ou `CreateEntityFromSpriteIndex` (les
  tests d'e0k1, `AlundraMapStateWorldTests` (k1) à (k4) et ceux d'`AlundraMapStateTests` qui y passent, compris), avec son compte ; elle
  remplace la liste d'e0k1 pour l'acceptation et les arrêts d'e0k2. À l'oracle (copie du scratchpad, deux exécutions au manifeste
  identique, sorties ajoutées à l'annexe) : deux scénarios de l'aimant pour une entité à gravité, `ForceZ == 0`, avec une force XY, qui
  arrive sur un terrain 3 px plus haut (aimant : posée dessus) et 4 px plus haut (pas d'aimant), clés `magnet-3px` et `magnet-4px` ; les
  scénarios `walker-ramp-up` et `walker-ramp-down` existants servent de témoins.
- ✅ **T1 — Phase A** (tests d'abord, puis le code). `AlundraLogicalZStepTests`, chaque cas « sans effet » accolé dans la même assertion à un cas positif : (a) le marqueur est posé par l'interface et par elle seule : héros adopté, entité recyclée et créations par enregistrement sans marqueur, créée par l'interface avec marqueur, clone sans marqueur ; (b) la fonction, appelée à la main : boule (vecteur `arrows-ball-fire`, Z de l'oracle moins une unité brute, atterrissage au tick 11, `+0x140`), flèche (Z constant, `IsOnGround` 0 avec l'unité, 1 à `sol + 0`), impulsion 1 024 depuis 32 px (série de `crate-flat`, atterrissage au tick 23), cas d'effleurement exact (la somme vaut le sol : atterrit au tick suivant ; accolé à un cas non exact), plafond de 1 920 px, aimant de 3 px (seuils `0x30000` et `0x30001`, accolé à un cas à 4 px), plus 0x8000 sans gravité. Vert à la fin de T1.
- ⏳ **T2 — Tests de monde**, écrits après T1, **rouges avant T3** (la phase A ne branche rien), à 1, 2 et 3 ticks par image : (z1) une entité née sans gravité à la hauteur du héros garde `PosZ = héros + 1 unité` et `IsOnGround` 0 pendant 30 ticks (c'est la sonde de f3c : elle passe de 2 ticks de vie à 26 par la règle), accolée à la même entité par enregistrement, inchangée ; (z2) flèche à Z + 16 px au-dessus d'une marche de 16 px : `IsOnGround` 0 pendant 20 ticks, et contre une marche de 32 px : `ForceAdjusted` au tick 43 (oracle `arrow-h1`, `arrow-h2`) ; (z3) boule : série de Z de l'oracle **dès le tick 1** (31,5 px moins une unité brute ; une double décroissance
  la fausse), atterrissage au tick 11, `ForceAdjusted` au tick 7 contre la marche de 32 px (`ball-step32`), pose au tick 8 sur la marche de 16 px (`ball-step16`) ; (z4) entité à gravité lancée par une impulsion de 1 024 : atterrissage au tick 23 à plat, 21 sur 16 px, 18 sur 32 px ; (z5) gel puis dégel en l'air : le Z continue, sans ancrage, accolé à un PNJ par enregistrement inchangé ; (z6) la racine suit le Z logique sans que le moteur la déplace entre deux ticks ; (z7) la règle de `StepHeight` : 0 en l'air, la valeur du préfab au repos avec gravité ; (z8) invariance au groupement : mêmes séries à 1, 2 et 3 ticks par image ; (z9) aimant : une entité marquée à gravité, `ForceZ == 0`,
  avec une force XY, posée sur un terrain 3 px plus haut (clé `magnet-3px`), accolée dans la même assertion à la même entité devant un
  terrain 4 px plus haut, non posée (clé `magnet-4px`) ; (z10) téléportation en cours de vie : une entité marquée en l'air, déplacée par
  une écriture de position scriptée (`PushLogicalPositionToRoot` appelé directement, ou l'opcode `0x64`), puis une mise à jour du moteur
  sans tick logique : elle n'est pas reposée au sol par le moteur ; accolée dans la même assertion à une entité par enregistrement,
  inchangée.
- ⏳ **T3 — Phase B** (commit de code B) : les quatre branchements. Les tests de T2 passent au vert ; suite complète.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre, dont un sur les épingles : liste prédite contre liste mesurée). Mutants, au moins 12, tous tués : marqueur posé sur le héros ou sur les créations par enregistrement, marqueur sur `EntityRefId == −1` (tué par le héros et l'entité recyclée de (a)), marqueur cloné, loi de force appliquée deux fois, retour anticipé de W2 avant l'écriture de `TerrainHeight`, `PosZ` rapatrié, `IsOnGround` lu sur le contrôleur, atterrissage `<=`, plafond changé, aimant de 4 px, aimant placé avant le XY (tué par (z9)), `IsOnGround` dérivé avant le XY, `StepHeight` inversé, sentinelle perdue après
  un `Teleport` (tué par (z10)), dégel qui rend `FinalForceZ`, Z après XY ; la loi appliquée deux fois doit être tuée par un test de **monde** ((z3)),
  pas seulement par T1.

##### Acceptation

- DLL : tests de T1 et T2 verts ; (z1) à (z10) rouges avant T3 (nommés) ; `Alundra.Tests` en Release **et** en Debug, zéro échec ; tests déplacés ⊂ la liste prédite recomptée au T0 d'e0k2, chacun réécrit par dérivation ; **preuve de retrait** : les branchements
  retirés, exactement (z1) à (z10) et les épingles réécrites rouges ; l'écart racine / Z logique mesuré par (z6) et par une reprise de P6
  après la phase B, versé aux résultats ; aucun diff du héros (`RunHeroVerticalTick`, `SnapHeroAtApex`, `AlundraPlayerManager`) ; traces inchangées ; `cmp` de la DLL ; moteur, convertisseur et données sans diff.

##### Arrêts, budget, retour arrière

- **Arrêts** : un test hors de la liste prédite bouge ; plus de 10 tests existants bougent ; la phase A fait bouger un test ; un test de (z1) à (z10) vert avant T3 ; après la phase B, (z6) ou la reprise de P6 montre un écart de plus de 1 px entre la racine et
  le Z logique, ou un déplacement de la racine par le moteur entre deux ticks (arrêt ; question : `GroundSnapDistance` à 0 pour les
  marquées) ; (l'arrêt sur P3 d'e0k1 est satisfait : P3 a confirmé l'unité perdue) ; un diff du chemin du héros ou des entités par enregistrement ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (les quatre branchements, dont les deux appels de W1). La phase A ne change aucun comportement : le marqueur n'a aucun lecteur, l'extraction conserve le comportement.

##### Résultats (exécution du 2026-10-16, phase A)

- **T0 ✅** : départ au HEAD `5c5f70a`, suite **4 898** en Release et en Debug, `cmp` de la DLL identique, cinq traces inchangées. Liste prédite recomptée (README de l'annexe) : 9 classes, **70 méthodes, 82 cas** (la liste d'e0k1 plus `AlundraMapStateTests` et `AlundraMapStateWorldTests`). Oracle de l'aimant (`k0_magnet.py`, clés `magnet-3px` et `magnet-4px`) : deux exécutions, manifeste identique (93 fichiers, les 88 d'e0k1 inchangés plus 5), M12 le tue. **Écart avec la lettre de (z9)** : à 4 px le premier essai est refusé puis le pas est raffiné (4 unités brutes de moins) et l'entité finit le tick sur le terrain ; la différence 3 px / 4 px est le déplacement du tick 1 (pas entier contre pas raccourci), non la hauteur en fin de tick. Détail au README de l'annexe.
- **T1 ✅** (phase A) : `AlundraLogicalZStepTests` (9 tests : (a) le marqueur, (b) B1 à B8). **Rouges sur un squelette qui compile** (le champ `HasLogicalZ` déclaré mais jamais posé, les deux fonctions du pas logique sans corps) : 9 sur 9 rouges, (a) parce que l'entité née par l'interface n'a pas le marqueur, B6 parce que l'aimant ne fait rien, B1 à B5, B7 et B8 parce que la première moitié du pas rend faux (`Assert.True` en tête des cas). Puis le code : `HasLogicalZ` (non cloné, posé par `SpawnEntityBySpriteIndex` dans le bloc du marqueur d'ADR-0065, aucun lecteur), la loi de force extraite en `ApplyVerticalForceLaw` (un seul site de décroissance, `EvaluateEntitySupport` l'appelle), le pas logique en deux fonctions de `AlundraScriptedMotion` (`RunLogicalZPreXyStep` : loi, `TickForceZ`, pas vertical, plafond absolu sans les autres entités, atterrissage strict, haut des entités pour un sujet, `FollowPosZOnRoot`, sentinelle ; `RunLogicalZPostXyStep` : aimant de 3 px, copie du corps de `SnapHeroAtApex`, puis `IsOnGround = (PosZ + ModZ <= sol)`), **non appelées par le tick**. Chaque cas « sans effet » est accolé à un cas positif dans la même assertion (arrivée exacte contre arrivée d'une unité plus bas, flottante sans gravité contre balle, flèche à « sol + 1 » contre « sol + 0 », marqueur 0x8000 sans gravité contre avec gravité, fenêtre de l'aimant des deux côtés plus ses trois portes). Suite : **4 907 sur 4 907** en Release et en Debug (4 898 + 9), aucun test existant déplacé, `cmp` de la DLL identique, cinq traces inchangées, aucun diff de `RunHeroVerticalTick`, `SnapHeroAtApex` ni `AlundraPlayerManager` (le diff de `AlundraScriptedMotion.cs` n'a que des lignes ajoutées). Quatre mutants réels sur le code neuf, tous tués (arrivée `<=` par B4, aimant de 4 px par B6, loi appliquée deux fois par B1 et B3, `IsOnGround` `<` par B1 et B2) ; la liste complète est celle de T4.

---

#### 1.18.3 E20.e0k3 — Le bord de la carte pour les entités marquées ⏳

**Prérequis.** E20.e0k1.

**But.** D-E0K-7. Le pas d'une entité marquée est borné à la grille de 52 × 60 cases avec `NegMod*` et `ScreenClip*`, déjà stockés, et le drapeau est levé le même tick.

**Périmètre.** `AlundraScriptedMotion.cs` (`RunOneKinematicTick`, `SlideAlongWall`), `Alundra.Tests/`, annexe, ADR. **Hors périmètre.** Les entités par enregistrement et le héros (O-E19-17, D-E19-25) ; la dérive de tuile ; la fraction de pixel.

**Partage des phases** :
- **Phase A** (T1, sans changement de comportement) : la fonction pure de bornage et ses tests directs.
- **Phase B** (T3) : l'appel avant le mover et la conservation du drapeau dans `SlideAlongWall`.

##### Tâches

- ⏳ **T1 — Phase A** : tests directs de la fonction, accolés deux à deux : (a) pas qui tient (inchangé) accolé à un pas qui dépasse (borné, drapeau) ; (b) les quatre bords avec les décalages du préfab (`PosX + ModX ≥ 0`, `PosY + ModY ≥ 0`, bornes `0x4E00000` et `0x3C00000` moins la boîte).
- ⏳ **T2 — Tests de monde**, **rouges avant T3** : (b1) flèche née en X = 1 200 vers l'est sur un champ ouvert : X = 1 233 au tick 6, X = 1 237,0 au tick 7 avec `ForceAdjusted` **le même tick** (oracle `map-edge`), accolée à une flèche née en X = 1 100 qui n'est pas bornée pendant 20 ticks ; (b2) drapeau conservé à travers `SlideAlongWall` (cas cardinal et oblique), y compris au tick qui progresse encore, et au tick suivant ; (b3) une entité par enregistrement au bord est inchangée (O-E19-17), accolée à la même flèche bornée ; (b4) le héros inchangé.
- ⏳ **T3 — Phase B** : le branchement. Verts.
- ⏳ **T4 — ADR, verifiers frais.** Mutants, au moins 8 : bornes permutées, drapeau non levé, levé au tick suivant, effacé par `SlideAlongWall`, appliqué au héros, appliqué aux entités par enregistrement, bornes calculées sans les décalages, entité non marquée bornée.

##### Acceptation

- DLL : tests verts ; (b1) à (b3) rouges avant T3 ; `Alundra.Tests` en Release et en Debug, zéro échec ; aucun test existant ne bouge hors de la liste prédite recomptée au HEAD de départ de la tranche (T0 d'e0k1) ; traces inchangées ; moteur, convertisseur et données sans diff.

##### Arrêts, budget, retour arrière

- **Arrêts** : un test hors de la liste prédite recomptée au HEAD de départ de la tranche (T0 d'e0k1) bouge ; la carte du test n'a pas de bord franchissable (relever une autre carte ou un champ synthétique, `ContactWorld`) ; un besoin de changer le moteur ; un drapeau de rognage effacé par `SlideAlongWall`.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule. La phase A ne change aucun comportement.

---

#### 1.18.4 E20.e0k4 — Boucle d'affinage entière de `ComputeXY` pour les entités marquées (conditionnelle) ⏳

Détaillée seulement si l'auteur répond à Q-K2 par l'option B. Contenu : un port entier de la boucle d'affinage de `0x80037730` (pas entier en unités brutes, halvings, `moved_ok`, `NoObstacleSlide`), sans entité (les projectiles ne sont pas sujets), pour les entités marquées, en remplacement du mover du moteur. T0 : le modèle du fuzz (20 000 cas, 0 écart) comme oracle de la fonction ; acceptation : les 12 départs de l'oracle (drapeau aux ticks 43 et 44 exacts), aucun écart sur le fuzz. Phases A (fonction pure) et B (branchement). Prérequis : e0k2, e0k3.

---

**Points ouverts ajoutés.**

| # | Question | Tranchée par |
|---|---|---|
| O-E0K-1 | Z avant XY, atterrissage strict et aimant de 3 px pour les entités par enregistrement (monstres, PNJ, caisses posées) | E19.h2 (Q-K1) |
| O-E0K-2 | Attributs de tuile des entités à gravité (`+0x180`, `+0x184`, `+0x18C`, `+0x190`) : E9 lit `+0x18C` (1 et 4), `DestroyOnSlidingSlope`, `DestroyOnVramFlags` | Partagé le 2026-10-15 (révision 9) : `+0x18C` des entités marquées à gravité et `DestroyOnSlidingSlope` → E20.f6a ; `+0x180`, `+0x184`, `+0x190` et `DestroyOnVramFlags` → le T0 d'E20.f6a recense leurs lecteurs et les types qui portent `0x200000`, et nomme leur propriétaire dans le plan (aucun lecteur dans f4 à f6) |
| O-E0K-3 | Cellules de dérive (tables `0x80028B94`, `0x80028BD4` ; 5 539 cellules sur environ 48 cartes, 876 avec le bit d'air) : aucun équivalent dans la DLL, héros compris | à placer |
| O-E0K-4 | Rognage au bord pour les entités par enregistrement et pour le héros | O-E19-17, D-E19-25 |
| O-E0K-5 | Porté `+0x28` et passagers : le marqueur doit être posé par g1 au soulèvement d'une entité par enregistrement (détail : §1.24, D-G-2, D-G-3, D-G-12) | E20.g1, E20.g2 |
| O-E0K-6 | Plafonds formés par des entités pendant une montée (`0x80036D94`, sujets) : caisse lancée sous un objet | à placer (aucun cas du corpus) |
| O-E0K-7 | Fraction de pixel de X et Y à la naissance (racine tronquée) | E20.f3c (O-F3-2) |
| O-E0K-8 | La liste des collidables de la DLL est refaite par image, celle du binaire par tick : une entité née dans l'image n'est pas vue des sujets avant la fin de l'image | à mesurer par e0c ou g2 |
| O-E0K-9 | Commentaires périmés : `EntityFlags.NoRiders` (inversé), « 6 préfabs sans contrôleur » (12), `plan-e14-ia-native.md:698` | corrigés par la tranche qui touche le fichier, sinon reportés |

**Corrections induites (faites par la session principale à l'insertion, 2026-10-15).** (1) §1.19 D-F3-8 : le critère `EntityRefId == −1`
est remplacé par le marqueur d'e0k2 et le repli B de Q-F3-1 tombe. (2) §1.1 : le fait [DLL] « seul le héros a un pas vertical » est
corrigé, la ligne 4 est scindée (révision 8 de l'enveloppe). (3) `docs/plan-e14-ia-native.md` : la chute des Muruta passe par le chemin
du contrôleur. (4) E20.g1 : poser le marqueur au soulèvement (ligne 10 de l'enveloppe).
### 1.19 E20.f3 — Le tir du héros : direction, effets de coup 4 et 5, vague de l'épée (f3a, f3b, f3c) ⏳ (détaillée le 2026-10-15 ; f3a et f3b faites et CONFIRMED le 2026-10-15, ADR-0067 ; f3c après e0k2)

**Décisions de la session principale (2026-10-15, mode AUTO, l'auteur absent ; réversibles, signalées à l'auteur).** Q-F3-1 : option A —
f3c attend la règle du Z logique (D-F3-8) d'E20.e0k, que l'enveloppe ordonne déjà avant f3 ; si la section d'e0k ne la contient pas, f3c
prend le repli B (elle porte la règle elle-même, restreinte aux entités nées par l'interface sans gravité). Q-F3-2 n'est pas une question :
l'enveloppe READY (§1.1, ligne 5) a déjà fixé la coupe d'herbe de la vague en bouchon nommé jusqu'à E20.e ; la dérive du flux qui en découle
est O-F3-1. Ordre : f3a et f3b ne dépendent pas d'e0k et s'exécutent avant elle (E20.e0b2 est en pause) ; f3c après e0k (révision 7 de
l'enveloppe).

Découverte en lecture seule du 2026-10-15, au HEAD `0f5b484` : trois relevés (binaire, impact sur la DLL, inventaire des tests), puis un critique adversarial. Le critique a redésassemblé les sites qui portent les décisions (le tir `0x8002F768`, la porte des effets `0x800395D4`, le choix du programme `0x80038764`, l'écriture de `IsOnGround` `0x800380F4`, le Z de création `0x80039EA0`, C61, E10, E1, A0, le son `0x800490FC`). Il a rejoué dix-sept sorties de l'oracle sur une copie des outils (identiques aux relevés). Il a écrit deux sondes nouvelles (l'arène de la charge avec la vague ; les directions) et refait deux recensements (en-têtes de sprites ; attaquants des nibbles 6, 7, 9 et 10 sur les 483 cartes). Enfin il a relu les chemins de la DLL que la vague traverse. Scripts et sorties dans le scratchpad de la session : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20f3/` (`bin/`, `dll/`, `tests/`, `critic/`). Aucune modification du moteur, du convertisseur ni des données. La DLL n'a pas été exécutée : ce qui la concerne est lu dans le code.

**But.** Brancher le tir du héros dans le monde, un type à la fois et jamais avant son gestionnaire E. Dans l'ordre : la direction du tir lue comme le binaire (P3 d'E20.f1a) ; les effets de coup 4 et 5 de la passe de coups ; la vague de l'épée (types 27 à 29, gestionnaires C61 et E10). Rien d'autre ne naît : la flèche chargée (E20.f4), les flèches (E20.f5) et les boules (E20.f6) restent des bouchons comptés.

**Découpage (Q-F3-1 ; l'enveloppe, §1.1 ligne 5, en faisait une seule tranche ; révision 7 de l'enveloppe).**

| Sous-tranche | Contenu | Prérequis |
|---|---|---|
| E20.f3a | Direction du tir par `BoxDirection` ; couture de branchement (ensemble de types câblés, vide) ; invariant « jamais avant son gestionnaire E ». Aucun type n'est activé. | E20.f1 ✅, E20.e0a1 à E20.e0a3 ✅ |
| E20.f3b | Effets de coup 4 et 5 (porte `(b & 0xC0) != 0x80`, `0x800395E8`) | le T0 d'E20.f3a (oracle commun), E20.c1a ✅, E14.a1b ✅, E20.e0b1 ✅ |
| E20.f3c | Vague : C61 (coupe d'herbe en bouchon nommé), E10, activation des types 27 à 29 | E20.f3a, E20.e0a1 à E20.e0a3 ✅, E20.e0b1 ✅, **la règle « Z logique » d'E20.e0k** (§1.18) ou son repli (Q-F3-1) |

Ordre : f3a, f3b, f3c. f3b dépend seulement du T0 d'f3a (l'oracle commun), pas de ses phases de code. f3b est indépendante de la vague parce qu'aucun projectile de f3 ne porte un nibble 6, 7, 9 ou 10 ; elle change pourtant des chemins du jeu existant (voir les faits).

**Faits porteurs.** Étiquettes : [binaire] décodé sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS de f-projectiles (`pworld.py`) ; [DLL] lu dans le code au HEAD `0f5b484` (numéros de ligne à relire à l'exécution : E20.e0b2, en pause, touche aussi `AlundraWorldProxy.cs`) ; [données] export et cartes extraites ; [critique] établi ou corrigé par la critique.
- [binaire] **Le tir.** Un seul site d'apparition d'arme, `0x8002F818`, dans `UpdateWeaponStepProgression` (`0x8002F768`, appelée par `MovePlayer` en `0x80031D70`, avant l'aiguillage d'état). L'image compte 102 `jal 0x80039F58` ; les autres sites du voisinage du héros sont le butin (`0x80032BEC`, `0x80032CD8`) et l'usage des objets (E20.h). Arguments : propriétaire = le héros, table 0, type = `ptr[1]`, X et Y = ceux du héros (aucun décalage), Z = `+0x11C` du héros + `ptr[3] << 16`, direction = `[0x80023644 + 4 × (+0x98)]` (`{0, 0x10, 8, 0x18}` : bas, haut, gauche, droite). Le retour est ignoré : le pointeur d'arme est effacé même si la table de cases est pleine (tir perdu en silence). Le pointeur est armé par `PlayerTryAttack` ; `S = tick d'armement + délai + 1`. Rien ne compte les projectiles vivants : deux flèches vivent ensemble (oracle `multishot`).
- [binaire, critique] **Fiches** (`0x80028C79 + 13 × w`, redumpées). Épées d'acier, de démon, sainte (w = 1, 2, 3) : relâchement animation `0x18`, types 27, 28, 29, délai 20, z 0 ; pas de tir à l'appui. Arc du chasseur (w = 8) : type 3, délai 8 au sol et 0 en l'air, z 16. Arc de William (w = 9) : type 4 ; chargé, type 7. Baguettes : w = 4 (11, 12), w = 5 (14, 15). Seules les animations `0x11`, `0x13`, `0x15`, `0x17`, `0x18` gardent le pointeur (drapeau b1 de `0x80028D14`).
- [binaire, oracle, critique] **Direction.** `+0x98` est l'index de direction écrit par `UpdateAnimation` (`0x80038B40`) ; le tir au tick S lit la valeur de la fin du tick S−1. La direction du tick de l'appui compte ; un virage demandé au tick du tir ou pendant le coup est ignoré. Clés : `dirtest.txt`, `dir_extra.txt` (arc du chasseur en l'air : Carré + Gauche au tick 8, flèche au tick 9, `dir_t = 8` ; Gauche seul à partir du tick 9, `dir_t = 24`).
- [binaire, oracle] **La vague : données.** Drapeaux `0x205C`, programmes A0, C61, D0, E10, F0, corps (−10, −7, 0) de 21×14×8, `BreakEffect` 0, `Contents` 0 ; fiche d'équilibre 27 à 29 : attribut `0x81`, puissance 10, 20, 38, toutes réponses 128. Animation 0 : vitesse 0, octet D `0xC1` (intouchable, hors collisions), 14 images par direction (13 de 2 ticks, puis une image de transition sans boîte), boîtes de 36×24×31.
- [binaire, oracle] **La vague : vie.** Née à `S = R + 21` dans `MovePlayer`, à X et Y du héros, au Z brut du héros + 1 (`0x80039EA0-0x80039EA4`). Elle ne bouge jamais (forces 0, `ForceAdjusted` 0). A0 tourne à S, C61 de S+1 à S+25, le drapeau `+0xAC` vaut 1 à S+25, E10 détruit à S+26, la case est libre à S+27. La boîte change à S, S+1, S+3, S+5, S+7, S+9 puis tient. Le son 30 joue à S ; `snd(0)` à S+25 est **muet** (`0x80049134`, `blez`).
- [binaire, oracle] **Pourquoi elle ne se pose jamais.** Le choix du programme (`0x80038764-0x80038834`) prend E si le drapeau `0x10` est levé et si `+0x13C` ou `+0x144` est non nul. `+0x144 = (+0x134 >= Z)`, et `+0x134` est le sol + 1. La vague naît à sol + 1 + 1 : jamais au sol. Mutant W1 (sans le « +1 ») : elle meurt à S+1. Mutant W3 (sans E10) : elle reste en état 3 sans fin.
- [binaire, données] **C61 et E10.** C61 (`0x8007A958`) appelle `CheckAndTriggerTileEffect` (`0x8003A7B0`) et rien d'autre. E10 (`0x8007F23C`) a le corps d'E1 (`0x8007ED10`) : `DestroyEntity(e, −1)`. A0 (`0x8006174C`) n'écrit que `AIValues[0]`, jamais lu par C61 ni E10. Recensement des 395 en-têtes : E10 et C61 n'existent que sur les sprites 27 à 29.
- [oracle] **La coupe d'herbe.** Avec toutes les cases en herbe, l'acier coupe 3 cellules (S+10 à l'est) ; la fonction tire au moins 1 mot par cellule coupée et peut lâcher un objet. Le héros coupe les autres cellules.
- [oracle] **Les touches.** Masque d'attaque 1 : la vague ne touche que les cibles de classe A ; le héros n'est jamais touché. Premier tick de touche de la vague seule (est) : cibles à +32, +44, +56, +68, +80 → S+1, S+3, S+5, S+7, S+9 ; rien au-delà de +92 (nord −60, ouest −88, sud +60). Le héros (case 0, premier de la liste) gagne la fenêtre de 25 ticks quand son coup `0x18` touche la même cible.
- [oracle, critique] **Géométrie de l'arène de la charge** (`arena_probe.txt`, ticks de l'oracle moins 5 = ticks de l'arène). Avec E10 : `[(5,héros),(118,héros)]`, soit 2 contacts. Sans E10 : `[(5,héros),(118,héros),(143,vague),(168,vague)]`, soit 3 contacts dans les 140 ticks de l'arène (le troisième au tick 138).
- [binaire, oracle] **Effets 4 et 5.** `b = [fiche de la cible + (nibble) + 2]`. Si `(b & 0xC0) == 0x80`, ni effet ni magasin de `TouchingEntity` (`0x8003966C`), mais `+0x1D0 = 25`, `HitCounter` + 1 et trois étincelles. Sinon : nibbles 6 et 10 → effet 4, nibbles 7 et 9 → effet 5, par `0x8003BE74(0, id, 0, cible, 1, 0, 0, 0)`, avant le magasin de `TouchingEntity` et avant les étincelles. Table `hitfx_table` : 160 paires sur 160 conformes. Aucun tirage (599 accès au flux dans l'image, aucun dans le code des effets). L'effet 4 dure 40 ticks, l'effet 5 dure 95.
- [binaire, critique] **La cible héros.** La porte lit la fiche `+0x1C4` du héros, dont le seul magasin est `0x80039E38` : l'entrée 0, réponses nulles. L'effet s'attache donc toujours au héros touché par le feu ou la glace, charme de Nava compris (le charme agit dans `ComputeNewHp`).
- [DLL] **Le bouchon.** `SpawnWeaponProjectile` (`AlundraPlayerManager.cs:1186`) compte, journalise (`WeaponSpawnCallsForTests`), appelle le crochet de test, et indexe la direction par `AnimationDirection` (écrit en fin d'image par `SyncAnimation`). `BoxDirection` (valeur du tick, D-E14-6) et `TickFacing` valent la même chose (`TryResolveAnimationTarget(rangée, TargetDirection)`) ; `BoxDirection` vaut 3 après le `Start()` des montages.
- [DLL] **L'interface.** `SpawnEntityBySpriteIndex` ajoute l'unité brute (D-E0A-1), prend la première case libre sur 63, joue le son de l'animation 0 et pose `JoinsBirthTick` ; `RunEntityBlock` re-collecte juste après `MovePlayer` (`AlundraWorldProxy.cs:~2860-2875`). Les hôtes de montage ne sont pas des `IEntityWorldContext` : aucun appel n'y aboutit. Aucune entité n'est née par l'interface en production aujourd'hui.
- [DLL] **Le registre.** 6 clés (Beannoïde A1, C1, D1 ; Muruta A0, C10, D9), épinglées par `AlundraNativeSpriteRegistryTests.cs:51`. `RunSpriteEvent` traite E 0 et 1 en ligne, hors du registre et de l'interrupteur `NativeHandlersEnabled` ; E10 n'y est pas. `HitArena` éteint l'interrupteur (`AlundraHitArena.cs:65-69`) et enveloppe le lanceur réel dans `RecordingRunner`.
- [DLL, critique] **Le va-et-vient de la racine.** `PushLogicalPositionToRoot` écrit la racine en pixels entiers (`ResolveLogicalPosition`, `>> 16`). `PullRootPose` remet `PosZ` depuis la racine à chaque image pour toute entité à contrôleur (`AlundraEntityScriptProxy.cs:~1416`) et `IsOnGround = Controller.IsGrounded` (`:~1423`). La branche d'atterrissage du terrain de `TickPhysics` (`:~940-1000`) met `IsOnGround = 1` dès que `PosZ ≤ sol`. La DLL dit elle-même que l'unité du binaire est « avalée » (`:~880-1010`, `:1875-1890`, `:2049-2060`). Une entité née par l'interface n'a ni gravité de contrôleur, ni `IsVerticalOwnedExternally` (réservés à `ApplySpawnInitialization`, « que E20.e0k règle »). Conséquence prédite, **non exécutée** : la vague meurt à S+1 – S+3.
- [DLL] **La passe de coups.** `AlundraHitPass.Run(liste, héros, étincelles)` (`:78`) ; les effets 4 et 5 y sont notés « non portés » avec un commentaire périmé (l'ancre existe : `IAlundraEffectAnchor` est implémentée par le proxy, `AlundraEffectReservoir.CreateAttached` et `CreateBreakEffect` sont le modèle). Trois tests appellent `Run` avec trois arguments ; la production l'appelle en `AlundraWorldProxy.cs:~3026` par un délégué statique.
- [données, critique] **Producteurs des nibbles 6, 7, 9, 10.** Balance globale 11, 12, 14, 15, 18, 19, 20, 21, 23, 38, 238, 239 ; cartes 304, 309, 316, 319, 320, 337, 343, 390, 393, 412 à 415, 420, 437, 441, 473 à 475, 480, 481. **85 cartes** placent au moins un tel attaquant (238 sur 21 cartes, 319 sur 27, 437 sur 16, 390 sur 10). La carte 14 en a un : enregistrement 17, sprite 238 (drapeaux `0xC3`, D34), case (14, 28).

**Écarts tranchés par la critique** (les relevés sont corrigés ainsi ; les audits restent en lecture seule).
1. *« La vague ne demande rien à E20.e0k »* (relevés binaire et tests) est **CORRIGÉ**. Vrai pour le mouvement. Faux pour la DLL : l'unité de Z et `IsOnGround` ne survivent pas à la racine (fait « va-et-vient »). L'enveloppe met d'ailleurs e0k (ligne 4) avant f3 (ligne 5).
2. *« La vague peut mourir après environ un tick si `IsGrounded` est vrai »* (impact DLL) est **CORRIGÉ vers le haut**. L'unité est perdue quel que soit `IsGrounded`. La sonde de la phase A de f3c tranche (D-F3-8).
3. *« L'épingle de l'arène change très probablement / peut changer »* (impact DLL / tests) est **PRÉCISÉ**. Elle passe de 2 à 3 contacts (tick 138) si et seulement si E10 ne tourne pas. Avec E10 en ligne (D-F3-5) elle reste à 2.
4. *« `snd(0)` : audibilité inconnue »* est **CLOS** : muet.
5. *« BoxDirection ou TickFacing ? »* : égaux. `BoxDirection` est retenu (enveloppe, P3) ; un test affirme l'égalité.
6. *« f3a peut se fondre dans f3b »* (impact DLL) est **REFUSÉ** : le P3 est à corriger « obligatoirement par E20.f3 » et ne dépend ni de la vague ni d'e0k.
7. *« Aucun producteur des effets 4 et 5 dans f3 »* (binaire) est **PRÉCISÉ** : vrai pour les projectiles de f3, faux pour le jeu existant (85 cartes). f3b est visible.
8. *Crochet de test* (question 2 des tests) : option (a) retenue (D-F3-3).
9. *« Un type à la fois »* (enveloppe) : les trois épées partagent C61 et E10 et ne diffèrent que par leur fiche. Elles sont câblées ensemble, chacune avec son test.

**Décisions de conception (ADR : le prochain numéro libre au moment de l'écriture, revérifié dans `docs/decisions/README.md` ; E20.e0b2, en
pause, prendra le suivant à sa reprise).**
- **D-F3-1** — Découpage f3a, f3b, f3c et ordre ci-dessus (Q-F3-1).
- **D-F3-2** — La direction du tir est `BoxDirection`, lue dans `MovePlayer` avant l'horloge du tick (jamais `AnimationDirection`), puis `SpawnDirections[BoxDirection]`. Un test affirme `BoxDirection == TickFacing` au tick du tir à 1, 2 et 3 ticks par image.
- **D-F3-3** — `SpawnWeaponProjectile` garde dans l'ordre : le compteur, le journal, puis, si le crochet de test est posé, le crochet **à la place** de l'appel de production (pas de double apparition) ; sinon, pour un type de l'ensemble `WiredWeaponTypes` seulement et si l'hôte est un `IEntityWorldContext`, `SpawnEntityBySpriteIndex(héros, false, type, X, Y, Z, direction)`. Un retour nul est toléré : tir perdu en silence, pointeur effacé, comme le binaire. L'ensemble est vide en f3a et vaut {27, 28, 29} en f3c ; un ensemble de test le remplace pour les tests.
- **D-F3-4** — Un test mécanique : pour chaque type de `WiredWeaponTypes`, chaque code C et E non nul de son en-tête est géré (clé du registre de production pour ce type, ou branche en ligne d'E). Un faux ensemble {3} fait échouer le vérificateur sur `(E, 17)` (cas négatif dans le même test).
- **D-F3-5** — E10 est traité comme E0 et E1 : dans la branche en ligne de `RunSpriteEvent`, hors de l'interrupteur des gestionnaires natifs (même corps `DestroyEntity(e, −1)`, recensement : seulement 27 à 29). `HitArena` détruit donc la vague au bon tick et l'épingle d'arène reste à 2. Écart de forme : deux adresses du binaire (`0x8007ED10`, `0x8007F23C`), une branche.
- **D-F3-6** — C61 est un gestionnaire natif du registre (clé (C, 61), types 27 à 29) qui appelle le bouchon nommé `CheckAndTriggerTileEffect(entité)` (`0x8003A7B0`) avec un journal par thread, comme `AlundraPlayerAnimationEffects.StubCallsForTests`. L'herbe n'est pas coupée avant E20.e (Q-F3-2). Écart documenté dans le code : le binaire tire au moins 1 mot par cellule coupée ; le flux dérive après toute vague sur l'herbe.
- **D-F3-7** — A0 des types 27 à 29 n'est pas enregistré (il n'écrit que `AIValues[0]`, jamais lu). ~~Les tranches f4 à f6 devront élargir l'enregistrement A0 du Muruta~~ (**réfuté par la découverte d'f4 à f6, §1.20** : A0 est une
  donnée morte pour ces types ; aucun enregistrement).
- **D-F3-8** — (**corrigé le 2026-10-15 par la découverte d'E20.e0k** : le critère est le marqueur `HasLogicalZ` d'e0k2, D-E0K-1, et la
  règle vaut aussi pour les entités à gravité, D-E0K-4 ; le repli B de Q-F3-1 tombe, f3c exige e0k1 et e0k2) Le Z d'une entité née par
  l'interface (`EntityRefId == −1`) sans le bit de gravité `0x100` reste logique. `PullRootPose` ne rapatrie ni `PosZ` ni `IsOnGround` pour elle, `TickPhysics` ne ré-extrait pas `PosZ` de la racine, et `IsOnGround = (PosZ ≤ sol)` dans le repère de la DLL (la règle de `AlundraEntityScriptProxy.cs:~1109`, binaire − 1 unité). X et Y continuent d'être rapatriés (écart sous le pixel, O-F3-2). La règle est livrée par E20.e0k (§1.18) ou par le repli de f3c (Q-F3-1). Si la sonde de vie est verte sans elle, la décision tombe (le fait était faux).
- **D-F3-9** — `AlundraHitPass.Run` gagne un paramètre facultatif `attachEffect` (cible, identifiant). La porte `(r & 0xC0) != 0x80` est celle du magasin de `TouchingEntity` ; nibbles {6, 10} → 4, {7, 9} → 5 ; l'appel précède le magasin de `TouchingEntity` et les étincelles. Le monde passe un délégué statique (aucune allocation par tick) qui met sur l'étage, au rang 1 du tick comme les étincelles, `CreateAttached(false, id, 0, cible, 1, 0, 0, 0)` (profondeur + 1 comme l'effet de bris). Aucun tirage. Écart documenté : dans une image de plusieurs ticks la création a lieu en fin d'image ; l'effet suit ensuite la cible, aucune conséquence visible.
- **D-F3-10** — La cible héros n'est pas filtrée : la fiche 0 donne des réponses nulles, donc l'effet s'attache toujours (fait « cible héros »).
- **D-F3-11** — `HitArena` garde ses gestionnaires natifs éteints. L'observation du nouveau-né passe par `Arc.Entities` ; le support de test gagne un échantillon d'entité née et un pas de groupement de 1, 2 ou 3 ticks par image.
- **D-F3-12** — Les comparaisons lisent l'oracle par clé (voir f3c). Le Z du binaire est le Z de la DLL + 1 unité brute (ADR-0026). Pour le flux, seuls les écarts comptent (l'oracle de f-projectiles n'avance pas le flux par tick) : le mot attendu est calculé par un pas local (D-E0B-8).

**Questions à l'auteur.** Aucune bloquante : Q-F3-1 et Q-F3-2 sont tranchées plus haut (décisions de la session principale).
Pour mémoire : Q-F3-1 opposait f3c qui attend e0k (A) à f3c qui porte elle-même la règle du Z (B) ; Q-F3-2, la coupe d'herbe réelle
sous la vague, demanderait d'avancer E20.e0c et E20.e en entier.

**Prérequis et dépendants.** E20.e0k est-il nécessaire ? **Pas pour le mouvement** : la vague vole à Z constant, sans gravité ni impulsion (oracle : position et forces constantes). **Oui pour une règle** (D-F3-8) : f3a et f3b n'en ont pas besoin ; f3c l'exige de §1.18 ou la porte elle-même (Q-F3-1). f3 n'exige pas E20.e0b2, en pause : le `Contents` de la vague vaut 0, donc aucun tirage à la création. Elle touche pourtant les mêmes fichiers (`AlundraWorldProxy.cs`) : les tranches s'exécutent l'une après l'autre. Dépendants : E20.f4, E20.f5 et E20.f6 (la couture, l'invariant, le registre, le Z logique) ; E20.f6 (les effets 4 et 5 de f3b, producteurs réels) ; E20.e (ferme le bouchon d'herbe) ; E14 (les ennemis de feu et de glace touchent le héros).

---

#### 1.19.1 E20.f3a — Direction du tir et couture de branchement (aucun type activé) ✅ (faite et CONFIRMED le 2026-10-15, `33c4590` à `242e7b8`, ADR-0067)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, un point corrigé (FIX) : (s1) attendait la case 1, que les entités chargées de la
carte 14 occupent ; il attend la plus basse case libre mesurée avant le tir. Repris en même temps : la sonde de la flamme (recette de
f3b) est une sortie nommée du T0, et f3b exige ce T0 (relecture de l'enveloppe et de f3b).
**Révision 2 (2026-10-15).** Seconde relecture : **REVISE**, un blocage ; disposition de la session principale (cap de deux relectures
automatiques atteint) : **FIX** — (s3) attendait « compteur et journal inchangés » sur un hôte de montage, ce que l'ordre de D-F3-3
(compteur, journal, puis appel) et les tests existants du journal contredisent ; il attend maintenant compteur + 1, journal + 1, aucun
appel de l'interface, sous l'ensemble de test {3}. Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-15).

**Prérequis.** E20.f1 ✅ (§1.9), E20.e0a1 à E20.e0a3 ✅ (§1.16).

**But.** Corriger le P3 d'E20.f1a (la direction du tir) et poser la couture qui appelle l'interface pour les types câblés, sans en câbler aucun.

**Périmètre.** `Alundra/Scripts/AlundraPlayerManager.cs` (`SpawnWeaponProjectile`, ses commentaires), `Alundra.Tests/` (nouveaux tests ; commentaires périmés mis à jour), `docs/plan-e20-annexe/e20-f3/` (README, `oracle/`), un ADR commun à f3a, f3b et f3c.

**Hors périmètre.** Tout type câblé ; le retrait du crochet de test ; `PlayerTryAttack`, la charge, les autres lignes de `MovePlayer` ; E20.e0k.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : `WiredWeaponTypes` (vide), l'ensemble de test, la garde, l'appel de l'interface après le crochet (le crochet remplace l'appel), la tolérance du nul, le vérificateur de l'invariant. En production rien ne change : l'ensemble est vide. La direction reste `AnimationDirection`.
- **Phase B** (T3) : la ligne de la direction (`BoxDirection`) et les commentaires de `SpawnDirections` et de `WeaponSpawnCall`. C'est le seul changement de comportement observable (champ `Direction` du journal).

##### Tâches

- ✅ **T0 — Prédiction et oracle** (commit de docs, avant tout code), pour les trois sous-tranches. `docs/plan-e20-annexe/e20-f3/oracle/` : les outils de la machine copiés octet pour octet avec leurs sommes de contrôle ; les scripts `wave_run`, `summary`, `wave_hit`, `wave_only`, `wave_z`, `wave_mut`, `wave_grass`/`2`/`3`, `hitfx`, `hitfx_mut`, `multishot`, `dirtest`, `dir_extra`, `arena_probe`, `twoint` ; sorties en texte **et** en JSON, avec les clés des lignes de l'oracle (`slots[1].status`, `.cnt1c`, `.reset_AC`, `.px`, `.py`, `.pz`, `.fx`, `.fy`, `.fz`, `.adj_13C`, `.ground_144`, `.hbx`, `.hby`, `.hbz`, `.bw`, `.bd`, `.bh`, `.atk_attr`, `.atk_pow`, `.type68`, `.flags6C`, `.loot3c`, `.c1d0`, `.touch_slot`, `.box1d4` ; pour `hitfx_table` : `attr`, `nib`, `resp`, `fx`, `sparks`, `c1d0`, `touch`, `hitc`, `rng_before`, `rng_after`). Deux exécutions donnent un manifeste identique. Au moins 12 mutants d'un mot, tous tués ou équivalents prouvés par écrit : W1 à W3, les 4 de `hitfx_mut`, et 5 de plus (table des directions `0x80023644` permutée, comparaison du délai de `0x8002F7C8` inversée, octet z de `ptr[3]` changé, pointeur non effacé après l'apparition, drapeau b1 ignoré). L'oracle accepte une position fractionnaire du héros (O-F3-2). **Sonde de la flamme** (pour la recette de f3b, O-F3-5) : la carte 14,
  enregistrement 17 (sprite 238, case (14, 28)) attaque-t-il dans son animation 0, avec quel nibble, et le héros peut-il être à portée ;
  sortie `flame_probe.json` ; un « non » ne bloque rien (la recette passe alors à une autre carte des 85). README : les valeurs attendues, la prédiction **qualitative** (aucun test existant ne bouge en f3a ni en f3b ; en f3c : l'épingle du registre passe de 6 à 7, l'épingle d'arène reste à 2) et le compte de départ de la suite (4 852 après e0b1, à relever).
  **Résultat (2026-10-15).** `docs/plan-e20-annexe/e20-f3/` (README, `oracle/`) : 56 outils copiés octet pour octet (SHA-1 dans `checksums.txt` ; 25 identiques à des copies d'annexes antérieures, `pworld.py` avec `e20-e0a`, `diff2.py` de même nom mais d'autres octets, 30 nouveaux) ; `run_f3.py` en cinq étapes (texte, mutants, flamme, JSON, contrôles) ; 45 fichiers de sortie en texte et en JSON (clés de l'énoncé ; `hitfx_table.json` : 160 paires sur 160 conformes ; `directions.json` : 11 cas) ; les sorties texte sont identiques octet pour octet à celles de la découverte ; **deux exécutions complètes donnent le même manifeste** (`diff -rq` vide) ; 21 contrôles PASS. 12 mutants d'un mot, **12 tués**, aucun équivalent (W1 à W3, H1 à H4, D1 table des directions permutée, D2 comparaison du délai inversée, D3 décalage de l'octet z, D4 pointeur non effacé, D5 drapeau b1 ignoré ; D5 exige un stimulus du harnais, documenté : l'animation cible du héros forcée à 0 au tick 3 ; cinq historiques de manette ne la tuent pas). Position fractionnaire du héros acceptée (`hero_fractional.json` : la vague naît aux mêmes X et Y bruts). Clés de direction : (d1) air, Carré + Gauche au tick 8, tir au tick 9, 8 ; (d2) Carré seul au tick 8, Gauche seule dès le tick 9 : `0x18` ; (d3) sol, Haut / Bas / Gauche / Droite : `0x10`, 0, 8, `0x18` (tir au tick 10). **Sonde de la flamme** (carte 14, enregistrement 17, sprite 238) : l'animation 0 attaque avec le nibble 1 (boîte 42×26×31) et touche le héros à son Z sans aucun effet attaché ; le nibble 6 (effet 4) appartient au jeu d'animation 1, que l'oracle force (le programme D34 n'est pas exécuté ni décodé) : pour l'effet 4 la réponse est « non » en animation 0, la recette de f3b pourra passer à une autre carte des 85 (`flame_probe.json`). Compte de départ re-mesuré au HEAD `d0e91db` : **4 852 en Release et en Debug, zéro échec**. Prédiction qualitative dans le README de l'annexe.
- ✅ **T1 — Phase A** (tests d'abord, rouges sur un squelette qui compile, puis le code). `AlundraWeaponSpawnSeamTests`, chaque cas « sans effet » accolé dans la même assertion à un cas positif :
  - (s1) monde (`ArcRun`), arc du chasseur au sol : ensemble vide → compteur 1, journal 1 appel, **zéro** entité nouvelle ; ensemble de test {3} → exactement une entité de type 3, parent le héros, à la **plus basse case libre mesurée avant le
    tir** (sur la carte 14 les entités chargées tiennent les cases 1 à n ; même règle qu'`AlundraSpawnBySpriteIndexInterfaceTests`),
    `JoinsBirthTick` ;
  - (s2) crochet posé et type dans l'ensemble → exactement **une** entité (celle du crochet) ; crochet nul → une entité (celle de la production) ;
  - (s3) avec l'ensemble de test {3}, sur un hôte de montage (qui n'est pas un `IEntityWorldContext`) : compteur + 1 et journal + 1 entrée,
    comme aujourd'hui (l'ordre de D-F3-3 : compteur, journal, puis crochet ou appel de production), **aucun** appel de l'interface, zéro
    entité, aucune exception, `WeaponRecordPointer == 0` ; dans la même assertion, le même tir dans `ArcRun` avec le même ensemble → une
    entité ;
  - (s4) 63 cases occupées → rien n'est créé, `WeaponRecordPointer == 0`, pas d'exception ; une case libre → une entité ;
  - (s5) invariant (D-F3-4) : l'ensemble de production (vide) passe, le faux ensemble {3} échoue sur `(E, 17)`.
  Puis le code de la phase A. Verts à la fin de T1 ; la suite complète ne bouge pas.
  **Résultat (2026-10-15).** `Alundra.Tests/AlundraWeaponSpawnSeamTests.cs` (5 tests, (s1) à (s5)). Squelette qui compile (membres `ProductionWiredWeaponTypes`, `WiredWeaponTypesForTests`, `WiredWeaponTypes`, vérificateur vide, paramètre `host` passé à `SpawnWeaponProjectile`, aucun changement de comportement) : **les 5 tests sont rouges** (S1, S2, S3, S4 : « The collection was empty » sur l'entité attendue ; S5 : `(4, 17)` absent d'une liste vide). Puis le code de la phase A dans `AlundraPlayerManager.cs` : `SpawnWeaponProjectile(player, pointer, host)` garde dans l'ordre le compteur, le journal, puis le crochet **à la place** de l'appel de production, sinon, pour un type de `WiredWeaponTypes` et un hôte `IEntityWorldContext`, `SpawnEntityBySpriteIndex(héros, false, type, X, Y, Z, direction)` (retour ignoré) ; `ProductionWiredWeaponTypes` vide, `WiredWeaponTypesForTests` par thread ; le vérificateur `UnhandledWeaponSpriteCodes(types, catalogue, registre)` (C et E non nuls de l'en-tête : clé du registre, ou code E en ligne, définition unique `AlundraEventProgramRunner.IsInlineDeactivateCode` partagée avec `RunSpriteEvent`, comportement inchangé) ; la direction reste `AnimationDirection`. **5 tests verts** ; suite complète **4 857 = 4 852 + 5 en Release et en Debug, zéro échec** (aucun test existant ne bouge) ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques ; les cinq traces inchangées (garde `git diff --ignore-cr-at-eol` verte) ; aucune entité née en production (ensemble vide). Choix de test : (s3) utilise le montage `JumpHeroRig` (hôte `ContactHost`, qui n'est pas un `IEntityWorldContext`) avec le pointeur armé à la main.
- ✅ **T2 — Tests du P3**, écrits après T1, **rouges avant T3**. `AlundraShotDirectionTests` :
  - (d1) arc du chasseur en l'air (fiche 8, air : type 3, délai 0, z 16), le héros regarde à l'est, Carré + Gauche pressés au tick P, tir au tick P+1 : `Direction == 8` (clé `dirtest`, section « air »), aux groupements de 1 tick **et** de 2 ticks par image (P et P+1 dans la même image) ; la phase A rend `0x18` au groupement de 2 ticks : rouge ;
  - (d2) accolé dans le même test : Carré au tick P, Gauche seule à partir du tick du tir : `0x18` (clé `dir_extra`, « Left only from t9 »), aux groupements de 1 tick (le virage du tick du tir est ignoré) ;
  - (d3) les quatre directions au sol, Carré + Haut, Bas, Gauche, Droite : `0x10`, `0`, `8`, `0x18` (clés `dir_extra`) ;
  - (d4) `BoxDirection == TickFacing(héros)` au tick du tir, quatre directions × 1, 2, 3 ticks par image (vert dès T1 : il épingle l'hypothèse de D-F3-2).
  **Résultat (2026-10-15), phase A.** `Alundra.Tests/AlundraShotDirectionTests.cs` sur `HitArena` (carte 14 réelle, arc du chasseur : objet 5, emplacement 2 ; le journal `WeaponSpawnCallsForTests` donne `Direction` ; le crochet du tir relève `BoxDirection` et le `TickFacing` de la tête du tick, lu à la passe `pad`). Exécutés sur le code de la phase A, Release : **(d1) ROUGE** — 1 tick par image : tick 9, `Direction` 8 (vert) ; 2 ticks par image (le tick P = 8 et le tir P+1 = 9 dans la même image) : tick 9, `BoxDirection` 2 (l'indice de gauche, déjà bon), mais `Direction` **24 (`0x18`) au lieu de 8** (`AnimationDirection`, écrit en fin d'image, est périmé) ; **(d2) vert** (tick 9, `0x18`) ; **(d3) vert** (4 cas : Haut `0x10`, Bas 0, Gauche 8, Droite `0x18`, tir au tick 10) ; **(d4) vert** (12 cas, 4 directions × 1, 2, 3 ticks par image : `BoxDirection == TickFacing` de la tête du tick). Écart de montage, dit ici : une image de plusieurs ticks tient le pad de son premier tick pour tous ses ticks (support de l'arène), donc en (d1) à 2 ticks par image le Carré reste tenu au tick du tir (l'oracle le relâche) ; le tir part au tick 9 quand même. Les tests verts (D2, D3, D4 : 17 cas) sont commités avec ce texte : suite **4 874 = 4 857 + 17 en Release et en Debug, zéro échec**, `cmp` des DLL identique, traces inchangées. **(d1) restait non commité** (version complète) : la phase B le commite avec la ligne de la direction (T3), sans quoi aucun commit ne serait vert. **Phase B (T3) : (d1) VERT** (2 ticks par image : `Direction` 8).
- ✅ **T3 — Phase B** (commit de code B) : la ligne de la direction ; commentaires périmés corrigés (`AlundraPlayerManager.cs:1150, 1183-1185, 1198-1199`, sept commentaires de tests nommant le bouchon). Les tests de T2 passent au vert ; suite complète.
  **Résultat (2026-10-15).** Une ligne de code : `SpawnDirections[Math.Clamp(player.BoxDirection, ...)]` à la place de `AnimationDirection` dans `SpawnWeaponProjectile`. Commentaires mis à jour : le résumé de `SpawnWeaponProjectile` (couture, plus « bouchon »), celui de `SpawnDirections`, celui de `WeaponSpawnCall` ; quatre commentaires de tests qui nommaient le bouchon du tir (`AlundraSpawnBySpriteIndexInterfaceTests`, `AlundraSpawnBySpriteIndexTests` ×2, `AlundraHeroChargeTests`). Le texte du plan disait « sept » : seuls ces quatre commentaires de tests nomment encore le bouchon du TIR (les autres occurrences de « stub » du dépôt désignent d'autres bouchons : atterrissage, aura, butin, étincelles). **Les tests de T2 (d1 à d4) sont verts, (d1) compris** (4 875 = 4 874 + 1) ; suite complète **4 875 en Release et en Debug, zéro échec** ; aucun test existant ne bouge (les huit cas du journal restent à `0x18`) ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques ; les cinq traces inchangées (garde verte ; l'EOL seul restauré par `git checkout`).
  **Preuve de retrait.** L'ancienne ligne (`AnimationDirection`) remise dans l'arbre de travail, construction avec la copie neutralisée, suite complète en Release : **4 875 tests, exactement 1 rouge, `AlundraShotDirectionTests.D1_InTheAir_SquarePlusLeftAtTickP_ShotsAtPPlus1_ToTheWest_At1TickAnd2TicksPerFrame`** (le test que le plan nomme rouge avant T3) ; les 4 874 autres, (d2), (d3) et (d4) compris, sont verts. Restauré par `git checkout --`, `git status` revenu à l'état d'avant.
- 🧪 **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre). Mutants du code de la DLL, au moins 8, tous tués : direction lue sur `AnimationDirection`, indice non borné, garde inversée, crochet qui ne remplace pas, appel avant le compteur, appel même hors ensemble, nul non toléré, pointeur non effacé.
  **Résultat (2026-10-15).** ADR-0067 (`docs/decisions/0067-...`, index mis à jour) : D-F3-1 à D-F3-4 implémentées, D-F3-5 à D-F3-12 notées PRÉVUES pour f3b et f3c. **Neuf mutants réels** (appliqués par script au fichier de travail, construction avec la copie neutralisée, puis restauration), **tous tués** : M1 direction lue sur `AnimationDirection` → (d1) et (s6) ; M2 indice non borné → (s6) seul (**survivait** aux tests de T1 et T2 : aucun tick du jeu ne produit un `BoxDirection` hors de 0 à 3 ; correction : `SpawnWeaponProjectile` passe de `private` à `internal` et le test (s6) l'appelle avec `BoxDirection` 9 et −4, plus 3 et 0 inchangés) ; M3 garde inversée → (s1), (s2), (s3), (s4) ; M4 crochet qui ne remplace pas → (s2) ; M5 appel de l'interface avant le compteur → (s2) ; M6 appel même hors ensemble → (s1) ; M7 nul non toléré → (s4) ; M8 pointeur non effacé → AT9 (8 cas), 9 tests de charge, (d1) à (d4) ; M9 table des directions permutée → (d1), (d2), (d3), (s6) et 8 tests de charge. Exécution des mutants sur les classes `AlundraShotDirectionTests`, `AlundraWeaponSpawnSeamTests`, `AlundraHeroAttackTests`, `AlundraHeroChargeTests` et `AlundraSpawnBySpriteIndex*` (M1 : la suite complète l'avait aussi montré). Suite finale : **4 876 = 4 875 + 1 ((s6)) en Release et en Debug, zéro échec** ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques ; cinq traces inchangées. **Reste : les deux verifiers frais, lancés par la session principale** (d'où 🧪).

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais, l'un après l'autre (rejeu : oracle relancé sur une copie, manifeste
identique ; suites complètes en Release et en Debug, zéro échec ; état rouge avant la phase B reproduit ; `cmp` de la DLL ; adversarial :
mutants de la direction et de la couture, tous tués sauf un). **Reportés** (sans changer le candidat confirmé) :
- **P3** — l'ordre « compteur, journal, puis appel » de D-F3-3 n'est épinglé par aucun test : le mutant qui déplace compteur et journal
  après l'appel, dans les deux branches, laisse 141 tests ciblés verts (le M5 de l'exécuteur ne déplaçait que le compteur). Sans effet en
  production (compteur et journal ne servent qu'aux tests). Épingle à ajouter par f3c (un crochet qui lit le compteur et le journal au
  moment de son appel).
- **P4** — deux commentaires périmés dans `AlundraPlayerManager.cs` (« spawn stub », « named stub ») ; `AlundraEventProgramRunner.cs`
  modifié hors de la liste du périmètre (extraction d'`IsInlineDeactivateCode`, comportement identique, déclarée dans les résultats) ;
  l'ADR-0067 garde l'ancienne formulation de D-F3-7 (corrigée dans le plan par `f8f9f09`) : à reprendre par f3c ou f4 ; à 3 ticks par
  image aucun test n'affirme `Call.Direction` (le code lit `BoxDirection` partout).

##### Acceptation

- Oracle : manifeste identique sur deux exécutions ; au moins 12 mutants tués ou prouvés équivalents.
- DLL : tous les tests de T1 et T2 verts ; (d1) rouge avant T3 (nommé) ; `Alundra.Tests` en Release **et** en Debug, compte de départ plus les nouveaux, zéro échec ; **aucun test existant ne bouge** (les huit cas du journal restent à `0x18`) ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug ; les cinq traces inchangées ; moteur, convertisseur et données sans diff.

##### Arrêts, budget, retour arrière

- **Arrêts** : une entité née en production ; la phase A fait bouger un test ; (d1) vert avant T3 ; un test existant à réécrire ; un double appel du crochet et de la production ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (la ligne de la direction) ; la phase A reste inerte (ensemble vide).

---

#### 1.19.2 E20.f3b — Effets de coup 4 et 5 ✅ (faite et CONFIRMED le 2026-10-15, `5cfac5b` à `dfd72a3`, ADR-0067)

**Révision 1 (2026-10-15).** Relecture : **REVISE**, deux points corrigés (FIX) : (1) l'oracle que lisent (e1) et l'acceptation vient du T0
d'f3a, devenu prérequis ; (2) (w2) et (w3) passaient déjà en phase A : chacun affirme maintenant l'emplacement d'effet de (w1) dans la
même assertion, et l'arrêt « vert avant T3 » vise les trois.
Seconde relecture : **READY** (2026-10-15).

**Prérequis.** Le T0 d'E20.f3a (commit de l'oracle commun `docs/plan-e20-annexe/e20-f3/oracle/` : `hitfx_table` en JSON avec ses clés, `hitfx_mut`, sonde de la flamme), E20.c1a ✅, E14.a1b ✅, E20.e0b1 ✅. Indépendante des phases de code d'f3a et de f3c. Touche `AlundraWorldProxy.cs` : à exécuter après E20.e0b2 ou à relire ses numéros de ligne.

**But.** La passe de coups attache l'effet 4 ou 5 à la cible, comme `0x800395E8-0x80039668`.

**Périmètre.** `AlundraHitPass.cs`, `AlundraNativeEffects.cs`, `AlundraWorldProxy.cs` (`RunHitPassHook`), les commentaires périmés (« NOT PORTED », « no effect anchor »), tests, ADR.

**Hors périmètre.** Les producteurs nouveaux (E20.f6) ; la destruction anticipée de l'effet ; le pool de 128 effets pleins.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : la logique pure de la passe : paramètre facultatif `attachEffect`, porte, table des nibbles, ordre. Le monde passe `null` : rien ne change en production.
- **Phase B** (T3) : le câblage du monde : le délégué statique, l'opération sur l'étage, `CreateAttached` (D-F3-9). C'est la seule ligne de comportement de la phase.

##### Tâches

- ✅ **T1 — Phase A** (tests d'abord, rouges sur un squelette, verts à la fin de T1). `AlundraHitPassEffectTests` (acteurs faits à la main, rappel enregistreur) :
  - (e1) les **160 paires** (16 nibbles × 10 réponses) de `hitfx_table` : effets attendus = clé `fx`, `TouchingEntity` = clé `touch`, étincelles = clé `sparks`, `+0x1D0` = clé `c1d0`, `HitCounter` = clé `hitc`. Chaque ligne « immune » (`resp` 0x80, 0xBF : `fx = []`, `touch = none`, mais `c1d0 = 25`, `hitc = 1`, trois étincelles) est vérifiée dans la même boucle que les lignes non immunes des mêmes nibbles ;
  - (e2) ordre : le journal du rappel est [effet, étincelles] et `TouchingEntity` est encore nul quand l'effet est demandé ;
  - (e3) la cible héros (fiche 0) : l'effet est demandé pour les nibbles 6, 7, 9, 10.
  **Résultat (2026-10-15).** `Alundra.Tests/AlundraHitPassEffectTests.cs` (4 tests : (e1) les 160 paires de `hitfx_table.json` lues par leurs clés `attr`, `nib`, `resp`, `fx`, `sparks`, `c1d0`, `touch`, `hitc`, plus les huit `equipment_variants` ; (e2) ; (e3)). Les fiches sont fabriquées par un octet au rang `nibble + 2` (vie maximale pour 0, réponses pour 1 à 11, nombre d'attaques pour 12, octets d'attaque pour 13 à 15, via `AlundraBalanceRecord.ByteAt`). Sur le squelette qui compile (le paramètre `attachEffect` ajouté à `AlundraHitPass.Run` et ignoré) : **les 4 tests sont rouges** (« effects [], oracle [4] » sur la première ligne de nibble 6 ; (e2) et (e3) : collections différentes). Puis le code de la phase A dans `AlundraHitPass.cs` : `Run(liste, héros, étincelles, attachEffect = null)` ; dans la branche non immune (`(r & 0xC0) != 0x80`) le rappel est appelé avec (cible, effet) **avant** le magasin de `TouchingEntity` et avant les étincelles ; `EffectOfNibble` : 6 et 10 → 4, 7 et 9 → 5, sinon rien ; aucun tirage ; le commentaire « NOT PORTED » de la passe est remplacé. Le monde passe toujours `null` (`RunHitPassHook` inchangé). **4 tests verts** ; suite complète **4 880 = 4 876 + 4 en Release et en Debug, zéro échec** (compte de départ re-mesuré à 4 876 au HEAD `9ecc7d5`) ; aucun test existant ne bouge (les trois appels à trois arguments compilent et passent tels quels) ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques ; les cinq traces inchangées (garde `git diff --ignore-cr-at-eol` verte ; l'EOL seul restauré par `git checkout`).
- ✅ **T2 — Tests du monde, écrits après T1, rouges avant T3.** `AlundraHitEffectWorldTests` (`HitArena`, carte 14) :
  - (w1) un attaquant dont `CurrentAttack` est posé à (attribut `0x06`, puissance 1) touche le héros : un emplacement du réservoir avec `TargetEffect == 4`, mode 1, ancre = le héros, `DepthOffset == 1`, créé au tick de la touche, libéré 40 ticks après ; attribut `0x07` → effet 5, libéré après 95 ticks ; la même arène avec un attribut 1 ne crée aucun emplacement (cas « sans effet » dans la même assertion) ;
  - (w2) **dans la même assertion que l'emplacement de (w1)** (effet 4 présent, mode 1, ancre = le héros, `DepthOffset == 1`) : le mot
    du flux après la touche vaut celui d'une touche de nibble 1 (aucun tirage de l'effet), calculé par un pas local (D-F3-12) ;
  - (w3) groupements de 1, 2 et 3 ticks par image : dans **chaque** groupement, l'emplacement de (w1) existe (effet 4, mode 1, ancre =
    le héros, `DepthOffset == 1`), au même tick de création. (w1) à (w3) attendent chacun au moins un emplacement d'effet : la phase A
    (le monde passe `null`) ne peut en passer aucun.
  **Résultat (2026-10-15), phase A.** `Alundra.Tests/AlundraHitEffectWorldTests.cs` écrit (5 cas : (w1), (w2), (w3) × 1, 2, 3 ticks par image), **non commité** : le fichier est commité avec le câblage (T3), sans quoi aucun commit ne serait vert. Montage : un Beannoïde de la carte 14 est porté sur le héros, sa `CurrentAttack` posée à (attribut, 1) (son masque de classe, drapeaux `0x103`, atteint le héros) puis effacée après la première touche ; le réservoir est lu après chaque image ; le flux part de la graine `0x2468ACE0` au tick 1. Exécutés sur le code de la phase A : **les 5 sont ROUGES**, chacun à l'assertion de l'emplacement d'effet (« Collection was empty »), après que la touche a bien eu lieu au même tick (touche d'attribut 1, 6 et 7 constatée ; tick de touche identique aux trois groupements). La dérivation du flux de (w2) est validée sur le code actuel (touche de nibble 1) : mot après la touche = pas local `s·0x7D2B89DD + 0xE06A02E7` appliqué (ticks + 9) fois (un mot par tick, trois mots par étincelle, trois étincelles), à chaque image après la touche. Points que la phase B confirmera en vert : la durée mesurée aux bords d'image (premier bord sans l'effet = tick de touche + 40 pour l'effet 4, + 95 pour l'effet 5, à un tick par image) et l'ancre (le proxy du héros lui-même, `Assert.Same`).
- ✅ **T3 — Phase B** (commit de code B) : le câblage ; commentaires périmés corrigés ; suite complète ; aucun test existant ne bouge.
  **Résultat (2026-10-15).** Le câblage : `AlundraNativeEffects.CreateHitAttachedEffect(reservoir, effet, cible)` (`CreateAttached(false, id, 0, cible, BreakEffectDepthOffset = 1, 0, 0, 0)`, le modèle de l'effet de bris, mais l'ancre est le proxy vivant de la cible) ; `AlundraWorldProxy.AttachHitEffect` (délégué d'instance mis en cache une fois, `_attachHitEffect`, comme `_emitHitSparks` : aucune allocation par tick) met l'opération sur l'étage (`_effectStage.Emit`, rang 1 du tick, comme les étincelles) ; `RunHitPassHook` le passe à `AlundraHitPass.Run`. Aucun tirage. Les commentaires périmés (« NOT PORTED », « no effect anchor ») avaient été remplacés en phase A. **(w1) à (w3) VERTS** (5 cas) ; suite complète **4 885 = 4 880 + 5 en Release et en Debug, zéro échec** ; aucun test existant ne bouge (aucun test d'arc de la carte 14 ne bouge) ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques (DLL non verrouillée) ; les cinq traces inchangées (garde `git diff --ignore-cr-at-eol` verte ; l'EOL seul restauré par `git checkout`). **Précision de (w1), mesurée et non supposée** : la durée « 40 » et « 95 » est la somme S des délais de l'animation (20 images de 2 ticks, 19 de 5) ; le réservoir dessine un emplacement `Destroy` sur ses ticks 0 à S et le libère au tick S + 1, et l'étage applique les opérations d'un tick AVANT la mise à jour du réservoir de ce tick : le tick 0 de l'emplacement est donc le tick de la touche, et le premier bord d'image sans l'effet tombe à touche + 41 (effet 4) et touche + 96 (effet 5). Le test, écrit à +40 et +95 en phase A, attend maintenant +41 et +96. Écart d'un tick avec le binaire (où la passe de coups suit la passe des effets : premier tick de mise à jour touche + 1, soit touche + 42), commun aux étincelles (même étage, même rang) et sans conséquence visible.
  **Preuve de retrait.** Le monde repassé à `null` dans l'arbre de travail (`_hitPass.Run(..., null)`), construction avec la copie neutralisée, suite complète en Release : **4 885 tests, exactement 5 rouges** : `AlundraHitEffectWorldTests.W1_...`, `W2_TheEffectDrawsNoRandomWord` et `W3_TheSlotExists_InEveryGrouping_AtTheSameHitTick` aux groupements 1, 2 et 3 ; les 4 880 autres sont verts. Restauré par `git checkout --`, `git status` revenu à l'état d'avant.
- 🧪 **T4 — ADR, plan, verifiers frais** (deux). Mutants du code de la DLL, au moins 12, tous tués : masque `0xC0` → `0x80`, porte inversée, nibble 10 omis, 9 mappé sur 4, identifiants échangés, effet après le magasin, effet après les étincelles, effet pour une réponse immune, un seul effet par passe au lieu d'un par touche, `DepthOffset` 0, ancre = l'attaquant, rappel appelé deux fois.
  **Résultat (2026-10-15).** ADR : **rien à amender ni à écrire** ; l'ADR-0067 (f3a) enregistre déjà D-F3-9 et D-F3-10 comme PRÉVUES pour f3b et elles sont implémentées telles quelles (seul le détail « délégué statique » devient un délégué d'instance mis en cache une fois, `_attachHitEffect`, comme `_emitHitSparks` : même absence d'allocation par tick ; `docs/decisions/README.md` revérifié, dernier numéro de ces décisions 0067). **Quatorze mutants réels** (appliqués par script au fichier de travail, construction avec la copie neutralisée, tests `FullyQualifiedName~Hit`, restauration par `git checkout --`), **tous tués** : M1 masque `0xC0` → `0x80` → E1 (160 paires) et `AlundraHitPassRulesTests.Case3` ; M2 porte inversée → 44 tests (E1 à E3, W1 à W3, la famille `AlundraHitPassCatchUpTests`...) ; M3 nibble 10 omis → E1 (deux), E2, E3 ; M4 nibble 9 mappé sur 4 → E1 (deux), E2, E3 ; M5 identifiants échangés → E1 (deux), E2, E3, W1 à W3 ; M6 effet après le magasin de `TouchingEntity` → E2 ; M7 effet après les étincelles → E2, E3 ; M8 effet pour une réponse immune → E1 ; M9 un seul effet par passe → **survivait** aux tests de T1 et T2 (aucun ne frappait deux cibles dans une passe) ; correction : (e4) `E4_OneEffectPerHit_NotPerPass` (un attaquant de nibble 6 frappe deux cibles dans la même passe : [A:fx4, étincelle, B:fx4, étincelle], un second attaquant de nibble 7 ne demande rien), qui tue M9 ; M10 `DepthOffset` 0 → W1 à W3 ; M11 ancre = l'attaquant → E1 à E4, W1 à W3 ; M12 rappel appelé deux fois → E1 à E4 ; M13 (monde) identifiant toujours 4 → W1 ; M14 (monde) effet libre de mode 0 au lieu d'attaché → W1 à W3. Suite finale : **4 886 = 4 885 + 1 ((e4)) en Release et en Debug, zéro échec** ; `cmp` de `alundra-project/Alundra.dll` et de la DLL Debug identiques ; cinq traces inchangées (garde verte). **Reste : les deux verifiers frais, lancés par la session principale** (d'où 🧪).

**Vérification (2026-10-15) : CONFIRMED** par deux verifiers frais (rejeu : les 160 paires, suites complètes en Release et en Debug,
zéro échec ; preuve de retrait reproduite ; aucun test existant déplacé). **Reportés** :
- **P3** — la libération de l'effet tombe à touche + 41 (effet 4) et touche + 96 (effet 5) ; le binaire donnerait probablement touche + 42
  et + 97 (la passe de coups y suit celle des effets ; inférence de l'exécuteur, binaire non rejoué sur ce point). L'écart d'un tick vient
  du rang 1 de D-F3-9, commun aux étincelles, sans effet visible ; il est écrit dans le test. Point ouvert O-F3-6.
- **P4** — le message du commit `48cf43b` annonce des résultats du plan qui sont dans `dfd72a3`.

##### Acceptation

- Pass : les 160 paires conformes à l'oracle ; (e2) et (e3) verts.
- Monde : (w1) à (w3) verts, rouges avant T3 (nommés) ; compte de la suite plus les nouveaux, zéro échec en Release et en Debug ; **aucun test existant ne bouge** (les trois appels à trois arguments restent inchangés) ; `cmp` de la DLL ; cinq traces ; moteur, convertisseur et données sans diff.
- Recette de l'auteur (non bloquante, à confirmer par la sonde de la flamme du T0 d'f3a, `flame_probe.json`) : carte 14, la « flamme » de l'enregistrement 17 (sprite 238, case (14, 28)) ; si elle blesse le héros, l'effet 4 s'attache au héros pendant 40 ticks.

##### Arrêts, budget, retour arrière

- **Arrêts** : la phase A fait bouger un test ; (w1), (w2) ou (w3) vert avant T3 ; un test existant à réécrire ; un tirage de l'effet ; un test d'arc de la carte 14 qui bouge (la flamme touche un arc) : question à l'auteur ; une modification du moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (le monde repasse `null`) ; la phase A reste inerte.

---

#### 1.19.3 E20.f3c — La vague de l'épée (C61, E10) et l'activation des types 27 à 29 ⏳

**Prérequis.** E20.f3a, E20.e0a1 à E20.e0a3 ✅, E20.e0b1 ✅, la règle « Z logique » (D-F3-8) d'E20.e0k (§1.18) ou son repli (Q-F3-1). Touche `AlundraWorldProxy.cs`, `AlundraEventProgramRunner.cs` et `AlundraEntityScriptProxy.cs` : après E20.e0b2 si celle-ci reprend.

**But.** L'épée chargée tire la vague au tick `R + 21` ; elle vit 26 ticks, balaie sa boîte, touche les cibles de classe A, coupe l'herbe en bouchon nommé, et meurt à S+26 avec sa case libre à S+27.

**Périmètre.** `AlundraEventProgramRunner.cs` (E10 en ligne), nouveau fichier `AlundraWaveSwordAi.cs` (C61, bouchon nommé, journal), `AlundraNativeSpriteRegistry.cs` (un appel de plus), `AlundraPlayerManager.cs` (l'ensemble {27, 28, 29}), `AlundraEntityScriptProxy.cs` (la règle du Z logique si repli), `Alundra.Tests/` (support : échantillon de nouveau-né), `docs/plan-e20-annexe/e20-f3/`, ADR.

**Hors périmètre.** La coupe d'herbe réelle et son butin (E20.e, E20.e0c) ; tout autre type câblé ; A0 ; le déplacement de X et Y (O-F3-2).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1 à T3, sans changement de comportement en production) : E10 en ligne ; C61 et son bouchon ; la règle du Z logique (si repli) ; les tests de vie par le **crochet de test** (`WeaponSpawnBirthHookForTests` appelle l'interface au lieu exact du tir, tick de naissance compris). Aucune entité n'est née par l'interface en production (l'ensemble reste vide) : la règle du Z logique, restreinte aux entités nées par l'interface sans gravité, ne touche donc aucune entité de production.
- **Phase B** (T4) : l'ensemble câblé devient {27, 28, 29}. C'est la seule ligne de comportement de la phase. Les tests de tir de production (crochet nul) sont rouges avant elle.

**Valeurs attendues et leurs clés** (acier, est, relâchement à 96 ; fichiers `wave_<épée>_<direction>.json`, ligne du tick, `slots[1]`). Ticks de l'arène = ticks de l'oracle − 5 ; `pz` du binaire = Z de la DLL + 1 unité brute.

| Valeur | Clé | Attendu |
|---|---|---|
| Tick d'apparition | première ligne avec `slots[1]` | `S = R + 21` (117) |
| Statut | `status` | 2 de S à S+25 ; 4 à S+26 ; absent à S+27 |
| Compteur d'images | `cnt1c` | 1 à S ; 26 à S+25 ; 27 à S+26 |
| Drapeau de fin | `reset_AC` | 0 jusqu'à S+24 ; 1 à S+25 |
| Position | `px`, `py`, `pz` | constantes (X, Y du héros ; Z du héros + 1 unité) |
| Forces, `ForceAdjusted`, sol | `fx`, `fy`, `fz` ; `adj_13C` ; `ground_144` | 0 ; 0 ; 0 à chaque tick |
| Boîte | `hbx`, `hby`, `hbz`, `bw`, `bd`, `bh` | change à S, S+1, S+3, S+5, S+7, S+9 (est : x 82, 94, 106, 118, 130, 142 ; y 88) ; 36×24×31 |
| Attaque | `atk_attr`, `atk_pow` | 129 ; 10, 20, 38 (acier, démon, saint) |
| Type, drapeaux | `type68`, `flags6C` | 27, 28, 29 ; `0x205C` |
| Butin | `loot3c` | 0 (aucun tirage à la création) |
| Touche (cibles) | `c1d0`, `touch_slot` (`wave_reach_sweep`) | est +56, +68, +80 → S+5, S+7, S+9 par la vague ; +32, +44 → le héros |

##### Tâches

- ⏳ **T1 — Phase A, E10 et C61** (tests d'abord, rouges sur un squelette, puis le code). `AlundraWaveSwordLifeTests` (monde, nouveau-né née par le crochet) :
  - (v1) E10 : détruite à S+26, case libre à S+27 (une apparition suivante reprend la même case), aucun effet de bris créé (`BreakEffect` 0) ; **accolé** à `HitArena` (gestionnaires natifs éteints) qui détruit aussi la vague au même tick (D-F3-5) ;
  - (v2) C61 : le bouchon est appelé une fois à chaque tick de S+1 à S+25 (25 appels, clé `wave_grass2` : spy), jamais à S (A tourne) ni après S+25 ; paire : la même vague dans `HitArena` (natifs éteints) n'appelle pas le bouchon (cas négatif de l'interrupteur) ;
  - (v3) registre : 7 clés (6 + (C, 61)) ; le type 27 a (C, 61) et (E, 10 en ligne), aucun autre ; `AlundraNativeSpriteRegistryTests.cs:51` réécrit par dérivation (6 + 1).
- ⏳ **T2 — Sonde de vie, rouge avant T3.** (v4) la vague par le crochet, pour les épées 27, 28, 29 × les quatre directions de l'oracle (acier ; démon est ; saint est), à **1, 2 et 3 ticks par image** : pour chaque tick de S à S+27, `Status`, `FrameCounter`, `ForceResetAnimationFlag`, `PosX/PosY`, `PosZ` (clé `pz` − 1), forces, `ForceAdjusted`, `IsOnGround` et la boîte égalent les clés du tableau. Zéro changement et changement dans la même assertion : la position et le sol constants avec le balayage de la boîte. Le tick de mort mesuré **avant** la règle est noté au README (prédit : S+1 à S+3). Si la sonde est verte sans la règle, D-F3-8 tombe : arrêt et rapport.
- ⏳ **T3 — La règle du Z logique** (D-F3-8), si Q-F3-1 donne le repli B ; sinon T3 est « relire §1.18 » et la sonde de T2 sert d'acceptation d'e0k. Liste prédite des tests qui bougent : ceux qui lisent le Z d'une entité née par l'interface après sa première image (arbres de `AlundraBirthTickTests`) ; mesurée par la suite complète ; chacun réécrit par dérivation. T2 passe au vert aux trois groupements.
- ⏳ **T4 — Phase B, câblage de production.** Tests écrits d'abord, **rouges avant T4**, crochet nul, dans `HitArena` et dans `ArcRun` (cartes 14, natifs allumés pour le second) :
  - (p1) l'arène de la charge (relâchement à 91, S = 112) : une entité de type 27 fille du héros, aux clés du tableau, à la plus basse case libre mesurée avant le tir ; épées de démon et sainte : types 28 et 29, `atk_pow` 20 et 38 ;
  - (p2) la cible à +56, +68, +80 est touchée par la vague aux ticks S+5, S+7, S+9 (clé `wave_reach_sweep`) ; la cible à +44 l'est par le héros (cas positif et « la vague ne passe pas devant le héros » dans la même assertion) ;
  - (p3) la vague ne touche jamais le héros (`FrameCollisionTickCounter` du héros jamais posé par elle) dans la même exécution où la cible est touchée ;
  - (p4) l'épingle d'arène : 2 contacts avec E10 en ligne ; le mutant « E10 dans le registre » en donne 3 au tick 138 (clé `arena_probe`) ;
  - (p5) un tir d'arme non câblée (arc du chasseur) : compteur 1, journal 1 appel, **zéro** entité ; accolé au tir de l'épée : une entité ;
  - (p6) table pleine : 62 entités posées, le tir de la vague ne crée rien et le pointeur est effacé ; une case libre : une entité ;
  - (p7) dégâts : la fonction pure `AlundraDamage.ResolveBalanceTarget` pour (attribut 129, puissance 10, 20, 38) contre la fiche du Beannoïde ; `TouchingEntity` = la vague au tick de la touche (le dégât de bout en bout reste à la recette).
  Puis la ligne de câblage.
- ⏳ **T5 — ADR, plan, verifiers frais** (deux ; l'un sur les épingles : liste prédite contre liste mesurée). Mutants du code de la DLL, au moins 14, tous tués : E10 absent, E10 sous l'interrupteur natif, C61 absent, C61 aussi au tick S, unité de Z perdue, `IsOnGround` inversé, destruction avec effet, ensemble contenant 3, mapping des directions permuté, ordre de la garde, apparition deux fois, crochet qui ne remplace pas, tolérance du nul, boîte figée au repli.

##### Acceptation

- Oracle : les sorties de T0 (acier ×4 directions, démon, saint, balayages, `arena_probe`, mutants) ; manifeste identique.
- DLL : (v1) à (v4), (p1) à (p7) verts ; (v4) rouge avant T3 et (p1) à (p6) rouges avant T4 (nommés) ; la vague vit 26 ticks aux trois groupements ; `Alundra.Tests` en Release **et** en Debug, compte de départ plus les nouveaux, zéro échec ; tests déplacés ⊂ liste prédite (registre : 6 → 7 ; les arbres du Z), chacun justifié par sa dérivation ; l'épingle d'arène reste à 2 ; `cmp` de la DLL ; cinq traces ; moteur, convertisseur et données sans diff.
- Recette de l'auteur : sauvegardes `steel-m14`, `demon-m14`, `arsenal-b-m14` (F9). Charger, relâcher, la vague apparaît 20 ticks après et dure 26 ticks. Attendu : elle ne coupe pas l'herbe (Q-F3-2) et touche un Beannoïde à 56 px et plus.

##### Arrêts, budget, retour arrière

- **Arrêts** : la vague vit un nombre de ticks différent selon le groupement ; la sonde est verte sans la règle (D-F3-8 tombe) ; un test hors de la liste prédite bouge ; plus de 10 tests existants bougent ; la vague touche le héros ; l'arène donne 3 contacts avec E10 en ligne ; la règle de §1.18 diffère de D-F3-8 ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (vider l'ensemble) ; la phase A reste inerte en production.

---

**Points ouverts ajoutés.**

| # | Question | Tranchée par |
|---|---|---|
| O-F3-1 | Le flux de nombres aléatoires dérive après toute vague sur l'herbe (le bouchon ne tire pas les mots de la coupe) | E20.e |
| O-F3-2 | X et Y du nouveau-né sont arrondis au pixel par la racine : écart sous le pixel, boîte de la vague à moins d'un pixel de celle du binaire ; à mesurer avec un héros à position fractionnaire (T2 de f3c) | sonde de T2 ; E20.e0k |
| O-F3-3 | ~~L'enregistrement A0 du Muruta devra devenir un ensemble de types partagé pour f4 à f6~~ **Fermé le 2026-10-15** (découverte d'f4 à f6 : A0 est une donnée morte) | — |
| O-F3-4 | Les effets 4 et 5 sont créés en fin d'image : position de départ de fin d'image dans une image de plusieurs ticks | écart accepté ; E20.f6 si visible |
| O-F3-5 | Recette de f3b : la flamme de la carte 14 (enregistrement 17) est un candidat, pas un fait | sonde de la flamme du T0 d'f3a |
| O-F3-6 | La libération des effets 4 et 5 (et des étincelles, même rang) tombe un tick avant ce que le binaire donnerait probablement (touche + 41 au lieu de + 42) ; à rejouer sur le binaire, puis exactitude ou écart documenté | auteur, ou E20.f6 si visible |

### 1.20 E20.f4 — La flèche chargée de William : C94 et E1 avec l'effet de bris 8 ⏳ (détaillée le 2026-10-15)

**Réponses de l'auteur.** Aucune à ce jour (2026-10-15, mode AUTO, l'auteur absent). f4 ne pose aucune question. La seule question de f4 à f6,
Q-F5-1 est **déjà tranchée par l'auteur** (§1.1, « Questions de la nuit, tranchées le 2026-10-10 » : la flèche normale vole 16 px
au-dessus du sol du héros et passe au-dessus des ennemis bas, défaut fidèle sans question). Q-K1 et Q-K2 (§1.18) gardent leur défaut A.

Découverte en lecture seule du 2026-10-15, au HEAD `be9a067` (f3a phase A faite ; f3a phase B, f3b, f3c, e0k1 à e0k3 non faites) : trois relevés
(binaire, impact sur la DLL, inventaire des tests) communs à f4, f5 et f6, puis un critique adversarial commun. Le critique a redésassemblé E17, C94,
E1, A0, C60, E9, la passe de fin `0x80038064`, le nettoyage des cases `0x80038634`, `DestroyEntity`, le chargeur d'enregistrements et les opcodes `0x63`
et `0x1A`. Il a rejoué les scénarios des relevés (identiques) et les deux interpréteurs (huit scénarios identiques). Il a écrit un modèle à part des
tirages d'E9 (cinq sorties, accord), quatre recensements (enregistrements globaux, codes de gestionnaires, cibles de la carte 14, sites d'apparition),
neuf scénarios nouveaux et 36 mutants d'un mot sur E17, C94, E1, C60 et E9 (34 d'abord, tous tués sauf trois équivalents prouvés ; deux tués par
injection ; un de plus pour la borne de la boucle). Scripts et sorties dans le scratchpad de la session :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20f4f6/`
(`bin/`, `dll/`, `tests/`, `critic/`). Aucune modification du moteur, du convertisseur ni des données. La DLL n'a pas été exécutée : ce qui la concerne
est lu dans le code ou inféré.

**But.** Brancher la flèche chargée de William (type 7) : la traînée d'effets de C94, le bouchon nommé de la coupe d'herbe, la mort au mur ou au bord par E1
avec l'effet de bris 8. Rien d'autre ne naît (flèches : f5 ; boules : f6).

**Découpage.** Une seule sous-tranche, deux phases (§1.20.1). Ordre ferme (révision 9 de l'enveloppe) : f4, puis f5, puis f6a, puis f6b. Ce que les
trois partagent appartient à f4 (interrupteur d'arène par type, réécriture des témoins négatifs, annexe d'oracle commune et ses outils) et à f5
(règle de vitesse D-F5-5) ; les tranches suivantes le relisent et l'étendent sans toucher aux outils.

**Faits porteurs.** Étiquettes : [binaire] décodé sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS ; [DLL] lu dans le code au HEAD
`be9a067` (numéros de ligne à relire à l'exécution : e0k1 à e0k3, f3b et f3c touchent les mêmes fichiers) ; [données] export et cartes ; [inférence]
déduit, non exécuté ; [critique] établi ou corrigé par la critique.
- [binaire, données] **Le type 7.** Drapeaux `0x32054` : attaque la classe A (`0x4`), `DeactivateOnImpact` (`0x10`), `DeactivateOnAnimEnd` (`0x40`),
  `NoObstacleSlide` (`0x2000`), pas de gravité, pas de `DeactivateOnHit`. Codes A0, C94, E1. Corps (−10, −7, 0) de 21×15×8. `BreakEffect` 8, `Contents` 0.
  Fiche d'équilibre 7 : attribut 131, puissance 5, avec la puissance de l'équipement. Le seul site d'apparition est le tir du héros : fiche w9 de William
  chargé, animation `0x13`, délai 8, z 8.
- [binaire, oracle] **La naissance.** `S = R + 9` (105 pour R = 96 ; 100 pour la charge de l'arène, R = 91). Z brut 524 290 (8 px plus le repos du héros, plus 1).
  Le son 33 joue à S. A0 tourne à S, C94 de S+1 à la mort. La flèche avance de 6 px dès S (octet D `0x40`, accélération nulle).
- [binaire] **C94** (`0x8007C0D8`). Appelle `CheckAndTriggerTileEffect(e)` (`0x8003A7B0`) à chaque appel. Si `(+0x1C & 3) == 0`, crée l'effet global `0x19`
  d'animation `+0x98`, à (X, Y, Z + `0x80000`) de la fin du tick précédent, puis, s'il est créé, pose `ForceX = (−F_C4) >> 1`, `ForceY = (−F_C8) >> 1`
  (décalage arithmétique de l'opposé). Aucun tirage.
- [binaire] **E1** (`0x8007ED10`) : `DestroyEntity(e, −1)` ; `BreakEffect` 8 : `Attach(0, 8, 0, e, 1, 0, 0, 0)`, statut 4 avant la création.
- [oracle] **Vie, mur colonne 15, héros en x = 100.** Statut 2 de S à S+42. Positions : 106 à S, +6 par tick, 346 à S+40, 349 à S+41. `adj_13C` = 1 à S+42.
  À S+43 : statut 4 et `attach(8)`. La case est libre à S+44. Z constant 8,0. Traînée à S+4, S+8, …, S+40 (dix) : id 25, direction 3, x = 124 + 24(k−1),
  y = 100, z = 16, v = (−3, 0, 0). Ensemble des 12 départs 96 à 107 : drapeau de mur de S+41 à S+43.
- [oracle] **Elle traverse.** Contre le vrai Beannoïde : touche à S+29, 3 étincelles, 9 tirages, la flèche continue et meurt au mur à S+43. Sa boîte (Z 0 à 15)
  atteint les corps de 8 px. Un pas de 16 px comme un pas de 32 px (colonne 8) l'arrêtent (`adj_13C` à S+14, x = 181, détruite à S+15). Bord de carte en x = 1 150 :
  drapeau à S+14, x = 1 237, détruite à S+15.
- [oracle] **L'herbe.** Sur une bande de cases coupables, deux cases par colonne, toutes les 4 ticks : effet 2, son 31, de 2 à 3 tirages par colonne.
  Le bouchon n'en fait rien (E20.e).
- [oracle, critique] **Mutants.** C94-1 à C94-5 (période, identifiant, décalage de la vitesse, décalage de Z, direction), C94-5b (direction, héros au nord), E1-1
  (sans effet de bris) : tous tués. Pas de mutant équivalent pour f4.
- [DLL] **Le runner.** E0 et E1 sont en ligne, hors du registre (`AlundraEventProgramRunner.cs:336-342`). `ResolveSpriteType` donne 7 pour un enregistrement global.
  Le registre a 6 clés ; une clé en double lève une exception ; il est épinglé à `AlundraNativeSpriteRegistryTests.cs:51`.
- [DLL] **f3a (commité).** `ProductionWiredWeaponTypes` est vide. `UnhandledWeaponSpriteCodes(types, catalogue, registre)` vérifie les codes C et E. Le test
  `S5_...` contient quatre clauses qui basculent : `Contains((C,94))` pour le type 7 (à f4), `Contains((E,17))` et `Register(E,17,...)` pour la flèche (à f5).
- [DLL] **Les effets.** `AlundraEffectStage.Emit` met l'opération en file ; les tirages ont lieu à l'application, avant la mise à jour du réservoir du tick.
  Modèle : `CreateHitSparks`. Le Z des créateurs est celui du binaire (`PosZ + 1`, ADR-0026).
- [critique] **A0.** Il n'écrit que `AIValues[0]` (`0x280`) ; les huit lectures de l'image sont des lectures de soi dans d'autres sprites.

**Écarts tranchés par la critique** (les relevés sont corrigés ainsi ; les audits restent en lecture seule).
1. *« f4 à f6 devront élargir l'enregistrement A0 du Muruta »* (D-F3-7, O-F3-3) est **REFUTÉ**. A0 est une donnée morte pour ces types. Pas d'enregistrement,
   pas de modification du fichier du Muruta. O-F3-3 est fermé.
2. *« Les témoins négatifs de f3a (S5, S1) tiennent »* est **CORRIGÉ** : ils basculent à f4 et f5. Remplacés une fois, par dérivation, par un registre vide
   et un ensemble vide de test (D-F4-8).
3. *« HitArena suffit aux tests natifs »* est **CORRIGÉ** : elle éteint tous les gestionnaires natifs. Interrupteur par type (D-F4-7).
4. *« La dérive du flux après l'herbe n'est que f3c »* est **ÉLARGI** : la flèche chargée coupe deux cases par colonne ; O-F3-1 vaut aussi pour f4.

**Décisions de conception (ADR : le prochain numéro libre au moment de l'écriture, revérifié ; un ADR commun à f4, f5 et f6).**
- **D-F4-1** — Une sous-tranche, deux phases : A (gestionnaire, créateur, bouchon, clé du registre, tests ; rien de visible en production) ; B (la ligne `7`).
- **D-F4-2** — Créateur de la traînée : effet global `0x19`, animation = l'indice de direction de l'entité (celui de `+0x98`), position (X, Y, Z du binaire + `0x80000`),
  forces `(−ForceX) >> 1` et `(−ForceY) >> 1` (décalage arithmétique de l'opposé, pas une division), aucune force en Z, aucun tirage. Posé par `Emit` ; les valeurs
  sont capturées à l'émission. Créé seulement en statut Normal, quand `FrameCounter & 3 == 0`. L'indice vient de la direction cible de l'entité (table inverse de
  `{0, 0x10, 8, 0x18}`), pas de `AnimationDirection` ; le T0 épingle l'égalité avec `BoxDirection` aux quatre directions.
- **D-F4-3** — C94 appelle le bouchon nommé `CheckAndTriggerTileEffect(entité)` (D-F3-6) à chaque appel, avant la traînée. Le bouchon est celui de f3c, dans un fichier
  neutre : s'il est dans le fichier de la vague, f4 l'en extrait par un déplacement pur (les tests de f3c ne changent pas).
- **D-F4-4** — E1 reste en ligne. Le type 7 s'appuie sur `BreakEffect` 8 de l'en-tête (E14.a1b ✅) ; aucun changement de runner.
- **D-F4-5** — Clé du registre (C, 94) pour {7} ; pas de clé A0. Une classe `AlundraChargedArrowAi` expose ses clés ; le test du registre somme les clés des classes d'IA
  (dérivation), plus un cas nommé par clé, et rien ne dépend de l'ordre f3c / f4 / f5 / f6.
- **D-F4-6** — Phase B : 7 entre dans `ProductionWiredWeaponTypes`. Le vérificateur de D-F3-4 doit être vide pour {7} avec le registre de production.
- **D-F4-7** — Interrupteur d'arène, additif : `AlundraEventProgramRunner.NativeSpriteTypeFilterForTests` (nul en production), lu avec `NativeHandlersEnabled`. Une arène
  allume les types de projectiles et laisse les Beannoïdes inertes. Choix de l'exécuteur au T1 : option de `HitArena`, ou `ArcRun` plus l'interrupteur (README).
- **D-F4-8** — Témoins négatifs durables, réécrits une fois (par la première des tranches) : les clauses négatives de S5 prennent un **registre vide** ; la clause
  positive garde le registre de production ; le premier bloc de S1 et (p5) de f3c prennent un ensemble de test **vide** à la place de l'arc du chasseur.
- **D-F4-9** — Tick du mur : Q-K2, défaut A. Les tests d'une flèche contre un mur lisent la clé de l'oracle et prennent pour attendu le tick d'arrivée au mur plus 1 ; il égale
  la clé de l'oracle pour 8 départs sur 12 et la précède d'un tick pour les 4 autres (x0 = 98, 101, 104, 107). Cette fenêtre est écrite dans le code et ici.
- **D-F4-10** — Coupe d'herbe : le bouchon ne coupe rien (E20.e) ; le flux ne reçoit pas les mots de la coupe (O-F3-1, O-F4-1).

**Questions à l'auteur.** Aucune.

**Prérequis et dépendants.** E20.f3 (f3a phases A et B, f3b, f3c pour le bouchon), E20.e0k1 à E20.e0k3, E14.a1b ✅. Dépendants : E20.e (ferme le bouchon d'herbe).
f5 exige f4 ; f6b exige f4 et f5 (éléments partagés ci-dessus) ; f6a ne dépend que d'e0k1 et e0k2 mais passe, dans l'ordre ferme, après f5.

---

#### 1.20.1 E20.f4 — La flèche chargée (C94, E1, effet 8) ⏳

**Prérequis.** Voir ci-dessus. **À relire à l'exécution** : le nom du bouchon de f3c et son fichier, le marqueur `HasLogicalZ` et l'état de carte d'e0k1 à e0k3, la liste des
tests nés par l'interface recomptée au HEAD de départ.

**But.** La flèche chargée naît à R+9, vole à 6 px par tick à Z constant, laisse une traînée, traverse les cibles, et meurt au mur ou au bord avec l'effet de bris 8.

**Périmètre.** Nouveau fichier `Alundra/Scripts/AlundraChargedArrowAi.cs` (C94, créateur de la traînée, clés) ; `AlundraNativeSpriteRegistry.cs` (un appel de plus) ;
`AlundraEventProgramRunner.cs` (l'interrupteur par type) ; `AlundraPlayerManager.cs` (la ligne `7` en phase B) ; le fichier du bouchon (déplacement pur si besoin) ;
`Alundra.Tests/` ; annexe `docs/plan-e20-annexe/e20-f4f6/` ; ADR.

**Hors périmètre.** La coupe d'herbe réelle et son butin ; les flèches (f5) ; les boules (f6) ; A0 ; le tir en l'air de la flèche chargée.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, aucun changement de comportement en production) : C94, le créateur, la clé du registre (aucun enregistrement placé par une carte n'a le type 7), l'interrupteur
  d'arène, la réécriture des témoins négatifs, l'épingle du registre par dérivation. Les tests de vie utilisent le **crochet de test**.
- **Phase B** (T3) : la ligne `7` de `ProductionWiredWeaponTypes`. Seul changement de comportement. Les tests de tir de production (crochet nul) sont rouges avant elle.

**Valeurs attendues et leurs clés** (fichiers `charged7_east_wall.json`, `slots[k]` du type 7 ; ticks relatifs à S ; Z de la DLL = Z de l'oracle − 1 unité brute).

| Valeur | Clé | Attendu |
|---|---|---|
| Naissance | première ligne du type 7 | S = R + 9 (105) |
| Statut | `status` | 2 de S à S+42 ; 4 à S+43 ; absent à S+44 |
| X | `px` | 106 à S, +6 par tick, 346 à S+40, 349 à S+41 et S+42 |
| Z, forces | `pz`, `fx`, `fz` | 8,0 ; 393216 ; 0 |
| Drapeau de mur | `adj_13C` | 1 à S+42 (départ x0 = 100) ; fenêtre S+41 à S+43 sur 96..107 |
| Boîte, attaque | `bw,bd,bh,hbz`, `atk_attr`, `atk_pow` | 24×16×15 à hbz 0 ; 131 ; 5 |
| Drapeaux | `flags6C` | `0x32054` |
| Traînée | `effects` | id 25, dir 3, y 100, z 16, v (−3, 0, 0) à S+4, +8, …, +40 ; x = 124, 148, …, 340 |
| Mort | `effects` | `attach(id 8, cible = la flèche, mode 1)` à S+43 |
| Sons | journal | 33 à S ; 0 muet ailleurs |
| Tirages | `draws` | 0 (traînée et bris) |

Autres clés : `charged7_free_{west,north,south}`, `charged7_map_edge`, `charged7_step16`, `charged7_step32`, `charged7_vs_target` (touche à S+29, 9 tirages),
`charged7_grass`. Pour la cible : la flèche touche une fois par fenêtre de 25 ticks (`c1d0` = 25).

##### Tâches

- ⏳ **T0 — Prédiction et oracle** (commit de docs, avant tout code). Annexe commune `docs/plan-e20-annexe/e20-f4f6/` (créée par f4 ; f5, f6a et f6b l'étendent sans
  toucher aux outils) :
  - les outils de la machine copiés octet pour octet depuis `docs/plan-e20-annexe/e20-f3/oracle/tools` avec leurs sommes de contrôle ;
  - un script par tranche ; sorties en texte **et** en JSON avec les clés ci-dessus, plus `.draws`, `.rng0`, `.rng1`, `.effects[]` (objets `{id, dir, x, y, z, vx, vy, vz}`
    ou `{attach, id, cible, mode}`) et `.snd[]` ;
  - deux exécutions complètes donnent un manifeste identique ;
  - les scénarios de f4 (liste ci-dessus), avec la flèche suivie par ses drapeaux (jamais « la case 1 », piège du bouton tenu) ;
  - le suivi de la direction aux quatre directions (indice de `+0x98` contre `BoxDirection`) ;
  - au moins 12 mutants d'un mot, tous tués ou prouvés équivalents par écrit : C94-1 à C94-5, C94-5b, E1-1, appel du bouchon retiré (clé : cases coupées), drapeau
    `0x20` posé dans l'en-tête (la flèche ne traverse plus), `BreakEffect` 8 à 0, octet z de la fiche de 8 à 16, `+1` du Z de création retiré ;
  - README : valeurs attendues et clés, prédiction qualitative (phase A : bougent l'épingle du registre, S5 et les témoins de D-F4-8 ; phase B : aucun test existant),
    compte de départ de la suite (4 857 + les nouveaux tests de f3a et f3c, à relever), liste prédite des tests nés par l'interface qui font tourner des images,
    recomptée au HEAD de départ.
  - **Sondes de la DLL** (écrites, lancées au HEAD de départ, versées au README, puis retirées ; prédiction écrite avant) : P-F4-1, indice de direction de l'effet aux quatre
    directions ; P-F4-2, la boîte de la flèche au tick S (résultat de la sonde v4 de f3c).
- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette qui compile, puis le code). Chaque cas « sans effet » est accolé à un cas positif dans la même assertion :
  - (c1) C94 direct, entité faite à la main : à `FrameCounter` 4 une traînée (id, direction aux quatre indices, position, Z + `0x80000`, forces `−F >> 1` pour 393216 et pour
    1, qui donne −1), à 3 et 5 aucune ; le bouchon est appelé aux trois ticks ; réservoir plein : aucun effet, bouchon appelé, aucune exception ; le mot du flux est inchangé ;
  - (c2) registre : (C, 94) pour 7 seulement (pas pour 3, 4, 11) ; épingle par dérivation ; (c3) S5 réécrit (D-F4-8) ;
  - (c4) vie par le crochet, tableau ci-dessus, aux groupements de 1, 2 et 3 ticks par image ; pas de traînée à S ni en statut 4 ;
  - (c5) la flèche traverse : touche la cible à S+29 (`hitc` 1, `c1d0` 25, 3 étincelles, 9 tirages) et meurt au mur à S+43, accolée à la même flèche sans cible ;
  - (c6) pas de 16 px et de 32 px : bloquée dans les deux cas, accolée au couloir libre ; bord : drapeau à S+14, x = 1 237, accolé à un départ non borné ;
  - (c7) fenêtre du mur (D-F4-9) sur les 12 départs ; (c8) herbe : le bouchon est appelé à chaque tick de S+1 à S+42, aucune case coupée, le flux égale l'avance pure
    (O-F4-1 épinglé) ;
  - (c9) l'interrupteur d'arène : un type de projectile s'exécute, un Beannoïde reste inerte, dans la même arène.
  Puis le code de la phase A. Verts à la fin de T1.
- ⏳ **T2 — Tests de production**, crochet nul, **rouges avant T3** :
  - (p1) la charge de William (arène de la charge) : une entité de type 7, fille du héros, à la plus basse case libre mesurée avant le tir, au tick R+9 ; sa vie suit le tableau.
    Dans la même assertion, le tir normal du début de la prise crée une entité de type 4 **si et seulement si** `ProductionWiredWeaponTypes` contient 4 (dérivation) ;
  - (p2) table pleine : aucune entité, pointeur effacé ; une case libre : une entité ;
  - (p3) dégâts : `AlundraDamage.ResolveBalanceTarget` pour (attribut 131, puissance 5) contre la fiche du Beannoïde ; `TouchingEntity` = la flèche au tick de la touche.
- ⏳ **T3 — Phase B** : la ligne `7`. Verts ; la suite complète.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux, l'un sur les épingles : liste prédite contre liste mesurée). Mutants du code de la DLL, au moins 12, tous tués : C94 absent ;
  période 8 ; traînée aussi à S ; traînée en statut 3 ; mauvaise animation ; forces non décalées ; décalage de Z de 4 px ; bouchon appelé seulement toutes les 4 ticks ;
  bouchon jamais ; 7 oublié dans le registre ; 7 absent de l'ensemble ; 4 présent dans l'ensemble ; E1 sans effet de bris.

##### Acceptation

- **Oracle** : manifeste identique sur deux exécutions ; au moins 12 mutants tués ou prouvés équivalents ; traces des 12 départs du mur.
- **DLL** : tous les tests de T1 et T2 verts. (p1) à (p3) rouges avant T3 (nommés). `Alundra.Tests` en Release **et** en Debug, compte de départ plus les nouveaux, zéro échec.
  Tests déplacés ⊂ liste prédite (épingle du registre, S5, premier bloc de S1 si f5 est déjà faite). `cmp` de la DLL. Cinq traces inchangées. Moteur, convertisseur et
  données sans diff.
- **Preuve de retrait** : retirer la ligne `7` rend rouges exactement (p1) à (p3) ; retirer l'appel du bouchon rend rouge (c1) et (c8) seulement ; retirer la clé (C, 94)
  rend rouges (c2) et (c4) ; aucun autre test ne change.
- **Recette de l'auteur** (non bloquante) : `arsenal-b-m14` (F9), l'arc de William en emplacement 2. Charger environ 90 ticks, relâcher : la flèche part 9 ticks plus tard avec une
  traînée, blesse un Beannoïde sans s'arrêter, meurt au mur avec l'effet de bris. Elle ne coupe pas l'herbe (E20.e).

##### Arrêts, budget, retour arrière

- **Arrêts** : la phase A fait bouger un test hors de la liste prédite ; une entité née en production avant T3 ; (p1) vert avant T3 ; un tirage par C94 ou par le bris ; la flèche
  prend E1 à S+1 (l'unité de Z est perdue : e0k2 ne tient pas) ; le drapeau de mur hors de la fenêtre ; plus de 10 tests existants bougent ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (retirer `7`). La phase A reste inerte en production.

---

### 1.21 E20.f5 — Les flèches du chasseur et de William : E17 ⏳ (détaillée le 2026-10-15)

**Réponses de l'auteur.** Q-F5-1 est **déjà tranchée par l'auteur** (§1.1, « Questions de la nuit, tranchées le 2026-10-10 » : la flèche normale vole 16 px
au-dessus du sol du héros et passe au-dessus des ennemis bas, défaut fidèle sans question) ; la question est gardée plus bas pour mémoire.

Découverte : celle de §1.20 (relevés communs et critique commun). Les faits propres à f5 ont été revérifiés par le critique : le désassemblage complet d'E17, 12 mutants
(dont 2 par injection après la touche et 1 pour la borne de la boucle), la portée verticale (16 rate, 17 touche), la cible détruite dans le même tour, la cible dans la
dernière case.

**But.** Brancher les flèches normales (types 3 et 4) : le gestionnaire E17 (touche, rebond contre le mur ou le bord, mort après 16 ticks de saut) et la règle de vitesse
horizontale des entités nées par l'interface.

**Faits porteurs.** (Étiquettes comme au §1.20.)
- [binaire, données] **Les types 3 et 4.** Drapeaux `0x12074` : attaque la classe A, `DeactivateOnImpact`, `DeactivateOnHit`, `DeactivateOnAnimEnd`, `NoObstacleSlide`. Codes
  A0, E17. Fiche d'équilibre : 131 / puissance 1 (type 3) ou 2 (type 4), avec la puissance de l'équipement. Les types 5 et 6 ont les mêmes codes mais aucune attaque.
  Tir : fiche w8 (chasseur : type 3, délai 8 au sol, 0 en l'air, z 16) et w9 (William : type 4, délai 8, z 16).
- [oracle] **La naissance.** `S = P + 9` au sol (10), `P + 1` en l'air pour le chasseur. Z brut 1 048 578. X = x0 + 3 à S (octet D `0x41`, accélération 1), puis +6 par tick.
  Boîte (−12, −8, 0) de 24×16×11 au Z de la flèche, donc 16 à 27 : portée verticale.
- [binaire] **E17** (`0x8007F420`).
  - Si `+0x88 == 1` (phase de rebond) : `+0xAC == 1` donne `DestroyEntity(e, −1)` ; sinon rien.
  - Sinon, si `+0x220 == 0` (pas de touche) : rebond.
  - Sinon le masque `m = ((F<<2)&8) | (F&4 ? 1:0) | (F&0x1000 ? 0x800:0)` ; pour les flèches `m = 1`.
  - Balayage des cases 0 à `[0x80134360]` inclus, **sans filtre de statut** ; ignore soi, `+0xB4 & 0x40`, `+0x1CC ≠ 0`, `(+0x6C & m) == 0`, et le type `0x1AD`.
  - Recouvrement inclusif de la boîte stockée de la flèche contre le corps de la cible.
  - Trouvé : `DestroyEntity(e, −1)` (aucun bris), puis les écritures du rebond sur l'entité morte (sans effet). Non trouvé : statut 2, la flèche continue.
  - Rebond : `+0x88 = 1`, `+0x8C = (+0x8C + 0x10) & 0x1F`, `+0x6C = (+0x6C | 0x140) & ~0x30`. Seul le bit de gravité est vivant (statut 3 prend E à chaque tick ; `0x40` est
    déjà posé ; `~0x30` ne sert plus).
- [oracle] **Mur colonne 15, x0 = 100.** Voir le tableau de valeurs. Saut de 16 ticks : l'animation 1 pose l'impulsion 1 024 (ForceZ 262 144 à S+43), la gravité de la carte
  128, la vitesse −1,875 px par tick vers l'ouest, la boîte disparaît, le son 34. Fin à S+60, case libre à S+61. Mur ouest, nord, sud : drapeau à S+4, S+12, S+22 ;
  détruite à S+22, S+30, S+40. Bord : drapeau à S+15, x = 1 237, rebond S+16, détruite S+33. Pied du mur (12 px) : 1 px à S, drapeau S+1, rebond S+2.
- [oracle, critique] **Touche.** Une cible haute (corps ≥ 17) en x = 300 : touche à S+30, 3 étincelles (9 tirages), E17 détruit à S+31, case libre S+32. Cible de 16 : aucune
  touche. Cible rendue invulnérable après la touche (`+0x1CC := 0x78`) : la flèche repart au statut 2 et vole. Cible de type `0x1AD` : touchée une fois, la flèche vole.
  Deux cibles : touchées le même tick (18 tirages), une seule mort. **Cible détruite dans le même tour** (statut 4, case plus basse) : toujours trouvée. **Cible dans la
  dernière case** (flèche dans une case plus basse) : trouvée ; un balayage arrêté une case trop tôt la manque.
- [DLL] **Vitesse.** `TickScriptedNpc` règle la vitesse sur `CurrentAnimationId`, assigné seulement en fin d'image (`SyncAnimation`) ; la direction vient de `TargetDirection`.
  [inférence] Au tick du rebond (1 tick par image) la force passe de +393 216 à 0 au lieu de −122 880 : décalage de 1,875 px, persistant. e0k1 pose l'épingle P3 « une entité
  marquée garde la clé de son animation courante » : la règle de D-F5-5 la réécrit.
- [DLL] **Le balayage.** Les entités sont `SpawnedEntities` plus le héros (`PlayerEntity`). `AlundraHitPass.AxisOverlaps` donne la forme inclusive ; la boîte stockée est
  `HitBoxX/Y/Z` plus `CollisionWidth/Depth/Height` (`StoreHitBox`, après chaque tick) ; le corps de la cible est `Pos + Mod` avec la correspondance Width/Height/Depth du coup
  (`AlundraHitPass.cs:109-131`) ; `DamagedTickCounter` est écrit seulement par l'opcode `0x86`.
- [DLL] **Le rebond.** `ResyncControllerFromFlags` ne change rien ici : `WalkabilityMaskFor` ne lit que ClassA et ClassB, et la gravité du contrôleur est toujours nulle.
- [critique] **Mutants d'E17.** E17-1 à E17-12 : 10 tués, E17-4 équivalent (`0x40` déjà posé), E17-5 tué seulement par `flags6C` (écriture morte). E17-8 et E17-9 tués par une
  injection après la touche. Borne de la boucle tuée par `arrow3_target_higher_slot`.

**Écarts tranchés par la critique.**
1. *« La flèche vole à travers une cible touchée »* (relevé du binaire) est **PRÉCISÉ** : vrai seulement pour une cible dont `+0x1CC` est posé avant E17. En production : 0x86 ou E14.
2. *« Le décalage de vitesse est d'environ 4,1 px »* (relevé de la DLL) est **CORRIGÉ** : 1,9 px (flèche, inférence) ; pour une boule, 6 px par tick restant de l'image.
3. *« N9 : E17 appelle `ResyncControllerFromFlags` »* est **PRÉCISÉ** : sans effet visible ; gardé en assurance (D-F5-4).
4. *« Types 5, 6, 8, 9, 10, 13 sans site d'apparition »* est **CORRIGÉ** : sans site à type constant ; des sites dynamiques restent (inférence).
5. *« Q2 (tirages différés d'E9) est une question »* est **REFUSÉ** : D-E0B-3(a) est accepté.

**Décisions de conception.**
- **D-F5-1** — Une sous-tranche, deux phases : A (E17, clé, règle de vitesse, tests) ; B (l'ensemble {3, 4}).
- **D-F5-2** — E17 en classe `AlundraArrowAi`. Balayage : toutes les entités de `SpawnedEntities` **sans filtre de statut** (y compris FlagToDestroy) plus le héros ; saute soi,
  `HitAnimByte & 0x40`, `DamagedTickCounter ≠ 0`, un `Flags` sans bit du masque, et `SpriteType == 429` ; recouvrement par `AxisOverlaps` sur la boîte stockée de la flèche.
- **D-F5-3** — Trouvé : `DestroyEntity(e, −1)` puis retour ; l'équivalence avec la chute dans le rebond (écritures sur une entité morte) est écrite dans le code.
- **D-F5-4** — Rebond : cible d'animation 1, direction `(TargetDirection + 0x10) & 0x1F`, `Flags = (Flags | 0x140) & ~0x30`, puis `ResyncControllerFromFlags()` en assurance.
  Les tests lisent `Flags` (0x12144) ; le retrait de l'appel est un mutant équivalent prouvé.
- **D-F5-5** — (**partagée avec f6 ; portée par f5**, qui passe avant f6b dans l'ordre ferme) Une entité née par l'interface prend la clé de vitesse de `TargetAnimationId` (celle que l'horloge du tick
  charge, comme `TickPlayer`), non de `CurrentAnimationId`. Le mouvement du tick redevient exact. L'épingle P3 d'e0k1 est réécrite par dérivation (clé de l'animation cible).
- **D-F5-6** — Registre : (E, 17) pour {3, 4, 5, 6} (l'ensemble complet de l'en-tête) ; ensemble câblé {3, 4}. Pas de clé A0.
- **D-F5-7** — Fenêtre du mur : D-F4-9. La flèche rebondit un tick après le drapeau ; l'attendu est dérivé comme en f4.
- **D-F5-8** — Portée verticale : fidèle (Q-F5-1, défaut A). Le test de portée est accolé : cible de 16 ratée, cible de 17 touchée.
- **D-F5-9** — La mort sur touche et le rebond ne créent aucun effet (`BreakEffect` 0). Le saut ne touche personne (la boîte disparaît au tick du rebond).

**Questions à l'auteur.**
- **Q-F5-1 — La flèche normale passe-t-elle au-dessus des petits ennemis ?** *L'original :* la flèche du chasseur et la flèche normale de William volent à 16 pixels du sol. Un
  ennemi de 16 pixels de haut ou moins n'est pas touché ; un ennemi de 17 pixels ou plus l'est. Les Beannoïdes de la carte 14 font 8 pixels. La flèche chargée de William et les
  boules les touchent. *Le port :* il reproduit cela, comme le binaire. *Visible en jeu :* à l'arc normal, les Beannoïdes de la carte 14 ne sont pas blessés (l'épée, la
  flèche chargée et les baguettes les blessent) ; les ennemis de 17 pixels et plus le sont (par exemple le sprite 136 de cette carte). *Option A (défaut, recommandée, fidèle) :*
  garder. *Option B :* faire voler la flèche plus bas pour toucher aussi les petits ennemis. Ce n'est pas l'original ; cela change aussi quels ennemis une flèche atteint sur
  toutes les cartes ; un écart de plus à porter. Réversible. Rien dans le binaire ne montre que c'est involontaire ; c'est pourquoi la question est posée (règle du 2026-09-25 :
  un bug avéré de l'original se corrige).

**Prérequis et dépendants.** E20.f4 (ordre ferme : interrupteur d'arène, témoins négatifs, annexe), E20.f3, E20.e0k1 à E20.e0k3. Dépendants :
E20.f6b (règle de vitesse D-F5-5) ; aucun autre avant E14 (flèches des ennemis) et la recette.

---

#### 1.21.1 E20.f5 — Les flèches (E17) et la règle de vitesse ⏳

**Prérequis.** Voir ci-dessus, et **E20.f4** (ordre ferme de la révision 9 : l'interrupteur d'arène D-F4-7, les témoins négatifs D-F4-8 et l'annexe commune viennent d'f4). **À relire à l'exécution** : la garde de D-E0A-8 dans `TickScriptedNpc`, l'épingle P3 d'e0k1, le marqueur d'e0k2, l'interrupteur d'arène de f4.

**But.** Les types 3 et 4 vivent comme dans le binaire : ils volent, touchent, rebondissent et meurent. Les entités marquées avancent à la vitesse de l'animation du tick.

**Périmètre.** Nouveau fichier `Alundra/Scripts/AlundraArrowAi.cs` ; `AlundraNativeSpriteRegistry.cs` (un appel) ; `AlundraScriptedMotion.cs` (la clé, une ligne) ;
`AlundraPlayerManager.cs` (l'ensemble) ; `Alundra.Tests/` (nouveaux tests ; réécriture par dérivation de S5, du premier bloc de S1, de (p5) de f3c et de P3) ; annexe ; ADR.

**Hors périmètre.** Les flèches des ennemis (E14) ; les enregistrements globaux de flèches (aucun) ; la portée verticale (Q-F5-1) ; `DamagedTickCounter` écrit par des
gestionnaires (E14) ; le tir en l'air hors de la sonde.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, aucun changement de comportement en production) : E17, la clé, la règle de vitesse (aucune entité marquée n'existe en production), les réécritures (l'interrupteur
  d'arène vient d'f4, prérequis). Les tests de vie utilisent le crochet.
- **Phase B** (T3) : l'ensemble {3, 4}. Seul changement de comportement.

**Valeurs attendues** (`arrow3_east_wall.json`, `slots[k]` du type 3 ; S = 10 ; x0 = 100 ; Z DLL = Z oracle − 1 unité brute).

| Valeur | Clé | Attendu |
|---|---|---|
| Naissance | première ligne | S = P + 9 |
| Statut | `status` | 2 de S à S+42 ; 3 de S+43 à S+59 ; 4 à S+60 ; absent à S+61 |
| X | `px` | 103 à S, 109 à S+1, +6, 349 à S+41 et S+42 ; 347,125 à S+43 puis −1,875 par tick jusqu'à 317,125 à S+59 |
| Z | `pz` | 16,0 jusqu'à S+42 ; puis 20 ; 23,5 ; 26,5 ; 29 ; 31 ; 32,5 ; 33,5 ; 34 ; 34 ; 33,5 ; 32,5 ; 31 ; 29 ; 26,5 ; 23,5 ; 20 ; 16 (S+43 à S+59) |
| Forces | `fx`, `fz` | fx 196608 à S, 393216 de S+1 à S+42, −122880 de S+43 ; fz 0 puis 262144 à S+43 et −32768 par tick |
| Mur | `adj_13C` | 1 à S+42 (fenêtre D-F4-9) |
| Direction, animation, drapeaux | `dir_t`, `anim_t`, `flags6C` | 24 puis 8 ; 0 puis 1 ; `0x12074` puis `0x12144` à S+43 |
| Boîte | `box1d4` | 1 puis 0 à S+43 |
| Fin d'animation | `reset_AC` | 1 à S+59 |
| Son | journal | 34 à S+43 |
| Touche (cible haute en x = 300) | `hitc`, `c1d0`, `touch_slot`, `draws` | 1 ; 25 ; la flèche ; 9 à S+30 ; mort à S+31 ; case libre S+32 |

##### Tâches

- ⏳ **T0 — Prédiction et oracle** (commit de docs). Annexe commune (§1.20, T0). Scénarios de f5 : les quatorze des relevés (`arrow3_east_wall`, `arrow4_east_wall`,
  `arrow3_free_*`, `arrow3_wall_*`, `arrow3_map_edge`, `arrow3_point_blank`, `arrow3_target`, `arrow3_target_low`, `arrow4_target`, `arrow3_target_invulnerable_after_hit` avec la
  colonne 15, `arrow3_target_1AD`, `arrow3_two_targets`) plus les nouveaux : `arrow3_target_filter_b4` et `arrow3_target_filter_class` (injection après la touche),
  `arrow3_target_dead_same_dispatch`, `arrow3_target_higher_slot`, `arrow3_zreach_16` et `arrow3_zreach_17`, `arrow3_target_sprite136` (corps réel 14×16×17), le tir en l'air
  du chasseur (S = P+1). Au moins 12 mutants : E17-1 à E17-12 (tués ou équivalents), la borne de la boucle, le délai de l'arc, l'octet z de la fiche. Sondes de la DLL : tick du
  rebond et position à 1, 2 et 3 ticks par image **avant** la règle (prédiction écrite : 1,875 px d'écart à 1 tick) ; le tick du drapeau sur les 12 départs. README : valeurs,
  liste prédite des tests qui bougent (registre, S5, S1, (p5), P3, les tests nés par l'interface qui font tourner des images, recomptée au HEAD de départ).
- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette, puis le code). Chaque « sans effet » est accolé à un positif :
  - (e1) E17 direct : touche avec une cible valide → `FlagToDestroy`, aucun effet ; sans touche → rebond aux quatre directions (24→8, 8→24, 16→0, 0→16), cible d'animation 1,
    `Flags` 0x12144, bit de gravité posé, masque de marche et gravité du contrôleur inchangés ;
  - (e2) un filtre par cas, chacun accolé à la cible valide : `DamagedTickCounter ≠ 0` (statut 2, la flèche vit), `HitAnimByte & 0x40`, sans bit de classe, `SpriteType 429`, soi-même,
    le héros (drapeaux `0x3118C`) ; limites du recouvrement des trois axes, au contact et à 1 unité ;
  - (e3) aucun filtre de statut : une cible `FlagToDestroy` est trouvée ; une cible dans la plus haute case est trouvée ; une cible libérée ne l'est pas ;
  - (e4) phase de rebond : sans drapeau de fin, rien ; avec `ForceResetAnimationFlag`, mort ; aucun balayage même avec `HitCounter ≠ 0` ;
  - (e5) registre : (E, 17) pour 3, 4, 5, 6 ; pas pour 7, 11 ; S5 réécrit ; (e6) la règle de vitesse : à 1, 2 et 3 ticks par image, le rebond et la série de X égalent l'oracle ;
    P3 réécrit ;
  - (e7) vie par le crochet : tableau ci-dessus, mur, bord, tir en l'air ; groupements 1, 2, 3 ; la case libre à S+61 ; cible haute (touche S+30, mort S+31) ; cible de 16 ratée,
    de 17 touchée ; invulnérable après la touche ; `0x1AD` ; deux cibles ; la flèche dans une case plus basse que sa cible : le D de la cible lit encore la flèche (attaque 131, puissance 1).
  Puis le code de la phase A. Verts à la fin de T1.
- ⏳ **T2 — Tests de production**, crochet nul, **rouges avant T3** : (p1) le tir du chasseur au sol : une entité de type 3 à P+9 (case la plus basse), puis sa vie ; William normal :
  type 4 ; (p2) table pleine ; (p3) les témoins négatifs utilisent l'ensemble vide de test ; (p4) la fermeture : l'ensemble de production égale les octets de type non nuls des fiches
  d'arme **si** f3c, f4 et f6 sont faites (sinon ce test est ajouté par la dernière des quatre).
- ⏳ **T3 — Phase B** : l'ensemble {3, 4}. Verts.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux). Mutants du code de la DLL, au moins 16, tous tués : filtres retirés (un par un), type 429 changé, héros inclus ou exclu, balayage
  arrêté une case trop tôt, filtre de statut ajouté, recouvrement exclusif, direction +8, gravité oubliée, phase de rebond non reconnue, fin sans drapeau, statut 3 au repli, destruction
  avec effet, clé de vitesse sur l'animation courante, E17 enregistré pour 7, ensemble contenant 5, `Resync` appelé deux fois.

##### Acceptation

- **Oracle** : manifeste identique ; 12 mutants tués ou prouvés équivalents ; les 12 départs du mur.
- **DLL** : T1 et T2 verts ; (p1) à (p3) rouges avant T3 ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite, chacun réécrit par dérivation ; `cmp` ;
  cinq traces ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : retirer {3, 4} rend rouges exactement (p1) à (p3) ; retirer la ligne de la clé de vitesse rend rouge (e6) et (e7) au rebond ; retirer la clé (E, 17) rend
  rouges (e5) et (e7) ; retirer l'appel de `Resync` ne rend rien rouge (mutant équivalent, écrit).
- **Recette de l'auteur** (non bloquante) : `arrow-m16` (F9) : le ballon d'interrupteur (sprite 173, carte 16) est traversé par la flèche ; sur la carte 14 (`arsenal-b-m14`), un ennemi
  de 17 pixels (sprite 136) est touché, un Beannoïde non (Q-F5-1) ; la flèche rebondit au mur par un saut de 16 ticks et disparaît.

##### Arrêts, budget, retour arrière

- **Arrêts** : la phase A fait bouger un test hors de la liste prédite ; (p1) vert avant T3 ; la flèche touche le héros ; une cible détruite dans le tour est manquée ; le décalage de la
  sonde de vitesse excède la prédiction ; plus de 10 tests existants bougent ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase B se retire seule (retirer {3, 4}). La phase A reste inerte, sauf la ligne de la clé de vitesse, qui ne touche que les entités marquées.

---

### 1.22 E20.f6 — Les boules de feu et de glace : C60 et E9 (f6a, f6b) ⏳ (détaillée le 2026-10-15)

**Réponses de l'auteur.** Aucune à ce jour. Pas de question propre (les défauts fidèles s'appliquent : voir les décisions).

Découverte : celle de §1.20. Revérifiés par le critique : C60 et E9 (désassemblage complet), la passe de fin des attributs de tuile, un modèle à part des tirages d'E9, les quatre
boules (11, 12, 14, 15) sur terrain plat, au mur, sur marche, sur l'eau (pentes 1 à 4), contre une cible, 22 mutants d'un mot, la branche A sur l'eau au tick d'une bouffée, les
trois enregistrements de la 309.

**But.** f6a : alimenter les attributs de tuile des entités marquées (la mort dans l'eau). f6b : les boules (C60, E9), les trois flammes de la 309, le branchement des types 11, 12, 14, 15.

**Découpage.**

| Sous-tranche | Contenu | Prérequis |
|---|---|---|
| E20.f6a | Attributs de tuile des entités marquées à gravité (`Slope_18c`) ; la mort dans l'eau du choix du programme ; aucun gestionnaire | E20.e0k1 et e0k2 |
| E20.f6b | C60, E9, trois créateurs, clés, flammes de la 309, ensemble {11, 12, 14, 15} | f6a, E20.f4, E20.f5, E20.f3 (dont f3b), E20.e0b1 ✅, E20.e0k1 à e0k3 |

Ordre : f6a puis f6b, après f4 et f5 (ordre ferme de la révision 9) ; f6a ne dépend techniquement que d'e0k1 et e0k2.

**Faits porteurs.** (Étiquettes comme au §1.20.)
- [binaire, données] **Les types 10 à 15.** Drapeaux `0x132154` : attaque la classe A, `DeactivateOnImpact`, `DeactivateOnAnimEnd`, `NoObstacleSlide`, gravité, `DestroyOnSlidingSlope`
  (`0x100000`), pas de `DeactivateOnHit`. Codes A0, C60, E9. Fiches : 11 (feu, 134 / 4), 12 (feu chargé, 134 / 12, corps 21×15×15), 14 (glace, 135 / 4), 15 (glace chargée, 135 / 12, 21×15×16).
  Les types 10 et 13 n'ont pas de fiche d'arme. Tir : fiches w4 (baguette de feu : types 11 et 12, délai 10, z 32) et w5 (baguette de glace : types 14 et 15).
- [binaire, oracle] **La naissance.** `S = P + 11` (12), charge `R + 11` (107 pour R = 96 ; 112 pour R = 101 de l'arène). Z brut 2 097 154. Pas d'impulsion (l'octet est effacé à chaque
  appel). Feu : 6 px par tick dès S. Glace : 3 px à S (accélération 1) puis 6.
- [binaire] **C60** (`0x8007A8A0`) : si `(+0x1C & 3) == 0`, effet global `0x11` (feu : type − 11 < 2) ou `0x12` (glace), à (X, Y, Z + `0x80000` pour le feu, + 0 pour la glace),
  vitesse (−ForceX, −ForceY, −ForceZ). Aucun tirage.
- [binaire, oracle] **E9** (`0x8007EF50`). Première fois (`+0x88 ≠ 1`) : si `+0x18C ∈ {1, 4}` → `DestroyEntity(e, 6)` et retour ; sinon cinq effets au Z de la boule, chacun avec 2
  tirages (vx dans [−3, 3), vy dans [−2, 2)), puis `+0x88 = 1` et `+0x6C = (+0x6C | 0x40) & ~0x30`. Ensuite (`+0x88 == 1`) : même test de pente, puis si `+0xAC == 1` → `DestroyEntity(e, −1)` et retour, sinon
  toutes les 8 ticks une bouffée à Z + (feu : `0x80000`) avec 2 tirages (vx dans [−1,5 ; 1,5), vy dans [−1 ; 1), vz = 2). Un réservoir plein saute les écritures et les tirages.
- [oracle] **Vie, feu (S = 12, plat).** Atterrissage à S+10 (x = 166, Z 0, `coll_140` 1) ; Z : 31,5 ; 30,5 ; 29 ; 27 ; 24,5 ; 21,5 ; 18 ; 14 ; 9,5 ; 4,5. Traînée à S+4 et S+8. Explosion à S+11
  (statut 3, 10 tirages, son 39, boîte 30×20×31) ; bouffées à S+16, +24, +32, +40 (2 tirages) ; fin d'animation à S+47, mort à S+48, case libre S+49. Glace : atterrissage x = 163,
  son 38, fin S+43, mort S+44, libre S+45. Chargées : 12 → son 41, mort S+48 ; 15 → son 40, bouffées jusqu'à S+64, fin S+67, mort S+68, libre S+69 ; boîte 36×24×31.
- [oracle] **Mur colonne 6.** Drapeau à S+6 (fenêtre S+5 à S+8), explosion à S+7 (Z 18), la boule continue de tomber et se pose à S+10. **Marche de 32 px** : comme le mur. **Marche de 16 px** :
  pose sur la marche à S+7, explosion à S+8, mort S+45 (glace : S+41), libre S+46.
- [oracle, critique] **Eau.** Pente 4 (case `0x08` sous tout le pied) : mort à S+11 par le choix du programme (`DestroyEntity(e, 6)`), libre à S+12, aucun tirage. Pente 1 (`0x02`) : même issue, mais
  par E9 (branche B). Pentes 2 et 3 : explosion normale. Pied moitié sur l'eau : explosion. Boule qui explose contre un mur puis tombe dans l'eau : la branche A détruit à S+11 ;
  **à un tick multiple de 8, la destruction est suivie de la bouffée du même tick** (ordre littéral).
- [oracle] **Touches.** Le vrai Beannoïde en x = 156 : la boule le touche en vol à S+8 (feu) ou S+9 (glace), `attach(4)` ou `attach(5)` avant les étincelles, 9 tirages, la boule continue ;
  la boîte d'explosion le touche à nouveau 25 ticks plus tard (S+33 ou S+34).
- [binaire, critique] **Attributs de tuile** (`0x80038064`). Pour une entité à gravité : sur les quatre coins, un coin « posé » est tel que `hauteur + 1 == +0x1E0` ; `t1` démarre à `0xE00`, un coin
  non posé le met à 0 pour de bon, un coin posé le baisse au minimum de `cellule & 0xE00` ; `+0x18C = t1 >> 9`. Sans gravité, `+0x180 = +0x184 = +0x18C = 0`. Pente 1 : 8 146 cases sur
  49+ cartes ; pente 4 : 10 644 cases sur 54+. Le héros n'a pas de cas pour la pente 1 (`case 1: break` de la décompilation).
- [DLL] `UpdateGroundSlope` (même règle, égalité avec `PosZ + ModZ`) ne tourne que par `RunHeroProbes`. L'égalité vaut pour un Z logique au repos (binaire − 1 unité). 14 sprites locaux (444 instances
  de carte) et 12 globaux portent `DestroyOnSlidingSlope` : alimenter toutes les entités à gravité changerait ces 444 instances (E19.h2 / Q-K1).
- [données, oracle] **La 309.** Trois enregistrements (6, 7, 8) du sprite global 12 (`SpriteDirection` 65, hauteur 2, pixels (948, 672), (960, 664), (936, 664), Z 32). Leur programme A (`1A 02`,
  `63 80 F0 00`) pose l'animation 2 et efface `0x00F0` : plus aucun bit de désactivation, donc E9 ne tourne jamais. Enregistré, C60 émet l'effet 17 toutes les 4 ticks à (x, y, Z 40), vitesse nulle,
  sans fin, sans tirage ; la boîte 36×24×31 (134 / 12) existe déjà. Sans le programme A (témoin) les boules s'envoleraient et exploseraient. Le programme A est un bouchon de l'oracle, pas le vrai
  interpréteur.
- [DLL] **Registre et épingles.** Voir §1.20. `CreateForProduction` appelle les classes d'IA une à une ; enregistrer C60 pour 12 rend les flammes de la 309 vivantes en production.
- [critique] **Mutants.** C60-1 à C60-4, E9-1 à E9-10 : tous tués sauf E9-5b (équivalent : le choix du programme prend la pente 4 avant) et E9-9 (`0x40` déjà posé).

**Écarts tranchés par la critique.**
1. *« f6 en f6a et f6b »* (relevé de la DLL) est **CORRIGÉ** : f6b a trois phases, parce que l'enregistrement de C60 est visible (les flammes de la 309) avant même le câblage.
2. *« Les trois flammes de la 309 explosent »* (enveloppe) est **CORRIGÉ** : elles émettent des étincelles ; E9 ne tourne jamais pour elles.
3. *« E9 branche A sur l'eau : non exécuté »* est **CLOS** (ci-dessus).
4. *« La pente 1 est de l'eau »* reste une **INFÉRENCE** sur son sens ; le fait est que la boule meurt (ORACLE).
5. *« Le décalage de vitesse »* : D-F5-5 s'applique ; sans elle une boule qui explose avance de 6 à 18 px de trop.

**Décisions de conception.**
- **D-F6-1** — f6a puis f6b.
- **D-F6-2** — Alimentation (f6a) : pour toute entité marquée dotée de la gravité, `UpdateGroundSlope()` après le pas de chaque tick (le même code que le héros). Aucune autre entité : Q-K1 option A.
  Pas de `UpdateVramFlags` (aucun projectile de f4 à f6 n'a `0x200000`).
- **D-F6-3** — E9 en classe `AlundraBallAi` : branches A et B littérales, y compris l'ordre « destruction puis bouffée du même tick » ; le test de pente lit `Slope_18c ∈ {1, 4}` ; les cinq effets
  et les bouffées passent par `Emit`, les tirages sont faits à l'application (D-E0B-3(a)), dans l'ordre des deux tirages par effet ; vitesses capturées ; un effet non créé ne tire pas.
- **D-F6-4** — C60 : effet 17 pour les types 11 et 12, 18 sinon ; décalage de Z de `0x80000` pour le feu seulement ; vitesse (−ForceX, −ForceY, −ForceZ) ; Z du binaire = `PosZ + 1`.
- **D-F6-5** — Clés (C, 60) et (E, 9) pour {10, 11, 12, 13, 14, 15} ; pas de clé A0.
- **D-F6-6** — L'enregistrement des clés est une phase à part (B) : visible sur la 309, avec son test et sa ligne de recette. L'ensemble câblé {11, 12, 14, 15} est la phase C.
- **D-F6-7** — Les effets d'attache 4 et 5 viennent de f3b (prérequis) ; aucun tirage de leur part. Les 9 tirages de la touche sont ceux des trois étincelles.
- **D-F6-8** — Vitesse et accélération : D-F5-5. La position de l'explosion à l'atterrissage égale celle de l'oracle (x = 166 en feu, 163 en glace).
- **D-F6-9** — La force verticale d'une flamme posée par enregistrement, au moment de C60, est lue telle que la DLL la tient (sonde P-F6-1) ; un écart avec 0 est documenté, pas corrigé (Q-K1 A).
- **D-F6-10** — Le tick du drapeau de mur : D-F4-9 (fenêtre S+5 à S+8 sur les 12 départs).

**Questions à l'auteur.** Aucune.

**Prérequis et dépendants.** Voir le tableau. Dépendants : E14 (ennemis de feu et de glace, la Torche E20 qui copie le type 12 sans gestionnaires), E20.e (effets du héros, herbe), E20.h3 et E20.h4 (f6a : graines, bombe et types 16, 17, 23 des magies portent `DestroyOnSlidingSlope` avec la gravité) et toute tranche future qui fait naître par l'interface un type portant `0x100000` avec la gravité (1, 2, 10 à 17, 23, 234).

**Ordre ferme** (révision 9 de l'enveloppe, après sa relecture) : f4, puis f5, puis f6a, puis f6b. Les éléments communs « portés
par la première exécutée » le sont donc par f4 (l'interrupteur d'arène D-F4-7 et son fichier, la réécriture des témoins négatifs D-F4-8,
l'annexe et les outils communs) et par f5 (la règle de la clé de vitesse D-F5-5) ; les mentions conditionnelles (« si f4 ne l'a pas
posé ») sont sans objet.


---

#### 1.22.1 E20.f6a — Attributs de tuile des entités marquées ⏳

**Ajout de la révision 9 (O-E0K-2).** Le T0 recense aussi les lecteurs de `+0x180`, `+0x184`, `+0x190` et les types qui portent
`DestroyOnVramFlags` (`0x200000`), et nomme leur propriétaire dans le plan ; f6a ne nourrit que `+0x18C` (D-F6-2).

**Prérequis.** E20.e0k1 et e0k2 (le Z logique et le marqueur). **À relire à l'exécution** : le point d'appel dans `TickMotion`, le nom du marqueur.

**But.** Une entité marquée à gravité a `Slope_18c` calculé comme le binaire ; le choix du programme détruit une boule posée sur de l'eau profonde, sans gestionnaire.

**Périmètre.** `AlundraEntityScriptProxy.cs` (un appel pour les entités marquées à gravité), `Alundra.Tests/`, annexe, ADR. **Hors périmètre.** E9 et C60 ; les entités par enregistrement ; `UpdateVramFlags`.

**Partage des phases** : une seule phase (T1 à T2). La ligne d'appel est le seul changement de comportement ; elle est inerte en production (aucune entité marquée à gravité et à `DestroyOnSlidingSlope` n'existe).

##### Tâches

- ⏳ **T0 — Oracle** : dans l'annexe commune, scénarios `fire11_gp8_deepwater`, `fire11_gp2_slope1`, `fire11_gp4_slope2`, `fire11_gp6_slope3`, `fire11_mixed_footprint_water`, `ice14_gp8_deepwater`,
  avec les clés `slope_18C`, `status`, `effects` ; mutants du test des quatre coins ; sonde de la DLL : `Slope_18c` d'une boule marquée posée sur chaque pente.
- ⏳ **T1 — Tests puis code.** (s1) boule marquée au repos sur une case `0x08` sous tout le pied : `Slope_18c` 4, accolée à la même boule sur `0x02` (1), `0x04` (2), `0x06` (3), pied moitié sur l'eau (0), boule
  en l'air (0), sans gravité (0) ; (s2) la même boule née par enregistrement garde 0 ; (s3) par le crochet, la boule née sur l'eau profonde est détruite à S+11 avec l'effet 6, libre à S+12, aucun tirage, accolée
  à la même boule sur la terre ferme qui vit ; groupements 1, 2 et 3.
- ⏳ **T2 — ADR, plan, verifiers frais.** Mutants : appel pour tous les gravitaires, appel sans marqueur, minimum remplacé par maximum, égalité de Z décalée d'une unité, coin ignoré.

##### Acceptation

- **DLL** : (s1) à (s3) verts, rouges avant le code ; suite complète ; tests déplacés ⊂ liste prédite (aucun attendu). **Preuve de retrait** : retirer l'appel rend rouges exactement (s1) et (s3).
- **Arrêts** : une entité par enregistrement change de pente ; un test existant bouge ; un besoin de changer le moteur. **Budget** : un `executor`, deux tentatives. **Retour arrière** : retirer l'appel.

---

#### 1.22.2 E20.f6b — C60, E9, les flammes de la 309 et le branchement ⏳

**Prérequis.** f6a, **E20.f4 et E20.f5** (ordre ferme de la révision 9 : D-F4-7, D-F4-8, l'annexe, la règle de vitesse D-F5-5), E20.f3 (dont f3b), E20.e0b1 ✅, E20.e0k1 à e0k3. **À relire à l'exécution** : le point de création des effets, l'interrupteur d'arène (D-F4-7), la règle de vitesse (D-F5-5).

**But.** Les boules tirent, tombent, explosent ou s'éteignent ; les trois flammes de la 309 émettent leurs étincelles.

**Périmètre.** Nouveau fichier `Alundra/Scripts/AlundraBallAi.cs` ; `AlundraNativeSpriteRegistry.cs` (un appel, phase B) ; `AlundraPlayerManager.cs` (l'ensemble, phase C) ; `Alundra.Tests/` ; annexe ; ADR.

**Hors périmètre.** La Torche E20, les ennemis de feu et de glace (E14) ; le sens de la pente 1 ; le réservoir plein hors des tests directs.

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, aucun changement de comportement) : E9, C60, les créateurs, la méthode `Register(registry)`. Les tests enregistrent les clés eux-mêmes dans le registre du runner de test ; les boules naissent par le crochet.
- **Phase B** (T3) : `CreateForProduction` appelle `Register`. Seul changement de comportement : les trois flammes de la 309 émettent. L'épingle du registre bouge (dérivation).
- **Phase C** (T4) : l'ensemble {11, 12, 14, 15}. Seul changement de comportement : les tirs.

**Valeurs attendues** (`fire11_flat.json` ; S = 12 ; Z DLL = Z oracle − 1 unité brute).

| Valeur | Clé | Attendu |
|---|---|---|
| Naissance | première ligne | S = P + 11 |
| Statut | `status` | 2 de S à S+10 ; 3 de S+11 à S+47 ; 4 à S+48 ; absent à S+49 |
| X | `px` | 106 à S, +6, 166 à S+10 et ensuite |
| Z | `pz` | 31,5 ; 30,5 ; 29 ; 27 ; 24,5 ; 21,5 ; 18 ; 14 ; 9,5 ; 4,5 ; 0 (S à S+10) |
| Forces | `fx`, `fz` | 393216 puis 0 dès S+11 ; −32768 × (k+1) pour k = 0 à 9, puis 0 |
| Sol | `coll_140`, `ground_144` | 1 à S+10 |
| Boîte | `bw,bd,bh,hbz` | 30×20×15 en vol ; 30×20×31 à hbz 0 dès S+11 |
| Attaque, drapeaux | `atk_attr`, `atk_pow`, `flags6C` | 134 ; 4 ; `0x132154` puis `0x132144` |
| Traînée | `effects` | id 17, (124, 100, 35) à S+4 et (148, 100, 22) à S+8, v (−6, 0, 2) puis (−6, 0, 4) |
| Explosion | `draws`, `effects`, `snd` | S+11 : 10 tirages, cinq effets 17 en (166, 100, 0), vitesses de la clé ; son 39 |
| Bouffées | idem | S+16, +24, +32, +40 : 2 tirages, effet 17 à Z 8, vz 2 |
| Fin | `reset_AC` | 1 à S+47 ; mort à S+48, sans bouffée |

Glace (`ice14_flat.json`) : x = 103 à S, 109 à S+1, 163 à S+10 ; effet 18 de Z + 0 : traînée (121, 100, 27) à S+4, (145, 100, 14) à S+8 ; son 38 ; fin S+43, mort S+44, libre S+45.
Chargées (`fire12_flat`, `ice15_flat`) : S = R + 11 ; boîte d'explosion 36×24×31 à hbx = x − 18 ; puissance 12 ; son 41 / 40 ; fin S+47 / S+67 ; mort S+48 / S+68.
Autres clés : `*_wall_col6`, `*_step16_col6`, `*_step32_col6`, `fire11_gp{8,2,4,6}_*`, `fire11_wall_then_water`, `fire11_vs_target`, `ice14_vs_target`, `flames309`.

##### Tâches

- ⏳ **T0 — Prédiction et oracle** (commit de docs). Annexe commune. Scénarios de f6 : les trente des relevés plus `fire11_branchA_water_on_puff_tick` (injection de `+0x18C` à S+15), les flammes de la 309 avec le vrai
  interpréteur d'octets si possible. Au moins 14 mutants : C60-1 à C60-4, E9-1 à E9-10 (E9-5b et E9-9 prouvés équivalents), `+1` du Z de création. Sondes de la DLL : (P-F6-1) force verticale d'une flamme posée
  au moment de C60 ; (P-F6-2) la 309 se construit-elle dans `ArcRun` (arrivée en tuile (38, 47), depuis les cartes 306 et 441) ; (P-F6-3) position de l'explosion à 1, 2, 3 ticks par image avant la règle de vitesse.
- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette). Chaque « sans effet » est accolé à un positif :
  - (b1) E9 direct, branche B : 10 tirages dans l'ordre, les cinq effets au Z de la boule ; pente 1 ou 4 : `DestroyEntity(e, 6)`, aucun tirage, accolée à la pente 2 ; réservoir plein : aucun tirage ni écriture ;
  - (b2) E9 branche A : bouffée tous les 8 ticks (2 tirages, décalage de Z du feu seulement), pente 1 ou 4 à un tick multiple de 8 : destruction puis bouffée ; `+0xAC == 1` : destruction sans bouffée ;
  - (b3) C60 direct : traînée tous les 4 ticks, feu 17 et glace 18, vitesse −F, Z + `0x80000` pour le feu seulement ; (b4) les tirages égalent un pas local (D-E0B-8) ;
  - (b5) vie par le crochet, les quatre types, plat, mur, marches de 16 et 32, eau (pentes 1 à 4), à 1, 2 et 3 ticks par image, tableau ci-dessus ; la fenêtre du mur ; l'explosion à x = 166 / 163 ;
  - (b6) touches : feu `attach(4)`, glace `attach(5)`, avant les étincelles, 9 tirages, la boule ne s'arrête pas ; seconde touche à S+33 / S+34 ;
  - (b7) la table du registre : `Register` ajoute les clés (C, 60) et (E, 9) pour 10 à 15.
  Puis le code de la phase A. Verts à la fin de T1.
- ⏳ **T2 — Tests des flammes de la 309, rouges avant T3.** (m1) trois entités du type 12, `Flags` 0x132104, qui émettent l'effet 17 à (x, y, Z du binaire + 8 px) tous les 4 ticks de `FrameCounter`, vitesse lue
  (P-F6-1), sans tirage ; accolées au témoin « sans C60 » (aucune émission) ; (m2) elles ne touchent aucune entité ; (m3) l'épingle du registre par dérivation (+2 clés).
- ⏳ **T3 — Phase B** : l'appel d'enregistrement. Verts.
- ⏳ **T4 — Phase C et tests de production**, crochet nul, **rouges avant l'ensemble** : (p1) la baguette de feu et celle de glace, non chargées et chargées (R = 101 de l'arène) : une entité du bon type à la plus basse case libre,
  au tick R+11 ; (p2) le commutateur de feu de la carte 303 (`fire-m303`) : la boule touche, le programme D s'exécute, le drapeau est posé et la classe A effacée (opcode `0x87 [128, 6]` répond oui) ;
  (p3) table pleine ; (p4) la fermeture : l'ensemble de production égale les octets de type non nuls des fiches d'arme. Puis la ligne de l'ensemble.
- ⏳ **T5 — ADR, plan, verifiers frais** (deux). Mutants du code de la DLL, au moins 16, tous tués : 5 effets → 4 ; période 8 ; test de pente 1 oublié ; pente 4 oubliée dans la branche A ; destruction avec un autre effet ; bouffée
  avant la destruction ; décalage de Z du feu appliqué à la glace ; vitesse non opposée ; effet 17 pour la glace ; tirage hors de l'application ; clé C60 oubliée ; clé E9 oubliée ; type 10 ou 13 câblé ; ensemble avec 3 ; boule
  qui avance à la vitesse de l'animation courante (D-F5-5) ; alimentation de la pente à toutes les entités.

##### Acceptation

- **Oracle** : manifeste identique ; 14 mutants tués ou prouvés équivalents.
- **DLL** : verts ; (m1) à (m3) rouges avant T3 ; (p1) à (p4) rouges avant T4 ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite (registre, S5 et témoins si f5 n'est pas faite) ; `cmp` ;
  cinq traces ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : retirer l'appel d'enregistrement rend rouges exactement (m1) à (m3) et (b7) de production ; retirer l'ensemble rend rouges exactement (p1) à (p4) ; retirer l'appel de f6a rend rouge (s3).
- **Recette de l'auteur** (non bloquante) : `arsenal-a-m14` : le feu (emplacement 5) et la glace (emplacement 4) partent en arc, explosent à l'atterrissage avec cinq étincelles ; sur l'étang de la carte 14 (tuile (40, 48)), la
  boule s'éteint avec l'effet de l'eau ; `fire-m303` : un commutateur de feu s'allume ; carte 309 (par un `Change map` depuis les cartes 306 ou 441) : les trois flammes émettent des étincelles.

##### Arrêts, budget, retour arrière

- **Arrêts** : 10 et 2 tirages non respectés ; une flamme de la 309 fait autre chose qu'émettre ; l'explosion à plus d'un pixel de l'oracle ; la 309 ne se construit pas (sonde : un montage sans carte) ; un test hors de la liste prédite
  bouge ; plus de 10 tests existants bougent ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase C se retire seule (vider {11, 12, 14, 15}) ; la phase B se retire seule (retirer l'appel) ; la phase A reste inerte.

---

**Points ouverts ajoutés.**

| # | Question | Tranchée par |
|---|---|---|
| O-F4-1 | Le flux dérive après chaque passage de la flèche chargée sur l'herbe (le bouchon ne tire pas les mots de la coupe : deux à trois mots par colonne) | E20.e (étend O-F3-1) |
| O-F5-1 | Quels ennemis posent `+0x1CC` avant E17 (la flèche les traverse) | E14 |
| O-F6-1 | Sens de la pente 1 (la boule s'éteint ; le héros n'a pas de cas) | à placer ; aucun effet de comportement connu |
| O-F6-2 | Le réservoir de 128 effets plein : branche décodée, seulement testée en direct | écart accepté |
| O-F6-3 | La Torche E20 (cartes 86 à 94, 441, 442) copie le type 12 et efface ses six codes natifs | E14 |
| O-F6-4 | Les flammes posées par enregistrement gardent l'ancienne règle de chute (Q-K1 A) : la force verticale au moment de C60 peut différer de 0 | E19.h2 |

**Corrections induites** (faites par la session principale à l'insertion, 2026-10-15, sauf (2) : les tests d'f3a déjà commités (S5, premier
bloc de S1) et (p5) de f3c sont réécrits par f4 et f5, qui les nomment dans leurs listes prédites). (1) §1.19 D-F3-7 : retirer « les tranches f4 à f6 devront élargir l'enregistrement A0 » ; fermer O-F3-3. (2) §1.19.1 T1 (s5), premier bloc de (s1)
et §1.19.3 (p5) : témoin négatif durable (registre vide, ensemble de test vide). (3) §1.18 : N9 (« E17 doit appeler `ResyncControllerFromFlags` ») devient une assurance ; l'épingle P3 d'e0k1 est réécrite par la règle de D-F5-5.
(4) §1.1 ligne 8 : « les trois enregistrements `0x0C` de la 309 prendront vie » → ils émettent des étincelles (E9 ne tourne jamais pour eux). (5) §1.1 : f6 a deux sous-tranches (f6a, f6b) ; les trois tranches
partagent l'annexe `e20-f4f6` et les décisions D-F4-7, D-F4-8 et D-F5-5, portées par la première exécutée.

### 1.24 E20.g1 et E20.g2 — Soulever, porter, lancer et briser : g1a (porter), g2a (lancer), g2b (briser), g1b (soulever), g2c (autres familles) ⏳ (détaillée le 2026-10-15)

**Réponses de l'auteur.** Aucune à ce jour (2026-10-15, mode AUTO, l'auteur absent). Trois questions sont posées plus bas (Q-G1 à Q-G3). Chacune a un défaut réversible, **noté mais à confirmer par l'auteur** :
ces tranches ne s'exécutent qu'après f6b, h0 et h1 et A362 (ordre de l'enveloppe), et l'auteur sera revenu. Aucune ne bloque g1a, g2a ni g2b. Demande de l'auteur du 2026-10-15 : « le joueur doit pouvoir transporter un objet et monter sur une plateforme ».

Découverte en lecture seule du 2026-10-15, au HEAD `9ecc7d5` : trois relevés (binaire, impact sur la DLL, inventaire des tests), puis un critique adversarial. Le critique a redésassemblé les sites qui portent les décisions : `TryLift` `0x8002EDBC`, `PlayerTryAction` `0x8002EEAC`, `UpdatePlayerCarriedEntity` `0x8002F884` avec `StepTowards` `0x8002F854`, le groupe des états de portage `0x8003232C`, les états `0x05` et `0x0B`, la branche verrouillée `0x80031BC4`, la garde de l'échelle `0x80031D54`, `MoveEntity` `0x80037E34`, `DestroyEntity` `0x8003A59C` et `0x8003A774`, C23 `0x8006B848`, E0 et E2 `0x8007ED10` et `0x8007ED30`, la tête de C4 `0x80066984`, la fonction d'arrivée dans la même carte `0x80031340`. Il a fait un balayage de toute l'image : les écritures de `+0x28` et de `+0x2C`, les lectures de `+0x2C` et de `+0x28`, les appelants des fonctions ci-dessus. Il a rejoué les 19 scripts de l'oracle du relevé binaire dans son propre dossier (55 sorties identiques au manifeste des relevés), puis écrit trois scénarios nouveaux : l'égalité et les seuils de `UpdatePlayerCarriedEntity` (`c1_equality`), l'arrivée à pied et en sautant sur une plateforme qui bouge, caisse en main (`c2_platform`), la persistance du contact (`c3_contact`). Il a relu le code de la DLL (stubs, sonde, contact, riding, `DestroyEntity`, ordre du tick, horloge d'animation, registre, exports du héros et de la caisse) et recompté les en-têtes soulevables (17), la liste de la chaîne (30 cartes, 18 avec des soulevables) et les gardes des arcs. Scripts et sorties dans le scratchpad de la session : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g/` (`bin/`, `dll/`, `tests/`, `critic/`). Aucune modification du moteur, du convertisseur ni des données. La DLL n'a pas été exécutée : ce qui la concerne est lu dans le code ou inféré.

**But.** (1) Le héros soulève, porte, saute avec, lance et brise caisses et jarres comme l'original ; l'objet porté suit le héros sur une marche de 16 px, sur le dessus d'un autre objet et sur une plateforme qui bouge à l'horizontale. (2) Une couture unique, `PutInHands`, que les graines et la bombe (E20.h3) réutiliseront. (3) Rien d'autre : les caisses et les jarres restent traversables par le héros jusqu'à E20.g3 (D-E19-28, D-E19-46).

**Réponse précise à la question de l'auteur** (« porter un objet et monter sur une plateforme »). Ce que fait l'original (binaire, oracle), ce que fait le port aujourd'hui, ce qu'il faut :

| Situation | Original | Port aujourd'hui | Ce qu'il faut |
|---|---|---|---|
| Soulever | On marche contre l'objet et on appuie sur Carré ; 36 ticks de geste, puis la marche reprend à la même vitesse (2,4375 px par tick) | Impossible : `PlayerTryAction` est un bouchon et la sonde de mouvement ignore les soulevables | g1a, g1b |
| Sauter avec l'objet | Même impulsion, même arc (sommet 27,5 px, 20 ticks) ; l'objet reste exactement à +32 px | Impossible | g1a (états et fiches d'animation, aucun code de saut) |
| Marcher contre une marche de 16, 32 ou 48 px | Bloqué à X = 133,0, avec ou sans objet | Idem sans objet | rien |
| Monter sur 16 px, sur le dessus d'une caisse | Oui, en sautant, avec l'objet (oracle) | Oui sans objet | g1a |
| Monter sur 32 px et plus | Non (le sommet du saut est à 27,5 px) | Non | rien |
| Plateforme qui bouge à l'horizontale | Le héros est emporté (appui `+0x12C`) ; l'objet suit le héros ; marche puis saut sur la plateforme : identique avec et sans objet (oracle `c2`) | Le héros est emporté (E19.d2c2) | g1a : l'objet suit le héros |
| Plateforme qui monte ou descend | L'objet suit le héros (copie de position) | Le héros tombe à travers ou clignote (D5b, E19, en pause) | Hors tranche (Q-G2) |
| Échelle | Refusée en portant (`0x80031D54`) | Déjà refusée (la garde existe) | rien |
| Poser | Il n'y a pas de « poser » : Carré lance toujours (court, environ 34 px, si l'on est immobile) ; caisses et jarres se brisent en arrivant ; pierres et tonneaux (C4) restent | — | g2a, g2b, g2c |

Le nom `PlatformEntity` de la DLL (et de la décompilation) est trompeur : dans le binaire, `+0x28` n'est écrit non nul que par trois sites, qui tous y rangent le héros (le soulèvement, le départ des graines, le départ de la bombe). Le vrai appui d'une plateforme est `+0x12C`. Il n'y a donc pas de « branche de suivi de plateforme » à ajouter ; il y a la copie de position d'un objet porté.

**Découpage.**

| Sous-tranche | Contenu | Prérequis |
|---|---|---|
| E20.g1a | Porter : `UpdatePlayerCarriedEntity` (trois modes), couture `PutInHands`, états du héros `0x05`, `0x0B`, `0x0C`, `0x07`, `0x06`, `0x2E`, `0x2F`, `0x30`, suivi de l'objet porté, effacement du porteur dans les deux `DestroyEntity`, C23 en entier, pose du marqueur du Z logique. Rien de visible sans la couture | E20.e0k2 (le marqueur, la loi du vol, à relire à l'exécution), E20.e0a1 ✅, E14.a1b ✅ |
| E20.g2a | Lancer : front de Carré dans le groupe de portage, table des directions, `+0x24`, états `0x09` et `0x0A`, vol de l'objet relâché (règle de vitesse d'f5) | g1a, E20.f5 (D-F5-5), E20.e0k2 |
| E20.g2b | Briser : E2 (destruction à l'arrivée, à la touche, au mur), clés E2 des dix en-têtes, pas de butin (bouchon nommé) | g2a, E14.a1b ✅, E20.f6a (mort dans l'eau) ; **E20.e0b2 : arête de l'enveloppe, jugée souple par le critique (voir D-G-14)** |
| E20.g1b | Soulever : `TryLift`, `PlayerTryAction`, sonde de contact des soulevables (Q-G1), ensemble câblé des types, appels de l'état d'escalade ; **l'activation, en dernier** | g1a, g2a, g2b (ordre ; voir ci-dessous), E20.e0k2 |
| E20.g2c | Autres familles : C4 (pierres, tonneaux, blocs ; 191 placements), C70 et E20 (torches, 57) | g2b, un T0 de lecture seule (C4 n'est décodé que dans sa tête) |

**Ordre interne** (affine la ligne 10 de l'enveloppe, qui reste READY) : **g1a, g2a, g2b, puis g1b**, puis g2c. L'activation vient en dernier pour qu'aucun état de production n'ait « soulever sans pouvoir lancer ». Si l'enveloppe maintient l'arête g2 → E20.e0b2 (en pause), l'ordre devient g1a, g1b (soulever et porter, sans lancer ; la recette le dit), puis g2a et g2b quand e0b2 est faite.

**Faits porteurs.** Étiquettes : [binaire] décodé sur `ALUN_CD.EXE` (France) ; [oracle] exécuté dans la machine MIPS ; [DLL] lu dans le code au HEAD `9ecc7d5` (numéros de ligne à relire à l'exécution : e0k2, f3 à f6, h0 et h1 touchent les mêmes fichiers) ; [données] export et cartes extraites ; [inférence] déduit, non exécuté ; [critique] établi ou corrigé par la critique. Les clés de l'oracle sont celles de `gl.py` (`off30`, `off34`, `off38`, `carried2C`, `carrier28`, `anim_t`, `px`, `pz`, `support`, `xcoll`, `t24`, `flags6C`, `status`, `hitc`, `touch`).
- [binaire, critique] **Les écritures de `+0x28`.** Trois sites dans toute l'image, qui rangent tous le héros `0x80127D30` : `TryLift` `0x8002EE1C`, le départ des graines `0x8003429C`, le départ de la bombe `0x800345FC`. Les effacements : C23 `0x8006B8AC`, C4 `0x80066A1C`, C62 `0x8007AABC` et `0x8007AAF8`, C70 `0x8007B814`, C73 `0x8007BB68`, et les trois queues de `DestroyEntity` `0x8003A62C`, `0x8003A74C`, `0x8003A7A4` (`porteur.+0x2C = 0`, sans comparer l'identité). `+0x28` porte l'objet, `+0x2C` le héros, `+0x30/+0x34/+0x38` le décalage, rangé **chez le porteur** (un porteur ne porte qu'une chose).
- [binaire, oracle, critique] **Le héros ne change pas de physique en portant.** Aucune instruction de la passe physique (`0x80036000`-`0x80038400`) ne lit le `+0x2C` du héros. Vitesse de marche : fiche 7 = fiche 1 (208). Saut : fiches 6 et 46 (impulsion 1 280, son 10) = fiches 2 et 43. Oracle : mêmes positions et mêmes Z, avec et sans caisse. La seule lecture de portage dans le mouvement est celle de l'objet : `MoveEntity` pose l'objet à `porteur + décalage` sans Z ni XY propres, avec forces nulles (`0x80036A84`), hors de la liste B des collidables (`+0x28` non nul).
- [binaire] **`PlayerTryAction`** (`0x8002EEAC`, appelée en `0x80031E50` pour Idle et Moving, `0x80031E98` pour les sauts, `0x80031F64` pour `0x29`, `0x80031FF4` pour `0x03`, `0x80032128` pour `0x2A`, `0x800321E4` pour l'escalade). Porté : `+0x88 = (sol ? 0x0C : 0x30)`, retour 1. Sinon, si le contact `+0x130` est non nul et que le front de Carré (`0x80126F2A & 0x80`) est levé : retour direct de `TryLift(contact)`, même si 0. Sinon boucle sur les cases 0 à `[0x80134360]` : statut 2 ou 3, `+0x20` nul, `+0x12C` égal au héros, **sans aucun bouton** ; le premier `TryLift` non nul gagne.
- [binaire, oracle] **`TryLift`** : genre = `(+0x6C & 0x600) >> 9` ; 0 refuse ; 1 soulève ; 2 et 3 soulèvent **seulement si le héros possède l'objet `0x3B`** (la doc de `EntityPickupKind` et la décompilation lisent l'inverse). Aucun test de distance, de regard, de statut, de taille ni de Z. Le héros passe à `0x0C` (sol) ou `0x30` (air) si l'objet était posé sur sa tête (`+0x12C` = héros), sinon à `0x05` (sol) ou `0x0B` (air). Le décalage est `position de l'objet − position du héros`, sur les trois axes.
- [oracle, critique] **Le contact `+0x130`** est l'entité qui a bloqué le pas du **tick précédent**. Avec la caisse en x = 130 et le héros qui pousse vers l'est : Carré aux ticks 4 et 5 donne l'épée (animation 16), aux ticks 6 et 7 le soulèvement (`c3`). La force du héros ne retombe pas d'un coup : pousser aux ticks 1 à 9, lâcher au tick 10, Carré au tick 10 ou 11 soulève, Carré au tick 12 donne l'épée. Appuyer sur Carré au premier tick d'une nouvelle poussée donne l'épée, au suivant le soulèvement. Carré tenu depuis le tick 1 : jamais de front, jamais de soulèvement. Haut (sans pousser la caisse) puis Carré : l'épée.
- [oracle] **Le soulèvement (`g1_lift`, L = 11).** Le héros s'arrête à 109,0 (le bord de la caisse en x = 130) dès le tick 5. À L : `carried2C` = caisse, `carrier28` = héros, `anim_t` 5, (`off30`, `off34`, `off38`) = (21, 0, 0). De L+1 à L+11, `off38` monte de 1 par tick et `off30` reste à 21. De L+12 à L+32, `off30` baisse de 1 par tick et `off38` monte de 1 : (0, 0, 32) à L+32, stable ensuite. `anim_t` vaut 5 de L à L+35, 12 à L+36, puis 7 à la reprise de la marche (X = 111,4375). Les décalages après le soulèvement, objet moins héros : ouest vers est (21, 0), est vers ouest (−21, 0), sud vers nord (0, −15), nord vers sud (0, +15). Objet posé sur la tête du héros (Z = 32) : soulevé au premier `MovePlayer`, sans bouton, `anim_t` 12.
- [binaire, oracle, critique] **`UpdatePlayerCarriedEntity`** (`0x8002F884`) ; l'objet est `héros.+0x2C`, le compteur est `[0x80098F28]`, le pas est `StepTowards` `0x8002F854` (`|diff| < pas` donne la cible). Si la fiche d'équilibre de l'objet a `AttackCount` 0 (octet `0xE`), le mode 1 devient le mode 0. **Mode 0** (à chaque tick libre, `0x80031D78`, avant l'aiguillage d'état) : décalages lus sur les **positions** (objet − héros), `m = max(|dx|, |dy|)`, `e = |dz − 0x200000|` ; si `m < e` seul Z avance de `0x10000` vers `0x200000`, sinon X et Y avancent vers 0 et Z vers `0x200000`, chacun de `0x10000` ; écrit `héros.+0x30/34/38`, compteur à 0. **Égalité (`m == e`) : les trois axes avancent** (oracle `c1_equality` : (21, 0, 11) donne (20, 0, 12) ; 21 − 1 unité brute donne (même X, 12)). **Mode 1** (branche verrouillée `0x80031BF4`, case d'escalier ou de sortie `0x80032778`) : compteur < 5 → compteur + 1, rien d'écrit ; sinon, si les trois |décalages| valent au plus `0x1FFFF` → destruction à un argument `0x8003A774` (statut 4, ni butin ni effet, porteur effacé), compteur gardé ; sinon chaque axe avance de `0x20000` vers 0. Mort de l'objet : 5 + 16 + 1 = 21 ticks après le début du verrou (oracle `g8` H3 : verrou au tick 60, statut 4 au tick 81). **Mode 2** (blessure `0x80031C64`, pente 4 `0x8003261C`, mort `0x800327AC`) : `objet.+0x24 = −1`, `objet.+0x8C = héros.+0x8C`, position = héros + (0, 0, `0x200000`), compteur à 0, `héros.+0x2C` **non** effacé. Le compteur est remis à 0 aussi quand rien n'est porté.
- [binaire, critique] **Le groupe de portage** (`0x8003232C`, états `0x06`, `0x07`, `0x0C`, `0x2E`, `0x2F`, `0x30`). Dans l'ordre : `héros.+0x8C =` direction du pad ; `TryUseItem` (0 termine le tick) ; objet nul → sol : animation 0, air : `0x2D` ; front de Carré : si `héros.+0x180 & 0x2000` rien, sinon **lancer** (g2a) ; sinon objet lourd (genre 3) → `0x0C` ou `0x30` seulement ; sinon au sol, front de Croix (refusé si `+0x180 & 0x4000`) → `0x06` (direction tenue) ou `0x2E` (aucune), sinon `0x07` (direction) ou `0x0C` ; en l'air `0x2F` ou `0x30`. L'état `0x05` (`0x800322F0`) : objet nul → `0x2D` ; au sol il attend la chaîne 5 → 12 ; en l'air → `0x30`. L'état `0x0B` (`0x800322B4`) : objet nul → animation 0 ; au sol → `0x0C`. Aucun de ces états n'appelle l'interaction ni l'attaque. `0x06` est le saut en marche avec objet, `0x2E` le saut sur place (la lecture « sol et air » de `h-g-deps.md` est fausse).
- [données] **Les fiches du héros** exportées (`sprite-records.json`, héros `192c2eeb`) portent déjà les animations 5, 6, 7, 9, 10, 11, 12, 46, 47, 48 avec leurs chaînes (5 → 12, 6 → 47, 10 → 45, 11 → 48, 46 → 48, 9 → 0) et leurs impulsions : les états sont pilotés par les données, comme le reste du saut du héros (`StepAnimationClock` donne l'impulsion de la fiche cible au tick du changement).
- [binaire, oracle] **Le lancer** (`0x8003237C`-`0x80032408`). Direction de l'objet = `mot[0x80023644 + 4 × héros.+0x98]` (`{0, 0x10, 8, 0x18}` : bas, haut, gauche, droite), avec le `+0x98` du tick précédent (oracle `g12` : tourner puis lancer en tenant une autre direction lance toujours dans l'ancien regard) ; position de l'objet = X, Y du héros, Z du héros + `0x200000` ; au sol, héros `0x09` et `+0x24` = 1 (aucune direction) ou 2 ; en l'air, héros `0x0A` et `+0x24` = 2 (aucune) ou 3. La garde `+0x180 & 0x2000` (case de propriété 0x20) interdit le lancer, jamais le soulèvement ; `& 0x4000` interdit le saut. **Carré lance toujours** : il n'y a pas de « poser ».
- [binaire, oracle] **C23** (`0x8006B848`), tick de l'objet : rien si `+0x28` nul. Sinon, `+0x24` nul → animation 0, **liens gardés**. Sinon animation = `mot[0x80027400 + 4 × (+0x24)]` (1 → 3, 2 → 1, 3 → 4, −1 → 3), puis `+0x24 = 0`, `porteur.+0x2C = 0`, `+0x28 = 0`, `drapeaux = (drapeaux | 0x30) & ~0x80`. La caisse (en-tête 36 de la carte 392) passe de `0x3323E4` à `0x332374`. Fiches d'animation : 3 vitesse 128, 1 vitesse 256, 4 vitesse 384, impulsion 1 024 : 1,5, 3 ou 4,5 px par tick vers l'est ou l'ouest, deux tiers de cela vers le nord ou le sud.
- [binaire, oracle] **E2** (`0x8007ED30`) : si `(animation == 2 et +0xAC == 1)` ou `(+0x184 & 4)` → `DestroyEntity(e, −1)` ; sinon animation 2 et `drapeaux = (drapeaux | 0x40) & ~0x30` (le masque est `~0x30`, `addiu −0x31` ; le relevé de la DLL écrit `~0x31`, faux ; sans conséquence sur le corpus, le bit 0 des drapeaux de ces types est nul) : `0x332374` devient `0x332344`. Le choix du programme prend E quand le drapeau `0x10` est levé et que `+0x13C` ou `+0x144` est non nul, ou `0x20` et `+0x220`, ou `0x40` et `+0xAC`.
- [oracle] **Vol, arrivée, destruction** (`g4`, T = tick du front de Carré, héros immobile, plat). Au sol sans direction : `anim_t` caisse 3, X + 1,5 par tick (110,5 à T, 143,5 à T+22), Z : 36 ; 39,5 ; 42,5 ; 45 ; 47 ; 48,5 ; 49,5 ; 50 ; 50 ; 49,5 ; 48,5 ; 47 ; 45 ; 42,5 ; 39,5 ; 36 ; 32 ; 27,5 ; 22,5 ; 17 ; 11 ; 4,5 ; 0 (T à T+22). **L'objet bouge dès le tick du lancer** (programmes, horloge, physique viennent après `MovePlayer` dans le même tick). `statut` 3 et `anim_t` 2 à T+23 (`flags6C` `0x332344`, son 27), statut 4 à T+25 avec l'effet d'attache 10, case libre à T+26. En marche (+0x24 = 2) : animation 1, + 3,0 par tick, arrivée en 178,0. Depuis le sommet du saut (Z héros 27,5) : Z0 = 59,5, arrivée à T+26, 3,0 (sans direction) ou 4,5 (direction) px par tick. Mur : arrêt à X = 133 (E2 à T+9, statut 4 à T+11). Marche de 16 px : pose dessus, puis E2. Marche de 48 px : touche la face. Un Beannoïde touché (x = 165) : `touch` posé à T+21, E2 à T+22, statut 4 à T+24 ; un Beannoïde sous l'arc (x = 150, Z de la caisse 47) n'est pas touché. La caisse lancée traverse une autre caisse (C23 efface `0x80` : plus un sujet).
- [oracle] **Ce qui termine le portage.** Blessure par case dangereuse, mort (PV 0), nage en eau profonde (pente 4, avec les bottes) : mode 2, l'objet est relâché (animation 3), se brise à l'arrivée (l'eau : effet 6). Verrou de dialogue (`[0x800DC4B8] & 0x34`) et case de sortie (`+0x188 & 0x80`) : mode 1, l'objet est avalé 21 ticks après si sa fiche a des attaques (la caisse : `AttackCount` 1, attaque 1, puissance 128 ; les graines : 0, donc gardées ; la bombe, la pierre lourde : 1). Chargement de carte : les 64 cases sont réécrites.
- [binaire, critique] **La fonction d'arrivée dans la même carte** (`0x80031340`, chemin `0x80031498`-`0x800314DC`) décale l'objet porté du même vecteur que le héros avant de poser le héros ; la copie de position de la DLL donne le même résultat au tick suivant [inférence sur l'identification de la fonction ; si ce téléport pose un verrou qui avale l'objet n'est pas établi, O-G-8].
- [binaire, oracle] **Pile** (`g11`) : le héros qui pousse contre deux caisses empilées soulève celle de la **plus basse case**, pas forcément celle du haut ; la caisse du haut tombe de 16 px quand celle du bas est partie ; si c'est celle du haut qui est prise, celle du bas reste en place.
- [binaire, oracle] **Genre 3** (« Pierre très lourde », sprite 234, 46×34×34, `CanPickup 0x27`, effet de bris 11) : sans l'objet `0x3B`, Carré donne l'épée ; avec lui, soulevé (tick 13), puis le héros reste en `0x0C` : droite et Croix sont ignorées. Le genre 2 n'existe pas dans le corpus.
- [binaire, oracle] **C4** (`0x80066984`) : même tête de portage que C23, mais il ne pose ni `0x30` ni n'efface `0x80` : une pierre lancée atterrit en (178, 100, 0), `statut` 2, `flags6C` `0x3323E4` inchangé ; le héros peut ensuite se tenir dessus (`support` = la pierre, Z 16) et la reprendre. [inférence, non exécutée] Elle se brise (E2, drapeau `0x20` de l'en-tête) si elle touche une cible.
- [données, critique] **Soulevables** : 17 en-têtes de l'export portent un genre ; C23 : 7 (carte 182, 35, 36, 38, 41, 48 et le sprite commun 234), E2 : 10 (les 7 plus 117, 34, 37), C4 : 7 (107, 111, 112, 113, 117, 34, 37), C70 et E20 : 1 (la torche 137), C73 et E21 : 1 (les graines, sprite commun 1), C62, D30 et E12 : 1 (la bombe, sprite commun 2). 2 251 placements sur 242 cartes : 2 002 en C23, 191 en C4, 57 torches, 1 bombe. Les indices de page des cartes sont uniques : le registre par type suffit. La chaîne (`AlundraStoryChainOpcodeAudit.ChainMaps`, 30 cartes) compte 18 cartes avec 219 soulevables (389, 391, 476, 478, 416, 162, 169, 10, 176, 183, 185, 135 : aucun) ; la liste de 12 cartes de `h-g-deps.md` est fausse (166, 167, 168 n'y sont pas ; 170 à 175, 177 à 182 et 184 y sont).
- [DLL] **Rien n'existe.** `PlayerTryAction` renvoie 0 (`AlundraPlayerManager.cs:927`) ; `UpdatePlayerCarriedEntity` est vide (`:1272`), appelée avec 0 en `:395` et avec 2 en `:646` (nage, E20.i) ; la branche verrouillée n'a pas l'appel du mode 1 (`:322-332`) ; les états `0x05` à `0x0C`, `0x2E` à `0x30` n'ont pas de cas ; `0x09` et `0x0A` sont dans les groupes d'attaque (`IsGroundAttackGroup`, `IsAirAttackGroup`) mais rien ne les entre. `CarriedEntity` et `PlatformEntity` (`AlundraEntityScriptProxy.cs:88-89`, `Entity?`) ne sont jamais écrits. `EntitySupport.IsEligibleSubject` exclut déjà `PlatformEntity != null`. Les deux `DestroyEntity` ne touchent pas le porteur (`AlundraWorldProxy.cs:3240`, `:3254`). Aucun test ne nomme les bouchons ; `CarriedEntity` n'apparaît que dans `AlundraLadderClimbTests`.
- [DLL, critique] **Le trou du contact.** Le soulèvement se déclenche sur `XCollisionEntity` (le `+0x130`), que la DLL remplit seulement quand la sonde de mouvement renvoie un obstacle (`AlundraEntityScriptProxy.cs:2355-2362`). La sonde passe `EntityFlags.PickupKindMask` comme drapeaux écartés (`AlundraMovementObstacleProbe.cs:48-49`) : un soulevable n'est jamais un obstacle (D-E19-28, D-E19-46). Aujourd'hui, **aucun soulevable ne peut être le contact** : `h-g-deps.md` §3.6 (« g1 garde la solidité éteinte ») est insoutenable pour une recette sur une vraie carte. Les gardes `ArcsWithoutEntityContact` (A1c, A3, A4p, A5, A5r, A6, A7, A8, A9, A10, A10J, A20) échouent à la fin d'un arc dont un pas est raccourci par une entité (`TotalEntityBlockCount`).
- [DLL, critique] **Ordre du tick** (`AlundraWorldProxy.cs:2860-2920`) : `MovePlayer` d'abord, puis les programmes des entités dans l'ordre des cases (C23 y tourne), les compteurs, **les listes (collidables et `RidingEntity`) à chaque tick**, l'horloge d'animation, la physique (le héros en premier : pas de récursion de porteur à écrire). Les commentaires qui parlent d'un recalcul « une fois par image » sont périmés. `StepAnimationClock` donne l'impulsion de la fiche cible au tick du changement (le saut Z d'un objet lancé part au tick du lancer) ; la vitesse horizontale d'une entité non héros lit encore `CurrentAnimationId`, posée à la fin de l'image : D-F5-5 (f5) la remplace par la clé de `TargetAnimationId` pour les entités marquées [à relire à l'exécution : la garde de la règle].
- [DLL, critique] **`RidingEntity` d'un objet porté** n'est jamais recalculé (il sort des collidables) : une caisse soulevée de la tête du héros garde `RidingEntity == héros`, et la boucle sans bouton de `PlayerTryAction` la soulèverait de nouveau dès le relâchement. Le binaire met `+0x12C` à 0 à chaque tick (`0x800389DC`).
- [DLL, critique] **Le piège « nul égale nul »** : `LogicContextEntity` vaut `null!` par défaut (`:483`) ; dans un banc de test où le héros n'a pas de `LogicContextEntity` et où un soulevable a `RidingEntity` nul, la comparaison de la boucle serait vraie.
- [DLL, critique] **Doc périmée** : `EntityPickupKind` (`EntityFlags.cs:274-290`) dit que les genres 2 et 3 se soulèvent « tant que le joueur ne possède pas `0x3B` » (inverse du binaire) et que le genre 3 « change les animations » (il interdit marche et saut).
- [DLL, tests] **Registre** : `ProductionRunner_RegistryHoldsTheSixHandlers...` épingle `Count == 6` (`AlundraNativeSpriteRegistryTests.cs:51`) ; d'ici g1a, f3 à f6 l'auront réécrite par dérivation. Aucun autre test existant ne peut atteindre un soulèvement, un portage ou un lancer (raisonnement d'atteignabilité du relevé des tests, six conditions R1 à R6, à confirmer par les compteurs de la phase B de g1b).

**Écarts tranchés par la critique** (les relevés et le plan sont corrigés ainsi ; les audits restent en lecture seule).
1. *« Mode 0 : X et Y avancent seulement tant que l'erreur de Z est strictement plus petite que le plus grand des décalages »* (`h-g-deps.md` §3.1, relevé de la DLL) est **CORRIGÉ** : à égalité les trois axes avancent (désassemblage `0x8002F950` : `bnez` sur « plus petit que », et oracle `c1_equality`). Le relevé du binaire avait raison.
2. *« Le mode 1 détruit quand les décalages sont sous `0x10000` »* (`h-g-deps.md`) est **CORRIGÉ** : au plus `0x1FFFF`, soit moins de `0x20000` (oracle `c1_equality`).
3. *« E2 efface `~0x31` »* (relevé de la DLL) est **CORRIGÉ** : `addiu −0x31` est le masque `~0x30` ; `0x332374` devient `0x332344`.
4. *« `0x06` et `0x2E` sont l'état au sol et l'état en l'air »* (`h-g-deps.md`) est **CORRIGÉ** : saut en marche et saut sur place, avec objet ; `0x2F` et `0x30` sont l'air.
5. *« La liste des collidables est refaite par image »* (relevé des tests) est **CORRIGÉ** : par tick depuis E20.a (`RunEntityBlock`), avec le recalcul de `RidingEntity` ; seuls des commentaires sont périmés (`AlundraWorldProxy.cs:2930`, `:3553`).
6. *« Le vol d'un objet lancé part avec un retard d'une image en XY »* (relevé de la DLL, Q-G3 du relevé) est **PRÉCISÉ** : vrai de `CurrentAnimationId` pour une entité non héros ; D-F5-5 (f5, qui passe avant g) lit `TargetAnimationId` pour les entités marquées. Le marqueur posé par g1 fait entrer l'objet lancé dans cette règle ; P-G-3 le prouve avant le code.
7. *« g2 exige f6a »* (relevé de la DLL) est **CORRIGÉ par l'ordre** : f6a (ligne 8) précède g (ligne 10) ; la mort d'un objet lancé dans l'eau profonde marche dès que g1 marque l'objet. Reste ouvert, et hors d'f6a : `+0x180`, `+0x184`, `DestroyOnVramFlags` des entités marquées (O-E0K-2) : D-G-14.
8. *« Le soulèvement par `XCollisionEntity` ne demande aucun changement de sonde »* (`h-g-deps.md` §3.6, « la solidité reste éteinte ») est **RÉFUTÉ** : voir le trou du contact. Q-G1.
9. *« Une branche de suivi de plateforme manque dans `MoveEntity` »* (`order-critic.md` ligne 19) est **CORRIGÉ** : `+0x28` n'est que le lien de portage du héros ; ce qui manque est la copie de position de l'objet porté. Les plateformes sont l'appui `+0x12C`, que la DLL a déjà pour le héros.
10. *« Le héros arrive sur la plateforme par téléportation »* (oracle `g5`) est **COMBLÉ** par `c2_platform` : marche et saut sur une plateforme qui marche vers lui, avec et sans caisse : même résultat, l'objet suit à (0, 0, 32).
11. *« Les 12 cartes de la chaîne qui portent des soulevables »* (`h-g-deps.md` §0.6) est **CORRIGÉ** : 18 cartes, 219 enregistrements (`ChainMaps`).
12. *« Les graines et la bombe demandent g1 et g2 en entier »* (ligne 11 de l'enveloppe) est **PRÉCISÉ** : elles demandent `PutInHands` et le lancer (g1a, g2a), ni E2 ni l'activation du soulèvement.
13. *« La mort d'un objet porté dans un verrou est un défaut »* est **REFUSÉ** : la fiche sans attaque (graines) est gardée (mode 1 → mode 0), celle avec attaque est avalée ; c'est un choix de conception de l'original, gardé.

**Décisions de conception (ADR : le prochain numéro libre au moment de l'écriture, revérifié dans `docs/decisions/README.md`).**
- **D-G-1** — Découpage et ordre : g1a, g2a, g2b, g1b (l'activation en dernier), g2c après un T0 de lecture. Chaque sous-tranche a ses phases, ses tests rouges nommés, sa preuve de retrait. Proposition à la session principale (ligne 11 de l'enveloppe) : E20.h3 exige g1a et g2a seulement.
- **D-G-2** — Une seule couture `PutInHands(héros, objet)` (interne). Elle écrit `CarriedEntity` du héros et `PlatformEntity` de l'objet (les `LogicContextEntity`, comme `RidingEntity`), le triplet `RelativeWarpOffsetX/Y/Z` = position de l'objet − position du héros en unités brutes, vide `RidingEntity` de l'objet, pose `HasLogicalZ` (D-E0K-1) et choisit l'animation cible du héros : `0x0C` ou `0x30` si `RidingEntity` de l'objet était le héros (sol, air), sinon `0x05` ou `0x0B`. `TryLift` l'appelle ; h3 l'appellera pour les graines et la bombe.
- **D-G-3** — Suivi de l'objet porté : une branche en tête de `TickMotion` pour une entité non héros, active, avec `PlatformEntity != null` : ni `TickScriptedNpc` ni `EvaluateEntitySupport` ; forces à 0 ; `ForceAdjusted` et `CollidedWithEntityZ` à 1 ; `XCollisionEntity` nul ; hauteurs du sol et du terrain copiées du porteur ; position = porteur + triplet, modules recalculés ; racine poussée à pixel entier (`PushLogicalPositionToRoot`). L'écart sous le pixel avec la racine flottante du héros est documenté (le binaire dessine aussi à pixel entier). Le héros passe avant dans le tick : pas de récursion de porteur.
- **D-G-4** — `UpdatePlayerCarriedEntity` : les trois modes à l'octet. Le compteur est un champ du héros, remis à 0 aux modes 0 et 2 et quand rien n'est porté, gardé après la destruction. Les décalages des modes 0 et 1 sont lus sur les positions. Le mode 1 appelle la destruction à un argument (ni butin ni effet ; elle efface le porteur), par `IEntityWorldContext.DestroyEntity`, déjà là. Sites câblés par g1a : mode 0 (`:395`) et mode 2 (nage, `:646`) par le corps de la fonction ; mode 1 par un appel nouveau dans la branche verrouillée, après les deux remises à zéro et avant `EndOfMovePlayer` (l'annulation d'objet reste le bouchon de h0). Sites laissés en bouchon nommé à leur place exacte : blessure `0x80031C64` (E20.c2b), case d'escalier ou de sortie `0x80032778` et mort `0x800327AC` (E18, c2b).
- **D-G-5** — États du héros : le groupe de portage et les états `0x05` et `0x0B` à l'octet (§ faits). Le saut est piloté par les fiches d'animation exportées (aucun code de saut). Genre 3 : `0x0C` ou `0x30` seulement. Gardes `NoAttackVramFlag` (lancer) et `NoJumpVramFlag` (saut), lues comme ailleurs. La branche de Carré du groupe est, en g1a, un bouchon nommé à sa place exacte (modèle D-E20-19) ; g2a le remplace.
- **D-G-6** — `PlayerTryAction` et `TryLift` à l'octet, en fonctions internes qui prennent un contexte explicite (contact, ensemble câblé, hôte, possession de `0x3B`). Trois garde-fous : la boucle refuse un `LogicContextEntity` nul (piège « nul égale nul ») ; un genre hors de l'ensemble câblé renvoie 0 comme le genre 0 ; les genres 2 et 3 lisent `0x3B` dans le sens du binaire. La doc d'`EntityPickupKind` est corrigée (g1a).
- **D-G-7** — Contact de soulèvement (Q-G1, défaut A) : un champ `LiftContactEntity` du héros, rempli par `MoveControllerAndPullPosition` avec `FindEntityCollisionCandidate` restreint aux soulevables (un paramètre facultatif `requiredCandidateFlags`, sans effet pour les appelants actuels), à la position de fin du pas **demandé**, nul quand aucun pas n'est demandé (la force du héros ne retombe pas d'un coup : le contact survit un tick après le lâcher, `c3`). Il ne touche ni `XCollisionEntity` ni `EntityBlockCount` (donc ni l'interaction avec les PNJ ni les gardes des arcs). E20.g3 le retire. Écart O-G-1.
- **D-G-8** — Ensemble câblé : `ProductionWiredLiftableTypes` (modèle de `ProductionWiredWeaponTypes`), vide jusqu'à la phase C de g1b, puis les 7 types C23 (indices de page 182, 35, 36, 38, 41, 48 des cartes, soit `0x100 +` l'indice [à confirmer par le T0], et le sprite 234 du banc commun) ; `WiredLiftableTypesForTests` remplace l'ensemble de production dans un test. Invariant de fermeture : un type câblé a tous ses codes C, D et E enregistrés. Torches, C4, graines, bombe : hors ensemble (g2c, E14, h3).
- **D-G-9** — C23 et E2 dans une classe `AlundraLiftableAi` : C23 en entier dans g1a (les deux moitiés : porté et relâché ; la moitié « relâché » sert déjà à la nage, mode 2), E2 dans g2b. Le registre porte l'ensemble complet de l'en-tête (clés (C, 23) pour les 7 types, (E, 2) pour les 10), l'ensemble câblé est plus petit (modèle D-F5-6).
- **D-G-10** — Lancer : la direction de l'objet est lue dans `SpawnDirections` indexé par la direction du regard au début du tick (`BoxDirection`, D-F3-2 ; la valeur est celle du tick précédent) ; position = X, Y du héros, Z du héros + `0x200000` ; `+0x24` ∈ {1, 2} au sol, {2, 3} en l'air ; héros `0x09` ou `0x0A`. La vitesse du vol vient de D-F5-5 (clé de `TargetAnimationId`) ; l'impulsion verticale est déjà prise au tick du changement par `StepAnimationClock`.
- **D-G-11** — `DestroyEntity` (deux surcharges) efface `CarriedEntity` du porteur de l'entité détruite, sans comparer l'identité (le binaire). Sans proxy de porteur (cas des projections d'oracle d'E14 : `new Entity { Name = "platform" }`) : rien, sans exception. `PlatformEntity` de l'entité détruite reste (le binaire n'y touche pas ; l'épingle X6 de ces projections reste vraie).
- **D-G-12** — `RidingEntity` d'un objet est remis à nul par `PutInHands` et par le relâchement de C23 (le recalcul de la DLL ignore les non-collidables) : plus de reprise sans bouton d'un objet lancé.
- **D-G-13** — Marqueur : posé par `PutInHands`, jamais retiré, jamais cloné ; l'état de carte d'une entité par enregistrement est déjà écrit (e0k1, chemin inchangé). L'objet relâché prend la loi d'e0k2 (Z avant XY, atterrissage strict).
- **D-G-14** — E20.e0b2 et le butin : l'arête g2 → e0b2 de l'enveloppe (révision 6) est **souple** (`order-critic.md`, arête 20, genre S) : le butin d'un objet brisé reste le bouchon nommé de `DestroyEntity` (D-E14-3), que porte E20.e0c. g2a et g2b n'écrivent ni ne lisent `+0x3C`. Proposition à la session principale : révision 10 de l'enveloppe qui retire e0b2 des prérequis de g2 (e0c et e0a4 le gardent). Tant que l'enveloppe ne change pas, l'ordre est g1a, g1b, puis g2a et g2b après e0b2. Les attendus qui dépendent d'un tirage (étincelles de coup) sont dérivés (D-E0B-8), jamais collés, pour que l'arrivée d'e0b2 ne les déplace pas. `+0x180`, `+0x184` et `DestroyOnVramFlags` des entités marquées (E2 lit `+0x184 & 4`, le choix du programme lit `+0x180 & 0x8004`) : le T0 d'f6a nomme leur propriétaire (O-E0K-2) ; à défaut g2b les prend, en lecture de `CombinedVramFlagsAND` tel que la DLL le tient (nul pour un non-héros) : E2 le lit déjà, rien ne le nourrit (écart O-G-2).
- **D-G-15** — Aucun rétypage : `CarriedEntity` et `PlatformEntity` restent des `Entity?` (dix sites de test inchangés) ; on y range `LogicContextEntity` et on atteint le proxy par `GameplayProxy`.
- **D-G-16** — Écarts documentés : O-G-1 (soulever en traversant), O-G-2 (fosses et attributs de tuile des entités marquées), O-G-3 (pas de butin), O-G-4 (sites de blessure, de mort et de case de sortie), O-G-5 (ascenseurs), O-G-6 (torches, C4, graines, bombe), O-G-7 (le lancer par un objet marqué part au sous-pixel près de la racine tronquée).
- **D-G-17** — Chaque épingle déplacée est réécrite par une **dérivation** (valeur calculée dans le test ou clé de l'oracle), jamais par une constante collée (D-E0B-8, D-E0K-10).

**Questions à l'auteur** (aucune ne bloque g1a, g2a ni g2b).
- **Q-G1 — Soulever avant que les caisses deviennent solides ?** *L'original :* les caisses et les jarres sont des murs pour le héros. On en soulève une en marchant contre elle puis en appuyant sur Carré : le héros reste collé au bord et l'objet part de 21 px à côté de lui. *Le port :* le héros traverse les caisses et les jarres tant que E20.g3 n'est pas faite (écart D-E19-28, pris pour ne pas déplacer les arcs de l'histoire). *Visible en jeu :* si l'on soulève avant g3, le héros est déjà dans la caisse quand il la saisit (de quelques px jusqu'à 21 px, selon le temps passé à pousser), et la caisse monte au-dessus de sa tête en 32 images au lieu de partir du côté ; le reste ne change pas. *Option A (défaut, recommandée) :* soulever dès g1b avec un détecteur de contact propre aux soulevables, retiré par g3. Les arcs existants ne bougent pas (des compteurs le prouvent avant l'activation). Coût faible, réversible. *Option B :* n'activer le soulèvement qu'avec g3 (fidèle dès le premier jour). Le soulèvement n'est alors visible qu'après g3, qui déplace beaucoup d'arcs (les 18 cartes de la chaîne avec des soulevables) et dépend de la barricade de la 392 (g4). Coût élevé ; aucune recette de portage avant.
- **Q-G2 — Que veut dire « plateforme » pour la recette ?** *L'original :* le héros ne monte que de 16 px en sautant (le sommet du saut est à 27,5 px), avec ou sans objet ; une plateforme qui bouge à l'horizontale l'emporte et l'objet suit ; une plateforme qui monte et descend emporte aussi l'objet, mais le port ne sait pas encore y tenir le héros (D5b, E19, en pause). *Le port après g :* marche de 16 px, dessus d'un objet, plateforme horizontale. *Option A (défaut) :* la recette se limite à ces trois cas. *Option B :* y ajouter les ascenseurs : D5b devient prérequis de la recette, et g n'avance pas plus vite.
- **Q-G3 — Faut-il un vrai « poser » ?** *L'original :* il n'y en a pas. Carré lance toujours ; immobile, l'objet part à environ 34 px et atterrit ; caisses et jarres se brisent en arrivant, pierres, tonneaux et blocs restent (g2c). *Le port :* identique. *Visible en jeu :* on ne dépose pas une caisse intacte sur une plateforme ; une pierre, oui. *Option A (défaut, fidèle) :* garder. *Option B :* ajouter un « poser » : un écart de plus, qui change les énigmes (les caisses ne se brisent plus). Question posée parce que le but cité parle de transporter ; une réponse n'est utile que si l'auteur veut cet écart.

**Prérequis et dépendants.** Voir le tableau de découpage. Tout ce qui dépend d'e0k2, d'f5 (D-F5-5), d'f6a ou d'e0b2 et n'est pas encore exécuté est **à relire à l'exécution** : le nom et le point d'appel du marqueur (`HasLogicalZ`, W1 à W4), la garde de la règle de vitesse, l'appel d'f6a dans `TickMotion`, la forme de `CreateForProduction` et son épingle, l'interrupteur d'arène (D-F4-7 : `NativeSpriteTypeFilterForTests`), `ProductionWiredWeaponTypes` (modèle), les numéros de ligne. Dépendants : E20.h3 (g1a et g2a ; proposition), E20.h4 (rien de g), E20.g3 (retire la sonde de contact de g1b ; exige g1b et g2b), E20.g4, E20.e0c (le butin d'un objet brisé : branche le bouchon de `DestroyEntity`), E14 (la torche E20, les objets des ennemis), E20.c2b (le site de blessure), E18 (mort, case de sortie).

---

#### 1.24.0 T0 commun à g1a, g2a, g2b, g1b et g2c

- ⏳ **T0 — Prédiction et oracle** (commit de docs, avant tout code), `docs/plan-e20-annexe/e20-g/`.
  - `oracle/` : les outils du relevé binaire copiés octet pour octet avec leurs sommes de contrôle (`gl.py`, `ana.py`, `k0lib.py`, `exe.py`, le dossier `oracle/` de la machine, `g1_lift.py` à `g18_c4_stone.py`, `run_all.py`) et les trois scénarios du critique (`c1_equality.py`, `c2_platform.py`, `c3_contact.py`), plus un quatrième à écrire, `c4_climb_lift` (Carré en état d'escalade, au contact d'un soulevable ; prédiction : soulevé, `anim_t` 5 ou 11) ; sorties en texte **et** en JSON (le JSON s'ajoute aux scripts sans changer un scénario ; clés = les noms de `HFIELDS` et de `CFIELDS`) ; deux exécutions complètes donnent un manifeste identique.
  - Au moins 16 mutants d'un mot, tous tués ou prouvés équivalents un à un : test du genre (`0x8002EDD4`-`0x8002EDE0`), branche de `GetNumberOfItem(0x3B)`, masque de Carré `0x80`, test de statut de la boucle, `+0x12C == héros`, pas de `0x10000` du mode 0, pas de `0x20000` du mode 1, seuil `0x1FFFF`, le 5 du compteur, `slt` de l'égalité du mode 0, cible `0x200000`, masque `~0x30` de E2, `~0x80` et `|0x30` de C23, table `0x80027400`, gardes `0x2000` et `0x4000`, `+0x98` lu à la place de `+0x8C`.
  - README : les vecteurs de « Faits porteurs » avec leurs clés ; la prédiction qualitative de chaque sous-tranche ; les comptes de départ de la suite (à relever) ; la **liste prédite recomptée au HEAD de départ** : toutes les méthodes de `Alundra.Tests/` qui atteignent `PlayerTryAction`, `UpdatePlayerCarriedEntity`, `CarriedEntity`, `PlatformEntity`, un type de l'ensemble câblé, un `Flags & 0x600`, ou qui épinglent le registre ou la liste des préréglages, avec leur compte.
  - Le recensement des cartes de la recette : une carte libre à l'arrivée (aucun verrou de contrôle dans son programme B[1]), avec au moins un soulevable C23 et, à moins de 100 px, un dessus d'objet de 16 px ; candidates du relevé : 224 (23 caisses), 27, 24, 159, 445 ; chacune passe un arc de construction (6 images). Le préréglage de la recette se nomme `porter-m<N>` ; il déplace l'épingle de la liste des préréglages (dérivation).
  - **Sondes de la DLL** (écrites, lancées au HEAD de départ, versées au README, puis retirées ; prédiction écrite avant) : P-G-1, une caisse de la 392 portée en animation 0 n'a aucune boîte d'attaque (l'export n'a des boîtes que pour les animations 1, 3 et 4) ; P-G-2, la valeur de `BoxDirection` au moment du lancer (prédiction : la direction du tick précédent) ; P-G-3, la garde de D-F5-5 couvre-t-elle une entité par enregistrement marquée par g1 ? (prédiction : oui si elle lit le marqueur, non si elle lit `JoinsBirthTick` : alors g2a étend la garde d'une ligne) ; P-G-4, la force verticale et le Z brut d'une caisse relâchée au tick du lancer (prédiction : Z brut = héros + `0x200000`, aucune perte) ; P-G-5, `RidingEntity` du héros au tick qui suit l'atterrissage sur une plateforme mouvante (prédiction : posé au tick suivant, comme `c2`).

---

#### 1.24.1 E20.g1a — Porter ⏳

**Prérequis.** E20.e0k2 (le marqueur `HasLogicalZ` et la loi du vol ; **à relire à l'exécution** : le nom, les points d'appel W1 à W4 d'e0k2), E20.e0a1 ✅, E14.a1b ✅ ; le T0 commun.

**But.** Un objet mis dans les mains par `PutInHands` suit le héros qui marche, saute, monte sur un dessus de 16 px et sur une plateforme mouvante, comme l'original ; le verrou, la nage et la destruction le traitent comme l'original. Aucun chemin de production ne met encore un objet dans les mains (g1b).

**Périmètre.** `AlundraPlayerManager.cs` (corps de `UpdatePlayerCarriedEntity`, groupe de portage, états `0x05` et `0x0B`, appel du mode 1, `PutInHands`) ; `AlundraEntityScriptProxy.cs` (branche de suivi de `TickMotion`, compteur de verrou du héros) ; `AlundraWorldProxy.cs` (les deux `DestroyEntity`) ; `AlundraEntityDepthKey.cs` ou `AlundraFrameSyncPasses.cs` (un objet porté se trie au moins comme son porteur, `0x80039824`-`0x80039858`) ; `EntityFlags.cs` (doc d'`EntityPickupKind`) ; nouveau `Alundra/Scripts/AlundraLiftableAi.cs` (C23) ; `AlundraNativeSpriteRegistry.cs` (une ligne, phase C) ; `Alundra.Tests/` ; annexe ; un ADR.

**Hors périmètre.** Le déclencheur (g1b), le lancer (g2a), E2 (g2b), C4 et torches (g2c), les sites de blessure, de mort et de case de sortie, l'annulation d'objet (h0), le butin (e0c), les ascenseurs (D5b), la solidité (g3).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : les fonctions pures d'`AlundraCarryRules` (pas, modes 0, 1 et 2, table de transitions du groupe de portage), la classe C23 (non enregistrée), `PutInHands` (sans appelant de production), le champ du compteur. Tests directs.
- **Phase B** (T3) : les six branchements, ensemble, car ils n'ont de sens qu'ensemble ; tous inertes en production parce que rien n'est porté : (W1) le corps de `UpdatePlayerCarriedEntity` appelle les règles (les sites `:395` en mode 0 et `:646` en mode 2 existent) ; (W2) l'appel du mode 1 dans la branche verrouillée ; (W3) la branche de suivi dans `TickMotion` ; (W4) le groupe de portage et les états `0x05` et `0x0B` dans la chaîne de `MovePlayer` ; (W5) l'effacement du porteur dans les deux `DestroyEntity` ; (W6) la clé de tri.
- **Phase C** (T4) : `CreateForProduction` enregistre les clés (C, 23) des 7 types. Inerte au repos (C23 ne fait rien si rien n'est porté). Seul effet visible : l'épingle du registre.

**Valeurs attendues** (clés de `g1_lift`, `g2_jump`, `g3_step`, `g13_onto_crate`, `c2_platform`, `g8`, `g15` ; L = tick du front de Carré ; héros en (100, 100), caisse en (130, 100)).

| Valeur | Clé | Attendu |
|---|---|---|
| À L, après `PutInHands` | `carried2C`, `carrier28`, `anim_t`, `off30`, `off34`, `off38` | caisse ; héros ; 5 ; 21 ; 0 ; 0 |
| De L+1 à L+11 (k) | `off38`, `off30`, caisse `pz` | k ; 21 ; k |
| De L+12 à L+32 (k) | `off30`, `off38`, caisse `px` | 32 − k ; k ; 109 + `off30` ; (0, 0, 32) à L+32, stable ensuite |
| Gestes | `anim_t` | 5 de L à L+35 ; 12 à L+36 ; 7 à la reprise de la marche |
| Caisse portée | `flags6C`, `ground`, `adj`, `collz`, `xcoll`, `support`, `fx`, `fz` | `0x3323E4` ; 0 ; 1 ; 1 ; 0 ; 0 ; 0 ; 0 |
| Marche portée | héros `px` | + 2,4375 par tick, comme sans caisse |
| Marche contre 16, 32 ou 48 px | héros `px`, `adj` | bloqué à 133,0, chargé ou non |
| Saut sur place (Croix en T) | `anim_t` ; héros `pz` ; caisse `pz` | 46 en T, 48 en T+1, 12 en T+21 ; 5 ; 9,5 ; 13,5 ; 17 ; 20 ; 22,5 ; 24,5 ; 26 ; 27 ; 27,5 ; 27,5 ; 27 ; 26 ; 24,5 ; 22,5 ; 20 ; 17 ; 13,5 ; 9,5 ; 5 ; 0 (T à T+20) ; héros + 32 à chaque tick |
| Saut en marche | `anim_t` ; `px` | 6 en T, 47 en T+1, 7 en T+21 ; + 2,4375 par tick, mêmes `pz` |
| Dessus d'un objet de 16 px (saut en t66, t70, t74) | `support`, héros `pz` | l'objet dès t85, t89, t93 (19 ticks après), 16 ; identique sans caisse ; 32 px : bloqué à 133,0 |
| Plateforme qui marche vers l'ouest (saut en t94 et t97) | `support`, X héros − X plateforme, caisse − héros | la plateforme dès t115 et t118, sans interruption jusqu'à la fin de la scène (50 ticks relevés) ; constant ; (0, 0, 32) ; identique sans caisse |
| Échelle (pente 6) | `anim_t`, héros `pz` | portée : 7 puis rien, Z 0 ; non portée : 14 en t66 |
| Verrou de 40 ticks posé au tick 60 | caisse `status`, héros `carried2C` | statut 4 au tick 81 (21 ticks), libre au 82 ; `carried2C` 0 au 81 ; fiche sans attaque : l'objet reste [dérivé du mode 0 et du fuzz direct `g10`, non exécuté en tick complet : le spawner d'enregistrement refuse ces en-têtes, O-G-10] |
| Nage (pente 4 au tick 78, bottes) | héros `anim_t`, `carried2C` ; caisse `anim_t`, `carrier28`, `flags6C` | 29 au 79, 0 ; 3, 0, `0x332374` au 79 |

##### Tâches

- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette qui compile, puis le code). Chaque cas « sans effet » est accolé à un cas positif dans la même assertion.
  - (c1) mode 0 direct : les sept cas de `c1_equality` (21, 0, 11) → (20, 0, 12) ; 21 px − 1 unité → X inchangé, Z 12 ; 21 px + 1 → X − 1 ; (0, −21, 11) → Y −20 ; (5, 0, 37) → (4, 0, 36) ; 5 px − 1 → X inchangé ; (0, 0, 32) inchangé ; les anciens décalages du héros sont ignorés, le compteur est remis à 0 ; la trace de (21, 0, 0) à (0, 0, 32) en 32 appels, X au 12e ;
  - (c2) mode 1 direct : compteur de 0 à 4 → + 1 sans écriture, accolé au compteur 5 qui écrit ; (0x1FFFF, 0, 0), (0, 0x1FFFF, 0x1FFFF) et (−0x1FFFF)×3 → destruction à un argument (compteur gardé, décalages du héros non réécrits), accolés à (0x20000, 0, 0), (0, 0, 0x20000) et (−0x20000, 0, 0) → avance de 2 px vers 0 ; `AttackCount` 0 se comporte comme le mode 0, accolé à `AttackCount` 1 ;
  - (c3) mode 2 direct : `t24` −1, direction du héros, position héros + (0, 0, 32 px), `CarriedEntity` gardé, compteur 0 ; rien de porté : aucun effet, accolé ;
  - (c4) C23 direct : non porté → rien, accolé au porté ; `t24` 0 → animation 0, liens gardés ; 1, 2, 3, −1 → animations 3, 1, 4, 3, `t24` 0, liens effacés, `0x3323E4` → `0x332374` ; un second appel ne change rien ;
  - (c5) `PutInHands` : champs, triplet brut, marqueur, `RidingEntity` vidé ; les quatre animations cibles (sol et air, objet sur la tête ou non), accolées deux à deux ;
  - (c6) le groupe de portage, table pure : (état, objet nul ou présent ou lourd, sol ou air, direction ou non, Carré, Croix, gardes `0x2000` et `0x4000`) → état suivant, chaque ligne accolée à sa ligne inverse ; états `0x05` et `0x0B` de même.
  Verts à la fin de T1.
- ⏳ **T2 — Tests de monde**, écrits après T1, **rouges avant T3** (sauf (w6)), chacun à 1, 2 et 3 ticks par image (bancs `JumpHeroRig` et `ContactWorld`, l'objet mis par `PutInHands` à la géométrie de l'oracle) :
  - (w1) suivi : position = héros + triplet à chaque tick, forces nulles, `adj` et `collz` à 1, `xcoll` nul, hors de `Collidables` ; (w2) la trace du soulèvement du tableau, tick par tick ; (w3) marche et marches de 16, 32 et 48 px : vitesse et blocage égaux à la même scène sans objet (accolés) ; (w4) saut sur place et en marche : séries du tableau, caisse = héros + 32 ; (w5) dessus d'un objet de 16 px (modèle UH10) et plateforme mouvante (modèle UH14, un tick de phase documenté) : `support` et l'objet qui suit, accolés à l'objet de 32 px que l'on n'atteint pas ;
  - (w6) **garde, verte dès T1** : l'entrée d'échelle est refusée en portant (`CaseSixGate`), accolée au non-porté qui entre ;
  - (w7) verrou : l'objet est avalé 21 ticks après, accolé à la fiche sans attaque qui reste ; (w8) nage : l'objet est relâché (animation 3, liens effacés), tombe et atterrit, **sans** se briser (E2 est g2b) ; (w9) `DestroyEntity`, les deux surcharges, d'un objet porté : `CarriedEntity` du héros effacé ; porteur sans proxy : aucune exception ; objet non porté : rien ne change ; (w10) clé de tri : un objet porté se trie au moins comme son porteur ; (w11) objet lourd (genre 3) : reste en `0x0C`, droite et Croix ignorées, accolé au genre 1 qui marche ;
  - (w12) **garde, verte dès T1** : le bouchon du lancer, Carré avec un objet porté ne change rien (g2a le remplace).
- ⏳ **T3 — Phase B** (commit de code B) : les six branchements. Verts.
- ⏳ **T4 — Phase C** : l'enregistrement. Tests de production, **rouges avant la ligne** : (r1) registre : (C, 23) pour les 7 types, pas pour les autres, épingle par dérivation ; (r2) une carte entière de caisses au repos (la 392) garde tous ses champs pendant 100 ticks, accolée à une caisse portée qui les change.
- ⏳ **T5 — ADR, plan, verifiers frais** (deux, l'un après l'autre, dont un sur les épingles : liste prédite contre liste mesurée). Mutants du code de la DLL, au moins 18, tous tués : `<` pour `<=` à l'égalité du mode 0 ; pas de 1 px changé ; seuil `0x1FFFF` changé ; le 5 du compteur changé ; compteur non remis à 0 ; mode 1 → mode 0 oublié pour `AttackCount` 0 ; destruction à deux arguments ; décalage lu sur le triplet au lieu des positions ; mode 2 sans `−1` ; mode 2 qui efface `CarriedEntity` ; suivi sans copie des hauteurs ; suivi qui garde les forces ; `XCollisionEntity` non annulé ; objet porté laissé dans `Collidables` ; effacement du porteur oublié (chacune des deux surcharges) ; effacement avec test d'identité ; états `0x06` et `0x2E` permutés ; marqueur non posé ; `RidingEntity` non vidé.

##### Acceptation

- **Oracle** : manifeste identique ; 16 mutants tués ou prouvés équivalents (T0 commun).
- **DLL** : verts (c1) à (c6) ; (w1) à (w5) et (w7) à (w11) **rouges avant T3** (nommés), (r1) et (r2) rouges avant T4 ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite (l'épingle du registre, phase C ; rien d'autre n'est attendu), chacun réécrit par dérivation ; aucun diff de `RunHeroVerticalTick` ni de `SnapHeroAtApex` ; les cinq traces et les arcs inchangés ; `cmp` de la DLL ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : retirer (W3) rend rouges exactement (w1), (w2), (w3), (w4), (w5), (w7) ; retirer (W4) rend rouges exactement (w3), (w4), (w5), (w11) ; retirer le corps de (W1) rend rouges exactement (w2), (w7), (w8) ; retirer la seule branche du mode 2 rend rouge exactement (w8) ; retirer (W2) rend rouge exactement (w7) ; retirer (W5) rend rouge exactement (w9) ; retirer (W6) rend rouge exactement (w10) ; retirer l'enregistrement rend rouges exactement (r1) et (r2).

##### Arrêts, budget, retour arrière

- **Arrêts** : un test existant hors de la liste prédite bouge ; la phase A fait bouger un test ; un test nommé rouge vert avant son branchement ; une sonde contredit sa prédiction (P-G-3, P-G-4) ; le Z brut de l'objet relâché perd une unité (le marqueur d'e0k2 ne tient pas) ; un diff du chemin du héros hors du groupe de portage ; plus de 10 tests existants bougent ; un besoin de changer le moteur.
- **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.
- **Retour arrière** : la phase C se retire seule (l'enregistrement) ; la phase B se retire seule (les six branchements) ; la phase A ne change aucun comportement.

---

#### 1.24.2 E20.g2a — Lancer ⏳

**Prérequis.** g1a ; E20.f5 (D-F5-5 : la vitesse d'une entité marquée lit `TargetAnimationId`) ; E20.e0k2 ; le T0 commun. **À relire à l'exécution** : la garde de D-F5-5 (P-G-3), la valeur de `BoxDirection` (P-G-2), le point d'appel dans le groupe de portage.

**But.** Carré lance l'objet porté comme l'original : direction, position, vitesse, arc, gestes `0x09` et `0x0A`. Sans E2 (g2b), l'objet atterrit et reste.

**Périmètre.** `AlundraPlayerManager.cs` (la branche de Carré du groupe de portage ; `SpawnDirections` partagée avec f3a), `AlundraScriptedMotion.cs` (une ligne, seulement si P-G-3 le demande), `Alundra.Tests/`, annexe, l'ADR de g1a. **Hors périmètre.** E2, le butin, C4, les dégâts de l'objet (la passe de coups existe ; g2b l'éprouve).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : `ThrowCarried`, fonction pure des écritures du lancer ; tests directs.
- **Phase B** (T3) : la branche de Carré du groupe de portage appelle `ThrowCarried` à la place du bouchon de g1a. Seul changement de comportement (inerte en production tant que rien n'est porté).

**Valeurs attendues** (clés de `g4`, `g4b`, `g12`, `g16` ; T = tick du front de Carré ; héros en 109,0, caisse portée).

| Valeur | Clé | Attendu |
|---|---|---|
| À T, sol, sans direction | héros `anim_t`, `carried2C` ; caisse `carrier28`, `t24`, `anim_t`, `flags6C` | 9, 0 ; 0, 0, 3, `0x332374` (C23 tourne dans le même tick) |
| Vol, sol, sans direction | caisse `px`, `pz` | 110,5 à T, + 1,5 par tick, 143,5 à T+22 ; Z : 36 ; 39,5 ; 42,5 ; 45 ; 47 ; 48,5 ; 49,5 ; 50 ; 50 ; 49,5 ; 48,5 ; 47 ; 45 ; 42,5 ; 39,5 ; 36 ; 32 ; 27,5 ; 22,5 ; 17 ; 11 ; 4,5 ; 0 (T à T+22) |
| Sol, direction tenue | `t24`, `anim_t`, `px` | 2 ; 1 ; + 3,0 par tick, 178,0 à T+22 |
| En l'air (héros à 27,5) sans / avec direction | `t24`, `anim_t`, `pz` à T | 2 / 3 ; 1 / 4 ; Z0 59,5, arrivée à T+26 ; + 3,0 / + 4,5 par tick |
| Regard | caisse `dir_t` | celui du **tick précédent** : est 24, nord 16, ouest 8, sud 0 ; est ou ouest 1,5 sans direction et 3,0 avec ; nord ou sud 1,0 et 2,0 ; tourner puis lancer au même tick lance dans l'ancien regard |
| Gardes | héros `anim_t`, `carried2C` | `+0x180 & 0x2000` : rien, l'objet reste porté ; `& 0x4000` : le lancer a lieu, le saut non |
| Geste (états `0x05`, `0x0B`) | `anim_t` | pas de lancer pendant le geste ; lancer possible en `0x0C` |
| Héros après le lancer | `anim_t` | 9 de T à T+18, 0 à T+19 ; en l'air 10, puis la chaîne 45 |

##### Tâches

- ⏳ **T1 — Phase A** (tests d'abord, puis le code). (t1) `ThrowCarried` direct : position, direction aux quatre indices (accolées deux à deux), `t24` ∈ {1, 2} au sol et {2, 3} en l'air, animations `0x09` et `0x0A`, garde `0x2000` (rien, accolée) ; (t2) le regard du tick précédent contre le regard de fin de tick.
- ⏳ **T2 — Tests de monde**, **rouges avant T3**, à 1, 2 et 3 ticks par image : (v1) vol au sol, sans direction ; (v2) en marche ; (v3) en l'air ; (v4) les quatre directions et les vitesses nord et sud ; (v5) les gardes `0x2000` et `0x4000` ; (v6) le geste ; (v7) le tick du lancer : X + 1,5 dès T (règle de vitesse) ; (v8) mur et marches de 16, 32 et 48 px : l'objet s'arrête au mur à X = 133, se pose sur la marche, **reste** (E2 absent) ; (v9) l'objet lancé traverse une autre caisse. Chaque cas « sans effet » est accolé à un cas positif.
- ⏳ **T3 — Phase B** : la branche. Verts.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux). Mutants, au moins 12, tous tués : indice de la table permuté ; `+0x98` lu à la place de `+0x8C` ; regard de fin de tick ; `t24` permutés ; Z sans `0x200000` ; garde `0x2000` inversée ; `0x4000` appliquée au lancer ; geste non protégé ; héros `0x09` et `0x0A` permutés ; vitesse lue sur `CurrentAnimationId` (la ligne de couverture de D-F5-5).

##### Acceptation

- **DLL** : (t1), (t2) verts ; (v1) à (v9) rouges avant T3 (nommés) ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite (aucun attendu) ; `cmp` ; cinq traces ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : retirer la branche de la phase B rend rouges exactement (v1) à (v9) ; retirer la couverture de la règle de vitesse pour l'objet marqué rend rouge exactement (v7).
- **Arrêts** : une sonde contredit sa prédiction (P-G-2, P-G-3) ; X au tick T diffère de 1,5 ; un test existant bouge ; un besoin de changer le moteur. **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après. **Retour arrière** : la phase B se retire seule.

---

#### 1.24.3 E20.g2b — Briser ⏳

**Prérequis.** g2a ; E14.a1b ✅ ; E20.f6a (la mort dans l'eau d'une entité marquée à gravité) ; E20.e0k2 ; E20.e0b2 selon D-G-14 ; le T0 commun. **À relire à l'exécution** : la sortie d'f6a (`Slope_18c`), l'interrupteur d'arène (D-F4-7), l'épingle du registre.

**But.** Caisses et jarres lancées se brisent comme l'original : à l'arrivée, à la touche d'une cible, contre un mur ; l'effet de bris est celui de l'en-tête. Le butin reste le bouchon nommé de `DestroyEntity` (D-E14-3).

**Périmètre.** `AlundraLiftableAi.cs` (E2), `AlundraNativeSpriteRegistry.cs` (clés (E, 2) des 10 types), `Alundra.Tests/`, annexe. **Hors périmètre.** Le butin (e0c), C4 (g2c), la nourriture de `+0x180` et `+0x184` (D-G-14), les dégâts réels sur les ennemis (E14).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement en production) : E2 et la méthode `Register`. Les tests enregistrent les clés eux-mêmes dans le registre du runner de test ; les objets viennent de `PutInHands` et de `ThrowCarried`.
- **Phase B** (T3) : `CreateForProduction` enregistre les clés (E, 2). Inerte au repos (un objet au repos ne lève jamais E). Seul effet visible : l'épingle du registre ; la casse n'est atteignable qu'avec g1b.

**Valeurs attendues** (clés de `g4`, `g4b`, `g7`, `g15` d ; T = tick du front de Carré).

| Valeur | Clé | Attendu |
|---|---|---|
| Arrivée à T+22 (plat, sans direction) | caisse `pz`, `status` | 0 ; 2 |
| T+23 | `status`, `anim_t`, `flags6C`, son | 3 ; 2 ; `0x332344` ; 27 |
| T+25, T+26 | `status`, effet, case | 4 + attache de l'effet 10 (jarre du sprite 41 : 2) ; libre |
| Mur en x = 133 (`wall-moving`) | `px`, `status` | X = 133 ; E2 à T+9 ; statut 4 à T+11 ; libre à T+12 (le tick du drapeau de mur : Q-K2, D-F4-9) |
| Marche de 16 px | `pz`, `status` | pose à 16 à T+20 ; E2 à T+21 |
| Beannoïde à x = 165 touché | `touch`, `hitc`, `status` | `touch` et `hitc` 1 à T+21 ; E2 à T+22 ; statut 4 à T+24 ; libre à T+25 |
| Beannoïde à x = 150 (sous l'arc, Z de la caisse 47) | `touch` | aucune touche ; arrivée et casse à T+22 et T+23 |
| Eau profonde sous l'arrivée (f6a) | effet | l'effet 6, au lieu du 10 |
| Un objet au repos | `status` | 2 pour toujours |

##### Tâches

- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette, puis le code). Chaque « sans effet » est accolé à un positif.
  - (b1) E2 direct, table : (animation 2 et `+0xAC` 1) → `DestroyEntity(e, −1)` ; `(+0x184 & 4)` → idem ; sinon animation 2 et `(drapeaux | 0x40) & ~0x30`, `0x332374` → `0x332344` ; les trois lignes accolées deux à deux ; (b2) la vie par le crochet : la chronologie du tableau pour une caisse (effet 10) et pour une jarre (effet 2) ; (b3) la touche (arène : caisses vivantes, Beannoïde inerte, D-F4-7) à x = 165, accolée au Beannoïde à x = 150 ; (b4) mur, marches de 16 et de 48 px ; (b5) aucun butin : aucune entité née, accolé à `Contents` 130 (le bouchon documenté) ; (b6) l'eau profonde (f6a) : effet 6 ; (b7) un objet au repos n'exécute jamais E2 ; les étincelles de la touche sont **dérivées** (D-E0B-8).
- ⏳ **T2 — Tests de production**, crochet nul, **rouges avant T3** : (p1) un objet lancé (par `PutInHands` et `ThrowCarried`) se brise en production à T+23, effet 10 ; (p2) l'épingle du registre, (E, 2) pour les 10 types, pas pour les autres ; (p3) un monde de caisses au repos (la 392) ne change pas pendant 100 ticks.
- ⏳ **T3 — Phase B** : l'enregistrement. Verts.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux). Mutants, au moins 12, tous tués : `~0x30` changé en `~0x31` (équivalent si aucun test ne lit le bit 0 : à prouver) ; `|0x40` oublié ; `+0xAC` ignoré ; `+0x184 & 4` oublié ; destruction avec un autre effet ; E2 enregistré pour un type hors des 10 ; clé (E, 2) oubliée.

##### Acceptation

- **DLL** : (b1) à (b7) verts ; (p1) à (p3) rouges avant T3 ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite (l'épingle du registre) ; `cmp` ; cinq traces ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : retirer l'enregistrement rend rouges exactement (p1) et (p2) ; retirer la ligne de masque `~0x30` rend rouge exactement (b1) et la chronologie de (b2).
- **Arrêts** : la chronologie T+22, T+23, T+25, T+26 diffère ; un butin naît ; un test existant bouge ; un besoin de changer le moteur. **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après. **Retour arrière** : la phase B se retire seule.

---

#### 1.24.4 E20.g1b — Soulever (l'activation) ⏳

**Prérequis.** g1a, g2a, g2b (ordre de D-G-1 ; si l'enveloppe maintient e0b2 pour g2, g1b passe après g1a et avant g2a, sans le lancer), E20.e0k2, le T0 commun. **À relire à l'exécution** : `MoveControllerAndPullPosition` après e0k2 et f3 à f6, l'ensemble de test d'f3a (modèle), l'état des préréglages.

**But.** Carré contre une caisse ou une jarre la soulève comme l'original (aux écarts de Q-G1 et O-G-1 près) ; un soulevable posé sur la tête du héros est soulevé sans bouton ; les genres 2 et 3 demandent l'objet `0x3B`. C'est la phase visible : la recette de l'auteur.

**Périmètre.** `AlundraPlayerManager.cs` (`PlayerTryAction`, `TryLift`, appels de l'état d'escalade, `ProductionWiredLiftableTypes`) ; `AlundraEntityScriptProxy.cs` (champ `LiftContactEntity`, remplissage dans `MoveControllerAndPullPosition`) ; `AlundraEntityCollision.cs` (paramètre facultatif `requiredCandidateFlags`) ; `AlundraTestSaves.cs` (préréglage de la recette) ; `docs/test-saves.md` ; `Alundra.Tests/` ; annexe ; ADR. **Hors périmètre.** La solidité (g3), les PNJ, torches, C4, graines et bombe (hors ensemble), la priorité de l'interaction (inchangée).

**Partage des phases** (seul à faire foi) :
- **Phase A** (T1, sans changement de comportement) : `TryLift`, `PlayerTryAction` et `FindLiftContact` en fonctions pures ; le bouchon `PlayerTryAction` renvoie toujours 0. Tests directs.
- **Phase B** (T2) : le remplissage de `LiftContactEntity` par `MoveControllerAndPullPosition` et les compteurs-témoins temporaires. Aucun lecteur de production : inerte.
- **Phase C** (T3) : l'activation, trois lignes ensemble : le corps du bouchon `PlayerTryAction` remplacé ; les appels de `TryUseItem` et de `PlayerTryAction` de l'état d'escalade ; `ProductionWiredLiftableTypes` = les 7 types. Seul changement de comportement.

**Valeurs attendues** (clés de `g9`, `g15`, `c3`, `g1_lift` ; la caisse en x = 130, le héros pousse vers l'est depuis x = 100).

| Valeur | Clé | Attendu |
|---|---|---|
| Contact | `xcoll` | non nul de t5 (le héros reste à 109,0) |
| Carré au tick 4 ou 5 | `anim_t` | 16 (l'épée), pas de soulèvement |
| Carré au tick 6 ou 7 | `carried2C`, `anim_t` | caisse ; 5 |
| Pousser aux ticks 1 à 9, lâcher au tick 10 | Carré au tick 10, 11 | soulèvement (la force résiduelle garde le contact) |
| Idem | Carré au tick 12 | l'épée |
| Pause de 5 ticks puis nouvelle poussée | Carré au premier tick, au suivant | l'épée, puis le soulèvement |
| Carré tenu depuis le tick 1 | `anim_t` | jamais de soulèvement |
| Haut tenu (sans pousser la caisse) | `anim_t` | l'épée |
| En l'air, contact au tick 5 | Carré au tick 6, 7, 8 | soulèvement (`anim_t` 11), soulèvement, l'épée en l'air (20) |
| Objet de genre 3 (sprite 234) sans / avec `0x3B` | `anim_t`, `carried2C` | l'épée / soulevé au tick 13 puis 12 à L+36 |
| Objet sur la tête (Z 32, `support` = héros) | `carried2C`, `anim_t` | soulevé au premier `MovePlayer` sans bouton ; 12 |
| Décalages écrits par `TryLift` (test direct, géométrie de l'oracle : poussée vers l'est, l'ouest, le nord, le sud) | `off30`, `off34` | (21, 0), (−21, 0), (0, −15), (0, +15) |

##### Tâches

- ⏳ **T1 — Phase A** (tests d'abord, puis le code). (l1) `TryLift` direct : genres 0, 1, 2, 3 × `0x3B` (huit cas), retour, appel de `PutInHands`, animation cible ; type hors ensemble câblé → 0, accolé au même objet dans l'ensemble ; (l2) `PlayerTryAction` direct : porté → 1 et `0x0C`/`0x30` ; contact et front → retour direct de `TryLift` (même 0, sans passer par la boucle) ; contact sans front → boucle ; boucle sans bouton : objet sur la tête soulevé, accolé à `RidingEntity` autre ; statut 4 ou `+0x20` non nul ignoré ; **héros sans `LogicContextEntity` : rien** (piège « nul égale nul »), accolé au héros qui en a un ; (l3) `FindLiftContact` pur : chevauchement du pas, bord contre bord (non), Z hors (non), premier de la liste, héros et objet porté exclus, pas nul → nul.
- ⏳ **T2 — Phase B** (tests puis code, puis compteurs). (k1) le champ est rempli à la position de fin du pas **demandé**, nul sans pas, vivant un tick après le lâcher ; il ne touche ni `XCollisionEntity` ni `EntityBlockCount`, accolé à un PNJ qui, lui, les remplit. **Compteurs-témoins**, retirés avant la phase C : (k2) nombre de pas où `LiftContactEntity` est non nul sur toute la suite hors des tests nouveaux, prédit **0** ; (k3) boucle sans bouton qui lève, prédit 0 ; (k4) `CarriedEntity` non nul à la fin d'un test existant, prédit 0. Un compteur non nul est un arrêt (liste mesurée contre liste prédite, avant l'activation).
- ⏳ **T3 — Phase C** (tests de monde écrits avant, **rouges avant C**) : (m1) la chronologie du tableau sur le vrai contrôleur (champ synthétique avec un soulevable de l'ensemble) ; (m2) l'arc de recette sur la carte choisie par le T0 (préréglage `porter-m<N>`) : soulever, marcher, sauter sur un dessus de 16 px, lancer, briser ; (m3) genre 3 sur la carte 3, 4, 7 ou 11, avec et sans l'objet `0x3B` ; (m4) objet sur la tête ; (m5) hors ensemble (torche de la 86, bombe de la 362 : genre 1 mais non câblés) : Carré donne l'épée, accolé à la caisse de la même scène qui se soulève ; (m6) priorité : contact PNJ → `TryLift` 0 → interaction ; contact caisse → soulèvement ; (m8) l'état d'escalade (`0x0E`, `0x35`, binaire `0x800321D4`-`0x800321E4` : `TryUseItem`, puis `PlayerTryAction`, dont un retour non nul termine le tick) : Carré au contact d'un soulevable câblé pendant l'escalade soulève (`anim_t` 5 ou 11, selon la prédiction du scénario `c4_climb_lift`), accolé à l'escalade sans contact qui ne change pas. Témoins (suite existante, verts avant et après) : les arcs de leviers (115, 116, 117, 44), `A5`, `A5r` et la chaîne. Puis la ligne d'activation.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux). Mutants, au moins 16, tous tués : genre 2 ou 3 sans `0x3B` ; `0x3B` inversé (la doc fausse) ; masque de Carré ; boucle avec bouton ; `+0x12C` ignoré ; statut 2 ou 3 élargi ; retour de `TryLift` du contact perdu ; ensemble câblé étendu à un type non enregistré ; sonde sur le pas effectué au lieu du pas demandé ; sonde qui remplit `XCollisionEntity` ; contact non nul sans pas ; héros inclus dans la sonde ; garde « nul égale nul » retirée ; priorité de l'interaction inversée ; appel de l'état d'escalade oublié ; marqueur non posé.

##### Acceptation

- **DLL** : (l1) à (l3), (k1) verts ; (k2) à (k4) à 0 ; (m1) à (m6) et (m8) **rouges avant C** (nommés) ; suite en Release **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite (l'épingle de la liste des préréglages) ; `cmp` ; cinq traces ; arcs de la chaîne inchangés ; moteur, convertisseur et données sans diff.
- **Preuve de retrait** : vider `ProductionWiredLiftableTypes` rend rouges exactement (m1) à (m6) et (m8) ; remettre le bouchon rend rouges exactement les mêmes ; retirer le remplissage de la phase B rend rouges exactement (k1) et (m1) à (m4), (m6) ; retirer les appels de l'état d'escalade rend rouge exactement (m8).
- **Recette de l'auteur** (non bloquante) : générer `porter-m<N>` hors de l'app (`docs/test-saves.md`), puis : (1) pousser une caisse ou une jarre et appuyer sur Carré : le geste de 36 images, l'objet monte au-dessus de la tête ; (2) marcher, tourner, sauter avec l'objet ; (3) sauter sur un dessus de 16 px ; (4) monter sur une plateforme qui bouge, si la carte en a une ; (5) Carré : l'objet part et se brise (sans butin avant E20.e0c) ; (6) l'échelle est refusée ; (7) une scène qui verrouille le contrôle avale l'objet. Avant g2, l'objet ne peut pas être lancé : l'écrire dans la recette.
- **Arrêts** : un compteur de la phase B est non nul ; un arc de la chaîne ou de levier bouge ; un test nommé de (m1) à (m6) ou (m8) vert avant C ; la boucle sans bouton relève un objet lancé ; plus de 10 tests existants bougent ; un besoin de changer le moteur. **Budget** : un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après. **Retour arrière** : la phase C se retire seule (ensemble vide, bouchon) ; la phase B se retire seule ; la phase A ne change aucun comportement.

---

#### 1.24.5 E20.g2c — C4 et les autres familles (esquisse, détaillée après un T0 de lecture seule) ⏳

**Prérequis.** g2b. **But.** Les pierres, tonneaux, troncs, rochers, blocs de glace et « Objets (transportables) » (C4, 191 placements) et les torches (C70 et E20, 57) : ce qui reste sur le sol après le lancer (les objets de g2a et g2b se brisent, ceux-ci demeurent et peuvent servir de marche, `g18_c4_stone`). **Pourquoi une esquisse.** Le corps de C4 (`0x80066984`, plus de 150 instructions : chute, rebond contre un mur en montée `0x80066A68`, son `0x18`, effet `0x8003BDD8`, le cas du type `0x175`) n'est décodé que dans sa tête ; la casse d'une pierre qui touche une cible (E2, drapeau `0x20`) est une inférence ; la torche lance des flammes (E20, hors de g : E14). **T0 de lecture seule** (avant tout plan détaillé) : décoder le corps de C4 et le rejouer sur la machine (pierre, tonneau, bloc de glace, sprite 117), poser la question de la touche, lister les trois en-têtes que le spawner d'enregistrement refuse (cartes 70 et 27) ; ensuite sous-tranches g2c1 (C4) et g2c2 (torches, avec E14).

---

**Points ouverts ajoutés.**

| # | Question | Tranchée par |
|---|---|---|
| O-G-1 | Soulever en traversant la caisse (le héros n'est pas collé au bord) tant que les caisses ne sont pas solides | E20.g3 (retire la sonde) ; Q-G1 |
| O-G-2 | `+0x180`, `+0x184`, `DestroyOnVramFlags` des entités marquées : mort au-dessus d'une fosse ou d'un attribut de tuile pendant le vol ; E2 lit `+0x184 & 4` | le T0 d'f6a (O-E0K-2), à défaut g2b (lecture seule, rien ne le nourrit) |
| O-G-3 | Un objet brisé ne lâche rien (cœurs, argent) | E20.e0c (le bouchon de `DestroyEntity`, D-E14-3) |
| O-G-4 | Sites du mode 2 et du mode 1 : blessure `0x80031C64`, case de sortie `0x80032778`, mort `0x800327AC` | E20.c2b, E18 |
| O-G-5 | Plateformes qui montent et descendent : le héros ne tient pas dessus (D5b) ; l'objet suivrait | E19 (D5b, en pause) ; Q-G2 |
| O-G-6 | Torches (C70, E20), C4, graines (C73, E21), bombe (C62, D30, E12) hors de l'ensemble câblé | g2c, E14, E20.h3 |
| O-G-7 | La racine d'un objet porté ou lancé est à pixel entier, celle du héros est flottante : écart sous le pixel (le binaire dessine à pixel entier) | accepté, documenté |
| O-G-8 | Portail de la même carte avec un objet porté : l'original décale l'objet avec le héros (`0x80031340`) ; la DLL le suit par la copie de position ; reste à établir si ce téléport pose un verrou qui avalerait l'objet | recette, ou une sonde de l'oracle |
| O-G-9 | Casse d'une pierre ou d'un tonneau (C4) qui touche une cible : inférence non exécutée | le T0 de g2c |
| O-G-10 | Le genre 2 (jamais dans le corpus) et les trois en-têtes refusés par le spawner d'enregistrement (`AttackCount` 0 et 7) ne sont couverts que par le fuzz direct | le T0 de g2c |

**Corrections induites.** (1) **Proposées, non appliquées le 2026-10-15** (elles iront dans une révision 10 de l'enveloppe, relue, quand g
deviendra la tranche suivante) : §1.1, ligne 10 : l'ordre interne g1a, g2a, g2b, g1b ; ligne 11 : h3 exige g1a et g2a ; D-G-14 : retirer
e0b2 des prérequis de g2. Jusque-là l'enveloppe READY (révision 9) fait foi. (2) `docs/plan-e20-annexe/e20-armes/h-g-deps.md` §3.1 et `order-critic.md` ligne 19 : les cinq corrections des écarts 1 à 4 et 9 (annexes en lecture seule : une note de renvoi dans le plan, pas de modification). (3) §1.18 O-E0K-5 : « Porté `+0x28` et passagers » renvoie à D-G-2, D-G-3 et D-G-12 (fait à l'insertion) ; O-E0K-2 : la révision 9 a déjà
nommé ses propriétaires (f6a, et le recensement de son T0) ; g2b ne prend rien de plus sans la révision 10. (4) Commentaires périmés à corriger par la tranche qui touche le fichier : `EntityFlags.cs` (`EntityPickupKind`, g1a), `AlundraWorldProxy.cs:2930` et `:3553`.

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
| O-E20-11 | Une entité neuve tourne-t-elle dans le même tick dans le binaire (répétition de `0x800386D0`, contrôle de D7 : non vérifié) ? E20.a garde la règle d'aujourd'hui (premier tick à l'image suivante) | **Tranché en partie le 2026-10-15** (E20.e0a3b, ADR-0065) : oui ; la DLL le fait pour les entités nées par l'interface (`SpawnEntityBySpriteIndex`) ; les apparitions par enregistrement attendent Q-A1 (E20.e0a4, mesure P-B au §1.16.4) |
| O-E20-14 | Atteignabilité en jeu des salles des leviers : la 115 nord-ouest après le hall (E14.a) ; la 116 derrière la grille (E14.a) ; la salle de la bascule de la 117, où la bascule n'apparaît que si l'on arrive par les portails internes 2, 4 ou 6 de la 117 (les arrivées depuis la 115 et la 116 sont hors de sa zone) ; la 44, dont on ne sait pas si le chemin vers les leviers passe par la case de la vague (43..44, 20) | recette de l'auteur, après E14.a |
| O-E20-15 | **Tranché le 2026-10-10 (D-E20-30)** : la poignée d'aura périmée est corrigée par un numéro de génération (E20.f1b) | — |
| O-E20-16 | Les courants (`+0x188`, branche `0x80032768`, dérive ; 16 cartes, dont la cascade de la 416) n'ont pas de propriétaire : E20.i2 avec le mur d'eau profonde (O-E19-22), ou plus tard ? (Q-I1) **E20.i (2026-10-10) les laisse dehors : sur la cascade de la 416, le héros nage sans dérive.** | l'auteur |
| O-E19-22 (rappel, ligne du plan d'E19 : `docs/plan-e19-opcodes.md`) | Le mur d'eau profonde sans bottes de niveau ≥ 2 (`(cellule & 0xE00) == 0x800`, `0x80037504`) : toujours sans propriétaire. **Après E20.i, un héros sans bottes nage là où l'original le bloque** (la mer de la 416 dès la nouvelle partie) ; les traces `*-L0`, `*-L1` et `R9-deepwater-noboots` sont mesurées (information). Recommandé : E20.i2, juste après (une égalité sur `0xE00`, pas un bit de masque : les échelles valent `0xC00`) | l'auteur (Q-N3) |
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
