# Critique adverse de la découverte E20.h4d1 (feu 2, objet 0x30), lecture seule

Verdict d'ensemble : **REVISE**. Mes propres modèles du pilote, de l'annulation et d'E28 concordent avec le vrai code, donc ces faits sont CONFIRMÉS. En revanche, la chronologie de la clé d'oracle, les huit positions d'effets et les ticks de coups sont **RÉFUTÉS** : le harnais d'e20-h4 initialise mal la table des emplacements. Deux tests de monde (w1, w4) comparent aussi des mots que la DLL ne peut pas égaler. Une fois ces points corrigés (§3 à §6), le reste du plan tient.

**Légende.** FAIT : vu dans le code, les données ou le binaire, avec adresse ou fichier:ligne. INFÉRENCE : déduit, avec de quoi. INCONNU : non établi. Verdicts : CONFIRMÉ, RÉFUTÉ, NON ÉTABLI.

**Base.**
- Dépôt `D:/development/repo/alundra-casaengine-project-converter`, HEAD `023b33ce`. `git diff --stat f81304b HEAD -- Alundra Alundra.Tests alundra-project/Data` est vide : le code lu est celui des états d'annexe.
- Binaire `ALUN_CD.EXE` France, SHA-1 `a705f8dab665...` (relu : même fichier).
- Rien écrit hors de `C/` = `.../disc-h4el/h4d1-critic/`. Aucune édition du dépôt, aucun git d'écriture, aucun build, aucun test, aucun export. Python lancé avec `-B`, depuis des copies.

## 1. Ce que j'ai refait moi-même

| Moyen | Contenu | Résultat |
|---|---|---|
| `c/cmipsmini.py` | Mon propre interpréteur MIPS-I (capstone, créneaux de retard, 32 bits), écrit sans reprendre le code du harnais de la découverte. | sert aux deux lignes suivantes |
| `c/crit_models.py` | Mes modèles Python du pilote `0x80035C64`, de la garde `0x800351B8`, du chemin d'annulation `0x800350C0 -> 0x80034E08` et d'E28 `0x8007FB58`. Écrits d'après ma propre lecture du désassemblage, puis fuzzés contre le VRAI code interprété. | pilote : **6 000 états aléatoires, 0 écart**. États tirés : phases −1 à 5, animations, pente, entités, naissance réussie ou refusée, identité égale ou non. Écarts cherchés sur le retour, les 9 mots du bloc, les 9 du tampon, le pointeur, l'identité, la phase, l'animation du héros, le mot de l'objet, le statut et `+0x20` de 7 emplacements, et les arguments de l'appel de naissance. E28 : 6 000 tirages de graine et de position, 0 écart (8 appels d'effet, graine finale, `DestroyEntity(self, -1)` une fois, 9 appels au total). |
| 202 vecteurs `vec_e28_8007FB58.json` | rejoués par mon modèle | 202 sur 202, 0 écart |
| `c/scan_refs.py` | balayage de tout le segment texte (lui + accès) des accès aux adresses voulues | voir §2.3 |
| `c/reach.py` | graphe de flot de contrôle depuis une racine, tables de saut résolues (`sltiu` + base), recherche de tout accès au mot du générateur `0x80098708` dans l'ensemble atteint | voir §2.2 |
| `c/h4o/fire2_key_c.py`, `fire2_targets_c.py` | copies des scripts de la découverte avec UNE seule correction : l'initialisation des emplacements du chargement de carte (§3) | clés refaites dans `keys_faithful/` |

Lectures du binaire citées plus bas :
- `0x80035C64-0x80035EA0`, `0x800350C0-0x800351B4`, `0x800351B8-0x80035200`, `0x80034E08-0x80034EC0`
- `0x8007FB58-0x8007FC60`, `0x80039D04-0x80039F54`, `0x80039F58-0x8003A044`
- `0x80038634-0x800386CC`, `0x800386D0-0x800388B4`, `0x8003B1F0-0x8003B2D8`, `0x8003B388-0x8003B3F4`
- `0x8003634C-0x800364BC`, `0x8002C894-0x8002C9D8`, `0x80040534-0x80040598`

## 2. Faits du binaire : verdict, un par un

### 2.1 Le pilote `0x80035C64` : tout CONFIRMÉ

