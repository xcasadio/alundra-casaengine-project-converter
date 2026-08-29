# Plan — Caractériser `AlundraWorldProxy.Update` puis découper sa câblerie

Date : 2026-08-29. Demande de l'utilisateur, dans cet ordre : « fais aussi le test de
caractérisation d'`Update` **et** découpe la câblerie ». Suite différée de
[plan-decoupage-proxies.md](plan-decoupage-proxies.md), dont la décision R-1 avait explicitement
laissé cette zone de côté **parce qu'elle n'a aucune couverture**.

**L'ordre est le fond du plan** : un test de caractérisation écrit *après* un découpage ne fige pas
le comportement d'origine, il fige celui qu'on vient d'écrire — erreurs comprises. Écrit *avant*, il
devient l'oracle qui rend l'extraction vérifiable. S1 ne touche donc à **aucun** code de production.

## 1. Faits établis par la reconnaissance (2026-08-29)

**`Update(float)` = lignes 1035-1115, 13 étapes**, un seul retour anticipé, deux verrous, un
passage de relais de caméra :

| # | ligne | étape | observable ? |
|---|---|---|---|
| 1 | 1049 | `LogicTicksThisFrame(elapsedTime)` | **non** directement (voir ci-dessous) |
| 2 | 1060 | `ResolveDebugCameraOnce` | `_debugCamera.Zoom`, `.PixelSnap` |
| 3 | 1061 | `UpdateCameraFollow(ticks)` | `_debugCamera.Target` |
| 4 | 1062 | `UpdateDebugCameraPan(dt)` | `_debugCamera.Target` |
| 5 | 1065 | `ApplyOriginalBackgroundClearColorOnce` | vues du `ViewManager` |
| 6 | 1066 | `UpdateAndDrawBackdrop(dt)` | files du renderer de sprites |
| 7 | 1068-1076 | boucle map-events (gardée joueur) | état des programmes |
| 8 | 1082 | `_cellVisualSync?.FlushPendingOverlayReconstruction()` | overlay |
| 9 | 1084-1090 | **retour anticipé si 0 entité** (+ `CloseFrame`) | — |
| 10 | 1092 | `RefreshUpdateProxiesAndCollidables` | listes |
| 11 | 1096-1099 | boucle de rattrapage d'évènements | état des programmes |
| 12 | 1106-1109 | passe d'interleave des murs | `Elevation` des entités |
| 13 | 1114 | `_logicClock.CloseFrame()` | — |

**Les cinq membres de câblerie visés par le découpage occupent les étapes 2 à 6** — donc **avant** le
retour anticipé de l'étape 9. Conséquence exploitée par S1 : **dans un monde sans aucune entité, toute
la câblerie tourne quand même à chaque frame.**

**L'absence de couverture est confirmée, et plus forte qu'annoncé** : non seulement aucun test
n'appelle `Update`, mais aucun ne peut l'atteindre indirectement — `World.Update` n'appelle le proxy
que si `Game?.ExecutionPolicy.UpdateGameplayScripts` est vrai (`World.cs:492-494`), et aucun test
n'appelle `InitializeWithWorld`, seul écrivain de `_world` (`:419`).

**Mais `Update` ne lève pas** : ses 13 étapes sont toutes gardées contre le nul, donc
`new AlundraWorldProxy().Update(dt)` s'exécute déjà de bout en bout — en **no-op**. 9 étapes sur 13
abandonnent sur un champ nul.

**Porte d'entrée sans jeu vivant, avec sa condition RÉELLE** (correction de relecture — la première
rédaction avait l'ordre faux) : `InitializeWithWorld` pose `_world` (`:419`) **puis appelle
`_backdropRenderer.Load(world, ...)` en `:445`, donc AVANT le retour anticipé de `:447-452`**. Ce
`Load` touche `world.Game.GraphicsDevice` (`BackdropRenderer.cs:121`) et l'`AssetContentManager`
(`:143`). **La porte ne tient que parce que `BackdropLoader.Load` rend null quand le nom du monde n'a
pas d'index de carte final** (`BackdropLoader.cs:37-43`) — condition tacite dont dépend tout S1, donc
écrite ici : **le monde de test doit porter un nom sans suffixe `-<chiffres>`**. Seconde porte,
précédent existant (`BackdropRendererTests`) : la réflexion.

