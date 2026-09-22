# Plan — Migration de la DLL Alundra vers les handles comptés du moteur

Tranche consommatrice du chantier moteur
[`CasaEngineMonogame/ai-agent/tasks/asset-handles-migration-tasks.md`](../CasaEngineMonogame/ai-agent/tasks/asset-handles-migration-tasks.md)
(ADR-0037 du moteur). Les décisions ont été arbitrées avec l'auteur le 2026-09-21 et figurent dans le plan
moteur (D1 → D9) : **ce plan les applique, il ne les rediscute pas**.

Statuts : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Objectif

La DLL Alundra n'appelle plus que la nouvelle API du moteur :
- **`Acquire<T>`** pour ce qu'elle partage, rendu quand son détenteur disparaît ;
- **`LoadCopy<T>`** pour les modèles d'entités.

Ensuite, une fois que le moteur a supprimé l'ancienne API, la recette en jeu prouve deux choses sur le
parcours 389 → 390 → 389. Ce que les écrans tiennent n'est chargé qu'une fois. Ce qui n'appartient qu'à la
389 est libéré en la quittant, puis rechargé au retour.

## État vérifié (2026-09-21)

**Git**
- La branche sera `chantier/migration-handles`, créée depuis `chantier/e13d-inventaire` (`542344b`), non
  mergée : c'est une branche empilée.
- Sous-module moteur : branche `chantier/asset-handles-migration`.

**Appels de la DLL** (`rg`, 2026-09-21)
- `AlundraWorldProxy.cs:753` et `:2304` : `Load<Entity>(guid)`. Ce sont des modèles d'entités passés au
  créateur d'entités, donc des copies.
- `AlundraWorldProxy.cs:1442` : `Load<TileSetData>(assetId)`, dans la construction du monde.
- `AlundraPlayerController.cs:147` : `Load<ButtonsMapping>`.
- `AlundraHudScreen.cs:323` et `:482` : `Load<SpriteData>`, suivi de `Sprite.Create`.
- `AlundraInventoryScreen.cs:437` : `Load<SpriteData>`, suivi de `Sprite.Create`.

**Détenteurs existants**
- `AlundraWorldProxy.OnEndPlay` est appelé par `World.Clear` et libère déjà l'écran d'inventaire (D5.f).
- `AlundraInventoryScreen` est `IDisposable` ; `AlundraHudScreen` ne l'est pas.

**Tests** : `AlundraWorldProxyUpdateCharacterizationTests` et `BackdropStageDefinitionTests` utilisent le
gestionnaire. `Alundra.Tests` : 1047/1047.

**Harnais de recette :** hors dépôt, dans le scratchpad de la session, `d6-font`. Il ouvre l'inventaire,
passe un portail, et compte les lignes `Load asset` au niveau trace.

## Règles

- Branche `chantier/migration-handles`, jamais de commit sur `main`, jamais de push.
- Un commit par tâche, avec la mise à jour de ce plan. Message en anglais `type(area): summary`.
- Indexation fichier par fichier ; on ne touche ni `.serena/`, ni les modifications de l'auteur dans les
  sous-modules.
- Build `Alundra/Alundra.csproj` et `dotnet test Alundra.Tests/Alundra.Tests.csproj` avant toute tâche ✅.

## Tâches

### ✅ M1 — Migrer la DLL (tâche T7.1 du plan moteur)

**Fait :**
- **Modèles d'entités** (`AlundraWorldProxy.cs`, les deux créations d'entités) : `LoadCopy<Entity>`.
- **Tilesets de la grille de navigation :** pris par `Acquire` le temps de la construction, puis rendus dans
  un `finally`. `NavigationGrid2D.TryCreateFromTileMap` recopie ce qu'il lit, et la carte de tuiles tient
  les tilesets pour toute sa vie. La méthode gagne une surcharge interne testable sans jeu.
