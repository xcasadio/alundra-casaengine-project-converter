# Plan — Dette des 17 tests moteur rouges

Chantier ouvert le 2026-09-06 à la demande de l'utilisateur : « corrige la dette des 17 tests moteur
rouges ». Ces 17 échecs de `CasaEngine.Tests` sont la baseline rouge portée depuis le 2026-09-03 et
traversée par tous les chantiers E9 : ils étaient exclus des critères d'acceptation « échecs
identiques nom pour nom ». Le chantier les supprime en tant que baseline.

Branche : `chantier/dette-tests-moteur`, créée le 2026-09-06 dans le dépôt parent (`2d5426f`) **et**
dans le sous-module `CasaEngineMonogame` (`050221b7`), conformément à `CasaEngineMonogame/AGENTS.md`.

**Révision 2** — la révision 1 a été relue et a reçu deux blocages, tous deux retenus.

1. **`EditorThemeAsset_Maps_Editor_Control_Templates` n'est pas un chargeur de fichiers.** D-DT-6
   affirmait que les deux tests supprimés « ne faisaient que charger les fichiers XAML ». C'est faux
   pour le second : il garde **15 correspondances de gabarits et 6 valeurs de thème** (taille de case
   à cocher, style d'indicateur, deux couleurs de `ListBoxItemBackground`, `TabActiveBackground`,
   deux accents transparents). Ces invariants **existent tous nativement**, et la couverture de
   substitution invoquée sous `MGUI/MGUI.Tests/Architecture/` n'en reprend que 5 sur 15 et 2 sur 6.
   Le supprimer aurait retiré silencieusement un garde encore valable. Il est **repointé**, pas
   supprimé. Le décompte passe de « 13 repointages, 3 suppressions » à **14 repointages, 2
   suppressions**.
2. **La baseline n'était pas épinglée nominativement.** Le critère 1 (« zéro échec ») n'était pas
   reproductible sans la liste des tests réellement rouges. Elle est ajoutée en §0.4.

Le relecteur a par ailleurs relevé, à juste titre, que D3 avait été exécutée **pendant** sa relecture
et qu'il lisait donc un arbre déjà modifié. C'est exact : les tranches D2 et D3, mécaniques et sans
risque, ont été lancées en parallèle de la relecture, seules D1 et D4 l'attendaient. Le fait est
consigné ici pour que la lecture du plan ne bute pas sur cet écart. Les trois remplacements de D3
sont chacun adossés à une preuve dans le code de production (§5).

---

## 0. Cadre

### 0.1 Le tri, et comment il a été établi

Enquête en lecture seule à six angles, suivie d'une **réfutation contradictoire** sur les deux plus
grosses familles : quatre sceptiques ont tenté d'infirmer le diagnostic, les quatre ont échoué.

La question posée à chaque test est la même : **le garde protège-t-il encore quelque chose qui
existe ?** Un test qui échoue parce que la fonctionnalité a été retirée volontairement ne se répare
pas, il se supprime. Un test qui échoue parce qu'il vise un chemin ou un nom déplacé se repointe. Un
test qui échoue parce que le code est faux se garde tel quel et c'est le code qui change.

### 0.2 Le verdict

| Famille | Tests | Remède |
|---|---|---|
| `EditorControlTemplateAssetLoadingTests` | 8 | 1 obsolète, 7 repointés |
| `CasaMguiBackendOwnershipTests` | 5 | 4 repointés, 1 obsolète |
| `MaterialDefinitionEditorRegistryTests` | 1 | repointé |
| `EditorAssetWriterServiceTests` | 1 | repointé |
| `LightOverlayTests` | 1 | repointé |
| `CutsceneDirectorTests` | 1 | **défaut réel du moteur** |

Soit **14 repointages, 2 suppressions, 1 correctif de code**.

### 0.4 La baseline, nommément

Mesurée deux fois de suite sur un arbre propre au 2026-09-03 (`1460 / 17 / 1477`, échecs identiques
d'une exécution à l'autre) :

