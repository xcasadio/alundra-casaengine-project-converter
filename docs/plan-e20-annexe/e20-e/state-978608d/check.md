# Contre-vérification de `e/state.md` (lot E20.e, CATE `0x8003A7B0`, citations au HEAD `978608d`)

Lecture seule. Je n'ai rien écrit dans le dépôt ni dans les sous-modules, et je n'ai rien compilé ni lancé de .NET. Mes scripts sont à côté de ce fichier : `v_show.sh` (ligne ancienne et ligne HEAD côte à côte), `v_pure.py` et `v_puret.py` (contrôle de pureté), `v_census.py`, `v_m14.py`, `v_map.py`.

Méthode :
- J'ai rouvert les 60 lignes du tableau de citations, chacune à `9502f75` puis au HEAD.
- J'ai relu les 25 verdicts A/M.
- J'ai refait la recherche des pins (`rg StubCallsForTests|GrassCut|CheckAndTriggerTileEffect|GrassCallTicks|wave_grass|E20.e`).
- J'ai refait le contrôle de pureté sur les 21 fichiers DLL cités et les 5 fichiers de test cités.
- J'ai recalculé le recensement des cases coupables et les boîtes d'épée à partir des `.anim2d` exportés.

## Erreurs

1. **M1b n'est pas un changement survenu au HEAD : c'est une erreur du rapport dès `9502f75`.** La signature était déjà `AlundraNativeSpriteHandler(AlundraEntityScriptProxy entity, IEntityWorldContext context, ...)` (`9502f75:Alundra/Scripts/AlundraNativeSpriteRegistry.cs:13`), appelée avec `_worldContext` (`9502f75:Alundra/Scripts/AlundraEventProgramRunner.cs:355`). La phrase « vrai seulement pour la voie du héros » va trop loin. Seule la signature de `PlayerAnimationEffects.Create` (`Alundra/Hero/PlayerAnimationEffects.cs:56`) ne donne que l'`IScriptHost`. En production, `ScriptHost` est le `WorldProxy` (`Alundra/Maps/WorldProxy.cs:2107`), qui implémente les deux interfaces (`WorldProxy.cs:82`). Le héros atteint donc le magasin par un test de type, comme le fait `PlayerManager.cs:1216`. Quand le runner n'a pas de contexte, un natif reçoit `NoOpEntityWorldContext.Instance` (`Alundra/Events/EventProgramRunner.cs:147`), dont `CellMutator` vaut nul (`IEntityWorldContext.cs:116`). Ce point est à verser à U3.
2. **M2, preuve fausse à la lettre.** La preuve dit « aucun `TileEffect` dans `Alundra/` », mais `rg -c TileEffect Alundra/` donne 5 occurrences dans `Alundra/Ai/Projectiles/WaveSwordAi.cs` (le nom du bouchon `CheckAndTriggerTileEffect`). Il faut écrire : aucun port, hors le nom du bouchon.
3. **M1, conséquence présentée comme un fait.** « La coupe doit donc être une opération distincte, pas un mode de `ClearCellBits` » ne découle pas du test cité. `SetThenClearCellBits_NeverTriggersOverlayReconstruction` (`Alundra.Tests/AlundraCellVisualSyncTests.cs:619-636`) n'épingle qu'un Set puis un Clear de masques `0x10/0x20` sur (18, 15), sans reconstruction. Il n'interdit pas une variante de coupe. C'est une INFÉRENCE de conception.
4. **§0, liste des résidus de `AlundraWorldProxy` incomplète.** Il n'y a pas que des qualifications de `cref`. Dix lignes de code passent de `DialogueDirector.Instance` à `Dialogues.DialogueDirector.Instance` (sortie de `v_pure.py`). C'est neutre pour le comportement, et la phrase « aucune ligne de logique n'a changé hors R0 » reste vraie.
5. **§3.3, phrase fausse sur le banc.** « Champ `FlatCells.Create()` … marchabilité 0 … Aucune case coupable » est faux comme généralité. `FlatCells.Create` prend une fonction de case (`Alundra.Tests/AlundraJumpTestSupport.cs:457`). Voir l'omission 2.
6. **A19 et §3.5, imprécisions de nommage.**
   - `Alundra/Ai/` contient aussi `EnemyHelpers.cs` et `NativeSpriteRegistry.cs`. La liste des gestionnaires (Beannoïde, Muruta, WaveSword, SaveBook) reste juste.
   - `AlundraEnemyHandlerOracle.cs:14` n'est pas une exclusion documentée : c'est la doc de l'enregistrement `HandlerJournalEntry`. La seule exclusion écrite est `AlundraBeannoideOracleTests.cs:27`.
