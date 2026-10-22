# Texte de la révision 2 de E20.e (2026-10-18), remplacé le 2026-10-22 par la révision 3 au plan

Archive du texte tel qu'il était dans `docs/plan-e20-heros.md` avant la révision 3 (réponses de l'auteur du 2026-10-22).

### 1.27 E20.e — L'herbe coupée : `CheckAndTriggerTileEffect` ⏳ (détaillée le 2026-10-18, révision 2 **READY** ; exécutable après E20.e0c et la fin du chantier de restructuration ; **à réviser** après les réponses du 2026-10-22 : D-E20-49, D-E20-59 (la file d'effets), D-E20-60 (E20.eR d'abord))

**Note (2026-10-18, relecture d'E20.f4).** O-E-1 (§2) : l'oracle de coupe de T0 (g) doit modéliser le pas par image du générateur (ADR-0066), sinon (w1), (w2) et la sonde (g2) divergent ; à relire à l'exécution.

**Relecture (2026-10-18).** REVISE, deux blocages corrigés (FIX, révision 1) : (1) l'oracle `wave_grass` d'E20.f3 a été calculé sur un champ
synthétique (toutes les cases en herbe, hauteur 0, `wave_grass.py:2`), pas sur la vraie carte 14 : (w1) et (w2) se comparent maintenant à un
oracle que T0 (g) calcule sur les vraies cases de la 14 et le même état de départ ; `wave_grass` ne sert plus qu'au rejeu de T0 (a) ;
(2) le chemin de l'octet 9 jusqu'au port est nommé et ses fichiers sont dans le périmètre. Seconde relecture : REVISE, un blocage (la vague
C61 est un natif, éteint dans le banc `HitArena` : (w2) ne pouvait pas passer, et rallumer les natifs réveillait les Beannoïdes de la carte) :
FIX en révision 2 (le banc vidé des autres entités, natifs allumés, une sonde du générateur en T0) ; nouvelle époque, une seule relecture de
clôture : **READY** (2026-10-18). Les défauts réversibles restent à confirmer par l'auteur (Q-E1 à Q-E5).

**Provenance.** Lecture du binaire du 2026-10-17 et sa critique (`docs/plan-e20-annexe/e20-e/`, `report.md` et `check.md`, au HEAD
`9502f75`) ; état de la DLL et des tests rejoué au HEAD `978608d` (`docs/plan-e20-annexe/e20-e/state-978608d/` : 60 citations traduites,
toutes au même code ; 25 affirmations, 21 confirmées telles quelles, 4 avec une correction) ; l'oracle MIPS de l'herbe déjà écrit par
E20.f3a (`docs/plan-e20-annexe/e20-f3/oracle/out/wave_grass.txt`, `.json`, `wave_grass2.txt`, `wave_grass_zwindow.txt`).

**But.** Comme l'original : l'épée du héros (15 états d'attaque) et la vague de l'épée (C61) coupent les buissons ; la case perd son bit
`0x02` et sa pile de tuiles (le buisson disparaît), l'effet global 2 naît au centre de la case, le butin est tiré (une chance sur huit
d'argent ou de cœur, E20.e0c) et le son 31 joue. La flèche chargée (C94, E20.f4) et la bombe (E12, E20.h3) n'auront que l'appel à ajouter.

**Faits (binaire, FAIT sauf mention).**
- `CheckAndTriggerTileEffect` `0x8003A7B0` (140 instructions), quatre appelants : le héros (`CreatePlayerAnimationEffects`, premier aiguillage,
  15 états : `0x04, 0x10, 0x12, 0x14, 0x16, 0x18, 0x19, 0x3F, 0x40, 0x41, 0x42, 0x44, 0x46, 0x49, 0x4B`, dans les deux modes, à chaque tick
  de ces états), C61 (une enveloppe de 8 mots), C94, E12.
- Rien si l'entité n'a pas de boîte de frame (`+0x1D4 == 0`). La boîte est celle **stockée à la fin de l'image précédente**
  (`0x80038064`, ou à la création). Les quatre coins dans l'ordre (colA, rowA), (colB, rowA), (colA, rowB), (colB, rowB) :
  `colA = T[(s16)(X >> 16)]`, `colB = T[(X + W) >> 16]` (`T[p] = p / 24` pour `p` de 0 à 1247), `rowA = Y >> 20`, `rowB = (Y + D) >> 20`
  (plancher) ; chaque case bornée à 0..51 et 0..59 ; seul le bit `0x02` de l'octet 0 compte ; bande de Z : `zc = hauteur << 20 + 0x80000`,
  sauté si `zc < bz` ou `bz + bh < zc` (signés). Coupe : bit `0x02` effacé, mot `+6 = 0xFFFF` (la pile), rien d'autre (hauteur, pente, sol
  gardés ; une case coupée ne se recoupe pas). Position : `X = col * 24 + 12`, `Y = row * 16 + 8` px, avec la colonne et la rangée **non
  bornées**. Puis `CreateEffectEntity(0, octet 9 de l'en-tête de carte, 0, X, Y, hauteur << 20)` (sans le `+0x80000`), `SpawnRandomLoot(0xFF,
  X, Y, hauteur << 20)`, `PlaySoundEffect(0x1F)`. L'effet ignore une table d'effets pleine ; le butin et le son suivent quand même.
- Émulation : 4 000 vecteurs de la lecture et 6 000 de la critique (son propre interpréteur), 0 écart avec le pseudo-code. Hors table
  (`X < 0` ou `X + W >= 1248` px) : le binaire lit le tableau des pointeurs d'entités ou un mot global (déterministe pour une disposition,
  valeur hors du modèle) ; la DLL bornera (écart noté).