```
CutsceneDirectorTests.Play_MoveToActionAdvancesPositionInRuntimeUpdateOrder
LightOverlayTests.LightOverlayIcons_AreExposedAndLoadedByEditorIcons
EditorAssetWriterServiceTests.SaveAsset_WithEntitySceneTransforms_PersistsRootAndChildCoordinates
MaterialDefinitionEditorRegistryTests.GetDescriptors_LitDiffuseDefinition_UsesSemanticGroupsAndControlHints
CasaMguiBackendOwnershipTests.BackendParityDocument_Lists_MainRendererResponsibilities
CasaMguiBackendOwnershipTests.ExtensibilityArchitectureDocument_Lists_TargetLayers_And_ValidationMatrix
CasaMguiBackendOwnershipTests.NvgVectorCanvas_SessionDisposal_RestoresStateAndFlushes
CasaMguiBackendOwnershipTests.OverlayPipeline_RendersVectorPass_BeforeUiComposition
CasaMguiBackendOwnershipTests.RuntimeAndEditorBootstrap_Use_CasaOwnedBackendTypes
EditorControlTemplateAssetLoadingTests.DockSplitContainer_Overconstrained_MinSizes_Do_Not_Throw
EditorControlTemplateAssetLoadingTests.DockTabGroup_Uses_Theme_Accent_For_Active_Group
EditorControlTemplateAssetLoadingTests.DockTabItem_Active_AccessoryButtons_Use_Active_Background
EditorControlTemplateAssetLoadingTests.EditorControlTemplateAsset_Can_Load_With_BasedOn_Templates
EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Applies_CheckBox_Defaults
EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Disables_Docking_Accent_Bars
EditorControlTemplateAssetLoadingTests.EditorThemeAsset_Maps_Editor_Control_Templates
EditorControlTemplateAssetLoadingTests.EditorThemeAssets_Apply_CustomTemplates_Without_TemplateErrors
```

`StaticModelMaterialOverrideResolverTests.ResolveForMesh_UsesOverrideMaterialAssetAndInstanceOverrides`
est **instable** (vu rouge une fois sur quatre) : il ne fait pas partie de la baseline et un échec
isolé se rejoue seul avant d'être compté.

### 0.3 Ce que le tri a corrigé de nos propres notes

La note de mémoire attribuait cette dette aux commits moteur `878458a8` / `1ccc9238`. **C'est faux.**
Le commit qui supprime les deux fichiers de thème est `eb054241`, dernière étape (T07) du plan
`ai-agent/tasks/mgui-native-dark-theme-plan.md`, mené à son terme : le thème sombre a été porté
nativement dans `MGUI.Core` sous `MGTheme.BuiltInTheme.Dark`. La note sera corrigée en fin de
chantier.

---

## 1. Décisions

- **D-DT-1 — Un test dont la fonctionnalité gardée a été retirée volontairement se supprime.** Il ne
  se repointe pas vers un substitut approximatif : cela fabriquerait un faux garde. Trois tests sont
  dans ce cas.
- **D-DT-2 — Un test dont la cible a seulement changé de nom ou d'emplacement se repointe.** Le garde
  reste valable, seule son adresse a bougé. Treize tests sont dans ce cas.
- **D-DT-3 — L'ordre des systèmes de `WorldRuntimeSystems.Update` est un défaut du moteur, pas du
  test.** Preuve par l'historique, pas par le nom du test : avant `fc66513c`, `World.Update` appelait
  `CoroutineManager.Update` (ligne 309) **avant** `RuntimeSystems.Update` (ligne 326). Le commit de
  nettoyage a déplacé la mise à jour des coroutines dans `WorldRuntimeSystems.Update` en l'**ajoutant
  à la suite** de `CharacterMotion.Update`, ce qui a inversé la relation. Un refactor annoncé comme
  neutre a changé le comportement. Le correctif restaure l'ordre d'origine.
- **D-DT-4 — Le producteur passe avant le consommateur.** Les coroutines émettent les ordres de
  déplacement, `CharacterMotionSystem` les consomme. Dans l'ordre inversé, un ordre émis à l'image N
  n'est exécuté qu'à l'image N+1 : la cinématique perd une image à chaque commande.
- **D-DT-5 — L'abstraction du canevas vectoriel est retirée, le document le dira.** Décision de
  l'utilisateur. `IEditorVectorCanvas`, `IVectorCanvasSession` et `NvgSharpVectorCanvas` n'existent
  plus, et **aucun fichier `.cs` de `CasaEngine.Editor` ne référence encore `NvgSharp`** : seule la
  référence de paquet du `.csproj` subsiste. La section 7 du document d'extensibilité décrit donc une
  couche entièrement absente. Le test qui la garde est supprimé, le document est corrigé, et le test
  qui vérifie le contenu du document voit ses attentes ajustées **dans le même mouvement** — les deux
  sont couplés.
