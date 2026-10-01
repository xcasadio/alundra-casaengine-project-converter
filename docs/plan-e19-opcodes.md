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
- **D-E19-17 à D-E19-20** (2026-10-01, après la vérification d'E19.c1) — ADR-0019 :
  - **D-E19-17** — Le rendu des sprites reste en temps réel ; seules les fins logiques (fin Hold, fin
    Chain, tour de Loop) deviennent exactes au tick. Précise D-E19-16 : l'auteur refuse que les sprites
    changent d'image sur les ticks logiques et se figent pendant le départ d'un portail.
  - **D-E19-18** — Le signal de tour de boucle arrive dans E19.c2 et corrige le blocage introduit par
    E19.c1 (`0x1C`/`0x1D` sur une Loop). E19.c1 ne se merge pas sans E19.c2.
  - **D-E19-19** — Le défaut du moteur qui fige des Loop sur le chemin en temps réel (O-E19-10) se
    corrige dans E19.c2.
  - **D-E19-20** — La branche moteur d'E19.c2 part de `main`, après le merge de
    `chantier/field-move-to-contact` par l'auteur (fait le 2026-10-01).
- **D-E19-21 à D-E19-23** (2026-10-01, avant le plan d'E19.d) — ADR-0020 :
  - **D-E19-21** — `0x24` se porte comme le binaire, avec un **recensement statique** de ses 395 sites ; un
    site du chemin de l'histoire qui risque de ne jamais finir arrête l'exécution et se soumet à l'auteur.
  - **D-E19-22** — `0x40` et `0x41` se portent **complètement** dans E19.d, les deux cas d'effacement de
    l'état compris ; un numéro de programme hors limites (v1 ≥ 6, aucun site) est ignoré avec un
    avertissement : on corrige l'écriture hors tableau de l'original. E19.j ne garde que le réarmement hors
    zone.
  - **D-E19-23** — L'arc de la 392 replace Alundra dans le couloir du portail après le retour de la main,
    au lieu de le faire marcher à travers les caisses.

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
- `UnknownOpcode_KnownSize_SkipsBySize` (`AlundraEventProgramRunnerTests.cs:320`) utilisait `0x08` : il
  utilise `0x4C` (la machine à écrire, E12.c) depuis E19.c1 T3, car `0x08` est porté.

---

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Arcs de test (§1.3) | Recette en jeu |
|---|---|---|---|
| **E19.a** ✅ | Entité de contexte (`0x42`, `0x43`, et tous les opcodes sur l'entité logique), `0x59`, garde de boucle (D-E19-3), support des arcs | A0, A0b, A1 | Le capitaine sort par l'escalier et réapparaît en pièce B ; sommeil, puis 476 |
| E19.a2 ✅ | Moteur : sur le champ de cellules, un pas bloqué avance jusqu'au contact (D-E19-8) ; épingles et traces de référence du héros re-mesurées ; la cabine testée avec un vrai contrôleur | cabine seule, A1c | La cabine : Alundra s'endort, puis la 476 se charge |
| E19.a3 ✅ | DLL : `ForceAdjusted` ne se lève qu'au tick sans avance, comme le binaire (D-E19-12) ; épingles du héros re-mesurées | marin 12 de la 389 | Le marin 12 rejoint sa place en fin d'intro |
| E19.b 🧪 | Carte 476 : `0xC4` sans nom (D-E19-5), `0x8A` (bloc caméra), `0x4C` gardé pour la machine à écrire | A2, A4 | La vision de Lars et Melzas jusqu'à 478, puis jusqu'à 392 |
| E19.c1 🧪 | Cartes 478 et 416 : `0x0B` avec détour (D-E19-6), `0x1C`/`0x1D` (compteur du binaire, Chain et Hold), `0x5E`, `0x08`, `0x0C`, `0x3A`, `0x89`, `0x73`/`0x74` ; arcs en vrais préfabs (D-E19-14) | A3, A7, A4p | La vision de 478 va au bout ; la plage 416 mène à la 163 |
| E19.c2 🧪 | Moteur : horloge logique exacte des fins d'animation, rendu en temps réel (D-E19-16, D-E19-17), correction des Loop figées ; DLL : pilotage à chaque tick logique, signal de boucle (D-E19-18), garde de `0x1C` sous rattrapage | A3, A9, tests moteur | Wendell à Inoa rend la main ; les fins d'animation au tick de l'original |
| E19.d | Fin de chaîne : `0x24` avec recensement (D-E19-21), `0x40`/`0x41` complets (D-E19-22), défaut d'atterrissage de la DLL (391), vrai héros et pad tenu dans les arcs ; reste de 392, 391 et 163 | A5, A5r, A6, A8 | Naufrage, plage, réveil à Inoa, main rendue |
| E19.e | Recette de bout en bout, plus un test statique : aucun opcode sauté sur la chaîne hors liste d'exceptions | toute la chaîne | Nouvelle partie jusqu'au livre de la 163, sauvegarde, rechargement (avec les recettes d'E16 en attente) |
| E19.f | Boîte de nom et boîte de texte fidèle (D-E19-4) : export du cadre, écrans XAML liés à un view model, cycle de vie de la boîte de nom, pour `0x0D`/`0x5C`/`0xC4` | tests MGDesktop | Les noms s'affichent au-dessus de la boîte, à la place de l'original |
| E19.g | Effets visuels (D-E19-7) : export des effets par le convertisseur, réserve de 128 effets aux règles du binaire, `0x90`-`0x94`, `0xA0`-`0xA3`, rendu | cartes à effets | L'aura de 476, les vagues de 391 |
| E19.h | Attentes en Z et contacts : `0x20`-`0x23`, `0x25`, `0x26`, `0x47`, `0x48` ; `CollidedWithEntityZ` et `ForceAdjusted` alignés sur le binaire | ciblés | ciblée |
| E19.i | ~~Boucles d'animation Loop pour `0x1C`/`0x1D`~~ — **absorbée par E19.c2** (D-E19-18) : le signal de boucle et son pont y arrivent ; le recensement exact est de 208 sites dans 53 cartes, et non 101 dans 30 | — | — |
| E19.j | Événements de carte : réarmement hors zone du binaire (619 enregistrements) ; (`0x40`/`0x41` complets : faits en E19.d, D-E19-22) | ciblés | ciblée |
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
  - chaque panoramique du bloc fait 48,75 px au lieu de 48 dans la DLL. L'auteur garde ce retard (D-E19-13, ADR-0018), et E19.c1 en épingle les valeurs : il n'est pas corrigé en E19.c.
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

### 1.2e E19.c1 — Cartes 478 et 416 : attentes de mouvement, arcs en vrais préfabs 🧪 (code et arcs faits et vérifiés CONFIRMED le 2026-10-01 ; le P1 introduit est corrigé par E19.c2 (code fait le 2026-10-01), sans merge d'E19.c1 avant celui d'E19.c2 ; reste la recette T9)

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
- **T6 — `ForceAdjusted` sur Y et en sens négatif** ✅ *(fait le 2026-10-01, tests dans `AlundraNpcCharacterControllerMoverTests` ; toutes les valeurs écrites tiennent : 646,0, 650,0, 537,0 et le contact à 567,0. Précision de lecture : « `Move(0, +1)` le laisse à 0 » se lit après la remise à zéro de la passe par image, car un `Move` ne l'efface jamais lui-même ; le test la fait explicitement.)* (P3 reporté par E19.a3) : même montage que
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
- **T7 — Hygiène reportée par E19.b** ✅ *(fait le 2026-10-01 ; le fragment retenu pour `S102` est le texte entier du nœud, 15 caractères dont le glyphe `U+0012`, `Tu` + glyphe + ` m'entends ?`, car le texte n'a pas plus de caractères ; les parents sont vérifiés par A2, A4 et A4p)* :
  - un test unitaire de `0x8A` avec des octets forts non nuls sur Y et Z ;
  - A2 et A4 vérifient les parents (bloc → héros, Rancune → bloc) ;
  - le fragment « Tu » de `S102` remplacé par un fragment distinctif d'au moins 15 caractères du nœud ;
  - la doc de classe et les commentaires de cas d'`EntitySearchService` disent « entité de référence »
    au lieu d'« owner » ; la doc d'`OpenDialog` cite `0xC4` ;
  - le commentaire d'`AlundraWorldProxy.cs:773-778` dit que l'ajustement au sol à l'apparition ne fait
    rien en production (`Entity.World` est nul à ce moment ; O-E19-6).
- **T8 — Docs** ✅ *(fait le 2026-10-01)* : statuts de ce plan, mesures au §2, points ouverts, ligne E19 du plan maître ; les
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

**Vérification d'E19.c1 (2026-10-01).**
- **Commits** : `d1d2bea` (T1), `7d6d8ce` (T3), `53b1503` (T4), `35feaa5` (T5, arcs), `767c6a5` (T6),
  `6023e4c` (T7), `72a2b20` (T8). Le pointeur du moteur ne change pas.
- **Verifier frais : CONFIRMED** sur les acceptations 1 à 4. `Alundra.Tests` 1995/1995 en Release,
  convertisseur 400/400, dans un arbre jetable hors du dépôt.
  - Le rouge de la table se reproduit : sans `0x5E`, A3 échoue sur `0x36 @180` ; sans `0x0B`, A7 atteint
    son `0x53` puis échoue sur les opcodes sautés ; sans `0x1C`/`0x1D`, A3 atteint `0x53 @245` puis échoue
    sur `0x1C @854`. Sans l'installation des préfabs, l'autotest échoue.
  - Une mutation par famille est attrapée : le `+1` sur Z de `0x89`, la borne de `0x74`, les opérandes et
    l'inégalité de `0x0B`, l'incrément Chain du pont, l'effacement du drapeau par `SyncAnimation`, la
    remise à zéro du compteur de la décompilation, le masque de `0x08`, la table de `0x3A`, le décalage
    de `0x0C`, l'extension de signe de `0x5E`.
  - Traces du héros et de l'intro inchangées ; DLL Debug déployée.
- **Trois contradicteurs en lecture seule** (fidélité au binaire, support d'arcs, corpus). La fidélité
  au binaire des dix gestionnaires et du pont est confirmée instruction par instruction. Dispositions :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | `0x1C`/`0x1D` attendent désormais sans fin sur une animation Loop : la DLL n'a pas de signal de tour de boucle, alors que le binaire compte chaque tour (`0x80038D70`-`0x80038D7C`). Avant E19.c1, ces opcodes étaient sautés et le script passait. Quand le script tient la main du joueur (`0x10`), c'est un **blocage définitif** : vérifié sur la 172 (Inoa, après la 163), `0x10 @530`, `0x1A [11] @537` (Loop), `0x1C [1] @539`, `0x11 @547` jamais atteint. Même cas sur la 165 et la 179 (Wendell), et sur 138, 115, 440, 396 ; 28 sites dans 11 cartes. | **P1, introduit** | **Décidé par l'auteur le 2026-10-01 (D-E19-18)** : corrigé par le signal de boucle d'E19.c2 ; E19.c1 ne se merge pas sans E19.c2. La 478, la 416, la 476 et la 163 ne sont pas touchées : la recette T9 reste possible. Recensement exact en E19.c2 : 36 blocages dans 15 cartes. **Corrigé en E19.c2 (2026-10-01)** : le signal de tour de boucle et l'horloge logique ; A9 le prouve sur la 172 (`0x11 @547` à F530 + 91). Reste la recette C8. |
  | Même cause hors blocage : des programmes d'interrupteurs, de plaques et de PNJ restent figés et ne posent plus leurs drapeaux ni leurs apparitions (sanctuaires 29 et 30, Magyscar 92, sanctuaire du lac 337 et 343, Meia 306, 440, 441). 141 sites Loop dans 40 cartes, et non 101 dans 30 comme le disait E19.i. | P2, introduit | Même décision que le P1. Recensement exact en E19.c2 : 208 sites exposés dans 53 cartes, dont 145 qui commandent un effet dans 27 cartes. **Corrigé en E19.c2 (2026-10-01)** par le même mécanisme ; les tests couvrent le mécanisme (T-D3, T-D5, T-D10, T-D15), le corpus se vérifie en jouant (C8). |
  | Sous rattrapage (2 ticks ou plus par image), `0x1C` compte une fin Hold à chaque tick, et un PNJ relancé reste à vitesse nulle jusqu'à 3 ticks. Le test unitaire de T4 épingle ce comptage. | P3, introduit | Reporté : la garde de `0x1C` sous rattrapage est prévue dans E19.c2. **Corrigé en E19.c2 (C3, 2026-10-01)** pour le comptage (TG2, T-D2 et T-D3 en 2 t/i) ; le tick à vitesse nulle d'un PNJ relancé reste, écart gardé (D-E19-13). |
  | Une relance `0x1C` depuis un événement de carte laisse `CurrentAnimationId` = ~`TargetAnimationId` visible une image : la passe de tri lit un biais IDSV nul. | P4 | Reporté à E19.c2 (pilotage des animations). **Corrigé en E19.c2 (C4, 2026-10-01)** : repli de la passe de tri sur la cible, T-D18. |
  | Une fin Chain qui tombe à l'image même du premier appel de `0x1C` est perdue (l'événement du sprite arrive avant le script). | P4 | Reporté à E19.c2 (le sprite avancera après les scripts). **Corrigé en E19.c2 (2026-10-01)** : l'horloge logique avance après le script de l'entité ; T-D7 (le premier `0x1C` à s+24, rend 2 à s+25). |
  | `0x0B` sur un bloc de vitesse nulle, qui attend d'être poussé (435, `@547`) : attend sans fin, la poussée entre entités n'étant pas portée. | P4, introduit | Reporté (poussée : E14 ou E19.h) ; il ne perd que le `0x1A [0]` qui suit. |
  | Le mode préfabs construit un `ProjectSettings` vide : A4p ne passe pas par le gabarit de boîte du projet ni par font3, et vérifie ses boîtes sur le directeur. La ligne de risque « les huit boîtes d'A4p le vérifient » en disait trop. | P4 | Accepté ; la boîte fidèle vient avec E19.f. |
  | `ArcRun.ResetAll` ne remet pas `AlundraRandom` à zéro, alors que `0x0C` le consomme désormais. Aucune carte d'arc actuelle n'a de `0x0C`. | P4 | Reporté à E19.d (la 392 en a un). |
  | A7 ne vérifie l'immobilité du héros qu'à `@421` et à la fin. | P4 | Reporté (hygiène d'E19.c2). **Fait en E19.c2 (C5, C6, 2026-10-01)** : le héros ne bouge sur aucune des plus de 1000 images de `@421` à la fin. |
  | Libellé de `0x73` « Set program counter _30 » : c'est un compteur de boucle. Coquille « the / the » dans la doc d'`EntitySearchService`. | P4 | Reportés (hygiène d'E19.c2). **Faits en E19.c2 (C5, 2026-10-01).** |
  | `0x0B`, `0x1C` et `0x1D` prennent `CodeIndex` pour clé : une attente au pc 0 se croirait déjà mémorisée. Aucun site au pc 0 dans les 483 cartes. | P4 | Accepté, comme pour `Wait`. |
  | T6 : « `Move(0, +1)` le laisse à 0 » se lit avec la remise à zéro par image, car un `Move` ne fait que lever le drapeau. | — | Précision acceptée ; valeurs inchangées. |

### 1.2f E19.c2 — Fins d'animation exactes et signal de boucle 🧪 (C0 à C7 faites et vérifiées CONFIRMED le 2026-10-01, moteur et parent ensemble ; reste la recette C8 de l'auteur)

**But.**
- `0x1C` et `0x1D` voient les fins Hold, les fins Chain et les tours de Loop au tick de l'original. Le
  blocage d'Inoa et les programmes figés introduits par E19.c1 (P1 et P2) disparaissent.
- Le rendu des sprites reste en temps réel (D-E19-17) : seule la fin logique devient exacte.
- Le défaut du moteur qui fige 197 Loop et les rend invisibles est corrigé (D-E19-19).

**Périmètre.**
- Moteur : plan `CasaEngineMonogame/ai-agent/tasks/animation-logical-end-clock-tasks.md`, branche
  `chantier/animation-logical-end-clock` partie de `main` `74e97293`, ADR-0046 du moteur.
- DLL : `AlundraEntityScriptProxy.cs`, `AlundraFrameSyncPasses.cs`, `AlundraEntitySpawnFactory.cs`,
  `AlundraEventProgramRunner.cs` (`RepeatAnimation`), `AlundraWorldProxy.cs` (passes des événements de
  carte et passe de tri), docs de `AlundraGameplayFreeze.cs`, hygiène reportée par E19.c1.
- Tests (`Alundra.Tests`, `CasaEngine.Tests`), docs, ADR-0019.

**Faits établis** (découverte en lecture seule du 2026-10-01 : trois volets, chacun contre-vérifié ;
scratchpad `e19c2/`) :

- **Binaire** **[binaire]** :
  - à chaque tick : événements de carte, scripts des entités, `UpdateAnimation`, physique ; rien de cela
    quand le jeu est gelé ;
  - un changement d'animation au tick s montre l'image 0 sans décompter : l'ancienne animation ne reçoit
    ni tick ni fin à ce tick ;
  - la fin est traitée à s+D : Hold pose le drapeau ; Chain change de cible et fait le compteur + 1 ; Loop
    fait le compteur + 1 (`0x80038D70`-`0x80038D7C`). Les scripts la voient à s+D+1 ;
  - `1A A ; 1C [n]` lancé au tick s se termine à : Hold s + n × (D+1) ; Chain (n = 1) s+D+1 ; Loop s + n ×
    D + 1 ; Chain (D1) puis Loop (D2) s + D1 + (n−1) × D2 + 1 ;
  - l'indice d'animation n'est jamais borné (`0x80038B18`-`0x80038B58`).
- **Moteur** **[moteur]** : l'horloge des sprites est le temps réel en float32, avant les scripts. À 0,02 s
  par image, 1010 des 4413 Once finissent à la D-ième mise à jour (vue par les scripts un tick trop tôt) et
  3403 à la (D+1)-ième ; 1897 des 5205 Loop bouclent à la D-ième, 3111 à la (D+1)-ième, et 197 jamais
  (cause et correction : plan moteur). Les délais entiers se retrouvent exactement par arrondi.
- **DLL** **[DLL]** :
  - le pont des fins est abonné à `AnimationFinished`, levé par le composant avant les scripts ; aucun
    signal de Loop ;
  - `SyncAnimation` change d'animation une fois par image, à la fin (gardé, D-E19-13) ;
  - les événements de carte tournent après toutes les entités ;
  - `IsPlaybackPaused` n'est posé qu'en fin d'image : l'horloge logique ne doit pas en dépendre.
- **Corpus** **[données]** — 2071 sites de `0x1C`/`0x1D` atteignables, classés par la fin attendue :
  - Hold 888, Chain 973, Hold et Chain 2, Loop 189, animation héritée 16, animation absente 3 ;
  - **exposés au Loop : 208 sites dans 53 cartes**, dont les 4 de la 113 (le rêve de Nestus) que la
    vérification d'E19.c1 n'avait pas vus ;
  - blocages du joueur : 36 sites dans 15 cartes (29 où le programme tient lui-même la main, 7 par un
    drapeau) ; sans blocage, 145 sites dans 27 cartes posent des drapeaux, font apparaître, détruisent ou
    changent de carte ;
  - après la 163, sur le chemin de l'histoire : l'animation 11 de Wendell (Loop de 90 ticks, fin du binaire
    à s+91) sur la 165 (3 sites, dont la première visite de sa maison), la 172 et la 179 ;
  - 30 sites dans 12 cartes attendent une Loop que le chemin en temps réel ne boucle jamais (grilles et
    portes de 16 ticks) : **le tour doit venir de l'horloge logique** ;
  - **animation absente** : les Flammes de l'antre de Nirude (préfab `0edffd14`, cartes 35 `@1228`, 38
    `@850`, 39 `@726`, 11 entités) font `1A [9]` sur une banque de 9 jeux. L'original lit au-delà de la
    table : vers le bas (toutes les Flammes regardent vers le bas), cela donne les images vers le bas de
    l'animation 0, une Loop de 12 ticks, et la fin à s+13. Dans la DLL, la sélection échoue et l'attente
    dure sans fin depuis E19.c1 ;
  - aucun site n'attend une animation de durée nulle ; aucun acteur n'est sans sprite en production.

**Conception.**

- **Moteur** : horloge logique optionnelle sur `AnimatedSpriteComponent` (détail dans le plan moteur).
  Quand elle est active, elle seule lève `AnimationFinished` (Once, à la D-ième avance) et `AnimationLooped`
  (Loop, à chaque D-ième avance). Le rendu reste en temps réel.
- **Pilotage par la DLL** : `StepAnimationClock(proxy)` une fois par tick logique, dans le bloc soumis au gel
  :
  - pour un PNJ, entre `RunPickedEvent` et `TickScriptedNpc` ; pour le héros, à chaque tick, avant
    `Tick(this, 1)` (qui ne tourne qu'avec un contrôleur), même sans contrôleur ;
  - un changement en attente — `PendingChainRestartFlag` ≠ 0, ou `TryResolveAnimationTarget` qui
    changerait l'animation, c'est-à-dire **une autre cible ou une autre direction**, comme le binaire
    (`0x80038B08` et `0x80038B10`) — **réserve le tick** et efface le tick dû : l'ancienne animation
    n'avance pas et ne finit pas ;
  - sinon, un tick dû est consommé sans avancer ;
  - sinon, `AdvanceLogicalTicks(1)` ; une fin Chain levée par cette avance réserve le tick ;
  - `SyncAnimation`, toujours en fin d'image : sur un changement sans tick réservé (image sans tick
    logique), il pose le tick dû ; avec plus d'un tick réservé (rattrapage), il avance la nouvelle
    animation des ticks réservés moins un ; il remet le compte des ticks réservés à zéro à chaque appel ;
  - entre les passes 2 à k des événements de carte d'une même image, le drapeau Hold des entités dont un
    changement est en attente est effacé, comme le ferait l'`UpdateAnimation` intermédiaire du binaire
    (`0x80038B6C`) ; sans effet à 0 ou 1 tick par image.
- **Pont** :
  - `SubscribeAnimationEndBridge` met le sprite à 50 ticks par seconde et s'abonne à `AnimationLooped` par
    un délégué statique en cache : chaque levée fait `AnimCompleteCounter` + 1, sans table, comme le
    binaire qui compte les images qui jouent ;
  - Hold et Chain restent sur `AnimationFinished`, levé désormais par la seule horloge logique : pas de
    double fin ;
  - quand la sélection d'animation échoue (animation absente du préfab), `SyncAnimation` relance
    l'animation courante du composant quand il en a une (`SetCurrentAnimation(…, forceReset: true)`), et
    ne fait rien sinon. Pour les Flammes, c'est exactement
    l'original : les images de l'animation 9 vers le bas sont celles de l'animation 0, reprises à l'image
    0, d'où la fin à s+13.
- **Garde de `0x1C` sous rattrapage** : le drapeau Hold est invisible pour `RepeatAnimation` tant qu'un
  tick de changement est réservé et pas encore fait, ou qu'une fin Hold déjà comptée attend son
  changement. Sans effet à 0 ou 1 tick par image.
- **Tri** : quand `CurrentAnimationId` = ~`TargetAnimationId` (relance par `0x1C` depuis un événement de
  carte), la passe de tri lit `TargetAnimationId` (P4 d'E19.c1).
- **Clonage** : les nouveaux champs du proxy (sprite à horloge, ticks réservés, tick dû, marque de fin
  comptée) ne sont pas copiés.
- **Écarts gardés** : le retard d'animation (D-E19-13) et son tick à vitesse nulle après un comptage Hold
  (sous rattrapage, jusqu'à la fin de l'image, au plus k−1 ticks, comme le P3 d'E19.c1) ; sous rattrapage (2 ticks ou plus par image), des résidus bornés pour les programmes des événements de
  carte et pour les programmes C qui agissent sur une autre entité (`0x42`/`0x43`). Le rendu et la fin
  logique peuvent différer d'environ un tick (D-E19-17).

**Tâches.** Chaque tâche porte son icône de statut et se commite avec la mise à jour de ce plan (ou du plan
moteur pour C0).

- **C0 — Moteur** ✅ *(fait le 2026-10-01 par un autre exécutant : commits moteur `1561fd07`, `cc6f498e`, `34ddf5e2`,
  `b40888f1` sur `chantier/animation-logical-end-clock` ; toutes les valeurs écrites d'avance tenues. `Alundra.Tests` sur le moteur
  de C0 avant tout changement de la DLL : 1995 réussis, 0 échec, traces inchangées)* : exécuter le plan moteur (T0.1 à T3.1) sur sa
  branche. `CasaEngine.Tests` 0 échec, aucun test existant modifié.
- **C1 — Preuves rouges** ✅ *(fait le 2026-10-01, non commité : les résultats mesurés sur la DLL d'E19.c1 au-dessus du moteur de C0 sont
  ceux de la liste, test par test, sans écart ; A9 échoue dans sa limite en nommant `slot 2 program @504: last 0x1C @539`, A3 resserré
  échoue sur (1) avec 25 au lieu de 26, puis (2) 24 et (3) 65 une fois (1) retiré)* (non commitées jusqu'à C6) : l'arc A9, le resserrement d'A3 et les tests
  T-D1 à T-D16, T-D19 et T-D20 **à 1 tick par image seulement**, écrits avant le code de la DLL et lancés sur
  la DLL d'E19.c1 au-dessus du moteur de C0 (le pointeur du sous-module est déplacé localement pour la
  course, sans commit). Résultats attendus, test par test :
  - **rouges** : T-D1 rend 2 à s+24 ; T-D3, T-D4, T-D5, T-D7, T-D10, T-D15, T-D16 et T-D19 ne finissent
    jamais ; T-D20 finit sa première attente à s+16, sa seconde jamais ; T-D6 rend 2 à s+93 ; T-D8 à s+102
    puis s+144 ; T-D11, T-D12 et T-D14 lisent un tick logique de 0 (et T-D14 un compteur de 0) ; T-D13 rend
    2 à s+24, avec un tick de 0 ;
  - **verts** : T-D2 (s+99) et T-D9 (s+25) ;
  - A9 et A3 resserré : table ci-dessous.
  Les colonnes 60 Hz et 2 t/i, TB1, TB2, TG2 à TG4 et T-D17 demandent le code de C2 : elles s'écrivent en
  C2. T-D18 s'écrit en C4.
- **C2 — Pointeur du moteur, pilotage et pont** ✅ *(fait le 2026-10-01 : pointeur sur `b40888f1`, `StepAnimationClock`, tick dû,
  compensation et effacement entre passes dans `AlundraFrameSyncPasses`, pont (taux 50, `AnimationLooped`, relance sur sélection échouée) ;
  tests TB1, TB2, TG3, TG4, T-D17 et les colonnes 60 Hz et 2 t/i dans `AlundraAnimationClockDriveTests`, `AlundraWorldProxyAnimationEndBridgeTests`
  et `AlundraRepeatAnimationOpcodeTests`. A9 et A3 resserré passent ; seuls rouges, comme annoncé : T-D2 en 2 t/i rend 2 à s+33 et T-D3 en
  2 t/i à s+5. Ces deux colonnes et TG2, tests de la garde, entrent avec C3 pour que chaque commit reste vert)* : le pointeur du sous-module sur la tête de la branche
  moteur ; `StepAnimationClock`, le tick dû, la compensation, l'effacement entre passes ; le pont (taux,
  `AnimationLooped`, relance sur sélection échouée) ; TB1, TB2, TG2 à TG4, T-D17 et les colonnes 60 Hz et
  2 t/i. A9 et A3 passent, ainsi que toutes les valeurs écrites, **sauf deux, annoncées** : sans la garde
  de C3, T-D2 en 2 t/i rend 2 à s+33 (double comptage) et T-D3 en 2 t/i à s+5 (drapeau Hold périmé) ;
  elles passent après C3.
- **C3 — Garde de `0x1C`** ✅ *(fait le 2026-10-01 : TG2 déplacé ici de C2 puisqu'il teste la garde ; T-D2 et T-D3 en 2 t/i passent (49 et 10) ;
  le test annoncé rend bien 0, 0, 0, drapeau 0 et marque effacée après le changement, quatrième appel 2, et il est renommé
  `..._CountsTheEndOnce_...` car l'ancien nom disait le contraire ; `Alundra.Tests` 2063 réussis avec les arcs en cours)* : la garde et ses tests. **Seul test existant qui bouge, annoncé** :
  `RepeatAnimation_0x1C_AHoldFlagHeldWithoutASyncBetweenCalls_CountsOnEveryCall_AndKeepsTheFlag`
  (`01 1C 02 FF`, drapeau tenu) passe de 0, 0, 2 à 0, 0, 0 ; après un changement, drapeau 0 et marque
  effacée ; avec le drapeau reposé à 1, le quatrième appel rend 2.
- **C4 — Repli de la passe de tri** ✅ *(fait le 2026-10-01 ; T-D18 rouge sans le repli, 6 au lieu de 22 pour l'élévation, puis vert)* et son test (T-D18).
- **C5 — Hygiène reportée par E19.c1** ✅ *(fait le 2026-10-01 : les textes du code sont à jour ; la vérification d'A7 image par image est écrite
  (le héros ne bouge pas, sur plus de 1000 images) mais, comme tout le fichier des arcs, elle entre avec C6 ; les comptes de la ligne E19.i
  et des lignes P1/P2 de §1.2e sont mis à jour avec C7, qui met à jour ces lignes)* :
  - le libellé de `0x73` devient « Set loop counter _30 » ; la coquille « the / the » d'`EntitySearchService` ;
  - A7 vérifie à chaque image que le héros ne bouge pas, de `@421` à la fin ;
  - textes en retard : « Loop not bridged » d'`AlundraEntitySpawnFactory`, la doc de `SyncAnimation` et du
    pont, `AlundraGameplayFreeze.cs`, le commentaire du proxy sur la latence, le texte de rôle
    d'`IntroTraceHarnessTests.cs:878`, les comptes de la ligne E19.i et des lignes P1/P2 de §1.2e.
- **C6 — Arcs verts** ✅ *(fait le 2026-10-01 : le fichier des arcs entre avec A9, A3 resserré et la vérification d'A7 ; A9 mesuré F530 = 4, boîte fermée
  avant l'image 8, `0x11 @547` à l'image 95 ; ligne A9 ajoutée au tableau du §1.3)* : A9 et A3 resserré commités.
- **C7 — Docs** ✅ *(fait le 2026-10-01 : statuts, mesures au §2, O-E19-10 et O-E19-13 réglés, lignes P1 à P4 d'E19.c1 mises à jour, ligne E19.c2 de la
  table des tranches, ligne E19 du plan maître ; la ligne E19.i disait déjà 208 sites dans 53 cartes ; le pointeur du moteur n'a pas bougé depuis C2)* :
  statuts, mesures au §2, points ouverts, ligne du plan maître.
- **C8 — Recette en jeu (auteur)** ⏳ : voir plus bas.

**Valeurs écrites d'avance.** Sauf mention, 1 tick logique par image. La colonne « 60 Hz » suppose le motif
de ticks [1,1,1,1,1,0] avec l'image s au début du motif (la première image à un tick après l'image sans
tick) ; « 2 t/i » = 2 ticks par image, le `0x1A` au premier tick de l'image s. Dans tous les tests où un
`1A` lance l'attente, l'entité joue **une autre animation** avant s (par exemple une Loop de 7 ticks) : sans
cela, `0x1A` ne change rien et l'attente dépend de la phase de l'animation déjà jouée.

- **Pont et pilotage** :
  - **TB1** : `SubscribeAnimationEndBridge` met le sprite à 50 et le garde sur le proxy ; une levée
    d'`AnimationLooped` fait `AnimCompleteCounter` 0 → 1. Sans sprite : aucune exception, champ nul.
  - **TB2** : chaque levée ajoute 1 (une avance qui fait deux tours lève deux fois : + 2) ; un enregistrement
    tout en Loop (table des fins nulle) compte aussi ; un émetteur nul ou étranger : aucune exception.
  - **TG2** : `01 1C 01 FF`, drapeau 1 et un tick de changement réservé : le deuxième appel rend 0 et le
    compte reste 0 ; avec le compteur à 1 au lieu du drapeau : rend 2.
  - **TG3** : `SyncAnimation` avec un sprite à horloge : changement sans tick réservé → tick dû posé, tick 0 ;
    1 réservé → pas de tick dû, tick 0 ; 3 réservés → tick 2. Le compte des réservés revient à 0 après chaque
    appel, aussi sans changement et sur une entité à détruire ; la marque est effacée par tout changement.
    Un sprite sans animation dont la sélection échoue au changement : aucune exception (les trois tests
    `SyncAnimation_*` existants qui changent d'animation sur un tel sprite ne bougent pas).
  - **TG4** : `StepAnimationClock` : changement en attente → 1 réservé, tick dû effacé, tick inchangé ; tick
    dû sans changement → effacé, tick inchangé ; ni l'un ni l'autre → tick + 1 ; une avance qui lève une fin
    Chain → 1 réservé et `PendingChainRestartFlag` 1 ; entité à détruire ou sans sprite à horloge → rien ;
    un changement de direction seul → 1 réservé, tick dû effacé, tick inchangé, aucune fin levée.
- **Fins vues par `0x1C`** (programme C sauf mention ; s = image de l'instruction `1A`) :

  | Test | Programme et données | 1 t/i | 60 Hz | 2 t/i |
  |---|---|---|---|---|
  | T-D1 | `1A 06 ; 1C 01 ; 1A 00`, Hold 4 × 6 puis Loop 80 (Ronan) | s+25 | s+30 | s+12 |
  | T-D2 | `1A 53 ; 1C 03`, Hold 32 | s+99 | s+118 | s+49 |
  | T-D3 | depuis une Hold de 10 finie, `1A L ; 1C 02`, Loop 10 | s+21 | s+25 | s+10 |
  | T-D4 | depuis l'animation 10 (Loop 3), `1A 0B ; 1C 01 ; 1A 0A`, Loop 90 (Wendell) | s+91 | s+109 | s+45 |
  | T-D5 | `1A L ; 1C n`, Loop 10, n = 1, 2, 3 | s+11, s+21, s+31 | s+13, s+25, s+37 | s+5, s+10, s+15 |
  | T-D6 | `1A 01 ; 1C 03`, Chain sur soi-même de 30 (marche du héros) | s+91 | s+109 | s+45 |
  | T-D7 | `1A 0C ; 37 17 ; 1C 01`, Chain 24 vers 0, puis Loop 85 (Jess) | premier `1C` à s+24, rend 2 à s+25 | — | — |
  | T-D19 | `1A 04 ; 1C 03`, Chain 16 vers 0, puis Loop 10 (Aida) | s+37 | — | — |
  | T-D20 | `1A 04 ; 1C 01 ; 37 0A ; 00 ; 1C 01`, Chain 15 vers 1, puis Loop 10 (interrupteur de la 113) | second `1C` rend 2 à s+36 | — | — |

  - À 1 t/i pour T-D1 : le tick logique en fin d'image s+k vaut k pour k = 0 à 24 ; le drapeau Hold vaut 1 en
    fin d'image s+24 et 0 en fin d'image s+25.
  - **Programmes d'événements de carte** (B), 1 t/i et 60 Hz seulement (sous rattrapage, résidus bornés, non
    épinglés) : T-D8 héros `1A 53 ; 1C 03 ; 1A 55 ; 1C 01 ; 1A 00` → s+99 puis s+140 (60 Hz : s+118 puis
    s+168) ; T-D9 Jess `1A 0C ; 1C 01` → s+25 (60 Hz : s+30) ; T-D10 `1A L ; 1C n`, Loop 10 → s+11, s+21,
    s+31 (60 Hz : s+13, s+25, s+37).
- **Tick dû, gel et cas limites** :
  - **T-D11** : cible 1 après l'image 3, image 4 sans tick, le script de l'image 5 met la cible 2 : en fin
    d'image, f4 (animation 1, tick 0), f5 (animation 2, tick 0), f6 1, f7 2, f8 3.
  - **T-D12** : cible 1 après l'image 3, puis images 4 à 9 avec les ticks [0,1,1,1,1,1] : (4, 0), (5, 0),
    (6, 1), (7, 2), (8, 3), (9, 4).
  - **T-D13** : T-D1 avec `MenuOpen` posé sur les images s+11 à s+20 : le tick logique reste 10 sur ces
    images ; `0x1C` rend 2 à s+35.
  - **T-D14** : héros sans contrôleur, Loop 10 : tick k mod 10 en fin d'image k ; `AnimCompleteCounter` 3
    après l'image 30.
  - **T-D15** : programme B `0x1D [1]` sur l'animation de repos du héros (Loop 54) posée à l'image 0 : un
    premier appel aux images 0, 1, 10 ou 53 rend 2 dans les événements de carte de l'image 54 ; aux images
    54, 55 ou 100, à l'image 108.
  - **T-D16** : Flamme : sprite qui joue l'animation 0 (Loop 12) depuis une phase quelconque ; `1A 09 ; 1C 01`
    sur une entité sans animation 9 : l'animation 0 repart (tick 0 en fin d'image s), `0x1C` rend 2 à s+13,
    le compteur revient à 0.
  - **T-D17** : 1000 appels de `StepAnimationClock` sur une Loop 10, pont abonné : 0 octet alloué.
  - **T-D18** : `CurrentAnimationId` = ~6, `TargetAnimationId` = 6, IDSV {6 vers le bas : 3} : biais 3
    (aujourd'hui 0).
- **Arcs** (vrais préfabs ; ordre des vérifications : signal de fin, opcodes sautés ou dépassés, puis le
  reste) :
  - **A3 resserré**, trois vérifications ajoutées à la fin d'A3, **dans cet ordre** : (1) le premier
    `0x1A @856` s'exécute à l'image 26 ; (2) chaque `0x1A @856` s'exécute exactement 25 images après le
    `0x1A @852` qui le précède ; (3) deux `0x1A @852` successifs sont à 66 images l'un de l'autre jusqu'à
    T60. Toutes les autres valeurs d'A3 restent celles d'E19.c1. Sur la DLL d'E19.c1, les trois échouent
    (25, 24, 65) : la première qui échoue est (1).
  - **A9** (nouveau) : `ArcSpec("A9", "Inoa", "Inoa (inner)-172", {}, héros en (36, 18, 2), limite 400,
    RealController, Prefabs)`.
    - Déroulé : après 2 images, l'arc pose `ActiveCollisionEntity` = l'enregistrement 4 (le déclencheur
      invisible, par le point d'entrée de production `IAlundraScriptHost`), court jusqu'à `0x10 @530`, ferme
      la boîte au bouton dès qu'elle s'ouvre (autant d'appuis que de pages), puis court jusqu'au signal de
      fin `0x11 @547` (créneau C, programme `@504`, Wendell, enregistrement 6).
    - `0x05 @1840` (créneau F, enregistrement 4) s'exécute une seule fois. L'image de `0x10 @530`, F530, est
      entre 1 et 10 (dérivée : 4).
    - `0x10 @530`, `0x0D @534`, `0x1A @537` et le premier `0x1C @539` s'exécutent à l'image F530 ;
      `TargetAnimationId` vaut 11 après `@537` ; `PlayerControlFlags` vaut 0x14 après `@534`.
    - `0x1A @541` s'exécute pour la première fois à F530 + 91 exactement, avec `AnimCompleteCounter` 0 et
      `TargetAnimationId` 10 après lui.
    - La boîte est fermée avant F530 + 91 ; `0x39 @543`, `0x06 @544` et `0x11 @547` s'exécutent à F530 + 91.
    - Fin : `PlayerControlFlags` 0, T0 effacé, une seule boîte ouverte, aucun opcode sauté.
  - **A0 à A4, A4p, A7 et l'autotest des préfabs** gardent leurs valeurs.
- **Rouge attendu, étape par étape** :

  | Étape | A9 | A3 resserré |
  |---|---|---|
  | Après C0 (moteur seul, DLL d'E19.c1) | échoue dans sa limite en nommant `slot 2 program @504: last 0x1C @539` | échoue sur (1) : premier `0x1A @856` à l'image 25 (attendu 26) |
  | Après C2 | passe | passe |
  | Après C3 et C4 | passe | passe |

**Lancement des tests.** `CasaEngine.Tests` se construit à part (il n'est pas dans la solution) et se lance
avec `--no-build`. `Alundra.Tests` au premier plan, `--blame-hang-timeout 300s` dès qu'un arc en préfabs est
dans la course ; un arc qui dépasse 120 s est un arrêt. À la fin : une course en Release, puis la build Debug
et une course Debug ; la DLL déployée dans `alundra-project/` est la Debug.

**Recette en jeu C8 (auteur).**
1. **Wendell, sur la 172** : par raccourci (F6, `initialMapId` 172 dans `debug-json.sav`, F9), aller parler à
   Wendell (le déclencheur est en (36, 14)). La boîte s'ouvre, Wendell joue son animation de parole environ
   1,8 s, et la main revient quand la boîte se ferme.
2. **La première visite de la maison de Wendell (165)** et **la 179** : la cinématique va au bout et rend la
   main.
3. Les grilles et portes des sanctuaires (par exemple la grande grille de fer) ne disparaissent plus après
   leur premier cycle.

**Acceptation d'E19.c2.**
1. Le plan moteur est exécuté : R1 à R3 rouges puis verts, R4 et L1 à L16 verts, aucun test existant du moteur
   modifié.
2. A9 et A3 resserré échouent comme le dit la table, puis passent ; les autres arcs gardent leurs valeurs.
3. Les tests du pilotage, du pont, de la garde et du tri passent ; le seul test existant qui bouge est celui
   annoncé en C3.
4. `CasaEngine.Tests`, `Alundra.Tests` et les tests du convertisseur passent à 0 échec ; les traces du héros
   et de l'intro ne changent pas.
5. Un verifier frais rend CONFIRMED sur 1 à 4, moteur et parent ensemble.
6. La recette C8 de l'auteur.

**Risques.**
- Environ 200 sites du corpus changent d'un coup : des programmes figés depuis E19.c1 posent à nouveau leurs
  drapeaux, apparitions et destructions. Les tests couvrent le mécanisme ; le corpus se vérifie en jouant.
- Les fins Chain passent une image plus tôt pour 3403 des 4413 Once, et `DeactivateOnAnimationEnd` un tick
  plus tard pour les 1010 qui finissaient tôt : c'est le tick de l'original. Le cycle de marche du héros
  passe de 31 à 30 ticks, et `MovePlayer` voit la fin de l'animation d'arrivée sur une carte une image plus
  tard. Les Chain de Rancune dans A4p bougent d'au plus une image, sans valeur épinglée : la course le
  confirme.
- Sous rattrapage, les programmes d'événements de carte et ceux qui agissent sur une autre entité gardent
  des résidus bornés ; une fin qui tombe dans la compensation d'une image de 3 ou 4 ticks (D ≤ 3) se voit à
  l'image suivante.
- Dans les cartes 35 et 39, la Flamme se désactive au tick où son attente finit, car `0x24` n'est pas porté
  (E19.d) : seule la 38 la fait charger.
- Le héros reçoit son tick logique même sans contrôleur : sans effet en production, mais un montage de test
  qui aurait un sprite de héros sans contrôleur l'animerait.

**Revues** : plan-verifier sur cette section et sur le plan moteur, avant approbation ; verifier frais après
exécution.

**Relectures du 2026-10-01.**
- **Plan-verifier** (`5399bac` et moteur `9c191381`) : **REVISE**, deux P2 : les rouges de C1 ne couvraient
  pas tous les tests écrits (T-D2 et T-D3 manquaient), et la case rouge d'A3 resserré ne fixait pas l'ordre
  de ses trois nouvelles vérifications.
- **Audit indépendant des valeurs, en parallèle** : toutes les valeurs du moteur (R1 à R4, L1 à L16), de la
  table T-D, d'A3 resserré et d'A9 recalculées et confirmées par un modèle propre. Trois P2 : la liste des
  rouges de C1 ; le moteur ne disait pas ce que fait l'horloge sans animation courante, état où la DLL pose
  le taux à l'apparition ; le changement de direction manquait à la définition d'un changement en attente.
  Des P3 : préconditions des tests (autre animation avant s, `0x1A` au premier tick en 2 t/i), rouges de
  T-D2 et T-D3 en 2 t/i entre C2 et C3, relance sur sélection échouée sans animation courante, règle de
  `Seek`. Des P4 : formulation des comptes du moteur, vitesse nulle sous rattrapage, valeur rendue de L4,
  borne de la grille.
- **Tous intégrés** : liste complète des résultats attendus en C1 et en C2, ordre des vérifications d'A3
  resserré et sa case rouge, règles « sans animation » et `Seek` du plan moteur avec le test L17, direction
  dans le changement en attente (cas de TG4), précondition des tests, cas sans animation de TG3, formulations.
- **Relecture neuve de la révision** (`9d169eb` et moteur `92869187`) : **READY**.

**Vérification d'E19.c2 (2026-10-01).**
- **Commits** : moteur `1561fd07`, `cc6f498e`, `34ddf5e2`, `b40888f1` (plus `f205683a`, docs) ; parent `54029c4`
  (C2, pointeur sur `b40888f1`), `371ac46` (C3), `ff2328d` (C4), `1509625` (C5), `4cab666` (C6), `39a1843` (C7).
- **Verifier frais, moteur et parent ensemble : CONFIRMED.** Dans un arbre jetable : `CasaEngine.Tests`
  2405/2405, `Alundra.Tests` 2064/2064 en Release et en Debug, convertisseur 400/400.
  - Rouge reproduit : la DLL d'E19.c1 sur le moteur de C0 fait échouer A9 sur `0x1C @539` et A3 resserré sur
    25 au lieu de 26.
  - Mutations attrapées : l'ancienne logique de bouclage (R1 à R4), le pont de boucle retiré (A9), la direction
    retirée du changement en attente (TG4), la garde retirée (T-D2 et T-D3 en 2 t/i, TG2), un chemin en temps
    réel qui lèverait encore les fins en mode logique (L6, cas 60 Hz, T-D7, T-D13, A3).
  - Traces inchangées ; DLL Debug déployée ; pointeur sur la tête de la branche moteur.
- **Trois contradicteurs en lecture seule** (moteur, pilotage contre le binaire, corpus). Le corpus est fermé :
  aucune attente `0x1C`/`0x1D` ne peut plus durer sans fin là où l'original se termine ; rien de nouveau ne bloque
  sur les cartes de la chaîne ni sur les cartes d'Inoa. Dispositions (signalé, jamais corrigé sur un candidat
  CONFIRMED) :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | À 1 tick par image, une autre entité qui change l'animation d'une entité après la synchronisation de celle-ci, au tick exact où son animation finit, fait compter une fin fantôme par le `0x1C` de cette entité (la garde ne voit que les changements déjà comptés par le pas). Rare : environ 1/D des écritures croisées, et seulement au tick de fin. | P3, introduit | Reporté : E19.d vérifie les sites croisés de la 163 (`0x43` vers Jess) ; piste : cacher le drapeau quand un changement est déjà en attente à l'appel. |
  | Un programme d'événement de carte qui attend sans `1A` (animation déjà jouée) voit la fin un tick tôt, ou un cycle entier plus tard si son premier appel tombe sur le tick de la fin (396 `@77` et suivants sur le repos du héros, 226 `@322`/`@437`, 115 `@231`, boss). Jamais de blocage. T-D15 épingle les valeurs de la DLL, et sa doc se contredit. | P4, introduit | Accepté (classe D-E19-13 : événements de carte après les entités) ; doc de T-D15 à corriger en hygiène. |
  | Sous rattrapage, une fin Chain puis un changement par le script dans la même image font avancer la compensation d'un tick de trop : 1 à 3 ticks d'écart pour les programmes C de l'entité elle-même (jusqu'à 26 ticks à 4 ticks par image avec D ≤ 3). Le plan disait les programmes C exacts sous rattrapage. | P4, introduit | Accepté et consigné ici ; piste : appliquer le changement Chain au pas. |
  | Autres résidus sous rattrapage : un changement demandé puis annulé dans la même image perd son tick ; une Chain de D = 1 finit dans la compensation dès 2 ticks par image. | P4, introduit | Accepté. |
  | La relance sur sélection échouée vaut pour toute entité et toute direction, et aussi pour un sprite sans horloge ; seule la Flamme vers le bas est fidèle. Le corpus n'a que les Flammes (les deux autres écritures vers une animation absente sont écrasées dans le même appel). | P4 | Accepté. |
  | Latent : le binaire exclut aussi les entités bloquées (`BlockedByEntity`, `+0x20`) de `UpdateAnimation` ; la DLL n'exclut que les entités à détruire. Rien ne pose ce champ aujourd'hui. | P4 | Reporté (à reprendre si le blocage entre entités est porté). |
  | P4 du moteur (horloge activée en cours d'animation, `LastUpdateLoopTurns` périmé, numéros de ligne de la doc, trous de tests, entrées limites, `Detach`). | P4 | Reportés : plan moteur, « Vérification de clôture ». |

### 1.2g E19.d — Fin de la chaîne : 392, 391 et 163 jusqu'au premier livre ⏳ (proposée le 2026-10-01)

**But.**
- La 392 mène à la 391 par son portail ; la 391 mène à la 416 ; la 163 joue le réveil à Inoa, Jess marche
  jusqu'au mur et disparaît, Alundra reprend la main près du premier livre.
- `0x24` attend comme le binaire, avec un recensement de ses 395 sites (D-E19-21) ; `0x40`/`0x41` sont portés
  complètement (D-E19-22).
- Les arcs à préfabs ont le vrai préfab d'Alundra, et le générateur aléatoire repart de sa graine à chaque
  arc.

**Périmètre.** DLL (`AlundraEventProgramRunner.cs`, `AlundraEntityScriptProxy.cs` pour le défaut
d'atterrissage, `EventOpcodeSizeTable.cs` pour les libellés), tests (`Alundra.Tests`, support d'arcs), docs.
Le moteur ne change pas.

**Décisions** (2026-10-01, avec l'auteur ; ADR-0020) : D-E19-21 à D-E19-23, au §0.1.

**Faits établis** (découverte en lecture seule du 2026-10-01 : quatre volets, chacun contre-vérifié ;
scratchpad `e19d/`) :

- **La 392** **[données]** :
  - on y arrive par le `0x53 @986` de la 476 (tuile (29, 13), z 4, effet 4) ; la carte ne lit ni n'écrit aucun
    drapeau ;
  - B1 (`@20`) : `0x10 @49`, `0x64 @50` place le héros en (720, 216, 64) à l'image 0, animation 13, attente,
    marche de 48 px vers l'est (`0x1F @68`), puis `0x11 @104` ; ensuite une boucle de roulis : tant qu'une
    direction est tenue (`0x2F [0,240,0]`, masque 0xF000) et que le héros est au sol, `0x10`, `0x0C`,
    `0x08 [2]`, attente de 4, `0x11` — le héros est poussé 4 à 5 ticks dans une direction tirée au hasard ;
  - la sortie est le **portail** 0 en (22, 23), sol de portail, drapeau 1 : il faut la flèche bas tenue,
    regarder vers le bas et `PlayerControlFlags` = 0 ; fenêtre `PosX >> 16` dans [538, 541], `PosY >> 16`
    dans [375, 376]. Arrivée : 391, `PosX` 24379392, `PosY` 29884416, `PosZ` 0, animation 0x36, direction 0,
    effet 0 ;
  - seul opcode non porté atteint : `0x8E @20` (balancement de caméra, E19.k), qui ne bloque rien ;
  - **les caisses** (4 à 11) ferment le passage vers le portail. Dans l'original, Alundra les soulève et les
    lance, ou saute par-dessus (hypothèse appuyée par les drapeaux des caisses). **Dans la DLL, elles ne
    bloquent pas** : leur corps de collision est un fantôme (`isSensor: true`,
    `BepuPhysicsEngine.cs:188`) que les déplacements par contrôleur ignorent (`AllowSweepTest`, `:460`) —
    c'est la règle déjà en place, les entités ne se bloquent pas entre elles. Alundra traverse donc les
    caisses : écart connu, à reprendre avec le port et le lancer (E14).
- **La 391** **[données]** : B1 (`@228`), B2 (`@552`) et le programme du bloc caméra (C[100], enregistrement
  0, apparu par `0x2D @265`). Opcodes non portés atteints : `0xA2` × 5, `0x94` × 5 (effets, E19.g), `0x8E`
  × 3 ou 4 (E19.k), `0x4C @706` (E12.c) ; aucun ne bloque.
- **Défaut d'atterrissage de la DLL** **[DLL][binaire]** (bloque la 391) : dans `EvaluateEntitySupport`,
  `wasAlreadyLanded` exige `ForceZ == 0` (`AlundraEntityScriptProxy.cs:716`). Une entité sans gravité qui se
  pose garde sa `ForceZ` négative (comme le binaire, `ComputeZPosition` `0x800375E0`, qui n'écrit jamais X
  ni Y). La DLL pousse alors sa position vers la racine à chaque tick, en la tronquant aux pixels entiers :
  une marche de moins d'un pixel par tick n'avance jamais. Le bloc caméra de la 391 (`0x1B [0,255]`, sans
  gravité) se pose à 144 px, puis sa marche vers le sud à 0,5 px par tick (`0x5B @470`) ne bouge pas ;
  `0x07 @480` (TileY 44) ne passe jamais et le `0x53 @540` vers la 416 n'est jamais atteint.
- **La 163** **[données]** — B[1] `@60` (événement de carte 0, héros = propriétaire) est toute la
  cinématique :
  - `@98`-`@110` : animations 83 (Hold 32) et 85 (Hold 40) du héros, avec `0x1C [3]` et `0x1C [1]` ;
  - `@115 0x43 [0]` : l'entité logique devient Jess ; `@135 0x1A 12 ; 0x1C [1]` (Chain 24 vers 0) ;
    marches `0x0B @143` (96 px) et `@157` (16 px) ;
  - cinq boîtes `0x0D` suivies de `0x39` (`@117`, `@123`, `@129`, `@163`, `@169` ; 2, 3, 3, 3 et 2 pages) :
    il faut le bouton ;
  - `@181 0x1A 1 ; @183 0x24` : Jess marche vers +Y jusqu'au mur ; `@184 0x42`, `@185 0x2E [0]` (Jess
    détruite), marche du héros `0x0B @190`, `G0 @198`, `0x11 @201`, `G200` effacé `@202`, `G201 @205`,
    `G1662 @208` ;
  - le livre (enregistrement 7) est là dès l'image 0 ;
  - opcodes non portés atteints : `0x24 @183` (le seul qui bloque), `0x41 @56`, `0x41 @728` et `0x40 @731`
    (objets de la boutique, sans effet sur la cinématique) ;
  - **Jess au mur** : préfab sans ClassA ni ClassB, masque 0x40 ; boîte 20 × 14 × 32 centrée ; la colonne
    38 a la praticabilité 0x41 aux lignes 19 à 21. Contact exact à `PosY` 297,0 (19464192), avec une avance
    de 0,5 px au tick du contact (drapeau à 0, E19.a3), puis `ForceAdjusted` = 1 au tick suivant ;
  - le P3 d'E19.c2 (fin fantôme par une écriture croisée) ne peut pas arriver ici : les trois attentes sont
    dans B[1], qui écrit l'animation attendue juste avant.
- **Binaire** **[binaire]** :
  - `0x24` (`0x8003DB70`, taille 1) : rend 1 si `ForceAdjusted` (`+0x13C`) de l'entité logique ≠ 0, sinon 0 ;
    sans opérande, sans mémoire, sans autre sortie ;
  - `0x40` (`0x8003E7B8`, taille 3) : `ProgramIndexes[v1]` = v2 de l'entité logique et pose
    `g_clearProgramState` ; `0x41` (`0x8003E7E4`, taille 3) : `SpriteProgramIndexes[v1]` = v2 de l'entité
    logique ; aucun ne borne v1 ;
  - `RunScript` remet le drapeau à zéro au début de chaque appel et le teste après chaque instruction (avec
    l'entité logique lue avant celle-ci) : si l'entité logique est le propriétaire, l'état qui tourne est
    effacé à la fin de l'appel (même sur un Break ou une suspension, quel que soit v1) ; sinon,
    `logic+0x234` et `+0x238` sont effacés tout de suite. Dans les deux cas, le drapeau est remis à zéro
    (`0x80042310`) — la décompilation ne le fait que dans le second cas ;
  - corpus : 426 sites de `0x40` dans 121 cartes, toujours (v1, 0) avec v1 de 2 à 5, aucun dans un programme
    B ; 85 sites de `0x41`.
- **`0x24` dans le corpus** **[données]** : un balayage linéaire de toutes les entrées de programme trouve
  429 sites dans 82 cartes ; 395 d'entre eux, dans 76 cartes, sont dans des programmes atteignables (référencés
  par un événement de carte ou par l'index d'un créneau d'un enregistrement, bit 0x80 posé, ou atteints depuis
  ceux-ci). Les 34 autres sont dans des entrées dormantes. Exemple vérifié : la 172 a 6 sites (`@372`,
  `@383`, `@839`, `@1004`, `@1342`, `@1666`) dans C[4], C[12], C[13], C[19] et C[21], qu'aucun enregistrement
  ne référence (ils utilisent C 6, 7, 8, 9, 22 et 23) et qu'aucun `0x40`/`0x41` de la carte ne peut activer.
  160 des 395 sites tournent dans des programmes qui tiennent la main du joueur (46 cartes). Après la 163,
  sur le chemin de l'histoire : 162 (`@205`, `@210`, `@215`), 164 (`@341`, `@346`), 165 (Nestus, `@932`,
  `@943`, qui commandent par T103 la cinématique de la première visite, sous sa propre main tenue), 176,
  178 (× 5), 179 (× 5), 44 et 10. Tous les acteurs de `0x24` en programme C ont un contrôleur.
- **Support d'arcs** **[test]** :
  - le héros des arcs `RealController` est construit à la main : une boîte et un contrôleur, sans
    `AnimatedSpriteComponent`, donc sans horloge d'animation ; `0x1C @100` de la 163 ne finit jamais. Son
    corps est même `Static` (un mur fantôme à sa tuile de départ), alors que le préfab est `Kinetic`
    (fantôme) ;
  - en production, le héros est le préfab `Entities/Alundra/Alundra.entity` (`192c2eeb`), créé par
    `World.SpawnEntity<Entity>` ; `AdoptPlayerPawn` abonne son sprite (376 animations) à l'horloge logique ;
  - `ArcRun.ResetAll` ne remet pas `AlundraRandom` à zéro (P4 reporté d'E19.c1) ; l'arc de la 392 tient une
    direction, donc `0x0C` tire dans le flux ;
  - le pad de l'arc ne tient un bouton qu'une image (`Press`) et l'arc ne place le héros qu'à sa
    construction.

**Tâches.** Chaque tâche porte son icône de statut et se commite avec la mise à jour de ce plan. Les arcs A6
et A8 restent hors du dépôt jusqu'à D7 ; d'ici là, les lancements de toute la suite filtrent leurs classes.

- **D0 — Preuves rouges** ✅ (faites le 2026-10-01 : A6 échoue sur `slot 1 program @228: last 0x00 @479`, A8 sur `slot 1 program @60: last 0x1C @100`, comme écrit ; arcs non commités jusqu'à D7) : les arcs A6 et A8 écrits avant le code, avec toutes les
  valeurs ci-dessous, lancés sur le code actuel (`74df40e`). Rouges attendus : A6 échoue dans sa limite en
  nommant `slot 1 program @228: last 0x00 @479` ; A8 échoue dans sa limite en nommant
  `slot 1 program @60: last 0x1C @100` (le message donne l'image de la dernière exécution, pas 253).
- **D1 — Support d'arcs** ✅ (faite le 2026-10-01 ; TH1 à TH3 rouges d'abord, puis verts du premier coup ; les 2064 tests existants gardent leurs valeurs avec le vrai héros ; A8 atteint `0x11 @201` à l'image 941 puis échoue sur `0x41 @56`, `0x41 @728`, `0x40 @731`, `0x24 @183`, comme écrit) (tests seulement) :
  - **vrai héros** : en mode `Prefabs`, le héros est le préfab d'Alundra créé par l'appel de production
    (`World.SpawnEntity<Entity>(HeroPrefabId)`, puis `Initialize()`, puis le `AlundraPlayerController`
    existant) ; on garde l'application de l'en-tête, la resynchronisation du masque, l'animation 0 forcée,
    le placement et le pad ;
  - **`AlundraRandom.Reset()`** dans `ArcRun.ResetAll()` (le flux de l'arc part de la graine ; en jeu, sa
    position dépend des tirages d'avant : convention d'arc documentée) ;
  - **directions tenues** : `HoldDirections` / `ReleaseDirections`, à côté de `Press` (inchangé) ;
  - **placement en cours d'arc** : `PlaceHero(x, y, z)` (pixels), qui pose `Pos*`, `Tile*` (`TileZ` =
    `PosZ >> 20`) et déplace la racine ;
  - **boîtes fermées par appuis consécutifs** : un outil qui appuie sur Carré à **chaque** image dès l'image
    qui suit l'ouverture, jusqu'à la fermeture (autant d'appuis que de pages), pour A8. L'outil existant
    `CloseDialogueWithTheButton` alterne un appui et une image sans bouton : il donnerait d'autres images
    (4, 6, 6, 6 et 4 au lieu de 3, 4, 4, 4 et 3) ; il reste inchangé pour A1 et A9 ;
  - autotests, rouges d'abord : **TH1**, **TH2**, **TH3** (valeurs plus bas) ;
  - **non-régression** : A3 (resserré compris), A4p, A7, A9 et l'autotest des préfabs gardent toutes leurs
    valeurs avec le vrai héros ; A0 à A4, la cabine et le marin 12 (sans préfabs) ne changent pas ; les
    traces ne bougent pas. Une valeur qui bouge est un arrêt ; repli : le vrai héros derrière un drapeau
    `RealHero` de l'`ArcSpec`, réservé à A5, A6 et A8.
  - Après D1, A8 atteint `0x11 @201` vers l'image 941 puis échoue sur les opcodes sautés, qui contiennent
    `(1,183,0x24)`, `(0,56,0x41)`, `(2,728,0x41)` et `(2,731,0x40)`.
- **D2 — Défaut d'atterrissage** ✅ (faite le 2026-10-01 ; rouge du test unitaire : PosY n'avance pas du tout (6553600 au lieu de 8650752), vert après le correctif ; les deux jumeaux passent ; A6 (non commité) passe du premier coup avec toutes ses valeurs ; suite complète sans A8 : 2071 réussis) : `wasAlreadyLanded` ne dépend plus de `ForceZ`
  (`PosZ == targetPosZ` suffit) ; l'entité qui reste posée n'est plus repoussée à chaque tick. Tests
  unitaires (valeurs plus bas), puis A6 passe.
- **D3 — Arc A5 de la 392** ✅ (faite le 2026-10-01 ; A5 et A5r verts du premier coup, toutes les valeurs écrites égales : départ dans l'image 215 (A5) et 238 (A5r)) (avec A5r) : écrits et verts d'un coup (aucun opcode d'E19.d n'y joue).
- **D4 — `0x24`** ⚠️ (code et U1 à U4 écrits le 2026-10-01 mais NON commités, en attente de la réponse à O-E19-16 ; U1, U3 et U4 rouges sans le cas puis verts (U2 passe déjà : l'opcode sauté rend sa taille 1) ; A8 atteint ensuite la fin à l'image 1025 et échoue seulement sur `0x41 @56`, `0x41 @728`, `0x40 @731`, comme écrit) : le cas, une ligne sur l'entité logique, sans détour ni minuterie (D-E19-6) ; tests
  unitaires U1 à U4. A8 échoue ensuite sur `0x41 @56`, `0x41 @728` et `0x40 @731`.
- **D5 — Recensement des sites de `0x24`** ⚠️ (fait le 2026-10-01, rapport `docs/census-0x24-waits.md` écrit mais NON commité ; **arrêt de la règle de D5** : 26 des 59 sites atteignables des cartes du chemin de l'histoire sont hors de « mur trouvé », voir O-E19-16 ; population 429 sites dans 82 cartes, 395 atteignables dans 76 cartes, 34 dormants, 0 « DLL seulement », comme attendu ; les six sites de la 172 sont dormants) (D-E19-21) :
  - **population** : les 429 sites du balayage linéaire de toutes les entrées de programme de toutes les cartes.
    Chacun reçoit d'abord une **atteignabilité**, par une règle mécanique :
    - **racines** : l'entrée B de chaque événement de carte ; pour chaque enregistrement de la carte, l'entrée
      désignée par chacun de ses index de créneau A, C, D, E et F quand le bit 0x80 est posé (index & 0x7F) ;
    - **atteignable** : toute instruction atteinte depuis une racine en suivant tous les sauts, branches et
      appels (toutes les branches ouvertes) ; `0x40` (toujours v2 = 0 dans le corpus) et `0x41` (index de
      sprite, natifs) n'ouvrent aucune entrée de script ;
    - **DLL seulement** : atteint seulement par le repli de la DLL sur un index sans bit 0x80 (par exemple la
      carte 60) ;
    - **dormant** : aucune des règles ci-dessus ;
    - une atteignabilité qui ne se décide pas compte comme **atteignable**. Attendu : 395 atteignables dans 76
      cartes, 34 dormants ou DLL seulement ; tout autre compte est noté et expliqué dans le rapport ;
  - **méthode** (sites atteignables et DLL seulement ; entité logique suivie à travers `0x42`/`0x43`) :
    l'acteur (préfab), son animation au moment de l'attente (dernier `0x1A`, `0x59`,
    `0x5B` ou `0x0B` avant le site), la vitesse de cette animation (jeux d'animation du préfab), sa
    direction (dernier `0x09`, `0x5B`, `0x3A`, `0x08` ou `0x0C`) et sa position quand elle se déduit
    statiquement (apparition ou dernier `0x64`/`0x65`) ; puis un lancer de rayon dans la grille de cellules
    depuis l'empreinte de l'acteur, dans sa direction : la première cellule qui bloque son masque (comme
    `AlundraCellsCollisionField`) ou dont la marche de hauteur dépasse celle du contrôleur ;
  - **classes** : « mur trouvé » (finit sur une cellule) ; « vitesse nulle » (attend un contact extérieur :
    E19.h ou E14) ; « aucun mur » (le rayon sort de la carte) ; « indéterminé » (position ou direction non
    déductible) ;
  - **rapport** : `docs/census-0x24-waits.md` (en anglais) — la méthode, le tableau site par site (carte,
    pc, créneau, atteignabilité, acteur, animation, vitesse, direction, classe, distance au mur, main tenue ou
    non) et les totaux par atteignabilité et par classe ;
  - **chemin de l'histoire** (critère et fin) : de la 163 jusqu'au premier rêve, soit toutes les cartes de la
    zone Inoa (162 à 182, la 172 comprise), la 44 (Wendels Nightmare, atteinte depuis la 179) et la 10
    (Overworld 2,1, voisine d'Inoa) ;
  - **arrêt** : un site atteignable ou DLL seulement de ces cartes hors de « mur trouvé », ou un site dont
    l'appartenance au chemin ou l'atteignabilité reste incertaine, arrête l'exécution **avant** le commit de
    D4 et D5 : la question est soumise à l'auteur. Les sites hors du chemin dans une classe à risque sont
    listés pour l'auteur, sans arrêt. Les six sites de la 172 sont attendus dormants ; si le parcours les
    trouve atteignables, ils tombent sous l'arrêt.
- **D6 — `0x40` et `0x41`** ⏳ (D-E19-22) : les deux cas, comme le binaire. `0x41` écrit
  `SpriteProgramIndexes[v1]` de l'entité logique ; `0x40` écrit `ProgramIndexes[v1]` et demande
  l'effacement : propriétaire → l'état qui tourne, à la fin de l'appel (`ClearProgramStateRequested`), quel
  que soit v1, même sur un Break ou une suspension ; autre entité → son `EventProgramState` tout de suite. Le
  drapeau est remis à zéro à chaque test. v1 ≥ 6 : aucun effet (rien n'est écrit, aucun état n'est effacé),
  un avertissement une seule fois par opcode, taille rendue 3 (correction d'un défaut de l'original, qui
  écrirait hors du tableau). Tests unitaires U5 à U13 (sans U9).
  A8 passe ensuite.
- **D7 — Arcs verts** ⏳ : A6 et A8 commités ; temps d'exécution relevés.
- **D8 — Hygiène** ⏳ : miroir `ImplementedOpcodes` (`0x24`, `0x40`, `0x41`) ; libellés de la table des
  tailles ; la doc de `RunScript` sur l'effacement (le choix laissé au port de `0x40` est fait) ; la doc de
  T-D15 qui se contredit (P4 d'E19.c2) ; la ligne E19.j de l'enveloppe ne garde que le réarmement hors zone ;
  au §0.2.4, `0x8E @20` est en tête de B1, hors de la boucle de roulis.
- **D9 — Docs** ⏳ : statuts, mesures au §2, points ouverts, ligne du plan maître.
- **D10 — Recette en jeu (auteur)** ⏳ : voir plus bas.

**Valeurs écrites d'avance.** Échantillons pris pendant la course (`OnInstruction`, après l'effet), vérifiés
après le signal de fin ; ordre des vérifications : signal de fin, opcodes sautés ou dépassés, puis le reste
dans l'ordre écrit ; images absolues à ± 3 près, écarts exacts.

- **Support d'arcs** :
  - **TH1** (carte 478, `ArcSpec("PrefabHeroSelfTest", "Inoa", "Inoa (Vision Event from Lars and Melzas
    cutscene)-478", {1641}, 22, 57, 1, 200, RealController, Prefabs)`). Rouge aujourd'hui : échoue sur
    l'absence d'`AnimatedSpriteComponent` du héros. Vert, dans cet ordre : (1) après une image, le héros est
    dans `RealWorld.Entities` avec `Entity.World` = `RealWorld` ; un `AnimatedSpriteComponent` de 376
    animations ; `Hero.LogicalClockSprite` est ce composant, avec `LogicalTickRate` 50 ; une boîte
    (21, 15, 32) en (0,5 ; 0,5 ; 16) ; un `CollisionComponent` `Kinetic` sans réponse de contact ; un
    contrôleur de masque 0x41 ; (2) en fin d'image 0, l'animation courante est `bankalundra_0_anim0_down`,
    tick logique 0 ; (3) `Hero.AnimCompleteCounter` vaut 0 à `arc.Frame` = 54, 1 à 55, 2 à 109 ; (4) après la
    première image, exactement 6463 erreurs de résolution de sprite (3628 + 2835), aucune autre erreur, aucun
    repli sur une entité nue ; le temps est relevé.
  - **TH2** : trois `AlundraRandom.Next()`, puis `new ArcRun(spec)` : graine 0xB017C93D ; un `Next()`, puis
    `Dispose()` : graine 0xB017C93D. Rouge aujourd'hui sur la première.
  - **TH3** (lu dans `ArcRun.State.LastPadState` après chaque image) : `HoldDirections(Down)` : image 1,
    `Hold` 0x4000 et `JustPressed` 0x4000 ; image 2, 0x4000 et 0 ; `Press(Square)` avec bas tenu : 0x4080 et
    0x0080 ; `ReleaseDirections()` : image suivante 0 et 0 ; bas de nouveau : `JustPressed` 0x4000. A1 et A9
    ne changent pas.
- **Défaut d'atterrissage (D2)** — entité sans gravité, avec contrôleur, posée sur un sol plat avec
  `ForceZ` −65536, `CurrentAnimationId` déjà sur une animation de marche de 0,5 px par tick vers le sud
  (vitesse 64, **accélération 0**, comme l'animation 1 du bloc de la 391 ; direction 0 ; `ForceY` initiale
  0 ; sans le préréglage de l'animation, le retard D-E19-13 donne 31,5 px) :
  - après 64 ticks, `PosY`, lue après la reprise de la racine de l'image suivante, a avancé de exactement
    2097152 (32 px) ; `PosZ` reste au sol ; `ForceZ` reste −65536. Aujourd'hui : 0 (le champ `PosY` vaut le
    départ + 32768 juste après chaque tick, sans jamais cumuler) ;
  - jumeaux : une entité avec gravité se pose comme avant (`ForceZ` 0, plus de poussée une fois posée) ; une
    entité sans gravité qui marche hors d'un rebord descend de 1 px par tick.
- **A6** (391) : `ArcSpec("A6", "The Klark", "Ship Klark (night, break, Event)-391", {1641}, 15, 28, 7,
  limite 1500, RealController, Prefabs)`.
  - Fin : `0x53 @540` (créneau B, programme `@228`) à l'image 1149 ; arrivée sur la 416 en `PosX` 65273856,
    `PosY` 51904512, `PosZ` 1048576, effet 2, animation 0, direction 0.
  - Opcodes sautés : contenus dans {`0xA2` `@228`/`@236`/`@244`/`@252`/`@408`, `0x8E` `@260`/`@335`/`@342`/
    `@721`, `0x94` `@417`/`@425`/`@433`/`@441`/`@503`, `0x4C @706`} ; `0x4C @706` s'exécute exactement 3 fois ;
    aucun dépassement de la garde.
  - Image 0 : après `0x2D @265`, l'enregistrement 0 est en (29097984, 44040192, 7340032) : l'appui évalué à
    l'apparition, sans limite de portée, le pose une image sur le dessus du marin 4 (TileZ 7) ; dès l'image 1,
    il revient sur le terrain à 144 px, et la suite ne change pas (O-E19-15) ; après `@270`, le
    héros a `TargetAnimationId` 1 et `TargetDirection` 0. C[100] tourne pour la première fois à l'image 2 :
    après `@764`, `PosZ >> 16` du bloc vaut 336 ; après `@775`, sa `ForceZ` vaut −65536. Le héros à `@275`
    (image 21) : (24379392, 32067584).
  - Premier `0x00 @291` à l'image 179, le bloc en TileX 18, TileY 42, TileZ 9.
  - Boîtes : `0x5C @295` à l'image 180, `@306` à 273, `@327` à 444 ; chacune fermée par `0x51 @739` exactement
    61 images après (241, 334, 505) ; trois ouvertures en tout, boîte fermée à la fin.
  - À l'instruction : `0x5E @321` (413), enregistrement 6, `ForceZ` 393216 ; `@368` (619), enregistrement 2,
    524288 ; `@377` (640), enregistrement 3, 393216 ; `@497` (854), enregistrement 0, 24576. T0 posé `@347`
    (568). `0x2E @388` et `@390` (702), `Result` 1. `0x64 @449` (703) : l'enregistrement 4 en (29097984,
    46661632, 3145729) ; après `0x63 @457`, `(Flags & 0x100) == 0`. `0x5B @470` (728) : animation 1, direction
    0 du bloc. Premier `0x5B @491` à l'image 793, bloc en `PosY` ≥ 46137344 et TileY 44.
  - Boucle et fin : `0x73 @514` (987) ; `0x65 @517` et `0x74 @525` s'exécutent 40 fois chacun, `0x65` sur 40
    images consécutives à partir de 988 ; l'enregistrement 4 en `PosY` 51904512 à `0x00 @528` (1027) ;
    premier `0x37 @529` à 1028 ; le `0x53 @540` exactement 121 images après. B2 : `0xAF` aux images 62, 184,
    206, 448 et 510 ; fin `@761` dès 633. État final : `PlayerControlFlags` 0x04 ; T0 posé ; T999 et T1000
    effacés ; `GameFlags[51]` = 512.
  - La `PosY` finale du bloc n'est pas épinglée (une dernière troncature à l'atterrissage reste).
  - Ces images suivent l'ordre actuel de `TileZ` : elles reculeront d'une image avec E19.h (D-E19-15).
- **A5** (392) : `ArcSpec("A5", "The Klark", "Ship Klark (night, inner, break)-392", {1641}, 29, 13, 4,
  limite 400, RealController, Prefabs)`.
  - Partie 1 : image 0, `0x8E @20` sauté, `0x10 @49` ; après `0x64 @50`, le héros en (47185920, 14155776,
    4194305) ; après `@58`, animation 13, direction 0. `@64` à l'image 121 (animation 3, direction 24).
    `0x00 @71` à 135, héros en `PosX` 50411520, `PosY` 14155776 ; `@72` à 136, `PosX` 50669568 ; `@78`, `@84`,
    `@90` à 152, 168, 184 ; `@96` à `@99` de 200 à 203 ; `0x11 @104` à 204, héros en (50798592, 14155776),
    tuile (32, 13, 4), `PlayerControlFlags` 0.
  - Partie 2 : à l'image 205, `PlaceHero(540, 360, 64)` et `HoldDirections(Down)`, puis course jusqu'au départ
    par le portail (`HasPendingArrival`) : le départ a lieu dans l'image 215 (`arc.Frame` vaut 216 au retour),
    sans aucun `0x2F @107` avant. Arrivée : 391, `PosX` 24379392, `PosY` 29884416, `PosZ` 0, animation 0x36,
    direction 0, effet 0.
  - Opcodes sautés : exactement `0x8E @20`, une fois ; aucune boîte ; `0x3B @133` rend toujours 0.
  - **Signal de fin hors de la trace** : le départ par un portail n'est pas une instruction. A5 prend
    `HasPendingArrival` et `ArrivalRecordForTests`, l'exception à la règle du §1.3 est écrite ici.
  - **A5r** (roulis) : bas tenu sur place dès l'image 205 ; `0x2F @107` à 220 avec `Result` 1, `0x70 @114`
    avec `Result` 1, `0x10 @118` ; après `0x08 @120`, `TargetDirection` du héros 0x02 ; de 221 à 225,
    `PlayerControlFlags` 0x04, `PosX` décroît et `PosY` croît (à 225 : (50523408, 16302440)) ; `0x11 @124` à
    225 ; relâché aux images 226 et 227 ; `PlaceHero(540, 360, 64)` à 228 ; bas tenu ; départ dans l'image
    238, même arrivée.
- **A8** (163) : `ArcSpec("A8", "Inoa", "Inoa (inner)-163", {}, 40, 9, 2, limite 1300, RealController,
  Prefabs)`, avec le vrai héros ; chaque boîte `0x0D` se ferme par l'outil d'appuis consécutifs de D1 : un
  appui de Carré à chaque image dès l'image qui suit l'ouverture (autant d'appuis que de pages).
  - Fin : `0x11 @201` (créneau B, programme `@60`) à l'image 1025 ; aucun opcode sauté ni dépassé.
  - État final : `0x05 @198`, `0x06 @202`, `0x05 @205` et `0x05 @208` dans l'image de `0x11 @201` ; `G0`,
    `G201` et `G1662` posés, `G200` effacé ; `PlayerControlFlags` 0 ; cinq boîtes ouvertes, aucune ouverte à
    la fin.
  - Livre : l'enregistrement 7 est présent, `SpriteType` 237, statut `Normal`, `SpriteProgramIndexes[C]` = 72,
    `SaveBookEventRunCount` > 0.
  - Héros pendant les attentes : à `@73`, en (62914560, 9961472), `PosZ` 2359297 (seule épingle de Z) ; X et
    Y du héros inchangés de `@73` au premier appel de `@190` ; `@102` (`0x1A`) exactement 99 images après
    `@98` ; `@110` exactement 41 images après `@106` ; `TargetAnimationId` 83 après `@98`, 85 après `@106`.
  - Entité logique : après `@115`, celle du héros est l'enregistrement 0 (Jess) ; après `@184`, le héros.
  - Jess : `@139` exactement 25 images après `@135` ; `TargetAnimationId` 12 après `@135` ;
    `AnimCompleteCounter` 0 à `@139`. Au premier appel de `@143` : (60555264, 8912896), `PosZ` 2097152,
    TileZ 2, un contrôleur de masque 0x40. Marche 1 : à `@147`, (60555264, 15237120) (232,5 px),
    `ForceAdjusted` 0, `@147` exactement 98 images après `@143` ; à `@155`, `PosY` 15302656 (233,5).
    Marche 2 : à `@161`, (60555264, 14221312) (217,0), `ForceAdjusted` 0, exactement 18 images après `@157` ;
    à `@181`, `PosY` 14155776 (216,0).
  - `0x24` : à `@184`, Jess en (60555264, 19464192) (924 ; 297,0 px), `ForceAdjusted` 1 ; `@184` exactement 84
    images après le premier `@183` ; `0x24 @183` s'exécute 85 fois ; après `@185`, Jess (enregistrement 0) est
    à détruire.
  - Marche du héros : au premier appel de `@190`, en (62914560, 9961472), puis `TargetAnimationId` 1 et
    `TargetDirection` 8 ; à `@194`, en (58681344, 9961472) (895,40625 ; 152) ; `@194` exactement 27 images
    après `@190`.
  - Boîtes, fermeture (pages + 1 images) : `@121` − `@117` = 3, `@127` − `@123` = 4, `@133` − `@129` = 4,
    `@167` − `@163` = 4, `@173` − `@169` = 3. Attentes : `@123` − `@121` = 46, `@129` − `@127` = 46, `@135` −
    `@133` = 46, `@169` − `@167` = 31, `@175` − `@173` = 31, `@179` − `@177` = 11, `@98` − `@96` = 241.
  - Images absolues : `@98` 253, `@106` 413, `@115` 516, `@123` 565, `@129` 615, `@135` 665, `@143` 690,
    `@157` 815, `@163` 833, `@169` 868, `@181` 913, `@184` 997, `@190` 998, `@201` 1025.
  - Objets de la boutique (enregistrements 3, 4, 5, 6 et 8), après leur premier tick C : `ProgramIndexes[2]` =
    0 (avant 132), `SpriteProgramIndexes[0]` = 0, `SpriteProgramIndexes[2]` = 4, `EventProgramState.Codes`
    nul ; `(0,56,0x41)`, `(2,724,0x62)`, `(2,728,0x41)`, `(2,731,0x40)` et `(2,734,0xFF)` s'exécutent
    exactement 5 fois chacun sur tout l'arc.
- **Tests unitaires de `0x24` (D4)** :
  - **U1** : propriétaire = entité logique, `01 24 FF`, `ForceAdjusted` 0 : appel 1, trace (0, `0x01`, 1)
    puis (1, `0x24`, 0), `CodeIndex` 1, `Parameters[1]` 0 ; `ForceAdjusted` 1 : appel 2, (1, `0x24`, 1) puis
    (2, fin), `CodeIndex` 2 ;
  - **U2** : `ForceAdjusted` 2 → 1 ;
  - **U3** : lit l'entité logique : propriétaire à 1 et entité logique à 0 → 0 ; l'inverse → 1 ;
  - **U4** : aucun effet de bord ni détour : avec une grille de navigation et `Parameters[1..3]` à 0 au
    départ, dix appels avec 0 puis 1 laissent `TargetDirection`, `TargetAnimationId` et les champs du détour
    inchangés, et `Parameters[1..3]` à 0.
- **Tests unitaires de `0x40`/`0x41` (D6)** :
  - **U5** : programme C, `ProgramIndexes[2]` = 0x84, `40 02 00 1A 05 FF` : après l'appel,
    `ProgramIndexes[2]` = 0, `TargetAnimationId` 5, `EventProgramState.Codes` nul et `Sp` 0 ; trace
    (`0x40`, 3), (`0x1A`, 2), fin ; au tick C suivant, `SpriteEventRunCount` + 1 et `ScriptRunCount` inchangé ;
  - **U6** : programme C du propriétaire `40 03 00 37 05` avec une entité logique X (état non nul en
    `CodeIndex` 7) : après l'appel 1 (suspendu dans l'attente), `X.ProgramIndexes[3]` = 0, l'état de X effacé,
    celui du propriétaire gardé (`Codes` non nul, `CodeIndex` 3) et ses `ProgramIndexes` inchangés ;
  - **U7** : `40 02 00 43 kk 1A 05 FF`, l'entité logique est d'abord le propriétaire, puis kk trouve X : l'état
    de X n'est pas effacé, `X.TargetAnimationId` = 5, l'état du propriétaire est effacé en fin d'appel ;
  - **U8** : `40 02 00 FF` dans un programme A d'une entité dont l'état C est non nul : `ProgramIndexes[2]` = 0,
    état C intact, et le tick C suivant passe par `RunSpriteEvent` ;
  - (U9, un `0x40` dans un programme B, est abandonné : aucun site du corpus) ;
  - **U10** : `40 06 00 FF` et `41 06 00 FF` : aucune exception, tableaux inchangés, **état non effacé**
    (`Codes` non nul : l'opcode hors limites n'a aucun effet), un avertissement par opcode (deux en tout),
    taille rendue 3 ;
  - **U11** : `41 02 04 FF` : `SpriteProgramIndexes[2]` = 4 de l'entité logique, `ProgramIndexes` inchangés,
    état non effacé (fin sur `0xFF @3`), taille 3 ; avec une entité logique X, seule X change ;
  - **U12** : programme C `40 03 00 1A 05 00 1A 06 FF`, `ProgramIndexes[2]` = 0x81 et `[3]` = 0x82 : appel 1,
    trace (0, `0x40`, 3), (3, `0x1A`, 2), (5, Break), puis `ProgramIndexes[3]` = 0 et `Codes` nul ; l'appel 2
    reprend en pc 0 ; `TargetAnimationId` ne vaut jamais 6 ;
  - **U13** : `40 03 00 37 05 1A 06 FF` n'atteint jamais le pc 5.
- **Rouge attendu, étape par étape** :

  | Étape | A6 | A8 |
  |---|---|---|
  | Code actuel (`74df40e`) | échoue dans sa limite en nommant `slot 1 program @228: last 0x00 @479` | échoue dans sa limite en nommant `slot 1 program @60: last 0x1C @100` |
  | Après D1 (support d'arcs) | même échec | atteint `0x11 @201` vers l'image 941, puis échoue sur les opcodes sautés (`0x24 @183`, `0x41 @56`, `0x41 @728`, `0x40 @731`) |
  | Après D2 (atterrissage) | passe | même échec |
  | Après D4 (`0x24`) | passe | atteint la fin à 1025, puis échoue sur les opcodes sautés (`0x41 @56`, `0x41 @728`, `0x40 @731`) |
  | Après D6 (`0x40`/`0x41`) | passe | passe |

**Lancement des tests.** Comme E19.c1 et E19.c2 : au premier plan, `--blame-hang-timeout 300s` dès qu'un arc en
préfabs tourne ; un arc qui dépasse 120 s est un arrêt ; à la fin, une course en Release, puis la build Debug
et une course Debug ; la DLL déployée est la Debug.

**Recette en jeu D10 (auteur).**
1. **De la 476 à la 163, en jouant** (après le raccourci de l'arc A4 sur la 476) : la 392 se charge, Alundra
   reprend la main ; il marche vers l'ouest jusqu'au portail en (22, 23) et le prend en tenant bas. Il
   traverse les caisses : c'est l'écart connu (l'original demande de les soulever ou de sauter). La 391 joue
   sa cinématique (les marins sautent, le bloc caméra descend puis glisse au sud, trois boîtes), puis la 416,
   puis la 163 : réveil, cinq boîtes, Jess marche jusqu'au mur et disparaît, Alundra fait quelques pas à
   gauche et reprend la main, le livre est sur sa table.
2. **La fin de l'intro (389)** se joue comme avant : le bloc 18 se pose et la suite démarre (le défaut
   d'atterrissage corrigé touche ce genre d'entité).

**Acceptation d'E19.d.**
1. TH1 à TH3 rouges puis verts ; les arcs et épingles existants gardent leurs valeurs avec le vrai héros.
2. A6 et A8 échouent comme le dit la table, puis passent ; A5 et A5r passent.
3. Les tests unitaires de D2, D4 et D6 passent.
4. Le recensement de `0x24` est dans `docs/census-0x24-waits.md`, et aucun site du chemin de l'histoire n'est
   hors de « mur trouvé » (sinon arrêt, D5).
5. `Alundra.Tests` et les tests du convertisseur passent à 0 échec ; le moteur, les traces du héros et de
   l'intro ne changent pas.
6. Un verifier frais rend CONFIRMED sur 1 à 5.
7. La recette D10 de l'auteur.

**Risques.**
- `0x24` passe en vrai dans tout le corpus : 395 sites, dont 160 sous main tenue. Le recensement montre les
  sites à risque ; les contacts entre entités, les porteurs et les bornes de force qui lèvent aussi le drapeau
  dans le binaire restent pour E19.h.
- `0x40` en vrai rend inertes, jusqu'à E14, environ 200 entités dans 40 cartes dont le binaire coupe le
  script (par exemple une énigme résolue) : c'est fidèle, mais leur comportement natif manque.
- Le défaut d'atterrissage corrigé touche toute entité sans gravité qui se pose avec une `ForceZ` négative
  (183 sites de `0x1B` négatif dans 67 cartes), et toute entité avec gravité au repos dont le contrôleur ne se
  dit pas au sol à ce tick. Aucun arc existant n'en a ; l'intro a son propre moteur
  vertical. La fin de l'intro en jeu (bloc 18) est vérifiée à la recette. La branche « posé sur une entité »
  tronque toujours à chaque tick (écart préexistant).
- Le vrai héros dans les arcs à préfabs ajoute 2835 erreurs de résolution de sprite par arc ; une valeur
  épinglée qui bougerait est un arrêt (repli : drapeau `RealHero`).
- A5 replace Alundra dans le couloir : le trajet à travers les caisses n'est pas testé.
- Les images d'A8 dépendent du protocole des boîtes (pages, un appui par image) : la machine à écrire (E12.c) ou
  la boîte de nom (E19.f) les décaleront, pas les écarts.
- Sur la 391, la branche T1000 (tout le monde se tourne vers le nord pendant la phrase des récifs) ne se joue
  qu'une fois au plus dans la DLL, parce que Yarn pose les drapeaux à l'ouverture de la boîte : écart
  cosmétique, jusqu'à E12.c.

**Revues** : plan-verifier sur cette section, avant approbation ; verifier frais après exécution.

**Relectures du 2026-10-01.**
- **Plan-verifier** (`f27d4d0`) : **REVISE**, un P2 : la population du recensement de D5 n'était pas définie
  de façon mécanique (395 ou 429 sites, « atteignable » non défini), la liste du chemin de l'histoire était
  écrite en dur sans critère, et la 172 n'était pas tranchée.
- **Audit indépendant des valeurs, en parallèle** : toutes les valeurs recalculées et confirmées, sauf une
  (P2) : après `0x2D @265`, le bloc de la 391 est en `PosZ` 7340032 (posé une image sur le marin 4), pas
  9437185. Un P3 : les images d'A8 supposent un appui à chaque image, que l'outil existant ne fait pas. Des
  P4 : l'accélération du test de D2, U10 (avertissements, état non effacé), U4 (`Parameters`), U9 absent, le
  rayon d'action de D2.
- **Corrigé** : règle d'atteignabilité, population de 429 sites et réconciliation avec les 395, critère et fin
  du chemin de l'histoire (zone Inoa 162 à 182, 44, 10), arrêt sur tout site incertain, verdict sur la 172
  (six sites dormants, vérifié dans les données) ; l'épingle d'A6 et O-E19-15 ; l'outil d'appuis consécutifs
  pour A8 ; les précisions des tests et des risques.
- **Relecture neuve de la révision** (`556b53b`) : **READY**.

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
| A5 | 392 | `G1641` | arrivée, puis replacé dans le couloir du portail après `0x11 @104` (D-E19-23) | départ par le portail vers 391 (`HasPendingArrival`, hors trace ; §1.2g) | 400 | E19.d |
| A5r | 392 | `G1641` | comme A5, avec un roulis avant le portail | idem | 400 | E19.d |
| A6 | 391 | `G1641` | arrivée (15, 28, 7) | `0x53 @540` vers 416 (§1.2g) | 1500 | E19.d |
| A7 | 416 | — | arrivée | `0x53` de `C[1] @640` vers 163 (§1.2e) | 2200 | E19.c1 |
| A4p | 476 | `G1641` | comme A4 | comme A4, en vrais préfabs (§1.2e) | 2500 | E19.c1 |
| A8 | 163 | — | arrivée (40, 9, 2), vrai héros | `0x11 @201`, `G0`, `G1662`, livre présent (§1.2g) | 1300 | E19.d |
| A9 | 172 | — | (36,18,2) ; l'arc pose `ActiveCollisionEntity` = enregistrement 4 | `0x11 @547` de C[6] @504 (Wendell), après `0x10 @530` et la boîte (§1.2f) | 400 | E19.c2 |

Les valeurs exactes de chaque arrivée se décodent des opcodes `0x53` cités et s'écrivent dans le test
avant le code.

---

## 2. Mesures

Réservé aux mesures faites en exécutant les tranches.

### E19.c1 (2026-10-01)

- **Commits** (branche `chantier/e19-opcodes`) : T1 `d1d2bea`, T3 `7d6d8ce`, T4 `53b1503`, T5 `35feaa5`, T6 `767c6a5`, T7
  `6023e4c`, puis cette mise à jour des docs. T2 n'a pas de commit propre (les arcs entrent avec T5). Le pointeur du moteur
  ne change pas.
- **Autotest du mode préfabs** : rouge avec le drapeau sans effet (« record 0 à 21 : no controller »), puis vert en 993 ms.
  Journal après la première image : 3628 erreurs de résolution de sprite (616760 caractères, une par animation, faute de
  chargeur `SpriteData`), 4 avertissements, 3 messages d'information, aucun repli sur une entité nue. Le volume ne grandit pas
  avec les images : A3 sur 2263 images en compte le même nombre, A7 en compte 229.
- **Rouge constaté** (conforme à la table) : après T1, A3 échoue dans sa limite (3 s) en nommant `slot 1 program @164: last
  0x36 @180`, A7 atteint `0x53 @640` à l'image 1145 puis échoue sur les opcodes sautés `0x0B @659`, `@572`, `@586`, `@594`,
  `@608`, A4p passe. Après T3, A7 passe et A3 atteint `0x53 @245` puis échoue sur `0x1C @854` seul. Après T4, les trois passent.
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sans en changer une.
  - A3 : premiers `0x05` de T20 à T60 aux images 301, 856, 1368, 1624, 2008 ; `0x53` à 2262 (2263 images) ; T10 à 63 ;
    écarts 555, 512, 256, 384 ; `TileZ` 8, 21, 33, 39, 48 ; `PosZ` 8421376, 22061056, 34643968, 40935424, 50372608 ;
    C[11] atteint `@725` à 1702 et C[12] `@802` à 1682 ; Ronan passe `0x1C @854` à l'image 25.
  - A7 : T0 à 493 ; premier `0x0B @659` à 494 ; fin à 879 (385 images) ; `0x2D @671` à 1062 (183 images après) ; les quatre marches
    de Jess finissent aux images 1161, 1288, 1321, 1385 ; `0x53 @640` à 1759.
  - A4p : toutes les valeurs écrites (les quatre panoramiques, `PosZ` 3145729 puis 3145728, les parents).
- **Temps d'exécution** (Debug) : autotest 1 s ; A3 environ 1 s (2263 images, 57182 entrées de trace) ; A7 environ 0,4 s (1760
  images) ; A4p environ 0,5 s. Aucun arc n'approche 120 s, et le rouge d'A3 (3 s) n'a pas eu besoin de la limite de 400 images.
- **Suites** : `Alundra.Tests` 1995 réussis, 0 échec, en Debug (21 s) comme en Release (24 s), contre 1939 avant la tranche ;
  convertisseur 400 sur 400. Les annexes de trace de l'intro et du héros ne changent pas. La DLL déployée dans
  `alundra-project/` est la Debug (`cmp` identique).

### E19.c2 (2026-10-01)

- **Commits** : moteur (branche `chantier/animation-logical-end-clock`, exécutés par le plan moteur) `1561fd07`, `cc6f498e`, `34ddf5e2`,
  `b40888f1` ; parent (branche `chantier/e19-opcodes`) C2 `54029c4`, C3 `371ac46`, C4 `ff2328d`, C5 `1509625`, C6 `4cab666`, puis cette mise à jour
  des docs. C1 n'a pas de commit propre (les preuves rouges entrent avec C2, C3 et C6). Le pointeur du moteur est `b40888f1` ; il n'a pas bougé
  depuis C2.
- **Rouge constaté (C1, DLL d'E19.c1 au-dessus du moteur de C0, 1 tick par image)**, conforme à la liste test par test :
  - T-D1 rend 2 à s+24 ; T-D6 à s+93 ; T-D8 à s+102 puis s+144 ; T-D20 finit sa première attente à s+16, la seconde jamais ;
  - T-D3, T-D4, T-D5 (n = 1, 2, 3), T-D7, T-D10 (n = 1, 2, 3), T-D15 (les sept premiers appels), T-D16 et T-D19 ne finissent jamais ;
  - T-D11 et T-D12 lisent un tick logique de 0 (T-D11 : (4, 1, 0), (5, 2, 0), (6, 2, 0), ...) ; T-D14 lit un tick de 0 et un compteur de 0 ;
    T-D13 lit un tick de 0 sur les images gelées et rend 2 à s+24 ;
  - verts, comme annoncé : T-D2 (s+99) et T-D9 (s+25) ;
  - A9 échoue dans sa limite de 400 images en nommant `slot 2 program @504: last 0x1C @539` ; A3 resserré échoue sur (1), 25 au lieu de 26 ; une fois (1)
    retiré, (2) donne 24 au lieu de 25 et (3) 65 au lieu de 66.
- **Rouge constaté (C2)** : seuls rouges, comme annoncé, T-D2 en 2 t/i (rend 2 à s+33 au lieu de 49) et T-D3 en 2 t/i (s+5 au lieu de 10) ; ils passent avec C3.
  Une exception de déroulé, écrite en déviation : TG2, test de la garde, est écrit avec C3 et non avec C2, où il n'aurait pas pu passer.
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sans en changer une.
  - Table T-D, 1 t/i, 60 Hz, 2 t/i : T-D1 25, 30, 12 (tick logique k en fin d'image s+k pour k = 0 à 24, drapeau Hold 1 en fin de s+24 puis 0 en fin de s+25,
    animation 0 à l'image s+25, tick 0) ; T-D2 99, 118, 49 ; T-D3 21, 25, 10 ; T-D4 91, 109, 45 ; T-D5 11, 21, 31 / 13, 25, 37 / 5, 10, 15 ;
    T-D6 91, 109, 45 ; T-D7 premier `0x1C` à s+24, rend 2 à s+25 ; T-D19 s+37 ; T-D20 s+16 puis s+36 ; T-D8 s+99 et s+140 (60 Hz : s+118 et s+168) ;
    T-D9 s+25 (60 Hz : s+30) ; T-D10 11, 21, 31 (60 Hz : 13, 25, 37).
  - T-D11 (4, 1, 0), (5, 2, 0), (6, 2, 1), (7, 2, 2), (8, 2, 3) ; T-D12 (4, 0), (5, 0), (6, 1), (7, 2), (8, 3), (9, 4) ; T-D13 tick 10 sur les dix images gelées,
    `0x1C` à s+35 ; T-D14 tick k mod 10 sur 36 images, compteur 3 après l'image 30 ; T-D15 images 54 pour un premier appel aux images 0, 1, 10 et 53, 108
    pour 54, 55 et 100 ; T-D16 tick 0 en fin d'image s, `0x1C` à s+13, compteur 0 ; T-D17 0 octet pour 1000 pas et 100 tours comptés ; T-D18 élévation 22 avec le
    biais 3 (6 sans le repli).
  - TB1, TB2, TG2, TG3 (réservés 0, 1 et 3 : tick dû posé et tick 0, rien et tick 0, tick 2 ; compte des réservés revenu à 0 à chaque appel, marque
    effacée par tout changement), TG4 : toutes les valeurs écrites. Le test annoncé en C3 rend 0, 0, 0, puis, après le changement, drapeau 0 et marque
    effacée, et 2 au quatrième appel.
  - A3 resserré : premier `0x1A @856` à l'image 26 ; 31 `0x1A @852` et 31 `0x1A @856`, chaque `@856` 25 images après le `@852` qui le précède, deux `@852`
    successifs à 66 images l'un de l'autre jusqu'à T60 (image 2008, dernier tour commencé à l'image 1981) ; toutes les autres valeurs d'A3 sont celles d'E19.c1.
  - A9 : `0x05 @1840` une seule fois ; F530 = 4 (la dérivée) ; `0x10 @530`, `0x0D @534`, `0x1A @537` et le premier `0x1C @539` à l'image 4 ; `TargetAnimationId` 11
    après `@537`, `PlayerControlFlags` 0x14 après `@534` ; `0x1A @541` à l'image 95 = F530 + 91 avec le compteur 0 et la cible 10 ; la boîte fermée avant l'image 8
    (huit images exécutées) ; `0x39 @543`, `0x06 @544` et `0x11 @547` à l'image 95 ; fin : `PlayerControlFlags` 0, T0 effacé, une seule boîte, aucun opcode sauté,
    aucune erreur inattendue.
  - A7 : le héros reste en (65273856, 49807360) sur toutes les images de `@421` à la fin (plus de 1000 images vérifiées).
- **Temps d'exécution** (Debug) : A9 environ 0,7 s ; A3 environ 1 s ; la classe `AlundraAnimationClockDriveTests` quelques dizaines de ms ; la suite complète
  environ 20 s.
- **Suites** : `CasaEngine.Tests` 2405 réussis, 0 échec (construit à part, lancé avec `--no-build`) ; `Alundra.Tests` 2064 réussis, 0 échec, en Release comme en
  Debug (1995 avant la tranche : 69 tests de plus, dont les colonnes de la table T-D comptées une par une) ; convertisseur 400 sur 400. Les annexes de trace du héros
  et de l'intro ne changent pas. La DLL déployée dans `alundra-project/` est la Debug (`cmp` identique).

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E19-1 | ~~Portails trou et escalier de la 390~~ — **réglé par la recette du 2026-09-29** : le journal montre le passage par le portail 5, qui charge la pièce B. Question d'origine : si la recette d'E19.a montre que le héros ne suit pas le capitaine par là, faut-il corriger dans E19 ou dans un chantier de transitions ? | E19.a (recette) |
| O-E19-2 | Nouveaux écarts de la décompilation relevés dans le binaire : `0x5F` (entité et taille), `0x66` (sens de la copie), compteur de `0x1C`, `y` de la boîte de nom, portrait de `Script_196_0C4`, test de zone de `GetMapEffectRecord`, `AddOneItemIfUnlocked`, `InitializeEventData`. Le portage suit le binaire. Faut-il aussi corriger la décompilation dans l'analyseur, comme pour la taille de `0x78` en E16.a ? | E19.m |
| O-E19-3 | ~~Déplacement vertical des entités nues dans le support d'arcs~~ — **tranché le 2026-10-01 (D-E19-14)** : les arcs chargent les vrais préfabs par un gestionnaire d'assets construit par le test. | E19.c1 |
| O-E19-5 | Quand un `0x5B [x,0,dir]` arrête un PNJ au tick où sa marche se termine, le mouvement de ce tick s'applique encore avec la nouvelle direction : le marin 12 de la 389 descend de 1,25 px au tick de `@1494` (mesuré en E19.a3 : y = 843,25 à l'image de `@1494`, contre 842,0 à l'image précédente, lue par le test du marin 12). Cela peut venir de la latence d'une image de `CurrentAnimationId` des PNJ, déjà relevée (le moteur synchronise l'animation en fin d'image). **Tranché par le binaire le 2026-10-01 (§1.2d, [binaire])** : l'original exécute dans l'ordre les événements de carte, les entités, `UpdateAnimation` puis la physique (`0x8002E100`, `0x8003B388` → `0x8003B3D8` → `0x8003B3E0`) ; un changement d'animation par script s'applique donc dans la physique du même tick. La DLL a une image de retard : c'est la cause du pas de 1,25 px du marin 12, et chaque panoramique du bloc de la 476 fera 48,75 px au lieu de 48. (La correction qui était annoncée dans E19.c est abandonnée : voir la suite.) **Précisé puis tranché le 2026-10-01** : chaque marche du bloc fait bien 48,0 px, mais la première de chaque panoramique dure un tick de plus et le bloc dépasse de 0,75 px après chaque panoramique. L'auteur garde ce retard (D-E19-13, ADR-0018) : le corriger aurait déplacé des points épinglés de l'intro. | E19.c1 (clos) |
| O-E19-6 | À l'apparition, l'ajustement au sol (`ClampToGround`) et `TerrainHeight` ne font rien en production : `World.AddEntity` ne fait que mettre l'entité en file, et `Entity.World` n'est posé qu'à l'intégration suivante. Le commentaire d'`AlundraWorldProxy.cs:773-778` dit le contraire (corrigé en E19.c1 T7). Faut-il corriger le comportement ? | E19.h |
| O-E19-7 | Une entité sans contrôleur ne bouge jamais en Z dans la DLL, alors que le binaire intègre Z pour toute entité active (`MoveEntity` `0x80037E34` → `ComputeZPosition`). Sur la chaîne, tous les enregistrements ont un contrôleur. | E19.h |
| O-E19-8 | `IsZForceApplied` (`+0xF8`) n'est pas porté : au tick d'un changement d'animation, le binaire remplace `ForceZ` par la valeur du jeu d'animation (131 des 395 enregistrements de sprite en ont une non nulle). Il suppose l'animation résolue avant la physique, ce que D-E19-13 ne fait pas. | E19.h |
| O-E19-9 | Les 5 animations Loop de durée 0 de l'export (banque 127 anim 0 gauche et droite, banque 151 anim 1 haut, gauche et bas) sont invisibles dans le moteur : la clé cachée de fin tombe au même instant 0 que l'image. Le binaire montre l'image figée. À corriger au convertisseur (export complet à relancer). | à placer |
| O-E19-10 | ~~Défaut du moteur sur le chemin en temps réel~~ — **réglé le 2026-10-01 par le moteur (`cc6f498e`, R2) et vérifié par le plan moteur (R1 à R4, 0 échec)** ; question d'origine : défaut du moteur sur le chemin en temps réel : à 0,02 s par image, 197 des 5205 Loop de durée positive de l'export ne bouclent jamais (le temps tombe pile sur la durée, puis la dépasse), et le sprite montre la pose cachée de fin (exemple : animations 53 et 55 du héros). **Correction planifiée** (D-E19-19) : tâche T1.1 du plan moteur d'E19.c2. | E19.c2 |
| O-E19-11 | La remise à zéro hors zone d'un événement de carte diffère du binaire : la DLL écrit sur l'entité de l'événement et ne remet pas `mapEvent.EventData` à zéro, le binaire (`0x8003C7F0`-`0x8003C804`) remet le pc et l'entrée de l'état de l'événement, `state+0x2C`, l'entité logique et l'octet de programme. Un programme B réentré reprend dans la DLL et recommence dans le binaire. Sans effet sur la 478 et la 416 (zones de toute la carte). | E19.j |
| O-E19-12 | ~~Base de la branche moteur d'E19.c2~~ — **réglé le 2026-10-01 (D-E19-20)** : l'auteur a mergé `chantier/field-move-to-contact` dans `main` du moteur (`74e97293`) ; la branche d'E19.c2 part de `main`. | E19.c2 |
| O-E19-13 | ~~Le moment de la fin Hold de Ronan~~ — **réglé le 2026-10-01 : A3 resserré épingle 26 (premier `0x1A @856`), 25 et 66 images, mesurés égaux** ; question d'origine : le moment de la fin Hold de Ronan (`0x1C @854`, image 25 de la 478) vient de l'horloge à virgule flottante du moteur, à ± 1 tick du binaire : il n'est pas épinglé. **Se ferme en E19.c2** : A3 resserré épingle 25 et 66 images. | E19.c2 |
| O-E19-15 | À l'apparition, `EvaluateEntitySupport(…, immediateAtSpawn: true)` accepte un support sans limite de portée : le bloc de la 391, apparu à 144 px au-dessus du marin 4, se pose une image sur sa tête (`PosZ` 7340032) avant de revenir sur le terrain. La fidélité de cet appui au binaire n'est pas vérifiée. | E19.h |
| O-E19-14 | Un test statique qui compte les attentes `0x1C`/`0x1D` sur une animation absente du préfab de l'acteur (attendu : 3, les Flammes des cartes 35, 38 et 39), pour voir arriver tout nouveau cas avec une future exportation. | E19.m |
| O-E19-16 | **Arrêt de D5 (recensement de `0x24`)** : 26 des 59 sites atteignables des cartes du chemin de l'histoire (Inoa 162-182, 44, 10) ne finissent pas sur « mur trouvé » dans le modèle statique (rapport `docs/census-0x24-waits.md`) : 17 « aucun mur » et 9 « indéterminé ». Dont 8 sous main tenue : le héros marche jusqu'à un mur depuis une position que le programme ne fixe pas (cartes 10 `@1212` et `@2462`, 176 `@599`, 178 `@123`, `@141`, `@146`, `@151`, 179 `@564`) ; sans main tenue : des villageois de la 10 qui marchent jusqu'au bord de la carte (`@4973`, `@5061`, `@5163`, `@5275`, `@5344`, `@5430`, `@5588`, `@5905`, où l'original termine par le rognage d'écran, non porté) et leurs sites suivants indéterminés, le héros de la 10 `@1047` et Nestus de la 165 (`@932`, `@943`). **Question** : D4 (`0x24` et U1 à U4) et D5 se commitent-ils tels quels, en acceptant ces sites comme risque connu jusqu'à E19.h (contacts, rognage d'écran) et à leurs arcs, ou faut-il d'abord porter le rognage d'écran de la carte et vérifier les positions d'arrivée du héros des sites sous main tenue ? Recommandation : commiter D4 et D5 avec le rapport (le port suit le binaire ; sur le chemin de l'histoire seuls les 8 sites sous main tenue peuvent bloquer le joueur, et ils se vérifient par un arc à l'arrivée de chaque carte) et ouvrir le rognage d'écran dans E19.h. D6 à D9 attendent cette réponse (A8 ne passe qu'avec D4 et D6). | E19.d (D4, D5) |
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