- **`ButtonsMapping` :** `LoadCopy`. Ses `InputMapping` sont confiés au gestionnaire d'entrées de la partie
  par `RegisterMappings` : c'est lui le propriétaire, il n'y a rien à tenir ensuite. C'est l'écart assumé
  au texte, qui prévoyait un `Acquire`.
- **Écrans :**
  - l'écran du HUD et l'écran d'inventaire tiennent leurs `SpriteData` par `Acquire` et rendent sprites et
    handles dans `Dispose` ;
  - l'écran du HUD devient `IDisposable`, et le proxy le libère dans `OnEndPlay` comme l'inventaire.
- **Tests :** trois nouveaux — tilesets rendus après la construction de la grille, tileset tenu ailleurs
  préservé, HUD libéré par `OnEndPlay`. La libération des sprites des écrans n'est pas testable sans
  périphérique graphique (`Sprite.Create`) ; elle est couverte par M2.
- **Résultats :** `Alundra.Tests` 1050/1050 ; `rg "\.Load<|AddAsset\(|GetAsset<" Alundra` ne rend plus que
  des commentaires.

**Étape 1 faite :** la référence du moteur passe de `dceec487` à `d7891342` (T6.1 close, ancienne API pas
encore supprimée). `Alundra.Tests` reste à 1047/1047, DLL inchangée.

**Prérequis :** tâches moteur T1.1 à T6.1 closes. La nouvelle API est présente, l'ancienne pas encore
supprimée.

**Étapes :**
1. La référence du sous-module moteur passe au commit de T6.1.
2. `Load<Entity>` → `LoadCopy<Entity>` (lignes 753, 2304).
3. Tilesets : `Acquire<TileSetData>`. Le proxy garde les handles et les rend dans `OnEndPlay`.
4. `ButtonsMapping` : `Acquire`, tenu par son propriétaire réel. Le vérifier dans le code : le
   commentaire de `AlundraWorldProxy.cs:689` dit que les correspondances sont enregistrées une fois par
   partie. L'écrire sous la tâche.
5. Écrans :
   - l'écran d'inventaire et l'écran du HUD tiennent leurs `SpriteData` par `Acquire`, et leurs `Sprite`
     (devenus `IDisposable`, P1 du plan moteur) ;
   - `AlundraHudScreen` devient `IDisposable`, et le proxy le libère dans `OnEndPlay` comme l'écran
     d'inventaire.
6. Adapter les deux fichiers de tests. Ajouter les tests de rendu :
   - le HUD libéré par `OnEndPlay` ;
   - les tilesets rendus.

**Validation :** build ; `Alundra.Tests` = 1047 + nouveaux ; `rg "\.Load<|AddAsset\(|GetAsset<" Alundra`
vide.

**Commits :**
- `chore(submodules): point at the engine migrated to counted handles (M1)` ;
- `refactor(alundra): hold assets through the engine's handles (M1)`.

### ✅ M2 — Recette après la suppression

**Fait :**
- **Référence du moteur :** `0b0abd05`, qui clôt le plan moteur et ne touche que sa documentation. Le code
  recetté et vérifié est celui de `1e7a4e36` (T9.1 ; la suppression T8.1 est `1d2eaa60`). DLL buildée,
  `Alundra.Tests` 1050/1050.
- **Harnais `d6-font` étendu :** après la 390, il prend le premier portail de la 390 vers la 389 (la 390 en a
  quatre, O1 est sans objet), rouvre l'inventaire et capture. Il capture aussi le HUD sur chaque carte, juste
  avant d'ouvrir l'inventaire.
- **Prédiction écrite avant chaque passage** (scratchpad, `d6-font/prediction-m2.md`).
  - Premier passage : tout est tenu sauf le HUD, absent de toutes les captures. La cause est lue dans le
    code, ce n'est pas un défaut : au début de la partie, le HUD est au repos ; il ne s'ouvre que sur
    demande d'un script ou par la bascule F1 de débogage (`AlundraWorldProxy.ToggleDebugHud`).
  - Second passage, sur une prédiction révisée écrite avant : le harnais appelle la bascule F1 sur la 389.
