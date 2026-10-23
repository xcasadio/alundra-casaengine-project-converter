# Rapport de découverte E20.g5b : les opcodes `0x72`, `0xAA` et `0xA9` (lecture seule)

**Provenance.**
- Dépôt lu à `e49cce2b`. HEAD est ensuite passé à `2eff6b46`, avec des commits de docs seulement : `git diff --stat e49cce2b HEAD -- Alundra Alundra.Tests` est vide.
- `ALUN_CD.EXE` (France) : SHA-1 `a705f8da…` vérifié.
- Je n'ai rien construit, testé, modifié ni indexé dans le dépôt. Les changements visibles dans `git status` (`NativeSpriteRegistry.cs`, `PillarAi.cs`) sont ceux de l'executor.
- Tout mon travail est dans `G5B/` = `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g5b-disc/`.
- Étiquettes : FAIT (lu), MESURÉ (exécuté sur la machine MIPS ou sur la vraie DLL dans la copie), PROTOTYPE (patch jetable de la copie), INFÉRENCE.

## 1. Verdicts

1. **Les trois gestionnaires sont décodés et rejoués.**
   - La décompilation est fidèle pour les trois : mêmes champs, mêmes largeurs, mêmes tailles.
   - Le modèle égale le code réel sur 9 000 vecteurs, sans écart (§2, §3).
2. **Les trois opcodes ont déjà des tranches READY au thème 3, avec leurs preuves.** C'est le point qui change le plan. Ma lecture confirme ces tranches ; g5b doit les reprendre, pas les refaire (§7).
   - `E19.sm3` (§1.4.4 de `docs/plan-e19-opcodes.md`) prend `0xA9` et `0xAA`. Annexe : `docs/plan-e19-sm-annexe/sm3/`, avec 837 vecteurs, 56 mutants, tests U-A9-01 à 15 et U-AA-01 à 12, et des arcs.
   - `E19.sm4` (§1.4.5) prend `0x72`. Annexe : `docs/plan-e19-sm-annexe/sm4/`, avec les tests U72-1 à 6, ARC-40 et 25 mutants.
   - Leurs règles R1 et R2 (`ReferenceEquals`, Result écrit sur toutes les voies, taille 2, entité logique) sont exactement ce que je trouve.
3. **`0xAA` ne sert pas qu'aux boutons.**
   - Sur ses 22 sites, 4 seulement sont les boutons de la 426.
   - 15 sont les blocs « Mini-Nilude (D12) » (type 433, M177), poussés dans les cartes 421 et 423 à 426 : cinquième famille de poussables, absente du recensement de g5.
   - 2 sont des bulles de la grotte des fées, 1 est le bloc de la 440.
4. **Aucun site sur les 30 cartes de la chaîne d'histoire.** L'intersection est vide, et `Alundra.Tests/Data/story-chain-skipped-opcodes.tsv` n'a que son en-tête.
5. **État DLL au HEAD.**
   - Les trois opcodes sont sautés par leur taille, journalisés une fois par valeur d'opcode, et `Result` reste périmé.
   - Aucun test ne les nomme.
   - MESURÉ : porter les trois (prototype) ne déplace aucun test existant. Sur 5 435 tests, 5 426 passent et 9 échouent avant comme après (musique : la copie n'a pas `Musics`).
6. **Mesures de la sonde (PROTOTYPE).** Elles corrigent le texte de la tâche (détail au §6) :
   - Carte 40 : le bloc glisse de 1,5 à 5,5 px, toujours vers le sud. Ce n'est pas 7,5 px, et la longueur dépend de l'instant du contact.
   - Carte 426 : il faut 40 ticks de poussée consécutifs (pas « au plus 40 »). Le bouton glisse alors de 80 px en 160 ticks.
7. **`0xA9` relève de la même famille** que `0xAA` : jumeau du gestionnaire, même recherche, même montage de test. Je propose de le garder avec `0xAA` dans g5b. Ses 4 sites sont tous sur la 440 (Meia Dream) et aucun poussable n'en a besoin.

## 2. Binaire (FAIT)

Table : `0x80098FAC + 4 × opcode`, script `G5B/tab.py`, sortie `G5B/tab_out.txt`.

| Opcode | Gestionnaire (slot de table) | Mots | Taille instr. | Lit | Écrit | Retour |
|---|---|---|---|---|---|---|
| `0x72` | `0x8003FA24` (`0x80099174`) | 6 | 1 | logique+`0x88`, +`0x8C` (mots 32 bits) | logique+`0x26C` ← +`0x88` ; +`0x270` ← +`0x8C` | 1 |
| `0xA9` | `0x80041174` (`0x80099250`) | 35 | 2 | opérande `v1` (`lbu`), tampon `0x8013D8D8`, logique+`0x130` | état+`0x2C` (Result) | 2 |
| `0xAA` | `0x80041200` (`0x80099254`) | 36 | 2 | opérande `v1`, tampon, tampon[i]+`0x130` | état+`0x2C` | 2 |

**Convention d'appel** (boucle `0x80042280-0x8004233C`, listing `G5B/dis_dispatch.txt`) :
- `a0` = entité logique, relue avant chaque instruction (`0x80042284`).
- `a1` = propriétaire.
- `a2` = adresse du mot qui porte le pointeur d'instruction (le gestionnaire lit son octet d'opérande à travers lui).
- `a3` = état du programme.
- `v0` = longueur. Zéro suspend l'appel ; sinon l'état+8 est remis à 0 et le PC avance de `v0`.