- **D-DT-6 — Le thème de l'éditeur se vérifie sur le thème natif, plus sur des fichiers XAML.** Les
  **sept** tests (cases à cocher, barres d'accent, onglets ancrés, gabarits, et les 15
  correspondances de gabarits) gardent des invariants qui existent toujours ; ils sont repointés vers
  `MGTheme.BuiltInTheme.Dark` + `MGControlTemplateCatalog.RegisterDefaults`, comme le fait
  `GameEditor.cs:953`. **Un seul** test n'a plus d'objet, celui qui ne faisait que charger le fichier
  de gabarits pour vérifier le mécanisme d'héritage `BasedOn` : ce mécanisme est couvert sous
  `MGUI/MGUI.Tests/Architecture/`.
- **D-DT-6bis — la disparition de la cible se prouve assertion par assertion, pas au fichier.**
  `EditorThemeAsset_Maps_Editor_Control_Templates` a d'abord été classé « chargeur de fichiers » et
  promis à la suppression. Relecture faite, il garde 15 correspondances de gabarits et 6 valeurs de
  thème, dont `ListBoxItemBackground` (focused et selected), `Docking.TabActiveBackground` et
  `TabHoverAccentColor` — **10 correspondances et 4 valeurs que rien d'autre ne garde**. Toutes
  existent nativement dans le thème `Dark`. Le test est donc repointé. La leçon vaut au-delà de ce
  cas : un test qui charge un fichier disparu n'est pas forcément un test *sur* ce fichier.
- **D-DT-7 — Aucune suppression de test n'est faite pour rendre la suite verte.** Chaque suppression
  est justifiée par la disparition prouvée de la cible, et la preuve est citée dans le journal.
- **D-DT-8 — Le correctif d'ordre est prouvé non-régressif sur le portage.** `Alundra.Tests` doit
  rester à 752/752 : le portage consomme le moteur, un changement de la boucle de jeu ne peut pas
  être livré sans cette preuve.

---

## 2. Tranches

Les quatre tranches ont des fichiers **disjoints**. L'ordonnancement évite que deux agents compilent
la même solution en même temps, ce qui corromprait les sorties de build.

### D1 — Thème de l'éditeur (8 tests)

Fichier : `CasaEngine.Tests/UI/EditorControlTemplateAssetLoadingTests.cs`.

