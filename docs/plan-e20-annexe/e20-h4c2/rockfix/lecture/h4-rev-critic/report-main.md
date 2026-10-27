# Critique frais de la découverte « h4-rev » : révisions de h4c2 (§1.31.4) et h4d2 (§1.31.6)

Lecture seule. Je n'ai rien écrit dans le dépôt, rien construit, aucun test lancé. HEAD courant : `5e67722d`. Depuis `8174c0cb`, le plan n'a pas changé. `Alundra/` n'a changé que dans `EventProgramRunner.cs` et `TestSaves.cs`, hors périmètre ici. Les numéros de fait suivent ceux du rapport critiqué.

Légende : [FAIT] vu dans le plan, le code ou le binaire. [MESURÉ] sorties de mes propres rejeux, depuis des copies. [INFÉRENCE] déduit, non exécuté.

## 0. Verdict d'ensemble

Le rapport tient sur le fond :
- Tous les chiffres binaires (§1, faits 4 à 9) se rejouent à l'identique.
- Les clés `rockfix` et `_qfix` sont reproductibles au bit près.
- Les passages du plan à corriger sont bien repérés.

Six corrections ou manques sont à intégrer avant la rédaction des révisions (§2 et §3). Les deux plus importants :
- **R-EL-6 proposée** : l'équivalence « 0x31 posé à T = blessure réelle à T − 1 » ne vaut que pour la garde. Pour les colonnes du héros et le son 23, une blessure réelle à T − 1 est en avance d'un tick sur la clé.
- **Q-H4C2-5** : le défaut A est mal étiqueté « comme l'original », et sa dérive est sous-estimée.

## 1. Verdict par fait porteur

