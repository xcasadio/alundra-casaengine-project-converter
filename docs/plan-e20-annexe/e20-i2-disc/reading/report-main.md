# E20.i2 : lecture au binaire et à la DLL (lecture seule, 2026-10-25)

Rien n'a été écrit dans le dépôt, aucun build ni test dotnet n'a été lancé. Tout ce qui est marqué [MESURÉ] a été rejoué sur le vrai code de `ALUN_CD.EXE` (France), depuis une copie du harnais `e20-d` dans le scratchpad. Le source de la DLL a été lu au HEAD `8174c0cb`. Le HEAD a avancé de deux commits d'E20.b2, qui ne touchent que des tests, donc `Alundra/` est inchangé.

Les chemins du plan datent d'avant la restructuration :

| Ancien nom | Chemin au HEAD |
|---|---|
| AlundraScriptedMotion | `Alundra/Physics/ScriptedMotion.cs` |
| AlundraCellsCollisionField | `Alundra/Physics/CellsCollisionField.cs` |
| AlundraPlayerManager | `Alundra/Hero/PlayerManager.cs` |
| AlundraEntityScriptProxy | `Alundra/Entities/EntityScriptProxy.cs` |
| AlundraWorldProxy | `Alundra/Maps/WorldProxy.cs` |
| AlundraTestSaves | `Alundra/SaveGames/TestSaves.cs` |

## 0. Synthèse

- E20.i2 recouvre quatre morceaux indépendants du binaire.
  - Le mur d'eau profonde est propre au héros. Il se règle au test de murs des coins, hors de la passe des forces.
  - La fonction `0x800366FC` fait en un seul bloc la table de glissement, le décalage `SlideEffectId`, la poussée `+0xCC/+0xD0` et le serrage à la carte.
  - Le calcul de `+0x188` (courants et case verrouillée) est dans `0x80038064`, pour toute entité de la liste physique.
  - La branche `+0x188 & 0x80` de `MovePlayer` (`0x80032768`) est la « case de sortie » O-H-2, laissée hors de c2b par D-C2B-14.
- Le plan sous-estime le bloc. La « vingtaine de lignes » de Q-H4B-6 ne vaut que pour la fonction des forces. Les courants ajoutent une sonde par entité, la branche verrouillée de `MovePlayer` et un oracle neuf.
- Les courants ne sont pas dans « 16 cartes » seulement. Il y a des cases de courant dans 44 cartes (5 539 cases). Seize cartes ont des courants sur eau profonde, c'est le compte du plan. Les 28 autres sont des courants à sec (tapis, escaliers, sable). La case verrouillée existe dans 23 cartes.
- Seule la poussée bloque h4e2 et h4f1. Le mur et les courants n'y changent rien.
- Aucun changement du convertisseur ni du moteur n'est nécessaire : l'octet Slope des cases, la propriété `SlideEffectId` et le champ de cases sont déjà exportés.

## 1. Faits

### A. Binaire

**A1** [FAIT] Ordre de la passe physique `0x8003836C` :
- remises à zéro de `+0x168/+0x140/+0x13C`, et mots modifiés `+0x1D8..+0x1E0` ;
- passe des porteurs `0x800364C8` ;
- passe des forces `0x80036828` ;
- ajout du porteur `0x80037364` ;
- déplacement `0x80037E34` ;
- puis `UpdateTileAttributes` `0x80038064` pour chaque entité (`0x800384B8-0x800384BC`).

