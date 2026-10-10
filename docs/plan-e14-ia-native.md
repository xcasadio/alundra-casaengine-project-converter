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
| **E14.a1** | Beannoïde (A1, C1, D1 pour le type 335) et Muruta (A0, C10, D9 pour le type 347) ; E1 inchangé ; export de l'effet de bris (D-E14-2 : convertisseur, export complet, preuve par manifeste et double export, ADR de format) ; oracle d'E20.c1 étendu (les six gestionnaires réels, A lancé) ; arènes d'E20.c1a/c1b derrière l'interrupteur ; arcs des leviers relancés | oracle appel par appel ; arcs de la 115 (quatre Muruta tués par des coups scriptés, `0x2C` passe, G569 posé `@434`, portails rendus `@493-503`) et de la 116 (Beannoïde 0 et 1 tués, grille animée) | Le hall de la 115 et la grille de la 116 |

Ordre : E14.a0 (prérequis des armes, avant E20.e0a, qui en dépend), puis, après les armes, les objets et les collisions (D-E20-24),
E14.a0b puis E14.a1. **D-E14-8 (2026-10-10)** : l'auteur les avance tous avant les armes, les objets et les collisions.

### 1.1a E14.a0 — Le registre et l'interrupteur ⏳

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
- ⏳ **T1 — Registre et interrupteur**, avec un gestionnaire de test enregistré le temps d'un test.
- ⏳ **T2 — Tests** : une entrée pour un type tourne pour ce type et pas pour un autre ; une clé absente reste un no-op compté ;
  l'interrupteur coupe tout ; le livre et E0/E1 ne passent pas par le registre ; les épingles de `SpriteEventRunCount`
  (`AlundraEventProgramRunnerProgramIndexTests`, `AlundraEventProgramRunnerTests`, `AlundraSaveBookDispatchTests`) ne bougent pas.

**Acceptation.** T2 vert ; registre vide en production ; `Alundra.Tests` sans échec en Release et en Debug ; traces inchangées ; verifier
frais (commun avec E14.a0b, à la frontière d'intégration la plus petite).

**Arrêts.** Une épingle du lanceur qui bouge ; un besoin de changer le moteur. **Retour arrière** : `git revert`.

### 1.2 E14.a0b — Infrastructure des ennemis ⏳ (après E14.a0, D-E14-8 ; le registre est E14.a0)

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

`TryAttackPlayer2` (`0x8008048C`) ne lit que le tableau des positions relatives qu'on lui passe. `HandleAnimationDirection` écrit `+0x88`
(`0x80080230`), remet à zéro les forces `+0xBC`-`+0xD8` et écrit `+0x8C`.

L'exécuteur complète la table pour les aides qu'elle ne couvre pas encore (`StartWalk` `0x8007FEF8`, le squelette de D1/D9), toujours
avec l'adresse du chargement relue dans le binaire, la garde dans le code (commentaire de chaque aide) et dans l'annexe, et arrête si
deux sources se contredisent ou si un champ n'a pas d'équivalent au tick.

**Hors périmètre.** Tout enregistrement de gestionnaire réel (E14.a1) ; l'effet de bris et son export (E14.a1) ; le butin (E20.e0) ;
l'avance du générateur par tick (E20.e0b).

#### Tâches

- ⏳ **T1 — Oracle des aides.** Dans `docs/plan-e14-annexe/e14a/oracle/`, à partir de l'oracle d'E20.c1b (`docs/plan-e20-annexe/e20-c1b/
  oracle/tools/`, copié au bit près) et de fichiers neufs : (a) chaque aide lancée en vrai code sur des états tirés au hasard (au moins
  10 000 par aide), sorties gardées ; (b) **sol et hauteurs de coin sur les vraies cartes** : les cellules des cartes 14, 115 et 116
  (`data-extracted/data/map_<N>.json`) chargées dans la mémoire émulée à la place du champ plat (`*[0x800DD86C] + 0x604`, 52 × 60 × 8
  octets), puis `0x800370C4` et `0x80038064` lancés pour le corps de chaque Beannoïde et Muruta de ces cartes et pour le héros posé à
  côté, sur sol plat et en hauteur (Muruta du hall à 16, Beannoïde de la grille à 20) ; second interprète ; deux relances, même
  manifeste.
- ⏳ **T2 — Sol et hauteurs de coin des ennemis.** Ports et tests contre les sorties (b) de T1.
- ⏳ **T3 — Aides.** Ports exacts, table des entrées complétée ; tests contre les sorties (a) de T1, valeur par valeur ; un test qui
  échoue si une aide lit `AnimationDirection` alors que `BoxDirection` diffère.
- ⏳ **T4 — Verrou du héros sur le contexte, `HitReaction`, doc.**

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

## 2. Points ouverts

| Point | Question | Où |
|---|---|---|
| O-E14-0 | **Tranché le 2026-10-10 (D-E14-8)** : E14.a0, E14.a0b et E14.a1 passent avant les armes, les objets et les collisions | — |
| O-E14-1 | **Tranché le 2026-10-10 (D-E20-26)** : l'export de l'effet de bris reste dans E14.a1 (D-E14-2) | — |
| O-E14-2 | Les Muruta de la 15 (scène, `0x2E [128]` `@712`, `@777`, `@890`) : le héros peut-il les toucher pendant la scène ? (`check.md` ligne 45) | E14.a1, lecture de `0x63` et des drapeaux de la scène |
| O-E14-3 | Le sprite C10/D18 (cartes 329, 153, 368) et le petit slime (C1/D23) partagent du code avec E14.a : ils restent inertes jusqu'à leurs propres gestionnaires (D-E14-1) | plus tard dans E14 |

## 3. Hors périmètre

Le butin (E20.e0) ; la mort du héros (E18) ; le héros blessé (E20.c2b) ; les autres ennemis.
