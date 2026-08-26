# Tranche — Oracle du héros : trace dorée sur la map 389

> Périmètre NARROWED après trois rondes de relecture sur l'enveloppe M3 : l'utilisateur a décidé le
> 2026-08-26 de livrer **cet oracle seul** et de différer M3.a/M3.b/M3.c. Ce document ne revendique
> donc **rien** au sujet de la bascule d'horloge — voir §1.3, « ce que cet oracle ne prouve PAS ».

## 1. But, et surtout non-but

### 1.1 But

Le héros n'a **aucune couverture automatisée** : il est exclu du harnais d'intro (ni contrôleur ni
entrée) et sa seule vérification est visuelle, au pad. C'est la plus ancienne lacune de couverture du
projet, et elle pèsera sur toute étape future le touchant (combat, objets, warps, M3 si elle revient).

Cette tranche lui donne l'équivalent de ce que la trace d'intro donne aux PNJ : **une trace dorée
déterministe, commitée, régénérée par un test et comparée octet pour octet**, produite en pilotant un
parcours au pad scripté sur la map 389 réelle, avec un vrai `CharacterControllerComponent` et le vrai
champ de collision.

### 1.2 Bénéfice secondaire : mesurer l'effet du pas fixe, sans le déployer

Le mode à pas fixe du moteur (M1) est **activé nulle part dans le dépôt** (vérifié : aucune
assignation de `CharacterMotionSystem.FixedTimeStep` hors des tests moteur). La tranche produit donc
**deux** traces à partir du MÊME parcours — `FixedTimeStep = 0` (production actuelle) et
`FixedTimeStep = 1/50` (la cible qu'une future M3.a poserait) — et assère qu'elles **diffèrent**.
Ce n'est pas une protection de M3.a : c'est une **mesure** de son effet réel sur le héros, qui rendra
la décision différée factuelle au lieu de spéculative. Coût marginal : un paramètre de plus.

### 1.3 Ce que cet oracle ne prouve PAS (à écrire dans l'en-tête des traces)

Le harnais implémente lui-même `IAlundraScriptHost` et **n'instancie pas `AlundraWorldProxy`**.
**Liste exhaustive de ce qui n'est PAS couvert**, à recopier telle quelle dans l'en-tête des deux
traces :

1. `AlundraWorldProxy.LogicTicksThisFrame` (la bascule d'horloge d'une future M3.a) ;
2. le rattrapage caméra E5.c (`UpdateCameraFollow` / `StepCameraScroll`) ;
3. `RunMapEventsPass` ;
4. le rattrapage D3 (`RunPendingEventTriggers`) ;
5. **`AlundraWorldProxy.AdoptPlayerPawn`** : le §2.2 en RECOPIE l'état initial champ par champ, sans
   lien avec la production — si l'adoption réelle dérive, la trace restera verte alors que l'état New
   Game aura changé ;
6. **le chemin d'entrée réel de `AlundraPlayerController.BuildPadState`** (`Input`,
   `InputMappingManager`, `ComputePadState`, mapping « AlundraButtons ») : court-circuité par le
   fournisseur de pad du §2.4, consulté en tête de méthode.

Toute revendication de couverture sur ces six points serait fausse — c'est le défaut qui a fait
échouer trois rondes de relecture sur le plan M3.

## 2. Scope

Fichiers : `Alundra.Tests/HeroTraceHarnessTests.cs` (nouveau), une fixture partagée extraite (§2.1),
`docs/hero-trace-389-freestep.txt` et `docs/hero-trace-389-fixedstep.txt` (nouveaux, commités), et
**un seul ajout inerte en production** (§2.4).

### 2.1 Montage

- Vrai `World`, `PhysicsWorld(false, TopDownElevationSimulationSpacePolicy)`,
  `World.CollisionField = AlundraCellsCollisionField` **réel de la 389** ; pion héros portant un vrai
  `CharacterControllerComponent` dont les `Settings` viennent de l'export réel du convertisseur.
- Patron existant : `AlundraCharacterControllerAdoptionTests.BuildWorld` / `BuildHeroPawn`
  (`:130-208`). **À extraire dans une fixture partagée** plutôt qu'à recopier ; les tests existants
  doivent continuer de passer inchangés.
- Le harnais pose lui-même `world.RuntimeSystems.CharacterMotion.FixedTimeStep` et `MaxStepsPerFrame`
  (paramètres de la campagne, §1.2) et pilote **`world.Update(frameTime)`** par frame — sans quoi
  `CharacterMotionSystem` ne tourne pas et `ExecutedFixedStepCount` reste 0.

### 2.2 État initial du héros — liste nommée et obligatoire

