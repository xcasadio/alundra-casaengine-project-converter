# E20.f5, T1 (phase A) : résultats (2026-10-23)

HEAD de départ de T1 : `0de20e19` (T0). Branche `chantier/theme1-porter-plateforme`. Rien ne naît en production : `ProductionWiredWeaponTypes` reste `{7, 27, 28, 29}` (l'ensemble `{3, 4}` est la phase B, T3). La clé (E, 17) du registre de production est inerte : aucun enregistrement de carte
n'a un type 3 à 6 de la page globale (`../../bin/out/census_global_page_records.txt`).

## 1. Ce qui est écrit

Code (DLL) :
- `Alundra/Ai/Projectiles/ArrowAi.cs` (neuf) : E17 (`Deactivate`), la phase de rebond (animation 1 : la fin d'animation détruit, sinon rien), le rebond (`Bounce` : animation 1, direction + 0x10, drapeaux `(F | 0x140) & ~0x30`, `ResyncControllerFromFlags`), la recherche
  (`FindTarget` : tous les corps de `SpawnedEntities`, sans filtre de statut ; soi, `HitAnimByte & 0x40`, `DamagedTickCounter`, le masque de classe de `HitPass.ClassMaskOf`, le type 429 ; recouvrement inclusif de `HitPass.AxisOverlaps` sur la boîte stockée), `Keys`, `Register`.
  Trouvé : `DestroyEntity(e, -1)` puis retour (D-F5-3) ; non trouvé : statut 2.
- `Alundra/Ai/NativeSpriteRegistry.cs` : `ArrowAi.Register` dans `CreateForProduction` ((E, 17) pour 3, 4, 5, 6) ; docs.
- `Alundra/Physics/ScriptedMotion.cs` : la règle de la clé de vitesse (D-F5-5) : une entité marquée `HasLogicalZ` prend la vitesse de `TargetAnimationId` (le prédicat de la révision au HEAD ; le terme `JoinsBirthTick && CurrentAnimationId == ~0u` reste).
- `Alundra/Hero/PlayerManager.cs` : le commentaire de la ligne 1208 (et celui de la doc de `SpawnWeaponProjectile`) cite maintenant le type 7 d'f4, les types 27 à 29 de f3c et les types que f5 (3, 4) et f6 (11, 12, 14, 15) ajoutent (le report P4 d'f4).

Tests (dossier miroir, ADR-0080) :
- `Alundra.Tests/Ai/Projectiles/ArrowAiTests.cs` : (e1) la touche détruit, l'absence de touche rebondit aux quatre directions ; (e2) un filtre par cas à côté de la cible valide, soi-même, le héros (drapeaux `0x3118C`, puis avec la classe A posée), les limites du recouvrement sur les trois axes (contact et une unité raw de plus,
  des deux côtés) ; (e3) aucun filtre de statut, la dernière case, une case libérée ; (e4) la phase de rebond ; (e5) les clés du registre (3 à 6, pas 7, 11, 27, 79).
