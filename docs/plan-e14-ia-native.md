# E14 — IA native : plan d'E14.a (Muruta et Beannoïde)

> Statut : **approuvé par l'auteur le 2026-10-10** (D-E14-8, ADR-0058 : E14.a passe en premier). Rédigé la nuit du 2026-10-10.
> Étape maîtresse : `docs/plan-conversion-totale.md`, section « E14 — IA native ». Décisions de l'auteur : D-E20-4 (E14.a juste après
> la passe de coups), D-E14-1 à D-E14-7 (ADR-0055). Découverte en lecture seule et contre-vérification : `docs/plan-e14-annexe/e14a/`
> (`muruta.md`, `beannoide.md`, `dll-natives.md`, `check.md`).

**Révision 1 (2026-10-10).** Relecture : **REVISE**, trois points corrigés : l'ordre suit D-E20-24 (E14.a0, simple infrastructure,
est un prérequis des armes ; avancer E14.a1 est une question posée à l'auteur) et le registre a un seul propriétaire (E14.a0) ;
la comparaison du sol et des hauteurs de coin des ennemis est écrite par une formule unique, avec une source de valeurs attendues sur
les vraies cartes ; les entrées des aides qui lisent une direction ou une animation sont reliées aux valeurs du tick (D-E14-6).
**Révision 2 (2026-10-10).** Seconde relecture : **REVISE**, deux points corrigés (FIX) : seul le registre et son interrupteur sont
un prérequis des armes ; ils forment E14.a0, et les aides, le sol et les hauteurs de coin des ennemis, `Below(n)`, `HitReaction` et le
verrou du héros passent dans une tranche E14.a0b, après les collisions et avant E14.a1 (D-E20-24) ; la table des entrées porte les
adresses des chargements, relues dans le binaire (`TryAttackPlayerFront` lit `+0x98`, en `0x800805E0`, pas `+0x9C`). Une seule
relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-10). **À approuver par l'auteur** avant toute exécution.

## 0. Cadre

### 0.1 But

Les Muruta et les Beannoïde vivent comme dans `ALUN_CD.EXE` : ils se réveillent, marchent, attaquent, réagissent aux coups, perdent
des PV par `ComputeNewHp` (porté en E20.c1b-1) et meurent. Le hall de la 115 (quatre Muruta, `0x2C` aux `@414-433`, puis G569) et la
grille de la 116 (`0x2C` sur les Beannoïde des enregistrements 0 et 1) deviennent jouables : c'est le premier combat obligatoire de
l'histoire.

### 0.2 Décisions de l'auteur (2026-10-09, ADR-0055)

