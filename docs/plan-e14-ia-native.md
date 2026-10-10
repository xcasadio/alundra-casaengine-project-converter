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

### 1.1a E14.a0 — Le registre et l'interrupteur ✅

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

### 1.2 E14.a0b — Infrastructure des ennemis ✅ (après E14.a0, D-E14-8 ; le registre est E14.a0 ; exécutée le 2026-10-10, verifier frais à lancer)

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

### 1.3 E14.a1 — Découpage ⏳ (détaillé le 2026-10-10)

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

### 1.3a E14.a1a — L'octet de l'effet de bris dans l'export ⏳

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
  du moteur a bougé depuis le dernier export (`1b2b616a` → `781d3527`, dont la fusion d'`origin/main` du 2026-10-10).

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
- ⏳ **T0 — Prédiction et état de départ.** Annexe écrite ; manifeste `m0` (attendu : 24 103 fichiers, empreinte ci-dessus ; un écart
  s'explique avant d'aller plus loin) ; `diff_manifests.py` de `m0` contre `m7-manifest-after.txt` : exactement les 47 chemins plus
  `UI/Textures/font3.png` ; compte de tests `Alundra.Tests` relevé.
- ⏳ **T1 — Export de référence au HEAD**, avant toute modification du convertisseur. Fenêtre calme : aucun build, aucune suite, aucun
  Launcher ni éditeur sur `alundra-project/` ; `Alundra.dll` déverrouillé. Depuis la racine :
  `dotnet run --project alundra-casaengine-project-converter -c Release -- data-extracted alundra-project` ; sortie 0 et
  `Verification: PASSED` ; `m1` contre `m0` : seulement `report.json`.
- ⏳ **T2 — Tests rouges.** `"BreakEffect": 8` écrit entre `SizeZ` et `Contents` ; clé absente → écrite à 0, pas omise ; le fichier
  reste hors catalogue.
- ⏳ **T3 — Code et docs.** `dotnet test alundra-casaengine-project-converter.Tests --blame-hang-timeout 60s`, au premier plan ; 848 plus
  les nouveaux, tous verts.
- ⏳ **T4 — Export 1, puis export 2 tout de suite.** `m2` contre `m1` : changés exactement `Data/sprite-records.json` et `report.json`,
  0 ajouté, 0 retiré ; `cmp` du fichier exporté avec le fichier prédit ; `m3` contre `m2` : `report.json` seul (D-N-7).
- ⏳ **T5 — Suites.** `Alundra.Tests` en Release puis en Debug : même compte, sans échec (la DLL ignore la clé jusqu'à E14.a1b) ; `cmp`
  de la DLL ; traces inchangées.
- ⏳ **T6 — ADR-0061, plan, verifier frais.**

**Acceptation.**
- `Data/sprite-records.json` : 2 561 505 o (+9 132), SHA-1 `007f2660eb83d5a3d21aa35d7c71dd34dba0afcf`, 395 fiches ; histogramme
  {0: 284, 2: 1, 8: 63, 10: 30, 11: 15, 12: 1, 13: 1}, soit 111 non nuls ; `e3a39ec4-45d5-55f2-85e9-894c7b6e4a08` (Muruta) = 8 et
  `1ef8f16c-605f-58c3-af19-b6bb602fc40f` (Beannoïde) = 8 ; ordre des clés : …`SizeZ`, `BreakEffect`, `Contents`, `IdsvAnimDirs`,
  `AnimSets`[, `DialoguePortrait`] ; chaque objet égal à l'ancien, moins `BreakEffect`.
- `AssetInfos.json` inchangé (`a09917c31694925076d1fc1aba5fca773f6cada2`).
- `report.json` : `Errors` 0 ; les 6 `Warnings` inchangés ; `Verify.Assets` 22 616, `Verify.Loaded` 20 139,
  `Verify.ExistenceChecked` 2 477 ; `Entities.Prefabs` et `SpriteRecords.Exported` à 395 ; `Phase1.StalePagesPurged` 2 415 ;
  `OutputFileCount` 24 106 ; `OutputSizeBytes` + 9 132, plus l'écart de taille de la DLL et du PDB.
- Rien ne change sous `alundra-project/UI/Screens/` (suivi par git) ; convertisseur vert ; `Alundra.Tests` sans échec en Release et en
  Debug ; verifier frais.

**Arrêts.**
- T1 change autre chose que `report.json` : attribuer d'abord à la montée du pointeur du moteur (`1b2b616a` → `781d3527`), rebaser
  comme L-b2 (nouvelle base, consignée), puis seulement continuer ; un écart que la montée n'explique pas : question.
- Après le code : tout autre chemin changé, ajouté ou retiré ; `AssetInfos.json` qui bouge ; fichier différent de la prédiction ;
  second export différent hors `report.json` ; erreur ; compteur ou avertissement non prévu.
- Tout process (build, test, Launcher, éditeur) sur l'arbre pendant un export.
- Toute idée de supprimer `alundra-project/`, de faire un `robocopy /MIR` ou un miroir de `data-extracted/`, ou d'indexer
  `CasaEngineMonogame/CasaEngine.Launcher/Program.cs`.

**Budget** : un `executor`, deux tentatives au plus ; `plan-verifier` avant l'exécution ; `verifier` frais à la fin. **Retour arrière** :
`git revert`, puis un export complet en place.

### 1.3b E14.a1b — Effet de bris, Beannoïde, grille de la 116 ⏳ (à relire avant exécution, après E20.f1b)

**Faits porteurs.**
- [binaire] `0x8003A59C`, dans l'ordre : (1) butin `0x80032B90`, sans effet tant que `+0x3C` vaut 0 (`0x80032BA4`) ; (2) statut 4 ;
  (3) `+0x228 = -1` ; (4) `+0x1B8`, code mort (jamais posé dans le binaire) ; (5) `fx = -1` → octet de l'en-tête ; si `fx != 0`,
  `0x8003BE74(0, fx, 0, e ; 1, 0, 0, 0)` : X/Y/Z lus en `+0x114/118/11C`, `+0x60 = 1`, pas de son, pas de tirage, rien quand la
  réserve est pleine ; (6) effacement du porteur `+0x28 → +0x2C`, qui reste à E20.g.
- [binaire] 38 sites d'appel, dont la pente (`fx = 6`, `0x8003878C` ; 26 préfabs, 16 à octet non nul : jarres, tonneaux, caisses, effet
  10) et le drapeau VRAM (`fx = -1`, `0x800387C4` ; 107 préfabs, 18 à octet non nul) de la passe de choix ; la DLL leur passe déjà 6 et
  −1 (`AlundraEntityScriptProxy.cs`, passe de choix).
- [DLL] La file des effets est appliquée en fin d'image (`AlundraEffectStage.RunFrame`) ; dans une image de 2 ticks ou plus, une ancre
  vivante est déjà recyclée au statut 0 et `MoveFollower` gèle l'emplacement sans écrire sa clé : la création prend donc une **ancre
  figée** au moment de la destruction (statut 4, X, Y, `PosZ + 1`, clé).
- [oracle] Mort avec un coup au tick H : D à H+1, animation 6 à H+14, E1 à H+23, case recyclée à H+24, `0x2C` d'un événement de carte
  nul à H+25 ; pour un programme d'entité, nul dès H+24 (ordre du binaire ; à mesurer dans la DLL).

**Périmètre.**
- `SpriteRecordCatalog` (en-tête, JSON, `ToHeader`), champ `BreakEffect` du proxy, `Clone`, `ApplySpawnInitialization`.
- Ancre et clé : celles d'E20.f1b (B3 : `AlundraEntityDepthKey.Compute`, l'aide d'IDSV partagée, l'ancre du proxy en `PosZ + 1`),
  reprises telles quelles ; une ancre figée prise à la destruction.
