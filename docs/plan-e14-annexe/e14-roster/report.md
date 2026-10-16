# enemies2 : roster complet de l'IA native des sprites (Alundra vers CasaEngine)

Lecture seule du dépôt, état du 2026-10-17 : branche `chantier/script-types-full-name`, HEAD `7d776b3` (DLL `Alundra/Scripts/`, annexes `docs/`). Tout vient de scripts du dossier de travail (section 9). Étiquettes : **FAIT** (vu dans le code ou les données, avec sa source), **INFÉRENCE** (déduit, la déduction est écrite), **INCONNU** (non établi).

## 0. Résultat

- **FAIT** : 483 cartes, 9 741 enregistrements `Entities`, dont 9 631 activés (`IsEnabled`). 395 types de sprite ont un en-tête (151 page globale, 244 page carte) ; 281 sont dans au moins un enregistrement activé.
- **FAIT** : le binaire a 203 entrées non nulles dans les tables de gestionnaires natifs (201 adresses distinctes) : A 24, C 103, D 45, E 29, F 2. Elles correspondent une à une à `SpriteEventHandlers.cs` ; les cinq `AI_EmptyFunction` de la décompilation (A13, C0, D0, D4, F0) sont des mots nuls dans le binaire.
- **FAIT** : 24 007 « instances » de clé native (enregistrement x emplacement natif, sans les entrées nulles C0, D0, F0) ; la DLL en couvre 7 506 (31 %), surtout E0/E1 en ligne. Pour les ennemis et les boss : 2 309 sur 5 651.
- **Ennemis** (classe `enemy` ou `boss`, définition section 3) : **52 types avec enregistrements** (1 566 enregistrements sur 185 cartes) dont 13 boss, et **17 types ennemis sans enregistrement** (nés d'un `SpawnWarpEntity`). À côté : 27 projectiles hostiles, 14 objets rattachés à un ennemi, 18 dangers non ennemis (1 062 enregistrements).
- **Portés aujourd'hui** : 2 types ennemis, Beannoïde (335, partiel) et Muruta (347, complet pour ses clés d'en-tête). **50 types avec enregistrements n'ont aucune clé native portée** (1 233 enregistrements sur 171 cartes), boss compris (13). Le résultat est très au-dessus des quatre types non portés d'une passe antérieure, qui ne comptait que les utilisateurs de A1, D1, C1, C10 et D9.
- **Côté histoire** (seul ordre connu : les 37 cartes de `docs/plan-e19-opcodes.md`) : les types non portés qu'on y rencontre sont Poisson et Guêpe (carte 14, jour 2), Orc (masse) (carte 15, jour 2) et Slime géant grand/petit (carte 44, jour 3). Tous les autres apparaissent hors de la chaîne connue : l'ordre est **INCONNU** pour eux.
- **Trouvailles qui changent le plan** : (a) E6, E7, E8, E10 et E27 ont un corps identique octet pour octet à E0/E1 (`DestroyEntity(e, -1)`), alors que la DLL ne traite en ligne que 0, 1 et 10 ; (b) A254, C24, C57 et D25 sont des fonctions vides (`jr ra`) : rien à porter ; (c) dix codes D ont exactement la forme de `HitReaction` (D1, D6, D7, D8, D9, D11, D13, D17, D19, D24 : 25 mots, seule la constante d'animation change) ; (d) le registre refuse une seconde inscription d'une même clé, alors que D6 et D11 servent chacun 8 types ; (e) cinq aides du binaire manquent à la DLL et `CreateEffectEntity` n'est porté que pour l'effet de bris (section 6) ; (f) E11 n'est pas un alias de E1 : c'est la destruction récursive des enfants d'un boss ; (g) la décompilation ajoute des gardes de nom (`entity.Name != ...`) qui donnent, par gestionnaire, les noms de sprite attendus : elles recoupent le recensement dans 132 cas sur 161.

## 1. La règle de répartition, ré-établie

**FAIT, binaire** (`ALUN_CD.EXE` France, `D:/development/repo/Alundra Remake/Alundra (France)/Alundra (France)_extracted/ALUN_CD.EXE`, désassemblé avec capstone, `mipsdis.py`) :

1. `0x800388F8-0x80038944` (passe des événements) : pour l'emplacement en attente `s` (`entity+0x228`), si `entity[+0x4C+4*s] & 0x7F == 0` la fonction `table[s][ entity[+0x70+4*s] ]` est appelée, **sauf si le mot de la table est nul** (`beqz` en `0x80038934` : aucun appel) ; sinon le programme d'octets `0x8004205C` tourne. Les tables sont atteintes par le tableau maître `0x80098F4C` = {A `0x800C4B34`, B 0, C `0x800C4F34`, D `0x800C5334`, E `0x800C5734`, F `0x800C5B34`}, 256 mots chacune.
2. `InitializeCodePrograms` `0x8004201C` : si l'entité a un enregistrement (`entity+0x44`), `entity[+0x4C+4*i] = (octet signé) enregistrement[0x0A+i]` pour i = 0..5 (A, B, C, D, E, F). Une entité née sans enregistrement (`SpawnWarpEntity`) garde 0 : tous ses emplacements sont natifs.
3. `InitializeEntity` `0x80039DD4-0x80039E10` : les octets d'en-tête `+0x13..+0x17` (ProgramLoad, Tick, Touch, Deactivate, Interact) sont copiés en `entity+0x70` (A), `+0x78` (C), `+0x7C` (D), `+0x80` (E), `+0x84` (F) ; `+0x74` (B) vaut 0.

Donc : **un emplacement tourne en natif quand l'index de l'enregistrement, masqué par `0x7F`, est 0 ; le gestionnaire est celui que nomme l'en-tête du sprite.** Le bit `0x80` de l'index choisit la table de la carte ; clair avec un index non nul, c'est la table globale : 6 enregistrements seulement, tous du type 409 (Pot magique), emplacement C. C'est la règle de `dll-natives.md` §0 point 3, confirmée dans le binaire.

**FAIT, données** : `census.py` (enregistrements activés de `data-extracted/data/map_N.json`, 483 cartes) reproduit à l'unité `docs/plan-e14-annexe/e14a/dll-natives.md` §0.3 : 9 631 enregistrements activés ; Beannoïde 130 enregistrements sur 18 cartes ; Muruta 203 sur 25 ; A1 : 335 x120, 329 x118, 401 x84, 410 x76, 395 x4 ; D1 : 335 x130, 329 x118 ; C1 : 335 x105 ; C10 : 347 x28 ; D9 : 347 x198. Les sept champs d'en-tête lus (cinq codes, `BreakEffect`, `Contents`) sont identiques d'une carte à l'autre et à `alundra-project/Data/sprite-records.json` (0 écart sur les 244 types de la page carte et les 151 de la page globale).

**FAIT (réconcilié)** : l'annexe compte « 121 couples (emplacement, code) distincts, code non nul ou emplacement A » ; ce recensement trouve 120 couples à code non nul atteints par au moins un enregistrement, plus A0 : 121.

**Type de sprite** : `SpriteTableIndex`, plus `0x100` quand `SpriteDirection & 0x80` (page carte) : `GameEngine.SpawnEntity` (décompilation) et `EntityRecordMapper.ResolveSpriteType` (`Alundra/Scripts/EntityRecordMapper.cs:226`). Un enregistrement n'existe que si `IsEnabled != 0` (`GameEngine.GetEntityRecord`) ; il naît à la charge de la carte seulement si `SpriteDirection & 0x40`, sinon par script (`0x2D`, `0x8A`, `0x8B`).
Parmi les enregistrements ennemis et boss : 826 naissent à la charge, 740 seulement par script. L'emplacement C est natif d'emblée pour 1030 enregistrements et scripté pour 536 ; 390 occurrences de `0x40 [C, 0]` dans leurs programmes scriptés le rendent natif ensuite (le hall des Muruta de la 115 est le cas connu).

## 2. Ce que la DLL exécute aujourd'hui

| clé | types | inscription (registre de production) |
|---|---|---|
| A0 | 347 Muruta (griffes) Niv.1 | Alundra/Scripts/AlundraMurutaAi.cs:43 |
| A1 | 335 Beannoïde | Alundra/Scripts/AlundraBeannoideAi.cs:29 |
| C1 | 335 Beannoïde | Alundra/Scripts/AlundraBeannoideAi.cs:30 |
| C10 | 347 Muruta (griffes) Niv.1 | Alundra/Scripts/AlundraMurutaAi.cs:44 |
| C61 | 27 Épée d’onde Niv.1, 28 Épée d’onde Niv.2, 29 Épée d’onde Niv.3 | Alundra/Scripts/AlundraWaveSwordAi.cs:28 |
| D1 | 335 Beannoïde | Alundra/Scripts/AlundraBeannoideAi.cs:31 |
| D9 | 347 Muruta (griffes) Niv.1 | Alundra/Scripts/AlundraMurutaAi.cs:45 |

- **FAIT** : E0, E1 et E10 sont traités en ligne **pour tout type** (`AlundraEventProgramRunner.IsInlineDeactivateCode`, `AlundraEventProgramRunner.cs:378`) : `DestroyEntity(e, -1)` avec l'effet de bris de l'en-tête, sans butin (le butin au décès est E20.e0c, `docs/plan-e20-heros.md:605-632`).
- **FAIT** : le livre de sauvegarde (type 237) : F1 et C72 (`AlundraSaveBook.cs`).
- **FAIT** : toute autre entrée est un no-op compté (`RunSpriteEvent`, `AlundraEventProgramRunner.cs:314-374`), ce que fait aussi le binaire pour un mot nul et pour les quatre fonctions vides.
- **FAIT** : `AlundraNativeSpriteRegistry.Register` **lève une exception** si la clé (emplacement, code) est déjà inscrite (`AlundraNativeSpriteRegistry.cs:47-60`). Or les codes sont partagés par construction (section 5.2) : toute tranche qui ajoute un type à A1, D1, C1, C10 ou D9, ou qui inscrit D6 ou D11 pour un deuxième type, bute dessus. Il faut une inscription cumulative des types (prérequis de S0).
- **FAIT** : le Beannoïde n'est que partiel. Dix de ses enregistrements (cartes 472 et 473) exécutent dans un programme A scripté `0x41 [C,99]` (x9), `0x41 [D,44]` (x10) et `0x41 [C,96]` (x1), qui remplacent le code du sprite par C99, D44 et C96 ; ces trois clés ne sont pas inscrites. **INFÉRENCE** : elles ne tournent que si l'emplacement est natif à ce moment-là. Hors chaîne de l'histoire.
- **FAIT** : A0 n'est inscrit que pour le type 347 ; les 6 722 enregistrements dont A est natif avec le code 0 ne l'exécutent pas (invisible tant que rien ne lit `AIValues[0]`, décision AD-3 de `dll-natives.md`).

## 3. Classification, preuves

Aucune classe n'est posée sur un nom seul. Signaux, par ordre de force :

1. **FAIT, en-tête + binaire** : `MoreFlags` (octet d'en-tête `0x10`) porte les classes de combat (`Gameplay/EntityFlags.cs` de la décompilation, bits 0 à 3 : ClassA = frappé par le héros, HitsClassB = blesse le héros ; le héros a `0x8C`). Un type est `enemy` quand il est frappable (ClassA) **et** que son gestionnaire D natif appelle `ComputeNewHp` `0x8003A374` (vérifié par les appels `jal` de l'étendue du gestionnaire dans le binaire). Il est `boss` s'il a en plus une preuve de boss (point 5).
2. **FAIT, binaire** : projectile hostile = blesse le héros sans être frappable (HitsClassB seul), avec bits de désactivation ou nom de projectile ; arme du héros = types 1 à 24 et 27 à 29 de la page globale (noms).
3. **FAIT, en-tête** : portrait de dialogue présent (`FlagsPortraitShadowType & 0x80`) = PNJ qui parle.
4. **FAIT, en-tête** : objet ramassable = en-tête A255/C255 (C255 est le gestionnaire de l'objet ramassable, `docs/plan-e20-heros.md:547`).
5. **Boss** : *FAIT de placement* si un code de l'en-tête est enregistré dans `Gameplay/Scripts/Boss/` de la décompilation (A22, C40, C41, C77, C88, C90). *INFÉRENCE* sinon : tous les enregistrements dans une carte nommée `(Boss)` ou `Arena`, ou au plus 5 enregistrements dont un dans une telle carte, ou nom contenant `Boss`, ou nom de fonction C de la décompilation contenant Boss, Twin, Watcher, Melzas ou Special.
6. **INFÉRENCE (recoupement, pas un critère)** : le marqueur `◆` du nom (`EntityNames.csv`, submodule de l'analyseur, colonnes japonaise et française, 106 lignes). Les 106 types `◆` tombent tous dans enemy, boss, projectile hostile ou objet rattaché ; aucun dans PNJ, objet ou objet ramassable. Hors `◆`, seuls les types 150 à 153 (projectiles génériques de la page globale) sont classés projectile hostile.

| identifiant (roster.tsv) | libellé | types | types avec enregistrement | enregistrements |
|---|---|---|---|---|
| `object/prop` | objet ou décor | 108 | 105 | 5608 |
| `enemy` | ennemi | 39 | 39 | 1539 |
| `NPC` | PNJ | 43 | 43 | 1212 |
| `hazard/object (hurts the hero)` | danger qui blesse le héros, non frappable | 13 | 13 | 864 |
| `hazard/object (hittable and hurts the hero, no native hit handler)` | danger frappable et blessant, sans D natif qui appelle ComputeNewHp | 5 | 5 | 198 |
| `item (pickup)` | objet ramassable (item) | 88 | 54 | 153 |
| `boss` | boss | 13 | 13 | 27 |
| `projectile/effect (hostile)` | projectile ou effet hostile | 27 | 3 | 16 |
| `projectile/effect (hero weapon)` | arme ou magie du héros | 18 | 2 | 7 |
| `enemy-attached object (diamond name; hittable and hurts, no native hit handler)` | objet rattaché à un ennemi (nom diamant, frappable et blessant, sans D natif) | 11 | 2 | 3 |
| `enemy-attached object (diamond name, no combat bits)` | objet rattaché à un ennemi (nom diamant, sans bits de combat) | 3 | 1 | 3 |
| `object (hero item: beans/bomb)` | objet du héros (haricots, bombe) | 2 | 1 | 1 |
| `hero` | héros | 1 | 0 | 0 |
| `unused slot` | emplacement inutilisé | 7 | 0 | 0 |
| `enemy (spawned only, no map record)` | ennemi né à l'exécution (aucun enregistrement) | 17 | 0 | 0 |

Les noms viennent de `alundra-datas-analyser/AlundraTools/AlundraTools/EntityNames.csv` (ligne i = type i ; 0-255 page globale, 256-511 page carte), la table que charge aussi le moteur d'origine selon `EntityNameCatalogReader.cs`. Les noms de carte viennent de `alundra-casaengine-project-converter/maps.json` (catalogue écrit à la main). Les gardes de nom de la décompilation (« Homme-lézard », « Zazan-Roi Muruta », « Tortue de roche », « Homme momie », « Homme de bois », « Homme-ombre », « Homme de boue », « Slime géant » dans `FunctionTypeD.cs`) sont analysées en 5.4.

## 4. Les ennemis

### 4.1 Cartes de la chaîne de l'histoire

Source de l'ordre : `docs/plan-e19-opcodes.md:382` (chaîne 389 à 163), `:2564-2566` (jours 1 à 4), `:4118` et `:4153` (cartes de combat 14, 15, 44, 115-117, 362). L'ordre à l'intérieur d'une phase n'est pas donné par les plans ; les cartes 177, 180, 181, 182, 184 sont dans la chaîne sans jour précisé. `*` = type déjà porté (Beannoïde partiel).

| phase | carte | ennemis (enregistrements) |
|---|---|---|
| 3-jour2 | 14 Overworld 3,1 | Beannoïde x7*, Guêpe Niv.1 x3, Poisson Niv.1 x2 |
| 3-jour2 | 15 Overworld 3,2 | Orc (masse) Niv.1 x3, Muruta (griffes) Niv.1 x3* |
| 4-Tarn | 115 Tarn's Manor | Muruta (griffes) Niv.1 x11* |
| 4-Tarn | 116 Tarn's Manor | Beannoïde x11* |
| 4-Tarn | 117 Tarn's Manor (unused broken) | Beannoïde x5* |
| 5-jour3 | 44 Wendels Nightmare | Beannoïde x14*, Slime géant (grand) x1, Slime géant (petit) x1 |
| 6-jour4 | 362 Coal Mine / Olen's Dream | Muruta (griffes) Niv.1 x4* |

Les autres cartes de la chaîne (389, 390, 476, 478, 392, 391, 416, 163, 162, 165, 164, 169 à 175, 10, 179, 176, 135, 178, 183, 185, 177, 180 à 182, 184) n'ont aucun enregistrement ennemi activé. **FAIT** : les types portés y sont Beannoïde (14, 44, 116, 117) et Muruta (15, 115, 362). **INFÉRENCE** : les cinq premiers types non portés que le joueur rencontre sont Poisson, Guêpe, Orc (masse), Slime géant grand et petit ; la suite, au-delà du jour 4, est **INCONNUE**.

### 4.2 Classement des ennemis non portés avec enregistrements

`1re carte histoire` = première carte de la chaîne ci-dessus où le type apparaît, `-` sinon ; `prof.` = profondeur minimale en sauts depuis la carte 389 par les portails et les `0x53` (**INFÉRENCE** : indice, pas l'ordre de l'histoire ; les villes-carrefours aplatissent tout). `mots` = étendue majorée des clés manquantes dans le binaire (hors A0). `(n)` après une clé = nombre de types ennemis ou boss qui la réclament.

**Par nombre de cartes** (puis enregistrements) :

| type | id | enreg. | cartes | 1re carte histoire | prof. | 3 premières cartes (id) | clés manquantes | mots | clés partagées |
|---|---|---|---|---|---|---|---|---|---|
| Slime gélatineux | 329 | 118 | 18 (dont 1 test/inutilisées) | - | 8 | 9 Overworld 2,0, 13 Overworld 3,0, 22 Lars' Crypt | A1 C26 D1 | 272 | A1(4) |
| Tortue de roche Niv.1 | 437 | 75 | 16 (dont 1 test/inutilisées) | - | 7 | 3 Overworld 0,2, 8 Overworld 1,3, 12 Overworld 2,3 | A2 C3 D3 | 312 | A2(2) C3(2) D3(2) |
| Mimming (faux) Niv.1 | 377 | 65 | 14 | - | 10 | 17 Overworld 4,0, 35 Nirude’s Lair, 38 Nirude’s Lair | A0 C13 D11 | 233 | D11(8) |
| Orc (masse) Niv.1 | 341 | 61 | 13 (dont 3 test/inutilisées) | 15 [3-jour2] | 9 | 3 Overworld 0,2, 7 Overworld 1,2, 11 Debug zone | A0 C16 D12 | 288 | D12(2) |
| Homme-insecte Niv.1 | 401 | 84 | 12 | - | 9 | 86 Magyscar, 87 Magyscar, 88 Magyscar | A1 C7 D6 | 529 | A1(4) D6(8) |
| Homme-lézard (épée) Niv.1 | 362 | 110 | 11 | - | 8 | 16 Overworld 3,3, 43 Surferboy Nightmare (outer), 80 Lizardman's Lair | A0 C2 D2 | 288 | D2(4) |
| Poisson Niv.1 | 392 | 48 | 10 | 14 [3-jour2] | 7 | 4 Overworld 0,3, 8 Overworld 1,3, 12 Overworld 2,3 | A0 C19 D6 | 168 | D6(8) |
| Muruta (arc) Niv.1 | 350 | 39 | 10 | - | 10 | 148 Murgg Tree (inner), 149 Murgg Tree (inner), 150 Murgg Tree (inner) | A0 C33 D13 | 309 |  |
| Muruta (boomerang) Niv.1 | 356 | 30 | 10 | - | 9 | 2 Overworld 0,1, 111 Nestus' Dream, 114 Tarn's Manor | A0 C35 D6 | 370 | D6(8) |
| Mimique Niv.1 | 404 | 22 | 10 | - | 9 | 5 Overworld 1,0, 101 Giles Nightmare, 139 Cave (Fire manor) | A3 C6 D5 | 193 | A3(2) D5(6) |
| Ver Niv.1 | 410 | 76 | 9 | - | 9 | 18 Overworld 4,1, 19 Overworld 4,2, 26 Ancient Shrine | A1 C29 D6 | 270 | A1(4) D6(8) |
| Homme-lézard (projectiles) Niv.1 | 371 | 40 | 9 | - | 9 | 80 Lizardman's Lair, 81 Lizardman's Lair, 82 Lizardman's Lair | A0 C34 D2 | 384 | D2(4) |
| Homme-ombre Niv.1 | 440 | 40 | 8 | - | 10 | 100 Giles Nightmare, 101 Giles Nightmare, 102 Giles Nightmare | A4 C20 D6 | 445 | D6(8) |
| Orc (hache) Niv.1 | 338 | 33 | 8 (dont 2 test/inutilisées) | - | 9 | 4 Overworld 0,3, 5 Overworld 1,0, 18 Overworld 4,1 | A0 C15 D12 | 250 | D12(2) |
| P-Zoldia Niv.1 | 452 | 26 | 8 (dont 1 test/inutilisées) | - | 11 | 47 Unused Dream (Boss) (Beta Surferboys Dream)?, 54 Elenes Nightmare, 103 Giles Nightmare | A0 C12 C78 D10 D35 | 1587 | C12(2) D10(2) |
| P-Zoldia Niv.2 | 453 | 30 | 7 | - | 12 | 338 Lake Shrine (inner), 339 Lake Shrine (inner), 341 Lake Shrine (inner) | A0 C12 D10 | 655 | C12(2) D10(2) |
| Fourneau Niv.1 | 467 | 37 | 6 | - | 10 | 88 Magyscar, 90 Magyscar, 92 Magyscar | A0 C28 D5 | 347 | D5(6) |
| Homme momie Niv.1 | 425 | 34 | 6 | - | 15 | 27 Ancient Shrine, 29 Ancient Shrine, 30 Ancient Shrine | A0 C5 D5 | 270 | C5(2) D5(6) |
| Oiseau de feu Niv.1 | 473 | 35 | 5 (dont 1 test/inutilisées) | - | 11 | 59 Torla Buggy? with bar music and random texts (early version of Torla), 444 Torla (inner), 445 Torla (inner) | A7 C30 D11 | 494 | D11(8) |
| Champignon Niv.1 | 443 | 26 | 5 | - | 9 | 2 Overworld 0,1, 382 Murgg Caves, 383 Murgg Caves | A6 C25 D6 | 307 | D6(8) |
| Homme-lézard (lance) Niv.1 | 365 | 25 | 5 | - | 9 | 80 Lizardman's Lair, 82 Lizardman's Lair, 83 Lizardman's Lair | A0 C32 D2 | 384 | D2(4) |
| Homme momie Niv.2 | 454 | 11 | 5 | - | 14 | 339 Lake Shrine (inner), 340 Lake Shrine (inner), 341 Lake Shrine (inner) | A0 C5 D5 | 270 | C5(2) D5(6) |
| Homme de boue Niv.1 | 431 | 16 | 4 | - | 8 | 16 Overworld 3,3, 70 Coastal Cave, 72 Coastal Cave | A0 C31 D11 | 299 | D11(8) |
| Mimming (rotation) Niv.1 | 386 | 15 | 4 | - | 11 | 38 Nirude’s Lair, 39 Nirude’s Lair, 40 Nirude’s Lair | A0 C14 D11 | 201 | D11(8) |
| Zombie Niv.1 | 449 | 15 | 4 | - | 8 | 21 Lars' Crypt, 22 Lars' Crypt, 24 Lars' Crypt | A0 C21 D11 | 208 | D11(8) |
| Roper Niv.2 | 456 | 15 | 4 | - | 12 | 337 Lake Shrine (inner), 338 Lake Shrine (inner), 351 Lake Shrine (inner) | A0 C11 D6 | 255 | C11(2) D6(8) |
| Guêpe Niv.1 | 455 | 13 | 4 | 14 [3-jour2] | 7 | 12 Overworld 2,3, 14 Overworld 3,1, 30 Ancient Shrine | A0 C18 D11 | 285 | D11(8) |
| Slime géant (œil) | 471 | 6 | 4 | - | 12 | 435 Elane Nightmare, 436 Elane Nightmare, 437 Elane Nightmare | A21 D40 | 71 | D40(2) |
| Loup-garou Niv.1 | 482 | 5 | 4 | - | 8 | 226 Inoa, 229 Inoa (inner), 274 Inoa (inner) | A0 C37 D11 | 217 | D11(8) |
| Zorgia (boss) | 509 | 4 | 4 | - | 11 | 321 Arena Zorgia (Boss), 388 The Klark - inner, 432 Nava''s House (inner) | A0 C44 D19 E11 | 623 | E11(8) |
| Fantôme Niv.1 | 428 | 16 | 3 | - | 8 | 21 Lars' Crypt, 351 Lake Shrine (inner), 353 Lake Shrine (inner) | A0 C9 D8 | 264 |  |
| Tortue de roche Niv.2 | 441 | 14 | 3 | - | 10 | 443 Torla (inner), 445 Torla (inner), 446 Torla (inner) | A2 C3 D3 | 312 | A2(2) C3(2) D3(2) |
| Melzas (corps principal) (boss) | 418 | 3 | 3 | - | 5 | 327 Arena Melzas 1 (Boss), 347 Lake Shrine (inner), 415 Lake Shrine Melzas (inner, Beginning Event?) | A17 C55 D28 E11 | 534 | E11(8) |
| Monsieur Aspiration (boss) | 476 | 3 | 3 | - | 10 | 99 Giles Nightmare, 123 Kline's Nightmare, 449 Giles Nightmare (Boss) | A0 C48 D22 E11 | 669 | E11(8) |
| Élément Niv.1 (boss) | 497 | 3 | 3 | - | 10 | 25 Lars' Crypt, 34 Ancient Shrine - Golem, 399 Murgg Golem (Boss) | A10 C40 D17 | 454 |  |
| Zazan-Roi Muruta (boss) | 506 | 3 | 3 | - | 9 | 153 Murgg Tree (inner), 329 Coal Mine (Elevator & Zazan & Murgg), 368 Arena Zazan (Sybil's vision) | A0 C43 D2 E11 | 760 | D2(4) E11(8) |
| Roper Niv.1 | 389 | 12 | 2 | - | 11 | 50 Elenes Nightmare, 54 Elenes Nightmare | A0 C11 D6 | 255 | C11(2) D6(8) |
| Homme de bois Niv.1 | 461 | 6 | 2 | - | 9 | 4 Overworld 0,3, 13 Overworld 3,0 | A3 C27 D5 | 372 | A3(2) D5(6) |
| Dragon (boss) | 479 | 2 | 2 | - | 15 | 323 Arena Black Dragon (Boss), 480 Lake Shrine (Melzas, Dark Dragon Event) | A0 C49 D24 E11 | 1274 | E11(8) |
| Mille-pattes (corps principal) (boss) | 488 | 2 | 2 (dont 1 test/inutilisées) | - | 13 | 95 Unused Nightmare, 322 Arena Corpse Worm (Boss) | A12 C47 D21 E11 | 928 | D21(5) E11(8) |
| Boss lézard Niv.1 (boss) | 494 | 2 | 2 | - | 10 | 84 Lizardman's Lair, 411 Lizardman's Lair (Boss) | A9 C39 D16 | 619 |  |
| Tête de roche Niv.1 | 395 | 4 | 1 | - | 10 | 78 Underground Watercourse | A1 C8 D7 | 319 | A1(4) |
| Orc (armure de fer) Niv.1 | 344 | 2 | 1 | - | 10 | 3 Overworld 0,2 | A0 C17 D5 | 240 | D5(6) |
| Wilda (tête) (boss) | 412 | 1 | 1 | - | 15 | 325 Arena Wilda (Boss) | A15 C51 D26 | 1280 |  |
| Ronan divin corrompu (boss) | 446 | 1 | 1 | - | 8 | 326 Arena Ronan (Boss) | A16 C54 D27 E11 | 743 | E11(8) |
| Slime géant (grand) | 459 | 1 | 1 | 44 [5-jour3] | 10 | 44 Wendels Nightmare | C102 D46 | 271 | C102(2) D46(2) |
| Slime géant (petit) | 460 | 1 | 1 | 44 [5-jour3] | 10 | 44 Wendels Nightmare | A0 C102 D46 | 271 | C102(2) D46(2) |
| Après Melzas (boss) | 463 | 1 | 1 | - | 19 | 471 Arena Melzas 2 (Boss) | A22 C88 D42 E11 | 221 | E11(8) |
| Abyss Niv.1 (boss) | 485 | 1 | 1 | - | 14 | 76 Coastal Cave (Boss) | A8 C38 D14 | 837 |  |
| Frère Rayon (aîné) Niv.1 (boss) | 500 | 1 | 1 | - | 10 | 109 Nestus' Dream | A11 C46 D20 | 1122 | D20(2) |

**Par première carte de l'histoire** (rang de phase, puis profondeur ; les types hors chaîne sont classés par profondeur, **INFÉRENCE**) :

| type | id | enreg. | cartes | 1re carte histoire | prof. | 3 premières cartes (id) | clés manquantes | mots | clés partagées |
|---|---|---|---|---|---|---|---|---|---|
| Poisson Niv.1 | 392 | 48 | 10 | 14 [3-jour2] | 7 | 4 Overworld 0,3, 8 Overworld 1,3, 12 Overworld 2,3 | A0 C19 D6 | 168 | D6(8) |
| Guêpe Niv.1 | 455 | 13 | 4 | 14 [3-jour2] | 7 | 12 Overworld 2,3, 14 Overworld 3,1, 30 Ancient Shrine | A0 C18 D11 | 285 | D11(8) |
| Orc (masse) Niv.1 | 341 | 61 | 13 (dont 3 test/inutilisées) | 15 [3-jour2] | 9 | 3 Overworld 0,2, 7 Overworld 1,2, 11 Debug zone | A0 C16 D12 | 288 | D12(2) |
| Slime géant (grand) | 459 | 1 | 1 | 44 [5-jour3] | 10 | 44 Wendels Nightmare | C102 D46 | 271 | C102(2) D46(2) |
| Slime géant (petit) | 460 | 1 | 1 | 44 [5-jour3] | 10 | 44 Wendels Nightmare | A0 C102 D46 | 271 | C102(2) D46(2) |
| Melzas (corps principal) (boss) | 418 | 3 | 3 | - | 5 | 327 Arena Melzas 1 (Boss), 347 Lake Shrine (inner), 415 Lake Shrine Melzas (inner, Beginning Event?) | A17 C55 D28 E11 | 534 | E11(8) |
| Tortue de roche Niv.1 | 437 | 75 | 16 (dont 1 test/inutilisées) | - | 7 | 3 Overworld 0,2, 8 Overworld 1,3, 12 Overworld 2,3 | A2 C3 D3 | 312 | A2(2) C3(2) D3(2) |
| Slime gélatineux | 329 | 118 | 18 (dont 1 test/inutilisées) | - | 8 | 9 Overworld 2,0, 13 Overworld 3,0, 22 Lars' Crypt | A1 C26 D1 | 272 | A1(4) |
| Homme-lézard (épée) Niv.1 | 362 | 110 | 11 | - | 8 | 16 Overworld 3,3, 43 Surferboy Nightmare (outer), 80 Lizardman's Lair | A0 C2 D2 | 288 | D2(4) |
| Homme de boue Niv.1 | 431 | 16 | 4 | - | 8 | 16 Overworld 3,3, 70 Coastal Cave, 72 Coastal Cave | A0 C31 D11 | 299 | D11(8) |
| Zombie Niv.1 | 449 | 15 | 4 | - | 8 | 21 Lars' Crypt, 22 Lars' Crypt, 24 Lars' Crypt | A0 C21 D11 | 208 | D11(8) |
| Loup-garou Niv.1 | 482 | 5 | 4 | - | 8 | 226 Inoa, 229 Inoa (inner), 274 Inoa (inner) | A0 C37 D11 | 217 | D11(8) |
| Fantôme Niv.1 | 428 | 16 | 3 | - | 8 | 21 Lars' Crypt, 351 Lake Shrine (inner), 353 Lake Shrine (inner) | A0 C9 D8 | 264 |  |
| Ronan divin corrompu (boss) | 446 | 1 | 1 | - | 8 | 326 Arena Ronan (Boss) | A16 C54 D27 E11 | 743 | E11(8) |
| Homme-insecte Niv.1 | 401 | 84 | 12 | - | 9 | 86 Magyscar, 87 Magyscar, 88 Magyscar | A1 C7 D6 | 529 | A1(4) D6(8) |
| Muruta (boomerang) Niv.1 | 356 | 30 | 10 | - | 9 | 2 Overworld 0,1, 111 Nestus' Dream, 114 Tarn's Manor | A0 C35 D6 | 370 | D6(8) |
| Mimique Niv.1 | 404 | 22 | 10 | - | 9 | 5 Overworld 1,0, 101 Giles Nightmare, 139 Cave (Fire manor) | A3 C6 D5 | 193 | A3(2) D5(6) |
| Homme-lézard (projectiles) Niv.1 | 371 | 40 | 9 | - | 9 | 80 Lizardman's Lair, 81 Lizardman's Lair, 82 Lizardman's Lair | A0 C34 D2 | 384 | D2(4) |
| Ver Niv.1 | 410 | 76 | 9 | - | 9 | 18 Overworld 4,1, 19 Overworld 4,2, 26 Ancient Shrine | A1 C29 D6 | 270 | A1(4) D6(8) |
| Orc (hache) Niv.1 | 338 | 33 | 8 (dont 2 test/inutilisées) | - | 9 | 4 Overworld 0,3, 5 Overworld 1,0, 18 Overworld 4,1 | A0 C15 D12 | 250 | D12(2) |

Le Beannoïde (335) manque encore C96, C99 et D44 (section 2) ; il n'est pas dans ces tables.

### 4.3 Types ennemis sans enregistrement (nés d'un `SpawnWarpEntity`)

**FAIT (décompilation)** : `FunctionType{A,C,E}.cs` et `Boss/*.cs` appellent `SpawnWarpEntity` ou `SpawnChildEntity` avec un indice de sprite littéral dans 96 sites sur 103 ; les 7 autres sont dynamiques (objets lâchés `id + 0x1E`, cas génériques). Les enfants n'ont aucun enregistrement : ils n'existent qu'à l'exécution, tous leurs emplacements sont natifs, donc tous les codes de leur en-tête comptent.

| parent | gestionnaire(s) qui crée(nt) | enfants (id nom [classe]) |
|---|---|---|
| 329 Slime gélatineux | C26 | 330 Projectile [projectile] |
| 350 Muruta (arc) Niv.1 | C33 | 351 Flèche Niv.1 [projectile] |
| 356 Muruta (boomerang) Niv.1 | C35 | 357 Boomerang Niv.1 [projectile] |
| 365 Homme-lézard (lance) Niv.1 | C32 | 366 Lance Niv.1 [projectile], 372 Projectile Niv.1 [projectile] |
| 371 Homme-lézard (projectiles) Niv.1 | C34 | 366 Lance Niv.1 [projectile], 372 Projectile Niv.1 [projectile] |
| 410 Ver Niv.1 | C29 | 411 Projectile Niv.1 [projectile] |
| 412 Wilda (tête) | A15, C51 | 236 Nuage de sable [object], 413 Corps [projectile], 414 Souffle de dragon [projectile], 415 Boule de feu [projectile] |
| 418 Melzas (corps principal) | C55 | 419 Laser à tête chercheuse [enemy-attached], 420 Projectiles en spirale (lents) [enemy-attached], 423 Projectiles à tête chercheuse (lents) [enemy], 424 Double de Melzas [enemy-attached] |
| 431 Homme de boue Niv.1 | C31 | 432 Projectile Niv.1 [projectile] |
| 443 Champignon Niv.1 | C25 | 444 Spores Niv.1 [projectile] |
| 446 Ronan divin corrompu | C54 | 25 Bloc transparent (1×1×2) [object], 447 Collider de Roein [projectile], 448 Bombardement de Roein [enemy-attached], 450 Bouclier de Roein [enemy] |
| 461 Homme de bois Niv.1 | C27 | 462 Projectile Niv.1 [projectile] |
| 463 Après Melzas | A22, C88 | 464 Bras, projectiles [enemy], 465 Tentacule [projectile] |
| 465 Tentacule | C90 | 464 Bras, projectiles [enemy] |
| 467 Fourneau Niv.1 | C28 | 468 Projectile Niv.1 [projectile] |
| 471 Slime géant (œil) | A21 | 472 Slime géant (corps) [enemy] |
| 473 Oiseau de feu Niv.1 | C30 | 474 Projectiles (plumes) Niv.1 [projectile] |
| 476 Monsieur Aspiration | C48 | 236 Nuage de sable [object], 477 Effet d’aspiration [enemy-attached], 478 Petit slime [enemy] |
| 479 Dragon | C49 | 480 Flammes de dragon [enemy-attached], 481 Triple projectile de dragon [enemy-attached] |
| 485 Abyss Niv.1 | C38 | 484 Abyss (projectiles-bulles) [enemy-attached], 486 Abyss (droite) [enemy-attached], 487 Abyss (gauche) [enemy-attached] |
| 486 Abyss (droite) | C42 | 484 Abyss (projectiles-bulles) [enemy-attached] |
| 487 Abyss (gauche) | C42 | 484 Abyss (projectiles-bulles) [enemy-attached] |
| 488 Mille-pattes (corps principal) | C47 | 505 Mille-pattes (œufs explosifs) [enemy] |
| 497 Élément Niv.1 | C40 | 499 Surveillance élémentaire [enemy-attached] |
| 499 Surveillance élémentaire | C41 | 498 Roche élémentaire [projectile] |
| 500 Frère Rayon (aîné) Niv.1 | A11, C46 | 470 Frères Rayon (gaz toxique) [projectile], 501 Frère Rayon (cadet) [enemy] |
| 505 Mille-pattes (œufs explosifs) | C82 | 458 Mouche Niv.1 [enemy] |
| 506 Zazan-Roi Muruta | C43 | 507 Grand rocher du roi [projectile], 508 Sbires du roi [enemy] |
| 507 Grand rocher du roi | E24 | 483 Débris du roi [projectile] |
| 509 Zorgia | C44 | 469 Magie de Zoldia [projectile], 510 Cercle magique de Zoldia [projectile], 511 Projectiles de Zoldia [enemy-attached] |

Sites dynamiques (pour mémoire) : `SpawnEntityContents` (GameEngine.cs:1888); `UpdateWeaponStepProgression` (PlayerManager.cs:1437); `UpdateWarpSpawnBehavior` (FunctionTypeC.cs:15140); `AI_UpdateLoaderBossAI` (FunctionTypeC.cs:16137); `AI_SpawnWarpIfValid` (FunctionTypeC.cs:17105); `Script_FInteract_FUN_8007fc88` (SpriteEventHandlers.cs:300); `RandomlySpawnItem` (EffectManager.cs:458).

Les projectiles 150 à 153 de la page globale (génériques, C24 ou C36 + E3) n'ont pas de parent littéral. Un enfant sans enregistrement porté seul serait inatteignable : le parent d'abord.

### 4.4 Projectiles hostiles, objets rattachés, dangers

| type | nom | C E (en-tête) | enreg. | clés manquantes dans la DLL |
|---|---|---|---|---|
| 150 | Projectile (ligne droite) | C24 E3 | 0 | E3 |
| 151 | Projectile (chute) | C24 E3 | 0 | E3 |
| 152 | Projectile (parabolique) | C24 E3 | 0 | E3 |
| 153 | Projectile (boomerang) | C36 E3 | 0 | C36 E3 |
| 330 | Projectile | C24 E3 | 0 | E3 |
| 351 | Flèche Niv.1 | C24 E3 | 2 | E3 |
| 357 | Boomerang Niv.1 | C36 E3 | 0 | C36 E3 |
| 366 | Lance Niv.1 | C24 E3 | 0 | E3 |
| 372 | Projectile Niv.1 | C24 E3 | 0 | E3 |
| 411 | Projectile Niv.1 | C24 E3 | 0 | E3 |
| 413 | Corps | C0 E0 | 0 | aucune |
| 414 | Souffle de dragon | C0 E0 | 0 | aucune |
| 415 | Boule de feu | C52 E5 | 0 | C52 E5 |
| 432 | Projectile Niv.1 | C24 E3 | 0 | E3 |
| 444 | Spores Niv.1 | C24 E3 | 0 | E3 |
| 447 | Collider de Roein | C0 E0 | 0 | aucune |
| 462 | Projectile Niv.1 | C24 E3 | 0 | E3 |
| 465 | Tentacule | C90 E0 | 0 | C90 |
| 468 | Projectile Niv.1 | C24 E3 | 0 | E3 |
| 469 | Magie de Zoldia | C0 E13 | 1 | E13 |
| 470 | Frères Rayon (gaz toxique) | C85 E0 | 0 | C85 |
| 474 | Projectiles (plumes) Niv.1 | C24 E3 | 0 | E3 |
| 475 | Projectiles (étincelles) Niv.1 | C24 E3 | 0 | E3 |
| 483 | Débris du roi | C0 E3 | 0 | E3 |
| 498 | Roche élémentaire | C77 E3 | 0 | C77 E3 |
| 507 | Grand rocher du roi | C24 E24 | 0 | E24 |
| 510 | Cercle magique de Zoldia | C87 E3 | 13 | C87 E3 |

**FAIT (binaire)** : C24 est la fonction vide (`jr ra`) ; la mort à l'impact est E3 (25 mots, `0x8007EDA0`) ; C36 (`0x8006E83C`, 24 mots) est le boomerang. **INFÉRENCE** : le mouvement vient du mouvement commun (vitesse de l'animation) et les dégâts au héros de la passe de coups par drapeaux, donc d'E20.c2b, pas de ces gestionnaires.

Dangers non ennemis (classes `hazard/object`, 18 types, 1 062 enregistrements) : Rocher tombant (252), flammes (319, 343, 337), Sol piquant (318), boules de fer (310, 311), Torche (393), Ronces enflammées (238, 239), Laser (251), Plateforme lance-flammes (309), etc. Presque tous n'ont que A0 et E0 natifs ; les clés qui manquent vraiment sont D34 (ronces), E14 (rocher tombant), C66, C67 et E18 (boules de fer), C70 et E20 (torche).

## 5. Les gestionnaires natifs

### 5.1 Formes (FAIT, comparaison d'octets dans le binaire)

- **Fonctions vides** (deux mots, `jr ra ; nop`) : A254, C24, C57, D25. Rien à porter ; le no-op de la DLL est déjà exact.
- **Corps identiques à E0/E1** (`DestroyEntity(e, -1)`, 8 mots, md5 égal) : E0, E1, E6, E7, E8, E10, E27. La DLL traite 0, 1 et 10 en ligne. Types de E6, E7 et E27 : 420, 423 et 464 (tous sans enregistrement) ; E8 n'a aucun type. Ajouter 6, 7, 8 et 27 à `IsInlineDeactivateCode` tient en une ligne.
- **E11 n'est pas un alias** : `0x8007F25C` appelle `0x8003A648` (destruction récursive des entités enfants, butin, effet). Atteint par 8 types : 7 par leur en-tête (Melzas, Ronan, Après Melzas, Monsieur Aspiration, Dragon, Mille-pattes, Zorgia) et Zazan par un `0x41 [E,11]` de son programme C.
- **D en forme `HitReaction`** (25 mots, appels `ComputeNewHp` + `GetDirectionToTarget`, `Bytes[3]=1`, animation constante) : D1 (5), D6 (6), D7 (7), D8 (4), D9 (2), D11 (4), D13 (3), D17 (8), D19 (8), D24 (7). `AlundraEnemyHelpers.HitReaction` existe. Corps identiques deux à deux : D7=D24, D8=D11, D17=D19. D2 (76 mots), D3 (42), D5 (62), D10 (32), D12 (74) sont des variantes plus longues.
- **A en setters** : A1 (anim 2), A3 (4), A4 (9), A5 (7), A6 (3), A7 (9), A9 (10), A10 (6), 3 mots chacun ; A8, A14, A17 : 5-6 mots ; A2 : 21 mots (animation aléatoire). A4 et A7 ont le même corps. A11, A15, A21 et A22 appellent `SpawnWarpEntity`.

### 5.2 Partage des codes entre types ennemis

| clé | mots | forme | types qui l'utilisent | déjà inscrit pour |
|---|---|---|---|---|
| A1 | 3 | anim:2 | Slime gélatineux, Beannoïde, Tête de roche Niv.1, Homme-insecte Niv.1, Ver Niv.1 | 335 |
| A2 | 21 | anim:4 | Tortue de roche Niv.1, Tortue de roche Niv.2 | - |
| A3 | 3 | anim:4 | Mimique Niv.1, Homme de bois Niv.1 | - |
| C1 | 223 |  | Beannoïde, Petit slime | 335 |
| C3 | 249 |  | Tortue de roche Niv.1, Tortue de roche Niv.2 | - |
| C5 | 208 |  | Homme momie Niv.1, Homme momie Niv.2 | - |
| C10 | 503 |  | Muruta (griffes) Niv.1, Sbires du roi | 347 |
| C11 | 230 |  | Roper Niv.1, Roper Niv.2 | - |
| C12 | 623 |  | P-Zoldia Niv.1, P-Zoldia Niv.2 | - |
| C24 | 2 |  | Projectile (ligne droite), Projectile (chute), Projectile (parabolique), Projectile, Flèche Niv.1, Lance Niv.1, Projectile Niv.1,  | vide (`jr ra`), rien à porter |
| C36 | 24 |  | Projectile (boomerang), Boomerang Niv.1 | - |
| C83 | 62 | anim:1,2,4 | Chute de rochers de Nilude (222), Chute de rochers de Nilude (112), Chute de rochers de Nilude (111) | - |
| C102 | 242 |  | Slime géant (grand), Slime géant (petit) | - |
| D1 | 25 | HitReaction(anim:5 Bytes[3]=1) | Slime gélatineux, Beannoïde | 335 |
| D2 | 76 | anim:7 Bytes[3]=1 | Homme-lézard (épée) Niv.1, Homme-lézard (lance) Niv.1, Homme-lézard (projectiles) Niv.1, Zazan-Roi Muruta | - |
| D3 | 42 | anim:7,9 Bytes[3]=1 | Tortue de roche Niv.1, Tortue de roche Niv.2 | - |
| D5 | 62 | anim:5 Bytes[3]=1 | Orc (armure de fer) Niv.1, Mimique Niv.1, Homme momie Niv.1, Homme momie Niv.2, Homme de bois Niv.1, Fourneau Niv.1 | - |
| D6 | 25 | HitReaction(anim:6 Bytes[3]=1) | Muruta (boomerang) Niv.1, Roper Niv.1, Poisson Niv.1, Homme-insecte Niv.1, Ver Niv.1, Homme-ombre Niv.1, Champignon Niv.1, Roper N | - |
| D10 | 32 | anim:6 Bytes[3]=1 | P-Zoldia Niv.1, P-Zoldia Niv.2 | - |
| D11 | 25 | HitReaction(anim:4 Bytes[3]=1) | Mimming (faux) Niv.1, Mimming (rotation) Niv.1, Homme de boue Niv.1, Zombie Niv.1, Guêpe Niv.1, Mouche Niv.1, Oiseau de feu Niv.1, | - |
| D12 | 74 | anim:4 Bytes[3]=1 | Orc (hache) Niv.1, Orc (masse) Niv.1 | - |
| D20 | 72 | anim:6,9 Bytes[3]=1 | Frère Rayon (aîné) Niv.1, Frère Rayon (cadet) | - |
| D21 | 73 | anim:4 Bytes[3]=1 | Mille-pattes (corps principal), Mille-pattes (tronc), Mille-pattes (queue), Mille-pattes (joint), Mille-pattes (tronc 2) | - |
| D39 | 24 | anim:3 Bytes[3]=1 | Chute de rochers de Nilude (222), Chute de rochers de Nilude (112), Chute de rochers de Nilude (111) | - |
| D40 | 38 | anim:1,2 Bytes[3]=1 | Slime géant (œil), Slime géant (corps) | - |
| D41 | 15 | anim:3 Bytes[3]=1 | Projectiles à tête chercheuse (lents), Bouclier de Roein | - |
| D46 | 29 | anim:3 Bytes[3]=1 | Slime géant (grand), Slime géant (petit) | - |
| E3 | 25 | anim:1 | Projectile (ligne droite), Projectile (chute), Projectile (parabolique), Projectile (boomerang), Projectile, Flèche Niv.1, Boomera | - |
| E11 | 8 |  | Melzas (corps principal), Ronan divin corrompu, Après Melzas, Monsieur Aspiration, Dragon, Mille-pattes (corps principal), Zorgia | - |
| E23 | 50 |  | Chute de rochers de Nilude (222), Chute de rochers de Nilude (112), Chute de rochers de Nilude (111) | - |

### 5.3 Clés natives les plus utilisées et leur état

| clé | fonction de la décompilation | enreg. | cartes | types | rôles | porté (au moins un type) |
|---|---|---|---|---|---|---|
| A0 | SetSpawnFlagFromZPos | 6722 | 409 | 180 | object/prop 83, NPC 43, enemy 25 | oui |
| E0 | FUN_8007ed10 | 5005 | 436 | 199 | object/prop 83, item 54, NPC 43 | oui |
| E2 | AI_FUN_8007ed30 | 2161 | 239 | 10 | object/prop 10 | non |
| C23 | AI_FUN_8006b848 | 1995 | 212 | 7 | object/prop 7 | non |
| E1 | FUN_8007ed10 | 1538 | 165 | 42 | enemy 36, boss 5, enemy-attached 1 | oui |
| A255 | FUN_80061bd4 | 597 | 165 | 49 | item 48, object/prop 1 | non |
| F255 | Script_FInteract_FUN_8007fc88 | 502 | 159 | 1 | object/prop 1 | non |
| A1 | SetAnimationTo2 | 402 | 53 | 5 | enemy 5 | oui |
| E18 | AI_FUN_8007f658 | 341 | 58 | 2 | hazard/object 2 | non |
| D6 | AI_FUN_8007dcd8 | 323 | 53 | 8 | enemy 8 | non |
| D31 | AI_FUN_8007e7e4 | 292 | 29 | 7 | object/prop 7 | non |
| E15 | AI_FUN_8007f3b0 | 263 | 28 | 6 | object/prop 6 | non |
| D1 | AI_FUN_8007d9a4 | 248 | 35 | 2 | enemy 2 | oui |
| D9 | AI_FUN_8007de04 | 198 | 25 | 1 | enemy 1 | oui |
| D2 | AI_FUN_8007da08 | 178 | 15 | 4 | enemy 3, boss 1 | non |
| D11 | AI_FUN_8007dee8 | 157 | 34 | 7 | enemy 7 | non |
| D34 | AI_FUN_8007e8ac | 157 | 21 | 2 | hazard/object 2 | non |
| C93 | AI_UpdateEntityAI_IdleCurious | 150 | 21 | 1 | NPC 1 | non |
| C4 | AI_FUN_80066984 | 135 | 51 | 7 | object/prop 7 | non |
| D5 | AI_FUN_8007dbe0 | 112 | 30 | 6 | enemy 6 | non |
| C1 | AI_UpdateEntityAI_IdleSkittish | 105 | 16 | 1 | enemy 1 | oui |
| C2 | AI_UpdateEntityAI_CuriousFlying | 98 | 11 | 1 | enemy 1 | non |
| D12 | AI_FUN_8007df4c | 94 | 20 | 2 | enemy 2 | non |
| E14 | AI_FUN_8007f378 | 94 | 26 | 1 | hazard/object 1 | non |
| A2 | SetRandomizedAnimIdAndFlag | 89 | 19 | 2 | enemy 2 | non |
| D3 | AI_FUN_8007db38 | 89 | 19 | 2 | enemy 2 | non |
| C3 | AI_UpdateEntityAI_1 | 86 | 18 | 2 | enemy 2 | non |
| C26 | AI_UpdateEntityAI_17 | 78 | 12 | 1 | enemy 1 | non |
| C29 | AI_UpdateEntityAI_20 | 76 | 9 | 1 | enemy 1 | non |
| A254 | FUN_80061bcc | 65 | 65 | 1 | object/prop 1 | oui |
| C72 | AI_ProcessWarpTransitionState | 65 | 65 | 1 | object/prop 1 | oui |
| F1 | Script_FInteract_FUN_8007fc64 | 65 | 65 | 1 | object/prop 1 | oui |
| E19 | AI_FUN_8007f690 | 60 | 29 | 5 | object/prop 5 | non |
| C255 | FUN_8007c174 | 59 | 25 | 37 | item 37 | non |
| C69 | AI_FUN_8007b6ec | 57 | 5 | 1 | object/prop 1 | non |
| D33 | AI_FUN_8007e86c | 57 | 5 | 1 | object/prop 1 | non |
| E20 | AI_FUN_8007f6c8 | 57 | 10 | 1 | hazard/object 1 | non |
| A19 | SetCustomByteFromProgramIndex | 56 | 6 | 1 | object/prop 1 | non |
| C7 | AI_UpdateEntityAI_4 | 56 | 10 | 1 | enemy 1 | non |
| D10 | AI_FUN_8007de68 | 56 | 15 | 2 | enemy 2 | non |
| C16 | AI_FUN_80069f44 | 51 | 12 | 1 | enemy 1 | non |
| C70 | AI_FUN_8007b7b0 | 49 | 10 | 1 | hazard/object 1 | non |
| C92 | AI_UpdateEntityTriggerWarpBehavior | 47 | 20 | 1 | NPC 1 | non |
| C91 | AI_UpdateEntityAI_IdleLookAround | 41 | 21 | 1 | NPC 1 | non |
| A4 | SetAnimationTo9 | 40 | 8 | 1 | enemy 1 | non |
| C19 | AI_UpdateEntityAI_12 | 40 | 10 | 1 | enemy 1 | non |

Les gros consommateurs hors ennemis (E2 caisses et jarres, C23, A255, F255 coffres) relèvent d'E20.g et du butin, pas d'E14.

### 5.4 Gardes de nom de la décompilation

`FunctionType{A,C,D,E}.cs` et `Boss/*.cs` contiennent 161 gestionnaires dont une garde `entity.Name != "..."` appelle `Breakpoint.TriggerBreak()` si le nom n'est pas dans la liste. **FAIT** : 132 de ces listes égalent exactement l'ensemble des types dont l'en-tête (ou un `0x41`) nomme ce code ; les 29 autres sont des listes incomplètes (D2 sans la lance, D6 sans Poisson, D11 sans Loup-garou, E11 sans Après Melzas ni Mille-pattes) ou plus larges (colonnes `guard_names_not_in_census` et `census_types_not_in_guard` de `handlers.tsv`). **INFÉRENCE** : ce sont des assertions de débogage de l'auteur de la décompilation, pas une source de vérité ; l'en-tête et le binaire tranchent. **INCONNU** : E4, E8, E15 et E27 citent aussi « Homme-lézard (épée) » alors qu'aucun en-tête, aucun programme et aucun `0x41` de la base ne mène ce type à ces codes (`scan41.py` : aucun `0x41` vers E4, E8, E15, E27).

## 6. Prérequis partagés

Aides du binaire appelées par les clés manquantes des ennemis et des boss et **absentes de la DLL** (recherche de l'adresse dans `Alundra/Scripts/*.cs`, aucune occurrence ; `CreateEffectEntity` existe mais seul l'effet de bris est porté) :

| aide | clés qui l'appellent | types ennemis | clés |
|---|---|---|---|
| 0x800808DC (sans nom dans la décompilation ; appelle GetEntityTileHeight) | 11 | 12 | C13 C14 C2 C25 C28 C32 C34 C35 C5 C6 C9 |
| CanMoveForward 0x80080260 | 10 | 11 | C12 C17 C19 C20 C21 C27 C29 C31 C33 C35 |
| SetEntityRandomDirection 0x80080030 | 7 | 9 | C11 C25 C27 C44 C5 C6 C9 |
| TryAttackPlayer 0x80080704 | 3 | 4 | C2 C5 C6 |
| GetEntityTileHeight 0x8003AB54 | 2 | 3 | C12 C20 |
| CreateEffectEntity 0x8003BDD8 (autres effets que le bris) | 9 | 11 | C16 C2 C21 C37 C39 C51 C55 C8 E23 |

Déjà présentes (E14.a0b, `AlundraEnemyHelpers.cs`, vérifiées contre l'émulateur MIPS) : position relative `0x8007FE20`/`0x8007FE8C`, `TryAttackPlayer2`, `TryAttackPlayerFront`, `HandleAnimationDirection`, `StartWalk`, `Below(n)`, `HitReaction`. Présents ailleurs : `ComputeNewHp` (`AlundraDamage.cs`), `GetDirectionToTarget` (`ScriptHelper.cs`), `SpawnWarpEntity` par indice de sprite (`IEntityWorldContext.SpawnEntityBySpriteIndex`, E20.e0a1), le générateur et son avance par tick (E20.e0b1, `AlundraWorldProxy.cs:2658,3001`), l'effet de bris (`AlundraNativeEffects.CreateBreakEffect`).

Dépendances hors E14 :

- **Butin au décès** (`SpawnEntityContents` `0x80032B90`, appelé par `DestroyEntity`) : E20.e0c, non faite (D-E14-3). Tant qu'elle ne l'est pas, tout ennemi tué ne lâche rien. Les champs d'en-tête `Contents` et `BreakEffect` sont exportés (`alundra-project/Data/sprite-records.json`).
- **Blessure du héros au contact** : E20.c2b, non faite (`docs/plan-e20-heros.md:727`, `:2343`). Nécessaire à toute recette où l'ennemi doit blesser ; inutile pour porter ses gestionnaires.
- **Effets** : `CreateEffectEntity` autre que le bris (9 clés d'ennemis et de boss, 11 types), `CheckAndTriggerTileEffect` (bouchon nommé dans `AlundraWaveSwordAi.cs`) : E20.e.
- **Destruction récursive des enfants** (`0x8003A648`, E11) : nouvelle, pour 8 types de boss.
- **Création d'entités par les gestionnaires** : C7 et C8 créent un sprite de leur propre type (401 et 395) d'après la décompilation ; les créateurs A11, A15, A21, A22 et les clés de boss créent des enfants (colonne `calls_SpawnWarp/SpawnChild` de `handlers.tsv`).

## 7. Ordre de tranches proposé

Principes : (1) chaque tranche reprend la méthode d'E14.a1 (oracle MIPS appel par appel, arcs, vérificateur frais) ; (2) un type n'est activé qu'avec **toutes** ses clés (le piège des 118 slimes, `dll-natives.md` §0.3) ; (3) l'histoire connue d'abord (S1, S2), ensuite le rendement (cartes couvertes par millier de mots, colonne `c/kmots`, S3 à S10), puis les boss ; (4) l'ordre réel de l'histoire au-delà du jour 4 est **INCONNU** : l'auteur peut réordonner S3 à S10 et B1 à B14 sans casser les dépendances ci-dessous.

| tranche | contenu | types | enreg. | cartes | cartes de la chaîne | clés à ajouter | mots | c/kmots | aides absentes | enfants sans enregistrement | dépend de |
|---|---|---|---|---|---|---|---|---|---|---|---|
| S0 | Socle partagé : inscription cumulative des types, alias de E, formes HitReaction et setters A, E3 et C36, aides manquantes |  | 0 | 0 | - | - | 0 | - | - | - | E14.a0, a0b, a1 faits |
| S1 | Route du jour 2 (cartes 14 et 15) : Poisson, Guêpe, Orc (masse, hache, armure de fer) | 392, 455, 341, 338, 344 | 157 | 31 | 14,15 | A0 C15 C16 C17 C18 C19 D11 D12 D5 D6 | 1157 | 26.8 | CanMoveForward, CreateEffectEntity | - | S0 |
| S2 | Slimes géants de la 44 (jour 3) | 459, 460 | 2 | 1 | 44 | A0 C102 D46 | 271 | 3.7 | - | - | S0 |
| S3 | Slime gélatineux (rouvre A1 et D1 au type 329 ; projectile 330) | 329 | 118 | 18 | - | A1 C26 D1 | 272 | 66.2 | - | 330 | S0 ; projectile 330 (C24 vide, E3) |
| S4 | Mimming et mimique | 377, 386, 404 | 102 | 25 | - | A0 A3 C13 C14 C6 D11 D5 | 602 | 41.5 | 0x800808dc, SetEntityRandomDirection, TryAttackPlayer | - | S0 |
| S5 | Roches, insectes, ver | 437, 441, 395, 401, 410 | 253 | 40 | - | A1 A2 C29 C3 C7 C8 D3 D6 D7 | 1399 | 28.6 | CanMoveForward, CreateEffectEntity | 411 | S0 ; projectile 411 ; C7 et C8 créent des entités |
| S6 | Momies, fantôme, zombie, loup-garou | 425, 454, 428, 449, 482 | 81 | 21 | - | A0 C21 C37 C5 C9 D11 D5 D8 | 934 | 22.5 | 0x800808dc, CanMoveForward, CreateEffectEntity, SetEntityRandomDirection, TryAttackPlayer | - | S0 |
| S7 | Muruta à l'arc et au boomerang | 350, 356 | 69 | 14 | - | A0 C33 C35 D13 D6 | 679 | 20.6 | 0x800808dc, CanMoveForward | 351,357 | S0 ; projectiles 351 (C24 vide, E3) et 357 (C36, E3) |
| S8 | Hommes-lézards (épée, lance, projectiles) | 362, 365, 371 | 175 | 12 | - | A0 C2 C32 C34 D2 | 904 | 13.3 | 0x800808dc, CreateEffectEntity, TryAttackPlayer | 366,372 | S0 ; projectiles 366 et 372 (E3) |
| S9 | Casters et divers : Roper, Homme de boue, Homme-ombre, Champignon, Fourneau, Oiseau de feu, Homme de bois, P-Zoldia | 389, 456, 431, 440, 443, 467, 473, 461, 452, 453 | 243 | 45 | - | A0 A3 A4 A6 A7 C11 C12 C20 C25 C27 C28 C30 C31 C78 D10 D11 D35 D5 D6 | 3969 | 11.3 | 0x800808dc, CanMoveForward, GetEntityTileHeight, SetEntityRandomDirection | 432,444,462,468,474 | S0 ; projectiles 432, 444, 468, 474 |
| S10 | Slime géant d'Elane (œil, corps) et petit slime | 471, 472, 478 | 6 | 4 | - | A0 A21 C1 C84 D23 D40 | 600 | 6.7 | - | - | S0 ; C1 du Beannoïde pour 478 (inscription cumulative) |
| B1 | Boss : Zorgia | 509 | 4 | 4 | - | A0 C44 D19 E11 | 623 | 6.4 | SetEntityRandomDirection | 469,510,511 | S0 (E11 : voir 5.1 si la clé est listée) |
| B2 | Boss : Élément Niv.1 | 497 | 3 | 3 | - | A10 C40 D17 | 454 | 6.6 | - | 498,499 | S0 (E11 : voir 5.1 si la clé est listée) |
| B3 | Boss : Melzas (corps principal) | 418 | 3 | 3 | - | A17 C55 D28 E11 | 534 | 5.6 | CreateEffectEntity | 419,420,423,424 | S0 (E11 : voir 5.1 si la clé est listée) |
| B4 | Boss : Monsieur Aspiration | 476 | 3 | 3 | - | A0 C48 D22 E11 | 669 | 4.5 | - | 477,478 | S0 (E11 : voir 5.1 si la clé est listée) |
| B5 | Boss : Zazan-Roi Muruta | 506 | 3 | 3 | - | A0 C43 D2 E11 | 760 | 3.9 | - | 483,507,508 | S0 (E11 : voir 5.1 si la clé est listée) |
| B6 | Boss : Boss lézard Niv.1 | 494 | 2 | 2 | - | A9 C39 D16 | 619 | 3.2 | CreateEffectEntity | - | S0 (E11 : voir 5.1 si la clé est listée) |
| B7 | Boss : Mille-pattes (corps principal) | 488 | 2 | 2 | - | A12 C47 D21 E11 | 928 | 2.2 | - | 458,505 (+ par le nom : 489,490,491,492,504) | S0 (E11 : voir 5.1 si la clé est listée) |
| B8 | Boss : Dragon | 479 | 2 | 2 | - | A0 C49 D24 E11 | 1274 | 1.6 | - | 480,481 | S0 (E11 : voir 5.1 si la clé est listée) |
| B9 | Boss : Après Melzas | 463 | 1 | 1 | - | A22 C88 D42 E11 | 221 | 4.5 | - | 464,465 | S0 (E11 : voir 5.1 si la clé est listée) |
| B10 | Boss : Nirude (avec corps) | 495 | 2 | 1 | - | A14 C50 | 556 | 1.8 | CreateEffectEntity |  (+ par le nom : 496,502,503) | S0 (E11 : voir 5.1 si la clé est listée) |
| B11 | Boss : Ronan divin corrompu | 446 | 1 | 1 | - | A16 C54 D27 E11 | 743 | 1.3 | - | 447,448,450 | S0 (E11 : voir 5.1 si la clé est listée) |
| B12 | Boss : Abyss Niv.1 | 485 | 1 | 1 | - | A8 C38 D14 | 837 | 1.2 | - | 484,486,487 | S0 (E11 : voir 5.1 si la clé est listée) |
| B13 | Boss : Frère Rayon (aîné) Niv.1 | 500 | 1 | 1 | - | A11 C46 D20 | 1122 | 0.9 | - | 470,501 | S0 (E11 : voir 5.1 si la clé est listée) |
| B14 | Boss : Wilda (tête) | 412 | 1 | 1 | - | A15 C51 D26 | 1280 | 0.8 | CreateEffectEntity | 413,414,415 | S0 (E11 : voir 5.1 si la clé est listée) |

**S0, socle (avant toute famille).** (a) Inscription cumulative des types par clé dans `AlundraNativeSpriteRegistry` (aujourd'hui une seconde inscription lève) ; (b) `IsInlineDeactivateCode` : 6, 7, 8, 27 ; (c) table des `HitReaction` (D6, D7, D8, D11, D13, D17, D19, D24 : une ligne chacun, `AlundraEnemyHelpers.HitReaction` existe) ; (d) setters A en table ; (e) E3 (mort des projectiles, 25 mots) et C36 (boomerang, 24 mots) ; (f) les aides de la section 6, chacune avec ses vecteurs d'oracle ; (g) l'interrupteur des natifs existe déjà (`NativeHandlersEnabled`). Valeur : D6 et D11 réclamés par 16 types, D5 par 6, D2 par 4. S0 ne change aucun comportement visible (registre vide pour les nouveaux types).

**S1 et S2 d'abord**, parce que la chaîne les touche (cartes 14 et 15 au jour 2 ; carte 44 au jour 3). **S3** (Slime gélatineux) est le meilleur rendement : A1 et D1 existent, il faut les rouvrir au type 329 (sinon les 118 slimes ne meurent pas, `dll-natives.md` §0.3), ajouter C26 et le projectile 330. S5 porte le plus d'enregistrements ; deux de ses clés (C7, C8) créent des entités. Les **boss** B1 à B14 passent en dernier, chacun avec son oracle : ordre par nombre de cartes décroissant puis taille croissante (données, pas histoire) ; leurs clés pèsent de 221 à 1280 mots, avec 0 à 12 sites du générateur et 0 à 6 appels de création. Les enfants sans enregistrement 489-492, 504 (Mille-pattes) et 496, 502, 503 (Nirude) n'ont pas de parent littéral dans la décompilation : rattachés par le nom (**INFÉRENCE**) à B7 et B10.

Dépendances croisées à respecter : les enfants (4.3) après leur parent ; D2 partagé entre S8 et Zazan (B5), donc B5 après S8 ; « Petit slime » (478, S10) réutilise C1 du Beannoïde et « Sbires du roi » (508, B5) C10 du Muruta, d'où l'inscription cumulative de S0 ; E20.c2b avant toute recette qui compte les points de vie du héros ; E20.e0c avant toute recette qui compte les objets lâchés ; E20.e pour les effets autres que le bris.

## 8. Limites et inconnues

- **INCONNU** : l'ordre de l'histoire hors de la chaîne de 37 cartes. `1re carte histoire` est vide pour les types qui n'y apparaissent pas ; `prof.` n'est qu'un indice (portails et `0x53` lus comme des numéros de carte, vérifié seulement sur la chaîne 389 vers 163).
- **INFÉRENCE** : les étendues de fonctions (« mots ») sont des majorants (début de la fonction suivante, resserré par un contrôle de `jr ra`). Elles retrouvent 223 mots pour C1 et 503 pour C10, comme l'annexe.
- **INFÉRENCE** : la colonne `short_handler_effect` (constantes écrites en `entity+0x88`) ignore les branchements ; E2 y paraît écrire -1 alors que la constante est 2. Relire le binaire avant de porter.
- **INFÉRENCE** : le balayage linéaire des programmes (`0x40`, `0x41`, `0x53`) borne chaque programme au début du suivant ; il n'a rencontré aucun opcode inconnu. Il ne suit pas `0x43` (changement d'entité logique) : un `0x40`/`0x41` visant une autre entité est attribué au type du programme. Les entrées de la table F sont lues seulement pour les index réellement utilisés (la table n'a pas de taille enregistrée).
- **INCONNU** : pourquoi la décompilation cite le lézard (épée) aux gardes d'E4, E8, E15, E27.
- Les types `◆` sans bits de combat (419, 477, 493, 499 et quelques projectiles de boss) sont classés « objet rattaché à un ennemi » sur le seul nom ; presque aucun enregistrement.

## 9. Fichiers (dossier de travail)

Dossier : `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/themes/enemies2/`.

- Sorties : `roster.tsv` (395 types), `handlers.tsv` (206 clés), `slices.tsv`, `report.md`.
- Scripts (`python -B`) : `exelib.py`, `mipsdis.py` (lecture et désassemblage de l'EXE), `exe_handlers.py` (tables, étendues, appels), `decomp_index.py` (adresses, enregistrements, sites de création, gardes de nom), `extract_maps.py` (cache des cartes), `common.py` (noms, DLL, balance), `census.py` (recensement, `0x40`/`0x41`/`0x53`), `scan41.py` (balayage global des `0x41`), `build_outputs.py` (classification, TSV), `slices.py`, `report_gen.py`. Ordre : `extract_maps.py`, `exe_handlers.py`, `census.py`, `build_outputs.py`, `slices.py`, `report_gen.py`.
- Cache : `cache/` (cartes extraites, désassemblage de l'EXE, résultats intermédiaires).
