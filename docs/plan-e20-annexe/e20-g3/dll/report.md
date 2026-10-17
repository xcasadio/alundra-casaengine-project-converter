# E20.g3 — découverte DLL en lecture seule : les caisses et cruches redeviennent solides

Dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`, HEAD `c84fd72`. Rien n'a été écrit dans le dépôt, construit, testé ni lancé. Seuls des scripts Python en lecture seule ont tourné.

**Livrables.** `report.md` n'a pas pu être écrit : l'outil Write l'a refusé (« Subagents should return findings as text »). Ce message EST le rapport. Les scripts, leurs sorties et `MANIFEST.txt` (SHA-1 de 36 fichiers) sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g3/dll/`.

Étiquettes : **FAIT** = vu dans un fichier (`fichier:ligne`) ou sortie d'un de mes scripts ; **INFÉRENCE** = déduit, non vérifié ; **INCONNU** = non établi. Les chemins sont ceux d'après la restructuration (`tools/dll-structure/table.json`, extrait dans `rename_subset.tsv`) :
- `AlundraMovementObstacleProbe` → `Alundra/Physics/MovementObstacleProbe.cs`
- `AlundraEntityCollision` → `Alundra/Physics/EntityCollision.cs`
- `AlundraEntitySupport` → `Alundra/Physics/EntitySupport.cs`
- `AlundraScriptedMotion` → `Alundra/Physics/ScriptedMotion.cs`
- `AlundraEntityScriptProxy` → `Alundra/Entities/EntityScriptProxy.cs`
- `AlundraWorldProxy` → `Alundra/Maps/WorldProxy.cs`
- `AlundraEventProgramRunner` → `Alundra/Events/EventProgramRunner.cs`
- `AlundraPlayerManager` → `Alundra/Hero/PlayerManager.cs`

Les tests gardent leur nom `Alundra*` dans `Alundra.Tests/`.

Scripts (`python -I -B`) :
- `census_liftables.py` : recensement des soulevables.
- `lift_layout.py` : empreintes par carte.
- `decode_events.py` : balayage linéaire des programmes d'événements, tailles lues dans `EventOpcodeSizeTable.cs`.
- `segment_check.py` : boîte balayée contre empreintes.
- `walkers.py` : acteurs qui marchent et écart aux soulevables.
- `reach_check.py` : borne grossière, peu utile.
- `portal_arrivals.py`, `warp53_arrivals.py`, `placement_overlaps.py`, `spawn_overlaps.py` : chevauchements à l'arrivée, au placement et à l'apparition.
- `prefab_flags.py` : drapeaux d'en-têtes.

---

## B1. Chemins de code qui excluent les soulevables

### B1.1 Le site unique du masque (FAIT)

- `MovementObstacleProbe.cs:48-51` appelle `EntityCollision.FindEntityCollisionCandidate(proxy, candidateX, candidateY, proxy.PosZ, _host.Collidables, EntityFlags.PickupKindMask)`. C'est le **seul** site de production qui passe un masque. Doc du site : `MovementObstacleProbe.cs:13,23-24`.
- Le paramètre est déclaré `EntityCollision.cs:37-39` (`uint skippedCandidateFlags = 0`), testé `EntityCollision.cs:53` (`(candidate.Flags & skippedCandidateFlags) != 0` → saut) et documenté `:32-33`. La surcharge à deux arguments (`EntityCollision.cs:101`) ne saute rien.
- `PickupKindMask` est défini `Alundra/Entities/EntityFlags.cs:112` (0x600). `GetPickupKind` : `:202-203`. `ToString` : `:238-241`. `EntityPickupKind` : `Alundra/Entities/EntityPickupKind.cs`.
- Un grep de `PickupKind|0x600|soulev|liftable` dans `Alundra/**/*.cs` ne trouve rien d'autre que ces fichiers et des commentaires (`EntitySupport.cs:53-54`, `MovementObstacleProbe.cs:23`, `EntityCollision.cs:33`).
- La sonde est installée à chaque chargement de monde (`WorldProxy.cs:1216`). Le moteur la consulte dans l'étage champ du contrôleur pour tout pas horizontal non nul (`CasaEngineMonogame/CasaEngine/Framework/Physics/IMovementObstacleProbe.cs:10-26`, `CharacterControllerComponent.cs:1118-1160`). Il ne teste que le point d'arrivée de chaque essai (`IMovementObstacleProbe.cs:14-16`). Un objet de 15 px ne peut pas être traversé.

