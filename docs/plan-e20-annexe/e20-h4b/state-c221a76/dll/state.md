# E20.h4b : où atterrissent les briques communes dans la DLL (état au HEAD, lecture seule)

Ce rapport est aussi écrit dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/dll/state.md`.

## 0. Base, méthode, légende

- FAIT : dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`, HEAD `eb285bf7`.
  - `git diff --stat 66d86ebc HEAD -- Alundra Alundra.Tests` est vide. Le code et les tests sont donc ceux que l'annexe `docs/plan-e20-annexe/e20-h3/state-f81304b/` a déjà traduits (R19, R26-R30, C1-C4 ; `check/check.md` fait foi).
  - Tous les numéros de ligne ci-dessous ont été relus dans les fichiers, aucun n'est recopié. `tools/verify_cites.py` contrôle 270 citations (fichier, ligne, expression attendue) et rend 0 écart.
- Rien n'a été écrit hors de ce dossier : ni édition, ni `git` en écriture, ni build, ni test, ni export. `data-extracted/` et `alundra-project/` ne sont que lus.
- Binaire `ALUN_CD.EXE` (France) : relu avec des COPIES de `exelib.py`, `lin.py`, `scan_refs.py`, `scan_jal.py` placées dans `tools/`, lancées `python -B` depuis ce dossier. Trois scripts sont à moi : `tables256.py`, `scan_push.py`, `scan_e3.py`. La décompilation n'est que lue.
- Légende : **FAIT** (vu dans le code, les tests, les données ou le binaire, avec fichier:ligne ou adresse), **INFÉRENCE** (déduit, avec de quoi), **INCONNU** (non établi).
- Noms : les types ont perdu le préfixe `Alundra` (restructuration) ; les fichiers de test gardent `Alundra*`. Exemples : `Alundra/Physics/ScriptedMotion.cs`, `Alundra/Entities/EntityScriptProxy.cs` (+ `.HitPass.cs`), `Alundra/Maps/WorldProxy.cs`.

## 1. Verdict court

1. **Aucune des sept briques ne demande de changer une représentation existante.** Chacune a un point d'atterrissage naturel dans un fichier qui existe déjà (§2 à §8). Les ajouts sont des accesseurs, un test de remise à zéro, au plus deux membres d'interface par défaut et un fichier nouveau pour la requête.
2. **Aucun test existant ne bouge** si les briques sont ajoutées sans écrivain.
   - La poussée, le coup de caméra et le tampon restent à zéro tant qu'aucun gestionnaire ne les écrit. La requête et les tables ne sont lues par personne.
   - Seules les deux épingles du nombre de clés du registre bougent, et seulement quand une clé de production est enregistrée (§8, §9). Si l'inscription commune d'E3 reste hors production jusqu'à h4c1, aucun test ne bouge dans h4b.
3. **Trois corrections aux rapports lus.**
   - (a) Le binaire ajoute la poussée dans la même passe pour le héros. Il saute entièrement cette passe pour une entité dont `+0x28` (porteur) n'est pas nul (§2.3).
   - (b) La requête lit `+0xB4`, que la DLL tient dans `HitAnimByte` et non dans `AnimFlags` (§4.3).
   - (c) L'indice du plus haut emplacement se lit sans recalcul ni relèvement. Il vaut le plus haut emplacement tenu de `EntitySlotTable`, car la table les tient et les libère exactement aux deux instants du binaire (§3.3).
4. **E3 est partagé par 6 types de la page globale (16, 23, 150, 151, 152, 153) et par 15 indices distincts de la page de carte (types `0x100 + indice`).** Les « 98 » du rapport sont des couples (carte, indice), pas des types (§8.2).
5. **Pièges à éviter, tous lus.**
   - Les assistants `GetInt32`/`Set` de la décompilation sur `short[]` sont faux (signe, troncature à l'octet).
   - Le coup de caméra n'a pas de partenaire X écrit (§7.2).
   - La DLL ne lit pas l'octet `SlideEffectId` de l'en-tête de carte, que le binaire utilise comme décalage dans la passe des forces. Deux cartes, 159 et 160, l'ont à 1 (§2.3).

## 2. Brique 1 : la poussée `+0xCC/+0xD0`

### 2.1 Ce que la DLL a

- FAIT : deux champs publics `PreviousAdjustedForceX/Y`, `Entities/EntityScriptProxy.cs:341-342` (commentaires `//?cc`, `//?d0`). Copiés par `Clone` (`:2638-2639`). Aucun consommateur ni écrivain dans `Alundra/`.
  - `rg` ne trouve que ces trois lignes et le commentaire `Physics/ScriptedMotion.cs:609` : « no riding-platform force feed (PreviousAdjustedForceX/Y stay 0) ».
  - Ce commentaire est approximatif : la poussée n'est pas un flux de plateforme, celui de la DLL passe par `LastTickDeltaX/Y` (`:677-681`).
- FAIT : la passe de forces d'une entité est `ScriptedMotion.RunOneKinematicTick`, `Physics/ScriptedMotion.cs:614`, dans l'ordre :
  - mise à jour de la vitesse cible `:625-639` ;
  - copies locales glace/eau du héros seul `:641-662` ;
  - `ForceX/Y = IncrementForce(...)` `:665-666` ;
  - `AdjustedForceX/Y = ForceX/Y` `:670-671` ;
  - `FinalForceX/Y = Adjusted` `:672-673` ; `FinalForceZ = ForceZ` `:674` ;
  - flux du porteur, héros seul `:677-681` ;
  - borne de carte, `HasLogicalZ` seul `:683-691` ;
  - déplacement `:700-709` (contrôleur : `MoveControllerAndPullPosition` puis `SlideAlongWall`, sinon `PosX/Y += FinalForceX/Y`) ;
  - drapeau de borne levé après la glissade `:712-715` ;
  - `LastTickDeltaX/Y` `:717-718`.
- FAIT : la doc `:603-613` dit « flat-ground-only ». Le terme de table `(ForceX + table[+0x188 & 0xF]) >> shift` du binaire n'est pas porté (`Adjusted = Force`).
- FAIT : `ForceAdjusted` (`EntityScriptProxy.cs:397`, `//0x13c`) :
  - remis à 0 en tête de tick (`ScriptedMotion.cs:188`) ;
  - posé par `MoveControllerAndPullPosition` (`EntityScriptProxy.cs:2435`) ;
  - réécrit par `SlideAlongWall` (`ScriptedMotion.cs:825-891`) ;
  - levé par la borne de carte après la glissade (`:712-715`) ;
  - lu par le choix du programme E (`EntityScriptProxy.cs:1718`) et par les opcodes de marche (doc `ScriptedMotion.cs:23-31`) ;
  - copié par `Clone` (`:2666`).

### 2.2 Quelles entités exécutent la passe (FAIT, code)

- Une seule fonction sert tout le monde. `RunOneKinematicTick` est atteinte :
  - par `TickPlayer` (héros, `ScriptedMotion.cs:88`), via `PlayerManager.Tick` (`Hero/PlayerManager.cs:1839-1840`), appelé par `EntityScriptProxy.TickMotion` (`:1616-1622`) quand `ScriptHost.PlayerController != null` ;
  - par `TickScriptedNpc` (`ScriptedMotion.cs:171-176`), appelé par `TickMotion` (`EntityScriptProxy.cs:1629-1632`) pour tout non-héros dont `Status.IsActive()`, c'est-à-dire Normal ou Deactivated (`Entities/EntityStatusExtensions.cs:11-12`).
- FAIT : `TickMotion` est appelé par `TickPhysics` (`EntityScriptProxy.cs:1602-1611`). `TickPhysics` est lui-même appelé par `WorldProxy.RunEntityBlock` pour chaque entité de `_tickEntities` (`Maps/WorldProxy.cs:2976-2980` ; en production `DrivesEntityTicks` est vrai quand `_world.Game != null`, `:821`). Hors monde piloté, c'est la boucle autonome (`EntityScriptProxy.cs:1527`, `:1546`).
- FAIT : `_tickEntities` contient le héros s'il a participé à la frame, puis les entités de `_spawnedEntities` dans l'ordre des emplacements dont `TookPartInFrame` est vrai. S'y ajoutent celles nées par l'interface ou un opcode avant l'intégration (`JoinsBirthTick && entity.World == null`) (`WorldProxy.cs:2865-2885`). Porte fermée (`GameplayBlockedMask`) : seules les listes tournent (`:2907-2916`), donc aucune passe de forces.
- FAIT : trois familles d'entités passent par cette même passe, avec trois différences seulement.
  - Entités de carte (enregistrements du chargement) : `JoinsBirthTick` et `HasLogicalZ` faux ; Z tenu par le moteur ; pas de borne de carte.
  - Entités nées d'un enregistrement par les opcodes `0x2D`, `0x8A`, `0x8B` (E20.e0a4) : `JoinsBirthTick` vrai (`EntityScriptProxy.cs:1375`), `HasLogicalZ` faux (posé « par l'interface seule », doc `:1377-1383`).
  - Entités nées par `SpawnEntityBySpriteIndex` : `JoinsBirthTick` et `HasLogicalZ` vrais (`WorldProxy.cs:3572-3573`). Elles ont le Z logique avant/après le pas XY (`ScriptedMotion.cs:211-217`, `RunLogicalZPreXyStep` `:465`, `RunLogicalZPostXyStep` `:540`) et la borne de carte `:685-691`.
