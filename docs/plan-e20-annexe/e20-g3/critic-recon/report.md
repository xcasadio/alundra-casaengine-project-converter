# Critique de réconciliation binaire / DLL pour E20.g3 et g4 (lecture seule)

HEAD est `600b938c`. `git diff --stat c84fd72 HEAD -- Alundra CasaEngineMonogame` est vide : le code est celui que le rapport DLL a lu. Je n'ai rien écrit dans le dépôt, les sous-modules, `alundra-project/` ni `data-extracted/`. Le `git status` est inchangé (` m CasaEngineMonogame`, ` m alundra-datas-analyser`, présents avant mon passage). Le SHA-1 du binaire, recalculé, vaut `a705f8da…`.

Mon dossier est `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-e20g3/critic-recon/`. Il contient :
- `c1_oblique.py` (SHA-1 `b60c1773…`), lancé avec `python -B -I` ;
- sa sortie `out/c1_oblique.txt` (`232864dc…`) ;
- des copies non modifiées de `lib392.py` et de `g1a/`, l'oracle MIPS du rapport binaire.

Aucun projet .NET n'a été construit ni testé : tout fait côté DLL vient de la lecture du code. Je n'ai pas écrit `report.md`, la consigne de l'environnement interdit aux sous-agents d'écrire un fichier rapport. Ce message est le rapport.

## Tableau règle par règle

| # | Règle du binaire | DLL à HEAD | Verdict |
|---|---|---|---|
| 1a | Liste : `flags & 0x80` | `EntitySupport.cs:74`, `Collidable = 0x80` (`EntityFlags.cs:92`) | même |
| 1b | Liste : octet `+0xB4 & 0x80`. La liste est bâtie avant l'horloge du tick, la porte du sujet est testée après | `AnimFlags` (`EntitySupport.cs:75`), écrit seulement en fin d'image (`FrameSyncPasses.cs:154-161`) | **différent** (retard d'un tick ou plus) |
| 1c | Liste : `+0x28 == 0` (non porté) | `PlatformEntity //28` (`EntityScriptProxy.cs:101`). Seule écriture : la copie en `:2609`. Jamais affecté en production | même champ, toujours nul aujourd'hui |
| 1d | Aucun test de statut | `Status != FlagToDestroy` (`EntitySupport.cs:55`) | **différent**, un tick |
| 1e | Ordre de la liste = ordre des slots | slots insérés dans l'ordre, plus bas slot libre | même |
| 2a | `+0x130` : premier de la liste au point testé | premier de la liste au point testé, porté par l'obstacle du dernier essai bloqué (`H2 ?? H1`) | même en cardinal ; écart en oblique |
| 2b | Lu par le `MovePlayer` du tick suivant | même ordre d'exécution | même |
| 3 | Animations 32 à 36 et 39 du héros : bit `0x80` | même octet dans l'export | même contenu ; décalage de 1b |
| 4a | L'entité l'emporte sur le terrain ; pas de glissement après un contact d'entité | sonde avant champ ; glissement interdit si contact | même |
| 4b | Division du pas conjointe sur X et Y | bissection axe par axe | cardinal : même ; **oblique : différent** (mesuré côté binaire) |
| 4c | Calage à y = 295 et glissement de 0,75 px | table portée, boîte et bords identiques | même (code lu, non exécuté) |
| 5 | La caisse du dessus tombe de 16 px en 8 ticks | ancienne règle des entités posées par la carte | **inconnu** |

## Confirmé

**1a, 1c, 1e : la liste.**
- `IsEligibleSubject` (`EntitySupport.cs:73-76`) teste Collidable 0x80, `AnimFlags & 0x80` et `PlatformEntity == null`. `PlatformEntity` est bien le `+0x28` (`objet.+0x28 = héros` au `TryLift`). Une recherche ne trouve aucune affectation hors du clone.
- Ordre de la liste :
  - `_spawnedEntities` est tenu dans l'ordre des slots (`InsertBySlot`, `WorldProxy.cs:216-232`) ;
  - le slot pris est le plus bas libre, de 1 à 63 (`EntitySlotTable.cs:28-41`) ;
  - au chargement, les enregistrements sont parcourus dans l'ordre de la couche (`WorldProxy.cs:1045`, `:1074`) ;
  - dans le `.tileMap` de la 392, `object_layers[2]` va de `Entity_0` à `Entity_11` dans l'ordre des enregistrements (sortie de `jq`).
- Donc rec5 passe avant rec6, et rec6 avant rec10.

**2a, 2b : le contact.**
- La sonde rend le premier candidat de la liste qui recouvre la boîte testée (`EntityCollision.cs:50-80`). `XCollisionEntity` en vient (`EntityScriptProxy.cs:2424-2431`).
- Dans le tick (`WorldProxy.cs:2927-2979`), `MovePlayer` passe avant la physique. Le passage des compteurs ne touche pas le champ.
- Avec une force nulle, `Move` remet le contact à zéro (`CharacterControllerComponent.cs:443-450`). Le binaire renvoie 0 dans ce cas (`0x80037DC0`-`0x80037DC8`).
- Conséquence : Carré au premier tick bloqué donne l'épée, et le soulèvement vient au tick suivant, comme dans le binaire (bin A2.2).
- Pile vue du sol : c'est la caisse du bas qui est le contact.
- Héros debout sur rec5 :
  - il repose à 80,0 (`ScriptedMotion.cs:337-347`) ; le binaire le pose 1 unité plus haut, décalage cohérent avec la convention de la DLL ;
  - rec6 ne recouvre pas ses pieds (bord semi-ouvert, `EntityCollision.cs:74-78`) ; le contact vers le sud est donc rec10, comme R7 du rapport binaire.

**3 : les animations du héros.** L'en-tête du héros (`alundra_0`, clé `192c2eeb…`, résolue par `AssetInfos.json` → `Entities\Alundra\Alundra.entity`) porte `Acceleration` (l'octet 0xD entier) à 208, 208, 212, 209, 208, 208 pour les animations 32, 33, 34, 35, 36 et 39. C'est 0xD0, 0xD0, 0xD4, 0xD1, 0xD0, 0xD0, les octets du binaire. Aucun des 17 en-têtes soulevables n'a le bit 0x80 dans ses animations.