**Peupler `World.Entities` : une seule voie praticable ici** (correction de relecture — la première
rédaction en désignait une qui ne peut pas marcher). `World.AddEntity` ne fait que mettre en file
`_baseObjectsToAdd`, et `InternalAddEntities` **ne fonctionne pas sans `Game`** : elle passe par
`Entity.InitializeWithWorld` → `CameraComponent.InitializeWithWorld`, qui déréférence
`Owner.World.Game.ScreenSizeWidth` (`CameraComponent.cs:95-96`) ; l'exception est **avalée** par le
`catch` (`World.cs:697-711`) **avant** le `_entities.Add` (`:703`), donc l'entité caméra n'entre
jamais. `World.LoadContent` déréférence de son côté `Game.ExecutionPolicy` (`World.cs:240`).
**Voie retenue** : `world.Entities` est une `IList<Entity>` **mutable** (`World.cs:41`) — le montage
fait donc `world.Entities.Add(cameraEntity)` **directement**, et assère la présence avant la première
frame. **Fournir un `Game` pour contourner est interdit** : cela réactive le piège 5 et exige un
`CasaEngineGame` initialisé.

**Corollaire chiffré, à ne pas deviner** : `CameraComponent.InitializeWithWorld` ne tournant donc
jamais, `Viewport` reste `default`. `ResolveDebugCameraOnce` pose alors
`Zoom = Camera2dComponent.MinimumZoom` (0,0001f, valeur clampée par le setter,
`Camera2dComponent.cs:20-50`) et **non** un « hauteur de viewport / 236 ». C'est **cette** valeur que
l'item 1 doit épingler.

**Ce que la caméra rend observable — et la limite que la relecture a corrigée** : les deux étapes de
caméra tournent **intégralement sans aucun `Game`** (la branche manette s'auto-saute,
`GamePad.IsConnected` faux). **Mais cela pinne `_debugCameraOffset` à zéro, et à décalage nul l'étape 4
est l'IDENTITÉ sur `Target`** : `Target = base + 0` avec `base = ResolveDebugCameraBase(Target, ...)`
qui rend `Target` lui-même dès qu'il a changé (`AlundraCameraMath.cs:331-333`). Le pan n'a alors
**aucune trace observable**, et intervertir les étapes 3 et 4 serait indétectable. **S1 doit donc
ensemencer `_debugCameraOffset` à une valeur non nulle par réflexion** pour que le pan contribue
réellement à `Target`.

**Pièges relevés, à épingler ou à éviter** :

1. **Verrou posé trop tôt (défaut latent réel)** : `ResolveDebugCameraOnce` écrit
   `_debugCameraLookupDone = true` (`:1228`) **avant** la garde `_world != null` (`:1235`). Un seul
   `Update` appelé **avant** `InitializeWithWorld` désactive donc caméra **et** pan **pour toute la
   vie du proxy**, sans récupération possible. S1 **épingle ce comportement tel qu'il est** — c'est le
   principe d'un test de caractérisation — et le signale comme défaut à traiter séparément.
2. **`ResolveDebugCameraOnce` a DEUX sites d'appel** : `Update:1060` et `UpdateDebugCameraPan:1391`.
   Une extraction doit préserver les deux, sinon le pan perd sa résolution idempotente.
3. **L'écriture de `Target` par le suivi est INCONDITIONNELLE**, y compris sur une frame à zéro tick,
   et le commentaire du code dit pourquoi : pour que l'adoption de base du pan voie la dernière
   écriture de ce proxy. La rendre conditionnelle casserait le relais.