- FAIT : les préfabs des sorts ont un `CharacterControllerComponent`. Vérifié par `grep` sur le nom du composant dans `alundra-project/Entities/` : Magie de feu Niv.1 et Niv.2, Magie de terre Niv.1 et Niv.2, Magie de vent Niv.1 et Niv.2 (bas), Magie d’eau Niv.2, Projectile de magie de feu Niv.1.
  - Une poussée passe donc par `MoveControllerAndPullPosition` (`ScriptedMotion.cs:702`) et heurte les murs.
  - Seul un préfab sans contrôleur intègre `PosX/Y` directement (`:707-708`).

### 2.3 Ce que fait le binaire (FAIT, relu avec `lin.py` ; il fait foi)

- La fonction `0x800366FC` a un seul appelant, `0x80036B9C`, dans la passe `0x80036828` (`scan_jal.py`).
  - Le héros y arrive aussi (saut `0x80036A7C` vers `0x80036B9C`).
  - Les autres entités y arrivent après `IncrementForce` (`0x80036B78-0x80036B98`).
  - La poussée concerne donc le héros comme les autres, une fois par tick, après l'incrément de force.
- Formule (`0x80036710-0x80036764`) : `Adjusted = ((Force + table[+0x188 & 0xF]) >> shift) + poussée`, X avec `+0xCC`, Y avec `+0xD0`.
  - `shift` est l'octet `+8` de l'en-tête de carte pointé par `[0x800E4334]`.
  - Les deux mots de poussée sont remis à zéro en `0x80036750` et `0x80036754`, avant les bornes. La poussée n'est pas décalée.
- Borne de carte (`0x80036768-0x800367D4`) : appliquée à tout le monde au binaire, avec levée de `+0x13C`. La DLL ne la porte que pour `HasLogicalZ` (ADR E20.e0k3, `ScriptedMotion.cs:609-612`, `:683-691`).
- Branche « porteur » (`0x80036A84-0x80036AB4`) : pour une entité dont `+0x28` (`PlatformEntity`, `EntityScriptProxy.cs:101`) n'est pas nul, les forces `+0xB8`, `+0xC4`, `+0xC8`, `+0xDC/+0xE0`, `+0xE4/+0xE8/+0xEC` sont mises à zéro et la passe saute à `0x80036BBC`. La poussée n'est alors ni appliquée ni remise à zéro. La DLL n'a pas cette branche (`RunOneKinematicTick` ne lit pas `PlatformEntity`).
- FAIT (données exportées) : le champ `SlideEffectId` de l'export (`data-extracted/data/map_N.json`, `Info`, après `MapId`, `Gravity`, `ZViscosity`) vaut 0 sur 481 cartes et 1 sur deux (159 et 160).
  - INFÉRENCE : c'est l'octet `+8` que le binaire lit (`lb 8(...)`, `0x80036744`) comme décalage. Ses lectures voisines `lhu 4` et `lhu 6` (`0x80036B24`, `0x80036B34`) sont la gravité et la viscosité de la même structure.
  - La DLL ne le lit nulle part (`rg SlideEffectId Alundra Alundra.Tests` : rien). Sur ces deux cartes le binaire décale la force avant d'ajouter la poussée.
  - Hors périmètre de h4b, mais la formule `Adjusted = Force + poussée` n'est exacte que pour `shift = 0` et table nulle.
