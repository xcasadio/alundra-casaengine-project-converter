# Plan — E15, tout le texte d'Alundra en Yarn

Étape E15 de [plan-conversion-totale.md](plan-conversion-totale.md). Le convertisseur produit
directement des dialogues Yarn Spinner pour tout le texte du jeu, la DLL les joue sur le runner Yarn
du moteur, et les fichiers de texte brut disparaissent du projet exporté.

**Statut** : **approuvé par l'auteur le 2026-09-27** (enveloppe et E15.0, avec le plan moteur d'E15.a,
branche moteur `chantier/yarn-extension-points`). **Révision 3.** Première relecture
(REVISE, sept P2 corrigés : sens de `\Y` et effets de `\X`, pages vides et espaces, `:` de Yarn,
oracle d'équivalence, lecteurs oubliés, retour arrière de l'export, E15.0 vraiment en lecture seule) ;
deuxième relecture (REVISE, un P2 corrigé : `\W` dessine un glyphe visible, qui doit être gardé) ;
relecture de clôture (REVISE, un P2 introduit par la révision 3 : la chaîne citée en exemple pour
`\W` était désignée par son numéro de ligne dans le fichier au lieu de son index). **Corrigé par la
session principale et vérifié dans le fichier** (index 95, page 4 de la carte 323), **sans nouvelle
relecture** : le plafond de relecture est atteint pour cette unité.

| Unité | Revue avant approbation | Après exécution |
|---|---|---|
| Enveloppe (ce plan) + E15.0 (mesure, première tranche exécutable) | plan-verifier | commit de documentation |
| E15.a — points d'extension Yarn du moteur (plan `CasaEngineMonogame/ai-agent/tasks/yarn-extension-points-tasks.md`) | plan-verifier séparé | verifier frais (**CONFIRMED** exigé) |
| E15.b — le convertisseur émet le Yarn | plan-verifier, approuvée après E15.0 | verifier frais |
| E15.c — la DLL lit le Yarn | plan-verifier, approuvée après E15.a et E15.b | verifier frais, recette en jeu |
| E15.d — suppression du texte brut | plan-verifier, approuvée après E15.c | verifier frais, recette en jeu |

Cette approbation couvre l'enveloppe et E15.0 ; chaque tranche suivante est approuvée à part.

---

## 0. Cadre

### 0.1 Décisions de l'auteur (2026-09-27)

- **D-E15-1 — E15 = le texte en Yarn, seulement.** La conversion de la cinématique d'intro
  (programme B 129) en `.cutscene` devient une étape à part, **E17**, après E16 : le module de
  cinématiques du moteur n'est pas prêt (liste d'actions fermée, ni caméra, ni attente sur une
  condition, ni action d'entité, ni dialogue).
- **D-E15-2 — Le moteur ne reçoit que des points d'extension génériques**, sur le modèle de Yarn
  Spinner : un stockage de variables injectable (`Yarn.IVariableStorage`), un registre de commandes
  par nom avec un repli pour une commande inconnue, l'accès à la bibliothèque de fonctions. **Aucun
  code propre à Alundra dans le moteur.**
- **D-E15-3 — Les phrases partagées** (table `map_alundra`) et les textes de la table ETC vont dans
  des fichiers Yarn partagés, écrits et traduits une seule fois, désignés par des identifiants stables.
- **D-E15-4 — Les fichiers de texte brut disparaissent du projet exporté** : `{carte}.strings.json`,
  `Dialogues/global-strings.json`, `Dialogues/etc-index.json`, `Dialogues/control-codes.json`. Ils ont
  servi à comprendre la structure des données ; le convertisseur construit désormais les données de
  CasaEngine directement.
