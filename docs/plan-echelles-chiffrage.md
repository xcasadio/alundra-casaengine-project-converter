# Échelles — découpage chiffré

Bug rapporté par l'utilisateur le 2026-08-26 : « Alundra ne peut pas monter les échelles ». Document
de **chiffrage**, demandé avant toute décision d'engagement. Rien n'est implémenté à ce stade.

Décisions utilisateur déjà prises (2026-08-26) : on porte **le cas 6 seul** (le cas 4, la nage, est
différé — la 389 n'a aucune cellule d'eau) ; la sortie latérale va vers **`Idle`** et non `Jump`, le
saut n'étant pas porté.

## 1. Le fait qui déclenche tout

La map 389 porte **4 cellules d'échelle** — (18,36), (19,38), (15,55), (21,55) — reconnaissables à
`GroundProperty = 12`, soit `Slope_18c = (GroundProperty >> 1) & 7 = 6`, le « mur d'escalade » du
`switch` de pente de `PlayerManager.MovePlayer`. Ce cas n'a jamais été porté.

**Fait annexe, désormais clos** : l'original **n'a aucun mécanisme de step-up**. Toute élévation se
franchit par pente, escalier ou échelle. Notre `step_height` de 3 px est donc fidèle, et
« Alundra ne peut rien escalader » n'était pas un second bug — c'était l'absence des échelles.

## 2. Volumes côté original (mesurés)

| pièce | emplacement | lignes | dépendances |
|---|---|---|---|
| `bestFlagMask` + `Slope_18c` | `PhysicsEngine.cs:1706-1826` | 120 | `MapTiles[0..3]`, `MapHeights[0..3]`, `ModdedPosZ`, `Flags.Gravity` |
| ↳ remplissage de l'empreinte | `ComputeEntityGroundHeight:956-1073` | 118 | — |
| `FloorHeight` | `:1703` via `GetCollisionOnZ:1602-1675` | 74 | `TerrainHeight` |
| `GetTileHeightAtOffset` | `EntityGameplayManager.cs:277-345` | 69 | échantillonne 4 coins |
| cas 6 + états Climbing/ClimbStill | `PlayerManager.cs:341-350`, `675-731` | ~60 | les précédentes |

Total brut : **~440 lignes** de logique décompilée. **Mais** une grande partie est déjà couverte —
voir §3. Le chiffre brut n'est pas le coût.

**Fait qui simplifie la conception** : `Slope_18c` est calculé **en fin de passe physique**
(`UpdateTileAttributes`), alors que `MovePlayer` s'exécute **avant** dans la même frame. Le
`MovePlayer` de l'original lit donc la valeur de la frame **précédente**. Cette latence d'une frame
est native : notre portage n'a rien à calculer en milieu de frame.

## 3. Ce qui existe déjà (mesuré)

| brique | état | où |
|---|---|---|
| Échantillonnage terrain **sur 4 coins** | **existe** | `AlundraEntityScriptProxy.ComputeTerrainHeight:1179-1197` (19 l.) + `SampleTerrainHeightCorner:1199-1209` |
| Hauteur de sol par cellule, pente comprise | **existe** | `AlundraCellsCollisionField.TrySampleGround` / `ComputeGroundHeight` |
| Propriété de sol par cellule | **calculée, non exposée** | `TrySampleGround` la lit (`_groundProperty[cellIndex]`) mais ne la rend que sous forme de **chaîne** (`surfaceTag`) |
| Support entité-vs-entité | **existe** | `EntitySupport.cs` (262 l.), `TryFindSupport` |
| Décroissance `ForceZ` par tick, atterrissage terrain | **existe, PNJ seulement** | `EvaluateEntitySupport` (branche `!IsPlayer`) |
| Déplacement horizontal du joueur | **existe** | `AlundraScriptedMotion.RunOneKinematicTick:118-179` |
| Verticale du joueur | **moteur, continue** | `AdoptPlayerPawn:1518-1524` pose `Settings.Gravity`/`MaxFallSpeed` — le héros tombe bien, mesuré à 1250 px/s² par l'oracle |
| `MapTiles[4]` | **n'existe pas** | commenté, `AlundraEntityScriptProxy:187` |
| `MapHeights[4]` | **déclaré, jamais écrit** | `:188` |
| `FloorHeight` | **déclaré, jamais écrit** | `:142` |
| `GetTileHeightAtOffset` | **n'existe pas** | — |
| Tests joueur | 18 | `AlundraPlayerManagerTests.cs` — **aucun** ne touche la verticale |
| Tests PNJ terrain/gravité/support | 34 | `AlundraNpcCharacterControllerMoverTests.cs` — patron réutilisable |

