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
Réponses de l'auteur à la mesure d'E15.0 (2026-09-27) :

- **D-E15-6 — Les symboles de `\W` deviennent des marqueurs** `[glyph id=N/]` que la DLL dessine ;
  la police `font3` ne change pas. Les octets bruts `0x1A`/`0x1C` des descriptions d'objets
  deviennent le même marqueur.
- **D-E15-7 — Les emplacements `#Disuse` sont gardés** (écrits `\#Disuse`).
- **D-E15-8 — Les espaces de début et de fin de page sont perdus**, comme Yarn les retire ; c'est
  documenté, aucun lecteur actuel n'en dépend.
- **D-E15-9 — Les effets de bord de `\X` passent par une commande** placée avant la ligne, les
  valeurs par des fonctions sans effet de bord.

Decisions: see ADR-0006 (`docs/decisions/0006-alundra-text-is-authored-as-yarn.md`).

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

## 1. Correspondance arrêtée (E15.0, 2026-09-27)

Chaque marqueur autofermant porte `trimwhitespace=false` : sans lui, Yarn avale l'espace qui suit
(« Humm[br/] Rien » donne « HummRien », vérifié contre Yarn Spinner 3.2.1). Il est omis dans le
tableau pour la lisibilité.

| Source | Yarn |
|---|---|
| Table d'une carte, chaîne `n` | fichier `Maps/…/dialogues/{carte}.yarn`, nœud `M{carte}_S{nnn}` |
| `map_alundra`, chaîne `n` | fichier `Dialogues/Shared.yarn`, nœud `Shared_S{nnn}` |
| Table ETC, entrée `i` | fichier `Dialogues/Etc.yarn`, nœud `Etc_{iiii}` (une ligne) |
| Page (séparée par `\A`) | une ligne Yarn, identifiant `#line:{nœud}_p{k}` stable et déterministe |
| Page sans texte visible (fin de chaîne par `\A` : 37 ; page qui ne pose qu'un drapeau : 58) | une ligne réduite à `[empty/]`, que la DLL affiche comme une boîte vide : **le rythme des dialogues ne change pas** (le défilement fin de l'original, `TextDecoder.cs:330-334`, `:688-715`, relève d'E12.c) ; vérifié : `[empty/]` seul compile et donne un texte vide |
| `\N` | `[br/]` |
| `\<chiffres>` | commande `<<flag n>>` juste avant la ligne de sa page, valeur normalisée (`\0999` et `\999` sont le même drapeau, comme le `uint.Parse` de l'original) |
| `\Y` | rien : la page continue |
| `\W<c>` | `[glyph id=N/]`, dessiné par la DLL (D-E15-6), avec `N = c − 0x20` pour un chiffre et `c − 0x27` pour une lettre (formule d'`ALUN_CD.EXE`, `0x800462e0`–`0x800462f0`) : `\W0` 16 •, `\W2` 18 …, `\W3` 19 “, `\W4` 20 ”, `\W5` 21 ☆, `\W6` 22 →, `\W7` 23 ←, `\W8` 24 ↑, `\W9` 25 ↓, `\WA` 26 □, `\WD` 29 ✕ ; jamais retiré |
| Octets bruts `0x1A`/`0x1C` des descriptions d'objets (ETC) | `[glyph id=26/]` □ / `[glyph id=28/]` ○ |
| Ligne qui contient un `\X` | commande `<<falcon_update>>` avant la ligne (D-E15-9) : elle relève le nombre de faucons temporaires, puis met à jour le nombre de faucons et la progression (`UpdateNumberOfFalcon`, `UpdatePlayerProgressState`), comme l'original |
| `\X0`, `\X1`, `\X2`/`\X4`, `\X3`, `\X5` | fonctions sans effet de bord : `{falcon_temp()}` (valeur relevée avant la mise à jour), `{falcon()}`, `{category_item_name()}`, `{category_threshold()}`, `{category_remaining()}` |
| `\V<n>` | `{game_var(n)}` |
| `\B` … `\G`, `\H`, `\T` | marqueurs gardés pour E12.c : `[voice id=-1…4/]`, `[center/]`, `[slow/]` (`\M\CE` n'apparaît nulle part dans le corpus : pas de correspondance) |
| `:` | `\:` (sinon Yarn fait du début de ligne un nom de personnage, même avec une espace avant le `:`) |
| `#` (seulement dans `#Disuse`) | `\#` ; les `#Disuse` sont gardés (D-E15-7) |
| Espaces de début et de fin de page | perdus, Yarn les retire à la compilation (D-E15-8) |
| `[`, `]`, `{`, `}`, `//`, `<<`, `\` isolé | absents du corpus ; le writer les échappe quand même |

Chaque fichier est compilé en un `.dialogue` catalogué à côté de sa source `.yarn`. Le bytecode
désigne déjà une chaîne par son numéro : la DLL démarre le nœud dans l'asset de la carte ou dans
l'asset partagé selon le bit `0x80`. Aucune référence entre fichiers, aucune shadow line.

**Oracle d'équivalence** (utilisé par E15.b) : un **décodeur de référence** écrit dans les tests du
convertisseur d'après `TextDecoder.cs` et la formule de `\W` de l'exécutable, qui découpe les pages,
consomme les opérandes de `\W`, `\V` et `\X` comme l'original, et **émet le glyphe de `\W`** (sous la
forme de son marqueur). On ne compare pas à la sortie actuelle de la DLL, qui affiche un chiffre à la
place du glyphe : E15 corrige ce défaut du portage, et c'est un **changement visible** (« Urrr2 Tu es
agile » devient « Urrr… Tu es agile »).

---

## 2. Tranches

### E15.0 — Mesure et correspondance ✅ (lecture seule, faite le 2026-09-27, résultats au §5)

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
- **Retour** : deux commits de documentation (l'ADR, puis les mesures), annulables par `git revert`.
- **Commit** : `docs(adr): record that all Alundra text is authored as Yarn`, puis
  `docs(e15): record the text measurements and the Yarn mapping`.

### E15.a — Points d'extension Yarn du moteur ✅ (moteur, faite le 2026-09-27)

**Réalisé** : plan moteur exécuté (T0.1 à T2.2, branche moteur `chantier/yarn-extension-points`,
ADR-0042 et ADR-0043 du moteur) ; T1.3 révisée sur décision de l'auteur (le compilateur reçoit les
déclarations de fonctions du jeu) ; vérificateur de clôture REFUTED sur trois lignes de validation
sans test, corrigé, revérification ciblée **CONFIRMED** ; `CasaEngine.Tests` 1991/1991. Pointeur du
sous-module déplacé ; `Alundra.Tests` 1291/1291 et tests du convertisseur 197/197 sans échec avec ce
moteur.

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
  d'E15.0, échappe le texte, compile par `YarnDialogueCompiler` **en lui passant les déclarations des
  fonctions d'Alundra** (`falcon_temp`, `falcon`, `category_item_name`, `category_threshold`,
  `category_remaining`, `game_var` ; surcharge ajoutée par E15.a T1.3, décision de l'auteur du
  2026-09-27 sur O4 du plan moteur), écrit l'asset par le sérialiseur
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
    du fichier), **page 4** (base 0, ligne Yarn `M323_S095_p4`) : son texte, marqueurs retirés, est
    « Tu en as rencontré un Nirude. », saut de ligne, « Les Gazeck ont été taillés dans la pierre »,
    saut de ligne, « par d'anciens humains. », avec un attribut `glyph` d'identifiant 18 juste après
    « un » ;
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
  - les commandes `flag` et `falcon_update`, les fonctions de `\X` et `\V`, et un stockage de variables
    adossé à `AlundraGameState` (D-E16-6), tous enregistrés par la DLL ; le port de
    `UpdateNumberOfFalcon`, `UpdatePlayerProgressState` et de la table des seuils (§5.4) ;
  - `[br/]` rendu en saut de ligne, `[empty/]` en boîte vide, `[glyph id=N/]` dessiné par la DLL
    (glyphe `N` de `font3`) ; les marqueurs d'E12.c ignorés à l'affichage mais présents dans les
    données ;
  - OUI/NON et tous les noms et descriptions d'objets (inventaire, arme, objet, armure, bottes) lus
    dans l'asset `Etc` par identifiant de ligne ;
  - **le harnais de l'intro résout son texte par Yarn**, et le vérifie ;
  - les tests qui fabriquaient des fichiers texte bruts fabriquent des assets.
- **Acceptation** : `Alundra.Tests` sans échec ; oracle de l'intro inchangé (`0x11` à la frame 1704)
  **avec un harnais qui passe par Yarn** ; **recette en jeu** (cibles au §5.5) : marin 12 à l'identique
  (texte, OUI/NON, drapeau `\999`), marin 13 (une ligne, fermeture au bouton), la phrase partagée, le
  texte `\X` du marin de la carte 134, le panneau aux boutons de la carte 143, noms et descriptions de
  l'inventaire, de l'arme, de l'objet, de l'armure et des bottes inchangés ; **une ligne à `\W2` montre
  « … »**, ni rien ni « 2 ». `\V` est vérifié par test (mini-jeux non portés).
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
| O-E15-1 | ~~La correspondance du §1~~ — **arrêtée le 2026-09-27** (§1, D-E15-6 à D-E15-9, ADR-0006). | E15.0 |
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

## 5. Mesures (E15.0, 2026-09-27)

Quatre mesures en lecture seule, chacune recalculée par un agent neuf avec ses propres scripts ; un
programme de test contre Yarn Spinner 3.2.1 sur les formes réelles ; scripts et sorties dans le
dossier temporaire de la session (`scratchpad/e15-0/`). Aucun fichier suivi n'a été modifié hors de
ce plan et de l'ADR-0006.

**5.1 Corpus**

| Table | Emplacements | Non vides | `#Disuse` | Textes réels |
|---|---:|---:|---:|---:|
| Cartes (483 fichiers) | 61 824 | 24 303 | 14 435 (dont 5 en minuscules, Lars' Crypt 21-25) | 9 868 |
| `map_alundra` | 128 | 128 | 29 | 99 |
| ETC | 1 024 | 353 | 5 | 348 |

- Codes, tables des cartes : `\A` 6 972, `\B` 17 063 (2 906 hors `#Disuse`), `\C` 3 725, `\D` 1 751,
  `\E` 1 078, `\F` 686, `\G` 0, `\H` 732, `\N` 24 593, `\T` 5 319, `\Y` 921, numériques 931 (43
  graphies, 33 valeurs ; `999` ×417), `\W` 11 099. `map_alundra` : `\A` 1, `\B` 126, `\C` 2, `\H` 91,
  `\N` 95, `\Y` 1, `\999` 1, `\W` 71. ETC : `\N` seul, 19 fois. `\M\CE` et codes inconnus : 0.
- `\W` par opérande (toutes tables) : `2` 10 515, `3` 181, `4` 179, `0` 108, `8` 41, `9` 40, `D` 33,
  `A` 32, `7` 21, `5` 10, `6` 10 ; aucun `1`, `B`, `C`. `\W3`/`\W4` ouvrent et ferment une citation
  (180 fois sur 181 ; deux chaînes déséquilibrées dans les données d'origine, Inoa 178 et 260).
- `\X` : seulement « Church (lobby)-134 » (`\X0` 1, `\X1` 2, `\X2` 2, `\X3` 1, `\X4` 1, `\X5` 1).
  `\V` : seulement les pubs 472, 473, 474 (4 chacun).
- Pages : 37 chaînes finissent par `\A` ; 167 pages sans texte une fois les codes retirés, dont 58 ne
  portent qu'un code numérique et `\Y` (toutes au milieu d'une chaîne) et 72 ne portent que `\W`/`\T`
  (elles dessinent des glyphes : ce ne sont pas des pages vides). `\Y` suit un code numérique 920 fois
  sur 921.
- Espaces : 108 pages de cartes finissent par une espace et 2 commencent par une espace, une fois les
  codes numériques déplacés ; ETC : 56 et 2, dont les 42 titres de chapitre de `0x000`–`0x02F`
  remplis à 31 caractères.
- Caractères spéciaux de Yarn : `[`, `]`, `{`, `}`, `//`, `<<`, `>>`, `\` isolé : 0 ; `#` seulement dans
  `#Disuse` ; `:` 442 occurrences sur 381 pages, **toutes** lues comme nom de personnage par le
  `LineParser` de Yarn 3.2.1 (motif `^((?:[^:\\]|\\.)*):\s*`), espace avant ou non. Paires `{x`/`}x`
  non décodées : 0. `%` : 1 (ETC `0x2A9`, sans effet hors des marqueurs de Yarn).
- Textes de l'inventaire (ETC `0x43`, `0x44`, `0x200`–`0x261`, `0x280`–`0x2E1`, `0x300`–`0x361`) :
  aucun code `\` ; octets bruts `0x1A` (□) 7 fois et `0x1C` (○) 2 fois.

**5.2 Glyphes de `\W`**

La police exportée (`alundra-project/UI/font3.fnt`) donne à chaque glyphe sous 128 son propre code
comme point de code : les glyphes 16 à 29 sont des caractères de contrôle, sans équivalent Unicode
utilisable. Le binaire France (`ALUN_CD.EXE`, `0x800462e0`–`0x800462f0`) calcule le glyphe par
`c − 0x20` pour un chiffre et `c − 0x27` pour une lettre ; la décompilation (`TextDecoder.cs:442-443`)
a perdu la branche des lettres et dessinerait `!` et `$`. Glyphes lus dans `font3.png` : 16 •, 18 …,
19 “, 20 ”, 21 ☆, 22 →, 23 ←, 24 ↑, 25 ↓, 26 □, 27 △, 28 ○, 29 ✕ (boutons de manette en couleur).
Décision D-E15-6 : des marqueurs dessinés par la DLL.

**5.3 Lecteurs des fichiers de texte brut**

| Fichier | Lecteurs et écrivains |
|---|---|
| `{carte}.strings.json` | écrit par `TextWriter.cs:225-242` ; chemin `MapCatalogReader.StringsRelativePath` (`:46`), testé par `MapLocationTests.cs:32-55` ; lu par `AlundraDialogueStringsLoader.cs:29-101` (branché en `AlundraWorldProxy.cs:579` sur `AlundraEventProgramRunner.LocalDialogueStrings`) et par le harnais `IntroTraceHarnessTests.cs:731-749` ; `AlundraDialogueOpcodeDispatchTests.cs:46` injecte des chaînes par le même point |
| `global-strings.json`, `etc-index.json` | écrits par `TextWriter.cs:104-121`, `:305-315` ; lus par `AlundraEtcStringTable.cs:32-100` (OUI/NON `0x43`/`0x44` ; noms `0x200 + id`, descriptions `0x280 + id` et `0x300 + id`), appelé par `AlundraInventoryTextReveal`, `AlundraInventoryDirector.cs:994-1004` (arme, objet), `AlundraSubInventoryDirector.cs:440-445` (armure, bottes) ; faux fichiers écrits par `AlundraInventoryDirectorTests.cs:79-81` et `AlundraSubInventoryDirectorTests.cs:78-80` ; relus par `TextWriterTests.cs` |
| `control-codes.json` | écrit par `TextWriter.cs:262-265`, relu seulement par `TextWriterTests.cs` |
| Entrées du convertisseur (restent) | `StringTableReader.cs:38-84`, `EtcIndexCatalogReader.cs:26-60` |

**5.4 Faisabilité**

- `CasaEngine.Compiler` cible `net9.0` (`$(BaseTargetFramework)`, `Directory.Build.props:3-5`) et ne
  tire que `YarnSpinner.Compiler` : le convertisseur (`net9.0-windows`) peut le référencer sans
  changer de cadre cible.
- `AssetVerifier` n'a aucun chargeur pour `.dialogue` ni `.yarn` (`AssetVerifier.cs:32-54`) : sur un
  export complet, un tel fichier non catalogué est ignoré (`:124`), un fichier catalogué est seulement
  vérifié présent et non vide (`:288-292`, `:370-401`). Le retour arrière d'E15.b laisserait donc ses
  fichiers sans erreur. E15.b peut ajouter un chargeur `.dialogue` (le moteur en a un).
- Suites de référence (branche `chantier/e15-yarn`, 2026-09-27) : `Alundra.Tests` 1 291/1 291,
  tests du convertisseur 197/197, `CasaEngine.Tests` 1 957/1 957.
- Manifeste de référence d'`alundra-project/` (hors `Alundra.dll`, `Alundra.pdb`, `.casaeditor/`) :
  23 247 fichiers, empreinte SHA-1 du manifeste `331c0e3fcf82020792b72e97fd9fd7c18b10e7d0`. E15.b
  reprend son propre manifeste juste avant son premier export et le compare à celui-ci.
- `\X` : `UpdateNumberOfFalcon` (`PlayerManager.cs:5188-5199`) et `UpdatePlayerProgressState`
  (`TextDecoder.cs:1098-1172`) sont idempotentes après un premier appel (compteur temporaire à 0,
  progression calculée des seuls drapeaux) : une commande par ligne vaut les appels répétés de
  l'original. Aucune des deux n'est portée dans la DLL : E15.c les porte, avec
  `g_categoryThresholdTable` et l'indice de catégorie. `\V` lit `INT_ARRAY_80191908`, écrit par les
  mini-jeux du pub, non portés : `game_var(n)` rend la valeur portée, 0 tant qu'aucun mini-jeu ne
  l'écrit.

**5.5 Cibles de recette (E15.c)**

- **Phrase partagée** : le coffre d'Anzes (carte 163, programme F7 de l'entité 9 : `0D 09 00; FF`,
  chaîne partagée 9, avec un `\W2`), atteint tôt par la tempête (391 → 416 → 163) ; ou un panneau
  indicateur (carte 12 entité 1, carte 10 entité 99, chaînes partagées 34 et 31, avec les flèches
  `\W6`–`\W9`). Ces trois programmes tournent sans opcode manquant dans la DLL.
- **`\X`** : l'entité 0 de la carte 134 (un marin, « Marin-passager-mouette »), chaînes 13 à 16
  (`\X0`, `\X1`, `\X2`). Les chaînes 19 et 20 (`\X3`, `\X4`, `\X5`) passent par l'opcode `0x78`, que la
  DLL ne porte pas : hors d'atteinte en jeu aujourd'hui.
- **`\V`** : carte 473, map-events 4 à 8 (chaîne 11), dans un mini-jeu non porté : vérifié par test
  seulement.
- **`\W2`** : carte 389, entité 15 (programme F15) ; ou carte 390, entité 2.
- **Boutons** : carte 143 (« Coast house (nava's cave entrance) »), « Appuie sur □ pour l'écouter ».
- **Inventaire** : noms et descriptions, arme, objet, armure, bottes ; une description avec □ ou ○.

**5.6 À noter pour la suite**

- L'opcode `0xC4` (dialogue avec un nom) ouvre aussi du texte (26 sites atteignables) ; la DLL ne le
  porte pas : il lira les mêmes assets le jour où il sera porté (E12.c).
- Les chaînes partagées 5 et 120 sont des `#Disuse` ouverts par 7 sites atteignables (par exemple 135
  C6 → 120) : elles doivent exister (D-E15-7).
- Sites qui ouvrent la table partagée : 347 par parcours linéaire, 342 sur 174 cartes une fois le code
  mort retiré (les « ~345 » du plan E12 ne correspondent à aucun des deux).

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
