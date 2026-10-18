# Découverte E20.h4c2 : la terre 2 (objet 0x2C, sprite 17), pilote, E13, annulation, coup de caméra

Lecture seule. Je n'ai pas pu écrire `report.md` ni `check.md` : l'outil d'écriture refuse les fichiers de rapport. Ce message est donc le rapport complet. Les clés, scripts et désassemblages sont dans le dossier de sortie (liste en fin de message).

Dépôt `D:/development/repo/alundra-casaengine-project-converter` au HEAD `023b33ce`, branche `chantier/restructuration-dll-r3`. Le diff de `Alundra`, `Alundra.Tests` et `alundra-project/Data` contre `c221a76f` et contre `f81304b` est vide : le code lu est celui des annexes `e20-h3/state-f81304b` et `e20-h4b/state-c221a76`. Binaire `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, recalculé. Rien n'a été écrit hors du dossier de sortie. Les scripts de l'annexe ont tourné `python -B` depuis des copies (`h4copy/oracle`, `critic_copy`).

Légende : FAIT (vu dans le code, le binaire ou les données, avec fichier:ligne ou adresse), INFÉRENCE (déduit, de quoi dit), INCONNU. « h4 » = `docs/plan-e20-annexe/e20-h4/` (`check.md` prime sur `report.md`). « h4b » = `docs/plan-e20-annexe/e20-h4b/` (`critic/check.md` prime). « k/ » = `keys/` de mon dossier.

## 0. Verdict

1. **Tous les faits de comportement de h4 et h4b sur 0x2C tiennent.** J'ai redésassemblé le pilote `0x800354D0`, E13 `0x8007F30C`, l'annulation `0x80034BDC`, la garde, le pas de caméra et le pas du générateur. J'ai relu les tables dans l'EXE et rejoué h4 et h4b sans écart (§1, §8).
2. **Découverte neuve (artefact du banc d'oracle de h4, FAIT).**
   - L'`O.setup` de h4 laisse `+0` (l'indice d'emplacement) à 0 dans les emplacements jamais utilisés. Le chargement de carte l'écrit pour les 64 cases (`0x8003B24C-0x8003B2CC`).
   - Un nouveau-né dans une case jamais utilisée n'est pas visité par la passe d'événements de son tick de naissance : **+1 tick** pour les 6 premiers rochers.
   - D'où « durées de vie 49 à 82 » (le bon chiffre est 81) et des fins d'objet décalées d'un tick pour 4 autres tomes (§1.8).
   - **Toutes les clés de h4 (`trace_cast`, `trace_targets`, `interrupt_check`) sont à refaire.** Il faut aussi le pas par image du générateur (`_pf`, §1.20 point 3), car les positions des rochers et les comptes de touches dépendent des tirages.
   - Elles sont refaites dans `k/` et déterministes (deux exécutions, mêmes md5).
3. **Dépendances.** h4c2 dépend de h0a, de h4a et de **f5 (D-F5-5, clé de vitesse sur `TargetAnimationId`)**, que le tableau du §1.31 ne cite pas : sans elle un rocher fait un pas de trop (1,7 px) au tick d'E13 (§1.3). Il ne dépend **pas** de f6a (le sprite 17 n'a pas la gravité, §1.9), ni de h4b1, h4b1b, h4b2.
4. **Le coup de caméra est assigné (`sw 2`), pas additionné.** Deux E13 au même tick donnent 2, pas 4 (15 rochers, 13 ticks de coup distincts). Une annulation en l'air ne donne aucun coup. Il faut le ranger **par tick** (§1.6).
5. **Atterrissage DLL (§3).**
   - Un fichier d'IA neuf : pilote, E13, annulation, table.
   - Une clé de registre `(E, 13)` pour le seul type 17.
   - Un membre par défaut de `IEntityWorldContext`, une liste par tick dans `WorldProxy`, un paramètre optionnel de `CameraMath.AdvanceCameraSmoothing`.
   - Deux épingles du registre bougent (+1). S5, B6_R et les tests de caméra ne bougent pas.
6. **Plan proposé (§4).** T0 (annexe, rejeux, sondes), T1 (tests purs puis code, phase A inerte), T2 (tests de monde rouges, puis phase B : câblage), T3 (ADR, plan, verifiers, 30 mutants). Quatre questions pour l'auteur (§5).

## 1. Faits du binaire

### 1.1 Le pilote `0x800354D0` (FAIT ; `dis/driver_800354D0.txt` ; modèle de référence `h4/work/oracle/h4_models.py:389-427`)

- **Appel.** Par l'enveloppe `0x8003634C` (table `0x800235F4`, objet `0x2C`).
  - Elle rend 1 sans objet en cours.
  - Elle incrémente `[0x8012715C]` (saturé à `0x7FFFFFFF`) **après** le pilote, puis met `[0x80127164]` à 0 si le pilote a rendu non nul (`h4/report.md` §1 ; `h4_models.py:684-709`).
  - Premier appel au tick qui suit le départ, compteur 0 (`h4/report.md` §0.9). Confirmé : `k/cast_2c_pf.json`, `per_tick`, objet 44, phase 0, compteur 0, 1, 2 aux ticks 1, 2, 3.
- **Phases** `[0x80127160]` : 0 (table), 1 (attente), 2 (fin). Toute autre valeur rend 0 (`0x8003551C`, `0x8003552C`).
- **Garde** `0x800351B8` en tête des phases 0 et 1, jamais en phase 2 (`h4/report.md` §1 ; `dis/guard_800351B8.txt` ; 19/19 instructions couvertes) :
  - pente du héros 4 → annulation et 1 ;
  - sinon animation `0x32` ou `0x33` → 0 ;
  - sinon annulation et 1.
- **Table** `0x80023504` (lue dans l'EXE par `tools/read_tables_2c.py`) : 15 entrées de 4 octets (tick u16, p s16) puis la sentinelle `0xFFFF`.
  - Ticks : 16, 32, 48, 64, 80, 96, 112, 128, 144, 160, 176, 192, **224, 224**, 240.
  - p = 512, 1024, … 7680 (pas 512).
- **Parcours** (`0x80035590-0x800356D4`).
  - À chaque appel la table est reparcourue depuis son début tant que `compteur >= tick`. Une entrée n'agit que si `tick == compteur`. Le compteur monte d'un par appel, donc aucune entrée n'est perdue.
  - Deux entrées au compteur 224 : deux rochers au même tick.
  - Quand l'entrée suivante est la sentinelle (compteur 240), la phase passe à 1 **dans le même appel**.
- **Tirages** (FAIT, `0x80035568-0x80035624` et mes 609 cas sur le vrai code).
  - Par rocher, **deux** tirages, **r1 puis r2**, chacun `hi(état' × 32)` = `état' >> 27`, avec `état' = état × 0x7D2B89DD + 0xE06A02E7` ([`0x80098708`]).
  - Les deux sont tirés **avant** l'appel de naissance, qu'elle réussisse ou non.
  - `Ai/EnemyHelpers.cs:35` (`Below(n)`) est la même formule.
- **Naissance** : `SpawnWarpEntity` (`0x80039F58`), parent = héros, page globale (indicateur 0), indice `0x11`.
  - `x = X_héros + X32[r1] × (p + 0xA00)`
  - `y = Y_héros + Y32[r1] × (p + 0xA00)`
  - `z = Z_brut_héros + 0x1000000`
  - direction = `r2`
  - `X32` est à `0x80023654`, `Y32` à `0x80023694` (s16). Ce sont `Alundra/Entities/AnimationTables.cs:71` et `:82`, identiques au binaire (h4b critique §5).
  - Rayon : X de 36 px (p = 512) à 120 px (p = 7680), Y de 24 à 80 px.
  - Exemple (graine `0x12345678`, héros (100, 100), Z brut 1, compteur 16) : r1 = 23, r2 = 4, x = `0x874C00`, y = `0x5F5000`, z = `0x1000001`, direction 4.
- **Après la naissance.**
  - Retour nul : rien d'autre (ni destruction ni ForceZ). Les tirages ont eu lieu.
  - Sinon, comparaison signée stricte `Z_brut_héros + 0x1000000 < [rocher + 0x138]` :
    - vrai → `DestroyEntity` à un argument (`0x8003A774`, statut 4) ;
    - faux (égalité comprise) → `ForceZ (+0xB8) = 0xFFF80000` (−8 px/tick).
  - `+0x138` est écrit par la création elle-même (`0x80039EF8`, après `0x800370C4`), donc lisible au retour. Cas `lt`, `eq`, `lo` de `k/driver_2c_cases.json`.
- **Phase 1.**
  - Garde, puis compteur `< 0x118` → 0.
  - Sinon `hero+0x88 = 0x34`, phase 2, rend 0. La borne 0x118 est vérifiée sur le vrai code par la critique h4.
- **Phase 2** : rend 0 tant que `hero+0x88 == 0x34`, 1 sinon.
  - Cast mesuré : `0x34` au tick 282, retour à 0 au tick 308, objet libre au tick 309.
- **Rejeux.**
  - `h4_vec_drivers.py 1500` : 0 écart. Couverture 0x2C 174/176 (les deux manquantes sont la phase négative), annulation 30/30, garde 19/19, enveloppe 73/93 (manquantes : graines, cape, bombe).
  - Mes 609 cas contrôlés : 566 rendent 0, 43 rendent 1, aucun autre.

### 1.2 Chronologie d'un rocher (FAIT ; `k/cast_2c_pf.json`, 15 rochers, banc corrigé §1.8)

Rocher **intérieur** né au tick b (14 sur 15) :

| Événement | Tick |
|---|---|
| Naissance, statut 2 | b |
| Animation cible 2 (chaîne de l'animation 0, 32 ticks) | b+31 |
| Indicateur de sol `+0x144` (atterrissage strict : atteindre le terrain exactement n'est pas atterrir) | b+32 |
| Statut 3 et premier passage d'E13 (`+0x88 = 1`, son 45, coup de caméra) | b+33 |
| `+0xAC = 1` (46 ticks après E13) | b+79 |
| Statut 4 | b+80 |
| Case recyclable | b+81 |

- **Z.** À b, Z brut = `0x1000002 − 0x80000`, soit 248 px + 2 unités brutes au premier instantané, puis −8 px/tick.
- **Convention DLL.** Le DLL est en convention « binaire moins une unité brute » (ADR-0026, `Alundra/Maps/IEntityWorldContext.cs:81`). Il faut donc lire chaque Z de la clé moins 1.
- **Drapeaux** : `0x132014` à la naissance, `0x132044` après E13.
- **Sons** : 55 au tick b, 45 au tick b+33. 30 sons pour 15 rochers, aucun effet créé.
- **Déplacement horizontal** :
  - `OffsetXList[r2] × 160` (vitesse 160 des animations 0 et 2, en 16.16 : 1,73 px/tick pour r2 = 26), **y compris au tick de naissance** ;
  - arrêt (vitesse 0 de l'animation 1) **au tick d'E13 lui-même**.

**Rocher de bord de carte.** Avec le héros en (100, 100) et les tirages `_pf`, c'est 1 rocher sur 15 : l'id 11, r1 = 8, x demandé −20 px.
- `ForceAdjusted` (`+0x13C`) dès le tick de naissance, x = 10. La borne gauche vaut 10 px (`NegModX = −(offsetX << 16)`, `Alundra/Entities/Spawning/EntitySpawnFactory.cs:853`) ; la haute 1237 px (`:858`).
- Statut 3 et E13 à **b+1**, alors qu'il est encore à 240 px de haut (coup, son 45).
- Il continue de tomber en animation 1 (drapeaux `0x132044`), sol à b+32, `+0xAC` à b+47, statut 4 à b+48 (vie de 49).
- Sans le pas par image (clé de h4), 3 rochers sur 15 sont dans ce cas.

### 1.3 E13 `0x8007F30C` (FAIT ; `dis/E13_8007F30C.txt` ; h4b critique §5 ; `h4_vec_handlers.py 1500` : 0 écart, 27/27 ; `vec_e13_8007F30C.json` : 40/40)

- **Si `+0x88 == 1`** :
  - si `+0xAC == 1` → `DestroyEntity(self, −1)` (`0x8003A59C`). L'effet de bris est lu dans l'en-tête : `BreakEffect` = 0 pour le sprite 17, donc aucun effet ;
  - sinon rien.
  - E13 ne teste que `TargetAnim == 1`, pas le statut.
- **Sinon (première passe)** : `[0x800DD874] = 2` (`sw`, assignation), `+0x88 = 1`, `+0x6C = (+0x6C | 0x40) & ~0x30`.
  - `0x40` = `DeactivateOnAnimationEnd`.
  - `~0x30` éteint `DeactivateOnImpact` et `DeactivateOnHit`.
  - Différence avec E3 `0x8007EDA0` : le coup de caméra, et le test « `+0x88 == 1` puis `+0xAC` » au lieu des deux ensemble.
- **Rocher annulé** : s'il arrive à E13 avec `+0x88` déjà à 1, ni coup ni changement de drapeaux (`k/cancel_2c_pf_t100.json` : drapeaux `0x132014` conservés, aucun coup après l'annulation).
- **Conséquence DLL** (INFÉRENCE appuyée par l'oracle : x identique aux ticks b+32 et b+33, rocher d'id 2 de `cast_2c_pf.json`).
  - La clé de vitesse de `ScriptedMotion.TickScriptedNpc` (`Alundra/Physics/ScriptedMotion.cs:171-176`) est aujourd'hui `CurrentAnimationId`, sauf au tick de naissance.
  - Au tick d'E13 elle donnerait la vitesse 160 de l'animation 2 au lieu de la vitesse 0 de l'animation 1 : un pas de trop.
  - **D-F5-5** (clé = `TargetAnimationId` quand `HasLogicalZ`, `docs/plan-e20-heros.md` ligne 5854) le règle. f5 devient donc un prérequis de h4c2 (ordre ferme du §1.20 : f4, f5, f6a, f6b, puis h).
- **Pas de resynchronisation du contrôleur.** `ResyncControllerFromFlags` (`Alundra/Entities/EntityScriptProxy.cs:670`) ne lit que `ClassA`, `ClassB` et `Gravity` (`Alundra/Physics/CellsCollisionField.cs:147-162`). E13 ne les touche pas, donc l'appel « en assurance » est un mutant équivalent (comme D-F5-4).

### 1.4 L'annulation `0x80034BDC` (FAIT ; `dis/cancel_80034BDC.txt` ; couverture 30/30)

- **Chemin** : via `0x800350C0` (table `0x800233EC`, objet `0x2C`), puis `[0x80127164] = 0`.
- **Effet.**
  - Elle parcourt les emplacements **1 à `[0x80134360]` inclus** (h4b critique §3.2).
  - Tout rocher (`+0x68 == 0x11`) au statut 2 passe à statut 3, `+0x20 = 0`, `+0x88 = 1`.
  - Rien d'autre : les statuts 1, 3 et 4, les autres types, les drapeaux et les sons restent.
- **Suite.** Les rochers annulés continuent de tomber, finissent leur animation 1 et meurent par E13 (`+0xAC`).
- **Sites d'appel** : la garde `0x800351B8`, la branche verrouillée `0x80031BFC`, le défaut des états sans gestionnaire `0x80032758`, la case de sortie, la mort (h0a).
- **Mesure** `k/cancel_2c_pf_t100.json`. L'animation du héros est posée à `0x31` avant les passes du tick 100.
  - Les rochers 6 et 7 (en l'air à 112 et 240 px) passent à statut 3 et `+0x88 = 1`, drapeaux `0x132014`.
  - Ils chutent encore de 8 px/tick et meurent au tick 147.
  - Aucun coup après l'annulation.

### 1.5 Le sprite 17 (FAIT ; `alundra-project/Data/sprite-records.json`, BankKey `alundra_17` ; `Data/balance.json` entrée 17)

- **Programmes** : A0 C0 D0 **E13** F0.
- **Drapeaux.** `MoreFlags` 20, `CanPickup` 32, `FlagsPortraitShadowType` 19, d'où `Flags = 0x132014` (`EntitySpawnFactory.cs:740`) :
  - `HitsClassA` (masque de touche ClassA seul : `HitPass.ClassMaskOf`, `Alundra/Combat/HitPass.cs:43`) ;
  - `DeactivateOnImpact`, `NoObstacleSlide`, `DestroyOnSlidingSlope` ;
  - **pas de `Gravity`**.
- **Corps et contenu.**
  - Boîte du corps 21 × 15 × 16, décalage (−10, −7, 0).
  - `BreakEffect` 0, `Contents` 0 : le butin à la création ne tire pas (`Items/LootRoll.cs`, code 0).
- **Animations** (`AnimSets` : vitesse, accélération, son) :
  - 0 : 160, 64, **55** ;
  - 1 : 0, 64, **45** ;
  - 2 : 160, 64, 0.
  - `Acceleration & 0x20` est nul, donc pas de +0x100 (`EntityScriptProxy.cs:329-337`).
- **Fins d'animation** (`IdsvAnimDirs`) :
  - animation 0 : 16 images, fin `Chain` vers 2 (32 ticks) ;
  - animation 1 : 23 images, fin `Hold` (46 ticks) ;
  - animation 2 : 72 images (sprite 0), `Loop` (144 ticks).
  - `Hold` lève `ForceResetAnimationFlag` (= `+0xAC`) par l'horloge logique (`EntitySpawnFactory.cs:283-325`).
- **Attaque** (`balance.json`).
  - Un seul enregistrement `{AttackType 8, AttackAttribute 8, Power 64}` pour toutes les animations (`BalanceRecord.AttackForAnimation`, `Alundra/Combat/BalanceRecord.cs:61-70`).
  - Réponses 128 partout, PV 0.
  - Boîte d'attaque (`collision_keyframes`, identique aux animations 0, 1 et 2) : 72 × 48 × 39, position locale (−1, 0, 3,5), soit z de −16 à 23.
  - Les dégâts de h4 valent `max(1, (64·r) >> 4)`, soit 64 pour r = 16. Dans la DLL le dégât est le programme D de la cible, que seuls 335 et 347 ont (`e20-h3/state-f81304b/dll/state.md` R18). Les clés de touches comptent donc des **touches**, pas des PV.
- **Le héros** (ClassB) n'est pas touché : aucun réglage D-H-7.

### 1.6 Le coup de caméra

Sources : h4b `critic/check.md` §5, `bin/report.md` §6, `dis/camera_8002CDA0.txt` relu. Rejeux : `c5_camera.py` (3000 essais, 0 écart, 184/185) et `c9_replay.py` (770/770 : `vec_camera` 100, `vec_sway` 80, `vec_e13` 40).

- **Écrivain et lecteur uniques.**
  - Écrivain : E13, `0x8007F350` (`sw 2` dans `[0x800DD874]`).
  - Lecteur : le pas de caméra `0x8002CDA0`, en `0x8002CE6C`.
  - Formule : `scrollY += ((cibleY − (scrollY + 0x88)) >> 4) + OffsetY(sway, +0x18) + coup`.
  - Remise à zéro à chaque pas (`0x8002CE98`) et au chargement de carte (`0x8002CD7C`).
  - Au premier appel après un chargement, le coup est **effacé sans être appliqué** (`0x8002CDF4-0x8002CE0C`).
- **Vie d'un coup au repos.** Scroll Y +2, puis −1, −1, puis 0 (`−2 >> 4 = −1`) : `[302, 301, 300, 300, …]`. L'unité est le pixel de défilement.
- **Signe.** Le coup s'ajoute comme `OffsetY`. Dans le DLL, `CameraMath.ComputeSmoothedCameraTarget` ajoute `(swayX, −swayY, 0)` (`Alundra/Cameras/CameraMath.cs:223`). Le coup se passe donc **en plus de `swayOffsetY`** (INFÉRENCE : il suffit d'additionner avant l'appel).
- **Assigné, pas additionné** (FAIT, nouveau).
  - C'est un `sw` d'une constante. Mon banc donne 15 rochers pour 13 ticks de coup distincts.
  - Deux rochers partagent le tick 163 (un rocher intérieur et le rocher de bord), deux autres le tick 259 (les deux du compteur 224).
  - La valeur par tick vaut 2, pas 4.
- **Calendrier.**
  - Binaire (`0x8002C3F4-0x8002C45C`) : caméra, puis tick. Le coup du tick N est consommé par la caméra qui précède le tick N+1.
  - DLL : la boucle des ticks (`Alundra/Maps/WorldProxy.cs:2524`) précède le suivi de caméra, qui fait `ticksThisFrame` pas (`:2716` ; `CameraDirector.cs:214` ; `CameraMath.cs:167`).
  - Le coup du tick N doit donc être consommé par le pas N de la même image. Il faut le **ranger par tick**.
  - Précédent : `_effectFrozenOfTick` (`WorldProxy.cs:767`, vidé à `:2519`, lu par `EffectFrozenOfTickForTests` `:776`).
- **Cas où le coup est perdu.** Pas de coup sur un pas de calage (`needsSnap`) ni pendant une transition (`swayActive`, `CameraDirector.cs:222`). Le binaire efface le coup au chargement de carte ; la liste par image du DLL est vidée à chaque `Update` (équivalent).
- **Coup X** : sans écrivain (`[0x800DD870]`, dormant).

### 1.7 Le générateur (FAIT)

- **Pas de fin de tick.** `0x8002BD0C-0x8002BD34` (`dis/frame_step_8002BCF0.txt`) fait **un pas par tick** de l'état [`0x80098708`], après les passes (appel en `0x8002BC4C`, h4b critique, puis `jal 0x8004B1D4`). C'est le « pas par image » de `docs/plan-e20-heros.md` §1.20 point 3.
- **DLL.** Il a ce pas par tick (`GameRandom.AdvanceFrame`, `Alundra/Rng/GameRandom.cs`, appelé par `EffectStage.RunFrame`, `WorldProxy.cs:2692`, ADR-0066).
- **Oracle de h4.** `W.tick` ne l'ajoute pas : sa clé a `rng0 = rng1` sur un tick sans tirage.
- **Effet sur 0x2C.**
  - Rocher 1 en (135, 95) sans `_pf`, en (76, 83) avec.
  - Comptes de touches des six cibles : (7, 9, 0, 6, 9, 6), puis (7, 8, 0, 6, 8, 6) avec l'initialisation des emplacements seule, puis (10, 6, 7, 6, 3, 6) avec `_pf` (`k/targets_2c_pf_inv0.txt`).

### 1.8 L'artefact d'initialisation des emplacements (FAIT, nouveau ; `dis/maploadinit_8003B24C.txt`, `dis/events_pass_800386D0.txt`, `dis/spawn_init_80039D04.txt`, `tools/cast_slotinit.py`)

- **Binaire.**
  - Le chargement de carte (`0x8003B24C-0x8003B2CC`) recopie le gabarit `0x80134368` dans chacune des 64 cases **et écrit l'indice dans `+0`**, puis met `[0x80134360]` à 0 (`0x8003B2D8`).
  - La création (`0x80039D04`) **relève** `[0x80134360]` à `[ent+0]` si celui-ci est plus grand (`0x80039D24-0x80039D3C`, h4b critique §3.1).
  - La passe d'événements `0x800386D0` appelle `MovePlayer` (`jal 0x80031B50`) **puis** lit le compte (`0x80038700`), et le relit à chaque tour (`0x800388A0`).
- **Banc de h4.**
  - `O.setup` met les 64 cases à zéro. Une case jamais utilisée garde `+0 = 0`, la création ne relève pas le compte, et la passe d'événements ne visite pas le nouveau-né dans son tick (statut 1, pas de physique).
  - Seules les cases **recyclées** (statut 4) reçoivent `+0 = i` par la passe `0x80038634` (`vec_slotpass.json`).
- **Preuve.**
  - `trace_cast.py 0x2C` rejoué à l'identique (sorties de `trace_summaries.txt`).
  - Avec `+0 = i` posé pour les 64 cases (`tools/cast_slotinit.py`) : les 15 rochers naissent tous au statut 2 à Z 248 px, durée de vie 81 (49 et 51 pour les rochers de bord de la clé sans `_pf`).
  - Les rochers de la clé de h4 nés à partir du tick 114 (cases recyclées) étaient déjà exacts.
- **Portée sur les autres tomes** (`cast_pf.py ITEM` avec et sans `--slotinit`) :

  | Tome | Sans → avec `+0 = i` |
  |---|---|
  | 0x2D, fin d'objet | 344 → 343 |
  | 0x2F, fin d'objet | 150 → 149 |
  | 0x2F, boules | 12 → 11 |
  | 0x30, fin d'objet | 183 → 182 |
  | 0x32, fin d'objet | 243 → 242 |
  | 0x2B, durées | médiane 31 → 30 (fin d'objet 129 inchangée) |
  | 0x31, durées | 38-39 → 38 (fin d'objet 129 inchangée) |

  0x2E n'a pas été rejoué.
- **Conséquence.** Le DLL fait participer tout nouveau-né de l'interface à son tick (ADR-0065, `WorldProxy.cs:3572`). **La clé correcte est celle avec `+0 = i`**, comme les lignes BT-M1 à BT-M3 (`docs/plan-e20-annexe/e20-e0a/README.md`).

### 1.9 Pente et eau : le sprite 17 n'y est pas sensible (FAIT, rejeu)

- **Règle.** Le type 17 porte `DestroyOnSlidingSlope` (`0x100000`) mais pas `Gravity`. Pour une entité sans gravité, le binaire n'écrit pas `+0x18C` (`docs/plan-e20-heros.md` §1.22 : « Sans gravité, +0x18C = 0 »). Le choix du programme de l'état Normal (`EntityScriptProxy.cs:1708`) ne le détruit donc pas.
- **Rejeu** (`tools/cast_rich_water.py`).
  - Une bande d'eau profonde (`ground_property` 8) à x ≥ 168 px laisse la chronologie des 15 rochers **identique**. Les rochers 9 et 13 y tombent.
  - Contrôle avec `0x2B` (type 16, gravité) sur la même bande : durées de vie de 17-18 pour une partie des rochers, et 14 effets. L'eau tue.
- **Conclusion.** **h4c2 ne dépend pas de f6a.** Seule la garde du héros (pente 4 → annulation, h4a) agit.

### 1.10 L'autre utilisateur d'E13 (FAIT, données)

- **Un seul** : l'enregistrement 1 de la carte 321, page de carte, `SpriteTableIndex 213` (type 469 = `0x100 + 213`), `SpriteDirection 128`, `XPos 0`, `YPos 82`.
- **En-tête 213** : mêmes programmes et même boîte que le sprite 17, mais `MoreFlags 18`, c'est-à-dire `HitsClassB` : il blesse le héros, pas les ennemis.
- **Naissance.** Il naît par un `0x2D` d'un programme de la 321. Le test B6_R (`Alundra.Tests/AlundraRecordBirthTickTests.cs:364`) le fait naître à la main.
- **Effet de l'inscription.** Avec `(E, 13)` inscrit pour le seul type 17, il reste un no-op compté comme aujourd'hui. L'inscrire pour 469 le ferait jouer son impact.
- **INCONNU** : le programme de la 321 qui le fait naître, et si la 321 est sur la chaîne.

## 2. Les clés d'oracle

**Scénario commun de h4** (FAIT, `h4/work/oracle/world.py:95-125`, `spellworld.py`) :
- champ synthétique plat de 52 × 60 cases (1248 × 960 px, toutes les cases à 0) ;
- héros en (100, 100) px regardant à droite, graine `0x12345678`, PM 4/4, Rond pressé au tick 1 ;
- sprites globaux 1, 16, 17, 19-24 installés, **aucune autre entité** (hors cibles).

C'est le banc de `Alundra.Tests/AlundraMapEdgeWorldTests.cs:43-53` : champ plat de 52 × 60 posé dans `ArcRun`, `Born`, `Tick`. Le héros se place par `ArcRun.PlaceHero` (`AlundraArcSupport.cs:470`). Le tome se donne par `Give`, `SetItem`, `SetMp`, `Press` (`:151-171`, `:454`).

| Clé | Contenu | Banc DLL | Verdict |
|---|---|---|---|
| `h4/work/results/trace_summaries.txt`, entrée `item 0x2c` (lignes 10-15) | chronologie du héros, 15 naissances (ticks 18 à 242), durées 49 à 82, sons {55 : 15, 45 : 15} | oui (aucune autre entité) | **à refaire** : artefact de §1.8 (82 → 81). La partie héros (0x32 au tick 1, 0x33 au 33, phase 1 au 242, 0x34 au 282, 0 au 308, objet libre au 309) est exacte et sert h4a (o3) |
| `trace_cast.py 0x2C --out` | suivi par rocher et par tick : statut, id, type, x, y, z, `+0x88`, `+0x90`, `+0xB8`, PV, `+0x220`, drapeaux | oui | **à refaire** (slot + `_pf`) : `k/cast_2c_pf.json` |
| `targets_inv0.txt`, `targets_inv30.txt` (bloc `0x2c`) | six cibles statiques (sprite global 25 posé ClassA, 5000 PV) : touches, dégâts, premier tick | oui, avec le préfab 25 « Bloc transparent (1×1×2) » ; ClassA et PV posés par le test | **à refaire** : `k/targets_2c_pf_inv0.json`. `inv30` demande que le test pose `+0x1CC = 30` comme l'oracle : optionnel |
| `interrupt_check.txt` (0x2c, tick 100) | animation du héros posée à `0x31` avant le tick 100 : statuts à 100, 101, 102 | oui, par `TargetAnimationId = 0x31` posé avant l'image ; la garde annule | **à refaire** : `k/cancel_2c_pf_t100.json` (rochers 6 et 7 en l'air) et `_t60.json` |
| `damage.txt` | règle de dégâts (20 480 cas) | non (dégâts = programme D de la cible) | hors portée |
| `h4_vec_drivers.py` | aléatoire sur l'image mémoire, spawn stubbé | non (adaptateur) | remplacé par `k/driver_2c_cases.json` |
| h4b `vec_e13` (40), `vec_camera` (100), `vec_sway` (80), `vec_extra_camera` (3) | vecteurs d'image mémoire | adaptateur à écrire (règle de h4b1 : tout octet lu doit avoir un membre) ; le `ret` d'E13 n'a pas de sens | utilisables ; limite des bornes de la caméra à mesurer en T0 |

**Ce que contient `k/`** (`k/README.txt`, `k/MANIFEST.md5`) :
- `cast_2c_pf.json` : par rocher, naissance, r1, r2, position et trace par tick ; les coups ; les sons ; par tick `rng0`, `rng1`, objet, phase, compteur, PM. Les Z sont en mots bruts.
- `targets_2c_pf_inv0.json` et `_inv30.json`.
- `cancel_2c_pf_t100.json` et `_t60.json`.
- `driver_2c_cases.json` : 609 cas.
  - Entrées : phase, compteur, animation, pente, héros X/Y/Z brut, graine, résultats de naissance, terrain donné au nouveau-né, rochers du monde.
  - Sorties : retour, journal de naissance, générateur après, phase/compteur/objet/animation après, statut et `+0xB8` rendus.

La place du pas dans `_pf` est la règle du §1.20 point 3 : après l'enregistrement de `rng1` du tick, avant la lecture de `rng0` du tick suivant.

**Règles pour les tests de monde.**
- Un tick par image pour tout ce qui dépend de r1/r2 (positions, bords, touches).
- À 2 et 3 ticks par image, seulement la chronologie relative d'un rocher intérieur (b+31, b+32, b+33, b+79, b+80). Les tirages des ticks suivants ne voient pas les pas intermédiaires (écart documenté d'ADR-0066).
- Les entités de la carte autres que le héros et les cibles sont retirées avant le scénario (bloc du §1.20 point 10 : `Status = FlagToDestroy` puis `WorldProxy.RecycleDestroyedEntities()`, aide `Recycle` de `AlundraLootRollWorldTests.cs:74-78`).
- La graine `GameRandom.RandomSeed = 0x12345678` est posée juste avant la première image, après la construction d'`ArcRun`.

## 3. Atterrissage dans la DLL (HEAD `023b33ce`)

### 3.1 Ce qui existe (FAIT, relu)

| Brique | Où |
|---|---|
| Pilote d'objet en cours à chaque tick libre (bouchon) | `Alundra/Hero/PlayerManager.cs:353` (appel), `:1507-1509` (corps vide). Le retour anticipé de `BlockedByEntity` (`:321-328`) et la branche verrouillée (`:335-345`) le précèdent comme dans le binaire |
| `TryUseItem` (bouchon, rend 1) et couture `TryUseItemResultForTests` | `:932`, `:936-937`. `CancelItemMode` vide `:740-742`, appelé par le groupe `Skip` de la nage `:664`. Commentaire « NOT PORTED » `:620-621` (h0a et h4a les remplacent) |
| Naissance par indice de sprite | `Alundra/Maps/WorldProxy.cs:3488-3598` (interface `IEntityWorldContext.cs:81`). Z de l'appelant + 1 en `:3549`. Marqueurs `JoinsBirthTick` et `HasLogicalZ` en `:3572-3573` (`EntityScriptProxy.cs:1375`, `:1383`). Appel type depuis `MovePlayer` : `PlayerManager.cs:1216-1218` (`host is IEntityWorldContext world`) |
| Destruction | À deux arguments `WorldProxy.cs:3301` (`IScriptHost.DestroyEntity(entity, effectId)`, `Maps/IScriptHost.cs:48`). À un argument `:3329` (`IEntityWorldContext.cs:90`). Recyclage `:3265` |
| Liste des entités nées | `IEntityWorldContext.SpawnedEntities` (`:35`), dans l'ordre des emplacements. Filtres : `EntityScriptProxy.SpriteType` (`:141`), `Status` (`:88`), `BlockedByEntity` (`:99`, `+0x20`) |
| Choix d'un gestionnaire E | `Alundra/Events/EventProgramRunner.cs:264-324` (`RunSpriteEvent`). E0/E1/E10 en ligne `:283-292`, `:328`. Registre `:294-315`. Le statut 3 choisit le programme E (`EntityScriptProxy.cs:1758-1760`). `DeactivateOnImpact && (ForceAdjusted != 0 \|\| IsOnGround != 0)` → statut 3 (`:1718-1722`) |
| Registre natif | `Alundra/Ai/NativeSpriteRegistry.cs:36-43` (`CreateForProduction`, 7 clés), `:54-67` (`Register`, doublon `:62`). Modèle `Alundra/Ai/Projectiles/WaveSwordAi.cs:30` (`Register`), `:25-27` (journal de test par thread) |
| Vitesse, Z logique, bord de carte | `Alundra/Physics/ScriptedMotion.cs:171-176` (clé de vitesse). `:465` (`RunLogicalZPreXyStep` : force de Z conservée sans gravité, atterrissage strict). `:540` (`IsOnGround`). `:687` et `:741` (`ClampStepToMapEdge`, `HasLogicalZ`). `ApplyVerticalForceLaw` laisse la force intacte sans gravité (`EntityScriptProxy.cs:1053-1078`) |
| Terrain d'une entité | `ComputeTerrainHeight()` (`EntityScriptProxy.cs:1888`), champ `TerrainHeight` (`:366`). La création place l'entité au moins au terrain (`EntitySpawnFactory.cs:812`) mais **n'écrit pas** `TerrainHeight` (FAIT par grep : seules écritures `EntityScriptProxy.cs:842` par tick, `:2154` héros, `WorldProxy.cs:2233` adoption du héros ; aucun appel `immediateAtSpawn: true` en production). Le test du pilote lira donc `ComputeTerrainHeight()` du nouveau-né |
| Tables 32 directions | `Alundra/Entities/AnimationTables.cs:71`, `:82` |
| Générateur | `Alundra/Rng/GameRandom.cs` (`Next`, `AdvanceFrame`). Tirage borné : `Ai/EnemyHelpers.cs:35` |
| Sons | À la naissance `WorldProxy.RequestAppearanceSound` (`:3342`, appelé `:3562`). Au changement d'animation `EntityScriptProxy.AnimationSoundOf` (`:329-337`) |
| Touches | `Alundra/Combat/HitPass.cs` (masque `:43`, recouvrement élargi d'une unité, `+0x1D0 = 0x19`). Attaque courante rechargée à chaque changement d'animation (`EntityScriptProxy.HitPass.cs:106-113`). Boîtes de `collision_keyframes` (`Combat/FrameBoxTimeline.cs`) |
| Caméra | `Alundra/Cameras/CameraSway.cs:33-41` (9 champs), `:65` (`Step`). `CameraMath.cs:167` (`AdvanceCameraSmoothing`), `:216` (`ComputeSmoothedCameraTarget`, `:223` : `−swayOffsetY`), `:301-302` (`ToOriginalScrollSpace`). `CameraDirector.cs:214` (`UpdateCameraFollow`, seul appelant `WorldProxy.cs:2716`) |
| Banc de test | `AlundraMapEdgeWorldTests.cs:43-53` ; `AlundraJumpTestSupport.cs:457` (`FlatCells.Create`) ; `AlundraArcSupport.cs:151-171` (`Give`, `Equip`, `SetMp`, `SetItem`), `:430` (`OneFrame`), `:454` (`Press`), `:470` (`PlaceHero`) ; tables relues dans l'EXE à l'exécution : `AlundraHeroRunTablesTests.cs:18`, `:42`, `:57` |

### 3.2 Ce qu'il faut écrire (propositions de forme, noms libres sous `check_names.py`)

- **Un fichier neuf** `Alundra/Ai/Items/RockFallAi.cs`.
  - Namespace `Alundra.Ai.Items`, celui de `BombAi` (D-H-6).
  - `grep -w` de `RockFall` et `RockFallAi` ne trouve rien dans `Alundra`, `Alundra.Tests`, `CasaEngineMonogame`.
  - Contenu :
    - la table (15 entrées + sentinelle) ;
    - le pilote : phases, garde de h4a, deux tirages `EnemyHelpers.Below(32)`, `SpawnEntityBySpriteIndex(hero, false, 17, x, y, hero.PosZ + 0x1000000, r2)` ;
    - la comparaison `hero.PosZ + 1 + 0x1000000 < nouveau.ComputeTerrainHeight()`, puis `ForceZ = unchecked((int)0xFFF80000)` ou `DestroyEntity(nouveau)` à un argument ;
    - E13 ;
    - l'annulation (toutes les `SpawnedEntities` de `SpriteType` 17 au statut 2) ;
    - `SpriteTypes = {17}` ;
    - `Register(registry)` pour la clé `(ProgramEDeactivate, 13)` ;
    - un journal de test par thread.
- **Registre** : `NativeSpriteRegistry.CreateForProduction` (`:36-43`) reçoit un appel `RockFallAi.Register` (phase B), avec le `using` du nouveau namespace.
- **Entrées de l'objet `0x2C`** dans les tables de h0a (départ commun de h4a, pilote, annulation).
  - Phase B. En phase A elles restent des bouchons (D-H-3).
  - Les tests passent par `ItemStartForTests`, `ItemDriverForTests`, `ItemCancelForTests` (D-H-4).
- **Caméra.**
  - Un membre par défaut de `IEntityWorldContext` (après `:193`, par exemple `void SetCameraKick(int scrollY) { }`), implémenté par `WorldProxy`.
  - Une liste par tick `_cameraKickOfTick` à côté de `_effectFrozenOfTick` (`:767`, vidée `:2519`, une case par tick dans la boucle `:2524`).
  - L'indice du tick en cours posé en tête de `RunEntityBlock(tick, …)` (`:2905`), et un accès de test comme `EffectFrozenOfTickForTests` (`:776`).
  - `CameraDirector.UpdateCameraFollow` reçoit la liste.
  - `CameraMath.AdvanceCameraSmoothing` reçoit un paramètre **optionnel** final (coups par tick) qu'il ajoute à `swayOffsetY` à chaque pas non calé.
  - Aucun champ dans `CameraSway` : c'est un singleton de session, et un seul des six `ResetSingletons` de test le remet à zéro (`SaveGameDirectorTestSupport.cs:134`, h4b `check/check.md` §1.2).
- **Rien** dans le moteur, le convertisseur ou les données.

### 3.3 Tests qui bougent ou non (FAIT, lus ; liste prédite à recompter en T0)

| Test | Bouge ? |
|---|---|
| `AlundraNativeSpriteRegistryTests.cs:50-66` (`:55` `Assert.Equal(6 + 1, …)`) | **oui** : le compte d'avant + 1 clé `(E, 13)` (dérivation, règle du §1.24 point 4) |
| `AlundraWaveSwordLifeTests.cs:166-188` (`:181-183` `(3 + 3 + 1, …)`, `registry.Count`) | **oui**, idem |
| S5 `AlundraWeaponSpawnSeamTests.cs:238-259` | non. Les tomes ne passent pas par le tir (ensemble câblé `{27, 28, 29}`) et `(E, 13)` n'est pas la clé `(E, 17)` que S5 inscrit lui-même |
| `AlundraNativeSpriteRegistryTests.cs:84-96`, `:205-251` | non (types listés, jamais −1 ; clé libre `(C, 25)`) |
| B6_R `AlundraRecordBirthTickTests.cs:364` (enregistrement 1 de la carte 321, type 469) | non si `(E, 13)` ne liste que `{17}` ; à remesurer si 469 est ajouté |
| Caméra : `AlundraCameraSwayFollowTests.cs:41-133`, `AlundraWorldProxyCameraFollowTests.cs:246-477` (17 appels de `AdvanceCameraSmoothing`, 2 de `ComputeSmoothedCameraTarget`) | non : paramètre optionnel. Aucun test n'appelle `UpdateCameraFollow` directement |
| `AlundraBirthBoxTests.cs:131`, `AlundraSpawnBySpriteIndex*Tests.cs:55,102` (naissance de tous les préfabs, sans tick) | non |
| Tests qui font naître le sprite 17 et font tourner des ticks | aucun (aucun indice 17 dans les appels de `SpawnEntityBySpriteIndex` des tests) |

## 4. Plan proposé : E20.h4c2 (phase A inerte, phase B visible)

**Prérequis.**
- h0a : cadre, tables d'entrées, coutures D-H-4, annulation D-H-5.
- h4a : départ commun, garde `0x800351B8`, états `0x32`-`0x34`, `DecreaseMp`.
- f5 (D-F5-5).
- E20.e0k2/e0k3, e0a3b, e0b1 (faits).
- La fin du chantier de restructuration et la garde des fichiers neufs (Q-R-7).
- Ni f6a, ni h4b1, h4b1b, h4b2.

**Périmètre.**
- `Alundra/Ai/Items/RockFallAi.cs` (neuf), `NativeSpriteRegistry.cs`, les entrées `0x2C` des tables de h0a.
- `IEntityWorldContext.cs`, `WorldProxy.cs`, `CameraDirector.cs`, `CameraMath.cs`.
- `Alundra.Tests/`.
- L'annexe `docs/plan-e20-annexe/e20-h4c2/` (versement de `k/`, `tools/`, `dis/`).
- Ce plan, l'index des ADR et un ADR (dernier numéro du dépôt parent : 0075 ; à vérifier à l'exécution).

**Hors périmètre.** E3 et le type 16 (h4c1) ; l'inscription d'E13 pour le type 469 ; la blessure du héros (E20.c2b) ; les dégâts ; les murs et marches (Q-H4C2-3).

**Partage des phases.**
- **A** (T1), inerte : table, pilote, E13, annulation, `Register`, caméra. Les tests inscrivent leurs clés dans un registre neuf.
- **B** (T2) : `CreateForProduction` appelle `Register`, les entrées `0x2C` sont câblées.
- **Visible en B** : Rond avec le livre de terre niveau 2 (objet 44) → 15 rochers, un sursaut de 2 px par impact.

**T0 — Annexe, rejeux, sondes, liste prédite** (aucun code).
- (a) Rejeux depuis des copies, identiques aux sorties versées (faits ici, §8) :
  - `trace_cast.py 0x2C` ;
  - `h4_vec_drivers.py` (0 écart, 174/176, annulation 30/30, garde 19/19) ;
  - `h4_vec_handlers.py` (E13 27/27) ;
  - `c5_camera.py` (3000, 0 écart) ;
  - `c9_replay.py` en repointant `VEC` (`critic/c9_replay.py:11`) : 770/770.
- (b) Verser `k/`, `tools/` et `dis/` ; le README nomme l'artefact de §1.8 et marque les clés de h4 de tous les tomes comme à refaire.
- (c) **Sondes de la DLL** (un arrêt si l'une diffère) :
  - (s1) Générateur : héros au repos, un tick par image. L'état après chaque image vaut `rng0(t+1)` de la clé, et le nombre de pas vaut `draws + 1` (§1.20 point 3).
  - (s2) Naissance du pilote : Z de la DLL à b = `0xF80001` (clé − 1), x et y de la clé, `ComputeTerrainHeight()` du nouveau-né = 0 sur le champ plat.
    - Sur un plateau, la hauteur de case qui donne un terrain à plus de 256 px est INCONNUE : à mesurer par `FlatCells.Create(cell:…)`, sinon le test du terrain se fait en pur.
  - (s3) Naissance hors de la grille (x < 0, comme le rocher 11) : pas d'exception, x = 10 et `ForceAdjusted` à b.
  - (s4) `ForceResetAnimationFlag` du sprite 17 levé 46 ticks après l'animation cible 1, à 1, 2 et 3 ticks par image.
  - (s5) Sons : 55 au tick b, 45 au tick d'E13 (`RecordingSoundPlayer`).
  - (s6) Attaque courante (8, 64) et boîte 72 × 48 × 39 dans les animations 0, 1 et 2.
  - (s7) Rien n'appelle `UpdateGroundSlope` pour le type 17 (témoin négatif sur une bande d'eau, §1.9).
- (d) Table de correspondance des octets lus par les vecteurs d'E13 et de la caméra vers les membres de la DLL :
  - `+0x88` → `TargetAnimationId` ; `+0xAC` → `ForceResetAnimationFlag` ; `+0x6C` → `Flags` ;
  - les mots du bloc de tremblement → `CameraSway` ; la cible et le défilement → l'état lissé.
  - Un octet sans membre arrête la tranche.
- (e) Liste prédite : les deux épingles du registre, recomptée au HEAD de départ (qui peut avoir les clés de h3, f4-f6, g1a, e0c).

**T1 — Tests puis code, phase A** (chaque cas « sans effet » accolé à un positif).
- (t1) `T1_TheSchedule_IsTheOneOfTheExecutable_WordByWord` : les 16 mots de `0x80023504` lus dans l'EXE à l'exécution (modèle `AlundraHeroRunTablesTests.cs:42`).
- (t2) `T2_TheDriver_AgainstTheCasesOfTheRealCode` : les 609 cas de `driver_2c_cases.json` sur un monde factice, **en un seul test**. On compare le journal de naissance (x, y, z − 1, direction), le générateur, la phase, le compteur, l'animation du héros, le statut et `ForceZ` des nouveau-nés.
- Accolés à (t2) :
  - (t3) un refus de naissance tire quand même et ne détruit rien ;
  - (t4) `eq` garde, `lt` détruit (comparaison stricte, Z brut = `PosZ + 1`) ;
  - (t5) deux rochers au compteur 224 (ordre r1, r2 par rocher) ; la phase 1 au compteur 240 dans le même appel ; 0x117 contre 0x118 ; la phase 2.
- (t6) La garde : pente 4, animation `0x31`, `0`, `0x34` → annulation et 1 en phases 0 et 1, rien en phase 2.
- (e1) `E1_E13_AgainstTheVectors` : les 40 vecteurs de `vec_e13` (adaptateur de T0 (d)) en un test.
- (e2) Première passe : coup 2, `TargetAnimationId` 1, drapeaux `0x132014` → `0x132044`.
- (e3) `+0xAC` à 0 : rien ; à 1 : `DestroyEntity(−1)` (statut `FlagToDestroy`, aucun effet).
- (e4) `TargetAnimationId` déjà 1 à la première vue (annulé) : ni coup ni changement de drapeaux.
- (c1) `C1_TheCancel_*` : un rocher au statut 2 → 3, `BlockedByEntity` nul, animation cible 1, drapeaux intacts. Accolés : statuts 1, 3, 4 intacts ; un autre type au statut 2 intact (une cible de type 25) ; le rocher du plus haut emplacement atteint.
- (r1) Registre : `(E, 13)` pour 17 seul, absent pour 469, 16, 3, 27-29 ; `Register` sur un registre neuf.
- (r2) L'invariant S5 (`PlayerManager.UnhandledWeaponSpriteCodes`) est inchangé.
- (k1) `K1_TheKick_*` (pur) :
  - coup 2 au pas 0 depuis le repos → décalages de défilement Y `[+2, +1, 0, 0, 0, 0]` (coups +2, −1, −1) ;
  - pas de calage : coup perdu ; coup au pas 1 d'une image de 3 pas ; deux coups dans deux ticks d'une image ; bornes de la carte ;
  - sans liste : identique à aujourd'hui (les 17 appels de test).
- (k2) Le monde : le coup est rangé à l'indice du tick (accès de test), valeur 2 jamais 4 (deux E13 au même tick), vide à l'image suivante, rien pendant une transition.
- Puis le code.

**T2 — Tests de monde, rouges avant la phase B, puis la phase B.**
- **Banc** : `ArcRun` sur le champ plat de 52 × 60 cases, entités de la carte retirées, natifs allumés, `PlaceHero(100, 100, 0)`, graine posée avant la première image, tome par `Give(44)`, `SetItem(44)`, `SetMp(4, 4)`, `Press(Circle)`.
- (w1) `W1_TheWholeCast_AgainstTheKey` : les 15 rochers de `cast_2c_pf.json` à 1 tick par image, sans aucun écart. On compare :
  - naissance, position, Z − 1 par tick ;
  - `+0x144`, statut 3, `TargetAnimationId` 2 puis 1, `+0xAC`, statut 4, case libre, drapeaux ;
  - sons 55 et 45 aux bons ticks, coups aux 13 ticks de la clé ;
  - PM 3, objet libre au tick 309.
- (w2) `W2_TheEdgeRock` : le rocher 11 (x = 10, `ForceAdjusted` à b, statut 3 à b+1, vie de 49), accolé à un rocher intérieur.
- (w3) `W3_TheChronology_At2And3TicksPerFrame` : b+31, b+32, b+33, b+79, b+80 pour les rochers sans `ForceAdjusted` observé.
- (w4) `W4_TheInterruption_AgainstTheKey` : `cancel_2c_pf_t100` et `t60`, la garde posant l'animation à `0x31`.
  - Rochers 6 et 7 : statut 3, animation cible 1, drapeaux `0x132014`, chute de 8 px/tick, mort au tick 147, aucun coup.
  - Accolé : la même chose par la branche verrouillée (`PlayerControlFlags`) si le site de h0a est en place.
- (w5) `W5_TheTargets_AgainstTheKey` : `targets_2c_pf_inv0` (touches et ticks par cible, attaquant de type 17).
- (w6) `W6_OverDeepWater_TheRocksSurvive` : bande d'eau à x ≥ 168, chronologie identique à (w1), accolée à un type à gravité qui meurt (témoin).
- (w7) `W7_AfterThePhaseB_TheProductionPath` : la même chose sans registre de test ni couture.
- Puis la phase B : `CreateForProduction` appelle `Register`, les entrées `0x2C` sont câblées.

**T3 — ADR, plan, verifiers frais (deux).**
- **Mutants (30), tous tués** :
  - pilote :
    - r1 et r2 échangés ; direction prise sur r1 ; `p + 0xA00` → `p` ; X32 et Y32 échangés ;
    - Z + 128 px ; ForceZ −4 px ;
    - comparaison du terrain `<=` ; terrain lu sans le +1 ; naissance nulle qui détruit ;
    - entrée du tick 224 décalée ; un seul tirage par rocher ; tirage après la naissance ;
    - garde absente en phase 1 ; seuil 0x118 → 0x119 ; phase 2 qui rend 1 tout de suite ; phase 1 non enchaînée au compteur 240 ;
  - E13 :
    - sans coup ; à coup additionné (`+=`) ; sans `0x40` ; qui n'efface pas `0x30` ; qui détruit sans `+0xAC` ;
    - qui donne un coup à la première vue d'un rocher annulé ;
  - annulation :
    - de tous les types ; qui touche les statuts 3 ; qui oublie le plus haut emplacement ;
  - caméra :
    - coup de signe inversé ; coup appliqué au pas de calage ; coup rangé par image (tué par (k2) et (w1) à 2 ticks par image) ; coup consommé un tick trop tard ;
  - registre :
    - `(E, 13)` aussi pour 469 (tué par (r1)).
- **Mutants équivalents à nommer** : le `ResyncControllerFromFlags` d'E13 (prouvé, §1.3). L'annulation qui toucherait les statuts 3 l'est probablement aussi, car E13 pose toujours l'animation 1 avant (à vérifier).

**Acceptation.**
- **Oracle** : rejeux de T0 (a) identiques ; deux exécutions des scripts de refonte donnent des md5 égaux (`k/MANIFEST.md5`).
- **DLL.**
  - (t1) à (t6), (e1) à (e4), (c1), (r1), (r2), (k1), (k2) verts en T1.
  - (w1) à (w7) rouges avant la phase B, verts après.
  - Suite en Release (copie neutralisée) et en Debug, zéro échec.
  - Tests déplacés ⊂ liste prédite (les deux épingles).
  - `cmp` de la DLL ; traces inchangées ; moteur, convertisseur et données sans diff.
- **Preuve de retrait.**
  - Retirer l'appel `Register` de `CreateForProduction` rend rouges exactement (w1) à (w5), (w7) (les rochers restent au statut 3, jamais détruits), (r1) et les deux épingles. (t2) à (t6), (e1) à (e4), (c1) et (k1) restent verts.
  - Retirer le câblage des entrées `0x2C` rend rouges (w1) à (w7).
  - Retirer le rangement du coup rend rouges (k2) et les coups de (w1).
  - Retirer l'annulation rend rouges (c1) et (w4).
- **Recette de l'auteur** (non bloquante).
  - Préréglage `arsenal-b-m14` (livres 44, 46, 48, 50, PM 4/4, `TestSaves.cs:124-129`), puis le livre de terre niveau 2 mis en objet du Rond par l'inventaire.
  - Attendu : 15 rochers autour d'Alundra, sons 55 et 45, un sursaut de 2 px à chaque impact, PM 4 → 3, menu refusé jusqu'à la fin (~309 ticks).
  - Ne pas ajouter de préréglage : `AlundraTestSavesTests.cs:88-91` et `:429-433` bougeraient.

**Arrêts.**
- Une clé refaite diffère entre deux exécutions.
- Une sonde (s1) à (s7) diffère.
- Une valeur de (w1) diffère d'un tick ou d'une unité à 1 tick par image (aucune tolérance).
- f5, h0a ou h4a ne sont pas faites.
- Un test hors de la liste prédite bouge (B6_R, S5, caméra).
- Un octet lu par un vecteur n'a pas de membre.
- Un besoin de toucher au moteur, au convertisseur ou aux données.
- Un nom refusé par `check_names.py`.

**Budget.** Un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux `verifier` frais après.
**Retour arrière.** Phase B : retirer `Register` et les entrées `0x2C` (retour aux bouchons). Phase A : retirer le fichier, le membre de contexte, la liste par tick et le paramètre optionnel.

## 5. Questions pour l'auteur (défauts réversibles)

- **Q-H4C2-1 (le sursaut de caméra, précise Q-H4B-4).** À chaque rocher qui touche le sol, l'original décale l'image de 2 pixels pendant un tick, puis la ramène en deux ticks. Cela fait 15 petits sursauts en 6 secondes, et aucun pour un rocher annulé.
  - (A, défaut) Le porter : une liste par tick et un paramètre optionnel dans trois fichiers de caméra, 6 tests.
  - (B) L'ignorer : rien d'autre ne change.
- **Q-H4C2-2 (le rocher de la carte 321).** La 321 place un rocher de la même famille (sprite 213 de la carte, enregistrement 1, né par un script, il blesse le héros). Il est aujourd'hui inerte : il ne joue pas son impact.
  - (A, défaut) Le laisser inerte : `(E, 13)` pour le seul sprite 17.
  - (B) L'inscrire aussi : il jouerait impact, son 45 et sursaut, puis disparaîtrait, mais ne blesserait toujours pas le héros avant E20.c2b. B6_R serait à remesurer.
  - La 321 est-elle jouée dans l'histoire actuelle ? Je ne le sais pas (INCONNU).
- **Q-H4C2-3 (rocher qui bute en l'air).** Quand le héros est à moins de 120 px du bord de la carte, un rocher que la limite ramène au bord (1 sur 15 dans la scène témoin) passe tout de suite à son animation d'impact. Il est alors en l'air à 240 px, avec son et sursaut, et continue de tomber invisible. C'est l'original (même cause que Q-H4B-1 pour les orbes d'eau et les murs).
  - (A, défaut) Fidèle.
  - (B) Corriger : le rocher ne prend l'animation d'impact qu'au sol (écart à documenter, un test de plus).
- **Q-H4C2-4 (organisation des clés refaites).** L'artefact des emplacements touche les clés des huit tomes. Chaque tranche refait la sienne avec les scripts versés (défaut : h4c1 est la suivante), ou une tranche d'outils commune ? Coût de la seconde voie : une tranche de plus sans code.

## 6. Écarts à reporter dans le plan (§1.31 et ailleurs)

1. **Tableau du découpage, ligne h4c2.** Prérequis `h4a` → `h0a, h4a, f5 (D-F5-5)` (ordre ferme du §1.20). La ligne ne dépend pas de f6a, confirmé pour le seul type 17 ; h4c1 en dépend toujours pour le type 16.
2. **`h4/report.md` §3.**
   - « Durées de vie 49 à 82 » devient 49 à 81 (81 pour un rocher intérieur).
   - Les fins d'objet des tomes 0x2D, 0x2F, 0x30, 0x32 (344, 150, 183, 243) sont 343, 149, 182, 242 ; les douze boules du feu 1 deviennent onze (§1.8).
   - **Toutes les clés de h4 sont à refaire** avant d'être citées par une tranche.
3. **Fait 8 du §1.31** (« le coup doit être consommé par le pas de caméra du même tick, rangé par tick ») : confirmé. À ajouter : assigné et non additionné, perdu sur un pas de calage, absent pour un rocher annulé.
4. **Champ du plus haut emplacement.** h4b critique §3.4 et `Q-H4B-2` ne concernent pas h4c2 : l'annulation de la terre 2 itère toutes les entités vivantes, la borne inclusive équivaut à « toutes ». Aucun champ du plus haut emplacement n'est nécessaire (confirmé par `dis/cancel_80034BDC.txt`).

## 7. INCONNU

- Le programme de la carte 321 qui fait naître le rocher 469, et si la 321 est sur la chaîne.
- La hauteur de case qui donne un terrain à plus de 256 px au-dessus du héros. Le test « détruit sur place » du pilote n'est atteignable que sur un tel plateau ; la DLL calcule `ComputeTerrainHeight()` à chaque lecture (sonde s2).
- Le comportement de la DLL pour une naissance hors de la grille (sonde s3), et la levée de `ForceResetAnimationFlag` à 2 et 3 ticks par image (sonde s4).
- Le rendu en jeu du sursaut (non vu).
- Le cas d'un rocher qui bute contre un mur ou une marche : même mécanisme que le bord, mais sans clé, car le banc plat n'en a pas.
- Où les livres de terre niveau 2 sont donnés sur la chaîne actuelle (Q-H4-7 : cartes 17 et 439 en coffre, scripts `0x82` cartes 17, 31, 77, 80, 158, 439, 443). Non relu.

## 8. Preuves de cette session (rejouables)

Rejeux sans écart :
- `trace_cast.py 0x2C` : sorties de `trace_summaries.txt` à l'identique.
- `h4_vec_drivers.py 1500` : 0 écart.
- `h4_vec_handlers.py 1500` : 0 écart, E13 27/27.
- `c5_camera.py` : 3000 essais, 0 écart.
- `c9_replay.py` : 770 vecteurs, 0 échec.

Désassemblages relus par moi : pilote, E13, E3, annulation, garde, enveloppe, passe d'événements, initialisation du chargement de carte, pas du générateur, suivi de caméra, création. Tables lues dans l'EXE : `0x80023504` et les tables de 32 directions.

Dossier de sortie `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4c2/` :
- `keys/` : `cast_2c_pf.json`, `targets_2c_pf_inv0.json`, `targets_2c_pf_inv30.json`, `cancel_2c_pf_t100.json`, `cancel_2c_pf_t60.json`, `driver_2c_cases.json`, `README.txt`, `MANIFEST.md5`
- `tools/` : `cast_rich.py`, `cast_pf.py`, `cast_slotinit.py`, `cast_rich_water.py`, `targets_pf.py`, `targets_pf2.py`, `cancel_pf.py`, `driver_keys.py`, `read_tables_2c.py`, `anim_summary.py`, `sprite17_dump.py`, `coll_dump.py`, `analyze_cast2c.py`. Ils se lancent depuis une copie de `e20-h4/work/oracle` où on les dépose.
- `dis/` : 11 désassemblages
- `h4copy/`, `critic_copy/` : copies de travail des harnais