# Plan — E19, opcodes de l'interpréteur de scripts

Étape E19 de [plan-conversion-totale.md](plan-conversion-totale.md). Elle porte les opcodes que
l'interpréteur saute encore par leur taille. La première phase suit la chaîne de l'histoire, du
bateau Klark jusqu'au premier livre de sauvegarde. La seconde couvre le reste du corpus, une famille
par tranche.

**Statut** : proposé le 2026-09-29, sur la branche `chantier/e19-opcodes`, avec l'enveloppe (§1.1)
et la première tranche exécutable, E19.a (§1.2). Les tranches suivantes se planifient et
s'approuvent chacune à part, comme en E16. Décisions de l'auteur : §0.1 et ADR-0015.

| Unité | Revue avant approbation | Après exécution |
|---|---|---|
| Enveloppe (ce plan) + E19.a | plan-verifier | verifier frais |
| E19.b à E19.f (phase 1) | plan-verifier, tranche par tranche | verifier frais |
| E19.g à E19.m (phase 2) | plan-verifier, tranche par tranche | verifier frais |

Aucune tranche ne touche aux fichiers de sauvegarde, aux secrets ni à une entrée non fiable : pas de
revue de sécurité. Si une tranche en ajoute, sa ligne ajoute la revue.

**Révision 1 (2026-09-29).** Première relecture de l'enveloppe et d'E19.a : **REVISE**, deux P2.
Un audit en lecture seule des citations et de la faisabilité, mené en parallèle, n'a trouvé aucun P1,
mais trois P2 et neuf P3. Corrections :
- l'arc A1 visait le mauvais `0x53` : c'est `@688` (vers 476), pas `@698` (vers 412, jamais atteint) ;
- l'arc A0 n'a pas de `0x53` : il a maintenant son propre signal de fin (`0x11 @547`, puis `0xFF
  @548`), et chaque arc a une limite d'images qui le fait échouer en nommant l'endroit où il s'est
  arrêté ;
- A1 part de la vraie arrivée `(44,23,4)`, où le programme B3 ouvre un dialogue : l'arc le ferme au
  bouton, comme le joueur ;
- la clé de l'avertissement de la garde ne contient plus le pc ;
- les détails du montage de test sont listés (§0.2.6, T5) ;
- A0b couvre la branche où la cinématique pousse le héros ;
- les documents périmés sont complétés, et quelques formulations sont corrigées.

Relecture neuve de clôture de la révision 1 (`b75807e`) : **READY**. L'enveloppe et E19.a attendent
l'approbation de l'auteur.

---

## 0. Cadre

### 0.1 Décisions de l'auteur (2026-09-29)

Question de l'auteur : l'histoire ne progresse pas après les premiers dialogues du bateau, alors
« faut-il des tâches pour que les drapeaux soient utilisés par Yarn et par le moteur de script ? ».
Le diagnostic en lecture seule (§0.2.1) répond non. Ce qui manque, ce sont des opcodes. Les
décisions suivantes ont été prises avec l'auteur le 2026-09-29.

- **D-E19-1** — Nouvelle étape E19 au plan maître, en tranches par famille d'opcodes. Phase 1 : la
  chaîne du bateau jusqu'au premier livre (carte 163). Phase 2 : le reste du corpus.
- **D-E19-2** — Tout opcode porté suit `ALUN_CD.EXE`, qui tranche sur la décompilation. Cette règle
  est déjà celle du chantier ; elle couvre ici l'entité de contexte (§0.2.3).
- **D-E19-3** — **Garde de boucle en production.** C'est un écart assumé à l'original, documenté.
  Après environ 1024 opcodes dans un même appel de script, le programme se met en pause jusqu'à
  l'image suivante sans perdre sa position, comme après un Break. La première fois, le journal le
  note.
- **D-E19-4** — **La boîte de nom est portée dans E19**, avec la **boîte de texte fidèle** : la
  place et le cadre de l'original. Elle sert à `0x0D`, `0x5C` et `0xC4`. Les portraits et la
  machine à écrire restent dans E12.c.
- **D-E19-5** — **Débloquer d'abord.** `0xC4` ouvre d'abord son dialogue sans nom, comme `0x5C`
  aujourd'hui. La boîte de nom arrive dans une tranche suivante (E19.f).
- **D-E19-6** — **Marches scriptées : contournement.** L'attente de marche `0x0B` reçoit le détour de
  navigation d'E4.d, que `0x1E` a déjà (écart E4-D5). `0x24` n'en reçoit pas. Aucune minuterie ne
  termine une attente bloquée : un blocage qui reste se corrige à la racine, dans les collisions ou
  le moteur.
- **D-E19-7** — **Effets visuels après le bateau** : les opcodes `0x90`-`0x94` et `0xA0`-`0xA3`
  forment une tranche de la phase 2 (E19.g).

### 0.2 Faits établis (lecture seule, 2026-09-29)

Deux passes de découverte ont établi ces faits ; chaque surface a été contre-vérifiée par un
contradicteur. Elles n'ont rien construit ni exécuté. Un fait marqué **[binaire]** est relu dans
`ALUN_CD.EXE` (France) ; les adresses sont celles du binaire. Les rapports complets sont dans le
scratchpad de la session (`progress/captain.md`, `progress/sweep.md`, `e19-0/*.md`).

#### 0.2.1 Diagnostic de progression

- Les drapeaux ne bloquent pas. Les 13 opcodes de drapeaux sont portés (`0x05 0x06 0x30`-`0x36 0x7B
  0x7C 0x80 0x81`, E16.a). Yarn `<<flag n>>` écrit la banque temporaire comme le décodeur de texte de
  l'original, et `$flag_n` / `$tmp_flag_n` existent (E16.f). La chaîne du capitaine n'utilise que des
  drapeaux persistants posés par `0x05`, jamais par Yarn.
- Chaîne de l'histoire jusqu'au premier livre : 389 → 390 → 476 → 478 → 476 → 392 → 391 → 416 → 163.
  Le livre est l'enregistrement 7 de la 163 (sprite 237), déjà géré par `AlundraSaveBook` (E16.e).
