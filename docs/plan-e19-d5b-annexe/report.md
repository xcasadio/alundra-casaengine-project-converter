# d5b — E19.d2c2 D5b (F2) : porteurs et passagers, ordre du binaire contre ordre de la DLL

Lecture seule respectée : `git status` identique au départ, rien écrit dans le dépôt. La DLL n'a pas été exécutée ; tout ce qui la concerne est lu dans le code ou transcrit en Python (INFERENCE). Binaire : SHA-1 `a705f8da…58` vérifié. Scratch `$S` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/d5b/`.

## 0. Verdict

- **FACT.** Depuis E20.a, la DLL joue les programmes de toutes les entités avant toute physique, comme le binaire. Il reste quatre écarts, tous dans la passe physique :
  1. Le héros lit la force du tick du porteur avec un tick de retard (`TickForceZ`, `LastTickDeltaX/Y`).
  2. La DLL ne « déplace pas d'abord la cible » quand le héros se pose sur une entité qui n'a pas encore bougé (le binaire le fait).
  3. `IsOnGround` du héros est calculé avant que le porteur bouge.
  4. `RidingEntity` n'est pas remis à 0 par la passe des compteurs.
- **Conception proposée.** C'est un portage d'ordre, pas une tolérance (la tolérance `|LastTickDeltaZ|` / S4b de l'ancien F2 devient inutile). Elle tient en trois mécanismes plus le nettoyage de la passe des compteurs :
  - force du tick du porteur connue avant que le héros bouge ;
  - déplacement préalable de la cible ;
  - passe de fin pour `IsOnGround`.
- **Preuves.**
  - Modèle Python de la passe du binaire contre le code réel : 0 écart sur 2 × 4 000 vecteurs Z et 400 vecteurs XY.
  - Transcription de la DLL contre le binaire sur 600 scénarios : ordre actuel faux sur 595, ordre proposé exact sur 600.
  - Chaque mécanisme est nécessaire (ablation : 505 à 595 scénarios faux).
- **Correction du sujet.** `0x52` et `0x83` ne sont pas des attentes. Ce sont les numéros de carte 52 et 83 (voir §8).

## 1. Méthode et limites

- Binaire lu avec capstone (`$S/lst/*.txt`) et exécuté dans l'interpréteur MIPS, passe réelle `0x80038364` avec copie passager `0x80037364` non stubée (zsim stubait celle-ci). Le héros est en slot 0, donc la branche héros de `0x80036828` est prise.
- Fichiers principaux : `oracle_p.py`, `pmodel.py`, `vec_p.py`, `vec_xy.py`, `dllmodel.py`, `cmp_dll*.py`, `census_z*.py` ; sorties dans `$S/out/`.
- **Deux erreurs de mes outils trouvées et corrigées pendant la validation :**
  - la liste des collidables était périmée à l'initialisation du harnais ;
  - l'impulsion `0x8000` du héros n'est pas un marqueur d'arrêt : `ForceZ = IZF<<8` toujours, le marqueur ne vaut que hors héros.
- **Limites.**
  - Le modèle de la DLL est une transcription lue, pas une exécution.
  - Non couverts : le branchement `×160` (eau) du héros, les mises à l'échelle XY du héros, la branche « attaché » (`+0x28`) de `0x80036828` et de `MoveEntity`.
  - Le XY du modèle est la version terrain ouvert ; le vrai `ComputeXY` tourne pour de vrai, donc toute collision se verrait comme un écart.

## 2. Ordre du binaire (FACT)

`0x8003B388` : si `[0x800DC4B8] & 0x48`, seules les listes tournent (`0x8003B408`). Sinon, dans l'ordre :

| Étape | Adresse | Fonction |
|---|---|---|
| a | `0x8003B3A0` | `0x80038634` (statuts, recyclage) |
| b | `0x8003B3A8` | `0x800386D0` (scripts ; `MovePlayer` en premier, plan §0.2.1) |
| c | `0x8003B3B0` | `0x80038998` (compteurs) : `+0x12C := 0` pour chaque slot (`0x800389DC`, `sw $zero,-0xa4($v1)` avec `$v1 = slot+0x1D0`) |
| d | `0x8003B3D0` | `0x800384F4` (listes) |
| e | `0x8003B3D8` | `0x80038E18` (horloge d'animation) |
| f | `0x8003B3E0` | `0x80038364` (physique) |
| g | `0x8003B3E8` / `0x8003B3F0` | `0x80038E84`, `0x80039300` |

- **Listes (`0x800384F4`).**
  - Actifs : statut 2 ou 3 et `+0x20 == 0`.
  - **Collidables : `flags & 0x80`, `AnimFlags(+0xB4) & 0x80 == 0`, `+0x28 == 0`, aucun test de statut** (`0x80038584-0x800385BC`).
  - Vérifié sur le code réel : une entité de statut 4 (`FlagToDestroy`) collidable est dans la liste. `DestroyEntity 0x8003A59C` n'écrit pas `+0x6C`.
- **Physique `0x80038364`.**
  - Prépasse (`0x80038394-0x800383E0`, entités actives) : `+0x168 := 0` (« déjà bougé »), `+0x140 := 0`, `+0x13C := 0`, Modded := Pos + Mod.
  - Passagers (`jal 0x800364C8`, `0x800383E4`) : parcours de la liste des collidables.
    - Sujet : `(flags & 0x4100) == 0x100` (`0x800364FC`).
    - Candidat : `top + 1 == self.ModdedZ` (`0x80036554-0x8003655C`) et fenêtres X et Y à demi-ouvert (`+1`, `0x80036524`, `0x80036528`, `0x800365B0`, `0x800365C4`).
    - Le **premier** candidat écrit `+0x12C`.
  - Forces de toutes les entités actives (`jal 0x80036828`, `0x800383EC`), dans l'ordre des slots.
    - Décroissance de gravité, avec borne à ±(V<<8).
    - `FinalForceZ(+0xEC) := ForceZ` (`0x80036BB8`) ; `FinalForceX/Y(+0xE4/E8) := AdjustedForceX/Y(+0xDC/E0)` (`0x80036BB0`, `0x80036BB4`).
  - Copie passager (`0x80038400-0x80038440`, **sur la liste collidable**) : `jal 0x80037364` pour tout `+0x12C != 0`.
    - La copie récursive sur le propre porteur du porteur passe d'abord (`0x80037388`), sans mémo.
    - `FinalForceX/Y += Adjusted du porteur` (`0x8003739C-0x800373B0`).
    - Si le passager n'a pas d'impulsion (`+0xF8 == 0`, `0x800373B4`) : `ForceZ(+0xB8) := FinalForceZ(+0xEC) := FinalForceZ du porteur` (`0x800373BC-0x800373C8`).
  - Déplacements (`0x80038450-0x80038494`) : ordre des slots, `+0x168 == 0` puis `MoveEntity 0x80037E34`.
    - `MoveEntity` pose `+0x168 := 1` (`0x80037E58`), puis `ComputeZ 0x800375E0` (appel `0x80037EF8`), puis `ComputeXY 0x80037730` (appel `0x80037F00`).
    - **Récursion** : un atterrissage (`F<=0`) ou un plafond (`F>0`) sur une entité qui n'a pas bougé (`+0x168 == 0`) la déplace d'abord par `MoveEntity` et refait le test (`0x800376B8-0x800376DC` pour l'atterrissage, `0x80037630-0x80037650` pour le plafond). Cette récursion n'est pas dans le plan.
  - Passe de fin (`0x800384A4-0x800384D8`) : `0x80038064` pour chaque actif, après tous les déplacements.
    - `FloorHeight 0x80037F28` (appel `0x800380E8`).
    - `IsOnGround := !(Floor < PosZ)` (`0x800380F8-0x8003810C`).
- **ComputeXY sans force XY** (`0x800377A0-0x800377A8` → `0x80037DC0`) : recalcule seulement Modded et resonde le terrain.

## 3. Ordre de la DLL (FACT, code lu)

- `AlundraWorldProxy.RunEntityBlock` (2871-2954), par tick :
  1. `CollectTickEntities` (2884, 2831-2851) : héros d'abord, puis l'ordre des slots.
  2. `MovePlayerOnTickPad` (2893).
  3. `TickPrograms` de chaque entité (2905-2908), puis le ré-examen des déclencheurs (2922).
  4. `RunCountersPass` (2926, 2966-2989) : **`RidingEntity` non effacé**. Le commentaire 2960-2961 est périmé : le recalcul est déjà par tick.
  5. Listes (2928-2932) → `RefreshUpdateProxiesAndCollidables` (3641-3654) → `EntitySupport.UpdateRidingEntities(_collidables)` (EntitySupport.cs:276-288, `FindRidingEntity` 310-366) : positions d'avant mouvement, chaque tick, tout collidable à gravité, héros compris.
  6. `BuildHitList` (2934) et horloge (2937-2940).
  7. Boucle physique (2943-2946) → `TickPhysics` (AlundraEntityScriptProxy.cs:1589) → `TickMotion` (1601) : héros `AlundraPlayerManager.Tick` (1826) → `TickPlayer` (AlundraScriptedMotion.cs:82) → `RunOneMotionTick` (180-227), puis `RunHeroProbes` (1629) ; autres `TickScriptedNpc` (165) puis `EvaluateEntitySupport` (726-1031).
  8. `RunHitPassHook` (2952).
- **Héros** (`RunOneMotionTick`).
  - Recalcul de `RidingEntity` (189-192), redondant avec les listes.
  - `RunHeroVerticalTick` (245-394) : copie de force `ForceZ = carrier.ForceZ; FinalForceZ = carrier.TickForceZ` (311-315). Le premier est vivant, le second est celui du tick précédent du porteur.
  - Atterrissage par `TryFindSupport` (328-336, 339-353) sur le dessus **d'avant déplacement** du porteur.
  - `IsOnGround` calculé juste après (221-224) via `ComputeFloorHeight` (AlundraEntityScriptProxy.cs:2192).
  - XY : `FinalForceX/Y += carrier.LastTickDeltaX/Y` (AlundraScriptedMotion.cs:670-675), c'est-à-dire le déplacement **réalisé au tick précédent**.
- **Porteur** : `RunOneKinematicTick` pose `FinalForceZ = ForceZ` (668). `EvaluateEntitySupport` applique la loi et écrit `TickForceZ` (781-790), puis le pas Z par `MoveVerticalAndPullPosition` (1005, 2512-2538).
- **Contrôleur.** `Controller.Move` d'un porteur est balayé contre le monde rigide. Le prefab de plateforme porte un `CollisionComponent` `Kinetic` (`Entities/Plateforme flottante (grande)/…entity`). Le support de test dit « invisible to the rigid sweep » (`AlundraContactTestSupport.cs:154`). INFERENCE : le héros ne bloque pas la montée d'un porteur.

**Comparaison**

| Étape | Binaire | DLL | Verdict |
|---|---|---|---|
| Programmes avant physique, héros en slot 0 | oui | oui | égal |
| Passagers sur positions d'avant mouvement | `0x800364C8` | listes, par tick | égal (valeurs) |
| Forces de tous avant tout déplacement | `0x80036828` | chaque entité à son tour | **écart** |
| Copie passager avec force du tick du porteur | `0x80037364` | `TickForceZ` et `LastTickDeltaX/Y` du tick précédent, héros seul | **écart** |
| Déplacer d'abord la cible d'un atterrissage ou d'un plafond | oui | non | **écart** |
| `IsOnGround` après tous les déplacements | `0x80038064` | juste après le pas du héros | **écart** |
| `+0x12C` remis à 0 avant `0x800364C8` | oui | non | **écart** (O-E20-9) |

## 4. Preuves exécutées sur le binaire réel (`$S/out/t*.txt`)

- **Plateforme qui monte** (1 px/tick de t3 à t8, arrêt à t9) : le héros garde `PosZ − porteur − 8 px = 0` à **tous** les ticks, y compris le premier tick de montée et le tick d'arrêt. `RidingEntity` = porteur, `CollidedWithEntityZ` 0, `IsOnGround` 1.
- **Descente** (1 px/tick, 0,5 puis 2 px/tick) : idem. Le héros ne « se pose » pas sur son porteur ; c'est la récursion du §2 qui l'explique.
- **Saut depuis une plateforme qui monte.** Impulsion 1280 au tick 3 : pas de copie Z ce tick, `rel` = +262 144 (4 px) ; ensuite `RidingEntity` est nul.
- **Quai.** Bloc 24×16×16 dans un creux, dessus à fleur d'un sol de 16 px, héros à cheval sur le sol et sur le bloc : le bloc qui monte l'emporte (`rel` 0). À la descente il l'emporte jusqu'au niveau du quai (`CollidedWithEntityZ` 1 à l'arrivée).
- **Téléportation.** Objet abaissé de 32 px sous le héros : atterrissage au tick 11, `PosZ` 2 228 224 en convention DLL, `CollidedWithEntityZ` 1. C'est exactement la valeur écrite dans le texte d'UH-16.
- **Plateforme horizontale.** Le héros bouge avec elle au même tick (pas de phase), et s'arrête au même tick.
- **Pile héros / caisse à gravité / plateforme** (+1 px/tick en Z, +1 px/tick en X) :
  - En Z, tout suit exactement.
  - En XY, la caisse reçoit **deux fois** la force de la plateforme (2 px/tick contre 1), parce que la copie récursive repasse à son tour. Le héros en haut de la pile ne suit pas en XY (il reçoit seulement `Adjusted` de la caisse, qui vaut 0).
  - C'est un défaut du binaire ; aucun cas du corpus n'est recensé (UNKNOWN).

## 5. Modèle Python du binaire contre le code réel (`pmodel.py`, `vec_p.py`, `vec_xy.py`)

- Le modèle couvre : listes, prépasse, passagers, forces, copie récursive, `MoveEntity` avec récursion, plafond, atterrissage, `FloorHeight`, passe de fin.
- **Vecteurs Z** : 2 × 4 000 (graines 7 et 11), 9 947 et 9 987 ticks, héros plus 1 à 3 entités aux drapeaux, boîtes et statuts aléatoires, piles construites exprès, 537 et 566 entités-ticks porteurs, impulsions et écritures de script entre ticks, scènes proches du plafond de 1 920 px. **0 écart.**
- **Vecteurs XY** : 400 vecteurs (graine 3, `vec_xy.py`), forces planifiées XY, piles comprises. **0 écart**, 1 935 entités-ticks porteurs, 1 080 avec XY copié.
- **Couverture de la machine MIPS** (instructions) :
  - 100 % pour `0x80038364`, `0x800364C8`, `0x80037364`, `0x800375E0`, `0x80036BFC`, `0x80036D94`, `0x80037F28` ;
  - 71,5 % pour `0x80036828`, 36 % pour `0x80037E34` (branches non modélisées, voir §1).
- **Mutants** (13) : 12 tués sur les vecteurs Z (`win_no_plus1`, `ride_top_nopl1`, `ride_mask_0x100`, `land_t_ge`, `land_cand_le`, `land_below_le`, `ceil_le`, `no_ridercopy`, `no_move_first`, `moved_ignored`, `copy_ignores_impulse`, `one_sided`). `no_recursion` survit en Z seul et est tué par les vecteurs XY de pile (518 entités-ticks divergents).

## 6. Transcription de la DLL contre le binaire (INFERENCE, `dllmodel.py`, `cmp_dll_rand.py`)

Héros dans l'état tenu par le tick, porteur à 300 px, 600 scénarios scriptés, comparaison à chaque tick de Z relatif, `IsOnGround`, `CollidedWithEntityZ` et `RidingEntity` :

| Ordre | Scénarios avec au moins un écart |
|---|---|
| Actuel | **595 / 600** |
| Proposé | **0 / 600** |
| Proposé sans la force du tick du porteur | 595 |
| Proposé sans le déplacement préalable de la cible | 544 |
| Proposé sans la passe de fin | 505 |

- L'ordre actuel reproduit les symptômes écrits au plan :
  - à la montée, le héros passe au travers (Z relatif −1 px, −1,5 px, …, `RidingEntity` perdu dès t4) ;
  - à la descente, `IsOnGround` alterne et le héros flotte (+1 px, +1,5 px).
- Cas d'UH-18 (porteur à gravité au repos) : seule la première image de calage diffère (contact 0 au lieu de 1), ce qui concorde avec la remarque du plan.

## 7. Conception proposée (une seule)

Principe : porter l'ordre `forces de tous → copie passager → déplacements avec récursion → passe de fin`. Ce que la boucle `TickPhysics` ne sait pas faire seule est exprimé par des appels à la demande.

- **M1 — Force du tick du porteur connue avant que le héros bouge.**
  - Binaire : `0x80036828` pour tous avant `0x80037364` ; copie `0x800373BC-0x800373C8` de `FinalForceZ` dans `ForceZ` et `FinalForceZ`.
  - DLL : `PeekTickForceZ()` pur dans `AlundraEntityScriptProxy`, partageant l'unique site de loi `ApplyVerticalForceLaw` (1040-1065). `RunHeroVerticalTick` (AlundraScriptedMotion.cs:311-315) l'utilise pour les deux champs.
  - Les entrées de la loi (`ForceZ`, `IsZForceApplied`, drapeaux) sont figées avant la physique : programmes et horloge ont tourné (2905-2940).
  - Variante équivalente : une prépasse « forces ». Elle n'existe pas en mode autonome des montages, d'où la préférence pour `Peek`.
- **M2 — Déplacer d'abord la cible.**
  - Binaire : `0x800376B8-0x800376DC`, `0x80037630-0x80037650`, `+0x168` posé en `0x80037E58` et testé en `0x8003846C`.
  - DLL :
    - `TryFindCeiling` rend l'entité trouvée ; `TryFindSupport` la rend déjà.
    - `RunHeroVerticalTick` (328-336, 354) appelle `EnsureMovedThisTick()` sur la cible non encore déplacée puis refait la recherche.
    - `TickPhysics` (1589) est gardée par un estampillage par passe.
    - `PhysicsPassId` est incrémenté par `RunEntityBlock` avant la boucle (2942). Il est ajouté à `IAlundraScriptHost`, `AlundraWorldProxy` et au `ContactHost` de test, qui est en mode autonome.
    - Garde : ne déplacer que les entités participantes (`TookPartInFrame`).
- **M3 — Passe de fin.**
  - Binaire : `0x800384A4-0x800384D8`, `0x800380E8-0x8003810C`.
  - DLL : sortir `IsOnGround` / `FloorHeight` du héros de `RunOneMotionTick` (219-224) et de `RunHeroProbes` pour un `EndPhysicsTick()` appelé par `RunEntityBlock` après la boucle (2946). Rien ne lit ces champs entre-temps.
  - Ne pas ajouter de nom de passe à `TickTraceForTests` : A8 (`AlundraTickStructureTests`) épingle la liste des passes.
- **M4 — Compteurs.**
  - Binaire : `0x800389DC`.
  - DLL : `RunCountersPass` (2966-2989) met `RidingEntity = null` ; les listes (2931) le recalculent juste après. Cela clôt O-E20-9 (la condition « recalcul par tick » est remplie). `UpdateRidingEntityOfHero` (EntitySupport.cs:296) ne reste utile qu'en mode autonome.
- **Option M5 — XY planifié** (tranche séparée) : `AdjustedForceX/Y` du porteur du tick (`0x80037364` ajoute `+0xDC/+0xE0`). On scinde `RunOneKinematicTick` (608-722) en forces puis pas, et on remplace `LastTickDelta` (670-675) par la force planifiée. Le retard de phase de S4 disparaît, ce qui déplace l'épingle d'UH-14.
- **Option M6 — Passagers autres que le héros** (caisses, PNJ sur ascenseur) : le binaire copie pour tout `+0x12C`, la DLL pour le héros seul. Touche les gardes de piles et reproduit le défaut des piles du §4. À ne faire que sur décision.
- **Raffinement possible (règle d'entrée).** INFERENCE : un héros porté mais que le moteur dit « au sol » (le quai du §4, F3 exigeant `IsOnGround == 0`) n'entre pas dans l'état tenu et ne suit pas un bloc qui monte. À traiter par une condition « porté et force du tick du porteur non nulle ».

## 8. Risques pour les épingles

- **UH-10 / UH-11** : porteur immobile, pas de changement attendu. Le tick 25 (sortie du coffre) donne `IsOnGround` 0 aussi avec la passe de fin. INFERENCE.
- **UH-14** : inchangée avec M1 à M4. Avec M5, `hero.PosX − heroStart == −(tick−1)·65536` devient `−tick·65536`, valeur dérivable de l'oracle (§4, plateforme horizontale).
- **UH-15** : fenêtre Y (F1), non touchée.
- **UH-18** : valeurs des ticks 1 à 10 inchangées (`ForceZ` 0, contact 1, `IsOnGround` 1, `PosZ` 1 048 576, `TickForceZ` −32 768). Seule la première image de calage change, et elle n'est pas assertée.
- **Piles h1b1** (`AlundraAbsoluteZWritesTests.cs:74-104`) : intactes tant que M6 n'est pas faite. M4 recalcule les mêmes valeurs. Si M6 déplace ces valeurs, le plan dit que c'est un arrêt pour D5b.
- **Modes** : les montages UH-* sont en mode autonome (`DrivesEntityTicks` faux, `AlundraWorldProxy.cs:793`). L'estampillage doit marcher dans les deux modes, avec une aide de test sur `ContactHost`.
- **Traces** : les quatre traces du héros et les traces d'intro doivent rester à l'octet. Les cartes 389 et 391 portent des « Bloc transparent » qui bougent en Z, et 389 des marins. UNKNOWN tant que la suite n'est pas lancée.
- **Landing non strict des PNJ** (D-E19-40, `<=`) : inchangé, mais il décale d'un tick la fin d'une descente qui touche le terrain. Cela concerne les cartes à `0x21`.

## 9. Recensement (statique, tous les branchements ouverts, borne haute — `census_z.py`, `$S/out/census_z_report3.txt`)

- **Correction.** Dans `docs/plan-e19-opcodes.md:6940`, « `0x21`, 52, 83, 148, … » signifie l'opcode `0x21` suivi des numéros de carte. Dans `EventOpcodeSizeTable.cs`, `0x52` est « Use portal under hero » et `0x83` « If number of item >= value ».
- **Opcodes** (population en sites / cartes ; accessibles) :

| Opcode | Population | Accessibles |
|---|---|---|
| `0x1B` | 944 / 147 | 890 / 143 |
| `0x3E` | 458 / 82 | 450 / 82 |
| `0x21` | 16 / 8 | 16 / 8 |
| `0x22` | 21 / 7 | 16 / 5 |
| `0x23` | 1 / 1 (carte 36) | 1 / 1 |
| `0x20` | 277 / 81 | 266 / 78 |
| `0x64` | 551 / 157 | |
| `0x65` | 253 / 102 | |

  - Les 458 sites de `0x3E` sur 82 cartes retrouvent le chiffre de l'ADR-0023.
  - `0x64` et `0x65` écrivent une position sans force : le binaire ne porte pas non plus le passager.
- **Les 16 sites `0x21`** sont sur exactement les cartes 52, 83, 148, 152, 154, 344, 410, 423 :

| Carte | Entité (enregistrement) | Sites (distance en px) |
|---|---|---|
| 52 | 22 | `[16]` |
| 83 | 22 | `[32]`, `[64]` |
| 148 | 28 | `[32]` |
| 152 | 22, 23 | `[31]`, `[32]` |
| 154 | 22 | `[31]` |
| 344 | 21, 42, 50 | `[1]` ×2 chacun |
| 410 | 22 | `[32]`, `[64]` |
| 423 | 32 | `[64]` |

  - Exemple, carte 83 enregistrement 22 (`C[22]` @1232-1266) : `1B [0,1]` / `0x21 [32,0]` / stop / `1B [0,255]` / `0x21 [64,0]` / stop. C'est une navette de 1 px/tick sur 32 px en montant et 64 px en descendant, avec arrêts nets : le cas qui cassait F2.
  - `0x22` : cartes 12, 36, 89, 115, 127, 178, 363 ; `0x23` : carte 36.
- **Porteurs de Z** (entités collidables à boîte 3D, avec un `0x1B` non nul sur cible statique) : **247 enregistrements sur 88 cartes**.

| Classe | Enregistrements | Cartes | Remarque |
|---|---|---|---|
| Plateformes flottantes (grande, petite, fragile) et `asiba` | 99 | 40 | forces ±0,125 à ±4 px/tick |
| Trappes d'escalier | 44 | 21 | |
| Blocs transparents | 25 | 20 | |
| Interrupteurs à piétiner | 26 | 2 (345, 347) | descendent de 0,5 px/tick |
| Grilles de fer | 20 | 8 | |
| Bois flottant | 16 | 2 (77, 78) | gravité, −2,5 px/tick |
| Portes, statue, bloc à pousser | 7 | 3 (80, 102, 121) | |
| Bulles porteuses | 6 | 2 (159, 160) | +0,5 px/tick |
| Interrupteur à ballon | 4 | 1 (60) | gravité |

  - Les 8 cartes à `0x21` sont toutes parmi ces 88.
  - 44 cartes cumulent un porteur de Z et un `0x3E` ; 38 cartes ont un `0x3E` sans porteur de Z (objets statiques).
- **Chaîne d'histoire (30 cartes)** : aucun `0x3E`. Les cartes 389 et 391 ont des « Bloc transparent » mobiles en Z (389 aussi des marins) ; la carte 178 a un `0x22`.
- **Non recensés (UNKNOWN)** :
  - les porteurs XY (le plan cite 38, 57, 119, 149) ;
  - les piles de porteurs ;
  - les `0x1B` sur le héros (cartes 1, 7, 19, 59, 61-64, 66, 68, 159, 160, 358, 443, 444, 478, 480), qui sont des vols scriptés, pas des plateformes.

## 10. Autres constats

- **Plan F2 / S4.** Les phrases « ordre du binaire » sont confirmées (§2). Le portage de la descente ne passe pas par une règle sur `CollidedWithEntityZ` ni par la tolérance `|LastTickDeltaZ|`, mais par la récursion `MoveEntity` (§2, §4). La règle « `IsOnGround` 1 tant qu'il est porté » est remplacée par la passe de fin.
- **Liste des collidables.** `EntitySupport.cs:52-54` suppose que le binaire exclut `FlagToDestroy`. Le binaire n'a pas de test de statut sur cette liste (§2). Effet limité à un tick avant le recyclage (P4).
- **Défaut du binaire sur les piles XY** (§4) : à trancher selon la règle « un défaut avéré se corrige ».

## 11. Questions ouvertes

1. Périmètre : héros seul (M1-M4, puis M5) ou tous les passagers (M6) ?
2. Accepter que M5 déplace l'épingle d'UH-14 (retard de phase de D-E20-40 supprimé) ?
3. Piles XY : reproduire, corriger ou ignorer ? Il faut d'abord savoir si le corpus en contient.
4. Adopter la règle d'entrée dans l'état tenu pour un héros porté que le moteur dit « au sol » (quais) ?
5. Carte de recette : 83 (navette `0x21`), 22 (ascenseur), 159/160 (bulles), 52/148/152/154/410/423 (trappes) ?
6. Atterrissage non strict des PNJ (D-E19-40) sur les ascenseurs qui touchent le terrain : tranche séparée ?
7. Ajouter `PhysicsPassId` à `IAlundraScriptHost` : acceptable ?
8. Aligner la liste des collidables sur le binaire pour le statut 4 ?

## 12. Liste de tâches proposée

- **T0 (docs).** Annexe versionnée avec `oracle_p.py`, `pmodel.py`, `vec_p.py`, `vec_xy.py`, `dllmodel.py`, `cmp_dll*.py`, `census_z*.py`, sorties et SHA-1 ; valeurs écrites d'avance tirées des traces du §4.
- **T1 (tests rouges d'abord, nouveau fichier).**
  - UH-16 : montée, descente, changements de vitesse, arrêt, saut depuis une plateforme qui monte, téléportation (atterrissage au tick 11), bloc de quai.
  - Montage autonome, plus un arc réel sur la carte 83 enregistrement 22.
- **T2 (code, un commit par mécanisme).**
  - M4, qui clôt O-E20-9, avec les épingles vertes.
  - M1 (`PeekTickForceZ`).
  - M2 (estampillage, `EnsureMovedThisTick`, retour de l'entité par `TryFindCeiling`).
  - M3 (`EndPhysicsTick`).
- **T3 (vérification).** `Alundra.Tests` en Release puis Debug, les six traces à l'octet, mutations jetables (une par mécanisme), vérificateur neuf.
- **T4.** M5 si la question 2 est oui.
- **T5.** M6 si la question 1 est « tous ».
- **T6.** Recette de l'auteur sur la carte choisie.

## Fichiers (scratch `$S`)

- `oracle_p.py` : la passe réelle, héros en slot 0.
- `pmodel.py` : modèle du binaire.
- `vec_p.py`, `vec_xy.py` : vecteurs Z et XY, couverture, mutants.
- `dllmodel.py`, `cmp_dll.py`, `cmp_dll_rand.py` : transcription de la DLL et comparaison.
- `census_z.py`, `census_z_report*.py`, `census_z.json`, `carrier_census.json` : recensement.
- `t1.py` à `t7.py` : traces du code réel.
- `lst/*.txt` : désassemblages.
- `out/*.txt` : toutes les sorties, dont `vec_p_out_4000_7.txt`, `vec_p_out_4000_11.txt` et `cmp_dll_rand_600_ablation.txt`.