**Corps décodés.**
- `0x72` : recopie anim et direction cibles dans les champs « derniers », sans toucher `Result`. Aucun appel, aucune branche, ne retourne jamais 0.
- `0xA9` et `0xAA` : appellent la recherche `0x8003C954(a0, v1)`, qui remplit le tampon global et rend le compte `n`. Puis, sur le tampon parcouru de `n-1` à 0 :
  - `0xA9` : `Result = 1` si `logique.+0x130` est l'une des entités ; sinon 0.
  - `0xAA` : `Result = 1` si l'une des entités a `+0x130 == logique` ; sinon 0.
  - `Result` est écrit sur toutes les voies, jamais périmé.
- Avec `v1 = 129` (`0x81`, fonction 1 « le joueur », `0x8003CA38`) : le tampon est le héros seul (`0x80127D30`). `0xAA [129]` signifie « le héros est bloqué en X par moi », c'est le prédicat de poussée.
- `0xA9` et `0xAA` sont jumeaux : même squelette, comparaison inversée.
- Annotations : `G5B/dis_handlers_annotated.txt` ; brut : `G5B/dis_handlers_raw.txt`.

**Comparaison avec la décompilation** (`EntityEventHandlers.cs:2175, 3159, 3182`) :
- Les trois gestionnaires : aucun écart de comportement. La boucle arrière du binaire est aussi celle de `--count`.
- Écart nommé dans l'aide de recherche, hors de tout site : les types `0x82` et `0x83` parcourent `0..N` et `1..N` **inclus**, alors que la décompilation (`GameEngine.cs:1965-1988`) fait `i < N`. Sur mes vecteurs, 523 sur 766 diffèrent. La DLL boucle sur toute la liste, donc suit le binaire.
- La recherche par identifiant brut teste l'état du **propriétaire**, pas celui de l'entité. Décompilation et DLL le font déjà.
- `+0x130` n'est écrit que par le déplacement d'entité `0x80037E34` (scan : `G5B/scan130_out.txt`) :
  - `0x80037E80` l'efface si l'entité est portée.
  - `0x80037F08` y met l'obstacle du tick.
  - Un script voit donc le contact du tick précédent (confirmé par `sm3/report.md` §4.4).

## 3. Modèle rejoué, couverture, mutants, goldens (MESURÉ)

Script `G5B/ops_full.py`, sortie `G5B/ops_full_out.txt`.
- **Couche A (gestionnaires)** : 9 000 vecteurs (3 000 par opcode), les 256 valeurs d'opérande tirées. 0 écart sur la mémoire observable et sur la longueur rendue.
- **Couche B (recherche)** : 8 899 vecteurs sur les types modélisés (id brut, `0x80`, `0x81`, `0x82`, `0x83`, `0x87`, `0x88`, ids illégaux). 0 écart, journaux `CheckEntityRecord` et logueur d'erreur compris.
- **Couverture des gestionnaires** : 6/6, 35/35, 36/36 mots. Dans l'aide, 15 mots de 371 ne sont pas exécutés (cases 4, 5, 6, 10, 11 partielles).
- **Mutants d'un mot** : 78, dont 72 tués. Les 6 survivants sont équivalents (`move rd,rs` devenu `subu rd,rs,$zero`).
  - Les mutants de cadre de pile sont tués parce que mon appel strict contrôle `s0-s7`, `gp`, `sp`, `fp` et vide la pile avant chaque appel.