- **Résultats** (second passage ; identiques au premier sur les points communs) :
  - l'inventaire est en `font3` sur les trois étapes (douze `FontFamily = 'font3'`), icônes affichées ; le
    HUD est affiché sur les trois étapes (phase `Displayed`) ;
  - `UI\font3.fnt` et `UI\Textures\font3.png` sont chargés une seule fois, comme les 51 chargements de
    `UI\…` (boîtes de l'inventaire, sprites `wind_*` du HUD et leurs textures), tous sur la première 389 ;
  - trace « World change » : 0 au démarrage, 0 en 389 → 390, **31** en 390 → 389 ;
  - `map_389_tileset.texture` et `.png` sont chargés à l'entrée sur la 389, **de nouveau** au retour, jamais
    sur la 390 ; de même pour le monde, la carte de tuiles, le tileset et le fond de la 389 ;
    `map_390_tileset` n'est chargé qu'une fois ;
  - aucun avertissement, aucune erreur, aucune exception.
- **Constat hors périmètre :** la 390 s'affiche en noir autour du héros. La capture de l'inventaire sur la
  390 est identique au pixel près à celle d'avant la migration (D5.f) : ce comportement n'a pas changé.
- **Vérificateur frais sur l'ensemble du chantier (moteur et DLL) :** **CONFIRMED**, sans constat P0 à P2.
  - Il a reproduit les builds, les suites et la recette (sortie `run-verify`).
  - Ses trois remarques (une P3, deux P4) sont reportées au plan moteur (O3 à O5). L'une des P4 (O4) vise
    aussi des commentaires de la DLL et de ses tests qui nomment encore l'API supprimée.

**Prérequis :** tâche moteur T8.1 close.

**Étapes :**
1. La référence du sous-module moteur passe au commit de T8.1 ou du dernier commit du chantier. Build et
   `Alundra.Tests` verts.
2. **Prédiction écrite avant d'exécuter.** Le harnais est étendu : après la 390, il reprend un portail de la
   390 vers la 389, rouvre l'inventaire, capture et compte.
3. **Acceptation :**
   - l'inventaire est en `font3` et les icônes du HUD sont affichées, sur chacune des trois étapes ;
   - `UI\font3.fnt` et `UI\Textures\font3.png` sont chargés **une seule fois** sur tout le parcours ;
   - à chaque changement de carte, la trace « assets freed » (P7 du plan moteur) est présente ;
   - au changement 390 → 389, elle compte au moins une ressource libérée ;
   - une planche de tuiles propre à la 389, identifiée dans le journal du premier passage, est chargée à
     l'entrée sur la 389, **de nouveau** au retour, et pas entre les deux. Cela vaut pour son `.texture` et
     pour son `.png` (lignes `Load asset …`). La carte de tuiles étant un composant racine, ce point prouve
     aussi le détachement de tout l'arbre d'une entité (T1.2 du plan moteur) et la libération des `Texture`
     (P10) ;
   - ni exception, ni avertissement nouveau.
4. Vérificateur frais sur l'ensemble du chantier : moteur et DLL.

**Commit :** `docs(plan): close the handle migration with the in-game check (M2)`.

## Points ouverts

| Réf | Sujet |
|---|---|
| O1 | ~~Si la 390 n'a aucun portail vers la 389, la recette prend un autre aller-retour entre deux cartes voisines, et le note.~~ Sans objet : la 390 a quatre portails vers la 389 (M2). |
| O2 | L'ordre des merges est la décision de l'auteur (plan moteur, O2). |

## Hors périmètre

- Le convertisseur, qui n'appelle pas le gestionnaire de ressources.
- La recette D6 d'E13.d, qui reste à l'auteur.