- `AlundraWorldProxy.DestroyEntity(entity, fx)` dans l'ordre du binaire, pour **tous** ses appelants (D-E14-9), avec un bouchon nommé
  pour le butin (E20.e0).
- `AlundraNativeEffects.CreateBreakEffect(reservoir, fx, anchor)`, fonction pure appelée par `_effectStage.Emit` (rang 1) ; `CreateNow`
  n'est pas utilisé.
- `AlundraBeannoideAi` : A1, C1, D1, enregistrés pour 335 ; D1 passe par `AlundraEnemyHelpers.HitReaction(e, 5, ctx)`.
- Oracle `docs/plan-e14-annexe/e14a/oracle-a1/` (copie du scratchpad `oracle-handlers/docs/plan-e14-annexe/e14a/oracle/`, sans
  `__pycache__`), sorties versées en texte (O-E14-6) ; tests ; arc G116.

**Tâches** (détail à relire avant l'exécution) : T0 oracle (deux relances, même manifeste, 74 fichiers) ; T1 `BreakEffect` dans la DLL
(aller-retour, clé absente → 0, `Clone`, épingles sur l'export réel : les deux préfabs valent 8) ; T2 ancre figée ; T3
`DestroyEntity(e, fx)`, tests d'abord (E1 `fx -1` octet 8 : un emplacement, effet 8, animation 0, `DepthOffset` 1, X/Y = `PosX`/`PosY`,
Z = `PosZ + 1` ; clé de l'entité + 1 après la première mise à jour ; libéré à la mise à jour 33 ; octet 0 → rien ; pente → effet 6 ;
réserve pleine → rien ; images de 1, 2 et 3 ticks → même emplacement ; aucun tirage) ; T4 Beannoïde A1, C1, D1 contre
`oracle-a1/out/vectors-beannoide.json` et `calls[]` des scénarios `BN-*` et `ARENA-*` (conversion du binaire vers la DLL : `PosZ`,
`FloorHeight` et `MapHeights` − 1) ; T5 tests qui bougent (§1.3d), arc G116, suite complète ; T6 plan, notes, verifier frais.

**Arc G116** (`ArcRun` direct, natifs actifs). `LeverArcs.Zone` / `World116`, sans drapeaux, `Arrival` (84, 328, z 0) — la case
(3,20), qui fait apparaître les enregistrements 0, 1, 17 et 41 à 44 (l'arrivée en (444,328) n'en fait apparaître aucun : la zone
d'apparition des enregistrements 0 et 1 est X 0..7, Y 0..29) —, limite d'environ 3 000 images, `Give(1)`, `Equip(1)`, puis
`PlaceHero` sur le plateau (z 160). Politique d'épée copiée de `Swordsman` (`oracle-a1/e14run.py`) : cible frappable si statut 2,
`IsOnGround`, `FrameCollisionTickCounter` 0 et animation sans le bit `0x40` ; héros posé à 40/34/28/22/16 px à l'ouest, au premier
endroit dont les quatre coins (décalage (−10, −7), taille (21, 15)) sont marchables à la hauteur de la cible (`LeverArcs.CellAt`) ;
direction 24 (est), `Square`, 25 ticks entre deux appuis.

**Acceptation** (une image = un tick). Vecteurs : 2 052 (A1 3, C1 1 185, D1 720, E1 144), 0 écart sur les champs changés, le mot du
générateur en sortie, les sons et l'effet. Appels des scénarios : 3 140 (A1 12, C1 3 102, D1 22, E1 4), rejoués un par un, 0 écart.
Arc G116 : 10 appels de D1, 5 par Beannoïde, PV 9→7→5→3→1→0 ; pour chaque mort : D à H+1, statut 4 à H+23, `EntityByRecord` nul après
H+24 ; `FrameOf(C, 323)` = E1 le plus tardif + 1 ; enregistrement 6 : `TargetAnimationId` 3 ; enregistrements 17 et 41 à 44 vivants ;
deux effets 8 ; `AssertNoUnexpectedError`. Mutations réelles, par script, chacune rouge : C1 portée `slti` 3→4 ; D1 animation 5→6 ;
ancre vivante au lieu de l'ancre figée (image de 2 ticks) ; `fx -1` non résolu. Suite en Release puis en Debug ; `cmp` ; traces.

**Arrêts.** Un vecteur en écart après deux tentatives ; une épingle de l'arène c1a ou c1b qui bouge ; le décalage H+1/H+23/H+24 qui
diffère dans la DLL (diagnostiquer, ne jamais déplacer l'épingle) ; un arc qui bouge sans attribution à un effet de bris légitime
(octet non nul de l'entité détruite, lu dans l'export) ; un besoin de modifier le moteur ; un défaut de l'original soupçonné (l'auteur
tranche).

### 1.3c E14.a1c — Muruta, hall de la 115 ⏳ (à relire avant exécution)

**Périmètre.** `AlundraMurutaAi` : A0, C10, D9, enregistrés pour 347 ; D9 passe par `HitReaction(e, 2, ctx)` ; A0 écrit
`AIValues[0] = (short)((PosZ + 1) >> 16)` (Z du binaire, ADR-0026 : `lh +0x11E` lit le Z du binaire) ; la branche carte `0x99` de
C10 (`0x80068164`-`0x8006819C`) n'est pas portée, avec un commentaire (morte pour 347, qu'aucun enregistrement de la 153 ne place) ;
C10 lit `BoxDirection` pour `+0x98` (D-E14-6).

**Tâches.** T1 tests sur `vectors-muruta.json` et `calls[]` des scénarios `MU-*` ; T2 arc H115 ; T3 recette de l'auteur ; T4 verifier
frais.

**Arc H115.** `World115`, drapeaux `{262}`, `Arrival` (252, 872) — la case (10,54), z 0, direction 16 ; jamais la case (10,56), qui
lance le dialogue 81 de B[8] (`@1104`). Image 1 : B[2] passe par `@172, @177, @381…, @401, @403, @405, @407` ; `Give(1)`, `Equip(1)` ;
attendre les quatre `FramesOf(C, 1469, 0x40)` ; même politique d'épée ; le héros reste dans X 0..21, Y 34..57 ; ne jamais tenir Haut.

**Acceptation.** Vecteurs : 4 913 (A0 6, C10 4 043, D9 720, E1 144), 0 écart. Appels : 3 675 (A0 16, C10 3 637, D9 15, E1 7), 0 écart.
Arc H115 : 8 appels de D9, 2 dégâts par coup, PV 3→1→0 ; pour chaque mort : D à H+1, statut 4 à H+23, recyclage à H+24 ;
`FrameOf(B, 434)` = dernière E1 + 2 = X ; `@442` = X+41, `@461` = X+50, `@477` = X+59, `@493` = X+68 ; G569 et G262 posés ;
`GroundProperty & 0x80` posé en (10,44), (11,44) et (5,42), éteint en (16,42) (fidèle : seul le chemin G659 le rouvre) ;
`EntityByRecord(24..27)` nul ; quatre effets 8. Mutations : C10 masque de fin `0x1C`→`0x1F` ; D9 animation 2→3 ; A0 sans le +1 ;
état 5 `0x40`→`0x20`. Suite complète en Release et en Debug ; `cmp` ; traces.

**Arrêts.** Un Muruta du hall qui n'atteint pas `@1469` dans les 600 images qui suivent son `0x2D` (la chute des entités autres que le
héros relève d'E20.e0k : question) ; un coup qui ne fait pas 2 ; les arrêts d'E14.a1b.

**Recette (D-E14-7).** Le hall de la 115 : quatre Muruta tués à l'épée, un nuage de bris par mort, trois portails rendus, (16,42) reste
fermé. La grille de la 116 : animée après les deux Beannoïde. La 14 : les sept Beannoïde dorment, se réveillent et meurent.

### 1.3d Tests qui bougent

- `AlundraNativeSpriteRegistryTests` : `ProductionRunner_RegistryIsEmpty_AndTheSwitchIsOn` (`Count` 0 → 3 en E14.a1b → 6 en E14.a1c) ;
  `RegisteredEntry_ListingSeveralTypes_RunsForEach` (sa clé (D,1), désormais enregistrée en production, devient une clé libre, par
  exemple (D,25)).
- `AlundraEntityScriptProxyCloneTests` (`BreakEffect`) ; `SpriteWriterSpriteRecordsTests` (nouveaux cas, E14.a1a).
- À attribuer s'ils bougent (effet de bris d'une destruction E0/E1, de pente ou VRAM, D-E14-9) : `AlundraDay3SceneArcTests`,
  `AlundraInoaDayOneArcTests`, `AlundraShipBlockArcTests`, `AlundraVisionArcTests`.