- **Goldens** : `G5B/ops_goldens.json`, 70 vecteurs pour 70 classes, SHA-256 `c4c75a9146eaa239e8d8b3cb38cb4a2c3da82b6f369f080d0a6acc5ae0027dba`. Deux exécutions donnent le même fichier à l'octet.

## 4. Sites dans les données (FAIT)

Script `G5B/sites.py` (balayage linéaire de chaque programme), sorties `G5B/sites_all.txt` (5 instructions avant et après, placements qui exécutent le programme) et `G5B/sites_all.json`. Regroupement : `G5B/families.txt`.

**`0x72`** : 24 sites linéaires, 20 atteignables, sur 12 cartes (40, 51, 53, 54, 139, 140, 159, 160, 199, 394, 444, 446). Les 4 autres sont `D[7]`, `D[9]`, `D[11]`, `D[13]` de la 160, sans placement qui les référence. Tous les utilisateurs ont un programme C scripté, sauf les 10 chaînes de la 160 (C natif, donc sans effet réel).

| Contexte | Sites | Placements | Séquence |
|---|---|---|---|
| Blocs glissants M89 (carte 40, F) | 4 | 8 | `0xBD 18`, `0x27`, `0x0A`, `0x1A [1]`, `0x3A [0]`, **`0x72`**, `0xFF` |
| PNJ en dialogue : Meia M144 (51, 53, 54), Jess M4 (199), Phineas M16 et Yuri M27 (394), en F | 6 | 6 | `0x27`, `0x0D` ou `0x5C`, **`0x72`** |
| Flamme M63 (139, 140), D | 3 | 3 | `0x27`, `0x0A`, son, **`0x72`** |
| Bulle M141 (159, 160), D | 2 | 2 | `0x1A [2]`, `0x63`, `0x2B`, **`0x72`** |
| Chaîne M147 (160), D | 1 (+4 non référencés) | 10 | **`0x72`**, `0x63`, `0x1A [1]`, `0x5E` |
| Interrupteur d'allumage M51 (444, 446), D | 4 | 16 | `0x87` (type de coup 6 ou 7), `0x2B`, `0x09`, **`0x72`**, `0x02` |

**`0xAA`** : 22 sites, 8 cartes (159, 160, 421, 423, 424, 425, 426, 440).

| Famille | Sites | Placements | Forme |
|---|---|---|---|
| Mini-Nilude M177 (type 433), cartes 421, 423, 424, 425, 426 | 15 | 28 | `0x73 [40]`, `0xAA [129]`, `0x04` ou `0x4B`, `0x74`, `0x57`, puis marche (24 ou 48 px) |
| Bouton M89 (426) | 4 | 4 | idem, avec attente de drapeau, marche de 80 px |
| Bulle M141 (159, 160) | 2 | 2 | compteur de 60, pose le drapeau `0x8000` tant que le héros pousse |
| Bloc transparent G25 (440) | 1 | 3 | sans compteur ; `C[2]`, `C[3]`, `C[4]` partagent `@1140` |

**`0xA9`** : 4 sites, tous sur la 440.
- `B[9]` : bouton `0x2F` puis `0xA9 [24]` (id brut 24 = Jeune Meia).
- `C[16]`, `C[18]`, `C[21]` : `0xA9 [129]` pour la Jeune Meia (M93), qui bute sur le héros.

**Cartes de la chaîne :** aucune.

**Sauvegardes de recette** (`tools/Saves`) :

| Cartes | Sauvegardes |
|---|---|
| 426 | `234-torla.sav` |
| 421 | `235-torla.sav` |
| 423 | `231-torla.sav` |
| 425 | `232-torla.sav` |
| 159 | `241-mare-enchantee.sav`, `242-mare-enchantee.sav` |
| 440 | `180-meiadream-bug.sav` |
| 140 | `211-manor-of-fire.sav`, `212-after-manor-of-fire.sav` |
| 53, 54 | `260-elenes-dream.sav` ; `261-elenes-dream.sav`, `262-elenes-dream.sav` |
| 199 | `80-after-bonaires-dream.sav` |
| 394 | `110-after-ancien-sanctuaire.sav`, `casino.sav` |
| 444, 446 | `300-mont-torla.sav`, `301-mont-torla.sav` |
| **Sans sauvegarde** | 40, 51, 139, 160, 424 |