- **D-E14-1** : un gestionnaire natif est enregistré par type de sprite ; toute autre entrée native reste un no-op compté.
- **D-E14-2** : le convertisseur exporte l'octet de l'effet de bris (`+0x1E` de l'en-tête) dans E14.a, avec un export complet et une
  ADR de format (la révision de l'enveloppe d'E20, en pause, propose de l'avancer à E20.e0x : question Q-N2 posée à l'auteur).
- **D-E14-3** : le butin reste à E20.e0 ; d'ici là, un ennemi tué ne lâche rien.
- **D-E14-4** : E14.a passe avant la blessure du héros (E20.c2b), qui remesure ses arcs ; l'ordre donné ensuite par l'auteur
  (D-E20-24 : armes, objets, collisions ; les prérequis d'abord) place E14.a après ces tranches.
- **D-E14-5** : un interrupteur coupe les gestionnaires natifs dans les arènes d'oracle d'E20.c1a et E20.c1b.
- **D-E14-6** : les gestionnaires lisent les valeurs du tick (direction et animation de la boîte), comme le binaire.
- **D-E14-7** : recette : le hall de la 115 et la grille de la 116 ; la 362 et la vague de la 44 viennent avec.
- **D-E14-9** (2026-10-10, réponse à Q-A1-1, ADR-0060) : l'effet de bris est créé pour **tous** les appelants de `DestroyEntity(e, fx)`,
  comme le binaire (E0/E1 de tout sprite dont l'octet `+0x1E` est non nul, destruction sur pente `fx 6`, destruction par le drapeau
  VRAM `fx -1`), pas seulement pour le Muruta et le Beannoïde.

Choix de la session principale (à confirmer par l'approbation de ce plan) :
- E0/E1 et le livre de sauvegarde restent hors du registre (ils sont déjà épinglés ; `dll-natives.md` AD-2).
- A0 n'est enregistré que pour les types listés (D-E14-1), pas pour toutes les entités (`dll-natives.md` AD-3).
- Les quatre hauteurs de coin (`MapHeights`) et le sol (`FloorHeight`) des entités autres que le héros sont tenus à la création et à la
  fin de chaque pas de physique, comme `0x80038064`/`0x800370C4` (`check.md` C1, C2 ; `dll-natives.md` AD-5).
- **Ordre et propriété** : seul le registre par type et son interrupteur sont un prérequis des armes (les gestionnaires des
  projectiles du héros passent par le registre ; `docs/plan-e20-annexe/e20-armes/order-critic.md` lignes 23 à 31). Ils forment
  **E14.a0**, qui les **possède**, entre dans l'ordre de D-E20-24 comme prérequis, et que la révision de l'enveloppe d'E20 (en pause) cite
  comme prérequis d'E20.e0a. Le reste de l'infrastructure (aides, sol et hauteurs de coin des ennemis, `Below(n)`, `HitReaction`, verrou
  du héros) forme **E14.a0b**, travail d'E14.a. Depuis D-E14-8 (2026-10-10), E14.a0, E14.a0b et E14.a1 passent avant les armes.

### 0.3 Faits porteurs (contre-vérifiés, `check.md`)

- [binaire] Tables : maître `0x80098F4C` (A `0x800C4B34`, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`) ; C10 =
  `0x80068154`, D9 = `0x8007DE04`, E1 = `0x8007ED10`, A0 = `0x8006174C`, A1 = `0x80061758`, C1 = `0x80065ED4`, D1 = `0x8007D9A4`.
  Répartition `0x800388F8`-`0x80038944` ; ordre de choix (destructions, E, D, C, F seulement pour `[0x80127108]`).
- [binaire] `0x2C` (`0x8003DC84`) : `Result = (compte == 0)` ; le candidat n'est testé que sur `+0x48`, effacé par la remise à zéro de la
  case au tick qui suit E1 ; avec un coup au tick H : E1 à H+23, encore compté à H+24, zéro à H+25 (INFERENCE, à prouver par l'oracle).
- [binaire] Les Muruta du hall (115, enregistrements 24 à 27, sprite 91, type 347) ne sont pas natifs à l'apparition : leur C[25]
  (`@1464` : `2B 25 37[20] 2A 40[2,0] FF`) les passe au natif C10 après la chute ; D et E sont natifs dès le départ. Muruta L1 sur la
  115 (PV 3, deux coups d'épée), L8 sur la 362 (PV 15, puissance 6, huit coups). Beannoïde : PV 9, cinq coups d'épée.
- [binaire] Aides partagées : position relative `0x8007FE20` (Z = `+0x134` `FloorHeight`, égal à `TerrainHeight + 1` pour une entité
  non collidable, `0x80037F28`) et `0x8007FE8C` ; `TryAttackPlayer2` `0x8008048C` ; `TryAttackPlayerFront` `0x800805C8` ;
  `HandleAnimationDirection` `0x80080144` (lit les quatre hauteurs de coin `+0x158`-`+0x164`) ; `StartWalk` `0x8007FEF8` ; table de
  quart de tour `0x80028B34` (32 octets ; la décompilation et le doc de `ScriptHelper.DirectionTable` se trompent).
- [binaire] Générateur : C10 tire en 8 sites, C1 en 3, `StartWalk` 2 ou 3 fois ; la DLL n'a pas d'avance par tick (E20.e0b). Les
  gestionnaires se comparent donc appel par appel, avec le mot du générateur en entrée et en sortie ; les arcs n'affirment que des
  résultats que le générateur ne change pas.
- [DLL] `RunSpriteEvent` (`AlundraEventProgramRunner.cs`) ne tourne que le livre et E0/E1 ; `AlundraDamage.ComputeNewHp` n'a pas
  d'appelant de production ; le verrou d'animation du héros (`HeroAnimationLatch`) n'est pas exposé au contexte d'entité ;
  `FloorHeight` n'est tenu que pour le héros ; `MapHeights` n'est jamais écrit.
- [DLL, tests] L'arène de c1a/c1b fait suivre `RunSpriteEvent` au vrai lanceur : avec C1/D1 vivants, ses Beannoïde bougeraient
  (D-E14-5). Les arcs des leviers d'E20.c1b-2 tournent sur des cartes à Muruta et Beannoïde.

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Preuve | Recette |
|---|---|---|---|
| **E14.a0** | Registre par type de sprite (vide) appelé par `RunSpriteEvent` après le livre et E0/E1, entrée absente = no-op compté avec une trace par clé ; interrupteur des natifs pour les tests ; prérequis des armes | un test par type de sprite ; suite inchangée (aucune épingle de `SpriteEventRunCount` ne bouge) | — |
| **E14.a0b** (§1.2) | Infrastructure des ennemis, sans comportement visible : `FloorHeight` et `MapHeights` des entités autres que le héros, aides partagées, verrou d'animation du héros sur le contexte, `Below(n)`, squelette de la réaction aux coups (D1/D9) | aides contre le vrai code dans l'émulateur, états tirés au hasard ; sol et hauteurs sur les vraies cartes ; mutations ; suite inchangée | — |
| **E14.a1** (§1.3 : E14.a1a, E14.a1b, E14.a1c) | Beannoïde (A1, C1, D1 pour le type 335) et Muruta (A0, C10, D9 pour le type 347) ; E1 inchangé ; export de l'effet de bris (D-E14-2 : convertisseur, export complet, preuve par manifeste et double export, ADR de format) ; oracle d'E20.c1 étendu (les six gestionnaires réels, A lancé) ; arènes d'E20.c1a/c1b derrière l'interrupteur ; arcs des leviers relancés | oracle appel par appel ; arcs de la 115 (quatre Muruta tués par des coups scriptés, `0x2C` passe, G569 posé `@434`, portails rendus `@493-503`) et de la 116 (Beannoïde 0 et 1 tués, grille animée) | Le hall de la 115 et la grille de la 116 |

Ordre : E14.a0 (prérequis des armes, avant E20.e0a, qui en dépend), puis, après les armes, les objets et les collisions (D-E20-24),
E14.a0b puis E14.a1. **D-E14-8 (2026-10-10)** : l'auteur les avance tous avant les armes, les objets et les collisions.

### 1.1a E14.a0 — Le registre et l'interrupteur ✅ (CONFIRMED le 2026-10-10, verifier commun avec E14.a0b)

Relecture : **READY** dès la première lecture (2026-10-10).

**But.** `RunSpriteEvent` sait lancer un gestionnaire natif enregistré pour un type de sprite (D-E14-1), et un interrupteur coupe les
gestionnaires natifs dans les tests (D-E14-5). Le registre est vide : aucun comportement ne change.

**Faits porteurs.** `RunSpriteEvent` (`AlundraEventProgramRunner.cs`) tourne le livre de sauvegarde (`AlundraSaveBook.TryMatch`), puis
E0/E1, puis compte un no-op (`SpriteEventRunCount`) ; un emplacement natif est celui dont `ProgramIndexes[slot] & 0x7F == 0`
(`AlundraEntityScriptProxy.cs`, `RunPickedEvent`) ; le livre est filtré par type de sprite (`dll-natives.md` §1.4, le seul précédent) ;
les arènes d'E20.c1a/c1b font suivre `RunSpriteEvent` au vrai lanceur (`AlundraHitArena.cs`, `RecordingRunner`).

**Périmètre.** Un registre interne `(emplacement, code) → gestionnaire`, chaque entrée avec sa liste de types de sprite permis, appelé
par `RunSpriteEvent` après le livre et E0/E1 ; signature des gestionnaires sur le modèle du livre (entité, contexte, état) ; une entrée
absente ou un type non listé reste un no-op compté, avec une trace de débogage par clé ; un compteur des gestionnaires natifs lancés à
côté de `SaveBookEventRunCount` ; un interrupteur (propriété du lanceur ou du contexte) qui coupe le registre, posé par l'arène de c1a
et c1b ; le livre et E0/E1 restent hors du registre.

**Tâches.**
- ✅ **T1 — Registre et interrupteur**, avec un gestionnaire de test enregistré le temps d'un test. `AlundraNativeSpriteRegistry` (clé (emplacement, code), liste de types), `AlundraEventProgramRunner.NativeSprites` / `NativeHandlersEnabled` / `NativeHandlerRunCount`, interrupteur coupé par `HitArena` (c1a, c1b) ; une trace de débogage par clé absente.
- ✅ **T2 — Tests** (`AlundraNativeSpriteRegistryTests`, 12 tests ; suite 4617/4617 en Release et en Debug, aucune épingle ne bouge) : une entrée pour un type tourne pour ce type et pas pour un autre ; une clé absente reste un no-op compté ;
  l'interrupteur coupe tout ; le livre et E0/E1 ne passent pas par le registre ; les épingles de `SpriteEventRunCount`
  (`AlundraEventProgramRunnerProgramIndexTests`, `AlundraEventProgramRunnerTests`, `AlundraSaveBookDispatchTests`) ne bougent pas.

**Acceptation.** T2 vert ; registre vide en production ; `Alundra.Tests` sans échec en Release et en Debug ; traces inchangées ; verifier
frais (commun avec E14.a0b, à la frontière d'intégration la plus petite).

**Arrêts.** Une épingle du lanceur qui bouge ; un besoin de changer le moteur. **Retour arrière** : `git revert`.

### 1.2 E14.a0b — Infrastructure des ennemis ✅ (après E14.a0, D-E14-8 ; le registre est E14.a0 ; exécutée et CONFIRMED le 2026-10-10)

**Prérequis.** E14.a0 (le registre et l'interrupteur). D-E14-8 : E14.a passe en premier ; E14.a0b suit E14.a0 sans attendre les
armes.

**Périmètre.** `Alundra/Scripts/` :
- `FloorHeight` (`+0x134`) et `MapHeights[0..3]` (`+0x158`-`+0x164`) des entités autres que le héros, écrits à la création et à la fin de
  chaque pas de physique comme `0x80038064` et `0x800370C4`, avec la même convention que le héros (la DLL tient `FloorHeight` à la
  valeur du binaire moins 1 pour le héros, `AlundraEntityScriptProxy.cs`, sol sans support : l'ennemi suit la même convention) ;
- les aides : position relative (`0x8007FE20`, `0x8007FE8C`), `TryAttackPlayer2`, `TryAttackPlayerFront`, `HandleAnimationDirection`,
  `StartWalk`, la table `0x80028B34`, `Below(n) = ((ulong)(uint)Next() × n) >> 32`, le squelette `HitReaction(e, anim)` de D1/D9 (appel
  de `ComputeNewHp`, `Bytes[3]`, animation, direction opposée au héros) ;
- le verrou d'animation du héros exposé au contexte d'entité (membre par défaut, comme `EquipmentRecords`) ;
- le doc d'adresse faux de `ScriptHelper.DirectionTable` corrigé (commentaire).

**Entrées des aides : valeurs du tick (D-E14-6).** Chaque champ du binaire que lit une aide est relié à un champ de la DLL ; ceux qui
portent une direction ou une animation prennent la valeur du tick, pas celle de fin d'image :

| Champ du binaire | Lu par (adresse du chargement) | Champ de la DLL |
|---|---|---|
| `+0x98` (direction d'animation) | `TryAttackPlayerFront` (`lw 0x98` en `0x800805E0`) | `BoxDirection` (valeur du tick), jamais `AnimationDirection` (fin d'image) |
| `+0x120`, `+0x124` (cases X, Y) | position relative (`0x8007FE28`-`0x8007FE34`, et `0x8007FE94`-`0x8007FEA0` pour la seconde entrée) | `TileX`, `TileY` (écrits à chaque tick, `check.md` ligne 97) |
| `+0x134` (sol) | position relative (`0x8007FE38`, `0x8007FE3C` ; `0x8007FEA8` pour la cible de la seconde entrée) ; `HandleAnimationDirection` (`0x8008014C`) | `FloorHeight` |
| `+0x11C` (Z) | seconde entrée de la position relative, pour l'entité elle-même (`0x8007FEA4`) | `PosZ` |
| `+0x158`-`+0x164` (hauteurs de coin) | `HandleAnimationDirection` (`0x80080150`-`0x80080164`) | `MapHeights[0..3]` |
| `+0x8C` (direction cible) | `HandleAnimationDirection` (`0x80080184`, `0x800801B0`, `0x800801DC`, `0x80080208`) | `TargetDirection` |
| `0x80098708` (mot du générateur) | `StartWalk` (lu en `0x8007FF00`, relu en `0x8007FFD4` ; écrit en `0x8007FF60`, `0x8007FFA4`, `0x8007FFF8`) | `AlundraRandom.RandomSeed` (32 bits bas, par `Next()` ; `Below(n)`) |
| `+0x114`, `+0x118` (X, Y) de l'entité | `StartWalk` (`0x8007FF70`, `0x8007FF78`) ; D1 (`0x8007D9D4`, `0x8007D9DC`) ; D9 (`0x8007DE34`, `0x8007DE3C`) | `PosX`, `PosY` |
| `+0x114`, `+0x118` du héros (slot 0, `0x80127D30`) | `StartWalk` (`0x8007FF6C`, `0x8007FF74`) ; D1 (`0x8007D9D8`, `0x8007D9E0`) ; D9 (`0x8007DE38`, `0x8007DE40`) | `PosX`, `PosY` du héros (`IEntityWorldContext.PlayerEntity`) |
| `+0x224` (entité qui touche), `+0x1C4` (fiche), `+0x14` (vie), `+0x1C8` de l'attaquant | `ComputeNewHp` (`0x8003A39C`, `0x8003A3B4`, `0x8003A3B8`, `0x8003A3BC`), appelée par D1 (`0x8007D9B0`) et D9 (`0x8007DE10`) | `TouchingEntity`, `BalanceRecord`, `Hp`, `CurrentAttack` (`AlundraDamage.ComputeNewHp`) |
| `0x80146DF8` (verrou d'animation du héros), `0x80146E00` (trois fiches d'équipement) | `ResolveBalanceTarget`, via `ComputeNewHp` | `IEntityWorldContext.HeroAnimationLatch`, `IEntityWorldContext.EquipmentRecords` |
| écrits : `+0x88` / `+0x8C` | `StartWalk` (`0x8007FFC0` / `0x8007FF8C`, `0x8007FFB8`) ; D1 (`0x8007D9C8` / `0x8007D9F0`) ; D9 (`0x8007DE28` / `0x8007DE50`) | `TargetAnimationId` / `TargetDirection` |
| écrits : `+0x277` (octet), `+0x282` (16 bits) | D1 `0x8007D9C0`, D9 `0x8007DE20` ; `StartWalk` `0x80080010` | `Bytes[3]` ; `AIValues[1]` |

`TryAttackPlayer2` (`0x8008048C`) ne lit que le tableau des positions relatives qu'on lui passe. `HandleAnimationDirection` écrit `+0x88`
(`0x80080230`), remet à zéro les forces `+0xBC`-`+0xD8` et écrit `+0x8C`.

L'exécuteur complète la table pour les aides qu'elle ne couvre pas encore (`StartWalk` `0x8007FEF8`, le squelette de D1/D9), toujours
avec l'adresse du chargement relue dans le binaire, la garde dans le code (commentaire de chaque aide) et dans l'annexe, et arrête si
deux sources se contredisent ou si un champ n'a pas d'équivalent au tick.

**Hors périmètre.** Tout enregistrement de gestionnaire réel (E14.a1) ; l'effet de bris et son export (E14.a1) ; le butin (E20.e0) ;
l'avance du générateur par tick (E20.e0b).

#### Tâches

- ✅ **T1 — Oracle des aides.** Dans `docs/plan-e14-annexe/e14a/oracle/`, à partir de l'oracle d'E20.c1b (`docs/plan-e20-annexe/e20-c1b/
  oracle/tools/`, copié au bit près) et de fichiers neufs : (a) chaque aide lancée en vrai code sur des états tirés au hasard (au moins
  10 000 par aide), sorties gardées ; (b) **sol et hauteurs de coin sur les vraies cartes** : les cellules des cartes 14, 115 et 116
  (`data-extracted/data/map_<N>.json`) chargées dans la mémoire émulée à la place du champ plat (`*[0x800DD86C] + 0x604`, 52 × 60 × 8
  octets), puis `0x800370C4` et `0x80038064` lancés pour le corps de chaque Beannoïde et Muruta de ces cartes et pour le héros posé à
  côté, sur sol plat et en hauteur (Muruta du hall à 16, Beannoïde de la grille à 20) ; second interprète ; deux relances, même
  manifeste.
  **Fait (2026-10-10)** : `docs/plan-e14-annexe/e14a/oracle/` (`notes.md`, `tools/run_e14a0b.py`, `out/`) ; neuf aides × 10 000 états tirés au hasard, égalité avec le modèle indépendant et avec le second interprète, couverture complète ; 1 711 lignes sur les cellules réelles des
  cartes 14, 115 et 116 (sol plat et en hauteur) : `floor = terrain + 1` d'une unité brute pour chaque corps et pour le héros ; 19 mutations attrapées, 2 témoins inchangés ; deux relances, même manifeste.
- ✅ **T2 — Sol et hauteurs de coin des ennemis.** Ports et tests contre les sorties (b) de T1.
  **Fait (2026-10-10)** : `AlundraEntityScriptProxy.UpdateEnemyGroundHeights` (création : `AlundraEntitySpawnFactory.ApplySpawnInitialization` ; chaque pas : `EvaluateEntitySupport`, hors héros) ; `AlundraEnemyGroundTests` (8 tests : 1 711 lignes de l'oracle sur les
  cartes 14, 115, 116 lues dans l'export, plus le spawn de production des sept Beannoïde de la 14). Formule vérifiée : `FloorHeight(DLL) = floor(binaire) − 1`, `MapHeights[k](DLL) = coin(binaire)[k] − 1`, donc `FloorHeight − MapHeights[k]` et `|dz|` du couple
  égalent ceux du binaire (même écart − 1 pour le héros et pour les ennemis ; plats et en hauteur). **Décision** : l'écriture ignore un proxy nu (`SpriteType` −1, jamais créé depuis un enregistrement de sprite) pour que l'épingle E2
  `AlundraFloorHeightTests.ProductionCallSite_NpcNeverGetsFloorHeight_StaysZero` (un pion de test nu) reste verte sans changement ; son nom (« un PNJ n'a jamais de sol ») ne dit plus la vérité pour les entités de la carte : à renommer par l'auteur ou la session principale.
  **Disposition de la session principale (2026-10-10)** : garde gardée (aucun gestionnaire natif ne tourne pour un type −1, le registre étant indexé par type de sprite, donc aucun lecteur de ces champs) ; l'épingle est renommée `ProductionCallSite_BarePawnWithoutSpriteRecord_NeverGetsFloorHeight_StaysZero`, son commentaire dit ce qu'elle épingle.
- ✅ **T3 — Aides.** Ports exacts, table des entrées complétée ; tests contre les sorties (a) de T1, valeur par valeur ; un test qui
  échoue si une aide lit `AnimationDirection` alors que `BoxDirection` diffère.
  **Fait (2026-10-10)** : `Alundra/Scripts/AlundraEnemyHelpers.cs` (position relative et sa seconde entrée, `TryAttackPlayer2`, `TryAttackPlayerFront`, `HandleAnimationDirection`, `StartWalk`, `QuarterTurnTable`, `Below`) ; `AlundraEnemyHelpersTests`
  (16 tests : 90 000 états de l'oracle valeur par valeur ; le piège `BoxDirection` / `AnimationDirection` par un leurre `+0x9C` dans chaque état et par un test à part ; 5 mutations du port attrapées). Table des entrées complétée ci-dessus (adresses des chargements relues dans le
  binaire, aucune contradiction entre les sources). Le squelette `HitReaction` est en T4.
- ✅ **T4 — Verrou du héros sur le contexte, `HitReaction`, doc.**
  **Fait (2026-10-10)** : `IEntityWorldContext.HeroAnimationLatch` (membre par défaut, 0 ; `AlundraWorldProxy` renvoie le verrou de sa passe de coups) ; `AlundraEnemyHelpers.HitReaction(entité, animation, contexte)` (D1 = 5, D9 = 2) ; `AlundraEnemyHitReactionTests` (5 tests : 20 000 états des
  gestionnaires D1 et D9 du binaire, `ComputeNewHp` remplacé par une souche dans l'oracle ; l'équipement et le verrou du contexte ; sans héros ; le défaut et la valeur du monde ; 4 mutations du port attrapées) ; le commentaire d'adresse faux de `ScriptHelper.DirectionTable` corrigé
  (la table d'octants est à `0x80022488`, `0x80028B34` est la table de quart de tour).

#### Acceptation

- Chaque aide égale l'oracle sur tous ses états ; chaque mutation du script de mutations est attrapée.
- Sol et hauteurs de coin, formule unique : pour chaque ennemi et pour le héros des sorties (b) de T1, l'écart `FloorHeight(DLL) −
  FloorHeight(binaire)` est le même (la convention de la DLL, − 1) ; `FloorHeight − MapHeights[k]` (k = 0 à 3) et `|dz|` entre le héros et
  l'ennemi égalent ceux du binaire.
- Rien ne change de comportement : `Alundra.Tests` sans échec, traces du héros et de l'intro inchangées, arcs des leviers inchangés.
- Build Release puis Debug ; verifier frais.

#### Arrêts

- La formule du sol et des hauteurs de coin ne tient pas pour un ennemi (écart différent de celui du héros, ou différences fausses) :
  pause, question à l'auteur.
- Une aide demande un appel non porté hors de son périmètre.
- Un besoin de changer le moteur.

**Budget** : un `executor`, deux tentatives au plus ; un verifier frais. **Retour arrière** : `git revert`.

**Vérification (2026-10-10) : CONFIRMED** (E14.a0 et E14.a0b ensemble, au HEAD `51bd341`, après la fusion du moteur `1959b27`).
Verifier frais : `Alundra.Tests` 4646/4646 en Release puis en Debug, `cmp` de la DLL, traces inchangées ; oracle relancé
(`run_e14a0b.py`, 2 min 46 s, tout PASS, `out/manifest.txt` identique, aucun fichier suivi changé) ; dispatch de `RunSpriteEvent` relu
(compteur, livre, E0/E1, puis registre) ; registre vide en production (aucun `Register` hors des tests) ; épingles de
`SpriteEventRunCount` inchangées depuis `a2187e6` ; les tests des aides lisent l'oracle mot à mot ; dix mutations du port faites hors
du dépôt, chaque famille d'aides attrapée ; aucun lecteur de production ne voit un `FloorHeight` ou des `MapHeights` différents pour
une entité autre que le héros (le moteur n'en lit aucun ; seul `AlundraPlayerManager` lit celui du héros) ; argument de la garde
`SpriteType < 0` vérifié (le registre filtre par type ; les 9 741 entités des cartes exportées ont toutes un `SpriteTableIndex`).
Remarques P4, dispositions de la session principale :
- test du verrou côté monde tautologique (`AlundraEnemyHitReactionTests`, il ne distingue pas `=> HeroAnimationLatch` de `=> 0` quand
  le verrou vaut 0) : **reporté** (à renforcer quand E14.a1b lance D1 avec un verrou non nul) ;
- `Register` accepte le type −1 : **contrôle ajouté à E14.a1b** (aucune inscription ne liste −1) ;
- le support par une autre entité dans `ComputeFloorHeight` n'est pas couvert par l'oracle h2 : **reporté à E14.a1b** (un vecteur
  avec un ennemi posé sur une entité, si l'oracle en offre un ; sinon noté comme limite).

### 1.3 E14.a1 — Découpage ⏳ (détaillé le 2026-10-10)

**Révision 1 (2026-10-10).** Relecture de l'enveloppe : **REVISE**, un point corrigé (FIX) : §1.3d nomme les quatre tests
d'`AlundraNativeSpriteRegistryTests` que les enregistrements de production touchent, avec la tranche et la nouvelle valeur ; la
classe entière a été relue contre les six clés de production.
Relecture de clôture : **READY** (2026-10-10).

Découverte en lecture seule du 2026-10-10 (quatre agents, dont une contre-vérification adversariale) : export de l'octet de bris,
oracle avec les six gestionnaires réels, effet de bris et arcs de la 115 et de la 116. Scripts et sorties dans le scratchpad de la
session (`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e14a1/`, sous-dossiers `converter-export/`, `oracle-handlers/`,
`destroy-arcs/`, `e14a1-check/`) ; ce qui suit en reprend les faits contre-vérifiés.

| Tranche | Contenu | Preuve | Prérequis |
|---|---|---|---|
| **E14.a1a** (risque : format de données) | `BreakEffect` dans `Data/sprite-records.json` (D-E14-2, D-E20-26) ; export complet en place, manifeste, double export ; ADR de format | prédiction au bit près ; manifestes ; convertisseur vert | E14.a0b CONFIRMED ; fenêtre calme (O-E14-7) |
| **E14.a1b** | Oracle `oracle-a1/` ; `BreakEffect` dans la DLL ; ancre figée ; `DestroyEntity(e, fx)` crée l'effet de bris pour tous ses appelants (D-E14-9) ; Beannoïde A1, C1, D1 (type 335) ; arc de la grille de la 116 | appel par appel contre l'oracle ; arc G116 ; mutations | E14.a1a CONFIRMED ; **E20.f1b CONFIRMED** (ancre et clé, B3 ; O-E14-4) |
| **E14.a1c** | Muruta A0, C10, D9 (type 347) ; arc du hall de la 115 ; recette | appel par appel ; arc H115 ; mutations | E14.a1b CONFIRMED |

Ordre (session principale, 2026-10-10) : E14.a1a, puis E20.d2a et E20.f1b (déjà prêtes, `docs/plan-e20-heros.md`), puis E14.a1b,
puis E14.a1c. E1 ne change pas : la branche E de `RunSpriteEvent` reste hors du registre ; l'effet de bris vit dans `DestroyEntity` de
l'hôte. Chaque tranche : relecture du plan, exécuteur, verifier frais.

### 1.3a E14.a1a — L'octet de l'effet de bris dans l'export ✅ (faite et CONFIRMED le 2026-10-10, `669078d` à `0fb0954`)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, un point corrigé (FIX) : l'acceptation de T4 est écrite par rapport à
l'export de référence de T1 (`m1` et son `report.json`), plus en valeurs absolues de `m0` qu'un rebasage permis par les arrêts
contredirait ; la taille de sortie tient compte du `report.json` précédent, compté par `MeasureOutput` (`ConversionReport.cs`)
dans un export en place, comme L-b2 (« à l'écart de `report.json` près »). Point P3 corrigé : la montée du moteur part du
pointeur du dernier export.
Relecture de clôture : **READY** (2026-10-10).

**Faits porteurs.**
- [binaire] `DestroyEntity` `0x8003A59C` : quand `fx == -1`, l'effet est l'octet `0x1E` de l'en-tête `+0x64` (`0x8003A5D8`-`0x8003A5E8`) ;
  0 = aucun effet ; sinon `0x8003BE74(0, fx, 0, e ; 1, 0, 0, 0)`, retour inutilisé.
- [données] La clé `BreakEffect` de `data-extracted/data/map_*.json` (`SpriteInfo.SpriteRecords[i].Header`) est présente dans les 2 658
  fiches des 484 fichiers, égale toujours `Ubuff[14]` (l'octet `+0x1E`) ; les 395 banques n'ont aucun conflit entre cartes, sur aucun
  champ de l'en-tête (le choix « première fiche » du lecteur est sûr).
- [convertisseur] Lecteur : `ReadHeader` (`Readers/SpriteBankReader.cs`, vers `:422-442`) et la classe `SpriteRecordHeader` (vers
  `:154-171`) ; écrivain : `SpriteRecordJson` (`Writers/SpriteWriter.cs`, vers `:1240-1267`) et `WriteSpriteRecords` (vers
  `:1149-1222`) ; propriétés dans l'ordre de déclaration, CRLF, indentation de 2 espaces ; la DLL ignore une clé inconnue
  (`SpriteRecordCatalog`, options par défaut), donc l'export et la DLL peuvent arriver dans n'importe quel ordre.
- [arbre] `alundra-project/` est la base : 24 103 fichiers hors `Alundra.dll`, `Alundra.pdb` et `.casaeditor/`, empreinte
  `bc31b7a2055a1dd939ef3e4280203c94a3ef354e` au 2026-10-10 ; il diffère de `docs/plan-e19-loop-annexe/lb-1008/m7-manifest-after.txt`
  par les 47 chemins de `docs/plan-e19-loop-annexe/data/predicted_export_changes.txt` plus `UI/Textures/font3.png` (L-b2). Le pointeur
  du moteur a bougé depuis le dernier export (2026-10-08, L-b2) : `1b2b616a`, `a24fe31d`, `b2205d9f`, puis `781d3527` (fusion
  d'`origin/main` du 2026-10-10). Le convertisseur compte 848 tests (campagne de la fusion du 2026-10-10).

**Périmètre.**
- `SpriteBankReader.cs` : champ `BreakEffect` entre `SizeZ` et `Contents`, lu par `GetOptionalInt32(…, "BreakEffect")` ; doc de la
  classe (« header +0x1E, read by DestroyEntity 0x8003A59C »).
- `SpriteWriter.cs` : propriété `BreakEffect` entre `SizeZ` et `Contents`, toujours écrite, 0 compris ; copie dans
  `WriteSpriteRecords` ; doc de la classe. Pas de nouveau compteur dans `report.json`.
- `SpriteWriterSpriteRecordsTests.cs` ; `docs/formats/sprite-records.md` (schéma et exemple) s'il existe, sinon la doc de format qui
  décrit `sprite-records.json` (l'exécuteur la nomme).
- ADR de format : **ADR-0061** (0060 est D-E14-9) ; revérifier le numéro juste avant d'écrire.
- Annexe `docs/plan-e14-annexe/e14a/a1a-export-prediction.md`, écrite avant le code.

**Hors périmètre.** La DLL (E14.a1b) ; `BankKey` (E20.e0x, qui part de l'état laissé ici) ; tout miroir de `data-extracted/`.

**Outils** (copiés dans le scratchpad de l'exécuteur, jamais lancés depuis le dépôt) : `converter-export/manifest.py` et
`diff_manifests.py` (format vérifié identique au manifeste engagé) ; `e14a1-check/bescan.py` ou `converter-export/predict_records.py`
(mêmes octets) ; fichier prédit `converter-export/sprite-records.predicted.json` (2 561 505 o, SHA-1
`007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`). Chemins relatifs à `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e14a1/`.

**Tâches.**
- ✅ **T0 — Prédiction et état de départ.** Annexe écrite ; manifeste `m0` (attendu : 24 103 fichiers, empreinte ci-dessus ; un écart
  s'explique avant d'aller plus loin) ; `diff_manifests.py` de `m0` contre `m7-manifest-after.txt` : exactement les 47 chemins plus
  `UI/Textures/font3.png` ; compte de tests `Alundra.Tests` relevé.
- ✅ **T1 — Export de référence au HEAD**, avant toute modification du convertisseur. Fenêtre calme : aucun build, aucune suite, aucun
  Launcher ni éditeur sur `alundra-project/` ; `Alundra.dll` déverrouillé. Depuis la racine :
  `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project` ; sortie 0 et
  `Verification: PASSED` ; `m1` contre `m0` : seulement `report.json` (sinon, l'arrêt de T1). Valeurs de référence relevées dans le
  `report.json` de T1 : compteurs, avertissements, `Verify.*`, `OutputFileCount`, `OutputSizeBytes` ; taille du `report.json` présent
  sur le disque avant T1 et après T1.
- ✅ **T2 — Tests rouges.** `"BreakEffect": 8` écrit entre `SizeZ` et `Contents` ; clé absente → écrite à 0, pas omise ; le fichier
  reste hors catalogue.
- ✅ **T3 — Code et docs.** `dotnet test alundra-casaengine-project-converter.Tests --blame-hang-timeout 60s`, au premier plan ; 848 plus
  les nouveaux, tous verts.
- ✅ **T4 — Export 1, puis export 2 tout de suite.** `m2` contre `m1` : changés exactement `Data/sprite-records.json` et `report.json`,
  0 ajouté, 0 retiré ; `cmp` du fichier exporté avec le fichier prédit ; `m3` contre `m2` : `report.json` seul (D-N-7).
- ✅ **T5 — Suites.** `Alundra.Tests` en Release puis en Debug : même compte, sans échec (la DLL ignore la clé jusqu'à E14.a1b) ; `cmp`
  de la DLL ; traces inchangées.
- 🧪 **T6 — ADR-0061, plan, verifier frais.** ADR-0061 écrit et indexé ; plan et annexe à jour ; le verifier frais est lancé par la session principale.

**Acceptation.**
- `Data/sprite-records.json` : 2 561 505 o (+9 132), SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`, 395 fiches ; histogramme
  {0: 284, 2: 1, 8: 63, 10: 30, 11: 15, 12: 1, 13: 1}, soit 111 non nuls ; `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08` (Muruta) = 8 et
  `1ef8f16c-605f-58c3-af19-b6bb602fc40f` (Beannoïde) = 8 ; ordre des clés : …`SizeZ`, `BreakEffect`, `Contents`, `IdsvAnimDirs`,
  `AnimSets`[, `DialoguePortrait`] ; chaque objet égal à l'ancien, moins `BreakEffect`.
- Tout ce qui suit est **relatif à l'export de référence de T1** (`m1` et son `report.json`) ; les valeurs de `m0` ci-dessous sont
  celles attendues pour T1, pas pour T4.
- `AssetInfos.json` égal à celui de `m1` (attendu en T1 : `a09917c31694925076d1fc1aba5fca773f6cada2`).
- `report.json` de l'export 1 contre celui de T1 : `Errors` 0 ; mêmes `Warnings` (6 attendus en T1) ; mêmes `Verify.Assets`,
  `Verify.Loaded`, `Verify.ExistenceChecked` (22 616, 20 139, 2 477 attendus en T1) ; `Entities.Prefabs` et `SpriteRecords.Exported`
  égaux (395) ; même `Phase1.StalePagesPurged` (2 415 attendu) ; même `OutputFileCount` (24 106 attendu) ; aucun compteur nouveau ou
  disparu ; `OutputSizeBytes` = valeur de T1 + 9 132 + écart de taille de la DLL et du PDB + écart de taille du `report.json` présent
  sur le disque au moment de chaque mesure (mesuré ; un reste qui n'est pas expliqué par ces trois termes est un arrêt).
- Export 2 contre export 1 : `m3` contre `m2`, `report.json` seul ; mêmes compteurs hors durées.
- Rien ne change sous `alundra-project/UI/Screens/` (suivi par git) ; convertisseur vert ; `Alundra.Tests` sans échec en Release et en
  Debug ; verifier frais.

**Arrêts.**
- T1 change autre chose que `report.json` : attribuer d'abord à la montée du pointeur du moteur depuis le dernier export (jusqu'à
  `781d3527`), rebaser comme L-b2 (nouvelle base `m1`, consignée dans l'annexe ; l'acceptation, relative à `m1`, reste valable),
  puis seulement continuer ; un écart que la montée n'explique pas : question.
- Après le code : tout autre chemin changé, ajouté ou retiré ; `AssetInfos.json` qui bouge ; fichier différent de la prédiction ;
  second export différent hors `report.json` ; erreur ; compteur ou avertissement non prévu.
- Tout process (build, test, Launcher, éditeur) sur l'arbre pendant un export.
- Toute idée de supprimer `alundra-project/`, de faire un `robocopy /MIR` ou un miroir de `data-extracted/`, ou d'indexer
  `CasaEngineMonogame/CasaEngine.Launcher/Program.cs`.

**Résultat de l'exécution (2026-10-10).** Toutes les acceptations mesurées, aucun arrêt : T1 ne change que `report.json` (la montée du
moteur jusqu'à `781d3527` n'a changé aucun octet exporté, pas de rebasage) ; export 1 : `Data/sprite-records.json` et `report.json`
seuls, fichier égal à la prédiction au bit près (SHA-1 `007f2660…`) ; export 2 : `report.json` seul ; `OutputSizeBytes` = T1 + 9 132 + 8
(écart de taille du `report.json` présent) + 0 (DLL et PDB) ; convertisseur 849/849 ; `Alundra.Tests` 4 646/4 646 en Release et en Debug
(une première passe Release a eu 1 échec non capturé, non reproduit en trois passes complètes ensuite). Preuves : annexe
`docs/plan-e14-annexe/e14a/a1a-export-prediction.md`, dossier `a1a/`, ADR-0061.

**Vérification (2026-10-10) : CONFIRMED** par deux verifiers frais, l'un après l'autre (workflow) :
- **rejeu** : `m4` égal à `m3` ; un export de plus ne change que `report.json` ; `Data/sprite-records.json` égal à la prédiction au bit
  près ; compteurs de `report.json` égaux à ceux de T1 ; `OutputSizeBytes` expliqué à l'octet (+9 132, DLL et PDB du dernier build
  Debug, `report.json` présent) ; convertisseur 849/849 ; `Alundra.Tests` 4 646/4 646 en Release et en Debug (l'échec unique de la
  première passe Release de l'exécuteur ne se reproduit pas) ; `cmp` de la DLL ; traces inchangées ;
- **lecture seule** (INCONCLUSIVE par construction : sa lentille interdisait l'exécution, fournie par le rejeu) : diff conforme au
  périmètre, tests avant le code, annexe avant le code, ADR-0061 et docs de format ; recalcul indépendant des 395 fiches depuis
  `data-extracted` (UUIDv5 recalculés) : 0 écart de `BreakEffect` ni des 15 autres champs d'en-tête ; l'ancien fichier se retrouve au
  bit près en retirant la clé (SHA-1 `772bb8b7…`).
Remarques P4, **reportées** : l'échec Release non capturé de l'exécuteur (non reproduit en quatre passes) ; le lien vers l'ADR-0061
ajouté au commit T3 avant l'ADR (résolu au HEAD) ; le bruit de fin de ligne des traces ; Visual Studio (`devenv.exe`) et d'autres
`dotnet.exe` tournaient sur la machine pendant les exports, sans effet sur la preuve (manifestes et prédiction exacts).

**Budget** : un `executor`, deux tentatives au plus ; `plan-verifier` avant l'exécution ; `verifier` frais à la fin. **Retour arrière** :
`git revert`, puis un export complet en place.

### 1.3b E14.a1b — Effet de bris, Beannoïde, grille de la 116 🧪 (faite et CONFIRMED le 2026-10-10, `3cb5920` à `bf519fa` ; recette de l'auteur avec E14.a1c)

**Révision 1 (2026-10-10).** Relecture : **REVISE**, trois points corrigés (FIX) après une découverte ciblée en lecture seule
(table des champs, projection d'E1, format JSON, puis un critique adversarial ; rapports dans le scratchpad de la session,
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e14a1/a1b_table.md`, `a1b_projection.md`, `a1b_critic.md`, scripts sous `disc-e14a1b/`) : (1) une règle de
projection d'E1 dit ce qui est comparé et exclu, avec ses comptes ; (2) une table des champs binaire → DLL et le lecteur JSON à
écrire remplacent le « chargeur réutilisé », qui n'existe pas sous cette forme ; (3) but, hors périmètre, budget et retour arrière
sont écrits. Le critique a aussi **réfuté** un fait de la version précédente : `+0x1B8` n'est pas du code mort (voir les faits).
**Révision 2 (2026-10-10).** Seconde relecture : **REVISE**, un blocage ; dispositions de la session principale (cap de deux
relectures automatiques atteint) : **FIX** du blocage — le compte des effets de bris de l'arc G116 se fait à l'image E1 + 1 sur un
prédicat observable (modes 1 et 2 : un emplacement de bris passe en mode 2 dès sa première mise à jour, `MoveFollower` voyant
l'ancre figée au statut 4) ; **FIX** des points non bloquants : C5 se lit à l'application de la file sans mise à jour (`RunFrame` à
0 tick) pour le mode 1, puis après la première mise à jour pour le mode 2 ; le rang d'`Emit` est celui de la source en cours ;
l'épingle `FrameOf(C, 323)` = E1 + 1 s'appuie sur l'ordre de la DLL (`RecycleDestroyedEntities` en tête du bloc des entités) ;
`Bytes` se convertit en `byte` ; l'arc G116 tourne hors de la collection du générateur (limite écrite) ; la passe de l'oracle
(environ 500 s) peut être rendue à la session principale. Nouvelle époque de relecture : une seule relecture de clôture suit.
Relecture de clôture : **READY** (2026-10-10). Exécution après E20.f1b CONFIRMED.

**But.** Le Beannoïde (type 335) vit comme dans `ALUN_CD.EXE` (A1, C1, D1 par le registre) et toute destruction par
`DestroyEntity(e, fx)` crée l'effet de bris de l'en-tête comme le binaire (D-E14-9) ; la grille de la 116 s'anime après la mort des
deux Beannoïde des enregistrements 0 et 1.

**Faits porteurs.**
- [binaire] `DestroyEntity` `0x8003A59C`, dans l'ordre : (1) butin `0x80032B90` (`0x8003A5B0`), sans effet tant que `+0x3C` vaut 0 ;
  (2) statut 4 (`0x8003A5BC`) ; (3) `+0x228 = -1` (délai de branchement `0x8003A5CC`) ; (4) si `+0x1B8` (effet actif attaché) est non
  nul : libération de cet effet (`+0x68` de l'emplacement à 0, `0x8003A5D0`) et `+0x1B8 = 0` (`0x8003A5D4`) ; (5) `fx == -1` → octet
  `+0x1E` de l'en-tête (`0x8003A5E0`-`0x8003A5E8`) ; si `fx != 0`, `0x8003BE74(0, fx, 0, e ; 1, 0, 0, 0)` (arguments de pile écrits en
  `0x8003A5F8`, `0x8003A608`, `0x8003A60C`, `0x8003A610`, `0x8003A618`) : X/Y/Z lus en `+0x114/118/11C`, mode 1, pas de son, pas de
  tirage, rien quand la réserve est pleine ; (6) effacement du porteur `[+0x28] + 0x2C = 0` (`0x8003A62C`).
- [binaire, **corrigé**] `+0x1B8` **est écrit** par `UpdateActiveEffects` (`0x80038E84`, boucle des entités, `sw $s1, 0x84($s0)` en
  `0x80038F5C` avec `s0 = entité + 0x134`) : c'est l'état normal d'une entité active ; la décompilation le dit aussi
  (`EntityManager.cs`, `entity.ActiveEffect = effect`). La DLL laisse cette passe vide jusqu'à E20.e (`RunActiveEffectsPass`,
  `AlundraWorldProxy.cs`) et n'a pas de membre pour `+0x1B8` : l'étape (4) est un bouchon nommé, sans effet, qui renvoie à E20.e.
- [binaire] 38 sites d'appel, dont la pente (`fx = 6`, `0x8003878C`) et le drapeau VRAM (`fx = -1`, `0x800387C4`) de la passe de choix ;
  la DLL leur passe déjà 6 et −1 ; tout appel d'E1 passe −1.
- [DLL] `AlundraWorldProxy.DestroyEntity(entity, effectId)` ne pose aujourd'hui que `Status` (et journalise) : ni `EventTrigger`, ni
  effet, ni butin ; la surcharge à un argument écrit `EventTrigger = -1` ; le −1 d'aujourd'hui vient de la fin de `RunPickedEvent`.
  Écrire `EventTrigger = -1` dans la surcharge à deux arguments ne change rien en production (les appelants de pente et VRAM finissent
  à `ProgramUnknown`, `RunPickedEvent` finit à −1).
- [DLL] La branche E de `RunSpriteEvent` ne tourne que si `EventTrigger == 4`, `(ProgramIndexes[4] & 0x7F) == 0` et
  `(uint)SpriteProgramIndexes[4] <= 1` ; `RunSpriteEvent` n'est appelé que par `RunPickedEvent`.
- [DLL] La file des effets est appliquée en fin d'image (`AlundraEffectStage.RunFrame`) ; dans une image de 2 ticks ou plus, une ancre
  vivante est déjà recyclée au statut 0 et `MoveFollower` gèle l'emplacement sans écrire sa clé : la création prend donc une **ancre
  figée** au moment de la destruction (statut 4, X, Y, `PosZ + 1`, clé de profondeur d'E20.f1b).
- [DLL] `AlundraRandom.RandomSeed` est statique (64 bits, `Next()` rend les 32 bits bas) : les tests qui le posent sont dans
  `[Collection(AlundraRandomStaticStateCollection.Name)]` et rendent la graine (précédent `AlundraRandomTests.cs`).
- [oracle] Mort avec un coup au tick H : D à H+1, animation 6 à H+14, E1 à H+23, case recyclée à H+24, `0x2C` d'un événement de carte
  nul à H+25 ; pour un programme d'entité, nul dès H+24 (ordre du binaire ; dans la DLL, `RecycleDestroyedEntities` tourne en tête
  du bloc des entités, ce qui donne le même ordre : l'épingle de l'arc en dépend, et un écart est un arrêt à diagnostiquer).
- [DLL] Un emplacement créé par une opération en file est mis à jour dans le même tick que son application (`RunFrame` : opération,
  puis `_reservoir.Tick`) ; à cette mise à jour, `MoveFollower` voit l'ancre figée au statut 4, copie position et clé, et passe en
  mode 2. Le mode 1 ne s'observe donc qu'à l'application sans mise à jour (`RunFrame` à 0 tick applique toute la file sans
  `Tick`).
- [oracle] Dans les 37 appels D des scénarios, l'attaque de l'attaquant est `(attribut 0x81, puissance 1)` au verrou 16
  (`curattack_1C8 = 0x80144229` ; `docs/plan-e20-annexe/e20-c1/oracle/notes.md` ; `e20-c1b/oracle/out/K2.json`) ; les vecteurs portent la
  paire explicitement.

**Périmètre.**
- `SpriteRecordCatalog` (en-tête, JSON, `ToHeader`), champ `BreakEffect` du proxy, `Clone`, `ApplySpawnInitialization`.
- Ancre et clé : celles d'E20.f1b (B3 : `AlundraEntityDepthKey.Compute`, l'aide d'IDSV partagée, l'ancre du proxy en `PosZ + 1`),
  reprises telles quelles ; une ancre figée prise à la destruction, après l'écriture du statut.
- `AlundraWorldProxy.DestroyEntity(entity, fx)` dans l'ordre du binaire, pour **tous** ses appelants (D-E14-9) : bouchon nommé du butin
  (E20.e0), statut, `EventTrigger = -1`, bouchon nommé de la libération de l'effet attaché (`+0x1B8`, E20.e), résolution de `fx = -1`
  par `BreakEffect`, création de l'effet ; l'effacement du porteur reste à E20.g (commentaire à sa place).
- `AlundraNativeEffects.CreateBreakEffect(reservoir, fx, anchor)`, fonction pure appelée par `_effectStage.Emit` (au rang de la source
  en cours : E1 peut venir de `RunPendingEventTriggers`, rang 2) ; `CreateNow` n'est pas utilisé.
- `AlundraBeannoideAi` : A1, C1, D1, enregistrés pour 335 ; D1 passe par `AlundraEnemyHelpers.HitReaction(e, 5, ctx)`.
- Oracle `docs/plan-e14-annexe/e14a/oracle-a1/` (copie du scratchpad `disc-e14a1/oracle-handlers/docs/plan-e14-annexe/e14a/oracle/`,
  sans `__pycache__`), sorties versées en texte (O-E14-6) ; lecteur JSON de test ; tests ; arc G116.

**Hors périmètre.** Le butin et son tirage (E20.e0, D-E14-3) ; le son 84 du butin ; `+0x1B8` et la passe des effets actifs (E20.e) ;
l'effacement du porteur `+0x2C` (E20.g) ; le Muruta (E14.a1c) ; l'avance du générateur par tick (E20.e0b) ; le rendu du nuage au-delà
de l'état du réservoir (l'ordre de dessin est celui d'E20.f1b) ; un ennemi posé sur une entité (l'oracle n'en a aucun : limite notée).

**Table des champs** (Beannoïde A1, C1, D1, E1 et `DestroyEntity` ; les lignes d'E14.a1c sont marquées). Adresses : instructions de
`ALUN_CD.EXE` (France). Lignes de la DLL : `Alundra/Scripts/AlundraEntityScriptProxy.cs` sauf mention ; à relire au HEAD de
l'exécution. Conversions : `S(v)` identité en `int` ; `U(v)` = `unchecked((uint)v)` ; `H(v)` = `unchecked((int)(v − 1))` (ADR-0026,
E14.a0b). « Entrée » = posé avant l'appel ; « Comparé » = comparé après.

| Champ | Nom de l'oracle | Lu / écrit par (adresse) | Membre de la DLL | Conversion | Traitement |
|---|---|---|---|---|---|
| `+0x10` | `status` | É : DestroyEntity `0x8003A5BC` (4) | `Status` | `(EntityStatus)v` (vecteurs : 2 ; scénarios : `before`, E1 : 3) | Entrée ; comparé (E1 : `FlagToDestroy`) |
| `+0x14` | `hp` | L `0x8003A3B8`, É `0x8003A528` (ComputeNewHp) | `Hp` | S | Entrée (D1) ; comparé ; `HpMax` jamais touché |
| `+0x28` | `platform_28` | L : DestroyEntity `0x8003A61C` | `PlatformEntity` | 0 → null ; ≠ 0 → `Entity` nue | E1 : entrée ; comparé inchangé (même référence) |
| porteur `+0x2C` | — | É : DestroyEntity `0x8003A62C` | `CarriedEntity` du porteur | — | **Exclu** (E20.g ; absent du JSON) |
| `+0x3C` | `contents_3C` | L : butin `0x80032BA4` | `ContentsItemId` | `(uint)v` | Entrée ; le bouchon l'ignore ; comparé inchangé |
| `+0x40` | (non décodé) | L : butin `0x80032C4C` | `ContentsGameFlag` | — | **Exclu** (butin) |
| `+0x64` | `hdr_64` | L `0x8003A5E0`, octet `+0x1E` `0x8003A5E8` | `BreakEffect` (nouveau, T1) | `inputs.fx` sinon 8 (les deux espèces valent 8) | Entrée ; le pointeur n'est pas porté |
| `+0x6C` | `flags6C` | L `0x800661CC`, É `0x800661DC` (C1) | `Flags` | `U(v)` | Entrée ; comparé (seulement `|= 0x40`) |
| `+0x88` | `anim_target88` | L : C1 `0x80065EEC` ; É : A1 `0x80061760`, C1 ×8, D1 `0x8007D9C8` | `TargetAnimationId` | `U(v)` (domaine −1..255) | Entrée ; comparé |
| `+0x8C` | `dir_target8C` | L : C1 `0x800660B4` ; É : C1 ×4, D1 `0x8007D9F0` | `TargetDirection` | `U(v)` (0..31) | Entrée ; comparé |
| `+0x98` | `animdir98` | non touché par A1, C1, D1, E1 | `BoxDirection` (`HitPass.cs`), jamais `AnimationDirection` | S (−1..4) | E14.a1c (C10 : `lbu` `0x8006841C`, `lw` `0x800805E0`) |
| `+0xAC` | `anim_end_AC` | L : C1 `0x80066184`, `0x800661AC`, `0x800661E8` | `ForceResetAnimationFlag` | S ({0,1}) | Entrée |
| `+0xB8`, `+0xCC`, `+0xD0`, `+0xDC` | `forcez_B8`, (non décodés) | jamais touchés | `ForceZ`, `PreviousAdjustedForceX/Y`, `AdjustedForceX` | — | marqueur posé, comparé inchangé |
| `+0xBC`, `+0xC0`, `+0xC4`, `+0xC8`, `+0xD4`, `+0xD8` | `tforcex_BC`, `tforcey_C0`, `forcex_C4`, `forcey_C8`, `fstepx_D4`, `fstepy_D8` | É : C1 `0x800660C8`-`0x800660DC` (toujours 0) | `TargetForceX/Y`, `ForceX/Y`, `ForceStepX/Y` | S | Comparé ; valeur initiale : `changed[].before` du mot changé, sinon le marqueur |
| `+0x114`, `+0x118` | `posx`, `posy` | L : C1 `0x80065F94/F9C` ; D1 `0x8007D9D4/DC` ; effet `0x8003BEBC/C8` | `PosX`, `PosY` | S | Entrée |
| `+0x11C` | `posz` | L : effet `0x8003BED4` ; A0 `lh +0x11E` `0x8006174C` (E14.a1c) | `PosZ` | `H(v)` ; Z de l'effet = `PosZ + 1` | Entrée (lire `entity_before`, jamais `inputs.posz`) |
| `+0x120`, `+0x124` | `tilex_120`, `tiley_124` | L : position relative `0x8007FE28`-`0x8007FE34` | `TileX`, `TileY` | S | Entrée |
| `+0x134` | `floor_134` | L `0x8007FE38/3C` | `FloorHeight` | `H(v)` (héros aussi) | Entrée |
| `+0x13C` | `forceadj_13C` | L : C1 `0x8006607C` | `ForceAdjusted` | S ({0,1}) | Entrée |
| `+0x144` | `onground_144` | L : C1 `0x80066200` | `IsOnGround` | S ({0,1}) | Entrée |
| `+0x158`-`+0x164` | `mh0..3` | C10 seulement | `MapHeights[0..3]` | `H(v)` | E14.a1c |
| `+0x1B8` | `activeeffect_1B8` | L `0x8003A5C0`, É `0x8003A5D4` ; aussi É par `UpdateActiveEffects` `0x80038F5C` | aucun (passe vide jusqu'à E20.e) | — | **Exclu** ; bouchon nommé à l'étape (4) |
| `+0x1C4` | (non décodé) | L : ComputeNewHp `0x8003A3B4` | `BalanceRecord` (`HitPass.cs`) | `AlundraBalanceTables.Select(335, 0)` (`level_D` = 0) | Entrée dérivée |
| `+0x1C8` de l'attaquant | `attacker_attack` (vecteurs) | L : ComputeNewHp `0x8003A3BC` | `CurrentAttack` du proxy de l'attaquant | `new AlundraBalanceAttack(attr, power)` ; scénarios : `(0x81, 1)` | Entrée |
| `+0x224` | `touching_224` | L : ComputeNewHp `0x8003A39C` | `TouchingEntity` | 0 → null ; pointeur du héros → `Entity` du héros | Entrée |
| `+0x228` | `letter_228` | É : DestroyEntity `0x8003A5CC` (−1) | `EventTrigger` | S | Comparé −1 (la surcharge à deux arguments doit l'écrire) |
| `+0x274` | `b0_274` | C10 | `Bytes[0]` | `(byte)v` | E14.a1c |
| `+0x275` | `b1_275` | L `0x80065F78` ; É `0x80065FC0`, `0x800661A8` (C1) | `Bytes[1]` | `(byte)v` | Entrée ; comparé |
| `+0x277` | `b3_277` | L : C1 `0x800661BC` ; É : D1 `0x8007D9C0` | `Bytes[3]` | `(byte)v` | Entrée ; comparé |
| `+0x280` | `ai0_280` | É : A0 `0x80061754` | `AIValues[0]` | `unchecked((short)v)` ; comparé en `(ushort)` | E14.a1c |
| `+0x282` | `ai1_282` | L/É : C1 (`0x80065F1C`, `0x8006600C`, `0x80066060`, …) | `AIValues[1]` | `unchecked((short)v)` ; comparé en `(ushort)` (le décrément boucle) | Entrée ; comparé |
| héros `+0x114`, `+0x118`, `+0x120`, `+0x124`, `+0x134` | `hero_before.*`, `calls[].hero.*` | L : C1 `0x80065F90/F98` ; D1 `0x8007D9D8/E0` ; position relative | `PlayerEntity` → `PosX`, `PosY`, `TileX`, `TileY`, `FloorHeight` | `H` pour le sol | Entrée |
| héros `+0x1C8`, `+0x1D4` | `curattack_1C8`, `box_1D4`, `hero_attack` | C10 `0x80068484`, `0x80068474`, `lbu` `0x80068494` | `CurrentAttack`, `HasFrameBox` (lire le champ, jamais `EvaluateFrameBox`) | pointeur ≠ 0 ⇔ non nul | E14.a1c |
| `0x80098708` | `rng_in`, `rng_out`, `draws` | C1 (LCG en ligne), `StartWalk` | `AlundraRandom.RandomSeed` | `RandomSeed = rng_in` ; comparé `(uint)RandomSeed == rng_out` | Entrée ; comparé |
| `0x80146DF8` | `latch` | L : ResolveBalanceTarget `0x800446DC` | `context.HeroAnimationLatch` | `U(latch)` | Entrée (D) |
| `0x80146E00`-`0x80146E10` | absent du JSON (« kit ») | L `0x800446F0`… | `context.EquipmentRecords` | voir T4 | Entrée (D) |
| `0x800DC058` | — | L `0x8003A3D0` (branches de `printf` de débogage) | aucun | — | non porté ; l'oracle tient 0 |
| réserve d'effets (`0x80138608`, 128 emplacements) | journal `effect_3BE74 [0, id, 0, ancre]` | É : `0x8003BE74` | `AlundraEffectSlot` | voir la projection d'E1 | Comparé |
| `v0` | `v0` | registre incident | — | — | jamais comparé |

Clôture : les noms changés sont, pour le Beannoïde, `activeeffect_1B8, ai1_282, anim_target88, b1_275, b3_277, dir_target8C, flags6C,
forcex_C4, forcey_C8, fstepx_D4, fstepy_D8, hp, letter_228, status, tforcex_BC, tforcey_C0` (16 ; le Muruta ajoute `ai0_280` et
`b0_274`) ; `hero_changed_words` est vide partout (les gestionnaires n'écrivent jamais le héros).

**Projection d'E1** (identique pour les deux espèces ; E14.a1b fait les 144 vecteurs du Beannoïde et ses 4 appels de scénario).
Ordre reproduit dans `DestroyEntity(e, fx)` : (1) bouchon du butin, (2) `Status = FlagToDestroy`, (3) `EventTrigger = -1`, (4) bouchon de
l'effet attaché, (5) `fx = -1` → `BreakEffect`, création si `fx != 0`, (6) porteur : E20.g.
- **Comparé** sur les 144 vecteurs et les 4 appels : C1 `Status == FlagToDestroy` ; C2 `EventTrigger == -1` ; C3
  `(uint)RandomSeed == rng_in` (0 tirage) ; C4 aucun autre membre du proxy ne change (`ContentsItemId`, `PlatformEntity` même
  référence, `Hp`, `Flags`, forces) ; C5 effet de bris : aucun emplacement si l'effet résolu vaut 0, sinon exactement un : le plus bas
  libre (réserve vide → 0 ; 3 occupés → 3), `Active`, `RecordIndex = -1`, `TargetEffect` = effet résolu (8, ou 12 quand
  `inputs.fx = 12`), table globale, animation 0, ancre = l'ancre figée, X = `PosX`, Y = `PosY`, Z = `PosZ + 1`, décalages 0,
  `DepthOffset` 1 (arguments de pile du binaire) ; **mode 1 lu à l'application de la file sans mise à jour** (`RunFrame` à 0 tick),
  **mode 2 après la première mise à jour** (ancre au statut 4), position et clé de l'ancre recopiées ; réserve pleine (128) → rien, statut et `EventTrigger` posés quand même ; C6 héros
  inchangé ; C7 épingles négatives (D-E14-3) : aucun son 84, aucune entité créée.
- **Exclu** (motif ; vecteurs par espèce) : X1 `spawn_39F58` (bouchon du butin, D-E14-3 ; 80) ; X2 le son 84 et ce qu'écrit le butin
  (lié au butin ; 80) ; X3 l'effacement de `+0x1B8` et l'emplacement qu'il libère (passe des effets actifs vide jusqu'à E20.e ; 72,
  `ae = 1`) ; X4 l'écriture du porteur `[+0x28] + 0x2C` (E20.g, absente du JSON ; 72, `plat = 1`) ; X5 le pointeur `hdr_64` (remplacé par
  `BreakEffect`) ; X6 `v0`, le 8e mot périmé des arguments du butin, et le veto de PM (constante du monde).
- **Comptes** (par espèce) : 144 vecteurs comparés sur 144 ; 160 entrées de journal exclues (80 butins + 80 sons) ; 108 effets comparés
  (72 × effet 8, 36 × effet 12) ; 16 vecteurs entièrement comparables (sans butin, `ae = 0`, `plat = 0`), dont 12 avec un effet ;
  6 comportements observables distincts (`plat`, effet ∈ {aucun, 8, 12}). Scénarios du Beannoïde : 4 appels E1, tous comparés.
- **Appel d'E1 dans les tests** : poser `EventTrigger = 4`, `ProgramIndexes[4] = 0`, `SpriteProgramIndexes[4] = 1`, puis passer par
  `RunPickedEvent` (le vrai chemin) ; un test direct de `DestroyEntity(e, -1)` est permis en plus. Dans les deux cas, `EventTrigger`
  vaut −1 après.
- **Chemins `fx = 6` (pente) et `fx = -1` (VRAM)** : mêmes instructions (`0x8003A5D8`-`0x8003A5F0`) ; leurs tests sont construits dans
  la DLL, pas tirés de l'oracle, et le disent.

**Lecteur de l'oracle** : `internal static class AlundraEnemyHandlerOracle` dans `Alundra.Tests/AlundraEnemyHandlerOracle.cs` ; dossier
`docs/plan-e14-annexe/e14a/oracle-a1/out` trouvé en remontant depuis `AppContext.BaseDirectory` (comme `AlundraEnemyOracle.cs` pour
`oracle/out`). `JsonDocument.Parse` une fois par fichier ; tous les nombres lus par `GetInt64()` puis convertis (pointeurs et mots du
générateur dépassent `Int32`) ; enregistrement de vecteur : `handler, rng_in, rng_out, draws, latch, v0` (ignoré), `entity_before`
(27 noms), `hero_before` (7), `hero_attack`, `attacker_attack`, `changed` [(nom, avant, après)], `journal` [(type, arguments)], et de
`inputs` seulement `fx`, `plat`, `ae` ; scénario : colonnes `tick, hold, rng, latch, hero{53 noms}, targets[]`, `meta`, et `calls[]`
(`tick, handler, slot, letter, rng_in, rng_out, latch, v0, before, after, hero{atk, box, floor, posx, posy, tilex, tiley}, changed,
changed_words, hero_changed_words, journal`) ; seul `calls[].journal` sert à la comparaison (le journal par tick contient d'autres
sons et effets). Auto-contrôles du lecteur : 2 052 vecteurs = A1 3 + C1 1 185 + D1 720 + E1 144 ; 3 140 appels = A1 12 + C1 3 102 +
D1 22 + E1 4 ; 21 fichiers de scénario ; chaque nom changé dans l'ensemble de clôture de l'espèce. Les comptes sont des appels, pas des
entrées distinctes (2 405 distinctes sur 3 140 pour le Beannoïde).

##### Tâches

- ✅ **T0 — Oracle** (fait par la session principale le 2026-10-10 : dossier copié, `notes.md` écrit, deux passes depuis le dépôt,
  sortie 0, `out/summary.txt` tout PASS, `out/manifest.txt` égal à l'original du scratchpad aux deux passes, aucun cache Python)
  (commit de docs ; si une passe dépasse le plafond d'une commande au premier plan, l'exécuteur rend la commande
  exacte, le dossier et l'environnement à la session principale, qui la lance en arrière-plan). Copier le dossier de l'oracle vers `docs/plan-e14-annexe/e14a/oracle-a1/` (sans `__pycache__`) avec
  un `notes.md` (réel, bouchons, harnais, scénarios, limites : reprendre `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e14a1/disc_oh.md` et la projection
  d'E1 ci-dessus) ; lancer deux fois `python -B docs/plan-e14-annexe/e14a/oracle-a1/run_e14.py` (environ 500 s) : sortie 0,
  `out/summary.txt` tout PASS, `out/manifest.txt` égal à celui du scratchpad (74 fichiers).
- ✅ **T1 — `BreakEffect` dans la DLL.** Tests d'abord : aller-retour du catalogue ; clé absente → 0 ; l'apparition copie la valeur ;
  `Clone` la copie ; épingles sur l'export réel : les préfabs du Muruta (`e3a39ec4-…`) et du Beannoïde (`1ef8f16c-…`) valent 8.
- ✅ **T2 — Ancre figée.** Un instantané immuable (statut 4, X, Y, `PosZ + 1`, clé d'E20.f1b) implémentant `IAlundraEffectAnchor` ; test :
  dans une image de 2 puis 3 ticks, l'emplacement garde la position et la clé de l'entité détruite après son recyclage.
- ✅ **T3 — `DestroyEntity(e, fx)`.** Tests d'abord : la projection d'E1 (C1 à C7) sur un cas construit pour chaque branche ; octet 0 →
  rien ; pente → effet 6 ; VRAM (`fx = -1`) → octet de l'en-tête ; réserve pleine → rien, statut et `EventTrigger` posés ; images de 1, 2 et
  3 ticks → même emplacement ; aucun tirage ; `ContentsItemId` non nul → aucun son 84, aucune entité créée (bouchon nommé du butin) ;
  `EventTrigger = -1` écrit par la surcharge à deux arguments. Puis le code, dans l'ordre du binaire.
- ✅ **T4 — Beannoïde A1, C1, D1 et E1 contre l'oracle.** Le lecteur ci-dessus ; une classe de tests
  `AlundraBeannoideOracleTests` dans `[Collection(AlundraRandomStaticStateCollection.Name)]` (graine rendue) qui, pour chaque vecteur et
  chaque appel `BN-*` et `ARENA-*` : pose l'entité, le héros et l'attaquant selon la table des champs, pose `RandomSeed`, appelle le
  gestionnaire par le registre (A1, C1, D1) ou E1 par `RunPickedEvent`, puis compare les champs changés, le mot du générateur et le
  journal (sons, effet) selon la projection. Montage de D1 : tables par `AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find())` et
  `AlundraItemTables.GetOrCreate(...)` ; kit d'équipement par `new AlundraGameState()`, `AlundraPlayerManager.InitializeNewGameInventory`
  et `FillEquipmentRecords` (idiome d'`AlundraDamageTests`), vérifié d'abord contre `kit.meta.equipment` de
  `docs/plan-e20-annexe/e20-c1b/oracle/out/K2.json` (« [0] item 17 [], [1] item 1 [(1, 1)], [2] null ») ; héros : un proxy pour
  `PlayerEntity` (positions) et l'`Entity` du toucheur dont le proxy porte `CurrentAttack` (précédent `AlundraEnemyHitReactionTests`) ;
  verrou `context.HeroAnimationLatch = U(latch)`. Sans héros (`PlayerEntity` nul, absent de l'oracle) : no-op documenté, comme
  `HitReaction`. Contrôles venus d'E14.a0b : aucune inscription ne liste le type −1 ; le test du verrou côté monde distingue
  `=> HeroAnimationLatch` de `=> 0` (verrou non nul, par D1).
- ✅ **T5 — Tests qui bougent, arc G116, suite complète** (§1.3d).
- ✅ **T6 — Plan, notes** (résultats, écarts, limites ; faite, voir « Résultats » ci-dessous) ; le verifier frais est lancé par la session principale.

**Arc G116** (`ArcRun` direct, natifs actifs). `LeverArcs.Zone` / `World116`, sans drapeaux, `Arrival` (84, 328, z 0) — la case
(3,20), qui fait apparaître les enregistrements 0, 1, 17 et 41 à 44 (l'arrivée en (444,328) n'en fait apparaître aucun : la zone
d'apparition des enregistrements 0 et 1 est X 0..7, Y 0..29) —, limite d'environ 3 000 images, `Give(1)`, `Equip(1)`, puis
`PlaceHero` sur le plateau (z 160). Politique d'épée copiée de `Swordsman` (`oracle-a1/e14run.py`) : cible frappable si statut 2,
`IsOnGround`, `FrameCollisionTickCounter` 0 et animation sans le bit `0x40` ; héros posé à 40/34/28/22/16 px à l'ouest, au premier
endroit dont les quatre coins (décalage (−10, −7), taille (21, 15)) sont marchables à la hauteur de la cible (`LeverArcs.CellAt`) ;
direction 24 (est), `Square`, 25 ticks entre deux appuis.

##### Acceptation

Une image = un tick.
- Vecteurs : 2 052 (A1 3, C1 1 185, D1 720, E1 144), 0 écart sur les champs changés, le mot du générateur et les sons d'A1, C1 et D1 ;
  E1 selon la projection (144 comparés, 108 effets, exclusions X1 à X6).
- Appels des scénarios : 3 140 (A1 12, C1 3 102, D1 22, E1 4), rejoués un par un d'`before` vers `after`, 0 écart (E1 selon la
  projection ; D1 avec l'attaque `(0x81, 1)` au verrou 16).
- Arc G116 : 10 appels de D1, 5 par Beannoïde, PV 9→7→5→3→1→0 ; pour chaque mort : D à H+1, statut 4 à H+23, `EntityByRecord` nul après
  H+24 ; `FrameOf(C, 323)` = E1 le plus tardif + 1 ; enregistrement 6 : `TargetAnimationId` 3 ; enregistrements 17 et 41 à 44 vivants ;
  deux effets de bris : à l'image qui suit chaque E1 (E1 + 1), relevé des emplacements `Active` avec `TargetEffect == 8`,
  `RecordIndex == -1`, `Mode ∈ {1, 2}` (2 attendu après la première mise à jour), `Anchor` non nul, et X/Y de l'emplacement égaux à
  `PosX`/`PosY` du Beannoïde tué relevés au tick de son E1 : exactement un tel emplacement par mort (relevé fait avant la fin de
  l'effet 8, qui se libère après sa durée) ; `AssertNoUnexpectedError`. Limite écrite : l'arc tourne hors de la collection du
  générateur statique et n'affirme que des issues indépendantes du tirage. Ne s'affirme pas : le butin, le son 84, les trajectoires qui dépendent du générateur après l'apparition
  (la DLL ne tire rien à l'apparition, son flot diffère de celui de l'oracle).
- Mutations réelles, par script, chacune rouge : C1 portée `slti` 3→4 ; D1 animation 5→6 ; ancre vivante au lieu de l'ancre figée (image
  de 2 ticks) ; `fx -1` non résolu ; `EventTrigger` non écrit par la surcharge à deux arguments.
- Registre : 3 entrées en production ; les tests du §1.3d mis à jour ; aucune inscription ne liste −1.
- Suite `Alundra.Tests` en Release puis en Debug sans échec ; `cmp` de la DLL ; traces inchangées ; verifier frais.

##### Résultats (2026-10-10)

Commits : T1 `3cb5920`, T2 `6be996a`, T3 `589c664`, T4 `6b62cf9`, T5 `5236937`, T6 (ce texte). Build Release puis Debug : `Alundra.Tests` **4736 sans échec** dans les deux configurations (4694 avant : +42 tests),
`cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique, garde des traces `docs/hero-trace-389-*` et `docs/intro-trace-389.txt` passée (`git diff --ignore-cr-at-eol --exit-code`).

**Ce qui est fait.** `BreakEffect` dans l'en-tête, le catalogue, le proxy et son clone (T1) ; `AlundraFrozenEffectAnchor` (instantané statut 4, X, Y, `PosZ + 1`, clé d'E20.f1b, coordonnée de tri) et
`AlundraNativeEffects.CreateBreakEffect` (T2) ; `AlundraWorldProxy.DestroyEntity(e, fx)` dans l'ordre du binaire pour tous les appelants : bouchons nommés du butin (E20.e0), de l'effet attaché `+0x1B8` (E20.e) et de
l'effacement du porteur (E20.g), statut 4, `EventTrigger = -1`, résolution de `fx = -1` par `BreakEffect`, création de l'effet en file au rang de la source (T3) ; `AlundraBeannoideAi` (A1, C1, D1 inscrits pour le type 335 par
`AlundraNativeSpriteRegistry.CreateForProduction`), le lecteur `AlundraEnemyHandlerOracle`, `AlundraBeannoideOracleTests` (T4) ; les quatre tests du registre, l'arc G116 et la suite complète (T5).

**Acceptation, mesurée.**
- Vecteurs : **2 052, 0 écart** : A1 3, C1 1 185, D1 720 (champs changés, mot du générateur, journal vide : aucun son) ; E1 144 selon la projection : C1 à C7 comparés sur les 144, **108 effets** comparés (60 avec butin et son
  exclus, 48 sans), **160 entrées de journal exclues** (80 butins X1 + 80 sons X2), mode 1 lu à l'application de la file sans mise à jour (`RunFrame` à 0 tick), mode 2 après la première mise à jour, emplacement le plus bas libre
  (0 ou 3 occupés, alternés), le tout par `RunPickedEvent` (le vrai chemin) ; les comptes de l'oracle sont vérifiés par un test du lecteur (2 052 vecteurs, 21 scénarios, 3 140 appels).
- Appels des scénarios : **3 140, 0 écart**, rejoués un par un d'`before` vers `after` : A1 12, C1 3 102, D1 22 (attaque `(0x81, 1)`, verrou de l'appel, kit d'équipement vérifié contre `kit.meta.equipment` de K2.json), E1 4.
  Aucun nom changé hors de l'ensemble de clôture (16 noms).
- Arc G116 (`AlundraBeannoideGrateArcTests`, un tick par image) : 10 appels de D1, 5 par Beannoïde, PV 9, 7, 5, 3, 1, 0. Enregistrement 0 : coups écrits aux ticks 3, 35, 67, 99, 131, D1 à H + 1 (4, 36, 68, 100, 132), **statut 4 à H + 23 = 154**,
  recyclé à **H + 24 = 155**. Enregistrement 1 : coups aux ticks 99, 131, 163, 195, 227 (les appuis des ticks 95 et 127 touchent les deux), D1 à 100 ... 228, **statut 4 à 250** (H + 23), recyclé à **251** (H + 24).
  `FrameOf(C, 323)` = **251 = E1 le plus tardif + 1** ; enregistrement 6 : `TargetAnimationId` 0 jusqu'à la porte, **3** à partir du tick 251 (et jusqu'au moins le tick 290) ; enregistrements 17 et 41 à 44 vivants ;
  **deux effets de bris** : à E1 + 1 (155 et 251), exactement un emplacement `Active` d'effet 8, `RecordIndex` −1, mode 2 (le premier tick de la mise à jour l'a gelé), ancre non nulle, X et Y égaux à ceux du Beannoïde au tick de son
  E1 (emplacements 6 puis 3) ; `AssertNoUnexpectedError`. Le décalage H + 1 / H + 23 / H + 24 est celui de l'oracle : aucun écart à diagnostiquer. Limite écrite : l'arc tourne hors de la collection du générateur et n'affirme que des
  issues indépendantes du tirage ; ni butin, ni son 84.
- Registre : **3 entrées en production** ; les quatre tests de §1.3d mis à jour (voir ci-dessous) ; aucune inscription ne liste −1 (test des 6 emplacements × 400 codes).
- Traces, arcs des leviers, arène de c1a et c1b, `AlundraDay3SceneArcTests`, `AlundraInoaDayOneArcTests`, `AlundraShipBlockArcTests`, `AlundraVisionArcTests`, `AlundraHeroLifeArcTests`, `AlundraEntityContactArcTests`,
  `AlundraTarnMurggArcTests` : **aucun ne bouge** (suite complète sans échec, aucune attribution à faire). `AlundraTarnMurggArcTests` R3-2 ne bouge pas : O-E14-2 se ferme (voir la section 2).

**Les quatre tests du registre.** `ProductionRunner_RegistryIsEmpty_AndTheSwitchIsOn` devient `ProductionRunner_RegistryHoldsTheBeannoidsThreeHandlers_AndTheSwitchIsOn` (`Count` 3, les trois clés pour 335, pas pour 347) ;
`RegisteredEntry_ListingSeveralTypes_RunsForEach` passe à la clé libre (D, 25) ; `Registering_TheSameKeyTwice_Throws_AndDisposingRemovesTheEntry` compare le `Count` à celui d'avant l'enregistrement ;
`EmptyRegistry_EveryEntity_IsACountedNoOp` ne bouge pas en E14.a1b (ses clés (C, 25) et (D, 9) ne sont pas inscrites en production ; E14.a1c le fera). Un test ajouté : `ProductionRunner_NoRegistrationListsTheTypeMinus1`.
Contrôle venu d'E14.a0b : le test du verrou côté monde distingue maintenant `=> HeroAnimationLatch` de `=> 0` (un coup d'épée donne un verrou non nul).