7. **Bornes de plages à peine décalées, sans effet sur le sens.**
   - Ligne 2 : `EffectManager.cs:443-490` dépasse la fin du fichier (`wc -l` donne 485), et ce défaut existe dès le rapport.
   - Ligne 16 : `FrameBoxTimeline.cs:14` est une doc. La lecture des `collision_keyframes` est à `:76-90`.
   - Ligne 27 et A10, M6 : le corps de `FlushPendingOverlayReconstruction` va de 212 à 235, pas jusqu'à 237.
   - M1 : `ApplyCellBits` va de 314 à 338, pas jusqu'à 340.
8. **§1.3, titre trompeur.** Les lignes 56 à 60 citent la partie « État des lieux » du plan §1.25 (`docs/plan-e20-heros.md:6622-6635`, tirée de `e20-themes`). Ce n'est pas le résumé de la lecture e20-e (`:6641-6649`), qui ne cite aucun `fichier:ligne` de DLL. Seule la ligne 55 (`:6628`) porte sur CATE.

## Omissions

1. **Un test risque de casser au câblage de CATE : `AlundraHeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle`** (`Alundra.Tests/AlundraHeroAttackTests.cs:441`).
   - **Le décor.** Le test se joue sur la vraie carte 10 (`"Overworld 2,1-10"`, `:418`, 133 cases coupables d'après `v_census.py`). L'arrivée est `ArcArrival(16515072, 61341696, 0, …, 16)` (`:419`), soit le pixel (252, 936, z 0), case (10, 58), face au nord. 16 vaut le haut d'après `Alundra/Entities/AnimationTables.cs:59-61`.
   - **La boîte.** Le héros frappe à l'épée de base (état `0x10`, qui est dans les 15 états). La boîte de `bankalundra_0_anim16_up.anim2d` au temps 0 vaut `w23 h19 l15` et `lp(-12.5,-15.5,7.5)`. Avec la règle `local - size/2` de `Alundra/Combat/FrameBoxTimeline.cs:89-90` et `Size = (w,h,l)` (`CasaEngineMonogame/CasaEngine/Engine/Geometry/Box.cs:76-80`), ses coins tombent en (9,56), (10,56), (9,58) et (10,58).
   - **La case touchée.** La case (10, 56) est coupable, de hauteur 0 (`v_map.py 10 4 16 52 59`). La bande de Z est satisfaite : `zc` vaut 8 px et la boîte va de z 0 à 15 px.
   - **L'assertion qui casserait.** Une fois CATE porté, la coupe demanderait le son 31, et peut-être 84. L'assertion `Assert.Equal(new[] { 29 }, sounds.Requests.ToArray()); // nothing else asked during the swing.` (`:465`) échouerait.
   - INFÉRENCE : je suppose que la position d'arrivée ne bouge pas avant la frappe (le test ne l'affirme pas) et je n'ai rien exécuté. Ce test manque au §3.2 (seul `:157` y figure) comme au §3.3.
2. **Une case coupable est posée sous une boîte dans un banc synthétique : `AlundraFlailBounceTests.TheWallRule_IsTheHeightOrTheBit0x40`**, avec `[InlineData(0x3F, 0, false)]` (`Alundra.Tests/AlundraFlailBounceTests.cs:210`, `:214-224`).
   - **Le montage.** La marchabilité `0x3F` a le bit `0x02`. Elle est posée en (5, 5) d'un champ `FlatCells` de 52 x 60 (`:76`). La boîte posée (`:40-44`, `:66-74`) couvre les colonnes 4-5 et les rangées 5-6. L'état est `0x40`, qui est dans la liste, et le test appelle `Create(FreeMode)`.
   - **L'issue.** Il y a coupe si `CollisionHeight` (non posée par `PoseBox`, valeur INCONNUE) atteint `zc`. L'hôte est un `ContactHost : IScriptHost` sans `CellMutator`. L'issue dépend donc de la règle U3. L'assertion (pas un mur) ne dépend que de `0x40` et de la hauteur, donc elle ne devrait pas changer (INFÉRENCE).