La `234-torla.sav` arrive sur la 426 avec les cinématiques `B[2]` et `B[3]` faites et les boutons non touchés.
- Elle porte les drapeaux 1253, 1254, 1267 et 1268 posés, et 1263 à 1266 et 1269 à zéro.
- Le héros arrive en haut de la carte (tuile caméra 7,35).
- En traversant les tuiles x 4 à 10, y 47 à 50, il déclenche la cinématique `B[4]`.

## 5. État de la DLL au HEAD (FAIT)

- **Dispatch.** `Alundra/Events/EventProgramRunner.cs:2110-2111` envoie ces opcodes à `UnknownOpcode` (`:3073`) : saut par la taille de `EventOpcodeSizeTable` (1, 2, 2).
- **Journal et trace.** Un avertissement par valeur d'opcode (`Logs.WriteWarning`) et la trace `EventTraceKind.UnknownSkipped`. Il n'y a aucun compteur de production.
- **`Result`.** Le saut ne touche pas `Result`. Au HEAD il vaut le plus souvent 0, d'où des boucles inertes (`0x04 → recharge du compteur`).
- **Libellés.** `0xA9` et `0xAA` n'ont pas de nom dans la table (`""`, lignes 217 et 218).
- **Champs déjà là :**
  - `EntityScriptProxy.TargetAnimationId` et `TargetDirection` (`:158-159`).
  - `LastTargetAnimationId` et `LastTargetDirection` (`:1225-1226`).
  - `XCollisionEntity` (`:364`, écrit par `MoveControllerAndPullPosition`, `:2424-2431`).
- **Sauvegarde et restauration des champs « derniers » :** déjà portées, `EventProgramRunner.cs:210-211` (reprise de C) et `:236-237` (départ de A, D, E, F).
- **Recherche et logique.** `EntitySearchService` fait déjà les 12 fonctions ; l'entité logique est calculée en `:431`.
- **Tests qui épinglent l'état :**
  - Aucun ne nomme `0x72`, `0xA9` ou `0xAA` (grep sur `Alundra.Tests`).
  - `StoryChainSkippedOpcodesTests` ne vise que la chaîne ; son oracle `IsPorted` est dérivé du vrai runner, donc un opcode porté sort seul.
  - `UnknownOpcode_KnownSize_SkipsBySize` utilise `0xB3`.
  - Le miroir `ImplementedOpcodes` (`Alundra.Tests/Arcs/HeadlessIntroSimulation.cs:85`) ne sert qu'à étiqueter ; il faudra y ajouter les trois.
- **Aucun test existant ne bouge** (MESURÉ). Suite complète `Alundra.Tests` dans la copie :
  - HEAD pur : 5 435 tests, 5 426 passent, 9 échouent (`MusicPlayerTests` ×2, `WarpDepartureTests` ×7).
  - Avec le prototype : 5 435 tests, 5 426 passent, mêmes 9 échecs, aucune différence.
  - Comparaison : `G5B/cmp_trx_out.txt`.
  - Les 9 échecs sont dus à l'absence de `Musics` dans la copie.
- Piège d'environnement : la suite ne passe dans une copie que si `docs/` et `tools/` sont présents (par exemple `BirthClockOracle` lit `docs/plan-e20-annexe/e20-e0a5/oracle/out/birth_clock.json`).

## 6. Ce que montrent la 40 et la 426 (MESURÉ au HEAD, PROTOTYPE après)

**Prototype** (`G5B/prototype_ops.diff`, installé dans la copie `G5B/probe/`) : trois `case` de 30 lignes dans `Dispatch`, avant `case 0x86`. Aucun test n'a été ajouté. Cas : `G5B/cases_a.json`, `cases_d.json`, `cases_phase.json` ; sorties `G5B/probe_out_head/` et `G5B/probe_out_proto/` ; lecteurs `G5B/summ.py`, `G5B/series.py`.

**Carte 40, bloc rec 0 en (108,840,16).**

| Approche | HEAD | Prototype |
|---|---|---|
| Nord (héros en (108,818), tient Bas) | contact f5 (`xc=0`), F en f6 (anim 1), f7 anim 0 restaurée et **0,5 px**, fin | f6 à f16 : +0,5 px par image pendant 10 images = **5,0 px** vers le sud |
| Est (héros en (144,840), tient Gauche) | contact f7, F en f16, 0,5 px | F en f16, 11 images = **5,5 px** |
| Sud (héros en (108,862), tient Haut) | le bloc pousse 0,5 vers le héros et est bloqué (`xc=-1`), 0 px | 0 px (le héros est dans le chemin) |

