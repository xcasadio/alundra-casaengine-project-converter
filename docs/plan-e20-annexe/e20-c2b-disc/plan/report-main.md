# Rapport : E20.c2b (blessure du héros), angle « plans et dépendances »

Lecture seule. Je n'ai lu le DLL et les tests que par `git show HEAD:` et `git grep HEAD`, et les `docs/` directement. Aucune construction, aucun test, aucune écriture dans le dépôt.

- **HEAD** : `e9e8c58a`, branche `chantier/theme2-attaques-objets`.
- **Binaire** : j'ai re-décodé avec `bx.py` (désassemblage seul) l'entrée `0x80031A68`, le bloc de blessure de `MovePlayer`, l'ordre des passes du tick, la passe des compteurs et le test de clignotement.
- **Arbre de travail** : `Alundra/Ai/Objects/ChestAi.cs` y est modifié. Ce n'est pas moi et je ne l'ai pas lu.

Étiquettes : [FAIT] vu dans un fichier ou une adresse, [MESURÉ] mesuré dans l'oracle ou re-décodé par moi, [INFÉRENCE] déduit.

## 1. Résumé

1. [FAIT] **E20.c2b n'a aucune section détaillée dans le plan.** Elle n'existe que par une ligne d'enveloppe, `docs/plan-e20-heros.md:679`, nommée « E20.c2 », et par 56 lignes de renvoi. `docs/plan-e20-annexe/e20-theme2/report-main.md:343` la classe « ligne d'enveloppe seulement ». `…/e20-h3/state-f81304b/dll/state.md:165` note l'absence de section.
2. [FAIT] Le contenu précis de la tranche est la spécification de `docs/plan-e20-annexe/e20-c2/check.md` §c (lignes 177-203), écrite le 2026-10-09. Cinq de ses morceaux sont faits depuis par d'autres tranches (§3.3).
3. [FAIT] Les trois prérequis nommés sont faits et CONFIRMED par verifier, mais aucune recette de l'auteur n'est notée faite : E14.a, E20.i, E20.c2a (§4).
4. [FAIT] Au HEAD, rien du chemin de blessure n'existe dans le DLL (§8). Aucune ligne de code de c2b n'a été commencée : `git log --grep=c2b` ne rend que des commits de plan.
5. [FAIT] Quatre éléments attendent c2b : h3b, la défense visible des armures et anneaux, le clignotement des objets lâchés (Q-E0C-3), le rejeu des clés d'oracle de h3a (D-H-7). Les pins d'E14.a à remesurer s'y ajoutent (§5).
6. [INFÉRENCE] Placer c2b avant h3a et avant E14.b1 supprime le contournement D-H-7 et la question Q-E14-b3 (§10, Q-C2B-1).

## 2. Mentions de c2b et de ce qu'elle porte

**`docs/plan-e20-heros.md`**

| Ligne | Contenu |
|---|---|
| 679 | Périmètre d'origine : entrée `0x80031A68` (toucheur et cases dangereuses), `0x31`/`0x3A`, invulnérabilité `0x78`, clignotement, PV vers les stats et le HUD, `UpdateItemEffectState`, pilote `0x8003634C`, chemin `controlFlags == 0x20`, pas de mort. |
| 134-135 | D-E20-23 caduque (2026-10-10) : E20.i précède c2b. c2b n'a qu'à ajouter `+0x88 = (pente 4) ? 0x3A : 0x31`. |
| 209 | D-E20-52 : h3b n'est ouverte au joueur qu'après c2b. |
| 225 | Défauts confirmés : « Q-E0C-3 (avec E20.c2b) ». |
| 707, 3124-3125, 3154-3156 | Arête inversée : la branche de l'eau de c2b dépend d'E20.i. `0x3A`/`0x3B` ne sont atteints que par les tests jusqu'à c2b. |
| 848, 866, 871 | Ordre : c2b « après E14.a, qui remesure ses arcs », avant h3b, hors de l'ordre armes/objets/collisions. |
| 2495-2497, 2514, 2561 | L'oracle c2-0 sert d'acceptation à c2a et c2b. Les scénarios A3 et A5 « pourront revenir avec c2b ». |
| 2591, 2636 | Hors périmètre de c2a : la blessure, `0x80031A68`, `0x31`/`0x1C`/`0x3A`, l'invulnérabilité, le clignotement. |
| 2764, 2831, 2917, 2971 | E20.d1 : les colonnes de la blessure ne sont pas comparées sans c2b. `0x1C` est partagé avec `0x3E`, la première tranche qui arrive branche les deux. |
| 3259-3261 | E20.i : le point d'accès de la blessure (W7) pose `0x3A` sans l'entrée. Les traces `W7-hurt-keep` et `W7-hurt-shore` sont laissées à c2b. |
| 8600 (O-G-4) | Sites de blessure `0x80031C64` (mode 2), de case de sortie `0x80032778` et de mort `0x800327AC` : « E20.c2b, E18 ». |
| 8853, 8892, 8905 | e0c : « rendu du clignotement (avec E20.c2b) », hors périmètre d'e0c. |
| 10213, 10255 (D-H-7), 10276, 10354, 10431, 10514 | h3a/h3b : prérequis et clés d'oracle (§5). |
| 10787, 11110, 11172, 11243, 11274 | h4 : la blessure est hors tranche. |
| 11932 (Q-G1A-2), 11939 (Q-E0C-3), 11955 (Q-H3B-R), 11990 (Q-H4C2-2), 11999-12000 (Q-H3-1, Q-H3-2), 12008 (O-H-2), 12011 (O-H-6), 11887 (O-E20-6) | Questions et points ouverts liés à c2b (§5 et §6). |