| # | Fait | Verdict | Vérification |
|---|---|---|---|
| 1 | D-E20-55/56/57 et lignes de questions | CONFIRMÉ | Plan lignes 219-224 ; questions aux lignes 12209, 12216, 12220. |
| 2 | Sections détaillées non à jour, tableau à jour | CONFIRMÉ | Lignes 11446, 11558, 11693-11694, 11881 relues. Le tableau 11108/11110 est à jour. Les lignes 11079 et 11097 sont génériques, rien à changer. Les cinq occurrences de `ShortestTurn` (11753, 11771, 11832, 11835, 11846) sont toutes dans les lignes listées par le rapport. |
| 3 | R-EL-6 (lignes 11328-11330) devient fausse | CONFIRMÉ, avec un manque | R-EL-6 est citée aussi par h4d1 (11645, 11658) et h4e1 (12034, 12045), absentes du §2 du rapport. Voir M1. |
| 4 | Passe des événements : statut 3 si `0x10` levé et `+0x13C` ou `+0x144` non nul | CONFIRMÉ | Binaire `0x800387D4-0x80038834` re-désassemblé. DLL : `EntityScriptProxy.cs:1953`. |
| 5 | E13 écrit `+0x88 = 1`, `[0x800DD874] = 2`, `\|0x40 & ~0x30` ; son 45 = animation 1 | CONFIRMÉ [MESURÉ] | `0x8007F30C-0x8007F374` relu. « Sons 45 = ticks de `ta == 1` » vrai dans mes 8 rejeux. |
| 6 | Chronologie originale du rocher de bord et du rocher 8 | CONFIRMÉ [MESURÉ] | Bord (id 11) : naît au 162, statut 3 au 163 à z = 240, sol au 194, `+0xAC` au 209, statut 4 au 210. Rocher 8 : `fadj` 142, statut 3 au 143 à z = 16, sol 146, statut 4 au 190. Mes sorties de base ont les MD5 des clés versées (`16bc521d…`, `79cd6cab…`, `7e402387…`). |
| 7 | Binaire modifié en mémoire (`fixpatch.py`) et ses résultats | CONFIRMÉ [MESURÉ] | Voir ci-dessous. |
| 8 | Précédent `ArrowAi` ; doublon de clé lève | CONFIRMÉ | `ArrowAi.cs:73` et `NativeSpriteRegistry.cs:70`. |
| 9 | `vec_e13` : 40 vecteurs, 20 de première passe, aucun ne lit `+0x144` | CONFIRMÉ [MESURÉ] | Offsets lus : seulement `+0x88`, `+0x6C`, `+0xAC`. |
| 10 | Glissement post-contact de la DLL | NON VÉRIFIABLE (exécution DLL interdite), ampleur CORRIGÉE | Voir ci-dessous. |
| 11 | Loi du tour : 240 paires, 5 à 8 pas contre 1 à 4 | CONFIRMÉ [MESURÉ] | Loi relue à `0x8007AD58-0x8007ADBC`, recalculée : 240 paires, 5 à 8 pas (moyenne 5,833), 1 à 4 (2,967), 31 → 0 en huit pas, `+0x274 = 2` après chaque virage. |
| 12 | 321 virages, 57 longs (17,8 %), 1 433 / 3 524 | CONFIRMÉ pour les 321 | Recompté depuis les clés : 69 + 121 + 117 + 14 = 321. Mon compte de virages « longs » donne 61 (segmentation par épisode différente) ; la source des 57 est `turn_stats_my_cpu.txt` du critique d'h4d2. Le 1 433 est lu dans son `check.md`. |
| 13 | 30 clés, dont 2 `_qfix` et 6 `_allfix` | CONFIRMÉ | `disc/keys/manifest.txt`. |
| 14 | `_pf` ≡ `_qfix` pour cinq scénarios | CONFIRMÉ [MESURÉ] | J'ai relancé `qfix_make_keys.py` depuis une copie : manifeste identique à celui du rapport. `rows` égales pour `tgt_one`, `tgt_three`, `tgt_vanish75`, `col_hit`, `cast_pf` ; seul `meta.patch` diffère. Les deux sentinelles divergent dès la rangée d'indice 50 (150 rangées). |
| 15 | Aucun mutant ne concerne la loi corrigée | CONFIRMÉ | Greps sur `extra_mutants_vs_vectors.txt` et `their_selftest_36_mutants_rerun.txt`. |
| 16 | Accesseur 32 bits : nécessaire aux vecteurs, pas à la production | CONFIRMÉ | `critic/check.md:190-192`. |
| 17 | Précédents d'aides propres ; `Alundra/Ai/Items/` absent au HEAD | CONFIRMÉ | `PickupAi.cs:420-430`, `PillarAi.cs:85-94`, `ChestAi.cs:95`. Dossiers de `Alundra/Ai` : Enemies, Objects, Projectiles. |
| 18 | Erreur de texte ligne 11732 (« rochers à gravité de h4c1 et h4c2 ») | CONFIRMÉ | Le sprite 17 vaut `0x132014`, sans `0x100`. Seul le type 16 (`0x132114`) a la gravité (plan lignes 6458-6459, 11356, 11463). |
| 19 | Aucun sort ne blesse le héros | CONFIRMÉ | `EntityFlags.cs` : `HitsClassA = 0x4`, `HitsClassB = 0x2`, `ClassB = 0x8`. Les sprites 17 (`0x132014`), 20 (`0x2044`) et 23 (`0x102074`) n'ont que `0x4`. Le héros `0x3118C` est ClassB, sans `ClassA`. |
| 20 | Le pilote est appelé avant le rangement de `0x31` | CONFIRMÉ pour l'ordre ; équivalence CORRIGÉE | Binaire `0x80031C6C` (appel du pilote), `0x80031C84` (`+0x88 = 0x31`), `0x80031C94` (`+0x8C = d`). Voir C1. |
| 21 | Colonnes du héros de `fire1_hurt60_pf` | CONFIRMÉ [MESURÉ] | `0x31` aux ticks 60-63, `0x1C` aux ticks 64-74, repos au 75. X de 6 553 600 à 8 722 432, soit +33,09375 px. `snd(23)` au tick 60. |
| 22 | Pas de `case 0x31` sur terre au HEAD | CONFIRMÉ (absence + §1.12.1) | `PlayerManager.cs:186` : `table[0x31]` est la table de nage, pas la terre. |

