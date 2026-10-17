# Contre-vérification de `e0c/state.md` (E20.e0c, HEAD `978608d`)

Lecture seule ; aucun build, aucun test .NET. Lignes anciennes lues par `git show 2010ec3:<chemin>` (aucune différence `9502f75..2010ec3` dans `Alundra/` et `Alundra.Tests/` : `git diff --stat` vide), lignes HEAD lues dans l'arbre. Scripts de cette vérification : `scratchpad/e20-e-state/e0c-verif/` (`show.sh`, `normdiff.py` + `normdiff.out`, `md5table.py`, `oracle_facts.py`, `catalog2.py`). Seul outil du dépôt lancé : `python -B tools/dll-structure/check_names.py --tree --stage final` (lecture seule d'après sa docstring `:9`).

## Erreurs

1. **A4, preuve fausse (verdict juste).** `rg -n 'SpawnRandomLoot|SpawnContents|GiveItem|ApplyItem' Alundra` ne rend **aucune** ligne (même en `-i`, avec `RandomlySpawnItem|SpawnEntityContents`). `LootRoll.cs:11` et `:52` citent `ChooseRandomlyAnItem`, pas ces noms. Le « seulement des commentaires (`LootRoll.cs:11`, `:52`) » est à remplacer par « aucune occurrence ».
2. **A12, ligne de doc.** « a constant of the binary is passed minus one » est à `Alundra/Maps/IEntityWorldContext.cs:76` (doc du Z `:75-76`), pas `:77-79`. `:79` est la phrase « Not called by any production path in E20.e0a1 » (A11).
3. **§1.a, ligne `AlundraWorldProxy.cs:2958-2978`.** L'appariement `2992-3012` et le décrément `:3010-3012` sont justes, mais `RunCountersPass` est déclaré à `WorldProxy.cs:3000` (`:3002` foreach, `:3003` accolade), pas `:3003`.
4. **§1.b, ligne `AlundraEnemyHandlerOracle.cs`.** Le fichier ne contient pas « que `HandlerJournalEntry` et le lecteur ». Il a cinq records : `HandlerChange :12`, `HandlerJournalEntry :15`, `HandlerVector :21`, `HandlerCall :30`, `HandlerScenario :36`. Il a aussi la classe `AlundraEnemyHandlerOracle :42`. Le verdict PARTIEL tient : `rg -i 'exclu|X1'` ne trouve rien dans ce fichier, l'exclusion est dans les deux tests.
5. **A19, attribution.** La parenthèse « Le rapport dit « index 0 et 1 » » se trompe de source. C'est le commentaire de la DLL qui le dit (`EventProgramRunner.cs:284`). `report.md:245` et `check.md:78` ne parlent que de l'index 11.
6. **§3.c, premier point.** « `rg ProgramEDeactivate` dans les tests : seuls les codes 0, 1, 17 sont testés » est faux.
   - `Alundra.Tests/AlundraEntityDestructionTests.cs:106-119` (`TR5_NativeEOfAnotherIndex_DoesNotDestroy`, `[InlineData(2)]`, `(3)`, `(255)`, `(-1)`) épingle le no-op du slot E pour 2, 3, 255 et −1. `:92-104` épingle 0 et 1.
   - `AlundraWaveSwordLifeTests.cs:185-187` épingle E10, traité en ligne et hors registre.
   - `AlundraWeaponSpawnSeamTests.cs:247-258` épingle E17 et E1.

   La conclusion reste vraie : aucun test ne porte sur l'index 11. Ces tests TR5 passent par un `ContactHost` factice, pas par `WorldProxy`, donc le lâcher ne les touche pas.
7. **§4.4, ADR-0057 « jamais cité ».** Faux : `report.md:263` (Q-e0c-2) écrit « étendre l'ADR-0057 aux objets ».
8. **§4.4, prérequis « tous faits » (`plan:605, 624, 729`).** Ces lignes énoncent les exigences, pas l'état. L'état est ailleurs dans `docs/plan-e20-heros.md` :
   - `:4298` : e0a5 ✅, CONFIRMED le 2026-10-16 ;
   - `:4497` : e0b2, CONFIRMED le 2026-10-16 ;
   - `:4945` : e0k1 à e0k3, CONFIRMED le 2026-10-16.

   Le fait est juste, la citation non.