- Couverture : `Dispatch` porte 87 opcodes, plus `0x00` et `0xFF` dans la boucle d'appel. Un opcode non
  porté est sauté par sa taille (`AlundraEventProgramRunner.cs:1980-2003`,
  `EventOpcodeSizeTable.cs`), avec un avertissement par opcode. Sur le corpus : 111 136 instructions
  atteignables, dont 14 644 sautées, soit 88 opcodes distincts dans 392 cartes sur 483.
- Trois arrêts sur la chaîne :
  1. **390, blocage** (inférence statique) : la cinématique de sortie du capitaine (programme de
     carte B1, pc 481-547) utilise `0x43`, `0x42` et `0x59`, que la DLL saute. Le `0x1E Walk [80,0]`
     de `@540` attend alors le héros, verrouillé depuis `@481`, au lieu du capitaine. `0x11 Player
     gain control` (`@547`) n'est jamais atteint. Ce blocage survient quand le capitaine est le
     dernier des deux à qui on parle (le marin au grog, `G866`, et le capitaine, `G869`).
  2. **476, arrêt certain** : `0xC4` (7 sites) n'ouvre pas son dialogue, donc le drapeau temporaire
     T999, posé par `<<flag 999>>` dans le nœud, ne l'est jamais. B4 attend T999 à `@116`.
  3. **478, gel du processus** : les programmes C[11] (`@664`) et C[12] (`@756`) bouclent sans
     opcode qui suspende une fois `0x0B` sauté (sortie sur T50, éteint au chargement), et
     `RunOneScriptCall` n'a pas de garde en production (`AlundraEventProgramRunner.cs:355-415` ;
     `MaxIterationsPerCall` est nul par défaut, seul le harnais de l'intro le règle).
- Le reste de la chaîne n'a pas d'autre arrêt connu en lecture statique : 389 à 392, 391 et 416
  utilisent surtout des opcodes portés, ce qui leur manque est cosmétique ou de rythme, et le
  déroulé de 163 atteint `0x11 @201` (§0.2.4).

#### 0.2.2 Interpréteur de la DLL

- `RunOneScriptCall` est une boucle `while (true)` qui ne s'arrête que sur `0xFF`, `0x00` ou un
  gestionnaire qui rend 0. `EventTraceKind.LoopBudgetExceeded` est documenté « jamais produit en
  production » (`AlundraEventProgramRunner.cs:53-59`).
- Mesures pour la garde (corpus, statique) : la plus longue suite finie sans suspension compte 52
  opcodes (carte 11, F[2] `@408`), la pire boucle de minuterie `0x73`/`0x74` sans suspension 120
  (carte 440, `@1257`). En dehors de 478 sur la chaîne, 45 à 51 programmes de 18 ou 19 cartes
  forment des cycles sans suspension sous les règles de la DLL ; le nombre dépend de la façon de
  compter.