Reproduire le bloc New Game d'`AlundraWorldProxy.AdoptPlayerPawn` (`:1442-1518`), champ par champ :
`PosX`, `PosY`, `PosZ` puis `ClampToGround()`, `TileX`, `TileY`, `TileZ`, `Flags` (header du
`SpriteRecordCatalog` du héros), **`AnimSetsByAnim = header.AnimSets`**, `MapGravityRaw`,
`MapZViscosityRaw`, et les surcharges `Settings.Gravity` / `MaxFallSpeed` / `WalkabilityMask`.

**`AnimSetsByAnim` est vital** : sans lui `RunOneKinematicTick` lit `speed = 0`
(`AlundraScriptedMotion.cs:121-123`) et le héros ne bouge jamais — la trace serait verte et immobile.
Le test doit **échouer avec un message nommant le champ** si le header du héros est introuvable.

### 2.3 État d'animation initial — écart assumé

`AdoptPlayerPawn` pose `TargetAnimationId = 0x36` (LoadingMap), or `MovePlayer` ne réagit qu'à
Idle(0)/Moving(1) et la sortie réelle de LoadingMap passe par le pont `OnAnimationFinished`, qui exige
un asset d'animation absent de ce montage. Le harnais part donc de **`TargetAnimationId = 0` (Idle),
`CurrentAnimationId = ~0`**, écart écrit dans l'en-tête des traces.

### 2.4 Hôte du harnais et unique ajout en production

**Nouvel `IAlundraScriptHost` dédié** — ne pas réutiliser `FakeScriptHost`, dont `PlayerController`
est `null` par construction, ce qui est exactement pourquoi ce montage n'exerce jamais `MovePlayer` :
- `PlayerController` → **instance réelle d'`AlundraPlayerController`** (classe `sealed`, non fakeable),
  ensemencée par le **seul ajout de production de la tranche** : un champ
  `internal Func<AlundraPadState>? PadStateProviderForTests` consulté en tête de `BuildPadState`.
  **Portée instance**, défaut `null` → strictement aucun effet en production. Même idiome que
  `AlundraPlayerManager.SetDebugIgnoreControlLockOverrideForTests`, déjà présent au dépôt.
- `GameState.PlayerControlFlags = 0` — sans quoi la porte `InputBlockedMask` fait sortir `MovePlayer`
  sans rien faire. **Ne PAS** utiliser `ALUNDRA_DEBUG_IGNORE_CONTROL_LOCK`, qui masquerait une
  régression du verrou.
- `Collidables` vide, `Runner` réel, `LogicTicksThisFrame` explicite.

### 2.5 Calendrier de `dt` imposé — DEUX cadences distinctes, à ne pas confondre

Le parcours est exprimé en **frames de `dt` explicite**, non en ticks. Deux compteurs indépendants
coexistent et le plan les nomme séparément (correctif de relecture) :

- **`dllTicks`** — ticks de l'horloge de la DLL, plafonnés par `AlundraScriptedMotion.MaxTicksPerFrame`
  (= 4). Existent dans les DEUX campagnes.
- **`engineStepsDelta`** — pas fixes du moteur, plafonnés par `CharacterMotionSystem.MaxStepsPerFrame`.
  **Identiquement nuls dans la campagne `freestep`** (`FixedTimeStep = 0` → un seul `RunStep`, compteur
  jamais incrémenté) ; n'ont de sens que dans la campagne `fixedstep`.

Le calendrier doit produire, **sur les deux campagnes** : des frames à `dllTicks = 0`, des frames à
`dllTicks ≥ 2`, et au moins une frame à `dllTicks = AlundraScriptedMotion.MaxTicksPerFrame` ; et,
**sur la seule campagne `fixedstep`** : au moins une frame à `engineStepsDelta ≥ 2` et une frame à
`engineStepsDelta = MaxStepsPerFrame`.

### 2.6 Colonnes et format figé — UNE LIGNE PAR FRAME

**Correctif de relecture (structurel)** : les ticks du héros s'exécutent **à l'intérieur** de
`world.Update(frameTime)` (`AlundraEntityScriptProxy.Update:932-939` → `AlundraPlayerManager.Tick`),
et le harnais pilote la frame de l'extérieur — il ne peut donc observer **aucun** état intermédiaire
dès qu'une frame porte ≥ 2 ticks. `IsOnGround` n'est d'ailleurs rafraîchi qu'une fois par frame
(`:828-839`). Obtenir une granularité par tick exigerait soit un second ajout de production (interdit
par §2.4/§2.9), soit que le harnais s'approprie la boucle de ticks — écart de fidélité inacceptable
pour un oracle.

**La trace est donc à granularité FRAME** : une seule ligne par frame, échantillonnée **après** le
retour de `world.Update(frameTime)`. Rien n'est perdu pour l'usage visé : toute variation de
comportement par tick se répercute sur l'état de fin de frame, puisque les positions s'accumulent.

