# Contre-vérification de l'état d'E20.f6 (§1.22, f6a et f6b) — lecture seule

**Provenance.** Branche `chantier/restructuration-dll-r3`. Le HEAD réel est `3c923cfa`, pas `e1a68676` comme le dit le rapport. Les trois commits intermédiaires (`5db6eae0`, `7f3dcb83`, `3c923cfa`) ne modifient que `docs/plan-e20-heros.md`, à partir de la ligne 7318 (`git diff e1a68676 HEAD` : hunks @7318, @7416, @7436, soit §1.29 g4b). Les numéros de ligne cités pour §1.20 à §1.28 restent donc valables.

Ce que j'ai fait, sans rien écrire dans le dépôt :
- `cmp` des sections §1.20 à §1.22 contre `9ecc7d5` : identiques octet pour octet ;
- extraction des anciens fichiers à `9ecc7d5` et `be9a067` dans le scratchpad `f-state/f6check/` ;
- deux scripts Python en lecture seule (`sr.py`, `tmj.py`), lancés avec `-I -B` ;
- `check_names.py --tree --stage final`, qui donne « tree: 207 files, 220 top-level types … 0 finding(s), 1 warning(s) ».

`git status --short` montre ` m CasaEngineMonogame` et ` m alundra-datas-analyser`, déjà présents avant moi.

## Erreurs

1. **§7 « Inconnus » et §4 (« Effets de e0c ») : `SpawnContents` sur l'objet 0 n'est pas inconnu.**
   - §1.26 l'écrit comme un fait du binaire, `docs/plan-e20-heros.md:6892` : « `item = e.+0x3C` ; si `item == 0`, rien ».
   - Une boule née par l'interface a `ContentsItemId` 0 : `WorldProxy.cs:3566` appelle `RollLootAtCreation(..., hasRecord: false, 0)` ; l'en-tête porte `Contents 0` ; `LootRoll.Choose(0)` ne tire rien.
   - Le « aucun tirage » de (s3) reste donc vrai après e0c. La phrase « sa ligne 1.26 ne traite que `ContentsItemId = 69` » est fausse.
2. **Tableau 1b, ligne `AlundraHitPass.cs:109-131` : le glissement n'est pas de 25 lignes, mais de 10.**
   - Preuve : à `be9a067`, `for (var t` est à :108 et `bodyX = target.PosX` à :127. Au HEAD, ils sont à :118 et :137.
   - L'équivalent de 109-131 est donc `HitPass.cs:119-141`. La plage :134-144 ne couvre que le corps et le recouvrement.
3. **M11 : le recensement des lecteurs est bien une tâche du T0.**
   - Le plan écrit en toutes lettres « Le T0 recense aussi les lecteurs de `+0x180`, `+0x184`, `+0x190` … et nomme leur propriétaire » (plan:6141-6142).
   - Le vrai manque : la puce T0 (plan:6154-6155) ne le reprend pas, et aucune sortie ni acceptation ne le vérifie.