- Seuls les créneaux B et C reprennent un appel suspendu (état propre à l'entité). A, D, E et F
  partagent un état de travail (`:233-243`). `InitializeEventData` (`:312`) le réinitialise à chaque
  appel (`CodeIndex`, `Parameters` et `Sp`) mais garde `Result` exprès (`:107-121`). La table de
  créneaux du binaire `0x80023E10` ne fait reprendre, elle aussi, que B et C **[binaire]**.

#### 0.2.3 Entité de contexte (« logic entity ») **[binaire]**

- `RunScript` (`0x8004205C`) appelle chaque gestionnaire comme `h(a0 = logique = *(owner+0x230),
  a1 = owner, a2 = &pc, a3 = state)`. Le mot `+0x230` est relu avant **chaque** instruction
  (`0x80042284`).
- Seuls `0x42` et `0x43` écrivent sur l'owner (son `+0x230`), et `0x66` son état (`+0x234`/`+0x238`).
  Tous les autres gestionnaires agissent sur l'entité logique, ou la passent comme référence de
  recherche ou comme parent d'apparition. La décompilation est d'accord sauf pour `0x5F` : le
  binaire exécute `move a1,a0` en `0x8003F228` et agit donc sur l'entité logique.
- `0x42` (`0x8003E808`) pose le **héros** (`0x80127D30`), pas l'owner, et rend 1.
  `0x43` (`0x8003E81C`) cherche selon v1, avec l'entité logique pour référence :
  - avec au moins un résultat, il pose le **dernier** trouvé et `Result = 1` ;
  - sans résultat, il met `Result = 0` et laisse le contexte ;
  - dans les deux cas, il rend 2.
  `0x43 [0x80]` ne change pas le contexte (la recherche 0x80 rend la référence) mais met `Result`
  à 1. Il y a 2 sites, 476 B5 `@90` et `@107`.
- `0x66` ne change pas l'entité logique. Les commentaires qui l'affirment se trompent :
  `AlundraEntityScriptProxy.cs:40`, `AlundraWorldProxy.cs:2290` et `:2711`,
  `docs/intro-roadmap.md:99`. Il a 0 site.
- Durée de vie : `InitializeEntity` pose l'entité elle-même (`0x80042028`). Rien ne la remet à zéro
  ensuite : ni `0xFF`, ni Break, ni restart, ni changement de créneau, ni changement d'image. Le mot
  est partagé par les créneaux A à F de l'entité.
- Événements de carte (`RunMapEvents`, `0x8003C67C`) : l'owner est le héros. Chaque événement garde
  sa propre entité logique (`slot+0xC`, héros au départ), recopiée dans `hero+0x230` avant l'appel
  (`0x8003C794`) et relue après (`0x8003C7E4`).
- Hors de la zone de l'événement, le binaire réarme l'événement (`0x8003C7F0`-`0x8003C804`) :
  - l'état de son programme est vidé, `Result` compris ;
  - son programme B reprend la valeur de l'enregistrement ;
  - son entité logique redevient le héros.

  Aucune entité n'est écrite. La DLL suit la décompilation (`AlundraWorldProxy.cs:2313-2323`), qui
  écrit des champs sans lecteur ; un événement limité à une zone n'y redémarre donc jamais pendant
  un même chargement. Ces événements comptent 619 enregistrements dans 190 cartes → E19.j.
- Recherche (`0x8003C954`) : le type 0x80 rend la référence et 0x81 le héros. Une recherche par
  numéro d'enregistrement ne teste l'état que de la **référence** (Loaded, Normal ou Deactivated),
  jamais celui du candidat. `EntitySearchService` a déjà la même garde.
- DLL : `AlundraEntityScriptProxy.LogicEntity` (`:47`) est écrit et relu seulement par
  `RunMapEventsPass` (`AlundraWorldProxy.cs:2334`, `:2340`), et copié par `Clone` (`:1910`). Le
  runner ne le lit jamais, et `Dispatch` (`:458`) donne à chaque cas l'entité qui exécute. 13 opcodes
  portés agissent donc sur l'owner au lieu de l'entité logique : `09 0A 16 17 19 1A 1B 1E 1F 27 3E
  6E 70`. 16 autres lui passent l'owner comme référence de recherche ou d'apparition : `07 2C 2D 2E
  5A 5B 62 63 64 65 67 8B 8D AC AD B8`.
- Étendue : `0x43` a 478 sites et `0x42` 166, dans 98 cartes (programmes B et C, un E). Aucun dans
  389, 478, 392, 391 ni 416. Sur la chaîne : 390 B1 (`@534`, `@543`), 476 B5 (`@78`, `@90`, `@95`,
  `@107`) et 163 B1 (`@115`, `@184`). Hors chaîne, 74 enregistrements de 38 cartes laissent un
  contexte non nul dans un programme C, puis leurs programmes D, E ou F agissent sur l'entité
  retenue : c'est un changement visible, fidèle au binaire.

#### 0.2.4 Sites de la chaîne

- **390 B1** (`@481-547`, owner = héros) : `43 [2]` (`@534`) fait du capitaine n°1 (enregistrement 2)
  le contexte. `5B [2,6,65]` lui donne l'animation de marche, `1E [80,0]` attend qu'il ait fait
  80 px vers le nord (5 tuiles : la rampe `(39..40,45..47)` puis la rangée du trou `(39..40,44)`,
  walkability 4), `42` (`@543`) rend le héros, `2E [2]` détruit le capitaine et `11` (`@547`) rend
  la main. Le capitaine n°2 (enregistrement 3, pièce B, tuile `(7,33)`) n'existe au chargement que si
  `G870` était posé avant. Lui parler pose `G871`, qui arme le sommeil dans la cabine (B2, zone
  `(39,9)-(50,26)`, puis `53` vers 476).
- **476** : B4 (`@612`) attend `G1640`, puis `0xC4` (`@622`, `@632`, `@739`) et le sous-programme
  `@112` (`4C [2]; 50 [4]; 36 T999; 37 [60]; 51; 39`), puis `53` vers 478 (`@758`). B1 (`@63`) fait
  apparaître le bloc caméra transparent (enregistrement 1) par `0x8A`, et `67 [1]` le suit. B5
  (`@772`) déplace ce bloc par `43 [1]; 5B; 1E [48,0]; 59 [1,0]` et parle par `0xC4` et `0x5C`,
  puis `53` vers 392 (`@986`).
- **478** : 21 `0x0B`, `0x5E` (`C[5] @363`, fait monter le bloc 0 : T20 à T60 posés à mesure qu'il
  monte, environ 2000 ticks en tout, calcul statique), `0x73`/`0x74`, `0x08`, `0x89` (le chien
  recollé au bloc à chaque tick), `0x1C` (C[16]) et 11 `0x59`.
- **392** : `0x8E` (balancement de caméra), `0x0C`, `0x08`, tous trois dans la boucle de roulis.
  **391** : effets `0xA2`/`0x94`, `0x8E`, `0x5E` ×4, `0x59`, `0x4C`, `0x73`/`0x74`, tous cosmétiques
  ou de rythme. **416** : `0x0B` ×5 ; aujourd'hui la carte atteint `53` vers 163 plus tôt, sans ces
  marches. **163** B1 (réveil) : `0x1C` ×3, `0x0B` ×3, `0x24`, `0x43`/`0x42` et `0x59`, jusqu'à
  `11 @201` ; les prédicats de boutique ne servent qu'avec le portage d'objets (IA native, E14).

#### 0.2.5 Autres faits utiles aux tranches suivantes **[binaire]**

- `0xC4` (`0x80041DA8`, taille 6) : v1 = recherche du locuteur, v2 | v3<<8 = nom (index ETC
  0x100 à 0x1FF, par exemple 0x1BC « Lars »), v4 = texte, v5 = mode. Il rend 6 le jour même où la
  boîte s'ouvre, et 0 seulement si un dialogue est déjà ouvert ; il n'attend jamais la fermeture
  (`0x39` le fait). Il y a 31 sites dans 11 cartes, dont les 31 nœuds Yarn existent. Les huit noms
  sont déjà dans `Etc.yarn` (`Etc_0256` à `Etc_0509`), lisibles par
  `AlundraEtcStringTable.TryResolveText`. Le code de portrait décompilé de `Script_196_0C4` est
  faux : le binaire lit la même image que `0x5C` et `0x0D`.
- Boîte de nom : créneau 12 (`0x800A58BC`), à `(64,140)`, de 14×4 cellules (112×32 px). Elle a un
  **cadre** de 56 cellules 8×8 (listes `0x800A4FFC` et `0x800A545C`), absent de `UiBoxes.csv`. Le
  texte est centré à `y = 148`, pas 144 comme dans la décompilation. La boîte glisse depuis x = 320
  en 15 pas, à l'ouverture comme à la fermeture. La boîte de dialogue fait 36×7 cellules à
  `(16,168)` (`0x8009CFBC`). `0x0D` et `0x5C` prennent le nom (`SpriteTableIndex + 0x100`) et le
  portrait de l'entité logique, ou de la première trouvée.
- Tant qu'il n'y a pas de machine à écrire, Yarn pose les drapeaux d'une page à son ouverture ; dans
  l'original, `\999` est le dernier code du texte. C'est déjà vrai pour les `0x5C` de 476 et 391 :
  une telle boîte se ferme environ 60 ticks après son ouverture.
- `0x0B` (`0x8003D468`, taille 4) : il pose `TargetAnimationId = v1` à chaque appel. Au premier
  appel pour un pc, il mémorise ce pc et la position, et rend 0. Ensuite il rend 4 dès que
  `r <= |dX|>>16` ou `r <= |dY|>>16`. Il ne donne pas de direction et n'a pas de sortie si le
  marcheur est bloqué.
- `0x1C` (`0x8003D7FC`) compte les passages d'animation. **Le binaire contredit la décompilation** :
  la fin d'une animation Chain incrémente le compteur `+0xB0` (`0x80038D64`), et le changement
  d'animation ne le remet **pas** à zéro (`0x80038B6C` ne remet que `+0xAC`). Sur la chaîne, les
  quatre sites sont des animations Hold ou Chain : aucun n'a besoin d'un signal de boucle Loop.
- `0x5E` : `ForceZ = int16(v2 | v3<<8) << 8` pour chaque entité trouvée. Le bloc 0 de 478 a un
  `CharacterController`, et la DLL recopie `ForceZ` dans `FinalForceZ` à chaque tick
  (`AlundraScriptedMotion.cs:227`). Elle ne l'applique à `PosZ` que dans la branche « en l'air »
  d'`EvaluateEntitySupport` (`AlundraEntityScriptProxy.cs:700`), celle d'une entité qui monte.
- La DLL n'a pas de compteur de boucles d'animation : `AnimCompleteCounter` n'est jamais écrit, et
  `ForceResetAnimationFlag` n'est jamais effacé. `CollidedWithEntityZ` diffère du binaire : le
  binaire l'efface à chaque passe physique (`0x800383B4`) et le pose à tout contact en Z
  (`0x800376E0`). Aucun code de la DLL ne le lit aujourd'hui.
- `0x8E`/`0x8F` sont un **balancement de caméra**, pas un défilement du fond. `0xA4` pose le masque
  des couches de fond. `0x82` donne un objet. `AddOneItemIfUnlocked` rend le nombre courant quand il
  est au maximum (`0x8004E58C`-`0x8004E5B0`) ; la DLL et son test (`AlundraItemInventoryTests.cs:368`)
  figent l'erreur de la décompilation.
- Le convertisseur n'exporte pas les effets de carte : 544 enregistrements dans 157 cartes, dont 1 sur
  476, 5 sur 478, 5 sur 391 et 4 sur 163. Quand la réserve d'effets du binaire est pleine, un effet
  demandé est simplement abandonné ; la `Breakpoint` de la décompilation n'y existe pas.
- La table des tailles se trompe pour `0x5F` : elle donne 1, le binaire rend 8 (0 site). Plusieurs
  libellés sont faux : `0x20`, `0x21`, `0x47`, `0x48`, ainsi que `0x35` et `0x36`, inversés.

#### 0.2.6 Banc de test

- Le montage d'E16.e (`AlundraSaveBookEndToEndTests.cs`) fait tourner une vraie carte exportée :
  `InitializeWithWorld` réel, héros possédé par un vrai `AlundraPlayerController` avec une entrée de
  manette de test, un tick logique par `Update(0.02f)`. Il ne suit pas les warps, et ses entités sont
  « nues » : déplacement horizontal sans collision, aucun déplacement vertical.
- Sans gestionnaire d'assets, `InstallDialogueAssets` ne fait rien (`AlundraWorldProxy.cs:1112-1118`) :
  aucun nœud Yarn ne joue. Un test d'arc doit injecter lui-même les assets de dialogue de la carte, le
  partagé et l'ETC, sinon 476 reste bloquée même avec `0xC4`. Les voies :
  - les propriétés internes `MapDialogueAsset` et `SharedDialogueAsset` du runner (`:112`, `:118`),
    chargées par `DialogueTestAssets.LoadFromDisk` depuis `Maps/…/dialogues/<carte>.dialogue` et
    `Dialogues/Shared.dialogue` ;
  - `AlundraEtcStringTable.SetEtcDialogueAssetForTests` pour l'ETC.
- Détails du montage qu'un arc doit reprendre (`AlundraSaveBookEndToEndTests.cs`) :
  - `BuildRealMap17World` (`:271-310`) est privé et code en dur `Maps/Overworld/<nom>/tilemap/` avec
    quatre couches ; la 390 a aussi quatre couches, mais sous `Maps/The Klark/…` ;
  - réflexion `_backdropStage._clearColorApplied = true` (`:95-96`) ;
  - `SetDebugCameraPanEnabledOverrideForTests(true)` (`:50`, `:55`) ;
  - remise à zéro des singletons (`:59-64`, `SaveGameDirectorTestSupport.cs:106-124`) ;
  - animation d'entrée du héros ramenée de 54 à 0 (`:121-122`) ;
  - `PadStateProviderForTests` (`:76`) ;
  - attribut `[Collection(AlundraMusicPlayerSingletonCollection.Name)]` (`:37`).
- Placement du héros : `AdoptPlayerPawn` le pose sur la case de nouvelle partie `(33,59,0)` et calcule
  ses `Tile*` une seule fois (`AlundraWorldProxy.cs:1674-1688`). Un arc doit poser `Pos*` **et**
  `Tile*` avant la première image, avec `TileZ = PosZ >> 20` exact : les zones testées (`0x3B`) en
  dépendent.
- Les entités nues bougent quand on leur donne une animation de marche : `TickScriptedNpc`
  (`AlundraEntityScriptProxy.cs:1019`, `AlundraScriptedMotion.cs:159-162`), avec une image de retard
  sur `CurrentAnimationId` (`AlundraFrameSyncPasses.cs:106-113`). La branche sans contrôleur fait
  `PosX += FinalForceX` (`AlundraScriptedMotion.cs:237-241`). Le capitaine (animation 6, vitesse
  160) avance de 1,25 px par tick vers le nord : 80 px en environ 64 ticks.
- `SaveGameDirectorTestSupport.LogCapture` (`:358`) capte le journal, qui est global : il faut
  filtrer sur le texte du message.
- Les tests unitaires du runner construisent leurs programmes avec `NewDocument`
  (`AlundraEventProgramRunnerTests.cs:20`), qui ne fait qu'une table A. Un test des créneaux B ou C
  demande un document avec ces tables.
- `TraceSink` et `MaxIterationsPerCall` sont internes et atteignables par `proxy.EventProgramRunner`.
  Les types `UnknownSkipped`, `UnknownNoSizeTerminated` et `LoopBudgetExceeded` donnent le triplet
  (créneau, pc, opcode) exact.
- `AlundraWarpDirector.HasPendingArrival` et `ArrivalRecordForTests` permettent d'arrêter un arc dès son
  `0x53`, sans construire la carte suivante.
- `IntroTraceHarnessTests.ImplementedOpcodes` (`:319`) est une copie tenue à la main. Le test de trace
  réécrit `docs/intro-trace-389.txt` et `docs/intro-programs-389.txt` à chaque passage.
- `UnknownOpcode_KnownSize_SkipsBySize` (`AlundraEventProgramRunnerTests.cs:320`) utilise `0x08` : à
  changer quand `0x08` sera porté (E19.c), par exemple pour `0x4E`.

---

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Arcs de test (§1.3) | Recette en jeu |
|---|---|---|---|
| **E19.a** | Entité de contexte (`0x42`, `0x43`, et tous les opcodes sur l'entité logique), `0x59`, garde de boucle (D-E19-3), support des arcs | A0, A0b, A1 | Le capitaine sort par l'escalier et réapparaît en pièce B ; sommeil, puis 476 |
| E19.b | Carte 476 : `0xC4` sans nom (D-E19-5), `0x8A` (bloc caméra), `0x4C` gardé pour la machine à écrire | A2, A4 | La vision de Lars et Melzas jusqu'à 478, puis jusqu'à 392 |
| E19.c | Carte 478 et marches : `0x0B` avec détour (D-E19-6), `0x1C`/`0x1D` (compteur du binaire, Chain et Hold), `0x5E`, `0x08`, `0x0C`, `0x3A`, `0x89`, `0x73`/`0x74` | A3, A7 | La vision de 478 va au bout ; la plage 416 mène à Inoa |
| E19.d | Fin de chaîne : `0x24` sur l'entité logique, `0x40`/`0x41` sur l'entité logique, reste de 392, 391 et 163 | A5, A6, A8 | Naufrage, plage, réveil à Inoa, main rendue |
| E19.e | Recette de bout en bout, plus un test statique : aucun opcode sauté sur la chaîne hors liste d'exceptions | toute la chaîne | Nouvelle partie jusqu'au livre de la 163, sauvegarde, rechargement (avec les recettes d'E16 en attente) |
| E19.f | Boîte de nom et boîte de texte fidèle (D-E19-4) : export du cadre, écrans XAML liés à un view model, cycle de vie de la boîte de nom, pour `0x0D`/`0x5C`/`0xC4` | tests MGDesktop | Les noms s'affichent au-dessus de la boîte, à la place de l'original |
| E19.g | Effets visuels (D-E19-7) : export des effets par le convertisseur, réserve de 128 effets aux règles du binaire, `0x90`-`0x94`, `0xA0`-`0xA3`, rendu | cartes à effets | L'aura de 476, les vagues de 391 |
| E19.h | Attentes en Z et contacts : `0x20`-`0x23`, `0x25`, `0x26`, `0x47`, `0x48` ; `CollidedWithEntityZ` et `ForceAdjusted` alignés sur le binaire | ciblés | ciblée |
| E19.i | Boucles d'animation Loop pour `0x1C`/`0x1D` (101 sites dans 30 cartes) : signal de boucle du moteur, manque à consigner puis à corriger | ciblés | ciblée |
| E19.j | Événements de carte : réarmement hors zone du binaire (619 enregistrements) ; `0x40`/`0x41` complets avec `g_clearProgramState` sur l'entité logique | ciblés | ciblée |
| E19.k | Caméra : balancement `0x8E`/`0x8F`, masque des fonds `0xA4` | ciblés | 392, 391 |
| E19.l | Prédicats, branches et restes : `0x82` (avec la correction d'`AddOneItemIfUnlocked`), `0x83`, `0x84`, `0x87`, `0x3F`, `0x95`, `0x99`, `0x9A`, `0x9F` (avec `InitializeContents`), `0x57`, `0x58`, `0x4A`, `0x2A`, `0x2B`, `0x5D`, etc. ; liste fermée au recensement du moment | ciblés | ciblée |
| E19.m | Hygiène et clôture : taille de `0x5F` (8), libellés faux, `0x01` qui rend 0, modes aléatoires 4 et 5 de `ResolveDirectionFromParam` ; test statique : aucun opcode atteignable sauté dans le corpus hors E14 (IA native) et E18 (`0xBB`) | corpus | — |

- **Ordre** : E19.a → E19.b → E19.c → E19.d → E19.e, puis E19.f. Les tranches de phase 2 viennent
  ensuite, dans l'ordre que l'auteur choisira.
- **Dépendances** : E19.b et E19.d ont besoin de l'entité de contexte d'E19.a. E19.c a besoin de la
  garde, pour que ses tests ne figent pas si un opcode reste non porté.
- **Tests de ces tranches** : E19.c et E19.d ajouteront au support d'arcs un déplacement vertical
  pour les entités nues (point O-E19-3).
- **Mise à jour de ce plan** : chaque tranche de la phase 1 remet au §0.2 ce qu'elle a mesuré en
  vrai, puis fait détailler, relire et approuver la tranche suivante.

### 1.2 E19.a — Entité de contexte, `0x59` et garde de boucle ⏳

**But.** La cinématique du capitaine sur la 390 se joue comme dans l'original : le capitaine monte
l'escalier et disparaît, puis on le retrouve en pièce B, et la cabine endort Alundra et charge 476.
Plus aucun script ne fige le jeu.

**Périmètre.** DLL (`Alundra/Scripts/AlundraEventProgramRunner.cs`, `AlundraEntityScriptProxy.cs`,
commentaires d'`AlundraWorldProxy.cs`), tests (`Alundra.Tests/`), docs (`docs/intro-roadmap.md`,
annexes de trace régénérées, ce plan). Ni le moteur, ni le convertisseur, ni l'export ne sont touchés.

**Hors périmètre d'E19.a.** Le réarmement hors zone des événements de carte (E19.j). `0x40`/`0x41`
et `g_clearProgramState` sur l'entité logique (E19.j ; ces deux opcodes ne sont pas portés
aujourd'hui). Le portail trou et escalier de la 390 (O-E19-1).

#### Tâches

- **T1 — L'entité logique, instruction par instruction.**
  - `RunOneScriptCall` résout `logic = owner.LogicEntity ?? owner` avant chaque opcode
    (`0x80042284`), puis appelle `Dispatch(command, logic, owner, v, state)`.
  - Dans les cas existants, `entity` désigne désormais l'entité logique, et le paramètre de l'owner
    est ajouté. Les 13 opcodes de champs et les 16 opcodes de référence du §0.2.3 agissent alors sur
    l'entité logique, sans modifier chaque cas.
  - `state` reste l'état du programme qui s'exécute, sur l'owner. Il faut vérifier, cas par cas et
    dans chaque méthode appelée, qu'aucun cas ne lit l'état du programme sur l'entité : aucun
    `entity.EventProgramState`, `ProgramIndexes` ni `EventTrigger`. Aujourd'hui aucun cas de
    `Dispatch` ne le fait.
  - `Clone` ne copie plus `LogicEntity` : une nouvelle entité démarre sur elle-même, comme
    `InitializeEntity` (`0x80042028`).
  - `RunMapEventsPass` garde déjà une entité logique par événement (`:2334`, `:2340`) : inchangé.
  - Corriger les commentaires qui disent que seul `0x66` change l'entité logique :
    `AlundraEntityScriptProxy.cs:40`, `AlundraWorldProxy.cs:2290` et `:2711`. Corriger aussi
    l'entrée entière du point (2) de `docs/intro-roadmap.md:98-99`, pas seulement sa clause sur
    `0x66` : `RunMapEventsPass` pose déjà ces champs (`AlundraWorldProxy.cs:2326-2334`).
  - `EntitySearchService.cs:86-91` dit que la référence de recherche est toujours l'entité elle-même :
    c'est faux après T1, à corriger.
- **T2 — `0x42` et `0x43`** (`0x8003E808`, `0x8003E81C`).
  - `0x42` : `owner.LogicEntity = PlayerEntity`. Rend 1. Sans héros (contexte dégradé), rien ne
    change, et le cas passe par `LogDegradedNoPlayerOpcodeOnce`, comme `0x3B`, `0x3E` et `0x53`.
  - `0x43` : la recherche `v1` prend l'entité logique pour référence (`EntitySearchService`).
    - Sans résultat : `Result = 0`, contexte inchangé.
    - Sinon : `owner.LogicEntity` prend le **dernier** résultat, et `Result = 1`.
    - Rend 2 dans les deux cas.
    - `0x43 [0x80]` reste un changement sans effet sur le contexte, avec `Result = 1`, comme le
      binaire.
- **T3 — `0x59`** (`0x8003EE8C`, taille 3) : pour chaque entité trouvée par `v1` (référence :
  l'entité logique), `TargetAnimationId = v2`. Rend 3. Ce cas couvre 669 sites « héros au repos »
  `[129,0]`, dont 390 `@515` et 389 `@1369`.
- **T4 — Garde de boucle** (D-E19-3).
  - Le budget d'un appel est `MaxIterationsPerCall ?? ProductionLoopBudget`, avec
    `ProductionLoopBudget = 1024` : 8,5 fois la pire boucle mesurée, 19,7 fois la plus longue suite
    finie.
  - La garde existe déjà (`:361-365`) : elle coupe avant la lecture de l'opcode et ne touche ni à
    `CodeIndex` ni à `Parameters`. B et C reprennent donc au même opcode à l'image suivante ; A, D, E
    et F repartent de zéro, comme aujourd'hui. T4 lui donne le budget par défaut et ajoute
    l'avertissement.
  - Au dépassement, la garde envoie `LoopBudgetExceeded` au `TraceSink`. Ce rapport porte l'opcode
    `Codes[CodeIndex]`, pas `state.Sp` comme aujourd'hui (`:363`) : `Sp` est l'opcode lu juste avant.
  - La garde écrit alors **un seul** avertissement par (owner, créneau, index du programme de ce
    créneau), sur le canal de `UnknownOpcode` (`Logs.WriteWarning`), avec un ensemble à côté de
    `_loggedUnknownOpcodes` (`:125`).
    - Le pc n'est pas dans la clé : une boucle B ou C reprend à un pc différent à chaque image.
    - L'index du programme sépare les événements de carte, qui ont tous le héros pour owner et le
      créneau B.
  - Mettre à jour la doc de `MaxIterationsPerCall` et celle de `LoopBudgetExceeded`, ainsi que
    `docs/intro-roadmap.md:88-90` (« `MaxIterationsPerCall` nul par défaut »).
  - Le harnais de l'intro garde sa valeur explicite, 20000.
  - Limite connue : un programme de recherches qui boucle alloue deux listes par opcode, soit
    environ 2048 par image. Cela ne concerne que les boucles pathologiques.
- **T5 — Support d'arcs et tests** (§1.3).
  - Support partagé, construit sur le montage d'E16.e, avec les détails du §0.2.6 :
    - monde bâti d'après le chemin du dossier de la carte (zone et nom), toutes ses couches,
      `ProjectPath` réglé ;
    - héros possédé, en-tête du héros appliqué, animation d'entrée ramenée à 0 ;
    - assets de dialogue injectés (carte, partagé, ETC) ;
    - drapeaux posés, puis `Pos*` et `Tile*` du héros posés avant la première image ;
    - boucle d'images avec une **limite chiffrée par arc**. Au-delà, le test échoue et nomme, pour
      chaque programme qui a tourné, le dernier (créneau, pc, opcode) exécuté ;
    - arrêt sur le **signal de fin** de l'arc (§1.3) : `HasPendingArrival` pour un arc qui finit par
      un `0x53`, sinon l'instruction de fin nommée ;
    - collecteur `TraceSink` de toutes les instructions, qui sert aux signaux de fin, au dernier pc
      et aux listes d'opcodes sautés et de dépassements ;
    - boutons de manette pour fermer une boîte de dialogue, comme le joueur.
  - Un export absent **fait échouer** le test en le nommant ; il ne le fait jamais passer en silence.
  - A0 s'écrit **avant** T1 à T3. Sur le code d'avant, il doit échouer dans sa limite en nommant
    `0x1E @540`, sans figer la suite de tests : c'est la preuve que l'arc voit le blocage.
  - Tests unitaires (sur le modèle d'`AlundraEventProgramRunnerTests` ; un document avec tables B et
    C pour les tests de ces créneaux ; `LogCapture` filtré sur le message pour le journal) :
    - `0x43` trouvé et non trouvé : `Result`, dernier résultat, taille 2, contexte inchangé sans
      résultat, `[0x80]` ;
    - `0x42` : héros, taille 1, sans héros ;
    - persistance du contexte entre deux appels, après un Break, et d'un créneau à l'autre de la
      même entité ;
    - `0x1E`, `0x1A`, `0x09` et une recherche d'id qui agissent sur l'entité logique ;
    - une recherche d'id depuis une entité logique détruite ne trouve rien ;
    - un événement de carte garde son contexte ;
    - `Clone` ne copie pas le contexte ;
    - `0x59` sur 0x81, sur un id et sur 0x80 ;
    - garde :
      - une boucle `Goto` sans suspension, de longueur qui ne divise pas 1024, rend après le budget
        et reprend au même `CodeIndex` ;
      - l'avertissement n'est écrit qu'une fois en plusieurs images ;
      - le type de trace et l'opcode rapportés sont les bons ;
      - une boucle avec une attente ne la déclenche jamais ;
      - `MaxIterationsPerCall` la remplace.
  - Arcs A0, A0b et A1 (§1.3).
  - Tests existants à revoir, pour la raison de la tranche :
    - `IntroTraceHarnessTests.ImplementedOpcodes` gagne `0x42`, `0x43` et `0x59` ; régénérer et
      committer `docs/intro-trace-389.txt` et `docs/intro-programs-389.txt`. Seul le libellé de
      `0x59 @1369` doit changer : aucun point épinglé de l'intro ne bouge (389 n'a pas de
      `0x42`/`0x43`) ;
    - le test « Cave 140 » (`AlundraEventProgramRunnerTests.cs:4451`) traverse `0x43 [17] @750`. La
      recherche n'y trouve rien, donc `Result = 0` et le contexte ne change pas, puis `0x3B @757`
      réécrit `Result` avant `0x03 @764` : ses assertions tiennent. Seul son commentaire (`:4472-4476`,
      « `0x43` non porté, sauté par sa taille ») est à mettre à jour ;
    - `AlundraWorldProxyEventPassTests.cs:387` reste valide.
- **T6 — Docs.** Mettre à jour ce plan (statuts, faits mesurés) et `plan-conversion-totale.md`
  (ligne E19). L'ADR-0015 est déjà écrite.
- **T7 — Recette en jeu (auteur).** Lancer le jeu hors de l'app Claude, en Debug.
  1. Après l'intro, parler au marin au grog (salle du nord, par la porte 2 du pont) puis au
     capitaine **en dernier**.
  2. Le capitaine va vers l'est, monte l'escalier et disparaît dans le trou ; la main revient.
  3. Aller en pièce B, par l'escalier et le trou ou par la porte du pont : le capitaine y est.
     Lui parler.
  4. Entrer dans la cabine d'Alundra. Un dialogue s'ouvre à l'entrée (programme B3) ; le fermer.
     Alundra s'endort, et 476 se charge. 476 s'arrête ensuite au premier dialogue `0xC4`, ce qui est
     attendu jusqu'à E19.b.
  5. Le journal ne doit contenir aucun avertissement de garde de boucle.

  Raccourci : F6 puis éditer `debug-json.sav` : `initialMapId` 390, `cameraTile` (30,57,4) pour la
  salle 1, `gameFlags[27] |= 36` (`G866`, `G869`), puis F9.

#### Acceptation d'E19.a

1. Les arcs A0 et A0b passent, chacun en moins de 800 images :
   - pendant `1E @540`, c'est le capitaine qui avance d'au moins 80 px vers le nord, pas le héros ;
   - signal de fin : le programme B1 exécute `0x11 @547` puis `0xFF @548`. Ensuite, `G870` est posé,
     le capitaine n°1 est `FlagToDestroy` et `PlayerControlFlags == 0` ;
   - aucun opcode sauté ni aucun dépassement dans les programmes de la 390 ;
   - A0b passe aussi par la branche qui pousse le héros (`@482-514`, deux `0x1F`).
   - Sur le code d'avant T1 à T3, A0 a échoué dans sa limite en nommant `0x1E @540` (constat
     consigné dans le rapport de la tâche).
2. L'arc A1 passe en moins de 900 images :
   - au chargement, le capitaine n°2 existe et le n°1 est détruit ;
   - le dialogue de B3 s'ouvre, puis se ferme au bouton ;
   - `G1640` est posé (`@685`) ;
   - signal de fin : `0x53 [220,1,0,0,3,4,73]` est exécuté à `@688` (créneau B). L'arrivée porte la
     carte 476, `PosX = 12 << 16`, `PosY = 8 << 16`, `PosZ = 3 << 20` et l'effet 4 ;
   - le second `0x53` du programme, à `@698` (vers 412), n'est jamais atteint : le warp gèle les
     événements de carte dès `@688`.
3. Les tests unitaires de T5 passent.
4. Build de la solution en Release sans erreur. `Alundra.Tests` et les tests du convertisseur en
   Release, avec `--blame-hang-timeout 60s` : 0 échec.
5. Un verifier frais rend CONFIRMED sur 1 à 4.
6. La recette T7 de l'auteur. Tant qu'elle n'est pas faite, la tranche reste 🧪.

#### Risques d'E19.a

- **Le capitaine bloqué en production.** Le montage fait marcher des entités nues, sans collision.
  En jeu, le capitaine a un contrôleur : s'il ne franchit pas la rampe ou la rangée du trou
  (walkability 4), `0x1E` attend toujours, malgré son détour E4.d. La recette le montre. Un tel
  blocage se corrige à la racine (D-E19-6), dans une tâche de diagnostic ajoutée au plan.
- **Changements visibles hors de la chaîne** : 98 cartes utilisent `0x42`/`0x43`, et 74
  enregistrements de 38 cartes ont un contexte qui persiste d'un créneau à l'autre. C'est fidèle au
  binaire ; la recette ne couvre que la chaîne.
- **Contexte périmé du héros** : après la passe des événements de carte, `player.LogicEntity` garde la
  valeur du dernier événement, comme `hero+0x230` dans l'original. Aucun autre appel de production ne
  fait exécuter un script au héros. Un test qui le fait doit remettre le contexte à zéro.
- La garde relance du début les créneaux A, D, E et F à chaque image. C'est journalisé, et préférable
  à un jeu figé.

### 1.3 Arcs de test (support d'E19.a, réutilisé par les tranches suivantes)

Chaque arc part d'une carte chargée seule, avec des drapeaux posés et le héros placé. Les valeurs
attendues sont écrites à la main avant le code. Chaque arc a :
- un **signal de fin** prouvé par la trace : son propre `0x53` quand le programme en a un, sinon
  l'instruction de fin nommée dans la table. Un arc qui n'a pas produit ce signal ne passe jamais ;
- une **limite d'images** chiffrée. Au-delà, il échoue en nommant, pour chaque programme qui a
  tourné, le dernier (créneau, pc, opcode) exécuté, sans figer la suite de tests.

Les limites des arcs des tranches suivantes seront fixées par leur tranche, d'après une première
mesure.

| Arc | Carte | Drapeaux posés | Héros | Signal de fin et attendu | Limite | Tranche |
|---|---|---|---|---|---|---|
| A0 | 390 | `G866`, `G869` (mot 27 : 36) | (30,57,4), salle 1, hors de la zone de poussée | `0x11 @547` puis `0xFF @548` (B1) ; §1.2, acceptation 1 | 800 | E19.a |
| A0b | 390 | idem | (38,49,4), dans la zone de poussée (`3B [36,40,48,50,4,8]` `@482`) | idem, après les deux `0x1F` (`@496`, `@507`) | 800 | E19.a |
| A1 | 390 | `G866`, `G869`, `G870`, `G871` (mot 27 : 228) | (44,23,4), arrivée de la porte 3 du pont ; le dialogue de B3 (`3B [43,45,20,23,4,4]` `@712`) s'ouvre et se ferme au bouton | `0x53 @688` (B2) vers 476 ; §1.2, acceptation 2 | 900 | E19.a |
| A2 | 476 | `G1640` (mot 51 : 256) | arrivée | `0x53 @758` vers 478 | E19.b | E19.b |
| A3 | 478 | `G1641` (mot 51 : 512) | arrivée | T20 à T60 posés, puis le `0x53` vers 476 | E19.c | E19.c |
| A4 | 476 | `G1641` | arrivée | `0x53 @986` vers 392 | E19.b | E19.b |
| A5 | 392 | — | couloir du portail | portail vers 391 | E19.d | E19.d |
| A6 | 391 | — | arrivée | le `0x53` vers 416 | E19.d | E19.d |
| A7 | 416 | — | arrivée | `0x53` de `C[1] @640` vers 163 | E19.c | E19.c |
| A8 | 163 | — | arrivée | `G0`, `0x11 @201`, `G1662`, livre présent | E19.d | E19.d |

Les valeurs exactes de chaque arrivée se décodent des opcodes `0x53` cités et s'écrivent dans le test
avant le code.

---

## 2. Mesures

Réservé aux mesures faites en exécutant les tranches.

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E19-1 | Les portails trou et escalier de la 390 (portails 4 et 5, même carte, effet 0) n'ont jamais été essayés sur les vraies cellules : `plan-transitions-carte.md:97` les déclarait hors périmètre. Si la recette d'E19.a montre que le héros ne suit pas le capitaine par là, faut-il corriger dans E19 (le passage est sur la chaîne) ou dans un chantier de transitions ? | E19.a (recette) |
| O-E19-2 | Nouveaux écarts de la décompilation relevés dans le binaire : `0x5F` (entité et taille), `0x66` (sens de la copie), compteur de `0x1C`, `y` de la boîte de nom, portrait de `Script_196_0C4`, test de zone de `GetMapEffectRecord`, `AddOneItemIfUnlocked`, `InitializeEventData`. Le portage suit le binaire. Faut-il aussi corriger la décompilation dans l'analyseur, comme pour la taille de `0x78` en E16.a ? | E19.m |
| O-E19-3 | Déplacement vertical des entités nues dans le support d'arcs (A3, A6) : dupliquer la passe du harnais de l'intro, l'extraire (le fichier de trace épinglé serait touché), ou ouvrir en production un point d'entrée du chargeur de préfabs pour que le montage ait de vrais contrôleurs ? | E19.c |
| O-E19-4 | Le gestionnaire natif du créneau E (`0x8007ED10`, destruction après `Deactivated`, 417 enregistrements sur 85 cartes) : E14, ou une tranche d'E19 ? Sur la chaîne, il ne touche que l'oiseau de la 389 et des PNJ d'Inoa. | E14 |

## 4. Hors périmètre

- IA native (E14) : portage des objets de boutique, gestionnaire de destruction du créneau E,
  animaux d'Inoa.
- « Réessayer » après la mort, `0xBB` (E18). Conversion des cinématiques (E17).
- Portraits et machine à écrire des dialogues (E12.c, D-E19-4).
- Le rechargement du monde par les portails de même carte, sauf si la recette d'E19.a le montre cassé
  (O-E19-1).

## 5. Arrêts, budgets et retours arrière

- **Budgets** : au plus deux tentatives par tranche au même niveau d'exécutant, jamais une troisième
  identique ; au plus cinq passes de correction et re-vérification par tranche, chacune sur un état
  réellement changé.
- **Arrêts** :
  - un fait mesuré qui contredit une décision D-E19 ou un fait marqué [binaire] ;
  - un point épinglé de l'intro qui bouge ;
  - un test existant qui devrait changer pour une autre raison que la tranche ;
  - un arc qui passe sans avoir produit son signal de fin ou son résultat attendu, ou un arc qui
    fige la suite de tests au lieu d'échouer dans sa limite ;
  - une modification de l'auteur indexée par erreur ; un commit sur `main` ; un push ;
  - tout contact avec `CasaEngine.Launcher/Program.cs`.
- **Retours arrière** :
  - DLL et tests : abandon de la branche (`git switch main`) ; rien n'est fusionné sans l'auteur ;
  - docs et ADR : `git revert` de leur commit ;
  - annexes de trace régénérées : elles reviennent avec le commit qui les a changées.