**Mutations réelles** (appliquées au fichier, construites, testées, restaurées par la copie d'origine ; aucune n'est dans un commit) :
- C1 portée `slti` 3 → 4 : 2 rouges (`TheVectors_OfTheRegistryHandlers_HaveNoDifference("C1")`, `TheCallsOfTheScenarios_...("C1")`) ;
- D1 animation 5 → 6 : 3 rouges (vecteurs D1, appels D1, `D1_AppliesTheLatchOfTheContext_ToTheEquipmentTerm`) ;
- ancre vivante au lieu de l'ancre figée : 3 rouges (`TheEffect_TakesTheSameSlotAndTheSameState_InAFrameOfOneTwoOrThreeTicks`, images de 1, 2 et 3 ticks) ;
- `fx = -1` non résolu : 10 rouges (E1 vecteurs et scénarios, octet 0, E1 par `RunPickedEvent`, images de 2 et 3 ticks, pente et VRAM, `TakeTheByteOfTheHeader`, sans tirage) ;
- `EventTrigger` non écrit par la surcharge à deux arguments : 2 rouges (`TheTwoArgumentOverload_WritesTheStatus4_AndTheLetterMinus1`, `AFullReservoir_...`) ; les tests d'E1 par `RunPickedEvent` restent verts, comme prévu (le vrai chemin réécrit −1) ;
- hors liste : enregistrements de production retirés : 7 rouges (les tests du registre) ; `IEntityWorldContext.HeroAnimationLatch => 0` : 1 rouge.
Écart d'ordre « tests d'abord » pour T4 : le code d'`AlundraBeannoideAi` a été écrit avant le fichier des tests d'oracle ; la preuve que les tests mordent est la mutation « enregistrements de production retirés » (7 rouges sans les gestionnaires).

