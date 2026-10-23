# Rapport de découverte E20.g5 (les vrais objets à pousser), lecture seule

HEAD lu : `46589b86` (branche `chantier/theme1-porter-plateforme`). La tâche citait `55cff3df` : deux commits de docs sont arrivés depuis. L'executor a modifié `ArrowAi*` pendant mon travail.

**Dépôt :** je n'ai rien construit, modifié ni indexé dans le dépôt. `git status` n'y montre que les fichiers de l'executor et les fichiers non suivis qui préexistaient. La DLL réelle de `alundra-project/` date de 11:40 et n'est pas de moi.

**Binaire :** `ALUN_CD.EXE` (France), SHA-1 `a705f8da…` vérifié.

**Dossier de travail :** `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g5-disc/`. Je l'appelle `G5/` ci-dessous. Les hachages sont dans `G5/MANIFEST.sha256`.

Étiquettes : FAIT (lu), MESURÉ (exécuté sur la machine MIPS ou sur la vraie DLL dans une copie), PROTOTYPE (patch jetable dans la copie), INFÉRENCE.

## 1. Verdicts

1. **Le pilier est seul à manquer de code.** MESURÉ au HEAD : le héros bute sur le pilier (`XCollisionEntity` posé dès la 2e image) et le pilier reste immobile 160 images. Sa clé `(C, 76)` n'est pas au registre, donc zéro exécution native. Un prototype de 40 lignes (`PillarAi`) reproduit les valeurs de la machine (§4).
2. **Le gestionnaire C76 est décodé et rejoué.** Le modèle égale le code réel sur 6 000 vecteurs sans écart, avec 82/82 instructions couvertes.
3. **Les statues, le bloc de la 23 et le bouton de la 40 bougent déjà au HEAD.** Ils sont entièrement scriptés, et le moteur d'événements les joue. L'affirmation de g2c « sans tranche, ils restent immobiles » est fausse pour eux (MESURÉ, §5).
4. **Deux opcodes manquent, pour le seul poussoir :** `0x72` (24 sites sur 12 cartes) et `0xAA` (22 sites sur 8 cartes). Hors des quatre familles, `0xA9` a 4 sites sur la carte 440.
5. **Découverte annexe, la décompilation se trompe à deux endroits.** L'opcode `0xBE` n'est pas « jouer le son (bis) » : le binaire appelle `StopSoundEffect` (`0x80049634`). Idem pour la fin de C76. La DLL rejoue le son là où le binaire l'arrête (392 sites sur 119 cartes, dont 12 sur la chaîne).
6. **Aucune des 30 cartes de la chaîne d'histoire ne porte une de ces familles.** L'intersection est vide, donc aucun arc de la chaîne ne bouge.
7. **Plaques et interrupteurs ne sont pas actionnés en poussant un pilier au sol.** `sm1` (`0x60`/`0x61`) n'est pas requis. Le gating par D5b est INFÉRENCE (§3.2).

## 2. Recensement (FAIT, MESURÉ)

Les 111 placements sont identiques entre `data-extracted/` et l'export `alundra-project/` (script `G5/census_export.py`). Les sorties sont dans `G5/census_g5.txt`, `G5/census_g5.json`, `G5/census_signature.txt`.

Le type d'une entité est `SpriteTableIndex`, plus `0x100` si `SpriteDirection & 0x80`. Les drapeaux sont `MoreFlags | CanPickup<<8 | ombre<<16`.

| Famille | Type | En-tête | Placements |
|---|---|---|---|
| Pilier poussable | 430 (M174) | `0x80` collidable, `CanPickup 1` (gravité), codes A/D/E/F à 0, **C=76**, boîte 20×14×32, drapeaux `0x330180` | 24, 8 cartes |
| Statue poussable (événement) | 298 (M42) | `0x80` + `NoObstacleSlide 0x2000`, aucun code natif, boîte 20×14×32, drapeaux `0x32080` | 62, 11 cartes |
| Bloc à pousser/attaquer | 229 (page globale) | idem, boîte 48×32×32, 5 jeux d'animation de vitesses 0/64/128/176/256 | 9, 4 cartes |
| Interrupteur poussoir | 345 (M89) | idem, boîte 24×16×16, animation 1 de vitesse 64 | 16, 2 cartes |