9. **§4.7, plage de R5.** `docs/plan-restructuration-dll.md:861-1232` ne délimite pas R5 :
   - R5 (§9) occupe `:861-1005` ;
   - `:1006-1201` est R6 (§10) ;
   - Q-R-3 est à `:1204` (§11) ;
   - « 333 fichiers / 306 aussi de nom » est à `:881-883`.
10. **A21, verdict trop large.** Le VRAI ne couvre que les programmes 255 (repris de A20). `check.md:80` affirme aussi « mêmes drapeaux et tailles que dans l'EXE ». L'agent n'a pas refait cette comparaison à l'EXE : les drapeaux sont relus dans le catalogue seulement (A22). La partie EXE est NON VÉRIFIÉE.
11. Détails mineurs :
    - A8 « clone `:2682` » désigne `EntityScriptProxy.cs:2682`, mais le nom de fichier manque après une citation d'`EventProgramRunner.cs`.
    - La qualification de `WorldProxy.cs:408` est `Alundra.Entities.SpriteRecordCatalog` (nom complet), pas `Entities.SpriteRecordCatalog`.

## Omissions

1. **Une seconde épingle du registre à 7 clés.** `Alundra.Tests/AlundraWaveSwordLifeTests.cs:166-189` (`V3_TheProductionRegistryHoldsSevenKeys_...`) contient `Assert.Equal((3 + 3 + 1, …), (registry.Count, …))` (`:181-183`). Inscrire (C,255) et (A,255) dans `CreateForProduction` casse aussi ce test, pas seulement `AlundraNativeSpriteRegistryTests.cs:55`. Aucun autre test n'épingle `registry.Count` ou `NativeSprites.Count` en absolu : `:242-249` du test du registre est relatif.
2. **Les bouchons de la coupe d'herbe, seul appelant de `SpawnRandomLoot`** (`0x8003A988` dans `0x8003A7B0`, report §1.4). Le §3.c ne nomme que celui de la vague. La DLL en a deux :
   - `PlayerAnimationEffects.PlayerGrassCut` (`Alundra/Hero/PlayerAnimationEffects.cs:217-218`), avec `GrassCutStubName :37`, `IsGrassCutState :195` et le journal `StubCallsForTests :51` ;
   - `WaveSwordAi.CheckAndTriggerTileEffect` (`WaveSwordAi.cs:36-40`, journal `:27`).

   Tests qui les épinglent :
   - `AlundraFlailBounceTests.cs:375-381`, `:397-401`, `:508`, `:513`, `:753-763` ;
   - `AlundraWaveSwordLifeTests.cs:129-151`.

   Ils relèvent d'E20.e, mais T1 écrira `SpawnRandomLoot` sans appelant de production tant qu'E20.e n'est pas faite (INFÉRENCE).
3. **`ModdedPos` (Q-E0C-4, A7).**
   - `AlundraEntityCollisionTests.cs:102-123` (`PositionsAreRecomputedOnTheFly_...`) commente `:106` « ModdedPos* fields - written only at spawn in this DLL » et simule un cache figé (`:110-120`). Le test resterait vert si `ModdedPos` était rafraîchi : il ne fait pas de tick et vérifie que la collision lit les positions en direct. Son commentaire deviendrait périmé (INFÉRENCE).
   - Précision pour A7 : **aucun code de la DLL ne lit `ModdedPos`**. Hors déclaration (`EntityScriptProxy.cs:486`), écritures (`EntitySpawnFactory.cs:825-827`) et clone (`:2684-2686`), tous les résultats de `rg 'ModdedPos[XYZ]' Alundra` sont des commentaires. Le contact de C255 serait le premier lecteur.