**Conséquence majeure du tableau** : les 118 lignes de `ComputeEntityGroundHeight` sont **déjà
couvertes pour l'essentiel** par `ComputeTerrainHeight` + `TrySampleGround`. Il ne manque qu'un
**accesseur numérique** à la propriété de sol (quelques lignes, tout est déjà calculé en interne) et
la règle du **minimum sur les coins qualifiés** (~25 lignes). `MapTiles[4]` n'a pas à être ressuscité.

## 4. Découpage

### É1 — Sonde de terrain du joueur et `Slope_18c` (petite)

- **Construit** : accesseur numérique de propriété de sol sur `AlundraCellsCollisionField` (additif) ;
  port de la règle des quatre coins — minimum de `GroundProperty` sur les coins vérifiant
  `hauteur == ModdedPosZ`, remise à 0 dès qu'un coin échoue, et 0 forcé quand le bit `Gravity`
  est absent ; appel de `ComputeTerrainHeight` (existant) dans la branche joueur.
  **Décision de conception (2026-08-26, post-revue adversariale) : pas de `+1`.** L'original compare
  `hauteur + 1 == ModdedPosZ` parce que SON invariant au repos est `ModdedPosZ == TerrainHeight + 1`
  (`PhysicsEngine.cs:186`/`:128`). Ce portage n'a jamais adopté ce `+1` — son propre invariant au repos
  est `ModdedPosZ == TerrainHeight` (`AlundraEntityScriptProxy.cs`, atterrissage et `ClampToGround` :
  aucun des deux n'ajoute `+1`), confirmé sur les 4 traces dorées du héros (`posZ == cellHeight × 16 <<
  16` exactement, jamais `+1`). Porter le `+1` littéralement aurait rendu la condition en permanence
  insatisfiable dans ce moteur (verdict de la revue adversariale, F1) : on porte le SENS de la règle
  (« le héros est posé sur ce coin »), pas sa lettre.
- **Coût** : ~25 lignes de règle + ~10 d'accesseur + le câblage joueur. Réemploie les 30 lignes
  d'échantillonnage 4 coins existantes.
- **Acceptation** : sur le champ réel de la 389, `Slope_18c == 6` sur les 4 cellules d'échelle et 0
  ailleurs — vérifié à la fois en appelant `UpdateGroundSlope()` directement ET via le site d'appel de
  PRODUCTION (`Update`'s own `IsPlayer` branch, `HeroWorldFixture`-monté, un vrai `World`/`PhysicsWorld`/
  champ de collision réel) ; **et** un cas qui tue la mutation « échantillon au centre » (mais voir la
  note ci-dessous sur ce qu'il prouve réellement).
- **Débloque en propre** : rien de visible. C'est une brique.

### É2 — `FloorHeight` du joueur (petite)

- **Construit** : `FloorHeight` composé des briques existantes — `ComputeTerrainHeight` pour le
  terrain, `EntitySupport.TryFindSupport` pour les plateformes — au lieu de porter les 74 lignes de
  `GetCollisionOnZ`.
- **Coût** : ~20 lignes. **Réemploi quasi total.**
- **Acceptation** : sur le sol plat de la 389, `FloorHeight` vaut la hauteur de terrain ; posé sur une
  vraie plateforme entité (record 1, sommet ≈ 496 px) à une pose où le terrain seul rendrait une
  valeur différente (176 px), il vaut la hauteur de la plateforme + 1 (convention `+1` de ce portage
  pour le repos sur entité, `EntitySupport.cs:173`) — avec un contrôle obligatoire prouvant que
  `Collidables` vide fait échouer cette assertion.
- **Débloque en propre** : rien d'observable pour l'instant — `grep` sur `Alundra/` ne trouve aucun
  consommateur de `FloorHeight` (déclaration, clone, et mentions en commentaire seulement) ; É2 est
  une brique pure, sans risque de régression, dont les consommateurs viendront en É4 (la condition de
  descente `FloorHeight + 1 < PosZ`, `PlayerManager.cs:701`). À la pose New Game réelle, `FloorHeight`
  vaut déjà 0 — correct dans la convention de ce portage (l'original rendrait 1), donc ce bénéfice
  n'est de toute façon pas observable à la pose où la partie démarre.

### É3 — `GetTileHeightAtOffset` (moyenne)

- **Construit** : port des 69 lignes, échantillonnage 4 coins à un décalage donné. Nécessaire au
  **seul** garde de montée (« continuer à monter tant que `PosZ <= hauteur de la tuile devant »).
