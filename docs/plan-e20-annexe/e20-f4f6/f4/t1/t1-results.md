# E20.f4, T1 (phase A) : résultats (2026-10-23)

HEAD de départ de T1 : `dc29fde6` (T0). Branche `chantier/theme1-porter-plateforme`. Rien ne naît en production : l'ensemble `ProductionWiredWeaponTypes` reste `{27, 28, 29}` (la ligne `7` est la phase B, T3).

## 1. Ce qui est écrit

Code (DLL) :
- `Alundra/Ai/Projectiles/ChargedArrowAi.cs` (neuf) : C94 (`Tick`), `CheckAndTriggerTileEffect` (le journal par thread `StubCallsForTests`, puis `TileEffect.Trigger`, le même appel que `WaveSwordAi`), la traînée (statut Normal, `FrameCounter & 3 == 0`, valeurs capturées à
  l'émission), `Keys`, `Register`, `DirectionIndex` (la table `0x80023644` à l'envers).
- `Alundra/Rendering/Effects/NativeEffects.cs` : `ChargedArrowTrailEffect` (0x19) et `CreateChargedArrowTrail` (aucun tirage, aucun son, forces `(-F) >> 1`, `CreatedForTests` appelé).
- `Alundra/Events/EventProgramRunner.cs` : `NativeSpriteTypeFilterForTests` (D-F4-7, `[ThreadStatic]`, nul en production) lu avec `NativeHandlersEnabled`.
- `Alundra/Ai/NativeSpriteRegistry.cs` : `ChargedArrowAi.Register` dans `CreateForProduction` (la clé (C, 94) pour le type 7) ; docs.
- `Alundra/Hero/PlayerManager.cs` : quatre commentaires périmés corrigés (« empty until E20.f3c », « The set is empty in this slice », « the production set stays empty », « wire the others »).

Tests (dossier miroir, ADR-0080) :
- `Alundra.Tests/Ai/Projectiles/ChargedArrowAiTests.cs` : (c1) C94 sur une entité faite à la main (quatre directions, trois jeux de forces dont 1, compteurs 3 à 8, statuts, réservoir plein, sans contexte de monde), (c2) la clé du registre.
- `Alundra.Tests/Ai/Projectiles/ChargedArrowLifeTests.cs` : (c4) la vie contre le mur à 1, 2 et 3 ticks par image + le générateur à un tick par image ; (c5) la touche d'une cible et la mort au mur ; (c6) marches de 16 et de 32 px et bord de carte ; (c7) la fenêtre du mur
  sur les 12 départs ; (c8) l'herbe, à 1, 2 et 3 ticks par image.
- `Alundra.Tests/Ai/Projectiles/ChargedArrowOracle.cs` (les clés lues par nom) et `ChargedArrowRun.cs` (le banc), `Alundra.Tests/Support/SyntheticField.cs` (pose le champ et le magasin de `CutBench` dans le monde par réflexion ; aucune aide de coupe en double).
- `Alundra.Tests/Events/NativeSpriteTypeFilterTests.cs` : l'interrupteur (cas pur) et (c9) (l'arène de la carte 14 : C94 tourne, le Beannoïde reste inerte, et sans l'ensemble la flèche ne tourne plus).
- Réécritures par dérivation (D-F4-8) : `Hero/WeaponSpawnSeamTests.cs` S5 (la clause négative du type 7 prend un registre **vide**, la clause positive le registre de production), `Ai/NativeSpriteRegistryTests.cs` (l'épingle : le compte d'avant plus `ChargedArrowAi.Keys`),
  `Ai/Projectiles/WaveSwordLifeTests.cs` V3 (la seconde épingle ; renommé `V3_TheProductionRegistryHoldsTheKeysOfItsClasses_…`, son ancien nom disait « Nine »).

## 2. Rouge avant le code, vert après (nommés)

Sur le squelette qui compile (C94, le créateur de traînée et la clé sans corps, le filtre posé et jamais lu) : **37 tests lancés, 32 rouges, 5 verts** (`t1-red-names.txt`). Les cinq verts sont des tests de caractérisation de la vie native de la flèche
(sans C94 ni coupe) et du générateur, ils le restent après le code : `C4_TheGeneratorFollowsTheKey…`, `C6_AStepOfTheColumn8…` (2 cas), `C6_TheEdgeOfTheMap…`, `C7_TheFlagOfTheWall…` (la flèche avait déjà les ticks de la clé, D-F4-9 est vérifié par eux).
Les 32 rouges (les autres) deviennent verts avec le code : (c1) 17 cas, (c2) 3, (c4) 3, (c5), (c8) 3, (c9) et le cas pur de l'interrupteur, et les trois réécritures (`ProductionRunner_RegistryHolds…`, `V3_…`, `S5_…`).