4. **ADR utiles absents de la liste du §4.4.**
   - `0055-native-enemy-handlers-are-registered-per-sprite-type-and-read-per-tick-values` : le registre de T2a.
   - `0072-the-sword-wave-is-born-in-production-with-e10-in-line-and-c61-as-a-named-stub` : le précédent d'A17 et le bouchon `CheckAndTriggerTileEffect`.
5. **INCONNU du §4.2, à moitié levable.**
   - `tools/dll-structure/check_names.py:96-98` signale `[hors table]` tout fichier de la DLL absent de la table à l'étape demandée. C'est un constat, donc sortie 1 (docstring `:9`).
   - `table.json` porte « Generated by tools/dll-structure/gen_table.py; do not edit » (`:3`).

   Un fichier ajouté par la tranche fera donc échouer `--tree --stage final` tant que la table n'est pas mise à jour. Qui la met à jour, et comment, reste INCONNU.

## Confirmé (comptes)

- **Tables de citations.** §1.a : 30 lignes, 29 justes en entier ; la dernière a le bon appariement et un détail faux (erreur 3). §1.b : 15 lignes, 14 justes ; la dernière a un verdict PARTIEL juste mais une description inexacte (erreur 4). Chaque ligne a été ouverte à `2010ec3` et à HEAD (`show.sh`) : même code partout. Les citations hors DLL ont été relues dans le sous-module à `fc16872` :
  - `FunctionTypeE.cs:291-292` ;
  - `PlayerManager.cs:4562`, `:5070` ;
  - `GameMapInfo.cs:11`, `:38` ;
  - `GameEngine.cs:720`.
- **Diff normalisé, refait indépendamment** (`normdiff.py`, résidu multiensemble hors commentaires, sur 14 fichiers DLL et 10 fichiers de tests). Résidus de code, et seulement eux :
  - dans `WorldProxy` : les 3 blocs `catch (ScriptTypeResolutionException) { AbortCreation(...); throw; }` et les qualifications `Audio.` / `Transitions.` / `Dialogues.` / `Alundra.Entities.` ;
  - `typeof(EntityScriptProxy).FullName!`, ×3 dans la fabrique et ×1 dans chaque test d'oracle (`AlundraBeannoideOracleTests.cs:139`, `AlundraMurutaOracleTests.cs:160`) ;
  - les `#nullable enable` des fichiers coupés.

  Aucun résidu pour `LootRoll`, `GameRandom`, `EntityScriptProxy`, `PlayerManager`, `NativeEffects`, `NativeSpriteRegistry`, `WaveSwordAi`, `SaveBook`, `ItemTables`, `HitPass`, ni pour les 8 autres tests.
- **Historique.**
  - 131 commits `2010ec3..HEAD`, dont 81 touchent `Alundra/`, `Alundra.Tests/` et `tools/AlundraTestSaves`. Hors `git mv`, namespace et renommage, il n'y a que `146e1d8` (R0) et `15804ae` (R4).
  - `b35a2c4` ne touche que `docs/` (739 fichiers).
  - Le sous-module analyseur reste à `fc16872` ; le pointeur moteur passe de `781d3527` à `92d8e847`.
  - Tests ajoutés depuis le rapport : exactement `AlundraScriptAssemblyRegistration.cs` et `AlundraScriptNamesInTheDataTests.cs` (`git diff --diff-filter=ADR`).
- **Affirmations A1-A27.** Les 27 verdicts tiennent. A24 est NON VÉRIFIÉ, étiquette juste. Les preuves sont fausses ou incomplètes pour A4, A12, A19 et A21 (erreurs 1, 2, 5, 10). Recalculé :
  - md5 de la table : `7c6b97ac50de72f436a6fbf08ed6bcd7` sur 2 048 octets ; lignes non nulles 0x81-0x83 et 0xF6-0xFF ; épingle `AlundraLootRollTests.cs:55`.
  - Catalogue (`sprite-records.json`, page globale `alundra_N`) : 87 types de 31 à 118 avec A = C = 255 et `Contents` 0. Le 72 manque : il est absent du catalogue. `alundra_30` a A = F = 255 et C = 0. Aucun autre type global n'a un programme A ou C à 255.
  - Drapeaux des 9 objets lâchables : ceux d'A22, à l'identique.
  - Sons 52, 53, 54, 84 présents, `skip_reason` nul.
  - `Effects[14]` : 3 animations.
  - `UseLifeVessel` passe par `Emit` (`EventProgramRunner.cs:3154`) ; `CreateNow` est à `EffectStage.cs:199`.