**Détail du fait 7 [MESURÉ].**
- `fixpatch.py` relu instruction par instruction : encodages, décalage de branche, délai de chargement, créneau de délai du saut.
- Les trois clés rejouées ont les MD5 du manifeste du rapport. La clé « milieu de carte » et les interruptions T = 60 et T = 100 sont identiques avec et sans patch, et égales aux clés versées.
- Rocher de bord : y passe de 101,2256 à 140,4443 (39,2 px) à x = 10. Sol au 194, statut 3 au 195, `+0xAC` au 241, statut 4 au 242 (b + 80).
- Rocher 8 : immobile à (157,0000 ; 72,0208) du tick 141 au 146, statut 3 au 147, statut 4 au 194.
- Plateau 16 : ids 13, 14, 15 changent de case (13 : 1 → 2, 14 : 2 → 1, 15 : 4 → 6).
- Plateau 17 : ids 13 et 15 changent de case. Les rochers 8 et 11 changent aussi.

## 2. Corrections

**C1. R-EL-6 proposée : l'équivalence « 0x31 posé à T = blessure réelle à T − 1 » ne vaut que pour la garde.**
- La garde ne voit `0x31` qu'au tick suivant la blessure, car le pilote est appelé avant le rangement de l'état (`0x80031C6C` avant `0x80031C84`). C'est ce que dit le rapport.
- En revanche, l'animation, la physique du recul et le son 23 partent dans le tick même de la blessure.
  - Oracle c2 A1, `out/A1.txt` : blessure au tick 18, `anim_target` 0x31 à la fin du 18, force X −196 608 dès le 18, son 23 au 18.
  - Banc des clés (poke avant le tick T) : X bouge dès le tick 60, son 23 au 60.
- Une blessure réelle à T − 1 produit donc les colonnes du héros et le son 23 un tick avant la clé. Une blessure réelle à T produirait les colonnes de la clé, mais l'annulation au tick T + 1.
- Conséquence pour le texte de R-EL-6 : comparer le héros (animation, X, Y, Z) à la clé seulement dans le banc au poke. Le test de blessure réelle compare les entités et les sons 45 « à T ». Pour le héros et le son 23, il compare soit avec un décalage de −1 tick, soit pas du tout.
- À l'inverse, la précondition de direction n'est pas un souci. Le recul de la clé part vers +X parce que le héros de l'oracle a `+0x8C = 24` (mesuré avec `herodir.py`). R-EL-4 place déjà le héros « tourné à l'est ».

**C2. Q-H4C2-5 : défaut A mal étiqueté, dérive sous-estimée.**
- Dans l'original, le rocher s'arrête net au contact, parce que l'animation 1 a la vitesse 0. L'option A (continuer à pousser) n'est donc pas « comme l'original sauf l'instant de l'impact ».
- A est ce que produit le binaire modifié en mémoire. C'est le seul oracle disponible, et c'est un argument fort pour garder A comme défaut : B n'a aucune clé.
- La dérive « à quelques pixels près en oblique » vaut pour le rocher 8, soit 5 ticks restants à environ 0,7 px par tick. Un rocher qui bute tôt dans sa chute peut en avoir jusqu'à environ 30. À 1,2 à 1,7 px par tick sur l'axe libre, la DLL dérive alors de plusieurs dizaines de pixels, alors que le binaire reste immobile. [INFÉRENCE]
- À reformuler dans la question.
  - A : le rocher continue de glisser le long du mur jusqu'au sol. La position d'écrasement peut différer de plusieurs dizaines de pixels de l'original.
  - B : il s'arrête là où il a touché, comme l'original. Cela demande une règle propre au rocher et n'a pas de clé.

