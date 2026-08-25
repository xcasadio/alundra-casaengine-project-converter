# Plan moteur — pas fixe et rapport de contact du `CharacterMotionSystem`

Date : 2026-08-25. Suite directe de la revue d'architecture demandée après E4 (voir
[plan-e4-deplacement-scripte.md](plan-e4-deplacement-scripte.md), notamment E4.g). Chantier
**moteur** : chaque tranche passe par un `plan-verifier` avant exécution et un `verifier` frais
après. Dépôt propriétaire : submodule `CasaEngineMonogame` (commit propre puis bump du pointeur).

## 0. Pourquoi (constats mesurés pendant E4, 2026-08-24/25)

Deux classes de bugs ont coûté une journée entière et ont la même racine :

1. **Deux horloges.** Le moteur intègre le mouvement en temps réel (`CharacterMotionSystem.Update`
   → `controller.Update(dt)` une fois par frame RENDUE, `CharacterMotionSystem.cs:96-120`), alors
   que la logique Alundra avance à pas fixe 50 Hz. Mesuré en jeu : à ~123 im/s la mouette montait à
   158 px/s au lieu de 150 et dérivait de 179 px au lieu de 209 ; le marin 12 restait bloqué parce
   que `ForceAdjusted`, posé au tick, était effacé par une frame sans tick. Corrigé côté DLL en lui
   donnant la verticale (E4.g), au prix d'un régime différent entre héros (verticale moteur) et PNJ
   (verticale DLL) — dette assumée, à résorber plus tard grâce à M1.
2. **Pas de rapport de contact.** `Move` ne renvoie qu'un `Vector3` de déplacement effectif ; la DLL
   doit re-déduire « quel axe a été raboté » en comparant demandé/résolu avec un epsilon
   (`AlundraEntityScriptProxy.MoveControllerAndPullPosition`), et re-porter elle-même le support
   d'entité (`EntitySupport`, port de `CheckEntityCollisionDown`) alors que le mover connaît déjà
   ces faits.

## 1. Décisions de cadrage

| # | Décision |
|---|---|
| M-1 | **Opt-in, défaut inchangé.** Le pas fixe est désactivé par défaut : démos, éditeur et tous les tests existants gardent exactement le comportement actuel (variable). Aucune régression possible pour un consommateur qui ne l'active pas. |
| M-2 | **Le rapport de contact est additif et sans allocation** : `readonly struct` rempli en place, exposé en propriété du composant (dernier pas), jamais un objet alloué par frame. |
| M-3 | **Aucune migration de la DLL dans ces deux tranches.** Alundra garde son `AlundraLogicClock` et son `EntitySupport`. Rapatrier la verticale des PNJ vers le moteur et remplacer `EntitySupport` sont des décisions ultérieures, à prendre une fois ces capacités livrées et vérifiées (l'oracle d'intro doit rester byte-identique à chaque étape). |

## 2. Tranches

### M1 — Pas fixe du `CharacterMotionSystem` ✅ (moteur, plan-verifier)