| # de la découverte | Verdict | Preuve |
|---|---|---|
| P1 phase 0 : garde, si ≠ 0 rend 1 sans rien d'autre ; la restauration est faite par l'annulation que la garde appelle | CONFIRMÉ | listing `0x80035CA8-0x80035CB4`, modèle fuzzé. Précision : la restauration n'a lieu que si l'objet en cours `[0x80127164]` vaut 0x30 au moment de la garde (`0x800350C0` aiguille sur ce mot). Parmi les 60 vecteurs de `vec_fire2`, 4 lisent l'objet à 0 et 2 à 43 (18 à 48, 36 ne le lisent pas) : pas de restauration. À écrire dans l'adaptateur de (d1). |
| P2 phase 0, animation ≠ 0x33 rend 0 ; sinon `SpawnWarpEntity(héros, page 0, 0x15, X, Y, Z, 0)` avec `+0x114/+0x118/+0x11C` | CONFIRMÉ | `0x80035CC0-0x80035CF0`, modèle (arguments comparés) |
| P3 pointeur rangé avant le test de nullité ; nul : rend 1 | CONFIRMÉ | `0x80035CF8-0x80035CFC`, `0x80035DC8` |
| P4 copie des 9 mots vers `0x80127138`, phase + 1, identité `[ptr+4]`, enchaîne la phase 1 du même tick | CONFIRMÉ | `0x80035D00-0x80035D70`. Les 9 mots : deux tours de 4 mots (`0x4338..0x4357`) puis le 9e en `0x4358`. |
| P5 phase 1, garde ≠ 0 : restaure encore, rend 1, la phase n'avance pas | CONFIRMÉ | `0x80035D70-0x80035DC8` |
| P6 `Flag = 1`, `LimitY = SpeedY = c >> 4`, `LimitX = SpeedX = c >> 6`, `sra` arithmétique | CONFIRMÉ | `0x80035DF0-0x80035E1C`. La disposition (Flag +0, LimitX +4, LimitY +8, SpeedX +0xC, SpeedY +0x10, OffsetX +0x14, OffsetY +0x18, ReachX +0x1C, ReachY +0x20) est celle des neuf propriétés de `Alundra/Cameras/CameraSway.cs:33-41`. Je l'ai relue contre `0x8002C894` (le pas) et `0x80040534` (l'opcode 0x8E : speedX en +0xC, speedY en +0x10, limitX en +4, limitY en +8). |
| P7 identité changée : restaure, animation 0x34, phase + 1, rend 0 | CONFIRMÉ | `0x80035E20-0x80035E84` |
| P8 phase 2 rend 1 si l'animation ≠ 0x34 ; toute autre phase rend 0 | CONFIRMÉ | `0x80035C7C-0x80035CA0`, `0x80035E88-0x80035E9C` |
| P9 le compteur lu est celui d'avant l'incrément | CONFIRMÉ | `0x80036434-0x80036448` (appel du pilote) puis `0x80036480-0x8003649C` (incrément) |
| P10 pilote non appelé quand `hero.+0x20 ≠ 0` ou sous `& 0x48` | NON REFAIT | périmètre de h0a ; non porteur ici |
| P11 aucun tirage, son ou lecture du pad dans le pilote | CONFIRMÉ | seuls appels : garde, naissance |

### 2.2 E28, l'annulation, le sprite : CONFIRMÉ, avec deux compléments

- **E1/E2/E4/E5 : CONFIRMÉ.** Huit tours, un pas de générateur avant chaque effet, `r = hi(s × 0x1801) + 0x320`, tables à pas de 8 octets, Z de l'entité sans `+0x100000`, `DestroyEntity(self, -1)`, tirage même si la création échoue.
  - Preuve : `0x8007FB90-0x8007FC3C`, aucun test de `v0` après le `jal 0x8003BDD8`.
  - Tables lues par moi dans l'EXE : `TabX[4k] = 0, −543, −768, −543, 0, 543, 768, 543` et `TabY[4k] = 512, 362, 0, −362, −512, −362, 0, 362`.
  - Les mêmes valeurs sont dans la DLL : `Alundra/Entities/AnimationTables.cs:71` et `:82`, indices 0, 4, ..., 28, extraits et comparés par script. `NativeEffects.RingOffsetX/Y` en est une copie privée (`Alundra/Rendering/Effects/NativeEffects.cs:21-23`).
- **E6 (aucun tirage caché dans la création ni la mise à jour des effets) : CONFIRMÉ, plus fort que la découverte.** Mon `reach.py` suit les branches, les `jal` ET les tables de saut :
  - depuis `0x8003BDD8` : 103 instructions, 3 fonctions, aucun saut indirect, 0 accès à `0x80098708` ;
  - depuis `0x8003C410` (mise à jour de la réserve) : 324 instructions, 7 fonctions, 0 accès. Un seul saut indirect, `jr $t2` en `0x800831E4`, un tremplin BIOS (`addiu $t2,$zero,0xB0 ; jr $t2`) ;
  - depuis `0x80038E84` (passe des effets attachés aux entités, que le harnais bouche), avec sa table `0x800238DC` de 8 entrées résolue à la main : 339 instructions, 9 fonctions, 0 accès.
  - Dans le monde complet du harnais, `DestroyEntity` et son butin `0x80032B90` tournent pour de vrai (Contents 0). `rng1` y égale exactement 8 pas depuis `rng0` (clé refaite : `0xA468A517` → 8 pas → `0x4C00831F`).
