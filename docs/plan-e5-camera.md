# Plan E5 — Caméra suivant l'entité désignée

Date : 2026-08-25. Étape E5 de [plan-conversion-totale.md](plan-conversion-totale.md) §4. Chantier
**DLL** (aucune modification moteur prévue : les capacités nécessaires existent). Verifier frais
après la tranche.

## 0. Décisions de l'utilisateur (2026-08-25, ne pas re-débattre)

| # | Décision |
|---|---|
| E5-1 | **Cadrage fidèle** : la caméra montre la zone visible d'origine (320×240 unités logiques autour du point suivi), mise à l'échelle pour remplir la fenêtre, `PixelSnap` actif, et le clamp aux bords de map reprend la formule originale. Les décors sont dessinés pour ce cadre ; c'est la seule option où la chorégraphie de l'intro se lit comme dans le jeu d'origine. |
| E5-2 | **Lissage flottant continu**, au même taux de rattrapage que l'original mais **exprimé en fonction du temps** — `facteur = 1 − (15/16)^(dt × 50)` — donc identique à 50, 123 ou 240 im/s (on ne réintroduit pas la dépendance à la cadence corrigée en E4). Écart assumé : pas d'arrêt résiduel entier (l'original s'arrête quand l'écart passe sous 16 px, notre convergence est asymptotique). |

## 1. Faits qui bornent le plan

**Original** (`alundra-datas-analyser/AlundraTools/AlundraEngine/`) :

- `g_entityFollowedByCamera` : initialisé au joueur (`GameEngine.cs:644`) ; écrit par 0x67
  (`EntityEventHandlers.cs:2073`, première entité du résultat de recherche — donc `null` si la
  recherche ne trouve rien), remis à `null` par 0x68 (`:2080`) et 0x69 (`:2087`).
- Suivi par frame (`GameEngine.cs:1747-1752`, dans `UpdateEntities`) : **seulement si la cible est
  non nulle ET `IsLoadedNormalOrDeactivated`** → `g_cameraLookAtX/Y/Z = cible.PosX/Y/Z >> 16`. Une
  cible détruite **fige la caméra** sur sa dernière position et n'est jamais remise à `null`
  automatiquement.
- 0x69 (7 octets) : `null` + position caméra imposée `g_cameraLookAtX/Y/Z` depuis `variables[1..6]`.
- Lissage (`GraphicManager.cs:75-92`) : à l'entrée de map `g_isCameraScrolling = 1` → **snap**
  `scrollX = X − 0xa0`, `scrollY = (Y − Z) − 0x88` ; ensuite, chaque frame,
  `scrollX += (X − (scrollX + 0xa0)) >> 4` et `scrollY += ((Y − Z) − (scrollY + 0x88)) >> 4`.
  Centre écran (0xa0, 0x88) = (160, 136) — noter que 136 n'est pas la moitié de la hauteur visible :
  **le point suivi n'est pas au centre géométrique de l'écran**, il est décalé ; ce biais doit être
  porté tel quel.
- Clamp (`GraphicManager.cs:97-122`) : `scrollX ∈ [0, 0x39f]` (927), `scrollY ∈ [0, 0x2cf]` (719) —
  cohérent avec une vue 320×240 dans une map de 1248×960 px (52×60 cellules de 24×16), qui est
  exactement la taille de la 389. **À vérifier dans la tranche** : ces bornes sont-elles des
  constantes globales ou dérivées de la taille de map ? Notre port les **dérive de la taille de map**
  (`TileMapData.MapSize` × 24/16 − taille visible) et documente l'écart si les constantes de
  l'original s'avéraient figées.
- Projection : `Y_écran = Y − Z` — même transformation que notre politique `TopDownElevation`
  (`DeriveRenderPosition` = `(X, −(Y − Z), 0)`, au signe d'axe près que la politique gère déjà).

**Moteur** (`CasaEngineMonogame/`) :

- La vue runtime résout la **première `CameraComponent`** trouvée
  (`DefaultRuntimeViewBootstrapper`) : c'est le `Camera2dComponent` de l'entité `AlundraCamera`,
  celui que le pan de debug pilote déjà.
- `Camera2dComponent` : **orthographique**, `Target` (Vector3, sûr à écrire chaque frame), `Zoom`
  (zone visible = viewport / zoom), `PixelSnap` (accroche la vue à la grille de texels **sans**
  modifier `Target`). Aucun clamp aux limites : c'est à l'appelant de le faire.
- `CameraTargeted2dComponent` dérive de `Camera3dComponent` : **perspective**, donc incompatible
  pixel-perfect — **non utilisé** (le point « à valider » du plan maître est ainsi tranché).
- Le rendu lit `ViewMatrix`/`ProjectionMatrix` après tous les `Update` de la frame.

**DLL** (ce repo) : `AlundraWorldProxy.UpdateDebugCameraPan` maintient déjà `Target = base + offset`
et **adopte comme nouvelle base toute écriture externe** (travail du 2026-08-24, `bca9338`) — le
suivi scripté écrit donc la base sans que le stick droit ne le perturbe, et `ALUNDRA_DEBUG_CAMERA_ENABLED`
neutralise l'offset.

## 2. Tranche E5.a — Suivi scripté de la caméra ⏳ (DLL)

- **But** : la caméra suit la cible désignée par les scripts, avec le cadrage et le rattrapage de
  l'original ; au bout de l'intro elle revient au héros.
- **Scope** :
  1. **État** : `AlundraWorldProxy.EntityFollowedByCamera` (port de `g_entityFollowedByCamera`),
     initialisé sur le héros à l'adoption du pawn (port de `GameEngine.cs:644`).
  2. **Opcodes** : 0x67 (2 octets, première entité de `EntitySearchService` ; `null` si aucune —
     fidèle), 0x68 (1 octet, `null`), 0x69 (7 octets, `null` + position imposée). Retirés du
     décompte « non implémentés » du harnais.
  3. **Suivi par tick logique** : si la cible est non nulle ET son `Status` est
     Loaded/Normal/Deactivated → `lookAt = (cible.PosX, PosY, PosZ) >> 16` ; sinon **la caméra ne
     bouge pas** (cible détruite = gel, port fidèle, pas de repli sur le joueur).
  4. **Cadrage (E5-1)** : à `InitializeWithWorld`, la DLL pose sur le `Camera2dComponent`
     `Zoom = viewportHeight / 240` (la fenêtre 1280×944 donne exactement 4 → zone visible 320×236,
     à confirmer sur les valeurs réelles) et `PixelSnap = true`. **Réglage runtime, pas d'asset
     modifié** → aucun export complet nécessaire ; si l'asset `AlundraCamera.entity` porte déjà un
     zoom, la DLL l'écrase et le documente.
  5. **Position caméra** : `Target` = pose de rendu du `lookAt` via la politique, **plus le biais de
     centre de l'original** (le point suivi est à (160, 136) du coin, pas au centre géométrique) ;
     la formule exacte est à dériver et à figer par un test chiffré sur la position New Game.
  6. **Lissage (E5-2)** : snap à l'entrée de map (port de `g_isCameraScrolling = 1`), puis
     rattrapage `cur += (cible − cur) × (1 − (15/16)^(dt × 50))` — identique à toute cadence.
  7. **Clamp** : vue maintenue dans la map, bornes dérivées de `TileMapData.MapSize`
     (largeur × 24, hauteur × 16, moins la zone visible), appliquées **après** le lissage comme
     l'original.
  8. **Debug** : le pan du stick droit reste un offset par-dessus la base (déjà en place) ; le suivi
     scripté écrit la base.
- **Non-goals** : `InitializeScrollingMode` complet (modes de scroll par map), effets de warp/fondu
  (E10), tri de profondeur (E8), transitions de map.
- **Acceptation** :
  - Tests DLL (patron `AlundraWorldProxyTests`/`AlundraEventProgramRunnerTests`) : 0x67 sur une
    recherche réelle de la 389 désigne la bonne entité ; 0x67 sans résultat met `null` ; 0x68/0x69
    (valeurs réelles si un programme de la 389 en contient) ; cible détruite → la caméra garde sa
    position (échoue si l'on replie sur le joueur) ; formule de cadrage : à la position New Game du
    héros, `Target` vaut la valeur calculée à la main (biais de centre compris) ; lissage : partant
    d'un écart connu, la position après N ticks est celle du taux 1/16 par tick de 50 Hz, **et
    identique à dt = 1/50, 1/123 et 1/240** (échoue avec un lissage par frame) ; clamp : une cible
    près d'un bord de la 389 ne fait pas sortir la vue de la map.
  - Harnais d'intro : les six changements de cible apparaissent comme `Implemented` ; **jalons
    inchangés** (554 / 555-678-801 / 1034 / 1202 / 1704, arrêt condition (a)) — la caméra n'influe
    sur aucun flag. Trace régénérée et commitée si les kinds changent.
  - Suites : build 0 erreur ; `Alundra.Tests` 454 + nouveaux ; convertisseur 137 inchangé.
  - Runtime (utilisateur) : la caméra suit la mouette, le bloc 10 qui descend, les marins 11 puis
    12, le bloc 18, puis revient sur Alundra ; le cadrage ressemble à l'original ; le stick droit
    décale toujours la vue sans la perturber.
- **Rollback** : revert du commit. **Budget** : un commit DLL. **Arrêts** : si le biais de centre ne
  peut pas être reproduit sans toucher au moteur ; si le clamp dérivé de la taille de map contredit
  les constantes de l'original sur la 389.

## 3. Suivi

| Tranche | Statut | Commit |
|---|---|---|
| E5.a suivi scripté de la caméra | ⏳ | |
