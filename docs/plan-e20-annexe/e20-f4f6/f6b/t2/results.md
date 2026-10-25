# E20.f6b, T2 (2026-10-25) : les tests des flammes de la 309, rouges avant T3

Plan : `docs/plan-e20-heros.md` §1.22.2, T2. HEAD de depart `1dbc17b7`, branche `chantier/theme2-attaques-objets`. Un seul fichier neuf : `Alundra.Tests/Ai/Projectiles/BallFlamesTests.cs` (dossier miroir de `BallAi`, ADR-0080). Aucun code de la DLL.

## Les tests

Le monde reel de la carte 309 par `ArcRun` (arrivee posee sur la tuile (38, 47) comme la sonde P-F6-2 de T0, `RealController`, prefabs reels), registre de production, aucun crochet de test. Les attendus viennent de la cle `flames309_real_pf` (vrai interprete d'octets), lue par `BallOracle.Load`, a 1, 2 et 3 ticks par image (78 ticks ; le tick d'une emission de la cle est ramene a la fin de la trame qui le contient).

- (m1) `M1_TheThreeFlames...EmitTheEffect17EveryFourTicks...` (3 cadences) : les 57 effets 17 (3 flammes x 19 emissions, ticks 5, 9, ..., 77), position, Z 40.000 (32 px + 8), vitesse nulle, dans l'ordre de la cle ; `M1_TheFlamesAreWhereTheKeyHasThem...` (2 cadences) : a chaque trame statut, X, Y, Z (cle - 1 unite brute), forces, drapeaux `0x132104` egaux a la cle, plus le cas positif (des effets existent) ; `M1_WithoutNativeHandlers...` : le temoin « sans C60 » (`NativeHandlersEnabled` faux : aucun effet, le programme A a tourne quand meme, drapeaux `0x132104`) accole au meme monde qui emet 57 effets.
- (m2) `M2_TheFlamesTouchNoEntity...` (3 cadences) : statut Normal, aucun contact, compteur de coups nul a chaque trame (colonnes `hitc` et `touch` de la cle nulles), tous les effets sont l'effet 17 (ni explosion, ni bouffee, ni destruction) ; accole au cas positif (elles emettent).
- (m3) `M3_TheProductionRegistryHoldsC60AndE9...` : le registre de production (`CreateForProduction` et celui d'un `EventProgramRunner`) egale la somme des classes (`BeannoideAi` ... `LiftableAi.RegisterC4Production`) **plus** `BallAi.Register` (+2 clés) ; (C, 60) et (E, 9) tenues pour les types 10 a 15 et aucun autre (0 a 399).

## Rouge avant T3

10 cas (5 tests), **10 rouges, 0 vert** (`red-before-T3.txt`). Les causes sont les bonnes : la liste des effets est vide (aucune cle (C, 60) dans le registre de production), et le compte du registre manque deux cles. Les comparaisons d'etat des flammes (statut, positions, forces, drapeaux `0x132104` de la cle) passent deja avant T3 (le programme A tourne, la phase B n'y change rien) et ne sont rouges que par le cas positif accole.

# E20.f6b, T3 (2026-10-25) : la phase B, l'appel d'enregistrement

Un seul changement de code : `Alundra/Ai/NativeSpriteRegistry.cs`, `CreateForProduction` appelle `BallAi.Register(registry)` apres `RegisterC4Production` (et les deux commentaires de documentation nomment E20.f6b). Seul changement de comportement : les trois flammes de la 309 emettent (m1, m2).

## Vert apres

- `BallFlamesTests` : **10 cas verts sur 10** (`green-after-T3.txt` : (m1) 3 + 2 + 1, (m2) 3, (m3) 1), contre 10 rouges avant.
- **Tests existants deplaces : 4**, exactement la liste predite par T0 (section 5, phase B), mesures en remettant les anciens tests devant le nouveau code (4 rouges, 71 verts sur les 75 des quatre classes) : `Ai.NativeSpriteRegistryTests.ProductionRunner_RegistryHoldsTheHandlersOf...`, `Ai.Objects.LiftableAiRegistrationTests.R1_TheKeyIsInTheSlotCOnly_AndTheCountOfTheProductionRegistryIsTheSumOfTheCountsOfTheClasses`, `Ai.Projectiles.ChargedArrowAiTests.C2_TheProductionRegistry_HoldsTheKeyForTheType7_...`, `Ai.Projectiles.WaveSwordLifeTests.V3_TheProductionRegistryHoldsTheKeysOfItsClasses_...`. Chacun est reecrit **par derivation** : `+ BallAi.Keys.Count` dans le compte litteral, et `BallAi.Register(sum)` dans la somme (R1 et C2 portaient les deux ; C2 a un second compte litteral que le releve de T0 ne distinguait pas du premier : meme test). Aucune valeur collee de la DLL.
- Suites, Release (copie neutralisee) puis Debug (derniere), au premier plan, `--blame-hang-timeout 60s`, quatre partitions disjointes (celles de T1) : 317 + 1 538 + 3 085 + 1 966 = **6 906 tests, zero echec** dans les deux configurations (6 896 a la fin de T1, +10). `check_names.py --rules` : 0 finding, 1 avertissement preexistant. Les fins de ligne des quatre traces `docs/hero-trace-389-*.txt` : reecrites par les suites, `diff --ignore-cr-at-eol` vide, restaurees.
- **Preuve de retrait** (mutation reelle, appliquee par script puis annulee, `git diff` du fichier = le seul changement de T3) : la ligne `BallAi.Register(registry);` retiree de `CreateForProduction`, les classes `NativeSpriteRegistryTests`, `LiftableAiRegistrationTests`, `ChargedArrowAiTests`, `WaveSwordLifeTests`, `BallFlamesTests`, `BallAiTests` et `BallLifeTests` (262 cas) : **14 rouges**, exactement les 10 cas de (m1) a (m3) et les 4 epingles reecrites (`removal-proof.txt`) ; (b7) (`BallAiTests`, sur un registre neuf) et `BallLifeTests` restent verts, comme le plan l'ecrit.
- Ecarts au plan : aucun sur T2 et T3. Le temoin « sans C60 » est le meme monde avec `NativeHandlersEnabled` faux (le registre de production ne permet pas de retirer une seule cle) ; (m1) et (m2) tournent dans le monde reel de la 309 par `ArcRun` (les trois entites du type 12 y sont celles de la carte, programme A reel de la DLL) et non sur des entites faites a la main.
