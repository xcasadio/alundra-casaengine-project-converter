# E20.c2b, angle « la DLL au HEAD » (lecture seule)

## 0. Cadre

- HEAD = `e9e8c58ad7cfc2ba90d06b163de95f8535b05ffc`, branche `chantier/theme2-attaques-objets`.
- Tout le code de `Alundra/` et `Alundra.Tests/` est lu par `git show HEAD:` ou `git grep … HEAD`. Les numéros de ligne sont donc ceux du HEAD.
- L'arbre de travail contient une modification de `ChestAi.cs`, d'un autre agent. Je ne l'ai pas lue.
- Moteur : pointeur de sous-module du HEAD, `92d8e847`, lu par `git show`.
- Je n'ai lancé ni build ni test.
- Les chemins ont changé depuis la restructuration (ADR-0075) : `AlundraPlayerManager.cs` est devenu `Alundra/Hero/PlayerManager.cs`, `AlundraHitPass.cs` est devenu `Alundra/Combat/HitPass.cs`, etc.
- Étiquettes : [FAIT] (adresse ou fichier:ligne), [MESURÉ] (mesure sur le binaire ou l'oracle), [INFÉRENCE] (avec sa base), [INCONNU].
- Le binaire France tranche. J'ai relu moi-même l'entrée `0x80031A68`, le bloc de blessure de `MovePlayer`, le HUD et le chargement de carte (section 5).

## 1. Synthèse

1. [FAIT] Un coup atteint déjà le héros dans la DLL. La passe de coups `HitPass.Run` le traite comme une cible : elle écrit `TouchingEntity`, le délai de 25 ticks et trois étincelles. Personne ne consomme ensuite `TouchingEntity` du héros.
2. [FAIT] Le trou est unique. `PlayerManager.MovePlayer` n'a ni l'appel `0x80031C44`, ni le bloc de blessure `0x80031C58-0x80031C98`, ni le chemin `controlFlags == 0x20`. Le point d'insertion est entre les lignes 355 et 358 de `PlayerManager.cs`.
3. [FAIT] Tout ce qui entoure ce trou est déjà porté :
   - l'entité porte les PV et les stats la reflètent (c2a) ;
   - la fiche de défense, les bottes, la régénération, l'alarme de PV bas et le HUD existent ;
   - les états `0x1C`, `0x3A` et `0x3B` ont leurs gestionnaires ;
   - le recul, le son 23 et les compteurs sont portés ;
   - `UpdatePlayerCarriedEntity(2)` est réel.
4. [FAIT] Il n'existe aucun clignotement. `Entity.IsVisible` suffit côté moteur. Trois écrivains de `IsVisible` existent déjà dans la DLL (D18).
5. [FAIT] La cause principale de casse dans les tests est le constructeur de `HitArena` (T6). Il pose le héros dans le corps d'un Beannoïde puis laisse 6 ticks passer. Dès que le héros peut être blessé, il sera blessé pendant ce réglage.
6. [INFÉRENCE] Après c2b, tout sprite de carte qui a une fiche d'attaque peut blesser le héros par la passe générique, même sans IA native. 169 des 512 entrées de `balance.json` portent une attaque de nibble ≠ 0 [MESURÉ].

## 2. Inventaire de la DLL au HEAD

### 2.1 Où un coup ou un contact atteint le héros aujourd'hui

**D1.** [FAIT] Le héros est dans la liste de la passe de coups.
- `BuildHitList` (`Maps/WorldProxy.cs:3099-3106`) met le héros en tête s'il est actif et non bloqué.
- Ses drapeaux sont `0x3118C` [MESURÉ]. `sprite-records.json` donne MoreFlags 140, CanPickup 17 et ShadowType 3, et `ArrowAi.cs:92` cite la même valeur. Il est donc ClassB (`0x8`).
- Un attaquant HitsClassB le touche. Le Beannoïde a les drapeaux `0x103` (`Alundra.Tests/Combat/HitEffectWorldTests.cs:19`).
- Le recouvrement des boîtes est testé en `Combat/HitPass.cs:142-147`.

**D2.** [FAIT] Sa fiche pour la passe est l'enregistrement 0 (`EntityScriptProxy.BalanceRecord`, `Entities/EntityScriptProxy.HitPass.cs:24`).
- Toutes ses réponses valent 0 (`balance.json`, entrée 0, `DamageResponses`).
- Le test d'immunité `(r & 0xC0) == 0x80` (`HitPass.cs:149-150`) ne l'exclut donc jamais.

**D3.** [FAIT] Ce que la passe écrit sur le héros :
- `TouchingEntity = attacker.SelfEntity` (`HitPass.cs:163`) ;
- `FrameCollisionTickCounter = 0x19` (`:166`, constante `:28`) ;
- `attacker.HitCounter++` (`:167`) ;
- trois étincelles, effet 9 (`:170-176`) ;
- l'effet 4 ou 5 attaché au héros pour les nibbles 6, 7, 9 et 10 (`:154-161`, `WorldProxy.cs:3133-3134`).

Aucun PV ne bouge (`HitPass.cs:23`).

**D4.** [FAIT] L'ordre du tick suit le binaire. Dans `WorldProxy.RunEntityBlock` (`:2914-3028`) :

| Passe | Ligne |
|---|---|
| `MovePlayer` | 2936 |
| programmes | 2948 |
| compteurs | 2969 |
| listes | 2974-2977 |
| horloge d'animation | 2985 |
| physique | 3009 |
| effets actifs (corps vide) | 3023, `:3071-3073` |
| passe de coups | 3026 |

- `RunCountersPass` (`:3040-3064`) efface `TouchingEntity` et `HitCounter` (`:3060-3061`) et décrémente `+0x1CC` et `+0x1D0` (`:3050-3058`).
- Ce que la passe écrit au tick N est donc lu par `MovePlayer` au tick N+1, avant l'effacement. C'est l'ordre du binaire (`c2-binary.md` §2.1).
- Porte fermée (`gateClosed`) : seules les listes tournent (`:2916-2925`). `TouchingEntity` du héros survit alors jusqu'à la réouverture. L'oracle D1 donne la même chose pour `0x48`.

**D5.** [FAIT] Il n'y a aucun lecteur de production de `TouchingEntity` du héros :
- `Combat/HitOpcodes.cs:30` : l'opcode `0x87`, sur une cible cherchée, qui peut être le héros ;
- `Entities/EntityScriptProxy.cs:1968` : le choix des programmes D ou C, hors joueur ;
- `Combat/Damage.cs:81` ;
- `Maps/WorldProxy.cs:3060` : l'effacement.

**D6.** [FAIT] `Damage.ComputeNewHp` et `ResolveBalanceTarget` (`Combat/Damage.cs:79-84` et `:24-59`) sont des fonctions pures.
- Un seul appelant de production existe : `Ai/EnemyHelpers.cs:232`, dans `HitReaction` (`:230-242`), pour D1 (Beannoïde) et D9 (Muruta). Il agit sur l'ennemi touché, jamais sur le héros.
- Le commentaire `Damage.cs:10-11` (« No production code calls them yet ») est périmé.
- Tous les arguments du héros existent :
  - fiche : `EquipmentRecords.HeroSheet` (`Items/EquipmentRecords.cs:76`) ;
  - enregistrements : `host.EquipmentRecords` ;
  - loquet d'animation : `IEntityWorldContext.HeroAnimationLatch` (`Maps/IEntityWorldContext.cs:154`, défaut 0 ; `WorldProxy.cs:3085-3087`).
- Le héros atteint le monde par `player.ScriptHost as IEntityWorldContext`, comme `PlayerManager.cs:1414` le fait pour le port.
- La direction se calcule avec `ScriptHelper.GetDirectionToTarget(int, int)` (`Events/ScriptHelper.cs:77`). Elle est vérifiée contre le binaire par `EnemyHelpersTests.GetDirectionToTarget_EqualsTheBinarysDirectionRoutine_OnEveryState` (`Alundra.Tests/Ai/EnemyHelpersTests.cs:164`).

### 2.2 Le site manquant dans `MovePlayer`

**D7.** [FAIT] L'ordre actuel dans `Hero/PlayerManager.cs` :

| Étape | Ligne |
|---|---|
| portail | 311-318 |
| `BlockedByEntity` → fin | 323-330 |
| branche verrouillée → fin | 337-350 |
| `Flags \|= Gravity` | 354 |
| `PlayerAnimationEffects.Create(FreeMode)` | 355 |
| `UpdateItemUseDriver`, stub vide (`:1688-1690`) | 358 |
| nage | 377-382 |
| échelle | 399-407 |
| trois appels avant la chaîne | 411-413 |
| chaîne d'états | 419-636 |
| fin (`EndOfMovePlayer`) | 642 |

- Binaire : `0x80031C38` (drapeau), `0x80031C3C` (`Create(0)`), `0x80031C44` (`jal 0x80031A68`), bloc `0x80031C58-0x80031C98`, test PV 0 en `0x80031CA8`, tour du pilote en `0x80031CC4`.
- L'entrée prend donc place entre les lignes 355 et 358, avant la nage (377).
- Rien ne la porte : aucune occurrence de `FindWarpFacingDirection`, `DamageTaken` ou `HurtState` dans `Alundra/`. Seuls les commentaires `PlayerAnimationEffects.cs:14` et `PlayerManager.cs:352-353` nomment `0x80031C44`.

**D8.** [FAIT] La fin de `MovePlayer` passe par `EndOfMovePlayer` sur toutes les sorties (appels `:328`, `:348`, `:380`, `:642`).
- Le miroir vers les stats est gardé par `EntityScriptProxy.LifePulledFromStats` (`EntityScriptProxy.cs:97`, `PlayerManager.cs:652-663`).
- Un PV perdu dans le bloc de blessure est donc miroité le même tick, comme le binaire le fait en `0x80031C98 → 0x80032914`.

**D9.** [FAIT] Pour le bloc de blessure (`0x80031C58-0x80031C98`), les pièces de la DLL sont :

| Pièce | État au HEAD |
|---|---|
| `WeaponChargeTimer = 0` et `WeaponRecordPointer = 0` | champs existants, utilisés `:326-327`, `:343-344`, `:675-676` |
| `UpdatePlayerCarriedEntity(player, 2)` | réel depuis g1a (`:1413-1414` → `Hero/CarryRules.cs:128`) ; la nage l'appelle déjà en mode 2 (`:677`) |
| pilote d'objet | stub `UpdateItemUseDriver`, décision D-E20-19 (ADR-0054) |
| `Slope_18c` | calculé par les sondes du héros à chaque tick (`EntityScriptProxy.cs:1877-1882`, `:2234` `Slope_18c = bestFlagMask >> 9`), lu au tick suivant |
| état `0x3A` | constante `SwimHurtAnimationId` (`PlayerManager.cs:145`) |
| état `0x1C` | constante `HurtBounceAnimationId` (`:130`) |
| état `0x31` | aucune constante |

- Le binaire lit la pente en `0x80031B98`, avant `CheckAndExecuteWarp`. Cet appel n'écrit pas la pente côté DLL (`PortalTrigger.TryGetTrigger` est pur).

**D10.** [FAIT] La branche verrouillée (`PlayerManager.cs:337-350`) n'a ni le chemin `controlFlags == 0x20`, ni l'appel d'annulation d'objet `0x800350C0`. Le commentaire `:346` nomme cet appel sans le faire.
- `CancelItemMode` est un stub vide (`:758-760`), appelé seulement par le groupe `Skip` de la nage.
- `GameState.PlayerControlBits.ForcedSequence = 0x20` (`Session/GameState.cs:88`) n'est posé nulle part en production. `git grep` ne trouve qu'un test (`Alundra.Tests/Dialogues/DialogueOpcodesProductionTests.cs:330`).
- `ForcedWeapon = 0x80` existe (`Events/EventProgramRunner.cs:646-674`, opcodes `0xC0` et `0xC1`). La comparaison du binaire porte sur le mot entier (`bne $v1, 0x20`, `0x80031C0C`).
  - [MESURÉ] Oracle D1 : `0x80` seul suit le chemin normal ; `0xA0` perd le coup.
- Le chemin `== 0x20` n'est donc atteignable que par un test qui pose le mot.

### 2.3 Bouchons, gardes et commentaires qui nomment c2b ou la blessure

**D11.** [FAIT]
- `PlayerManager.cs:48` : la nage est entrée par la blessure de c2b.
- `PlayerManager.cs:129-130` : `0x1C` est le recul après un coup, atteint depuis `0x31` (c2b).
- `PlayerManager.cs:144-145` : `0x3A` est « entered by the hurt block of E20.c2b ».
- `PlayerManager.cs:740` : « Nothing enters 0x3A or 0x3B yet ».
- `PlayerManager.cs:1411` : « The sites of the wound (`0x80031C64`, E20.c2b), the exit square and the death stay named stubs at their place ». C'est inexact : il n'existe aucun stub nommé au site de la blessure, la DLL n'a pas de bloc de blessure.
- `Items/EquipmentRecords.cs:72-77` : `HeroSheet` attend c2b.
- `Hero/PlayerAnimationEffects.cs:14` : place de l'entrée des dégâts.
- `PlayerManager.cs:352-358` : `UpdateItemUseDriver` « Stub until E20.c2 ».
- `Ai/Objects/LiftableAi.cs:21` et `Hero/CarryRules.cs:14-15` : le code −1 est écrit par le mode 2 « a wound, deep water, a death ».
- Commentaire périmé : `PlayerManager.cs:64-67` (« no HP system wired to the player yet … Hp stays the C# default 0 ») ne vaut plus depuis c2a.

### 2.4 États `0x31`, `0x1C`, `0x3A`, `0x3B`

**D12.** [FAIT] `0x1C` est partagé avec `0x3E` par `BounceTick` (`PlayerManager.cs:599-602`, `:931-946`).
- Sans `TryUseItem` à 0, rien ne se passe jusqu'au drapeau de maintien `ForceResetAnimationFlag` (`+0xAC`).
- Ensuite la direction tourne de `+0x10`, et l'état devient Idle au sol ou `1` en l'air (`:945`).
- C'est la bizarrerie « 1 en l'air » du binaire (`c2-binary.md` §3.2). Elle est déjà couverte par E20.d1 (`Alundra.Tests/Hero/HeroRunStatesTests.cs:37`, `:436-451`, `:492`).

**D13.** [FAIT] `0x3A` et `0x3B` sur terre (pente ≠ 4) ont leur sortie (`PlayerManager.cs:573-584`) : direction `+0x10`, puis Idle, ou `0x2D` en l'air, puis `TryUseItem`.
- En eau profonde, la table de nage `SwimTable` (`:176-191`) donne :
  - `0x1C`, `0x31` et `0x3E` → `FlipThenFloat` ;
  - `0x28` et `0x3A` → `ItemOnly` ;
  - `0x3B` → `HurtEnd` (`SwimHurtEndTick`, `:742-755`).

**D14.** [FAIT] `0x31` sur terre n'a aucun `case` dans la chaîne `:419-636`.
- Le binaire a pour `0x31` le gestionnaire `0x80032680`, soit `TryUseItem(); END` (`c2-binary.md` §3.2).
- Aujourd'hui `TryUseItem` est un bouchon qui rend 1 (`PlayerManager.cs:950`). Le trou est donc invisible.
- [INFÉRENCE] Il deviendra visible dès que h0a rendra `TryUseItem` réel : un appui sur Rond pendant la blessure atteint `TryUseItem` dans le binaire, pas dans la DLL.

**D15.** [FAIT] Les animations du héros existent.
- `.anim2d` à 4 directions pour 28, 49, 57, 58, 59 et 60 (`alundra-project/Entities/Alundra/bankalundra_0_anim<N>_<dir>.anim2d`).
- Paramètres du héros (`sprite-records.json`, GUID `192c2eeb-…`) :

| Anim | Speed | Acceleration (octet `0xD`) | Sfx |
|---|---|---|---|
| 28 (`0x1C`) | 0 | 68 | 0 |
| 49 (`0x31`) | 256 | 64 | 23 |
| 58 (`0x3A`) | 256 | 64 | 23 |
| 59 (`0x3B`) | 0 | 68 | 0 |

### 2.5 Recul

**D16.** [FAIT] Le recul est porté sans code nouveau.
- `ScriptedMotion.PlanForce` (`Physics/ScriptedMotion.cs:714-775`) lit `Speed` et `Acceleration & 0xF` de l'animation. Pour le joueur, la clé est `TargetAnimationId` (`:182`).
- `0x31` donne une accélération 0, donc une force instantanée (`:738-739`, `stepX = |cible − force| >> 0`).
- [MESURÉ] Oracle A1 : `ForceX = −196608` aux ticks 18 à 21, puis `−184320` en décroissant de `12288`. L'oracle a aussi `0x1C` au tick 22 et Idle au tick 33.
- Le même tick, l'horloge d'animation recharge l'octet `+0xB4` (`0x40` protège pendant `0x31`) et l'attaque (`EntityScriptProxy.HitPass.cs:106-118`, via `StepAnimationClock`, `Maps/Loop/FrameSyncPasses.cs:282-291`).

### 2.6 Invulnérabilité `+0x1CC`

**D17.** [FAIT] Le champ est `EntityScriptProxy.DamagedTickCounter` (`Entities/EntityScriptProxy.cs:488`, cloné `:2918`).

| Rôle | Où |
|---|---|
| décompte | `Maps/WorldProxy.cs:3050-3053` |
| lecteur | `Combat/HitPass.cs:127` |
| lecteur | `Ai/Projectiles/ArrowAi.cs:101` |
| écrivain (SET) | opcode `0x86`, `Events/EventProgramRunner.cs:801-809` |
| écrivain | marque de vie `0x78` des objets lâchés, `Ai/Objects/PickupAi.cs:182` (constantes `:174-183`) |
| écrivain pour le héros | aucun |

- Le héros n'est écrivain nulle part. Il est reconstruit à chaque carte.
- [INFÉRENCE] Le binaire recopie un gabarit par case à chaque chargement de carte (voir B4), donc le compteur est lui aussi remis à zéro. Les deux comportements concordent.

### 2.7 Clignotement

**D18.** [FAIT] Il n'existe aucun clignotement dans la DLL. Les seuls écrivains de `Entity.IsVisible` sont :
- `Maps/Loop/FrameSyncPasses.cs:109` (`= false`, entité à détruire) ;
- `Ai/Objects/ChestAi.cs:88` (`= false` à la naissance de l'objet du coffre) ;
- `Ai/Objects/PickupAi.cs:285` (`= open`, appelé par le chargement de cet objet).

**D19.** [FAIT] La décision D-E20-21 (ADR-0054) rend le clignotement générique : il vaut pour toute entité, 0x86 compris.
- Condition du binaire : un 4ᵉ tick sur 4 est caché quand `(+0x1CC & 3) == 3`. Ce test est dans la passe des listes (`0x800385D0-0x8003860C`, `check.md` #24, #25).
- Résultat mesuré : 30 ticks cachés sur 120, dont le tick de la blessure (`+0x1CC = 0x77` après la passe des compteurs).
- Effets pour la DLL :
  - Une règle qui réécrirait `IsVisible` à chaque tick pour toutes les entités effacerait les deux masquages de ChestAi et PickupAi (statut `Loaded`).
  - Elle devrait donc ne toucher que les entités à compteur non nul ou qu'elle a elle-même cachées [INFÉRENCE ; `check.md` §3 point 6 : ne jamais montrer une entité `FlagToDestroy`].
- Point d'application naturel : `WorldProxy.PublishEntityFrame` (`:3157-3175`, appel `:2646`).
  - Il tourne une fois par image, après le dernier tick (D-E19-13).
  - Un tick caché peut donc être sauté dans une image de deux ticks [INFÉRENCE ; déjà relevé en `c2-dll.md` §0.5].
- Le héros est dans `_spawnedEntities` à la case 0. La passe des compteurs et `PublishEntityFrame` le parcourent.

**D20.** [FAIT] Il n'y a pas d'ombre dans la DLL : la passe `RunActiveEffectsPass` est vide (`WorldProxy.cs:3071-3073`) et `CheckEntityShadow` n'existe pas.
- Le binaire relâche l'ombre sur le même test `(+0x1CC & 3)` (`check.md` #28).
- Les ombres sont décidées par ADR-0078 : le moteur les dessine, la DLL pousse l'état. La tranche moteur n'est pas faite.

### 2.8 Son

**D21.** [FAIT] Le son 23 de la blessure vient de l'animation.
- `EntityScriptProxy.AnimationSoundOf` (`:338-341`) donne `Sfx + (Acceleration & 0x20 ? 0x100 : 0)`, soit 23 pour `0x31` et `0x3A`.
- `FrameSyncPasses.GiveImpulse` (`:342-350`) le joue au tick où le changement est vu. Le Z de l'impulsion de ces animations est 0.
- [INFÉRENCE] Aucun code nouveau n'est nécessaire pour ce son.

**D22.** [MESURÉ] Le HUD ne fait aucun son quand les cœurs baissent.
- Dans le bloc PV du HUD (`0x8004C220-0x8004C34C`), le chemin de décrément (`0x8004C280-0x8004C2B0`) ne contient aucun `jal`.
- Le seul son de ce bloc est `jal 0x800490FC` avec le son 8, en `0x8004C2F8`, sur le chemin d'incrément. Un second, en `0x8004C41C`, appartient au bloc MP.
- `c2-binary.md` §6 (« sound 9 on increments and 8 on others ») est donc inexact.
- Côté DLL, `UI/Hud/HudDirector.cs:556` dit que ces sons ne sont pas portés. Ils ne servent qu'aux hausses.

**D23.** [FAIT] L'alarme de PV bas est réelle : `Hero/PlayerAnimationEffects.cs:241-266`. Son 0x190, période 46, seulement en mode 0 et avec une vie non nulle. Elle sonnera dès que c2b fera descendre la vie.

### 2.9 PV, armures, bottes, régénération, HUD

**D24.** [FAIT] c2a est fait :
- `WorldProxy.PullHeroLifeFromStats` (`:2027-2032`), appelé à l'adoption (`:2218`) ;
- vaisseau de vie `IncreaseHpMaxAndFill` (`PlayerManager.cs:2056`), `0xBC` `IncreaseHpMax` (`:2074`), `0xC3` `RestoreLifeAndMagic` (`:2091`) ;
- `SetPlayerHp` (`:2110`).

Avec `Hp = 0`, le héros garde la main (D-E20-20) : la régénération s'arrête (garde `Hp == 0`, `:1607`) et l'alarme se tait.

**D25.** [FAIT] La fiche de défense du héros :
- `RefreshEquipmentAndSheet` (`PlayerManager.cs:1491-1505`) calcule les bottes puis la fiche. `ComputeHeroSheet` (`:1531-1549`) appelle `CombineResponse` (`:1557-1589`).
- À l'adoption (`WorldProxy.cs:2224-2226`), la fiche est calculée puis `EquipmentRecords.Clear()` vide les pointeurs sans toucher `HeroSheet` ni `BootsLevel` (`Items/EquipmentRecords.cs:43-49`).
- Les tests de c2a rejouent les groupes G, F et E de l'oracle : `HeroLifeTests` (`G_…`, `CombineResponse_FollowsTheDisassembly`, `BootsLevelOf_IsTheFirstOwnedOf_1C_1B_1A`, `Regeneration_…`).
- Dégradé : `HeroSheet` est `null` pour une montage sans tables. `Damage.ResolveBalanceTarget` rend alors la vie inchangée (`Damage.cs:26-29`), alors que le binaire lirait une fiche BSS à zéro et prendrait 1 PV [INFÉRENCE]. En production la fiche est non nulle dès l'adoption.

**D26.** [FAIT] Le HUD suit les stats : `UpdateHpAndMpCatchUp` (`UI/Hud/HudDirector.cs:559-610`), un point toutes les 8 ticks actifs.
- Les cœurs ne se voient que si la jauge est dessinée. Elle l'est quand le verrou persistant (drapeau 1662, masque `0x40000000`) a été posé (`HudDirector.cs:218-260`).
- [INCONNU] Si la jauge est ouverte au point de recette de l'auteur (question déjà posée en `c2-dll.md` §6).

### 2.10 Cases dangereuses

**D27.** [FAIT] `CombinedVramFlagsOR` est vivant pour le héros.
- `EntityScriptProxy.UpdateVramFlags` (`:2280-2309`), appelé par `RunHeroProbes` à chaque tick.
- Chaque coin qualifié contribue `walkability | (groundProperty << 8)` (`Physics/TerrainProbe.cs:85-101`), donc `0x80` = coût 1, `0x100` = coût 2, `0x180` = coût 3.
- Aucun lecteur de `& 0x180` dans la DLL. La table des coûts `{0, 1, 2, 3}` n'est pas codée.
- Les bottes : `PlayerManager.BootsLevelOf` (`:1511`) et `EquipmentRecords.BootsLevel` (`:66`, posé à l'adoption et au bout de chaque tick).
- Aucun test existant ne cible les bits `0x180`. Les tests qui posent des mots de cellule utilisent `0x4000`, `0x1800` ou `0x18` (`HeroJumpStatesTests.cs:92-110`, `HeroWaterIceTests.cs`).
- [MESURÉ] Données : 36 cartes ont des cases dangereuses ; aucune n'est sur la chaîne de l'histoire ni dans un arc (`c2-dll.md` §1.10, `check.md` #41-43).

### 2.11 Mort, case de sortie, objet porté

**D28.** [FAIT] La mort (E18) est absente : pas de branche `Hp == 0`, pas d'état `0x39/0x3C/0x4E`, pas de compteur de « Réessayer » incrémenté (`Session/GameState.cs:282-286`). L'opcode `0xBB` n'existe que dans la table de tailles (`Events/EventOpcodeSizeTable.cs:235`).
- La coupe D-E20-5 / D-E20-20 (le héros garde la main à 0 PV) ne demande aucun code.

**D29.** [FAIT] `TileAttributes & 0x80` (case de sortie) n'est jamais écrit : `EntityScriptProxy.cs:478`, doc `PlayerManager.cs:68-69`. O-H-2 reste ouvert.

**D30.** [FAIT] Le mode 2 de `UpdatePlayerCarriedEntity` est réel et testé : `Alundra.Tests/Hero/CarryRulesModesTests.cs`, `CarryLockSwimAndStatesTests.cs` (w7 verrouillage, w8 eau profonde puis C23), `Ai/Objects/LiftableAiMap392Tests.cs:120`.
- Le site de la blessure (O-G-4) lui manque seulement son appel.
- Q-G1A-2, tranchée oui le 2026-10-22 : le héros garde sa caisse tant que c2b n'existe pas.

### 2.12 Qui peut blesser le héros dans la DLL aujourd'hui

**D31.** [FAIT] Le registre natif de production (`Ai/NativeSpriteRegistry.cs:41-52`) contient : Beannoïde, Muruta, onde de l'épée, ramassage, coffre, flèche chargée, flèche, pilier, soulevables et bris.
- Il n'y a ni bombe (pas de `Alundra/Ai/Items`) ni explosion, ni toucheur d'E14 (pas de `0x56/0x58`), ni flèches ennemies.
- Le Beannoïde n'a qu'une attaque, d'attribut 1 et de puissance 1 (`balance.json`, `EntriesBySpriteIndex[335]`). `AttackForAnimation` (`Combat/BalanceRecord.cs:61-70`) la lui donne pour toutes ses animations.

**D32.** [FAIT] Tout sprite créé reçoit sa fiche (`InitializeBalance` : `WorldProxy.cs:1105`, `:2213`, `:3483`, `:3610`).
- La passe générique peut donc blesser le héros avec n'importe quel sprite à fiche d'attaque et boîte d'image, y compris ceux sans IA native, dont les animations sont jouées par l'interpréteur.
- [MESURÉ] 169 entrées sur 512 de `balance.json` ont un nibble d'attaque non nul. Les classes (`HitsClassB`) en réduisent la part dangereuse ; je ne l'ai pas dénombrée.
- Ce point nourrit Q-E14-b3 (`docs/plan-e14-ia-native.md:947`).

**D33.** [FAIT] Sur D-H-7, O-G-4 et Q-E0C-3, aucun de ces trois identifiants n'est référencé dans `Alundra/` ni `Alundra.Tests/`.
- D-H-7 concerne les bancs d'oracle de h3. La DLL n'a pas encore `BombAi`. Si un test DLL de h3 devait mettre le héros hors de la passe, le précédent est `HitPassTests.H6_…` (`Combat/HitPassTests.cs:231-243`), qui remplace `BalanceRecord` de la cible par une fiche à réponse `0x80`.
- Q-E0C-3 : les objets lâchés reçoivent `DamagedTickCounter = 0x78` à 120 ticks de leur fin (`PickupAi.cs:174-183`), mais rien ne les cache. Le clignotement générique (D19) clôt ce point sans code propre à PickupAi.

## 3. Tests du HEAD

### 3.1 Épingles explicites « le héros n'est pas blessé » (`Alundra.Tests/Combat/HitPassTests.cs`)

**T1.** `H3_TheHeroWalkingIntoABeannoid_IsTouchedAtTick17_TheCountersClearItAtTick18_TheHeroDoesNotLoseLifeYet` (`:276-296`).
- Limites actuelles : `lastTick: 18` et une clause `ignore` (`:284-285`). Épingles `:291-295` : `AnimTarget 1`, `Latch 1`, `Hp 10`, `DamagedTicks 0`.
- [MESURÉ] Oracle H3/A1 : le contact est au tick 17 et la blessure au tick 18 (PV 10 → 9, état `0x31`, direction 24 → 8, `+0x1CC` = 119 après le tick, son 23). `0x1C` arrive au tick 22, Idle au 33, Moving au 34. Ces épingles et la limite doivent disparaître.

**T2.** `H1both_TheSwingAndTheBodyOfABeannoid20PixelsAway_HitEachOtherAtTick1_SixSparks` (`:299-317`).
- Limites actuelles : `lastTick: 2` et une clause `ignore`. Épingles : `:309-310` (`0x10`), `:316` (`Hp 10`).
- [MESURÉ] Oracle `H1-both` : héros à (232, 96), contact au tick 1, `0x31` au tick 2, PV 9.

**T3.** `WhileTheContactLasts_ABeannoidTouchesTheHero_EveryTwentyFiveTicks_AndHisDamagedCounterStaysAtZero` (`:320-339`).
- Épingles : touches aux ticks `{1, 26, 51, 76}` (`:332`), `DamagedTicks == 0` (`:336`), `Hp == 10` (`:337`), 4 étincelles (`:338`).
- [MESURÉ] Oracle A1-pad0 (même scénario : héros immobile à 20 px) : touché au tick 1 seulement, blessé au 2, repoussé de 232 à 198,90625, aucune seconde touche en 260 ticks. La liste d'épingles devient `{1}`.

**T4.** `TheReachBehindTheHero_Beyond9Pixels_IsOnlyContactedByTheLateFrames_BecauseTheHeroIsNotHurtYet` (`:456-462`).
- Elle épingle la touche au tick 12 pour `dx = −10` (`:461`), alors que l'oracle n'en a aucune (`Assert.Null(ReadXSweep()[-10])`, `:460`). Elle doit devenir « nulle ».

**T5.** Épingles `Hp == 10` sans que le héros soit touché :
- `H1_TheBasicSword_HitsABeannoid40PixelsAwayAtTick5_…` (`:148-149`, héros à 192 + 40 px de la cible) ;
- `ARealArcOnMap14_TheHeroStrikesTheBeannoidOfRecord0_…_NoWound` (`:374-375`).

Je les attends inchangées [INFÉRENCE : le corps du Beannoïde est à plus de 20 px dans ces deux scénarios].

### 3.2 Comparaisons à l'oracle arrêtées avant la blessure

**T6.** `Alundra.Tests/Hero/HeroRunArenaTests.cs`.
- Le helper `FirstHurtTick` (`:48-53`) coupe la comparaison un tick avant le premier `0x31` de l'oracle (`last = FirstHurtTick − 1`, `:60`).
- Trois méthodes sont concernées :
  - `G7_RunningIntoABeannoid_OnTheRealWorld_IsTheOracle_TheBodyTouchesAtTick60` (`:104`) ;
  - `G7_TheTackle_OnTheRealWorld_HitsTheBeannoidAtTick61_ThreeSparks_ComparedUntilTheHurt` (`:117`, avec `Assert.Equal(64, last)` en `:123`) ;
  - `G7_TheSweepOfThePressTick_OnTheRealWorld_IsTheOracleUntilTheHurt` (`:133`, 17 traces).
- [MESURÉ] Dans les traces de l'oracle (`docs/plan-e20-annexe/e20-d/oracle/out/g7`) :
  - `R7-run-into-beannoid` : blessé au tick 61, trace de 90 ticks ;
  - `R7-tackle-s38` à `s62` : blessé aux ticks 69 (s38), 67, 66, 65, 64, 63, 62 (s50 à s56), 61 (s58 à s62), trace de 96 ticks ;
  - `s30` à `s36` : jamais blessé. Elles sont déjà comparées en entier.

### 3.3 Le harnais `HitArena` (`Alundra.Tests/Support/HitArena.cs`)

**T7.** [FAIT] Le constructeur (`:51-113`) place le héros et les Beannoïdes puis fait `Frames(6)` (`:85`) et vérifie `Hero.TargetAnimationId == 0` et la position (`:86-91`). Ensuite seulement `ResetHitState` (`:98-103`, `:117-123`), dont le commentaire dit « a hero carried into the body of a Beannoid was already hit during the settling frames ».
- Dès que le héros peut être blessé, tout arène dont le héros démarre dans le corps d'un Beannoïde échoue dans le constructeur : après 2 ticks il est en `0x31`, repoussé et à 9 PV.
- Arènes concernées (positions lues dans les sources) :
  - `HitPassTests.WhileTheContactLasts` : `new HitArena("contact", 232, 96, …252…)` (`:324`) ;
  - `StoredHitBoxTests.TheWorldsTick_StoresTheBoxOfAnEntityWithNoAttack_AfterThePhysics` (`:112`, héros 232, Beannoïde 252) : la ligne `CurrentAttack = null` vient trop tard, après le constructeur ;
  - les traces `H1-both` (`HitPassTests.Arena`, `:117`) ;
  - dans `TheReachInX_AtDyZero_IsTheOracleOne_FromMinus9To59Pixels`, les lignes `dx = −9, −8, 20` ;
  - peut-être des lignes `(20, dy)` de `TheReachInY_…`.
- [MESURÉ] D'après la table de `docs/plan-e20-annexe/e20-c1/oracle/out/reach-sweep.txt`, le corps du Beannoïde touche le héros pour `dx ∈ [−18, 20]` à `dy = 0`.
- `HitArena.Snapshot` (`:197-224`) et `Support/HitEntitySample.cs:15` ne portent ni direction, ni forces, ni sol, ni PV des stats, ni `Owner.IsVisible`. Les colonnes `dir_target`, `force_x/y/z`, `on_ground`, `stats_hp`, `visible` existent dans l'oracle c2 (`docs/plan-e20-annexe/e20-c2/oracle/out/A1.json`, colonnes de `runs[0].hero`).

**T8.** [FAIT] Les autres arènes ont leurs cibles à 40 px ou plus du héros, donc ne sont pas touchées par le constructeur :
- `HeroChargeArenaTests`, `FlailBounceArcTests`, `WaveRun`, `ArrowRun`, `BallRun`, `ChargedArrowRun`, `NativeSpriteTypeFilterTests` (cible à 300 px), `RandomPerTickTests.Advance.cs:195` (+100 px) ;
- `EnemyGroundTests` et `EnemyHitReactionTests` (héros à 212, cibles là où la carte les met).

La mutation `HitEffectWorldTests` place l'attaquant sur le héros après la construction : le héros y sera blessé, mais aucun test ne regarde ses PV.

### 3.4 Les montages de la nage : l'« accès de la blessure »

**T9.** `Alundra.Tests/Hero/HeroSwimTests.cs` simule la sortie du bloc de blessure sans l'entrée.
- `HurtAccess` (`:70-84`) pose `TargetAnimationId = 0x3A` et la direction au tick où l'oracle passe à `0x3A`. L'allocation `Hurt: true` (`:68`, `:101-105`) ignore la colonne `effects`.
- Méthodes : `W7_AHurtAtRest_3AFor4Ticks_3BFor9_FloatsAt15_TheDirectionTurnsAndComesBack` (`:588`), `W7_AKnockbackThatLeavesTheWater_CutsTheHurt_Idle_TheDirectionComesBack` (`:608`), `W7_AHurtDuringTheBurst_InterruptsIt_At81` (`:618`), `W7_TheOtherHurts_AreTheOracle_AtTheFirstContact` (théorie de 11 lignes, `:626-638`), avec `FirstHurtEnd` (`:641-646`).
- Le montage `RunMontage` n'a ni passe de coups ni Beannoïde. Ces tests ne bougent pas avec c2b, mais ils couvrent l'inverse de l'entrée.
- Deux traces de l'oracle ne sont référencées par aucun test [FAIT, `git grep`] :
  - `w7/W7-hurt-keep` ;
  - `w7/W7-hurt-shore`.
- [MESURÉ] `W7-hurt-shore` : héros à (335, 96), moitié dans l'eau, `vram_or = 0x0800` mais `slope = 0`. L'oracle donne `0x31` au tick 2 (et non `0x3A`), `0x1C` au 6, Idle au 17. C'est la seule trace qui isole le choix `Slope_18c == 4 ? 0x3A : 0x31` (`docs/plan-e20-annexe/e20-d/oracle/out-swim/w7/W7-hurt-shore.json`).

### 3.5 Arcs et suites où le héros peut être touché

**T10.** [INFÉRENCE] À remesurer, car le héros y côtoie un ennemi vivant, aucune assertion ne portant sur lui :
- `Alundra.Tests/Arcs/BeannoideGrateArcTests.G116_TheTwoBeannoids_DieByTheSword_AndTheGrateOpens_WithAFrameBreakEffectEach` ;
- `BeannoideLootArcTests.TheBeannoidOfTheMap14_DiesByTheSword_…` et `TheDroppedObject_LeftAlone_VanishesAtTheEndOfItsLife_WithoutGivingAnything` ;
- `MurutaHallArcTests.H115_TheFourMuruta_DieByTheSword_…`.

Leurs assertions portent sur les cibles (suite de PV, chronologie des morts, effets de bris). Ces arcs ont une politique « Swordsman » (héros à 16 à 40 px à l'ouest, un coup après chaque pause), et la IA native du Beannoïde se tourne vers le héros à moins de 3 à 4 tuiles (`Ai/Enemies/BeannoideAi.cs:44-52`).
- [MESURÉ] `FindDx` essaie `{40, 34, 28, 22, 16}` ; à 16 px le corps touche (zone [−18, 20]).

**T11.** [INFÉRENCE] Autres suites à surveiller, sur des cartes à ennemis réels avec `Prefabs: true` : les arcs des leviers (`LeverArcTests`, `LeverBridgeArcTests`, `LeverBasculeArcTests`, `LeverPillarArcTests`, `LeverDoorsArcTests`, cartes 115, 116, 117 et 44).
- `c2-binary.md` §0.8 et `check.md` #50 la signalent comme inconnue.
- `Events/HeightTargetSitesTests.TheWaitOfTheBinaryEndsAtTheCallOfTheBinaryAtEveryRealSite` : 12 des 33 lignes sont sur la carte 127 (`s127_rec6` à `s127_rec18`).
  - [FAIT, plan §1.5 résultat de c1a] « le seul coup de toute la suite est la petite boule de fer de la 127 sur le héros ». Je n'ai pas pu identifier le test. Si c'est celui-là, un héros repoussé de 33 px peut sortir de sa zone d'attente.

**T12.** [FAIT] Les arcs de la chaîne (389 à 392, 179, intro) n'ont aucun masque de classe qui atteigne le héros (plan §1.5, vérifié à c1a le 2026-10-10 ; sans changement de carte depuis, hors E14 qui n'y est pas actif).

**T13.** [INCONNU] Quels tests touchent réellement le héros au HEAD. Il faudrait un compteur de lecture sur `HitPassHookForTests`, ou un journal de `Hero.TouchingEntity` par test, puis la suite complète. Je n'ai pas le droit de la lancer.

### 3.6 Montages qui laissent le héros hors de la passe

**T14.** [FAIT] Il n'existe aucun dispositif explicite (`Hero.Flags`, `BlockedByEntity`, `FrameCollisionTickCounter`) qui retire le héros de la passe. `git grep` ne trouve que des usages sans rapport.
- Les montages nus le laissent hors de la passe de fait :
  - `JumpHeroRig` : drapeaux `Collidable | Gravity` seulement (`Support/JumpHeroRig.cs:125-128`), sans bit de classe ;
  - `TickStructureTests` : `:106` ;
  - `RunMontage`, `ContactHost` : aucune passe de coups.
- Ces rigs ne bougent pas : leur `TouchingEntity` reste nul, donc l'entrée rendrait −1.
- Appels directs de `PlayerManager.MovePlayer` : 14 fichiers de test, 32 appels avec `host: null`. L'entrée doit tolérer l'hôte nul ; sans `TouchingEntity` ni bits `0x180`, c'est un no-op.

### 3.7 Tests sur la visibilité

**T15.** [FAIT] Les tests sur `Entity.IsVisible` sont :
- `Alundra.Tests/Maps/WorldProxyEntityManipulationTests.cs:269-273` (`RunAnimationSyncPass_FlagToDestroy_HidesEntity_AndSkipsSync`) ;
- `Ai/Objects/ChestAiBlockWorldTests.cs:58` (« it is still drawn » : entités bloquées dessinées) ;
- `Ai/Objects/ChestAiWorldTests.cs:101` (`obj.IsVisible` suivi à chaque tick).

Aucun ne pose de `DamagedTickCounter` sur ces entités. `Entities/TickStructureTests.cs:313-323` pose `Hero.DamagedTickCounter = 3` sans regarder la visibilité.

### 3.8 Ancrages de réutilisation pour la validation

**T16.** [FAIT]
- `HeroLifeTests` (`Alundra.Tests/Hero/HeroLifeTests.cs`) rejoue l'oracle c2 : `Load`, `RunNamed` et `RunNames` sont `internal static`, `Column` privée (`:116`). `BuildMontage` (`:97-124`) construit un `ContactHost` avec tables, enregistrements, stats et héros qui a repris sa vie. Elle est privée.
- Le format de l'oracle c2 (`runs[…]`, colonnes `hero{…}`) diffère de celui du c1 lu par `HitOracleTrace` (`rows`, `pre`). Il n'existe aucun chargeur C# de l'oracle c2 pour un arène à passe de coups.
- Oracle c2 déjà complet (`docs/plan-e20-annexe/e20-c2/oracle/out/`) : A1, A2, A4, A6, B1, B2, D1, E2, E3, F1, F2, F4, F5, G, plus C1 (eau) et E1 (PV 0). Voir `summary.txt`.
- Cibles de la mesure déjà écrites : Orque de la 15 (`A2`), dégâts 4, 3, 2, 1, 1, 3 ; cases dangereuses coût 1, 2, 3 et bottes de niveau 3 jamais touchées.

## 4. Moteur (`CasaEngineMonogame`, pointeur `92d8e847`)

**M1.** [FAIT] Rien ne manque pour le clignotement.
- `Entity.IsVisible { get; set; }` : `CasaEngine/Framework/Scene/Entities/Entity.cs:130`.
- `Entity.Draw` retourne tout de suite si `!IsVisible` (`:544-549`).
- `World.Draw` ne dessine que les entités du frustum et appelle `entityBase.Draw(0f)` (`CasaEngine/Framework/Scene/World/World.cs:873-890`).
- `Entity.Update` ne regarde pas `IsVisible` (`:495-526`) : un héros caché continue d'être mis à jour.

**M2.** [FAIT] Aucun mécanisme de clignotement, de scintillement ou de « flash » dans le moteur (recherche `blink|flicker|flash` sans résultat). `AnimatedSpriteComponent.Color` existe (teinte), mais n'est pas la règle du binaire.

**M3.** [FAIT] L'ombre manque au moteur (ADR-0078 : « Until the engine slice lands, the shadow bits are written and nothing is drawn »). Le moteur n'a que `ShadowPass` / `ForwardShadowResources` pour les ombres de lumière.
- Quand l'ombre existera, elle devra suivre le même test `(+0x1CC & 3) == 3` que le sprite (`check.md` #28).

**M4.** [FAIT] Le recul et la gravité du héros passent par le contrôleur de caractère du moteur, déjà utilisé par E20.d1 pour les rebonds `0x3E` et `0x1C`.

## 5. Relectures au binaire (ALUN_CD.EXE France)

**B1.** [MESURÉ] `0x80031A68-0x80031B4C` (listé dans `entry-0x80031A68.dis`) :
- avec un toucheur : `ComputeNewHp(héros)`, puis `GetDirectionToTarget(PosX − tX, PosY − tY)` ;
- sans toucheur : `+0x1CC == 0`, bottes `< 3`, `n = (+0x180 & 0x180) >> 7 ≠ 0`, coût lu dans la table `{0, 1, 2, 3}` à `0x80028B8C`, PV = max(0, PV − coût), retour `(+0x8C + 0x10) & 0x1F`.
- Le retour −1 signifie « rien ».

**B2.** [MESURÉ] `0x80031C30-0x80031C98` (dans `hurt-block-0x80031BD4.dis`) : `Flags |= 0x100`, `Create(0)`, entrée. Si le résultat n'est pas −1 : `[0x80126FFC] = 0`, `[0x80127110] = 0`, `0x8002F884(2)`, `jal 0x8003634C`, `+0x88 = (s2 == 4) ? 0x3A : 0x31`, `+0x8C = résultat`, `+0x1CC = 0x78`, fin en `0x80032914`.

**B3.** [MESURÉ] Branche verrouillée : `0x80031C04-0x80031C2C`. Le mot `[0x800DC4B8]` est comparé en entier à `0x20` ; si l'entrée rend autre chose que −1, seul `+0x1CC = 0x78` est écrit.

**B4.** [MESURÉ] Chargement de carte : `0x8003B24C-0x8003B2D0` recopie, pour chacune des 64 cases de 0x294 octets, un gabarit de 0x290 octets pris en `0x80134368`, et écrit l'indice en tête. Le héros est ensuite créé par `0x80031974` (`InitializeEntity` puis les stats en `+0x18` et `+0x14`).
- [INFÉRENCE] Le gabarit est en BSS, donc à zéro : `+0x1CC` repart de 0 à chaque carte.

**B5.** [MESURÉ] Le HUD ne joue aucun son au décrément des PV (D22).

## 6. Ce qui manque et questions ouvertes

**Q1.** [INCONNU] Quels tests ou arcs sont réellement touchés (T13). Une veille sur `Hero.TouchingEntity` pendant la suite complète le dirait. Je ne peux pas la lancer ici.

**Q2.** [INFÉRENCE] Exposition du jeu. Après c2b, tout ennemi de carte à fiche d'attaque blessera le héros par la passe générique, y compris ceux dont l'IA n'est pas portée (D32). C'est fidèle au binaire. L'ordre avec E14.b reste la question Q-E14-b3 déjà posée.

**Q3.** [INFÉRENCE] Règle du clignotement et autres écrivains d'`IsVisible` (D19). Choix à faire : ne toucher que les entités à compteur non nul ou déjà cachées par elle, pour ne pas montrer les objets de coffre masqués en statut `Loaded`.

**Q4.** [INFÉRENCE] Publication par image (D19) : un tick caché peut être sauté dans une image de deux ticks. Écart documenté à ajouter.

**Q5.** [INFÉRENCE] Le `case 0x31` sur terre (D14), à ajouter pour que h0a retrouve `TryUseItem` pendant la blessure. À décider avec c2b ou à laisser à h0a.

**Q6.** [INFÉRENCE] L'appel d'annulation d'objet `0x800350C0` dans la branche verrouillée (D10). Le stub `CancelItemMode` existe ; à câbler avec le chemin `== 0x20` ou à laisser à h0.

**Q7.** [INFÉRENCE] Le constructeur de `HitArena` (T7) est le point à refaire en premier. Pistes : poser les cibles après le réglage du héros, ou désactiver leur attaque pendant les ticks de réglage.

**Q8.** [INFÉRENCE] `W7-hurt-shore` et `W7-hurt-keep` (T9) restent sans test. La première isole le choix `0x3A` / `0x31`.

**Q9.** [INFÉRENCE] `HeroSheet` nul (D25) : décider si l'entrée doit se comporter comme le binaire (dégât 1 sur fiche à zéro) dans les montages sans tables, ou laisser la vie inchangée comme `Damage.cs:26-29`.

**Q10.** [INCONNU] Si la jauge est ouverte au point de recette (D26).

**Q11.** [INFÉRENCE] Pas de chargeur C# de l'oracle c2 pour une arène de coups (T16). À écrire, ou à étendre à partir de `HeroLifeTests.Load` / `RunNamed`.

## 7. Chemins de mes scripts et sorties

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/c2b-disc/dll/`

- `bx.py` : copie de l'aide de désassemblage.
- `entry-0x80031A68.dis` : désassemblage de l'entrée des dégâts.
- `hurt-block-0x80031BD4.dis` : branche verrouillée, bloc de blessure et chemin normal de `MovePlayer`.
- `hud-hp-catchup-0x8004C1F0.dis` : bloc PV du HUD.
- `hit-traces-summary.py` et `hit-traces-summary.txt` : quelles traces de c1 touchent ou blessent le héros (`H1-both`, `H3` seulement).
- `head/` : copies des fichiers du HEAD lus (lecture seule, extraits par `git show HEAD:`), avec la même arborescence que `Alundra/`.