- **Nouveau, FAIT : la création et la mise à jour de la réserve d'effets n'appellent aucune fonction de son.** Les seules fonctions atteintes sont les deux imprimantes d'erreur `0x800815EC/FC`, le tremplin BIOS et les trois fonctions de réserve. L'effet 19, animation 2, est donc silencieux : **ferme Q-H4-5 pour cet effet** (aucun chemin de code de la création ni de la mise à jour n'appelle un son).
- **A1/A2 : CONFIRMÉ.** Annulation sans garde ; type `0x15` au statut 2 vers 3 avec `+0x20 = 0` ; emplacements 1 à `compte` inclus (`0x80034E54-0x80034EB8`, modèle fuzzé). Le tampon n'est lu qu'ici et dans le pilote (§2.3).
- **A3 (type d'une entité de la page de carte = indice + 0x100) : CONFIRMÉ.**
  - Au binaire : `0x80039FC0-0x80039FC4`, `0x80039D68`.
  - Dans la DLL : `EntityScriptProxy.SpriteType` est « the original's own sprite type », avec `0x100` ajouté pour une carte (`Alundra/Entities/EntityScriptProxy.cs:127-141`, posé en `Alundra/Entities/Spawning/EntitySpawnFactory.cs:772`).
  - 19 cartes ont l'indice 21 (`alundra-project/Data/map-sprite-indices.json` : 138, 167, 174, 181, 191, 210, 224, 235, 246, 264, 266, 270, 280, 287, 295, 297, 361, 376, 482).
- **S1 (E28 n'est porté que par le sprite 21) : CONFIRMÉ, plus large.** 484 fichiers `map_*.json` balayés ; seul l'enregistrement global 21 porte `ProgramDeactivate = 28`.
- **S2/S3/S4 : CONFIRMÉS** sur l'extraction (`sprite21_dump.txt`).
  - 40 images : 37 de 3 ticks + une de 4 + une de 5 + une terminale de délai 0 = 120 ticks.
  - Boîte de 30×18×127 à 178×158×127 à l'image 18, puis constante.
  - Fiche 21 : une attaque, type 10, attribut 10, puissance 128.
  - Flags `0x2044` = `HitsClassA 0x4 | DeactivateOnAnimationEnd 0x40 | NoObstacleSlide 0x2000` (`Alundra/Entities/EntityFlags.cs:53,83,130`), donc `ClassMaskOf = 1` (`Alundra/Combat/HitPass.cs:43-50`) ; pas de bit `Gravity 0x100`.
- **S5 (le héros n'est jamais touché) : CONFIRMÉ.** Le masque de l'attaquant est ClassA seul ; le héros est ClassB.
- **D1 (128 par coup, repris tous les 25 ticks) : CONFIRMÉ** sur ma clé refaite (rejeu du vrai code : résolutions de 128, écritures espacées de 25).
- **D3 « 3 étincelles à 2 tirages, 6 par tick de coup » : RÉFUTÉ.** Sur la clé refaite, chaque tick de coup fait **9 tirages** (3 étincelles × 3 nombres) et crée 4 effets (3 étincelles + l'effet attaché 4). La DLL dit la même chose (`NativeEffects.CreateHitSparks` : « three numbers are drawn from GameRandom in a row » par étincelle créée). Pas de conséquence de fond, mais le chiffre est faux dans la découverte.
- **E3 (tables déjà dans la DLL) : CONFIRMÉ** (ci-dessus).
- **T1, T3 : CONFIRMÉS.**
  - L'appel du suivi de caméra `0x8002CDA0` est en `0x8002BDB8`, dans la fonction `0x8002BD60` appelée en `0x8002C3FC` AVANT la fonction de trame `0x8002BAEC`.
  - Cette fonction de trame contient la mise à jour du monde `0x8002E38C` puis, en fin, le pas du générateur `0x8002BD0C-0x8002BD34`.
  - Le pas du tremblement `0x8002C894` est appelé en `0x8002CDD8`, en tête du suivi.
  - Dans la DLL le pas vient après tous les ticks de l'image (`Alundra/Cameras/CameraDirector.cs:214-236`, appelé après `RunFrame`, `Alundra/Maps/WorldProxy.cs:2692`) : un tick d'avance, comme le dit Q-H4D1-1.
- **T2 (le tampon n'a que des sites du feu 2) : CONFIRMÉ** par mon balayage (§2.3).

### 2.3 Balayages de références (`c/out_*.txt`)

| Adresses | Fonctions qui les touchent |
|---|---|
| tampon `0x80127138..0x80127157` | la région de l'annulation `0x80034E08` (8 accès, que le balayage rattache au prologue précédent `0x80034D2C`) et le pilote `0x80035C64` (23) : rien d'autre |
| pointeur et identité `0x80127124`, `0x80127128` | **uniquement `0x80035C64`** (5 accès) |
| bloc `0x800E4338..` | le pas (région `0x8002C708-0x8002C9D8`), le chargement de carte `0x8002CD54`, le suivi `0x8002CDA0`, le pilote, l'annulation, les opcodes `0x8E/0x8F` (`0x800404A8`). S'y ajoutent une dizaine de fonctions de la table des handlers (`0x80061D14` à `0x8007F974`) que je n'ai PAS ouvertes (NON ÉTABLI ce qu'elles font du bloc ; hors tranche : elles ne touchent ni le tampon ni le pointeur). |
| mot du générateur `0x80098708` | une centaine de fonctions (c'est le générateur partagé) ; aucune n'est atteinte depuis la création ni la mise à jour des effets (§2.2) |

Conséquence (FAIT sur le binaire, INFÉRENCE sur l'écriture du plan) : les huit pilotes ont chacun leurs propres globaux. Balayage de `0x80127100..0x80127138` : `0x80127118` eau 1, `0x8012711C/20` feu 1, `0x80127124/28` feu 2, `0x8012712C` vent 1, `0x80127130/34` vent 2. Le champ « commun » `ItemSpawnedEntity` proposé tient parce qu'un seul objet est en cours, mais il ne correspond pas à un mot commun du binaire. Ne pas le justifier par « chacun écrit avant de lire ».

## 3. RÉFUTÉ (P1) : la chronologie de la clé, les huit positions, les ticks de coups

**Le défaut.**
- Le monde du harnais d'e20-h4 met à zéro les 64 emplacements d'entités (`h4oracle/world.py:106`, `m.zero(ENT0, 64 * STRIDE)`) et le gabarit (`spellworld.py:59`, « BSS zero »).
- L'initialisation de chargement de carte du binaire, `0x8003B24C-0x8003B2D8`, copie le gabarit dans les 64 emplacements ET écrit **`+0` = l'indice de l'emplacement** (`sw $s0,($t0)` en `0x8003B2BC`).
- Le gabarit porte `+0x48 = −1` (`0x8003B218-0x8003B21C`). L'init remet aussi le compte `[0x80134360]` à 0 (`0x8003B2D0-0x8003B2D8`).
- Le harnais d'e20-e0a fait déjà ce qu'il faut (`docs/plan-e20-annexe/e20-e0a/oracle/espawn.py:75-90`, `m.poke(ent(i), i)`, « the map-load init 0x8003B24C copies the template into all 64 slots »).

**Pourquoi cela change la vie de la colonne.**
- À la naissance, `InitializeEntity` `0x80039D04` fait `si compte < [entité+0] alors compte = [entité+0]` (`0x80039D20-0x80039D3C`).
- Avec `+0 = 0` (harnais), le compte ne monte pas. La boucle des statuts de `0x800386D0` lit le compte APRÈS le pas du héros (`0x800386FC-0x80038708`) : elle ignore donc le nouveau-né pour ce tick, qui ne passe au statut 2 qu'au suivant.
- Avec `+0 = indice` (jeu réel), le compte monte. Le nouveau-né prend part au tick de sa naissance : statut 1 puis 2 dans le même tick, horloge d'animation comptée.
- C'est ce que dit l'ADR-0065 pour la DLL (« an entity created inside MovePlayer ... is processed by the passes of the very tick that sees it born »). C'est aussi ce que fait `RunEntityBlock` (`Alundra/Maps/WorldProxy.cs:2924-2938`, commentaire en `:2930` : « an entity MovePlayer made (a shot, an item) is born in the pass of the hero, which comes first, and runs in this very tick »).
- Les tests de l'onde de l'épée le confirment côté DLL (ADR-0071 : drapeau de fin à S+25 pour une animation de 26 ticks, `AlundraWaveSwordLifeTests.cs`, statut 2 à S).

**Preuve par rejeu.** `h4o/fire2_key_c.py` est la copie du script de la découverte plus, avant la première image :
- `poke(0x80134368+0x48, −1)` ;
- pour les emplacements 1 à 63, `poke(ent(i), i)` et `poke(ent(i)+0x48, −1)`.

Rien d'autre ne change. `FAITHFUL=0` redonne exactement la clé publiée (200 lignes égales, vérifié).

| Événement (S = 34, tick de naissance) | Clé publiée (zéros) | Clé refaite (fidèle) |
|---|---|---|
| statut du sprite 21 à la fin du tick 34 | 1 | **2** |
| indicateur de fin d'animation `+0xAC` levé | 154 (S+120) | **153 (S+119)** |
| E28 : 8 tirages, 8 effets, statut 4 | 155 (S+121) | **154 (S+120)** |
| case recyclée, identité changée, bloc restauré, héros en 0x34, phase 2 | 156 (S+122) | **155 (S+121)** |
| héros en animation 0 | 182 | **181** |
| phase 2 rend 1, objet libéré | 183 | **182** |
| vie du sprite (ticks avec état) | 122 | **121** |

Les relations d'E28 sont conservées : un tick après l'indicateur de fin ; le pilote écrit encore le bloc au tick d'E28 (c = 153 → LimitY = SpeedY = 9, LimitX = SpeedX = 2) ; la restauration a lieu au tick suivant. Seuls les numéros bougent d'un tick. La relation durée-fin est celle de la DLL pour la vague (26 ticks → drapeau à S+25) : 120 ticks → **S+119**.

**Les huit effets d'E28 refaits.** Tick 154, `rng0 = 0xA468A517`, `rng1 = 0x4C00831F`, `rng2 = 0xEC3BCBAA` après le pas de fin de trame. Graine de départ `0x12345678` au tick 1, Z brut 2. Vérifiés par mon modèle d'E28.

| k | X | Y | Z |
|---|---|---|---|
| 0 | 6553600 | 9383424 | 2 |
| 1 | 3421033 | 8641978 | 2 |
| 2 | 3294976 | 6553600 | 2 |
| 3 | 3333067 | 4406578 | 2 |
| 4 | 6553600 | 5288960 | 2 |
| 5 | 7846483 | 5691678 | 2 |
| 6 | 10161664 | 6553600 | 2 |
| 7 | 7978432 | 7503488 | 2 |

Ce ne sont PAS les positions de la découverte ((6553600, 9507328), (4249651, 8089566), ... au tick 155 avec `rng0 = 0xC4F6D6C2`) : elles viennent d'un état du générateur pris un tick trop tard.
- Le seul tick à tirages de la clé refaite est le **154** (8 tirages) ; `rng0(t+1) = pas(rng1(t))` partout (vérifié).
- Les deux vecteurs d'E28 « graine de la clé » de `vec_e28_8007FB58.json` portent la mauvaise graine (`0xC4F6D6C2`). Les garder comme vecteurs ordinaires et ajouter celui de `0xA468A517`.

**Autres clés** (comparées ligne à ligne, clé refaite contre clé publiée).

| Clé | Verdict |
|---|---|
| `fire2_hurt60` (annulation en vol), `fire2_hurt20`, `fire2_hurt20_zerobuf`, `fire2_hurt20_stale` | **identiques, CONFIRMÉES** telles que publiées (0 ligne différente sur 230, 230, 60, 60 ticks). hurt60 : premier effet (6553600, 9487360, 2), deuxième (4029736, 8236176, 2), `rng0(60) = 0x85E33D01`. |
| `fire2_cast_pf`, `fire2_cast_nopf`, `fire2_cast_preset_block`, `fire2_hurt170` | **RÉFUTÉES** (lignes 154, 155 ; pour la clé du préréglage, aussi 181, 182) ; à remplacer par `keys_faithful/` |
| `fire2_targets_pf` | **RÉFUTÉE**. Écritures de coup (140,100) aux ticks **42, 67, 92, 117, 142** (et non 43, ...) ; (100,140) : **54, 79, 104, 129** ; (180,100) : **72, 97, 122, 147**. Résolution un tick après l'écriture (43, 68, ...), 128 par coup. Le premier coup est à S+8, pas S+9 ; le contrôle « image 3 » de la découverte tient avec le compteur du tick de naissance déjà compté (ADR-0071). |

**Portée au-delà de h4d1 (INFÉRENCE appuyée par le mécanisme).**
- Toute entité née dans le pas du héros par le harnais d'e20-h4 a sa vie décalée d'un tick de la même façon. Les chiffres d'e20-h4 `report.md` lignes 178 à 181 (« feu 2 ... vie 122, total 183 », vent 2 « 182 de vie ») sont concernés.
- h4c1, h4c2, h4d2, h4e2... doivent refaire leurs clés avec cette initialisation avant leur T0.
- Les vecteurs de fonctions de `e20-h4b/bin/vec` ne sont pas touchés (une fonction, pas un monde). `vec_slotpass.json` documente déjà « +0 = slot index ».

## 4. Les clés d'oracle : existence, adéquation au banc

| Clé proposée | Existe ? | S'applique au banc DLL ? | Verdict |
|---|---|---|---|
| cast complet `_pf` | oui (publiée), **fausse** ; refaite : `keys_faithful/fire2_cast_pf.json` | héros (100,100), direction 0x18, graine `0x12345678` au tick 1, champ plat, aucune autre entité (point 10 du §1.20 : retirer les entités de la carte 14) | **REFAIRE** (remplacer par la mienne) |
| annulation en vol `fire2_hurt60` | oui, **juste** | pose de `TargetAnimationId = 0x31` avant le tick 60 : la DLL a le même site (`PlayerManager.cs:353`, après `Create(FreeMode)` ; le site de blessure `0x80031C6C` est E20.c2b, hors tranche) | CONFIRMÉE |
| avant naissance (3 variantes) | oui, **juste** | idem | CONFIRMÉES |
| `fire2_hurt170` | oui, fausse aux ticks 154-155 | idem | REFAIRE (refaite : `keys_faithful/fire2_hurt170.json`) |
| `fire2_cast_preset_block` | oui, fausse (ticks 154, 155, 181, 182) **et inadaptée** (§5) | non tel quel | REFAIRE avec un préréglage à limites nulles (§5) : `keys_faithful/fire2_cast_preset_limit0_pf.json` |
| `fire2_targets_pf` | oui, fausse d'un tick | blocs `alundra_25` posés ClassA à la main : adéquat. La fiche d'équilibrage des cibles du harnais est synthétique (« toutes réponses 0x10 »), alors que la DLL lit la fiche exportée 25 : l'écriture de coup n'en dépend pas, la résolution oui (hors tranche). | REFAIRE (refaite : `keys_faithful/fire2_targets_pf.json`) |
| vecteurs d'E28 (202) | oui | rejouables par un adaptateur image → proxies | CONFIRMÉS (mon modèle : 202/202) |
| vecteurs du pilote (60 + 1) | oui | voir §6 | CONFIRMÉS (fuzz 6 000) |

**Ajout utile que la découverte n'a pas** : des cibles aux bords de la boîte maximale, rejouées sur le vrai code (`keys_faithful/fire2_targets_edge_pf.json`).
- Un bloc ClassA en **(200,100)** reçoit des coups aux ticks **87, 112, 137** (S+53, l'image 18 est atteinte) ; en **(201,100)**, aucun.
- En **(100,186)** : 87, 112, 137 ; en **(100,187)**, aucun.
- Le test de croissance de la boîte de (w6) (« une cible hors de la boîte maximale ne reçoit aucun coup ») gagne ainsi une borne exacte à un pixel. Il tue le mutant « la boîte cesse de grandir avant l'image 18 ».

## 5. RÉFUTÉ (P2) : (w1) et (w4) comparent des mots que la DLL ne peut pas égaler

**FAIT.**
- Le harnais de la découverte n'exécute jamais le pas du tremblement `0x8002C894` (ses passes sont celles de `0x8003B388` seules).
- Les clés gardent donc `OffsetX/Y` et `ReachX/Y` à leur valeur de départ : `[1,0,2,0,2,0,0,0,0]` partout, et `3, −2, 1, 0` constants dans la clé du préréglage.
- La DLL, elle, appelle `CameraSway.Step` une fois par tick, toujours, après le bloc d'entités de l'image (`CameraDirector.cs:214-236`, y compris sans caméra). Dès la frame 34 : Flag 1, LimitY = SpeedY = 2 → `OffsetY = −2`, `ReachY = 1` au tick 35, etc.

**Conséquences.**
- **(w1)** « les mots du bloc, le tampon » aux ticks 1 à 183 : entre les ticks 34 et 154, seuls **Flag, LimitX, LimitY, SpeedX, SpeedY** sont comparables à la clé. `OffsetX/Y` et `ReachX/Y` doivent être comparés à un rejeu de `CameraSway.Step` (déjà épinglé par les tests K1 contre `vec_sway`), pas à la clé. Après la restauration (tick 155), les neuf mots sont de nouveau comparables (bloc restauré = tampon = zéros).
- **(w4)** « le bloc est restauré à l'identique, Offsets et Reach compris » : avec le préréglage `[1,5,6,7,8,3,−2,1,0]`, la DLL fait tourner `Step` 33 fois avant la capture (Flag 1, limites non nulles). Le tampon contient les Offsets/Reach déjà avancés, pas `3, −2, 1, 0`. Le test échouerait pour une raison étrangère au code testé.
- **Correction proposée, vérifiée sur le vrai code** : un préréglage à **limites nulles** `[1, 0, 0, 7, 8, 0, 0, 1, 1]` (Flag 1, LimitX = LimitY = 0, SpeedX = 7, SpeedY = 8, Offsets 0, Reach 1 et 1).
  - `Step` avec LimitX = 0 remet OffsetX à 0, avec LimitY = 0 remet OffsetY à 0, et ne touche pas Reach. Le bloc de la DLL reste exactement ce préréglage jusqu'au tick 33.
  - La capture donne le préréglage, et la restauration (tick 155) rend `[1,0,0,7,8,0,0,1,1]` à l'octet près, Reach compris. Clé : `keys_faithful/fire2_cast_preset_limit0_pf.json` (tick 34 : tampon = préréglage ; ticks 155 à 182 : bloc = préréglage).
  - Le test reste réfutable : capture après l'écriture des limites → tampon `[1,0,2,0,2,...]` ≠ préréglage ; Reach non restaurés → rouge ; tampon gardé par « non nul » → rouge.
- **Lecture** : au point `TickTraceForTests("inventory")` du tick (après le bloc d'entités, avant le pas de l'image), comme proposé. Pour comparer « le bloc juste avant la capture », le point `"movePlayer"` du tick 34 existe (`WorldProxy.cs:2924`, `TickTraceForTests?.Invoke("movePlayer")`).

## 6. Atterrissage dans la DLL : ce qui est juste, ce qui manque

**CONFIRMÉ (citations relues au HEAD).**
- `SpawnEntityBySpriteIndex` : `WorldProxy.cs:3488`. `JoinsBirthTick` et `HasLogicalZ` posés en dernier (`:3572-3573`). Z en repère DLL, `unchecked(z + 1)` en `:3549`.
- `RecycleDestroyedEntities` (`:3265`, appelé `:2572` après les événements de carte, avant `RunEntityBlock`) ; `Slot = -1` après recyclage.
- `DestroyEntity(entity, effectId)` (`:3301`) ; `IEntityWorldContext.SpawnEntityBySpriteIndex` (`:81`).
- `EffectStage.Emit/Queue/RunFrame` : les opérations d'un tick s'appliquent avant la mise à jour de la réserve de ce tick. `EffectReservoir.Create(bool, int, int, int, int, int)` (`:101`).
- Registre : `NativeSpriteRegistry.cs:36-43/54-67/70-72` (7 clés). Recherche : `EventProgramRunner.cs:300-301` (`(entity.ProgramIndexes[slot] & 0x7f) == 0`, `NativeHandlersEnabled`, type listé). `IsInlineDeactivateCode` ne couvre pas 28 (`:328`).
- `PlayerManager.cs:353` (`UpdateItemUseDriver`), `:932` (`TryUseItem` bouchon), `:937`, `:1507-1509`, `:740-742`, `:620-621`.
- Zone « Hero only » `EntityScriptProxy.cs:432-442`, `Slope_18c :474`, `TargetAnimationId :158`.
- Six `ResetSingletons` : `AlundraGameTimeTests.cs:33`, `AlundraNativeEffectTests.cs:49`, `AlundraSaveGameLoadResetTests.cs:34`, `AlundraSaveGameProductionTests.cs:48`, `BackdropWarpDepartureFreezeTests.cs:224`, `SaveGameDirectorTestSupport.cs:120`. Un seul appelle `CameraSway.Instance.ResetForTests()` (`:134`).
- **Et** `ArcRun.ResetAll` (`AlundraArcSupport.cs:378-385`, via `using static SaveGameDirectorTestSupport`) l'appelle à la construction et au `Dispose`. Tout test à base d'`ArcRun` remet donc le tampon à zéro dès que `ResetForTests` le fait. Le plan peut le dire : c'est plus fort que « précaution ».
- Les deux épingles du registre : `AlundraNativeSpriteRegistryTests.cs:55` (`6 + 1`) et `AlundraWaveSwordLifeTests.cs:181-183` (`3 + 3 + 1`) : CONFIRMÉES.

**MANQUE / À CORRIGER.**

1. **(P2) `Alundra/Rendering/Effects/NativeEffects.cs` n'est pas dans le périmètre.**
   - Le point 8 du bloc commun du §1.20 l'exige pour un créateur d'effets (« le créateur y appelle `CreatedForTests`, `:96`, comme ceux du héros : le point d'observation des tests »).
   - Sans ce point, (e1) (« les 8 appels d'effet (19, 2, X, Y, Z) ») et (w1) n'ont pas où lire les positions.
   - Il faut choisir et écrire : (a) un créateur nommé dans `NativeEffects.cs` appelant `CreatedForTests` (modèle `CreateSprintBounce`), ou (b) la lecture de `Reservoir.ActiveSlots` après `RunFrame`. L'option (b) marche (forces nulles, mais l'emplacement a déjà reçu sa première mise à jour) et doit alors être dite.
2. **(P2) Identité côté DLL.**
   - « Identité changée ⇔ `Slot < 0` » est faux pour un proxy de montage de test : `EntityScriptProxy.Slot` vaut −1 pour « a test montage, a refused apparition, a recycled entity » (`EntityScriptProxy.cs:78-82`).
   - Les vecteurs de (d1) doivent donc poser `Slot` explicitement pour « vivant ».
   - Mieux : tester `Status == Destroyed || Slot < 0` après recyclage (le recyclage pose les deux, `WorldProxy.cs:3281` et `:3285`), pour ne pas confondre un vrai mort avec un montage.
3. **(P3) Mutant équivalent.** « `−1` retiré de `DestroyEntity(−1)` » ne change rien d'observable sur le sprite 21 (`BreakEffect = 0` ; `DestroyEntity(e, 0)` et la surcharge à un argument font les mêmes écritures). « Tous tués » est impossible. Pour le tuer, poser `BreakEffect = 8` sur le proxy de (e3) (le −1 lit l'en-tête, le 0 non) ; sinon, le déclarer équivalent.
4. **(P3) Preuves de retrait inexactes.**
   - Phase B : « remettre l'entrée 0x30 au bouchon rend rouges exactement (w1) à (w7) et (w9) » omet (w8), dont la première moitié (le Rond avec l'objet 0x30 fait naître le sprite 21) devient rouge.
   - Phase A : « neutraliser E28 rend rouges (e1) à (e4) ». (e5), qui lance E28 par `RunSpriteEvent` sur un statut 3, devient rouge aussi si elle observe un effet ou la destruction. Écrire « (e1) à (e5) » ou borner (e5) à l'inscription seule.
   - Phase A : « neutraliser le tampon rend rouges (b1), (b2), (d1), (w3), (w4) ». (w3) et (w4) sont des tests de la phase B ; en phase A seuls (b1), (b2), (b3), (d1) existent.
5. **(P3) Recette.** `arsenal-b-m14` (`Alundra/SaveGames/TestSaves.cs:125`) porte les livres **44, 46, 48, 50** (commentaire `:122` : « the books 44, 46, 48, 50 win over the scrolls of their slot »), pas « les tomes 43 à 50 », et pas le 47. La phrase « si le Rond tient 47 ... promeut vers 48 » ne s'applique donc pas à cette sauvegarde. Le tome 48, le bâton 7 et MP 4/4 sont bien là (`TestSaves.cs:93-98`, `:125`).
6. **(P3) (g)** « aucune des 30 cartes de la chaîne ne donne le tome 48 ; coffre carte 448, entité 45 » : NON ÉTABLI par moi (non porteur pour la tranche).
7. **(P3) Temps de la recette.** « Le héros est immobile de 1 à 182 (3,64 s) » devient 1 à 181 (3,62 s). La colonne grossit toujours 120 ticks (2,4 s).
8. **(P3) T0 (a).** « Les sorties doivent être identiques à celles du §7 » : après correction, la sortie de `trace_cast.py 0x30` (156/182/183, vie 122) n'est plus la référence. La référence devient `keys_faithful/`, à reproduire par `fire2_key_c.py --pf` et `fire2_targets_c.py --pf` (deux exécutions identiques).

## 7. Tests proposés : réfutabilité et preuves de retrait (après les corrections ci-dessus)

| Test | Réfutable ? | Remarque |
|---|---|---|
| (e1) 202 vecteurs d'E28 | oui | Mutants table (indice k au lieu de 4k, `+0x320`, borne 0x1800, produit signé, X/Y permutés), Z (±0x100000, sans +1), animation/effet : tous tués par les 202 vecteurs (graines hautes pour le produit signé). |
| (e2) réserve pleine : 8 tirages, 0 effet, destruction faite | oui | tue « tirage sauté si la création échoue » |
| (e3) destruction | oui si `BreakEffect = 8` posé (point 3 ci-dessus) | |
| (e4) exactement 8 tirages | oui | sonde du générateur (`RandomShadow`) |
| (e5) inscription `(4, 28)` pour {21} | oui | `TryGet(4, 28, 0x115)` faux tue une inscription étendue aux types de la page de carte |
| (e6) tables à la main | oui | |
| (b1) à (b3) tampon | oui | |
| (b4) bloc restauré Flag 0 / Flag 1 | redondant avec K1 mais inoffensif | |
| (d1) 61 vecteurs du pilote | oui | L'objet en cours (0, 43, 48 dans les vecteurs) doit être posé sur le héros : l'annulation générale aiguille dessus. |
| (d4) bloc du tick de naissance `[1,0,2,0,2,0,0,0,0]` | oui | |
| (d5) `FlagToDestroy` non recyclé vivant ; recyclé = mort | oui | **tick 154 (écriture) et 155 (restauration)** après correction |
| (d6) paliers `c >> 4`, `c >> 6` | oui | (0,0), (1,0), (3,0), (4,1), (7,1), (8,2) pour c = 15, 16, 63, 64, 127, 128 : arithmétique vérifiée |
| (c1), (c2) annulation | oui | L'entité de la page de carte d'indice 21 doit avoir `SpriteType = 0x115` ET `SpriteTableIndex = 21`. |
| (w1) cast complet | oui, après correction §3 et §5 | ticks de la clé refaite ; 5 mots du bloc seulement entre 34 et 154 |
| (w2) hurt60 | oui | clé juste |
| (w3) hurt20 (3 variantes) | oui | clé juste |
| (w4) aller-retour | oui avec le préréglage à limites nulles | |
| (w5) hurt170 | oui | clé refaite |
| (w6) cibles | oui | ticks refaits ; ajouter les paires de bord (200,100)/(201,100) et (100,186)/(100,187) |
| (w7) 2 et 3 ticks par image | oui | seulement les ticks de naissance, de fin (S+119), d'annulation, la libération, le nombre d'effets et de tirages |
| (w8) câblage | oui | |
| (w9) naissance refusée (optionnel) | oui | |

Mutants listés : tous tuables sauf `−1` retiré (point 3). Mutants à ajouter :
- « boîte qui cesse de grandir avant l'image 18 » (tué par les paires de bord) ;
- « sprite 21 qui ne prend pas part au tick de sa naissance » (tué par (w1) : statut 2 et drapeau à 153) ;
- « Offsets/Reach non restaurés » (tué par (w4) à limites nulles).

## 8. Mon verdict sur les tâches proposées

- **T0 (annexe, rejeux, clés) : à refaire.**
  - (a) Rejouer les harnais ET la correction d'initialisation.
  - (b) Verser `keys_faithful/` (+ les deux scripts `fire2_key_c.py`, `fire2_targets_c.py`) à la place des clés de la découverte pour les cinq clés réfutées. Garder hurt20 et hurt60.
  - (d) Sonde : drapeau de fin à **S+119**, E28 et statut 4 à **S+120**, case libre à S+121, statut 2 à la fin du tick de naissance, Z de naissance = Z héros + 1 unité brute, écritures de coup aux ticks refaits.
  - (e) Sonde du générateur : état de la DLL égal à `rng0(t+1)` de la clé refaite, tick **154** à 8 + 1 pas.
- **T1 phase A : correct**, avec le point 1 (créateur d'effets dans `NativeEffects.cs`) ajouté au périmètre, les points 2 à 4 appliqués, et la sonde du pas de caméra (§5) écrite.
- **T1 phase B : correct après correction** des ticks, du préréglage de (w4), de la restriction de (w1) aux cinq mots, et des paires de bord de (w6).
- **T2 : correct.** Mutants : retirer l'équivalent, ajouter les trois ci-dessus. Les deux vérificateurs frais, dont l'un sur la clé et son initialisation.
- **Q-H4D1-1 (pas du tremblement un tick en avance) : réponse (A), défaut juste.** L'écart de 20 ms est confirmé par T3.
- **Q-H4D1-2 (tampon de session) : réponse (A), fidèle.** Confirmé : `OnMapLoad` ne touche pas le tampon, aucun autre site du binaire ne l'écrit.
- **Aucune dépendance à f5 ni f6a : CONFIRMÉ.**
  - Le sprite 21 a une vitesse d'animation nulle (`AnimSets` : `Speed 0`, `IsZForceApplied 0`) et pas de gravité.
  - La règle D-F5-5 (clé de vitesse = `TargetAnimationId` pour `HasLogicalZ`) ne le déplace pas.
  - Les attributs de tuile des entités marquées à gravité (f6a) ne le concernent pas.
  - Si f5 ou f6a précèdent h4d1, relancer la sonde T0 (d) (Z et position constants). Le pas vertical d'une entité marquée sans gravité et à force nulle laisse `PosZ` inchangé (`Alundra/Physics/ScriptedMotion.cs:465-515`, branche `PosZ += tickForce`).

## 9. INCONNU et INFÉRENCE restants

- **INCONNU** :
  - le rendu exact du tremblement en jeu ;
  - le verdict de `check_names.py` pour `FireTome2Ai` (non lancé) ;
  - si le menu du Rond de la sauvegarde de test sélectionne bien 48 ;
  - (g).
  - Le son de l'effet 19, animation 2 : aucun chemin de code n'en joue (§2.2), donc rien à porter.
- **INFÉRENCE** :
  - le décalage d'un tick touche aussi les clés des autres tomes d'entités du harnais d'e20-h4 (même mécanisme, non rejoué ici) ;
  - `Status == Destroyed || Slot < 0` équivaut au changement d'identité du binaire (le recyclage DLL pose les deux, `WorldProxy.cs:3281`, `:3285`) ;
  - la DLL donnera S+119/S+120 pour la colonne (analogie avec la vague : même mécanisme, `SwitchedAtCreation`, ADR-0071), à confirmer par la sonde T0 (d).

## 10. Fichiers

Tous sous `C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/disc-h4el/h4d1-critic/` :

- `check.md`, `report.md` : ce rapport.
- `c/` :
  - `cmipsmini.py`, `crit_models.py`, `scan_refs.py`, `reach.py`, `cmp_all.py`, `cmp_keys.py`, `exelib.py`, `mipsdis.py` ;
  - `c/h4o/` : copie du harnais d'e20-h4 + `fire2_key_c.py` et `fire2_targets_c.py` (`FAITHFUL=1` par défaut ; `FAITHFUL=0` redonne la clé publiée) ;
  - `c/out_*.txt` : balayages de références.
- `keys_faithful/` : `.json` et `.txt` pour `fire2_cast_pf`, `fire2_cast_preset_block`, `fire2_cast_preset_limit0_pf`, `fire2_hurt170`, `fire2_hurt60`, `fire2_hurt20`, `fire2_hurt20_stale`, `fire2_hurt20_zerobuf`, `fire2_targets_pf`, `fire2_targets_edge_pf`.
- `crit_models_run.txt` (sortie du fuzz), `reach_create_effect.txt`, `reach_pool_update.txt`, `reach_active_effects_pass.txt`.
- `SHA1SUMS_keys_faithful.txt`, `SHA1SUMS_scripts.txt`.