3. **La citation `report.md` l.303 n'est pas traduite.** Elle porte `AlundraWorldProxy.cs:3269` (e0c-T3), soit `Alundra/Maps/WorldProxy.cs:3303`. C'est la même cible que la ligne 44.
4. **U7 peut être en grande partie levé par le calcul.** J'ai repris le test `AlundraHitPassTests.cs:348`, joué à (172, 424, z 80) face à l'ouest, avec les quatre images-clés de `bankalundra_0_anim16_left.anim2d`. Les coins touchent les cases (5..7, 25..27) uniquement. Aucune n'est coupable : la carte 14 n'en a qu'en (3,25), (4,25), (3,26) et (7,24) dans ce voisinage (`v_m14.py`). Il n'y a donc pas de coupe (INFÉRENCE : héros immobile, animation 16).
   - Pour la vague, l'oracle donne une portée inférieure à 60 px au sud depuis (212, 96) (`docs/plan-e20-annexe/e20-f3/README.md:55-56`), donc elle reste dans les rangées 0 à 9.
   - Les cases coupables de la carte 14 proches des colonnes 4 à 15 ont une hauteur de 5 ou 9, soit `zc` ≥ 88 px. C'est un argument de plus que le rapport ne donne pas.
5. **Le chiffre M4 « 18 342 tuiles » n'est que partiellement reproduit.** Je retrouve 9 089 cases et 8 989 cases avec pile, mais un total brut de **18 361** tuiles de pile. Le rapport parle de tuiles « visibles », règle que je n'ai pas reproduite.

## Confirmé (comptes)

- **Historique (§0).** Je retrouve :
  - 81 commits sur `Alundra` et `Alundra.Tests` entre `9502f75` et le HEAD, dont un seul hors `refactor(dll)` (`146e1d8`, R0) ;
  - 0 commit sur ces chemins entre `9502f75` et `2010ec3`, puis `b35a2c4`, `532cf3f` et `a6f003b` en documents ;
  - 331 fichiers de test `.cs` à `9502f75` contre 333 au HEAD, les deux ajouts de R0 se retrouvant dans `git show --stat 146e1d8` ;
  - 131 `.cs` dans `Alundra/` à `9502f75` (129 scripts plus 2) ;
  - le sous-module analyseur à `fc16872` aux trois dates.
- **Pureté.** Mon contrôle indépendant porte sur les 21 fichiers DLL cités (scissions comprises) et les 5 fichiers de test cités.
  - Aucune ligne hors commentaire ne diffère, sauf dans `WorldProxy` : les 3 `catch (ScriptTypeResolutionException) { AbortCreation(...); throw; }` de R0 (`WorldProxy.cs:1122`, `:3444`, `:3580`) et la qualification `Dialogues.` (erreur 4).
  - Les 5 fichiers de test sont identiques après renommage.
- **Citations.** Les 60 lignes du tableau désignent le même code au HEAD (54 de `report.md` et `check.md`, 6 du plan), avec les seuls décalages de bornes de l'erreur 7.
- **Verdicts DLL.** Sur les 25 (A1 à A19, M1 à M6) :
  - 21 sont confirmés tels quels ;
  - 4 le sont avec une correction (M1b : erreur 1 ; M2 : erreur 2 ; M1 : erreur 3 ; A19 : erreur 6).
  - Contrôles d'appui :
    - A3 : la liste des 15 états ;
    - A4 : aucune occurrence `-i` de `8007C0D8` ni de `8007F27C` ;
    - A5 et A10 : l'ordre `:2927`, `:2941`, `:2979`, puis `RunEntityBlock` `:2582` dans la boucle et le vidage `:2650` après ;
    - A11 : aucun faux d'`ICellMutator` dans les tests ;
    - A12 : `Effects[2]` a 1 animation, 9 images (première `[5,0]`), fin `Destroy` et 9 jeux d'images ;
    - A17 : la doc `IEntityWorldContext.cs:72-80` ;
    - la ligne 37 : sons 31 et 84 avec `skip_reason` nul et 1 tone ;
    - M3 : le précédent `BalanceTables.cs:176-178` et le leurre `AlundraBalanceTablesTests.cs:154` ;
    - M4 : 483 cartes, 165 avec cases coupables, 9 089 cases, `BalanceLevel` = "2" partout.