**Autres plans**
- [FAIT] `docs/plan-e14-ia-native.md:34` (D-E14-4 : E14.a avant c2b, qui remesure ses arcs), `:891` (prérequis hors E14 de toute recette qui compte les PV), `:947` (Q-E14-b3, sans réponse), `:956`.
- [FAIT] `docs/plan-conversion-totale.md:761` (D-E14-4, écrit « E20.c2 ») et `:968-976`.
- [FAIT] `docs/plan-e19-opcodes.md` ne mentionne pas E20.c2b : le seul « c2b » est l'adresse `0x8002c2b8` (`:9871`). Il ne lui promet ni ne lui renvoie rien. O-E19-37 (`:13263`) sur `0x87`/`0x95` est périmé depuis c1b-1.
- [FAIT] ADR-0054 (D-E20-19 à D-E20-24). ADR-0056 : « The hurt itself (the damage entry `0x80031A68`) is E20.c2b ».

**Annexes citant c2b**
- `e20-armes/order-critic.md` lignes 49-51 et 150-152, 246. Ligne 150 : le clignotement est « owned by c2b (D-E20-21) ». Ligne 246 : Q-E « Confirm the c2 split », jamais répondue.
- `e20-i/{check,swim-binary,swim-dll,swim-oracle}.md`, `e20-d/check.md`, `e20-g1a/*`, `e20-h3/bin/report.md:225,379,434-441`, `e20-h4*/*`, `e20-theme2/report-main.md:301,331,343,354,365,412,425-427`.

## 3. Périmètre

### 3.1 Spécification la plus précise (`check.md` §c, lignes 177-203)
1. L'entrée `0x80031A68` en fonction pure, rejouée contre A2, A4, B1, B2, D1.
2. Le bloc de blessure du chemin normal :
   - remise à zéro du compteur de charge et du pointeur d'arme ;
   - `UpdatePlayerCarriedEntity(2)` puis le pilote en bouchon ;
   - `0x3A` si la pente d'avant `CheckAndExecuteWarp` vaut 4, sinon `0x31` ;
   - direction et `+0x1CC = 0x78`.
   Port du chemin verrouillé `controlFlags == 0x20`. Les branches `Hp == 0` et `+0x188 & 0x80` restent non portées, documentées au site.
3. Les états `0x31`, `0x1C` (avec E20.d) et `0x3A`/`0x3B`.
4. Le clignotement, pour les entités `IsActive`, sans toucher à `FlagToDestroy`.
5. Les pins de c1a à reprendre (§8).
6. Les arcs des leviers, le cas de la boule de fer de la 127 et les arcs d'histoire, avec une surveillance de `TouchingEntity` et `Hp` du héros.

