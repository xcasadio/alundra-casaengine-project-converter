# Critique du rapport « E20.e0c — butin » (HEAD `2010ec3`)

**Verdict : fiable pour écrire les tâches du binaire (T0, T1, T2a, cœur de T3).** Il faut corriger le recensement (§4), élargir T3 et ajouter un point manquant (VRAM). Je n'ai trouvé aucune erreur sur les adresses, les branches, les offsets, les écritures ni les appels aléatoires.

J'ai tout redésassemblé moi-même, avec mes propres modèles écrits depuis le désassemblage. Je n'ai pas réutilisé les modèles de l'autre agent. Le SHA-1 de l'EXE est bien `a705f8da…`. Mes fichiers sont dans `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts2/check-e0c/` (`t_spawn2.py`, `t_init2.py`, `t_c255b.py`, `t_c255_bound.py`, `t_a255b.py`, `t_give2.py`, `t_herb.py`, `t_destroy2.py`, `census.py`).

## 1. Faits du binaire

### Confirmés par ré-exécution du vrai code (mon harnais, 0 écart)

| Élément | Preuve |
|---|---|
| `Choose` `0x80032968`, table `0x80028E0C` | md5 `7c6b97ac…`. Distributions exactes recalculées (0x80, 82, F7, F8, FA, FB, FC, FF) : identiques. La table de la DLL (`AlundraLootRoll.cs:108`) est identique octet pour octet. Les lignes non nulles sont celles de 0x81-0x83 et 0xF6-0xFF. |
| `InitializeContents` `0x80032A40` | 20 000 vecteurs, 57/58 instructions couvertes. Le seul bras non couvert est `0x80032AB8`, mort comme annoncé. Le code négatif est le seul cas où le comportement diffère du modèle, et les données ne l'atteignent pas. |
| Veto `0x80032A00` | MpMax lu par `lh [[0x80176318]+6]`. |
| `SpawnRandomLoot` `0x80032C7C` | 12 000 vecteurs, 62/62. Le tirage a lieu avant le veto. Les arguments sont (code, X, Y, Z). Les sept écritures sont celles du rapport : `+0xB8=0xA0000`, `+0x274=1`, `+0x6C&=~0x80`, `+0x278=600 ou −1`, `+0x27C=0`, `+0x280`, `+0x284=0xA0000`. Puis son 0x54. |
| `SpawnContents` `0x80032B90` | Même corps, avec `+0x3C`, `+0x114/118/11C` et `+0x280=+0x40`. Trois appelants confirmés. |
| `DestroyEntity` `0x8003A59C`, récursion `0x8003A648`, destruction légère `0x8003A774` | 2 004 vecteurs, couverture 43/43, 75/75 et 15/15. Le test d'état `1..3` porte bien sur la cible. Un enfant déjà mort est détruit de nouveau et relâche donc deux fois. La destruction légère a 32 appelants. |
| C255 `0x8007C174` | 18 000 vecteurs (3 graines), couverture 381/381, 0 écart. J'ai vérifié que mon test n'est pas creux : 3 mutants sur 7 sont tués. Un test de bornes dirigé confirme le recouvrement : `Δ ≤ taille` dans les deux sens, donc `a < taille+1`. |
| A255 `0x80061BD4` | 15 000 vecteurs, 79/80. |
| `GiveItem` `0x80033F00` | 0x54, 0x55, 0x56, 0x51, 0x26 : 6 000 vecteurs. Ordre des arguments d'effet (`0, 0xE, anim, X, Y, Z+0x100000`), 2 pas du générateur par effet créé, forces `hi(r1·0x20001)−0x10000`, `hi(r2·0x30001)−0x18000`, `+0x5C=0x20000`. Herbe 0x24, règle « inventaire d'abord » : 4 000 vecteurs. Retour = `(octet4 == 0)`. |
| Tableau de sauts de `GiveItem`, `AddMoney` | Cible conforme pour chaque objet. Plafond `0x270F`. |
| Table des objets `0x800C5F7C` | 14 objets avec octet4 ≠ 0, sons 52/53/54. `item-drop-properties.json` = octets 4 à 7 des 98 rangées (vérifié). |
| Gravité | 481 cartes à 128, 2 cartes (159 et 160) à 3. Le champ `Gravity` est bien à `+4` (`GameMapInfo.cs`). |
| Séquence de rebond à Gravity 128 | `0x78000 … 0x9029` : exacte, 10 valeurs. |
| Enregistrements | C255 = 87 types (31 à 118 sauf 72), A255 = 88 types. Aucun enregistrement côté carte. |
| Mots de garde | `[0x80029BCC]` est seulement lu (valeur 1), `[0x8009A858]` aussi. |
| Boucle d'image | Ordre `0x80038634`, `0x800386D0`, `0x80038998`, `0x800384F4`, `0x80038E18`, `0x80038364`, `0x80038E84`, `0x80039300` confirmé. `0x80038364` réécrit bien `+0x1D8…` avant le déplacement. |
| Clignotement | `(+0x1CC & 3) != 3` à `0x800385E8` pour la liste des visibles. Décrément de `+0x1CC` à `0x80038998`. Le rapport dit « 1 tick sur 4 » : exact. |
| Autres | C6 (type carte 148, 22 posés, état 6 à `0x8006707C`, 1/4). E11 `0x8007F25C` : 7 types, soit 232, 220, 253, 223, 190, 162, 207 + 256, comme annoncé. Coupe d'herbe : `SpawnRandomLoot(0xFF, X, Y, Z)`. |