**C3. Fait 10, ampleur.** La lecture du code est compatible avec le rapport : `ScriptedMotion.cs` branche oblique de `SlideAlongWall`, et `ClampStepToMapEdge` indépendant par axe. Le « jusqu'à 1,73 px par tick » est juste par tick, mais le total dépend des ticks restants (jusqu'à 31). Voir C2. Je n'ai pas pu mesurer côté DLL.

**C4. (s12) est surestimée en risque.**
- R-EL-4 pose un champ plat de cases à zéro, donc aucun bit `0x180`.
- Les données disent la même chose : la carte 14 est dans la liste des cartes sans case dangereuse (`e20-c2/c2-dll.md` §1.10, FAIT). La sonde reste utile mais le risque est nul.

**C5. Clés `rockfix` annoncées « quatre » alors que `keys_rockfix/` en contient trois.** La quatrième (`cancel_t170`) n'est que dans `h4c2/out/` (`cancel_t170_FIX.json` et `.txt`). Elle n'est donc ni dans le manifeste ni versée comme clé.

**C6. (w8) : la moitié « octet 17 » doit viser `…_hb17_rockfix`.** L'ancienne clé `plateau_hb17` est fausse pour la DLL : les rochers 8 et 11 et les indices de case 13 et 15 changent, ainsi que les coups de caméra (143 → 147, 163 → 195). Le texte proposé pour (w8) ne cite que `hb16_rockfix`.

**C7. Compte « 7 virages sur 321 ».** Sans objet (voir le fait 12). Rien à corriger, seulement une segmentation à ne pas figer.

## 3. Manques

**M1. Retombées de R-EL-6 sur d'autres tranches.**
- h4d1 (11645, 11658) et h4e1 (12034, 12045, clés `_gateoff`) citent R-EL-6.
- Elles comparent aujourd'hui « l'objet, la phase, le compteur, les éclairs, le générateur », pas le héros après T.
- Après la révision, leur texte doit dire que la colonne héros est optionnelle ou comparée. Sinon la règle commune et les tranches se contredisent.
- Le plan contient déjà une incohérence voisine : h4d2 T1 bis liste « le héros (`anim_t`, `anim_c`, X, Y, Z) » dans les colonnes comparées alors que R-EL-6 les exclut.

**M2. Argument d'ordre manquant à D-H4C2-3.**
- Le texte affirme « le statut vu à la fin du tick reste 2, de sorte que `Cancel` l'atteint » sans dire pourquoi `Cancel` ne voit jamais le statut 3 transitoire.
- Dans le binaire, `MovePlayer` (`0x80031B50`) est appelé en tête de la passe, à `0x800386EC`, donc avant la boucle de choix. L'annulation s'exécute avant les programmes des rochers.
- La DLL fait pareil : `WorldProxy.RunEntityBlock` appelle `MovePlayerOnTickPad` avant `TickPrograms`.
- À écrire dans D-H4C2-3.

**M3. Cas hors murs ou bord.** La garde `IsOnGround` couvre toute cause de `ForceAdjusted`, y compris un pas bloqué par une entité `Collidable`, pas seulement « mur ou bord » comme D-E20-56 le dit. Aucune clé ne l'exerce. Le dire dans l'écart consigné.

**M4. (w3) : variante de l'écart au bord à 2 et 3 ticks par image.** Le rocher de bord dépend du rognage par tick (`ClampStepToMapEdge`) et de l'ordre D-E20-38. Seule la chronologie d'un rocher intérieur est testée à 2 et 3 ticks par image. Un (w3b) pour le rocher de bord est un ajout raisonnable.

## 4. Ajouts mesurés

- **Balayage de l'annulation, T = 150 à 200** (`sweep_cancel.sh`, `sweep_analyze.py`), binaire modifié, pour chaque T :
  - tous les rochers au statut 2 en fin de tick T − 1 sont annulés : nombre de sons 45 au tick T égal au nombre de rochers en l'air ;
  - le rocher de bord (id 11) est vu pour la dernière fois à T + 47 pour T = 164 à 194, et à 242 (mort naturelle) à partir de T = 195.
  - Cela renforce D-H4C2-3 : le statut 2 en fin de tick tient à toutes les dates.