Acceptation : le `HitArena` du DLL égale l'oracle A1 sur toutes les colonnes comparées, `stats_hp` et `visible` compris (visible pris au dernier tick de chaque image). Recette sur la carte 14 : les cœurs baissent, le héros clignote 30 ticks sur 120, il ne meurt pas.

Arrêts :
- force ou position de recul différentes de l'oracle (problème de physique, pas de c2) ;
- un arc de levier ou d'histoire qui change de trajectoire ;
- un changement de moteur nécessaire pour le clignotement ;
- une seconde boucle correction/reverify qui échoue.

### 3.2 Ce qui est depuis déjà fait ailleurs (à retirer de c2b)
- [FAIT] Le modèle de PV, l'épilogue sur toutes les sorties de `MovePlayer`, `UpdateItemEffectState`, `0xBC`, `0xC3` : c2a (`c7da0c9` à `259c76d`).
- [FAIT] Les gestionnaires `0x3A`/`0x3B` et la sortie vers la terre : E20.i (`Hero/PlayerManager.cs:144-145,573-577`).
- [FAIT] Le gestionnaire `0x1C`/`0x3E` : E20.d1 (`Hero/PlayerManager.cs:129-130,927`). Il ne reste à poser sur terre que l'état `0x31`, dont le gestionnaire `0x80032680` n'appelle que `TryUseItem`, qui rend 1.
- [FAIT] `UpdatePlayerCarriedEntity(2)` est réel depuis g1a (`Hero/CarryRules.cs:14`, `Hero/PlayerManager.cs:1411`). Il n'est plus un bouchon. Au site de blessure, l'objet porté est lâché et se brise.
- [FAIT] La fiche de défense du héros (`Items/EquipmentRecords.cs:72`) et le niveau des bottes (`Hero/PlayerManager.cs:1508`) : c2a.

### 3.3 Reste à faire
- L'entrée (toucheur et cases dangereuses), le bloc de blessure, l'état `0x31`, le chemin `== 0x20`.
- Le clignotement générique.
- Les remises à zéro du site de blessure.
- Le pilote `0x8003634C` reste un bouchon nommé (h0a non faite).

## 4. Prérequis et état

| Prérequis | État | Ligne |
|---|---|---|
| E20.c2-0 (oracle) | Fait, CONFIRMED : 105 parcours, 7979 ticks. Sorties dans `e20-c2/oracle/out/` (42 fichiers). | `plan-e20-heros.md:2485` |
| E20.c2a | Fait, CONFIRMED le 2026-10-10, « reste la recette de l'auteur ». | `:2584` |
| E20.i | Fait, CONFIRMED le 2026-10-10, recette `swim-m14` en attente. | `:3110` |
| E20.d1 | Fait, CONFIRMED, recette en attente. | `:2734` |
| E14.a0, a0b, a1a | Faits. | `plan-e14-ia-native.md:90,119,245` |
| E14.a1b, a1c | Faits, CONFIRMED le 2026-10-10 ; a1c : « recette de l'auteur à venir ». | `plan-e14-ia-native.md:357,659` |
| E20.g1a | Faite, CONFIRMED le 2026-10-24. | `plan-e20-heros.md:7154` |
| E20.e0c | Faite, CONFIRMED le 2026-10-23. Elle pose `+0x1CC = 0x78` au reste de vie 120 (`Ai/Objects/PickupAi.cs:45-46,180-182`). | `:8774` |
| E20.h0a (pilote réel) | READY, non faite. Non requis : le pilote reste un bouchon nommé (D-E20-19). | `:10266` |
| Restructuration R8 et garde `check_names.py --rules` | Faites le 2026-10-22 ; les fichiers neufs passent la garde. | `:848` |

[INFÉRENCE] D-E14-4 et D-E20-24 sont satisfaites : « après E14.a » est vrai.

## 5. Ce qui attend c2b

