# Contre-vérification du rapport « E19.sm1 à sm4 » (label check-e19sm, 2026-10-17)

Lecture seule sur le dépôt et sur `ALUN_CD.EXE` (France). Tous mes fichiers sont dans :
`C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/binfacts/check-e19sm/`

Je n'ai pas écrit de `report.md` : la consigne de session interdit les fichiers de rapport. Le rapport est donc ce message.

## 0. Verdict

Le rapport est fiable pour écrire les tâches sm1 à sm4. Un seul constat est réfuté : le point 4, « boucles exclusives de la décompilation ». Quelques omissions et imprécisions sont à corriger avant de recopier le texte dans les annexes (§3 et §4).

- **Adresses :** les 21 adresses de gestionnaires sont exactes, relues dans la table `0x80098FAC + 4*op`.
- **Pseudo-code :** les 21 pseudo-codes sont exacts. J'ai relu chaque gestionnaire instruction par instruction.
- **Oracle indépendant :** j'ai écrit mes propres modèles et mon propre générateur de mondes, et je les ai comparés aux VRAIS gestionnaires exécutés dans l'émulateur MIPS copié. J'ai fait 21 000 exécutions en deux passes (400 puis 1 000 par opcode). Elles donnent 0 écart.
- **Sensibilité du banc :** deux mutants sont détectés. Les bornes exclusives sur 3D, 60, B4, B2, 96 donnent 13 à 52 écarts sur 300. Le prédicat inversé de 0x3C donne 44 écarts sur 300.

## 1. Méthode

- **Fichiers copiés :** `exelib.py` et `mipsdis.py` (copiés comme demandé), puis `emu.py` et `lib.py` du dossier e19sm (copiés, lecture seule du binaire).
- **Mes outils :**
  - `dh.py` : désassemblage qui suit le flot de contrôle.
  - `scan.py` : balayage de tout le texte du binaire.
  - `stateuse2.py` : audit des écritures dans la structure d'état.
  - `myoracle.py` : l'oracle indépendant décrit plus haut.
- **Écarts avec l'oracle précédent :**
  - Mon générateur de mondes remplit de motifs toutes les zones comparées, y compris les slots au-delà de N.
  - Je compare la fenêtre mémoire entière `0x80100000..0x80190000`, ce qui attrape toute écriture non modélisée.
  - Les stubs sont les mêmes que les siens : `0x8003C914` et `0x8003C840` sont des no-op.
