# E19.sm3 - vecteurs de valeurs rejouables (0xA9 0xAA 0xB2 0xB4 0xB5 0xB6 0xB7 0x9E)

Préparation en lecture seule du 2026-10-18 (dépôt lu à `36ac0686`, binaire `ALUN_CD.EXE` (France) SHA-1 `a705f8dab665645af5dfaf8fb348c7926b5caa58`,
vérifié). Rien n'a été écrit hors de ce dossier ; le binaire n'est lu que par les copies d'outils de `../tools/`.

## Ce que c'est

Les VRAIS gestionnaires du binaire (table `0x80098FAC + 4*op`) et la VRAIE recherche `0x8003C954` tournent dans l'interpréteur MIPS-I de l'annexe
`docs/plan-e19-sm-annexe/sm1-sm4/work/oracle/` (copie : `../tools/oracle/{oracle,emu,lib}.py`) sur des mondes explicites. Chaque ligne de
`sm3-vectors.jsonl` est un monde lu, les octets d'instruction, et ce que le binaire rend. 837 vecteurs : 8 opcodes.

| Famille | Nombre | Rôle |
|---|---|---|
| main (`id` sans préfixe `SWEEP`/`RAND`) | 213 | cas écrits à la main, ceux qui nomment les tests ; chaque mutant du plan (`mutants.json`) a au moins un tueur ici |
| `SWEEP-*` | 384 | balayage des 12 types de groupe (`0x80`-`0x8B`) et de 4 identifiants bruts pour chaque opcode de recherche (`0xB2` : 16 types x 6 seconds types, un monde graine), 3 mondes graine chacun pour les autres |
| `RAND-*` | 240 | 30 mondes graine par opcode (8 cases + grille aléatoire pour `0x9E`) |

(Les comptes exacts sont dans `stats.json`.) Reproduire : `python -B gen_vectors.py OUT` puis `python -B mutants.py OUT` ; deux exécutions donnent
des fichiers identiques à l'octet (`RUNS-IDENTICAL.txt`). Un script ne s'exécute QUE depuis une copie (il lit le binaire au chemin que nomme `lib.py`).

## Format d'une ligne (JSON, clés triées)

```
id            nom stable du vecteur (les tests le citent)
op            "0xA9" ...
instr         octets de l'instruction, opcode compris, EXACTEMENT la taille de la table (2,2,3,2,2,2,3,6)
ref           indice de slot de l'entité LOGIQUE (le a0 du gestionnaire) ; 0 = le héros
n_last        N, l'indice du dernier slot utilisé ([0x80134360]) ; la recherche parcourt 0..N (groupes 2 et 4) ou 1..N
slots[i]      status (0 libre, 1 Loaded, 2 Normal, 3 Deactivated, 4 FlagToDestroy), ref_id (EntityRefId, -1 pour le héros),
              flags, anim_flags, force_z (signé), target_anim (non signé), et quatre pointeurs en INDICE DE SLOT (null = 0) :
              platform, riding, xcoll, parent.  Un champ absent des lectures du gestionnaire reste dans la ligne (il vaut sa
              valeur d'écriture du monde) mais n'a aucun effet.
cells         9E seulement : liste [x, y, mot16] posée sur une grille 52 x 60 (le reste vaut 0, ou la grille graine)
grid_seed     9E, lignes RAND : graine de la grille 52 x 60 (valeurs {0,1,2,3,0x100,0x102,4,6,8})
result_before le Result avant l'instruction (7 par défaut, la convention d'E19.l : ni 0 ni 1 ; 0 ou 1 pour les lignes « écrase un Result périmé »)
expect.v0     la taille rendue (= taille de la table, = CodeIndex ajouté)
expect.result le mot Result écrit (0 ou 1)  [jamais null pour ces 8 opcodes : ils écrivent toujours]
expect.writes_other   les écritures hors Result, hors pile, hors tampon de recherche : TOUJOURS vide (vérifié sur les 837)
red_today     ce que la DLL donne aujourd'hui (saut par la taille, UnknownOpcode) : Result = result_before, CodeIndex += taille, trace UnknownSkipped
              (fails_today = expect.result != result_before ; 837 sur 837)
model_binary_agrees   le modèle python écrit d'après le désassemblage (oracle.MODELS) égale le binaire : vrai sur les 837
dll_shape_agrees      le modèle de la forme DLL (traduction python de EntitySearchService.cs, sans slot libre) égale le binaire : vrai sur les 837
reads         les classes de lectures mémoire du gestionnaire, une clé par (slot, champ)
```

## Correspondance de chaque octet lu avec un membre de la DLL (`read-map.json`)

Chaque lecture de donnée (instruction de chargement, hors fetch) est journalisée par l'interpréteur ; une adresse sans membre est un ARRÊT
(`gen_vectors.py` sort en code 2). Résultat : aucune lecture non mappée sur les 837 vecteurs.