1. **h3b** (`:10427-10436`, D-E20-52). Elle attend c2b pour être ouverte au joueur. Elle est aussi en pause (Q-H3B-R, `:11955`).
2. **h3a** (D-H-7, `:10255`). Ses clés d'oracle où le héros est dans une explosion sont refaites avec le héros « insensible au type 4 » (`0x80` dans la fiche `0x80127008`). « E20.c2b rejouera ces clés sans ce réglage. » [INFÉRENCE] Si c2b passe avant h3a, ce contournement n'a plus lieu d'être. [MESURÉ] La bombe fait 5 PV sur 10 au héros à 17 px (`e20-h3/bin/report.md:225`).
3. **Défense des armures et anneaux** (`report-main.md:425-427`). Leur effet est « invisible en jeu » sans c2b. [MESURÉ] Dégâts d'un coup d'orque (puissance 4) par équipement : 4, 3, 2, 1, 1, 3 pour tissu, cuir, ancienne, argent, sans armure, bracelet (`plan-e20-heros.md:2563`).
4. **Q-E0C-3** (`:11939`, répondue « avec E20.c2b »). Le clignotement des 120 derniers ticks d'un objet lâché partage la règle du héros blessé. Aujourd'hui `+0x1CC = 0x78` est écrit sur l'objet (rendu non ciblable par `Combat/HitPass.cs:127`) mais rien ne le masque.
5. **Pins d'E14.a** (D-E14-4). Les arcs concernés sont `Alundra.Tests/Arcs/MurutaHallArcTests.cs`, `BeannoideGrateArcTests.cs` et `BeannoideLootArcTests.cs`. [FAIT] L'acceptation de l'arc du hall de la 115 pose le héros avec une « politique d'épée » à 40/34/28 px (`plan-e14-ia-native.md:742-745`) et n'affirme rien sur d'éventuels coups reçus.
6. **Q-G1A-2** (`:11932`, « oui » accepté) et **Q-H3-1** (`:11999`). Tant que c2b manque, le héros touché garde la caisse ou la bombe dans les mains. c2b met fin à cet écart via le mode 2.
7. **O-G-4** : c2b doit câbler le site `0x80031C64`.
8. **O-H-6** (`:12011`) : le héros touché au même tick qu'un soin. C'est une INFÉRENCE (le soin précède les dégâts), « à mesurer avec c2b ».
9. **O-H-2** (`:12008`) : la case de sortie « à placer (E19 ou E20.c2b) ».
10. **E14.b1 et Q-E14-b3** (`plan-e14-ia-native.md:947`, sans réponse). La question visait deux manques, la blessure et le butin. Le butin est fait (e0c), reste la blessure.
11. **E18** : le site de mort `0x80031CA8`/`0x800327A4` est le leur. O-E20-6 (`:11887`) reste ouvert, voir §9.

## 6. Décisions de l'auteur qui contraignent c2b

- **D-E20-5** (`:84`) : avant E18 le héros perd des PV mais ne meurt pas. La branche `Hp == 0` n'est pas portée.
- **D-E20-20** (`:128`) : à 0 PV, 0 cœur affiché, le héros garde la main, la régénération s'arrête.
- **D-E20-21** (`:130`) : le clignotement s'applique à toute entité, y compris celles que `0x86` rend invulnérables. [FAIT] 76 sites dans le corpus (`:513`). Elles clignoteront.
- **D-E20-19** (`:126`) : le pilote et le mode 2 restent des bouchons nommés à leur place exacte. Amendement de fait : le mode 2 est réel depuis g1a.
- **D-E20-22** : `0xC3` et `0xBC`, faits par c2a.
- **D-E20-23** : caduque, pas de « blessure brève » dans l'eau.
- **D-E20-24** et **D-E14-4** : l'ordre.
- **D-E20-52** : h3b après c2b.
- **Q-E0C-3** : clignotement des objets avec c2b.
- **Q-G1A-2 et Q-H3-1** : acceptés comme écarts jusqu'à c2b et E18.
- **Q-H4C2-2 (A)** : le rocher de la 321 reste inerte, sans blessure avant c2b.
- **D-E20-82** (2026-10-25) : toutes les réponses par défaut sont prises pour les questions alors posées. Aucune ne porte sur c2b.
- **Règles de travail de l'auteur** (mémoire du projet) :
  - le binaire France tranche ;
  - on ne corrige pas la décompilation ;
  - un défaut avéré de l'original se corrige (règle du 2026-09-25) ;
  - les questions se posent en termes de jeu.
- **Budgets du plan** (`:12029-12041`) :
  - deux tentatives par tranche au plus, cinq passes de correction au plus ;
  - arrêt si un arc de la chaîne bouge sans raison de la tranche ;
  - rien n'est fusionné sans l'auteur.

## 7. Ce que le joueur voit dans l'original quand Alundra est touché