**Aucune valeur flottante** : tous les nombres sont des entiers (16.16 brut pour les positions),
**culture invariante**, séparateur ` | `, fin de ligne **LF**.

Ligne de frame :
`frame | dtMicros | dllTicks | engineStepsDelta | posX | posY | posZ | tileZ | isOnGround |
forceAdjusted | targetAnim | targetDir | cellSlope | cellHeight`

**Source de `cellSlope`/`cellHeight`** : `AlundraCellsCollisionField` n'expose aucun accesseur de
cellule (champs privés ; seule API d'instance publique `TrySampleGround`, dont la normale est la
constante `Vector3.UnitZ` et dont le `surfaceTag` est la propriété de sol, pas la pente). Le harnais
lit donc `AlundraCellsRecords.TryParse` (public, `Slope`/`Height` publics,
`AlundraCellsCollisionField.cs:33-34`) et applique la même formule d'index clampée que
`TrySampleGround` : `(floor(x) / 24, floor(y) / 16)`. **`cellSlope` est publiée MASQUÉE — `Slope & 0x3`
— exactement comme `TrySampleGround` l'utilise** (`AlundraCellsCollisionField.cs:238`) : le champ brut
n'est pas un type de pente (le dépôt documente lui-même une cellule « slope 5 (stairs) », or 5 & 3 = 1).
**Aucun accesseur n'est ajouté à la production.**

### 2.6 bis — DEUX scénarios, imposés par le terrain (amendement du 2026-08-26)

**Fait mesuré sur les données réelles de la 389** (3120 cellules, vérifié en session principale) :
`step_height` du héros = **3 px** (export réel), quantum de hauteur des cellules = **16 px**, et les
**23** cellules à pente (`slope & 3 ≠ 0`) n'ont **aucune** voisine de hauteur 0, même en diagonale —
la hauteur voisine minimale d'une pente est **5** (80 px). Depuis le sol plat où naît le héros,
**aucune pente ni marche n'est atteignable en marchant** : ce n'est pas une question de longueur de
parcours, c'est topologiquement impossible.

La tranche exécute donc **deux scénarios**, chacun avec ses deux campagnes `freestep`/`fixedstep` :

- **Scénario A — « spawn »** : départ à la position New Game réelle. Couvre les prédicats
  **1 (plat)**, **4 (mur)** et **5 (chute)**.
- **Scénario B — « highground »** : départ **délibérément ensemencé** sur une cellule élevée adjacente
  à une cellule à pente, puis descente. Couvre les prédicats **2 (pente)** et **3 (marche)**.
  Ce n'est pas un contournement : c'est exactement le procédé du test existant
  `Stairs_SteppingDownTheSlope` d'`AlundraCharacterControllerAdoptionTests`, et la descente est le
  seul mouvement vertical que le terrain autorise. La position de départ et sa justification sont
  écrites dans l'en-tête de la trace du scénario B, et **ajoutées à la liste du §1.3** (l'état initial
  du scénario B n'est pas un état de jeu atteignable).

Aucun prédicat n'est affaibli : les cinq restent assérés > 0, simplement répartis sur les deux
scénarios. Quatre fichiers dorés au total (`{spawn,highground} × {freestep,fixedstep}`).