**Pilier (24)**
- C76 natif actif : 11 placements, cartes 26, 30, 341 (×3), 343, 344 (×2), 346 (×2), 347.
  - Sur la 341, le programme A23 pose le drapeau `0x2000`.
  - Le pilier 343/40 naît par script (`0x8A` dans C[40]@2208).
- Inertes : 13 sur la 423, parce qu'un programme C scripté remplace le natif.
  - Douze ont un programme `00 FF` vide.
  - Le 13e (rec 24) a un A et un C scriptés (zone, touche de la croix directionnelle).

**Statue (62), toutes scriptées**
- 19 statiques sans programme (37 : 14 ; 149 ; 351 : 3 ; 382).
- Les autres :

| Signature | Placements | Cartes |
|---|---|---|
| ACF | 3 | 2, 152, 352 |
| C seul | 7 | 117, 152 ×4, 115 ×2 (naissance par script) |
| CF | 3 | 37 ×2, 351 |
| CF, naissance par script | 20 | 51 |
| A seul, naissance par script | 10 | 102 |

**Bloc (9)**

| Carte | Placements | Déclencheur |
|---|---|---|
| 23 | 1 | contact (F) |
| 100 | 5 | pilotés par drapeaux |
| 102 | 2 | coup (D) |
| 382 | 1 | coup (D) |

**Poussoir (16)**
- Carte 40 : 8, avec C+F.
- Carte 426 : 4 avec C scripté (`0xAA`), 4 sans programme.

**Ce que lit la DLL**
- Les en-têtes viennent de `Data/sprite-records.json` (395 entrées).
- Les placements viennent des objets Tiled « Entities » des `.tmj`.
- Sondé au HEAD sur les huit cartes de piliers : types 430 chargés, `SpriteProgramIndexes[C]=76`, drapeaux `0x330180` (ou `0x332180` sur la 341).

## 3. C76 et le héros

### 3.1 Corps décodé (FAIT, MESURÉ)

C76 fait 43 mots, de `0x8007C024` à `0x8007C0D4`. Le héros est l'entité `0x80127D30`.

```
si hero.+0x130 == pilier :           (le héros est bloqué en X par le pilier)
    t = pilier.+0x274  (mot 32 bits signé)
    si t < 31 : t+1 ; fin
    PlaySoundEffect(0x19) ; +0x88 = 1
    r = Direction(pilier, hero)      (0x8003AC9C)
    +0x8C = (r + 0x10) & 0x1F ; fin
sinon (ou r == -1, mort depuis C76, le contact est pré-testé) :
    si +0x88 == 1 : StopSoundEffect(0x19)      (0x80049634)
    +0x274 = 0 ; +0x88 = 0
```

L'aide `0x8003AC9C` fait 37 mots :
- `dx = hero.+0x1D8 - pil.+0x1D8`.
- Si `dx < 0`, côté si `hero.W < -dx` ; sinon côté si `pil.W < dx`.
- Hors « côté » (colonnes qui se recouvrent), retour `(hero.Y < pil.Y) << 4`. Sinon retour 8 si `hero.X < pil.X`, sinon `0x18`.

Les largeurs valent `(taille<<16)-1`, donc le contact à ras tombe côté « horizontal ». Directions : 24 = est, 8 = ouest, 0 = sud, 16 = nord.

**Preuves**
- Modèle contre le code réel : 6 000 vecteurs, 0 écart, 82/82 instructions couvertes, 14 classes de sortie.
- 73 mutants d'un mot : 56 tués, 17 survivants, tous équivalents :
  - 10 de cadre de pile (le harnais restaure les registres),
  - 4 `move`→`subu`,
  - 3 sur la constante `-1` du retour de l'aide (code mort depuis C76).