- **Coût** : ~50 lignes en réemployant `SampleTerrainHeightCorner`.
- **Acceptation** : valeurs chiffrées sur les 4 cellules d'échelle et leurs voisines.
- **Débloque en propre** : rien. Brique de É4.

### É4 — Cas 6 et états d'escalade (moyenne)

- **Construit** : les 5 conditions d'entrée ; les états `Climbing` (anim 14) et `ClimbStill` (anim 53) ;
  `ForceZ = ±0x10000` soit ±1 px par tick ; **suspension de la gravité moteur pendant l'escalade et
  restauration à la sortie**, avec mémorisation de la valeur d'adoption (aujourd'hui inexistante) ;
  sortie latérale vers `Idle` (décision utilisateur).
- **Coût** : ~80 lignes + la prise de contrôle verticale par tick **limitée à l'état d'escalade**.
- **Point de conception retenu** : on ne rend PAS toute la verticale du joueur à la DLL. Le héros
  continue de tomber par la gravité moteur ; seule l'escalade prend le contrôle, par tick, puis rend
  la main. C'est ce qui garde les **quatre traces dorées du héros inchangées** — les rendre
  DLL-continues les ferait toutes bouger.
- **Acceptation** : un cas par condition d'entrée retirée ; +1 px exactement par tick en montée, −1 en
  descente ; pad relâché → anim 53, position figée ; sortie latérale → anim 0 ; gravité restaurée à sa
  valeur d'adoption, assertion chiffrée avant/pendant/après.
- **Débloque** : le bug rapporté.

## 5. Deux chemins possibles

| | chemin **minimal** (É1→É4) | chemin **complet** |
|---|---|---|
| périmètre | l'escalade seule prend la verticale, par tick | le joueur reçoit toute la verticale par tick, comme les PNJ |
| lignes neuves | **~185** | ~185 + le portage complet d'`EvaluateEntitySupport` côté joueur |
| traces dorées du héros | **inchangées** | **les quatre bougent** — la chute passerait de moteur-continue à DLL-par-tick |
| tests | 4 tranches, ~20 nouveaux cas | idem + réécriture des oracles du héros |
| risque | contenu | élevé : touche la chute, le support, l'atterrissage du héros |

**Recommandation : le chemin minimal.** Il livre le bug rapporté sans toucher à ce qui marche
aujourd'hui, et il laisse le chemin complet ouvert si un besoin réel apparaît (plateformes mobiles,
support entité-vs-entité pour le joueur).

## 6. Ce que ça n'inclut pas

Le cas 4 (nage) — différé, aucune cellule d'eau sur la 389. Le saut — non porté, d'où la sortie
latérale vers `Idle`. Le portage complet de `ComputeEntityGroundHeight` — rendu inutile par le
réemploi de `ComputeTerrainHeight`. `MapTiles[4]` — n'a pas à être ressuscité.