- Autres écrivains (cadrage de Q-H4-3) :
  - au binaire, 14 `sw` non nuls à `+0xCC` et 13 à `+0xD0` dans des fonctions distinctes (`tools/scan_push.py` ; balayage par déplacement, coïncidences possibles). Dans le lot figurent les sites C65 `0x8007B004/0x8007B018` et C75 `0x8007BEB8/0x8007BED4`, `0x8007C000/0x8007C004`. Il n'y a que deux remises à zéro (`0x80036750/54` ; `0x8007EBBC/C4`) ;
  - la décompilation écrit `PreviousAdjustedForce[XY]` en 27 lignes de `FunctionTypeC.cs` et les consomme en `PhysicsEngine.cs:1518-1522`.
  - INFÉRENCE : le canal est général, pas propre aux sorts (la décompilation n'est pas vérifiée au binaire ligne à ligne).

### 2.4 Où elle s'ajoute et se remet à zéro (INFÉRENCE)

- Point d'insertion : dans `RunOneKinematicTick`, entre `ScriptedMotion.cs:671` et `:672`, après `IncrementForce` (`:665-666`) comme le binaire. On écrit `Adjusted = Force + Previous...`, puis on met les deux champs de poussée à 0. Tout ce qui suit (porteur, borne, déplacement, `SlideAlongWall`) lit déjà `FinalForceX/Y`.
- Exactitude : égale au binaire tant que la table et le décalage sont nuls, ce que `Adjusted = Force` suppose déjà. C'est vrai sur 481 cartes sur 483 (FAIT, données).
- Même remise à zéro pour le héros (même fonction).
  - Non consommée si le statut n'est pas Normal/Deactivated. La doc de la DLL (`EntityScriptProxy.cs:1599`) dit que la liste de physique du binaire prend les statuts 2 et 3 ; garde `TickMotion` `:1629`.
  - Non consommée non plus quand la porte est fermée (`WorldProxy.cs:2907`).
- Conséquences à prévoir, lues dans le code :
  - Borne de carte : une entité de carte ou le héros poussé n'est pas borné (le binaire le bornerait). C'est l'écart déjà documenté O-E19-17/D-E19-25, épinglé par `AlundraMapEdgeWorldTests.B3` (`:213`) et `B4` (`:275`). Les orbes et les boules (nés par l'interface) sont bornés.
  - `SlideAlongWall` (`ScriptedMotion.cs:781-892`) décide `agrees` d'après `TargetDirection` et le signe de `FinalForceX/Y` (`:786-813`). Avec une poussée dans `FinalForce`, une entité dont la direction ne colle pas à la poussée sort tôt (`:810`). `ForceAdjusted` reste alors celui que `MoveControllerAndPullPosition` a posé (`EntityScriptProxy.cs:2435`). INFÉRENCE : à mesurer pour l'orbe C75 en vol.
  - `DeactivateOnImpact` (drapeau `0x10` posé par C75) lit `ForceAdjusted`/`IsOnGround` (`EntityScriptProxy.cs:1718`). `NoObstacleSlide` (`0x2000`) est lu par `SlideAlongWall` (`ScriptedMotion.cs:819`).
- Branche porteur : non portée ; sans effet tant qu'aucune entité poussée n'a `PlatformEntity` (INFÉRENCE).

### 2.5 Tests

- FAIT (aucun ne bouge si l'insertion est inerte, poussée à zéro) :
  - `AlundraBeannoideOracleTests.cs:93-94` et `AlundraMurutaOracleTests.cs:92-93` : `prevadjx_CC` et `prevadjy_D0` dans `UntouchedWords` (`:90-95`, boucles `:178`, `:230` et `:210`, `:261`). Ils sont posés depuis les vecteurs et relus inchangés, sans passe de mouvement. Ils ne bougent que si le champ est renommé ou retypé (4 lignes + `EntityScriptProxy.cs:341-342`, `:2638-2639`).
  - Passes de mouvement réelles à poussée nulle : `AlundraLogicalZWorldTests.cs` (Z1 `:133` à Z11 `:545`), `AlundraMapEdgeWorldTests.cs` (B1 `:120`, B2 `:183` et `:196`, B3 `:213`, B4 `:275`), `AlundraBirthTickTests.cs:458` et `AlundraBirthTickProbePaTests.cs:202` (appels directs de `TickScriptedNpc`), `AlundraRecordBirthTickTests.cs:396`, `AlundraNpcCharacterControllerMoverTests.cs`, `AlundraGlobalFreezeEntityUpdateTests.cs` (branche sans contrôleur).
  - `AlundraLogicalZStepTests.cs` (`:306-307`, `:335`, `:355`) pose `FinalForceX/Y` à la main et appelle les moitiés Z. Elle ne passe pas par `RunOneKinematicTick` (privée) ; neutre.
  - `AlundraMapEdgeClampTests.cs` (`:46`, `:62`, `:99`) : fonction pure `ClampStepToMapEdge` ; neutre.
- Manque (FAIT, `rg`) : aucun test ne pose la poussée avant un tick réel. À écrire :
  - ajout une seule fois puis remise à zéro ;
  - ajout après `IncrementForce` ;
  - avec/sans `HasLogicalZ` (borne) ;
  - héros ;
  - statut non actif ;
  - porte fermée.

## 3. Brique 2 : l'indice du plus haut emplacement `[0x80134360]`

### 3.1 Ce que la DLL a (FAIT)

- `Entities/EntitySlotTable.cs` : 64 cases (`:17`), masque `_held` (`:19`), `HeldCount` (`:22`), `IsHeld` (`:25`), `Allocate` (plus bas libre de 1 à 63, `:28-41`), `HoldHeroSlot` (`:44`), `Release` (`:47-54`, ignore 0 et hors table). Pas d'accesseur du plus haut tenu.
- `Maps/WorldProxy.cs:182-194` `HighestLiveSlot()` : privé, parcourt `_spawnedEntities` et rend le plus grand `EntityScriptProxy.Slot` (−1 sans entrée).
  - Son seul usage est `ReportSlot` (`:257-264`, ligne `:264`), sonde `SlotShadow` active seulement avec la variable d'environnement `ALUNDRA_SLOT_SHADOW_LOG` (`Probes/SlotShadow.cs:15`).
  - Aucun test ne la lit.
- `_spawnedEntities` est tenu dans l'ordre des emplacements (`InsertBySlot` `:216-232`). Le héros y est en 0 (`:2302-2304`). Une entrée sans emplacement (montage de test par réflexion) vaut −1.
- Le recyclage : `RecycleDestroyedEntities` `:3265-3289`.
  - Il est appelé après chaque passe d'événements de carte quand le héros existe et que la porte est ouverte (`:2557`, `:2572`).
  - Porte fermée : retour immédiat (`:3267-3270`).
  - Il retire l'entité de `_spawnedEntities`, libère l'emplacement (`:3284`) et met `Slot = -1`.
- Trois sites prennent un emplacement : chargement `:1074`, enregistrement `:3399`, indice de sprite `:3528`. Chacun passe par `HoldSlot` (`:200`) et `InsertBySlot` (`:216`).

### 3.2 Ce que fait le binaire (FAIT, relu)

- `0x80038634` (recyclage) : pour chaque case 0 à 63 :
  - une case de statut 4 est recopiée depuis le modèle `0x80134368` (statut 0, indice de case réécrit en `+0`) et ne compte pas ;
  - une case de statut autre que 0 compte (`t2 = indice`) ;
  - `[0x80134360] = t2` en `0x800386CC`.
  - La valeur est donc le plus haut indice d'une case de statut ∉ {0, 4} au moment du passage.
- Relèvement à la naissance (`0x80039D24-0x80039D3C`) : si `[0x80134360] < indice de la case née`, il prend cet indice.
- Remise à 0 à l'initialisation de la table (`0x8003B2D8`).
- Le passage `0x80038634` est appelé en `0x8003B3A0` après le test `[0x800DC4B8] & 0x48` (`0x8003B38C-0x8003B398`). Porte fermée : ni recalcul ni recyclage, comme la DLL (`:3267`).
- La requête balaie `1..[0x80134360]-1` (`slt t3, count`, `0x8003AF98` et `0x8003B0A8`). Les autres passes vont de `1` à `count` inclus.

### 3.3 Où il atterrit (INFÉRENCE, prémisses données)

- Valeur : le plus haut emplacement tenu de la table, `63 - LeadingZeroCount(_held)` (la case 0 est tenue dès la construction : au moins 0).
  - Équivalence avec le binaire : la table tient une case dès l'`Allocate` (naissance, comme le relèvement) jusqu'au `Release` du recyclage (comme le recalcul).
  - Une entité de statut 4 garde sa case jusque-là (test `AlundraEntitySlotWorldTests.cs:139`), exactement comme le binaire la compte jusqu'au passage suivant.
  - Les deux seuls écarts : `AbortCreation` libère une case que le binaire n'abandonne jamais (chemin de faute), et `SlotTableFactoryForTests` (`WorldProxy.cs:176`) peut précharger des cases tenues par personne.
- Alternative : rendre `HighestLiveSlot()` (`:182`) lisible. Même valeur, calculée sur les entités, elle ignore les montages sans emplacement. Les deux donnent la même valeur en production.
- Aucun « relèvement à la naissance » ni « recalcul par tick » à écrire : l'état est déjà exactement tenu par `Allocate`/`Release`. Un membre stocké serait redondant et demanderait une remise à zéro (nouvelle carte = nouveau `WorldProxy` = nouvelle table : `:170`).
- Exposition :
  - la requête est appelée par des gestionnaires de sprite (`NativeSpriteHandler(entity, IEntityWorldContext, GameState)`, `Ai/NativeSpriteRegistry.cs:20`) et par le pilote du vent 1 du héros ;
  - ce dernier atteint le monde par `host is IEntityWorldContext world` (précédent `Hero/PlayerManager.cs:1218`) ;
  - l'interface `IEntityWorldContext` (`Maps/IEntityWorldContext.cs:26`) n'a ni le plus haut emplacement ni la requête. Il faut un membre par défaut (précédents `:81`, `:116`, `:193`) ;
  - il doit être par défaut, car au moins 38 doubles de test l'implémentent (`rg`, comptage approché) et un membre abstrait casserait leur compilation.
- Type : l'interface est publique ; un membre ne peut exposer que des types publics (`EntityScriptProxy` l'est, `EntitySlotTable` non).