**4a : entité avant terrain.**
- Binaire : test d'entité d'abord ; le test de terrain (`jalr` en `0x80037928`) n'a lieu que si aucune entité ne bloque ; si la dernière entité testée est non nulle, pas de glissement (`bnez s4` en `0x800379D0`).
- DLL : `IsCandidateBlocked` interroge la sonde avant le champ (`CharacterControllerComponent.cs:1319`) ; le glissement est interdit si `XCollisionEntity != null` (`ScriptedMotion.cs:819`).

**4c : le calage au mur nord de la porte.**
- La boîte du héros dans l'export fait 21 × 15, posée à (0,5 ; 0,5) de la racine (`Alundra.entity:513-524`). C'est la boîte du binaire (-10, -7, 21 × 15).
- Les coins lointains sont pris avec `BitDecrement`, donc bord semi-ouvert (`CharacterControllerComponent.cs:976-979`). La bissection garde le côté libre, donc y ≥ 295,0.
- La table du glissement nord est portée : `c0 && !c1` donne `+0xC000` (`ScriptedMotion.cs:871-873`), comme le binaire qui écrit `+0xE4 = 0xC000` (`0x80037A6C`-`0x80037A74`).

**Nouveau fait binaire (`out/c1_oblique.txt`, cas C).** Héros collé à la colonne en x = 657, Haut tenu :
- il monte jusqu'à y = 295,0 (tick 11) ;
- à partir du tick 12 : `adj = 1` et `xcoll = rec5` ;
- le glissement du mur part vers l'ouest et c'est rec5 qui le refuse.

Côté DLL, par le code : le premier pas est bloqué par le champ, donc pas de contact ; ensuite `SlideAlongWall` demande −0,75 px en X, la sonde le refuse sur rec5, et `XCollisionEntity` vaut rec5. Même résultat : Carré soulève rec5 depuis y = 295, donc dans la fenêtre.

## Réfuté ou corrigé

1. Le commentaire `EntitySupport.cs:53-54` (D-E19-29) affirme que la liste du binaire n'a pas d'entité marquée pour destruction. C'est faux (bin A1.7).
   - Retirer le filtre reproduit exactement le binaire, car le recyclage de la DLL tombe au même endroit : en tête du tick suivant, après les événements de carte (`WorldProxy.cs:2572`, `:3265-3289`).
   - Ce retrait fait échouer `AlundraEntityDestructionTests.cs:42-43`. Le rapport DLL (B2) prédit « vert » sans condition : ce n'est vrai que si le filtre est gardé.