4. **M15 : deux des trois docs dites « périmées par f6a » ne le sont pas.**
   - `PlayerManager.cs:72-74` et `CellsCollisionField.cs:192-200` ne disent nulle part « héros seul ».
   - f6a rend fausses : `EntityScriptProxy.cs:1956-1962` (RESTRICTION « PLAYER ONLY … Every NPC's Slope_18c stays 0 ») ; `:1915-1918` (« nothing reads it yet besides … », déjà faux depuis la nage) ; la doc de `TickPhysics` / `TickMotion` (:1594-1599, :1613).
5. **§4 « Fichiers partagés » : les docs du registre sont rendues à g1a, pas à g2b.**
   - Le rapport écrit « `EventProgramRunner.cs` … g2b (doc :47-50) ».
   - Le bloc 1.24, point 6 (plan:6399-6403), confie ces docs à g1a : `NativeSpriteRegistry.cs:29`, `:35`, `EventProgramRunner.cs:47-50`.
6. **§4 « Même défaut dans f4 : `AlundraWeaponSpawnSeamTests.cs:251` » : mauvaise attribution.**
   - (E,17) est la clé de f5 (D-F5-6, plan:5954).
   - D-F4-8 de f4 (plan:5786-5787) fait déjà passer les clauses négatives de S5 sur un **registre vide**. Le défaut n'existe que si cette réécriture laisse :251 sur le registre de production. C'est l'affaire de f4/f5, pas de f6.
7. **§3 « hors de cause : `AlundraBirthBoxTests` (spawn puis recyclage sans tick) » : la raison est fausse, la conclusion tient.**
   - Le fichier fait tourner des images : `arc.OneFrame()` :192, `Frame(...)` :235 et :242.
   - Mais sur le type 27 (:173, :279).
8. **§4 et M3 : Q-R-7 n'est pas seulement dans 1.24 et §1.27.**
   - Il apparaît aussi dans §1.26 (plan:6968-6969), g1b (:6673-6674) et g3 (:7266).
9. **Tableau 2, ligne 9 (« pas d'impulsion ») : verdict juste, preuve insuffisante.**
   - `FrameSyncPasses.cs:242` ne montre que l'effacement. La même passe donne l'impulsion d'un changement d'animation (doc :230-237).
   - L'absence d'impulsion à la naissance vient de la règle R2 : pas d'impulsion pour l'animation d'une apparition. Or l'animation 0 des types 10 à 15 porte `IsZForce=512` (scratchpad `disc-e20f4f6/bin/out/data_animsets_3_15.txt:79-149`).
   - Témoins existants :
     - `AlundraAnimationImpulseSpawnTests.cs:107` (boule chargée, chemin par enregistrement) ;
     - Z3 d'`AlundraLogicalZWorldTests` (série de Z par l'interface).
   - L'animation 1 a `IsZForce=0` : E9 ne donne donc aucune impulsion.
10. **M7 rétrogradé en non bloquant.**
    - Le périmètre (plan:6174) ne compte qu'un fichier neuf de la DLL, plus le registre et `PlayerManager`. Les créateurs ne peuvent donc aller que dans `BallAi.cs`.
    - f4 fait de même : « C94, créateur de la traînée, clés » dans son fichier d'IA (plan:5806).
    - Les nommer reste utile, mais ce n'est pas un blocage.
11. **Décalages mineurs, sans effet sur le fond.**
    - `EffectOfNibble` est en `HitPass.cs:55` (:54 est sa doc).
    - `AttachHitEffect` est en `WorldProxy.cs:3092` (corps :3093).
    - D-E20-37 et D-E20-38 sont aux lignes 171 et 173 (le rapport dit 169-172).
    - `TickPhysics` finit à :1611.
    - §1.22 finit à :6261.
    - Statut « OK » donné à `FrameCounter` (:85 à `9ecc7d5` → :98) et à `Flags`, `TargetAnimationId`, `ForceResetAnimationFlag` (:137/139/148 → :156/158/167) : selon la définition du rapport, c'est « DEPLACE ».
12. **§4 « f6b n'est prérequis d'aucune tranche g » : vrai pour les prérequis nommés seulement.**
    - L'ordre de l'enveloppe place g après f6b : plan:6267 (« ces tranches ne s'exécutent qu'après f6b, h0 et h1 et A362 ») et §1.25 (plan:6838).

## Omissions

1. **Une moitié non-héros de `0x80038064` existe déjà, et l'étendue des statuts compte.**
   - `UpdateEnemyGroundHeights()` (`EntityScriptProxy.cs:2172`) écrit `FloorHeight` et `MapHeights` « at the end of the step (`0x80038064` / `0x800370C4`) ».
   - Il est appelé en :844-848, dans `EvaluateEntitySupport` (:739).
   - `TickMotion` appelle `EvaluateEntitySupport` **pour tout statut** (:1634, doc :1599 « the support runs for all »). `TickScriptedNpc` ne tourne que si `Status.IsActive()` (:1629-1632), soit les statuts 2 et 3, comme la liste de physique du binaire (D-E19-29).
   - f6a doit choisir le point d'appel et sa garde de statut. Aucun test (s1) à (s3) ne porte sur une boule en statut 3, alors que la branche A d'E9 (`fire11_wall_then_water`, f6b) en dépend. Le mutant « appel limité au statut 2 » survivrait aux tests de f6a.
2. **Les types 17 et 23 n'ont pas la gravité dans leur en-tête.**
   - Le plan affirme le contraire en trois endroits :
     - plan:6129 (« `0x100000` avec la gravité (1, 2, 10 à 17, 23, 234) ») ;
     - l'enveloppe, plan:641-642 ;
     - plan:652-653 (« h4 … types 16, 17 et 23, qui portent `DestroyOnSlidingSlope` avec la gravité »).
   - Or `sprite-records.json` donne `alundra_17` = `0x132014` et `alundra_23` = `0x102074` : ni l'un ni l'autre n'a le bit `0x100`.
   - Seul 16 (`0x132114`) justifie que h4 exige f6a. Le rapport cite le fait (tableau 2, ligne 3) sans signaler les phrases fausses du plan.
3. **Prérequis manquants de f6b (plan:6069, :6170).**
   - L'enveloppe, révision 10 (plan:657-658), dit que « f3c, f4, f5, **f6b**, h3 et h4 exigent e0a5 ». §1.22 ne le reprend pas. e0a5 est faite, donc ce n'est pas un blocage.
4. **« La fin du chantier de restructuration » (règle du programme) manque à §1.22.**
   - Les autres tranches READY la portent : plan:6386, :6968 (« règle du programme »), :7124, :7266.
   - R5 est ⚠️, bloquée sur Q-R-3 (`docs/plan-restructuration-dll.md:861`). Le nom et l'emplacement des fichiers de test restent donc ouverts.
5. **Recensement de `+0x18x` incomplet (tableau 2, ligne 15).**
   - `+0x190` = `Slope_190` (`EntityScriptProxy.cs:475`) n'a aucun lecteur dans la DLL, seulement la copie du clone (:2679).
   - `+0x184` = `CombinedVramFlagsAND` (:472) n'a qu'un lecteur, le héros (`PortalTrigger.cs:68`).
6. **e0c dépend aussi du propriétaire d'O-E0K-2.**
   - §1.26 (plan:6965-6966) : « Un objet lâché dans l'eau ou dans un trou n'est pas détruit tant que `CombinedVramFlagsOR` n'est nourri que pour le héros (O-E0K-2) ».
   - Une fois e0c faite, les objets lâchés naissent par l'interface : ils sont marqués et ont la gravité (`0x200180`/`0x230180` comprennent `0x100`). f6a calculerait leur `Slope_18c`.
   - Il n'y a aucun consommateur (pas de `0x100000`), donc c'est inerte. INFERENCE. Le T0 d'f6a doit l'inscrire au recensement.
7. **Tir en l'air des baguettes : aucun scénario ni test.**
   - Les fiches w4 et w5 ont un enregistrement « en l'air » de type 11 ou 14, délai 10, z 32 (`WeaponTables.cs:61-62` : `0x15, 0x0B/0x0E, 0x0A, 0x20`).
   - (p1) ne couvre que le sol, chargé ou non. Les 30 scénarios du scratchpad n'en ont pas (`bin/out`).
   - Non bloquant. Que le tir en l'air soit atteignable en jeu est une INFERENCE.
8. **Dépendants de f6a au-delà de g1a, g2b, h3 et h4 :**
   - g1b, « par eux f4, f5, f6a » (plan:6673) ;
   - g3, par transitivité.
9. **Tests qui ne bougent pas à la phase C, utiles à la liste prédite.**
   - `AlundraHeroAttackTests` (AT9, objets) et `AlundraHeroChargeTests` tirent avec un `ContactHost`. C'est un `IScriptHost` (`AlundraContactTestSupport.cs:52`), pas un `IEntityWorldContext` : `PlayerManager.cs:1216` n'y fait rien naître.
   - Les autres fichiers qui appellent `SpawnEntityBySpriteIndex` (19 fichiers) font naître les types 3, 7, 20, 27-29, 79, 213, 254, ou font naître puis recyclent.
   - Boules et bombes ne paraissent que dans `AlundraLogicalZStepTests`, `AlundraLogicalZWorldTests` et `AlundraMapStateWorldTests` (grep). Le recensement du rapport tient.
10. **Risque sur l'arrêt « plus de 10 tests existants bougent » (f6b).**
    - Cas de test à boule qui font tourner des images, natifs allumés (aucun `NativeHandlersEnabled` dans ces fichiers) :
      - `AlundraLogicalZWorldTests` : Z3 ×3, Z3 marches, Z5, Z6 ×3, Z8, Z10, Z11, soit 11 cas ;
      - `AlundraMapStateWorldTests` : K2 et K3.
    - Si la phase B (C60, puis E9 à l'atterrissage : 10 tirages, animation 1, drapeaux) en déplace la plupart, l'arrêt se déclenche. INFERENCE : non mesuré.
11. **Docs périmées depuis f3c, à corriger par la phase C.**
    - `PlayerManager.cs:1208` (« The set is empty in this slice, so nothing is born in production ») et `:1228` (« the production set stays empty »).
12. **M13, nuance : la dérivation de (p4) n'exige aucune nouvelle API.**
    - `WeaponTables` expose `RecordByte(uint)` en public (:128) et les constantes `RecordsAddress`, `RecordSize`, `RecordCount`, `GroundAnimationOffset`, `AirOffset`, `ChargeOffset` (:121). On peut énumérer les octets de type sans nouvelle API.
    - L'ensemble {27, 28, 29, 11, 12, 14, 15, 3, 4, 7} se vérifie sur le tableau (:57-68).
13. **L'INFERENCE « `FlatCells` a `ground_property` 0 » est un FAIT.**
    - `AlundraJumpTestSupport.cs:509` écrit `Zeros()` quand `groundProperty == null`.
    - `RampDown` d'`AlundraLogicalZWorldTests` remplit la colonne `slope`, que `UpdateGroundSlope` ne lit pas : il lit `SampleGroundProperty`, `TerrainProbe.cs:53-54`.
    - Pour f6a, ces tests restent à pente 0.

## Manques bloquants confirmés ou ajoutés

Confirmés :

- **M1 (inscription).** Confirmé. Les tests de phase A inscrivent leurs clés dans le registre du runner, que `CreateForProduction` remplit (`EventProgramRunner.cs:51`). Après T3, `Register` lève (`NativeSpriteRegistry.cs:60-63`). La même faute a fait REVISE g1a (plan:6481-6482) et g2b (plan:6615-6616). Il faut la règle du bloc 1.24, point 4 (plan:6395-6397).
- **M2 (liste prédite).** Confirmé, à trois titres :
  - les deux épingles : `AlundraNativeSpriteRegistryTests.cs:55` et `AlundraWaveSwordLifeTests.cs:181-183` ;
  - les tests monde à boules de la phase B (Omission 10) ;
  - aucune recomptée au HEAD de départ dans les T0 de f6a et de f6b (plan:6154-6155, :6206-6208), alors que f4 (:5849-5850) et f5 (:6014) en ont une.
- **M3 (garde des fichiers neufs).** Confirmé : `check_names.py:96-98` donne `[hors table]` pour `BallAi.cs`. **Ajout** : la fin du chantier de restructuration (Omission 4).
- **M4 (« 112 pour R = 101 de l'arène », plan:6077 et (p1) :6220).** Confirmé incohérent :
  - l'arène relâche à 91 (`AlundraWaveSwordSupport.cs:18`, `AlundraWaveSwordWiringTests.cs:64`, `e20-f3/README.md:30`) ;
  - §1.20 lui-même dit « R = 91 » (plan:5740) ;
  - 112 est le S de la vague de l'épée (91 + 21) ;
  - l'oracle des boules prend R = 96 et S = 107 (scratchpad `run_balls.py:2`).
- **M5 (conditionnelles caduques).** Confirmé : P-F6-3 « avant la règle de vitesse » (:6208) et « si f5 n'est pas faite » (:6230).
- **M6 (preuves hors dépôt).** Confirmé, avec une nuance.
  - Le chemin du scratchpad est nommé en §1.20 (plan:5721-5723). Les 30 scénarios de boules et les 2 des flammes y sont présents (32 noms dans `bin/out`, 58 fichiers dans `bin/out/json`).
  - Mais aucun T0 ne les importe dans `e20-f4f6/`. Le T0 de f4 ne copie que `e20-f3/oracle/tools` (:5840).
  - Bloquant si le scratchpad disparaît.

Ajouté :

- **B-a (statut et point d'appel de f6a, Omission 1).**
  - D-F6-2, « après le pas de chaque tick », ne fixe ni le site ni la garde de statut. Les deux sites possibles sont la branche `IsActive` de `TickMotion` (statuts 2 et 3) et `EvaluateEntitySupport` (tous statuts).
  - Aucun test ne porte sur le statut 3, dont dépend la branche A d'E9 de f6b.
  - À ajouter : un test (s4), une boule marquée en statut 3 posée sur l'eau, `Slope_18c` 4, accolée à la même boule en statut 4 ; et le mutant « statut 2 seulement ».

Non bloquants : M7 (voir Erreur 10), M8 à M16, avec les corrections des Erreurs 3 et 4.

## Confirmé (comptes)

- **Tableau 1a** : 22 lignes, 22 justes. Deux portent un décalage d'une ligne sans effet (Erreur 11).
- **Tableau 1b** : 16 lignes, 15 justes ; 1 fausse (Erreur 2).
- **Tableau 2** : 19 verdicts, 19 justes. Ligne 9 : preuve à compléter (Erreur 9). Ligne 15 : recensement incomplet (Omission 5).
- **Ligne 3, rejouée** sur `alundra-project/Data/sprite-records.json` et les 483 `.tmj` :
  - 12 types globaux : 1, 2, 10 à 17, 23, 234 ;
  - 14 types locaux : 34 à 38, 48, 107, 111 à 113, 134, 137, 174, 182 ;
  - **2 276 instances locales et 444 couples (carte, sprite) locaux**, ce qui confirme que « 444 » compte les couples et non les instances ;
  - plus 15 instances globales, dont les 3 du type 12 sur la 309.
- **Carte 309, données rejouées.** Entités 11 à 13 du `.tmj` (`Entity_6` à `Entity_8`) : sprite 12, `SpriteDirection` 65, `EventCodesA_LoadIndex` 135, soit A[7] = 76, et `Codes[76..82]` = `26,2,99,128,240,0,255`.
- **§3 (épingles et tests)** : toutes les citations vérifiées sont justes :
  - S5 :243, :244, :247, :251, :256 ;
  - P3 :150 et B6_R :364 ;
  - `AlundraFireSwitchRegressionTests` ;
  - `AlundraTestSavesTests.cs:67`, `TestSaves.cs:124` et `:128` ;
  - `FlatCells.Create` :451-457 ;
  - `AlundraDestroyEntityBreakEffectTests.cs:248`, `AlundraWorldProxyEventPassTests.cs:285` ;
  - les FireBall et Bomb d'`AlundraLogicalZWorldTests`, `AlundraMapStateWorldTests.cs:140-141` et `:154-155`.
  - Une seule raison fausse (Erreur 7).
- **§4** :
  - les 15 clés au total et l'absence de double sont justes ;
  - les lignes `ScriptedMotion.cs:175` et `PlayerManager.cs:1226` sont justes ;
  - les bouchons d'herbe sont justes : `WaveSwordAi.cs:34-40` et `PlayerAnimationEffects.cs:218` ;
  - erreurs : 1, 5, 6, 8 et 12.
- **§5** : `BallAi` est libre (aucune occurrence dans les `.cs` du dépôt, sous-modules compris). Le chemin `Alundra/Ai/Projectiles/` suit le modèle de `WaveSwordAi` (table.json). Le dernier ADR est 0075.
- **§6** : M1 à M6 confirmés, M7 rétrogradé. M8, M9, M10, M12, M13, M14 et M16 justes ; M11 et M15 à corriger ; 1 bloquant ajouté (B-a).

## Inconnus

- La carte 309 se construit-elle dans `ArcRun`, et les flammes y tournent-elles (P-F6-2) ? Aucun test ne charge la 309 : pas de « Meia Dream » ni de « -309 » dans `Alundra.Tests`.
- La valeur de R pour une baguette dans l'arène de charge (91 attendu par analogie, non mesuré), donc S des tirs chargés.
- L'effet réel de la phase B sur `AlundraLogicalZWorldTests` et `AlundraMapStateWorldTests`, et leur nombre (Omission 10) : exécution interdite.
- Le site d'appel retenu par f6a et sa conformité au binaire pour le statut 3 (Omission 1) : à trancher en T0.
- La chronologie de fin d'animation des boules (`+0xAC` à S+47, S+43, S+67) en DLL à 1, 2 et 3 ticks par image : non mesurée.
- Tous les chiffres de l'oracle et des recensements binaires du scratchpad n'ont pas été rejoués : pentes 1 et 4, 22 mutants, 30 scénarios.
