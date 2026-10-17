# Contre-vérification du rapport « d5b » (E19.d2c2 D5b, plateformes qui bougent en Z)

**Verdict.** Le côté binaire est fiable : je l'ai redémarré à neuf et j'obtiens les mêmes traces. Le côté DLL est exact pour les faits lus dans le code. Mais la conception M1 à M4 est **incomplète** : elle ne couvre pas le héros qui retombe sur une plateforme qui monte. Le rapport peut servir de base aux tâches M1 à M4, à condition d'ajouter la tâche manquante décrite en §2.

Mes scripts sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/check-d5b/`. Le harnais `hh.py` est le mien, sur la copie de l'interpréteur de `e14b1/work/emu` et non sur le `mips.py` de l'auteur. Rien n'a été écrit dans le dépôt, la DLL n'a pas été exécutée.

## 1. Vérifications par affirmation

### Adresses et pseudo-code du binaire (capstone, relu)

| Affirmation | Verdict | Preuve |
|---|---|---|
| Ordre de `0x8003B388` : a `0x80038634`, b `0x800386D0`, c `0x80038998`, d `0x800384F4`, e `0x80038E18`, f `0x80038364`, g `0x80038E84` et `0x80039300` | Confirmé | `lst/pass.txt` ; porte `[0x800DC4B8] & 0x48` confirmée |
| Listes : actifs = statut 2 ou 3 et `+0x20 == 0` ; collidables = `flags & 0x80`, `AnimFlags & 0x80 == 0`, `+0x28 == 0`, aucun test de statut | Confirmé | `0x80038548-0x800385BC`. Au code réel, une entité de statut 4 reste dans la liste et porte le héros (rider = 1) |
| `DestroyEntity 0x8003A59C` n'écrit pas `+0x6C` | Confirmé | Le `sw 0x6c` de `0x80032b90` vise une entité de loot, pas l'entité détruite |
| Compteurs : `+0x12C := 0` à `0x800389DC` (`$v1` = slot + `0x1D0`) | Confirmé | |
| Prépasse `0x80038394-0x800383E0` : `+0x168`, `+0x140`, `+0x13C` à 0, Modded := Pos + Mod | Confirmé | |
| Rider `0x800364C8` : sujet `(flags & 0x4100) == 0x100`, `top + 1 == ModdedZ`, fenêtres `+1`, premier candidat écrit `+0x12C` | Confirmé | Adresses `0x800364FC`, `0x80036554-5C`, `0x80036524/28`, `0x800365B0`, `0x800365C4` toutes justes |
| Forces `0x80036828` : branche héros si l'adresse vaut `0x80127D30`, décroissance bornée, `+0xEC := +0xB8`, `+0xE4/E8 := +0xDC/E0` | Confirmé | `0x80036BB0-BB8`. Le marqueur `0x8000` n'agit que hors héros et sans bit `0x100` |
| Copie passager `0x80037364` : récursion d'abord, `+= Adjusted X/Y`, copie de Z si `+0xF8 == 0` | Confirmé | `0x80037388`, `0x8003739C-B0`, `0x800373BC-C8`. Aucun mémo : la caisse du milieu d'une pile reçoit deux fois la force |
| `MoveEntity` pose `+0x168 := 1` à `0x80037E58`, puis `ComputeZ`, puis `ComputeXY` ; déplacer d'abord la cible à `0x800376B8-DC` (atterrissage) et `0x80037630-50` (plafond) | Confirmé | |
| Passe de fin `0x80038064` après tous les déplacements, `IsOnGround := !(Floor < PosZ)` | Confirmé | `0x800380F8-0810C` |
| `ComputeXY` sans force XY va à `0x80037DC0` | Confirmé | `0x800377A0-A8` |

### Traces du code réel

Mes traces sont **identiques ligne à ligne** à celles du rapport (`diff` vide) pour : montée, descente (deux profils), quai (demi-quai et creux, montée et descente), saut depuis plateforme qui monte, téléportation (atterrissage au tick 11, 2 228 224), plateforme horizontale, pile héros/caisse/plateforme (caisse à 2 px/tick contre 1 pour la plateforme, héros immobile en XY).

Deux contrôles que le rapport n'a pas faits :
- **Mutation du binaire réel.** J'ai forcé le branchement `0x800376C8` (« la cible a déjà bougé »). La descente casse alors : `IsOnGround` alterne et le héros reste à +1, +1,5 px. La récursion « déplacer d'abord la cible » est donc bien ce qui tient le héros sur une plateforme qui descend.
- **Porteur à gravité au repos (cas d'UH-18).** `ForceZ` 0, `FinalForceZ` −32 768, contact 1 et `IsOnGround` 1 à chaque tick, comme écrit au plan.

### Modèle Python contre le code réel

J'ai fait tourner `pmodel.py`, copié chez moi, contre le code réel dans mon harnais, avec mon propre générateur : 2 à 6 entités, tours aléatoires, forces et impulsions, statuts 0 à 4, `+0x20`, `AnimFlags`, hauteurs de terrain 0, 1 ou 3.
- **3 000 vecteurs, 13 680 ticks : 0 écart.** Les riders se déclenchent 3 684 fois, les contacts 7 959 fois.
- Test exact de la limite du plafond (`cand.Z == top + F`) : réel et modèle concordent, et le mutant `ceil_le` diverge.

Mes vecteurs ne tuent pas deux mutants (`win_no_plus1`, `ceil_le`), mais la fenêtre `+1` et la limite du plafond sont confirmées par le désassemblage.

### Côté DLL (code lu)

| Affirmation | Verdict |
|---|---|
| Numéros de ligne cités : `RunEntityBlock` 2871, `RunCountersPass` 2966, `EntitySupport` 54/276/296/310, `RunOneMotionTick` 180, `RunHeroVerticalTick` 245 et 313-314, `RunOneKinematicTick` 608 et 672-675, `TickPhysics` 1589, `ApplyVerticalForceLaw` 1040, `DrivesEntityTicks` 793 | Confirmés |
| Héros : copie `ForceZ = carrier.ForceZ`, `FinalForceZ = carrier.TickForceZ`, atterrissage sur le dessus d'avant déplacement, `IsOnGround` calculé juste après, XY par `LastTickDelta` | Confirmé : le héros passe avant le porteur, donc `TickForceZ` date du tick précédent |
| Le recalcul de `RidingEntity` est déjà par tick, donc le commentaire de `RunCountersPass` est périmé et O-E20-9 se clôt avec M4 | Confirmé : `RefreshUpdateProxiesAndCollidables` est appelé à chaque tick (2931) |
| `EntitySupport.cs:52-54` croit que le binaire exclut `FlagToDestroy` | Confirmé : le binaire n'a aucun test de statut sur cette liste |
| `TryFindSupport` rend l'entité, `TryFindCeiling` non ; `A8` épingle la liste des passes (`AlundraTickStructureTests.cs:31`) | Confirmé |
| Prefab « Plateforme flottante (grande) » avec `CollisionComponent` Kinetic ; `AlundraContactTestSupport.cs:154` « invisible to the rigid sweep » | Confirmé. « Le héros ne bloque pas la montée » reste une INFERENCE, non vérifiable sans exécuter la DLL |
| Décompilation : `UpdateEntitiesPhysics` et `ComputeZPosition` ont la récursion `MoveEntity(platformEntity)` (`PhysicsEngine.cs:138/161`) et la copie passager | « Cette récursion n'est pas dans le plan » est vrai pour le plan, mais ce n'est **pas** un écart de la décompilation. Les seuls écarts vus sont ceux déjà connus : `Depth + 1` dans `CheckRidingEntities` (F1), `RidingEntity` effacé seulement pour les entités à gravité |

### Modèle de la DLL et ablation

J'ai refait l'expérience avec **mon** harnais comme oracle binaire, sur 600 scénarios sans saut (graine 11) :
- ordre actuel faux sur 594 scénarios, ordre proposé faux sur 0 ;
- ablations (400 scénarios, graine 21) : sans la force du tick du porteur 396 faux, sans le déplacement préalable 347 faux, sans la passe de fin 357 faux.

C'est reproduit, mais avec deux réserves :
- Le modèle « proposé » rejoue l'algorithme du binaire sur des entrées simplifiées. Il prouve que chaque mécanisme est nécessaire, pas que les modifications de la DLL réelle suffisent.
- Le modèle suppose le héros déjà dans l'état « tenu ». Le rapport le dit lui-même (INFERENCE).

### Recensement §9

Je l'ai relancé depuis une copie de ses scripts : mêmes chiffres (247 enregistrements sur 88 cartes ; 16 sites `0x21` sur les cartes 52, 83, 148, 152, 154, 344, 410, 423). C'est la même logique, donc reproductible mais pas indépendant. Le comptage « tous les branchements ouverts » reste une borne haute. Le semantique de `0x1B` et `0x21` est confirmée au désassemblage : `0x1B` écrit `ForceZ = param << 8`, `0x21` attend `|ΔZ| >= N` ou un contact Z. `0x52` est bien « Use portal » : la correction du sujet tient.

## 2. Ce qui manque (point bloquant)

**Le héros qui retombe sur une plateforme qui monte n'est pas couvert.**

Dans le binaire, quand le porteur monte (`F > 0`), son `ComputeZ` appelle le plafond `0x80036D94` sur toutes les collidables, dont le héros en l'air. Le héros a déjà bougé (slot 0), donc le porteur s'arrête sous ses pieds et le héros se pose.

Cas minimal au code réel : plateforme qui monte de 3 px/tick, héros à 1 px au-dessus, avec gravité.
- Le porteur ne monte que de 32 768 au lieu de 196 608.
- Le héros est au repos (`rel` 0) dès le tick 0 et porté dès le tick 1.
- Avec 1 px/tick et 0,25 px de jeu : le porteur monte de 16 384, contact 1.

Dans la DLL, le plafond d'entités n'est appelé que pour le héros (`AlundraScriptedMotion.cs:354`) et, pour les entités marquées `HasLogicalZ`, avec une liste vide (`:498`, le commentaire parle de O-E0K-6). Les porteurs à contrôleur montent par `Controller.Move`, sans plafond d'entités.

Avec M1 à M4 seuls, le modèle du rapport donne donc un porteur qui traverse le héros : `rel` −2,5 px à −27 px sur six ticks, aucun appui retrouvé ensuite.

Mesure sur scénarios avec sauts (mon harnais comme oracle) :

| Série | Ordre proposé faux sans plafond porteur | Avec plafond porteur |
|---|---|---|
| graine 12 | 73 / 600 | 1 / 600 |
| graine 14 | 78 / 600 | 4 / 600 |
| graine 31 | 48 / 500 | 1 / 500 |
| graine 32 | 63 / 500 | 4 / 500 |

Le recensement donne des forces jusqu'à +4 px/tick sur les plateformes flottantes. Sauter sur un ascenseur est le geste courant.

**Tâche à ajouter :** porter, pour les entités non héros qui montent, l'arrêt sous une collidable non encore déplacée, y compris le héros en l'air. Et des tests « atterrissage sur plateforme qui monte ». La partie entité de `0x80036D94` y suffit, car c'est le test de plafond qui coupe la montée.

**Autres manques, moins graves :**
- Le rapport n'évoque que le chemin porteur à contrôleur (`EvaluateEntitySupport`). Il existe aussi `StepVerticalWithoutController` (12 prefabs sans contrôleur) et `RunLogicalZPreXyStep`. Le site unique de la loi `ApplyVerticalForceLaw` couvre les trois pour M1, mais M2 (« déplacer d'abord ») doit les traverser tous.
- M3 est sous-spécifié en mode autonome. Chaque entité y joue **tous** ses ticks (entité par entité, `RunSelfDrivenTicks`), donc une passe de fin « après la boucle » n'existe pas : il faut un appel de test. Le rapport ne traite l'estampille `PhysicsPassId` que pour ce mode, pas la passe de fin.
- Le binaire déplace une cible collidable même inactive (statut 4, `+0x20`) à son premier contact. Le garde `TookPartInFrame` du rapport s'en écarte, sans conséquence visible.
- Le rapport dit « quatre écarts dans la passe physique » : le quatrième (`RidingEntity`) relève de la passe des compteurs.
- Autres lecteurs de `+0x12C` dans le binaire non listés : `0x8002EE10` (prise d'objet), `0x8003CBE4` (recherche), `0x8003F444` et `0x8003F4D0` (0x3E). Ils tournent avant les compteurs, donc M4 ne les change pas.

## 3. Non vérifié

- Couvertures de code (100 %, 71,5 %, 36 %) et le décor du plafond de 1 920 px : mes vecteurs ne les mesurent pas bien (les créneaux de délai ne sont pas comptés par mon instrumentation).
- Comportement réel de la DLL : épingles UH-10/11/14/15/18, six traces à l'octet, cartes 389 et 391. Le rapport les marque INFERENCE ou UNKNOWN, à raison.
- Défaut des piles XY : confirmé dans le binaire, mais le recensement ne dit pas si le corpus en contient.

## 4. Fiabilité pour écrire les tâches

- **M1, M2, M3, M4** : appuyées par le binaire, adresses et ordre exacts, ablation reproduite. Écrire ces tâches est possible.
- **Avant de figer le découpage** : ajouter la tâche « plafond des porteurs » du §2 et les tests de retombée sur plateforme qui monte. Préciser M3 en mode autonome.
- **Questions de l'auteur du rapport (§11)** : pertinentes. La question 1 (héros seul ou tous les passagers) se pose avec encore plus d'acuité, car la règle du plafond touche aussi les caisses.
- **Tâches T1 à T3** : les valeurs écrites d'avance tirées du binaire (traces §4) sont exploitables telles quelles ; j'ai la même série de valeurs ligne à ligne.