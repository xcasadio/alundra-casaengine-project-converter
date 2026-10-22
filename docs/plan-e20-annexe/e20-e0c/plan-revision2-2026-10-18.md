# Texte de la révision 2 de E20.e0c (2026-10-18), remplacé le 2026-10-22 par la révision 3 au plan

Archive du texte tel qu'il était dans `docs/plan-e20-heros.md` avant la révision 3 (réponses de l'auteur du 2026-10-22).

### 1.26 E20.e0c — Le butin et l'objet ramassable ⏳ (détaillée le 2026-10-18, révision 2 **READY** ; exécutable après la fin du chantier de restructuration ; **à réviser** après les réponses du 2026-10-22 : D-E20-47, D-E20-59 (la file d'effets))

**Relecture (2026-10-18).** REVISE, deux blocages corrigés (FIX, révision 1) : (1) le tick de naissance d'un objet lâché n'avait pas de
défaut alors que V6 mesure déjà l'écart (`BT-L1`, `BT-L3`) : défaut retenu, Q-E0C-8, P-L1 le confirme ; (2) les états 2 et 3 du ramassage
(le CD, le masque du dialogue, `0x80049AF4`, l'attente du dialogue) étaient réduits à « attendent » : défaut retenu, T0 (f) les lit, (w8) les
épingle ; Q-E0C-5 concerne aussi les objets lâchés. Seconde relecture : REVISE, un blocage (l'acceptation ne citait pas (w8) et (w9)) :
FIX en révision 2 ; nouvelle époque, une seule relecture de clôture : **READY** (2026-10-18). Les défauts réversibles restent à confirmer
par l'auteur (Q-E0C-1 à Q-E0C-8).

**Provenance.** Lecture du binaire du 2026-10-17 et sa critique (`docs/plan-e20-annexe/e20-e0c/`, `report.md` et `check.md`, au HEAD
`2010ec3`) ; état de la DLL et des tests rejoué au HEAD `978608d` après la restructuration (`docs/plan-e20-annexe/e20-e0c/state-978608d/`,
`state.md` et sa contre-vérification `check.md` : 45 citations traduites, toutes au même code ; 27 affirmations sur la DLL, toutes encore
vraies, quatre preuves corrigées). Le binaire tranche ; la décompilation se lit, ne se corrige pas.

**But.** Comme l'original : un ennemi tué ou un conteneur détruit lâche l'objet tiré à sa création (`+0x3C`, déjà tiré par E20.e0b2) ;
l'objet monte de 10 px par tick, retombe, rebondit selon la gravité de la carte, se ramasse au contact du héros (argent, cœurs, magie,
herbe) avec son son, ou s'éteint au bout de 600 ticks ; la coupe d'herbe (E20.e) trouve `SpawnRandomLoot` prêt. Les objets posés sur les
cartes et le coffre restent inertes (comme aujourd'hui).

**Faits (binaire, FAIT sauf mention).**
- `SpawnContents(e)` `0x80032B90` : `item = e.+0x3C` ; si `item == 0`, rien ; veto MP (`0x80032A00` : objets `0x26`, `0x51`, `0x52`
  annulés si `MpMax == 0`) ; `SpawnWarpEntity(parent 0, page globale, type item + 0x1E, e.+0x114, +0x118, +0x11C, direction 0)` ; table
  pleine → rien, sans son ; puis sept écritures : `+0xB8 = 0xA0000` (`ForceZ`), `+0x274 = 1` (« lâché »), `+0x6C &= ~0x80` (plus
  collisionnable), `+0x278 = 600` si l'octet 4 de la table des objets `0x800C5F7C` est non nul sinon `-1`, `+0x27C = 0`, `+0x280 = e.+0x40`
  (le drapeau du conteneur), `+0x284 = 0xA0000` ; son `0x54` (84). `SpawnRandomLoot(code, X, Y, Z)` `0x80032C7C` : le même corps, l'objet
  tiré par `Choose(code)` (le générateur avance même si l'on abandonne ensuite), la position donnée, `+0x280 = 0` ; un seul appelant, la
  coupe d'herbe.
- `DestroyEntity(e, a1)` `0x8003A59C` appelle `SpawnContents(e)` en **première** instruction utile ; la destruction légère `0x8003A774`
  (32 appelants, dont C255) ne lâche rien ; la récursion `0x8003A648` (E11, 7 types de boss) détruit d'abord les enfants.
- C255 `0x8007C174` (381 mots ; tick de 87 types globaux, 31 à 118 sauf 72) et A255 `0x80061BD4` (chargement de 88 types, les mêmes plus le
  coffre 30). Branche « lâché » (`+0x274 == 1`) : A255 rend la main tout de suite ; C255 : si au sol et `+0x284 != 0`, `v = (+0x284 * 12)
  >> 4`, remis à 0 si `v <= u16[en-tête de carte + 4] << 8` (la gravité), `+0xB8 = +0x284` ; décompte de `+0x278` (s'il n'est ni `-1` ni 0),
  destruction légère à 0, `+0x1CC = 0x78` à 120 (clignotement) ; `+0x27C` : 0 attend la fin de la montée (`ForceZ <= 0`), 1 teste le
  recouvrement des boîtes avec le héros (positions `+0x1D8..` de l'image précédente, tailles `+0x1F0..` : `d = objet − héros`, `d >= 0 ?
  d < taille du héros + 1 : −d < taille de l'objet + 1` sur les trois axes ; rien si un dialogue est ouvert), puis `GiveItem` (`0x80033F00`),
  `SetGameFlag(+0x280)`, le son de l'octet 5 de la table, `+0x27C += 2` (les objets du butin ont un son : aucun n'est un objet-clé) ; pour un
  objet lâché, `GiveItem` rend 0 et le tampon de texte est vidé : **aucun message** ; état 2 : rien si `CdBusy()` (`0x8005A7D4`, lit
  `[0x800C480C]`), sinon `+0x27C = 3`, `[0x80149CBC] = 3` (`g_etcAnimationMode`, le masque de fermeture du dialogue) et `0x80049AF4()` ;
  état 3 : rien tant qu'un dialogue est ouvert, puis `Unblock(soi)` et destruction légère (`report.md` §1.6, `:117-118`, `:125-127`). Séquence de rebond à la gravité 128 (481 cartes) : `0x78000 … 0x9029`, puis 0 ; 23 rebonds à la gravité 3 (cartes 159
  et 160).
- `GiveItem` pour les objets atteignables par le butin : `0x45`..`0x48` argent 1, 5, 10, 30 (plafond 9999) ; `0x54` PV + 2 ; `0x55` PV +
  `max(1, PVmax/4)` et un effet global 14 animation 0 ; `0x56` PV + `max(5, PVmax/2)` et 4 effets ; `0x51` (et `0x26`) PM + 1 et 4 effets
  global 14 animation 2 ; `0x24` (herbe) : à l'inventaire d'abord, utilisée si l'inventaire est plein. Chaque effet **créé** consomme 2 pas
  du générateur (8 pas pour 4 étincelles si le réservoir n'est pas plein) ; la fonction rend `octet 4 == 0`.
- Recensement (critique, corrigé) : 4 200 entités posées tirent dans la table à leur création (3 355 avec apparition automatique) ;
  objets lâchables atteignables : `0x24`, `0x45`..`0x48`, `0x51`, `0x54`..`0x56` (sprites globaux 66, 99 à 102, 111, 114 à 116) ; 152 objets
  posés sur 53 types (hors tranche) ; le code `0x82` des cruches, caisses et tonneaux ne donne que des cœurs.

**Faits (DLL au HEAD `978608d`).** Existent : le tirage à la création et sa table (`Alundra/Items/LootRoll.cs:54`, `:111`, md5 identique au
binaire ; `Alundra/Maps/WorldProxy.cs:3469`, appelé aux trois sites de création `:1119`, `:3440`, `:3566`) ; les champs de l'objet
(`Alundra/Entities/EntityScriptProxy.cs` : `ContentsItemId`, `ContentsGameFlag` `:106-107`, `ForceZ` `:338`, `IsOnGround` `:417`,
`DamagedTickCounter` `:483`, `ModdedPos*` `:486`, `AIValues` `:1230`, `short[10]`, sans accès 32 bits) ; l'apparition par indice
(`Alundra/Maps/IEntityWorldContext.cs:81`, `WorldProxy.cs:3488`, `:3601`, ajoute 1 au Z, `:3549`) ; `AddMoney`, `SetPlayerMp`,
`IncreaseHpMaxAndFill` (`Alundra/Hero/PlayerManager.cs:2285`, `:1970`, `:1875`) ; le modèle d'étincelles
(`Alundra/Rendering/Effects/NativeEffects.cs:31`) ; le registre des natifs par (slot, code) avec liste de types
(`Alundra/Ai/NativeSpriteRegistry.cs:54`, sept clés de production `:36-43`) ; le test d'axe de C255, déjà écrit pour la passe de coups
(`Alundra/Combat/HitPass.cs:63`, `AxisOverlaps`). Manquent : `SpawnContents`, `SpawnRandomLoot`, le veto, `GiveItem`, C255, A255 ; le
bouchon nommé « (1) loot » de `WorldProxy.DestroyEntity` (`:3303`) ; les colonnes 0 (règle des 600 ticks) et 1 (son) de
`item-drop-properties.json` (`Alundra/Items/ItemTables.cs:57-58`, `:81-90` : lues puis jetées) ; `ModdedPos` n'est écrit qu'à la création
(`Alundra/Entities/Spawning/EntitySpawnFactory.cs:825-827`) et **aucun code ne le lit** ; aucun rendu ne cache une entité selon
`(DamagedTickCounter & 3) == 3` ; `CombinedVramFlagsOR` n'est nourri que pour le héros (`EntityScriptProxy.cs:1642-1646`), alors que les 9
sprites lâchables portent `DestroyOnVramFlags` (`0x200000`). Depuis R0, une création dont le nom de script ne se résout pas relance
`ScriptTypeResolutionException` (`WorldProxy.cs:1122`, `:3444`, `:3580`).

**Tests qui épinglent les manques** (noms et chemins au HEAD ; R5 les déplacera selon sa table) : le bouchon de `DestroyEntity`
(`Alundra.Tests/AlundraDestroyEntityBreakEffectTests.cs:204-219`, aucun tirage, aucune entité) ; les oracles d'ennemis, qui **excluent**
les 160 entrées `spawn_39F58` et `sound 84` de leurs journaux (`AlundraBeannoideOracleTests.cs:370`, `:404-415` ; `AlundraMurutaOracleTests.cs:450`,
`:483-494` ; l'oracle porte déjà le lâcher et le veto : 80 vecteurs sur 144 lâchent, types 99, 100, 101, 114, 115) ; le compte des clés du
registre de production (`AlundraNativeSpriteRegistryTests.cs:55` et `AlundraWaveSwordLifeTests.cs:181-183`, 7) ; `AlundraItemTablesTests`
(colonne 2 seule) ; les arcs 115 et 116 tuent des Muruta et des Beannoïdes sans rien asserter des objets nés (`AlundraMurutaHallArcTests`,
`AlundraBeannoideGrateArcTests`). Un monde de test nu (`new WorldProxy()` sans `InitializeWithWorld`) refuse toute apparition par indice
(`WorldProxy.cs:3498-3500`) : les tests de lâcher demandent un monde initialisé avec le catalogue.

**Défauts réversibles retenus en mode autonome (à confirmer par l'auteur, §2).** Q-E0C-1 : les objets posés et le coffre **hors** tranche
(ils demandent le dialogue, la fanfare CD et le blocage : une tranche e0c-b) ; C255 et A255 s'inscrivent pour leurs 87 et 88 types, mais
leurs branches « posé » (`+0x274 == 0`) et « coffre » (`== 2`) sont des bouchons nommés qui ne font rien (l'objet posé reste inerte comme
aujourd'hui). Q-E0C-2 : les étincelles naissent **tout de suite** (`EffectStage.CreateNow`, comme le binaire : l'ordre exact des pas du
générateur), ce qui étend l'ADR-0057 aux objets (un ADR). Q-E0C-3 : le clignotement des 120 derniers ticks **avec E20.c2b** (une règle de
rendu générique, partagée avec le héros blessé ; d'ici là l'objet s'éteint à 600 ticks sans avertissement ; `+0x1CC = 0x78` est quand même
écrit, ce qui rend l'objet non ciblable par la passe de coups, `HitPass.cs:127`, comme le binaire). Q-E0C-4 : `ModdedPos` **rafraîchi en fin
d'image** pour les entités actives (fidèle, une image de décalage). Q-E0C-5 : l'écriture `SetGameFlag(0xFFFFFFFF)` est propre aux objets posés (hors
tranche) ; le registre `[0x80149CBC]` est écrit à 3 par l'état 2 d'un objet **lâché** ramassé : c'est le masque de fermeture que la DLL tient
dans `DialogueDirector` (`Alundra/Dialogues/DialogueDirector.cs:48`, `DefaultCloseMask = 3`, « the original's default g_etcAnimationMode ») :
défaut **porté** (le masque remis à 3 par le membre de la DLL que T0 (f) nomme ; sans effet visible quand il vaut déjà 3). États 2 et 3 :
`CdBusy()` pris comme jamais occupé tant que T0 (f) ne le relie pas à un état que la DLL tient ; `0x80049AF4()` lu en T0 (f) : porté s'il a
un équivalent dans la DLL, bouchon nommé s'il est sans effet pour un objet lâché (aucun texte), arrêt sinon ; l'attente de l'état 3 lit le
dialogue ouvert comme le livre de sauvegarde (`dialogue.IsOpen || IsAwaitingChoice`, `Alundra/Ai/Objects/SaveBook.cs:248`) ; `Unblock(soi)`
sur un objet jamais bloqué, sans effet (T0 (f) le vérifie). Q-E0C-8 (nouvelle, §2) : un objet lâché dans la passe des programmes participe,
dans le binaire, au tick même si sa case est plus haute que celle de l'entité détruite (V6, lignes `BT-L1` et `BT-L3`, §1.16.3) et attend le
tick suivant si elle est plus basse (`BT-L2`) ; la DLL le fait toujours attendre (`Alundra/Maps/WorldProxy.cs:2930-2936`) : défaut, **l'écart
d'un tick gardé** pour la case plus haute (invisible : l'objet commence à monter 1/50 s plus tard), documenté et épinglé par un test nommé
d'après l'écart, comme D-E20-34 et D-E0A4-5 ; la case plus basse est égale au binaire. Q-E0C-6 : la récursion E11
**avec E14.b** (les boss). Q-E0C-7 : les PV des cœurs écrits sur le héros quand il porte sa vie (`LifePulledFromStats`), sur les statistiques
sinon, comme `IncreaseHpMaxAndFill`. Un objet lâché dans l'eau ou dans un trou n'est pas détruit tant que `CombinedVramFlagsOR` n'est nourri
que pour le héros (écart noté, O-E0K-2).

**Prérequis.** E20.e0b2 ✅, E20.e0a1 à e0a5 ✅, E20.e0k1 à e0k3 ✅ ; la fin du chantier de restructuration (règle du programme) et la garde
des fichiers neufs (Q-R-7 du plan de restructuration : sans elle, `check_names.py --tree` refuse tout fichier ajouté).

**Périmètre.** Nouveaux : `Alundra/Items/LootDrop.cs` (`SpawnContents`, `SpawnRandomLoot`, le veto), `Alundra/Items/ItemEffects.cs`
(`GiveItem` pour les objets atteignables, les étincelles), `Alundra/Ai/Objects/PickupAi.cs` (C255 et A255, branche « lâché ») ; modifiés :
`ItemTables.cs` (colonnes 0 et 1), `EntityScriptProxy.cs` (accès 32 bits aux mots `+0x280`, `+0x284`, `+0x288`), la passe de fin d'image
(`ModdedPos`), `NativeSpriteRegistry.cs` (deux clés de production), `WorldProxy.DestroyEntity` (le bouchon (1)) ; les tests ; l'annexe ; un
ADR. Les noms passent la règle 4 de l'ADR-0075 (`check_names.py`).

**Hors périmètre.** Les objets posés et le coffre (Q-E0C-1) ; la récursion E11 (Q-E0C-6) ; le clignotement rendu (Q-E0C-3) ; la
destruction des objets par la VRAM ; la coupe d'herbe (E20.e) ; le lâcher des enfants d'ennemis remis à 0 par leurs gestionnaires (E14.b).

**Partage des phases.**
- **Phase A** (T1, sans changement de comportement) : `LootDrop` et `ItemEffects` purs, testés contre les vecteurs de l'oracle (aucun
  appelant de production) ; les colonnes de `ItemTables` ; l'accès 32 bits.
- **Phase B** (T2) : C255 et A255 inscrits ; `ModdedPos` rafraîchi. Inerte tant que rien ne lâche (aucune entité de type 31 à 118 n'est
  « lâchée » aujourd'hui ; les posées gardent leur bouchon).
- **Phase C** (T3) : `DestroyEntity` appelle `SpawnContents` en tête. Seul effet visible : les objets tombent des ennemis et des conteneurs
  détruits.

**Tâches.**
- ⏳ **T0 — Annexe, oracle, mesures** (aucun code de la DLL) : (a) les modèles et harnais de la lecture (`docs/plan-e20-annexe/e20-e0c/work/`)
  rejoués depuis une copie, sortie identique au manifeste ; les vecteurs exportés en JSON pour les tests de la DLL (`SpawnContents`,
  `SpawnRandomLoot`, veto, table pleine ; C255 sur 8 000 vecteurs ; `GiveItem` des objets atteignables, avec les pas du générateur) ; (b)
  **P-L1** : le premier tick où tourne un objet lâché par la mort d'un ennemi dans son programme E1, case plus haute et case plus basse que
  celle de l'ennemi, mesuré sur la DLL ; **attendu** : un tick après la mort dans les deux cas (case plus haute : l'écart du défaut Q-E0C-8 ;
  case plus basse : égal au binaire, `BT-L2`) ; un résultat qui contredit V6 ou ce défaut est un arrêt ; (c) **P-L2** : le premier `IsOnGround` de l'objet lâché dans la DLL (le binaire : 0 au premier tick de C255, la fin
  d'image l'ayant déjà déplacé de `0xA0000`) ; (d) où la fin d'image de la DLL rafraîchira `ModdedPos` (l'équivalent de `0x80038364`,
  `0x800383C0-0x800383E0`, entités des états 2 et 3 non bloquées) ; (e) la liste prédite des tests qui bougeront en T3, nommément ; (f) les
  états 2 et 3 au binaire : le corps de `0x80049AF4`, `CdBusy` `0x8005A7D4` et ce que lit `[0x800C480C]`, `Unblock` sur un objet non bloqué ;
  le membre de la DLL qui écrit le masque de fermeture (celui de l'opcode qui écrit `[0x80149CBC]`, `0x8003EB08`) ; les vecteurs de C255
  qui couvrent les états 2 et 3.
- ⏳ **T1 — Phase A** (tests d'abord, rouges sur un squelette qui compile) : (l1) `SpawnContents` et `SpawnRandomLoot` contre les vecteurs (les
  sept écritures, le son 84, le Z passé moins 1 à `SpawnEntityBySpriteIndex`, le veto qui consomme quand même le générateur, la table pleine
  sans son), chaque cas « sans effet » accolé à un cas positif ; (l2) `GiveItem` : argent et plafond, PV des trois cœurs et leurs plafonds,
  PM, herbe avec inventaire plein et non plein, étincelles créées et pas du générateur, accolés deux à deux ; (l3) colonnes 0 et 1 de la
  table ; (l4) accès 32 bits, lecture et écriture des trois mots.
- ⏳ **T2 — Tests de monde, rouges avant T3** (un monde initialisé avec le catalogue ; un objet mis par `SpawnContents` sur une entité dont
  `ContentsItemId` est posé, à 1, 2 et 3 ticks par image) : (w1) la montée, le rebond tick par tick à la gravité 128 et à la gravité 3 ;
  (w2) le ramassage au contact, aux bornes exactes du recouvrement (`taille + 1`), accolé au tick où le héros est à un pixel de plus ; (w3)
  rien pendant un dialogue ouvert ; (w4) l'extinction à 600 ticks (destruction légère, aucun lâcher), `+0x1CC = 0x78` à 120 ; (w5) un objet
  sans règle des 600 ticks (`-1`) reste ; (w6) l'effet du ramassage sur le héros (argent, PV, PM, herbe) et le son de l'octet 5 ; (w7) un
  objet posé (type 31 à 118 né d'un enregistrement) ne fait rien, accolé à l'objet lâché ; (w8) du ramassage à la destruction légère, tick par
  tick, contre les vecteurs de C255 : états 1, 2, 3, le masque de fermeture à 3, l'objet dessiné jusqu'à sa destruction, aucun message ;
  accolé à un dialogue ouvert qui retient l'état 3 ; (w9) le premier tick où tourne l'objet lâché à la mort d'une entité : case plus haute
  (l'écart de Q-E0C-8, le test nommé d'après lui) et case plus basse (égal au binaire). **Phase B (code B)** : C255 et A255 inscrits,
  `ModdedPos` rafraîchi ; le compte du registre passe de 7 à 9 (les deux tests qui l'épinglent, réécrits par dérivation). Verts.
- ⏳ **T3 — Phase C** : `DestroyEntity` lâche en tête. Tests de production, **rouges avant la ligne** : (d1) le test qui épinglait le bouchon
  (`AlundraDestroyEntityBreakEffectTests.cs:204-219`) réécrit : une entité à `ContentsItemId = 69` lâche le type 99, le générateur n'avance pas
  (le tirage a eu lieu à la création), une entité à contenu 0 ne lâche rien ; (d2) les oracles Beannoïde et Muruta **comparent** les entrées
  `spawn_39F58` et `sound 84` au lieu de les exclure (80 lâchers, 48 vetos, 16 contenus nuls) ; (d3) la destruction légère ne lâche rien ; (d3b) le tick de naissance de (w9) sur le vrai chemin de `DestroyEntity` ;
  (d4) un arc réel : tuer un Beannoïde de la carte 14 (code `0xFC`) jusqu'au ramassage, ou jusqu'à l'extinction.
- ⏳ **T4 — ADR, plan, verifiers frais** (deux, l'un après l'autre, dont un sur les épingles : liste prédite contre liste mesurée). Mutants du
  code de la DLL, au moins 14, tous tués : veto oublié ; veto avant le tirage ; `+0x278` toujours 600 ; seuil du rebond `<` pour `<=` ; rebond
  sans la remise à 0 ; décompte qui détruit à 1 ; marque à 120 oubliée ; recouvrement `<=` pour `<` (chaque sens) ; positions du tick
  courant au lieu de l'image précédente ; ramassage pendant un dialogue ; l'état 3 qui n'attend pas la fermeture du dialogue ; le masque non écrit ; plafond d'argent ; PV d'un cœur moyen à `PVmax/2` ; étincelles par la
  file au lieu de tout de suite ; lâcher à la destruction légère ; lâcher après l'état 4 au lieu d'avant.

**Acceptation.** Les vecteurs de l'oracle égaux ; (l1) à (l4) verts en T1 ; (w1) à (w9) rouges avant le code B (nommés), verts après ((w9) : le test de la case
plus haute porte le nom de l'écart de Q-E0C-8 et affirme le tick de retard ; celui de la case plus basse est égal au binaire, `BT-L2`) ;
(d1) à (d4) et (d3b) rouges avant T3, verts après ; suite en Release (copie neutralisée) **et** en Debug, zéro échec ; tests déplacés ⊂ liste prédite de T0
(e), chacun réécrit par dérivation ; les traces et les arcs inchangés hors de la liste ; `cmp` de la DLL ; moteur, convertisseur et données
sans diff ; recette de l'auteur : sauvegarde de test près d'un Beannoïde de la carte 14 ; le tuer, voir l'objet monter et rebondir, le
ramasser (argent ou cœur, son), en laisser un s'éteindre.

**Arrêts.** P-L1 contredit V6 ou le défaut de Q-E0C-8 ; P-L2 contredit le binaire ; `0x80049AF4` n'a ni équivalent ni absence d'effet
établie ; un test hors de la liste prédite bouge ; un vecteur de l'oracle diffère ; un besoin de
toucher au moteur ; plus de 10 tests existants bougent ; un nom refusé par `check_names.py`.

**Budget.** Un `executor` par phase, deux tentatives ; `plan-verifier` avant ; deux verifiers frais après.

**Retour arrière.** La phase C se retire seule (la ligne de `DestroyEntity`) ; la phase B se retire seule (les deux inscriptions et le
rafraîchissement) ; la phase A ne change aucun comportement.