| Lecture binaire | Membre de la DLL |
|---|---|
| entité `+0x0C` | `EntityScriptProxy.ParentEntity` (`Entity?`, comparé au `LogicContextEntity` de la référence ; types 9 et 10) |
| entité `+0x10` | `EntityScriptProxy.Status` ; `IsLoadedNormalOrDeactivated` = 1 à 3 |
| entité `+0x28` | `EntityScriptProxy.PlatformEntity` (test de nullité seul ; types 4 et 11) |
| entité `+0x48` | `EntityScriptProxy.EntityRefId` (`int` ; le héros vaut -1) |
| entité `+0x6C` | `EntityScriptProxy.Flags` (`EntityFlags.Collidable` = `0x80` ; type 4) |
| entité `+0x88` | `EntityScriptProxy.TargetAnimationId` (`uint` ; `0xB7`) |
| entité `+0xB4` | `EntityScriptProxy.AnimFlags` (`NoEntityCollision` = `0x80`, constante locale du service ; type 4) |
| entité `+0xB8` | `EntityScriptProxy.ForceZ` (`int`, signé ; `0xB4`-`0xB6`) |
| entité `+0x12C` | `EntityScriptProxy.RidingEntity` (`Entity?` ; types 5 et 6) |
| entité `+0x130` | `EntityScriptProxy.XCollisionEntity` (`EntityScriptProxy?` ; types 7 et 8, `0xA9`, `0xAA`) |
| `[0x80134360]` N | aucun membre : la DLL parcourt `IEntityWorldContext.SpawnedEntities` (héros d'abord, puis l'ordre des slots, ADR-0064) ; un slot libre n'y est pas |
| octets d'instruction `CODE+k` | `variables[k]` de `FillDataFromCommand` (octets étendus par zéro) |
| pointeur d'instruction | `EventProgramState.CodeIndex` |
| table de sauts `0x80023C30` | le `switch (searchType & 0x7F)` d'`EntitySearchService` (constante du binaire) |
| tampon des résultats `0x8013D8D4..` | la liste que rend `GetMatchingEntitiesBySearchType` (écrite par la recherche du même appel) |
| `[0x800DD86C] + 0x604 + (y*52+x)*8`, mot de 16 bits (`0x9E`) | l'octet de marchabilité de la case (`CellStore._walkability`, `CellsCollisionField.TryReadBinaryCell`) ; l'octet haut (GroundProperty) est masqué par `& 2` |
| pile | registres sauvés et cadre de `0xB2` (copie des 65 mots) : pas une donnée du monde |

Écriture : le SEUL mot écrit est `Result` (état `+0x2C`) ; les écritures du tampon de recherche et de la pile sont du brouillon.
La copie de `0xB2` lit 65 mots du tampon (la recherche n'en a écrit que n1) : les mots au-delà de n1 sont copiés mais jamais comparés
(la boucle de comparaison s'arrête à n1) ; ce cas est classé `match_buffer_copy`, ni inconnu ni erreur.

## Montage d'un test C# à partir d'un vecteur

1. Une `IEntityWorldContext` de test (le `Context` de `AlundraClassAndBranchOpcodesTests.cs:25-35`) ; le héros : `IsPlayer = true`,
   `EntityRefId = -1`, `Status = Normal`, présent dans `SpawnedEntities` ET en `PlayerEntity` ; chaque autre slot : un `EntityScriptProxy`
   dans `SpawnedEntities` dans l'ORDRE des slots. Un slot de statut 0 n'est PAS créé (la DLL n'a pas de slot libre).
2. Les pointeurs `xcoll`/`platform`/`riding`/`parent` de la ligne se posent ainsi : `XCollisionEntity = proxies[j]` (type proxy) ;
   `PlatformEntity`, `RidingEntity`, `ParentEntity` = le `LogicContextEntity` du slot j (`new Entity { Name = ... }`), jamais nul si la ligne a un indice.
3. `Result` départ = `result_before` ; programme `[instr..., 0xFF]` ; `Run` (`RunOneScriptCall`) ; assertions : `Result == expect.result`,
   `CodeIndex == expect.v0`, aucun autre champ de l'état changé (`Parameters[2..9]` intacts).
4. Rouge d'aujourd'hui : `Result` reste `result_before` et la trace dit `UnknownSkipped`.
5. Évitez l'identifiant brut 0 dans un test : le binaire peut y apparier un slot libre effacé (`+0x48 == 0`, INFÉRENCE) que la DLL n'a pas.

## Limites déclarées (INCONNU, non épinglées, aucune ligne ne les contient)

- **Type de recherche >= 0x8C** (douze fonctions ou plus) : le binaire imprime puis quitte (`0x8003C840`, appel BIOS INFÉRÉ) ; la DLL journalise et ne rend rien. Aucun site.
- **Identifiant brut invalide** (`0x8003C914` -> `0x80039BD0`) : fatal dans le binaire sur un id absent des enregistrements (check.md O1) ; les ids du corpus sm3 (3, 4, 5, 6, 8, 24) sont des enregistrements des cartes visées (258, 298, 440).
- **9E hors de la grille 52 x 60** (index linéaire `(y+r)*52 + x+c >= 3120`) : le binaire lit ce qui suit le tableau de cases, indéfini ; les vecteurs restent dans la grille.
- **Contenu d'un slot libre** : supposé effacé (tous champs à 0) ; les lignes `free_slot()` suivent cette hypothèse.