`0x80038064` est aussi appelée à la naissance (`0x80039F30`). `+0x188` calculé au tick N est lu au tick N+1 (retard d'un tick).

**A2** [FAIT] `0x80036828` :
- Le héros prend la branche `0x80036884-0x80036A80`, puis saute à `0x80036B9C`.
- Un non-héros porteur (`+0x28 ≠ 0`) a ses forces mises à 0 et sa poussée n'est ni consommée ni remise à zéro.
- Les autres font `IncrementForce` puis `0x80036B9C`.
- `0x800366FC` n'a qu'un site d'appel, `0x80036B9C` (`out_callers.txt`).

**A3** [FAIT] `0x800366FC` (désassemblé) :
- Calcul : `adj = ((F + tab[idx]) >> sh) + push`, avec `idx = (+0x188) & 0xF`.
- Tables X `0x80028B94` et Y `0x80028BD4`, 16 mots chacune (`out_drift_tables.txt`).
  - Les indices 1, 2, 4, 8 donnent Y −40960, Y +40960, X −61440, X +61440. Ce sont les seuls que la sonde écrit.
  - Les indices combinés existent aussi, dont X ∓983040 aux indices 7 et 11.
- `sh` est l'octet signé `[[0x800E4334]+8]` pris avec `& 31` (`lb` en `0x80036744`, `srav` en `0x8003675C` et `0x80036760`).
- La poussée n'est pas décalée. La décompilation (`ForceX + (table>>sh) + prev`) se trompe dès que `sh ≠ 0`.
- La poussée est lue puis remise à zéro (`0x80036750`, `0x80036754`).
- Le serrage à `[+0x108/+0x10C ; +0xFC/+0x100]` est strict. Il lève `+0x13C` en `0x8003679C` et `0x800367D4`.
- Sorties : `+0xDC/+0xE0`.

**A4** [FAIT] L'octet `SlideEffectId` n'est lu qu'en `0x80036744`. Parmi 22 lecteurs de `[0x800E4334]`, aucun autre ne lit `+8` (`out_scan_4334.txt`).

**A5** [FAIT] `+0x188` n'a que quatre écritures, toutes dans `0x80038064` (`0x80038268`, `0x8003832C`, `0x80038338`, `0x8003833C`), et deux lectures :
- `0x80031CB0`, dans `MovePlayer` ;
- `0x80036710`, dans la fonction des forces.

Les autres résultats du balayage brut du déplacement `0x188` sont d'autres structures. J'ai vérifié `0x8007F540` (base = entité+0x68, boîte du gestionnaire E17). Les trois résultats en `0x8009xxxx` ne sont pas inspectés (`out_scan_188.txt`).

**A6** [FAIT] Décodage de `0x80038064`, gravité (`+0x6C & 0x100`) :
- La case lue est celle du point `(PosX, PosY)` du héros, pas des coins : colonne = table de pixels `0x80127168` bornée à 0..0x33, rangée = `PosY>>20` bornée à 0..0x3B. Le mot de case est `Walk | GP<<8 | Slope<<16 | Height<<24`.
- Une case de courant est une case dont `word & 0xC00000 ≠ 0` (octet Slope `0x40` ou `0x80`).
- Test de niveau : `((word & 0xFF000000)>>4) + 1 == +0x1E0` (hauteur brute de la case contre `PosZ+ModZ`).
  - Niveau égal : il faut le bit Slope `0x04` (`0x40000`).
  - Niveau différent : il faut le bit Slope `0x08` (`0x80000`).
- Le résultat vaut `1 << ((word>>20)&3)`, plus `0x80` si le bit Slope `0x80` est posé. La direction est donc dans les bits 4-5 de l'octet Slope.
- Sans gravité (`0x80038274`), il faut `0xC00000` et `0x80000` ensemble, sans test de niveau.
- Cela lève le doute de `swim-binary.md` §7 sur la convention des octets 2 et 3.

**A7** [FAIT] Branche verrouillée `0x80032768`, atteinte depuis `0x80031CB8` après la blessure et le test des PV, avant le pilote d'objet `0x8003634C`. Elle fait, dans cet ordre :
- charge `[0x80126FFC]` = 0 ;
- annulation de l'objet `0x800350C0`, avec `[0x80127110]` = 0 dans le delay slot ;
- `0x8002F884(1)` ;
- pente 4 → état `0x1D` ;
- sinon, au sol → Idle (`0x80031E7C`), en l'air → `0x2D`.

Puis elle finit le tick (`0x80032914`) sans lire le pad.

**A8** [FAIT] Mur d'eau profonde, test de coins du héros `0x80037488` :
- Le déplaceur choisit ce test pour le héros en `0x80037764` et `0x800373E4` pour les autres. L'appel est indirect, aucun `jal` ne l'atteint.
- Le test est exécuté pour les quatre coins du candidat.
- Un coin est bloqué si le demi-mot de la case (`Walk | GP<<8`) a un bit du masque (`0x40`, `0x41` avec le drapeau 8, `|0x1000` avec le drapeau 1), ou si le niveau de la case est supérieur ou égal à `ModdedPosZ`.
- Il est aussi bloqué si `[0x80127000] < 2` et `(demi-mot & 0xE00) == 0x800`, sans condition de hauteur (`0x80037504`, `0x80037558-0x80037560`).
- La cape (`[0x80127164] == 0x20`) ajoute un confinement au sable (`0x80037568-0x800375A0`). C'est h2, pas i2.
- Un court-circuit total existe en `0x80037490-0x800374AC` : il se déclenche quand `[0x800DC058]` est négatif et `[0x800DC05C]` a son bit 31. Il est non porté, pour les murs ordinaires comme pour l'eau [INFÉRENCE sur sa cause : transition d'écran].