- Seuls CATE et l'opcode `0x9E` lisent le bit `0x02` ; il n'entre dans aucun masque de marche : **couper ne change ni la collision ni la
  hauteur**. Le rendu (`0x8002CDA0`, une fois par image) saute la pile `0xFFFF` : le buisson disparaît à l'image même. Aucune persistance
  dans le binaire (INFÉRENCE : la carte relue à chaque entrée, comme D-E7-5).
- Écarts de la décompilation (le binaire tranche) : D1 garde de table ajoutée ; D3 la rangée sud divisée deux fois ; D4 `+0x80000` de trop
  au Z de l'effet et du butin ; D7 deux tirages par maillon de butin ; D9 une division tronquée au lieu du plancher (rangée −1 au-dessus de
  la carte).
- Données : 9 089 cases coupables dans 165 cartes sur 483 (aucune sur la 389), 8 989 avec une pile, 100 sans (coupe invisible : effet,
  butin, son) ; l'octet 9 vaut 2 sur les 483 cartes (exporté sous le nom trompeur `BalanceLevel`) ; l'effet global 2 (9 images de 5 ticks,
  fin `Destroy`) et les sons 31 et 84 sont exportés. La carte 14 a 179 cases coupables (rangées 11 à 55).

**Faits (DLL au HEAD `978608d`).** Bouchons nommés : `PlayerAnimationEffects.PlayerGrassCut` (`Alundra/Hero/PlayerAnimationEffects.cs:218`,
appelé par `Create` `:65-67`, lui-même appelé dans les deux modes, `Alundra/Hero/PlayerManager.cs:337`, `:350` ; `IsGrassCutState` `:195`
égale la table du binaire) et `WaveSwordAi.CheckAndTriggerTileEffect` (`Alundra/Ai/Projectiles/WaveSwordAi.cs:40`, appelé par `Tick` `:34`),
chacun avec son journal de test. La boîte : `StoreHitBox` (`Alundra/Entities/EntityScriptProxy.HitPass.cs:87-99`, l'équivalent de
`0x80038064`, à la création `:78` et en fin de pas `EntityScriptProxy.cs:1610`) ; dans `RunEntityBlock`, `MovePlayer` (`WorldProxy.cs:2927`)
et les programmes (`:2941`) passent avant la physique (`:2979`) : CATE lira la boîte du tick précédent, comme le binaire. La convention de Z
(`IsWall` ajoute 1 au `HitBoxZ`, `PlayerAnimationEffects.cs:178`, ADR-0026) ; la lecture de case du binaire
(`Alundra/Physics/CellsCollisionField.cs:272-285`) ; `BinaryColumn`/`BinaryRow` (`:185-188`) rendent une valeur **bornée** : la position de
l'effet demande la valeur non bornée. Le magasin de cases (`Alundra/Cells/CellStore.cs:35`, `ApplyCellBits` `:314` n'écrit que les bits,
jamais la pile ; `CellsMutated` `:53`) et le visuel (`Alundra/Cells/CellVisualSync.cs:338-355` retire les tuiles d'une pile devenue nulle ;
la reconstruction a lieu une fois par image, `WorldProxy.cs:2650`, après `RunEntityBlock` : le buisson disparaît dans le même `Update`) ;
`ICellMutator` (`Alundra/Cells/ICellMutator.cs:21-38`, trois opérations, un seul implémenteur, aucun faux dans les tests). Les natifs du
registre reçoivent un `IEntityWorldContext` (`Alundra/Ai/NativeSpriteRegistry.cs:20`, `Alundra/Events/EventProgramRunner.cs:305`) : C61 a
`CellMutator`, `SpawnEntityBySpriteIndex` et `EffectStage` ; le héros les atteint par `host is IEntityWorldContext` (`PlayerManager.cs:1216`) ;
le banc `ContactHost` des tests n'est qu'un `IScriptHost`. Effets : `EffectReservoir.Create` (`Alundra/Rendering/Effects/EffectReservoir.cs:101`,
128 emplacements), `EffectStage.Emit` `:192` et `CreateNow` `:199` (réservé au héros, ADR-0057). L'octet 9 n'est lu par personne (précédent
de lecture d'une propriété de carte : `Alundra/Combat/BalanceTables.cs:173-178`). Rien d'autre ne lit le bit `0x02` ; l'opcode `0x9E` n'est
pas porté.

**Tests qui épinglent les bouchons** (à faire évoluer ; R5 les déplacera) : `AlundraFlailBounceTests` (les 15 états journalisent la coupe,
les autres non, l'ordre exact `[GrassCut, Landing, LowHpAlarm]`, un appel à chaque tick des états du fléau) ; `AlundraWaveSwordLifeTests.V2_…`
(25 appels de S+1 à S+25) et `AlundraWaveSwordSupport.WaveOracle.GrassCallTicks`. Tests dont les valeurs supposent qu'aucune coupe n'a lieu :
`AlundraHeroAttackTests.RealPrefab_TheHeroOfTheExport_SwingsTheBasicSwordOnTheGroundAndInTheAir_AtTheTicksOfTheOracle` (carte 10 réelle,
la boîte de l'épée de base touche la case coupable (10, 56) de hauteur 0 d'après le calcul de la critique : l'assertion « seul le son 29 »
changerait, INFÉRENCE à mesurer en T0) ; `AlundraTestSavesTests` (liste exacte de 9 préréglages : un préréglage de recette l'allonge). Les
autres bancs (`JumpHeroRig`, `HitArena` de la carte 14, nage) n'ont pas de case coupable à portée (calculé par la critique, non mesuré).

**Défauts réversibles retenus en mode autonome (à confirmer par l'auteur, §2, Q-E1 à Q-E5).** Q-E1 : E20.e **après** E20.e0c (le butin et
son ramassage d'abord, comme le plan). Q-E2 : l'effet, le butin et le son **tout de suite** (`CreateNow`, sans file : l'ordre exact des
pas du générateur, le binaire n'a pas de file), pour le héros comme pour C61 : l'ADR-0057 s'étend à CATE (même ADR que Q-E0C-2). Q-E3 :
l'octet 9 **lu** dans la propriété exportée de la carte, repli sur 2, par le chemin du seuil d'équilibre (ci-dessous, « Périmètre »). Q-E4 : les 100 cases sans pile gardent le comportement du binaire
(coupe invisible, avec butin et son). Q-E5 : pas de persistance, l'herbe repousse à chaque entrée de carte (D-E7-5). Pour la tranche (pas
pour l'auteur) : Q-E6 le repère de Z se mesure en T0 ; Q-E7 hors table, la DLL borne (écart noté) ; Q-E8 la participation au tick de
naissance du butin se mesure en T0 (P-L1 d'E20.e0c) ; Q-E9 le renommage de « BalanceLevel » dans la doc de format et le convertisseur, hors
tranche (une note). Sans `IEntityWorldContext` (banc de test), CATE ne fait rien : le binaire n'a pas de mode dégradé, la production a
toujours le magasin.

**Prérequis ajouté le 2026-10-22 : E20.eR** (D-E20-60, esquisse). Le renommage de la propriété de carte `BalanceLevel` (l'octet 9) en
`TileCutEffectId`, à la source : le nom écrit par l'exporteur, `alundra-datas-analyser/AlundraTools/AlundraDataExtractor/TiledMapExporter.cs:275`
(`TiledProperty.Int("BalanceLevel", gameMap.Info.BalanceLevel)`), et la ligne de la doc `AlundraTools/docs/datas-bin-structure.md:78` (qui
note le nom exporté). **Ne changent pas** : le champ `GameMapInfo.BalanceLevel` (`AlundraTools/AlundraEngine/DatasBin/GameMapInfo.cs:14`, `:41`,
`:121`) et son lecteur `AlundraTools/AlundraEngine/GameEngine.cs:2520`, qui sont la décompilation (elle se lit, ne se corrige pas), ni le
chemin d'affichage du serveur MCP (`AlundraTools/AlundraGameRuntimeMcpServer/RuntimeHelperTools.cs:49`, `:172`, qui lit ce champ). Suivent le
nouveau nom : les commentaires `Alundra/Combat/BalanceTables.cs:27` et
`alundra-casaengine-project-converter/Writers/CellMetadataWriter.cs:13`, la doc vivante `docs/formats/cells-companion.md:73` ; puis la ré-extraction des cartes du remaster, le miroir vers `data-extracted/` (avec la
preuve `diff -rq` ; vérifier d'abord le sens de chaque écart : le remaster ré-extrait le 2026-09-19 avait le texte non décodé), et l'export
complet avec sa preuve par manifeste. Le test `Alundra.Tests/AlundraBalanceTablesTests.cs:154` (qui pose `BalanceLevel` sur une carte) suit
le nouveau nom ; `BalanceLevel` de `balance.json` (le rang d'une fiche d'ennemi) ne change pas ; les scripts figés des annexes d'oracles ne
changent pas (D-R-6). Le sous-module de l'analyseur porte des modifications locales de l'auteur : ne pas les indexer. Après E20.eR, E20.e
lit `TileCutEffectId` au lieu de `BalanceLevel`.

**Prérequis.** E20.e0c (`SpawnRandomLoot`, C255) ; E20.c1a et c1b (boîtes) ✅, E7 (magasin et visuel) ✅, E20.e0a, e0b, e0k ✅, E20.f3c (C61
en production) ✅ ; la fin du chantier de restructuration et la garde des fichiers neufs (Q-R-7).

**Périmètre.** Nouveau : `Alundra/Cells/TileEffect.cs` (le port de CATE, et `ResolveCutEffectIndex(TileMapData?)` : la propriété de carte
`BalanceLevel`, l'octet 9, entière, 2 si elle manque ou n'est pas un nombre, sur le modèle de `BalanceTables.ResolveThreshold`,
`Alundra/Combat/BalanceTables.cs:176-183`) ; l'octet 9 suit le chemin du seuil d'équilibre : `Alundra/Maps/WorldProxy.cs` le lit **une fois à
la charge de la carte**, à côté de `BalanceThreshold` (`:965`), dans un champ exposé par un membre explicite d'`IScriptHost` (comme `:437-441`),
et `Alundra/Maps/IScriptHost.cs` gagne un membre par défaut `TileCutEffectIndex => 2` (comme `BalanceThreshold => 0`, `:143` ; les hôtes
de test le reçoivent sans changement) ; `TileEffect` le lit sur `entity.ScriptHost` (le héros comme C61) ; modifiés : `CellStore.cs` et `ICellMutator.cs` (une opération de
coupe : bit `0x02`, mot de pile `-1`, pile détruite, `CellsMutated` ; distincte de `SetCellBits`/`ClearCellBits`, dont un test épingle qu'elles
ne reconstruisent jamais l'overlay), `PlayerAnimationEffects.cs` et `WaveSwordAi.cs` (les deux bouchons appellent le port ; leurs journaux
d'appels restent), `Alundra/SaveGames/TestSaves.cs` (un préréglage de recette `herbe-m14`) ; les tests ; l'annexe ; un ADR.

**Hors périmètre.** C94 et E12 (leurs tranches) ; l'opcode `0x9E` ; les autres bouchons de `CreatePlayerAnimationEffects` (pas, poussière,
atterrissage, eau : une tranche E20.e2 avec son T0) ; la libération de l'effet actif de `DestroyEntity` (`+0x1B8`, étape (4)) ; le coût de
l'overlay (mesuré, non optimisé).

**Partage des phases.**
- **Phase A** (T1, sans changement de comportement) : l'opération de coupe du magasin et `TileEffect` pur (effet, butin et son par une
  interface), testés contre les vecteurs ; aucun appelant de production.
- **Phase B** (T3) : les deux bouchons appellent `TileEffect` ; visible en jeu (les buissons se coupent).

**Tâches.**
- ⏳ **T0 — Annexe, oracle, mesures** (aucun code) : (a) les émulations de la lecture et de la critique (`work/emu_cate.py`,
  `check/` : ses `emu2.py`, `emu3.py`) et l'oracle `wave_grass` d'E20.f3 (champ synthétique) rejoués depuis une copie, sorties identiques (un
  contrôle de l'outillage, pas une valeur attendue des tests de monde) ; vecteurs exportés en
  JSON (boîte, cases avant, cases après, positions, Z, nombre de tirages) ; (b) **Q-E6** : le repère de Z de la DLL (la règle inférée est
  `HitBoxZ + 1` dans les deux comparaisons, comme `IsWall`), contre l'égalité exacte `HitBoxZ == zc` atteignable : vecteurs de bord sur la
  machine MIPS ; (c) la latence d'une image (boîte de l'image précédente) mesurée sur la DLL ; (d) le test réel de la carte 10
  (`AlundraHeroAttackTests.RealPrefab_…`) : la coupe a-t-elle lieu, à quel tick, avec quel son et quel tirage (la valeur attendue réécrite
  par dérivation du binaire, jamais collée) ; (e) la liste prédite des tests qui bougeront en T3 ; (f) la place des tests de monde et de la
  recette sur la carte 14 : un pixel du héros, sa direction, un buisson (pile visible) à portée de l'épée de base, et une direction de vague
  qui traverse au moins deux buissons ; (g) **l'oracle des tests de monde**, versé dans `docs/plan-e20-annexe/e20-e/oracle/` : un script sur
  le modèle de `wave_grass.py` (les outils `pworld` d'E20.f3 copiés), avec la fonction de cases lue dans les **vraies cases de la carte 14**
  (`data-extracted/data/map_14.json`, `Map.MapTiles` : marchabilité, hauteur, pile), le héros seul à la place de (f), **aucune autre entité** (le banc de la DLL
  les retire, ci-dessous), le générateur posé à une valeur `s0` en tête du tick de la frappe ;
  deux scénarios, le coup d'épée de base et la vague de (f) ; sorties `cut_m14.txt` et `.json` : pour chaque coupe, le tick, la case,
  la position et le Z de l'effet, le son, l'état du générateur après la coupe ; plus la liste des ticks sans coupe ; (g2) **sonde du générateur** dans le banc de T2
  (vidé, natifs allumés) : un coup d'épée en l'air loin de toute herbe, l'état du générateur tick par tick égal à celui de l'oracle dans la
  même situation (seule l'avance d'un pas par tick, ADR-0066) ; un écart est un arrêt (quelque chose d'autre tire).
- ⏳ **T1 — Phase A** (tests d'abord) : (c1) la coupe du magasin : bit, pile, sol et hauteur gardés, une seule reconstruction de l'overlay
  par image, collision et grille de navigation inchangées, une case coupée qui ne se recoupe pas, chaque cas accolé à son contraire ; (c2)
  `TileEffect` contre les vecteurs : les quatre coins et leur ordre, les bornes, la bande de Z aux trois valeurs de bord, la position non
  bornée, l'octet 9 et son repli, l'ordre coupe, effet, butin, son, un tirage ou deux par case coupée, la table d'effets pleine qui n'arrête
  ni le butin ni le son ; (c3) sans boîte, rien ; sans `IEntityWorldContext`, rien, accolé au monde réel qui coupe.
- ⏳ **T2 — Tests de monde, rouges avant T3** (le monde réel de la carte 14 du banc `HitArena`, sans cible ; **les autres entités de la carte
  retirées** avant le scénario, chacune par `Status = FlagToDestroy` puis `WorldProxy.RecycleDestroyedEntities()`, l'aide de
  `AlundraLootRollWorldTests.Recycle` (`:74-78`), qui ne passe pas par `DestroyEntity` : ni butin ni effet ; **les natifs allumés**
  (`Runner.NativeHandlersEnabled = true`, comme `AlundraWaveSwordLifeTests.V2`, `:128` : C61 en est un) ; le héros à la place de T0 (f) ; le générateur posé à `s0` en tête du tick de la frappe, comme l'oracle T0 (g)) : (w1) le coup d'épée : chaque coupe de
  `cut_m14` à son tick, sa case, la position et le Z de l'effet 2, le son 31, l'état du générateur après elle ; aucune coupe aux autres
  ticks ; (w2) la vague de (f) : les coupes de `cut_m14`, tick par tick, de même ; (w3) une frappe à côté d'un buisson ne coupe rien ; (w4)
  un état hors des 15 ne coupe pas, à la même place ; (w5) l'herbe repousse à la rentrée dans la carte.
- ⏳ **T3 — Phase B** : les deux appels. Verts ; les tests qui épinglaient les bouchons gardent leurs journaux d'appel (inchangés) ; ceux de
  la liste prédite réécrits par dérivation.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre, dont un sur les épingles). Mutants, au moins 12, tous tués : rangée sud
  divisée deux fois (D3) ; `+0x80000` ajouté au Z de l'effet (D4) ; division tronquée (D9) ; colonne bornée pour la position ; un coin oublié ;
  ordre des coins changé ; bit `0x01` au lieu de `0x02` ; bande de Z stricte ; pile gardée ; collision changée par la coupe ; butin avant
  l'effet ; son oublié ; boîte du tick courant.

**Acceptation.** Les vecteurs et l'oracle `wave_grass` égaux ; (c1) à (c3) verts en T1 ; (w1) à (w5) rouges avant T3 (nommés), verts après ;
suite en Release (copie neutralisée) et en Debug, zéro échec ; tests déplacés ⊂ liste prédite de T0 (e) ; traces inchangées (la 389 n'a
aucune case coupable) ; `cmp` de la DLL ; moteur, convertisseur et données sans diff ; recette de l'auteur : la sauvegarde `herbe-m14`,
couper un buisson à l'épée puis avec la vague, voir le buisson disparaître, l'effet, parfois un objet, l'entendre.

**Arrêts.** Q-E6 contredit la règle inférée sans règle de remplacement établie ; la sonde T0 (g2) diffère de l'oracle ; un test hors de la liste prédite bouge ; un besoin de toucher
au moteur ; plus de 10 tests existants bougent ; un nom refusé par `check_names.py`.

**Budget.** Un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.

**Retour arrière.** La phase B se retire seule (les deux appels) ; la phase A ne change aucun comportement.