Supprimer **le seul** `EditorControlTemplateAsset_Can_Load_With_BasedOn_Templates` (il vérifie le
mécanisme d'héritage `BasedOn` du chargeur XAML, couvert sous `MGUI/MGUI.Tests/Architecture/`), ainsi
que les constantes `TemplatePath` / `ThemePath` devenues inutiles.

Repointer le fabricant partagé `CreateThemeTestContext()` (`:379-399`) : plus d'assertion
`File.Exists`, plus de `LoadControlTemplatesFromXaml` ni `LoadThemesFromXaml`, mais
`MGControlTemplateCatalog.RegisterDefaults` puis
`new MGTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily)` comme thème par défaut.

Repointer aussi `EditorThemeAsset_Maps_Editor_Control_Templates` (D-DT-6bis) sur le thème natif, en
**conservant ses 15 `AssertMappedTemplate` et ses 6 assertions de valeurs** ; seuls les noms attendus
`CasaEditor.X` deviennent leur équivalent natif, d'après le catalogue réel.

Les sept tests repointés doivent passer **sans qu'aucune assertion de comportement soit affaiblie**.

### D2 — Propriété du backend MGUI (5 tests + 1 document)

Fichiers : `CasaEngine.Tests/UI/CasaMguiBackendOwnershipTests.cs`,
`docs/engine/casaengine-mgui-backend-extensibility.md`.

Quatre repointages, tous vérifiés : `CasaEngine.Editor/Game1.cs` → `CasaEngine.Editor/GameEditor.cs`
avec le littéral `CasaGameRenderHost<GameEditor>` ; `docs/casaengine-mgui-backend.md` →
`docs/engine/…` ; idem pour le document d'extensibilité ;
`CasaEngine/Framework/Rendering/EditorViewPipeline.cs` →
`CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs`.

Suppression de `NvgVectorCanvas_SessionDisposal_RestoresStateAndFlushes` (D-DT-5), correction de la
section 7 du document d'extensibilité, et **retrait des deux fragments correspondants** de la liste
attendue par `ExtensibilityArchitectureDocument_Lists_TargetLayers_And_ValidationMatrix`.

### D3 — Trois littéraux périmés (3 tests)

Fichiers : `CasaEngine.Tests/EditorServices/MaterialDefinitionEditorRegistryTests.cs`,
`CasaEngine.Tests/EditorServices/EditorAssetWriterServiceTests.cs`,
`CasaEngine.Tests/Editor/LightOverlayTests.cs`.

`"Vector3Editor"` → `"ColorPicker"` (trois occurrences ; `f9b48c09` a introduit
`editorControlHint:"ColorPicker"`). Clé JSON `"coordinates"` → `"local_transform"` (lignes 118-119) :
l'écrivain n'est pas cassé, c'est la clé qui a été renommée volontairement. `Texture2D? Lightbulb` →
`Texture2D Lightbulb` et les deux jumeaux (`38747de3` a retiré le `?` comme norme de style).

### D4 — Ordre des systèmes du monde (1 test, défaut réel)

Fichier : `CasaEngine/Framework/Scene/World/WorldRuntimeSystems.cs`.

Intervertir les deux lignes de `Update` pour que `CoroutineManager.Update` précède
`CharacterMotion.Update`. Une ligne, restauration de l'ordre d'avant `fc66513c`.

---

## 3. Acceptation

1. `CasaEngine.Tests` : **zéro échec**. La baseline des 17 disparaît.
2. Aucun test vert avant le chantier ne devient rouge.
3. `Alundra.Tests` : 752/752 (D-DT-8).
4. Les tests du convertisseur : 153/153.
5. Les deux suppressions sont chacune justifiées par une cible prouvée absente.
6. Aucune assertion de comportement affaiblie pour faire passer un test.
7. **Aucun invariant ne perd son garde.** Les 15 correspondances de gabarits et les 6 valeurs de
   thème de `EditorThemeAsset_Maps_Editor_Control_Templates` sont, après le chantier, soit assurées
   par un test de `CasaEngine.Tests`, soit nommées au journal avec le test de `MGUI.Tests` qui les
   couvre.

## 4. Arrêts

- Un test vert devient rouge et le lien avec le chantier n'est pas immédiat.
- `Alundra.Tests` bouge d'un seul test après D4.
- Un test ne peut être rendu vert qu'en affaiblissant ce qu'il garde : on s'arrête et on demande.
- Le catalogue natif n'offre pas d'équivalent à un invariant gardé par les six tests de D1.

## 5. Journal

### 2026-09-06 — chantier mené de bout en bout

**D3 — trois littéraux périmés.** Chacun adossé à une preuve dans le code de production, pas au nom
du test : `BuiltInMaterialDefinitions.cs:79,88,97` déclare `editorControlHint: "ColorPicker"` pour
les trois couleurs ; `EditorEntityJsonSerializer.cs:281,555` écrit `local_transform` (et
`SceneComponent.cs:421` montre que `coordinates` n'est plus qu'une clé de repli **en lecture**, ce
qui explique que l'écrivain n'ait jamais été cassé) ; `EditorIcons.cs:78-81` déclare les trois
textures sans `?`.

**D2 — propriété du backend MGUI.** Les quatre repointages sont des déménagements purs :
`Game1.cs` → `GameEditor.cs` (`:304` porte bien `CasaGameRenderHost<GameEditor>`), les deux documents
`docs/` → `docs/engine/`, et `EditorViewPipeline.cs` →
`CasaEngine.Editor/Runtime/Rendering/OverlayViewPipeline.cs`. Ce dernier n'a même pas eu besoin de
nouveaux littéraux : `:79` `RenderVectorOverlay` et `:85` `RenderUIOverlay` sont exactement ce que la
garde attendait, dans le même ordre. Les dix lignes de tableau du document de parité ont été
vérifiées une à une dans le document déplacé.

Suppression de `NvgVectorCanvas_SessionDisposal_RestoresStateAndFlushes` : `NvgSharpVectorCanvas.cs`
absent, `IEditorVectorCanvas` / `IVectorCanvasSession` absents de tout `.cs`, et **aucun `.cs` de
`CasaEngine.Editor` ne référence `NvgSharp`** — seules les lignes 28-29 du `.csproj` subsistent. La
section 7 du document d'extensibilité, qui décrivait cette couche, dit désormais ce qui est vrai. Une
nouvelle garde a été ajoutée sur la phrase réécrite, pour que la section ne devienne pas silencieuse
en perdant ses deux anciens fragments.

**D1 — thème de l'éditeur.** Les quinze noms natifs viennent de
`MGUI/MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml:44-212`, et la correspondance élément →
gabarit de `MGUI/MGUI.Core/UI/Themes/BuiltInThemes.xaml:988-1004` : `CasaEditor.X` → `Dark.X`, nom
pour nom, les quinze. Les six valeurs de thème existent nativement (`:1174-1176` pour la case à
cocher, `:1191-1197` pour `ListBoxItemBackground`, `:1492-1502` pour le bloc `Docking`).

L'exécuteur a suivi le brief — donc supprimé les deux tests, y compris celui que la révision 2 a
sauvé. Le test a été **restauré depuis `HEAD` puis repointé** sur `new MGTheme(BuiltInTheme.Dark,
…)`, ses quinze `AssertMappedTemplate` et ses six assertions de valeurs intactes. Il est renommé
`EditorTheme_Maps_Editor_Control_Templates` : il ne charge plus d'*asset*, le mot n'a plus lieu
d'être dans son nom. **Critère 7 satisfait** : aucun des 21 invariants n'a perdu son garde.

Seul `EditorControlTemplateAsset_Can_Load_With_BasedOn_Templates` est supprimé. Il vérifiait le
mécanisme d'héritage `BasedOn` du chargeur XAML, couvert par
`MGUI.Tests/Architecture/ControlTemplateLoaderTests.Xaml_Control_Template_BasedOn_Reuses_Base_Template_Defaults:54`
et par `ThemeDefinitionTests:218,255,282`. L'alias `using XamlDocumentSource`, devenu mort par cette
suppression, est retiré.

**D4 — ordre des systèmes.** Une ligne, plus deux de commentaire nommant l'invariant, pour que le
prochain nettoyage ne réinverse pas l'ordre comme `fc66513c` l'avait fait.

**Suites, après chantier :** `CasaEngine.Tests` **1543 / 1543, zéro échec** (la baseline des 17
disparaît) ; `Alundra.Tests` **752 / 752** ; convertisseur **153 / 153**. La non-régression du
portage est prouvée, pas supposée : le `CasaEngine.dll` contre lequel `Alundra.Tests` a tourné est
horodaté 24 secondes **après** la modification de `WorldRuntimeSystems.cs`.

### Vérification indépendante — CONFIRMED

Le vérificateur a reproduit les trois suites lui-même (deux exécutions consécutives identiques pour
`CasaEngine.Tests`), refait l'archéologie git de `fc66513c` sans se fier au message de commit, et
tenté de falsifier le critère 7 en extrayant le corps du test supprimé depuis
`git show 4224c44c^:…` pour l'apparier appel par appel avec le test restauré : **les 15
correspondances y sont une à une dans le même ordre, et les 6 assertions de valeurs sont identiques
au caractère près**. Il a aussi cherché un consommateur silencieux de l'ancien ordre : le seul
producteur de coroutines en production est le système de cinématiques, exactement la paire que le
correctif rétablit ; `StartCoroutine` est public mais le portage ne l'utilise pas.

Il signale honnêtement une lacune qu'il n'a pas comblée : il n'a pas rejoué l'échec sur l'arbre
d'avant correctif, ce qu'une contrainte de lecture seule interdisait.

### Suites (P4, non traitées ici — les corriger invaliderait la couverture du verdict)

- **[A1]** La section 7 du document d'extensibilité est gardée par un fragment là où elle en avait
  deux : le troisième point réécrit (« `NvgSharp` ne survit que comme référence de paquet ») n'est
  pas gardé et peut donc dériver en silence. Ajouter un fragment si la section compte.
- **[A2]** Le correctif rétablit l'ordre **relatif**, pas la position absolue dans l'image. Avant
  `fc66513c`, les coroutines tournaient tout en haut de `World.Update`, avant `GameMode.Tick`,
  `InternalAddEntities` et les passes de préparation spatiale ; elles tournent aujourd'hui dans
  `RuntimeSystems.Update` (`World.cs:476`), donc après. Ce décalage résiduel est **antérieur** au
  chantier et n'est pas introduit par lui. Consigné pour qu'on ne le prenne pas plus tard pour une
  restauration complète de la disposition d'image d'origine.

### Ce que le chantier a appris

Le relecteur indépendant a bloqué une suppression que j'avais justifiée par une caractérisation
fausse : j'avais lu « le test commence par `File.Exists` sur un fichier disparu » et conclu « test de
chargement de fichier ». Il gardait en réalité 21 invariants, dont 14 que rien d'autre ne couvrait.
**Un test qui charge un fichier disparu n'est pas forcément un test sur ce fichier** : la disparition
de la cible se prouve assertion par assertion.
