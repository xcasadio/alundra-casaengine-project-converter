# Plan — E10 : fondu et teinte plein écran, en vraie feature moteur

Date : 2026-08-30. Origine : étape choisie par l'utilisateur après la clôture d'E11, avec trois
décisions actées par lui avant rédaction : **périmètre** = fondu d'entrée + teinte + opcodes
`0xAF`/`0xB0`/`0xB1` (transitions par capture du framebuffer : chantier séparé) ; **architecture** =
le patron du système audio V1 du moteur (service sans MonoGame + composant + doc + tests) ; **les 36
calques de backdrop mal fusionnés** sont corrigés au passage.

**Tout le §1 est lu dans la décompilation par la reconnaissance, puis les points porteurs revérifiés.**

## 1. La spec de fidélité

### 1.1 Deux machines d'état couleur, en 16.16, avancées par `RenderTransitionEffects`

- **Machine B — le rectangle de fondu** (la seule qui dessine) : `g_currentFadeColor*` avance vers
  `g_targetFadeColor*` par pas `g_fadeColorStep*` (`MoveTowards`, `GraphicManager.cs:1622-1643` :
  addition pure + clamp au dépassement strict, **aucun arrondi**). Tous arrivés →
  `g_fadeStepFlags = 0` (`:1589`).
  **Garde de dessin** (`:1574-1577`) : si `g_fadeStepFlags == 0 && g_fadeFrameCounter == 0`,
  **aucun rectangle n'est soumis**. `g_fadeFrameCounter` est un **verrou de persistance, jamais
  décrémenté nulle part** (posé par l'opérande 6 de `0xAF`) — mais **PAS permanent à vie de session**
  (correction de relecture, vérifiée en session principale) : **chaque chemin d'entrée de carte et de
  départ de warp le remet à 0**, avec les deux drapeaux (`GameEngine.cs:886` et `:1146`,
  `g_fadeFrameCounter = 0; g_fadeStepFlags = 0; g_warpFlags = 0`). Une teinte posée par `0xAF`
  persiste donc **dans sa carte**, et l'entrée de carte suivante l'efface — le port doit reproduire
  cette remise à zéro dans sa méthode d'installation (D-E10-7).
- **Machine A — la machine « warp »** : `g_warpFadeColor*` → `g_fadeColor*_Target`, arrivée →
  `g_warpFlags = 0`. **Ses couleurs sont MORTES dans le port** : leur seule sortie,
  `g_displayEnvColor*`, n'a aucun consommateur (vérifié : 3 écritures, 0 lecture). Seul son **drapeau**
  compte — consommé par `0xB1` et par le keep-alive de warp. On porte la machine pour ses drapeaux et
  sa durée, pas pour ses couleurs.

### 1.2 L'ÉCHANGE DE CANAUX, porteur et piégeux

`GraphicManager.cs:1594-1596` : `tile.r0 = machineB >> 16`, `tile.b0 = machineR >> 16` — **les noms de
variables de la décomp sont inversés par rapport aux canaux dessinés**. `0xAF` écrit opérande 1 →
machine « B », opérande 3 → machine « R » (`EntityEventHandlers.cs:3292-3294`) : **les opérandes de
script sont donc (R, G, B) dans l'ordre d'affichage réel**, et l'échange est invisible sur tous les
fondus gris. Il ne se voit que sur les effets asymétriques (4, 6, 8). **Ne PAS « corriger » les noms :
implémenter opérandes = R,G,B affichés, et documenter l'échange à côté.**

### 1.3 Le dessin, et la formule de fusion PSX

Rectangle plein écran (320×**236** dans l'original — 4 px non fondus en bas, déviation inerte : le
port couvre le viewport entier), profondeur `FadeTransitionEffect = BackgroundUI − 1` (au-dessus du
monde, sous l'UI), fusion par `g_fadeTPagePrim1` (`:1599-1604`) :

| tpage | GPU PSX (par canal, saturé [0,255]) | `BlendState` XNA |
|---|---|---|
| 1 | `dst' = dst + src` | `Add`, `One`/`One` |
| 2 | `dst' = dst − src` | **`ReverseSubtract`**, `One`/`One` (Subtract calculerait src−dst) |
| autre | opaque | existant |

Alpha : `Add, Zero/One` (préserve l'alpha destination). Les TILE plats non texturés utilisent leur
couleur 8 bits **littéralement** (pas d'échelle 0x80). Le shader sprite du moteur sort du
non-prémultiplié (`SpriteBlendMode.cs:13-22`) — à alpha 1, `One/One` est exact. **Contrat du moteur**
(`SpriteRendererComponent.cs:183-188`) : `GetBlendState` rend des instances **en cache, jamais
d'allocation par run**.

### 1.4 `BeginFadeEffect` / `SetFadeDuration` — les pas, et leurs arêtes

`step = (target − current) / duration` par canal, **division entière tronquante** (= MIPS `div`), en
16.16 (`GameEngine.cs:1000/1010/1020`). Conséquences fidèles à porter telles quelles :
- delta divisible → exactement `duration` frames (les fondus gris ×16 : `0xff0000 = 16·0xff000`) ;
  sinon **`duration + 1`** (le dernier pas clampe) ;
- `|delta| < duration` → pas **0** → un fondu montant **ne se termine jamais** (`0xB1` jamais vrai) —
  l'arête existe dans l'original, on ne la « répare » pas ;
- `duration = 0` → l'original trappait ; le port **doit garder** un comportement défini (division
  gardée, documentée comme déviation de nécessité) ;
- machine **inactive** (`flags == 0`) → `Begin`/`SetDuration` **claque current = target** sans animer.

### 1.5 L'effet 0 — l'acceptation, chiffrée à la frame près

`WarpPlayer` effet 0 (`GameEngine.cs:895-905`) : soustractif, 16 frames, canaux `0xff0000 → 0`,
pas `−0xff000` exactement. **La machine avance AVANT de dessiner** (`:1581-1583` précèdent `:1593`) :
le soustracteur dessiné à la frame n vaut `(0xff0000 − n·0xff000) >> 16` —

```
n=1..16 : 239, 223, 207, 191, 175, 159, 143, 127, 111, 95, 79, 63, 47, 31, 15, 0
```

**Jamais de frame à soustraction 255 pleine** (la frame 1 laisse jusqu'à 16 de résidu sur les canaux
très clairs). À n=16 la valeur atteint 0 et `g_fadeStepFlags` se clear ; dès n=17, plus aucun
rectangle. L'UI est dessinée au-dessus, jamais assombrie. `g_warpDelayFrames = 10` est un pur verrou
d'entrée (Start+Select, inventaire — **non portés**) : zéro rôle visuel, **non porté**.

Table complète des effets d'arrivée (pour plus tard, seul l'effet 0 est câblé ici) : 0 = sub 16f ;
2/10 = add 16f (flash blanc) ; 4 = add, BLEU en 8f, R/V en 16f ; 5 = glissement sans fondu ; 6 = add
24f + glissement ; 8 = sub 60f, ROUGE relâché en 15f (la scène réémerge rouge d'abord) ; défaut =
instantané.

### 1.6 La cadence — et l'arbitrage entre les deux agents de reconnaissance

L'original appelle `RenderTransitionEffects` une fois par frame **rendue** (`RenderScene`,
`GM:61`), non gaté par la pause. Mais dans l'original, frame rendue = frame logique (boucle unique).
Dans le port, le rendu est à pas libre : avancer par frame rendue rendrait **la durée du fondu
dépendante du taux de rafraîchissement** — la classe de bug d'E5.c et de la caméra 1re frame.
**Décision : les machines avancent par TICK LOGIQUE (`ticksThisFrame`), comme `UpdateCameraFollow`**,
et l'état courant est poussé au moteur à chaque `Update` (une frame sans tick re-soumet la valeur
tenue). Les données converties sont la version **France (PAL, 50 Hz)** — le tick 50 Hz du port est la
cadence fidèle ; 16 ticks = 0,32 s.

### 1.7 Les opcodes, et le corpus recompté

- **`0xAF`** taille 7 `[op, r, g, b, tpage, duration, persist]` — cible machine B (ordre affiché
  R,G,B §1.2), `g_fadeFrameCounter = persist` (verrou §1.1), `flags = 1`, `BeginFadeEffect`.
- **`0xB0`** taille 5 `[op, r, g, b, duration]` — machine A : cibles (mortes), `g_warpFlags = 1`,
  `SetFadeDuration`. **Dans le port : un minuteur de `duration` frames, rien de visuel.**
- **`0xB1`** taille 1 — prédicat : `Result = (fadeFlags==0 && warpFlags==0) ? 1 : 0`. **Écrit Result
  dans les DEUX cas** — le risque de Result périmé ne vaut que pour un `0xB1` sauté, ce qui est
  précisément l'état actuel du port (`UnknownOpcode` ne touche pas `Result`).

**Corpus, recompté par descente récursive en suivant les sauts** (l'ancien chiffre « 50 sites » était
une marche linéaire qui en cachait les trois quarts ; borne encore basse : 0x7D-0x81 sautent vers des
cibles dynamiques) : **`0xAF` 240 sites / 59 cartes ; `0xB0` 9 sites / 2 cartes ; `0xB1` 7 sites /
4 cartes. La 389 : zéro des trois** — son fondu d'entrée est piloté par le moteur (`WarpPlayer`),
pas par un opcode. **Les goldens sont donc aveugles à toute la tranche** (deux lignes SYSTEM sans
aucune valeur) : les tests portent toute la preuve, et les six goldens doivent rester byte-identiques.

### 1.8 Les 36 calques de backdrop, et le mapping d'origine

Re-vérifié sur l'export : `(Ground=true, BlendMode 2)×3, (3)×17, (4)×16` = **36 calques dessinés
opaques par-dessus toute la scène** (`BackdropRenderer.cs:164-167` force Opaque hors `(true,1)`).
Mapping de l'original (`GraphicManager.cs:846-853`) : **1 = moyenne, 2 = additif, 3 = soustractif,
4 = additif-atténué (B + 0,25·F)**. Correction une fois l'enum moteur livré : 2 → Additive teinte
blanche ; 3 → Subtractive teinte blanche ; **4 → Additive teinte `(63,63,63)`** — le shader sprite multiplie la teinte dans la source
(`SpriteBatch.fx:42`), donc B + 0,247·F — l'original vise 0,25, écart de quantification 63/255,
sans quatrième état de fusion.
**Hors périmètre, explicitement** : le bucket `(Ground=false, BlendMode 1)×34, aussi forcé Opaque —
l'original y gate la fusion **par pixel** sur le bit STP (`:1233-1246`), analyse manquante ; n'y pas
toucher. Cartes témoins de la correction : **Coal Mine (First Entrance)-61** (soustractif),
**Lars' Crypt-21** (additif-atténué) — invisibles sur la 389, qui est du type déjà correct.

## 2. Les contraintes

- **Code moteur → revue non négociable** : sur M1/M2, le plan-verifier a trouvé un vrai défaut à
  CHAQUE ronde. Idem exécution : verifier de clôture obligatoire.
- **`CasaEngine.Launcher/Program.cs` ne doit JAMAIS être stagé** (modification locale de
  l'utilisateur, présente depuis le début de session).
- Le moteur n'a **aucun** post-process/fondu existant (vérifié) ; le patron maison d'une feature V1
  est le système audio : service MonoGame-free + backend/composant mince + page `docs/engine/` +
  tests `CasaEngine.Tests` + cas sérialiseur éditeur si asset.
- Les six goldens byte-identiques ; l'audio exporté intact (rien ne touche à l'export ici — **aucun
  changement de convertisseur ni d'export dans E10**).

## 3. Les décisions

- **D-E10-1/2/3** (utilisateur) : périmètre, architecture « patron audio », 36 calques inclus.
- **D-E10-4 — Moteur = MÉCANISME, DLL = POLITIQUE.** Le moteur reçoit une feature **générique** :
  `ScreenEffectService` (sans type MonoGame : état = actif, r/g/b octets, mode de fusion, plus une
  commodité `StartFade(from, to, seconds, blend)` pilotée par `Update(dt)` pour tout autre
  consommateur) et un composant mince qui soumet le quad plein viewport par frame via
  `SpriteRendererComponent`, à un cran de rendu nommé entre `Effects` (500) et `UI` (1000).
  **Toutes les bizarreries de fidélité — 16.16, échange de canaux, division tronquante, verrou de
  persistance — restent dans la DLL**, qui calcule ses machines et pousse `(r,g,b,blend,actif)` au
  service chaque frame. C'est exactement la découpe audio : le moteur joue, la DLL décide.
- **D-E10-5 — Les états de fusion** : deux membres d'enum `Additive`/`Subtractive`, deux
  `BlendState` **en cache** (§1.3), le contrat anti-allocation de `GetBlendState` respecté.
- **D-E10-6 — L'état de fondu de la DLL est de portée SESSION, avec une justification CORRIGÉE par
  la relecture.** Ma première rédaction affirmait que la portée session « préservait les teintes
  persistantes » à travers les cartes — faux : l'original les **efface** à chaque entrée de carte
  (§1.1). Dans le périmètre d'E10, session et par-monde sont donc **quasi indiscernables à
  l'observation** — et le plan le dit honnêtement. La portée session est retenue pour deux raisons
  moindres mais réelles : la cohérence avec le précédent `AlundraMusicPlayer` (D-C-6), et le chantier
  des transitions à venir, où les fondus de DÉPART enjambent le changement de carte. La remise à zéro
  d'entrée de carte est **délibérée** (l'installation applique le préambule de l'original), pas un
  effet de bord de reconstruction.
- **D-E10-7 — Le fondu d'entrée (effet 0) s'arme À L'INTÉRIEUR de la méthode d'installation**
  appelée par `InitializeWithWorld` — la leçon M16 d'E11.c : pas de second site d'appel supprimable
  avec une suite verte. Seul l'effet 0 est câblé (les transitions de carte poseront l'id d'effet plus
  tard) ; la table du §1.5 est documentée pour ce jour-là.
- **D-E10-8 — Fidèle jusque dans les arêtes** : division tronquante (durée ou durée+1), pas-0 qui ne
  termine jamais, claquage quand la machine est inactive, avance-puis-dessine (première frame 239),
  couleurs de la machine A mortes. Une seule déviation de nécessité : durée 0 gardée au lieu de
  trapper, documentée.

## 4. Tranches — à approuver ensemble, exécutées dans l'ordre

### E10.a — MOTEUR (sous-module CasaEngineMonogame)

1. `SpriteBlendMode.Additive`/`.Subtractive` + `GetBlendState` + deux états en cache (§1.3, §D-E10-5).
2. `ScreenEffectService` (MonoGame-free) : `SetOverlay(r,g,b,blend)`, `Clear()`,
   `StartFade(fromR..B, toR..B, durationSeconds, blend)`, `Update(elapsedSeconds)`, état lisible.
   Sémantique de la commodité calquée sur les rampes d'`AudioService` (cible atteinte exactement,
   pas de dépassement, durée 0 = immédiat).
3. **Le nouveau cran de rendu, livrable nommé** : un membre `RenderPass2D.ScreenEffects = 750`
   (entre `Effects` 500 et `UI` 1000 — il n'en existe aucun aujourd'hui).
4. `ScreenEffectComponent` mince, **avec son contrat de placement écrit** (blocage de relecture : la
   première rédaction disait « soumet le quad » sans dire où, quand, ni depuis quelle caméra — tout
   le mécanisme de dessin de la tranche n'était prouvé que par l'œil en fin d'E10.b) :
   - possède le pixel 1×1 ; soumet quand le service est actif, au cran `ScreenEffects` ;
   - **formule de placement, dans une méthode PARAMÉTRÉE et testable sans device** (blocage de
     relecture : le pixel 1×1 exige un `GraphicsDevice`, `ScreenSizeWidth` déréférence `Window` sur
     un jeu non initialisé, et aucun test moteur ne fabrique d'`ActiveView` — le test de soumission
     aurait été abandonné à l'exécution) : le chemin sprite trié transforme chaque quad par le
     `ViewProj` de la frame (`SpriteRendererComponent.cs:126-149`), donc annulation caméra, **le même
     calcul que `BackdropRenderer.cs:190-235`**. Le composant expose
     `SubmitOverlay(renderer, cameraPosition, viewportWidth, viewportHeight)` — **tous les intrants
     fournis par l'appelant, texture injectable ou contournée quand nulle** — et son `Update` l'appelle
     avec **LA source caméra unique du plan : la caméra de la vue active (`ViewManager.ActiveView`)**
     et `ScreenSizeWidth/Height` ;
   - **point de soumission** : dans l'`Update` du composant — `CasaEngineGame.cs:502` exécute
     `UpdateWorld` AVANT les composants (`:506-520`), donc la DLL a déjà poussé l'état de la frame
     quand le composant lit le service et soumet, avant le `Flush` par vue ;
   - **instancié par `CasaEngineGame`** avec une propriété d'accès, le site exact
     d'`AudioSystemComponent` (`:353`).
5. Propriété `game.ScreenEffectComponent` / accès au service, comme `game.AudioSystemComponent.Service`.
6. **L'action de cutscene `FadeScreen`** (décision utilisateur, ajoutée après la revue de clôture —
   voir §5) : `FadeScreenCutsceneActionData` (champs `r`, `g`, `b`, `duration_seconds`, `blend_mode`),
   exécutée par la fabrique de coroutines via la commodité `StartFade` du service, **bloquante
   jusqu'à l'arrivée** comme `FadeMusicCutsceneActionData` — le calque exact des quatre actions
   audio : cas sérialiseur dans `CutsceneAssetJsonSerializer`, validation dans `CutsceneValidator`,
   test d'aller-retour de sérialisation. **Mutation appariée** : retirer le cas sérialiseur → le test
   d'aller-retour tombe (la famille du piège d'`EntityComponent` : perte de données silencieuse).
7. Page `docs/engine/screen-effects.md` + entrée dans l'index, au gabarit de `audio-system.md`.
8. Tests `CasaEngine.Tests` : rampes du service (cible exacte, long-frame sans dépassement, durée 0,
   redémarrage depuis la valeur courante — le miroir d'`AudioServiceFadeTests`) ; `GetBlendState`
   rend des instances **en cache** (contrat anti-allocation) ; la config exacte des deux
   `BlendState` (fonctions et facteurs du §1.3) ; **et le test de SOUMISSION du composant** (blocage
   de relecture) : un vrai `SpriteRendererComponent` est constructible headless
   (`SpriteRendererComponentBlendModeTests.cs:177-185` le prouve) — pour une position caméra et un
   viewport donnés, assérer la **position monde, l'échelle, la clé de tri, la fusion et la couleur**
   du sprite soumis.

**Mutations E10.a** (chacune vérifiée exécutable) : inverser `ReverseSubtract` en `Subtract` → le
test de config tombe ; allouer le `BlendState` par appel → le test de cache tombe ; dépassement de
rampe non clampé → le test de cible tombe ; **retirer le terme d'annulation caméra de la position du
quad → le test de soumission tombe** — sans lui, tout le dessin de la tranche ne serait prouvé
qu'en jeu.

**Interdits** : ne jamais stager `CasaEngine.Launcher/Program.cs` ; ne rien casser des 1200+ tests
moteur existants. Commit sous-module + pointeur parent.

### E10.b — DLL (dépôt parent)

1. **`AlundraScreenFadeDirector`** (portée session, D-E10-6) : les DEUX machines 16.16 exactes —
   `MoveTowards`, `BeginFadeEffect`/`SetFadeDuration` avec claquage-si-inactive et division
   tronquante, verrou de persistance, échange de canaux §1.2, drapeaux. Avancées par
   `Advance(ticks)` ; `PushTo(service)` chaque frame.
2. **Câblage proxy** : passe de fondu dans `Update` après le bloc caméra/backdrop (position par
   cohérence d'ordre de frame — elle n'a **aucune** dépendance caméra : elle avance les machines par
   `ticksThisFrame` puis pousse couleur/fusion/actif au service) ; armement de l'effet 0 **dans**
   `InstallScreenFadeSystems` appelé par `InitializeWithWorld` (D-E10-7), **sans poussée au service à
   l'armement** (voir l'encadré au-dessus des tests).
3. **Opcodes** `0xAF`/`0xB0`/`0xB1` dans `Dispatch`, seam `IEntityWorldContext` par défaut null,
   branche dégradée — la forme E11. **`0xB1` écrit `Result` dans les deux cas.**
4. **Les 36 calques** : le switch du §1.8 dans `BackdropRenderer`, bucket `(false,1)` intact.

**Câblage caméra, assertion obligatoire** (blocage de relecture : E10.a lit la vue ACTIVE, or rien
sous `Alundra/Scripts` ne référence `ActiveView` — si elle est nulle ou porte une autre caméra que la
caméra résolue du backdrop, le quad part hors champ avec des tests verts) : E10.b doit prouver, par
test sur le chemin d'installation réel ou par évidence citée, que **la caméra de la vue active EST la
caméra résolue** que `AlundraBackdropStage` utilise déjà — et si `ActiveView` s'avère absente du
runtime Alundra, c'est un **arrêt à remonter**, pas un contournement silencieux. La justification
« la passe de fondu a besoin de la caméra résolue » de la première rédaction était **fausse** (la DLL
ne pousse que couleur/fusion/actif) et est retirée.

**L'armement ne pousse RIEN** (blocage de relecture : pousser l'état courant dès l'installation
enverrait 255 — une frame noire pleine que l'original n'a jamais, §1.5). L'installation arme les
machines de la DLL **sans toucher au service** ; la première poussée a lieu dans `Update`, **après**
`Advance(ticks)` — et le plancher de tick de première frame (correctif caméra, `_firstFrameStillOpen`)
garantit ≥ 1 tick, donc **la première valeur poussée est 239**, jamais 255.

**Tests, écrits AVANT, chacun avec sa mutation appariée vérifiée exécutable :**

- **T1 — l'effet 0, chiffré** : armer par le vrai chemin d'installation ; **avant tout `Update`, le
  service est intact (aucune poussée)** ; au premier `Update`, la valeur poussée est **239** ; puis
  tick par tick **la table des 16 valeurs du §1.5 exactement**, le clear du drapeau au tick 16,
  **plus aucune soumission au tick 17**. Mutations : dessiner-puis-avancer → la première valeur
  devient 255 ; pousser dès l'armement → l'assertion « service intact avant Update » tombe.
- **T2 — jumeau de neutralisation** : sans service (pas de `Game`), zéro soumission, zéro exception.
- **T3 — l'échange de canaux** : `0xAF` avec une couleur asymétrique (r≠g≠b) → la couleur POUSSÉE au
  service est (r,g,b) dans l'ordre d'affichage. Mutation : suivre les noms de la décomp sans
  l'échange → T3 tombe.
- **T4 — la troncature, avec le SIGNE épinglé** *(blocage de relecture : l'arête « ne se termine
  jamais » est directionnelle)* : un delta non divisible → `duration + 1` ticks ; un fondu **MONTANT**
  (cible AU-DESSUS de la valeur courante) avec `|delta| < duration` → pas 0, **ne se termine jamais**,
  `0xB1` rend 0 pour toujours ; et le symétrique **descendant** avec pas 0 → **claque en UN tick**
  (la branche `step >= 0` de `MoveTowards` déclare l'arrivée immédiate quand la cible est en dessous).
  Mutation : arrondir au lieu de tronquer → T4 tombe.
- **T5 — le verrou de persistance** : `0xAF` avec persist ≠ 0 → après arrivée, la teinte reste
  soumise à la couleur posée, indéfiniment. Mutation : traiter persist comme un compte à rebours →
  T5 tombe.
- **T6 — `0xB1` des deux côtés** : poser `Result = 1` périmé, dispatcher `0xB1` pendant un fondu →
  0 ; après arrivée → 1. Mutation : laisser `0xB1` en `UnknownOpcode` → T6 tombe sur le périmé.
- **T7 — le préambule de remise à zéro, PUIS le ré-armement** *(refondu DEUX fois en relecture : la
  v1 assérait qu'une teinte survit au changement de carte — inverse de l'original ; la v2 assérait
  « tout à zéro » après installation — faux aussi, car le préambule `:886-888` est immédiatement
  suivi du ré-armement de l'effet 0 `:895-905`)*. L'état fidèle après installation du second monde :
  **verrou de persistance à 0, teinte du monde 1 jetée, ET fondu effet 0 fraîchement armé**
  (`flags = 1`, première valeur avancée 239) ; l'instance du directeur est la même (ré-appointée).
  Après l'arrivée du fondu du monde 2 (tick 16), **plus aucune soumission au tick 17**.
  **Mutation, ré-appariée pour mordre** : retirer le seul préambule de remise à zéro (garder le
  ré-armement) → le verrou du monde 1 fuit, la garde `flags==0 && verrou==0` reste ouverte après
  l'arrivée, et **la soumission continue au tick 17** — l'assertion finale de T7 tombe.
- **T8 — le mapping backdrop** : un document synthétique avec les quatre BlendMode → les couples
  (fusion, teinte) du §1.8 exacts, et le bucket `(false,1)` reste Opaque. Mutation : mapper 4 sur
  Subtractive → T8 tombe.

**Acceptation** : `CasaEngine.Tests` au vert (build + suite moteur complète, aucun nouvel échec) ;
`Alundra.Tests` 637 + n ; convertisseur 139 ; **six goldens byte-identiques** avec preuve positive
d'exécution ; audio exporté intact (aucun export relancé) ; **et la validation en jeu** : au New
Game sur la 389, la première frame est quasi noire et le pont émerge en ~0,32 s — le fondu de
l'original. Pour VOIR la correction des 36 calques il faut charger une carte témoin (§1.8) — hors
recette obligatoire, à la discrétion de l'utilisateur.

## 5. Budget, arrêts, état de relecture

**Budget** : E10.a un commit sous-module + pointeur ; E10.b un commit parent ; exécution par agents
(moteur puis DLL, le second dépend de l'enum du premier) ; verifier de clôture avant tout commit.
≤ 6 tours au total.

**État de relecture.** Trois rondes, sept blocages, tous FIX ; revue de clôture **READY**.
**Ajout postérieur au READY, sur décision utilisateur** : l'action de cutscene `FadeScreen`
(livrable 6 d'E10.a) — non re-relue, mais calquée sur un précédent littéral du dépôt (les quatre
actions audio, sérialiseur + validateur + tests compris) et couverte par sa propre mutation. Le
verifier de clôture d'exécution la vérifie comme le reste. Deux précisions issues des questions de
l'utilisateur : la durée de 16 frames vaut **0,32 s sur la version France (PAL, 50 Hz) convertie
ici** (~0,27 s sur NTSC) ; et hors cette action de cutscene, la feature n'a **pas de surface
éditeur** — choix explicite, pas un oubli.

**Arrêts** : un golden qui bouge ; un test moteur existant qui casse ; `CasaEngine.Launcher/Program.cs`
stagé ; toute tentative de « corriger » l'échange de canaux, le pas-0, ou la troncature (§D-E10-8) ;
le bucket `(false,1)` touché ; un fondu piloté par frame rendue au lieu du tick (§1.6).