### Écarts avec la décompilation
Le rapport signale L1 (deux pas par maillon dans la décompilation), L2 (`Contents` ushort contre octet) et L3 (garde perdue, décrément si `d ≤ −2`, pointeur contre indice). Je les confirme tous.

### Réfuté ou corrigé

- **R1. Recensement du §4 : la page n'est pas décidée par « le record existe ».**
  - Le binaire la lit dans le bit 7 de `SpriteDirection` : `0x8003A270`, `andi 0x80`, puis `0x80039B28(page, type)`. La décompilation fait de même (`GameEngine.cs:720`). Le rapport utilise une heuristique.
  - Avec la bonne règle (`census.py`), six entités changent de classe : (329, 4/11/12/19/20), qui sont G252 sans butin, et (231, 3), qui est G35.
  - Chiffres corrigés :

    | | Rapport | Corrigé |
    |---|---|---|
    | tirées dans la table | 4 206 | **4 200** |
    | sans butin | 4 928 | **4 934** |
    | 0xFB | 674 | **669** |
    | 0x82 | 2 070 | **2 069** |
    | M35 | 915 | **914** |
    | pas du générateur attendus | ≈ 4 857 | **≈ 4 848** |
    | objets posés servis par C255 | 151 entités / 51 types | **152 / 53** |
    | coffres | 515 / 98 contenus distincts | **518 / 97** |

  - **M252 « sbires du roi » n'est pas tiré dans la table.** Les 5 posés sont G252 (`Contents` 0). M252 rejoint la liste « têtes avec `Contents` et 0 posé ». Les 396 drapeaux de contenu sont tous des coffres globaux : l'ambiguïté disparaît.
  - Seuls 3 355 des 4 200 porteurs de butin ont le bit `0x40` (apparition automatique). 845 n'apparaissent que forcés. Idem pour 100 des 152 objets posés.
  - Les chiffres des autres lignes du tableau des codes concordent avec les miens.
- **R2.** « 24 rebonds à Gravity 3 » : je compte 23 valeurs non nulles, la 24e est le zéro final. C'est ambigu, sans conséquence.
- **R3.** Étape 4 de `DestroyEntity` : « `+0x68 = 0` » est le champ de l'effet attaché, pas celui de l'entité. Imprécis, mais l'ordre des étapes est juste.

### Non vérifié

- Les corps de `0x80033274`, `0x80033A2C`, `0x800330FC`, `0x8003382C`, `0x800336F0`, `0x800333AC` : non atteignables par le butin.
- Le contenu de `0x801ECA3C` : le rapport l'étiquette déjà UNKNOWN. J'ai confirmé que `SetGameFlag(0xFFFFFFFF)` écrit le bit 31 de `0x801EBA40+0xFFC`.
- Le premier tick de C255 et l'`IsOnGround` initial : INFERENCE, à mesurer sur la physique de la DLL.
- L'effet exact du sous-effet `0x8003BE74`.

## 2. Faits de la DLL

Tous confirmés :