4. **`_cameraNeedsSnap` traverse la frontière initialisation/update** : posé par `InitializeWithWorld`
   (`:424`), consommé et effacé par `UpdateCameraFollow` (`:1349`, `:1352`).
5. **`ApplyOriginalBackgroundClearColorOnce` lève** (`NullReference`) sur tout montage qui fournit un
   `Game` sans réflexion sur `GameManager`/`ViewManager` (`:1451` garde, `:1456` déréférence).
   S1 travaille donc **sans `Game`**, où cette étape est un no-op franc.
6. **`InitializeWithWorld` remplace `EventProgramRunner`** (`:440`) : un runner enregistreur installé
   avant serait écrasé et n'enregistrerait rien — test vert et vide.
7. **`DebugCameraPanEnabledFromEnvironment` est un `static readonly`** évalué au premier usage du
   type, et le dépôt documente déjà que l'hôte xunit partagé rend cela peu fiable : passer
   obligatoirement par `SetDebugCameraPanEnabledOverrideForTests`.
8. **Le mémo d'horloge n'est pas relisible après coup** : les deux sorties appellent `CloseFrame`, qui
   remet `_frameComputed` à faux. L'étape 1 n'est observable qu'**indirectement**, par le nombre de
   tours des boucles et par la distance parcourue par la caméra.
9. **`Clone()` n'est sûr que par accident de style** : il rend un `new AlundraWorldProxy()` nu, ce qui
   est correct **tant que chaque collaborateur est construit dans un initialiseur de champ**. Un
   collaborateur créé paresseusement depuis `_world`, mis en cache statique, ou recevant une
   référence au proxy, casserait cette propriété. (Cela dit, un proxy de **monde** n'est jamais cloné
   par le moteur : `Clone` n'a qu'un site d'appel, le constructeur de copie d'`Entity`.)
10. **`Owner` est toujours nul** pour un proxy de monde (`World.LoadContent` appelle
    `Initialize(null)`), et `AlundraWorldProxy` ne le lit jamais.
11. **Le backdrop est caractérisable headless sans couture** : `BackdropRenderer.Tick` est de
    l'arithmétique pure et `Draw` ne fait qu'empiler dans le renderer de sprites, sans toucher au
    `GraphicsDevice` (précédent : `BackdropRendererTests`). Seul `Load` exige un périphérique, et il
    est appelé par `InitializeWithWorld`, **pas** par `Update`.

## 2. Enveloppe

- **Résultat** : `Update` a un oracle chiffré sur les **étapes 2 à 4** (résolution, suivi, pan), puis sa
  câblerie d'instance sort de `AlundraWorldProxy` sans changement de comportement.
  **Périmètre de preuve, énoncé sans le surestimer** (correction de relecture) : l'oracle prouve les
  étapes **2-4** seulement. Les étapes 5-6 (couleur de fond, backdrop) sont des **no-op sans `Game`**
  et **aucun oracle ne les couvre** ; leur extraction en S3 est donc garantie par la non-régression
  des suites et par la relecture chiffrée du corps déplacé, ce que le plan assume au lieu de laisser
  croire à une preuve.
- **Portée de couverture, énoncée honnêtement** : sans nouvelle couture, S1 couvre **les étapes 1 à
  6**. Les étapes 7 à 13 exigeraient d'ensemencer `_spawnedEntities`/`PlayerEntity` (réflexion ou
  couture interne) et **ne sont pas la cible du découpage** : elles restent non couvertes, et le plan
  le dit plutôt que de laisser croire à une couverture d'`Update` entière.
- **Non-objectifs** : corriger le défaut du verrou (fait 1) — il est **épinglé**, pas corrigé, parce
  qu'un test de caractérisation fige l'existant ; couvrir les étapes 7-13 ; toucher au proxy
  d'entité ; élargir `IAlundraScriptHost`/`IEntityWorldContext`.