- Inchangés attendus (INFERENCE), relancés pour preuve : les leviers (`AlundraLeverArcTests`, `…BridgeArcTests`, `…BasculeArcTests`,
  `…PillarArcTests`, `…DoorsArcTests`, `AlundraLeverSmokeTests`) ; `AlundraHeroLifeArcTests` ; `AlundraEntityContactArcTests` T-C61 ;
  `AlundraTarnMurggArcTests` R3-2, qui ferme O-E14-2 s'il ne bouge pas. Inchangés par l'interrupteur : les classes de `HitArena` (dont
  `AlundraEnemyGroundTests` et `AlundraEnemyHitReactionTests`).

## 2. Points ouverts

| Point | Question | Où |
|---|---|---|
| O-E14-0 | **Tranché le 2026-10-10 (D-E14-8)** : E14.a0, E14.a0b et E14.a1 passent avant les armes, les objets et les collisions | — |
| O-E14-1 | **Tranché le 2026-10-10 (D-E20-26)** : l'export de l'effet de bris reste dans E14.a1 (D-E14-2) | — |
| O-E14-2 | Les Muruta de la 15 (scène, `0x2E [128]` `@712`, `@777`, `@890`) : le héros peut-il les toucher pendant la scène ? (`check.md` ligne 45) | E14.a1, lecture de `0x63` et des drapeaux de la scène |
| Q-A1-1 | **Tranché le 2026-10-10 (D-E14-9, ADR-0060)** : l'effet de bris pour tous les appelants de `DestroyEntity(e, fx)`, comme le binaire | — |
| O-E14-4 | **Tranché par la session principale (2026-10-10)** : l'ancre et la clé viennent d'E20.f1b (B3), exécutée avant E14.a1b ; pas de copie de B3 | — |
| O-E14-5 | **Tranché par la session principale (2026-10-10)** : E20.e0b perd « `DestroyEntity(e, fx)` avec l'effet de bris », qu'E14.a1b fait ; E20.g2 et E20.f4 le prennent d'E14.a1b ; E20.e0x ne garde que `BankKey` et part de l'état d'E14.a1a (`docs/plan-e20-heros.md` §1.1) | — |
| O-E14-6 | **Tranché par la session principale (2026-10-10)** : les sorties d'`oracle-a1/out/` (24 Mo de texte, lues par les tests) sont versées en texte | — |
| O-E14-7 | **Tranché par la session principale (2026-10-10)** : E14.a1a tourne seule (aucun autre exécuteur ni verifier sur l'arbre pendant ses exports) | — |
| O-E14-3 | Le sprite C10/D18 (cartes 329, 153, 368) et le petit slime (C1/D23) partagent du code avec E14.a : ils restent inertes jusqu'à leurs propres gestionnaires (D-E14-1) | plus tard dans E14 |

## 3. Hors périmètre

Le butin (E20.e0) ; la mort du héros (E18) ; le héros blessé (E20.c2b) ; les autres ennemis.
