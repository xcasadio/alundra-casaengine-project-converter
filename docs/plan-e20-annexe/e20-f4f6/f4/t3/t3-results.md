# E20.f4, T3 (phase B) : resultats (2026-10-23)

HEAD de depart de T3 : le commit de T2. Branche `chantier/theme1-porter-plateforme`.

## 1. Ce qui change

- `Alundra/Hero/PlayerManager.cs` : `ProductionWiredWeaponTypes` = `{7, 27, 28, 29}` (la ligne `7`, seul changement de comportement) ; trois commentaires mis a jour (le deuxieme texte « E20.f4 to E20.f6 wire the others » devient « 7 since E20.f4 ; E20.f5 and E20.f6 »).
- `Alundra.Tests/Hero/WeaponSpawnSeamTests.cs` S5 : l'egalite de l'ensemble de production (`:246`) est derivee des classes qui enregistrent les gestionnaires (`WaveSwordAi.SpriteTypes` plus `ChargedArrowAi.SpriteTypes`), comme le demande la repartition des reecritures du plan (point 6).
- `ChargedArrowRun` / `ChargedArrowProductionTests` : deux corrections de test trouvees au premier vert (l'entite touchee se compare a `SelfEntity`, pas au proxy ; (p2) lit le tick et le type de la fleche nee, pas un compte a la fin de la partie, la fleche etant morte au tick 150).

## 2. Verts apres, rouges avant

(p1) `P1_TheChargeOfWilliamBirthsTheArrowAtS_...`, (p2) `P2_WithNoFreeSlotTheShotOfTheArrowCreatesNothingAndClearsThePointer_...`, (p3) `P3_TheTouchingEntityOfTheTarget_IsTheArrowOfProduction_...` : rouges avant T3 (T2), verts apres. (c10) vert avant et apres.

## 3. Preuve de retrait (mutation reelle, annulee ; `git diff` vide sur le fichier)

Retirer la ligne `7` (sur les dossiers `Projectiles`, `Hero`, `NativeSprite`, `BirthTick`, `Combat` : 1 857 tests) : **4 rouges** : P1, P2, P3 et `WeaponSpawnSeamTests.S5_...` (l'egalite de l'ensemble de production, derivee des classes : c'est la reecriture de phase B du plan, point 6, donc dans la liste predite).
Le plan dit « exactement (p1) a (p3) » ; S5 en plus est la ligne `:243` deplacee par f4 phase B, predite par le point 6 (ecart de formulation du plan, pas de test hors liste).

## 4. Suites

`Alundra.Tests` **Release** (construit avec `skipcopy.targets`) : **5 368 reussites, 0 echec** (5 364 apres la phase A + 4 tests de T2). **Debug** (en dernier, depose la DLL dans `alundra-project/`) : **5 368 reussites, 0 echec**.
Garde `check_names.py --rules` : 0 finding. Fins de ligne des quatre traces `docs/hero-trace-389-*.txt` : `git diff --ignore-cr-at-eol` vide, restaurees, non indexees.

## 5. Tests existants deplaces, contre la liste predite (`../results.md` §4)

Phase A : les trois de la liste (deux epingles du registre, S5 clause `Contains((C, 94))`). Phase B : S5 (l'egalite `:246`, ex-`:243`) seulement. Aucun test hors liste n'a bouge ; `BirthTickTests.TheSoundOfTheCreation_...` reste vert sans modification. Total 4 distincts, sous le seuil d'arret (10).