## 3. Suite

`Alundra.Tests` Release (construit avec `skipcopy.targets`) : **5 364 réussites, 0 échec** (5 330 au départ + 34 cas neufs : 37 lancés moins les trois réécritures). Les fins de ligne des quatre traces `docs/hero-trace-389-*.txt` : `git diff --ignore-cr-at-eol` vide, restaurées, non indexées.
Garde `python -B tools/dll-structure/check_names.py --rules` : 0 finding (1 avertissement connu, `TextBoxGpu.cs`). **Debug** : non relancé en phase A (la construction Debug dépose la DLL dans `alundra-project/` ; elle vient en dernier, à la fin de T3).

## 4. Tests existants déplacés, contre la liste prédite (`../results.md` §4)

Les trois tests réécrits sont exactement ceux de la liste prédite (les deux épingles du registre, S5 phase A). Le quatrième nommé à surveiller, `BirthTickTests.TheSoundOfTheCreation_IsRequestedOnce_…` (il exécute C94 trois fois), reste **vert sans modification** avec le vrai C94.
Aucun autre test existant n'a bougé. Reste à mesurer en phase B (T3) : l'égalité `:243` de S5 (l'ensemble de production), prédite.

## 5. Constats de l'exécution (écarts et précisions au plan)

1. **Le banc.** `HitArena` natifs éteints + l'ensemble `{7}` de D-F4-7, les entités de la carte autres que le héros et les cibles retirées (`FlagToDestroy` puis `RecycleDestroyedEntities`), le champ synthétique de `CutBench` posé par réflexion ;
   le héros à (100, 100) (pour x0 = 96 à 107 et 1100/1150 : posé là par `PlaceHero` sans image de plus). Sonde T0 : relâchement 96, naissance 105 (décalage nul).
2. **Effet de bris.** L'effet 8 est un effet attaché : créé en mode 1, il passe en mode 2 à la mise à jour de la même image ; le banc le cherche en mode 1 ou 2, ancre non nulle (comme `BeannoideGrateArcTests`). Son ancre est la flèche où elle est morte (349, 100).
3. **Création des effets à la fin de l'image.** L'étage applique sa file à la fin de l'image : à 2 et 3 ticks par image, le tick de création d'une traînée ou d'une coupe est le dernier de l'image ; les tests comparent au tick de fin d'image de l'attendu.
4. **Sons.** La clé journalise `snd(0)` aux ticks absolus 10, 24 et 119 de ses treize fichiers, quoi que fasse la flèche : ce n'est pas la sienne (l'identifiant 0 est « pas de son »), le lecteur le saute. Le son 84 d'un objet lâché par une coupe : la machine de l'oracle n'a que les enregistrements des
   projectiles, son apparition d'objet échoue sans son ; la DLL fait naître l'objet et joue le 84. Le test compare les sons sans 84 à la clé, et les 84 d'un tick aux tirages de la clé au-delà d'un par case coupée, **à un tick par image seulement** (à 2 et 3, les valeurs tirées ne sont plus
   celles de la clé, l'écart documenté d'ADR-0066).
5. **Touche.** L'étincelle de touche est un appel de la passe qui crée **trois** étincelles : le banc compte les appels (un à S+29), les neuf tirages se lisent aux pas du générateur (10 avec le pas de l'image).
6. **Fenêtre du mur (D-F4-9).** Vérifiée sur les 12 départs : le drapeau de la DLL vient un tick après l'arrivée (x0 + 6 + 6k atteint 349), le statut 4 un tick après le drapeau ; la clé est au même tick pour 8 départs et un tick plus tard pour x0 = 98, 101, 104 et 107.
7. **Statut de la traînée.** La traînée n'est faite qu'en statut Normal (D-F4-2) : le gestionnaire du binaire n'a pas de test de statut, c'est le choix du programme C (statut 2) qui le donne. Le monde ne montre jamais C94 en un autre statut ; le cas est fixé par (c1) seulement (mutant « traînée en statut 3 » de T4).
8. **Indice de direction.** `DirectionIndex` lit `TargetDirection` par la table à l'envers de `{0, 0x10, 8, 0x18}` (D-F4-2) ; un mot hors table (inatteignable : le tir ne passe que ces quatre mots) se lit comme le bas.
