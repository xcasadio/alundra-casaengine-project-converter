# Plan — Écrans d'Alundra en assets du projet, liés à des view-models

Tranches consommatrices du programme moteur
[`CasaEngineMonogame/ai-agent/tasks/bound-screens-tasks.md`](../CasaEngineMonogame/ai-agent/tasks/bound-screens-tasks.md)
(décisions D1 → D12, points à valider P1 → P9). Décision du dépôt :
[ADR-0002](decisions/0002-game-screens-are-project-assets-bound-to-view-models.md), qui remplace l'ADR-0001.
**Ce plan applique les décisions, il ne les rediscute pas.**

Statuts : ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked.

## Objectif

Les écrans d'Alundra (inventaire, HUD, boîte de dialogue) deviennent des assets du projet, versionnés dans
`alundra-project/UI/Screens/`, ouvrables et prévisualisables dans l'éditeur. Ils se lient à des view-models
écrits par les présentateurs. Le curseur, les pastilles de magie et la pièce deviennent des animations d'asset
écrites par le convertisseur. Le HUD passe en XAML. Plus aucun XAML n'est embarqué dans `Alundra.dll`.

## État vérifié (2026-09-24)

**Git** : branche `chantier/bound-screens` créée depuis `main` à `084d3e8`. Modifications préexistantes à ne pas
indexer : aucune dans le parent en dehors du sous-module moteur (`CasaEngine.Launcher/Program.cs` de l'auteur).

**Projet généré**
- `alundra-project/` est ignoré en bloc (`.gitignore:63`). Un motif de négation seul ne réinclut pas un chemin sous
  un dossier ignoré (vérifié le 2026-09-23 par `git check-ignore -v`, fichier de test retiré ensuite).
- L'export ne supprime aucun fichier qu'il n'écrit pas, mais réécrit `AssetInfos.json` en entier
  (`alundra-casaengine-project-converter/Writers/ProjectWriter.cs`, `CreateEmptyProject`).
- Le 2026-09-24, `alundra-project/UI/` avait disparu hors de git ; régénéré par un export complet depuis
  `data-extracted/`, à la demande de l'auteur : manifeste avant/après = 336 fichiers ajoutés sous `UI/` et
  `report.json`, rien d'autre ; 0 erreur ; recette 389 → 390 → 389 repassée. La copie `data-extracted/` est la bonne
  source : l'extraction du remaster du 2026-09-19 a le texte non décodé.

**Écrans** (`Alundra/Scripts/`)
- Inventaire : `AlundraInventoryScreen : XamlUIScreenBase` sur le XAML embarqué `Alundra/Screens/InventoryScreen.xaml`
  (`Alundra/Alundra.csproj:23`) ; 42 `Image` dont aucune ne porte de source. 8 statiques (6 boîtes, 2 cadres de
  sélection), 24 icônes et 9 chiffres pilotés par l'état, 1 curseur piloté par le temps.
- HUD : `AlundraHudScreen : UIScreenBase` (`AlundraHudScreen.cs:68`), construit en C# : réserve de 26 images, 2 fonds
  d'équipement en dégradé, 2 icônes d'équipement.
- Présentateurs : `AlundraInventoryPresenter.cs:67-80` et `AlundraHudPresenter.cs:112-135` poussent tout le modèle à
  chaque tick logique. Écrans poussés par `AlundraWorldProxy.cs` (HUD), `AlundraInventoryPresenter.cs:89` et
  `AlundraDialoguePresenter.cs` (écran de dialogue du moteur).
- Cycles d'origine, à 50 Hz : curseur 4 × 10 ticks (200 ms, `AlundraInventoryDirector.cs:520-524`), avec un décalage
  d'un pixel selon la phase (`AlundraInventoryComposer.cs:105-106`) ; pastilles 4 × 10 ticks, phase décalée par
  pastille (`AlundraHudDirector.cs:461-468`) ; pièce 4 × 6 ticks (120 ms), seulement pendant le défilement de
  l'argent, figée sur sa première image sinon (`AlundraHudDirector.cs:594-634`).

## Règles

- Branche `chantier/bound-screens`, jamais de commit sur `main`, jamais de push.
- Un commit par tâche, avec la mise à jour de ce plan. Message en anglais `type(area): summary`.
- Indexation fichier par fichier ; ni `.serena/`, ni les modifications de l'auteur dans les sous-modules.
- Build `Alundra/Alundra.csproj` et `dotnet test Alundra.Tests/Alundra.Tests.csproj` avant toute tâche ✅ ; tests du
  convertisseur (`alundra-casaengine-project-converter.Tests`) dès que le convertisseur change.
- Toute modification du convertisseur se valide par un **export complet en place** depuis `data-extracted/`, sans
  jamais supprimer `alundra-project/` avant, avec un manifeste avant et après ; jamais pendant une suite de tests.
- Recettes en jeu : prédiction écrite avant chaque exécution ; captures par le back-buffer (`GetBackBufferData`),
  harnais hors dépôt.

## Tâches

### ⏳ B1 — Écrans versionnés et animations d'interface

**Prérequis :** phases 1 à 4 du programme moteur closes ; la référence du moteur passe à leur dernier commit.

**Étapes :**
1. `.gitignore` : remplacer `alundra-project/` par une exclusion en cascade qui ne versionne que
   `alundra-project/UI/Screens/` (`alundra-project/*`, `!alundra-project/UI/`, `alundra-project/UI/*`,
   `!alundra-project/UI/Screens/`).
2. Convertisseur, passe des écrans : enregistrer au catalogue chaque `.uiscreen` trouvé sous `UI/Screens/`, avec
   l'identifiant écrit dans l'enveloppe ; ne jamais écrire dans ce dossier. Enregistrer les fichiers voisins comme
   le fait RPGDemo (à relever au début de la tâche).
3. Convertisseur, animations d'interface : écrire `UI/Animations/` avec trois `.anim2d` en boucle, une partie, une
   piste de sprite en paliers et une image de fin qui répète la dernière (convention de `SpriteWriter.cs:510-514`) :
   curseur (sprites `wind_159/182/210/237`, 200 ms, avec le décalage de pixel par phase), pastille pleine et pièce
   (sprites relevés dans `AlundraHudComposer.cs` au début de la tâche ; 200 ms et 120 ms). Identifiants stables par
   `Ids.For`.
4. `AssetVerifier` : vérifier aussi les `.uiscreen`.

**Validation :**
- `git check-ignore -v` : un fichier sous `UI/Screens/` n'est pas ignoré ; un fichier sous `UI/` hors `Screens/` et
  un fichier ailleurs dans `alundra-project/` le sont.
- Tests du convertisseur, dont un test du writer d'animations (durées, image de fin, identifiants stables).
- Export complet : le diff des manifestes ne contient que `UI/Animations/`, `AssetInfos.json` (les nouvelles
  entrées) et `report.json` ; 0 erreur ; un second export ne change que `report.json`.

**Commits :** `chore(submodules): point at the engine with bound screens` ;
`feat(convert): catalogue versioned screens and write the UI animation cycles`.

### ⏳ B2 — L'inventaire en asset lié

**Étapes :**
1. Déplacer `Alundra/Screens/InventoryScreen.xaml` vers `alundra-project/UI/Screens/InventoryScreen.xaml` (ses
   commentaires restent) ; écrire son `.uiscreen` (identifiant fixe) et son fichier de conception
   `InventoryScreen.design.json`.
2. Balisage : `SourceName` des 6 boîtes et des 2 cadres de sélection (GUID fixes) ; bindings des icônes, chiffres,
   textes, visibilités et positions ; le curseur nomme l'animation du curseur.
3. `AlundraInventoryViewModel` observable, qui ne notifie que ce qui change ; le présentateur écrit le view-model au
   lieu d'appeler la vue.
4. L'écran charge l'asset par le catalogue et pose le view-model comme contexte de la fenêtre ; retirer la ressource
   embarquée d'`Alundra.csproj` et le code qui assignait sources et positions.
5. Tests : ceux du présentateur passent sur les propriétés du view-model ; ceux du XAML lisent l'asset versionné ; un
   test de liaison sans affichage ; un test de timing du curseur : sur 25 cycles, chaque changement d'image à moins
   de 20 ms de k × 200 ms.

**Validation :**
- Build, `Alundra.Tests` verts.
- Recette en jeu, prédiction écrite avant : l'inventaire affiche boîtes, icônes, chiffres et textes en `font3` comme
  la capture de référence D5.f, le curseur s'anime ; aucun avertissement.
- Éditeur : l'écran s'ouvre, l'aperçu montre les boîtes et les données de conception ; un enregistrement sans
  modification laisse `git diff` vide.
- **Vérificateur frais.**

**Commit :** `feat(inventory): move the inventory screen to a project asset bound to a view model`

### ⏳ B3 — Le HUD en XAML

**Étapes :**
1. `alundra-project/UI/Screens/HudScreen.xaml`, `.uiscreen` et `HudScreen.design.json` : 26 emplacements `Image`
   nommés, 2 `Rectangle` en `GradientFillBrush`, 2 icônes ; pixels natifs et une seule mise à l'échelle, comme
   l'inventaire.
2. `AlundraHudViewModel` : par emplacement, source, visibilité, position, décalage d'animation et lecture ; les
   pastilles nomment l'animation de pastille avec un décalage de 200 ms par pastille ; la pièce joue pendant le
   défilement de l'argent et revient à sa première image ensuite.
3. Le présentateur écrit le view-model ; l'écran charge l'asset ; retirer la construction en C#.
4. Tests : présentateur sur le view-model ; timing des pastilles (décalages) et de la pièce (120 ms, retour à la
   première image) à moins de 20 ms de l'original.

**Validation :** build, tests ; recette en jeu avec la bascule F1 : HUD identique aux captures de référence
`run-m2-hud` sur les trois étapes, pastilles et pièce animées ; éditeur comme en B2. **Vérificateur frais.**

**Commit :** `feat(hud): rewrite the HUD as a project asset bound to a view model`

### ⏳ B4 — La boîte de dialogue en asset

**Prérequis :** tâche moteur T6.1 close.

**Étapes :** écrire `UI/Screens/DialogueScreen.*` à partir du XAML par défaut du moteur, avec son fichier de
conception ; renseigner le champ de réglage du projet qui le désigne (écrit par le convertisseur dans
`AlundraGame.json`).

**Validation :** recette : un dialogue de la 389 s'affiche comme avant (capture comparée) ; l'écran s'ouvre et se
prévisualise dans l'éditeur. **Vérificateur frais.**

**Commit :** `feat(dialogue): ship Alundra's dialogue screen as a project asset`

### ⏳ B5 — Clôture

Plan clos, mémoire à jour, rapport final ; merges laissés à l'auteur.

**Commit :** `docs(plan): close the bound screens program`

## Points ouverts

| Réf | Sujet |
|---|---|
| O1 | Chemins de binding imbriqués (`Slot0.SourceName`) : supportés par le chemin à points de MGUI d'après l'exploration du 2026-09-23 ; à confirmer par un test en B3 avant d'écrire les 26 emplacements. |
| O2 | L'ordre des merges est la décision de l'auteur (plan moteur, O2). |

## Hors périmètre

- Les glissements d'ouverture et les défilements d'argent et de PV : ils restent calculés par les directeurs.
- Le rafraîchissement de `data-extracted/` depuis le remaster, dont l'extraction du 2026-09-19 a régressé.
- La recette D6 d'E13.d et les vérifications visuelles 🧪 du chantier des handles, qui restent à l'auteur.