Sources : `c2-binary.md` §2-4 et §8, `check.md`, `e20-c2/oracle/out/`, `e20-i/swim-oracle.md` point 10, `e20-0-rapports/D3-degats-au-contact.md`. T est le tick où `MovePlayer` entre dans l'état blessé. Je suppose 50 ticks par seconde, qui est une INFÉRENCE du plan (`e20-h3/bin/report.md`, Q-H3-1).

**Au contact d'un ennemi**
- [FAIT] À la fin du tick T-1, la passe de coups pose 3 étincelles au centre du recouvrement, `+0x1D0 = 25` sur le héros et `TouchingEntity`. C'est fait depuis c1a.
- [MESURÉ] Au tick T, `MovePlayer` fait, avant tout état :
  - PV moins les dégâts ;
  - passage à l'animation `0x31` pendant 4 ticks (son 23 au chargement de l'animation), puis `0x1C` pendant 11 ticks ;
  - recul le long de la direction qui s'éloigne du toucheur, d'environ 33 px (−32,625 px d'après `c2-binary.md` §2.3) ;
  - retour à Idle à T+15, la direction retournée de 180° : il regarde de nouveau vers l'ennemi.
- [MESURÉ] Un coup d'épée en cours, une charge, un saut ou une échelle sont remplacés. La charge et l'arme en attente sont remises à zéro (bloc `0x80031C58-0x80031C98`, re-décodé). Un objet porté tombe et se brise (mode 2).
- [MESURÉ] Pendant ces 15 ticks, le pad ne choisit pas la direction. Rond atteint encore `TryUseItem`.
- [MESURÉ] En l'air : la chute continue, l'état `1` (marche) puis `0x2D` en fin de recul. `c2-binary.md` §3.2 appelle cela une bizarrerie confirmée.

**Invulnérabilité et clignotement**
- [MESURÉ] `+0x1CC` vaut `0x78`, puis la passe des compteurs le décrémente. Le coup suivant est possible à T+120 (toucher au tick T+119, blessure en T+120).
- [MESURÉ] Le héros est caché au tick T lui-même, car `0x77 & 3 == 3`. Puis il est caché à T+4j pour j de 0 à 29, soit 30 ticks sur 120 (un tick sur quatre).
- [MESURÉ] Re-décodé par moi : le test `andi 3` en `0x800385F0`, avec `$t5 = 3` en `0x80038540`, et l'ordre des passes d'`0x8003B388` (slots, événements dont `MovePlayer`, compteurs, listes, physique, effets, passe de coups).
- [INFÉRENCE] Environ 2,4 s d'invulnérabilité et 0,3 s d'animation de blessure.

**Dégâts**
- [MESURÉ] `max(1, puissance × réponse >> 4)`, minimum 1 PV. Tissu : Beannoïde 1, orque à la masse 4. Cuir : orque 3. Armure ancienne : orque 2. Armure d'argent : orque 1. Sans armure : toujours 1.
- [FAIT] Le charme (objet `0x36`) porte `0x80` à la 6e réponse (`balance.json`). Selon `check.md` point 15, une réponse `0x80` ou `0xC0` ne coûte aucun PV mais donne quand même l'animation, `+0x1CC` et le recul. [INFÉRENCE] Contre le feu, Alundra recule donc sans perdre de cœur.
- [MESURÉ] Bombe : 10 PV vers 5, recul, `0x31`.
- [FAIT] Un hit qui arrive pendant un état verrouillé (dialogue, scène) est perdu : étincelles seulement, aucun PV. Seul le mot exact `0x20` (prise du héros par un ennemi natif, E14) entre dans l'entrée, sans animation ni son, avec `+0x1CC = 0x78`.

**Cases dangereuses** (`c2-binary.md` §2.1, §8)
- [MESURÉ] Coût 1, 2 ou 3 PV selon la case, sans armure, toutes les 120 ticks (blessures aux ticks 2, 122, 242), sauf bottes de niveau 3 (objet `0x1C`).
- [MESURÉ] Table `0x80028B8C` relue par moi : 0, 1, 2, 3. La décompilation donne 0, 256, 512, 768, ce qui tuerait d'un coup : le binaire gagne.
- [MESURÉ] Le recul se fait vers l'arrière de la direction regardée.
- [FAIT] 36 cartes en ont, aucune de la chaîne actuelle (14, 15, 115-117, 389-392).

**Eau profonde** (`swim-oracle.md` point 10)
- [MESURÉ] `0x3A` pendant 4 ticks, puis `0x3B` pendant 9, et il flotte à T+13. Un recul qui sort de l'eau coupe la blessure.

**À 0 PV**
- [FAIT] Alundra reste jouable avec 0 cœur. La régénération s'arrête. Seuls les soins remontent la vie.

## 8. État du DLL au HEAD et tests qui bougent

**DLL** (par `git grep HEAD`)
- [FAIT] Aucun appel à `Damage.ComputeNewHp` avec le héros pour cible. Le seul appelant est `Ai/EnemyHelpers.cs:232`.
- [FAIT] Le site d'insertion est `Hero/PlayerManager.cs:355` (`PlayerAnimationEffects.Create(player, FreeMode)`) à `:358` (`UpdateItemUseDriver`). Les commentaires du fichier le disent.
- [FAIT] Le test du `0x20` aurait sa place dans la branche verrouillée, `:337` et suivantes, avant l'appel à `EndOfMovePlayer`.
- [FAIT] `Maps/WorldProxy.cs:3050-3060` décrémente `+0x1CC` et `+0x1D0` et efface `TouchingEntity`.
- [FAIT] `Maps/Loop/FrameSyncPasses.cs:109` n'écrit `IsVisible` que pour les entités détruites. `Maps/WorldProxy.cs:3157` (`PublishEntityFrame`) publie une fois, après le dernier tick de l'image.
- [FAIT] Le moteur n'a pas besoin de changer : `Entity.IsVisible` existe (`c2-dll.md` §1.6).
- [FAIT] `CombinedVramFlagsOR` du héros est déjà alimenté (`Entities/EntityScriptProxy.cs:2307`), donc la branche des cases dangereuses est atteignable.

**Pins qui bougent** (`HEAD:Alundra.Tests/…`)
- `Combat/HitPassTests.cs` :
  - `:276-296` H3 (`Hp == 10`, `DamagedTicks == 0`, clause `ignore`, `lastTick: 18`) ;
  - `:299-318` H1both ;
  - `:320-336` `WhileTheContactLasts` (touchers aux ticks 1, 26, 51, 76 ; `DamagedTicks == 0`) ;
  - `:456-461` `TheReachBehindTheHero…` (`Assert.Equal(12, …)` ; dans l'oracle le contact à dx −10 n'existe pas).
- `Hero/HeroRunArenaTests.cs:123` : `Assert.Equal(64, last)` avec le commentaire « hurt at 65 (E20.c2b) ».
- `Hero/HeroSwimTests.cs:70-105, 640` : point d'accès `HurtAccess`, colonne `effects` ignorée, seconde blessure.
- `Hero/HeroRunTests.cs:22`.
- [FAIT] La liste des fichiers de tests qui exposent des ennemis natifs compte 47 fichiers (`git grep -l "HitArena|MurutaAi|BeannoideAi|NativeSpriteRegistry…"`).
- [INFÉRENCE] `c1a` disait qu'un seul coup sur le héros existait dans toute la suite (la boule de fer de la 127). Cela date d'avant E14.a et E20.f/g : c2b doit recompter par une surveillance de `TouchingEntity` et de `Hp`.

**Sauvegardes de test**
- [FAIT] Les préréglages d'armes (`docs/test-saves.md:65`, `test-means.md` §7) sont à 4/10 PV sur la carte 14, case (8,6). Après c2b, ils deviennent dangereux (jusqu'à 0 cœur près des Beannoïdes, sans mort).
- [FAIT] `git grep -i "armor|armour|leather" tools/AlundraTestSaves` ne trouve rien : aucun préréglage d'armure.
- [FAIT] Pour voir les cœurs, la touche F1 bascule la jauge (D-E13-12) ; sans elle, `c2-dll.md` §1.4 constate que les cœurs ne se dessinent que jauge ouverte.

