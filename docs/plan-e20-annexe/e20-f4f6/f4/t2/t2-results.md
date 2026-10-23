# E20.f4, T2 (tests de production) : resultats (2026-10-23)

HEAD de depart de T2 : `9fcc46ad` (phase A). Branche `chantier/theme1-porter-plateforme`. `ProductionWiredWeaponTypes` = `{27, 28, 29}` (la ligne `7` est T3).

## 1. Ce qui est ecrit (tests seulement, aucun code de la DLL)

- `Alundra.Tests/Ai/Projectiles/ChargedArrowProductionTests.cs` (neuf), chemin de production (aucun crochet de naissance, aucun ensemble de test : `PlayerManager.ProductionWiredWeaponTypes` decide), sur le banc de T1 :
  - (p1) `P1_TheChargeOfWilliamBirthsTheArrowAtS_...` : l'appel du tir de type 4 (debut de la prise) est journalise, et l'entite de type 4 est nee si et seulement si l'ensemble de production contient 4 (derive), dans la meme assertion que
    la naissance du type 7 (contient 7) ; puis la fille du heros, la plus basse case libre mesuree avant le tir (1), S = 105, la ligne de la fleche de S a S+44 contre la cle `east_wall_pf`, la trainee (dix effets 25), l'effet de bris 8 attache a S+43,
    aucun tirage (un seul pas, celui de fin d'image) ;
  - (p2) `P2_WithNoFreeSlotTheShotOfTheArrowCreatesNothingAndClearsThePointer_...` : table pleine (64 cases tenues par des fleches de type 3 posees juste avant le tick S) : le tir est demande, aucune entite, pointeur efface ; une case libre : une entite, pointeur efface ;
  - (p3) `P3_TheTouchingEntityOfTheTarget_IsTheArrowOfProduction_...` : la cible de `vs_target` (300, 104) : `TouchingEntity` = la fleche, premier tick S+29 (la cle : `hitc` 1 a S+29), la meme fleche sans cible ne touche rien ;
  - (c10) `C10_TheArrowAttack131Power5_...` : la partie pure `Damage.ResolveBalanceTarget` (attribut 131, puissance 5) contre la fiche 335 du Beannoide, verte par construction (comme le (p7a) de f3c, qui est dans la liste du plan T1 mais n'avait pas ete ecrit en T1 : ecrit ici).
- `Alundra.Tests/Ai/Projectiles/ChargedArrowRun.cs` : option `production` (aucun crochet ni ensemble ; journal du joint de tir), `freeSlotsAtBirth` (remplissage de la table au tick S), relevés neufs (types engendres par le heros, pointeur du tir, la cible touchee par la fleche).

## 2. Rouge avant T3 (nommes), sur HEAD + T2

4 tests lances : **3 rouges** (`P1_...`, `P2_...`, `P3_...`), 1 vert (`C10_...`, par construction). P1 echoue a `Assert.NotNull(run.Born)` (aucune fleche nee : 7 n'est pas dans l'ensemble), P3 de meme, P2 sur la moitie « une case libre » (aucune entite de type 7).
(p1) a (p3) sont donc rouges avant la ligne `7`, comme le plan l'exige.