2. Le rapport DLL (B1.4.3) dit « rien d'autre n'encode la déviation, aucun changement moteur ». C'est vrai pour le masque seulement. À HEAD, la liste ne suit pas tout A1.2 (1b, 1c, 1d) : le « retirer le masque suffit, si la liste suit A1.2 » du rapport binaire (g3, point 1) ne tient donc pas à HEAD.
3. Textes périmés :
   - `MovementObstacleProbe.cs:27` dit la liste « rebuilt … at the end of each frame » : elle est rebâtie à chaque tick dans `RunEntityBlock` (`WorldProxy.cs:2963-2966`) ;
   - `WorldProxy.cs:2994-2995` dit « recomputes the carrier once per frame » : c'est aussi par tick (`UpdateRidingEntities`, `:3699`) ;
   - `EntitySupport.cs:36` et `:68-70` disent « always null », ce qui deviendra faux avec g1.
4. Le rapport binaire (g4, point 2) laissait inconnu le comportement de la DLL au mur nord. Le code reproduit ce calage (point 4c ci-dessus).

## Manques (aucun des deux rapports ne les traite)

**M1. Poussée en diagonale contre une caisse (FACT binaire, `c1_oblique.txt`).**
- Cas A : Gauche + Haut depuis (664, 314). Le héros s'arrête à (657,0000 ; 309,3333) au tick 5 et reste figé jusqu'au tick 40, avec `adj = 1` et `xcoll = rec5`.
- Cas B : Gauche + Bas depuis (664, 300). Il reste figé à (657 ; 304,6666).
- Mécanisme : X et Y bougent ensemble (`0x800377F4`-`0x80037814`) et sont divisés ensemble (`0x80037944`-`0x80037960`). En oblique, la boucle s'arrête dès qu'un axe tombe à 0 (`0x8003798C`-`0x80037998`). Pas de glissement après une entité (`0x800379D0`).
- DLL : le contrôleur traite les axes l'un après l'autre (`CharacterControllerComponent.cs:1170-1197`). X s'arrête au contact, Y continue : le héros devrait longer la colonne jusqu'à y = 295. C'est une INFERENCE tirée du code, non exécutée.
- De même, le cas n°7 du rapport binaire (bloqué au seuil en (694,5 ; 295)) devrait devenir, dans la DLL, une glissade vers l'ouest le long du mur jusqu'à rec5.
- C'est l'écart déjà accepté par O-E19-28 et D-E19-58 (`docs/plan-e19-opcodes.md:12421`, `:204`). Mais g3 le rend visible sur 2 250 placements.

**M2. Décalage de l'octet d'animation (1b).**
- Le binaire lit `+0xB4` deux fois par tick : la liste avant l'horloge (valeur du tick précédent), la porte du sujet après (valeur du tick).
- La DLL lit `AnimFlags`, écrit en fin d'image. La porte du sujet a un tick de retard. Dans une image à 2 ou 3 ticks, la liste aussi garde l'ancienne valeur jusqu'à la fin de l'image.
- L'équivalent exact existe déjà : `HitAnimByte` (`EntityScriptProxy.HitPass.cs:29-30`, `:106-108`), rechargé à l'horloge (`FrameSyncPasses.cs:290`). Avec l'ordre listes → horloge → physique de la DLL, l'utiliser donne les deux moments du binaire.
- Tests qui épinglent `AnimFlags` : `AlundraEntityDestructionTests` TR6 (`:205-245`), `AlundraEntityCollisionTests.cs:85-99`, `AlundraMovementObstacleProbeTests.cs:118`, `EntitySearchServiceTests.cs:134`.
- Portée : 14 en-têtes ont un octet 0x80. Les collidables parmi eux sont le héros, 67, 68, 69, 70, 105 et `alundra_101`.

**M3. `PlatformEntity` n'est jamais affecté.** Sans cette affectation dans g1a/g1b, la caisse portée reste dans la liste. Elle bloquerait les PNJ et serait elle-même sujet. Par le code (`EntitySupport.cs:213-252`, INFERENCE), son dessous, à 1 unité au-dessus du haut du héros, ferait plafond dès le premier tick de montée : le héros ne pourrait plus sauter en portant.

