# E20.f6a, T1 : résultats (2026-10-23)

HEAD de départ de T1 : `96ca9001` (T0). Branche `chantier/theme1-porter-plateforme`.

## 1. Ce qui est écrit

Code (DLL) : `Alundra/Entities/EntityScriptProxy.cs`, `TickMotion` : dans la branche `IsActive` (statuts 2 et 3) des entités autres que le héros, juste après `ScriptedMotion.TickScriptedNpc(this)`, `if (HasLogicalZ) { UpdateGroundSlope(); }`.
Le marqueur est `HasLogicalZ` (celui d'e0k1 et e0k2 : « la vertical appartient au tick », posé pour toute entité née par l'interface). Les docs que f6a rendait fausses sont corrigées : la doc de `UpdateGroundSlope` (le paragraphe « RESTRICTION… PLAYER ONLY… Every NPC's Slope_18c stays 0 » et la phrase « nothing reads it yet »),
la doc de `TickPhysics` et le résumé de `TickMotion`. Rien d'autre dans la DLL ; le moteur, l'analyseur, le convertisseur et les données n'ont aucun diff.

Tests (dossiers miroirs, ADR-0080) :
- `Alundra.Tests/Entities/MarkedEntityGroundSlopeTests.cs` (21 cas) : (s1a) la boule marquée au repos sur l'eau profonde (feu et glace), la pente 1, 2, 3 : `Slope_18c` égal à la clé (4, 4, 1, 2, 3), 0 en l'air avant l'atterrissage, la ligne de vol (statut, X, Z, pente, posé) égale à la clé de S à l'atterrissage ;
  (s1b) les sept cas « sans effet », chacun accolé dans le même test à la boule sur l'eau profonde qui lit 4 : le pied moitié sur l'eau, les quatre coins secs un à un, la marche de 16 px (feu et glace) ; (s1c) sans la gravité : 0, après la lecture 4 de la même boule ; (s2) une entité née par les enregistrements de la carte (un Beannoïde, avec la gravité,
  debout sur la même eau) garde 0 à chaque tick, et la règle appelée à la main sur elle lirait 4 ; (s3a) la boule de feu née sur l'eau profonde est détruite à S+11 (statut 4, encore dans la table), absente à S+12, l'effet 6 attaché vu sur la scène à la fin de l'image qui contient S+11 et jamais avant, un pas du générateur par tick, à 1, 2 et 3 ticks par image,
  accolée à la même boule sur le sol sec qui atterrit, prend le statut 3 de l'impact, vit et ne lit rien ; (s3b) la glace, mêmes ticks ; (s4) en statut 3 la boule lit 4 (et n'est pas détruite), en statut 4 elle garde 0.
- `Alundra.Tests/Ai/Projectiles/BallRun.cs` (le banc : le modèle d'`ArrowRun`, la baguette de feu ou de glace, la boule née par le crochet) et `BallOracle.cs` (les clés lues par nom, `bin/out/json` ou `f6a/oracle/out/json`) ;
  `Alundra.Tests/Support/CutBench.cs` : `Cell` reçoit `GroundProperty` (défaut 0), écrit dans le tableau `ground_property` du champ (aide de test partagée, aucun appel existant ne change).

## 2. Rouge avant le code, vert après (nommés)

Sur le HEAD de T0 avec les bancs et les tests, sans l'appel : 21 tests, **20 rouges, 1 vert** (`t1-red-names.txt`). Les rouges sont (s1a) ×5, (s1b) ×7, (s1c), (s3a) ×3, (s3b) ×3 et (s4), tous par la valeur de `Slope_18c` (attendu 4, 1, 2 ou 3, obtenu 0) ou par la ligne de la clé ;
le vert est (s2), le témoin négatif (l'entité née par enregistrement garde 0), **vert avant et après** le code. Avec l'appel : **21 sur 21 verts**.
**Preuve de retrait** : retirer l'appel (c'est l'état « avant ») rend rouges exactement (s1a), (s1b), (s1c), (s3a), (s3b) et (s4) ; (s2) reste vert.
La boule née sur l'eau profonde est détruite à S+11 par le choix du programme du code existant (`PickEventTrigger`, `DestroyOnSlidingSlope` et `Slope_18c == 4`), sans que f6a n'y touche.

## 3. Mutants réels de la DLL (`mutants.txt`, `t1mut.py`)

Neuf mutations appliquées par script, construites en Release (copie neutralisée), les tests de la tranche et ceux de `GroundSlopeTests` du héros lancés, les fichiers restaurés octet pour octet : appel pour tous les gravitaires en tout statut (M1), appel sans marqueur (M2), minimum remplacé par maximum (M3), égalité de Z décalée d'une unité (M4),
chacun des quatre coins ignorés (M5a à M5d), appel limité au statut 2 (M6) : **tous tués**, chaque coin ignoré par exactement un des quatre scénarios de coin sec.

## 4. Suites

- `Alundra.Tests` **Release** (copie neutralisée, `skipcopy.targets`), trois partitions (une passe complète dépasse dix minutes) : 1 210 + 2 628 + 1 789 = **5 627 réussites, 0 échec** (5 606 au départ + 21 cas neufs).
- `Alundra.Tests` **Debug** (en dernier, il dépose la DLL dans `alundra-project/`) : 1 210 + 2 628 + 1 789 = **5 627 réussites, 0 échec**.
- `cmp Alundra/bin/Debug/net9.0-windows/Alundra.dll alundra-project/Alundra.dll` : identique.
- `python -B tools/dll-structure/check_names.py --rules` : 0 finding, 1 avertissement connu (`TextBoxGpu.cs`).
- Les quatre `docs/hero-trace-389-*.txt` réécrits par les suites : `git diff --ignore-cr-at-eol` vide, restaurés par `git checkout`, jamais indexés.
- Moteur (`CasaEngineMonogame`), analyseur, convertisseur et données : non touchés par T1.

## 5. Tests existants déplacés, contre la liste prédite

Liste prédite (T0, §4 de `../results.md`) : **vide**. Mesurée : **vide** (aucun test existant n'a changé de résultat ni été modifié ; seul l'aide partagée `CutBench` reçoit un champ optionnel).

## 6. Constats de l'exécution (écarts et précisions au plan)

1. (s2) : le plan dit « la même boule née par enregistrement » ; aucun enregistrement de carte ne porte un type 10 à 15, le témoin est l'entité à gravité née par enregistrement la plus proche (le Beannoïde de la carte 14), avec la règle lue à la main sur elle comme positif accolé.
2. La boule du banc n'a pas de gestionnaire (C60 et E9 sont f6b) : après l'atterrissage elle continue d'avancer de 6 px par tick ; les comparaisons à la clé s'arrêtent donc à l'atterrissage (S+10 à plat, S+7 sur la marche), sauf sur l'eau profonde où la destruction de S+11 et l'absence de S+12 sont celles de la clé.
3. Les tests de la tranche sont des tests de monde sur le vrai monde de la carte 14 (`HitArena`) avec le champ synthétique ; aucun n'a besoin de la graine seule (le générateur est lu comme « un pas par tick » : zéro tirage).
