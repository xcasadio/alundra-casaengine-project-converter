# Plan E12.d — l'interaction joueur → entité (le bouton Carré face à un PNJ)

Date : 2026-09-01. Déclencheur : le re-verifier d'E12.a (avis A2) a constaté — et la session principale
a contre-vérifié — que **rien n'assigne `ActiveCollisionEntity`** (`AlundraWorldProxy.cs:239`), alors
que le pick du slot F l'exige (`AlundraEntityScriptProxy.cs:1060`). Conséquence : les dialogues d'E12.a
sont corrects au niveau harnais mais **injouables** — parler à un marin est impossible. L'intro ne
dispatche aucun opcode de dialogue (zéro 0x0D dans le golden), donc aucune partie d'E12 n'est validable
en jeu sans cette tranche.

Décisions utilisateur (2026-09-01) : **câbler l'interaction** ; périmètre = **détection seule, sans
blocage physique** (le joueur peut encore traverser les PNJ — écart assumé, le blocage entité↔entité
reste au chantier E14).

## §1 — Les faits, lus dans la décompilation (rien n'est supposé)

Toute la chaîne originale, vérifiée file:line dans `alundra-datas-analyser` :

1. **`Entity.XCollisionEntity` (offset 0x130) est écrit par la physique** :
   `PhysicsEngine.MoveEntity` (`PhysicsEngine.cs:71-84`) fait
   `entity.XCollisionEntity = ComputeXYPosition(entity, gameEngine)` — l'entité qui a bloqué le
   déplacement tenté, ou null. La détection de paire est `FindEntityCollisionCandidate`
   (@0x80036F34, `PhysicsEngine.cs:1169-1283`) : AABB **asymétrique** (delta négatif → dimensions de
   l'AUTRE entité +1 ; positif → les siennes +1, axes X=Width/Y=Height/Z=Depth), sur la liste
   `g_collideableEntities`, avec les portes `Flags & Collidable`, `AnimFlags & NoEntityCollision == 0`,
   `PlatformEntity == null`, plus un bypass de debug (non porté).
2. **`CheckEntityInteraction`** (@0x8002e910, `PlayerManager.cs:1597-1669`) : lit
   `player.XCollisionEntity` ; **latch d'interaction** (les variables `g_lastValidWarp*` — noms
   d'artefact du décompilateur, c'est un latch d'INTERACTION) : quand l'entité touchée porte
   `InteractRequiresButton` (0x8000), il mémorise entité + ses Pos + les Pos du joueur + la direction ;
   quand `XCollisionEntity` est null mais que TOUTES les positions mémorisées concordent encore et que
   le joueur fait toujours face, l'entité mémorisée compte encore. Puis : l'entité candidate doit avoir
   un programme F (`ProgramIndexes[5] != 0 || SpriteProgramIndexes[5] != 0`) ; sans
   `InteractRequiresButton` → **res=1** (interaction au contact) et `g_activeCollisionEntity = entité` ;
   avec le drapeau → il faut **Carré naissant** (`ButtonsJustPressed & 0x80`) → **res=2** et
   assignation ; sinon res=0 **sans effacer**.
3. **Le signal ne vit qu'UN TICK** : `MovePlayer` (@0x80031b50) fait
   `g_activeCollisionEntity = null` en **toute première instruction**, chaque tick, avant même la
   branche verrouillée (`PlayerManager.cs:23`). C'est ce qui empêche le programme F de se re-picker
   après fermeture du dialogue (aucun autre site d'effacement n'existe hormis l'entrée de carte,
   `GameEngine.cs:664`, et le constructeur).
4. **Sites d'appel** : cas Idle/Moving de `MovePlayer` (`PlayerManager.cs:361-383`), après
   `TryUseItem`/`PlayerTryAction` (non portés, no-ops documentés) : res==2 →
   `TargetAnimationId = Idle` et fin du traitement du tick ; res==1 → fin aussi (l'animation n'est pas
   remise à jour ce tick). Le cas nage (`:234`) n'est pas porté (pas de nage).
5. **Ordre dans le tick original** : `UpdateEntitiesEvents` (MovePlayer + pick + run) tourne **AVANT**
   `PhysicsEngine.UpdateEntitiesPhysics` (`EntityManager.cs:377-387`) — donc `MovePlayer` consomme le
   `XCollisionEntity` du tick **précédent**. Notre insertion (calcul en fin de passe proxy, consommé
   par le MovePlayer du tick suivant) reproduit exactement cette phase.
6. **Toute la passe d'entités est portée par `GameplayBlockedMask`** (`EntityManager.cs:377`) : boîte
   ouverte en mode MenuOpen, ni MovePlayer, ni pick, ni physique ne tournent. Notre pipeline n'a pas
   cette porte globale (E4.c n'a porté que la porte des map events) ; l'équivalent au site le plus
   étroit est de sauter le calcul d'interaction quand `GameplayBlockedMask` est posé.
7. **Pourquoi l'original ne referme pas la boîte avec l'appui qui l'ouvre** : l'avance
   (`ProcessEtcTextAdvance`) vit dans un callback de RENDU (`UIManager.Fun_80046ef0:118`, enregistré
   `StaticVariables.cs:11395`) qui est court-circuité pendant l'animation d'ouverture
   (`g_dialog_flags & 3`) et tant que le texte n'est pas décodé (`g_textPrimitives == 0` →
   `TextInterpreter`). Nous n'avons ni animation d'ouverture ni machine à écrire (E12.c) : **sans
   contre-mesure, le Carré qui ouvre la boîte la fermerait dans le même tick** (notre passe de frame
   tourne en fin d'Update avec le même instantané de pad).
8. **Les marins de la 389** (records 146 et 161, `Marin-passager-mouette-*`) portent
   `MoreFlags=0x80, CanPickup=0xa1` → Flags spawn `Collidable=OUI, InteractRequiresButton=OUI` — c'est
   la branche bouton (res=2) qui sera exercée en jeu.
9. **Côté DLL, l'infrastructure existe déjà** : la liste `_collidables` (E4.f,
   `AlundraWorldProxy.cs:160-164`, même règle de population que `g_collideableEntities`), les
   constantes `EntityFlags.Collidable`/`InteractRequiresButton` (`EntityFlags.cs:92/:143`), le pick F
   (`AlundraEntityScriptProxy.cs:1056-1064`) ; `BlockedByEntity` existe et reste null.
   **CORRECTION P1 de relecture (ronde 3) — le champ `XCollisionEntity` EXISTE DÉJÀ**
   (`AlundraEntityScriptProxy.cs:141`), typé **`Entity?` moteur**, cloné (`:1773`), jamais écrit en
   production (toujours null) mais avec DEUX consommateurs vivants : les recherches d'entités
   fonctions 7 et 8 (`EntitySearchService.cs:194-213`, comparaison via `LogicContextEntity`) et des
   tests commités qui le posent (`EntitySearchServiceTests.cs:173-189`,
   `AlundraNpcCharacterControllerMoverTests.cs:2045-2046`). **Disposition D-E12D-8** : le champ est
   **retypé `AlundraEntityScriptProxy?`** — l'original compare des entités UNIFIÉES (l'objet qui
   porte flags et programmes, c'est notre proxy) ; les cases 7/8 comparent alors les proxies
   directement (`ReferenceEquals(owner.XCollisionEntity, candidate)`), ce qui SUPPRIME un piège
   préexistant : avec un champ null et un `LogicContextEntity` null (proxies nus de harnais),
   `ReferenceEquals(null, null)` répondait vrai — appariement arbitraire. Conséquence de fidélité
   assumée : le champ du joueur devient vivant, donc **les fonctions 7/8 avec le joueur pour
   propriétaire deviennent atteignables** — c'est l'état de l'original (notre null permanent était la
   déviation) ; les six goldens sont l'oracle et §4 nomme ce point comme suspect désigné si l'un
   bouge. Un test épingle : joueur propriétaire, champ non nul → la case 7 rend exactement l'entité
   désignée, et aucun appariement nul-nul.