- `Alundra.Tests/Ai/Projectiles/ArrowLifeTests.cs` : (e7) la vie entière contre le mur à 1, 2 et 3 ticks par image pour le chasseur et William (le (e6) y est : le rebond et la série de X égalent l'oracle quelle que soit la cadence), le générateur au pas par image, les autres murs, les bords (est, sud),
  les vols libres, les 12 départs du mur, la cible haute (touche S+30, trois étincelles, neuf tirages, mort S+31, case libre S+32), la portée de 16 et de 17, le vrai sprite 136, l'invulnérable après la touche et les deux filtres posés après la touche, le ballon (type 429), deux cibles, la cible dans une
  case plus haute que la flèche (D qui lit encore la flèche), la cible détruite dans le même tour (par un D de la cible fait à la main, `DestroyEntity(e, -1)`), les tirs en l'air (S = 9 et S = 17).
- `Alundra.Tests/Ai/Projectiles/ArrowOracle.cs` (les clés lues par nom, en `bin/out/json` ou `f5/oracle/out/json`, `_pf` pour le pas par image) et `ArrowRun.cs` (le banc, le modèle de `ChargedArrowRun`) ; `Support/CutBench.cs` : `CutHost.DestroyEntity(e, fx)` note l'appel et passe l'entité en `FlagToDestroy` (rien d'autre n'a changé).
- Réécritures par dérivation (liste mesurée du T0) : `Ai/NativeSpriteRegistryTests.cs`, `Ai/Projectiles/WaveSwordLifeTests.cs` V3, `Ai/Projectiles/ChargedArrowAiTests.cs` C2 (les trois épingles du registre : le compte d'avant plus `ArrowAi.Keys`) ;
  `Hero/WeaponSpawnSeamTests.cs` S5 (la clause négative de la flèche prend un registre **vide**, la clause positive le registre de production : les types 3 et 4 passent l'invariant) ; `Maps/MapStateTests.cs` P3 (le pin d'e0k1 : une entité marquée prend la vitesse de l'animation demandée, une
  entité sans marqueur celle de l'animation courante, les deux côtés se discriminent) ; `Entities/BirthTickTests.cs` (le pin « sans marqueur » efface les DEUX marqueurs, et l'entité marquée à côté avance à 153, 159, 165 comme l'oracle) ;
  `Entities/LogicalZWorldTests.cs` Z5 (la flèche « posée au ras du sol » n'est plus désactivable : elle rebondirait sinon, E17 existant).

## 2. Rouge avant le code, vert après (nommés)

Sur le squelette qui compile (`ArrowAi` avec la clé enregistrée mais `Deactivate`, `Bounce` et `FindTarget` sans corps, la règle de la clé de vitesse absente) : 146 tests lancés (les classes neuves et les tests réécrits), **50 rouges, 96 verts** (`t1-red-names.txt`).
Les 50 rouges sont les 49 cas neufs rouges (`ArrowAiTests` : 19 cas sur 20 ; `ArrowLifeTests` : 30 cas sur 35) et P3 réécrit ; ils deviennent tous verts avec le code.
Les verts du squelette sont des tests de caractérisation, ils le restent : E5 (la clé du registre), le générateur à un tick par image contre le mur, les vols libres est et sud (2 cadences) — et les épingles réécrites du registre, S5, le pin de `BirthTickTests` et Z5, que la dérivation rend
vertes avant et après le code.
Preuves de retrait faites en T1 (sur le code final, puis annulées) : retirer la ligne de la clé de vitesse rend rouges P3 et 21 cas de la vie (le mur 6, les chemins 13, les tirs en l'air 2) ; retirer l'appel `ArrowAi.Register` du registre rend rouges E5, les trois épingles, S5 et 30 cas de la vie.

## 3. Suites

- `Alundra.Tests` **Release** (copie neutralisée, `skipcopy.targets`) : **5 424 réussites, 0 échec** (5 369 au départ + 55 cas neufs).
- `Alundra.Tests` **Debug** (en dernier, il dépose la DLL dans `alundra-project/`) : **5 424 réussites, 0 échec**.
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identique.
- `python -B tools/dll-structure/check_names.py --rules` : 0 finding, 1 avertissement connu (`TextBoxGpu.cs`).
- Les quatre `docs/hero-trace-389-*.txt` réécrits par les suites : `git diff --ignore-cr-at-eol` vide, restaurés par `git checkout`, jamais indexés.
- Moteur (`CasaEngineMonogame`), analyseur, convertisseur et données : aucun diff.

## 4. Tests existants déplacés, contre la liste prédite (`../results.md` §3)

Phase A, mesurée = prédite : les trois épingles du registre (`NativeSpriteRegistryTests`, `WaveSwordLifeTests.V3`, `ChargedArrowAiTests.C2` — la troisième, née d'f4, est de la même nature que les deux du plan), S5 (clauses de E17), P3, `BirthTickTests.AnEntityWithoutTheMarker…`,
`LogicalZWorldTests.Z5…`. Soit 7 tests réécrits par dérivation (sous le seuil de 10). S1 (premier bloc) et `ChargedArrowProductionTests.P1` bougent en phase B seulement (T3) : verts ici. `MapEdgeWorldTests` B1, `MapStateTests` K1 et le (p5) de f3c : verts sans modification.
Aucun test hors de la liste prédite n'a bougé.

## 5. Constats de l'exécution (écarts et précisions au plan)

1. **Le prédicat de la règle.** `entity.HasLogicalZ` (révision au HEAD) ; il se pose avec `JoinsBirthTick` pour toute entité née par l'interface. La phase A reste inerte en production (aucune flèche, aucune boule : les vagues de l'épée des types 27 à 29 sont les seules entités marquées nées en production et
   les tests de vie de la vague, verts, le confirment).
2. **L'effet des écritures du rebond sur l'entité détruite.** Dans les clés, la ligne du statut 4 de la touche porte `dir=8 anim=1 flags=0x12144` : le binaire retombe de la destruction dans les écritures du rebond (D-F5-3). La DLL retourne sans les faire (rien ne vit après) ; les lignes de statut 4 se comparent
   donc sans direction, animation, drapeaux ni fin d'animation (`ArrowOracle.Line`, `ArrowTick.Line`). À T4, le mutant « E17 retombe dans le rebond après la destruction » est équivalent.
3. **Le générateur des clés à cible.** `rng0` du tick 1 d'une clé à cible est le germe plus 2 pas (le créateur réel de la cible tire deux fois) : les tests de cibles le posent (`ArrowRun.generatorAtTick1`), jamais le germe.
4. **Le générateur du tir en l'air.** La clé a 6 tirages au tick 22, ceux du héros qui retombe ; la DLL n'en fait qu'un pas dans ce banc : le générateur n'est pas comparé pour les tirs en l'air (la vie de la flèche l'est). Écart du saut du héros, hors f5.
5. **Les sons.** Le 34 du rebond est entendu au tick qui voit le changement d'animation (S+43), quelle que soit la cadence ; aucun autre son de S à S+62, et aucun effet attaché (le bris de l'en-tête de ces types est 0).
6. **Écart relevé, hors du périmètre d'E17 : `arrow3_point_blank`.** Hero à 108 contre un mur de la colonne 5 (x ≥ 120) : l'oracle borne le pas du tick de naissance (x = 109 à S, drapeau S+1, rebond S+2, la flèche repart vers l'ouest), la DLL fait naître la flèche à 111, dans le mur, et le contrôleur ne la laisse plus bouger
   (drapeau tenu, x = 111 aux ticks suivants). C'est le pas du tick de naissance d'une entité marquée contre un mur plus proche qu'un pas (le mover, E20.e0a3b / e0k), pas E17. **Pas de test, rien de corrigé** (une tranche à part).
7. **Le sprite 136.** Il démarre en animation 0, sans boîte vulnérable (le programme A, bouchonné, la changerait) : le test pose `TargetAnimationId = 3`, comme la clé (`sprite136` : touche S+30, corps de l'en-tête 14 × 16 × 17).
8. **La cible détruite dans le même tour** se fait par un D de la cible écrit à la main dans le test (`DestroyEntity(e, -1)` au pick qui suit la touche, la cible étant dans la case plus basse que la flèche) : la flèche trouve la cible de statut 4 et meurt à S+31.
