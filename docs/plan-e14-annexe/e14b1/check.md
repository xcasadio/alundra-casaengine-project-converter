# Contre-vérification de « E14.b1 : cartes 14 et 15 » (label check-e14b1)

Je n'ai pas pu écrire `report.md` : l'outil Write a refusé (« les sous-agents rendent leurs résultats en texte »). Ce texte en tient lieu. Mes scripts et listings sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e14b1/` :
- `cdis.py` : désassembleur capstone annoté.
- `lst/*.txt` : listings de A0, C15 à C19, D5, D6, D11, D12, E1 et des aides (`H_*`).
- `mymips.py` : interpréteur MIPS-I écrit de zéro par moi, sans reprendre l'`emu.py` de l'autre agent.
- `check.py` : harnais différentiel.
- `models_copy.py` : copie de `e14b1/models.py`, seule la ligne d'import change.
- `cmf_check.py`, `had_check.py`, `anim_check.py`, `check_run_20000.txt`.

Dépôt lu à `9502f75`. Binaire : `ALUN_CD.EXE` (France), SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`, identique à celui du rapport. Tout est en lecture seule.

## 1. Verdict

Le rapport est fiable pour écrire les tâches T2 à T7. Je n'ai trouvé aucune erreur de comportement dans ses pseudo-codes.

Je relève des imprécisions de rédaction, un écart de décompilation non listé et quelques lacunes (§4). Aucune n'invalide une tâche.

Preuves, indépendantes de l'autre agent :
1. J'ai relu à la main tous les désassemblages des dix gestionnaires (A0, C15 à C19, D5, D6, D11, D12), de E1 et des aides utilisées, branche par branche, délais de saut compris.
2. J'ai fait tourner le vrai code du binaire sur `mymips.py` contre `models.py` :
   - 20 000 vecteurs par gestionnaire (graine 987654), 0 écart.
   - La comparaison porte sur l'image d'entité de 0x294 octets, l'image du héros, le mot du générateur et le journal des appels bouchonnés (son, effet, `ComputeNewHp`).
   - Couverture d'instructions 100 % sur les dix : 176/176, 214/214, 178/178, 260/260, 143/143, 62/62, 25/25, 25/25, 74/74, 3/3. Elle inclut les 2 mots de C19 que l'autre agent n'atteignait pas (animation cible négative).
   - Seule exception : C15 avec `+0x98` >= 4 ou négatif. Le modèle ramène à 0, le binaire lit hors de la table `0x80028B54`. Le binaire n'écrit jamais que 0 à 3 dans `+0x98` (table `0x800237F4`, 32 valeurs toutes dans 0..3). Cela ne joue donc pas en jeu.
3. `CanMoveForward` `0x80080260` : 12 288 cas (16 motifs de coins, 32 directions, 4 distances, variantes de profondeur), 0 écart avec ma formule.
4. L'arbre de la DLL (`AlundraEnemyHelpers.HandleAnimationDirection`, :95-170, que j'ai retranscrit moi-même) contre le vrai code : 60 000 cas, 0 écart.
5. J'ai relu moi-même `map_N.json`, `balance.json` et les fiches d'animation : tout concorde.

## 2. Vérification par affirmation

### 2.1 Adresses et structures (toutes CONFIRMÉES)

- **Entrées de table et codes.**
  - Entrées : A0 `0x800C4B34`, C15 à C19 `0x800C4F70/74/78/7C/80`, D5/D6/D11/D12 `0x800C5348/4C/60/64`, E1 `0x800C5738`.
  - Codes : `0x8006174C`, `0x80069C84`, `0x80069F44`, `0x8006A29C`, `0x8006A564`, `0x8006A974`, `0x8007DBE0`, `0x8007DCD8`, `0x8007DEE8`, `0x8007DF4C`, `0x8007ED10`.
  - Méthode : `base = u32(0x80098F4C+4*slot)` puis `u32(base+4*code)`.
- **Tailles** 176, 214, 178, 260, 143, 62, 25, 74, 25, 8, 3 mots : écarts entre débuts de fonctions. D11 est identique à D8 octet pour octet (testé).
- **Tables de sauts** `0x80027334/54/74/94` et toutes leurs cibles : relues. Les bornes `sltiu` sont 7, 7, 7 et 6 pour C15, C16, C17 et C18.
- **En-tête** : octets `+0x13..+0x17` vers `+0x70/+0x74(=0)/+0x78/+0x7C/+0x80/+0x84` à `0x80039DD4-0x80039E10`. `+0x6C = lhu +0x10 | lbu +0x12 << 16`.
- **Valeur de `+0x68`** :
  - `0x8003A010` passe `s2` (ou `s2+0x100` pour une page carte) en 5e argument de `0x80039D04`, donc `+0x68` = index + `0x100`.
  - 338 = `0x152`, 341 = `0x155`, 425 = `0x1A9`.
- **Répartition par phases** :
  - Phase 1, `0x800386D0-0x8003889C` : D si `+0x224 != 0` à `0x80038840`, C par défaut à `0x80038860`.
  - Phase 2, `0x800388E4-0x80038944` : appel seulement si `entity[+0x4C+4*s] & 0x7F == 0`.
- **Codes d'en-tête des cinq types** (0/19/6/1/0, 0/18/11/1/0, 0/16/12/1/0, 0/15/12/1/0, 0/17/5/1/0) : confirmés par `map_14/15/3/4.json` et `roster.tsv`.
- **Drapeaux** 0x00103, 0x10003, 0x30103 : confirmés. Le bit `0x100` conditionne le bloc `0x80038110-0x80038270` (hauteurs de coins `+0x158..+0x164`). La Guêpe, sans ce bit, n'appelle pas `CanMoveForward` : c'est cohérent.

### 2.2 Pseudo-codes des gestionnaires

| Gestionnaire | Verdict | Points vérifiés (adresses) |
|---|---|---|
| A0 `0x8006174C` | CONFIRMÉ | `lh +0x11E` puis `sh +0x280`, rien d'autre. Seule la Guêpe lit `+0x280` (extraction automatique des accès champ par champ). |
| E1 `0x8007ED10` | CONFIRMÉ | `DestroyEntity(self,-1)`. `0x8003A59C` : Status 4 à `0x8003A5BC`, `+0x228 = -1`, effet `lbu +0x1E` si `a1 == -1` à `0x8003A5E8`. |
| C19 Poisson | CONFIRMÉ | État 1 `0x8006AA30` : `\|dx\|<3`, `\|dy\|<3`, non `0x100000<\|dz\|`, B0=1, `below(16)+0x3C`. `FA==0 && CMF==0` donne fin, sinon `below(32)`. État 0 `0x8006A9D0`. État 5 `0x8006AB30` (son `0x96`). État 7 `0x8006AB58`. |
| D6 | CONFIRMÉ | `ComputeNewHp`, B3=1 si vrai, TA=6, TD = (soi − héros). `0x8007DCE4-0x8007DD24`. |
| D11 | CONFIRMÉ | Idem avec TA=4. |
| C18 Guêpe | CONFIRMÉ | Voir détail ci-dessous. |
| C15 hache | CONFIRMÉ | Voir détail ci-dessous. |
| C16 masse | CONFIRMÉ | Voir détail ci-dessous. |
| C17 fer | CONFIRMÉ | Voir détail ci-dessous. |
| D5 | CONFIRMÉ | Voir détail ci-dessous. |
| D12 | CONFIRMÉ | Voir détail ci-dessous. |

Détail pour les gestionnaires longs :

- **C18** :
  - B0 != 0, `0x8006A5AC` : bandes `0xFFFFF` et `0x180000` sur `dz` signé, TA 2/1/3, sans test OG.
  - AI1 != 0, `0x8006A784` : avec test OG pour TA 3.
  - AI1 == 0, `0x8006A630` : `|dz| <= 0x200000` sur la valeur absolue (sp+0x20), puis deux tirages systématiques (`below(32)`, `below(41)+0x50`).
  - Ensuite `t0 = +0x11C − (lhu +0x280 << 16)` : `> 0x4FFFFF` donne 3, `> 0xFFB00000` donne `below(3)+1`, sinon 2.
  - États 1/2/3 aux adresses `0x8006A7EC/64/88`. Après `TD+4`, l'état 1 sort à `0x8006A860`.
  - État 5 `0x8006A920`.
- **C15** :
  - `0x80069CCC` : B0 donne TA 3 ; B1==0 donne un quart de tour `Cardinal[AD]−8` avec B1=4 et `stop6`.
  - AI1==0 donne AI1=`0x1E`, puis `TryAttackPlayerFront(2,2,0)`, puis `below(5)==0` donne le son `0x94`, puis B0=3.
  - Sinon : AI1--, et si FA alors `stop6`, B1--, `Cardinal[AD]−0x10`.
  - État 3 `0x80069E40`. États 5/6 `0x80069ECC`. Table `0x80028B54` = `00 10 08 18`.
- **C16** :
  - État 0 `0x80069F8C` : `0x8007FEF8(1,0x78,0x46)` puis B1=0.
  - État 1 `0x80069FC0`.
  - ATTAQUE `0x8006A258`.
  - État 3 `0x8006A18C` : `W28C++` sur 32 bits, puis `&7==0` donne `CreateEffectEntity(0, octetC, 0, +0x114, +0x118, +0x134)`.
  - État 5 `0x8006A220`. État 6 `0x8006A1F4`.
- **C17** :
  - État 0 `0x8006A2E4`. État 1 `0x8006A3A4`.
  - Zone d'attaque : `dy<=0`, `|dx|<4`, `|dz|<=0x100000`. Selon `dy`, TA vaut 4, 2 ou 3. TD vaut `0x18` si `dx<-1`, sinon 0.
  - Hors zone : FA donne `TD−0x10` et AI1 `0x78` ou `0xF0` selon AD ; sinon CMF donne TA 0 et AI1 0.
  - États 2 à 4 `0x8006A4C8`. État 6 `0x8006A4E4`.
  - Les attaques n'ont bien que deux orientations.
- **D5** :
  - TD est écrit dans le délai de `0x8007DC30`.
  - `(nib-1) <u 3 && +0x68==0x1A9 && TA <u 2` puis `below(100) >= 0x3C` donne TA=`0xB` sans dégât (`0x8007DC98-0x8007DCBC`). Sinon `ComputeNewHp`, B3, TA=5.
  - Aucun tirage pour le type 344.
- **D12** :
  - `(nib-1) <u 3` entraîne un seul tirage `below(100)` à `0x8007DFE4`, pour tout type et tout état.
  - Si le tirage vaut `>= 0x46` : type `0x152` donne TA 6 si `TA <u 2` ; type `0x155` donne TA 6 si `TA <u 2` ou `TA==7`.
  - Sinon `ComputeNewHp` puis TA=4.

Les appels (`jal`) extraits automatiquement sont exactement ceux du rapport. Aucun appel manqué.

### 2.3 Aides

- **`0x8007FE20` / `0x8007FE8C`** : CONFIRMÉ. `dz = +0x134 − +0x134 héros` ou `+0x11C − +0x134 héros` (`lw +0x11C` à `0x8007FEA4`).
- **`0x800805C8`** : CONFIRMÉ. Fenêtres des 4 AD, `zmax` à `[sp+0x10]`.
- **`0x8007FEF8`** : CONFIRMÉ. 2 ou 3 tirages avec `range`, 1 ou 2 sans.
- **`0x80080260`** : CONFIRMÉ. Formule exacte du rapport, « vrai = chute devant ».
- **`0x8003A374`** : CONFIRMÉ. Écrit `+0x14`, retourne `sltiu s3,1`.
- **`0x8002E814`** : CONFIRMÉ. Mesuré dans mon interpréteur : `gdt(0,+100)=0` (bas), `(+100,0)=24` (droite), `(0,-100)=16` (haut), `(-100,0)=8` (gauche).
- **Aides hors b1** : `0x80080030`, `0x800808DC` et `0x80080144` sont CONFIRMÉES.

### 2.4 Écarts avec la décompilation

- **E1 (Guêpe, `FUN_8007fe8c`)** : CONFIRMÉ. Décompilation `FunctionTypeC.cs:8120` (`entity.FloorHeight`), binaire `lw +0x11C` à `0x8007FEA4`, appelé en `0x8006A574`. La même fonction est appelée par au moins 3 autres gestionnaires (`FunctionTypeC.cs:2129, 2340, 3564`), donc l'écart les touche aussi.
- **E2 (Guêpe, état 1)** : CONFIRMÉ. La décompilation (`FunctionTypeC.cs:8040-8056`) tombe dans la course d'attaque ; le binaire fait `j 0x8006A960` après `TD+4` (`0x8006A860`).
- **E3 (`Random.Next() % 32`)** : CONFIRMÉ mais surévalué.
  - Le binaire prend les 5 bits hauts (`0x8007FFA0-0x8007FFB8`, `0x8006A6D0`).
  - Le texte dit « toutes les directions tirées au hasard par la masse et la guêpe », ce qui est faux.
  - Dans C16 seule la branche « au hasard » de `StartFlying` est touchée. Les trois autres tirages de C16 sont corrects dans la décompilation (`(Next()*0x20)>>0x20`).
  - Dans C18 seul `TD = r1` de l'état 0 est touché.
- **E4 (D12, masse)** : CONFIRMÉ. Le test `0x155` est imbriqué dans `0x152` (`FunctionTypeD.cs:277` et `:286`), code mort.
- **E5 (`CanMoveForward`)** : CONFIRMÉ. Avec l'énumération du rapport je reproduis 171 écarts sur 1 536. Avec la mienne j'en trouve 1 368 sur 12 288. Sur terrain plat avec TD de 0 à 15, la décompilation répond 1 et le binaire 0.
- **E6 (`W28C`)** : CONFIRMÉ, mais la citation est décalée. Les lignes sont `FunctionTypeC.cs:811-812` et 847-848, pas 846-847. Binaire : `lw/sw +0x28C`.
- **E7 et E8** : CONFIRMÉS (`FunctionTypeA.cs:9`, gardes `entity.Name`).
- **Non-écarts annoncés** (C15, C17, C19, C16 hors E3/E6, D5, D6, D11, A0, E1, `TryAttackPlayer`) : CONFIRMÉS par comparaison ligne à ligne avec la décompilation. `TryAttackPlayerFront` y est équivalent au binaire.

**Écart non listé (E9, mineur)** : `FunctionTypeD.cs:137` (D5) et `:271` (D12) testent `(val & 0xf) - 1 < 3` en arithmétique signée C#. Un nibble 0 donne `-1 < 3`, donc vrai, donc un tirage. Le binaire utilise `sltiu` non signé (`0x8007DC2C`, `0x8007DF98`) : nibble 0, pas de tirage. Le rapport (§5) dit bien « nibble 0 : pas de tirage » pour le binaire, mais ne range pas cela parmi les écarts.

### 2.5 Données

- **Fiches d'animation du §1.3** : CONFIRMÉES pour les cinq types (vitesses, impulsions Z, sons, ticks, boîtes, fins). C'est une relecture indépendante du JSON.
- **Sémantique de fin d'animation** : CONFIRMÉE au binaire `0x80038D14-0x80038D88` (delay 1 = boucle ; 0 avec bit 7 = `+0xAC=1` ; 0 sinon = enchaînement).
- **Impulsion Z** : `lh +0xA` du jeu d'animation (`0x80038B5C`). L'octet `+0xD` va dans `+0xB4`, l'octet `+0xC` est le son.
- **Enregistrements** :
  - Compte brut de 160 sur 32 cartes (33 + 64 + 2 + 48 + 13).
  - Moins 3 sur la carte 134, où `SpriteTable[85] = -1` (vérifié), soit 157 sur 31 cartes.
  - Positions, `0xC0`, codes 0 et octet C = 7 des cartes 14 et 15 : confirmés. L'octet C vaut 7 sur les 13 cartes de l'Orc masse.
- **Balance** (`balance.json`, indexé par numéro de type) : CONFIRMÉE. Vies 2/2/9/9/9, attaques (1,1), (1,1), (2,4), (1,3), (2,1), réponses `[16,16,16,64,…]`.
- **Sons** 0x94, 0x96, 29, 35, 64, 65, 225 : présents dans `sfx-manifest.json`.

### 2.6 Citations de la DLL (échantillon de 25)

Elles sont exactes à une ou deux lignes près :
- `AlundraNativeSpriteRegistry.cs:47, 53-55, 63`
- `AlundraMurutaAi.cs:40, 52`
- `AlundraEnemyHelpers.cs:16, 25, 35, 39, 65, 95, 201, 224`
- `AlundraEventProgramRunner.cs:106, 345-352, 378`
- `AlundraHitArena.cs:68, 77`
- `AlundraNativeSpriteRegistryTests.cs:51, 235-245`
- `AlundraTarnMurggArcTests` R3-2 : (26, 21, 7, 1500)

`NativeHandlersEnabled` vrai par défaut : exact. La DLL a déjà `RelativePositionFromPosZ`, donc l'écart E1 n'existe pas côté port.

## 3. Réponses aux questions ouvertes du rapport

**INCONNU-2 (Guêpe, impulsion Z sans gravité) : largement résolu, et la DLL le fait déjà.**
- `0x80038AE4` met `+0xF8` à 0 à chaque appel de la mise à jour d'animation. `0x80038B5C-0x80038B64` ne le repose qu'au tick du changement d'animation.
- `0x80036AB8-0x80036B04`, entité non héros : si `+0xF8 != 0`, alors `ForceZ(+0xB8) = +0xF8 << 8`. La valeur est 0 si les 16 bits bas valent `0x8000` sans drapeau `0x100`.
- `0x80036B08-0x80036B60` : sans impulsion, la décroissance de gravité ne s'applique que si `Flags & 0x100`. Sans gravité ni impulsion, `ForceZ` reste tel quel.
- `0x80036BAC` copie `+0xB8` dans `+0xEC`. Le moteur vertical `0x800375E0` fait `PosZ += +0xEC` (`0x80037708-0x80037714`). Il ne remet `ForceZ` à 0 au contact que pour une entité à gravité (`0x80037688`, `0x80037704`).
- Donc la Guêpe monte ou descend de 1 px/tick en permanence après une animation 2 ou 3, y compris pendant 0 et 1, jusqu'à la prochaine impulsion non nulle ou au sol.
- La DLL l'implémente : `AlundraEntityScriptProxy.ApplyVerticalForceLaw` (E20.e0k2, vers `AlundraEntityScriptProxy.cs:1036`). Il reste à l'observer en scénario `GU-*`, mais ce n'est plus un inconnu du binaire.

**Ordre du tick** (utile à l'oracle), à `0x8003B388` :
1. Phases 1 et 2 (`0x800386D0`).
2. `0x80038998` : remise à zéro de `+0x224`, `+0x220`, `+0x12C` (boucle `0x800389D0-0x80038A14`).
3. `0x800384F4`.
4. Mise à jour d'animation (`0x80038E18`, qui appelle `0x80038AB4`).
5. Physique (`0x80038364`, qui appelle `0x80036828`).
6. `0x80038E84`, puis `0x80039300`.

Les gestionnaires voient donc le `ForceAdjusted` du tick précédent (remis à 0 en `0x800383B8`). Leur `TA` est pris en compte par l'animation et la physique du même tick.

## 4. Imprécisions et lacunes à corriger avant d'écrire les tâches

1. **C16, état 1 (§3.5).** L'indentation du pseudo-code est ambiguë. Dans le binaire, quand `AI1` tombe à 0 (les deux branches, B1 != 0 ou B1 == 0), le gestionnaire sort : le `TryAttackPlayerFront` n'est pas évalué ce tick-là. `models.py` est juste, le texte l'est moins. Les tâches doivent suivre `models.py`.
2. **§7.4, calendrier de mort.** L'ordre « animation de mort, puis `Flags |= 0x40`, puis E1 » est inexact.
   - `Flags |= 0x40` est posé en même temps que `TA = mort`, quand C voit `END` du dernier recul.
   - E est choisi quand `Flags & 0x40` et `+0xAC != 0` (`0x80038818-0x80038838`). Le Status passe à 3, la tranche E tourne à chaque tick jusqu'à `DestroyEntity`, qui met le Status à 4.
   - E n'arrive donc qu'à la fin de l'animation de mort.
3. **Coup pendant la mort (non traité).** Tant que `+0xAC == 0` (animation de mort en cours), la phase 1 continue de choisir D si `+0x224 != 0` (`0x80038840`). Un nouveau coup rejouerait D6, D11, D5 ou D12 : TA retombe en recul. Le rapport ne le dit pas. L'oracle doit mesurer un coup pendant la mort ; cela dépend de la passe de coups (`0x800394xx`), que je n'ai pas lue.
4. **E3 surévalué, E6 mal cité, E9 manquant** (voir §2.4).
5. **« Piège » `AI0 lu non signé (lhu)` (§3.3).** Sans effet. `lhu` puis `<< 16` donne le même mot que `lh` puis `<< 16`, et en C# `(ushort)x << 16` et `(int)(short)x << 16` sont égaux. Ce n'est pas un piège de portage.
6. **C15 `CA = -1`.** Dans la DLL, `ResolveIdsv` (`AlundraFrameSyncPasses.cs:478`) ne reconnaît que l'idiome `~TargetAnimationId`. Avec `CA = 0xFFFFFFFF` et la cible 3, la clé IDSV du tick intermédiaire est celle de l'animation `0xFFFFFFFF`, soit la valeur 0. Le rapport écrit « pas de conflit » en le marquant « à vérifier ». À tester dans T4. Le déclenchement du redémarrage lui-même est bon : `TryResolveAnimationTarget` repart dès que `CurrentAnimationId != TargetAnimationId`.
7. **Champs omis.** La liste « champs lus » du Poisson omet `+0x114/+0x118` et `+0x6C`. Sans conséquence.
8. **Non vérifié** (INFÉRENCE ou INCONNU maintenus) :
   - le butin `Contents >= 0x80` (`0x80032B90` non lu) ;
   - la puissance des coups d'épée ;
   - la passe de coups ;
   - la physique du Poisson dans le bassin (INCONNU-1) ;
   - l'arc R3-2 (INCONNU-4) ;
   - les tests exposés du §6, dont je n'ai pas exécuté la suite.

## 5. Peut-on écrire les tâches de la tranche ?

Oui. T2 à T7 peuvent être rédigées à partir de `models.py` et des §2 à §4 du rapport, avec les corrections ci-dessus.

Les 9 clés (C15 à C19, D5, D6, D11, D12) et le A0 étendu sont les bonnes. Leurs corps sont exacts d'après le test différentiel indépendant (20 000 vecteurs par gestionnaire, couverture d'instructions 100 %). Les décisions Q-b1-1 à Q-b1-5 sont correctement posées. La recommandation « le binaire tranche » (parade de la masse, branche 425 de D5) est confirmée par le code.

Fichiers :
- `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e14b1/check.py`
- `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e14b1/mymips.py`
- `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e14b1/lst/` (listings)
- `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e14b1/check_run_20000.txt`