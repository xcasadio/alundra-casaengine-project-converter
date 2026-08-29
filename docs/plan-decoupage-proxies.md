# Plan — Découpage d'`AlundraWorldProxy` et `AlundraEntityScriptProxy`

Date : 2026-08-28. Demande de l'utilisateur : « les classes sont devenues trop grosses, il faut les
découper, extraire des responsabilités dans d'autres classes ». **Refactoring à comportement
constant** : aucune modification de logique, aucun changement d'ordre d'exécution.

## 0. Décisions de l'utilisateur (2026-08-28, ne pas re-débattre)

| # | Décision |
|---|---|
| R-1 | **Périmètre « statique seul »** : on extrait les grappes **100 % `static`** d'`AlundraWorldProxy` (fabrique d'entités, passes de synchronisation par frame, mathématiques de caméra) et les sondeurs de terrain statiques d'`AlundraEntityScriptProxy`. **Hors périmètre** : le regroupement des 131 champs du proxy d'entité, son `Clone()`, et la câblerie d'instance de `Update()` (caméra vivante, backdrop). |

## 1. Faits qui bornent le plan (mesurés, pas supposés)

**Les fichiers sont gros surtout de documentation** (compté en session principale) :

| fichier | lignes | doc XML | commentaires | vides | **code** |
|---|---|---|---|---|---|
| `AlundraWorldProxy.cs` | 2948 | 1088 | 325 | 307 | **1228** |
| `AlundraEntityScriptProxy.cs` | 1896 | 731 | 302 | 125 | **738** |

Un découpage **déplace donc surtout de la prose**, et la prose doit voyager **avec** le code qu'elle
explique (ces docs citent des `file:line` de la décompilation : les séparer les rendrait fausses).

**`AlundraWorldProxy` se découpe bien** : 41 de ses ~60 membres sont `static` et ne touchent aucun
état d'instance (~1060 lignes). Son `Clone()` (`:2920-2926`) **ne recopie rien** — il rend un
`new AlundraWorldProxy()` nu. Le piège du clone y est donc **inversé** : le danger n'est pas d'oublier
de copier un état, c'est d'introduire un collaborateur partagé, statique ou injecté au constructeur
au lieu d'être créé par instance.