**Écarts et limites.**
1. **`AlundraBalanceRecord.ByteAt` au-delà de l'enregistrement** (hors du périmètre écrit, nécessaire à l'acceptation) : 60 des 720 vecteurs de D1 portent l'attribut d'attaque `15` (octet `2 + 15 = 17` de la fiche). La fiche du Beannoïde fait
   17 octets : le binaire lit alors le premier octet de l'enregistrement suivant du fichier (`BALANCE.BIN`), la DLL renvoyait 0 (« la donnée n'a pas une telle attaque, la lecture est exacte » : faux au-delà de la taille de la fiche). Corrigé :
   `AlundraBalanceTables` relie chaque fiche à la suivante par le `FileOffset` exporté (613 fiches contiguës, une seule sans suivante), `ByteAt(offset >= RecordSize)` lit la suivante ; une fiche écrite à la main (sans suivante) lit 0 comme
   avant. Test `AByteBeyondTheRecord_IsReadFromTheNextRecordOfTheFile`. Aucun attribut réel du jeu n'a ce quartet (non vérifié sur les 512 fiches : non nécessaire à l'acceptation).
2. `AlundraEnemyHelpers.ZeroForces` ajoutée (les six mots que C1 et `HandleAnimationDirection` remettent à zéro) ; `Turn` l'utilise : même comportement, les tests d'E14.a0b restent verts.
3. Sans héros (`PlayerEntity` nul), C1 ne fait rien et D1 applique les dégâts et l'animation sans la direction (comme `HitReaction`) : absent de l'oracle, noté.
4. Un ennemi posé sur une entité (support par une autre entité dans `ComputeFloorHeight`, P4 d'E14.a0b) : l'oracle n'en offre aucun (`platform_28` n'existe que dans les 72 vecteurs d'E1 avec `plat = 1`, où il sert au porteur, E20.g) : **limite notée**, pas de vecteur.
5. Le butin et son son 84 (E20.e0), `+0x1B8` (E20.e), le porteur (E20.g) restent des bouchons nommés dans `DestroyEntity` ; la levée de clé par voisin ou porteur reste l'écart écrit d'`AlundraEntityDepthKey`.
6. Commentaires périmés d'E20.f1b corrigés dans les commits des tâches qui touchent les fichiers : `AlundraEffectReservoir.cs` (T3), `AlundraPlayerManager.cs` et `AlundraHeroAuraWorldTests.cs` (T5, ticks 129 à 131).
7. L'arc G116 tourne hors de la collection du générateur statique : d'autres classes de tests qui tirent des nombres en parallèle peuvent entrelacer le flux ; les assertions n'en dépendent pas (deux suites complètes, Release et Debug, vertes).

**Vérification (2026-10-10) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre). Rejeu : `Alundra.Tests` 4736/4736 en Release
et en Debug, `cmp`, traces ; chaque point de l'acceptation relu contre les tests ; mutations rejouées sur une copie (`EventTrigger` non écrit 2
rouges, C1 `slti` 2, D1 animation 4, ancre vivante 3, `fx -1` non résolu 11 : l'arc compte aussi). Adversarial : suites complètes 4736/4736 ;
les trois appelants de la surcharge à deux arguments (E1, pente `fx 6`, VRAM `fx -1`) et l'ordre du binaire dans `DestroyEntity` ; mots changés
bruts des appels `BN-*`/`ARENA-*` tous dans la clôture, `hero_changed_words` vide ; collections de tests non parallèles
(`parallelizeTestCollections: false`) ; registre limité au type 335 ; aucun arc existant ne bouge. Remarques P4, dispositions de la session
principale :
- **Écart 1 (`ByteAt` au-delà de la fiche) : accepté.** Fidèle au binaire, exigé par 60 vecteurs D1 ; mesuré par le verifier sur
  `alundra-project/Data/balance.json` : quartets d'attribut 0 à 11 seulement et fiches de 15 octets au moins, donc aucune lecture au-delà
  d'une fiche avec les données réelles : aucun effet en production. Code d'E20.c1b touché : consigné ici et au plan d'E20 (passe de coups).