## 7. Risques identifiés

1. **La divergence centre/coins, mesurée sur la 389, ne se matérialise PAS à la pose « collé au mur »**
   (correctif du risque ci-dessous, revue adversariale F3 : force brute sur toutes les positions
   entières de la carte 52×60, empreinte de production). La règle des quatre coins ne rend 6 que pour
   12 positions sur toute la carte (3 par cellule d'échelle), et pour chacune d'elles — y compris la
   pose collée contre le mur (18,37) — le centre géométrique et les quatre coins **s'accordent** (les
   deux lisent 6). La divergence centre/coins n'est démontrable que sur une pose 2px plus profonde,
   inatteignable par la marche (pas de step-up, voir §1). Le test É1 qui l'exerce est donc conservé
   comme tueur de mutation « échantillon central » uniquement, documenté comme tel — ce n'est PAS une
   démonstration que le risque ci-dessous se réalise en jeu réel.

2. **Écarts connus, documentés mais non corrigés dans É1** (identifiés par la revue adversariale,
   disposition : documenter seulement) :
   - `Slope_190` n'est jamais mis à jour par ce portage alors que l'original l'écrit à chaque appel
     d'`UpdateTileAttributes` (`PhysicsEngine.cs:1819`) ; le champ existe et est cloné, et l'original le
     consomme (détection de front eau, `FunctionTypeC`/`FunctionTypeE`) mais rien ne l'alimente ici.
   - `CombinedVramFlagsOR`/`CombinedVramFlagsAND` ne sont jamais écrits par ce portage (déviation
     préexistante à É1, non introduite par elle), alors qu'un consommateur les lit
     (`DestroyOnVramFlags`).
   - `TrySampleGround`/`ProbeSlopeCorner` n'appliquent jamais le bump `slopesHit` de +16 que l'original
     applique aux 2e/3e/4e coins d'une empreinte à cheval sur plusieurs cellules en pente
     (`PhysicsEngine.cs:1021-1024`/`:1037-1040`/`:1053-1056`). Sans effet mesurable sur la 389 (toutes
     ses cellules en pente ont `GroundProperty = 0`), mais non porté.
   - La position réelle de `Flags |= EntityFlags.Gravity` dans `MovePlayer` est APRÈS deux `return`
     anticipés (`BlockedByEntity != null`, `InputBlockedMask`) — sans effet observable puisque le bit
     est rémanent, mais le rapport initial de la tranche affirmait à tort qu'il était posé
     inconditionnellement « chaque frame ».

3. **Trous de couverture acceptés par construction (F8)** : le minimum entre deux masques
   `(GroundProperty & 0x0e) << 8` non nuls et distincts n'est exercé par aucun test — la 389 ne porte
   que trois valeurs de `GroundProperty` (`{0, 12, 128}`), et `(128 << 8) & 0xe00 == 0`, donc ce chemin
   est inatteignable avec les données réelles de cette carte. Une inversion min/max serait quand même
   attrapée par les 4 cas `[Theory]` existants (la sentinelle `0xe00` est le maximum, une inversion
   rendrait 7 au lieu de 6). Le clamp hors-carte, lui, EST testé (voir
   `AlundraGroundSlopeTests.OutOfMapFootprint_ClampsToNearestCell_*`).
2. **`MovePlayer` tourne par image rendue**, pas par tick comme dans l'original. L'escalade lit le pad
   (par image) et décide d'un déplacement (par tick). La frontière doit être écrite explicitement dans
   É4, sinon on réintroduit la classe de bug corrigée en E4 et en E5.c.
3. **La restauration de gravité n'a aujourd'hui aucune source** : `AdoptPlayerPawn` écrit
   `Settings.Gravity` sans mémoriser la valeur. É4 doit créer cette réserve, sans quoi « restaurer »
   restaurerait zéro.