### B. Mesures sur le vrai code

**M1** [MESURÉ] Traces du mur déjà dans l'annexe :
- `W1-walk-L0` : x = 325,00 au tick 11, `ForceAdjusted` au tick 12.
- `R9-deepwater-noboots` : x = 325,00 au tick 84, `ForceAdjusted` au 86, rebond `0x3E` au 87.
- `W2-L0-inside-R` : Alundra démarre dans l'eau, sa position reste 624,00 et `ForceAdjusted` vaut 1 dès le tick 5, alors que `ForceX` monte de 6912 par tick.

**M2** [MESURÉ] `s5_belt_probe.py` et `s6_belt_probe2.py` (harnais de e20-d, cases à octet Slope choisi) :
- Courant est à sec, bit `0x04`, au niveau : `+0x188` = 8. Alundra immobile dérive de 61 440 par tick (0,9375 px), dès le tick 1.
- Bit `0x08` seul, au sol : aucune dérive. Bits `0x0C` : dérive. Aucun bit de niveau : aucune dérive.
- Les quatre directions : bit 0 = Y −40 960, bit 1 = Y +40 960, bit 2 = X −61 440, bit 3 = X +61 440.
- En marchant, `adj` = force + dérive : 159 744 + 61 440 = 221 184 vers l'est, −159 744 + 61 440 = −98 304 vers l'ouest.
- Avec `SlideEffectId` = 1 :
  - la dérive est divisée par 2 (30 720) ;
  - la marche aussi (`adj` 79 872 pour `ForceX` 159 744) ;
  - la poussée, elle, n'est pas décalée (T4).
- Case verrouillée (`0xF4`) : `+0x188` = `0x88`. Alundra reste en Idle malgré Droite tenu, et le saut à la Croix est ignoré.
- Même chose sur eau profonde : état `0x1D` forcé, pas de plongée au Triangle.
- Eau profonde avec courant :
  - flotter dérive de 61 440 ;
  - nager avec le courant donne `adj` 172 032 ;
  - nager contre donne −49 152.
- En l'air au-dessus d'une case à bit `0x08` seul, la dérive agit pendant le saut puis s'arrête à l'atterrissage.
- La dérive s'arrête un tick après la sortie de la case : l'attribut se recalcule quand l'origine change de colonne.

**M3** [MESURÉ] `s9_belt_target_push_probe.py` :
- Le Beannoïde inerte de l'oracle, posé sur une case de courant, dérive exactement comme Alundra (`adj` 61 440 par tick).
- Une poussée écrite à la main sur le héros ou sur lui est ajoutée une fois au tick suivant, puis remise à zéro. Exemple : 131 072 donne +2 px.

**M4** [MESURÉ] `s7_belt_wall_probe.py` : sous un courant sud contre un coin de mur, le glissement le long du mur dépend du regard.
- Regard est (24) : Alundra reste bloquée (`ForceAdjusted` = 1).
- Regard sud (0) : elle glisse de côté (x 500 → 514,25) puis passe.
- La DLL fait déjà ce choix par `TargetDirection` (`ScriptedMotion.cs:858`), donc pas de risque nouveau de ce côté.