- **D-E15-5 — `\X` (nom d'objet, quantités) et `\V` (variable numérique) deviennent des fonctions
  Yarn** fournies par la DLL ; les codes numériques (`\999`, drapeau temporaire) deviennent une
  commande Yarn fournie par la DLL.
- Rappel des décisions antérieures : **D6** (un fichier Yarn par carte, un nœud par chaîne, pas de
  reconstruction de séquences depuis le bytecode : la logique reste dans l'interpréteur) ; **D-E16-6**
  (les drapeaux restent la propriété de la DLL ; le pont vers Yarn passe par le stockage injecté).

### 0.2 Faits établis (2026-09-27)

Chaque fait a été relu dans le code ; les recherches larges ont été contre-vérifiées par un agent
neuf, puis par la relecture de plan.

**Le texte aujourd'hui**

- Le convertisseur écrit le texte brut : `TextWriter` (`alundra-casaengine-project-converter/Writers/TextWriter.cs:62-318`)
  produit la table de 128 chaînes de chaque carte, `global-strings.json` (table ETC par décalage
  d'octets, **remplissage final d'espaces gardé exprès**, `:14-16`), `etc-index.json` (index ETC →
  décalage, depuis `EtcIndexTable.csv`) et `control-codes.json` (inventaire des codes). Les paires
  d'échappement `{x`/`}x` sont déjà décodées en Unicode à l'export. `TextWriter.cs:25-27` : aucun
  graphe de dialogue n'est produit. Le chemin de la table d'une carte vient de
  `MapCatalogReader.StringsRelativePath` (`Readers/MapCatalogReader.cs:46`).
- La table partagée `map_alundra` n'est pas exportée : 128 entrées (`GameMap.cs:111`,
  `data-extracted/data/map_alundra.json`), environ 99 utiles, 345 sites d'appel dans le corpus, aucun
  sur la carte 389.
- Lecteurs connus des fichiers de texte brut (liste complète à établir en E15.0) :
  - pages de dialogue : `AlundraDialogueStringsLoader` (`Alundra/Scripts/AlundraDialogueStringsLoader.cs:29-100`),
    découpées par `AlundraDialogueTextParser` (`:55-116`) : `\A` change de page, `\N` va à la ligne,
    les codes numériques posent un drapeau temporaire à l'affichage de la page
    (`AlundraDialogueDirector.cs:290-302`), les autres codes sont retirés **mais leur opérande reste
    affiché** : `\W2`, `\V<n>`, `\X<n>` laissent un chiffre parasite dans le texte, déviation
    documentée (`AlundraDialogueTextParser.cs:31-36`, `:105-107`), qui prend `\W` pour un code
    d'attente alors qu'il dessine un glyphe ; `\W` apparaît environ 11 000 fois ;
  - OUI/NON : `AlundraEtcStringTable.TryResolveYesNo` (`AlundraEtcStringTable.cs:32-71`), ETC `0x43`/`0x44` ;
  - noms et descriptions d'objets : `TryResolveItemName`, `TryResolveItemDescriptionLine0/1`
    (`:89-100`), ETC `0x200 + id`, `0x280 + id`, `0x300 + id` ; appelants : l'affichage caractère par
    caractère `AlundraInventoryTextReveal` (`:62-176`), l'arme et l'objet équipés
    (`AlundraInventoryDirector.cs:994-1004`), l'armure et les bottes
    (`AlundraSubInventoryDirector.cs:440-445`) ;
  - le harnais de l'intro lit lui-même le `{carte}.strings.json` exporté, dans un `catch` silencieux
    (`Alundra.Tests/IntroTraceHarnessTests.cs:731-749`) ;
  - les tests d'inventaire et de sous-inventaire fabriquent de faux `etc-index.json` et
    `global-strings.json` ; les tests du convertisseur relisent ses sorties.
- `control-codes.json` n'a aucun lecteur à l'exécution.

**Sens des codes de contrôle dans l'original** (`alundra-datas-analyser/AlundraTools/AlundraEngine/Text/TextDecoder.cs`)

| Code | Effet | Insère du texte |
|---|---|---|
| `\A` | attend le bouton, page suivante (`:330-334`) | non |
| `\N` | ligne suivante (`:389-424`) | non |
| `\B` … `\G` | voix des caractères (`-1`, `0` … `4`) (`:336-359`) | non |
| `\H` | centre la ligne (`:362-372`) | non |
| `\M\CE` | avance automatique (`:374-387`) | non |
| `\T` | double le délai d'écriture (`:426-429`) | non |
| `\V<n>` | valeur de `INT_ARRAY_80191908[n]` (`:431-439`) | **oui** |
| `\W<c>` | **dessine un glyphe visible** : le caractère `c − '0' + 16` de la police, opérande consommé (`:441-469`) ; dans le corpus, surtout `\W2` en fin de phrase en suspens (« Ouf\W2 Je l'ai échappé belle », « Tu en as rencontré un\W2 Nirude. ») : très probablement des points de suspension, à établir | **oui (un glyphe)** |
| `\X0` … `\X5` | faucons temporaires, faucons, nom d'objet (`\X2`, `\X4`), seuil, reste (`:505-570`) ; **effets de bord** : `UpdateNumberOfFalcon` et `UpdatePlayerProgressState` (`:514-515`, `:521-522`, `:537-538`, `:544-545`, `:556-557`) | **oui** |
| `\Y` | **termine l'étape de rendu en cours, la page continue** (`:572-574`) ; dans le corpus, il suit toujours un code numérique (`\401\Ydétruite.`, `\A\999\Y\A`) | non |
| `\<chiffres>` | pose le drapeau temporaire n à l'affichage (`:280-328`) | non |

Le choix de table se fait par le bit `0x80` du numéro de texte : posé → table de la carte, clair →
`map_alundra` (`GameEngine.cs:2565-2572`). Les noms d'objets de `\X2`/`\X4` viennent de la table ETC
(`0x200 + id`).

**Formes de pages à traiter** (relevées à la relecture) : une chaîne qui finit par `\A` produit une
dernière page vide, que la DLL affiche aujourd'hui (`AlundraDialogueTextParser.cs:114`,
`Coal Mine-67.strings.json:26`, `:35`) ; une page qui ne fait que poser un drapeau (`\A\999\Y\A`,
`Arena Black Dragon-323.strings.json:99`) est une boîte vide qui attend le bouton ; le texte français
contient souvent ` : ` (`Arena Nirude (Boss)-324.strings.json:43`), que le `LineParser` de Yarn lirait
comme un nom de personnage.

**Le moteur**

- Le compilateur `YarnDialogueCompiler` (`CasaEngineMonogame/CasaEngine.Compiler/Dialogue/YarnDialogueCompiler.cs:9-62`)
  compile **une** source en `DialogueAsset` (`ProgramBytes`, `LineTexts` indexé par identifiant de
  ligne). Le convertisseur (`net9.0-windows`) ne référence pas `CasaEngine.Compiler`
  (`alundra-casaengine-project-converter.csproj:86-87`), qui cible `$(BaseTargetFramework)` et tire le
  paquet `YarnSpinner.Compiler` (`CasaEngine.Compiler.csproj:4`, `:9`).
- `DialogueAsset.FromCompiledProgram` et son sérialiseur d'éditeur existent
  (`DialogueAsset.cs:22-43`, `DialogueAssetJsonSerializer.cs:9-29`).
- `YarnDialogueRunner` (`CasaEngine/Framework/Dialogue/Yarn/YarnDialogueRunner.cs`) : stockage de
  variables codé en dur (`:74-77`), commandes et options vides (`:94-100`), aucun accès à la
  bibliothèque de fonctions, aucune analyse du markup ; `DialogueLine` ne porte que `Text` et
  `Speaker`. `Start(asset, node)` démarre n'importe quel nœud d'un asset.
- Pièges de Yarn Spinner 3.2.1 relevés en recherche (tests réels contre la bibliothèque) : un
  stockage de variables maison doit respecter les valeurs initiales du programme, les variables
  calculées et les variables internes de `visited()` ; après une commande, le dialogue attend
  `Continue()` ; une fonction non enregistrée ne lève qu'à l'exécution ; le markup est à la charge de
  l'hôte (`LineParser`), qui fait d'un préfixe `Nom:` un personnage ; une ligne Yarn ne peut pas être
  vide et ses espaces de début et de fin sont retirés ; le pluriel français intégré est faux (voir §4).

**Export** : sur place, jamais précédé d'une suppression manuelle d'`alundra-project/` ; preuve par
manifeste (avant, après, double export identique hormis `report.json`), `report.json` à 0 erreur.
Un export sur place **ne supprime pas** les fichiers qu'il ne produit plus.

---

## 1. Correspondance proposée (arrêtée par E15.0)

| Source | Yarn |
|---|---|
| Table d'une carte, chaîne `n` | fichier `Maps/…/dialogues/{carte}.yarn`, nœud `M{carte}_S{nnn}` |
| `map_alundra`, chaîne `n` | fichier `Dialogues/Shared.yarn`, nœud `Shared_S{nnn}` |
| Table ETC, entrée `i` | fichier `Dialogues/Etc.yarn`, nœud `Etc_{iiii}` (une ligne) |
| Page (séparée par `\A`) | une ligne Yarn, identifiant `#line:{nœud}_p{k}` stable et déterministe |
| Page vide (fin de chaîne par `\A`, page qui ne pose qu'un drapeau) | une ligne réduite à un marqueur, par exemple `[empty/]`, que la DLL affiche comme une boîte vide ; à confirmer en E15.0 (compilation et rendu), sinon question à l'auteur |
| `\N` | marqueur `[br/]` |
| `\<chiffres>` | commande `<<flag n>>` juste avant la ligne de sa page (drapeau posé à l'affichage) |
| `\Y` | rien : la page continue |
| `\V<n>` | fonction Yarn de la DLL, par exemple `{game_var(n)}` |
| `\X0` … `\X5` | fonctions Yarn de la DLL, par exemple `{falcon_temp()}`, `{category_item_name()}` ; **leurs effets de bord** (`UpdateNumberOfFalcon`, `UpdatePlayerProgressState`) : dans la fonction, évaluée à la livraison de la ligne, ou dans une commande placée avant la ligne — choix arrêté en E15.0 |
| `\W<c>` | **le caractère que dessine le glyphe `c − '0' + 16` de `font3`**, établi par opérande en E15.0 (par exemple `…`) ; s'il n'a pas d'équivalent Unicode dans la police exportée, un marqueur `[glyph id=…/]` **que la DLL dessine** ; jamais retiré |
| `\B` … `\G`, `\H`, `\M\CE`, `\T` | marqueurs gardés pour E12.c, par exemple `[voice id=0/]`, `[center/]`, `[auto_advance/]`, `[slow/]` |
| Espaces de début et de fin (remplissage des enregistrements ETC) | gardés si E15.0 montre qu'un lecteur en dépend (échappement ou marqueur), sinon retirés et documentés ; à décider par l'auteur si un lecteur en dépend |
| Caractères spéciaux de Yarn dans le texte (`[`, `]`, `{`, `}`, `#`, `//`, `<<`, `\`, **`:`**) | échappés ; le `:` pour que le `LineParser` n'en fasse pas un nom de personnage |

Chaque fichier est compilé en un `.dialogue` catalogué à côté de sa source `.yarn`. Le bytecode
désigne déjà une chaîne par son numéro : la DLL démarre le nœud dans l'asset de la carte ou dans
l'asset partagé selon le bit `0x80`. Aucune référence entre fichiers, aucune shadow line.

**Oracle d'équivalence** (utilisé par E15.b) : un **décodeur de référence** écrit dans les tests du
convertisseur d'après `TextDecoder.cs`, qui découpe les pages, consomme les opérandes de `\W`, `\V`
et `\X` comme l'original, et **émet le glyphe de `\W`**. On ne compare pas à la sortie actuelle de la
DLL, qui affiche un chiffre à la place du glyphe : E15 corrige ce défaut du portage, et c'est un
**changement visible** (« Urrr2 Tu es agile » devient « Urrr… Tu es agile », avec le glyphe établi en
E15.0).

---

## 2. Tranches

### E15.0 — Mesure et correspondance ⏳ (lecture seule)

- **But** : les chiffres et les choix qui fixent E15.b et E15.c.
- **Contenu** :
  1. Inventaire du corpus : chaînes non vides de chaque table (cartes, `map_alundra`, ETC) ; chaque
     code de contrôle par table avec un exemple ; **pages vides** (fin par `\A`, pages qui ne posent
     qu'un drapeau), **espaces de début et de fin**, occurrences de **`:`** et des autres caractères
     spéciaux de Yarn, chacun compté ; codes présents dans les textes de l'inventaire.
  2. La correspondance du §1, arrêtée ligne par ligne, avec les noms exacts des fonctions, de la
     commande et des marqueurs ; **le glyphe de chaque opérande de `\W` relevé dans le corpus**
     (caractère `c − '0' + 16` de `font3`, lu dans la police exportée et sa table de caractères) et
     son caractère affiché ou son marqueur ; **la place et le moment des effets de bord de `\X`** ;
     la représentation des pages vides ; le sort des espaces ; la confirmation de l'oracle
     d'équivalence. Tout choix qui n'a pas de réponse dans le code remonte à l'auteur.
  3. La liste **complète et sourcée** des lecteurs des quatre familles de fichiers, à l'exécution et
     dans les tests (DLL, convertisseur, harnais de l'intro, inventaire, sous-inventaire), et les
     entrées ETC réellement lues.
  4. Recette : une carte et un déclencheur qui affichent une phrase de `map_alundra`, un qui affiche
     `\X` ou `\V`, et les écrans d'inventaire qui montrent arme, objet, armure et bottes.
  5. Faisabilité, **par lecture seule** : compatibilité du cadre cible et du graphe de paquets de
     `CasaEngine.Compiler` avec le convertisseur (csproj et fichiers de propriétés) — le build est
     laissé à E15.b ; comportement de l'`AssetVerifier` devant `.yarn` et `.dialogue`
     (`AssetVerifier.cs:32`, entrée non cataloguée sur un export complet) ; suites de référence
     (`Alundra.Tests`, convertisseur, `CasaEngine.Tests`) et **manifeste de référence**
     d'`alundra-project/`.
- **Livrable** : section « §5 Mesures » de ce plan ; ADR-0006 du portage (texte en Yarn, texte brut
  retiré, fichiers partagés), numéro libre au moment de l'écriture.
- **Acceptation** : chaque question a une réponse sourcée ou une ligne « non trouvé » ; la
  correspondance couvre **tous** les codes et **toutes** les formes de pages relevés dans le corpus ;
  aucun fichier suivi modifié hors de ce plan, de l'ADR et de son index.
- **Budget** : par question, une passe dans le code et le corpus ; au-delà, « non trouvé ».
- **Arrêt** : un code, une forme de page ou un effet de bord dont la correspondance reste ouverte, ou
  une mesure qui contredit une décision D-E15 → question à l'auteur, plan corrigé et relu avant E15.b.
- **Retour** : un seul commit de documentation, annulable par `git revert`.
- **Commit** : `docs(e15): record the text measurements and the Yarn mapping`.

### E15.a — Points d'extension Yarn du moteur ⏳ (moteur)

- **But** : exécuter le plan moteur `yarn-extension-points-tasks.md`, puis déplacer le pointeur du
  sous-module.
- **Contenu** (détail dans le plan moteur) : stockage de variables injectable ; registre de
  commandes par nom avec repli et reprise conditionnelle ; enregistrement de fonctions ; analyse du
  markup par le `LineParser` de Yarn, les attributs livrés au présentateur par un ajout compatible à
  `DialogueLine` ; lecture d'une ligne d'un `DialogueAsset` hors d'un dialogue (T1.5, dont dépendent
  OUI/NON et l'inventaire).
- **Acceptation** : plan moteur clos, verifier **CONFIRMED** ; ce dépôt compile avec le nouveau
  pointeur ; `Alundra.Tests` sans échec.
- **Arrêt** : un besoin qui n'entre pas dans une API générique → question à l'auteur, jamais de code
  Alundra dans le moteur.
- **Dépendances** : aucune dans ce plan (peut avancer en parallèle d'E15.0).

### E15.b — Le convertisseur émet le Yarn ⏳ (convertisseur)

- **But** : un `.yarn` et un `.dialogue` catalogué par carte, plus `Shared` et `Etc`, **à côté** des
  anciens fichiers, que la DLL lit encore.
- **Contenu** : référence à `CasaEngine.Compiler` ; un writer Yarn qui applique la correspondance
  d'E15.0, échappe le texte, compile par `YarnDialogueCompiler`, écrit l'asset par le sérialiseur
  d'éditeur, l'inscrit au catalogue et appelle `EditorAssetCatalogService.Save()` ; compteurs dans
  `report.json`, dont l'inventaire des codes qui remplacera `control-codes.json` ; le décodeur de
  référence dans les tests.
- **Acceptation** :
  - **équivalence sur tout le corpus contre le décodeur de référence** (§1) : pour chaque chaîne, les
    lignes compilées, markup analysé, `[br/]` rendu en saut de ligne, marqueurs retirés, égalent les
    pages du décodeur ; pages vides et pages à drapeau seul comparées par leur représentation
    arrêtée en E15.0 ; espaces comparés selon la règle d'E15.0 ; drapeaux, fonctions et marqueurs
    reconstruits égaux aux codes de la chaîne ; **le glyphe de `\W` présent là où la chaîne avait
    `\W<c>`** (un marqueur de glyphe, s'il est retenu en E15.0, n'est jamais retiré dans cette
    comparaison) ; nommément couvertes : `\401\Ydétruite.` (une seule page), les chaînes de la
    carte 324 (` : `), et, dans la table de la carte 323, la chaîne d'**index 95** (base 0, ligne 97
    du fichier), **page 4** (base 0, ligne Yarn `M323_S095_p4`) : son texte est égal à
    « Tu en as rencontré un<glyphe> Nirude. », saut de ligne, « Les Gazeck ont été taillés dans la
    pierre », saut de ligne, « par d'anciens humains. », où `<glyphe>` est le caractère établi en
    E15.0 pour `\W2` ;
  - aucune erreur de compilation Yarn sur le corpus ; identifiants de ligne stables d'un export à
    l'autre ; aucun `Speaker` produit par l'analyse d'une ligne du corpus ;
  - export complet sur place, `report.json` à 0 erreur, manifeste = seulement des ajouts `.yarn` et
    `.dialogue` (plus le catalogue et `report.json`), double export identique hormis `report.json` ;
  - tests du convertisseur sans échec.
- **Arrêt** : une chaîne dont l'équivalence échoue → cause établie avant toute correction.
- **Dépendances** : E15.0.

### E15.c — La DLL lit le Yarn ⏳ (DLL)

- **But** : les dialogues, OUI/NON et l'inventaire lisent les assets Yarn ; le jeu ne lit plus aucun
  fichier de texte brut.
- **Contenu** :
  - le directeur de dialogue démarre le nœud dans l'asset de la carte ou dans l'asset partagé selon le
    bit `0x80`, et fait avancer le runner à chaque page ; modes de fermeture, blocage du joueur,
    `0x39`/`0x44`/`0x50`/`0x51` inchangés ;
  - la commande `flag`, les fonctions de `\X` et `\V` avec leurs effets de bord à la place arrêtée en
    E15.0, et un stockage de variables adossé à `AlundraGameState` (D-E16-6), tous enregistrés par la
    DLL ;
  - `[br/]` rendu en saut de ligne, la page vide rendue en boîte vide, le glyphe de `\W` affiché (un
    caractère du texte, ou un marqueur `[glyph/]` que la DLL dessine) ; les marqueurs d'E12.c ignorés
    à l'affichage mais présents dans les données ;
  - OUI/NON et tous les noms et descriptions d'objets (inventaire, arme, objet, armure, bottes) lus
    dans l'asset `Etc` par identifiant de ligne ;
  - **le harnais de l'intro résout son texte par Yarn**, et le vérifie ;
  - les tests qui fabriquaient des fichiers texte bruts fabriquent des assets.
- **Acceptation** : `Alundra.Tests` sans échec ; oracle de l'intro inchangé (`0x11` à la frame 1704)
  **avec un harnais qui passe par Yarn** ; **recette en jeu** : marin 12 à l'identique (texte,
  OUI/NON, drapeau `\999`), marin 13 (une ligne, fermeture au bouton), la phrase partagée et le texte
  `\X`/`\V` repérés en E15.0, noms et descriptions de l'inventaire, de l'arme, de l'objet, de
  l'armure et des bottes inchangés ; **une ligne à `\W2` montre le glyphe établi en E15.0**, ni rien
  ni « 2 ».
- **Arrêt** : un écart de texte ou de déroulement en jeu → cause établie avant toute correction.
- **Dépendances** : E15.a, E15.b.

### E15.d — Suppression du texte brut ⏳ (convertisseur, DLL, docs)

- **But** : D-E15-4.
- **Contenu** : le convertisseur ne produit plus les quatre fichiers et **retire** ceux d'un export
  précédent (liste fermée, comptée dans `report.json`) ; retrait de `TextWriter`, de
  `MapCatalogReader.StringsRelativePath`, du chargeur et de la table ETC de la DLL, des tests devenus
  sans objet ; `docs/formats/text-tables.md` remplacé par un document du format Yarn.
- **Acceptation** : export complet sur place, 0 erreur ; manifeste = seulement la disparition des
  quatre familles de fichiers ; dans `alundra-project/`, aucun `*.strings.json`,
  `global-strings.json`, `etc-index.json`, `control-codes.json` ; **recherche dans tous les projets**
  (DLL, `Alundra.Tests`, convertisseur et ses tests) : aucun lecteur de ces fichiers ; recette en jeu
  d'E15.c refaite.
- **Arrêt** : un lecteur de ces fichiers retrouvé après la suppression → la tranche s'arrête.
- **Dépendances** : E15.c.

---

## 3. Points ouverts

| Réf | Sujet | Tranche |
|---|---|---|
| O-E15-1 | La correspondance du §1 (noms des fonctions, de la commande, des marqueurs, pages vides, espaces, effets de bord de `\X`) se fixe en E15.0 ; un choix sans réponse dans le code remonte à l'auteur. | E15.0 |
| O-E15-2 | `EtcIndexTable.csv`, entrée du convertisseur venue de l'analyseur, reste une entrée : seule la **sortie** `etc-index.json` disparaît. | E15.d |
| O-E15-3 | Numéros d'ADR : ce plan passe avant E16, il prend l'ADR-0006 du portage et la prochaine ADR du moteur ; les plans d'E16 renverront au numéro libre au moment de leur écriture. | E15.0, E15.a |

## 4. Hors périmètre

- La cinématique d'intro en `.cutscene` : E17 (D-E15-1).
- La fidélité fine d'E12.c (machine à écrire, défilement, curseur, sons de texte, voix, nom et
  portrait, minuterie de 360 images, `0x4C`/`0x4D`) : les marqueurs sont gardés dans les données,
  leur rendu reste à E12.c.
- Les options Yarn (`->`) : la question OUI/NON reste dans la DLL (`0x44`) ; l'aiguillage des options
  du runner reste la tâche 13 du plan Yarn du moteur.
- La traduction dans d'autres langues : les identifiants stables la rendent possible, l'outillage
  (export CSV par langue) n'est pas fait ici. À noter pour plus tard : le pluriel français intégré de
  Yarn Spinner 3.2.1 est faux (toute quantité ≥ 2 classée `many`).
- Les shadow lines et la déduplication des phrases identiques entre cartes.
- L'import `.yarn` dans l'éditeur (tâche 17 du plan Yarn du moteur).

## 5. Mesures

_(Remplie par E15.0.)_

## 6. Arrêts, budgets et retours arrière

### 6.1 Budgets

- Chaque tranche d'exécution a au plus **deux tentatives** au même niveau d'exécutant, puis la
  session principale reprend ou l'exécution monte d'un niveau.
- Chaque frontière à risque (E15.a, E15.b pour le format, E15.c et E15.d pour la recette) a au plus
  **cinq passes** correction puis re-vérification, chacune sur un état réellement changé.
- Budget épuisé : la tranche passe en ⚠️, la question va au §3, le travail s'arrête.

### 6.2 Retours arrière

| Mutation | Tranche | Retour |
|---|---|---|
| Documentation | E15.0 | `git revert` du commit. |
| Code du convertisseur, de la DLL, tests | E15.b, E15.c, E15.d | Branche du chantier abandonnée ; rien n'est fusionné sans l'auteur. |
| Pointeur du sous-module moteur | E15.a | Revenir au pointeur de `main` du parent ; la branche moteur est gardée. |
| `alundra-project/` après E15.b (ajouts) | E15.b | Export depuis `data-extracted/` avec le convertisseur de `main` ; la preuve est le manifeste de référence d'E15.0 **plus la liste énumérée des `.yarn` et `.dialogue` ajoutés par E15.b** (relevée dans le manifeste d'E15.b), qui restent sur le disque, non catalogués. Si l'`AssetVerifier` de `main` refuse ces fichiers non catalogués (constat d'E15.0), leur retrait, limité à cette liste et jamais au dossier, se fait avec l'accord de l'auteur. |
| `alundra-project/` après E15.d (suppressions) | E15.d | Export depuis `data-extracted/` avec le convertisseur d'avant E15.d : il réécrit les quatre familles de fichiers ; preuve contre le manifeste de fin d'E15.c. |

### 6.3 Arrêts communs

- Une mesure ou un fait qui contredit une décision D-E15 : arrêt, question à l'auteur, plan corrigé
  et relu.
- Du code propre à Alundra qui devrait entrer dans le moteur.
- Un test existant qui devrait changer pour une autre raison que la tranche en cours.
- Un export lancé pendant qu'une suite `Alundra.Tests` tourne ; `data-extracted/` rafraîchi sans
  vérifier le sens de l'écart ; une suppression manuelle d'`alundra-project/`.
- Une modification de l'auteur indexée ; un commit sur `main` ; un push.