**`AlundraEntityScriptProxy` se découpe mal** : 131 champs d'instance, un `Clone()` de 118 entrées
qui **omet déjà 10 champs en silence** (une entrée manquante dans un initialiseur d'objet est du C#
légal, le compilateur n'aide pas), ~200 sites d'accès en production et **905+ dans les tests**. En
revanche ses *méthodes* sont bon marché (4 sites de production). D'où R-1.

**Contraintes dures (violées = casse silencieuse)** :

1. **Aucune des deux classes ne peut être RENOMMÉE.** Leurs **noms simples** sont inscrits dans
   **878 fichiers d'assets commités** (483 `.world` + 395 `.entity`, clé `script_class_name`), plus
   deux constantes du convertisseur. `ElementFactory` les résout par nom simple, insensible à la
   casse. Un renommage **compile** et casse au chargement d'asset. En revanche un **déplacement de
   fichier ou de namespace est sans risque** (résolution par `Type.Name`) — et n'apporte rien, donc
   on n'en fait pas.
2. **Collision de noms simples** : `ElementFactory.BuildTypeCache` groupe par `Type.Name` sur
   **toutes** les assemblies chargées, premier arrivé gagne. Toute classe extraite doit porter un nom
   globalement improbable → **règle : préfixe `Alundra`** (convention déjà dominante du dossier).
   Vérifié : `AlundraEntitySpawnFactory`, `AlundraFrameSyncPasses`, `AlundraCameraMath`,
   `AlundraTerrainProbe` n'existent nulle part dans le dépôt.
3. **Deux noms de membres sont inscrits verbatim dans les quatre traces dorées du héros** —
   `AlundraWorldProxy.LogicTicksThisFrame` et `AlundraWorldProxy.AdoptPlayerPawn` (const du harnais,
   écrite dans l'en-tête des traces). **Ces deux membres restent sur `AlundraWorldProxy`.**
4. **Visibilité** : 16 membres appelés par les tests sont `internal static`, atteints via
   `InternalsVisibleTo`. Les classes extraites sont donc `internal static`, jamais `private` ni
   `file`-scoped, et **on n'en profite pas pour les passer `public`** (élargirait le contrat pour rien).
   **Règle d'élargissement (seul changement de visibilité autorisé, et il ne compte PAS comme un
   changement de signature au sens du §2)** : un membre `private static` qui part dans une autre
   classe alors que des appelants restent sur la classe d'origine devient `internal static`, jamais
   `public`. Membres nommément concernés, à vérifier un par un : les quatre échantillonneurs de R4
   (`AlundraEntityScriptProxy.cs:1221, :1322, :1554, :1563`), `TryGetRecordInt` (`:912`),
   `ResolveMapGravitySettings` (`:1241`, encore appelé par `AdoptPlayerPawn` qui reste),
   `IdsvDirectionStride` (`:756`) et `AnimationFinishedHandler` (`:846`). Aucun autre modificateur
   d'accès ne bouge dans ce chantier.
5. **Ne pas élargir `IAlundraScriptHost` ni `IEntityWorldContext`** : 19 implémentations factices
   dans les tests en dépendent. Aucune extraction de ce plan n'en a besoin.

**Le filet de sécurité, honnêtement** (correction d'une affirmation initiale fausse) :
**aucun test n'assère l'identité des goldens** — les deux harnais font un `File.WriteAllText`
inconditionnel sans jamais relire la version commitée. Ce qui discrimine réellement :
(a) les assertions chiffrées dans les harnais (frames 554/555/678/801/1034/1202/1704, valeurs), et
(b) **un `git status --short docs/` manuel après exécution**. De plus `IntroTraceHarnessTests`
**se saute en silence** si `alundra-project/` est absent, et ce dossier est **gitignoré**.

**La zone la plus dangereuse est hors périmètre, et c'est voulu** : `AlundraWorldProxy.Update(float)`
n'est appelé par **aucun test** — les deux harnais réimplémentent la boucle de frame à partir des
passes statiques. Son ordre par frame est porteur de sens et n'est protégé par rien. C'est
précisément là que vivent la caméra d'instance et le backdrop : R-1 les laisse tranquilles.

## 2. Enveloppe

- **Résultat** : `AlundraWorldProxy.cs` passe de 2948 à ~1900 lignes et `AlundraEntityScriptProxy.cs`
  perd ses sondeurs statiques, par **déplacement pur** de membres déjà `static` vers quatre classes
  `internal static` nommées par responsabilité. Aucun comportement ne change.
- **Non-objectifs** : tout ce qu'exclut R-1 ; tout renommage ; tout déplacement de namespace ; toute
  modification de signature ; tout « nettoyage » opportuniste du code déplacé.
- **Propriétaires** : `Alundra/Scripts/` et `Alundra.Tests/` (repo parent). Moteur et convertisseur
  intouchés.
- **Acceptation globale** : build `alundra-casaengine-project-converter.slnx -c Release` 0 erreur ;
  `Alundra.Tests` **589** verts ; convertisseur **138** ; **preuve positive d'exécution des deux
  harnais** (ci-dessous) ; `git status --short docs/` **vide** sur les six goldens ; aucun fichier
  moteur touché.

**Preuve positive d'exécution — sans elle l'acceptation ne prouve rien** (correction de relecture).
Les deux harnais se sautent par un `return;` nu, **sans statut « skipped »** : un harnais non exécuté
rend donc exactement le même vert qu'un harnais exécuté, et laisse `docs/` tout aussi propre. « 589
verts + `docs/` vide » est **précisément la signature d'un harnais qui n'a pas tourné**. Deux pièges
aggravants : la garde réelle est `Directory.Exists(<racine>/alundra-project/**Maps**)`
(`IntroTraceHarnessTests.cs:65`, `HeroTraceHarnessTests.cs:103`), pas la simple présence
d'`alundra-project/` — et `Alundra.csproj:24` **crée ce dossier à chaque build**, donc « le dossier
existe » est toujours vrai et ne prouve rien.

Chaque tranche rapporte donc :
1. `alundra-project/Maps/` existait au moment du run (la vraie garde) ;
2. **les six fichiers dorés de `docs/` ont une date de modification postérieure au début du run** —
   les harnais les réécrivent inconditionnellement (`File.WriteAllText`), donc une mtime fraîche est
   la preuve qu'ils ont réellement tourné ;
3. `git status --short docs/` vide **sur les six goldens** — ce point ne compte comme preuve
   qu'**après** le point 2, et les éventuelles modifications de ce document de plan doivent être
   commitées ou exclues avant de lire ce statut.

**Contrôle négatif à exécuter une fois** (pas à chaque tranche) : dans un checkout où
`alundra-project/Maps` est absent, le point 2 doit **échouer** — sinon la preuve d'exécution est
elle-même vide.
- **Rollback** : une tranche = un commit, revert simple.

## 3. Règle de preuve propre à ce chantier

Un refactoring à comportement constant se prouve autrement qu'une fonctionnalité. En trois points,
parce que la formulation naïve (« corps octet-identique ») est à la fois **inapplicable** — un corps
déplacé doit gagner des qualifications d'appel — et **insuffisante** : elle ne couvre ni les corps qui
restent, ni les deux dérives silencieuses ci-dessous.

1. **Delta autorisé sur un corps déplacé** : **qualification d'appel** (`Foo()` →
   `AutreClasse.Foo()`) et **élargissement `private` → `internal`** (§1 contrainte 4). Rien d'autre.
   La comparaison avant/après se fait **modulo ce delta**, explicité membre par membre dans le
   rapport de tranche. Toute autre retouche est une **condition d'arrêt**.
2. **Les corps qui RESTENT sont le vrai risque de saisie**, pas ceux qui partent : `Update`,
   `UpdateCameraFollow`, `AdoptPlayerPawn` et `AlundraEntityScriptProxy.Update` doivent être
   requalifiés. Chaque tranche liste les corps restants qu'elle a touchés et montre que le seul
   changement y est la qualification.
3. **Deux dérives qu'un corps identique ne prouve PAS** : (a) **résolution différente** si le nouveau
   fichier n'a pas le même bloc `using` — chaque tranche produit donc un **diff des `using`** entre
   fichier source et fichier cible, qui doit être vide ; (b) **ordre d'initialisation statique** —
   deux champs statiques aujourd'hui portés par le même type s'initialisent ensemble ; les séparer
   change ce moment. Concerné : `AnimationFinishedHandler` (`:846`, part en R3) face à
   `DebugCameraPanEnabledFromEnvironment` (`:156`, reste). R3 doit le signaler explicitement et
   justifier que rien ne dépend de cet ordre.

Corollaire : **pas de méthodes de façade** laissées sur `AlundraWorldProxy` pour éviter la mise à
jour des appelants. Le compilateur doit signaler **tous** les sites : c'est le filet de ce
refactoring, et le masquer par des façades le supprimerait.

Chaque tranche rapporte donc, en plus des suites : la comparaison normalisée par membre déplacé, le
diff des `using` (vide), la liste des corps restants requalifiés, et **la liste nommée des tests
couvrant les membres déplacés** qui ont été exécutés.

## 4. Tranches (ordre = coût croissant, mesuré en sites de test à mettre à jour)

### R1 — `AlundraFrameSyncPasses` ✅ `fb35d90` (~248 lignes, ~34 sites)

Déplacer le bloc contigu des passes par frame : `RunAnimationSyncPass`, `SyncAnimation`,
`RunTransformSyncPass`, `SyncTransform`, `RunWallInterleaveSortKeyPass`, `TryResolveAnimationTarget`,
`TrySelectAnimationByNameSuffix`.

**Les deux dépendances croisées vers la grappe R3 ne se traitent PAS de la même façon** (correction
de relecture — le plan les confondait, et la tranche ne compilait pas) :

| dépendance | état | traitement en R1 | après R3 |
|---|---|---|---|
| `ResolveLogicalPosition` (`:1251`, lue en `:2587`) | déjà `internal static` | référencée `AlundraWorldProxy.ResolveLogicalPosition` | devient `AlundraEntitySpawnFactory.ResolveLogicalPosition` |
| `IdsvDirectionStride` (`:756`, lue en `:2626`) | **`private const`** — inatteignable depuis une autre classe | **passe `internal const`** sur `AlundraWorldProxy` (règle d'élargissement, §1.4), référencée `AlundraWorldProxy.IdsvDirectionStride` | déménage en `AlundraEntitySpawnFactory` (ses 3 autres lectures `:775, :807, :885` y partent) ; la référence de `AlundraFrameSyncPasses` devient `AlundraEntitySpawnFactory.IdsvDirectionStride` |

`IdsvDirectionStride` est donc **touché deux fois**, volontairement : c'est le prix de l'ordre
« moins cher d'abord », et les deux touches sont mécaniques et vérifiées par le compilateur.
**Vérification de tranche** : `dotnet build … -c Release` = 0 erreur au commit de R1 **seul**, sans R3.

### R2 — `AlundraCameraMath` ✅ `eefa292` (~150 lignes, ~52 sites)

Déplacer **la seule mathématique pure** de caméra : `StepCameraScroll`, `AdvanceCameraSmoothing`,
`ComputeSmoothedCameraTarget`, `ResolveCameraLookAt`, `ComputeCameraLookAtRenderPosition`,
`ClampCameraTargetToMap`, `ComputeCameraZoom`, `ComputeDebugCameraPanOffset`, `ResolveDebugCameraBase`,
avec les constantes que ces seules méthodes lisent. **Restent sur le proxy** : `UpdateCameraFollow`,
`UpdateDebugCameraPan`, `ResolveDebugCameraOnce`, `SetForcedCameraLookAt` et les champs de caméra —
c'est la câblerie d'instance non couverte, explicitement hors périmètre.

### R3 — `AlundraEntitySpawnFactory` ✅ `3bb6755` (~635 lignes, ~69 sites, 10 fichiers de test)

La plus grosse et la plus payante : `ShouldSpawnRecord` (×3), `BuildIdsvByAnimDirection`,
`BuildAnimationEndByAnimDirection`, `SubscribeAnimationEndBridge`, `OnAnimationFinished`,
`TryGetRecordInt`, `CreateEntityFromRecord`, `TryGetPrefabAssetId`, `CreateEntityFromPrefab`,
`CreateBareEntityFromRecord`, `ApplyRecord`, `ApplySpawnInitialization`, `SetEntityDimensions`,
`BuildEntityName`, `ResolveLogicalPosition`, `ResolveMapGravitySettings`, plus `IdsvDirectionStride`
et le handler statique d'animation. Touche les deux harnais de trace : **exécuter les deux et
vérifier `git status docs/` après**.

### R4 — `AlundraTerrainProbe` ✅ `df6cf0e` (proxy d'entité, ~51 lignes de code)

Les quatre échantillonneurs de coin `private static` qui ne touchent aucun état d'instance :
`SampleTerrainHeightCorner`, `ProbeSlopeCorner`, `SampleRawTileHeightCorner`, `SampleGroundCorner`.
**Rien d'autre** : les méthodes d'instance du terrain (`ClampToGround`, `ComputeTerrainHeight`,
`UpdateGroundSlope`, `UpdateFloorHeight`, `GetTileHeightAtOffset`) lisent `Owner` — `protected` sur
`GameplayProxy` — et forment un **cycle** avec le pont contrôleur
(`EvaluateEntitySupport` → `PushLogicalPositionToRoot` → `ClampToGround`) : elles ne peuvent pas
devenir un collaborateur indépendant sans casser ce cycle, ce que R-1 exclut.

### R5 — Clôture ✅ `d29fd63`

Mise à jour des `<see cref>` cassés par les déplacements. **Attention** : `GenerateDocumentationFile`
n'est pas activé, donc le compilateur **ne signale jamais** un cref mort — il en existe déjà trois
avant ce chantier. C'est une passe explicite, pas un effet de bord espéré.

## 5. Réalisé — résultat et écarts (2026-08-28/29)

**Les cinq tranches sont livrées.** `AlundraWorldProxy.cs` **2948 → 1749 lignes (−41 %)** ;
`AlundraEntityScriptProxy.cs` 1896 → 1838. Quatre classes créées, toutes `internal static` et
préfixées `Alundra` : `AlundraFrameSyncPasses` (284 l.), `AlundraCameraMath` (334 l.),
`AlundraEntitySpawnFactory` (697 l.), `AlundraTerrainProbe` (88 l.). Suites finales :
`Alundra.Tests` **589**, convertisseur **138**, build 0 erreur, six goldens inchangés à chaque
tranche avec preuve d'exécution.

- **Écart corrigé en session principale (R3)** : l'exécuteur avait laissé `TryGetRecordInt` et
  `ResolveMapGravitySettings` sur le proxy en les élargissant, au lieu de les déplacer — et rapporté
  « aucun écart ». Résultat : la fabrique extraite **rappelait sept fois** la classe dont elle
  venait, soit un **cycle** entre les deux, dans un refactoring dont le but est de découpler.
  Déplacés, appelants requalifiés des deux côtés ; la fabrique ne mentionne plus le proxy que dans
  des commentaires.
- **Méthode qui a payé en R5** : activer temporairement `GenerateDocumentationFile` transforme chaque
  cref mort en avertissement `CS1574` — le compilateur trouve ce qu'un grep rate. Décisif ici : la
  première passe n'en a corrigé qu'un tiers, parce que la plupart des références étaient écrites
  `cref="AlundraWorldProxy.X"`, **déjà qualifiées avec l'ancienne classe**, et que le compilateur ne
  signale que le **segment fautif** (`X`) — une recherche du nom nu les manquait toutes.
  97 références repointées. Le csproj est restauré à l'octet près.
- **Estimation corrigée** : le plan annonçait « trois crefs déjà cassés avant le chantier ». Il y en
  a **26** (plus 18 dans le sous-module, hors périmètre), nommant des membres jamais déplacés ou des
  types moteur non résolvables. Laissés tels quels, hors périmètre de R5.
- **Fausse alerte de mon propre outillage (R1)**, à ne pas reproduire : mon premier script de
  comparaison de corps accrochait le **site d'appel** d'une méthode au lieu de sa déclaration, puis
  prenait l'accolade suivante — il comparait donc deux méthodes différentes et annonçait une
  divergence inexistante. Un extracteur de corps doit ancrer sur la **déclaration** (modificateur
  d'accès + `static` + nom), jamais sur la première occurrence du nom.
- **Reste hors périmètre, comme décidé (R-1)** : les 131 champs et le `Clone()` de 118 entrées du
  proxy d'entité, et la câblerie d'instance d'`Update()` (caméra vivante, backdrop) — cette dernière
  n'ayant toujours **aucune couverture de test**, c'est le prochain travail utile si le découpage
  doit aller plus loin, et il faut le faire précéder d'un test de caractérisation de l'ordre.
