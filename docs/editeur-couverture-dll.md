# Couverture éditeur des fonctionnalités utilisées par la DLL Alundra

Date : 2026-09-01. But, fixé par l'utilisateur avant E12 : **la liste de ce que la DLL consomme et que
l'éditeur ne sait pas encore créer ou configurer**, pour ne rien oublier d'implémenter côté éditeur à
la fin. Audit par trois inventaires croisés (DLL, éditeur, aller-retours), chaque affirmation vérifiée
avec file:line — les références vivent dans les rapports d'agents ; ce document est la synthèse
actionnable.

Chaque case à cocher = un chantier éditeur à faire. Ordre : par dégât potentiel, pas par thème.

---

## 1. DANGER ACTUEL — des manipulations d'éditeur qui détruisent silencieusement

Ces points ne sont pas des « fonctionnalités manquantes » : ce sont des gestes **possibles aujourd'hui**
dans l'éditeur qui cassent le jeu sans un mot.

- [ ] **Renommer un monde (ou son fichier) casse événements, backdrop et musique de la carte.** La DLL
  résout ses compagnons par le nom du monde (`-{id}` final) via `Maps/world-index.json` — que l'éditeur
  ne voit ni ne maintient. Chaque chargeur dégrade en no-op loggé : le jeu tourne, la carte est morte.
  *À faire : le renommage de monde met à jour `world-index.json` et les dossiers compagnons — ou mieux,
  les compagnons deviennent des assets liés au monde (§2).*
- [ ] **Renommer l'entité `tileMap` ou les couches `Entities`/`Portals`/`MapEvents` tue le spawn de la
  carte** (avertissement en log seulement). La couche de navigation est mieux protégée (clé par rôle
  `CollisionOnly` + propriétés, pas par nom). *À faire : soit protéger ces noms dans l'éditeur, soit
  les remplacer par des rôles.*
- [ ] **« Remove » dans le navigateur de contenu SUPPRIME le fichier sur disque** — et les compagnons
  hors catalogue ne peuvent jamais être re-liés.
- [ ] **« Save project » réécrit le monde courant depuis l'état VIVANT**, après que
  `InitializeWithWorld` de la DLL a tourné (politique EditorPreview) : le transform racine vivant d'une
  entité référencée est baké dans `initial_local_transform`. Les entités spawnées à l'exécution ne
  sont PAS sauvées (bien) ; le risque est le repositionnement par le code d'init. *À faire : sauver
  depuis l'état de chargement, pas l'état vivant — ou geler l'init gameplay pour l'édition.*
- [ ] **La famille IA/steering est proposée dans le dialogue d'ajout de composant mais ne persiste
  RIEN** (`SteeringAgent`, `NavigationAgent`, ponts `CharacterController*Bridge`… — ni cas de save ni
  `Load`). Toute configuration posée dans l'éditeur est perdue en silence. *À faire : cas de
  sérialisation + Load, ou `[Browsable(false)]` en attendant.*
- [ ] Petite asymétrie : `Entity.Load` lit `updates_enabled`, la sauvegarde ne l'écrit jamais — un
  fichier annoté à la main le perd au premier save. (Absent des données converties.)

## 2. INVISIBLE PAR CONSTRUCTION — les compagnons hors système d'assets

La DLL lit ces fichiers **directement sur disque**, hors catalogue : l'éditeur ne peut ni les voir, ni
les créer, ni préserver leurs liens.

| fichier | consommé par | rôle |
|---|---|---|
| `Maps/**/events/*.events.json` | `EventProgramDocument` | tout le bytecode de la carte |
| `Maps/**/backdrop/*.backdrop.json` | `BackdropLoader` | parallaxe/fusion des fonds |
| `Maps/world-index.json` | les deux ci-dessus + musique | id de carte → chemin du monde |
| `Maps/music-index.json` | `AlundraMusicIndexTable` | carte → piste musicale |
| `Sounds/sfx-manifest.json` | `AlundraSoundBank` | id de bruitage → fichiers/boucles |
| `Musics/bgm-manifest.json` | `AlundraMusicPlayer` | index musical → wav/guid |
| `Data/sprite-records.json` | `SpriteRecordCatalog` | records/AnimSets/Sfx |
| (à venir : `strings.json`, `balance.json`, `control-codes`, `font3-charset`, `wind-sprites`) | E12/E13 | dialogues, HUD |

- [ ] Décision d'architecture à prendre le moment venu : **promouvoir ces compagnons en assets** (avec
  type, catalogue, éditeurs) ou donner à l'éditeur une **notion de fichier compagnon** suivie lors des
  renommages/déplacements. Sans l'un des deux, toute gestion de contenu par l'éditeur restera piégée.

## 3. SÉRIALISÉ MAIS SANS UI — l'aller-retour marche, rien ne l'édite