**M5** [MESURÉ] Recensement (`s2_census_belts.py`, sur `data-extracted/`) :
- 44 cartes ont des cases de courant, 5 539 cases : 3 772 à sec et 1 767 sur eau profonde.
- Les 16 cartes à courants sur eau profonde sont : 3, 4, 5, 6, 8, 15, 19, 20, 49, 77, 78, 79, 335, 416, 435, 479.
- Les 28 autres sont à sec : 0, 10, 23, 97, 100, 116-118, 124, 125, 127, 128, 151, 157, 271, 289, 293, 297, 301, 324, 331, 351, 357, 403-405, 434, 481.
- Passerelles de niveau : 4 663 cases ont le bit `0x04` seul, 735 le bit `0x08` seul, 141 les deux.
- Cases verrouillées : 704 dans 23 cartes.
  - Grands champs : 125 (217 cases), 23, 100, 434, 351.
  - Cases isolées : une par carte dans 151, 271, 289, 293, 297, 301, 357, 481. Ce sont des cases d'escalier (Slope `0xA7`, genre 3, GP 16). C'est la « case de sortie » d'O-H-2.
- La cascade de la 416 : 8 rangées × 2 colonnes (x 14-15, y 35-42), de hauteur 4, 4, 3, 3, 3, 2, 2, 1.
  - Toutes ont le bit `0x04`.
  - Les rangées 36, 39 et 41 sont verrouillées.
  - Les rangées 36 à 42 ont aussi le bit `0x08`, y compris la rangée 41.
- `SlideEffectId` ≠ 0 : cartes 159 et 160 seulement (valeur 1).
- Entités posées (x, y seulement) sur une case de courant : 13 cartes (49, 77, 79, 116, 127, 403, 404 notamment).

**M6** [MESURÉ] `s8_portal_deep.py` : 76 portails sur 3 316 ont une tuile d'arrivée qui tombe sur une case d'eau profonde. Cette tuile n'est peut-être pas la position exacte d'arrivée [INFÉRENCE].

### C. DLL au HEAD

**C1** `RunOneKinematicTick` (`ScriptedMotion.cs:635-705`) pose `AdjustedForceX = ForceX` (l. 643).
- Rien de la table, du décalage ni de la poussée.
- Le serrage `ClampStepToMapEdge` (l. 666, définition l. 813) ne tourne que pour `HasLogicalZ`, après l'ajout des porteurs. Le binaire serre avant l'ajout du porteur. C'est un écart minime [INFÉRENCE].

**C2** `PeekAdjustedForce` (l. 782) appelle `PlanForce` seul : un passager lit donc la force d'un porteur sans dérive ni poussée.

**C3** `PreviousAdjustedForceX/Y` (`EntityScriptProxy.cs:346-347`) est le `+0xCC/+0xD0`, sans consommateur. `BeannoideOracleTests.cs:97` et `MurutaOracleTests.cs:96` ne le posent et ne le lisent qu'à zéro.

**C4** `TileAttributes` (l. 478) n'est jamais écrit ni lu. `PlayerManager.cs:68-69` et `:361` écrivent « NOT PORTED ». La doc de la nage (`:48`) liste le mur et les courants comme non portés.

**C5** Sondes de pente et de mots de case :
- Elles tournent pour le héros (`RunHeroProbes`, l. 1877), les entités marquées `HasLogicalZ` et le tronc (`TickMotion`, l. 1825-1835). Les entités placées par la carte ne sont pas sondées.
- `CellsCollisionField` expose `SampleGroundProperty`, `SampleRawWalkability` et `SampleRawCellHeight`, mais pas l'octet Slope (`_slope` privé, lu seulement `& 0x3`, l. 179).
- La convention de la DLL est `ModdedPosZ == TerrainHeight`, sans le `+1` du binaire.

**C6** Masque de marche du contrôleur du héros :
- `WalkabilityMaskFor(Flags)` (`CellsCollisionField.cs:147`) est posé à l'adoption (`WorldProxy.cs:2271`) et refait par `ResyncControllerFromFlags` (`EntityScriptProxy.cs:675-684`).
- Le champ teste `(walkability | gp<<8) & masque == 0` (l. 184-186).
- `BlockedCorners` (`ScriptedMotion.cs:983`) reconstruit le masque depuis `Flags`, pas depuis le contrôleur.
- `RunMontage.cs:63` (tests) pose le masque à la main : le montage ne voit que ce que le tick de production écrit.
- Le moteur transmet le masque tel quel (`CharacterControllerComponent.cs:1346`, `:1584`). `IsHorizontalMoveBlocked` bloque sur `!IsWalkable`, sans test de hauteur. La résolution du sol (`:1584`) ignore la marchabilité.

