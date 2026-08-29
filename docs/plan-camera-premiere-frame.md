# Plan — La caméra au démarrage : le snap dépensé sur une frame sans tick

Date : 2026-08-29. Origine : **l'utilisateur a retesté en jeu après le correctif d'ordre
(`5271400`) et le symptôme persiste** — « au tout début du jeu la caméra n'est pas à la bonne
position, corrigée très vite par le suivi ».

**Ce plan repose sur une MESURE, pas sur une hypothèse.** Les deux hypothèses précédentes — la
mienne (ordre caméra/map-events) et celle de l'utilisateur (semis manquant au chargement) — ont été
**réfutées comme cause de ce symptôme** par instrumentation du vrai `Update` sur les données réelles
de la 389.

## 1. La cause, mesurée

Le projet tourne en **pas de temps libre** (`alundra-project/AlundraGame.json:6`,
`"IsFixedTimeStep": false`). Sur tout écran plus rapide que 50 Hz, **la frame 1 ne porte aucun tick
logique**. Or :

- la boucle de map-events est **cadencée aux ticks** → ni `0x67` (caméra → entité 6) ni `0x64` ne
  s'exécutent à la frame 1 ;
- **le snap ne l'est pas** : `AdvanceCameraSmoothing` l'applique **avant** sa boucle de ticks
  (`AlundraCameraMath.cs:187-204`) et `UpdateCameraFollow` efface `_cameraNeedsSnap`
  **inconditionnellement** (`AlundraCameraDirector.cs:236-239`).

**L'unique snap est donc dépensé sur la pose de spawn du joueur.** Mesuré à 60 Hz :

| frame | ticks | cible suivie | `camera.Target` |
|---|---|---|---|
| 1 | **0** | joueur @(804,952) | **(804, −839)** ❌ |
| 2 | 1 | Entity_6 @(804,576) | (804, −821) |
| … | | | lissage `≈1/16` par tick |
| 46 | | | à moins de 16 px de la cible |

La bonne valeur est **(804, −560)** : **279 px d'écart**, plus d'une hauteur d'écran, résorbés en
**~0,8 s**. À 144 Hz, deux frames sans tick au lieu d'une. À **50 Hz verrouillé, la frame 1 est déjà
correcte** depuis `5271400`.

**Ce que fait l'original** : il exécute une **frame logique complète (`Update(1)`,
`GameEngine.cs:217`) AVANT son premier `RenderScene()` (`:225`)**. Sa première image affichée porte
donc déjà le look-at d'après les map-events — (804, 576) → (804, −560).

**Les deux hypothèses réfutées, pour ne pas les rouvrir** : l'entité 6 **est** trouvée et valide dès
la première frame porteuse de tick ; et le look-at n'est **jamais** à (0,0,0), l'adoption posant le
joueur comme cible avant la première frame — la première résolution écrit (804,952), soit
numériquement ce que le semis manquant de l'original aurait produit. Ce semis reste une divergence
réelle mais **latente**, hors périmètre ici.

**`5271400` a aidé et n'a rien cassé** (mesuré dans les deux ordres) : il rend la frame 1 exacte à
50 Hz verrouillé et décale la courbe d'un tick à pas libre. Il ne pouvait pas suffire — sur une frame
sans tick, il n'y a aucun map-event à voir « en premier ».

## 2. La contrainte qui décide de la conception

**Le correctif NE DOIT PAS toucher `AlundraLogicClock`.** Les **deux harnais dorés construisent leur
propre instance** de cette classe (`IntroTraceHarnessTests.cs:1146`, `HeroTraceHarnessTests.cs:224`)
et épinglent des comptes de ticks — le harnais d'intro est même documenté comme produisant
« exactement 1 tick par frame, pour toujours ». Amorcer un tick dans la classe déplacerait **les six
goldens et tous les jalons de frame** (554, 555, 678, 801, 1034, 1202, 1704). Ce sont les oracles du
projet : **c'est une condition d'arrêt, pas un ré-étalonnage.**