## 9. Ce qui manque

1. Une section de plan pour c2b, avec tâches, acceptation, arrêts.
2. Couverture de l'oracle, selon `e20-c2/oracle/notes.md:§3` :
   - retirés par le contrôle et à reprendre : A3 (réponse par type d'attaque, dont le charme), A5 (blessure en plein coup, en marche, à l'apogée, à l'atterrissage) et A7 (deux Beannoïdes) ;
   - mesure en l'air faite dans le scratch de la découverte (`c2-binary.md` §8) et non versée dans l'oracle du dépôt ;
   - cases dangereuses : l'oracle les peint sur un champ synthétique, pas sur une vraie carte (36, y 43 à 56) ;
   - [INFÉRENCE] ni l'objet porté, ni la charge, ni le fléau ne sont combinés avec la blessure ;
   - `run_c2.py` dure environ 300 s : à ne pas relancer pendant la passe de mutants d'un autre agent.
3. U5 (`c2-binary.md:388`) : on ne sait pas quels sprites posent le bit `0x20` (`0x80062740`, `0x80075264`). Le chemin `0x20` est donc inatteignable depuis le DLL : un test qui pose le bit, six lignes.
4. [INFÉRENCE] **Clignotement et fréquence d'image.** Le DLL publie la visibilité une fois par image, après son dernier tick. À 60 images/s pour 50 ticks/s, chaque tick est publié. À 30 images/s (2 ticks par image), le dernier tick de chaque image garde toujours la même parité, et le motif « caché à T+4j » peut ne jamais être publié ou l'être une image sur deux. À mesurer.
5. Aucun point de mesure n'existe pour O-H-6 (soin et blessure au même tick).
6. SC8/O-E20-6 : avec c2b, une sauvegarde à 0 PV devient atteignable (`AlundraSaveGame` accepte `hp` de 0 à `hpMax`, d'après `c2-binary.md` §2.5). Son rechargement à 0 reste à trancher par E18.
7. U4 (`c2-binary.md`) : un objet du Rond démarré pendant la blessure est-il refusé ? C'est du ressort de h0a.
8. [INFÉRENCE] Annulation d'un sort par la blessure : le chemin de blessure appelle le pilote une dernière fois avec l'ancienne animation (`e20-h4/report.md:83`). Il n'appelle pas l'annulation `0x800350C0`. L'annulation en vol testée par h4d1 pose `TargetAnimationId = 0x31` à la main. Le pilote réel est à h0a, mais le site est à c2b.
9. Q-E14-b3 reste sans réponse. Q-E (« confirmer le découpage c2-0, c2a, c2b », `order-critic.md:246`) n'a jamais été posée comme question à part. Le découpage a été pris par la session.

## 10. Questions à poser à l'auteur

Chaque question a un défaut proposé. Les défauts « comme l'original » sont indiqués.

**Q-C2B-1. Où placer la blessure du héros dans l'ordre des tranches ?**
- Aujourd'hui, au contact d'un Beannoïde, d'un orque ou d'une explosion, Alundra voit des étincelles mais ne perd aucun cœur et ne recule pas.
- (A, défaut proposé) tout de suite : avant les objets du Rond (h0a, h1, h3a) et avant les ennemis suivants (E14.b1).
  - Gain : la bombe et les ennemis se testent avec un héros qui réagit, sans le réglage « héros insensible » de D-H-7, et la question Q-E14-b3 tombe.
  - Coût : les sauvegardes d'armes (4 cœurs sur la carte 14) deviennent dangereuses, jusqu'à 0 cœur sans mort.
- (B) à sa place actuelle du plan : avant h3b seulement.
  - Coût : h3a (la bombe sans les mains) se livre avec le héros insensible, puis ses clés sont refaites.

**Q-C2B-2. Les cases dangereuses dans c2b ?**
- Dans l'original, certaines cases coûtent 1, 2 ou 3 cœurs toutes les 2,4 s tant qu'Alundra reste dessus, sans que l'armure y change rien. Les bottes de niveau 3 en protègent.
- 36 cartes en ont (surtout 52 à 60 et 443 à 448). Aucune de l'histoire jouée aujourd'hui.
- (A, défaut « comme l'original ») les porter dans c2b : même fonction, un test sur une vraie carte en plus.
- (B) les reporter avec les bottes et le mur d'eau (E20.i2) : d'ici là, ces cases sont inoffensives.

**Q-C2B-3. Une sauvegarde de test pour voir la défense ?**
- Avec les quatre armures (un coup d'orque coûte 4, 3, 2, 1 cœurs), le bracelet, le charme (le feu fait reculer sans coûter de cœur) et les bottes de niveau 3, à 10/10 PV sur la carte 15 (orques) ou 14.
- (A, défaut) en ajouter une, que tu génères hors de l'app (`docs/test-saves.md`).
- (B) s'en passer : la défense n'est alors testée que par les oracles.

**Q-C2B-4. Les recettes en attente (hall de la 115, grille de la 116, cœurs d'une carte à l'autre, nage de la 14).**
- Elles sont notées « à venir » ou « en attente ». Le hall de la 115 change de visage avec c2b : Alundra y perdra des cœurs face aux Muruta.
- (A, défaut) les faire après c2b, d'un coup.
- (B) les faire avant, puis refaire celles qui bougent.

**Q-C2B-5. Les bizarreries de l'original à garder.**
- (a) Pendant un dialogue ou une scène, un ennemi qui touche Alundra ne lui fait rien (étincelles seulement).
- (b) Touché en l'air, une image de pose de marche avant la chute (invisible à l'œil).
- (c) À 0 cœur le jeu continue (déjà tranché, D-E20-20).
- (d) Un objet porté tombe et se brise.
- Défaut « comme l'original » : tout garder.
- Rappel pour (c) : l'auteur verra 0 cœur pour la première fois ; le rechargement d'une sauvegarde à 0 PV relève d'E18 (SC8).

**Q-C2B-6. La case de sortie (O-H-2, escaliers et sorties dans le sol) dans c2b ?**
- L'original y arrête et détruit l'objet porté, puis enclenche la sortie. Le DLL n'écrit jamais l'attribut de tuile du héros.
- (A, défaut) hors c2b, à placer avec E19 ou la restructuration ; (B) dans c2b.

À ne pas redemander (déjà tranchés) :
- le clignotement des objets lâchés et des entités à `0x86` (D-E20-21, Q-E0C-3) ;
- 0 cœur sans mort (D-E20-5, D-E20-20) ;
- l'eau profonde : `0x3A`, sans « blessure brève » (D-E20-23 caduque) ;
- h3b après c2b (D-E20-52).

## 11. Forme probable de la tranche

[INFÉRENCE] Ce n'est pas une décision, c'est une proposition à relire.

- **T0 (lecture et mesure)**
  - Compter par surveillance les coups reçus par le héros dans toute la suite.
  - Reprendre A3, A5, A7, la blessure en l'air et avec objet porté dans l'oracle (fichiers neufs seulement).
  - Mesurer le motif de clignotement publié par image.
  - Écrire les clés qui remplacent D-H-7 si h3a passe après.
- **T1 (tests d'abord, rouges)** : l'entrée en fonction pure, les états `0x31`, le chemin `0x20`, les sites de remise à zéro, `UpdatePlayerCarriedEntity(2)` et le pilote en bouchon.
- **T2 (code)** : insertion entre `:355` et `:358`, capture de la pente avant `CheckAndExecuteWarp` pour le choix `0x3A`/`0x31`, pins re-dérivés de l'oracle avec justification du binaire.
- **T3 (clignotement)**, générique, `IsActive` et jamais `FlagToDestroy`, objets lâchés compris.
- **T4 (cases dangereuses)**, si Q-C2B-2 répond (A).
- **T5 (plan, ADR, recette)**.

Arrêts à reprendre de `check.md` §c : recul différent de l'oracle, arc d'histoire qui bouge, moteur nécessaire pour le clignotement.

## 12. Fichiers produits

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/c2b-disc/plan/`
- `…/plan/bx.py` : copie de l'aide de désassemblage.
- `…/plan/entry_80031A68.dis.txt` : l'entrée `0x80031A68`.
- `…/plan/movePlayer_hurt_block.dis.txt` : `0x80031B98-0x80031CC8`, bloc de blessure de `MovePlayer`.
- `…/plan/blink_lists_pass.dis.txt` : test de clignotement, `0x800385C8-0x80038614`.
- `…/plan/counters_pass.dis.txt` : passe des compteurs.
- `…/plan/tick_dispatcher.dis.txt` : ordre des passes d'`0x8003B388`.

Aucun fichier du dépôt n'a été écrit.