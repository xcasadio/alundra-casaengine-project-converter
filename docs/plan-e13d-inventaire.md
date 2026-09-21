# Plan — E13.d : l'inventaire principal

**État** : 🚧 **réapprouvé par l'auteur le 2026-09-21 après D0**, exécution en mode **AUTO** (travail
réversible dans le périmètre approuvé, ni push, ni merge, ni action externe). D0 close ; son arrêt propre
a fait réviser le plan (§1.2 et §1.5 corrigés), dont la dernière correction n'a pas été relue, plafond
atteint (§7) ; l'auteur l'a approuvée telle quelle. Décisions de l'auteur au §2.2 : boîtes en **image
cuite** depuis la copie A (D-E13D-13, D-E13D-14), **portrait reporté** (D-E13D-12 amendée), **gel sur tout
`MenuOpen`** (D-E13D-15).
**Naissance** : `docs/plan-conversion-totale.md` §E13, E13.d — « porter l'original tel quel » ; découpage
décidé par l'auteur le 2026-09-19 : **l'inventaire principal d'abord** (ouverture, fermeture, équipement),
puis le sous-inventaire et la bascule L1/R1. Ce plan ne couvre que l'inventaire principal ; le
sous-inventaire aura son propre plan, écrit après la recette de celui-ci.
**Dépend de** : E13.c S3 (`AlundraItemTables`, les compteurs d'objets, `SetPlayerWeaponId` et la chaîne
d'équipement portée ligne à ligne), close et mergée dans `main` (`ea633ad`).
**Branche** : `chantier/e13d-inventaire`, créée depuis `chantier/e13c-suite`, rebasée sur `main` le
2026-09-21 après le merge d'E13.c (`ea633ad`).

---

## 1. Les faits établis

Reconnaissance du 2026-09-21 : cinq relevés en lecture seule et une critique de complétude, puis des
mesures reprises en session principale (marquées **[mesuré]**). Tout est cité.

### 1.1 L'ouverture et la fermeture dans l'original

- **Déclencheur** (`GameEngine.cs:1567-1576`) : les drapeaux de contrôle du joueur valent **exactement 0**,
  personne ne tient le joueur, aucun verrou ni délai de warp, aucune transition globale, `Select` n'est
  **pas** maintenu, et l'un quelconque de `Start`, `L2` ou `R2` vient d'être pressé (`PadState.cs:22`,
  masque lu par ET binaire). **Il ne vérifie pas que le héros est au sol** : on peut ouvrir en plein saut.
  Dans le portage, **le saut n'est pas porté** (`AlundraPlayerManager.cs:66-73`, `:286-289`, et aucun appel
  à `RequestJump` dans la DLL) : le cas en l'air y est la **chute**, au bord d'un plateau.
- **Ouverture** (`MainInventoryManager.DisplayInventory`, `:443-499`) : glissement du HUD
  (`HudManager.InitializeHudPosition`), armement de l'emplacement de rappel 6, portrait d'Alundra préparé
  pour l'effet d'ouverture, noms de l'équipement, **son 4**.
- **[lu] Les gardes de l'ouverture** (D0.6) : `DisplayInventory` ne fait rien si `g_forbiddenWarpFlag != 0`
  (`:445`), ni si l'emplacement de rappel **0** ou **0xb** est actif (`CheckSpecialWarpCondition`,
  `GameEngine.cs:1590-1593`, lit `g_callbackTable[i].Flags & 1`, que `SetTransitionType(i)` pose,
  `GraphicManager.cs:1714-1735`) : l'emplacement 0 est la **boîte de dialogue**, l'emplacement 0xb le menu
  de débogage des drapeaux (`StaticVariables.cs:11388-11470`, `UIDebugManager.InitializeFlagsDebugMenu`).
  Une branche de débogage (`g_cdIsReady == 0`, `:460-481`) est morte en jeu : `g_cdIsReady` passe à 1 après
  l'initialisation du CD (`SoundManager.cs:331`). `StartFadeOut` (`GraphicManager.cs:1767-1783`), malgré son
  nom, est une variante de l'ouverture appelée par cette branche morte (`:474`) et par un post-traitement
  (`GraphicManager.cs:1697`) ; l'ouverture en jeu passe par `:484-495`.
- **[lu] `g_forbiddenWarpFlag`** (D0.6) : 0 à l'initialisation (`:30`) ; **5** à la mise en place (`:505`) ;
  **`|= 2`** au début de la fermeture (`:1903`) ; **`&= ~4`** (`:875`) puis **0** à la fin de la fermeture
  (`:880`). Lectures : `== 0` pour pouvoir ouvrir (`:445`) ; `& 6` bloque la lecture de la manette pendant un
  glissement (`:783`) ; `& 4` et `& 2` règlent la fin des glissements (`:873`, `:878`).
- **Mise en place** (`FUN_80054f1c`, `:503-776`) : `MenuOpen` levé (`:507`), sept boîtes armées pour
  glisser de hors-écran à leur place en **15 ticks** ; puis la fonction de rendu est remplacée par
  `FUN_80056598` (`:775`).
- **Chaque image** (`FUN_80056598`, `:779-923`) : pendant un glissement, seules les boîtes bougent ;
  sinon, lecture de la manette. **Fermeture** par `Start`/`L2`/`R2` (`:846-851`) : glissement inverse,
  **son 5** (`FUN_800556dc`, `:1901-2158`), et `MenuOpen` n'est retiré qu'à la fin du glissement (`:896-899`).
  `L1`/`R1` (`:853-859`) passent au sous-inventaire : **hors de ce plan**.

### 1.2 Le gel du monde — porté en partie **[mesuré]**

- **Original** : `MenuOpen` fait partie de `GameplayBlockedMask`. `EntityManager.UpdateEntities`
  (`EntityManager.cs:367-390`) saute alors, **pour toutes les entités, héros compris**, les évènements,
  compteurs, animations, la physique et les effets ; les évènements de carte s'arrêtent aussi
  (`GameEngine.cs:1667-1671`).
- **Portage** : **la même porte existe depuis T2**. `AlundraEntityScriptProxy.Update` calcule
  `gameplayBlocked` sur `GameplayBlockedMask` et n'appelle `RunGameplayBlockableUpdate` — où vivent
  `MovePlayer`, la physique et la synchronisation d'animation — que s'il est faux
  (`AlundraEntityScriptProxy.cs:828-867`, `:874-1020`) ; le monde porte la sienne
  (`AlundraWorldProxy.cs:1787-1856`). Un dialogue qui lève `MenuOpen` fige déjà héros et entités, constaté
  en jeu (`AlundraEntityScriptProxy.cs:852-853`). La porte arrête bien le déplacement du héros, que
  `MovePlayer` fait par `Controller.Move` (`AlundraEntityScriptProxy.cs:1730`).
- **[mesuré] Mais la porte ne fige pas tout** (D0.3, corrige la phrase « lever `MenuOpen` suffit à figer
  le héros » de la première rédaction) :
  - **la gravité** : `CharacterControllerComponent.Update` (moteur, `:186-293`) intègre la vitesse
    verticale à chaque image rendue, hors du proxy (`ApplyVerticalVelocity`, `:768-811`), sauf si
    `IsVerticalOwnedExternally` est levé (`:770-774`) ; il l'est pour les PNJ (`AlundraEntitySpawnFactory.cs:586`),
    pas pour le héros hors escalade et départ de warp. **Ouvert en pleine chute, le héros continuerait de
    tomber** derrière l'inventaire (le saut n'étant pas porté, §1.1) ;
  - **les animations de sprite** : `AnimatedSpriteComponent.Update` (moteur, `:195-217`) avance à chaque
    image rendue, arrêté seulement par `IsPlaybackPaused` (`:54`, `:202`) ou la politique d'exécution.
    **Toutes les entités continuent de s'animer** pendant un `MenuOpen` : pendant l'inventaire, et déjà
    pendant les dialogues, alors que l'original fige tout (`EntityManager.cs:367-390`).
  - Le moteur offre de quoi figer sans le modifier : `IsPlaybackPaused` pour les animations ; pour le
    contrôleur, le mode `Disabled`, où `Update` sort avant toute intégration
    (`CharacterControllerComponent.cs:202-208`) et `Move` ne fait rien (`:425`). Mais `ControlMode` n'a
    qu'un accesseur privé en écriture (`:90`) : on ne le change que par `SetControlMode`, qui pour
    `Disabled` appelle **`Stop()`** (`:597-605`), et `Stop()` **efface** la vitesse, l'intention de
    déplacement, le saut demandé, les minuteries de saut, de coyote et de dash, et le déplacement vertical
    externe (`:400-416`) ; revenir au mode précédent ne rend que `Grounded` ou `Falling` (`:608-611`), l'état
    de saut est perdu. L'état complet se garde par **`CaptureStateSnapshot`** (`:530-554`) et se rend par
    **`RestoreStateSnapshot`** (`:556-587`), qui remet position, orientation, `ControlMode`,
    `MovementState`, `Velocity`, les minuteries et l'état de sol. Le précédent du portage,
    `SuspendGravityForWarpDeparture` (`AlundraPlayerManager.cs:485-527`), lève `IsVerticalOwnedExternally` :
    la composante verticale est alors retirée du déplacement (`:259-264`) puis la vitesse recalculée depuis
    le déplacement effectif (`:285-286`), ce qui **perd la vitesse verticale** — sans conséquence pour un
    départ de warp, gênant pour un saut qui doit reprendre. **D2 existe donc** (§3).
  - Restent hors de la porte, à juste titre : la publication de position `SyncTransform`
    (`AlundraEntityScriptProxy.cs:869`), l'échantillonnage de la manette (`:855-862`), les directeurs du
    dialogue et du HUD, qui tournent au tick (`AlundraWorldProxy.cs:1940-1943`, `:1957-1968`).
- **Commentaire périmé** : `AlundraPlayerManager.cs:555-558` affirme encore « our pipeline has no such
  global gate ». Il a trompé la reconnaissance puis la première rédaction de ce plan ; D4 le corrige,
  puisqu'elle touche ce fichier.

### 1.3 La grille, la navigation, l'équipement

- **Grille** de 24 cases, 6 colonnes × 4 rangées ; rangée 0 les armes, rangées 1-3 les objets
  (`MainInventoryManager.cs:1292-1456`). Contenu par case : `g_ItemIdBySlotIndex`
  (`StaticVariables.cs:12355-12361`) — `-1` veut dire « résoudre par la case », `0` vide, sinon un objet
  fixe. Décalages : `g_uiBoxesInventoryAnimationOffsetX/Y` (`:12363-12377`).
- **Navigation** (`:785-829`) : les quatre directions avec bouclage par rangée et par colonne, **son 1**,
  et remise à zéro du texte déroulant. **Croix** (`:831-844`) : case d'arme → `FUN_8005795c`, case d'objet
  → `FUN_80057854`. **Aucun bouton d'annulation** : fermer et annuler sont la même action.
- **Ces lectures utilisent la répétition des touches**, `ButtonsJustPressedByInterval`
  (`:785`, `:798`, `:810`, `:822`, `:834`, `:846`, `:853`) : un premier appui, puis après **20 images**
  maintenues une répétition toutes les `RepeatInterval` images, tant que l'état complet des boutons ne
  change pas (`PadManager.cs:26-74`, `PadState.cs:24-31`).
