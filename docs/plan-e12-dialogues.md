# Plan — E12 : les dialogues

Date : 2026-09-01. Origine : étape choisie par l'utilisateur, avec trois décisions actées avant
rédaction : **route directe** (le bytecode pilote l'écran de dialogue — pas de Yarn en E12, il reste
pour E15) ; **police `font3` bitmap AVEC extraction des largeurs proportionnelles** depuis les données
du jeu ; **cadence fidèle en tranche 2**, après le premier dialogue jouable.

**Tout le §1 vient de la reconnaissance, points porteurs revérifiés en session principale.**

## 1. Les faits établis

### 1.1 Le plan maître est périmé côté moteur — comme pour l'audio

Un pipeline de dialogue **existe déjà de bout en bout pour les lignes simples** : paquets YarnSpinner
3.2.1, compilateur, `DialogueAsset` + chargeur `.dialogue`, `YarnDialogueRunner` (lignes),
`DialogueService`, **`DialogueScreen` MGUI modal** (UIScreenBase, `IsModal`, wrapping, balisage riche),
navigation clavier **et manette** (Submit/Cancel/Navigate, `MGListBox` focusable), arbitrage d'entrée
modal (`InputRouter` route tout vers l'UI modale), et une **démo fonctionnelle** (`UIOverlayDemo` :
`.dialogue` compilé → runner → écran modal au-dessus d'une scène).

**Ce qui manque vraiment côté moteur** : les **choix** (le presenter/service est mono-ligne,
`OnOptions` est un stub — mais la route directe n'a pas besoin du côté Yarn de ce stub), la découpe
locuteur, **le chargement d'une police bitmap** (MGUI ne consomme que des TTF via `FontSystem` ;
`StaticSpriteFont.FromBMFont` est dans le paquet FontStashSharp, vérifié par réflexion — il manque le
chemin d'enregistrement), et l'effet machine à écrire (tranche cadence).

### 1.2 La machinerie du message de l'original — la spec de la tranche cadence, et le contrat de la tranche 1

Boîte 288×56 px en bas d'écran, **3 lignes** de glyphes 16 px, glissement d'ouverture 15 ticks, sfx 6
à l'ouverture / 7 à la fermeture. Révélation : **1 glyphe/4 frames** (compteur `g_textDelayReset=4`),
**1 glyphe/frame bouton tenu**, blip vocal un glyphe sur deux si une voix est active. Pagination :
saut de ligne en ligne 2 → défilement de 16 px en 8 pas ; `\A` = attente bouton avec **curseur animé**
16×16 (4 images, 10 ticks/image) en bas à droite. Avance/fermeture au bouton d'interaction
(bit **0x80**, appui naissant).

**Modes de fermeture** (`g_etcAnimationMode`, défaut **3**) : bit0 = minuterie auto **360 frames**,
bit1 = bouton, bit2 = fermeture par script (`0x51`). La 389 pose le masque **4** pendant la question
du choix : *seul le script ferme*.

**Blocage du gameplay** par l'opérande `controlMode` de `0x0D`/`0x5C` : **1 → `MessageBox` (0x10)**,
dans `InputBlockedMask` — le joueur est figé mais **le monde et les scripts continuent de tiquer**
(obligatoire : c'est le Tick lui-même qui interroge `0x39`/`0x44`) ; **0 → `MenuOpen` (0x08)**, dans
`GameplayBlockedMask` — les map events s'arrêtent aussi. La fermeture efface les deux. Les portes de
ces deux masques **existent déjà dans la DLL** (E4.c/E6).

### 1.3 Les opcodes — plusieurs noms de la table sont FAUX, la sémantique lue fait foi

| op | taille | sémantique réelle (décompilation) |
|---|---|---|
| `0x0D` | 3 `[op,textId,ctrl]` | ouvre le dialogue. **`textId & 0x80` → chaîne LOCALE `Strings[textId & 0x7F]`** ; bit clair → table PARTAGÉE `map_alundra` (§1.5). **Retourne 0 (re-tenté chaque frame) tant qu'un dialogue est ouvert.** Chaque ouverture **remet le mode de fermeture à 3**. |
| `0x39` | 1 | « Wait for dialog » — **porte bloquante** : retourne 0 tant que le dialogue est actif, avance d'1 une fois la boîte **fermée**. **N'écrit PAS `Result`.** |
| `0x44` | 1 | le CHOIX. Avec état : première entrée → ouvre la boîte OUI/NON — **libellés = chaînes GLOBALES ETC index 0x43/0x44, jamais des chaînes de carte** — puis bloque jusqu'à la sélection ; **`Result = 1 ssi la PREMIÈRE option**, sinon 0 ; avance d'1. |
| `0x50` | 2 `[op,mask]` | mal nommé « Set dialog choice » : **pose le masque de FERMETURE** (§1.2). |
| `0x51` | 1 | mal nommé « Get dialog choice » : **demande de fermeture par le script**. Pas de `Result`. |
| `0x5C` | 4 `[op,search,textId,ctrl]` | comme `0x0D`, portrait/nom pris sur l'entité trouvée par `search`. Retourne 0 = re-tenter. |

Adjacents, hors périmètre dialogue : `0x27` Face player (**déjà implémenté**) ; `0x59` Set entity anim
(non implémenté — le marin ne changera pas d'animation, cosmétique, différé) ; `0xC4` variante avec
nom (66 sites corpus, zéro sur la 389) ; `0x4C`/`0x4D` drapeaux de cadence (tranche cadence).

### 1.4 Le flux exact du marin 12 — l'acceptation, décodée chaîne par chaîne

F(Interact) @1656 = `0x05 FlagOn 0x800C ; 0xFF` — **l'interaction arme le Tick**. Tick 140 @1356,
**avec sa garde d'entrée que ma première rédaction omettait** (blocage P0 de relecture) :
`0x30 If flag ON 0x35C → +9 (1365), sinon Goto 1433` (la boucle d'attente d'intro). **`0x35C` = 860 =
LE drapeau de fin d'intro** — celui que l'oracle doré épingle à la frame 1704. Le corps du dialogue
n'est donc atteignable qu'APRÈS l'intro : cohérent en jeu réel (le joueur ne gagne le contrôle qu'à
1704, quand 860 vient d'être posé), mais **T1 doit semer 860 EN PLUS de 0x800C**, et la validation en
jeu se fait après avoir laissé l'intro se terminer. Puis, depuis 1365 :
`0x10 → 0x59 → 0x27 → 0x31 (flag 0x367 : déjà parlé ?) → 0x0D idx1` («
Qu'est-ce que tu veux, petit ? As-tu encore oublié où se trouve ta cabine ?\999\Y ») `→ 0x50 [4] →
0x36 → 0x44 (OUI/NON) → 0x51 → 0x03 → 0x0D idx2` (« C'est la CINQUIÈME fois que je te répète que ta
cabine est celle de droite ! ») `ou idx3 → 0x39 → drapeaux → 0x11`. Visites suivantes : idx9. Les six
autres marins : des F(Interact) mono-ligne (idx0, 4/10, 5, 6, 7/11, 27).

**Aucun marin n'a de boîte de nom** : leurs `SpriteTableIndex` (146/161) sont sous 0x100 — le nom et
le portrait sont différables sans toucher l'acceptation.

### 1.5 Les données

- **Chaînes locales** : `dialogues/{monde}.strings.json`, tableau de **128 positions** (l'index du
  bytecode), 41 non vides sur la 389 ; 483 fichiers, 24 303 chaînes non vides au total. Codes de
  contrôle dedans : `\N` (24 612 occurrences, saut de ligne), `\A` (6 972, attente), `\C` (centrage),
  `\Y`, `\999`, jetons `{`/`}` (« o}i » = où) — inventaire complet dans `control-codes.json` (27 codes).
- **Chaînes globales** : `Dialogues/global-strings.json`, clés = **offsets** décimaux dans ETC_RES ;
  les libellés OUI/NON de `0x44` y sont — **mais `0x44` les adresse par INDEX (0x43/0x44)**, et la
  table de correspondance vit dans le FICHIER ETC_RES, pas dans le code : exportée en
  `Dialogues/etc-index.json` par E12.b (voir D-E12-6, corrigé en relecture).
- **TROU CONVERTISSEUR** : la table partagée `map_alundra.json` (99 chaînes système : « C'est
  verrouillé. », « Clef obtenue ! ») — adressée par les `0x0D` à bit 0x80 clair, **345 sites
  corpus-wide, ZÉRO sur la 389** — **n'est pas exportée**. Différé en E12.c avec ré-export.
- **Police** : `UI/font3.fnt` valide (BMFont 16 px, 214 glyphes, page + `.texture` catalogués), mais
  **monospace** : chaque `xadvance` vaut 16. **La table des largeurs EXISTE dans la décompilation** —
  `g_fontCharWidthTable` (`StaticVariables.cs:9484`), pas de 5 entiers, avance en `[code*5]`,
  consommée par `TextDecoder` aux quatre sites d'avance — le commentaire du `FontWriter` (« jamais
  extraite ») était périmé. La collision CP850 (42/256) est sans gravité : chaque glyphe français
  reste atteignable par son point de code canonique.

### 1.6 Les goldens sont AVEUGLES, et le forçage optimiste s'auto-neutralise

**Zéro des huit opcodes dispatchés** dans la fenêtre des 1704 frames (comptés dans la trace ; le corps
du dialogue du marin 12 est derrière 0x800C, posé par l'interaction seulement). Et le forçage
`OptimisticPredicateOpcodes = {0x39,0x44,0x51}` ne s'applique **qu'aux `UnknownSkipped`**
(`IntroTraceHarnessTests.cs:1352-1362`) : dès qu'un opcode gagne un `case`, le forçage se neutralise
tout seul — retirer les entrées du set est un nettoyage, pas un ré-étalonnage. **Aucun octet doré ne
peut bouger ; E12 porte son propre oracle** (la conclusion E11, connue d'avance pour la 3e fois).

Corpus (descente récursive) : `0x0D` **2247 sites/332 cartes**, `0x39` 2383/302, `0x5C` 784/115,
`0x44` 117/52, `0x50` 207/100, `0x51` 177/81. Sur la 389 : 23 sites, dont **un seul** `0x44`.

À corriger au passage (harnais) : `TryResolveDialogText` lit `parameters[0]` **sans le masque 0x7F** —
toute annotation d'id local ≥128 résout à null aujourd'hui.

## 2. Les contraintes

- **La règle du seam** : dispatché par l'interpréteur → `IEntityWorldContext`, membre par défaut null,
  branche dégradée, méthode d'installation interne (le patron E11).
- **L'état du dialogue est global dans l'original** (`g_dialog_flags`, mode de fermeture, résultat du
  choix) → **directeur de portée SESSION** (la leçon D-C-6/E10 : un état par-monde rend les gardes
  vacues) ; remise à zéro à l'entrée de carte comme les fondus.
- **Les opcodes bloquants dégradés ne doivent PAS interbloquer** : sans presenter, `0x0D` saute,
  `0x39` avance, `0x44` doit écrire un `Result` déterministe — **1** (première option), le comportement
  qu'avait le forçage optimiste du harnais. Documenté comme mode dégradé.
- Moteur → plan-verifier et verifier de clôture non négociables ; `CasaEngine.Launcher/Program.cs`
  jamais stagé ; `CasaEngine.Tests` à builder explicitement avant `--no-build`.
- Ré-export complet requis par E12.b (largeurs) — identité au bit près sur **tout sauf** `font3.fnt`
  et le rapport, baselines nommés avant le run.

## 3. Les décisions

- **D-E12-1 (utilisateur) — route DIRECTE** : la DLL pilote le presenter/`DialogueService` avec le
  texte des `strings.json`. Yarn n'est pas touché (il reste le chantier E15) ; le stub `OnOptions` du
  runner Yarn reste un stub.
- **D-E12-2 (utilisateur) — `font3` + largeurs proportionnelles** : chemin d'enregistrement
  `StaticSpriteFont` dans le moteur (E12.a, monospace d'abord) ; table des largeurs publiée par
  l'analyseur et consommée par `FontWriter` (E12.b, le précédent `MapMusicIndex` : liée, pas copiée).
- **D-E12-3 (utilisateur) — cadence en tranche E12.c** : machine à écrire, pagination, curseur, blips,
  minuterie 360, `0x4C`/`0x4D`, nom/portrait, table partagée `map_alundra`, `0xC4`.
- **D-E12-4 — E12.a affiche le texte PAR PAGES, instantanément** : découpe sur `\A`, `\N` = saut de
  ligne, avance au bouton (bit 0x80, appui naissant, via `GameState.LastPadState`), fermeture selon le
  masque.
  **CORRECTION P0 de relecture — les codes NUMÉRIQUES `\<chiffres>` sont ACTIFS, pas décoratifs** :
  `\999` POSE le drapeau temporaire n° 999 (= `0x83E7` : banque temporaire,
  `index = ((n>>3)&0xffc)>>2`, `bit = n&0x1f` — la formule exacte de `TextDecoder.cs:259-307`), et
  c'est **précisément le drapeau que le `0x36 [231,131]` du marin 12 attend entre `0x50` et `0x44`**.
  La chaîne signale elle-même au script qu'elle a été affichée. Retirer ces codes gèlerait le joueur à
  jamais (0x10 + MessageBox posés, choix jamais ouvert). Règle : **à l'affichage d'une page, tous ses
  codes numériques posent leurs drapeaux temporaires** ; les autres codes non gérés sont retirés et
  comptés (log une fois par code, liste dans `control-codes.json`).
  **Le mode dégradé pose AUSSI les drapeaux numériques** : un `0x0D` sans presenter n'affiche rien
  mais parse la chaîne et pose ses drapeaux — sinon le `0x36` suivant suspend indéfiniment.
- **D-E12-5 — le moteur reçoit le MÉCANISME générique** (le partage E10) : presenter avec état de
  choix (`ShowLine`, `ShowChoices(labels[])`, `SelectedChoice`, `Close`), liste de choix dans
  `DialogueScreen` naviguée par la mécanique de focus existante ; **toutes les bizarreries Alundra**
  (masques de fermeture, `Result`, drapeaux de contrôle, codes `\`) restent dans la DLL.
- **D-E12-6 — la correspondance index ETC → offset N'EST PAS DANS LE CODE** (blocage P1 de
  relecture) : `GetEtcString(id) = StringByIndex[IndexTable[id]]`, et **`IndexTable` est lue depuis le
  FICHIER ETC_RES** (1024 int16 en tête, `EtcResR.cs:7-23`) — la dériver « de la décompilation » était
  impossible. **Et la ronde de clôture a trouvé le trou suivant : le convertisseur n'a AUCUNE source
  pour cette table** — son entrée `data/ETC_RES.R.json` commence à la clé 2048 (la seule région des
  chaînes ; la région d'index 0..2047 n'a jamais été extraite), et le binaire `ETC_RES.R` n'est pas
  dans ce dépôt. La table n'est lisible que côté ANALYSEUR (`EtcResR.cs:7-13`, depuis les fichiers du
  jeu). **Chaîne de production retenue — le précédent exact de `MapMusicIndex.csv`** : un dump
  one-shot côté analyseur (outil jetable lisant `ETC_RES.R` au chemin du jeu) produit un
  `EtcIndexTable.csv` COMMITÉ dans l'analyseur, avec sa provenance écrite ; le convertisseur le LIE
  (comme `EntityNames.csv`) et `TextWriter` émet `Dialogues/etc-index.json` (1024 entrées, brut).
  Test convertisseur : 1024 entrées, et les valeurs aux index 0x43/0x44 sont des clés **réellement
  présentes** dans `global-strings.json` et résolvent les deux libellés attendus. La DLL résout
  OUI/NON = `global-strings[etc-index[0x43]]` / `[0x44]`. **E12.b s'exécute AVANT la moitié DLL
  d'E12.a** (ordre : E12.a-moteur → E12.b → E12.a-DLL ; un seul ré-export).

## 4. Tranches

### E12.a — le dialogue jouable *(à approuver maintenant)*

**Moteur (sous-module)** : ① presenter/`DialogueService` avec état de choix + liste de choix dans
`DialogueScreen` (MGListBox ou boutons, focus/pad existants) — tests miroirs de `DialogueServiceTests` ;
② chemin d'enregistrement d'une police bitmap (`FromBMFont`) + `DialogueScreen` sur `font3` ;
③ page `docs/engine/` mise à jour. **Mutations** : liste de choix sans câblage `SelectedChoice` → le
test de sélection tombe ; police non enregistrée → le test de résolution tombe.

**DLL** : ③bis **LE MAILLON UI, livrable NOMMÉ** (blocage P2 de relecture — la famille « jamais
dessiné » a déjà frappé deux fois : fondu, backdrops) : un `AlundraDialoguePresenter` côté DLL qui
construit le `DialogueScreen` moteur, **obtient la vue UI par la route de la démo**
(`GameManager.ViewManager.GetActiveUIView()`) et **pousse/retire l'écran sur la pile** à
l'ouverture/fermeture — appelé depuis la méthode d'installation (pas de site séparable, leçon M16).
**Assertion AUTOMATISÉE sur le maillon** *(corrigée en clôture : `ScreenStack` n'est PAS
constructible headless — son `Push` initialise l'écran contre un `UIRoot` qui exige la pile graphique
vivante ; ma revendication était fausse)* : le presenter de la DLL dépend de **`IUIViewRuntime`**
(`PushScreen`/`RemoveScreen`) — **l'interface que la démo elle-même consomme** — et le test utilise un
double enregistreur de cette interface : push à l'ouverture, retrait à la fermeture. L'œil ne reste
pas la seule preuve du séquencement ; le rendu réel, lui, reste couvert par la validation en jeu.
Mutation : ne pas pousser à l'ouverture (ou ne pas retirer à la fermeture) → exactement ce test tombe.
④ `AlundraDialogueDirector` (session, remis à zéro à l'entrée de carte) : état
ouvert/fermé, masque de fermeture, résultat de choix, découpe en pages (D-E12-4), résolution des
chaînes locales (`strings.json`, **masque 0x7F**) et des libellés OUI/NON (D-E12-6) ; pose/efface
`MessageBox`/`MenuOpen` selon `controlMode` ; avance au pad ; minuterie du masque bit0 par ticks ;
⑤ opcodes `0x0D` (retour 0 tant qu'ouvert, remise du masque à 3 à chaque ouverture), `0x39` (porte
bloquante, PAS de `Result`), `0x44` (avec état, `Result` des deux côtés), `0x50`, `0x51`, `0x5C`
(recherche ignorée pour le portrait différé, ouverture identique) ; ⑥ correctif du masque manquant
dans `TryResolveDialogText` du harnais ; ⑦ retrait de `{0x39,0x44,0x51}` du set optimiste (nettoyage,
§1.6).

**Tests DLL, écrits AVANT, mutations appariées vérifiées exécutables** :
- **T1 — le flux du marin 12 AU VRAI SITE DE PRODUCTION** *(corrigé en relecture : ma première
  rédaction nommait `RunPendingEventTriggers`, qui est le RATTRAPAGE D3, pas le chemin du slot C)* :
  monde 389 réel, **drapeaux `860` ET `0x800C` semés**, une **boucle de frames** dont chaque itération
  traverse la vraie phase de pick — `AlundraEntityScriptProxy.Update` → `PickEventTrigger` →
  `RunPickedEvent` — pendant que `0x44` est suspendu (l'état du programme DOIT survivre d'une frame à
  l'autre). Assertions chiffrées : le programme passe la garde `0x30` et atteint 1365 ; la question
  est **la chaîne d'index 1** ; `\999` posé à l'affichage débloque le `0x36` ; le masque passe à 4 ;
  le choix s'ouvre avec **les libellés OUI/NON exacts** (résolus par `etc-index`) ; **choix 1 →
  `Result=1` → l'index 2** ; **choix 2 → l'index 3** ; `0x39` bloque puis avance ; les drapeaux de
  contrôle posés puis effacés. **Cas négatif consigné** : sans 860, le programme part en 1433 et rien
  ne s'ouvre. Mutations : inverser la convention de `Result` → mauvaise branche ; casser la
  conservation d'état inter-frames → T1 tombe pendant la suspension ; retirer le code numérique de la
  chaîne d'index 1 → le programme reste suspendu au `0x36` et T1 tombe.
- **T2 — `0x0D` retourne 0 tant qu'un dialogue est ouvert** (re-tenté, pas sauté). Mutation : avancer
  quand même → double ouverture détectée.
- **T3 — modes de fermeture** : masque 4 → le bouton ne ferme PAS, `0x51` ferme ; masque défaut 3 →
  le bouton ferme ; **chaque `0x0D` remet le masque à 3**. Mutation : ne pas remettre → T3 tombe.
- **T4 — pages** : une chaîne à `\A` et `\N` → pages et sauts de ligne exacts, codes inconnus retirés
  et comptés. Mutation : ne pas découper sur `\A` → T4 tombe.
- **T5 — dégradé sans presenter** : aucun blocage infini, `0x44` écrit `Result=1`, kinds `Degraded`.
  Mutation : laisser `0x44` en `UnknownOpcode` → le périmé revient.
- **T6 — les portes de contrôle** : `controlMode 1` → `MessageBox` posé (le joueur est bloqué par la
  porte existante d'E4.c), `0` → `MenuOpen` ; la fermeture efface les deux.

**Acceptation** : suites au vert (`Alundra.Tests` 662+n, convertisseur 139+n, moteur 1421+18
préexistants, zéro nouveau) ; **six goldens byte-identiques** avec preuve d'exécution (§1.6 — s'ils
bougent, arrêt) ; le test du maillon UI (③bis) au vert ; **et la validation en jeu** : laisser
l'intro se terminer (le drapeau 860 se pose à la frame 1704), puis parler aux marins — le dialogue du
marin 12 avec le choix OUI/NON navigué à la manette, en `font3`, les deux branches accessibles.

### E12.b — les largeurs proportionnelles *(à approuver avec E12.a, exécutée après)*

Analyseur : publier `g_fontCharWidthTable` (avance = `[code*5]`, dérivation écrite) en CSV lié —
le précédent `MapMusicIndex`. Convertisseur : `FontWriter` pose `xadvance` par glyphe (mapping raw →
codepoint existant) ; **et la table d'index ETC** : dump one-shot analyseur → `EtcIndexTable.csv`
commité (provenance écrite), lié au convertisseur, `TextWriter` émet `Dialogues/etc-index.json`
(D-E12-6 corrigé — la région d'index n'a jamais été extraite, le convertisseur n'avait aucune source).
**Ré-export complet**, identité au bit près sur tout sauf `font3.fnt`, `etc-index.json` (nouveau) +
`report.json` (baselines pris AVANT). Test convertisseur : deux glyphes de largeurs sources
différentes → `xadvance` différents dans le `.fnt`. Vérification visuelle : le texte n'est plus
espacé uniformément.

### E12.c — la fidélité fine *(plus tard)*

Machine à écrire (1/4 frames, bouton tenu ×4), pagination défilante, curseur d'attente animé, blips,
minuterie, `0x4C`/`0x4D`, boîte de nom + portrait (`0xC4`, entités ≥0x100), table partagée
`map_alundra` (export + `0x0D` bit clair), sfx 6/7 d'ouverture/fermeture.

## 5. Budget, arrêts

**Budget** : ordre d'exécution **E12.a-moteur → E12.b → E12.a-DLL** (D-E12-6 : la DLL consomme
`etc-index.json`) ; un commit moteur + pointeur, un commit analyseur, un commit convertisseur (largeurs
+ etc-index + un seul ré-export), un commit DLL ; ≤ 8 tours. Exécution par agents, verifier de clôture
avant tout commit.

**État de la relecture, à savoir avant d'approuver.** Deux rondes, **sept blocages** (dont deux P0),
tous dispositionnés FIX. Les plus marquants : la garde d'entrée du marin 12 omise (le drapeau 860 de
fin d'intro — T1 et l'acceptation étaient inatteignables tels qu'écrits) ; les codes de contrôle
numériques ACTIFS (`\999` pose le drapeau que le `0x36` attend — les retirer aurait gelé le joueur à
jamais) ; une table de données sans source (la région d'index d'ETC_RES n'a jamais été extraite) ; et
une revendication de testabilité fausse (`ScreenStack` n'est pas constructible headless). **Le plafond
de relecture est atteint : cette version n'a PAS été re-relue.** Les deux dernières corrections
suivent mot pour mot les révisions minimales prescrites par le relecteur de clôture.

**Arrêts** : un golden qui bouge ; un test moteur préexistant qui casse ; un fichier exporté hors
`font3.fnt`/`report.json` qui bouge en E12.b ; `Program.cs` du lanceur stagé ; toute inversion des
noms trompeurs de `0x50`/`0x51` non documentée ; un opcode bloquant qui interbloque en mode dégradé ;
et si T1 passe avant l'implémentation, il ne teste rien — arrêt.