- Commentaires qui contredisent maintenant `DestroyEntity(e, fx)` (`EntityFlags.cs` « sans effet de bris » pour la destruction VRAM,
  `AlundraEventProgramRunner.cs` « destruction seulement » pour E1, `AlundraEntityDepthKey.cs` « later », `IEntityWorldContext.cs` et
  `IAlundraScriptHost.cs` qui ne citent que la phase de choix) : **reportés à E14.a1c**.
- L'arc G116 déduit les 10 appels de D1 des baisses de PV au lieu de les compter : **reporté** ; E14.a1c compte ses appels de D9 directement.
- Le test des images de 2 et 3 ticks de l'ancre simule le recyclage au lieu d'une vraie image de monde de plusieurs ticks : **limite notée**
  (l'arc réel, à un tick par image, montre un seul effet par mort, en mode 2, à la bonne position).
- L'ordre « tests d'abord » de T4 n'a pas été suivi (le code d'`AlundraBeannoideAi` avant le fichier des tests d'oracle) ; la morsure est
  prouvée (sans les inscriptions de production, 7 tests rouges) : **noté**.

##### Arrêts, budget, retour arrière

- **Arrêts** : un vecteur en écart après deux tentatives ; une épingle de l'arène c1a ou c1b qui bouge ; le décalage H+1/H+23/H+24 qui
  diffère dans la DLL (diagnostiquer, ne jamais déplacer l'épingle) ; un arc qui bouge sans attribution à un effet de bris légitime
  (octet non nul de l'entité détruite, lu dans l'export) ; un nom changé hors de l'ensemble de clôture ; un besoin de modifier le
  moteur ; un défaut de l'original soupçonné (l'auteur tranche).
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E14.a1b ; le dossier `oracle-a1/` (commit de docs de T0) peut rester, il ne change
  aucun comportement.

### 1.3c E14.a1c — Muruta, hall de la 115 🧪 (faite et CONFIRMED le 2026-10-10, `258eb53` à `d90b605` ; recette de l'auteur à venir)

Relecture : **READY** dès la première lecture (2026-10-10).

Détail écrit le 2026-10-10 sur le modèle d'E14.a1b (révision 2, READY), à partir des mêmes rapports de découverte
(`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e14a1/a1b_critic.md`, `a1b_table.md`, `a1b_projection.md`, `disc_da.md` §3, `disc_oh.md`) et de l'oracle
`docs/plan-e14-annexe/e14a/oracle-a1/` (versé par E14.a1b T0).

**But.** Le Muruta (type 347) vit comme dans `ALUN_CD.EXE` (A0, C10, D9 par le registre) ; le hall de la 115 devient jouable : quatre
Muruta tués à l'épée, la porte de B[2] passe, G569 est posé et trois portails sont rendus.

**Faits porteurs.**
- [binaire] A0 `0x8006174C` : `lh +0x11E` (moitié haute du Z du binaire) écrit `+0x280` (`0x80061754`) ; dans la DLL
  `AIValues[0] = (short)((PosZ + 1) >> 16)` (ADR-0026 : le `PosZ` de la DLL vaut celui du binaire moins 1 ; `PosZ >> 16` seul diffère
  quand les 16 bits bas de la DLL valent `0xFFFF`).
- [binaire] C10 `0x80068154` lit `+0x98` en deux largeurs : `lbu` en `0x8006841C` (direction `^ 1`, masquée `& 0xFF` par
  `TryAttackPlayer2`) et `lw` en `0x800805E0` (dans `TryAttackPlayerFront`) ; les deux sont `BoxDirection` (valeur du tick, D-E14-6),
  jamais `AnimationDirection` (même décalage par la disposition des champs). Domaine des vecteurs : −1..4.
- [binaire] C10 lit le mot BSS `[0x800DC5A0]` en `0x80068164` (test de la carte `0x99`) ; la branche `0x80068178`-`0x8006819C` (et la
  lecture de `+0x0C`) est morte pour 347, qu'aucun enregistrement de la 153 ne place : **non portée**, avec un commentaire.
- [binaire] C10 lit, sur le héros, `+0x1D4` (boîte de l'image, `0x80068474`), `+0x1C8` (attaque courante, `0x80068484`) et l'octet
  d'attribut de l'attaque (`lbu` `0x80068494`) ; un Muruta alerté dont le héros frappe (boîte et attribut non nuls) à 5 cases ou moins
  se fend aussitôt (animation 3), quel que soit le générateur.
- [DLL] `HasFrameBox` est rafraîchi pour chaque entité à la fin de chaque tick (`StoreHitBox` → `EvaluateFrameBox`) : C10 **lit le
  champ** ; appeler `EvaluateFrameBox` sur un proxy nu le remettrait à faux.
- [binaire] D9 `0x8007DE04` : `HitReaction(e, 2, ctx)` (animation 2, direction opposée au héros, `Bytes[3]`), `ComputeNewHp` ; carte 115
  `D = 0` : Muruta de niveau 1, PV 3, deux coups d'épée de 2 (le dernier 1), espacés d'au moins 25 ticks.
- [oracle] Appels C10 des scénarios : pointeur d'attaque du héros `0x80144229` (612 appels, attaque `(0x81, 1)`) ou `0x80144209`
  (3 025 appels, jamais déréférencé dans les ensembles de lecture) ; vecteurs : `hero_attack` explicite.
- [oracle] E1 : vecteurs identiques à ceux du Beannoïde hormis le générateur ; la projection d'E1 d'E14.a1b s'applique telle quelle ;
  7 appels E1 dans les scénarios `MU-*`, tous comparés.
- [données] Hall (`map_115.json`) : B[2] lié à MapEvents[1] (cases X 0..21, Y 34..57 ; réarmé si le héros sort) ; `@172` (G569 → fin),
  `@177` (G262 → `@381`), `@381`-`@396` (`0x54` sur (18,44), (19,44), (2,44), (3,44)), `@401`/`@403`/`@405`/`@407` (`0x2D` des
  enregistrements 24 à 27), `@409` son 55, `@413` `Break`, `@414`/`@419`/`@424`/`@429` (`0x2C` sur 24..27), `@434` (`0x05 [57,2]` =
  G569), sons et `Wait` jusqu'à `@493`/`@498`/`@503` (`0x54 [x,y,0,128]` : bit de portail `0x80` sur (10,44), (11,44), (5,42)) ; B[4]
  (`@684`) verrouille (10,44), (11,44), (5,42) et (16,42) à l'entrée quand G262 est posé ; (16,42) reste fermé (seul le chemin G659 le
  rouvre). Enregistrements 24 à 27 : sprite 91, type 347, cases (9,46), (12,46), (9,50), (12,50), `Height 16` (128 px au-dessus du sol
  du hall), C = 153 : `C[25]` (`@1464 2B`, `@1465 25`, `@1466 37[20]`, `@1468 2A`, `@1469 40 [2,0]`) passe au natif C10 après la chute ;
  D et E natifs dès le départ.
- [DLL, INCONNU] La chute de 128 px d'une entité autre que le héros passe par `StepVerticalWithoutController` (**corrigé le 2026-10-15
  par la découverte d'E20.e0k, `plan-e20-heros.md` §1.18** : les Muruta du hall tombent par le chemin du contrôleur ; la chute n'est pas
  un arrêt d'E14.a1c) ; sa durée dans la DLL
  n'est pas mesurée ; le pas vertical et le vol des entités sont E20.e0k (non fait) : d'où l'arrêt ci-dessous.

**Périmètre.** `AlundraMurutaAi` : A0, C10, D9, enregistrés pour 347 (D9 par `AlundraEnemyHelpers.HitReaction(e, 2, ctx)`) ; tests
contre l'oracle avec le lecteur d'E14.a1b (`AlundraEnemyHandlerOracle`) ; arc H115 ; tests du §1.3d pour E14.a1c (compte 6 ;
`EmptyRegistry_EveryEntity_IsACountedNoOp` sur une clé libre).
Reporté d'E14.a1b (commentaires seulement) : les commentaires qui contredisent maintenant `DestroyEntity(e, fx)`
(`EntityFlags.cs` « sans effet de bris » pour la destruction VRAM, `AlundraEventProgramRunner.cs` « destruction seulement » pour E1,
`AlundraEntityDepthKey.cs` « later », `IEntityWorldContext.cs` et `IAlundraScriptHost.cs` qui ne citent que la phase de choix).

**Hors périmètre.** Ceux d'E14.a1b (butin, son 84, `+0x1B8`, porteur, avance du générateur par tick, rendu au-delà du réservoir) ; la
branche de la carte `0x99` ; le chemin complet du hall avec sa scène (le raccourci `{262}` suffit) ; la chute des entités (E20.e0k) ;
les Muruta de la 362 et la vague de la 44 hors recette.

**Table des champs (ajouts à celle d'E14.a1b, qui reste valable).**

| Champ | Nom de l'oracle | Lu / écrit par (adresse) | Membre de la DLL | Conversion | Traitement |
|---|---|---|---|---|---|
| `+0x98` | `animdir98` | C10 `lbu` `0x8006841C`, `lw` `0x800805E0` | `BoxDirection` | S (−1..4) | Entrée |
| `+0x11C` (moitié haute `+0x11E`) | `posz` | A0 `lh` `0x8006174C` | `PosZ` | `H(v)` | Entrée (lire `entity_before`, jamais `inputs.posz`) |
| `+0x158`-`+0x164` | `mh0..3` | C10 (`HandleAnimationDirection` `0x80080150`-`0x80080164`) | `MapHeights[0..3]` | `H(v)` | Entrée |
| `+0x274` | `b0_274` | C10 | `Bytes[0]` | `(byte)v` | Entrée ; comparé |
| `+0x280` | `ai0_280` | É : A0 `0x80061754` | `AIValues[0]` | `unchecked((short)v)` ; comparé en `(ushort)` | Comparé |
| héros `+0x1C8` | `curattack_1C8`, `hero_attack` | C10 `0x80068484`, attribut `lbu` `0x80068494` | `CurrentAttack` du héros | pointeur ≠ 0 ⇔ non nul ; `0x80144229` → `(0x81, 1)` ; `0x80144209` → une attaque dont la lecture fait échouer le test (jamais lue) | Entrée |
| héros `+0x1D4` | `box_1D4`, `calls[].hero.box` | C10 `0x80068474` | `HasFrameBox` (le champ) | booléen | Entrée |
| `0x800DC5A0` | — | C10 `0x80068164` | aucun | — | non porté (branche morte) |

Clôture du Muruta : les 16 noms du Beannoïde plus `ai0_280` et `b0_274` (18).

##### Tâches

- ✅ **T1 — Muruta A0, C10, D9 contre l'oracle** (`258eb53`). Tests d'abord : `AlundraMurutaOracleTests` dans
  `[Collection(AlundraRandomStaticStateCollection.Name)]` (graine rendue), même montage qu'`AlundraBeannoideOracleTests` (table des
  champs, héros, attaquant, kit, verrou), sur `vectors-muruta.json` et les `calls[]` des scénarios `MU-*` ; puis `AlundraMurutaAi`
  enregistré pour 347 ; les tests du §1.3d pour E14.a1c.
- ✅ **T2 — Arc H115** (`f15a892`).
- ✅ **T3 — Suite complète, plan, notes** (faite, voir « Résultats » ci-dessous ; le verifier frais est lancé par la session principale).
- ⏳ **T4 — Recette de l'auteur** (D-E14-7) : le hall de la 115 (quatre Muruta tués à l'épée, un nuage de bris par mort, trois portails rendus, (16,42) reste fermé), la grille de la 116 (animée après les deux Beannoïde) et la 14 (les sept Beannoïde dorment, se réveillent et meurent) ; non tentée par l'exécuteur.