**M4. Chute d'une caisse de la carte (5).**
- Si g1b écrit `PlatformEntity` dans `MovePlayer`, rec6 sort de la liste le tick même, et rec10 perd son appui dans la même physique (INFERENCE).
- La loi de force est celle du binaire (`EntityScriptProxy.cs:1053-1078`).
- Mais les entités posées par la carte gardent l'ancienne règle (D-E20-37, `docs/plan-e20-heros.md:169-170`) :
  - le moteur accroche au sol à 4 px (`ground_snap_distance` 4.0, `Caisse en bois générique.entity:219` ; `CharacterControllerComponent.cs:1528-1546`) ;
  - la remise à zéro sur `IsGrounded` (`EntityScriptProxy.cs:928`) peut jouer dans les 4 derniers pixels.
- La fin des 8 ticks n'est pas décidable en lecture seule.

**M5. Montée de marche.** Avec la gravité, `+0xB8 == 0` et une marche de moins de 3 px, le binaire fait le test d'entité à Z = T + 1 (`0x80037848`-`0x800378CC`). La sonde de la DLL teste au `PosZ` courant (`MovementObstacleProbe.cs:50-51`). Écart seulement pour une caisse posée sur une marche de 1 à 3 px (INFERENCE : rare).

## Inconnus

1. Aucun comportement de la DLL n'a été exécuté. Les verdicts « même » côté DLL sont des lectures de code.
2. Que le balayage Bepu ignore les corps fantômes des entités (inconnu repris du rapport DLL). La déviation actuelle, visible en jeu, le suggère : le héros traverse les caisses (`docs/plan-e20-heros.md:6364`). INFERENCE.
3. La chute de rec10 dans la DLL (M4).
4. Sémantique du contact : le binaire renvoie le candidat de l'avant-dernier essai (stocké en `0x800377CC`, rendu en `0x80037DFC`) ; la DLL renvoie l'obstacle du dernier essai bloqué. Effet inconnu sur un soulèvement depuis les états `0x2B`-`0x2D`, en l'air.
5. L'exception de transition de la porte du sujet (`0x80036F3C`-`0x80036F64`) n'est pas portée. Atteignable ou non dans la DLL : inconnu.
6. Les états `0x20`-`0x24` et `0x27` du héros : aucune constante dans `Alundra/`. Je ne sais pas si la DLL les atteint.

## Conséquences pour les tranches g3 et g4

**g3, au-delà du retrait du masque (`MovementObstacleProbe.cs:51`) :**
1. Prérequis g1a/g1b : écrire `PlatformEntity` au `TryLift` et l'effacer à la libération. Acceptation proposée : la caisse levée n'est plus dans `Collidables` dès le tick du soulèvement, et le héros qui porte peut sauter.
2. Choisir entre `HitAnimByte` dans `BuildCollidables` et `IsEligibleSubject` (fidèle au tick, 4 fichiers de tests à revoir) et un écart consigné.
3. Choisir entre retirer le filtre `FlagToDestroy` (fidèle, TR2 `:42-43` à revoir) et consigner l'écart d'un tick.
4. Corriger les textes périmés listés plus haut, et ceux déjà relevés par le rapport DLL.
5. Rien à changer pour : l'ordre de la liste, la persistance du contact, l'entité avant le terrain, l'interdiction de glisser après une entité, le contact affleurant en cardinal (657,0 est exact en flottant).
6. L'ADR de g3 doit dire que l'écart oblique de D-E19-58 s'applique désormais aux caisses et aux jarres. C'est à l'auteur de décider s'il rouvre la question.

**g4 (barricade de la 392) :**
1. Les critères scriptés en directions cardinales reproduisent le binaire. Critère à ajouter, mesuré : Haut le long de la face en x = 657 donne y = 295 et `xcoll = rec5`, et Carré soulève rec5 (`c1` cas C).
2. Le critère (b) (« à y = 298 le saut échoue ») doit être scripté en cardinal. En diagonale, la DLL devrait amener le héros à y = 295, alors que le binaire reste figé à (657 ; 309,333). La DLL serait donc plus permissive que l'original.
3. Le critère (c) (rec10 tombe en 8 ticks) est à mesurer. S'il doit être exact, il faut probablement le marqueur de Z logique sur la caisse qui tombe, ou E19.h2. Sinon, consigner l'écart.
4. Le calage à y = 295 est le même mécanisme que le binaire selon le code. Un arc DLL doit le confirmer, à cause du contact au flottant (quelques unités 16.16, toujours du côté libre, donc dans la fenêtre).