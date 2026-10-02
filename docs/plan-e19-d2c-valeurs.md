# Annexe d'E19.d2c — valeurs écrites d'avance

Annexe du plan `docs/plan-e19-opcodes.md`, §1.2h.3 (E19.d2c). Elle recopie, sans les retoucher, les tests proposés par les découvertes en lecture seule du 2026-10-02 (surfaces « conception », « valeurs », « regression », « saut-binaire » et « saut-dll »), puis les corrections de leurs contre-vérifications indépendantes. **Règle de lecture** : une valeur de la section C (corrections) l'emporte sur la même valeur des sections A et B ; une valeur que la mesure contredit est un arrêt, jamais une ré-épingle. Positions en unités 16.16 sans le `+1` du binaire (convention de la DLL), sauf mention.

## A. Saut scripté, `0x25`, eau et glace

### A.1 Surface « conception » (UJ, UW)

- UJ-1 PNJ sol plat (montage AlundraNoGravityLandingTests, Gravity raw 128, ZViscosity raw 4096, drapeau Gravity, animation 0 courante, animation 3 vitesse 0 IZF 1360, cible 3 écrite avant la mise à jour 1) : PosZ aux mises à jour 1 à 22 = 348160, 663552, 946176, 1196032, 1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400, 1462272, 1253376, 1011712, 737280, 430080, 90112 ; mise à jour 23 : PosZ 0, ForceZ 0, IsOnGround 1, CollidedWithEntityZ 1 ; IsOnGround 0 aux mises à jour 1 à 22 ; IsZForceApplied 1360 après la 1, 0 après la 2. Rouge aujourd'hui : PosZ reste 0.
- UJ-1b même montage, IZF 1280 : PosZ 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680 ; mise à jour 21 : PosZ 0, atterri (test <= de la DLL), IsOnGround 1, CollidedWithEntityZ 1, ForceZ 0 (le binaire : CollidedWithEntityZ 0 et ForceZ -327680 à la 21, atterri à la 22 : écart épinglé et consigné).
- UJ-2 opcode 0x25 (runner, comme U1 à U4) : programme 01 25 FF, entité logique = propriétaire ; (CollidedWithEntityZ, IsOnGround) = (0,0) : appel 1 trace (0,0x01,1), (1,0x25,0), CodeIndex 1 ; puis (0,1) : (1,0x25,1), fin, CodeIndex 2 ; idem (1,0) ; propriétaire (1,1) et logique (0,0) -> 0 ; propriétaire (0,0) et logique (0,1) -> 1 ; Parameters[1..3], TargetAnimationId, TargetDirection inchangés. Rouge : rend 1 d'emblée.
- UJ-3 héros avec contrôleur (HeroWorldFixture.BuildWorld/BuildHeroPawn), animation 43 vitesse 0 IZF 1280, MapGravity 1250, MaxFallSpeed 800, sol plat : PosZ aux mises à jour 1 à 20 = 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680 ; mise à jour 21 : PosZ 0, pas atterri, IsOnGround 1, ForceZ -327680, CollidedWithEntityZ 0 ; mise à jour 22 : atterri, ForceZ 0, CollidedWithEntityZ 1 ; mises à jour 1 à 21 : IsVerticalOwnedExternally vrai et Settings.Gravity 0 ; après la 22 : faux, 1250, MaxFallSpeed 800 ; PosX et PosY inchangés avec leur fraction conservée à l'atterrissage (pas de troncature).
- UJ-3b héros, 0x1B [0,8] (ForceZ = 524288 posé avant le tick, décroît dès ce tick), gravité 128 : PosZ aux mises à jour 1 à 30 = 491520, 950272, 1376256, 1769472, 2129920, 2457600, 2752512, 3014656, 3244032, 3440640, 3604480, 3735552, 3833856, 3899392, 3932160, 3932160, 3899392, 3833856, 3735552, 3604480, 3440640, 3244032, 3014656, 2752512, 2457600, 2129920, 1769472, 1376256, 950272, 491520 ; mise à jour 31 : 0 (au sol, IsOnGround 1, pas atterri) ; atterri à la 32.
- UJ-4 CollidedWithEntityZ : PNJ au repos avec gravité : 1 après chaque mise à jour ; PNJ au repos sans gravité (F == 0) : 0 et IsOnGround 1 ; en l'air : 0 ; appuyé à la mise à jour n puis en l'air à n+1 : 0 à n+1.
- UJ-5 rattrapage : UJ-1 avec une Update(0.04f) (deux ticks) : PosZ 663552 (une impulsion, pas deux), IsZForceApplied 0 ; sur fixture sans sprite, un test de niveau donnerait 696320 (rouge de référence).
- UJ-6 apparition : préfab réel eab8d775 (flèche Niv.1, animation 0, IZF 256, sans gravité) posé par le chemin d'apparition sur un sol de hauteur h, 10 mises à jour : PosZ reste h, ForceZ 0, IsZForceApplied 0 à chaque tick (rouge avec un crochet naïf : h + 65536*n) ; idem 67b30f8f (IZF 512, gravité). UJ-6b : apparition puis programme A 1A [3] au premier tick (animation 3, IZF 1360) : impulsion appliquée (PosZ 348160 au premier tick).
- UJ-7 arrivée du héros (AdoptPlayerPawn avec animation d'arrivée 2, IZF 1280) : pas d'état en l'air, PosZ inchangé pendant 5 ticks.
- UJ-8 image sans tick : UJ-1 avec une Update(0.001f) sans tick entre l'écriture de la cible et la première mise à jour à tick : PosZ 348160 après le premier tick, avec et sans sprite à horloge. UJ-9 chaîne : A (IZF 0) chaîne vers B (IZF 1360) : impulsion au tick de la fin de A (PosZ 348160 à ce tick puis la liste d'UJ-1). UJ-10 : une impulsion par relancement 0x1C d'une animation à IZF.
- UJ-11 gel et reprise pendant le vol du héros : après la reprise sur image sans tick, IsVerticalOwnedExternally vrai, héros non rabattu (PosZ inchangé), vol repris à la bonne valeur au tick suivant. UJ-12 héros 0x1B [0,8] contre une entité de hauteur 32 px (PosZ requis >= 2097152) : pas XY libre aux mises à jour 5 à 26 (PosZ 2129920 ... 3932160 ... 2129920) et bloqué hors de cet intervalle (XCollisionEntity posé).
- UW-1 eau, héros, hauteur 0, quatre coins sur une case 0x18, niveau 0, animation 1 (vitesse 208, accélération 1) vers l'est depuis l'arrêt : ForceX 79872, 79872, 79872... ; niveau 1 (objet 0x1A) : 79872, 159744, 159744... ; rouge aujourd'hui : 79872, 159744. UW-2 glace (0x20) : 4992, 9984, 14976, ... 159744 à la mise à jour 32 ; arrêt (animation 0 vitesse 0 accélération 1) : 154752, 149760, ... 4992, 0 à la 32 (31 ticks non nuls, 37,78125 px) ; TargetForceX et ForceStepX en cache inchangés. UW-3 PNJ sur eau ou glace : forces identiques au sol plat.
- UW-4 x160 : animation 2 sur case 0x18, niveau 0 : ForceZ 204800 ; PosZ 204800, 376832, 516096, 622592, 696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496, 0 (atterri à la 14, crête 11,375 px) ; niveau 1 : 327680 comme UJ-3. UW-5 : une case d'eau plus basse que le pied (berge) ne ralentit pas ; en l'air VramOR vaut 0.
- A10J (carte 10 B[20], valeurs du binaire eau comprise, DLL sans le +1 ; à rejouer sous les contacts d'E19.d2b, couloir sans entité) : @2441 rend à F0+50 ; PosX 16594944 à F0+51 puis +79872 par image jusqu'à 18032640 à F0+69 puis +159744 ; @2447 rend la main et @2451, @2453 partent à F0+158 en (32249856, 56016896, 0) ; PosZ de F0+159 à F0+176 = 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 1048576 ; PosX F0+159, +160, +161 = 32409600, 32564736, 32715264 puis +150528 jusqu'à 34973184 à F0+176 ; animation 44 dès F0+160 ; 0x37 rend à F0+162 ; 0x25 @2455 s'exécute de F0+162 à F0+176 (0 les 14 premières fois, 1 à F0+176) ; @2456 part à F0+176 en (34973184, 56016896, 1048576) et rend à F0+256 en 47748096. Giles : dérivé, non rejoué, +-1 tick.
- Non-régression : A3 (chiens des rec 3 et 4 de la 478 : impulsion 196608, crête 10,5 px) remesuré d'avance ; A10 de la 165 et TA10v : retirer les 0x25 des listes d'ignorés et remesurer les images d'avance ; traces d'or de l'intro et du héros inchangées ; tout autre écart d'épingle est un arrêt.

### A.2 Surface « valeurs » (A10J, A10, A3, A12, chaîne)

- A10J (arc 10 B[20], ArcSpec("A10J","Overworld","Overworld 2,1-10",{1654,203},0,0,0,limite ~F0+330,RealController,Prefabs,Arrival: ArcArrival(16515072,61341696,0,ResetAnimationId,16)), F0 = première exécution de `0x0B @2441`, opcodes sautés attendus : `0x90 @6406` seulement) HÉROS : `@2441` rend et `@2445`/`@2447` à F0+50 en (16515072 ; 56070144 ; 0) ; PosX F0+51 16594944 puis +79872 par image jusqu'à 18032640 à F0+69, puis +159744 ; `@2447` rend, `@2451`, `@2453` à F0+158 en (32249856 ; 56016896 ; 0), TargetAnimationId 2 après `@2451` ; PosZ F0+159..F0+175 : 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112 ; PosX F0+159 32409600, F0+160 32564736, F0+161 32715264 puis +150528 jusqu'à 34973184 à F0+176 ; TargetAnimationId 44 dès F0+160 ; IsOnGround 0 de F0+159 à F0+175 ; `37` rend à F0+162 ; `0x25 @2455` s'exécute F0+162..F0+176 (15 fois, 14 rendent 0), rend 1 à F0+176 avec CollidedWithEntityZ 1, IsOnGround 1, ForceZ 0, PosZ 1048576 ; `@2456` premier appel F0+176 en (34973184 ; 56016896 ; 1048576), rend à F0+256 en (47748096 ; 56016896 ; 1048576) ; `@2460`/`@2462` à F0+256 ; `0x24 @2462` rend dans [F0+295 ; F0+299] avec le héros à ± 2,5 px de (47827968 ; 51838976) (DLL, PosZ 2097152, ForceAdjusted 1), l'original étant F0+308 en (47877120 ; 50790400) ; puis `0x53 @2463` vers 135 en (30670848 ; 54001664 ; 1048576), direction 16 ; EntityBlockCount 0, XCollisionEntity null. Arrivée réelle (animation 1) : toutes les PosY moins 79872 unités et `@2462` une image plus tôt. Variante sans l'eau : F0+149, F0+167, F0+247 (mêmes positions).
- A10J GILES (même arc, C[74], DLL dérivé, tolérance ± 1 image et ± 1 px) : `@6385` premier appel F0+1 en (16515072 ; 56098816), rend à F0+101 en x = 32329728 ; `1A [3]`/`37 [3]` F0+101 ; PosZ de F0+101 à F0+118 : 348160, 663552, 946176, 1196032, 1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400, 1462272, 1253376 puis 1048576 à F0+119 ; PosX F0+101 32489472, F0+102 32618496, F0+103 32716800 puis +98304 par image ; `37` rend à F0+105 ; `0x25 @6393` s'exécute 15 fois (F0+105..F0+119) et rend 1 à F0+119 (relationnel : à l'image d'atterrissage ou la suivante) ; `@6394` premier appel F0+119 en (34191360 ; 56098816 ; 1048576), rend à F0+206 en x = 48027648 ; `0x24 @6400` rend ~F0+258 à y ≈ 775 (± 2,5 px) ; `0x19` ~F0+268, détruit ~F0+269 ; aucune troncature de X/Y à l'atterrissage (PosX non entier en pixels).
- UJ-1 (PNJ, sol plat 0 : ContactWorld.BuildWorld(new FlatGroundField{GroundZ=0},null) + ContactHost + ContactWorld.AddEntity(...,200,100,0,-10,-7,0,20,14,32) ; Flags|=Gravity, MapGravityRaw 128, MapZViscosityRaw 4096, AnimSetsByAnim {0 : vitesse 0 ; 3 : vitesse 0, IsZForceApplied 1360}, Current=Target=0, ResyncControllerFromFlags, une mise à jour de calage, puis TargetAnimationId = 3 ; aucun sprite) : PosZ après les mises à jour 1 à 23 : 348160, 663552, 946176, 1196032, 1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400, 1462272, 1253376, 1011712, 737280, 430080, 90112, 0 ; ForceZ 348160 puis -32768 par mise à jour, 0 à la 23 ; IsOnGround 0 de 1 à 22 puis 1 ; CollidedWithEntityZ 0 de 1 à 22 puis 1 ; IsZForceApplied 1360 après la 1, 0 après la 2. Rouge aujourd'hui : PosZ reste 0.
- UJ-2 (opcode `0x25`, montage de AlundraEventProgramRunnerWaitForceAdjustedTests : NewDocument(0x01,0x25,0xFF), NewRunner, StateFor, RunOneScriptCall) : propriétaire = logique, (CollidedWithEntityZ, IsOnGround) = (0,0) : appel 1 trace (0,0x01,1),(1,0x25,0), CodeIndex 1 ; puis (0,1) : (1,0x25,1),(2,0xFF,0), CodeIndex 2 ; idem (1,0) et (1,1) ; lit l'entité logique (propriétaire (1,1) et logique (0,0) continue ; propriétaire (0,0) et logique (0,1) rend) ; aucun effet de bord après 10 appels (Parameters[1..3], TargetAnimationId, TargetDirection inchangés). Rouge : rend 1 d'emblée.
- UJ-3 (héros en l'air, sol plat : montage d'AlundraLadderClimbTests : AlundraPlayerController{PadStateProviderForTests}, hôte joueur avec GameState.PlayerControlFlags = ControlLocked, HeroWorldFixture.BuildWorld(champ plat synthétique TileMapData/AlundraCells) + BuildHeroPawn avec LoadHeroControllerSettings, MapGravity 1250, MapMaxFallSpeed 800, MapGravityRaw 128, MapZViscosityRaw 4096 posés comme AdoptPlayerPawn, animation 43 : vitesse 0, accélération 1, IZF 1280) : PosZ après les mises à jour 1 à 20 : 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680 ; mise à jour 21 : 0, non atterri (PosZ+F == T, test strict), IsOnGround 1, ForceZ -327680, CollidedWithEntityZ 0 ; mise à jour 22 : atterri, ForceZ 0, CollidedWithEntityZ 1 ; mise à jour 23 : CollidedWithEntityZ 0 ; mises à jour 1..21 : IsVerticalOwnedExternally vrai, Settings.Gravity 0 ; après la 22 : faux, 1250, MaxFallSpeed 800 ; PosX/PosY inchangés ; avec `<=` l'atterrissage passerait à la mise à jour 21.
- UJ-4 (CollidedWithEntityZ effacé à chaque tick, montage d'UJ-1) : PNJ au repos : 1 après chaque mise à jour ; en l'air : 0 ; soutenu à n puis en l'air à n+1 : 0 à n+1.
- UJ-5 (rattrapage : UJ-1 avec Update(0.04f), deux ticks par appel) : PosZ après les appels 1, 2, 3 : 663552, 1196032, 1597440 (une seule impulsion) ; une accroche à niveau donnerait 696320, 1294336, 1761280 (rouge de garde).
- UJ-6 (pas d'impulsion à l'apparition, préfabs réels par le chemin de spawn de production, arc Prefabs sur la 382/386 pour eab8d775 et sur la 309 pour 67b30f8f) : flèche (256, sans gravité) : PosZ = h, ForceZ 0, IsZForceApplied 0 à chacun des 10 premiers ticks (rouge avec l'accroche naïve : h + 65536·n) ; boule de feu (512, gravité) : PosZ = h (rouge : h + 131072 au tick 1 puis la décroissance) ; grille -32768 sans gravité : ForceZ 0 à chaque tick.
- UJ-7 (nouveau, fenêtre de l'aimantation) : programme d'entité (créneau C) `1A [3]`,`37 [3]`,`25`,`1A [0]` sur le montage d'UJ-1 : `1A [3]` à l'image s (mise à jour 1 = s), `37` rend à s+4, `0x25` s'exécute de s+4 à s+22 (19 exécutions) et rend 1 à s+22 (pied 1,375 px, aimantation) ; épingler l'intervalle [s+22 ; s+23] ; variante programme de carte (entité logique = ce PNJ, `43 [n]`) : `0x25` rend à la fin de l'image du 23e tick.
- UW-1 à UW-5 (même montage que UJ-3 sur un TileMapData synthétique : cases walkability 24 (0x18) ou 32 (0x20), hauteur 0, une mise à jour de calage au repos pour poser CombinedVramFlagsOR) : UW-1 eau niveau 0, animation 1 (vitesse 208, accélération 1) direction 24 depuis le repos : ForceX après les ticks 1 à 4 = 79872, 79872, 79872, 79872 ; niveau 1 (GameState.NumberOfItems[0x1A]=1) : 79872, 159744, 159744, 159744 (rouge : 79872, 159744) ; UW-2 glace : 4992·k jusqu'à 159744 au tick 32 ; arrêt (animation 0) : 154752, 149760, …, 4992, 0 au tick 32 (31 ticks non nuls, 37,78125 px), TargetForceX et ForceStepX conservés ; UW-3 PNJ sur eau/glace : 159744 dès le tick 1 (identique au sol plat) ; UW-4 x160 (animation 2, case 0x18, niveau 0) : ForceZ 204800 ; PosZ 204800, 376832, 516096, 622592, 696320, 737280, 745472 (sommet 11,375 px), 720896, 663552, 573440, 450560, 294912, 106496, 0 (mise à jour 14, atterri) ; niveau 1 : valeurs d'UJ-3 ; UW-5 héros sur une case de 16 px à côté d'une case d'eau de 0 px : pas de bit d'eau, forces 79872 puis 159744.
- A3 (478, invariance) : A3_TheVision... inchangé ; garde statique : exactement deux écritures d'animation à impulsion sur la 478 (`C[3] @329`, `C[4] @345`, animation 12, IZF 768), rec3 et rec4 non collidables ; épingle optionnelle : à l'image de `@329` (mise à jour 1 = cette image), PosZ de rec3 au-dessus de son repos 196608, 360448, 491520, 589824, 655360, 688128, 688128, 655360, 589824, 491520, 360448, 196608, 0 puis atterri (14e mise à jour) ; mêmes valeurs pour rec4 à l'image de `@345` ; IsZForceApplied 768 à cette image seulement.
- A10 (165) ré-épinglé : `0x25 @838` premier appel à l'image 484 et rend à 503 (20 exécutions), `@839` et `@841` à 503, `0x25 @843` premier appel 506, rend à 525 (20 exécutions), `@844`/`@846`/`0x06 @847` à 525 ; PosZ de Bergus aux images 481..502 puis 503..524 : la liste d'UJ-1 jusqu'à 90112 (deux fois), 0 à 525 ; arêtes T104 effacé 526, T102 posé 527 et effacé 619, T105 posé 620 et effacé 775 ; fin `0x11 @354` 960 ± 3 ; AssertSkippedWithin sur l'ensemble vide ; ordre T101,T102,T103,T104,T102,T105 et effaceurs inchangés ; limite d'images relevée à 1100.
- A12 (179 B[2], pour E19.e) : à partir de F = image de `1A [2] @411` : `37 [3] @413` rend à F+4, `0x25 @415` s'exécute de F+4 à F+23 (20 exécutions) et rend à F+23, `1A [0] @416` à F+23 ; la suite de B[2] (boîtes, G1652, `0x53 @451`) +19 images ; rec8 PosZ : liste d'UJ-1 à partir de F+1, atterri à F+23.
- Test statique de chaîne : sur les programmes de la chaîne (179 B[1], B[2], B[3], C[8] ; 176 B[6], B[7] ; 10 B[20], C[74] ; 135 ; 178 ; 185 ; 162/164/165), les écritures d'animation à impulsion sont exactement {179 B[2] @411, 179 C[8] @1110, 10 B[20] @2451, 10 C[74] @6389, 165 C[3] @834/@839/@861, 165 C[8] @1162} ; les `0x25` atteignables sont ceux listés ; IntroTraceHarnessTests.ImplementedOpcodes complété de 0x25 (ses commentaires disent qu'il est comparé aux labels de Dispatch).

### A.3 Surface « regression » (TR)

- TR-G garde d'octets : git diff --exit-code sur docs/hero-trace-389-spawn-freestep.txt, spawn-fixedstep, highground-freestep, highground-fixedstep, intro-trace-389.txt, intro-programs-389.txt -> 0 après chaque tâche ; épingles 99, 40, 221, 232, 1704 inchangées.
- TR-A10 : AssertFrame(B, 354) = 960 (± 3, couvre 962) ; 0x25 @838 : 20 exécutions (19 retours 0, 1 retour 1) de t0+3 à t0+22 dans la DLL (t0+23 binaire) ; 0x25 @843 de t0+25 à t0+44 ; 06 @847 à t0+44 au lieu de t0+6 ; SkippedOrExceeded sans 0x25 ; positions 0x64, T100 @180, ordre T101,102,103,104,102,105 et T-REG-0 = 0 inchangés.
- TR-A3 : chiens rec 3 et 4 : une impulsion chacun, hauteurs 196608, 360448, 491520, 589824, 655360, 688128, 688128, 655360, 589824, 491520, 360448, 196608, 0 ; épingles d'A3 inchangées (frames 301, 856, 1368, 1624, 2008, 2262 ; gaps 555, 512, 256, 384 ; Ronan 26, 25, 66) ; T-REG-0 = 0 ; AssertNothingSkippedOrExceeded.
- TR-V bouquets (T-A10v) : rec 69 et 81 sans gravité, 8 px au-dessus du sol au lâcher : PosZ - sol aux ticks t, t+1, t+2... = 491520, 458752, 425984... (moins 32 768 par tick) tant que le moteur ne les a pas aimantés ; 0x25 @4741 et @5545 rendent 1 entre t+8 et t+16 ; puis PosZ - sol = 0 et ForceZ = 0 ; aucun bouquet suspendu à la fin ; épingles de T-A10v (T666 image 1160, 0x53 @2003 image 5100, limite 6500) inchangées.
- UJ-6 apparition : préfabs Flèche Niv.1 (anim 0, IZF 256, sans gravité) et Boule de feu chargée (512, avec gravité) par SpawnEntityByRecordId : PosZ constant, ForceZ 0, IsZForceApplied 0 à chaque tick n = 1 à 10 ; héros arrivé avec l'animation 2 puis 62 : PosZ constant pendant 5 ticks.
- UJ-8 relance 0x1C : entité à IZF 1360 sur l'anim 3, Hold levé, 0x1C relance : seconde impulsion, PosZ après les mises à jour suivantes = 348160, 663552, 946176, 1196032... (liste de UJ-1) ; sans drapeau explicite : 0.
- UJ-9 marqueur 0x8000 : PNJ sans gravité, anim 3 (IZF 256) puis chaîne vers l'anim 0 (IZF -32768) : ForceZ 65536 pendant la montée, 0 au tick de la chaîne (jamais -8388608) ; avec gravité : -8388608 conservé tel que le binaire.
- TR-H héros sans état en l'air : héros à contrôleur et PlayerController marchant 120 ticks sur la 389 : PosX, PosY, PosZ, IsOnGround, ForceAdjusted identiques tick pour tick avant et après le port ; héros sans contrôleur : IsOnGround posé à la main jamais réécrit.
- TR-B bottes paresseuses : AlundraPlayerManager.Tick sur un proxy nu (ScriptHost nul) avec CombinedVramFlagsOR = 0x08 : pas d'exception, niveau 0, ForceX 79872 puis 79872 ; avec VramOR = 0 : 79872 puis 159744.
- TR-S statique : l'ensemble des écritures IZF par script sur les cartes du banc égale la liste de R3 ; l'ensemble des 0x25 atteignables égale 10 @2455/@4741/@5545/@6393, 44 @262/@387/@2214/@2228, 135 @2483, 165 @838/@843/@865/@1166, 172 @610, 174 @203, 179 @415/@1114, 181 @207, 362 @359 ; aucun 0x25 de la chaîne n'est sauté.
- TR-P propriétaire de gravité : héros en l'air, puis montée d'échelle, puis départ de portail annulé : chacun restitue les valeurs vivantes d'entrée, y compris Gravity = 0 posé par un 0x17 antérieur.

## B. Saut à la manette, chutes, dessus d'objets

### B.1 Surface « saut-binaire » : scénarios exécutés sur le vrai code (S-A à S-L, UH)

```text
## S-A standing jump (Idle, no stick, Cross edge at tick 1), flat ground
hero at rest; map 10 constants gravity 128, ZViscosity 4096; animations: 0x2B at tick 1 (IZF 1280), 0x2D from tick 2, Idle at tick 22
Current animation per tick : [43, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 0, 0, 0]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680, 0, 0, 0]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0, 0, 0, 0]
ForceX per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]

## S-B jump walking east (steady Moving, stick Right held all along, Cross edge at tick 1)
ForceX at tick 0 = 159744 (steady walk); animation 2 at tick 1, 44 (speed 196) from tick 2, Moving (1) at tick 22
Current animation per tick : [2, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 1, 1, 1]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680, 0, 0, 0]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0, 0, 0, 0]
ForceX per tick           : [159744, 155136, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 155136, 159744, 159744]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1218048, 1368576, 1519104, 1669632, 1820160, 1970688, 2121216, 2271744, 2422272, 2572800, 2723328, 2873856, 3024384, 3174912, 3330048, 3489792, 3649536]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]

## S-B2 jump east, stick released at tick 8
nibble 0 from tick 8: anim 0x2D (speed 0), ForceX halves then 0
Current animation per tick : [2, 44, 44, 44, 44, 44, 44, 45, 45, 45, 45, 45, 45, 45]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632]
ForceX per tick           : [159744, 155136, 150528, 150528, 150528, 150528, 150528, 75264, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1142784, 1142784, 1142784, 1142784, 1142784, 1142784, 1142784]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]

## S-B3 jump east, stick reversed to Left at tick 6
direction 8 from tick 6: ForceX 0 at tick 6, -150528 from tick 7
Current animation per tick : [2, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472]
ForceX per tick           : [159744, 155136, 150528, 150528, 150528, 0, -150528, -150528, -150528, -150528, -150528, -150528]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [159744, 314880, 465408, 615936, 766464, 766464, 615936, 465408, 314880, 164352, 13824, -136704]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]

## S-I standing jump, stick Right pressed in the air from tick 5
ForceX 75264 at tick 5 then 150528
Current animation per tick : [43, 45, 45, 45, 44, 44, 44, 44, 44, 44]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240]
ForceX per tick           : [0, 0, 0, 0, 75264, 150528, 150528, 150528, 150528, 150528]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0, 75264, 225792, 376320, 526848, 677376, 827904]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]

## S-B4 jump walking Down (dir 0), steady ForceY 106496
ForceY 106496, 103424, 100352 ... then 103424, 106496 after landing
Current animation per tick : [2, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 1, 1, 1]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680, 0, 0, 0]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0, 0, 0, 0]
ForceX per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [106496, 103424, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 100352, 103424, 106496, 106496]
PosX - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosY - start (16.16)      : [106496, 209920, 310272, 410624, 510976, 611328, 711680, 812032, 912384, 1012736, 1113088, 1213440, 1313792, 1414144, 1514496, 1614848, 1715200, 1815552, 1915904, 2016256, 2116608, 2220032, 2326528, 2433024]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]

## S-F chained jump (second Cross edge at tick 22)
tick 21: PosZ 0, IsOnGround 1, not landed; tick 22: StartJump again, PosZ 327680
Current animation per tick : [43, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 43, 45, 45, 45, 45]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680, 327680, 294912, 262144, 229376, 196608]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0, 327680, 622592, 884736, 1114112, 1310720]
ForceX per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]

## S-C jump east over a 16 px step (terrain cells from x=144), hero x0=120, Cross edge at tick 1, stick held
front edge enters the 16 px cells at tick 6 (PosZ 1474560); landing on the step at tick 18 (PosZ 1048576, CollidedWithEntityZ 1); Moving from tick 19
Current animation per tick : [2, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 44, 1, 1, 1, 1, 1, 1, 1, 1]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576]
ForceX per tick           : [159744, 155136, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 155136, 159744, 159744, 159744, 159744, 159744, 159744, 159744]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1218048, 1368576, 1519104, 1669632, 1820160, 1970688, 2121216, 2271744, 2422272, 2572800, 2723328, 2878464, 3038208, 3197952, 3357696, 3517440, 3677184, 3836928, 3996672]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1]

## S-E walk east off a 16 px ledge (hero x0=120 on the ledge, edge at x=144), stick held, no Cross
last grounded tick 14 (all four corners over the low cells at tick 14, no 3 px snap: drop 16 px); fall animation 44 from tick 15; landed at tick 22; Moving at tick 23
Current animation per tick : [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 44, 44, 44, 44, 44, 44, 44, 44, 1, 1, 1, 1]
ForceZ per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, 0, 0, 0, 0, 0]
PosZ (DLL, no +1) per tick: [1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1048576, 1015808, 950272, 851968, 720896, 557056, 360448, 131072, 0, 0, 0, 0, 0]
ForceX per tick           : [159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 159744, 155136, 150528, 150528, 150528, 150528, 150528, 150528, 150528, 155136, 159744, 159744, 159744]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [159744, 319488, 479232, 638976, 798720, 958464, 1118208, 1277952, 1437696, 1597440, 1757184, 1916928, 2076672, 2236416, 2391552, 2542080, 2692608, 2843136, 2993664, 3144192, 3294720, 3445248, 3600384, 3760128, 3919872, 4079616]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1]
CollidedWithEntityZ       : [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1]

## S-D standing jump from 0x18 water cells, boots level 0 (x160)
VramOR 0x18 at rest; ForceZ 204800 at tick 1; landed at tick 14
Current animation per tick : [43, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 0, 0]
ForceZ per tick           : [204800, 172032, 139264, 106496, 73728, 40960, 8192, -24576, -57344, -90112, -122880, -155648, -188416, 0, 0, 0]
PosZ (DLL, no +1) per tick: [204800, 376832, 516096, 622592, 696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496, 0, 0, 0]
ForceX per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]

## S-D2 same with boots level 1
ForceZ 327680 at tick 1 (as on dry ground)
Current animation per tick : [43, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 45, 0, 0, 0]
ForceZ per tick           : [327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680, 0, 0, 0]
PosZ (DLL, no +1) per tick: [327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0, 0, 0, 0]
ForceX per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
PosY - start (16.16)      : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
IsOnGround after tick     : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1]
CollidedWithEntityZ       : [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1]

## S-G Cross edge at tick 1 on cells with ground_property 0x40 (VramOR & 0x4000)
no jump: animation stays Idle, PosZ stays 0
Current animation per tick : [0, 0, 0, 0]
ForceZ per tick           : [0, 0, 0, 0]
PosZ (DLL, no +1) per tick: [0, 0, 0, 0]
ForceX per tick           : [0, 0, 0, 0]
ForceY per tick           : [0, 0, 0, 0]
PosX - start (16.16)      : [0, 0, 0, 0]
PosY - start (16.16)      : [0, 0, 0, 0]
IsOnGround after tick     : [1, 1, 1, 1]
CollidedWithEntityZ       : [1, 1, 1, 1]
```

Tests proposés par la surface :

- UH-1 (sauts du héros par la manette, valeurs du vrai code, convention DLL sans +1, carte 10 : Gravity raw 128, ZViscosity raw 4096, sol plat, héros au repos, un tick par image) saut sur place : Croix au tick 1, aucune touche. Animation courante : 0x2B au tick 1, 0x2D aux ticks 2 à 21, Idle (0) au tick 22. ForceZ : 327680, 294912, 262144, 229376, 196608, 163840, 131072, 98304, 65536, 32768, 0, -32768, -65536, -98304, -131072, -163840, -196608, -229376, -262144, -294912, -327680 (ticks 1 à 21), 0 au tick 22. PosZ aux ticks 1 à 20 : 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680 ; tick 21 : PosZ 0, IsOnGround 1, CollidedWithEntityZ 0 (pas atterri) ; tick 22 : PosZ 0, ForceZ 0, CollidedWithEntityZ 1. IsOnGround 0 aux ticks 1 à 20. PosX et PosY inchangés. IsZForceApplied 1280 au tick 1, 0 ensuite. Rouge aujourd'hui : aucun saut à la manette.
- UH-2 saut en marchant vers l'est (Moving en régime, ForceX initial 159744, croix à droite tenue tout du long, Croix au tick 1) : animation 2 au tick 1, 44 aux ticks 2 à 21, Moving (1) au tick 22 ; ForceZ et PosZ comme UH-1. ForceX : 159744, 155136, 150528 (ticks 1 à 3), 150528 jusqu'au tick 21, 155136 (tick 22), 159744 (ticks 23 et suivants). PosX moins départ (16.16) : 159744, 314880, 465408, 615936, 766464, 916992, 1067520, 1218048, 1368576, 1519104, 1669632, 1820160, 1970688, 2121216, 2271744, 2422272, 2572800, 2723328, 2873856, 3024384, 3174912 (tick 21), 3330048 (22), 3489792 (23), 3649536 (24) ; PosY inchangé. Variante vers le bas (dir 0, ForceY 106496) : ForceY 106496, 103424, 100352 (ticks 1 à 3), 100352 jusqu'au 21, 103424 (22), 106496 (23).
- UH-3 contrôle en l'air : (a) croix relâchée au tick 8 pendant UH-2 : animation 44 aux ticks 2 à 7 puis 45 ; ForceX 150528 (7), 75264 (8), 0 (9) ; PosX moins départ 1067520 (7), 1142784 (8 et suivants) ; (b) inversion vers la gauche (direction 8) au tick 6 : ForceX 150528 (5), 0 (6), -150528 (7 et suivants) ; PosX moins départ 766464 (5), 766464 (6), 615936 (7), 465408 (8), 314880 (9) ; (c) saut sur place, croix à droite pressée au tick 5 : animation 45 aux ticks 2 à 4 puis 44 ; ForceX 0 (ticks 1 à 4), 75264 (5), 150528 (6 et suivants) ; PosZ inchangé dans les trois cas.
- UH-4 bord de Croix : (a) en l'air (Croix aux ticks 1 et 5) : le tick 5 ne change rien ; (b) rebond : Croix aux ticks 1 et 22 : au tick 21 PosZ 0 et IsOnGround 1 (pas atterri), au tick 22 animation 0x2B de nouveau, ForceZ 327680, PosZ 327680, CollidedWithEntityZ 0 ; (c) Croix tenue seule après atterrissage : pas de nouveau saut (il faut un nouveau bord) ; (d) Croix et Carré au même tick : pas de saut (l'attaque passe avant, test à écrire quand PlayerTryAttack sera porté, sinon documenter).
- UH-5 case sans saut : héros au repos sur des cases dont ground_property & 0x40 (VramOR & 0x4000), Croix au tick 1 : animation Idle, PosZ 0 aux ticks 1 à 4, ForceZ 0, IsOnGround 1. Idem avec croix à droite : pas de passage en animation 2.
- UH-6 marche de 16 px (cases de 16 px de haut dès x = 144 px, héros en marche vers l'est à x0 = 120 px, bord avant à 131 px, croix à droite tenue, Croix au tick 1) : PosZ aux ticks 1 à 17 : 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112 ; tick 18 : PosZ 1048576, ForceZ 0, CollidedWithEntityZ 1, IsOnGround 1 (atterri sur la marche) ; ticks 19 et suivants : 1048576. Animation 44 jusqu'au tick 17, Moving au 19. PosX moins départ : 2572800 (17), 2723328 (18), 2878464 (19), 3038208 (20). Le bord avant entre dans les cases au tick 6. Variantes : x0 = 125 (8 px) : passe sans être arrêté, x finale au tick 30 = 195,734 ; x0 = 126 (7 px) : arrêté à x = 133,0 aux ticks 2 et 3 (ForceX conservé), passe au tick 4, x finale au tick 30 = 196,633, identique pour x0 = 127 à 132. Test de dépassement : saut qui n'a pas PosZ >= 1048576 quand le coin entre est arrêté.
- UH-7 chute d'un rebord (héros en marche à x0 = 120 sur des cases de 16 px dont le bord est à x = 144, PosZ 1048576, croix à droite tenue, aucune Croix) : ticks 1 à 14 au sol (PosZ 1048576, animation Moving, IsOnGround 1 jusqu'au 13, 0 au 14 car les quatre coins sont au-dessus des cases basses, PosX moins départ au 14 = 2236416) ; tick 15 : animation 44, ForceZ -32768, PosZ 1015808 ; PosZ 950272, 851968, 720896, 557056, 360448, 131072 (ticks 16 à 21) ; tick 22 : PosZ 0, atterri (CollidedWithEntityZ 1, IsOnGround 1) ; tick 23 : Moving. ForceX 155136 (15), 150528 (16 à 22), 155136 (23), 159744 (24). Une descente de 3 px ou moins ne tombe pas (recollage), une de 16 px tombe.
- UH-8 eau et bottes : saut sur place depuis des cases de walkability 0x18 sans bottes : ForceZ 204800 au tick 1 ; PosZ 204800, 376832, 516096, 622592, 696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496 (ticks 1 à 13), tick 14 : PosZ 0, atterri, IsOnGround 1 ; VramOR 0x18 au repos, 0 en l'air, 0x18 de nouveau au tick 14 ; avec bottes niveau 1 (objet 0x1A) : mêmes valeurs qu'UH-1. Marche : eau sans bottes ForceX 79872 constant, avec bottes niveau 1 : 79872 puis 159744 ; glace : 4992 par tick, 154752 au tick 31, 159744 au 32 ; en l'air sur glace, ForceX 159744, 155136, 150528.
- UH-9 images sans tick et rattrapage (cible du défaut F16) : (a) Update(0.001f) sur l'image du bord de Croix (aucun tick) puis Update(0.02f) : le saut part au premier tick (animation 0x2B, PosZ 327680, ForceZ 327680) et n'est pas écrasé par Idle ; (b) Update(0.04f) (deux ticks) sur l'image du bord : une seule impulsion, PosZ 622592 après les deux ticks (pas 655360), et avec décision par tick l'animation courante est 0x2D au second tick ; (c) bord de Croix pressé pendant un menu ouvert (gel) puis menu fermé : aucun saut, et le verrou est vide ; (d) Croix pressée sous ControlLocked/MessageBox/ForcedSequence : aucun saut au retour du contrôle.
- UH-10 dessus d'entité (coffre 24x16x16 en x 144..168, y 92..108, héros en marche à x0 = 120 sur sol plat, croix à droite tenue, Croix au tick 1) : mêmes valeurs qu'UH-6 : atterrissage au tick 18, PosZ DLL 1048576, ForceZ 0, CollidedWithEntityZ 1, IsOnGround 1 ; il marche sur le coffre et en tombe : IsOnGround 0 au tick 25, animation 44 au tick 26, ForceZ -32768, PosZ 1015808 (chute de 16 px en 8 ticks). Sans saut : le héros est arrêté à x = 133,0 dès le tick 6 avec XCollisionEntity posé. UH-11 : RidingEntity du héros vaut le coffre tant qu'il repose dessus (opcode 0x3E rend 1).
- UH-12 plafond (facultatif, rare) : dalle collidable de 8 px de haut dont le bas est à 40 px, saut sur place d'un héros de 32 px : PosZ 327680 (tick 1), 524288 (tick 2, ForceZ 0, CollidedWithEntityZ 1), 491520, 425984, 327680, 196608, 32768 (ticks 3 à 7), atterri au tick 8.
- UH-13 non-régression : hero Idle et Moving sur sol plat inchangés (épingles du héros) ; pas de pose de saut en marchant sur une rampe ou une marche de 3 px (IsOnGround vaut 1) ; les épingles d'arcs de la conception (A10, TA10v, A3) bougent comme annoncé et rien d'autre ; les cartes hors chaîne 61, 62, 63, 64, 66, 68, 329 : un héros relâché en animation 44 après un saut scripté revient à Idle/Moving dès qu'il est au sol (le gestionnaire des états de saut le fait).

### B.2 Surface « saut-dll » (SJ, traces de référence)

#### B.2.1 (notes §4.1) Valeurs prévues des traces d'or (modèle calibré)

`trace_predict.py` rejoue la cinématique de `RunOneKinematicTick` sur les lignes des fichiers : le **modèle de l'ancien comportement redonne chaque `posX` des 267 lignes (0 écart)** ; le nouveau comportement donne, pour `spawn-freestep` et `spawn-fixedstep` (mêmes `dllTicks`) :

| Image | `isOnGround` | `targetAnim` ancien -> prévu | `posX` ancien -> prévu |
|---|---|---|---|
| 220 | 1 | 1 -> 1 | 40132608 inchangé |
| **221** | 0 | 1 -> **44** | 40292352 -> **40287744** (-4608) |
| 222 | 0 | 1 -> 44 | 40452096 -> 40438272 (-13824) |
| 223 | 0 | 1 -> 44 | 40611840 -> 40588800 (-23040) |
| 231 | 0 | 1 -> 44 | 41889792 -> 41793024 (-96768) |
| **232** | 1 | 1 -> 1 | 42049536 -> 41948160 (-101376) |
| 233 à 267 | 1 | 1 -> 1 | ancien - 101376 |

Mécanisme : en l'air, vitesse 196 (cible 150528) au lieu de 208 (159744) ; au retour au sol (image 232) la force remonte en deux ticks (155136, 159744). 47 lignes changent, `posZ`, `isOnGround`, `tileZ`, `forceAdjusted`, `targetDir`, `cellSlope`, `cellHeight` ne bougent pas.
`highground-*` : aucune ligne en l'air, 117 lignes `targetAnim` 1 : **octets identiques** ; `intro-trace-389.txt`, `intro-programs-389.txt` : le banc d'intro n'appelle pas `MovePlayer`.


#### B.2.2 (notes §7) Tests proposés (valeurs écrites d'avance ; modèle, à re-dériver à l'écriture ; PosZ en 16.16 sans le `+1` du binaire)

Séquence de J (proposition à relire avec § 11 de `conception.md`) : **J4a** états de `MovePlayer` + cachet + gardes 2-4 (SJ-1 à SJ-4, SJ-8) ; **J4b** vol de la manette en monde réel (SJ-5 à SJ-7, SJ-9) ; **J4c** appui d'entité (SJ-12, SJ-13) ; garde d'octets sur six fichiers après chaque tâche.

- **SJ-1 `MovePlayer` direct, `IsOnGround = 1`, sans contrôleur** : (a) Idle, Croix front (`ButtonsHold = ButtonsJustPressed = Cross`) -> **0x2B**, `TargetDirection` inchangée ; (b) Moving, Droite + Croix front -> **2**, direction `0x18` ; (c) Idle, Croix tenue sans front -> 0 ; (d) Idle, Croix front, `CombinedVramFlagsOR = 0x4000` -> 0 ;
  Moving, pad nul, Croix, `0x4000` -> reste 1 (fin sans changer l'animation) ; (e) Carré et Croix la même image près d'un PNJ à `InteractRequiresButton` -> Idle, `ActiveCollisionEntity` posé, pas de saut ; PNJ à contact automatique -> animation inchangée ; (f) `ControlLocked` + Croix -> inchangé ; avec le drapeau de débogage -> `0x2B`.
- **SJ-2 en l'air** (`HeroAirborne = true`) : cible 2, `0x2B`, `0x2C`, `0x2D`, 1 ou 0 ; Droite tenue -> **0x2C**, direction `0x18` ; rien -> **0x2D** ; direction changée en l'air (Gauche) -> `0x08` à l'image même.
- **SJ-3 atterrissage** (`IsOnGround = 1`, cible 0x2C ou 0x2D) : Droite -> 1 ; rien -> 0 ; Croix front -> `0x2B` ; Droite + Croix -> 2 ; Triangle tenu -> inchangé (non porté).
- **SJ-4 cachet** : cible 2, `JumpStartTickStamp == MotionTickCount`, `IsOnGround = 1`, pad nul -> **reste 2** ; après un `Tick(player, 1)` -> **0**. Rouge avec une version sans cachet.
- **SJ-5 monde réel, 1 tick par image** (`HeroWorldFixture`, sol plat, gravité 128, ZViscosity 4096, hôte réel) : image 1 Croix front : cible **43** ; PosZ après chaque image 1 à 20 : 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560,
  1310720, 1114112, 884736, 622592, 327680 ; image 21 : 0 (pas atterri), `IsOnGround` 1 ; image 22 : atterri, `CollidedWithEntityZ` 1, `ForceZ` 0, `IsVerticalOwnedExternally` faux. Cibles : image 1 -> 43, images 2 à 21 -> **45** ; **image 22 -> 0** (avec le pull sauté ; image 23 sans). Rouge aujourd'hui : PosZ reste 0.
- **SJ-5b image sans tick** : même scénario avec `Update(0.001f)` avant la première image à tick ; **PosZ à la première image à tick : 327680** ; cible reste **43** jusqu'à ce tick ; sans cachet : cible Idle et PosZ 0.
- **SJ-5c 144 Hz** : `Update(1/144)` x 8, Croix à l'image 1 : ticks aux images 3 et 6 ; PosZ après l'image 3 : **327680**, après l'image 6 : 622592.
- **SJ-6 rattrapage** : Croix sur un `Update(0.04f)` (2 ticks) : PosZ **622592** (une impulsion).
- **SJ-7 horizontal** (Droite tenue, depuis l'arrêt) : `ForceX` aux ticks 1 à 4 : **79872, 115200, 150528, 150528** ; cibles : image 1 -> **2**, images 2 à 21 -> **44** (`0x2C`). Depuis la marche (ForceX 159744 stable) : 159744, **155136, 150528**.
- **SJ-8 menus** : Croix pendant `MenuOpen` : aucun saut, PosZ inchangé ; boîte fermée sans nouveau front : 10 images sans saut ; Croix pendant la boîte puis Croix après : un saut.
- **SJ-9 gel en vol** (UJ-11 de `conception.md`) : Début pressé en plein vol (image 8), 5 images de gel, reprise : PosZ identique avant et après le gel, `IsVerticalOwnedExternally` vrai, vol repris à la valeur suivante de la liste ; sans `OwnerExternalVerticalDisplacement`, le pied est rabattu (PosZ 0).
- **SJ-10 chute sans saut** (héros réel, falaise de 32 px, Droite tenue) : cibles **44** pendant les images en l'air, **1** à l'image du sol ; valeurs des fichiers `spawn-*` du § 4.1.
- **SJ-11 escalier** : descente d'une rampe 1:1 à 2 ticks par image, 30 images : cible **1** à chaque image (invariant voulu ; si rouge, tolérance du § 4.3).
- **SJ-12 appui sur une caisse de 16 px** (héros dans l'empreinte, au sol à 0, Croix) : PosZ au tick 17 **1114112**, au tick 18 **1048576** (= 16 px, `candidateTop + 1`), `ForceZ` 0, `CollidedWithEntityZ` 1, `IsOnGround` 1 ; ticks 19 à 25 : 1048576 ; héros déplacé hors de l'empreinte au tick 26 : PosZ au tick 27 : 1015808, 28 : 950272, 29 : 851968, 30 : 720896.
- **SJ-13 tombe de 25 px non soulevable** (héros à côté, saut vers elle) : pas XY libre aux ticks 8 à 13 (pied >= 1638400) et bloqué avant ; atterrissage au **tick 14, PosZ 1638400** (non 1605632) ; héros immobile dessus 5 ticks ; sans appui : PosZ 1605632 au tick 14 et coincé.
- **SJ-14 plafond (E19.h)** : boîte 24 x 16 x 32 à 48 px de hauteur : PosZ aux ticks 1 à 3 : 327680, 622592, 884736 ; **tick 4 : 1048576** (tête contre la boîte, `ForceZ` 0, `CollidedWithEntityZ` 1), tick 5 : 1015808 ; 6 : 950272 ; 7 : 851968.
- **SJ-15 garde d'octets** : `git diff --exit-code -- docs/hero-trace-389-highground-freestep.txt docs/hero-trace-389-highground-fixedstep.txt docs/intro-trace-389.txt docs/intro-programs-389.txt` = 0 ; `docs/hero-trace-389-spawn-*.txt` : 47 lignes changent, exactement celles du § 4.1, pas une autre.
- **SJ-16 statique** : le recensement des sites `0x2F` à front de Croix (4 hors chaîne) et des zones `0x3B` à un seul niveau de Z de la chaîne (40) est figé par un test de corpus (une exportation qui l'agrandit échoue).
- **SJ-17 gravité** : `0x17` du héros (comme la 163 `@81`) puis saut : gravité moteur après le vol = option 6 retenue (`MapGravity` : 1250 ; vivante : 0).


## C. Corrections des contre-vérifications (elles l'emportent)

### C.conception

Manques :

- Le chemin d'atterrissage terrain des PNJ appelle PushLogicalPositionToRoot (AlundraEntityScriptProxy.cs:733-760) quand !wasAlreadyLanded : troncature de PosX et PosY au pixel plus Controller.Teleport. R-1 ne l'interdit que pour le héros. Avec le port des sauts de PNJ, ce chemin devient actif pour tout PNJ à vitesse horizontale. Le snap du moteur à la tête de l'image (4 px) le rend inactif quand le dernier PosZ en l'air est <= 4 px (Giles 1360, chiens 768 : PosZ déjà égal à la cible, donc pas de Push). Il reste actif pour IZF 1280, 1408, 1536, 1664, 1792 et 2048 (dernier PosZ de 5 à 8 px), avec une perte de fraction X/Y jusqu'à 0,99 px à l'atterrissage. UJ-1 et UJ-1b (sur place, X entier) ne l'attraperaient pas. À prévoir : un test UJ-1c (sauteur à vitesse horizontale, fraction de PosX épinglée) et soit un atterrissage par MoveVerticalAndPullPosition sans Push, soit une épingle documentée.
- Contradiction interne entre C5 et UJ-1b : voir valueCorrections. Il faut trancher entre « écrire CollidedWithEntityZ sur le test strict » (C5, UJ-4 sans gravité) et « sur le test <= de la DLL » (UJ-1b).
- Saut de débogage de la 478 : le programme B[2] est collant (0xFF sans 0x40 ne relance pas, binaire et DLL). Il n'agit que si L2 ou R1 sont tenus au premier appel du programme, à l'arrivée sur la carte (premier tick non gelé), puis seulement tant que L2 ou R1 restent tenus seuls. La recette « tenir Y ou I sur la 478 » doit dire « à l'arrivée ». R-4 est donc plus faible qu'annoncé (un joueur qui ne tient rien ne déclenche jamais le saut), et la question 1 à l'auteur doit être reformulée.
- Carte 178 (sur la chaîne, dans la recette 176 -> 178 -> 183) : @386 est 0x22 (« Clamp forceZ to height target », 0x8003DA70) entre 1B [0,1] @383 et 1B [0,0] @387, sous 0x17 (Low gravity). Dans le binaire 0x22 attend PosZ == hauteur cible (+9<<19) en bornant ForceZ ; non porté (E19.h), il est ignoré : l'entité concernée ne montera jamais. Sans effet bloquant (0x22 est ignoré, pas attendu), mais visible en recette. À consigner dans O-E19-27/E19.h.
- IsOnGround n'est jamais mis à jour pour une entité sans contrôleur (13 prefabs, dont Sara (femme du rêve), Nirude (pensée résiduelle), Rancune de Melzas) : 0x25 y resterait à 0 pour toujours. Mon recensement (ownerless.py) ne trouve aucun site 0x25 possédé par un enregistrement de ces prefabs, mais les programmes B retargetés par 0x42/0x43 n'ont pas été vérifiés. R-9 reste valide pour les programmes d'enregistrement ; les B restent à recenser.
- Autres lecteurs de IsOnGround et PosZ du héros qui verront le vol : AlundraSaveGameDirector.FindSaveBlocker (:472, refus de sauvegarder en l'air, attendu), le verrou d'interaction (AlundraPlayerManager.cs:586-608, égalité de PosZ), la caméra (AlundraCameraDirector.cs:227, suit PosZ>>16) et la tête du portrait (AlundraWorldProxy.cs:1417). Ils n'ont aucune épingle de vol : à noter pour la recette (la caméra suit-elle le saut du héros comme dans l'original ?).
- Simplification possible du crochet (alternative, non un défaut) : AnimationSwitchTickOwed existe déjà et n'est posé que quand SyncAnimation valide un changement sans tick (avec horloge de sprite). Le champ OwedZForce peut être remplacé par la lecture de l'impulsion de CurrentAnimationId au moment où ce drapeau est consommé, ce qui évite un champ et le recalcul. Il faut garder le drapeau d'apparition et le verrou. Autre alternative : tenir dans le proxy une copie « courante binaire » (animation, ligne) mise à jour au tick, indépendante de SyncAnimation : détection exacte sans verrou ni dû, mais elle exige les mêmes crochets d'apparition et de relancement 0x1C.
- Le test d'atterrissage du héros et celui des PNJ n'ont pas la même convention : le héros utilise le strict (<), les PNJ gardent <=. À documenter pour E19.h : CollidedWithEntityZ n'est aligné qu'à moitié tant que ForceZ vaut 0 au tick d'atterrissage (DLL) contre F négatif (binaire).

Corrections de valeurs :

- UJ-1b, mise à jour 21 : la valeur attendue « CollidedWithEntityZ 1 » est incompatible avec la règle stricte de C5 (écrire 1 seulement si moddedPosZ + FinalForceZ < terrainHeight). À la 21 on a 327680 - 327680 = 0, donc pas strictement inférieur : CollidedWithEntityZ = 0 (comme le binaire). La DLL met bien ForceZ = 0 et IsOnGround = 1 à la 21 (branche <=), et CollidedWithEntityZ passe à 1 à la 22 (F = -32768, 0 - 32768 < 0). L'écart épinglé avec le binaire se réduit donc à ForceZ (0 contre -327680 à la 21), pas à CollidedWithEntityZ. À corriger dans UJ-1b et dans le texte de C2 (« la DLL pose CollidedWithEntityZ = 1 un tick plus tôt » est faux sous C5). UJ-4 (sans gravité au repos : 0) reste valable seulement avec la règle stricte.
- F13 et UJ-5 : « fixture sans sprite : PosZ 696320 au lieu de 663552 à la mise à jour 2 » n'est pas reproductible dans une fixture World à un tick par image. SyncAnimation valide CurrentAnimationId en :123, avant le retour sans sprite en :140, donc un test de niveau donne 663552. 696320 (= 2 x 348160) n'apparaît qu'avec une image à deux ticks (Update(0.04f)) ou sans SyncAnimation. Les expected de UJ-5 (663552, IsZForceApplied 0) sont corrects ; seule la justification du rouge de référence est à reformuler.
- Recette 478 : remplacer « tenir Y ou I : le héros décolle » par « tenir Y ou I (L2 ou R1, seuls) dès l'arrivée sur la 478, jusqu'à la fin du fondu ; relâcher puis retenir ne refait rien ». Même correction pour R-4 et la question 1.
- F22 : « 0x25 d'un PNJ peut rendre un tick avant le binaire » ne vaut que pour les arcs dont le dernier PosZ en l'air est <= 4 px (IZF <= 1024, 1360, 1616, 1872 ; Giles, Bergus et les chiens en font partie). Pour IZF 1280, 1408, 1536, 1664, 1792, 2048 le moteur ne rabat pas (dernier air de 5 à 8 px) et 0x25 rend au tick exact.
- Chiffre secondaire : mon scan linéaire compte 487 sites 0x25 dans 136 cartes (contre 465 « atteignables » du premier tour ; les filtres diffèrent, rien de décisif).
- Toutes les autres valeurs écrites d'avance sont confirmées : UJ-1 (22 valeurs, 0 à la 23), UJ-3 (20 valeurs, 0 à la 21, atterri à la 22, ForceZ -327680 à la 21), UJ-3b (30 valeurs, crête 3932160 aux ticks 15 et 16, 0 à la 31, atterri à la 32), UJ-12 (ticks 5 à 26), UW-1 (79872 constant), UW-2 (4992 par tick, 32 ticks, 31 non nuls, 37,78125 px), UW-4 (13 valeurs, crête 745472, atterri à la 14), arc des chiens 768 (crête 688128, atterri à la 14), arithmétique d'A10J (+79872 x 18, 18 valeurs de PosZ, atterrissage 1048576, x 34973184).

### C.valeurs

Manques :

- T-A10v (Alundra.Tests/AlundraEntityContactArcTests.cs:164) est un arc EXISTANT qui exécute `0x25 @4741` et `@5545` de la carte 10 (rec69/rec81, 'Bouquet de fleurs', CanPickup 1 donc sans gravité, `1B [128,255]` = -0,5 px/tick). Aujourd'hui `0x25` est sauté et les fleurs restent en l'air ; après E19.d2c elles tombent d'environ 16 ticks. valeurs.md les classe « hors chaîne » mais ne les met pas dans les épingles à re-mesurer (V3/V4). À ajouter : retirer ces deux entrées de l'ensemble des sautés (AssertSkippedWithin accepte un sur-ensemble, donc il ne rougit pas, mais il devient menteur) et re-mesurer les épingles (ordres et positions ; ses images absolues ne sont pas épinglées).
- A10J : l'ensemble des opcodes sautés proposé (« `0x90 @6406` seulement ») est incomplet. rec104 (bloc transparent à (252,440), SpriteDirection 64 donc chargé, C 203 = C[75]) exécute `2B @6413` et `95 @6418` à chaque image de toute carte 10 ; ce sont deux sauts mesurés dans T-A10v et T-B9. Il faut au moins (0x90,6406), (0x2B,6413), (0x95,6418), et probablement (0x90,2689) (B[22], sans condition d'arc). Sans cela `AssertSkippedWithin` d'A10J rougit.
- Garde de non-régression d'A3 : le bloc rec0 (gravité retirée par `0x63`, `0x5E [0,96,0]`) monte par EvaluateEntitySupport ; les épingles ForceZ=24576 et PosZ aux T20-T60 sont les plus sensibles à la restructuration (impulsion « hors de la porte Gravity »). Prévoir un rouge/vert de A3 inchangée comme preuve, pas la seule analyse statique des chiens.
- Exclusion d'apparition : le critère `Current == ~Target` supprime aussi une impulsion légitime si le programme C d'une entité écrit une animation à impulsion à son tout premier tick, avant le premier StepAnimationClock (le binaire fait le changement d'initialisation dans InitializeEntity, puis donne l'impulsion au changement suivant). Cas rare (aucun site de la chaîne), mais à écrire dans les risques ou à tester.
- La règle 0x8000 (IZF -32768 sans gravité => ForceZ=0) doit se tester sur les 16 bits bas : la DLL charge -32768 en int.
- Le 1er pas de la recette `day3-after-dream` (179 B[2], saut de Bergus) n'est couvert par aucun arc avant E19.e (A12). Proposition : épingler 179 B[2] (F+4, F+23, +19 images, rec8 PosZ = liste d'UJ-1) dès E19.d2c, puisque la recette de l'auteur en dépend.
- A10J n'est pas dans `ArcsWithoutEntityContact` (AlundraArcSupport.cs:269) : l'ajouter (T-REG-0) au lieu de n'asserter `EntityBlockCount` qu'à la main.
- Pas d'épingle pour l'atterrissage des chiens (optionnelle) à la 14e : elle serait fausse (13e).

Corrections de valeurs :

- UW-1, niveau de bottes 1 : `GameState.NumberOfItems[0x1A]=1` est FAUX. `AlundraPlayerManager.GetNumberOfItem` lit `state.NumberOfItems[itemId*2+1]` (AlundraPlayerManager.cs:932-946 ; AlundraGameState.cs:168-180, « read and written at [itemId*2+1] »). Écrire `NumberOfItems[0x1A*2+1] = 1` (indice 53), sinon le niveau reste 0 et le test attendu (79872,159744,159744,159744) échoue.
- A3, chiens rec3/rec4 (IZF 768) : la liste de PosZ est juste jusqu'à 196608 (12e mise à jour), mais la 13e mise à jour vaut 0 ET atterrit (test NPC `<=` : 196608 - 196608 <= 0, plus aimantation de tête d'image à 3,0 px). « 0 (non atterri), atterri à la 14e » est le comportement du test strict du héros, pas du PNJ de la DLL. IsOnGround vaut 1 dès la tête de la 13e.
- Giles, `0x24 @6400` : il rend à F0+257, pas F0+258 (la direction est lue en direct avant le mouvement de la même image : butée au tick F0+255, poussée sans avance à F0+256, lu au script de F0+257). `37 [10]` rend à s+11 : `0x19` à F0+268 (cohérent avec 257, pas avec 258), natif E de destruction à F0+269. Le côté héros reste F0+297 (événement de carte après les entités).
- Références de ligne décalées par rapport au HEAD actuel : EvaluateEntitySupport décroissance :562-574, test d'atterrissage :693 (pas :684), wasAlreadyLanded :730, CollidedWithEntityZ=0 :733, tirage racine+IsOnGround :1011 (pas :1002), UpdateVramFlags :1166 (pas :1151). Sans effet sur les valeurs.
- R7 (`IntroTraceHarnessTests.ImplementedOpcodes`) : surévalué. Il n'est utilisé que pour étiqueter « [implemented] / [NOT IMPLEMENTED] » dans une trace (IntroTraceHarnessTests.cs:1756-1759) ; aucun test ne le compare à Dispatch. Ajouter 0x25 est cosmétique, pas un risque de rouge.
- Skipped set d'A10J : voir le manque ci-dessus ({(0x90,6406),(0x2B,6413),(0x95,6418)} au minimum).
- Montage UJ-1 : correct dans ses signatures (ContactWorld.AddEntity(world, host, name, x, y, z, offX, offY, offZ, sizeX, sizeY, sizeZ), FlatGroundField.GroundZ, AnimSetEntry init-only avec IsZForceApplied, ResyncControllerFromFlags, MapGravityRaw/MapZViscosityRaw publics) ; AddEntity pose `Flags &= ~Gravity` donc `Flags |= Gravity` doit venir après. `ContactHost.LogicTicksThisFrame` ferme la mémo d'horloge à chaque appel : une seule entité par monde dans UJ-1/UJ-5, sinon l'horloge double.

### C.regression

Manques :

- M1. T-C61 (carte 61, T-C61 dans AlundraEntityContactArcTests.cs:419-470, Prefabs: true) charge la grille de fer rec 9 (prefab 396c008e, SpriteDirection 192 donc apparue au chargement, anim 0 à IZF −32768, sans gravité, collidable). C'est un arc existant exposé à l'exclusion d'apparition ET à la règle 0x8000 : sans l'une des deux, la grille prendrait ForceZ = −8 388 608 (−128 px/tick) ; la grille est aussi un obstacle de contact. Ajouter une assertion cheap à T-C61 (PosZ de la rec 9 constant, IsZForceApplied nul sur toute la durée). Le premier tour ne cite ce risque que pour la 389 et les projectiles. La 44 (recs 30-32) porte les mêmes grilles, avec 0x25 @387 sur l'une d'elles (entité sans gravité) à vérifier en E19.e.
- M2. 61 B[7] : 0x31 est « If flag off » ; avec G784 posé par T-C61 il NE saute PAS et le corps (dont le saut du héros @1165 et 0x25 @1174) est ouvert. Le premier tour dit « fermée par G784 » : polarité inversée. La conclusion tient pour une autre raison : la zone de B[7] (cases 0-20 x 4-20, soit x 0-504, y 64-336) n'est jamais atteinte par le héros de T-C61 (y ≥ 551). À noter dans le plan pour qu'un futur arc de la 61 ne l'ouvre pas par erreur.
- M3. Critère d'arrêt mal formulé : A3 et T-A10v ne deviennent pas rouges ; seules leurs traces changent. L'ensemble « tests qui changent » doit se définir par résultats rouge/vert plus les nouvelles assertions (TR-A3, TR-V) et la garde d'octets, pas par « trace changée ». L'ensemble rouge attendu est exactement {A10 : deux assertions des lignes 179-180} ; les entrées (0x25, 838/843/4741/5545) de AssertSkippedWithin deviennent mortes mais inoffensives (sur-ensembles), à nettoyer en hygiène avec le commentaire « the jump, E19.d2c » de T-A10v.
- M4. Le binaire relance aussi l'impulsion sur un changement de ligne de direction pendant une animation à IZF non nul (le bloc de changement d'UpdateAnimation 0x80038B08-0x80038B64 se déclenche sur Target != Current OU ligne de direction différente) : le port via TryResolveAnimationTarget le reproduit, mais aucun test ne le couvre (aucun site du banc). Un test UJ dédié (entité en anim 3 qui change de direction, ou chaîne 2 vers 2) éviterait une régression silencieuse.
- M5. Dossiers Alundra.Tests/Scripts et Alundra.Tests/UI : 19 fichiers de test hors du balayage du premier tour (142 fichiers de premier niveau). Vérifié : aucun ne touche les mécanismes de la tranche (trois seulement instancient un AlundraEntityScriptProxy nu comme PlayerEntity).
- M6. Cartes de jeu libre voisines avec eau : 12 (103 cases) et 16 (600 cases), hors chaîne ; 219 cases 0x08 sans 0x10 (donc ralentissement sans saut abaissé x160) hors chaîne et hors banc.
- M7. Le 0x53 copie l'animation courante du héros (AlundraWarpDirector.cs:410) : l'exclusion d'arrivée doit couvrir cette voie, pas seulement l'arrivée par défaut 0x36 ; le test UJ-6 « héros arrivé avec l'animation 2 puis 62 » doit passer par BeginDepartureFromChangeMapOpcode pour exercer la vraie voie.
- M8. Dans la boucle de tick de l'entité, le script d'une entité tourne avant StepAnimationClock : un drapeau d'exclusion « consommé au premier pas » exclurait aussi une écriture de script faite au premier tick (le binaire, lui, appliquerait l'impulsion). Sans conséquence sur les données (aucun site A, aucune écriture au tick d'apparition), mais le contrat du drapeau doit le dire (par exemple comparer Target à la valeur d'apparition).
- M9. Hors tranche mais exposé à l'auteur en jeu : les bouquets de la 10 flottent aujourd'hui à +8 px après le lâcher (1B [0,0] suit dans le même tick) ; la tranche corrige ce défaut, ce qui n'est pas dans la liste des effets visibles du plan.

Corrections de valeurs :

- Fichiers de test : 161 .cs au total (142 de premier niveau + 19 dans Scripts/ et UI/, sans rapport), et non 142 ; 1586 attributs Fact/Theory.
- 61 B[7] : « fermée par G784 posé par l'arc » est faux ; avec G784 posé le corps est ouvert (0x31 saute quand le drapeau est éteint) ; il n'est pas joué parce que le héros de T-C61 reste hors de sa zone (y ≥ 551 px contre y 64-336).
- R17 : « eau (walk & 0x08), toujours avec 0x10 » ne vaut que sur les cartes du banc ; 12 745 cases 0x08 dont 12 526 portent aussi 0x10 ; 219 cases (walk == 0x08) sur les cartes 0, 138, 271, 289, 293, 297, 301, 357, 481, 482.
- R20 / D3 : « aucune chaîne vers une animation à IZF sur les cartes du banc » : vrai pour les transitions atteintes seulement ; le préfab 396c008e (chaînes 2, 3, 4 vers 0 à IZF −32768) est présent sur les cartes du banc 44 et 61.
- 0x25 atteignables : mon modèle par propriétaires donne 467 sites dans 134 cartes (487 sites linéaires dans 136 cartes, identique) contre 465 annoncés ; écart de 2 sans conséquence, probablement la modélisation de 0x57/0x58.
- R1 : « le seul 0x25 du code est la ligne de la table » : AlundraInventoryDirector.cs:892 contient aussi un 0x25, mais c'est un identifiant d'objet, pas un opcode.
- « A3 : chiens à boîtes non recouvrantes en Y » : plus fort que dit, les Toutou 3, 4, 5, 13 ont MoreFlags 0 et ne sont pas collidables du tout.
- Bouquets : « PosZ − sol = 491520, 458752, 425984... » est exact seulement si le lâcher se fait à 8,0 px exactement au-dessus du sol (offset 65 [.., +8] relatif à la position posée par 89) ; la convention de repos de la DLL (pas de +1) peut décaler d'une unité 16.16.
- A10 : la marge de limite d'images est de 40 sur 1000 si la valeur mesurée est 960 ou 962 ; le premier tour recommande 1100 au-delà de 990, je recommande de la relever d'office à 1100.

### C.saut-binaire

Manques :

- M1. Cartes sous-marines 159 et 160 (Fairy cave underwater) : Gravity 3, ZViscosity 256 et octet d'en-tête +8 (nommé SlideEffectId dans l'export) = 1 ; les 481 autres cartes valent 128 / 4096 / 0. L'octet +8 est le décalage appliqué à ForceX et ForceY avant déplacement (srav en 0x8003675C) : en 159/160 le héros va deux fois moins vite (mesure : PosX +79872 par tick au lieu de 159744) et le saut dure 178 ticks (ForceZ écrêté à +65536 dès le tick 2 ; sommet 3156736 = 48,2 px au tick 87 ; atterri au tick 178, Idle au 179). La DLL n'a aucun consommateur de ce décalage (grep SlideEffectId dans Alundra/ : aucun). Les tests UH-1..UH-10 supposent 128 / 4096 / 0 et doivent le dire ; la lecture des trois valeurs par carte doit être testée.
- M2. Règle du passager nécessaire, pas facultative : 0x80037364 ajoute AdjustedForceX/Y de la plateforme à FinalForce du passager et, si le passager n'a pas d'impulsion, remplace son ForceZ/FinalForceZ par ceux de la plateforme. Des plateformes flottantes bougent avec le héros libre (docs/census-0x24-waits.md : cartes 38, 57, 119, 149, soit 6 sites 0x24). La recommandation (7) range la règle du passager dans 'consigner sans porter' ; avec l'atterrissage sur dessus d'entité le héros se pose sur une plateforme qui part sans lui.
- M3. RidingEntity est remis à 0 chaque tick par 0x80038998, avant P, et reposé par CheckRidingEntities 0x800364C8 en tout début de P (condition (Flags&0x4100)==0x100, dessus+1 == ModdedPosZ d'avant déplacement, recouvrement XY). Mon premier essai sans cette routine : le héros, après avoir marché hors du coffre, flottait (RidingEntity périmé = ForceZ du coffre = 0, jamais de gravité). Le port doit recalculer le passager à chaque tick et ne jamais garder l'ancien.
- M4. Au repos sur un objet CollidedWithEntityZ vaut 0 (le héros est porté, pas ré-atterri à chaque tick comme sur le terrain où cz vaut 1 en permanence) : mesure ticks 19 à 24 du saut sur coffre. Cela touche les opcodes 0x25/0x26 qui lisent cz et le test UH-10 (à asserter).
- M5. Dimensions des entités : Width/Height/Depth = (taille<<16) - 1 (0x80039CBC-0x80039CF4). Les valeurs de seuil (x0 = 126, x = 133,0) et la hauteur de repos sur un coffre en dépendent ; avec taille<<16 les seuils bougent d'une unité, x0 = 133 donne 196,844 au lieu de 196,633.
- M6. L'ouverture de dialogue pose le bit 0x10 (MessageBox) ou le bit 0x08 (MenuOpen) selon le mode (0x800452D4-0x800452F8) : un saut en cours se déroule pendant les dialogues de la première sorte et se fige pendant les autres. Le test d'un saut sous dialogue doit couvrir les deux.
- M7. Autres lecteurs de la Croix non listés : 0x8004A408 et 0x80051798 (menus, INTERVAL), masques de groupe 0xD0 (nage 0x800326D0 -> état 0x28 ; transport 0x800324E4 -> 0x24) et Hold&0xE0 en course (0x800320FC -> état 4). Sans effet sur le saut mais S4 était incomplet.
- M8. L'attaque en l'air existe dans le binaire (PlayerTryAttack est appelé avant le test IsOnGround, dans le gestionnaire des états de saut) : tant qu'il reste un no-op, Carré est mort en l'air ; à documenter parmi les écarts.
- M9. Saut sur place avec la croix directionnelle pendant un VramOR&0x4000 : le héros ne passe même pas en Moving ce tick-là (animation inchangée, ForceX 0), à noter dans UH-5.
- M10. Le tick 18 du saut sur marche : l'animation est encore 44 (MovePlayer lit og = 0 au début du tick 18) ; le héros n'a pas de tick intermédiaire où og vaut 1 sans atterrissage, contrairement au saut à plat (tick 21). Deux mécaniques de fin d'arc différentes pour la décision par tick.
- M11. Les 2012 enregistrements 'montables' incluent des objets qui ne sont pas des obstacles de puzzle (Mur à boule de fer 155, SaveBook 65, Goutte d'eau 56, Pot magique 39) : le recensement surestime l'usage de saut (la conclusion reste valable avec Coffre, Interrupteur, Plateformes).

Corrections de valeurs :

- UH-6 (variantes de x0) : x0 = 126, 127, 128 sont bloqués au seul tick 3 (x = 133,0) ; x0 = 129 à 133 sont bloqués aux ticks 2 et 3. Le plan dit 'aux ticks 2 et 3' dès 126. Position finale au tick 30 : 196,633 pour x0 = 126 à 133 (avec Width = (21<<16)-1), 195,734 pour x0 = 125, 190,734 pour x0 = 120 : inchangé.
- S-C / UH-6 : l'animation reste 44 jusqu'au tick 18 inclus (le tick d'atterrissage, MovePlayer lisant og = 0), Moving au tick 19 ; le texte 'animation 44 jusqu'au tick 17' est faux d'un tick.
- UH-10 / UH-11 : PosZ DLL 1048576 sur un coffre de 16 px vaut seulement si le coffre repose au terrain exact (convention DLL) et si sa profondeur est (16<<16)-1 ; avec 16<<16 le héros se pose 1 unité plus haut (1048577). À asserter aussi : CollidedWithEntityZ = 1 au tick 18 puis 0 aux ticks 19 à 24, og 1 jusqu'au tick 24, og 0 au 25.
- F19 : 'Sprint + Croix -> 0x2A' est un résultat de l'absence de Triangle tenu, pas de la Croix ; le bord de Croix est sans effet en course.
- Table des sons (F5, hors tranche) : 0x800490FC lit des lignes de 22 octets à partir de 0x800A82E8 (0x80049168-0x80049178 : id*22 + 0x800B0000-0x7D18), pas 0x800B82E8 + 14*id ; la ligne de l'identifiant 10 est en 0x800A83C4, valeurs en demi-mots (-1, 1, 0, 60, 0, -1, 0, -1, 2, 0, 1). La correspondance avec AlundraSoundBank reste à établir.
- F11 / UH-12 (plafond) : 524288, 491520, 425984, 327680, 196608, 32768 supposent la dalle posée à ModdedPosZ = 40 px + 1 unité (repos binaire) ; avec une dalle à exactement 40 px j'obtiens 524287, 491519, 425983, 327679, 196607, 32767 (atterri au tick 8 dans les deux cas).
- UH-8 glace : '4992, 9984 ...' vaut après un échauffement sur la glace (VramOR 0x20 déjà posé) ; depuis un premier tick à froid la première valeur est 79872 puis 84864, 89856 ... et 159744 est atteint au tick 17 au lieu de 32.
- Constantes de carte : 'gravité 128, ZViscosity 4096' valent pour 481 cartes sur 483 ; les cartes 159 et 160 valent 3 / 256 avec un décalage XY de 1 (M1). Les tests UH-1 à UH-10 doivent préciser qu'ils visent les 481 cartes.
- UH-9 (b) et la section sur le rattrapage : pas de valeur à corriger, mais sur une image à deux ticks la transition 0x2B vers 0x2D arrive avec un tick de retard (vitesse cible 208 au lieu de 196 pendant ce tick : ForceX 159744 au lieu de 155136), à asserter explicitement si l'option (b) est retenue.

### C.saut-dll

Manques :

- DÉFAUT DE CONCEPTION (recommandation 4, § 0.5, § 2.2) : le prédicat « airborne = HeroAirborne || (Controller != null && IsOnGround == 0) » est faux dès que HeroAirborne reste vrai alors que IsOnGround vaut 1. Or § 3.2 de leurs notes dit que le héros posé sur une entité « reste dans l'état en l'air (verrou, sentinelle) et IsOnGround = 1 », et à l'image 22 de SJ-5 HeroAirborne est encore vrai (l'atterrissage a lieu au tick 22, après MovePlayer). Avec le prédicat tel qu'écrit : (a) un héros posé sur une caisse ou une tombe serait en 0x2C/0x2D en permanence, à vitesse 196, et ne pourrait plus sauter (la branche air précède la Croix) ni déclencher d'interaction ; (b) SJ-5 donnerait 45 à l'image 22 et 0 à l'image 23, contre 0 à l'image 22 annoncé. Correctif : airborne = (Controller != null) ? IsOnGround == 0 : HeroAirborne (HeroAirborne ne sert qu'aux tests sans contrôleur, SJ-2), ce qui est cohérent avec la garde 2 et avec les valeurs SJ-5.
- Animation d'arrivée par 0x53 : AlundraWarpDirector.cs:408-411 prend l'animation du héros (TargetAnimationId) à l'instant de l'opcode, pas la constante 0x36 du chemin portail. Un héros qui change de carte pendant ou juste après un saut scripté (scène B[20] de la 10 : saut puis 0x53 vers la 135) arrive donc en 2/0x2B/0x2C/0x2D. Aujourd'hui MovePlayer l'ignore (le héros reste en 44 et court à 196) ; après le port, la queue le ramène à Idle/Moving dès la première image au sol. À épingler (UJ-7 de conception.md ne couvre que l'adoption), et le test d'arrivée AlundraWarpArrivalTests ne doit pas supposer une animation figée.
- Le jeu libre sur la 10 : B[20] correspond à MapEvents Index 19 (EventCodesBIndexMasked 20, zone 0..51 x 0..59, vérifié) ; ses deux premiers opcodes sont des 0x31/0x30 sur les drapeaux 1654/1655 puis 0x10. Confirmé « global » mais le verrouillage dépend de ces deux drapeaux, pas inconditionnel.
- Sites 0x2F à Croix tenue non listés : 143 @796 (0x40) et 475 @1723 (0xF0), hors chaîne ; le 478 @267 (0xFFF6) l'est aussi par la Croix, comme noté.
- Chaîne d'animation du héros : OnAnimationFinished (AlundraEntitySpawnFactory.cs:305) écrit TargetAnimationId = 44 ou 45 et fait AnimCompleteCounter++ à la fin de 2 et 43, après MovePlayer, dans la boucle de ticks. Inoffensif pendant un vol (MovePlayer a déjà écrit 0x2C/0x2D à l'image 2), mais à pinner : un héros qui relâche la direction au même tick que la fin de chaîne passe une image en 44 avant 45, et le compteur d'animations (0x1C/0x1D) monte sans lecteur sur la chaîne.
- Le crochet C1 (OwedZForce, SwitchZForceTaken) et l'étape verticale C3 de conception.md n'existent pas dans le code : le cachet (garde 1) n'a de sens qu'avec eux. L'ordre J4a -> J4b doit donc dire que SJ-5 à SJ-7 dépendent des tâches C1/C3 ; J4a seul (états, cachet, gardes 2-4) ne fait décoller personne.
- Seule vérification possible de l'incertitude A2/A4/A4p : résolue (bare ou verrouillé). Reste à baseliner A5/A5r/corridor 392 (PlaceHero + direction tenue, contrôleur réel), pas A2/A4/A4p.
- Convention de Z des entités au spawn : PosZ = z - ModZ + 1 (AlundraEntitySpawnFactory.cs, avant l'écrêtage au sol) : une entité sans gravité (tombe, livre) peut avoir son dessus à +1 unité 16.16 ; les valeurs SJ-12/13/14 (1048576, 1638400) supposent PosZ = T exactement. À re-dériver sur une fixture réelle, pas à figer avant.
- Le héros arrive avec IsOnGround = 1 posé par AdoptPlayerPawn tant que le pull n'a pas tourné (cas sans contrôleur : toujours) ; la queue d'états doit donc rester inerte sans contrôleur, ce que la garde 4 fait, mais l'argument « proxy nu : IsOnGround 0 par défaut » ne vaut que pour les proxys construits à la main dans les tests, pas pour les montages de monde.
- Aucun conflit avec le jump moteur : jump_speed 5.0 du prefab est inutilisé et ApplyInputSnapshot sans appelant ; à ne pas câbler en même temps.

Corrections de valeurs :

- Prédicat d'air : remplacer « HeroAirborne || (Controller != null && IsOnGround == 0) » par « Controller != null ? IsOnGround == 0 : HeroAirborne » ; sinon SJ-5 donne 45 à l'image 22 (et non 0) et le héros posé sur une entité reste en 44/45.
- R-4 / SJ-11 / § 4.3 : la descente d'une rampe est de 1,625 px par tick (trace highground : 8323072 -> 5898240 en 23 images, soit 1,61 px par image ; vitesse nord-sud 208*0x200 = 1,625 px), pas 2,4375 px. Deux ticks par image = 3,25 px < 4 (pas de saccade) ; trois ticks = 4,875 px > 4. Le seuil de risque passe de 2 à 3 ticks par image.
- F-X4 : 6 sites 0x2F contiennent la Croix hors chaîne (4 à front : 51 @271, 440 @640, 477 @68, 477 @106 ; 2 tenus : 143 @796, 475 @1723), pas 4.
- F-D6 : 36 solides bas -> 35 (sol sous les 4 coins) ou 36 (sol sous le centre) ; population à risque de coincement environ 32 (hors 2 marqueurs de 1 px et 2 panneaux de 28 px) ; sur la 10 : 16 et non 18.
- F-T3 : A0, A0b, A1 n'ont pas IsOnGround = 0 (AdoptPlayerPawn le pose à 1) ; A2, A4 (héros nu) et A4p (ControlLocked) sont inchangés ; l'incertitude à porter est A5, A5r, corridor 392.
- SJ-12, SJ-13, SJ-14 : valeurs en unités 16.16 possiblement décalées de +1 pour les entités sans gravité (spawn z - ModZ + 1) ; SJ-5, SJ-6, SJ-7 recalculés à l'identique : PosZ 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680, 0 (image 21, IsOnGround 1), atterrissage au tick 22 ; ForceX 79872, 115200, 150528, 150528 depuis l'arrêt et 159744, 155136, 150528 depuis la marche ; ticks du 144 Hz aux images 3 et 6.
- Valeurs des traces d'or : inchangées (47 lignes confirmées, mêmes écarts : -4608, -13824, -23040, ... -96768 aux images 221-231, -101376 de 232 à 267).