**C7** Mots de carte : `ResolveMapGravitySettings` (`EntitySpawnFactory.cs:888-895`) ne lit que `Gravity` et `ZViscosity`, rangés par entité (`EntitySpawnFactory.cs:707`, `WorldProxy.cs:2254-2266`). `SlideEffectId` serait un troisième mot aux mêmes deux sites.

**C8** La nage est faite : `SwimTick` (`PlayerManager.cs:673`), branche à la ligne 377. `HeroSwimArenaTests.WithoutTheBoots_TheHeroSwimsToo_AnAcceptedDeviation_O_E19_22` épingle l'écart du mur.

**C9** Pour la branche verrouillée, les briques existent : `CancelItemMode` (`PlayerManager.cs:758`) est un bouchon, `UpdatePlayerCarriedEntity(player, mode)` (l. 1413) est fait pour les modes 0, 1 et 2.

**C10** Le mur est donc un test sur le masque du héros, dans le champ de cases de la DLL, sans changement moteur. Le moteur ne lit que `IsWalkable`. Une égalité sur `0xE00` ne s'exprime pas par un bit de masque, d'où un pseudo-bit au-dessus de 16 bits (par exemple `0x10000`) que le champ interprète. Le piège de l'échelle (`0xC00 & 0x800 ≠ 0`) est évité par l'égalité.

### D. Plan

- D-E20-31, D-E20-43 et D-E20-61 sont tranchées (`plan-e20-heros.md:155`, `:186`, `:245`). Q-N3 et Q-I1 sont closes.
- La ligne d'enveloppe est à `:694`. Il n'y a pas encore de section détaillée.
- La cape (h2) : D-H2-4 et Q-H2-4 laissent la dérive du sable hors tranche (`:10985`, `:12145`). D-H2-6 prévoit un pseudo-bit « cape » dans le masque du héros, mis par une fonction par tick.
- O-H-2 (« à placer ») est à `:12233`. D-C2B-14 laisse la case de sortie hors de c2b.

## 2. Ce que la tranche doit faire : découpage proposé

La seule vraie dépendance est que la dérive passe par la fonction des forces. Le mur est indépendant de tout le reste.

### i2a : le mur d'eau profonde sans bottes (héros seul)

**Périmètre.**
- Pseudo-bit dans le champ de cases : bloqué quand le bit est dans le masque et `(tileFlags & 0xE00) == 0x800`.
- Une fonction par tick du tick du héros écrit ce bit dans `Controller.Settings.WalkabilityMask`, selon `BootsLevel < 2`.
- `ResyncControllerFromFlags` et `WorldProxy.cs:2271` ne doivent pas l'effacer.
- `BlockedCorners` lit le masque vivant du contrôleur.
- Un préréglage sans bottes (par exemple `mur-m14`) et sa doc.

**Fichiers.** `CellsCollisionField.cs`, `ScriptedMotion.cs`, `PlayerManager.cs` (appel dans le tick), `EntityScriptProxy.cs`, `WorldProxy.cs`, `TestSaves.cs`, `docs/test-saves.md`, tests.

**Oracles (déjà dans le dépôt).**
- `docs/plan-e20-annexe/e20-d/oracle/out/g9/R9-deepwater-noboots`.
- Dans `out-swim/` : W1 walk, run, jump et fall-walk en L0/L1 (8), W2-L0/L1-inside-R, W5-L0/L1-wall-walk et W5-L0-wall-diag, W10-shallow-to-deep-L0.
- Au total 15 traces, aujourd'hui « information » et non rejouées.
- Valeurs : voir M1.