### 3.4 Tests

- Rien ne bouge (FAIT : aucun test ne lit `HighestLiveSlot`, `SlotShadow` n'est pas référencé par les tests).
- Épinglent le comportement sur lequel on s'appuie :
  - `AlundraEntitySlotWorldTests.cs` : `:77` slots du chargement, `:97` plus bas libre et ordre de la liste, `:117` entrées sans emplacement, `:139` détruite garde sa case jusqu'au recyclage et porte fermée, `:169` 64e refusée ;
  - `AlundraEntitySlotTableTests.cs` : V5 `:26-80`, héros `:97-115`, `:161` deux tables indépendantes ;
  - `AlundraEntitySlotV5RowsTests.cs`.
- À écrire : plus haut tenu après naissance, après libération du plus haut, case 0 seule, porte fermée.

## 4. Brique 3 : la requête de cibles `0x8003AF70` et le tri `0x8003AE04`

### 4.1 Ce que la DLL a (FAIT)

- `Entities/EntitySearchService.cs:75` est la déclaration de la classe, `:99` la méthode `GetMatchingEntitiesBySearchType`.
  - C'est le port de `0x8003C954` (les 12 requêtes canoniques des opcodes `0x2E`, `0x62`..`0x65`, `0xAC`...).
  - Elle prend un type de recherche de script et parcourt toute la liste. Elle ne trie rien, ne filtre ni par drapeaux, ni par fiche d'équilibrage, ni par distance, et alloue une `List` par appel.
  - Elle n'est appelée que par `Events/EventProgramRunner.cs` (27 sites, `:791` à `:3382`) et `AlundraDestroyedEntityRecyclingTests.cs:63`.
- FAIT : il n'existe dans `Alundra/` ni requête de cibles, ni tri (`rg Sort(|OrderBy|Hoare` : seulement `EffectStage.cs:232` et `DialogueDirector.cs:305`, sans rapport). `Ai/EnemyHelpers.cs` n'a que `RelativePosition` et `TryAttackPlayerFront` (le héros comme seule cible).
- FAIT : `IEntityWorldContext.SpawnedEntities` est un instantané alloué à chaque lecture (`WorldProxy.cs:3657-3670`, doc : « Not a per-frame hot path »).

### 4.2 Pourquoi elle diffère (FAIT, binaire)

La requête `0x8003AF70` (annexe `work/lst/f_8003AF70.txt`, relue) :
- exclut l'appelant ;
- garde les statuts 2 ou 3 ;
- exige `+0x20 == 0` et `(+0x6C & masque) != 0` ;
- exige `+0x1D0 == 0` et `+0x1CC == 0` ;
- exige `(+0xB4 & 0x40) == 0` ;
- exige une fiche `+0x1C4` non nulle et `(octet[fiche + type + 2] & 0xC0) != 0x80` ;
- calcule la distance² entre positions `>> 16`, dans deux tableaux parallèles (pointeurs, distances) ;
- trie par Hoare seulement à partir de 3 résultats.

Aucune des canoniques de `EntitySearchService` ne fait cela.

### 4.3 Chaque filtre a son champ dans la DLL (FAIT, sauf une nuance)

| Binaire | DLL | Où |
|---|---|---|
| statut `+0x10` | `Status` | `EntityScriptProxy.cs:88` |
| `+0x20` | `BlockedByEntity` | `:99` |
| `+0x6C` | `Flags` | `:156` |
| `+0x1CC` / `+0x1D0` | `DamagedTickCounter` / `FrameCollisionTickCounter` | `:483` / `:484` |
| `+0xB4` | **`HitAnimByte`** | `EntityScriptProxy.HitPass.cs:30` |
| fiche `+0x1C4` | `BalanceRecord`, octet par `ByteAt(2 + type)` | `HitPass.cs:24`, `Combat/BalanceRecord.cs:76` |
| `+0x114/+0x118` | `PosX`, `PosY` (16.16) | `EntityScriptProxy.cs:351-352` |

- Nuance (FAIT) : `AnimFlags` (`:337`) reçoit la même valeur une frame trop tard (écrit en fin de frame, D-E19-13). `HitAnimByte` est l'octet du binaire chargé au moment du changement d'animation (doc `HitPass.cs:28-30`). `AlundraBirthTickTests.cs:485` puis `:495-496` les montrent égaux à la naissance puis après la première frame. La requête doit lire `HitAnimByte`.
- Les fiches de sorts sont lues par `BalanceRecord.ByteAt` : l'octet `type + 2` peut sortir de l'enregistrement et lire le suivant (`BalanceRecord.cs:78-82`), comme au binaire.

### 4.4 Où elle atterrit (INFÉRENCE)

- Un fichier nouveau (voisin de `EntitySearchService.cs` ou dans `Alundra/Combat/`), statique.
  - Il prend l'appelant, le masque, le type d'attaque, le plus haut emplacement et la liste des entités en ordre d'emplacement, et rend deux tableaux ou une structure.
  - Le tri est écrit à la lettre d'après `check/o/my_query.py` : pivot = `(clé[bas] + clé[haut])` non signé `>> 1`, balayages croisés, échange de la clé et du pointeur, récursion `(bas, i-1)` puis `(j+1, haut)`.
  - Sans allocation par tick si possible : C65 l'appelle à chaque tick et la règle du dépôt l'interdit (doc `ScriptedMotion.cs:49-53`).
- Le balayage `1..count-1` : parcourir les entités de `_spawnedEntities` dont `Slot` va de 1 à `highest - 1`. L'entité du plus haut emplacement n'est donc jamais visée (FAIT binaire). Corriger ou reproduire relève de Q-H4-2 (auteur).
- Le tri instable donne un ordre différent d'`Array.Sort` sur les égalités (D10) : à écrire à la lettre.

### 4.5 Tests

- Aucun ne bouge. À écrire :
  - vecteurs du modèle `my_query.py` ;
  - montages avec `Slot` posé (une entrée à `Slot = -1` est invisible du balayage) ;
  - ordre des égalités ;
  - moins de 3 résultats non triés ;
  - `HitAnimByte` contre `AnimFlags`.

## 5. Brique 4 : les tables sin/cos à 256 entrées

- FAIT : `rg 800204F0|800206F0|sin256|SinTable|CosTable` ne trouve rien.
  - Le seul gabarit de table de directions est `Entities/AnimationTables.cs` : `OffsetXList` (`:71`) et `OffsetYList` (`:82`), 32 directions, `short`, amplitude X 768 (`0x300`) et Y 512 (`0x200`). Le rapport 3:2 y est cuit.
  - Consommateurs : `ScriptedMotion.cs:632-633` et `EventProgramRunner.cs:2985-2986`.
  - Autres tables du binaire du même fichier : `CardinalDirectionTable`, `DashDirectionByNibble` (`0x80022CAC`), `SprintConeRows`, `HeightsTable_800236d4`.
- FAIT (binaire, `tools/tables256.py`) : `0x800204F0` (sin) et `0x800206F0` (cos) sont 256 `short` chacune, amplitude 256.
  - Valeurs : `sin[64] = 256`, `sin[128] = 0`, `sin[192] = -256`, `sin[1] = 6`.
  - `sin[i] = trunc(256·sin(2πi/256))` sur les 256 entrées (`round` n'en donne que 136).
  - `cos[i] = sin[(i + 64) & 255]` et `sin[(i + 128) & 255] = -sin[i]` partout.
- FAIT : seuls C65 (`0x8007AF74/0x8007AF7C`, indice `8·idx`) et C75 (`0x8007BE54/0x8007BE78`, indice `a` de 0 à 255) lisent ces tables (`scan_refs.py`).
- Où (INFÉRENCE) : `Entities/AnimationTables.cs`, même registre de tables du binaire avec l'adresse en commentaire, deux tableaux de 256 `short`.
  - Épingle sur le modèle de `AlundraHeroRunTablesTests.cs:46-64` (contenu littéral), plus un test de génération (`trunc(256·sin)`, qui reproduit le binaire).
- À éviter : la décompilation a d'autres tables, `g_sinus`/`g_cosinus` de 512 entrées, amplitude 512 (`StaticVariables.cs:10930`, `:10998`). `FunctionTypeC.cs:16886-16887` les utilise pour C65 : c'est l'écart D13 de la critique. Le binaire fait foi.
- Tests : aucun ne bouge.

## 6. Brique 5 : les accès 32 bits sur `+0x274` et `AIValues`

### 6.1 Ce que la DLL a (FAIT)

- `Entities/EntityScriptProxy.cs:1227-1230` :
  - `public byte[] Bytes = new byte[4]` (`+0x274`) ;
  - `public int DelayOrAngleOrEntityId` (`+0x278`) ;
  - `public int ItemState` (`+0x27C`) ;
  - `public short[] AIValues = new short[10]` (`+0x280..+0x293`).
  - `AIValues` couvre exactement les cinq mots `+0x280..+0x290` de l'eau.
- Utilisateurs de `Bytes` :
  - `Ai/Enemies/MurutaAi.cs` (`:93-98`, `:129`, `:145`, `:157-163`, `:199`, `:209-210`, `:235`, `:322-324`, `:354`) ;
  - `Ai/Enemies/BeannoideAi.cs` (`:89-93`, `:104`, `:144`, `:161`) ;
  - `Ai/EnemyHelpers.cs:234` ;
  - `Ai/Objects/SaveBook.cs:164-171`.
- Utilisateurs de `AIValues` : `MurutaAi.cs:56`, `:150`, `:215`, `:330`, `:361` ; `BeannoideAi.cs:74`, `:196` ; `EnemyHelpers.cs:221`. Seuls les indices 0 et 1 sont lus.
- Utilisateurs de `DelayOrAngleOrEntityId` : `SaveBook.cs:264`, `:273-274`, `:322`, `:332-333`. De `ItemState` : personne.
- Précédent de composition de 4 octets en un mot, petit-boutiste : `SaveBook.cs:161-172` (`ReadState` interne, `WriteState` privée).
- Sérialisation : `EntityScriptProxy` ne redéfinit ni `Save` ni `Load`. `GameplayProxy.Save` n'écrit que `id` et `name` (`CasaEngineMonogame/CasaEngine/Framework/Scripting/GameplayProxy.cs`). Les sauvegardes de partie ne contiennent aucune entité, donc rien de ces champs n'est sérialisé.
- `Clone` : `EntityScriptProxy.cs:2716-2719` copie `Bytes` et `AIValues` par `Clone()` et les deux `int` par valeur. `AlundraEntityScriptProxyCloneTests.cs:41-42`, `:55-59` exigent l'égalité de contenu et `NotSame`.

### 6.2 Où atterrit le 32 bits (INFÉRENCE)

- Des accesseurs par-dessus les tableaux existants (propriétés ou méthodes sur le proxy, ou assistants sur `byte[]`/`short[]`), jamais un changement de type.
  - `Bytes[0..3]` et `AIValues[k]` restent la vérité, vus en mots petits-boutistes.
  - Pour `AIValues`, le mot `k` vaut `(ushort)AIValues[2k] | (AIValues[2k+1] << 16)`.
  - La DLL n'utilise que les indices 0 et 1 aujourd'hui, donc C75 (indices 0 à 9) n'aliase rien d'un autre type d'entité.
- Si l'on changeait la représentation (mots `int`), bougeraient :
  - `AlundraBeannoideOracleTests.cs:73-77`, `AlundraMurutaOracleTests.cs:72-76`, `:397`, `:412` ;
  - `AlundraEnemyHitReactionTests.cs:58`, `:80`, `:96` ;
  - `AlundraEnemyHelpersTests.cs:145`, `:151` ;
  - `AlundraSaveBookDispatchTests.cs:146` ;
  - `AlundraEntityScriptProxyCloneTests.cs:41-59` ;
  - les quatre fichiers `Ai/`.
  - À éviter (INFÉRENCE).
- PIÈGE (FAIT, lu) : la décompilation a ces accesseurs sous `ScriptHelper.cs`.
  - `:126-129` : `GetInt32(byte[])`, correct.
  - `:166-169` : `GetInt32(short[])`, faux. `array[0] | (array[1] << 16)` étend le signe d'une moitié basse négative sur toute la moitié haute.
  - `:171-182` : `Set(short[], ...)` convertit en `(byte)` et tronque à 8 bits.
  - À ne pas recopier : masquer la moitié basse et écrire deux `short`.
- Note de lecture : C63 range un pointeur en `+0x278` et une identité en `+0x27C` (rapport h4 §4). La DLL n'a pas de pointeur d'entité dans ces champs `int`. C'est un choix de la tranche d'élément (h4d2), pas de h4b.

### 6.3 Tests

- Aucun ne bouge avec des accesseurs. À écrire : petit-boutiste (octets ↔ mot, mots négatifs, moitiés négatives de `AIValues`), indépendance des mots voisins, `Clone` inchangé.

## 7. Brique 6 : le coup de caméra Y d'un tick et le tampon du tremblement

### 7.1 Ce que la DLL a (FAIT)

- `Cameras/CameraSway.cs` : singleton de session `CameraSway.Instance` (`:26`).
  - Neuf propriétés `internal set` (`:33-41` : `Flag`, `LimitX/Y`, `SpeedX/Y`, `OffsetX/Y`, `ReachX/Y`).
  - `Start` (`:45`), `Stop` (`:55`), `OnMapLoad` (`:58`, remet seulement `Flag` à 0, appelé en `Maps/WorldProxy.cs:880`), `Step` (`:65`), `ResetForTests` (`:116`).
  - Aucune capture ni restauration des neuf champs.
  - Joignable par `CameraSway.Instance` (statique) ou `IEntityWorldContext.CameraSway` (`Maps/IEntityWorldContext.cs:193`, `WorldProxy.cs:699-701`).
- `Cameras/CameraDirector.cs:214-281` `UpdateCameraFollow` :
  - `swayActive = !WarpDirector.Instance.IsTransitionInProgress` (`:222`) ;
  - sans caméra, la boucle `sway.Step()` seule (`:224-236`, avant le retour) ;
  - avec caméra, `CameraMath.AdvanceCameraSmoothing(..., swayActive ? sway : null)` (`:268-270`) ;
  - `_cameraNeedsSnap` n'est consommé que si la frame a un tick (`:262`, `:272-275`).
- `Cameras/CameraMath.cs:167-194` `AdvanceCameraSmoothing` :
  - sur un cliché, le tick de cliché fait `sway?.Step()` sans décalage ni pas de suivi (`:174-183`) ;
  - puis, par tick, `sway.Step()` et `ComputeSmoothedCameraTarget(..., sway?.OffsetX ?? 0, sway?.OffsetY ?? 0)` (`:185-191`).
- `ComputeSmoothedCameraTarget` (`:216-228`) : hors cliché, pas de suivi `+ new Vector3(swayOffsetX, -swayOffsetY, 0f)` (`:222-223`), puis borne de carte (`:225-227`).
  - Signe de Y (`:223`, doc `:210-214`, `ToOriginalScrollSpace` `:301-302` : `scrollY = -renderY - 120`) : un décalage positif du défilement d'origine devient négatif en espace de rendu.
- `WorldProxy` appelle le suivi une fois par frame après la boucle des ticks (`WorldProxy.cs:2716-2720`), avec `ticksThisFrame`.

### 7.2 Ce que fait le binaire (FAIT, relu : `0x8002CDA0-0x8002CEA4`, `0x8002CD54-0x8002CD8C`)

- Le suivi, par tick :
  - `jal 0x8002C894` (pas du tremblement) ;
  - si `[0x800E42B8] != 0` (cliché) : il le remet à 0 et pose `camX = lookX - 0xA0`, `camY = (lookY - lookZ) - 0x88`, sans tremblement ni coup ;
  - sinon `camX += ((lookX - (camX + 0xA0)) >> 4) + OffsetX(0x800E4338+0x14) + [0x800DD870]` et `camY += ((lookY - lookZ - (camY + 0x88)) >> 4) + OffsetY(0x800E4338+0x18) + [0x800DD874]`. Le coup Y est lu en `0x8002CE6C`, le coup X en `0x8002CE2C`.
  - Les deux coups sont remis à 0 en queue commune (`0x8002CE8C` et `0x8002CE98`), **cliché compris**. La borne `0..0x39F` etc. vient ensuite.
- À la charge de carte (`0x8002CD54`) : `Flag = 0`, cliché armé, `[0x800DD868] = 0x3C`, `[0x800DD874] = 0` (`0x8002CD7C`), `[0x800DD870] = 0` (`0x8002CD8C`).
- `[0x800DD874]` n'a qu'un écrivain, E13 (`0x8007F350`, valeur 2). `[0x800DD870]` n'en a aucun trouvé (3 références : les deux remises à zéro et la lecture ; balayage par `lui`+déplacement). INFÉRENCE : le coup X est dormant.
- Le tampon `0x80127138` n'a que quatre sites, tous du feu 2 : `0x80034E14` (annulation), `0x80035D04`, `0x80035D88`, `0x80035E28` (pilote).
- Le bloc de tremblement est `0x800E4338..` : `+0` Flag, `+0x14` OffsetX, `+0x18` OffsetY, neuf mots, cohérents avec les neuf champs de `CameraSway`.

### 7.3 Où ça atterrit (INFÉRENCE)

- Coup Y : un état de session sur `CameraSway`, remis à zéro par `ResetForTests` et donc par `SaveGameDirectorTestSupport.cs:134`, `AlundraCameraSwayTests.cs:28`, `:37`, `AlundraCameraSwayWiringTests.cs:39`.
  - Il est consommé dans `AdvanceCameraSmoothing` à chaque tick plein, en l'ajoutant à `OffsetY`. `OffsetY + coup` entre dans `-swayOffsetY` (`CameraMath.cs:223`), soit −2 en rendu pour un coup de 2. La critique h4 §3.7 le disait déjà ; c'est maintenant lu aux deux bouts.
  - Remise à zéro à chaque tick plein ET sur le tick de cliché (binaire `0x8002CE8C/98`), dans la branche sans caméra du suivi (`CameraDirector.cs:224-236`), et à la charge de carte (`CameraSway.OnMapLoad`, `:58`).
  - Écriture par E13 via `CameraSway.Instance` ou `context.CameraSway`.
- Granularité (FAIT du code, INFÉRENCE de la conséquence) : le suivi de la DLL est un lot par frame après tous les ticks (`WorldProxy.cs:2716`), alors que le binaire consomme le coup au tick même de son écriture.
  - Sur une frame de plusieurs ticks, le coup posé par le tick k serait consommé au premier tick plein du lot.
  - La boucle de ticks de la frame (`WorldProxy.cs:2524`) a un précédent de registre par tick (`_effectFrozenOfTick`, `_squareOfTick`) si l'on veut l'exactitude.
- Tampon du feu 2 : un tableau de neuf entiers de session sur `CameraSway` (`Capture`/`Restore`).
  - `Restore` est sans garde : un tampon jamais écrit remet le bloc entier, `Flag` et `Reach` compris, à 0.
  - Il est initialisé à 0 et remis à 0 par `ResetForTests`. Il persiste d'une carte à l'autre comme le tremblement (`OnMapLoad` ne touche que `Flag`).
  - Piège : `Step` (`:65-89`) vide `Limit`, `Speed`, `Offset` quand `Flag == 0`. Un bloc restauré avec `Flag == 0` est vidé au pas suivant (critique h4 §B8).
- Autre emplacement possible pour le tampon : une classe de session voisine. Mais `CameraSway` est déjà réinitialisé par tous les `ResetSingletons`.

### 7.4 Tests

- Rien ne bouge si le coup et le tampon restent à zéro et si les paramètres ajoutés à `AdvanceCameraSmoothing` sont facultatifs et en dernière position.
- Tests qui épinglent la structure :
  - `AlundraCameraSwayFollowTests.cs` : appels positionnels `:41`, `:73`, `:86`, `:102`, `:116`, `:124`, `:132-134` ; cliché de trois ticks `:69-80`, de zéro tick `:82-90` ;
  - `AlundraWorldProxyCameraFollowTests.cs` : `:246-255`, `:278`, `:300-302`, `:325`, `:381-387`, `:466-477` ;
  - `AlundraCameraSwayTests.cs` : séquences `:63-204`, charge de carte `:220`, `ResetForTests` neuf champs `:239` ;
  - `AlundraCameraSwayWiringTests.cs` : `:64`, `:97`, `:115` ;
  - `AlundraWorldProxyUpdateCharacterizationTests.cs` (7 références) ;
  - arcs `AlundraDay3SceneArcTests`, `AlundraShipBlockArcTests`, `AlundraShipCorridorArcTests`, `HeroTraceHarnessTests`.
- À écrire : coup consommé une fois ; remis à zéro sur cliché, sans caméra, à la charge de carte ; jamais pendant une transition. Pour le tampon : sauvegarde puis restauration, restauration d'un tampon jamais écrit, persistance entre cartes, `ResetForTests`.

## 8. Brique 7 : le registre et l'inscription commune d'E3

### 8.1 Ce que la DLL a (FAIT)

- `Ai/NativeSpriteRegistry.cs` : `CreateForProduction` (`:36-43`) = `BeannoideAi.Register`, `MurutaAi.Register`, `WaveSwordAi.Register`, soit 7 clés :
  - (A,1), (C,1), (D,1) pour 335 ;
  - (A,0), (C,10), (D,9) pour 347 ;
  - (C,61) pour 27, 28, 29.
- `Register(slot, code, types, handler)` (`:54-67`) : une clé = une inscription, restreinte à un ensemble de types. Un doublon lève `InvalidOperationException` (`:60-63`). `TryGet` teste la clé puis le type (`:70-72`). `Dispose` retire toute la clé (`:95-98`).
- Les types : indice seul sur la page globale, `0x100 + indice` sur la page de carte (`Entities/Spawning/EntitySpawnFactory.cs:541`).
- Le compte est épinglé (§8.4). `E0`, `E1`, `E10` ne passent pas par le registre (`Events/EventProgramRunner.cs:283-292`, prédicat `:328`).
- Aucune clé (E,3), (E,13), (E,28), (C,63), (C,65), (C,75) aujourd'hui.

### 8.2 Qui porte quoi (FAIT, données)

- `alundra-project/Data/sprite-records.json` (395 en-têtes) :
  - E3 = page globale 16, 23, 150, 151, 152, 153 ;
  - E3 = page de carte (clé de banque numérique) 74, 95, 101, 110, 116, 155, 176, 188, 206, 212, 218, 219, 227, 242, 254, soit 15 indices distincts, types `330, 351, 357, 366, 372, 411, 432, 444, 462, 468, 474, 475, 483, 498, 510` ;
  - E13 = global 17 et carte 213 (type 469) ;
  - E28 = global 21 seul ;
  - C63 = 23 ; C65 = 24 et 154 ; C75 = 19 (tous globaux).
- FAIT (`tools/scan_e3.py`, 484 cartes de `data-extracted/data`, lu seulement) : les 15 indices de carte portent E3 dans 98 couples (carte, indice) sur 75 cartes (18 + 10 + 10 + 5 + 9 + 9 + 4 + 5 + 2 + 6 + 5 + 5 + 3 + 3 + 4). Les 6 globaux (et 17, 21) sont dans `map_alundra.json`.
  - Les « 98 sprites » de `h4/report.md` §0.7 sont donc des placements. L'inscription porte sur 15 types de carte, plus les 6 globaux.
  - E13 : 2 couples (17 global, 213 carte 321). E28 : 1.
- Seuls (E,3) et (E,13) sont donc partagés avec des types d'E14. (E,28), (C,63), (C,65), (C,75) sont propres aux sorts.

### 8.3 Comment faire l'inscription commune (INFÉRENCE ; options)

- (a) Un seul site d'inscription qui connaît l'union des types : une liste statique (par exemple dans le fichier du corps d'E3) lue par `CreateForProduction`. Aucun changement de l'API. h4 y met {16, 23, 150..153} et E14 ajoute ses types de carte à la liste.
- (b) Un `Register` cumulatif (E14.b0, `docs/plan-e14-ia-native.md:881-882`, `:903`) : une seconde inscription de la même clé avec le même gestionnaire étend l'ensemble de types.
  - Il change la sémantique épinglée par `AlundraNativeSpriteRegistryTests.cs:238-251` (`Registering_TheSameKeyTwice_Throws_AndDisposingRemovesTheEntry` : le doublon lève, le compte revient à sa valeur).
  - Il change aussi celle de `Dispose`, qui retire toute la clé.
- Le corps d'E3 est le même pour tous les types (une seule adresse, `0x8007EDA0`). Restreindre la clé aux seuls types voulus est un choix de la DLL, pas du binaire.
- Inscrire E3 pour les 15 types de carte change le jeu pour 98 placements (ils passent aujourd'hui en no-op compté). C'est le périmètre d'E14, pas de h4b.
- Un enregistrement restreint à {16, 23} ne touche aucun placement de carte (les types de carte sont `0x100+`).

### 8.4 Tests qui épinglent le compte et le comportement (FAIT)

- `AlundraNativeSpriteRegistryTests.cs:55` `Assert.Equal(6 + 1, runner.NativeSprites.Count)` (nom `…SevenHandlers…` `:51`, doc de classe `:14-17`). Il devient 8 dès qu'une clé de production est enregistrée (h4b : la clé (E,3) si l'inscription est de production), puis +1 par clé (E,13), (E,28), (C,63), (C,65), (C,75).
- `AlundraWaveSwordLifeTests.cs:166-188` : `(3 + 3 + 1, …)` `:182`, nom `…SevenKeys…` `:167`, `registry.Count` `:183`.
- Neutres :
  - `AlundraNativeSpriteRegistryTests.cs:84-96` (aucune inscription ne liste −1 : vrai si les types sont explicites) ;
  - `:238-251` (doublon sur la clé libre (C,25)) ;
  - `:205-236` (E0/E1 hors registre) ;
  - `AlundraWeaponSpawnSeamTests.cs:238-258` (inscrit (E,17) sur le registre de production ; il lèverait si une inscription de production (E,17) arrive, sans rapport avec h) ;
  - `AlundraHitArena.cs:70` (coupe les gestionnaires natifs) ;
  - `AlundraBirthBoxTests.cs:131` (fait naître tous les préfabs globaux, sans tick).
- Compteurs d'exécution (`NativeHandlerRunCount`) : seules `AlundraBeannoideOracleTests.cs:263`, `:330`, `AlundraMurutaOracleTests.cs:316`, `:398` et `AlundraMurutaHallArcTests.cs:100` les lisent, sur des types 335/347. Neutres pour E3/E13/E28/C63/C65/C75.

## 9. Tableau : ce qui bouge, brique par brique

| Brique | Test existant qui bouge | Condition | Tests qui épinglent ce qu'on touche (ne bougent pas) |
|---|---|---|---|
| 1 Poussée | aucun | poussée nulle ; 4 lignes d'oracle bougent seulement si le champ est renommé/retypé | `AlundraBeannoideOracleTests.cs:93-94`, `AlundraMurutaOracleTests.cs:92-93`, mouvements réels (§2.5) |
| 2 Plus haut emplacement | aucun | accesseur nouveau | `AlundraEntitySlotWorldTests`, `AlundraEntitySlotTableTests`, `AlundraEntitySlotV5RowsTests` |
| 3 Requête | aucun | fichier nouveau + membres d'interface par défaut | aucun test de requête n'existe |
| 4 Tables 256 | aucun | membres nouveaux de `AnimationTables` | `AlundraHeroRunTablesTests.cs:46-64` (modèle) |
| 5 32 bits | aucun avec des accesseurs ; environ 12 fichiers de test si la représentation change | | Clone `:41-59`, oracles `:73-77` |
| 6 Coup Y / tampon | aucun si inerte et paramètres facultatifs | | §7.4 |
| 7 Registre | `AlundraNativeSpriteRegistryTests.cs:55` ; `AlundraWaveSwordLifeTests.cs:182-183` | dès qu'une clé de production est inscrite | `:238-251` change si le registre devient cumulatif |

## 10. Écarts et compléments aux rapports lus

1. `e20-h4/report.md` §8 et `state.md` R27 : la poussée est aussi consommée pour le héros. Le binaire saute la passe pour une entité porteuse (`+0x28` non nul) en laissant la poussée intacte (§2.3). `state.md` ne le voyait pas.
2. `state.md` R28 et le rapport : `AIValues` n'est utilisé que sur les indices 0 et 1 ; les cinq mots de l'eau n'aliasent rien.
3. `check.md` §3.1 (l'indice du plus haut emplacement) : il n'y a ni recalcul ni relèvement à écrire, `EntitySlotTable` le tient déjà (§3.3). `WorldProxy.HighestLiveSlot` existe (privé, `:182`) mais est redondant.
4. `state.md` R33/S3 : les 98 sont des couples (carte, indice) ; il y a 15 types de carte distincts (§8.2).
5. Rapport §4 : la requête lit `+0xB4`, que la DLL tient dans `HitAnimByte`.
6. Le coup X `[0x800DD870]` existe au binaire (lu, remis à zéro, ajouté à X) mais n'a aucun écrivain trouvé : dormant (§7.2).
7. Le tampon du tremblement (4 sites, feu 2 seul) et le coup Y (un écrivain, E13) sont confirmés au binaire.
8. `ScriptedMotion.cs:609` : le commentaire « riding-platform force feed » range les champs de poussée sous un mauvais intitulé.

## 11. INCONNU et laissé ouvert

- INCONNU : le sens du bit `0x2000` (`NoObstacleSlide` côté DLL) dans le binaire.
- INCONNU : l'effet exact d'une poussée sur `SlideAlongWall` (à mesurer sur l'orbe C75 en vol).
- INCONNU : la présence d'une branche équivalente à « porteur » pour les entités nées par l'interface.
- INCONNU : si un sprite 154 (C65, global) est placé par une carte (non relu) ; le contenu des pistes XA ; les sons des effets.
- INCONNU : le détail du pilote du vent 1 qui appelle la requête depuis le héros (adresse `0x80035FA4`). La DLL l'atteindra par `host is IEntityWorldContext`.
- INFÉRENCE non mesurée : l'équivalence du plus haut emplacement tenu avec `[0x80134360]` (§3.3) ; le report du coup Y sur une frame de plusieurs ticks ; la non-allocation de la requête.
- Questions de l'auteur déjà posées :
  - Q-H4-2 : balayage `count-1`, tri instable, reproduire ou corriger.
  - Q-H4-3 : propriétaire de la poussée. Faits en §2.3 : canal général, une seule fonction.
  - Q-H4-8 : inscription commune d'E3.

## 12. Fichiers (dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/dll/`)

- `state.md` (ce rapport).
- `tools/` :
  - copies de `exelib.py`, `lin.py`, `scan_refs.py`, `scan_jal.py` ;
  - `tables256.py`, `scan_push.py`, `scan_e3.py` ;
  - `verify_cites.py`.
- Rejouer : `python -B <script>` depuis `tools/`.