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
- **D-E19-24 à D-E19-26** (2026-10-01, après l'arrêt de D5, O-E19-16) :
  - **D-E19-24** — Le modèle du recensement de `0x24` est corrigé, puis relancé, avant le commit de D4 et
    D5 : chaque acteur part de la position où il apparaît vraiment (`0x8A`, ou `0x2D` puis `0x64`, y compris
    les déplacements faits par d'autres programmes) ; le héros part de ses vraies arrivées (`0x53`, portails,
    case d'un `0x3B`, contact d'une interaction) ; une marche `0x0B` qui bute dans le modèle n'est plus
    « rognée » : la suite du programme est « non atteinte (0x0B bloqué) », de même pour ce qui dépend d'un
    drapeau qu'aucun programme atteint ne pose. Ces sites deviennent des **risques séparés** et n'arrêtent
    plus le commit de `0x24` ; un site du chemin encore « aucun mur » ou « indéterminé » reste un arrêt.
  - **D-E19-25** — Le rognage au bord de la carte (O-E19-17) se porte dans une tranche à part, plus tard.
  - **D-E19-26** — Une tranche **E19.d2 « Inoa après le premier livre »** suit E19.d, avant E19.e : la scène
    des villageois de la carte 10 (O-E19-18) et le saut scripté d'Alundra avec `0x25` (O-E19-19).
- **D-E19-27 à D-E19-33** (2026-10-02, après la découverte d'E19.d2) — ADR-0021 :
  - **D-E19-27** — **Blocage universel entre entités**, comme le binaire : le héros et tous les PNJ s'arrêtent au contact
    de toute entité collisionnable, en jeu libre comme sous script. Le moteur reçoit un prédicat d'obstacle optionnel
    dans l'étage champ du contrôleur (point d'extension, comme `CollisionField`) ; la DLL l'implémente avec la règle du
    binaire. Lève le report du blocage à E14 de D-E12D-1.
  - **D-E19-28** — Jusqu'à E14, les entités soulevables (`Flags & 0x600`) et cassables (natif E 2) ne sont **pas** des
    obstacles : la DLL ne sait ni les soulever ni les briser. Le héros les traverse, comme aujourd'hui.
  - **D-E19-29** — Le blocage vient avec ce qu'il exige : une entité marquée pour destruction quitte les obstacles comme
    dans le binaire ; le natif E d'index 0 et 1 (destruction) est porté, sans butin ni effet de bris (E14) ; `AnimFlags`
    est chargé depuis le jeu d'animation ; l'entité de contact du dialogue et de la saisie vient du rapport de blocage.
  - **D-E19-30** — Quand une **entité** bloque une marche `0x0B`, le détour E4.d ne s'engage pas : la marche attend,
    comme l'original. D-E19-6 garde le détour pour les contacts de case.
  - **D-E19-31** — **Saut scripté fidèle** pour toute entité : impulsion au tick du changement d'animation (jamais au
    premier changement d'une apparition ou d'une arrivée), héros en l'air tenu par le tick (amende E3.d, comme
    l'escalade), `0x25` à deux termes, `CollidedWithEntityZ` aligné sur le binaire (avancé d'E19.h).
  - **D-E19-32** — Les règles **eau et glace** du héros et le saut abaissé sur les cases `0x18` se portent maintenant,
    avec le niveau de bottes recalculé à chaque tick depuis les objets possédés. L'eau profonde et les dégâts de sol
    restent des points ouverts (O-E19-22).
  - **D-E19-33** — **Sauvegardes de test** : un outil console construit des sauvegardes préréglées (nouvelle partie plus
    drapeaux, table et position), les valide par les règles du jeu et les écrit dans le dossier du jeu, où F9 les charge.
    L'auteur le lance hors de l'app Claude.
- **D-E19-34 à D-E19-37** (2026-10-02, après la découverte d'E19.d2b) — ADR-0022 :
  - **D-E19-34** — Les obstacles que l'original détruit par du code natif non porté (environ 600 : murs à boule de fer,
    ronces enflammées, colonnes de glace, rochers et couvercles, piliers) restent **solides** jusqu'à E14, comme dans
    l'original avant l'objet ou le pouvoir. Seuls les soulevables sont exclus (D-E19-28 : tous les cassables sont
    soulevables).
  - **D-E19-35** — D-E19-30 s'étend à `0x1E` : une seule règle dans `UpdateWalkDetour` n'engage le détour que si aucune
    entité n'est en contact. Amende l'écart E4-D5 pour les seuls contacts d'entité.
  - **D-E19-36** — Un mobile qui chevauche déjà une entité reste bloqué, comme dans le binaire ; aucune règle de sortie.
    Les arrivées de la chaîne sont vérifiées par des arcs.
  - **D-E19-37** — Le calage à l'entrée sud de la case (37,46) de la 10 (O-E19-24) est **reproduit et noté** : un arc de
    contre-épreuve le documente ; le point reste ouvert jusqu'à ce que le chapitre 16 soit jouable.
- **D-E19-38** (2026-10-02, après la recette d'E19.d2b ; ADR-0023) — **E12.c se fait avec E19.f** : la boîte de texte fidèle et
  la boîte de nom (E19.f) et la fidélité fine des dialogues (E12.c : portraits, machine à écrire, pagination, curseur,
  blips et voix, `0x4C`/`0x4D`, table partagée `map_alundra`, sons d'ouverture et de fermeture) forment une seule
  tranche ; ordre inchangé : E19.d2c, E19.e, puis E19.f avec E12.c.
- **D-E19-39 à D-E19-41** (2026-10-02, après la découverte d'E19.d2c) — ADR-0023 :
  - **D-E19-39** — Le **saut à la manette** de l'original (Croix) et ses états (saut sur place, saut en marchant,
    atterrissage) entrent dans **E19.d2c**, avec le saut scripté : une seule tranche pour tout le saut du héros. Ils
    règlent aussi le héros qui reste en animation 44 après certains sauts scriptés (cartes 61 à 68 et 329).
  - **D-E19-40** — Le `0x25` d'un PNJ rend une image avant l'original à cause de l'aimantation au sol de 4 px du
    moteur : **écart accepté jusqu'à E19.h**, qui portera l'état « en l'air » des PNJ avec les contacts en Z ; les
    valeurs écrites d'avance le reflètent.
  - **D-E19-41** — L'envol caché de la carte 478 (programme `B[2]` : L2 ou R1, touches Y ou I, tenus seuls à
    l'arrivée, Alundra monte de 60 px puis retombe) **reste comme dans l'original** : le port du `0x1B` du héros le
    fait fonctionner, sans exception.
- **D-E19-42 à D-E19-44** (2026-10-02, après la découverte du saut à la manette) — ADR-0023 :
  - **D-E19-42** — Le héros **se pose sur le dessus des objets** (coffres, plateformes, interrupteurs à piétiner) et
    **est porté** par une plateforme qui bouge (règle du passager du binaire, passager recalculé à chaque tick), dans
    E19.d2c ; le plafond reste à E19.h.
  - **D-E19-43** — **Tomber d'un rebord** passe par la même mécanique que le saut, comme dans l'original (même état en
    l'air, même gravité, même atterrissage) ; les deux traces de référence « spawn » du héros sont régénérées aux
    valeurs prévues d'avance.
  - **D-E19-44** — Le **son du décollage** d'Alundra (son 10 de l'original) est joué dès E19.d2c, s'il se retrouve dans la
    banque de sons de la DLL ; sinon il est consigné.
  - **D-E19-45** — (2026-10-03, l'auteur, recette E19.r) Les **entités détruites sont recyclées** comme dans le binaire,
    au point d'`UpdateDestroyedEntities` (après chaque passe des événements de carte, sous la porte `0x48`) : remise au
    gabarit (`Status` `Destroyed`, `EntityRefId` −1), retrait des listes de la DLL, entité rendue au moteur
    (`World.RemoveEntity`). Remplace la portée « invisibilité, pas retrait ». ADR-0024.
    Decisions: see ADR-0024.
  - **D-E19-46** — (2026-10-03, l'auteur) Les **caisses et cruches soulevables restent traversables** jusqu'à E14
    (D-E19-28 gardé).
  - **D-E19-47** — (2026-10-03, l'auteur, O-E19-46) L'**image reste celle de l'original, 320 × 240**, agrandie sans
    déformation à la taille de la fenêtre et recalculée en temps réel ; le reste de la fenêtre en bandes noires (sous-tranche
    E19.s).
  - **D-E19-48** — (2026-10-03, l'auteur, Q-F1) Les **drapeaux posés au milieu d'un texte** sont portés par des marqueurs de
    position dans le Yarn (format, émetteur, preuve d'équivalence du corpus ; ADR au plan d'E19.f).
    Decisions: see ADR-0025.
  - **D-E19-49** — (2026-10-03, l'auteur, Q-F2) Les **portraits de 48 × 72** (enregistrements 122 et 162) s'affichent entiers, au
    même endroit, bas aligné : défaut de l'original corrigé.
  - **D-E19-50** — (2026-10-03, l'auteur, Q-F3) La **boîte de choix fidèle** (deux options côte à côte, validée à la Croix, sons)
    se fait dans E19.f, pour les dialogues et le livre de sauvegarde.
  - **D-E19-51** — (2026-10-03, l'auteur, Q-G1) La **ré-extraction** des données du remaster se fait par la session seule, avec la
    preuve que seuls les fichiers attendus changent (`diff -rq`) avant le miroir dans `data-extracted/`.
  - **D-E19-52** — (2026-10-03, l'auteur, Q-G2) La **semi-transparence par texel et les quads déformés** valent aussi pour les
    sprites d'entités, dans E19.g.
  - **D-E19-53** — (2026-10-03, l'auteur, Q-G3) Les **251 effets d'ambiance** qui apparaissent au chargement s'allument sur toutes
    les cartes.
  - **D-E19-54** — (2026-10-03, l'auteur, Q-G4) Les **effets natifs du warp et du ramassage** viennent avec E19.g ; ceux du combat
    avec E14.
  - **D-E19-55** — (2026-10-03, l'auteur, Q-G5) Les **rais de lumière de la 163** gardent le comportement de l'original (ils
    s'éteignent 11 ticks après l'arrivée).
  - **D-E19-56** — (2026-10-03, l'auteur) **D5 d'E19.d2c2 reprend** avec sa révision (relecture de clôture, puis exécution).
  - **D-E19-57** — (2026-10-03, l'auteur, O-E19-41) Le **son de chaque changement d'animation** est porté pour toutes les entités,
    son à l'arrivée compris.
    Decisions: see ADR-0028.
  - **D-E19-58** — (2026-10-03, l'auteur, O-E19-42) Le **glissement de la DLL est gardé** ; pas de résolution exacte des contacts
    (étape 2) pour l'instant.
  - **D-E19-59** — (2026-10-03, l'auteur, O-E19-45) L'**apparition passe en convention de la DLL** (plus de `+ 1`, relevée à
    `max(PosZ, T)`) ; `0x8A` et `0x64` sont examinés dans la même ADR.
    Decisions: see ADR-0026.
  - **D-E19-60** — (2026-10-03, l'auteur, E19.s) L'image de 320 × 240 s'agrandit d'un **facteur entier seulement**
    (`k = max(1, floor(min(L / 320, H / 240)))`), centrée, le reste en bandes noires ; tous les pixels restent égaux.
    Decisions: see ADR-0027.
  - **D-E19-61** — (2026-10-03, l'auteur) Les **sons d'apparition au chargement d'une carte** se résolvent avec le groupe de
    sons de la **nouvelle** carte : défaut de l'original corrigé (le binaire initialise les entités en `0x8002C3AC` avant de
    poser le nouveau groupe en `0x8004A09C`, ces sons sont donc cherchés dans le groupe de la carte précédente).
  - **D-E19-62** — (2026-10-03, l'auteur, E19.f2) Le **`0x4D` resté en attente** (un `0x4D` suivi dans le même tick d'un
    `0x4C` sans le bit 4 se déclenche au `0x4C 4` suivant ; 164 `@350`, 138 `@543` et `@557`) est un défaut de l'original,
    **corrigé** : le `0x4C` efface le `0x4D` en attente.
  - **D-E19-63** — (2026-10-03, l'auteur, E19.f2) Le **bit de défilement résiduel** (une attente `\A` relâchée en 1re ou 2e
    ligne fait sauter au défilement suivant de 3e ligne, dans la même boîte, son attente de 10 images) est un défaut de
    l'original, **corrigé** : chaque défilement de 3e ligne attend ses 10 images ou un appui.
  - **D-E19-64** — (2026-10-03, l'auteur, E19.f2a) L'**écart d'un tick dû à l'ordre du moteur** (les entités avant le mandataire
    du monde) est accepté pour la boîte de texte : pas de datation des ouvertures et des écritures ; les écarts sont nommés par
    origine dans ADR-0029 (F2-R1) et l'oracle en tient compte.
  - **D-E19-65** — (2026-10-03, la session en mode AUTO, à confirmer par l'auteur, E19.g G0b) Les **planches d'entités passent
    dans la disposition `Compact`** de l'extracteur (une case par signature) : la disposition `Original` donnait à 2702 des 6909
    sprites exportés la palette d'un autre (PNJ de la chaîne, Rancune de Melzas, 23 icônes d'objets) ; défaut du portage, corrigé
    par une ré-extraction (D-E19-51) ; ADR-0031.
  - **D-E19-66** — (2026-10-03, la session en mode AUTO, règle « le binaire tranche », E19.m2) La **réapparition des cellules de
    pluie tire le `rand()` de la bibliothèque C** (générateur à part dans la DLL, état 0 au lancement, jamais réensemencé) et pose
    `posX = rand() / 102`, comme le binaire ; remplace le choix du flux de D7 (`docs/plan-e9d-mode-cellulaire.md:245`), qui la
    branchait sur le flux du jeu ; ADR-0032, ADR-0050 du moteur.
  - **D-E19-67** — (2026-10-03, la session en mode AUTO, règle « le binaire tranche », E19.m4) Les **fonds ne reçoivent aucun tick
    pendant le départ d'un passage** (de l'image d'armement à la fin du fondu), comme la boucle de transition du binaire, qui
    n'appelle pas `RenderScene` ; ADR-0034.
  - **D-E19-68** — (2026-10-05, la session en mode AUTO, règle « le binaire tranche », E19.g G2c) Les **fonds rendent la
    semi-transparence par texel** comme les sprites d'entités (G2a) : le bit STP de chaque texel dans les planches des fonds
    (alpha 128), le mode PSX de la couche = `BlendMode − 1`, sans porte `Ground` (celle d'E10 §1.8 tombe) ; ADR du parent et du
    moteur.

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
- **392** : `0x0C` et `0x08` dans la boucle de roulis ; `0x8E` (balancement de caméra) en tête de B1
  (`@20`), hors de cette boucle.
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
- (E19.d2c1) Montages du saut : `AlundraJumpTestSupport.cs` (`JumpNpcRig` : PNJ à contrôleur sur sol plat ou à sprite
  d'horloge ; `JumpHeroRig` : héros possédé sur `ContactWorld` avec la sonde d'obstacles et un lecteur de sons factice ;
  `FlatCells` : cases synthétiques d'une marche donnée, eau `0x18`, glace `0x20`). Le héros d'un montage a besoin du bit
  Gravity et de `Collidable`.
- (E19.d2c1) La suite réécrit les quatre traces du héros en fins de ligne LF à chaque passage : contenu identique, `git diff
  --exit-code` rend 0 ; remettre les fichiers par `git checkout` avant un commit.

---

## 1. Tranches

### 1.1 Enveloppe

| Tranche | Contenu | Arcs de test (§1.3) | Recette en jeu |
|---|---|---|---|
| **E19.a** ✅ | Entité de contexte (`0x42`, `0x43`, et tous les opcodes sur l'entité logique), `0x59`, garde de boucle (D-E19-3), support des arcs | A0, A0b, A1 | Le capitaine sort par l'escalier et réapparaît en pièce B ; sommeil, puis 476 |
| E19.a2 ✅ | Moteur : sur le champ de cellules, un pas bloqué avance jusqu'au contact (D-E19-8) ; épingles et traces de référence du héros re-mesurées ; la cabine testée avec un vrai contrôleur | cabine seule, A1c | La cabine : Alundra s'endort, puis la 476 se charge |
| E19.a3 ✅ | DLL : `ForceAdjusted` ne se lève qu'au tick sans avance, comme le binaire (D-E19-12) ; épingles du héros re-mesurées | marin 12 de la 389 | Le marin 12 rejoint sa place en fin d'intro |
| E19.b ✅ | Carte 476 : `0xC4` sans nom (D-E19-5), `0x8A` (bloc caméra), `0x4C` gardé pour la machine à écrire | A2, A4 | La vision de Lars et Melzas jusqu'à 478, puis jusqu'à 392 |
| E19.c1 ✅ | Cartes 478 et 416 : `0x0B` avec détour (D-E19-6), `0x1C`/`0x1D` (compteur du binaire, Chain et Hold), `0x5E`, `0x08`, `0x0C`, `0x3A`, `0x89`, `0x73`/`0x74` ; arcs en vrais préfabs (D-E19-14) | A3, A7, A4p | La vision de 478 va au bout ; la plage 416 mène à la 163 |
| E19.c2 ✅ | Moteur : horloge logique exacte des fins d'animation, rendu en temps réel (D-E19-16, D-E19-17), correction des Loop figées ; DLL : pilotage à chaque tick logique, signal de boucle (D-E19-18), garde de `0x1C` sous rattrapage | A3, A9, tests moteur | Wendell à Inoa rend la main ; les fins d'animation au tick de l'original |
| E19.d ✅ | Fin de chaîne : `0x24` avec recensement (D-E19-21), `0x40`/`0x41` complets (D-E19-22), défaut d'atterrissage de la DLL (391), vrai héros et pad tenu dans les arcs ; reste de 392, 391 et 163 | A5, A5r, A6, A8 | Naufrage, plage, réveil à Inoa, main rendue |
| E19.d2a ✅ | Après le premier livre (§1.2h) : sauvegardes de test chargées par F9 (D-E19-33) ; arrivée par portail dans les arcs (U3) ; arcs du jour 1 | A10, A11, A20, TH3 | Jour 1 jusqu'au jour 2 ; F9 sur les préréglages du jour 3 et du jour 4 |
| E19.d2b ✅ | Moteur : sonde d'obstacles dans l'étage champ du contrôleur (ADR-0047 du moteur) ; DLL : contacts entre entités du binaire (D-E19-27 à D-E19-30, D-E19-34 à D-E19-37), natif E 0/1, contact du dialogue ; règle les deux P2 d'E19.d (O-E19-18, 185 `@506`) | T-A19, T-A10v, T-B9, T-C61, TN-3, A11, tests moteur | Le héros bute sur les PNJ et leur parle ; jour 4 : la 185 mène à la 362 |
| E19.d2c | DLL, en deux sous-tranches (§1.2h.3, ADR-0023) : saut scripté et `IsZForceApplied` (D-E19-31), `0x25`, `CollidedWithEntityZ`, eau et glace du héros (D-E19-32), son du décollage (D-E19-44) ; puis saut à la manette, chutes, dessus d'objets et passager (D-E19-39, D-E19-42, D-E19-43) (O-E19-8, O-E19-19) | UJ, UW, A10J, A12, A10, T-A10v, T-C61, A3 ; SJ, UH, traces « spawn » | Jour 3 : le saut de la 10 jusqu'à la 135 ; saut à la Croix, chutes, objets |
| E19.e | (§1.2i) Test statique : la liste fermée des opcodes sautés atteignables sur les 30 cartes de la chaîne ; arcs des scènes scriptées du jour 3 (176, 179, 135, 178) ; arrivées sans recouvrement ; les villageois de la 185 et 179 `B[1]` exclus (recette, O-E19-33) | A13, A14, A15, A17, A18, TH4 étendu, test statique | Nouvelle partie jusqu'au livre de la 163, sauvegarde, rechargement ; `day3-after-dream` jusqu'à la 183 ; `day4-meeting` jusqu'à la 362 |
| E19.f (§1.2j ; D-E19-48 à D-E19-50) | Boîte de nom et boîte de texte fidèle (D-E19-4) : export du cadre, écrans XAML liés à un view model, cycle de vie de la boîte de nom, pour `0x0D`/`0x5C`/`0xC4` ; **avec E12.c** (D-E19-38) : portraits, machine à écrire, pagination, curseur, blips et voix, `0x4C`/`0x4D`, table partagée `map_alundra` | tests MGDesktop | Les noms s'affichent au-dessus de la boîte, à la place de l'original |
| E19.g (§1.2o ; D-E19-51 à D-E19-55) | Effets visuels (D-E19-7) : export des effets par le convertisseur, réserve de 128 effets aux règles du binaire, `0x90`-`0x94`, `0xA0`-`0xA3`, rendu | cartes à effets | L'aura de 476, les vagues de 391 |
| E19.h (§1.2n : h1, h1b, h2, h3, h4) | Attentes en Z et contacts : `0x20`-`0x23`, `0x26`, `0x47`, `0x48` ; `ForceAdjusted` aligné sur le binaire ; glissement le long des murs ; reste du saut (O-E19-27) — `0x25` et `CollidedWithEntityZ` avancés en E19.d2c (D-E19-31) | ciblés | ciblée |
| E19.i | ~~Boucles d'animation Loop pour `0x1C`/`0x1D`~~ — **absorbée par E19.c2** (D-E19-18) : le signal de boucle et son pont y arrivent ; le recensement exact est de 208 sites dans 53 cartes, et non 101 dans 30 | — | — |
| E19.j ✅ (§1.2l, recette J3 en attente) | Événements de carte : réarmement hors zone du binaire (619 enregistrements, O-E19-11) ; aucun effet sur la chaîne | ciblés | ciblée |
| E19.k (§1.2k ; E19.k1 ✅, recette K5 en attente) | Caméra : balancement `0x8E`/`0x8F` (E19.k1, DLL seule), masque des fonds `0xA4` (E19.k2, plan moteur) | ciblés | 392, 391 |
| E19.l ✅ (§1.2m ; recettes en attente ; `0x87`/`0x95` → E14, `0xB9` → auteur) | Prédicats, branches et restes : `0x82` (avec la correction d'`AddOneItemIfUnlocked`), `0x83`, `0x84`, `0x87`, `0x3F`, `0x95`, `0x99`, `0x9A`, `0x9F` (avec `InitializeContents`), `0x57`, `0x58`, `0x4A`, `0x2A`, `0x2B`, `0x5D`, etc. ; liste fermée au recensement du moment | ciblés | ciblée |
| E19.s (§1.2q ; D-E19-47, D-E19-60) | Image native 320 × 240 (le binaire ; 236 venait de la décompilation), facteur entier, bandes noires, suivie en temps réel : résolution virtuelle du moteur (ADR du moteur), écrans d'Alundra recalculés au changement, fenêtre 1280 × 960 | tests moteur, ciblés | Fenêtre redimensionnée en jeu, plein écran, halo de la 476 |
| E19.t (§1.2r ; D-E19-57, D-E19-61) | Son de chaque changement d'animation pour toute entité, apparition comprise (binaire `0x80038BB0`, octet `0xD` pour la banque) | ciblés | Grognement de Melzas, livre des 178/179, sprint de la 392 |
| E19.m | Hygiène et clôture : taille de `0x5F` (8), libellés faux, `0x01` qui rend 0, modes aléatoires 4 et 5 de `ResolveDirectionFromParam` ; test statique : aucun opcode atteignable sauté dans le corpus hors E14 (IA native) et E18 (`0xBB`) | corpus | — |

- **Ordre** : E19.a → E19.b → E19.c1 → E19.c2 → E19.d → E19.d2a → E19.d2b → E19.d2c → E19.e, puis E19.f. Les tranches de phase 2
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
et `g_clearProgramState` sur l'entité logique (faits en E19.d, D6, D-E19-22). Le portail trou et
escalier de la 390 (O-E19-1).

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

### 1.2d E19.b — Carte 476 : la vision de Lars et Melzas ✅ (code et arcs faits et vérifiés CONFIRMED le 2026-10-01 ; **recette de l'auteur validée le 2026-10-02** (« tout fonctionne ») ; l'affichage de la vision est noté à part, O-E19-30)

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
    emplacements d'entités : depuis E19.r R3 (D-E19-45) les entités détruites sont retirées et leur `EntityRefId` est libéré, mais la DLL ne
    réutilise toujours pas le créneau libéré (les nouvelles entités sont ajoutées en fin de liste). C'est une limite connue, notée pour E14 et E19.m.
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
- **T6 — Recette en jeu (auteur)** ✅ (2026-10-02, « tout fonctionne » ; affichage de la vision : O-E19-30) :
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
  | `0x8A` apparaît désormais pour de vrai dans environ 70 cartes. Des programmes en boucle (sanctuaire 28, et 30, 134, 147, 159, 160) font apparaître des entités sans fin tant que le joueur reste sur la carte, car la DLL ne réutilise pas les créneaux libérés (depuis E19.r R3 les entités détruites sont retirées du moteur, mais les nouvelles s'ajoutent en fin de liste). | P3, introduit | Reporté : limite déjà acceptée par ce plan (risques ci-dessus, E14 ou E19.m). Aucune de ces cartes n'est sur la chaîne. |
  | Aucun test n'exerce `0x8A` sur le vrai bloc, qui porte un contrôleur : dans les arcs, le bloc est nu et `PushLogicalPositionToRoot` ne fait rien. Le second appel de `PushLogicalPositionToRoot` est hors du `try` de `SpawnEntityByRecordId`. | P3, introduit | Reporté : la recette T6 le couvre ; les arcs avec de vrais contrôleurs viennent avec O-E19-3 (E19.c). |
  | Après l'écriture de la position par `0x8A`, l'état de support et les champs `Tile*` restent ceux de la position du record jusqu'au tick suivant. | P4 | Accepté : même forme que `0x8B` ; sans effet pour le bloc et Rancune, qui ne portent personne. |
  | Tests plus faibles que le plan : l'octet fort n'est testé que sur X ; les arcs ne vérifient pas les parents (bloc → héros, Rancune → bloc) ; le fragment de texte « Tu » de `S102` est faible. | P4 | Reporté à l'hygiène d'E19.c. |
  | La correction du constructeur d'`ArcRun` n'a pas de test qui la distingue de l'ancien ordre. Un `ResetAll` qui lèverait dans `Dispose` masquerait l'exception d'origine. | P4 | Accepté : correction structurelle, déjà notée en T4. |
  | Textes en retard : la doc de classe et les commentaires de cas d'`EntitySearchService` disent encore « owner » ; la doc d'`OpenDialog` ne cite que `0x0D` et `0x5C`. | P4 | Reporté à l'hygiène d'E19.c. L'ADR-0006 ne se réécrit pas. |
  | La preuve rouge des arcs n'est pas dans l'historique : les arcs sont commités après le code. | P4 | Accepté : le verifier l'a reproduite à part, comme pour E19.a3. |

### 1.2e E19.c1 — Cartes 478 et 416 : attentes de mouvement, arcs en vrais préfabs ✅ (code et arcs faits et vérifiés CONFIRMED le 2026-10-01 ; le P1 introduit est corrigé par E19.c2 (code fait le 2026-10-01), sans merge d'E19.c1 avant celui d'E19.c2 ; recette T9 de l'auteur validée le 2026-10-02, « tout fonctionne »)

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
- **T9 — Recette en jeu (auteur)** ✅ (2026-10-02, « tout fonctionne ») :
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

### 1.2f E19.c2 — Fins d'animation exactes et signal de boucle ✅ (C0 à C7 faites et vérifiées CONFIRMED le 2026-10-01, moteur et parent ensemble ; **recette de l'auteur validée le 2026-10-02** (« tout fonctionne »))

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
- **C8 — Recette en jeu (auteur)** ✅ (2026-10-02, « tout fonctionne ») : voir plus bas.

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

### 1.2g E19.d — Fin de la chaîne : 392, 391 et 163 jusqu'au premier livre ✅ (D0 à D9 faites et vérifiées CONFIRMED le 2026-10-01 ; les deux P2 introduits, cartes 10 et 185, sont réglés par E19.d2b ; **recette de l'auteur validée le 2026-10-02** (« tout fonctionne »))

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
- **D4 — `0x24`** ✅ (faite le 2026-10-01, commitée après la correction du recensement de D5 (D-E19-24) : le port suit le binaire et les sites « non atteints » sont des risques séparés ; U1, U3 et U4 rouges sans le cas (revérifiés le 2026-10-01 : U1 rend (1, 0x24, 1) puis la fin au lieu de (1, 0x24, 0), U3 et U4 lisent `CodeIndex` 1 au lieu de 0) puis verts (U2 passe déjà : l'opcode sauté rend sa taille 1) ; A8 atteint ensuite la fin à l'image 1025 et échoue seulement sur `0x41 @56`, `0x41 @728`, `0x40 @731`, comme écrit) : le cas, une ligne sur l'entité logique, sans détour ni minuterie (D-E19-6) ; tests
  unitaires U1 à U4. A8 échoue ensuite sur `0x41 @56`, `0x41 @728` et `0x40 @731`.
- **D5 — Recensement des sites de `0x24`** ✅ (fait le 2026-10-01 ; le premier recensement s'était arrêté sur la règle de D5 (26 des 59 sites atteignables du chemin de l'histoire hors de « mur trouvé », O-E19-16) ; **modèle corrigé puis relancé (D-E19-24)** : départ des acteurs à leur vraie apparition (`0x8A`, ou `0x2D` puis `0x64`, y compris les écritures d'autres programmes), départ du héros à ses vraies arrivées (`0x53`, portails, case d'un `0x3B`, contact), une marche `0x0B` bloquée n'est plus rognée (suite « non atteinte (0x0B bloqué) »), dépendances de drapeaux `T` de la carte et `G` du corpus (« non atteint (drapeau jamais posé) ») ; population 429 sites dans 82 cartes, 395 atteignables dans 76 cartes, 34 dormants, 0 « DLL seulement », comme attendu ; classes des 395 : mur trouvé 293, vitesse nulle 1, aucun mur 4, indéterminé 4, non atteint (0x0B bloqué) 30, non atteint (drapeau jamais posé) 63 ; **chemin de l'histoire : 59 sites atteignables, 31 « mur trouvé », 0 « aucun mur », 0 « indéterminé », 28 non atteints** (risques séparés, O-E19-18 et O-E19-19, sans arrêt) ; hors du chemin, 9 sites en classe à risque (aucun mur 4, indéterminé 4, vitesse nulle 1), dont 1 sous main tenue ; les six sites de la 172 sont dormants ; rapport `docs/census-0x24-waits.md`) (D-E19-21) :
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
- **D6 — `0x40` et `0x41`** ✅ (faite le 2026-10-01 ; U5 à U13 (sans U9) écrits d'abord : les 8 rouges sur le code de D4 (tableaux non écrits, état non effacé, aucun avertissement) puis verts du premier coup ; A8 passe avec toutes ses valeurs écrites, A6 inchangé ; suite complète 2086 réussis ; l'effacement se fait dans `RunOneScriptCall` (état propriétaire effacé en fin d'appel, état d'une autre entité tout de suite, drapeau remis à zéro à chaque test), `RunScript` n'efface plus après coup) : les deux cas, comme le binaire. `0x41` écrit
  `SpriteProgramIndexes[v1]` de l'entité logique ; `0x40` écrit `ProgramIndexes[v1]` et demande
  l'effacement : propriétaire → l'état qui tourne, à la fin de l'appel (`ClearProgramStateRequested`), quel
  que soit v1, même sur un Break ou une suspension ; autre entité → son `EventProgramState` tout de suite. Le
  drapeau est remis à zéro à chaque test. v1 ≥ 6 : aucun effet (rien n'est écrit, aucun état n'est effacé),
  un avertissement une seule fois par opcode, taille rendue 3 (correction d'un défaut de l'original, qui
  écrirait hors du tableau). Tests unitaires U5 à U13 (sans U9).
  A8 passe ensuite.
- **D7 — Arcs verts** ✅ (faite le 2026-10-01 ; A6 et A8 commités avec toutes leurs valeurs écrites d'avance, sans en changer une ; table du rouge constatée : A8 après D4 échouait sur `0x41 @56`, `0x41 @728`, `0x40 @731` seuls, passe après D6 ; temps d'exécution en Debug : A6 environ 0,75 s, A8 environ 0,78 s, A5 environ 1 s, A5r environ 0,7 s, aucun arc près de 120 s) : A6 et A8 commités ; temps d'exécution relevés.
- **D8 — Hygiène** ✅ (faite le 2026-10-01 : le miroir `ImplementedOpcodes` compte `0x24`, `0x40` et `0x41` ; la table des tailles nomme `0x24` « Wait until force adjusted » et `0x40` « Set program index and clear the program state », sans changer une taille ; la doc de `RunScript` sur l'effacement avait été réécrite en D6 ; la doc de T-D15 dit les valeurs de la DLL (premier appel avant le tour : 54 ; sur le tour ou après : 108) ; la ligne E19.j, le « hors périmètre » d'E19.a et le §0.2.4 sont corrigés ; les annexes de trace ne bougent pas) : miroir `ImplementedOpcodes` (`0x24`, `0x40`, `0x41`) ; libellés de la table des
  tailles ; la doc de `RunScript` sur l'effacement (le choix laissé au port de `0x40` est fait) ; la doc de
  T-D15 qui se contredit (P4 d'E19.c2) ; la ligne E19.j de l'enveloppe ne garde que le réarmement hors zone ;
  au §0.2.4, `0x8E @20` est en tête de B1, hors de la boucle de roulis.
- **D9 — Docs** ✅ (faite le 2026-10-01 : statuts, mesures au §2 (recensement compris), points ouverts (O-E19-21), ligne du plan maître avec E19.d2 en suite, ADR-0020 amendé pour D-E19-24 à D-E19-26) : statuts, mesures au §2, points ouverts, ligne du plan maître.
- **D10 — Recette en jeu (auteur)** ✅ (2026-10-02, « tout fonctionne ») : voir plus bas.

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
   hors de « mur trouvé » (sinon arrêt, D5). **Modifié par D-E19-24** : aucun site du chemin n'est « aucun mur »
   ni « indéterminé » ; les sites « non atteints » (marche `0x0B` bloquée, drapeau jamais posé) sont des
   risques séparés, listés dans le rapport.
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

**Vérification d'E19.d (2026-10-01).**
- **Commits** : `acbb789` (D1), `c91af00` (D2), `a31689e` (D3), `dfd8ded` (arrêt de D5), `67f8941`
  (D-E19-24 à D-E19-26), `8dca230` (D4), `8ee80e9` (D5, recensement corrigé), `6d6647a` (D6), `d3c397a` (D7),
  `0a926e4` (D8), `1afc26b` (D9). Le moteur et son pointeur ne changent pas.
- **Verifier frais : CONFIRMED.** Dans un arbre jetable : `Alundra.Tests` 2086/2086 en Release,
  convertisseur 400/400.
  - Mutations attrapées : l'ancien `wasAlreadyLanded` (A6 sur `@479`, test de D2), `0x24` retiré (A8 sur les
    opcodes sautés, U1, U3, U4), `0x40`/`0x41` retirés (A8, U5 à U13), effacement du propriétaire quand
    l'entité logique est une autre (U6, U7), remise à zéro du générateur retirée (TH2), héros construit à la
    main (TH1, A8 bloqué sur `0x1C @100`).
  - Son propre recensement (décodeur, atteignabilité, rayons) redonne les 429 sites, les 395 atteignables, les
    59 sites du chemin et les 28 non atteints ; aucun site du chemin n'est « aucun mur » ni « indéterminé ».
    Quelques distances diffèrent, jamais la classe.
  - Traces inchangées ; DLL Debug déployée.
- **Trois contradicteurs en lecture seule.** La fidélité au binaire de `0x24`, `0x40`, `0x41`, de l'effacement
  dans `RunScript` et du correctif d'atterrissage est confirmée instruction par instruction. Dispositions :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | Porter `0x24` fait caler la scène des villageois de la carte 10 (G218, B[14]) : avant D4, l'opcode sauté laissait Nestus repartir de son point d'apparition ; maintenant il marche 193 px au nord jusqu'à la falaise, puis sa marche `0x0B @5172` ne peut pas partir (marche de 80 px, au-delà du pas de 3). T666 n'est jamais posé et le héros reste bloqué (`0x10 @1826`). Même chose sur la 331 (miroir de la 10). Dans l'original la scène va au bout : c'est l'écart O-E19-18, que le port fidèle de `0x24` met au jour. | **P2, introduit** | **Bloquant pour le merge** : se règle en E19.d2 (D-E19-26), décidée avant ce constat ; E19.d ne se merge pas sans E19.d2. La recette D10 ne passe pas par la carte 10. |
  | La liste des sites « non atteints » ne vaut pas liste des blocages : elle ne part que des sites de `0x24`. Neuf marches `0x0B` bloquées n'existent qu'à cause du port et n'y figurent pas (héros B[9] sur la 10 et la 331, Septimus `@506` sur la 185, Jess sur la 334 et la 365, Zane sur la 363, Beaumont sur la 366, Nestus `@5172` sur la 10 et la 331). L'argument d'O-E19-16 « seuls les 8 sites sous main tenue peuvent bloquer » est donc faux. | P3, introduit | Reporté : la découverte d'E19.d2 recense toutes les marches `0x0B` bloquées après un `0x24`, sur tout le corpus. **Reclassé le 2026-10-02** : le recensement confirme les neuf sites (O-E19-25) ; Septimus `@506` de la 185 est sur le seul chemin vers la 362 : **P2 introduit, bloquant pour le merge**, réglé par E19.d2b avec la scène de la 10 (§1.2h). |
  | L'acceptation 4 et l'arrêt de D5 gardaient l'ancienne formulation ; le chemin de l'histoire omet la 135 (église), qui commande la 178 par G1655 (six sites « non atteints » de la 135 non marqués). | P3 | Acceptation 4 annotée ci-dessus ; la 135 entre dans le périmètre d'E19.d2. |
  | Le recensement n'est pas reproductible depuis le dépôt : les scripts sont dans le scratchpad de la session. | P4 | Réglé le 2026-10-02 : scripts archivés dans `research/census/`, sans statut d'outil (O-E19-25). |
  | Écarts du modèle : une marche `0x0B` courte d'un pixel compte comme passée (la DLL cale) ; le filtre de zone d'apparition n'est pas modélisé. Aucune classe du chemin ne change. | P4 | Accepté, noté pour le prochain recensement. |
  | `0x40` touche 427 sites dans 122 cartes (782 enregistrements) ; 362 sont suivis directement de `0xFF`, donc rien ne change ; environ 65 continuent et sont désormais coupés. Sur le chemin, seules la 44 et la 163, toutes suivies de `0xFF`. | P4 | Accepté ; les chiffres du plan et de l'ADR (environ 200 entités dans 40 cartes) sont à ce titre une estimation basse. |
  | La garde de boucle coupe maintenant aussi l'état d'un programme qui a fait `0x40` plus tôt dans l'appel (le binaire n'a pas de garde) ; jamais atteint dans le corpus. | P4, introduit | Accepté. |
  | Le terrain pose `CollidedWithEntityZ` à 0 dans la DLL, à 1 dans le binaire (`0x800376E0`) ; aucun opcode ne le lit aujourd'hui (`0x25`, `0x26`, `0x47` le liront). | P4 | Reporté à E19.h ; avancé en E19.d2c (D-E19-31). |
  | La doc XML de `Dispatch` est passée sur un nouveau champ ; la doc de l'`ArcSpec` décrit encore le héros construit à la main ; « `0x3B @133` rend toujours 0 » d'A5 ne vérifie pas qu'il y a des échantillons ; A8 épingle des effets du retard D-E19-13 ; l'ordre de mise en place du héros des arcs diffère de la production (couvert par TH1) ; le rouge d'A6 et d'A8 n'est pas dans l'historique ; l'ADR-0020 a reçu un amendement au lieu d'une nouvelle ADR. | P4 | Reportés (hygiène d'E19.d2). |

### 1.2h E19.d2 — Après le premier livre : contacts entre entités, saut scripté, sauvegardes de test ⏳ (programme ; E19.d2a et E19.d2b faites ; E19.d2c en deux sous-tranches, E19.d2c1 détaillée, E19.d2c2 esquissée)

**Cadrage corrigé** (découverte du 2026-10-02, en lecture seule, chaque surface recoupée par une contre-expertise
indépendante ; notes et scripts dans le scratchpad de la session, `e19d2-disc/` et `e19d2-disc2/`).
- La « scène des villageois de la 10 » (O-E19-18, `B[14]`) exige G218 : elle est au chapitre 18, loin après le premier
  rêve, et non « juste après le livre ». Elle reste dans E19.d2 (D-E19-26) : elle se règle par le même mécanisme que la
  185.
- **Un second P2 introduit par E19.d** : sur la 185 (jour 4, réunion chez Beaumont, seul chemin vers le rêve d'Olen
  362), Septimus doit s'arrêter contre le bloc transparent rec7 (`0x24 @458`/`@463`) ; dans la DLL il file au mur et sa
  marche `0x0B @506` ne peut pas partir : T70 n'est jamais posé et le héros reste verrouillé. Il était classé P3 dans la
  vérification d'E19.d ; il passe **P2 introduit, bloquant pour le merge d'E19.d**, comme la scène de la 10 (§1.2g).
- Les deux P2 ont **une seule cause** : le blocage entre entités, absent de la DLL (D-E12D-1). La règle des cases est la
  même dans les deux jeux.
- Le saut de la 10 (`B[20]`, jour 3) est bien sur la chaîne : `163 → 162 → 165 → 164 → [jour 2] → 10/14/15 → Tarn's Manor
  → [jour 3] 179 → 44 → 179 → 176 → 179 → 176 → 10 → 135 → 10 → 176 → 178 → [jour 4] 183 → 185 → 362`. Le jour 1
  (162, 165, 164) est entièrement scripté ; le passage du jour 2 au jour 3 (combat de la 15, Tarn's Manor) et le rêve 44
  exigent le combat (E14) : d'où les sauvegardes de test (D-E19-33).

**Faits établis pour E19.d2.**
- *Contacts entre entités* **[binaire]** :
  - Chaque essai de `ComputeXYPosition` (`0x80037730`) appelle `FindEntityCollisionCandidate` (`0x80036F34`, appels
    `0x800378C4` et `0x80037908`) avant le test de case. Un mobile de la liste physique qui est lui-même collisionnable
    (`+0x6C & 0x80`, `AnimFlags +0xB4 & 0x80` nul, non porté `+0x28`) s'arrête au contact de toute entité de la liste
    des collisionnables (mêmes gardes), boîte en unités 16.16 et recouvrement en Z ; le héros en jeu libre comme les PNJ.
    L'obstacle n'est jamais poussé ; un contact d'entité ne glisse jamais.
  - La liste se construit en `0x800384F4`, après les événements et avant l'animation et la physique, **sans test
    d'état** ; le recyclage des créneaux `FlagToDestroy` (`0x80038634`) ouvre `UpdateEntities` : une entité détruite par
    un programme de carte disparaît dans le même tick, par un script d'entité ou un natif E au tick suivant. Côté DLL,
    exclure `FlagToDestroy` dans `BuildCollidables` (`EntitySupport.cs:43-71`, reconstruit en fin d'image,
    `AlundraWorldProxy.cs:2584-2599`) reproduit les deux délais à un tick par image.
  - Un mobile qui chevauche déjà un obstacle au début d'un tick reste bloqué, sauf par un pas qui sort entièrement du
    chevauchement.
  - Un contact d'entité lève `ForceAdjusted` (`0x800379D0` → `0x80037D54`) : `0x24` finit sur une entité. `0x0B`
    (`0x8003D468`-`0x8003D514`) ne lit jamais `ForceAdjusted` : l'original attend que l'obstacle s'en aille.
  - `+0x130` (`XCollisionEntity`) est le retour de `ComputeXYPosition` (écrit en `0x80037F08`) : l'entité trouvée par
    l'essai précédent, 0 si la force est nulle ou si le premier essai est libre. Le dialogue (`0x8002E910`) et la saisie
    (`0x8002EF08`) le lisent. La sonde E12.d de la DLL (`AlundraWorldProxy.cs:2209-2216`) teste un chevauchement à la
    position courante : sous des contacts exacts elle ne trouverait plus rien et on ne pourrait plus parler aux PNJ.
  - Le natif E d'index 0 ou 1 (`0x8007ED10`) ne fait que `DestroyEntity(e, -1)` (`0x8003A59C`), à chaque tick de l'état 3
    (`0x800237B4[3]` → `0x80038838`) quand le programme E scripté est vide ; index lu en `+0x80`. 417 enregistrements sur
    85 cartes l'atteignent par `0x19` (O-E19-4). `DestroyEntity` fait aussi tomber un butin (`0x80032B90`) et joue un
    effet de bris (octet d'en-tête `0x1E`, `0x8003BE74`), non portés. Dans la DLL, `RunSpriteEvent` est un no-op compté
    (`AlundraEventProgramRunner.cs:290-316`).
  - `AnimFlags` est l'octet `0xD` du jeu d'animation, rechargé à chaque changement d'animation (`0x80038B68`) ; bit 0x80
    = pas de collision entre entités (animation 1, état ouvert, des portes, grilles de fer et de la boîte bicolore ;
    animations de sable 32 à 36 et 39 du héros). **[données]** Il est déjà exporté sous le nom `Acceleration`
    (`SpriteRecordCatalog.cs:331`), mais jamais copié dans `AnimFlags` (`AlundraEntityScriptProxy.cs:172`).
  - **[données]** 2 250 enregistrements collisionnables sont soulevables (`Flags & 0x600`) sur 241 cartes, dont 2 161
    avec le natif E 2 (bris puis destruction). La DLL ne porte ni la saisie (`0x8002EDBC`) ni les coups (D-E19-28).
  - **[émulation]** Sous la règle binaire, les scènes `B[14]` et `B[9]` de la 10, la réunion de la 185 et la scène
    `B[3]` de la 179 vont au bout ; sans blocage entre entités (la DLL d'aujourd'hui), les trois premières calent.
- *Saut scripté* **[binaire]** :
  - `UpdateAnimation` (`0x80038AB4`) efface `IsZForceApplied` (`+0xF8`) à chaque appel et y écrit `lh(animSet+0xA)` au
    tick d'un changement (nouvelle cible, nouvelle ligne de direction ou chaîne). La passe de forces en fait l'impulsion
    `ForceZ = IZF << 8` (héros `0x80036884`, PNJ `0x80036AB8`, 0 pour 0x8000 sans gravité) ; `IZF * 160` pour le héros
    sur une case `0x18` sans bottes. `InitializeEntity` (`0x80039D04`) fait lui-même le premier changement d'une entité
    qui apparaît : jamais d'impulsion à l'apparition.
  - `0x25` (`0x8003DB7C`) rend 1 si `CollidedWithEntityZ` (`+0x140`) **ou** `IsOnGround` (`+0x144`) ; l'atterrissage
    sur le terrain pose `+0x140` à 1 (`0x800376E0`), la passe physique l'efface à chaque tick (`0x800383B4`). Sans le
    terme `+0x144`, la première visite de la 165 calerait au jour 1 (sauts de Bergus `@838`/`@843`).
  - En l'air, aucune tolérance de marche ; l'aimantation de 3 px ne joue que si `ForceZ == 0` (`0x80037848`).
- *Eau et glace du héros* **[binaire]** :
  - Dans la branche du héros seulement, sur des copies locales avant `IncrementForce` (`0x800367E4`) : glace
    (`VramOR & 0x20`) → pas d'accélération divisé par 16 (`0x80036954`-`0x800369C8`) ; eau (`VramOR & 0x08` et niveau de
    bottes ≤ 0) → force cible divisée par 2 (`0x800369CC`-`0x80036A50`). La branche des PNJ n'a ni l'un ni l'autre.
  - `VramOR` (`+0x180`) est calculé en fin de physique par `UpdateTileAttributes` (`0x80038110`-`0x80038190`) : le OU
    des mots de case des 4 coins de la boîte dont la hauteur + 1 vaut `ModdedPosZ` ; 0 en l'air et sans gravité ; lu
    par les forces du tick suivant. **[DLL]** `UpdateVramFlags` (`AlundraEntityScriptProxy.cs:1542-1571`, appelée en
    `:1151`, héros seul) donne déjà le même OU, une fois par image (à déplacer dans la boucle par tick).
  - Le niveau de bottes (`0x80127000`) vaut 3, 2 ou 1 si l'objet `0x1C`, `0x1B` ou `0x1A` est **possédé**, sinon 0 ;
    rafraîchi à chaque tick (`0x800307E8`, depuis la fin de `MovePlayer` `0x80032914`, verrou compris). La DLL ne le
    stocke pas : il se calcule avec `AlundraPlayerManager.GetNumberOfItem` (`AlundraPlayerManager.cs:932-946`).
  - **[données]** Eau `0x08` : 12 745 cases dans 50 cartes ; `0x18` : 12 526 cases dans 41 cartes ; glace `0x20` :
    2 750 cases dans 6 cartes. Sur le chemin de l'histoire, seules la 10 et la 416. Les bits sont dans l'export
    (`AlundraCells.walkability`) : aucune modification du convertisseur.
- *Sauvegardes* **[DLL]** :
  - Seule la touche F9 (DLL Debug) recharge : elle prend le créneau lisible **le plus récent** de
    `%LOCALAPPDATA%\AlundraGame\SaveGames` (`AlundraSaveGameDirector.cs:259`, `:427-456`) ; le livre de sauvegarde ne
    recharge pas (ADR-0013, ADR-0014). La sauvegarde s'applique à l'entrée de la carte d'arrivée ; toute carte et toute
    case valides ; arrivée au centre de la case, `PosZ = z << 20`, animation `0x36`, direction 0 ; les programmes B dont
    la zone contient le héros partent au premier tick. Les drapeaux T ne sont jamais sauvegardés.
  - API publiques : `GameSettings.SaveGames.Save` (dossier par défaut, après `ProjectSettings.ProjectName =
    "AlundraGame"`), `AlundraSaveGame.Capture`, `AlundraPlayerManager.InitializeNewGameStats` et
    `InitializeNewGameInventory`, `AlundraItemTables.GetOrCreate`, `AlundraSaveGameRules`. Aucun outil n'écrit de
    sauvegarde aujourd'hui. Les écritures sous AppData faites depuis l'app Claude sont virtualisées : l'outil se lance
    hors de l'app, par l'auteur.

**Sous-tranches.**

| Tranche | Résultat | Périmètre | Acceptation | Dépend de | Retour arrière |
|---|---|---|---|---|---|
| **E19.d2a** — Sauvegardes de test et jour 1 | L'auteur charge par F9 une partie au jour 3 après le rêve (179) et au jour 4 (185) ; le jour 1 après le livre est prouvé par des arcs partis de vraies arrivées | DLL (préréglages), outil console, support d'arcs (arrivée par portail), arcs A10, A11, A20 | §1.2h.1 | E19.d | abandon de la branche ; outil et préréglages se retirent en un revert |
| **E19.d2b** — Contacts entre entités | Les deux P2 d'E19.d sont réglés ; le héros et les PNJ s'arrêtent au contact comme dans l'original ; parler aux PNJ marche toujours | Moteur : prédicat d'obstacle dans l'étage champ du contrôleur et rapport de contact (ADR moteur) ; DLL : règle binaire, gardes (D-E19-28, D-E19-29), natif E 0/1, contact E12.d, détour (D-E19-30) | arcs T-A19 (185 → 362), T-A10v (villagers), T-B9, T-C61 (rails), test moteur T-ENG-1 ; toutes les épingles existantes inchangées ou re-mesurées d'avance ; émulation binaire des 7 nouveaux blocages avant merge | E19.d2a (recette 185) | branche moteur et branche parent abandonnées |
| **E19.d2c** — Saut, `0x25`, eau et glace | La chaîne du jour 3 passe le saut de la 10 jusqu'à la 135 ; le héros ralentit dans l'eau et glisse sur la glace ; il saute à la Croix, tombe et se pose sur les objets comme dans l'original | DLL seule, deux sous-tranches (§1.2h.3) : E19.d2c1 (impulsion, `0x25`, `CollidedWithEntityZ`, héros en l'air pour les sauts scriptés, eau, glace, `x160`, bottes, son) ; E19.d2c2 (saut à la manette, chutes, dessus d'objets, passager) | §1.2h.3.1 et §1.2h.3.2 | E19.d2b | abandon de la branche |

- **Ordre** : E19.d2a → E19.d2b → E19.d2c. E19.d2a ne touche ni au mouvement ni aux collisions : elle donne à l'auteur
  de quoi recetter les deux suivantes en jeu, et elle mesure U3 (l'arrivée sur la 165, condition du jour 1) avant tout
  changement de collisions. E19.d2b passe avant E19.d2c : les épingles du saut de la 10 se mesurent sous les règles de
  contact définitives.
- **Proposition pour E19.e** (à approuver avec E19.e) : étendre la recette de bout en bout et le test statique à la
  chaîne jusqu'à la 362 (arcs A12 à A18 de la découverte : 179, 176, 135, 178).
- **Merge** : E19.d ne se merge pas sans E19.d2b et E19.d2c ; moteur d'abord (`chantier/animation-logical-end-clock`,
  puis la branche moteur d'E19.d2b, qui en part).

#### 1.2h.1 E19.d2a — Sauvegardes de test et arcs du jour 1 ✅ (S1 à S5 faites et vérifiées CONFIRMED le 2026-10-02 ; **recette de l'auteur validée le 2026-10-02** (« tout fonctionne »))

**Résultat** : l'auteur lance un outil qui écrit une sauvegarde préparée dans le dossier du jeu ; F9 la charge ; la
partie reprend au jour 3 après le rêve (carte 179) ou au jour 4 (carte 185). Le jour 1 après le livre (162, 165, 164)
est prouvé par des arcs qui partent des vraies arrivées, U3 compris.

**Périmètre** : `Alundra/` (préréglages et constructeur de sauvegarde ; un point d'entrée de test du directeur de
warp), un nouveau projet `tools/AlundraTestSaves/` inscrit dans `alundra-casaengine-project-converter.slnx`,
`Alundra.Tests/` (tests des préréglages, support d'arcs, arcs du jour 1), `docs/` (mode d'emploi, plan).

**Hors périmètre** : aucun changement de mouvement, de collision, d'opcode ni du format de sauvegarde ; pas de
sous-module touché ; les positions [modèle] du jour 1 qui dépendent des contacts entre entités (épinglées en E19.d2b) ;
la saisie d'un emplacement de sauvegarde par le joueur (E16.e) ; l'historique complet des drapeaux d'une vraie partie
(les préréglages ne posent que ce que lisent les cartes visées).

**Tâches.**

- ✅ **S1 — Préréglages de sauvegarde (DLL), tests d'abord.**
  - Fichier `Alundra/Scripts/AlundraTestSaves.cs`, classe publique statique : la liste des préréglages et un
    constructeur qui part d'un `AlundraGameState` neuf (drapeaux à 0, table identité `AlundraGameState.cs:233` et `:299-308`), appelle
    `AlundraPlayerManager.InitializeNewGameStats` et `InitializeNewGameInventory` (tables d'objets du projet), applique
    le préréglage (drapeaux G posés ou effacés, entrées de table, compteurs d'objets), puis
    `AlundraSaveGame.Capture(state, carte, x, y, z)`. Il refuse, sans exception et avec un message : un nom inconnu, un
    drapeau T (id ≥ 0x8000), un drapeau G hors des 64 mots sauvegardés (id ≥ 2048, que `Capture` perdrait en silence),
    une sauvegarde que `AlundraSaveGameRules` refuse. Aucun accès disque, aucun état global.
  - Préréglages (valeurs écrites d'avance) :

    | Nom | Carte, case, z | Drapeaux posés | Effacés | Table | Objet 88 |
    |---|---|---|---|---|---|
    | `day3-after-dream` | 179, (17,7), 1 | G203 (`GameFlags[6] = 0x800`), G1651 et G1660 (`GameFlags[51] = 0x10080000`) | — | `[162] = 176` | 0 (`NumberOfItems[177]`) |
    | `day4-meeting` | 185, (5,18), 1 | G204 (`GameFlags[6] = 0x1000`), G1651 à G1655 (`GameFlags[51] = 0x00F80000`) | G203 | `[162] = 183`, `[176] = 183` | 0 |

    Le reste vient de la nouvelle partie : PV 10/10, MP 0/0, or 0, arme 1, objet équipé 0, faucon 0 (`Falcon` et
    `FalconTemp`), temps de jeu 0, reprises 0 ; objets 1, 17 et 25 (`NumberOfItems[3] = [35] = [51] = 1`, tous les autres
    compteurs à 0) ; tous les autres mots de drapeaux à 0 ; toutes les autres entrées de table à l'identité.
    Provenance [audit du 2026-10-02, recalcul indépendant] : sur la 179, la scène qui part au premier tick est `B[2] @328`
    (zone de toute la carte, G1651 posé, G1652 éteint) ; G1660 est posé par la 174 au jour 2, et sans lui la 176
    (`B[9] @668`) n'active pas son entité 13 ; aucun programme de la 179 ne lit G203, et la 185 ne lit ni G204 ni G1651
    à G1655 : ces drapeaux servent à la suite de la chaîne (10, 135, 176, 178). Les tables viennent des poseurs de
    l'original : 117 `@1068` (`[162] = 176`) et 178 `C[6]` (`[176] = 183`, `[162] = 183`). Au jour 4, G120 à G123 restent éteints : les quatre
    villageois de la 185 les posent quand on leur parle, avant que la réunion (`B[1]`) ne parte.
  - Tests `Alundra.Tests/AlundraTestSavesTests.cs`, écrits **rouges d'abord** : pour chaque préréglage, les 64 mots de
    drapeaux, les 500 entrées de table, les 256 compteurs d'objets, les neuf statistiques, le temps de jeu et les reprises
    égaux à la nouvelle partie plus le préréglage, la carte et la case ; validation par les vraies règles (`RealRules()` des tests existants)
    acceptée ; deux constructions égales octet pour octet ; le créneau de chaque préréglage égal à `test-<nom>` et conforme au
    motif `^[a-z0-9_-]{1,32}$` (copie de test de la règle d'ADR-0044 du moteur, provenance citée) ; nom inconnu, drapeau T et drapeau G ≥ 2048 refusés ; l'application de la
    sauvegarde par le chemin existant (motif d'`AlundraSaveGameApplyTests`) pose les drapeaux et la table dans le
    `AlundraGameState` d'arrivée.

- ✅ **S2 — Outil console.**
  - Projet `tools/AlundraTestSaves/AlundraTestSaves.csproj` (exécutable, même cible que `Alundra`), références à
    `Alundra` et au moteur, inscrit dans le `.slnx`.
  - Ligne de commande : `AlundraTestSaves <fichier projet AlundraGame.json> <préréglage> [--dry-run]` (pas d'option de créneau).
    Dans cet ordre : il lit le nom du projet dans le fichier
    (`ProjectName`, « AlundraGame ») pour `GameSettings.ProjectSettings` ; branche un journal console (sans lui, les
    avertissements de `Logs` ne s'affichent nulle part) ; charge `AlundraItemTables.GetOrCreate(<dossier du projet>)`
    (le dossier qui contient `Data/` ; la fonction ne lève jamais et rend des tables à zéro sur un fichier absent :
    l'outil vérifie qu'elles sont chargées, au moins une valeur non nulle dans `ItemsProperties` et dans `DropField3`,
    sinon refus) ; construit les règles
    `new AlundraSaveGameRules(<dossier>, chemin => File.Exists(Path.Combine(<dossier>, chemin)), tables)`, équivalent
    sur disque du catalogue d'assets de la production (`AlundraSaveGameDirector.cs:553-556`) ; construit la sauvegarde
    par S1, puis :
    - `--dry-run` : imprime la carte, la case, les drapeaux posés et effacés, la table, et le créneau visé, **sans rien
      écrire** ;
    - sinon : `GameSettings.SaveGames.Save(créneau, sauvegarde, SaveGameFormat.Json, sauvegarde.BuildMetadata())`
      (les métadonnées de F5/F6) dans le créneau fixe du préréglage, puis imprime le résultat et le chemin du fichier.
    - Créneaux fixes, portés par les préréglages de S1 : `test-day3-after-dream` et `test-day4-meeting`. `SaveGameNames`
      est interne au moteur et l'outil ne peut pas l'appeler : sans option de créneau, il n'a rien à valider ; ces deux
      noms suivent la règle publiée par ADR-0044 du moteur (1 à 32 caractères parmi `a-z0-9_-`, aucun nom réservé de
      Windows), vérifié par l'audit et par le test de S1.
    - Code de sortie non nul sur tout refus (préréglage inconnu, projet illisible, validation, écriture) ; aucun
      `try/catch` qui avale une erreur.
  - Les agents ne lancent que `--dry-run` (pas d'écriture sous AppData depuis l'app). Construire l'outil construit
    aussi `Alundra` et recopie la DLL dans `alundra-project/` : la tâche finit, comme les autres, par une build Debug de
    la DLL déployée.
  - Mode d'emploi `docs/test-saves.md` (anglais) : à lancer hors de l'app Claude, en Debug, depuis le checkout dont la
    DLL est déployée ; F9 charge le créneau le plus récent ; ce que chaque préréglage pose ; ce qu'une sauvegarde ne peut
    pas porter (drapeaux T, états locaux de carte, direction du héros). Ligne dans l'index de `docs/` s'il en a un.

- ✅ **S3 — Arrivée par portail dans les arcs (U3).**
  - Un point d'entrée de test interne sur `AlundraWarpDirector` pose un enregistrement d'arrivée en attente (carte,
    `PosX`, `PosY`, `PosZ`, animation, direction), comme le fait `BeginDepartureCore` ; `ArcSpec` gagne une arrivée
    optionnelle : quand elle est donnée, l'arc ne réécrit ni la position ni les `Tile*` du héros, et c'est
    `AdoptPlayerPawn` (donc `ClampToGround`) qui les pose, comme en production.
  - Test TH3 (rouge d'abord, faute de point d'entrée) : arrivée par le portail 162.3 sur la 165 — (19660800, 23592960,
    0), direction 16 — donne `PosZ = 1048576` et `TileZ = 1` après l'adoption (case (12,22) de hauteur 1).
  - **Arrêt** si `TileZ` ≠ 1 : la première visite de la 165 ne partirait pas en jeu (blocage du jour 1) ; question à
    l'auteur, pas de contournement dans l'arc.
  - **Mesure et déviation (exécution, 2026-10-02)** : TH3, écrit tel quel, a d'abord donné `PosZ = 0` et `TileZ = 0`.
    Cause établie dans le montage, pas dans le jeu : en production `World.InitializePlayerControllers` intègre le pion
    (`InternalAddEntities`, qui pose `Entity.World`) AVANT que le proxy du monde ne lance `AdoptPlayerPawn`, dont
    `ClampToGround` lit `Owner.World.CollisionField` ; le montage laissait le pion en file, donc l'adoption ne voyait
    aucun monde et ne relevait rien (`World` nul, constaté). Dans le mode arrivée, `ArcRun` intègre donc le pion avant
    l'entrée de carte (appel réfléchi de `InternalAddEntities`, comme la production) ; aucun autre arc n'est touché.
    Mesure après correction : `PosZ = 1048576`, `TileZ = 1`, stables sur 10 images. Le critère d'arrêt (`TileZ` ≠ 1 en
    production) n'est pas atteint ; l'auteur peut contester cette lecture.

- ✅ **S4 — Arcs du jour 1** (`Alundra.Tests/AlundraInoaDayOneArcTests.cs`, vrais préfabs, vrai héros, contrôleur réel,
  `AlundraRandom.Reset()`, conventions du §1.3, limite d'images fixée après une première mesure, ± 3 images).
  - **A20 — 162, Sybill** : drapeaux {} ; arrivée par le portail 163.0 (47972352, 27787264, 0), direction 0. Sybill
    part de (540,472) vers la droite et sa marche `@584` finit avec x ≥ 732 (le dépassement se mesure au premier
    passage) ; fin `0x11 @596` ; G1659 posé `@597`, dans le même appel. Opcodes sautés : exactement onze `0xA2` de
    `B[9]` (`@608` à `@688`), une fois chacun, au premier tick.
  - **A10 — 165, première visite** : drapeaux {} ; arrivée par le portail 162.3 (S3).
    - Premier appel de `B[1]` : `0x3B @106` rend 1 (`0x00 @121` clôt l'appel) ; `0x69 @122` est atteint au deuxième
      appel, les écritures `0x64` au troisième.
    - Positions écrites par `0x64` (`@140`-`@172`), exactes en X et Y : rec0 (29884416, 7864320), rec1 (11796480,
      16252928), rec2 (27525120, 6815744), rec3 (25952256, 8912896), rec5 (19660800, 28049408) ; T100 posé `@180`.
    - Au premier tick de `rec5 C[1]`, les cellules (12,23) à (12,26) ont la praticabilité 0x00 (0x41 avant) ; 0x41 de
      nouveau après `@721`.
    - Drapeaux dans cet ordre : T101, T102, T103, T104, T102, T105, chacun effacé par son acteur (rec5 `@703`, Wendell
      `@774`, Nestus `@944`, Bergus `@847`, Meade `@1026`).
    - Fin : `0x11 @354` (créneau B, programme `@236`), G3 posé `@351` dans la même image, `PlayerControlFlags` 0 ; aucune
      boîte ouverte à la fin.
    - Opcodes sautés : exactement `0x25 @838` et `@843`, une fois chacun (changera en E19.d2c, annoncé).
  - **A11 — 164, Septimus** : drapeaux {G3, G201} ; héros en contact avec rec1 (1092,120) et entité de contact rec1 (comme A9),
    puis appui sur Carré.
    - Boîte 130 fermée par appuis ; T6 puis T2 posés ; `PlaceHero(996,120,16)` (case (41,7)) ; T1 posé `@83` ; rec4
      apparu (position lue sur le cas `0x8B` de la DLL avant d'être écrite ; pas d'épingle supposée).
    - T200 posé à l'ouverture de la boîte 131 (`0x0D @325`) ; T201 au premier appui qui tourne la page (nœud
      `M164_S003`, `<<flag 201>>` avant la page 1 : le directeur ne tourne jamais une page seul, `0x4D @350` étant
      sauté).
    - Fin : `0x38 @437` (`C[2]`, programme `@240`) ; G4, G8, G202 posés, G201 effacé, table `[162] = 169`,
      `PlayerControlFlags` 0, T3 posé, rec4 désactivé (`C[3] @451`, image mesurée).
    - Opcodes sautés, une fois chacun : `0x4C @331`, `@351`, `@356`, `@402` ; `0x4D @350`, `@361`, `@373`, `@376`,
      `@379`, `@382`, `@392`, `@395`, `@398`. En plus, `0x58 @110` (programmes C de rec6 Beaumont et rec7 Thyea, par
      `@100`) est sauté à répétition tant que le héros n'est pas près d'eux : son nombre se mesure, il n'est pas épinglé.
    - Les positions de Septimus ne sont pas épinglées ici : dans l'original rec4 l'arrête (E19.d2b).
  - Une valeur écrite d'avance que l'arc contredit est un **arrêt** (question à l'auteur), jamais une ré-épingle.

- ✅ **S5 — Vérification et clôture** (builds, tests et mesures faits par l'exécutant, voir §2 « E19.d2a » ; verifier frais et dispositions ci-dessous, « Vérification d'E19.d2a »). Build de la solution et `Alundra.Tests` complet en **Release** d'abord
  (`--blame-hang-timeout 300s`), puis, **en dernier**, build et `Alundra.Tests` complet en **Debug** : la dernière
  build est Debug, et `cmp` ne montre aucun écart entre `Alundra/bin/Debug/<cible>/Alundra.dll` et
  `alundra-project/Alundra.dll` (chaque build de `Alundra` recopie sa DLL dans le projet exporté, et une DLL Release
  n'a pas F9) ; convertisseur inchangé ; `--dry-run` des deux préréglages lancé après cette build Debug
  (`dotnet run -c Debug … --dry-run`) et imprimé dans le rapport. Verifier frais sur l'acceptation ci-dessous ; dispositions au plan ; §0.2 et §2 mis à jour.

- ✅ **S6 — Recette en jeu (auteur).** Validée le 2026-10-02 (« tout fonctionne »).
  1. Jour 1 : nouvelle partie jusqu'au livre de la 163 (recette D10 d'E19.d), puis la 162 (Sybill), la 165 (première
     visite chez Meade) et la 164 (Septimus) ; la partie passe au jour 2 (la 162 devient la 169).
  2. Hors de l'app Claude : `dotnet run --project tools/AlundraTestSaves -c Debug -- <alundra-project\AlundraGame.json>
     day3-after-dream`, lancer le jeu, F9 : la scène de la 179 démarre ; attendu ensuite : 176, 179, 176, puis la 10 où
     le saut cale (O-E19-19, réglé par E19.d2c) — noté, pas un échec de la tranche.
  3. Même chose avec `day4-meeting` : la 185 ; parler aux quatre villageois ; la réunion démarre ; attendu : Septimus
     cale avant T70 (réglé par E19.d2b) — noté.

**Acceptation d'E19.d2a.**
1. Les tests de S1 et TH3 passent, écrits rouges d'abord (rouge noté dans le rapport).
2. `--dry-run` des deux préréglages imprime exactement les valeurs du tableau de S1, sans fichier créé (vérifié par le
   verifier : aucun `.sav` nouveau sous le dossier de l'utilisateur pendant ses propres essais).
3. A20, A10 et A11 passent avec les valeurs écrites d'avance ; A10 part d'une vraie arrivée de portail.
4. Aucune autre épingle ni trace ne bouge ; `Alundra.Tests` sans échec en Release puis en Debug ; la DLL Debug est
   déployée en dernier (`cmp` sans écart).
5. Recette S6 faite par l'auteur.

**Risques d'E19.d2a.**
- U3 : si l'arrivée ne relève pas le héros, le jour 1 cale en jeu ; S3 le mesure avant tout le reste (arrêt).
- Les préréglages ne posent que les drapeaux lus par les cartes visées : d'autres cartes visitées depuis une sauvegarde
  de test peuvent se comporter comme une partie incohérente ; la recette reste sur la chaîne.
- F9 charge le plus récent : une ancienne sauvegarde de date future ou un créneau récent illisible bloque F9 (limite
  connue d'ADR-0013).
- La phase du HUD n'est pas sauvegardée : après F9, la jauge est armée par l'entrée de carte comme dans l'original (R1) :
  elle glisse à l'écran si les drapeaux sauvés portent le verrou G1662, et reste cachée sinon.
- Construire l'outil redéploie la DLL dans `alundra-project/` dans la configuration de la build : une build Release la
  remplace par une DLL sans F9.

**Relecture d'E19.d2a (2026-10-02).**
- Plan-verifier sur `30a88d7` : **REVISE**, un P2 — S5 faisait la build Release après la build Debug et laissait une
  DLL sans F9 dans le projet exporté. Corrigé : Release d'abord, Debug en dernier, `cmp`.
- Audit des valeurs (recalcul indépendant, décodeur à lui) : drapeaux, cases, arrivée de U3, positions `0x64`, fins et
  sautés d'A10 confirmés. Corrigés : citation de la table identité ; compteurs d'objets de la nouvelle partie, temps et
  reprises ; refus d'un drapeau G ≥ 2048 ; G1660 ajouté au jour 3 ; scène `B[2] @328` de la 179 nommée ; outil (prédicat
  de catalogue sur disque, tables chargées vérifiées, journal console, `--slot` validé, métadonnées de F5/F6) ; A20 (fin
  x ≥ 732, onze `0xA2` sautés) ; A10 (`0x69 @122` au deuxième appel) ; A11 (G201 au départ, T201 au premier appui,
  `0x58 @110` sauté à répétition, désactivation de rec4 mesurée).
- Relecture neuve sur `050cfd8` : **REVISE**, un P2 — S2 validait `--slot` par `SaveGameNames`, interne au moteur et
  inaccessible à l'outil. Disposition **FIX** : l'option de créneau est retirée, chaque préréglage a son créneau fixe,
  testé en S1. P3 corrigé aussi : le critère « tables chargées » est nommé. Deuxième REVISE automatique : nouvelle
  époque de préparation, une seule relecture de clôture.
- Relecture de clôture sur `f9512d1` : **READY**.

**Vérification d'E19.d2a (2026-10-02).**
- **Commits** : `3be0d89` (S1), `62dccdd` (S2), `ad04f9c` (S3), `43b12ac` (S4), `15daf93` (mesures, part de l'exécutant de S5).
- **Verifier frais : CONFIRMED** sur les critères 1 à 4. Il a relancé `Alundra.Tests` en Release puis en Debug, en dernier (2112/2112 chaque fois,
  `--blame-hang-timeout 300s`), le `--dry-run` des deux préréglages (conforme au tableau de S1, code 0, aucun dossier créé sous `%LOCALAPPDATA%`), et
  le `cmp` de la DLL déployée. Mutations attrapées : `[176] = 182` dans le préréglage du jour 4 (2 tests de S1) ; intégration du pion retirée du support
  d'arcs (TH3 et A10). Il a vérifié dans le moteur (`World.cs:294-311`, `:360-375`) que la déviation de S3 suit l'ordre de la production.
- **Trois contradicteurs en lecture seule** (valeurs, sûreté de l'outil, qualité des tests) : aucun P0 à P2. Valeurs recalculées depuis les données :
  toutes conformes, aucune ré-épinglée. L'outil n'écrit que par `GameSettings.SaveGames.Save`, refuse avec un code non nul sans exception avalée, et le
  `--dry-run` n'écrit rien (13 lancements). La session principale a refait une build Debug après eux : à jour, `cmp` sans écart.
- **Dispositions** :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | S3 : l'arrêt écrit « `TileZ` ≠ 1 » a été atteint à la première mesure (`PosZ = 0`) ; l'exécutant a corrigé le support d'arcs au lieu de s'arrêter. Cause établie : le support laissait le pion en file, donc sans monde à l'adoption, alors que la production l'intègre avant `AdoptPlayerPawn` (`World.cs:294-311`, `:360-375`, `Entity.cs:227-229`). Avec l'ordre de production, `PosZ = 1048576` et `TileZ = 1` ; retirer cette intégration fait échouer TH3 et A10. | P4, procédure | Accepté : l'arrêt visait un défaut de production (U3), que la mesure écarte ; le défaut était dans le montage du test. Signalé à l'auteur. |
  | A11 n'asserte ni T200 (ouverture de la boîte 131) ni T201 (premier appui), mesurés aux images 12 et 13 ; seuls la fin et l'image de `@353` les couvrent indirectement. A10 ne vérifie pas que `0x11 @354` appartient au programme `@236`. | P3 et P4, introduits | Reporté à E19.d2b, qui re-mesure et ré-épingle A10, A11 et A20. |
  | A20 vérifie que les onze `0xA2` s'exécutent une fois à l'image 0, pas qu'ils sont sautés (compte toute la trace, quand A10 et A11 comptent les sautés). | P3, introduit | Reporté à E19.d2b (même fichier). |
  | S1 : l'effacement de drapeaux et les compteurs d'objets ne sont observés par aucun test (G203 vaut déjà 0 dans une nouvelle partie ; aucun préréglage n'a de compteur) : leurs mutations survivent aux 22 tests. | P3, introduit | Reporté à E19.d2b (hygiène) : un préréglage de test qui pose puis efface, un compteur, les refus d'index. |
  | Outil : le `--dry-run` accepte un `ProjectName` que le moteur refuserait à l'écriture ; le code de sortie annoncé (« 1 sur tout refus ») est faux pour les exceptions non gérées (fichier projet verrouillé, nom refusé : sortie non nulle mais pas 1) ; le mode d'emploi ne nomme pas les limites et préconditions de F9 ; aucun test automatique de l'outil. | P4, introduits | Reportés (hygiène d'E19.d2b) ; sans effet sur la recette (le vrai nom `AlundraGame` est valide). |
  | Les arcs en mode arrivée partent avec un inventaire vide (`AdoptPlayerPawn` ne donne l'inventaire de nouvelle partie que sans arrivée). Sans effet aujourd'hui. | P4, introduit | Reporté à E19.d2c : le niveau de bottes s'y calcule depuis les objets possédés. |
  | Deux tests portent le libellé « TH3 » (celui d'E19.d D1 et celui d'E19.d2a S3). | P4 | Reporté (hygiène). |
  | L'empreinte sha256 de la DLL consignée au §2 ne désignait plus la DLL déployée, reconstruite par le verifier depuis les mêmes sources. | P4 | Corrigé au §2 dans ce commit. |

#### 1.2h.2 E19.d2b — Contacts entre entités ✅ (B1 à B8 faites et vérifiées CONFIRMED le 2026-10-02, sans démo moteur ; **recette de l'auteur validée le 2026-10-02** (« tout fonctionne »), jour 4 jusqu'à la 362 compris)

**Résultat** : le héros et les PNJ s'arrêtent au contact exact de toute entité collisionnable, comme dans le binaire ;
les deux P2 introduits par E19.d sont réglés (la réunion de la 185 mène à la 362 ; la scène des villageois de la 10
va au bout) ; parler aux PNJ marche toujours, par le contact.

**Découverte** (2026-10-02, lecture seule, cinq surfaces dont quatre contre-vérifiées par des émulations indépendantes ;
notes dans le scratchpad, `e19d2b-disc/`). Faits porteurs :
- **Règle de bord tranchée** **[binaire]** : `FindEntityCollisionCandidate` (`0x80036FE0`-`0x800370B8`) est
  semi-ouvert ; `Width = (taille << 16) - 1` (`0x80039CB8`) ; par axe, `delta = Mod(obstacle) - Mod(mobile)`, recouvrement
  si `delta < Width(mobile) + 1` (delta ≥ 0) ou `-delta < Width(obstacle) + 1` (delta < 0) : **un contact affleurant ne
  bloque pas**, une unité de moins bloque ; premier candidat de la liste qui recouvre en X, Y et Z. Le port E12.d de la
  DLL (`AlundraEntityCollision.cs:40-111`, `AlundraEntitySpawnFactory.cs:697-699`) est déjà identique ; seuls le texte
  (`AlundraEntityCollision.cs:27-33`, `docs/plan-e12d-interaction-joueur.md:86-88`) et le nom du test
  `FlushContact_Counts_TheDerivedPlusOne` (`AlundraEntityCollisionTests.cs:42-52`) disent le contraire.
- **Gardes** **[binaire]** : le mobile doit avoir `Flags & 0x80`, `AnimFlags & 0x80` nul et ne pas être porté ; la liste
  des collisionnables (`0x800384F4`) a les mêmes gardes **sans test d'état**, héros compris ; la liste physique des
  mobiles exige l'état 2 ou 3. Un contact d'entité ne glisse jamais ; l'entité est testée avant la case.
- **Contact du dialogue** **[binaire]** : `+0x130` (`0x80037F08`) vaut l'obstacle qui a raccourci ou annulé le pas du
  tick, le plus proche du contact quand deux se suivent (dernier rejet), 0 sinon ; pour un mobile sans gravité ou en l'air,
  le binaire le met à 0 dans environ la moitié des ticks raccourcis (écart accepté).
- **Un seul chemin** **[DLL]** : `MoveControllerAndPullPosition` (`AlundraEntityScriptProxy.cs:1846-1878`) pour le
  héros (`TickPlayer`) comme pour les PNJ (`TickScriptedNpc`) ; tirage 16.16 par `Math.Round((double)racine * 65536.0)`
  (`:1862-1863`) ; téléports (`0x64`, `0x65`, `0x8B`, `0x2D`, arrivées) par `PushLogicalPositionToRoot` → `Teleport`,
  jamais bloqués ; entités sans contrôleur jamais bloquées.
- **Liste de la DLL** : `BuildCollidables` (`EntitySupport.cs:43-71`), reconstruite en fin d'image
  (`AlundraWorldProxy.cs:2586-2599`), héros en tête, sans test d'état ; elle sert aussi l'appui vertical
  (`TryFindSupport`, `UpdateRidingEntities`, `EvaluateEntitySupport`) : l'exclusion des soulevables va dans le prédicat,
  pas dans la liste. Le mouvement d'une entité `FlagToDestroy` continue aujourd'hui (le binaire l'exclut).
- **Natif E 0/1** **[binaire]** : `0x8007ED10` = `DestroyEntity(e, -1)` (`0x8003A59C`), à chaque tick de l'état 3 quand le
  programme E scripté est vide ; port dans `RunSpriteEvent` (`AlundraEventProgramRunner.cs:290-316`).
- **AnimFlags** : octet `0xD` du jeu d'animation (`0x80038B68`), exporté sous le nom `Acceleration`
  (`SpriteRecordCatalog.cs:331`), jamais copié (`AlundraEntityScriptProxy.cs:172`) ; une porte qui s'ouvre cesse de
  bloquer un tick plus tard dans le binaire, un ou deux dans la DLL (script d'entité, programme de carte).
- **Détour** : `0x0B` (`0x8003D468`) et `0x1E` (`0x8003D8D8`) ne lisent jamais `ForceAdjusted` ; la DLL leur donne le
  même détour (`UpdateWalkDetour`, `AlundraEventProgramRunner.cs:2264`).
- **Écarts mesurés et acceptés** : avance par axe du moteur contre division conjointe du binaire (un mobile qui pousse
  en diagonale contre un PNJ glisse dans la DLL, reste collé dans le binaire ; 5,6 % des pas obliques bloqués finissent
  jusqu'à 2 px ailleurs) ; `ForceAdjusted` sur contact d'entité 0 à 2 ticks plus tôt à l'est et au sud, même position ;
  contact au flottant (4 unités 16.16 entre 512 et 1024 px, 8 au-delà) ; liste d'un tick de retard pour `0x62`/`0x63` et
  les créations par script ; Z des PNJ un tick en retard sur le vertical.
- **Épingles existantes** : sur 24 tests d'arcs, seul A11 change (Septimus s'arrête contre rec4 puis contre le héros,
  19 images plus tôt). Changent aussi, par construction (D-E19-29), les trois montages qui reposent sur la sonde de
  chevauchement de fin d'image (`AlundraWorldProxy.cs:2208-2216`) : `AlundraInteractionPassTests` (`:142`, `:161`) et
  `AlundraSaveBookEndToEndTests` (`:132`, héros sans contrôleur posé sur le livre) ; `SailorThirteen`
  (`AlundraDialogueOpcodesProductionTests.cs:339-375`) reste vert mais son miroir devient inerte. Inchangés : les
  traces de l'intro (entités nues ou hôte à liste vide), le marin 12, la cabine, les tests de mouvement. Garde-fous à
  conserver : marge d'une unité en Z d'une entité posée sur une autre (`AlundraNpcCharacterControllerMoverTests.cs:1801`,
  `:1996`) : le prédicat lit les champs logiques entiers, jamais la racine flottante en Z.
- **Scènes cibles** (émulées, reproduites par un mover float32 indépendant) : 185, 10 `B[14]`, 10 `B[9]` et 61 `B[6]`
  vont au bout avec le prédicat et calent sans lui ; plusieurs contacts sont à 0 px (arrivée de la 179, héros sous le
  livre de la 178, Naomi sous le héros de la 10, Giles et le héros de la 176 à 0,625 px) : la boîte semi-ouverte exacte
  est décisive (en boîtes fermées la scène `B[14]` ne finit jamais).
- **Porte avant merge franchie** : des sites que le correctif bloquerait d'après le recensement, seul 346 `@943` est aussi
  bloqué dans le binaire (plateforme contre un mur à boule de fer : fidèle, D-E19-34) ; tous les autres (52, 199, 426, 61,
  62, cimetière de la 10 et de la 331, 240) passent : artefacts du modèle statique. Les sites « nuage » des cartes de
  l'histoire non émulés restent couverts par les arcs des cartes jouées.
- **Jeu libre** : aucun passage obligé de la chaîne n'est fermé ; au jour 1 de la 162, les sorties secondaires 9, 12, 13
  et 14 sont gardées par Sierra, Talis, Kline et Yuri (choix de level design apparent, hors chaîne).

**Périmètre** : moteur (plan `CasaEngineMonogame/ai-agent/tasks/field-movement-obstacles-tasks.md`, ADR-0047, branche
`chantier/field-movement-obstacles`) ; DLL `Alundra/` ; `Alundra.Tests/` ; `tools/AlundraTestSaves/` et `docs/test-saves.md`
(hygiène reportée d'E19.d2a) ; docs du parent. **Hors périmètre** : glissement et division conjointe (E19.h) ; saut,
`0x25`, eau et glace (E19.d2c) ; destruction des obstacles natifs, saisie et coups (E14) ; `0x28`-`0x2B` (E19.l) ;
rognage au bord (O-E19-17) ; le calage de la case (37,46) (reproduit, D-E19-37).

**Tâches.**

- ✅ **B0 — Plan et ADR.** Ce plan, le plan moteur (commits `b3aa47ca`, `c372e946` puis `c72d08a8` sur `chantier/field-movement-obstacles`) et
  l'ADR-0022 du parent. Fait avec la relecture.

- ✅ **B1 — Moteur** : exécuter le plan moteur (T0.1 à T2.1, `CasaEngine.Tests` sans échec), puis pointer le
  sous-module du parent sur sa dernière tâche (commit `chore(engine): ...` du parent). Le pointeur ne bouge que là.
  - Fait le 2026-10-02 : plan moteur exécuté (commits `4e6bd6bd`, `3dc98385`, `c57c120f`, `d509bc10`, `c2e4fdce`) ;
    `CasaEngine.Tests` 2441 réussis, 0 échec (base 2405 : +36 tests nouveaux) ; le pointeur du parent passe à `c2e4fdce`.

- ✅ **B2 — Liste, mobiles, AnimFlags, natif E (DLL), tests d'abord.**
  - `BuildCollidables` exclut `Status == FlagToDestroy` ; la porte du mouvement (`RunGameplayBlockableUpdate`,
    `AlundraEntityScriptProxy.cs:961-1090`) ne fait bouger qu'une entité `Status.IsActive()` (Normal ou Deactivated).
  - `SyncAnimation` (`AlundraFrameSyncPasses.cs:123`, juste après `CurrentAnimationId`, avant le retour sans sprite)
    copie `AnimSetsByAnim[CurrentAnimationId].Acceleration` (octet entier) dans `AnimFlags`, 0 si l'animation manque.
  - `RunSpriteEvent` : `EventTrigger == ProgramEDeactivate` et `(uint)SpriteProgramIndexes[4] <= 1` →
    `ScriptHost.DestroyEntity(entity, -1)` ; natif 2 et autres index inchangés.
  - Tests **T-R2** (un candidat `FlagToDestroy` absent de `Collidables` ; Deactivated et Loaded présents ; une entité
    `Flags & 0x600` présente dans `Collidables`) ; **T-R5** (`EventTrigger` 4, programme E scripté vide :
    `SpriteProgramIndexes[4]` 0 ou 1 → `FlagToDestroy` ; 2 → inchangé ; programme E scripté → pas de destruction ;
    créneaux C et F → pas de destruction ; chaîne `0x19` → une image → `FlagToDestroy` → l'image suivante, absent de la
    liste) ; **T-R6** (jeu d'animation {0 : `Acceleration` 0x00, 1 : 0xD0} : cible 1 puis `SyncAnimation` → `AnimFlags`
    0xD0 ; cible 0 → 0 ; entité sans sprite : chargé aussi).
  - Commit : `feat(alundra): drop destroyed entities from collidables, load AnimFlags, destroy on native E 0 and 1`
  - Fait le 2026-10-02. Rouge d'abord : 8 des 17 nouveaux tests échouent sur la DLL d'avant (`AlundraEntityDestructionTests` : T-R2 deux,
    T-R5 index 0 et 1 et la chaîne `0x19`, T-R6 trois ; les 9 autres sont des contre-épreuves vertes, index 2 et 3, autres créneaux,
    programme scripté) ; vert après : 17 sur 17. `Alundra.Tests` 2129 réussis (2112 avant), aucun test existant modifié. Le support
    de test partagé (`AlundraContactTestSupport.cs` : sol plat, hôte à liste de collidables, monde réel à contrôleurs) sert B3 et B4.

- ✅ **B3 — Prédicat d'obstacle (DLL), tests d'abord.**
  - Classe `AlundraMovementObstacleProbe : IMovementObstacleProbe` (interface du moteur, namespace
    `CasaEngine.Framework.Physics`), installée par `InstallCellAndOverlaySystems`
    (`AlundraWorldProxy.cs:850-871`) à côté du champ, à chaque chargement de monde.
  - `TryFindObstacle(mover, racine candidate, out obstacle)` : proxy du mobile ; X et Y candidats =
    `Math.Round((double)racine * 65536.0)`, exactement le tirage de `MoveControllerAndPullPosition` ; Z = `PosZ` logique
    du mobile (champs entiers, jamais la racine flottante) ; parcours indexé de `ScriptHost.Collidables`, sans
    allocation, en sautant le mobile lui-même et tout candidat `(Flags & EntityFlags.PickupKindMask) != 0`
    (D-E19-28) ; la règle est celle de `FindEntityCollisionCandidate`, par une surcharge qui prend la position du sujet ;
    premier candidat de la liste qui recouvre.
  - `DrawDebug` : les boîtes de la liste, sous `DisplayPhysics`.
  - Tests **T-R1** (règle pure, déjà portée, en dimensions réelles : héros Pos (32768000 ; 19660800 ; 3145729), boîte
    (-10, -7, 0 ; 21, 15, 32) contre un villageois (-10, -7, 0 ; 20, 14, 32) : est X 34144256 → null, 34144255 → villageois ;
    ouest 31457280 → null, 31457281 → villageois ; sud Y 20643840 → null, 20643839 → villageois ; nord 18743296 → null,
    18743297 → villageois ; Z haut PosZ 5242881 → null, 5242880 et 5242879 → villageois ; Z bas 1048577 → null, 1048578 →
    villageois ; héros PosZ 3145728 : villageois 5242880 → null, 5242879 → villageois ; Nestus PosY 33947648 → null,
    33947647 → Meade, 33947649 → null avec Meade (62881792 ; 33030144) et Nestus X 63438848) ;
    **T-R3** (contrôleurs réels, cases plates de hauteur 48, positions posées sur les champs logiques : Nestus (968,0 ;
    552,0) vers le nord, pas imposé de -1 px par tick par `MoveControllerAndPullPosition(0, -1)` (forces imposées, sans
    accélération), contre Meade immobile (960,0 ; 504,0), posée sur un pixel entier parce que le
    tirage tronque une téléportation : PosY finale 33947648, soit `Nestus.PosY - 7 px = Meade.PosY + 7 px` ; `XCollisionEntity`
    null aux ticks 0 à 33 puis Meade dès le tick 34 ; `ForceAdjusted` 0 aux ticks 0 à 33 puis 1 dès le tick 34 ; Meade
    dont `0x63` efface Collidable : plus d'arrêt à l'image qui suit la reconstruction de la liste) ;
    **T-R4** (contact est : le mobile finit affleurant, bord droit = bord gauche de l'obstacle à un ULP près,
    `XCollisionEntity` = l'obstacle au tick qui raccourcit, `ForceAdjusted` 1 au tick suivant ; commentaire : le binaire
    laisse 0 à 3 unités et lève le drapeau 0 à 2 ticks plus tard) ; **T-R-ID** (deux obstacles sur le chemin, le premier
    de la liste plus loin, le second plus près : l'obstacle rapporté est le plus proche) ; **T-REG-Z** (plateforme PosZ
    24117249, profondeur 2097151 : mobile à PosZ 26214401 → null, à 26214400 → la plateforme ; les tests
    `AlundraNpcCharacterControllerMoverTests.cs:1801` et `:1996` restent verts avec la sonde installée) ; **T-R-LIFT**
    (une caisse `Flags & 0x600` sur le chemin ne bloque pas ; un mur à boule de fer bloque, D-E19-34) ; **T-R9**
    (héros (500 ; 300), direction 20, vitesse 256, pas (139008 ; -92672), villageois affleurant en (521 ; 296) : X
    bloqué, Y descend de 1,4140625 px par tick, `XCollisionEntity` = le villageois ; écart au binaire, où le héros reste
    immobile, écrit dans le test).
  - Commit : `feat(alundra): block movers at entities through the controller's movement obstacle probe`
  - Fait le 2026-10-02. Rouge d'abord : 20 des 32 tests de `AlundraMovementObstacleProbeTests` échouent contre un prédicat qui ne bloque rien et une
    surcharge de règle qui rend null (T-R1 douze lignes, T-R3 deux, T-R4, T-R-ID, T-R-LIFT, T-R9, `DrawDebug`, les deux contrôles d'ordre et de drapeaux
    soulevables) ; les 12 autres sont des contre-épreuves (flush libre) et T-REG-Z, qui garde le comportement (marin soutenu sur sa plate-forme avec un
    jeu d'une unité) : verts avant et après. Vert après : 32 sur 32, aucune valeur du plan contredite. `Alundra.Tests` : aucun test existant modifié par
    B3, sauf A11 (ci-dessous). Précisions : (1) dans T-R1 les positions du villageois sont celles que les valeurs écrites imposent (501 px, 301 px, z 48 px
    plus une unité : héros à l'est ou à l'ouest, au sud ou au nord) ; (2) la sonde reçoit l'hôte du monde (`AlundraMovementObstacleProbe(IAlundraScriptHost)`),
    qui lui donne la liste pour le test et pour le tracé ; (3) T-R3 et T-R4 lisent l'obstacle dans le rapport de contact du contrôleur (`LastContact`) :
    `XCollisionEntity` n'est écrit qu'en B4, où T-R7 le vérifie tick par tick ; (4) T-REG-Z est un équivalent synthétique (une plate-forme à
    `PosZ` 24117249 et un marin à gravité qui marche dessus avec le jeu d'une unité, supports de l'étage E4.f réels), non une relance des deux tests
    de la carte 389, qui n'ont pas la sonde installée : la règle pure est testée aux trois valeurs du plan (26214401, 26214400, 26214399).
  - **Déviation d'ordre (A11)** : l'installation de la sonde dans `InstallCellAndOverlaySystems` change A11 et seulement A11 (le reste de la suite reste vert,
    mesuré), alors que le plan ré-épingle A11 en B6. Pour ne laisser aucun commit rouge, le ré-épinglage d'A11 est fait dans le commit de B3, aux valeurs écrites
    d'avance, sans en changer une : A11 rouge sur ses anciennes valeurs avec la sonde installée, puis vert avec les valeurs du plan (contacts de Septimus {rec4,
    héros}, `@341` et `@346`, `@386` ≥ 68222976, images 48, 175, 189 et 189, T200 et T201, `rec4` Deactivated puis détruit une image plus tard). Le support
    `AlundraArcSamples.cs` (instantanés au premier instruction qui suit un pc) sert aussi les arcs de B6.

- ✅ **B4 — Contact du dialogue et détour (DLL), tests d'abord.**
  - `MoveControllerAndPullPosition` remet `XCollisionEntity` à null avant le `Move`, puis y écrit le proxy de
    `H2Obstacle ?? H1Obstacle` (le dernier axe traité) ; null si rien n'a bloqué ou si le déplacement demandé est nul ; le
    gel garde la valeur. La sonde de chevauchement de fin d'image (`AlundraWorldProxy.cs:2208-2216`) est retirée.
    `CheckEntityInteraction` ne change pas. Un compteur interne `EntityBlockCount` (incrémenté à chaque `Move` qui
    rapporte un obstacle) sert aux arcs.
  - `UpdateWalkDetour` ne s'engage que si `ForceAdjusted != 0` **et** `XCollisionEntity == null`, pour `0x0B` et
    `0x1E` (D-E19-30, D-E19-35).
  - Tests **T-R7** (héros à contrôleur réel, au sol, contre un PNJ `InteractRequiresButton` : `XCollisionEntity` = le PNJ
    au tick du contact, null quand il s'écarte, null pour un déplacement demandé nul, gardé pendant le gel) ; **T-R8**
    (`0x0B` et `0x1E` avec `ForceAdjusted` 1 et `XCollisionEntity` posé : ni chemin ni `WalkDetourAttempted` ; avec
    `XCollisionEntity` null : détour comme avant, tests E4.d existants inchangés).
  - **Montages réécrits** (changement propre à la tranche, D-E19-29) : `AlundraSaveBookEndToEndTests` → **T-REG-E12D-1**
    (carte 17, livre rec0 (84 ; 328 ; z 0), boîte y 320 à 336 ; héros à contrôleur réel posé en (84 ; 360 ; 0), Haut
    tenu : au contact PosY = 22478848 (343 px) et `XCollisionEntity` = le livre ; Carré démarre le flux du livre ;
    jumeau posé en PosY 343 sans pousser : aucun contact écrit) ; les deux P-a d'`AlundraInteractionPassTests` →
    **T-REG-E12D-2** (joueur à contrôleur, marin collisionnable à 40 px en X, Droite tenu : contact écrit à l'image de
    l'arrêt ; null après un `Move` nul ; gardé pendant le gel `MenuOpen`) ; le miroir de `SailorThirteen` suit le site
    de production.
  - Commit : `feat(alundra): take the interaction contact from the blocking report and keep walk detours off entities`
  - Fait le 2026-10-02. Rouge d'abord, sur la DLL d'avant B4 (la sonde installée, `XCollisionEntity` encore écrit par la passe de recouvrement de fin d'image) :
    7 des 16 tests des trois classes `AlundraEntityContactReportTests`, `AlundraSaveBookEndToEndTests` et `AlundraInteractionPassTests` échouent : T-R7 (deux),
    T-R8 (deux), T-REG-E12D-1 (un), T-REG-E12D-2 (deux) ; les 9 autres sont les contre-épreuves (détour sur contact de case, jumeau posé sans pousser) et les tests inchangés. Sur la DLL de B4 avant les
    réécritures, exactement les trois montages nommés par le plan échouaient (`AlundraSaveBookEndToEndTests`, les deux P-a d'`AlundraInteractionPassTests`) ; aucun autre
    test existant n'a bougé. Vert après : `Alundra.Tests` 2178 sur 2178 (dont 4 tests d'exploration non commités, et les 5 arcs de B6 non encore commités). Aucune valeur du
    plan contredite : le héros du livre finit à `PosY` 22478848 (343 px) et nomme le livre, au pixel prévu.
  - Réécritures (D-E19-29) : **T-REG-E12D-1** (`AlundraSaveBookEndToEndTests`) : héros à contrôleur réel (les réglages de l'export), posé 32 px au sud du livre, Haut tenu ; jumeau
    posé à y = 343 sans pousser (aucun contact, le flux ne démarre pas). **T-REG-E12D-2** (les deux P-a d'`AlundraInteractionPassTests`, remplacés) : héros à contrôleur réel
    dans un monde réel, marin collisionnable à 40 px en X, Droite tenu au pad (chaîne réelle pad, `MovePlayer`, `TickPlayer`, `Move`) : contact à l'image de l'arrêt, null une fois
    le pas nul, gardé pendant le gel `MenuOpen`. Le miroir de `SailorThirteen` (`AlundraDialogueOpcodesProductionTests`) garde son code et son commentaire dit qu'il tient lieu du
    contact du pas du héros (le banc n'a pas de contrôleur).
  - Code : `MoveControllerAndPullPosition` remet `XCollisionEntity` à null, fait le `Move`, puis y écrit le proxy de `H2Obstacle ?? H1Obstacle` et incrémente
    `EntityBlockCount` ; la passe de recouvrement de `AlundraWorldProxy.Update` est retirée ; `UpdateWalkDetour` ne s'engage que sans entité en contact (0x0B et 0x1E).
    T-R3, T-R4 et T-R9 (B3) lisent aussi `XCollisionEntity`.

- ✅ **B5 — Textes.** Commentaire d'`AlundraEntityCollision.cs:27-33` et nom du test `FlushContact_*` corrigés (le
  contact affleurant ne recouvre pas) ; note datée dans `docs/plan-e12d-interaction-joueur.md:86-88`.
  Commit : `docs(alundra): state that a flush contact does not overlap`
  - Fait le 2026-10-02 : le commentaire de la méthode, le test renommé `FlushContact_DoesNotOverlap_OneUnitLessDoes` (mêmes deux assertions : l'ancien « flush » de la
    ligne était une unité en deçà du contact affleurant, l'ancien « apart » en est le contact affleurant) et la note datée du plan E12.d.

- ✅ **B6 — Arcs** (vrais préfabs, vrai héros, contrôleurs réels ; images jamais absolues ; contacts en relationnel
  exact à un ULP de la coordonnée près ; positions de transition à ± 2,5 px ; une valeur contredite est un **arrêt**).
  - **T-REG-0** : `EntityBlockCount` vaut 0 à la fin de A1c, A3, A4p, A5, A5r, A6, A7, A8, A9, A10 et A20 (preuve que
    leurs épingles n'ont pas de raison de bouger) ; leurs épingles restent inchangées.
  - **A11 ré-épinglé** (drapeaux {G3, G201}, `PlaceHero(996, 120, 16)`) : contacts de Septimus = {rec4, héros}
    seulement ; `0x24 @341` finit en (71565312 ; 7995392) contre rec4 ; `@346` finit en (66650112 ; 7995392) contre le
    héros, `ForceAdjusted` 1 ; `0x0B @386` finit avec PosX ≥ 68222976 (valeur du modèle 68272128) ; images (C,276) 5,
    (C,289) 9, (B,86) 10, (C,325) 11 inchangées ; T200 vu posé à l'image de l'ouverture de la boîte 131, T201 au premier
    appui (images 12 et 13) ; (C,353), (C,417), (C,437) et (C,451) avancent de 19 images (48, 175, 189, 189 ± 3) ; rec4
    Deactivated à l'image de fin, `FlagToDestroy` une image plus tard. Reprend les P3 reportés d'E19.d2a sur A11.
  - **T-A19** (185, drapeaux G204, G120 à G123 ; arrivée par le portail 183.6 (60555264 ; 24641536 ; 0), direction 16,
    puis `PlaceHero(132, 296, sol)`) : rouge sur la DLL d'avant B3 (`slot 2 program @452: last 0x0B @506`, Septimus en
    (15073280 ; 25821184), T70 jamais posé) ; vert : héros `@95` (6946816 ; 19398656) ; Septimus `@458` (8650752 ;
    21626880) contre rec7 en (7864320 ; 22544384), relation `Septimus.PosY + 6 px = rec7.PosY - 8 px` ; `@463` PosX
    11927552 ; rec7 hors de la liste l'image qui suit sa destruction ; `@506` finit avec PosY ≥ 22413312 ; T10, T30, T40,
    T50, T60, T70 dans cet ordre ; `0x53 @147` vers la 362 en (16515072 ; 17301504 ; 2097152), effet 4. Positions après
    `@506` non épinglées (jamais jouées dans la DLL ; le détour E4.d sur contact de case de Lutas `@219` n'est pas
    modélisé).
  - **T-A10v** (10 `B[14]`, drapeaux G218, G456) : rouge (dernier pc de Nestus `0x0B @5172`, Nestus calé à PosY 23527424 avec X dans [980,0 ; 982,0], valeur du modèle
    64323584) ; vert :
    Meade au repos (62881792 ; 32997376) ; Nestus `@5163` PosY 33914880 avec `Nestus.PosY - 7 px = Meade.PosY + 7 px` ;
    `@5166` finit avec X dans [980,0 ; 981,0] ; `@5172` finit avec PosY ≤ 32112640 ; T666 ; Bergus `@5061` PosX 63963136
    avec `Rumi.PosX - Bergus.PosX = 20 px` ; T668 ; `@5071` finit en (67108864 ; 36175872) à ± 2,5 px (fin de marche au rayon) ; T674 posé puis effacé ;
    `0x53 @2003`. Dans cet ordre.
  - **T-B9** (10 `B[9]`, drapeaux G216, G485, G1666 à G1669, G482 éteint ; héros (898 ; 736 ; 16)) : rouge (`0x0B @1215`
    du héros et `@3673` de Septimus ; héros (58851328 ; 17235968), Septimus calé au contact du mur (36,16) en (58851328 ; 17170432)) ; vert : Septimus
    `@3670` (59006976 ; 39190528) contre rec41 ; héros `@1212` (58851328 ; 39256064) ; `@3673` PosX 54214656 à ± 2,5 px (fin de marche au rayon) ; `@1215` PosX
    dans [x0 - 52,5 ; x0 - 48,5] px ; rec41 détruit puis absent de la liste l'image suivante ; T511, T512, T514, T515 ;
    `@3693`, `@3725`, `@3731` et `@3734` finissent (positions après `@3734` non épinglées : détour sur contact de case) ;
    G482 et `0x11 @3738`. **Contre-épreuve D-E19-37** : héros posé en (898 ; 744 ; 16) : Septimus arrêté en (57409536 ;
    51912704) au contact du héros garé (relation exacte bord contre bord), héros garé en (57094144 ; 50995200) à
    ± 2,5 px, T510 jamais posé dans la limite ; le test affirme ce calage
    reproduit et cite O-E19-24.
  - **T-C61** (61 `B[6]`, G784 posé, G670 éteint ; arrivée par le portail 61.1 en (588 ; 648)) : rouge (`@980` jamais fini,
    héros en (5373952 ; 54001664)) ; vert jusqu'à G672 : `@929` PosY 36110336 contre rec32 ; `@934` PosX 27394048 contre
    rec33 ; `@959` (17956864 ; 45547520) contre rec34 ; `@964` (17956864 ; 50855936) contre rec35 ; `@977` (11665408 ;
    50855936) ; `0x1E @980` finit en (11665408 ; 55050240) ; arrêt à G672 (la suite dépend d'O-E19-17 et d'O-E19-26).
  - **TN-3** (346, hôte dans la zone x 0 à 15, y 40 à 59, G1022 éteint) : `0x1E @943` part de (96,0 ; 752,0) ; la
    plateforme s'arrête en (96,0 ; 800,0) contre le mur rec10 et n'avance plus pendant 500 ticks ; `ForceAdjusted` posé ;
    aucun détour engagé (D-E19-34, D-E19-35).
  - **TN-1 retiré** (cortège de la 10 `B[13]`, chapitre 18) : la relecture a trouvé que la DLL tronque au pixel la racine
    d'un PNJ qui atterrit sur une nouvelle hauteur (`EvaluateEntitySupport` → `PushLogicalPositionToRoot` →
    `ResolveLogicalPosition`, `AlundraEntityScriptProxy.cs:700-735`, `:1807-1820`, `AlundraEntitySpawnFactory.cs:472-479`) ;
    sur la rampe du cortège, cela double sa vitesse et le fait caler **avec ou sans** blocage d'entités : écart
    préexistant, hors de cette tranche (O-E19-29, question à l'auteur). Aucune autre scène cible n'a de changement de
    hauteur à une position fractionnaire (185, 164, 61, `B[9]` ; `B[14]` identique avec ou sans troncature).
  - **TH4** (arrivées sans chevauchement, D-E19-36) : à la première image après l'adoption, `FindEntityCollisionCandidate`
    du héros rend null pour les arrivées de A20 (162), A10 (165), T-A19 (185) et du préréglage `day3-after-dream` (179,
    case (17,7), z 1, drapeaux du préréglage).
  - Commits : `test(alundra): pin the entity contact arcs of maps 185, 10, 61 and 346 (E19.d2b)` et
    `test(alundra): re-pin A11 under entity contacts and guard the unchanged arcs`.
  - Premier commit fait le 2026-10-02 (arcs T-A19, T-A10v, T-B9 et sa contre-épreuve, T-C61, TN-3, TH4 ; `AlundraEntityContactArcTests.cs`). **Rouge d'abord, sur la DLL d'avant B3**
    (sonde non installée, aucune règle d'entité) : T-A19 échoue dans sa limite (1700 images) en nommant `slot 2 program @452: last 0x0B @506` ; T-A10v dans la sienne (6500) en
    nommant `slot 2 program @5156: last 0x0B @5172` ; T-B9 dans la sienne (3200) en nommant `slot 1 program @1132: last 0x0B @1215` (le héros) et `slot 2 program @3636: last 0x0B @3673`
    (Septimus) ; T-C61 dans la sienne (700) en nommant `slot 1 program @872: last 0x1E @980` ; la contre-épreuve de T-B9 et TN-3 sont rouges aussi (TN-3 : la plateforme traverse le
    mur et passe à 800,5 px). Des explorations jetables du même jour (non commitées) ont mesuré, sur cette DLL, les positions de calage écrites au plan : Septimus de la 185 en
    (15073280 ; 25821184), Nestus de la 10 en (64323584 ; 23527424), hors des tests. **Vert après B3 et B4** : toutes les valeurs écrites d'avance sont égales aux mesures, sans en
    changer une (contacts en relationnel exact, fins de marche dans leurs tolérances, T10 à T70 dans l'ordre, la 362 en (16515072 ; 17301504 ; 2097152) effet 4 ; T-A10v jusqu'à
    `0x53 @2003` ; T-B9 jusqu'à G482 et `0x11 @3738` ; T-C61 jusqu'à G672 ; TN-3 : la plateforme en (96 ; 800) contre rec10, immobile 500 images, `ForceAdjusted` 1, `XCollisionEntity` rec10,
    ni chemin ni `WalkDetourAttempted` ; contre-épreuve de T-B9 : Septimus arrêté en (57409536 ; 51912704), T510 jamais posé, héros garé à ± 2,5 px de (57094144 ; 50995200)).
    Les ensembles d'opcodes sautés sont ceux de la mesure (0x25 de la 10 pour E19.d2c, 0x58, 0x90, 0x95, 0x2B, 0x4C, 0x4D, 0x29, 0x5D, 0x52, 0x3F, 0x45, 0x46). TH4 : les quatre arrivées
    (A20, A10, T-A19, `day3-after-dream`) ne recouvrent aucune entité à la première image.
  - Montages des arcs (précisions) : T-A19 comme écrit (arrivée puis `PlaceHero(132, 296, 16)`, « sol » = 16 px) ; T-A10v : héros posé par la tuile (41, 29, 3) = (996, 472, 48), que `B[14]` ré-écrit
    par `0x64` ; T-B9 : tuile (37, 46, 1) puis `PlaceHero(898, 736, 16)` avant la première image ; contre-épreuve `PlaceHero(898, 744, 16)`, bilan à l'image 1400 ; T-C61 : arrivée (588, 648)
    à z 2 ; **TN-3 : le héros « dans la zone » est une arrivée à la tuile (6, 50), z 2** (la zone de spawn est lue à l'adoption du héros : un héros placé après la construction du monde ne fait pas
    apparaître la plateforme). Support partagé : `AlundraArcSamples.cs` (instantané au premier instruction qui suit un pc, ordre des drapeaux temporaires).
  - Second commit fait le 2026-10-02 : **T-REG-0**, garde des arcs inchangés. `EntityBlockCount` est lu à la fin de chaque arc par `ArcRun.Dispose` (le `using` du test), pour les onze arcs
    nommés (A1c, A3, A4p, A5, A5r, A6, A7, A8, A9, A10, A20) : **0 sur les onze**, leurs épingles n'ont donc aucune raison de bouger, et elles n'ont pas bougé. La garde discrimine
    (A11 ajouté à l'ensemble par une mutation jetable : « 5 controller step(s) were shortened or cancelled by an entity », retirée ensuite). Déviation de forme : la garde est dans le support
    d'arcs (une ligne par nom), non une assertion ajoutée dans chacun des onze tests existants, qu'aucun n'a donc à changer.
  - **Déviation d'ordre (rappel)** : le ré-épinglage d'A11 (premier point de la seconde ligne de commit du plan) est dans le commit de B3 (voir B3) ; ce second commit ne porte que la garde.
    Le plan de B6 listait aussi « TN-1 retiré » (aucun test) et TH4 (fait, quatre arrivées).

- ✅ **B7 — Hygiène reportée d'E19.d2a.** A20 compte les onze `0xA2` parmi les sautés (`SkippedOrExceeded`) ; A10 vérifie
  le programme `@236` de `0x11 @354` ; S1 : un préréglage de test qui pose puis efface un drapeau, un compteur d'objet,
  les refus d'index ; l'outil refuse en `--dry-run` un `ProjectName` que le moteur refuserait et rend 1 sur tout refus
  (exceptions comprises) ; `docs/test-saves.md` : ne pas lancer l'outil par le terminal ou le bouton Run de l'app Claude,
  le choix de la sauvegarde que charge F9 par la date (`LastWriteTime`), les limites et préconditions de F9 ; le second
  libellé « TH3 » renommé. Commit : `chore(alundra): close the E19.d2a hygiene items`
  - Fait le 2026-10-02. A20 compte maintenant les onze `0xA2` parmi les instructions sautées (`SkippedOrExceeded`, une fois chacun à l'image 0) ; A10 vérifie que `0x11 @354` est du
    programme `@236` ; S1 : quatre nouveaux tests (pose puis effacement d'un drapeau, compteur d'objet de l'objet 5 — les règles plafonnent chaque objet, 0..1 ici —, refus des index
    de table de cartes -1 et 500, des index de compteur -1 et 256) ; le test de TH3 d'E19.d2a est renommé `TH3b` (méthode et nom d'arc). L'outil : `--dry-run` refuse un `ProjectName` que
    le moteur refuserait comme nom de dossier (copie de la règle de l'ADR-0044 du moteur, noms réservés et nom par défaut compris) et rend 1 sur toute exception ; mesuré par trois
    `--dry-run` (`alundra-project/AlundraGame.json` : sortie conforme, code 0, « dry run: nothing written » ; `bad/name` et `CON` : refusés, code 1 ; `Good Name` passe le nom puis échoue
    sur les tables absentes, code 1), rien écrit sous `%LOCALAPPDATA%` (les trois fichiers du dossier sont ceux de l'auteur, datés du matin). `docs/test-saves.md` : ne pas lancer
    l'outil depuis l'app Claude (terminal, bouton Run, agent), choix du slot de F9 par la date (`LastWriteTime`), refus et limites de F9. **Non fait** : un test automatique de
    l'outil (il faudrait référencer un projet exécutable depuis `Alundra.Tests`) ; le verrouillage du fichier projet n'a pas été exercé (le chemin d'exception est une ligne).

- ✅ **B8 — Vérification et clôture.** Builds et suites dans cet ordre : `CasaEngine.Tests` (moteur) ; build de la solution
  parent et `Alundra.Tests` en Release, puis en Debug **en dernier**, `--blame-hang-timeout 300s` ; convertisseur
  inchangé ; `cmp` de la DLL Debug déployée. Verifier frais et contradicteurs ; dispositions au plan ; §0.2, §2 et mémoire.
  - Part de l'exécutant faite le 2026-10-02 (mesures au §2, sous-section E19.d2b) : `CasaEngine.Tests` 2441 réussis, 0 échec ; solution parent et `Alundra.Tests` 2185 réussis en Release puis
    en Debug (la Debug en dernier), 0 échec ; `cmp` de la DLL Debug déployée sans écart ; convertisseur et analyseur non touchés (leurs tests ne sont pas relancés). **Reste** : le verifier frais
    et les contradicteurs, les dispositions, §0.2 et la mémoire, qui se font après l'exécutant. Faits : voir « Vérification
    d'E19.d2b » ci-dessous.

- ✅ **B9 — Recette en jeu (auteur).** Validée le 2026-10-02 (« tout fonctionne »).
  1. Jeu libre de l'intro et du jour 1 (sauvegarde devant le livre) : le héros bute sur les marins et les villageois au
     lieu de les traverser ; il leur parle en marchant contre eux puis Carré ; les portes ouvertes se passent ; aucune
     scène ne cale.
  2. F9 sur `day4-meeting` : parler aux quatre villageois de la 185 ; la réunion va au bout ; la 362 (rêve d'Olen) se
     charge.
  3. F9 sur `day3-after-dream` : sur la 179, Alundra s'arrête contre Septimus au lieu de le traverser ; la chaîne
     continue jusqu'au saut de la 10 (réglé par E19.d2c).

**Acceptation d'E19.d2b.**
1. Plan moteur fait, `CasaEngine.Tests` sans échec, aucun test moteur existant modifié.
2. Tests B2 à B4 écrits rouges d'abord, verts ; T-A19, T-A10v, T-B9 et T-C61 rouges sur la DLL d'avant B3, verts
   après, avec les valeurs écrites d'avance ; la contre-épreuve de T-B9 et TN-3 rendent le comportement fidèle annoncé.
3. A11 ré-épinglé aux valeurs écrites ; T-REG-0 à 0 sur les onze arcs ; aucune autre épingle ni trace ne bouge ; seuls
   les trois montages nommés sont réécrits.
4. `Alundra.Tests` sans échec en Release puis en Debug, la DLL Debug déployée en dernier.
5. Recette B9 faite par l'auteur.

**Risques d'E19.d2b.**
- Contacts à 0 px et marges de 1 à 2 px (179, 178, 10, 176, cortège) : une lecture flottante, un arrondi différent du
  tirage, une boîte fermée ou un skin figeraient une scène ; B3 impose la règle exacte et la même conversion.
- Chevauchement reproduit (D-E19-36) : un héros ou un PNJ placé dans une entité se fige ; 87 arrivées de portail le
  feraient en statique (drapeaux non appliqués) ; seules les arrivées de la chaîne sont vérifiées (TH4) ; les autres
  cartes, à leur premier arc.
- Ressenti du jeu libre : le héros glisse le long d'un PNJ en diagonale (le binaire reste collé) ; corrigé par la
  division conjointe d'E19.h.
- Obstacles natifs solides (D-E19-34) : cartes 14 et 15 au jour 2, 362, 346, 52 fermées là où l'original attend un objet.
- L'intro jouée avec contrôleurs réels n'a aucun arc : la recette B9 la couvre.
- Troncature au pixel d'un PNJ qui atterrit sur une nouvelle hauteur (O-E19-29) : écart préexistant de la DLL, qui fait
  caler le cortège du chapitre 18 sur sa rampe ; hors tranche, il peut toucher d'autres scènes à rampe.
- Ordre de mise à jour : la DLL déplace par entité dans l'ordre du monde (héros en tête supposé), le binaire par créneau ;
  sans effet mesuré sur les scènes, à surveiller quand deux mobiles se touchent dans le même tick.
- Détour E4.d sur contacts de case (Lutas `@219` sur la 185, Septimus `@3734` sur la 10) : non modélisé ; les positions
  qui suivent ne sont pas épinglées, la fin des scènes l'est.
- Les valeurs viennent d'émulations calées sur des mesures de la DLL : une valeur contredite est un arrêt, jamais une
  ré-épingle.

**Relecture d'E19.d2b (2026-10-02).**
- Plan-verifier frais, auditeur des valeurs et relecteur moteur, sur `34518c5` et le plan moteur `b3aa47ca` :
  **REVISE**. Bloquant : T-ENG-12 étendait des tests moteur existants (corrigé : nouveaux tests). P2 : interface
  moteur placée dans `Engine` alors qu'elle cite `Entity` (corrigé : `Framework/Physics`) ; positions rouges de T-A10v
  et T-B9 prises à l'instruction précédente (corrigées) ; TN-1 rendu faux par une troncature préexistante de la DLL
  (retiré, O-E19-29). P3 et P4 corrigés (pilotage de T-R3, tolérances des fins de marche, commentaire de T-R4, plan
  moteur). Toutes les autres valeurs ont été recalculées par l'auditeur : justes.
- Relecture neuve sur `18786f4` et le plan moteur `c372e946` : **READY** (plan-verifier) ; plan moteur sain (aucune
  remarque P0 à P3 ; huit P4 portées au plan moteur comme consignes d'exécution).

**Vérification d'E19.d2b (2026-10-02).**
- **Commits** : moteur `4e6bd6bd` (T0.1, ADR-0047), `3dc98385` (T1.1), `c57c120f` (T1.2), `d509bc10` (T1.3), `c2e4fdce`
  (T2.1) sur `chantier/field-movement-obstacles` ; parent `fdd6fb9` (B1, pointeur sur `c2e4fdce`), `f3e3a03` (B2),
  `469c6f3` (B3, avec A11 ré-épinglé), `9896578` (B4), `363db2a` (B5), `ee00ff0` et `0a68fa4` (B6), `f87ae56` (B7),
  `360fe7f` (mesures de B8).
- **Verifier frais : CONFIRMED** sur les critères 1 à 4. `CasaEngine.Tests` 2441/2441 (aucun test moteur existant
  modifié : seuls deux fichiers de test nouveaux) ; `Alundra.Tests` 2185/2185 en Release puis en Debug, la Debug en
  dernier, `cmp` sans écart. Toutes les valeurs épinglées de T-A19, T-A10v, T-B9 et sa contre-épreuve, T-C61, TN-3 et A11
  sont celles du plan. Sans la sonde (approximation de la DLL d'avant B3), 7 tests rougissent sur exactement les pc que
  le plan nomme (`@506`, `@5172`, `@1215` et `@3673`, `@980`, la plateforme de la 346 à 800,5 px, T510 posé dans la
  contre-épreuve, A11 revenu à l'image 208). Mutations attrapées : bord fermé au lieu de semi-ouvert (22 échecs), garde
  `FlagToDestroy` retirée (4), condition « aucune entité » du détour retirée (les 2 cas d'entité de T-R8), destruction
  native et copie d'`AnimFlags` retirées (5). La session principale a refait la build Debug après les relecteurs : à
  jour, `cmp` sans écart.
- **Trois contradicteurs en lecture seule** (fidélité au binaire, règles du moteur, tests et non-régression) : aucun P0
  à P2. Fidélité confirmée au désassemblage (règle, gardes, Z logique, conversion, exclusion des soulevables limitée au
  prédicat, natif E 0/1, `AnimFlags`, `XCollisionEntity`, détour) ; moteur : API additive, aucune allocation, identique au
  bit près sans sonde, contact exact ; `T-REG-0` n'est pas nul par construction (un mutant qui compte chaque pas fait
  échouer les onze arcs) ; la suite d'avant la tranche, compilée contre la DLL d'après, n'échoue que sur les trois
  montages nommés et l'ancien A11.
- **Dispositions** :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | Les arcs tournent sans grille de navigation (le banc n'a pas de chargeur de `TileSetData`) : le détour E4.d sur contact de **case**, que la production engage, n'y est jamais exercé ; les fins de T-A19 (Lutas `@219`) et de T-B9 (Septimus `@3734`) ne sont prouvées qu'en son absence. | P3 | Reporté ; **point de recette** B9 : si la réunion de la 185 cale sur Lutas après `@219`, c'est le détour sur contact de case (D-E19-6), à traiter à part. |
  | T-A19 épingle deux positions du héros après `@506` (`@120` (6946816 ; 21495808), `@125` (11862016 ; 21495808)) alors que le plan disait ne pas les épingler ; ce sont les valeurs de l'émulation de la découverte (`scenes`, reproduites par la contre-vérification), mesurées égales. | P3, introduit | Accepté et consigné ; si le détour sur contact de case ou O-E19-29 les déplace un jour, c'est un arrêt à soumettre, pas une ré-épingle. |
  | Le retrait de rec7 et de rec41 de la liste est lu deux images après la destruction (`destroyFrame + 2`), non « l'image qui suit ». | P3, introduit | Reporté (hygiène d'E19.d2c) : resserrer à l'image exacte ; la garde est discriminante (mutation attrapée). |
  | TN-3 « aucun détour » est vrai par construction dans le banc (pas de grille) ; seul T-R8 couvre D-E19-35. | P3, introduit | Accepté : T-R8 couvre la règle. |
  | La règle `H2Obstacle ?? H1Obstacle` n'a pas de test (l'inversion survit). | P3, introduit | Reporté (hygiène d'E19.d2c) : un test où deux entités raccourcissent X et Y. |
  | Docs et textes : doc XML d'`H1Curtailed` qui ne cite que le champ (moteur) ; `DrawDebug` reçoit un tiroir dont `Draw3dText` lève `NotImplementedException`, non signalé au contrat (moteur) ; doc B5 rattachée à la nouvelle surcharge, l'ancienne sans doc ; références obsolètes à la passe de contact retirée (un `cref`, deux commentaires) ; assistant `UlpUnits` inutilisé ; ventilation inexacte du rouge de B3 au §2 ; signal de fin de T-C61 par drapeau. | P4 | Reportés (hygiène d'E19.d2c pour le parent ; suite moteur pour les deux points moteur). |
  | Le miroir de `SailorThirteen` garde la passe de chevauchement retirée de la production ; `T-REG-Z` remplace les deux tests de la 389 par un équivalent synthétique (sonde installée, même marge) ; l'assertion par image de T-REG-Z en monde ne peut pas échouer (l'assertion finale tue la mutation). | P4 | Acceptés (écarts déclarés) ; le miroir sera aligné avec l'hygiène d'E19.d2c. |
  | La garde T-REG-0 lève depuis `ArcRun.Dispose` et peut masquer l'échec d'origine d'un arc. | P4, introduit | Reporté (hygiène). |
  | Empreinte de la DLL déployée différente de celle du §2 : reconstruite depuis les mêmes sources (fins de ligne). | P4 | Sans effet ; `cmp` refait par la session principale. |

#### 1.2h.3 E19.d2c — Saut, `0x25`, eau et glace ⏳ (deux sous-tranches : E19.d2c1 faite et CONFIRMED le 2026-10-03, recette en attente ; E19.d2c2 détaillée ci-dessous)

**Résultat** : la chaîne du jour 3 passe le saut de la 10 jusqu'à la 135 ; toute entité saute au tick du changement
d'animation, comme dans l'original ; `0x25` attend l'atterrissage ; le héros ralentit dans l'eau sans bottes et glisse sur
la glace ; il saute à la Croix, tombe des rebords et se pose sur les objets comme dans l'original (D-E19-39, D-E19-42,
D-E19-43) ; le son 10 accompagne son décollage (D-E19-44).

**Conduite** : consigne de l'auteur du 2026-10-02 au soir, « Fait tout E12 et E19 » : mode AUTO. Travail réversible dans
le périmètre de ce plan ; ni merge, ni push, ni action irréversible ou externe ; une question de produit que les décisions
D-E19-1 à D-E19-44 et les règles de l'auteur ne tranchent pas met la sous-tranche en pause et va aux points ouverts (§3) ;
les recettes en jeu attendent l'auteur. Les deux sous-tranches se mergent ensemble (D-E19-39 : une seule tranche pour
tout le saut du héros) ; le découpage sert la relecture et la vérification.

**Découverte** (2026-10-02, lecture seule, cinq surfaces : conception, valeurs, non-régression, saut du binaire, saut dans
la DLL ; chacune contre-vérifiée par des modèles et des émulateurs MIPS indépendants qui exécutent le vrai code de
`ALUN_CD.EXE` ; notes et scripts dans le scratchpad de la session, `e19d2c-disc/`). **Les valeurs écrites d'avance sont
dans l'annexe `docs/plan-e19-d2c-valeurs.md`** (recopie sans retouche des tests proposés, puis des corrections des
contre-vérifications, qui l'emportent ; là où le plan écrit une autre valeur avec sa raison, le plan l'emporte, par
exemple `CollidedWithEntityZ` 1 à la mise à jour 23 d'UJ-3 par R5 d). Faits porteurs :
- **Impulsion** **[binaire]** : `UpdateAnimation` (`0x80038AB4`) efface `+0xF8` à chaque appel (`0x80038AE4`) et y écrit
  `IZF = lh(animSet+0xA)` dans son bloc de changement (`0x80038B5C`-`0x80038B64`) : nouvelle cible, nouvelle ligne de
  direction (`0x80038B08`/`0x80038B10`) ou fin de chaîne (`0x80038D54`-`0x80038D68`, qui saute dans le bloc) ; un tour de
  boucle (`0x80038D70`) n'y passe pas. `InitializeEntity` (`0x80039D04`) fait lui-même le premier changement d'une entité
  qui apparaît ou du héros qui arrive (`0x80031974`), et l'`UpdateAnimation` suivant efface `+0xF8` avant la physique :
  **jamais d'impulsion à l'apparition ni à l'arrivée**. Le relancement `0x1C` (`0x8003D830`-`0x8003D840` : `Current =
  ~Target`) est un changement ordinaire : **il donne l'impulsion**.
- **Forces** **[binaire]** : héros (`0x80036884`-`0x80036948`, reconnu par son adresse) : `IZF != 0` → `ForceZ = IZF * 160`
  si Gravity, `VramOR & 0x10` et niveau de bottes ≤ 0, sinon `IZF << 8`, sans décroissance ce tick ; `IZF == 0` et Gravity
  → `ForceZ -= Gravity << 8` puis borne à `±(ZViscosity << 8)` des deux côtés ; sans Gravity, inchangé. PNJ
  (`0x80036AB8`-`0x80036B60`) : même chose sans `x160`, et `(IZF & 0xFFFF) == 0x8000` sans Gravity → `ForceZ = 0` (marqueur
  d'arrêt des grilles de fer : 148 enregistrements sur 58 cartes) ; la DLL ne borne qu'un côté
  (`AlundraEntityScriptProxy.cs:562-574`), sans effet tant que `IZF` ≤ 2048.
- **Physique** **[binaire]** : la passe efface `+0x140` et `+0x13C` de toute entité active en tête de tick
  (`0x800383B4`/`0x800383B8`) ; Z avant XY (`0x80037E34`) ; atterrissage terrain **strict** `PosZ + F < T` en convention
  de la DLL (`0x80036C20`-`0x80036C34`, avec le `+1` du binaire) : `PosZ = T`, `+0x140 = 1` (`0x800376E0`), `ForceZ = 0` si
  Gravity (`0x80037700`) ; au repos avec gravité, l'entité atterrit à chaque tick (`+0x140` vaut 1 en permanence au sol) ;
  `IsOnGround = !(FloorHeight < PosZ)` en fin de passe (`0x800380F8`-`0x8003810C`), soit `PosZ <= T` sans le `+1`.
- **`0x25`** (`0x8003DB7C`) : rend 1 si `+0x140` ou `+0x144` de l'**entité logique**, sans effet de bord. La DLL n'a
  aucun `case 0x25` (taille 1, ignoré, `EventOpcodeSizeTable.cs:74`).
- **Eau, glace, `x160`** **[binaire]** : héros seul, copies locales avant `IncrementForce` (`0x80036954`-`0x80036A50`) :
  glace (`VramOR & 0x20`) → pas `= (pas * 0x1000) >> 16` ; eau (`VramOR & 0x08` et bottes ≤ 0) → cible
  `= (cible * 0x8000) >> 16` signé (arrondi vers le bas) ; caches inchangés. `VramOR` vaut 0 en l'air. Niveau de bottes
  3/2/1 si l'objet `0x1C`/`0x1B`/`0x1A` est possédé, lu par `GetNumberOfItem` (`NumberOfItems[id * 2 + 1]`).
- **DLL d'aujourd'hui** : `StepAnimationClock` (`AlundraFrameSyncPasses.cs:189-215`) retourne tôt sans horloge de sprite ;
  le tick dû n'est posé qu'avec une horloge ; le projet tourne en pas de temps variable (une image sur six sans tick à
  60 Hz, deux sur trois à 144 Hz, `AlundraLogicClock`) : une cible écrite par un événement de carte peut être validée par
  `SyncAnimation` sur une image sans tick. Trois sites écrivent `Current = ~cible` : apparition
  (`AlundraEntitySpawnFactory.cs:672-673`), adoption du héros (`AlundraWorldProxy.cs:1698-1699`), relancement `0x1C`
  (`AlundraEventProgramRunner.cs:2206`) ; un `0x53` copie l'animation courante du héros comme animation d'arrivée
  (`AlundraWarpDirector.cs:408-411`). `PushLogicalPositionToRoot` tronque X, Y et Z au pixel et son `Teleport` remet le
  verrou vertical à zéro (`AlundraEntitySpawnFactory.cs:472-479`, `CharacterControllerComponent.cs:419-435`, `:608-613`).
  Le gel redéclare le verrou par `AlundraGameplayFreeze.OwnerExternalVerticalDisplacement` (`:122-130`), qui ne connaît
  que l'escalade. `ResyncControllerFromFlags` met la gravité moteur à 0 pour toute entité à contrôleur, héros compris
  (`0x16`, `0x17`, `0x62`, `0x63`). `VramOR` du héros n'est calculé qu'une fois par image (`:1166`). Le moteur ignore la
  vitesse verticale résiduelle d'un contrôleur dont la verticale est externe (`CharacterControllerComponent.cs:269-275`,
  `:787-793`).
- **Chaîne** **[données]** : sur toute la chaîne du jour 3 et du jour 4, quatre écritures d'animation à impulsion : 179
  `B[2] @411` et `C[8] @1110` (rec8), 10 `B[20] @2451` (héros) et `C[74] @6389` (Giles) ; `0x25` utiles : 179 `@415`,
  `@1114`, 10 `@2455`, `@6393`. Sur la 478, seuls les chiens rec3 et rec4 (`C[3] @329`, `C[4] @345`, animation 12, IZF
  768, non collisionnables) ; sur la 165, Bergus (`@834`, `@839`, `@861`, `@1162`) ; au jour 4, sur la 362 (rêve d'Olen), la bombe de `C[5] @635`
(`1A [2]`, IZF 1024). La règle d'eau ralentit 18 ticks de
  la marche `@2447` de la 10 (+9 ticks).
- **Saut à la manette** **[binaire, exécuté]** : voir le premier point du contexte de l'ADR-0023 ; tables S-A à S-L et UH
  de l'annexe (B.1), tirées du vrai `MovePlayer` (1536 combinaisons d'entrées, aucun écart entre deux interpréteurs
  indépendants) et de la vraie passe physique. Chute = même code (`0x80031F44`) ; dessus d'entité = même arc qu'une marche
  de terrain de même hauteur ; au repos sur un objet `+0x140` vaut 0 (porté, pas ré-atterri) ; `RidingEntity` est remis à
  0 à chaque tick (`0x80038998`) puis reposé (`0x800364C8`).
- **Son** **[données]** : le son 10 est dans la banque système exportée (`Sounds/sfx-manifest.json`, id 10, vab -1,
  `max_voices` 2, `sfx_0010.wav`), même espace d'identifiants que `PlaySoundEffect` (`0x800490FC`), déjà joué par la DLL
  pour le son 4 de l'inventaire. Les animations à impulsion du héros portent un son : 2, 6, `0x2B`, `0x2E` → 10 ; 62 → 28.

**Périmètre d'E19.d2c** : DLL `Alundra/`, `Alundra.Tests/`, docs du parent ; en E19.d2c2, les deux traces d'or « spawn »
du héros. **Aucun changement moteur** (verticale externe, sentinelle, `Move` et sonde d'obstacles existent).
**Hors périmètre** : plafonds et aimantation au sommet (E19.h, O-E19-27) ; état en l'air des PNJ et leur `0x25` exact
(E19.h, D-E19-40) ; troncature au pixel de l'atterrissage des PNJ (O-E19-29, plus tard selon l'auteur) ; branche
`LoadingMap` (`0x36`) → `0x2D` (O-E19-32) ; physique des cartes sous-marines 159 et 160 (O-E19-31) ; nage (pente 4),
transport, course, attaque en l'air, saisie et coups (E14) ; sons des autres changements d'animation (E19.h).

| Sous-tranche | Résultat | Périmètre | Acceptation | Dépend de |
|---|---|---|---|---|
| **E19.d2c1** — Saut scripté, `0x25`, eau et glace | Le saut de la 10 et les sauts de Bergus vont au bout ; aucune entité ne décolle à l'apparition ; eau et glace du héros ; son du décollage | impulsion au tick, exclusion d'apparition, impulsion des PNJ et règle `0x8000`, `0x25`, `CollidedWithEntityZ`, état en l'air du héros pour les sauts scriptés et `0x1B`, eau, glace, `x160`, bottes, `VramOR` par tick, son | §1.2h.3.1 | E19.d2b |
| **E19.d2c2** — Saut à la manette, chutes, dessus d'objets | Le héros saute à la Croix, tombe des rebords et se pose sur les objets comme dans l'original | états de saut de `MovePlayer`, front de Croix par tick, chute dans l'état en l'air, dessus d'entités et règle du passager, traces « spawn » | §1.2h.3.2 | E19.d2c1 |

##### 1.2h.3.1 E19.d2c1 — Saut scripté, `0x25`, eau et glace ✅ (C0 à C6 faites et CONFIRMED le 2026-10-03 ; recette C7 en attente)

**Règles d'exécution** (contrat ; les noms de champs sont indicatifs, l'exécutant garde ceux du code).

- **R1 — Impulsion au tick** (`AlundraFrameSyncPasses.StepAnimationClock`) :
  - tout en haut, **avant tout retour anticipé** (sans sprite, sans horloge, `FlagToDestroy`) : `IsZForceApplied = 0` ;
  - un changement en attente (le même prédicat que l'horloge : `PendingChainRestartFlag`, ou `TryResolveAnimationTarget`
    qui changerait l'animation ou la ligne de direction) dont l'impulsion n'est pas encore prise : `IsZForceApplied =
    IZF(animation cible)`, verrou « impulsion prise » levé ; sauf R2. Vaut aussi pour une entité **sans horloge ni
    sprite** ;
  - `SyncAnimation`, quand il valide le changement : si le verrou est levé, il le baisse ; s'il ne l'est pas (validation
    sur une image sans tick), il pose une **impulsion due**, que le premier `StepAnimationClock` suivant prend
    (`IZF(CurrentAnimationId)`) avant toute autre chose ;
  - une fin de chaîne levée par `AdvanceLogicalTicks` dans ce tick donne l'impulsion de l'animation chaînée **à ce
    tick** ; un tour de boucle n'en donne pas ;
  - jamais deux impulsions pour un même changement, même sous rattrapage (plusieurs ticks dans l'image).
- **R2 — Apparition et arrivée** : un drapeau d'apparition, posé avec l'animation d'apparition par
  `ApplySpawnInitialization` (chargement, `0x2D`, `0x8A`, `0x8B`) et par `AdoptPlayerPawn` (toutes les arrivées du héros :
  défaut `0x36`, F9, `0x53` qui copie l'animation courante), copié par `Clone`. Le changement en attente dont la cible est
  l'animation d'apparition ne donne pas d'impulsion ; le drapeau tombe à la première validation de `SyncAnimation`. Une
  autre animation écrite par un script avant cette validation donne l'impulsion (le binaire : changement ordinaire après
  `InitializeEntity`). Le relancement `0x1C` n'est jamais exclu.
- **R3 — Impulsion des PNJ** (`EvaluateEntitySupport`, au point de la décroissance, `AlundraEntityScriptProxy.cs:561-573`,
  même porte `Controller != null && !immediateAtSpawn` mais **sans** la condition Gravity) : `IsZForceApplied != 0` →
  `ForceZ = FinalForceZ = ((IZF & 0xFFFF) == 0x8000 && !Gravity) ? 0 : IZF << 8`, sans décroissance ce tick ; sinon, avec
  Gravity, décroissance puis borne **des deux côtés** à `±(MapZViscosityRaw << 8)` (binaire). La remise à zéro de
  `:676-680` (`Controller.IsGrounded && Gravity && ForceZ < 0` → `ForceZ = FinalForceZ = 0`, correctif `a750256` de
  l'escalier du marin 12) est **gardée telle quelle** : aucune épingle existante ne bouge par elle. Le test
  d'atterrissage des PNJ garde `<=` (D-E19-40 ; écart d'un tick sur `ForceZ` pour les impulsions multiples de la gravité,
  épinglé par UJ-1b).
- **R4 — `0x25`** : `case 0x25` dans `Dispatch`, sur l'entité logique, taille 1 : continue si `CollidedWithEntityZ != 0`
  ou `IsOnGround != 0`, sinon attend (même forme que `0x24`) ; libellé de `EventOpcodeSizeTable.cs:74` corrigé (taille
  inchangée) ; `0x25` ajouté à `IntroTraceHarnessTests.ImplementedOpcodes` (étiquette de trace seulement).
- **R5 — `CollidedWithEntityZ`** : effacé en tête du tick de mouvement de toute entité (même point que `ForceAdjusted`,
  `AlundraScriptedMotion.cs:174`) ; posé à 1 :
  - (a) à l'atterrissage terrain d'un PNJ (branche `<=` de `:693`), **seulement** si le test strict du binaire tient sur la
    **force du tick** : `moddedPosZ + F < terrainHeight`, avec `F` la valeur de `FinalForceZ` juste après R3 (impulsion
    ou décroissance), **relevée avant la remise à zéro de `:676-680`** ; l'écriture à 0 de `:733` disparaît. Traces :
    PNJ au repos avec gravité, `F` = -32768 : 1 à chaque tick (UJ-4) ; UJ-1, mise à jour 23 (aimanté à la tête de
    l'image, `PosZ` 0, `F` = -372736) : 1 ; UJ-1b, mise à jour 21 (`PosZ` 327680, `F` = -327680, somme 0, non strict) :
    0, puis mise à jour 22 (`F` = -32768) : 1 ; PNJ sans gravité au repos (`F` = 0) : 0 ;
  - (b) à l'appui sur une entité (`:640`, inchangé ; le binaire pose 0 tant qu'une entité est portée : écart sans effet
    sur `0x25`, consigné pour E19.h) ;
  - (c) à l'atterrissage du héros (R6) ;
  - (d) pour le héros hors de l'état en l'air, avec Gravity et `IsOnGround == 1`, à chaque tick (son repos atterrit à
    chaque tick dans le binaire).
- **R6 — Héros en l'air, tenu par le tick** (sauts scriptés et `0x1B` ; la chute vient en E19.d2c2) :
  - **entrée**, dans le tick du héros : impulsion prise (R1) ; ou marque posée par `0x1B` quand l'entité logique est le
    héros (le `ForceZ` écrit par l'opcode décroît dès ce tick) ;
  - **place** : dans le tick de mouvement du héros, **après** l'effacement de `ForceAdjusted` et de
    `CollidedWithEntityZ` (`AlundraScriptedMotion.cs:174`) et **avant** le pas XY (`RunOneKinematicTick`) ; R5 d au
    même endroit, hors de l'état ;
  - **étape verticale avant le pas XY** : tick d'impulsion : `ForceZ = IZF * 160` si Gravity, `VramOR & 0x10` et bottes
    ≤ 0, sinon `IZF << 8`, sans décroissance ; sinon, avec Gravity, décroissance et borne des deux côtés. `F > 0` :
    montée (pas de plafond, E19.h). Sinon test **strict** `PosZ + F < T` (`T` : hauteur du terrain sous la boîte, le
    plus haut des quatre coins, à la position d'avant le pas XY ; les dessus d'entités viennent en E19.d2c2) : atterri →
    `PosZ = T`, `ForceZ = 0` si Gravity, `CollidedWithEntityZ = 1`, sortie de l'état ; sinon `PosZ += F` ;
  - **position** : `PosZ` logique est la vérité, posé exactement ; la racine suit par un déplacement vertical du
    contrôleur, **jamais** `PushLogicalPositionToRoot` ni `Teleport` ; `PosX` et `PosY` gardent leur fraction ;
  - **moteur** : à l'entrée, capture des valeurs vivantes (`Settings.Gravity`, `MaxFallSpeed`,
    `IsVerticalOwnedExternally`), puis `Gravity = 0`, `MaxFallSpeed = 0`, verticale externe, sentinelle positive à chaque
    tick ; à l'atterrissage, restitution **des valeurs capturées** (y compris une gravité nulle posée par un `0x17`
    antérieur) ;
  - **tirages** : pendant l'état, ni le tirage de tête d'image (`AlundraEntityScriptProxy.cs:1001-1012`) ni celui de
    `MoveControllerAndPullPosition` (`:1900-1903`) ne réécrivent `PosZ`, et `IsOnGround` ne vient pas du moteur (`PosX`,
    `PosY` si) ; après chaque tick de l'état, `IsOnGround = PosZ <= T'` (`T'` au XY d'après
    le pas). Hors de l'état, rien ne change ;
  - **gel** : `OwnerExternalVerticalDisplacement` rend la sentinelle pendant l'état ; **adoption** d'un nouveau pion :
    état remis à faux.
- **R7 — Eau, glace, bottes** (héros seul) : dans `RunOneKinematicTick`, sur des copies locales de la cible et du pas,
  règles du binaire ; niveau de bottes lu seulement quand `VramOR & 0x08` ou `x160` le demandent, sans exception si
  `ScriptHost` est nul (niveau 0) ; `UpdateVramFlags` appelé après chaque `Tick(this, 1)` de la boucle du héros, l'appel
  de fin d'image gardé (portails, images sans tick).
- **R8 — Son du décollage** : au tick où le héros prend une impulsion (R1 ; pas la marque `0x1B`), la DLL joue le `Sfx` de
  l'animation (10 pour 2, 6, `0x2B`, `0x2E` ; 28 pour 62) par le lecteur de sons du monde, une fois par impulsion ; jamais
  à l'apparition ni à l'arrivée. Le lecteur s'atteint par un membre par défaut de `IAlundraScriptHost` qui rend `null`
  (comme `Portals`, `IAlundraScriptHost.cs:85`, pour ne toucher aucun autre hôte), que `AlundraWorldProxy` implémente
  avec son lecteur (`IEntityWorldContext.SoundPlayer`, `AlundraWorldProxy.cs:460`).

**Tâches.**

- ✅ **C0 — Plan, annexe, ADR ; mesure de base.** Ce plan, l'annexe des valeurs et l'ADR-0023 (commit de docs). Puis,
  avant toute édition de code : `Alundra.Tests` complet à la tête de la branche (attendu 2185 réussis, 0 échec,
  `--blame-hang-timeout 300s`) ; l'arc A12 (ci-dessous, C1) se monte d'abord **sur la DLL d'avant C1**, `0x25 @415`
  sauté, pour mesurer sa base (images de `@411`, `@416`, `0x53 @451`, ensemble des sautés). Garde d'octets à la fin de
  chaque tâche : `git diff --exit-code` sur les six traces (`docs/hero-trace-389-*.txt`, `docs/intro-trace-389.txt`,
  `docs/intro-programs-389.txt`).

- ✅ **C1 — `0x25` et `CollidedWithEntityZ` (R4, R5), tests d'abord.**
  - **UJ-2** (montage de `AlundraEventProgramRunnerWaitForceAdjustedTests` : programme `01 25 FF`) : propriétaire = logique,
    `(CollidedWithEntityZ, IsOnGround)` = (0,0) : l'appel 1 trace `(0,0x01,1)`, `(1,0x25,0)`, `CodeIndex` 1 ; puis (0,1) :
    `(1,0x25,1)`, fin, `CodeIndex` 2 ; idem (1,0) et (1,1) ; lit l'entité logique (propriétaire (1,1) et logique (0,0) :
    attend ; propriétaire (0,0) et logique (0,1) : continue) ; aucun effet de bord en 10 appels (`Parameters[1..3]`,
    `TargetAnimationId`, `TargetDirection`). Rouge aujourd'hui : continue d'emblée.
  - **UJ-4** (montage d'UJ-1, sans impulsion) : PNJ au repos avec gravité : 1 après chaque mise à jour ; sans gravité
    (`F == 0`) : 0 et `IsOnGround` 1 ; soutenu à n puis en l'air à n+1 : 0 à n+1 ; héros à contrôleur au repos, hors de
    l'état en l'air : 1 après chaque mise à jour (montage d'UJ-3, hôte qui expose un `PlayerController`, sinon le tick du
    héros ne tourne pas, `AlundraEntityScriptProxy.cs:1140-1143`).
  - **A10** (165) : les deux comptes « sauté une fois » (`AlundraInoaDayOneArcTests.cs:179-180`) deviennent « jamais
    sauté », et chaque `0x25` s'exécute une fois et rend 1 ; l'ensemble des sautés attendu devient vide ; **toutes les
    autres épingles inchangées**, `0x11 @354` toujours à 922 (sans impulsion, Bergus reste au sol et `0x25` rend 1 à son
    premier appel). Doc du test mise à jour.
  - **T-A10v** (10) : `(0x25,4741)` et `(0x25,5545)` quittent l'ensemble des sautés ; **TR-V** : les bouquets rec69 et
    rec81 (sans gravité) descendent de 32768 unités par tick depuis le lâcher (`1B [128,255]`), leur `0x25` rend 1 entre
    t+8 et t+17 (t = tick du lâcher ; t+8 à t+9 par l'aimantation de 4 px du moteur, t+16 à t+17 par l'atterrissage de la
    DLL et le test strict de R5), puis le `1B [0,0]` qui suit met `ForceZ` à 0 et le bouquet reste à sa hauteur de
    repos ; aucun bouquet en l'air à la fin ; toutes les assertions existantes de T-A10v (`AlundraEntityContactArcTests.cs:150-203`)
    restent vertes.
  - **A12** (179 `B[2]`, nouvel arc, même départ que l'arc TH4 du préréglage `day3-after-dream`
    (`AlundraEntityContactArcTests.cs:389-391` : « Inoa (inner)-179 », G203, G1651, G1660, arrivée en (17,7) z1), limite
    d'images assez large pour atteindre `0x53 @451`, boîtes fermées par un appui par image) : sur la DLL de C1, `0x25 @415` rend 1 à son premier appel ;
    les images de `@411`, `@416` et `@451` sont celles de la base de C0 ; ensemble des sautés = celui de la base moins
    `(0x25,415)`.
  - Commit : `feat(alundra): port opcode 0x25 and clear CollidedWithEntityZ every tick like the binary`
  - Fait le 2026-10-03 : rouges d'abord sur le code d'avant C1 (production remisée) : UJ-2 (3 tests : continue d'emblée), UJ-4 (repos avec gravité,
    plateforme puis air à n+1, héros au repos : 1 attendu, 0 rendu ; les cas sans gravité et en l'air passent déjà, ce sont des gardes), A10
    (`0x25 @838` et `@843` encore sautés), T-A10v et A12 (`0x25` sautés) ; verts après : 2193 puis 2194 tests, aucun test existant hors liste
    touché. Montage UJ-4 : `JumpNpcRig` et `FlatCells` (`AlundraJumpTestSupport.cs`), PNJ de l'annexe A.1. Base d'A12 mesurée sur la DLL d'avant C1 :
    F = 287, `37 [3] @413` rend à F+4 (291), `0x25 @415` sauté à 291, `1A [0] @416` à 291, `0x53 @451` à 359, sautés = {(0x25, 415)} seul ; sur
    la DLL de C1 : mêmes images, `0x25 @415` exécuté une fois à 291, rien de sauté. TR-V : lâcher à t = 187 (rec69), `1B [128,255]` puis -32768 par
    tick, `0x25` rend à t+8 (aimantation de 4 px du moteur : 52 px puis 48 px), `1B [0,0]` dans le même appel, repos à 48 px, rien en l'air à la fin.
    Mutation jetable de R5 a (`FinalForceZ` à la place de la force du tick relevée avant la remise à zéro) : UJ-4 repos avec gravité rouge. Écart :
    le test d'A12 vit dans `AlundraBergusJumpArcTests.cs` (nouveau fichier), non dans `AlundraEntityContactArcTests`.

- ✅ **C2 — Impulsion au tick, apparition, PNJ, règle `0x8000` (R1, R2, R3), tests d'abord.** Montage de PNJ des tests
  (annexe A.1 et C.valeurs) : `ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null)`, `ContactHost`,
  `ContactWorld.AddEntity(..., 200, 100, 0, -10, -7, 0, 20, 14, 32)`, `Flags |= Gravity` **après** `AddEntity`,
  `MapGravityRaw` 128, `MapZViscosityRaw` 4096, animations {0 : vitesse 0 ; 3 : vitesse 0, IZF 1360}, une entité par
  monde (l'hôte ferme sa mémo d'horloge à chaque appel). Ce montage n'a pas de sprite : les cas qui exigent une horloge
  d'animation (fin de chaîne, tick dû) prennent celui d'`AlundraAnimationClockDriveTests` (hôte, `Drive`, `Animation2d`).
  - **UJ-1** (cible 3 écrite avant la mise à jour 1) : `PosZ` aux mises à jour 1 à 22 = 348160, 663552, 946176, 1196032,
    1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400, 1462272,
    1253376, 1011712, 737280, 430080, 90112 ; mise à jour 23 : `PosZ` 0, `ForceZ` 0, `IsOnGround` 1,
    `CollidedWithEntityZ` 1 ; `IsOnGround` et `CollidedWithEntityZ` 0 aux mises à jour 1 à 22 ; `IsZForceApplied` 1360
    après la 1, 0 après la 2. Rouge aujourd'hui : `PosZ` reste 0.
  - **UJ-1b** (IZF 1280) : `PosZ` 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240,
    1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112, 884736, 622592, 327680 ; mise à jour 21 : `PosZ` 0,
    atterri par le `<=` de la DLL, `ForceZ` 0, `IsOnGround` 1, **`CollidedWithEntityZ` 0** ; mise à jour 22 :
    `CollidedWithEntityZ` 1. Commentaire : le binaire a `ForceZ` -327680 à la 21 et atterrit à la 22 (D-E19-40).
  - **UJ-1c** (IZF 1280, PNJ à vitesse horizontale, `PosX` non entier au décollage) : à l'atterrissage (mise à jour 21),
    `PosX` est tronqué au pixel par `PushLogicalPositionToRoot` : écart **épinglé et commenté** (O-E19-29, le binaire
    garde la fraction) ; un futur correctif d'O-E19-29 le fera basculer exprès.
  - **UJ-5** (rattrapage : UJ-1 avec `Update(0.04f)`, deux ticks par appel) : `PosZ` après les appels 1, 2, 3 = 663552,
    1196032, 1597440 ; `IsZForceApplied` 0 après l'appel 1 (une seule impulsion ; une impulsion rejouée donnerait 696320).
  - **UJ-6** (apparition par le chemin de production, préfabs réels) : flèche `eab8d775` (animation 0, IZF 256, sans
    gravité) : `PosZ` = hauteur d'apparition, `ForceZ` 0, `IsZForceApplied` 0 à chacun des 10 premiers ticks ; boule de
    feu `67b30f8f` (IZF 512, gravité) : `PosZ` constant ; grille `396c008e` (IZF -32768, sans gravité) : `ForceZ` 0 à
    chaque tick. **UJ-6b** : apparition puis `1A [3]` écrit par le programme de l'entité à son premier tick :
    l'impulsion part (`PosZ` 348160 au premier tick).
  - **UJ-7** (arrivée du héros par la vraie voie d'un `0x53` lancé pendant l'animation 2, IZF 1280) : aucune impulsion,
    `PosZ` inchangé pendant 5 ticks, aucun son.
  - **UJ-8** (image sans tick : `Update(0.001f)` entre l'écriture de la cible et la première image à tick) :
    `PosZ` 348160 après le premier tick, sans sprite (montage d'UJ-1) et avec horloge de sprite (montage
    d'`AlundraAnimationClockDriveTests`). **UJ-9** (montage d'`AlundraAnimationClockDriveTests` : animation A, IZF 0,
    chaîne vers B, IZF 1360) : impulsion au tick de la fin de A, puis la liste d'UJ-1. **UJ-10** : un relancement `0x1C` d'une
    animation à IZF 1360 redonne l'impulsion (liste d'UJ-1). **UJ-DIR** : en animation 3 (IZF 1360), un changement de
    ligne de direction redonne l'impulsion.
  - **UJ-0x8000** (montage d'UJ-1, IZF stocké en entier signé -32768, la règle compare les 16 bits bas) : PNJ **sans**
    gravité, cible 3 (IZF 256) : `ForceZ` 65536 et `PosZ` +65536 à chaque tick (pas de décroissance) ; au tick n, cible 0
    (IZF -32768) écrite : `ForceZ` 0 et `PosZ` constant à n et après (sans la règle : `ForceZ` -8388608 et l'entité
    tombe au sol) ; PNJ **avec** gravité, au repos au sol, cible 0 écrite : au tick du changement l'impulsion
    -8388608 atterrit aussitôt (`PosZ` 0, `ForceZ` 0, `CollidedWithEntityZ` 1 par R5 a) : la règle ne joue pas avec
    gravité, comme le binaire.
  - **UJ-CLAMP** : PNJ avec gravité, sans impulsion, `ForceZ` 1966080 posé avant le tick : 1048576 après le tick (la DLL
    d'aujourd'hui donne 1933312).
  - **A10** (165) ré-épinglé : `0x25 @838` premier appel à l'image 484, rend à 503 (20 exécutions) ; `@839` et `@841` à
    503 ; `0x25 @843` premier appel 506, rend à 525 (20 exécutions) ; `@844`, `@846`, `0x06 @847` à 525 ; arêtes : T104
    effacé 526 ; T102 posé 527, effacé 619 ; T105 posé 620, effacé 775 ; **fin `0x11 @354` 960 ± 3** ; `PosZ` de Bergus
    au-dessus de son repos aux images 481 à 502 puis 503 à 524 : la liste d'UJ-1 jusqu'à 90112 (deux fois), repos à 525 ;
    ordre T101, T102, T103, T104, T102, T105 et effaceurs inchangés ; ensemble des sautés vide ; limite d'images 1100.
  - **A12** (179) : F = image de `1A [2] @411` : `37 [3] @413` rend à F+4 ; `0x25 @415` s'exécute de F+4 à F+23
    (20 exécutions) et rend à F+23 ; `1A [0] @416` à F+23 ; `0x53 @451` à la base + 19 ; `PosZ` de rec8 au-dessus de son
    repos : la liste d'UJ-1 de F+1 à F+22, repos à F+23.
  - **T-C61** (61) : la grille rec9 (`396c008e`, apparue au chargement) garde `IsZForceApplied` 0, `ForceZ` 0 et son
    `PosZ` à chaque image de l'arc.
  - **A3** (478) : **inchangé**, toutes ses épingles comprises (dont le bloc rec0, `ForceZ` 24576 et `PosZ` aux
    T20-T60, qui passe par la fonction restructurée) ; les chiens rec3 et rec4 sautent une fois (non épinglés).
  - Commit : `feat(alundra): give the animation impulse on the switch tick, never at spawn, like the binary`
  - Fait le 2026-10-03. Rouges d'abord sur la DLL de C1 : UJ-1, 1b, 5, 8 (deux montages), 9, 10, DIR, 0x8000, CLAMP et A12
    (11 tests) ; `AlundraAnimationImpulseSpawnTests` (UJ-6 sur les préfabs réels `eab8d775`, `67b30f8f`, `396c008e` par
    `CreateEntityFromRecord`, UJ-6b, UJ-7) ne compile pas sur l'ancien code (champs nouveaux). Vert après : `Alundra.Tests`
    2210 réussis, 0 échec ; A3, T-A10v et les traces inchangés (garde d'octets 0). Mutations jetables, une par règle :
    verrou (UJ-1, 1b, 1c, 5, 9, 10, DIR rouges), borne à deux côtés (CLAMP), règle `0x8000` (0x8000), impulsion due (UJ-8,
    deux montages), fin de chaîne (UJ-9), exemption d'apparition (UJ-6 flèche et boule de feu, UJ-7). Toutes les valeurs
    d'UJ et d'A12 tiennent telles qu'écrites ; A10 : `0x25 @838` 484 à 503, `@843` 506 à 525, hauteurs de Bergus, fin 960.
  - **Arrêt et disposition (session principale)** : deux arêtes d'A10 contredisaient le plan, T102 effacé **620** (écrit
    619) et T105 posé **621** (écrit 620). Cause établie : les valeurs du plan ajoutaient +38 à la base sans la phase de
    Wendell (rec0, programme `@740`), qui n'interroge T102 qu'une image sur trois (`37 [1] @747` attend deux images, `0x00
    @759` termine l'appel, `0x02 @760` relance) : il le lit aux images multiples de 3, 489 dans la base, 528 ici (T102 posé
    à 527), et son dialogue `@764` dure les mêmes 92 images. Erreur de dérivation du plan, non du portage : les deux
    assertions passent à 620 et 621, avec cette explication dans le test.
  - Écarts acceptés : UJ-7 exécute le vrai `0x53` sur le héros en animation 2, puis repose l'enregistrement d'arrivée sur
    le directeur de transition et appelle `AdoptPlayerPawn` (réflexion), parce qu'`ArcRun` refuse une arrivée autre que
    `0x36` ; UJ-6 prend sa hauteur de référence après le premier tick (le `+1` d'apparition est avalé par le premier
    aller-retour de la racine) ; UJ-0x8000 pose le marqueur au tick 9 (8 px : à 4 px ou moins l'aimantation du moteur
    masquerait la règle) ; UJ-1c en relationnel (`PosZ` 327680 à la 20, X entier au pixel à l'atterrissage de la 21,
    O-E19-29). **Non couvert** : l'effacement du drapeau d'apparition par le relancement `0x1C` (aucun site réel ; une
    mutation qui le retire reste verte) : reporté à E19.m.

- ✅ **C3 — Héros en l'air pour les sauts scriptés et `0x1B`, son (R6, R8), tests d'abord.** Montage héros (annexe A.1) :
  `AlundraLadderClimbTests` (contrôleur de joueur, `PlayerControlFlags = ControlLocked`), `HeroWorldFixture.BuildWorld`
  (il prend un `AlundraCellsCollisionField` : `TileMapData` synthétique à `AlundraCells` de hauteur 0, modèle
  `AlundraCellStoreTests.cs:207`) et `BuildHeroPawn` (`LoadHeroControllerSettings`), `MapGravity` 1250, `MapMaxFallSpeed`
  800, `MapGravityRaw` 128, `MapZViscosityRaw` 4096 posés comme `AdoptPlayerPawn`, et **`Flags |= EntityFlags.Gravity`**
  (`BuildHeroPawn` laisse `Flags` à 0 ; sous `ControlLocked`, `MovePlayer` sort avant de poser le bit,
  `AlundraPlayerManager.cs:196-202` ; l'en-tête réel du héros l'a).
  - **UJ-3** (animation 43 : vitesse 0, accélération 1, IZF 1280) : `PosZ` aux mises à jour 1 à 20 = liste d'UJ-1b ;
    mise à jour 21 : `PosZ` 0, pas atterri (test strict), `IsOnGround` 1, `ForceZ` -327680, `CollidedWithEntityZ` 0 ;
    mise à jour 22 : atterri, `ForceZ` 0, `CollidedWithEntityZ` 1 ; mises à jour 1 à 21 : verticale externe vraie,
    `Settings.Gravity` 0 ; après la 22 : faux, 1250, `MaxFallSpeed` 800 ; mise à jour 23 : `CollidedWithEntityZ` 1 (repos,
    R5 d) ; `PosX` et `PosY` inchangés, fraction comprise.
  - **UJ-3b** (`0x1B [0,8]` sur le héros) : `PosZ` aux mises à jour 1 à 30 = 491520, 950272, 1376256, 1769472, 2129920,
    2457600, 2752512, 3014656, 3244032, 3440640, 3604480, 3735552, 3833856, 3899392, 3932160, 3932160, 3899392,
    3833856, 3735552, 3604480, 3440640, 3244032, 3014656, 2752512, 2457600, 2129920, 1769472, 1376256, 950272, 491520 ;
    mise à jour 31 : 0, `IsOnGround` 1, pas atterri ; atterri à la 32. Aucun son.
  - **UJ-11** (gel en vol) : gel puis reprise sur une image sans tick : verticale externe vraie, `PosZ` inchangé, le vol
    reprend à la valeur suivante de la liste.
  - **UJ-12** (`0x1B [0,8]`, le héros pousse vers une entité de 32 px de haut ; sonde d'obstacles installée,
    `ContactWorld.BuildWorld(champ, sonde)` comme T-R3 et T-R9 d'E19.d2b, avec le chemin de tick du héros) : pas XY
    libre exactement aux mises à jour 5 à 26 (`PosZ` ≥ 2097152), bloqué avant (`XCollisionEntity` = l'entité).
  - **TR-P** (propriétaire de la gravité) : un `0x17` du héros (gravité moteur 0 et bit Gravity effacé,
    `AlundraEventProgramRunner.cs:634-641`), puis le bit rétabli comme le fait `MovePlayer` en jeu libre
    (`AlundraPlayerManager.cs:202`), puis un saut : après l'atterrissage, `Settings.Gravity` vaut 0 (valeur vivante
    restituée) ; sans `0x17` : 1250.
  - **UJ-SND** (lecteur de sons factice rendu par l'hôte, R8 ; animation 43 du montage avec `Sfx` 10) : `PlaySfx(10)`
    une fois au tick de l'impulsion d'UJ-3 ; une seule fois sous
    rattrapage ; aucun son pour UJ-3b ni UJ-7.
  - Commit : `feat(alundra): own the hero's airborne state in the logic tick for scripted jumps, with the take-off sound`
  - Fait le 2026-10-03 : rouges d'abord sur le code d'avant C3 : UJ-3, UJ-3b, UJ-11, UJ-12 (PosZ reste 0, l'entité ne se franchit pas), TR-P (deux cas : la verticale ne passe pas au tick), UJ-SND (aucun son) ;
    verts après : `Alundra.Tests` 2218 réussis (2210 + 8), 0 échec, aucun test existant touché, six traces inchangées (garde d'octets 0). Toutes les valeurs écrites tiennent telles quelles
    (UJ-3, UJ-3b, liste d'UJ-12 : bloqué aux mises à jour 1 à 4, libre de 5 à 26). Mutations jetables : restitution de `MapGravity` au lieu de la valeur capturée (TR-P), test d'atterrissage
    non strict (UJ-3, UJ-3b), sentinelle du gel retirée (UJ-11), tirage de `PosZ` ou de `IsOnGround` depuis le moteur en l'air (UJ-3c), son sur la marque `0x1B` (UJ-SND).
    Écarts : montage `JumpHeroRig` (`AlundraJumpTestSupport.cs`) sur `ContactWorld.BuildWorld` et `ContactHost` (qui gagne `SoundPlayer`) plutôt que sur `HeroWorldFixture.BuildWorld`, parce que UJ-12 exige la sonde d'obstacles
    et que `ContactHost` rend un `PlayerController` ; le héros du montage porte `Collidable` (la sonde ignore un héros qui ne l'est pas) ; UJ-3c ajouté (la vérité de `PosZ` et de `IsOnGround` en l'air,
    que les mutations de tirage ne faisaient pas rougir autrement) ; l'état n'existe que pour un héros à contrôleur (sans contrôleur, rien ne change) ; le `x160` de R6 vient avec C4 (UW-4).

- ✅ **C4 — Eau, glace, `x160`, bottes, `VramOR` par tick (R7), et l'arc A10J, tests d'abord.** Montage d'UJ-3 (bit
  Gravity compris : sans lui `UpdateVramFlags` rend 0, `AlundraEntityScriptProxy.cs:1559-1564`) sur un `TileMapData`
  synthétique (cases de marche 24 (`0x18`) ou 32 (`0x20`), hauteur 0), une mise à jour de calage au repos pour poser
  `CombinedVramFlagsOR`.
  - **UW-1** (eau, niveau 0, animation 1 : vitesse 208, accélération 1, direction 24, depuis l'arrêt) : `ForceX` après
    les ticks 1 à 4 = 79872, 79872, 79872, 79872 ; niveau 1 (`NumberOfItems[0x1A * 2 + 1] = 1`) : 79872, 159744,
    159744, 159744 (rouge aujourd'hui : 79872, 159744, …).
  - **UW-2** (glace, après calage) : `ForceX` = 4992 · k jusqu'à 159744 au tick 32 ; arrêt (animation 0) : 154752,
    149760, …, 4992, 0 au tick 32 (31 ticks non nuls, 37,78125 px) ; cible et pas en cache conservés.
  - **UW-3** : un PNJ sur eau ou sur glace a, tick pour tick, exactement les forces du même PNJ sur le sol plat (même
    animation, même accélération).
  - **UW-4** (`x160` : animation 2, case `0x18`, niveau 0) : `ForceZ` 204800 ; `PosZ` 204800, 376832, 516096, 622592,
    696320, 737280, 745472, 720896, 663552, 573440, 450560, 294912, 106496, puis 0 et atterri à la mise à jour 14 ;
    niveau 1 : valeurs d'UJ-3.
  - **UW-5** (berge) : héros sur une case de 16 px voisine d'une case d'eau à 0 px : aucun ralentissement (79872 puis
    159744) ; en l'air, `VramOR` vaut 0.
  - **TR-B** (proxy nu, `ScriptHost` nul, `AlundraPlayerManager.Tick`) : `CombinedVramFlagsOR = 0x08` : aucune exception,
    niveau 0, `ForceX` 79872 puis 79872 ; `VramOR = 0` : 79872 puis 159744.
  - **A10J** (10 `B[20]`, nouvel arc ; spec de l'annexe A.2 : « Overworld 2,1-10 », drapeaux {1654, 203}, arrivée
    `ArcArrival(16515072, 61341696, 0, ResetAnimationId, 16)`, vrai contrôleur, vrais préfabs ; F0 = première exécution
    de `0x0B @2441` ; limite d'images ≥ F0 + 330 ; ensemble des sautés : sur-ensemble {(0x90,6406), (0x2B,6413),
    (0x95,6418), (0x90,2689), (0x90,2828)} ; `EntityBlockCount` 0 : A10J entre dans `ArcsWithoutEntityContact`, T-REG-0).
    **Giles** (relationnel, D-E19-13 et D-E19-40, ± 1 image et ± 1 px) : impulsion à F0+101, atterrissage à F0+119,
    `0x25 @6393` rend 1 à l'image d'atterrissage ou la suivante, `@6394` part de x ∈ [521,5 ; 522,5] px et finit à
    x ∈ [732,3 ; 733,3] px, `0x24 @6400` rend à F0+257 vers y = 775 (± 2,5 px), `0x19` à F0+268, détruit à F0+269 ;
    `PosX` non entier à l'atterrissage (pas de troncature).
  - **A10J, héros** (valeurs du binaire, eau comprise, `PosZ` sans le `+1`) : `@2441` rend et `@2445`/`@2447` partent à
    F0+50 en (16515072 ; 56070144 ; 0) ; `PosX` à F0+51 16594944 puis +79872 par image jusqu'à 18032640 à F0+69, puis
    +159744 ; `@2447` rend, `@2451` et `@2453` à F0+158 en (32249856 ; 56016896 ; 0) ; `TargetAnimationId` 2 après
    `@2451` ; `PosZ` de F0+159 à F0+175 = 327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472,
    1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112 ; `PosX` à F0+159, +160, +161 = 32409600,
    32564736, 32715264, puis +150528 par image jusqu'à 34973184 à F0+176 ; `TargetAnimationId` 44 dès F0+160 ;
    `IsOnGround` 0 de F0+159 à F0+175 ; `37` rend à F0+162 ; `0x25 @2455` s'exécute de F0+162 à F0+176 (15 fois, 14 rendent
    0), rend 1 à F0+176 avec `CollidedWithEntityZ` 1, `IsOnGround` 1, `ForceZ` 0, `PosZ` 1048576 ; `@2456` part à F0+176
    en (34973184 ; 56016896 ; 1048576) et rend à F0+256 en (47748096 ; 56016896 ; 1048576) ; `@2460`/`@2462` à F0+256 ;
    `0x24 @2462` rend dans [F0+295 ; F0+299], héros à ± 2,5 px de (47827968 ; 51838976), `PosZ` 2097152, `ForceAdjusted` 1
    (l'original : F0+308 en (47877120 ; 50790400), il glisse le long de l'angle, E19.h) ; `0x53 @2463` vers la 135 en
    (30670848 ; 54001664 ; 1048576), direction 16. Toute valeur contredite est un arrêt.
  - Commit : `feat(alundra): port the hero's water, ice and lower-jump rules with the boots level, per tick`
  - Fait le 2026-10-03. Tests `AlundraHeroWaterIceTests` (UW-1 à UW-5, TR-B en deux cas, et UW-6 ajouté : `VramOR` rafraîchi
    à chaque tick) et arc A10J (`AlundraHeroJumpArcTests`, nouveau fichier) ; `ArcsWithoutEntityContact` gagne A10J. Rouges
    d'abord sur le code de C3 : UW-1, UW-2, UW-4, TR-B (cas `0x08`) et A10J (sans la règle d'eau, l'arc échoue dès `PosX` à
    F0+52 : 16754688 mesuré, 16674816 attendu) ; UW-3, UW-5 et le cas `0` de TR-B sont des gardes, vertes avant et après.
    Mutations jetables, toutes rouges : eau sans l'exception des bottes, glace divisée par 8, `x160` retiré, eau et glace
    pour toute entité, `VramOR` seulement en fin d'image, niveau de bottes ignoré. Vert après : `Alundra.Tests` 2227
    réussis, 0 échec ; garde d'octets des six traces à 0. A10J, héros : **toutes les valeurs du binaire tiennent à
    l'unité**, eau comprise ; `0x24 @2462` rend à F0+297, héros en (47827968 ; 51838976 ; 2097152), `ForceAdjusted` 1 ;
    arrivée sur la 135 en (30670848 ; 54001664 ; 1048576), direction 16 ; F0 = image 33 de l'arc.
  - **Arrêt et disposition (session principale)** : trois valeurs relationnelles de Giles contredisaient le plan (`0x24
    @6400` rend à F0+255 contre F0+257 ± 1, `0x19 @6410` à F0+266 contre F0+268 ± 1 ; la destruction était lue sur la
    désactivation). Cause mesurée : **O-E19-29**. Sur la rampe (30,49), la hauteur de Giles change à chaque tick ; chaque
    atterrissage passe par `PushLogicalPositionToRoot`, qui ramène X au pixel (732,84 → 732,0) et le fait monter de 2,0 px
    par image au lieu de 1,625 (F0+241 à F0+244). Il atteint y = 775 à F0+253 ; `0x24` rend à F0+255, `0x19` à F0+266
    (s+11), et le natif E le marque `FlagToDestroy` à F0+267. La dérivation du plan ignorait cette troncature, connue et
    laissée à plus tard par l'auteur. Ré-épinglé à F0+255, F0+266 et F0+267 (± 1), avec X entier au retour de `0x24`, cause
    et renvoi à O-E19-29 dans le test : un correctif d'O-E19-29 les ramènera à F0+257, F0+268 et F0+269. Sans effet sur la
    scène (Giles est détruit avant que le héros n'approche).

- ✅ **C5 — Hygiène reportée d'E19.d2b** (dispositions du §1.2h.2) : retrait de rec7 et de rec41 de la liste lu à l'image
  exacte qui suit la destruction ; un test de la règle `H2Obstacle ?? H1Obstacle` (deux entités qui raccourcissent X et
  Y) ; doc de la surcharge B5 rattachée aux deux surcharges ; références à la passe de contact retirée (un `cref`, deux
  commentaires) ; assistant `UlpUnits` retiré s'il reste inutilisé ; ventilation du rouge de B3 au §2 corrigée ; signal de
  fin de T-C61 ; miroir de `SailorThirteen` aligné sur la production ; la garde T-REG-0 ne masque plus l'échec d'origine
  d'un arc (elle ne lève pas depuis `Dispose` quand le test a déjà échoué) ; défaut d'`emu.py` (`0x37` rend à s+v) ajouté
  aux défauts connus de `research/census/README.md`. Les deux points moteur (doc d'`H1Curtailed`, contrat de `DrawDebug`)
  restent à une suite moteur. Vérification propre à C5 : les lectures resserrées sont vertes ; le nouveau test de la
  règle `H2Obstacle ?? H1Obstacle` échoue sous une mutation jetable qui inverse l'ordre ; la garde T-REG-0 lève toujours
  sur un arc vert qui a un contact (mutation jetable d'E19.d2b rejouée). Commit : `chore(alundra): close the E19.d2b hygiene items`
  - Fait le 2026-10-03. Pas de rouge d'abord au sens strict : la tâche ne change aucun comportement, ses tests sont des gardes resserrées. Nouveau test `TRH2_...` (`AlundraMovementObstacleProbeTests` : deux
    murs raccourcissent X puis Y au même tick, l'obstacle rapporté est celui de H2 ; seul H1 raccourci : H1) vert, et **rouge sous la mutation jetable** `H1Obstacle ?? H2Obstacle` de
    `MoveControllerAndPullPosition` (R9 reste vert : il ne discrimine pas l'ordre). Lectures de T-A19 et T-B9 resserrées à `destroyFrame + 1` : l'échantillon est indexé par le compteur d'images
    APRÈS l'image (`OneFrame` l'incrémente avant `OnFrame`), donc `destroyFrame + 1` est la fin de l'image même de `0x2E` ; rec7 et rec41 y sont déjà hors de la liste (mesuré : faux à
    120 et suivantes, 1044 et suivantes). Garde T-REG-0 : `Dispose` ne lève plus si une exception est en vol (`Marshal.GetExceptionPointers`, vérifié sous xunit 2.9.3 sur Windows : non nul
    dans un `Dispose` d'échec, nul sur un arc vert) ; mutation d'E19.d2b rejouée (A11 ajouté à `ArcsWithoutEntityContact` : la garde lève, 5 pas) et, avec T-A19 aussi ajouté, une assertion
    de T-A19 cassée à dessein rend l'échec d'origine, non celui de la garde. Signal de fin de T-C61 : `arc.Has(B, 983, 0x30)` (l'instruction qui pose G672, même image 336 qu'avant) ; `IsSet(672)`
    reste affirmé. Miroir de `SailorThirteen` : le contact est le rapport de la règle de la sonde de production (surcharge à position donnée, soulevables écartés) au lieu de la passe de
    chevauchement ; vert, même preuve. Docs : la doc de la règle (B5) est sur la surcharge à deux arguments, la seconde renvoie à la première ; `cref` périmé d'`AlundraWorldProxyGlobalFreezeTests`
    corrigé (`Update_TheContactIsFrozen_...`) ; commentaires périmés de `XCollisionEntity` (`AlundraEntityScriptProxy`) et de `CheckEntityInteraction` (`AlundraPlayerManager`) mis à jour ;
    `UlpUnits` retiré (inutilisé). §2 : ventilation du rouge de B3 reprise de la puce de B3. `research/census/README.md` : défaut 5 (`emu.py`, `0x37`). `Alundra.Tests` 2228 réussis (2227 + 1), 0 échec ;
    six traces inchangées (garde d'octets 0). Écart : aucun test existant hors de la liste de l'acceptation 2 touché ; le texte du plan ne dit pas où poser le test de H2 : `AlundraMovementObstacleProbeTests`.

- ✅ **C6 — Vérification et clôture.** `Alundra.Tests` en Release puis en Debug **en dernier**
  (`--blame-hang-timeout 300s`), `cmp` de la DLL Debug déployée ; convertisseur, analyseur et moteur non touchés.
  Verifier frais et contradicteurs (fidélité au binaire, non-régression, tests) ; dispositions au plan ; §0.2, §2, §3,
  doc d'architecture des opcodes si elle cite `0x25` ; mémoire.

- 🧪 **C7 — Recette en jeu (auteur).** En attente de l'auteur (avec celle d'E19.d2c2, qui se merge avec elle).
  1. F9 sur `day3-after-dream` : sur la 179, Bergus saute, sans son (le son n'est porté que pour Alundra) ; la chaîne 179 → 176 → 179 → 176 → 10 continue ; sur la 10, Alundra marche plus
     lentement dans le bassin, saute la falaise avec le son 10, Giles saute aussi ; la scène mène à la 135, puis 10 → 176
     → 178 → 183.
  2. Partie libre au jour 1 : sur la 165, Bergus saute deux fois sur place pendant la première visite ; la scène va au
     bout.
  3. Sur la 478 (vision) : tenir Y ou I (L2 ou R1), **seuls, dès l'arrivée et jusqu'à la fin du fondu** : Alundra monte de
     60 px puis retombe ; relâcher puis retenir ne refait rien.
  4. F9 sur `day4-meeting` : la réunion de la 185 mène à la 362 ; dans le rêve d'Olen, la bombe saute (impulsion
     1024) ; la scène continue.
  5. Les flèches et les boules de feu des cartes 382, 386 et 309, si elles sont atteintes, ne décollent pas à leur
     apparition.

**Acceptation d'E19.d2c1.**
1. Tests C1 à C4 écrits rouges d'abord sur la DLL d'avant leur tâche, verts après, avec les valeurs écrites d'avance ;
   une valeur contredite est un arrêt, jamais une ré-épingle.
2. **Code de test existant touché** — la liste est fermée ; toute autre assertion existante reste inchangée et verte :
   - **C1** : A10 (`AlundraInoaDayOneArcTests.cs` : comptes des sautés `:179-180`, ensemble des sautés `:178`, doc du
     test) ; T-A10v (`AlundraEntityContactArcTests.cs` : `(0x25,4741)` et `(0x25,5545)` retirés de l'ensemble des
     sautés, assertions TR-V ajoutées) ; R4 : la liste `IntroTraceHarnessTests.ImplementedOpcodes` gagne `0x25`
     (étiquette de trace seulement) ;
   - **C2** : A10 (`AssertFrame(arc, B, 354, 922)` → 960 ± 3, limite d'images 1000 → 1100, assertions de ré-épinglage
     ajoutées) ; T-C61 (assertion de la grille rec9 ajoutée) ;
   - **C4** : `ArcsWithoutEntityContact` (`AlundraArcSupport.cs:269`) gagne A10J ;
   - **C5** : T-A19 et T-B9 (`AlundraEntityContactArcTests.cs:106` et `:275` : `destroyFrame + 2` resserré à l'image
     exacte qui suit la destruction) ; T-C61 (signal de fin, sans changer ce qu'il vérifie) ; le miroir de
     `SailorThirteen_FullInteractionChain_SquareOpensTheBox_AndNothingReopensIt`
     (`AlundraDialogueOpcodesProductionTests.cs`, aligné sur la production) ; la garde T-REG-0 de
     `ArcRun.Dispose` (`AlundraArcSupport.cs:274`, ne lève plus quand le test a déjà échoué) ; l'assistant
     `UlpUnits` (`AlundraEntityContactArcTests.cs:32`) retiré s'il reste inutilisé ; nouveau test de la règle
     `H2Obstacle ?? H1Obstacle`, rouge quand l'ordre est inversé.
   A3 et toutes ses épingles restent inchangés ; T-REG-0 vaut 0 sur les onze arcs nommés et sur A10J.
3. Les six traces de référence sont identiques à l'octet après chaque tâche.
4. `Alundra.Tests` sans échec en Release puis en Debug, la DLL Debug déployée en dernier, `cmp` sans écart.
5. Recette C7 faite par l'auteur.

**Risques d'E19.d2c1.**
- `0x25` devient bloquant sur environ 465 sites : une entité qui n'atterrit jamais (sans gravité, gravité effacée par
  `0x63`, entité sans contrôleur dont la DLL ne met pas `IsOnGround` à jour) cale là où l'opcode était ignoré. Sur la
  chaîne : aucun site de ce genre recensé dans les programmes d'enregistrement ; les programmes B reciblés par
  `0x42`/`0x43` restent à recenser : **reporté à E19.h** (disposition de la relecture d'E19.e, 2026-10-03 : voir §1.2i).
- Giles et Bergus : leur `0x25` et leurs images dépendent de l'aimantation de 4 px du moteur (D-E19-40) : épingles
  relationnelles à ± 1 image ; les PNJ dont le dernier point en l'air est au-dessus de 4 px atterrissent par
  `PushLogicalPositionToRoot` et perdent leur fraction (O-E19-29, épinglé par UJ-1c).
- Marge de 0,25 px en y pour l'eau de la rangée 52 de la 10 : un départ décalé d'un quart de pixel supprime les 9 ticks
  d'eau et décale A10J.
- Un relancement `0x1C` ou un changement de ligne de direction d'une entité sans gravité à impulsion la fait monter
  sans fin (aucun plafond, E19.h).
- Après un saut scripté sans remise de l'animation, le héros reste en animation 44 (cartes 61 à 68 et 329, hors chaîne)
  jusqu'à E19.d2c2.
- L'envol de la 478 est actif (D-E19-41), y compris pendant la vision.
- La bombe de la 362 (rêve d'Olen, `C[5] @635`, IZF 1024) saute maintenant ; aucun arc ne couvre la 362 : la recette C7
  le vérifie.
- Les arcs à vrai contrôleur qui tiennent une direction juste après `PlaceHero` (A5, A5r, couloir de la 392) : risque
  faible d'une image à vitesse différente ; mesurés par la suite complète à chaque tâche.

**Relecture d'E19.d2c1 (2026-10-02).**
- Plan-verifier frais et auditeur des valeurs indépendant, sur `d85a156` : **REVISE**. Bloquants : (1) le test strict de
  R5 a, lu sur `FinalForceZ` après la remise à zéro de `:676-680`, aurait rendu 0 là où UJ-4, UJ-1 (mise à jour 23) et
  UJ-1b (mise à jour 22) attendent 1 ; corrigé : le test se lit sur la force du tick relevée avant la remise à zéro,
  gardée (R3, R5 a) ; (2) le cas avec gravité d'UJ-0x8000 n'était pas observable ; corrigé : montage, tick et
  observables écrits. P2 de l'auditeur : le montage héros n'avait pas le bit Gravity (ajouté à C3 et C4) ; la place de
  l'étape verticale du héros par rapport à l'effacement de `:174` (écrite dans R6). P3 et P4 portés : chemin du lecteur
  de sons (R8), montage à horloge pour UJ-8 et UJ-9, sonde pour UJ-12, TR-P avec le bit rétabli, UW-3 en égalité,
  épingles de T-A10v qui n'existent pas retirées du texte, bombe de la 362 (découverte, recette, risques), `(0x90,2828)`
  dans le sur-ensemble d'A10J, second tirage de `PosZ` (`:1900-1903`) dans R6, hôte à `PlayerController` pour le volet
  héros d'UJ-4, préséance du plan sur l'annexe (UJ-3, mise à jour 23).
- L'auditeur a recalculé toutes les autres valeurs (UJ-1, UJ-1b, UJ-3, UJ-3b, UJ-5, UJ-12, UJ-CLAMP, UW-1, UW-2, UW-4,
  A10, A12, A10J héros et Giles, traces « spawn » d'E19.d2c2) : justes ; restent invérifiables sans exécution l'image de
  `0x24 @2462` d'A10J et le total de base de 2185 tests (C0 le mesure).
- Relecture neuve sur `db67bab` : **REVISE**, les deux bloquants précédents levés. Bloquant restant : l'acceptation 2
  ne listait pas les retouches de tests existants que demandent C5, R4 et C4 ; **FIX** : liste fermée par tâche
  (acceptation 2) et vérification propre à C5. Deuxième REVISE : la relecture de clôture est la dernière ; un nouveau
  REVISE met E19.d2c1 en pause.
- Mesure de base de C0 faite le 2026-10-02 : `Alundra.Tests` 2185 réussis, 0 échec (Debug, `--blame-hang-timeout 300s`) ;
  les six traces identiques en contenu (le test réécrit les quatre traces du héros en fins de ligne LF : `git diff
  --exit-code` rend 0, fins de ligne remises par `git checkout`).
- Relecture de clôture sur `6d4d778` : **READY**. Exécution lancée le 2026-10-03 sous la consigne de l'auteur du
  2026-10-02 (« Fait tout E12 et E19 », mode AUTO : ni merge ni push).

**Vérification d'E19.d2c1 (2026-10-03).**
- **Commits** (branche `chantier/e19-opcodes`) : plan `d85a156`, `db67bab`, `6d4d778`, `5e74e86` ; C1 `8bcc4cd`, C2 `b4a573b`, O-E19-30
  `93cf6d7`, C3 `fc58684`, C4 `6a9a7a8`, C5 `7ea2426`, deux commentaires périmés `6d0cc23`. Rien sur `main`, rien poussé ; la
  modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` ni touchée ni indexée. Les commits des exécutants
  portent la signature `Claude Sonnet 5.5` imposée par leur outil.
- **Verifier frais : CONFIRMED** sur les critères 1 à 4. `Alundra.Tests` 2228/2228 en Release puis en Debug, la Debug en
  dernier, `cmp` de la DLL déployée sans écart ; traces identiques en contenu ; 25 mutations de production, une par règle
  (R1 à R8) et trois sur les arcs, toutes attrapées ; le diff des tests reste dans la liste fermée de l'acceptation 2 ;
  T-REG-0 vaut 0 sur les douze arcs nommés et reste actif (A11 ajouté par mutation : rouge).
- **Deux contradicteurs en lecture seule** (fidélité au binaire ; tests et non-régression) : aucun P0 à P2. Le premier
  confirme au désassemblage l'ordre du tick, `+0xF8` écrit seulement en `0x80038AE4` et `0x80038B64`, la fin de chaîne,
  le tour de boucle, les cinq points d'apparition par `InitializeEntity`, les forces du héros et des PNJ, l'eau et la
  glace sur copies locales, l'atterrissage strict, `IsOnGround`, `VramOR` et `0x25`. Le second confirme les deux
  dispositions octet par octet (programme de Wendell `@740`-`@777` sur la 165 ; chaîne de Giles `@6389`-`@6411` sur la
  10) et ajoute la contre-partie de la première : **Meade** (`@984`) interroge T105 une image sur deux, ce qui garde
  T105 effacé à 775 et la fin à 960 ; aucune assertion existante n'est affaiblie.
- **Dispositions** :

  | Constat | Priorité | Disposition |
  |---|---|---|
  | Verrou « impulsion prise » qui peut rester levé : un changement vu en attente à un tick puis annulé par le script de l'entité avant la validation de fin d'image (image de rattrapage, PNJ seulement) laisse `ZImpulseTaken` levé, et le changement suivant ne donne pas d'impulsion (le binaire en donne une à chaque changement). | P3, introduit | Reporté à l'hygiène d'E19.d2c2 : baisser le verrou quand plus rien n'est en attente à la validation, avec un test. |
  | L'exemption d'apparition (R2) ne regarde que l'animation : un changement de ligne de direction de l'animation d'apparition avant la première validation ne donne pas d'impulsion, le binaire en donne une. Aucun site réel (13 préfabs à IZF sur l'animation 0, programmes natifs). | P4 | Reporté à E19.m. |
  | Son à l'arrivée : le bloc de changement que fait `InitializeEntity` joue le son de l'animation (`0x80038BB0`), sauf décompte de changement de musique ou doublon de l'image ; un héros qui arrive en animation 2, 6, 43 ou 46 l'entend dans le binaire, pas dans la DLL (R8 : jamais à l'arrivée). `ZImpulseSfxOf` ignore aussi le `+0x100` (bit `0x20` de l'octet `0xD`), sans effet sur les animations à impulsion du héros. | P4 | Reporté à E19.h avec les sons des changements d'animation (O-E19-27). |
  | Ordre des PNJ : la DLL fait le pas XY puis le vertical sur le terrain d'après le pas ; le binaire fait Z avant XY sur le terrain d'avant le pas. Seconde cause, avec l'aimantation de 4 px, d'un tick d'écart à l'atterrissage d'un PNJ au bord d'une marche. | P4 | Consigné avec D-E19-40 ; E19.h. |
  | UJ-6 (grille) ne rougit sous aucune mutation d'une seule règle (exemption et règle `0x8000` se couvrent) ; T-C61 couvre l'exemption sur le même préfab. | P3 | Accepté. |
  | UJ-2 « aucun effet de bord » : `0x25` au pc 0 ne voit pas une écriture parasite de `Parameters[1]`. | P3 | Reporté à l'hygiène d'E19.d2c2 (`0x25` au pc 1). |
  | Le branchement de production du lecteur de sons (`AlundraWorldProxy`) n'a pas de test : UJ-SND passe par l'hôte de test ; la recette C7 le couvre. | P3 | Reporté à l'hygiène d'E19.d2c2. |
  | La garde T-REG-0 lit `Marshal.GetExceptionPointers()` dans `Dispose` ; aucun test permanent ne fixe ce comportement, et une assertion attrapée dans un `using` la court-circuite. | P3, introduit | Reporté à l'hygiène d'E19.d2c2 (un test de la garde). |
  | Giles : le mécanisme d'O-E19-29 (pas de 2,0 px de F0+241 à F0+244) n'est pas épinglé à l'image, seulement « X entier au retour ». | P3 | Reporté à l'hygiène d'E19.d2c2. |
  | Textes : commentaire de T-C61 (`0x30 @983` teste G672, `0x05 @990` le pose), doc d'A10 périmée (« returns 1 at its first call »), Meade non écrit dans le test d'A10 ; A12 sans `using` (un arc bloqué laisse l'état global sale) ; UJ-0x8000 avec gravité ne discrimine que le terme `!gravity` ; liste fermée de l'acceptation 2 qui omettait la propriété `SoundPlayer` de `ContactHost` et un commentaire et un `cref` de C5. | P4 | Reportés à l'hygiène d'E19.d2c2 ; la liste incomplète est acceptée (aucune assertion touchée). |
  | L'effacement du drapeau d'apparition par `0x1C` n'a pas de test (aucun site réel). | P4 | Reporté à E19.m (déjà noté). |

##### 1.2h.3.2 E19.d2c2 — Saut à la manette, chutes, dessus d'objets ⏳

**Résultat** : Alundra saute à la Croix (sur place ou en marchant), se dirige en l'air, retombe ; elle tombe des rebords
avec la même mécanique ; elle se pose sur les coffres, plateformes et interrupteurs et une plateforme qui bouge la porte ;
après un saut scripté elle revient à l'arrêt ou à la marche dès qu'elle touche le sol (cartes 61 à 68 et 329).

**Point de départ** (E19.d2c1 faite) : l'état en l'air du héros existe pour les sauts scriptés et `0x1B`
(`AlundraScriptedMotion.RunHeroVerticalTick`, champs `HeroAirborne`, `HeroFlyMarked`, `ZHeldByTick`,
`AirborneSaved*` ; atterrissage strict sur le terrain ; `IsOnGround` tenu par le tick pendant l'état ; son du décollage
par `ZImpulseSfx`) ; l'impulsion d'animation est prise au tick (R1) ; `CollidedWithEntityZ` suit le binaire (R5).

**Règles d'exécution** (contrat ; noms de champs indicatifs).

- **S1 — États de saut de `MovePlayer`** (binaire `0x80031E38`, `0x80031E84`, queue `0x80031EA8`) :
  - Idle (0) et Moving (1) gardent `TargetDirection = dir` et `CheckEntityInteraction` (E12.d) : `res` 2 → Idle, `res` 1
    → animation inchangée ; `res` 0 → la **queue** ;
  - 2, `0x2B`, `0x2C`, `0x2D` : `TargetDirection = dir`, **sans** `CheckEntityInteraction`, puis la queue ;
  - **queue**, dans cet ordre : en l'air (prédicat `Controller != null ? IsOnGround == 0 : HeroAirborne`) → `0x2C`
    si une direction est tenue (`buttonsHold != 0`, le quartet de la croix, comme Idle/Moving) sinon `0x2D` ; au sol,
    front de Croix (`pad.ButtonsJustPressed & Cross`) → si `CombinedVramFlagsOR & 0x4000`, fin sans rien changer (pas
    même le passage en Moving), sinon 2 (direction tenue) ou `0x2B` ; sinon Moving ou Idle (`TryUseItem`,
    `PlayerTryAction`, `PlayerTryAttack` et la course de Triangle restent des no-ops : Triangle tenu donne Moving ou Idle
    comme aujourd'hui) ;
  - la branche `LoadingMap` → `0x2D` reste non portée (O-E19-32) ; la sortie latérale d'échelle reste Idle (décision du
    2026-08-26), la chute suit par la queue à l'image suivante ;
  - **cachet** : quand la queue écrit 2 ou `0x2B`, elle note `MotionTickCount` ; tant qu'aucun tick n'a tourné depuis
    (image sans tick), `MovePlayer` ne réécrit pas la cible. Sous rattrapage (plusieurs ticks dans l'image), la décision
    reste par image : la transition `0x2B`/2 → `0x2D`/`0x2C` arrive un tick plus tard que dans le binaire (écart accepté,
    épinglé par SJ-6b).
- **S2 — Chute** (D-E19-43) : au premier tick d'une image, un héros à contrôleur, hors de l'état en l'air, entre dans
  l'état en l'air **sans impulsion** si le tirage de tête d'image de cette image a donné `IsOnGround` 0 **et était
  fiable** : au moment du tirage, la verticale n'était pas tenue ailleurs (`IsVerticalOwnedExternally` faux, donc ni
  escalade ni départ de transition ni gel ; la sentinelle d'escalade fait rendre « pas au sol » au moteur). Le tirage
  note ce fait (champ indicatif `HeadPullGroundTrusted`). L'image où une escalade se termine (sortie latérale, bas ou haut
  de l'échelle) n'entre donc jamais ; si le héros n'est pas au sol, la chute part à l'image suivante, au premier tirage
  fiable (« la chute suit par la queue à l'image suivante »). À l'entrée,
  `ForceZ` remis à 0 (le binaire au repos atterrit à chaque tick : `ForceZ` 0 ; l'escalade laisse ±0x10000), puis la
  gravité de la carte joue dès ce tick, même atterrissage strict. Sous rattrapage, l'entrée se décide au premier tick de
  l'image (le tirage est par image) : écart d'un tick au plus, accepté.
- **S3 — Dessus d'objets** (D-E19-42) : dans l'étape verticale du héros, la hauteur d'atterrissage est le plus haut du
  terrain et du dessus d'une entité trouvée par `EntitySupport.TryFindSupport` (même appel, mêmes gardes et même valeur de
  `PosZ` que la branche « trouvé » d'`EvaluateEntitySupport` pour un PNJ) ; atterrissage sur une entité : `PosZ` posé,
  `ForceZ` 0 avec gravité, `CollidedWithEntityZ` 1. **Posé sur une entité, le héros reste dans l'état tenu par le tick**
  (le moteur ne voit pas les boîtes d'entités) avec `IsOnGround` 1 ; il en sort seulement quand son appui est le terrain
  (atterrissage sur le terrain, ou passage d'un dessus à un terrain de même hauteur). `IsOnGround` pendant l'état :
  `PosZ <= max(terrain, dessus d'entité sous la boîte)` (la règle `FloorHeight` du binaire, `UpdateFloorHeight` la
  compose déjà).
- **S4 — Passager** (D-E19-42 ; binaire `0x80038998`, `0x800364C8`, `0x80037364`) : `RidingEntity` du héros recalculé en
  tête de chaque tick du héros, jamais gardé (règle exacte d'`EntitySupport.UpdateRidingEntities` : héros
  `(Flags & 0x4100) == 0x100`, dessus + 1 == `ModdedPosZ` d'avant le mouvement, recouvrement XY) ; porté et sans
  impulsion ce tick : `ForceZ`/`FinalForceZ` = ceux de la plateforme (force du tick, 0 pour un objet immobile),
  **avant** le test d'atterrissage de S3, et le pas XY du héros ajoute le déplacement réalisé de la plateforme. « Porté »
  se lit sur `RidingEntity` non nul, qui vaut l'entité logique de la plateforme (`LogicContextEntity`) : les montages de
  test posent `LogicContextEntity` sur leurs entités (`ContactWorld.AddEntity` ne le fait pas). Au repos sur un objet immobile, `CollidedWithEntityZ` vaut donc 0 (porté, pas ré-atterri). Le
  déplacement de la plateforme est celui de son dernier tick : le héros est mis à jour avant elle dans l'image (écart d'un
  tick de phase, accepté, épinglé par UH-14).
- **S5 — Son** : rien de neuf (le décollage 2/`0x2B` passe par R8).
- **S6 — Pas de tolérance de marche en l'air** (binaire `0x80037524`-`0x80037538` : un coin de case bloque si sa hauteur
  est `>= ModdedPosZ`, soit `> PosZ` en convention de la DLL ; la tolérance de 3 px ne joue qu'à `ForceZ == 0`,
  `0x80037848`) : pendant l'état tenu par le tick, avant le pas XY de chaque tick, `Settings.StepHeight` du contrôleur vaut
  0 si `ForceZ != 0` et la valeur capturée à l'entrée sinon ; restitution de la valeur capturée à la sortie de l'état, comme
  la gravité. La règle du moteur (`GroundHeight > pied + StepHeight`, `CharacterControllerComponent.cs:1339-1357`) donne alors
  exactement celle du binaire. Sans S6, un héros qui monte de 13,5 px entre dans une case de 16 px, y trouve un « sol » d'une
  image et peut ressauter de l'intérieur de la marche, ce que l'original rend impossible. Avancé d'O-E19-27 (le saut à la
  Croix rend le cas jouable).

**Tâches.**

- ✅ **D0 — Plan ; mesure de base des arcs à vrai contrôleur** : A5, A5r, le couloir de la 392 (direction tenue juste
  après `PlaceHero`) et TN-3 (arrivée à 32 px au-dessus d'une case de 16 px : le héros tombe de 16 px au départ) sont
  mesurés tels quels avant D1 ; tout écart d'une de leurs épingles après D1 ou D2 est un arrêt.
- ✅ **D1 — États de saut et cachet (S1), tests d'abord.** SJ-1 à SJ-4 (annexe B.2.2, avec la correction C.saut-dll sur le
  prédicat d'air), SJ-8 (menus, boîtes), UH-4 (bords de Croix : en l'air sans effet ; rebond au tick 22 ; Croix tenue sans
  nouveau front ; Croix et Carré la même image : l'interaction l'emporte), UH-5 (case `VramOR & 0x4000` : Idle, pas
  même Moving), le retour à Idle ou Moving après un saut scripté sur un héros relâché en animation 44. Test existant rouge
  par construction : `MovePlayer_OtherAnimationId` (`0x2D` + Droite → 1). UJ-7 (E19.d2c1) passe sous `ControlLocked` :
  sinon S1 remplace l'animation 2 par Idle dès la première image au sol et le test ne discrimine plus l'exemption R2.
  - Fait le 2026-10-03 : nouveau fichier `AlundraHeroJumpStatesTests.cs`, 26 tests (SJ-1 a à f, SJ-2, SJ-3, SJ-4, SJ-8 a et b, UH-4 a à c, UH-5, retour à Idle/Moving après un saut scripté, dont relâché en l'air). Rouge d'abord sur le code d'avant : 22 rouges, 4 verts d'emblée qui sont des gardes d'un chemin existant (SJ1c, SJ1e, SJ8a, UH4c) ; verts après S1, du premier coup, aux valeurs écrites. `Alundra.Tests` 2254 réussis (2228 + 26). Écarts : SJ-3, Triangle seul donne Idle (le plan l'emporte sur l'annexe, « inchangé ») ; UH-4 d est couvert avec SJ-1 e (MovePlayer direct, hôte à PNJ). S1 seul déplace déjà 47 lignes `posX`/`targetAnim` de chaque trace « spawn » (celles de l'annexe B.2.1, valeurs de `posX` du plan tenues) : non committées avec D1, remises par `git checkout`, elles se committent avec D2 aux valeurs complètes.
  - **Arrêt et disposition (session principale)** : `AlundraSaveBookEndToEndTests.RealMap17_TheBook_Oui_…` rougissait (animation 44 au lieu de Moving, jamais d'interaction avec le livre). Cause mesurée : le montage n'appelle jamais les systèmes d'exécution du monde ; le contrôleur du héros n'est jamais mis à jour, `IsGrounded` reste faux à vie, et le prédicat d'air de S1 prend le héros pour « en l'air ». Aucun effet en production (le contrôleur tourne à chaque image, World.cs:531). Correctif de montage, seul `OneFrame` change : il fait tourner `RuntimeSystems.Update` en tête d'image, après avoir inscrit le héros (ajouté à la main) dans `World.Entities` que le système de mouvement parcourt. Aucune autre ligne ni assertion du test ne change.
- ✅ **D2 — Saut à la manette en monde réel, chutes (S1, S2), tests d'abord.** UH-1 à UH-3 (tables S-A, S-B, S-B2, S-B3,
  S-I, S-B4 de l'annexe B.1), SJ-5, SJ-5b, SJ-5c, SJ-6, **SJ-6b** (deux ticks dans l'image du front : une impulsion,
  `PosZ` 622592 ; la cible reste 2 ou `0x2B` pendant les deux ticks de l'image, d'où `ForceX` 159744 au second tick d'un
  saut en marchant, 155136 dans le binaire : l'écart accepté de S1),
  SJ-7, SJ-9 (gel en vol), UH-7 (chute d'un rebord : table S-E, sauf `IsOnGround` **1** après le tick 14 dans la DLL :
  hors de l'état, `IsOnGround` vient du tirage de tête d'image, qui voit la position d'avant le tick ; il passe à 0 au tirage
  de l'image 15, d'où la chute dès le tick 15 comme dans le binaire ; c'est le même décalage que les images 220 et 221 des
  traces « spawn »), UH-8 (eau, `x160` : table S-D, S-D2), SJ-11 (rampe à
  deux ticks par image : cible 1 à chaque image), SJ-17 (gravité vivante restituée). **Traces « spawn »** : régénérées aux
  valeurs prévues ci-dessous, les quatre autres à l'octet ; épingles de `HeroTraceHarnessTests` changées par construction
  (première image en l'air 221 à `posZ` 2064384, la suivante plus basse, atterrissage à 231 à `posZ` 0). **Gel en pleine
  chute** : `AlundraLadderClimbTests.GameplayFreeze_MidFall_HeroHoldsItsHeight_ThenFallsOnWithTheSameVelocity` (héros Idle
  à 40 px au-dessus du sol, commande libre) passe par construction à la chute tenue par le tick : le montage pose
  `MapGravityRaw` 128 et `MapZViscosityRaw` 4096 (comme `AdoptPlayerPawn` ; sinon l'état ne retombe jamais) ; avant le gel,
  `ForceZ` décroît de 32768 par tick depuis 0 et `PosZ` suit ; pendant le gel, `PosZ` et `ForceZ` inchangés, verticale
  externe vraie ; à la reprise, la suite continue à la valeur suivante (assertions sur `Velocity.Z` et `MovementState` du
  moteur remplacées). Les trois tests de sortie d'échelle (`GravitySuspendedWhileClimbing_RestoredOnLateralExit`,
  `DescendingReachesBottom…`, `AscendingReachesTop…`) restent inchangés et verts : S2 n'entre pas à l'image de sortie.
  - Fait le 2026-10-03 : nouveau fichier `AlundraHeroFallAndPadJumpTests.cs`, 24 tests (UH-1, UH-1b, SJ-5, SJ-5b, SJ-5c, SJ-6, SJ-6b, UH-2, UH-2b, UH-3 a à c, SJ-7, UH-7, UH-7b, UH-8, UH-8b, SJ-9, SJ-11, SJ-17 en deux cas, S6, S6b, S6c). Rouge d'abord sur le code d'après D1 : seuls UH-7 (tick 15 : `posZ` 1048576, la chute n'entre pas) et les trois tests de S6 (StepHeight 3 en l'air) ; tous les autres sont verts d'emblée, parce que l'impulsion, les états de saut (D1) et le test d'atterrissage strict existaient déjà (UH-1 à UH-3, SJ-5 à SJ-7, SJ-9, SJ-11 et UH-8 sont des gardes des valeurs écrites, qui tiennent toutes telles quelles). Après S2 et S6 : 24 verts du premier coup, aux valeurs écrites (UH-7 comprise : `IsOnGround` 1 après le tick 14, chute dès le tick 15, `posZ` 1015808 puis 950272, 851968, 720896, 557056, 360448, 131072, atterri au 22, `posX` de 14 à 24). `Alundra.Tests` 2278 réussis (2254 + 24), 0 échec. **Traces « spawn »** : régénérées, comparées par script ligne à ligne aux valeurs prévues : 47 lignes par fichier, exactement celles du plan (`posZ` 2064384 à 294912 de 221 à 230, `tileZ` 1 de 221 à 227 puis 0, `isOnGround` 0, `targetAnim` 44 ; 231 `posZ` 0, `isOnGround` 1, anim 44 ; 232 anim 1 ; `posX` prévus puis ancien moins 101376), aucune autre ligne de données ne bouge ; les deux traces « highground » et celles de l'intro restent à l'octet. L'en-tête des deux traces « spawn » change de deux lignes (la note « chute » ne parlait plus que de la gravité du moteur). `HeroTraceHarnessTests` : première image en l'air à 221 à `posZ` 2064384 (était 2097152), atterrissage à 231 (était 232). Test de gel en pleine chute d'`AlundraLadderClimbTests` réécrit comme écrit au plan (même nom) ; les trois tests de sortie d'échelle inchangés et verts. Mutations jetables de production, toutes attrapées : drapeau de tirage fiable retiré ou qui ignore la verticale externe (les trois tests de sortie d'échelle), `StepHeight` gardé en l'air (les trois S6), non restitué à l'atterrissage (A10J et les trois S6) ou à l'adoption (S6c), cachet retiré (SJ-4, SJ-5b, SJ-5c), prédicat d'air de l'ancienne forme (14 rouges), cases `0x4000` ignorées (SJ-1 d, UH-5).
  - Écarts : (1) la chute n'entre que si le bit Gravity est posé (le binaire n'applique pas de gravité sans lui ; sans cette garde un héros sans le bit resterait suspendu à `ForceZ` 0 sans suivre le terrain) ; le texte de S2 ne le dit pas, aucune valeur du plan n'en dépend ; (2) S6 : `AdoptPlayerPawn` restitue maintenant les valeurs capturées du moteur (dont `StepHeight`, et la propriété verticale qu'il laissait vraie) quand l'état en l'air est levé, avant de remettre les réglages de la carte ; (3) SJ-11 (rampe 1:1 à deux ticks par image) sur une rampe synthétique de pente 1 de `FlatCells` plutôt que la rampe de la 389 : cible 1 à chaque image, vert d'emblée.
- ✅ **D3 — Dessus d'objets et passager (S3, S4), tests d'abord.** UH-6 (marche de 16 px : table S-C, avec les
  corrections C.saut-binaire : animation 44 jusqu'au tick 18 inclus, Moving au 19 ; sous S6, x0 = 126 à 128 bloqués au
  seul tick 3, 129 à 133 aux ticks 2 et 3, x finale au tick 30 = 196,633 px (± 0,05) de 126 à 133, 195,734 px pour 125 ;
  `IsOnGround` 0 à chaque tick du vol, jamais d'image « au sol » dans la marche), UH-10/UH-11 (coffre
  24 × 16 × 16 : atterrissage au tick 18, `PosZ` posé selon S3, `CollidedWithEntityZ` 1 au tick 18 puis 0 aux ticks 19 à
  24, `IsOnGround` 1 jusqu'au 24 et 0 au 25, animation 44 au 26 ; `RidingEntity` = le coffre tant qu'il y repose ;
  `0x3E` rend 1), SJ-12 et SJ-13 (valeurs relatives au dessus de l'entité, la convention de Z d'apparition à lire sur la
  fixture), **UH-14** (plateforme qui avance de 1 px par tick : le héros posé dessus avance de 1 px par tick, un tick de
  phase après elle ; il ne glisse pas hors d'elle en 50 ticks).
  - Fait le 2026-10-03 : nouveau fichier `AlundraHeroObjectTopsTests.cs`, 18 cas (UH-6 principal et son tick d'atterrissage, 9 lignes de variantes, UH-10 en trois tests, UH-11 avec `0x3E`, SJ-12, SJ-13, UH-14). Rouge d'abord sur le code de D2 : SJ-12 (tick 18, `PosZ` 884736), SJ-13 (tick 14, `PosZ` 1605632, la valeur « sans appui »), UH-10 (héros bloqué dans le coffre), sa chute, UH-11 (`RidingEntity` nul), UH-14 ; verts d'emblée (gardes) : UH-6 principal et variantes 125 à 130, UH-10 sans saut. Verts après S3/S4 aux valeurs écrites. `Alundra.Tests` 2296 réussis (2278 + 18), garde d'octets à 0. Code : `EntitySupport.UpdateRidingEntityOfHero` (passager recalculé en tête de chaque tick), atterrissage sur `TryFindSupport` avec la graine d'`EvaluateEntitySupport` (le héros reste dans l'état tenu par le tick sur une entité), `ForceZ`/`FinalForceZ` de la plateforme avant le test, `IsOnGround` par le sol composé (`ComputeFloorHeight`), pas XY du porté augmenté du `LastTickDeltaX/Y` de la plateforme.
  - **Arrêt et disposition (session principale)** : UH-6, x0 = 131 à 133, ticks bloqués mesurés {1, 2, 3} (pas XY plus court que `ForceX`) au lieu de {2, 3}. À x0 ≥ 131 le bord avant est à 2 px ou moins de la marche : le premier pas du tick 1 (pied à 5 px) atteint déjà le contact (`x` = 133,0) par l'avance au contact d'E19.a2, ou part du contact (133) ; la règle du binaire (case > pied, S6) bloque forcément ce tick ; la table de l'annexe ne dit pas sa définition de « bloqué » et aucune définition ne réconcilie toutes ses lignes ; la position finale est identique. Mesure épinglée ; à revérifier contre le binaire avec E19.h (glissement et résolution du pas). La Theory ajoute pour toutes les lignes l'invariant de S6 : `IsOnGround` 0 à chaque tick du vol et bord avant hors des cases de 16 px tant que `PosZ` < 1048576.
  - Écarts et points notés : (1) le pas XY du porté est ajouté même au tick du décollage (binaire, annexe M2), la condition « sans impulsion » ne valant que pour `ForceZ` ; (2) « déplacement réalisé » = position après le pas moins avant (`LastTickDeltaX/Y`), non `AdjustedForce` (égal à `ForceX` même quand le contrôleur raccourcit le pas) ; (3) pas de test de plateforme qui monte : le héros, mis à jour avant elle, perdrait son passager un tick plus tard (phase d'un tick d'S4) ; (4) montage d'UH-14 : plateforme ajoutée après le héros, qui va vers l'ouest, boîte logique décalée de sa racine (`offsetX`) pour que sa boîte physique de 18 × 12 × 32 ne rencontre jamais le corps du héros ; (5) SJ-13 sans variante « sans appui » (son rouge sur D2 la documente).
- ✅ **D4 — Hygiène reportée d'E19.d2c1** (dispositions de « Vérification d'E19.d2c1 ») :
  - **verrou d'impulsion** : `SyncAnimation` baisse `ZImpulseTaken` aussi quand plus rien n'est en attente à la fin de
    l'image (changement vu à un tick puis annulé par le script avant la validation) ; test **UJ-LOCK** (PNJ, image à deux
    ticks : cible 3 (IZF 1360) écrite par son script au tick 1, remise à la cible courante au tick 2 ; à l'image suivante,
    cible 3 de nouveau (image à un tick) : `ForceZ` vaut 348160 après ce tick, l'impulsion
    sans décroissance ; rouge aujourd'hui : la décroissance continue) ;
  - UJ-2 : le programme met `0x25` au pc 1 (comme le premier cas) pour que « aucun effet de bord » voie `Parameters[1]` ;
  - un test du branchement de production du lecteur de sons (`AlundraWorldProxy` rend son lecteur par
    `IAlundraScriptHost.SoundPlayer`) ;
  - un test permanent de la garde T-REG-0 (`ArcRun.Dispose` lève sur un arc vert à contact, ne lève pas quand le test
    échoue déjà) ;
  - A10J : épingler le mécanisme d'O-E19-29 sur Giles (pas de y de 131072 aux images F0+241 à F0+244, puis 106496) ;
  - textes : commentaire du signal de fin de T-C61 (`0x30 @983` teste G672, `0x05 @990` le pose) ; doc du test A10
    (« returns 1 at its first call » périmé) et la phase de Meade (`@984`, une image sur deux) à côté de celle de
    Wendell ; A12 sous `using` (un arc bloqué ne doit pas laisser l'état global sale).
  Commit : `chore(alundra): close the E19.d2c1 hygiene items`
  - Fait le 2026-10-03 : `SyncAnimation` baisse `ZImpulseTaken` quand plus rien n'est en attente à la validation (UJ-LOCK, `AlundraAnimationImpulseLockTests.cs`, rouge sans le correctif puis vert : 348160 et impulsion sans décroissance). UJ-2 : `0x25` au pc 1 (3 tests verts, les index de `CodeIndex` décalés de 1). Nouveau `AlundraArcGuardAndSoundHostTests.cs`, 4 tests verts d'emblée (ce sont des gardes d'un comportement existant) : la garde T-REG-0 (lève sur un arc gardé qui a rencontré une entité, ne lève pas hors de l'ensemble gardé ni quand le test échoue déjà), et `AlundraWorldProxy.SoundPlayer` rendu par `IAlundraScriptHost.SoundPlayer` (champ posé par réflexion, `InstallAudioSystems` demandant un `Game`). A10J épingle les pas de Giles : Y de 131072 aux images F0+241 à F0+244 puis 106496 à F0+245, mesurés en valeur négative (il va vers les Y décroissants ; le plan donnait les modules). Textes : T-C61 (`0x30 @983` teste G672, `0x05 @990` le pose), doc d'A10 et phase de Meade (`@984`, une image sur deux, T105 effacé par son `0x06 @1026` à l'image 774) à côté de celle de Wendell ; A12 : l'arc est créé sous `using` avant la course (`Run(arc)`), car `Run()` créait l'arc et pouvait lever avant le `using`. `Alundra.Tests` 2301 réussis (2296 + 5), garde d'octets à 0. Aucune assertion existante autre que celles de la liste fermée n'a bougé.
- ✅ **D5 — Correctifs de la vérification** — **D5a faite le 2026-10-03** (F1, F2b, F3, F4, F5) ; **F2 en D5b, ⏳ en pause** (voir l'audit
  ci-dessous) — (2026-10-03, après le verifier CONFIRMED sur `103dcb7` et les deux
  contradicteurs ; nouvelle époque de relecture : une relecture de clôture avant exécution ; **révisé le 2026-10-03 à la
  demande de l'auteur, D-E19-56** : F2 complété, F2b ajouté (le P3 du passager sur un porteur avec gravité), F5 réduit). Tests d'abord, une valeur
  contredite est un arrêt :
  - **F1 — Fenêtre Y du passager** (contradicteur binaire, P2) : `CheckRidingEntities` compare `deltaY < Height + 1`
    (`0x80036514`, `0x80036528`, `0x800365B0`) ; `EntitySupport.FindRidingEntity` (`EntitySupport.cs`, branche `deltaY >= 0`)
    garde la bizarrerie de la décompilation (`Depth + 1`, « sic »), que la physique du héros lit depuis D3. Correctif : `Height
    + 1`, comme le binaire, pour toute entité (la même fonction sert `0x3E`). Test **UH-15** (coffre 24 × 16 × 16, héros posé
    dessus, marche vers le nord) : `RidingEntity` nul et chute dès le tick où le recouvrement en Y cesse (`deltaY` > 15 px),
    pas 17 px plus loin (la fenêtre est `Height + 1` de l'entité ; la relecture de clôture du 2026-10-03 la trace à 15 px pour
    le héros : le recouvrement cesse à `deltaY` ≥ 15 px ; le tick exact est relevé par l'audit des valeurs) ; `PosZ` 1015808 à ce tick (le binaire, `sc_north.py` : tick 8, 1015809 avec son `+1`).
  - **F2 — Plateformes qui bougent en Z** (contradicteur binaire, P2 ; D-E19-42) : le binaire calcule `RidingEntity` de toutes
    les entités sur les positions d'avant tout mouvement, puis les forces de toutes, puis applique la force du tick de la
    plateforme (`0x800364C8`, `0x80036828`, `0x80037364`) ; la DLL met le héros à jour avant la plateforme dans l'image, et
    l'égalité exacte `dessus + 1 == ModdedPosZ` se rompt au premier tick où la plateforme change de vitesse en Z : le héros
    n'est jamais porté par une plateforme qui monte (il passe au travers) ni qui descend (`IsOnGround` alterne). Correctif,
    écart d'ordre assumé (S4b) : un héros porté au tick précédent par une plateforme dont la boîte le recouvre encore en X et
    en Y (fenêtre de F1) reste porté même si l'égalité exacte est rompue ; son `PosZ` est recalé sur `dessus + 1` de la
    plateforme (convention de la DLL) avant le test d'atterrissage, puis il prend la force de la plateforme (`ForceZ` **et**
    `FinalForceZ`, `0x800373BC`, P4 du même contradicteur). **Révision (D-E19-56)**, porteur **sans gravité** (les
    plateformes pilotées en Z) : un héros porté n'atterrit pas sur son porteur et ne lève pas `CollidedWithEntityZ` (dans le
    binaire, il recopie la force de la plateforme et la suit sans la traverser : `+0x140` reste 0 sans gravité sur le
    porteur, découverte d'E19.h, surface A) ; `IsOnGround` vaut 1 tant qu'il est porté, quelle que soit la phase du tick (le
    héros est mis à jour avant la plateforme : sans cette règle, il monte à `dessus + v` avant elle et finit le tick en l'air) ;
    **une seule règle, une seule source** (révision n°2) : le « porté » du héros est son `RidingEntity` ; la valeur d'avant le
    recalcul est l'entrée de la continuation ; le recalcul de tête de tick (`UpdateRidingEntityOfHero`,
    `AlundraScriptedMotion.cs` ~187-190) et la passe par image (`EntitySupport.UpdateRidingEntities`, appelée par
    `RefreshUpdateProxiesAndCollidables`, `AlundraWorldProxy.cs` ~2664, qui traite toute entité collidable à `Gravity`, le
    héros compris, `EntitySupport.cs` ~272-283) appliquent pour le héros la **même** fonction (égalité exacte, sinon
    continuation) ; les autres entités gardent la règle exacte ;
    il cesse d'être porté par une impulsion (saut), par la fin du recouvrement XY (fenêtre de F1), ou si
    `|PosZ − (dessus + 1)|` dépasse **strictement** `|LastTickDeltaZ|` de la plateforme : nouveau champ moteur de l'entité, le
    déplacement réalisé en Z par son dernier tick logique, soit `PosZ` après `EvaluateEntitySupport` moins `PosZ` avant
    `TickScriptedNpc`, dans le même tick de `Update` (`AlundraEntityScriptProxy.cs` ~1230-1243 : le pas XY de
    `RunOneKinematicTick` ne bouge jamais `PosZ`, `AlundraScriptedMotion.cs:508-536`, c'est `EvaluateEntitySupport` qui le
    bouge, `AlundraEntityScriptProxy.cs` ~762, ~857 et ~891) ; mesuré pour les entités autres que le héros seulement (dans D5,
    le héros ne porte personne ; son champ reste 0) ; une écriture de `PosZ` hors de ce tick (tirage de tête d'image,
    téléportation d'un montage) ne le change pas ; sur le montage d'UH-16, la montée de 1 px par tick lancée,
    `LastTickDeltaZ` de la plateforme vaut 65536. Tests (révision n°1 de la relecture du 2026-10-03 : la mesure était placée
    dans le pas XY)
    **UH-16** (montage d'UH-14, plateforme pilotée en Z par `ForceZ` sans gravité ; après chaque image, le test appelle
    `EntitySupport.UpdateRidingEntities` sur la liste des collidables, comme la production ; `ContactHost.Rebuild` ne change
    pas) : montée de 1 px par tick pendant 50
    ticks : `RidingEntity` non nul, `IsOnGround` 1, `CollidedWithEntityZ` 0 à chaque tick, `PosZ` du héros = dessus + 1 à 1
    px près (un tick de phase), jamais en dessous du dessus ; descente de 1 px par tick : mêmes invariants, aucune image en
    animation d'air ; enfoncement de 0,125 px par tick : `RidingEntity` non nul, `CollidedWithEntityZ` 0 ; `0x3E` rend 1 dans
    les trois cas ; **téléportation** (révision n°2) : plateforme arrêtée au sommet de la montée de 50 ticks (`PosZ` de la
    plateforme 50 px, `ForceZ` 0, sans gravité, dessus à 66 px), héros porté ; entre deux images, `PosZ` de la plateforme
    abaissé de 32 px (18 px, toujours au-dessus du terrain à 0) par écriture poussée à la racine (`PosZ` puis
    `PushLogicalPositionToRoot`, le geste de placement d'UH-14) : la plateforme reste à 18 px les ticks suivants (sans force ni
    gravité, aucune branche d'atterrissage ne la touche) ; au tick suivant, `RidingEntity` nul, aucun recalage du héros, chute
    par la gravité depuis le repos à partir de 66 px, atterrissage sur le nouveau dessus (34 px, `PosZ` 2228224) avec
    `CollidedWithEntityZ` 1 au tick d'atterrissage ; les hauteurs de chaque tick et le tick d'atterrissage (11 attendu : 33 px
    cumulés après 11 ticks) sont relevés par l'audit des valeurs avant l'exécution.
  - **F2b — Porteur avec gravité au repos** (P3 du contradicteur binaire, reporté à E19.h puis rendu à D5 par D-E19-56) :
    dans le binaire, les forces de toutes les entités sont calculées avant la boucle des passagers (`0x80036828`) ; un
    porteur au repos avec gravité a alors `FinalForceZ` = −(Gravity << 8) (−32768 sur ces cartes), le passager le recopie
    (`0x80037364`) et atterrit sur lui à chaque tick : `CollidedWithEntityZ` 1 à chaque tick, `ForceZ` 0, comme le héros au
    repos sur le terrain (R5 d, UJ-4). La DLL recopie le `FinalForceZ` du porteur, qu'elle remet à 0 à l'atterrissage du
    porteur (`AlundraEntityScriptProxy.cs` ~795-799 et ~860-864, alors que le binaire ne remet à 0 que `ForceZ`, `0x80037700`) :
    le héros porté lit 0 et ne lève jamais le contact. Correctif : la force que le héros porté recopie est la force du tick
    du porteur prise **avant** son atterrissage (la valeur que le binaire lit au même point de la passe) ; rien ne change pour
    un porteur sans gravité (sa force de tick au repos vaut 0 : UH-10, UH-11, SJ-12, SJ-13 et UH-14 gardent leurs valeurs ;
    les objets de `ContactWorld.AddEntity` n'ont pas le bit `Gravity`). Test **UH-18** (révisé après la relecture du
    2026-10-03) : montage d'UH-14 sans mouvement, même objet 24 × 16 × 16 et même décalage `offsetX`, ajouté après le héros
    (il est mis à jour après lui, comme en production) ; l'objet reçoit le bit `Gravity`, `MapGravityRaw` 128,
    `MapZViscosityRaw` 4096 puis `ResyncControllerFromFlags` (la décroissance lit les champs bruts de l'entité,
    `AlundraEntityScriptProxy.cs` ~679-694 ; même geste qu'`AlundraCollidedWithEntityZTests.cs:71-74`) ; héros posé par
    `PosZ` 1048576 et `PushLogicalPositionToRoot`, sans entrée ; 4 images de calage (l'objet y fait ses premiers ticks, sa
    force de tick au repos vaut alors −32768) ; puis, à chacun des ticks 1 à 10 : `RidingEntity` = l'objet,
    `CollidedWithEntityZ` 1, `ForceZ` 0, `IsOnGround` 1, `PosZ` 1048576, animation de repos, `0x3E` 1 ; les valeurs de chaque
    tick sont confirmées par l'audit des valeurs avant l'exécution. Variante sans le bit `Gravity` sur l'objet (champs bruts
    laissés à 0) : `CollidedWithEntityZ` 0 aux mêmes ticks (garde).
  - **F3 — Dessus à fleur du sol** (contradicteur binaire, P3, visible : 122 interrupteurs à piétiner, 38 plateformes à
    quai) : le tirage du moteur ignore les boîtes d'entités et rend `IsOnGround` 0 quand le héros passe du sol au dessus
    d'un objet de même hauteur posé dans un creux ; `MovePlayer` montre alors une image d'animation de saut. Correctif : au
    tirage de tête d'image d'un héros hors de l'état, si le moteur rend « pas au sol » mais qu'un dessus d'entité sous la
    boîte est exactement au pied (la règle de `TryFindSupport`, celle de `FloorHeight` du binaire `0x80037F28`), alors
    `IsOnGround` vaut 1 et le premier tick entre dans l'état tenu par le tick **au repos sur l'entité** (sans chute, `ForceZ`
    0). Test **UH-17** (montage : boîte physique de l'interrupteur décalée par `offsetX`, comme UH-14 ; héros en marche vers l'est
    sur un sol de 16 px ; une case creuse à 0 px ; un interrupteur
    24 × 16 × 16 posé dans le creux, dessus à 16 px, à fleur du sol) : animation Moving à chaque image, `ForceX` 159744
    constant, `PosZ` 1048576 constant, `RidingEntity` = l'interrupteur pendant le recouvrement, `0x3E` 1 ; puis retour sur le
    sol de l'autre côté, sortie de l'état, toujours sans image d'animation d'air.
  - **F4 — Entrée en escalade pendant l'état** (contradicteur tests, P3, atteignable depuis D1) : le portail de pente 6 de
    `MovePlayer` peut poser `Climbing` au tick où le pied touche le sol avant l'atterrissage strict ; l'état tenu par le tick
    continuait alors avec l'escalade. Correctif : l'entrée en escalade (`SuspendGravityForClimb`) termine d'abord l'état
    (restitution des valeurs capturées, `HeroAirborne` faux), puis l'escalade prend la verticale. Test **UJ-CLIMB** (saut
    vers le nord contre un mur d'échelle, Haut tenu) : `Climbing`, `HeroAirborne` faux, gravité et `StepHeight` du moteur
    tenus par l'escalade (`Gravity` 0, verticale externe) puis rendus à la sortie de l'échelle.
  - **F5 — Tests** (contradicteur tests, P2 introduits) : UH-7b, **révisé (D-E19-56, puis révision n°2)** : après
    `FreeRig` (dont la mise à jour de calage aimante déjà le héros au sol), le test pose `PosZ` = 196608 (3 px au-dessus du
    sol) par `PushLogicalPositionToRoot` (geste d'UH-14) et l'observe aussitôt, héros hors de l'état ; puis, à chaque image de
    la boucle, `HeroAirborne` faux et aucune animation d'air, et à la fin `PosZ` 0 et `IsOnGround` 1 (l'aimantation de 4 px du
    moteur absorbe la descente de 3 px sans entrer dans l'état) ; la vraie descente de 3 px demande des cases de hauteur libre que
    `FlatCells` ne sait pas construire (hauteurs de 16 px seulement) : manque de montage consigné pour E19.m ; le test de gel en pleine
    chute d'`AlundraLadderClimbTests` retrouve ses assertions sur la racine (`Position.Z` figée sur les 5 images de gel,
    plus basse après la reprise) ; SJ-17 assert que le héros a décollé (`HeroAirborne` vrai à un tick intermédiaire).
  - Commit : `fix(alundra): rider window, carriers with gravity, flush tops and climbing from a jump (E19.d2c2 D5a)` (D5a ; le message d'origine citait F2, parti en D5b)
  - **Relectures de la reprise** (D-E19-56) : n°1 sur `3eb96b1` **REVISE** (mesure de `LastTickDeltaZ` placée dans le pas XY ;
    montage d'UH-18 sans gravité brute) ; n°2 sur `22f4ca8` **REVISE** : (1) la passe par image `UpdateRidingEntities` récrit
    le `RidingEntity` du héros par la règle exacte : **corrigé**, une seule fonction pour le héros aux deux sites, UH-16 lance la
    passe ; (2) la téléportation d'UH-16 partait d'une plateforme à 0 px, que l'atterrissage au terrain remettait en place :
    **corrigé**, départ au sommet de la montée ; (3) la précondition d'UH-7b était déjà aimantée par la mise à jour de calage de
    `FreeRig` : **corrigé**, pose et observation après le calage. Deux REVISE automatiques : nouvelle époque de relecture,
    une seule relecture de clôture ; un nouveau REVISE met D5 en pause pour l'auteur. **Relecture de clôture sur `99ff51d` :
    READY.**
  - **Audit des valeurs** (2026-10-03, sur `6e6a0cd`, `e19d2c2-audit-d5/notes.md` du scratchpad) : F1, F2b, F3, F4 et F5
    tenus (valeurs ci-dessous) ; **F2 contredit** : C1 la règle « `IsOnGround` 1 tant qu'il est porté » déplacerait UH-10 (au
    tick 25, le héros est encore porté en tête de tick mais son pas le sort du coffre ; le binaire rend 0 à ce tick) ; C2 la
    montée d'UH-16 lancée par `ForceZ` seul laisse le héros 1 px sous le dessus au tick 1 ; C3 la descente et l'enfoncement
    enchaînés font tomber le héros ; G1 (conception) : la force recopiée a un tick de retard, la tolérance `|LastTickDeltaZ|`
    ne couvre pas un changement de vitesse : à l'arrêt d'une montée le héros saute, à l'arrêt ou au ralentissement d'une
    descente il **passe au travers** de la plateforme (la production a le même retard : un script arrête la plateforme pendant
    son propre tick, après celui du héros). **Disposition** (session principale) : D5 est resserrée en **D5a** (F1, F2b, F3, F4,
    F5, valeurs tenues, exécutable) ; **F2 passe en D5b**, en pause : sa conception est à reprendre (par exemple résoudre le
    passager après le tick de son porteur) et à soumettre à l'auteur ; la chaîne de l'histoire n'en dépend pas (aucun `0x3E`
    ni plateforme en Z sur la chaîne). Nouvelle époque de relecture pour D5a : une seule relecture de clôture.
  - **D5a — valeurs de l'audit, écrites d'avance** : UH-15, montage d'UH-14 (coffre décalé par `offsetX` 44, sans quoi sa boîte
    physique touche le corps du héros dès le départ), Haut tenu depuis l'arrêt : le recouvrement cesse au **tick 12** (tête à
    `PosY` 83,4375 px, `deltaY` 15,5625 px ; le recouvrement tient tant que `PosY > 84 px`), `PosZ` 1015808 à ce tick, chute
    ensuite (l'ancienne fenêtre `Depth + 1` le portait 17 px plus loin ; le tick 8 du binaire, `sc_north.py`, vient d'une force
    nord artificielle, non comparable) ; UH-18 : force de tick du porteur −32768, ticks 1 à 10 tenus
    (`CollidedWithEntityZ` 1, `ForceZ` 0, `IsOnGround` 1, `PosZ` 1048576 ; seule la première image de calage donne 0), variante
    sans `Gravity` : 0 ; UH-17 : héros placé à `y` 103,5 (dans [103, 104]) pour que sa boîte tienne dans une seule rangée de
    cases, sinon une case creuse ne retire jamais le sol au moteur et F3 n'est pas exercé ; UJ-CLIMB : `StepHeight` 3 pendant
    l'escalade, puis `Gravity` 1250, `MaxFallSpeed` 800, verticale non externe après l'échelle ; UH-7b : `PosZ` 196608 hors de
    l'état, `HeroAirborne` faux à chaque image, fin à `PosZ` 0 et `IsOnGround` 1 ; SJ-17 : `HeroAirborne` vrai aux ticks 1 à 21 ;
    test de gel : racine figée 5 images, plus basse après la reprise. Les remises à 0 de `FinalForceZ` à l'atterrissage du
    porteur sont en `AlundraEntityScriptProxy.cs:801-805` et `:866-870`.
  - **D5a faite le 2026-10-03** : code `EntitySupport.FindRidingEntity` (F1 : fenêtre Y `Height + 1`, pour le héros, la passe par image et `0x3E`), `AlundraEntityScriptProxy.TickForceZ` (F2b : force du tick prise avant l'atterrissage, que le héros porté recopie en `FinalForceZ` ; `ForceZ` reste celui du porteur), `IsEntityTopFlushWithFoot` et `HeadPullFlushOnEntity` (F3 : au tirage de tête d'image, héros de confiance, `IsOnGround` 1 et entrée de l'état au repos sur l'entité), `SuspendGravityForClimb` (F4 : termine d'abord l'état, restitue les valeurs capturées). Tests : `AlundraHeroObjectTopsTests` UH-15, UH-17, UH-18 (deux cas) ; `AlundraHeroFallAndPadJumpTests` UJ-CLIMB, UH-7b réécrit, SJ-17 complété ; `AlundraLadderClimbTests` test de gel avec assertions de racine. **Rouges d'abord sur le code d'avant, valeurs lues** : UH-15 au tick 12 `PosZ` 1048576 et `RidingEntity` non nul (attendu 1015808 et nul ; la tête du tick 12 est bien à `PosY` < 84 px et celle du tick 11 à > 84 px) ; UH-17 au tick 17 animation 44 (image d'air, attendu 1) ; UH-18 au tick 1 `CollidedWithEntityZ` 0 (attendu 1) ; UJ-CLIMB l'entrée en escalade à l'image 22 (comme l'audit) puis `HeroAirborne` vrai (attendu faux). Verts d'emblée comme l'audit le prévoyait (préconditions et gardes) : UH-7b réécrit, SJ-17 complété, test de gel complété, UH-18 sans `Gravity`. **Verts après** : les valeurs écrites d'avance tiennent toutes, aucune ré-épingle. `Alundra.Tests` 2496 réussis (2491 + 5), 0 échec, en Debug ; UH-10, UH-11, UH-14, SJ-12, SJ-13 et les trois tests de sortie d'échelle inchangés et verts ; garde d'octets à 0 (les quatre traces du héros remises par `git checkout`).
  - Écarts de D5a : (1) F2b : le héros porté recopie `ForceZ` du porteur (0 après son atterrissage, comme le binaire qui ne remet que `ForceZ` à 0) et prend `TickForceZ` pour `FinalForceZ` ; (2) F3 : seulement pour le héros avec le bit `Gravity` et un tirage de confiance (verticale non tenue ailleurs) ; le drapeau est remis à 0 à l'adoption d'un pawn ; (3) F4 : l'entrée en escalade baisse aussi `HeroFlyMarked` ; (4) UH-15 épingle la tête de `PosY` par l'encadrement de 84 px aux ticks 11 et 12 plutôt que tick par tick (la valeur du tick 12 et la chute sont celles du plan) et `0x3E` rend 0 à ce tick ; (5) montages : UH-17 sur `x` 165, `y` 103,5, `z` 16, interrupteur de racine (148 ; 96) décalé de `offsetX` 44 (boîte logique x 192 à 216, y 96 à 112), sol de 16 px sauf la case (8 ; 6) à 0 ; UJ-CLIMB sur des cases de `ground_property` 12 à hauteur 0 avec un mur (hauteur 36) des rangées 0 à 4, Haut tenu avec le front de Croix à l'image 1 ; UH-7b vérifie aussi qu'aucune cible d'air (44, 45) n'est posée ; (6) l'assertion d'UH-17 « jamais d'image d'air » s'écrit sur le triplet état tenu, contact 0 et pas au sol, et le test exige aussi que l'état ait bien été entré (le moteur a perdu le sol au-dessus du creux) ; (7) UH-16, la règle d'`IsOnGround` de F2, `LastTickDeltaZ` et la fonction unique du héros aux deux sites : hors périmètre, D5b.
  - **Historique** : la relecture de clôture de l'époque précédente (`4432dae`) rendait **REVISE** : (1) F2/UH-16 : le héros,
    mis à jour avant la plateforme, monte à `dessus + v` avant elle (`IsOnGround` 0 en fin de tick, animation d'air), et en
    descente il atterrit sur elle à chaque tick (`CollidedWithEntityZ` 1, `ForceZ` 0) ; la condition de fin n'était pas définie
    en Z ; (2) F5/UH-7b : la descente de 3 px n'est pas constructible avec `FlatCells`. Les deux sont traités par la révision
    ci-dessus (F2, F5). Tests à surveiller sous F1 (relevés par la même relecture) :
    `AlundraNpcCharacterControllerMoverTests.EntitySearchService_Searches5And6_…` et les traces de l'intro par
    `UpdateRidingEntities` ; une valeur qui bouge est un arrêt.
  - Ce qui reste ouvert jusqu'à l'exécution de D5 : deux P2 introduits côté tests (UH-7b vacant, assertions de racine du test de
    gel) ; deux P2 de fidélité (fenêtre Y du passager, plateformes en Z non porteuses : D-E19-42 n'est pas tenu pour elles) ;
    le défaut probable d'escalade depuis un saut (F4) ; la pose de saut d'une image sur les dessus à fleur (F3). La chaîne de
    l'histoire n'en dépend pas (aucun `0x3E` ni plateforme en Z sur la chaîne, d'après la découverte d'E19.e).
  - ✅ **Vérification de D5a** (2026-10-03) : exécution `950b85e` ; `Alundra.Tests` 2496/2496 en Release (42 s) puis en Debug
    (43 s), la Debug en dernier ; `cmp` sans écart (sha256 `dbdccbc8...44df`) ; les six traces à l'octet. Vérificateur neuf :
    **CONFIRMED** (binaire relu : fenêtre `Height + 1` des deux côtés, `0x80036514`/`0x80036528`/`0x800365B0`/`0x800365C4` ;
    `0x80037364` écrit `ForceZ` et `FinalForceZ` du passager depuis `FinalForceZ` du porteur, `0x80037700` ne remet à 0 que
    `ForceZ` ; quatre mutations de production, chacune attrapée par son test : `Depth + 1` remis (UH-15), copie de
    `FinalForceZ` du porteur (UH-18), drapeau de F3 coupé (UH-17), bloc de F4 coupé (UJ-CLIMB) ; seule la liste fermée a
    changé ; UH-10, UH-11, UH-14, SJ-12, SJ-13 inchangés). Écarts déclarés jugés acceptables : F2b garde `ForceZ` du porteur pour
    le héros (ne se voit que si un script écrit `FinalForceZ` seul) ; F3 limité au héros à `Gravity` et tirage de confiance ; F4
    baisse aussi `HeroFlyMarked` ; UH-15 encadre la tête à 84 px aux ticks 11 et 12. Avis reportés à E19.m : P4 UH-17 n'épingle
    pas les ticks où le héros est porté ; P4 l'écart de `ForceZ` de F2b (à reprendre avec D5b : recopier `TickForceZ` dans les
    deux champs).
- ⏳ **D6 — Vérification et clôture**, comme C6. **D7 — Recette** (auteur, avec la recette C7 d'E19.d2c1) : saut sur place
  et en marchant, contrôle en l'air, saut depuis l'eau (plus bas), chute d'un rebord, saut sur un coffre puis descente, une
  plateforme mobile, la falaise de la 10 franchie seule ; une case où la Croix ne fait rien ; après un saut scripté sur la
  61, le héros revient à l'arrêt.

**Traces « spawn » prévues** (les deux fichiers, valeurs recalculées par l'auditeur des valeurs le 2026-10-02) : images 221
à 230, `posZ` 2064384, 1998848, 1900544, 1769472, 1605632, 1409024, 1179648, 917504, 622592, 294912 (`tileZ` 1 de 221 à
227, 0 ensuite), `isOnGround` 0, `targetAnim` 44 ; image 231 : `posZ` 0, `isOnGround` 1, `targetAnim` 44 ; image 232 :
`targetAnim` 1 ; `posX` 221 à 232 = 40287744, 40438272, 40588800, 40739328, 40889856, 41040384, 41190912, 41341440,
41491968, 41642496, 41793024, 41948160, puis ancien − 101376 de 233 à 267 ; toutes les autres colonnes et lignes
inchangées.

**Relecture d'E19.d2c2 (2026-10-03).**
- Plan-verifier frais et auditeur des valeurs indépendant, sur `d3da00c` : **REVISE**. Bloquants, les mêmes chez les deux :
  (1) les variantes d'UH-6 supposaient le blocage du binaire alors que le moteur garde 3 px de tolérance en l'air ;
  corrigé par S6 (`StepHeight` 0 tant que `ForceZ != 0`, la règle du binaire, sans changement moteur) ; (2) UH-7 attendait
  `IsOnGround` 0 au tick 14 ; la DLL donne 1 (tirage de tête d'image), écrit avec sa raison ; (3) la liste fermée omettait
  le test de gel en pleine chute et l'effet de S2 sur les sorties d'échelle ; S2 n'entre plus que sur un tirage fiable, le
  test de gel est réécrit et listé. P3 et P4 portés : ordre passager puis atterrissage et `LogicContextEntity` (S4), UJ-7
  sous verrou, TN-3 dans la base de D0, observable d'UJ-LOCK, note d'en-tête des traces, aides de montage pour D1 à D4.
- L'auditeur a recalculé tout le reste (UH-1 à UH-3, UH-8, SJ-5 à SJ-7, SJ-6b, traces « spawn » des images 221 à 267, UH-6
  principal, UH-10/UH-11 dont `CollidedWithEntityZ` 1 au tick 18 puis 0 de 19 à 24, SJ-12, SJ-13, UJ-LOCK) : juste.
- Relecture neuve sur `be04f27` : **READY**. Exécution lancée le 2026-10-03 sous la consigne de l'auteur du 2026-10-02 (mode
  AUTO, ni merge ni push).

**Vérification d'E19.d2c2 (2026-10-03, premier passage, avant D5).**
- Verifier frais sur `103dcb7` : **CONFIRMED** (2301/2301 en Release puis en Debug, `cmp` sans écart, 14 mutations de
  production attrapées, traces « spawn » égales à la prévision ligne à ligne, liste fermée respectée).
- Contradicteur tests : aucun P1 ; P2 introduits UH-7b vacant et assertions de racine perdues (→ F5) ; P3 entrée en escalade
  pendant l'état (→ F4), SJ-17 sans précondition (→ F5), UH-14 prouve le transport en montage seulement (recette D7) ;
  « rouge d'abord » démontré pour une partie (accepté) ; P4 (noms trompeurs de `MovePlayer_OtherAnimationId_…` et de `SJ4b`,
  ternaire redondant de SJ-12, multiple de 32768 du test d'échelle, branches sans test) → E19.m.
- Contradicteur binaire : P2 fenêtre Y du passager (→ F1) ; P2 plateformes en Z jamais porteuses (→ F2) ; P3 `CollidedWithEntityZ`
  vaut 1 au repos sur un objet **avec gravité** dans le binaire (le passager recopie `FinalForceZ` −32768), 0 dans la DLL →
  reporté à E19.h avec les lecteurs de `CollidedWithEntityZ` (la phrase de S4 ne vaut que sans gravité) ; P3 dessus à fleur
  (→ F3) ; P4 copie de `FinalForceZ` (→ F2) ; à vérifier à 144 Hz : un héros sur un dessus à fleur et la gravité temps réel du
  moteur sur une image sans tick (recette D7). Confirmé fidèle : la table de saut, la queue commune, le front de Croix, le
  refus `0x4000`, l'atterrissage sur un dessus, S6, `RidingEntity` remis à 0 à chaque tick.

**Acceptation d'E19.d2c2.**
1. Tests D1 à D3 rouges d'abord, verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. **Code de test existant touché** — liste fermée ; toute autre assertion existante reste inchangée et verte :
   - **D1** : `AlundraPlayerManagerTests.MovePlayer_OtherAnimationId` (`0x2D` + Droite → 1) ; UJ-7
     (`AlundraAnimationImpulseSpawnTests.cs`, sous `ControlLocked`) ;
   - **D1** : `AlundraSaveBookEndToEndTests` (`OneFrame` seul, montage : contrôleurs tenus à jour en tête d'image ; disposition de la session principale) ;
   - **D2** : `HeroTraceHarnessTests` (épingles de la chute : première image en l'air 221 à `posZ` 2064384, atterrissage à
     231 ; commentaires ; la note « chute » de l'en-tête des deux traces « spawn » si elle parle de la gravité du moteur) ;
     les deux traces `docs/hero-trace-389-spawn-*.txt` régénérées aux valeurs prévues ;
     `AlundraLadderClimbTests.GameplayFreeze_MidFall_HeroHoldsItsHeight_ThenFallsOnWithTheSameVelocity` (réécrit à la chute
     tenue par le tick, voir D2 ; il peut être renommé) ;
   - **D3** : aucun (nouveaux tests seulement) ;
   - **D4** : UJ-2 (`AlundraEventProgramRunnerWaitCollidedZTests.cs`, programme avec `0x25` au pc 1) ; A10J (assertions des
     pas de Giles ajoutées) ; A10 (doc du test, commentaire de Meade) ; T-C61 (commentaire du signal de fin) ; A12
     (`AlundraBergusJumpArcTests.cs` sous `using`) ; `AlundraArcSupport.cs` si le test de la garde T-REG-0 l'exige ;
   - **D5a** : UH-7b et SJ-17 (`AlundraHeroFallAndPadJumpTests.cs`), le test de gel en pleine chute
     (`AlundraLadderClimbTests.cs`, assertions de racine rendues) ; nouveaux tests UH-15, UH-17, UH-18 et UJ-CLIMB (UH-16 part
     avec F2 en D5b ; UH-10 ne bouge pas sans la règle d'`IsOnGround` de F2) ; aucune valeur
     existante de `CollidedWithEntityZ` ne change (F2b ne touche que les porteurs avec gravité, absents des montages actuels) ; tout test de `0x3E` ou de `RidingEntity` dont une valeur
     dépend de la fenêtre Y (F1) est un arrêt, pas une ré-épingle ;
   - **D1 à D5** : les montages de `AlundraJumpTestSupport.cs` et `AlundraContactTestSupport.cs` peuvent gagner des aides
     (cases à `ground_property` 0x40 pour UH-5, `LogicContextEntity` des entités, etc.) sans changer leurs comportements
     existants.
   A5, A5r, le couloir de la 392, A3 et toutes les autres épingles restent inchangés ; les traces `highground` et de l'intro
   restent à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la DLL Debug déployée en dernier, `cmp` sans écart.
4. Recette D7 faite par l'auteur.

**Risques.**
- Saut perdu sur les images sans tick si le cachet manque (une pression sur six à 60 Hz) : SJ-5b et SJ-5c le gardent.
- S6 (`StepHeight` 0 en l'air) touche le seul contrôleur du héros pendant l'état ; une sortie de l'état qui oublierait de
  rendre la valeur capturée bloquerait toute marche de 1 à 3 px : test dédié (sortie par atterrissage, par adoption d'un
  nouveau pion, par gel et reprise).
- Chute parasite sur une marche ou une rampe à trois ticks par image ou plus (4,875 px > 4 px d'aimantation du moteur) :
  invisible à un ou deux ticks par image (SJ-11) ; à surveiller en recette.
- Coincement dans un objet si l'appui manque (D-E19-36) ; héros qui flotte si `RidingEntity` est gardé d'un tick à
  l'autre.
- Le joueur franchit seul la falaise de la 10 et sort des zones `0x3B` à un seul niveau de Z (40 sur la chaîne) : comme
  l'original.
- Sites `0x2F` qui lisent la Croix hors de la chaîne (51, 440, 477, 143, 475) : la même pression sert le script et le saut,
  comme l'original.

### 1.2i E19.e — La chaîne du jour 3 et du jour 4 prouvée, et un test statique des opcodes sautés ✅ (E0 à E5 faites et CONFIRMED le 2026-10-03 ; recette E6 en attente)

**Résultat** : les scènes scriptées du jour 3 qui ne demandent pas le combat sont rejouées par un arc sur la vraie DLL (176
`B[6]`, 179 `B[3]`, 176 `B[7]`, 135, 178), en plus de celles qui le sont déjà (179 `B[2]` A12, 10 `B[20]` A10J, et au jour 4
la réunion de la 185, T-A19) ; **exclus d'E19.e** : les quatre villageois de la 185 (des dialogues que le joueur déclenche
par contact, que T-A19 remplace en posant G120 à G123 ; seule la recette E6 les vérifie, un arc A21 reste possible plus
tard), la scène d'avant le rêve 179 `B[1]` (O-E19-33) et tout ce qui demande le combat (E14) ;
les arrivées de la chaîne qui n'ont pas de vérification ne recouvrent aucune entité ; un test statique ferme la liste des
opcodes sautés atteignables sur les cartes de la chaîne, et échoue dès qu'un nouveau site apparaît ou qu'une entrée devient
périmée.

**Découverte** (2026-10-03, lecture seule, HEAD `e7674be`, parcours statique des programmes ; scripts et listes dans le
scratchpad de la session, `e19e-disc/`). Faits porteurs :
- **Preuve existante** : de la 389 à la 164 (jour 1), chaque étape a un arc ou un banc (§1.3 ; A0 à A11, A20, TH3b, TH4) ;
  au jour 3, A12 (179 `B[2]`) et A10J (10 `B[20]`) ; au jour 4, T-A19 (185 → 362). **Sans arc** : 176 `B[6]`, 179 `B[3]`,
  176 `B[7]`, 135 (`B[14]`, `B[2]`, `C[1]`), 178 (`B[1]`, `C[6]`), les quatre villageois de la 185 (T-A19 pose G120 à G123),
  et, derrière le combat (E14), le jour 2 (14, 15, 169 à 175), Tarn's Manor (115 à 117), le rêve 44 et la 362 après
  l'arrivée. La 183 n'a pas de scène (ambiance et portails).
- **Opcodes sautés** (parcours de flot de contrôle depuis chaque racine — événements B, programmes A, C, D, E, F des
  enregistrements, racines de `0x40` —, les deux branches de chaque saut conditionnel : un sur-ensemble ; contrôle : il
  contient tous les ensembles de sautés que les arcs épinglent aujourd'hui) : **198 sites** sur les 30 cartes de la chaîne
  hors cartes de combat, dont 76 déjà épinglés par un arc ; effets (E19.g) 99, caméra (E19.k) 28, texte (E19.f/E12.c) 32,
  `0x45`/`0x46` (E19.h) 6, prédicats et restes (E19.l) 33 ; **aucune attente** ; les 9 prédicats suivis d'un consommateur de
  `Result` sont optionnels (boutiques de la 163, blocs de la 10) ou déjà épinglés (`0x95` d'A10J). Cartes de combat (14, 15,
  44, 115, 116, 117, 362) : 88 sites, exemptés (E14).
- **Aucun blocage statique** de 179 `B[2]` jusqu'à l'arrivée sur la 362, après E19.d2c : chaque attente sur drapeau a son
  poseur (même programme, programme voisin, ou `<<flag>>` de Yarn) ; les arrivées par `0x53` de la chaîne tombent à la hauteur
  exacte de leur case.
- **Valeurs des scènes** : émulations du binaire du 2026-10-02 (`e19d2b-disc/scenes`), antérieures à E19.d2c ; les fins de
  scène, les drapeaux et les destinations de `0x53` sont des données exactes ; les positions de contact suivent la règle
  exacte d'E19.d2b ; le reste se re-mesure (voir les règles d'épinglage).

**Choix de conduite** (mode AUTO ; techniques, sans effet visible pour le joueur) :
- le test statique couvre **tous** les programmes des 30 cartes (198 lignes, avec une colonne de niveau : S scène, A ambiance,
  O optionnel, X hors chaîne), plutôt qu'une liste de programmes à entretenir à la main ;
- `0x52`, `0x8C`, `0xB9` sont rattachés à E19.l, `0x86` à E14 (ils n'avaient pas de tranche) ;
- la scène d'avant le rêve (179 `B[1]`) et le rêve 44 restent hors d'E19.e : le rêve demande le combat, et la scène d'avant
  n'est atteignable qu'après le jour 2 (combat). **Question à l'auteur, O-E19-33** : veut-il un préréglage `day3-start`
  (179, case (12,22), z1, G203, G1660, table [162] = 176) et un arc de 179 `B[1]` ?
- les recettes en attente d'E16.a (T7) restent hors d'E19.e : elles visent des lieux hors chaîne (321, 411, 55 à 60).

**Règles d'épinglage des arcs** : exacts, les fins de scène (instruction), les drapeaux posés et effacés (avec leur pc), les
destinations de `0x53` (données), les relations de contact (règle semi-ouverte d'E19.d2b) ; à ± 2,5 px, les positions finales
de marche issues de l'émulation (le binaire glisse le long des angles, la DLL non : E19.h) ; les images absolues ne sont pas
épinglées, seulement l'ordre des événements et la limite d'images ; l'ensemble des sautés de chaque arc doit être inclus dans
la liste statique (contrôle croisé). Une valeur exacte contredite est un arrêt ; une position hors de ± 2,5 px aussi.

**Tâches.**

- ✅ **E0 — Plan.** Ce plan, relu jusqu'à READY.
- ✅ **E1 — Test statique des opcodes sautés de la chaîne.**
  - **Cartes** : les 30 cartes de la chaîne, 389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 172, 169, 170, 171,
    173, 174, 175, 10, 179, 176, 177, 178, 180, 181, 182, 135, 183, 184, 185 (dix n'ont aucun site sauté : 170, 171, 173,
    175, 177, 184, 389, 390, 416, 478) ; les 7 cartes de combat exemptées, 14, 15, 44, 115, 116, 117, 362. Toutes deux
    écrites dans le test.
  - **Liste fermée versionnée** : `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` (nom indicatif), **copie à l'identique**
    des 198 lignes de `exceptions-core.tsv` de la découverte (scratchpad de la session,
    `e19e-disc/tools/exceptions-core.tsv`, construit à `e7674be` ; recalculé ligne à ligne par l'auditeur indépendant du
    2026-10-03 avec son propre parcours) ; colonnes gardées : carte, créneau, index de programme, pc, opcode, classe,
    tranche, niveau (S scène, A ambiance, O optionnel, X hors chaîne). La clé d'une ligne est (carte, créneau, programme,
    pc) : deux pc servent deux programmes (164 `@110`, `C[4]` et `C[5]` ; 476 `@112`, `B[4]` et `B[5]`). Le niveau est une
    donnée de la liste (135 `C[1]` reste O : son `0x58 @1008` est sur le chemin d'après la scène). Si le parcours en C#
    ne redonne pas exactement ces 198 lignes sur le code d'E19.d2c2 (`103dcb7`), c'est un **arrêt**.
  - **Oracle « porté »** : le vrai runner, pas un miroir écrit à la main : pour chaque opcode de taille connue non nulle,
    un programme `[op, 0…, 0xFF]` sur un hôte factice (comme `AlundraEventProgramRunnerTests`) ; sauté si la trace rend
    `UnknownSkipped`.
  - **Parcours** (port en C# de celui de la découverte, vérifié par l'auditeur contre `Dispatch`) : depuis les événements B
    de la carte et les index A, C, D, E, F de ses enregistrements (`index & 0x7F`), plus les racines de `0x40` ; sauts suivis
    `0x02` ; `0x03`, `0x04` (les deux branches) ; `0x30`, `0x31` (décalage en v3/v4) ; `0x74` (v1/v2) ; `0x78` (+3, marque),
    `0x79` (+3 conditionnel), `0x7B`, `0x7C` (+5), `0x7D` (retour), `0x7E`, `0x7F` (taille 1), `0x80`, `0x81` (taille 3)
    (`0x7A` n'est pas porté : sauté par sa taille 3) ; `0x49`, `0x4B` (retour à l'entrée du programme) ; `0x00` taille 1,
    le parcours continue ; arrêt sur `0xFF`, une taille nulle ou inconnue. Les tables F des cartes 177 et 184 ont 16 entrées
    qui chevauchent le code : ce sont de vrais programmes, le chargeur ne doit pas les rejeter.
  - **Règles** (chacune une fonction testée à part sur des entrées fabriquées, puis appliquée au corpus) :
    1. tout site atteint et sauté est une ligne de la liste ;
    2. toute ligne de la liste est un site atteint et sauté (une ligne périmée échoue : opcode devenu porté ou site devenu
       inatteignable) ;
    3. aucun opcode d'attente **sauté** (`0x20` à `0x23`, `0x26`, `0x47`, `0x48`, `0x5F`, `0x9F`) ni opcode de taille nulle
       ou inconnue n'est atteint sur les 30 cartes, à aucun niveau (l'auditeur n'en trouve aucun) ;
    4. parmi les lignes de niveau S, les opcodes de prédicat ou de branche sautés, l'ensemble fermé {`0x52`, `0x58`,
       `0x82`, `0x84`, `0x87`, `0x95`, `0x99`, `0x9A`} (ceux qui écrivent `Result` ou sautent), n'apparaissent qu'aux quatre
       sites connus : `0x58 @110` de 164 `C[4]` et `C[5]`, `0x84 @183` de 179 `B[1]`, `0x95 @6418` de 10 `C[75]` ;
    5. **seulement pour les nouveaux arcs A13, A14, A15, A17 et A18, dans leurs propres tests** : toute entrée de leur trace
       de type `UnknownSkipped` (les types `LoopBudgetExceeded` et `UnknownNoSizeTerminated` sont des échecs à part), sur la
       carte de l'arc (chaque arc s'arrête à son `0x53`, avant de changer de carte), ramenée à la clé (carte, créneau, index
       de programme retrouvé par le début de programme, pc), est une ligne de la liste. Les arcs existants ne sont pas
       retouchés.
  - **Rouge d'abord, une mutation par règle** (tests permanents des fonctions de règle sur des entrées fabriquées, plus un
    essai jetable sur le corpus) : (1) une ligne retirée de la liste ; (2) une ligne ajoutée pour un site d'opcode porté
    (`0x0B`) ; (3) un programme fabriqué qui contient `0x20` ; (4) la ligne 163 `0x82 @297` passée du niveau O au niveau
    S ; (5) dans A13, la ligne de l'un de ses sites sautés retirée d'une copie de la liste. Chacune fait échouer sa règle,
    et elle seule.
  - Commit : `test(alundra): close the list of the skipped opcodes reachable on the story chain`
  - Fait le 2026-10-03 : liste `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` (copie à l'octet des 198 lignes de la découverte, CRLF), helpers `AlundraStoryChainOpcodeAudit.cs` (oracle par le vrai runner, parcours porté de `cfg.py`, règles 1 à 5 en fonctions) et `AlundraStoryChainSkippedOpcodesTests.cs` (20 tests : 6 sur le corpus, dont une par règle 1 à 4, 1 sur l'oracle, 6 sur le parcours, 7 sur les fonctions de règle dont celle de la règle 5 sur une trace fabriquée, sans arc). Aucun fichier de `Alundra/` touché. Le parcours C# redonne exactement les 198 lignes du premier coup (règles 1 et 2 vertes, aucune ré-épingle) ; les tests sont donc verts d'emblée, la preuve qu'ils discriminent est dans les mutations. Mutations, chacune ne fait échouer que sa règle : (1) la ligne 476 `B[2]` `@326` retirée de la liste, seul `Rule1_...` rouge ; (2) une ligne `0x0B` ajoutée pour 179 `B[1]` `@114`, seul `Rule2_...` rouge ; (4) 163 `B[2]` `@297` `0x82` de O à S, seul `Rule4_...` rouge ; (3) la fonction de la règle 3 privée de sa clause d'attente, les deux tests de règle 3 rouges (programme fabriqué avec `0x20`, et chaque opcode d'attente) ; (5) la fonction de la règle 5 privée de son filtre `UnknownSkipped`, seul le test de règle 5 rouge ; la mutation (5) du plan sur A13 n'existe pas encore (A13 vient en E2) : elle est faite sur l'entrée fabriquée. Fichiers rendus à l'octet après chaque mutation. Écarts au plan : le test du corpus s'exécute sur toute la liste (pas de sous-ensemble) ; la liste est trouvée en remontant les dossiers depuis les binaires (pas de copie dans `bin`) ; un export absent fait ÉCHOUER le test (convention des arcs). `Alundra.Tests` 2321 réussis (2301 + 20), 0 échec, en Debug ; garde d'octets à 0 (les quatre traces du héros remises par `git checkout`).
- ✅ **E2 — Arcs du jour 3 : A13, A14, A15.**
  - **A13** (176 `B[6]` @468) : arrivée du `0x53 @451` de la 179 en (11796480 ; 36175872 ; 10485760), drapeaux {G203,
    G1651, G1652, G1660} ; fin : `0x53 @533` vers la 179 en (27525120 ; 7864320 ; 1048576), G1653 posé à `@530` ; Giles
    `0x24 @526` finit près de (494 ; 552) px (± 2,5 px).
  - **A14** (179 `B[3]` @464) : arrivée du `0x53 @533` de la 176 ; drapeaux {G203, G1651, G1652, G1653, G1660} ; fin :
    `0x53 @568` vers la 176 en (8650752 ; 35127296 ; 10485760), G1654 posé à `@565` ; T800 et T801 posés à `@528`/`@542`,
    effacés par Nestus à `@1149`/`@1160` ; héros `0x24 @564` au contact de Septimus (relation exacte de la règle d'E19.d2b),
    près de (27525120 ; 21757952) (± 2,5 px).
  - **A15** (176 `B[7]` @544) : arrivée du `0x53 @568` de la 179 ; drapeaux {G203, G1651 à G1654, G1660} ; fin : `0x53 @600`
    vers la 10 en (16515072 ; 61341696 ; 0) ; drapeaux temporaires posés : T0 par `B[7]` `@561`, T2 par Septimus (rec5
    `C[1]`) `@812`, T1 et T3 par Giles (rec6 `C[2]`) `@850` et `@860` ; attentes satisfaites : Septimus attend T0 `@776` et
    T1 `@792`, Giles T2 `@853`, `B[7]` T3 `@588` ; aucun programme ne les efface (ils tombent tous à l'arrivée sur la 10,
    `InstallForMapEntry`) ; héros `@599` près de (266 ; 231) px (± 2,5 px ; il passe à 0,625 px de Giles : une
    troncature d'O-E19-29 sur un PNJ peut faire basculer cette marge, ce serait un arrêt).
  - Commit : `test(alundra): arcs of the day-3 scenes of maps 176 and 179`
  - Fait le 2026-10-03 : fichier neuf `Alundra.Tests/AlundraDay3SceneArcTests.cs` (CRLF, comme ses voisins), 3 tests (A13, A14, A15) sur la vraie DLL, vrais préfabs, vrai héros, arrivée réelle (`ArcArrival`) ; la règle 5 est branchée dans chacun (`AssertSkippedAreListed` : les `UnknownSkipped` de la carte de l'arc sont des lignes de la liste, et aucun `LoopBudgetExceeded` ni `UnknownNoSizeTerminated`). Aucun fichier de `Alundra/` ni de test existant touché. Tests verts d'emblée (le code de production est celui d'E19.d2c2, les arcs sont de la preuve neuve, pas un correctif) ; aucune valeur du plan n'a été contredite, aucune ré-épingle. Valeurs mesurées : A13 fin à l'image 343, Giles `@526` en (494 ; 552) exactement, 10 sites sautés (les dix `0xA2` de `B[10]`, niveau A) ; A14 fin à 257, aucun saut, T800 posé à 106 puis effacé par Nestus à 107, T801 posé à 118 puis effacé à 121, le héros en (420 ; 332) exactement contre Septimus (y + 8 = y de Septimus - 6) ; A15 fin à 663, hero en (266,0625 ; 231), T0 à T3 posés dans l'ordre, aucun effacé, 11 sauts (dix `0xA2` de `B[10]` et `0xA2 @815` de Septimus), `TotalEntityBlockCount` 0 (le garde-fou du risque O-E19-29 sur le passage à 0,625 px de Giles : aucun pas raccourci par une entité). Écarts : (1) les directions des arrivées des arcs sont celles des opérandes des `0x53` mesurés (A13 24, A14 0, A15 0), le plan ne les donnant pas ; (2) A15 épingle en plus `TotalEntityBlockCount == 0` ; (3) les attentes sont épinglées par l'ordre des instructions (pc d'écriture avant le pc qui suit l'attente), jamais par une image absolue. Mutation jetable (5), sur la liste (la ligne 176 `B[10]` `@676` `0xA2` retirée du fichier par un script, fichier rendu à l'octet après) : A13 rouge (`0xA2 @676 (slot 1, program @676)` hors liste), A15 rouge aussi (même programme), A14 vert (autre carte), et `Rule1_...` rouge. `Alundra.Tests` 2324 réussis (2321 + 3), 0 échec, en Debug ; garde d'octets à 0 (les quatre traces du héros remises par `git checkout`).
- ✅ **E3 — Arcs de la 135 et de la 178 : A17, A18.**
  - **A17** (135 `B[14]` @912, `B[2]` @216, `C[1]` @960, et `B[1]` @136 qui active Ronan rec0 sous G203 et fait apparaître
    Giles rec28 par `0x8A @159`) : arrivée du `0x53 @2463` de la 10 en (30670848 ; 54001664 ;
    1048576), drapeaux {G203, G1654} ; fin : `0x11 @1115` (`C[1]`), G14 posé à `@1120`, G1655 posé à `@953` (T1 avant G1655) ;
    le choix de la boîte 129 accepté (T0 posé par Yarn) ; héros `0x24 @942` au contact de Ronan ; blocs rec8 à rec10 détruits.
  - **A18** (178 `B[1]` @104, `C[6]` @512) : arrivée par le portail 176.7 en (60555264 ; 26738688 ; 0), direction 16,
    drapeaux {G203, G1654, G1655} ; deux phases (fin de la première à `0x11 @543`, puis `PlaceHero(948, 232, 16)` : une entrée choisie
    dans la boîte du `0x3B @576 [39,40,13,15,1,1]`, case (39,14) de hauteur 1, non une mesure) ; le z 0 de l'arrivée vient
    de l'enregistrement du portail et `ClampToGround` le remonte : z n'est pas épinglé après l'adoption ; fin : `0x11 @724`, G204 posé à `@721`, G203 effacé à `@718`, `0x38 @708` et `@713` (tables [176] et
    [162] = 183) ; `@694` et `@697` (marge nulle) non épinglés.
  - Commit : `test(alundra): arcs of the day-3 scenes of maps 135 and 178`
  - Fait le 2026-10-03 : A17 et A18 ajoutés à `Alundra.Tests/AlundraDay3SceneArcTests.cs` (2 tests, 5 en tout dans le fichier ; seuls les en-têtes du fichier d'E2 changent), règle 5 branchée dans chacun. Rouge d'abord : A17, lancé avec la boucle des arcs existants (Carré sur chaque image de boîte), reste bloqué sur `0x44 @1056` jusqu'à sa limite (le choix OUI/NON n'est jamais répondu) ; il est donc écrit avec une boucle propre au test, qui répond la première option (`SelectChoiceForTests(0)`) et appuie sur Carré sinon, sans toucher `ArcRun`. Aucune valeur du plan contredite. Valeurs mesurées : A17 `0x24 @942` (B[14]) à l'image 310 puis T1 `@945` à 324, T0 posé par Yarn pendant la boîte (images 326 à 328), `0x44 @1056` à 328, G1655 `@953` à 329, `0x11 @1115` à 700 ; le héros en (447 ; 358) contre Ronan en (468 ; 360), relation exacte x héros + 11 = x Ronan - 10 (le plan ne donne pas le côté : arête est du héros, à x + 11 comme dans les contacts de T-C61, contre l'arête ouest de Ronan, à x - 10, valeur déduite de la mesure) ; rec28 (Giles) présent par `0x8A @159`, Ronan actif, rec8 à rec10 apparus puis `FlagToDestroy` ; un seul saut, `0xA0 @536` (135 `B[5]`, niveau A). A18 : phase 1 finie par `0x11 @543` à l'image 191, `PlaceHero(948, 232, 16)`, phase 2 par `0x11 @724` à 1068 avec `0x38 @708`, `@713` (les tables [176] et [162] passent de l'identité à 183), `0x06 @718` (G203 effacé), `0x05 @721` (G204 posé) ; sauts `0x46 @105`, `0x45 @129`, `0x46 @134`, `0x45 @157`, `0x8E @608`, `@618`, `0x8F @683` : tous des lignes de la liste. Écarts : (1) A17 épingle le côté et les décalages du contact (relation exacte déduite de la mesure, cohérente avec les boîtes déjà épinglées par T-A19) ; (2) A17 passe en deux boucles (fin `0x11 @1115`, puis `0x05 @1120` G14) pour garder le signal de fin nommé par le plan avant le drapeau ; (3) A17 a besoin d'un nom de zone `Church` / `Church-135`, A18 de `Inoa (inner)-178`, A13 et A15 de `Inoa-176` (les noms du plan sont ceux des cartes, pas des dossiers). `Alundra.Tests` 2326 réussis (2324 + 2), 0 échec, en Debug ; garde d'octets à 0 (les quatre traces du héros remises par `git checkout`).
- ✅ **E4 — Arrivées.** TH4 (`AlundraEntityContactArcTests.cs`) gagne une ligne par arrivée de la chaîne qui n'en a pas : à
  la première image après l'adoption, la règle du binaire ne trouve aucune entité qui recouvre le héros. Départs (toutes les
  valeurs tracées aux octets par l'auditeur du 2026-10-03) :
  - 179 → 176 : carte 176, `0x53 @451` de la 179, (11796480 ; 36175872 ; 10485760), drapeaux {G203, G1651, G1652, G1660} ;
  - 176 → 179 : carte 179, `0x53 @533` de la 176, (27525120 ; 7864320 ; 1048576), drapeaux {G203, G1651, G1652, G1653, G1660} ;
  - 179 → 176 : carte 176, `0x53 @568` de la 179, (8650752 ; 35127296 ; 10485760), drapeaux {G203, G1651 à G1654, G1660} ;
  - 176 → 10 : carte 10, `0x53 @600` de la 176, (16515072 ; 61341696 ; 0), drapeaux {G1654, G203} (ceux d'A10J) ;
  - 10 → 135 : carte 135, `0x53 @2463` de la 10, (30670848 ; 54001664 ; 1048576), drapeaux {G203, G1654} ;
  - 135 → 10 : carte 10, portail 135.0 (case (19,52) de la 135 vers la case (30,48) de la 10, z 0), (47972352 ; 50855936 ;
    0), drapeaux {G203, G1654, G1655, G14} ;
  - 176 → 178 : carte 178, portail 176.7 (case (2,22) vers (38,25), z 0, direction 16), (60555264 ; 26738688 ; 0), drapeaux
    {G203, G1654, G1655}.
  Les arrivées sur la 183 (portails 0, 1, 2 de la 178) n'ont pas de scène : elles restent à la recette E6.
  Commit : `test(alundra): no overlap at the arrivals of the day-3 chain`
  - Fait le 2026-10-03 : sept lignes `InlineData` et sept entrées de `ArrivalSpec` dans TH4 (`AlundraEntityContactArcTests.cs`, le seul code de test existant touché ; aucune autre ligne ni aucun autre test modifiés), avec les départs du plan à l'octet : A13, A14, A15, A10J, A17, `135-to-10`, A18. Verts d'emblée : aucune des sept arrivées ne recouvre une entité (la règle du binaire ne trouve aucun obstacle à la première image). Preuve que la ligne discrimine : l'arrivée d'A17 déplacée à la position de Ronan (468 ; 360 ; z 64) fait échouer la seule ligne A17 (copie de sauvegarde, fichier rendu à l'octet ensuite). Écarts : les directions des arrivées ne sont pas dans le plan : les `0x53` donnent 24 (176 depuis `@451`), 0 (179 depuis `@533`, 176 depuis `@568`) et 16 (135, mesuré par A10J) ; le portail 135.0 prend 0 et le 176.7 prend 16 (celle du plan) ; la boîte du héros ne tourne pas, la direction ne change pas le recouvrement. `Alundra.Tests` 2333 réussis (2326 + 7), 0 échec, en Debug ; garde d'octets à 0 (les quatre traces du héros remises par `git checkout`).
- ✅ **E5 — Vérification et clôture**, comme C6 d'E19.d2c1.
- 🧪 **E6 — Recette de bout en bout** (auteur) : nouvelle partie jusqu'au livre de la 163, sauvegarde, rechargement ; F9 sur
  `day3-after-dream` jusqu'au retour libre dans la 10, puis la 178 et la 183 à la main ; F9 sur `day4-meeting`, parler aux
  quatre villageois de la 185, jusqu'à l'arrivée sur la 362.

**Relecture d'E19.e (2026-10-03).**
- Plan-verifier frais et auditeur des valeurs indépendant, sur `ace3aa7` : **REVISE**. Bloquants : la liste des 30 cartes
  et la source des 198 lignes n'étaient pas écrites ; les règles 3 à 5 du test statique n'étaient ni définies ni éprouvées ;
  les nouvelles lignes de TH4 n'avaient pas de départ ; les drapeaux T0 à T3 d'A15 étaient dits effacés par des programmes
  (aucun ne le fait). Corrigés : E1 réécrit (cartes, source, clé, ensemble fermé de la règle 4, règle 5 limitée aux nouveaux
  arcs, une mutation par règle), A15, A17 (`B[1]`), A18 (`PlaceHero` avec z, z de l'arrivée non épinglé), E4 (sept départs
  tracés, la 183 laissée à la recette).
- L'auditeur a recalculé la liste (198 lignes, ligne à ligne), les comptes par opcode, l'absence d'attente, les quatre sites
  S connus, et toutes les valeurs exactes d'A13, A14, A17 et A18 : justes.

- Relecture neuve sur `535cf7b` : **REVISE** (second). Bloquants et dispositions de la session principale : (1) le résultat
  disait « chaque scène du jour 3 et du jour 4 » alors que les villageois de la 185 n'ont pas d'arc : **FIX par réduction**,
  le résultat et la ligne du §1.1 les excluent nommément (recette E6, A21 possible plus tard) ; (2) le recensement, hérité
  d'E19.d2c1, des `0x25` des programmes B reciblés par `0x42`/`0x43` (une entité qui n'atterrit jamais calerait) n'était ni
  fait ni reporté : **DEFER à E19.h**, propriétaire des attentes en Z et de l'état en l'air des PNJ. Raison : les `0x25`
  du chemin critique de la chaîne sont tous exercés par un arc qui va au bout (179 `@415` A12, 10 `@2455` et `@6393` A10J,
  165 `@838` et `@843` A10 ; 10 `@4741` et `@5545` T-A10v hors chaîne) ; les autres sites recensés de la chaîne (165 `@865`
  et `@1166`, 172 `@610`, 174 `@203`, 179 `@1114`, 181 `@207` ; 135 `@2483` hors chaîne) sont dans des programmes
  d'ambiance ou optionnels, et la recette E6 parcourt le jeu libre de la 179. Nouvelle époque : une seule relecture de
  clôture ; un nouveau REVISE met E19.e en pause.
- Relecture de clôture sur `8e5f4cb` : **READY**. Exécution lancée le 2026-10-03 (mode AUTO, ni merge ni push).

**Vérification d'E19.e (2026-10-03).**
- **Commits** : E1 `22e5205`, E2 `2b0beeb`, E3 `c65b5dc`, E4 `7d6bf2d` (tests seulement ; aucun fichier de `Alundra/` ni du
  moteur). Signature `Claude Sonnet 5.5` imposée par l'outil des exécutants.
- **Verifier frais : CONFIRMED** sur les critères 1 à 4 : liste versionnée identique octet pour octet à celle de la
  découverte (fins de ligne mises à part) ; 30 + 7 cartes conformes ; mutations des règles 1, 2, 4 et 5 rejouées (chacune ne
  fait échouer que sa règle ; celle de la règle 5, faite sur la liste partagée, fait aussi échouer la règle 1, ce qui est
  inhérent) ; les cinq arcs aux valeurs exactes et dans leurs tolérances ; le choix d'A17 réellement présenté (une mutation
  qui répond NON fait échouer l'arc à sa limite) ; seul TH4 touché parmi les tests existants ; `Alundra.Tests` 2333/2333 en
  Release puis en Debug, `cmp` sans écart, traces identiques.
- **Dispositions** : A17 n'affirme pas lui-même que la boîte de choix s'est ouverte (en mode dégradé `0x44` rendrait 1 sans
  choix) : P4, reporté à E19.m (une assertion permanente) ; direction d'arrivée du portail 135.0 choisie à 0 par l'exécutant
  (sans effet sur le recouvrement) : P4, à lire dans une trace de sortie réelle si besoin.

**Acceptation d'E19.e.**
1. Le test statique est vert avec exactement la liste fermée, et rouge sur chacune des mutations d'E1.
2. A13, A14, A15, A17, A18 vont au bout avec les valeurs exactes écrites et les positions dans leurs tolérances ; leurs
   sautés sont dans la liste statique ; aucune valeur contredite.
3. Code de test existant touché : TH4 (`AlundraEntityContactArcTests.cs`, nouvelles lignes d'arrivée de E4) ; rien d'autre
   (les nouveaux tests et la liste versionnée sont des fichiers neufs ; les arcs existants ne sont pas retouchés).
4. `Alundra.Tests` sans échec en Release puis en Debug, la DLL Debug déployée en dernier, `cmp` sans écart.
5. Recette E6 faite par l'auteur.

**Risques.**
- E19.e s'exécute sur le code d'E19.d2c2 tel que vérifié (`103dcb7`), D5 en pause : si D5 est repris, ses correctifs
  (fenêtre du passager, plateformes en Z, dessus à fleur, escalade depuis un saut) doivent garder les arcs d'E19.e verts,
  sinon c'est un arrêt de D5. Aucune scène de la chaîne n'utilise `0x3E` ni une plateforme en Z (découverte).
- Les valeurs des scènes viennent d'émulations d'avant E19.d2c (sauts, contacts en Z) : des arrêts sont possibles ; chacun se
  tranche par une mesure et une cause, jamais par une ré-épingle sans cause.
- Contacts à 0 px (176, 178, 179) et marge nulle de `@697` sur la 178 : sensibles à toute différence d'arrondi.
- O-E19-29 (troncature au pixel à l'atterrissage d'un PNJ) n'est pas vérifiée sur 176, 178, 179 et 135.
- Les drapeaux posés par Yarn pendant une boîte (T0 de la 135, après la page 0 du nœud `M135_S001` ; T900 de la 179 ;
  T200/T201 de la 164) dépendent du dialogue actuel ; E19.f (boîte fidèle) devra les garder.

### 1.2j E19.f avec E12.c — La boîte de dialogue fidèle ⏳ (questions tranchées le 2026-10-03 : D-E19-48 à D-E19-50 ; programme f0 à f4 ; E19.f0 planifiée)

**Découverte** (2026-10-03, lecture seule, deux surfaces : le binaire, puis la DLL, le moteur et les données ; notes et
scripts dans le scratchpad de la session, `e19f-disc/binaire/notes.md` et `e19f-disc/dll/notes.md`, aperçu des cadres
`e19f-disc/dll/boxes_preview.png`). Faits porteurs **[binaire]** :
- **Boîte de texte** : toujours en bas, (16,168), 288 × 56 px (36 × 7 cellules de 8 px, configuration `0x8009CFBC`) ;
  glissement d'ouverture y 240 → 168 et de fermeture 168 → 240, 15 pas tronqués plus 2 images de calage
  (`0x80047DD0`) ; cadre `wind.tx` palette `WIND.CL` #0, opaque (les 252 cellules de l'analyseur sont exactes) ; texte
  font3, palette `WIND.CL` #8, x = 32 (centré par `\H` : 16 + (288 − l) / 2), lignes y = 173, 189, 205 (pas de 16, la
  décompilation dit 15 à tort), **3 lignes visibles**, découpe (32,172) 258 × 50, aucun retour à la ligne automatique.
- **Cadence** : un glyphe toutes les 4 images (le premier aussitôt), un par image Carré tenu, `\T` → 8 images ; codes
  sans coût de tick et codes à un tick listés dans les notes ; un appui naissant pendant la frappe est ignoré.
- **Défilement ligne par ligne, jamais de page entière** : `\N` sur la 3e ligne arme un défilement de 8 images (2 px par
  image) après 10 images ou un appui naissant ; `\A` affiche le curseur et attend un appui, puis passe à la ligne
  (défilement immédiat en 3e ligne) ; `\N\A` (1374 fois) laisse une ligne vide.
- **Curseur d'attente** 16 × 16 en (288,200), images u B0/C0/D0/E0, v 0x38, 10 images par image ; aucun curseur en fin de
  texte (22 612 chaînes sur 24 431).
- **Voix** : `\B` aucune (défaut), `\C` à `\F` voix 0 à 3 → sfx 79 à 82, `\G` muette ; un blip sur un caractère normal de
  rang pair, la parité repartant à chaque ligne ; **`\C` est la voix 0, pas un centrage** (le plan E12 §1.5 se trompe ;
  le centrage est `\H`). Sfx 6 à l'ouverture, 7 au déclenchement de la fermeture.
- **Fermeture** évaluée seulement une fois la frappe finie : appui naissant (bit 2), minuterie 360 **armée à la fin du
  texte** (bit 1), demande `0x51` (bit 4) ; le drapeau d'activité, `MessageBox` et `MenuOpen` ne tombent qu'après le
  glissement de sortie (17 à 18 images plus tard) : aujourd'hui la DLL ferme tout d'un coup, `0x39` rend la main
  environ 15 ticks trop tôt.
- **`0x4C [v]`** pose les drapeaux de texte (bit 1 vitesse au bouton, bit 2 cadence à 4 images, bit 4 frappe pilotée par
  `0x4D`) ; **`0x4D`** donne un glyphe si le bit 4 est posé ; **`0x4E`/`0x4F`** (mode de défilement, défilement) existent,
  0 site ; table partagée `map_alundra` par le bit 7 clair (déjà exportée par E15).
- **Nom** : chaîne ETC d'index = champ +0x68 de l'entité (0x100 à 0x1FF), boîte (64,140) 112 × 32 (cadre absent de
  `UiBoxes.csv`, cellules déjà dans l'atlas `wind`), texte centré y = 148, glissement x 320 ↔ 64 ; ouvert dans le même
  tick que l'opcode, fermé avec la boîte.
- **Portrait** : bit 7 de l'octet 0x12 de l'en-tête du sprite (`HasPortrait`, 25 enregistrements, 331 dans le corpus
  par carte) ; la **première image** du sprite (`FramesPointer` entrée 0), 48 × 56 en (8,116), vol de 15 pas depuis la
  position écran de l'entité, couleur 255 → 128 ; 23 portraits font 48 × 56, **deux font 48 × 72** (enregistrements 122 et
  162) et sont écrasés à 48 × 56 ; la région est vide dans les planches exportées pour 24 sur 25 : **changement
  d'extracteur et ré-extraction** nécessaires (précédent : le portrait de l'inventaire, ADR-0005).
- **Choix** : **deux options seulement** (aucun 3 ou 4+ dans le binaire) ; boîte (176,144) 128 × 32, libellés ≤ 6 octets
  en (192,152) et (240,152), curseur animé au-dessus ; gauche/droite en appui répété, sfx 1 au déplacement ; **validation
  à la Croix (bit 0x40)**, sfx 5 puis 2 ou 3 ; sfx 4 à l'ouverture.
- **DLL d'aujourd'hui** : boîte du moteur (`DialogueScreen`) sans cadre ni échelle 320 × 240, page affichée d'un bloc,
  aucun son, `0x4C`/`0x4D` non portés (32 lignes de la liste fermée d'E19.e), voix et centrage ignorés, minuterie depuis
  l'ouverture, directeur hors de la boucle `padTick` (avis A1 d'E12.a). La pile UI suffit (écrans XAML liés à un view
  model comme `AlundraSaveScreen`, `TextProgress` de MGUI pour la révélation, portrait volant déjà fait pour
  l'inventaire) ; aucun manque moteur bloquant ; un candidat de rapport (`TextProgress` est une fraction).

**Questions à l'auteur** (elles changent le travail ; la tranche attend) :
- **Q-F1 — Drapeaux posés pendant le texte.** Sur 932 codes de drapeau du texte, 87 sont avant tout caractère, 647 en fin
  de page et **198 au milieu du texte** ; l'export Yarn les place tous avant la ligne. Avec une vraie machine à écrire, le
  marin 12 casse (son `\999` est en fin de texte ; le choix s'ouvrirait pendant la frappe) et la scène de la 164 aussi.
  Voies : (A) marqueurs de position dans le Yarn (exact ; changement de format, de l'émetteur et de la preuve
  d'équivalence du corpus, ADR) ; (B) le directeur pose les drapeaux d'une page à la fin de sa frappe (exact pour 734, en
  retard pour les 198 du milieu) ; (C) tel quel (faux). **Recommandation : A.**
- **Q-F2 — Portraits 48 × 72** (enregistrements 122 et 162) : l'original les dessine écrasés à 48 × 56. Corriger (règle du
  2026-09-25 : un défaut avéré de l'original se corrige) ou reproduire ? **Recommandation : corriger** (afficher 48 × 72 à
  la même place, bas aligné), si l'auteur juge que c'est un défaut.
- **Q-F3 — Boîte de choix fidèle** (deux options côte à côte, validée à la Croix, sons) : elle remplace aussi la liste
  verticale du livre de sauvegarde (E16.e, validé en jeu), qui passe par le même mécanisme. La faire dans E19.f, pour les
  dialogues et le livre ? **Recommandation : oui**, comme l'original.

**Choix de conduite déjà pris** (techniques) : la boîte d'Alundra devient un écran XAML lié à un view model (règle de
l'auteur sur l'UI) au patron d'`AlundraSaveScreen` ; le `DialogueScreen` du moteur reste dans le moteur, Alundra cesse de
l'utiliser ; l'élément « 4+ choix » d'E12.c disparaît (le binaire n'en a pas) ; la machine du portrait volant de
l'inventaire est généralisée, pas dupliquée ; la passe du dialogue va dans la boucle `padTick` (correction d'A1).

**Programme d'E19.f** (2026-10-03, après D-E19-48 à D-E19-50 ; cartographie en lecture seule, trois surfaces, notes dans le
scratchpad de la session : `e19f-plan2/yarn/notes.md`, `e19f-plan2/dialog/notes.md`, `e19f-plan2/extract/notes.md`). Chaque
sous-tranche a son plan relu, son exécution et sa vérification :
- **E19.f0 — Drapeaux du texte à leur position** (convertisseur et DLL, comportement inchangé) : détaillée en §1.2j.1.
- **E19.f1 — Cadres et curseur** (convertisseur) : cadres du nom (14 × 4 cellules) et du choix (16 × 4) cuits comme celui du
  texte, animation du curseur d'attente (wind_150, 173, 201, 228, 10 ticks par image). Les deux lignes manquent à `UiBoxes.csv`
  de l'analyseur, dont le producteur est introuvable (fichier de données) : à établir au plan.
- **E19.f2 — Boîte fidèle** (DLL) : écran XAML lié à un view model au patron d'`AlundraSaveScreen` ; machine d'état du binaire
  (glissements d'entrée et de sortie, frappe à 4 images, Carré tenu, `\T`, `\Y`, `0x4C`/`0x4D`, 3 lignes et défilement ligne à
  ligne, curseur, voix et parité, centrage `\H`, minuterie armée en fin de texte, `0x51` en verrou, sons 6, 7, 79 à 82) ; passe
  dans la boucle `padTick` sur `TickPad` (avis A1 d'E12.a) ; les drapeaux tirés à leur glyphe (fin de D-E12-4). Grosse vague de
  ré-épingles : chaque boîte gagne son glissement et sa frappe avant toute fermeture (391 : environ 324, 472 et 376 ticks), les
  arcs qui appuient sur Carré à chaque image (`AlundraArcSupport.cs:184-187`) doivent relâcher. Reste à établir dans le binaire :
  le coût en ticks de chaque code, le défilement et le curseur au tick près.
- **E19.f3 — Choix fidèle** (DLL) : la boîte à deux options du binaire, ouvreur unique `0x80050BA8` (`0x44`, l'écran de
  sauvegarde, le livre de sauvegarde, trois appelants non identifiés), validation à la Croix, sons 4, 1, 5, 2 et 3 ; un crochet de
  test remplace `SelectChoiceForTests` (23 sites).
- **E19.f4 — Nom et portrait** (DLL, après la ré-extraction de G0, D-E19-51) : boîte de nom (ETC 0x100 à 0x1FF ; le champ de la
  DLL qui porte `+0x68` est à établir), portrait du locuteur (vol de 15 pas de l'inventaire généralisé : repos (8,116), taille par
  portrait, 48 × 72 bas aligné, D-E19-49), rampe de couleur 255 → 128 que MGUI ne sait pas rendre (il ne fait que multiplier :
  rapport de manque).
- Ordre : f0 et f1 d'abord, indépendants ; f2a (logique, sans vue) ; **E19.s avant f2b** (la vue) ; f3 après f2a ; f4 après f2b et G0. **E19.s est le seul
  propriétaire de la formule d'échelle commune des écrans** (résolution virtuelle du moteur, recalcul des écrans au changement
  de taille) ; f2 bâtit son écran dessus sans la modifier (révision n°1 de la relecture du 2026-10-03).

##### 1.2j.1 E19.f0 — Drapeaux du texte à leur position ✅ (CONFIRMED le 2026-10-03)

**Faits** (cartographie, `e19f-plan2/yarn/notes.md`) :
- **[binaire]** un code numérique pose son drapeau temporaire quand l'interpréteur du texte l'atteint et passe au caractère suivant
  dans la même étape (`0x80046B9C` à `0x80046CC4`, puis `j 0x80046100`) ; `\Y` termine l'étape (`0x800463B8`, `j 0x80046EBC`).
- **Export** : `YarnTextEmitter.BuildPageRender` écrit chaque code numérique en `<<flag n>>` avant la ligne de sa page, le drapeau
  n'entre jamais dans la ligne (`YarnTextEmitter.cs:444-456`, `:475-477`) ; `\Y` ne produit rien (`:309-313`).
- **Corpus** : 932 codes numériques dans 627 pages, 587 nœuds, 155 des 485 fichiers `.yarn` : 87 avant tout glyphe, 647 après le
  dernier, 198 au milieu ; 922 `\Y`, dont 921 juste après un drapeau ; 58 pages ne portent que des drapeaux (`[empty/]`) ; 105
  paires de drapeaux à la même position ; valeur maximale 1005. Le marin 12 (`M389_S001`, `\999` en fin de texte) est de la classe
  « fin » : traiter le milieu seul ne suffit pas.
- **Moteur** : Yarn Spinner 3.2.1 ; un marqueur auto-fermant garde sa position exacte (11 182 marqueurs `glyph` le prouvent dans la
  preuve du corpus) ; le moteur ne change pas.
- **DLL** : le présentateur de capture ne garde que `br` et `glyph` (`AlundraDialogueCapturePresenter.cs:68`, `:83-107`) ; les
  drapeaux viennent de la commande `flag` (`AlundraYarnBindings.cs:142-153`) ; le chemin dégradé (`PlayNodeHeadlessToEnd`,
  `AlundraEventProgramRunner.cs:1896-1928`) ne lit rien des lignes.
- **Preuve** : `YarnCorpusEquivalenceTests` joue chaque nœud exporté et le compare à `ReferenceTextDecoder`, écrit indépendamment
  de l'émetteur (`ReferenceTextDecoder.cs:148-150`) ; le comparateur rejette tout nom de marqueur inconnu (`:1064-1080`).

**Règles.**
- **F0-R1 — Format** : chaque `\<chiffres>` devient `[flag id=N trimwhitespace=false/]` à la position du code dans le texte de la
  page (N en décimal normalisé, comme aujourd'hui) ; chaque `\Y` devient `[yield trimwhitespace=false/]` à sa position (choix
  technique : la cadence de la frappe d'E19.f2 en dépend, 922 marqueurs dans les mêmes 155 fichiers) ; plus aucun `<<flag n>>`
  n'est émis ; `<<falcon_update>>` ne change pas ; à une même position, l'ordre du texte source est gardé.
- **F0-R2 — Transparence** : la coupe des espaces de bord (D-E15-8) et la règle de la page vide ignorent les marqueurs `flag` et
  `yield` : un espace entre la dernière unité visible et un drapeau reste coupé (sinon 32 pages, qui portent 47 codes, changeraient de texte) ; une page
  qui ne porte que des drapeaux et des `\Y` s'écrit avec ses marqueurs dans l'ordre source puis `[empty trimwhitespace=false/]`.
- **F0-R3 — Garde** : un identifiant au-delà d'`int.MaxValue` est une erreur de conversion (les propriétés entières de Yarn).
- **F0-R4 — Compteurs** : `Yarn.FlagCommands` de `report.json` est remplacé par `Yarn.FlagMarkers` (932) et `Yarn.YieldMarkers`
  (922) ; `Yarn.EmptyPages` reste 95 (la règle de la page vide ignore les marqueurs `flag` et `yield`, y compris dans le test de
  recensement, `ReferenceTextDecoderTests.cs:805` et `:890`).
- **F0-R5 — DLL, comportement inchangé** (D-E12-4 tient jusqu'à E19.f2 : tous les drapeaux d'une page posés à son affichage) : le
  présentateur de capture pose les drapeaux des marqueurs `flag` de la ligne, dans l'ordre de la liste, à `ShowLine`, avant de
  transmettre la ligne ; le chemin dégradé (`NullDialoguePresenter.ShowLine`) aussi (il change de forme : c'est aujourd'hui un
  singleton statique sans état de jeu, `AlundraEventProgramRunner.cs:1915-1917`) ; une écriture partagée dans
  `AlundraYarnBindings` (`n | 0x8000`, bit `n & 0x1f`, la même que la commande) ; le gestionnaire de la commande `flag` reste (anciens
  exports, montages de test écrits à la main) ; `yield` est ignoré jusqu'à E19.f2.
- **F0-R6 — Ordre de livraison** : la DLL d'abord (construite et déposée dans `alundra-project/`), puis l'export complet en place ;
  ni l'un ni l'autre pendant que l'auteur joue (la DLL est verrouillée, le projet est lu par le jeu). Un export au nouveau format
  avec l'ancienne DLL ne poserait aucun drapeau du texte (le `0x36` du marin 12 ne rendrait jamais la main).
- **F0-R7 — Indépendance de la preuve** : `ReferenceTextDecoder` est mis à jour depuis ce contrat, jamais depuis l'émetteur.

**Tâches.**
- **F0-1 — Tests du convertisseur, rouges d'abord** : chaînes exactes de l'émetteur pour `M389_S001` p0, `M164_S003` p0 et p1,
  `M389_S022` p0 (drapeau de fin après un espace : l'espace est coupé), `\1004\Y\999\Y` (ordre à une même position) ; une page de
  drapeaux seuls compilée puis relue (positions 0, marqueurs puis `[empty/]`) ; la garde d'identifiant ; le décodeur de référence
  (marqueurs positionnés, recensement 932 et 922) ; le comparateur accepte `flag` et `yield` et perd son gestionnaire de commande
  `flag` (un `<<flag>>` restant tombe dans les commandes non gérées) ; tests négatifs : drapeau décalé d'un caractère, drapeau
  redevenu commande, deux drapeaux d'une même position permutés, un `yield` retiré ; compteurs de l'écrivain.
- **F0-2 — Émetteur et écrivain** (`Text/YarnTextEmitter.cs`, `Writers/YarnDialogueWriter.cs`).
- **F0-3 — DLL, tests d'abord** : drapeaux posés à `ShowLine` avant la transmission ; ordre avec `falcon_update` (`M134_S019`) ;
  chemin dégradé ; ancienne commande toujours servie ; `yield` et tout marqueur inconnu ignorés ; puis le code
  (`AlundraDialogueCapturePresenter.cs`, `AlundraYarnBindings.cs`, `AlundraEventProgramRunner.cs`).
- **F0-4 — ADR-0025** (marqueurs de position des codes numériques et de `\Y` ; ADR-0006 passe « partly superseded by ADR-0025 »)
  et `docs/formats/dialogues-yarn.md` (`:49-63`, `:86`, `:140`, `:144-176`).
- **F0-5 — Livraison** : build Release puis Debug (la Debug en dernier, `cmp` de la DLL déposée), manifeste avant, export complet en
  place, manifeste après, double export, `Alundra.Tests` sur le nouvel export.

**Acceptation.**
1. Les tests de F0-1 et F0-3 sont rouges d'abord, verts après.
2. Code de test existant touché, liste fermée : `YarnTextEmitterTests.cs` (`:172-211`, `:434-480`, `:790-880`),
   `YarnCorpusEquivalenceTests.cs` (`:275-314`, `:351-391`, `:499-536`, `:606-624`, `:792-822`, comparateur `:956` et
   `:1064-1092`, commentaire de doc `:913`), `ReferenceTextDecoderTests.cs` (`:76-125`, `:129-135` `Decode_BackslashY_AddsNothing` renommé, `:452-458`, `:566-586`, `:608-635`, `:692-704`, `:770-897`),
   `YarnDialogueWriterTests.cs` (`:687`, `:135`), et le support de test `ReferenceTextDecoder.cs` (forme de commande de
   `ReferenceCommand.Flag`, `:30`, et décodage des codes numériques et de `\Y` en marqueurs positionnés, F0-R7) ; côté DLL, aucun : les montages qui écrivent `<<flag n>>` restent servis par le
   gestionnaire gardé ; `AlundraDialogueOpcodesProductionTests` reste inchangé et vert sur le nouvel export (le `\999` du marin 12
   vient désormais d'un marqueur).
3. Export : exactement 155 `.yarn`, 155 `.dialogue` et `report.json` changent, rien d'autre (le catalogue ne change pas :
   l'identifiant vient d'`Ids.For` et l'entrée ne porte que l'identifiant, le nom et le fichier, `YarnDialogueWriter.cs:426-441`) ;
   tout autre fichier changé est un arrêt ; aucun `<<flag` dans les `.yarn` ; compteurs 932 et 922 ; double export identique à l'octet hors `report.json`.
4. Tests du convertisseur verts ; `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart ; les six traces
   à l'octet.

**Audit des valeurs** (2026-10-03, `e19f0-audit/notes.md` du scratchpad ; l'émetteur porté en Python reproduit les 485 `.yarn`
exportés à l'octet) : toutes les valeurs tenues (932, 627, 587, 155 sur 485 ; 87, 647, 198 ; 922 `\Y` ; 58 pages de drapeaux
seuls ; 932 et 922 marqueurs ; texte visible inchangé sur les 31 757 pages ; exactement les 155 tables à drapeaux changent, 311
chemins) ; chaînes exactes : `M389_S001` p0 finit par `…ta cabine ?[flag id=999 trimwhitespace=false/][yield
trimwhitespace=false/] #line:M389_S001_p0` ; `M164_S003` p0 `[voice id=0 trimwhitespace=false/]Attends...[flag id=200
trimwhitespace=false/][yield trimwhitespace=false/]Fais-moi voir ton front.[slow trimwhitespace=false/][slow
trimwhitespace=false/] #line:M164_S003_p0` ; `M164_S003` p1 `[flag id=201 trimwhitespace=false/]Tu[yield
trimwhitespace=false/][slow trimwhitespace=false/]...[slow trimwhitespace=false/]...[slow trimwhitespace=false/]tu as la
cicatrice ! #line:M164_S003_p1` ; `M389_S022` p0 finit par `…on[glyph id=18 trimwhitespace=false/][flag id=999
trimwhitespace=false/][yield trimwhitespace=false/] #line:M389_S022_p0` ; même position : `M389_S106` p0 finit par
`…leurs efforts ?[flag id=1004 trimwhitespace=false/][yield trimwhitespace=false/][flag id=999 trimwhitespace=false/][yield
trimwhitespace=false/] #line:M389_S106_p0` ; page de drapeaux seuls : `M45_S097` p1 `[flag id=999 trimwhitespace=false/][yield
trimwhitespace=false/][empty trimwhitespace=false/] #line:M45_S097_p1`. Une contradiction de rédaction, corrigée : « 47 pages »
valait 32 pages portant 47 codes (aucun test n'en dépend). Lacunes réglées dans le texte : `Yarn.EmptyPages` 95, deux entrées
de la liste fermée, la forme de `NullDialoguePresenter`.

**Retour arrière** (le projet joué n'est pas suivi par git, `.gitignore:64`) : après F0-5, ré-exporter en place avec le
convertisseur d'avant f0 en **gardant la nouvelle DLL** (elle sert encore la commande `<<flag n>>`) ; ne jamais remettre une DLL
d'avant f0 sur un export au nouveau format ; le manifeste du projet restauré doit égaler le « manifeste avant » de F0-5.

**Risques.** Livraison dans le mauvais ordre (F0-R6) ; coupe des bords non transparente (32 pages) ; une ligne de drapeaux seuls
jamais compilée (test de F0-1) ; la preuve qui perd son indépendance (F0-R7) ; numéro d'ADR (0025 est le suivant ; E19.s et la
convention d'apparition prendront les suivants).

Commits : `test(converter): …` puis `feat(converter): emit text flag codes and \Y as positioned Yarn markers` ;
`feat(alundra): read positioned flag markers from Yarn lines` ; `docs(adr): …` ; clôture du plan avec les mesures de l'export.

- **Fait le 2026-10-03** (commits `0beab51` convertisseur, `fa8d86c` DLL, `cd8cc35` ADR-0025). **Rouges d'abord** : convertisseur, 31 tests rouges
  sur le code d'avant (par exemple `M389_S001` p0 lu `<<flag 999>>` avant la ligne au lieu des marqueurs, `Yarn.FlagMarkers`
  lu 0, `2147483648` accepté, `WholeCorpus_EveryPageOfEveryNodeMatchesTheReferenceDecoder` 628 pages en écart) ; les tests du
  décodeur de référence étaient verts d'emblée (F0-R7 : mis à jour depuis le contrat) ; DLL, 5 tests rouges sur 8
  (drapeau de milieu de texte jamais posé à l'affichage, page de drapeaux seuls, drapeau lu `False` à `ShowLine`, chemin dégradé), les
  trois autres (commande ancienne, `yield` et marqueur inconnu ignorés, présentateur sans état) verts d'avance. **Verts après** :
  convertisseur 415/415, `Alundra.Tests` 2504/2504 (Debug, avant puis après le nouvel export, `AlundraDialogueOpcodesProductionTests` vert).
  **Livraison dans l'ordre F0-R6** : DLL Release puis Debug, `cmp` sans écart avec `alundra-project/Alundra.dll` (avant et après les
  exports et la suite) ; manifeste avant 23 734 fichiers, export complet en place (`dotnet run --project
  alundra-casaengine-project-converter -c Release -- data-extracted alundra-project`), manifeste après : exactement 311 fichiers
  changés (155 `.yarn`, 155 `.dialogue`, `report.json`), identiques à la liste de l'audit, 0 ajouté, 0 retiré ; double export :
  seul `report.json` diffère ; aucun `<<flag` dans les 485 `.yarn` ; `report.json` : `Yarn.FlagMarkers` 932, `Yarn.YieldMarkers` 922,
  `Yarn.EmptyPages` 95, 0 erreur, 6 avertissements d'avant (maps.json, EntityNames.csv, sprites). Garde d'octets des six traces : 0 ;
  les quatre traces du héros réécrites en LF par la suite ont été remises par `git checkout`. **Écarts** : (1) l'ordre « dans l'ordre de
  la liste » des écritures de drapeaux n'est pas observable (des OU sur des bits) : il est tenu par l'itération de la liste, sans test
  dédié ; (2) pied de commit `Co-Authored-By: Claude Sonnet 5.5` (le modèle réel) au lieu de `Opus 5.5` du brief ; (3) le constructeur du
  présentateur de capture prend l'état de jeu en paramètre facultatif (`null` : aucun drapeau posé) pour laisser les montages existants
  intacts ; (4) la valeur du drapeau est lue en `Int64` (jamais d'exception sur un `id` hors plage, marqueur ignoré).
- ✅ **Vérification d'E19.f0** (2026-10-03) : `Alundra.Tests` 2504/2504 en Release (45 s) puis en Debug (47 s), la Debug en
  dernier ; `cmp` sans écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** (les six chaînes de l'audit relues à
  l'identique dans `alundra-project/` ; son propre manifeste SHA-1 égal au manifeste d'après de l'exécuteur, 311 fichiers changés,
  0 ajouté, 0 retiré, double export ne différant que par `report.json` ; 932 `[flag`, 922 `[yield`, 0 `<<flag` ; décodeur de
  référence d'un algorithme distinct de l'émetteur ; quatre mutations attrapées : coupe de fin non transparente, `yield` retiré
  (dont la preuve du corpus entier), présentateur de capture sans écriture (dont le marin 12 sur le vrai export), chemin dégradé
  sans écriture ; seule la liste fermée a changé). Note : l'empreinte de la DLL suit le commit (version informationnelle), elle
  change avec un commit du seul plan. Avis reporté à E19.m : P4 un `[flag id=abc/]` écrit à la main (propriété texte) lèverait
  une `FormatException` dans `ShowLine` (`AlundraYarnBindings.cs:183`, `Convert.ToInt64`), le convertisseur n'en produit jamais.

##### 1.2j.2 E19.f1 — Cadres du nom et du choix, curseur d'attente ✅ (CONFIRMED le 2026-10-03)

**Faits.**
- **Données de la décompilation** (`alundra-datas-analyser/AlundraTools/AlundraEngine/StaticVariables.cs`) : la boîte du nom est
  la configuration `g_textTilesConfiguration` (X 0x40, Y 0x8C, 14 × 4 cellules, `SpritesA = g_font1_A`, 56 cellules, `:11185-11193`)
  et la boîte du choix `g_uiBoxesConfigurationBackgroundMessageChoice` (X 0xB0, Y 0x90, 16 × 4, `SpritesA = SPRT_ARRAY_800a45ec`,
  64 cellules, `:11268-11276`) ; positions conformes au binaire (nom en (64,140), choix en (176,144), découverte d'E19.f). Toutes
  leurs cellules sont dans l'atlas `wind` (56 sur 56, 64 sur 64 ; `e19f-disc/dll/namebox_tiles.py`, `choicebox_tiles.py`, aperçu
  `boxes_preview.png` : parchemin, coins transparents).
- **Chaîne** : l'analyseur porte la disposition des boîtes en deux fichiers de données, `UiBoxes.csv` (12 boîtes) et
  `UiBoxCells.csv` (1351 cellules), complétés à partir de `StaticVariables.cs` avec preuve par un lecteur indépendant (précédents :
  commits `64978f8` et `8f403d5` du sous-module) ; le convertisseur les lie (`.csproj` `:76-80`) et `UiBoxWriter` cuit **chaque**
  boîte listée (`UI/Textures/<boîte>.png`, son `.texture`, `UI/<boîte>.sprite`, identifiant `Ids.For("sprite-ui:" + boîte)`).
- **Boîte du nom au repos** **[binaire]** (question bornée du 2026-10-03, `e19f1-namebox/` du scratchpad) : configuration en
  `0x800A58BC` (X 64, Y 140, 14 × 4), tampons de cellules `0x800A4FFC` et `0x800A545C` ; le seul code qui écrit leurs `x0`/`y0` est
  `UpdateUiBoxesPosition` (`0x80047DD0`, `x0 = X + 8c` en `0x80047F40`, `y0 = Y + 8r` en `0x80047F44`), appelé par le rendu de la
  boîte (`0x8005A3E0`, appel en `0x8005A41C`) avant le dessin de la même image (`0x800481F8`, qui ne fait que chaîner les
  cellules) ; l'ouverture (`0x80059F6C` → `0x8005A268`) et la fermeture (`0x80059FE0`) ne glissent qu'en x et rendent Y = 140. Au
  repos, le cadre occupe **x 64 à 175, y 140 à 171** ; le nom est centré en `X + (112 − largeur) / 2`, à y = Y + 8 = 148
  (`0x8005A4F0`-`0x8005A534`). Les `y0` bruts du tableau (144 à 168, 4 px plus bas) ne sont jamais affichés : la boîte du nom est la
  seule dont les positions brutes diffèrent de sa configuration (boîte du choix, boîte de l'arme, boîte du message et boîte de
  l'objet contrôlées). La décompilation reproduit les mêmes données brutes.
- **Curseur d'attente** : 16 × 16, cellules `wind_150`, `wind_173`, `wind_201`, `wind_228` (u 176, 192, 208, 224 ; v 56 ;
  palette 8), 10 ticks par image, sans décalage (binaire, découverte d'E19.f) ; `UiAnimationWriter` écrit déjà trois cycles de ce
  genre (le curseur de l'inventaire, `:35-50`).

**Règles.**
- **F1-R1 — Analyseur** (branche dédiée du sous-module `chantier/e19f1-dialogue-boxes`, depuis `master` `242b09a`) : deux lignes
  ajoutées à `UiBoxes.csv`, `g_textTilesConfiguration;64;140;14;4` et `g_uiBoxesConfigurationBackgroundMessageChoice;176;144;16;4`,
  et leurs 56 + 64 cellules de la copie A à `UiBoxCells.csv`, dans l'ordre des tableaux ; `u0`, `v0`, `w`, `h` et `clut` bruts ;
  `x0`/`y0` **aux positions où le binaire les dessine**, `(X + 8·colonne, Y + 8·rangée)` (révision n°1 : pour la boîte du choix
  et toutes les boîtes déjà listées, ce sont les valeurs brutes ; pour la boîte du nom, les `y0` bruts 144 à 168 sont des données
  initiales périmées que le binaire réécrit avant tout affichage, voir les faits) ; les lignes existantes ne changent pas, à
  l'octet ; aucun code de l'analyseur ne change.
- **F1-R2 — Convertisseur** : `UiBoxWriter` cuit les deux nouvelles boîtes sans changement de code ; un quatrième cycle,
  `ui_dialogue_cursor` (`wind_150`, `173`, `201`, `228`, 10 ticks par image, sans décalage), rejoint `UiAnimationWriter`.
- **F1-R3** : aucun écran ne les utilise encore (E19.f2 et E19.f3 les brancheront) ; la DLL ne change pas.

**Tâches.**
- **F1-1 — Analyseur** : les lignes, puis la preuve par un lecteur indépendant (script Python du scratchpad qui relit
  `StaticVariables.cs` : 14 boîtes et 1471 cellules, aucune manquante ni en trop, même ordre ; chaque cellule tombe dans sa boîte,
  0 hors boîte sur les 14 ; pour la boîte du nom, `x0`/`y0` = configuration + grille, le reste brut) ; commit du sous-module
  `feat(tables): export the dialogue name and choice boxes as CSV (E19.f1)`.
- **F1-2 — Convertisseur, tests d'abord** : le cycle `ui_dialogue_cursor` (quatre images, 10 ticks, identifiant stable) ; la cuisson
  des deux boîtes depuis les vraies données (tailles 112 × 32 et 128 × 32), comparée **octet pour octet, alpha compris**, à une
  référence indépendante (révision n°1 : l'aperçu `boxes_preview.png` ne suit pas les mêmes règles, fond magenta, collage par
  masque, échelle 4, origine à la boîte englobante) : deux PNG de référence, versionnés comme données de test, produits par un
  script qui ne lit pas les CSV (il relit les tableaux de `StaticVariables.cs`), fond entièrement transparent, copie brute RGBA de
  chaque tuile de `data-extracted/ui/wind.png`, origine à la position décidée en F1-R1 ; le test est rouge sur les CSV d'avant et
  rougit si une cellule bouge de 8 px ou si un texel de coin transparent change ; commit `feat(converter): bake the dialogue name and choice boxes and the wait cursor`
  avec le pointeur du sous-module.
- **F1-3 — Livraison** : manifeste avant, export complet en place, manifeste après, double export ; `Alundra.Tests` sur le nouvel
  export (garde : la DLL ne change pas).

**Acceptation.**
1. La preuve du lecteur indépendant (14 et 1471, 0 cellule hors de sa boîte) ; les tests de F1-2 rouges d'abord, verts après.
2. Tests existants touchés, liste fermée : `UiWriterTests.cs:97` (`Assets.UiAnimation` 3 → 4), `:149` (fichiers de
   `UI/Animations` 3 → 4), `:164` (avertissements « skipped: sprite wind_ » du montage à trois entrées 3 → 4), et les assertions
   du nouveau cycle à côté de celles de `:100-120` ; la doc de classe d'`UiAnimationWriter` (« the three looping UI animations »,
   `:9`) passe à quatre ; rien d'autre (révision n°1).
3. Export : exactement sept fichiers nouveaux (`UI/Textures/g_textTilesConfiguration.png` et son `.texture`,
   `UI/g_textTilesConfiguration.sprite`, les trois mêmes pour `g_uiBoxesConfigurationBackgroundMessageChoice`,
   `UI/Animations/ui_dialogue_cursor.anim2d`), deux modifiés (le catalogue `AssetInfos.json`, `report.json`), rien d'autre ;
   double export identique hors `report.json` ; 0 erreur ; `UiBoxes.CellsWithoutTile` reste 0 ; `UI/Textures/g_textTilesConfiguration.png`
   fait 112 × 32 et `UI/Textures/g_uiBoxesConfigurationBackgroundMessageChoice.png` 128 × 32.
4. Tests du convertisseur verts ; `Alundra.Tests` vert sur le nouvel export ; les six traces à l'octet.

- **Fait le 2026-10-03** (sous-module `chantier/e19f1-dialogue-boxes` `b92b7b9`, parent `2e86c87`). F1-1 : 2 boîtes et 120
  cellules ajoutées en fin de CSV ; lecteur indépendant de `StaticVariables.cs` : 14 boîtes, 1471 cellules, aucune manquante ni
  en trop, même ordre, 0 cellule hors de sa boîte, aucun recouvrement, `y0` de la boîte du nom 144-168 bruts → 140-164, lignes
  existantes à l'octet. F1-2 : rouges d'abord sur les CSV d'avant, valeurs lues : les deux PNG de la cuisson absents (boîtes non
  cuites) et 3 tests de `UiWriterTests` avec « attendu 4, lu 3 » (animations, fichiers, avertissements) ; verts ensuite
  (10 sur les deux classes, 417 sur la suite du convertisseur). Mutations jetables, restaurées : deux cellules du nom
  permutées (8 px) rougit (`premier octet différent en (3, 0), attendu 72, lu 0`) ; un texel de coin transparent de la
  référence passé à alpha 255 rougit (`(0, 0), canal alpha, attendu 255, lu 0`). Références versionnées dans
  `alundra-casaengine-project-converter.Tests/TestData/` (copiées à la sortie par le `.csproj`). F1-3 : manifeste SHA-1 de
  `alundra-project/` hors DLL, PDB et `.casaeditor/` (23734 fichiers) avant ; export complet en place ; après 23741 :
  exactement les sept fichiers nouveaux prévus, `AssetInfos.json` et `report.json` modifiés, rien d'autre ; double export
  identique hors `report.json` ; 0 erreur, `UiBoxes.CellsWithoutTile` 0, `UiBoxes.Boxes` 13, `UiBoxes.Cells` 1471,
  `Assets.UiAnimation` 4 ; PNG 112 × 32 et 128 × 32 ; `Alundra.Tests` 2504 verts ; six traces à l'octet
  (`--ignore-cr-at-eol`), les quatre traces du héros remises par `git checkout`. Écarts : aucun du texte du plan ; le test de
  cuisson saute si `data-extracted/` est absent (convention des tests sur données réelles), et le cycle est testé par la
  constante littérale `"ui_dialogue_cursor"`.

**Retour arrière** : le pointeur du sous-module et le commit du convertisseur reviennent ; ré-export en place ; la DLL n'a rien à
reprendre (elle ne lit pas encore ces fichiers).

**Risques.** Une cellule hors de l'atlas (preuve de F1-1) ; le nom des boîtes, celui des variables de la décompilation, comme les
boîtes de l'inventaire.
- ✅ **Vérification d'E19.f1** (2026-10-03) : convertisseur 417/417 (Release), `Alundra.Tests` 2504/2504 en Release puis en Debug,
  la Debug en dernier ; `cmp` sans écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** (son propre lecteur de
  `StaticVariables.cs` retrouve les 120 cellules, l'ancien CSV est un préfixe exact du nouveau ; binaire relu en `0x800A58BC` et
  `0x80047DD0` ; les deux boîtes reconstruites une troisième fois, égales octet pour octet aux références et à l'export ; deux
  mutations à lui attrapées, une cellule de la boîte du choix déplacée de 8 px et un texel transparent changé ; empreintes de
  l'export égales au manifeste d'après). Avis reporté à E19.m : P4 le message d'échec d'`UiDialogueBoxTests` calcule la position du
  pixel avec une largeur fixe de 128 (`UiDialogueBoxTests.cs:78-89`), faux pour la boîte du nom ; le verdict du test est juste.
  La branche `chantier/e19f1-dialogue-boxes` du sous-module de l'analyseur (`b92b7b9`) n'est pas mergée : le pointeur du parent la
  désigne, comme les chantiers précédents.

##### 1.2j.3 E19.f2 — Boîte de texte fidèle ⏳ (planifiée : f2a détaillée, f2b esquissée ; ordre tranché par l'auteur, D-E19-64)

**Découpage** (2026-10-03) : **f2a** porte la logique de la boîte au tick près dans la DLL, sans vue (le directeur et son état,
les opcodes `0x4C` à `0x51`, les sons, les drapeaux à leur glyphe), avec la vague de ré-épingles des tests ; **f2b** branche la vue
XAML liée à un view model (cadre cuit, glissements, trois lignes en font3, défilement, curseur `ui_dialogue_cursor`, centrage
`\H`) après E19.s, seul propriétaire de la formule d'échelle des écrans. Nom et portrait : f4 ; choix : f3.

**Découverte** (2026-10-03, lecture seule, deux surfaces, scratchpad de la session) : `e19f2-disc/model/` (modèle au tick près tiré
du binaire, `model.py`, `selftest()`, traces des trois textes réels) et `e19f2-disc/impact/notes.md` (tests touchés, classes R, C,
L, U). Faits porteurs **[binaire]** (adresses dans les notes) :
- **Ordre de l'image** : la mise à jour de la boîte (RenderScene, emplacement 0 du répartiteur `0x80048054`) passe AVANT tous les
  scripts de la même image et lit la manette lue à l'image précédente (`0x8002C3F4` : `0x8002C3FC` puis `0x8002C404`).
- **Ouverture** (`0x0D`, `0x5C`, `0xC4` ; `TryOpenDialog` `0x800423F8`) : au tick N, le script continue dans le même tick ; 0 et
  nouvel essai tant que `g_dialog_flags & 4` ; son 6 au tick N ; mode 1 → MessageBox, sinon MenuOpen (plus aucun script
  d'événement de N+1 à la fermeture) ; remise à zéro à l'ouverture (`textFlags` 3, délai 1, `scrollMode` 3, `closeMode` 3,
  verrous `0x4D`/`0x4F`/`0x51` à 0, voix −1, ligne 0, frappe finie 0, curseur éteint et son compteur à 0 ; la minuterie de
  fermeture et les compteurs de défilement ne sont PAS remis à zéro).
- **Glissement d'entrée** : y = 240, 236, 231, 226, 221, 216, 212, 207, 202, 197, 192, 188, 183, 178, 173 de N+1 à N+15, 168 à
  N+16 et N+17, rien à N+18 ; **première lettre à N+19**.
- **Porte de pas** (une fois par image) : `textFlags & 2` et délai qui tombe à 0 (puis 4) ; ou `textFlags & 1` et Carré
  **tenu** ; ou `textFlags & 4` et verrou `0x4D` (consommé). Un appui naissant est ignoré pendant la frappe.
- **Contenu d'un pas** : codes gratuits (`0x0A`, drapeaux `\chiffres` posés à cet instant, `\B` à `\G`, `\H`, `\MCE`, `\V`, `\X`),
  puis exactement UN de : un glyphe, `\N`, `\A`, `\T` (qui pose aussi le délai à 8), `\Y` (rien de dessiné), la fin du texte (une
  porte de plus qui lit l'octet 0 : image E ; la minuterie de 360 n'est armée qu'à E et seulement si `closeMode & 1`).
- **Lignes** : trois visibles ; `\N` en 1re ou 2e ligne passe à la suivante ; en 3e ligne il arme un défilement : attente de 10
  images (`scrollMode & 1`) ou d'un appui naissant (`& 2`), puis 8 images de +2 px, l'interpréteur n'étant pas appelé pendant ce
  temps. `\A` : curseur allumé dès l'image du pas, relâché par un appui naissant à partir de l'image suivante, suivi d'une nouvelle
  ligne ; `\N\A` laisse une ligne vide.
- **Curseur** : (288,200), image = compteur/10 (quatre images), compteur +1 par image dessinée, remis à 0 seulement à l'ouverture.
- **Voix** : son `79 + voix` quand un caractère normal est dessiné à un rang pair, rang remis à 0 à chaque nouvelle ligne ; `\W`,
  `{` et `}` ne comptent pas ; `\B` (−1) et `\G` (4) muets.
- **Fermeture** (évaluée à partir de E+1) : appui naissant (`closeMode & 2`), minuterie (déclenchement à E+360, `& 1`), verrou
  `0x51` (`& 4` : déclenchement à `max(L+1, E+1)` pour un `0x51` au tick L ; le verrou n'est posé que si `closeMode & 4` à ce
  moment, il est effacé à l'ouverture suivante et quand il sert). Image T du déclenchement : son 7, glissement de sortie y = 168,
  172, 177, 182, 187, 192, 196, 201, 206, 211, 216, 220, 225, 230, 235 de T+1 à T+15, 240 à T+16 et T+17 ; **libération à T+18**
  (drapeaux, MessageBox et MenuOpen tombent avant les scripts de l'image : `0x39` rend 1, un `0x0D` réessayé ouvre).
- **`0x4C` à `0x51`** : `0x4C v` → `textFlags = v` ; `0x4D` → verrou si `textFlags & 4` ; `0x4E v` → `scrollMode = v` ; `0x4F` →
  verrou si `scrollMode & 4` ; `0x50 v` → `closeMode = v` ; `0x51` → verrou si `closeMode & 4` ; aucun ne rend la main.
- **Deux défauts de l'original, corrigés par l'auteur** : D-E19-62 (le `0x4C` efface le `0x4D` en attente) et D-E19-63 (chaque
  défilement de 3e ligne attend ses 10 images ou un appui ; le bit 8 résiduel d'un `\A` relâché en 1re ou 2e ligne ne s'applique
  plus).
- **Valeurs du modèle sur les trois textes** (pad A du binaire, impossible sur une vraie manette) : 389 `M389_S001` première lettre
  N+19, T999 à N+94, fin E = N+95 ; 391 S019 E = N+355, déclenchement N+413, `0x39` à N+431 ; 164 `M164_S003` E = N+172.
- **DLL d'aujourd'hui** (`e19f-plan2/dialog/notes.md`) : page entière d'un coup, fermeture immédiate, minuterie depuis
  l'ouverture, aucune vue fidèle ni son ; la passe tourne après les événements de carte (`AlundraWorldProxy.cs` ~2217) sur
  l'instantané de manette de l'image rendue (avis A1 d'E12.a). Le chemin dégradé (sans présentateur) joue le nœud jusqu'au bout
  d'un coup et ne doit jamais attendre.

**Décision à prendre par cette tranche, consignée en ADR-0029** : la boîte suit le binaire (règles ci-dessus) ; les drapeaux du
texte sont posés à leur glyphe (fin de D-E12-4, que remplace cette ADR) ; à chaque tick logique, la passe de la boîte tourne
avant les événements de carte du même tick, sur la manette du tick précédent, et la porte des événements de carte est relue après
elle ; écarts nommés dus à l'ordre du moteur (les entités avant le mandataire du monde), acceptés par l'auteur (D-E19-64), sans
datation, origine par origine dans la table de F2-R1, avec l'ordre propre au harnais d'intro ; les deux défauts corrigés.

###### E19.f2a — Logique de la boîte au tick près ⚠️ (en pause : la relecture de clôture de l'époque D-E19-64 rend REVISE sur deux points du harnais d'intro, l'origine d'un test et la règle d'appui ; corrigés ci-dessous ; une nouvelle relecture demande l'accord de l'auteur)

**Règles.**
- **F2-R1 — Ordre d'un tick** (révision n°1 : la porte calculée une fois par image et la boucle de la manette, qui fait tous
  les ticks avant les événements de carte, contredisaient l'ordre du binaire, `AlundraWorldProxy.cs` ~1998-1999, ~2022-2061,
  ~2082-2101, ~2127-2132) : la passe de la manette enregistre l'état de Carré de chaque tick (maintien et appui naissant) ; la
  boucle des ticks des événements de carte devient une boucle qui tourne toujours (même jeu bloqué) et fait, à chaque tick k :
  (1) la passe de la boîte, sur l'état de Carré du tick k−1 (celui de la dernière passe de l'image précédente pour k = 0), comme le
  binaire lit la manette de l'image précédente ; (2) la porte relue (`GameplayBlockedMask`, transition de warp) ; (3) si le héros
  existe et la porte est ouverte, les événements de carte puis le recyclage (R3) ; les déclencheurs en attente relisent la porte
  après la boucle ; l'ancienne passe de dialogue (~2217) disparaît. Conséquences : une boîte ouverte au tick N fait sa première
  mise à jour à N+1 ; une boîte libérée à T+18 rend la main aux événements de carte dès T+18, en mode 1 comme en mode MenuOpen ;
  sur une image de rattrapage, boîte et événements de carte s'entrelacent tick par tick (B0 M0 B1 M1), comme le binaire ; le
  déclencheur d'inventaire ne change pas de place. L'avis A1 d'E12.a est clos.
  **Écarts dus à l'ordre du moteur** (D-E19-64 ; le moteur met à jour les entités, scripts compris, avant le mandataire du
  monde, `AlundraEntityScriptProxy.cs` ~1090-1091 et ~1209-1213, `AlundraWorldProxy.cs` ~1946-1956 ; les déclencheurs en
  attente tournent après la boucle des ticks, ~2127-2133 ; aucune datation) : ce qui est immédiat dans le binaire le reste
  (drapeau d'activité, MessageBox/MenuOpen, son 6, texte chargé, ce que lisent `0x39` et l'ouverture suivante) ; la passe de la
  boîte voit tout ce qui a été écrit avant elle. Par origine (N = tick de l'ouverture, k = tick d'une écriture `0x4C` à `0x51`,
  T = déclenchement de la fermeture ; « rattrapage » = image de plusieurs ticks, indices j dans l'image) :

  | Origine | 1re mise à jour | Effet d'une écriture | Libération vue par cette origine |
  |---|---|---|---|
  | Événement de carte | N+1 (binaire) | k+1 (binaire) | T+18 (binaire) |
  | Script d'entité | **N** à un tick par image (un tick tôt) ; rattrapage : la passe 0 de l'image | **k** (un tick tôt) ; rattrapage : passe 0 | **T+19** à un tick par image (un tick tard) ; rattrapage : premier tick de l'image suivante |
  | Déclencheur en attente | N+1 à un tick par image ; rattrapage : première passe de l'image suivante | même règle | T+18 à un tick par image ; rattrapage : une itération j < (indice de la passe de libération) la voit déjà (tôt) |

  **Harnais d'intro** (seul son bloc `if (_installDialogueDirector)` change, `IntroTraceHarnessTests.cs` ~581-591) : chaque image
  y fait `RunFrame` (scripts), puis la passe de la boîte, puis le rappel du test (`afterEachFrame`). Par origine : **scripts** de
  `RunFrame` (entité, événement de carte, déclencheur en attente) : première mise à jour à N, écriture qui agit à k ; **rappel du
  test** : première mise à jour à N+1, écriture qui agit à k+1 ; une libération à la passe de l'image T+18 est vue par le rappel à
  T+18 et par les scripts à T+19. **Manette** : le bloc tient un miroir de `TickPad` mis à jour à chaque image avant la passe de la
  boîte à partir de `LastPadState.ButtonsHold` (que le rappel de l'image précédente a écrit : c'est la manette du tick précédent) ;
  un appui est donc un maintien d'une image, suivi d'un relâchement. Les tests du harnais qui appuient aujourd'hui par
  `ButtonsJustPressed` seul (`AlundraDialogueOpcodesProductionTests.cs` ~111, ~152, ~246, ~363 ;
  `AlundraGlobalFreezeEntityUpdateTests.cs` ~98, ~176) écrivent `ButtonsHold` pour l'image de l'appui (modification permise, dans
  la liste fermée) ; l'oracle prend exactement cette manette. **Règle d'appui des tests du harnais** (révision de la relecture
  de clôture) : celle de l'aide des arcs, Carré tenu pendant la frappe, et, quand la boîte attend un appui (curseur `\A`, ou frappe
  finie avec `closeMode & 2`), un relâchement d'une image puis un appui ; les fenêtres d'appui et les budgets de
  `RunFramesForTest` de ces tests deviennent des lignes de l'annexe (valeurs écrites d'avance par l'oracle sous cette manette) et
  entrent dans la liste fermée. **Origine des ouvertures, test par test** : `SailorTwelve_*` (~85) et
  `SailorThirteen_FullInteractionChain_*` (~298, l'ouverture vient de la vraie sélection du programme F dans `RunFrame`) par des
  scripts (première mise à jour à N) ; `SailorThirteen_MonoLine_*` (~201, images 2 et 50) et `AlundraGlobalFreezeEntityUpdateTests`
  (~86, ouverture en ~116) par le rappel (première mise à jour à N+1) ; le test sans boîte (~438) n'ouvre rien.
- **F2-R2 — Machine de la boîte** : le directeur porte l'état du binaire (glissements, porte de pas, pas, lignes et défilement,
  curseur, voix, fermeture, libération) exactement comme les faits ci-dessus, D-E19-62 et D-E19-63 compris, sans dépendre d'une
  vue : il tourne avec ou sans présentateur visuel ; « a un présentateur » garde son sens d'aujourd'hui (un chemin non dégradé).
  **Pendant un choix en attente** (révision n°1), la machine de la boîte continue de tourner comme dans le binaire (la boîte de
  choix est un autre emplacement du répartiteur) : le retour immédiat sur `_awaitingChoice` (`AlundraDialogueDirector.cs:314`)
  ne gèle plus la boîte ; la liste de choix du moteur reste celle d'aujourd'hui jusqu'à E19.f3. **L**, le tick du `0x51` qui suit
  un choix, est le tick où `0x44` rend son résultat (le premier appel de `0x44` après `SelectChoiceForTests`) ; le `0x51` qui le
  suit dans le même tick pose le verrou ; l'oracle prend la même définition.
- **F2-R3 — Texte** : la page Yarn est découpée en pas à partir de son texte et de ses marqueurs (`br` = `\N`, `glyph` = un
  glyphe `\W`, `voice`, `center` = `\H`, `slow` = `\T`, `flag` posé à sa position, `yield` = `\Y`, `empty`) ; `\A` est la frontière
  de page de l'export (ADR-0006) : une page suivante est un `\A` ; un drapeau est posé dans le pas où l'interpréteur l'atteint (fin
  de D-E12-4) ; le chemin dégradé reste instantané et pose tous les drapeaux du nœud.
- **F2-R4 — Opcodes** : `0x4C`, `0x4D`, `0x4E`, `0x4F` portés (taille du binaire, aucun ne rend la main) ; `0x50` et `0x51`
  suivent le binaire (`0x51` devient un verrou) ; `0x39` attend la libération (T+18).
- **F2-R5 — Sons** : 6 à l'ouverture (tick N), 7 au déclenchement de la fermeture, voix 79 à 82 ; par le lecteur du monde (le
  directeur en reçoit un, comme les directeurs d'inventaire).
- **F2-R6 — Préfixe et accès de test** (révision n°1) : en f2a, la boîte du moteur montre le **préfixe tapé** : le directeur
  renvoie au présentateur, à chaque pas qui change le texte visible, la page tapée jusque-là (glyphes en font3, retours de ligne) ;
  le présentateur de capture ne pose plus les drapeaux à `ShowLine` (le directeur les pose à leur pas, F2-R3) ; `CurrentLineForTests`
  lit la page entière **chez le directeur**, plus chez le présentateur (8 fichiers de test l'utilisent) ; un accès à part rend le
  préfixe tapé et l'état (phase, y, lignes, curseur, attente d'appui) ; aucun mode rapide de production. Tests : le présentateur
  reçoit un préfixe qui grandit ; `CurrentLineForTests` rend la page entière pendant la frappe.

**Oracle de test** : `AlundraTextBoxOracle` (projet de tests), port en C# du modèle tiré du binaire, écrit depuis les règles et
`model.py`, **jamais** depuis le code du directeur ; il lit la **page Yarn exportée** (texte et marqueurs, espaces de bord déjà
coupés par D-E15-8), pas le texte brut des cartes, et l'origine de chaque ouverture, écriture et attente de libération avec
l'ordre de son hôte (production ou harnais d'intro) : il calcule les valeurs de la DLL selon la table de F2-R1, et celles du binaire
pour les écarts ; ses propres tests reprennent les valeurs de `selftest()` (glissements, AB :
glyphes 19 et 23, fin 27, fermeture par minuterie 387, libération 405 ; pad A : 19, 20, 21, 22, 40 ; `\T` ; défilement de 4
lignes ; `\N\A` ; verrou `0x4D` ; images du curseur) et les trois textes réels, corrigés de D-E19-62 et D-E19-63 là où ils
jouent.

**Manette des arcs** (test seulement) : l'aide `RunUntilPressingTheButtonOnEveryDialogueFrame` (`AlundraArcSupport.cs:465-482`) et
ses copies (A17 `AlundraDay3SceneArcTests.cs:211-234`, contre-preuve de T-B9 `AlundraEntityContactArcTests.cs:340-348`) tiennent
Carré enfoncé pendant la frappe et, quand la boîte attend un appui (curseur `\A`, ou frappe finie avec `closeMode & 2`), le
relâchent une image puis l'enfoncent (un appui naissant) ; l'oracle reproduit la même manette.

**Tâches.**
- **F2A-1 — Oracle, tests d'abord** (test seulement) : l'oracle et ses tests (valeurs de `selftest()` et des trois textes, sous la
  manette des arcs, calculées d'avance par l'audit des valeurs avec `model.py` corrigé de D-E19-62 et D-E19-63).
- **F2A-2 — Directeur et opcodes** : F2-R1 à F2-R6 ; tests unitaires de la boîte contre l'oracle (synthétiques et trois textes
  réels sous la manette des arcs).
- **F2A-0 — Table des épingles, avant F2A-2** (révision n°1) : l'audit des valeurs écrit l'annexe versionnée
  `docs/plan-e19-f2a-valeurs.md` (patron de `docs/plan-e19-d2c-valeurs.md`), une ligne par assertion de classe R de la liste
  fermée : valeur d'avant ; boîtes traversées ; durée de chaque boîte aujourd'hui (déduite des règles d'aujourd'hui : une page par
  appui, fermeture au tick de l'appui ou du `0x51`, avec la manette des arcs d'aujourd'hui) et sous la boîte fidèle (oracle, sous
  la nouvelle manette des arcs, avec l'origine de chaque ouverture et l'ordre de l'hôte, F2-R1) ; nature de l'épingle et nouvelle valeur. Deux natures : **(a) valeur de la boîte** (tests de la
  boîte, A6 dont les attentes sont pures, A8, A9, A20, A12 ré-ancré) : nouvelle valeur écrite d'avance ; **(b) ré-ancrage** (A10,
  A11 : attentes périodiques d'autres acteurs, attentes physiques `0x24`/`0x0B`) : l'épingle absolue devient « image de libération
  de la dernière boîte traversée + l'écart d'aujourd'hui », écart écrit d'avance, avec une marge écrite d'avance égale à la période
  de l'attente périodique de l'acteur qui suit (Wendell 3, Meade 2) ; toute autre différence est un arrêt. Budgets `FrameLimit` :
  fin prédite + 20 %. La table est commitée avant F2A-2.
- **F2A-3 — Vague de ré-épingles**, dans le même commit que F2A-2 (chaque commit vert) : chaque assertion de classe R prend la
  valeur de sa ligne de la table, rien d'autre ; une mesure qui diffère de la table est un arrêt ; classe C (aide et budget
  seulement : toute valeur qui bouge est un arrêt) ; classe L (les 32 lignes `0x4C`/`0x4D` de `story-chain-skipped-opcodes.tsv`,
  A6 `:38`, `:186`, A11 `:356-364`, `AlundraEventProgramRunnerTests.cs:322-330`) ; classe U inchangée ; tests nouveaux : la
  libération d'une boîte MenuOpen vue par un `0x39` d'événement de carte à T+18, l'entrelacement sur une image de rattrapage, une
  boîte ouverte par un script d'entité (première mise à jour à N, premier glyphe à N+18, écart nommé, valeurs écrites d'avance) et
  sa libération vue par un script d'entité à T+19, un `0x51` posé par un script d'entité qui agit à k, une boîte qui finit sa frappe
  pendant un choix en attente, le préfixe reçu par le présentateur.
- **F2A-4 — ADR-0029** et le plan.

**Acceptation.**
1. Oracle : ses tests verts aux valeurs écrites d'avance ; l'annexe des épingles commitée avant F2A-2 ; tests de la boîte rouges
   d'abord, verts après ; chaque épingle ré-épinglée égale à sa ligne de l'annexe.
2. Tests existants touchés, liste fermée (`e19f2-disc/impact/notes.md` §0) : classe R — `AlundraDialogueFramePassTests.cs:81-136`,
   `:163-222` ; `AlundraDialogueOpcodeDispatchTests.cs:134-187`, `:268-298` ; `AlundraDialogueOpcodesProductionTests.cs:71-420` ;
   `AlundraGlobalFreezeEntityUpdateTests.cs:174-187` ; `AlundraSaveBookTests.cs:101-194` et les appels d'`AssertReleased` ;
   `AlundraDialogueFlagMarkerTests.cs:105-218` ; `AlundraDialogueYarnRenderingTests.cs:143-176` ; A6
   (`AlundraShipBlockArcTests.cs:199-275`), A8 (`AlundraInoaAwakeningArcTests.cs:194-280`), A20 et A10 et A11
   (`AlundraInoaDayOneArcTests.cs:69-87`, `:184-284`, `:345-433`), A12 (`AlundraBergusJumpArcTests.cs:44-66`), A9
   (`AlundraVisionAndCoastArcTests.cs:470-512`) ; classe C — budgets et aides de A2, A4, A4p, T-A19, T-A10v, T-B9, A13 à A18, A1,
   A1c, A10J, `AlundraSaveBookEndToEndTests` ; classe L ci-dessus ; `AlundraArcSupport.cs` (aide de manette, budgets) ; harnais
   d'intro : seul le bloc `if (_installDialogueDirector)` de `RunFramesForTest` (`IntroTraceHarnessTests.cs:581-591`) et les
   écritures de manette, fenêtres d'appui et budgets de `RunFramesForTest` des tests du harnais nommés en F2-R1
   (`AlundraDialogueOpcodesProductionTests.cs` ~111, ~152, ~210, ~244-247, ~307, ~357-365 ; `AlundraGlobalFreezeEntityUpdateTests.cs`
   ~96-98, ~174-177), aux valeurs de l'annexe ; les tests du
   présentateur qui supposent une ligne par page, que l'audit des valeurs nomme dans l'annexe avant F2A-2. Toute autre assertion
   qui bouge est un arrêt.
3. Les six traces à l'octet (la trace d'intro ne voit aucun opcode de dialogue et tourne sans directeur).
4. `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
5. **Recette F2a** (auteur, sans vue fidèle avant f2b : la boîte du moteur affiche le préfixe tapé) : la frappe lettre à lettre, le
  défilement, `\A`, la fermeture après le glissement, le marin 12 de la 389, le réveil à Inoa, Septimus à la 164.

**Retour arrière** : revert des commits (DLL et tests seulement, aucun export).

**Risques.** La vague de ré-épingles (règle de l'écart expliqué) ; les budgets des arcs ; une aide de manette qui bloque (arrêt) ;
le chemin dégradé qui doit rester instantané ; la catégorie « a un présentateur » qui ne doit pas dépendre d'une vue ; les espaces
de bord perdus par D-E15-8 raccourcissent la frappe d'un pas à ces bords (écart connu, ADR-0006) ; la place des codes `\X` dans le
texte (`falcon_update` avant la ligne) n'est pas établie au milieu d'une page.

###### E19.f2b — Vue de la boîte (esquisse, après E19.s et E19.f2a)

Écran XAML lié à un view model au patron d'`AlundraSaveScreen` : cadre cuit (`973a9208-…`), y du glissement, trois lignes font3,
défilement par découpe (la découpe de MGUI sous l'échelle de la racine est à vérifier), curseur `ui_dialogue_cursor`, centrage `\H`
par les largeurs des glyphes (table du binaire `0x800993C4` comparée aux avances de `font3.fnt`) ; la boîte du moteur n'est plus
utilisée par Alundra.

### 1.2k E19.k — Caméra : balancement `0x8E`/`0x8F` (E19.k1), masque des fonds `0xA4` (E19.k2) — E19.k1 ✅ (recette K5 en attente) ; E19.k2 ⏳ (planifiée)

**Découverte** (2026-10-03, lecture seule ; notes et scripts dans le scratchpad de la session, `e19k-disc/`). Faits
porteurs **[binaire]** :
- Gestionnaires : `0x8E` en `0x80040534` (taille 5), `0x8F` en `0x80040598` (taille 1), `0xA4` en `0x80041098` (taille 3),
  conformes à `EventOpcodeSizeTable.cs:185,186,207` (libellés faux : « Set scrolling … »).
- Une structure globale unique en `0x800E4338` : Flag, LimitX/Y, SpeedX/Y, OffsetX/Y, ReachX/Y. **`0x8E [b1,b2,b3,b4]`** :
  Flag = 1, SpeedX = b1, SpeedY = b2, LimitX = b3, LimitY = b4, octets **non signés** ; ne touche ni Offset ni Reach.
  **`0x8F`** : Flag = 0, rien d'autre.
- **Pas de balancement** (`0x8002C894`, premier appel du suivi caméra `0x8002CDA0`, en `0x8002CDD8`, une fois par tick) : si Flag = 0,
  Limit, Speed et Offset à 0 (**pas Reach**) ; axe X : si LimitX = 0 ou SpeedX = 0, OffsetX = 0 ; axe Y : seul LimitY = 0
  est testé (`0x8002C944` ; un SpeedY nul fige OffsetY) ; par axe, si Reach = 0 : `Off -= Speed`, et si `Off <= -Limit`,
  `Off = -Limit` et Reach = 1 ; sinon `Off += Speed`, et si `Off >= Limit`, `Off = Limit` et Reach = 0 (onde triangulaire
  qui part vers le négatif).
- **Combinaison avec le suivi** : `camX += ((cibleX − (camX + 0xA0)) >> 4) + OffsetX`, de même en Y avec `0x88` et
  OffsetY, puis bornes [0, 0x39F] × [0, 0x2CF], la valeur bornée réécrite dans l'état : le décalage est **intégré** au
  scroll à chaque tick. Le tick de l'accroche (chargement) fait le pas de balancement **sans** ajouter le décalage. Les
  tuiles, les entités et les fonds (avec leur parallaxe) suivent le scroll ; le HUD et l'UI non.
- **Transition** (audit des valeurs) : le portail (`0x800314FC`) et `0x53` (`0x8003EC80`) arment le départ ; la boucle
  d'images sort aussitôt (`0x8002C454`-`0x8002C45C`) et la boucle de transition n'appelle ni le rendu ni le suivi : pas
  de balancement pendant le fondu de départ.
- **Chargement de carte** (`0x8002CD54`) : seul Flag repasse à 0 ; le premier pas efface Limit, Speed, Offset ; Reach
  persiste d'une carte à l'autre (fidèle, gardé).
- `0xA4` : masque des couches de fond (bit 1 couche 0, bit 2 couche 1 ; une couche masquée est **figée** : ni avance ni
  tirage aléatoire) et cycle de palettes (second opérande) ; aucun site sur la chaîne ; 21 sites (337 à 345, 347, 471, 475).
- **DLL** : suivi caméra entier par tick (`AlundraCameraMath.cs:153-164`, `:191-208`, `:224-235`, `:277-293`, `:308-309`) ;
  `0x8E`, `0x8F`, `0xA4` sautés par leur taille ; aucun état de balancement ; le moteur n'a rien à faire pour le balancement
  (la DLL pose la cible de la caméra). Pour `0xA4`, `ScrollingLayerService` et `CellularLayerService` du moteur n'ont pas de
  couche active (manque réel : E19.k2).
- **Sites de la chaîne** : exactement les 28 lignes `0x8E`/`0x8F` de la liste fermée d'E19.e ; arcs concernés : A5 et A5r
  (`0x8E @20` épinglé « sauté une fois »), A6 (4 entrées `0x8E` dans son ensemble autorisé), A18 (178 `C[6]`). La 476 n'en a
  aucun : E19.k ne règle pas O-E19-30.

**Choix de conduite** (techniques) : découpage en E19.k1 (`0x8E`/`0x8F`, DLL seule, maintenant) et E19.k2 (`0xA4`, plan
moteur d'abord : couche active par identifiant, ni avance, ni dessin, ni tirage ; rapport de manque ; sans site sur la
chaîne, après les tranches qui débloquent l'histoire) ; Reach persistant d'une carte à l'autre, comme le binaire ; libellés
de `0x8E`, `0x8F`, `0xA4` corrigés dans E19.k1.

##### 1.2k.1 E19.k1 — Balancement de la caméra ✅ (recette K5 en attente)

**Relecture** (2026-10-03) : REVISE n°1 (site du pas sans caméra, tick de l'accroche, épingle d'A18) ; audit des valeurs :
toutes les valeurs de K1 et K2 confirmées par un modèle indépendant (`e19k-audit/model.py`), un P2 (le binaire ne balance
pas pendant le fondu de départ), trois P3 et six P4. Dispositions : les trois blocages, le P2 et les trois P3 **corrigés**
ci-dessous (K1, K3, K4, K2, K2b, K3 des tâches) ; P4-a et P4-b (deux termes additifs du suivi et autres écrivains natifs de
la structure) consignés en O-E19-35 ; P4-c (fréquences de la recette) corrigé en K5 ; P4-d (frame de rattrapage : tous les
scripts avant les pas de caméra, motif existant) en risque ; P4-e (remise à zéro) en K1 ; P4-f (« premier appel ») corrigé
dans la découverte.

**Règles d'exécution.**
- **K1 — État** : une classe `AlundraCameraSway` (nom indicatif) avec les neuf champs du binaire et son pas (K3) ; un
  constructeur interne pour les tests purs ; une instance de session `AlundraCameraSway.Instance` (modèle
  d'`AlundraScreenFadeDirector.Instance`), jamais sauvegardée. `InitializeWithWorld` ne remet que Flag à 0, **en tête,
  avant ses retours anticipés**. `ResetForTests` (les neuf champs à 0, Reach compris) est ajouté à
  `SaveGameDirectorTestSupport.ResetSingletons`, que les arcs appellent déjà (`AlundraArcSupport.cs:301`) : chaque arc part
  d'un état neuf.
- **K2 — Opcodes** : `0x8E` et `0x8F` dans `Dispatch` comme ci-dessus ; accès par un membre par défaut
  `IEntityWorldContext.CameraSway => null` (modèle de `ScreenFadeDirector`), implémenté par `AlundraWorldProxy` (qui rend
  `AlundraCameraSway.Instance`) ; sans état, repli dégradé journalisé une fois (modèle de `0xAF`) ; libellés de la table
  corrigés (« Camera sway start », « Camera sway stop », « Background layer mask ») ; `0x8E` et `0x8F` ajoutés à
  `IntroTraceHarnessTests.ImplementedOpcodes`.
- **K3 — Pas et site.** Le pas suit les règles exactes de la découverte (asymétrie de l'axe Y comprise : avec SpeedY nul,
  OffsetY est figé dans les bornes, mais borné avec bascule de Reach s'il est au-delà). Le binaire n'appelle le pas qu'au
  début du suivi caméra (premier appel, `0x8002CDD8`) : dans la DLL, **un seul site**, `AlundraCameraDirector.UpdateCameraFollow`,
  appelé une fois par frame dans le bloc caméra d'`AlundraWorldProxy.Update`, **après** la passe des événements de carte et
  celle des déclencheurs en attente (`AlundraWorldProxy.cs:2070-2113`), sans autre garde que celles du suivi (le bloc caméra
  ne passe pas par `gameplayBlocked` : inchangé). Il fait **exactement `ticksThisFrame` pas** : avec caméra, un pas par tick
  dans `AdvanceCameraSmoothing` (K4) ; sans caméra (`_debugCamera == null`, cas des arcs), les `ticksThisFrame` pas juste
  avant le retour anticipé. **Pendant une transition** (`AlundraWarpDirector.Instance.IsTransitionInProgress`, lu au bloc
  caméra) : aucun pas de balancement et aucun ajout du décalage ; le binaire sort de sa boucle d'images dès que le départ
  est armé (`0x8002C454`-`0x8002C45C`) et sa boucle de transition (`0x8002C490`-`0x8002C4C0`) n'appelle ni le rendu ni le
  suivi (audit : exact pour `0x53`, au plus un pas d'écart pour un portail). Le suivi lui-même pendant la transition reste
  tel qu'aujourd'hui (hors tranche).
- **K4 — Caméra.** `ComputeSmoothedCameraTarget` gagne un paramètre facultatif, le décalage du tick (espace de scroll),
  ajouté **entre le pas de suivi et la borne**, en rendu `X += OffsetX` et `Y −= OffsetY` (`ToOriginalScrollSpace` :
  scrollY = −renderY − 120), jamais sur l'accroche. `AdvanceCameraSmoothing` gagne un paramètre facultatif
  `AlundraCameraSway? sway = null` : à chaque tick de sa boucle, le pas de balancement puis le suivi avec le décalage de ce
  tick. **Accroche** : une frame d'accroche à N ≥ 1 ticks fait d'abord le tick d'accroche (pas de balancement, accroche à
  la cible bornée, ni suivi ni décalage), puis N − 1 ticks complets : N pas de balancement, N − 1 pas de suivi ; à 0 tick
  (fonctions pures seulement : le directeur n'accroche qu'avec un tick), l'accroche seule, sans pas. Sans balancement
  (`sway` nul, ou Flag 0 et état nul), rien d'observable ne change : après l'accroche, l'état est un point fixe du suivi
  vers la même cible (la cible est calculée une fois par frame), d'où les épingles existantes intactes. Le commentaire
  « No tick is consumed » (`AlundraCameraMath.cs:178-183`) est réécrit : l'accroche consomme le premier tick, comme le tick
  de chargement du binaire. **Signatures existantes gardées** (paramètres facultatifs) : `AlundraWorldProxyCameraFollowTests.cs`
  n'est pas touché.

**Tâches.**
- ✅ **K0 — Plan** : ce plan, relu jusqu'à READY (REVISE n°1 puis audit des valeurs ; READY à la deuxième relecture, le 2026-10-03).
- ✅ **K1 — État, opcodes, pas, tests d'abord.** Tests purs du pas, sur une instance neuve : `[1,1,3,2]` → OffsetX −1, −2, −3,
  −2, −1, 0, 1, 2, 3, 2, 1, 0 (ReachX passe à 1 au pas 3, à 0 au pas 9) et OffsetY −1, −2, −1, 0, 1, 2, 1, 0 (ReachY à 1 au
  pas 2, à 0 au pas 6) ; `[3,1,6,2]` → OffsetX −3, −6, −3, 0, 3, 6, 3, 0 ; `[8,1,8,2]` → −8, 0, 8, 0 ; `[13,11,1,2]` → OffsetX
  −1, 1, −1, 1 et OffsetY −2, 2 ; changement en cours (deux pas de `[1,1,3,2]` puis `[8,1,8,2]`) → (−8, Reach 1), 0, 8, 0 ;
  `0x8F` puis un pas : Speed, Limit, Offset à 0, Reach conservé ; asymétrie : SpeedX 0 → OffsetX 0 ; SpeedY 0 avec OffsetY 1,
  ReachY 0, LimitY 2 → OffsetY 1, ReachY 0 (figé) ; SpeedY 0 avec OffsetY −3, ReachY 0, LimitY 2 → OffsetY −2, ReachY 1 ;
  opérande 200 lu 200 ; chargement (`InitializeWithWorld`) : Flag 0, Reach conservé. Tests du runner : `0x8E` pose les
  quatre valeurs, `0x8F` pose Flag 0, tailles 5 et 1. Commit : `feat(alundra): port the camera sway opcodes 0x8E and 0x8F`
  - Fait le 2026-10-03 : 17 tests dans `AlundraCameraSwayTests.cs` ; rouges d'abord sur le code sans comportement (état et paramètres présents, `Step` vide, opcodes non dispatchés) : 16 rouges (valeurs lues : suites toutes à 0, `UnknownSkipped` au lieu de `Implemented`, Flag 1 après chargement) et 1 vert d'avance (`SpeedY` nul qui fige `OffsetY`, vrai trivialement avec un pas vide) ; tous verts après. Écarts : un seul constructeur interne sans paramètre (au lieu d'un privé plus un interne) ; setters internes pour poser l'état des tests ; `AlundraCameraSway` est publique car `IEntityWorldContext` l'est. Les deux libellés et `0xA4` de la table sont corrigés. Suite Debug après K1 : 3 rouges attendus de la liste fermée, `0x8E` étant désormais exécuté (A5 et A5r, `Rule2_EveryListedLineIsASiteReachedAndSkipped`), résolus par K3 ; le reste vert.
- ✅ **K2 — Caméra, tests d'abord.** Tests purs (cible fixe, sans bornes sauf mention, instance neuve, valeurs lues dans
  l'espace de scroll de l'original par `ToOriginalScrollSpace`, relatives au repos), un appel
  `AdvanceCameraSmoothing(…, ticksThisFrame: 1, …, sway)` par tick : `[1,1,3,2]` → X −1, −3, −6, −8, −9, −9, −8, −6, −3, −1,
  0, 0 ; Y −1, −3, −4, −4, −3, −1, 0, 0 ; avec l'accroche au premier appel (`needsSnap: true`, 1 tick) : X 0, −2, −5, −7, −8,
  −8, −7, −5, −2, 0, +1, 0 puis le régime ; une frame d'accroche à 3 ticks (un seul appel, `needsSnap: true`,
  `ticksThisFrame: 3`) → X −5, Y −3, Offset (−3, −1), Reach (1, 1) ; bornée à 0 (bornes de carte, repos au bord gauche) :
  0, 0, 0, 0, 0, 0, 1, 2, 4, 5, 5, 4, 2, 0 ; après `0x8F` à −9, la caméra reste à −9 (zone morte du `>> 4`, E5.c).
  **K2b — Câblage**, nouveau fichier `AlundraCameraSwayWiringTests.cs` (montage caméra d'`AlundraWorldProxyUpdateCharacterizationTests`
  : entité caméra, entité suivie immobile, `proxy.Update(0.02f)` = un tick ; état de session remis à zéro) : (a) caméra au
  repos (frames sans balancement jusqu'à l'arrêt), puis Flag 1 `[1,1,3,2]` posé sur `AlundraCameraSway.Instance`, puis trois
  frames → `Target` dans l'espace de scroll, relatif au repos : X −1, −3, −6 ; Y −1, −3, −4 ; (b) monde sans caméra, même
  état, trois frames → Offset (−3, −1), Reach (1, 1) ; (c) transition armée (montage d'`AlundraInventoryDirectorTests.cs:255-265`,
  `BeginDeparture`), état `[1,1,3,2]` après un pas, une frame → état de balancement inchangé, et avec caméra `Target`
  inchangé. (a) et (b) rouges avant K2 (aucun pas, aucun ajout), (c) vert avant (garde). Commit :
  `feat(alundra): the camera sway moves the scroll like the binary`
  - Fait le 2026-10-03 : 10 tests (`AlundraCameraSwayFollowTests.cs` 7, `AlundraCameraSwayWiringTests.cs` 3) ; rouges d'abord avec des paramètres facultatifs sans effet : 7 rouges (suites lues toutes à 0 au lieu de −1, −3, −6… ; Offset (0, 0) au lieu de (−3, −1) sans caméra ; Target inchangé au lieu de −1, −3, −6) et 3 verts d'avance : K2b (c) (garde, transition), l'accroche à 0 tick et le pas sans balancement inchangé (gardes) ; tous verts après. Montage : `AlundraCameraDirector.UpdateCameraFollow` lit `AlundraCameraSway.Instance` et `AlundraWarpDirector.Instance.IsTransitionInProgress` (pas de paramètre ajouté à sa signature) ; les paramètres facultatifs sont `ComputeSmoothedCameraTarget(…, swayOffsetX = 0, swayOffsetY = 0)` et `AdvanceCameraSmoothing(…, sway = null)`. Écart : la valeur « 0, −2, −5, … 0, +1, 0 » de l'accroche est tenue telle quelle ; le `+1` vient de ce que le suivi tire en arrière d'un pixel (`>> 4` d'un écart négatif vaut −1). Suite Debug après K2 : mêmes 3 rouges attendus qu'après K1 (A5, A5r, `Rule2`), résolus par K3.
- ✅ **K3 — Liste fermée et arcs.** Les 28 lignes `0x8E`/`0x8F` quittent `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv`
  (198 → 170) ; la 392 entre dans `MapsWithoutSkippedSite` (10 → 11 ; nom et doc du test,
  `AlundraStoryChainOpcodeAudit.cs:46-47`, `AlundraStoryChainSkippedOpcodesTests.cs:74-83`) ; A5 et A5r : `0x8E @20` exécuté à
  l'image 0, rien de sauté (lignes 91 et 143 changées, ensembles autorisés des lignes 142 et 186 resserrés), paramètres
  (1,1,3,2), Offset (−1, −1) après l'image 0, OffsetY −2 et ReachY 1 après l'image 1, OffsetX −3 et ReachX 1 après l'image 2,
  Flag 1 jusqu'au départ ; A6 : les 4 entrées `0x8E` quittent son ensemble autorisé (lignes 33, 36-37), paramètres (1,1,3,2)
  après l'image 0, (3,1,6,2) après `@335`, (8,1,8,2) après `@342`, Flag 1 jusqu'à la fin de l'arc (les images de `@335` et
  `@342` ne sont pas épinglées) ; A18 : (5,3,7,5) après `@608`, (13,11,1,2) après `@618`, Flag 0 après `@683`, puis au pas
  suivant Limit, Speed et Offset à 0 (Reach non épinglé : sa valeur dépend du nombre de pas depuis `@618`). Commit :
  `test(alundra): the camera sway on the story chain`
  - Fait le 2026-10-03 : les 28 lignes `0x8E`/`0x8F` retirées de la liste (198 → 170, aucune autre ligne ne bouge : 28 suppressions et rien d'autre au diff) ; `MapsWithoutSkippedSite` passe à 11 avec la 392 (nom du test renommé `…TheElevenMapsWithoutASiteHaveNone`). Rouges d'abord : A5, A5r et `Rule2_EveryListedLineIsASiteReachedAndSkipped` étaient rouges depuis K1 (`0x8E` exécuté) ; les pins réécrits (`0x8E @20` exécuté à l'image 0, rien de sauté, (1,1,3,2), Offset (−1,−1) / OffsetY −2 ReachY 1 / OffsetX −3 ReachX 1 après les images 0, 1, 2, Flag 1 jusqu'au départ ; A6 : (1,1,3,2) après l'image 0, (3,1,6,2) après `@335`, (8,1,8,2) après `@342`, Flag 1 à chaque image ; A18 : (5,3,7,5) après `@608`, (13,11,1,2) après `@618`, Flag 0 après `@683` puis tout à 0 à la fin de la même image) sont tous tombés verts du premier coup, aucune valeur écrite contredite. Écart : `0x8E @721` de A6 n'est pas atteint avant le départ (mesuré) : il sort de l'ensemble autorisé sans être épinglé. Suite Debug : 2371 tests, 0 échec ; aucun test hors de la liste fermée touché.
- ✅ **K4 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2344 → 2371 (+27), 0 échec, en Release
  (45 s) puis en Debug (42 s), la Debug en dernier ; `cmp` sans écart (sha256 `797de8ba...a719`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (douze mutations de production, toutes attrapées : double pas, garde de transition,
  décalage après la borne ou sur l'accroche, accroche sans tick, asymétrie de Y, Reach remis au chargement, signe de Y,
  aucun pas sans caméra, `OnMapLoad` absent ou après les retours anticipés, effacement de Flag 0 ; frames à plusieurs
  ticks par le vrai `Update`, avec et sans caméra). Avis reportés : **P3** les commits K1 et K2 laissent A5, A5r et la
  règle 2 rouges jusqu'à K3 (branche verte à K3, non bissectable entre les deux ; une écrasure avant le merge est à la
  main de l'auteur) ; P4 `0x8E @721` d'A6 n'est atteint par aucun arc ; P4 constructeur interne unique et mutateurs
  internes au lieu d'un constructeur privé plus un interne.
- 🧪 **K5 — Recette** (auteur) : la 392 (couloir du
  navire), la 391 (la coque cède), la 178 (séisme de Septimus) : comparer l'impression à l'original. À 50 ticks par seconde,
  `[1,1,3,2]` et `[3,1,6,2]` font une secousse de 4 à 6 Hz, `[8,1,8,2]` 12,5 Hz, `[5,3,7,5]` 8,3 Hz en X et 6,25 Hz en Y,
  `[13,11,1,2]` 25 Hz ; l'amplitude à l'écran est l'intégrale du décalage (0 à −9 px pour `[1,1,3,2]`) ; après `0x8F`, la
  caméra peut rester décalée de 15 px au plus (zone morte d'E5.c) ; aucun balancement pendant le fondu de départ.

**Acceptation d'E19.k1.**
1. Tests K1, K2 et K2b rouges d'abord (sauf K2b (c), garde), verts après, valeurs écrites tenues ; une valeur contredite est
   un arrêt.
2. Code de test existant touché, liste fermée : A5 et A5r (`AlundraShipCorridorArcTests.cs`), A6 (`AlundraShipBlockArcTests.cs`),
   A18 (`AlundraDay3SceneArcTests.cs`), la liste versionnée et `AlundraStoryChainOpcodeAudit.cs`/`AlundraStoryChainSkippedOpcodesTests.cs`
   (28 lignes, la 392 sans site), `IntroTraceHarnessTests.ImplementedOpcodes`, `SaveGameDirectorTestSupport.cs` (remise à
   zéro du singleton) ; rien d'autre (`AlundraWorldProxyCameraFollowTests.cs` et les autres tests de caméra inchangés) ; les
   six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette K5 faite par l'auteur.

**Risques.** Le décalage intégré ne revient pas toujours à zéro après `0x8F` (zone morte d'E5.c, jusqu'à 15 px) ; un état
de balancement oublié d'une carte à l'autre (Flag remis à 0 au chargement : gardé ; Reach persiste, comme le binaire) ; les
fonds suivent le scroll avec leur parallaxe : aucune épingle n'existe sur leur position dans les arcs ; sur une frame de
rattrapage à plusieurs ticks, les scripts de tous les ticks passent avant les pas de caméra (motif existant de la DLL ; les
arcs, à un tick par frame, n'en dépendent pas) ; les deux termes additifs du suivi et les écrivains natifs de la structure
(O-E19-35) restent à porter avec E14.

##### 1.2k.2 E19.k2 — Masque des fonds `0xA4` 🧪 (recette K2-4 en attente ; verifier frais K2-3 à lancer)

**Relecture** (2026-10-03) : REVISE n°1 (le masque du binaire porte sur les deux sortes de couches, défilement et
cellulaire, et la 475 masque une couche 0 cellulaire ; l'index d'une couche est son identifiant du binaire, pas sa place dans
le service, car l'export écarte des couches ; l'accès du runner à la scène des fonds n'était pas nommé) : corrigé ci-dessous.

**Faits porteurs** **[binaire]** (découverte d'E19.k, `e19k-disc/notes.md`, `dis_8005b848.txt`) : `0xA4 [b1, b2]`
(`0x80041098`, taille 3) appelle `0x8005D668(b1, b2)` : mode des fonds `0x8018678C = b1`, banque de palettes `0x80181BE4 = b2`,
`0x800C490C` (décalage de palette) remis à 0, et si `b2 > 0` un programme de cycle de palettes (`0x80186790`). Le dessin des
fonds (`0x8005B670`) ne traite la couche d'identifiant 0 que si `mode & 1` et la couche d'identifiant 1 que si `mode & 2`
(`0x8005B848`) ; chaque couche traitée l'est selon son octet de mode : 1 « tuiles » (`0x8005C294`, défilement), 2
« cellulaire » (`0x8005C8BC`) ; l'état par tick d'une couche (cadence d'animation, défilement automatique, vagues et tirages
aléatoires du cellulaire) est **dans** l'appel gardé : une couche masquée est **figée** et non dessinée, quelle que soit sa
sorte (**correction du 2026-10-03 : faux pour les vagues seulement, leur compteur est unique et hors de l'appel gardé, voir §1.2s.4 et ADR-0052 du moteur**) ; la surcouche (`0x8005BA40`) ne dépend que de l'en-tête de la carte. Au chargement, `SetScrollingMode(3, 0)`
(`0x8005B63C`) : les deux couches actives, banque 0. Le décalage `0x800C490C` s'ajoute à l'octet de palette des tuiles et des
couches cellulaires (`0x8005C574`, `0x8005CDBC`, `0x8005D544`) : le cycle de palettes touche toute la carte. Recensement : 21
sites (cartes 337 à 345, 347, 471, 475), aucun sur la chaîne ; à la 475, la couche 0 est cellulaire et `0xA4 [0,0] @52`,
`[3,0] @330`, `[0,0] @355` l'éteignent et la rallument.

**Choix de conduite** (techniques) : le masque est un manque du moteur (aucune couche active ou inactive dans
`ScrollingLayerService` ni dans `CellularLayerService`) : il se porte dans le moteur (branche dédiée du sous-module, ADR du
moteur), par une API générique keyée par l'identifiant de couche déjà porté par les définitions (`ScrollingLayerDefinition.
StableId`, `CellularLayerDefinition.LayerId`, posés depuis le `LayerId` du binaire par `AlundraBackdropStage`) ; la DLL traduit
les bits ; le cycle de palettes (`b2 > 0`) sort d'E19.k2 : consigné (O-E19-43), trace `Degraded`.

**Règles d'exécution.**
- **K2-R1 — Moteur** : `ScrollingLayerService.SetLayerActive(int stableId, bool active)` et `CellularLayerService.SetLayerActive(
  int layerId, bool active)` : chaque couche dont l'identifiant correspond prend l'état ; une couche inactive n'avance pas (ni
  `AdvanceLayerOneTick`, ni vagues, ni tirage aléatoire, son état figé) et ne se dessine pas (`ScrollingLayerComponent`,
  `CellularLayerComponent` : aucune soumission) ; un identifiant absent est sans effet ; `SetLayers` et `Clear` remettent toutes
  les couches actives. Tests du moteur à côté de `ScrollingLayerServiceTests` et `CellularLayerServiceTests` ; ADR du moteur.
- **K2-R2 — DLL** : membre par défaut `IEntityWorldContext.SetBackgroundLayerMask(int mask) => false` (« non traité ») ;
  `AlundraWorldProxy` l'implémente en le passant à son `AlundraBackdropStage`, qui appelle pour chaque identifiant `id` de 0 à 1
  `SetLayerActive(id, (mask & (1 << id)) != 0)` sur chacun des deux services attachés et rend `true` ; sans aucun service
  attaché, la scène journalise une fois « no layer service attached » et rend `true`. Le runner : `0xA4 [b1, b2]` appelle
  `SetBackgroundLayerMask(b1 & 3)` ; s'il rend `false` (contexte sans fonds), journal une fois et trace `Degraded` ; `b2 > 0` :
  journal une fois et trace `Degraded` (O-E19-43) ; taille 3. Au chargement, `SetLayers` remet tout actif (comme
  `SetScrollingMode(3, 0)`).

**Tâches.**
- ✅ **K2-0 — Plan**, relu jusqu'à READY (REVISE n°1, READY à la deuxième relecture le 2026-10-03).
- ✅ **K2-1 — Moteur, tests d'abord** (branche `chantier/e19k2-layer-mask` du sous-module, **empilée sur
  `chantier/e19s-virtual-resolution` (`dfaed7a6`)** : le pointeur du parent ne peut désigner qu'un commit et doit garder E19.s ;
  plan du moteur dans son `ai-agent/tasks/`, comme l'exige son `AGENTS.md`) :
  défilement, deux couches d'identifiants 0 et 1 avec défilement automatique et cadence : `SetLayerActive(1, false)` puis trois
  ticks → la couche 0 avance, la couche 1 garde ses compteurs et son défilement d'avant et n'est pas soumise ; puis
  `SetLayerActive(1, true)` → les deux avancent ; une seule couche d'identifiant 1 placée à l'index 0 : `SetLayerActive(0, false)`
  est sans effet, `SetLayerActive(1, false)` la fige et la cache ; `SetLayers` après un masque : toutes actives. Cellulaire, une
  couche d'identifiant 0 : `SetLayerActive(0, false)` puis trois ticks → positions, vagues et générateur aléatoire inchangés
  (aucun tirage), aucune soumission ; `true` → elle reprend. ADR du moteur. Commit (moteur) :
  `feat(rendering): scrolling and cellular layers can be switched off, frozen and not drawn`
  - Fait le 2026-10-03 : moteur, branche `chantier/e19k2-layer-mask` (empilée sur `chantier/e19s-virtual-resolution` `dfaed7a6`), trois commits :
    plan du moteur `7350f247`, K2-1 `79ef3532`, ADR-0049 `987f0c7f` (`ai-agent/tasks/e19k2-layer-mask-tasks.md`). API posée d'abord en
    talon sans effet (`SetLayerActive` ne fait rien, `IsLayerActive` rend vrai), puis 12 tests neufs (`ScrollingLayerMaskTests` 6,
    `CellularLayerMaskTests` 6) : 8 rouges d'avant (premier assert lu : `IsLayerActive` faux attendu, vrai lu ; compteur attendu 0, lu 2 ;
    soumission attendue vide, non vide), 4 verts d'avance (`SetLayers` et `Clear` remettent tout actif, vrais avec le talon). Après
    l'implémentation toutes les valeurs écrites tenues, aucune ré-épinglée. `IsLayerActive(index)` est le lecteur par position ajouté
    à l'API du plan (pour les tests et la DLL). Une couche inactive est figée en entier, décalages enroulés compris. Écart : aucun test
    existant du moteur touché ; `CasaEngine.Tests` 2490 puis **2502** sur 2502 (Debug), 0 échec.
- ✅ **K2-2 — DLL, tests d'abord** : runner avec un contexte de test qui enregistre les masques : `[0xA4, 1, 0, 0xFF]` → masque 1,
  `CodeIndex` 3 (rouge : sauté, aucun masque) ; `[0xA4, 2, 0, …]` → 2 ; `[0xA4, 0, 0, …]` → 0 ; `[0xA4, 7, 0, …]` → 3 ;
  `[0xA4, 3, 5, …]` → 3 et une trace `Degraded` ; contexte sans fonds (membre par défaut) → trace `Degraded`, un journal.
  Scène des fonds avec les deux services de test et la forme de la 475 (couche 0 cellulaire, couche 1 de défilement) : masque 0
  → les deux inactives ; masque 3 → actives ; masque 1 → seule la cellulaire active ; scène sans service → un journal, rien
  d'autre. Pointeur du sous-module mis à jour dans le même commit. Commit :
  `feat(alundra): port the background layer mask opcode 0xA4`
  - Fait le 2026-10-03 : 16 tests neufs dans `AlundraBackgroundLayerMaskTests` (runner : 8, scène des fonds : 8). Rouges d'avant (stubs :
    membre d'interface par défaut et `SetLayerMask` sans effet) : les 16 rouges ; l'opcode lu comme `UnknownSkipped` (attendu `Implemented`
    ou `Degraded`), `CodeIndex` 3 déjà tenu, aucun masque enregistré ; `SetLayerMask` lu faux (attendu vrai). Verts après : toutes les
    valeurs écrites tenues (modes 1, 2, 0, 7 → masques 1, 2, 0, 3 ; `[3, 5]` → masque 3 et `Degraded` ; contexte sans fonds → `Degraded`,
    un journal ; la 475 : masque 0 → les deux inactives, 3 → actives, 1 → seule la cellulaire (identifiant 0) active ; sans service : un
    journal, rendu vrai), aucune ré-épinglée. Un mode au-delà de 3 (`0xFC`) est masqué par `& 3` (cas ajouté). Écarts : (1) `b2 > 0` avec
    un contexte qui ne traite pas les fonds ne journalise que la dégradation « sans fonds » (une seule des deux causes, même trace) ;
    (2) la clé de journal « une fois » du cycle de palettes est `0x1A4`, distincte de celle de `0xA4`. Test existant touché :
    `IntroTraceHarnessTests.ImplementedOpcodes` (`0xA4` ajouté) seulement. `Alundra.Tests` 2523 puis **2539** sur 2539 (+16), 0 échec, en
    Release (54 s) puis en Debug (54 s), la Debug en dernier ; `cmp` de `Alundra/bin/Debug/net9.0-windows/Alundra.dll` et
    `alundra-project/Alundra.dll` sans écart (sha256 `cf538c38...`) ; les six traces à l'octet (`git diff --ignore-cr-at-eol` rend 0, les
    quatre traces du héros remises par `git checkout`) ; `story-chain-skipped-opcodes.tsv` non touché.
- ⏳ **K2-3 — Vérification et clôture.** **K2-4 — Recette** (auteur, hors chaîne) : la 475, la couche cellulaire s'éteint et se
  rallume avec la scène ; une carte de 337 à 345.

**Acceptation d'E19.k2.**
1. Tests de K2-1 et K2-2 rouges d'abord, verts après, valeurs écrites tenues.
2. Code de test existant touché : `IntroTraceHarnessTests.ImplementedOpcodes` ; côté moteur, aucun ; les six traces à l'octet ;
   la liste fermée de la chaîne inchangée.
3. `CasaEngine.Tests` sans échec ; `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette K2-4 faite par l'auteur.

**Risques.** Le cycle de palettes de la 471 reste absent (O-E19-43).
- ✅ **Vérification d'E19.k2** (2026-10-03) : `CasaEngine.Tests` 2502/2502 ; `Alundra.Tests` 2539/2539 en Release puis en Debug, la
  Debug en dernier ; `cmp` sans écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** (binaire relu : `0x80041098` lit
  b1 et b2 non signés, `0x8005D668`, porte de dessin `0x8005B6F8`-`0x8005B744` ; à la 475, couche 0 cellulaire et couche 1 absente,
  comme le plan ; mutations du moteur, le talon et la porte de gel ou de soumission retirée, 8 rouges sur 12 chacune ; mutation de
  la DLL, 15 rouges sur 16). Avis : P3 `SetLayerMask_AfterAMask_ALaterMaskOfThreeTurnsEverythingBackOn`
  (`AlundraBackgroundLayerMaskTests.cs:206-216`) ne peut pas échouer (il ne vérifie ni l'état intermédiaire ni le retour) : « 16
  rouges » valait 15 → E19.m ; P4 `0x8005D668` écrit aussi 1 en `0x800C4908` (sans doute un drapeau de palette à refaire) → O-E19-43.
  **Reste la recette K2-4 de l'auteur.** Branche du moteur `chantier/e19k2-layer-mask` (`987f0c7f`, empilée sur E19.s), non mergée.

### 1.2l E19.j — Réarmement des événements de carte hors zone ✅ (recette J3 en attente)

**Découverte** (2026-10-03, lecture seule ; notes et scripts dans le scratchpad de la session, `e19j-disc/`). Faits porteurs
**[binaire]** :
- `RunMapEvents` (`0x8003C67C`), une fois par tick avant `UpdateEntities`, garde globale `g_playerControlFlags & 0x48`.
  Table des créneaux en `0x8013C688` (`0x40` créneaux de `0x48` octets : +0 id, +4 enregistrement, +8 octet de programme B,
  +0xC entité logique, +0x10..+0x47 état du programme : +0 entrée, +4 pc, +8 clé d'attente, +0xC compteur de `0x37`, +0x2C
  `Result`, +0x30 boucle, +0x34 retour). Ordre par créneau : un octet `& 0x7F == 0` est sauté **avant** le test de zone ; le
  test de zone est une case **inclusive** sur TileX/TileY du héros, ni Z ni drapeau.
- **Hors zone, à chaque tick** (`0x8003C7F0`-`0x8003C804`) : entrée = 0, pc = 0, `Result` = 0, entité logique = le héros,
  octet de programme = l'octet de l'enregistrement ; **aucune entité n'est écrite** ; les autres champs de l'état restent.
  À la réentrée, `RunScript` B (`0x800420DC`) relance le programme depuis le début (entrée ou pc nul) : un programme quitté
  est interrompu et recommence ; un programme garé sur `0xFF` se rejoue à chaque réentrée.
- **DLL** (`AlundraWorldProxy.cs:2309-2359`, hors zone `:2335-2338`) : suit la décompilation : écrit sur l'entité logique
  (`ChildEntity`, `Sp`, `RelativeWarpOffsetX`, `Index`) et ne remet ni `EventData`, ni l'entité logique, ni l'octet de
  programme : un programme quitté reprend au lieu de recommencer, un programme fini ne se rejoue jamais. `BuildMapEvents`
  (`:1845-1877`) ne garde pas l'octet d'origine de l'enregistrement. Le commentaire `:2344-2348` et la doc du test `:390-397`
  disent que la table des créneaux n'est pas compactée ; le binaire la compacte (`0x8003C5F4`-`0x8003C608`) ; sans effet
  (aucun enregistrement à octet nul sur 1714).
- **Recensement** : 1714 enregistrements, 619 à zone bornée dans 190 cartes ; sur les 30 cartes de la chaîne, 8, et **aucune
  réentrée sans rechargement** de carte (pièces closes, sorties par portails), sauf 476 `B[3]`, un programme vide : **aucun
  effet sur l'histoire**. Hors chaîne, environ 134 à 159 enregistrements dans 48 à 57 cartes se rejoueront à la réentrée
  (modèle approché ; exemple : Torla 445, enregistrement 0, son 216).
- Aucun arc ni test ne fait sortir puis rentrer le héros d'une zone.

**Choix de conduite** (techniques) : port fidèle du réarmement ; l'écart général d'`InitializeEventData` (effacement des
paramètres au redémarrage d'un programme, alors que le binaire garde +8..+0x28, D7 de la découverte : 19 programmes B à
zone bornée et 243 programmes C commencent par `0x37`) n'est pas corrigé ici : **consigné, O-E19-34**, pour E19.m ; le
commentaire faux sur la compaction est corrigé ici ; une relecture hors chaîne qui serait un vrai défaut de l'original se
corrige au cas par cas (règle de l'auteur), pas en bloc.

**Règles d'exécution.**
- **J-R1** : `AlundraMapEvent` garde l'octet de programme d'origine de l'enregistrement, **obligatoire à la construction**
  (posé par `BuildMapEvents` ; un oubli ne doit pas pouvoir désactiver l'événement).
- **J-R2 — Hors zone** (après le saut `& 0x7F == 0`, qui reste avant le test de zone) : `EventData.Sp = 0`,
  `EventData.Codes = null`, `EventData.CodeIndex = 0` (la forme déjà utilisée pour « entrée et pc à 0 »,
  `AlundraEventProgramRunner.cs:450-451`, `:472-473`), `EventData.Result = 0`, entité logique de l'événement = le héros,
  `ProgramBMap` = l'octet d'origine ; **plus aucune écriture sur une entité**. Le runner n'est pas touché.
- **J-R3** : le commentaire `AlundraWorldProxy.cs:2344-2348` et la doc du test `:390-397` disent la compaction du binaire.

**Tâches.**
- ✅ **J0 — Plan**, relu jusqu'à READY (READY à la première relecture ; audit des valeurs : les sept valeurs attendues et
  rouges confirmées, montage précisé).
- ✅ **J1 — Réarmement, tests d'abord** (montage des tests de la passe, `AlundraWorldProxyEventPassTests` ; sauf mention :
  programme B au masque 1, table B `{99, 0}`, zone (0,0)-(10,10), héros en (5,5) dans la zone, en (50,50) hors zone) :
  - **J1-a** `{1A 7, 00, 1A 8, FF}` : dedans, animation remise à 0, dehors, dedans → animation 7 ; hors zone : `Codes` null,
    `Result` 0, entité logique = le héros, `ProgramBMap` 129 (rouge aujourd'hui : 8) ;
  - **J1-b** `{1A 7, FF}` : dedans, dedans, dehors, dedans → 7, 0 (remise à 0 entre-temps), —, 7 (rouge : 0) ;
  - **J1-c** `{43 5, 00, 1A 7, FF}` : dedans, dehors, dedans, dedans → l'entité logique revient au héros ; le PNJ (préparé
    avec `RelativeWarpOffsetX` 42, `Index` 3, un `ChildEntity`, `Sp` 0xAB) et le héros (`RelativeWarpOffsetX` 17, `Index` 9)
    gardent ces valeurs ; animation 7 sur le PNJ au 4e tick (rouge : entité réécrite, 7 dès le 3e tick) ;
  - **J1-d** `@0 03[6,0], @3 1A 7, @5 FF, @6 1A 8, @8 FF` : `Result` 1 au départ, dehors, puis dedans → 7 (rouge : 8) ;
  - **J1-e** `ProgramBMap` 0x80 hors zone : reste 0x80 (sauté avant le test de zone) ; `ProgramBMap` 130 hors zone : revient
    à 129 (rouge : 130) ;
  - **J1-f** zone (2,3)-(4,6) : (4,6) et (2,3) dans la zone, le programme tourne ; (5,6), (4,7), (1,3) hors zone, réarmement ;
  - **J1-g** (réel, hors chaîne) : carte 445, enregistrement 0, zone (1,8)-(13,59), octet 129 : dedans (5,20), dedans, dehors
    (20,20), dedans → trace `BD @160`, `BF @163`, fin `@168` ; puis fin `@168` ; puis rien ; puis `BD @160`, `BF @163`, fin
    `@168` (rouge : fin `@168` seule au dernier tick).
  - **Montage** (audit des valeurs du 2026-10-03 : les sept valeurs attendues et rouges confirmées) : vrai runner
    (`new AlundraEventProgramRunner(doc, new AlundraGameState(), world)`, comme
    `AlundraEventProgramRunnerLogicEntityTests.cs:163-168`), monde avec `PlayerEntity` = le héros ; position par les champs
    `TileX`/`TileY` du héros ; `playerControlFlags` à 0 ; un appel de `RunMapEventsPass` = un tick. J1-c : héros en `Status`
    Normal, PNJ d'`EntityRefId` 5 dans `Spawned` ; l'entité logique se lit sur `mapEvent.Entity` ; `Sp` 0xAB posé sur
    `npc.EventProgramState.Sp` ; le rouge se lit aussi à l'issue du 3e tick (animation du PNJ 0, aujourd'hui 7). J1-b : la
    remise à 0 de l'animation après le 1er tick est porteuse. J1-d : `Result` 1 posé sur `mapEvent.EventData.Result`.
    J1-e : la moitié 0x80 est verte aujourd'hui, seule la moitié 130 est rouge (octet d'origine 129 dans les deux cas).
    J1-f : chaque position sur un événement neuf, seul « tourne ou non » est vérifié. J1-g : l'enregistrement 0 seul (en
    (20,20) l'enregistrement 1 est dans sa zone), trace vidée à chaque tick, `BD` et `BF` tracés `Degraded` sans lecteur
    de son ; données de `alundra-project/` par `MapEventProgramLoader.Load(root, "Torla (inner)-445")`.
  Tests existants touchés : `RunMapEventsPass_PlayerOutOfZone_ResetsLogicEntityState_AndDoesNotRun`
  (`AlundraWorldProxyEventPassTests.cs:419-435`, réécrit au comportement du binaire) ; la doc du test `:390-397` (J-R3) ; la
  construction de `AlundraMapEvent` (octet d'origine) dans `NewMapEvent` (`:365-367`),
  `AlundraEventProgramRunnerLogicEntityTests.cs:171` et `:344`, `AlundraEventProgramRunnerTests.cs:3051`,
  `IntroTraceHarnessTests.cs:681-690` (construction seulement, aucune assertion). Commit :
  `feat(alundra): rearm map events out of zone like the binary`
  - Fait le 2026-10-03 : champ `OriginalProgramBMap` (`required`) ajouté d'abord, passe inchangée ; 11 tests neufs (J1-a à J1-g, J1-f en 5 cas) et le test existant réécrit
    (`RunMapEventsPass_PlayerOutOfZone_RearmsTheSlot_AndWritesNoEntity`). Rouges d'avant : 6 des 7 tests J1 plus le réécrit ; J1-f vert (5 cas), moitié 0x80 de J1-e verte.
    Le premier assert rouge est parfois antérieur à celui du plan (J1-a : `Codes` non nul, J1-c : l'entité logique reste le PNJ, J1-d : `Result` reste 1) ; J1-e lit 130 au lieu de
    129 ; J1-g lit `0xFF @168` seul au dernier tick, comme écrit. Verts après : toutes les valeurs tenues, aucune ré-épinglée. `Alundra.Tests` Debug 2344 réussis, 0 échec. Écarts : aucun
    (`OriginalProgramBMap` en champ `required` public, comme les autres champs du type).
- ✅ **J2 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2333 → 2344 (+11), 0 échec, en Release
  (44 s) puis en Debug (46 s), la Debug en dernier ; `cmp` sans écart (sha256 `3b554ab0...4a1e`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (rouges d'avant reproduits en remettant l'ancien bloc ; neuf mutations d'une ligne,
  toutes attrapées ; au retour dans la zone, redémarrage depuis `table[octet & 0x7F]` et réécriture du créneau en fin de
  passage ; aucun autre lecteur des champs du créneau, rien dans la sauvegarde ; J1-g contre l'export réel de la 445).
  Avis P4, reportés : J1-f ne teste pas la borne juste sous Y1 ; les remises à 0 de `Sp` et `CodeIndex` ne sont épinglées
  que par le test réécrit (`InitializeEventData` les réécrit de toute façon).
- 🧪 **J3 — Recette** (auteur, hors chaîne) : Torla 445, sortir de la zone puis y revenir rejoue le son 216 ; une partie
  de la chaîne sans écart.

**Acceptation d'E19.j.**
1. Tests J1 rouges d'abord (sauf J1-f, garde des bornes), verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. Code de test existant touché : exactement la liste de J1 ; tous les arcs et toutes les autres épingles inchangés ; les six
   traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette J3 faite par l'auteur.

**Risques.** Un événement sans octet d'origine serait désactivé pour toujours (J-R1 obligatoire, J1-e) ; hors chaîne, des
sons, dialogues et scènes se rejouent à la réentrée comme dans l'original (arènes 323, 324, 327, la 86, les maisons de
Torla) ; le modèle de réentrée est approché ; le moment où TileX change après un `0x64` pris dans le même passage n'est pas
vérifié (sans effet sur la chaîne).

### 1.2m E19.l — Prédicats, branches et restes ✅ (recettes L1-4 et L2-4 en attente ; `0x87`/`0x95` → E14, `0xB9` → auteur)

**Découverte** (2026-10-03, lecture seule, deux surfaces : A branches, entités et contrôle ; B objets, argent et coffres ;
notes, scripts et recensements dans le scratchpad de la session, `e19l-disc/A/notes.md` et `e19l-disc/B/notes.md`). Faits
porteurs **[binaire]** (table des gestionnaires en `0x80098FAC + 4*op` ; les tailles du binaire égalent celles de la DLL pour
les 19 opcodes ; aujourd'hui la DLL les saute tous par leur taille, `Result` laissé tel quel) :
- **`0x28`/`0x29`/`0x2A`/`0x2B`** (`0x8003DC24`-`0x8003DC6C`) : posent ou effacent ClassB (bit 3) et ClassA (bit 0) des
  drapeaux de l'entité logique, rien d'autre. Seul site de la chaîne : 10 `C[75] @6413` (`0x2B`, S), sans effet visible.
- **`0x3F`** (`0x8003E734`) : `Result` = 1 s'il existe, parmi les créneaux 0 à N **inclus** (le héros compris), une entité
  d'état 2 ou 3 (Normal, Deactivated), sans `BlockedByEntity`, dont le `RidingEntity` est l'entité logique ; sinon 0. La
  décompilation s'arrête avant N et accepte l'état Loaded : le binaire l'emporte. 211 sites, dont 205 interrupteurs au sol ;
  aucun sur la chaîne.
- **`0x4A`** (`0x8003E9EC`) : `Result` ≠ 0 → retour à l'entrée du programme (pas = entrée − pc) ; sinon avance de 1.
- **`0x52`** (`0x8003EB20`) : portail sous le héros (`GetActivatedPortal` `0x8003166C`, 64 enregistrements, case inclusive,
  premier trouvé, **sans test de `DestMapId`**) → `HandleWarpTransition(portail, hero.TargetAnimationId, hero.TargetDirection)`
  et `Result` = 1, même quand le warp est désactivé (la transition rend la main aussitôt) ; sinon `Result` = 0 ; taille 1.
  Sites de la chaîne : 162 `B[2] @216`, 135 `B[13] @909` (X).
- **`0x57`/`0x58`** (`0x8003EE28`/`0x8003EE5C`) : saut relatif signé lu à `v[1 + 2d]`, `d` = `AnimationDirection` du héros
  (`0x57`) ou de l'entité logique (`0x58`) ; **jamais de chute** au pc + 9. Sur la chaîne, 14 sites de `0x58`, dont 164
  `C[4]`/`C[5] @110` (S) : Beaumont et Thyea apparaissent face en bas (d = 0), le binaire prend `@124` (ils restent face en
  bas), la DLL prend `@119` à chaque passage (face à gauche dix ticks, puis en bas) : **défaut du port**, visible.
- **`0x5D`** (`0x8003F144`) : toutes les correspondances de la recherche `v1` passent à l'état 3 (Deactivated) ; taille 2.
- **`0x8C`** (`0x80040438`) : un tirage du générateur partagé (`0x80098708`, mêmes constantes et même graine initiale que
  `AlundraRandom`) ; `Result` = 1 si `(graine >> 24) >= v1`, sinon 0 (le libellé « < » est inversé).
- **`0x82`** (`0x8003FE7C`) : `Result` = `HandleMapTriggerCommand(v1) != 0` (`0x80034108`) : id 0 → 0 ; `0x45`-`0x48` →
  argent +1, +5, +10, +30 (bornes 0 à 9999 sur 32 bits) ; `0x4F` → `FalconTemp` + 1 plafonné à 50 et G1450 posé ; `0x50`-
  `0x56` → fonctions sur le héros (vie, magie, effets ; deux corps seulement lus dans le binaire), rend 1 ; tout autre id →
  `GetNumberOfItem(id) < AddOneItemIfUnlocked(id)`. **`AddOneItemIfUnlocked`** (`0x8004E530`) rend le **compte** quand
  l'objet est au maximum (`0x8004E58C`, `0x8004E5B0`) ; la décompilation et la DLL (`AlundraPlayerManager.cs:1066`) rendent
  l'id : porté tel quel, `0x82` donnerait `Result` 1 sans rien donner. Sites de la chaîne : 163 `@297`, `@413`, `@529` (O,
  la boutique), 10 `@995` (X).
- **`0x83`** (`0x8003FEC8`) : `Result` = `GetNumberOfItem(v1) >= v2`. **`0x84`** (`0x8003FF34`) : `UseItem(v1)` (compte − 1
  s'il est positif) ; `Result` = 0 si le compte était nul, sinon 1 (un id invalide rend 1). Sites de la chaîne : 179 `B[1]
  @183` (S, son `Result` est écrasé par `0x44 @287` avant toute lecture), 10 `@707`, 135 `@1421` (X).
- **`0x98`** (`0x80040A58`) : argent + `v1 | v2 << 8`. **`0x99`** (`0x80040A8C`) : si argent < prix, `Result` 0 ; sinon argent
  − prix et `Result` 1. **`0x9A`** (`0x80040B00`) : `Result` = argent >= prix, rien d'écrit. Prix non signé sur 16 bits.
  Sites de la chaîne : 163 (O, la boutique). Aucun son, aucun appel au HUD (le HUD suit déjà `PlayerStats.Money`).
- **`0x9F`** (`0x80040C80`) : `Result` = 1 si la première correspondance de la recherche `v1` a un `ContentsGameFlag` non
  nul et que ce drapeau est posé ; **rend toujours 2, n'attend jamais** (l'attente est la forme des scripts, `00; 9F; 04`).
  Aucun site sur la chaîne. La moitié « drapeau » d'`InitializeContents` est déjà portée (`EntityRecordMapper.cs:170-175`) ;
  les coffres eux-mêmes (natifs, contenu tiré au hasard) sont d'E14.
- **`0x87`/`0x95`** : prédicats d'attaque (« touché par une arme de tel type », « une attaque active dans une boîte ») ; les
  données qu'ils lisent (`TouchingEntity`, attaque courante, boîtes de coup) ne sont écrites par rien dans la DLL (E14). Sur
  la chaîne, 10 `C[75] @6418` (`0x95`, S) rend 0 dans toutes les scènes, ce que donne déjà le saut.
- **`0xB9`** (`0x80041A18`) : lance la piste XA `v1` (table de 13 entrées en `0x800C4818`, pas un index de carte) ; la piste
  11 suit chaque objet reçu (fanfare, d'après le contexte) ; `0xBA` en rend l'état. Aucun XA nulle part (convertisseur, DLL,
  moteur) ; l'extraction actuelle a perdu l'audio des secteurs XA.
- **Sauvegarde (E16)** : toutes les écritures restent dans le domaine du format (argent 0 à 9999, comptes entre 0 et le
  maximum, drapeaux dans les 64 mots) : ni champ nouveau, ni changement de version (ADR-0012).

**Choix de conduite** (techniques) :
- découpage : **E19.l1** (classes, `0x3F`, `0x4A`, `0x57`/`0x58`, `0x5D`, `0x8C`, et `0x52` avec une entrée du directeur de
  warp) puis **E19.l2** (objets, argent, `0x9F`, correction d'`AddOneItemIfUnlocked`), exécutées l'une après l'autre (elles
  partagent la liste fermée et le test statique), après E19.k1 ;
- `0x28`/`0x29` viennent avec `0x2A`/`0x2B` (O-E19-26 groupe les quatre) ;
- `0x87`/`0x95` restent sautés jusqu'à E14 (les champs qu'ils lisent n'existent pas ; O-E19-37) ; `0xB9` reste sauté en
  attendant l'auteur (O-E19-36) ;
- `0x82` sous-cas `0x50`-`0x56` : `Result` 1, rien d'écrit, trace `Degraded` (l'emplacement de la vie du héros dans le binaire
  n'est pas établi ; O-E19-39) ;
- le scanner de portails partagé garde l'arrêt de la décompilation sur `DestMapId == 0` (aucun portail exporté n'en a :
  sans effet ; O-E19-38) ;
- les libellés faux des opcodes portés sont corrigés dans leur sous-tranche (aucun test ni aucune trace ne les épingle) ;
- **boutique de la 163** : une fois `0x9A`/`0x82`/`0x99` portés, un achat demande de l'argent ; sans source d'argent avant
  E14, la boutique répond « pas assez d'argent » (dialogue 140), comme l'original avec une bourse vide, au lieu du « merci »
  sans rien donner d'aujourd'hui (le port suit le binaire : à la recette, pas une question).

##### 1.2m.1 E19.l1 — Classes, branches et portail scripté ✅ (recette L1-4 en attente)

**Relecture** (2026-10-03) : REVISE n°1 (les tests fabriqués de la règle 4 rougissent quand 164 quitte les sites connus) ;
corrigé en L1-2 (option : `PredicateOpcodes` inchangé, entrées des tests réécrites sur les sites restants). Audit des
valeurs : toutes les valeurs de L1-1, L1-2, L2-1 et L2-2 confirmées contre le binaire et la DLL (`e19l-audit/`), le parcours
du test statique refait indépendamment (170 lignes aujourd'hui, 153 puis 140 après) ; préconditions de montage ajoutées,
test de `0x4A` renforcé, prémisse de l'épingle d'A11 écrite.

**Règles d'exécution.**
- **L1-R1 — Classes** : `0x2A` `Flags |= ClassA`, `0x2B` `Flags &= ~ClassA`, `0x28` `Flags |= ClassB`, `0x29`
  `Flags &= ~ClassB`, sur l'entité logique, puis `ResyncControllerFromFlags()` (comme `0x16`/`0x17` et `0x62`/`0x63`) ;
  taille 1.
- **L1-R2 — `0x3F`** : parcours du héros (s'il existe) puis des entités apparues ; `Result` = 1 au premier qui est Normal ou
  Deactivated, sans `BlockedByEntity`, et dont `RidingEntity` est `entity.LogicContextEntity` (même comparaison que `0x3E`) ;
  sinon 0 ; taille 1.
- **L1-R3 — `0x4A`** : `Result` ≠ 0 → pas = `Parameters[0] − CodeIndex` (comme `0x49`/`0x4B`) ; sinon 1.
- **L1-R4 — `0x57`/`0x58`** : `d` = `AnimationDirection` du héros (`0x57` ; sans héros, le repli de `0x3E`) ou de l'entité
  logique (`0x58`), pas = `(short)(v[1 + 2d] | v[2 + 2d] << 8)`, sans chute. Le parcours du test statique
  (`AlundraStoryChainOpcodeAudit.WalkProgram`) suit les quatre cibles et cesse de suivre la chute pour ces deux opcodes.
- **L1-R5 — `0x5D`** : chaque correspondance de `GetMatchingEntitiesBySearchType(entity, v1)` passe à `Deactivated` ; taille 2.
- **L1-R6 — `0x8C`** : `Result` = `((uint)AlundraRandom.Next() >> 24) >= v1 ? 1 : 0` ; taille 2.
- **L1-R7 — `0x52`** : portail par `AlundraPortalScanner.FindPortalAtTile(host.Portals, héros.TileX, héros.TileY)` ; trouvé →
  départ par `AlundraWarpDirector.BeginDeparture` avec l'animation et la direction **courantes du héros**
  (`TargetAnimationId`, `TargetDirection`) au lieu de `0x36` et de la direction de l'appelant (nouveau paramètre, la marche
  sur un portail garde `0x36`), puis `Result` = 1 (même warp désactivé : `BeginDeparture` rend la main, `Result` 1) ; pas de
  portail → `Result` 0 ; taille 1. Sans hôte ou sans héros : repli dégradé journalisé une fois, `Result` 0.
- **L1-R8 — Libellés** : `0x8C` « If random >= value », `0x52` « Use portal under hero », `0x57` « Goto by hero animation
  direction », `0x58` « Goto by animation direction » ; `IntroTraceHarnessTests.ImplementedOpcodes` reçoit les opcodes portés.

**Tâches.**
- ✅ **L1-0 — Plan**, relu jusqu'à READY (REVISE n°1, audit des valeurs, READY à la deuxième relecture le 2026-10-03).
- ✅ **L1-1 — Opcodes, tests d'abord** (nouveaux fichiers de tests, montage d'`AlundraEventProgramRunnerTests` ; `Result` de
  départ 7 sauf mention ; rouge = la valeur d'aujourd'hui, l'opcode sauté) :
  - classes : `[0x2A, 0xFF]`, `Flags` 0x108 → 0x109, `CodeIndex` 1 (rouge 0x108) ; `[0x2B, 0xFF]`, `Flags` 0x101 et masque du
    contrôleur 0x1040 → 0x100 et 0x40 ; `[0x28, 0xFF]`, 0x100 et 0x40 → 0x108 et 0x41 ; `[0x29, 0xFF]`, 0x109 et 0x1041 →
    0x101 et 0x1040 (rouges : drapeaux et masque inchangés) ;
  - `0x3F` `[0x3F, 0xFF]` : un PNJ apparu, Normal, qui porte l'entité (`RidingEntity` = `entity.LogicContextEntity`), `Result`
    0 avant → 1 (rouge 0) ; le même en Deactivated → 1 ; en Loaded, `Result` 1 avant → 0 (rouge 1 ; la décompilation dirait
    1) ; le héros porteur, Normal → 1 (rouge 0) ; un porteur avec `BlockedByEntity` posé, `Result` 1 avant → 0 ; personne,
    `Result` 1 avant → 0 (rouge 1) ;
  - `0x4A` `[0x00, 0x4A, 0xFF]` lancé à `CodeIndex` 1, `Parameters[0]` 0 : `Result` 1 → `CodeIndex` 1 et la trace montre
    `0x00 @0` exécuté (saut à 0, `0x00` avance et finit l'appel ; rouge : `CodeIndex` 2, pas de `0x00 @0`) ; `Result` 0 → `CodeIndex` 2 (garde, vert avant) ;
  - `0x58` sur `[0x58, 9,0, 12,0, 15,0, 18,0, 0x1A,1, 0xFF, 0x1A,2, 0xFF, 0x1A,3, 0xFF, 0x1A,4, 0xFF]` : `AnimationDirection`
    de l'entité 0, 1, 2, 3 → animation 1, 2, 3, 4 et `CodeIndex` 11, 14, 17, 20 (rouge : 1 et 11 pour tout `d`) ; saut
    négatif `[0xFF, 0x58, 0xFF,0xFF, 0xFF,0xFF, 0xFF,0xFF, 0xFF,0xFF, 0xFF]` lancé à 1 → `CodeIndex` 0 (rouge 10) ; `0x57` sur
    le premier programme, entité en `d` 0, héros en `d` 2 → animation 3, `CodeIndex` 17 (rouge 1, 11) ;
  - `0x5D` `[0x5D, 10, 0xFF]` : un PNJ d'`EntityRefId` 10, Normal → Deactivated, `CodeIndex` 2 (rouge Normal) ; deux
    correspondances → les deux Deactivated ; aucune → rien, `CodeIndex` 2 ;
  - `0x8C` (graine posée à `0xB017C93D`, rendue par `AlundraRandom.Reset()` en fin de test) : `[0x8C, 128, 0xFF]` → `Result`
    0 et graine 32 bits `0x35E36190` (premier tirage, r = 53) ; `v1` 53 → 1 ; `v1` 54 → 0 ; deux appels à `v1` 128 → 0 puis 1
    (r = 200) (rouges : `Result` 7, graine inchangée) ;
  - `0x52` `[0x52, 0xFF]` (hôte de test avec un portail dont la case contient le héros ; héros `TargetAnimationId` 44,
    `TargetDirection` 2) : `Result` 0 avant → 1, `CodeIndex` 1, départ armé (`IsTransitionInProgress`) avec l'animation
    d'arrivée 44 et la direction 2 (rouge : `Result` 0, aucun départ) ; héros hors de tout portail, `Result` 1 avant → 0
    (rouge 1) ; warp désactivé, `Result` 0 avant → 1, aucun départ (rouge 0). Le chemin de la marche sur un portail garde
    l'animation `0x36` (tests existants du directeur inchangés).
  - **Montage** (audit des valeurs) : l'entité qui exécute est Loaded, Normal ou Deactivated (sinon la recherche par id de
    `0x5D` ne trouve rien) ; les tests de classes ont un contrôleur vivant, masque initial posé sur ses réglages vivants
    (piège du setter qui clone) ; `0x57` et `0x52` ont un héros `PlayerEntity` du monde de test ; `0x8C` compare
    `(uint)AlundraRandom.RandomSeed` (la graine stockée est sur 64 bits), un état neuf ou `CodeIndex` remis à 0 pour le
    deuxième appel, et la classe dans la collection `AlundraRandomStaticStateCollection` (comme `AlundraRandomOpcodeTests`) ;
    `0x52` : portail de test de `DestMapId` non nul et < 500, directeurs de warp, de fondu et de musique remis à zéro, dans
    la collection `AlundraMusicPlayerSingletonCollection`.
  Commit : `feat(alundra): port the class, branch, riding, random and scripted-portal opcodes`
  - Fait le 2026-10-03 : 30 tests neufs en 3 fichiers (`AlundraClassAndBranchOpcodesTests.cs` 22, `AlundraRandomBranchOpcodeTests.cs` 3, `AlundraScriptedPortalOpcodeTests.cs` 5) ; rouges d'abord (le seul changement préalable est le paramètre facultatif `arrivalAnimationId` de `BeginDeparture`, sans effet) : 26 rouges aux valeurs du plan (classes : drapeaux 0x108, 0x101, 0x100, 0x109 et masques inchangés ; `0x3F` : `Result` 0 au lieu de 1 pour Normal, Deactivated et héros porteur, 1 au lieu de 0 pour Loaded, `BlockedByEntity` et personne ; `0x4A` : `CodeIndex` 2 au lieu de 1 ; `0x58` : animation 1 au lieu de 2, 3, 4 pour `d` 1 à 3 et `CodeIndex` 10 au lieu de 0 au saut négatif ; `0x57` animation 1 au lieu de 3 ; `0x5D` : Normal au lieu de Deactivated ; `0x8C` : `Result` 7 ; `0x52` : `Result` 0 au lieu de 1 (départ et warp désactivé) et 1 au lieu de 0 (hors portail, sans hôte)) et 4 verts d'avance (gardes : `0x58` `d` 0, `0x4A` `Result` 0, `0x5D` sans correspondance, `BeginDeparture` garde `0x36` par défaut) ; tous verts après, aucune valeur du plan contredite. Écarts : (1) **commits L1-1 et L1-2 fusionnés en un seul** (bissectabilité : porter `0x58`/`0x2B`/`0x52` rougit la règle 2 et A11, dont les ajustements sont L1-2) ; (2) `0x2A` sur 0x108 : le masque attendu est 0x1041 (ClassB reste posé : 0x40 | 0x01 | 0x1000), le plan ne donnait que les drapeaux (0x109) ; (3) le plan ne dit pas comment `0x52` atteint les portails : lus par `hero.ScriptHost.Portals` (la liste que le monde expose déjà), le héros étant `IEntityWorldContext.PlayerEntity` ; sans héros ou sans hôte, `Result` 0 et repli journalisé une fois ; (4) `0x57` sans héros : repli journalisé comme `0x3E`, saut pris avec `d` = 0 (le plan ne précise pas la direction de repli) ; (5) `d` est masqué par `& 3` avant la lecture de l'offset ; (6) le paramètre de `BeginDeparture` est `uint? arrivalAnimationId = null` (null = `0x36`) et `0x52` passe `TargetDirection` comme direction d'arrivée (pas la direction du portail) ; (7) libellés de `EventOpcodeSizeTable` corrigés (`0x52`, `0x57`, `0x58`, `0x8C`) ; les libellés de `0x28` à `0x2B` étaient déjà justes.
- ✅ **L1-2 — Test statique et arcs.** La liste versionnée perd 17 lignes (`0x58` × 14, `0x2B` × 1, `0x52` × 2 ; 170 → 153
  après E19.k1) ; le parcours suit les cibles de `0x57`/`0x58` (L1-R4 ; effet calculé sur les 30 cartes : 10 instructions
  nouvelles, toutes portées, deux `0x00` atteints seulement par la chute disparaissent ; aucune ligne nouvelle) ;
  `MapsWithoutSkippedSite` gagne 165, 172, 180, 182 (11 → 15 ; le test
  `TheListIsOnTheChainAndTheElevenMapsWithoutASiteHaveNone` devient `…TheFifteenMaps…`, `Assert.Equal(15, …)`) ; **règle 4** :
  `PredicateOpcodes` reste tel quel (un opcode porté ne fait plus de ligne sautée) ; `KnownScenePredicateSites` perd 164
  `C[4]`/`C[5] @110` et garde 179 `B[1] @183` et 10 `C[75] @6418` (doc « The two known level-S sites », et le « four known
  sites » de la doc de `Rule4ScenePredicates`) ; `Rule4_ScenePredicatesAreOnlyAtTheFourKnownSites` devient `…TheTwoKnownSites`
  avec le message « beyond the two known sites » ; `Rule4_APredicateOrBranchAtLevelSOutsideTheFourKnownSitesIsFlagged`
  devient `…TheTwoKnownSites…` : son entrée `known` = les deux sites restants (179 `B[1] @183 0x84`, 10 `C[75] @6418 0x95`),
  vide ; + 163 `@297 0x82` niveau O → vide ; + `0xA2` niveau S → vide ; + 163 `@297 0x82` niveau S → une ligne ; + 10
  `C[76] @6418 0x95` niveau S (même pc, autre programme) → une ligne ; le test « deux programmes au même pc » perd son
  assertion sur 164 pc 110 (il garde 476 pc 112) ; **A11**
  (`AlundraInoaDayOneArcTests.cs:357-367`) : `(0x58, 110)` quitte l'ensemble autorisé, l'assertion « sauté au moins une fois »
  devient « `0x58 @110` exécuté au moins une fois, aucune instruction exécutée à `@119`, au moins une à `@124` » (Beaumont et
  Thyea apparaissent en direction 0, `AlundraEntitySpawnFactory.cs:667-669`, et seuls les `0x27 @470`/`@498`, gardés par T4 et
  T5, peuvent les tourner : si l'un d'eux s'exécute pendant l'arc, c'est un arrêt, pas une ré-épingle). Les autres arcs (A10J, T-A10v, T-B9, TN-3, T-C61) ne sont pas touchés : leurs entrées
  autorisées devenues mortes restent (« inclus dans ») ; s'ils rougissent, c'est un arrêt (T-A10v : les bouquets passent
  au nord de leur porteur et la boucle perd un tick, les épingles sont relatives à la pose ; TN-3 : `0x3F` écrit
  désormais `Result` ; T-C61 : `0x29 @882` efface ClassB du héros). Commit : `test(alundra): the ported branches on the story chain`
  - Fait le 2026-10-03 : fait dans le commit de L1-1 (fusion, voir L1-1). Liste versionnée 170 → 153 lignes (17 retirées : `0x58` × 14, `0x2B` × 1, `0x52` × 2, rien d'autre au diff) ; le parcours suit les quatre cibles de `0x57`/`0x58` et cesse de suivre leur chute : aucune ligne nouvelle (règle 1 verte) ; `MapsWithoutSkippedSite` 11 → 15 (165, 172, 180, 182), test renommé `…TheFifteenMapsWithoutASiteHaveNone` ; `KnownScenePredicateSites` ne garde que 179 `B[1] @183` et 10 `C[75] @6418`, règle 4 et ses deux tests renommés `…TheTwoKnownSites…` avec les entrées du plan ; `PredicateOpcodes` inchangé ; le test des deux programmes au même pc ne garde que 476 pc 112 ; A11 : `(0x58, 110)` hors de l'ensemble autorisé, `0x58 @110` exécuté, rien à `@119`, au moins une instruction à `@124`, et aucun `0x27 @470`/`@498` exécuté (la prémisse, ajoutée en assertion) ; `ImplementedOpcodes` reçoit les 11 opcodes. Rouges avant ajustement (suite complète après les opcodes) : exactement 2 sur 2401, `Rule2_EveryListedLineIsASiteReachedAndSkipped` (17 lignes périmées) et A11 (`0x58 @110 never skipped`) ; A10J, T-A10v, T-B9, TN-3 et T-C61 n'ont pas bougé. Suite Debug après : 2401 tests, 0 échec ; les six traces à l'octet (contenu identique, les quatre traces du héros remises par `checkout`).
- ✅ **L1-3 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2371 → 2401 (+30), 0 échec, en Release
  (47 s) puis en Debug (43 s), la Debug en dernier ; `cmp` sans écart (sha256 `32c651d2...727f`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (chaque gestionnaire relu dans le binaire ; douze mutations sur quatorze attrapées ; le
  parcours, tous les opcodes portés remis en « sautés », retrouve exactement les 17 lignes retirées et aucun site de
  `0x28`, `0x29`, `0x2A`, `0x3F`, `0x4A`, `0x57`, `0x5D`, `0x8C` sur la chaîne ; seul appelant de production du nouveau
  paramètre : `0x52`). Avis reportés à E19.m : **P3** aucun test ne distingue, pour `0x58`, l'entité logique du héros (le
  code lit bien l'entité logique, comme le binaire) ; P4 aucun test n'épingle l'arrêt de la chute de `0x57`/`0x58` dans le
  parcours ; P4 le masque `& 3` n'existe pas dans le binaire (sans effet sur le domaine 0 à 3) ; P4 ligne d'attribution
  du commit au nom de l'exécuteur.
- 🧪 **L1-4 — Recette** (auteur) : Inoa 164, Beaumont et
  Thyea restent face en bas tant que le héros est loin (aujourd'hui ils tournent à gauche par moments) ; la 10, les bouquets du
  côté de leur porteur ; un donjon à interrupteur au sol (hors chaîne) : l'interrupteur s'enfonce sous le héros.

**Acceptation d'E19.l1.**
1. Tests de L1-1 rouges d'abord (sauf les gardes nommées), verts après, valeurs écrites tenues ; une valeur contredite est un
   arrêt.
2. Code de test existant touché, liste fermée : A11 (`AlundraInoaDayOneArcTests.cs`), la liste versionnée,
   `AlundraStoryChainOpcodeAudit.cs` et `AlundraStoryChainSkippedOpcodesTests.cs`, `IntroTraceHarnessTests.ImplementedOpcodes` ;
   rien d'autre ; les six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette L1-4 faite par l'auteur.

**Risques.** `AnimationDirection` est posée une image après le binaire (D-E19-13) : un `0x58` juste après un demi-tour lit
l'ancienne direction ; les interrupteurs au sol dépendent de l'exactitude de `RidingEntity` pour le héros (E19.d2c2 S4), non
vérifiée sur un interrupteur ; `0x8C` suit la distribution du binaire, pas sa suite de tirages (267 lecteurs de la graine
dans l'exécutable, la plupart non portés) ; `0x28`/`0x29` libèrent 15 marches bloquées seulement dans la DLL et en bloquent
4 sur la 102 (O-E19-26), hors chaîne.

##### 1.2m.2 E19.l2 — Objets, argent et coffres ✅ (recette L2-4 en attente)

**Règles d'exécution.**
- **L2-R1 — Tables** : membre par défaut `IEntityWorldContext.ItemTables => null`, implémenté par `AlundraWorldProxy` (son
  `ItemTables`) ; sans tables, la branche « objet » de `0x82` est dégradée (journal une fois, `Result` 0, rien d'écrit).
- **L2-R2 — `AddOneItemIfUnlocked`** rend le compte quand l'objet est au maximum (binaire `0x8004E58C`/`0x8004E5B0`) ; son seul
  appelant de production (`InitializeNewGameInventory`) ignore le retour : ni la nouvelle partie ni les préréglages ne
  changent. Nouveaux helpers au patron des voisins : `UseItem` (`0x8004E5C4` : id hors de [0, 99) → 0 sans écrire ; compte
  nul → −1 ; sinon compte − 1 écrit et rendu), `AddMoney` (somme sur 32 bits puis bornes [0, 9999], comme `SetMoney`
  `0x8004DF80`, sans passer par un `short` qui déborde).
- **L2-R3 — `0x82`** : `Result` = `HandleMapTriggerCommand(v1) != 0 ? 1 : 0`, taille 2, avec l'aiguillage du binaire
  (id 0 → 0 ; `0x45`-`0x48` → `AddMoney(1/5/10/30)`, 1 ; `0x4F` → `FalconTemp` + 1 plafonné à 50 et G1450, 1 ; `0x50`-`0x56` →
  1, trace `Degraded` (O-E19-39) ; sinon `GetNumberOfItem(id) < AddOneItemIfUnlocked(id)`).
- **L2-R4 — `0x83`** `Result` = `GetNumberOfItem(v1) >= v2`, taille 3 ; **`0x84`** `Result` = `UseItem(v1) == −1 ? 0 : 1`,
  taille 2 ; **`0x98`** `AddMoney(v1 | v2 << 8)`, taille 3, `Result` non touché ; **`0x99`** prix > argent → `Result` 0 ; sinon
  argent − prix et `Result` 1, taille 3 ; **`0x9A`** `Result` = argent >= prix, taille 3 ; **`0x9F`** `Result` = 1 si la première
  correspondance de la recherche `v1` a un `ContentsGameFlag` non nul et posé (`AlundraGameState.GetFlag`, banques G et T
  comme partout), sinon 0, taille 2, sans jamais suspendre.
- **L2-R5 — Libellés** : `0x82` « Give item or pickup by id », `0x83` « If number of item >= value », `0x9F` « If chest
  opened » ; `IntroTraceHarnessTests.ImplementedOpcodes` reçoit les opcodes portés.

**Tâches.**
- ✅ **L2-0 — Plan**, relu jusqu'à READY (READY à la première relecture, le 2026-10-03 ; valeurs auditées).
- ✅ **L2-1 — Helpers et opcodes, tests d'abord** (tables réelles `ItemTablesFixture.LoadReal()` ; `Result` de départ 7 ;
  programmes `[op, …, 0x1A, 9, 0xFF]` : l'animation 9 prouve que l'opcode n'a pas suspendu) :
  - `AddOneItemIfUnlocked(17)` deux fois → 1 puis **1** (aujourd'hui 17), compte 1 : le test existant
    `AlundraItemInventoryTests.cs:368-375` est renommé et ré-épinglé (cause établie : le binaire) ;
  - `0x82` : objet 36 de compte 0 → `Result` 1, `NumberOfItems[73]` = 1 ; objet 36 de compte 8 (maximum 9) → 1, compte 9,
    encore → 0, compte 9 ; objet 41 (maximum 1) de compte 1 → 0, compte 1 (un port sur le helper d'aujourd'hui rendrait 1) ;
    id 0 → 0, rien d'écrit ; id 99 → 0, rien d'écrit ; `0x47` argent 9995 → 9999, 1 ; `0x48` argent 0 → 30, 1 ; `0x4F`
    `FalconTemp` 49 → 50, `GameFlags[45] & 0x400` posé, 1 ; `FalconTemp` 50 → 50, 1 ; `0x53` → 1, rien d'écrit, trace
    `Degraded` (rouges : `Result` 7, comptes et argent inchangés) ;
  - `0x83` `[0x83, 61, v2, …]` : compte 0 et `v2` 1 → 0 ; 1 et 1 → 1 ; 3 et 4 → 0 ; 2 et 0 → 1 ; id 99 et 1 → 0 ;
  - `0x84` : objet 88 de compte 1 → compte 0, `Result` 1 ; compte 0 → 0, `Result` 0 ; id 99 → `Result` 1, rien d'écrit ;
  - `0x98` : argent 0, `[10, 0]` → 10 ; argent 9990, `[0xFF, 0xFF]` → 9999 ; `Result` reste 7 ;
  - `0x99` : argent 5, `[5, 0]` → 0, `Result` 1 ; argent 4 → 4, 0 ; argent 2163, `[0x73, 0x08]` → 0, 1 ; argent 2162 →
    2162, 0 ; argent 9999, `[0xFF, 0xFF]` → 9999, 0 ;
  - `0x9A` : argent 15, `[15, 0]` → 1, argent inchangé ; 14 → 0 ; 9999 contre `[0xFF, 0xFF]` → 0 ;
  - `0x9F` `[0x9F, 4, …]` (monde de test, PNJ d'`EntityRefId` 4) : `ContentsGameFlag` 1192 et G1192 posé → 1 ; G1192 clair
    → 0 ; `ContentsGameFlag` 0 → 0 ; aucune correspondance → 0 ; drapeau temporaire `0x8005` et T5 posé → 1 ; deux
    correspondances, seule la première décide ; l'animation 9 dans le même appel dans tous les cas.
  - **Montage** (audit des valeurs) : le monde de test fournit `ItemTables` (sinon la branche « objet » de `0x82` se dégrade
    et l'objet 36 lirait 0) ; pour `0x9F`, l'entité qui exécute est Loaded, Normal ou Deactivated ; la « première
    correspondance » est celle du plus petit créneau (la recherche du binaire monte).
  Commit : `feat(alundra): port the item, money and chest opcodes like the binary`
  - Fait le 2026-10-03 : 39 tests neufs en 2 fichiers (`AlundraItemAndMoneyOpcodesTests.cs` 33, `AlundraChestOpcodeTests.cs` 6) + le test d'`AddOneItemIfUnlocked` renommé (`…AndThenReturnsTheCount`) et ré-épinglé ; rouges d'abord (seuls changements préalables : le membre `ItemTables` par défaut, `UseItem` et `AddMoney` en signatures sans effet) : 40 sur 40 rouges aux valeurs du plan (`Result` 7 pour chaque opcode sauté ; comptes et argent inchangés ; `AddOneItemIfUnlocked(17)` rend 17 au lieu de 1 ; `UseItem` rend 0 au lieu de −1 sur un compte nul, `AddMoney` 0 au lieu de 9999), aucune garde verte d'avance ; tous verts après, aucune valeur du plan contredite. Écarts : (1) **commits L2-1 et L2-2 fusionnés en un seul** (bissectabilité : porter `0x82`/`0x84`/`0x99`/`0x9A` rougit la règle 2, exactement 1 rouge sur 2440, dont l'ajustement est L2-2) ; (2) `ItemTablesFixture.LoadReal()` ne porte pas les lignes 36, 38, 41, 61 et 88 (maximum 0) : les tests écrivent la même fixture complétée de ces cinq lignes, valeurs de `alundra-project/Data/items-properties.json` (36 max 9, 38 max 1, 41 max 1, 61 max 99, 88 max 1) ; (3) `AlundraWorldProxy.ItemTables` est un champ : le membre d'interface est implémenté explicitement (`IEntityWorldContext.ItemTables`) ; (4) tests ajoutés en plus des valeurs du plan : `0x45`/`0x46` (1 et 5 pièces), `0x82` sans tables (dégradé, `Result` 0), les helpers `AddMoney`/`UseItem`, `0x9F` avec le drapeau 0 posé (garde contre « bit 0 du mot 0 »), les deux ordres de deux correspondances ; (5) libellés de `EventOpcodeSizeTable` corrigés (`0x82`, `0x83`, `0x9F`) ; `IntroTraceHarnessTests.ImplementedOpcodes` reçoit les sept opcodes.
- ✅ **L2-2 — Test statique.** La liste versionnée perd 13 lignes (`0x82` × 4, `0x84` × 3, `0x99` × 3, `0x9A` × 3 ; 153 → 140
  après E19.l1) ; `MapsWithoutSkippedSite` gagne 163 et 179 (15 → 17 ; le test devient `…TheSeventeenMaps…`,
  `Assert.Equal(17, …)`) ; `WaitOpcodes` perd `0x9F` (le binaire n'attend pas) ; `PredicateOpcodes` gagne `0x83` et `0x9F` ;
  `KnownScenePredicateSites` perd 179 `B[1] @183` et ne garde que 10 `C[75] @6418` (doc « The one known level-S site ») ;
  les deux tests de la règle 4 passent de « two » à « one known site » (noms, message, doc) et l'entrée `known` du test
  fabriqué ne garde que 10 `C[75] @6418 0x95`, ses quatre autres cas inchangés. Aucun arc n'atteint ces sites.
  Commit : `test(alundra): the item and money opcodes leave the skipped list`
  - Fait le 2026-10-03 : fait dans le commit de L2-1 (fusion, voir L2-1). Liste versionnée 153 → 140 lignes (13 retirées : `0x82` × 4, `0x84` × 3, `0x99` × 3, `0x9A` × 3, rien d'autre au diff) ; `MapsWithoutSkippedSite` 15 → 17 (163, 179), test renommé `…TheSeventeenMaps…` ; `WaitOpcodes` sans `0x9F` ; `PredicateOpcodes` + `0x83`, `0x9F` ; `KnownScenePredicateSites` ne garde que 10 `C[75] @6418` ; règle 4 et ses deux tests renommés `…TheOneKnownSite…` (message, doc, entrée `known` du test fabriqué réduite à 10 `C[75] @6418 0x95`, quatre autres cas inchangés). Rouge avant ajustement : exactement 1 sur 2440, `Rule2_EveryListedLineIsASiteReachedAndSkipped` ; aucun arc n'a bougé. Suite Debug après : 2440 tests (2401 + 39), 0 échec ; les six traces à l'octet (les quatre traces du héros remises par `checkout`).
- ✅ **L2-3 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2401 → 2440 (+39), 0 échec, en Release
  (42 s) puis en Debug (47 s), la Debug en dernier ; `cmp` sans écart (sha256 `b5324d4e...33f8`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (chaque gestionnaire relu dans le binaire, dont la table de `HandleMapTriggerCommand` ;
  dix-sept mutations sur dix-huit attrapées ; seul appelant de production d'`AddOneItemIfUnlocked` sans effet ; aucun
  changement de la sauvegarde ; le monde de production fournit bien `ItemTables`). Avis reporté à E19.m : P4 aucun test ne
  distingue le cas explicite de l'id 0 de `0x82` de la branche par défaut (même résultat sur les données réelles : l'objet
  0 a un maximum de 0).
- 🧪 **L2-4 — Recette** (auteur) : la boutique de la 163 refuse faute d'argent
  (dialogue 140), comme l'original avec une bourse vide ; une partie chargée garde ses objets et son argent.

**Acceptation d'E19.l2.**
1. Tests de L2-1 rouges d'abord, verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. Code de test existant touché, liste fermée : `AlundraItemInventoryTests.cs` (le test d'`AddOneItemIfUnlocked`), la liste
   versionnée, `AlundraStoryChainOpcodeAudit.cs` et `AlundraStoryChainSkippedOpcodesTests.cs`,
   `IntroTraceHarnessTests.ImplementedOpcodes` ; rien d'autre ; les six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette L2-4 faite par l'auteur.

**Risques.** Sans source d'argent avant E14, les boutiques refusent tout ; les sondes `0x9F` attendent que les coffres
s'ouvrent, ce qu'E14 seul fera (aujourd'hui un `Result` périmé peut lancer la scène d'après coffre aussitôt) ; les sous-cas
`0x50`-`0x56` de `0x82` (8 sites `0x53`, hors chaîne) ne donnent ni vie ni effet (O-E19-39).

### 1.2n E19.h — Attentes en Z, contacts, saut et murs ⏳ (E19.h1, E19.h3, E19.h4 ✅, recettes en attente ; E19.h1b1 planifiée ; E19.h1b2 et E19.h2 esquissées ; O-E19-41 et O-E19-42 tranchés)

**Découverte** (2026-10-03, lecture seule, trois surfaces : A attentes et contacts en Z, B murs et glissement, C reste du
saut ; notes, émulations du code du binaire et recensements dans le scratchpad de la session, `e19h-disc/A`, `B`, `C`). Hors
périmètre, nommément : D5 d'E19.d2c2 (en pause pour l'auteur), O-E19-29, O-E19-17, O-E19-31, O-E19-32, E14. Faits porteurs
**[binaire]** :
- **Sept attentes** (aucune ne pose `Result`, toutes suspendent au même pc, aucune n'est atteinte sur les 30 cartes de la
  chaîne ; la décompilation est juste sauf le cas sans enregistrement de `0x22`) : `0x20` (`0x8003D9BC`, taille 3) attend que
  `|PosZ − PosZ du premier appel| >> 16 >= v1 | v2 << 8` sur l'entité logique (mémo sur le pc ; ne lit pas `+0x140`, libellé
  faux) ; `0x21` (`0x8003DA28`, 3) = `0x20` **ou** `CollidedWithEntityZ`, dès le premier appel ; `0x22` (`0x8003DA70`, 1) attend
  `PosZ == hauteur de l'enregistrement << 19` exactement et ne fait que borner `ForceZ` pour ne pas dépasser (jamais de
  poussée) ; `0x23` (`0x8003DB28`, 1) = `0x22` **ou** `CollidedWithEntityZ` (libellé faux) ; `0x26` (`0x8003DBA8`, 1) attend
  `ForceAdjusted` ou `CollidedWithEntityZ` ; `0x47` (`0x8003E984`, 1) attend `HitCounter` ou `ForceAdjusted` (libellé faux) ;
  `0x48` (`0x8003E9B0`, 1) attend `HitCounter` ou `CollidedWithEntityZ` (aucun site). `0x6F` (`0x8003F9E8`, 1) :
  `Result` = `CollidedWithEntityZ`. Recensement : `0x20` 266 sites dans 78 cartes, `0x21` 16, `0x22` 16, `0x23` 1, `0x26` 28,
  `0x47` 11 (les flammes de 26, 29, 94, 106, 110, 111, 352), `0x6F` 16.
- **Toute entité active intègre Z** (`0x80038364`, sans notion de contrôleur) et reçoit en fin de passe `IsOnGround =
  !(FloorHeight < PosZ)` (`0x800380F8`) ; la DLL ne bouge jamais en Z une entité sans contrôleur et n'écrit jamais son
  `IsOnGround` (O-E19-7 ; 12 préfabs sans contrôleur, 67 enregistrements, dont 476 rec0, 135 rec26, 173 rec4, 180 rec4, 185
  rec10 sur la chaîne, qui n'y reçoivent jamais de force en Z). Deux `0x20` du corpus attendent sur une entité sans
  contrôleur : 47 `C[4] @442`, 260 `C[5] @747`.
- **Apparition** (`InitializeEntity` `0x80039D04`) : `TerrainHeight` = terrain sous la boîte, `PosZ` relevé à `T + 1` s'il est
  en dessous (`0x80039EF0`-`0x80039F2C`), `FloorHeight` et `IsOnGround` calculés ; **aucun appui sur une entité à
  l'apparition** (terrain seul, `0x800370C4`). La DLL ne fait rien à l'apparition (le monde n'est pas encore posé : O-E19-6)
  et accepte un appui sur une entité à toute distance (O-E19-15).
- **Glissement** (`ComputeXYPosition` `0x80037730`, appelé par `MoveEntity` `0x80037E34` après `ComputeZPosition`) : le pas
  est divisé de moitié jusqu'au contact ; au tick sans aucun demi-pas accepté, contre une **cellule**, une seule tentative de
  glissement (table `0x80023734` : ± 0,75 px de côté pour un pas nord ou sud, ± 0,5 px pour est ou ouest, un axe annulé en
  oblique) ; `ForceAdjusted` n'est levé qu'en `0x80037D54` (pas de progrès, glissement interdit ou impossible). Glissement
  interdit par le bit `0x2000` de `+0x6C` (`0x800379C4`), que posent `0x46` (`0x8003E96C`) et effacent `0x45`
  (`0x8003E954`) ; 244 des 395 en-têtes de sprite le portent : sur la chaîne, **seul le héros glisse** (tous les PNJ nommés
  portent le bit). La décompilation se trompe en trois points (le binaire l'emporte).
- **Aimantation au sommet** (`0x80037848`) : avec le bit Gravity et `ForceZ == 0` après le pas, `PosZ = T + 1` si la montée
  est d'au plus 3 px + 1 unité ou la descente d'au plus 3 px, défaite si une entité occupe la boîte. **Plafonds**
  (`0x80036D94`) : en montée, le haut de la boîte s'arrête sous le bas d'une entité collidable au-dessus,
  `CollidedWithEntityZ` = 1, `ForceZ` = 0 avec Gravity ; plafond absolu `0x77FFFFF` (1920 px), testé avant tout. Sur la
  chaîne : aucun rebord à moins de 3 px d'un sommet de saut, aucun plafond sur le chemin d'un arc.
- **Sons des changements d'animation** (`0x80038BB0`) : tout changement d'animation de toute entité, l'apparition comprise,
  joue le son de l'animation (`Sfx`, + 0x100 si le bit 0x20 de l'octet 0xD) ; la DLL ne joue que le décollage du héros (R8).
- `+0xF8` « surchargé pour le type 0x14 » (O-E19-27) est une fausse piste (le registre de base vaut `entity + 0x20` :
  `+0x118`, `PosY`) : retiré.

**Choix de conduite** (techniques) :
- découpage : **E19.h1** (`0x21`, `0x26`, `0x47`, `0x48`, `0x6F`), **E19.h1b** (`0x20`, `0x22`, `0x23`, Z des entités sans
  contrôleur, apparition, conventions de `PosZ` ; esquissée, réduite d'E19.h1 après sa relecture et l'audit des valeurs),
  **E19.h3** (reste du saut du héros :
  aimantation au sommet, plafonds), **E19.h4** (glissement, étape 1 de la DLL, et `0x45`/`0x46`), E19.h1, E19.h3 et E19.h4 exécutées dans cet ordre ;
  **E19.h2** (état en l'air des PNJ tenu par le tick, Z avant XY, atterrissage strict, sans l'aimantation de 4 px du moteur :
  ferme D-E19-40 et « Z des PNJ en retard » d'O-E19-28) est esquissée : ses épingles d'arcs (A10, A12, A10J, TR-V) demandent
  d'abord une émulation de la boucle de scène du binaire ;
- `0x47` se porte maintenant avec `HitCounter` à 0 (E14 l'écrira) : aujourd'hui chaque flamme se désactive un tick après son
  départ, une fois portée elle avance jusqu'au mur, comme l'original quand on ne la frappe pas (hors chaîne) ;
- le P3 du passager (`CollidedWithEntityZ` 1 au repos sur un porteur avec gravité) est rendu à D5 (D-E19-56, F2b) ;
- l'aimantation de descente de 4 px du moteur, une fois par image, contre 3 px par tick dans le binaire : écart consigné
  (O-E19-40), visible seulement aux images de rattrapage ; exporter 3 px casserait SJ-11 et les épingles des PNJ ;
- **questions à l'auteur** (consignées, elles n'arrêtent pas h1, h3 et h4) : Q-H1 sons des changements d'animation pour
  toutes les entités (O-E19-41) ; Q-H2 résolution exacte des contacts, étape 2 du glissement, plan moteur (O-E19-42).

##### 1.2n.1 E19.h1 — Attentes `0x21`, `0x26`, `0x47`, `0x48` et `0x6F` ✅ (recette H1-3 en attente)

**Relecture** (2026-10-03) : REVISE n°1, quatre blocages, tous sur la partie « Z sans contrôleur et apparition » (le harnais
d'intro pilote déjà des mandataires nus par sa propre passe verticale ; une entité sans contrôleur n'a ni gravité ni
viscosité de carte en production ; la convention de `PosZ` à l'apparition contredit la règle d'`IsOnGround` ; le remplaçant
de l'appui d'apparition et les tests qui le vérifient ne sont pas spécifiés). Audit des valeurs : les valeurs des attentes
confirmées contre le binaire ; `0x22` compare `PosZ` à `hauteur << 19` dans la convention du binaire, alors que la `PosZ` de
la DLL vaut celle du binaire moins 1, et à 304 px la cible convertie (impaire) n'est pas représentable sur la racine en
flottant de la DLL. Disposition : **réduction** ; `0x20` (qui calerait sur deux entités sans contrôleur, 47 `C[4] @442`, 260
`C[5] @747`), `0x22`, `0x23`, la hauteur d'enregistrement, le Z sans contrôleur et l'apparition passent en E19.h1b (esquissée,
ses points à concevoir) ; E19.h1 ne garde que les attentes sans question de convention ni entité sans contrôleur, et `0x6F`.

**Règles d'exécution.**
- **H1-R1 — Attentes** : `0x21`, `0x26`, `0x47`, `0x48` portées comme attentes sur l'entité logique (rendre 0 = suspendre au
  même pc, comme `0x24`/`0x25`) ; `0x21` = (distance en Z depuis le premier appel atteinte : `|PosZ − PosZ du premier appel|
  >> 16 >= v1 | v2 << 8`, la règle de `0x20`, une différence, donc sans question de convention) **ou** `CollidedWithEntityZ`,
  dès le premier appel ; mémo du premier appel : `Parameters[1]` = `CodeIndex`, `Parameters[2]` = `PosZ` ; `0x26` :
  `ForceAdjusted` ou `CollidedWithEntityZ` ; `0x47` : `HitCounter` ou `ForceAdjusted` ; `0x48` : `HitCounter` ou
  `CollidedWithEntityZ`. `0x6F` : `Result` = `CollidedWithEntityZ`, taille 1. `0x20`, `0x22`, `0x23` restent sautés (E19.h1b).
- **H1-R2 — Libellés** : « Wait Z distance » (`0x20`), « Wait Z distance or Z contact » (`0x21`), « Wait height target »
  (`0x22`), « Wait height target or Z contact » (`0x23`), « Wait hit or ForceAdjusted » (`0x47`), « Wait hit or Z contact »
  (`0x48`) ; `IntroTraceHarnessTests.ImplementedOpcodes` reçoit les opcodes portés. La règle 3 du test statique et
  `WaitOpcodes` ne changent pas (elles ne regardent que les attentes sautées).

**Tâches.**
- ✅ **H1-0 — Plan**, relu jusqu'à READY (REVISE n°1, réduction, audit des valeurs, READY à la deuxième relecture le 2026-10-03).
- ✅ **H1-1 — Attentes et `0x6F`, tests d'abord** (montage d'`AlundraEventProgramRunnerWaitCollidedZTests`, opcode au pc 1
  derrière un `0x01`, entité logique avec contrôleur) : **T-Z21** `01 21 10 00 FF` (16 px) : `CollidedWithEntityZ` 1 au premier
  appel → fin aussitôt (`0x21` rend 3, puis `0xFF @4`) ; sinon, `PosZ` 3145728 au premier appel : appel 1 suspendu, `CodeIndex`
  1, `Parameters[2]` 3145728 ; `PosZ` 4194303 → attend ; 4194304 → fin ; vers le bas, 2097152 → fin ; l'entité logique bouge,
  pas le propriétaire → fin ; `CollidedWithEntityZ` 1 à un appel suivant → fin. **T-Z26** `01 26 FF` : (`ForceAdjusted`,
  `CollidedWithEntityZ`) (0, 0) attend ; (1, 0), (0, 1), (1, 1) finissent ; dix appels sans effet de bord. **T-Z47** `01 47 FF` :
  (`HitCounter`, `ForceAdjusted`) (0, 0) attend ; (1, 0), (0, 1) finissent ; `CollidedWithEntityZ` 1 seul attend ; `Result` 7
  reste 7. **T-Z48** `01 48 FF` : `HitCounter` ou `CollidedWithEntityZ` finissent ; `ForceAdjusted` seul attend. **T-Z6F**
  `01 6F FF` : `Result` = `CollidedWithEntityZ` (0 ou 1). Rouges : aujourd'hui chaque opcode est sauté, le programme continue
  au premier appel et `Result` reste tel quel. Commit : `feat(alundra): port the Z wait opcodes 0x21, 0x26, 0x47, 0x48 and 0x6F like the binary`
  - Fait le 2026-10-03 : 20 cas dans `AlundraZWaitOpcodesTests` ; rouges d'abord sur le code d'avant (12 rouges : opcodes sautés, ex. `0x21` à 16 px rend `(4,0xFF,0)` au lieu de `(1,0x21,0)`, `Parameters[2]` 0 au lieu de 3145728, `0x47`/`0x48`/`0x26` en attente finissent `(1,op,1)`, `0x6F` laisse `Result` à 9 ; les 8 cas « fin immédiate » passaient déjà, le saut rend la même taille), verts après, valeurs écrites tenues ; suite Debug 2460/2460 ; libellés H1-R2 corrigés, `ImplementedOpcodes` reçoit `0x21 0x26 0x47 0x48 0x6F` ; six traces inchangées (`git diff --ignore-cr-at-eol` à 0). Aucun écart au plan ; `0x21` écrit le mémo même quand le contact termine au premier appel (le binaire appelle `0x20` d'abord).
- ✅ **H1-2 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2440 → 2460 (+20), 0 échec, en Release
  (44 s) puis en Debug (46 s), la Debug en dernier ; `cmp` sans écart (sha256 `21786093...9869`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (chaque gestionnaire relu dans le binaire, décalage arithmétique, ordre du mémo de `0x21`
  au premier appel ; mutations attrapées ; aucun site de ces cinq opcodes sur une entité sans contrôleur ni sur la chaîne).
  Avis reportés à E19.m : P4 l'octet fort de la distance de `0x21` n'est pas testé (tous les sites du corpus ont `v2` 0) ; P4
  un décalage logique et l'ordre du mémo ne sont pas observables.
- 🧪 **H1-3 — Recette** (auteur, hors chaîne) : une flamme (carte 26) avance jusqu'au mur
  au lieu de s'éteindre au départ ; sur la chaîne, aucun écart.

**Acceptation d'E19.h1.**
1. Tests de H1-1 rouges d'abord, verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. Code de test existant touché, liste fermée : `IntroTraceHarnessTests.ImplementedOpcodes` ; rien d'autre (la liste
   versionnée ne change pas : aucun de ces opcodes n'est atteint sur la chaîne) ; les six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette H1-3 faite par l'auteur.

**Risques.** Une attente portée peut caler hors chaîne là où le saut laissait passer (ascenseurs et trappes de `0x21`, 52, 83,
148, 152, 154, 344, 410, 423 : leur contact en Z dépend de D5) ; une flamme (`0x47`) n'est arrêtée que par un mur tant
qu'E14 n'écrit pas `HitCounter`.

##### 1.2n.1b E19.h1b1 — Écritures absolues de Z en convention de la DLL (apparition, `0x8A`, `0x64`) ✅ (CONFIRMED le 2026-10-03 ; recette H1b1 en attente)

**Décision** : D-E19-59 (l'apparition passe en convention de la DLL, `0x8A` et `0x64` examinés dans la même ADR). E19.h1b est
coupée en deux : **h1b1** (cette tranche, la convention) puis **h1b2** (`0x20`, `0x22`, `0x23`, Z des entités sans contrôleur,
gravité de carte de toute entité ; esquisse en §1.2n.1c), qui en dépend, comme E19.h2. **Découverte** (2026-10-03, lecture seule,
`e19spawnz-disc/notes.md` et `e19h1b-disc/notes.md` du scratchpad de la session).

**Faits.**
- **[binaire]** `0x64` et `0x8A` écrivent `PosZ = (z << 16) + 1`, sans `ModZ` ni butée (`0x8003F66C`-`0x8003F670`,
  `0x80040318`-`0x80040320`) ; `0x65`, `0x89` et `0x8B` ajoutent un décalage sans `+ 1`. La physique suit les scripts dans la
  même image (événements `0x8003B3A8`, physique `0x8003B3E0`). Le sol du binaire est `T + 1` : `z << 16 == T` pose l'entité au
  repos exact. La butée de l'apparition compare `PosZ`, pas `ModdedPosZ` (`0x80039EF0`).
- **DLL** : partout `PosZ` = binaire − 1, sauf trois écritures qui gardent le `+ 1` : la fabrique (`z − ModZ + 1`,
  `AlundraEntitySpawnFactory.cs:656`), `0x8A` (`AlundraEventProgramRunner.cs:2084`) et `0x64` (`:2190`). Une entité à contrôleur
  perd l'unité au premier tirage de tête d'image ; posée sur une autre entité, elle traverse alors son appui : c'est la raison
  d'être de l'appui d'apparition sans portée (`immediateAtSpawn`, sites de production `AlundraWorldProxy.cs` ~793-798 et
  ~2503-2508). Une entité sans contrôleur garde le `+ 1` pour toujours (14 sites : Rancune à la 476, Sara aux 41 et 47, les
  déclencheurs de clé des 145, 229 et 398).
- **Recensement** : `0x64` 544 sites dans 155 cartes (62 sur la chaîne), `0x8A` 684 sites dans 69 cartes (49 sur la chaîne). Seul
  effet visible trouvé : sur les cartes 83 et 410, la plateforme flottante (`0x64 @1235`, puis `0x21 @1246`) finit sa première
  montée à 48 px après 32 ticks comme l'original, au lieu de 49 px après 33 aujourd'hui.
- **Appui d'apparition à distance** (corrigé par l'audit des valeurs du 2026-10-03, `e19h1b1-audit/notes.md`) : aujourd'hui, une
  seule entité de la chaîne est collée à un appui situé sous elle, 391 rec0 (bloc transparent, 32 px plus bas, sur rec4 : c'est
  A6) ; les déclencheurs de clé 172 rec4 et 179 rec4 apparaissent avant leur appui rec6 (ordre des couches) et restent à 2621441
  (2621440 après). Avec H1b1-R3, le bloc de la 391 reste à sa hauteur d'apparition, 144 px (`T`), comme dans le binaire.
- **Butée au terrain** (audit) : elle relève 431 enregistrements du corpus (54 sur les cartes des tests) ; la plupart rejoignaient
  déjà le terrain au premier tick ; 87 entités sans gravité dont `OffsetZ` > 0 restent plus haut d'au plus `OffsetZ`, comme dans
  le binaire (aujourd'hui dessinées enfoncées : livres de sauvegarde +2 px aux 178, 179 et 185, cruches rebondissantes +12 px aux
  15 et 17, tremplins, interrupteurs de lampe…) ; 6 couvercles à gravité tombent de `OffsetZ` après l'apparition ; 4 lasers sans
  contrôleur restent relevés. Fidèle (D-E19-59), visible : à la recette.
- **Traces** : aucun octet ne devrait changer (raisonné, non lancé) : `intro-programs-389.txt` est un listage statique, les
  traces du héros passent par un exécuteur vide et leur propre placement, et le harnais d'intro pose toute entité placée au sol à
  `T + 1` dans la même image.

**Règles.**
- **H1b1-R1 — Apparition** (révision n°1) : la fabrique écrit `PosZ = (z << 16) − ModZ`, puis, **si un champ de collision lui
  est passé**, le relève à `T` quand `PosZ <= T` (le binaire : `PosZ <= T + 1 → PosZ = T + 1`, en comparant `PosZ` et non
  `ModdedPosZ`, `0x80039EF0`, décompilation `EntityManager.cs:128-136`). `T` est la hauteur de `ComputeEntityGroundHeight` : le
  maximum des quatre coins de l'empreinte de l'enregistrement par `AlundraTerrainProbe.SampleTerrainHeightCorner`, le même
  échantillonnage que `ComputeTerrainHeight` (`AlundraEntityScriptProxy.cs` ~1548-1566), pas celui de `ClampToGround` (boîte
  physique). La fabrique n'a jamais de monde (`ApplySpawnInitialization` s'exécute avant `world.AddEntity`,
  `AlundraWorldProxy.cs` ~774-791) : `AlundraWorldProxy` lui passe son `CollisionField` (chargement de carte et
  `SpawnEntityByRecordId`) par un argument optionnel ; le harnais d'intro n'en passe pas, et sans champ **aucune butée** n'est
  appliquée (ni à 0). H1b1 possède O-E19-6 (relevé au terrain à l'apparition), retiré de la portée d'E19.h1b2.
- **H1b1-R2 — `0x8A` et `0x64`** écrivent `PosZ = z << 16`, sans `+ 1` et sans butée (le binaire n'en a pas) ; `0x65`, `0x89` et
  `0x8B` ne changent pas.
- **H1b1-R3 — Appui d'apparition** : les deux appels de production d'`immediateAtSpawn` disparaissent (une pose en pixels
  entiers tient exactement sur la racine en flottant : l'appui normal du premier tick la trouve) ; le paramètre reste pour le
  harnais d'intro et ses tests.
- **H1b1-R5 — O-E19-15** : h1b1 ferme O-E19-15 en production (l'appui d'apparition sans portée disparaît des deux sites de
  production, `AlundraWorldProxy.cs:798` et `:2548`) ; le harnais d'intro et les tests qui l'appellent gardent le paramètre.
- **H1b1-R4 — Ensemble** : R1 à R3 partent dans le même commit (convertir `0x8A`/`0x64` seuls laisserait une entité posée sur une
  plateforme née de la fabrique une unité dans sa boîte, sans appui).

**Tâches.**
- **H1b1-1 — Tests d'abord** : les épingles déplacées de −1 (liste fermée ci-dessous) ; nouveau test de production carte 83,
  enregistrement 22 : `0x21 @1246` finit au 33e appel de son programme (l'appel de `0x64` compté comme le 1er) avec `PosZ`
  3145728 (aujourd'hui 34e, 3211264), rouge avant, vert après ; **gardes de piles** (vertes avant et après, exemptées de la règle
  « rouge d'abord » ; rouges avant = arrêt et diagnostic), sur les cartes réelles chargées par le support d'arcs en vrais préfabs,
  60 images après le chargement, `haut.PosZ + haut.ModZ == bas.PosZ + bas.ModZ + bas.Depth + 1` et les valeurs : 390 rec6 sur
  rec4 (bas 4194304, haut 5242880), 163 rec15 sur rec14 (bas 2097152, haut 3145728), 179 rec12 sur rec11 (bas 2097152, haut
  3145728) (`e19h1b-disc/spawnsupport_chain.txt`) ; **relevé au terrain**, montage synthétique sur le chemin de production
  (`SpawnEntityByRecordId` d'`AlundraWorldProxy` avec un vrai champ, terrain plat de 16 px, enregistrement à `z` 0 et `ModZ` 0,
  au patron d'`AlundraWorldProxySpawnInitializationTests`) : `PosZ` 1 aujourd'hui (aucune butée), 1048576 (`T`) après, rouge
  avant, vert après.
- **H1b1-2 — Code** (`AlundraEntitySpawnFactory.cs`, `AlundraEventProgramRunner.cs`, `AlundraWorldProxy.cs`).
- **H1b1-3 — ADR-0026** (les écritures absolues de Z suivent la convention de la DLL, binaire − 1 ; les écritures relatives ne
  changent pas ; pourquoi le pixel et `TileZ` restent identiques : les Z du binaire valent toujours 1 modulo 256 ; les deux
  exceptions, la cible littérale de `0x22` et un héros qui arrive au-dessus du sol ; la butée du binaire compare `PosZ`).

- ✅ **Fait le 2026-10-03** (H1b1-1 à H1b1-3, un seul commit, H1b1-R4). **Rouges d'abord** sur le code d'avant, valeurs lues : carte 83
  enregistrement 22, `0x21 @1246` finit au **34e appel** (attendu 33), `PosZ` 3211264 (attendu 3145728) ; relevé au terrain
  synthétique (`SpawnEntityByRecordId`, champ plat de 16 px) `PosZ` **1** (attendu 1048576) ; et les épingles de la liste fermée
  passées à la valeur écrite d'avance (−1 exactement pour `0x64`/`0x8A`, fabrique 24117248 et `rawZ − ModZ`, A6
  `(29097984, 44040192, 9437184)`) rouges. **Verts après** : 5 tests ajoutés dans `AlundraAbsoluteZWritesTests` (carte 83 :
  33e appel et 3145728 ; relevé au terrain : 1048576) et les trois **gardes de piles** (390 rec6 sur rec4 : 4194304 et 5242880 ;
  163 rec15 sur rec14 et 179 rec12 sur rec11 : 2097152 et 3145728 ; 60 images après le chargement, relation
  `haut.PosZ + haut.ModZ == bas.PosZ + bas.ModZ + bas.Depth + 1`), **vertes avant et après**. Code : `ApplySpawnInitialization`
  écrit `PosZ − ModZ` et relève à `T` si `PosZ <= T` quand un champ lui est passé (argument optionnel `collisionField` de
  `CreateEntityFromRecord`/`CreateEntityFromPrefab`/`CreateBareEntityFromRecord`/`ApplySpawnInitialization`, rempli par
  `AlundraWorldProxy` aux deux sites de production) ; `T` par une surcharge `ComputeTerrainHeight(ICollisionField?)` ; `0x8A` et
  `0x64` écrivent `z << 16` ; les deux appels de production d'`immediateAtSpawn` supprimés. Tests existants touchés : la liste
  fermée seule (valeurs −1, noms « PlusOne » renommés, A6, fabrique) plus les commentaires listés ; les valeurs dérivées de
  `AlundraNpcCharacterControllerMoverTests` (26214400) et `AlundraMovementObstacleProbeTests` n'ont pas bougé. `Alundra.Tests` en
  Debug : 2510/2510 (2505 avant, 5 ajoutés) ; les six traces à l'octet (`git diff --ignore-cr-at-eol` : 0), les quatre traces du
  héros remises à l'état du dépôt par `git checkout`. Écarts : les gardes de piles passent par une arrivée (`ArcSpec.Arrival`)
  qui place le héros dans la zone de naissance des caisses (la zone se teste contre la tuile du héros AU chargement, et le
  montage d'arcs pose le héros après) ; la butée n'écrit pas `TerrainHeight` à l'apparition (G4 de l'audit, optionnel : rien ne
  bouge, `EvaluateEntitySupport` l'écrit au premier tick) ; pas de Release (consigne). ADR-0026.

**Acceptation.**
1. Tests de H1b1-1 rouges d'abord, verts après, sauf les gardes de piles nommées (vertes avant et après) ; une valeur contredite
   est un arrêt.
2. Tests existants touchés, liste fermée : pour `0x64`/`0x8A` (−1 exactement) `AlundraEventProgramRunnerTests.cs:596-646`
   (noms « PlusOne » à renommer), `:692-693`, `:1071-1083`, `Map389LoadProgramsTests.cs:159`, `:169`,
   `AlundraWorldProxyUpdateCharacterizationTests.cs:437-461`, `AlundraVisionArcTests.cs:201`, `:276`, `:299-303`,
   `AlundraShipBlockArcTests.cs:252`, `AlundraShipCorridorArcTests.cs:104`, `AlundraInoaAwakeningArcTests.cs:224` ; pour la
   fabrique `AlundraWorldProxySpawnInitializationTests.cs:238` (→ 24117248), `:286` (`rawZ − ModZ`), `:394` (→ 24117248) ;
   valeurs dérivées `AlundraNpcCharacterControllerMoverTests.cs:1836`, `:1948`, `:2026` : code inchangé, la valeur dérivée de la
   plateforme née de la fabrique devient 26214400 d'elle-même (`InRange(399.99, 400.01)` et la racine à 400 tiennent) ; ces tests
   et `AlundraMovementObstacleProbeTests.cs:366-393` appellent `immediateAtSpawn` eux-mêmes, avec des Z posés à la main ou
   dérivés : **valeurs inchangées**, seuls leurs commentaires « comme la production » / « as the spawn leaves it » deviennent
   « comme le harnais d'intro » ; **A6** (`AlundraShipBlockArcTests.cs:210`) : `(29097984, 44040192, 7340032)` →
   `(29097984, 44040192, 9437184)` (le bloc de `0x2D @265` n'est plus collé sur le marin 4 : apparition à `z << 16` = 9437184
   moins `ModZ` 0, égale au terrain `T` de 144 px), commentaire ajusté ; commentaires devenus faux, commentaires seulement :
   `AlundraNpcCharacterControllerMoverTests.cs` ~1833, ~1857, `AlundraAnimationImpulseSpawnTests.cs` ~85, ~111,
   `AlundraVisionAndCoastArcTests.cs` ~182, l'en-tête de section d'`AlundraTerrainHeightTests.cs`. Rien d'autre ; aucune épingle
   d'image ne bouge. Ancres de ligne mesurées par l'audit sur `e08c467` : `0x8A` ~2092, `0x64` ~2198, second site d'apparition
   ~2549, `ComputeTerrainHeight` ~1579-1597, A6 ~211.
3. Les six traces à l'octet (sinon arrêt et diagnostic).
4. `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
5. **Recette H1b1** (auteur) : les piles de caisses des 165, 178 et 179 ; la plateforme flottante de la 83 ; les livres de
   sauvegarde des 178, 179 et 185 et les cruches des 15 et 17 (plus haut de quelques pixels, comme l'original).

**Ordre** : après **D5a** d'E19.d2c2 (close), avant E19.t (mêmes fichiers) ; une tranche à la fois. D5b (en pause) ne la bloque
pas : si une reprise de D5b change l'ordre passager/porteur et déplace les valeurs des gardes de piles de h1b1, c'est un arrêt
pour D5b, pas pour h1b1.

**Retour arrière** : revert du commit (DLL seule, aucun export).

**Risques.** Une pile qui s'effondre sans l'appui d'apparition (tests de H1b1-1, arrêt) ; une épingle hors liste ; le harnais
d'intro, qui relit la fabrique (garde des six traces).
- ✅ **Vérification d'E19.h1b1** (2026-10-03) : `Alundra.Tests` 2510/2510 en Release puis en Debug, la Debug en dernier ; `cmp` sans
  écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** (binaire relu : apparition `0x80039EA0`-`0x80039EF0`, `0x64`
  `0x8003F668`-`0x8003F670`, `0x8A` `0x80040318`-`0x80040320` ; rouges d'avant rejoués sur le code d'avant, 34 au lieu de 33 et 1 au
  lieu de 1048576, gardes de piles vertes avant ; mutation `+1` remis dans `0x64` attrapée ; seule la liste fermée a changé). Écarts
  déclarés acceptés. Avis : P4 commentaires périmés sur l'appui d'apparition (`AlundraWorldProxy.cs:1763-1765`,
  `AlundraEntityScriptProxy.cs` ~742-747) → E19.m ; écart préexistant, hors de la tranche : le binaire écrit `TerrainHeight` à
  l'apparition (`0x80039EF8`), la DLL au premier tick seulement, si bien que `0x8D` (`PosZ <= TerrainHeight + 1`,
  `AlundraEventProgramRunner.cs:1808`) lit 0 avant le premier tick d'une entité → E19.h1b2.

##### 1.2n.1c E19.h1b2 — `0x20`, `0x22`, `0x23`, Z des entités sans contrôleur ⏳ (esquisse, après E19.h1b1)


Portée : `0x20` (attente de distance en Z, 266 sites), `0x22`/`0x23` (attente d'une hauteur d'enregistrement, 17 sites) et
la hauteur d'enregistrement gardée sur le mandataire, O-E19-7 (pas vertical et `IsOnGround` de fin de tick pour toute entité
sans contrôleur) ; O-E19-6 (relevé au terrain à l'apparition) et O-E19-15 (appui d'apparition sans portée) passent à E19.h1b1, avec la
convention de `PosZ` à l'apparition, A6 et les deux sites de production d'`immediateAtSpawn`.
**À concevoir avant le plan** (blocages de la relecture d'E19.h1) : (1) la cohabitation avec la passe verticale du harnais
d'intro (`IntroTraceHarnessTests.cs` ~738, ~861, ~895, ~959-1028), qui pilote déjà des mandataires nus sans monde ni gravité ;
(2) la résolution de la gravité et de la viscosité brutes de la carte pour toute entité apparue, contrôleur ou non
(`AlundraEntitySpawnFactory.cs` ~604-606), avec un test sur préfab réel sans contrôleur (260 rec17 ou 47 rec3) ; (3) et (4) **passés à E19.h1b1** (convention de `PosZ` à
l'apparition, A6, les deux sites de production d'`immediateAtSpawn`) ; reste ici : les appelants du paramètre dans le harnais et les tests
(`AlundraNpcCharacterControllerMoverTests.cs` ~1844-1850, `AlundraMovementObstacleProbeTests.cs` ~384-386, `AlundraTerrainHeightTests.cs` ~96-120,
`AlundraJumpTestSupport.cs` pour un montage sans contrôleur) ; (5) la
cible de `0x22` dans la convention de la DLL (`hauteur << 19 − 1`) et sa représentation sur la racine en flottant (impaire, elle
n'existe pas en `float` au-delà de 256 px : 127 rec17 et rec18 à 304 px), écart de précision à porter au moteur s'il le faut ;
une entité sans enregistrement sous `0x22` (le binaire imprime une erreur et lit un enregistrement nul ; aucun site).

**Découverte de conception** (2026-10-03, lecture seule, `e19h1b-disc/notes.md`) :
- **Harnais d'intro** : ses entités sont nues et hors de tout monde (`Owner.World` nul, ni contrôleur ni racine) et il fait sa
  propre passe verticale, en convention du binaire. Critère propre pour que le nouveau pas sans contrôleur ne s'y applique pas :
  `Owner?.World?.CollisionField == null`, la dépendance de données du pas lui-même. Il n'est jamais vrai en production pour une
  entité mise à jour (le moteur pose `World` avant sa boucle ; les 483 cartes portent `AlundraCells`). Aucun test ne bouge.
- **Gravité de carte** : toutes les entités sans contrôleur ont `MapGravityRaw` et `MapZViscosityRaw` à 0, à cause de la garde
  `Controller != null` de la fabrique (~604). Lever la garde pour ces deux champs seulement est inerte tant que le nouveau pas ne
  les lit pas.
- **Convention de `PosZ` à l'apparition** : la DLL atterrit à `T` (le binaire à `T + 1`), mais la fabrique écrit à l'apparition
  la convention du binaire, `z − ModZ + 1` (~657). `0x8A` et `0x64` ajoutent aussi `+ 1`. Une entité à contrôleur perd l'unité
  au premier tirage de tête d'image ; posée sur une autre entité, elle passe alors à travers son appui. C'est la raison d'être de
  l'appui d'apparition sans portée. Une entité sans contrôleur garde le `+ 1` pour toujours.
- **Appui d'apparition** : le supprimer sans autre changement ferait s'effondrer les piles de caisses de 165, 178, 179 et 10.
- **Cible de `0x22`** : la cible du binaire, `hauteur << 19`, est atteignable aux 17 sites. La cible décalée (`− 1`) ne l'est pas
  à 304 px : la boule monterait sans fin.

**Conception proposée** (soumise à l'auteur, O-E19-45) :
- (1) le critère `Owner?.World?.CollisionField == null` pour le harnais ;
- (2) la garde levée pour les deux champs de carte ;
- (3) **la fabrique abandonne le `+ 1`** et relève l'apparition à `max(PosZ, T)`, convention de la DLL, comme le héros
  (`AlundraWorldProxy` ~1702). Toute apparition passe alors en convention de la DLL et l'érosion disparaît (une pose en pixels
  entiers tient exactement sur la racine en flottant) ;
- (4) les appels de production d'`immediateAtSpawn` disparaissent ; le paramètre reste pour le harnais et ses tests ;
- (5) `0x22` vise `hauteur << 19` sans conversion : c'est l'égalité du binaire, atteignable, au prix d'une unité 16.16 au-dessus
  de la convention de la DLL.
Tests à changer : `AlundraWorldProxySpawnInitializationTests` (~238, ~242, ~286, ~394), A6 (~210 : 9437184), la valeur dérivée du
marin 11 (26214401 → 26214400, assertions intactes), et les tests qui lisent l'appui trouvé à l'image 0
(`AlundraNpcCharacterControllerMoverTests` ~1844, ~1951, ~2021 ; `AlundraMovementObstacleProbeTests` ~384-386). Effet sur la trace
d'intro non mesuré ; à mesurer avant le plan.

##### 1.2n.2 E19.h2 — État en l'air des PNJ ⏳ (esquisse)

Port borné, au patron de R6 du héros : un état en l'air des PNJ tenu par le tick (entré par une impulsion, une force `0x1B`/`0x5E`
ou un pas sans atterrissage), sentinelle positive déclarée au moteur, pas en Z **avant** le pas XY sur le terrain d'avant le pas,
atterrissage strict, `IsOnGround` par la règle du sol après le tick (jamais `Controller.IsGrounded`) ; hors de l'état, le moteur
garde les escaliers. Valeurs du binaire déjà connues : UJ-1b (atterrit au tick 22, `ForceZ` −327680 au tick 21), UJ-7 (`0x25`
à s+23), le rebord (chute au tick 5 au lieu de 4). **Avant le plan** : une émulation de la boucle de scène du binaire pour les
images de TR-V, A10 (Bergus), A12, A10J (Giles).

##### 1.2n.3 E19.h3 — Aimantation au sommet et plafonds du héros ✅ (recette H3-3 en attente)

**Relecture** (2026-10-03) : REVISE n°1 (le retour arrière de l'aimantation quand une entité occupe la boîte n'avait aucun
test, alors qu'un héros posé sur un objet garde la porte de l'aimantation ouverte à chaque tick) : corrigé (H3-R1 nomme la
recherche, H3-1 gagne UH-APEX-REVERT, le risque disparaît).

**Règles d'exécution.**
- **H3-R1 — Aimantation au sommet** (héros, état en l'air) : au tick où le héros a le bit Gravity et `ForceZ == 0` après le pas
  vertical, après le pas XY, lire le terrain `T'` sous la boîte ; si `PosZ − T' <= 0x30000` et `T' − PosZ <= 0x30001` et
  qu'aucune entité n'occupe la boîte à `T'` (surcharge positionnelle de `AlundraEntityCollision.FindEntityCollisionCandidate`,
  avec la position XY d'après le pas, `subjectPosZ = T'`, sans drapeaux de candidats ignorés ; une entité trouvée annule
  l'aimantation, `PosZ` inchangé), `PosZ = T'` (`ModZ` du héros 0) et la
  racine suit ; `IsOnGround` en sort 1 par la règle existante ; l'état en l'air n'est pas quitté (le tick suivant atterrit
  par le test strict).
- **H3-R2 — Plafonds** (héros, état en l'air, `F > 0`, avant `PosZ += F`), en convention de la DLL (`ModdedPosZ = PosZ +
  ModZ`, `Depth = SizeZ × 65536 − 1`) : `haut = PosZ + ModZ + Depth`, `haut cible = haut + F` ; si le héros est un sujet
  éligible (`IsEligibleSubject`), toute collidable `c` autre que lui avec `haut < c.ModdedPosZ <= haut cible` et le
  recouvrement XY semi-ouvert → `haut cible = c.ModdedPosZ − 1`, contact (le plus bas l'emporte) ; contact : `PosZ = haut cible
  − Depth − ModZ`. Plafond absolu, constante du binaire testée avant tout : si `haut cible + 1 > 0x7800000`, `PosZ =
  0x77FFFFF − Depth − ModZ − 1`, contact (le `+ 1` de conversion ne vaut que pour cette constante). Contact :
  `CollidedWithEntityZ` = 1, `ForceZ` = 0 avec Gravity ; l'état continue (H3-R1 peut jouer au même tick). Fonction
  `TryFindCeiling` au patron de `TryFindSupport`. Un plafond **mobile** (l'entité au-dessus doit d'abord bouger) relève de
  D5 : seul le plafond immobile est porté.
- **H3-R3** : O-E19-27 perd l'élément `+0xF8` (fausse piste) ; le commentaire « no ceiling, E19.h »
  (`AlundraScriptedMotion.cs` ~224) est mis à jour.

**Tâches.**
- ✅ **H3-0 — Plan**, relu jusqu'à READY (REVISE n°1, READY à la deuxième relecture le 2026-10-03 ; valeurs auditées).
- ✅ **H3-1 — Tests d'abord, puis règles** (montage `JumpHeroRig` et `FlatCells`, IZF de l'animation 2 imposé dans le
  montage) : **UH-APEX-DOWN** (IZF 1408, rebord de 32 px à partir de x = 144, héros en marche vers l'est à x0 = 108, `ForceX`
  159744, Croix au tick 1, droite tenue) : `PosZ` des ticks 1 à 11 = 360448, 688128, 983040, 1245184, 1474560, 1671168,
  1835008, 1966080, 2064384, 2129920, 2162688 ; tick 12 : `ForceZ` 0, `PosZ` 2097152, `IsOnGround` 1, `CollidedWithEntityZ` 0 ;
  tick 13 : `CollidedWithEntityZ` 1, animation 1 (rouge : tick 12 2162688 et `IsOnGround` 0, atterrissage au tick 14, marche au
  tick 15). **UH-APEX-UP** (IZF 1664, rebord de 48 px à partir de x = 144, x0 = 120) : tick 14 `PosZ` 3145728, `PosX` 8866816,
  `IsOnGround` 1 (rouge : `PosZ` 2981888 au tick 14). **SJ-14** (boîte de 24 × 16 × 32 posée par `AddEntity(z = 48)`, `PosZ` 3145728, saut sur place
  de 32 px) : `PosZ` ticks 1 à 3 = 327680, 622592, 884736 ; tick 4 : 1048576, `ForceZ` 0, `CollidedWithEntityZ` 1 ; ticks 5 à 11
  = 1015808, 950272, 851968, 720896, 557056, 360448, 131072 ; tick 12 : 0, atterri (rouge : tick 4 à 1114112, contact 0).
  **UH-12** (dalle posée par `AddEntity(z = 40)`, `PosZ` 2621440) : 327680 ; tick 2 : 524288, `ForceZ` 0, contact 1 ; puis 491520, 425984, 327680,
  196608, 32768 ; tick 8 : 0 (rouge : tick 2 à 622592). **UH-CAP** (héros dans l'état en l'air sans le bit Gravity, `ForceZ`
  64 px par tick depuis 1792 px) : `PosZ` plafonné à `0x7600000 − 1` en convention de la DLL dès le tick 2, contact 0 au tick 1
  puis 1 à chaque tick, `ForceZ` inchangé (rouge : monte sans fin). **UH-APEX-REVERT** (montage de SJ-12, caisse `AddObject(…, 190, 90, 30, 24, 2)` de 2 px, saut sur place
  dans son empreinte) : après l'atterrissage sur le dessus (premier tick après le sommet où `CollidedWithEntityZ` vaut 1),
  pendant dix ticks, `PosZ` reste 131072 et `IsOnGround` 1 (l'aimantation vers le terrain `T'` = 0, à 2 px, est annulée par
  la caisse qui occupe la boîte à `T'`) ; rouge, la même règle sans l'annulation : `PosZ` quitte 131072. Gardes : UH-1 (sol
  plat, sommet à 27,5 px, aucune aimantation), UH-8 (saut dans l'eau, jamais `ForceZ == 0`), SJ-12 (caisse de 16 px, hors
  de la fenêtre de 3 px). Les boîtes sont posées par `ContactWorld.AddEntity` (convention de la DLL ; l'audit des valeurs a vérifié que la règle H3-R2
  sans `+ 1` donne exactement ces valeurs) ; une valeur contredite est un arrêt.
  Commit : `feat(alundra): the hero snaps at the top of a jump and stops under ceilings like the binary`
  - Fait le 2026-10-03 : 6 tests dans `AlundraHeroApexAndCeilingTests` ; rouges d'abord, valeurs du plan lues telles quelles (APEX-DOWN tick 12 `PosZ` 2162688, APEX-UP 2981888, SJ-14 tick 4 1114112, UH-12 tick 2 622592, UH-CAP monte sans fin : 125730816 au tick 2) ; UH-APEX-REVERT passe déjà avant (pas de règle d'aimantation à annuler), prouvé par mutation de l'annulation (build, filtre : rouge ; fichier rendu à l'octet) ; verts après, aucune valeur ré-épinglée ; suite Debug 2466/2466 (2460 + 6), six traces inchangées. `TryFindCeiling` dans `EntitySupport`, aimantation `SnapHeroAtApex` après le pas XY (seulement si l'état en l'air n'est pas quitté à ce tick), commentaire « no ceiling » mis à jour. Écart : UH-CAP n'épingle pas `PosZ` au tick 1 (le montage à 1792 px laisse le héros 1 px plus bas à l'entrée de l'état, valeur hors plan) mais vérifie qu'il est sous le plafond avec contact 0 ; aucune autre.
- ✅ **H3-2 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2460 → 2466 (+6), 0 échec, en Release
  (44 s) puis en Debug (51 s), la Debug en dernier ; `cmp` sans écart (sha256 `c4091898...5902`) ; les six traces à l'octet
  (une première clôture avait échoué : la DLL déployée était tenue par le jeu ouvert et par Visual Studio ; relancée une fois
  le jeu fermé). Vérificateur neuf : **CONFIRMED** (règles relues dans le binaire, émulation de SJ-14 refaite, rouges d'avant
  reproduits, sept mutations du cœur des règles attrapées ; un héros posé sur un objet de 1 à 3 px n'est jamais tiré dedans).
  Avis reportés : **P3** le binaire n'aimante pas un tick sans force XY ni un tick dont le pas est entièrement bloqué
  (O-E19-44 ; aucun site connu dans le corpus) ; P4 les bords des fenêtres de l'aimantation, la porte Gravity, le plafond
  testé avant les entités ne sont pas épinglés ; P4 au plafond la DLL remet aussi `FinalForceZ` à 0, et au plafond absolu
  le binaire continue de parcourir les entités (hors chaîne).
- 🧪 **H3-3 — Recette** (auteur, hors chaîne) : un saut sous une plateforme suspendue (le
  héros se cogne la tête), un saut vers un rebord à peine plus haut.

**Acceptation d'E19.h3.**
1. Tests de H3-1 rouges d'abord (sauf les gardes), verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. Code de test existant touché : aucun (A10J, A12, A10, A3, T-A10v, les tests UJ, UH et SJ, les traces du héros inchangés) ;
   les six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette H3-3 faite par l'auteur.

**Risques.** Les plafonds mobiles (D5) et ceux des PNJ (E19.h2) restent sans plafond.

##### 1.2n.4 E19.h4 — Glissement le long des murs (étape 1) et `0x45`/`0x46` ✅ (recette H4-4 en attente)

**Relecture** (2026-10-03) : REVISE n°1 (la table du glissement et le test d'un coin bloqué n'étaient que dans les notes ;
la règle de `ForceAdjusted` se contredisait, une lecture cassant T-R4 et l'autre faisant tomber le FA des PNJ en oblique ;
les épingles d'A10J et les comptes du test statique étaient laissés à la mesure) : corrigé (H4-R2 complète, H4-R4, H4-2).
REVISE n°2 (le prédicat ne disait ni où il s'exécute, ni quel axe compte quand `TargetDirection` ne s'accorde pas avec le
déplacement demandé ; T-R4 et les tests de mobiles appellent `MoveControllerAndPullPosition` directement avec la direction 0) ;
dispositions de la session principale : **FIX par réduction** — le glissement et le nouveau prédicat ne s'appliquent que dans
le pas cinématique (`RunOneKinematicTick`, seul appelant de production de `MoveControllerAndPullPosition`), après son
`Move`, et seulement quand la direction et le déplacement s'accordent ; `MoveControllerAndPullPosition` garde sa règle
d'aujourd'hui pour tout appel direct (T-R4 et les tests de mobiles inchangés) ; une seule relecture de clôture.

**Règles d'exécution.**
- **H4-R1 — `0x45`/`0x46`** : `Flags &= ~NoObstacleSlide` / `Flags |= NoObstacleSlide` sur l'entité logique, taille 1, sans
  `Result` ni resynchronisation du contrôleur.
- **H4-R2 — Glissement** (dans `AlundraScriptedMotion.RunOneKinematicTick`, juste après son appel de
  `MoveControllerAndPullPosition`, qui garde sa règle de `ForceAdjusted` d'aujourd'hui pour tout appel direct ; un tick,
  convention de la DLL, directions du binaire : 0 sud (+Y), 8 ouest, 16 nord, 24 est ; la ligne est choisie par
  `TargetDirection`, cardinale si `TargetDirection & 7 == 0`) :
  - **accord** : la règle ne s'applique que si `TargetDirection < 32` et que le déplacement demandé (`FinalForceX/Y`) s'accorde
    avec elle : cardinale, seul l'axe de la direction est demandé, avec son signe (0 : `dy > 0`, 8 : `dx < 0`, 16 : `dy < 0`,
    24 : `dx > 0`, l'autre composante nulle) ; oblique, les deux composantes demandées, aux signes du quadrant (1-7 : `dx < 0`,
    `dy > 0` ; 9-15 : `dx < 0`, `dy < 0` ; 17-23 : `dx > 0`, `dy < 0` ; 25-31 : `dx > 0`, `dy > 0`) ; sinon (désaccord, ou
    `TargetDirection >= 32`), le `ForceAdjusted` posé par `MoveControllerAndPullPosition` reste tel quel et aucun glissement ;
  - **coins** : à la position atteinte par le premier `Move`, avancée d'une unité 16.16 dans le sens de la marche (cardinale),
    `x1 = (PosX + ModX) >> 16`, `x2 = (PosX + ModX + Width) >> 16`, `y1`, `y2` de même (`Width = SizeX × 65536 − 1`, `Height =
    SizeY × 65536 − 1`), coins `[0] = (x1, y1)`, `[1] = (x2, y1)`, `[2] = (x1, y2)`, `[3] = (x2, y2)` ; un coin est **bloqué**
    si l'échantillon `AlundraCellsCollisionField.TrySampleGround` de son point n'est pas marchable pour le masque du héros
    (`WalkabilityMaskFor(Flags)`) **ou** si la hauteur du sol y dépasse le pied plus la hauteur de marche du contrôleur
    (`StepHeight` : 3 px au sol, 0 en l'air, S6), la règle des coins du moteur (`IsHorizontalMoveBlocked`) ;
  - **table** (`0x80023734`) :

    | direction | `ForceAdjusted` si | sinon, déplacement du glissement |
    |---|---|---|
    | 0 sud | ([2] et [3]) ou [0] ou [1] | Y 0 ; X + 0xC000 si [2] seul, − 0xC000 si [3] seul |
    | 8 ouest | ([0] et [2]) ou [1] ou [3] | X 0 ; Y + 0x8000 si [0] seul, − 0x8000 si [2] seul |
    | 16 nord | ([0] et [1]) ou [2] ou [3] | Y 0 ; X + 0xC000 si [0] seul, − 0xC000 si [1] seul |
    | 24 est | ([1] et [3]) ou [0] ou [2] | X 0 ; Y + 0x8000 si [1] seul, − 0x8000 si [3] seul |
    | 1-7 sud-ouest | [0] et [3] | (oblique : pas de second `Move`) |
    | 9-15 nord-ouest | [1] et [2] | idem |
    | 17-23 nord-est | [0] et [3] | idem |
    | 25-31 sud-est | [1] et [2] | idem |

  - **cardinale** : si le premier `Move` n'a pas progressé sur son axe, qu'aucune entité n'est en contact
    (`XCollisionEntity` du premier `Move` nul), que `Flags & 0x2000 == 0` et que `TargetDirection < 32`, et que la table ne
    donne pas `ForceAdjusted`, un second `Move` du déplacement de la table (un seul par tick ; aucun si le déplacement est
    nul) ; `SlideCount` (compteur du mandataire) augmente de 1 à chaque second `Move` effectivement fait ;
  - **oblique** : pas de second `Move` (le moteur a déjà avancé l'axe libre, O-E19-28 a).
- **H4-R4 — `ForceAdjusted`** (dans le pas cinématique seulement, en cas d'accord, H4-R2 ; il remplace la valeur posée par
  `MoveControllerAndPullPosition` ; « l'axe » est l'axe demandé, « progressé » le sens d'`AxisMadeNoProgress`) :
  - cardinale : FA = 0 si le premier `Move` a progressé sur l'axe demandé ; sinon FA = 1 si le glissement est interdit (entité en
    contact, bit `0x2000`, `TargetDirection >= 32`), si la table donne FA, si le déplacement est nul, ou si le `Move` de
    glissement n'a pas progressé ; sinon 0 ;
  - oblique : FA = (un axe demandé n'a pas progressé) **et** (glissement interdit, ou table FA, ou aucun axe n'a progressé).
  T-R4 et les tests de mobiles sur les vrais murs de la 389 (`AlundraNpcCharacterControllerMoverTests.cs` ~1529-1640), qui
  appellent `MoveControllerAndPullPosition` directement, gardent la règle d'aujourd'hui et leurs valeurs ; un PNJ marqué `0x2000` qui pousse en oblique le long
  d'un mur garde FA 1 (aujourd'hui et dans le binaire).
- **H4-R3** : le commentaire d'UH-6 (`AlundraHeroObjectTopsTests.cs` ~173-185) dit le résultat : {1, 2, 3} pour x0 de 131 à 133
  est celui du binaire, la ligne de l'annexe ({2, 3}) était fausse.

**Tâches.**
- ✅ **H4-0 — Plan**, relu jusqu'à READY (REVISE n°1 et n°2, dispositions de la session principale, READY à la relecture de clôture le 2026-10-03).
- ✅ **H4-1 — Tests d'abord, puis règles** (montage `JumpHeroRig`, `FlatCells`, cellule de marche 0x40 bloquante, masque de marche du
  héros posé à 0x40 sur les réglages vivants de son contrôleur (le montage le laisse à 0 aujourd'hui), boîte du héros −10, −7,
  21 × 15, Gravity, sol plat) : **T-SL1** (cellule (10,10) bloquée, héros en (263,0 ; 200,0), Haut tenu, force
  −79872 puis −159744) : `PosY` des ticks 1 à 8 = 13027328, 12867584, 12707840, 12548096, 12388352, 12228608, 12068864, 11993088,
  `PosX` 17235968 ; ticks 9 à 23 : `PosX` + 49152 par tick (17285120 … 17973248), `PosY` 11993088 ; tick 24 `PosY` 11833344, tick
  25 11673600 ; `ForceAdjusted` 0 à chaque tick (rouge : `PosX` reste 17235968, `ForceAdjusted` 1 dès le tick 9). **T-SL2** :
  le même avec `Flags |= 0x2000` : `PosX` 17235968 pour toujours, `ForceAdjusted` 1 dès le tick 9 (garde, vert avant). **T-SL3**
  (ouest, héros en (300,0 ; 177,0), `TargetDirection` 8, `FinalForceX` −159744 imposé dès le tick 1) : contact `PosX` 17956864 au tick 11 ; ticks 12 à 23 `PosY` +
  32768 par tick jusqu'à 11993088 ; tick 24 `PosX` 17797120 ; `ForceAdjusted` 0 (rouge : bloqué à 274,0, `ForceAdjusted` 1 dès
  le tick 12). **T-SL4** (oblique NE, direction 20, force 112944 / −75296, mur aux cellules x ≥ 12, héros en (270,0 ; 400,0)) :
  `ForceAdjusted` 0 à chaque tick (rouge : 1 dès le tick 6) ; `PosY` reste celle d'aujourd'hui (25837920 au tick 5 : pas de
  second `Move` en oblique ; l'avance par axe du moteur reste, O-E19-28 a). **UO-1** `46 FF` sur `Flags` 0x100 → 0x2100, `45 FF` → 0x100, pc + 1, `Result` intact ; par `0x42` dans un
  programme d'enregistrement, c'est le bit du héros qui change. **UO-2** : T-SL2 piloté par le script (`46` puis la marche : pas
  de glissement, `ForceAdjusted` au tick 9 ; `45` : le glissement reprend). **T-SL6** (garde) : un mobile marqué `0x2000` qui
  pousse en oblique par le pas cinématique (direction 20, force aux signes du quadrant nord-est) contre un mur de cellules garde `ForceAdjusted` 1 dès le tick du contact (vert avant et
  après). T-R4 inchangé et vert. Garde d'arcs `ArcsWithoutSlide` : `SlideCount` 0
  sur tous les arcs à vrai contrôleur, sauf A10J. Commit : `feat(alundra): the hero slides along walls like the binary (stage 1)`
  - Fait le 2026-10-03 (commit unique, H4-1 et H4-2 fusionnés : A10J et la règle 2 ne pouvaient pas rester rouges entre les deux) : 8 tests dans `AlundraHeroSlideTests.cs` (T-SL1 à T-SL4, T-SL6, UO-1, UO-2, garde `ArcsWithoutSlide`), rouges d'abord aux valeurs prévues (T-SL1 `ForceAdjusted` 1 dès le tick 9, T-SL3 dès le tick 12, T-SL4 dès le tick 6, UO-1 `Flags` 256 pour 8448), T-SL2 et T-SL6 gardes vertes avant ; verts après avec les valeurs du plan. Écart de montage : les forces `-79872` puis `-159744` de T-SL1 viennent de l'animation 1 à la vitesse 312 (la table d'offsets donne -512 au nord, 208 donnait -53248 puis -106496). Suite complète Debug 2474/2474. Arrêt et disposition (session principale) : la garde a trouvé un glissement sur A14 et A18 (le héros sans 0x2000 contre un seul coin, comme le binaire, épingles intactes) ; `ArcsThatSlide` épingle 1 glissement sur A10J, A14 et A18 ; `AlundraArcSupport.cs` rejoint la liste fermée.
- ✅ **H4-2 — Liste fermée et arcs.** Les 6 lignes `0x45`/`0x46` quittent la liste versionnée (178 `B[1] @105`, `@129`, `@134`,
  `@157` ; 185 `B[1] @92`, `@108` ; 140 → 134), seules lignes de ces deux cartes : `MapsWithoutSkippedSite` reçoit 178 et 185
  (17 → 19), le test devient `…TheNineteenMaps…` avec `Assert.Equal(19, …)` ; **A10J** (`AlundraHeroJumpArcTests.cs` ~125-132) :
  `0x24 @2462` finit à la position du binaire, exacte, (47877120 ; 50790400) (aujourd'hui (47827968 ; 51838976) : le héros, à
  32 px, bute à y = 791 contre la cellule (29,48) de 48 px par son seul coin nord-ouest ; un glissement de + 0,75 px le dégage,
  puis il marche 16 px vers le nord jusqu'à la rangée 47, haute de 96 px, à y = 775,0) ; `(Z, ForceAdjusted)` à `@2463` reste
  (2097152, 1) ; la fenêtre d'image devient [F0+303, F0+309] (aujourd'hui [F0+295, F0+299], fin à F0+297 ; plus un tick de
  glissement et 16 px à la vitesse de la marche, sept ticks : F0+305 environ ; le binaire F0+308) ; les tests de mobiles sur de vrais murs (`AlundraNpcCharacterControllerMoverTests`, drapeaux
  sans 0x2000) sont relancés avec le compteur : un changement est un arrêt, avec diagnostic. Commit : `test(alundra): the slide on the story chain`
  - Fait le 2026-10-03 : 6 lignes retirées de la liste versionnée, `MapsWithoutSkippedSite` à 19 (178, 185), A10J aux valeurs du plan (image F0+308 dans [F0+303, F0+309], position exacte (47877120 ; 50790400), `(Z, ForceAdjusted)` (2097152, 1)), commentaire d'UH-6, `ImplementedOpcodes` avec 0x45 et 0x46 ; T-A19 inchangé et vert, tests de mobiles sur les murs inchangés. Commit fusionné avec H4-1 (message de H4-1).
- ✅ **H4-3 — Vérification et clôture.** Vérifié le 2026-10-03 : `Alundra.Tests` 2466 → 2474 (+8), 0 échec, en Release
  (45 s) puis en Debug (48 s), la Debug en dernier ; `cmp` sans écart (sha256 `b75702ca...fdc2`) ; les six traces à l'octet.
  Vérificateur neuf : **CONFIRMED** (table `0x80023734`, coins et ordre des portes relus dans le binaire ; A10J à F0+308 exactement,
  l'image du binaire ; les glissements d'A10J, A14 et A18 sont des glissements du héros sur un seul coin que le binaire fait
  aussi ; aucun PNJ ne glisse ; dix mutations attrapées). Avis reportés à E19.m : **P3** des mutations survivent (signe du
  glissement au sud et à l'est, porte d'entité, règle d'accord, glissement déplacé dans `MoveControllerAndPullPosition`) — le
  code est juste, des tests manquent ; P4 le terme de table des obliques n'est jamais atteint (échantillon à la position
  atteinte) ; P4 UO-1 ne passe pas par `0x42`.
- 🧪 **H4-4 — Recette** (auteur) : le héros glisse le long d'un coin de mur au lieu de
  s'y coller ; sur la 178 et la 185, les marches encadrées par `0x46`/`0x45` s'arrêtent au mur.

**Acceptation d'E19.h4.**
1. Tests de H4-1 rouges d'abord (sauf les gardes), verts après, valeurs écrites tenues ; une valeur contredite est un arrêt.
2. Code de test existant touché, liste fermée : A10J (`AlundraHeroJumpArcTests.cs`), la liste versionnée,
   `AlundraStoryChainOpcodeAudit.cs`/`AlundraStoryChainSkippedOpcodesTests.cs`, le commentaire d'UH-6,
   `IntroTraceHarnessTests.ImplementedOpcodes` ; rien d'autre ; les six traces à l'octet.
3. `Alundra.Tests` sans échec en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. Recette H4-4 faite par l'auteur.

**Risques.** Le glissement en oblique garde l'avance par axe du moteur (O-E19-28 a) ; le reste de contact en 16.16 à l'est et
au sud (O-E19-28 b, aussi contre les cellules) demande l'étape 2 (O-E19-42) ; l'image d'A10J `@2462` peut bouger d'un tick
(retards connus de la DLL).

### 1.2o E19.g — Effets visuels ⏳ (D-E19-51 à D-E19-55, D-E19-65 ; G0 ✅ ; G0b ✅ ; G2a 🧪 (G2a-1 et G2a-2 faites le 2026-10-03, recette G2a-4 en attente) ; G2c 🧪 (CONFIRMED le 2026-10-05, recette G2c-6 en attente) ; G2b, G1/G3, G4 à planifier)

**Découverte** (2026-10-03, lecture seule, deux surfaces : A le binaire et `DATAS.BIN`, B les données, le convertisseur,
l'analyseur, le moteur et la DLL ; notes, rendus et scripts dans le scratchpad de la session, `e19g-disc/A/notes.md` et
`e19g-disc/B/notes.md`). Faits porteurs **[binaire, données]** :
- **Réserve** : 128 créneaux de 0x80 octets en `0x80138608`, état 0 (libre) ou 2 (actif) ; le plus petit créneau libre gagne
  (`0x8003B9C4`) ; réserve pleine : la création rend 0, rien d'autre ; vidée à chaque chargement de carte (`0x8003C1A4`) ;
  un effet détruit est libéré un tick après sa fin. 84 sites de création : 3 opcodes, 1 au chargement, 80 natifs (30 du
  joueur, 10 du moteur commun, 39 d'IA et de boss).
- **Opcodes** `0x90`-`0x94`, `0xA0`-`0xA3` (tailles justes) : un script désigne un effet par l'**index de son enregistrement
  de carte**, jamais par une poignée ; chaque opcode agit sur tous les créneaux de cet index ; aucun `Result`, aucune
  suspension ; `0x93`/`0xA2` posent `z = (u16 << 16) + 1`, `0x94` des forces `s16 << 8`, `0xA1`/`0xA3` la première entité de la
  recherche.
- **Mise à jour**, une fois par tick logique après les événements de carte et les entités (`0x8003C410`) : liste d'images de
  3 octets (délai en ticks, ensemble d'images), fin 0 = détruire, 1 = boucler ; quatre modes de position (libre avec forces,
  attaché, figé, forces avec la profondeur d'une entité) ; ni collision, ni son ; gelée sous `& 0x48` mais toujours dessinée.
- **Chargement** : les enregistrements d'effets de la carte (12 octets : zone, drapeaux 0x80 table de carte, 0x40 apparition
  au chargement, index de table, X, Y, Z, animation) ; 544 enregistrements dans 157 cartes, **251 apparaissent seuls au
  chargement** (effets d'ambiance) ; la décompilation parcourt la mauvaise table et inverse le test de zone (le binaire
  l'emporte).
- **Rendu** : la même liste de sprites et le même tri par rangée que les entités (`0x8002DB48`, case `16 × min(depth >> 20, 59)
  + 6`) ; chaque image est un **quad texturé à quatre sommets libres**, avec sa page, sa palette et son mode de mélange PSX
  (additif dominant, soustractif, moyenne) et la semi-transparence **par texel** (bit STP de la palette : 609 quads mêlent texels
  opaques et semi-transparents) ; 63 % des références de quads sont déformées (mises à l'échelle, parallélogrammes,
  rotations). Deux banques : celle de la carte et une globale (29 sprites).
- **Données** : enregistrements, tables, images et quads sont déjà dans `data-extracted/data/map_N.json` et
  `map_alundra.json` ; **aucun pixel d'effet n'est extrait** (1397 quads vides, 40 partiels dans les planches), et le bit STP
  est perdu dans les palettes JSON. Le convertisseur n'exporte rien d'utile (`hero_effects.json`, copie brute non lue). Le
  moteur sait un mode de mélange par sprite et une clé de tri, mais **ni quad à sommets libres, ni semi-transparence par
  texel** (manques à rapporter).
- **Sur la chaîne, des effets portent le décor** : 476 l'aura de la vision (ellipse additive, trois faisceaux, tourbillon,
  dans une zone sans tuiles) ; 391 des **pièces du pont arrière et un mât brisé** (pas des vagues), qui comblent la poupe que
  les tuiles laissent noire puis dérivent pendant le fondu ; 135 **l'autel** qui glisse de 72 px pour découvrir l'escalier ;
  10 **le rocher** de la grotte qui roule ; Inoa la fumée des cheminées et les **portes ouvertes** ; 174/181 trois éclats
  blancs ; 163 quatre rais de lumière aux fenêtres. La liste fermée de la chaîne en compte 99 lignes.

**Programme proposé** (chaque étape, un plan relu et vérifié, comme les tranches d'E19) :
- **G0 — Extracteur** (sous-module de l'analyseur, branche dédiée) : planches d'effets par carte et globale, une case par région
  et palette, palettes brutes (bit STP gardé) ; puis **ré-extraction** et miroir prouvé (`diff -rq` vide hors les fichiers
  attendus). Piège connu : le remaster ré-extrait le 2026-09-19 a perdu le décodage du texte (mémoire de session) : ne jamais
  mirer à l'aveugle.
- **G1 — Convertisseur** : export des effets en données (animations, images, ensembles d'images, quads à quatre sommets avec
  page, palette, mode de mélange et masque STP), invariants (544 enregistrements, 251 au chargement, 136 + 29 tables, 363 vraies
  animations, 83 cases de remplissage écartées), export complet en place et double export.
- **G2 — Moteur** (sous-module, branche dédiée, ADR) : dessin de quads à sommets libres et mélange PSX par texel (masque STP),
  inerte tant que la DLL ne pousse rien ; tests du moteur et démo.
- **G3a — DLL** : la réserve de 128 créneaux aux règles du binaire, l'apparition au chargement, la mise à jour par tick, la
  poussée par image vers le moteur (patron d'`AlundraBackdropStage`) ; **G3b** : les neuf opcodes et les arcs (la liste fermée
  perd ses 99 lignes `EFFET`, les arcs A2/A4, A6, A10/A11, T-A10v, T-B9, A10J, A13, A15, A17 vérifient l'état de la réserve).
- **G4 — Recette** (auteur) : 476 (O-E19-30), 391, 162/169/176/183, 10, 174/181, 135, avec captures du back-buffer.

**Questions à l'auteur** (elles changent le travail ; le programme attend) :
- **Q-G1 — Ré-extraction** : G0 change l'extracteur de l'analyseur et ré-extrait les données du remaster hors du dépôt
  (`Alundra Remake/remaster-data-extracted`), puis les mire dans `data-extracted/`. D'accord pour la faire, et dans quel cadre
  (en séance avec l'auteur, ou par la session seule avec la preuve `diff -rq`) ?
- **Q-G2 — Entités aussi** : le même travail du moteur rendrait aussi la semi-transparence (7824 quads d'entités, le héros
  compris, dessinés opaques aujourd'hui) et les quads déformés des entités (environ 16 % des références). Le faire dans E19.g, ou
  garder les entités telles qu'aujourd'hui ?
- **Q-G3 — Effets d'ambiance** : les 251 effets qui apparaissent seuls au chargement s'allument d'un coup sur toutes les cartes
  (fidèle, visible partout), ou d'abord sur les cartes de la chaîne ?
- **Q-G4 — Effets natifs** : étincelles d'attaque, ramassage de vie et de magie, entrée et sortie de warp, éclats d'ennemis,
  éclaboussures (40 sites du joueur et du moteur commun, visibles sur toutes les cartes, la chaîne comprise) : dans E19.g, ou
  avec l'IA native (E14) ?
- **Q-G5 — Rais de lumière de la 163** : dans l'original, les quatre rais finissent leur animation et disparaissent 11 ticks
  après l'arrivée, sans doute pendant le fondu d'entrée. Garder, ou corriger comme un défaut de l'original (rais allumés) ?

**Choix de conduite déjà pris** (techniques) : la DLL tient la réserve et la machine d'animation au tick et pousse une liste de
quads par image (pas de 128 entités du moteur, pas de `.anim2d`) ; la semi-transparence par texel et les quads libres se font
dans le moteur (rapports de manque, jamais de contournement en amont) ; le tri reprend la formule des entités dans la couche
triée du monde ; les données d'effets s'exportent en données, pas en images précuites.

**Découverte du rendu moteur et du format de G0** (2026-10-03, lecture seule, `e19g2-disc/notes.md` du scratchpad ; un script
re-simule les 484 planches d'entités depuis `DATAS.BIN` et reproduit chaque pixel exporté) :
- **Moteur** : `SpriteRendererComponent` est un regroupeur maison à une liste triée, un appel par sprite avec `SpriteBatch.fx`, état de
  mélange par série (opaque, alpha non prémultiplié, additif, soustractif), échantillonnage par point (`PointClamp`, `:192`) ; les
  entités sont dessinées en opaque, l'alpha n'y sert qu'au rejet `<= 0.01`. Les effets devront entrer dans la même liste
  `YSortedWorld`, avec la clé des entités (`WallPlacementOverlay.cs:393-403`).
- **Schéma de G2 proposé** : fenêtre sur l'alpha brut du texel dans `SpriteBatch.fx` (la fenêtre par défaut reproduit
  l'existant) ; une entrée semi-transparente = deux dessins disjoints, texels opaques en opaque, texels STP dans l'état de leur mode
  (ABR0 alpha, ABR1 additif, ABR2 soustractif, ABR3 additif teinté à 64) ; ABR2 ne se fait pas en une passe (MonoGame n'a pas de
  mélange à double source) ; quad à quatre sommets libres coupé selon la diagonale TR–BL, sans élimination des faces (les quads miroir
  inversent l'ordre).
- **Manques du moteur à rapporter** : pas d'API de quad à sommets libres ; pas de semi-transparence par texel ; pas de champ
  semi/ABR sur `SpriteData` ; pas de piste de déformation par partie dans `.anim2d` (changement de format : ADR du moteur) ; pas de
  service pour pousser des quads d'effets par image ; mineurs (règle de tri privée, tri instable, plafond de 10 000 entrées non gardé,
  le mélange alpha réécrit l'alpha du back-buffer).
- **Format de G0** : un **code alpha par texel** dans chaque planche écrite par l'extracteur, 255 opaque (bit 15 de la couleur clair),
  128 semi-transparent (bit 15 posé, `0x8000` compris), 0 transparent (`0x0000`) ; le RVB ne change pas ; aucune palette brute (les
  mots bruts ne reconstruisent pas le masque : 301 quads ambigus). Sans risque avant G2 : le chargement ne prémultiplie pas, les
  entités sont dessinées en opaque, 0 des 85 icônes d'objets et du HUD et aucun portrait n'ont de texel semi-transparent, 128 se relit
  exactement par point et donne d'emblée la moyenne PSX dans l'état alpha.
- **Données** : 2514 quads semi-transparents d'entités et 88 d'effets mêlent texels opaques et STP ; aucun quad non semi ne contient
  de texel STP ; 205 des 484 planches d'entités changent, **alpha seulement** (1 525 717 pixels de 255 à 128) ; 1836 `.sprite` sont
  semi-transparents et 1923 `.anim2d` contiennent un quad déformé (30,8 % des références de quads d'entités).
- **Défaut du portage trouvé** : quand un quad a un `SourceX` impair et un `Swidth` impair, l'extracteur perd sa dernière colonne de
  texels (`GameMap.cs:188-194`) : 2646 pixels manquent dans 265 planches (695 dans 74 planches exportées, dont 83 dans
  `map_alundra`) ; la simulation le prouve.

##### 1.2o.1 E19.g G0 — Extracteur : texels semi-transparents, planches d'effets, portraits, colonne impaire ✅ (relectures n°1 et n°2 REVISE, révisée ; nouvelle époque, relecture de clôture ; annexe des valeurs versionnée)

**Faits.** Ceux de la découverte ci-dessus et de la cartographie de l'extracteur (`e19f-plan2/extract/notes.md`) : extracteur
`AlundraDataExtractor` (`Program.cs:77-151`, arguments `<gamePath> <extractionPath>`, dispositions par défaut `original` et
`compact`) ; sortie 4450 fichiers ; planche de carte 256 × 2048 RGBA ; `SaveSpriteSheet` (`GameMapHelper.cs:109-156`) ne parcourt que
les animations d'entités et le portrait de l'inventaire ; couleur par `FromPsxColor` (`ImageHelper.cs:12-21`), qui perd le bit 15 ;
les quads d'effets ont `AtlasX/AtlasY` à 0 (20 315 références) ; 25 banques à portrait (331 occurrences dans 184 cartes), dont deux de
48 × 72 (enregistrements 122 et 162), toutes les régions cibles vides, pixels et couleurs identiques d'une carte à l'autre (banques
relogées par carte : l'indice de portrait vient de la carte canonique, la plus basse) ; l'analyseur n'a pas de projet de tests ; la
régression du texte du 2026-09-19 venait de l'autre checkout de l'analyseur (`926dcb8`, sans `a8598f4`) : on n'extrait que depuis le
sous-module, après une construction Release fraîche ; le sous-module est aujourd'hui sur `chantier/e19f1-dialogue-boxes` (`b92b7b9`).

**Règles.**
- **G0-R1 — Code alpha** : chaque planche écrite par l'extracteur (entités et effets) porte par texel l'alpha 255, 128 ou 0 selon la
  règle ci-dessus ; RVB inchangé ; disposition des planches d'entités inchangée (le dernier texel dessiné gagne).
- **G0-R2 — Planches d'effets** (révision n°1) : une planche par carte qui a des effets et une globale, nommées
  `data/map_<n>_effectsheet.png` et `data/map_alundra_effectsheet.png` (87 fichiers, larges de 512) ; disposition compacte ; une
  case par **(page, palette, région)** — **1435 cases dessinées** (la clé `Signature` de la disposition compacte existante en
  donnerait une de plus par paire ne différant que par les bits ABR, qui appartiennent au quad, pas à la case) ; les 46 quads de
  taille 0 × 0 de la 161 sont ignorés (aucune case, `AtlasX/AtlasY` restent à 0 ; sinon l'extracteur plante, `ImageHelper.cs:179`) :
  18 875 références de quads d'effets quittent (0,0), dont 297 dans `map_161.json` ; `AtlasX/AtlasY` écrits
  sur les seules références de quads d'effets, jamais sur un objet partagé avec un quad d'entité (13 images d'effets sont partagées
  avec des entités de `map_alundra`) ; le code alpha est posé **après** le dessin des texels (un dessin par `DrawImage` mêlerait le
  RVB des texels semi-transparents) ; aucun nouveau champ public sérialisé hors de ceux de G0-R3 (le JSON s'écrit avec
  `IncludeFields = true`, `Program.cs:17`) et aucune modification de `FromPsxColor` (elle toucherait toutes les palettes, `ui/`,
  `tiled/` et les planches de tuiles).
- **G0-R3 — Portraits de dialogue** : pour chaque enregistrement de sprite à portrait, l'image (première image du bloc,
  `SpriteRecord.GetPortraitImageset`) est écrite dans la planche de la carte à sa région, et un champ **`DialoguePortrait`** rejoint
  chaque objet d'enregistrement de sprite du JSON de la carte, **de la même forme que `InventoryPortrait`** (image sérialisée :
  `Spritesheet`, `Palette`, `Sx`, `Sy`, `Swidth`, `Sheight`, les quatre coins, `Signature`, miroirs, `SourceX/Y`, `AtlasX/Y`), **omis
  quand il est nul** (331 champs dans 184 JSON).
- **G0-R4 — Colonne impaire** : un quad à `SourceX` impair et `Swidth` impair garde sa dernière colonne (défaut du portage corrigé).
- **G0-R5 — Ré-extraction et miroir** (D-E19-51, par la session seule) : construction Release fraîche de l'extracteur du
  sous-module ; extraction dans un dossier **neuf** (`D:\development\repo\Alundra Remake\remaster-data-extracted-e19g0`), jamais
  par-dessus l'existant ; `diff -rq` contre `data-extracted/` : **exactement** la liste de fichiers écrite d'avance par l'audit des
  valeurs (tout autre fichier changé est un arrêt) ; garde du texte (aucun marqueur non décodé ; `ReferenceTextDecoderTests` vert) ;
  puis copie de sauvegarde de `data-extracted/` (`data-extracted.bak-e19g0`, hors suivi git), renommage de l'ancien remaster en
  `remaster-data-extracted.bak-2026-10-03` et du dossier neuf en `remaster-data-extracted`, et miroir par `robocopy /MIR` lancé depuis
  PowerShell ; preuve finale `diff -rq` vide entre le remaster et `data-extracted/`. Rien n'est supprimé. Condition de lancement :
  chemin du jeu en barres obliques et `--no-launch-profile` (sinon `BALANCE.BIN.json` change et le diff sort de la liste). La
  sauvegarde va **hors du dépôt** : `D:\development\repo\Alundra Remake\data-extracted.bak-e19g0`.
- **G0-R6 — Export** (révision n°1 : la prochaine exportation emporterait sinon un écart sans propriétaire) : après le miroir, G0
  fait un export complet en place, sans changer le convertisseur ; manifeste avant et après : exactement les 103 textures de sprites
  exportées dont la planche source change (lignes `exported_today = 1` de l'annexe ; seule celle de la 398 ne change pas),
  `Sprites/hero/hero_effects.json` (copie brute des effets de `map_alundra` : 1671 des 2089 quads y prennent un `AtlasX/AtlasY`
  non nul) et `report.json`, rien d'autre ; double export identique hors `report.json` ; `Alundra.Tests` sur cet export. En jeu, le
  seul changement visible est la colonne impaire rendue ; le code alpha est ignoré tant que G2 n'existe pas (entités dessinées en
  opaque, rejet à `<= 0.01`).

**Tâches.**
- **G0-0 — Annexe des valeurs** (faite, révision n°1) : l'audit des valeurs a écrit, et le plan versionne dans
  `docs/plan-e19-g0-annexe/`, la liste exacte des fichiers (`predicted_changes.txt` : 613 modifiés, 381 planches d'entités et 232
  JSON, et 87 ajoutés, 700 lignes) et les fichiers de valeurs (empreintes RGBA des 484 planches d'entités et des 87 planches
  d'effets, les 1435 cases, les 331 portraits, la classe de chaque changement par planche et par JSON), tous dans la seule variante
  « quads vides ignorés » (`cell_skip0` de l'audit ; révision n°2) ; la simulation reproduit
  aujourd'hui chacune des 484 planches à l'octet près (0 pixel d'écart).
- **G0-1 — Extracteur** (sous-module, branche `chantier/e19g0-extractor` **empilée sur** `chantier/e19f1-dialogue-boxes` `b92b7b9`) :
  G0-R1 à G0-R4, un commit par règle, chacun construit.
- **G0-2 — Preuves indépendantes** (scripts du scratchpad, qui ne partagent pas le code de l'extracteur) : la re-simulation des 484
  planches d'entités depuis `DATAS.BIN` (script de la découverte) égale chaque pixel, alpha compris ; les 1435 cases d'effets égales à
  un décodage indépendant ; les 25 portraits égaux à un décodage indépendant ; les 2646 pixels de la colonne impaire présents ; les
  empreintes de l'annexe égales ; **preuve des JSON** : chaque JSON changé égale sa version de `data-extracted/` une fois retirés les
  champs `DialoguePortrait` et remis à 0 les `AtlasX/AtlasY` des références de quads d'effets, et chaque référence de quad d'effet
  désigne une case égale à son décodage indépendant (0 écart, 0 référence vers une case fausse ou absente), sauf les 46 références
  aux quads vides de la 161, qui restent à (0,0) sans case ; les quads d'entités gardent leurs coordonnées.
- **G0-3 — Ré-extraction et diff** (G0-R5, avant le miroir).
- **G0-4 — Miroir et preuve finale** (G0-R5).
- **G0-5 — Gardes et export** : tests du convertisseur verts sur le nouveau `data-extracted/` (aucun ne lit l'alpha des planches ni
  les nouveaux champs ; un test qui bouge est un arrêt) ; puis l'export de G0-R6 et `Alundra.Tests` dessus.
- **G0-6 — ADR-0030** (le format des planches de l'extracteur : code alpha par texel ; planches d'effets ; portraits ; colonne
  impaire) ; pointeur du sous-module ; le plan.

**Acceptation.**
1. Les quatre preuves de G0-2 sans écart.
2. `diff -rq` de la ré-extraction : exactement `docs/plan-e19-g0-annexe/predicted_changes.txt` (381 planches d'entités, dont 47
   par l'alpha seul ; 87 planches d'effets nouvelles ; 232 JSON) ; texte décodé ; la preuve des JSON sans écart.
3. Après le miroir : `diff -rq` vide ; sauvegardes présentes aux chemins hors du dépôt ; tests du convertisseur verts.
4. Export (G0-R6) : manifeste exactement comme prévu, double export, `Alundra.Tests` en Release puis en Debug sur l'export, la
   Debug en dernier, `cmp` sans écart ; les six traces à l'octet.
5. `git status` du parent : seulement le pointeur du sous-module, l'ADR, le plan et son annexe.

**Retour arrière** (révision n°2) : remettre le pointeur du sous-module ; renommer les dossiers dans l'autre sens et refaire le miroir
depuis la sauvegarde ; puis un export complet en place dont le manifeste égale le manifeste « avant » de G0-R6 (hors `report.json`),
et `Alundra.Tests` sur cet export ; le retour arrière n'est fini que quand ce manifeste est égal ; rien n'est supprimé.

**Risques.** Une extraction depuis le mauvais checkout ou une construction périmée (G0-R5) ; un `robocopy` lancé depuis Git Bash
(chemins avec espaces : il ne copie rien) ; la colonne impaire élargit le diff à des RVB (liste écrite d'avance) ; le dernier texel
dessiné gagne aussi pour l'alpha (déjà le cas pour les couleurs).

- **Fait le 2026-10-03.** Sous-module `alundra-datas-analyser`, branche `chantier/e19g0-extractor` (depuis `b92b7b9`) : `97ca6a2` (R1 code
  alpha), `876dfd9` (R2 planches d'effets), `109f62e` (R3 portraits), `d8d9230` (R4 colonne impaire), chacun construit (Release).
  Preuves indépendantes (Python, `DATAS.BIN`) : 0 écart sur les 484 planches d'entités (253 755 392 pixels, RVB et alpha, empreintes
  de l'annexe égales), 87 planches d'effets et 1435 cases égales à leur décodage (positions de l'annexe, 20 269 références de quads
  pointant une case juste, 46 quads 0 × 0 laissés à (0,0)), 331 portraits (champs de l'annexe, 895 104 texels), 2646 texels de la
  colonne impaire présents, 484 JSON égaux à l'ancien une fois retirés `DialoguePortrait` et remis à 0 les `AtlasX/AtlasY` des
  quads d'effets. Ré-extraction (Release fraîche, chemin en barres obliques, `--no-launch-profile`, 46 s, dossier neuf
  `remaster-data-extracted-e19g0`, 4537 fichiers) : `diff -rq` contre `data-extracted/` = exactement les 700 lignes de
  `predicted_changes.txt` (613 M, 87 A), 0 marqueur de texte non décodé. Sauvegardes hors dépôt : `data-extracted.bak-e19g0` (4450
  fichiers, identique), `remaster-data-extracted.bak-2026-10-03` (ancien remaster, identique à `data-extracted/` avant le miroir) ;
  nouveau dossier renommé `remaster-data-extracted` ; `robocopy /MIR` depuis PowerShell, `diff -rq` final vide, 4537 fichiers.
  Tests du convertisseur 418/418. Export en place : manifeste SHA-1 (23 741 fichiers hors DLL, pdb, `.casaeditor/`) avant/après =
  exactement 105 fichiers changés (les 103 textures, `hero_effects.json`, `report.json`), aucun ajouté ni retiré ; second export
  identique hors `report.json` ; `Alundra.Tests` 2539/2539 en Release puis en Debug ; `cmp` de la DLL déposée contre la build Debug
  sans écart ; six traces à l'octet (`git diff --ignore-cr-at-eol` vide), les quatre traces du héros remises par `git checkout`.
  Écarts au plan : aucun. ADR-0030. Decisions: see ADR-0030.
- ✅ **Vérification de G0** (2026-10-03) : vérificateur neuf **CONFIRMED** (preuves rejouées sur l'état actuel contre la sauvegarde :
  0 écart ; son propre `diff -rq` = l'annexe à la ligne près, 700 lignes ; dossiers : sauvegarde 4450, ancien remaster 4450, remaster et
  `data-extracted/` 4537, `diff -rq` vides deux à deux ; 571 planches à alpha dans {0, 128, 255} ; 331 `DialoguePortrait` de la forme
  d'`InventoryPortrait` ; export : 23 741 empreintes égales au manifeste d'après, 105 fichiers changés égaux à la règle de l'annexe ;
  convertisseur 418/418 ; `Alundra.Tests` 2539/2539 en Debug ; `cmp` sans écart ; traces à l'octet). **Point pour l'auteur** (P3) : la
  correction de la colonne impaire est dans la fonction partagée `GameMap.CropSpriteIndices` (`GameMap.cs:224-229`), qui sert aussi
  les moteurs de rendu de la décompilation (`GraphicManager`, `MainInventoryManager`, `HudManager`, `SubInventoryManager`,
  `EntityEventHandlers`, la visionneuse) : la décompilation garde désormais elle aussi la colonne ; le portage n'en dépend pas (aucune
  référence à `AlundraEngine` dans le convertisseur ni la DLL) ; règle de l'auteur du 2026-10-03 (ne pas corriger la décompilation) :
  accepter et le dire dans ADR-0030, ou déplacer la correction dans un chemin propre à l'extracteur. P4 un `log.txt` vide non suivi
  laissé par l'extracteur dans `alundra-datas-analyser/AlundraTools/` (laissé tel quel). Branche de l'analyseur
  `chantier/e19g0-extractor` (`d8d9230`, empilée sur f1), non mergée.

##### 1.2o.1b E19.g G0b — Planches d'entités en disposition compacte (chaque sprite avec sa palette) ✅ (relecture n°1 READY ; faite et CONFIRMED le 2026-10-03 ; recette G0b-6 à faire par l'auteur)

**Faits** (mesure des palettes `e19g-palette/notes.md` et audit des valeurs `e19g0b-audit/notes.md` du scratchpad, 2026-10-03,
lecture seule) :
- Les planches d'entités sont écrites dans la disposition `Original` (défaut de `--spritesheet-layout`,
  `alundra-datas-analyser/AlundraTools/AlundraDataExtractor/Program.cs:147`) : une case par région de la VRAM ; les variantes de
  palette d'une même région partagent la case et la dernière peinte gagne. 11 803 des 46 497 images uniques ne sont pas dans leur
  case. Dans l'export, **2702 des 6909 sprites** posés sur une planche d'entités montrent des pixels qui ne sont pas les leurs (2700
  en couleur, 2 par le seul bit STP), dans 172 des 364 dossiers d'entités : sur la chaîne, Thyea et Sierra avec la palette de Naomi
  (21/21 chacune), Kisha avec celle de Fein, Talis avec celle de Bergus, Yustel avec celle de Myra, Bonaire avec celle de Lutas,
  Gustav avec celle de Yuri, Lyman et Jaylen avec celle de Zane, la Rancune de Melzas presque noire (47 sprites sur 74) ; **23 des
  85 icônes d'objets** (le petit cœur, 1 Gilda et le grand cristal en aplat `#382800`, le bâton magique brun au lieu de bleu-violet,
  les tomes de la Terre, de l'Eau et du Feu sur la palette 32) ; le héros : 44 sprites, aucun dans une animation jouée aujourd'hui ;
  Naomi : aucun. Défaut du portage : l'original lit chaque quad dans la VRAM avec sa propre palette.
- La disposition `Compact` existe (`GameMapHelper.cs:249-301` : une case par `Signature`, la plus haute d'abord puis par signature,
  512 de large, 1 px de marge) et passe par la même fonction que l'`Original` (`SaveSpriteSheet`, `:126-168`) : énumération
  (animations, portrait de l'inventaire, portraits de dialogue, `:391-444`), toile de mots bruts (`DrawSpriteWords`), code alpha
  (`SaveWordCanvas`, `:197-221`), colonne impaire (`GameMap.cs:224-229`), tamponnage des positions par signature (`:160-167`) ; le
  choix de la disposition (`:143-148`) ne fixe que les positions : **les règles de G0 tiennent, aucun code de l'extracteur n'est à
  changer**. Les planches d'effets n'en dépendent pas (les 13 signatures de `map_alundra` à la fois d'entités et d'effets sont des
  objets distincts, sans position commune).
- Le convertisseur ne lit que `AtlasX/AtlasY` (`SpriteBankReader.cs:564-565` ; le portrait de l'inventaire `:242-255`) et les écrit
  dans le `.sprite` (`SpriteWriter.cs:905`) ; les identifiants de sprites viennent de la planche et de la signature (`:822-825`) :
  inchangés, comme l'index des icônes (`ItemsWriter.cs:155-170`) ; les `.texture` ne portent pas de taille. Aucun test du
  convertisseur, de la DLL ni du moteur n'épingle une position ou une taille réelle (montages synthétiques) ; la DLL ne lit que la
  largeur et la hauteur des sprites (`AlundraHudScreen.cs:196-197`, `AlundraInventoryScreen.cs:177-178`) ; le moteur ne se sert de la
  position que comme rectangle source. Le jeu tourne en HiDef ; 661 PNG exportés ne sont déjà pas en puissance de deux.
- Simulation de l'audit : les 484 planches d'aujourd'hui re-simulées à 0 pixel près (RGBA) et les 914 789 références d'entités à
  leur position ; en `Compact`, **6909 des 6909** découpes exportées égalent leur propre décodage ; sur les sprites exportés, les
  sprites non semi qui portent des texels STP passent de 463 à 0 et les sprites semi sans texel STP de 235 à 4 (C1 de G2a levée).

**Règles.**
- **G0b-R1 — Disposition** (D-E19-65) : les planches d'entités (`data/map_<n>_spritesheet.png`, `data/map_alundra_spritesheet.png`)
  sont écrites dans la disposition `Compact` ; le défaut de `--spritesheet-layout` passe à `Compact` (`Program.cs:147`), pour qu'une
  ré-extraction aux options par défaut ne revienne pas en silence à l'`Original` ; les textes qui disent l'ancien défaut suivent
  (`GameMapHelper.cs:10-13`, `:126`, `:223-233`, `SiImage.cs:48-53`, la table des options et la phrase qui la précède dans
  `alundra-datas-analyser/docs/alundra-tiled-map-exporter-usage.md:26-31`) ; aucun autre code de l'extracteur ne change.
- **G0b-R2 — Ré-extraction et miroir** (D-E19-51, comme G0-R5) : construction Release fraîche de l'extracteur du sous-module ;
  extraction aux options par défaut (aucune option de disposition passée), chemin du jeu en barres obliques, `--no-launch-profile`,
  dans un dossier **neuf** `D:\development\repo\Alundra Remake\remaster-data-extracted-e19g0b` ; `diff -rq` contre `data-extracted/` :
  **exactement** `docs/plan-e19-g0b-annexe/predicted_changes.txt` (903 `M` : les 484 planches d'entités et 419 JSON ; aucun `A`,
  aucun `D` ; tout autre fichier changé est un arrêt) ; garde du texte (aucun marqueur non décodé) ; puis sauvegardes **hors du
  dépôt** `D:\development\repo\Alundra Remake\data-extracted.bak-e19g0b` (copie de `data-extracted/`) et
  `remaster-data-extracted.bak-e19g0b` (l'ancien remaster renommé ; les sauvegardes de G0 restent en place), renommage du dossier
  neuf en `remaster-data-extracted`, miroir par `robocopy /MIR` lancé depuis PowerShell ; preuve finale `diff -rq` vide. L'exemple
  de commande de la doc d'usage de l'analyseur (`:40`, chemin à barres obliques inverses, construction Debug, écriture dans le
  remaster lui-même, `--tiled-tileset-layout original`) n'est jamais copié. Rien n'est supprimé.
- **G0b-R3 — Export** : après le miroir, export complet en place, sans changer le code du convertisseur ; manifeste avant et après
  (comme G0-R6) : **exactement** `docs/plan-e19-g0b-annexe/export_predicted_changes.txt` (7014 `M` : les 104
  `Sprites/Textures/*_spritesheet.png`, 6908 `Entities/**/*.sprite`, `UI/Portraits/sprite_61779762221058.sprite`, `report.json` ;
  aucun ajout ni retrait) ; dans chaque `.sprite` changé, seuls `location.x` et `location.y` changent ; double export identique hors
  `report.json`.
- **G0b-R4 — Convertisseur, commentaire** : la doc de `SpriteWriterInventoryPortraitTests` (`:13-17`) dit que la position (200, 568)
  est celle du vrai portrait ; elle devient (431, 121) : la doc dit que le montage reprend les valeurs de la disposition `Original`
  au moment de l'écriture et que l'écrivain recopie la position lue ; aucune valeur ni assertion du test ne change.
- **G0b-R5 — DLL et moteur** : rien.

**Tâches.**
- **G0b-0 — Annexe des valeurs** (faite) : `docs/plan-e19-g0b-annexe/` (liste du diff de la ré-extraction, liste du manifeste de
  l'export, tailles et empreintes RGBA des 484 planches d'entités en `Compact`, positions des 332 portraits, changements par JSON,
  textures exportées, icônes fausses ; les deux gros fichiers, cases et `.sprite`, restent dans le scratchpad avec leur empreinte).
- **G0b-1 — Extracteur** (sous-module, branche `chantier/e19g0b-compact` **empilée sur** `chantier/e19g0-extractor` `d8d9230`) :
  G0b-R1, un commit, construit en Release.
- **G0b-2 — Ré-extraction, diff et preuves** (G0b-R2, avant le miroir), preuves par scripts du scratchpad qui ne partagent pas le
  code de l'extracteur : (a) chaque planche d'entités : taille et empreinte RGBA égales à `expected_entity_sheets_compact.tsv` ;
  (b) **preuve des JSON** : chaque JSON changé égale sa version de `data-extracted/` une fois chaque `AtlasX/AtlasY` de référence
  d'entité remplacé par sa valeur de `expected_cells_compact.tsv` (empreinte de l'annexe vérifiée d'abord), rien d'autre ; (c) les
  332 portraits à leur position de `expected_portraits_compact.tsv` ; (d) chaque référence d'entité désigne une case dont les pixels
  égalent son propre décodage depuis `DATAS.BIN` (0 écart) ; (e) garde du texte.
- **G0b-3 — Miroir et preuve finale** (G0b-R2).
- **G0b-4 — Gardes et export** : tests du convertisseur verts sur le nouveau `data-extracted/` (418 ; un test qui bouge est un
  arrêt) ; G0b-R4 ; export de G0b-R3, manifeste, double export, chaque `.sprite` changé égal à l'ancien hors `location.x/y` et sa
  nouvelle position égale à `export_sprites.tsv` ; `Alundra.Tests` en Release puis en Debug sur l'export, la Debug en dernier, `cmp`
  sans écart ; les six traces à l'octet.
- **G0b-5 — ADR-0031** (planches d'entités en disposition `Compact` ; elle amende la ligne « Original layout » d'ADR-0030, dont le
  statut gagne « entity sheet layout amended by ADR-0031 ») ; pointeur du sous-module ; le plan.
- **G0b-6 — Recette** (auteur) : (1) carte 10, Thyea, Sierra et Naomi côte à côte : Thyea en robe verte (`#587050`), cheveux blond
  pâle, Sierra en robe gris lavande (`#9890a0`), Naomi inchangée en bleu, Kisha aux cheveux blond orangé ; (2) carte 476, la Rancune
  de Melzas : corps bleu (`#104888`), plus de silhouette noire ; (3) carte 362, Lyman et Jaylen avec leurs propres couleurs ; (4)
  objets : un cœur tombé rouge (`#883038`), un Gilda doré, le cristal blanc et gris, les trois tomes de couleurs différentes, le bâton
  magique bleu-violet ; (5) gardes : portrait de l'inventaire identique, le héros (attente, marche) et Naomi inchangés.

**Acceptation.**
1. G0b-1 construit ; aucun changement de l'extracteur hors G0b-R1.
2. `diff -rq` de la ré-extraction : exactement `predicted_changes.txt` (903 lignes) ; les cinq preuves de G0b-2 sans écart.
3. Après le miroir : `diff -rq` vide ; les sauvegardes présentes ; tests du convertisseur verts (418).
4. Export : manifeste exactement `export_predicted_changes.txt` (7014 lignes), double export, les `.sprite` changés seulement en
   `location.x/y`, aux positions de `export_sprites.tsv` ; `Alundra.Tests` en Release puis en Debug (aucun test touché), `cmp` sans
   écart ; les six traces à l'octet.
5. `git status` du parent : seulement le pointeur du sous-module, le commentaire de G0b-R4, ADR-0031, le statut d'ADR-0030, l'index
   des ADR, le plan et son annexe.

**Retour arrière** : remettre le pointeur du sous-module ; renommer les dossiers dans l'autre sens (`remaster-data-extracted` →
`remaster-data-extracted-e19g0b`, `remaster-data-extracted.bak-e19g0b` → `remaster-data-extracted`) et refaire le miroir ; export complet
en place dont le manifeste égale le manifeste « avant » (hors `report.json`), puis `Alundra.Tests` ; rien n'est supprimé.

**Risques.** Une extraction depuis le mauvais checkout ou une construction périmée ; un `robocopy` lancé depuis Git Bash (chemins avec
espaces : rien n'est copié) ; l'exemple de la doc d'usage copié (il réécrirait `tiled/` et `BALANCE.BIN.json`) ; les octets des PNG
ne se prédisent pas (preuve par pixels décodés et tailles) ; textures hors puissance de deux (déjà présentes) ; les couleurs de la
recette sont des couleurs dominantes d'un sprite chacune, sans les changements de palette faits en jeu.

- **Fait le 2026-10-03.** Sous-module `alundra-datas-analyser`, branche `chantier/e19g0b-compact` (depuis `d8d9230`) : `51b77d8` (G0b-R1 :
  défaut `Compact` à `Program.cs:147`, `GameMapHelper.SaveSpriteSheet`, commentaires de `GameMapHelper.cs` et `SiImage.cs`, table de la doc
  d'usage ; aucun autre code), construit en Release (0 erreur). Ré-extraction (Release, aucune option de disposition, chemin en barres
  obliques, `--no-launch-profile`, dossier neuf `remaster-data-extracted-e19g0b`, 4537 fichiers) : `diff -rq` contre `data-extracted/` =
  exactement les 903 lignes de `predicted_changes.txt` (903 M, aucun A ni D). Preuves indépendantes (Python, `DATAS.BIN`, empreinte
  de `expected_cells_compact.tsv` vérifiée d'abord), 0 écart : (a) 484 planches, taille et empreinte RGBA égales à l'annexe, alpha dans
  {0, 128, 255} ; (b) 484 JSON égaux à l'ancien une fois les 914 789 `AtlasX/AtlasY` d'entités remplacés par les cases de l'annexe,
  rien d'autre ; (c) 332 portraits (1 inventaire, 331 dialogue) à leur position ; (d) 914 789 références, chacune sur une case égale à
  son décodage ; (e) 0 marqueur de texte non décodé. Miroir : `data-extracted.bak-e19g0b` (copie, 4537 fichiers, identique avant le
  miroir), ancien remaster renommé `remaster-data-extracted.bak-e19g0b`, dossier neuf renommé `remaster-data-extracted`, `robocopy /MIR`
  depuis PowerShell, `diff -rq` final vide (4537 fichiers) ; les sauvegardes de G0 (`data-extracted.bak-e19g0`,
  `remaster-data-extracted.bak-2026-10-03`) sont intactes (4450 fichiers chacune). Convertisseur : G0b-R4 (doc de
  `SpriteWriterInventoryPortraitTests`, aucune valeur ni assertion), tests 418/418. Export en place : manifeste SHA-1 (23 741
  fichiers hors DLL, pdb, `.casaeditor/`) avant/après = exactement les 7014 lignes de `export_predicted_changes.txt` (aucun ajout ni
  retrait) ; les 6909 `.sprite` changés égalent leur ancien octet pour octet (empreinte SHA-1 du manifeste « avant ») une fois
  `location.x/y` remis à l'ancienne valeur, et leur position égale `export_sprites.tsv` ; les 104 textures égales à la planche source,
  taille et empreinte RGBA de `export_textures.tsv` ; second export identique hors `report.json` ; `Alundra.Tests` 2548/2548 en
  Release puis en Debug (2548 et non 2539 : les tests ajoutés par E19.m0 depuis G0, aucun test ne bouge) ; `cmp` de la DLL déposée contre
  la build Debug sans écart ; six traces à l'octet (`git diff --ignore-cr-at-eol` vide), les quatre traces du héros remises par
  `git checkout`. Écarts au plan : aucun (le plan chiffre `Alundra.Tests` à 2539 par héritage de G0 ; la preuve des `.sprite` a utilisé
  le manifeste « avant » faute de copie des anciens fichiers). ADR-0031 (Proposed : décision D-E19-65 de la session en mode AUTO, à
  confirmer par l'auteur). Decisions: see ADR-0031. Reste la recette G0b-6 (auteur).
- ✅ **Vérification de G0b** (2026-10-03) : vérificateur neuf **CONFIRMED**, par ses propres scripts (décodage de `DATAS.BIN`
  écrit à part, sans `g0lib`) : diff de l'extracteur limité au défaut et aux textes ; `data-extracted/` égal au remaster, 903 lignes
  d'écart avec la sauvegarde égales à l'annexe, sauvegardes de G0 intactes ; les 914 789 références d'entités et les 20 315 d'effets
  égales à leur propre décodage ; les JSON ne changent que par des `AtlasX/AtlasY` d'entités ; export : 7014 lignes égales à l'annexe,
  les 6909 `.sprite` égaux à l'ancien hors `location.x/y`, double export ; convertisseur 418/418, `Alundra.Tests` 2548/2548 en
  Release puis en Debug, `cmp` sans écart, traces à l'octet (les quatre du héros remises ensuite par la session) ; contrôles
  ponctuels : icônes 84, 69, 82 (rouge, doré, blanc et gris au lieu de l'aplat `#382800`), bâton magique bleu-violet, Thyea et
  Sierra distinctes de Naomi. P4 : le `log.txt` vide non suivi de l'analyseur, recréé par l'extraction (laissé tel quel).

##### 1.2o.2 E19.g G2a — Semi-transparence par texel des sprites d'entités (moteur et convertisseur) ⏳ (révision n°1 après G0b ; relecture n°2 REVISE, révision n°2 ; nouvelle époque, relecture de clôture READY ; faite et CONFIRMED le 2026-10-03, section 🧪 tant que la recette G2a-4 manque)

**Découpage d'après G0** (2026-10-03) : **G2a** (cette tranche) rend la semi-transparence par texel des sprites d'entités
(D-E19-52) : moteur et convertisseur ; **G2b** (quads à quatre sommets libres et piste de coins dans `.anim2d`, changement de format,
ADR du moteur) ; **G1/G3** (effets : export, service du moteur qui reçoit les quads d'effets par image, réserve de 128 effets de la DLL,
opcodes) ; **G4** recette. G2a dépend de G0 et de G0b (faits : le code alpha est dans les planches, chaque sprite a sa propre case).

**Historique.** Relecture n°1 **REVISE** et audit des valeurs (2026-10-03, `e19g2a-audit/notes.md` du scratchpad) : fenêtre par défaut
sur le produit et non sur l'alpha brut, décodage non écrit et lu à tort sur la `Signature` (11 montages de test deviendraient semi),
liste fermée des tests, valeurs de la démo, garde des 10 000 entrées au remplissage des sommets ; contradictions C1 (planches en
disposition `Original` : couleurs et masques d'autres palettes) et C2 (aucun sprite d'entité en mode 2). C1 est levée par G0b
(§1.2o.1b) ; cette révision n°1 (après G0b) reprend chaque point ; les faits sont recomptés sur l'export d'après G0b
(`e19g2a-recount/notes.md` du scratchpad). Relecture n°2 **REVISE** (P2) : la règle de capacité ne pouvait pas être réfutée (le test
sans périphérique ne voit pas le `VertexBuffer`, et un tampon resté à 40 000 sommets aurait passé) et demandait à tort de faire
grandir le tampon d'indices. Deux REVISE automatiques : disposition **FIX** (fait corrigé, couture nommée, preuve sur périphérique
ajoutée à la démo) ; nouvelle époque, une relecture de clôture.

**Faits.**
- **Données** (export d'après G0b) : 6908 `.sprite` d'entités, dont **1836 semi** (ABR1 1615, ABR0 218, ABR3 3, **ABR2 0**) ; parmi
  eux 1528 entièrement STP, **304 mêlant texels opaques et STP** (4801 références de quads), 4 sans texel STP (le héros ×2, Magie de feu
  Niv.2, Magie de vent Niv.2 (bas) : leur propre décodage n'en a pas, la PS1 les dessine opaques aussi) ; **aucun sprite non semi ne
  porte de texel STP** (0 sur 5073) ; alpha des découpes dans {0, 128, 255} ; 1 748 055 texels d'alpha 128 dans les 104 planches
  exportées, 88 planches en portent ; `SpriteQuad.Spritesheet` égale `Signature & 0xFF` sur les 160 355 quads réels, mais les montages
  de test du convertisseur ont `Spritesheet` 0 (2 pour le portrait) et des octets bas de `Signature` quelconques
  (`SpriteWriterTests.cs:68`, `:74`, `:203-204`, `:296/:299`, `:487/:490/:493`, `SpriteWriterDeterministicIdsTests.cs:154/:157`, et
  les signatures modèles de `SpriteWriterNpcCharacterControllerTests.cs:150`, `SpriteWriterAnimationEndTests.cs:171/174`,
  `SpriteWriterCollisionTests.cs:212/215`, `SpriteWriterBodyPrefabTests.cs:184`).
- **Binaire** **[binaire]** (`e19g2a-recount/notes.md` §2) : `RenderEntities` (`0x8002E130`) appelle l'émetteur de quads `0x8002DB48`
  (appel en `0x8002E1D4`), commun aux entités et aux effets ; il lit l'octet 0 de l'enregistrement d'image (`0x8002DC28`) : **bit 3**
  allumé → bit 1 du code de primitive (semi) et **ABR = `(b & 0x30) >> 4`** ; éteint → opaque, ABR 0 (`0x8002DC30`-`0x8002DC58`) ; page
  = `b & 7` (`0x8002DC5C`-`0x8002DC8C`, table `0x800DC4F0` construite par `GetTPage` en `0x8002C09C`-`0x8002C0F4`, ABR dans les bits 5-6
  du tpage) ; les bits 6-7 ne sont jamais lus. Les primitives sont `POLY_FT4` en texture brute (`SetShadeTex(p, 1)`, `0x8002DE4C`) :
  aucune teinte par entité ; le mode 3 vaut fond + texel/4.
- **Moteur** (branche `chantier/e19k2-layer-mask` `987f0c7f`, lignes inchangées depuis l'audit) : le shader
  (`CasaEngine/Content/Shaders/SpriteBatch.fx:40-49`) rejette `(tex × Color).a <= 0.01` et rend `tex × Color` ; les entités passent
  par `AnimatedSpriteComponent.DrawComposedAnimation` (`:507-547`) : chemin trié (`:537-541`, entités à `DepthSortable2DComponent`,
  toutes les entités converties) par une surcharge sans mélange, donc opaque, et chemin par `zOrder` (`:543-545`) ; `SpriteData`
  (`SpriteData.cs:18-50`) n'a aucun champ de mélange ; états de mélange `GetBlendState` (`SpriteRendererComponent.cs:251-260` ; opaque
  `:104-112`, additif `:62-70`, soustractif `:74-82`, alpha non prémultiplié) appliqués par séries contiguës ; échantillonnage par point
  (`:192`) ; l'effet `_effect` est partagé par `Draw`, `DrawStaticBatch` (`:302-356`) et `DrawDirectly` (`:264-300`) et ses paramètres
  persistent d'un dessin à l'autre, y compris après `TryReloadBuiltInShader` (`:130`) ; capacité fixe de 10 000 entrées
  (`NbSprites`, `:36-37`) : `UpdateBuffer` (`:395-417`) écrit les sommets de chaque entrée dans `_vertices` (40 000 sommets) et lève
  `IndexOutOfRange` à la 10 001e, avant le dessin ; le `VertexBuffer` n'existe qu'après `LoadContent` (`:122`, 40 000 sommets) et
  `SetData` est borné à `NbSprites * 4` (`:416`) ; le tampon d'indices compte six indices `{0, 1, 2, 0, 2, 3}`, réutilisés par le
  sommet de base `i * 4` de chaque entrée (`:123-124`, `:237`) ; les tests existants évitent `UpdateBuffer` faute de périphérique
  (`SpriteRendererComponentBlendModeTests.cs:216-220`) ; `List.Sort` (`:399`) n'est pas stable ; chargement des textures sans prémultiplication (`Texture2DLoader.cs:12`).
- **Tests existants** (audit §2) : aucun test du moteur ne soumet un `AnimatedSpriteComponent` et ne compte ses entrées ; les tests qui
  comptent les entrées en file (fonds, cellules, fondus, couches de tuiles) passent des entrées opaques ou un `SpriteBlendMode`
  explicite : ils ne bougent pas tant que le dédoublement ne dépend que du nouveau mode PSX ; aucun test du convertisseur n'épingle le
  JSON complet d'un `.sprite` ; aucun côté DLL.

**Règles.**
- **G2a-R1 — Moteur, shader** : le rejet existant sur le produit `(tex × Color).a <= 0.01` reste pour tout dessin ; s'y ajoute une
  fenêtre sur l'alpha **brut** du texel, `(AlphaMin ; AlphaMax]`, dont la valeur neutre (−1 ; 2] ne rejette rien : l'existant est
  reproduit exactement ; échantillonnage par point seulement (documenté dans le shader). **Chaque** chemin qui dessine avec `_effect`
  (`Draw` à chaque entrée, `DrawStaticBatch`, `DrawDirectly`, et après `TryReloadBuiltInShader`) pose la fenêtre qu'il veut (la neutre
  hors des passes semi) : aucune fenêtre ne fuit d'un dessin à l'autre.
- **G2a-R2 — Moteur, mode semi d'un sprite** : `SpriteData` gagne un champ optionnel de semi-transparence PSX (`None` par défaut, puis
  `Mode0` à `Mode3`), lu par `SpriteData.Load` (absent = `None`) et écrit par le sérialiseur de l'éditeur
  (`EditorAssetJsonSerializer.SaveSpriteData`, `:370-402`) **seulement quand il n'est pas `None`** ; `AnimatedSpriteComponent` passe le
  mode de chaque partie au chemin trié par une **nouvelle surcharge ou un nouveau paramètre** ; aucune surcharge existante de
  `DrawSprite` ne change de signature ni de valeur par défaut ; le chemin par `zOrder` reste opaque (hors tranche, documenté).
- **G2a-R3 — Moteur, deux dessins disjoints** : une partie de mode non `None` se soumet en deux entrées de même clé de tri : texels
  opaques (fenêtre (0,75 ; 1], état opaque), puis texels STP (fenêtre (0,25 ; 0,75], état du mode : `Mode0` alpha non prémultiplié,
  `Mode1` additif, `Mode2` soustractif, `Mode3` additif avec la couleur (64, 64, 64) qui **remplace** la couleur du composant, le
  binaire n'ayant aucune teinte par entité) ; une partie `None` reste une seule entrée à la fenêtre neutre ; le dédoublement ne dépend
  que du mode PSX, jamais du `SpriteBlendMode`. **Capacité** : la file peut dépasser 10 000 entrées ; `_vertices` et le `VertexBuffer`
  (recréé à une capacité suffisante) grandissent pour la contenir et la borne de `SetData` suit la capacité réelle ; le tampon
  d'indices (six indices) ne change pas ; aucune exception, aucune entrée perdue. Le remplissage des sommets est séparé de l'envoi au
  GPU : une méthode interne de remplissage (tri compris), appelée par `UpdateBuffer`, testable sans périphérique.
- **G2a-R4 — Convertisseur** : `SpriteWriter` écrit le mode semi de chaque `.sprite` depuis **`SpriteQuad.Spritesheet`** (bit 3 →
  semi ; mode = `(Spritesheet >> 4) & 3`), jamais depuis la `Signature` (1836 sprites) ; les autres `.sprite` ne changent pas.
- **G2a-R5 — DLL** : rien (les entités passent par le moteur ; la DLL ne pose jamais `AnimatedSpriteComponent.Color`).

**Tâches.**
- ✅ **G2a-1 — Moteur, tests d'abord** (branche du sous-module **empilée sur** `chantier/e19m2-cellular-binary` (E19.m2) ; plan du moteur
  dans son `ai-agent/tasks/` ; ADR du moteur) : tests sur la liste des entrées en file (le dessin du périphérique ne tourne pas sans
  GPU ; avant le tri, ordre d'insertion ; après le tri, aucun ordre relatif entre les deux entrées d'une partie n'est affirmé) : une
  partie `None` = une entrée, fenêtre neutre, état opaque ; une partie de chaque mode = deux entrées de même clé, fenêtres et états
  ci-dessus, couleur (64, 64, 64) pour `Mode3` ; `SpriteData` lu et écrit avec et sans le champ (absent = `None`, `None` jamais écrit) ;
  10 001 entrées en file : la méthode interne de remplissage ne lève rien et remplit 40 004 sommets, ceux de la 10 001e aux positions
  de son entrée (aujourd'hui : `IndexOutOfRange`) ; les chemins `DrawStaticBatch` et `DrawDirectly` posent la fenêtre neutre (test
  par une couture interne si le périphérique manque, à nommer dans le plan du moteur). **Démo de capacité** (sur périphérique, même
  harnais de capture) : 12 000 sprites opaques de 1 × 1 en file, à clés de tri croissantes ; les 11 999 premiers loin du pixel témoin ;
  le dernier dans l'ordre de tri, un texel vert pur (0, 255, 0), seul au pixel témoin (loin des bords) sur le fond (100, 150, 200) ; la
  capture y lit (0, 255, 0) à ±1 (aujourd'hui : `IndexOutOfRange` ; un `VertexBuffer` resté à 40 000 sommets : le fond ou une erreur). **Démo** du moteur (une
  scène, un sprite de chaque mode, à `DepthSortable2DComponent`, sur un fond uni (100, 150, 200) ; texel opaque (60, 40, 20), texel STP
  (120, 80, 40) ; capture du back-buffer en processus, `GetBackBufferData`, pixels loin des bords) : opaque → (60, 40, 20) ; STP
  `Mode0` → (110, 115, 120) ; `Mode1` → (220, 230, 240) ; `Mode2` → (0, 70, 160) ; `Mode3` → (130, 170, 210) ; transparent → le fond ;
  à ±1 par canal (RVB seulement : la formule PSX sur les couleurs 8 bits, pas l'arithmétique 5 bits).
- ✅ **G2a-2 — Convertisseur, tests d'abord** : le mode lu sur `SpriteQuad.Spritesheet` (un sprite réel de chaque mode présent, 0, 1 et
  3 ; le mode 2 sur un montage synthétique ; un sprite non semi sans champ ; un montage dont l'octet bas de la `Signature` vaut
  `0x18` et `Spritesheet` 0 reste `None`), puis le pointeur du sous-module ; export complet en place, manifeste : exactement les 1836
  `.sprite` semi et `report.json` ; double export ; `Alundra.Tests` en Release puis en Debug sur l'export.
- **G2a-3 — Vérification**, **G2a-4 — Recette** (auteur, avec une capture du back-buffer) : (1) carte 476, la Rancune de Melzas : le
  grand halo bleu ajoute sa lumière à la pièce au lieu d'un bloc bleu sombre opaque, ses texels blancs restent pleins ; (2) le héros,
  sur n'importe quelle carte : le coup d'épée (animations 0x10 et 0x3F) laisse une traînée éclaircie, la poussière du sprint un léger
  éclaircissement, l'attente et la marche inchangées ; (3) carte 100, ◆Homme-ombre Niv.1 : des ombres assombries à 50 % où le sol se
  voit ; (4) carte 9, ◆Slime gélatineux : le corps bleu clair opaque, le bord bleu foncé mêlé au sol (sinon carte 7 Beannoïde, carte 4
  Poisson, carte 48 barrière) ; (5) cartes 41/42, Sara (femme du rêve), animations 3 à 5 : une lueur bleue additive (sinon carte 306,
  Médium ligoté). Aucun PNJ ni le héros n'a de partie moyennée (mode 0).

**Acceptation.**
1. Tests de G2a-1 et G2a-2 rouges d'abord, verts après ; les captures des deux démos (modes et capacité) égalent les valeurs
   ci-dessus à ±1.
2. Tests existants touchés : **aucun** (moteur, convertisseur, DLL) ; toute assertion existante qui bouge est un arrêt.
3. Export : exactement 1836 `.sprite` et `report.json` ; dans chaque `.sprite` changé, seul le nouveau champ apparaît ; double export
   identique hors `report.json`.
4. `CasaEngine.Tests` (construit à part) ; tests du convertisseur ; `Alundra.Tests` en Release puis en Debug, la Debug en dernier,
   `cmp` sans écart ; les six traces à l'octet.

**Retour arrière** : pointeur du sous-module et commit du convertisseur ; ré-export en place, manifeste égal au manifeste d'avant.

**Risques.** Les quads déformés restent dessinés en rectangle jusqu'à G2b ; l'alpha du back-buffer réécrit par le mode 0 (191 au lieu
de 255 : captures en processus translucides) ; un sprite partagé par des quads de modes différents est impossible (le mode est dans la
signature) ; la croissance des tampons alloue pendant le dessin (rare : plus de 10 000 entrées).

- **Fait le 2026-10-03.** **Moteur** (sous-module, branche `chantier/e19g2a-psx-semi` depuis `61358ac0`, plan
  `ai-agent/tasks/e19g2a-psx-semi-tasks.md`, la modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` jamais indexée) :
  `78e98c66` plan, `c2489471` (E1 : champ, fenêtre du shader, deux dessins disjoints), `67e79e5b` (E2 : capacité), `1c9b129c` (E3 : démos et
  `docs/engine/sprite-psx-semi-transparency.md`), `b5a9fbcf` (ADR-0051). Rouges d'abord, valeurs lues égales aux valeurs écrites
  d'avance : `SpriteData` écrit sans clé là où `"ModeN"` est attendu (4 cas) ; une entrée en file là où deux sont attendues (4 modes, et
  `AnimatedSpriteComponent` à `DepthSortable2DComponent`) ; aucune pose de fenêtre sur `Draw`, `DrawStaticBatch`, `DrawDirectly` ;
  `IndexOutOfRangeException` dans le remplissage des sommets à la 10 001e entrée (12 rouges sur 20 pour E1, 1 sur 2 pour E2). Verts
  après : `CasaEngine.Tests` 2533/2533 (2511 avant, +22), aucun test existant touché. Démos (Debug, 1024 × 768, sonde en processus
  `GetBackBufferData`, à ±1) : modes 15 contrôles sur 15, opaque (60, 40, 20), `Mode0` (110, 115, 120) avec alpha de back-buffer 191,
  `Mode1` (220, 230, 240), `Mode2` (0, 70, 160), `Mode3` (130, 170, 210), transparent (100, 150, 200), témoin sans mode (120, 80, 40) ;
  capacité : 12 001 entrées, pixel témoin (702, 469) lu (0, 255, 0) ; images `scratchpad/e19g2a-exec/demo-out/psx-modes.png` et
  `psx-capacity.png`, lectures `modes.txt` et `capacity.txt` ; la démo de capacité sur le code d'E1 (sans croissance) lève
  `IndexOutOfRangeException` à chaque image, sans capture. **Convertisseur** : tests 420/420 (418 + 2, rouges d'abord : `sprite_3001`
  attendu `Mode2` lu `None` sur le montage synthétique ; `sprite_16561416966684` attendu `Mode1` lu `None` sur données réelles),
  `SpriteWriter.ReadPsxSemiTransparency` lit `SpriteQuad.Spritesheet` (e8231cf). Le montage synthétique porte six quads dont `Signature`
  d'octet bas `0xB9`, `0xBA` et `0x18` avec `Spritesheet` 0 ou 0x28 : seuls ceux dont `Spritesheet` a le bit 3 reçoivent un mode ; le test
  réel convertit `map_alundra.json` (héros ABR 1, nuages de sable ABR 3) et `map_100.json` (◆Homme-ombre Niv.1, ABR 0), 2 s, et rend
  sans rien vérifier si `data-extracted/` manque. **Export** en place : manifeste SHA-1 (23 741 fichiers hors DLL, pdb, `.casaeditor/`)
  avant/après = exactement les 1837 lignes (les 1836 chemins de `predicted_sprites.txt` et `report.json`) ; pour chacun des 1836
  `.sprite`, ôter la ligne `"psx_semi_transparency"` rend l'empreinte SHA-1 du manifeste « avant », et la ligne porte le mode de sa
  ligne de la liste (218 `Mode0`, 1615 `Mode1`, 0 `Mode2`, 3 `Mode3`) ; second export identique hors `report.json` ; `Alundra.Tests`
  2555/2555 en Debug (pas de Release ici : la clôture la lance), `cmp` de la DLL déposée contre la build Debug sans écart ; six traces
  à l'octet (`git diff --ignore-cr-at-eol` vide), les quatre traces du héros remises par `git checkout`. Écarts au plan : (1) le fond de
  la démo de capacité est une entrée de plus en tête de file (12 000 sprites de texel plus le fond, 12 001 entrées) ; (2) le test de
  `DrawDirectly`, `DrawStaticBatch` et `Draw` observe la pose de la fenêtre par la couture `AlphaWindowWriter` puis laisse passer le
  `NullReferenceException` de la partie qui touche le périphérique (couture nommée au plan du moteur) ; (3) la `Release` de
  `Alundra.Tests` n'est pas lancée ici (brief). ADR-0033 (`Accepted`, D-E19-52) ; moteur : ADR-0051. Decisions: see ADR-0033 (moteur :
  ADR-0051). Reste : G2a-3 (vérification) et G2a-4 (recette de l'auteur).
- ✅ **Vérification de G2a** (G2a-3, 2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2555/2555
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet. Vérificateur neuf **CONFIRMED** : démos relancées sur un
  vrai périphérique (15 contrôles sur 15 ; capacité : 12 001 entrées, pixel témoin (0, 255, 0) ; captures identiques au pixel à
  celles de l'exécuteur) ; rouges rejoués sur des copies (13 des 22 nouveaux tests du moteur rouges sur le code d'avant, les 2 du
  convertisseur aussi) ; export recompté (1837 chemins, chaque `.sprite` égal à l'ancien une fois la ligne du champ ôtée, modes
  218/1615/3) ; `CasaEngine.Tests` 2533/2533, convertisseur 420/420. Avis P4 : le test réel du convertisseur passe sans rien
  vérifier si `data-extracted/` manque (documenté) ; la remise de la fenêtre après `TryReloadBuiltInShader` et la croissance du
  `VertexBuffer` n'ont pas de test sans périphérique (lecture du code et démo de capacité). Reste la recette G2a-4 (auteur).

##### 1.2o.3 E19.g G2c — Semi-transparence par texel des fonds (O-E19-54) 🧪 (relecture n°1 READY ; faite et CONFIRMED le 2026-10-05 ; recette G2c-6 en attente)

**Faits** **[binaire, données]** (découverte du 2026-10-03, `e19o54-disc/notes.md` du scratchpad ; deux vérifications indépendantes
du 2026-10-05, `e19o54-verify-binary/verify.md` (recensement refait sur `DATAS.BIN` brut, rien de réfuté) et
`e19o54-verify-code/verify.md` (cuisson refaite depuis le code du convertisseur, rien de réfuté, corrections reprises ci-dessous) ;
annexe des valeurs versionnée `docs/plan-e19-g2c-annexe/`) :
- **Binaire** : les tuiles des fonds (`SPRT_16`, `0x8005BB24`) et les cellules (types 0, 2, 4, `SPRT`, `0x8005BDD4`) sont posées une
  fois au chargement en texture brute, avec le bit semi = `BlendMode ≠ 0` (`SetSemiTrans`, `0x8005BC34`, `0x8005BC64`, `0x8005BFB4`,
  `0x8005C03C`) ; l'ABR de la couche vaut `BlendMode − 1`, par la primitive de mode posée en tête du créneau de la couche
  (`0x8005B958`-`0x8005B9E4`) ; `Ground` ne choisit que le créneau (`0x8005B8B0`) ; rien n'écrit ensuite l'octet de code d'une
  primitive. La table de couleurs envoyée en `0x8005B2A0` égale les `PaletteWords` extraits (bit 15 compris) sur les 192 cartes à
  fond. **[PSX]** une primitive semi ne mêle que ses texels STP (bit 15), les autres sont opaques ; une primitive non semi dessine
  tout texel non nul opaque.
- **Recensement** (224 couches) : 11 couches sont fausses aujourd'hui : la pluie (cellules de type 2, `BlendMode` 1) des cartes **391**
  et 31, dont les 720 texels ont le bit STP éteint, est **opaque** dans l'original et dessinée à 50 % par le portage ; la couche 1
  (tuiles, `Ground` faux, `BlendMode` 1) des cartes 41, **44**, 109 à 114 et 470 porte un mot STP (`0x8C83`, 518 pixels de toile en
  81 poses sur la 44) que l'original mêle et que le portage dessine opaque. Toutes les autres couches semi sont entièrement STP
  (leur mélange par couche est juste) et toutes les couches opaques sont justes ; aucun texel `0x8000`.
- **Portage** : le convertisseur écrit l'alpha 255 pour tout texel dessiné (`BackdropImageBuilder.DrawTile`, `:213` ; `stp` calculé
  `:199` ne sert qu'au test de transparence `:200`) ; les 245 PNG de fonds exportés égalent une cuisson indépendante ; la DLL choisit
  le mélange par `ResolveGroundLayerBlend` (`AlundraBackdropStage.cs:438-458` : `Ground` et 1 → `AlphaBlend` teinte α128, 2 →
  `Additive`, 3 → `Subtractive`, 4 → `Additive` teinte (63, 63, 63), sinon `Opaque`), appelé par `BuildDefinitions` (`:261`,
  `:287-288`) et `BuildCellularDefinitions` (`:353`, `:386-387`) ; la porte `Ground` vient d'E10 (`docs/plan-e10-fondu.md` §1.8,
  `:129-131` : « analyse manquante ; n'y pas toucher ») ; le moteur dessine chaque quad ou cellule par la surcharge publique
  `Texture2D` + ciseaux + `SpriteBlendMode` (`SpriteRendererComponent.cs:769-775`), une entrée, fenêtre neutre ; le chemin à deux
  entrées de G2a n'existe que pour un `Sprite` (`:618-652`) ; le cœur privé prend déjà `alphaMin`/`alphaMax` (`:797-859`).
- **Ordre de livraison** : les trois moitiés ne sont pas sûres séparément : de nouveaux PNG (alpha 128) avec la DLL d'aujourd'hui
  dessinent les 50 couches de mer à 25 % (texel de la 389 (85, 124, 160) au lieu de (70, 99, 120)) ; un nouveau moteur et une nouvelle
  DLL avec les PNG d'aujourd'hui dessinent opaques la mer de la 389, les vagues des 476/478/362/44 et toute couche additive ou
  soustractive. Aucun test existant ne voit ces états mixtes (les tests sur données réelles lisent les compagnons, inchangés).
- **Dépendance** : G2a est 🧪 (recette G2a-4 en attente) et n'est pas sur `main` du moteur ; la démo de G2a crée sa texture par
  `SetData` : le chargement d'un PNG à alpha 128 par `Texture2DLoader.cs:12` (`Texture2D.FromStream`) n'est pas prouvé (hypothèse :
  pas de prémultiplication, alpha gardé).

**Règles.**
- **G2c-R1 — Convertisseur (règle A)** : `BackdropImageBuilder.DrawTile` écrit l'alpha 128 pour un texel STP, 255 pour un texel non
  STP dessiné, 0 pour le mot `0x0000` (le code de G0) ; RVB inchangé ; dans toutes les planches de fonds, quel que soit le mélange de
  la couche (la planche = le contenu de la VRAM ; le bit semi et l'ABR restent dans le `BlendMode` du compagnon) ; doc de la classe
  (`:19-24`) et `docs/formats/backdrops.md` (décodage des couleurs) ; compteurs et invariants inchangés.
- **G2c-R2 — Moteur** : `ScrollingLayerDefinition` et `CellularLayerDefinition` gagnent un champ public `SpritePsxSemiTransparency
  PsxSemiTransparency` (défaut `None`) ; une nouvelle surcharge **interne** de `DrawSprite` (`Texture2D`, rectangle source, origine,
  position, rotation, échelle, couleur, z, clé de tri, effets, ciseaux, mode PSX) : `None` → une entrée identique à celle de la
  surcharge existante ; un mode → les deux entrées de G2a (texels opaques, fenêtre (0,75 ; 1], état opaque ; texels STP, fenêtre
  (0,25 ; 0,75], état du mode, couleur (64, 64, 64) pour `Mode3`), même clé et même z ; la surcharge `Sprite` n'est pas redirigée sur
  elle (elle garde son `drawDebug`) ; `ScrollingLayerComponent.Submit` (`:259-271`) et `CellularLayerComponent.Submit` (`:242-254`)
  l'appellent quand le champ n'est pas `None` (`Blend` alors ignoré), l'appel existant sinon ; la clé par cellule d'E19.m3 est
  gardée ; la surcouche de teinte n'est pas touchée ; docs : `CellularLayerDefinition.cs:10-12`, `docs/engine/scrolling-layers.md`,
  `cellular-layers.md`, `sprite-psx-semi-transparency.md` ; ADR du moteur (prochain numéro libre, 0053 aujourd'hui).
- **G2c-R3 — DLL** : un résolveur sur le seul `BlendMode` remplace `ResolveGroundLayerBlend` : 0 → `None`, 1 → `Mode0`, 2 → `Mode1`,
  3 → `Mode2`, 4 → `Mode3`, toute autre valeur → `None` (choix : le binaire lirait une table au-delà, aucun cas dans le corpus) ;
  teinte blanche pour toute couche ; `Blend = Opaque` quand un mode est posé (le mode seul décide) ; `Ground` ne choisit plus que la
  passe (`:262`) ; docs de `AlundraBackdropStage` (`:233-235`, `:318-319`, `:429-437`) ; la porte `Ground` d'E10 tombe : note dans
  `docs/plan-e10-fondu.md` §1.8 vers la nouvelle ADR du parent (prochain numéro libre, 0035 aujourd'hui) ; D-E19-68.
- **G2c-R4 — Livraison verrouillée et garde** : convertisseur, moteur et DLL arrivent ensemble ; l'export complet en place suit la
  construction de la DLL ; un test de la DLL sur l'export réel (convention locale : échoue si l'export manque,
  `BackdropStageDefinitionTests.cs:28-32`) lit l'alpha 128 au pixel (46, 23) de `layer0` de la 389 et au pixel (0, 0) de `cellsheet0`
  de la 476, et l'alpha 255 au pixel (2, 0) de `cellsheet0` de la 391 : un export périmé échoue haut et fort.

**Tâches.**
- **G2c-0 — Annexe des valeurs** (faite) : `docs/plan-e19-g2c-annexe/` (la liste exacte de l'export, 187 PNG et `report.json` ; le détail
  par PNG ; le recensement par couche).
- ✅ **G2c-1 — Moteur, tests d'abord** (sous-module, branche empilée sur `chantier/e19m3-cellular-order` `a885f226`, qui porte G2a ; plan
  du moteur dans son `ai-agent/tasks/` ; `CasaEngine.Launcher/Program.cs` de l'auteur jamais indexé) : au niveau de la file (comme
  G2a) : une couche de défilement d'un quad en `Mode0` → **2** entrées de même clé : [opaque, (0,75 ; 1], blanc] puis [`AlphaBlend`,
  (0,25 ; 0,75], blanc] (aujourd'hui 1 entrée, `definition.Blend`, (−1 ; 2]) ; `Mode1` → additif ; `Mode2` → soustractif ; `Mode3` →
  additif avec (64, 64, 64) ; une couche cellulaire de 3 cellules en `Mode0` → **6** entrées, les entrées 2c et 2c+1 avec
  `LocalSortOffset` −c (aujourd'hui 3) ; `None` inchangé (une entrée, `definition.Blend`). **Démo** du moteur (cas ajouté à la démo de
  G2a ou démo sœur, sonde du back-buffer, même harnais), avec une texture **chargée depuis un PNG** par le chargeur du moteur, sur un
  fond (100, 150, 200), pixels loin des bords, ±1 par canal : un texel (96, 96, 88, α255) d'une couche `Mode0` → (96, 96, 88) ; un
  texel (24, 32, 24, α128) d'une couche `Mode0` → (62, 91, 112) ; un texel (96, 96, 96, α128) d'une couche `Mode1` → (196, 246, 255) ;
  un texel (120, 80, 40, α128) d'une couche `None` → (120, 80, 40).
- ✅ **G2c-2 — Convertisseur, tests d'abord** : synthétiques : le mot `0x83E0` → (α128, 0, 248, 0) dans `Build` (toile) et dans
  `BuildTileSheet` (aujourd'hui α255) ; gardes : `0x03E0` → α255 ; `0x0000` → α0 ; `0x8000` → (α128, 0, 0, 0) (aujourd'hui
  (α255, 0, 0, 0)) ; données réelles (`data-extracted/`, sortie anticipée si absent, comme les tests voisins) : la 44, couche 1,
  pixel (79, 38), mot `0x8C83` → (24, 32, 24, α128) ; pixel (78, 34), `0x0020` → (0, 8, 0, α255) ; la 476, `cellsheet0`, (0, 0),
  `0xB18C` → (96, 96, 96, α128) ; la 389, `layer0`, (46, 23), `0x94C5` → (40, 48, 40, α128) ; la 391, `cellsheet0`, (2, 0), `0x2D8C` →
  (96, 96, 88, α255) (garde ; la 31 de même).
- ✅ **G2c-3 — DLL, tests d'abord** : résolveur : (`Ground` vrai, 1) → `Mode0`, blanc (aujourd'hui `AlphaBlend`, α128) ; (`Ground` faux,
  1) → `Mode0` (aujourd'hui `Opaque`) ; 2 → `Mode1` ; 3 → `Mode2` ; 4 → `Mode3` (aujourd'hui `Additive`, 63) ; 0 et 99 → `None` ;
  compagnons réels : la 391 cellulaire → `Mode0`, blanc (aujourd'hui `AlphaBlend`, α128) ; la 44 couche 1 → `Mode0` (aujourd'hui
  `Opaque`) ; la 44 couche 0 cellulaire → `Mode1` ; la 389 → `Mode0` ; la 321 → `None` ; le test garde de G2c-R4 (rouge sur l'export
  d'aujourd'hui, vert après l'export).
- ✅ **G2c-4 — Export et preuves** : manifeste SHA-1 avant et après (comme G0b) : exactement `export_predicted_changes.txt` (188
  lignes) ; pour chaque PNG changé, le décodage avec l'alpha 128 remis à 255 égale l'ancien, et ses pixels STP sont ceux de
  `export_predicted_pngs.tsv` ; double export identique hors `report.json`.
- **G2c-5 — Vérification**, **G2c-6 — Recette** (auteur, sur l'export de G2c, avec une capture du back-buffer) : la 391, la pluie
  blanche et opaque ; la 44, la couche du fond (traits vert sombre mêlés) ; la 389 et la 416, la mer inchangée ; les 476 et 478, les
  vagues inchangées.

**Acceptation.**
1. Tests de G2c-1 à G2c-3 rouges d'abord (valeurs d'aujourd'hui), verts après ; la démo égale les valeurs ci-dessus à ±1.
2. Tests existants touchés, liste fermée : `BackdropStageBlendTests.cs` (les trois tests) ; `BackdropStageDefinitionTests.cs:64-65` et
   `:103` ; `BackdropStageCellularDefinitionTests.cs:50`, `:123-124` et la théorie `:298-329` ; rien d'autre, ni côté moteur (les tests
   des couches ne posent jamais le nouveau champ ; `SpriteRendererComponentPsxSemiTransparencyTests.cs:69-81` reste à une entrée) ni
   côté convertisseur (`BackdropWriterTests.cs` : aucune palette STP) ; restent aussi inchangés `AlundraLibcRandomTests.cs:82`,
   `CellularLayerCellOrderTests.cs`, `ScrollingLayerMaskTests.cs`, `CellularLayerMaskTests.cs`, les tests de soumission des couches.
3. Export : exactement `export_predicted_changes.txt` ; double export ; le test garde vert sur cet export.
4. `CasaEngine.Tests` (construit à part) ; tests du convertisseur ; `Alundra.Tests` en Release puis en Debug sur l'export, la Debug en
   dernier, `cmp` sans écart ; les six traces à l'octet.

**Retour arrière** : pointeur du sous-module, revert des commits du parent, puis export complet en place dont le manifeste égale le
manifeste « avant » (hors `report.json`).

**Risques.** Un état mixte (une moitié sans l'autre) dessine faux : la livraison et la recette se font sur l'ensemble (recette en
worktree : copier le projet exporté) ; G2a-4 non recettée (le chemin PNG est prouvé par la démo de G2c) ; ce qui se trouve sous la
couche 1 de la 44 (le portage efface en noir ; la couleur d'effacement du binaire n'est pas relue) ; l'octet de palette 30 (cartes 1,
13, 17, 153, 439) reste la palette 0 (O-E19-59) ; la surcouche additive de la 293 hors tranche (O-E19-58).

- **Fait le 2026-10-05** (G2c-1 à G2c-4, exécuteur en mode AUTO). **Moteur** (sous-module, branche `chantier/e19g2c-backdrop-stp` depuis
  `a885f226`, plan `ai-agent/tasks/e19g2c-backdrop-stp-tasks.md`, la modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs`
  jamais indexée) : `6553d3db` plan, `c33c12fd` (E1 : champ `PsxSemiTransparency` des deux définitions, surcharge interne de `DrawSprite`,
  `Submit` des deux composants), `61543a13` (E2 : démo), `3b05301f` (E3 : docs et ADR-0053). Rouges d'abord sur le code d'avant (champ,
  surcharge qui ignore le mode, `Submit` qui l'appelle), valeurs lues égales aux valeurs écrites d'avance : couche de défilement en
  `Mode0` à `Mode3` et `Mode0` à `Blend = Additive` : 1 entrée là où 2 sont attendues ; couche cellulaire de 3 cellules, mêmes cas : 3
  entrées là où 6 sont attendues (10 rouges sur 12 ; les cas `None`, une entrée au `Blend` de la couche, étaient verts d'avance). Verts
  après : `CasaEngine.Tests` 2562/2562 (+12), aucun test existant touché. Démo `Background layers PSX semi-transparency` (quatre couches
  de défilement et une couche cellulaire `Mode0` à `Blend = Additive`, feuilles PNG générées dans `CasaEngine.Demos/Content/PsxBackdropLayers/`
  et chargées par `Texture2DLoader` : l'alpha 128 d'un PNG est gardé, hypothèse prouvée ; sonde `BackBufferProbe`, `GetBackBufferData`,
  Debug 1024 × 768, à ±1) : 6 contrôles sur 6, texel (96, 96, 88, α255) d'une couche `Mode0` lu (96, 96, 88) ; texel (24, 32, 24, α128)
  `Mode0` lu (62, 91, 112) (alpha de back-buffer 191) ; texel (96, 96, 96, α128) `Mode1` lu (196, 246, 255) ; texel (120, 80, 40, α128)
  sans mode lu (120, 80, 40) ; la cellule `Mode0` lue (62, 91, 112) ; fond lu (100, 150, 200) ; image
  `scratchpad/e19g2c-exec/demo-out/backdrop-layers.png`, lectures `backdrop-layers.txt`. Rouge de la démo, la soumission des couches
  rendue aveugle au mode : `Mode0` STP lu (24, 32, 24), `Mode1` STP lu (96, 96, 96), cellule lue (124, 182, 224) (le `Blend` additif
  de la couche, ignoré quand un mode est posé), 3 contrôles sur 6 rouges (`backdrop-layers-red.txt`). ADR-0053 du moteur.
  **Convertisseur** : `BackdropImageBuilder.DrawTile` écrit l'alpha 128 pour un texel STP, 255 pour un autre texel dessiné, 0 pour le mot
  `0x0000` ; tests 432/432 (420 + 12 dans `BackdropStpAlphaTests`, rouges d'abord : 8 rouges sur 12, l'alpha 255 lu là où 128 est attendu
  sur `0x83E0` et `0x8000` dans `Build` et `BuildTileSheet`, sur le mot `0x8C83` de la 44 (79, 38), le mot `0x94C5` de la 389 (46, 23),
  le mot `0xB18C` de la 476 ; les gardes `0x03E0`, la 391 et la 31 (word `0x2D8C`, alpha 255) étaient verts d'avance ; les RVB lus égalent
  ceux écrits d'avance, et le pixel (78, 34) de la 44 `0x0020` lu (0, 8, 0, α255)). **DLL** : `AlundraBackdropStage.ResolveLayerPsxSemiTransparency(int
  blendMode)` remplace `ResolveGroundLayerBlend` (0 → `None`, 1 → `Mode0`, 2 → `Mode1`, 3 → `Mode2`, 4 → `Mode3`, autre → `None`), les
  définitions portent ce mode, un `Blend` opaque et une teinte blanche, `Ground` ne choisit que la passe ; rouges d'abord (15 rouges sur 37
  des tests `BackdropStage*`, le résolveur d'abord sans comportement) : mode lu `None` là où `Mode0` à `Mode3` sont attendus (résolveur,
  389, 159, 391, 420, 44, définitions synthétiques), et, sur la théorie cellulaire `(true, 1..4)`, le blend d'aujourd'hui lu `AlphaBlend`,
  `Additive`, `Subtractive`, `Additive` là où `Opaque` est attendu (les anciennes assertions de la 389, 159, 391 et 420 portaient déjà
  `AlphaBlend` α128, `Additive`, `AlphaBlend` α128, `Additive`) ; les gardes (`None` de la 321, résolveur à 0, 99 et −1) étaient verts
  d'avance ; le test garde sur l'export d'avant lit l'alpha
  255 au pixel (46, 23) de `layer0` de la 389 là où 128 est attendu ; verts après : `Alundra.Tests` 2566/2566 (Debug, sur le nouvel
  export). **Export** en place (`dotnet run -c Release`, 1 min, 23 744 fichiers, vérification PASSED) : manifeste SHA-1 (23 741 fichiers
  hors DLL, pdb, `.casaeditor/`) avant/après = exactement les 188 lignes de `export_predicted_changes.txt` (187 PNG et `report.json`) ;
  pour chacun des 187 PNG, le décodage avec l'alpha 128 remis à 255 égale celui de l'ancien PNG (RVB et alpha), les alphas sont
  seulement 0, 128 et 255, son nombre de pixels d'alpha 128 égale `stp_px` et son nombre de pixels dessinés `drawn_px` de
  `export_predicted_pngs.tsv` (7 092 983 pixels STP en tout) ; second export identique hors `report.json` ; `Alundra.dll` déposée
  inchangée par l'export (construite avant, `cmp` sans écart avec la build Debug) ; six traces à l'octet (`git diff
  --ignore-cr-at-eol` vide), les quatre traces du héros remises par `git checkout`. Écarts au plan : (1) le résolveur ne prend pas
  `Ground` (le plan l'écrit « sur le seul `BlendMode` ») : les cas `(Ground vrai, 1)` et `(Ground faux, 1)` sont vérifiés par
  `BuildDefinitions` sur un document synthétique (`Ground` vrai et faux : `Mode0`, blanc, opaque, passe selon `Ground`) ; (2) les
  compagnons réels de la 389, 391, 44 et 321 sont lus dans un nouveau fichier `BackdropStagePsxSemiTransparencyTests.cs` (le test de la 321
  de `BackdropStageDefinitionTests` reste intact : sa liste fermée n'en parle pas), et le test garde y décode le PNG à la main (le projet
  de tests n'a pas de bibliothèque d'images) ; (3) les tests du convertisseur sont dans un nouveau fichier (`BackdropWriterTests.cs`
  intact) ; (4) la démo ajoute une couche cellulaire aux quatre valeurs du plan (même surcharge, autre composant) ; (5) la `Release` de
  `Alundra.Tests` n'est pas lancée ici (le vérificateur la lance). Decisions: see ADR-0035 (moteur : ADR-0053). Reste : G2c-5
  (vérification) et G2c-6 (recette de l'auteur sur l'export de G2c).
- ✅ **Vérification de G2c** (G2c-5, 2026-10-05) : vérificateur neuf **CONFIRMED** : `Alundra.Tests` 2566/2566 en Release puis en
  Debug (la Debug en dernier), `cmp` sans écart, les six traces à l'octet ; `CasaEngine.Tests` 2562/2562 (deux fois), convertisseur
  432/432 ; manifeste recalculé : exactement les 188 lignes de l'annexe, chaque PNG égal à l'ancien une fois l'alpha 128 remis à 255,
  7 092 983 pixels STP comme au tsv, aucun des 58 PNG inchangés n'a d'alpha 128, double export ; démo relancée sur le périphérique
  (6 contrôles sur 6, texture chargée d'un PNG) ; binaire relu (`0x8005BBF4`, `0x8005BC34`/`0x8005BC64`, `0x8005B958`-`0x8005B9E4`).
  Avis P4 : 14 rouges côté DLL au lieu de 15 (le test garde ne peut rougir que sur l'ancien export, montré à part) ; la branche `None`
  de la surcharge interne rend une entrée opaque (aucun appelant aujourd'hui ne lui passe `None`). Reste la recette G2c-6 (auteur).

### 1.2p E19.r — Recette de l'auteur du 2026-10-03 ✅ (R1 à R4 ; recette R5 en attente)

**Constat de l'auteur** (conversion relancée, DLL reconstruite) : contacts avec les PNJ bons ; sons et musique bons ; boîtes de
dialogue toujours celles de MGUI ; scène de Lars et Melzas toujours fausse, halo plus petit que la fenêtre ; on traverse les
boîtes ; le HUD n'apparaît pas à la sortie de la chambre (seulement après l'écran de sauvegarde ou l'inventaire) ; portes des
maisons d'Inoa fermées ; devant le manoir de Tarn, après la scène des Murggs, le contrôle ne revient pas.

**Enquête** (2026-10-03, lecture seule, un enquêteur et un contre-vérificateur par point, notes dans le scratchpad de la
session, `recipe-bugs/<point>/notes.md` et `recipe-bugs/<point>-verify/`) :
- **Boîte de dialogue** : E19.f (en pause : Q-F1 à Q-F3) ; une ré-extraction n'y change rien.
- **Portes d'Inoa** : ce sont des effets de carte qui apparaissent au chargement (découverte d'E19.g) : E19.g (en pause : Q-G1 à
  Q-G5).
- **HUD** **[binaire]** (confirmé) : le bloc d'entrée de carte de la boucle principale (`0x8002c2b8`) appelle
  `InitializeHudPositionBeforeHide` (`0x8004be0c`, en `0x8002c3d0`) ; si le verrou G1662 (posé par 163 `B[1] @208`) est posé et
  que `g_drawFrameFlags` vaut 0, il pose 5 et le HUD glisse à l'écran. Toute sortie de la chambre de la 163 est un warp complet
  (portails d'effet 0). Dans la DLL, `AlundraHudDirector.InstallForMapEntry` est vide (sa doc s'appuie sur une preuve fausse :
  `GameEngine.cs:215` et `:1090` appellent bien cette fonction) ; seuls la fermeture de l'inventaire et l'écran de sauvegarde
  l'appellent, d'où le constat de l'auteur. **Défaut du port**, correction de la DLL seule (R1).
- **Scène de la 476** (confirmé) : (a) le cadre elliptique gris vers noir est la couche cellulaire 0 de la carte (bandes à
  vagues, mélange additif ABR1, texels STP), allumée au chargement et fixée à l'écran : **fidèle**, l'original l'a aussi (il
  couvre tout l'écran 320 × 240, la fenêtre de 1280 × 944 comprise) ; (b) la carte « en partie noire » : les scripts de la 476
  copient par `0x85` la chambre de Lars et la salle du trône (sols de hauteur 3) dans la zone vide de la vision ; le binaire
  copie les cases entières et redessine les sols depuis le tableau vivant chaque image (`0x8002CDA0`, `0x8002D0F8`-`0x8002D174`) ;
  `AlundraCellVisualSync.ProcessCellFloor` refuse d'adopter un sol surélevé dans une case sans sol à plat et journalise 18 960
  avertissements : **défaut du port** (le cas dégradé de D-E7-3 est plus large que sa propre formulation), correction de la DLL
  seule (R2) ; (c) l'aura (`0xA2`/`0x92`/`0x93`/`0x91`) : E19.g ; (d) Melzas paraît plein et bleu : 1856 de ses 1864 quads sont
  semi-transparents additifs et déformés : E19.g, question Q-G2 ; (e) la fenêtre : la DLL montre 320 × 236 (décompilation), le
  binaire fixe 320 × 240 (`0x800424AC`) : 4 lignes de moins, sans rapport avec le halo (O-E19-46).
- **Manoir de Tarn** **[binaire]** (confirmé) : carte 15 (la cour devant le manoir, portail 0 vers la 115). L'événement 0, `B[1]`,
  boucle sans fin en `@108`-`@111` (`0x00`, `0x2C [2]`, `0x04` vers 108) : il attend que le Murgg d'enregistrement 2 ait disparu.
  Le Murgg se détruit à la fin de son script (`C[8] 0x2E [0x80] @890`), mais la DLL ne recycle jamais les créneaux détruits : le
  mandataire reste dans la liste avec `EntityRefId` 2 et la recherche le trouve toujours ; le binaire recycle chaque tick
  (`UpdateDestroyedEntities` `0x80038634`, premier appel d'`UpdateEntities`, sous la porte `0x48` ; gabarit à `EntityRefId` −1)
  et rend `Result` 1 deux images plus tard. La scène n'a besoin ni du combat ni de l'IA native : **défaut du port** (limite
  « invisibilité, pas retrait » consignée comme P3 hors chaîne, son effet sur les attentes de scripts ne l'était pas). Même
  blocage ailleurs sans combat (exemple : carte 6, `B[2] @239`, attente de Jess). Non établi : le script `C[8]` du Murgg 2
  pourrait aussi caler avant `@890`. Correction : R3 (décision et ADR, plan à écrire).
- **Boîtes** (confirmé) : ce sont les caisses et cruches soulevables (`Caisse en bois générique`, `Cruche générique`,
  `Cruche n°2`, drapeaux `0x3323E4`, type de levage 1) ; la sonde d'obstacles les ignore exprès : **écart consigné D-E19-28**
  (ADR-0021, O-E19-23) jusqu'à E14 (porter, lancer). Le binaire les traite comme obstacles tant qu'elles ne sont pas portées ;
  les rendre pleines sans « porter » fermerait la 392 (la barricade de caisses ne laisse qu'une fenêtre de 3 px en Y sur la caisse
  5). Décision de l'auteur (R4).

**Tâches.**
- ✅ **R1 — HUD à l'entrée de carte** : `AlundraHudDirector.InstallForMapEntry` appelle `InitializeHudPositionBeforeHide()` (port
  de l'appel en `0x8002c3d0` ; ses deux portes du binaire y sont déjà : le verrou G1662 et `Phase == Idle`) ; docs fausses
  corrigées (`AlundraHudDirector.cs` ~22-26, ~200-217, ~235-242, ~449-451 ; doc d'`InstallHudSystems` dans `AlundraWorldProxy.cs`
  ~1066-1072 ; `docs/intro-roadmap.md` ligne 9 du tableau ~333 ; `docs/test-saves.md` ; ce plan ~2602 ;
  `docs/plan-e16-etat-partie.md` ~1148 ; `docs/plan-e13d-inventaire.md` ~231-235). Tests d'abord : verrou posé, `Idle`,
  `AttachToWorld` puis `InstallForMapEntry` → `Opening` et la table d'ouverture jusqu'à `Displayed` ; verrou clair → `Idle` ;
  au niveau du mandataire, G1662 posé sur l'état de session puis `InitializeWithWorld` sur une carte réelle → `Opening`, sans le
  verrou → `Idle` ; nouvelle partie sur la 389 → `Idle` ; chargement d'une sauvegarde portant G1662 → `Opening` ; le test existant
  `AttachToWorld_RePointsWithoutResetting_InstallForMapEntry_DoesNotResetEither` reste vert. Écart connu : un portail d'effet 3
  vers la même carte (28 dans les données, hors chaîne) recharge le monde dans la DLL (D-T-7) et armera aussi le HUD.
  Commit : `fix(alundra): arm the HUD at map entry like the binary`
  - Fait le 2026-10-03 : rouges d'abord sur le code d'avant, `Phase` lue `Idle` là où `Opening` est attendu (directeur seul, mandataire
    sur la 389 avec G1662 posé en session, chargement d'une sauvegarde portant G1662) ; les cas verrou clair et nouvelle partie
    sur la 389 étaient déjà `Idle` (gardes, verts avant et après). Verts après : `InstallForMapEntry` appelle
    `InitializeHudPositionBeforeHide()`, 6 tests ajoutés (2 dans `AlundraHudDirectorTests`, 2 dans `AlundraNewGameEntryTests`, 2 cas
    d'une théorie dans `AlundraSaveGameApplyTests`) ; le test existant `AttachToWorld_RePointsWithoutResetting_...` reste vert.
    Aucun écart au plan.
- ✅ **R2 — Sols copiés par `0x85`** : `AlundraCellVisualSync` note à la création les cases **sans sol à plat à elles** (aucun
  sol au chargement, ou un sol de chargement qui était une pose de la surcouche) ; dans `ProcessCellFloor`, une case mutée sans
  entrée de sol dans le modèle et sans sol à plat à elle adopte son nouveau sol dans la surcouche triée, quelle que soit sa
  hauteur, placé en (x, y − hauteur) avec `WallPlacementOverlay.ComputeFloorSortKey(y, ComputeDepthSlot(raw), newStableId)`
  comme les entrées re-dérivées ; le cas dégradé ne reste que pour une case dont le sol vit dans une couche à plat (D-E7-3), avec
  un seul avertissement par monde. Tests d'abord (`AlundraCellVisualSyncTests`) : copier un bloc de sols de hauteur 3 dans une zone
  vide ajoute des entrées de sol en (x, y − 3) aux bons identifiants locaux ; recopier le bloc vide les retire ; un sol de hauteur
  0 copié dans une case vide est adopté ; une case à sol à plat reste dégradée, un seul avertissement ; les tests de la 389
  inchangés. Commit : `fix(alundra): draw floors that 0x85 copies into empty cells like the binary`
  - Fait le 2026-10-03 : 3 tests ajoutés dans `AlundraCellVisualSyncTests` (carte synthétique 2 × 8 ; sols de hauteur 3 et 0 copiés dans des
    cases vides, recopie du vide, cases à sol à plat). Rouges d'abord sur le code d'avant : 0 entrée de surcouche là où une est attendue (hauteur 3
    en (1,2), hauteur 0 en (1,6)), et 3 avertissements là où un seul est attendu. Verts après : `AlundraCellVisualSync` note à la création les cases
    à sol à plat propre (id de sol présent et absent de la surcouche) ; hors de celles-ci un sol copié est adopté quelle que soit sa hauteur ; le cas
    dégradé ne reste que pour ces cases, un seul avertissement par monde. Tests de la 389 inchangés et verts. Aucun écart au plan.
- ✅ **Vérification de R1 et R2** (2026-10-03) : `Alundra.Tests` 2474 → 2483 (+9), 0 échec, en Release (47 s) puis en Debug
  (48 s), la Debug en dernier ; `cmp` sans écart (sha256 `70a86bd7...c483`) ; les six traces à l'octet. Vérificateurs neufs :
  R1 **CONFIRMED** (chaîne d'appels du binaire relue : `0x8002c3d0` → `0x8004be0c`, ses deux portes, les seuls écrivains de
  `g_drawFrameFlags` ; quatre mutations attrapées : appel vidé, porte du verrou, porte `Idle`, armement avant le chargement) ;
  R2 **CONFIRMED** (copie et rendu du binaire relus ; sur l'export réel de la 476, 400 sols sur 400 de la chambre de Lars et
  360 sur 360 de la salle du trône adoptés en (x, y − 3), retirés par le bloc vide, sans avertissement ; le code d'avant
  reproduit la panne et ses avertissements ; cinq mutations sur six attrapées). Avis reportés à E19.m : P3 aucun test ne
  couvre la moitié « pose de surcouche au chargement » de la règle de R2 (le code est juste : 20 cases sur 20 adoptées sur la
  476) ; P4 aucun test ne prend un vrai warp hors de la chambre de la 163 (couvert en deux morceaux ; recette R5).
- ✅ **R3 — Recyclage des entités détruites** (D-E19-45, ADR-0024 ; décision de l'auteur du 2026-10-03 : « normalement le moteur
  recycle les entités à détruire, fais-le » ; les trois Murggs ont bien disparu à la fin de la scène : seul le recyclage manque).
  **Faits** **[binaire]** : `UpdateDestroyedEntities` (`0x80038634`) est le premier appel d'`UpdateEntities` (`0x8003B3A0`),
  sous la porte `g_playerControlFlags & 0x48` ; la boucle principale appelle `RunMapEvents` (`0x8002E100`) puis
  `UpdateEntities` (`0x8002E108`) ; chaque créneau d'état 4 (`FlagToDestroy`) est recouvert par le gabarit (`0x80134368`, état 0,
  `+0x48` = −1, écrit en `0x8003B21C`) ; la recherche par id brut (`0x8003C954`) ne teste pas l'état du candidat : un cadavre
  reste trouvable jusqu'au recyclage. **Moteur** : `World.RemoveEntity` appelle `Entity.Destroy()` (`ToBeRemoved`), et la boucle
  de `World.Update` retire l'entité et rend ce que tiennent ses composants (ADR-0037 du moteur).
  **Relecture** (2026-10-03) : REVISE n°1 et audit des valeurs : la liste fermée des tests touchés était incomplète (A0, A0b,
  A1 et A10J observent un cadavre ; l'épingle des blocs est dans A17, pas A18 ; A8 ne change pas) et la porte `0x48` est relue
  après les événements de carte (`0x8003B38C`) : corrigé ci-dessous. Critère de l'audit : tout `EntityByRecord`, toute lecture
  d'`Entities` et toute référence capturée dans `Alundra.Tests/*Arc*.cs` dont l'enregistrement est détruit (`0x2E`, `0x19` suivi
  du gestionnaire natif E, programmes de chargement) avant la lecture ; fichiers examinés : tous les arcs, les tests de
  sauvegarde, les tests unitaires sur hôte de test et le harnais d'intro (qui ne passent pas par `AlundraWorldProxy.Update`) ;
  sans changement : T-A19, T-A10v, A3, A6, A1c, A15, TH3b, TH4, T-C61, les gardes des `Dispose`. L'ordre du moteur est établi :
  `World.Update` met à jour les entités, retire celles marquées `ToBeRemoved`, puis appelle le mandataire du monde ; `ArcRun.OneFrame`
  fait de même : une destruction faite dans la mise à jour d'une entité précède toujours les événements de carte et le recyclage
  de la même image.
  **Règles.**
  - **R3-R1 — Point de recyclage** : dans `AlundraWorldProxy.Update`, juste après **chaque** appel de `RunMapEventsPass` de la
    boucle des ticks, une passe `RecycleDestroyedEntities`, gardée par `PlayerControlFlags & 0x48` **relu après la passe des
    événements** (le binaire relit les drapeaux au début d'`UpdateEntities`, `0x8003B38C` : un programme de carte qui détruit
    une entité et ouvre une boîte au même tick retarde le recyclage jusqu'à la fermeture) ; ordre par image de la DLL :
    entités, événements de carte, recyclage, comme le binaire par tick (événements, recyclage, entités) ; un cadavre laissé par
    un script d'entité est vu par exactement une passe d'événements, un cadavre laissé par un programme de carte disparaît dans
    la même image.
  - **R3-R2 — Recyclage** : chaque mandataire d'état `FlagToDestroy` de `_spawnedEntities` (jamais le héros) est remis au
    gabarit du binaire (`Status` = `Destroyed`, `EntityRefId` = −1), retiré de `_spawnedEntities` (et donc des listes de mise à
    jour et des collidables reconstruites ensuite), et son entité est rendue au moteur par `World.RemoveEntity` ; une carte
    sans monde (montages de test) : retrait des listes seulement.
  - **R3-R3 — Références** : rien d'autre n'est effacé : comme dans le binaire, une référence gardée sur un créneau recyclé voit
    un créneau à l'état 0 (les lecteurs testent déjà « Loaded, Normal ou Deactivated » : cible de la caméra, entité logique d'un
    événement, recherches) ; `RidingEntity`/`PlatformEntity` sont recalculés à chaque tick depuis les collidables.
  - **R3-R4 — Docs** : la portée « invisibilité, pas retrait » disparaît des docs (`AlundraWorldProxy.DestroyEntity`,
    `AlundraFrameSyncPasses.cs` ~96-107, `AlundraEntitySpawnFactory.cs` ~236) et des limites du plan (~1031-1032, ~1203).
  **Tâches et tests** (tests d'abord) :
  - **R3-1 — Recyclage, tests unitaires** : un PNJ d'`EntityRefId` 2 passé en `FlagToDestroy` : `0x2C [2]` rend `Result` 0 avant
    la passe, 1 après ; après la passe, son `Status` est `Destroyed`, son `EntityRefId` −1, il n'est plus dans
    `SpawnedEntities` et son entité est marquée `ToBeRemoved` ; avec `PlayerControlFlags & 0x48` non nul, rien n'est recyclé ;
    sur une image à deux ticks, un cadavre posé par le premier passage des événements est recyclé avant le second. Rouges
    d'aujourd'hui : `Result` 0 après la passe (aucune passe), `Status` `FlagToDestroy`.
  - **R3-2 — Arc de la carte 15** (nouveau fichier, vrais préfabs) : héros en case (26,21), z 7, G1650 clair ; la scène des
    Murggs va au bout : `C[6] 0x2E @712`, `C[7] @777`, `C[8] @890` exécutés, puis `B[1]` `0x05` G1650 `@114`, `0x2E [24] @117`,
    `0x11 @119`, `PlayerControlFlags & 0x04` à 0 à la fin, dans une limite de 1500 images. Rouge d'aujourd'hui : `B[1]` tourne
    sans fin en `@108`-`@111` (l'arc s'arrête à la limite sans `0x11 @119`).
  - **R3-3 — Épingles qui observent un cadavre** (ré-épinglées, cause établie : le recyclage du binaire ; valeurs re-dérivées par
    l'audit) :
    - **A11** (`AlundraInoaDayOneArcTests.cs` ~406-408) : le mandataire du record 4, capturé à l'image de `0x19 @451` (189,
      `Deactivated`), est `Destroyed` après une image (190 : détruit par le gestionnaire natif E dans sa mise à jour, recyclé
      à la fin de la même image) et `EntityByRecord(4)` est nul ;
    - **A17** (`AlundraDay3SceneArcTests.cs` ~276-280, l'épingle des blocs) : les blocs d'`EntityRefId` 8 à 10 n'existent qu'après
      `B[2] 0x2D @219`-`@223` ; ils sont capturés à la trace de `0x2E @272` (tous trois encore listés, `FlagToDestroy`) ; détruits
      par le programme de carte `B[2]` (`0x2E` `@268`, `@270`, `@272`), ils sont recyclés au même tick : à la fin de l'arc, chacun
      est `Destroyed` et aucun ne reste dans `Entities` ;
    - **A0** et **A0b** (`AlundraShipArcTests.cs` ~72, ~110) : le capitaine (record 2), détruit par `B[1] 0x2E [2] @545`
      (programme de carte), est recyclé dans l'image où l'arc s'arrête : `captain.Status` est `Destroyed` ;
    - **A1** (`AlundraShipArcTests.cs` ~130-134) : avec G870 posé, le record 2 se détruit dans son programme de chargement
      (`A[2] @433`) pendant la passe des entités de l'image 0 et il est recyclé dans l'image 0 (la boîte de `B[3]` n'ouvre qu'à
      l'image 4 ou plus tard) : le mandataire est capturé avant la première image, il est `Destroyed` après elle, et
      `EntityByRecord(2)` est nul ;
    - **A10J** (`AlundraHeroJumpArcTests.cs` ~43-50, ~144, ~166-167) : Giles (record 98) passe `Deactivated` par `0x19 @6410` à
      F0+266, est détruit par le gestionnaire natif E à F0+267 et recyclé dans la même image : l'épingle « marqué pour destruction
      à F0+267 » devient « le mandataire de Giles est `Destroyed` et `EntityByRecord(98)` nul à partir de F0+267 » (l'échantillon
      ne voit plus jamais `FlagToDestroy`) ;
    - **A8** (`AlundraInoaAwakeningArcTests.cs`) : **inchangé** : l'échantillon de Jess est pris par la trace juste après
      `0x2E [0] @185`, dans la passe des événements, avant le recyclage : il lit encore `FlagToDestroy`.
  - **R3-4 — Décision et ADR** : D-E19-45 au plan, ADR-0024 (skill `adr`), ligne au tableau, O-E19-47 réglé.
  Commit : `fix(alundra): recycle destroyed entities through the engine like the binary` (le code, les tests, l'ADR et le plan
  ensemble, pour que le commit reste vert).
  **Acceptation.** 1. Tests de R3-1 et R3-2 rouges d'abord, verts après ; l'exécution rouge de R3-2 sur le code d'aujourd'hui
  doit montrer `C[8] 0x2E @890` exécuté (sinon le script du Murgg 2 cale aussi : arrêt et diagnostic). 2. Code de test existant
  touché, liste fermée : A11, A17, A0, A0b, A1, A10J (leurs seules épingles de cadavre) et `AlundraArcSupport.cs` s'il faut y
  exposer une capture ; rien d'autre ; les six traces à l'octet (le harnais d'intro ne passe pas par
  `AlundraWorldProxy.Update`) ; aucune erreur nouvelle au journal (`AssertNoUnexpectedError`) quand le moteur détache les
  composants d'une entité recyclée. 3. `Alundra.Tests` sans échec en Release
  puis en Debug, `cmp` sans écart. 4. Recette (auteur) : la carte 15, le contrôle revient après les Murggs.
  **Risques.** D'autres attentes hors combat se débloquent (exemple : carte 6, `B[2] @239`, l'attente de Jess) : c'est le
  comportement du binaire ; le détachement des composants par le moteur pourrait journaliser une erreur (arrêt et diagnostic) ;
  le binaire rend un créneau libéré à la prochaine apparition, la DLL ajoute les nouvelles entités en fin de liste (écart
  existant, non traité par R3).
  - Fait le 2026-10-03 : rouges d'abord sur le code d'avant : 5 tests unitaires sur 7 (`AlundraDestroyedEntityRecyclingTests`) rouges, `Status` lu
    `FlagToDestroy` là où `Destroyed` est attendu, et, sur l'image à deux ticks, `0x2C [2]` lu `[0, 0]` là où `[0, 1]` est attendu (les deux gardes,
    héros jamais recyclé et entité vivante intacte, sont vertes avant et après) ; arc de la 15 (`AlundraTarnMurggArcTests`) rouge : `C[8] 0x2E @890`
    exécuté (image ~304, programme @784 terminé en `@892`), `B[1]` tourne en `@108` jusqu'à la limite de 1500 images, sans `0x11 @119`. Verts après :
    `RecycleDestroyedEntities` appelée après chaque passe des événements de carte, 8 tests ajoutés (7 unitaires, 1 arc) ; épingles ré-épinglées
    aux valeurs prévues du plan, sans écart : A0, A0b (`Destroyed`), A1 (capture avant la première image, `Destroyed`, `EntityByRecord(2)` nul), A11,
    A17 (blocs capturés à la trace de `0x2E @272`), A10J (recyclage lu à F0+267) ; A8 inchangé. `Alundra.Tests` 2483 → 2491, 0 échec, en Debug
    seulement (la consigne d'exécution interdisait Release : acceptation 3 à compléter) ; les six traces à l'octet ; aucune erreur nouvelle au
    journal (`AssertNoUnexpectedError`). Docs « invisibilité, pas retrait » mises à jour (`AlundraWorldProxy.DestroyEntity`, `AlundraFrameSyncPasses.cs`,
    `AlundraEntitySpawnFactory.cs`, limites du plan). Aucun écart au plan, hormis le périmètre de la limite des `0x8A` (la réutilisation des créneaux
    libérés reste absente).
- ✅ **Vérification de R3** (2026-10-03) : relecture READY à la deuxième passe, exécution `dbcd680` ; `Alundra.Tests` 2483 → 2491
  (+8), 0 échec, en Release (48 s) puis en Debug (48 s), la Debug en dernier ; `cmp` sans écart (sha256 `c7a3f0dc...cb45`) ; les six
  traces à l'octet. Vérificateur neuf : **CONFIRMED** (chaîne du binaire relue : `0x8002E100` puis `0x8002E108`, la porte relue en
  `0x8003B38C`, `0x80038634` sur 64 créneaux, gabarit ; six mutations de production, toutes attrapées ; rouge d'avant reproduit :
  `C[8] 0x2E @890` exécuté vers l'image 305, `B[1]` bloqué en `@108` jusqu'à la limite ; aucune référence périmée ni erreur au
  journal sur 300 images de plus et 120 images de marche avec Carré ; seule la liste fermée des tests a changé). Écart signalé
  par l'exécuteur : la passe vit dans le bloc `PlayerEntity != null`, comme les événements de carte (un monde sans héros ne
  recycle rien). Avis reportés à E19.m : P4 les tests de recyclage ne remettent pas l'état de session à zéro entre eux ; P4 le
  verrou d'interaction du héros pourrait viser une entité recyclée (non reproduit, chemin déjà présent avant R3).
- ✅ **R4 — Boîtes** : décision de l'auteur du 2026-10-03 : **l'écart D-E19-28 est gardé** jusqu'à E14 (D-E19-46).
- 🧪 **R5 — Recette** (auteur) : sortir de la chambre de la 163 (le HUD glisse à l'écran) ; la 476 (les pièces apparaissent dans
  le cadre) ; la carte 15 après R3.

### 1.2q E19.s — Image 320 × 240, facteur entier, bandes noires, suivie en temps réel 🧪 (exécutée le 2026-10-03 ; recette S6 en attente)

**Décisions** : D-E19-47 (image de l'original, agrandie sans déformation, recalculée en temps réel, bandes noires), D-E19-60
(facteur entier seulement). **Découverte** (2026-10-03, lecture seule, `e19s-disc/notes.md` du scratchpad de la session).

**Faits.**
- **[binaire]** l'écran de l'original fait 320 × 240 : `0x800424AC` appelle `SetDefDrawEnv` (`0x80042504`) et `SetDefDispEnv`
  (`0x8004251C`) en 0x140 × 0xF0 ; les deux TILE plein écran font 0x140 × 0xF0 (`0x800429C8`-`0x800429EC`). La hauteur 236 vient
  de la décompilation (`StaticVariables.cs:55`, `ScreenHeight = 236; //224`, qui écrit aussi 236 dans le TILE de `0x8013FB98`
  là où le binaire écrit 0xF0) : **le binaire l'emporte**. 236 est entré dans le dépôt par `5c7fdad` (convertisseur) et `1507afc`
  (zoom de la DLL, pour que 944 / 236 donne 4).
- **Où vit la taille** : convertisseur `AlundraDisplay.cs:32-51` (320 × 236, ×4, fenêtre 1280 × 944, zoom 4), lu par
  `ProjectWriter.cs:75-76` et `WorldWriter.cs:459-460, 473` ; DLL `AlundraCameraMath.cs:54-55` (320 et 240 pour la butée et les
  fonds), `:90` (236, zoom seulement), `:348` (`ComputeCameraZoom`), appliqué une fois par monde (`AlundraCameraDirector.cs:188`) ;
  les quatre écrans (`AlundraHudScreen.cs:82-105`, `AlundraInventoryScreen.cs:114-136`, `AlundraSubInventoryScreen.cs:86-99`,
  `AlundraSaveScreen.cs:79-100`) prennent `max(1, largeur / 320)` une fois, dans `OnWindowLoaded` ; leurs toiles XAML font déjà
  320 × 240.
- **Moteur** : la fenêtre est redimensionnable (`ProjectWriter.cs:65`, `CasaEngineGame.cs:440`) mais, en DesktopGL, un
  redimensionnement ne fait que changer le back-buffer et lever `ClientSizeChanged` ; `CasaEngineGame.OnScreenResized` n'est
  atteint que par `DeviceReset` et `ApplyDisplaySettings` (`:145`, `:217`, `:265`) : la vue, la caméra et les bornes de MGUI
  gardent la taille du chargement du monde (`GameManager.cs:122-136`) (déduit du code, non lancé). Une vue plein écran par monde
  (`DefaultRuntimeViewBootstrapper.cs:36`), aucune cible de rendu, aucune bande ; `BackBufferPresenter`/`PresentMode.Fit`
  existent mais ne sont ni branchés ni complets. MGUI suit le rectangle de sa vue (`ViewRenderHost.cs:11-19, 47-78`) ; un écran
  XAML n'a que `OnWindowLoaded` (`XamlUIScreenBase.cs:111-138`). Fonds, couches cellulaires et fondus sont en unités du monde
  autour de la cible de la caméra : rien à y changer.
- **Le halo de la 476** est la couche cellulaire 0 de la carte, un rectangle de 320 × 240 unités du monde fixé sur la cible de la
  caméra (`CellularLayerComponent.cs:185-252`) : il ne paraît plus petit que la fenêtre que si la vue montre plus de 320 unités
  (fenêtre plus large que 320:236 au chargement, ou agrandie après). E19.s supprime la cause.

**Règles.**
- **S-R1 — Taille native** : 320 × 240 partout. Convertisseur : `AlundraDisplay.NativeHeight` 240, fenêtre par défaut
  1280 × 960, zoom de la caméra 4 ; la note de 1,7 % d'écart d'aspect disparaît (320 × 240 est exactement 4:3). DLL :
  `CameraDisplayHeight` et `ComputeCameraZoom` sont retirés, le directeur de caméra ne pose plus le zoom (il garde
  `PixelSnap`) : le zoom appartient au moteur.
- **S-R2 — Résolution virtuelle du moteur** : un réglage de projet déclare une résolution virtuelle (largeur, hauteur) et son
  mode d'échelle ; seul mode livré, `IntegerFit` ; bandes noires. Réglage absent : comportement d'aujourd'hui, inchangé
  (éditeur, démos, autres projets).
- **S-R3 — Mise en page**, fonction pure du moteur : `k = max(1, floor(min(L / 320, H / 240)))` ; rectangle de l'image
  320k × 240k centré, décalages `floor((L − 320k) / 2)` et `floor((H − 240k) / 2)`, puis rogné par la fenêtre (une fenêtre plus
  petite que 320 × 240 montre le centre de l'image à l'échelle 1). Valeurs écrites d'avance :

  | Fenêtre | k | Rectangle (x, y, l, h) |
  |---|---|---|
  | 1280 × 960 | 4 | (0, 0, 1280, 960) |
  | 1280 × 944 | 3 | (160, 112, 960, 720) |
  | 1920 × 1080 | 4 | (320, 60, 1280, 960) |
  | 2560 × 1440 | 6 | (320, 0, 1920, 1440) |
  | 800 × 600 | 2 | (80, 60, 640, 480) |
  | 640 × 480 | 2 | (0, 0, 640, 480) |
  | 1281 × 961 | 4 | (0, 0, 1280, 960) |
  | 300 × 200 | 1 | (0, 0, 300, 200) |
  | 400 × 200 | 1 | (40, 0, 320, 200) |

- **S-R4 — Temps réel** : hors éditeur (`!UseExternalViewManagement`), le moteur s'abonne à `Window.ClientSizeChanged` et
  applique la mise en page : la vue unique prend le rectangle (`ViewportRect`), sa caméra 2D prend `Zoom = k` **et un
  viewport de la taille du rectangle rogné** (la projection se calcule sur le viewport de la caméra, `Camera2dComponent.cs:84-88`,
  aujourd'hui posé à la taille de la fenêtre par `CameraComponent.cs:95-96, 106-110` et `CasaEngineGame.cs:283, 299-300` :
  sans cela, une fenêtre de 1920 × 1080 au zoom 4 montrerait 480 × 270 unités écrasées dans 1280 × 960), posé après
  `World.OnScreenResized` ; même calcul à la
  création de la vue (`DefaultRuntimeViewBootstrapper`) et dans `OnScreenResized` ; quand le rectangle ne couvre pas la fenêtre,
  le back-buffer entier est effacé en noir avant les vues, à chaque image.
- **S-R5 — Écrans** : le moteur ajoute à `XamlUIScreenBase` un rappel de changement des bornes de l'écran (manque relevé : seul
  `OnWindowLoaded` existe), appelé quand les bornes valides du bureau MGUI de la vue changent ; les quatre écrans d'Alundra y
  refont exactement le calcul d'`OnWindowLoaded` (fenêtre aux bornes, `max(1, largeur / 320)`, qui vaut k puisque la vue fait
  320k de large). La boîte de dialogue temporaire du moteur suit sa vue sans autre changement (E19.f2 la remplace).
- **S-R6 — Fenêtre** : elle s'ouvre toujours à la taille du projet (1280 × 960) ; rien n'est mémorisé (comme aujourd'hui).

**Tâches.**
- **S-1 — Moteur, tests d'abord** (branche dédiée du sous-module `chantier/e19s-virtual-resolution`, depuis la `main` du
  moteur ; ne jamais indexer `CasaEngine.Launcher/Program.cs`, modification locale de l'auteur) : la fonction de mise en page
  (table de S-R3), le réglage de projet (lecture, écriture, absent = inerte), l'amorce de la vue, `OnScreenResized` (rectangle,
  zoom et viewport de la caméra : fenêtre 1920 × 1080, k 4 → viewport de la caméra 1280 × 960 et aire visible 320 × 240, à la
  création de la vue comme après un redimensionnement ; fenêtre 400 × 200 → viewport 320 × 200), l'abonnement à `ClientSizeChanged`, l'effacement des bandes, le rappel des écrans XAML ; ADR du moteur (numéro suivant au
  moment de l'exécution) et `docs/engine/rendering-2d-3d-spaces.md`.
- **S-2 — Convertisseur** : `AlundraDisplay` (240, doc), écriture du réglage dans `AlundraGame.json`, `WorldWriter` (doc
  « 320x236 ») ; tests : `WorldWriterTests.cs:340` (236 → 240) et doc `:296-303`, nouveau test du réglage écrit ;
  `docs/guidelines-runtime-alundra-casaengine.md:60-62`.
- **S-3 — DLL** : retrait du zoom (`AlundraCameraMath.cs:57-90, 336-348`, `AlundraCameraDirector.cs:140-189`) ; les quatre écrans
  branchent le rappel de S-R5 ; tests : `AlundraWorldProxyCameraFollowTests.cs:192-224` retiré avec `ComputeCameraZoom`, nouveau
  test par écran (bureau de 640 × 480 puis 960 × 720 : échelle 2 puis 3, fenêtre aux nouvelles bornes).
- **S-4 — ADR-0027** (l'image d'Alundra est le 320 × 240 du binaire, à facteur entier et bandes noires ; 236 était une erreur de
  la décompilation) et le plan.
- **S-5 — Livraison** : tests du moteur, pointeur du sous-module, build Release puis Debug (la Debug en dernier, `cmp`),
  manifeste avant, export complet en place, manifeste après, double export.

**Acceptation.**
1. Tests de S-1 à S-3 rouges d'abord, verts après ; la table de S-R3 tenue telle quelle.
2. Tests existants touchés, liste fermée : `WorldWriterTests.cs:296-345` ; `AlundraWorldProxyCameraFollowTests.cs:192-224`
   (retirés) ; `AlundraWorldProxyUpdateCharacterizationTests.cs:153-174` et `:217-240` (le directeur ne pose plus le zoom :
   `camera.Zoom` garde sa valeur d'avant, 1,0, le défaut du montage, `Camera2dComponent.cs:26`, au lieu de `MinimumZoom` ;
   `PixelSnap` posé une fois, inchangé ; les noms qui parlent du zoom ajustés) ; les tests de toile des écrans (`AlundraHudScreenXamlTests.cs:196-212` et ses trois frères) seulement si la source
   de l'échelle change de forme, valeurs inchangées (640 × 480 → 2). `ScreenEffectComponentViewSizeTests`,
   `Camera2dComponentTests`, `CellularLayerServiceTests` et `BackdropStageDefinitionTests` inchangés.
3. Export : exactement `AlundraGame.json` (`DebugHeight` 960 et le réglage), `Entities/AlundraCamera.entity` (hauteur du
   rectangle 960) et `report.json` changent ; tout autre fichier changé est un arrêt ; double export identique hors `report.json`.
4. `CasaEngine.Tests` (construit à part, il n'est pas dans le `.sln`) et `Alundra.Tests` en Release puis en Debug, la Debug en
   dernier, `cmp` sans écart ; les six traces à l'octet.
5. **Recette S6** (auteur) : fenêtre redimensionnée en jeu (l'image suit à chaque palier entier, bandes noires), plein écran
   1920 × 1080 (×4, bandes de 320 et 60 px), le halo de la 476 couvre toute l'image, HUD, inventaire, sous-inventaire et écran de
   sauvegarde suivent l'image.

**Retour arrière** : branche du moteur et pointeur abandonnés ; ré-export en place avec le convertisseur d'avant ; une DLL
d'avant E19.s reste compatible avec un export d'E19.s (le réglage n'est lu que par le moteur).

**Risques.** Un écran MGUI dans une vue décalée (prouvé par l'écran partagé, jamais par Alundra) ; l'effacement des bandes et
l'ordre des vues ; l'éditeur ne doit rien voir (S-R2) ; le moteur est ouvert dans Visual Studio par l'auteur : S-1 attend que le
sous-module soit libre.

- **Fait le 2026-10-03.** Moteur (branche `chantier/e19s-virtual-resolution`, partie de `main` `22228ffd`, plan
  `ai-agent/tasks/e19s-virtual-resolution-tasks.md`, ADR-0048, `CasaEngine.Launcher/Program.cs` de l'auteur jamais indexé) : `c54cc939` plan,
  `fb97d4b7` mise en page pure, `b07f3b5e` réglage de projet, `696dcb1a` application à la vue/caméra/bandes, `b236dfbe` rappel des bornes des
  écrans XAML, `428b0e49` ADR-0048 et doc, `dfaed7a6` clôture du plan (pointeur du sous-module). Parent : `1250f9b` convertisseur (le pointeur y
  entre, sans quoi ce commit ne se construit pas : écart au brief, qui le rangeait avec la DLL), `e08c467` DLL, `b386132` ADR-0027, puis cette puce.
  - **Rouges d'abord, valeurs lues.** Mise en page : talon qui rend l'échelle 0, 19 tests sur 22 rouges (1920 × 1080 → 0 au lieu de 4). Réglage de
    projet : réglage lu null, les cinq déclarations invalides ne lèvent rien. Application : 1920 × 1080 → rectangle (0, 0, 1920, 1080) lu au lieu
    de (320, 60, 1280, 960) ; 400 × 200 → (0, 0, 1280, 960) lu au lieu de (40, 0, 320, 200) ; prédicat des bandes toujours faux. Rappel des bornes :
    liste vide lue au lieu de [(0, 0, 960, 720)]. Convertisseur : `DebugHeight` 944 lu au lieu de 960 (`windowHeight / zoom` 236 au lieu de 240) et
    `VirtualResolution` absent. DLL : `camera.Zoom` 9,99999975E-05 (`MinimumZoom`) lu au lieu de 1 (2 tests) ; les quatre écrans restent à l'échelle
    2 au lieu de 3 après le passage du bureau de 640 × 480 à 960 × 720 (4 tests). Aucune valeur écrite d'avance n'a été contredite.
  - **Verts après.** Moteur `CasaEngine.Tests` 2490 sur 2490 (Debug, aucune instabilité de matériaux constatée) ; la table de S-R3 tenue telle
    quelle (neuf lignes) ; viewport de la caméra 1280 × 960 et aire visible 320 × 240 pour 1920 × 1080 à la création de la vue comme après un
    redimensionnement, 400 × 200 → viewport 320 × 200. Convertisseur 418 sur 418 (Debug), dont le nouveau test du réglage écrit. DLL
    `Alundra.Tests` 2505 sur 2505 en Release puis en Debug (la Debug en dernier), `cmp` de `Alundra/bin/Debug/net9.0-windows/Alundra.dll` contre
    `alundra-project/Alundra.dll` sans écart (2504 avant : 3 tests de zoom retirés, 4 de suivi des écrans ajoutés).
  - **Tests existants touchés (liste fermée tenue).** `WorldWriterTests.cs` (236 → 240 et doc) ; `AlundraWorldProxyCameraFollowTests.cs` (les trois
    tests de `ComputeCameraZoom` retirés, doc de tête) ; `AlundraWorldProxyUpdateCharacterizationTests.cs` (deux tests : zoom gardé à 1, noms
    ajustés `FirstFrame_ResolvesPixelSnap_LeavesTheZoomToTheEngine_...` et `PixelSnap_IsSetOnlyOnFirstFrame_AndTheZoomIsNeverTouched`) ;
    `HeadlessUiTestHarness.cs` de la DLL (méthode `Resize`, la surface devient modifiable). Aucun autre test n'a changé ni rougi ;
    `ScreenEffectComponentViewSizeTests`, `Camera2dComponentTests`, `CellularLayerServiceTests`, `BackdropStageDefinitionTests` et les tests de toile
    des écrans sont inchangés.
  - **Export.** Manifeste SHA-1 de `alundra-project/` (hors `Alundra.dll`, `Alundra.pdb`, `.casaeditor/` ; 23741 fichiers) avant et après l'export
    complet en place : exactement `AlundraGame.json` (`DebugHeight` 960, bloc `VirtualResolution` 320 × 240 `IntegerFit`),
    `Entities/AlundraCamera.entity` (viewport 1280 × 960, zoom 4.0) et `report.json` changent ; vérification de l'export passée (20026 chargés,
    2391 existences). Double export identique hors `report.json`. Les six traces à l'octet (`git diff --ignore-cr-at-eol --exit-code`), les
    quatre traces du héros remises par `git checkout`.
  - **Écarts au plan.** (1) Le pointeur du sous-module entre avec le commit du convertisseur et non avec celui de la DLL, pour que chaque commit se
    construise. (2) Le rappel des écrans est `XamlUIScreenBase.OnScreenBoundsChanged(Rectangle)`, déclenché par `NotifyScreenBounds`, que
    `UIRoot.Update` appelle chaque image via `ScreenStack.NotifyScreenBounds` (tous les écrans XAML de la pile, y compris ceux gelés sous un modal) ;
    chaque écran d'Alundra a une méthode `ApplyScreenBounds` qui refait le calcul d'`OnWindowLoaded`. (3) Réglage nommé `VirtualResolution`
    (`Width`, `Height`, `Mode`) ; effacement des bandes dans `CasaEngineGame.Draw` juste avant le rendu des vues, la fenêtre du back-buffer
    posée puis rendue autour du `Clear`. (4) Le document `docs/engine/rendering-2d-3d-spaces.md` est en français comme le reste de ce
    document. (5) `docs/demarrage-nouvelle-partie.md` (« 320 × 236 ») corrigé aussi. (6) ADR-0027 : le numéro 0026 reste libre.
  - **Non exécuté en test** (périphérique graphique requis) : l'abonnement à `Window.ClientSizeChanged`, l'effacement noir des bandes, la ligne
    de `UIRoot.Update`. Les fonctions qu'ils appellent le sont. À voir à la recette S6. Icônes d'équipement du HUD : leur centrage dépend du
    facteur, il est refait au prochain tick du présentateur, pas dans le rappel.
- ✅ **Vérification d'E19.s** (2026-10-03) : `Alundra.Tests` 2505/2505 en Release puis en Debug, la Debug en dernier ; `cmp` sans
  écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** pour les acceptations 1 à 4 (moteur relu : réglage absent
  inerte et invisible à l'éditeur, mise en page conforme aux neuf lignes, ordre `World.OnScreenResized` puis vue et caméra, viewport
  de la caméra 1280 × 960 et aire visible 320 × 240 à la création comme après un redimensionnement ; lignes sans test vérifiées à la
  lecture ; `CasaEngine.Tests` 2490/2490 ; convertisseur 418/418 ; mutations : `camera.OnScreenResized` retiré (4 rouges), rappel
  d'un écran vidé (rouge) ; la ligne `UIRoot.cs:135` n'a pas de test, comme annoncé ; empreintes de l'export égales au manifeste
  d'après). Écarts déclarés acceptés. Avis reportés à E19.m : P3 une valeur non numérique ou booléenne du réglage lève une erreur qui
  ne nomme pas la clé, un flottant est arrondi (`ProjectSettingsHelper.cs:86-88`) ; P4 commentaire « 320x236 »
  (`AlundraHudComposer.cs:43`) ; P4 section de doc du moteur en français dans un document français alors que l'`AGENTS.md` du moteur
  demande l'anglais pour `docs/` (à l'auteur) ; P4 trois branchements sans test (`UIRoot.cs:135`, `CasaEngineGame.cs:472`, `:638`),
  couverts par la recette S6. **Reste la recette S6 de l'auteur.** Branche du moteur `chantier/e19s-virtual-resolution` (`dfaed7a6`),
  non mergée : le pointeur du parent la désigne.

### 1.2r E19.t — Son de chaque changement d'animation 🧪 (exécutée le 2026-10-03 ; recette T6 en attente)

**Ordre et propriété** (révision n°1) : E19.t s'exécute **après D5 d'E19.d2c2 et après E19.h1b1**, qui touchent les mêmes
fichiers (`AlundraScriptedMotion.cs` pour D5 ; les sites d'apparition d'`AlundraWorldProxy.cs` et la fabrique pour h1b1) ; une
seule tranche à la fois possède ces fichiers. Sa base est le commit de clôture d'E19.h1b1 : les ancres de ce texte (mesurées sur
`99ff51d`) et sa liste fermée sont re-mesurées sur cette base par l'audit des valeurs avant l'exécution.

**Décisions** : D-E19-57 (toutes les entités, apparition comprise), D-E19-61 (sons d'apparition au chargement résolus avec le
groupe de la nouvelle carte). **Découverte** (2026-10-03, lecture seule, `e19snd-disc/notes.md` du scratchpad de la session).

**Faits.**
- **[binaire]** un seul site : le bloc de changement d'`UpdateAnimation` (`0x80038AB4`), en `0x80038BB0`-`0x80038BC8` :
  `id = octet 0xC du jeu d'animation`, plus `0x100` si `octet 0xD & 0x20`, demandé par `0x800490FC`. `UpdateAnimation` a deux
  appelants : la passe par tick (`0x80038E4C`) et l'apparition (`0x80039EA8`).
- **Changement** : nouvelle cible, nouvelle rangée de direction (table `0x800237F4`), fin de chaîne, auto-chaîne comprise ; un
  tour de boucle, un maintien ou la même animation redemandée ne jouent rien ; `0x1C` et `0x5F` ne rejouent qu'une animation
  tenue (`0x8003D820`-`0x8003D840`). La DLL a déjà cette détection (`AlundraFrameSyncPasses.cs:215-262`).
- **Apparition** : `InitializeEntity` écrit `Current = ~Target` (`0x80039DAC`) : toute apparition joue le son de son animation
  d'apparition (animation 0 pour les enregistrements de carte et `0x2D`/`0x8A`/`0x8B` ; le héros, son animation d'arrivée,
  `0x80031974`). La DLL exempte ce premier changement (R2).
- **Groupe au chargement** : le binaire cherche ces sons dans le groupe de la carte précédente (D-E19-61 : corrigé) ; la DLL
  construit déjà le lecteur du monde avec le groupe de la nouvelle carte avant les apparitions (`AlundraWorldProxy.cs:697`, puis
  `:726` et `:774`) : la correction ne demande rien de plus.
- **Portes** : compte à rebours du changement de BGM (`0x80175850`), `id <= 0`, `id > 0x3C2`, table anti-doublon par image, rangée
  −2, plafond de voix ; aucune porte de position ni de visibilité ; seules les entités actives changent (statuts 2 et 3). Tout est
  déjà dans `AlundraSoundPlayer.PlaySfx` (`:160-232`), sauf que l'id 0 y entre dans la table anti-doublon : l'appelant saute
  `id <= 0`.
- **Données** : octet `0xC` (`Sfx`) et octet `0xD` (bit `0x20`, nommé `Acceleration` dans le JSON) déjà exportés
  (`SpriteWriter.cs:1023-1025`) et lus (`SpriteRecordCatalog.cs:72-102`) ; la décompilation lit l'octet `0xB` (`EntityManager.cs:241-245`),
  faux contre le binaire sur 91 des 2405 jeux. Recensement : 161 préfabs sur 395 et 471 jeux sur 2405 portent un son ; 80 jeux ont
  le bit de banque.
- **Audit des valeurs** (2026-10-03, `e19t-audit/notes.md` du scratchpad) : toutes les valeurs de T-1 tenues (héros 93 → 480,
  `0edffd14` 5 → 365, auto-chaîne 15 → 22, 476 `0x8A @553` → 219 → 864 dans le groupe 62, 392 `@64` → 13, le livre 204 une fois
  à la 179 `@224` et à la 178 `@728`, ce dernier livre né de `0x2D [2] @537`, arrivée en 43 → `[10]`), **sauf une** : Melzas
  (enregistrement 0 de la 476) n'apparaît **jamais** au chargement, son `SpriteDirection` 0x80 n'ayant pas le bit 0x40 que demande
  l'apparition au chargement (binaire `0x8003A260`-`0x8003A278`, appel en `0x8003B308` ; `AlundraEntitySpawnFactory.cs:87-92`) ;
  il n'apparaît que par `0x8A @534` et `0x8A @553`. Sur les 483 cartes, 20 enregistrements ont un préfab qui sonne en animation 0
  et aucun ne peut apparaître au chargement : D-E19-61 ne touche, sur la chaîne, que l'arrivée du héros dans l'animation 55 (son
  229, banque 4), les autres sons du héros étant dans la banque système. Le test « Melzas au chargement » est remplacé par un
  montage synthétique de la même règle ; l'auteur en est informé. Ancres re-mesurées sur `b381de8` : `ZImpulseSfxOf`
  `AlundraEntityScriptProxy.cs:282-283`, `StepAnimationClock` des PNJ `:1220` et du héros `:1310`, `SyncAnimation` `:1357`,
  animation d'apparition `AlundraEntitySpawnFactory.cs:680-690`, `SpawnEntityByRecordId` `AlundraWorldProxy.cs:2504-2563` ; la
  demande d'arrivée vient après l'affectation des jeux d'animation du héros (`AlundraWorldProxy.cs` ~1747) ; le test d'arrivée
  demande un faux moteur audio ou un point d'appui interne (le lecteur du monde est remis à neuf en ~963). Aucun test à lecteur
  enregistreur ne gagne de demande : la liste fermée est UJSND seul (renommé, contenu inchangé, `[10]`, `[10]`, `[]`, `[]`).
  Point ouvert : la carte 10 `@5651`, `0x1A [16]` sur l'enregistrement 78 (Beaumont), vise une animation absente de son jeu.
- **DLL d'aujourd'hui** : seul le décollage du héros joue (R8, `AlundraScriptedMotion.cs:242-245`, `ZImpulseSfxOf`,
  `AlundraEntityScriptProxy.cs:267`, qui ignore `+ 0x100`).

**Règles.**
- **T-R1 — Un seul crochet** : à chaque changement d'animation d'une entité active, détecté par la passe existante, la DLL
  demande `id = Sfx + (octet 0xD & 0x20 ? 0x100 : 0)` au lecteur du monde (`ScriptHost.SoundPlayer.PlaySfx`), au tick du
  changement ; rien si `id <= 0`.
- **T-R2 — Apparition** : chaque apparition demande une fois le son de son animation d'apparition : apparitions par script
  (`SpawnEntityByRecordId` : `0x2D`, `0x8A`, `0x8B`) au tick de l'opcode ; apparitions du chargement et arrivée du héros
  (`AdoptPlayerPawn`) à l'initialisation du monde, avec le lecteur du monde (groupe de la nouvelle carte, D-E19-61) ;
  l'exemption R2 empêche le premier changement de la redemander.
- **T-R3 — Décollage** : l'appel dédié de R8 et `ZImpulseSfx`/`ZImpulseSfxOf` disparaissent ; le son du décollage vient de T-R1
  (aucune double demande).
- **T-R4 — Portes** : celles d'`AlundraSoundPlayer.PlaySfx`, inchangées.
- **T-R5 — Rattrapage** (accepté, comme R1) : un changement annulé par un second tick de la même image n'est pas entendu.

**Tâches.**
- **T-1 — Tests d'abord** (valeurs de la découverte, confirmées par l'audit des valeurs avant l'exécution) : héros, animation 93
  → 480 (bit de banque) ; préfab `0edffd14`, animation 5 → 365 (la décompilation donnerait 109) ; `Sfx` 0, tour de boucle,
  maintien, même animation redemandée → rien ; auto-chaîne → une demande par cycle (héros, animation 15 → 22) ; 476 `0x8A @553`
  → 219 demandé au tick de l'opcode, que la banque résout en 864 dans le groupe 62 ; **son d'apparition au chargement résolu dans
  le groupe de la nouvelle carte** (D-E19-61), montage synthétique : un enregistrement chargeable dont l'animation 0 porte un son
  de groupe, chargé après une carte d'un autre groupe → la demande se résout dans le groupe de la nouvelle carte (révision de
  l'audit : aucun enregistrement réel ne le permet, voir les faits) ; 392 `0x5B [0x81, 3, 0x43] @64` → 13 ; le livre, 179 `@224` et 178 `@728` → 204 une fois
  chacun ; arrivée du héros par `AdoptPlayerPawn` dans l'animation 43 → `[10]`, et le premier changement n'ajoute aucune seconde
  demande (l'animation d'arrivée par défaut, 0x36, n'a pas de son : elle ne prouverait rien) ; retirer la demande de
  `AdoptPlayerPawn` doit rendre ce test rouge.
- **T-2 — Code** : le crochet dans la passe de changement (`AlundraFrameSyncPasses.cs`, `AlundraEntityScriptProxy.StepAnimationClock`),
  l'apparition (`AlundraWorldProxy`, fabrique), le retrait de R8.
- **T-3 — ADR-0028** (chaque changement d'animation joue son son, apparition comprise ; octet `0xD` ; groupe de la nouvelle carte ;
  remplace la portée « décollage seul » d'ADR-0023).
  - Fait le 2026-10-03 (🧪 : la recette T6 de l'auteur manque). **Rouges d'abord** sur le code d'avant (`AlundraAnimationSoundTests`, 13 tests, 12
    rouges ; valeurs lues) : héros 93 → 480 `[]` (attendu `[480]`) ; `0edffd14` 5 → 365 `[]` (attendu `[365]`) ; maintien `[]` (attendu `[8]`) ; tour
    de boucle et même animation redemandée `[]` (attendu `[9]`) ; auto-chaîne 15 → 22 : aucune demande ; 476 `0x8A @553` : 0 demande de 219 (attendu
    1) ; 392 `0x5B @64` : 0 demande de 13 (attendu 1) ; livre de la 178 `@728` : 0 demande de 204 (attendu 1) ; livre de la 179 `[]` (attendu
    `[204]`) ; arrivée du héros par `AdoptPlayerPawn` en animation 43 `[]` (attendu `[10]`) ; son d'apparition au chargement `[]` (attendu le clip
    du groupe 62) ; le test « `Sfx` 0 » est vert d'emblée (garde). **Verts après** : 13/13. **Mutations jetables** (fichiers restaurés, `cmp` sans
    écart) : l'exemption R2 levée pour le son → rouges : arrivée en 43 et 476 (seconde demande) ; octet `0xB` (`Flags`) pour la banque → rouges :
    93 → 480 et 5 → 365 ; une demande à chaque tick de l'animation courante (tour de boucle, maintien) → 7 rouges (maintien, boucle, même
    animation, auto-chaîne, livres 178 et 179, 392) ; la demande d'`AdoptPlayerPawn` retirée → rouge : arrivée en 43 (point d'appui du test : le
    lecteur du monde remplacé par un enregistreur par réflexion sur son champ, puis `AdoptPlayerPawn` rappelé comme UJ-7 le fait, sans changement de
    production). **Suite Debug** `Alundra.Tests` : 2523/2523 (2510 + 13), `AlundraSoundOpcodesProductionTests` toujours 23 demandes, UJSND renommé
    (`UJSND_TheSoundOfASwitchIsAskedOnceAtTheTickOfTheSwitch_...`), contenu inchangé ; garde d'octets des six traces : `git diff
    --ignore-cr-at-eol --exit-code` rend 0, les quatre traces du héros remises en LF. Le crochet est `AlundraFrameSyncPasses.GiveImpulse`
    (`AnimationSoundOf` dans le proxy d'entité, octet `0xD`) ; l'apparition demande par `RequestAppearanceSound` (chargement, `AdoptPlayerPawn`,
    `SpawnEntityByRecordId`) ; R8 et `ZImpulseSfx` retirés. **Écarts** : (1) la 179 : B[1] ne s'exécute pas de bout en bout dans l'arc (son `0x0B
    @114` attend une marche de Septimus que le montage ne produit pas, 3000 images), le livre est donc créé par l'appel de production de l'opcode
    (`SpawnEntityByRecordId`, enregistrement 2) puis reçoit l'animation 1 comme `0x1A` ; (2) la 392 : la liste exacte `[13]` est remplacée par « un
    seul 13, à l'image 121 à 123 » (les opcodes de son de la scène demandent d'autres ids : 306, 85, 57, 303 ×2) ; la demande suit l'opcode d'une image
    (ordre existant de la DLL : les événements de carte passent après les entités ; D-E19-64 ne vaut que pour la boîte, M-38) ; (3) l'auto-chaîne 15 → 22 est jouée sur un PNJ à sprite avec les valeurs du jeu d'animation du héros (le montage héros n'a pas de
    sprite) ; (4) le chargement : montage synthétique sur la 476 réelle (bit `0x40` de l'enregistrement 0 posé, jeu à moteur audio factice), la
    demande se résout en 864 (clip du groupe 62) ; (5) « l'exemption R2 s'applique au son » est lue comme « l'exemption est levée pour le son » ;
    (6) `ArcRun` reçoit un paramètre optionnel `beforeInitialize` (support de test) ; (7) l'acceptation 3 « Release puis Debug » n'a eu que la Debug
    (consigne d'exécution : pas de Release ici).

**Acceptation.**
1. Tests de T-1 rouges d'abord, verts après ; une valeur contredite est un arrêt.
2. Tests existants touchés, liste fermée : UJSND (`AlundraHeroJumpTests.cs:256-306`, renommé : son nom contredit D-E19-57 ; ses
   trois premiers segments inchangés ; le segment d'arrivée reste `[]` sur le chemin du changement, le son d'arrivée passe dans le
   nouveau test d'arrivée) ; tout test à lecteur enregistreur dont la liste gagne une demande est un arrêt, sauf ceux que l'audit
   des valeurs aura nommés avant l'exécution (candidats : `AlundraJumpTestSupport.cs:192`, `AlundraWarpDepartureTests.cs:847`,
   `AlundraEventProgramRunnerTests.cs:174` et `:3615`) ; `AlundraSoundOpcodesProductionTests` (23 demandes) inchangé ; les arcs
   n'ont pas de lecteur ; les six traces à l'octet (aucune demande de son n'y est écrite).
3. `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart.
4. **Recette T6** (auteur) : à la 476, le grognement de Melzas à son apparition par `0x8A @534` et `@553` ; la page du livre des 178 et 179, une fois ; le sprint de la 392 ; le son du saut inchangé.

**Retour arrière** : revert du commit (DLL seule, aucun export).

**Risques.** Des sons nouveaux partout où un préfab sonore change d'animation (161 préfabs) : c'est la règle ; la table
anti-doublon par image rendue et non par tick (écart existant) ; une animation cible absente du jeu du préfab (hors chaîne) : rien
n'est demandé, et l'original lirait au-delà de la table (point ouvert à consigner si l'audit le confirme).
- ✅ **Vérification d'E19.t** (2026-10-03) : `Alundra.Tests` 2523/2523 en Release puis en Debug, la Debug en dernier ; `cmp` sans
  écart ; les six traces à l'octet. Vérificateur neuf : **CONFIRMED** (binaire relu en `0x80038BB0`-`0x80038BC8` : banque par
  l'octet `0xD` ; un seul crochet atteint par trois chemins ; trois sites d'apparition, seuls sites de création ; ses mutations
  attrapées : demandes d'apparition retirées, R2 levée pour le son, banque lue à l'octet `0xB` ; UJSND renommé seulement ; décalage
  d'une image de la 392 mesuré : l'opcode à l'image 121, le son et le changement visible à 122, dû à l'ordre existant de la DLL,
  les événements de carte après les entités, pas à cette tranche). Avis reportés à E19.m : P4 l'ADR-0028, le plan et
  `AlundraAnimationSoundTests.cs:281` attribuent ce décalage à D-E19-64, qui ne vaut que pour la boîte de dialogue ; P3 le livre de
  la 179 n'est pas exercé par son vrai opcode (le `0x0B @114` cale l'arc, antérieur) ; P4 le test du groupe au chargement contraste
  avec un groupe nul, pas avec une carte d'un autre groupe ; P4 la période de l'auto-chaîne n'est pas épinglée. **Reste la recette T6
  de l'auteur.**

### 1.2s E19.m — Hygiène et clôture ⏳ (recensement fait le 2026-10-03 ; E19.m0 ✅ ; E19.m1 ✅ ; E19.m2 ✅ ; E19.m3 ✅ ; E19.m4 ✅ ; E19.m5 ✅)

**Recensement** (2026-10-03, lecture seule ; table complète versionnée dans `docs/plan-e19-m-annexe/backlog-2026-10-03.md`, en
anglais) : 46 points M-01 à M-46, chacun vérifié contre le code de `bafbd5a`, classé (test seul, commentaire ou doc, petit correctif
de production, décision de l'auteur, autre tranche) et groupé : **G1** tests seuls, faisables maintenant ; **G1-late** tests seuls,
après f2a ; **G2a/G2b** commentaires et docs ; **G3** interpréteur et table des tailles, après f2a (`0x5F` de taille 8, libellés
`0x35`/`0x36` inversés qui changent deux traces, `InitializeEventData`, recherches sans allocation) ; **G4** petits correctifs de
fidélité hors f2a et G0 (balayage des portails, deux portes de l'aimantation au sommet, exemption d'apparition et ligne de direction,
plafond absolu) ; **G5** moteur (erreur du réglage qui ne nomme pas la clé) ; **G6** décisions de l'auteur ou autres tranches ;
**G7** tests statiques du corpus, en dernier. La marche de la chaîne portée en Python retrouve exactement les 134 lignes de
`story-chain-skipped-opcodes.tsv`.
- **M-04, à corriger d'abord (E19.m0)** : les modes aléatoires 4 et 5 de `ResolveDirectionFromParam` lèvent toujours
  `NotSupportedException` (`AlundraEventProgramRunner.cs` ~3024-3030) ; le mode 4 est atteint à la carte 167 (Inoa, intérieur),
  enregistrement 1, `C[4]` `@144` et `@186` (`0x5A [0x80, 0x80]`), environ 120 ticks après le chargement si T1 n'est pas posé ; rien ne
  rattrape l'exception : **plantage probable du jeu** (preuve statique, non reproduite). Le générateur du binaire est déjà porté
  (`AlundraRandom`).
- **Points pour l'auteur** (G6) : M-03 `0x01` rend 0 dans la décompilation, 1 dans la DLL (0 site du corpus ; des tests s'en servent
  comme bourrage) ; M-05 le test statique « aucun opcode atteignable sauté hors E14 et E18 » ne peut pas passer de longtemps : 22
  opcodes (483 sites) sans propriétaire (`0x60` 172 sites, `0x6A`, `0x96`, `0x6B`, `0x6D`, `0x97`, `0xAA`, `0x72`, `0x56`, `0x61`,
  `0xC3`, `0x6C`, `0xB2`, `0xA9`, `0xB5`, `0x88`, `0xC0`, `0xC1`, `0xC2`, `0xB6`, `0xB3`, `0x3D`), les autres à E19.g (1427), E14 (318),
  E19.h1b2 (283), E19.f (97), `0xB9` (42), E18 (2) : proposition, une liste versionnée avec une colonne de propriétaire ; M-12 réemploi
  des créneaux et plafond de 63 entités (E14 ou E19.m) ; M-36 langue de la section de doc du moteur ; M-39 dépend du préréglage
  `day3-start` (O-E19-33) ; M-41 écraser K1 à K3 avant le merge ; M-27 l'attribution des commits aux exécuteurs ; M-06 et M-08
  (O-E19-2, O-E19-25) relèvent de la décompilation et des scripts de recensement, hors du portage (règle de l'auteur du 2026-10-03).
- **M-47** (ajouté par E19.m0) : `0x5A`/`0x5B` parcourent les entités trouvées de la dernière à la première dans le binaire
  (`0x8003EEF4`, `0x8003EF80`), la DLL de la première à la dernière ; effet sur les modes aléatoires et sur le mode 6 quand le héros est
  trouvé avec d'autres entités ; à recenser (sites en mode 6 avec une recherche qui rend le héros et une autre entité) puis à porter
  (groupe G4). Recensé le 2026-10-03 (aucun site réel où l'ordre compte) : E19.m1, avec M-10.

#### 1.2s.1 E19.m0 — Modes aléatoires de `ResolveDirectionFromParam` (M-04) ✅ (relectures n°1 et n°2 REVISE ; resserrée et corrigée ; relecture de clôture)

**Faits** **[binaire]** (question bornée du 2026-10-03, `e19m0-disc/notes.md` du scratchpad) :
- `0x8003CFC8` : `result = a1 & 0x1F`, `mode = a1 >> 5` (8 ou plus : 0) ; table de sauts en `0x80023C60` : mode 4 → `0x8003D060`,
  mode 5 → `0x8003D0BC` ; le générateur est écrit en ligne à chaque usage, état en `0x80098708` : `nouveau = germe × 0x7D2B89DD +
  0xE06A02E7` (32 bits bas, rangés en `0x8003D08C` et `0x8003D0E8`) ; **mode 4** rend `table[nouveau >> 30]`, table en `0x80023644` =
  {0, 16, 8, 24}, la même que le mode 2 (`AnimationTables.CardinalDirectionTable`) ; **mode 5** rend `nouveau >> 27` (0 à 31) ;
  `result` n'y sert pas. La décompilation est d'accord (`GameEngine.cs:2354-2359`).
- C'est le générateur d'`AlundraRandom` (mêmes constantes ; 10 000 pas comparés) ; 93 fonctions du binaire l'utilisent, la DLL en
  porte deux (`0x0C` = `0x8003D518`, le même calcul que le mode 4 ; `0x8C` = `0x80040438`, `nouveau >> 24`) ; il n'est jamais
  réensemencé (valeur initiale de l'image de l'exécutable, `0xB017C93D`, comme le champ statique de la DLL).
- `0x5A` (`0x8003EEF4`) et `0x5B` (`0x8003EF80`) appellent la routine une fois par entité trouvée, **de la dernière trouvée à la
  première** (tampon `0x8013D8D8`) ; la DLL (`TurnMatchingEntities`) et la décompilation vont dans l'autre sens. L'ordre compte pour
  un mode aléatoire et pour le **mode 6** (direction du héros) quand le héros est l'une des entités trouvées (recherches 0x82 et 0x84,
  `EntitySearchService.cs:134-143`, `:156-166`) : relevé par la relecture n°1, il sort de cette tranche (M-47, groupe G4, avec le
  recensement des sites en mode 6). Les deux sites de la 167 (recherche 0x80, l'entité du script seule) n'ont qu'une entité : l'ordre
  n'y joue pas.
- **Seul `throw` d'opcode atteignable** : `AlundraEventProgramRunner.cs` ~3026 ; sa doc (~2991-2999) dit à tort qu'aucun générateur
  n'est porté ; le test `ResolveDirectionFromParam_RandomModes_ThrowNotSupported` (`AlundraEventProgramRunnerTests.cs:1924-1933`)
  épingle l'exception.

**Règles.**
- **M0-R1** : mode 4 → `AlundraRandom.Next()`, puis `CardinalDirectionTable[nouveau >> 30]` ; mode 5 → `AlundraRandom.Next()`, puis
  `nouveau >> 27` ; `result` ignoré ; la doc de la méthode dit la règle et les adresses.
- **M0-R2** (révision n°1) : l'ordre de parcours ne change pas dans cette tranche (M-47).

**Tâches.**
- **M0-1 — Tests d'abord** (dans la collection `AlundraRandomStaticStateCollection`, germe posé par le test, état comparé en
  `(uint)RandomSeed`) : trois appels consécutifs, mode 4 (`0x80`) et mode 5 (`0xA0`) : germe `0xB017C93D` → 0, 24, 24 et 6, 25, 28 ;
  germe 0 → 24, 8, 16 et 28, 19, 15 ; germe `0x12345678` → 8, 0, 16 et 23, 4, 11 ; en alternance 4, 5, 4 depuis `0xB017C93D` → 0, 25,
  24 ; `0x9F` se comporte comme `0x80` et `0xBF` comme `0xA0` ; **test sur les octets réels** (révision n°1, au lieu d'un arc) : les
  `Codes` de `alundra-project/Maps/Inoa/Inoa (inner)-167/events/Inoa (inner)-167.events.json` (l'octet 144 vaut `0x5A`, suivi de
  `0x80, 0x80`) chargés dans un document du runner, l'état placé à `CodeIndex` 144, l'entité du script seule (recherche 0x80), germe
  `0xB017C93D` : **un appel complet** du runner (révision n°2 : il enchaîne jusqu'à la première attente) n'élève aucune exception ;
  après `0x5A @144` viennent `0x59 [0x80, 8]` (taille 3, animation cible 8), `0x73 [40]` (taille 2), puis l'attente `0x37 [1]` en
  152, qui rend la main : `CodeIndex` 152, direction cible 0 (premier tirage du mode 4), animation cible 8, et
  `(uint)AlundraRandom.RandomSeed` = `0x35E36190` (un seul tirage) ; le test saute si l'export est absent, comme les autres tests sur données réelles. Rouges d'abord (l'exception
  de `:3026`), verts après.
- **M0-2 — Code** (`AlundraEventProgramRunner.cs`).

**Acceptation.** 1. Tests de M0-1 rouges d'abord, verts après, valeurs écrites tenues. 2. Test existant touché, liste fermée :
`AlundraEventProgramRunnerTests.cs:1924-1933` (l'exception), remplacé par les tests de valeurs ; rien d'autre (l'ordre de parcours
ne change pas). 3. `Alundra.Tests` en
Release puis en Debug, la Debug en dernier, `cmp` sans écart ; les six traces à l'octet ; la liste fermée de la chaîne inchangée (aucun
site sur la chaîne). **Retour arrière** : revert du commit (DLL seule).

**Risques.** Les tirages du flux partagé se décalent pour les sites portés suivants (`0x0C`, `0x8C`, et la chute des cellules qui y
puise à tort, O-E19-48) : le flux de la DLL ne suit déjà pas l'original tirage pour tirage (2 sites sur 93 portés).

- **Fait le 2026-10-03** (M0-1, M0-2) : nouveau fichier `Alundra.Tests/AlundraRandomDirectionModesTests.cs` (collection
  `AlundraRandomStaticStateCollection`, 11 cas : 3 germes en mode 4, 3 en mode 5, l'alternance 4-5-4 avec l'état final `0xE4013D62`, un
  tirage `0x35E36190`, `0x9F` et `0xBF`, le test sur les octets réels de la 167 à `CodeIndex` 144). Rouges d'abord : les 11 sur
  `NotSupportedException` de `AlundraEventProgramRunner.cs:3026` (le test de la 167 compris, donc l'export était présent). Verts
  après le code : 11 sur 11, toutes les valeurs écrites tenues sans en changer une (167 : `CodeIndex` 152, direction 0, animation 8,
  germe `0x35E36190`). Test existant touché : seulement `ResolveDirectionFromParam_RandomModes_ThrowNotSupported` (supprimé,
  remplacé). Suite `Alundra.Tests` en Debug : 2548 réussis, 0 échec ; six traces à l'octet (rc 0), les quatre du héros remises.
  Release non lancée (consigne du brief). Écart : aucun.
- ✅ **Vérification d'E19.m0** (2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2548/2548
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet (la puce « Fait » ne parle que de la Debug de
  l'exécuteur). Vérificateur neuf : **CONFIRMED** (binaire relu en `0x8003CFC8`-`0x8003D0E8` et table `0x80023644` ; valeurs
  recalculées par un générateur indépendant ; rouges rejoués en remettant l'exception : 11 sur 11 rouges, dont le test sur les octets
  réels de la 167 ; l'ordre de parcours inchangé). Avis reporté à E19.m : P4 des tests qui touchent `AlundraRandom` hors de la
  collection `AlundraRandomStaticStateCollection` (`AlundraPrefabArcSupportTests.cs:19`, `AlundraShipCorridorArcTests.cs:21`),
  antérieur. **Le plantage probable de la 167 (M-04) est corrigé.**

#### 1.2s.2 E19.m1 — Balayage des portails (M-10) et ordre de `0x5A`/`0x5B` (M-47) ✅ (faite et CONFIRMED le 2026-10-03)

- **Fait le 2026-10-03** (branche `chantier/e19-opcodes`). **Rouges d'abord** sur le code d'avant, valeurs lues égales aux valeurs
  « aujourd'hui » écrites : portail `null` ; mode 5 sur `0x5A` et `0x5B` : A 6 (écrit 25) ; mode 4 : A 0 (écrit 24) ; mode 6 : X 10
  (écrit 7). **Verts après** M1-R1 et M1-R2, valeurs écrites tenues (A 24, B 0 ; A 25, B 6 ; `0x5B` animation 5 pour les deux ; héros
  7, X 7 ; germe final `0xC81B4C37`). Tests : `AlundraPortalDetectionTests` (1 réécrit, `FindPortalAtTile_DestMapIdZero_IsReturnedLikeAnyOtherSlot_TheLaterSlotIsNeverReached`)
  et `AlundraTurnOrderTests` (4 nouveaux, collection `AlundraRandomStaticStateCollection`). Suite complète Debug : 2552 réussis, 0
  échec. Six traces à l'octet (`git diff --ignore-cr-at-eol`) sans écart. Écarts : aucun. La Release et le `cmp` restent à la
  clôture, par la session principale.

**Faits** **[binaire]** (question bornée du 2026-10-03, `e19m1-disc/notes.md` du scratchpad, scripts et sorties à côté) :
- **M-10** : la recherche de portail `0x8003166C` lit la case du héros (`hero+0x120`, `+0x124`), parcourt les 64 créneaux de 12 octets
  de la table en `*(0x800E4334)+0x42C`, compare en octets signés `X >= e[0]`, `X <= e[2]`, `Y >= e[1]`, `Y <= e[3]` et rend le premier
  créneau qui contient la case, ou 0 après 64 (`0x800316E8`) ; elle ne lit jamais `+4` (la carte de destination), ni aucun de ses
  cinq appelants (`0x8002F188`, `0x8002F1D4`, `0x8002F2D0`, `0x8002F378`, `0x8003EB2C`) ; le départ du passage (`0x80031340`) ne se
  sert de `+4` que comme index de la table des cartes (`0x800313A0`). La DLL rend `null` quand le créneau trouvé a
  `DestMapId == 0` (`Alundra/Scripts/AlundraPortalTrigger.cs:28`, doc `:13-20`) ; le test
  `AlundraPortalDetectionTests.cs:79-94` l'épingle. Corpus : 3316 portails dans 483 cartes, aucun de destination 0, aucune coordonnée
  de 128 ou plus, ordre des créneaux gardé : **sans effet sur les données exportées**.
- **M-47** : `0x5A` (`0x8003EEF4`) et `0x5B` (`0x8003EF80`) parcourent le tampon `0x8013D8D8` de `buf[n-1]` à `buf[0]` (`0x5B` pose
  `+0x88` avant l'appel de `0x8003CFC8`) ; la recherche (`0x8003C954`) remplit le tampon dans l'ordre des créneaux (`0x81`, `0x82`,
  `0x84` partent du créneau 0, le héros ; l'id brut, `0x83` et `0x85`-`0x8B` du créneau 1) ; le mode 6 lit le `+0x8C` du héros, que la
  boucle écrit ; les modes 4 et 5 tirent au générateur. La DLL (`TurnMatchingEntities`, `AlundraEventProgramRunner.cs:2978-2988`)
  parcourt de la première à la dernière ; sa liste commence aussi par le héros (`AdoptPlayerPawn` avant les enregistrements,
  `AlundraWorldProxy.cs:732`, `:750`). Recensement : 1700 sites atteints (`0x5A` 578, `0x5B` 1122) dans 218 cartes ; **aucun site où
  l'ordre compte** (les deux sites aléatoires, 167 `@144` et `@186`, cherchent par `0x80`, une entité ; les sites en mode 6, 113
  `@1221` et 394 `@398`/`@408`, par des recherches d'une entité) : sans effet observable sur un site réel.

**Règles.**
- **M1-R1** : `FindPortalAtTile` rend le premier créneau qui contient la case, quelle que soit sa destination ; la doc cite le binaire.
- **M1-R2** : `TurnMatchingEntities` parcourt les entités trouvées de la dernière à la première ; la doc cite `0x8003EEF4` et
  `0x8003EF80` ; la phrase « Known gap » de la doc de `ResolveDirectionFromParam` est retirée.

**Tâches.**
- **M1-1 — Tests d'abord** (valeurs d'aujourd'hui écrites pour le rouge ; une valeur que la mesure contredit est un arrêt) :
  - portails : `[P(index 0 : X 5..5, Y 5..5, destination 0), P(index 1 : X 0..10, Y 0..10, destination 200)]`, case (5, 5) → trouvé,
    `Index` 0, `DestMapId` 0 (aujourd'hui `null`) ; le test `AlundraPortalDetectionTests.cs:79-94` est réécrit en ce sens (son
    `Assert.Null` de `:93` devient ces assertions, son nom suit) ;
  - ordre (collection `AlundraRandomStaticStateCollection`, germe posé par le test, contexte de monde factice, deux entités trouvées
    `[A, B]` par la recherche `0x83`, germe `0xB017C93D`) : `0x5A 0x83 0x80` → A 24, B 0 (aujourd'hui A 0, B 24) ; `0x5A 0x83 0xA0` →
    A 25, B 6 (aujourd'hui A 6, B 25) ; `0x5B 0x83 0x05 0xA0` → les deux en animation 5, A 25, B 6 ; mode 6 : le héros (direction 4)
    et X trouvés `[héros, X]` par `0x5A 0x82 0xC3` → héros 7, X 7 (aujourd'hui X 10).
- **M1-2 — Code** : M1-R1 et M1-R2.

**Acceptation.** 1. Tests de M1-1 rouges d'abord (valeurs d'aujourd'hui), verts après, valeurs écrites tenues. 2. Test existant
touché, liste fermée : `AlundraPortalDetectionTests.cs:79-94` (réécrit) ; rien d'autre (les tests de `0x5A`/`0x5B`,
`AlundraEventProgramRunnerTests.cs:1804-1844`, n'ont qu'une entité trouvée). 3. `Alundra.Tests` en Release puis en Debug, la Debug
en dernier, `cmp` sans écart ; les six traces à l'octet ; la liste fermée de la chaîne inchangée. **Retour arrière** : revert du
commit (DLL seule).

**Risques.** Aucun effet sur les données exportées ; un futur site aléatoire à plusieurs entités tirerait dans l'ordre du binaire.
- ✅ **Vérification d'E19.m1** (2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2552/2552
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet. Vérificateur neuf **CONFIRMED** (binaire relu en
  `0x8003166C`-`0x800316FC`, `0x8003EEF4` et `0x8003EF80` ; rouges rejoués en remettant les deux fichiers de production : 5 rouges
  aux valeurs « aujourd'hui » du plan ; recensements rejoués sur l'export d'aujourd'hui : aucun portail de destination 0, aucun site
  où l'ordre compte). Avis P4 : `AlundraTurnOrderTests.cs` définit son propre contexte de monde au lieu de reprendre
  `FakeEntityWorldContext` (duplication de montage seulement).

#### 1.2s.3 E19.m2 — Couches de cellules comme le binaire (O-E19-48, O-E19-50) ✅ (relecture n°1 READY ; faite et CONFIRMED le 2026-10-03 ; recette à venir)

**Faits** **[binaire]** (audit du 2026-10-03, `e19m2-disc/notes.md` du scratchpad, scripts `sim` (le modèle du moteur corrigé égale
le modèle du binaire), `real391`, `census`) ; les routines à période sont le **type 0** (`0x8005CB38`) et le **type 2**
(`0x8005D05C`, appelée en `0x8005CAC8`) ; le type 1 (`0x8005CE44`) n'a pas de période et aucune cellule n'en est :
- **Période** : le compteur s'incrémente à chaque tick ; le pas s'applique quand `|P| <` le compteur d'avant l'incrément
  (`0x8005CC64`/`0x8005CCBC` pour le type 0, `0x8005D218`/`0x8005D278` pour le type 2), puis le compteur repasse à 0 : **un pas
  tous les `|P| + 2` ticks** ; `|P|` par `lb` puis `negu` ; compteurs remis à 0 au chargement du fond (`0x8005B47C`/`0x8005B480`
  dans `0x8005B1E8`) ; tables des pas (`0x801802E0` en X, `0x80180920` en Y, remplies en `0x8005C19C`-`0x8005C23C`) : −1 si D < 0 ou
  P < 0, +1 sinon, 0 si P = 0, comme `ComputePeriodStepOr` du moteur. Le défilement n'est pas touché (`0x8005C7E0` : `old < |P| - 1`,
  tous les `|P|` ticks, comme `ScrollingLayerService`). La période va de l'octet signé brut au moteur sans transformation
  (analyseur `ScrollScreen.cs:527/529`, convertisseur `BackdropReader.cs:357/359` → `BackdropWriter.cs:253`, DLL
  `BackdropDocument.cs:48/50` → `AlundraBackdropStage.cs:374/376`). Le moteur avance tous les `|P|` ticks (`++Tick >= |P|`,
  `CasaEngine/Framework/Rendering/CellularLayers/CellularLayerService.cs:303`, `:313`, `:364`, `:371`).
- **Position dessinée** : le binaire dessine `sx`/`sy` calculés **avant** les bouclages et la réapparition (type 0 : `0x8005CCFC`/
  `0x8005CD00`, écrits en `0x8005CDF0`/`0x8005CDF4` ; type 2 : `0x8005D2C0`/`0x8005D2C4`, écrits en `0x8005D3AC`/`0x8005D3B0`) ; les
  bouclages ne changent que la position rangée : au tick d'un bouclage la cellule est hors de l'écran. Le moteur dessine après
  (`:349-350`, `:403-404`, recalculs `:328/333/341/346`, `:385/390/399-400`) : une cellule bouclée apparaît un tick trop tôt au bord
  opposé (à la 391, environ 3 bouts de pluie par image au bord haut).
- **Tirage de la réapparition** : la cellule de type 2 qui passe `sy >= 240` (`0x8005D310`) tire **une fois** le `rand()` de la
  bibliothèque C (`0x80081E6C` : `s = s × 0x41C64E6D + 0x3039`, rend `(s >> 16) & 0x7FFF`) et pose `posX = rand() / 102` (division
  signée tronquée, `0x8005D324`-`0x8005D340`, 0 à 321) ; état `0x801EEB48` dans la BSS remise à 0 au lancement (`0x8008B548`), jamais
  réensemencé (`srand`, `0x80081E9C`, sans appelant) ; trois appelants en tout (la chute des cellules et deux écritures du bloc de
  carte mémoire, O-E19-49). La DLL branche la réapparition sur `AlundraRandom` (`Alundra/Scripts/AlundraWorldProxy.cs:652`, D7 de
  `docs/plan-e9d-mode-cellulaire.md:245`) et le moteur calcule `(u32 × 320) >> 32` (`:397`, 0 à 319). Les cartes à cellules de type 2
  (31 et 391) n'atteignent aucun autre consommateur d'`AlundraRandom`.
- **Corpus** : 92 couches cellulaires ; type 0 : 450 cellules dans 25 cartes, aucune sur la chaîne ; type 2 : 110 cellules (55 traits
  de pluie d'un pixel) dans les cartes 31 et **391** (sur la chaîne, le navire dans la tempête) ; type 4 (vagues) : 7800 cellules dans 65
  cartes, dont 476 et 478, non touchées ; toute cellule de type 2 a une période 0 et un DX 0 : la règle de période ne change pas la pluie.

**Règles.**
- **M2-R1 — Période (moteur)** : aux quatre endroits, le pas s'applique quand le compteur d'avant l'incrément dépasse `|P|`, puis le
  compteur repasse à 0 (`if (cell.TickX++ > Math.Abs(PeriodX)) { ... TickX = 0; }`, de même en Y) ; doc de `CellularCellDefinition.cs:46-49`
  et `docs/engine/cellular-layers.md` §4.
- **M2-R2 — Position dessinée (moteur)** : `DrawX` reçoit `sx` juste avant le bouclage en X, `DrawY` reçoit `sy` juste avant le
  bouclage en Y ou la réapparition ; les recalculs d'après sont retirés.
- **M2-R3 — Réapparition (moteur)** : `cell.PosX = (int)next() / 102` ; le délégué garde sa signature `Func<uint>` et sa doc dit
  « la prochaine valeur de `rand()` de la bibliothèque C, 0 à 0x7FFF » (`CellularLayerService.cs:22-25`, `:210-216`,
  `CellularLayerComponent.cs:45-59`, le texte d'avertissement `:69-71`, `cellular-layers.md` §5 et §12, ce dernier déjà périmé : il dit
  « throws », le code avertit une fois et rend 0) ; ADR du moteur (ADR-0050).
- **M2-R4 — Générateur de la bibliothèque C (DLL)** : nouveau générateur statique `AlundraLibcRandom` (état 0 au lancement du
  processus, jamais remis à zéro en production, pas de `srand` ; `Next()` = la formule du binaire ; un accès `internal` à l'état pour
  les tests) ; `AlundraWorldProxy.cs:652` branché dessus ; doc d'`AlundraRandom.cs:5-19` corrigée ; D-E19-66 remplace le choix du flux
  de D7 (le partage moteur sans générateur, DLL maîtresse du flux, reste) ; ADR-0032 du parent. Les sauvegardes de la DLL ne tirent
  rien (O-E19-49, écart consigné dans l'ADR jusqu'à la décision de l'auteur).

**Tâches.**
- **M2-1 — Moteur, tests d'abord** (sous-module, branche `chantier/e19m2-cellular-binary` **empilée sur** `chantier/e19k2-layer-mask`
  `987f0c7f` ; `CasaEngine.Launcher/Program.cs` de l'auteur jamais indexé) ; un tick à la fois, valeurs d'aujourd'hui écrites pour le
  rouge :
  - T-P1 : cellule normale, x0 100, u1 1000, période X 1 → `DrawX` 100, 101, 102 aux ticks 1, 3, 6 (aujourd'hui 101, 103, 106) ;
  - T-P2 : cellule normale, y0 100, v1 1000, période Y −2 → `DrawY` 100, 99, 98 aux ticks 2, 4, 8 (aujourd'hui 99, 98, 96) ;
  - T-P3 : `FallRespawn`, x0 100, u0 = u1 = 0, période X 2 → `DrawX` 100, 101, 102 aux ticks 2, 4, 8 (aujourd'hui 101, 102, 104) ;
  - bouclages (`CellularLayerServiceTests.cs:132-192`, réécrits) : au tick 1 la position d'avant le bouclage, au tick 2 la bouclée :
    x0 −20 → −20 puis 315 (aujourd'hui 315 dès le tick 1) ; x0 400 → 400 puis 65 ; y0 −20 → −20 puis 235 ; y0 400 → 400 puis 145 ;
  - T-W5 : x0 −13, dx −1 → ticks 1 à 4 : −14, −15, −16, 318 (aujourd'hui −14, −15, 319, 318) ;
  - T-F1 (remplace `:196-219`) : `FallRespawn` x0 7, y0 232, dy 8, u1 = v1 = 15, source qui rend 16320 → tick 1 (7, 240), un appel ;
    tick 2 (160, −7) ; tick 3 (160, 1) (aujourd'hui (0, −15) au tick 1) ; T-F2 : même cellule, source 32767 → (7, 240), (321, −7),
    (−14, 1) ; bornes : 32640 → 320 ;
  - `:111-128` réécrit : 12 ticks, 87 (89 aujourd'hui avec 10 ticks ; 87 aussi aujourd'hui avec 12 : le rouge vient de T-P1 à T-P3).
- **M2-2 — Moteur, code** : M2-R1 à M2-R3, docs, ADR-0050.
- **M2-3 — DLL, tests d'abord** (collection des tests qui touchent un état statique aléatoire) : `AlundraLibcRandom` depuis l'état 0
  rend 0, 21468, 9988, 22117, 3498 (états `0x00003039`, `0xD3DC167E`, `0xA70427DF`, `0xD6651C2C`, `0x0DAA96F5`) ; depuis `0x12345678`
  rend 2929, état `0x0B719151` ; `AlundraWorldProxyCellularRandomWiringTests.cs:26-51` réécrit : état `0x12345678`, `RandomSource()`
  rend 2929u, état `0x0B719151`, `AlundraRandom.RandomSeed` inchangé (`0xB017C93D`) ; sur données réelles (sauté si l'export manque) :
  les couches de la 391 construites comme les construit `AlundraBackdropStage` (`:374-376`), générateur à l'état 0, caméra 0, trois
  ticks : cellule 2 → (214, 249), (0, −46), (0, −38) ; cellule 30 → (23, 264), (210, −17), (210, 5) ; cellule 47 → (47, 247),
  (97, −26), (97, 4) ; état après le tick 1 `0xA70427DF`.
- **M2-4 — DLL, code** : M2-R4, pointeur du sous-module, ADR-0032.

**Acceptation.**
1. Tests de M2-1 et M2-3 rouges d'abord (valeurs d'aujourd'hui), verts après, valeurs écrites tenues ; une valeur que la mesure
   contredit est un arrêt.
2. Tests existants touchés, liste fermée : `CellularLayerServiceTests.cs:111-128`, `:132-192`, `:196-219` ;
   `AlundraWorldProxyCellularRandomWiringTests.cs:26-51` ; rien d'autre (`CellularLayerMaskTests`,
   `CellularLayerComponentSubmissionTests`, le test de remise à zéro `:359-381` inchangés).
3. `CasaEngine.Tests` construit à part et vert ; `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart ; les
   six traces à l'octet ; la liste fermée de la chaîne inchangée.

**Retour arrière** : pointeur du sous-module et revert du commit de la DLL.

Decisions: see ADR-0032 (moteur : ADR-0050).

- **Fait le 2026-10-03** (M2-1 à M2-4 ; moteur : branche `chantier/e19m2-cellular-binary` empilée sur `chantier/e19k2-layer-mask`
  `987f0c7f`, plan `ai-agent/tasks/e19m2-cellular-binary-tasks.md`, commits `3adb1480` (plan), `78ffdfac` (tests, code, docs),
  `61358ac0` (ADR-0050) ; parent : `593a2fd` (DLL et pointeur), `a08e863` (ADR-0032)). **Rouges d'abord**, valeurs lues égales aux
  valeurs « aujourd'hui » du plan. Moteur, 13 tests rouges sur les 26 de `CellularLayerServiceTests` (22 `[Fact]` et les 4 cas de la `[Theory]` ; la puce disait 55 par erreur, corrigé par E19.m5) : T-P1 tick 1 attendu 100 lu 101 ;
  T-P2 tick 2 attendu 100 lu 99 ; T-P3 tick 2 attendu 100 lu 101 ; bouclages au tick 1 attendu −20 lu 315, attendu 400 lu 65, `DrawY`
  attendu −20 lu 235, attendu 400 lu 145 ; T-W5 tick 3 attendu −16 lu 319 ; T-F1 tick 1 attendu (7, 240) lu (0, −15) ; T-F2 tick 2 :
  source 32767 attendu 321 lu 0, 32640 attendu 320 lu 0, 102 attendu 1 lu 0 (101 → 0 vert d'avance) ; le test réécrit `:111-128`
  (12 ticks, 87) était vert d'avance, comme annoncé. DLL (le type posé en talon qui rend 0, état sans effet) : générateur depuis
  l'état 0 attendu `[0, 21468, 9988, 22117, 3498]` lu `[0, 0, 0, 0, 0]` ; depuis `0x12345678` attendu 2929 lu 0 ; câblage attendu
  2929 lu 904094096 (`0x35E36190`, la première valeur d'`AlundraRandom`) ; 391 : les positions du tick 1 sont celles d'avant le tirage
  (identiques), le rouge est l'état après le tick 1, attendu `0xA70427DF` lu 0. **Verts après**, toutes les valeurs écrites tenues,
  aucune ré-épinglée. Nombres : `CasaEngine.Tests` 2511 sur 2511 (Debug, 2502 avant, +9) ; `Alundra.Tests` Debug 2555 sur 2555 (2552
  avant, +3 : `AlundraLibcRandomTests`, collection `AlundraRandomStaticStateCollection`, test de la 391 sauté si l'export manque).
  Tests existants touchés, liste fermée respectée : `CellularLayerServiceTests.cs:111-128`, `:132-192`, `:196-219` (remplacé par
  T-F1/T-F2) et `AlundraWorldProxyCellularRandomWiringTests.cs:26-51` ; rien d'autre. Six traces à l'octet (`git diff
  --ignore-cr-at-eol --exit-code` rend 0), quatre traces du héros remises par `git checkout --` ; la liste fermée de la chaîne
  inchangée. Écarts : le moteur est livré en un commit (tests, code et docs) au lieu de deux : séparer la période et la position
  dessinée du tirage laissait des tests rouges ; la Release et le `cmp` restent à la session principale. Doc corrigée :
  `AlundraRandom.cs`, `CellularCellDefinition.cs`, `CellularLayerComponent.cs` (avertissement), `cellular-layers.md` §4, §5, §12
  (le délégué non câblé avertit une fois et rend 0, il ne lève plus dans la doc). Le titre de la tranche E19.m (`§1.2s`) dit encore
  « E19.m2 planifiée ».

**Risques.** Les cellules de type 0 hors de la chaîne qui n'avancent que par leur période ralentissent (cartes 96 à 99, 271, 289, 357,
481 : 1,25 à 2 fois plus lentes ; les autres de 1 à 29 %) ; la pluie de la 391 change de positions (et peut atteindre 320 ou 321) ;
`AlundraRandom` n'avance plus d'environ 3 tirages par tick à la 391 (les sites qui y puisent tirent d'autres valeurs qu'avant) ;
O-E19-51 à O-E19-53 (autres écarts relevés) hors tranche.
- ✅ **Vérification d'E19.m2** (2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2555/2555
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet. Vérificateur neuf **CONFIRMED** (binaire relu en
  `0x8005CC30`-`0x8005CCE8`, `0x8005D1DC`-`0x8005D2A8`, `0x8005D31C`-`0x8005D340` et `0x80081E6C` ; rouges rejoués en remettant les
  fichiers de production : 13 rouges côté moteur aux valeurs « aujourd'hui », 4 côté DLL ; `CasaEngine.Tests` 2511/2511 ; seule la
  liste fermée des tests existants a bougé ; ADR-0050 et ADR-0032). Avis P4 : la puce « Fait » compte 55 tests dans
  `CellularLayerServiceTests` là où le filtre du vérificateur en lance 26 (le nombre de rouges, 13, concorde) ; O-E19-49 n'est
  consigné que dans ADR-0032 (écart côté DLL, conforme à M2-R4). Reste la recette : la pluie de la 391 et les cellules de type 0.

#### 1.2s.4 E19.m3 — Compteur des vagues, parallaxe et ordre des cellules (O-E19-51 à O-E19-53) ✅ (relecture n°1 REVISE, révisée ; relecture n°2 READY ; faite et CONFIRMED le 2026-10-03)

**Faits** **[binaire]** (audit du 2026-10-03, `e19m3-disc/notes.md` du scratchpad, scripts `values`, `census3`, `otusers`, `stp` ;
lignes du moteur citées à `61358ac0`) :
- **O-E19-51, compteur des vagues** : `0x800C48C4` est un mot unique pour les deux couches ; trois instructions seulement y touchent
  (`0x8005B6D8` lecture, `0x8005B6E8` écriture, `0x8005D448` lecture par les cellules de vague, type 4) ; le pilote des fonds
  `0x8005B670` l'incrémente une fois par tick de la boucle principale (appelé seulement par `RenderScene`, `0x8002BE18`, dans la boucle
  `0x8002C3F4`-`0x8002C45C`), **après** le test `Infos.Enabled` de la carte (`0x8005B6AC` : une carte sans fond ne le fait pas
  avancer) et **avant** le programme de palettes et les deux tests du masque (`0x8005B700`, `0x8005B72C`) ; la boucle de transition ne
  l'avance pas ; il vaut 0 au lancement (donnée initialisée de l'image, décalage de fichier `0xA50C4`, hors de la zone mise à zéro) ;
  rien ne le remet à 0 (ni `0x8005B2E0`-`0x8005B3B0` au chargement, ni `0x8005D668`) ; la formule n'use que `(compteur × k) & 0xFF`.
  Le moteur garde un compteur par couche (`CellularLayerService.cs:73`, `:258`, `:561`), remis à 0 au chargement et figé quand la
  couche est masquée (repris de la décompilation, `GraphicManager.cs:1011`). Le fait d'E19.k2 « l'état par tick d'une couche (cadence
  d'animation, défilement automatique, vagues et tirages aléatoires du cellulaire) est **dans** l'appel gardé » et ADR-0049 du moteur
  (`:15`, `:26`) se trompent pour les **vagues seulement** ; le reste (cadence, positions et compteurs de période, tirages, défilement,
  « non dessinée ») tient. Cartes à vagues sur la chaîne : 44, 362, 476, 478 (phase de départ différente, sans autre effet) ; la seule
  couche cellulaire masquée par `0xA4` est à la 475, hors de la chaîne. La phase exacte de l'original dépend de tous les ticks depuis
  le lancement et n'est pas atteignable (la DLL compte aussi les ticks des fondus de passage, O-E19-55 ; corrigé par E19.m4).
- **O-E19-52, parallaxe du type 0** : le facteur est calculé une fois, en division entière signée tronquée (`0x8005C0AC`-`0x8005C158`),
  puis `cam × facteur` à chaque tick (`0x8005CB78` en X, `0x8005CBB8` en Y) ; le type 2 calcule `cam × Num / Den` à chaque tick
  (`0x8005D0C4`/`0x8005D0D4`, `0x8005D13C`/`0x8005D14C`), comme le moteur ; le type 4 n'a pas de terme de caméra. Le moteur applique
  `ComputeCameraBase` aux cellules normales (`:327-328`). Effet : cartes 123 et 124, couche 1, 30 cellules par carte (des points
  opaques de 1 × 1) fixes à l'écran dans l'original, qui défilent aujourd'hui à la moitié de la caméra ; hors de la chaîne.
- **O-E19-53, ordre de dessin** : chaque cellule (type 0 `0x8005CE08`, type 1 `0x8005D008`, type 2 `0x8005D3C4`, type 4 `0x8005D5AC`) est
  insérée en tête d'un même créneau de la table d'ordre, puis la primitive de mode de la couche par-dessus (`0x8005B998`-`0x8005B9E4`) :
  **la cellule 0 est dessinée en dernier, dessus** ; l'ordre entre couches du moteur est déjà le bon. Le moteur donne une clé par couche
  (`CellularLayerComponent.cs:209`) puis `List.Sort` (`SpriteRendererComponent.cs:399`) : jusqu'à 16 entrées l'ordre de soumission
  est gardé (la dernière cellule dessus, l'inverse de l'original), au-delà il n'est pas défini. Effet sur la chaîne : la 391 (traits de
  pluie qui se chevauchent : 472 ticks sur 600 ont des pixels qui dépendent de l'ordre, jusqu'à 462 pixels par tick).

**Règles.**
- **M3-R1 — Compteur des vagues (moteur)** : un seul octet au niveau du service remplace le compteur par couche ; il avance de 1 à
  chaque tick, avant la boucle des couches, dès que `SetLayers` a été appelé depuis le dernier `Clear`, **même avec une liste vide**
  (révision n°1 ; l'équivalent moteur de `Infos.Enabled` : la DLL vide toujours, puis appelle `SetLayers` pour toute carte qui a un
  fond, même sans couche cellulaire, `AlundraBackdropStage.cs:202-223`, `BackdropWriter.cs:117`) ; un service neuf et `Clear` ferment
  la porte ; `SetLayers`, `Clear` et `ResetLayerRuntimeState` ne le remettent jamais à 0 ; le masque ne le fige pas ;
  `TryGetLayerState` le rapporte (`:510`) ; aucune nouvelle API publique ; ADR-0052 du moteur, qui amende ADR-0049 (vagues
  seulement) ; le fait d'E19.k2 reçoit une note de correction.
- **M3-R2 — Parallaxe (moteur)** : cellules normales seulement : `den != 0 ? cam × (num / den) : 0` en X et en Y (division entière
  tronquée) ; `FallRespawn` garde `ComputeCameraBase` ; docs `CellularCellDefinition.cs:35-36` et `cellular-layers.md:61`.
- **M3-R3 — Ordre (moteur)** : une clé par cellule, bâtie dans la boucle des cellules avec `LocalSortOffset = −c` (même procédé que
  `AnimatedSpriteComponent.BuildPartSortKey`) : la cellule 0 dessinée en dernier ; aucun tri stable. L'ordre entre couches de
  (passe, `SortingLayer`, `OrderInLayer`) distincts est inchangé (révision n°1) ; deux couches qui partagent ces trois champs voient
  désormais leurs cellules entrelacées par indice de cellule (`RenderSortKey2D.CompareTo` compare `LocalSortOffset` avant
  `StableId`, `RenderSortKey2D.cs:78-84`) : cas que la DLL ne construit pas (`OrderInLayer = DepthOrder`, 1 pour la couche 0 et 0
  pour la couche 1, `BackdropReader.cs:289`) ; ADR-0052 le dit.

**Tâches.**
- **M3-1 — Moteur, tests d'abord** (branche du sous-module **empilée sur** celle de G2a, `chantier/e19g2a-psx-semi`, une fois G2a
  faite ; plan du moteur dans son `ai-agent/tasks/`) ; valeurs d'aujourd'hui écrites pour le rouge (montage : table des vagues
  `lut[i] = i`, `BWavePhase` 1, `BWaveWeight` 128, `X0 = Y0 = 50`, donc `DrawX = 42 + compteur`) :
  - recharge : 3 ticks, `SetLayers` de nouveau, 1 tick → compteur 4, `DrawX` 46 (aujourd'hui 1, 43) ; 2 ticks, `Clear`, 3 ticks,
    `SetLayers`, 1 tick → 3, 45 (aujourd'hui 1, 43) ; 5 ticks, remise à zéro de l'état, 1 tick → 6, 48 (aujourd'hui 1, 43) ; deux
    couches, la couche 0 masquée 2 ticks, puis 1 tick → les deux à 3, 45 (aujourd'hui 1, 43 et 3, 45) ; test du masque avec la table :
    tick de reprise → 5, 47 (aujourd'hui 2, 44) ; liste vide (révision n°1) : `SetLayers(L)`, 1 tick, `SetLayers([])`, 4 ticks,
    `SetLayers(L)`, 1 tick → 6, 48 (aujourd'hui 1, 43) ; après un `Clear`, la table des vagues est reposée (`SetWaveLut`) avant le
    `SetLayers` suivant ;
  - parallaxe (cellule normale en (100, 100), un tick) : facteur 1/2, caméra (100, 60) → (100, 100) (aujourd'hui (50, 70)) ; 3/2,
    caméra (10, 10) → (90, 90) (aujourd'hui (85, 85)) ; −1/2, caméra (10, 10) → (100, 100) (aujourd'hui (105, 105)) ; gardes : 1/1
    inchangé ; `FallRespawn` 1/2, caméra (100, 60) → (50, 70) inchangé ;
  - ordre : trois cellules → `LocalSortOffset` 0, −1, −2 et des `CompareTo` strictement ordonnés (aujourd'hui tous 0) ; cellules
    `[Normal, ScriptTrack, Normal]` → décalages 0 et −2 ; garde : couche d'identifiant 0 à `OrderInLayer` 1 et couche 1 à
    `OrderInLayer` 0, trois cellules chacune → toute clé de la couche 1 se trie avant toute clé de la couche 0 ;
    après le tri (méthode de remplissage de G2a) l'ordre 2, 1, 0 (aujourd'hui 0, 1, 2).
- **M3-2 — Moteur, code** : M3-R1 à M3-R3, docs, ADR-0052 ; pointeur du sous-module ; note de correction au fait d'E19.k2 dans le plan.

**Acceptation.**
1. Tests de M3-1 rouges d'abord (valeurs d'aujourd'hui), verts après, valeurs écrites tenues ; une valeur que la mesure contredit est
   un arrêt.
2. Tests existants touchés, liste fermée : `CellularLayerMaskTests.cs:93` (réécrit : cellules et cadence figées, compteur 4), `:101`
   (2 → 5), `:122` (1 → 3), `:133` (0 → 2) ; `CellularLayerServiceTests.cs:487` (0 → 5) ; rien d'autre (`CellularLayerMaskTests.cs:82`
   et `CellularLayerServiceTests.cs:110`, `:115`, `:117`, `:134` ne bougent pas ; côté DLL, rien).
3. `CasaEngine.Tests` construit à part et vert ; `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart ; les
   six traces à l'octet.

**Fait le 2026-10-03** (moteur : branche `chantier/e19m3-cellular-order`, partie de `chantier/e19g2a-psx-semi` `b5a9fbcf`, commits
`3b15324c` plan, `159eb699` compteur, `58ef506f` parallaxe, `bfc36929` ordre, `a885f226` docs et ADR-0052 ; DLL inchangée) :
- **Rouges d'abord, valeurs lues égales aux valeurs d'aujourd'hui écrites** : recharge, `Clear`, remise à zéro de l'état, liste
  vide et couche 0 masquée donnent compteur 1 et `DrawX` 43 (attendu 4/46, 3/45, 6/48, 6/48, 3/45) ; reprise du masque avec la
  table 2 / 44 (attendu 5 / 47) ; `CellularLayerMaskTests.cs:93` attendu 4 lu 1, `:122` attendu 3 lu 1, `:133` attendu 2 lu 0,
  `CellularLayerServiceTests.cs:487` attendu 5 lu 0 (`:101` non atteint tant que `:93` échoue, valeur d'aujourd'hui 2) ;
  parallaxe (50, 70), (85, 85), (105, 105) au lieu de (100, 100), (90, 90), (100, 100) ; ordre : décalages 0, 0, 0 au lieu de 0, −1, −2,
  seconde entrée de `[Normal, ScriptTrack, Normal]` à 0 au lieu de −2, ordre après `FillVertices` 0, 1, 2 (centres x −52, −42, −32)
  au lieu de 2, 1, 0. Les gardes (1/1, 2/1, `FallRespawn`, Den 0, ordre entre couches) étaient verts d'avance.
- **Verts après** : aucune valeur écrite changée. Nouveaux tests : `CellularLayerWaveCounterTests` 6, `CellularLayerParallaxTests` 7
  cas, `CellularLayerCellOrderTests` 4 ; les cinq assertions de la liste fermée (dont `:93` réécrit : cadence, cellules et tirages
  figés, compteur 4) ; aucun autre test existant touché. `CasaEngine.Tests` 2550 sur 2550 (Debug) ; `Alundra.Tests` Debug 2555 sur
  2555 ; garde d'octets des six traces à 0, les quatre traces du héros remises par `git checkout`.
- **Écarts** : le test de reprise du masque avec la table utilise une couche à une seule cellule de vague (et non `MakeLayer(0)`
  de la suite du masque, dont `BWavePhase` est 0) ; le test d'ordre après tri lit le centre de chaque entrée (`x − 160 + 8` pour
  une cellule de 16 de large) ; la Release n'est pas lancée ici (session principale, à la clôture) ; les lignes O-E19-51 à O-E19-53
  du tableau des points ouverts ne sont pas marquées réglées.
- Decisions: see ADR-0052 (moteur).

**Retour arrière** : pointeur du sous-module.

**Risques.** Les vagues de la chaîne (44, 362, 476, 478) commencent à une autre phase ; la clé par cellule ajoute un champ par
entrée de la couche (même coût que les parties d'animation).
- ✅ **Vérification d'E19.m3** (2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2555/2555
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet. Vérificateur neuf **CONFIRMED** (binaire relu en
  `0x8005B6AC`-`0x8005B72C`, `0x8005D448`, `0x8005C0C4`-`0x8005C158`, `0x8005CB78`/`0x8005CBB8`, `0x8005CE08`-`0x8005CE2C` ; rouges
  rejoués en remettant les quatre fichiers de production : 16 rouges aux valeurs « aujourd'hui », gardes vertes ; `CasaEngine.Tests`
  2550/2550 ; seule la liste fermée a bougé ; ADR-0052, statut d'ADR-0049, note au fait d'E19.k2). Avis P4 : le corps d'ADR-0049
  cite encore le compteur des vagues (seul le statut renvoie à ADR-0052, conforme au contrat) ; dénominateur nul : le binaire garde
  l'ancien facteur (`0x8005C0B4`), le moteur pose 0 (choix du plan ; aucun cas dans le corpus).

#### 1.2s.5 E19.m4 — Fonds figés pendant le départ d'un passage (O-E19-55) ✅ (relecture n°1 REVISE, révisée ; relecture n°2 READY ; faite et CONFIRMED le 2026-10-03)

**Faits** **[binaire]** (audit du 2026-10-03, `e19o55-disc/notes.md` du scratchpad, scripts `census`, `values`, `x2` ; DLL citée à
`f596546`, moteur à `b5a9fbcf`) :
- Pendant un départ, la boucle de transition (`0x8002C490`-`0x8002C4C0`) n'appelle que `UpdatePads` (`0x8002E38C`), le tick des
  effets (`0x80044440`), le flux du son (`0x8004B1D4`), la fin d'image (`0x80042798`) et une fonction vide (`0x800815E4`) : jamais
  `RenderScene` ni `Update`. Le pilote des fonds, le suivi de la caméra, le dessin des entités, l'interface et le post-traitement ne
  sont atteints que par `RenderScene` : rien des fonds n'avance (cadence, défilement automatique, cellules, tirages `rand()`, compteur des
  vagues `0x800C48C4`, programme de palettes). L'écran montre une copie figée de la dernière image (`StartWarpTransition` `0x80044320`
  la copie par `MoveImage` en `0x8004434C` ; chaque tick du type 0, `0x800435E0`, en pose une copie sous la tuile du fondu) ; 16
  itérations.
- Arrivée : le chargement fait `Update(1)` (`0x8002C3E4`) sans rendu ; le fond de la nouvelle carte avance pour la première fois au
  premier `RenderScene` (`0x8002BE18`), au même tick que le premier pas du fondu d'arrivée (`0x8002BE44`) : le portage fait de même.
- Correspondance : l'image j du portage = (`Update` n° j puis `RenderScene` j) du binaire, `Update(1)` étant l'image 1
  (`docs/plan-camera-premiere-frame.md:37-38`) ; un départ est armé dans `Update` n° u (portail `0x800314FC`, opcode `0x53`
  `0x8003EC80`), donc `RenderScene` u ne tourne jamais : **aucun tick des fonds de l'image d'armement F0 à F15**.
- Le portage pousse les ticks sans condition (`Alundra/Scripts/AlundraWorldProxy.cs:2196`, commentaire `:2192-2195`) ; toute source de
  départ (portail dans la mise à jour du héros, `0x53`, chargement F9) pose `IsTransitionInProgress` avant cette ligne, dans la même
  image ; les services du moteur avancent alors (`ScrollingLayerComponent.cs:54-57`, `CellularLayerComponent.cs:85-87`) ; le fondu se
  pose à F15 (`AlundraWarpDirector.cs:614-621`) : 16 ticks de fond en trop par départ. Une image à 0 tick ne change aucun état des deux
  services. Effet sur la chaîne : la pluie de la 391 continue de tomber pendant le départ vers la 416 (7 à 31 pixels par tick, le cas le
  plus visible) ; mer de la 389 et de la 416 (1 à 4 pixels) ; vagues de la 476 et de la 478 ; jours 3-4 : 14, 15, 162, 169, 176, 183
  (mer), 44 et 362 (vagues) ; avec E19.m3 (compteur des vagues global), 16 ticks de vague en trop par départ.

**Prérequis et base** (révision n°1) : aucune dépendance de code envers E19.m3 (les valeurs de M4-1 sont les mêmes avant et après
E19.m3 : une image à 0 tick ne change aucun état, le compteur des vagues global compris ; la 391 n'a que des cellules de type 2).
Mais les deux tranches partagent ce checkout, les sorties de `Alundra.Tests`, le `cmp` et les traces : M4-1 ne commence qu'après la
clôture d'E19.m3 (vérification CONFIRMED, Release et Debug faits, pointeur du sous-module committé), sur le commit parent qui
consigne cette clôture et le sous-module qu'il désigne (`chantier/e19m3-cellular-order` `a885f226`) ; l'acceptation 3 se mesure sur
ce pointeur ; les quatre traces du héros sont remises par `git checkout` avant de commencer et après chaque suite.

**Règle.**
- **M4-R1** : `PushFrame` reçoit 0 tick quand `AlundraWarpDirector.Instance.IsTransitionInProgress` (le même prédicat que le balancement
  de la caméra, `AlundraCameraDirector.cs:219`), `ticksThisFrame` sinon ; l'image est toujours poussée (cible de la caméra et
  défilement à jour) ; pas de garde sur `gameplayBlocked` (sous `MenuOpen` le binaire continue `RenderScene` ;
  `BackdropPushProductionTests.cs:101-121` l'épingle) ; commentaire `:2192-2195`, doc de `PushFrame` (`AlundraBackdropStage.cs:461-474`),
  note sur D-E9b-2 (`docs/plan-e9b-backdrops-moteur.md:378`), et la phrase d'E19.m3 « la DLL compte aussi les ticks des fondus de
  passage, O-E19-55 » (§1.2s.4) reçoit « (corrigé par E19.m4) » ; décision D-E19-67, ADR-0034 du parent. Exact à 1 tick par image ; une
  image de rattrapage peut laisser jusqu'à 3 ticks d'écart.

**Tâches.**
- **M4-1 — Tests d'abord** (amorce `Update(0.02f)`, puis K images, puis `BeginDeparture` comme `AlundraWarpDepartureTests.cs:239-301`,
  puis 16 × `Update(0.02f)` ; montages sans caméra : défilement (−160, −120)) :
  - T-55a (montage de la 389, deux services attachés) : `PendingTicks` après chacune des images F0 à F15 → 0 (aujourd'hui 1 dès F0) ;
    gardes : `FramesPushed` +1 à chaque image ; `InstallForMapEntry` puis un `Update` → 1 (inchangé) ;
  - T-55b (mer de la 389, couche 0, `stage.Load`, `Advance()` après chaque image, K = 9, donc t = 10 à l'armement : `AutoScroll`
    (1, 2), `Timer` (0, 0)) : après F15, `AutoScroll` (1, 2), `Timer` (0, 0) (aujourd'hui (2, 5) et (6, 1) ; après F0 seule, `Timer`
    (1, 1)) ;
  - T-55c (pluie de la 391, générateur `rand()` à l'état 0 après le chargement ; le tick d'amorce fait 17 tirages, état `0x7E7099A9`) :
    après F15, toujours 17 tirages, état `0x7E7099A9`, cellule 0 en (251, 202) (aujourd'hui 71 tirages, état `0x277C02BB`, cellule 0
    en (284, 27)) ; saute si l'export manque ; dans la collection `AlundraRandomStaticStateCollection` (il pose l'état de
    `AlundraLibcRandom`, global au processus).
- **M4-2 — Code** : M4-R1, docs, ADR-0034.

**Acceptation.** 1. Tests de M4-1 rouges d'abord (valeurs d'aujourd'hui), verts après, valeurs écrites tenues ; une valeur que la
mesure contredit est un arrêt. 2. Tests existants touchés : aucun. 3. `Alundra.Tests` en Release puis en Debug, la Debug en dernier,
`cmp` sans écart ; les six traces à l'octet ; la liste fermée de la chaîne inchangée. **Retour arrière** : revert du commit (DLL seule).

**Risques.** Le reste de la scène (suivi de la caméra, animation des entités, physique des PNJ, tuiles animées, HUD, dialogue, fondu de
la musique) avance encore pendant le départ alors que le binaire montre une image figée (O-E19-57, hors tranche).

- **Fait le 2026-10-03** (M4-1, M4-2 ; DLL seule, moteur intact, sous-module resté à `a885f226`). Nouveau fichier
  `Alundra.Tests/BackdropWarpDepartureFreezeTests.cs` (3 tests : T-55a et T-55b dans `BackdropWarpDepartureFreezeTests`, collection
  `AlundraMusicPlayerSingletonCollection` car `BeginDeparture` arme le fondu de la musique ; T-55c dans `BackdropWarpDepartureRainTests`,
  collection `AlundraRandomStaticStateCollection`, sauté si l'export manque). **Rouges d'abord** sur le code d'avant, valeurs lues égales
  aux valeurs « aujourd'hui » du plan : T-55a `PendingTicks` 1 dès F0 (attendu 0) ; T-55b après F0 `Timer` (1, 1) (attendu (0, 0)) ;
  T-55c 71 tirages après F15 (attendu 17). Les tests s'arrêtant à la première assertion fausse, les valeurs de F15 de T-55b ((2, 5),
  (6, 1), décalage (482, 365)) et l'état `0x277C02BB` de T-55c n'ont pas été lus séparément : ils restent ceux de l'audit. **Verts après**,
  valeurs écrites tenues sans écart (T-55b : auto-défilement (1, 2) et `Timer` (0, 0) après chacune des images F0 à F15, décalage
  (481, 362) ; T-55c : 17 tirages, état `0x7E7099A9`, cellule 0 en (251, 202) ; T-55a : `FramesPushed` +1 par image, 1 tick après
  `InstallForMapEntry`). Code : M4-R1 à `AlundraWorldProxy.cs` (`PushFrame(IsTransitionInProgress ? 0 : ticksThisFrame, ...)`), commentaire
  et doc de `PushFrame` mis à jour, note sur D-E9b-2 dans `docs/plan-e9b-backdrops-moteur.md`, phrase d'E19.m3 (§1.2s.4) « corrigé par
  E19.m4 ». Suite complète `Alundra.Tests` en Debug : 2558 réussis, 0 échec, 0 ignoré (aucun test existant touché) ; Release, `cmp` et
  vérification : session principale. Garde d'octets des six traces : `git diff --ignore-cr-at-eol --exit-code` rend 0 ; les quatre traces
  du héros ont été remises par `git checkout` ; liste fermée de la chaîne inchangée. Décisions : D-E19-67. Decisions: see ADR-0034.
- ✅ **Vérification d'E19.m4** (2026-10-03) : la session principale a lancé `Alundra.Tests` en Release puis en Debug (2558/2558
  chacune, la Debug en dernier), `cmp` sans écart, les six traces à l'octet. Vérificateur neuf **CONFIRMED** (parcours d'appels
  transitif depuis la boucle de transition : 133 fonctions, ni `RenderScene` (`0x8002BD60`), ni `Update` (`0x8002BAEC`), ni le pilote
  des fonds ; témoin positif depuis `RenderScene` ; rouges rejoués : T-55a 1, T-55b (1, 1) à F0 et (2, 5)/(6, 1)/(482, 365) à F15,
  T-55c 71 tirages, état `0x277C02BB`, cellule 0 en (284, 27), toutes égales au plan ; isolement : `parallelizeTestCollections` est
  à `false`, aucune course possible ; 4 passes complètes sans test instable). Avis P4 : ADR-0034 cite D-E9b-2 à la ligne 378, la
  note est aux lignes 388-389 ; quelques sauts indirects des bibliothèques PsyQ ne sont pas résolus par le parcours.

#### 1.2s.6 E19.m5 — Hygiène des tests (G1) et deux commentaires (G2a) ✅ (relectures n°1 et n°2 REVISE ; disposition FIX du contrôle de M-38 ; nouvelle époque, relecture de clôture READY ; faite le 2026-10-05, vérification à venir)

**Faits** (découverte du 2026-10-05, `e19m5-disc/notes.md` du scratchpad, et vérification adverse, `e19m5-verify/verify.md`) : aucun
fichier de test du périmètre n'a changé depuis le recensement (`bafbd5a`) ; toutes les lignes sont relocalisées à `0d5849f`. Le code de
production est tenu pour juste : le rouge d'abord se fait **par mutation scriptée** d'une ligne de production nommée, que la nouvelle
assertion doit tuer alors que les tests existants la laissent passer.

**Points retenus.**
- **M-14** (`0x1C` relance et drapeau d'apparition) : nouveau `UJ10b_…` dans `AlundraAnimationImpulseTests.cs` (après UJ-10, `:215-253`,
  même programme `01 1A 03 1C 01 1C 01 FF`, même montage, `RelaunchRunner` `:193-213`) : juste après la mise à jour 23, poser
  `SpawnAnimationActive = true` et `SpawnAnimationId = JumpNpcRig.ImpulseAnimation` (le geste d'UJ-6b) ; affirmer : drapeau encore vrai
  après la mise à jour 31 ; **`impulseUpdates[1] == 32`** (la seule assertion qui tue la mutation : « drapeau faux après 32 » est aussi
  vrai sous elle) ; le vol `Flight1360[i] == posZ[31 + i]` pour i de 0 à 21. Mesurer d'abord le `impulseUpdates[1]` d'UJ-10 (32 attendu ;
  une contradiction est un arrêt). Mutation : supprimer `AlundraEventProgramRunner.cs:2511` → l'impulsion vient à la mise à jour 63.
  Le commentaire du test dit que le montage pose le drapeau dans un état que la production n'atteint pas (il tombe à la première
  validation).
- **M-22** (`UiDialogueBoxTests.cs:84`, message qui suppose une largeur de 128) : `FirstDifference(expected, actual, width)`, appel
  `:67` ; nouveau `[Fact]` : octet 470 d'une boîte de 112 de large → `"470 (pixel 5, 1, channel 2): expected 0, got 1"` ; mutation :
  le diviseur remis à 128 → `pixel 117, 0`.
- **M-24** (`0x58` lit l'entité logique) : nouveau test dans `AlundraClassAndBranchOpcodesTests.cs` : entité de direction 1, héros
  (`Context.PlayerEntity`) de direction 2 → `TargetAnimationId` 2, `CodeIndex` 14 ; mutation : `:1047` lit le héros s'il existe → 3
  et 17 (aucun test unitaire ne la tue aujourd'hui ; l'arc A11 n'est pas établi).
- **M-28** (`0x82` d'identifiant 0) : nouveau test dans `AlundraItemAndMoneyOpcodesTests.cs` avec des tables où l'objet 0 a un maximum
  de 1 (`rows[0] = {0, 0, 0, 1, 65535}`, `ItemTablesFixture`) ; précondition : `AddOneItemIfUnlocked(sonde, tables, 0) == 1` ; puis
  `82 00 1A 09 FF` → `Result` 0, aucun objet ni argent écrit ; mutation : supprimer `case 0: return false;` (`:2736-2737`) → `Result` 1.
- **M-29** (`0x21`) : (1) nouveau `TZ21_TheHighByteOfTheDistanceCounts_256Px`, codes `01 21 00 01 FF` (256 pixels), Z de départ 3145728 :
  à 19922943 l'attente continue, à 19922944 elle finit ; mutation : `:2472` sans l'octet haut → finit à 19922943 ; (2) dans
  `TZ21_ACollisionAtTheFirstCallEndsAtOnce`, `Assert.Equal(3145728, state.Parameters[2])` (l'ordre des appels du binaire, `0x8003DA34`
  avant `0x8003DA48`) ; mutation : opérandes de `:725` inversés → 0 ; (3) le décalage arithmétique ou logique ne diffère qu'au
  repliement de `int.MinValue`, hors d'atteinte du jeu : **accepté**, sans test.
- **M-33** (moitié de remise à zéro) : `AlundraDestroyedEntityRecyclingTests` devient `IDisposable` et remet les quatre porteurs de
  session (motif D-T-14, `AlundraWorldProxyGlobalFreezeTests.cs:43-66`) dans son constructeur et son `Dispose` ; de même
  `AlundraBackgroundLayerMaskTests` et `AlundraWorldProxyCellularRandomWiringTests`, qui construisent un `AlundraWorldProxy` sans
  remise ; aucune assertion ne bouge. Démonstration jetable (non committée) : `MenuOpen` posé en tête du constructeur sans la remise
  de l'état du jeu → 4 rouges (`:116`, `:142`, `:156`, `:191`), 3 verts ; avec la remise, 7 verts. La moitié « verrou d'interaction sur
  un mandataire recyclé » est de la production (G4) : hors tranche.
- **M-35** : `Alundra/Scripts/AlundraHudComposer.cs:43`, « 320x236 » → « 320x240 » (binaire `0x800424AC`-`0x8004251C`, ADR-0027).
- **M-38** (le retard d'une image de la 392 attribué à D-E19-64) : le commentaire de `AlundraAnimationSoundTests.cs:281`, une section
  « Amendment » ajoutée à ADR-0028 (sa ligne 29 n'est pas réécrite, précédent d'ADR-0020) et la phrase de l'écart (2) d'E19.t dans ce
  plan (deux lignes : la coupure de ligne et l'indentation comptent) nomment la vraie cause : l'ordre de la DLL (le moteur met à jour
  les entités avant le mandataire du monde, qui fait tourner les événements de carte) ; textes exacts versionnés dans
  `docs/plan-e19-m-annexe/m5-m38-texts.md` (révision n°1), avec le contrôle après l'édition.
- **M-46** : dans `AlundraAnimationImpulseTests.cs:304-312`, `Assert.Equal(-8388608, withGravity.Npc.TickForceZ)` (binaire
  `0x80036AF4`-`0x80036B04` : ni déclin ni borne) ; mutations : borne de l'impulsion (`AlundraEntityScriptProxy.cs:690`) → -1048576 ;
  marqueur avec gravité envoyé au déclin (`:688`) → -32768 ; E19.m5 reprend ce point de l'hygiène de d2c2 (D6).

**Clos sans changement** (vérification) : **M-23** (la prémisse est fausse : le test existant est le seul à tuer une mutation qui ne
ferait que désactiver ; l'ajout ne tuerait rien de neuf) ; **M-40** (a) et (b) (les mutations citées sont déjà tuées : le test existant
de la 476, quel que soit l'ordre, et TD6). **Reportés** à une tranche d'hygiène suivante (E19.m6, découverte à faire) : M-19 (trois
sous-points hors du territoire de D5b : `AlundraPlayerManagerTests.cs:95`, `AlundraHeroJumpStatesTests.cs:264`,
`AlundraLadderClimbTests.cs:423`), M-31, la moitié « surcouche » de M-32, la duplication du contexte de `AlundraTurnOrderTests.cs` (avis
d'E19.m1). Restent où ils sont : M-16, M-17 et SJ-12 (D5b), la moitié test de M-45 (après G4). Avis des clôtures : celui d'E19.m0
(`AlundraRandom` hors de la collection) est sans objet, `parallelizeTestCollections` étant à `false` ; celui d'E19.m2 (55 au lieu de 26
tests comptés) est corrigé dans la puce d'E19.m2 par cette tranche ; ceux d'E19.m3 (corps d'ADR-0049) et d'E19.m4 (ligne citée par
ADR-0034) restent tels quels (une ADR ne se réécrit pas ; leurs statuts et ce plan renvoient au bon endroit).

**Tâches.**
- **M5-1 — Tests et mutations** : pour chaque point retenu, le test ajouté ou renforcé, vert sur la production ; puis la mutation
  scriptée de la ligne nommée (script dans le scratchpad, jamais committée), le test filtré rouge avec la valeur lue, la production
  remise (`git diff` vide sur les fichiers de production) ; la démonstration de M-33 de même.
- **M5-2 — Commentaires et docs** : M-35, M-38 (test, ADR-0028, plan), la puce d'E19.m2.

**Acceptation.**
1. Chaque mutation nommée rougit le test retenu avec la valeur écrite ; la production remise, tout est vert ; les valeurs écrites
   d'avance tiennent (une contradiction est un arrêt).
2. Tests existants touchés, liste fermée : `UiDialogueBoxTests.cs` (le helper et son appel), `TZ21_ACollisionAtTheFirstCallEndsAtOnce`
   et le test d'impulsion de `:304-312` (une assertion ajoutée chacun), les trois classes de M-33 (constructeur et `Dispose` seulement),
   `AlundraAnimationSoundTests.cs:281` (le commentaire de M-38, aucune assertion) ; rien d'autre (un `git diff --stat` de la tranche ne
   montre aucun fichier de test hors de cette liste et des nouveaux tests).
3. Tests du convertisseur ; `Alundra.Tests` en Release puis en Debug, la Debug en dernier, `cmp` sans écart (M-35 touche un fichier de
   production) ; les six traces à l'octet.

**Retour arrière** : revert des commits (tests, un commentaire de la DLL, docs).

**Risques.** UJ10b dépend du montage de `JumpNpcRig` (territoire de D5b, utilisé sans être modifié) ; l'esquisse d'E19.h2 retouchera
UJ-1b dans le même fichier ; f2a refond la boucle des événements de carte que les tests de recyclage traversent (la remise à zéro ne
déplace aucune assertion).

- **Fait le 2026-10-05** (M5-1 et M5-2 ; commits `e35cd5e` (tests) et `3d3166b` (commentaires, ADR-0028, phrase d'E19.t), puis ce plan).
  **Rouge par mutation scriptée** (script du scratchpad, jamais committé ; production remise par `git checkout --`, `git diff` vide ; pour M-22,
  dont le « code » est l'assistant du fichier de test, le fichier est remis à son contenu d'avant la mutation). Chaque test était vert
  d'avance sur la production, chaque mutation ne rougit que lui, les valeurs écrites d'avance tiennent toutes :
  - **M-14** `UJ10b_…` : suppression de `AlundraEventProgramRunner.cs:2511` → `Expected 32, Actual 63` (UJ-10, lancé avec, reste vert) ; la
    valeur 32 attendue de la mesure d'UJ-10 est tenue.
  - **M-22** `FirstDifference_LocatesThePixelInTheBoxWidth` : diviseur remis à 128 → `"470 (pixel 117, 0, channel 2): …"` au lieu de
    `"470 (pixel 5, 1, channel 2): …"`.
  - **M-24** `DirectionalBranch_0x58_WithAHeroFacingElsewhere_…` : `:1047` lit le héros s'il existe → `Expected 2, Actual 3` (les cinq autres
    tests `0x58` et `0x57` verts ; l'arc A11 n'est pas mesuré : aucun test unitaire ne tuait la mutation).
  - **M-28** `Give_0x82_Id0_AnswersZero_EvenWhenTheItem0HasAMaximum` : suppression de `case 0: return false;` → `Expected 0, Actual 1`
    (précondition `AddOneItemIfUnlocked(…, 0) == 1` tenue).
  - **M-29** `TZ21_TheHighByteOfTheDistanceCounts_256Px` : `:2472` sans l'octet haut, puis avec `<< 7` → l'attente finit à 19922943
    (`Expected [(1, 33, 0)], Actual [(1, 33, 3), (4, 255, 0)]`, les deux fois) ; assertion `Parameters[2] == 3145728` ajoutée à
    `TZ21_ACollisionAtTheFirstCallEndsAtOnce` : opérandes de `:725` inversés → `Expected 3145728, Actual 0` ; le décalage arithmétique
    ou logique : accepté, sans test.
  - **M-46** `Assert.Equal(-8388608, withGravity.Npc.TickForceZ)` : impulsion bornée (`AlundraEntityScriptProxy.cs`, règle de l'impulsion) →
    `Actual -1048576` ; marqueur avec gravité envoyé au déclin → `Actual -32768`. E19.d2c2 D6 n'a plus à reprendre ce point.
  - **M-33** : `: IDisposable`, remise des quatre porteurs dans le constructeur et `Dispose` des trois classes
    (`AlundraDestroyedEntityRecyclingTests`, `AlundraBackgroundLayerMaskTests`, `AlundraWorldProxyCellularRandomWiringTests`) ; aucune
    assertion ne bouge. Démonstration jetable (non committée) : `MenuOpen` posé en tête du constructeur de la première classe, sans la
    remise de l'état du jeu → 4 rouges (`TheCorpseIsSeen…`, `AMapProgramThatDestroysAndOpens…` `Expected FlagToDestroy, Actual Normal`,
    `ACorpseThatTheEntitiesLeft…`, `OnAnImageOfTwoTicks…`), 3 verts ; avec la remise, 7 verts.
  - **M-35** (`AlundraHudComposer.cs:43`, 320x240) et **M-38** (commentaire de `AlundraAnimationSoundTests.cs:281`, section « Amendment »
    d'ADR-0028 sans toucher à sa ligne 29, phrase de l'écart (2) d'E19.t) : les trois contrôles de l'annexe tiennent. La puce d'E19.m2 est
    corrigée (26 tests, pas 55).
  - **Nombres** : `Alundra.Tests` Debug 2570 réussis, 0 échec (dont quatre nouveaux de cette tranche : UJ10b, `0x58`, `0x82`, `TZ21` octet haut ; les autres viennent d'E19.g G2c) ;
    convertisseur 433 sur 433 (un de plus). Six traces à l'octet (`git diff --ignore-cr-at-eol --exit-code` rend 0), quatre traces du héros
    remises par `git checkout --`. La DLL de `Alundra.Tests` en Debug est identique (`cmp`) à celle d'`alundra-project/`. Liste fermée de
    l'acceptation 2 respectée. Écarts : aucun ; la Release (M-35 touche un fichier de production) reste au vérificateur.

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

### E19.d (2026-10-01)

- **Commits** (branche `chantier/e19-opcodes`) : D1 `acbb789`, D2 `c91af00`, D3 `a31689e`, plan (arrêt de D5, décisions D-E19-24 à D-E19-26) `dfd8ded` et `67f8941`, D4 `8dca230`,
  D5 `8ee80e9`, D6 `6d6647a`, D7 `d3c397a`, D8 `0a926e4`, puis cette mise à jour des docs. D0 n'a pas de commit propre (les arcs A6 et A8 entrent avec D7). Le pointeur du moteur ne
  change pas ; le moteur n'est pas touché.
- **Rouge constaté** (conforme à la table) : A6 échouait avec `slot 1 program @228: last 0x00 @479` et A8 avec `slot 1 program @60: last 0x1C @100` sur `74df40e` (D0) ; après D1, A8
  atteignait `0x11 @201` à l'image 941 puis échouait sur les opcodes sautés ; après D2, A6 passait ; après D4, A8 atteignait la fin à l'image 1025 et échouait sur `0x41 @56`,
  `0x41 @728` et `0x40 @731` seuls (revu le 2026-10-01 sur le code de D4) ; après D6, A8 passe. U1, U3 et U4 rouges sans le cas de `0x24` (revérifié) ; U5 à U13 (sans U9) : les 8 rouges
  sur le code de D4, verts du premier coup avec D6.
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sans en changer une (A5, A5r, A6, A8, TH1 à TH3, D2, U1 à U13).
- **Recensement des sites de `0x24`** (`docs/census-0x24-waits.md`, modèle corrigé par D-E19-24, quatre passes jusqu'à un tableau stable) : 429 sites dans 82 cartes ; 395 atteignables dans
  76 cartes, 34 dormants, 0 « DLL seulement » ; classes des 395 : mur trouvé 293, vitesse nulle 1, aucun mur 4, indéterminé 4, non atteint (0x0B bloqué) 30, non atteint (drapeau jamais
  posé) 63. Premier modèle, pour mémoire : 269 / 1 / 83 / 42 (aucun non atteint). Chemin de l'histoire : 59 sites atteignables, 31 « mur trouvé », 0 « aucun mur », 0 « indéterminé »,
  28 non atteints (O-E19-18, O-E19-19) ; les six sites de la 172 sont dormants. Hors du chemin : 9 sites à risque (aucun mur 4 : cartes 40 et 62 ; indéterminé 4 : cartes 40, 62 et 249 ;
  vitesse nulle 1 : Flamme de la 152), dont 1 sous main tenue, et 65 sites non atteints (O-E19-21). Sites atteints dans le modèle : 302, dont 201 sous main tenue.
- **Temps d'exécution** (Debug) : A5 environ 1 s, A5r 0,7 s, A6 0,75 s, A8 0,78 s ; la suite complète environ 26 s.
- **Suites** : `Alundra.Tests` 2086 réussis, 0 échec, en Debug (26 s) comme en Release (27 s) (2071 à la fin de D2 sans A8 : A5, A5r, U1 à U13 sans U9 et A8 font 2086) ; convertisseur 400
  sur 400. Les annexes de trace du héros et de l'intro ne changent pas. La DLL déployée dans `alundra-project/` est la Debug (`cmp` identique).

### E19.d2a (2026-10-02)

- **Commits** (branche `chantier/e19-opcodes`) : S1 `3be0d89`, S2 `62dccdd`, S3 `ad04f9c`, S4 `43b12ac`, puis cette mise à jour du plan. Aucun sous-module
  touché ; le convertisseur n'est pas touché (ses tests ne sont pas relancés).
- **Rouge d'abord** : S1, 22 tests sur 22 rouges contre une ébauche qui rend `false` (« not implemented »), puis 22 verts ; TH3, rouge par l'absence du point
  d'entrée (`ArcSpec` sans `Arrival`, erreurs de compilation CS1739 et CS0246), puis, avec le point d'entrée seul, `PosZ = 0` au lieu de 1048576 (voir S3, déviation).
  A20, A10 et A11 testent du code existant : aucune implémentation ne les précède, ils sont verts du premier coup contre les valeurs écrites d'avance ; leur
  discrimination est vérifiée par trois valeurs altérées (X de Sybill, position de rec0, `(0x4C, 331)`), qui les font échouer, puis rétablies.
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sans en changer une (positions de Sybill, positions `0x64` de rec0 à rec5, cellules (12,23) à (12,26),
  ordre des drapeaux T101 à T105, ensembles d'opcodes sautés, position de rec4 = héros + (96, 16, 0) px).
- **Limites d'images retenues** : A20 160 (fin `0x11 @596` à l'image 134) ; A10 1000 (fin `0x11 @354` à l'image 922) ; A11 240 (fin `0x38 @437` à l'image 208).
- **A20** : Sybill part de (540, 472) à l'image 1 (`@584`) ; sa marche finit au premier passage de `@588` (image 131) avec **x = 732,75 px** (48021504), y = 472 :
  dépassement de 0,75 px, comme le retard D-E19-13 ; les onze `0xA2` sautés tous à l'image 0.
- **A10** : `B[1]` aux images 0 (`@106` à `@121`), 1 (`@122`, `@139`) et 2 (`@140` à `@180`) ; T100 visible à l'image 3 ; cellules à 0x00 au premier tick de rec5 `C[1]`
  (image 3), 0x41 de nouveau à l'image 227 ; drapeaux T101 posé à l'image 4, effacé en 228 (rec5 `@703`), T102 229 puis 323 (Wendell `@774`), T103 324 puis 479
  (Nestus `@944`), T104 480 puis 488 (Bergus `@847`, après `0x25 @838` à l'image 484 et `@843` à l'image 487), T102 de nouveau 489 puis 581, T105 582 puis 737 (Meade `@1026`).
- **A11** : `0x05 @576` (T0) à l'image 3 ; boîte 130 ouverte à l'image 5, fermée à l'image 9 (T6 puis T2) ; hero placé en (996, 120, 16), `0x8B @86` à l'image 10 ;
  boîte 131 à l'image 11 (T200 visible en 12, T201 en 13 au premier appui), attente `@353` à l'image 67 ; boîte 132 à l'image 194 ; `0x38 @437` et **désactivation de rec4
  (`C[3] @451`) à l'image 208**, T3 visible en 209. **`0x58 @110` sauté 36 fois** jusqu'au signal de fin (dès l'image 2, non épinglé).
- **Temps d'exécution** (Debug) : chaque arc environ 1 s ; la suite complète 23 s.
- **Suites** : `Alundra.Tests` **2086 réussis avant la tranche, 2112 après** (+22 S1, +1 TH3, +3 arcs), 0 échec, en Release (23 s) puis en Debug (23 s), la Debug en dernier.
  `cmp` sans écart entre `Alundra/bin/Debug/net9.0-windows/Alundra.dll` et `alundra-project/Alundra.dll` (sha256 `bb5332a6...a1b`), re-contrôlé après les `--dry-run`. Le verifier a reconstruit ensuite depuis les mêmes sources (HEAD embarqué dans la
  version) : sha256 `19398dc7...`, `cmp` sans écart, re-contrôlé par la session principale après sa dernière build Debug.
- **`--dry-run`** des deux préréglages (`dotnet run -c Debug`) : sortie conforme au tableau de S1, code de sortie 0, aucun dossier `AlundraGame` créé sous
  `%LOCALAPPDATA%`.

### E19.d2b (2026-10-02)

- **Commits** : moteur (branche `chantier/field-movement-obstacles`, faits avant cette exécution) `4e6bd6bd` (ADR-0047), `3dc98385`, `c57c120f`, `d509bc10`, `c2e4fdce`. Parent (branche
  `chantier/e19-opcodes`) : B1 `fdd6fb9` (pointeur du sous-module), B2 `f3e3a03`, B3 `469c6f3` (avec l'installation de la sonde et le ré-épinglage d'A11), B4 `9896578`, B5 `363db2a`, B6 `ee00ff0` (les
  arcs) et `0a68fa4` (T-REG-0), B7 `f87ae56`, puis cette mise à jour des docs. Aucun commit sur `main`, rien poussé ; la modification locale de l'auteur dans `CasaEngine.Launcher/Program.cs` n'a
  été ni touchée ni indexée.
- **Suites** : `CasaEngine.Tests` **2441** réussis, 0 échec (base 2405 : +36 tests moteur, aucun existant modifié). `Alundra.Tests` **2112 avant la tranche, 2185 après** (+73 : B2 17, B3 32, B4 7 et 1 jumeau
  du livre, arcs de B6 10 : T-A19, T-A10v, T-B9 et sa contre-épreuve, T-C61, TN-3 et quatre lignes de TH4, B7 6 lignes), 0 échec, en Release (30 s) puis en Debug (30 s), la Debug en dernier. Le convertisseur n'est pas
  touché (ses 400 tests ne sont pas relancés). `cmp` sans écart entre `Alundra/bin/Debug/net9.0-windows/Alundra.dll` et `alundra-project/Alundra.dll` (sha256 `ed1126bb...2dbf`).
- **Tests existants modifiés** (la liste du plan, rien d'autre) : A11 (ré-épinglé), `AlundraSaveBookEndToEndTests` (T-REG-E12D-1), les deux P-a d'`AlundraInteractionPassTests` (T-REG-E12D-2), le
  commentaire du miroir de `SailorThirteen`, le nom du test `FlushContact_*`, et la hygiène de B7 (A20, A10, `TH3b`). Aucune autre épingle ni trace n'a bougé (les annexes de trace de l'intro et du héros ne sont pas
  régénérées).
- **Rouge d'abord** : B2, 8 des 17 nouveaux tests rouges ; B3, 20 des 32 (T-R1 douze lignes, T-R3 deux, T-R4, T-R-ID, T-R-LIFT, T-R9, `DrawDebug` et les contrôles d'ordre et de drapeaux soulevables ; les 12 autres sont des contre-épreuves et T-REG-Z, verts avant et après) ; B4, 7 des 16 des trois classes touchées. Arcs sur la DLL d'avant B3 : T-A19 `slot 2 program @452: last 0x0B @506` (limite 1700) ;
  T-A10v `slot 2 program @5156: last 0x0B @5172` (6500) ; T-B9 `slot 1 program @1132: last 0x0B @1215` et `slot 2 program @3636: last 0x0B @3673` (3200) ; T-C61 `slot 1 program @872: last 0x1E @980` (700) ;
  la contre-épreuve de T-B9 et TN-3 rouges aussi (plateforme à 800,5 px au lieu de 800).
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sans en changer une.
  - T-A19 : héros `@95` (6946816 ; 19398656) ; Septimus `@458` (8650752 ; 21626880) contre rec7 (7864320 ; 22544384) ; `@463` PosX 11927552 ; `@506` finit en (11927552 ; 22446080) ; rec7 détruit à l'image 119 et
    absent de la liste à la fin de l'image 120 ; T10 à T70 posés aux images 334, 480, 552, 654, 702 et 821 ; héros `@120` (6946816 ; 21495808), `@125` (11862016 ; 21495808) ; `0x53 @147` à l'image 1116 vers la 362 en
    (16515072 ; 17301504 ; 2097152), effet 4.
  - T-A10v : Meade (62881792 ; 32997376) ; Nestus `@5163` PosY 33914880 (image 1090), `@5166` x = 64274432 (980,75 px, image 1099), `@5172` y = 32047104 (489,0 px, image 1128) ; T666 à 1160 ; Bergus `@5061`
    PosX 63963136 (image 1814) ; T668 à 1876 ; Bergus `@5071` (67108864 ; 36175872) exactement (image 1971) ; T674 posé à 4241, effacé à 4739 ; `0x53 @2003` à l'image 5100.
  - T-B9 : Septimus `@3670` (59006976 ; 39190528) à l'image 992 ; héros `@1212` (58851328 ; 39256064) à 1022 ; `@3673` PosX 54214656 (1022) ; `@1215` PosX 55576576 (848,03 px, image 1043) ; rec41 détruit à 1043 ;
    T510 à 905, T511 à 1055, T512 à 1221, T514 à 1333, T515 à 1583 ; `@3693` à 1693, `@3734` à 2115 (`0x11 @3738` à 2115). Contre-épreuve (y0 744), image 1400 : Septimus (57409536 ; 51912704), héros garé (57094144 ; 50995200).
  - T-C61 : `@929` (38535168 ; 36110336) à l'image 50, `@934` (27394048 ; 36110336) à 108, `@959` (17956864 ; 45547520) à 229, `@964` (17956864 ; 50855936) à 271, `@977` (11665408 ; 50855936) à 304, `@980`
    (11665408 ; 55050240) à 336 ; G672 à 337.
  - TN-3 : `0x1E @943` démarre à l'image 62 en (96 ; 752) ; la plateforme atteint (96 ; 800) à l'image 159 et n'en bouge pas pendant 500 images (fin à 659).
  - A11 : `@341` (71565312 ; 7995392), `@346` (66650112 ; 7995392) avec `ForceAdjusted` 1, `@386` PosX ≥ 68222976 ; `(C,276)` 5, `(C,289)` 9, `(B,86)` 10, `(C,325)` 11, `(C,353)` 48, `(C,417)` 175, `(C,437)` et `(C,451)` 189 ;
    T200 à 12, T201 à 13 ; contacts de Septimus {rec4, héros}.
  - T-REG-0 : `EntityBlockCount` 0 à la fin des onze arcs ; TH4 : aucune des quatre arrivées ne recouvre une entité à la première image.
- **Limites d'images retenues** : T-A19 1700 (fin à 1116) ; T-A10v 6500 (5100) ; T-B9 3200 (2115) et sa contre-épreuve 1500 ; T-C61 700 (337) ; TN-3 1200 (659) ; A11 inchangée, 240 (fin à 189).
- **Temps d'exécution** (Release) : chaque arc de B6 entre 0,5 et 0,8 s (T-A10v, 5100 images, le plus long) ; aucun n'approche 120 s ; la suite complète 30 s.
- **Ensembles d'opcodes sautés des nouveaux arcs** (mesurés) : 0x45, 0x46 (185) ; 0x25 deux fois (10, E19.d2c), 0x58, 0x90, 0x95, 0x2B, 0x4C, 0x4D (10) ; 0x29, 0x2B, 0x5D, 0x52 (61) ; 0x3F (346).

### E19.d2c1 (2026-10-03)

- **Suites** : `Alundra.Tests` **2185 avant la sous-tranche, 2228 après** (+43 : C1 9, C2 16, C3 8, C4 9, C5 1), 0 échec, en Release
  (33 s) puis en Debug (32 s), la Debug en dernier ; `cmp` sans écart entre `Alundra/bin/Debug/net9.0-windows/Alundra.dll` et
  `alundra-project/Alundra.dll` (sha256 `cee2e950...7df9`). Moteur, convertisseur et analyseur non touchés.
- **Traces** : contenu identique après chaque suite ; le test réécrit les quatre traces du héros en fins de ligne LF (`git diff
  --exit-code` rend 0 ; fins de ligne remises par `git checkout`).
- **Valeurs mesurées contre valeurs écrites** : toutes égales, sauf deux arrêts tranchés (A10 : T102 effacé 620 et T105 posé 621,
  phase de Wendell ; Giles dans A10J : `0x24 @6400` à F0+255, `0x19` à F0+266, `FlagToDestroy` à F0+267, O-E19-29). A10 : fin
  `0x11 @354` à 960 (922 avant). A12 (179) : `1A [2] @411` à l'image 287, `0x53 @451` de 359 à 378 (+19). A10J : F0 = image 33,
  héros aux valeurs du binaire à l'unité, eau comprise ; `0x24 @2462` à F0+297.
- **Temps d'exécution** : chaque nouvel arc sous la seconde ; aucun n'approche 120 s.

### E19.e (2026-10-03)

- **Suites** : `Alundra.Tests` **2301 avant la tranche, 2333 après** (+32 : E1 20, E2 3, E3 2, E4 7), 0 échec, en Release (43 s)
  puis en Debug (43 s), la Debug en dernier ; `cmp` sans écart (sha256 `87158773...35bc`, DLL inchangée par la tranche).
- **Test statique** : 198 lignes sur les 30 cartes, exactement celles de la découverte, au premier passage du parcours C#.
- **Arcs** (images de fin) : A13 343, A14 257, A15 663, A17 700 (`0x11 @1115`), A18 191 puis 1068 (deux phases) ; sautés : A13
  10 (`0xA2` de `B[10]`), A14 aucun, A15 11, A17 1 (`0xA0 @536`), A18 7 ; contacts exacts : héros contre Septimus (A14),
  contre Ronan (A17) ; A15 sans aucun pas raccourci par une entité.

### E19.j (2026-10-03)

- **Commits** : plan `2d64b90`, précisions de montage `f24c4cf`, J1 `dedd1f8`, puis cette clôture.
- **Suites** : `Alundra.Tests` **2333 avant la tranche, 2344 après** (+11 : J1-a à J1-g, J1-f en 5 cas ; un test existant
  réécrit), 0 échec, en Release (44 s) puis en Debug (46 s) ; `cmp` sans écart.
- **Rouges d'avant** (ancien bloc remis par le vérificateur) : 7 sur 32 du filtre ; J1-b lit 0 au lieu de 7, J1-d `Result` 1,
  J1-e 130 au lieu de 129, J1-g `0xFF @168` seul ; J1-f vert (garde des bornes).

### E19.k1 (2026-10-03)

- **Commits** : plan `cd8a69b`, révision `411189f`, relecture `1b992b0`, K1 `88ce888`, K2 `015eb5d`, K3 `9946ce9`, puis cette
  clôture.
- **Suites** : `Alundra.Tests` **2344 avant la tranche, 2371 après** (+27 : K1 17, K2 7, K2b 3), 0 échec, en Release (45 s)
  puis en Debug (42 s) ; `cmp` sans écart.
- **Arcs** : épingles A5, A5r, A6 et A18 vertes du premier coup aux valeurs écrites ; A6 n'atteint que `0x8E @260`, `@335`,
  `@342` avant le départ ; liste fermée 198 → 170, la 392 sans site.

### E19.l1 (2026-10-03)

- **Commits** : plan `16ff2be`, L1-1 et L1-2 ensemble `02277fe` (L1-1 seule laissait la règle 2 et A11 rouges), puis cette
  clôture.
- **Suites** : `Alundra.Tests` **2371 avant la sous-tranche, 2401 après** (+30), 0 échec, en Release (47 s) puis en Debug
  (43 s) ; `cmp` sans écart.
- **Arcs** : A11 réécrit (`0x58 @110` exécuté, rien à `@119`, au moins une instruction à `@124`, aucun `0x27 @470`/`@498`) ;
  A10J, T-A10v, T-B9, TN-3 et T-C61 inchangés et verts ; liste fermée 170 → 153, 15 cartes sans site.

### E19.l2 (2026-10-03)

- **Commits** : L2-1 et L2-2 ensemble `cb7453f` (L2-1 seule laissait la règle 2 rouge), puis cette clôture.
- **Suites** : `Alundra.Tests` **2401 avant la sous-tranche, 2440 après** (+39), 0 échec, en Release (42 s) puis en Debug
  (47 s) ; `cmp` sans écart.
- **Test statique** : liste fermée 153 → 140, 17 cartes sans site ; `0x9F` n'est plus une attente ; un seul site de
  prédicat de niveau S reste connu, 10 `C[75] @6418` (`0x95`, E14).

### E19.h1 (2026-10-03)

- **Commits** : plan `5bbfb42`, H1-1 `b3a4d5d`, puis cette clôture.
- **Suites** : `Alundra.Tests` **2440 avant la sous-tranche, 2460 après** (+20), 0 échec, en Release (44 s) puis en Debug
  (46 s) ; `cmp` sans écart ; la liste fermée de la chaîne inchangée (140 lignes).

### E19.h3 (2026-10-03)

- **Commits** : plan `5bbfb42`, révision `de4c4c7`, relecture `3beb2b2`, H3-1 `7030fcf`, puis cette clôture.
- **Suites** : `Alundra.Tests` **2460 avant la sous-tranche, 2466 après** (+6), 0 échec, en Release (44 s) puis en Debug
  (51 s) ; `cmp` sans écart ; aucun test existant, arc ni trace touché.

### E19.h4 (2026-10-03)

- **Commits** : révisions `d0f401f`, `e172db9`, H4-1 et H4-2 ensemble `a09cd66` (H4-1 seule laissait A10J et la règle 2 rouges),
  puis cette clôture. Arrêt en exécution tranché par la session principale (glissements d'A14 et A18, épinglés à 1).
- **Suites** : `Alundra.Tests` **2466 avant la sous-tranche, 2474 après** (+8), 0 échec, en Release (45 s) puis en Debug
  (48 s) ; `cmp` sans écart ; liste fermée 140 → 134, 19 cartes sans site.
- **A10J** : `0x24 @2462` finit à F0+308 (le binaire : F0+308), en (47877120 ; 50790400).

### E19.r, R1 et R2 (2026-10-03)

- **Commits** : enquête `3fe4640`, R1 `05072ef`, R2 `c56b87f`, puis cette clôture.
- **Suites** : `Alundra.Tests` **2474 avant, 2483 après** (+9 : R1 6, R2 3), 0 échec, en Release (47 s) puis en Debug (48 s) ;
  `cmp` sans écart.

### E19.r, R3 (2026-10-03)

- **Commits** : plan `8c9590a`, révision `672e3ef`, R3 `dbcd680` (code, tests, ADR-0024, plan), puis cette clôture.
- **Suites** : `Alundra.Tests` **2483 avant, 2491 après** (+8 : 7 tests de recyclage, l'arc de la carte 15), 0 échec, en Release
  puis en Debug ; `cmp` sans écart. La scène des Murggs finit vers l'image 307 de l'arc.

### E19.d2c2, D5a (2026-10-03)

- **Commits** : plan et relectures `3eb96b1`, `22f4ca8`, `99ff51d`, `6e6a0cd`, `c38f952`, `4fe5b4c` ; D5a `950b85e` ; puis cette
  clôture.
- **Suites** : `Alundra.Tests` **2491 avant, 2496 après** (+5 : UH-15, UH-17, UH-18 en deux cas, UJ-CLIMB), 0 échec, en Release
  puis en Debug ; `cmp` sans écart. D5b (plateformes qui bougent en Z) reste en pause pour une reprise de conception.

### E19.f0 (2026-10-03)

- **Commits** : plan `99ff51d` à `4fe5b4c` ; `0beab51` (convertisseur), `fa8d86c` (DLL), `cd8cc35` (ADR-0025), `4f65ecd` (plan) ; puis
  cette clôture.
- **Suites** : convertisseur 415/415 ; `Alundra.Tests` **2496 avant, 2504 après** (+8), 0 échec, en Release puis en Debug ; export
  en place : 311 fichiers changés, comme prévu.

### E19.f1 (2026-10-03)

- **Commits** : plan `3e6f285`, `60d9699` ; analyseur `b92b7b9` (branche `chantier/e19f1-dialogue-boxes`) ; parent `2e86c87`, `512177c` ;
  puis cette clôture.
- **Suites** : convertisseur **415 avant, 417 après** ; `Alundra.Tests` 2504 inchangé ; export en place : 7 fichiers nouveaux, 2 modifiés.

### E19.s (2026-10-03)

- **Commits** : moteur `c54cc939` à `dfaed7a6` (branche `chantier/e19s-virtual-resolution`, ADR-0048) ; parent `1250f9b`, `e08c467`,
  `b386132` (ADR-0027), `a7e57cf` ; puis cette clôture.
- **Suites** : `CasaEngine.Tests` 2490 ; convertisseur **417 avant, 418 après** ; `Alundra.Tests` **2504 avant, 2505 après** (3 retirés,
  4 ajoutés) ; export en place : 3 fichiers changés, comme prévu.

### E19.h1b1 (2026-10-03)

- **Commits** : plan et relectures jusqu'à `b842c7a` ; `b381de8` (code, tests, ADR-0026, plan) ; puis cette clôture.
- **Suites** : `Alundra.Tests` **2505 avant, 2510 après** (+5 : la plateforme de la 83, le relevé au terrain, trois gardes de piles), 0
  échec, en Release puis en Debug ; `cmp` sans écart.

### E19.t (2026-10-03)

- **Commits** : plan et relectures jusqu'à `a4a5ce8` ; `541ae72` (code, tests, ADR-0028, plan) ; puis cette clôture.
- **Suites** : `Alundra.Tests` **2510 avant, 2523 après** (+13), 0 échec, en Release puis en Debug ; `cmp` sans écart.

### E19.k2 (2026-10-03)

- **Commits** : moteur `7350f247`, `79ef3532`, `987f0c7f` (ADR-0049) ; parent `0d14dfd`, `6688fc9` ; puis cette clôture.
- **Suites** : `CasaEngine.Tests` **2490 avant, 2502 après** ; `Alundra.Tests` **2523 avant, 2539 après** (+16), en Release puis en Debug.

### E19.g G0 (2026-10-03)

- **Commits** : analyseur `97ca6a2`, `876dfd9`, `109f62e`, `d8d9230` ; parent `4e38a69` (pointeur, ADR-0030, plan) ; puis cette clôture.
- **Données** : `data-extracted/` 4450 → 4537 fichiers (700 lignes de diff, comme l'annexe) ; export en place : 105 fichiers changés ;
  `Alundra.Tests` 2539 inchangé, convertisseur 418 inchangé.

### E19.m0 (2026-10-03)

- **Commits** : plan et relectures jusqu'à `7722354` ; `0c151b6` (code, tests, plan) ; puis cette clôture.
- **Suites** : `Alundra.Tests` **2539 avant, 2548 après** (+11 nouveaux, −2 : la théorie retirée comptait deux cas), en Release puis
  en Debug.

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E19-1 | ~~Portails trou et escalier de la 390~~ — **réglé par la recette du 2026-09-29** : le journal montre le passage par le portail 5, qui charge la pièce B. Question d'origine : si la recette d'E19.a montre que le héros ne suit pas le capitaine par là, faut-il corriger dans E19 ou dans un chantier de transitions ? | E19.a (recette) |
| O-E19-2 | Nouveaux écarts de la décompilation relevés dans le binaire : `0x5F` (entité et taille), `0x66` (sens de la copie), compteur de `0x1C`, `y` de la boîte de nom, portrait de `Script_196_0C4`, test de zone de `GetMapEffectRecord`, `AddOneItemIfUnlocked`, `InitializeEventData`. Le portage suit le binaire. Faut-il aussi corriger la décompilation dans l'analyseur, comme pour la taille de `0x78` en E16.a ? | E19.m |
| O-E19-3 | ~~Déplacement vertical des entités nues dans le support d'arcs~~ — **tranché le 2026-10-01 (D-E19-14)** : les arcs chargent les vrais préfabs par un gestionnaire d'assets construit par le test. | E19.c1 |
| O-E19-5 | Quand un `0x5B [x,0,dir]` arrête un PNJ au tick où sa marche se termine, le mouvement de ce tick s'applique encore avec la nouvelle direction : le marin 12 de la 389 descend de 1,25 px au tick de `@1494` (mesuré en E19.a3 : y = 843,25 à l'image de `@1494`, contre 842,0 à l'image précédente, lue par le test du marin 12). Cela peut venir de la latence d'une image de `CurrentAnimationId` des PNJ, déjà relevée (le moteur synchronise l'animation en fin d'image). **Tranché par le binaire le 2026-10-01 (§1.2d, [binaire])** : l'original exécute dans l'ordre les événements de carte, les entités, `UpdateAnimation` puis la physique (`0x8002E100`, `0x8003B388` → `0x8003B3D8` → `0x8003B3E0`) ; un changement d'animation par script s'applique donc dans la physique du même tick. La DLL a une image de retard : c'est la cause du pas de 1,25 px du marin 12, et chaque panoramique du bloc de la 476 fera 48,75 px au lieu de 48. (La correction qui était annoncée dans E19.c est abandonnée : voir la suite.) **Précisé puis tranché le 2026-10-01** : chaque marche du bloc fait bien 48,0 px, mais la première de chaque panoramique dure un tick de plus et le bloc dépasse de 0,75 px après chaque panoramique. L'auteur garde ce retard (D-E19-13, ADR-0018) : le corriger aurait déplacé des points épinglés de l'intro. | E19.c1 (clos) |
| O-E19-6 | À l'apparition, l'ajustement au sol (`ClampToGround`) et `TerrainHeight` ne font rien en production : `World.AddEntity` ne fait que mettre l'entité en file, et `Entity.World` n'est posé qu'à l'intégration suivante. Le commentaire d'`AlundraWorldProxy.cs:773-778` dit le contraire (corrigé en E19.c1 T7). Faut-il corriger le comportement ? | E19.h |
| O-E19-7 | Une entité sans contrôleur ne bouge jamais en Z dans la DLL, alors que le binaire intègre Z pour toute entité active (`MoveEntity` `0x80037E34` → `ComputeZPosition`). Sur la chaîne, tous les enregistrements ont un contrôleur. | E19.h |
| O-E19-8 | `IsZForceApplied` (`+0xF8`) n'est pas porté : au tick d'un changement d'animation, le binaire remplace `ForceZ` par la valeur du jeu d'animation (131 des 395 enregistrements de sprite en ont une non nulle). Il suppose l'animation résolue avant la physique, ce que D-E19-13 ne fait pas. **Avancé le 2026-10-02 (D-E19-31)** : l'impulsion se pose dans `StepAnimationClock`, au tick du binaire ; seul le changement de vitesse horizontale des PNJ garde le retard D-E19-13. | E19.d2c |
| O-E19-9 | Les 5 animations Loop de durée 0 de l'export (banque 127 anim 0 gauche et droite, banque 151 anim 1 haut, gauche et bas) sont invisibles dans le moteur : la clé cachée de fin tombe au même instant 0 que l'image. Le binaire montre l'image figée. À corriger au convertisseur (export complet à relancer). | à placer |
| O-E19-10 | ~~Défaut du moteur sur le chemin en temps réel~~ — **réglé le 2026-10-01 par le moteur (`cc6f498e`, R2) et vérifié par le plan moteur (R1 à R4, 0 échec)** ; question d'origine : défaut du moteur sur le chemin en temps réel : à 0,02 s par image, 197 des 5205 Loop de durée positive de l'export ne bouclent jamais (le temps tombe pile sur la durée, puis la dépasse), et le sprite montre la pose cachée de fin (exemple : animations 53 et 55 du héros). **Correction planifiée** (D-E19-19) : tâche T1.1 du plan moteur d'E19.c2. | E19.c2 |
| O-E19-11 | ~~Réarmement hors zone~~ — **réglé le 2026-10-03 par E19.j** (`dedd1f8`, CONFIRMED) ; question d'origine : la remise à zéro hors zone d'un événement de carte diffère du binaire : la DLL écrit sur l'entité de l'événement et ne remet pas `mapEvent.EventData` à zéro, le binaire (`0x8003C7F0`-`0x8003C804`) remet le pc et l'entrée de l'état de l'événement, `state+0x2C`, l'entité logique et l'octet de programme. Un programme B réentré reprend dans la DLL et recommence dans le binaire. Sans effet sur la 478 et la 416 (zones de toute la carte). | E19.j |
| O-E19-12 | ~~Base de la branche moteur d'E19.c2~~ — **réglé le 2026-10-01 (D-E19-20)** : l'auteur a mergé `chantier/field-move-to-contact` dans `main` du moteur (`74e97293`) ; la branche d'E19.c2 part de `main`. | E19.c2 |
| O-E19-13 | ~~Le moment de la fin Hold de Ronan~~ — **réglé le 2026-10-01 : A3 resserré épingle 26 (premier `0x1A @856`), 25 et 66 images, mesurés égaux** ; question d'origine : le moment de la fin Hold de Ronan (`0x1C @854`, image 25 de la 478) vient de l'horloge à virgule flottante du moteur, à ± 1 tick du binaire : il n'est pas épinglé. **Se ferme en E19.c2** : A3 resserré épingle 25 et 66 images. | E19.c2 |
| O-E19-15 | À l'apparition, `EvaluateEntitySupport(…, immediateAtSpawn: true)` accepte un support sans limite de portée : le bloc de la 391, apparu à 144 px au-dessus du marin 4, se pose une image sur sa tête (`PosZ` 7340032) avant de revenir sur le terrain. La fidélité de cet appui au binaire n'est pas vérifiée. | E19.h1b1 (H1b1-R3, H1b1-R5) |
| O-E19-14 | Un test statique qui compte les attentes `0x1C`/`0x1D` sur une animation absente du préfab de l'acteur (attendu : 3, les Flammes des cartes 35, 38 et 39), pour voir arriver tout nouveau cas avec une future exportation. | E19.m |
| O-E19-16 | **Arrêt de D5 (recensement de `0x24`)** : 26 des 59 sites atteignables des cartes du chemin de l'histoire (Inoa 162-182, 44, 10) ne finissent pas sur « mur trouvé » dans le modèle statique (rapport `docs/census-0x24-waits.md`) : 17 « aucun mur » et 9 « indéterminé ». Dont 8 sous main tenue : le héros marche jusqu'à un mur depuis une position que le programme ne fixe pas (cartes 10 `@1212` et `@2462`, 176 `@599`, 178 `@123`, `@141`, `@146`, `@151`, 179 `@564`) ; sans main tenue : des villageois de la 10 qui marchent jusqu'au bord de la carte (`@4973`, `@5061`, `@5163`, `@5275`, `@5344`, `@5430`, `@5588`, `@5905`, où l'original termine par le rognage d'écran, non porté) et leurs sites suivants indéterminés, le héros de la 10 `@1047` et Nestus de la 165 (`@932`, `@943`). **Question** : D4 (`0x24` et U1 à U4) et D5 se commitent-ils tels quels, en acceptant ces sites comme risque connu jusqu'à E19.h (contacts, rognage d'écran) et à leurs arcs, ou faut-il d'abord porter le rognage d'écran de la carte et vérifier les positions d'arrivée du héros des sites sous main tenue ? Recommandation : commiter D4 et D5 avec le rapport (le port suit le binaire ; sur le chemin de l'histoire seuls les 8 sites sous main tenue peuvent bloquer le joueur, et ils se vérifient par un arc à l'arrivée de chaque carte) et ouvrir le rognage d'écran dans E19.h. D6 à D9 attendent cette réponse (A8 ne passe qu'avec D4 et D6). **Réglé le 2026-10-01 (D-E19-24 à D-E19-26)** : une vérification en lecture seule, contre-vérifiée, a montré que les 26 sites venaient de positions de départ fausses dans le modèle (les villageois de la 10 et Nestus de la 165 n'apparaissent pas à la position de leur enregistrement mais par `0x8A`, ou `0x2D` puis `0x64` ; le héros part de ses vraies arrivées, non de toute la carte). Corrigé, le chemin n'a plus aucun site « aucun mur » ni « indéterminé ». Elle a aussi trouvé, au-delà de la 163, deux blocages sans rapport avec `0x24` (O-E19-18, O-E19-19) et l'absence du rognage au bord de la carte (O-E19-17). | E19.d (D4, D5) |
| O-E19-17 | **Rognage au bord de la carte** **[binaire]** : `ApplyEntityForces` (`0x800366FC`) borne le pas de toute entité de la liste physique (le héros toujours ; les autres sauf portées) à la grille de 52 × 60 cases (x de 0 à 1248 px, y de 0 à 960 px, bornes posées par `SetEntityDimensions` `0x80039C40`) et lève alors `ForceAdjusted` (`0x8003679C`, `0x800367D4`) : dans l'original, une marche vers le bord finit toujours. La DLL ne le porte pas (`AlundraScriptedMotion.RunOneKinematicTick`, et le champ de cellules ramène un point hors grille à la case de bord) : une entité peut sortir de la carte. Change aussi Alundra en jeu libre au bord des cartes (portails sur les cases de bord à vérifier). | tranche à part, plus tard (D-E19-25) |
| O-E19-18 | **Scène des villageois de la carte 10** (après le premier rêve) : dans le modèle des cellules de la DLL, la marche `0x0B @5172` de Nestus (vers le haut, 28 px) et `0x0B @5071` de Bergus butent sur un mur dès le premier pas ; `0x0B` n'a pas de sortie sur blocage, donc T666 n'est jamais posé, B[14] ne finit jamais et le héros reste bloqué (`0x10 @1826`). Dans l'original la scène va au bout : contact entre entités (Meade immobile en (960, 504)) ou écart du champ de cellules, à établir. **Établi le 2026-10-02** : contact entre entités (la règle des cases est la même) ; scène du chapitre 18 (G218). | E19.d2b (D-E19-27) |
| O-E19-19 | **Saut scripté d'Alundra** : la chaîne 179 → 176 → 10 → 135 fait sauter le héros d'une marche de 16 px (carte 10, animation 2 et `0x25`, `0x0B @2456`) ; le saut et `0x25` ne sont pas portés, la marche cale. Même cas pour Giles (`0x0B @6394`, animation 3). La 178 n'est atteinte qu'après (G1655, posé sur la 135). | E19.d2c (D-E19-31) |
| O-E19-20 | `0x45`/`0x46` (« NoObstacleSlide », `+0x6C` bit 0x2000, `0x8003E954`/`0x8003E96C`) décident si un contact avec un mur lève `ForceAdjusted` tout de suite ou essaie d'abord le glissement ; non portés (la 178 encadre ses `0x24` du héros avec eux). | E19.h |
| O-E19-21 | **Sites de `0x24` à risque hors du chemin de l'histoire** (recensement corrigé, `docs/census-0x24-waits.md`) : 9 sites en classe à risque (aucun mur 4 sur les cartes 40 et 62, indéterminé 4 sur les cartes 40, 62 et 249, vitesse nulle 1 sur la 152), dont 1 sous main tenue, et 65 sites non atteints dans le modèle (0x0B bloqué 26, drapeau jamais posé 39). Aucun n'arrête E19.d (D-E19-24). À reprendre avec les contacts entre entités et les bornes de force (E19.h), le rognage au bord (O-E19-17) et, par carte, un arc d'arrivée quand la carte est jouée. | E19.h |
| O-E19-4 | Le gestionnaire natif du créneau E (`0x8007ED10`, destruction après `Deactivated`, 417 enregistrements sur 85 cartes) : E14, ou une tranche d'E19 ? Sur la chaîne, il ne touche que l'oiseau de la 389 et des PNJ d'Inoa. **Réglé en partie le 2026-10-02 (D-E19-29)** : les index 0 et 1 (destruction seule) se portent en E19.d2b, sans butin ni effet de bris (O-E19-23). | E19.d2b ; le reste E14 |
| O-E19-22 | **Autres règles des bottes, non portées** **[binaire]** : sans bottes triton (niveau < 2), une case d'eau profonde (`(walk \| gp << 8) & 0xE00 == 0x800`, `0x800374FC`) bloque le héros : 10 644 cases dans 54 cartes, dont 399 sur la 416 ; le champ de la DLL ne connaît que les masques 0x40, 0x41 et 0x1000. Sans bottes magiques (niveau < 3), les cases `VramOR & 0x180` blessent (`0x80031AD0`, 7 322 cases). | à placer |
| O-E19-23 | **Soulevables et cassables traversables** (D-E19-28) : 2 250 enregistrements collisionnables (`Flags & 0x600`, dont 2 161 du natif E 2) ne bloquent pas tant que la saisie (`0x8002EDBC`) et les coups (`HitCounter`) ne sont pas portés ; l'effet de bris (octet d'en-tête `0x1E`) n'est pas dans `sprite-records.json` ; le butin de `DestroyEntity` (`0x80032B90`) n'est pas porté. Les obstacles à destruction native (environ 600 : murs à boule de fer, ronces, glace, rochers, piliers) restent solides en attendant (D-E19-34). | E14 |
| O-E19-24 | **Calage à l'entrée sud de la case (37,46) de la 10** [émulation] : sous le blocage fidèle, si le joueur entre dans la zone de `B[9]` par le sud (y ≥ 744), le héros garé bloque l'approche de Septimus (`0x0B @3643`) et la scène cale avant T510 dans l'original émulé. Défaut de l'original à corriger (règle de l'auteur) ou à reproduire : à trancher dans le plan d'E19.d2b. **Tranché le 2026-10-02 (D-E19-37)** : reproduit et noté (contre-épreuve de T-B9) ; l'accès par le sud dépend d'échelles non modélisées ; correctifs possibles relevés : rayon de `0x0B @1176` du héros 32 → 24, ou de `0x0B @3643` de Septimus 44 → 36 (un octet sur les cartes 10 et 331), ou héros verrouillé non obstacle (change A11). | à rouvrir quand le chapitre 16 sera jouable |
| O-E19-25 | **Recensement des marches `0x0B`/`0x1E`** (découverte d'E19.d2, scratchpad) : avant tout réemploi de ses totaux, corriger son modèle (`0x62`/`0x63` sur le marcheur, glissement des directions obliques, départs écrits après le site, départs partagés avant une étiquette « règle »). **Scripts archivés le 2026-10-02** à la demande de l'auteur dans `research/census/` (archive, pas un outil : README avec les défauts connus ; seuls quatre chemins d'import vers le scratchpad ont changé). | E19.m |
| O-E19-26 | **`0x28` à `0x2B`** (bits de classe A/B du marcheur, `0x8003DC24`-`0x8003DC6C`) : 15 marches bloquées seulement dans la DLL (Muruta des cartes 2 et 384, chariots de la mine 61, 63, 66 et 328), aucune sur une carte de l'histoire ; les porter ajoute 4 blocages sur la 102. | E19.l |
| O-E19-27 | **Reste du saut, non porté en E19.d2c** : aimantation de 3 px au sommet, plafonds (`0x80036D94`), sons des changements d'animation autres que le décollage du héros (`0x800490FC`), ~~`+0xF8` surchargé pour le type 0x14~~ (fausse piste, découverte d'E19.h : le registre de base vaut `entity + 0x20`) ; tolérance de marche de 3 px en l'air du moteur (le binaire n'en a aucune ; **avancée en E19.d2c2, S6**) ; la descente de 4 px que le moteur aimante (le binaire tombe au-delà de 3 px). **Avancés en E19.d2c (2026-10-02)** : dessus d'entités et règle du passager pour le héros (D-E19-42), son du décollage (D-E19-44). | E19.h |
| O-E19-28 | **Écarts acceptés du contact entre entités** (E19.d2b) : avance par axe du moteur contre division conjointe du binaire (un mobile qui pousse en diagonale contre une entité glisse ; 5,6 % des pas obliques bloqués finissent jusqu'à 2 px ailleurs) ; `ForceAdjusted` sur contact d'entité 0 à 2 ticks plus tôt à l'est et au sud ; contact au flottant (4 à 8 unités 16.16) ; `XCollisionEntity` écrit pour tout mobile (le binaire le met à 0 dans la moitié des ticks raccourcis d'un mobile sans gravité ou en l'air) ; liste d'un tick de retard pour `0x62`/`0x63` et les créations par script ; ordre de mise à jour par entité au lieu de par créneau ; Z des PNJ en retard d'un tick. | E19.h |
| O-E19-29 | **Troncature au pixel à l'atterrissage** (relecture d'E19.d2b) : quand un PNJ à contrôleur atterrit sur une nouvelle hauteur de terrain, `EvaluateEntitySupport` (`AlundraEntityScriptProxy.cs:700-735`, `wasAlreadyLanded` faux à chaque changement de hauteur) appelle `PushLogicalPositionToRoot`, et `ResolveLogicalPosition` (`AlundraEntitySpawnFactory.cs:472-479`) ramène X et Y au pixel entier inférieur ; le binaire garde la fraction. Sur une rampe qui change de hauteur à chaque pixel, la vitesse double et le cortège de la 10 `B[13]` (chapitre 18) cale, avec ou sans blocage d'entités. Question à l'auteur : tranche à part. **L'auteur, le 2026-10-02 : plus tard, dans une tranche à part.** Vu aussi sur Giles dans A10J (E19.d2c1 C4) : sur la rampe (30,49), X ramené à 732,0 et une montée de 2,0 px par image, `0x24 @6400` deux images plus tôt ; épinglé dans l'arc avec ce renvoi. | tranche à part, plus tard |
| O-E19-30 | **Recette T6 d'E19.b (2026-10-02, auteur)** : la scène de la vision de Lars et Melzas (476) s'affiche mal : carte en partie noire derrière un halo elliptique, personnages mal affichés (captures de l'auteur). Pistes connues, non vérifiées : effets de carte non exportés (l'aura de la 476, E19.g, D-E19-7), masque des couches de fond `0xA4` et balancement `0x8E`/`0x8F` sautés (E19.k), autres opcodes d'affichage sautés sur la 476. À établir par une découverte en lecture seule avant de placer la correction. **Reconnaissance du 2026-10-03** : pendant la vision, les arcs A2 et A4 ne laissent sauter que `0x4C`, `0x92`, `0x93` et `0xA2` (`AlundraVisionArcTests.cs:32`) : trois opcodes d'effets (E19.g) ; `0xA4`, `0x8E` et `0x8F` n'y sont pas exécutés ; l'export de la 476 n'a aucune donnée d'effet (D-E19-7 : le convertisseur ne les exporte pas encore). Piste la plus probable : l'aura de la 476, non dessinée (E19.g) ; non vérifié en jeu. **Découverte d'E19.g (2026-10-03)** : la DLL ne dessine aucun effet ; à la 476, l'aura de la vision est un effet additif dans une zone sans tuiles (noire par construction) ; « le halo elliptique » des captures n'est donc probablement pas l'aura (non dessinée) ; non vérifié en jeu. | E19.g (à confirmer) |
| O-E19-31 | **Cartes sous-marines 159 et 160** (« Fairy cave underwater ») **[binaire, données]** : gravité 3, `ZViscosity` 256 et octet d'en-tête `+8` (exporté sous le nom `SlideEffectId`) à 1, contre 128, 4096 et 0 sur les 481 autres cartes ; l'octet `+8` décale `ForceX` et `ForceY` avant le déplacement (`srav` en `0x8003675C`) : le héros y va deux fois moins vite et un saut dure 178 ticks. La DLL ne lit pas ce décalage (aucun consommateur). Hors de la chaîne. | à placer |
| O-E19-32 | **`LoadingMap` (`0x36`) en l'air** **[binaire]** : `MovePlayer` passe en `0x2D` quand le héros arrive au-dessus du sol (`0x800325E8`) ; la DLL laisse `0x36` sans effet (`AlundraPlayerManager.cs:256-263`). La 476 fait arriver le héros à 48 px au-dessus du sol (`0x53` de la 390 `@688`). Non porté en E19.d2c2. | à placer |
| O-E19-33 | **Scène d'avant le rêve** (179 `B[1]`, jour 3) et rêve 44 : la scène n'est atteignable qu'après le jour 2 (combat, E14) et aucun préréglage ne la couvre ; le rêve demande le combat. Question à l'auteur : faut-il un préréglage `day3-start` (179, case (12,22), z1, G203, G1660, table [162] = 176) et un arc de 179 `B[1]` ? Hors d'E19.e en attendant. **Complément du 2026-10-03** (découverte d'E19.l) : la scène d'avant le rêve (179 `B[1] @183`) retire le livre d'Elna (objet 88), donné à la 117 `@167` ; un préréglage `day3-start` fidèle porterait `NumberOfItems[177]` = 1. | à placer |
| O-E19-34 | **Paramètres effacés au redémarrage d'un programme** (découverte d'E19.j, D7) : `InitializeEventData` efface tous les paramètres (`Array.Clear`, `AlundraEventProgramRunner.cs:352`) alors que le binaire ne réécrit que l'entrée et le pc (`0x80041EE4`) et garde +8..+0x28 (clé d'attente, compteur de `0x37`…) : un programme suspendu sur son premier `0x37` continue son compte dans le binaire et le recommence dans la DLL ; 19 programmes B à zone bornée et 243 programmes C commencent par `0x37`, 47 par `0x0B`. | E19.m |
| O-E19-35 | **Termes additifs du suivi et écrivains natifs du balancement** (audit d'E19.k1, P4-a et P4-b) : la formule du suivi caméra du binaire ajoute aussi `0x800DD870` (X) et `0x800DD874` (Y), effacés à chaque tick et au chargement, le terme Y écrit par du code natif (`0x8007F350`) ; la structure du balancement (`0x800E4338`) a d'autres écrivains natifs que `0x8E`/`0x8F` : la magie séisme la sauvegarde et la restaure (`0x80035D00`/`0x80034E08`) et pose Flag (`0x80035DF4`), des comportements d'entités l'écrivent (`0x80061E78`, `0x8006224C`…). L'instance de session d'E19.k1 doit leur servir. | E14 |
| O-E19-36 | **Pistes XA (`0xB9`, `0xBA`)** (découverte d'E19.l, question Q-A1 à l'auteur) : `0xB9 [v]` lance la piste XA `v` (table de 13 entrées en `0x800C4818`) ; la piste 11 suit chaque objet reçu (fanfare, d'après le contexte des scripts), 7 à 10 sont des thèmes d'événement ; 42 sites dans 26 cartes, aucun sur la chaîne. Les entendre demande trois travaux : décoder les pistes depuis l'image brute `Alundra (France).bin` (l'`ARAN_XA.XA` extrait a perdu l'audio), un lecteur de flux ponctuel dans le moteur, et `0xBA` « occupé » tant qu'une piste joue (contraire de D-E11-5 : la boîte « vous avez obtenu » attendrait la fin de la fanfare). Sinon, un sans-effet explicite et journalisé garde le silence d'aujourd'hui. **Question** : veut-il entendre les pistes XA ? | auteur, puis une tranche XA |
| O-E19-37 | **`0x87` et `0x95`** (prédicats d'attaque) : ils lisent `TouchingEntity`, l'attaque courante et les boîtes de coup, que rien n'écrit dans la DLL ; restés sautés jusqu'à E14 (sur la chaîne, 10 `C[75] @6418` rend 0 dans toutes les scènes, comme le saut). Avec eux, la question Q-A2 : sur la 226 (le poulet, `C[14]`), quatre sites de `0x95` lisent les index 8 à 11 au-delà de la table des armes ; le binaire lit les octets suivants (seul 10, la flèche, peut correspondre), la décompilation lèverait une exception : copier ou corriger ? | E14 |
| O-E19-38 | **Scanner de portails** : la décompilation (et la DLL, `AlundraPortalTrigger.cs:21-32`) s'arrête sur un portail de `DestMapId` nul, le binaire (`0x8003166C` et ses 5 appelants) ne le teste jamais ; aucun des 3316 portails exportés n'a `DestMapId` nul : sans effet, gardé par E19.l1. | E19.m |
| O-E19-39 | **`0x82`, sous-cas `0x50` à `0x56`** (vie, magie et effets du héros) : portés en E19.l2 comme « `Result` 1, rien d'écrit, trace `Degraded` » ; seuls `0x53` (vie max + 1, vie pleine, son `0x31`, effets) et `0x54` (vie + 2) ont été lus dans le binaire, et l'emplacement de la vie du héros (entité `+0x14/+0x18` ou `PlayerStats`) et leur synchronisation n'y sont pas établis. 8 sites `0x53` (vases de vie), aucun sur la chaîne. | E14, E19.g |
| O-E19-40 | **Aimantation de descente** (découverte d'E19.h, surface C) : le moteur accepte un sol jusqu'à 4 px sous le pied, une fois par image sur tout le déplacement de l'image (`CharacterControllerComponent.Update`, `GroundSnapDistance` 4 exporté par `SpriteWriter.cs` ~389 et ~432) ; le binaire, 3 px par tick (`0x80037848`). Sur la chaîne, aucune descente de plus de 3 px par tick ; l'écart ne se voit qu'aux images de rattrapage (une pose de chute d'une image). Exporter 3 px casserait SJ-11 et les épingles des PNJ. Un plan moteur (sol résolu par tick) ou un pas vertical tenu par la DLL au sol : consigné, liée à D5 (F3, UH-7b). | plus tard (moteur) |
| O-E19-41 | **Tranché le 2026-10-03 (D-E19-57 : porté pour toutes les entités, son à l'arrivée compris ; l'audit d'E19.t corrige deux faits de la découverte : Melzas n'apparaît pas au chargement, le livre joue 204 une seule fois).** **Sons des changements d'animation** (découverte d'E19.h, surface C, question Q-H1 à l'auteur) : le binaire (`UpdateAnimation` `0x80038BB0`-`0x80038BC8`) joue le son de l'animation à chaque changement d'animation de **toute** entité (nouvelle animation, nouvelle ligne de direction, fin de chaîne, `0x1C`, et l'apparition, `InitializeEntity`) ; identifiant `Sfx`, + 0x100 si le bit 0x20 de l'octet 0xD ; la DLL ne joue que le décollage du héros (R8). Porté, il ajoute des sons dans des scènes de la chaîne : 476 (Melzas, 219 au chargement et à `0x8A @553`, 220 à `0x59 @572`), 392 (la course du héros à `0x5B @64`, son 13), le livre de la 178 et de la 179 (204 deux fois), la boule de fer de la 135 (120 à chaque cycle) ; un héros qui arrive par `0x53` en animation de saut réentend le son du décollage. **Questions** : porter la règle pour toutes les entités maintenant (recommandé : oui, comme l'original) ? garder le son à l'arrivée (recommandé : oui, même porte que les autres sons) ? | auteur, puis une sous-tranche d'E19.h |
| O-E19-42 | **Tranché le 2026-10-03 (D-E19-58 : le glissement de la DLL est gardé).** **Résolution exacte des contacts** (découverte d'E19.h, surface B, question Q-H2 à l'auteur) : le binaire divise le pas conjointement en 16.16 (`ComputeXYPosition` `0x80037730`) ; une poussée en diagonale contre un PNJ ou contre la pointe d'un coin arrête net Alundra, et le contact est en unités 16.16 (le dernier demi-pas peut s'arrêter 1 à 3 unités avant à l'est et au sud, `ForceAdjusted` un tick plus tard) ; dans la DLL elle glisse le long, et arrive au contact un tick plus tôt à l'est et au sud (O-E19-28 a à d). Le porter demande un plan moteur (un résolveur horizontal tenu par le jeu) et une ADR ; quelques épingles de contact relationnelles bougent de 0 à 3 unités. D-E19-9 ne promettait que le glissement (E19.h4). **Question** : porter la résolution exacte (étape 2), ou accepter le glissement de la DLL ? (recommandé : étape 1 maintenant, étape 2 seulement si l'auteur veut la sensation exacte). | auteur, puis plan moteur |
| O-E19-43 | **Cycle de palettes de `0xA4`** (E19.k2) : `0xA4 [b1, b2 > 0]` lance un programme de cycle de palettes (`0x80186790`) dont le décalage (`0x800C490C`) s'ajoute à l'octet de palette des tuiles et des couches cellulaires (`0x8005C574`, `0x8005CDBC`, `0x8005D544`) : la carte entière change de couleurs. Non porté par E19.k2 (trace `Degraded`) ; hors chaîne (carte 471 selon la découverte). Il demande un décalage de palette des tuiles dans le moteur. | plus tard (moteur) |
| O-E19-44 | **Portes manquantes de l'aimantation au sommet** (vérification d'E19.h3, P3) : dans le binaire, l'aimantation (`0x80037848`) n'est atteinte que si le pas XY demande une force (`+0xE4`/`+0xE8` non nuls, sinon saut de `0x800377A0` à `0x80037DC0`) et elle est défaite quand le pas est entièrement bloqué (`0x80037938`-`0x80037948` rendent X, Y et Z) ; la DLL aimante dès que Gravity et `ForceZ == 0`. Émulation : saut sur place sous une boîte dont le bas est à 34 px, le binaire touche le plafond (t1 131072) et atterrit à t4, la DLL s'aimante au sol dès t1. Aucun plafond du corpus n'est à moins de 40 px du terrain. | E19.m |
| O-E19-45 | **Tranché le 2026-10-03 (D-E19-59 : convention de la DLL à l'apparition, `0x8A` et `0x64` dans la même ADR).** **Convention de `PosZ` à l'apparition** (conception d'E19.h1b) : la DLL tient partout `PosZ` = celle du binaire moins 1 (atterrissage à `T`), sauf à l'apparition (`z − ModZ + 1`, fabrique ~657) et dans `0x8A`/`0x64`. Le `+ 1` est perdu au premier tirage de tête d'image pour une entité à contrôleur, ce qui la fait passer à travers un appui exact ; c'est ce que masque aujourd'hui l'appui d'apparition sans portée (O-E19-15). **Question** : passer l'apparition en convention de la DLL (abandonner le `+ 1`, relever à `max(PosZ, T)`), décision transversale à consigner en ADR, ou garder la convention du binaire à l'apparition et la traiter autrement ? Recommandation : convention de la DLL à l'apparition (la fabrique ; `0x8A` et `0x64` à examiner dans la même ADR). | auteur, puis E19.h1b et E19.h2 |
| O-E19-46 | **Tranché le 2026-10-03 (D-E19-47 : 320 × 240 à bandes, recalculé en temps réel).** **Hauteur affichée** (recette du 2026-10-03, 476) : la DLL montre 320 × 236 (`AlundraCameraMath.CameraDisplayHeight` 236, fenêtre 1280 × 944, valeur de la décompilation) ; le binaire fixe ses environnements de dessin et d'affichage à 320 × 240 (`0x800424AC`, `SetDefDrawEnv`/`SetDefDispEnv` 0x140 × 0xF0) : quatre lignes de moins dans la DLL. La fenêtre est aussi redimensionnable sans bandes (`AllowUserResizing`, zoom fixé par monde sur la hauteur) : élargie, l'image ne couvre plus les côtés. **Question** : passer à 240 (1280 × 960), et que faire d'une fenêtre redimensionnée (bandes, zoom recalculé, ou taille fixe) ? | auteur |
| O-E19-47 | **Réglé le 2026-10-03 (R3, D-E19-45, ADR-0024).** **Entités détruites jamais recyclées** (recette du 2026-10-03, carte 15) : `0x2C` et les autres recherches par id brut trouvent le mandataire d'une entité détruite, qui reste dans la liste avec son `EntityRefId` ; la scène des Murggs devant le manoir de Tarn (`B[1] @108`-`@111`) tourne sans fin et le contrôle ne revient pas ; même blocage hors combat ailleurs (carte 6, `B[2] @239`). R3 (D-E19-45). | E19.r |
| O-E19-48 | **La chute des cellules puise dans le mauvais générateur** (question bornée du 2026-10-03 pour E19.m0) : dans le binaire, la cellule de type 2 (`0x8005CAC8` → `0x8005D05C`) tire sa nouvelle position au `rand()` de la bibliothèque C (`0x80081E6C`, état `0x801EEB48`, constantes 0x41C64E6D/0x3039, rend `(s >> 16) & 0x7FFF`) et pose `posX = rand() / 102` (0 à 321) ; la DLL la branche sur `AlundraRandom` (`AlundraWorldProxy.cs:652` → `CellularLayerService.cs:397`, décision D7), le flux du jeu, que l'original ne touche pas là ; D7 et la décompilation (`GraphicManager.cs:1172`) se trompent. Confirmé par la découverte d'E19.m1 (un seul tirage, seulement quand `sy >= 240`, `0x8005D310` ; division signée, tronquée vers 0). | E19.m2 |
| O-E19-49 | **Flux de `rand()` et sauvegardes** (découverte d'E19.m1) : le `rand()` de la bibliothèque C n'a que trois appelants dans le binaire, la chute des cellules (`0x8005D31C`) et l'écriture du bloc de carte mémoire (`0x80061150` dans `0x80060E20`, `0x80061584` dans `0x8006122C` : 36 valeurs `r & 0xFF` à l'offset `0x1FB0` du bloc en `0x8018F078`, avant la somme de contrôle) ; les sauvegardes de la DLL passent par le service du moteur et ne tirent rien. À trancher : tirer 36 valeurs à chaque sauvegarde pour garder l'ordre du flux de la chute des cellules, ou consigner l'écart. | Auteur (E19.m2) |
| O-E19-50 | **Deux écarts des couches de cellules** (découverte d'E19.m1, relevés, non audités) : les deux routines du binaire dessinent à la position d'avant le bouclage (`0x8005CDB0`, `0x8005D3AC`), le moteur à celle d'après (`CellularLayerService.cs:349-350`, `:403-404`) ; le binaire avance d'un pas quand `|P| <` le compteur d'avant l'incrément, donc tous les `|P| + 2` ticks, le moteur tous les `|P|` ticks (`++Tick >= |P|`, `:303`, `:313`, `:364`, `:371` ; `CellularLayerServiceTests.cs:117-128` épingle la règle du moteur) ; la table des pas et une transformation éventuelle de la période par le convertisseur restent à vérifier. | E19.m2 |
| O-E19-51 | **Réglé le 2026-10-03 (E19.m3, ADR-0052 du moteur).** **Compteur des vagues global** (audit d'E19.m2) : `0x800C48C4` est un compteur unique, incrémenté par le pilote des fonds (`0x8005B6D8`-`0x8005B6E8`) **avant** les tests du masque, jamais remis à 0 au chargement ; le moteur en garde un par couche, remis à 0 au chargement et figé quand la couche est masquée : les vagues commencent chaque carte à une autre phase, et à la 475 la vague de l'original avance pendant le masque. Contredit en partie le fait d'E19.k2 (« l'état par tick d'une couche est dans l'appel gardé ») et ADR-0049 du moteur ; le corriger bougerait `CellularLayerMaskTests.cs:82`, `:93`, `:101`. Confirmé et planifié (§1.2s.4). | E19.m3 |
| O-E19-52 | **Réglé le 2026-10-03 (E19.m3, ADR-0052 du moteur).** **Parallaxe des cellules de type 0 tronquée** (audit d'E19.m2) : le binaire calcule le facteur une fois, en entier tronqué `Num/Den` (`0x8005C0AC`), puis `camX × facteur` (`0x8005CB78`) ; aux cartes 123 et 124 (couche 1), les facteurs 1/2 donnent 0 : 59 cellules ne défilent pas dans l'original, le moteur les fait défiler à camX/2. Confirmé et planifié (§1.2s.4). | E19.m3 |
| O-E19-53 | **Réglé le 2026-10-03 (E19.m3, ADR-0052 du moteur).** **Ordre de dessin des cellules d'une couche** (audit d'E19.m2) : le binaire insère chaque cellule en tête du même créneau de la table d'ordre (`0x8005CE08`) : ordre inverse, la cellule 0 dessus ; le moteur donne la même clé à toutes et trie par `List.Sort` (`SpriteRendererComponent.cs:399`), instable ; effet non mesuré. Confirmé et planifié (§1.2s.4). | E19.m3 |
| O-E19-54 | **La pluie de la 391 et de la 31 est opaque dans l'original** (audit d'E19.m3) : ses 720 texels ont le bit STP éteint (table de couleurs envoyée en `0x8005B2A0` égale à la palette extraite) et la PS1 ne mêle que les texels STP ; le portage la dessine à 50 % (couche `Average`, alpha 128 de la DLL) ; les texels des vagues ont tous le bit allumé (justes). Remède naturel : le mécanisme par texel de G2a étendu aux couches cellulaires (audit à faire). Audité et vérifié le 2026-10-05 : 11 couches fausses (la pluie des 391 et 31 trop transparente, la couche 1 des 41, 44, 109-114, 470 trop opaque) ; planifié (§1.2o.3) ; **livré le 2026-10-05** (G2c-1 à G2c-4, ADR-0035), recette G2c-6 en attente. | E19.g G2c |
| O-E19-55 | ~~Les fonds avancent pendant un fondu de passage~~ — **réglé le 2026-10-03 par E19.m4 (D-E19-67, ADR-0034), vérification en attente** ; question d'origine : (audit d'E19.m3) la boucle de transition du binaire (`0x8002C490`-`0x8002C4C0`) n'appelle pas `RenderScene` ; la DLL poussait les ticks des fonds sans condition (`AlundraWorldProxy.cs:2192-2196`) : 16 ticks en trop par départ (F0 à F15). | E19.m4 |
| O-E19-56 | **Le type 2 ignore le décalage de palette `0x800C490C`** (`0x8005D370`-`0x8005D394`), contrairement aux types 0 et 4 et aux tuiles ; à retenir si le cycle de palettes (O-E19-43) est porté. Et : le service du moteur survit à un retour au titre ; on n'a pas vérifié si l'original relance l'exécutable (et remet le compteur des vagues à 0) à ce moment. | Note |
| O-E19-57 | **Le binaire montre une image figée pendant le départ d'un passage** (audit d'O-E19-55) : la boucle de transition n'appelle ni `Update` ni `RenderScene` ; l'écran est une copie de la dernière image sous le fondu. Le portage continue d'avancer et de dessiner la scène : suivi de la caméra, animation des entités (le gel ne touche que `GameplayBlockedMask`, `AlundraWorldProxy.cs:2261-2266`, `AlundraGameplayFreeze.cs:38-40`), physique des PNJ, tuiles animées (temps réel), HUD, dialogue, fondu de la musique (non vérifié dans le binaire). Aussi : l'initialisation du fondu de type 0 du binaire remet la couleur courante à 0 (`0x80042F68`-`0x80042F70`), le portage la garde (`BeginWarpDepartureFade`). À trancher : figer la scène entière (capture de la dernière image par le moteur) ou garder l'écart. | Auteur |
| O-E19-58 | **La surcouche de la 293 (Inoa en feu) est additive dans l'original** (vérification d'O-E19-54) : `BGColorA` 2 → `TILE` d'ABR 1 de (50, 0, 0) ; le portage moyenne `(50, 0, 0, 128)` (le convertisseur ne garde que `OverlayEnabled = BGColorA != 0`, `BackdropReader.cs:254-255` ; le moteur dessine la teinte en `AlphaBlend`, `ScrollingLayerComponent.cs:208` ; la DLL pose α128, `AlundraBackdropStage.cs:295`) : la scène est assombrie de moitié et teintée au lieu d'être un peu rougie ; hors de la chaîne. | E19.g |
| O-E19-59 | **Octet de palette 30 des tuiles** (cartes 1, 13, 17, 153, 439, couche 1, tuiles 249-251) : le binaire lit l'identifiant de palette en `0x800CA24C`, au-delà de la table (l'entrée 3 de la table d'ordre du tampon de dessin 0) : valeur d'exécution, aucune correction statique juste ; le convertisseur pose la palette 0 (`BackdropImageBuilder.cs:79-82`), opaque. | Note |

## 4. Hors périmètre

- IA native (E14) : portage des objets de boutique, gestionnaire de destruction du créneau E (sauf ses index 0 et 1, E19.d2b),
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