- **But** : rendre l'intégration du mouvement de personnage indépendante de la cadence d'affichage.
- **Scope** : `CharacterMotionSystem` gagne un mode à pas fixe **opt-in** : `FixedTimeStep`
  (secondes, 0 = désactivé, défaut 0) et `MaxStepsPerFrame` (défaut 4, ignoré quand le mode est
  off). Quand il est actif, `Update(frameTime)` accumule le temps réel et exécute N pas complets de
  `FixedTimeStep` — chaque pas exécutant la MÊME séquence qu'aujourd'hui (agents navigation/steering,
  drivers, `ApplyCommands`, `UpdateControllers`, ponts d'animation), le **reliquat étant conservé**
  (jamais remis à zéro sauf plafond atteint — exactement le piège déjà corrigé dans
  `AlundraLogicClock`). Quand il est inactif : chemin actuel, inchangé, zéro coût ajouté.
  **Testabilité** : le système expose le nombre cumulé de pas fixes exécutés (compteur simple,
  interne au moteur ou public selon ce qui reste le plus sobre), condition nécessaire du test (e)
  qui pilote jusqu'à un nombre de pas atteint plutôt que jusqu'à une durée.
- **Point à trancher par l'exécutant, à documenter** : les drivers/agents tournent-ils à chaque pas
  fixe ou une fois par frame ? Choix par défaut proposé : **à chaque pas** (un pas = une frame de
  simulation complète), avec justification écrite si l'inspection montre qu'un composant y est
  sensible (par exemple un driver qui consomme un `elapsedTime` réel pour un lissage).
- **Acceptation** (tests `CasaEngine.Tests`) : (a) mode off → séquence et résultats identiques à
  aujourd'hui, tous les tests existants inchangés ; (b) mode on à 1/50 avec dt = 1/50 → exactement
  un pas par frame ; (c) dt = 1/123 sur 123 frames → 50 pas ± 1, aucun reliquat perdu, aucune frame
  au-delà d'un pas ; (d) frame longue (1 s) → plafonnée à `MaxStepsPerFrame` ;
  (e) **invariance de trajectoire, pilotée par le NOMBRE DE PAS et non par une durée** (deuxième
  correctif plan-verifier : à durée fixée d'exactement 1 s, le 50e pas tombe sur la frontière et
  c'est l'arrondi flottant qui tranche — `float(1/123)` cumulé 123 fois reste ~8 ulps SOUS le seuil,
  donc 49 pas et non 50). Le test **pilote des frames jusqu'à ce que le système ait exécuté
  exactement 50 pas fixes** (compteur de pas exposé pour la testabilité, voir le scope), à
  dt = 1/50, 1/123 et 1/240, avec `MaxHorizontalSpeed 10`, `Acceleration 100`, `Gravity 0` (fixtures
  existantes, `CharacterControllerMoveToDriverTests.cs:53-62`) et une intention constante : les
  trois distances doivent être **égales à 1e-4 près** — elles voient les mêmes 50 pas de même dt,
  donc la trajectoire ne dépend plus de la cadence.
  (e-bis) **Témoin en mode off** (sinon (e) serait vacue) : même montage, `FixedTimeStep = 0`,
  chaque cadence pilotée sur ~1 s de temps réel → les distances divergent d'au moins **0,079**
  (9,600 à 1/50 ; 9,540 à 1/123 ; 9,5208 à 1/240 — le modèle est
  `v_n = MoveTowards(v_{n-1}, 10, 100·dt)`, déplacement `v_n·dt` ; la forme fermée `9,5 + 5·dt`
  n'est exacte que si `0,1/dt` est entier, ce qui n'est pas le cas à 1/123) ;
  (f) `CasaEngine.Tests` sans nouvel échec (18 préexistants).
- **Non-goals** : activer le mode pour Alundra (M-3) ; toucher `PhysicsWorld`/Bepu ; changer la
  cadence de systèmes non liés au mouvement de personnage.
- **Écart assumé à documenter (P3, relevé par le plan-verifier)** : sous le mode actif, les pas
  supplémentaires d'une frame voient un index de steering reconstruit une fois par
  `World.UpdateSequence` (`UniformGridSteeringSpatialIndex.cs:43-56`) et des corps Bepu avancés par
  `PhysicsSystemComponent` hors de `World.Update` — donc des positions d'un pas de retard pour ces
  deux sources. Dégradation acceptée, opt-in, sans effet sur un monde comme la 389 (aucun agent de
  steering, sol issu du `ICollisionField`) ; à réexaminer si un jeu active le mode avec du steering
  ou des plateformes rigides mobiles.
- **Rollback** : revert du commit moteur (+ pointeur si bumpé). **Budget** : un commit moteur.
  **Arrêt** : si un agent/driver ne peut pas tourner à pas fixe sans changer son comportement en
  mode off.

### M2 — Rapport de contact par pas ✅ (moteur, plan-verifier)

- **But** : que le mover publie ce qu'il sait déjà, au lieu que chaque jeu le re-déduise avec un
  epsilon. Faisabilité confirmée par le plan-verifier : les déplacements par axe sont de simples
  projections sur (`up`, `h1`, `h2`), et les indicateurs de blocage existent déjà comme variables
  locales dans la résolution par champ — **aucune restructuration de la résolution n'est requise**
  (la condition d'arrêt « restructuration » n'est pas déclenchée).
- **Scope** : `CharacterControllerComponent` expose `LastContact`, un `readonly struct` rempli en
  place (aucune allocation), avec DEUX moitiés de fraîcheur différente, explicitement documentées :
  1. **Moitié déplacement** — remplie à CHAQUE résolution, que l'appelant passe par `Update` ou par
     `Move` : déplacement demandé et résolu projetés sur `up`, `h1`, `h2`. **Site de remplissage** :
     fin de la résolution, avec la MÊME composition que les propriétés existantes — `Update` peut
     appeler `MoveWithCollisions` deux fois (déplacement de sol hérité, puis déplacement de vitesse,
     `:217-237`) et publie la SOMME, exactement comme `LastRequestedDisplacement`/
     `LastActualDisplacement` : le rapport publie donc lui aussi la somme du pas, jamais le seul
     second appel, pour ne pas contredire ces propriétés.
  2. **Moitié sol** — `IsGrounded`, normale, tag de surface, collider porteur : **état du sol tel
     que résolu par le dernier `Update`** (`UpdateGround` n'est appelée que depuis `Update`,
     `:244` — `Move` ne rafraîchit pas le sol). Cette fraîcheur est un CONTRAT écrit, pas un
     hasard : un consommateur qui appelle `Move` deux fois par tick lit le sol du dernier `Update`.
- **Remise à zéro (contrat explicite)** : la moitié déplacement est remise à zéro à l'ENTRÉE de
  chaque `Update` et de chaque `Move`, y compris sur tous les chemins de sortie anticipée
  (`:168-193`, `:232-235`, `:374-387`) — un pas sans déplacement publie donc des zéros et aucun
  drapeau, jamais l'état d'un pas antérieur. `Stop`, `Teleport` et `RestoreStateSnapshot`
  (`:355-370`, `:501-531`) effacent le rapport entier.
- **Sémantique du drapeau « axe raboté », par chemin (le point le plus subtil)** :
  - **Chemin champ** (`ICollisionField`) : les deux booléens sont AUTORITAIRES, plumés depuis les
    branches qui forcent déjà `h1Amount`/`h2Amount` à 0 (`:1023-1071`) — c'est exactement ce que la
    DLL re-déduit aujourd'hui à l'epsilon.
  - **Chemin sweep** (corps physiques) : la boucle raboté par fraction de hit / skin width / glisse
    (`:972-1013`) n'a AUCUNE notion d'axe. Les booléens par axe restent donc **false** sur ce
    chemin — les re-dériver à l'epsilon serait reproduire le défaut que M2 supprime. Le rapport
    porte à la place un indicateur distinct « le sweep a touché » alimenté par le hit existant
    (`LastCollisionHit`). Documenté noir sur blanc : booléens par axe = champ uniquement.
- **Données de sol publiées, par chemin** : chemin champ → `IsGrounded`, `SurfaceTag` du coin qui a
  fourni la hauteur MAXIMALE de la sonde 4 coins (règle de sélection explicite ; les 4 coins peuvent
  porter des tags différents), normale = `up` (convention actuelle du chemin champ, `:1296` —
  `HeightGridCollisionField` ne produit pas de normale de pente), collider = null ; chemin sweep →
  normale et collider issus du hit. Cela impose de faire remonter `GroundSample.SurfaceTag` hors de
  la sonde 4 coins (`:1300-1334`, qui ne garde aujourd'hui que `GroundHeight`) — modification
  interne, sans changement de comportement.
- **Interaction avec M1** : en mode pas fixe, un lecteur cadencé à la frame voit le rapport du
  DERNIER pas de la frame (N pas → dernier gagne). À documenter sur la propriété.
- **Additif** : `Move` garde sa signature (retour `Vector3`) ; `LastRequestedDisplacement`,
  `LastActualDisplacement`, `LastCollisionHit`, `GroundCollider` restent inchangées et coexistent
  avec le rapport (elles sont déjà consommées ; le rapport les décompose, ne les remplace pas).
- **Acceptation** (tests `CasaEngine.Tests`) : (a) déplacement libre sur le chemin champ → aucun axe
  marqué, demandé == résolu par axe ; (b) déplacement contre une cellule non marchable → l'axe
  concerné marqué, l'autre non, valeurs par axe correctes ; (c1) sol par le champ (montage
  `TopDownElevation` + `HeightGridCollisionField`, patron `CharacterControllerFieldAwareMoverTests`)
  avec des cellules de TAGS DIFFÉRENTS sous l'empreinte → le tag publié est celui du coin de hauteur
  maximale, normale = `up`, collider null ; (c2) sol par sweep (montage `FakePhysicsWorldContext` de
  `CharacterControllerComponentTests:288-305`) → collider et normale issus du hit ; (d) composition :
  un `Update` avec déplacement de sol hérité NON NUL plus un déplacement de vitesse → les valeurs
  par axe du rapport somment comme `LastRequestedDisplacement`/`LastActualDisplacement` ;
  (e) **pas sans déplacement** (déplacement nul, ou `ControlMode.Disabled`) juste après un pas
  bloqué → rapport remis à zéro, aucun drapeau résiduel ; (f) `Stop`/`Teleport`/
  `RestoreStateSnapshot` → rapport effacé ; (g) chemin sweep bloquant → booléens par axe **false**
  et indicateur de hit vrai (sémantique documentée) ; (h) `Move` seul après un `Update` au sol → la
  moitié sol vaut celle du dernier `Update` (contrat de fraîcheur) ; (i) aucune allocation par appel
  — test de comptage si le patron existe déjà dans la suite, sinon revue explicite justifiée dans le
  rapport (struct `readonly`, champ, aucun `params`/closure) ; (j) propriétés et tests existants
  inchangés, `CasaEngine.Tests` sans nouvel échec (18 préexistants).
- **Non-goals** : porter la sémantique Alundra (`ForceAdjusted`, `CollidedWithEntityZ`,
  `RidingEntity`) dans le moteur — noms de jeu ; la DLL les dérivera du rapport plus tard (M-3).
  M2 **publie**, ne décide pas : aucun changement de la résolution elle-même.
- **Rollback** : revert du commit moteur. **Budget** : un commit moteur. **Arrêt** : si publier le
  détail par axe imposait malgré tout de restructurer la résolution.

### M1.a — Correctif : `Clear()` ne remet pas l'accumulateur à zéro ✅ (moteur, avis P3 du verifier M1)

- **Fait** : `CharacterMotionSystem.Clear()` réinitialise toutes les listes mais pas
  `_fixedStepAccumulator` (état ajouté par M1) : un rechargement de monde avec un reliquat en
  attente le transporte dans le monde suivant, qui peut exécuter un pas fixe en trop à sa première
  frame. Sans effet aujourd'hui (mode opt-in, aucun consommateur ne l'active — M-3), mais casse le
  déterminisme au rechargement dès qu'il sera utilisé.
- **Scope/acceptation** : `Clear()` remet l'accumulateur à 0 ; test — activer le mode, exécuter une
  frame partielle (sous un pas), appeler `Clear()`, puis une frame partielle : aucun pas exécuté
  (échoue sans le correctif). `ExecutedFixedStepCount` reste cumulatif depuis la construction
  (documenté comme tel) et n'est PAS remis à zéro.

## 3. Ordre

M1 puis M2 (indépendantes, mais M2 profite du montage de tests à pas fixe). Après chaque commit
moteur : bump du pointeur dans le parent. La consommation par Alundra reste hors périmètre (M-3).

## 4. Suivi

| Tranche | Statut | Commit |
|---|---|---|
| M1 pas fixe du CharacterMotionSystem | ✅ | CasaEngineMonogame dcfd57a3 |
| M1.a correctif Clear() de l'accumulateur | ✅ | CasaEngineMonogame 864e3f97 |
| M2 rapport de contact par pas | ✅ | CasaEngineMonogame ec356065 |