- Goldens dans `G5/c76_goldens.json`, SHA-256 `c80ffbe3def37599c6671f32bf2020153c6b782c235707eceecce09f4ad90d5a`.
- Scripts : `G5/c76_full.py` (sortie `G5/c76_full_out.txt`), listings `G5/dis_c76_raw.txt` et `G5/dis_pushdir.txt`.

La décompilation (`FunctionTypeC.cs:7161`) est fidèle pour la direction. Elle rend la fin par `PlaySoundEffect` au lieu de `StopSoundEffect`.

### 3.2 Poussée, rejouée sur la machine MIPS (MESURÉ)

Le code réel tourne : `MovePlayer`, le spawner de cartes, C76, la physique. Les scénarios P1 à P11 sont dans `G5/g5_scen.py` ; la sortie complète est `G5/g5_scen_all_out.txt`, les tables par tick sont dans `G5/out/`. Deux exécutions donnent des fichiers identiques à l'octet.

- **Délai :** contact au tick N, compteur de 0 à 31, la poussée démarre au tick N+32 (0,64 s).
- **Vitesse :** `Speed 64` donne 0,75 px/tick en X et 0,5 en Y. L'accélération est nulle, donc la force est instantanée.
- **Déplacement :** pixel par pixel, sans grille. Le pilier s'arrête 1 à 2 ticks après le relâchement.
- **Reprise :** un nouvel appui impose de nouveau 32 ticks.
- **Contre un mur :** il s'arrête à ras (murs aux x 168, 192, 216 : pilier à 158, 182, 206). L'animation reste à 1 et le son 25 est demandé à chaque tick tant que le héros pousse.
- **Quatre côtés :** symétriques. La poussée est possible pour un décalage vertical `|dy| ≤ 12` ; à ±14 il n'y a pas de contact.
- **Appui diagonal :** le pilier avance d'environ 30 px, puis le héros glisse.
- **Pose du héros :** `MovePlayer` n'a aucun code de poussée (aucune lecture de `+0x130` dans `0x80031B50`–`0x80033800`). Il garde l'animation de marche (1) sur place. Il n'existe pas de pose de poussée dans `PlayerAnimation`.
- **Plaque `108` et interrupteur `45` au sol :** ils bloquent le pilier (x=156 et x=168). Il ne monte pas dessus.
- **Deux piliers alignés :** pas de chaînage, le second reste immobile.
- **Corniche :** le pilier tombe de z 32 à 0 vers x≈202 et se pose (t143). Le héros marche ensuite sur son sommet.
- **Fosse, cartes réelles :**
  - Carte 26 : le pilier avance de 26 px vers l'est (324→350, mur de la colonne 15) et de 18 px vers le nord (424→406).
  - Carte 347 : sur la rangée 14, les fosses d'une cellule remplies par le pilier donnent (130, 232, z 16) à l'ouest (t144) et (278, 232, z 16) à l'est (t143). Le héros passe dessus à z 48, l'ancien sol (le `support` du héros est le pilier au tick suivant).
- Le pilier sert donc de **marche, de pont et de bouchon de fosse**.