- Le sens est **toujours le sud** : `0x3A [0]` écrase le « demi-tour » du programme F (table cardinale `{0, 0x10, 8, 0x18}`).
- La glissade dépend de l'instant du contact (`G5B/phase40_summary.txt`) : contact en f5, 5,0 px ; f6, 4,5 ; f7, 4,0 ; f8, 3,5 ; f9, 3,0 ; f10, 2,5 ; f11, 2,0 ; f12, 1,5.
  - Raison (INFÉRENCE de lecture du script) : le programme C boucle sur `0x2A`, `Wait 10`, `0x1A [0]`, `Goto`. Le programme F n'interrompt pas cette attente, donc la glissade est le reste de l'attente en cours.
  - Le texte de la tâche donne « 7,5 px en 10 ticks » : c'est la vitesse en X (0,75 px/tick). Ici la poussée est verticale à 0,5 px/tick, donc 10 ticks au maximum = 5 px.
- Le héros reste figé après F, avant comme après (question A2 de g5).

**Carte 426, bouton rec 17 en (252,824,16), héros au nord en (252,790), flags 1253, 1254, 1267, 1269.**

| Cas | HEAD | Prototype |
|---|---|---|
| Nord, tient Bas 130 images | contact f12 (`xc=17`) tenu ; `0xAA` sauté 317 fois ; le compteur est rechargé à chaque tick ; **rien ne bouge** | `0x74` ×40 ; `0x57` pris en f52 ; anim 1 en f53 ; glisse de 0,5 px/image de y 824 à 904 (**80 px, 160 images**) ; anim 0 en f214 ; drapeau 1266 posé |
| Nord court (30 pressées, 20 relâchées, 30 pressées) | rien | rien : le compte repart à zéro |
| Ouest, tient Droite | rien | rien : `0x57` renvoie au rechargement du compteur |