- [ ] **`script_class_name`** (monde ET entité) — le lien au proxy gameplay, préservé au save mais
  **aucun sélecteur** ; idem `space_policy`, `player_startup_settings_asset_id`,
  `gameplay_mode_asset_id`. C'est LE branchement Alundra par excellence.
- [ ] **`CharacterControllerSettings`** — sérialisation complète (les 20 clés), mais la grille
  générique n'édite pas une propriété de type classe : seul un booléen apparaît. Il faut un éditeur
  dédié (gravité, vitesses, masques de marchabilité…).
- [ ] **`ButtonsMapping`** — sérialiseur complet, **zéro** référence dans l'éditeur : ni création, ni
  route d'ouverture, ni panneau. Le mapping d'entrée d'Alundra est ineditable.
- [ ] **`.gameMode` (PlayerStartupSettings)** — aucun cas de sérialisation : un save **jette** (échec
  bruyant, pas de perte — mais ineditable).
- [ ] **Action de cutscene `FadeScreen`** — aller-retour sérialiseur/validateur complet (E10), mais
  invisible même dans l'inspecteur *lecture seule* (tombe dans le `default:` du builder, seuls
  `runtime_type` affiché). *Une ligne de builder à ajouter, puis l'authoring (§4).*

## 4. AUCUNE SURFACE ÉDITEUR DU TOUT

- [ ] **Cutscenes : lecture seule intégrale** — pas de création, pas d'édition, et `CutsceneAsset` n'a
  **pas de cas de sauvegarde** (toute tentative jette). Pour qu'E15 (programmes → cutscenes) ait un
  sens éditeur, c'est le chantier n°1.
- [ ] **Mondes : pas de création, d'ouverture ni de changement** — un seul monde chargé au démarrage
  (`FirstWorldLoaded`), non modifiable depuis l'UI.
- [ ] **Tilemap : visionneuse, pas de peintre** (zoom/pan/animation, zéro écriture). Et le jour où un
  peintre arrive, **piège majeur** : toute la vérité physique d'Alundra (marchabilité, hauteurs,
  pentes, murs) vit dans la propriété personnalisée opaque `AlundraCells`, qui fait l'aller-retour
  intact mais que peindre des tuiles ne mettra PAS à jour → visuel et collision désynchronisés.
- [ ] **Effets d'écran** (`ScreenEffectService`) — zéro référence éditeur : ni aperçu, ni réglage.
- [ ] **Audio : pas de mixer** (volumes de bus, master), pas d'assignation carte → musique. Seul le
  panneau `.sound` existe (création + inspection, complet).
- [ ] **Créations absentes du navigateur** : entité, sprite, monde, tileset, anim2d, cutscene,
  buttonsMapping (seuls Particle/Sound/presets existent). Routes d'ouverture absentes pour `.world`,
  `.tileset`, `.gameMode`, `.gameplayMode`, `.buttonsMapping`, `.texture`, `.dialogue`, entre autres.
- [ ] **Projet : pas d'UI de réglages** (`AlundraGame.json` — `FirstWorldLoaded`, `IsFixedTimeStep`…),
  bien que l'aller-retour soit fidèle sur les 13 champs.

## 5. CE QUI EST SOLIDE — vérifié, pas supposé

- **Le piège du sérialiseur est FERMÉ pour 100 % des données converties.** Les 9 types de composants
  présents dans les 396 prefabs + 483 mondes ont tous leur cas dédié, clés comparées une à une
  (les deux victimes historiques — `CharacterController` E3.d.0, `DepthSortable2D` e828affa — portent
  leurs commentaires). **Ouvrir et sauver un prefab ou un monde converti ne perd rien au niveau
  composant.**
- Survivent à un open-and-save : `.entity`, `.world`, `.sprite`, `.anim2d`, `.tileset` (y compris les
  `SourceX/SourceY` des quads miroir), `.tileMap` (avec `AlundraCells` et `Gravity`/`ZViscosity`),
  `.texture`, `.buttonsMapping`, `.sound` ; `AlundraGame.json` et `AssetInfos.json` fidèles ;
  `wav`/`png`/`tmj` intouchés par construction.
- `.sound` a la chaîne UI complète (création + panneau + save) — le gabarit à imiter pour le reste.
- `ScreenEffectComponent` est un composant de JEU, pas d'entité — il ne rouvre pas le piège.

## 6. Lecture d'ensemble

L'éditeur d'aujourd'hui est un **inspecteur fidèle** : il préserve remarquablement bien ce que le
convertisseur produit (le §5 est une vraie réussite), mais il ne sait **créer ou configurer presque
rien de ce qui fait le gameplay d'Alundra** — le lien aux proxies, les entrées, les réglages du
contrôleur, les cutscenes, la musique par carte, les effets d'écran — et il ignore l'existence même de
la moitié des données du jeu (les compagnons). Les trois gestes dangereux du §1 méritent d'être
traités avant toute session d'édition sérieuse du projet converti ; le reste est la feuille de route
éditeur de fin de chantier que ce document existe pour ne pas perdre.