- **Propriétaires** : `Alundra/Scripts/` et `Alundra.Tests/`. Moteur et convertisseur intouchés.
- **Acceptation globale** : build 0 erreur ; `Alundra.Tests` **589 + nouveaux** verts ; convertisseur
  **138** ; **preuve positive d'exécution des harnais** (mtime des six goldens postérieure au début du
  run — un harnais sauté rend le même vert) puis `git status --short docs/` vide.
- **Rollback** : une tranche = un commit. **Budget** : une tranche = un commit, ≤ 2 tours de
  correctifs chacune.
- **Arrêts (question à l'utilisateur)** : **une mutation obligatoire qui ne fait échouer aucun item
  arrête S1** — cela signifie que le test ne mord pas, et c'est précisément le défaut que la relecture
  a trouvé deux fois dans la première rédaction ; tout delta hors règle de preuve en S2/S3 ; toute
  dérive de golden.

## 2 bis. Suite différée — `U-1` : corriger le verrou posé trop tôt

**Identifiant `U-1`**, prérequis **S1 et S2 livrées**. Résultat attendu : `ResolveDebugCameraOnce`
pose `_debugCameraLookupDone` **après** la garde `_world != null`, de sorte qu'un `Update` prématuré
ne condamne plus la caméra à vie. **Conséquence à ne pas découvrir plus tard** : l'item 4 de S1
épingle le comportement *actuel*, donc **`U-1` rendra ce test obsolète et devra le réécrire en sens
inverse** — la mutation (b) de S1 *est* la correction de `U-1`.

## 3. Tranches

### S1 — Test de caractérisation d'`Update` ✅ `e60d174` (test seul, zéro ligne de production)

Nouveau `Alundra.Tests/AlundraWorldProxyUpdateCharacterizationTests.cs`.

**Montage — chaque point vient d'un défaut trouvé en relecture, ne pas en retirer un seul :**

- `World` headless (patron `HeroWorldFixture`) **sans `Game`** (piège 5) et dont le **nom ne porte pas
  de suffixe `-<chiffres>`**, sans quoi `BackdropLoader` ne rend pas null et `InitializeWithWorld`
  déréférence le `Game` absent (§1, porte d'entrée).
- Une entité portant un `Camera2dComponent`, **réellement présente dans `_world.Entities`** — le
  montage l'assère avant la première frame, `AddEntity` seul ne suffisant pas (§1).
- **Une cible suivie explicite** : `EntityFollowedByCamera` non nul et
  `IsLoadedNormalOrDeactivated`, dont on **déplace `PosX/PosY/PosZ` de valeurs chiffrées entre les
  frames**. Sans elle le look-at reste (0,0,0), la cible de rendu est constante, et les items 4, 5 et
  6 n'observent plus rien (blocage de relecture P2).
- **`_debugCameraOffset` ensemencé non nul par réflexion** — sinon le pan est l'identité sur `Target`
  et n'est pas observable du tout (§1). **Cet ensemencement est isolé dans UN helper privé du fichier
  de test**, nommément `SeedDebugCameraOffset`, pour que S2 n'ait qu'une ligne à re-pointer (voir
  l'exception ci-dessous).
- `SetDebugCameraPanEnabledOverrideForTests` posé explicitement (piège 7).

**Ce que le test épingle, chiffré :**

1. **Étapes 2-4, première frame** : la résolution pose `PixelSnap == true` et
   `Zoom == Camera2dComponent.MinimumZoom` (valeur chiffrée, cf. le corollaire du §1 : sans `Game`, le
   viewport reste `default`) ; `Target` porte ensuite la **somme** de la cible lissée et du décalage
   non nul — donc la contribution du pan est lisible, et non masquée.
2. **Le relais (piège 3)**, avec la perturbation qui le rend discriminant : écrire externement
   `camera.Target = X` (X ≠ lissée) **avant** une frame à zéro tick. Non muté, le suivi réécrit la
   valeur lissée et le pan l'adopte comme base ; muté (écriture conditionnelle), le pan adopte `X`.
   Les deux valeurs attendues sont chiffrées dans le test.
3. **Le verrou de résolution** : `Zoom`/`PixelSnap` ne sont posés qu'à la **première** frame —
   modifiés entre deux frames, ils ne sont pas restaurés. *(La moitié « couleur de fond » est retirée :
   sans `Game`, l'étape 5 sort à sa première ligne et ne laisse rien d'observable.)*
4. **Le défaut du verrou (piège 1), épinglé tel quel** : un `Update` **avant**
   `InitializeWithWorld` laisse la caméra définitivement non résolue — `Target` ne bouge plus, **même
   avec une cible suivie qui se déplace**, ce qui est ce qui donne des dents à l'item. Nommé pour dire
   que c'est **caractérisé, pas souhaité**, avec renvoi à l'unité de suite **`U-1`**, qui rendra ce
   test obsolète et devra l'inverser.
5. **`_cameraNeedsSnap` (piège 4)** : la première frame **accroche** la cible, les suivantes lissent —
   valeurs chiffrées, jamais « a changé ».
6. **Cadence (piège 8)** : au moins trois frames consécutives dont les `dt` produisent 0, 1 et ≥ 2
   ticks, la **distance parcourue** par `Target` différant entre les trois.

**Acceptation S1** : `git diff --stat Alundra/` **vide** (aucune ligne de production) ; nouveaux tests
verts ; suites 589 + nouveaux, convertisseur 138 ; preuve positive d'exécution des harnais puis
`git status --short docs/` vide.
**Trois mutations obligatoires, exécutées et rapportées** : (a) rendre conditionnelle l'écriture de
`Target` du suivi → **item 2** échoue ; (b) déplacer le verrou après la garde `_world` → **item 4**
échoue ; (c) intervertir les étapes 3 et 4 → **item 1** échoue (observable **grâce au décalage non
nul**). **Arrêt** : une mutation qui ne fait échouer aucun item signifie que le test ne mord pas —
on s'arrête et on corrige le test. **Budget** : un commit, ≤ 2 tours.

### Règle de preuve étendue, applicable à S2 et S3 seulement

`plan-decoupage-proxies.md` §3.1 n'autorise sur un corps déplacé que **deux** deltas — qualification
d'appel et élargissement `private` → `internal` — et fait de tout autre retouche une condition
d'arrêt. Cette règle a été écrite pour des membres **statiques et sans état**. S2 et S3 déplacent des
membres **d'instance**, ce qui exige deux deltas de plus ; sans cette extension, la condition d'arrêt
se déclencherait sur l'implémentation **correcte** (blocage de relecture). Deltas supplémentaires
autorisés, **et strictement ceux-là** :

- **(a) Substitution par paramètre d'une lecture de champ qui reste sur le proxy**, à valeur
  identique, **limitée à cette liste nommée** : les bornes de carte tirées de `_tileMapData`,
  `EntityFollowedByCamera`, la `Camera2dComponent` résolue, **et le `World` courant `_world` — avec
  tout ce qui en dérive par lecture au moment de l'usage** (`_world.Entities`, `_world.Name`,
  `_world.Game` et, à travers lui, le gestionnaire de manette, le `ViewManager`, le
  `SpriteRendererComponent` et les dimensions d'écran). **`_world` est indispensable** : quatre des
  cinq membres déplacés le lisent (`ResolveDebugCameraOnce` `:1235-1251`, `UpdateDebugCameraPan`
  `:1405, :1416`, `ApplyOriginalBackgroundClearColorOnce` `:1451-1458`, `UpdateAndDrawBackdrop`
  `:1474-1491`), et le piège 9 interdit aussi bien de le capturer à la construction que de tenir une
  référence au proxy. Toutes ces valeurs se lisent **au moment de l'usage**, jamais capturées.
- **(b) Requalification d'un accès de champ vers son nouveau porteur** — par exemple
  `_cameraNeedsSnap = true` dans `InitializeWithWorld`, qui devient un membre du directeur.

Tout le reste demeure une condition d'arrêt. Le rapport de tranche liste, **membre par membre**, la
comparaison avant/après et le delta appliqué, en montrant qu'il appartient à cet ensemble énuméré.

### S2 — Extraction de la câblerie caméra ✅ `52771a4`

`AlundraCameraDirector` (`internal sealed`), **construit dans un initialiseur de champ** (piège 9),
portant les trois membres de câblerie et **les deux sites d'appel de la résolution** (piège 2).

**Deux points que la relecture a imposé de trancher :**

- **`_tileMapData` se lit AU MOMENT DE L'USAGE, jamais capturé à l'initialisation.** Il n'est affecté
  qu'en `:462`, **après** les deux retours anticipés de `:448-452` et `:456-460` : un directeur qui le
  capterait dans un `Initialize` garderait `null` à vie et **le clamp aux bornes de carte
  disparaîtrait en silence** — que S1 ne peut pas détecter, son monde n'ayant pas de tileMap. Les
  bornes sont donc passées **par frame**. **Vérification de tranche** : montrer, sur un monde
  possédant un tileMap, que le lissage reçoit des bornes non nulles.
- **`_cameraLookAtX/Y/Z` partent avec le suivi**, et `SetForcedCameraLookAt` — membre
  d'`IEntityWorldContext`, qui **doit** rester sur le proxy — délègue au directeur. **Ce n'est pas une
  façade au sens de la règle réutilisée** : l'interdit visait les renvois créés pour éviter de mettre
  à jour des appelants ; ici l'implémentation d'un membre d'interface délègue à son collaborateur,
  ce qui est la forme normale. Le rapport de tranche liste ces quatre membres et leur classe finale.

La cible suivie (`EntityFollowedByCamera`) **reste sur le proxy** et est passée par frame — aucune
interface n'est élargie. L'ordre des étapes 2-4 dans `Update` est inchangé.
**Acceptation** : **S1 vert, et AUCUNE de ses assertions chiffrées modifiée** ; corps déplacés
identiques modulo les deltas de la **règle de preuve étendue ci-dessus** ; `using` identiques ; aucune
façade au sens de cette règle.

**Exception unique et bornée, imposée par la relecture** : `_debugCameraOffset` part avec le pan, donc
la réflexion du montage S1 qui le cible sur `AlundraWorldProxy` cesserait de le trouver et **S1
deviendrait rouge par construction**. S2 est donc autorisée à **re-pointer cette seule ligne**, celle
du helper `SeedDebugCameraOffset`, vers le nouveau porteur. C'est la **seule** modification permise
dans le fichier S1 : le rapport de tranche montre le diff du fichier de test, qui doit tenir en cette
unique ligne, **zéro assertion touchée**.

**Arrêt** : tout delta hors règle de preuve ; toute modification de S1 **autre** que cette ligne.
**Budget** : un commit, ≤ 2 tours.

### S3 — Extraction de la câblerie de rendu ✅ `7bb6738`

`AlundraBackdropStage` (`internal sealed`), même forme, portant la couleur de fond une-fois et le
tick/draw du backdrop. Dépend de la caméra (il lit `Target`), donc **après S2**, la caméra résolue
lui étant passée en paramètre plutôt que re-cherchée.
**Acceptation** : S1 vert sans modification ; **règle de preuve étendue** ci-dessus (la caméra
résolue passée en paramètre relève du delta (a)). **Limite assumée et écrite** :
ces deux membres sont des **no-op sans `Game`**, donc **aucun oracle ne les couvre** — la garantie est
la non-régression des suites plus la relecture chiffrée du corps déplacé. **Arrêt** : tout delta hors
**règle de preuve étendue** (même formulation que S2 — la rédaction d'origine, restreinte à la seule
qualification, contredisait l'acceptation de sa propre tranche). **Budget** : un commit, ≤ 2 tours.

### S4 — Clôture ✅

Crefs cassés par S2/S3 traités **par le compilateur** (activer temporairement
`GenerateDocumentationFile`, corriger, désactiver) — méthode éprouvée en R5 ; mise à jour de
`plan-decoupage-proxies.md` §5 (renvoi ici) ; mémoire.
**Acceptation** : plus aucun cref cassé **nommant un membre déplacé par S2/S3** ; `Alundra.csproj`
**identique à l'octet près** après l'expérience (`git diff --stat` vide sur ce fichier) ; build 0
erreur ; suites vertes. **Budget** : un commit.

## 4. Réalisé — résultat et écarts (2026-08-29)

**Les quatre tranches sont livrées.** `AlundraWorldProxy.cs` : **1749 → 1426 lignes**, et **2948 →
1426 depuis le début du chantier de découpage, soit −52 %**. Deux collaborateurs créés,
`internal sealed`, construits dans des initialiseurs de champ : `AlundraCameraDirector` (327 l.) et
`AlundraBackdropStage` (141 l.). Suites : `Alundra.Tests` **595** (589 + 6 de caractérisation),
convertisseur **138**, six goldens inchangés avec preuve d'exécution à chaque tranche.

- **L'ordre demandé par l'utilisateur a payé, et c'est mesurable** : S2 est la première tranche de ce
  chantier vérifiée contre un **oracle** et non contre une relecture — les six tests de S1 sont passés
  **sans qu'une seule assertion ne change**.
- **Ce que la relecture a sauvé (S1)** : deux des trois mutations obligatoires **n'auraient rien
  cassé** dans la première rédaction. Sans `Game`, le décalage du pan vaut zéro, donc l'étape de pan
  est l'**identité** sur la cible : intervertir deux étapes d'`Update` était **indétectable**. Et sur
  une frame à zéro tick, la cible vaut déjà la valeur lissée, donc supprimer l'écriture du suivi ne se
  voyait pas sans perturber la cible d'abord. Vérifié en session principale : l'interversion fait
  tomber l'item 1 avec l'écart exact du décalage, `(105,−177)` contre `(100,−184)`.
- **Prédiction du plan démentie, sans conséquence** : la correction du pointeur de réflexion dans le
  fichier S1 a demandé **deux** lignes et non une — le type déclarant et l'instance cible sont deux
  expressions distinctes. Zéro assertion touchée, ce qui était la vraie garantie.
- **Encapsulation refermée en session principale (S3)** : les tranches livrées laissaient le proxy
  **atteindre à travers** ses collaborateurs (`_backdropStage._backdropRenderer.Load(...)`, et de même
  pour le drapeau de snap en S2). C'était dans les clous de la règle de preuve, mais **une
  responsabilité dans laquelle on pioche n'est pas extraite** : les deux champs sont redevenus privés
  et chaque collaborateur expose l'opération qu'il possède (`Load`, `ArmFirstFrameSnap`).
- **Incident consigné** : un test a échoué **une fois** pendant la vérification de S2 (594/595), sans
  jamais réapparaître en dix exécutions, séquence identique et reconstruction propre comprises. Nom
  non capturé. Ce dépôt a déjà connu cette forme d'instabilité par binaires périmés.
- **Limite assumée, répétée ici pour qu'elle ne soit pas redécouverte** : les deux membres de S3 sont
  des **no-op sans `Game`**, donc **l'oracle ne les couvre pas** — leur garantie est la non-régression
  plus la comparaison ligne à ligne du corps déplacé.
- **S4** : 9 crefs repointés vers les deux nouvelles classes, trouvés par le compilateur via
  `GenerateDocumentationFile` activé puis désactivé ; csproj restauré à l'octet près ; 62 crefs
  préexistants laissés tels quels, hors périmètre.

**Reste ouvert** : l'unité **`U-1`** (§2 bis) — corriger le verrou posé avant la garde `_world`, qui
condamne la caméra pour la vie du proxy si `Update` est appelé trop tôt. L'item 4 de S1 l'épingle
**tel quel** et devra être **inversé** par `U-1`.