**Interrupteurs et prédicats (FAIT, INFÉRENCE)**
- Dans les 8 cartes de piliers, aucun programme ne nomme un pilier par son id brut, hors le cas de la carte 40 (id de poussoirs, §4).
- Les `0x60` de la 30 (groupes 22 à 25) attendent les quatre objets de plaque (C4), pas le pilier.
- Les interrupteurs piétinés de la 347 emploient `0x3F` (« n'importe quelle entité qui me chevauche », porté en E19.l1). Ils sont posés à z 64, donc plus hauts que le plateau (48) et bloquent le pilier. Le lien pilier et interrupteurs de la 347 reste INCONNU (§8).

### 3.3 Gel du héros après un contact à programme F (MESURÉ)

- **Binaire :** au démarrage d'un programme F (case F du commutateur, `0x80042150`), le binaire met à zéro `+0xD8`, `+0xD4`, `+0xC8`, `+0xC4` du héros.
- **Machine :** j'ai écrit ces quatre mots à la main (scénario P7). Tant que la croix reste enfoncée, le héros est figé (x constant sur 70 ticks).
- **DLL :** elle reproduit le comportement. Pour reprendre la marche, il faut relâcher puis ré-enfoncer.

## 4. DLL au HEAD et prototype

**Existant (FAIT)**
- Registre `NativeSpriteRegistry` : 6 + `PickupAi.Keys` + `ChargedArrowAi.Keys` + `ArrowAi.Keys` clés.
- Mouvement des non-héros par `TickScriptedNpc` (vitesse d'animation, glissement avec `NoObstacleSlide`, support par entité).
- Sonde de mouvement d'E19.d2b : le pilier n'est pas ignoré par `PickupKindMask`, donc il bloque le héros. Le report d'obstacle remplit `XCollisionEntity`.
- `PlaySfx(int)` : plafond `MaxVoices` et table anti-doublon par image.

**Manque (FAIT)**
- `(C,76)`.
- `ISoundPlayer.StopSfx(int)` (seul `StopAllSfx` existe).
- Les opcodes `0x72`, `0xAA`, `0xA9`.
- L'arrêt de son pour `0xBE`.

**Prototype (PROTOTYPE).** Source : `G5/prototype_PillarAi.cs.txt` et `G5/prototype_registry.diff`. Le champ `+0x274` est tenu par `Bytes[0]`, avec les trois autres octets à 0. Il est exécuté sur la vraie DLL, dans une copie HEAD construite sous `G5/probe/`, via `G5/probe_G5ProbeTests.cs.txt` et les cas `G5/cases*.json`. Les sorties sont dans `G5/probe_out/` (HEAD pur) et `G5/probe_out_proto/` (prototype).

- **Carte 26, vers l'est :** contact à l'image 2, animation 1 à l'image 34, premier déplacement à l'image 35. La machine déplace à t34 : c'est la latence d'un tick de D-E19-13. Fin identique : pilier 350, héros 329.
- **Carte 26, vers le nord :** fin à 406, identique.
- **Carte 347, blocages du scénario retirés (puzzle ouvert à la main) :**
  - La chute commence à x 274,5, comme la machine, avec 1 image de retard.
  - Atterrissage en (130, 232, 16) à l'ouest et en (278, 232, 16) à l'est : même état final que la machine.
  - L'atterrissage arrive 2 à 3 ticks après la machine (images 146 contre t144 et t143, intégrateur de gravité de la DLL). Le héros traverse la fosse sur le pilier.
- **Non joué :** la poussée vers le sud sur la DLL (symétrique, non exécutée).

**Épingles à amender.** `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:58` (la formule de décompte) prendra un `+ PillarAi.Keys.Count`. `ArrowAiTests.cs:186` emploie le type 430 comme valeur neutre sur des proxies nus, sans effet attendu. L'arc TN-3 de la 346 contient un pilier, mais le héros y reste garé, donc rien ne devrait bouger (à relancer à l'implémentation). Aucun test existant ne cite les quatre familles.

## 5. Statues, blocs et poussoirs scriptés

**Opcodes employés (FAIT, `G5/progs_g5.txt`, `G5/opcode_hist_g5.json`).** Toutes les familles n'emploient que des opcodes déjà dispatchés par la DLL, hors `0x72` et `0xAA` du poussoir. Les opcodes `0x20`, `0x22`, `0x23` sont dans les programmes des blocs 100 et 102, pas des statues, et sont portés.

**Joué au HEAD (MESURÉ, aucun opcode sauté sauf mention)**

| Cas | Résultat |
|---|---|
| Statue de la carte 2 | Contact à l'image 2, le programme F pose le drapeau, la statue marche à 1,5 px/tick de 420 à 445,5 (le script demande 24 px, le dernier pas dépasse). |
| Statue de la 352 | 204 → 229,5, avec la zone de départ et la touche de croix. |
| Bloc de la 23 | Contact en 31, il marche 48 px vers l'ouest à 2,06 px/tick (768 → 720), puis tombe de z 32 à 16. |
| Poussoir de la 40 | Il bouge de 0,5 px seulement et `0x72` est sauté. |

**Pourquoi le poussoir de la 40 est défaillant.** Le programme C reprend l'animation et la direction sauvegardées par `0x72` (`Last*`). Sans cet opcode, le bouton ne coulisse pas des 7,5 px de 10 ticks prévus. (`0x72` = recopier `TargetAnimationId` et `TargetDirection` dans leurs champs `Last*`, 1 octet.)

**Qui est le poussoir, ce qui le déclenche (FAIT pour les programmes, INFÉRENCE pour le sens de jeu).** C'est un bouton-bloc qui coulisse.
- **Carte 40 :** le contact appelle F (son 18, face au joueur puis demi-tour, animation 1). C attend 10 ticks et repasse à l'animation 0. Les programmes B (cartes de zone) de la 40 testent la présence des enregistrements 3 à 7 dans des tuiles but : `B[2]` teste (10,56) et `B[3]` teste (41,55), (43,55), (45,55), (47,55).
- **Carte 426 :** C attend un drapeau, puis boucle au plus 40 ticks tant que `0xAA [129]` est vrai (le héros pousse le bouton), branche selon la direction du héros (`0x57`), pose un drapeau, fait coulisser le bouton de 80 px (`0x1E`) et rejoue un son. Les quatre boutons sans programme sont inertes.

**Blocs à coup (102, 382).** D appelle `0x87 [128,1]`, soit l'attaque d'indice 1 (nibble 2 : marteau ou fléau). C déplace ensuite le bloc selon la zone du joueur. Ils dépendent donc de l'attaque au fléau, pas de la poussée.

**Le son (FAIT, binaire).** Les scripts font `0xBD [25]`, la marche, puis `0xBE [25]`.
- Le binaire fait `PlaySoundEffect` pour `0xBD` (`0x80041C38`) et **`StopSoundEffect` pour `0xBE`** (`0x80041C6C` → `0x80049634`).
- La DLL joue le son 25 deux fois. Sites : 392 `0xBE` sur 119 cartes, dont 204 pour l'id 25 ; 302 paires `0xBD`/`0xBE` ; 12 sites sur la chaîne (`G5/op_be_sites_out.txt`).
- `PlaySound2Bis_0xBE_…` (`EventProgramRunnerTests.cs:3182`) épingle le comportement actuel.

## 6. Cartes, saisies et sauvegardes

Cartes concernées (23, aucune dans la chaîne) : pilier 26, 30, 341, 343, 344, 346, 347, 423 ; statue 2, 37, 51, 102, 115, 117, 149, 152, 351, 352, 382 ; bloc 23, 100, 102, 382 ; poussoir 40, 426.

Sauvegardes de recette dans `tools/Saves/` (arrivée sur la carte, caméra au point de sauvegarde) :
- Pilier : `92-ancien-sanctuaire-bug-pillier.sav` (26), `231-torla.sav` (423, piliers inertes), `353-lake-castle-bug.sav` et `356-lake-castle.sav` (344), `355-lake-castle.sav` (346), `361-after-melzas-2.sav` (347).
- Statue : `280-murgg.sav` et `murgg-before-bug.sav` (2), `281-murgg.sav` (382), `351-lake-castle.sav` (352).
- Bloc : `140-giless-dream.sav` (100), `281-murgg.sav` (382).
- Poussoir : `234-torla.sav` (426).
- Sans sauvegarde : 30, 341, 343, 37, 51, 102, 115, 117, 149, 152, 351, 23, 40.

## 7. Découpage proposé et dépendances

**E20.g5a, pilier (C76).** Indépendante de g1a à g4b (pas de code partagé avec le soulèvement ni le lancer). Elle ne touche que la clé du registre et le son.
- Phase A : fonctions pures (`PushDirection`, une étape de compteur) contre les 14 goldens, rouges d'abord, puis mutants.
- Phase B : enregistrement `(C,76)` pour 430 et épingles. Tests de monde avec les valeurs de la machine, où départ +1 tick et atterrissage +2/+3 sont épinglés sur les valeurs mesurées de la DLL :
  - délai de 32 ticks,
  - 0,75/0,5 px/tick,
  - arrêts aux murs de la 26 (350 et 406),
  - fosse de la 347,
  - plaque/interrupteur bloquants,
  - deux piliers non chaînés,
  - décor de la 423.
- Phase C : arcs sur cartes réelles. Pour la 347, retirer les entités bloquantes (rec 6 et 11, par écriture du harnais) et laisser un test sans retrait.
- Recette : sauvegarde 92 et, plus tard, celles du Lake Shrine.

**E20.g5b, opcodes `0x72` et `0xAA` (et `0xA9`).** Quelques lignes. Aucun site sur la chaîne (`G5/op_sites_all_out.txt`). Exige une décision de propriété avec le thème 3. Elle rétablit le poussoir de la 40 (coulisse de 7,5 px) et prépare la 426.

**E20.g5c, arrêt du son (`0xBE` = Stop, `ISoundPlayer.StopSfx`).** Corrige un défaut de décompilation. Elle amende l'épingle `PlaySound2Bis_0xBE_…` et peut changer des traces audio d'arcs sur 12 sites de la chaîne. Le pilier réclame ce même arrêt.

**E20.g5d, couverture des événementiels.**
- Arcs : statue (2 et 352), bloc 23, poussoir 40, poussoir 426 après g5b.
- Test statique « aucun opcode sauté dans les programmes des quatre familles ».
- Épingle du gel F.
- Les blocs 102 et 382 attendent le fléau (thème 2).

**Dépendances**
- sm1 n'est pas requis.
- D5b (riders non-héros) ne sert que si un pilier doit un jour presser un interrupteur (INCONNU, §8).
- Le thème 3 recouvre `0x72`, `0xAA`, `0xA9` et `0xBE` ; g5b et g5c peuvent en retirer les sites de son inventaire.
- Conflit de fusion probable sur `NativeSpriteRegistry.CreateForProduction` et l'épingle de décompte avec f5, g1a, g2c1 qui ajoutent aussi des clés.

## 8. Questions

**Pour le plan (défaut proposé)**
- **Q-G5-1** Découpage a/b/c/d ci-dessus. Défaut : a, b, d dans le thème 1 ; c en tranche séparée soumise à l'auteur.
- **Q-G5-2** Son du pilier. Défaut : c d'abord, puis le pilier appelle `StopSfx`. Sinon il laisse le son finir (écart documenté).
- **Q-G5-3** Épingler les valeurs de la DLL (latence +1 au départ, +2/+3 à l'atterrissage) plutôt que celles de la machine. Défaut : oui.
- **Q-G5-4** Piliers inertes de la 423 : reproduire l'inertie. Défaut : oui.
- **Q-G5-5** Gel du héros après un programme F : le garder, car le correctif toucherait toute interaction F.
- **Q-G5-6** Blocs 102 et 382 : un test synthétique en g5d, la recette au thème 2.
- **Q-G5-7** Ordre : g5a juste après f5 fusionné, une clé de plus dans la formule via `PillarAi.Keys`.

**Pour l'auteur, en termes de jeu**
- **A1** Le raclement de pierre des statues, blocs, boutons et piliers doit s'arrêter quand le mouvement finit (l'original fait ainsi). Au HEAD il est rejoué à la fin. Le corriger maintenant coûte une petite tranche, touche 119 cartes (12 passages de la chaîne) et change des tests audio. Sinon le défaut reste.
- **A2** Après avoir touché une statue, un bloc ou un bouton « à événement », le héros s'arrête net. Il ne repart que si on relâche puis ré-enfonce la direction. C'est le comportement mesuré de l'original, et il vaut pour toute interaction par contact, pas seulement ces objets. Le garder tel quel (fidèle) ou le corriger (règle « un bug avéré se corrige ») : le corriger change le comportement de toutes les interactions par contact.
- **A3** Pour info : un pilier exige 0,64 s de poussée continue avant de bouger. Il avance au pixel, il s'arrête quand on lâche, il tombe d'une corniche et sert de pont, et on ne le pousse pas sur une plaque posée au sol. Douze des piliers de la 423 sont immobiles dans l'original et le resteront. Les recettes sont en attente, faute de pouvoir les jouer.

## 9. Inconnus (INFÉRENCE ou à établir)

- Le but exact des piliers dans chaque carte : seules la 347 (fosses) et la 346 (marche vers une terrasse) sont comprises. Pour la 26, il reste une poche de 26 × 18 px. Aucun pilier ne gère la 30 ni la 341 en jeu.
- Les interrupteurs de la 347 (z 64, `0x3F`) : peuvent-ils être pressés par un pilier ? La suite des programmes d'ouverture n'est pas rejouée (la machine n'exécute pas ces scripts).
- La durée du son 25 et son `MaxVoices` : une demande à chaque tick est-elle audible ? À juger en recette.
- Les puzzles à séquence des cartes 40 et 426 : les drapeaux d'ouverture n'ont pas été joués.
- Le décalage de 2 à 3 ticks à l'atterrissage a pour cause probable l'intégrateur de gravité de la DLL : INFÉRENCE.
- Le sens du pilier non testé sur la DLL (sud).
- Limite de la machine : elle exécute le code réel du héros et de C76, mais pas les programmes d'événements.

## 10. Fichiers (tous sous `G5/`)

- **Recensement :**
  - `census_g5.py`, `.txt`, `.json`
  - `census_signature.txt`
  - `census_export.py`, `census_export_out.txt`
  - `sensors_g5.py`, `sensors_g5.txt`
  - `pillar_context.py`, `pillar_context.txt`
  - `pillar_refs.py`, `pillar_refs_out.txt`
  - `progs_g5.py`, `progs_g5.txt`, `opcode_hist_g5.json`
  - `op_sites_all.py`, `op_sites_all_out.txt`
  - `op_be_sites.py`, `op_be_sites_out.txt`
- **Binaire :**
  - `c76_full.py`, `c76_full_out.txt`, `c76_goldens.json`
  - `dis_c76_raw.txt`, `dis_pushdir.txt`, `dis_progstart.txt`, `dis_moveplayer.txt`
- **Machine :**
  - `g5_scen.py`, `g5_scen_all_out.txt`
  - `out/` (tables par tick)
  - `c4_scen.py`, `c4_full.py`
  - `work/` (la machine copiée de g1a)
- **DLL :**
  - `probe/` (copie construite de HEAD, avec `PillarAi.cs` prototype)
  - `probe_G5ProbeTests.cs.txt`
  - `prototype_PillarAi.cs.txt`, `prototype_registry.diff`
  - `cases1.json` à `cases11.json`
  - `probe_out/` (HEAD pur), `probe_out_proto/` (prototype)
  - `probe_build.log`, `make_probe.ps1`
- **Divers :** `mapgrid.py`, `dump_map.py`, `dump_prog.py`, `disasm_range.py`, `MANIFEST.sha256`.

**Reproduire**
- Machine : `python -B -X utf8 g5_scen.py`, puis `python -B -X utf8 c76_full.py 6000 mut gold`. Sans `-I` : capstone est dans le site-packages utilisateur.
- DLL : dans `G5/probe/`, `dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug`. Puis `G5_CASES=<cases.json> G5_OUT=<dossier> dotnet test --no-build --filter FullyQualifiedName~G5ProbeTests`.
- Ne jamais supprimer `probe/` récursivement : ses dossiers `alundra-project/` sont de vraies copies (sans `Musics`).