- **Faits de l'oracle** (`oracle_facts.py`), tous reproduits, Beannoïde comme Muruta :
  - 144 vecteurs E1, dont 128 avec `contents_3C ≠ 0` ;
  - 80 vecteurs avec `spawn_39F58 [0,0,type,X,Y,Z,0,0]` suivi de `sound 84` ; les types sont 99, 100, 101, 114, 115 pour les contenus 69, 70, 71, 84, 85 ;
  - 48 vecteurs à contenu 38, 81 ou 82 sans lâcher ; 16 à contenu 0 ;
  - 160 entrées exclues.

  Scénarios : BN a 4 appels E1, dont 3 lâchers ; MU en a 7, dont 6 ; les scénarios ARENA n'ont aucun E1, bien que le filtre `:422` les inclue.
- **Tests (§3).**
  - §3.a : les 4 lignes sont justes (`:204-219`, `RunE1Call` `:338-396` et `:420-475`, `excluded` `:404-415` et `:483-494`, docs des arcs `:19`, `:21`).
  - §3.b : les 6 lignes sont justes.
  - §3.c : 2 points sur 3 sont justes (voir l'erreur 6).
  - Recherches de complétude sans nouvelle épingle de « rien ne naît » : `Assert.Empty(...SpawnedEntities)` ne figure qu'aux trois sites connus (plus `AlundraWorldProxyEntityManipulationTests.cs:188`, sans rapport) ; `ContentsItemId =` n'est posé qu'à 4 endroits ; `Recycle` (`AlundraLootRollWorldTests.cs:74-78`) pose le statut sans `DestroyEntity`, donc T3 ne le touche pas ; aucun test n'utilise la clé (C,255) ou (A,255).
- **Points du §4.** Les points 1, 2, 3, 5, 6, 8, 9, 10 et 11 sont justes. Les points 4 et 7 ont des erreurs (7, 8, 9). Vérifications refaites :
  - `check_names.py --tree --stage final` rend 207 fichiers, 220 types, 0 constat et 1 avertissement (`Alundra.Tests/UI/TextBoxGpuHarness.cs:265`), sortie 0.
  - ADR-0075 : ajouté par `f2bdb4b` le 2026-10-17, en-tête daté du 2026-10-16. Aucun 0076 dans `docs/decisions/`.
  - ADR moteur 0075 à 0077 à `92d8e847` ; l'ADR 0076 décrit le refus (`:26-29`).
  - Lignes du plan relues : 3595, 3626, 3677, 3785, 6656, 6705, 3277, 4293, 6358, 5319 et 4924-4926 ; README e0a : 95, 113, 115.

## Inconnus

- La suite de tests à HEAD : non lancée (interdit).
- La liste exhaustive des tests qui détruisent, par un `WorldProxy` initialisé, une entité à `ContentsItemId ≠ 0`. Mes recherches ne trouvent rien au-delà des 3 tests épinglés et des 2 arcs ; les arcs d'autres cartes (E0/E1, pente, VRAM) n'ont pas été énumérés.
- Si l'indice de la liste `Effects` d'`effects-global.json` est bien le numéro d'effet : les entrées n'ont pas de champ d'identifiant (INFÉRENCE de l'agent comme de moi).
- Qui met `table.json` à jour pour un fichier ajouté après R4, et si `gen_table.py` (base `5434cf6`) peut être relancé.
- Les inconnues côté binaire (§6 du rapport) : inchangées, non revues ici.