## §2 — Décisions de conception

- **D-E12D-1 (utilisateur) — détection seule** : `FindEntityCollisionCandidate` est porté verbatim
  (AABB asymétrique, portes, +1 inclus — ce `+1` est *dérivé* : `dif < dim + 1` ⇔ `dif <= dim`, le
  contact affleurant compte) mais **aucun blocage** : le déplacement du joueur reste au mover moteur.
  Écart documenté : l'interaction se déclenche en chevauchement plutôt qu'en butée ; le joueur
  traverse toujours les PNJ (comme aujourd'hui). Le port est écrit comme une **fonction dédiée**
  (règle n°3 des ÉCHELLES : ne pas réutiliser un helper d'EntitySupport sans comparaison ligne à
  ligne — l'original a une fonction distincte, on la porte distincte).
  **CORRECTION P2 de relecture — la source de position n'est PAS `ModdedPos*`** : l'original lit
  `ModdedPosX/Y/Z`, rafraîchis à chaque tentative de déplacement (`PhysicsEngine.cs:428-430/:849-851`) ;
  dans notre DLL ces champs ne sont écrits **qu'au spawn** (`AlundraEntitySpawnFactory.cs:625-627`) et
  dans le clone — un port littéral comparerait des positions de spawn et ne détecterait jamais rien
  (passe verte et inerte, la classe de défaut que cette tranche existe pour fermer). Le port recalcule
  **`Pos* + Mod*` à la volée pour le sujet ET le candidat**, la convention établie
  d'`EntitySupport.cs:112-114`, dérivation écrite au code. Le test de contact vérifie la détection
  **après** que le héros a quitté sa position de spawn (cas qu'une implémentation à `ModdedPos*`
  figés rendrait null).
- **D-E12D-2 — le calcul vit dans la passe proxy de fin de tick** (à côté d'`EvaluateEntitySupport`) :
  `player.XCollisionEntity = FindEntityCollisionCandidate(player)` une fois par tick logique, position
  courante. Fidélité de phase : l'original calcule en physique (après les événements), consommé par le
  MovePlayer du tick suivant — identique chez nous (§1.5). Champ `XCollisionEntity` ajouté au proxy
  d'entité (copie/reset alignés sur les sites existants d'`Entity.cs:214/:365`).
- **D-E12D-3 — `CheckEntityInteraction` porté verbatim, latch compris**, dans `AlundraPlayerManager`.
  Le **latch vit sur `AlundraGameState`** (déjà paramètre de `MovePlayer`) : c'est la portée des
  globales originales (leçon E11.c « l'état de garde vit où vit l'état de l'original »), et ses huit
  comparaisons de position l'auto-invalident — y compris une coïncidence entre cartes, exactement
  comme l'original. `ActiveCollisionEntity` reste sur le proxy de monde (le champ existe, `:239`),
  écrit à travers `IAlundraScriptHost` (get/set) ; sa remise à zéro d'entrée de carte reproduit
  `GameEngine.cs:664`. Dérivations écrites au code.
- **D-E12D-4 — le signal est CONSOMMÉ AU PICK, pas effacé en tête de MovePlayer** (CORRECTION P2 de
  relecture — la transposition littérale de `PlayerManager.cs:23` est fausse dans notre pipeline).
  Chez l'original, frame == tick 50 Hz : « effacé en tête du MovePlayer suivant » ≡ « consommé par
  l'unique pick du tick ». Chez nous, `MovePlayer` tourne **une fois par frame RENDUE, hors boucle de
  ticks** (`AlundraEntityScriptProxy.cs:956-967`), et le pick tourne **dans** la boucle
  `ticksThisFrame` (0 à 4, `:889-893`). Un effacement en tête produirait deux divergences observables :
  frame à 0 tick → l'appui est perdu en silence (PNJ sourds par intermittence) ; frame à N ticks → une
  assignation lue par N picks (N programmes F, têtes tournées et réouvertures). **Règle retenue,
  équivalente par dérivation** : l'assignation persiste jusqu'à sa **consommation par le premier pick
  qui choisit F** (le pick efface l'entité active au moment où il la choisit) ; plus l'effacement
  d'entrée de carte existant. Une frame à 0 tick reporte simplement la consommation à la frame
  suivante (aucun appui perdu) ; une frame à N ticks ne donne qu'UN pick F (les ticks suivants voient
  null). Une assignation jamais consommée (entité détruite dans la fenêtre d'une frame) reste inerte —
  `ReferenceEquals` sur des proxies non poolés ne peut désigner personne d'autre — et l'assignation
  suivante ou l'entrée de carte l'écrase ; dérivation écrite au code. Test dédié : une frame à 0 tick
  puis une frame à ≥2 ticks encadrant un appui → exactement UN pick F, ni appui perdu ni double
  ouverture.
- **D-E12D-5 — porte `GameplayBlockedMask` sur le calcul d'interaction** (le `XCollisionEntity` du
  proxy ET `CheckEntityInteraction`) : équivalent au site le plus étroit de la porte globale
  `EntityManager.cs:377` que notre pipeline n'a pas. Sans elle, le Carré de fermeture re-déclencherait
  un pick F boîte ouverte (0x0D en retry inoffensif, mais 0x27 re-tournerait la tête du PNJ — et ce
  serait un écart).
- **D-E12D-6 — l'appui d'ouverture est avalé par le directeur** : `Open()` note si Carré est déjà
  naissant dans l'instantané de pad courant ; le premier `Tick()` ignore alors le bouton (la minuterie
  compte normalement). Fenêtre STRICTEMENT plus courte que la suppression originale (animation
  d'ouverture + décodage, §1.7) — dérivation au code. Sans ça, toute boîte d'une page ouverte au
  bouton se refermerait dans le même tick (T4 le prouve).
- **D-E12D-7 — les deux branches res=1/res=2 sont portées** (contact et bouton), y compris
  `TargetAnimationId = Idle` sur res=2 et l'arrêt du traitement du tick. Les marins n'exercent que
  res=2 ; res=1 est couvert par test synthétique (un record sans le drapeau).

## §3 — Tranche unique (DLL seule), surface de test COMPLÈTE et mutations

**RÉVISION P1 de relecture** : ma première rédaction envoyait T1 dans un harnais où il est
**impilotable** — `HeadlessIntroSimulation` fige `ActiveCollisionEntity => null`
(`IntroTraceHarnessTests.cs:1177`) et `PlayerController => null` (`:1189`), ce qui fait de la branche
joueur (donc de `MovePlayer`) un no-op documenté, et `RunFrame` n'appelle jamais
`AlundraWorldProxy.Update`. La surface est donc **à deux étages, le patron exact du correctif F1
d'E12.a** : les sites d'appel de production épinglés par des tests au niveau du proxy, ET le flux
complet dans le harnais élargi dont le miroir est adossé à ces tests de site.

**Fichiers de production** :
- `Alundra/Scripts/AlundraEntityCollision.cs` (nouveau) — port de `FindEntityCollisionCandidate`
  (D-E12D-1, positions recalculées `Pos*+Mod*`).
- `Alundra/Scripts/AlundraEntityScriptProxy.cs` — `XCollisionEntity` retypé
  `AlundraEntityScriptProxy?` (D-E12D-8, champ existant `:141`, clone `:1773` suivi) ; la branche
  joueur passe `ScriptHost` à `MovePlayer`.
- `Alundra/Scripts/EntitySearchService.cs` — cases 7/8 (`:198/:210`) comparent les proxies
  directement (D-E12D-8).
- `Alundra/Scripts/AlundraPlayerManager.cs` — `MovePlayer` gagne le paramètre hôte ;
  `CheckEntityInteraction` (port verbatim + porte D-E12D-5) appelé au cas Idle/Moving.
- `Alundra/Scripts/IAlundraScriptHost.cs` — `ActiveCollisionEntity` passe de get seul à **get/set**
  (l'écriture de `CheckEntityInteraction` passe par l'hôte).
- `Alundra/Scripts/AlundraWorldProxy.cs` — implémentation get/set (`:1539`), la passe de fin de tick
  (`player.XCollisionEntity = …` à côté des passes fondu/dialogue), reset d'entrée de carte.
- `Alundra/Scripts/AlundraGameState.cs` — le **latch** (9 valeurs de D-E12D-3) y vit : c'est déjà un
  paramètre de `MovePlayer`, et sa portée est celle des globales originales (leçon E11.c) ; les huit
  comparaisons de position l'auto-invalident, y compris entre cartes.
- `Alundra/Scripts/AlundraDialogueDirector.cs` — avale-appui (D-E12D-6).

**Contrat du paramètre hôte de `MovePlayer`** (RÉVISION P2, ronde 2) : le paramètre est
**obligatoire, de type `IAlundraScriptHost?`, sans valeur par défaut** — chaque site d'appel doit
écrire quelque chose. `null` = « pas de monde » : `CheckEntityInteraction` est sauté (dégradé
documenté, le même sens que partout ailleurs dans la DLL), ce qui laisse les ~19 appels directs des
tests de déplacement passer `host: null` EXPLICITEMENT (aucun saut silencieux par défaut — la famille
« vert et inerte » exige que le saut soit visible au site) ; le chemin de production passe
`ScriptHost` et il est épinglé par P-b + la mutation n°3.

**Fichiers de test touchés** (le get/set d'`IAlundraScriptHost` touche TOUS ses implémenteurs ; la
signature de `MovePlayer` touche tous ses appelants directs — listes vérifiées par grep, ronde 2) :
- Auto-propriété `ActiveCollisionEntity` (14 hôtes factices) : `IntroTraceHarnessTests.cs:1177`
  (+ branche joueur opt-in, voir T1), `HeroTraceHarnessTests.cs:209`,
  `AlundraCharacterControllerAdoptionTests.cs:146`, `AlundraFloorHeightTests.cs:119`,
  `AlundraEventProgramRunnerTests.cs:2205`, `AlundraLadderClimbTests.cs:156`,
  `AlundraGroundSlopeTests.cs:133/:384`, `AlundraTileHeightAtOffsetTests.cs:154`,
  `AlundraNpcCharacterControllerMoverTests.cs:273/:311/:2134/:2621`,
  `AlundraWorldProxyLogicClockTests.cs:32`.
- Sites d'appel directs de `MovePlayer` (passent `host: null`) : `AlundraPlayerManagerTests.cs`
  (13 sites) et `AlundraLadderClimbTests.cs` (6 sites).
- Retypage D-E12D-8 : `EntitySearchServiceTests.cs:173-189` et
  `AlundraNpcCharacterControllerMoverTests.cs:2045-2046` posent désormais des proxies (+ le test
  d'épinglage 7/8 « joueur propriétaire » de §1.9).
- Nouveaux : `AlundraInteractionPassTests.cs` (étage proxy), `AlundraEntityCollisionTests.cs` (AABB),
  + T1/T-cadence dans `AlundraDialogueOpcodesProductionTests.cs`.
**Critère de complétude exécutable** : un grep `ActiveCollisionEntity =>` et
`AlundraPlayerManager.MovePlayer(` sous `Alundra.Tests` ne rend AUCUN fichier absent de cette liste
(à re-vérifier à l'exécution — c'est le contrôle, pas la liste, qui fait foi).

**Étage 1 — les sites de production, au vrai `Update`** (`AlundraInteractionPassTests`, patron
`AlundraDialogueFramePassTests`) :
- **P-a** : `AlundraWorldProxy.Update` réel, joueur + entité chevauchée injectés par les seams
  internes du proxy (le champ `:239` est déjà « settable internally for tests » ; `PlayerEntity` et la
  liste des collidables reçoivent le même traitement interne si nécessaire) → après un tick,
  `player.XCollisionEntity` désigne l'entité ; après l'avoir ÉLOIGNÉE de sa position de spawn, la
  détection suit (tue la mutation `ModdedPos*` figés).
- **P-b** : la branche joueur réelle d'`AlundraEntityScriptProxy.Update` (hôte de test avec
  `PlayerController` non nul) + Carré naissant + `XCollisionEntity` posé → `ActiveCollisionEntity`
  assigné via l'hôte ; sans appui → rien ; pendant `GameplayBlockedMask` → rien (T2).

**Étage 2 — le flux complet en vrai monde 389** (`HeadlessIntroSimulation` élargi) :
- `ActiveCollisionEntity` devient une **auto-propriété** ; un paramètre opt-in (défaut OFF — les six
  goldens construisent le sim sans lui et ne changent pas d'un octet) fournit un `PlayerController`
  réel à la branche joueur et fait tourner, par frame, le miroir de la passe de contact — miroir
  légitime PARCE QUE l'étage 1 épingle les deux sites de production (le contrat F1).
- **T1** : héros déplacé au chevauchement du marin 13, Carré naissant une frame → le pick RÉEL choisit
  F, 0x27 oriente le marin, la boîte s'ouvre (MenuOpen posé) **et reste ouverte en fin de tick**
  (avale-appui) ; des frames passent ; Carré → fermeture ; **aucune réouverture** ensuite
  (consommation au pick, D-E12D-4).
- **T-cadence** (D-E12D-4) : l'appui tombe sur une frame à 0 tick → non perdu, consommé à la frame
  suivante ; une frame à ≥2 ticks → exactement UN pick F, une seule ouverture.
- **T3 — res=1 synthétique** : entité chevauchée `Collidable` SANS `InteractRequiresButton` avec
  programme F → interaction sans bouton.
- **T5 — le latch** : entité à bouton mémorisée au contact ; `XCollisionEntity` forcé null, positions
  intactes → l'appui interagit encore ; l'entité déplacée d'un pixel → plus rien.
- **T4 — l'avale-appui isolé** (unité directeur) : `Open()` avec Carré déjà naissant → le premier
  `Tick()` n'avance ni ne ferme ; un Carré naissant au tick suivant ferme ; la minuterie n'est pas
  suspendue.

**Mutations imposées — construct supprimé → assertion qui tombe** (chacune vérifiée mordante) :
1. La consommation au pick (`AlundraEntityScriptProxy` ~:1060) → T1 (« aucune réouverture ») et
   T-cadence (double ouverture).
2. **Le SITE D'APPEL de la passe de contact dans `AlundraWorldProxy.Update`** → P-a (jamais de
   contact) — la mutation « site supprimé » exigée par la relecture.
3. **Le SITE D'APPEL de `CheckEntityInteraction` dans `MovePlayer`** → P-b et T1 (la boîte ne s'ouvre
   jamais).
4. L'avale-appui (`AlundraDialogueDirector.Open`) → T4, et T1 (fermée le tick de l'ouverture).
5. La porte `GameplayBlockedMask` (`CheckEntityInteraction`) → P-b/T2.
6. AABB symétrisé (dimensions du seul sujet) → cas d'`AlundraEntityCollisionTests` dimensionné pour
   que la symétrisation change le verdict (candidat plus large que le sujet, delta négatif).
7. Positions figées (`ModdedPos*` au lieu de `Pos*+Mod*`) → P-a (détection après déplacement).

## §4 — Acceptation

Suites au vert (`Alundra.Tests` 693+n, convertisseur 141, moteur inchangé — aucun fichier moteur
touché) ; **six goldens byte-identiques avec preuve d'exécution** (les traces héros passent par
`MovePlayer`, dont la signature change — comportement identique attendu, aucun PNJ ne chevauche le
héros dans les scénarios dorés ; l'élargissement du sim d'intro est opt-in, défaut OFF ; si un golden
bouge, ARRÊT — **le suspect désigné est alors D-E12D-8** : le `XCollisionEntity` du joueur devenu
vivant rend les recherches 7/8 à propriétaire-joueur atteignables, fidélité voulue mais à analyser
avant tout re-baseline) ; verifier de clôture ;
puis **validation utilisateur en jeu** : laisser l'intro se terminer, marcher contre un marin,
appuyer Carré → dialogue en font3 ; marin 12 → choix OUI/NON navigable, les deux branches.

**Budget** : un commit DLL unique ; ≤ 4 tours. Exécution directe en session principale (un seul chemin
couplé : pick + directeur + player manager — pas de fan-out), verifier de clôture avant commit.

**Différés nommés** : blocage entité↔entité (E14) ; branche nage de l'interaction (pas de nage) ;
`BlockedByEntity` toujours null (préexistant) ; l'avis A1 d'E12.a (appui reconsommé par tick en frame
à rattrapage) reste différé à E12.c — l'avale-appui de D-E12D-6 n'y touche pas.