- **Pins des bouchons de l'herbe (§3.1).** La liste est complète.
  - `rg StubCallsForTests Alundra.Tests/` donne 13 lignes dans 4 fichiers. Ce sont exactement les poses et remises à nul citées, plus `AlundraHeroChargeTests.cs:667/675` (atterrissage) et `AlundraFlailBounceTests.cs:342/344/525/542/627/634`, qui sont des aides et des tests du §3.2.
  - `rg GrassCut|GrassCallTicks|wave_grass` ne trouve rien d'autre.
  - Au total : les 5 tests, l'aide `GrassCallTicks` et les 3 remises à nul sont confirmés.
- **Autres pins du §3.**
  - §3.2 : 15 lignes de `AlundraFlailBounceTests`, `AlundraHeroChargeTests.cs:654` et les 7 exclusions « E20.e », confirmées.
  - §3.4 : 9 pins de primitives, confirmés.
  - §3.5 : 3 pins (`AlundraDestroyEntityBreakEffectTests.cs:205/216`, `AlundraBeannoideOracleTests.cs:338/370`, `AlundraMurutaOracleTests.cs:420/450`), confirmés.
  - §3.3 : les 16 sites de `HitArena`, les positions (y compris le balayage `dy` de -22 à 25), la nage en (40, 48) sans case coupable autour, les 179 cases de la carte 14 en rangées 11 à 55, les 9 préréglages et `arsenal-a-m14` en (8, 6, 0), confirmés.
- **N1 à N8**, confirmés. Je retrouve notamment :
  - ADR-0075 aux lignes 23 et 28, et le plan R5 (333 fichiers changent de dossier, 306 de nom) à `plan-restructuration-dll.md:881-883` ;
  - les fichiers `wave_grass*` et leurs comptes (13 et 10 coupes, 67 et 25 appels), avec `rg wave_grass docs/plan-e20-annexe/e20-e/` vide ;
  - `Q-E1` à `Q-E9` absentes du plan ;
  - `Q-E0C-1` à `Q-E0C-7` en `:6705-6711` ;
  - aucun ADR daté du 2026-10-17, `f2bdb4b` étant du 2026-10-17 à 10:46.

## Inconnus

- **Omission 1 :** la position du héros au moment de la frappe et l'issue réelle du test. Aucun test n'a été lancé.
- **Omission 2 :** la valeur de `CollisionHeight` posée par `JumpHeroRig`, et la règle choisie quand `CellMutator` est nul (U3).
- **Arcs de récit sur des cartes avec herbe :** `Inoa-176`, `Inoa-162` et la 478 (103 cases chacune), la carte 10 (`AlundraEntityContactArcTests`, `AlundraHeroJumpArcTests`) et la 83 (`AlundraAbsoluteZWritesTests`).
  - Je n'y trouve aucune frappe délibérée : Square n'est tenu que pendant un dialogue ouvert (`AlundraArcSupport.cs:550-552`), et `AlundraInoaDayOneArcTests.cs:371` se joue sur la carte 164, sans herbe.
  - L'absence de tout état d'attaque dans ces arcs n'est pas mesurée.
- **Chiffres non recomptés :** les 18 342 tuiles « visibles » (omission 5) et les coûts d'overlay de M6 (1 816 + 2 360, 5 037).
- **Pureté des fichiers non cités :** je ne l'ai pas recontrôlée. Ma vérification couvre les 21 fichiers DLL et les 5 fichiers de test cités, pas les 129 + 331.