- **Listings :** `listings/op_*.txt` (21 gestionnaires), `listings/search_full.txt` (recherche, validateur, imprimante d'erreur), `myoracle_run.txt` (résultat des 21 000 exécutions).

## 2. Affirmations vérifiées

### 2.1 Adresses et conventions (§1, §2, §4)

- **Table et gestionnaires :** les 21 adresses de la table sont confirmées. Les tailles retournées (2 2 7 7 1 3 3 3 1 3 3 2 6 2 2 3 9 2 2 2 3) sont confirmées, y compris contre `EventOpcodeSizeTable.cs`.
- **Appel :** confirmé à `0x800422C0-0x800422F8` : `a0 = lw 0x230(s4)`, `a1 = s4`, `a2 = &pc`, `a3 = état`. Le retour 0 termine l'appel (`0x80042324`), et `sw zero,8(s3)` puis `pc += v0` sont à `0x8004232C-0x80042340`.
- **`InitializeEventData` :** `0x80041EE4` n'écrit que `+4` (`0x80041F5C`) et `+0` (`0x80041F60`) de l'état. Les mémos survivent donc à une réinitialisation.
- **Aucun appel caché :** les gestionnaires n'appellent que `0x8003C954` (recherche) et, pour 0x97, `0x8004DF10`.
- **Slots et N :**
  - Le slot i est à `0x80127D30 + 0x294*i`.
  - Les seuls écrivains de `[0x80134360]` sont `0x800386CC`, `0x80039D3C` et `0x8003B2D8`.
  - À `0x800386CC`, N vaut l'indice du dernier slot de statut non nul et différent de 4.
  - À `0x80039D3C`, N devient `max(N, idx)`, pas `idx+1`.

### 2.2 Recherche `0x8003C954` (§3)

- **Branche à identifiant brut :**
  - Le bit 0x80 clair appelle `0x8003C914` (appelé une seule fois).
  - Le statut testé est celui de la RÉFÉRENCE, `lw 0x10(s3)`.
  - La boucle va de 1 à N, et le candidat n'a aucun test de statut.
- **Groupes :** les 12 entrées de la table de sauts `0x80023C30` sont relues une à une. Les types 0 à 11 correspondent à la description du rapport :
  - Le type 4 teste le statut de la référence, `Flags&0x80`, `AnimFlags&0x80==0` et `+0x28==0`.
  - Les boucles 2 et 4 vont de 0 à N, les autres de 1 à N.
  - Les types 5 à 11 testent le statut du candidat.

### 2.3 Sm1 : 0x60, 0x61, 0x3C, 0x3D

- **0x60 (`0x8003F3F8`)** : confirmé. L'appel est à `0x8003F418`, la comparaison à `0x8003F444-0x8003F44C`, `Result := 1` à `0x8003F458` et `Result := 0` à `0x8003F468`. Il teste `res[i].+0x12C == logique`. Il n'a aucune garde de statut ni de blocage, contrairement à 0x3F (`0x8003E734`), qui a le statut 2 ou 3 et `+0x20==0`.
- **0x61 (`0x8003F488`)** : confirmé. Le test est `logique.+0x12C == res[i]` (`0x8003F4D0-0x8003F4D8`).
- **0x3D (`0x8003E64C`)** : confirmé. Les octets 1 à 6 sont non signés, les comparaisons `slt` sont signées, la boucle 0..N inclusive est à `0x8003E68C-0x8003E6F4`, et la sortie est `Result 1` ou `Result 0`, retour 7.
- **0x3C (`0x8003E558`)** : confirmé. Il ajoute `Flags&0x80!=0`, `AnimFlags&0x80==0` et `+0x28==0`. C'est aussi la clé du type 4 de la recherche.
- **Ordre d'une image :** `0x8002E100` appelle `RunMapEvents`, puis `0x8002E108` appelle `UpdateEntities`.
- **Calcul de `+0x12C` :**
  - `CheckRidingEntities` (`0x800364C8`) est appelé depuis la physique `0x80038364` (à `0x800383E4`).
  - Sa grille `(Flags&0x4100)==0x100` est confirmée.
  - Le remise à zéro à `0x80038998` (`+0x12C`, `+0x224`, `+0x220`) est confirmé.
  - Le script voit donc la valeur de la physique du tick précédent.

### 2.4 Sm2 : 0x6A à 0x6D

- **0x6A (`0x8003F8DC`)** : recopie `+0x114/118/11C` dans `+0xC/0x10/0x14` de l'état, sans toucher `Result`.
- **0x6B/6C/6D (`0x8003F908/94C/990`)** : confirmés. La différence est prise en valeur absolue 32 bits. Le seuil est `((b1|b2<<8)<<16)` en signé. `Result` vaut `!(d < seuil)`.
- **Mémos (indépendant, `stateuse2.py`) :**
  - Écrivains de `+0xC` : `0B 1C 1D 1E 1F 20 21 22 23 37 5F BB 6A`.
  - Écrivains de `+0x10` : `0B 1E 1F 5F 6A`.
  - Écrivains de `+0x14` : `5F 6A` seulement.
  - Cela correspond au tableau du §5 du rapport.
- **Corpus :** 129 sites (59+29+12+29) et 45 programmes.
  - Sur 70 lectures, 0 écrasement linéaire entre le dernier `0x6A` et la lecture.
  - Les 11 programmes de lecture sans `0x6A` (cartes 55, 56, 57) contiennent `0x37` mais pas `0x5F`.
  - Le Z n'est donc pas atteint.

### 2.5 Sm3 : A9, AA, B2, B4 à B7, 9E

- **0xA9, 0xAA** : confirmés comme décrits (`+0x130`).
- **0xB2 (`0x80041628`)** : confirmé. Il copie 65 mots du tampon, soit 16×4 plus 1. Une deuxième recherche, puis une intersection par pointeur. `Result 0` si le groupe 1 est vide.
- **0xB4, 0xB5, 0xB6** : `ForceZ > 0`, `< 0`, `== 0` du PREMIER résultat (`0x8013D8D8`). Les libellés de `EventOpcodeSizeTable.cs:228-230` sont donc faux.
- **0xB7** : `+0x88 == op2`, la recherche prend l'octet 1.
- **0x9E (`0x80040BB4`)** : confirmé. Les paramètres sont x=b1, y=b2, w=b3, h=b4, min=b5. Le pas de ligne est 416 octets (52×8), la base est `[0x800DD86C]+0x604`. `Result := count >= min`. Aucune borne. Le voisin `0x54` borne x à 0x33 et y à 0x3B (`0x8003ECBC`), ce qui est confirmé.

### 2.6 Sm4 : 0x72, 0x88, 0x96, 0x97, 0xB3

- **0x72 (`0x8003FA24`)** : `+0x26C := +0x88` et `+0x270 := +0x8C`, sur l'entité logique. La sauvegarde et la restauration du C (`0x8004213C-0x8004218C`) sont confirmées.
- **0x88 (`0x8004011C`)** :
  - La table `0x80023D2C` n'a que 3 mots, `{2, 3, 4}`. Le mot suivant est `0x312E6F4E`, soit « No.1 ».
  - Écriture de `+0x10` pour chaque résultat. `Result` est intact.
- **0x96 (`0x800409A8`)** : l'octet 1 sert de type de recherche ET de montant. L'octet 2 n'est jamais lu. Plafond à HpMax en signé. Aucun plancher.
- **0x97 (`0x80040A2C`)** : appelle `0x8004DF10`, qui est `IncreaseMp`, puis `0x8004DEBC` (`SetPlayerMp`).
  - `PlayerStats` : Mp à `+4`, MpMax à `+6`, argent à `+8` (via `[0x80176318]`).
  - `SpendMoney` est `0x8004E004`, utilisé par `0x99`. `0x98` est `0x8004DFD8` (AddMoney).
  - L'appelant `0x800335AC` est `IncreaseMpAndCreateEffect` (`a0=1`), et `0x80034948` appelle `DecreaseMp(1)`. Tout cela est confirmé.
- **0xB3 (`0x80041750`)** : quatre `sh` à `0x80126F28/2A/2C/2E`, retour 9.
  - La structure de `g_padState1` (`0x80126F18`) donne bien ButtonsHold, JustPressed, Released et Interval à `+0x10..+0x16`.
  - `RunMapEvents` précède `UpdateEntities`, et `MovePlayer` est appelé en premier dans la passe des programmes (`0x800386EC`).
  - Le site de la carte 161 (`00 10`) correspond à Haut (0x1000).

### 2.7 Divergences revendiquées avec la décompilation

| Point du rapport | Résultat |
|---|---|
| 0x97 vs SpendMoney (`EntityEventHandlers.cs:2879-2885`) | Confirmé. La décompilation commente elle-même `//8004df10 IncreaseMp` dans `PlayerManager.cs:1767`. |
| 0x3C inversé (`:1243-1271`) | Confirmé. La décompilation teste `AnimFlags&NoEntityCollision != 0` et `Flags&Collidable == 0`. Le binaire teste le contraire. |
| 0x9E | Confirmé : première case de la rangée comptée w fois, indexée par la largeur d'exécution. |
| Libellés de `EventOpcodeSizeTable.cs` (B4 à B6, 97, 88, A9, AA, 6B à 6D) | Confirmés. Les corps de B4 à B6 de la décompilation sont corrects. |
| 0xB7, 0xB2, 0xB3, 0x72, 0x88, 0x96, 0x60, 0x61, 0x6A à 0x6D | Identiques à la décompilation, hors la question de la borne (§3). |

### 2.8 DLL (vérifié par lecture du code)

- **Dispatch :** aucun des 21 opcodes n'a de `case` dans `AlundraEventProgramRunner.cs`. Ils vont dans `UnknownOpcode` (`:3115`) avec les tailles de la table.
- **Champs du proxy :** `Hp`, `HpMax`, `PlatformEntity`, `ForceZ`, `TileX/Y/Z`, `RidingEntity`, `XCollisionEntity`, `LastTarget*` sont aux lignes citées.
- **`PlatformEntity` :** jamais écrit, sauf la copie du clone (`:2596`).
- **Miroir de la vie :** `EndOfMovePlayer` écrit les statistiques depuis l'entité. Le binaire fait de même à `0x80032920-0x80032930`.
- **Prédicat de 0x3C :** `EntitySupport.IsEligibleSubject` (`:71-75`) est bien le prédicat de 0x3C.
- **Recherche :** `EntitySearchService.cs` égale le binaire pour les 12 types (relu à la main, pas seulement via la traduction python). Le piège `null == null` signalé pour les proxies de test est réel.
- **Tampon de pavé :** `AlundraTickPad` a bien des setters privés. `TickPad` est avancé en tête de tick (`AlundraWorldProxy.cs:2493`).

## 3. Réfuté ou à corriger

**R1. Point 4, « les boucles de la décompilation sont exclusives, elle perd le dernier slot » : réfuté.**
- Le rapport l'appuie sur une INFÉRENCE : la décompilation lirait `g_numberOfEntities` comme le global du binaire.
- Or la décompilation écrit ce champ avec `max + 1` : `EntityManager.cs:969`, port de `0x80038634`, et `:50`, `Index + 1`.
- Le binaire stocke l'indice `i` du dernier slot utilisé (`move t2,t1` puis `sw t2,0x4360`, `0x800386B0` et `0x800386CC`).
- Avec la valeur propre à la décompilation (N+1), ses boucles `i < g_numberOfEntities` couvrent 0..N, comme le binaire.
- L'oracle « mode décompilation » suppose `N = w.n - 1` (docstring de `decomp_search` dans `oracle.py`).
- Les 4 645 écarts dus à la « borne exclusive » (dont tous ceux de 0x60 à 0xB7) sont donc un artefact de cette hypothèse.
- Seuls restent deux vrais défauts de décompilation :
  - La branche à identifiant brut (`GameEngine.cs:1943`) fait `<= count`, donc elle dépasse d'un slot.
  - La garde de `InitializeEntity` (`EntityManager.cs:48`) teste `count < Index` au lieu de `N < Index` du binaire. Un slot créé juste après le dernier ne compte pas avant la passe de recyclage suivante.
- Ces deux défauts ne concernent pas la DLL, qui est correcte. Il ne faut simplement pas écrire « la décompilation perd le dernier slot ». Le mutant « borne exclusive » en T1 reste utile comme mutant, pas comme « divergence de la décompilation ».
- Le même raisonnement a déjà été écrit pour 0x3F dans le commentaire de la DLL (`AlundraEventProgramRunner.cs:2011-2013`).

**R2. « 12 ou plus : message et 0 résultat » (§3) : à nuancer.**
- `0x8003CEF0` appelle `0x8003C840` (chaîne « Illegal Distination! »).
- Cette fonction imprime plusieurs lignes, puis fait `jal 0x800831E0` avec `a0=1`. Le stub à `0x800831E0` est `t2=0xB0 ; jr t2 ; t1=0x38`.
- INFÉRENCE : c'est l'appel BIOS B0:38, `exit`. Je ne peux pas le prouver depuis le dépôt, la table BIOS n'y étant pas.
- Le « 0 résultat » n'est donc jamais observé. L'oracle l'ignore parce qu'il neutralise l'imprimante.

## 4. Omissions (rien de bloquant)

**O1. Le validateur d'identifiant brut est fatal.**
- Le rapport dit « résultat ignoré ». C'est vrai pour la valeur retournée.
- Mais `0x8003C914` appelle `0x80039BD0`. Ce dernier renvoie 0 si l'id est négatif, supérieur ou égal à `[0x80126E84]`, ou si l'octet `+4` de l'enregistrement vaut 0.
- Dans ce cas, `0x8003C914` appelle `0x8003C840` (« Illegal InitData Number!! ») puis le même appel BIOS.
- Sur des données valides, c'est sans effet. Cela explique pourquoi les ids bruts du corpus sont forcément valides dans le jeu d'origine. Un test de cohérence « id brut du corpus ∈ enregistrements de la carte » serait peu coûteux.

**O2. 0x9E, « le bit 2 » est ambigu.**
- Le masque est `andi 0x2`, valeur 0x0002 (`0x80040C1C`), donc le bit d'indice 1, et non 0x0004.
- Le modèle de l'oracle écrit bien `& 2`. Seule la prose est ambiguë.
- À écrire explicitement dans la tâche. Cela correspond à `(Walkability & 2)` de la décompilation, et aux masques que `0x54` pose par `or` dans le même halfword.

**O3. Porte de gel manquante.**
- `RunMapEvents` (`0x8003C69C-0x8003C6A0`) et `UpdateEntities` (`0x8003B394-0x8003B398`) sont courts-circuités quand `[0x800DC4B8] & 0x48 != 0`. Dans ce cas seule `0x800384F4` tourne.
- Cela précise « événements de carte puis entités » pour 0xB3 (§0.10) et 0x60/0x61 : l'ordre ne vaut que si cette porte est ouverte.

**O4. Étiquette de `0x800384F4`.**
- Le rapport l'appelle « coups ». Elle remet à zéro les compteurs de listes (`sw zero,0x7D2C`) et construit les listes active et collidable. Le nom de la décompilation, `UpdateEntityLists`, est juste.
- Cela ne change pas l'ordre : programmes, `0x80038998`, listes, horloge, physique.

**O5. Analyse des mémos : trou de couverture.**
- `memo_flow.py` ne compte que 25 lectures X sur 29. Les 4 manquantes, carte 307 C[1] @433 et @451, C[2] @498 et @516, sont masquées parce que le CFG ne suit pas la continuation d'un `0x78` (appel, retour par `0x7D`).
- J'ai vérifié à la main : le sous-programme appelé (`@474` : `6E 7F 1B 7D`) ne contient aucun écrivain de P2. La conclusion « 0 écrasement » reste vraie.
- À mentionner dans la tâche de test statique de sm2. Quatre appels `0x78/7B/7C` se trouvent entre un `0x6A` et une lecture, et aucun callee n'écrase.

**O6. Contexte logique des programmes B.**
- `RunMapEvents` appelle `RunScript` avec `a0` = le héros et initialise le contexte logique mémorisé à `s1` = le héros (`0x8003C800`).
- Donc `96 80 32` d'une salle de sauvegarde (carte 452 B[2]) soigne bien le héros, sauf `0x42/0x43` préalable. Ce fait manque au rapport, mais le sens attribué aux sites est cohérent.

**O7. Formulation à clarifier (sm2).**
- « Une lecture 0x6D SANS 0x6A compare `|0 - PosZ|` et ne bascule qu'à l'égalité exacte (INFÉRENCE) » : la phrase est confuse. Avec P4=0, `Result = (|PosZ| >= seuil)`.
- Seul l'écart de convention Z DLL/binaire d'une unité brute (ADR-0026) ne change le résultat qu'à l'égalité exacte du seuil.

## 5. Constats complémentaires

- **Compteurs de tuiles :** le binaire calcule `TileX` par une table `pixel→tuile` remplie à `0x8003B198-0x8003B1D0` (52×24, soit pixel/24). `TileY` et `TileZ` valent `PosY>>20` et `PosZ>>20`. Le rafraîchissement a lieu en fin de la passe de physique (`0x80038064`, depuis `0x800384BC`). La DLL donne le même résultat (24 px, 16 px).
- **Rapport 0x3D, pas de vérif complète :** je n'ai pas relevé de décalage de timing des tuiles entre DLL et binaire pour les entités bloquées. Cela reste hors du périmètre.

## 6. Non vérifiables depuis le binaire

- **Sens des sites :** « plaques/blocs », « pousser un bloc », et si les cavaliers réels passent la grille `(Flags&0x4100)==0x100`. C'est à mesurer en T0, comme le rapport le demande.
- **Z unitaire :** que les cartes 55 à 57 soient atteignables (Q-sm-9).
- **HUD :** que l'HUD « remonte par roulement ».
- **DLL :** l'exécution du C# n'a pas été faite, je l'ai relu seulement.
- **Plafond de vie :** `HpMax ≤ 51` (DLL : cap 0x32, `AlundraPlayerManager.cs:1885`), donc +50 et +128 soignent pareil. Cela ne vaut que pour la DLL telle quelle.

## 7. Fiabilité

Les tâches peuvent être écrites à partir du rapport, après ces corrections :
1. Supprimer la phrase « la décompilation perd le dernier slot » et la colonne d'écarts « mode décompilation » de l'annexe, ou la requalifier (R1).
2. Écrire le masque `0x0002` pour 0x9E (O2).
3. Ajouter O1, O3 et O5 aux notes de T0/T1 (sans changer le périmètre des slices).
4. Garder la décision « suivre le binaire à la lettre » pour 0x96, 0x97 et 0x3C. Elle est confirmée.

## 8. Fichiers

- Scripts : `dh.py`, `scan.py`, `stateuse2.py`, `myoracle.py`, `tbl.py`, `emu.py`, `lib.py`, `exelib.py`, `mipsdis.py`, `corpus_copy.py`, `mut1.py`, `mut2.py`.
- Sorties : `listings/op_*.txt`, `listings/search_full.txt`, `stateuse2.txt`, `myoracle_run.txt`, dossier `theirs/` (copie de `memo_flow.py` et du patch qui liste les lectures non visitées).