### B1.2 Tout ce qui lit, exclut ou garde des soulevables (FAIT)

| Chemin | Fichier:ligne | Traitement actuel des soulevables |
|---|---|---|
| Liste des collisionnables | `EntitySupport.cs:45-61` (`BuildCollidables`), reconstruite `WorldProxy.cs:3687-3699` | **Gardés** dans la liste (commentaire `:53-54` : leur exclusion appartient à la sonde). Seul `Status == FlagToDestroy` est retiré (D-E19-29). |
| Blocage horizontal | `MovementObstacleProbe.cs:51` | **Sautés** (le masque). |
| Appui vertical (dessus d'objet, D-E19-42) | `EntitySupport.cs:109-174` (`TryFindSupport`) ; `ScriptedMotion.cs:337` (héros), `:487` (autres) ; `EntityScriptProxy.cs:884,1121,1143,2210,2223` | **Comptés** : aucun filtre de bits de levage. |
| Plafond | `EntitySupport.cs:200-257` (`TryFindCeiling`) ; `ScriptedMotion.cs:360` (héros) ; `:504` passe `Array.Empty` pour les non-héros | **Comptés** pour le héros. Plafonds d'entités non portés pour les PNJ. |
| Passager (`RidingEntity`) | `EntitySupport.cs:277-340` | **Comptés**. |
| Calage à l'apex (3 px) | `ScriptedMotion.cs:438` (héros), `:580` (autres) : `FindEntityCollisionCandidate(...)` **sans masque** | **Comptés** : un soulevable qui occupe la boîte à `T'` annule déjà le calage. Sol, plafond et calage les voient déjà comme solides ; seule la marche les traverse. |
| Contact → `XCollisionEntity` | seul écrivain `EntityScriptProxy.cs:2424-2431` (remis à null, puis `H2Obstacle ?? H1Obstacle`, `EntityBlockCount++`) ; copie `:2663` | Jamais un soulevable (conséquence du masque). |
| Lecteurs de `XCollisionEntity` | `PlayerManager.cs:1750-1832` (`CheckEntityInteraction`) ; `ScriptedMotion.cs:819` (`forbidden` : pas de second pas le long du mur) ; `EventProgramRunner.cs:2952-2954` (détour de `0x0B`/`0x1E`) ; `EntitySearchService.cs:195-217` (types 7 et 8) | Voir B1.3. |
| Gardes d'arcs | `Alundra.Tests/AlundraArcSupport.cs:332` (`ArcsWithoutEntityContact`), `:353` | Échouent au `Dispose` si `EntityBlockCount` > 0. |
| Entités sans contrôleur | `ScriptedMotion.cs:704-708` (`PosX += FinalForceX`) | Jamais bloquées (pas de sonde). |
| Téléports | `EventProgramRunner.cs:2512-2527,2574-2589` (`0x64`, `0x65` → `PushLogicalPositionToRoot`, `EntityScriptProxy.cs:2368-2380` → `Controller.Teleport`) | Jamais bloqués. |

Règles de E19.d2b dans le code :
- D-E19-27 (blocage universel) : `MovementObstacleProbe`.
- **D-E19-28** : le masque.
- D-E19-29 : `BuildCollidables` sans `FlagToDestroy` (`EntitySupport.cs:53-60`), et `XCollisionEntity` alimenté par le rapport de blocage (`EntityScriptProxy.cs:2412-2431`).
- D-E19-30/35 : pas de détour quand une **entité** bloque (`EventProgramRunner.cs:2952-2954`).
- D-E19-34 : les murs à boule de fer restent solides (aucun code spécial, ils n'ont pas de bits de levage).
- D-E19-36 : un mobile qui chevauche déjà reste bloqué (aucune règle de sortie : la sonde renvoie l'obstacle tant que la boîte recouvre).
- D-E19-42 : appui (`EntitySupport`).

Le plan cite les mêmes sites (`docs/plan-e19-opcodes.md:118-146,164-179,2961-2964`).

Corps d'entités : ce sont des fantômes « Kinetic » invisibles au balayage rigide. Preuves : `Alundra.Tests/AlundraContactTestSupport.cs:165`, `EntityScriptProxy.cs:317,1493` (« the engine does not see the boxes of entities »), `ScriptedMotion.cs:356`, et le convertisseur `Writers/SpriteWriter.cs:336-339,396`. **INCONNU** : je n'ai pas relu l'adaptateur Bepu pour confirmer que `ShapeSweep` ignore ces fantômes. Les commentaires du dépôt et les arcs d'E19.d2b (« Septimus traverse rec7 » avant la sonde) le disent.

### B1.3 Ce que le retrait du masque change, mobile par mobile

Retirer l'argument rend obstacle toute entité de la liste qui recouvre la boîte candidate. Sont concernés les **sujets éligibles** (`EntitySupport.cs:73-76` : `Collidable`, `AnimFlags & 0x80 == 0`, `PlatformEntity == null`) **avec un contrôleur**. 2 250 des 2 251 soulevables de l'export sont collisionnables ; l'exception est la bombe de la 362 (rec5), non collisionnable (`census_liftables.out`, `prefab_flags.out` : `alundra_2 Flags=0x13234E`).

- **Héros.**
  - Tous ses états passent par `ScriptedMotion.RunOneKinematicTick` → `EntityScriptProxy.MoveControllerAndPullPosition` (`ScriptedMotion.cs:614,702,887`).
  - Il s'arrête au contact, affleurant à l'ULP près. `XCollisionEntity` = la caisse, `ForceAdjusted` = 1, `EntityBlockCount++`.
  - `SlideAlongWall` ne fait plus le second pas le long du mur (`ScriptedMotion.cs:819`).
  - `CheckEntityInteraction` : une caisse sans programme F (`ProgramInteract` 0 dans `sprite-records.json`) donne `res` 0. Mais `XCollisionEntity != null` sans `InteractRequiresButton` **vide le verrou d'interaction** (`PlayerManager.cs:1806-1810`).
  - Les recherches de types 7 et 8 (`EntitySearchService.cs:195-217`) peuvent trouver une caisse.
  - C'est aussi la condition du déclencheur de levage du binaire (`hero+0x130`, `docs/plan-e20-annexe/e20-armes/h-g-deps.md:149`).
- **Héros en l'air.**
  - Il passe au-dessus d'une caisse : à `top+1` le recouvrement Z disparaît (`AlundraMovementObstacleProbeTests.cs:64-67` : Z `5242881` libre, `5242880` bloqué).
  - Il ne monte pas dessus en marchant, car le balayage rigide ne voit pas les boîtes d'entités. Seul le saut atterrit dessus.
- **Héros déjà recouvrant une caisse** : reste bloqué (D-E19-36). Pour les arrivées, voir B3.2.
- **PNJ et ennemis scriptés avec contrôleur** : mêmes règles.
  - Une marche `0x0B` bloquée par une caisse attend indéfiniment (pas de détour, D-E19-30/35). `0x1F` et `0x24` voient `ForceAdjusted`.
  - Un PNJ placé ou apparu dans l'empreinte d'une caisse ne repart plus. 601 sites de placement `0x64`/`0x8A` balayés sur les cartes à soulevables donnent 16 recouvrements XY, **tous hors chaîne** (cartes 52, 92, 159 ; `placement_overlaps.out`).
  - Dans tout l'export, 2 apparitions de records se chevauchent (cartes 70 et 429, hors chaîne ; `spawn_overlaps.out`).
- **Déplacements sans contrôleur et téléports** (`0x64`, `0x65`, `0x89`, `0x53`, arrivées) : inchangés.
- **Projectiles et armes.** Les ondes d'épée (types 27-29, `Flags 0x205C`) ne sont pas collisionnables, donc jamais sujets (`prefab_flags.out`). Un objet lancé perd `0x80` (C23, plan g2).
- **Caisses elles-mêmes** : un contrôleur mais aucune force horizontale. Leurs piles restent plates (`AlundraAbsoluteZWritesTests.cs:99-108`).

### B1.4 Ce qui doit changer en même temps

1. `MovementObstacleProbe.cs:51` : ne plus passer le masque. Le paramètre `skippedCandidateFlags` (`EntityCollision.cs:37-39,53`) devient inutilisé en production. Le garder (défaut 0) ne casse rien. Le retirer casse la compilation des 3 tests qui le passent (B2).
2. Textes devenus faux : `MovementObstacleProbe.cs:13,23-24`, `EntityCollision.cs:33`, `EntitySupport.cs:53-54`, `docs/plan-e19-opcodes.md:2961`, ADR-0021 (ligne 19) et ADR-0022 (ligne 9). D-E19-28 et D-E19-46 sont à remplacer par D-E20-27/39 ; un ADR est probablement requis (INFÉRENCE, règle ADR de l'utilisateur).
3. Rien d'autre dans la DLL n'encode la déviation : pas de filtre dans la liste, l'appui, le plafond ni le passager. Aucun changement moteur n'est nécessaire.
4. Le plan prévoit un paramètre facultatif `requiredCandidateFlags` sur la même fonction (D-G-7, `docs/plan-e20-heros.md:6351`, retiré par g3). C'est la même zone de signature.
5. Données et convertisseur : rien (B4). La DLL copiée dans `alundra-project/Alundra.dll` est à rafraîchir pour une recette (mémoire de session, non vérifiée dans le dépôt).

### B1.5 Le dessus d'un objet fonctionne-t-il déjà pour les liftables ?

Oui par construction (FAIT + INFÉRENCE) : `BuildCollidables` garde les soulevables (`EntitySupport.cs:53-54`) et `TryFindSupport`, `TryFindCeiling` et `FindRidingEntity` n'ont aucun filtre de levage (`EntitySupport.cs:109-340`).

Preuves en test :
- Les vraies caisses exportées se tiennent l'une sur l'autre (`AlundraAbsoluteZWritesTests.cs:90,99-108`, cartes 390, 163, 179).
- L'atterrissage du héros sur un objet est testé avec des boîtes `Collidable` **sans** bits de levage (`AlundraHeroObjectTopsTests.cs:74-79`, SJ12 `:362-364`, `AlundraContactTestSupport.cs:191-194`).
- Aucun test n'atterrit donc sur un vrai soulevable (INFÉRENCE : pas de différence de code).

---

## B2. Tests qui épinglent la déviation

Recherche : `crate|jar|liftable|pickup|0x600|0x200|caisse|cruche|soulev|CanPickup` dans `Alundra.Tests/` (29 fichiers touchés, surtout des noms de boîtes synthétiques sans bits de levage), puis lecture des constructions.

| Test | Assertion qui repose sur des soulevables traversables | Verdict prédit si le masque est retiré (non exécuté) |
|---|---|---|
| `AlundraMovementObstacleProbeTests.TRLift_ACrateOnThePathDoesNotBlock_AnIronBallWallDoes` (`:338-364`) | `:352` `Assert.Equal(160f, Root(walker).X)` et `:353` `Assert.Null(ObstacleOf(walker))`. La caisse est `extraFlags: 0x200u` (`:342`). | **CHANGE** (rouge). Le promeneur s'arrête affleurant la caisse (140 − 10 − 10 = 120, même arithmétique que `:361` pour le mur) et l'obstacle est la caisse. La moitié « mur » (`:355-362`) est inchangée. Le contraste caisse/mur disparaît : à réécrire ou supprimer. |
| `…ProbeTests.TR1_SkippedCandidateFlags_LeaveTheSoulevablesOutOfTheSearch` (`:125-133`) | `:132` `Assert.Null(…, EntityFlags.PickupKindMask)`. `:131` (sans masque, trouve la caisse) reste vrai. | **Vert** si le paramètre est gardé ; ne **compile plus** s'il est retiré. Épingle une API non utilisée : à supprimer avec elle. |
| `AlundraEntityContactArcTests.TH4_TheArrivalOfTheChainOverlapsNoEntity` (`:476-486`) | `:485` même appel avec masque. `:484` (surcharge sans saut, **toutes** les entités) est déjà plus strict. | **Vert**. `:485` est redondante. `:484` est la garde qui protège les 11 arrivées de la chaîne (A20, A10, T-A19, day3-after-dream, A13, A14, A15, A10J, A17, 135→10, A18). |
| `AlundraDialogueOpcodesProductionTests` (`SailorThirteen_FullInteractionChain…`, miroir `:383-388`) | `:387-388` assigne `XCollisionEntity` avec le masque (miroir de la sonde de production, carte 389). | **Vert** (la 389 n'a aucun soulevable). Le miroir cesse de refléter la production : à aligner. |
| `AlundraShipCorridorArcTests` A5 / A5r (doc `:22-23` ; `:159-170`, `:186-230`) | Le doc dit « les caisses entre le héros et le portail ne bloquent pas ». Les arcs ne marchent pas à travers elles : `PlaceHero(540, 360, 64)` (`:166,212`). | **Inchangés** (lecture statique, B3). Le commentaire devient faux. Ils ne couvrent pas la barricade : c'est g4. |
| `AlundraEntityDestructionTests.TR2_BuildCollidables_…` (`:31-48`) | `:47` : un soulevable **est** dans la liste (« son exclusion appartient à la sonde »). | **Vert** (la liste ne change pas) ; commentaire périmé. |
| Doc de classe `AlundraMovementObstacleProbeTests` `:16`, commentaire `:335` | Citent D-E19-28. | Texte. |

Les 7 fichiers qui nomment « crate » (liste de `docs/plan-e20-annexe/e20-g1a/report.md:321`) ne dépendent **pas** de la traversée, sauf `AlundraMovementObstacleProbeTests`. Les raisons :
- Leurs boîtes « Crate » sont des entités `Collidable` sans bits de levage (`AlundraContactTestSupport.cs:160-200`).
- La série « crate » de `AlundraLogicalZStepTests` et `AlundraLogicalZWorldTests` est un oracle binaire de vol.
- `AlundraAbsoluteZWritesTests` teste l'empilement vertical, héros au repos à plus de 100 px.

Autres usages du drapeau `0x200` dans les tests : `AlundraEntityDestructionTests.cs:36`, `AlundraMovementObstacleProbeTests.cs:129,342` (`rg "0x[246]00u"`).

---

## B3. Arcs de l'histoire sur les cartes à soulevables

### B3.1 Recensement (FAIT : `census_liftables.out`, `lift_layout_tested_maps.out`)

Méthode :
- `Flags = MoreFlags | CanPickup<<8 | FlagsPortraitShadowType<<16`, lus dans `alundra-project/Data/sprite-records.json` par la clé `PrefabAssetId` de chaque enregistrement « Entities » des 483 `.tileMap`.
- Genre = `(Flags & 0x600) >> 9`.
- Position d'un record : `(XPos*12+12, YPos*8+8)`, Z = `Height*8` px (`EntityRecordMapper.cs:181,189,202`). Boîte `[pos+offset, pos+offset+taille)`.

Totaux : **9 741** enregistrements, **2 251** soulevables, **2 250** collisionnables, **242** cartes. 17 en-têtes portent un genre (`docs/plan-e20-heros.md:6320`).

Chaîne (`AlundraStoryChainOpcodeAudit.ChainMaps`, `Alundra.Tests/AlundraStoryChainOpcodeAudit.cs:39-43`, 30 cartes) : **18 cartes, 219 soulevables**, tous genre 1 et collisionnables. La liste de 12 cartes de `h-g-deps.md` §0.6 est fausse, comme le plan le dit déjà (`docs/plan-e20-heros.md:6340`).

Les 12 cartes de la chaîne sans soulevable sont 389, 391, 416, 476, 478, 162, 169, 10, 176, 183, 185 et 135. **La 389 n'en a aucun.**

| Carte | Soulevables (cruches + caisses) | Tests qui la chargent (ArcRun réel sauf mention) |
|---|---|---|
| 390 | 12 caisses | `AlundraShipArcTests` A0, A0b, A1 (**nus**, sans contrôleur donc sans sonde, `:32,90,133`) ; **A1c** (réel, gardé, `:201`) ; `AlundraCabinWalkTests` (cellules réelles, **sans** sonde) ; `AlundraAbsoluteZWritesTests` Map390 ; `AlundraLootRollWorldTests` m3/m6 et V8 paire 1 ; `AlundraPrefabArcSupportTests` `:186,203` (nus) ; `AlundraWarpArrivalTests` et `SaveGameDirectorKeysTests` (cellules et clefs) |
| 392 | 12 caisses | **A5, A5r** (gardés) ; `AlundraAnimationSoundTests.T1_Map392` (`:277-290`, première partie d'A5) ; V8 paire 2 |
| 163 | 4 + 4 | **A8** (gardé) ; AbsoluteZ Map163 |
| 165 | 2 + 6 | **A10** (gardé) ; TH4-A10 ; `AlundraPrefabArcSupportTests` TH3b (`:155-182`, arrivée, 10 images) ; V8 paire 12 |
| 164 | 8 + 9 | **A11** (non gardé) ; `AlundraEventProgramRunnerTests.cs:4373` (octets seulement) |
| 172 | 2 + 6 | **A9** (gardé) |
| 178 | 8 + 9 | A18 (`AlundraDay3SceneArcTests.cs:345-…`) ; TH4-A18 ; `AlundraAnimationSoundTests.T1_Map178` ; V8 paire 18 |
| 179 | 2 + 6 | A12 (`AlundraBergusJumpArcTests`) ; A14 (`AlundraDay3SceneArcTests.cs:112`) ; TH4 day3-after-dream et A14 ; UJ-7 (`AlundraAnimationImpulseSpawnTests.cs:224`) ; `AlundraAnimationSoundTests` `:323,356` ; S6c (`AlundraHeroFallAndPadJumpTests.cs:679`) ; `AlundraHeroLifeArcTests` Inoa179 ; autotests de garde (`AlundraHeroSlideTests.cs:352-375`, `AlundraArcGuardAndSoundHostTests.cs:19-60`) ; AbsoluteZ Map179 ; V8 paire 14 |
| 170, 171, 173, 174, 175, 177, 180, 181, 182, 184 | 8, 17, 6, 23, 15, 8, 6, 23, 15, 8 | **Aucun test** (grep des noms de dossier et de `MapIndex`) ; seulement des tests statiques d'opcodes (`AlundraStoryChainSkippedOpcodesTests`) |

Les cartes 163/170/177/184, 164/171/178, 165/172/179, 173/180, 174/181 et 175/182 sont des familles à comptes identiques jour à jour.

### B3.2 Chemin du héros ou de l'acteur contre les empreintes (FAIT : `segment_check.out`)

Boîte du héros : 21×15×32, offsets −10, −7, 0 (`AlundraDialogueOpcodesProductionTests.cs:350-351`). Écart XY minimal entre le rectangle balayé et une empreinte (0 = recouvrement) :

| Arc | Chemin (source) | Écart | Verdict |
|---|---|---|---|
| A5 partie 1 (392) | `0x64 @50` (720, 216) puis `0x1F @68` 48 px est (`events_392.out`) | 63 px | ne croise pas |
| A5r roulade (392) | (775, 216) → (770, 249) (`AlundraShipCorridorArcTests.cs:121-122,233`) | 113 px | ne croise pas |
| A5/A5r final (392) | `PlaceHero(540, 360, 64)` puis Bas jusqu'au portail (22, 23) | 57 px (rec9) | ne croise pas. C'est un **téléport** qui contourne la barricade (D-E19-23, `docs/plan-e19-opcodes.md:105-106`). |
| A1c (390) | cabine : 80 N, 48 O, 80 N, 32 O depuis (1068, 376) | 67 px | ne croise pas |
| A8 (163) | héros (960, 152) 64 px à l'ouest ; Jess (924, 136) → mur y 297 | 529 px / 384 px | ne croise pas |
| A9 (172) | héros immobile en (876, 296) | 27 px | aucun pas |
| A10 (165) | arrivée (300, 360) ; B[2] : 52 N, 118 E, 102 N (`events_165.out`) ; rec5 (300, 428) 96 N, 120 E, 78 N | 147 à 193 px | ne croise pas |
| A11 (164) | `PlaceHero(996, 120, 16)` ; Septimus (1092, 120) S, puis O, puis E 24 | 120-129 px | ne croise pas |
| A12/A14 (179) | arrivée (420, 120) puis 212 px au sud | 267 px | ne croise pas |
| A18 (178) | arrivée (924, 408) puis `PlaceHero(948, 232, 16)` ; Septimus (972, 344) O 24, O 22, E 48, S 160 | 27 px (rectangle englobant d'un téléport), 65 px | ne croise pas |
| T-C61 (61, hors chaîne) | (588, 648) → (252, 792) | 195 px | ne croise pas |

Les 27 px d'A9 et d'A18 viennent de positions immobiles ou de téléports, pas de marches.

Arcs gardés par `ArcsWithoutEntityContact` (`AlundraArcSupport.cs:332`) sur des cartes à soulevables : **A1c, A5, A5r, A8, A9, A10**. Les autres noms de la liste (A3, A4p, A6, A7, A10J, A20) tournent sur des cartes sans soulevable (478, 476, 391, 416, 10, 162).

Arrivées et placements (FAIT) :
- L'arrivée se calcule au centre de la case (portail 162.3 → carte 165 case (12, 22) = (300, 360), `AlundraInoaDayOneArcTests.cs:104-107`).
- 3 316 portails dans l'export ; 1 696 mènent à une carte à soulevables. Pour les **71 portails qui mènent à une carte de chaîne à soulevables : 0 recouvrement XY** (écart minimal 27 à 33 px ; `portal_arrivals.out`).
- 331 `0x53` : **0** recouvrement (`warp53_arrivals.out`).
- Le Z réel après `ClampToGround` n'est pas modélisé.

Marches de PNJ scriptés :
- INFÉRENCE : l'original a des caisses solides, donc ses scripts les évitent. Ne reste que l'écart du modèle de cellules de la DLL, indécidable en lecture seule.
- **À mesurer** : les acteurs d'A10 (Meade rec1, Nestus rec3, rec5 ; `walkers.out`) et le Septimus de la 178, bien qu'aucun chemin décodé ne passe à moins de 65 px d'une empreinte.
- Les marches de `B[1]` de la 179 ne sont pas exercées par un arc.

Harnais de traces : la **389 n'a aucun soulevable**. `IntroTraceHarnessTests`, `HeroTraceHarnessTests`, `Map389LoadProgramsTests` et les traces `docs/intro-trace-389.txt`, `docs/hero-trace-389-*.txt` sont hors sujet. Le harnais d'intro simule ses cinématiques sans contrôleur (`EntitySupport.cs:11-20`).

### B3.3 Cartes hors chaîne à soulevables chargées par des tests

| Carte | Soulevables | Tests | Verdict |
|---|---|---|---|
| 115 | 13 caisses | arcs de levier A115 ; `AlundraMurutaHallArcTests` H115 ; `AlundraHeightTargetSitesTests` `s115` | Muruta à z 128 contre caisses z 64-80 : pas de recouvrement Z, héros téléporté. Statues poussables rec0/rec1 à 33-51 px de caisses : **à mesurer** (`walkers_noncahin.out`). |
| 116 | 9 | B116 ; `AlundraBeannoideGrateArcTests` G116 | Le Beannoïde rec0 en (396, 296, z 160) est à 17 px au sud de la caisse rec9 (396, 264, z 160) : **à mesurer**. |
| 117 | 2 | C117, D117 | pas de marche vers elles |
| 69 | 7 | `AlundraFlailBounceArcTests` | héros fixe |
| 61 | 5 | T-C61 | 195 px |
| 83 | 16 | `AlundraAbsoluteZWritesTests` plate-forme | Z seulement |
| 260 | 8 | `AlundraControllerlessVerticalStepTests` | apparition d'un record |
| 127, 89 | 2, 9 | `AlundraHeightTargetSitesTests` | billes, Z seulement |
| 14 (arène des tests E20) | **0** | — | — |

---

## B4. Convertisseur et données exportées

- **Aucun code du convertisseur ne porte la déviation.** Le champ `CanPickup` est exporté tel quel (`Readers/SpriteBankReader.cs:159,519` → `Writers/SpriteWriter.cs:1183,1299`, `sprite-records.json`). Il est épinglé en passage direct par `SpriteWriterSpriteRecordsTests.cs:25,44` (valeur 17).
- Les corps d'entité sont des fantômes `Kinetic` à profil vide, résolu en `Pawn` (`SpriteWriter.cs:336-339,396`). Chaque prefab à boîte de corps reçoit un `CharacterControllerComponent` (`SpriteWriter.cs:453-495`), sauf la banque dégénérée 244 (jamais bloquée).
- La prefab exportée `alundra-project/Entities/Caisse en bois générique/Caisse en bois générique.entity` confirme ces réglages : `CollisionComponent` « Kinetic » et `CharacterControllerComponent` profil `Pawn`.
- Les drapeaux nécessaires sont déjà exportés : `MoreFlags` (bit 0x80), `CanPickup` (bits 9-10), `BreakEffect` (10 pour la caisse), `Contents`.
- Conclusion (INFÉRENCE) : retirer le masque n'exige ni modification du convertisseur ni nouvel export. L'export ne contient aucune trace de la déviation à retirer.

---

## Liste prédite des tests qui bougeraient (non exécutée)

**Rouge à coup sûr si le masque est retiré (1)**
- `AlundraMovementObstacleProbeTests.TRLift_ACrateOnThePathDoesNotBlock_AnIronBallWallDoes` (`:352-353`).

**Verts mais à retoucher ou sans objet (6)**
- `…ProbeTests.TR1_SkippedCandidateFlags_LeaveTheSoulevablesOutOfTheSearch` (`:125-133`) : API non utilisée ; la compilation casse si le paramètre disparaît.
- `AlundraEntityContactArcTests.TH4_…` (`:485`) : redondante.
- `AlundraDialogueOpcodesProductionTests` miroir (`:383-388`) : à aligner.
- `AlundraShipCorridorArcTests` A5/A5r (doc `:22-23`) : verts, texte faux ; à refaire avec g4.
- `AlundraEntityDestructionTests.TR2` (`:45-47`) et doc de `AlundraMovementObstacleProbeTests` (`:16,335`) : texte.

**À mesurer (la lecture statique ne tranche pas)**
- Marches de PNJ scriptés sur les cartes à soulevables : A10 (165), A11 (164), A12/A14 (179), A18 (178). Probabilité faible (écarts ≥ 65 px).
- Gardes à surveiller :
  - `ArcsWithoutEntityContact` (A1c, A5, A5r, A8, A9, A10).
  - `ArcsThatSlide` (A14 = 2, carte 179, chemin à 267 px).
- Hors chaîne : `AlundraBeannoideGrateArcTests` G116, `AlundraMurutaHallArcTests` H115, arcs de levier 115 (statues poussables).
- Les 10 cartes de chaîne à soulevables sans test (170, 171, 173-175, 177, 180-182, 184). Toute recette de g3 doit les couvrir ; les arrivées n'y sont vérifiées qu'en statique.

**Inchangés (preuve statique)**
- Arcs : A1c, A5, A5r, A8, A9, A10, A11, A12, A14, A18, TH4 (11 arrivées), UJ-7, S6c.
- Tests de son et de vie : `AlundraAnimationSoundTests` (392/178/179), `AlundraHeroLifeArcTests`.
- Garde et chargement : autotests de garde, `AlundraAbsoluteZWritesTests`, V8 (`AlundraLootRollWorldTests`), `AlundraCabinWalkTests`.
- Carte 389 : tous ses tests, traces comprises.
- Tests à boîtes synthétiques « Crate/Chest/Tomb/Roof/Box » (aucun bit de levage).

---

## Inconnus

1. Adaptateur Bepu : que `ShapeSweep` ignore les fantômes d'entités n'est pas relu (affirmé par les commentaires du dépôt, B1.2).
2. Z réel des records après le calage au sol : mes recouvrements Z utilisent `Height*8` (approximatif). Pour les arrivées, le Z n'est pas modélisé.
3. Marches de PNJ : directions lues par convention (0 sud, 8 ouest, 16 nord, 24 est), déduite de `AlundraInoaDayOneArcTests.cs:326-331`. Balayage linéaire des programmes : branches mortes possibles.
4. Dix des 18 cartes de chaîne à soulevables n'ont aucun test. Les recettes en jeu de la barricade 392 (g4) et des familles 170-184 restent à faire.
5. Hors chaîne et non testés, Z non décidé :
   - 3 arrivées de portail recouvrent un soulevable en XY : 397.0 et 380.0 → carte 3, case-centre (204, 552), « Pierre très lourde » rec12 ; 128.35 → carte 125 (`portal_arrivals.out`).
   - 2 apparitions se chevauchent : cartes 70 et 429.
6. Pas de build ni de test lancé : toutes les prédictions de comportement sont des déductions.
7. `h-g-deps.md` (12 cartes) est périmé ; le plan donne 18 cartes / 219 enregistrements, ce que mon recensement confirme.