- **Clés de cibles (w5) inchangées** : `targets_2c_pf_inv0` et `inv30` donnent un JSON identique avec et sans patch. Aucune clé à refaire pour (w5).
- **« 3 rochers sur 15 sans le pas par image »** : confirmé. Sans `--pf`, trois rochers (ids 13, 15, 16) ont `fadj` posé.
- **DLL** : `EntityStatus.Normal = 2`, `Deactivated = 3`, et `Status` est un champ simple (`EntityScriptProxy.cs:88`). D-H4C2-3 est implémentable telle qu'écrite.
- **Tests déplacés à l'exécution** : seulement les deux épingles du registre, à `Alundra.Tests/Ai/NativeSpriteRegistryTests.cs:55` et `Alundra.Tests/Ai/Projectiles/WaveSwordLifeTests.cs:185` (le rapport dit « ~190 »).

## 5. Questions à l'auteur, en termes de jeu

Le texte des deux questions est à corriger comme ci-dessous. Avec « prends les choix par défaut », ce sont A et A.

**Q-H4C2-5.** Quand un rocher de la terre 2 touche un mur ou le bord de la carte pendant sa chute, il ne joue plus bruit et secousse en l'air. Que fait-il d'ici là ?
- **A (défaut)** : il continue de pousser contre le mur et glisse le long du bord, puis s'écrase au sol. Dans la scène témoin du bord, il se pose 39 px plus loin ; en oblique contre un mur, l'écart avec l'original peut aller de quelques pixels à plusieurs dizaines. Ce n'est pas la position de l'original (qui s'arrête net), mais c'est la seule qu'une clé mesurée appuie.
- **B** : il s'arrête là où il a touché, comme l'original, et tombe droit. Coût : une règle de plus propre au rocher, un test et une clé en plus, sans oracle mesuré.

**Q-H4C2-6.** Même règle pour les rochers de la terre 1 et les boules du feu 1, qui éclatent aussi en l'air contre un mur (même code E3) ? Q-H4C1-5 ne disait pas que la terre 1 jouait son impact en l'air.
- **A (défaut)** : seule la terre 2 change.
- **B** : la terre 1 aussi. h4c1 est rouverte et la règle devient propre à chaque type.
- **C** : les boules aussi. Elles n'éclateraient plus au mur.

## 6. Risques à porter dans les révisions

- La comparaison des colonnes du héros après T suppose l'ordre des passes de c2b/d1 sur le héros bloqué par la pose. L'oracle A1 ne couvre que le héros libre. [INFÉRENCE]
- Le glissement oblique de la DLL est la seule divergence de position. R-EL-5 doit s'étendre à la position, avec une tolérance justifiée. Sinon (w8) échoue sur quelques pixels.
- Les clés `rockfix` restent « indicatives, non fidèles à l'octet » (D-H4C2-4). Les deux révisions ouvrent chacune une nouvelle époque de disponibilité : un `plan-verifier` frais pour clore chacune.

## 7. Chemins

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/t2-next/h4-rev-critic/`

- Lanceur : `run.py`. Aide de désassemblage : `bin/bx.py`.
- Banc rejoué : `h4c2/oracle/` (copie de l'annexe `disc/h4copy/oracle` plus les scripts de patch de la découverte, `herodir.py`, `targets_pf_fix.py`).
- Rejeux : `out/cast_pf_*`, `out/hb16_*`, `out/hb17_*`, `out/mid_*`, `out/c60100/`, `out/t_*`.
- Balayage d'annulation : `sweep_cancel.sh`, `sweep_analyze.py`, `out/sweep/`.
- Lois du tour et rangées de virages : `turn_law_mine.py`, `tr.py`.
- Regénération `_qfix` : `h4d2/h4o/` (copie de l'annexe plus `qfix_make_keys.py`), `out/qfix/` et `out/qfix/manifest.txt`.