**Arc H115** (`ArcRun` direct, natifs actifs). `LeverArcs.Zone` / `World115`, drapeaux `{262}`, `Arrival` (252, 872) — la case (10,54),
z 0, direction 16 ; jamais la case (10,56), qui lance le dialogue 81 de B[8] (`@1104`) —, limite d'environ 3 000 images. Image 1 : B[2]
passe par `@172, @177, @381…, @401, @403, @405, @407`. `Give(1)`, `Equip(1)` ; attendre les quatre `FramesOf(C, 1469, 0x40)` (passage
au natif), chacun au plus 600 images après le `0x2D` de son Muruta. Politique d'épée d'E14.a1b (`Swordsman` : 40/34/28 px à l'ouest,
empreinte marchable à la hauteur de la cible, direction 24, `Square`, 25 ticks entre deux appuis). Le héros reste dans X 0..21,
Y 34..57 ; ne jamais tenir Haut sur (5,42), (10,44), (11,44), (16,42) (dialogue de B[5]).

##### Acceptation

Une image = un tick.
- Vecteurs : 4 913 (A0 6, C10 4 043, D9 720, E1 144), 0 écart sur les champs changés, le mot du générateur et les sons ; E1 selon la
  projection d'E14.a1b.
- Appels des scénarios : 3 675 (A0 16, C10 3 637, D9 15, E1 7), rejoués un par un, 0 écart (D9 avec l'attaque `(0x81, 1)` au verrou
  16).