**Tests qui bougent.**
- Le test d'écart cité en C8 s'inverse, avec sa doc (`HeroSwimArenaTests.cs:19` et `:143`).
- Les en-têtes de `HeroSwimTests.cs:23-24` et `HeroRunTests.cs:24` sont à corriger.
- Nouveaux cas dans `CellsCollisionFieldTests`.
- `TestSavesTests` si un préréglage s'ajoute.
- À mesurer à T0 par exécution : tout test ou arc qui place un héros sans bottes sur des cases d'eau profonde. Candidats : `CarryLockSwimAndStatesTests.cs:122` (le héros démarre dans l'eau), les arcs de la 416 et de la 14.

**Lien avec h2.** La fonction du mur et celle de la cape (D-H2-6) sont la même, avec deux bits. Celle qui passe d'abord la crée, l'autre l'étend. Le texte d'h2 dit « une fonction neuve » : à ajuster.

### i2b : la fonction des forces (table, décalage, poussée)

**Périmètre.**
- Une fonction pure `adj = clamp(((F + tab[idx]) >> (sh & 31)) + push)`, qui remet la poussée à zéro et lève le drapeau au serrage.
- Appelée dans `RunOneKinematicTick`, avant l'ajout du porteur, avec les 16 + 16 mots dumpés depuis l'exécutable et vérifiés mot à mot (comme la table de nage d'E20.i).
- Le héros consomme toujours la poussée. Un porteur de type `FollowCarrier` ne la consomme pas (déjà le cas en pratique).
- `PeekAdjustedForce` applique la même chose sans écrire.
- `MapSlideShiftRaw` posé aux deux sites de C7.
- Tant que `+0x188` vaut 0, c'est inerte hormis `SlideEffectId`.

**Oracles.**
- `docs/plan-e20-annexe/e20-h4b/bin/vec/vec_push_800366FC.json` : 200 vecteurs, dont décalages extrêmes.
- `vec_physpass_80036828.json` : 40 vecteurs (le héros, sans glace ni eau).
- `tables.json`.
- Le décalage seul : les mesures M2 et M3, soit dérive et marche de la carte 159 avec `SlideEffectId` = 1. Les rejouer dans le harnais, de préférence avec Gravity 3 et ZViscosity 256.
- Les 200 vecteurs de poussée couvrent tous les indices et décalages. Le rapport h4b en annonce 3 000 exécutions, le fichier n'en contient que 200.

**Tests qui bougent.** Presque aucun : toutes les entrées à zéro donnent `adj = F`.
- À surveiller : `HeroObjectTopsTests.cs:494-512` (planifiée = utilisée), qui reste vrai si `PeekAdjustedForce` est mis à jour.
- À surveiller : les tests de dépôt sur Gravity 3 de la 159 (`PickupAiDrop*`), qui posent la gravité à la main et n'ont pas besoin de `SlideEffectId`.
- À corriger : le commentaire `ScriptedMotion.cs:630`.
- Nouveau : test de vecteurs via `AnnexFiles`.

**Débloque** h4e2, h4f1 (donc h4f2) et ferme O-E19-31. Le serrage de la carte pour le héros et les entités placées (O-E19-17) reste hors de cette tranche sauf décision contraire (Q-I2-5).

### i2c : les courants et la case verrouillée (O-E20-16, O-H-2)

**Périmètre.**
- Sonde `UpdateTileAttributes` : case de l'origine, test de niveau avec la convention de la DLL (hauteur brute `<<20` contre `PosZ+ModZ`, sans `+1`), portes `0x04` et `0x08`, direction, bit de verrou.
- Accesseur `SampleRawSlope`.
- Appel pour le héros, les entités marquées et (selon Q-I2-3) toutes les entités actives, aussi à l'adoption ou à la naissance : le binaire dérive dès le tick 1.
- La dérive passe par la fonction d'i2b.
- Branche verrouillée dans `MovePlayer`, après le bloc de c2b et avant `UpdateItemUseDriver` : charge, pointeur d'arme, `CancelItemMode`, `UpdatePlayerCarriedEntity(1)`, puis `0x1D` / Idle / `0x2D`, fin de tick. L'ordre diffère de celui de la branche des verrous de contrôle.

**Fichiers.** `EntityScriptProxy.cs`, `CellsCollisionField.cs`, `TerrainProbe.cs`, `PlayerManager.cs` (l. 68-69, 361), `TestSaves.cs`, tests, une ADR.

**Oracle : à créer, mais amorcé.**
- Nouveau groupe de scénarios du harnais (fichier neuf, comme `sscen.py`), preuve de non-régression par manifeste et porte D1.
- Mes sondes `s5` à `s9` donnent déjà les valeurs : portes, quatre directions, verrou, eau, air, sortie de case, Beannoïde, glissement sous poussée.
- Cartes réelles à rejouer : 416 (cascade, rangées 35 à 42), 10 (colonnes 44 et 47 : cases en rampe, Slope `0x67` et `0x76`, où l'égalité de niveau est fragile), 19 (sable, `0x54`, 140 cases), 403-405 (`0x08` seul : pas de dérive au sol), 125 (tapis verrouillé).

**Tests qui bougent.** Aucun existant ne lit `TileAttributes`. Le risque vient des arcs d'entités posées sur des courants (Q-I2-3) et des conflits de fichier avec c2b et h0a.

### i2d (optionnelle) : serrage de la carte pour le héros et les entités placées (O-E19-17)

La fonction d'i2b le contient déjà. L'activer pour tous change les bords de toutes les cartes et plusieurs tests. À ne faire que sur ordre.

### Ordre et dépendances

- i2a est indépendante. À placer avant h2 si on veut qu'h2 réutilise la fonction du mur.
- i2b est indépendante d'i2a. Il suffit qu'elle soit faite avant h4e2 et h4f1.
- i2c dépend d'i2b. Elle touche la même zone de `PlayerManager.cs` que c2b (bloc de blessure) et h0a (pilote réel et annulation). La planifier après c2b, en reprenant le HEAD.
- Pas de prérequis moteur, convertisseur ni donnée.
- Si i2c précède h2, le sable de la carte 19 fera dériver Alundra sous la cape. Les cartes 403 à 405 (bit `0x08` seul) ne dérivent pas au niveau du sable. Le préréglage de la carte 19 d'h2 est sur une case de courant (`t2-cape/critic-main.md`), à déplacer en (44, 40, 2).

## 3. Risques

1. Alundra sans bottes déjà dans l'eau est coincée (M1, M6). C'est fidèle à l'original, mais tout préréglage ou arc sans bottes qui y atterrit est bloqué. Un inventaire par exécution à T0 est indispensable.
2. Le pseudo-bit du masque doit survivre à `ResyncControllerFromFlags` (opcodes de drapeaux visant le héros) et à l'adoption. Le montage de tests pose le masque à la main (`RunMontage.cs:63`) : sans fonction de tick, les rejeux L0/L1 ne verront pas le mur.
3. `BlockedCorners` ignore le masque du contrôleur (C6). Sans correction, le glissement le long du bord d'eau se trompe (W5-L0-wall-diag).
4. La sonde de courant ne tourne aujourd'hui que pour le héros, les entités marquées et le tronc. La porter à toutes les entités actives est un changement de comportement visible sur 13 cartes (Q-I2-3).
5. Sur les cases en rampe (carte 10), l'égalité de niveau est fragile : la DLL arrondit, le binaire compare `+1`. À prouver par oracle sur les vraies cases.
6. `PeekAdjustedForce` doit suivre : sinon un passager d'un porteur qui dérive ou qui est poussé ne bouge pas avec lui. C'est invisible tant qu'aucun porteur ne dérive.
7. Pas de sonde à l'adoption du héros dans la DLL (C5) : un tick de dérive ou de verrou perdu après un portail. À trancher par la mesure M2 (le binaire dérive dès le tick 1).
8. Conflit de merge avec c2b et h0a dans `PlayerManager.cs`, et avec b2 dans `TestSaves.cs` (le tableau de préréglages est déjà modifié par l'exécutant).
9. Le harnais ne modélise pas `+0x188` dans ses colonnes de trace. Les scénarios neufs doivent lire la mémoire via `events`, comme mes sondes. `build_world` fixe `SlideEffectId` à 0 et se règle par `setup`.
10. Écart d'ordre du serrage de la carte (C1) : à ne pas toucher dans i2b, sous peine de déplacer les tests d'e0k3.

## 4. Questions à l'auteur (en termes de jeu, défaut « comme l'original »)

- **Q-I2-1 (découpage).** Faire E20.i2 en trois morceaux ?
  - Après le 1er : sans bottes de nage, Alundra s'arrête au bord de l'eau profonde, y compris la mer de la 416 dès la nouvelle partie.
  - Après le 2e : rien ne change à l'écran, sauf sur les cartes 159 et 160 où Alundra avance deux fois moins vite.
  - Après le 3e : les rivières et tapis roulants déplacent Alundra, et les cases verrouillées lui retirent la main.
  - (A, défaut) mur, poussée, puis courants et case verrouillée après la blessure ; (B) un seul bloc après c2b.
- **Q-I2-2 (Alundra déjà dans l'eau sans bottes).** Dans l'original, elle ne peut plus du tout bouger ; 76 arrivées de portails sur 3 316 tombent sur de l'eau profonde, normales avec les bottes. (A, défaut) coincée, comme l'original ; (B) elle peut nager tant qu'elle y est déjà, un écart documenté utile pour les sauvegardes de test.
- **Q-I2-3 (qui dérive).** Dans l'original, ennemis, objets lâchés et caisses sur un courant dérivent comme Alundra. (A, défaut) tous (13 cartes ont des entités posées sur une case de courant, une sonde par entité et par tick) ; (B) Alundra et les entités nées en jeu seulement, aucun ennemi placé ne dérive, un écart documenté.
- **Q-I2-4 (case verrouillée).** Sur ces cases (cascade de la 416, tapis de la carte 125, escaliers d'Inoa), Alundra ne se commande plus : ni marche, ni saut, ni attaque, ni plongée, ni objet du Rond, et l'objet porté est lâché. (A, défaut) avec les courants ; (B) plus tard (O-H-2). Avec (B), le tronçon verrouillé de la cascade se nagerait à contre-courant.
- **Q-I2-5 (bord de carte, O-E19-17).** Aujourd'hui Alundra et les ennemis peuvent sortir des cartes, ce que l'original interdit. (A, défaut, D-E19-25) hors i2 ; (B) le fermer dans la même fonction, avec un changement visible à tous les bords de carte et beaucoup de tests déplacés.
- **Q-I2-6 (préréglages de recette).** (A, défaut) trois préréglages : `mur-m14` (sans bottes, berge de l'étang), `cascade-m416` (bottes 0x1B, berge à vérifier vers (13, 34, 5)), un pour la 159 ; (B) la recette par la nouvelle partie seulement.

## 5. Notes à reporter au plan

- « 16 cartes » vaut pour les courants sur eau profonde (M5). Le total est 44 cartes dont 28 à sec.
- La « vingtaine de lignes » de Q-H4B-6 (A) ne couvre que i2b.
- O-H-2 (case de sortie) est la branche `0x80032768` : elle va naturellement avec les courants.
- La cape (h2) et le mur doivent partager la fonction de masque (D-H2-6).

## 6. Chemins, reproduction, limites

Dossier de travail : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/i2/`.

| Script | Sortie |
|---|---|
| `s1_scan.py` | `out_scan_188.txt`, `out_scan_4334.txt` |
| `s2_census_belts.py` | `out_census_belts.txt` |
| `s3_callers.py` | `out_callers.txt` |
| `s4_drift_tables.py` | `out_drift_tables.txt` |
| `s5_belt_probe.py` | `out_belt_probe.txt` |
| `s6_belt_probe2.py` | `out_belt_probe2.txt` |
| `s7_belt_wall_probe.py` | `out_belt_wall_probe.txt` |
| `s8_portal_deep.py` | `out_portal_deep.txt` |
| `s9_belt_target_push_probe.py` | `out_belt_target_push_probe.txt` |

- Autres éléments du dossier : `bx.py` (aide de désassemblage copiée), `s0_header.py`, `head/` (le source de la DLL au HEAD, extrait par `git show`), `oc/base/` (copie du harnais e20-d).
- Les sondes se lancent avec `python -B -I`. Capstone est ajouté explicitement au chemin dans chaque script.

Limites :
- Aucun test C# n'a été lancé et aucun vecteur n'a été rejoué en C#.
- Le recensement et les arrivées de portails viennent de `data-extracted/` et de `alundra-project/Maps`.
- La liste des tests qui bougent est faite par lecture, pas par exécution.