- **Fichiers et lignes** : `AlundraLootRoll.cs:22/51/77/108`, `AlundraWorldProxy.cs:3267-3273` (bouchon de `DestroyEntity`), 1091, 3406, 3420, 3429, 3448, 3526, 3555. `AlundraEntityScriptProxy.cs:93/94/100/325/404/470/473/475/1214-1217`. `AlundraEntitySpawnFactory.cs:743/821-823/858-860`. `AlundraEventProgramRunner.cs:3150/3196` et `:335`. `AlundraSaveBook.cs:232`. `AlundraWaveSwordAi.cs:28`, `AlundraNativeSpriteRegistry.cs:47`. Le test épingle le bouchon à `AlundraDestroyEntityBreakEffectTests.cs:199-214`.
- **Précision de localisation** : `AddMoney`, `AddOneItemIfUnlocked`, `UseItem`, `SetPlayerMp` et `IncreaseHpMaxAndFill` sont dans `AlundraPlayerManager.cs` (lignes 2272, 2225, 2250, 1957, 1862), pas dans `AlundraWorldProxy.cs`.
- **Ce qui manque** :
  - Aucun lâcher ni C255/A255/`GiveItem`.
  - `ModdedPos` n'est écrit qu'à la création (et le clone).
  - Aucun rendu ne lit `DamagedTickCounter`.
  - `AlundraItemTables` ne garde que la colonne 2.
  - `AIValues` est un `short[10]`, sans accesseur 32 bits.
  - Le slot E d'index 11 est un no-op.
- **Précédent sain pour T2a** : le registre est clé par (slot, code) et filtre sur `SpriteType`. Les types 31 à 118 sont disjoints des types de carte (≥ 256). L'onde d'épée, apparue sans record, est le précédent.
- Les sprites objets (`alundra_66/99-102/111/114-116`) sont dans le catalogue de la DLL, avec les mêmes drapeaux, programmes 255 et tailles que dans l'EXE.
- L'effet global 14 a 3 animations exportées. Les sons 52/53/54/84 existent.
- Le lien Z de T1 (« z−1 ») est cohérent avec la doc de `SpawnEntityBySpriteIndex` : l'interface ajoute 1.

## 3. Ce que le rapport a manqué

1. **VRAM des objets lâchés.** Les 9 sprites objets lâchables portent `DestroyOnVramFlags` (`0x200000`, ex. `0x220180`, `0x230180`). C'est la pente (`0x100000`) qui n'y est pas, comme le plan le note déjà. Le binaire les détruit donc via `DestroyEntity(e,-1)` quand `+0x180 & 0x8004` (`0x800387A0-0x800387C8`).
   - La DLL ne nourrit `CombinedVramFlagsOR` que pour le héros (`RunHeroProbes`). Plan : D-G-14, O-G-2, O-E0K-2.
   - Un objet tombé dans l'eau ou un trou ne disparaîtrait donc pas. À écrire en T2a/T4 comme écart accepté, ou à planifier.
2. **T3 incomplet.** En plus de `AlundraDestroyEntityBreakEffectTests.cs:199-214`, d'autres tests épinglent le bouchon :
   - `AlundraBeannoideOracleTests.cs:361` : `Assert.Empty(SpawnedEntities)`.
   - `AlundraMurutaOracleTests.cs:439` : `Assert.Empty(SpawnedEntities)`.
   - les entrées X1/X2 exclues (`spawn_39F58`, son 84) dans `AlundraEnemyHandlerOracle.cs`, et les lignes BT-L1/BT-L3 du plan.

   Brancher `SpawnContents` casse ces assertions : elles sont à réécrire dans T3.
3. **Divergences non listées avec la décompilation**, toutes dans la branche coffre (tranche à part) :
   - A255 lit `ParentEntity` (`+0x0C`) là où le binaire lit le pointeur `+0x280` ;
   - C255 coffre teste `AIValues[2]`/`[4]` (shorts) là où le binaire teste les mots `+0x284`/`+0x288`.
4. L'**effet du bit `0x40`** (apparition auto) sur le comptage des pas du générateur à la carte n'est pas discuté (ni le fait que les 845 porteurs sans ce bit ne consomment qu'à la création forcée).

## 4. Conclusion

Le rapport est fiable sur ce qui porte les tâches T0, T1, T2a et T3 : adresses, branches, offsets, écritures, appels aléatoires, gestionnaires, état des fonctions et de la DLL. L'ordre des tâches proposé est cohérent avec l'état réel de la DLL, sous réserve de corriger le recensement (M252, 4 200 au lieu de 4 206), d'élargir T3 aux tests Beannoïde et Muruta, et de traiter le point VRAM.

Rien de ce qui précède n'invalide T1 à T3, mais il faut corriger les chiffres du §4 avant de les citer dans le plan ou une ADR.