- Arc H115 : 8 appels de D9, **comptés directement** (compteur ou journal des gestionnaires natifs, pas déduits des PV ; reporté
  d'E14.a1b), 2 dégâts par coup, PV 3→1→0 pour chaque Muruta ; pour chaque mort : D à H+1, statut 4 à H+23, recyclage à
  H+24 ; `FrameOf(B, 434)` = dernière E1 + 2 = X (la porte d'un événement de carte passe à E1 + 2) ; `@442` = X+41, `@461` = X+50,
  `@477` = X+59, `@493` = X+68 (`Wait N` dure N+1 images, épinglé par `AlundraLeverArcTests` A115) ; G569 et G262 posés ;
  `GroundProperty & 0x80` posé en (10,44), (11,44) et (5,42), éteint en (16,42) ; `EntityByRecord(24..27)` nul ; quatre effets de bris
  relevés à E1 + 1 avec le prédicat d'E14.a1b (`TargetEffect == 8`, `RecordIndex == -1`, `Mode ∈ {1, 2}`, `Anchor` non nul, X/Y du
  Muruta tué) ; `AssertNoUnexpectedError` et `AssertNothingSkippedOrExceeded`. Ne s'affirme pas : les positions des Muruta (le
  générateur décide de leur marche), le butin, le son 84.
- Mutations réelles, par script, chacune rouge : C10 masque de fin `0x1C`→`0x1F` ; D9 animation 2→3 ; A0 sans le +1 ; état 5 `0x40`→`0x20` ;
  C10 lisant `AnimationDirection` au lieu de `BoxDirection`.
- Registre : 6 entrées en production ; aucune inscription ne liste −1 ; tests du §1.3d à jour.
- Suite `Alundra.Tests` en Release puis en Debug sans échec ; `cmp` de la DLL ; traces inchangées ; verifier frais.

##### Résultats (2026-10-10)

Commits : T1 `258eb53`, T2 `f15a892`, T3 (ce texte). Build Release puis Debug : `Alundra.Tests` **4752 sans échec** dans les deux configurations (4736 avant : +16 tests : 14 de `AlundraMurutaOracleTests`, l'arc H115, le journal des
gestionnaires natifs), `cmp alundra-project/Alundra.dll Alundra/bin/Debug/net9.0-windows/Alundra.dll` identique, garde des traces `docs/hero-trace-389-*` et `docs/intro-trace-389.txt` passée (`git diff --ignore-cr-at-eol --exit-code`).

**Ce qui est fait.** `AlundraMurutaAi` (A0, C10, D9 inscrits pour le type 347 par `AlundraNativeSpriteRegistry.CreateForProduction`, six entrées en production) ; `AlundraEnemyHelpers.CardinalDirections` (table `0x80028B54`) ; le lecteur
`AlundraEnemyHandlerOracle` étendu (`MurutaClosure`, 18 noms, et le pointeur d'attaque du héros par appel) ; `AlundraMurutaOracleTests` ; `AlundraMurutaHallArcTests` ; `AlundraEventProgramRunner.NativeHandlerRunCounts` (journal des
gestionnaires natifs par (emplacement, code), pour compter D9 directement) ; les tests du registre (§1.3d) ; les commentaires reportés d'E14.a1b.

**Acceptation, mesurée.**
- Vecteurs : **4 913, 0 écart** : A0 6, C10 4 043, D9 720 (champs changés, mot du générateur, sons : le son `0xCD` de C10 est comparé dans l'ordre par un enregistreur) ; E1 144 selon la projection d'E14.a1b (144 comparés, 108 effets,
  160 entrées de journal exclues).
- Appels des scénarios : **3 675, 0 écart**, rejoués un par un d'`before` vers `after` : A0 16, C10 3 637, D9 15 (attaque `(0x81, 1)`, verrou de l'appel), E1 7. Aucun nom changé hors de la clôture de 18 noms.
- Arc H115 (`AlundraMurutaHallArcTests`, un tick par image ; relevés de la graine par défaut, la structure est la même pour cinq autres graines essayées à part, non versées) : les quatre `0x2D` (`@401`, `@403`, `@405`, `@407`) à l'image
  **0** de la trace ; les quatre `@1469` à l'image **44** (44 images après le `0x2D`, loin des 600 de l'arrêt : la chute des entités n'est pas un arrêt ici) ; coups écrits aux images 61, 89, 121, 149, 189, 221, 249 ; **8 appels de D9
  comptés par le journal des gestionnaires natifs** (pas déduits des PV), chacun relié à une baisse de PV du même tick ; PV 3→1→0 par Muruta (2 par coup). Enregistrement 24 : D9 à 66 et 154, H = 153, statut 4 à **176** (H+23),
  recyclé à **177** (H+24) ; 25 : D9 à 94 et 126, H = 125, statut 4 à **148**, recyclé **149** ; 26 : D9 à 194 et 226, H = 225, statut 4 à **248**, recyclé **249** ; 27 : D9 à 122 et 254, H = 253, statut 4 à **276**, recyclé
  **277**. `FrameOf(B, 434)` = **278** = dernière E1 (276) + 2 = X ; `@442` = **319** (X+41), `@461` = **328** (X+50), `@477` = **337** (X+59), `@493` = **346** (X+68). G569 et G262 posés ; `GroundProperty` 128 en (10,44) et
  (11,44), 144 en (5,42) (bit `0x80` posé), 16 en (16,42) (éteint) ; `EntityByRecord(24..27)` nul ; **quatre effets de bris** (effet 8, `RecordIndex` −1, mode 2, ancre non nulle, X/Y du Muruta tué) : enregistrement 24 emplacement 4
  à E1 + 1 = 177, 25 emplacement 0 à 149, 26 emplacement 3 à 249, 27 emplacement 4 à 277 ; `AssertNoUnexpectedError` et `AssertNothingSkippedOrExceeded`.
- Mutations réelles (appliquées au fichier par un script qui enregistre l'original et le restaure, construites, testées ; aucune n'est dans un commit) : C10 masque de fin `0x1C`→`0x1F` : 3 rouges (vecteurs C10, appels C10, arc H115) ;
  D9 animation 2→3 : 4 rouges (vecteurs D9, appels D9, `D9_AppliesTheLatch…`, arc H115) ; A0 sans le +1 : 4 rouges (vecteurs A0 et trois cas de `A0_TakesTheHighHalf…`) ; état 5 `0x40`→`0x20` : 3 rouges (vecteurs C10, appels C10,
  arc H115) ; C10 lisant `AnimationDirection` au lieu de `BoxDirection` (la fenêtre avant, dans l'aide) : 4 rouges (vecteurs C10, appels C10, deux tests d'`AlundraEnemyHelpersTests`).
- Registre : **6 entrées en production** ; aucune inscription ne liste −1 (test des 6 emplacements × 400 codes, inchangé) ; les tests du §1.3d à jour.
- Traces, arcs des leviers, arène de c1a et c1b, `AlundraDay3SceneArcTests`, `AlundraInoaDayOneArcTests`, `AlundraShipBlockArcTests`, `AlundraVisionArcTests`, `AlundraHeroLifeArcTests`, `AlundraEntityContactArcTests`,
  `AlundraTarnMurggArcTests` : **aucun ne bouge** (suite complète sans échec). `AlundraTarnMurggArcTests` R3-2 (carte 15, dont les Muruta ont maintenant leurs gestionnaires) ne bouge pas : O-E14-2 reste fermé.

**Les tests du registre (§1.3d).** `ProductionRunner_RegistryHoldsTheBeannoidsThreeHandlers_AndTheSwitchIsOn` devient `ProductionRunner_RegistryHoldsTheSixHandlersOfTheBeannoidAndTheMuruta_AndTheSwitchIsOn` (`Count` 6, les trois clés
de chaque type, chaque gestionnaire pour son seul type) ; `EmptyRegistry_EveryEntity_IsACountedNoOp` passe à la clé libre (D, 25) pour le type 347 ; un test ajouté : `TheJournalOfTheNativeHandlers_CountsEachKeyThatRan`.

**Vérification (2026-10-10) : CONFIRMED** par deux verifiers frais (workflow, l'un après l'autre). Rejeu : `Alundra.Tests` 4752/4752 en
Release et en Debug, `cmp`, traces ; chaque point de l'acceptation relu contre les tests (comptes de vecteurs et d'appels réellement
comparés, nombres de l'arc H115, registre à 6). Adversarial : `BoxDirection` lue en deux largeurs et jamais `AnimationDirection`,
`HasFrameBox` lu comme champ, A0 avec le +1, branche de la carte `0x99` absente et commentée, D9 par `HitReaction(e, 2, ctx)`, registre
limité à 347 (335 inchangé), aucun arc existant ne bouge (dont `AlundraTarnMurggArcTests` R3-2 avec les Muruta de la 15 natifs).
Remarques P4, dispositions de la session principale :
- **Prédicat des effets de bris de l'arc** (écart 4 ci-dessous : « exactement un emplacement **nouveau**, absent à E1 − 1, à X/Y du Muruta
  tué ») : **accepté** ; l'effet 8 vit environ 33 ticks et deux Muruta morts à 28 ticks d'écart au même endroit laissaient deux
  emplacements ; le reste du prédicat d'E14.a1b est inchangé.
- Pointeur `0x80144209` monté avec l'attaque `(0x55, 1)` et non avec une attaque dont la lecture ferait échouer le test : **limite notée**
  (l'oracle ne le déréférence jamais : la boîte du héros est éteinte et la condition s'arrête avant l'attribut).
- Ligne O-E14-2 du tableau des points ouverts : **corrigée** (relue en E14.a1c).
- Commentaire au futur dans `AlundraEnemyHelpers.cs` (« the Muruta's come with E14.a1c ») : **reporté** à la prochaine tranche qui touche le
  fichier.

**Écarts et limites.**
1. **Ordre « tests d'abord » de T1 non suivi à la lettre** : le code d'`AlundraMurutaAi` a été écrit avant le fichier de tests. La morsure est prouvée : avec le fichier de tests et sans l'inscription dans `CreateForProduction`, 7 des 14 tests
   d'oracle sont rouges (« le registre n'a pas lancé le gestionnaire »), puis verts avec l'inscription ; les cinq mutations ci-dessus le confirment.
2. **Compte des pointeurs d'attaque du héros** : le plan disait 612 appels de `0x80144229` et 3 025 de `0x80144209` ; mesuré sur les 3 637 appels de C10, par la colonne par tick de la ligne qui précède l'appel : **615** et **3 022**, et
   `0x80144229` ⇔ boîte d'image du héros non nulle (aucun appel ne les sépare). `0x80144209` reçoit l'attribut 0x55 (jamais lu : la boîte est éteinte et le court-circuit s'arrête avant l'attribut).
3. **Mots de forces** (montage du test) : la saleté de l'oracle peut laisser un des six mots à 0, qu'aucun changement ne liste ; quand un mot de force listé passe à 0, les non listés valaient déjà 0 (l'aide les remet à zéro ensemble).
   Le vecteur C10 n° 3267 (`forcey_C8`) l'a montré ; sinon le marqueur reste attendu inchangé.
4. **Effet de bris : critère de l'arc** : un effet 8 dure environ 33 ticks et deux Muruta peuvent se tenir au même endroit (non collidables) ; l'enregistrement 24 et l'enregistrement 25 étaient au même X/Y, donc le prédicat « exactement
   un emplacement à X/Y du Muruta tué » du plan en comptait deux à E1 + 1 pour le 24 (l'emplacement du 25, mort 28 ticks plus tôt, vivait encore). Le critère est devenu : exactement un emplacement **nouveau** (absent à E1 − 1) aux X/Y
   du Muruta tué, avec le reste du prédicat (effet 8, `RecordIndex` −1, mode 1 ou 2, ancre non nulle). Aucune assertion ne dépend des positions des Muruta.
5. **Numérotation** : les quatre `0x2D` sont tous à l'image 0 de la trace (la première image, comme les arcs précédents), pas à « l'image 1 » du plan ; les écarts du plan (E1 + 2, X+41, +50, +59, +68) sont exacts.
6. `NativeHandlerRunCounts` est une addition de production (interne, un dictionnaire incrémenté avec `NativeHandlerRunCount`), faite pour compter D9 directement ; elle ne change aucun comportement.
7. Sans héros (`PlayerEntity` nul), C10 ne fait rien et D9 applique les dégâts et l'animation sans la direction ; la branche de la carte `0x99` n'est pas portée (commentaire dans `AlundraMurutaAi`) ; l'arc tourne hors de la collection du
   générateur statique et n'affirme que des issues indépendantes du tirage (butin, son 84 et positions non affirmés).
8. Commentaires reportés d'E14.a1b corrigés : `EntityFlags.cs` (destruction VRAM), `AlundraEventProgramRunner.cs` (E1), `AlundraEntityDepthKey.cs`, `IEntityWorldContext.cs`, `IAlundraScriptHost.cs`.
9. Aucun arrêt du plan n'a joué : tous les Muruta atteignent `@1469` 44 images après leur `0x2D`, chaque coup fait 2, les décalages H+1 / H+23 / H+24 et E1 + 2 sont ceux de l'oracle, aucun nom changé hors clôture, le moteur et
   `CasaEngineMonogame/` n'ont pas été touchés.

##### Arrêts, budget, retour arrière

- **Arrêts** : un Muruta du hall qui n'atteint pas `@1469` dans les 600 images qui suivent son `0x2D` (la chute des entités autres que
  le héros relève d'E20.e0k : question à l'auteur) ; un coup qui ne fait pas 2 ; les arrêts d'E14.a1b (vecteur en écart après deux
  tentatives, épingle de l'arène c1a ou c1b qui bouge, décalages H+1/H+23/H+24 ou E1 + 2 qui diffèrent, nom changé hors clôture,
  besoin de modifier le moteur, défaut de l'original soupçonné).
- **Budget** : un `executor`, deux tentatives au plus ; un verifier frais ; cinq passes de correction au plus.
- **Retour arrière** : `git revert` des commits d'E14.a1c.

**Recette (D-E14-7).** Le hall de la 115 : quatre Muruta tués à l'épée, un nuage de bris par mort, trois portails rendus, (16,42) reste
fermé. La grille de la 116 : animée après les deux Beannoïde. La 14 : les sept Beannoïde dorment, se réveillent et meurent.

### 1.3d Tests qui bougent

- `AlundraNativeSpriteRegistryTests`, relue en entier contre les six clés de production ((A,1), (C,1), (D,1) pour 335 ; (A,0),
  (C,10), (D,9) pour 347) ; quatre tests bougent, les autres (clés (C,25), (C,26), (D,25), (E,0), (E,1), livre de sauvegarde) non :
  - `ProductionRunner_RegistryIsEmpty_AndTheSwitchIsOn` : `Count` 0 → 3 en E14.a1b → 6 en E14.a1c (nom à mettre d'accord avec ce qu'il
    épingle : le registre de production et l'interrupteur) ;
  - `RegisteredEntry_ListingSeveralTypes_RunsForEach` (E14.a1b) : sa clé (D,1), enregistrée en production pour 335, devient une clé
    libre, par exemple (D,25) ;
  - `EmptyRegistry_EveryEntity_IsACountedNoOp` (E14.a1c) : son entité (D,9) du type 347 lancerait le vrai D9 ; elle prend une clé ou un
    type non enregistrés en production (par exemple (D,25)), et le test garde son sens (une clé non enregistrée est un no-op compté) ;
  - `Registering_TheSameKeyTwice_Throws_AndDisposingRemovesTheEntry` (E14.a1b) : le `Count` attendu après `Dispose` est le compte d'avant
    l'enregistrement du test (relatif), plus jamais un 0 absolu.
  Acceptation de la classe après E14.a1b et après E14.a1c : aucun test n'enregistre ni ne lance une clé de production pour 335 ou 347,
  aucun n'attend un `Count` absolu de 0 hors du test du registre de production.
- `AlundraEntityScriptProxyCloneTests` (`BreakEffect`) ; `SpriteWriterSpriteRecordsTests` (nouveaux cas, E14.a1a).
- À attribuer s'ils bougent (effet de bris d'une destruction E0/E1, de pente ou VRAM, D-E14-9) : `AlundraDay3SceneArcTests`,
  `AlundraInoaDayOneArcTests`, `AlundraShipBlockArcTests`, `AlundraVisionArcTests`.
- Inchangés attendus (INFERENCE), relancés pour preuve : les leviers (`AlundraLeverArcTests`, `…BridgeArcTests`, `…BasculeArcTests`,
  `…PillarArcTests`, `…DoorsArcTests`, `AlundraLeverSmokeTests`) ; `AlundraHeroLifeArcTests` ; `AlundraEntityContactArcTests` T-C61 ;
  `AlundraTarnMurggArcTests` R3-2, qui ferme O-E14-2 s'il ne bouge pas. Inchangés par l'interrupteur : les classes de `HitArena` (dont
  `AlundraEnemyGroundTests` et `AlundraEnemyHitReactionTests`).

### 1.4 E14.b — Les autres ennemis : recensement complet et découpage proposé ⏳ (2026-10-17, lecture seule ; à approuver)

**Origine.** Thème de repli donné par l'auteur le 2026-10-16 (« implémenter toutes les IA des ennemis »), pendant la pause du chantier de
restructuration (`docs/plan-restructuration-dll.md`, Q-R-2). Le §3 de ce plan excluait « les autres ennemis » : cette section les recense
et propose un découpage ; rien n'est exécuté (aucune tranche de code pendant le chantier, règle du programme de restructuration).
Preuves : `docs/plan-e14-annexe/e14-roster/` (rapport, `roster.tsv`, `handlers.tsv`, `slices.tsv`, scripts, critique `check.md`).

**Faits (contre-vérifiés par une critique fraîche ; réserves plus bas).**
- La règle de répartition, ré-établie au binaire : pour l'emplacement `s`, si `entity[+0x4C+4*s] & 0x7F == 0`, le gestionnaire natif
  `table[s][entity[+0x70+4*s]]` est appelé (`0x800388F8-0x80038944`, déjà CONFIRMED dans `e14a/check.md:75`) ; les codes viennent des
  octets `+0x13..+0x17` de l'en-tête du sprite (`InitializeEntity`) ; la DLL suit la même règle (`AlundraEventProgramRunner.cs:347-349`).
- Les tables natives du binaire : 203 entrées non nulles (A 24, C 103, D 45, E 29, F 2), 201 adresses distinctes.
- **52 types d'ennemis avec enregistrements** (39 ennemis, 13 boss ; 1 566 enregistrements sur 185 cartes), plus 17 types nés à
  l'exécution par `SpawnWarpEntity` (enfants de boss, projectiles d'ennemis…). **Portés : 2** (Beannoïde 335, Muruta 347). **Non portés :
  50** (1 233 enregistrements, 171 cartes), dont les 13 boss.
- Ordre de l'histoire connu (chaîne d'E19, `docs/plan-e19-opcodes.md:382`, `:2564-2566`) : carte 14 (jour 2) Poisson 392 ×2 et Guêpe 455
  ×3 ; carte 15 (jour 2) Orc (masse) 341 ×3 ; carte 44 (jour 3) les deux slimes géants 459 et 460 (enregistrements activés recomptés par la
  session principale dans `data-extracted/data/map_{14,15,44}.json`). Au-delà du jour 4 : **ordre inconnu**.