Le correctif est donc porté **dans `AlundraWorldProxy`**, dont aucun harnais doré ne traverse
l'`Update`.

**Décision annexe, à ne pas confondre avec un revirement** : la décision « ne rien changer à
l'horloge » de `ed8f729` portait sur la **précision de l'accumulateur** (ne pas passer en `double`
pour un décalage de phase d'un tick). Elle n'est **pas** contredite ici : on ne touche ni
l'accumulateur ni la classe.

## 3. Le correctif (décision utilisateur du 2026-08-29)

**Deux changements, tous deux dans le proxy et le directeur de caméra :**

1. **Tick garanti au premier passage — le plancher vit dans
   `AlundraWorldProxy.LogicTicksThisFrame`, PAS dans un drapeau consommé par `Update`**
   (correction de relecture, et le point le plus important du plan). Le moteur met à jour **toutes les
   entités avant** le proxy de monde, et chaque entité lit son compte de ticks par
   `ScriptHost.LogicTicksThisFrame` → `AlundraWorldProxy.LogicTicksThisFrame`. Un drapeau consommé
   dans `Update` donnerait donc **un tick au monde et zéro aux entités** à la frame 1, et laisserait la
   chronologie du monde **définitivement en avance d'un tick** sur celle des entités — exactement
   l'invariant que la doc de classe d'`AlundraLogicClock` donne comme raison d'avoir **une seule**
   horloge partagée (« must all agree on how many ticks happened this frame to stay in lock-step »).
   Ce serait aussi infidèle : le `Update(1)` de `GameEngine.cs:217` est une **frame logique complète**
   (`UpdateWorld` → `RunMapEvents` **et** `UpdateEntities`), pas une passe de niveau monde.
   Le plancher est donc appliqué dans `LogicTicksThisFrame`, que **tous** les appelants traversent
   déjà — **et il doit être COLLANT SUR TOUTE LA FRAME, pas « au premier appel »** (correction de
   relecture, et c'est le point qui décide de la justesse du correctif). Le mémo d'`AlundraLogicClock`
   cache le compte **AVANT** plancher (`_ticksThisFrame = ticks`, brut, `AlundraLogicClock.cs:55-79`)
   et le §2 interdit d'y toucher : un plancher appliqué au seul premier appel rendrait donc **1 au
   premier appelant et 0 à tous les suivants de la même frame** — en production l'entité n°1, puis
   zéro pour les entités suivantes **et pour la lecture d'`Update` elle-même**. Le défaut en jeu ne
   serait même pas corrigé, et **le test 1 ne pourrait pas le voir** : dans le montage headless rien
   ne pilote l'`Update` des entités, donc le proxy est lui-même le premier appelant et reçoit le tick
   planché.
   **Forme retenue** : un drapeau de niveau proxy appliqué à **chaque** appel de
   `LogicTicksThisFrame` tant que la première frame est ouverte, **effacé exactement une fois** au
   site de clôture de frame, à côté de `_logicClock.CloseFrame()` (`AlundraWorldProxy.cs:1100`).
   Portée : `AlundraWorldProxy` seul, **`AlundraLogicClock` n'est pas touchée** (§2).
2. **Snap cadencé, et la garde vit dans `AlundraCameraDirector.UpdateCameraFollow`** — à l'endroit où
   `_cameraNeedsSnap` est consommé et effacé — **et surtout PAS dans
   `AlundraCameraMath.AdvanceCameraSmoothing`**, dont le contrat pur « `needsSnap: true`,
   `ticksThisFrame: 0` accroche » est épinglé par `AlundraWorldProxyCameraFollowTests` à trois sites
   d'appel. Déplacer la garde dans la fonction pure les ferait passer au rouge et inviterait
   précisément au ré-étalonnage que ce plan interdit. Sans cette garde, une frame sans tick pourrait
   encore dépenser un snap — aujourd'hui la frame 1, demain n'importe quel snap armé plus tard.

Les deux sont nécessaires : (1) rend la frame 1 correcte, (2) rend le mécanisme correct. (1) sans (2)
laisserait le défaut latent ; (2) sans (1) ne ferait que retarder le snap d'une frame, pendant
laquelle la caméra afficherait la cible sérialisée de l'asset — **(624, −480)**, le centre de la
carte (`alundra-project/Entities/AlundraCamera.entity`).

## 4. Tranche unique — le test échoue d'abord

1. **Test de reproduction, écrit avant le correctif et vu ÉCHOUER.** Montage du diagnostic :
   `World` headless, vraies données 389 installées comme `_tileMapData`, vrai document d'évènements,
   vrai runner, joueur ensemencé comme le fait `AdoptPlayerPawn` (tuile 33/59 → px 804/952,
   `EntityFollowedByCamera` = joueur), 14 records spawnés par le vrai chemin, map-events construits
   par `BuildMapEvents`. Piloter `Update(1f/60f)` **une fois** et assérer
   `camera.Target == (804, −560)`. **Doit échouer sur `(804, −839)`** avant correctif. S'il passe du
   premier coup, il ne teste pas le défaut : arrêt.
1 bis. **Second test, sans lequel la moitié du correctif partirait non prouvée** (correction de
   relecture) : avec le tick garanti, la frame 1 **porte** un tick, donc retirer la garde de snap ne
   changerait rien au premier test — la mutation ne mordrait pas. Il faut donc un test **du snap sur
   une frame SANS tick** : armer le snap, piloter une frame à zéro tick, assérer que la caméra **n'a
   pas** accroché, puis qu'elle accroche à la frame porteuse de tick suivante. Les deux tests sont
   écrits **avant** le correctif.
1 ter. **Test du plancher collant** (correction de relecture — sans lui, une implémentation
   « premier appel seulement » passerait les tests 1 et 1 bis tout en laissant le défaut en jeu) :
   sur un proxy neuf, appeler `proxy.LogicTicksThisFrame(1f/60f)` **deux fois de suite** et assérer
   que **les deux** rendent 1 ; puis, après qu'un `Update` a clos cette frame, assérer qu'une frame
   suivante à `1f/60f` rend son compte **brut**. Ce test échoue contre un drapeau consommé au premier
   appel.
2. **Le correctif** du §3, et rien d'autre.
3. **Trois mutations obligatoires, chacune appariée au test qu'elle doit casser** : retirer le tick
   garanti → le **test 1** échoue sur `(804, −839)` ; retirer la garde de snap → le **test 1 bis**
   échoue (le snap redevient consommable sans tick) ; rendre le plancher consommable au premier appel
   au lieu de collant → le **test 1 ter** échoue (deuxième appel à 0).
4. **Non-régression stricte, chaque point étant une condition d'arrêt s'il bouge** :
   - les **six items de caractérisation** d'`Update` restent verts et **inchangés** ;
   - le test d'ordre de `5271400` reste vert et inchangé ;
   - **les six goldens ne bougent pas** (`git status --short docs/` vide **après** preuve positive
     d'exécution : `alundra-project/Maps` présent, mtime des six postérieure au début du run) ;
   - les tests d'horloge existants (`AlundraLogicClockTests`, `AlundraWorldProxyLogicClockTests`)
     restent verts et **inchangés** — si l'un bouge, c'est que le correctif a atteint la classe
     d'horloge, ce que le §2 interdit ;
   - **`AlundraWorldProxyCameraFollowTests` reste vert et inchangé** — il épingle à trois sites que
     `AdvanceCameraSmoothing(needsSnap: true, ticksThisFrame: 0)` accroche. S'il bouge, c'est que la
     garde a été posée dans la fonction pure au lieu du directeur (§3.2).

**Acceptation** : build 0 erreur ; `Alundra.Tests` 596 + 3 (tests 1, 1 bis et 1 ter) ; convertisseur 138 ; **validation en jeu
par l'utilisateur** — plus de glissement de caméra au démarrage, et ce à sa fréquence d'écran réelle.
**Budget** : un commit, ≤ 2 tours. **Arrêts** : ceux du point 4, plus toute modification de
`AlundraLogicClock`.