**Constat à remonter, hors périmètre de cette tranche** : la conjonction `step_height = 3 px` /
quantum 16 px signifie que le héros ne peut **rien escalader** sur la 389. Cela peut être fidèle
(dans l'intro, rien ne monte : les PNJ sont posés en hauteur et descendent) ou révéler une lacune —
E2 a explicitement laissé non porté le `switch` de pente de `PlayerManager` (cas 4 = eau, 6 = mur
d'escalade), et `Slope_18c` reste 0. À trancher séparément, pas ici.

### 2.7 Cinq prédicats disjoints, tous assérés > 0 (à granularité frame)

1. **plat** : `isOnGround = 1`, `cellSlope = 0`, `posZ` inchangé depuis la frame précédente ;
2. **pente** : `isOnGround = 1` et `cellSlope ≠ 0` (valeur masquée `& 0x3`) ;
3. **marche** : `isOnGround = 1` et `tileZ` change entre deux frames consécutives ;
4. **mur** : `forceAdjusted = 1` ;
5. **chute** : `isOnGround = 0` sur au moins deux frames consécutives.

Sans ces assertions, un parcours dégénéré (héros coincé au départ) produirait une trace verte et vide.

### 2.8 Anti-faux-vert

Le patron réutilisé **s'auto-saute silencieusement quand `alundra-project/` est absent**
(`AlundraCharacterControllerAdoptionTests.cs:264-267`) : le `git status` serait alors trivialement
vide et l'oracle « vert » sans avoir tourné. **Le nouveau test doit ÉCHOUER**, avec un message nommant
l'export manquant — jamais se sauter.

### 2.9 Non-goals

Aucune logique de production modifiée (seul ajout : le champ d'ensemencement du §2.4, inerte par
défaut) ; aucun accesseur ajouté à `AlundraCellsCollisionField` ; PNJ hors périmètre (couverts par
l'intro) ; **caméra, MapEvents et rattrapage D3 hors périmètre** (§1.3) ; aucune bascule de
`FixedTimeStep` en production — le harnais seul le pose, pour mesurer.

## 3. Acceptation

- Le test passe ; les **cinq compteurs du §2.7 sont > 0** sur les deux campagnes ; `MovePlayer` prouvé
  atteint — le premier appui directionnel produit `targetAnim = 1`.
- **Cadence DLL, sur les deux campagnes** : la trace contient au moins une frame `dllTicks = 0`, une
  `dllTicks ≥ 2`, et une `dllTicks = AlundraScriptedMotion.MaxTicksPerFrame`.
- **Cadence moteur, sur la seule campagne `fixedstep`** : au moins une frame `engineStepsDelta ≥ 2` et
  une frame `engineStepsDelta = CharacterMotionSystem.MaxStepsPerFrame` (preuve que le mode à pas fixe
  est réellement exercé) ; et, sur la campagne `freestep`, `engineStepsDelta` est **identiquement 0**
  sur toutes les frames (preuve que le mode est bien désactivé).
- **Deux régénérations consécutives** produisent des fichiers identiques (déterminisme).
- Les deux traces dorées **diffèrent** l'une de l'autre sur au moins une ligne.
- Dans un checkout sans `alundra-project/`, le test **échoue** avec un message explicite.
- `git status --short docs/hero-trace-389-freestep.txt docs/hero-trace-389-fixedstep.txt` **vide**
  après exécution, dans un checkout où `alundra-project/` est présent.
- Suites : build `alundra-casaengine-project-converter.slnx -c Release` 0 erreur ;
  `Alundra.Tests` 487 + nouveaux, 0 échec ; `alundra-casaengine-project-converter.Tests` 138 inchangé ;
  `--filter IntroTrace` vert et traces d'intro inchangées ; tests existants
  d'`AlundraCharacterControllerAdoptionTests` inchangés après extraction de la fixture.
- Moteur `CasaEngine.Tests` **non relancé** : aucun fichier moteur touché.

## 3 bis. Limites connues de l'oracle livré (relevées par le verifier, assumées)

1. **Les traces dorées sont réécrites en silence.** `WriteAndCheck` fait un `File.WriteAllText`
   inconditionnel sans jamais relire la version commitée : supprimer une trace ou en altérer une ligne
   laisse le test vert. C'est l'idiome déjà en place pour `IntroTraceHarnessTests`. **Ce qui rend
   réellement l'oracle discriminant, ce sont les assertions CHIFFRÉES** (frames 98 / 210 / 221 / 232,
   `posX` 36956160, `posZ` 2097152 puis 0), pas la comparaison de fichier — vérifié : une mutation de
   la gravité moteur fait échouer `Assert.Equal(232, landingLine.Frame)` alors que le compteur `fall`
   serait resté > 0 (il passait de 10 à 20). La garde `git status` sur les quatre traces ne vaut qu'une
   fois les fichiers commités.
2. **Angles morts verticaux.** Deux constantes du contrôleur ne sont pas couvertes : la distance
   d'accrochage au sol (`GroundSnapDistance`, 4 px — muter à 2 px ne change aucune trace, parce que le
   mur fait 16 px et la falaise 32 px) et le plafond de vitesse de chute (`MaxFallSpeed` = 800 px/s,
   jamais atteint : la chute culmine à ~250 px/s). Les couvrir demanderait une falaise plus haute.
3. **Ce que l'oracle couvre, et à quel prix** : la gravité moteur est mesurée à **1250 px/s²**, ce qui
   est exactement la formule de production appliquée au `Gravity = 128` réel de la 389
   (`128 × 256 / 65536 × 2500`). L'accélération est uniforme à 0,5 px par frame de 1/50 s.

## 4. Rollback, budget, arrêts

Rollback : revert du commit (aucun pointeur de submodule touché). Budget : un commit DLL + tests +
deux fichiers dorés. Arrêts : si le héros ne peut pas être mis en mouvement de façon déterministe ;
si `BuildPadState` ne peut pas être ensemencée sans désceller `AlundraPlayerController` ; si les cinq
situations ne sont pas atteignables depuis la position New Game sans un parcours déraisonnablement
long — dans ces cas, remonter à l'utilisateur plutôt que contourner.