- Déclenchement : 40 vrais `0xAA` consécutifs, soit le premier contact + 40 ticks. Correction du texte de la tâche (« au plus 40 ») : il faut **au moins** 40 ticks continus, du côté nord seulement (l'entrée 0 de la table de `0x57`).
- Trace : `G5B/trace_426_proto_key.txt`. Dès le drapeau 1266, `B[6]` fait perdre le contrôle au héros pendant 123 ticks et pose 4 tuiles de pont (`0x85`), un pas tous les 41 ticks.
- Chaîne confirmée : chaque bouton exige le drapeau du précédent.
  - Ordre de jeu : rec 17 (x=252), puis rec 16 (x=204, drapeau 1266), rec 15 (x=156, drapeau 1265), rec 14 (x=108, drapeau 1264).
  - Sans le drapeau 1266, rec 16 ne réagit pas.
  - Chacun se déclenche 40 images après le contact.
- Le son 25 est joué au départ puis rejoué à la fin (`0xBE` = rejouer dans la DLL, défaut de g5c).

**Non mesuré.** Les blocs M177 : mes sondes en 421 et 424 ont placé le héros à `z=48` ou `z=64`, sans contact (INCONNU : la bonne hauteur de départ à trouver en T0). Les bulles 159 et 160, la 440 et les PNJ : non joués.

## 7. Découpage proposé pour g5b

**Recommandation (défaut, Q-G5B-1).** Faire de g5b un prélèvement des trois opcodes sur sm3 et sm4, en réutilisant leurs annexes telles quelles.
- Pourquoi : sm3 et sm4 sont déjà marquées « à réviser » dans le plan, et sm0 (la liste fermée du corpus entier) n'est pas exécutée. Le prélèvement ne casse donc rien.
- Conséquence : sm3 perd `0xA9` et `0xAA` (28 lignes de 42), sm4 perd `0x72` (20 lignes de 90). Leurs comptes se recalculent par script (règle R-SM-2).
- Alternative écartée A : refaire une préparation. Elle double sm3 et sm4.
- Alternative écartée B : attendre sm3 et sm4. Les boutons restent inertes jusqu'à sm0 et R-SM-1.

**Tâches :**
- **T0** (docs, aucun code) :
  - Rejouer `ops_full.py` et les vecteurs de sm3 (837) et sm4 (370 et 21) depuis des copies.
  - Rejouer la sonde (`cases_a.json`) sur le HEAD de départ.
  - Fixer pour la 421 et la 424 la hauteur de départ du héros devant un bloc M177.
- **T1** (tests, rouges d'abord) :
  - `U72-1` à `6`, `U-A9-01` à `15`, `U-AA-01` à `12`, `U-ALL-01` à `03` de sm3 et sm4.
  - Une `[Theory]` sur les octets réels (20 instructions `0x72`, 22 `0xAA`, 4 `0xA9`).
  - Montage nu de R-SM-4.
  - Nom de fichier conforme à l'ADR-0080 (précédent : `WeaponLockOpcodesTests`).
- **T2** (code) :
  - Les trois `case` (une trentaine de lignes), placés après `case 0xB8` pour la paire (règle R0 de sm3) et près de `case 0x71` pour `0x72`.
  - Les libellés de `EventOpcodeSizeTable` (`0xA9`, `0xAA` vides).
  - Le miroir `ImplementedOpcodes`.
- **T3** (mutants, réels, par script, restaurés par `cmp`). Reprendre les 56 de sm3 et les M72a à d de sm4. Ajouter :
  - `0x72` ne copie que l'animation ;
  - champs inversés ;
  - `0xA9` et `0xAA` échangés ;
  - `Result` non remis à 0 quand rien n'est trouvé ;
  - taille 1 au lieu de 2 ;
  - lecture du propriétaire au lieu de l'entité logique.
- **T4** (arcs, valeurs de la copie comme épingles) :
  - Carte 40 : nord, est, sud, avec les trois approches du §6 et la dépendance à la phase.
  - Carte 426 : poussée nord complète, poussée courte, mauvais côté, bouton 16 avec et sans drapeau.
  - Arcs `S3-A1`, `A1b`, `A2`, `A3a/b`, `A7` de sm3 : facultatifs sous R-SM-6, à garder si T0 trouve la hauteur.
- **T5** (documentation) :
  - Réviser le §1.24.6, sm3, sm4 et sm0 (comptes, `ClosedOwners`).
  - Pas d'ADR : le binaire l'emporte, D-E19-2.
- **T6** (recette de l'auteur) :
  - `234-torla.sav` : descendre, subir `B[4]`, pousser le bouton de droite par le nord en maintenant Bas environ 0,7 s.
  - `231-torla.sav`, `232-torla.sav`, `235-torla.sav` pour les blocs.
  - `241-mare-enchantee.sav` et `242-mare-enchantee.sav` pour les bulles.
  - `180-meiadream-bug.sav` pour la 440.

**Dépendances et risques.**
- Indépendant de g5a (registre) et de g5c (`StopSfx`).
- Conflit de fusion probable avec les autres tranches qui touchent `EventProgramRunner.cs` ou `EventOpcodeSizeTable.cs`.
- Le son 25 joué deux fois devient audible en 426 tant que g5c n'est pas fait.

## 8. Questions

**Pour le plan (avec défaut) :**
- **Q-G5B-1.** Prélever `0x72`, `0xA9`, `0xAA` sur sm3 et sm4 et en réutiliser les annexes. Défaut : oui.
- **Q-G5B-2.** Garder `0xA9` avec `0xAA`. Défaut : oui.
- **Q-G5B-3.** Les arcs de sm3 qui dépendent de ces opcodes (421, 424, 440) passent-ils à g5b ? Défaut : facultatifs (R-SM-6), seulement ceux dont T0 trouve la géométrie.
- **Q-G5B-4.** Amender le §1.24.6 :
  - 7,5 px devient « de 1,5 à 5,5 px, vers le sud » ;
  - « au plus 40 ticks » devient « 40 ticks consécutifs » ;
  - le bouton n'est pas le seul client de `0xAA` (ajouter les M177) ;
  - `0x72` : 24 sites linéaires, 20 atteignables.
  Défaut : oui.
- **Q-G5B-5.** Rappeler dans le plan que toute copie de test a besoin de `docs/` et `tools/`. Défaut : oui.

**Pour l'auteur (en termes de jeu, avec défaut) :**
- **Q-G5B-A1.** Dans le repaire de Nirude (carte 40), les blocs glissent seuls d'environ 1,5 à 5 pixels, toujours vers le bas de l'écran, d'où qu'on les touche. Ils ne bougent pas si on les touche par le bas. La longueur dépend du moment précis du contact, et le héros reste figé un instant. C'est ce que dit le script de l'original. Défaut : le reproduire tel quel.
- **Q-G5B-A2.** À Torla (carte 426), les quatre boutons exigent une poussée continue de 40 images (environ 0,7 s) du côté nord, dans l'ordre de la droite vers la gauche. Si on lâche avant, le compte repart. Le bouton glisse ensuite seul de 80 px (environ 2,7 s), puis le pont se construit. Défaut : garder.
- **Q-G5B-A3.** La même correction débloque aussi :
  - les blocs de Nirude (28 emplacements) ;
  - les bulles de la grotte des fées ;
  - le rêve de Meia ;
  - les PNJ (Meia, Jess, Phineas, Yuri) qui restent tournés vers vous après un dialogue ;
  - les flammes et les interrupteurs d'allumage de Torla.
  Défaut : une ligne de recette par sauvegarde disponible (§4), sans cible précise sur les cartes 40, 51, 139, 160 et 424 qui n'ont pas de sauvegarde.
- **Q-G5B-A4.** Le raclement (son 25) est rejoué à la fin de chaque glissade. C'est la question A1 de g5, toujours en attente. Défaut : g5b d'abord.

## 9. Inconnus

- Les blocs M177 en 421 et 424 : le contact du héros avec le bloc dans la DLL (hauteur) n'est pas établi (§6).
- Les effets de `0x72` sur les PNJ en dialogue et sur les D de contact (flammes, bulles, interrupteurs d'allumage) : INFÉRENCE de lecture du script, non joués.
- La variation de glissade de la 40 existe-t-elle à l'identique dans l'original : INFÉRENCE (même script, même chaîne F puis reprise de C).
- Un décalage d'un tick possible entre le binaire et la DLL pour le comptage de 40 (le contact du tick précédent, D-E19-13) : non mesuré sur le binaire.
- `XCollisionEntity` des PNJ de la 440 quand ils marchent vers le héros (P5 de sm3) : non mesuré.
- Le catalogue de contacts « entités sans contrôleur » : non regardé.

## 10. Fichiers (tous sous `G5B/`)

Chemins complets (même préfixe `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/g5b-disc/`) :
- Binaire :
  - `tab.py`, `tab_out.txt`
  - `dis_handlers_raw.txt`, `dis_handlers_annotated.txt`
  - `dis_search_head.txt`, `dis_search_full.txt`, `dis_dispatch.txt`
  - `scan130.py`, `scan130_out.txt`
  - `disasm_range.py`
- Modèle : `ops_full.py`, `ops_full_out.txt`, `ops_goldens.json`.
- Sites :
  - `sites.py`, `sites_all.txt`, `sites_all.json`
  - `families.py`, `families.txt`
  - `dumpall.py`, `decode_events.py`
- Prototype et sonde :
  - `prototype_ops.diff`, `EventProgramRunner.cs.head`, `EventProgramRunner.cs.proto`
  - `cases_list.json`, `cases_a.json`, `cases_b.json`, `cases_c.json`, `cases_d.json`, `cases_phase.json`
  - `probe_out_list/`, `probe_out_head/`, `probe_out_proto/`
  - `summ.py`, `series.py`, `phase40.py`, `phase40_summary.txt`, `trace_426_proto_key.txt`
- Suite complète : `run_full.sh`, `full_head.trx`, `full_head.log`, `full_proto.trx`, `full_proto.log`, `cmp_trx.py`, `cmp_trx_out.txt`.
- Hachages : `MANIFEST.sha256`.
- Copie construite du HEAD `e49cce2b` (prototype installé, `docs/` et `tools/` copiés) : `probe/` (`probe/Alundra/Events/EventProgramRunner.cs`, `probe/Alundra.Tests/Probes/G5ProbeTests.cs`). Ne pas la supprimer récursivement : ses dossiers `alundra-project/` sont de vraies copies.
- Machine MIPS copiée : `work/`.

**Reproduire :**
- Modèle : `python -B -X utf8 ops_full.py 9000 mut gold`.
- Sonde : dans `probe/`, `dotnet build Alundra.Tests/Alundra.Tests.csproj -c Debug`, puis `G5_CASES=<cas.json> G5_OUT=<dossier> dotnet test --no-build --filter FullyQualifiedName~G5ProbeTests`.