- La DLL : `AlundraNativeSpriteRegistry.Register` lève une exception sur une clé déjà inscrite (`AlundraNativeSpriteRegistry.cs:53-55`),
  alors que D6 et D11 servent chacun 8 types et que A1, D1, C1, C10, D9 sont partagés : l'inscription doit devenir cumulative par type.
  E0, E1 et E10 sont traités en ligne pour tout type (`AlundraEventProgramRunner.cs:378`) ; E6, E7, E8, E27 ont un corps identique à
  E0/E1 (comparaison d'octets) ; **E11 n'est pas un alias** (`0x8007F25C` appelle `0x8003A648` : destruction récursive des enfants, butin,
  effet), atteint par 8 boss.
- Dix gestionnaires D ont la forme de `HitReaction` (déjà dans la DLL : `AlundraEnemyHelpers.HitReaction`) ; D2, D3, D5, D10, D12 sont
  plus longs.
- Aides du binaire absentes de la DLL, appelées par les clés manquantes : `0x800808DC` (11 clés), `CanMoveForward` `0x80080260` (10),
  `SetEntityRandomDirection` `0x80080030` (7), `TryAttackPlayer` `0x80080704` (3), `GetEntityTileHeight` `0x8003AB54` (2),
  `CreateEffectEntity` `0x8003BDD8` pour d'autres effets que le bris (9).
- Prérequis hors E14 : le butin (E20.e0c) et la blessure du héros au contact (E20.c2b) pour toute recette qui les compte ; les effets et
  `CheckAndTriggerTileEffect` (E20.e) ; la création d'enfants par les gestionnaires (C7, C8, créateurs A11, A15, A21, A22, clés de boss).

**Réserves de la critique** (à reprendre en T0 de la première tranche) : le Beannoïde « partiel » (des `0x41 [C,99]`, `[D,44]`, `[C,96]`
sur les cartes 472 et 473) n'est établi que par le script de l'annexe ; l'origine de l'E11 de Zazan (en-tête ou `0x41 [E,11]`) est dite
de deux façons ; les comptes bruts sont internes au pipeline (non recalculés indépendamment, sauf les cartes 14, 15 et 44).

**Découpage proposé** (`slices.tsv` ; chaque tranche suit le motif d'E14.a : oracle MIPS par gestionnaire, tests rouges d'abord,
verifier frais ; les arènes de test qui contiennent ces types changent de base, à recenser en T0) :

| Tranche | Contenu | Enreg. | Cartes | Dépend de |
|---|---|---|---|---|
| E14.b0 | Socle : inscription cumulative par type, E6/E7/E8/E27 en ligne, formes `HitReaction`, setters A, E3 et C36, aides manquantes ; aucun comportement visible | — | — | E14.a faite |
| E14.b1 | Jour 2, cartes 14 et 15 : Poisson, Guêpe, Orc (masse, hache, armure de fer) | 157 | 31 | b0 |
| E14.b2 | Slimes géants de la 44 (jour 3) | 2 | 1 | b0 |
| E14.b3 | Slime gélatineux (A1 et D1 rouverts au type 329, C26, projectile 330) | 118 | 18 | b0 |
| E14.b4 | Mimming et mimique | 102 | 25 | b0 |
| E14.b5 | Roches, insectes, ver (C7 et C8 créent des entités) | 253 | 40 | b0, projectile 411 |
| E14.b6 | Momies, fantôme, zombie, loup-garou | 81 | 21 | b0 |
| E14.b7 | Muruta à l'arc et au boomerang | 69 | 14 | b0, projectiles 351, 357 |
| E14.b8 | Hommes-lézards (épée, lance, projectiles) | 175 | 12 | b0, projectiles 366, 372 |
| E14.b9 | Roper, Homme de boue, Homme-ombre, Champignon, Fourneau, Oiseau de feu, Homme de bois, P-Zoldia | 243 | 45 | b0, projectiles 432, 444, 468, 474 |
| E14.b10 | Slime géant d'Elane (œil, corps) et petit slime | 6 | 4 | b0, C1 du Beannoïde |
| E14.c1 à c14 | Un boss par tranche, avec son oracle MIPS (Zorgia, Élément, Melzas, Monsieur Aspiration, Zazan-Roi Muruta, Boss lézard, Mille-pattes, Dragon, Après Melzas, Nirude, Ronan divin corrompu, Abyss, Frère Rayon, Wilda) | 1 à 4 | 1 à 4 | b0 ; Zazan après b8 (D2) ; E11 pour 8 boss |

Ordre : l'histoire connue d'abord (b1, b2), puis le rendement en cartes (b3 à b10), puis les boss ; l'auteur peut réordonner b3 à b10 et
les boss sans casser les dépendances. Questions : Q-E14-b1 à Q-E14-b3 (§2).

**Lecture au binaire d'E14.b1 (2026-10-17, `docs/plan-e14-annexe/e14b1/`).** Les dix gestionnaires des cinq types des cartes 14 et 15
(A0 ; C15 hache, C16 masse, C17 armure de fer, C18 Guêpe, C19 Poisson ; D5, D6, D11, D12) et leurs aides sont lus au binaire, transcrits en
modèles Python (`work/models.py`) et comparés au vrai code exécuté dans un interpréteur MIPS : 0 écart sur 9 000 vecteurs par gestionnaire,
100 % des instructions, 10 mutants tués ; une critique indépendante, avec **son propre** interpréteur, redonne 0 écart sur 20 000 vecteurs par
gestionnaire (`check.md`). Faits qui comptent : `CanMoveForward` (`0x80080260`) est exactement la décision d'`AlundraEnemyHelpers.HandleAnimationDirection`
déjà portée, sans ses effets (0 écart sur 82 944 cas) ; la décompilation se trompe sur `CanMoveForward` (sur sol plat, elle répond 1 et le
binaire 0), a perdu la **parade de l'Orc masse** (code mort par un test imbriqué, 30 % des coups parés), fait tomber la Guêpe dans sa course
d'attaque là où le binaire finit le tick, lit `FloorHeight` là où le binaire lit `+0x11C`, et teste ses nibbles en signé (un tirage de trop) : le binaire
tranche. 157 enregistrements sur 31 cartes. Le registre doit devenir cumulatif par type (D6 et D11 servent 8 types). Exposés aux
gestionnaires vivants : une vingtaine de classes de tests de la carte 14 et `AlundraTarnMurggArcTests` R3-2 (carte 15) ; les arènes
`AlundraHitArena` coupent les natifs. Tâches proposées T0 à T9 au §9 du rapport (oracle par la méthode d'`e14a/oracle-a1/`, socle, Poisson,
Orcs, Guêpe, câblage, verifiers, recette). Questions Q-b1-1 à Q-b1-5 (§2 de ce plan).

## 2. Points ouverts

| Point | Question | Où |
|---|---|---|
| O-E14-0 | **Tranché le 2026-10-10 (D-E14-8)** : E14.a0, E14.a0b et E14.a1 passent avant les armes, les objets et les collisions | — |
| O-E14-1 | **Tranché le 2026-10-10 (D-E20-26)** : l'export de l'effet de bris reste dans E14.a1 (D-E14-2) | — |
| O-E14-2 | **Fermé le 2026-10-10** : `AlundraTarnMurggArcTests` R3-2 (carte 15) ne bouge ni avec les gestionnaires natifs du Beannoïde (E14.a1b) ni avec ceux des Muruta de la 15 (relu en E14.a1c) | — |
| Q-A1-1 | **Tranché le 2026-10-10 (D-E14-9, ADR-0060)** : l'effet de bris pour tous les appelants de `DestroyEntity(e, fx)`, comme le binaire | — |
| O-E14-4 | **Tranché par la session principale (2026-10-10)** : l'ancre et la clé viennent d'E20.f1b (B3), exécutée avant E14.a1b ; pas de copie de B3 | — |
| O-E14-5 | **Tranché par la session principale (2026-10-10)** : E20.e0b perd « `DestroyEntity(e, fx)` avec l'effet de bris », qu'E14.a1b fait ; E20.g2 et E20.f4 le prennent d'E14.a1b ; E20.e0x ne garde que `BankKey` et part de l'état d'E14.a1a (`docs/plan-e20-heros.md` §1.1) | — |
| O-E14-6 | **Tranché par la session principale (2026-10-10)** : les sorties d'`oracle-a1/out/` (24 Mo de texte, lues par les tests) sont versées en texte | — |
| O-E14-7 | **Tranché par la session principale (2026-10-10)** : E14.a1a tourne seule (aucun autre exécuteur ni verifier sur l'arbre pendant ses exports) | — |
| O-E14-3 | Le sprite C10/D18 (cartes 329, 153, 368) et le petit slime (C1/D23) partagent du code avec E14.a : ils restent inertes jusqu'à leurs propres gestionnaires (D-E14-1) | plus tard dans E14 |
| Q-E14-b1 | (2026-10-17) Le recensement de §1.4 (50 types non portés, 13 boss) devient-il le périmètre d'E14, dans l'ordre proposé (b0, b1 et b2 pour l'histoire connue, puis le rendement, puis les boss) ? Et quand : après la restructuration de la DLL (règle du chantier), ou avant, en suspendant le chantier ? | l'auteur |
| Q-E14-b2 | (2026-10-17) L'ordre de jeu au-delà du jour 4 est inconnu des plans : l'auteur en a-t-il un (régions dans l'ordre), ou l'ordre par rendement de §1.4 convient-il ? | l'auteur |
| Q-E14-b3 | (2026-10-17) Une recette d'ennemi suppose souvent que le héros soit blessé (E20.c2b) et que l'ennemi lâche son butin (E20.e0c) : ces deux tranches d'E20 passent-elles avant E14.b1, ou E14.b1 s'accepte-t-elle sans elles (ennemis qui bougent, frappent et meurent, sans dégâts au héros ni butin) ? | l'auteur |
| Q-b1-1 | (2026-10-17) Porter la **parade de l'Orc masse** (30 % des coups d'épée parés dans ses états 0, 1 et 7), que la décompilation a perdue ? Le binaire tranche (règle de l'auteur) : recommandé oui | l'auteur |
| Q-b1-2 | (2026-10-17) Porter dès b1 la branche « Homme momie » de D5 (esquive 40 %, morte tant que le type 425 n'est pas inscrit), testée par vecteurs ? Recommandé oui (corps partagé, fidèle) | l'auteur |
| Q-b1-3 | (2026-10-17) Les ~20 classes de tests de la carte 14 qui gardent les natifs vivants : les laisser exposées et ne réépingler que ce qui bouge, preuve à l'appui (recommandé), ou couper les natifs dans chacune ? | l'auteur |
| Q-b1-4 | (2026-10-17) La poussière de l'Orc masse lit l'octet C de la carte (`[0x800E4334]+0xA`, 7 sur ses 13 cartes) : lire la donnée de carte exportée (recommandé) ou coder 7 ? | l'auteur |
| Q-b1-5 | (2026-10-17) Après b1, les cartes 14 et 15 gardent des types inertes (Boss lézard 494, Mille-pattes 488, Muruta au boomerang 356, projectiles 371) : sont-ils attendus là au jour 2 ? | l'auteur, Q-E14-b2 |

## 3. Hors périmètre

Le butin (E20.e0) ; la mort du héros (E18) ; le héros blessé (E20.c2b) ; les autres ennemis, recensés et proposés en §1.4 (Q-E14-b1).