- **[lu] `RepeatInterval` vaut 0** (D0.4) : seule écriture, `GameInitializer.cs:169` ; `MaxNbFrameHeld` vaut
  20 (`PadState.cs:25`). Tracé de `UpdatePad` (`PadManager.cs:27-74`) : l'image de l'appui donne le front ;
  les 20 images suivantes rien ; puis **un front à chaque image** tant que l'état complet des boutons ne
  change pas (`NumberOfFrameHold < 0` n'étant jamais vrai). Tout changement de l'état, ou aucun bouton,
  remet à zéro. La manette est mise à jour en tête de boucle (`GameEngine.cs:1518`), avant `UpdateWorld`
  (`:1560`). **Le portage ne calcule pas ce champ** :
  `AlundraPadState` n'a que `ButtonsHold` et `ButtonsJustPressed` (`AlundraPlayerController.cs:34-39`),
  et un opcode d'évènement le signale déjà comme manquant (`AlundraEventProgramRunner.cs:1723-1739`).
- **Deux horloges** : l'original met la manette à jour une fois par image de sa boucle fixe à **50 Hz**
  (`GameEngine.cs:1518`). Le portage tourne avec `IsFixedTimeStep` à faux (`ProjectWriter.cs:63`) : l'état
  des boutons est reconstruit **par image rendue** (`AlundraEntityScriptProxy.cs:855-862`), alors que la
  logique avance en **ticks à 50 Hz**, zéro, un ou plusieurs par image rendue (`AlundraLogicClock.cs:5-16`).
  Un front d'appui et un compteur de répétition calculés par image rendue dépendraient donc de la cadence
  d'affichage, et un directeur qui tourne au tick verrait un même front deux fois, ou pas du tout.
- **[lu] Table des horloges** (D0.10) : l'état est échantillonné **par image rendue** dans
  `GameState.LastPadState` (`AlundraEntityScriptProxy.cs:855-862`, `AlundraGameState.cs:123`). Le héros le
  lit **par image rendue** (`MovePlayer`, `:1017`) ; le directeur du dialogue **par tick**
  (`AlundraDialogueDirector.cs:239`), l'opcode `0x2F` **par tick** (`AlundraEventProgramRunner.cs:1020-1029`),
  tous deux sur le même instantané. Une image à **deux ticks** leur montre deux fois le même front (le
  dialogue n'avale que l'appui d'ouverture, `AlundraDialogueDirector.cs:172-173`) ; une image à **zéro tick**
  le leur fait perdre. D1 calcule donc fronts et répétition **une fois par tick**, pour l'inventaire.
- **Équiper une arme** (`FUN_8005795c`, `:1618-1747`) : cinq appels à `SetPlayerWeaponId`, **son 2** à
  l'équipement, **son 3** pour une case vide. **Équiper un objet** (`FUN_80057854`, `:1796-1855`) :
  `SetCurrentItemId`, son 2. Toute la chaîne de données est **déjà portée** par E13.c S3.

### 1.4 Ce qui est dessiné

- **Sept boîtes** de fond, dont six propres à l'inventaire principal (`StaticVariables.cs:11196-11267`) :
  fond des armes (21×6 cases de 8 px), fond des objets (21×13), nom d'arme (18×4), nom d'objet (18×4),
  un espaceur vide, argent/faucons/clés (3×9), plus la boîte de description. Chaque boîte a **deux
  copies** de ses cases, `SpritesA` et `SpritesB`, dont une seule est montrée par image (voir plus bas).
- **[mesuré] Les boîtes ne sont pas des cadres réguliers** : le fond des armes compte **59 tuples
  distincts** `(u0, v0, w, h, clut)`, dont 32 à l'intérieur ; celui des objets **81**, dont 48 à
  l'intérieur ; argent/faucons/clés **21** sur 27 cases. Ce sont des images dessinées case par case, pas
  des cadres en 9 tranches : **le patron case par case doit voyager comme donnée**.
- **[mesuré] Les pixels sont déjà exportés** (D0.1, script et sortie au §7) : les six boîtes à dessiner
  comptent **822 cases** par copie, 1644 pour les deux copies A et B (point suivant) ; **chacune** a son
  tuple `(u0, v0, w, h, clut)` à l'identique
  parmi les entrées de `alundra-project/UI/wind-sprites.json` `(u0, v0, width, height, palette_index)` ;
  toutes font 8×8, toutes tombent dans le rectangle de leur boîte, **exactement** en `X + 8 × col`,
  `Y + 8 × rang`, dans l'ordre de la boucle de dessin : **les cases d'une copie ne se recouvrent pas**. Palettes : `clut` 3 pour la pierre (armes, objets),
  0 pour le parchemin (noms, description), 5 et 6 pour argent/faucons/clés. `wind.png` n'a que deux
  valeurs d'alpha, 0 et 255. La septième boîte, `UIBoxConfiguration_800b9a10`, fait 0×0 case : rien à
  dessiner. **Le patron** (quelle boîte, où, de quelle taille, quelle case dans quel ordre) n'existe que
  dans la décompilation (`StaticVariables.cs:11196-11267`).
- **[lu] A et B sont deux copies de la boîte, pas deux couches** (D0.2, corrigée après relecture, §7).
  `FUN_800548a4` (`GraphicManager.cs:1829-1879`) ne fait dans l'original qu'**initialiser** les deux
  copies : les seuls appels d'origine, en commentaire, sont les macros PsyQ `SetSprt`,
  **`SetSemiTrans(sprite, 0)`** et **`SetShadeTex(sprite, 1)`** (`:1858-1860`) ; l'appel à
  `Renderer.AddSprite` (`:1867`) est propre au portage de la décompilation, qui dessine ainsi les deux.
  Le dessin par image est `DisplayUiBoxes` (`0x80055d78`, `MainInventoryManager.cs:1227-1265`, appelée
  pour les sept boîtes `:914-920`) : il ajoute **une** copie à la table d'ordre, à l'adresse du tampon
  courant `g_drawModes[0x14].tag` (`:1237`, `:1247`). Le pointeur vers la copie choisie est perdu dans la
  transcription, mais c'est l'usage PsyQ du double tampon : une copie par tampon d'affichage, une seule
  montrée par image. **Le portage de la décompilation ne lit que `SpritesA`** (`:1254`). Semi-transparence
  coupée et pas de modulation : un texel d'alpha 0 laisse voir la scène, tout autre texel s'affiche tel
  quel ; `wind.png` n'ayant que les alphas 0 et 255, **une image cuite reproduit exactement une copie**.
- **[mesuré] A contre B** (script et sortie au §7) : identiques case pour case pour les objets, le nom
  d'objet, argent/faucons/clés et la description ; le B du nom d'arme n'a pas de données propres dans la
  décompilation, c'est un `Clone()` de A (`StaticVariables.cs:11206`) ; **la boîte des armes diffère sur 26
  cases sur 126**. Dessinée seule, **A donne un cadre complet** ; B seule perd la bordure biseautée de
  droite et le bas du bord gauche, et la superposition de A puis B, celle de la maquette montrée à
  l'auteur, a le même défaut. D'où D-E13D-14.
- **Derrière l'inventaire** (relevé du 2026-09-21, non revérifié en session principale) : la scène reste
  dessinée, figée, sans effacement ni assombrissement (`MainInventoryManager.cs:779-923`) ; les parties
  transparentes des boîtes, les bords roulés du parchemin, la laissent voir.
- **Icônes** : même chaîne que le HUD (E13.c), déjà portée.
- **[mesuré] Les graphismes hors des boîtes** (D0.9) :
  - **Curseur** : 16×16 (`MainInventoryManager.cs:119-120`), `clut` 0, 4 phases de 10 images lues dans
    `g_inventoryCursorTextureUVs` au pas de `0x28` (`:1600-1601`) : `(176, 160)`, `(192, 160)`,
    `(208, 160)`, `(224, 160)` → **`wind_159`, `wind_182`, `wind_210`, `wind_237`**, à l'identique
    (palette 0). Décalage par phase : `g_inventoryCursorAnimSpriteX/Y`.
  - **Cadre de sélection** : `(48, 152)`, 24×32, `clut` 0 (`:1279-1288`) → **`wind_039`**, à l'identique.
  - **Chiffres** : 0 à 9 en `(8 × n, 40)`, 8×16, `clut` 5 (`g_numbersSpriteSheetUVs`) → **`wind_000`,
    `002`, `009`, `016`, `023`, `030`, `037`, `045`, `055`, `065`**, les glyphes déjà utilisés par le HUD.
  - **Portrait d'ouverture : ABSENT de l'export.** `GetAnimationImageByIndex(0)`
    (`GraphicManager.cs:1786-1793`) rend l'image de portrait de l'enregistrement de sprite 0, la même
    fonction que les icônes d'objets. Mesuré dans le binaire par un programme jetable (§7) : signature
    `61779762221058`, page 2, palette 16, source `(200, 56)`, **48×56**. Aucun `.sprite` ne porte cette
    signature, et la planche exportée n'a **aucun pixel** à cet endroit (`(200, 568)` dans
    `map_alundra_spritesheet.png`, 0 pixel opaque sur 2688) : le relevé de S1.a ne couvrait que les icônes
    31 à 119. **Arrêt du §5 pour ce graphisme**, question au §6 point 6.
- **Textes** : le nom équipé (`DisplayIconNames`, `:1750-1793`) ; nom puis deux lignes de description de
  la case survolée, **déroulés un caractère toutes les 3 images** (`:929-1154`, états 0 à `0xcf`). Source :
  `EtcRes` (`IndexTable[id + 0x200 / 0x280 / 0x300]`), **déjà exportée** en `Dialogues/etc-index.json` et
  `Dialogues/global-strings.json` ; police `UI/font3.fnt` déjà exportée. **[lu] La DLL lit déjà les deux
  fichiers** (`AlundraEtcStringTable.cs:25-72`), mais n'expose que `TryResolveYesNo` : la recherche d'un nom
  et de deux lignes de description est à ajouter (D4).
- **[lu] Les sons 1 à 5** (D0.7) : présents dans `Sounds/sfx-manifest.json` (identifiants 1 à 5, une tonalité
  chacun, banque système `vab_id` −1), dans le même espace d'identifiants que `SoundManager.PlaySoundEffect`
  de l'original ; `IAlundraSoundPlayer.PlaySfx` les joue déjà (`AlundraSoundPlayer.cs:16-50`, `:139`). Seuls
  manquent les appels de l'inventaire : son 1 `:794`, `:806`, `:818`, `:830` ; son 2 `:1651`, `:1675`,
  `:1693`, `:1713`, `:1733`, `:1850` ; son 3 `:1745`, `:1818`, `:1836` ; son 4 `:495` ; son 5 `:1904`.
- **[lu] Les objets 92 à 97 n'atteignent aucune case** (D0.8) : leur colonne de case vaut 0
  (`StaticVariables.cs:831-836`), les objets fixes de `g_ItemIdBySlotIndex` sont 7, `0x24`, `0x29`, `0x25`,
  `0x26`, `0x27`, `0x20`, `0x28`, `0x36`, `0x3B`, `0x1F` (`:12357-12360`), et la résolution par case
  (`PlayerManager.cs:4370-4414`) ne cherche que des objets de la case demandée.
- **Chiffres** : argent (4), clés (2, nombre de l'objet 61), faucons (2) — **le faucon n'est pas porté**
  (`AlundraPlayerStats.cs`).
- **Portrait** : à l'ouverture, un quad du portrait d'Alundra grandit puis se résorbe (`:209-440`) ; ce
  n'est pas une capture de la scène.

### 1.5 Le HUD autour de l'inventaire

**[lu] Corrigé par D0.5** : la première rédaction inversait l'ouverture. À l'ouverture
(`MainInventoryManager.cs:484`), `InitializeHudPosition` **cache** la jauge, si elle est affichée et au
repos (`(g_drawFrameFlags & 3) == 1`), par un glissement de 15 ticks de sa place vers le haut, puis
`g_drawFrameFlags |= 2` (`HudManager.cs:26-39`). À la fermeture (`:850`), `InitializeHudPositionBeforeHide`
la fait **revenir**, si elle est cachée (`g_drawFrameFlags == 0`) et que le verrou persistant est posé
(`GameFlags[0x33] & 0x40000000`, drapeau 1662), avec `SetTransitionType(1)` et `g_drawFrameFlags = 5`
(`:42-57`).

| Original | Portage (`AlundraHudDirector`) |
|---|---|
| `InitializeHudPosition`, garde `(g_drawFrameFlags & 3) == 1` | **`ArmDisappearance`**, garde `((int)Phase & 3) == 1`, porté ligne à ligne (`:322-346`), privé |
| `InitializeHudPositionBeforeHide`, gardes verrou 1662 et `g_drawFrameFlags == 0`, `SetTransitionType(1)` | **`ArmAppearance`** (`:350` et suivantes), privé, qui porte aussi l'effet de `FUN_8004b770` armé par `SetTransitionType(1)` ; la garde du verrou y est garantie par l'appelant |
| — | aujourd'hui atteints seulement par les drapeaux 1813 (ouverture animée) et 1814 (fermeture instantanée) dans `RunTriggerMachine` (`:288-319`) |

Rien ne manque : D4 rend ces deux ports appelables par l'inventaire, avec la garde du verrou pour
`ArmAppearance`.

### 1.6 Côté portage : ce qui existe

- **Manette** : 9 boutons seulement (`AlundraPlayerController.cs:23-31`) ; `Start` existe déjà (action
  « Menu », Échap / Start). **`L1`, `L2`, `R1`, `R2`, `Select` n'existent nulle part** : ni bit, ni action,
  ni touche dans `Data/Alundra.buttonsMapping`, que le convertisseur écrit (`PlayerSetupWriter.cs:116-137`).
  Touches clavier déjà prises : flèches, Espace, X, C, Maj gauche, Échap.
- **Écran modal** : le dialogue est le précédent — `AlundraDialoguePresenter` pousse un `DialogueScreen`
  XAML, et le monde est gelé par un bit de `PlayerControlFlags`, pas par la pile d'écrans. `UILayer.Menu`
  est documenté pour « menu pause, inventaire, carte » ; `IsModal` doit être levé à part.
- **Règle** : un écran se déclare en XAML (ADR-0035), se teste avec un `MGDesktop` headless, jamais un
  `UIRoot`.

---

## 2. Décisions

### 2.1 Verrouillées (plan maître et auteur)

| Réf | Décision |
|---|---|
| D-E13D-1 | **Porter l'original tel quel**, fonctions citées ligne à ligne, comme E13.c — **pour la logique**. Amendée par l'auteur le 2026-09-21 pour le dessin : « on ne va pas respecter au pixel près le jeu original » (§2.2, point 1). |
| D-E13D-2 | **L'inventaire principal d'abord** ; le sous-inventaire et L1/R1 dans un second plan. |
| D-E13D-3 | **L'écran se déclare en XAML** (ADR-0035) ; le code retrouve par nom et pousse des valeurs. |
| D-E13D-4 | **Aucun contournement** : un manque de MGUI ou du moteur se consigne et arrête la tranche. |

### 2.2 Tranchées par l'auteur le 2026-09-21

| Réf | Décision de l'auteur | Conséquence |
|---|---|---|
| D-E13D-10 | « **Les icônes doivent être centrées dans la boîte parente. On ne va pas respecter au pixel près le jeu original.** » | Une icône se centre dans sa case par alignement, au lieu d'être calée à la position de l'original ; **l'auteur l'étend aux deux cases du HUD** (§6 point 2, appliqué à S3 par `cdb7097`). Le patron case par case des boîtes n'est plus une obligation ; la façon de dessiner les boîtes est tranchée par D-E13D-13. |
| D-E13D-11 | **Touches** : `Y` = `L2`, `U` = `L1`, `I` = `R1`, `O` = `R2`, `P` = `Select` ; à la manette, `LeftShoulder` = `L1`, `LeftTrigger` = `L2`, `RightShoulder` = `R1`, `RightTrigger` = `R2`, `Back` = `Select`. | D1 lie ces touches. |
| D-E13D-12 | **Le portrait d'ouverture dès la première passe.** **Amendée par l'auteur le 2026-09-21** : « reporter le portrait », absent de l'export (§6 point 6). | L'inventaire est livré sans portrait d'ouverture ; D3.c et D5.p sont retirées ; le portrait reviendra avec une future extraction. |
| D-E13D-13 | « **Comment dessiner les boîtes : 1 avec image cuite.** » Le fond de chaque boîte a l'aspect de l'original, **cuit une fois par le convertisseur en une image par boîte**, après que l'auteur a vu la maquette des trois façons (§6 point 1). | D3 devient D3.a (le patron en CSV dans l'analyseur) et D3.b (la cuisson et six sprites dans le convertisseur) ; D5 affiche une image par boîte ; D0.1 et D0.2 sont closes (§1.4). La maquette superposait les copies A et B ; l'image cuite n'en prend qu'une (D-E13D-14). |
| D-E13D-15 | « **Tout `MenuOpen`** » (2026-09-21) : le gel de D2 vaut pour tout `GameplayBlockedMask`, fidèle à l'original | Les PNJ et le héros cessent aussi de s'animer pendant les dialogues, comme dans l'original ; D6 le vérifie aussi en dialogue. |
| D-E13D-16 | « **Dans la DLL** » (2026-09-21) : le XAML de l'écran d'inventaire vit dans `Alundra/Screens/`, embarqué dans `Alundra.dll` et chargé comme le `DialogueScreen` du moteur. Decisions: see ADR-0001 | Aucune modification du convertisseur ; l'écran ne s'ouvre pas comme `.uiscreen` dans l'éditeur |
| D-E13D-17 | « **Harnais minimal dans Alundra.Tests** » (2026-09-21) : un `MGDesktop` sans affichage, copie réduite de celui de `CasaEngine.Tests`. Decisions: see ADR-0001 | Les tests de l'écran XAML tournent dans `Alundra.Tests`, sans modifier le moteur |
| D-E13D-14 | « **Cuire la copie A seule.** » Confirmé par l'auteur le 2026-09-21 : chaque boîte est cuite depuis sa copie `SpritesA`, sans la superposer à B | le portage de la décompilation ne lit que A (`MainInventoryManager.cs:1254`) ; A et B sont identiques pour cinq boîtes sur six ; pour la boîte des armes, seule A donne un cadre complet (§1.4). Écart visible avec la maquette montrée à l'auteur : la bordure droite de la boîte des armes, que la superposition abîmait. D3.a n'exporte que A, D3.b ne cuit que A |

### 2.3 Proposées, sauf avis contraire

| Réf | Décision proposée | Pourquoi |
|---|---|---|
| D-E13D-5 | `Falcon` et `FalconTemp` portés sur `AlundraPlayerStats` **à 0**, la valeur de la nouvelle partie, et affichés ; aucune mécanique de faucon | fidèle à l'état réel du jeu à ce stade, sans inventer de système |
| D-E13D-6 | Un **directeur** d'inventaire pur (machine à états au tick, sans MGUI), un **présentateur** qui pousse vers une vue, un **écran XAML** — le découpage du dialogue et du HUD | testable sans tête, déjà prouvé deux fois dans ce dépôt |
| D-E13D-7 | L'écran en `UILayer.Menu`, `IsModal = true` | la couche que le moteur destine à l'inventaire, et le blocage des écrans inférieurs |
| D-E13D-8 | **La répétition des touches est portée**, pas contournée : `ButtonsJustPressedByInterval` calculé comme `PadManager.UpdatePad` | la navigation de l'original en dépend ; l'opcode qui le lit en profite aussi |
| D-E13D-9 | **Fronts et répétition comptés au tick logique**, l'équivalent de l'image à 50 Hz de l'original : l'état des boutons reste échantillonné par image rendue, mais l'inventaire lit des fronts et une répétition mis à jour une fois par tick | un appui donne exactement un front, et 20 ticks valent 20 images de l'original, quelle que soit la cadence d'affichage |

---

## 3. Tranches

Un commit par tranche, un vérificateur frais par tranche à risque, régime de preuve par double export dès
que le convertisseur est touché. **Chaque tranche ne commence que lorsque ses prérequis sont clos.**

### ✅ D0 — Les mesures (lecture seule) — close le 2026-09-21

**Prérequis** : aucun. **Livrable commun** : chaque résultat est reporté au §1 de ce plan, marqué
**[mesuré]**, avec sa citation ; les scripts de mesure sont recopiés en entier dans le journal (§7), avec
leur sortie, pour que la mesure soit refaisable.

| # | Mesure | Livrable | Close quand |
|---|---|---|---|
| D0.1 | ✅ *close le 2026-09-21, [mesuré] au §1.4, script au §7* — **Rapprochement des cases avec les `wind_NNN`** : un script relit tous les `new SPRT{…}` des tableaux `SpritesA`/`SpritesB` des sept boîtes et cherche chaque tuple `(u0, v0, w, h, clut)` **à l'identique** dans `alundra-project/UI/wind-sprites.json` `(u0, v0, width, height, palette_index)` ; la règle d'égalité, et toute table de passage entre `clut` et `palette_index` si elle existe, sont écrites avant de lancer le script | nombre de cases par boîte, nombre de tuples introuvables, et chaque tuple introuvable listé | le script a tourné sur les sept boîtes et son résultat est au §1.4 |
| D0.2 | ✅ *close le 2026-09-21, [lu] et [mesuré] au §1.4 : deux copies, pas deux couches ; comparaison A/B au §7* — **Le mode de mélange** des couches A et B (bits de `code`/`tag` des `SPRT`, et ce que `Renderer.AddSprite` en fait) | le mode par couche, cité | chaque couche des sept boîtes a son mode |
| D0.3 | ✅ *close, §1.2 : gravité et animations tournent hors de la porte, D2 existe* — **Ce qui tourne hors de la porte T2** pendant `MenuOpen` : vitesse et gravité du contrôleur de personnage côté moteur, lecture d'animation de sprite côté moteur ; et le cas d'une ouverture en plein saut | pour chacun : figé ou non, cité dans le code du moteur | la liste est complète et citée ; elle décide si une tranche de gel existe (voir D2) |
| D0.4 | ✅ *close, §1.3 : `RepeatInterval` = 0* — **La répétition des touches** : où l'original fixe `RepeatInterval` et sa valeur ; l'ordre de mise à jour dans la boucle | la cadence exacte | la valeur est citée |
| D0.5 | ✅ *close, §1.5 : table faite, le §1.5 corrigé* — **Le HUD** : correspondance entre `InitializeHudPosition`/`InitializeHudPositionBeforeHide` et leurs conditions, et les drapeaux 1813/1814 du directeur du portage | une table condition par condition | chaque condition a son équivalent, ou est déclarée manquante |
| D0.6 | ✅ *close, §1.1* — **`StartFadeOut`** et **`g_forbiddenWarpFlag`** : effet réel, sens des valeurs 0, 2, 5 et du test `& 6` | le sens de chaque bit, cité | chaque valeur a un sens établi par le code, ou est déclarée inconnue |
| D0.7 | ✅ *close, §1.4 : lecture présente mais incomplète ; sons présents* — **Ce que la DLL sait déjà faire** : lire `etc-index.json`/`global-strings.json` (un équivalent d'`EtcRes`), jouer les sons 1 à 5 de l'interface | présent ou absent, cité | les deux questions ont une réponse |
| D0.8 | ✅ *close, §1.4 : non* — **Les objets 92 à 97** (nom sans propriétés ni icône) : peuvent-ils atteindre une case de la grille ? | oui ou non, par la table et la résolution par case | la réponse est citée |
| D0.9 | ✅ *close, §1.4 : tout exporté sauf le portrait* — **Les graphismes hors des boîtes** : curseur (`g_inventoryCursorTextureUVs`), cadres de sélection, chiffres (`g_numbersSpriteSheetUVs`), portrait d'ouverture — leur source dans l'original, et l'actif exporté qui porte chacun | pour chaque graphisme : source citée et actif exporté, ou « absent → extraction » | chaque graphisme dessiné par D5 a sa ligne |
| D0.10 | ✅ *close, §1.3* — **Les horloges du portage** : où l'état des boutons est échantillonné, où chaque consommateur (héros, dialogue, opcode `0x2F`) le lit, sur quelle horloge ; et ce qu'un tick nul ou double fait à un front d'appui aujourd'hui | la table consommateur par consommateur, citée | la table est complète ; elle fixe où D1 met à jour fronts et répétition |

**Arrêt propre à D0** : si une mesure contredit un fait du §1 ou une recommandation du §6, le plan est
révisé et **resoumis à l'auteur avant** que D1, D2 ou D3 ne commence. **Déclenché le 2026-09-21** : §1.2 et
§1.5 contredits, corrigés ci-dessus ; resoumis (§7).

### ✅ D1 — Manette : cinq boutons et la répétition — faite le 2026-09-21

**Prérequis** : D0.4, D0.10 ; touches fixées par D-E13D-11.
Cinq bits sur `AlundraPadState` (`L1`, `L2`, `R1`, `R2`, `Select`), cinq lignes d'`ActionBits`, cinq
liaisons dans `PlayerSetupWriter` (clavier `U`, `Y`, `I`, `O`, `P` et manette `LeftShoulder`,
`LeftTrigger`, `RightShoulder`, `RightTrigger`, `Back`, pour `L1`, `L2`, `R1`, `R2`, `Select`, D-E13D-11) ; et `ButtonsJustPressedByInterval` porté ligne à ligne depuis
`PadManager.UpdatePad` (D-E13D-8), **mis à jour une fois par tick logique** (D-E13D-9). Tests, à cadence
de rendu variable, avec des images à zéro tick et à deux ticks : un appui donne exactement un front ;
une touche maintenue se tait 20 ticks, puis répète **à chaque tick** (`RepeatInterval` = 0, porté avec sa
valeur, §1.3) ; un changement de l'état des boutons remet à zéro. Les consommateurs existants (héros,
dialogue, opcode `0x2F`) ne changent pas d'horloge (§6 point 4). Le convertisseur change :
**régime de preuve complet**, diff prédit = `Data/Alundra.buttonsMapping` + `report.json`.

**Fait** : `AlundraPadState` gagne `L1`, `L2`, `R1`, `R2`, `Select` aux valeurs de l'original ;
`ActionBits` et `PlayerSetupWriter` les lient aux touches de l'auteur, ajoutées après les treize actions
existantes ; `AlundraTickPad` porte `PadManager.UpdatePad` ligne à ligne, et `AlundraGameState.TickPad` avance
une fois par tick en tête de `AlundraWorldProxy.Update`, hors de la porte. **Un consommateur de ses fronts
doit les lire dans cette même boucle**, juste après chaque mise à jour (D4).

| Preuve | Résultat |
|---|---|
| Prédiction écrite avant le code | diff = `Data/Alundra.buttonsMapping` + `report.json` |
| Export complet en place, contre le manifeste de référence | **exactement** ces deux fichiers ; 18 liaisons, les 13 premières identiques à l'octet |
| Double export | `report.json` seul |
| Suites | convertisseur 172/172, `Alundra.Tests` **947/947** (930 + 17) |
| Vérificateur frais | **CONFIRMED**, trois remarques P4 : la boucle de la manette n'est testée que par simulation (le sera par D4) ; l'arbre porte des changements hors D1, non indexés ; un appui plus court qu'une image sans tick se perd, par construction (D-E13D-9) |

### ✅ D2 — Le gel, pour ce que la porte T2 ne couvre pas — faite le 2026-09-21

**Prérequis** : D0.3 (close : D2 existe) ; portée tranchée par l'auteur : tout `MenuOpen` (D-E13D-15).
DLL seule, sans changement moteur. Pendant que `GameplayBlockedMask` est posé (et lui seul : le gel du
warp garde son propre mécanisme, T4) :
- **les contrôleurs de personnage s'arrêtent** : sur chaque entité qui en a un, l'état est saisi par
  `CaptureStateSnapshot`, puis `SetControlMode(Disabled)` ; à la levée du masque, `RestoreStateSnapshot`
  rend tout (§1.2), si bien qu'**une chute reprend où elle s'était arrêtée**, vitesse verticale et état
  `Falling` compris, comme dans l'original qui saute simplement `UpdateEntities`. Pas de suspension de gravité à la manière de
  T4, qui perdrait la vitesse verticale. Ce que la restauration remet à zéro : le dernier contact et les
  touches de collision et de marche (`_lastContact`, `_lastCollisionHit`, `_stepSupportHit`), recalculés
  au pas suivant ; la référence au support dans l'état de sol, retrouvée par `UpdateGround` au pas
  suivant ; la position, rendue à celle de l'instantané, que rien ne déplace pendant le gel puisque tout ce
  qui la déplace est derrière la porte ; et **le déplacement vertical externe**, qui, lui, **n'est pas sans
  effet** : `Stop()` et `RestoreStateSnapshot` le mettent à 0 (`CharacterControllerComponent.cs:483-492`,
  `:586`), le moteur n'a pas d'accesseur pour le relire, et la DLL ne le redéclare **qu'une fois par tick**
  (`AlundraScriptedMotion.cs:140-144`, `AlundraEntityScriptProxy.cs:670-688`). Sur une image de dégel sans
  tick, le contrôleur tournerait avec `IsVerticalOwnedExternally` levé et une valeur nulle, donc
  `UpdateGround` ramènerait au sol tout ce qui en est à moins de `GroundSnapDistance` — la régression déjà
  mesurée une fois pour l'escalade (`AlundraScriptedMotion.cs:117-130`). **D2 le redéclare donc juste après
  `RestoreStateSnapshot`**, avant la prochaine mise à jour du contrôleur, avec la valeur que son
  propriétaire déclare : la sentinelle d'escalade pour un héros en `Climbing` ou `ClimbStill`
  (`ClimbingExternalDisplacementSentinel`), `FinalForceZ / 65536f` pour un PNJ piloté par le contrôleur ;
- **les animations de sprite s'arrêtent** : `IsPlaybackPaused` levé sur chaque entité, sa valeur
  précédente gardée et rendue à la levée ; une entité créée pendant le gel est gelée à son tour.
Détection du passage par comparaison avec l'état de l'image précédente, au même endroit que la porte
(`AlundraEntityScriptProxy.Update`). **Tests** : gel puis dégel rendent exactement l'état d'avant
(`ControlMode`, `MovementState`, `Velocity`, minuteries, pause d'animation) ; **une chute gelée en l'air
reprend avec la même vitesse verticale et l'état `Falling`** ; sur le contrôleur seul, un saut demandé par
`RequestJump` synthétique garde l'état `Jumping` à travers le gel ; une entité déjà en pause le reste ;
l'escalade et le départ de warp ne sont pas dérangés ; **dégel sur une image à zéro tick** : un héros en
`ClimbStill` à moins de `GroundSnapDistance` du sol garde sa position, un PNJ en montée reste en l'air. **Arrêt** : si l'une des remises à zéro ci-dessus a un effet visible
(un pas de travers au dégel, une plateforme mobile qui décroche, une escalade qui lâche), ou si quelque
chose déplace une entité pendant le gel, la tranche s'arrête et le consigne (D-E13D-4).

**Fait** : `AlundraGameplayFreeze` (gel, dégel, redéclaration du déplacement vertical externe), un état par
entité sur `AlundraEntityScriptProxy`. **Écart avec le texte ci-dessus, pour la justesse** : la passe ne
tourne pas dans `AlundraEntityScriptProxy.Update` mais **à la fin d'`AlundraWorldProxy.Update`**, sur toutes
les entités créées. Le moteur met à jour contrôleurs et sprites **avant** les proxys de l'image
(`World.Update` → `CharacterMotion` → `Entity.Update`, `Entity.cs:478-508`), et `MenuOpen` change pendant
les proxys, dialogue compris (`AlundraDialogueDirector.Tick`, dans le proxy du monde) : appliqué par
entité, le gel laisserait passer une image de chute de trop, et le dégel une image figée de trop ; appliqué
en fin d'image, il prend effet dès la mise à jour suivante du moteur. Le gel du warp n'est pas touché (T4).

| Preuve | Résultat |
|---|---|
| Tests | `Alundra.Tests` **959/959** (947 + 12) : gel et dégel exacts (`ControlMode`, `MovementState`, `Velocity`, minuteries), un seul instantané par gel, redéclaration pour un PNJ et pour le héros sur l'échelle, pause d'animation rendue, câblage par le vrai `AlundraWorldProxy.Update` ; en production, **une chute gelée reprend avec la même vitesse**, et **un dégel sur une image sans tick garde un héros agrippé à 3 px du sol** |
| Mutation | la redéclaration retirée, le test de l'échelle et trois tests unitaires échouent ; code rendu à l'octet près |
| Vérificateur frais | **CONFIRMED** ; ordre du moteur et écart de site confirmés dans le code ; cas limites sans régression (entité créée ou détruite pendant un gel, gel posé et levé dans la même image, `AnimationFinished`, passes hors porte, position restaurée) ; une remarque P4 : la restauration perd la référence au support de sol, sans effet sur Alundra dont le sol vient du champ de collision | **Vérificateur frais** (changement de comportement partagé par tout `MenuOpen`).

### ✅ D3.a — Analyseur : le patron des boîtes en CSV — faite le 2026-09-21 (analyseur `64978f8`)

**Prérequis** : D-E13D-13, D-E13D-14 ; D0.1 et D0.2 (closes).
**Branche** : dans l'analyseur, `chantier/e13d-boites`, créée depuis `master` après le merge d'E13.c
(`c204009`).
Deux fichiers dans `AlundraTools/AlundraTools/`, liés au projet comme `ItemsProperties.csv`, générés depuis
les tableaux décompilés et **bruts**, sans interprétation (précédent S1.b et S1.c) :
- `UiBoxes.csv`, `box;x;y;width;height` : les **sept** boîtes de `StaticVariables.cs:11196-11267`, `box`
  étant le nom de la variable de la décompilation, l'espaceur 0×0 compris ;
- `UiBoxCells.csv`, `box;cell;x0;y0;u0;v0;w;h;clut` : les cases de la copie **A** (`SpritesA`,
  D-E13D-14), **822 lignes**, `cell` l'indice dans le tableau. La copie B n'est pas exportée ; sa
  comparaison avec A reste au §7.

**Acceptation** : un script indépendant relit `StaticVariables.cs` et retrouve chaque ligne des deux CSV,
et rien de plus ; 7 boîtes, 822 cases.

**Fait** : les deux CSV générés par un script (§7), dans l'ordre de dessin des sept appels à
`DisplayUiBoxes` (`MainInventoryManager.cs:914-920`), déclarés dans `AlundraTools.csproj` (projet chargé par
MSBuild, les deux éléments vus). **Acceptation passée** par un second script, écrit sans rien reprendre du
premier (lecture ligne à ligne, sans appariement de crochets) : 7 boîtes, 822 cases relues, **0 manquante,
0 en trop, même ordre**. La preuve au pixel de D3.b, qui recompose depuis `StaticVariables.cs` sans passer
par les CSV, la confirmera une seconde fois.

### ✅ D3.b — Convertisseur : une image cuite par boîte — faite le 2026-09-21

**Prérequis** : D3.a.
- **Lecture** : `UiBoxLayoutReader` lit les deux CSV, liés dans le `.csproj` du convertisseur comme ceux
  de S2 (précédent `ItemsPropertiesCatalogReader`, avertissements par ligne mal formée).
- **Cuisson** : une nouvelle phase, `Phase7.UiBoxes`, juste après `Phase7.Ui`, lit les mêmes entrées
  qu'elle, `data-extracted/ui/wind.png` et `wind.json`. Pour chaque boîte de taille non nulle, une image
  RGBA transparente de `8 × width` sur `8 × height` ; chaque case de la copie A est recopiée **telle
  quelle, alpha compris**, de l'atlas depuis `(u0, v0)` vers `(x0 − X, y0 − Y)` : les cases ne se
  recouvrant pas (§1.4), il n'y a rien à mélanger, et l'alpha 0 de l'atlas reste la transparence qui
  laisse voir la scène (D0.2). Une case qui sortirait du rectangle de sa boîte, ou en recouvrirait une
  autre, est une **erreur** du rapport, et sa boîte n'est pas émise. Une case dont le tuple
  `(u0, v0, w, h, clut)` n'a pas d'entrée `(U0, V0, Width, Height, PaletteIndex)` égale dans `wind.json`
  est une **erreur** du rapport, et sa boîte n'est pas émise. Composition en `System.Drawing`, comme
  `BackdropImageBuilder`.
- **Émission** : pour chaque boîte, `UI/Textures/<box>.png` et son `.texture` par
  `TextureAssetWriter.EnsureTexture`, puis `UI/<box>.sprite` couvrant l'image entière, identifiant
  `Ids.For("sprite-ui:" + box)`. **Piège** : `UiWriter` sauve déjà le catalogue (`UiWriter.cs:60`) ; la
  nouvelle phase le sauve à son tour, comme `BackdropWriter` (`:64-68`).
- **Compteurs** : `UiBoxes.Boxes` = 6, `UiBoxes.Cells` = 822, `UiBoxes.CellsWithoutTile` = 0.
- **Tests** : le lecteur sur des CSV synthétiques ; la cuisson sur un atlas synthétique (décalage
  `x0 − X`, alpha 0 et 255 recopiés tels quels, boîte 0×0 ignorée, case sans tuile en erreur, case hors
  de sa boîte ou recouvrant une autre en erreur).

**Régime de preuve complet** : manifeste de référence avant toute modification ; **diff prédit** =
6 × (`.png`, `.texture`, `.sprite`) nouveaux + `AssetInfos.json` + `report.json` ; export complet en place ;
mesuré ⊆ prédit ; double export ⊆ `{report.json}`. **Preuve au pixel** : chaque image cuite est égale,
pixel pour pixel, à une composition indépendante faite par script depuis les `SpritesA` de
`StaticVariables.cs` et `wind.png`, sans passer par les CSV ni par le code du convertisseur — ce qui
prouve aussi D3.a. Et **la boîte des armes cuite montre sa bordure droite** : le choix de A (D-E13D-14) se
voit dans l'image, pas seulement dans le code.

**Fait** : `UiBoxLayoutReader` (lecture brute), `UiBoxBaker` (cuisson pure, octets en entrée et en
sortie, testable sans fichier), `UiBoxWriter` (phase `Phase7.UiBoxes`, entrées-sorties en `System.Drawing`
comme les fonds). Les six sprites, pour D5 :

| Boîte | Sprite | Taille |
|---|---|---|
| `g_UiBoxesInventoryWeaponBackground` | `c2447262-414f-5b78-9def-b54b080636f4` | 168×48 |
| `g_UiBoxesInventoryItemBackground` | `615fe384-c2e5-5ad3-82ec-f0bd137c877a` | 168×104 |
| `g_UiBoxesInventoryWeaponNameBackground` | `eb357d61-17ab-5ea1-a287-b98196a6e7e3` | 144×32 |
| `g_UiBoxesInventoryItemNameBackground` | `1f5d5bab-7408-5da3-a8d6-20a02d3c0747` | 144×32 |
| `g_UiBoxesInventoryMoneyFalconKeyIcons` | `e8d58247-f02b-57cb-9ebf-f1df2b9be874` | 24×72 |
| `g_uiBoxesInventoryDescriptionBackground` | `973a9208-c867-57fe-bee3-cf30237221ef` | 288×56 |

| Preuve | Résultat |
|---|---|
| Prédiction écrite avant le code | 18 ajouts (6 × `.png`, `.texture`, `.sprite`) + `AssetInfos.json` + `report.json` |
| Export complet en place | **exactement** la prédiction ; aucun fichier existant touché ; `UiBoxes.Boxes` 6, `UiBoxes.Cells` 822, `UiBoxes.CellsWithoutTile` 0, aucune erreur |
| Double export | `report.json` seul (encodage PNG déterministe) |
| Preuve au pixel | **0 pixel différent** sur les six boîtes, en RGBA complet, contre une composition faite depuis `StaticVariables.cs` et `wind.png` seuls ; la boîte des armes a sa bordure droite |
| Suites | convertisseur **182/182** (172 + 10) |
| Vérificateur frais | **CONFIRMED** ; deux remarques P4 : la preuve au pixel ne regarde pas la palette (couverte par la recherche dans `wind.json` et `CellsWithoutTile` = 0) ; une boîte refusée lors d'un export ultérieur laisserait ses anciens fichiers, comme tout export en place |

### ✅ D4 — DLL : le directeur de l'inventaire — faite le 2026-09-21

**Prérequis** : D0.5, D0.6, D0.7, D0.8, D0.10 (closes), D1, D2.
Le directeur lit les fronts et la répétition de D1, au tick, jamais l'instantané par image rendue.
Machine à états au tick, portée ligne à ligne : déclencheur et ses gardes (§1.1 : `g_forbiddenWarpFlag`,
l'emplacement 0 porté par « une boîte de dialogue est ouverte », l'emplacement 0xb — menu de débogage non
porté — déclaré toujours inactif, la branche `g_cdIsReady == 0` non portée), `g_forbiddenWarpFlag` et ses
bits, glissement des sept boîtes en 15 ticks, `MenuOpen` et son retrait en fin de glissement, curseur et
bouclages sur la répétition des touches, équipement d'arme et d'objet par les fonctions d'E13.c, texte
déroulant, **sons 1 à 5 par `PlaySfx`** aux lignes du §1.4, **HUD** : `ArmDisappearance` à l'ouverture et
`ArmAppearance` à la fermeture, rendus appelables (§1.5), `Falcon`/`FalconTemp` à 0 (D-E13D-5) ; la
recherche du nom et des deux lignes de description d'un objet ajoutée à `AlundraEtcStringTable`. Corrige
au passage le commentaire périmé de `AlundraPlayerManager.cs:555-558`. Aucune dépendance MGUI.

**Fait** (par un exécuteur, sur contrat de la session principale, après une reconnaissance à quatre
surfaces) : `AlundraInventoryDirector`, porté fonction par fonction ; `AlundraHudDirector` gagne deux
entrées internes, `InitializeHudPosition` et `InitializeHudPositionBeforeHide`, qui appliquent les gardes
de l'original avant ses méthodes existantes ; `AlundraEtcStringTable` gagne le nom et les deux lignes de
description d'un objet (`IndexTable[id + 0x200 / 0x280 / 0x300]`, confirmés dans `EtcResUsa.cs:57-106`) ;
`Falcon`/`FalconTemp` à 0 ; le commentaire périmé corrigé. Le directeur tourne **dans la boucle de la
manette au tick**, juste après `TickPad.Update`.

**Ce que D5 lit** (sur `AlundraInventoryDirector.Instance`) : `IsActive`, `ForbiddenWarpFlag`,
`BoxPosition(index)` (les sept boîtes, dans l'ordre de `DisplayUiBoxes`), `SelectedSlotId`,
`CursorFrameDelay` (0 à `0x27`), `EquippedWeaponName`, `EquippedItemName`, `TextRevealState`,
`NameVisiblePrefix`, `Description0VisiblePrefix`, `Description1VisiblePrefix`. **À l'image de la mise en
place, `IsActive` est déjà vrai mais les boîtes sont encore à leur origine : l'original ne dessine rien à
cette image-là** (remarque A4 de la vérification) ; D5 n'affiche qu'à partir de la suivante.

**Ordre établi** : dans l'original, `RenderScene()` passe avant `Update(0)` (`GameEngine.cs:225-229`) ; le
déclencheur est dans `Update` N, la mise en place dans le rendu N+1, le premier `FUN_80056598` dans le
rendu N+2. Avec un tick du portage = un `Update` suivi du rendu suivant : déclencheur et mise en place au
tick N, gestion par image dès N+1 ; `MenuOpen` est vu par la logique du tick N+1 dans les deux.

**Gardes déclarées absentes**, chacune citée : `g_warpLockTimer` (aucun système de ce genre porté),
`g_globalTransitionState` (menu de carte mémoire, non porté), et `g_warpDelayFrames` : le portage a le
champ (`AlundraWarpDirector.WarpDelayFramesForTests`, 10 à chaque entrée de carte) mais **ne le décrémente
jamais**, contrairement à `GameEngine.cs:1561-1564` ; le lire bloquerait l'inventaire pour toujours. **Écart
qui en reste** : l'original refuse l'inventaire pendant les 10 premières images d'une carte (0,2 s), le
portage non (§6 point 9).

| Preuve | Résultat |
|---|---|
| Première vérification | **REFUTED**, un P1 reproduit : `global-strings.json` contient 562 valeurs `null` (la seconde ligne de description du Poignard, par exemple) que l'original lit comme vides (`?? string.Empty`, `MainInventoryManager.cs:972/:1010/:1041`) ; le portage levait une exception ~130 ticks après l'ouverture. Plus un P3 : les deux tables relues et analysées 50 fois par seconde |
| Correctif | une valeur `null` devient une chaîne vide dans la table ; les deux tables en cache, rechargées si un fichier change ; les deux commentaires faux corrigés ; deux tests de régression |
| Reproduction | le programme du vérificateur, sur l'export réel : état final `0xcf`, « Poignard », « Petit poignard. », seconde ligne vide, **aucune exception** |
| Suites | `Alundra.Tests` **988/988** (959 + 29) |
| Vérification neuve | **CONFIRMED** ; cache sans données périmées (changement de dossier, 200 réécritures), `TryResolveYesNo` inchangé ; deux remarques P4 reportées : le message de journal d'un échec parle encore de OUI/NON, et le cache n'est pas protégé contre des appels concurrents (aucun aujourd'hui) |

### ✅ D5 — DLL : l'écran XAML, le présentateur, la capture — faite le 2026-09-21

**Prérequis** : D0.9 (close), D3.b, D4.
Écran XAML (**six images de boîte**, les sprites de D3.b, chacune à son `(X, Y)` natif et glissant pour son
compte, grille d'icônes **centrées dans leur case** (D-E13D-10), curseur (`wind_159`, `wind_182`,
`wind_210`, `wind_237`), cadres de sélection (`wind_039`), textes en `font3`, chiffres (les glyphes du HUD)),
**sans le portrait d'ouverture**, qui est D5.p ; présentateur
branché comme celui du dialogue. Le XAML ne sait pas désigner un actif par identifiant : il nomme les
images, le code leur donne leur source, avec les identifiants des six sprites en constantes, comme les 24
glyphes du HUD (`AlundraHudScreen.cs:447-478`). Tests headless sur `MGDesktop` ; capture en
processus de l'inventaire ouvert, **prédite avant d'être prise**. **D5 se clôt sans le portrait**, quelle
que soit la réponse au §6 point 6.

**Fait** : `Alundra/Screens/InventoryScreen.xaml`, embarqué dans la DLL (D-E13D-16, ADR-0001) ;
`AlundraInventoryScreen` (`XamlUIScreenBase`, couche `Menu`, modal, agrandi au rendu par
`RenderTransform.Scale` ; enregistre font3 une fois par processus, et sinon journalise une fois et garde la
police par défaut, sans contournement) ; `AlundraInventoryComposer`, pur, qui compose au tick l'affichage
entier (boîtes, icônes centrées, cadres, curseur, textes, chiffres) ; `AlundraInventoryPresenter`, qui
empile l'écran à la première image dessinée et le retire à la fin du glissement de fermeture ;
`AlundraWorldProxy` le branche une fois et le fait tourner dans la boucle de la manette, juste après le
directeur. Le directeur gagne `IsDrawn` (rien à l'image de la mise en place), `ResolveSlotItemId` et
`DrawnDescriptionLine0/1` (ci-dessous). Tests : un harnais `MGDesktop` sans affichage dans
`Alundra.Tests/UI` (D-E13D-17), le XAML chargé et ses éléments nommés trouvés, le compositeur et le
présentateur sans MGUI.

**Ce que l'écran montre du texte** : ce que `DisplayInventoryDescription` de l'original a dessiné **à ce
tick-là**, publié par le directeur (`DrawnDescriptionLine0/1`) : rien à l'état 0 ni à 0x4d, rien sur une case
vide ou un objet non possédé, la ligne 0 seule jusqu'à 0x8e, les deux lignes dès 0x8f
(`MainInventoryManager.cs:929-1064`). Les préfixes bruts gardent leur valeur d'un tick à l'autre ; ce n'est
pas ce que l'écran lit. **Curseur** : sa position vient du compteur **avant** l'incrément de ce tick
(`:907-910`), son image du compteur après (`:1593-1601`).

| Preuve | Résultat |
|---|---|
| Prédiction écrite avant la capture | fenêtre 1280×944, échelle 4 ; les six boîtes à leur place de repos, la bordure droite de la boîte des armes ; l'épée de base en case 0 à (64,96), le cadre de sélection dessus, le curseur vers (136,64) ; « Poignard » en nom d'arme ; `0000`, `00`, `00` ; capture A : le nom sur la ligne 0 ; capture B : « Petit poignard. », ligne 1 vide ; font3 enregistrée ou un avertissement |
| Capture en processus (back-buffer, héros aux commandes, `Start` par la vraie entrée) | **conforme à la prédiction** : boîtes identiques à leurs images cuites hors des éléments posés dessus, épée et cadre à (64,96), curseur en phase 0 (A) et en phase 3, décalé de (-1,0) (B), « Poignard » puis « Petit poignard. », chiffres à zéro, font3 enregistrée (aucun avertissement) ; les objets 17 et 25 de la nouvelle partie ne sont pas dans la grille (ils vont au sous-inventaire) |
| Première vérification | **REFUTED** : un P2 reproduit sur l'export réel, la seconde ligne de description affichée sans condition, donc restée à l'écran après un déplacement vers une case vide ou non possédée et pendant le déroulement du nom suivant ; un P3, la ligne 0 affichait un tick de trop à l'état 0x4d ; un P4, la position du curseur prenait le compteur après son incrément |
| Correctif | le directeur publie ce qui est dessiné au tick (`DrawnDescriptionLine0/1`), le compositeur l'affiche tel quel ; phase de position du curseur `((d + 0x27) % 0x28) / 10` ; un test du directeur au tick près, des tests du compositeur sur les ticks de changement de phase |
| Reproduction | le programme du vérificateur, sur l'export réel : après le déplacement, lignes 0 et 1 vides ; retour sur les herbes, le nom sur la ligne 0 et rien sur la ligne 1 |
| Capture rejouée après le correctif | textes, nom d'arme, chiffres et case 0 **identiques au pixel** au premier passage ; seul le décor animé diffère |
| Suites | `Alundra.Tests` **1039/1039** (988 + 51) |
| Vérification neuve | **CONFIRMED** : les branches du texte et du curseur relues contre l'original, un tick à 0x4d et une image à deux ticks échantillonnés, l'empilement et le retrait de l'écran inchangés ; une remarque P4 : la capture en jeu ne passe pas par le cas du P2 (couvert par la reproduction et le test), à voir en D6 |

### ~~D3.c — Extraction du portrait d'ouverture~~ — retirée, portrait reporté (D-E13D-12 amendée)

**N'existe que si l'auteur répond (a) au §6 point 6.** Elle écrit hors du dépôt (ré-extraction dans
`Alundra Remake/remaster-data-extracted`, miroir vers `data-extracted/`, export complet) : même sur la
réponse (a), **aucun travail ne commence avant une révision de ce plan qui la définit** — périmètre dans
l'extracteur, prérequis, régime de preuve et diff prédit, preuve au pixel du portrait, retour arrière,
arrêts — **et sa relecture fraîche**. Sur la réponse (b), elle est retirée.

### ~~D5.p — Le portrait d'ouverture~~ — retirée, portrait reporté (D-E13D-12 amendée)

**Prérequis** : D3.c et D5. Le quad du portrait qui grandit puis se résorbe à l'ouverture (§1.1,
`MainInventoryManager.cs:209-440`, 48×56), dans l'écran de D5. **Sur la réponse (b) au §6 point 6, elle
est retirée et D-E13D-12 amendée** : l'inventaire est livré sans portrait.

### ⏳ D6 — Recette en jeu (l'auteur)

**Prérequis** : D5.
Ouvrir par `Start`, `L2` ou `R2` ; naviguer, y compris en maintenant une direction ; équiper une arme et
un objet ; lire le texte déroulant ; fermer ; le héros et le monde sont figés pendant, **y compris ouvert
en pleine chute**, et la chute reprend à la fermeture ; le HUD se cache à l'ouverture et revient à la
fermeture, comme dans l'original ; **pendant un dialogue aussi, les PNJ et le héros sont figés**
(D-E13D-15). Et, laissé par la vérification de D5 : après avoir lu les deux lignes de description d'un
objet, passer sur une case vide ou un objet non possédé, puis revenir : aucune ligne ne doit rester.

---

## 4. Acceptation d'ensemble

E13.d (principal) est close quand, en jeu : l'inventaire s'ouvre et se ferme comme l'original, avec ses
glissements, ses sons et le HUD ; on navigue sur les 24 cases, avec la répétition en maintenant une
direction ; on équipe une arme et un objet, et la jauge reflète l'arme ; le monde et le héros sont figés
tant qu'il est ouvert, y compris après une ouverture en pleine chute ; les boîtes ont l'aspect de
l'original, chacune une image cuite (D-E13D-13) ; suites vertes ; chaque export prouvé par double export.

## 5. Arrêts

- Une mesure de D0 qui contredit le §1 ou le §6 : plan révisé et resoumis avant D1-D3.
- Un manque de MGUI ou du moteur :
  consigné dans `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`, et la tranche s'arrête.
- Un diff d'export hors du prédit, un double export hors de `{report.json}`.
- Un graphisme de l'inventaire sans actif exporté — une case de boîte dont le tuple n'a pas de
  `wind_NNN`, le curseur, un cadre, un chiffre ou le portrait : l'hypothèse « pixels déjà exportés »
  tombe, et il faut une extraction.

## 6. Points ouverts

1. *Tranché par l'auteur le 2026-09-21 : la façon (a), cuite par le convertisseur, D-E13D-13.* Le fond de chaque boîte
   de l'inventaire (pierre pour les armes et les objets, parchemin pour les noms et la description) n'est
   pas une image : l'original le pose case par case, 822 tuiles de 8×8 prises dans l'atlas `wind` (la
   maquette superposait par erreur les deux copies A et B, 1644 tuiles, voir D-E13D-14). Une maquette, composée le 2026-09-21 avec les tuiles exportées et les positions
   décompilées (aucune case sans tuile, aucune position à palette ambiguë), montre trois façons de faire :
   (a) **l'original** : soit la DLL pose les tuiles une à une d'après un patron exporté, soit le
   convertisseur cuit une image par boîte et l'écran en affiche une seule — même rendu, pierre et
   parchemin variés ; (b) **un cadre à neuf tranches** (quatre coins, quatre bords, une tuile de fond
   répétée) : la pierre devient un motif répétitif et le parchemin des rayures, parce que ces fonds ne sont
   pas réguliers (59 et 81 tuples distincts, mesure antérieure) ; (c) **des panneaux MGUI simples** (fond
   uni et bordure) : rien à exporter, mais l'aspect d'Alundra est perdu.
2. *Le centrage vaut aussi pour le HUD : tranché par l'auteur le 2026-09-21 (« oui ») et appliqué à S3
   par `cdb7097` ; voir `docs/plan-e13c-icones-hud.md`, amendement de S3.*
3. *Touches et portrait : tranchés, D-E13D-11 et D-E13D-12.*
4. **Défaut latent du dialogue, consigné et non corrigé** : `AlundraDialogueDirector` tourne au tick
   mais lit le front d'appui de l'instantané par image rendue (`AlundraDialogueDirector.cs:239`,
   `AlundraWorldProxy.cs:1940-1943`) : sur une image à deux ticks il le voit deux fois, sur une image à
   zéro tick jamais. D0.10 le mesure ; le corriger n'est pas l'objet de ce plan.
5. **Deux remarques P4 d'E13.c S3, reportées** : `InitializeNewGameInventory` ne remet pas `ItemId` à 0
   (sans effet dans une vraie session) ; la citation de la remise à zéro des compteurs dit `:473-479` pour
   `:469-479`.
6. *Tranché par l'auteur le 2026-09-21 : reporter le portrait (b), D-E13D-12 amendée.* **Le portrait d'ouverture n'est pas exporté.** Il faut une extraction :
   l'extracteur doit émettre l'image de portrait de l'enregistrement 0 (48×56, page 2, palette 16) dans la
   planche, puis ré-extraction dans `Alundra Remake/remaster-data-extracted`, miroir vers
   `data-extracted/` et export complet — ce qui écrit hors du dépôt et, par le précédent D-E13C-5, se fait
   avec l'accord de l'auteur. (a) L'autoriser : D3.c sera d'abord définie par une révision relue de ce
   plan, puis D5.p ; (b) reporter le portrait et livrer l'inventaire sans lui, D-E13D-12 amendée. D5 n'en
   dépend pas.
7. *Tranché par l'auteur le 2026-09-21 : tout `MenuOpen` (a), D-E13D-15.* **La portée du gel de D2.** Fidèle à l'original, le gel vaut pour tout
   `MenuOpen`, donc aussi pendant les dialogues : les PNJ cesseraient de s'animer pendant qu'on leur parle,
   ce qui change un comportement déjà vu en jeu. (a) Pour tout `GameplayBlockedMask` (fidèle,
   recommandé) ; (b) seulement pendant l'inventaire.
8. **`RepeatInterval` = 0** : porté avec sa valeur, la navigation répète à chaque tick après 20 ticks, soit
   50 cases par seconde en maintenant une direction. Si la recette le trouve trop rapide, chercher dans
   Ghidra un écrivain que la décompilation n'aurait pas transcrit, avant de toucher à la valeur.
9. **Délai de warp non porté** (D4) : `AlundraWarpDirector.WarpDelayFramesForTests` est posé à 10 à chaque
   entrée de carte mais jamais décrémenté ; l'original le décrémente à chaque image (`GameEngine.cs:1561-1564`)
   et refuse l'inventaire tant qu'il ne vaut pas 0. Le portage ouvre l'inventaire dès la première image
   d'une carte. À corriger avec le directeur des warps (T4), hors de ce plan.

## 7. Journal

| Date | Évènement |
|---|---|
| 2026-09-21 | Reconnaissance à cinq surfaces (inventaire principal, sous-inventaire et bascule, déclencheur et gel, côté portage, ressources exportées) et une critique de complétude. Faits repris en session principale : la grille de 24 cases et sa table ; les touches déjà prises ; **les boîtes ne sont pas des cadres réguliers** (59 et 81 tuples distincts). |
| 2026-09-21 | Première relecture adverse : **REVISE**, un P1 et trois P2, tous acceptés. (1) **P1, une erreur de la première rédaction** : elle affirmait, marqué « mesuré », que lever `MenuOpen` ne figeait pas le héros. C'est faux : la porte d'`UpdateEntities` est portée depuis T2, et `MovePlayer` n'est appelé que hors d'elle. La mesure avait lu l'appel sans remonter à la fonction qui le contient, et un commentaire périmé l'y avait poussée. La question du gel posée à l'auteur est retirée ; D2 devient conditionnelle, sur la mesure de ce que la porte ne couvre pas (D0.3). (2) La navigation lit la répétition des touches, que le portage ne calcule pas : ajoutée à D1 (D-E13D-8). (3) D0 reçoit un livrable et un critère de clôture par mesure, la règle d'égalité du rapprochement, et son propre arrêt. (4) Chaque tranche reçoit ses prérequis. |
| 2026-09-21 | Relecture de clôture : **REVISE**, deux P2, tous deux acceptés en **FIX**. Les quatre blocages précédents y sont confirmés résolus. (1) **L'horloge de la manette** : l'original compte à 50 Hz, le portage échantillonne par image rendue et fait tourner sa logique au tick ; une répétition portée telle quelle dépendrait de l'affichage. Décision D-E13D-9, mesure D0.10, tests de D1 à cadence variable, et un défaut latent du dialogue consigné sans être corrigé. (2) **Les graphismes hors des boîtes** (curseur, cadres, chiffres, portrait) n'avaient pas de source établie : mesure D0.9, prérequis de D5, arrêt élargi à tout graphisme. **Deuxième REVISE : plafond atteint.** Disposition en session principale, une seule relecture de clôture ensuite, sans nouvelle boucle. |
| 2026-09-21 | Relecture de clôture, la seule autorisée après le plafond : **READY**. Le plan part à l'auteur avec trois questions (§6, points 1 à 3). |
| 2026-09-21 | **Décisions de l'auteur.** Icônes centrées dans leur boîte, et pas de fidélité au pixel près pour le dessin (D-E13D-10, qui amende D-E13D-1 pour le dessin seulement) ; touches `Y`/`U`/`I`/`O`/`P` pour `L2`/`L1`/`R1`/`R2`/`Select` (D-E13D-11) ; portrait dès la première passe (D-E13D-12). Deux questions restent ouvertes, que l'auteur n'a pas tranchées : la façon de dessiner les boîtes, et le centrage dans le HUD de S3. |
| 2026-09-21 | **L'auteur tranche le §6 point 2 : le centrage vaut aussi pour les deux cases du HUD**, appliqué à S3 par `cdb7097` sur `chantier/e13c-suite`, puis cette branche réempilée dessus. Le point 1 reste ouvert : l'auteur n'a pas compris la question, reformulée avec une maquette des trois façons de dessiner les boîtes. |
| 2026-09-21 | **L'auteur tranche le §6 point 1 : « 1 avec image cuite » (D-E13D-13).** Reconnaissance en lecture seule à trois surfaces (analyseur, convertisseur, DLL et moteur), puis faits porteurs de décision revérifiés en session principale. Un relevé disait `clut` 3 partout : faux, la mesure trouve 0, 3, 5 et 6 ; un autre ne voyait pas de mode de mélange dans le code : les appels de l'original sont en commentaire, `SetSemiTrans(sprite, 0)`. D0.1 close par mesure (1644 cases sur 1644 retrouvées), D0.2 par lecture ; D3 réécrite en D3.a et D3.b ; D5 ajustée. Relecture à venir. |
| 2026-09-21 | Relecture de la révision : **REVISE**, un blocage, accepté en **FIX**. La révision affirmait, marqué [lu], que la couche B se pose sur la couche A. Faux : `FUN_800548a4` n'appelle dans l'original que des macros d'initialisation ; A et B sont les deux copies d'un double tampon, et l'original n'en montre qu'une par image (`DisplayUiBoxes`, `0x80055d78`). Mesure ajoutée (script et sortie ci-dessous) : A et B égales pour cinq boîtes, le B du nom d'arme est un clone de la décompilation, et **la boîte des armes diffère sur 26 cases** ; dessinées séparément, A donne un cadre complet et B non, et la maquette montrée à l'auteur superposait les deux, avec ce défaut. D-E13D-14 : cuire A seule ; D3.a exporte 822 cases, D3.b recopie sans mélange, compteurs et tests ajustés. |
| 2026-09-21 | Relecture neuve après la correction : **READY**. La révision part à l'auteur : D-E13D-13 enregistrée, D-E13D-14 proposée, D0.1 et D0.2 closes, D3.a et D3.b prêtes à l'approbation avec D0. |
| 2026-09-21 | **L'auteur confirme D-E13D-14 : « cuire la copie A seule ».** La décision passe des proposées (§2.3) aux tranchées (§2.2). Le plan attend toujours son approbation et le feu vert pour D0. |
| 2026-09-21 | **Approuvé par l'auteur** (« tout est ok. fait toutes les taches »), mode **AUTO** choisi ; S4 d'E13.c validée par lui du même mot. E13.c close et mergée dans `main` à sa demande (analyseur `c204009`, parent `ea633ad`), sans push ; cette branche rebasée sur `main`. D0 commence. |
| 2026-09-21 | **D0 exécutée.** Huit mesures en lecture seule, par surface (original : manette, HUD, fondu ; original : contenu de l'inventaire ; portage : horloges et gel ; portage : HUD, textes, sons ; données exportées), chacune relue par un contradicteur ; puis faits porteurs de décision revérifiés en session principale, qui a corrigé trois relevés : le curseur fait 16×16 et ses quatre phases sont toutes exportées (le relevé comptait au pas de 8) ; le chiffre 9 est `wind_065` ; le sens du glissement de la jauge. Le portrait mesuré dans le binaire par un programme jetable (ci-dessous). **Arrêt propre à D0 déclenché** : §1.2 (le gel ne couvre ni la gravité ni les animations) et §1.5 (la jauge se cache à l'ouverture) contredits ; plan révisé, D2 concrétisée, D1 et D4 précisées, trois points ouverts (§6, 6 à 8). Relecture à venir, puis resoumission à l'auteur. |
| 2026-09-21 | Relecture de la révision : **REVISE**, un blocage, accepté en **FIX**. D2 posait `ControlMode = Disabled` en affirmant que la vitesse restait intacte ; faux : le mode ne se change que par `SetControlMode`, qui appelle `Stop()` et efface vitesse, saut et minuteries. D2 saisit maintenant l'état par `CaptureStateSnapshot` et le rend par `RestoreStateSnapshot`, avec la liste de ce que la restauration remet à zéro et pourquoi, et un arrêt élargi. |
| 2026-09-21 | Relecture neuve après cette correction : **REVISE**, deux blocages, tous deux acceptés en **FIX**. (1) **Le saut n'est pas porté** (`AlundraPlayerManager.cs:66-73`, `:286-289`, aucun `RequestJump` dans la DLL) : le cas « ouvert en plein saut » ne peut pas se produire dans le portage ; §1.1, §1.2, D2, §4 et D6 parlent maintenant de la chute, état `Falling`, avec un test du contrôleur seul pour l'état `Jumping`. (2) **D3.c n'était définie nulle part** alors qu'elle écrirait hors du dépôt : elle devient une tranche conditionnelle, qui ne commence qu'après une révision relue du plan ; le portrait sort de D5 en D5.p, et D5 se clôt sans lui. **Deuxième REVISE de cette révision : plafond atteint.** Disposition en session principale, puis une seule relecture de clôture. |
| 2026-09-21 | **Relecture de clôture : REVISE**, un blocage, accepté en **FIX** : la restauration remet à zéro le déplacement vertical externe, que la DLL ne redéclare qu'une fois par tick ; sur une image de dégel sans tick, une escalade près du sol se serait fait ramener au sol. D2 le redéclare juste après `RestoreStateSnapshot`, avec la valeur de son propriétaire, et gagne un test de dégel à zéro tick. **La relecture de clôture étant la dernière autorisée, cette correction n'est pas relue** : le plan part à l'auteur sans READY, avec ce statut écrit. |
| 2026-09-21 | **L'auteur réapprouve le plan révisé**, correction non relue comprise, et tranche : **portrait reporté** (D-E13D-12 amendée, D3.c et D5.p retirées) ; **gel sur tout `MenuOpen`** (D-E13D-15). Exécution reprise en mode AUTO : D1, D2, D3.a, D3.b, D4, D5. |
| 2026-09-21 | **D1 faite**, vérificateur **CONFIRMED**. Export prouvé (diff mesuré = prédit, double export = `report.json`). |
| 2026-09-21 | **D3.a faite** (analyseur `64978f8`, branche `chantier/e13d-boites`) : 7 boîtes, 822 cases, acceptation passée par un script indépendant. Générateur et vérificateur ci-dessous. |
| 2026-09-21 | **D3.b faite**, vérificateur **CONFIRMED** : six images cuites, prouvées par manifeste, double export et au pixel. |
| 2026-09-21 | **D2 faite**, vérificateur **CONFIRMED** : contrôleurs et animations figés pendant tout `MenuOpen`, dégel exact ; passe placée en fin de mise à jour du monde, pour l'ordre du moteur. |
| 2026-09-21 | **D4 faite** : une première vérification REFUTED (valeurs `null` des textes, P1 reproduit), corrigée, reproduction rejouée, vérification neuve **CONFIRMED**. |
| 2026-09-21 | Reconnaissance de D5 à quatre surfaces (livraison d'un écran XAML par la DLL, police, tests sans affichage, écran du HUD) : MGUI sait dessiner une police BMFont (`AddStaticFont`, `StaticSpriteFont.FromBMFont`) et agrandir au rendu (`RenderTransform.Scale`, ADR-0006 du moteur) ; la DLL n'enregistre pas encore font3 ; `Alundra.Tests` ne peut pas construire de `MGDesktop`. **L'auteur tranche** : XAML dans la DLL (D-E13D-16), harnais minimal dans les tests (D-E13D-17), ADR-0001. |
| 2026-09-21 | **D5 faite** : capture en processus conforme à sa prédiction ; une première vérification REFUTED (seconde ligne de description affichée sans condition, P2 reproduit), corrigée, reproduction et capture rejouées, vérification neuve **CONFIRMED**. Reste D6, la recette de l'auteur. |

### D0.1 — le script de mesure et sa sortie (2026-09-21)

Refaisable tel quel avec Python 3 et Pillow :

```python
"""D0.1 of docs/plan-e13d-inventaire.md: every cell of the inventory boxes against the exported wind atlas.

Rule, written before the run: a cell (u0, v0, w, h, clut) of a SpritesA/SpritesB array is FOUND when
alundra-project/UI/wind-sprites.json has an entry with (u0, v0, width, height, palette_index) equal to it.
Also measured: every cell lies inside its box's rectangle, the cells tile the rectangle in the drawing
loop's own order (index = row * Width + col, GraphicManager.cs:1855-1856), and the alpha values of wind.png.
"""
import io, json, os, re
from collections import Counter
from PIL import Image

ROOT = r'D:\development\repo\alundra-casaengine-project-converter'
src = io.open(os.path.join(ROOT, r'alundra-datas-analyser\AlundraTools\AlundraEngine\StaticVariables.cs'),
              encoding='utf-8-sig').read()
wind = json.load(io.open(os.path.join(ROOT, r'alundra-project\UI\wind-sprites.json'), encoding='utf-8-sig'))
atlas = Image.open(os.path.join(ROOT, r'alundra-project\UI\Textures\wind.png')).convert('RGBA')

BOXES = [  # StaticVariables.cs:11196-11267 - name, X, Y, Width, Height (cells), SpritesA, SpritesB
    ('weapons', 0x08, 0x10, 0x15, 0x06, 'g_MainInventoryWeaponBackgroundSpritesA', 'g_MainInventoryWeaponBackgroundSpritesB'),
    ('items', 0x08, 0x40, 0x15, 0x0D, 'g_MainInventoryItemBackgroundSpritesA', 'g_MainInventoryItemBackgroundSpritesB'),
    ('weapon-name', 0xB0, 0x10, 0x12, 0x04, 'SPRT_ARRAY_800b8370', 'SPRT_ARRAY_800b8370'),  # :11206, B = A.Clone()
    ('item-name', 0xB0, 0x40, 0x12, 0x04, 'SPRT_ARRAY_800b8ec0', 'SPRT_ARRAY_800b9460'),
    ('money-falcon-key', 0xB0, 0x60, 0x03, 0x09, 'g_moneyFalconKeyIconSpritesA', 'g_moneyFalconKeyIconSpritesB'),
    ('description', 0x10, 0xA8, 0x24, 0x07, 'g_dialogMessageBackgroundSpritesA', 'g_dialogMessageBackgroundSpritesB'),
]  # the seventh box, UIBoxConfiguration_800b9a10, is 0x0 cells: nothing to draw.

SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def cells(array_name):
    start = src.index('[', src.index(array_name + ' ='))
    depth, end = 0, start
    for end in range(start, len(src)):
        depth += {'[': 1, ']': -1}.get(src[end], 0)
        if depth == 0:
            break
    return [{k: int(v, 0) for k, v in FIELD.findall(m.group(1))} for m in SPRT.finditer(src[start:end])]


known = {(e['u0'], e['v0'], e['width'], e['height'], e['palette_index']) for e in wind}
total = found = outside = misplaced = 0
for name, bx, by, bw, bh, arr_a, arr_b in BOXES:
    per_layer, cluts = [], Counter()
    for layer in (cells(arr_a), cells(arr_b)):
        per_layer.append(len(layer))
        for i, c in enumerate(layer):
            total += 1
            found += (c['u0'], c['v0'], c['w'], c['h'], c['clut']) in known
            cluts[c['clut']] += 1
            outside += not (bx <= c['x0'] and c['x0'] + c['w'] <= bx + 8 * bw and by <= c['y0'] and c['y0'] + c['h'] <= by + 8 * bh)
            misplaced += (c['x0'], c['y0'], c['w'], c['h']) != (bx + 8 * (i % bw), by + 8 * (i // bw), 8, 8)
    print(f'{name}: {bw}x{bh} cells, A={per_layer[0]} B={per_layer[1]}, clut {dict(cluts)}')
print(f'cells: {total}; found in wind-sprites.json: {found}; outside their box: {outside}; '
      f'not at X + 8*col, Y + 8*row: {misplaced}')
print(f'wind.png alpha values: {sorted(Counter(p[3] for p in atlas.getdata()).items())}')
```

Sortie :

```text
weapons: 21x6 cells, A=126 B=126, clut {3: 252}
items: 21x13 cells, A=273 B=273, clut {3: 546}
weapon-name: 18x4 cells, A=72 B=72, clut {0: 144}
item-name: 18x4 cells, A=72 B=72, clut {0: 144}
money-falcon-key: 3x9 cells, A=27 B=27, clut {6: 36, 5: 18}
description: 36x7 cells, A=252 B=252, clut {0: 504}
cells: 1644; found in wind-sprites.json: 1644; outside their box: 0; not at X + 8*col, Y + 8*row: 0
wind.png alpha values: [(0, 48907), (255, 16629)]
```

### D0.2 — la comparaison des copies A et B et sa sortie (2026-09-21)

Refaisable tel quel avec Python 3 :

```python
"""D0.2 of docs/plan-e13d-inventaire.md: are SpritesA and SpritesB of each inventory box the same cells?

Rule, written before the run: for each box, cell i of SpritesA and cell i of SpritesB are EQUAL when their
full tuples (x0, y0, u0, v0, w, h, clut) are equal. Every differing cell is listed with both tuples.
The weapon name's SpritesB, SPRT_ARRAY_800b8910, has no data of its own in the decompilation: it is a
Clone() of SpritesA at initialisation (StaticVariables.cs:11206), so its equality is a transcription fact,
not a fact of the original; it is reported apart.
"""
import io, os, re

ROOT = r'D:\development\repo\alundra-casaengine-project-converter'
src = io.open(os.path.join(ROOT, r'alundra-datas-analyser\AlundraTools\AlundraEngine\StaticVariables.cs'),
              encoding='utf-8-sig').read()

BOXES = [  # StaticVariables.cs:11196-11267 - name, SpritesA, SpritesB
    ('weapons', 'g_MainInventoryWeaponBackgroundSpritesA', 'g_MainInventoryWeaponBackgroundSpritesB'),
    ('items', 'g_MainInventoryItemBackgroundSpritesA', 'g_MainInventoryItemBackgroundSpritesB'),
    ('weapon-name', 'SPRT_ARRAY_800b8370', None),  # B = A.Clone(), :11206
    ('item-name', 'SPRT_ARRAY_800b8ec0', 'SPRT_ARRAY_800b9460'),
    ('money-falcon-key', 'g_moneyFalconKeyIconSpritesA', 'g_moneyFalconKeyIconSpritesB'),
    ('description', 'g_dialogMessageBackgroundSpritesA', 'g_dialogMessageBackgroundSpritesB'),
]
KEYS = ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')
SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def cells(array_name):
    start = src.index('[', src.index(array_name + ' ='))
    depth, end = 0, start
    for end in range(start, len(src)):
        depth += {'[': 1, ']': -1}.get(src[end], 0)
        if depth == 0:
            break
    out = []
    for m in SPRT.finditer(src[start:end]):
        v = dict(FIELD.findall(m.group(1)))
        out.append(tuple(int(v[k], 0) for k in KEYS))
    return out


for name, arr_a, arr_b in BOXES:
    a = cells(arr_a)
    if arr_b is None:
        print(f'{name}: {len(a)} cells; SpritesB is a Clone() of SpritesA in the decompilation (no original data)')
        continue
    b = cells(arr_b)
    diff = [(i, a[i], b[i]) for i in range(min(len(a), len(b))) if a[i] != b[i]]
    print(f'{name}: A={len(a)} B={len(b)} cells; equal {min(len(a), len(b)) - len(diff)}; different {len(diff)}')
    for i, ta, tb in diff[:12]:
        print(f'    cell {i}: A {dict(zip(KEYS, ta))}  B {dict(zip(KEYS, tb))}')
    if len(diff) > 12:
        print(f'    ... {len(diff) - 12} more')
```

Sortie :

```text
weapons: A=126 B=126 cells; equal 100; different 26
    cell 19: A {'x0': 160, 'y0': 16, 'u0': 160, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 160, 'y0': 16, 'u0': 200, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}
    cell 20: A {'x0': 168, 'y0': 16, 'u0': 168, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 16, 'u0': 208, 'v0': 72, 'w': 8, 'h': 8, 'clut': 3}
    cell 41: A {'x0': 168, 'y0': 24, 'u0': 168, 'v0': 80, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 24, 'u0': 208, 'v0': 80, 'w': 8, 'h': 8, 'clut': 3}
    cell 42: A {'x0': 8, 'y0': 32, 'u0': 176, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 8, 'y0': 32, 'u0': 176, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}
    cell 62: A {'x0': 168, 'y0': 32, 'u0': 248, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 32, 'u0': 208, 'v0': 88, 'w': 8, 'h': 8, 'clut': 3}
    cell 83: A {'x0': 168, 'y0': 40, 'u0': 248, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 40, 'u0': 208, 'v0': 96, 'w': 8, 'h': 8, 'clut': 3}
    cell 84: A {'x0': 8, 'y0': 48, 'u0': 176, 'v0': 120, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 8, 'y0': 48, 'u0': 176, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}
    cell 104: A {'x0': 168, 'y0': 48, 'u0': 248, 'v0': 120, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 168, 'y0': 48, 'u0': 208, 'v0': 104, 'w': 8, 'h': 8, 'clut': 3}
    cell 106: A {'x0': 16, 'y0': 56, 'u0': 216, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 16, 'y0': 56, 'u0': 184, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 107: A {'x0': 24, 'y0': 56, 'u0': 200, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 24, 'y0': 56, 'u0': 192, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 108: A {'x0': 32, 'y0': 56, 'u0': 208, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 32, 'y0': 56, 'u0': 200, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    cell 109: A {'x0': 40, 'y0': 56, 'u0': 216, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}  B {'x0': 40, 'y0': 56, 'u0': 208, 'v0': 128, 'w': 8, 'h': 8, 'clut': 3}
    ... 14 more
items: A=273 B=273 cells; equal 273; different 0
weapon-name: 72 cells; SpritesB is a Clone() of SpritesA in the decompilation (no original data)
item-name: A=72 B=72 cells; equal 72; different 0
money-falcon-key: A=27 B=27 cells; equal 27; different 0
description: A=252 B=252 cells; equal 252; different 0
```

### D0.9 — le portrait d'Alundra mesuré dans le binaire (2026-09-21)

Programme jetable, hors dépôt, qui référence `AlundraEngine.csproj` et ouvre les `.BIN` du jeu comme `CreateGameEngine` de l'extracteur ; lancé depuis `AlundraTools/AlundraTools` avec le chemin `D:\development\repo\Alundra Remake\Alundra (France)\Alundra (France)_extracted`. Il n'écrit rien. L'enregistrement 31 sert de témoin : c'est l'épée de S3, même signature.

```csharp
// Read-only: opens the game's .BIN files like AlundraDataExtractor's CreateGameEngine and prints the
// image GraphicManager.GetAnimationImageByIndex(0) returns (GraphicManager.cs:1786-1793), plus
// records 1 and 31 as controls. Writes nothing.
using AlundraEngine;
using AlundraEngine.Balance;
using AlundraEngine.DatasBin;
using AlundraEngine.Editor;
using AlundraEngine.Etc;
using AlundraEngine.Sound;
using AlundraEngine.Text;

var gamePath = args[0];
var dataFolder = Path.Combine(gamePath, "DATA");
var datasBin = new DatasBin(Path.Combine(dataFolder, "DATAS.BIN"));
var balanceBin = new BalanceBin(Path.Combine(dataFolder, "BALANCE.BIN"));
var soundBin = new SoundBin(Path.Combine(dataFolder, "SOUND.BIN"));
var font3 = new Font3(Path.Combine(dataFolder, "..", "TAKI", "SCREEN"));
var etcResFileName = PathHelper.GetEtcFileName(dataFolder);
EtcRes etcRes = Path.GetFileName(etcResFileName).Contains("usa", StringComparison.InvariantCultureIgnoreCase)
    ? new EtcResUsa(etcResFileName)
    : new EtcResR(etcResFileName);
var engine = new GameEngine(datasBin, balanceBin, soundBin, etcRes, font3, null);
engine.InitializeEngine();

var records = engine.AlundraMap.SpriteInfo.SpriteRecords;
using var br = engine.DatasBin.OpenBin();
foreach (var index in new[] { 0, 1, 31 })
{
    var record = records[index];
    if (record == null) { Console.WriteLine($"record {index}: null"); continue; }
    var set = record.GetPortraitImageset(br);
    Console.WriteLine($"record {index}: Sector5Id={record.Header.Sector5Id} portrait images={set.Images.Length}");
    foreach (var image in set.Images)
    {
        Console.WriteLine($"  signature={image.Signature} spritesheet={image.Spritesheet} page={image.Spritesheet & 7} palette={image.Palette} " +
                          $"Sx={image.Sx} Sy={image.Sy} SourceX={image.SourceX} SourceY={image.SourceY} Swidth={image.Swidth} Sheight={image.Sheight} " +
                          $"mirX={image.IsMirroredX} mirY={image.IsMirroredY}");
    }
}
```

Sortie :

```text
record 0: Sector5Id=0 portrait images=1
  signature=61779762221058 spritesheet=2 page=2 palette=16 Sx=200 Sy=56 SourceX=200 SourceY=56 Swidth=48 Sheight=56 mirX=False mirY=False
record 1: Sector5Id=1 portrait images=1
  signature=25391459210757 spritesheet=5 page=5 palette=22 Sx=232 Sy=232 SourceX=232 SourceY=232 Swidth=23 Sheight=23 mirX=False mirY=False
record 31: Sector5Id=31 portrait images=1
  signature=34187962752519 spritesheet=7 page=7 palette=30 Sx=96 Sy=1 SourceX=96 SourceY=1 Swidth=24 Sheight=31 mirX=False mirY=False
```

Puis : aucun `sprite_61779762221058.sprite` sous `alundra-project/`, aucune entrée du catalogue, et 0 pixel opaque sur 2688 dans `map_alundra_spritesheet.png` au rectangle `(200, 568, 48, 56)` (page 2 × 256 + 56), où la disposition `Original` le placerait.

### D3.a — le générateur des CSV et son vérificateur indépendant (2026-09-21)

Générateur :

```python
"""D3.a of docs/plan-e13d-inventaire.md: writes UiBoxes.csv and UiBoxCells.csv from the decompilation.

The seven boxes are the seven DisplayUiBoxes calls of the main inventory's per-frame function, in their
drawing order (MainInventoryManager.cs:914-920). For each, its UIBoxConfiguration literal
(StaticVariables.cs:11196-11267) gives X, Y, Width, Height and SpritesA; the cells are the SPRT literals
of SpritesA, raw, in array order (D-E13D-14: copy A only). Values are written as decimal integers, the
convention of the analyser's other CSVs; ';' separator, header row, CRLF line endings, no BOM.
"""
import io
import os
import re

ROOT = r'D:\development\repo\alundra-casaengine-project-converter\alundra-datas-analyser\AlundraTools'
SRC = os.path.join(ROOT, r'AlundraEngine\StaticVariables.cs')
INVENTORY = os.path.join(ROOT, r'AlundraEngine\UI\MainInventoryManager.cs')
OUT = os.path.join(ROOT, 'AlundraTools')

src = io.open(SRC, encoding='utf-8-sig').read()
inventory = io.open(INVENTORY, encoding='utf-8-sig').read()

# The drawing order: FUN_80056598's seven DisplayUiBoxes calls (MainInventoryManager.cs:914-920).
calls = re.findall(r'DisplayUiBoxes\(_gameEngine\.StaticVariables\.(\w+)\)', inventory)
assert len(calls) == 7, calls

CONFIG_FIELD = re.compile(r'\b(X|Y|Width|Height|SpritesA)\s*=\s*([^,\n}]+)')
SPRT = re.compile(r'new\s+SPRT\s*\{([^}]*)\}', re.S)
SPRT_FIELD = re.compile(r'\b(x0|y0|u0|v0|w|h|clut)\s*=\s*(?:unchecked\s*\()?\s*(?:\(\w+\))?\s*(0x[0-9A-Fa-f]+|\d+)')


def box_config(name):
    start = src.index(name + ' = new UIBoxConfiguration')
    body = src[src.index('{', start):src.index('};', start)]
    fields = {k: v.strip() for k, v in CONFIG_FIELD.findall(body)}
    return {k: (int(fields[k], 0) if k != 'SpritesA' else fields[k]) for k in ('X', 'Y', 'Width', 'Height', 'SpritesA')}


def sprt_cells(array_name):
    start = src.index('[', src.index(array_name + ' ='))
    depth, end = 0, start
    for end in range(start, len(src)):
        depth += {'[': 1, ']': -1}.get(src[end], 0)
        if depth == 0:
            break
    cells = []
    for m in SPRT.finditer(src[start:end]):
        fields = dict(SPRT_FIELD.findall(m.group(1)))
        cells.append([int(fields[k], 0) for k in ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')])
    return cells


box_rows = ['box;x;y;width;height']
cell_rows = ['box;cell;x0;y0;u0;v0;w;h;clut']
for name in calls:
    config = box_config(name)
    box_rows.append(f"{name};{config['X']};{config['Y']};{config['Width']};{config['Height']}")
    if config['SpritesA'] == 'null':
        assert config['Width'] * config['Height'] == 0, name
        continue
    cells = sprt_cells(config['SpritesA'])
    assert len(cells) == config['Width'] * config['Height'], (name, len(cells))
    for index, cell in enumerate(cells):
        cell_rows.append(';'.join([name, str(index)] + [str(v) for v in cell]))


def write(file_name, rows):
    path = os.path.join(OUT, file_name)
    with io.open(path, 'wb') as handle:
        handle.write(('\r\n'.join(rows) + '\r\n').encode('utf-8'))
    print(f'{file_name}: {len(rows) - 1} rows')


write('UiBoxes.csv', box_rows)
write('UiBoxCells.csv', cell_rows)
```

Vérificateur, sans rien reprendre du générateur ; sortie : `boxes: 7; cells in csv: 822; cells re-read: 822; missing from csv: 0; extra in csv: 0; same order: True`.

```python
"""D3.a acceptance, independent of d3a_generate.py: re-reads StaticVariables.cs line by line (no bracket
matching, no shared regex) and checks that every row of UiBoxes.csv and UiBoxCells.csv is found there,
and that nothing is missing or extra."""
import io
import os

ROOT = r'D:\development\repo\alundra-casaengine-project-converter\alundra-datas-analyser\AlundraTools'
lines = io.open(os.path.join(ROOT, r'AlundraEngine\StaticVariables.cs'), encoding='utf-8-sig').read().splitlines()


def number(text):
    text = text.strip().rstrip(',').strip()
    for prefix in ('unchecked(', '(short)', '(byte)', '(ushort)', '(uint)'):
        text = text.replace(prefix, '')
    text = text.rstrip(')').strip()
    return int(text, 16) if text.lower().startswith('0x') else int(text)


def fields_of(chunk):
    """'a = 1, b = 0x2, ...' -> dict, splitting on commas at top level of the chunk."""
    out = {}
    for part in chunk.replace('{', ',').replace('}', ',').split(','):
        if '=' in part:
            key, value = part.split('=', 1)
            key = key.strip().split()[-1] if key.strip() else ''
            try:
                out[key] = number(value)
            except ValueError:
                out[key] = value.strip()
    return out


def config(name):
    i = next(k for k, l in enumerate(lines) if l.strip().startswith(name + ' = new UIBoxConfiguration'))
    block = []
    for l in lines[i + 1:]:
        block.append(l)
        if l.strip().startswith('};'):
            break
    return fields_of(' '.join(block))


def array_cells(name):
    i = next(k for k, l in enumerate(lines) if (name + ' =') in l and 'SPRT[]' in l)
    cells, current = [], None
    for l in lines[i + 1:]:
        s = l.strip()
        if s.startswith('new SPRT'):
            current = ''
            continue
        if current is not None:
            current += ' ' + s
            if s.startswith('}'):
                f = fields_of(current)
                cells.append(tuple(f[k] for k in ('x0', 'y0', 'u0', 'v0', 'w', 'h', 'clut')))
                current = None
            continue
        if s.startswith('];'):
            break
    return cells


def rows(file_name):
    data = io.open(os.path.join(ROOT, 'AlundraTools', file_name), 'rb').read()
    assert not data.startswith(b'\xef\xbb\xbf') and data.endswith(b'\r\n') and b'\n' not in data.replace(b'\r\n', b'')
    text = data.decode('utf-8').split('\r\n')[:-1]
    return text[0], [r.split(';') for r in text[1:]]


header, boxes = rows('UiBoxes.csv')
assert header == 'box;x;y;width;height', header
cell_header, cells = rows('UiBoxCells.csv')
assert cell_header == 'box;cell;x0;y0;u0;v0;w;h;clut', cell_header

expected_cells = []
for name, x, y, w, h in boxes:
    c = config(name)
    assert (c['X'], c['Y'], c['Width'], c['Height']) == (int(x), int(y), int(w), int(h)), (name, c)
    if c['SpritesA'] == 'null':
        continue
    for index, cell in enumerate(array_cells(c['SpritesA'])):
        expected_cells.append([name, str(index)] + [str(v) for v in cell])

missing = [r for r in expected_cells if r not in cells]
extra = [r for r in cells if r not in expected_cells]
print(f'boxes: {len(boxes)}; cells in csv: {len(cells)}; cells re-read: {len(expected_cells)}; '
      f'missing from csv: {len(missing)}; extra in csv: {len(extra)}; same order: {cells == expected_cells}')
```
