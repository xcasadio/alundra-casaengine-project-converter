# E20.h4b : contre-vérification du rapport « où atterrissent les briques communes dans la DLL »

Cette contre-vérification fait foi là où elle diffère du rapport (`../dll/state.md`). Verdicts : JUSTE / FAUX / IMPRÉCIS (le fait est vrai mais la formulation, le chiffre ou la portée est à corriger). Faits : FAIT (vu dans le code, les tests, les données ou le binaire, avec fichier:ligne ou adresse), INFÉRENCE (déduit, avec de quoi), INCONNU.

Rapport écrit aussi dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/dll-check/check.md`.

## Synthèse

- Le rapport est fiable sur ce qui compte pour l'atterrissage des briques.
  - Toutes les lignes citées existent au HEAD et portent ce qu'il dit, sauf deux décalages de ligne sans conséquence.
  - Les lectures du binaire se reproduisent : poussée du héros comme des autres, branche porteur, balayage `1..count−1`, tri de Hoare, tables 256, coup de caméra, tampon, écrivains de `[0x80134360]`.
  - L'équivalence « plus haut emplacement tenu = `[0x80134360]` » tient.
  - Aucune brique ne force un changement de représentation.
- À corriger :
  - **FAUX** : les « 98 placements ». Ce sont 98 enregistrements de tables de sprites ; les placements statiques d'E3 sont 19, sur 5 cartes.
  - **FAUX** : « CameraSway déjà réinitialisé par tous les `ResetSingletons` ». C'est 1 sur 6.
  - **FAUX** : le modèle de test des tables « contenu littéral ». Il compare à `ALUN_CD.EXE`.
  - **IMPRÉCIS** :
    - le poids du terme de table `+0x188` (non nul sur les tuiles de glissement, indépendant du décalage) ;
    - les lecteurs de `FinalForce` (deux portes XY de plus) ;
    - les écrivains de `+0xCC/+0xD0` (12 et 10, pas 14 et 13) ;
    - les appelants de la requête canonique et son filtre du cas 4.
- Omis :
  - la barrière de câblage `UnhandledWeaponSpriteCodes` (S5) ;
  - les filtres identiques déjà présents dans la passe de coups (`HitPass.cs:122-149`) ;
  - `IsLoadedNormalOrDeactivated` (statuts 1-3) contre le statut 2-3 de la requête ;
  - `PlatformEntity` jamais assigné (tranche l'INCONNU du porteur) ;
  - le sprite 154 sans contrôleur ;
  - les lecteurs de `ForceAdjusted` hors opcodes.

## 0. Base et méthode

- FAIT : dépôt `D:/development/repo/alundra-casaengine-project-converter`, branche `chantier/restructuration-dll-r3`. HEAD relu : `c221a76f`.
  - Le rapport cite `eb285bf7` : IMPRÉCIS sur l'identifiant, JUSTE sur le fond.
  - `git diff --stat eb285bf7 HEAD -- Alundra Alundra.Tests` et `66d86ebc HEAD` sont vides. Les trois commits entre `eb285bf7` et `c221a76f` sont des commits de plan (`docs(plan)`).
  - `git status --porcelain -- Alundra Alundra.Tests alundra-project/Data` est vide : j'ai lu le HEAD.
- Rien n'a été écrit hors de ce dossier (ni édition, ni `git` en écriture, ni build, ni test, ni export).
  - Binaire `ALUN_CD.EXE` (France) relu avec des COPIES de `exelib.py`, `lin.py`, `scan_jal.py`, `scan_refs.py` (annexe `e20-h4`), lancées `python -B` depuis `tools/` de ce dossier.
  - Scripts à moi : `chk_push.py`, `chk_disp.py`, `cite_bounds.py`, `placements.py`, `tables_check.py`.
- Citations : j'ai rouvert chaque fichier:ligne du rapport (`Read`, `awk` numéroté).
  - La copie de `verify_cites.py` rend bien « 270 citations, 0 échec » (JUSTE). Mais elle ne teste qu'une expression par ligne : elle valide `PlayerManager.cs:1218` (`world.SpawnEntityBySpriteIndex`) alors que la phrase citée porte sur `host is IEntityWorldContext`, ligne 1216.
  - Mon `cite_bounds.py` : 109 citations avec nom de fichier, aucune hors bornes. Trois citations de la décompilation ne se résolvent pas par nom ; je les ai relues à la main.

## 1. Ce qui prévaut sur le rapport (FAUX ou IMPRÉCIS à fond), du plus lourd au plus léger

1. **FAUX : « les 98 sprites sont des placements », « change le jeu pour 98 placements »** (§8.2, §8.3, §10 point 4 du rapport).
   - FAIT (`tools/placements.py`, 483 cartes de `data-extracted/data`) : les 98 sont des couples (carte, indice). Ce sont des **enregistrements de la table de sprites** des cartes (`SpriteInfo.SpriteRecords[i].Header.ProgramDeactivate == 3`) : 98 couples, 75 cartes, 15 indices. Le rapport a raison sur les 98 couples, les 75 cartes et les 15 indices (JUSTE) ; il a tort d'en faire des placements.
   - FAIT : les placements statiques sont les entités de `SpriteInfo.Entities.Entities`. Leur page est choisie par `SpriteDirection & 0x80` (page de carte ; règle de `Alundra/Entities/Spawning/EntityRecordMapper.cs:236-246`, `ResolveSpriteType`).
     - Pour E3 : **15 entités de page de carte sur 5 cartes** (382 : indice 95 ×1 ; 386 : 95 ×1 ; 388 : 254 ×11 ; 432 : 254 ×1 ; 451 : 254 ×1) **plus 4 entités de page globale** (sprite 16, carte 388) = 19 placements statiques sur 5 cartes.
     - 3 types touchés : `0x100+95 = 351`, `0x100+254 = 510`, et 16.
     - Pour E13 : 1 placement (carte 321, indice 213). Pour E28 : 0.
   - Conséquence : inscrire (E,3) pour les 15 types de carte ne change statiquement que ces 15 entités (2 types) et les 4 du sprite 16. Le reste des 98 couples n'est que des définitions jamais placées.
     - Le chiffre « 98 sprites » de `e20-h4/report.md` §0.7 et de `plan-e20-heros.md` §1.31 fait 7 est de la même nature (enregistrements, pas placements).
     - Les entités nées par script (`0x2D`, `0x8A`, `0x8B`) ou par l'interface ne sont pas comptées ici : INCONNU.
2. **FAUX : « CameraSway est déjà réinitialisé par tous les `ResetSingletons` »** (§7.3, dernier point).
   - FAIT : il y a six `ResetSingletons` dans `Alundra.Tests` ; un seul appelle `CameraSway.Instance.ResetForTests()` : `SaveGameDirectorTestSupport.cs:120-139` (ligne 134). Les cinq autres ne le font pas : `AlundraGameTimeTests.cs:33`, `AlundraNativeEffectTests.cs:49`, `AlundraSaveGameProductionTests.cs:48`, `AlundraSaveGameLoadResetTests.cs:34`, `BackdropWarpDepartureFreezeTests.cs:224`.
   - Conséquence : un coup de caméra ou un tampon de session ajouté à `CameraSway` (singleton de session, `CameraSway.cs:26`) fuit d'un test à l'autre dans toute suite qui n'appelle pas `SaveGameDirectorTestSupport.ResetSingletons`. Les trois appels directs du rapport restent justes : `AlundraCameraSwayTests.cs:28,37`, `AlundraCameraSwayWiringTests.cs:39`.
     - Le tampon du feu 2 est restauré sans garde : un tampon laissé par un test précédent serait restauré tel quel.
     - À traiter en h4b : remise à zéro dans les six variantes, ou appel commun.
3. **FAUX : le modèle de test des tables « AlundraHeroRunTablesTests.cs:46-64 (contenu littéral) »** (§5).
   - FAIT : `AlundraHeroRunTablesTests.cs:42` (T1a) et `:57` (T1b) comparent la table de la DLL **mot à mot avec `ALUN_CD.EXE` lu à l'exécution**. `Executable()` prend un chemin absolu par défaut ou la variable `ALUNDRA_EXE`, et le test échoue en nommant le fichier s'il manque. `Words` lit des mots de 32 bits.
   - Ce n'est pas un contenu littéral. Un test des tables 256 sur ce modèle dépend du même fichier et demande un lecteur de demi-mots.
4. **IMPRÉCIS : « Exactitude : égale au binaire tant que la table et le décalage sont nuls … vrai sur 481 cartes sur 483 »** (§2.4).
   - FAIT (`tools/tables_check.py`, binaire) : les tables de `0x800366FC` ne sont **pas** nulles. Elles sont à `0x80028B94` (X) et `0x80028BD4` (Y), 16 mots chacune, indice `+0x188 & 0xF`.
     - X vaut −61440 aux indices 4 à 6, −983040 à l'indice 7, +61440 aux indices 8 à 10, +983040 à l'indice 11.
     - Y vaut ±40960 aux indices 1, 2, 5, 6, 9, 10, 13, 14.
     - Le chiffre « 481/483 » ne concerne que l'octet de décalage (`Info.SlideEffectId`, 0 sur 481 cartes, 1 sur les cartes 159 et 160 : relu, JUSTE).
   - FAIT : la DLL n'écrit jamais `TileAttributes` (`+0x188`, `EntityScriptProxy.cs:473`). Seules références : déclaration, `Clone :2677`, commentaires. Le terme de table est donc toujours absent côté DLL.
   - INFÉRENCE (lecture partielle de `0x80038240-0x80038340`) : le binaire pose `+0x188` à `1 << ((tuile >> 20) & 3)`, avec `0x80` en plus sous une condition de bit de tuile. Les indices utiles seraient donc 1, 2, 4, 8 (Y −40960, +40960 ; X −61440, +61440). L'écart « sol plat seulement » est donc indépendant du décalage et existe sur toutes les cartes qui ont de telles tuiles.
   - FAIT : la décompilation (`PhysicsEngine.cs:1516-1519`) nomme le décalage `Info.SlideEffectId & 0x1f`. L'INFÉRENCE du rapport sur l'octet `+8` (`lb 8(...)`, `0x80036744`) est donc appuyée par le nom.
     - La décompilation décale **seulement la table** (`ForceX + (table >> shift) + push`). Le binaire décale **la somme** : `((Force + table) >> shift) + push` (relu : `0x80036758` addu, `0x8003675C` srav, `0x80036764` addu de la poussée).
     - Écart de décompilation non signalé par le rapport ; sans effet à `shift = 0`, réel sur les cartes 159 et 160. Le binaire fait foi.
5. **IMPRÉCIS : « Tout ce qui suit [l'insertion] lit déjà `FinalForceX/Y` »** (§2.4). Incomplet : deux lecteurs de `FinalForceX/Y == 0` ne sont pas cités.
   - FAIT : `ScriptedMotion.cs:421` (`SnapHeroAtApex`, porte 1 : « pas de force XY, pas de pincement », binaire `0x800377A0/0x800377A8`) et `:564` (`SnapAtApex`, l'aimant des entités `HasLogicalZ`, même règle).
   - Avec la poussée dans `FinalForce`, ces deux portes voient la poussée, comme le binaire. FAIT : `0x80037798-0x800377A8` lit `+0xE4` et `+0xE8`, recopiés de `+0xDC/+0xE0` en `0x80036BA4-0x80036BB4` après l'ajout de la poussée.
   - Pas de test qui bouge à poussée nulle, mais ce sont des consommateurs de plus à épingler (le héros d'abord).
6. **IMPRÉCIS : « 14 `sw` non nuls à `+0xCC` et 13 à `+0xD0` dans des fonctions distinctes »** (§2.3).
   - FAIT (`tools/chk_push.py`) : sur 39 accès, les décomptes 14 et 13 incluent des écritures de pile : `0x80047304` et `0x80051628` (`+0xCC`), `0x800472E4`, `0x80051634` et `0x8008CC98` (`+0xD0`), tous `sw $ra/$sN, 0xNN($sp)`.
   - Les écritures d'entité non nulles sont **12 à `+0xCC` et 10 à `+0xD0`**, réparties sur 6 fonctions environ (début de fonction pris à `addiu sp,sp,-N`, heuristique) :
     - `0x80061EB8` (3 sites à `+0xCC`, 2 à `+0xD0`) ;
     - `0x800647B0` (1 à `+0xCC`) ;
     - `0x80075A3C` (2 et 2) ;
     - `0x8007773C` (3 et 3) ;
     - `0x8007AF20` (C65 : 1 et 1) ;
     - `0x8007BD8C` (C75 : 2 et 2).
     - `0x8005709C` est un `sb` à `0xD0`, coïncidence.
   - Le fait utile est JUSTE : canal général, pas propre aux sorts ; deux remises à zéro `0x80036750/54` et `0x8007EBBC/C4`, la seconde dans une fonction `0x8007EBA8` qui met aussi `+0xB8` à zéro ; sites C65/C75 cités.
   - INCONNU : le rôle des quatre fonctions hors sorts (`0x80061EB8`, `0x800647B0`, `0x80075A3C`, `0x8007773C`) n'a pas été lu. La propriété de la poussée (Q-H4-3) reste à l'auteur.
7. **IMPRÉCIS : §4.1, qui appelle `EntitySearchService`.**
   - FAIT : 27 appels dans `EventProgramRunner.cs` (de `:791` à `:3382` ; la ligne `:1933` est un commentaire) : JUSTE.
     - Côté tests, le rapport ne cite que `AlundraDestroyedEntityRecyclingTests.cs:63`. Il y a aussi `EntitySearchServiceTests.cs` (20 appels), `AlundraNpcCharacterControllerMoverTests.cs` (7) et `AlundraSpawnBySpriteIndexInterfaceTests.cs:190` (1).
     - Le tableau §9 dit « aucun test de requête n'existe » pour la requête **nouvelle** : JUSTE. Mais les 20 tests de la requête canonique existent et épinglent le comportement qu'il ne faut pas toucher.
   - FAIT : l'affirmation « [la requête canonique] ne filtre ni par drapeaux, ni par fiche, ni par distance » est fausse pour le cas 4.
     - `EntitySearchService.cs:157-167` filtre sur `Flags & Collidable`, `AnimFlags & 0x80` (NoEntityCollision) et `PlatformEntity == null`.
     - Les cas 0 à 3 et 5 à 11 filtrent sur le statut (`IsLoadedNormalOrDeactivated`), `IsPlayer` et des relations, pas sur le masque `+0x6C`.
     - La conclusion (aucune des canoniques ne fait la requête du binaire) reste JUSTE.
   - FAIT : piège de réutilisation. `IsLoadedNormalOrDeactivated` (`EntityScriptProxy.cs:32-33`) vaut les statuts 1, 2 et 3 ; la requête du binaire prend 2 et 3 seulement (`(status − 2) < 2`, `0x8003AFC8-0x8003AFCC`). Utiliser `Status.IsActive()` (`EntityStatusExtensions.cs:11-12`).
8. **IMPRÉCIS : « `Ai/EnemyHelpers.cs` n'a que `RelativePosition` et `TryAttackPlayerFront` »** (§4.1).
   - Il a aussi `RelativePositionFromPosZ`, `TryAttackPlayer2`, `HandleAnimationDirection`, `ZeroForces`, `StartWalk`, `HitReaction` (`EnemyHelpers.cs:41-230`).
   - Aucun n'est une requête de cibles : le fond (pas de requête, pas de tri) est JUSTE. `rg Sort(|OrderBy|Hoare` relu : seuls `DialogueDirector.cs:305` et `EffectStage.cs:232` (JUSTE).
9. **IMPRÉCIS (2 lignes) : « précédent `Hero/PlayerManager.cs:1218` »**. `host is IEntityWorldContext world` est à `:1216`, l'appel `world.SpawnEntityBySpriteIndex` à `:1218`. Autre précédent non cité : `EntityScriptProxy.cs:1521` (`ScriptHost as IEntityWorldContext`).
10. **IMPRÉCIS : §2.1, « `ScriptedMotion.cs:641-662` copies locales glace/eau »**. Le code va de `:644` à `:662` (`:641-643` sont des commentaires). Sans conséquence.

## 2. Ce que j'ai relu et qui est JUSTE

### §1 Verdict court

1. « Aucune brique ne demande de changer une représentation existante » : JUSTE. Champs `int` `PreviousAdjustedForceX/Y`, `byte[4]`/`short[10]`, table de slots à `ulong`, `CameraSway` à 9 propriétés : tous compatibles avec des accesseurs ou des ajouts.
2. « Aucun test existant ne bouge si les briques sont ajoutées sans écrivain » : JUSTE pour la poussée, l'indice, la requête, les tables, les accès 32 bits, le coup et le tampon. Le registre ne bouge qu'avec une clé de production (2 épingles).
   - IMPRÉCIS : S5 de `AlundraWeaponSpawnSeamTests.cs:236-258` n'est pas cité comme barrière de h4c (voir « §3 Faits nouveaux », point 4).
3. (a) JUSTE : le binaire applique la poussée au héros ; il saute la passe pour une entité non héros dont `+0x28 != 0` (voir « §2 Poussée », branche porteur).
   (b) JUSTE : `+0xB4` = `HitAnimByte` (`EntityScriptProxy.HitPass.cs:30`), pas `AnimFlags`.
   (c) JUSTE : l'indice du plus haut emplacement = plus haut emplacement tenu (voir « §3 Indice du plus haut emplacement »).
4. JUSTE : E3 sur 6 types de page globale (16, 23, 150, 151, 152, 153) et 15 indices de page de carte (74, 95, 101, 110, 116, 155, 176, 188, 206, 212, 218, 219, 227, 242, 254 ; types `330 … 510`).
   - E13 : global 17 et carte 213 (type 469). E28 : global 21. C63 : 23. C65 : 24 et 154. C75 : 19.
   - Relu dans `alundra-project/Data/sprite-records.json` (395 en-têtes, `BankKey alundra_N` ou `N`).
5. JUSTE :
   - `ScriptHelper.cs:166-169` et `:171-182` de la décompilation sont faux (signe étendu de la moitié basse ; `(byte)(value & 0xFFFF)` tronque à 8 bits) ; `:126-129` est juste.
   - Pas de partenaire X écrit pour le coup (voir « §7 Caméra »).
   - `SlideEffectId` n'est pas lu par la DLL (`rg SlideEffect` dans `Alundra`/`Alundra.Tests` : rien). L'export de projet `alundra-project/Maps/*.tmj` le porte comme propriété.

### §2 Poussée

- JUSTE (relu ligne à ligne) : `PreviousAdjustedForceX/Y` `EntityScriptProxy.cs:341-342`, `Clone :2638-2639`, aucun consommateur ni écrivain. `rg` ne trouve que ces lignes, le commentaire `ScriptedMotion.cs:609` et quatre lignes d'oracle (`AlundraBeannoideOracleTests.cs:93-94`, `AlundraMurutaOracleTests.cs:92-93`).
- JUSTE : l'ordre de `RunOneKinematicTick` (`ScriptedMotion.cs:614`) :
  - vitesse cible `:625-639` ;
  - force `:665-666` ;
  - `Adjusted :670-671`, `Final :672-674` ;
  - flux du porteur du héros `:677-681` ;
  - borne de carte `HasLogicalZ` `:684-691` ;
  - déplacement `:700-709` ;
  - drapeau de borne `:712-715` ;
  - `LastTickDelta :717-718`.
  - `ForceAdjusted` : `:188`, `EntityScriptProxy.cs:2435`, `SlideAlongWall` (`:825`, `:840`, `:847`, `:853`, `:883`, `:891`), `:1718`, `Clone :2666`.
- JUSTE : atteignabilité.
  - `TickPlayer :88` via `PlayerManager.Tick :1839-1840` (héros, `EntityScriptProxy.cs:1616-1622`).
  - `TickScriptedNpc :171` (non-héros, `Status.IsActive()`, `:1629-1632`).
  - `TickPhysics :1602-1611`, `WorldProxy.RunEntityBlock :2976-2980`, `DrivesEntityTicks :821`.
  - Boucle autonome `EntityScriptProxy.cs:1527` et `:1546`.
  - `CollectTickEntities :2865-2885`, porte fermée `:2907-2916`.
- JUSTE : trois familles d'entités.
  - Carte : ni `JoinsBirthTick` ni `HasLogicalZ` (déclarations `EntityScriptProxy.cs:1375`, `:1383`).
  - Opcodes `0x2D/0x8A/0x8B` : `JoinsBirthTick` seul (`EventProgramRunner.cs:2378`).
  - Interface : les deux (`WorldProxy.cs:3572-3573`).
- JUSTE (FAIT, binaire, relu) :
  - `0x800366FC` a un seul appelant, `0x80036B9C`. `scan_jal` plus balayage des mots de données et des paires `lui`+immédiat : aucune référence indirecte. `0x80036828` n'est appelé qu'en `0x800383EC`.
  - Le héros (`bne s0, s2, 0x80036A84` non pris) y arrive par `j 0x80036B9C` en `0x80036A7C`.
  - Formule `0x80036710-0x80036764` : `((Force + table[+0x188 & 0xF]) >> shift) + poussée`, X avec `+0xCC`, Y avec `+0xD0`.
  - Remises à zéro en `0x80036750` (`+0xD0`) et `0x80036754` (`+0xCC`) avant les bornes ; poussée non décalée.
  - Borne de carte `0x80036768-0x800367D4` avec `+0x13C = 1`, pour tout le monde.
  - Branche porteur `0x80036A84-0x80036AB4` : `+0x28 != 0` met à zéro `+0xB8`, `+0xC4`, `+0xC8`, `+0xDC`, `+0xE0`, `+0xE4`, `+0xE8`, `+0xEC` et saute à `0x80036BBC`. Ni application ni remise à zéro de la poussée.
  - Le héros n'a **pas** la garde `+0x28`.
- JUSTE : `SlideAlongWall` (`ScriptedMotion.cs:781-892`) lit `FinalForceX/Y` et la direction (`:786-813`, sortie `:810`), `NoObstacleSlide :819`.
- JUSTE : les préfabs des sorts ont un `CharacterControllerComponent`.
  - J'ai relu les 11 types porteurs par `AssetInfos.json` + `.entity` (2 occurrences du nom) pour 16 « Magie de terre Niv.1 », 17 « Niv.2 », 19 « Magie d'eau Niv.2 », 21 « Magie de feu Niv.2 », 23 « Projectile de magie de feu Niv.1 », 24 « Magie de vent Niv.2 (bas) », 150 à 153 « Projectile (ligne droite/chute/parabolique/boomerang) ».
  - **Complément (FAIT)** : le sprite **154** (`bank_alundra_154`, C65, E1) n'a **aucun** `CharacterControllerComponent` (0 occurrence). C'est le « préfab sans contrôleur » qui intègre `PosX/Y` directement (`ScriptedMotion.cs:707-708`). Il est dormant : aucune des 484 cartes ne le place (`e20-h4/check.md` §2 item 2).
- JUSTE : borne de carte non appliquée au héros ni aux entités de carte (`ScriptedMotion.cs:609-612`, `:685`), épinglé par B3 (`AlundraMapEdgeWorldTests.cs:213`) et B4 (`:275`).
- JUSTE : sites C65 `0x8007B004/0x8007B018` et C75 `0x8007BEB8/0x8007BED4`, `0x8007C000/0x8007C004`.
  - C65 : Y = `sin ≪ 9` en `0x8007B010`, X = `cos × 3 ≪ 8` en `0x8007AFF8-0x8007B000`, relus. Les tables étant d'amplitude 256, les poussées font 3 px en X et 2 px en Y.
  - La poussée de C75 est une différence de positions : cible moins position de l'orbe.
- JUSTE : décompilation `PreviousAdjustedForce` écrite en 27 lignes de `FunctionTypeC.cs`, consommée en `PhysicsEngine.cs:1518-1522`. Complément : 2 lignes de plus dans `FunctionTypeD.cs`, 6 dans `Gameplay/Entity.cs`, 4 dans `PhysicsEngine.cs`.

### §3 Indice du plus haut emplacement

- JUSTE (binaire relu) : `0x80038634` parcourt 64 cases.
  - Statut 4 → recopie du modèle `0x80134368` et écriture de l'indice en `+0`.
  - Statut ≠ 0 → `t2 = indice` ; `sw t2, 0x4360` en `0x800386CC`.
  - Raise à la naissance `0x80039D24-0x80039D3C` (`slt` puis `sw`). Remise à 0 en `0x8003B2D8`.
  - Porte `[0x800DC4B8] & 0x48` en `0x8003B38C-0x8003B398` avant l'appel `0x8003B3A0` : porte fermée → saut vers `0x8003B408`, ni recalcul ni recyclage.
  - La requête balaie `1 .. count − 1` (`0x8003AF98`, `0x8003B0A8`) ; les annulations (ex. `0x80034E60-0x80034EB4`) vont de 1 à `count` inclus.
  - Mon balayage de `0x80134360` : 50 références, dont 3 écritures (`0x800386CC`, `0x80039D3C`, `0x8003B2D8`) : JUSTE.
- JUSTE : `EntitySlotTable.cs` (64 cases `:17`, `_held :19`, `HeldCount :22`, `IsHeld :25`, `Allocate :28-41`, `HoldHeroSlot :44`, `Release :47-54`).
  - `WorldProxy.HighestLiveSlot :182-194` est privé et sans test ; usage unique `ReportSlot :264`, sonde `SlotShadow` (`Probes/SlotShadow.cs:15`).
  - `InsertBySlot :216-232` ; `RecycleDestroyedEntities :3265-3289` (porte `:3267`, `Release :3284`) ; appel `:2557/:2572`.
  - Trois `Allocate` `:1074`, `:3399`, `:3528` ; héros `:2302-2304`.
- JUSTE (INFÉRENCE du rapport, maintenant vérifiée) : l'équivalence « plus haut emplacement tenu = `[0x80134360]` ».
  - (i) La table tient une case dès `Allocate` (naissance) et la libère au recyclage du statut 4 (`FlagToDestroy`), seuls instants où le binaire relève ou recalcule.
  - (ii) `Status` ne passe à `Destroyed` qu'en `WorldProxy.cs:3281`. `FlagToDestroy` n'est posé qu'en `WorldProxy.cs:3304` et `:3331`.
  - (iii) La passe de recyclage est par tick (dans la boucle `for (tick …)` de `:2524`), comme celle du binaire.
  - Écarts réels : `AbortCreation` (`:238-255`) et `SlotTableFactoryForTests` (`:176`), cités. En plus, la DLL ne recycle que si `PlayerEntity != null` (`:2557`), alors que le binaire n'a pas ce test.
- JUSTE : `IEntityWorldContext` n'a ni indice ni requête (`SpawnedEntities :35` seul, instantané alloué `WorldProxy.cs:3657-3670`).
  - Membres par défaut déjà présents `:81`, `:116`, `:193`.
  - 38 classes de test implémentent l'interface (35 fichiers, comptage exact).
  - **Complément** : deux classes de production aussi, `WorldProxy` et `NoOpEntityWorldContext` (`Maps/NoOpEntityWorldContext.cs:14`).

### §4 Requête et tri

- JUSTE (binaire relu en entier, `0x8003AF70-0x8003B0E4`) :
  - exclut l'appelant (`a0`) ;
  - statut 2 ou 3 ; `+0x20 == 0` ; `(+0x6C & masque) != 0` ; `+0x1CC == 0` et `+0x1D0 == 0` ; `(+0xB4 & 0x40) == 0` ;
  - fiche `+0x1C4` non nulle et `(octet[fiche + type + 2] & 0xC0) != 0x80` ;
  - distance² sur `(Δposition) >> 16`, deux tableaux (pointeurs, distances) ;
  - tri `jal 0x8003AE04` seulement si au moins 3 résultats.
  - Appelants de la requête : `0x80035FA4` (vent 1), `0x8007ACB4` (C63), `0x8007AF5C` (C65), `0x8007BF20` (C75), aucune autre référence.
- JUSTE : le tri `0x8003AE04` est le Hoare de `check/o/my_query.py`.
  - Pivot `(clé[bas] + clé[haut]) >> 1` logique (somme sur 32 bits non signée, `srl`), balayages `sltu` croisés, échange clé et pointeur puis `i++/j--` et reprise.
  - Récursion `(bas, i−1)` si `bas < i−1`, puis `(j+1, haut)` si `j+1 < haut`.
  - Le balayage `1..count−1` n'atteint pas l'entité du plus haut emplacement.
- JUSTE : table des champs.
  - `Status :88`, `BlockedByEntity :99`, `Flags :156`, `DamagedTickCounter :483` (`+0x1CC`), `FrameCollisionTickCounter :484` (`+0x1D0`).
  - `HitAnimByte HitPass.cs:30`, `BalanceRecord HitPass.cs:24`, `ByteAt BalanceRecord.cs:76` (lecture de l'enregistrement suivant `:78-82`), `PosX/PosY :351-352`.
  - `AnimFlags` une frame trop tard : `AlundraBirthTickTests.cs:485` puis `:495-496`.
- Complément (omission) : la DLL a déjà **les mêmes filtres** côté coups.
  - `Combat/HitPass.cs:122-131` : `FrameCollisionTickCounter`, `DamagedTickCounter`, `HitAnimByte & InvulnerableAnimationBit (0x40, :31)`, `Flags & mask`.
  - `:149` : `ByteAt((attack.Attribute & 0xF) + 2)` et `& 0xC0 != 0x80`. `Combat/Damage.cs:31` : même lecture de fiche.
  - Une liste sans allocation : `WorldProxy.BuildHitList :3058-3077` (statut actif, `BlockedByEntity == null`, héros d'abord, ordre des emplacements).
  - La requête gagne à réutiliser `InvulnerableAnimationBit` et la lecture de fiche. Elle doit se garder de `_updateProxies`/`_hitList` pour les gestionnaires d'un tick : ces listes sont reconstruites à l'étape « lists », donc en retard d'un tick pour la passe des programmes (INFÉRENCE d'après `WorldProxy.cs:2962-2968` et `:3689-3699`).

### §5 Tables 256

- JUSTE (FAIT, `tools/tables_check.py`) : `0x800204F0` sin et `0x800206F0` cos, 256 `short`, amplitude 256.
  - `sin[1] = 6`, `sin[64] = 256`, `sin[128] = 0`, `sin[192] = −256`.
  - `trunc(256·sin)` égale 256 entrées sur 256 (`round` : 136) ; `cos[i] = sin[(i+64)&255]` ; `sin[(i+128)&255] = −sin[i]`.
- JUSTE : seuls C65 (`0x8007AF74/0x8007AF7C`, indice `((dir + 0x14) & 0x1F) << 4` octets = `8·idx` demi-mots) et C75 (`0x8007BE54/0x8007BE78`, indice `a & 0xFF`) lisent les tables (balayage `lui`+immédiat : 4 références).
  - `rg 800204F0|800206F0|sin256|SinTable|CosTable` dans la DLL et les tests : rien.
  - `AnimationTables.cs` : `OffsetXList :71` (768), `OffsetYList :82` (512) ; consommateurs `ScriptedMotion.cs:632-633`, `EventProgramRunner.cs:2985-2986`.
  - Décompilation : `g_sinus` `StaticVariables.cs:10930` et `g_cosinus :10998`, amplitude 512 (`0x0200`), utilisées en `FunctionTypeC.cs:16886-16887` : JUSTE (écart D13).

### §6 Accès 32 bits

- JUSTE : `Bytes byte[4] :1227`, `DelayOrAngleOrEntityId :1228`, `ItemState :1229`, `AIValues short[10] :1230` ; `Clone :2716-2719` ; `AlundraEntityScriptProxyCloneTests.cs:41-42`, `:55-59`.
- Utilisateurs de `Bytes` et `AIValues` : listes du rapport relues, exactes (Muruta, Beannoïde, `EnemyHelpers.cs:221,234`, `SaveBook.cs:164-171`).
  - `AIValues` : indices 0 et 1 seulement. `ItemState` : sans utilisateur.
  - `DelayOrAngleOrEntityId` : `SaveBook.cs:264,273-274,322,332-333` et `AlundraSaveBookTests.cs:350-353`.
  - Aucun test ne lit `Bytes`/`AIValues` par réflexion. Pas de `Save`/`Load` redéfinis dans `Alundra/Entities`.

### §7 Caméra

- JUSTE (binaire relu) : suivi `0x8002CDA0-0x8002CEA4`.
  - `jal 0x8002C894` ; cliché `[0x800E42B8]` remis à 0, `camX = lookX − 0xA0`, `camY = lookY − lookZ − 0x88`, saut à la queue commune `0x8002CE80`.
  - Sinon `camX += ((lookX − (camX+0xA0)) >> 4) + OffsetX(+0x14) + [0x800DD870]` (lu `0x8002CE2C`) et `camY += … + OffsetY(+0x18) + [0x800DD874]` (lu `0x8002CE6C`).
  - Remises à zéro de `[0x800DD870]` en `0x8002CE8C` et de `[0x800DD874]` en `0x8002CE98`, cliché compris, avant la borne.
  - Charge de carte `0x8002CD54` : `Flag = 0`, cliché armé, `[0x800DD868] = 0x3C`, `[0x800DD874] = 0` (`0x8002CD7C`), `[0x800DD870] = 0` (`0x8002CD8C`).
  - `[0x800DD874]` : écriture unique hors remises, `0x8007F350` (E13, `$v0 = 2` posé en `0x8007F324`). `[0x800DD870]` : 3 références, dormant (balayage `lui`+déplacement).
  - Bloc `0x800E4338` : mots `+0` à `+0x20` = Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY. FAIT : relu dans `0x8002C894-0x8002C9D4`, même ordre que `CameraSway.cs:33-41`, que le rapport posait par cohérence.
  - Tampon `0x80127138` : quatre sites `0x80034E14`, `0x80035D04` (capture), `0x80035D88`, `0x80035E28` (restaurations), 9 mots, sans garde.
- JUSTE : DLL `CameraSway.cs:26,33-41,45,55,58,65,116` ; `CameraDirector.UpdateCameraFollow :214-281` (`:222`, `:224-236`, `:262`, `:268-270`, `:272-275`).
  - `CameraMath.AdvanceCameraSmoothing :167-194`, `ComputeSmoothedCameraTarget :216-228` (`:222-223`), `ToOriginalScrollSpace :301-302`.
  - Suivi par frame après la boucle des ticks `WorldProxy.cs:2716-2720` ; `OnMapLoad :880` ; `CameraSway :699-701`.
  - La secousse Y vaut −2 en espace de rendu pour un coup de 2 (JUSTE).
- Complément : autres utilisateurs de `CameraSway` : `EventProgramRunner.cs:1580-1598` (opcodes `0x8E/0x8F`, par `_worldContext.CameraSway`, avec un journal d'opcode dégradé si l'interface rend null).

### §8 Registre

- JUSTE : `NativeSpriteRegistry.CreateForProduction :36-43` (7 clés : (A,1), (C,1), (D,1) pour 335 ; (A,0), (C,10), (D,9) pour 347 ; (C,61) pour 27, 28, 29), `Count :46`, `Register :54-67` (doublon : `InvalidOperationException :62`), `TryGet :70`, `Dispose :95-98`.
  - E0, E1, E10 en ligne (`EventProgramRunner.cs:283-292`, prédicat `:328`).
  - Types `index | 0x100` (`EntitySpawnFactory.cs:541`, et la voie des enregistrements `:678`).
  - Le sprite 19 porte A=254, C=75, E=0 ; 23 porte A=254, C=63, E=3 ; 24 porte A=254, C=65, E=1 ; 154 porte C=65, E=1.
- JUSTE : tests qui bougent : `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`), `AlundraWaveSwordLifeTests.cs:182-183` (`3 + 3 + 1`, `registry.Count`).
  - Neutres : `AlundraNativeSpriteRegistryTests.cs:84-96`, `:205-236`, `:238-251` ; `AlundraHitArena.cs:70` ; `AlundraBirthBoxTests.cs:131` ; compteurs d'exécution `AlundraBeannoideOracleTests.cs:263,330`, `AlundraMurutaOracleTests.cs:316,398`, `AlundraMurutaHallArcTests.cs:100`.
  - L'option (b) du rapport (inscription cumulative, `docs/plan-e14-ia-native.md:881-882` et `:903`, tranche E14.b0) change bien la sémantique épinglée par `:238-251`.

## 3. Faits nouveaux et points INCONNU du rapport que je peux trancher

1. **Branche porteur (INCONNU du rapport, §11 point 3).**
   - FAIT : dans la DLL, `PlatformEntity` (`EntityScriptProxy.cs:101`, `//28`) n'est **jamais assigné** (`rg "PlatformEntity\s*="` : seul `Clone :2609`).
   - La doc de `EntitySupport.cs:68-73` le dit : « always null in this runtime's current scope (the carried/thrown relation, out of scope) ».
   - La relation de portage `+0x28` n'a donc aucune représentation vivante dans la DLL, pour aucune famille d'entités (héros, carte, interface). L'absence de la branche `0x80036A84` est neutre tant que cette relation reste hors périmètre.
   - Le porteur « de support » de la DLL est `RidingEntity` (`+0x12C`, assigné `EntitySupport.cs:287`, `:305`), relation distincte.
2. **Pas de pincement de Z sans force XY** : `SnapHeroAtApex` et `SnapAtApex` (voir §1 point 5). La poussée les rend actifs pour une entité à force propre nulle, ce que fait le binaire.
3. **Le terme de table `+0x188`** est absent parce que `TileAttributes` n'est jamais écrit : voir §1 point 4. Le rapport écrivait « n'est pas porté » (JUSTE) sans mesurer son poids.
4. **S5, barrière de câblage (omission).**
   - `Hero/PlayerManager.cs:1226` (`ProductionWiredWeaponTypes = {27, 28, 29}`) et `UnhandledWeaponSpriteCodes :1240-1264` : un type n'est câblé que si ses codes C et E sont gérés (clé du registre pour **ce type**, ou code E en ligne).
   - Épinglé par `AlundraWeaponSpawnSeamTests.cs:243-244` (`Assert.Equal({27,28,29}, ProductionWiredWeaponTypes)` et `Assert.Empty(UnhandledWeaponSpriteCodes(...))`) et `AlundraWaveSwordLifeTests.cs:188`.
   - Conséquence pour l'inscription commune (Q-H4-8) : la clé (E,3) doit lister **exactement** les types câblés (16, 23 et 150 à 153 si les ennemis en dépendent). Le test S5 bougera à h4c, pas à h4b.
   - Les codes A=254 des sprites 19, 23, 24 ne sont pas vérifiés par ce contrôle (A hors périmètre du contrôleur) : un (A,254) absent reste un no-op compté et journalisé.
5. **Précédent d'ordre** : la liste des coups et `CollectTickEntities` suivent l'ordre des emplacements. Le « premier attaquant » de la passe de coups en dépend (`WorldProxy.cs:2862-2863`) : toute requête qui itère doit lire `_spawnedEntities` dans cet ordre.

## 4. Omissions : utilisateurs des champs

| Champ ou symbole | Lecteurs de production non cités par le rapport |
|---|---|
| `ForceAdjusted` | `Hero/PlayerManager.cs:398`, `:811`, `:877` (rebond du sprint) ; `Ai/Enemies/MurutaAi.cs:332` ; `Ai/Enemies/BeannoideAi.cs:115` ; `Events/EventProgramRunner.cs:725`, `:741`, `:748`, `:776`, `:781`, `:2030`, `:2953` (le rapport cite seulement « opcodes de marche (doc) ») ; `EventOpcodeSizeTable.cs:71,117` (noms). Écrivains : `ScriptedMotion.cs:188`, `:714`, `:825`, `:840`, `:847`, `:853`, `:883`, `:891`, `EntityScriptProxy.cs:2435`. |
| `FinalForceX/Y` (XY) | `ScriptedMotion.cs:421` et `:564` (portes « force XY nulle »), `:679-680`, `:687-690`, `:702`, `:707-708`, `:784-785` ; tests : `AlundraHeroApexAndCeilingTests.cs:277-278,306,310,350`, `AlundraLogicalZStepTests.cs:306-307,335`. |
| `AdjustedForceX/Y` | écrits `ScriptedMotion.cs:670-671`, lus `:672-673`, copiés `Clone :2642-2643`, mots « intouchés » des oracles `AlundraBeannoideOracleTests.cs:95`, `AlundraMurutaOracleTests.cs:94`. |
| `HitAnimByte` | `Combat/HitPass.cs:128` ; tests `AlundraHitPassRulesTests.cs:352-354`, `AlundraBalanceTablesTests.cs:55,60`, `AlundraSpawnBySpriteIndexTests.cs:40`, `AlundraHitArena.cs:230`. |
| `Slot` | écrit `WorldProxy.cs:208`, `:250`, `:2302`, `:3285` ; lu par `InsertBySlot :223` et `HighestLiveSlot :187` seuls. |
| `CameraSway` | `EventProgramRunner.cs:1580-1598` (opcodes `0x8E/0x8F`), `CameraDirector.cs:221`, `CameraMath.cs:169` ; arcs `AlundraDay3SceneArcTests.cs:366`, `AlundraShipBlockArcTests.cs:66,77`, `AlundraShipCorridorArcTests.cs:52`. |
| `PlatformEntity` | jamais assigné ; lu `EntitySearchService.cs:163`, `EntitySupport.cs:76`. |
| `IsLoadedNormalOrDeactivated` | `EntityScriptProxy.cs:32-33` (statuts 1, 2, 3) ; différent de la requête du binaire (2, 3). |

## 5. Omissions : tests qui épinglent (et lesquels ne bougent pas)

- Poussée : en plus de la liste du rapport, des passes de mouvement réelles sont exercées par `AlundraMapStateTests.cs:145` (`TickScriptedNpc`), `AlundraInteractionPassTests.cs:122`, `AlundraLadderClimbTests.cs` (`TickPlayer`), `IntroTraceHarnessTests.cs` (production), `AlundraHeroApexAndCeilingTests.cs:277-350` (portes XY du héros). Aucun ne pose de poussée : à poussée nulle ils ne bougent pas (JUSTE). Toujours vrai : aucun test ne pose la poussée avant un tick réel.
- Requête : `EntitySearchServiceTests.cs` (20 appels) épingle la requête canonique, à ne pas modifier.
- Tables : `AlundraEventProgramRunnerTests.cs:2133`, `AlundraNpcCharacterControllerMoverTests.cs:382,424,658,1204,1887,2033,2106,2489`, `AlundraRecordBirthTickTests.cs:396` épinglent `OffsetXList/OffsetYList` ; neutres.
- Registre : S5 (voir « §3 Faits nouveaux », point 4) en plus des deux épingles de compte.
- Réinitialisation de session : voir §1 point 2.
- Aucun test ne lit les types par réflexion (`typeof(EntityScriptProxy)` n'apparaît que pour `GameplayProxyClassName`).

## 6. INCONNU restant

- INCONNU : le détail du pilote du vent 1 (`0x80035FA4`) ; les entités de sprites 16 à 24 nées par script ou par l'interface à l'exécution (les placements statiques sont comptés au §1 point 1) ; les sons et les pistes XA.
- INCONNU : l'effet d'une poussée sur `SlideAlongWall` pour l'orbe C75 en vol (à mesurer) ; le sens binaire du bit `0x2000`.
- INFÉRENCE non vérifiée : valeurs de `+0x188` limitées à 1, 2, 4, 8 (lecture partielle de la fonction).
- INFÉRENCE : le comptage des écritures de `+0xCC/+0xD0` est un balayage par déplacement (coïncidences possibles) ; les six fonctions sont délimitées par une heuristique de prologue.

## 7. Fichiers (dossier `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4b/dll-check/`)

- `check.md` (ce rapport).
- `tools/` :
  - `exelib.py`, `lin.py`, `scan_jal.py`, `scan_refs.py` (copies de l'annexe `e20-h4`).
  - `chk_push.py` (accès `0xCC/0xD0`), `chk_disp.py` (déplacement quelconque), `cite_bounds.py` (bornes des citations), `placements.py` (couples contre placements), `tables_check.py` (tables 256 et tables de forces).
  - `verify_cites_copy.py` (copie du vérificateur du rapport).
  - Rejouer : `python -B <script>` depuis `tools/`.