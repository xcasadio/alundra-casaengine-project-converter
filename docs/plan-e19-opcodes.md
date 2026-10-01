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
- **D-E19-12** (2026-10-01, après la recette d'E19.a2) : `ForceAdjusted` suit le binaire et ne se lève
  qu'au tick sans avance ; remplace D-E19-10. Détail au §1.2c (E19.a3), ADR-0017.
- **D-E19-8 à D-E19-11** (2026-09-29, après la recette d'E19.a) : un pas bloqué sur le champ de
  cellules avance jusqu'au contact, dans le moteur ; le glissement le long des murs vient avec E19.h ;
  `ForceAdjusted` garde la règle de la DLL (D-E19-10, **remplacée par D-E19-12** ci-dessus : le
  drapeau suit désormais le binaire) ; dans le moteur, les drapeaux « curtailed » signifient
  « raccourci ». Détail au §1.2b (E19.a2), ADR-0016.
- **D-E19-13 à D-E19-16** (2026-10-01, avant le plan d'E19.c) — ADR-0018 :
  - **D-E19-13** — Le retard d'une image de `CurrentAnimationId` pour le mouvement des PNJ est
    **gardé** (O-E19-5 tranché). Les points épinglés de l'intro ne bougent pas. Une marche qui part de
    l'arrêt dure un tick de plus que dans l'original, et le dernier tick d'une marche suivie d'un arrêt
    avance encore à l'ancienne vitesse (le pas de 1,25 px du marin 12, les 0,75 px du bloc de la 476).
  - **D-E19-14** — Les arcs chargent les **vrais préfabs** du projet exporté (O-E19-3 tranché) : un
    gestionnaire d'assets construit par le test, sans changement de la DLL.
  - **D-E19-15** — L'ordre de calcul de `TileZ` (avant le mouvement vertical dans la DLL, après dans
    le binaire) se corrige en **E19.h**. E19.c épingle les valeurs actuelles et annonce le décalage
    d'une image.
  - **D-E19-16** — Le moteur reçoit une **horloge d'animation exacte en ticks** dans E19.c (tranche
    E19.c2), pour que les fins d'animation tombent au tick de l'original. Le signal de fin de boucle
    reste rattaché à E19.i, sauf si E19.c2 l'apporte (décision au plan d'E19.c2).

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
| **E19.a** ✅ | Entité de contexte (`0x42`, `0x43`, et tous les opcodes sur l'entité logique), `0x59`, garde de boucle (D-E19-3), support des arcs | A0, A0b, A1 | Le capitaine sort par l'escalier et réapparaît en pièce B ; sommeil, puis 476 |
| E19.a2 ✅ | Moteur : sur le champ de cellules, un pas bloqué avance jusqu'au contact (D-E19-8) ; épingles et traces de référence du héros re-mesurées ; la cabine testée avec un vrai contrôleur | cabine seule, A1c | La cabine : Alundra s'endort, puis la 476 se charge |
| E19.a3 ✅ | DLL : `ForceAdjusted` ne se lève qu'au tick sans avance, comme le binaire (D-E19-12) ; épingles du héros re-mesurées | marin 12 de la 389 | Le marin 12 rejoint sa place en fin d'intro |
| E19.b 🧪 | Carte 476 : `0xC4` sans nom (D-E19-5), `0x8A` (bloc caméra), `0x4C` gardé pour la machine à écrire | A2, A4 | La vision de Lars et Melzas jusqu'à 478, puis jusqu'à 392 |
| E19.c1 | Cartes 478 et 416 : `0x0B` avec détour (D-E19-6), `0x1C`/`0x1D` (compteur du binaire, Chain et Hold), `0x5E`, `0x08`, `0x0C`, `0x3A`, `0x89`, `0x73`/`0x74` ; arcs en vrais préfabs (D-E19-14) | A3, A7, A4p | La vision de 478 va au bout ; la plage 416 mène à la 163 |
| E19.c2 | Moteur : horloge d'animation exacte en ticks (D-E19-16), pilotée par la DLL à chaque tick logique ; garde de `0x1C` sous rattrapage | A3, tests moteur | Les animations au tick de l'original |
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

- **Ordre** : E19.a → E19.b → E19.c1 → E19.c2 → E19.d → E19.e, puis E19.f. Les tranches de phase 2
  viennent ensuite, dans l'ordre que l'auteur choisira. E19.c1 passe avant E19.c2 : elle débloque
  l'histoire, et l'horloge exacte ne change pas le mouvement.
- **Dépendances** : E19.b et E19.d ont besoin de l'entité de contexte d'E19.a. E19.c1 a besoin de la
  garde, pour que ses tests ne figent pas si un opcode reste non porté. E19.c2 a besoin de `0x1C` et
  du pont des fins d'animation d'E19.c1.
- **Tests de ces tranches** : depuis E19.c1, les arcs peuvent charger les vrais préfabs (D-E19-14,
  O-E19-3 tranché).
- **Mise à jour de ce plan** : chaque tranche de la phase 1 remet au §0.2 ce qu'elle a mesuré en
  vrai, puis fait détailler, relire et approuver la tranche suivante.

### 1.2 E19.a — Entité de contexte, `0x59` et garde de boucle ✅ (T1 à T6 faites et vérifiées CONFIRMED le 2026-09-29 ; recette en jeu T7 validée le 2026-10-01, après E19.a2 et E19.a3)

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

- ✅ **T1 — L'entité logique, instruction par instruction.** *(fait le 2026-09-29 : `RunOneScriptCall` résout `LogicEntity ?? owner` avant chaque opcode et `Dispatch` reçoit l'entité logique et l'owner ; `Clone` ne copie plus le contexte ; commentaires, `docs/intro-roadmap.md` et `EntitySearchService` corrigés ; 7 tests unitaires, `AlundraEventProgramRunnerLogicEntityTests`. Les 13 opcodes de champs et les 16 de référence agissent sans autre modification de cas ; aucun cas ne lit l'état du programme sur l'entité.)*
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
- ✅ **T2 — `0x42` et `0x43`** (`0x8003E808`, `0x8003E81C`). *(fait le 2026-09-29 : cas `0x42` et `0x43` de `Dispatch`, taille 1 et 2 ; 7 tests unitaires ajoutés à `AlundraEventProgramRunnerLogicEntityTests` (14 au total avec T1) ; `IntroTraceHarnessTests.ImplementedOpcodes` gagne `0x42` et `0x43`, annexes de trace régénérées sans aucun écart (la 389 n'a aucun site) ; commentaire du test Cave 140 mis à jour. Le contexte reste sur l'owner à travers un Break et d'un créneau à l'autre.)*
  - `0x42` : `owner.LogicEntity = PlayerEntity`. Rend 1. Sans héros (contexte dégradé), rien ne
    change, et le cas passe par `LogDegradedNoPlayerOpcodeOnce`, comme `0x3B`, `0x3E` et `0x53`.
  - `0x43` : la recherche `v1` prend l'entité logique pour référence (`EntitySearchService`).
    - Sans résultat : `Result = 0`, contexte inchangé.
    - Sinon : `owner.LogicEntity` prend le **dernier** résultat, et `Result = 1`.
    - Rend 2 dans les deux cas.
    - `0x43 [0x80]` reste un changement sans effet sur le contexte, avec `Result = 1`, comme le
      binaire.
- ✅ **T3 — `0x59`** (`0x8003EE8C`, taille 3) : pour chaque entité trouvée par `v1` (référence :
  l'entité logique), `TargetAnimationId = v2`. Rend 3. Ce cas couvre 669 sites « héros au repos »
  `[129,0]`, dont 390 `@515` et 389 `@1369`.
  *(fait le 2026-09-29 : cas `0x59` de `Dispatch` ; 3 tests unitaires (17 au total dans `AlundraEventProgramRunnerLogicEntityTests`) ; `ImplementedOpcodes` gagne `0x59`, `docs/intro-programs-389.txt` régénéré : seul le libellé de `0x59 @1369` change, `docs/intro-trace-389.txt` et les points épinglés de l'intro ne bougent pas.)*
- ✅ **T4 — Garde de boucle** (D-E19-3). *(fait le 2026-09-29 : `ProductionLoopBudget` = 1024, budget `MaxIterationsPerCall ?? ProductionLoopBudget` ; le rapport de trace porte `Codes[CodeIndex]` ; un avertissement `loop guard` par (owner, créneau, index du programme du créneau) ; documentation de `MaxIterationsPerCall`, de `LoopBudgetExceeded` et de `docs/intro-roadmap.md` mise à jour ; 6 tests unitaires (23 au total dans `AlundraEventProgramRunnerLogicEntityTests`). Piège noté : un `0x37` placé au pc 0 d'une boucle ne suspend pas (il lit `Parameters[1] == CodeIndex` comme une ré-entrée). Ce n'est **pas** fidèle à l'original, correction de la vérification : le binaire compare la clé au pointeur de pc (`0x8003E3DC`), jamais nul. L'écart vient de la DLL, qui compare un index au lieu d'un pointeur ; il est inatteignable sur les vraies données, aucun programme ne commençant à l'index 0.)*
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
- ✅ **T5 — Support d'arcs et tests** (§1.3). *(fait le 2026-09-29 : `AlundraArcSupport.cs` (`ArcRun`, `ArcSpec`) et `AlundraShipArcTests.cs` (A0, A0b, A1) ; les tests unitaires sont dans `AlundraEventProgramRunnerLogicEntityTests` (T1 à T4, 23 tests au total). Constat rouge d'A0 écrit avant T1 : sur le code d'avant, échec à la limite de 800 images en nommant `slot 1 program @452: last 0x1E @540`, avec `0x59 @515` et `0x43 @534` sautés, sans figer la suite. Après T1 à T3, A0, A0b et A1 passent, chacun avec son signal de fin prouvé par la trace.)*
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
- ✅ **T6 — Docs.** Mettre à jour ce plan (statuts, faits mesurés) et `plan-conversion-totale.md`
  (ligne E19). L'ADR-0015 est déjà écrite. *(fait le 2026-09-29 : statuts, vérification et
  dispositions ci-dessous, ligne E19 du plan maître.)*
- ✅ **T7 — Recette en jeu (auteur).** *(validée par l'auteur : points 1 à 3 le 2026-09-29 ; point 4, la cabine, le 2026-10-01, après E19.a2 et E19.a3.)* Lancer le jeu hors de l'app Claude, en Debug.
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

#### Recette en jeu T7 (auteur, 2026-09-29)

- **Points 1 à 3 validés** : le capitaine monte l'escalier et disparaît dans le trou, la main revient, puis
  on le retrouve en pièce B.
- **O-E19-1 réglé** : le journal de la partie montre le passage du trou (portail 5, même carte), qui
  charge la pièce B.
- **Point 4 bloqué** : dans la cabine, Alundra reste à côté de son lit, et la 476 ne se charge pas.

Diagnostic, reproduit en rejouant la vraie cabine avec le vrai contrôleur de collision du héros (test
temporaire, non commité) :

- **Les rails.** Les quinze `0x54` de B2 posent des « rails » (`W |= 1`) qui bloquent le héros. Il porte
  ClassB, donc son masque vaut `0x41`, comme dans `GetCollisionFlagsWithPlayer` (`0x80037488`).
  Ensuite, B2 fait marcher le héros de 80, 48, 80 puis 32 px, avec des attentes `0x1E`.
- **La seconde marche nord** finit contre la butée `(42,12)` :
  - l'original avance le pas bloqué jusqu'au contact en coupant la force en deux
    (`ComputeXYPosition`, `0x80037730`), et la marche mesure 80 px ;
  - le contrôleur du moteur rejette le pas bloqué en entier (champ de cellules, « no partial
    displacement »). Alundra s'arrête à y = 216,34, soit 78,8 px, et `0x1E @658` attend indéfiniment.
- **Preuve.** En posant Alundra au point de contact de l'original (y = 215,0), toute la fin de B2 se
  joue : il entre dans le lit, s'endort, puis le `0x53 @688` part vers 476.
- **Ce que les tests d'E19.a ne pouvaient pas voir** : leur montage fait marcher des entités nues, sans
  collision.

Décisions de l'auteur (2026-09-29) :
- **D-E19-8** — le défaut se corrige dans le moteur. Sur le champ de cellules, un pas bloqué avance
  jusqu'au contact, sans marge sur la grille, comme les contrôleurs modernes et comme l'original. Nouvelle
  tranche **E19.a2**.
- **D-E19-9** — le glissement le long d'un mur quand un seul coin touche (`didAdjustForObstacle`) vient
  plus tard, dans E19.h.

E19.a reste 🧪 jusqu'à ce que la recette T7 repasse après E19.a2.

#### Vérification d'E19.a (2026-09-29)

Commits `cab5e4c` (T1), `ef8e509` (T2), `08f5a40` (T3), `55af880` (T4) et `db59cc9` (T5).

- **Verifier frais : CONFIRMED** sur 1 à 4, avec deux remarques P4.
  - Build Release sans erreur.
  - `Alundra.Tests` : 1912 sur 1912 en Release (1886 avant la tranche : 23 tests unitaires et 3 arcs
    ajoutés). Tests du convertisseur : 400 sur 400.
  - Les arcs chargent vraiment la 390.
  - Seul le libellé de `0x59 @1369` change dans `docs/intro-programs-389.txt`. `docs/intro-trace-389.txt`
    est inchangé : aucun point épinglé de l'intro n'a bougé.
- **Constat rouge d'A0**, sur le code d'avant T1 : échec à la limite de 800 images en 0,6 s, qui
  nomme `0x1E @540`, avec `0x59 @515` et `0x43 @534` sautés. Après T1 seul, A0 restait rouge. Après
  T2, il passait les assertions de marche et n'échouait plus que sur `0x59 @515`. Il est vert après
  T3.
- **Contradicteur en lecture seule** : aucun P0, P1 ni P2. Il a vérifié dans le binaire les
  écritures de `0x42`, `0x43` et `0x59` et le contrat des gestionnaires. Dispositions (règle : un P3
  ou un P4 est reporté ou signalé, jamais corrigé sur un candidat CONFIRMED) :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | La remise à zéro hors zone de `RunMapEventsPass` écrit maintenant dans l'entité d'un événement recontextualisé (`AlundraWorldProxy.cs:2312-2324`) et ne remet pas son contexte au héros. Sans effet aujourd'hui : ces champs n'ont aucun lecteur, et `Sp` est réécrit à chaque lecture. | P3, introduit | **Reporté à E19.j**, qui porte le réarmement du binaire (`0x8003C7F0`). |
  | Le constructeur d'`ArcRun` modifie l'état global avant des étapes qui peuvent échouer, et `Dispose` n'est alors jamais appelé : un arc qui échoue peut salir les tests suivants. | P3, introduit | **Reporté à E19.b**, à corriger avant d'ajouter les arcs A2 et A4 au support. |
  | Avec la garde, une boucle sans suspension qui a des effets (son, demi-tour, apparition) les répète environ 1024/N fois par image au lieu de figer le jeu. Exemple hors chaîne : Overworld 2,3-12, `C[2] @736`. | P4 | Signalé : c'est l'écart D-E19-3. Porter les opcodes qui suspendent (E19.c) retire ces boucles. |
  | Note de T4 et commentaire du test `LoopGuard_ALoopWithAWait` : « fidèle à l'original » est faux (index au lieu de pointeur). | P4 | Note de T4 corrigée ici. Commentaire du test reporté à E19.b. |
  | Commentaires et docs encore imprécis : commentaires de `0x3B` et `0x3E` (« entité qui exécute », `AlundraEventProgramRunner.cs:1176`, `:1295-1298`) ; `EntitySearchService.cs:31` et le nom `ownerEntity` ; `docs/intro-roadmap.md` vers `:101` (`EventTrigger = i`) ; la phrase de l'ADR-0015 « les programmes sans `0x42`/`0x43` se comportent comme avant », qui oublie `0x59` et la garde ; la doc de `ProductionLoopBudget` sur les créneaux A, D, E et F ; le commentaire des 23 opcodes d'`IntroTraceHarnessTests.ImplementedOpcodes`, déplacé par l'insertion. | P4 | Reportés à E19.b, sauf l'ADR, qui ne se réécrit pas : c'est ce plan qui fait foi (risques ci-dessous). |
  | `_loggedLoopGuards` garde une référence aux owners tant que le runner vit. | P4 | Reporté (borné par le nombre d'entités d'une carte). |
  | Le message dégradé de `0x42` sans héros dit « `Result = 0` », mais `0x42` n'écrit pas `Result`. | P4 | Reporté à E19.b. |

#### Risques d'E19.a

- **Le capitaine bloqué en production.** Le montage fait marcher des entités nues, sans collision.
  En jeu, le capitaine a un contrôleur : s'il ne franchit pas la rampe ou la rangée du trou
  (walkability 4), `0x1E` attend toujours, malgré son détour E4.d. La recette le montre. Un tel
  blocage se corrige à la racine (D-E19-6), dans une tâche de diagnostic ajoutée au plan.
- **Programmes sans `0x42`/`0x43`** : ils ne changent pas d'entité, mais deux autres changements
  les touchent. Les 1085 sites de `0x59` appliquent maintenant leur animation, et un programme qui
  dépasse 1024 opcodes par appel est coupé par la garde. Cette précision complète la phrase de
  l'ADR-0015 qui dit qu'ils « se comportent comme avant ».
- **Changements visibles hors de la chaîne** : 98 cartes utilisent `0x42`/`0x43`, et 74
  enregistrements de 38 cartes ont un contexte qui persiste d'un créneau à l'autre. C'est fidèle au
  binaire ; la recette ne couvre que la chaîne.
- **Contexte périmé du héros** : après la passe des événements de carte, `player.LogicEntity` garde la
  valeur du dernier événement, comme `hero+0x230` dans l'original. Aucun autre appel de production ne
  fait exécuter un script au héros. Un test qui le fait doit remettre le contexte à zéro.
- La garde coupe les créneaux A, D, E et F au lieu de les suspendre. Un programme A coupé ne reprend
  jamais, car A n'est proposé qu'une fois par entité ; D, E et F repartent du début à leur
  déclenchement suivant. C'est journalisé, et préférable
  à un jeu figé.

### 1.2b E19.a2 — Un pas bloqué avance jusqu'au contact ✅ (T0 à T4 faites le 2026-09-29 ; recette en jeu T5 validée le 2026-10-01, après E19.a3)

**But.** Dans la cabine de la 390, Alundra finit ses quatre marches, s'endort, et la 476 se charge (point 4
de la recette T7). Plus généralement : sur le champ de cellules, un pas bloqué avance jusqu'au contact,
comme dans l'original, au lieu d'être rejeté en entier.

**Décisions de l'auteur (2026-09-29)**, consignées dans l'ADR-0016 :
- **D-E19-8** — correction dans le moteur. Sur le champ de cellules, un pas bloqué avance jusqu'au contact,
  sans marge sur la grille.
- **D-E19-9** — le glissement le long d'un mur quand un seul coin touche vient dans E19.h.
- **D-E19-10** — la DLL garde sa règle pour `ForceAdjusted` : le pas a manqué plus de 0,01 px. Le drapeau
  se lève donc au tick du contact, un tick avant l'original, qui ne le lève qu'au tick sans aucune avance
  (`0x80037d54`, sauté par la garde de `0x800379a4`). L'écart est consigné ; E19.h aligne le drapeau sur
  le binaire, avec le glissement.
- **D-E19-11** — dans le moteur, `H1Curtailed`/`H2Curtailed` signifient désormais « pas raccourci sur cet
  axe », qu'il reste nul ou non.

**Faits établis** (découverte en lecture seule du 2026-09-29, trois volets contre-vérifiés ; émulations en
flottant 32 bits dans le scratchpad `e19a2/`) :

- **L'original** (`ComputeXYPosition`, `0x80037730`, relu dans le binaire) coupe en deux la force entière
  (dx et dy ensemble, décalage arithmétique, -1 ramené à 0). Il réessaie depuis la dernière position
  acceptée et cumule les demi-pas acceptés. Dans la cabine, il finit exactement en y = 215,0 **[binaire]**.
- **Le moteur** rejette l'axe bloqué en entier et n'applique aucune marge sur le champ
  (`CharacterControllerComponent.cs:1102-1194`). Le balayage rigide, lui, va déjà au contact.
- **Les marches de la cabine sont bornées par des murs** :
  - la première marche nord finit à 80 px (y ≤ 296), au plus tard contre la cellule (44,17), de hauteur 5,
    dont le contact est y = 295,0. La marche vers l'ouest longe la même rangée. La seconde marche nord part
    donc de y dans [295,0 ; 296,0] ;
  - elle finit contre le rail (42,12), dont le contact est y = 215,0 ;
  - avec un contact exact, elle mesure donc au moins 80 px, quelle que soit la cadence d'image.
- **La précision compte.** Une marge de 0,16 px suffit à reproduire le blocage (hero `SkinWidth` = 0,5).
  Le contact doit donc être exact : bisection sur la position de la racine, 24 itérations. Une bisection
  sur le centre peut finir 1 à 2 ULP dans le mur.
- **Portée.** Seule Alundra installe un champ. Le héros et chaque PNJ à contrôleur s'arrêtent désormais
  jusqu'à un pas plus près des murs (1,25 à 2,44 px), ce qui est plus proche de l'original.
- **Ce qui bouge côté DLL** :
  - `AlundraCharacterControllerAdoptionTests.cs` : `Mask_ClassBMaskOnEqualHeightCells_BlocksTheMove`
    (`:344`, attendu (565,632,80)) et `Cliff_HeightAboveStepHeight_BlocksTheMoveRegardlessOfMask`
    (`:392`, attendu (397,264,0)) ;
  - l'épingle de `HeroTraceHarnessTests.cs:748` (36956160 → 36831232 ; la première image au mur reste 98) ;
  - les quatre traces `docs/hero-trace-389-*.txt`, colonne `posX` à partir de l'image 98 (spawn) et 39
    (highground) ;
  - les commentaires `AlundraEntityScriptProxy.cs:1762-1770`, `:1881-1887` et la doc de `ForceAdjusted`
    (`:175-190`).
- **Ce qui ne bouge pas** : les tests de PNJ contre un mur (ils n'épinglent pas de position), les arcs
  (entités nues), l'intro (sa propre physique).
- **Pourquoi E19.a ne l'a pas vu** : ses arcs font marcher un héros nu, sans contrôleur. Le test du parcours
  doit donc aussi exister avec un vrai contrôleur.

**Tâches.**

- ✅ **T0 — Preuve rouge d'A1c, avant tout changement du moteur** : écrire le mode « vrai contrôleur »
  d'`ArcRun` et A1c (détail en T3), les lancer sur le sous-module en `a550859f`, consigner l'échec et
  le `HEAD`, et ne rien committer.
  *(fait le 2026-09-29 : `git -C CasaEngineMonogame rev-parse HEAD` = `a550859fd1c25fcfa74d13cc09ec2afec1a68949`. A1c échoue à la limite de 900 images, à l'image 899, en nommant `0x1E @658` : « arc A1c did not reach its end signal (B2 executes 0x53 @688 towards map 476 (the walk of 0x1E @658 ends at its contact)) within 900 frames. Last instruction of each program: [... slot 1 program @552: last 0x1E @658 (Implemented, frame 899); ...]. Skipped or exceeded: []. » Rien n'a été commité à ce stade ; les fichiers le sont en T3.)*
- ✅ **T1 — Moteur** : plan `CasaEngineMonogame/ai-agent/tasks/field-move-to-contact-tasks.md` (T0.1 branche,
  plan et ADR-0045 du moteur ; T1.1 bisection et tests ; T1.2 docs), sur la branche moteur
  `chantier/field-move-to-contact`.
  *(fait le 2026-09-29 : commits moteur `d51089f5` (T0.1), `62ac8431` (T1.1), `25f8385c` (T1.2) ; `CasaEngine.Tests` 2375/2375, aucun test hors de `CharacterControllerFieldAwareMoverTests` n'a changé de résultat.)*
- ✅ **T2 — Pointeur et épingles du parent**, en un seul commit compilable :
  *(fait le 2026-09-29 : pointeur sur `25f8385c` ; les tests dépendant du tick de `ForceAdjusted` (`Walk0x1E_RealWall…`, `Walk0x1F_RealWall…`, `ForceAdjusted_ClearedEachFrameTop…`, `AlundraLadderClimbTests`) passent sans changement, 20/20 ; traces : seule la colonne `posX` change, dès l'image 98 (spawn, 36956160 → 36831232, 112 lignes) et 39 (highground, 27506688 → 27394048, 79 lignes) ; épingle `:748` à 36831232 et nouvelle épingle highground 27394048 (image 39) ; les deux tests d'adoption à (565,632,80) et (397,264,0), renommés `…StopsAtContact` ; commentaires de `AlundraEntityScriptProxy.cs` dont l'écart D-E19-10. Aucune autre épingle n'a bougé : `Alundra.Tests` 1920/1920, en comptant A1c et les 7 cas de diagnostic non encore commités (T3).)*
  - le pointeur du sous-module suit la branche moteur ;
  - relancer `HeroTraceHarnessTests`. Il réécrit les quatre traces, qui ne sont comparées à rien par le
    test : le `git diff` est donc la seule garde. Vérifier, sans tenir compte des fins de ligne, qu'il ne
    touche que la colonne `posX` à partir des images 98 et 39, puis mettre l'épingle `:748` à la valeur
    mesurée (attendu 36831232) ;
  - lancer d'abord les tests qui dépendent du tick où `ForceAdjusted` se lève pour la première fois, sans
    épingler de position : dans `AlundraNpcCharacterControllerMoverTests`, les `Walk0x1E_RealWall…`, les
    `Walk0x1F_RealWall…` (`:1329`, et son jumeau à dt 1/123, `:2654`) et
    `ForceAdjusted_ClearedEachFrameTop…` (`:1449`) ; puis les tests `ProductionCallSite` de
    `AlundraLadderClimbTests` ;
  - ajouter une épingle chiffrée pour le premier contact de la trace highground (attendu 27394048) ;
  - mettre les deux tests d'adoption à (565,632,80) et (397,264,0), et les renommer (« StopsAtContact ») ;
  - mettre à jour les commentaires de `AlundraEntityScriptProxy.cs` cités plus haut, dont l'écart
    D-E19-10 dans la doc de `ForceAdjusted` ;
  - toute autre épingle qui bouge est un arrêt : elle se signale, elle ne s'adapte pas en silence.
- ✅ **T3 — La cabine avec un vrai contrôleur, en deux niveaux de test** :
  *(fait le 2026-09-29 : `AlundraCabinWalkTests` (2 cas, dt 0,02 et 1/60, rails posés comme B2, masque `0x41` vérifié, `PosY == 215 << 16` après la troisième marche, les quatre marches atteignent leur distance) ; `ArcSpec.RealController` et le mode « vrai contrôleur » d'`ArcRun` (politique `Runtime`, `PhysicsWorld`, héros à contrôleur, `ResyncControllerFromFlags`, `PushLogicalPositionToRoot`, `world.Update` seul) ; A1c vert à dt 0,02 dans la limite de 900 images ; `LoadHeroControllerSettings` et `LoadHeroHeader` dans `HeroWorldFixture` ; P3 du constructeur d'`ArcRun` corrigé (nettoyage sur échec) avec un test dédié, vu rouge sans le correctif ; les deux diagnostics `Zz*` supprimés. Mutation réelle côté moteur (rejet entier remis, code restauré à l'octet près) : A1c échoue en nommant `0x1E @658`, le test de la cabine attend 14090240 et lit 14098432, les épingles des traces et des deux tests d'adoption échouent. `Alundra.Tests` 1916/1916.)*
  - **cabine seule** : un test durable sur les vraies cellules de la 390, avec l'en-tête et les réglages
    réels du héros, le masque `0x41` vérifié, et les quatre marches.
    - Les rails sont posés comme B2 les pose, par `records.Walkability[y * largeur + x] |= 1` sur le
      tableau aliasé. Ce sont les quinze cellules des `0x54` de B2 (`@558` à `@628`) : (42,12),
      (43,13), (43,14), (43,15), (43,16), (43,17), (41,15), (41,16), (41,17), (41,18), (41,19), (42,19),
      (43,19), (43,20), (45,20).
    - Le test vérifie `PosY == 215 << 16` à la fin de la troisième marche. C'est une valeur calculée
      pour cette trajectoire, dont le dernier tick franchit le rail. Il vérifie aussi que chaque marche
      atteint sa distance, et que la quatrième s'achève, à dt 0,02 et 1/60 : les ticks logiques étant à
      50 Hz, les deux cadences donnent la même suite de positions.
    - Les aides dupliquées (`LoadHeroControllerSettings`, `LoadHeroHeader`) rejoignent
      `HeroWorldFixture` ;
  - **arc A1c** : `ArcSpec` gagne un indicateur de mode, et `ArcRun` un mode « vrai contrôleur ». Le
    diagnostic temporaire `ZzDiagE19CabinProductionTests.cs` montre la recette qui fonctionne :
    - avant `InitializeWithWorld` : la politique d'exécution du jeu (`GameplayExecutionPolicies.Runtime`,
      sans quoi `World.Update` ne fait pas tourner les scripts des entités) et un `PhysicsWorld` ;
    - un héros propre à ce mode : `CharacterControllerComponent` réglé comme l'export, puis
      `world.AddEntity`. `SaveGameDirectorTestSupport.AddHeroPawn` ne convient pas : il ne fait que poser
      `Entity.World` par réflexion, sans ajouter l'entité au monde, donc sans l'inscrire au système de
      mouvement ;
    - l'en-tête du héros, puis `ResyncControllerFromFlags`, avec le masque `0x41` vérifié ;
    - `PushLogicalPositionToRoot` après le placement ;
    - chaque image = `world.Update`, puis `Proxy.Update`, **sans** la boucle manuelle d'`OneFrame` sur les
      entités : le monde met déjà à jour les PNJ, ajoutés par `world.AddEntity`, et la boucle les mettrait
      à jour deux fois. Le mode par défaut ne change pas : A0, A0b et A1 restent tels quels.

    A1c part de (44,23,4) avec `G866`, `G869`, `G870` et `G871`, ferme le dialogue de B3 et exécute
    `0x53 @688`, en moins de 900 images à dt 0,02 (le diagnostic l'atteint vers l'image 330).
    - **Preuve sur l'ancien moteur** : `Alundra.Tests` se construit contre l'arbre de travail du
      sous-module, pas contre le pointeur du parent. Le mode « vrai contrôleur » d'`ArcRun` et A1c sont
      donc écrits et lancés **avant la première modification du sous-module**, c'est-à-dire avant le T0.1
      du moteur, sans rien committer. Il doit échouer dans sa limite en nommant `0x1E @658`. Le constat
      consigné porte, à côté du message d'échec, la sortie de `git -C CasaEngineMonogame rev-parse HEAD`,
      qui doit valoir `a550859f…`. Le sous-module n'est jamais remis en arrière par `checkout`.
    - Ces fichiers restent non commités jusqu'à T3, où ils sont commités après T2 ;
  - le P3 reporté du constructeur d'`ArcRun` (état global sali si une étape échoue) se corrige ici, parce
    que le mode « vrai contrôleur » rend un échec dans le constructeur plus probable ;
  - les deux diagnostics temporaires `Alundra.Tests/ZzDiagE19Cabin*.cs`, jamais commités, sont supprimés.
- ✅ **T4 — Docs du parent** :
  *(fait le 2026-09-29 : lignes « Mise à jour 2026-09-29 » dans les quatre plans cités, sans les réécrire ; ce plan ; ligne E19 du plan maître.)*
  - l'ADR-0016 (déjà écrite) ;
  - une ligne « Mise à jour 2026-09-29 » dans `docs/plan-e3-collisions.md` (C5),
    `docs/plan-moteur-character-motion.md` (sens des drapeaux M2), `docs/plan-e4-deplacement-scripte.md`
    (D5) et `docs/plan-oracle-heros.md` (`:227`, ancienne valeur 36956160), sans réécrire ces plans ;
  - ce plan et la ligne E19 du plan maître.
- ✅ **T5 — Recette en jeu (auteur)** *(validée le 2026-10-01 : la cabine passe dès le 2026-09-29 ; le marin 12 de la 389, déplacé par le contact, est corrigé par E19.a3)* : rejouer T7 d'E19.a. Le capitaine sort, on le retrouve en pièce B,
  Alundra s'endort dans la cabine, et la 476 se charge, puis s'arrête au premier `0xC4`, ce qui est attendu
  jusqu'à E19.b.

**Acceptation d'E19.a2.**
1. Moteur : T1.1 et T1.2 faites. Le test de la cabine rend y == 215,0 exactement. La suite du moteur
   passe à 0 échec.
2. Parent : les deux niveaux de test de T3 passent. A1c échouait sur l'ancien pointeur, nommant
   `0x1E @658`.
3. Parent : `Alundra.Tests` et les tests du convertisseur passent à 0 échec. Les seules épingles qui
   bougent sont celles de T2 ; les quatre traces ne changent que dans la colonne `posX`, aux images
   annoncées.
4. Un verifier frais rend CONFIRMED sur 1 à 3.
5. La recette T5 de l'auteur. Tant qu'elle n'est pas faite, E19.a et E19.a2 restent 🧪.

**Vérification d'E19.a2 (2026-09-29).**
- **Commits.** Moteur `d51089f5`, `62ac8431`, `25f8385c`, puis la clôture de son plan `7fae9959`.
  Parent `c48cf5e` (T2), `386bc60` (T3), `d174586` (T4).
- **Preuve rouge.** Sur le sous-module en `a550859f…`, A1c échoue à sa limite de 900 images en nommant
  `0x1E @658`.
- **Tests.** `CasaEngine.Tests` 2375/2375. `Alundra.Tests` 1916/1916 en Debug et en Release. Tests du
  convertisseur : 400/400.
- **Traces.** Les quatre traces ne changent que dans la colonne `posX` : images 98 à 209 (spawn) et
  39 à 117 (highground).
- **Verifier frais : CONFIRMED**, sans constat.
- **Contradicteur en lecture seule** : aucun P0 à P3. Dispositions de ses P4 (signalés, jamais corrigés
  sur un candidat CONFIRMED) :

  | Constat P4 | Disposition |
  |---|---|
  | Le contrôle final d'`AdvanceBlockedAxisToContact` ne peut jamais reculer : c'est une garde morte, décrite comme active. | Signalé dans le plan moteur ; reporté. |
  | La bisection suppose le blocage monotone : un pas de plus d'environ 45 px pourrait traverser un obstacle fin. Inatteignable avec les pas d'Alundra, mais absent de l'ADR-0045. | Signalé ; à écrire dans une prochaine ADR du moteur si des pas longs apparaissent. |
  | La correction du constructeur d'`ArcRun` est incomplète : `FindProjectRoot` et l'option de caméra sont hors du `try`, et le test ne vérifie que `ProjectPath`. | Reporté à E19.b, qui étend le support d'arcs. |
  | Le test « cabine seule » ne reproduit pas le blocage ; seule l'épingle `PosY == 215 << 16` le distingue. C'est A1c qui reproduit le blocage. | Accepté : A1c porte la preuve. |
  | Les aides `LoadHeroControllerSettings`/`LoadHeroHeader` ne sont pas regroupées dans `HeroWorldFixture` comme prévu ; des copies restent. | Reporté (hygiène). |
  | Les deux tests d'adoption re-mesurés rendent vert en silence quand l'export manque. Ce défaut existait avant. | Reporté (hygiène). |
  | Un reste de moins de 1e-3 px est abandonné par le balayage. | Limite acceptée P5, écrite dans l'ADR-0045. |
  | L'avance d'un tick de `ForceAdjusted` (D-E19-10) ne vient pas du contact : l'ancien rejet la produisait déjà ; les traces gardent les mêmes images. | Précision notée ici ; l'ADR-0016 n'est pas réécrite. |
  | Le pointeur du parent vise un commit moteur hors de `main`. | Ordre de merge : moteur d'abord, puis parent, avec le pointeur. |

**Risques.**
- **Changements ailleurs que dans la cabine.** Le héros et les PNJ s'arrêtent désormais plus près des murs.
  Les portails et les zones testés par case peuvent se déclencher un pas plus tôt, ce qui est plus proche
  de l'original.
- **Escaliers.** Sur un escalier, l'avance jusqu'à la limite de `StepHeight` peut changer l'allure. La
  trace highground le montrera (T2).
- **Pas de glissement le long des murs** (D-E19-9). Une marche en diagonale contre un coin reste différente
  de l'original jusqu'à E19.h.
- **Marge de la cabine.** Elle ne tient que si le contact est exact. Le test de la cabine l'épingle
  (y == 215,0), pas seulement « au moins 80 px ».

**Revues** : plan-verifier sur cette section et sur le plan moteur, avant approbation ; verifier frais
après exécution. Budgets et arrêts : ceux du §5.

**Relectures du 2026-09-29.**
- **Plan-verifier** (parent `54a99b5` et plan moteur) : **READY**.
- **Audit des citations et de la faisabilité**, en parallèle : aucun P1. Toutes les valeurs épinglées sont
  recalculées depuis la géométrie et les données : (565,632,80), (397,264,0), (56,24,0), 215,0,
  36831232, 27394048.
- **Trois P2 intégrés** :
  - la liste des quinze rails ;
  - la recette du mode « vrai contrôleur » d'`ArcRun` ;
  - la géométrie du test moteur aux frontières de puissance de deux (plan moteur).
- **P3 intégrés** : citations décalées, ordre de la preuve sur l'ancien moteur, tests à lancer d'abord,
  garde des traces par le `git diff`, contrat de la bisection, avance résiduelle inférieure à 1e-3 px
  (plan moteur).
- **Relecture neuve de clôture** (`8fec476`) : **REVISE**, deux P2, tous deux corrigés (FIX) en
  appliquant mot pour mot les révisions minimales demandées :
  - la preuve rouge d'A1c se fait avant toute modification du sous-module (nouvelle tâche T0), avec
    le `HEAD` du sous-module consigné ;
  - la grille du test aux puissances de deux doit contenir le coin lointain de l'empreinte de départ
    (65 rangées pour la seconde fenêtre).

  **Le plafond de relecture est atteint** : c'était la relecture de clôture, cette version n'est pas
  relue à nouveau. La décision d'approuver en l'état, ou de demander une relecture de plus, revient à
  l'auteur.

### 1.2c E19.a3 — `ForceAdjusted` comme dans le binaire ✅ (T0 à T3 faites le 2026-10-01 ; recette en jeu T4 validée le même jour)

**Origine : recette T5 d'E19.a2 (auteur).** La cabine passe : Alundra se couche et la suite du script
part. Mais à la fin de l'intro de la 389, le marin 12 s'arrête avant sa place : il finit 72 px trop à
l'est.

**Diagnostic**, reproduit avec le vrai contrôleur du marin sur les vraies cellules de la 389 (diagnostic
temporaire `Alundra.Tests/ZzDiagE19SailorTests.cs`, non commité) :

- Son programme C[12] (`@1438` à `@1494`) enchaîne huit marches : sud 16, ouest 120, sud 16, ouest 24,
  sud 96 en `0x1F`, est 120 en `0x1E`, puis sud 48 et ouest 72 en `0x1F`. Il part de (564, 672, 80).
- La marche sud 96 finit maintenant au contact exact, y = 794,0 ; avant E19.a2, en 793,25.
- La marche sud 48 part donc de 794,0. Son dernier pas bute sur le mur et est raccourci pour finir en
  842,0 : la distance de 48 est atteinte. Mais notre règle (manque de plus de 0,01 px) lève
  `ForceAdjusted` sur ce même tick.
- La marche ouest 72 commence dans le même tick. `0x1F` rend « fini » dès que `ForceAdjusted` vaut 1
  (`AlundraEventProgramRunner.cs:601`) : elle lit le drapeau resté du tick précédent et se termine sans
  un pas. Le marin reste en x = 540,875.
- Dans l'original, `ForceAdjusted` ne se lève qu'au tick où aucun demi-pas n'est accepté
  (`0x80037d54`, sauté par la garde de `0x800379a4`) **[binaire]**. Au tick du contact, le pas raccourci
  est accepté, le drapeau reste à 0, et la marche ouest part normalement.
- Avant E19.a2, le moteur rejetait le pas bloqué en entier : notre règle levait le drapeau au tick sans
  avance, comme l'original. C'est le contact, combiné à D-E19-10, qui l'a avancé d'un tick.

**Décision de l'auteur (2026-10-01)**, consignée dans l'ADR-0017, qui remplace D-E19-10 :
- **D-E19-12** — `ForceAdjusted` suit le binaire : il se lève quand un axe demandé (plus de 0,01 px)
  n'avance pas du tout (moins de 0,01 px), et non quand le pas n'est que raccourci. Le glissement le long
  des murs reste pour E19.h (D-E19-9).

**Tâches.**

- ✅ **T0 — Preuve rouge** :
  *(fait le 2026-10-01 : sur l'ancienne règle, le test échoue parce que la marche ouest 72 (`0x1F @1491`) se termine au tick où elle commence ; le marin reste en (540,875 ; 842,0).)*
  écrire le test durable du marin 12, d'après le diagnostic temporaire :
  - vrai contrôleur du banc 146 ;
  - vraies cellules et vraie grille de navigation de la 389 ;
  - en-tête réel du marin, masque `WalkabilityMaskFor(Flags)` ;
  - les codes réels de C[12] `@1438-1494`, suivis de `0xFF`.

  Les codes sont suivis de l'instruction `@1494` (`0x5B [128,0,64]`, qui arrête le marin), puis de `0xFF`.

  **Valeurs attendues, écrites avant le code** (calcul sur les cellules de la 389) :
  - la marche sud 48 finit contre la rangée 53, dont les cellules (22,53) et (23,53) sont des murs, au
    contact y = 842,0 (le bord lointain du marin, y + 6, touche la frontière 848) ;
  - la marche ouest 72 part de x = 540,875 sur la rangée 52, libre de x = 17 à 23 à la hauteur 8 du
    marin. Elle avance de 1,875 px par tick (vitesse 160 sur X) et se termine par la distance au 39e
    tick ;
  - position du marin à la fin de la marche ouest : **(467,75 ; 842,0)** exactement, lue à la fin de
    l'image du dernier pas vers l'ouest, c'est-à-dire juste avant l'image où `@1494` s'exécute ;
  - **correction du 2026-10-01**, après l'arrêt de l'exécution : la valeur attendue ne change pas, seul
    le moment de lecture est précisé. Au tick où la marche se termine, `@1494` tourne le marin vers le sud
    au repos, mais le mouvement de ce même tick s'applique encore avec la nouvelle direction : le marin
    descend de 1,25 px (y = 843,25 mesuré). Ce pas est consigné en O-E19-5 ;
  - **condition de fin** : le programme exécute `@1494` puis `0xFF` ;
  - **limite** : 450 images à dt 0,02 (le diagnostic atteint la fin de la marche sud 48 à l'image 313).
    Au-delà, le test échoue en nommant le dernier (pc, opcode) exécuté.

  Le lancer sur le code actuel : il doit échouer parce que la marche ouest 72 (`0x1F @1491`) se termine
  au tick même où elle commence, laissant le marin en x = 540,875. Consigner le constat, sans rien
  committer. Un écart avec ces valeurs après T1 est un arrêt : il ne s'épingle pas.
- ✅ **T1 — La règle** :
  *(fait le 2026-10-01 : règle `AxisMadeNoProgress`, docs de `ForceAdjusted` et de la méthode, 4 tests unitaires dans `AlundraNpcCharacterControllerMoverTests` ; commit `8a01fa1`.)*
  dans `AlundraEntityScriptProxy.MoveControllerAndPullPosition` (`:1792-1796`),
  `ForceAdjusted = 1` seulement si, sur un axe, |demandé| > 0,01 et |obtenu| ≤ 0,01.
  - Mettre à jour la doc de `ForceAdjusted` (`:168-196`, dont l'écart D-E19-10, maintenant levé) et celle
    de la méthode.
  - Tests unitaires : un pas raccourci mais avancé ne lève pas le drapeau ; un pas sans avance le lève ;
    les deux axes sont jugés séparément ; un reste de moins de 0,01 px compte comme « sans avance ».
- ✅ **T2 — Le marin 12 et les épingles** :
  *(fait le 2026-10-01 : le test `AlundraSailor12EndOfIntroTests` atteint `@1494` puis `0xFF` et lit (467,75 ; 842,0) à la fin de l'image qui précède celle de `@1494` ; traces du héros : seule la colonne `forceAdjusted` change, 1 → 0 à l'image 98 (spawn) et 39 (highground), un seul écart par fichier ; épingles 98 → 99 et 39 → 40, posX inchangée (36831232, 27394048) ; `Alundra.Tests` 1920/1920 avant ajout du test du marin, A1c et la cabine verts ; commits `8a01fa1` (règle, tests, traces, épingles) et `354b80a` (test du marin, diagnostic supprimé).)*
  - le test de T0 passe avec les valeurs écrites en T0 : le programme atteint `@1494` puis `0xFF` dans
    sa limite, et le marin finit en (467,75 ; 842,0) exactement ;
  - les traces du héros ne changent que dans la colonne `forceAdjusted`, à la première image de contact :
    98 devient 0 et le premier 1 passe à 99 (spawn) ; de même 39 devient 0 et le premier 1 passe à 40
    (highground) ;
  - épingles : `HeroTraceHarnessTests.cs:745` (98 → 99) et l'épingle highground (39 → 40). La position au
    premier drapeau ne change pas (36831232, 27394048) ;
  - lancer d'abord les tests qui dépendent du tick du premier `ForceAdjusted` (liste de T2 d'E19.a2). Un
    décalage d'exactement un tick, expliqué par la règle, s'épingle ; tout autre changement est un arrêt ;
  - A1c et le test de la cabine passent toujours.
- ✅ **T3 — Docs** *(fait le 2026-10-01)* : l'ADR-0017 (déjà écrite), la ligne de statut de l'ADR-0016, ce plan, et la ligne E19 du
  plan maître.
- ✅ **T4 — Recette en jeu (auteur)** *(validée le 2026-10-01)* : à la fin de l'intro de la 389, le marin 12 rejoint sa place (ta
  capture 2) ; puis la cabine, Alundra se couche, et la 476 se charge.

**Acceptation d'E19.a3.**
1. Le test du marin 12 échouait sur l'ancien code, avant T1 (marin laissé en x = 540,875). Après, il
   atteint `@1494` puis `0xFF` en moins de 450 images, et le marin est en (467,75 ; 842,0) à la fin de la
   marche ouest, juste avant l'image de `@1494`.

**Ordre des commits, précisé le 2026-10-01.** La règle de T1 fait bouger l'épingle des traces du héros
(98 → 99). La règle, ses tests unitaires, les traces régénérées et les deux épingles re-mesurées
partent donc dans un même commit, pour qu'aucun commit ne laisse un test rouge. Le test du marin 12 et
la suppression du diagnostic suivent dans le commit de T2.
2. Les traces du héros ne changent que dans la colonne `forceAdjusted`, aux deux images annoncées. Les
   seules épingles qui bougent sont celles de T2.
3. `Alundra.Tests` et les tests du convertisseur passent à 0 échec, A1c compris. Le moteur ne change pas.
4. Un verifier frais rend CONFIRMED sur 1 à 3.
5. La recette T4 de l'auteur.

**Risques.** Le drapeau se lève maintenant un tick plus tard qu'avant E19.a2 au contact. Tous ses
lecteurs le voient donc au même tick que l'original : `0x1F`, `0x24`, le détour E4.d, la désactivation
au choc et l'entrée sur une échelle. Les marches en diagonale contre un coin restent différentes de
l'original jusqu'au glissement (E19.h).

**Vérification d'E19.a3 (2026-10-01).**
- **Commits** : `8a01fa1` (règle, tests unitaires, traces et épingles), `354b80a` (test du marin 12),
  `eee8f76` (docs). Le pointeur du moteur ne change pas.
- **Verifier frais : CONFIRMED.** `Alundra.Tests` 1921/1921 en Release, convertisseur 400/400. Dans un
  arbre de travail jetable, avec l'ancienne règle remise :
  - le test du marin 12 échoue (x = 540,875) ;
  - 3 des 4 tests unitaires de la règle échouent.
- **Traces** : chaque fichier ne change que d'une ligne, dans la colonne `forceAdjusted` (images 98 et 39).
- **DLL déployée** : la dernière construction avait déployé la DLL Release, sans les touches de recette.
  La DLL Debug est redéployée dans `alundra-project/`.
- **Contradicteur en lecture seule** : aucun P0 à P2. Dispositions (signalé, jamais corrigé sur un
  candidat CONFIRMED) :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | La règle n'est jamais testée sur l'axe Y ni en sens négatif à travers le vrai contrôleur : retirer le test de Y laisserait tout vert. | P3 | Reporté : test à ajouter avec la prochaine tranche qui touche aux marches (E19.c). |
  | Le jugement par axe diffère encore du binaire sur une poussée en diagonale. L'original coupe le pas en deux puis glisse, et n'élève pas le drapeau si un demi-pas passe. | P3 | Connu : vient avec le glissement (E19.h, D-E19-9). |
  | La position finale réelle du marin (y = 843,25) n'est pas épinglée. | P3 | Accepté : point ouvert O-E19-5 (E19.c). |
  | Le commentaire d'en-tête du test du marin donne `@1487` pour la marche sud 48, qui est en `@1484`. | P4 | Reporté (hygiène). |
  | Textes en retard : `docs/plan-e4-deplacement-scripte.md:411` (drapeau « un tick avant »), `docs/plan-oracle-heros.md:233` (« première image au mur reste 98 »), le résumé de D-E19-10 au §0.1. | P4 | Reporté à la prochaine passe de docs (E19.b). |
  | L'ADR-0017 cite `0x24` parmi les lecteurs du drapeau, mais la DLL n'a pas encore ce cas. | P4 | Accepté : `0x24` arrive en E19.d. |
  | Au bord de l'epsilon, la nouvelle règle diffère de l'ancienne dans deux cas étroits : une demande entre 0,01 et 0,02 px, et un recul de plus de 0,01 px. | P4 | Accepté. |
  | La preuve rouge du test du marin n'est pas dans l'historique : la règle est commitée avant le test. | P4 | Accepté : le verifier l'a reproduite à part. |

### 1.2d E19.b — Carte 476 : la vision de Lars et Melzas 🧪 (code et arcs faits et vérifiés CONFIRMED le 2026-10-01 ; reste la recette T6 de l'auteur)

**But.** La vision se joue jusqu'au bout. À l'aller (`G1640`), la 476 ouvre ses trois boîtes puis part vers
la 478 (arc A2). Au retour (`G1641`), elle déplace la caméra par le bloc transparent, ouvre ses huit boîtes
puis part vers la 392 (arc A4).

**Faits établis** (découverte en lecture seule du 2026-10-01, trois volets contre-vérifiés ; scratchpad
`e19b/`) :

- **`0xC4`** (`0x80041DA8`, taille 6) **[binaire]** :
  - opérandes : v1 recherche du locuteur, v2 | v3<<8 nom, v4 texte, v5 mode ;
  - il rend 0 tant qu'un dialogue est ouvert, sinon ouvre la boîte et rend 6 au même tick. Il n'attend
    jamais la fermeture et n'écrit pas `Result` ;
  - la recherche du locuteur n'a aucun effet observable : E19.b ne l'évalue pas (D-E19-5).
  - **Portage** : `OpenDialog(v[4], v[5], 6, …)`, le même chemin que `0x0D` et `0x5C`
    (`AlundraEventProgramRunner.cs:748-755`, `:828-831`, `:1445-1468`).
  - **Corpus** : 31 sites dans 11 cartes. Les 31 nœuds Yarn existent, tous en mode 1 et en table de
    carte.
  - **Portée** : dix autres cartes (25, 76, 84, 95, 161, 226, 274, 347, 411, 432) ouvriront désormais leurs
    boîtes. `0x5C` n'a aujourd'hui aucun test direct.
- **`0x4C`** reste sauté, comme le prévoit l'enveloppe : il ne sert qu'à la machine à écrire (E12.c), et
  chaque ouverture de dialogue le remet à 3.
- **`0x8A`** (`0x80040284`, taille 8) **[binaire]** :
  - il fait apparaître l'enregistrement v1 sans test de zone, avec l'entité logique pour parent ;
  - puis il écrit `PosX = X << 16`, `PosY = Y << 16` et `PosZ = (Z << 16) + 1`, X, Y et Z étant des
    entiers de 16 bits absolus. Il n'écrit pas `Result` ;
  - un échec d'apparition est fatal dans l'original. La DLL le journalise et continue.
  - **Portage** : le chemin de `0x8B` (`SpawnEntityByRecordId`, puis `PushLogicalPositionToRoot`).
  - **Sites de la 476** : B1 `@63` (bloc caméra, enregistrement 1, en (972, 112, 48)) et B5 `@553`
    (Rancune, enregistrement 0, en (792, 176, 48)) ; le site `@534` est mort.
  - **Corpus** : 684 sites dans 69 cartes ; 191 sont dans des boucles qui supposent la réutilisation des
    emplacements d'entités, absente de la DLL. C'est une limite connue, notée pour E14 et E19.m.
- **Déroulé de la 476** : six événements de carte, B1 à B6, sans programme d'entité.
  - Les seules barrières sont `G1640` et `G1641`, T999 (posé par le nœud Yarn à l'ouverture), la poignée
    de main T1000 entre B4 et B2, les attentes `0x37` et `0x39`, et, en A4, quatre marches `0x1E` du bloc.
  - Les onze boîtes passent par le sous-programme `@112` et se ferment par le script, environ 61 ticks
    après leur ouverture : aucun bouton n'est nécessaire.
  - Après E19.b, restent sautés : `0x4C @112`, `0x92`, `0x93` et `0xA2` (effets, E19.g ; aucun ne
    suspend).
  - Dans la DLL, les drapeaux T1001 à T1005 tombent ensemble à l'ouverture. Certains éclairs blancs
    (`0xAF`) et effets de B2 ne se jouent donc pas : c'est cosmétique, jusqu'à la machine à écrire.
- **Le bloc** : 24×16×32, avec un contrôleur, collidable. Toutes les cellules de la 476 sont praticables
  (walkability 0) : rien ne peut l'arrêter. Ses panoramiques font 48 px à 0,75 px par tick.
- **Latence d'animation (O-E19-5)**, tranchée par le binaire **[binaire]** :
  - l'original exécute dans l'ordre les événements de carte, les entités, `UpdateAnimation` puis la
    physique (`0x8002E100`, `0x8003B388` → `0x8003B3D8` → `0x8003B3E0`). Un changement d'animation par
    script s'applique donc dans la physique du même tick ;
  - la DLL a une image de retard : c'est la cause du pas de 1,25 px du marin 12 ;
  - chaque panoramique du bloc fera 48,75 px au lieu de 48. La correction reste dans E19.c.
- **Arcs** : 1 tick logique par image à dt 0,02, donc des comptes d'images exacts.
  - A2 : environ 1209 images ;
  - A4 : environ 2059 images.

**Tâches.**

- **T0 — Preuves rouges** ✅ *(fait le 2026-10-01 ; rouge constaté, conforme à la table : sur le code d'avant, A2 échoue à 1500 images en nommant `slot 1 program @612: last 0x36 @116` avec `0xC4 @622` et `0x8A @63` sautés, A4 à 2500 images en nommant `slot 1 program @772: last 0x36 @116` avec `0xC4 @786` sauté. Après T1, A2 atteint `0x53 @758` puis échoue sur « record 1 (the camera block) is absent after 0x8A @63 », et A4 échoue à 2500 images en nommant `slot 1 program @772: last 0x1E @84`. Après T2, les deux passent avec les valeurs de T0, sans les changer. Le message d'A2 après T1 ne nomme que le bloc absent : l'assertion sur `0x8A` sauté vient après dans le test et n'est pas atteinte.)* : écrire les arcs A2 et A4 en mode nu, avec les valeurs ci-dessous écrites avant
  le code, puis les lancer.
  - **A2** : carte 476, drapeau `G1640` (mot 51 : 256), héros en (0,0,3), limite de 1500 images.
    - Fin : `0x53 @758`, arrivée sur la 478 en `PosX` 35389440, `PosY` 60293120, `PosZ` 1048576, effet 2.
    - À la fin, `G1640` éteint et `G1641` posé.
    - Le bloc (enregistrement 1) apparaît à l'instruction `@63` en (972 << 16, 112 << 16, (48 << 16) + 1).
      À `@71`, la caméra le suit.
    - Trois boîtes s'ouvrent, aux pc 622, 632 et 739, sur les nœuds `M476_S101`, `S102` et `S103` (texte
      échantillonné à l'ouverture). Chacune se ferme par le script, sans bouton.
  - **A4** : drapeau `G1641` (mot 51 : 512), héros en (0,0,0), limite de 2500 images.
    - Fin : `0x53 @986`, arrivée sur la 392 en `PosX` 46399488, `PosY` 14155776, `PosZ` 4194304, effet 4.
    - Huit boîtes : `0xC4` aux pc 786, 820, 842 et 873, `0x5C` aux pc 810, 832, 854 et 974, nœuds
      `M476_S104` à `S111`.
    - Les quatre marches `0x1E @84` et `@101` (deux fois chacune) se terminent : le bloc a bougé d'au moins
      48 px à chaque fois. L'écart au-delà de 48 px n'est pas épinglé tant qu'O-E19-5 n'est pas corrigé.
    - Rancune (enregistrement 0) apparaît à `@553` en (792 << 16, 176 << 16, (48 << 16) + 1).
  - **Pour les deux arcs** : aucun opcode sauté hors de {`0x4C`, `0x92`, `0x93`, `0xA2`} ; `0xC4` et `0x8A`
    jamais sautés ; aucun dépassement de la garde. À la fin, `PlayerControlFlags == 0x04` (ControlLocked
    par `0x10 @73`, la 476 n'a pas de `0x11`) : ne pas reprendre le « drapeaux à 0 » d'A0.
  - **Moment des vérifications** :
    - les échantillons pris à une instruction (position du bloc à `@63`, suivi de caméra à `@71`, Rancune à
      `@553`, texte de chaque boîte à son ouverture, position du héros avant et après chaque marche du bloc)
      sont **enregistrés pendant la course**, par `OnInstruction`, qui voit l'instruction après ses effets ;
    - ils ne sont **vérifiés qu'après** que `RunUntil` a atteint le signal de fin. L'échec d'un arc qui
      n'arrive pas au bout nomme donc toujours sa dernière instruction ;
    - une position se lit à l'instruction qui la pose : le Z du bloc dérive aux images suivantes, et
      Rancune est déplacé ensuite par les sous-programmes `@134` et `@188`.
  - **Précisions** :
    - `@553`, `@78`, `@90`, `@95` et `@107` sont dans le code d'autres programmes, mais appelés depuis
      B5 ;
    - le parent du bloc est le héros (entité logique de B1). Celui de Rancune est **le bloc**, car
      `0x43 [0x80]` laisse le bloc comme entité logique de B5 après le premier panoramique ;
    - « le bloc a bougé d'au moins 48 px » répète la condition de sortie de `0x1E`. Le test vérifie aussi
      que **le héros ne bouge pas** pendant chacune des quatre marches, ce qui prouve que la marche agit
      sur le bloc. Les positions sont mesurées après l'image 0, où B1 `@55` déplace le héros.
    - Chaînes de l'`ArcSpec` : zone `Lars & Melzas Room`, carte `Lars & Melzas Room (beginning Event)-476`.
      La classe porte `[Collection(AlundraMusicPlayerSingletonCollection.Name)]`.
  - **Rouge attendu, étape par étape**, cohérent avec chacune des vérifications ci-dessus :

    | Étape | A2 | A4 |
    |---|---|---|
    | Code actuel | échoue dans sa limite en nommant `0x36 @116` | échoue dans sa limite en nommant `0x36 @116` |
    | Après T1 (`0xC4`) | atteint `0x53 @758`, puis échoue sur le bloc absent à `@63` et sur `0x8A` sauté | échoue dans sa limite en nommant `0x1E @84` (`0x43 [1]` ne trouve pas de bloc) |
    | Après T2 (`0x8A`) | passe | passe |

    Ces constats sont consignés.
- **T1 — `0xC4`** ✅ : le cas dans `Dispatch` (fait le 2026-10-01 ; tests dans `AlundraDialogueSpeakerOpcodeTests`, 11 tests dont le corpus 31 sites / 11 cartes ; rouge d'A2 et d'A4 après T1 conforme à la table).
  - Tests unitaires :
    - positions des opérandes, avec des nœuds leurres : une lecture décalée ouvrirait un autre texte ;
    - taille 6, et rend 0 tant qu'une boîte est ouverte ;
    - mode 0 → `MenuOpen`, mode 1 → `MessageBox` ;
    - `Result` inchangé ; chemin dégradé sans présentateur ;
    - le premier test direct de `0x5C`, dans le même `Theory`.
  - Test de corpus : les 31 sites, chacun ouvrant un nœud qui existe.
  - Miroir `ImplementedOpcodes` et sélecteur `textId` du harnais de l'intro.
- **T2 — `0x8A`** ✅ : le cas et une petite méthode à côté de celle de `0x8B` (fait le 2026-10-01 ; 5 tests unitaires dans `AlundraEventProgramRunnerTests` ; A2 et A4 passent avec les valeurs de T0).
  - Un échec est journalisé en avertissement, une seule fois par (opcode, enregistrement).
  - Tests unitaires : position, `+1` sur Z seul, parent = entité logique, octet fort des 16 bits, échec sans
    exception (taille 8, aucune écriture de position), deux apparitions du même enregistrement, `0x8A` puis
    `0x67`.
    - Pièges du faux contexte : une recherche par numéro exige l'owner en `Status = Normal`, et l'entité
      apparue doit recevoir `EntityRefId = 1` et entrer dans `SpawnedEntitiesList`, ce que le faux ne fait
      pas seul. Le parent ne se vérifie que par `SpawnCalls.LogicEntity`.
  - Un échec d'apparition, fatal dans l'original, est journalisé et l'exécution continue : c'est la pratique
    déjà en place pour `0x2D` et `0x8B`, pas une décision nouvelle.
  - Miroir du harnais. Commentaires périmés : l'aide de `0x8B`, `IEntityWorldContext.cs:59`,
    `AlundraWorldProxy.cs:~2419`.
- **T3 — Les arcs** ✅ : A2 et A4 passent avec les valeurs de T0 ; ils sont commités (`AlundraVisionArcTests`, fait le 2026-10-01).
- **T4 — Hygiène reportée par E19.a et E19.a3** ✅ *(fait le 2026-10-01 ; le test d'échec du constructeur lit l'option de caméra, mais comme la valeur par défaut est déjà « activée », il ne distingue pas l'ancien ordre du nouveau : la correction est structurelle)* :
  - le constructeur d'`ArcRun` : `FindProjectRoot` et l'option de caméra sont placés dans le `try`, en
    mémorisant `_previousProjectPath` **avant** `FindProjectRoot`, sinon `Dispose` remettrait `ProjectPath`
    à null. Le test d'échec vérifie aussi l'option de caméra, lue par
    `AlundraWorldProxy.DebugCameraPanEnabledForTests` ;
  - commentaires : test `LoopGuard_ALoopWithAWait`, `0x3B`/`0x3E` (« entité qui exécute »),
    `EntitySearchService.cs:31`, doc de `ProductionLoopBudget`, place du commentaire des 23 opcodes
    d'`ImplementedOpcodes`, message dégradé de `0x42` (« `Result = 0` »), en-tête du test du marin 12
    (`@1484`) ;
  - textes en retard : `docs/intro-roadmap.md` vers `:101`, `docs/plan-e4-deplacement-scripte.md:411`,
    `docs/plan-oracle-heros.md:233`, résumé de D-E19-10 au §0.1, O-E19-5 mis à jour avec le constat du
    binaire.
- **T5 — Docs** ✅ : statuts de ce plan, ligne E19 du plan maître *(fait le 2026-10-01)*.
- **T6 — Recette en jeu (auteur)** ⏳ :
  1. **A2, en jouant.** Après la cabine, la 476 montre trois boîtes sans nom, d'environ 1,2 s chacune, et
     la caméra sur la salle de la vision. Puis la 478 se charge. Elle ne va pas plus loin : c'est E19.c.
  2. **A4, par raccourci.** F6, puis dans `debug-json.sav` : `initialMapId` 476, `cameraTileX`,
     `cameraTileY` et `cameraTileZ` à 0, `gameFlags[51] |= 512` **et le bit 256 effacé** (sinon B4 rejoue la
     séquence d'A2 en même temps), puis F9. On voit huit boîtes, la caméra glisse vers l'ouest puis l'est,
     et la 392 se charge.

**Acceptation d'E19.b.**
1. Les deux arcs échouaient comme annoncé avant T1 et T2, et passent avec les valeurs de T0.
2. Les tests unitaires de T1 et T2 passent, ainsi que le test de corpus.
3. `Alundra.Tests` et les tests du convertisseur passent à 0 échec ; le moteur ne change pas.
4. Un verifier frais rend CONFIRMED sur 1 à 3.
5. La recette T6 de l'auteur.

**Risques.**
- Chaque boîte reste ouverte environ 1,2 s, sans machine à écrire. Ce rythme est accepté depuis D-E19-5,
  jusqu'à E12.c.
- Dix autres cartes ouvrent désormais leurs boîtes `0xC4` : intros de boss, Inoa, sanctuaire du lac. Aucun
  arc ne les couvre. Un opcode non porté plus loin dans leurs scènes peut maintenant y bloquer : on le
  verra en jouant.
- `0x8A` passe en vrai dans 69 cartes. La DLL n'a pas de plafond de 63 entités ; les boucles qui font
  apparaître des entités en grand nombre attendent E14 ou E19.m.
- Le bloc a un vrai contrôleur en jeu, mais il est nu dans les arcs. Les données excluent tout blocage
  (cellules toutes praticables), et la recette le confirmera.
- A4 n'est pas atteignable en jouant avant E19.c : la 478 doit d'abord renvoyer vers la 476.

**Revues** : plan-verifier sur cette section, avant approbation ; verifier frais après exécution.

**Relectures du 2026-10-01.**
- **Plan-verifier** (`ba2bb30`) : **REVISE**, un P2 : la suite rouge annoncée (« après T1, A2 passe »)
  contredisait les vérifications d'A2 sur le bloc, et le moment des vérifications n'était pas dit.
- **Audit en parallèle** : aucun P1. Toutes les valeurs de T0 sont recalculées et confirmées, dont les
  1209 et 2059 images, par une réécriture indépendante de l'interpréteur. Il signale le même P2, et des P3 :
  - naming « appelé depuis B5 » ;
  - parent de Rancune = le bloc ;
  - positions lues à l'instruction ;
  - héros immobile pendant les marches ;
  - pièges du constructeur d'`ArcRun` et du faux contexte ;
  - détails de l'`ArcSpec` et des drapeaux de contrôle ;
  - raccourci de recette.
- **Tous sont intégrés** : table du rouge étape par étape, moment des vérifications, précisions de T0 et
  T2, constructeur d'`ArcRun`, recette.
- **Relecture neuve de la révision** (`1dc4bdb`) : **READY**.

**Vérification d'E19.b (2026-10-01).**
- **Commits** : `c885926` (`0xC4`), `50353df` (`0x8A`), `a6448a3` (arcs), `b987da3` (hygiène), `d8c9d83`
  (docs). Le pointeur du moteur ne change pas.
- **Verifier frais : CONFIRMED** sur les acceptations 1 à 3. `Alundra.Tests` 1939/1939 en Release,
  convertisseur 400/400.
  - Il a lu les arcs : ils ne sont pas vides. Valeurs exactes des arrivées, texte de chaque boîte par pc,
    héros immobile pendant les quatre marches, ensemble des opcodes sautés, échantillons vérifiés après le
    signal de fin.
  - Dans un arbre de travail jetable :
    - les deux cas retirés, le rouge du code d'avant se reproduit à l'identique ;
    - `0x8A` seul retiré, le rouge d'après T1 se reproduit à l'identique ;
    - le `+1` sur Z retiré, trois tests unitaires de `0x8A` et les deux arcs échouent.
  - Les traces du héros ne changent pas. La DLL déployée dans `alundra-project/` est la DLL Debug, à
    jour.
- **Contradicteur en lecture seule** : aucun P0 à P2. Les dix autres cartes de `0xC4` ne bloquent pas.
  Pour la 161 `S097`, T999 est posé par la page 1 au premier appui, comme dans l'original. Les 30 autres
  sites sont suivis directement d'un `0x39`. Dispositions (signalé, jamais corrigé sur un candidat
  CONFIRMED) :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | `0x8A` apparaît désormais pour de vrai dans environ 70 cartes. Des programmes en boucle (sanctuaire 28, et 30, 134, 147, 159, 160) font apparaître des entités sans fin tant que le joueur reste sur la carte, car `DestroyEntity` ne retire jamais l'entité du moteur. | P3, introduit | Reporté : limite déjà acceptée par ce plan (risques ci-dessus, E14 ou E19.m). Aucune de ces cartes n'est sur la chaîne. |
  | Aucun test n'exerce `0x8A` sur le vrai bloc, qui porte un contrôleur : dans les arcs, le bloc est nu et `PushLogicalPositionToRoot` ne fait rien. Le second appel de `PushLogicalPositionToRoot` est hors du `try` de `SpawnEntityByRecordId`. | P3, introduit | Reporté : la recette T6 le couvre ; les arcs avec de vrais contrôleurs viennent avec O-E19-3 (E19.c). |
  | Après l'écriture de la position par `0x8A`, l'état de support et les champs `Tile*` restent ceux de la position du record jusqu'au tick suivant. | P4 | Accepté : même forme que `0x8B` ; sans effet pour le bloc et Rancune, qui ne portent personne. |
  | Tests plus faibles que le plan : l'octet fort n'est testé que sur X ; les arcs ne vérifient pas les parents (bloc → héros, Rancune → bloc) ; le fragment de texte « Tu » de `S102` est faible. | P4 | Reporté à l'hygiène d'E19.c. |
  | La correction du constructeur d'`ArcRun` n'a pas de test qui la distingue de l'ancien ordre. Un `ResetAll` qui lèverait dans `Dispose` masquerait l'exception d'origine. | P4 | Accepté : correction structurelle, déjà notée en T4. |
  | Textes en retard : la doc de classe et les commentaires de cas d'`EntitySearchService` disent encore « owner » ; la doc d'`OpenDialog` ne cite que `0x0D` et `0x5C`. | P4 | Reporté à l'hygiène d'E19.c. L'ADR-0006 ne se réécrit pas. |
  | La preuve rouge des arcs n'est pas dans l'historique : les arcs sont commités après le code. | P4 | Accepté : le verifier l'a reproduite à part, comme pour E19.a3. |

### 1.2e E19.c1 — Cartes 478 et 416 : attentes de mouvement, arcs en vrais préfabs ⏳ (proposée le 2026-10-01)

**But.**
- La vision de la 478 va au bout : le bloc caméra monte, les drapeaux T20 à T60 tombent, puis la 476
  revient (arc A3).
- La plage 416 mène à la 163 : le bloc caméra glisse au nord, Jess apparaît, marche jusqu'à Alundra,
  puis la 163 se charge (arc A7).
- Les arcs chargent les vrais préfabs (D-E19-14), ce qui teste aussi `0x8A` sur le vrai bloc de la 476
  (arc A4p, le jumeau d'A4).

**Périmètre.** DLL (`AlundraEventProgramRunner.cs`, `AlundraEntitySpawnFactory.cs`,
`AlundraFrameSyncPasses.cs`, `EventOpcodeSizeTable.cs` pour les libellés, commentaires d'`EntitySearchService.cs` et
d'`AlundraWorldProxy.cs`), tests (`Alundra.Tests`), docs. Le moteur ne change pas. Le retard d'animation
est gardé (D-E19-13) : toutes les valeurs ci-dessous l'incluent.

**Faits établis** (découverte en lecture seule du 2026-10-01 : quatre volets, chacun contre-vérifié ;
scratchpad `e19c/`) :

- **La 478** (arc A3) **[données]** :
  - une seule chaîne fait avancer la carte. Le programme C[5] du chien (enregistrement 5) fait `0x63
    [0,128,1]` `@359`, puis `0x5E [0,96,0]` `@363` : le bloc caméra (enregistrement 0) reçoit `ForceZ`
    = 0x6000, soit 0,375 px par tick vers le haut ;
  - C[5] sonde ensuite à chaque image le `TileZ` du bloc (`0x07 [130,26,27,57,57,z,z]`, z = 8, 21, 33,
    39, 48) et pose T20 à T60. B1 (`@164`) attend chacun (`@180`, `@191`, `@202`, `@213`, `@224`) et
    téléporte le héros ;
  - puis `0xAF`, `Wait 130`, `0xA6`, `Wait 120`, `G1641` `@242`, et `0x53 @245` vers la 476 en `PosX`
    786432, `PosY` 524288, `PosZ` 0, effet 0 ;
  - seul `0x5E` bloque la progression. Les autres opcodes atteints et non portés sont cosmétiques :
    `0x0B` (21 sites), `0x1C @854`, `0x89 @836`, `0x73` et `0x74` (3 sites chacun), `0x08` (2 sites).
    Tant que `0x0B` est sauté, les cycles C[11] et C[12] déclenchent la garde de boucle à chaque image ;
  - aucune boîte de dialogue, aucun drapeau persistant lu ; pas de `0x11` : `PlayerControlFlags` finit
    à 0x04.
- **La 416** (arc A7) **[données]** :
  - on n'y entre que par `0x53 @540` de B1 sur la 391, en `PosX` 65273856, `PosY` 51904512, `PosZ`
    1048576, effet 2 ;
  - B1 pose T0 (`@506`). Le bloc (C[100], enregistrement 0) marche 192 px au nord à 0,5 px par tick
    (`0x0B [1,192]` `@659`), attend 3 × 60, puis `0x2D [1]` `@671` fait apparaître Jess ;
  - Jess (C[1], enregistrement 1) marche 72, 64, 48 et 16 px (`@572`, `@586`, `@594`, `@608`), attend,
    puis `0x53 @640` charge la 163 en `PosX` 63700992, `PosY` 9961472, `PosZ` 2097152, effet 2 ;
  - seul `0x0B` (5 sites) manque. Aujourd'hui, les marches sont sautées et la carte atteint son `0x53`
    dès l'image 1145.
- **Aucun mur sur les marches** : sur la 478 comme sur la 416, aucune cellule traversée n'a le bit
  0x40, et les hauteurs suivent le marcheur. Les tuiles n'ont aucun collisionneur. Seule marge étroite :
  le bloc de la 416 passe à 1 px de la boîte du héros allongé. Toutes les animations de marche de ces
  deux cartes sont des Loop : aucune fin Chain ne change la vitesse pendant une marche.
- **Sémantique du binaire** **[binaire]** (chaque gestionnaire agit sur l'entité logique) :
  - `0x0B` (`0x8003D468`, taille 4) : écrit `TargetAnimationId` = v1 à **chaque** appel. Le premier
    appel mémorise le pc et `PosX`/`PosY` puis rend 0. Ensuite il rend 4 dès que r ≤ |dX| >> 16 ou r ≤
    |dY| >> 16, avec r = v2 | v3 << 8. Il n'écrit pas la direction, ne lit pas `ForceAdjusted` et n'a
    aucune sortie en cas de blocage ;
  - `0x08` (taille 2) : `TargetDirection` = (`TargetDirection` + v1) & 0x1F ;
  - `0x3A` (taille 2) : `TargetDirection` = table[v1 & 3], table {0, 0x10, 8, 0x18} ;
  - `0x0C` (taille 1) : tirage du générateur partagé (graine `0x80098708`, mêmes constantes
    qu'`AlundraRandom`), puis `TargetDirection` = table[graine >> 30] ;
  - `0x5E` (taille 4) : pour chaque entité trouvée par la recherche v1, `ForceZ` = int16(v2 | v3 << 8)
    << 8 ;
  - `0x73` (taille 2) : compteur `state+0x30` = v1 (le champ `_30` d'`EventProgramState`, présent et
    inutilisé). `0x74` (taille 3) : le décrémente ; rend 3 s'il est ≤ 0, sinon le saut relatif
    int16(v1 | v2 << 8). Un compteur par état de programme, jamais remis à zéro ;
  - `0x89` (taille 9) : la référence est la première entité trouvée par v1, sa position lue avant la
    seconde recherche v2 ; chaque entité trouvée par v2 reçoit la position de la référence plus
    int16 << 16 sur X, Y et Z, sans `+1` sur Z. Rend 9, même sans entité trouvée ;
  - `0x1C` (taille 2) : le premier appel mémorise le pc, compte 0, `AnimCompleteCounter` = 0, rend 0,
    sans toucher `ForceResetAnimationFlag`. Ensuite : si le drapeau Hold est posé,
    `CurrentAnimationId` = ~`TargetAnimationId` et compte + 1 ; sinon, si `AnimCompleteCounter` ≠ 0,
    compte + 1 ; dans les deux cas `AnimCompleteCounter` = 0. Il rend 2 quand compte ≥ v1, sinon 0.
    `0x1C` n'efface jamais le drapeau Hold ;
  - `0x1D` : comme `0x1C`, mais rend 2 aussi quand `ForceAdjusted` ≠ 0, dès le premier appel ;
  - dans `UpdateAnimation` (`0x80038AB4`), une fin Hold pose le drapeau Hold ; une fin Chain et un
    tour de Loop font `AnimCompleteCounter` + 1 ; un changement d'animation efface le drapeau Hold mais
    ne remet jamais le compteur à zéro. La décompilation se trompe ici (remise à zéro au changement,
    pas d'incrément sur Chain) : le binaire tranche.
- **La DLL aujourd'hui** **[DLL]** :
  - aucun des dix opcodes n'a de cas dans `Dispatch` ;
  - le pont des fins d'animation (`OnAnimationFinished`) pose le drapeau Hold et applique les Chain,
    mais n'incrémente jamais `AnimCompleteCounter`, et `SyncAnimation` n'efface jamais le drapeau Hold ;
  - aucun site de `0x1D` sur la 478, la 416 ou la 163, et aucun site de la chaîne n'a besoin du
    signal de boucle ;
  - `Walk` (`0x1E`/`0x1F`) a le cœur de mesure de `0x0B` et le détour d'E4.d, avec trois écarts : clé
    sur les opérandes, seuil en v1 | v2 << 8, retour 3.
- **Support d'arcs** **[DLL][moteur]** :
  - tous les PNJ des arcs sont aujourd'hui des entités nues, dans les deux modes : le jeu factice n'a
    pas de gestionnaire d'assets, le chargement du préfab lève, et l'apparition retombe sur une entité
    nue. Une entité nue ne monte jamais en Z : le bloc de la 478 resterait au sol ;
  - un `AssetContentManager` construit par le test, posé par réflexion sur le jeu factice comme l'est
    déjà son `GameManager`, rend le vrai chemin d'apparition. Il faut les chargeurs `Entity`,
    `Animation2dData` et `UIScreenAsset`, et des identifiants résolus depuis `AssetInfos.json` par
    `RuntimeContext.ResolveAssetInfo` ;
  - trois pièges. Ne jamais enregistrer les chargeurs `SpriteData` ou `Texture` (il leur faut un
    `GraphicsDevice`). Sans chargeur `UIScreenAsset`, le branchement du HUD lève. Un résolveur qui lève
    sur un identifiant inconnu fait abandonner l'entité en silence par `World.InternalAddEntities` ;
  - dans le jeu, le bloc de la 478 monte par son contrôleur (`MoveVerticalAndPullPosition`). La DLL
    calcule `TileZ` avant ce mouvement, le binaire après (D-E19-15, E19.h).
- **Écarts gardés** (déjà décidés, rappelés pour les valeurs) :
  - `0x89` écrit par le chemin de `0x64`/`0x65` (`PushLogicalPositionToRoot`, donc `ClampToGround` puis
    `Teleport`), absent du binaire, comme décidé en E3.d. Chaque recherche alloue une liste, comme les
    sondes `0x07` déjà portées : une recherche sans allocation est pour E19.m ;
  - `0x0C` et les autres consommateurs du générateur : la position dans la suite ne peut pas égaler
    celle de l'original tant que l'IA native n'est pas portée (588 accès dans le binaire).

**Tâches.** Chaque tâche porte son icône de statut et se commite avec la mise à jour de ce plan. Les
arcs restent hors du dépôt jusqu'à T5 ; d'ici là, les lancements de toute la suite filtrent leur
classe.

- **T1 — Mode « vrais préfabs » du support d'arcs** ✅ *(fait le 2026-10-01 : `ArcSpec.Prefabs`, `ArcPrefabAssets` dans `AlundraArcSupport.cs`, autotest `AlundraPrefabArcSupportTests`. Rouge constaté avec le drapeau sans effet : « record 0 à 21 : no controller ». Vert ensuite, 993 ms. Seules erreurs journalisées après la première image : 3628 résolutions de sprite (616760 caractères), attendues sans chargeur `SpriteData` ; le test échoue sur toute autre erreur.)* (tests seulement) :
  - drapeau `Prefabs` de l'`ArcSpec`, qui exige `RealController` ;
  - un `AssetContentManager` construit par le test et posé par réflexion sur le jeu factice. Il a les
    chargeurs `Entity`, `Animation2dData` et `UIScreenAsset`, jamais `SpriteData`, `Texture` ni
    `TileSetData` : pas de grille de navigation, donc pas de détour, car aucune marche ne rencontre de
    mur ; le détour de `0x0B` est couvert par un test unitaire en T3 ;
  - un résolveur depuis `AssetInfos.json` par `TryGetValue`, qui rend `null` sur un identifiant
    inconnu ;
  - **autotest, rouge d'abord** (en mode `RealController` seul), puis vert. Sur la 478, après la
    première image :
    - les 22 enregistrements ont un `Controller`, sont dans `world.Entities` et ont `Entity.World` égal
      au monde de l'arc ;
    - l'enregistrement 0 a les drapeaux 0x6080, un `AnimatedSpriteComponent` de 24 animations et une
      boîte 24 × 16 × 32 en local (0, 0, 16) ;
    - aucun avertissement de repli sur une entité nue et aucune exception journalisée pendant
      l'intégration ;
  - A0, A0b, A1, A1c, A2, A4, la cabine et le marin 12 ne changent pas : ils n'activent pas le drapeau.
  - Le temps de l'autotest est relevé.
- **T2 — Arcs A3, A7 et A4p écrits avant le code** ✅ *(faits le 2026-10-01, non commités jusqu'à T5 ; fichier `AlundraVisionAndCoastArcTests.cs` pour A3 et A7, A4p dans `AlundraVisionArcTests` qui partage les échantillons d'A4. Rouge constaté après T1, conforme à la table : A3 échoue dans sa limite de 2700 images en nommant `slot 1 program @164: last 0x36 @180` (3 s, pas besoin de la limite de 400) ; A7 atteint `0x53 @640` à l'image 1145 puis échoue sur les opcodes sautés `0x0B @659`, `@572`, `@586`, `@594`, `@608` ; A4p passe avec toutes ses valeurs.)* : valeurs ci-dessous,
  lancées sur le code d'après T1. Les échantillons sont pris pendant la course (`OnInstruction`, après
  l'effet de l'instruction) et vérifiés après le signal de fin. La fin d'un `0x0B` se lit à
  l'instruction **suivante** : `ArcInstruction` ne porte pas le résultat du gestionnaire, et la
  première exécution d'un `0x0B` est son appel suspendu.
  - **Ordre des vérifications** (A3, A7 et A4p) : d'abord le signal de fin (`RunUntil`, qui échoue dans
    la limite en nommant la dernière instruction de chaque programme) ; **ensuite l'ensemble des
    opcodes sautés ou dépassés**, qui doit être vide (A3, A7) ou rester dans celui d'A4 (A4p) ; enfin
    les autres vérifications, dans l'ordre où elles sont écrites ci-dessous. La table du rouge suit cet
    ordre.
  - **A3** : `ArcSpec("A3", "Inoa", "Inoa (Vision Event from Lars and Melzas cutscene)-478", {1641},
    héros en (22, 57, 1), limite 2700, RealController, Prefabs)`.
    - Fin : `0x53 @245` (créneau B, programme `@164`) ; arrivée sur la 476 en `PosX` 786432, `PosY`
      524288, `PosZ` 0, effet 0. La trace contient `0x05 @242` : comme `G1641` est posé au départ,
      « `G1641` posé à la fin » ne prouverait rien.
    - Ordre des premiers `0x05` : T0 (B `@177`), T10 (C `@370`), T20 (`@437`), T30 (`@442`), T40
      (`@447`), T50 (`@452`), T60 (`@457`), puis le `0x53`.
    - Écarts exacts entre les premiers `0x05` : T20 → T30 = 555 images, T30 → T40 = 512, T40 → T50 =
      256, T50 → T60 = 384. Images attendues (modèle) : T20 301, T30 856, T40 1368, T50 1624, T60 2008,
      `0x53` 2262, vérifiées à ± 3 près (la convention de comptage des images n'est pas mesurée).
    - L'enregistrement 0, à chaque premier `0x05` de T20 à T60 : `TileZ` 8, 21, 33, 39, 48 ; `PosX`
      42467328 et `PosY` 60293120 (648 et 920 px) ; `PosZ` 8421376, 22061056, 34643968, 40935424,
      50372608.
    - Après `0x63 @359`, l'enregistrement 0 a les drapeaux 0x6000 ; après `0x5E @363`, `ForceZ` =
      24576.
    - Héros, lu aux téléportations de B1 (X et Y seulement : Z dépend du contrôleur) : `@166` (35389440,
      60293120), `@183` (36962304, 49807360), `@194` (38535168, 38273024), `@205` (47972352,
      33030144), `@216` (35389440, 26738688).
    - Programmes cosmétiques : C[11] atteint `@725` et C[12] `@802` après T50 (sortie de leurs tours) ;
      `0x08 @547` et `0x08 @576` s'exécutent 16 fois chacun, et `TargetDirection` vaut 16 à la sortie
      de la boucle de `@549`, 0 à celle de `@578` ; à chaque `0x89 @836`, l'enregistrement 13 a les
      `PosX`/`PosY` de l'enregistrement 11 ; le programme de Ronan dépasse `0x1C @854` au moins une
      fois (la fin Hold arrive par le vrai sprite).
    - Fin : aucun opcode sauté, aucun dépassement de la garde, aucune boîte ouverte,
      `PlayerControlFlags == 0x04`.
  - **A7** : `ArcSpec("A7", "Coast", "Coast beginning-416", {}, héros en (41, 49, 1), limite 2200,
    RealController, Prefabs)`.
    - Fin : `0x53 @640` (créneau C, programme `@568`, Jess) ; arrivée sur la 163 en `PosX` 63700992,
      `PosY` 9961472, `PosZ` 2097152, effet 2. Image attendue : 1759 (± 3).
    - B1 : `0x05 @506` (T0) à l'image 493 (± 3) ; héros en (65273856, 49807360) de `0x64 @421` à la
      fin ; après `0x5B @429`, `TargetAnimationId` 78 et `TargetDirection` 16.
    - Le bloc : premier appel de `0x0B @659` à l'image 494 (± 3). À l'instruction suivante (`0x1A
      @663`), 385 images plus tard exactement : `PosX` 66846720, `PosY` 49807360 (1020 et 760 px).
    - `0x2D @671`, 183 images après : une seule entité d'`EntityRefId` 1, Jess, en `PosX` 57409536,
      `PosY` 42991616 (876 et 656 px), `PosZ >> 16` = 16, `TargetDirection` 24, avec un `Controller`.
    - Les quatre marches de Jess finissent dans l'ordre, lues à l'instruction suivante, avec
      `ForceAdjusted` = 0 :
      - `@572` → `0x5B @576` : `PosX` 62128128 (948 px), image 1161 (± 3) ;
      - `@586` → `0x5B @590` : `PosY` 47251456 (721,0 px), image 1288 (± 3) ;
      - `@594` → `0x5B @598` : `PosX` 65323008 (996,75 px), `PosY` 47284224 (721,5 px), image 1321
        (± 3) ;
      - `@608` → `0x1A @612` : `PosX` 65372160 (997,5 px), `PosY` 48365568 (738,0 px), image 1385
        (± 3).
    - Au `0x53` : Jess en (65372160, 48398336), soit (997,5 ; 738,5) px ; le bloc en (66846720,
      49774592), soit (1020 ; 759,5) px. Le demi-pixel de plus vient du retard gardé (D-E19-13).
    - Fin : aucun opcode sauté, aucun dépassement, aucune boîte, `PlayerControlFlags == 0x04`.
  - **A4p** : l'arc A4 d'E19.b en mode `RealController` + `Prefabs`, mêmes valeurs de fin, mêmes huit
    boîtes, plus :
    - le bloc apparu par `0x8A @63` a un `Controller` et entre dans `world.Entities` à l'image
      suivante ; lu à `@63` en (63700992, 7340032, 3145729), puis `PosZ` = 3145728 une image plus tard
      (le `+1` tombe au premier ajustement de la racine) ;
    - les quatre panoramiques, mesurés comme dans A4 (`BlockBefore` → `BlockAfter`, en 16.16) :
      63700992 → 60555264, 60555264 → 57409536, 57360384 → 60506112, 60506112 → 63651840 ; `PosY`
      reste 7340032 ; `ForceAdjusted` du bloc reste 0 et le héros ne bouge pas ;
    - parents : celui du bloc est le héros, celui de Rancune est le bloc (`ParentEntity`).
  - **Rouge attendu, étape par étape** :

    | Étape | A3 | A7 | A4p |
    |---|---|---|---|
    | Après T1 (code d'avant) | échoue dans sa limite en nommant `slot 1 program @164: last 0x36 @180` | atteint `0x53 @640` vers l'image 1145, puis échoue sur les opcodes sautés, qui contiennent `0x0B @659`, `@572`, `@586`, `@594` et `@608` | passe |
    | Après T3 (opcodes de mouvement) | atteint `0x53 @245`, puis échoue sur les opcodes sautés ou dépassés, qui contiennent `0x1C @854` | passe | passe |
    | Après T4 (`0x1C`, `0x1D`) | passe | passe | passe |

    Dans chaque case qui nomme une vérification qui échoue, toutes les vérifications qui la précèdent
    dans l'ordre ci-dessus passent sur le code de l'étape : le signal de fin est atteint (A7 après T1,
    A3 après T3), et l'ensemble des opcodes sautés est la première vérification qui suit.
    A4p ne dépend d'aucun opcode d'E19.c1 : il vérifie le mode préfabs sur la 476. Le rouge d'A3
    enregistre environ 5,8 millions d'entrées de trace (deux coupures de garde par image) ; s'il dépasse
    le délai, il se relance avec une limite de 400 images, qui donne le même message.
- **T3 — Les opcodes de mouvement** ✅ *(fait le 2026-10-01 : tests dans `AlundraEventProgramRunnerTests` et `AlundraRandomOpcodeTests` (collection du générateur). Après T3, A7 passe avec toutes ses valeurs ; A3 atteint `0x53 @245` puis échoue sur les opcodes sautés, qui sont `0x1C @854` seul, comme dans la table. Le test unitaire de `0x0B` place un `0x01` devant : la clé est le pc, et un pc 0 se lit « déjà mémorisé » (aucun programme du corpus n'a `0x0B`, `0x1C` ou `0x1D` au pc 0, vérifié sur les 483 cartes).)* : `0x0B`, `0x08`, `0x0C`, `0x3A`, `0x5E`, `0x73`, `0x74`,
  `0x89`, sur l'entité logique.
  - `0x0B` : une méthode sœur de `Walk`, qui garde `0x1E`/`0x1F` intacts : `TargetAnimationId` = v1 à
    chaque appel avant la clé, clé `CodeIndex`, seuil v2 | v3 << 8, retour 4, détour d'E4.d réutilisé
    tel quel (D-E19-6).
  - `0x73`/`0x74` sur `EventProgramState._30` (doc du champ mise à jour) ; `0x0C` sur `AlundraRandom` ;
    `0x5E` sur le modèle de `TurnMatchingEntities` ; `0x89` sur le modèle de `SetEntitiesPosition`.
  - Libellés de la table des tailles corrigés pour ces opcodes ; miroir `ImplementedOpcodes` du harnais
    de l'intro ; le test `UnknownOpcode_KnownSize_SkipsBySize`, qui prend `0x08` comme opcode inconnu,
    passe à un opcode qui reste non porté.
  - **Tests unitaires** (valeurs du binaire) :
    - `73 03 | 08 01 | 74 FE FF | FF` en un appel : direction 0 → 3, `_30` = 0, `0x74` rend −2, −2
      puis 3, l'appel finit sur `0xFF @7`. Avec `73 00` : direction 1, `_30` = −1. Avec `73 01` :
      direction 1, `_30` = 0 ;
    - `73 02 | 00 | 08 01 | 74 FD FF | FF` sur un état de créneau C conservé : l'appel 1 laisse `_30` =
      2 et `CodeIndex` 3 ; l'appel 2 donne la direction 1 et repasse le Break ; l'appel 3 donne la
      direction 2 et finit sur `0xFF @8`. `_30` = 7 survit à `InitializeEventData` ;
    - `0x08` : 31 + 1 → 0 ; 0 + 31 → 31 ; 5 + 0xFF → 4 ; rend 2. `0x3A` avec v1 = 0, 1, 2, 3, 6 → 0x00,
      0x10, 0x08, 0x18, 0x08 ; rend 2 ;
    - `0x0C`, après `AlundraRandom.Reset()` et dans une collection qui sérialise `AlundraRandom` : cinq
      appels donnent 0x00, 0x18, 0x18, 0x08, 0x00, et la graine vaut 0x35E36190 après le premier ; rend
      1 ;
    - `0x0B [7,12,0]` depuis (100, 100) px : le premier appel rend 0 et pose `TargetAnimationId` 7 ;
      après un déplacement de (12 << 16) − 1, rend 0 ; une unité de plus, rend 4 ; un `Target` remis à 0
      entre deux appels revient à 7 ; `TargetDirection` ne change jamais ; un déplacement vers −X ou sur
      Y seul termine aussi ; `[a,0,1]` donne un rayon de 256 ; avec une entité logique posée sur un
      programme B, la mesure et l'animation portent sur l'entité logique ;
    - détour de `0x0B` : copie du test de détour de `0x1E` (grille, `ForceAdjusted` = 1 : le détour
      s'engage une fois et se remet à zéro à la fin) ;
    - `0x5E` : `[0,96,0]` → `ForceZ` 24576 sur chaque entité trouvée ; `[x,0,0x80]` → −8388608 ;
      `[6,0,6]` → 393216 ; rend 4 ; sans entité trouvée, rien n'est écrit ;
    - `0x89` : la référence est la première entité trouvée dans l'ordre d'apparition ; les décalages
      `[2,0,0xFE,0xFF,0,0]` donnent +2, −2 et 0 px, sans `+1` sur Z ; rend 9 ; sans référence, les
      cibles ne bougent pas ; une entité qui se prend pour référence et cible avec +2 ne bouge qu'une
      fois.
  - A7 passe après T3 ; A3 échoue comme le dit la table.
- **T4 — `0x1C`, `0x1D` et le pont des fins d'animation** ✅ *(fait le 2026-10-01 : tests dans `AlundraRepeatAnimationOpcodeTests` (19 tests, même précaution du `0x01` en tête que pour `0x0B`). Après T4, les trois arcs passent avec toutes leurs valeurs.)* :
  - les deux gestionnaires exactement comme le binaire ;
  - le pont : une fin Chain fait `AnimCompleteCounter` + 1 (un Chain sur soi-même aussi), une fin Hold
    ne touche pas le compteur ;
  - `SyncAnimation` efface `ForceResetAnimationFlag` à **tout** changement (cible, direction, relance
    de Chain), avant son retour anticipé pour une entité sans sprite, et ne remet jamais le compteur à
    zéro ;
  - **tests unitaires** :
    - `0x1C`, premier appel : `AnimCompleteCounter` 5 → 0, drapeau laissé à 1, rend 0 ; l'appel suivant
      avec le drapeau : `CurrentAnimationId` = ~`TargetAnimationId`, rend 2 pour `[1]` ; un compteur à
      3 compte une seule fois ; drapeau et compteur ensemble comptent une seule fois ; `[0]` rend 2 au
      deuxième appel ;
    - drapeau tenu à 1 sans `SyncAnimation` entre les appels : `1C [2]` rend 0, 0 puis 2 ; le drapeau
      vaut toujours 1 après ;
    - `0x1D` avec `ForceAdjusted` = 1 au premier appel rend 2 ;
    - pont : fin Chain 54 → 0, compteur 0 → 1 ; Chain sur soi-même, 1 ; Hold, compteur inchangé ;
    - `SyncAnimation` : un changement de cible avec drapeau 1 et compteur 2 donne drapeau 0 et compteur
      2 ; un changement de direction seul efface aussi le drapeau ; sans changement, le drapeau reste à
      1 ; même chose pour une entité sans sprite ;
  - A3 passe après T4. Le moment exact de la fin Hold de Ronan n'est pas épinglé : il vient de
    l'horloge à virgule flottante du moteur jusqu'à E19.c2.
- **T5 — Arcs verts** ✅ *(fait le 2026-10-01 : A3, A7 et A4p passent avec toutes les valeurs de T2, sans en changer une ; images mesurées égales aux images attendues, par exemple T20 à T60 aux images 301, 856, 1368, 1624, 2008 et le `0x53` d'A3 à 2262, celui d'A7 à 1759. Temps d'exécution : A3 environ 1 s (2263 images), A7 environ 0,4 s (1760 images), A4p environ 0,5 s ; l'autotest 1 s.)* : A3, A7 et A4p passent avec les valeurs de T2 ; ils sont commités. Leur temps
  d'exécution est relevé.
- **T6 — `ForceAdjusted` sur Y et en sens négatif** ⏳ (P3 reporté par E19.a3) : même montage que
  `BuildSailorBesideTheEastWall` (champ réel de la 389, contrôleur de la banque 146, boîte 18 × 12, masque
  0x41, une image d'enregistrement, appels directs de `MoveControllerAndPullPosition`) :
  - nord, pion en (516, 650, 80) : `Move(0, −10)` donne y = 646,0 (42336256) et `ForceAdjusted` 0 ;
    `Move(0, −2)` laisse y et lève le drapeau ; `Move(0, +1)` le laisse à 0 ; de retour au contact,
    `Move(+1, −2)` avance X de 1 et lève le drapeau ;
  - sud, pion en (492, 648, 80) : `Move(0, +10)` donne y = 650,0 (42598400) et 0 ; `Move(0, +2)` lève
    le drapeau ;
  - ouest, pion en (545, 632, 80) : `Move(−10, 0)` donne x = 537,0 (35192832) et 0 ; `Move(−2, 0)` lève
    le drapeau ;
  - le test vers l'est est resserré : il avance d'exactement 3,0 px (contact à 567,0).
- **T7 — Hygiène reportée par E19.b** ⏳ :
  - un test unitaire de `0x8A` avec des octets forts non nuls sur Y et Z ;
  - A2 et A4 vérifient les parents (bloc → héros, Rancune → bloc) ;
  - le fragment « Tu » de `S102` remplacé par un fragment distinctif d'au moins 15 caractères du nœud ;
  - la doc de classe et les commentaires de cas d'`EntitySearchService` disent « entité de référence »
    au lieu d'« owner » ; la doc d'`OpenDialog` cite `0xC4` ;
  - le commentaire d'`AlundraWorldProxy.cs:773-778` dit que l'ajustement au sol à l'apparition ne fait
    rien en production (`Entity.World` est nul à ce moment ; O-E19-6).
- **T8 — Docs** ⏳ : statuts de ce plan, mesures au §2, points ouverts, ligne E19 du plan maître ; les
  textes qui annonçaient la correction du retard en E19.c disent qu'il est gardé (D-E19-13).
- **T9 — Recette en jeu (auteur)** ⏳ :
  1. **La 478, en jouant** : après la cabine et la 476, la 478 montre la caméra sur le bloc qui monte
     lentement, pendant que les PNJ marchent (Talis, le bloc et son chien, Yuri qui tourne sur
     lui-même). Après environ 45 s, fondu, retour à la 476 (huit boîtes), puis la 392 se charge.
  2. **La 416, par raccourci** : F6, puis dans `debug-json.sav` : `initialMapId` 416, `cameraTileX`,
     `cameraTileY` et `cameraTileZ` à 0, aucun drapeau, puis F9. Alundra est allongé ; la caméra glisse
     au nord pendant environ 8 s ; Jess apparaît, marche jusqu'à Alundra, s'arrête, puis la 163 se
     charge.

**Lancement des tests.** Comme avant, au premier plan. Les lancements qui contiennent des arcs en
préfabs prennent `--blame-hang-timeout 300s` ; un arc qui dépasse 120 s est un arrêt. À la fin : une
course en Release, puis la build Debug et une course Debug, et la DLL déployée dans `alundra-project/`
est la Debug.

**Acceptation d'E19.c1.**
1. L'autotest du mode préfabs échoue puis passe ; les arcs et épingles existants ne bougent pas.
2. A3 et A7 échouent comme le dit la table avant T3 et T4, puis passent avec les valeurs de T2 ; A4p
   passe avec les siennes.
3. Les tests unitaires de T3, T4, T6 et T7 passent.
4. `Alundra.Tests` et les tests du convertisseur passent à 0 échec. Le moteur, les traces du héros et
   la trace de l'intro ne changent pas.
5. Un verifier frais rend CONFIRMED sur 1 à 4.
6. La recette T9 de l'auteur.

**Risques.**
- `0x0B` n'a pas de sortie en cas de blocage. Dans le jeu, un mur ou un corps d'entité que l'original
  n'a pas figerait la 416 (son `0x53` attend les marches). Les données l'excluent, et les arcs en
  préfabs le testent avec les vrais contrôleurs.
- Les vrais préfabs peuvent révéler un blocage propre au jeu, par exemple un PNJ qui en bloque un
  autre, ou le chien de la 478 que `0x89` téléporte à chaque tick dans le corps collidable du bloc 11.
  Un tel blocage est un arrêt : il se soumet à l'auteur avant toute correction.
- Avec un gestionnaire d'assets, le présentateur de dialogue change de chemin (`DialogueScreen`) : les
  huit boîtes d'A4p le vérifient.
- Les sprites des préfabs journalisent des erreurs de résolution à chaque animation (pas de
  `SpriteData` en test) ; le volume et la durée sont relevés en T1.
- Avec le retard gardé, une relance Hold par `0x1C` sur un PNJ **qui se déplace** donne un tick à
  vitesse nulle (`CurrentAnimationId` = ~`TargetAnimationId` n'a pas de jeu d'animation). Aucun site
  de la chaîne n'est dans ce cas : Ronan ne bouge pas.
- Les dix opcodes passent en vrai dans tout le corpus : 1246 sites de `0x0B`, 582 de `0x74`, 120 de
  `0x5E`, 86 de `0x89`. La 391 change en jeu (les marins sautent, une boucle `0x73`/`0x74` retarde son
  `0x53` d'environ 38 images) et la 392 tourne le héros au hasard dans sa boucle de roulis : c'est
  fidèle, et c'est vérifié en E19.d.
- Les valeurs des arcs viennent de deux modèles indépendants de la DLL, pas d'une course. Une valeur
  contredite est un arrêt, jamais une ré-épingle.

**Revues** : plan-verifier sur cette section, avant approbation ; verifier frais après exécution.

**Relectures du 2026-10-01.**
- **Plan-verifier** (`34379a3`) : **REVISE**, un P2 : la case rouge d'A7 après T1 (« atteint `0x53`
  puis échoue sur les opcodes sautés ») ne suivait pas l'ordre écrit des vérifications, où l'image de
  fin attendue (1759) venait d'abord. Le même défaut avait touché E19.b.
- **Corrigé** : T2 fixe l'ordre des vérifications (signal de fin, puis opcodes sautés ou dépassés, puis
  le reste), et la table du rouge le suit.
- **Relecture neuve de la révision** (`5a2fffa`) : **READY**.

### 1.2f E19.c2 — Horloge d'animation exacte en ticks ⏳ (esquisse ; se détaille après E19.c1)

**But.** Les fins d'animation et les changements d'image tombent au tick de l'original (D-E19-16).

**Faits établis** (découverte du 2026-10-01, contre-vérifiée ; scratchpad `e19c/animclock.md`) :
- le moteur avance les sprites en secondes réelles, en float32, avant les scripts et hors du gel du
  jeu. À 0,02 s par image, sur l'export : 1010 des 4413 animations Once finissent un tick trop tôt,
  3109 des 5202 Loop bouclent un tick trop tard, 197 Loop ne bouclent plus jamais et montrent la pose
  cachée de fin (défaut du moteur, O-E19-10) ; le cycle de marche du héros dure 31 ticks au lieu de
  30 ;
- les délais entiers survivent à l'export : chaque instant de clé est à moins de 1,02e-4 tick de la
  grille à 50 Hz. Aucun changement de format ni du convertisseur n'est nécessaire ;
- dans le binaire, `UpdateAnimation` tourne après les scripts et avant la physique, et pas du tout
  quand le jeu est gelé.

**Proposition** : un mode « horloge en ticks », optionnel, du `AnimatedSpriteComponent` et de son
échantillonneur (moteur, API additive, ADR-0046 du moteur, plan dans
`CasaEngineMonogame/ai-agent/tasks/`), avancé par la DLL d'un tick à chaque tick logique, dans le gel,
juste après les scripts du tick. Un modèle de ce pilotage redonne toutes les fins du binaire à 0 ou 1
tick par image (Ronan `s+25`, tour de 66 ; héros 83 `[3]` `s+99`, puis 85 `[1]` `+41` ; Jess `s+25`).
Le signal de fin de boucle en sort sans coût.

**Questions à trancher avec son plan** : base de la branche du moteur (`chantier/field-move-to-contact`
n'est pas mergée, O-E19-12) ; règles de l'ADR (une Once finit à la D-ième avance, une Loop boucle à la
D-ième, clés arrondies au tick) ; garde de `0x1C` sous rattrapage (2 ticks ou plus par image) ; le pont
du signal de boucle dans E19.c2 ou E19.i ; changements visibles (les sprites changent d'image sur les
ticks logiques, et se figent pendant le départ d'un portail).

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
| A3 | 478 | `G1641` (mot 51 : 512) | arrivée | T20 à T60 posés, puis `0x53 @245` vers 476 (§1.2e) | 2700 | E19.c1 |
| A4 | 476 | `G1641` | arrivée | `0x53 @986` vers 392 | E19.b | E19.b |
| A5 | 392 | — | couloir du portail | portail vers 391 | E19.d | E19.d |
| A6 | 391 | — | arrivée | le `0x53` vers 416 | E19.d | E19.d |
| A7 | 416 | — | arrivée | `0x53` de `C[1] @640` vers 163 (§1.2e) | 2200 | E19.c1 |
| A4p | 476 | `G1641` | comme A4 | comme A4, en vrais préfabs (§1.2e) | 2500 | E19.c1 |
| A8 | 163 | — | arrivée | `G0`, `0x11 @201`, `G1662`, livre présent | E19.d | E19.d |

Les valeurs exactes de chaque arrivée se décodent des opcodes `0x53` cités et s'écrivent dans le test
avant le code.

---

## 2. Mesures

Réservé aux mesures faites en exécutant les tranches.

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E19-1 | ~~Portails trou et escalier de la 390~~ — **réglé par la recette du 2026-09-29** : le journal montre le passage par le portail 5, qui charge la pièce B. Question d'origine : si la recette d'E19.a montre que le héros ne suit pas le capitaine par là, faut-il corriger dans E19 ou dans un chantier de transitions ? | E19.a (recette) |
| O-E19-2 | Nouveaux écarts de la décompilation relevés dans le binaire : `0x5F` (entité et taille), `0x66` (sens de la copie), compteur de `0x1C`, `y` de la boîte de nom, portrait de `Script_196_0C4`, test de zone de `GetMapEffectRecord`, `AddOneItemIfUnlocked`, `InitializeEventData`. Le portage suit le binaire. Faut-il aussi corriger la décompilation dans l'analyseur, comme pour la taille de `0x78` en E16.a ? | E19.m |
| O-E19-3 | ~~Déplacement vertical des entités nues dans le support d'arcs~~ — **tranché le 2026-10-01 (D-E19-14)** : les arcs chargent les vrais préfabs par un gestionnaire d'assets construit par le test. | E19.c1 |
| O-E19-5 | Quand un `0x5B [x,0,dir]` arrête un PNJ au tick où sa marche se termine, le mouvement de ce tick s'applique encore avec la nouvelle direction : le marin 12 de la 389 descend de 1,25 px au tick de `@1494` (mesuré en E19.a3 : y = 843,25 à l'image de `@1494`, contre 842,0 à l'image précédente, lue par le test du marin 12). Cela peut venir de la latence d'une image de `CurrentAnimationId` des PNJ, déjà relevée (le moteur synchronise l'animation en fin d'image). **Tranché par le binaire le 2026-10-01 (§1.2d, [binaire])** : l'original exécute dans l'ordre les événements de carte, les entités, `UpdateAnimation` puis la physique (`0x8002E100`, `0x8003B388` → `0x8003B3D8` → `0x8003B3E0`) ; un changement d'animation par script s'applique donc dans la physique du même tick. La DLL a une image de retard : c'est la cause du pas de 1,25 px du marin 12, et chaque panoramique du bloc de la 476 fera 48,75 px au lieu de 48. La correction reste dans E19.c. **Précisé puis tranché le 2026-10-01** : chaque marche du bloc fait bien 48,0 px, mais la première de chaque panoramique dure un tick de plus et le bloc dépasse de 0,75 px après chaque panoramique. L'auteur garde ce retard (D-E19-13, ADR-0018) : le corriger aurait déplacé des points épinglés de l'intro. | E19.c1 (clos) |
| O-E19-6 | À l'apparition, l'ajustement au sol (`ClampToGround`) et `TerrainHeight` ne font rien en production : `World.AddEntity` ne fait que mettre l'entité en file, et `Entity.World` n'est posé qu'à l'intégration suivante. Le commentaire d'`AlundraWorldProxy.cs:773-778` dit le contraire (corrigé en E19.c1 T7). Faut-il corriger le comportement ? | E19.h |
| O-E19-7 | Une entité sans contrôleur ne bouge jamais en Z dans la DLL, alors que le binaire intègre Z pour toute entité active (`MoveEntity` `0x80037E34` → `ComputeZPosition`). Sur la chaîne, tous les enregistrements ont un contrôleur. | E19.h |
| O-E19-8 | `IsZForceApplied` (`+0xF8`) n'est pas porté : au tick d'un changement d'animation, le binaire remplace `ForceZ` par la valeur du jeu d'animation (131 des 395 enregistrements de sprite en ont une non nulle). Il suppose l'animation résolue avant la physique, ce que D-E19-13 ne fait pas. | E19.h |
| O-E19-9 | Les 5 animations Loop de durée 0 de l'export (banque 127 anim 0 gauche et droite, banque 151 anim 1 haut, gauche et bas) sont invisibles dans le moteur : la clé cachée de fin tombe au même instant 0 que l'image. Le binaire montre l'image figée. À corriger au convertisseur (export complet à relancer). | à placer |
| O-E19-10 | Défaut du moteur sur le chemin en temps réel : à 0,02 s par image, 197 des 5202 Loop de l'export ne bouclent plus jamais (le temps tombe pile sur la durée, puis la dépasse), et le sprite montre la pose cachée de fin (exemple : animations 53 et 55 du héros). | E19.c2 |
| O-E19-11 | La remise à zéro hors zone d'un événement de carte diffère du binaire : la DLL écrit sur l'entité de l'événement et ne remet pas `mapEvent.EventData` à zéro, le binaire (`0x8003C7F0`-`0x8003C804`) remet le pc et l'entrée de l'état de l'événement, `state+0x2C`, l'entité logique et l'octet de programme. Un programme B réentré reprend dans la DLL et recommence dans le binaire. Sans effet sur la 478 et la 416 (zones de toute la carte). | E19.j |
| O-E19-12 | Base de la branche moteur d'E19.c2 : `chantier/field-move-to-contact` (E19.a2) n'est pas mergée dans `main` du moteur, et le parent la pointe. Partir d'elle, ou attendre son merge ? | E19.c2 (auteur) |
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
