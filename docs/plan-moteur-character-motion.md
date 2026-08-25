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

### M1 — Pas fixe du `CharacterMotionSystem` ⏳ (moteur, plan-verifier)

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

### M2 — Rapport de contact par pas ⏳ (moteur, plan-verifier)

- **But** : que le mover publie ce qu'il sait déjà, au lieu que chaque jeu le re-déduise.
- **Scope** : `CharacterControllerComponent` expose, pour le dernier pas, un
  `readonly struct` de rapport de contact contenant au minimum : déplacement demandé et résolu
  **par axe de la base de la politique** (`up`, `h1`, `h2`) ; un indicateur « axe raboté » par axe
  horizontal (ce que la DLL calcule aujourd'hui avec un epsilon) ; l'état de sol (`IsGrounded`,
  normale, tag de surface quand il vient d'un `ICollisionField`) ; le collider porteur (déjà connu
  via `GroundCollider`/`LastCollisionHit`). Exposé en propriété (`LastContact`), rempli en place,
  **sans allocation**. Strictement additif : `Move` garde sa signature (retour `Vector3`) et les
  propriétés existantes (`LastRequestedDisplacement`, `LastActualDisplacement`, `LastCollisionHit`,
  `GroundCollider`) restent inchangées.
- **Acceptation** (tests `CasaEngine.Tests`, montage `TopDownElevation` +
  `HeightGridCollisionField` du patron E3.c) : (a) déplacement libre → aucun axe marqué raboté,
  demandé == résolu ; (b) déplacement contre une cellule non marchable → l'axe concerné est marqué,
  l'autre non, demandé/résolu corrects par axe ; (c) atterrissage → sol présent, normale et tag de
  surface attendus, collider renseigné quand le sol vient d'un corps ; (d) le rapport reflète le
  DERNIER pas (deux `Move` successifs → le second gagne) ; (e) aucune allocation par appel (test de
  compteur d'allocations, ou revue explicite justifiée) ; (f) propriétés et tests existants
  inchangés, `CasaEngine.Tests` sans nouvel échec.
- **Non-goals** : porter la sémantique Alundra (`ForceAdjusted`, `CollidedWithEntityZ`,
  `RidingEntity`) dans le moteur — ce sont des noms de jeu ; la DLL les dérivera du rapport plus
  tard (M-3). Aucun changement de la résolution elle-même : M2 **publie**, ne décide pas.
- **Rollback** : revert du commit moteur. **Budget** : un commit moteur. **Arrêt** : si publier le
  détail par axe impose de restructurer la résolution (le scope changerait et repasserait en revue).

## 3. Ordre

M1 puis M2 (indépendantes, mais M2 profite du montage de tests à pas fixe). Après chaque commit
moteur : bump du pointeur dans le parent. La consommation par Alundra reste hors périmètre (M-3).

## 4. Suivi

| Tranche | Statut | Commit |
|---|---|---|
| M1 pas fixe du CharacterMotionSystem | ⏳ | |
| M2 rapport de contact par pas | ⏳ | |
